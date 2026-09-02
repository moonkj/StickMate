namespace StickMate.Platform
{
    /// <summary>
    /// 오버레이 창의 목표 상태(투명/항상위/클릭관통) <b>재적용 루프</b>가 지켜야 할 순수 규칙.
    /// UnityEngine 의존도 P/Invoke도 한 줄 없다 — Windows 실기가 없는 개발 머신에서 규칙 자체를
    /// 실행해 검증할 수 있어야 하기 때문이다(<see cref="OverlayBoundsFitPolicy"/>,
    /// <see cref="LayeredHybridPolicy"/>와 같은 설계).
    ///
    /// ============================================================================
    /// 왜 생겼는가 — 2026-09-02, 사용자 2회 신고 "윈도우 버전인데 여전히 사용할수록 렉생김"
    /// ============================================================================
    /// 실기 로그에서 오버레이 창 <b>폭이 재적용 1회당 정확히 1px씩</b> 줄었다(높이는 불변):
    /// <code>
    /// 재적용 1/5 ... windowSize=(2560.00, 1600.00)
    /// 재적용 2/5 ... windowSize=(2559.00, 1600.00)
    ///   ... 세션 동안 2560 -> 2550
    /// [프레임스파이크] 268ms 멈춤 — 백버퍼: 2560x1600 -> 2550x1600 (스왑체인 재생성)
    /// </code>
    /// 재적용 루프는 <b>크기를 한 줄도 대입하지 않는다.</b> 그런데도 창이 줄었다.
    ///
    /// ============================================================================
    /// 범인 — <c>isTransparent</c> 대입 한 번이 창을 네 번 리사이즈한다 (패키지 소스 실측)
    /// ============================================================================
    /// <c>UniWindowController.isTransparent = true</c>
    ///   -> <c>UniWinCore.EnableTransparent(true)</c>  (UniWinCore.cs:535)
    ///   -> <c>LibUniWinC.SetTransparent(true)</c>  <b>그리고</b>  <c>LibUniWinC.SetBorderless(true)</c>
    ///
    /// 그 <c>SetBorderless</c>가 <b>동등성 가드 없이</b> 매번 이것을 한다(libuniwinc.cpp:694~,
    /// kirurobo/UniWindowController main 브랜치 원본):
    /// <code>
    ///   int offset = 1;
    ///   if (bBorderless) { newStyle = (WS_VISIBLE | WS_POPUP); offset = -1; }
    ///   ...
    ///   SetWindowPos(hWnd, NULL, newX, newY, newW + offset, newH, SWP_FRAMECHANGED | ...);  // 폭 -1
    ///   SetWindowPos(hWnd, NULL, newX, newY, newW,          newH, SWP_FRAMECHANGED | ...);  // 폭 복구
    ///   SetWindowLong(hWnd, GWL_STYLE, newStyle);
    ///   SetWindowPos(hWnd, NULL, newX, newY, newW + offset, newH, SWP_FRAMECHANGED | ...);  // 폭 -1
    ///   SetWindowPos(hWnd, NULL, newX, newY, newW,          newH, SWP_FRAMECHANGED | ...);  // 폭 복구
    /// </code>
    /// 소스 주석이 이 흔들기의 목적을 직접 밝힌다: <i>"Unity2019까지의 순서로는 Unity2020에서 크기가
    /// 되돌아간다. 크기 변경을 반복하거나 나중에 스타일을 바꿔 본다."</i> 즉 <b>Unity가 크기 변경을
    /// 알아채게 하려고 일부러 폭을 1px 흔드는</b> 코드이며, 그래서
    /// <list type="number">
    ///   <item>흔들기는 <b>폭에만</b> 걸린다(높이는 <c>newH</c> 고정) — 실기에서 높이 1600이 불변인 것과 일치.</item>
    ///   <item>보더리스일 때 <c>offset = <b>-1</b></c>이라 중간 상태가 항상 <b>더 좁은</b> 쪽이다 —
    ///         잔차의 부호가 음수인 것과 일치.</item>
    ///   <item>다음 호출의 기준값을 <c>GetWindowRect</c>/<c>GetClientRect</c>로 <b>다시 읽는다</b> —
    ///         한 번 잃은 1px이 다음 회차의 새 기준이 된다. 이것이 래칫의 기억장치다.</item>
    /// </list>
    /// 그리고 흔들기 자체가 <b>클라이언트 영역 변경 4회 = 스왑체인/리디렉션 표면 재생성 4회</b>다.
    /// 재적용 루프가 <see cref="ReapplyAttempts"/>회 도니 UI 표면을 한 번 열고 닫을 때마다
    /// 최대 20회. "쓸수록 느려지고 재시작하면 회복"의 형태 그대로다.
    ///
    /// <para><b>macOS는 이 결함이 없다</b>(원인까지 확인 — 관측만이 아니다). Swift판
    /// <c>_setWindowBorderless</c>는 <c>window.styleMask = [.borderless]</c> 한 줄이고 프레임을
    /// 건드리지 않으며, 심지어 <c>window.styleMask != [.borderless]</c> 동등성 가드까지 걸려 있다.
    /// Windows판에만 그 가드가 없다.</para>
    ///
    /// ============================================================================
    /// 왜 "깎인 만큼 되돌리기"가 아니라 "깎지 않기"인가
    /// ============================================================================
    /// 사후 복원(재적용 전후로 창 사각형을 재서 차이만큼 <c>SetWindowPos</c>)은 <b>그 복원 자체가
    /// 또 하나의 클라이언트 영역 변경</b>이다. 리사이즈 4회를 5회로 늘리면서 폭만 맞추는 거래이며,
    /// 정지 시간(이번 신고의 본체)은 오히려 나빠진다. 그래서 규칙은 "필요 없으면 부르지 않는다"다.
    ///
    /// ============================================================================
    /// 그러면 무조건 재적용을 없애도 되는가 — <b>아니다. 반만 없앤다.</b>
    /// ============================================================================
    /// 이 저장소에는 "게터가 목표값이니 생략"이 실제 사고를 낸 전례가 있다(<c>isTopmost</c>:
    /// 게터가 순수 C# 캐시라 OS가 스타일을 떼어가도 계속 true를 돌려줬다). 그래서 그때의 처방과
    /// <b>같은 처방</b>을 쓴다 — 캐시가 아니라 <b>OS 실측</b>으로 판정한다.
    ///
    /// 결정적으로, 라이브러리가 한 덩어리로 묶어 둔 두 일은 성질이 전혀 다르다:
    /// <list type="table">
    ///   <item><term>유리(DWM 확장 프레임)</term><description><c>DwmExtendFrameIntoClientArea(MARGINS{-1})</c>.
    ///     창 사각형을 <b>건드리지 않는다</b>. 되읽을 공개 API가 없어 <b>실측 불가</b>.
    ///     -> 비용이 없으므로 <b>매 회차 무조건</b> 다시 건다(기존 방어 그대로 유지).</description></item>
    ///   <item><term>보더리스(스타일)</term><description><c>SetWindowLong(GWL_STYLE)</c> + 위의 리사이즈 4회.
    ///     <c>GetWindowLong(GWL_STYLE)</c>으로 <b>실측 가능</b>.
    ///     -> 실측이 이미 목표면 <b>부르지 않는다</b>.</description></item>
    /// </list>
    /// 결과적으로 "투명이 조용히 풀렸는데 캐시 때문에 못 고치는" 최악의 경우는 그대로 막힌다 —
    /// 화면을 실제로 비치게 만드는 쪽(유리)은 여전히 무조건 재적용되기 때문이다.
    /// </summary>
    public static class OverlayStateReapplyPolicy
    {
        /// <summary>
        /// 창 부착 확인 후 목표 상태를 재적용할 최대 횟수. 무한 반복은 하지 않는다 —
        /// 사용자가 창을 직접 조작했을 때 우리가 계속 되돌리는 것이 더 나쁘다.
        ///
        /// <para>★ 여기에 있는 이유: 양 플랫폼 Enforcer가 각자 <c>private const int = 5</c>를 들고
        /// 있었고, 둘 다 <c>#if UNITY_STANDALONE_*</c> 안이라 <b>테스트가 참조할 수 없어</b> 회귀
        /// 테스트가 숫자를 베낄 수밖에 없었다. CLAUDE.md "테스트에 프로덕션 상수를 숫자로 베끼지
        /// 않는다"를 지키려면 상수가 플랫폼 중립 위치에 있어야 한다.</para>
        /// </summary>
        public const int ReapplyAttempts = 5;

        /// <summary>재적용 간격(초). 위 상수와 같은 이유로 여기에 둔다.</summary>
        public const float ReapplyIntervalSeconds = 0.5f;

        /// <summary>
        /// <c>LibUniWinC.SetBorderless(TRUE)</c> 한 번이 창 폭에서 잃는 픽셀 수(실기 관측값).
        /// 회귀 테스트가 래칫을 모형화할 때 쓰는 유일한 숫자이며, 테스트가 이 상수를 참조한다.
        /// </summary>
        public const int BorderlessJiggleWidthLossPixels = 1;

        /// <summary>
        /// 창에 <b>프레임(테두리)</b>이 있는지를 가르는 Win32 스타일 비트 묶음.
        /// <c>WS_BORDER(0x00800000) | WS_DLGFRAME(0x00400000) | WS_THICKFRAME(0x00040000)</c>.
        /// (<c>WS_CAPTION</c>은 <c>WS_BORDER | WS_DLGFRAME</c>이므로 이 묶음에 이미 포함된다.)
        ///
        /// <para><b>왜 <c>WS_POPUP</c>을 요구하지 않는가</b>: 네이티브가 세우는 값은 정확히
        /// <c>WS_VISIBLE | WS_POPUP</c>이지만, 판정의 목적은 "SetBorderless가 실행됐는가"가 아니라
        /// <b>"사용자 눈에 테두리가 보이는가"</b>다. <c>WS_POPUP</c>까지 요구하면 누군가 그 비트만
        /// 건드렸을 때 <b>거짓 불일치</b>가 되어 매 회차 리사이즈 4회가 되살아난다 — 지금 고치는
        /// 버그 그 자체다. 반대 방향의 오판(테두리가 생겼는데 못 알아챔)은 이 묶음이 정확히 잡는다.</para>
        /// </summary>
        public const long WindowFrameStyleBits = 0x00800000L | 0x00400000L | 0x00040000L;

        /// <summary>OS에서 실측한 <c>GWL_STYLE</c> 값이 "테두리 없음"인가.</summary>
        public static bool IsBorderless(long windowStyle) => (windowStyle & WindowFrameStyleBits) == 0L;

        /// <summary>
        /// 이번 재적용 회차에서 투명화를 <b>어떻게</b> 다시 걸 것인가.
        /// </summary>
        /// <param name="desiredTransparent">목표 투명 상태(오버레이는 항상 true).</param>
        /// <param name="osStyleReadOk"><c>GetWindowLong(GWL_STYLE)</c> 실측에 성공했는가.</param>
        /// <param name="osBorderless">실측 결과 테두리가 없는가(<see cref="IsBorderless"/>).</param>
        /// <param name="glassOnlyPathAvailable">
        /// 유리만 다시 거는 경로(<c>LibUniWinC.SetTransparent</c> 직접 호출)를 쓸 수 있는가.
        /// 없으면 투명화를 포기하지 않고 라이브러리 전체 경로로 간다 — <b>회색 불투명 전체화면
        /// 창보다는 1px 래칫이 낫다</b>는 우선순위다.
        /// </param>
        public static TransparencyReapply DecideTransparencyReapply(
            bool desiredTransparent, bool osStyleReadOk, bool osBorderless, bool glassOnlyPathAvailable)
        {
            // 모를 때는 거는 쪽이 안전하다 — isTopmost 실측 가드와 같은 태도.
            if (!osStyleReadOk) return TransparencyReapply.ReassignStyleUnreadable;

            // 실측이 목표와 다르다 = 진짜로 SetBorderless가 필요한 순간. 리사이즈를 감수한다.
            if (osBorderless != desiredTransparent) return TransparencyReapply.ReassignStyleMismatch;

            if (!glassOnlyPathAvailable) return TransparencyReapply.ReassignGlassPathUnavailable;

            return TransparencyReapply.GlassOnly;
        }

        /// <summary>이 결정이 창 리사이즈(= 스왑체인 재생성)를 동반하는가.
        /// <see cref="TransparencyReapply.GlassOnly"/>만 0회다.</summary>
        public static bool CausesWindowResize(TransparencyReapply decision)
            => decision != TransparencyReapply.GlassOnly;

        /// <summary>Player.log에 그대로 찍히는 한국어 사유(사용자가 한 줄로 이해할 수 있어야 한다).</summary>
        public static string Describe(TransparencyReapply decision) => decision switch
        {
            TransparencyReapply.GlassOnly =>
                "유리만 재적용(OS 실측이 이미 보더리스) — 창 리사이즈 0회",
            TransparencyReapply.ReassignStyleUnreadable =>
                "창 스타일 실측 실패 — 모를 때는 거는 쪽이 안전하므로 라이브러리 전체 경로(리사이즈 4회)",
            TransparencyReapply.ReassignStyleMismatch =>
                "OS 실측 스타일이 목표와 다름 — SetBorderless가 실제로 필요(리사이즈 4회)",
            TransparencyReapply.ReassignGlassPathUnavailable =>
                "유리 전용 경로를 쓸 수 없음 — 투명화가 우선이므로 라이브러리 전체 경로(리사이즈 4회)",
            _ => decision.ToString(),
        };
    }

    /// <summary>
    /// 재적용 1회의 투명화 처리 방식. <see cref="TransparencyReapply.GlassOnly"/>만 창 사각형을
    /// 건드리지 않으며, 나머지는 전부 <c>SetBorderless</c>의 폭 흔들기(리사이즈 4회)를 동반한다.
    /// </summary>
    public enum TransparencyReapply
    {
        GlassOnly = 0,
        ReassignStyleUnreadable = 1,
        ReassignStyleMismatch = 2,
        ReassignGlassPathUnavailable = 3,
    }
}
