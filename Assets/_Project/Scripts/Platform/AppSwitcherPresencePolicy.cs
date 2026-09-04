namespace StickMate.Platform
{
    /// <summary>
    /// 이 오버레이가 OS의 <b>앱 전환 표면</b>(Windows 작업표시줄 버튼 · Alt+Tab / macOS Dock 아이콘 ·
    /// ⌘Tab)에 나타나는가를 정하는 <b>순수 규칙</b>. UnityEngine 의존도 P/Invoke도 한 줄 없다 —
    /// Windows 실기가 없는 이 개발 머신에서 규칙 자체를 실행해 검증할 수 있어야 하기 때문이다
    /// (<see cref="OverlayStateReapplyPolicy"/>, <see cref="LayeredHybridPolicy"/>와 같은 설계).
    ///
    /// ============================================================================
    /// 왜 생겼는가 — 2026-09-03 사용자 위임 판정 ③ "(가) 완전히 뺀다"
    /// ============================================================================
    /// 이 앱은 원래 <b>앱 전환기에 나타나지 않는 오버레이</b>다. macOS는 시작 시
    /// <c>NSApplicationActivationPolicyAccessory</c>로 내려가 Dock·메뉴바·⌘Tab에서 전부 빠지는데
    /// (<c>Platform/MacOS/MacSpaceBehaviorNative.ApplyAccessoryActivationPolicyOnce</c>),
    /// <b>Windows만 예외였다</b> — 작업표시줄 버튼과 Alt+Tab 항목이 그대로 남았다.
    ///
    /// <para>그 비대칭이 실제로 깨뜨린 것은 미관이 아니라 <b>불변식 R1-II(정직성)</b>이다.
    /// <c>⌃⌥⌘K</c>(사용자 직접 숨김)의 존재 이유가 "화면공유·발표 중에 나오지 마"인데
    /// <b>버튼은 공유 화면에 그대로 찍혔고</b>, 게다가 그 버튼을 <b>눌러도 아무 일도 일어나지
    /// 않았다</b> — 포커스를 듣고 <c>_userHidden</c>을 푸는 코드가 저장소에 0건이기 때문이다.
    /// "보이지만 아무 것도 하지 않는 표면"은 이 저장소가 금지하는 형태다.</para>
    ///
    /// ============================================================================
    /// ★ 이 정책이 <b>할 수 있는 것과 없는 것</b> — 여기가 이 파일에서 가장 중요한 문단이다
    /// ============================================================================
    /// <c>WS_EX_TOOLWINDOW</c>를 <b>이미 보이는 창</b>에 <c>SetWindowLong</c>으로 얹는 것은
    /// 두 표면에 <b>서로 다르게</b> 작용한다. 그 차이가
    /// <see cref="TakesEffectOnAlreadyShownWindow"/>에 <b>실행 가능한 형태로</b> 박혀 있다
    /// (주석으로만 적으면 늙는다 — 이 저장소가 여러 번 당한 형태다):
    /// <list type="table">
    ///   <item><term><see cref="AppSwitcherSurface.AltTabList"/></term>
    ///     <description><b>즉시 듣는다.</b> Alt+Tab 목록은 전환기가 <b>호출될 때</b> 최상위 창을
    ///     훑으며 스타일로 거른다. 스타일이 이미 바뀌어 있으면 그 호출부터 빠진다.</description></item>
    ///   <item><term><see cref="AppSwitcherSurface.TaskbarButton"/></term>
    ///     <description><b>듣지 않는다.</b> 셸은 창이 <b>보여질 때</b> 버튼을 만들고, 그 뒤의
    ///     <c>SetWindowLong</c>은 셸에 통보되지 않는다. Microsoft 문서가 직접 처방을 적어 두었다:
    ///     <i>"If you want to dynamically change a window's style to one that doesn't support
    ///     visible taskbar buttons, you must hide the window first (by calling ShowWindow with
    ///     SW_HIDE), change the window style, and then show the window."</i></description></item>
    /// </list>
    ///
    /// <para><b>그래서 스타일 비트가 닫는 것은 Alt+Tab(= macOS ⌘Tab의 대응물)뿐이고, 작업표시줄
    /// 버튼은 <i>다른 기전</i>이 닫는다.</b> 2026-09-03 리더 판정: <b>(b) <c>ITaskbarList::DeleteTab</c></b>
    /// (구현 <c>Platform/Windows/WindowsTaskbarButtonRemover.cs</c>).
    /// (a) 시작 시 <c>ShowWindow</c> 1왕복은 <b>기각</b>됐다 — 우리 창의 <b>표시 상태</b>를 바꾸는
    /// 순간 재적용 감시자들과 다투게 되고, 이 저장소에는 같은 종류의 충돌로 영구 비활성된 해소기가
    /// 이미 있다. (b)는 창 상태를 한 비트도 건드리지 않고 셸에게 목록에서 빼라고만 말한다.</para>
    ///
    /// <para>★ <b>그래도 <see cref="TakesEffectOnAlreadyShownWindow"/>의 답은 바뀌지 않는다.</b>
    /// 그 함수가 말하는 것은 <b>"스타일 변경이" 그 표면에 즉시 듣는가</b>이고, 작업표시줄에 대해
    /// 그 답은 여전히 <c>false</c>다 — 버튼을 지우는 것은 <b>스타일이 아니라 COM 호출</b>이다.
    /// 이 둘을 뭉치면 "스타일만 얹으면 버튼이 사라진다"는 틀린 지식이 코드에 굳는다.</para>
    ///
    /// ============================================================================
    /// 갚아야 할 대가 (매뉴얼에 반드시 들어간다)
    /// ============================================================================
    /// Windows에서 <b>앱을 찾는 시각 단서가 사라진다.</b> 탈출구는 확실한 것이 하나 있다 —
    /// <c>StickmanAgent._userHidden</c>은 <b>세이브에 없다</b>(<c>CharacterSaveStore</c>가 이 필드를
    /// 한 번도 읽거나 쓰지 않는다). 즉 <b>앱을 다시 실행하면 숨김이 반드시 풀린다.</b>
    /// 그 문장이 매뉴얼에 없으면 이 변경은 사용자를 가둔다.
    /// </summary>
    public static class AppSwitcherPresencePolicy
    {
        /// <summary>
        /// Windows에서 앱 전환 표면에서 빠지기 위해 우리 <b>자기 창</b>에 켜는 확장 스타일 비트
        /// (<c>WS_EX_TOOLWINDOW</c>). Win32 SDK 값이며 <b>이 저장소의 단일 출처</b>다 —
        /// <c>Platform/Windows/</c>의 실행부는 이 상수를 참조하고 자기 사본을 두지 않는다.
        ///
        /// <para>중립 위치에 두는 이유는 두 가지다. (1) CLAUDE.md 규칙 — 판정은 중립에, 플랫폼
        /// 코드는 사실 조회만. (2) <b>검증 가능성</b> — <c>Platform/Windows/</c>는 이 개발 머신의
        /// 활성 타깃(macOS)에서 <b>컴파일조차 되지 않으므로</b>, 상수를 그 안에 두면 EditMode가
        /// 값을 확인할 방법이 원리적으로 없다.</para>
        /// </summary>
        public const long WindowsToolWindowExStyleBit = 0x00000080L;

        /// <summary>macOS 쪽 대응 기전의 이름. 두 플랫폼이 <b>같은 목적</b>을 다른 API로 이룬다는
        /// 사실을 감사가 문자열을 베끼지 않고 확인할 수 있도록 여기 한 곳에 둔다.</summary>
        public const string MacOsMechanismName = "NSApplicationActivationPolicyAccessory";

        /// <summary>Windows 쪽 대응 기전의 이름(위와 같은 목적).</summary>
        public const string WindowsMechanismName = "WS_EX_TOOLWINDOW";

        /// <summary>
        /// 스타일 비트가 이미 서 있는가. <b>순수 비트 검사</b>이며 "모른다"는 표현하지 못한다 —
        /// 읽기 실패까지 함께 다뤄야 하면 <see cref="DecideToolWindowApply"/>를 써라.
        /// </summary>
        public static bool IsOutOfAppSwitcher(long exStyle)
            => (exStyle & WindowsToolWindowExStyleBit) != 0L;

        /// <summary>
        /// 지금 무엇을 해야 하는가. <b>모를 때는 아무것도 하지 않는다</b>가 이 정책의 기본값이다 —
        /// 우리 창의 스타일을 못 읽는 상황에서 추측으로 되쓰면 <b>다른 비트를 통째로 날린다</b>.
        /// </summary>
        /// <param name="styleReadOk">OS 실측이 성공했는가(호출자의 사실 조회 결과).</param>
        /// <param name="exStyle">그 실측값. <c>GetWindowLong</c> 계열은 실패를 0으로 알리므로
        /// 0은 성공 플래그와 무관하게 "모른다"로 다룬다 — <c>WindowsLayeredHybridResolver</c>와
        /// <c>WindowsWindowStyleProbe</c>가 이미 쓰는 같은 판정이다.</param>
        public static AppSwitcherStyleVerdict DecideToolWindowApply(bool styleReadOk, long exStyle)
        {
            if (!styleReadOk) return AppSwitcherStyleVerdict.Unknown;
            if (exStyle == 0L) return AppSwitcherStyleVerdict.Unknown;
            return IsOutOfAppSwitcher(exStyle)
                ? AppSwitcherStyleVerdict.AlreadyOut
                : AppSwitcherStyleVerdict.Apply;
        }

        /// <summary>
        /// 되쓸 확장 스타일 값. <b>비트 하나만 켜고 나머지는 읽은 그대로 보존한다</b> —
        /// 이 창에는 <c>WS_EX_LAYERED</c>·<c>WS_EX_TRANSPARENT</c>·<c>WS_EX_TOPMOST</c>가 동시에
        /// 서 있고, 그중 하나라도 떨어지면 클릭 관통(절대 불변 원칙 2)이 즉시 깨진다.
        /// </summary>
        public static long ComposeToolWindowExStyle(long currentExStyle)
            => currentExStyle | WindowsToolWindowExStyleBit;

        /// <summary>
        /// 쓴 뒤 <b>되읽어서</b> 실제로 섰는지 확인한다. 쓰기 반환값을 믿지 않는 이유는
        /// <c>SetWindowLong</c>이 "이전 값"을 돌려줄 뿐 성공/실패를 말하지 않기 때문이다.
        /// </summary>
        public static bool VerifyApplied(bool readBackOk, long exStyleAfter)
            => readBackOk && exStyleAfter != 0L && IsOutOfAppSwitcher(exStyleAfter);

        /// <summary>
        /// ★ <b>이 라운드의 핵심 사실</b>을 실행 가능한 형태로 박아 둔다(클래스 문서 참고).
        /// 이미 보여진 창에 스타일을 얹었을 때 그 표면이 <b>즉시</b> 반응하는가.
        /// </summary>
        public static bool TakesEffectOnAlreadyShownWindow(AppSwitcherSurface surface)
            => surface == AppSwitcherSurface.AltTabList;

        /// <summary>
        /// ★ 작업표시줄 버튼 제거(COM)를 <b>이번 틱에 시도할 것인가.</b>
        ///
        /// <para>이 규칙이 중립 위치에 있는 이유는 하나다 — <b>상한이 없으면 24시간 상주 앱에서
        /// COM 호출이 영원히 반복된다.</b> 그 상한은 <c>Platform/Windows/</c> 안에서는 이 머신이
        /// 실행해 확인할 방법이 없다(그 폴더는 활성 타깃에서 컴파일조차 되지 않는다).</para>
        ///
        /// <para>재시도가 필요한 이유는 <b>경합</b>이다: 셸이 버튼을 만드는 시점과 우리 첫 프레임의
        /// 선후를 보장할 수 없다. 너무 이르면 <c>DeleteTab</c>이 아무 일도 하지 않고, 버튼은 그
        /// 뒤에 생긴다. 그래서 몇 번만 더 묻되 <b>반드시 멈춘다</b>.</para>
        /// </summary>
        /// <param name="optedOut">사용자가 환경변수로 이 경로를 껐는가.</param>
        /// <param name="knownUnavailable">COM 경로가 이미 못 쓴다고 확인됐는가(실패는 조용히 넘어간다).</param>
        /// <param name="attemptsSoFar">지금까지 성공적으로 부른 횟수.</param>
        /// <param name="maxAttempts">상한. 0 이하이면 이 기능을 통째로 끈 것과 같다.</param>
        public static bool ShouldAttemptTaskbarButtonRemoval(
            bool optedOut, bool knownUnavailable, int attemptsSoFar, int maxAttempts)
        {
            if (optedOut) return false;
            if (knownUnavailable) return false;
            if (maxAttempts <= 0) return false;
            return attemptsSoFar < maxAttempts;
        }

        /// <summary>로그용 한 줄. 문자열을 실행부에 흩어 두지 않는다.</summary>
        public static string Describe(AppSwitcherStyleVerdict verdict)
        {
            switch (verdict)
            {
                case AppSwitcherStyleVerdict.AlreadyOut: return "이미 전환기 밖(쓰기 안 함)";
                case AppSwitcherStyleVerdict.Apply: return "전환기에 노출 중 — 비트를 켠다";
                default: return "스타일 실측 실패(모름 — 아무것도 쓰지 않는다)";
            }
        }
    }

    /// <summary>
    /// OS의 앱 전환 표면. <b>둘을 한 낱말로 뭉치지 않는 것</b>이 이 열거형의 존재 이유다 —
    /// 스타일 한 번 얹기에 대해 두 표면의 반응이 정반대이고, 그 차이를 뭉개면
    /// "작업표시줄 버튼이 사라졌다"는 <b>확인되지 않은 주장</b>이 보고서에 들어간다.
    /// </summary>
    public enum AppSwitcherSurface
    {
        /// <summary>Windows Alt+Tab 목록(= macOS ⌘Tab의 대응물). 전환기 호출 시점에 스타일로 거른다.</summary>
        AltTabList = 0,

        /// <summary>Windows 작업표시줄 버튼. 셸이 <b>창을 보일 때</b> 만들고 그 뒤 스타일 변경을 안 본다.</summary>
        TaskbarButton = 1,
    }

    /// <summary>스타일 비트에 대해 지금 할 일.</summary>
    public enum AppSwitcherStyleVerdict
    {
        /// <summary>못 읽었다. <b>아무것도 쓰지 않는다</b>(모를 때 안전한 쪽).</summary>
        Unknown = 0,

        /// <summary>이미 서 있다. 멱등 — 쓰기를 하지 않는다.</summary>
        AlreadyOut = 1,

        /// <summary>비트를 켜야 한다.</summary>
        Apply = 2,
    }
}
