#if UNITY_STANDALONE_OSX
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using Kirurobo;
using StickMate.Platform;

namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// macOS 전용 IPlatformWindowService 구현체. Win32WindowService.cs와 동일한 격리 컨벤션을 따른다 —
    /// CoreGraphics/CoreFoundation P/Invoke는 프로젝트 전체에서 이 파일에만 존재한다.
    ///
    /// 이 구현체가 절대 포함하지 않는 것은 Win32WindowService와 동일 원칙: 타 프로세스 창을
    /// 이동/크기변경/종료시키는 어떤 API도 호출하지 않는다(아키텍처 3절 유저 자산 불변 원칙). 여기서
    /// 사용하는 CoreGraphics 함수(CGWindowListCopyWindowInfo, CGEventCreate 등)는 전부 조회 전용 C ABI라
    /// 애초에 다른 창을 조작하는 부수효과가 존재하지 않는다.
    ///
    /// ============================================================================
    /// UniWindowController 도입 라운드(2026-08-28) — 진짜 투명 데스크톱 오버레이
    /// ============================================================================
    /// 이전 라운드들은 자체 제작 Objective-C 플러그인(당시 Assets/Plugins/macOS/, 이번 라운드에 삭제됨)으로
    /// NSWindow.opaque/backgroundColor/CALayer를 직접 만져 투명화를 시도했으나 여러 라운드에 걸쳐 한 번도
    /// 성공하지 못했다(창이 완전 검게 나오거나 아무 변화 없음). Unity Standalone Mac Player의 Metal 렌더
    /// 서페이스를 실제로 투명 합성시키려면 NSWindow 속성만으로는 부족하고 CAMetalLayer의 opaque 플래그와
    /// 뷰 계층 전체의 배경을 정확한 순서로 다뤄야 하는데, 그 노하우가 검증된 오픈소스가
    /// kirurobo/UniWindowController(MIT)다. 이번 라운드에서 자체 플러그인을 전부 제거하고 이 라이브러리로
    /// 교체했다(UPM: https://github.com/kirurobo/UniWindowController.git#upm, 패키지명 com.kirurobo.uniwinc).
    ///
    /// 배선 방식: UniWindowController는 씬에 배치하는 MonoBehaviour이므로(네이티브 LibUniWinC.bundle을
    /// 감싸는 래퍼), 이 서비스는 `UniWindowController.current`로 그 싱글턴 인스턴스를 찾아 프로퍼티를
    /// 세팅하는 얇은 어댑터 역할만 한다. 씬 배치 자체는 Assets/Editor/SceneBootstrapper.cs가 자동으로
    /// 수행하므로 수동 씬 편집이 필요 없다(--force 재현 가능, 기존 컨벤션).
    ///   - CreateOverlayWindow() -> UniWindowController.current 확보 + isTransparent=true 적용
    ///   - SetClickThrough(bool) -> isClickThrough + isHitTestEnabled 조합(아래 "히트테스트" 참고)
    ///   - SetAlwaysOnTop(bool)  -> isTopmost
    ///
    /// 히트테스트(isHitTestEnabled)와 안전장치의 상호작용 — 중요:
    /// UniWindowController는 isHitTestEnabled=true일 때 매 프레임 커서 아래 픽셀의 알파를 검사해
    /// isClickThrough를 자동으로 켜고 끈다(UpdateClickThrough()). 즉 "창 전체는 관통하되 캐릭터가 그려진
    /// 불투명 픽셀 위에서만 클릭을 받는" 동작을 OS 레벨로 실제 구현해준다 — docs/UX_FLOW.md 15절의
    /// "부분적 클릭관통 해제"(ILocalClickCaptureService)가 "진짜 OS 히트테스트는 불가능"이라며 미뤄뒀던
    /// 바로 그 기능이다. 다만 이 자동 제어는 StickmanAgent의 안전장치(5초 지연, Escape 강제 해제)를
    /// 다음 프레임에 그대로 덮어써 무력화할 수 있으므로, 이 어댑터는 두 값을 함께 다룬다:
    ///   - SetClickThrough(false)  -> isHitTestEnabled=false + isClickThrough=false (자동 제어까지 정지 =
    ///     Escape 긴급 해제가 실제로 "계속" 유지된다. 이게 없으면 다음 프레임에 다시 켜져 버린다.)
    ///   - SetClickThrough(true)   -> isHitTestEnabled=true  + isClickThrough=true  (이후는 라이브러리의
    ///     픽셀 히트테스트가 캐릭터 위에서만 클릭을 받도록 자동 관리)
    /// 결과적으로 "앱 시작 후 5초 동안은 어디를 클릭해도 앱이 받는다 / Escape를 누르면 즉시 그 상태로
    /// 영구 복귀"라는 기존 안전 계약이 그대로 보존된다.
    /// ============================================================================
    /// - 기존과 동일한 진짜 구현: EnumerateFootholds(), IsFullscreenAppActive(), ICursorPositionService —
    ///   CoreGraphics/CoreFoundation 공개 C ABI만 쓰는 순수 조회 동작(아래 #region 참고).
    /// - 이 클래스 자신은 여전히 "다른 프로세스의 창"에는 절대 접근하지 않는다 — UniWindowController도
    ///   자기 자신의 창(AttachMyWindow)만 다룬다.
    ///
    /// ILocalClickCaptureService/IDesktopIconLayoutService는 이번 라운드에도 의도적으로 구현하지 않는다
    /// (요청 범위 밖) — FallbackPlatformWindowService가 `as` 캐스팅으로 null 처리해 안전하게 no-op/실패
    /// 취급하므로 컴파일/런타임 모두 문제 없다(Win32WindowService가 실제로 두 인터페이스 다 구현한 것과
    /// 다른 점 — macOS는 이번 라운드에 그 두 캐퍼빌리티까지는 손대지 않는다).
    /// </summary>
    public sealed class MacWindowService : IPlatformWindowService, ICursorPositionService, IGlobalPointerButtonService
    {
        #region CoreGraphics / CoreFoundation P/Invoke 선언 (이 리전 밖으로 유출 금지)

        // 프레임워크 경로 직접 지정(dylib 캐시/서명 문제 없이 시스템 프레임워크를 안정적으로 로드하는
        // 표준 방법 — Apple도 이 절대경로를 프레임워크 "umbrella" 바이너리 경로로 문서화한다).
        private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        // CGFloat는 64비트 macOS(이 프로젝트가 타깃하는 유일한 아키텍처군, arm64/x86_64)에서 항상
        // double(8바이트)이다 — 32비트 macOS는 애초에 존재하지 않으므로 분기 불필요.
        [StructLayout(LayoutKind.Sequential)]
        private struct CGPoint { public double X; public double Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct CGSize { public double Width; public double Height; }

        [StructLayout(LayoutKind.Sequential)]
        private struct CGRect { public CGPoint Origin; public CGSize Size; }

        // CGWindowListOption 비트마스크 값. CoreGraphics.framework가 심볼로 익스포트하는 상수가 아니라
        // <CoreGraphics/CGWindow.h> 헤더에 박힌 고정 리터럴이라(과거 여러 macOS 버전에 걸쳐 안정적으로
        // 동일 값 유지) dlsym 등 심볼 조회 없이 하드코딩해도 안전하다.
        private const uint kCGWindowListOptionOnScreenOnly = 1u << 0;
        private const uint kCGWindowListExcludeDesktopElements = 1u << 4;
        private const uint kCGNullWindowID = 0;

        // CFStringEncoding/CFNumberType도 동일하게 헤더 리터럴 상수(심볼 아님).
        private const uint kCFStringEncodingUTF8 = 0x08000100;
        private const int kCFNumberSInt32Type = 3;

        [DllImport(CoreGraphicsLib)]
        private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

        [DllImport(CoreGraphicsLib)]
        private static extern CGRect CGDisplayBounds(uint display);

        // 마우스 버튼의 "현재 눌림 상태"를 창 포커스와 무관하게 조회한다(IGlobalPointerButtonService).
        // 조회 전용 공개 API이며 이벤트를 주입하지도, CGEventTap처럼 접근성 권한을 요구하지도 않는다.
        // 반환형은 C의 bool(1바이트)이므로 이 파일의 마샬링 규칙대로 I1을 명시한다(아래 "마샬링 함정" 참고).
        [DllImport(CoreGraphicsLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CGEventSourceButtonState(int stateID, uint button);

        // kCGEventSourceStateCombinedSessionState = 0 — "지금 이 로그인 세션에서 실제로 눌려 있는 상태".
        // HIDSystemState(1)는 물리 장치만 보므로 트랙패드/보조 입력 조합에서 놓칠 수 있어 세션 상태를 쓴다.
        private const int kCGEventSourceStateCombinedSessionState = 0;
        private const uint kCGMouseButtonLeft = 0;

        [DllImport(CoreGraphicsLib)]
        private static extern uint CGMainDisplayID();

        // 디스플레이 백킹 배율(Retina 배율) 조회용 3종. CGDisplayModeGetWidth는 "포인트" 폭,
        // CGDisplayModeGetPixelWidth는 실제 "백킹 픽셀" 폭을 돌려주므로 둘의 비가 곧 backingScaleFactor다
        // (Retina 2x면 3024/1512 = 2). NSScreen.backingScaleFactor를 AppKit 없이 얻는 공개 CoreGraphics
        // 경로 — 자체 네이티브 플러그인 제거 후 DetectDesktopDpiScale()의 대체 구현으로 쓴다.
        // size_t 반환이므로 64비트 macOS에서 UIntPtr(=8바이트)로 마샬링한다.
        [DllImport(CoreGraphicsLib)]
        private static extern IntPtr CGDisplayCopyDisplayMode(uint display);

        [DllImport(CoreGraphicsLib)]
        private static extern UIntPtr CGDisplayModeGetWidth(IntPtr mode);

        [DllImport(CoreGraphicsLib)]
        private static extern UIntPtr CGDisplayModeGetPixelWidth(IntPtr mode);

        [DllImport(CoreGraphicsLib)]
        private static extern void CGDisplayModeRelease(IntPtr mode);

        [DllImport(CoreGraphicsLib)]
        private static extern IntPtr CGEventCreate(IntPtr source);

        [DllImport(CoreGraphicsLib)]
        private static extern CGPoint CGEventGetLocation(IntPtr eventRef);

        // CGRectMakeWithDictionaryRepresentation: kCGWindowBounds 딕셔너리(X/Y/Width/Height 키)를 CGRect로
        // 직접 변환해주는 CoreGraphics 공식 보조 함수. X/Y/Width/Height 4개 키를 CFNumberGetValue로
        // 하나씩 손으로 파싱하는 대신 이 함수를 쓰면(같은 kCGWindowBounds 딕셔너리 포맷을 대상으로 하는
        // Apple 공식 왕복 함수 — 역함수 CGRectCreateDictionaryRepresentation과 쌍) 마샬링 실수 표면적이
        // 줄어든다(부동소수 필드 4개를 각각 개별 P/Invoke 호출로 읽는 것보다 실수 여지가 적음).
        [DllImport(CoreGraphicsLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CGRectMakeWithDictionaryRepresentation(IntPtr dict, out CGRect rect);

        [DllImport(CoreFoundationLib)]
        private static extern long CFArrayGetCount(IntPtr theArray);

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, long idx);

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

        [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
        private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

        [DllImport(CoreFoundationLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, long bufferSize, uint encoding);

        [DllImport(CoreFoundationLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFNumberGetValue(IntPtr number, int theType, out int value);

        [DllImport(CoreFoundationLib)]
        private static extern void CFRelease(IntPtr cf);

        // ============================================================================
        // 중요한 마샬링 함정(실측 이전에는 발견하기 어려움 — 반드시 문서화):
        // macOS/CoreFoundation의 Boolean/bool 반환값은 1바이트(C99 stdbool 또는 MacTypes.h의 unsigned
        // char Boolean)다. .NET P/Invoke에서 [MarshalAs(UnmanagedType.I1)]을 명시하지 않고 평범한 C#
        // bool 반환으로 선언하면 기본 마샬러가 4바이트 Win32 BOOL로 잘못 해석해 상위 3바이트의 쓰레기
        // 값까지 읽어버려 true/false 판정이 무작위로 깨질 수 있다. 이 파일의 모든 CF Boolean 반환
        // 함수(CGRectMakeWithDictionaryRepresentation/CFStringGetCString/CFNumberGetValue)에
        // [return: MarshalAs(UnmanagedType.I1)]을 명시적으로 붙인 이유다. Win32WindowService.cs의 bool
        // 반환 함수들(IsWindowVisible 등)은 반대로 이 속성이 없는 게 맞다 — Win32 BOOL은 실제로 4바이트라
        // .NET 기본 마샬링과 일치하기 때문이다. 두 플랫폼의 규칙이 다르므로 다른 파일의 패턴을 그대로
        // 복사하면 안 된다.
        // ============================================================================

        #endregion

        // kCGWindow* 딕셔너리 키는 CoreGraphics가 익스포트하는 전역 심볼(CFStringRef)이지만, dlsym으로
        // 그 심볼 자체를 조회하는 대신 동일한 리터럴 문자열 값으로 우리가 직접 CFString을 만들어 쓴다.
        // CGWindowListCopyWindowInfo가 반환하는 CFDictionary는 kCFTypeDictionaryKeyCallBacks로 생성되어
        // 키 비교가 포인터 동일성이 아니라 CFEqual(문자열 내용 비교)로 이뤄지므로, 내용만 같으면 우리가
        // 만든 별도의 CFStringRef 인스턴스로도 CFDictionaryGetValue가 정확히 매치된다(여러 언어의
        // CoreGraphics FFI 바인딩이 공통으로 쓰는 검증된 기법). 심볼 익스포트 주소를 얻으려면
        // dlopen/dlsym 및 포인터 역참조가 추가로 필요해 마샬링 표면적이 커지는데, 이 방식은 그 위험을
        // 회피한다. 프로세스 수명 동안 단 한 번만 만들고 절대 CFRelease하지 않는다(상수 취급 —
        // 24시간 상주 앱에서 이 정도 극소량의 1회성 누수는 무시 가능, 다른 Enumerate* 계열의 "버퍼
        // 재사용" 컨벤션과 같은 취지: 매 폴링마다 재생성하지 않는다).
        private readonly IntPtr _keyWindowLayer;
        private readonly IntPtr _keyWindowBounds;
        private readonly IntPtr _keyWindowOwnerPID;
        private readonly IntPtr _keyWindowOwnerName;
        private readonly IntPtr _keyWindowNumber;

        // 열거 결과 버퍼. 매 호출 시 새 List를 만들지 않고 Clear 후 재사용한다(Win32WindowService와
        // 동일한 24시간 상주 앱 컨벤션).
        private readonly List<PlatformFoothold> _footholdBuffer = new List<PlatformFoothold>(64);

        // CFStringGetCString용 재사용 버퍼(오너 이름 조회 — 자기 자신 제외 판정의 보조 신호로만 사용,
        // 아래 IsSelfWindow 참고). 창 제목 자체는 PlatformFoothold가 애초에 노출하지 않는다
        // (Win32WindowService도 GetWindowText 결과를 저장하지 않는 것과 동일한 설계).
        private readonly byte[] _ownerNameBuffer = new byte[256];

        private readonly int _currentProcessId;
        private readonly string _currentProcessName;

        // 클릭관통/항상위의 "우리가 마지막으로 의도한" 목표 상태. UniWindowController는 프로퍼티마다
        // 독립적으로 적용되므로(자체 플러그인처럼 한 함수에 두 값을 함께 넘길 필요가 없다) 이 값들은
        // 이제 상태 재적용용이 아니라 로그/진단과 CreateOverlayWindow() 재호출 시의 초기화 기준으로만
        // 쓴다. isHitTestEnabled는 클래스 문서 "히트테스트" 절에 설명한 대로 SetClickThrough()가 함께
        // 제어하므로 별도 필드를 두지 않는다(UniWindowController 인스턴스가 단일 진실 원천).
        private bool _clickThroughEnabled;
        private bool _alwaysOnTopEnabled;

        // 창 부착 이후 목표 상태를 재적용하는 런타임 전용 보조 컴포넌트(MacOverlayStateEnforcer.cs).
        private MacOverlayStateEnforcer _enforcer;

        /// <summary>
        /// 씬에 배치된 UniWindowController(Assets/Editor/SceneBootstrapper.cs가 자동 생성)를 찾는다.
        ///
        /// 왜 `UniWindowController.current` 하나로 끝나지 않는가: 그 프로퍼티가 내부적으로 쓰는
        /// FindAnyObjectByType은 기본적으로 "활성 오브젝트만" 찾는데, 이 프로젝트는 그 GameObject를
        /// 의도적으로 비활성 상태로 씬에 저장한다(SceneBootstrapper.ConfigureUniWindowController()의
        /// "매우 중요" 주석 참고 — 헤드리스 실행에서 네이티브 _findMyWindow()가 프로세스를 크래시시키기
        /// 때문). 그래서 비활성 오브젝트까지 포함해 한 번 더 찾고, activateIfInactive=true면 여기서
        /// 활성화한다. GameObject.SetActive(true)는 Awake()를 동기적으로 실행하므로, 이 호출 직후부터는
        /// `UniWindowController.current`도 정상적으로 채워진다.
        ///
        /// 인스턴스가 아예 없으면 새로 만들지 않고 null을 반환한다(라이브러리 0.9.8의
        /// FindOrCreateInstance()도 동일 정책) — 호출부가 실패를 명시적으로 처리한다(조용한 no-op 금지).
        /// </summary>
        private static UniWindowController ResolveController(bool activateIfInactive)
        {
            var controller = UniWindowController.current;
            if (controller == null)
            {
                controller = UnityEngine.Object.FindAnyObjectByType<UniWindowController>(FindObjectsInactive.Include);
            }
            if (controller == null)
            {
                return null;
            }

            if (activateIfInactive && !controller.gameObject.activeSelf)
            {
                Debug.Log("[MacWindowService] 씬의 UniWindowController가 비활성 상태 — 실제 Player에서만 " +
                    "활성화한다는 설계대로 지금 활성화합니다(SetActive(true) -> Awake() 동기 실행).");
                controller.gameObject.SetActive(true);
            }
            return controller;
        }

        /// <summary>이미 활성화된 인스턴스만 조회하는 축약형(활성화 부수효과 없음).</summary>
        private static UniWindowController Controller
        {
            get { return ResolveController(activateIfInactive: false); }
        }

        public MacWindowService()
        {
            _keyWindowLayer = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowLayer", kCFStringEncodingUTF8);
            _keyWindowBounds = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowBounds", kCFStringEncodingUTF8);
            _keyWindowOwnerPID = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowOwnerPID", kCFStringEncodingUTF8);
            _keyWindowOwnerName = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowOwnerName", kCFStringEncodingUTF8);
            _keyWindowNumber = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowNumber", kCFStringEncodingUTF8);

            using (var self = System.Diagnostics.Process.GetCurrentProcess())
            {
                _currentProcessId = self.Id;
                _currentProcessName = self.ProcessName;
            }
        }

        /// <summary>
        /// BUG-P1-R5-B3 조사 대응(Architect 실측 진단, 2026-08-28) — 이 클래스가 CoreGraphics로 읽는 실제
        /// OS 창 좌표(AppKit "포인트" 단위)와 Unity `Screen.width`/`height`/`WorldToScreenPoint`(Retina
        /// 화면에서는 실제 백킹 픽셀 단위, 포인트의 backingScaleFactor배)가 서로 다른 단위를 쓰는 문제를
        /// 보정하기 위해, 네이티브 플러그인으로 실제 화면의 `backingScaleFactor`를 조회해
        /// `Platform/ScreenCoordinateConverter.cs`가 기대하는 `StickConfig.desktopDpiScale`(그 필드
        /// 문서의 "Unity 픽셀 -> OS 픽셀 배율" 정의 그대로) 값으로 환산해 반환한다. 배율은 역수(1/backing)
        /// 다 — Unity 쪽이 OS(AppKit 포인트) 쪽보다 backingScaleFactor배 더 큰 숫자를 보고하므로, 그
        /// 값을 곱해 OS 단위로 줄이려면 1/backingScaleFactor를 곱해야 한다(예: Retina 2x면 0.5).
        /// 창을 못 찾거나 배율이 비정상(0 이하)이면 안전한 기본값 1(배율 없음)을 반환한다 — 조용히
        /// 잘못된 배율을 적용하는 것보다, "보정 없음"으로 안전하게 폴백하는 편이 기존 컨벤션과 일치한다.
        /// 호출자(StickmanAgent.CreatePlatformService())가 이 값을 `StickConfig.desktopDpiScale`에
        /// 1회 적용한다 — 씬 에셋(ScriptableObject) 파일 자체를 수정하는 것이 아니라 실행 중인 빌드의
        /// 메모리상 인스턴스 값만 갱신하므로, 다음 실행 때는 다시 이 메서드가 그 화면 기준으로 재계산한다.
        /// </summary>
        public float DetectDesktopDpiScale()
        {
            // 자체 플러그인의 SM_GetMainWindowBackingScaleFactor()(NSWindow.backingScaleFactor)를 대체하는
            // 순수 CoreGraphics 구현(UniWindowController 도입 라운드, 2026-08-28).
            //
            // 왜 UniWindowController.clientSize를 쓰지 않는가 — 실측으로 확인한 함정: 이 메서드는
            // StickmanAgent.Start()에서 호출되는데 그 시점에는 UniWindowController가 아직 자기 NSWindow를
            // 붙잡기 전이라(부착은 첫 Update()에서 일어난다) clientSize가 (0,0)으로 나온다. 실제로 처음
            // 그렇게 구현했다가 Player.log에 desktopDpiScale=1.000(= 보정 없음)이 찍히는 것을 실측으로
            // 확인했다. 그래서 창이 아니라 "디스플레이" 자체의 배율을 조회하는 방식으로 바꿨다 — 이쪽은
            // 창 부착 여부와 무관하게 항상 즉시 정확한 값을 준다.
            //
            // CGDisplayModeGetWidth = 포인트 폭, CGDisplayModeGetPixelWidth = 백킹 픽셀 폭이므로
            // backingScaleFactor = pixelWidth / pointWidth이고, 이 메서드가 반환해야 하는
            // StickConfig.desktopDpiScale("Unity 픽셀 -> OS 픽셀 배율")은 그 역수다(Retina 2x면 0.5).
            // 조회 실패/비정상 값이면 안전한 기본값 1(보정 없음)로 폴백한다.
            // ========================================================================
            // 1순위 — **직접 측정**(드래그&던지기 배선 라운드, 2026-08-28에 실측으로 교체)
            // ========================================================================
            // ScreenCoordinateConverter가 실제로 필요로 하는 값은 "디스플레이의 백킹 배율"이 아니라
            // **`Unity가 보고하는 1픽셀`이 `OS 좌표계의 몇 단위`인가**다. 그 둘은 항상 같지 않다:
            // 이 프로젝트는 ProjectSettings의 `macRetinaSupport: 0`이라 Unity가 Retina 백킹 픽셀이 아니라
            // 포인트 단위로 렌더/보고한다. 실측 로그(2026-08-28):
            //     디스플레이 backingScaleFactor = 2.000  ->  기존 식의 결과 desktopDpiScale = 0.500
            //     그러나 실제로는 Screen=(1512x846) == 우리 창 크기(1512x846 pt) == 배율 1.000
            // 즉 기존 식은 **정확히 2배 틀린 값**을 주고 있었고, 그 결과 커서 좌표(CGEventGetLocation,
            // 진짜 OS 포인트)를 월드로 되돌릴 때 좌표가 2배로 어긋났다(드래그 추종/로데오 도달 판정/
            // 실제 창 위 착지가 전부 이 오차를 공유한다).
            //
            // 그래서 우리 창의 실제 폭(kCGWindowBounds, OS 포인트)을 Screen.width(Unity 픽셀)로 나눠
            // 그 비율을 직접 측정한다. 창 열거는 UniWindowController의 부착 여부와 무관하게 Unity가
            // 자기 NSWindow를 만든 직후부터 성공하므로(실측 확인 — Start() 시점에 이미 자기 창이
            // 조회된다), 이전 라운드가 겪었던 "부착 전이라 clientSize=(0,0)" 함정에도 걸리지 않는다.
            // 겸사겸사 오버레이 원점도 여기서 즉시 반영해 첫 프레임부터 좌표 변환이 정확해진다.
            if (TryGetSelfWindowRect(out Rect selfRect) && Screen.width > 0 && selfRect.width > 0f)
            {
                ScreenCoordinateConverter.OverlayOriginOsScreen = selfRect.position;
                float measured = selfRect.width / Screen.width;
                Debug.Log($"[MacWindowService] DetectDesktopDpiScale(): 자기 창 실측 — 창={selfRect}, " +
                    $"Screen=({Screen.width}x{Screen.height}) -> desktopDpiScale={measured:F3} " +
                    "(창 폭[OS 포인트] / Screen.width[Unity 픽셀]). 오버레이 원점도 함께 반영했습니다.");
                return measured;
            }

            Debug.LogWarning("[MacWindowService] DetectDesktopDpiScale(): 자기 창을 찾지 못해 디스플레이 " +
                "백킹 배율 기반 폴백을 사용합니다 — macRetinaSupport 설정에 따라 틀릴 수 있습니다.");

            IntPtr mode = CGDisplayCopyDisplayMode(CGMainDisplayID());
            if (mode == IntPtr.Zero)
            {
                Debug.LogWarning("[MacWindowService] DetectDesktopDpiScale(): CGDisplayCopyDisplayMode 실패 — 배율 보정 없이 1을 사용합니다.");
                return 1f;
            }

            try
            {
                double pointWidth = (double)(ulong)CGDisplayModeGetWidth(mode);
                double pixelWidth = (double)(ulong)CGDisplayModeGetPixelWidth(mode);
                if (pointWidth <= 0.0 || pixelWidth <= 0.0)
                {
                    return 1f;
                }

                double backingScaleFactor = pixelWidth / pointWidth;
                if (backingScaleFactor <= 0.0)
                {
                    return 1f;
                }

                float scale = (float)(1.0 / backingScaleFactor);
                Debug.Log($"[MacWindowService] DetectDesktopDpiScale(): 디스플레이 포인트폭={pointWidth}, " +
                    $"백킹픽셀폭={pixelWidth}, backingScaleFactor={backingScaleFactor:F3} -> desktopDpiScale={scale:F3}.");
                return scale;
            }
            finally
            {
                CGDisplayModeRelease(mode);
            }
        }

        /// <summary>
        /// 화면에 보이는 창 목록을 원시(raw) CFArray로 확보해 안전하게 순회하는 공용 루틴. 호출자가
        /// try/finally로 CFRelease를 보장하도록 IntPtr을 그대로 반환한다(가공은 호출부 책임).
        /// </summary>
        private static IntPtr CopyOnScreenWindowList()
        {
            return CGWindowListCopyWindowInfo(
                kCGWindowListOptionOnScreenOnly | kCGWindowListExcludeDesktopElements,
                kCGNullWindowID);
        }

        private bool TryGetInt(IntPtr windowDict, IntPtr key, out int value)
        {
            value = 0;
            IntPtr numberRef = CFDictionaryGetValue(windowDict, key);
            if (numberRef == IntPtr.Zero) return false;
            return CFNumberGetValue(numberRef, kCFNumberSInt32Type, out value);
        }

        private string TryGetString(IntPtr windowDict, IntPtr key)
        {
            IntPtr stringRef = CFDictionaryGetValue(windowDict, key);
            if (stringRef == IntPtr.Zero) return string.Empty;
            if (!CFStringGetCString(stringRef, _ownerNameBuffer, _ownerNameBuffer.Length, kCFStringEncodingUTF8))
            {
                return string.Empty; // 버퍼보다 긴 문자열 등 — 자기자신 판정용 보조 신호일 뿐이라 조용히 포기.
            }
            int len = Array.IndexOf(_ownerNameBuffer, (byte)0);
            if (len < 0) len = _ownerNameBuffer.Length;
            return System.Text.Encoding.UTF8.GetString(_ownerNameBuffer, 0, len);
        }

        /// <summary>
        /// 이 창이 우리 자신(Unity 플레이어 프로세스)의 창인지 판정한다. PID 비교가 1차 근거(정확한
        /// 식별자 — 이름은 배포/로컬라이즈에 따라 달라질 수 있음)이고, kCGWindowOwnerName 문자열 비교를
        /// 보조 신호로 추가한다(작업 지시가 명시한 신호를 그대로 함께 반영 — PID 조회가 어떤 이유로
        /// 실패해도(이론상 발생 안 함, CGWindowListCopyWindowInfo가 항상 채워주는 필수 키) 이름 비교로
        /// 안전망 역할).
        /// </summary>
        private bool IsSelfWindow(IntPtr windowDict)
        {
            if (TryGetInt(windowDict, _keyWindowOwnerPID, out int ownerPid) && ownerPid == _currentProcessId)
            {
                return true;
            }
            string ownerName = TryGetString(windowDict, _keyWindowOwnerName);
            return !string.IsNullOrEmpty(ownerName) && ownerName == _currentProcessName;
        }

        public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
        {
            _footholdBuffer.Clear();
            _overlayOriginPassArea = 0.0; // CaptureOverlayOrigin()의 "이번 패스 최대 면적" 리셋.

            IntPtr windowArray = CopyOnScreenWindowList();
            if (windowArray == IntPtr.Zero) return _footholdBuffer; // 조회 실패 — FallbackPlatformWindowService 안전망이 감싸므로 빈 리스트로도 안전.

            try
            {
                long count = CFArrayGetCount(windowArray);
                for (long i = 0; i < count; i++)
                {
                    IntPtr windowDict = CFArrayGetValueAtIndex(windowArray, i);
                    if (windowDict == IntPtr.Zero) continue;

                    // 이 앱 자신(Unity 플레이어 프로세스)의 창은 발판 후보에서 제외한다. 순서 주의:
                    // 아래 레이어 필터보다 **먼저** 판정한다 — 우리 창은 항상위(kCGWindowLayer=101)라
                    // 레이어 필터에 먼저 걸리면 여기까지 오지 못하고, 그러면 바로 아래의 오버레이 원점
                    // 캡처가 영원히 실행되지 않는다(발판 목록에서 제외된다는 결과 자체는 순서와 무관하게 동일).
                    if (IsSelfWindow(windowDict))
                    {
                        CaptureOverlayOrigin(windowDict);
                        continue;
                    }

                    // 일반 앱 창(kCGWindowLayer==0)만 채택 — 메뉴바/데스크톱 배경 등 시스템 레이어 제외.
                    // Win32의 "제목 있는 가시 창"(GetWindowTextLength!=0) 필터와 같은 목적의 휴리스틱.
                    if (!TryGetInt(windowDict, _keyWindowLayer, out int layer) || layer != 0) continue;

                    IntPtr boundsDict = CFDictionaryGetValue(windowDict, _keyWindowBounds);
                    if (boundsDict == IntPtr.Zero) continue;
                    if (!CGRectMakeWithDictionaryRepresentation(boundsDict, out CGRect rect)) continue;

                    long handle = 0;
                    if (TryGetInt(windowDict, _keyWindowNumber, out int windowNumber)) handle = windowNumber;

                    // CGWindowListCopyWindowInfo(kCGWindowListOptionOnScreenOnly)는 z-order 기준
                    // 앞->뒤 순서로 보장되어 반환된다(Apple 문서화된 동작) — 필터 통과 후 이 버퍼에
                    // 처음 추가되는 항목이 곧 "현재 화면에서 가장 앞에 있는(눈에 보이는) 일반 앱 창"이다.
                    // Win32의 `hWnd == GetForegroundWindow()` 판정과 동일한 의도(포커스를 가진
                    // 최상단 창)를 z-order로 근사한다.
                    bool isTopmost = _footholdBuffer.Count == 0;

                    var screenRect = new Rect((float)rect.Origin.X, (float)rect.Origin.Y, (float)rect.Size.Width, (float)rect.Size.Height);
                    _footholdBuffer.Add(new PlatformFoothold(handle, screenRect, isTopmost));
                }
            }
            finally
            {
                CFRelease(windowArray);
            }

            return _footholdBuffer;
        }

        // ============================================================================
        // 오버레이 배선 3종 — UniWindowController(com.kirurobo.uniwinc) 어댑터.
        // 자체 Objective-C 플러그인은 이 라운드에서 완전히 제거됐다(클래스 문서 상단 참고).
        // ============================================================================

        /// <summary>
        /// 씬의 UniWindowController를 확보하고 "진짜 투명 오버레이" 초기 상태를 적용한다.
        ///   isTransparent=true  — 이번 라운드의 핵심 목표(회색 창이 아니라 바탕화면 위에 직접 표시).
        ///   isClickThrough=false / isHitTestEnabled=false — 클릭관통은 반드시 꺼진 채로 시작한다.
        ///     StickmanAgent.Start()의 5초 지연 안전장치와 이중으로 겹쳐, 시작 직후에는 사용자가 어디를
        ///     클릭해도 앱이 입력을 받는다(창을 되돌릴 수단 상실 방지).
        ///   isTopmost=false — SetAlwaysOnTop()이 이후 명시적으로 켠다.
        /// 인스턴스를 찾지 못하면 false를 반환할 뿐 예외를 던지지 않는다 — 기존 컨벤션(BUG-P1-M3,
        /// StickmanAgent.Start()가 경고만 남기고 계속 진행)을 그대로 유지한다.
        ///
        /// 주의(공식 문서 경고): 투명은 Unity 에디터에서는 동작하지 않는다 — 반드시 Standalone 빌드로
        /// 검증해야 한다. 실제로 UniWindowController.SetTransparent()의 네이티브 호출부는 `#if
        /// !UNITY_EDITOR`로 감싸여 있다.
        /// </summary>
        public bool CreateOverlayWindow()
        {
            var controller = ResolveController(activateIfInactive: true);
            if (controller == null)
            {
                Debug.LogWarning("[MacWindowService] CreateOverlayWindow(): 씬에서 UniWindowController를 " +
                    "찾지 못했습니다(UniWindowController.current == null). SceneBootstrapper가 프리팹을 " +
                    "배치했는지 확인하세요 — 이후 SetClickThrough/SetAlwaysOnTop 호출이 모두 실패합니다.");
                return false;
            }

            _clickThroughEnabled = false;
            _alwaysOnTopEnabled = false;

            // 순서 중요: 히트테스트 자동 제어를 먼저 끈 뒤에 클릭관통을 끈다. 반대로 하면 투명화 직후
            // 프레임에 자동 제어가 클릭관통을 다시 켜버릴 수 있다.
            controller.isHitTestEnabled = false;
            controller.isClickThrough = false;
            controller.isTopmost = false;
            controller.isTransparent = true;

            // 창 부착 타이밍 문제 보정(MacOverlayStateEnforcer 클래스 문서 참고) — UniWindowController는
            // 첫 Update()에서야 자기 NSWindow를 붙잡으므로, Start() 시점의 설정 중 항상위/클릭관통은
            // 조용히 되돌아간다. 목표 상태를 들고 있다가 부착 확인 후 재적용하는 보조 컴포넌트를 띄운다.
            _enforcer = MacOverlayStateEnforcer.EnsureExists(controller);
            _enforcer.DesiredTransparent = true;
            _enforcer.DesiredTopmost = false;
            _enforcer.DesiredClickThrough = false;
            _enforcer.DesiredHitTest = false;
            _enforcer.MarkDirty();

            Debug.Log("[MacWindowService] CreateOverlayWindow(): UniWindowController 확보 및 초기 상태 적용 완료 " +
                $"(isTransparent={controller.isTransparent}, isClickThrough={controller.isClickThrough}, " +
                $"isTopmost={controller.isTopmost}, isHitTestEnabled={controller.isHitTestEnabled}, " +
                $"hitTestType={controller.hitTestType}, opacityThreshold={controller.opacityThreshold}, " +
                $"clientSize={controller.clientSize}, windowPosition={controller.windowPosition}).");
            return true;
        }

        /// <summary>
        /// 클릭관통 on/off. 클래스 문서 "히트테스트" 절에 설명한 대로 isClickThrough 하나만 건드리면
        /// UniWindowController의 매 프레임 자동 제어(UpdateClickThrough)가 다음 프레임에 그대로 덮어써
        /// 버리므로, isHitTestEnabled도 함께 제어해 안전장치가 실제로 유지되게 한다.
        ///   enabled=false(Escape 긴급 해제, 시작 후 5초 구간) -> 자동 제어 정지 + 클릭관통 해제
        ///   enabled=true(정상 오버레이 동작)                  -> 클릭관통 ON + 픽셀 알파 기반 자동
        ///     히트테스트 ON(= 캐릭터가 그려진 불투명 픽셀 위에서만 클릭을 받는 부분적 관통 해제)
        /// 인스턴스가 없으면 조용히 무시하지 않고 NotSupportedException으로 즉시 실패를 알린다(이전
        /// 라운드들과 동일한 컨벤션 — StickmanAgent가 이 예외를 잡아 로그로 남기고 계속 진행한다).
        /// </summary>
        public void SetClickThrough(bool enabled)
        {
            var controller = Controller;
            if (controller == null)
            {
                throw new NotSupportedException(
                    "MacWindowService.SetClickThrough(): 씬에서 UniWindowController를 찾지 못해 클릭관통을 " +
                    "적용할 수 없습니다(UniWindowController.current == null). SceneBootstrapper가 프리팹을 " +
                    "배치했는지, CreateOverlayWindow()가 먼저 호출되었는지 확인하세요.");
            }

            _clickThroughEnabled = enabled;
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

            Debug.Log($"[MacWindowService] SetClickThrough({enabled}) 적용 완료 — " +
                $"isClickThrough={controller.isClickThrough}, isHitTestEnabled={controller.isHitTestEnabled}, " +
                $"isTransparent={controller.isTransparent}.");
        }

        /// <summary>SetClickThrough와 동일한 실패 정책 — UniWindowController.isTopmost를 통해 실제
        /// NSWindow.level(NSFloatingWindowLevel)을 설정한다.</summary>
        public void SetAlwaysOnTop(bool enabled)
        {
            var controller = Controller;
            if (controller == null)
            {
                throw new NotSupportedException(
                    "MacWindowService.SetAlwaysOnTop(): 씬에서 UniWindowController를 찾지 못해 항상위 " +
                    "설정을 적용할 수 없습니다(UniWindowController.current == null). SceneBootstrapper가 " +
                    "프리팹을 배치했는지, CreateOverlayWindow()가 먼저 호출되었는지 확인하세요.");
            }

            _alwaysOnTopEnabled = enabled;
            controller.isTopmost = enabled;

            if (_enforcer != null)
            {
                _enforcer.DesiredTopmost = enabled;
                _enforcer.MarkDirty();
            }

            // 되읽은 값이 목표와 다른 것은 "실패"가 아니라 "아직 창 부착 전"이라는 뜻이다 — 그 경우
            // MacOverlayStateEnforcer가 부착 직후 재적용한다(그 클래스 문서의 실측 사고 기록 참고).
            Debug.Log($"[MacWindowService] SetAlwaysOnTop({enabled}) 적용 완료 — isTopmost={controller.isTopmost}" +
                (controller.isTopmost != enabled ? " (아직 창 부착 전 — Enforcer가 재적용 예정)" : "") + ".");
        }

        /// <summary>
        /// Win32의 "전경창 사각형 == 모니터 전체 사각형" 휴리스틱과 동일한 아이디어를 macOS로 이식.
        /// kCGWindowListOptionOnScreenOnly 결과에서 (우리 자신을 제외한) 가장 앞선 일반 앱 창의 bounds가
        /// 메인 디스플레이 전체 영역과 일치하면 전체화면 앱으로 간주한다.
        /// </summary>
        public bool IsFullscreenAppActive()
        {
            IntPtr windowArray = CopyOnScreenWindowList();
            if (windowArray == IntPtr.Zero) return false;

            try
            {
                long count = CFArrayGetCount(windowArray);
                for (long i = 0; i < count; i++)
                {
                    IntPtr windowDict = CFArrayGetValueAtIndex(windowArray, i);
                    if (windowDict == IntPtr.Zero) continue;
                    if (!TryGetInt(windowDict, _keyWindowLayer, out int layer) || layer != 0) continue;

                    // 최상단 일반 창이 우리 자신이면 "다른 전체화면 앱"이 아니다(Win32의
                    // fg == _overlayHwnd 처리와 동일 의도) — 더 탐색하지 않고 즉시 false.
                    if (IsSelfWindow(windowDict)) return false;

                    IntPtr boundsDict = CFDictionaryGetValue(windowDict, _keyWindowBounds);
                    if (boundsDict == IntPtr.Zero) return false;
                    if (!CGRectMakeWithDictionaryRepresentation(boundsDict, out CGRect winRect)) return false;

                    CGRect displayBounds = CGDisplayBounds(CGMainDisplayID());
                    const double epsilon = 0.5; // 부동소수/서브픽셀 오차 허용치.
                    return Math.Abs(winRect.Origin.X - displayBounds.Origin.X) < epsilon
                        && Math.Abs(winRect.Origin.Y - displayBounds.Origin.Y) < epsilon
                        && Math.Abs(winRect.Size.Width - displayBounds.Size.Width) < epsilon
                        && Math.Abs(winRect.Size.Height - displayBounds.Size.Height) < epsilon;
                }
                return false; // 일반 레이어 창이 하나도 없음(전부 최소화 등) — 전체화면 아님으로 안전 처리.
            }
            finally
            {
                CFRelease(windowArray);
            }
        }

        // ============================================================================
        // ICursorPositionService — SetClickThrough와 완전히 무관한 별도 CoreGraphics API
        // (ICursorPositionService.cs의 설계 의도와 동일: 클릭관통 on/off와 커서 조회는 서로 영향 없음).
        // ============================================================================

        /// <summary>
        /// CGEventCreate(NULL)로 "현재 이벤트 상태"를 스냅샷해 CGEventGetLocation으로 커서 좌표를 읽는다
        /// (Apple 문서화된 전역 커서 조회 관용구). 클릭 등 어떤 입력도 주입하지 않는 순수 조회이며,
        /// CGEventRef도 CFTypeRef이므로 사용 후 CFRelease 필요.
        ///
        /// 좌표계 참고(중요): CGEventGetLocation/CGWindowListCopyWindowInfo/CGDisplayBounds는 전부 같은
        /// "Quartz 디스플레이 좌표계"(메인 디스플레이 좌상단 원점, y 아래로 증가)를 쓴다 — 이는
        /// PlatformFoothold.ScreenRect가 문서화한 좌표계(IPlatformWindowService.cs, Win32
        /// GetWindowRect와 동일 규약)와 이미 정확히 일치한다. NSWindow/NSScreen 같은 AppKit
        /// 좌표계(좌하단 원점, y 위로 증가)와는 다른 이야기라, 만약 이 프로젝트가 나중에 Cocoa
        /// NSWindow API를 직접 다루는 네이티브 플러그인을 추가하게 되면 그쪽에서는 별도의 y축 반전이
        /// 필요하다 — 하지만 이 파일은 CoreGraphics C ABI만 쓰므로 여기서는 추가 변환이 전혀 필요
        /// 없다(오히려 여기서 y를 뒤집으면 좌표계를 이중으로 반전시키는 버그가 된다).
        /// </summary>
        public bool TryGetGlobalCursorPosition(out Vector2 osScreenPosition)
        {
            IntPtr eventRef = CGEventCreate(IntPtr.Zero);
            if (eventRef == IntPtr.Zero)
            {
                osScreenPosition = Vector2.zero;
                return false;
            }

            try
            {
                CGPoint point = CGEventGetLocation(eventRef);
                osScreenPosition = new Vector2((float)point.X, (float)point.Y);
                return true;
            }
            finally
            {
                CFRelease(eventRef);
            }
        }

        // ============================================================================
        // IGlobalPointerButtonService — 창 포커스와 무관한 "왼쪽 버튼 눌림" 조회
        // (Platform/IGlobalPointerButtonService.cs 문서의 "왜 OnMouseDown만으로는 부족한가" 참고)
        // ============================================================================

        /// <summary>
        /// CGEventSourceButtonState로 지금 왼쪽 버튼이 눌려 있는지 조회한다. TryGetGlobalCursorPosition과
        /// 마찬가지로 순수 조회이며 어떤 이벤트도 주입하지 않는다. CoreGraphics 호출 자체가 실패하는
        /// 경우는 없으므로 항상 true를 반환한다(인터페이스가 false를 허용하는 것은 "이 플랫폼은 아예
        /// 지원 안 함"을 표현하기 위한 것으로, macOS에서는 해당 없음).
        /// </summary>
        public bool TryGetPrimaryButtonPressed(out bool pressed)
        {
            pressed = CGEventSourceButtonState(kCGEventSourceStateCombinedSessionState, kCGMouseButtonLeft);
            return true;
        }

        /// <summary>
        /// 우리 오버레이 창의 화면상 좌상단을 Platform/ScreenCoordinateConverter.OverlayOriginOsScreen에
        /// 반영한다(그 프로퍼티 문서의 "왜 필요한가" 참고 — 창이 화면 좌상단에서 시작하지 않아 커서↔월드
        /// 변환이 통째로 틀어지던 문제).
        ///
        /// 여기서 읽는 kCGWindowBounds는 CGEventGetLocation(커서 좌표)과 **정확히 같은 Quartz 전역
        /// 디스플레이 좌표계**(좌상단 원점, y 아래로 증가)라 별도 변환이 전혀 필요 없다 —
        /// UniWindowController.windowPosition을 쓰지 않는 이유가 이것이다(그쪽은 라이브러리 내부의
        /// 다른 좌표 규약을 따르므로 커서 좌표와 직접 비교할 수 없다).
        ///
        /// 한 프로세스가 여러 창을 가질 수 있으므로(상태 표시용 보조 창 등) **면적이 가장 큰 창**을
        /// 오버레이 본체로 본다. EnumerateFootholds()가 이미 도는 열거 루프 안에서 호출되므로 추가
        /// 시스템 호출이 0건이고, 창이 움직이거나 Dock/메뉴바 표시가 바뀌어도 폴링 주기마다 자동 추종한다.
        /// </summary>
        private void CaptureOverlayOrigin(IntPtr windowDict)
        {
            IntPtr boundsDict = CFDictionaryGetValue(windowDict, _keyWindowBounds);
            if (boundsDict == IntPtr.Zero) return;
            if (!CGRectMakeWithDictionaryRepresentation(boundsDict, out CGRect rect)) return;

            double area = rect.Size.Width * rect.Size.Height;
            if (area <= 0.0) return;

            // 같은 열거 패스 안에서 더 큰 창이 나오면 그쪽으로 교체한다. 패스가 새로 시작될 때
            // 리셋해야 하므로 EnumerateFootholds()의 프레임 카운터 대신 "이번 패스에서 본 최대 면적"을
            // _footholdBuffer.Clear()와 같은 시점에 초기화한다(아래 _overlayOriginPassArea 참고).
            if (area < _overlayOriginPassArea) return;
            _overlayOriginPassArea = area;

            var origin = new Vector2((float)rect.Origin.X, (float)rect.Origin.Y);
            if (!_overlayOriginLogged || Vector2.Distance(origin, ScreenCoordinateConverter.OverlayOriginOsScreen) > 0.5f)
            {
                _overlayOriginLogged = true;
                Debug.Log($"[MacWindowService] 오버레이 창 원점(Quartz 좌표) 갱신 — origin={origin}, " +
                    $"size=({rect.Size.Width}x{rect.Size.Height}), Screen=({Screen.width}x{Screen.height}). " +
                    "이 값이 커서<->월드 변환의 세로 오프셋 보정에 쓰입니다(ScreenCoordinateConverter.OverlayOriginOsScreen).");
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = origin;
        }

        /// <summary>
        /// 우리 프로세스가 소유한 온스크린 창 중 면적이 가장 큰 것의 화면 사각형(Quartz 좌표, OS 포인트).
        /// CaptureOverlayOrigin()과 같은 판정을 쓰지만, 이쪽은 발판 열거 루프 밖에서 단발성으로 필요할 때
        /// (DetectDesktopDpiScale) 독립적으로 한 번 조회한다.
        /// </summary>
        private bool TryGetSelfWindowRect(out Rect rect)
        {
            rect = default;
            IntPtr windowArray = CopyOnScreenWindowList();
            if (windowArray == IntPtr.Zero) return false;

            bool found = false;
            double bestArea = 0.0;
            try
            {
                long count = CFArrayGetCount(windowArray);
                for (long i = 0; i < count; i++)
                {
                    IntPtr windowDict = CFArrayGetValueAtIndex(windowArray, i);
                    if (windowDict == IntPtr.Zero) continue;
                    if (!IsSelfWindow(windowDict)) continue;

                    IntPtr boundsDict = CFDictionaryGetValue(windowDict, _keyWindowBounds);
                    if (boundsDict == IntPtr.Zero) continue;
                    if (!CGRectMakeWithDictionaryRepresentation(boundsDict, out CGRect r)) continue;

                    double area = r.Size.Width * r.Size.Height;
                    if (area <= bestArea) continue;
                    bestArea = area;
                    rect = new Rect((float)r.Origin.X, (float)r.Origin.Y, (float)r.Size.Width, (float)r.Size.Height);
                    found = true;
                }
            }
            finally
            {
                CFRelease(windowArray);
            }
            return found;
        }

        // CaptureOverlayOrigin()이 한 열거 패스 안에서 "가장 큰 자기 창"을 고르기 위한 작업 변수.
        private double _overlayOriginPassArea;
        private bool _overlayOriginLogged;
    }
}
#endif
