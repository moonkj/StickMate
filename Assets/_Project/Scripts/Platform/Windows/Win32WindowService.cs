#if UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using UnityEngine;
using Kirurobo;
using StickMate.Platform;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// Windows 전용 IPlatformWindowService 구현체. Win32 P/Invoke는 <c>Platform/Windows/</c> 밖으로
    /// 절대 나가지 않는다(컨벤션 준수) — 이 폴더 밖의 어떤 코드도 user32.dll을 직접 호출하지 않고,
    /// 인터페이스만 안다. (같은 폴더 안의 예외적 분가는 두 건뿐이다:
    /// <c>WindowsViewerPresenceService</c>의 유휴/전원 조회와, 2026-09-01에 생긴
    /// <c>WindowsGameProcessProbe</c>의 게임 여부 조회. 둘 다 관심사가 뚜렷이 달라 분리했다.)
    ///
    /// 이 클래스가 절대 포함하지 않는 것: 타 프로세스 창을 이동(좌표 변경)시키거나, 크기를 바꾸거나,
    /// 종료(WM_CLOSE 전송/TerminateProcess)시키는 메서드. 오직 열거(읽기)만 한다
    /// (아키텍처 3절 유저 자산 불변 원칙).
    ///
    /// ★ 2026-08-30 윈도우 지원 라운드 이후로 이 파일에는 **쓰기 계열 Win32 호출이 단 한 건도 없다**.
    ///   이전 라운드까지는 자기 오버레이의 Z-order 조정용 SetWindowPos와 확장 스타일 변경용
    ///   SetWindowLong이 있었고, 감사 테스트(Tests/EditMode/UserAssetImmutabilityAuditTests.cs)가
    ///   그 한 건을 라인 단위 화이트리스트로 예외 처리하고 있었다. 아래 "오버레이 확보 경로" 절대로
    ///   오버레이 제어가 통째로 UniWindowController로 옮겨가면서 그 예외 자체가 불필요해졌다 —
    ///   즉 이 라운드는 기능 추가이면서 동시에 원칙 3의 표면적을 줄인 라운드다.
    ///
    /// ============================================================================
    /// 오버레이 확보 경로 — BUG-B1(Blocker) 해소, macOS와 동일한 UniWindowController 사용
    /// ============================================================================
    /// 이전 상태(BUG-B1): CreateOverlayWindow()가 진짜 분리된 오버레이 창을 만들지 않고 Unity 게임
    /// 자신의 MainWindowHandle을 재사용하는 스텁이었고, 그 위에 클릭관통/항상위를 그대로 걸면
    /// "불투명한 게임 창이 최상단에 고정되고 모든 마우스 입력이 막히는" 더 나쁜 결과가 나오므로
    /// SetClickThrough/SetAlwaysOnTop이 NotSupportedException을 던지는 안전 가드로 막혀 있었다.
    ///
    /// 이번 라운드 판단(조사 근거는 Tasklist.md "과학적 토론 로그" 참고): 이 프로젝트가 이미 의존하고
    /// 있는 kirurobo/UniWindowController(com.kirurobo.uniwinc, MIT)는 **원래 Windows용으로 먼저
    /// 만들어진 라이브러리이고 Windows/macOS를 한 API로 지원한다**. 패키지 안에
    /// Runtime/Plugins/Windows/x64/LibUniWinC.dll이 동봉되어 있고, C# 래퍼(UniWinCore.cs)의
    /// DllImport는 플랫폼 분기 없이 같은 심볼("LibUniWinC")을 부른다 — 즉 macOS에서 이미 실동작
    /// 검증이 끝난 `isTransparent / isTopmost / isClickThrough / isHitTestEnabled` 네 프로퍼티가
    /// Windows에서도 그대로 유효하다. 그래서 CreateWindowEx로 새 오버레이 창을 손으로 만들고 메시지
    /// 루프를 통합하는 별도 구현을 **하지 않는다**(중복 구현 지양 컨벤션).
    ///
    /// 배선 방식은 MacWindowService와 1:1이다:
    ///   - CreateOverlayWindow() -> 씬의 UniWindowController 확보(비활성이면 활성화) + isTransparent=true
    ///   - SetClickThrough(bool) -> isClickThrough + isHitTestEnabled 조합
    ///   - SetAlwaysOnTop(bool)  -> isTopmost
    /// 씬 배치 자체는 Assets/Editor/SceneBootstrapper.ConfigureUniWindowController()가 이미 플랫폼
    /// 중립으로 수행하므로(에디터 코드에 OS 분기가 없다) Windows용으로 추가할 씬 작업이 없다.
    ///
    /// 히트테스트와 안전장치의 상호작용은 MacWindowService 클래스 문서의 "히트테스트" 절과 완전히
    /// 동일하다(라이브러리 공통 로직):
    ///   - SetClickThrough(false) -> isHitTestEnabled=false + isClickThrough=false
    ///     (자동 제어까지 정지 = Escape 긴급 해제가 다음 프레임에 덮이지 않고 유지된다)
    ///   - SetClickThrough(true)  -> 둘 다 true (캐릭터 콜라이더 위에서만 클릭을 받는 부분적 관통 해제)
    ///
    /// 창 부착 타이밍 보정은 Platform/Windows/WindowsOverlayStateEnforcer.cs가 담당한다
    /// (그 파일 클래스 문서에 "왜 macOS판을 공용화하지 않았는가"의 근거가 있다).
    ///
    /// ============================================================================
    /// 이 파일이 여전히 Win32를 직접 쓰는 곳 — 전부 조회 전용
    /// ============================================================================
    ///   · 창 열거(EnumWindows/GetWindowRect/...)          — 발판 계산의 원천
    ///   · 창 제목 유무(InternalGetWindowText, 폴백 GetWindowTextW) — "사용자가 보는 창인가" 필터.
    ///     ★ 2026-09-01: 여기는 GetWindowTextLength였고 그것이 대상 창의 메시지 루프를 깨워
    ///     응답을 기다리는 호출이라 실기에서 열거 1회 최대 199ms를 만들었다. 지금은 커널 구조체를
    ///     직접 읽어 다른 앱의 상태와 무관하다(선언부 문서에 실기 로그와 대안 비교가 있다).
    ///     읽은 제목은 보관하지 않는다 — "비었는가"만 보고 즉시 버린다.
    ///   · 창의 시각적 경계(DwmGetWindowAttribute + DWMWA_EXTENDED_FRAME_BOUNDS) — 보이지 않는
    ///     리사이즈 테두리(~7px)를 뺀 진짜 창 경계. 발판/가림 계산의 기준 사각형이다.
    ///   · 창 투명도(GetLayeredWindowAttributes)           — macOS kCGWindowAlpha 대응 필터
    ///   · 가상 화면 크기(GetSystemMetrics SM_*VIRTUALSCREEN) — 모든 모니터 밖 창 제외
    ///   · 커서 좌표(GetCursorPos)                          — ICursorPositionService
    ///   · 마우스/키보드 눌림 상태(GetAsyncKeyState)        — IGlobalPointerButtonService/IGlobalKeyStateService
    ///   · 전체화면 판정(MonitorFromWindow/GetMonitorInfo)  — 비침해 원칙 2 자동 숨김의 기하 조건.
    ///     "그 앱이 게임인가"라는 두 번째 조건은 WindowsGameProcessProbe.cs가 읽기 전용으로 조회한다.
    ///   · 작업표시줄 예약 영역(GetMonitorInfo의 rcMonitor/rcWork 차)  — IReservedBottomBarService(하단),
    ///     IReservedTopBarService(상단 도킹 작업표시줄). 같은 한 번의 조회에서 둘 다 나온다.
    ///   · 창 DPI(GetDpiForWindow)                          — UI 밀도(캔버스 배율) 보고
    /// 어느 것도 다른 창의 상태를 바꾸지 않으며, 입력을 주입하지도 않는다.
    /// </summary>
    public sealed class Win32WindowService :
        IPlatformWindowService,
        ICursorPositionService,
        IGlobalPointerButtonService,
        IGlobalKeyStateService,
        ILocalClickCaptureService,
        IDesktopIconLayoutService,
        IReservedBottomBarService,
        IReservedTopBarService,
        IRawWindowRectSource,
        IWindowEnumerationCostSource
    {
        #region Win32 선언 (이 리전 밖으로 유출 금지 — 전부 조회 전용)
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>최소화(아이콘화)된 창인지. 최소화 창은 화면에 없지만 IsWindowVisible이 여전히
        /// true라서, 이 필터가 없으면 (-32000,-32000) 같은 좌표의 유령 발판이 목록에 섞인다.</summary>
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>
        /// ★★ 2026-09-01 — <b>창 제목 유무</b> 조회. 실기 로그로 확정된 스톨 원인을 제거한 자리다.
        ///
        /// <para><b>여기 있던 것과 무엇이 문제였나.</b> 이 자리에는 <c>GetWindowTextLength</c>가 있었다.
        /// 그 함수는 대상 창에 <c>WM_GETTEXTLENGTH</c>를 보내고 <b>그 창의 메시지 루프가 응답할
        /// 때까지 블로킹한다</b>. 즉 우리 프레임 시간이 <b>남의 앱의 응답성</b>에 묶여 있었다.
        /// 사용자 실기 로그(릴리즈 20260901d, 계측 포함)가 그 서명을 그대로 찍었다:</para>
        /// <code>
        /// [발판열거] 1회 평균 14.09ms / 최대 199.27ms, 94회/30초
        /// [발판진단] 사유별 [IsWindowVisible=false=796, 최소화=20, 제목 없음=19, ...]  (합 846개)
        /// </code>
        /// <para>846개를 훑는데 1회 최대 199ms — 창 하나당 0.23ms다. 커널의 창 구조체를 읽는 단순
        /// 검사로는 나올 수 없는 값이고, 편차(1.36ms ~ 199ms)가 <b>"그 순간 다른 앱들이 뭘 하고
        /// 있었나"</b>에 달렸다는 점이 블로킹의 결정적 증거다. 796개가 <c>IsWindowVisible</c>에서
        /// 걸러지고 남은 ~50개가 이 검사까지 왔는데, 그중 <b>하나만</b> 바쁜 앱이어도 우리가 멈췄다.
        /// "켜둘수록 심해진다"는 신고도 같은 원인이다 — 작업할수록 창이 늘고 바쁜 앱이 는다.</para>
        ///
        /// <para><b>macOS에 같은 증상이 없는 이유</b>도 이것으로 설명된다:
        /// <c>CGWindowListCopyWindowInfo</c>는 창 목록을 <b>한 번에 스냅샷으로</b> 받아오므로
        /// 창별 왕복이 원리적으로 없다. 리더가 맥에서 20분 넘게 실측했는데 재현되지 않았다.</para>
        ///
        /// <para><b>왜 이 함수인가.</b> <c>InternalGetWindowText</c>는 커널이 보관한 캡션을
        /// <b>직접</b> 읽는다. 대상 프로세스에 아무 메시지도 보내지 않으므로 그 앱이 멎어 있어도
        /// 우리는 멎지 않고, 비용이 창 하나당 상수다. 문서화되지 않은 export지만 Windows 2000
        /// 이래 user32에 안정적으로 존재하며 창 열거 도구들이 쓰는 표준 경로다. 대안 비교와
        /// 없을 때의 폴백은 <see cref="ProbeHasTitle"/> 문서에 있다.</para>
        ///
        /// <para>조회 전용이다. 읽은 제목은 <b>보관하지 않는다</b> — "비었는가"만 보고 즉시 버린다
        /// (열거한 남의 창 정보를 최소화한다는 이 파일의 기존 원칙과 같다).</para>
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "InternalGetWindowText")]
        private static extern int InternalGetWindowText(IntPtr hWnd, [Out] char[] pString, int cchMaxCount);

        /// <summary>
        /// 위 함수가 없는 환경을 위한 폴백. <c>GetWindowTextW</c>는 <b>문서화된 동작</b>으로
        /// "대상 창이 다른 프로세스 소유이면 메시지를 보내지 않고 캡션을 직접 가져온다 —
        /// 그 프로세스가 멎어 있어도 호출자가 멎지 않게 하기 위한 의도된 설계"라고 명시돼 있다.
        /// 즉 <b>남의 창에 대해서는</b> 이쪽도 블로킹하지 않는다.
        ///
        /// <para>단, <b>우리 자신의 창</b>에는 실제로 WM_GETTEXT를 보낸다. 그것은 EnumWindows
        /// 콜백 한복판에서 우리 WndProc이 재진입한다는 뜻이라 그 자체로 위험하다. 그래서
        /// <see cref="ClassifyWindowStyle"/>은 <b>자기 프로세스 검사를 제목 조회보다 먼저</b> 둔다 —
        /// 그 순서가 성능 최적화이면서 동시에 이 폴백의 안전 조건이다.</para>
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
        private static extern int GetWindowTextW(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // 조회 전용. 쓰기 짝인 SetWindowLong은 이 라운드에 삭제했다(오버레이 스타일 제어가
        // UniWindowController로 옮겨감) — 이 파일에는 이제 창 스타일을 **바꾸는** 경로가 없다.
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>창의 소유 프로세스 ID 조회 — "우리 자신의 창을 발판으로 삼는" 사고 방지용
        /// (macOS의 IsSelfWindow와 같은 목적). 조회 전용이며 반환된 PID로 아무 것도 하지 않는다.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// 키/버튼의 "지금 눌려 있는가"를 창 포커스와 무관하게 조회한다
        /// (IGlobalPointerButtonService/IGlobalKeyStateService — macOS의 CGEventSourceKeyState /
        /// CGEventSourceButtonState와 같은 계열의 조회 전용 API).
        ///
        /// 왜 GetKeyState가 아니라 GetAsyncKeyState인가: GetKeyState는 **호출 스레드의 메시지 큐가
        /// 처리한 시점의** 키 상태를 돌려주므로 우리 창이 포커스를 갖고 있지 않으면 갱신되지 않는다.
        /// 이 앱의 창은 클릭관통 + 비활성이 기본이라 그 조건이 성립하지 않는다.
        /// 반환값의 최상위 비트(0x8000)가 "지금 눌려 있음"이다. 어떤 입력도 주입하지 않으며,
        /// 후킹(SetWindowsHookEx)이나 관리자 권한도 필요 없다.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>
        /// DWM "cloaked"(합성기 수준에서 숨겨진) 창 판정. Windows 10 이후 UWP/스토어 앱은 종료 후에도
        /// 제목이 있고 IsWindowVisible이 true인 껍데기 창을 남겨두는데, 이 필터가 없으면 보이지도 않는
        /// 창 위에 캐릭터가 서 있게 된다("허공을 걷는" macOS 신고와 같은 계열의 결함).
        /// </summary>
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        /// <summary>
        /// 같은 함수의 RECT 오버로드 — DWMWA_EXTENDED_FRAME_BOUNDS 전용.
        /// (P/Invoke는 반환 버퍼 타입이 다르면 시그니처를 따로 선언해야 한다. dwAttribute마다 out 타입이
        ///  달라지는 Win32의 관례라 이 중복은 피할 수 없다 — cbAttribute를 잘못 넘기면 스택이 깨지므로
        ///  아래 호출부는 반드시 Marshal.SizeOf&lt;RECT&gt;()를 넘긴다.)
        /// </summary>
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        /// <summary>
        /// ★ 2026-08-31 (이월 결함 해소) — macOS <c>kCGWindowAlpha</c>의 Windows 대응물.
        /// WS_EX_LAYERED 창의 전체 알파(0~255)와 플래그를 <b>읽기만</b> 한다. 대응하는 Set 계열
        /// (남의 창 투명도를 바꾸는 쓰기 API)은 이 파일에 없으며 앞으로도 추가하지 않는다(원칙 3).
        ///
        /// 반환 false의 의미가 중요하다: 창이 <c>UpdateLayeredWindow</c>로 <b>픽셀별 알파</b>를 쓰면
        /// 이 함수는 문서상 실패한다(에러가 아니라 "전체 알파라는 값이 없다"는 뜻). 그 해석은
        /// <see cref="WindowsFootholdFilter.ResolveWindowAlpha"/>가 담당한다.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint pcrKey, out byte pbAlpha, out uint pdwFlags);

        /// <summary>가상 화면(모든 모니터 외접 사각형) 조회용. 순수 시스템 지표 읽기다.</summary>
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        // 창이 놓인 모니터의 유효 DPI. 반환값 / 96 = "논리 포인트 1개가 몇 물리 픽셀인가"이며 그것이
        // 곧 이 앱의 캔버스 배율이다(아래 CaptureUiDensity 문서 참고). Windows 10 1607+ 전용 API라
        // 없는 환경에서는 EntryPointNotFoundException이 나는데, 그때는 96(=배율 1)로 폴백한다.
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private const int DWMWA_CLOAKED = 14;

        /// <summary>
        /// 창의 <b>실제 시각적 경계</b>. GetWindowRect는 Windows 10/11에서 DWM이 리사이즈용으로
        /// 잡아두는 <b>보이지 않는 테두리</b>(좌/우/하 약 7px)를 포함해 돌려주므로, 그대로 쓰면
        /// 발판이 눈에 보이는 창보다 좌우로 넓고 가리는 폭도 그만큼 과하게 계산된다.
        /// (이월 Minor: 인질극 닫기버튼 조준/로프 앵커가 Windows에서 일제히 ~7px 어긋나던 원인.)
        /// </summary>
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        // 가상 화면(모든 모니터를 감싸는 외접 사각형) — winuser.h 고정 리터럴.
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        private const int GWL_EXSTYLE = -20;
        /// <summary>도구 창(팔레트/툴팁류)은 Alt-Tab 목록에도 안 나오는 보조 창이라 발판에서 제외한다.</summary>
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // 가상 키코드(winuser.h 고정 리터럴 — 심볼이 아니라 헤더 상수라 하드코딩이 안전하다).
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;   // Alt
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const short KeyDownMask = unchecked((short)0x8000);
        #endregion

        // 우리 오버레이(Unity 플레이어) 창 핸들. 절대로 "타 윈도우" 핸들을 여기 담지 않으며,
        // 이 값은 이제 조회(자기 자신 제외 / 전체화면 판정 / 오버레이 원점 보고)에만 쓰인다.
        private IntPtr _overlayHwnd;

        /// <summary>우리 프로세스 ID — 열거에서 우리 자신의 창을 걸러내는 기준(macOS의 IsSelfWindow 대응).</summary>
        private readonly uint _currentProcessId;

        // EnumWindows 콜백 델리게이트는 인스턴스당 1회만 생성해 매 호출마다 델리게이트를
        // 재할당하지 않는다 (24시간 상주 앱, GC 압박 방지 컨벤션).
        private readonly EnumWindowsProc _enumWindowsCallback;

        // 최종 발판 버퍼 = "다른 창에 가려지지 않고 실제로 눈에 보이는 상단 테두리 조각"들.
        // 매 호출 시 새 List를 만들지 않고 Clear 후 재사용한다.
        private readonly List<PlatformFoothold> _footholdBuffer = new List<PlatformFoothold>(64);

        // ★ 2026-08-31 — 가려짐 필터 이전의 원본 창 목록(창 전체 사각형, z-order 앞->뒤).
        // 이전에는 _footholdBuffer가 곧 원본 목록이어서 이 버퍼가 없었다. 이제 발판은 조각으로
        // 잘리므로 둘이 다른 목록이 된다: 창 도둑(IRawWindowRectSource)은 "딛는" 것이 아니라
        // "미는" 연출이라 가려진 창도 대상이 될 수 있어야 하므로 반드시 이 원본 쪽을 봐야 한다.
        private readonly List<PlatformFoothold> _rawBuffer = new List<PlatformFoothold>(64);

        // IRawWindowRectSource용 살아있는 읽기 전용 뷰 — 매 폴링마다 재생성하지 않는다(할당 0).
        private readonly ReadOnlyCollection<PlatformFoothold> _readOnlyRawWindows;

        // 가려짐(오클루전) 계산 본체. macOS 구현체와 <b>같은 클래스를 공유</b>한다
        // (Platform/VisibleTopEdgeSolver.cs — 왜 공유하는지는 그 파일 문서 참고).
        private readonly VisibleTopEdgeSolver _topEdgeSolver = new VisibleTopEdgeSolver();

        /// <summary>
        /// 가려짐 계산 후 남은 상단 테두리 조각이 이보다 좁으면 버린다(픽셀). macOS판과 같은 값이며
        /// 이유도 같다: 캐릭터 몸통 폭보다 훨씬 좁은 조각 위에 서 있게 하면 "허공에 떠 있다"는
        /// 사용자 인식이 그대로 재발한다.
        /// </summary>
        private const float MinVisibleFootholdWidth = 24f;

        /// <summary>DwmGetWindowAttribute(EXTENDED_FRAME_BOUNDS)에 넘길 버퍼 크기. 값을 잘못 넘기면
        /// 네이티브가 스택을 넘어 쓰므로 리터럴이 아니라 실제 구조체 크기에서만 얻는다.</summary>
        private static readonly int DwmRectSize = Marshal.SizeOf<RECT>();

        // 이번 열거 패스의 포그라운드 창. 예전에는 창마다 GetForegroundWindow()를 불렀는데
        // 한 패스 안에서는 값이 같으므로 패스당 1회만 조회한다(의미 동일, OS 호출 n회 -> 1회).
        private IntPtr _foregroundHwndThisPass;

        // 이번 패스의 가상 화면(모든 모니터 외접 사각형). 창마다 조회하지 않고 패스당 1회만 조회한다.
        private bool _hasVirtualScreenThisPass;
        private Rect _virtualScreenThisPass;

        // ★ 2026-08-31 (이월 결함 해소) — 진단용 부기. 전부 사전 할당 재사용이라 폴링 경로 할당 0이다.
        // _rawBuffer와 인덱스 1:1로 유지되는 것이 계약이다(macOS의 _rawAlphas/_rawVisibleWidth와 동일).
        private readonly List<float> _rawAlphas = new List<float>(64);
        private readonly List<float> _rawVisibleWidth = new List<float>(64);

        // 알파 필터에 걸린 창의 사각형(그것만 좌표까지 남긴다 — 아래 OnEnumWindow 주석 참고).
        private readonly List<Rect> _alphaRejectRects = new List<Rect>(8);

        // 탈락 사유별 개수. 인덱스가 곧 WindowsFootholdRejection의 값이다.
        private readonly int[] _rejectCounts = new int[System.Enum.GetValues(typeof(WindowsFootholdRejection)).Length];

        // 이상 징후 로그 억제용(아래 ReportFootholdAnomaly 참고).
        private float _lastAnomalyLogTime = float.NegativeInfinity;
        private int _lastAnomalySignature;
        private readonly System.Text.StringBuilder _diagnosticsBuilder = new System.Text.StringBuilder(256);

        /// <summary>이번 패스에서 모든 필터를 통과해 가려짐 계산에 들어간 창 수(macOS와 같은 의미).</summary>
        public int LastRawWindowCount { get; private set; }

        /// <summary>그 중 다른 창에 완전히 가려져 발판을 하나도 내지 못한 창 수(macOS와 같은 의미).</summary>
        public int LastFullyOccludedWindowCount { get; private set; }

        // ★ 2026-09-01 스파이크 라운드 — IWindowEnumerationCostSource. 열거 **규모**의 실측치다.
        // LastRawWindowCount("필터를 다 통과한 창")와 혼동하지 말 것: 이 값은 그보다 훨씬 크며
        // (실기 관측 최대 818개) OS가 콜백한 최상위 창 전체다. 두 값의 비(818 -> 6~10)가
        // "비싼 호출이 실제로 몇 번 일어났는가"를 말해 준다.
        // 비용: 콜백당 int 증가 1회.
        private int _enumeratedWindowCount;
        private int _dwmProbeCount;

        // ============================================================================
        // ★★ 2026-09-01 — 제목 조회 경로의 상태와 계측(블로킹 호출 제거 라운드)
        // ============================================================================

        /// <summary>어느 제목 조회 API로 도는가. 최초 1회만 결정하고 그 뒤로는 분기 하나로 끝난다.</summary>
        private enum TitleProbeApi
        {
            Unresolved = 0,
            /// <summary>InternalGetWindowText — 커널 구조체 직접 읽기. 메시지를 보내지 않는다.</summary>
            Kernel,
            /// <summary>GetWindowTextW — 문서화된 폴백(타 프로세스 창에는 역시 메시지를 보내지 않는다).</summary>
            DocumentedFallback,
        }

        private TitleProbeApi _titleProbeApi = TitleProbeApi.Unresolved;
        private bool _titleProbeApiLogged;

        /// <summary>
        /// 제목 조회 버퍼. <b>"비었는가"만</b> 알면 되므로 1글자 + 널 종단이면 충분하다 —
        /// 제목이 아무리 길어도 복사 비용이 늘지 않는다. 인스턴스당 1개를 재사용하므로
        /// 열거 경로 할당은 그대로 0이다(24시간 상주 앱 컨벤션).
        /// </summary>
        private const int TitleProbeBufferChars = 2;
        private readonly char[] _titleProbeBuffer = new char[TitleProbeBufferChars];

        // 이번 패스의 제목 조회 누적 — Stopwatch 틱으로 모으고 보고할 때만 ms로 바꾼다.
        private long _titleProbeTicksThisPass;
        private int _titleProbeCountThisPass;

        // 30초 창(window)의 최악값. "이 수정이 먹었는가"를 한 숫자로 답하는 값이다.
        private float _titleProbeWorstMsInWindow;
        private float _titleProbeWindowStartTime = float.NegativeInfinity;

        private static readonly double TitleProbeTicksToMs =
            1000.0 / System.Diagnostics.Stopwatch.Frequency;

        /// <summary>마지막 패스에서 제목 조회를 실제로 몇 번 했는가(값싼 필터를 통과한 창 수).</summary>
        public int LastTitleProbeCount { get; private set; }

        /// <summary>
        /// 마지막 패스에서 <b>제목 조회에만</b> 쓴 시간(ms). 이 값이 이 라운드의 성패 지표다:
        /// 이전 구현에서는 이 한 단계가 열거 전체의 199ms를 만들 수 있었고, 지금은 원리적으로
        /// 창 수에 비례하는 상수 시간이라 두 자릿수 마이크로초여야 정상이다.
        /// </summary>
        public float LastTitleProbeMs { get; private set; }

        /// <summary>마지막 패스에서 <c>EnumWindows</c> 콜백이 불린 총 횟수(필터 이전 전체 창 수).</summary>
        public int LastEnumeratedWindowCount { get; private set; }

        /// <summary>
        /// 마지막 패스에서 <b>크로스 프로세스 DWM 조회</b>(<c>DwmGetWindowAttribute</c>)가 실제로
        /// 몇 번 일어났는가. 값싼 필터를 뚫고 온 창만 이 비용을 낸다 —
        /// <c>IsCloaked</c> 1회 + <c>TryGetVisualWindowRect</c> 1회다.
        ///
        /// <para>★ 2026-09-01 필터 순서 수정으로 <b>이 값이 줄어드는 것이 정상</b>이다. 이전에는
        /// <c>IsCloaked</c>가 도구 창(<c>WS_EX_TOOLWINDOW</c>) 검사와 자기 프로세스 검사보다 앞에 있어
        /// "어차피 버릴 창"에도 DWM 왕복을 지불했다. 지금은 그 둘을 통과한 창만 낸다 —
        /// 따라서 이 수치는 이제 대략 "제목 있고, 보이고, 도구 창이 아니고, 남의 프로세스인 창 x2"다.
        /// 전후 비교를 할 때 이 정의 변경을 감안해야 한다.</para>
        ///
        /// <b>이 값이 작으면 DWM 호출은 스파이크의 범인이 아니다</b>가 실측으로 확정된다.
        /// </summary>
        public int LastDwmProbeCount { get; private set; }


        // 오버레이 원점 보고용 부기(아래 CaptureOverlayOrigin 참고).
        private bool _overlayOriginLogged;
        private float _lastLoggedDpiScale = -1f;

        public Win32WindowService()
        {
            _enumWindowsCallback = OnEnumWindow;
            _readOnlyRawWindows = _rawBuffer.AsReadOnly();
            using (var self = System.Diagnostics.Process.GetCurrentProcess())
            {
                _currentProcessId = (uint)self.Id;
            }
        }

        #region 창 열거(조회 전용)

        /// <summary>
        /// "사용자가 실제로 보고 있는 최상위 창"만 남기는 표준 필터의 <b>스타일 단계</b>.
        /// 여기에 걸러지지 않은 창은 그대로 발판이 되므로, 조건 하나가 빠지면 캐릭터가 보이지 않는
        /// 창 위에 서 있게 된다.
        ///
        /// ★ 2026-08-31 — bool이 아니라 <b>사유</b>를 돌려주도록 바꿨다. 이월 Minor 3("Windows에는
        /// macOS 같은 발판 진단 로그가 없어 알파 문제를 원격으로 판별할 수단이 없다")를 해소하려면
        /// "몇 개가 왜 탈락했는가"를 남길 수 있어야 하기 때문이다. 사유 문자열은 전부 상수라
        /// 열거 경로에 할당은 늘지 않는다.
        /// </summary>
        /// <param name="alpha">
        /// 이 창의 전체 알파(0~1). macOS <c>kCGWindowAlpha</c>의 Windows 대응물이며 판정 근거는
        /// <see cref="WindowsFootholdFilter.ResolveWindowAlpha"/>에 있다. 스타일 단계에서 탈락한
        /// 창은 1로 둔다(쓰이지 않는다).
        /// </param>
        private WindowsFootholdRejection ClassifyWindowStyle(IntPtr hWnd, out float alpha)
        {
            alpha = 1f;

            // ★★ 2026-09-01 — 순서 자체가 성능이다(리더 지시, 저위험 고확실성).
            //
            // 이 필터들은 전부 "탈락시키는" 일만 하므로 **어떤 순서로 놓아도 결과 집합은 같다**.
            // 그런데 비용은 세 계단으로 갈린다:
            //
            //   [1] 순수 커널 구조체 읽기 — IsWindowVisible / IsIconic / GetWindowLong /
            //       GetWindowThreadProcessId. 창 하나당 수십~수백 나노초, 편차 없음.
            //   [2] 제목 조회 — 역시 커널 구조체를 읽지만 문자열을 버퍼로 복사한다.
            //       [1]보다 조금 비싸므로 [1] 뒤에 둔다.
            //   [3] IsCloaked(= DwmGetWindowAttribute) — **DWM 프로세스로 가는 크로스 프로세스
            //       호출**. 이 함수에서 압도적으로 비싼 단 하나다. 반드시 맨 뒤.
            //
            // ★ 2026-09-01 2차 라운드에서 [2]의 성질이 바뀌었다. 이전에는 여기가
            //   GetWindowTextLength였고 그것은 **대상 창의 메시지 루프를 깨워 응답을 기다리는**
            //   호출이었다(선언부 문서에 실기 로그와 함께 근거를 적어 두었다). 그때는 [2]가 사실상
            //   [3]보다도 비싸고 **상한이 없는** 구간이었다 — 실기 최대 199ms/열거.
            //   지금은 [2]도 커널 읽기이므로 상한이 생겼고, 위 계단이 실제 비용 순서와 일치한다.
            //
            // ★ 자기 프로세스 검사가 제목 조회보다 **앞에** 있는 것은 성능 이유만이 아니다.
            //   폴백 경로(GetWindowTextW)는 우리 자신의 창에 대해서만 WM_GETTEXT를 보내는데,
            //   그것은 EnumWindows 콜백 한복판에서 우리 WndProc이 재진입한다는 뜻이다.
            //   여기서 먼저 걸러 두면 그 상황 자체가 성립하지 않는다. 순서를 바꾸지 말 것.
            //
            // ★ 부작용 하나를 명시해 둔다: 여러 조건에 동시에 걸리는 창의 <b>탈락 '사유'</b>가 바뀔 수
            // 있다(예: 제목 없는 도구 창이 이제 NoTitle이 아니라 ToolWindow로 집계된다). 사유는
            // [발판진단] 로그의 분류일 뿐 발판 채택 여부와 무관하므로 기능적 영향은 없다.
            if (!IsWindowVisible(hWnd)) return WindowsFootholdRejection.NotVisible;
            if (IsIconic(hWnd)) return WindowsFootholdRejection.Minimized;   // 최소화 = 화면에 없음

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return WindowsFootholdRejection.ToolWindow;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == _currentProcessId) return WindowsFootholdRejection.SelfProcess;

            if (!ProbeHasTitle(hWnd)) return WindowsFootholdRejection.NoTitle;

            // 여기부터가 비싼 구간 — 값싼 필터를 전부 통과한 창만 도달한다.
            _dwmProbeCount++;                                                // 계측 전용(바로 아래가 크로스 프로세스 DWM 호출이다)
            if (IsCloaked(hWnd)) return WindowsFootholdRejection.Cloaked;    // DWM 수준에서 숨겨진 UWP 껍데기

            alpha = ReadWindowAlpha(hWnd, exStyle);
            return WindowsFootholdRejection.None;
        }

        /// <summary>
        /// ★★ 2026-09-01 — "이 창에 제목이 있는가"를 <b>블로킹 없이</b> 묻는다.
        ///
        /// ============================================================================
        /// 대안 비교 (둘 다 검토한 근거를 남긴다)
        /// ============================================================================
        /// <b>후보 A — SendMessageTimeout(WM_GETTEXTLENGTH, SMTO_ABORTIFHUNG, 프레임 예산에서 유도한
        /// 타임아웃).</b> 채택하지 않았다. 세 가지가 걸린다:
        /// <list type="number">
        /// <item><b>고치는 것이 원인이 아니라 상한이다.</b> 여전히 창마다 다른 프로세스로 왕복하고,
        ///   응답이 느린(멎지는 않은) 앱에는 타임아웃이 걸리지도 않아 그대로 기다린다. 이 검사에
        ///   도달하는 창이 실기에서 ~50개였으므로, 타임아웃을 프레임 예산의 1/8(약 2ms)로 잡아도
        ///   최악은 50 x 2 = 100ms다. 199ms를 100ms로 줄이는 것은 해결이 아니다.</item>
        /// <item><b>판정이 달라진다.</b> 타임아웃이 걸린 창을 '제목 있음'으로 보면 <b>사용자 눈에
        ///   보이지 않는 창 위에 캐릭터가 서는</b> 실패 모드가 열리고(이 파일이 명시적으로 경고하는
        ///   바로 그것), '제목 없음'으로 보면 멀쩡한 창이 발판에서 사라진다. 어느 쪽도 이전과 같은
        ///   집합이 아니다 — <b>이 라운드의 요구는 판정을 그대로 두는 것</b>이다.</item>
        /// <item><b>원칙 3의 표면적이 넓어진다.</b> 이 앱은 "남의 창에 보낼 메시지가 하나도 없다"를
        ///   감사(<c>UserAssetImmutabilityAuditTests</c>)로 잠가 두었다. 조회용 메시지를 한 번
        ///   열어 주면 그 다음 사람이 다른 메시지를 붙이는 것이 자연스러워진다.</item>
        /// </list>
        ///
        /// <b>후보 B — InternalGetWindowText(채택).</b> 커널이 보관한 캡션을 직접 읽으므로
        /// 대상 프로세스와 아무 상호작용이 없다. 비용이 창 하나당 상수이고 <b>다른 앱의 상태와
        /// 무관</b>하다 — 관측된 극심한 편차의 원인 자체가 사라진다. 판정도 그대로다:
        /// 캡션이 비었으면 0, 있으면 1 이상이고 이전 코드가 보던 것도 정확히 그 구분이었다
        /// (동치 근거는 <see cref="WindowsFootholdFilter.HasWindowTitle"/> 문서).
        ///
        /// <b>유일한 약점</b>은 문서화되지 않은 export라는 점이다. 그래서 최초 1회 존재 여부를
        /// 확인하고(<see cref="ResolveTitleProbeApi"/>), 없으면 문서화된 <c>GetWindowTextW</c>로
        /// 내려간다. 그쪽도 <b>타 프로세스 창에는 메시지를 보내지 않는다</b>고 문서에 명시돼 있어
        /// 폴백 경로에서도 블로킹 성질은 되살아나지 않는다.
        ///
        /// <para><b>계측.</b> 이 함수에 쓴 시간만 따로 누적한다(패스당 Stopwatch 2회 x 창 수 —
        /// 실기 기준 ~50회, QPC 1회가 약 25ns이므로 패스당 2.5us 수준). 이 값이
        /// <c>[발판진단]</c> 줄과 예산 초과 경보에 그대로 실린다 = 다음 실기 로그가 이 수정의
        /// 성패를 스스로 증명한다.</para>
        /// </summary>
        private bool ProbeHasTitle(IntPtr hWnd)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            int copied = _titleProbeApi == TitleProbeApi.Kernel
                ? InternalGetWindowText(hWnd, _titleProbeBuffer, TitleProbeBufferChars)
                : GetWindowTextW(hWnd, _titleProbeBuffer, TitleProbeBufferChars);
            _titleProbeTicksThisPass += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            _titleProbeCountThisPass++;
            return WindowsFootholdFilter.HasWindowTitle(copied);
        }

        /// <summary>
        /// 어느 제목 조회 API를 쓸지 <b>패스 시작 전에 한 번만</b> 정한다.
        ///
        /// <para>try/catch를 열거 루프 안에 두지 않는 것이 요점이다: 없는 export를 부르면
        /// <c>EntryPointNotFoundException</c>이 나는데, 그걸 창마다 잡으면 그 환경에서는 열거가
        /// 예외 수백 개를 던지는 경로가 된다. 여기서 한 번 불러 <b>존재만</b> 확인한다 —
        /// 우리가 알고 싶은 것은 반환값이 아니라 "이 심볼이 로드되는가"다.</para>
        ///
        /// <para>넘기는 핸들은 이번 패스의 포그라운드 창이다(없으면 <c>IntPtr.Zero</c>). 어느 쪽이든
        /// 결과를 쓰지 않으므로 유효성은 상관없지만, <b>실제 창 핸들</b> 쪽이 문서화되지 않은 함수의
        /// 인자 검증 경로에 의존하지 않아 더 보수적이다.</para>
        /// </summary>
        private void ResolveTitleProbeApi()
        {
            if (_titleProbeApi != TitleProbeApi.Unresolved) return;

            try
            {
                InternalGetWindowText(_foregroundHwndThisPass, _titleProbeBuffer, TitleProbeBufferChars);
                _titleProbeApi = TitleProbeApi.Kernel;
            }
            catch (EntryPointNotFoundException)
            {
                _titleProbeApi = TitleProbeApi.DocumentedFallback;
            }
            catch (Exception e)
            {
                // 여기서 예외가 새어 나가면 **발판 열거가 통째로 죽는다** = 캐릭터가 영원히 낙하한다.
                // 조회 실패를 이유로 기능을 멈추지 않는다(이 파일의 IsCloaked/DWM 폴백과 같은 보수 원칙).
                // 폴백 경로는 기능적으로 완전한 대체재이므로 잃는 것이 없다 — 다만 문서화되지 않은
                // export가 예상 밖의 방식으로 실패했다는 사실 자체는 반드시 남긴다(1회).
                _titleProbeApi = TitleProbeApi.DocumentedFallback;
                Debug.LogWarning("[Win32WindowService][제목조회] InternalGetWindowText 확인 중 예외 — " +
                    "문서화된 폴백으로 내려간다(기능은 동일). " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>진단 로그용 — 지금 어느 경로로 도는지. 상수 문자열이라 할당이 없다.</summary>
        private string DescribeTitleProbeApi()
        {
            switch (_titleProbeApi)
            {
                case TitleProbeApi.Kernel: return "InternalGetWindowText(커널 구조체 직접 읽기)";
                case TitleProbeApi.DocumentedFallback: return "GetWindowTextW(문서화된 폴백)";
                default: return "미결정";
            }
        }

        /// <summary>
        /// ★ 2026-08-31 (이월 Major 해소) — macOS의 알파 필터 대응물. OS 조회만 여기서 하고
        /// <b>해석은 전부</b> <see cref="WindowsFootholdFilter.ResolveWindowAlpha"/>가 한다
        /// (그래야 이 개발 환경(macOS)에서도 EditMode로 실측할 수 있다 — 그 클래스 문서 참고).
        ///
        /// WS_EX_LAYERED가 아닌 창에는 GetLayeredWindowAttributes를 아예 부르지 않는다: 그 경우 항상
        /// 실패하므로 호출해봐야 창 수 n만큼 P/Invoke + SetLastError 비용만 늘고 결과는 같다.
        /// </summary>
        private static float ReadWindowAlpha(IntPtr hWnd, int exStyle)
        {
            if ((exStyle & WindowsFootholdFilter.WsExLayered) == 0)
            {
                return WindowsFootholdFilter.ResolveWindowAlpha(exStyle, false, 0u, 255);
            }

            bool ok = GetLayeredWindowAttributes(hWnd, out _, out byte alphaByte, out uint flags);
            return WindowsFootholdFilter.ResolveWindowAlpha(exStyle, ok, flags, alphaByte);
        }

        /// <summary>
        /// ★ 2026-08-31 (이월 Minor 해소) — 창의 <b>실제 시각적 경계</b>를 돌려준다.
        ///
        /// <c>GetWindowRect</c>는 Windows 10/11에서 DWM이 잡아두는 <b>보이지 않는 리사이즈 테두리</b>
        /// (좌/우/하 약 7px)를 포함한다. 그대로 쓰면 (1) 발판이 눈에 보이는 창보다 좌우로 넓어 캐릭터가
        /// 창 밖 허공에 서고, (2) 가려짐 계산에서 앞 창이 실제보다 넓게 지우며, (3) 창 좌표를 겨냥하는
        /// 연출(인질극 닫기버튼 조준 / 로프 앵커)이 일제히 ~7px 어긋난다.
        ///
        /// <c>DWMWA_EXTENDED_FRAME_BOUNDS</c>는 정확히 그 "보이는 프레임"을 물리 픽셀로 돌려주므로
        /// GetWindowRect와 <b>같은 좌표계</b>다(GetCursorPos와도 같다) — 변환이 필요 없다.
        /// 실패(HRESULT != S_OK)하면 DWM 합성이 꺼졌거나 지원하지 않는 창이라는 뜻이므로 조용히
        /// GetWindowRect로 폴백한다: <b>조회 실패를 이유로 멀쩡한 창을 발판에서 지우지 않는다</b>
        /// (IsCloaked와 같은 보수 원칙).
        /// </summary>
        private static bool TryGetVisualWindowRect(IntPtr hWnd, out Rect screenRect)
        {
            if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT dwm, DwmRectSize) == 0)
            {
                int w = dwm.Right - dwm.Left;
                int h = dwm.Bottom - dwm.Top;
                // DWM이 S_OK를 주고도 빈 사각형을 돌려주는 순간이 있다(창 생성 직후 등) — 그때는 폴백.
                if (w > 0 && h > 0)
                {
                    screenRect = new Rect(dwm.Left, dwm.Top, w, h);
                    return true;
                }
            }

            if (!GetWindowRect(hWnd, out RECT raw))
            {
                screenRect = default;
                return false;
            }
            screenRect = new Rect(raw.Left, raw.Top, raw.Right - raw.Left, raw.Bottom - raw.Top);
            return true;
        }

        /// <summary>
        /// 가상 화면(모든 모니터를 감싸는 외접 사각형). macOS의 <c>TryGetMainDisplayBounds</c>에
        /// 대응하지만 <b>주 모니터가 아니라 전체</b>라는 점이 결정적이다 — 주 모니터로 자르면
        /// 보조 모니터 위의 멀쩡한 창이 통째로 탈락한다(같은 이유로 발판 클리핑은 여전히 끈다).
        /// 폭/높이가 0이면 조회 실패로 보고 이 필터 자체를 건너뛴다.
        /// </summary>
        private static bool TryGetVirtualScreenBounds(out Rect bounds)
        {
            int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            if (w <= 0 || h <= 0)
            {
                bounds = default;
                return false;
            }
            bounds = new Rect(GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN), w, h);
            return true;
        }

        private static bool IsCloaked(IntPtr hWnd)
        {
            // HRESULT != S_OK이면 이 속성을 지원하지 않는 OS/창이라는 뜻 — 그때는 "숨겨지지 않음"으로
            // 보수적으로 판정한다(조회 실패를 이유로 멀쩡한 창을 발판에서 지우지 않는다).
            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) != 0) return false;
            return cloaked != 0;
        }

        private bool OnEnumWindow(IntPtr hWnd, IntPtr lParam)
        {
            _enumeratedWindowCount++; // 계측 전용(IWindowEnumerationCostSource) — int 증가 1회.
            WindowsFootholdRejection rejection = ClassifyWindowStyle(hWnd, out float alpha);
            if (rejection != WindowsFootholdRejection.None)
            {
                // 스타일 단계 탈락은 수가 아주 많고(보통 수백 개) 진단 가치도 낮아 사각형까지
                // 기록하지 않고 사유별 개수만 센다(문자열/리스트 할당 0).
                _rejectCounts[(int)rejection]++;
                return true;
            }

            _dwmProbeCount++;                                                // 계측 전용(DWMWA_EXTENDED_FRAME_BOUNDS)
            if (!TryGetVisualWindowRect(hWnd, out Rect screenRect)) return true;

            rejection = WindowsFootholdFilter.ClassifyGeometry(screenRect, alpha,
                _hasVirtualScreenThisPass, _virtualScreenThisPass);
            if (rejection != WindowsFootholdRejection.None)
            {
                _rejectCounts[(int)rejection]++;
                // 알파 탈락만은 사각형까지 남긴다 — 이것이 바로 "전체화면 투명 창이 아래 발판을
                // 전부 지운다"는 이월 Major의 범인이고, 원격에서 범인을 특정하려면 좌표가 필요하다.
                if (rejection == WindowsFootholdRejection.TransparentAlpha) _alphaRejectRects.Add(screenRect);
                return true;
            }

            bool isTopmost = hWnd == _foregroundHwndThisPass;
            // ★ 여기서 바로 발판으로 만들지 않는다(2026-08-31 수정). EnumWindows는 z-order
            // 앞->뒤 순서로 콜백하므로, 일단 그 순서 그대로 원본 목록에 쌓아두고 열거가 끝난 뒤
            // 가려짐 계산을 한 번에 수행해야 "앞 창에 덮인 상단 테두리"를 뺄 수 있다.
            _rawBuffer.Add(new PlatformFoothold(hWnd.ToInt64(), screenRect, isTopmost));
            _rawAlphas.Add(alpha);
            _rawVisibleWidth.Add(0f);
            return true; // true = 열거 계속. 다른 창을 이동/조작하는 코드는 여기 절대 추가하지 않는다.
        }

        /// <summary>
        /// ★ 사용자 신고 버그(2026-08-31): "창이 겹쳐있을때 창이 뒤에 있음에도 그 경계면을 따라 걸음."
        ///
        /// 원인(코드로 확정, 추측 아님): 이 메서드는 <b>EnumWindows가 돌려준 창 전체 사각형을 그대로
        /// 발판으로 내보내고 있었다.</b> 즉 앞 창에 완전히 덮여 사용자 눈에 한 픽셀도 보이지 않는 창의
        /// 상단선도 유효한 발판으로 남았다. macOS는 2026-08-28 라운드에서 이미 이 결함을 고쳤지만,
        /// 그 수정이 MacWindowService의 private 메서드 안에 갇혀 있어 이 구현체가 재사용하지
        /// 못했다(중복이 아니라 <b>누락</b>이었다).
        ///
        /// 수정: 열거를 2패스로 나눈다.
        ///   1패스 — OnEnumWindow가 필터를 통과한 창을 z-order 앞->뒤 순서 그대로 _rawBuffer에 쌓는다.
        ///   2패스 — VisibleTopEdgeSolver가 "앞 창에 덮이지 않고 실제로 보이는 상단 테두리 조각"만
        ///          남기고, 그 조각들만 발판이 된다. 한 조각도 남지 않은 창은 발판을 내지 않으므로
        ///          그 위에 서 있던 캐릭터는 낙하한다(의도된 동작).
        ///
        /// z-order 전제: EnumWindows는 최상위 창을 z-order 앞->뒤로 콜백한다(GetTopWindow +
        /// GetWindow(GW_HWNDNEXT) 순회와 같은 순서). 이 전제가 깨지면 가려짐 판정이 정반대가 되므로,
        /// 순서에 의존한다는 사실을 여기 명시해 둔다 — macOS판이 CGWindowListCopyWindowInfo의
        /// 순서에 의존하는 것과 정확히 같은 계약이다.
        ///
        /// 화면 밖 클리핑(macOS판의 hasDisplay 인자)은 여기서 쓰지 않는다(hasClipBounds=false):
        /// Windows는 멀티 모니터 배치가 자유로워 "주 디스플레이 사각형"으로 자르면 보조 모니터 위의
        /// 멀쩡한 발판이 통째로 사라진다. 이번 신고와 무관한 별개 사안이므로 손대지 않는다.
        /// (대신 "모든 모니터 밖"인 창은 <see cref="WindowsFootholdFilter.ClassifyGeometry"/>의
        ///  가상 화면 검사가 후보 단계에서 뺀다 — 자르는 것과 빼는 것은 다르다.)
        ///
        /// ============================================================================
        /// ★ 2026-08-31 2차 — 후보 필터(이월 결함 2건 해소). 이 단계가 없으면 위 가려짐 수정이
        /// 오히려 새 사고를 만든다
        /// ============================================================================
        /// 가려짐 계산에는 <b>발판이 되는 자격</b>과 <b>가릴 자격</b>이 같다(솔버가 두 역할을 같은
        /// 목록으로 본다 — VisibleTopEdgeSolver.AddWindow 문서). 그래서 <b>눈에 보이지 않는 전체화면
        /// 투명 창</b>(스트리밍/접근성/보안 툴의 HUD)이 z-order 앞에 하나만 있어도 그 아래 <b>멀쩡한
        /// 발판을 전부 지운다.</b> macOS는 <c>kCGWindowAlpha &lt; 0.05</c> 필터로 이걸 원래부터 막고
        /// 있었지만 Windows에는 대응물이 없었다(이월 Major).
        /// 이제 스타일 -> 알파 -> 크기 -> 가상 화면 순으로 macOS와 1:1 대응하는 필터를 통과한 창만
        /// 솔버에 들어간다. 판정 본체는 플랫폼 중립 <see cref="WindowsFootholdFilter"/>에 있어
        /// macOS 개발 환경의 EditMode에서 그대로 실측된다.
        /// </summary>
        public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
        {
            _footholdBuffer.Clear();
            _rawBuffer.Clear();
            _rawAlphas.Clear();
            _rawVisibleWidth.Clear();
            _alphaRejectRects.Clear();
            System.Array.Clear(_rejectCounts, 0, _rejectCounts.Length);
            LastRawWindowCount = 0;
            LastFullyOccludedWindowCount = 0;
            _enumeratedWindowCount = 0;
            _dwmProbeCount = 0;
            _titleProbeTicksThisPass = 0;
            _titleProbeCountThisPass = 0;

            _foregroundHwndThisPass = GetForegroundWindow();
            ResolveTitleProbeApi();   // 최초 1회만 실제 작업을 한다(위 핸들을 쓰므로 순서 유지)
            _hasVirtualScreenThisPass = TryGetVirtualScreenBounds(out _virtualScreenThisPass);

            EnumWindows(_enumWindowsCallback, IntPtr.Zero);
            LastEnumeratedWindowCount = _enumeratedWindowCount;
            LastDwmProbeCount = _dwmProbeCount;
            LastTitleProbeCount = _titleProbeCountThisPass;
            LastTitleProbeMs = (float)(_titleProbeTicksThisPass * TitleProbeTicksToMs);
            LastRawWindowCount = _rawBuffer.Count;
            BuildVisibleTopEdgeFootholds();
            ReportTitleProbeCost();
            ReportFootholdAnomaly();
            CaptureOverlayOrigin();
            return _footholdBuffer;
        }

        /// <summary>
        /// _rawBuffer(z-order 앞->뒤)에서 "다른 창에 가려지지 않은 상단 테두리 조각"만 골라
        /// _footholdBuffer를 채운다. 계산 본체는 macOS와 공유하는 VisibleTopEdgeSolver다.
        /// </summary>
        private void BuildVisibleTopEdgeFootholds()
        {
            _topEdgeSolver.Begin();
            for (int i = 0; i < _rawBuffer.Count; i++)
            {
                _topEdgeSolver.AddWindow(_rawBuffer[i].ScreenRect);
            }
            _topEdgeSolver.Solve(MinVisibleFootholdWidth, false, default);

            for (int s = 0; s < _topEdgeSolver.SegmentCount; s++)
            {
                int i = _topEdgeSolver.GetSegmentWindowIndex(s);
                PlatformFoothold src = _rawBuffer[i];
                Rect r = src.ScreenRect;
                // 핸들과 IsTopmost(=포그라운드 창인가)는 원본 창 그대로 유지한다. FocusWatchDirector가
                // 이 플래그로 "지금 포커스된 창"을 관찰하므로 의미를 바꾸면 안 된다. 바뀌는 것은
                // 사각형의 좌/폭뿐이고, 발판 판정이 쓰는 상단선 높이(r.y)는 그대로다.
                _footholdBuffer.Add(new PlatformFoothold(src.Handle,
                    new Rect(_topEdgeSolver.GetSegmentStartX(s), r.y, _topEdgeSolver.GetSegmentWidth(s), r.height),
                    src.IsTopmost));
            }

            // ★ 2026-08-31 — 솔버의 세 번째 출력(GetVisibleWidth)을 여기서 비로소 쓴다.
            // 지금까지 Windows는 조각 목록만 읽고 이 값을 통째로 버렸다. 그래서 "발판이 사라졌다"는
            // 사고가 나도 그것이 (a) 정상적인 가려짐인지 (b) 투명 오버레이가 다 지운 것인지 구분할
            // 근거가 없었다 = 이월 Minor 3의 실체. macOS는 이 값으로 "완전히 가려짐 N개"를 집계한다.
            for (int i = 0; i < _rawBuffer.Count; i++)
            {
                float visible = _topEdgeSolver.GetVisibleWidth(i);
                _rawVisibleWidth[i] = visible;
                if (visible > 0f) continue;
                LastFullyOccludedWindowCount++;
                _rejectCounts[(int)WindowsFootholdRejection.FullyOccluded]++;
            }
        }

        /// <summary>
        /// ★ 2026-08-31 (이월 Minor 3 해소) — "발판이 통째로 사라졌다"를 <b>원격에서 판별</b>할 수 있게
        /// 하는 최소한의 신호. macOS는 <c>MacOverlayStateEnforcer</c>가 <c>[발판리포트]</c>를 주기적으로
        /// 남기지만 Windows에는 대응물이 없어, 이월 Major(투명 오버레이가 아래 발판을 전부 지움)가
        /// 실제로 터져도 사용자 로그에서 구분할 방법이 없었다.
        ///
        /// 상시 로그를 새로 만들지는 않는다(24시간 상주 앱 — 조용함이 기본). <b>이상 징후일 때만</b>
        /// 남기고, 같은 징후가 반복되는 동안에는 최소 간격을 두어 로그를 도배하지 않는다.
        /// 문자열 조립은 그 조건이 성립한 순간에만 일어나므로 정상 폴링 경로의 할당은 그대로 0이다.
        /// </summary>
        private void ReportFootholdAnomaly()
        {
            // 이상 징후 = "필터를 통과한 창이 있는데 발판이 하나도 안 나왔다" 또는 "투명 오버레이가
            // 하나라도 걸러졌다"(후자는 정상 동작이지만, 바로 그 창이 이월 Major의 잠재적 범인이라
            // 존재 자체를 한 번은 남겨야 원격 진단이 가능하다).
            bool footholdsVanished = _footholdBuffer.Count == 0 && _rawBuffer.Count > 0;
            bool sawTransparentOverlay = _alphaRejectRects.Count > 0;
            if (!footholdsVanished && !sawTransparentOverlay) return;

            // 같은 상황이 계속되는 동안 매 폴링(0.3초)마다 찍지 않는다.
            int signature = (_footholdBuffer.Count * 397) ^ (_rawBuffer.Count * 31) ^ _alphaRejectRects.Count;
            float now = Time.realtimeSinceStartup;
            if (signature == _lastAnomalySignature && now - _lastAnomalyLogTime < AnomalyLogMinIntervalSeconds) return;
            _lastAnomalySignature = signature;
            _lastAnomalyLogTime = now;

            _diagnosticsBuilder.Clear();
            AppendWindowDiagnostics(_diagnosticsBuilder);
            if (footholdsVanished)
            {
                Debug.LogWarning("[Win32WindowService][발판진단] 발판이 0개다 — " + _diagnosticsBuilder);
            }
            else
            {
                Debug.Log("[Win32WindowService][발판진단] 투명(레이어드) 창을 발판/가림 후보에서 제외했다 — "
                    + _diagnosticsBuilder);
            }
        }

        /// <summary>같은 이상 징후를 다시 로그로 남기기까지의 최소 간격(초).</summary>
        private const float AnomalyLogMinIntervalSeconds = 30f;

        /// <summary>
        /// ★★ 2026-09-01 — <b>제목 조회가 다시 비싸지지 않았는가</b>를 지키는 감시선.
        ///
        /// <para>이 라운드가 없앤 것은 "창 하나를 물어보는 데 남의 앱이 대답해 줄 때까지 기다린다"는
        /// 성질이다. 그 성질이 되살아나면(누군가 다시 메시지 기반 API로 바꾸거나, 폴백 경로가
        /// 예상 밖으로 비싸거나) <b>증상은 똑같이 돌아온다</b>. 그래서 시간을 따로 잰다.</para>
        ///
        /// <para><b>로그 정책</b>은 이 파일의 기존 원칙 그대로다 — 24시간 상주 앱이므로 조용함이
        /// 기본이다. 남기는 것은 둘뿐이다:</para>
        /// <list type="number">
        /// <item><b>기동 후 첫 열거에 한 줄</b> — 어느 API 경로로 도는지. 이게 없으면 원격에서
        ///   "고쳤다는데 왜 그대로냐"를 판별할 수 없다(폴백으로 내려갔는지 알 방법이 없다).</item>
        /// <item><b>예산 초과 시에만</b> 30초 간격으로 경고. 예산은 하드코딩이 아니라 프레임
        ///   예산에서 유도한다(<see cref="WindowsFootholdFilter.DeriveTitleProbeBudgetMs"/>).</item>
        /// </list>
        ///
        /// <para>정상 상태의 수치는 침묵하지만 사라지지는 않는다 — <c>[발판진단]</c> 줄이
        /// 이미 이 값을 함께 싣는다(<see cref="AppendWindowDiagnostics"/>). 그 줄은 사용자가 그대로
        /// 복사해 보내는 물건이라, 다음 실기 로그가 스스로 전후 비교를 제공한다.</para>
        /// </summary>
        private void ReportTitleProbeCost()
        {
            if (!_titleProbeApiLogged)
            {
                // ★ 이 한 줄은 폴러의 스톱워치 <b>안쪽</b>에서 찍힌다(기존 [발판진단]도 마찬가지다).
                // 그래서 첫 30초 창의 [발판열거] '최대'에는 로그 쓰기 1회분이 섞인다 — 두 번째
                // 창부터가 순수한 열거 비용이다. 전후 비교는 두 번째 창 이후로 할 것.
                _titleProbeApiLogged = true;
                Debug.Log($"[Win32WindowService][제목조회] {DescribeTitleProbeApi()} 경로로 동작한다 — " +
                    "대상 창의 메시지 루프를 깨우지 않으므로 다른 앱이 바쁘거나 멎어 있어도 우리 " +
                    "프레임은 그만큼 멈추지 않는다. (이전 구현 GetWindowTextLength는 그 반대였고, " +
                    "실기 로그에서 [발판열거] 1회 최대 199.27ms의 원인이었다.)");
            }

            float now = Time.realtimeSinceStartup;
            if (LastTitleProbeMs > _titleProbeWorstMsInWindow) _titleProbeWorstMsInWindow = LastTitleProbeMs;

            if (_titleProbeWindowStartTime < 0f) _titleProbeWindowStartTime = now;
            if (now - _titleProbeWindowStartTime < AnomalyLogMinIntervalSeconds) return;
            _titleProbeWindowStartTime = now;

            float worst = _titleProbeWorstMsInWindow;
            _titleProbeWorstMsInWindow = 0f;

            // 여기서만 Application을 조회한다(30초에 1회) — 열거 경로의 비용이 아니다.
            float budgetMs = WindowsFootholdFilter.DeriveTitleProbeBudgetMs(Application.targetFrameRate);
            if (worst <= budgetMs) return;

            Debug.LogWarning($"[Win32WindowService][제목조회] 30초 최악 {worst:F2}ms > 예산 {budgetMs:F2}ms " +
                $"({DescribeTitleProbeApi()}, 마지막 패스 {LastTitleProbeCount}회/{LastTitleProbeMs:F2}ms). " +
                "제목 조회는 커널 구조체 읽기라 창 수에 비례하는 상수 시간이어야 한다 — 예산을 넘었다면 " +
                "이 단계에 다시 크로스 프로세스 대기가 들어왔다는 뜻이다. [발판열거]의 '최대' 값과 함께 볼 것.");
        }

        /// <summary>
        /// macOS <c>MacWindowService.AppendWindowDiagnostics</c>의 Windows 대응물 — 이번 패스의
        /// 채택/탈락 내역을 한 줄로 덤프한다. 호출한 순간에만 문자열이 만들어진다(폴링 경로 할당 0).
        /// 창 제목/경로/사용자명은 <b>남기지 않는다</b>: 이 로그는 사용자가 그대로 복사해 보내는
        /// 물건이라, 열거한 남의 창 정보를 최소화하는 것이 원칙 2/3의 정신에 맞다.
        /// </summary>
        public void AppendWindowDiagnostics(System.Text.StringBuilder sb)
        {
            sb.Append("발판 ").Append(_footholdBuffer.Count).Append("조각 / 후보창 ").Append(LastRawWindowCount)
              .Append("개 (완전히 가려짐 ").Append(LastFullyOccludedWindowCount).Append("개) [");
            for (int i = 0; i < _rawBuffer.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                Rect r = _rawBuffer[i].ScreenRect;
                sb.Append('z').Append(i).Append(":(")
                  .Append(r.x.ToString("F0")).Append(',').Append(r.y.ToString("F0")).Append(' ')
                  .Append(r.width.ToString("F0")).Append('x').Append(r.height.ToString("F0")).Append(')')
                  .Append(" alpha=").Append(_rawAlphas[i].ToString("F2"))
                  .Append(" 보이는상단폭=").Append(_rawVisibleWidth[i].ToString("F0"));
            }
            sb.Append("] / 알파탈락 ").Append(_alphaRejectRects.Count).Append("개 [");
            for (int i = 0; i < _alphaRejectRects.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                Rect r = _alphaRejectRects[i];
                sb.Append('(').Append(r.x.ToString("F0")).Append(',').Append(r.y.ToString("F0")).Append(' ')
                  .Append(r.width.ToString("F0")).Append('x').Append(r.height.ToString("F0")).Append(')');
            }
            sb.Append("] / 사유별 [");
            bool first = true;
            for (int i = 0; i < _rejectCounts.Length; i++)
            {
                if (_rejectCounts[i] == 0) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(WindowsFootholdFilter.Describe((WindowsFootholdRejection)i)).Append('=').Append(_rejectCounts[i]);
            }
            sb.Append(']');

            // ★ 2026-09-01 — 제목 조회 실측치. 이 줄은 사용자가 그대로 복사해 보내는 물건이라,
            // 다음 실기 로그가 "블로킹 제거가 먹었는가"를 스스로 답하게 된다. 이전 구현
            // (GetWindowTextLength)에서는 이 한 단계가 열거 전체의 199ms를 만들 수 있었다.
            sb.Append(" / 제목조회 ").Append(LastTitleProbeCount).Append("회 ")
              .Append(LastTitleProbeMs.ToString("F3")).Append("ms(").Append(DescribeTitleProbeApi()).Append(')');
        }

        /// <summary>
        /// IRawWindowRectSource — 창 도둑(UX_FLOW.md 27-1)이 쓰는 "가려짐 필터 이전" 원본 목록.
        /// 창 전체 사각형이며 z-order 앞->뒤 순서다.
        ///
        /// ★ 2026-08-31 이전에는 이 구현체에 가려짐 분할이 아예 없어서 "발판 목록 == 원본 목록"이었고
        /// 이 프로퍼티가 _footholdBuffer를 그대로 돌려줬다. 이제 발판은 보이는 조각으로 잘리므로 둘이
        /// 갈라졌다 — 창 도둑은 "딛는" 것이 아니라 "미는" 연출이라 가려진 창도 대상이 되어야 하므로
        /// 반드시 이 원본 쪽을 봐야 한다(macOS판과 동일한 이유·동일한 계약).
        ///
        /// "원본"은 <b>가려짐 필터 이전</b>이라는 뜻이지 <b>모든 창</b>이라는 뜻이 아니다 —
        /// 후보 필터(보이지 않음/최소화/투명/너무 작음 등)에 걸린 창은 여기에도 없다. macOS도
        /// 동일하다(<c>_rawWindowBuffer</c>는 탈락 판정 뒤에 쌓인다). 창 도둑이 사용자 눈에 보이지도
        /// 않는 투명 창을 "미는" 연출을 하면 그림이 성립하지 않으므로 이쪽이 맞는 계약이다.
        /// </summary>
        public IReadOnlyList<PlatformFoothold> RawWindows => _readOnlyRawWindows;

        /// <summary>
        /// 우리 오버레이 창의 화면상 좌상단/폭을 ScreenCoordinateConverter에 보고한다
        /// (그 클래스의 OverlayOriginOsScreen/AutoDpiScale 문서 참고 — 원점과 배율이 항상 같은
        /// 관측에서 나와야 커서↔월드 변환이 어긋나지 않는다).
        ///
        /// macOS판(CaptureOverlayOrigin(windowDict))과 달리 열거 루프 안에서 자기 창을 골라낼 필요가
        /// 없다: Windows에서는 이미 확보해 둔 _overlayHwnd에 GetWindowRect 한 번이면 끝이고, 그 좌표계가
        /// GetCursorPos(전역 데스크톱 좌표, 좌상단 원점)와 정확히 같아서 변환도 필요 없다.
        /// </summary>
        private void CaptureOverlayOrigin()
        {
            if (_overlayHwnd == IntPtr.Zero) return;
            // ★ 2026-08-31 — 여기도 시각적 경계를 쓴다. 출하 형상(UniWindowController의 보더리스 +
            // 투명)에서는 DWM 확장 프레임과 GetWindowRect가 같은 값이라 동작이 바뀌지 않지만,
            // 보더리스가 아직 적용되지 않은 기동 직후 몇 프레임에는 GetWindowRect가 보이지 않는
            // 테두리를 포함해 원점을 좌상단으로 밀고 AutoDpiScale(창 폭 / Screen.width)까지 함께
            // 부풀린다. "틀릴 수 있는 상황에서만 개입하고 아닌 경우엔 아무것도 바꾸지 않는" 방향이다.
            if (!TryGetVisualWindowRect(_overlayHwnd, out Rect osRect)) return;

            int width = (int)osRect.width;
            int height = (int)osRect.height;
            if (width <= 0 || height <= 0) return;
            bool originMoved = Vector2.Distance(osRect.position, ScreenCoordinateConverter.OverlayOriginOsScreen) > 0.5f;
            // ★ 2026-09-01 — 원점 위생 검사(신고 "창에서 가끔 갑자기 떨어짐"의 근본 원인 3).
            // 가상 화면(모든 모니터의 외접 사각형)은 이번 패스 맨 앞에서 이미 조회해 뒀으므로
            // 추가 시스템 호출이 없다. 판정/영구고착 방지는 ScreenCoordinateConverter가 담당한다.
            if (_hasVirtualScreenThisPass)
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(osRect, _virtualScreenThisPass);
            }
            else
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(osRect);
            }
            // ★ UI 밀도는 원점 보고의 채택/거부와 **무관하게** 항상 갱신한다.
            //   CaptureUiDensity()는 로그만 찍는 함수가 아니라 ReportUiDensityScale()로 캔버스 배율을
            //   실제로 바꾸는 부수효과가 있다(macOS에는 이 함수 자체가 없다 — Retina 배율이 창 폭 비에
            //   실려 오기 때문). 아래 조기 반환보다 먼저 두지 않으면 원점이 거부되는 동안 표시 배율
            //   변경이 반영되지 않는다.
            float density = CaptureUiDensity();

            // ============================================================================
            // ★★ 2026-09-02 계측 정직성 수정 — macOS와 **글자 그대로 같은 결함**이 여기에도 있었다
            // ============================================================================
            // 이 줄은 바로 위 ReportOverlayWindowOsRect가 **거부한 보고까지** "갱신"으로 찍고 있었다.
            // macOS 실기 로그(/tmp/stickmate-run/stickmate.log)에서 정확히 이 모양이 21번 나왔다:
            //     874: [원점위생] ... 버렸습니다 — 보고=(x:-1007 ...), 유지 중인 원점=(0.00, 0.00)
            //     875: [MacWindowService] ... 갱신 — origin=(-1007.00, 0.00) ...   <- 갱신된 적 없다
            // 그 결과 **로그만 읽은 사람이 "원점이 -1007로 튀었다"고 판단**했고, 그 틀린 숫자가 리더
            // 브리핑과 페르소나 보고서로 그대로 흘러갔다. Windows에서 같은 사고가 일어날 경로도 이미
            // 있다 — Win+D/최소화 시 GetWindowRect가 (-32000,-32000)을 돌려주고, 그 값은 위생 검사가
            // 거부한다(ScreenCoordinateConverter 문서의 Windows 항목).
            //
            // 좌표계의 **진실**은 ScreenCoordinateConverter.OverlayOriginOsScreen 하나뿐이므로 그 값을
            // 찍고, 이번 보고가 반영됐는지를 같은 줄에 명시한다.
            // ★ 거부된 보고는 여기서 **아예 찍지 않는다**(침묵이 아니다 — 거부는 [원점위생]이 연속
            //   1,2,4,8...회째로 이미 남긴다). 이 줄까지 거부마다 찍으면 최소화 한 번에 같은 사건이
            //   두 태그로 두 번씩 쌓인다(24시간 상주 앱 — 로그 예산을 늘리지 않는다).
            Vector2 effectiveOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            bool reportAccepted = Vector2.Distance(effectiveOrigin, osRect.position) <= 0.5f;
            if (!reportAccepted && _overlayOriginLogged) return;

            if (!_overlayOriginLogged || originMoved
                || Mathf.Abs(ScreenCoordinateConverter.AutoDpiScale - _lastLoggedDpiScale) > 0.01f)
            {
                _overlayOriginLogged = true;
                _lastLoggedDpiScale = ScreenCoordinateConverter.AutoDpiScale;
                Debug.Log($"[Win32WindowService] 오버레이 창 원점/배율 " +
                    $"{(reportAccepted ? "갱신" : "보고 **거부됨**(위생 검사) — 좌표계는 직전 값 유지")} — " +
                    $"실효 원점={effectiveOrigin}, 이번 보고={osRect.position}, " +
                    $"size=({width}x{height}), Screen=({Screen.width}x{Screen.height}) " +
                    $"-> desktopDpiScale(자동)={ScreenCoordinateConverter.AutoDpiScale:F3}, " +
                    $"UI 밀도(캔버스 배율)={density:F3} (디스플레이 배율 {(density * 100f):F0}%).");
            }
        }

        // 직전에 보고한 UI 밀도. 값이 바뀔 때만 로그를 남기기 위한 부기다.
        private float _lastUiDensity = -1f;
        // GetDpiForWindow가 이 OS에 없으면(Win10 1607 미만) 다시 시도하지 않는다.
        private bool _dpiApiUnavailable;

        /// <summary>
        /// ★ 2026-08-31 — 사용자 신고 "캐릭터창 해상도도 엄청 낮아서 글씨도 잘 안보임"의 수정.
        ///
        /// ============================================================================
        /// 왜 Windows에서만 UI가 작아지는가 (macOS와의 단위 차이)
        /// ============================================================================
        /// 이 앱의 UI 상수는 전부 <b>논리 포인트</b> 기준으로 눈에 맞춰져 있고, 캔버스 배율은
        /// <see cref="ScreenCoordinateConverter.ResolveCanvasScaleFactor"/> 하나가 결정한다. 그 값은
        /// 지금까지 <c>1 / AutoDpiScale</c>, 즉 <b>창 사각형(OS 단위) 대 Screen.width(Unity 픽셀)의
        /// 비</b>에서만 나왔다.
        ///   · macOS: 창 사각형은 <b>AppKit 포인트</b>(1512), Screen.width는 <b>백킹 픽셀</b>(3024)
        ///     -> 비가 0.5 -> 캔버스 배율 2. <b>디스플레이 배율이 이 비에 실려 온다.</b>
        ///   · Windows: 창 사각형(GetWindowRect)도 Screen.width도 <b>둘 다 물리 픽셀</b>
        ///     -> 비가 항상 1.0 -> 캔버스 배율 1. <b>디스플레이 배율이 어디에도 실리지 않는다.</b>
        /// 그래서 디스플레이 배율 150%인 PC에서 "14pt로 보여야 할 글자"가 14 물리 픽셀로 그려진다 =
        /// 의도한 크기의 1/1.5. 사용자가 본 "해상도가 낮고 글씨가 안 보인다"가 정확히 이것이다.
        /// (같은 이유로 초상화 RenderTexture의 슈퍼샘플 배율도 1배에 머물러 실제로 덜 선명했다 —
        ///  그 배율도 같은 함수에서 나온다.)
        ///
        /// 수정: 좌표 변환용 배율(<see cref="ScreenCoordinateConverter.AutoDpiScale"/>, Windows에서
        /// 1.0이 맞다 — 창 좌표와 커서 좌표가 같은 물리 픽셀이다)과 <b>UI 밀도</b>를 분리하고, 밀도만
        /// OS에서 직접 읽는다. <c>GetDpiForWindow(hwnd) / 96</c>이 정의상 "논리 포인트 1개당 물리 픽셀
        /// 수"이므로 그것이 곧 캔버스 배율이다.
        ///
        /// 안전성(이 환경에서 실행 검증이 불가능하므로 특히 중요): Unity Player가 DPI 인식을 하지
        /// <b>않는</b> 환경이면 OS가 창 좌표를 가상화하고 <c>GetDpiForWindow</c>도 96을 돌려준다
        /// -> 밀도 1.0 -> <b>직전과 완전히 동일한 동작</b>. 즉 이 수정은 "틀릴 수 있는 상황에서는
        /// 아무것도 바꾸지 않는" 방향으로만 개입한다.
        /// </summary>
        private float CaptureUiDensity()
        {
            float density = 1f;
            if (!_dpiApiUnavailable)
            {
                try
                {
                    uint dpi = GetDpiForWindow(_overlayHwnd);
                    if (dpi > 0) density = dpi / 96f;
                }
                catch (EntryPointNotFoundException)
                {
                    _dpiApiUnavailable = true;
                    Debug.LogWarning("[Win32WindowService] GetDpiForWindow를 찾을 수 없습니다(Windows 10 1607 미만) — " +
                        "UI 밀도를 1.0(디스플레이 배율 100%)으로 둡니다. 고배율 디스플레이에서 UI가 작게 보일 수 있습니다.");
                }
                catch (DllNotFoundException)
                {
                    _dpiApiUnavailable = true;
                }
            }

            ScreenCoordinateConverter.ReportUiDensityScale(density);
            if (Mathf.Abs(density - _lastUiDensity) > 0.001f)
            {
                _lastUiDensity = density;
                Debug.Log($"[Win32WindowService] UI 밀도 갱신 — 캔버스 배율 {density:F3} " +
                    $"(디스플레이 배율 {(density * 100f):F0}%). 좌표 변환용 배율(AutoDpiScale=" +
                    $"{ScreenCoordinateConverter.AutoDpiScale:F3})과는 별개 값이다.");
            }
            return density;
        }

        #endregion

        #region 오버레이 배선 3종 — UniWindowController 어댑터(macOS와 동일 경로)

        /// <summary>
        /// 씬에 배치된 UniWindowController(Assets/Editor/SceneBootstrapper.cs가 자동 생성)를 찾는다.
        /// 그 GameObject는 의도적으로 **비활성**으로 저장되어 있으므로(헤드리스 실행에서 네이티브
        /// 창 탐색이 프로세스를 크래시시키기 때문 — SceneBootstrapper의 "매우 중요" 주석 참고)
        /// 비활성 오브젝트까지 포함해 찾고, 필요하면 여기서 활성화한다.
        /// 없으면 새로 만들지 않고 null을 반환한다(조용한 no-op 금지 — 호출부가 명시적으로 실패 처리).
        /// </summary>
        private static UniWindowController ResolveController(bool activateIfInactive)
        {
            var controller = UniWindowController.current;
            if (controller == null)
            {
                controller = UnityEngine.Object.FindAnyObjectByType<UniWindowController>(FindObjectsInactive.Include);
            }
            if (controller == null) return null;

            if (activateIfInactive && !controller.gameObject.activeSelf)
            {
                Debug.Log("[Win32WindowService] 씬의 UniWindowController가 비활성 상태 — 실제 Player에서만 " +
                    "활성화한다는 설계대로 지금 활성화합니다(SetActive(true) -> Awake() 동기 실행).");
                controller.gameObject.SetActive(true);
            }
            return controller;
        }

        private static UniWindowController Controller => ResolveController(activateIfInactive: false);

        private WindowsOverlayStateEnforcer _enforcer;

        /// <summary>
        /// UniWindowController를 확보하고 "진짜 투명 오버레이" 초기 상태를 적용한다(MacWindowService와 동일).
        ///   isTransparent=true — 창이 회색 사각형이 아니라 바탕화면 위에 직접 그려진다. Windows에서는
        ///     라이브러리가 이때 SetBorderless(true)도 함께 걸어 제목표시줄/테두리를 없앤다.
        ///   isClickThrough=false / isHitTestEnabled=false — 클릭관통은 반드시 꺼진 채로 시작한다
        ///     (StickmanAgent.Start()의 5초 지연 안전장치와 이중으로 겹친다).
        ///   isTopmost=false — SetAlwaysOnTop()이 이후 명시적으로 켠다.
        ///
        /// _overlayHwnd는 여전히 확보한다 — 다만 이제 **조회 용도 전용**이다(자기 창 제외 판정,
        /// 전체화면 판정 시 자기 자신 배제, 오버레이 원점 보고). 이 핸들로 창을 조작하는 호출은 없다.
        ///
        /// 주의(공식 문서 경고): 투명은 Unity 에디터에서 동작하지 않는다 — 반드시 Standalone 빌드로
        /// 검증해야 한다(UniWinCore.EnableTransparent가 `#if !UNITY_EDITOR`로 감싸여 있다).
        /// </summary>
        public bool CreateOverlayWindow()
        {
            using (var self = System.Diagnostics.Process.GetCurrentProcess())
            {
                _overlayHwnd = self.MainWindowHandle;
            }

            var controller = ResolveController(activateIfInactive: true);
            if (controller == null)
            {
                Debug.LogWarning("[Win32WindowService] CreateOverlayWindow(): 씬에서 UniWindowController를 " +
                    "찾지 못했습니다(UniWindowController.current == null). SceneBootstrapper가 프리팹을 " +
                    "배치했는지 확인하세요 — 이후 SetClickThrough/SetAlwaysOnTop 호출이 모두 실패합니다.");
                return false;
            }

            // 순서 중요(macOS와 동일): 히트테스트 자동 제어를 먼저 끈 뒤에 클릭관통을 끈다.
            controller.isHitTestEnabled = false;
            controller.isClickThrough = false;
            controller.isTopmost = false;
            controller.isTransparent = true;

            _enforcer = WindowsOverlayStateEnforcer.EnsureExists(controller);
            _enforcer.DesiredTransparent = true;
            _enforcer.DesiredTopmost = false;
            _enforcer.DesiredClickThrough = false;
            _enforcer.DesiredHitTest = false;
            // 재적합(전체화면 확장/해상도 변경 후 재무장)이 끝난 프레임에 좌표계를 즉시 갱신하는 훅.
            // 폴링(0.5초)을 기다리면 그 사이 원점/배율이 옛 값이라 캐릭터가 화면 밖으로 튄다.
            _enforcer.OverlayRectReporter = CaptureOverlayOrigin;
            // 항상위 상시 감시(2026-09-01)가 OS 실측(GetWindowLong(GWL_EXSTYLE))을 하려면 HWND가 필요하다.
            // 이 핸들은 이미 좌표계 원점 보고(CaptureOverlayOrigin)와 DPI 조회에 쓰이고 있어 실기에서
            // 검증된 값이며, 감시자 쪽에서 IsWindow()로 유효성을 한 번 더 확인한다.
            _enforcer.OverlayHandle = _overlayHwnd;
            _enforcer.MarkDirty();

            Debug.Log("[Win32WindowService] CreateOverlayWindow(): UniWindowController 확보 및 초기 상태 적용 완료 " +
                $"(isTransparent={controller.isTransparent}, isClickThrough={controller.isClickThrough}, " +
                $"isTopmost={controller.isTopmost}, isHitTestEnabled={controller.isHitTestEnabled}, " +
                $"hitTestType={controller.hitTestType}, transparentType={controller.transparentType}, " +
                $"clientSize={controller.clientSize}, windowPosition={controller.windowPosition}, " +
                $"overlayHwnd=0x{_overlayHwnd.ToInt64():X}).");
            return true;
        }

        /// <summary>
        /// 클릭관통 on/off. isClickThrough 하나만 건드리면 라이브러리의 매 프레임 자동 제어
        /// (UpdateClickThrough)가 다음 프레임에 덮어쓰므로 isHitTestEnabled를 함께 제어한다 —
        /// 그래야 Escape 긴급 해제/시작 5초 유예가 실제로 "계속" 유지된다(MacWindowService와 동일).
        /// 인스턴스가 없으면 조용히 무시하지 않고 NotSupportedException으로 즉시 실패를 알린다.
        /// </summary>
        public void SetClickThrough(bool enabled)
        {
            var controller = Controller;
            if (controller == null)
            {
                throw new NotSupportedException(
                    "Win32WindowService.SetClickThrough(): 씬에서 UniWindowController를 찾지 못해 클릭관통을 " +
                    "적용할 수 없습니다. SceneBootstrapper가 프리팹을 배치했는지, CreateOverlayWindow()가 " +
                    "먼저 호출되었는지 확인하세요.");
            }

            if (enabled)
            {
                controller.isClickThrough = true;
                controller.isHitTestEnabled = true;
            }
            else
            {
                controller.isHitTestEnabled = false;
                controller.isClickThrough = false;
            }

            if (_enforcer != null)
            {
                _enforcer.DesiredClickThrough = enabled;
                _enforcer.DesiredHitTest = enabled;
                _enforcer.MarkDirty();
            }

            Debug.Log($"[Win32WindowService] SetClickThrough({enabled}) 적용 완료 — " +
                $"isClickThrough={controller.isClickThrough}, isHitTestEnabled={controller.isHitTestEnabled}, " +
                $"isTransparent={controller.isTransparent}.");
        }

        /// <summary>
        /// 항상위. 이전 라운드의 Win32 Z-order 직접 호출(HWND_TOPMOST)을 대체한다 — 라이브러리가
        /// 자기 창에만 그 작업을 수행하므로 이 파일에서 쓰기 계열 Win32 호출이 사라졌다.
        /// 되읽은 값이 목표와 다른 것은 실패가 아니라 "아직 창 부착 전"이라는 뜻이며,
        /// WindowsOverlayStateEnforcer가 부착 직후 재적용한다.
        /// </summary>
        public void SetAlwaysOnTop(bool enabled)
        {
            var controller = Controller;
            if (controller == null)
            {
                throw new NotSupportedException(
                    "Win32WindowService.SetAlwaysOnTop(): 씬에서 UniWindowController를 찾지 못해 항상위 " +
                    "설정을 적용할 수 없습니다. SceneBootstrapper가 프리팹을 배치했는지, " +
                    "CreateOverlayWindow()가 먼저 호출되었는지 확인하세요.");
            }

            controller.isTopmost = enabled;

            if (_enforcer != null)
            {
                _enforcer.DesiredTopmost = enabled;
                _enforcer.MarkDirty();
            }

            Debug.Log($"[Win32WindowService] SetAlwaysOnTop({enabled}) 적용 완료 — isTopmost={controller.isTopmost}" +
                (controller.isTopmost != enabled ? " (아직 창 부착 전 — Enforcer가 재적용 예정)" : "") + ".");
        }

        #endregion

        // ICursorPositionService — 클릭 관통 여부와 무관하게 전역 커서 좌표를 조회하는 독립 경로
        // (UX_FLOW.md 9절-3 요구사항). GetCursorPos가 돌려주는 좌표계는 GetWindowRect/발판 사각형과
        // 동일한 전역 데스크톱 좌표(좌상단 원점)라 별도 변환이 없다.
        public bool TryGetGlobalCursorPosition(out Vector2 osScreenPosition)
        {
            if (GetCursorPos(out POINT p))
            {
                osScreenPosition = new Vector2(p.X, p.Y);
                return true;
            }
            osScreenPosition = Vector2.zero;
            return false;
        }

        #region IGlobalPointerButtonService / IGlobalKeyStateService (조회 전용)

        private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & KeyDownMask) != 0;

        public bool TryGetPrimaryButtonPressed(out bool pressed)
        {
            pressed = IsDown(VK_LBUTTON);
            return true;
        }

        public bool TryGetSecondaryButtonPressed(out bool pressed)
        {
            pressed = IsDown(VK_RBUTTON);
            return true;
        }

        /// <summary>
        /// GlobalKey -> Windows 가상 키코드. 조합키 3개의 매핑은 macOS 키보드의 물리적 대응 관계를
        /// 그대로 따른다(같은 위치의 키를 누르면 같은 기능):
        ///   Control -> Ctrl, Option -> Alt, Command -> Windows 키
        /// 즉 macOS의 Ctrl+Opt+Cmd+X는 Windows에서 Ctrl+Alt+Win+X가 된다.
        ///
        /// Windows 키를 조합에 넣어도 안전한 이유: 셸 단축키(Win+X 등)는 **정확히 그 조합만** 눌렸을
        /// 때 발동하므로 Ctrl+Alt가 함께 눌린 상태에서는 발동하지 않고, 시작 메뉴는 Win 키를 단독으로
        /// 눌렀다 뗐을 때만 열린다(사이에 문자 키가 들어가면 열리지 않는다).
        ///
        /// <para>★ 2026-09-01 — 이 "정확히 그 조합만" 성질은 <b>이 앱의 안전 근거 전체</b>다.
        /// 동작키에는 이미 Win 셸이 단독 조합으로 쓰는 글자가 여럿 들어와 있다(Win+D 바탕화면 /
        /// Win+R 실행 / Win+I 설정 / Win+S 검색 / Win+X 전원 메뉴, 그리고 이번에 추가된
        /// <b>Win+P 디스플레이 전환</b>). 셋 다 <c>RegisterHotKey</c> 계열의 <b>정확 일치</b>
        /// 매칭이라 Ctrl+Alt가 함께 눌린 우리 조합에서는 발동하지 않는다.
        /// <b>macOS는 사정이 다르다</b> — 그쪽 접근성 단축키는 <c>⌃⌥⌘</c> 마스크를 통째로 예약해
        /// 두었고(<c>8</c>/<c>,</c>/<c>.</c>), 그래서 원래 쉼표였던 설정창 단축키가 사용자의 OS
        /// 대비 설정을 실제로 바꾸는 사고를 냈다. 금지 목록은 플랫폼 중립 위치인
        /// <c>Core/ShortcutLabel</c>에 있다.</para>
        ///
        /// **비침해 원칙 유지(macOS와 동일)**: 조회만 하고 어떤 입력도 주입하지 않으며, 아래 switch에
        /// 열거된 키 외에는 애초에 물어볼 수단이 없다. 소비자(AppControlDirector)가 조합키 3개가 모두
        /// 눌린 상태에서만 동작키를 확인하므로, 사용자가 다른 앱에서 타이핑하는 내용은 이 채널로
        /// 관측될 수 없다.
        /// </summary>
        public bool TryGetKeyPressed(GlobalKey key, out bool pressed)
        {
            switch (key)
            {
                // 좌/우 어느 쪽을 눌러도 같게 취급한다(VK_CONTROL/VK_MENU는 이미 좌우 통합 코드).
                case GlobalKey.Control: pressed = IsDown(VK_CONTROL); return true;
                case GlobalKey.Option:  pressed = IsDown(VK_MENU);    return true;
                case GlobalKey.Command: pressed = IsDown(VK_LWIN) || IsDown(VK_RWIN); return true;
            }

            // 문자 키의 가상 키코드는 대문자 ASCII와 같다(VK_A = 0x41 ... VK_Z = 0x5A). 이는
            // winuser.h가 명시적으로 보장하는 규약이라 표를 따로 두지 않는다.
            char letter;
            switch (key)
            {
                case GlobalKey.Q: letter = 'Q'; break;
                case GlobalKey.C: letter = 'C'; break;
                case GlobalKey.D: letter = 'D'; break;
                case GlobalKey.R: letter = 'R'; break;
                case GlobalKey.B: letter = 'B'; break;
                case GlobalKey.K: letter = 'K'; break;
                case GlobalKey.G: letter = 'G'; break;
                case GlobalKey.T: letter = 'T'; break;
                case GlobalKey.X: letter = 'X'; break;
                case GlobalKey.H: letter = 'H'; break;
                case GlobalKey.S: letter = 'S'; break;
                case GlobalKey.N: letter = 'N'; break;
                case GlobalKey.J: letter = 'J'; break;
                case GlobalKey.F: letter = 'F'; break;
                case GlobalKey.A: letter = 'A'; break;
                case GlobalKey.I: letter = 'I'; break;
                // 설정창(Preferences). 2026-09-01 이전에는 GlobalKey.Comma(VK_OEM_COMMA)였고, 이 목록
                // 바깥에서 명시적으로 처리하던 유일한 항목이었다 — macOS가 ⌃⌥⌘,를 접근성 단축키로
                // 예약해 둔 것이 밝혀져 P로 옮기면서, 이제 모든 동작키가 이 ASCII 규약 하나로 끝난다.
                case GlobalKey.P: letter = 'P'; break;
                default:
                    pressed = false;
                    return false;
            }

            pressed = IsDown(letter);
            return true;
        }

        #endregion

        // ILocalClickCaptureService(UX_FLOW.md 15절, 부분적 클릭관통 해제) — 소유권/영역 부기는
        // LocalClickCaptureGate에 위임한다. 실제 OS 히트테스트는 이제 UniWindowController가
        // (hitTestType=Raycast) 담당하므로 여기서 창 리전을 직접 바꾸지 않는다 — MacWindowService와
        // 완전히 같은 구조다. 이 부기를 구현하지 않으면 FallbackPlatformWindowService의
        // RequestLocalClickCapture가 항상 false를 돌려줘 드래그&던지기가 조용히 죽는다
        // (macOS에서 실제로 발생했던 사고 — MacWindowService.cs의 해당 절 참고).
        private readonly LocalClickCaptureGate _clickCaptureGate = new LocalClickCaptureGate();

        public bool RequestLocalClickCapture(Rect hitboxOsScreen, object owner)
            => _clickCaptureGate.TryRequestCapture(hitboxOsScreen, owner);

        public void UpdateLocalClickCaptureRegion(Rect hitboxOsScreen, object owner)
            => _clickCaptureGate.UpdateRegion(hitboxOsScreen, owner);

        public void ReleaseLocalClickCapture(object owner)
            => _clickCaptureGate.ReleaseCapture(owner);

        public bool IsLocalClickCaptureOwnedBy(object owner)
            => _clickCaptureGate.IsOwnedBy(owner);

        #region IReservedBottomBarService — 작업표시줄(하단 예약 막대) 실측

        // 작업표시줄 구간 로그는 값이 바뀔 때만 남긴다(이 함수는 발판 폴링마다 불린다).
        private Rect _lastLoggedBottomBar = new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
        private bool _loggedNoBottomBar;

        /// <summary>
        /// ★ 2026-08-31 — 사용자 신고 "작업표시줄에 걸쳐서 돌아다닌다"의 수정
        /// (근거/이전 동작은 Platform/IReservedBottomBarService.cs 문서 참고).
        ///
        /// <c>rcMonitor</c>(모니터 전체, 작업표시줄 포함)와 <c>rcWork</c>(작업 영역, 작업표시줄 제외)의
        /// <b>하단 차이</b>가 곧 화면 아래쪽에 예약된 띠의 두께다. 추정이 하나도 들어가지 않는다.
        ///
        /// 이 한 번의 조회가 동시에 처리하는 경우들(별도 분기가 필요 없다):
        ///   · 작업표시줄이 화면 <b>좌/우/상단</b>에 있음 -> 하단 차이가 0 -> false(하단 막대 없음).
        ///   · <b>자동 숨김</b>이 켜져 있음 -> Windows가 작업 영역을 줄이지 않는다 -> 차이 0 -> false.
        ///     (macOS Dock 쪽에서 IsAutoHidden 플래그로 따로 처리하던 것과 같은 결론에 도달한다.)
        ///   · 디스플레이 배율 125%/150% -> rcMonitor/rcWork 둘 다 물리 픽셀이라 두께가 자동으로 따라온다.
        ///   · <b>도킹된 툴바</b>(appbar)가 아래에 붙어 있음 -> 그것도 예약 영역이므로 함께 피한다(정확).
        ///   · 멀티 모니터 -> 오버레이 창이 실제로 놓인 모니터 기준으로 계산된다.
        ///
        /// 기준 창을 <c>_overlayHwnd</c>로 삼는 이유: 발판/커서/오버레이 원점이 전부 "우리 창이 놓인
        /// 모니터"를 기준으로 하는데 여기만 주 모니터를 쓰면 보조 모니터에서 통째로 어긋난다.
        /// </summary>
        public bool TryGetReservedBottomBarOsScreen(out Rect osScreenRect)
        {
            osScreenRect = default;
            if (_overlayHwnd == IntPtr.Zero) return false;

            IntPtr monitor = MonitorFromWindow(_overlayHwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return false;

            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref mi)) return false;

            float thickness = mi.rcMonitor.Bottom - mi.rcWork.Bottom;
            float width = mi.rcMonitor.Right - mi.rcMonitor.Left;
            if (thickness <= 0f || width <= 0f)
            {
                if (!_loggedNoBottomBar)
                {
                    _loggedNoBottomBar = true;
                    _lastLoggedBottomBar = new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
                    Debug.Log("[Win32WindowService] 하단 예약 막대 없음 — 작업표시줄이 자동 숨김이거나 " +
                        "화면 좌/우/상단에 있습니다(rcWork 하단 == rcMonitor 하단). " +
                        "발판은 화면 최하단 안전망만 전체 폭으로 남습니다.");
                }
                return false;
            }

            osScreenRect = new Rect(mi.rcMonitor.Left, mi.rcWork.Bottom, width, thickness);

            if (osScreenRect != _lastLoggedBottomBar)
            {
                _lastLoggedBottomBar = osScreenRect;
                _loggedNoBottomBar = false;
                Debug.Log($"[Win32WindowService] 작업표시줄 실측 — rect={osScreenRect} " +
                    $"(모니터 {mi.rcMonitor.Right - mi.rcMonitor.Left}x{mi.rcMonitor.Bottom - mi.rcMonitor.Top}, " +
                    $"작업영역 하단 y={mi.rcWork.Bottom}, 두께 {thickness:F0}px). 캐릭터는 이 띠 위에 섭니다.");
            }
            return true;
        }

        #endregion

        #region IReservedTopBarService — 상단 도킹 작업표시줄/툴바 두께 실측

        // 값이 바뀔 때만 로그. 이 함수는 UI 표면 4곳이 ReservedTopBarProbe를 통해 0.5초마다 부른다.
        private float _lastLoggedTopInsetPoints = float.NaN;

        /// <summary>
        /// ★ 2026-09-02 — docs/UX_FLOW.md 41-1 ③의 <b>Windows 쪽 사실 조회</b>.
        /// macOS(<c>MacReservedTopBarService</c>)와 <b>같은 정책 / 다른 조회</b>다 — 이 값을 가지고
        /// 무엇을 할지는 전부 플랫폼 중립 <see cref="SurfaceSafeAreaPolicy"/>가 정한다.
        ///
        /// ============================================================================
        /// 조회는 이미 하고 있던 그 한 번이다 (새 P/Invoke 0줄)
        /// ============================================================================
        /// <c>rcWork.Top − rcMonitor.Top</c>이 곧 화면 <b>위쪽</b>에 예약된 띠의 두께다.
        /// 바로 위 <see cref="TryGetReservedBottomBarOsScreen"/>이 같은 <c>MONITORINFO</c>에서
        /// <b>아래쪽</b> 차이를 쓰고 있다. 한 번의 <c>GetMonitorInfo</c>에 두 사실이 함께 들어 있다.
        ///
        /// 이 한 줄이 동시에 처리하는 경우들(별도 분기가 필요 없다):
        ///   · 작업표시줄이 <b>하단/좌/우</b>에 있음  -> 상단 차이 0 -> false(상단 예약 띠 없음).
        ///   · <b>자동 숨김</b>                        -> Windows가 작업 영역을 줄이지 않는다 -> 0 -> false.
        ///   · 상단에 도킹된 <b>서드파티 툴바</b>(appbar) -> 그것도 예약 영역이므로 정확히 함께 잡힌다.
        ///   · 멀티 모니터                              -> 우리 창이 실제로 놓인 모니터 기준.
        ///
        /// ============================================================================
        /// ★ 단위 — 여기가 macOS와 유일하게 갈라지는 지점이다
        /// ============================================================================
        /// 이 인터페이스의 계약 단위는 <b>논리 포인트</b>(= 소비 측이
        /// <c>ScreenCoordinateConverter.CanvasToUnityScreen</c>으로 Unity 픽셀로 되돌리는 그 단위)다.
        ///   · macOS: <c>visibleFrame</c>이 애초에 AppKit 포인트라 변환이 필요 없다.
        ///   · Windows: <c>rcMonitor</c>/<c>rcWork</c>는 <b>물리 픽셀</b>이고 <c>Screen.width</c>도 물리
        ///     픽셀이라(이 파일 <see cref="CaptureUiDensity"/> 문서의 단위 비교표) 표시 배율 150%에서
        ///     그대로 넘기면 <b>1.5배 두꺼운 인셋</b>이 되어 팝오버가 이유 없이 아래로 밀린다.
        /// 그래서 <b>중립 변환기 한 곳</b>(<c>UnityScreenToCanvas</c>)을 통과시킨다 — 여기서 96으로
        /// 나누는 산술을 새로 쓰지 않는다(두 벌이 되면 반드시 한쪽만 고쳐진다).
        ///
        /// <para><b>알려진 한계</b>: <c>config</c>를 null로 넘긴다. 이 서비스는 <c>StickConfig</c>를 들고
        /// 있지 않기 때문이다(<c>new Win32WindowService()</c>). 사람이 <c>desktopDpiScale</c> 수동
        /// 오버라이드를 <b>0이 아닌 값</b>으로 지정한 경우에만 소비 측 배율과 어긋난다. 기본값 0에서는
        /// 자동 밀도(<c>AutoUiDensityScale</c>)를 쓰므로 소비 측과 완전히 같은 값이다.</para>
        ///
        /// <para><b>macOS에 있는 "상식 범위 클램프"(화면 높이의 25%)를 여기에는 두지 않는다.</b>
        /// 그 클램프는 macOS가 <b>서로 다른 두 API</b>(CGDisplayBounds + GetMonitorRect)를 빼서 값을
        /// 만들기 때문에 생기는 <b>유도 오차</b>를 막는 장치다. 여기는 <b>같은 구조체 안의 두 필드</b>라
        /// 그 오차가 발생할 경로 자체가 없고, 반대로 Windows에는 두꺼운 상단 도킹 툴바가 실제로 존재해
        /// 클램프를 켜면 <b>진짜 예약 띠를 0으로 지워 그 위를 덮게 된다</b>(원칙 2 위반 방향).</para>
        /// </summary>
        public bool TryGetReservedTopInsetPoints(out float insetPoints)
        {
            insetPoints = 0f;
            if (_overlayHwnd == IntPtr.Zero) return false;

            IntPtr monitor = MonitorFromWindow(_overlayHwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return false;

            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref mi)) return false;

            float thicknessPx = mi.rcWork.Top - mi.rcMonitor.Top;
            // rcWork는 정의상 rcMonitor 안에 있다. 0 이하 = 상단에 예약된 띠가 없다는 확정 신호다.
            if (thicknessPx <= 0f) return false;

            float points = ScreenCoordinateConverter.UnityScreenToCanvas(thicknessPx, null);
            if (float.IsNaN(points) || float.IsInfinity(points) || points <= 0f) return false;

            insetPoints = points;

            if (float.IsNaN(_lastLoggedTopInsetPoints)
                || Mathf.Abs(points - _lastLoggedTopInsetPoints) > 0.5f)
            {
                _lastLoggedTopInsetPoints = points;
                Debug.Log($"[Win32WindowService] 상단 예약 띠 실측 — 두께 {thicknessPx:F0}물리px " +
                    $"= {points:F1}논리pt (rcMonitor.Top={mi.rcMonitor.Top}, rcWork.Top={mi.rcWork.Top}). " +
                    "작업표시줄/툴바가 화면 위쪽에 도킹돼 있습니다 — 팝오버·정보창·설정창·톱니가 이 띠를 피합니다.");
            }
            return true;
        }

        #endregion

        // ============================================================================
        // 전체화면 자동 숨김(절대 불변 원칙 2) — macOS와 같은 뼈대
        // ============================================================================
        // 구조를 MacWindowService.IsFullscreenAppActive()와 **의도적으로 1:1로** 맞춘다:
        //   (1) EvaluateFullscreen(out reason) — 이번 폴링의 원시 판정 + 사람이 읽는 사유
        //   (2) FullscreenVerdictDebouncer     — 깜빡임(flapping) 흡수. 규칙은 두 플랫폼 공용
        //                                        Platform/FullscreenSuspendPolicy.cs 한 곳에만 있다.
        //   (3) 판정이 바뀔 때만 로그          — 24시간 상주 앱이라 매 폴링 로그는 금지
        // 한쪽만 고치면 다른 쪽에서 같은 버그가 그대로 살아남는다(이 프로젝트가 VisibleTopEdgeSolver /
        // WindowsFootholdFilter에서 이미 두 번 겪은 실패라, 2026-09-01 패리티 감사에서 맞췄다).
        //
        // ============================================================================
        // ★★ 2026-09-02 정정 — "1:1"은 위 **뼈대 3단계**에만 해당한다. 기하 판정은 갈라져 있다.
        // ============================================================================
        // 같은 날 macOS 쪽이 두 곳에서 넓어졌고, Windows는 **의도적으로 따라가지 않았다**.
        // 이 줄이 없으면 다음 사람이 "1:1"이라는 문구를 믿고 갭으로 오해한다.
        //
        //  (A) macOS: 투명 보조 창 알파 거부권(kCGWindowAlpha < 0.05면 건너뜀).
        //      Windows에는 **해당 코드 경로 자체가 없다** — 여기는 창 목록을 훑지 않고
        //      GetForegroundWindow() 단일 조회다. 전경 창은 정의상 알파 0짜리 보조 창이 아니다.
        //      즉 이건 갭이 아니라 "그 문제가 존재하지 않음"이다.
        //
        //  (B) macOS: 상단 시스템 스트립(메뉴바/노치) 만큼의 여백을 허용하는
        //      FullscreenGeometry.CoversDisplay(). Windows는 관용 없는
        //      FullscreenGeometry.MatchesExactly()와 **같은 의미**의 정수 비교를 계속 쓴다.
        //      근거: Windows에는 "OS가 화면 상단에 항상 남겨두는 띠"라는 개념이 없고, 오히려 상단
        //      도킹 작업표시줄이 흔하다. 여백 허용을 그대로 켜면 상단 작업표시줄 환경에서 **최대화한
        //      업무 창이 전부 전체화면 게임으로 오판**된다 — macOS 쪽이 "하단 밀착" 조건으로 방금
        //      피한 것과 정확히 같은 사고이며, 원칙 2의 반대편을 깨는 방향이다.
        //      실기 검증이 불가능한 이 환경에서 관용을 켜지 않는다. 이 분기는 **결정**이지 갭이 아니다.
        //      (해당 정책과 그 반증 기록은 Platform/FullscreenSuspendPolicy.cs의
        //       FullscreenGeometry 클래스 문서에 함께 적혀 있다.)

        /// <summary>디바운스 이후의 확정 판정 — "바뀔 때만 로그"용 상태.</summary>
        private bool _lastFullscreenVerdict;

        /// <summary>디바운스 이전의 원시 판정 — "흔들렸지만 흡수했다"를 한 번만 로그로 남기기 위한 상태.</summary>
        private bool _lastRawFullscreenVerdict;

        /// <summary>바뀐 원시 판정이 이만큼 연속 유지되어야 확정한다(macOS와 같은 값·같은 근거).
        /// Windows에서 이 값이 필요한 이유는 macOS의 메뉴바 호출과 정확히 같은 계열이다: 작업표시줄
        /// 자동 숨김이 켜져 있으면 전경 창이 표시/숨김 순간마다 rcMonitor와 정확히 일치했다 아니었다를
        /// 오가고, 게임의 해상도 전환/알트탭 순간에도 몇 프레임 동안 창 사각형이 중간값을 지난다.
        /// 그대로 두면 Resume/Suspend가 반복돼 캐릭터가 깜빡이고 프레임 등급도 요동친다.</summary>
        private const double FullscreenVerdictHoldSeconds = 1.0;

        private FullscreenVerdictDebouncer _fullscreenDebouncer;

        /// <summary>"전경 프로세스가 게임인가"라는 사실만 조회하는 Windows 전용 계층
        /// (macOS의 QueryAppCategory 자리). 판정 규칙은 이 안에 없다 —
        /// 플랫폼 공용 <see cref="WindowsGameExecutablePolicy"/>가 갖고 있다.</summary>
        private readonly WindowsGameProcessProbe _gameProbe = new WindowsGameProcessProbe();

        public bool IsFullscreenAppActive()
        {
            bool raw = EvaluateFullscreen(out string reason);
            bool verdict = _fullscreenDebouncer.Update(raw, Time.realtimeSinceStartupAsDouble, FullscreenVerdictHoldSeconds);

            if (raw != _lastRawFullscreenVerdict)
            {
                _lastRawFullscreenVerdict = raw;
                if (raw != verdict)
                {
                    Debug.Log($"[전체화면판정] 원시 판정이 {raw}로 흔들렸지만 {FullscreenVerdictHoldSeconds:F1}초 연속 " +
                        $"유지되기 전이라 확정하지 않습니다(작업표시줄 자동 숨김/알트탭 등에 의한 깜빡임 흡수) — {reason}");
                }
            }

            if (verdict != _lastFullscreenVerdict)
            {
                _lastFullscreenVerdict = verdict;
                Debug.Log($"[전체화면판정] {(verdict ? "전체화면 앱 감지 -> 캐릭터를 숨깁니다" : "전체화면 해제 -> 캐릭터를 되돌립니다")} — {reason}");
            }

            return verdict;
        }

        /// <summary>
        /// 이번 폴링의 원시 전체화면 판정(디바운스 이전). 네이티브 조회는 전부 여기 모여 있고,
        /// <b>판정 규칙</b>은 위 디바운서와 마찬가지로 플랫폼 공용 파일에 있다.
        ///
        /// 조건은 macOS와 같은 두 겹이다(2026-09-01 패리티 라운드에서 맞춤):
        ///   (1) 기하 — 전경 창 사각형이 그 창이 놓인 모니터 사각형과 정확히 일치할 것.
        ///   (2) <b>게임</b> — 그 창을 소유한 프로세스의 실행 파일이 게임으로 등록돼 있을 것.
        ///       macOS는 <c>LSApplicationCategoryType</c>, Windows는 게임 바가 관리하는
        ///       <c>HKCU\System\GameConfigStore</c> 목록을 <b>읽기 전용</b>으로 인용한다.
        ///
        /// <para>(2)가 없던 동안(~2026-09-01) Windows에서는 전체화면 Excel/PowerPoint/브라우저에서도
        /// 캐릭터가 사라졌다 — 사용자가 직접 신고한 바로 그 버그다. 원칙 2의 문구는 "전체화면
        /// <b>게임</b>"이고, 조회에 실패하면 <b>게임이 아닌 것으로</b> 처리해 숨기지 않는다.</para>
        /// </summary>
        private bool EvaluateFullscreen(out string reason)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero)
            {
                reason = "전경 창이 없음(GetForegroundWindow == 0) — 전체화면 아님으로 안전 처리.";
                return false;
            }
            if (fg == _overlayHwnd)
            {
                reason = "전경 창이 우리 오버레이 자신이라 '다른 전체화면 앱'이 아님.";
                return false;
            }

            // 우리 프로세스의 다른 창(있다면)도 "다른 앱"이 아니다 — 오버레이가 전경일 때
            // 자기 자신을 전체화면 게임으로 오인해 스스로 숨는 사고를 막는다.
            GetWindowThreadProcessId(fg, out uint fgPid);
            if (fgPid == _currentProcessId)
            {
                reason = $"전경 창이 우리 프로세스(pid {fgPid})의 다른 창이라 '다른 전체화면 앱'이 아님.";
                return false;
            }

            // 여기만은 의도적으로 GetWindowRect를 유지한다(DWMWA_EXTENDED_FRAME_BOUNDS를 쓰지 않는다).
            // 아래 판정이 "모니터 사각형과 정확히 일치"이고, 확장 프레임은 창 사각형보다 **작거나 같다**.
            // 즉 확장 프레임으로 바꾸면 전체화면인데도 몇 px 작게 나와 판정이 실패하는 방향
            // (= 전체화면 게임 위에 오버레이가 그대로 남는 비침해 원칙 2 위반)으로만 틀릴 수 있다.
            // 실기 검증이 불가능한 이 환경에서 안전한 쪽으로만 틀리게 두는 것이 맞다.
            if (!GetWindowRect(fg, out var winRect))
            {
                reason = $"전경 창(pid {fgPid})의 사각형을 읽지 못함 — 전체화면 아님으로 처리.";
                return false;
            }

            IntPtr monitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                reason = $"전경 창(pid {fgPid})이 놓인 모니터 정보를 읽지 못함 — 전체화면 아님으로 처리.";
                return false;
            }

            // 단순 휴리스틱: 전경 창 사각형이 모니터 전체 사각형과 정확히 일치하면 전체화면으로 간주.
            // 보더리스 윈도우/독점 전체화면 구분, 다중 모니터 경계 케이스 등은 후속 과제.
            bool match = winRect.Left == monitorInfo.rcMonitor.Left
                && winRect.Top == monitorInfo.rcMonitor.Top
                && winRect.Right == monitorInfo.rcMonitor.Right
                && winRect.Bottom == monitorInfo.rcMonitor.Bottom;

            string geometry = $"판정 근거 창 = pid {fgPid}, 창=({winRect.Left},{winRect.Top} " +
                $"{winRect.Right - winRect.Left}x{winRect.Bottom - winRect.Top}), 모니터=" +
                $"({monitorInfo.rcMonitor.Left},{monitorInfo.rcMonitor.Top} " +
                $"{monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left}x" +
                $"{monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top})";

            if (!match)
            {
                reason = geometry + " -> 기하 일치=false.";
                return false;
            }

            // ★ 2026-09-01 — 기하만으로는 부족하다. 여기서 "그 앱이 게임인가"까지 확인한다
            //   (사용자 신고: 전체화면 엑셀을 클릭하면 캐릭터가 사라진다). 조회 실패는 전부
            //   "게임 아님"으로 떨어져 숨기지 않는다 — macOS의 카테고리 미선언 처리와 같은 계약.
            bool isGame = _gameProbe.IsGameProcess(fgPid, out string gameReason);

            reason = geometry + $" -> 기하 일치=true, {gameReason}" +
                (isGame ? "." : " (게임이 아니므로 숨기지 않습니다 — 원칙 2는 '전체화면 게임'만 대상).");
            return isGame;
        }

        // IDesktopIconLayoutService(UX_FLOW.md 27-2/27-5절) — 정직한 미구현 스텁. 실제 구현은
        // Progman → SHELLDLL_DefView → SysListView32 창에 LVM_GETITEMCOUNT/LVM_GETITEMPOSITION을
        // 보내야 하는데, 그 결과는 대상(탐색기) 프로세스 메모리에 있는 구조체를 가리키므로
        // VirtualAllocEx/WriteProcessMemory/ReadProcessMemory 기반 크로스 프로세스 IPC가 추가로
        // 필요하다 — 이 파일의 나머지 P/Invoke(자기 프로세스 메모리만 다루는 EnumWindows/GetWindowRect류)
        // 보다 훨씬 복잡하고, 이 개발 환경에는 검증할 실제 Windows 하드웨어가 없다(Unity 배치모드는
        // macOS에서 실행 — Tasklist.md 교차 레이어 로그 참고). 검증 불가능한 크로스 프로세스 코드를
        // 작성해 배포하는 대신, 정직하게 false/빈 목록을 반환한다 — 그 결과 Windows 실빌드에서 청소부/
        // 블랙홀은 안전하게 "아이콘 조회 실패로 트리거 억제"만 될 뿐 어떤 오작동도 일으키지 않는다
        // (IDesktopIconLayoutService.cs 문서 상단 "알려진 한계" 참고 — macOS도 동일하게 미구현이다).
        private static readonly List<Rect> EmptyIconRects = new List<Rect>(0);

        public bool TryGetIconRegion(out Rect osScreenRegion)
        {
            osScreenRegion = default;
            return false;
        }

        public IReadOnlyList<Rect> EnumerateIconRects() => EmptyIconRects;
    }
}
#endif
