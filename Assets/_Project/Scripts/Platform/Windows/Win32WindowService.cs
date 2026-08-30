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
    /// Windows 전용 IPlatformWindowService 구현체. Win32 P/Invoke는 프로젝트 전체에서 이 파일에만
    /// 격리한다(컨벤션 준수) — 다른 어떤 코드도 user32.dll을 직접 호출하지 않는다.
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
    ///   · 커서 좌표(GetCursorPos)                          — ICursorPositionService
    ///   · 마우스/키보드 눌림 상태(GetAsyncKeyState)        — IGlobalPointerButtonService/IGlobalKeyStateService
    ///   · 전체화면 판정(MonitorFromWindow/GetMonitorInfo)  — 비침해 원칙 2 자동 숨김
    /// 어느 것도 다른 창의 상태를 바꾸지 않으며, 입력을 주입하지도 않는다.
    /// </summary>
    public sealed class Win32WindowService :
        IPlatformWindowService,
        ICursorPositionService,
        IGlobalPointerButtonService,
        IGlobalKeyStateService,
        ILocalClickCaptureService,
        IDesktopIconLayoutService,
        IRawWindowRectSource
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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

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

        private const int DWMWA_CLOAKED = 14;

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

        // 열거 결과 버퍼. 매 호출 시 새 List를 만들지 않고 Clear 후 재사용한다.
        private readonly List<PlatformFoothold> _footholdBuffer = new List<PlatformFoothold>(64);

        // IRawWindowRectSource용 살아있는 읽기 전용 뷰 — 매 폴링마다 재생성하지 않는다(할당 0).
        private readonly ReadOnlyCollection<PlatformFoothold> _readOnlyFootholds;

        // 오버레이 원점 보고용 부기(아래 CaptureOverlayOrigin 참고).
        private bool _overlayOriginLogged;
        private float _lastLoggedDpiScale = -1f;

        public Win32WindowService()
        {
            _enumWindowsCallback = OnEnumWindow;
            _readOnlyFootholds = _footholdBuffer.AsReadOnly();
            using (var self = System.Diagnostics.Process.GetCurrentProcess())
            {
                _currentProcessId = (uint)self.Id;
            }
        }

        #region 창 열거(조회 전용)

        /// <summary>
        /// "사용자가 실제로 보고 있는 최상위 창"만 남기는 표준 필터. 여기에 걸러지지 않은 창은
        /// 그대로 발판이 되므로, 조건 하나가 빠지면 캐릭터가 보이지 않는 창 위에 서 있게 된다.
        /// </summary>
        private bool IsUsableFootholdWindow(IntPtr hWnd)
        {
            if (!IsWindowVisible(hWnd)) return false;
            if (IsIconic(hWnd)) return false;                    // 최소화 = 화면에 없음
            if (GetWindowTextLength(hWnd) == 0) return false;    // 타이틀 없는 배경 프로세스 창
            if (IsCloaked(hWnd)) return false;                   // DWM 수준에서 숨겨진 UWP 껍데기

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;  // 팔레트/툴팁류 보조 창

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == _currentProcessId) return false;           // 우리 자신의 창은 발판이 될 수 없다
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
            if (!IsUsableFootholdWindow(hWnd)) return true;
            if (!GetWindowRect(hWnd, out var rect)) return true;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return true;

            bool isTopmost = hWnd == GetForegroundWindow();
            var screenRect = new Rect(rect.Left, rect.Top, width, height);
            _footholdBuffer.Add(new PlatformFoothold(hWnd.ToInt64(), screenRect, isTopmost));
            return true; // true = 열거 계속. 다른 창을 이동/조작하는 코드는 여기 절대 추가하지 않는다.
        }

        public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
        {
            _footholdBuffer.Clear();
            EnumWindows(_enumWindowsCallback, IntPtr.Zero);
            CaptureOverlayOrigin();
            return _footholdBuffer;
        }

        /// <summary>
        /// IRawWindowRectSource — 창 도둑(UX_FLOW.md 27-1)이 쓰는 "가려짐 필터 이전" 원본 목록.
        /// 이 구현체는 macOS와 달리 애초에 가려짐(오클루전) 분할을 하지 않으므로 발판 목록이 곧
        /// 원본 목록이다(창 전체 사각형, z-order 앞->뒤). 그래서 새 컬렉션을 만들지 않고 같은 버퍼의
        /// 읽기 전용 뷰를 그대로 노출한다(계약이 요구하는 "재사용 뷰" 그대로).
        /// </summary>
        public IReadOnlyList<PlatformFoothold> RawWindows => _readOnlyFootholds;

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
            if (!GetWindowRect(_overlayHwnd, out RECT r)) return;

            int width = r.Right - r.Left;
            int height = r.Bottom - r.Top;
            if (width <= 0 || height <= 0) return;

            var osRect = new Rect(r.Left, r.Top, width, height);
            bool originMoved = Vector2.Distance(osRect.position, ScreenCoordinateConverter.OverlayOriginOsScreen) > 0.5f;
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(osRect);

            if (!_overlayOriginLogged || originMoved
                || Mathf.Abs(ScreenCoordinateConverter.AutoDpiScale - _lastLoggedDpiScale) > 0.01f)
            {
                _overlayOriginLogged = true;
                _lastLoggedDpiScale = ScreenCoordinateConverter.AutoDpiScale;
                Debug.Log($"[Win32WindowService] 오버레이 창 원점/배율 갱신 — origin={osRect.position}, " +
                    $"size=({width}x{height}), Screen=({Screen.width}x{Screen.height}) " +
                    $"-> desktopDpiScale(자동)={ScreenCoordinateConverter.AutoDpiScale:F3}.");
            }
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

        public bool IsFullscreenAppActive()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == _overlayHwnd) return false; // 우리 자신은 "다른 전체화면 앱"이 아님

            // 우리 프로세스의 다른 창(있다면)도 "다른 앱"이 아니다 — 오버레이가 전경일 때
            // 자기 자신을 전체화면 게임으로 오인해 스스로 숨는 사고를 막는다.
            GetWindowThreadProcessId(fg, out uint fgPid);
            if (fgPid == _currentProcessId) return false;

            if (!GetWindowRect(fg, out var winRect)) return false;

            IntPtr monitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo)) return false;

            // 단순 휴리스틱: 전경 창 사각형이 모니터 전체 사각형과 정확히 일치하면 전체화면으로 간주.
            // 보더리스 윈도우/독점 전체화면 구분, 다중 모니터 경계 케이스 등은 후속 과제.
            return winRect.Left == monitorInfo.rcMonitor.Left
                && winRect.Top == monitorInfo.rcMonitor.Top
                && winRect.Right == monitorInfo.rcMonitor.Right
                && winRect.Bottom == monitorInfo.rcMonitor.Bottom;
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
