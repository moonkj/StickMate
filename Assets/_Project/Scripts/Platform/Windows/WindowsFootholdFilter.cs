using UnityEngine;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// Windows 창 하나가 발판 후보에서 탈락한 사유. 진단 로그가 "왜 이 창이 사라졌는가"를
    /// 원격에서 특정할 수 있게 하기 위한 것이며, 문자열은 전부 상수라 열거 경로에 할당이 없다.
    /// </summary>
    public enum WindowsFootholdRejection
    {
        None = 0,
        NotVisible,
        Minimized,
        NoTitle,
        Cloaked,
        ToolWindow,
        SelfProcess,
        DegenerateRect,
        TransparentAlpha,
        TooSmall,
        OffVirtualScreen,
        FullyOccluded,
    }

    /// <summary>
    /// ★ 2026-08-31 (이월 결함 해소) — <b>Windows에 macOS의 알파 필터 대응물이 없었다.</b>
    ///
    /// ============================================================================
    /// 왜 이 클래스가 따로 있는가 (Win32WindowService 안에 두지 않은 이유)
    /// ============================================================================
    /// <c>Win32WindowService.cs</c>는 파일 전체가 <c>#if UNITY_STANDALONE_WIN</c>이다. 그래서 그 안에
    /// 판정 로직을 두면 이 개발 환경(macOS)에서는 <b>컴파일조차 되지 않아 테스트로 겨냥할 수 없다</b> —
    /// 이것이 정확히 <see cref="VisibleTopEdgeSolver"/>를 뽑아내게 만든 그 실패(플랫폼 전용 private
    /// 메서드에 갇힌 수정)와 같은 구조다. 같은 실수를 반복하지 않기 위해, OS 호출이 필요한 부분
    /// (스타일 비트/레이어드 알파를 <b>읽어오는</b> 일)만 Win32 쪽에 남기고 <b>판정</b>은 전부 여기로
    /// 뺀다. 이 파일에는 P/Invoke가 한 줄도 없으므로 컨벤션("Win32 P/Invoke는 Platform/Windows/
    /// 하위에만")을 어기지 않으면서 EditMode에서 그대로 실측된다.
    ///
    /// ============================================================================
    /// macOS와의 대응 관계 (MacWindowService.EnumerateFootholds의 필터와 1:1)
    /// ============================================================================
    ///   macOS <c>kCGWindowAlpha &lt; 0.05</c>        -> <see cref="ResolveWindowAlpha"/> + <see cref="MinWindowAlpha"/>
    ///   macOS "너무 작음"(60x40)                     -> <see cref="MinWindowWidth"/>/<see cref="MinWindowHeight"/>
    ///   macOS "화면(주 디스플레이) 밖"               -> 가상 화면(전체 모니터 외접 사각형) 밖
    ///   macOS <c>kCGWindowIsOnscreen=false</c>       -> Win32는 IsWindowVisible/IsIconic/DWM cloaked가 담당
    ///
    /// ============================================================================
    /// 이 필터가 왜 지금 중요한가 (가려짐 필터가 새로 만든 노출면)
    /// ============================================================================
    /// 2026-08-31 라운드가 Windows에 가려짐 계산을 도입하면서, "앞에 있는 창은 뒤 창의 상단선을
    /// 지운다"는 규칙이 생겼다. 그런데 <b>눈에 보이지 않는 전체화면 투명 창</b>(스트리밍 오버레이 /
    /// 접근성 도구 / 보안 툴의 HUD)이 하나라도 z-order 앞에 있으면, 그 창이 아래의 <b>멀쩡한 발판을
    /// 전부 삭제</b>해 캐릭터가 영원히 낙하한다. 가려짐 수정 <b>전에는 없던</b> 위험이므로 반드시
    /// 같은 계층에서 막아야 한다 — 여기서 탈락한 창은 발판이 되지도 않고 <b>가리지도 못한다</b>
    /// (Win32WindowService가 탈락 창을 아예 솔버에 넣지 않는다. macOS와 동일한 계약).
    /// </summary>
    public static class WindowsFootholdFilter
    {
        /// <summary>WS_EX_LAYERED — 이 비트가 없으면 창에 "전체 알파"라는 개념 자체가 없다.</summary>
        public const int WsExLayered = 0x00080000;

        /// <summary>WS_EX_TRANSPARENT — 마우스 입력이 그대로 통과하는 창(우리 오버레이가 켜는 바로 그 비트).</summary>
        public const int WsExTransparent = 0x00000020;

        /// <summary>LWA_ALPHA — GetLayeredWindowAttributes의 dwFlags가 이 비트를 가질 때만 알파값이 유효하다.</summary>
        public const uint LwaAlpha = 0x00000002;

        /// <summary>macOS <c>MinWindowAlpha</c>와 같은 값·같은 이유(거의 투명한 창은 사용자 눈에 없다).</summary>
        public const float MinWindowAlpha = 0.05f;

        /// <summary>macOS <c>MinWindowWidth</c>와 같은 값 — 캐릭터가 설 수 없을 만큼 작은 창은 후보에서 뺀다.</summary>
        public const float MinWindowWidth = 60f;

        /// <summary>macOS <c>MinWindowHeight</c>와 같은 값.</summary>
        public const float MinWindowHeight = 40f;

        // ============================================================================
        // ★★ 2026-09-01 — 제목 판정 + 제목 조회 예산 (블로킹 호출 제거 라운드)
        //
        // 배경은 Win32WindowService의 InternalGetWindowText 선언 문서에 있다. 요약하면
        // "제목이 있는가"를 묻는 방법이 창의 메시지 루프를 깨우는 것이어서, 남의 앱이 바쁘면
        // 우리 프레임이 그만큼 멈췄다(실기 최대 199ms/열거).
        //
        // 판정 자체를 여기 두는 이유는 이 파일의 다른 규칙들과 같다: Win32WindowService.cs는
        // 통째로 #if UNITY_STANDALONE_WIN이라 이 개발 환경(macOS)에서 컴파일조차 되지 않아
        // 테스트로 겨냥할 수 없다. OS에서 값을 '읽어오는' 일만 저쪽에 남기고 '해석'은 여기서 한다.
        // ============================================================================

        /// <summary>
        /// 창 제목 조회 결과(복사된 글자 수)를 "제목이 있는가"로 해석한다.
        ///
        /// <para><b>이전 판정과 동치인 이유.</b> 이전에는 <c>GetWindowTextLength(hWnd) == 0</c>이면
        /// 제목 없음이었다. 그 함수는 문서상 <b>실제 길이보다 큰 값을 돌려줄 수 있지만</b>
        /// (ANSI/DBCS 변환 여유분) <b>0을 돌려주는 경우는 캡션이 빈 문자열일 때뿐</b>이다.
        /// 지금 쓰는 조회는 버퍼에 실제로 복사한 글자 수를 돌려주므로, 캡션이 비어 있으면 0이고
        /// 한 글자라도 있으면 1 이상이다 — <b>"0인가 아닌가"라는 이 판정에서는 두 값이 완전히
        /// 같은 답을 낸다.</b> 캡션이 공백 문자만으로 이루어진 창도 이전과 똑같이 '제목 있음'으로
        /// 남는다(트림하지 않는다 — 판정을 조금이라도 바꾸면 발판 후보 집합이 달라진다).</para>
        ///
        /// <para>버퍼가 작아도 무방하다: 잘라서 복사한 글자 수를 돌려주므로 제목이 길든 짧든
        /// 결과의 부호는 같다. 그래서 호출부는 1글자 + 널 종단짜리 버퍼만 쓴다.</para>
        /// </summary>
        public static bool HasWindowTitle(int copiedCharCount) => copiedCharCount > 0;

        /// <summary>프레임 목표를 알 수 없을 때(예: vsync 위임으로 -1) 가정하는 fps.</summary>
        public const int DefaultTargetFrameRate = 60;

        /// <summary>
        /// 한 번의 열거에서 <b>제목 조회 전체</b>가 프레임 예산에서 가져가도 되는 몫.
        /// 1/8이면 60fps(16.7ms) 기준 약 2.08ms다 — 이 단계는 커널 구조체 읽기 수십 회일 뿐이라
        /// 정상이면 두 자릿수 마이크로초로 끝난다. 즉 이 문턱은 "약간 느림"이 아니라
        /// <b>"블로킹 성질이 되살아났다"</b>를 잡는 경보선이다.
        /// </summary>
        public const float TitleProbeFrameBudgetShare = 0.125f;

        /// <summary>
        /// 제목 조회 예산(ms)을 <b>프레임 예산에서 유도</b>한다. 숫자를 코드에 박지 않는 것이
        /// 이 저장소 규칙이고, 실질적 이유도 있다: Windows 저전력 등급에서 목표가 60 -> 30fps로
        /// 내려가면 프레임 예산이 33ms가 되므로 경보선도 함께 움직여야 같은 의미를 유지한다.
        /// </summary>
        /// <param name="targetFrameRate">
        /// <c>Application.targetFrameRate</c>. 0 이하(= vsync에 위임, 상한 없음)이면
        /// <see cref="DefaultTargetFrameRate"/>로 대체한다.
        /// </param>
        public static float DeriveTitleProbeBudgetMs(int targetFrameRate)
        {
            int fps = targetFrameRate > 0 ? targetFrameRate : DefaultTargetFrameRate;
            return 1000f / fps * TitleProbeFrameBudgetShare;
        }

        /// <summary>
        /// 창의 "전체 알파"를 Win32 스타일 비트 + GetLayeredWindowAttributes 조회 결과로부터 판정한다.
        /// OS 호출은 호출부(Win32WindowService)가 하고, 이 함수는 그 결과만 해석한다.
        ///
        /// ============================================================================
        /// ★★ 2026-09-02 — <c>WS_EX_TRANSPARENT</c>를 <b>무조건 탈락</b>으로 승격했다(리더 승인)
        /// ============================================================================
        /// <b>승격 전</b>에는 이 비트를 <b>네 번째 갈래 안에서만</b> 봤다 — 즉 "레이어드이고 + 조회가
        /// 실패했을 때"만 클릭 관통을 근거로 0을 돌려줬다. 그래서 아래 <b>세 조합이 그대로 발판이
        /// 되고 동시에 <u>남의 발판을 가릴 자격</u>까지 가졌다</b>(솔버는 두 자격을 같은 목록으로 본다):
        /// <list type="number">
        ///  <item><c>TRANSPARENT</c> 단독(레이어드 아님) — 1번 갈래에서 1.0.</item>
        ///  <item><c>LAYERED|TRANSPARENT</c> + <c>LWA_ALPHA</c>가 큰 값 — 2번 갈래에서 최대 1.0.</item>
        ///  <item><c>LAYERED|TRANSPARENT</c> + 컬러키만 — 3번 갈래에서 1.0.</item>
        /// </list>
        /// 판정 근거는 한 줄이다: <b>사용자가 클릭조차 할 수 없는 창은 사용자가 쓰는 창이 아니다.</b>
        /// 클릭 관통 창은 정의상 "아래 창을 위해 자리를 비켜 주는" 창이므로, 그 위에 캐릭터를 세우거나
        /// 그것으로 아래 발판을 지우는 것은 어느 쪽도 사용자가 보는 화면과 맞지 않는다.
        /// 이 승격은 macOS <c>layer != 0</c> 필터와 같은 방향이고(시스템/오버레이 레이어는 발판이
        /// 아니다), 새 OS API를 한 개도 늘리지 않는다 — <c>exStyle</c>은 이미 읽고 있던 값이다.
        ///
        /// <para><b>정직하게 남기는 과잉 제거 위험</b>: 클릭 관통이면서 <u>눈에는 보이는</u> 창
        /// (예: 클릭 관통 모드의 데스크톱 위젯)도 함께 빠진다. 다만 그런 창은 대부분
        /// <c>UpdateLayeredWindow</c> 픽셀별 알파를 함께 쓰므로 <b>승격 전에도 이미 4번 갈래에서
        /// 빠지고 있었다</b> — 이번 승격으로 새로 빠지는 것은 위 세 조합뿐이다.
        /// <b>실기 미확인</b>: 이 개발 머신에 Windows가 없어 실제 데스크톱에서의 탈락 목록은 확인하지
        /// 못했다. 확인 수단은 <c>[발판진단]</c> 로그의 알파 탈락 사각형 목록이다.</para>
        ///
        /// 남은 갈래의 근거(클릭 관통이 <b>아닌</b> 창에 대해서만 적용된다):
        ///  1. <b>WS_EX_LAYERED가 없다</b> — 창에 전체 알파를 부여할 수단이 없다. 불투명(1.0).
        ///  2. <b>LWA_ALPHA로 알파가 설정돼 있다</b> — 그 값이 곧 macOS의 kCGWindowAlpha 대응물이다.
        ///  3. <b>조회는 됐지만 LWA_ALPHA가 없다</b> — 컬러키(LWA_COLORKEY)만 쓰는 창. 특정 색만 투명하고
        ///     전체 알파는 불투명이므로 1.0.
        ///  4. <b>조회가 실패했다</b> — 이 창은 <c>UpdateLayeredWindow</c>로 <b>픽셀별 알파</b>를 쓴다
        ///     (문서화된 동작: 그 경우 GetLayeredWindowAttributes는 실패한다). 전체 알파라는 단일 값이
        ///     존재하지 않으므로 알 방법이 없다. 클릭 관통이 아니라면 사용자가 실제로 조작하는 창이므로
        ///     보수적으로 불투명(1.0)으로 둔다 — <b>조회 실패를 이유로 멀쩡한 창을 발판에서 지우지
        ///     않는다</b>(IsCloaked와 같은 보수 원칙). 클릭 관통인 경우는 위 승격 게이트가 이미 처리했다
        ///     (우리 앱 자신의 오버레이도 이 조합이다 — 자기 자신을 발판으로 삼지 않는 것과 같은 판단.
        ///      다만 실제 열거에서는 그보다 앞선 SelfProcess 필터가 먼저 걸러낸다).
        /// </summary>
        public static float ResolveWindowAlpha(int exStyle, bool layeredAttributesQuerySucceeded,
            uint layeredFlags, byte layeredAlpha)
        {
            // ★ 승격된 게이트. 레이어드 여부·조회 성공 여부·알파값과 <b>무관하게</b> 먼저 닫는다.
            //   이 한 줄이 위 문서의 세 조합을 동시에 막는다. 순서를 아래로 내리면 승격이 무효가 된다.
            if ((exStyle & WsExTransparent) != 0) return 0f;

            if ((exStyle & WsExLayered) == 0) return 1f;

            if (layeredAttributesQuerySucceeded)
            {
                return (layeredFlags & LwaAlpha) != 0 ? layeredAlpha / 255f : 1f;
            }

            // 여기 도달하는 창은 반드시 클릭 관통이 아니다(위 게이트가 이미 돌려보냈다).
            return 1f;
        }

        /// <summary>
        /// 스타일/알파 판정을 통과한 창의 <b>기하</b>를 검사한다(macOS 필터 순서와 동일:
        /// 알파 -> 크기 -> 화면 밖).
        /// </summary>
        /// <param name="hasVirtualScreen">
        /// 가상 화면(모든 모니터를 감싸는 외접 사각형)을 알아냈는지. false면 화면 밖 판정을 건너뛴다 —
        /// 조회 실패를 이유로 멀쩡한 창을 지우지 않는다. macOS의 <c>hasDisplay</c>와 같은 계약이다.
        /// </param>
        /// <param name="virtualScreen">
        /// 가상 화면 사각형. <b>주 모니터가 아니라 전체 모니터의 외접 사각형</b>이어야 한다 —
        /// 주 모니터로 자르면 보조 모니터 위의 멀쩡한 창이 통째로 사라진다(같은 이유로 발판
        /// 클리핑은 여전히 끄고 있다. Win32WindowService.EnumerateFootholds 주석 참고).
        /// </param>
        public static WindowsFootholdRejection ClassifyGeometry(Rect screenRect, float alpha,
            bool hasVirtualScreen, Rect virtualScreen)
        {
            if (screenRect.width <= 0f || screenRect.height <= 0f) return WindowsFootholdRejection.DegenerateRect;
            if (alpha < MinWindowAlpha) return WindowsFootholdRejection.TransparentAlpha;
            if (screenRect.width < MinWindowWidth || screenRect.height < MinWindowHeight)
            {
                return WindowsFootholdRejection.TooSmall;
            }
            if (hasVirtualScreen && !screenRect.Overlaps(virtualScreen)) return WindowsFootholdRejection.OffVirtualScreen;
            return WindowsFootholdRejection.None;
        }

        /// <summary>진단 로그용 한국어 사유. 전부 상수 문자열이라 호출해도 할당이 없다.</summary>
        public static string Describe(WindowsFootholdRejection rejection)
        {
            switch (rejection)
            {
                case WindowsFootholdRejection.None: return "채택";
                case WindowsFootholdRejection.NotVisible: return "IsWindowVisible=false";
                case WindowsFootholdRejection.Minimized: return "최소화";
                case WindowsFootholdRejection.NoTitle: return "제목 없음";
                case WindowsFootholdRejection.Cloaked: return "DWM cloaked";
                case WindowsFootholdRejection.ToolWindow: return "WS_EX_TOOLWINDOW";
                case WindowsFootholdRejection.SelfProcess: return "우리 자신의 창";
                case WindowsFootholdRejection.DegenerateRect: return "사각형 폭/높이 0 이하";
                // 2026-09-02: WS_EX_TRANSPARENT 승격 이후 이 사유는 두 원인을 합친다 —
                // (a) 실제로 알파가 문턱 미만  (b) 클릭 관통 창(알파를 0으로 판정). 로그를 읽는 사람이
                // (b)를 모르면 "알파가 0인 창이 왜 이렇게 많지?"에서 조사가 멈춘다.
                case WindowsFootholdRejection.TransparentAlpha: return "알파≈0(투명·클릭관통 오버레이)";
                case WindowsFootholdRejection.TooSmall: return "너무 작음";
                case WindowsFootholdRejection.OffVirtualScreen: return "가상 화면 밖";
                case WindowsFootholdRejection.FullyOccluded: return "다른 창에 완전히 가려짐";
                default: return "알 수 없음";
            }
        }
    }
}
