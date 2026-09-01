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

        /// <summary>
        /// 창의 "전체 알파"를 Win32 스타일 비트 + GetLayeredWindowAttributes 조회 결과로부터 판정한다.
        /// OS 호출은 호출부(Win32WindowService)가 하고, 이 함수는 그 결과만 해석한다.
        ///
        /// 네 갈래의 근거:
        ///  1. <b>WS_EX_LAYERED가 없다</b> — 창에 전체 알파를 부여할 수단이 없다. 불투명(1.0).
        ///  2. <b>LWA_ALPHA로 알파가 설정돼 있다</b> — 그 값이 곧 macOS의 kCGWindowAlpha 대응물이다.
        ///  3. <b>조회는 됐지만 LWA_ALPHA가 없다</b> — 컬러키(LWA_COLORKEY)만 쓰는 창. 특정 색만 투명하고
        ///     전체 알파는 불투명이므로 1.0.
        ///  4. <b>조회가 실패했다</b> — 이 창은 <c>UpdateLayeredWindow</c>로 <b>픽셀별 알파</b>를 쓴다
        ///     (문서화된 동작: 그 경우 GetLayeredWindowAttributes는 실패한다). 전체 알파라는 단일 값이
        ///     존재하지 않으므로 알 방법이 없다. 여기서 <b>클릭 관통(WS_EX_TRANSPARENT)까지 켜져 있으면</b>
        ///     사용자가 만질 수조차 없는 순수 HUD/오버레이로 보고 0으로 판정한다 — 위 문서의
        ///     "전체화면 투명 오버레이가 아래 발판을 전부 지운다" 시나리오가 정확히 이 조합이다
        ///     (우리 앱 자신의 오버레이도 이 조합이다. 자기 자신을 발판으로 삼지 않는 것과 같은 판단).
        ///     클릭 관통이 아니라면 사용자가 실제로 조작하는 창이므로 보수적으로 불투명(1.0)으로 둔다 —
        ///     <b>조회 실패를 이유로 멀쩡한 창을 발판에서 지우지 않는다</b>(IsCloaked와 같은 보수 원칙).
        /// </summary>
        public static float ResolveWindowAlpha(int exStyle, bool layeredAttributesQuerySucceeded,
            uint layeredFlags, byte layeredAlpha)
        {
            if ((exStyle & WsExLayered) == 0) return 1f;

            if (layeredAttributesQuerySucceeded)
            {
                return (layeredFlags & LwaAlpha) != 0 ? layeredAlpha / 255f : 1f;
            }

            return (exStyle & WsExTransparent) != 0 ? 0f : 1f;
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
                case WindowsFootholdRejection.TransparentAlpha: return "알파≈0(투명/비표시)";
                case WindowsFootholdRejection.TooSmall: return "너무 작음";
                case WindowsFootholdRejection.OffVirtualScreen: return "가상 화면 밖";
                case WindowsFootholdRejection.FullyOccluded: return "다른 창에 완전히 가려짐";
                default: return "알 수 없음";
            }
        }
    }
}
