#if UNITY_STANDALONE_OSX
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public sealed class MacWindowService : IPlatformWindowService, ICursorPositionService, IGlobalPointerButtonService, IGlobalKeyStateService, ILocalClickCaptureService, IDockMetricsService, IRawWindowRectSource, IWindowEnumerationCostSource
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

        // 키보드 키의 "현재 눌림 상태"를 창 포커스와 무관하게 조회한다(IGlobalKeyStateService).
        // CGEventSourceButtonState와 정확히 같은 계열의 조회 전용 공개 API이며, 이벤트를 가로채는
        // CGEventTap과 달리 접근성 권한을 요구하지 않는다(2026-08-28 이 환경에서 실측 확인 —
        // Platform/IGlobalKeyStateService.cs의 "권한에 대하여" 절 참고). 반환형은 C의 bool(1바이트).
        [DllImport(CoreGraphicsLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CGEventSourceKeyState(int stateID, ushort keycode);

        // kCGEventSourceStateCombinedSessionState = 0 — "지금 이 로그인 세션에서 실제로 눌려 있는 상태".
        // HIDSystemState(1)는 물리 장치만 보므로 트랙패드/보조 입력 조합에서 놓칠 수 있어 세션 상태를 쓴다.
        private const int kCGEventSourceStateCombinedSessionState = 0;
        private const uint kCGMouseButtonLeft = 0;
        private const uint kCGMouseButtonRight = 1;

        // macOS 가상 키코드(<HIToolbox/Events.h>의 kVK_* 상수). CoreGraphics가 심볼로 익스포트하지 않는
        // 고정 리터럴이라(하드웨어 배열이 아니라 "가상" 키코드라 자판 배열/언어와 무관하게 불변)
        // 이 파일의 다른 헤더 상수들과 동일하게 하드코딩한다.
        private const ushort kVK_Command = 0x37;
        private const ushort kVK_Option = 0x3A;
        private const ushort kVK_Control = 0x3B;
        private const ushort kVK_ANSI_Q = 0x0C;
        private const ushort kVK_ANSI_C = 0x08;
        private const ushort kVK_ANSI_D = 0x02;
        private const ushort kVK_ANSI_R = 0x0F;
        private const ushort kVK_ANSI_B = 0x0B;
        private const ushort kVK_ANSI_G = 0x05;
        private const ushort kVK_ANSI_K = 0x28;
        private const ushort kVK_ANSI_T = 0x11; // 창 도둑 데모(Theft)
        private const ushort kVK_ANSI_X = 0x07; // 윈도우 크래시 데모(부서짐)
        private const ushort kVK_ANSI_H = 0x04; // 하드웨어 반응 데모 미리보기(Hardware)
        private const ushort kVK_ANSI_S = 0x01; // 스트레스 게이지 단계 순환(Stress)
        private const ushort kVK_ANSI_N = 0x2D; // 가출 발동 / 돌아오라고 부르기("Nope 나 안 해")
        private const ushort kVK_ANSI_J = 0x26; // 할일 추가 + 알림(Job)
        private const ushort kVK_ANSI_F = 0x03; // 집중 모드 켜기/끄기(Focus)
        private const ushort kVK_ANSI_A = 0x00; // 활쏘기 발동(Archery)
        private const ushort kVK_ANSI_I = 0x22; // 캐릭터 정보/장비 창(Info) — <HIToolbox/Events.h> kVK_ANSI_I = 34
        // 설정창(Preferences) — <HIToolbox/Events.h> kVK_ANSI_P = 35. 2026-09-01 쉼표에서 옮겨왔다:
        // ⌃⌥⌘,는 macOS가 접근성 "대비 줄이기"로 예약한 조합이라 우리 단축키가 사용자의 OS 설정을
        // 실제로 바꿨다(Core/ShortcutLabel.MacReservedActionKeys 참고).
        private const ushort kVK_ANSI_P = 0x23;

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

        // kCGWindowAlpha는 CFNumber(부동소수)라 위 int 오버로드로는 못 읽는다 — 같은 심볼의 double
        // 오버로드를 별도로 선언한다(P/Invoke는 시그니처별로 독립 바인딩되므로 이름 충돌이 아니다).
        [DllImport(CoreFoundationLib, EntryPoint = "CFNumberGetValue")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFNumberGetValueDouble(IntPtr number, int theType, out double value);

        // kCGWindowIsOnscreen은 CFBoolean이다.
        [DllImport(CoreFoundationLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFBooleanGetValue(IntPtr boolean);

        // ★ 2026-08-29 — Dock 실측(IDockMetricsService)용. 사용자 기본 설정을 **읽기만** 하는 공개 API다
        // (CFPreferencesSetAppValue 같은 쓰기 함수는 이 파일에 존재하지 않는다 — 절대 불변 원칙 3).
        // 권한 요구 없음: 화면 기록도 접근성도 아닌, 자기 프로세스에서 남의 앱 도메인 설정을 조회하는
        // 표준 경로다(`defaults read com.apple.dock`이 하는 일과 정확히 같다).
        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFPreferencesCopyAppValue(IntPtr key, IntPtr applicationID);

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFGetTypeID(IntPtr cf);

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFArrayGetTypeID();

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFNumberGetTypeID();

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFBooleanGetTypeID();

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFStringGetTypeID();

        private const int kCFNumberFloat64Type = 6;

        [DllImport(CoreFoundationLib)]
        private static extern void CFRelease(IntPtr cf);

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFDictionaryGetTypeID();

        // ============================================================================
        // ★ 2026-08-29 — Objective-C 런타임 최소 배선. **오직 하나의 목적**: NSWorkspace로
        // "지금 Dock에 타일이 생기는 앱"을 정확히 세는 것(IDockMetricsService 문서 2절).
        // ============================================================================
        // 왜 이게 필요한가: Dock 폭 공식의 유일한 미지수가 "실행 중이지만 Dock에 고정돼 있지 않은 앱의
        // 수"였고, 직전 라운드는 그걸 셀 방법이 없다고 보고 상수 6으로 때려박아 좌우 각 77pt를 틀렸다.
        // 그 수의 정의 그 자체가 NSWorkspace.runningApplications 중 activationPolicy ==
        // NSApplicationActivationPolicyRegular인 앱이다. 조회 전용 공개 API이며 어떤 권한도 요구하지
        // 않는다(화면 기록/접근성 모두 무관 — 절대 불변 원칙 3, 비침해 원칙).
        //
        // ★ 마샬링 안전 규칙 — 이 파일에서 objc_msgSend는 **정수/포인터 반환만** 쓴다.
        // 구조체(NSRect 등)나 부동소수 반환은 아키텍처별 반환 규약(ARM64의 x8 간접 반환, x86_64의
        // _stret/_fpret 분기)이 달라 P/Invoke 선언 실수 시 잡을 수 없는 하드 크래시가 난다. 그래서
        // NSScreen.visibleFrame으로 Dock 두께를 직접 재는 경로는 **일부러 쓰지 않았고**, 두께는
        // tilesize에서 파생시킨다(IDockMetricsService 문서 4절). 여기 선언된 3개 오버로드는 전부
        // 포인터/NSInteger 반환이라 두 아키텍처에서 동일한 규약을 탄다.
        //
        // objc_msgSend는 C에서 가변인자로 선언돼 있지만 실제 호출 규약은 "받는 메서드의 시그니처
        // 그대로"다. 그래서 시그니처마다 EntryPoint를 같게 둔 별도 선언을 만드는 것이 정석이며
        // (Xamarin.Mac/Unity 네이티브 플러그인이 쓰는 바로 그 방식), 하나의 선언을 여러 시그니처에
        // 재사용하면 안 된다.
        private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

        [DllImport(ObjCLib, CharSet = CharSet.Ansi, EntryPoint = "objc_getClass")]
        private static extern IntPtr ObjCGetClass(string name);

        [DllImport(ObjCLib, CharSet = CharSet.Ansi, EntryPoint = "sel_registerName")]
        private static extern IntPtr ObjCSelector(string name);

        /// <summary>[receiver selector] — 객체 포인터 반환(sharedWorkspace/runningApplications/bundleIdentifier).</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr ObjCSendPtr(IntPtr receiver, IntPtr selector);

        /// <summary>[receiver selector] — NSInteger/NSUInteger 반환(count/activationPolicy).
        /// IntPtr로 받으면 32/64비트 양쪽에서 폭이 자동으로 맞는다.</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr ObjCSendNInt(IntPtr receiver, IntPtr selector);

        /// <summary>[receiver selector:index] — NSUInteger 인자 1개, 객체 포인터 반환(objectAtIndex:).</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr ObjCSendPtrWithNUInt(IntPtr receiver, IntPtr selector, IntPtr index);

        /// <summary>[receiver selector:pid] — pid_t(= int32) 인자 1개, 객체 포인터 반환
        /// (+[NSRunningApplication runningApplicationWithProcessIdentifier:]). pid_t는 NSInteger가 아니라
        /// 정확히 32비트이므로 IntPtr로 넘기면 안 된다 — 아래 "전체화면 게임 판별" 절 참고.</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr ObjCSendPtrWithInt32(IntPtr receiver, IntPtr selector, int pid);

        /// <summary>[receiver selector:object] — 객체 인자 1개, 객체 포인터 반환(+[NSBundle bundleWithURL:]).</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr ObjCSendPtrWithPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

        /// <summary>NSApplicationActivationPolicyRegular — "Dock에 타일이 생기고 메뉴바를 갖는 보통 앱".
        /// 1=Accessory(LSUIElement, Dock 타일 없음), 2=Prohibited(백그라운드 전용).</summary>
        private const int NSApplicationActivationPolicyRegular = 0;

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
        private readonly IntPtr _keyWindowAlpha;
        private readonly IntPtr _keyWindowIsOnscreen;

        /// <summary>앱 번들 Info.plist의 App Store 카테고리 키 — 전체화면 "게임" 판별용
        /// (아래 <see cref="TryGetFrontWindowAppCategory"/> 참고). 다른 키들과 같은 규칙:
        /// 프로세스 수명 동안 1회 생성, CFRelease하지 않는다.</summary>
        private readonly IntPtr _keyAppCategoryType;

        // 열거 결과 버퍼. 매 호출 시 새 List를 만들지 않고 Clear 후 재사용한다(Win32WindowService와
        // 동일한 24시간 상주 앱 컨벤션).
        private readonly List<PlatformFoothold> _footholdBuffer = new List<PlatformFoothold>(64);

        // ★ 헤드라인 기능("윈도우 창 = 지형") 검증 라운드(2026-08-28) — _footholdBuffer와 **인덱스가 1:1로
        // 대응하는** 소유 앱 이름 버퍼. PlatformFoothold 자체에는 이름을 넣지 않는다: 그 구조체는 4개
        // 플랫폼이 공유하는 계약이고, "창 제목/앱 이름을 저장하지 않는다"는 기존 설계(Win32WindowService가
        // GetWindowText 결과를 버리는 것과 동일)를 깨지 않기 위해서다. 여기 이름은 **진단 로그에서
        // 사람이 읽기 위한 용도로만** 쓰이며(리더가 화면을 볼 수 없어 로그가 유일한 판별 수단),
        // 이 값으로 원본 창을 조작하는 API는 어디서도 호출하지 않는다(CLAUDE.md 절대 불변 원칙 3).
        private readonly List<string> _footholdOwnerNames = new List<string>(64);

        // ============================================================================
        // 가려진 창 제거(오클루전 컬링)용 재사용 버퍼 — 아래 EnumerateFootholds() 문서 참고.
        // 전부 재사용 리스트다(24시간 상주 앱, 매 폴링 할당 금지 컨벤션).
        // ============================================================================
        private readonly List<Rect> _rawRects = new List<Rect>(64);
        private readonly List<long> _rawHandles = new List<long>(64);
        private readonly List<string> _rawNames = new List<string>(64);
        private readonly List<int> _rawPids = new List<int>(64);
        private readonly List<float> _rawAlphas = new List<float>(64);
        private readonly List<bool> _rawOnscreen = new List<bool>(64);
        private readonly List<float> _rawVisibleWidth = new List<float>(64); // 가려짐 계산 후 남은 총 폭

        // ★ 2026-08-29 창 도둑 복구 라운드 — IRawWindowRectSource 읽기 전용 채널의 뒷단.
        // 위 _rawRects/_rawHandles(가려짐 계산 입력)와 **같은 패스에서 같은 창들로** 채워지는 사본이며,
        // 발판 열거/가려짐 계산에는 전혀 참여하지 않는다(순수 추가 출력). 소비자에게는 아래
        // _readOnlyRawWindows 뷰만 내보내 List로 다시 캐스팅해 변형하는 경로를 막는다
        // (FootholdPoller.cs의 BUG-P1-M4 대응과 같은 이유).
        private readonly List<PlatformFoothold> _rawWindowBuffer = new List<PlatformFoothold>(64);
        private readonly ReadOnlyCollection<PlatformFoothold> _readOnlyRawWindows;

        /// <summary>
        /// IRawWindowRectSource 구현 — 마지막 열거 패스의 원본 창 목록(가려짐 필터 이전, 창 전체 사각형).
        /// 조회만 한다(절대 불변 원칙 3).
        /// </summary>
        public IReadOnlyList<PlatformFoothold> RawWindows => _readOnlyRawWindows;

        // 필터에서 탈락한 창들(진단 로그 전용 — 리더가 "보이지 않는데 발판이 된 창"을 특정할 수 있게).
        private readonly List<Rect> _rejRects = new List<Rect>(32);
        private readonly List<string> _rejNames = new List<string>(32);
        private readonly List<int> _rejPids = new List<int>(32);
        private readonly List<string> _rejReasons = new List<string>(32);
        // ★ 2026-08-31 — 가려짐 계산 본체는 Platform/VisibleTopEdgeSolver.cs로 이관됐다.
        // 이유: 같은 알고리즘이 이 macOS 전용 파일 안에 갇혀 있어서 Windows 구현이 재사용하지
        // 못했고(그 결과 Windows에는 가려짐 필터가 아예 없어 2026-08-31 신고 버그가 났다),
        // macOS가 아닌 환경에서는 컴파일조차 안 돼 테스트로 겨냥할 수도 없었다.
        // 구간 작업 버퍼(_segStarts/_segEnds/_tmpStarts/_tmpEnds)도 그 클래스가 함께 소유한다.
        private readonly VisibleTopEdgeSolver _topEdgeSolver = new VisibleTopEdgeSolver();

        /// <summary>이번 열거에서 필터를 통과한 "원본 창" 개수(가려짐 판정 전). 진단 로그 전용.</summary>
        public int LastRawWindowCount { get; private set; }

        /// <summary>
        /// 가려짐 판정으로 상단 테두리가 통째로 사라진 창의 수(원본 창 - 발판을 하나라도 낸 창).
        /// 진단 로그 전용 — 리더가 "지금 몇 개가 가려져서 제외됐는지"를 로그로 판별할 수 있게 한다.
        /// </summary>
        public int LastFullyOccludedWindowCount { get; private set; }

        // ============================================================================
        // ★★ IWindowEnumerationCostSource (2026-09-01) — 리더 실측 대응
        // ============================================================================
        // 리더가 맥 빌드를 6분간 띄워 관측한 [발판열거] 시간순 수치:
        //   1분 3.55ms -> 2분 4.01ms -> 3분 3.95ms -> 5분 4.92ms -> 6분 5.10ms  (1회 평균, +44%)
        // **1회 비용이 단조 증가에 가깝게 커진다.** 그런데 같은 로그가
        //   `전체 창 -1개(최대 -1), 정밀검사 -1회(최대 -1)`
        // 로 찍혔다 — 이 서비스가 IWindowEnumerationCostSource를 구현하지 않아 "모르는 값"이었다.
        //
        // 그 두 숫자가 없으면 **처방이 정반대인 두 가설을 구분할 수 없다**:
        //   (A) 창 개수가 늘고 있다        -> 외부 요인. 우리 비용이 창 수에 비례하는 설계 문제
        //                                    -> 범위 축소/이벤트 방식이 정답.
        //   (B) 창 개수는 그대로인데 1회 비용만 커진다 -> **우리 코드 안의 누적 결함**. 즉시 고칠 버그다.
        // 이 두 프로퍼티가 그 구분을 준다. 비용은 패스당 정수 대입 1회 + 창당 증가 1회다.
        private int _enumeratedWindowCount;
        private int _detailProbeCount;

        /// <summary>
        /// 마지막 패스에서 <c>CGWindowListCopyWindowInfo</c>가 돌려준 창의 <b>총 개수</b>(필터 이전).
        /// Windows의 <c>EnumWindows</c> 콜백 횟수와 같은 의미다.
        /// </summary>
        public int LastEnumeratedWindowCount => _enumeratedWindowCount;

        /// <summary>
        /// 값싼 필터(레이어 0 + bounds 파싱)를 뚫고 <b>창당 상세 조회</b>까지 간 횟수.
        ///
        /// <para><b>Windows와 의미가 정확히 같지는 않다</b>(로그 라벨은 "정밀검사"로 공유한다).
        /// Windows에서는 이것이 <c>DwmGetWindowAttribute</c> = <b>크로스 프로세스</b> 호출 횟수다.
        /// macOS에서 창 목록은 <c>CGWindowListCopyWindowInfo</c> <b>한 번</b>의 WindowServer 왕복으로
        /// 통째로 오므로 창당 크로스 프로세스 호출이 없다. 대신 창마다
        /// <c>TryGetString</c>(CFString -> C# 문자열 복사) / <c>TryGetFloat</c> / <c>TryGetBool</c> /
        /// <c>CGRectMakeWithDictionaryRepresentation</c>가 도는데, <b>창 수에 비례해 커지는 실제 비용</b>은
        /// 그쪽이다. 그래서 두 플랫폼에서 이 값이 답하는 질문은 같다:
        /// <b>"창당 비싼 처리를 몇 번 했는가"</b>.</para>
        /// </summary>
        public int LastDwmProbeCount => _detailProbeCount;

        /// <summary>
        /// 가려짐 계산 후 남은 상단 테두리 조각이 이보다 좁으면 버린다(OS 포인트). 캐릭터 몸통 폭보다
        /// 훨씬 좁은 조각 위에 서 있게 하면 "허공에 떠 있다"는 사용자 인식이 그대로 재발하기 때문이다.
        /// </summary>
        private const float MinVisibleFootholdWidth = 24f;

        /// <summary>알파가 이 값 미만이면 "보이지 않는 창"으로 보고 발판 후보에서 제외(리더 지시 2항).</summary>
        private const float MinWindowAlpha = 0.05f;

        /// <summary>이보다 작은 창은 발판으로 쓰지 않는다(알림 배너/툴팁/보조 패널 등, OS 포인트).</summary>
        private const float MinWindowWidth = 60f;
        private const float MinWindowHeight = 40f;

        // 탈락 사유 문자열 상수 — 매 폴링 문자열 할당을 피하려고 리터럴을 재사용한다.
        private const string RejectAlpha = "알파≈0(투명/비표시)";
        private const string RejectTooSmall = "너무 작음";
        private const string RejectOffscreenFlag = "kCGWindowIsOnscreen=false";
        private const string RejectOffDisplay = "화면(주 디스플레이) 밖";
        private const string RejectFullyOccluded = "다른 창에 완전히 가려짐";

        // CFStringGetCString용 재사용 버퍼(오너 이름 조회 — **발판/전체화면 제외 판정**의 보조 신호로만
        // 사용한다. 좌표계의 출처 판정에는 절대 쓰지 않는다: 같은 앱의 두 번째 인스턴스는 이름이
        // 정확히 같아서 남의 창이 우리 좌표계를 덮어쓴다 — IsSelfProcessWindow/IsOwnAppWindow 문서 참고).
        // 창 제목 자체는 PlatformFoothold가 애초에 노출하지 않는다
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
            _readOnlyRawWindows = _rawWindowBuffer.AsReadOnly(); // 살아있는 뷰 — 매 폴링 재생성 금지(할당 0).
            _keyWindowLayer = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowLayer", kCFStringEncodingUTF8);
            _keyWindowBounds = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowBounds", kCFStringEncodingUTF8);
            _keyWindowOwnerPID = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowOwnerPID", kCFStringEncodingUTF8);
            _keyWindowOwnerName = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowOwnerName", kCFStringEncodingUTF8);
            _keyWindowNumber = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowNumber", kCFStringEncodingUTF8);
            _keyWindowAlpha = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowAlpha", kCFStringEncodingUTF8);
            _keyWindowIsOnscreen = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowIsOnscreen", kCFStringEncodingUTF8);
            _keyAppCategoryType = CFStringCreateWithCString(IntPtr.Zero, "LSApplicationCategoryType", kCFStringEncodingUTF8);

            using (var self = System.Diagnostics.Process.GetCurrentProcess())
            {
                _currentProcessId = self.Id;
                _currentProcessName = self.ProcessName;
            }
        }

        /// <summary>
        /// 화면 배율(Unity 픽셀 ↔ OS 포인트)을 실측해 <see cref="ScreenCoordinateConverter"/>에 반영하고,
        /// 호출자가 <c>StickConfig.desktopDpiScale</c>에 대입할 값으로 **0(= "자동")** 을 돌려준다.
        ///
        /// ============================================================================
        /// 반환값이 왜 배율이 아니라 0인가 (2026-08-29 Retina 대응 라운드, 리더 지시 2항)
        /// ============================================================================
        /// 이 라운드에서 배율의 단일 소스가 `StickConfig`(에셋 필드)에서 `ScreenCoordinateConverter`
        /// (런타임 실측)로 옮겨졌고, `StickConfig.desktopDpiScale`의 의미는 "배율 값"이 아니라
        /// **"자동 산출을 덮어쓰는 수동 오버라이드(0 이하 = 자동)"** 로 바뀌었다.
        ///
        /// 그래서 여기서 측정한 값을 그대로 반환해 호출자가 그 필드에 넣게 두면, 시작 시점의 배율이
        /// 수동 오버라이드로 **박제**되어 이후 <see cref="CaptureOverlayOrigin"/>의 매 폴링 재측정이
        /// 통째로 무시된다. 그 재측정은 이 앱에서 실제로 필요하다 — MacOverlayStateEnforcer가 시작 직후
        /// `Screen.SetResolution`으로 창을 화면 전체로 넓히고, 사용자가 창을 다른 배율의 모니터로 옮길
        /// 수도 있다. 그래서 측정 결과는 컨버터에 **직접** 넣고, 필드에는 "자동"을 뜻하는 0을 준다.
        ///
        /// ★ Player.log를 읽는 사람에게: 이 로그 바로 뒤에 찍히는
        ///   `[StickmanAgent] ... desktopDpiScale=0.000로 설정` 은 **정상**이며 "수동 오버라이드 없음
        ///   (= 자동)"을 뜻한다. 실제로 쓰이는 배율은 아래 이 로그가 찍는 값이다.
        ///
        /// 측정 방법(1순위): 우리 창의 실제 폭(kCGWindowBounds, OS 포인트)을 같은 순간의
        /// Screen.width(Unity 픽셀)로 나눈다. 창 열거는 UniWindowController의 부착 여부와 무관하게
        /// Unity가 자기 NSWindow를 만든 직후부터 성공하므로(실측 확인 — Start() 시점에 이미 자기 창이
        /// 조회된다), 이전 라운드가 겪었던 "부착 전이라 clientSize=(0,0)" 함정에 걸리지 않는다.
        /// 겸사겸사 오버레이 원점도 같은 관측에서 함께 반영돼 첫 프레임부터 좌표 변환이 정확해진다.
        ///
        /// 2순위(자기 창을 못 찾은 경우): 디스플레이의 backingScaleFactor(CGDisplayModeGetPixelWidth /
        /// CGDisplayModeGetWidth)의 역수. `macRetinaSupport`가 켜져 있는 지금은 이 값이 1순위와 일치하지만
        /// (Unity가 백킹 픽셀을 보고하므로), 그 설정이 다시 꺼지면 어긋난다 — 그래서 어디까지나 폴백이고
        /// 경고를 남긴다. 둘 다 실패하면 아무것도 건드리지 않는다(컨버터의 직전 값/기본값 1 유지).
        /// </summary>
        public float DetectDesktopDpiScale()
        {
            if (TryGetSelfWindowRect(out Rect selfFrame) && Screen.width > 0 && selfFrame.width > 0f)
            {
                // ★ 2026-09-01 — frame(타이틀바 포함)이 아니라 콘텐츠 사각형을 보고한다. 기동 직후에는
                //   창이 아직 보더리스가 아니라 frame이 28pt 더 크고 원점이 그만큼 위에 있다
                //   (실기 로그 기동 첫 줄: 창=(0,33,1512,1010), 같은 창의 콘텐츠는 (0,61,1512,982)).
                OverlayContentRectPolicy.TryStripTopDecoration(
                    selfFrame, ResolveOverlayContentSize(selfFrame),
                    OverlayContentRectPolicy.DefaultEpsilonPoints, out Rect selfRect, out _);
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(selfRect);
                Debug.Log($"[MacWindowService] DetectDesktopDpiScale(): 자기 창 실측 — 창={selfRect}(원본 frame={selfFrame}), " +
                    $"Screen=({Screen.width}x{Screen.height}) -> 자동 배율 {ScreenCoordinateConverter.AutoDpiScale:F3} " +
                    "(창 폭[OS 포인트] / Screen.width[Unity 픽셀])를 ScreenCoordinateConverter에 반영했습니다" +
                    "(오버레이 원점도 같은 관측에서 함께 반영). " +
                    "반환값 0 = '수동 오버라이드 없음(자동)' — 바로 뒤의 [StickmanAgent] 로그가 " +
                    "desktopDpiScale=0.000으로 찍히는 것은 정상입니다.");
                return 0f;
            }

            Debug.LogWarning("[MacWindowService] DetectDesktopDpiScale(): 자기 창을 찾지 못해 디스플레이 " +
                "백킹 배율 기반 폴백을 사용합니다 — macRetinaSupport 설정에 따라 틀릴 수 있습니다.");

            IntPtr mode = CGDisplayCopyDisplayMode(CGMainDisplayID());
            if (mode == IntPtr.Zero)
            {
                Debug.LogWarning("[MacWindowService] DetectDesktopDpiScale(): CGDisplayCopyDisplayMode 실패 — " +
                    "배율을 갱신하지 않고 ScreenCoordinateConverter의 현재 값을 그대로 둡니다.");
                return 0f;
            }

            try
            {
                double pointWidth = (double)(ulong)CGDisplayModeGetWidth(mode);
                double pixelWidth = (double)(ulong)CGDisplayModeGetPixelWidth(mode);
                if (pointWidth <= 0.0 || pixelWidth <= 0.0) return 0f;

                double backingScaleFactor = pixelWidth / pointWidth;
                if (backingScaleFactor <= 0.0) return 0f;

                float scale = (float)(1.0 / backingScaleFactor);
                ScreenCoordinateConverter.AutoDpiScale = scale;
                Debug.Log($"[MacWindowService] DetectDesktopDpiScale(): 디스플레이 포인트폭={pointWidth}, " +
                    $"백킹픽셀폭={pixelWidth}, backingScaleFactor={backingScaleFactor:F3} -> 자동 배율 {scale:F3}(폴백).");
                return 0f;
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

        private bool TryGetFloat(IntPtr windowDict, IntPtr key, out float value)
        {
            value = 0f;
            IntPtr numberRef = CFDictionaryGetValue(windowDict, key);
            if (numberRef == IntPtr.Zero) return false;
            if (!CFNumberGetValueDouble(numberRef, kCFNumberFloat64Type, out double d)) return false;
            value = (float)d;
            return true;
        }

        private bool TryGetBool(IntPtr windowDict, IntPtr key, out bool value)
        {
            value = false;
            IntPtr boolRef = CFDictionaryGetValue(windowDict, key);
            if (boolRef == IntPtr.Zero) return false;
            value = CFBooleanGetValue(boolRef);
            return true;
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
        /// 이 창이 <b>바로 이 프로세스</b>의 창인가 — <c>kCGWindowOwnerPID</c> <b>단독</b> 판정.
        ///
        /// ============================================================================
        /// ★ 2026-09-01 — 왜 이름 비교를 여기서 뺐는가 (이름은 프로세스를 식별하지 못한다)
        /// ============================================================================
        /// 직전까지 이 판정은 "PID가 같거나 <b>또는</b> kCGWindowOwnerName이 같으면 자기 창"이었다.
        /// 그런데 <b>같은 앱의 두 번째 인스턴스</b>는 PID가 다르고 이름은 <b>정확히 같다</b>. 그래서
        /// 이름 분기가 남의 프로세스 창을 "내 창"으로 통과시켰고, 그 창의 사각형이 그대로
        /// <see cref="ScreenCoordinateConverter.ReportOverlayWindowOsRect"/>로 흘러들어가
        /// <b>우리 좌표계 전체가 남의 창을 기준으로 재설정</b>됐다.
        ///
        /// <para>실기 흔적(2026-09-01, PID 11451 로그 18:05~18:06 구간): 오버레이 사각형이
        /// <c>(0,0,1512,982)</c> ↔ <c>(0,33,1512,1010)</c>로 폴링마다 교대했다. 뒤엣것은
        /// <b>StickMate가 기동 중일 때의 창 모양 그 자체</b>다 — 같은 로그 26번째 줄, 우리 자신의
        /// 기동 구간에 바이트 단위로 같은 값이 찍혀 있다(타이틀바 28pt가 아직 붙어 있고 AppKit이
        /// 그 타이틀바를 메뉴바 아래로 밀어 놓은 상태). 그런데 우리 창은 17:17에 전체화면 적합을
        /// <b>1회</b> 끝낸 뒤 다시 만져진 적이 없다(재적합 시도 1/6, 재무장 0회). 즉 그 순간의
        /// (0,33,1512,1010)은 <b>우리 창일 수 없다</b>.</para>
        ///
        /// <para>교대가 일어난 이유도 여기서 설명된다: <see cref="CaptureOverlayOrigin"/>은 한 패스에서
        /// <b>면적이 가장 큰</b> 자기 창을 고르는데, 남의 창(1512x1010)이 우리 창(1512x982)보다 크므로
        /// 그 창이 온스크린 목록에 있는 패스에서는 그쪽이, 없는 패스에서는 우리 창이 선택된다.</para>
        ///
        /// <para><b>PID 조회가 실패할 걱정은 없다</b>: <c>kCGWindowOwnerPID</c>는
        /// <c>CGWindowListCopyWindowInfo</c>가 항상 채우는 필수 키다. 그래도 키 자체가 없는 이론적
        /// 경우에만 이름을 안전망으로 쓴다 — <b>PID를 읽었다면 그 비교 결과가 곧 결론</b>이고,
        /// "PID는 다른데 이름이 같아서 통과"는 이제 일어나지 않는다.</para>
        /// </summary>
        private bool IsSelfProcessWindow(IntPtr windowDict)
        {
            if (TryGetInt(windowDict, _keyWindowOwnerPID, out int ownerPid))
            {
                return ownerPid == _currentProcessId;
            }
            // PID 키가 아예 없는 경우에만 이름 안전망(이론상 도달하지 않는다).
            string ownerName = TryGetString(windowDict, _keyWindowOwnerName);
            return !string.IsNullOrEmpty(ownerName) && ownerName == _currentProcessName;
        }

        /// <summary>
        /// 이 창이 <b>우리 앱(같은 이름의 다른 인스턴스 포함)</b>의 창인가 — "발판/전체화면 판정에서
        /// 빼야 하는가"라는 <b>다른 질문</b>에 답한다.
        ///
        /// <para>두 질문을 한 함수가 겸하고 있던 것이 위 사고의 구조적 원인이다:</para>
        /// <list type="bullet">
        ///   <item><b>좌표계의 출처</b>("이 창이 내 오버레이인가") — 반드시 PID 단독이어야 한다.
        ///         남의 창을 기준으로 좌표계를 세우면 캐릭터가 통째로 어긋난다.</item>
        ///   <item><b>제외 대상</b>("이 창을 밟거나 전체화면으로 오인하면 안 되는가") — 이름까지
        ///         포함하는 <b>넓은</b> 판정이 옳다. 두 번째 인스턴스의 투명 오버레이를 발판으로
        ///         삼거나 "전체화면 앱"으로 오인하면 안 되기 때문이다(기존 동작 유지).</item>
        /// </list>
        /// </summary>
        private bool IsOwnAppWindow(IntPtr windowDict)
        {
            if (IsSelfProcessWindow(windowDict)) return true;
            string ownerName = TryGetString(windowDict, _keyWindowOwnerName);
            return !string.IsNullOrEmpty(ownerName) && ownerName == _currentProcessName;
        }

        /// <summary>
        /// ★ 사용자 신고 버그(2026-08-28): "창 위에서 걸어다닐 때 다른 창을 최대화하면 중간에 그대로
        /// 거기서 걸어다님" — 즉 딛고 있던 창이 다른 창에 완전히 가려져 눈에 보이지 않는데도 캐릭터가
        /// 허공에서 계속 걷는다.
        ///
        /// 원인(가설 4가 적중, 나머지 3개는 코드로 배제됨):
        ///   (1) 폴링 주기? — StickConfig.footholdPollInterval은 0.3~0.5초로 짧다. 사용자는 "그대로
        ///       계속 걸어다닌다"고 했으므로 일시적 지연이 아니다. 주 원인 아님(반응성 차원에서 주기는
        ///       0.5 -> 0.3으로 함께 줄였다).
        ///   (2) 접지 고착(sticky)? — GroundSensor.Sense()는 핸들을 기억하지 않고 **매 프레임 좌표로
        ///       재판정**하고, StickmanBlackboard.GroundedTick()은 접지 실패가 fallGraceDuration(0.1초)
        ///       지속되면 무조건 Fall로 보낸다. 고착 경로 없음 — 원인 아님.
        ///   (3) 발판 사각형이 "창 전체"라 창 내부 어디서나 접지되나? — Sense()의 세로 판정은
        ///       `Mathf.Abs(footOs.y - r.y) <= tolerance`로 **r.y(창 상단선)만** 본다. 창 내부는 바닥이
        ///       되지 않는다 — 원인 아님.
        ///   (4) **가려진 창이 그대로 발판으로 남는다 — 이게 원인이다.**
        ///       kCGWindowListOptionOnScreenOnly는 "화면에 존재하는" 창을 모두 돌려준다. 다른 창에
        ///       완전히 덮여 **한 픽셀도 보이지 않는 창도 그대로 목록에 남는다.** 그래서 최대화된 창이
        ///       덮어버린 뒤에도 옛 창의 상단선이 발판으로 계속 살아 있었고, 사용자 눈에는 캐릭터가
        ///       허공을 걷는 것으로 보였다.
        ///
        /// 수정: z-order를 실제로 활용해 **"눈에 보이는 상단 테두리 조각"만** 발판으로 채택한다.
        /// CGWindowListCopyWindowInfo(OnScreenOnly)는 앞->뒤 순서를 보장하므로, 창 i의 상단선
        /// 구간 [x, x+width] (높이 r.y)에서 **i보다 앞에 있는 모든 창 j 중 그 높이를 세로로 포함하는
        /// 것들의 가로 구간**을 빼면 남는 것이 곧 "실제로 보이는 상단 테두리"다. 남은 조각이 여러 개면
        /// 조각마다 발판을 하나씩 낸다(핸들은 원본 창 그대로 — ParkourClimb의 핸들 추적과 진단 로그가
        /// 그대로 동작한다). 조각이 하나도 남지 않으면 그 창은 발판을 내지 않는다 = 캐릭터가 낙하한다.
        ///
        /// 비용: 창 수 n에 대해 O(n^2) 사각형 연산이지만 n은 보통 수십 개이고 폴링 주기(0.3초)마다
        /// 한 번만 도므로 무시 가능하다. 모든 버퍼는 재사용한다(할당 0).
        ///
        /// 읽기 전용 원칙은 그대로다 — 여기서 하는 일은 전부 우리 쪽 메모리상의 사각형 계산이며,
        /// 타 프로세스 창에는 조회 외에 어떤 호출도 하지 않는다.
        /// </summary>
        public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
        {
            _footholdBuffer.Clear();
            _footholdOwnerNames.Clear();
            _rawRects.Clear();
            _rawHandles.Clear();
            _rawNames.Clear();
            _rawPids.Clear();
            _rawAlphas.Clear();
            _rawOnscreen.Clear();
            _rawVisibleWidth.Clear();
            _rawWindowBuffer.Clear();
            _rejRects.Clear();
            _rejNames.Clear();
            _rejPids.Clear();
            _rejReasons.Clear();
            LastRawWindowCount = 0;
            LastFullyOccludedWindowCount = 0;
            _enumeratedWindowCount = 0;
            _detailProbeCount = 0;
            bool hasDisplay = TryGetMainDisplayBounds(out Rect displayBounds);
            _overlayOriginPassArea = 0.0; // CaptureOverlayOrigin()의 "이번 패스 최대 면적" 리셋.
            // ★ 2026-09-01 — 같은 관측을 CaptureOverlayOrigin()의 위생 검사에도 넘긴다(새 호출 0건).
            _hasDisplayBoundsThisPass = hasDisplay;
            _displayBoundsThisPass = displayBounds;
            // ★ 2026-09-01 — 창 장식(타이틀바) 제거용 콘텐츠 크기를 이번 패스에 **한 번만** 읽는다.
            //   CaptureOverlayOrigin()은 패스당 여러 번 불릴 수 있는데(자기 창이 여러 개), 그때마다
            //   네이티브를 두드릴 이유가 없다. 부착 전에는 (0,0)이고 그러면 규칙이 보정을 포기한다.
            _overlayContentSizeThisPass = ReadControllerContentSize();

            IntPtr windowArray = CopyOnScreenWindowList();
            if (windowArray == IntPtr.Zero) return _footholdBuffer; // 조회 실패 — FallbackPlatformWindowService 안전망이 감싸므로 빈 리스트로도 안전.

            try
            {
                long count = CFArrayGetCount(windowArray);
                _enumeratedWindowCount = (int)count; // 계측 전용(IWindowEnumerationCostSource) — 대입 1회.
                for (long i = 0; i < count; i++)
                {
                    IntPtr windowDict = CFArrayGetValueAtIndex(windowArray, i);
                    if (windowDict == IntPtr.Zero) continue;

                    // 이 앱 자신(Unity 플레이어 프로세스)의 창은 발판 후보에서 제외한다. 순서 주의:
                    // 아래 레이어 필터보다 **먼저** 판정한다 — 우리 창은 항상위(kCGWindowLayer=101)라
                    // 레이어 필터에 먼저 걸리면 여기까지 오지 못하고, 그러면 바로 아래의 오버레이 원점
                    // 캡처가 영원히 실행되지 않는다(발판 목록에서 제외된다는 결과 자체는 순서와 무관하게 동일).
                    if (IsOwnAppWindow(windowDict))
                    {
                        // ★ 좌표계의 출처는 **이 프로세스의 창만**이다(IsSelfProcessWindow 문서 참고).
                        //   같은 이름의 다른 인스턴스 창은 발판에서 빼기만 하고, 원점/배율에는 절대
                        //   반영하지 않는다 — 그것이 2026-09-01 좌표계 교대의 직접 원인이었다.
                        if (IsSelfProcessWindow(windowDict)) CaptureOverlayOrigin(windowDict);
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

                    _detailProbeCount++; // 계측 전용 — 아래부터가 창당 비싼 구간(CFString 복사 등)이다.

                    var screenRect = new Rect((float)rect.Origin.X, (float)rect.Origin.Y, (float)rect.Size.Width, (float)rect.Size.Height);
                    // 위 _footholdOwnerNames 선언부 참고 — 진단 로그 전용, 인덱스 1:1 유지가 계약이다.
                    string ownerName = TryGetString(windowDict, _keyWindowOwnerName);
                    if (string.IsNullOrEmpty(ownerName)) ownerName = "(이름없음)";
                    TryGetInt(windowDict, _keyWindowOwnerPID, out int ownerPid);
                    float alpha = TryGetFloat(windowDict, _keyWindowAlpha, out float a) ? a : 1f;
                    bool onScreen = !TryGetBool(windowDict, _keyWindowIsOnscreen, out bool os) || os;

                    // ---- 필터(리더 지시 2항). 탈락 사유를 남겨 진단 로그로 특정할 수 있게 한다. ----
                    string reject = null;
                    if (alpha < MinWindowAlpha) reject = RejectAlpha;
                    else if (screenRect.width < MinWindowWidth || screenRect.height < MinWindowHeight) reject = RejectTooSmall;
                    else if (!onScreen) reject = RejectOffscreenFlag;
                    else if (hasDisplay && !screenRect.Overlaps(displayBounds)) reject = RejectOffDisplay;

                    if (reject != null)
                    {
                        _rejRects.Add(screenRect); _rejNames.Add(ownerName); _rejPids.Add(ownerPid); _rejReasons.Add(reject);
                        continue;
                    }

                    // CGWindowListCopyWindowInfo(kCGWindowListOptionOnScreenOnly)는 z-order 기준
                    // 앞->뒤 순서로 보장되어 반환된다(Apple 문서화된 동작). 그 순서를 그대로 유지한 채
                    // 원본 목록에 쌓아두고, 루프가 끝난 뒤 가려짐 계산을 한 번에 수행한다.
                    _rawRects.Add(screenRect);
                    _rawHandles.Add(handle);
                    _rawNames.Add(ownerName);
                    _rawPids.Add(ownerPid);
                    _rawAlphas.Add(alpha);
                    _rawOnscreen.Add(onScreen);
                    _rawVisibleWidth.Add(0f);

                    // IRawWindowRectSource 채널(창 도둑 전용): 가려짐 계산에 들어가기 전 상태를 그대로
                    // 한 벌 복사해 둔다. IsTopmost는 z-order 맨 앞(첫 항목)만 true — 발판 쪽의
                    // "실제로 보이는 첫 조각" 정의와 달리 여기서는 순수 z-order 의미다.
                    _rawWindowBuffer.Add(new PlatformFoothold(handle, screenRect, _rawWindowBuffer.Count == 0));
                }
            }
            finally
            {
                CFRelease(windowArray);
            }

            LastRawWindowCount = _rawRects.Count;
            BuildVisibleTopEdgeFootholds(hasDisplay, displayBounds);
            return _footholdBuffer;
        }

        /// <summary>
        /// 원본 창 목록(_rawRects, z-order 앞->뒤)에서 "다른 창에 가려지지 않은 상단 테두리 조각"만
        /// 골라 _footholdBuffer를 채운다. 위 EnumerateFootholds() 문서의 (4)번 수정 본체.
        /// </summary>
        private void BuildVisibleTopEdgeFootholds(bool hasDisplay, Rect displayBounds)
        {
            // 1) z-order 앞->뒤 순서 그대로 솔버에 넣는다(그 순서가 이 계산의 전부다).
            _topEdgeSolver.Begin();
            for (int i = 0; i < _rawRects.Count; i++)
            {
                _topEdgeSolver.AddWindow(_rawRects[i]);
            }
            // 리더 지시 6항(화면 밖 소실 방지)의 발판 쪽 절반도 솔버가 함께 처리한다: 창이 화면
            // 경계를 넘어가 있어도 발판은 화면 안쪽까지만 뻗는다.
            _topEdgeSolver.Solve(MinVisibleFootholdWidth, hasDisplay, displayBounds);

            // 2) 채택된 조각마다 발판을 하나씩 낸다. 핸들은 원본 창 그대로 유지한다
            //    (ParkourClimb의 핸들 추적과 진단 로그가 그대로 동작해야 하므로).
            for (int s = 0; s < _topEdgeSolver.SegmentCount; s++)
            {
                int i = _topEdgeSolver.GetSegmentWindowIndex(s);
                Rect r = _rawRects[i];
                // isTopmost: 목록 전체에서 처음 채택되는 조각이 곧 "가장 앞에서 실제로 보이는" 발판.
                _footholdBuffer.Add(new PlatformFoothold(_rawHandles[i],
                    new Rect(_topEdgeSolver.GetSegmentStartX(s), r.y, _topEdgeSolver.GetSegmentWidth(s), r.height),
                    _footholdBuffer.Count == 0));
                _footholdOwnerNames.Add(_rawNames[i]);
            }

            // 3) 진단 부기 — 조각을 하나도 내지 못한 창이 곧 "완전히 가려진 창"이다.
            for (int i = 0; i < _rawRects.Count; i++)
            {
                float visible = _topEdgeSolver.GetVisibleWidth(i);
                _rawVisibleWidth[i] = visible;
                if (visible > 0f) continue;
                LastFullyOccludedWindowCount++;
                _rejRects.Add(_rawRects[i]); _rejNames.Add(_rawNames[i]); _rejPids.Add(_rawPids[i]);
                _rejReasons.Add(RejectFullyOccluded);
            }
        }

        /// <summary>
        /// ★ 리더 지시 1항 — "지금 열거되는 창 전체"를 앱 이름 + PID + 사각형 + 알파 + onscreen +
        /// z-order + 가려짐 후 남은 폭 + 탈락 사유까지 한 번에 덤프한다. 사용자 스크린샷과 좌표를
        /// 직접 대조해 "보이지 않는데 발판이 된 창"을 특정하는 것이 목적이다.
        /// 문자열 조립은 이 메서드를 실제로 호출할 때만 일어나므로(진단 주기 5초) 폴링 경로에는
        /// 할당이 추가되지 않는다.
        /// </summary>
        public void AppendWindowDiagnostics(System.Text.StringBuilder sb)
        {
            sb.Append("채택 ").Append(_rawRects.Count).Append("개 [");
            for (int i = 0; i < _rawRects.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                Rect r = _rawRects[i];
                sb.Append('z').Append(i).Append(':').Append(_rawNames[i]).Append("(pid ").Append(_rawPids[i]).Append(") ")
                  .Append('(').Append(r.x.ToString("F0")).Append(',').Append(r.y.ToString("F0")).Append(' ')
                  .Append(r.width.ToString("F0")).Append('x').Append(r.height.ToString("F0")).Append(')')
                  .Append(" alpha=").Append(_rawAlphas[i].ToString("F2"))
                  .Append(" onscreen=").Append(_rawOnscreen[i] ? "Y" : "N")
                  .Append(" 보이는상단폭=").Append(_rawVisibleWidth[i].ToString("F0"));
            }
            sb.Append("] / 탈락 ").Append(_rejRects.Count).Append("개 [");
            for (int i = 0; i < _rejRects.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                Rect r = _rejRects[i];
                sb.Append(_rejNames[i]).Append("(pid ").Append(_rejPids[i]).Append(") ")
                  .Append('(').Append(r.x.ToString("F0")).Append(',').Append(r.y.ToString("F0")).Append(' ')
                  .Append(r.width.ToString("F0")).Append('x').Append(r.height.ToString("F0")).Append(") 사유=")
                  .Append(_rejReasons[i]);
            }
            sb.Append(']');
        }

        /// <summary>
        /// 주 디스플레이의 전체 사각형(Quartz 좌표, OS 포인트 — 메뉴바/Dock 띠까지 포함한 진짜 화면 전체).
        /// MacOverlayStateEnforcer의 오버레이 전체화면 확장이 이 값을 목표 크기로 쓴다.
        ///
        /// 왜 UniWindowController.GetMonitorRect()로는 부족한가(실측): 그쪽은 **visibleFrame**(메뉴바
        /// 33pt + Dock 75pt를 뺀 작업영역)을 돌려준다 — 실측값 (0,75,1512,874). 화면 최상단/최하단 띠에
        /// 걸친 타 앱 창 위로도 캐릭터가 갈 수 있으려면 오버레이가 그 띠까지 덮어야 하므로 화면 전체
        /// 크기가 따로 필요하다. CGDisplayBounds는 순수 조회이며 어떤 창도 건드리지 않는다.
        /// </summary>
        public bool TryGetMainDisplayBounds(out Rect bounds)
        {
            CGRect r = CGDisplayBounds(CGMainDisplayID());
            bounds = new Rect((float)r.Origin.X, (float)r.Origin.Y, (float)r.Size.Width, (float)r.Size.Height);
            return bounds.width > 0f && bounds.height > 0f;
        }

        /// <summary>
        /// 마지막 EnumerateFootholds() 결과에서 handle에 해당하는 창의 소유 앱 이름(진단 로그 전용).
        /// 찾지 못하면 null. 위 _footholdOwnerNames 선언부의 사용 제한(읽기 전용, 로그 전용) 참고.
        /// </summary>
        public string TryDescribeFoothold(long handle)
        {
            int count = Mathf.Min(_footholdBuffer.Count, _footholdOwnerNames.Count);
            for (int i = 0; i < count; i++)
            {
                if (_footholdBuffer[i].Handle == handle) return _footholdOwnerNames[i];
            }
            return null;
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
            // 재적합(전체화면 확장/해상도 변경 후 재무장)이 끝난 프레임에 좌표계를 즉시 갱신하는 훅.
            // 폴링을 기다리면 그 사이 원점/배율이 옛 값이라 캐릭터가 화면 밖으로 튄다.
            _enforcer.OverlayRectReporter = ReportOverlayRectNow;
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
        /// Win32의 "전경창 사각형 == 모니터 전체 사각형" 휴리스틱과 동일한 아이디어를 macOS로 이식하되,
        /// 여기에 **두 개의 조건을 더** 얹는다(2026-08-31, 사용자 신고 "타 앱 전체화면 클릭 시 캐릭터가
        /// 사라짐"에 대한 리더 결정):
        ///   (1) 기하 조건 — (우리 자신을 제외한) 가장 앞선 일반(layer 0) 창의 bounds가 메인 디스플레이
        ///       전체 영역과 일치할 것. (기존 조건)
        ///   (2) <b>게임 조건</b> — 그 창을 소유한 앱의 LSApplicationCategoryType이 게임 계열일 것.
        ///       원칙 2의 문구가 "전체화면 <b>게임</b>"이기 때문이다. 이 조건이 없어서 엑셀/키노트/
        ///       브라우저 전체화면에서도 캐릭터가 사라졌다.
        ///   (3) 디바운스 — 위 판정이 뒤집혔더라도 일정 시간 연속 유지되어야 확정한다(메뉴바 호출로
        ///       bounds가 요동쳐 Resume/Suspend가 반복되던 깜빡임 차단).
        /// </summary>
        public bool IsFullscreenAppActive()
        {
            bool raw = EvaluateFullscreen(out string reason);

            // ★ 2026-08-31 깜빡임(flapping) 차단. 같은 전체화면 창인데도 사용자가 커서를 화면 상단에
            //   올려 메뉴바를 부르면 CGWindow bounds가 (0,33 ...) <-> (0,0 ...) 로 오가서 기하 판정이
            //   뒤집힌다(디버거 실측). 그대로 두면 Resume/Suspend가 반복돼 캐릭터가 깜빡인다.
            //   규칙 자체는 FullscreenSuspendPolicy.cs에 순수 함수로 분리돼 EditMode 테스트가 검증한다.
            bool verdict = _fullscreenDebouncer.Update(raw, Time.realtimeSinceStartupAsDouble, FullscreenVerdictHoldSeconds);

            if (raw != _lastRawFullscreenVerdict)
            {
                _lastRawFullscreenVerdict = raw;
                if (raw != verdict)
                {
                    Debug.Log($"[전체화면판정] 원시 판정이 {raw}로 흔들렸지만 {FullscreenVerdictHoldSeconds:F1}초 연속 " +
                        $"유지되기 전이라 확정하지 않습니다(메뉴바 호출 등에 의한 깜빡임 흡수) — {reason}");
                }
            }

            // ★ 2026-08-29 — 판정이 **바뀔 때만** 사유와 함께 남긴다(리더 지시: 사용자 신고 "캐릭터가
            // 안 보이다가 클릭하면 나타난다"의 원인 추적 수단이 전혀 없었다). 매 폴링(1.5초)마다 찍으면
            // 로그가 잠기므로 전이 순간만 기록한다 — "언제 숨었고 어느 창 때문이었나"에는 그것으로 충분하다.
            if (verdict != _lastFullscreenVerdict)
            {
                _lastFullscreenVerdict = verdict;
                Debug.Log($"[전체화면판정] {(verdict ? "전체화면 앱 감지 -> 캐릭터를 숨깁니다" : "전체화면 해제 -> 캐릭터를 되돌립니다")} — {reason}");
            }
            return verdict;
        }

        /// <summary>
        /// ============================================================================
        /// ★ 2026-08-29 검증 기록 — "Finder 데스크톱 창을 전체화면으로 오판한다"는 가설은 **반증됐다**
        /// ============================================================================
        /// 리더 가설: 유저가 창을 전부 닫으면 최상단 layer 0 창이 Finder 데스크톱 창이 되고, 그 bounds가
        /// 화면 전체라 이 함수가 true를 반환해 캐릭터가 사라진다.
        ///
        /// 실측(이 머신에서 CoreGraphics로 같은 질의를 직접 재현): Finder 데스크톱 창의 bounds는 확실히
        /// 디스플레이 전체(0,0 1512x982)가 맞지만, 두 겹의 필터에 **각각 독립적으로** 걸려 여기까지
        /// 도달하지 못한다.
        ///   (1) kCGWindowListExcludeDesktopElements — 실측으로 걸러진 4개가 전부 데스크톱 계열이었다:
        ///       Finder(레이어 -2147483603), Dock "Wallpaper-"(-2147483624),
        ///       Window Server "Display 1 Backstop"(-2147483626), Window Server "underbelly"(-2147483602).
        ///   (2) 아래 `layer != 0` 필터 — 그 창들의 layer는 전부 큰 음수라 어차피 통과할 수 없다.
        /// 즉 데스크톱 창은 목록에 애초에 들어오지 않고, 들어와도 걸러진다. 이 경로는 원인이 아니다.
        ///
        /// (진짜 원인은 가출(RunawayState)이었다 — 캐릭터를 숨기고 클릭으로 찾게 하는 스펙터클이 스트레스
        /// 게이지만으로 자율 발동했다. StickConfig.stressRunawayThreshold 문서 참고.)
        ///
        /// 그럼에도 이 진단 로그를 남기는 이유: 다음에 같은 증상이 신고됐을 때 "전체화면 판정 때문인가
        /// 아닌가"를 재빌드 없이 1초 만에 가를 수 있어야 하기 때문이다(이번 조사에서 그 수단이 없어
        /// 가설 하나를 세우는 데 로그 전수를 뒤져야 했다).
        /// </summary>
        private bool EvaluateFullscreen(out string reason)
        {
            IntPtr windowArray = CopyOnScreenWindowList();
            if (windowArray == IntPtr.Zero)
            {
                reason = "창 목록 조회 실패(CGWindowListCopyWindowInfo == null) — 안전하게 '전체화면 아님'으로 처리.";
                return false;
            }

            try
            {
                long count = CFArrayGetCount(windowArray);
                for (long i = 0; i < count; i++)
                {
                    IntPtr windowDict = CFArrayGetValueAtIndex(windowArray, i);
                    if (windowDict == IntPtr.Zero) continue;
                    if (!TryGetInt(windowDict, _keyWindowLayer, out int layer) || layer != 0) continue;

                    // ★★ 2026-09-02 — **투명 보조 창 거부권**. 이 한 줄이 없어서 원칙 2가 네이티브
                    //   전체화면 경로에서 통째로 죽어 있었다.
                    //   macOS는 네이티브 전체화면 창마다 "자동 숨김 타이틀바 컨테이너"를 함께 만든다.
                    //   그 창은 layer 0이면서 z-order상 **본 창보다 앞**이고, 알파가 0이라 눈에는
                    //   보이지 않는다:
                    //       L=0 a=0.0 (0,33,1512, 32)   <- 얘가 먼저 잡혀 기하 불일치 -> return false
                    //       L=0 a=1.0 (0,33,1512,949)   <- 본 창은 영원히 검사되지 않는다
                    //   버그의 본질은 "같은 파일 안에서 **발판 열거 경로에는 있는 알파 필터**가
                    //   전체화면 경로에만 빠져 있었다"는 것이다. 그래서 상수를 새로 만들지 않고
                    //   MinWindowAlpha(발판 경로와 같은 값)를 그대로 재사용한다.
                    //   (기하 불일치 쪽의 return false는 **의도적으로 유지한다** — continue로 바꾸면
                    //    z-order 아무 데나 화면 크기 게임 창이 있기만 하면 숨어서, 전체화면 게임 위에
                    //    작은 창을 띄우고 작업 중일 때 캐릭터가 사라진다.)
                    //   순서: 이 필터가 IsOwnAppWindow보다 **앞**이다. 우리 오버레이의 NSWindow
                    //   alphaValue는 1.0이고(투명은 픽셀 알파지 창 알파가 아니다) 창 레벨도 0이 아니라
                    //   여기 걸리지 않는다. 설령 걸려 건너뛰더라도 그 아래 창을 정상 판정하게 되므로
                    //   결과가 더 정확해지는 방향이다.
                    float alpha = TryGetFloat(windowDict, _keyWindowAlpha, out float rawAlpha) ? rawAlpha : 1f;
                    if (alpha < MinWindowAlpha) continue;

                    // 최상단 일반 창이 우리 자신이면 "다른 전체화면 앱"이 아니다(Win32의
                    // fg == _overlayHwnd 처리와 동일 의도) — 더 탐색하지 않고 즉시 false.
                    if (IsOwnAppWindow(windowDict))
                    {
                        reason = "최상단 일반(layer 0) 창이 우리 앱(같은 이름의 다른 인스턴스 포함)이라 전체화면 앱이 아님.";
                        return false;
                    }

                    string owner = TryGetString(windowDict, _keyWindowOwnerName);
                    if (string.IsNullOrEmpty(owner)) owner = "(이름 없음)";

                    IntPtr boundsDict = CFDictionaryGetValue(windowDict, _keyWindowBounds);
                    if (boundsDict == IntPtr.Zero)
                    {
                        reason = $"최상단 창 '{owner}'의 bounds를 읽지 못함 — 전체화면 아님으로 처리.";
                        return false;
                    }
                    if (!CGRectMakeWithDictionaryRepresentation(boundsDict, out CGRect winRect))
                    {
                        reason = $"최상단 창 '{owner}'의 bounds 파싱 실패 — 전체화면 아님으로 처리.";
                        return false;
                    }

                    // 퇴화 사각형(0폭/0높이)은 판정 근거가 될 수 없다. 위 알파 필터와 같은 성격의
                    // "보이지 않는 창" 거부권이며, 알파를 1로 보고하면서 크기만 0인 보조 창을 받는다.
                    if (winRect.Size.Width <= 0 || winRect.Size.Height <= 0) continue;

                    CGRect displayBounds = CGDisplayBounds(CGMainDisplayID());
                    // 규칙은 플랫폼 중립 파일에 있다(Platform/FullscreenSuspendPolicy.cs) — 네이티브
                    // 전체화면이 상단 시스템 스트립 33pt를 남기는 실측 근거와 반증 기록이 거기 있다.
                    bool match = FullscreenGeometry.CoversDisplay(
                        winRect.Origin.X, winRect.Origin.Y, winRect.Size.Width, winRect.Size.Height,
                        displayBounds.Origin.X, displayBounds.Origin.Y,
                        displayBounds.Size.Width, displayBounds.Size.Height,
                        FullscreenGeometry.Epsilon);

                    if (!match)
                    {
                        reason = $"판정 근거 창 = '{owner}' bounds=({winRect.Origin.X:F0},{winRect.Origin.Y:F0} " +
                            $"{winRect.Size.Width:F0}x{winRect.Size.Height:F0}), 메인 디스플레이=" +
                            $"({displayBounds.Origin.X:F0},{displayBounds.Origin.Y:F0} " +
                            $"{displayBounds.Size.Width:F0}x{displayBounds.Size.Height:F0}) -> 기하 일치=false.";
                        return false;
                    }

                    // ★ 2026-08-31 — 기하만으로는 부족하다. 여기서 "그 앱이 게임인가"까지 확인한다.
                    //   (원인 B: 엑셀/키노트/브라우저 전체화면에서도 캐릭터가 사라지던 문제.)
                    TryGetInt(windowDict, _keyWindowOwnerPID, out int ownerPid);
                    string category = TryGetAppCategory(ownerPid);
                    bool isGame = FullscreenGameCategory.IsGameCategory(category);

                    reason = $"판정 근거 창 = '{owner}'(pid {ownerPid}) bounds=({winRect.Origin.X:F0},{winRect.Origin.Y:F0} " +
                        $"{winRect.Size.Width:F0}x{winRect.Size.Height:F0}) 상단여백 {winRect.Origin.Y - displayBounds.Origin.Y:F0}pt " +
                        $"(허용 {displayBounds.Size.Height * FullscreenGeometry.MenuBarStripFraction:F0}pt) -> 기하 일치=true, " +
                        $"LSApplicationCategoryType={(string.IsNullOrEmpty(category) ? "(미선언)" : category)} -> 게임={isGame}" +
                        (isGame ? "." : " (게임이 아니므로 숨기지 않습니다 — 원칙 2는 '전체화면 게임'만 대상).");
                    return isGame;
                }
                reason = "layer 0(일반 앱) 창이 하나도 없음(전부 최소화 등) — 전체화면 아님으로 안전 처리.";
                return false;
            }
            finally
            {
                CFRelease(windowArray);
            }
        }

        // 위 IsFullscreenAppActive()의 "판정이 바뀔 때만 로그" 상태. 최초 1회는 false에서 시작하므로
        // 앱 시작 직후 정상(비전체화면) 상태에서는 아무 로그도 남지 않는다.
        private bool _lastFullscreenVerdict;

        /// <summary>디바운스 이전의 원시 판정 — "흔들렸지만 흡수했다"를 한 번만 로그로 남기기 위한 상태.</summary>
        private bool _lastRawFullscreenVerdict;

        /// <summary>바뀐 원시 판정이 이만큼 연속 유지되어야 확정한다. 메뉴바 호출로 인한 bounds 요동은
        /// 수백 ms 안에 되돌아오므로 1초면 충분히 흡수되고, 진짜 게임 실행/종료는 1초 늦게 반영돼도
        /// 사람이 눈치채지 못한다(발판 폴링 주기 0.3~0.5초와 같은 자릿수).</summary>
        private const double FullscreenVerdictHoldSeconds = 1.0;

        private FullscreenVerdictDebouncer _fullscreenDebouncer;

        // ============================================================================
        // 전체화면 "게임" 판별 — LSApplicationCategoryType 조회 (2026-08-31)
        // ============================================================================
        // 원칙 2의 문구는 "전체화면 게임 감지 시 자동 숨김"인데 기존 판정은 기하(창 == 화면)만 봤다.
        // 그래서 엑셀/키노트/브라우저 전체화면에서도 캐릭터가 사라졌다. 여기서 전경 창 소유 앱의
        // 번들 Info.plist에 선언된 App Store 카테고리를 **읽기만** 해서 게임 여부를 가른다
        // (NSRunningApplication -> bundleURL -> NSBundle.infoDictionary. 전부 조회 전용 공개 API이고
        //  어떤 권한도 요구하지 않으며, 타 앱의 파일을 수정하지 않는다 — 절대 불변 원칙 3 안전).
        //
        // 판정 규칙(문자열 -> 게임 여부)은 일부러 여기에 두지 않고 FullscreenSuspendPolicy.cs의
        // 순수 함수로 뺐다 — 네이티브 없이 EditMode에서 규칙 자체를 검증할 수 있어야 하기 때문이다.
        //
        // pid_t 마샬링 주의: runningApplicationWithProcessIdentifier:의 인자는 NSInteger가 아니라
        // pid_t = int32다. IntPtr(64비트)로 넘기면 arm64에서 상위 32비트 쓰레기까지 실려 조회가
        // 조용히 실패한다(nil 반환 -> "게임 아님"으로 폴백되어 증상이 눈에 띄지 않는 위험한 실패).
        // 그래서 전용 오버로드 ObjCSendPtrWithInt32를 따로 선언했다.
        private const double AppCategoryCacheSeconds = 30.0;
        private int _cachedCategoryPid = -1;
        private string _cachedCategory;
        private double _cachedCategoryTime = double.NegativeInfinity;
        private bool _categorySelectorsReady;
        private IntPtr _clsRunningApplication;
        private IntPtr _clsBundle;
        private IntPtr _selRunningAppWithPid;
        private IntPtr _selBundleUrl;
        private IntPtr _selBundleWithUrl;
        private IntPtr _selInfoDictionary;

        /// <summary>pid로 앱 카테고리(LSApplicationCategoryType)를 조회한다. 미선언/조회 실패는 null.
        /// 카테고리는 앱이 살아 있는 동안 바뀌지 않지만 pid는 재사용될 수 있어 짧은 만료를 둔다.</summary>
        private string TryGetAppCategory(int pid)
        {
            if (pid <= 0) return null;

            double now = Time.realtimeSinceStartupAsDouble;
            if (pid == _cachedCategoryPid && now - _cachedCategoryTime < AppCategoryCacheSeconds)
            {
                return _cachedCategory;
            }

            string category = QueryAppCategory(pid);
            _cachedCategoryPid = pid;
            _cachedCategory = category;
            _cachedCategoryTime = now;
            return category;
        }

        private string QueryAppCategory(int pid)
        {
            try
            {
                if (!_categorySelectorsReady)
                {
                    _clsRunningApplication = ObjCGetClass("NSRunningApplication");
                    _clsBundle = ObjCGetClass("NSBundle");
                    _selRunningAppWithPid = ObjCSelector("runningApplicationWithProcessIdentifier:");
                    _selBundleUrl = ObjCSelector("bundleURL");
                    _selBundleWithUrl = ObjCSelector("bundleWithURL:");
                    _selInfoDictionary = ObjCSelector("infoDictionary");
                    _categorySelectorsReady = true;
                }
                if (_clsRunningApplication == IntPtr.Zero || _clsBundle == IntPtr.Zero) return null;

                IntPtr app = ObjCSendPtrWithInt32(_clsRunningApplication, _selRunningAppWithPid, pid);
                if (app == IntPtr.Zero) return null;

                // 번들이 없는 프로세스(순수 실행파일)는 bundleURL이 nil이다 — 카테고리도 없다.
                IntPtr url = ObjCSendPtr(app, _selBundleUrl);
                if (url == IntPtr.Zero) return null;

                IntPtr bundle = ObjCSendPtrWithPtr(_clsBundle, _selBundleWithUrl, url);
                if (bundle == IntPtr.Zero) return null;

                // NSDictionary <-> CFDictionary는 toll-free bridge라 이미 선언된 CFDictionaryGetValue를
                // 그대로 쓸 수 있다(objc_msgSend 오버로드를 하나 덜 만든다 = 마샬링 표면적 축소).
                IntPtr info = ObjCSendPtr(bundle, _selInfoDictionary);
                if (info == IntPtr.Zero) return null;

                IntPtr value = CFDictionaryGetValue(info, _keyAppCategoryType);
                if (value == IntPtr.Zero) return null;
                if (CFGetTypeID(value) != CFStringGetTypeID()) return null;
                return CopyCFStringValue(value);
            }
            catch (System.Exception e)
            {
                // 실패 시 null = "미선언" = 숨기지 않음. 판정을 못 했다고 캐릭터를 감추지는 않는다.
                Debug.LogWarning($"[전체화면판정] pid {pid}의 앱 카테고리를 읽지 못했습니다" +
                    $"({e.GetType().Name}) — 게임이 아닌 것으로 간주해 숨기지 않습니다.");
                return null;
            }
        }

        // ============================================================================
        // IDockMetricsService — Dock 실측
        // (2026-08-29 1차 "지금도 독이랑 계속 겹쳐" / 2차 "지금도 제대로 바닥과 독을 제대로 인식 못하는거 같음")
        // ============================================================================

        // 이 조회는 발판 폴링마다(EnumerateFootholds가 한 번 돌 때 2회) 불린다. CFPreferences 조회 6종 +
        // NSWorkspace 앱 열거를 그 빈도로 돌릴 이유가 없어 짧게 캐시한다. 캐시 유효기간은 "앱을 켰을 때
        // Dock이 넓어지는 것을 사람이 눈치채기 전에 따라잡는" 수준이면 충분하다.
        private const double DockMetricsCacheSeconds = 0.75;
        private DockMetrics _cachedDockMetrics;
        private bool _cachedDockMetricsValid;
        private double _cachedDockMetricsTime = double.NegativeInfinity;

        /// <summary>
        /// com.apple.dock 설정과 NSWorkspace 실행 앱 목록을 **읽기 전용**으로 조회해 Dock 발판 기하의
        /// 입력을 만든다. 근거/실측/유도/한계는 Platform/IDockMetricsService.cs의 인터페이스 문서에
        /// 전부 적어뒀다(왜 Dock 창 bounds를 못 쓰는지에 대한 전수 조사 결과 포함).
        ///
        /// 읽는 키와 미설정 시 macOS 기본값:
        ///   · orientation    (CFString) : "bottom"/"left"/"right".  미설정 -> "bottom"
        ///   · autohide       (CFBoolean): 자동 숨김.                미설정 -> false
        ///   · tilesize       (CFNumber) : 아이콘 한 변(pt).         미설정 -> 48
        ///   · persistent-apps / persistent-others (CFArray) : 고정된 앱/기타 타일.
        ///   · show-recents   (CFBoolean): 최근 사용 앱 표시.        미설정 -> true
        ///   · recent-apps    (CFArray)  : 최근 사용 앱 타일.
        ///
        /// ★ 직전 라운드와의 결정적 차이 — 타일 개수를 **센다**.
        /// 예전에는 "실행 중이지만 고정돼 있지 않은 앱"을 셀 방법이 없다고 보고 호출부가 상수
        /// (dockExtraRunningAppTileEstimate = 6)를 더했고, 그 결과 Dock을 좌우 각 77pt 넓게 잡아
        /// "Dock 없는 자리에서 캐릭터가 부양"하는 이번 신고가 나왔다. 이제 그 집합을 NSWorkspace로
        /// 직접 열거한다(activationPolicy == Regular인 앱이 곧 Dock 타일이 생기는 앱의 정의).
        /// 성공하면 DockMetrics.IsTileCountExact = true로 알려서 호출부가 그 상수 보정을 **끄게** 한다.
        /// </summary>
        public bool TryGetDockMetrics(out DockMetrics metrics)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (_cachedDockMetricsValid && now - _cachedDockMetricsTime < DockMetricsCacheSeconds)
            {
                metrics = _cachedDockMetrics;
                return true;
            }

            metrics = default;
            IntPtr appId = IntPtr.Zero;
            var keys = new System.Collections.Generic.List<IntPtr>();
            try
            {
                appId = CFStringCreateWithCString(IntPtr.Zero, "com.apple.dock", kCFStringEncodingUTF8);
                if (appId == IntPtr.Zero) return false;

                IntPtr MakeKey(string name)
                {
                    IntPtr k = CFStringCreateWithCString(IntPtr.Zero, name, kCFStringEncodingUTF8);
                    if (k != IntPtr.Zero) keys.Add(k);
                    return k;
                }

                // 미설정이면 CFPreferencesCopyAppValue가 null을 주므로, 각 항목마다 macOS 기본값으로 둔다.
                bool isBottom = true;
                string orientation = CopyPrefString(MakeKey("orientation"), appId);
                if (!string.IsNullOrEmpty(orientation))
                {
                    isBottom = orientation == "bottom";
                }

                bool autoHide = CopyPrefBool(MakeKey("autohide"), appId, defaultValue: false);
                float tileSize = CopyPrefNumber(MakeKey("tilesize"), appId, defaultValue: 48f);

                // 고정 앱: 개수뿐 아니라 **번들 ID**까지 읽는다 — 아래에서 실행 중 앱 목록과 합집합을
                // 만들 때 "고정돼 있으면서 실행 중"인 앱을 두 번 세지 않기 위해서다(이 중복 제거가
                // 빠지면 타일을 과다 계상해 다시 Dock을 넓게 잡는다 = 이번 신고의 재발).
                var pinnedIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                int persistentApps = CopyPrefTileBundleIds(MakeKey("persistent-apps"), appId, pinnedIds);
                int persistentOthers = CopyPrefArrayCount(MakeKey("persistent-others"), appId);

                bool showRecents = CopyPrefBool(MakeKey("show-recents"), appId, defaultValue: true);
                // "최근 사용" 구획과 "실행 중이지만 고정 안 됨" 구획은 Dock에서 **같은 구획**이고 서로
                // 겹친다(최근 목록의 앱이 지금 실행 중일 수 있다). 그래서 개수를 더하는 게 아니라
                // 번들 ID 합집합의 크기를 쓴다.
                var recentsSection = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                if (showRecents) CopyPrefTileBundleIds(MakeKey("recent-apps"), appId, recentsSection);

                bool exact = TryAppendRunningRegularApps(recentsSection, pinnedIds);

                // Finder(+1)는 항상 있고 persistent-apps에 들어 있지 않다. 휴지통(+1)도 마찬가지다.
                int tileCount = 1 + persistentApps + persistentOthers + recentsSection.Count + 1;

                // 구분선: [Finder+고정앱] | [최근/실행중] | [기타스택+휴지통]. 가운데 구획이 비면 1개로 준다.
                int separatorCount = recentsSection.Count > 0 ? 2 : 1;

                metrics = new DockMetrics(isBottom, autoHide, tileSize, tileCount, separatorCount, exact);
                _cachedDockMetrics = metrics;
                _cachedDockMetricsValid = true;
                _cachedDockMetricsTime = now;
                return true;
            }
            catch (System.Exception e)
            {
                // 설정 조회 실패는 치명적이지 않다 — 호출부가 고정 비율 폴백으로 되돌아간다.
                Debug.LogWarning($"[Dock실측] com.apple.dock 설정을 읽지 못했습니다({e.GetType().Name}) — " +
                    "StickConfig.dockFootholdWidthFraction 고정 추정으로 폴백합니다.");
                return false;
            }
            finally
            {
                for (int i = 0; i < keys.Count; i++) CFRelease(keys[i]);
                if (appId != IntPtr.Zero) CFRelease(appId);
            }
        }

        /// <summary>
        /// com.apple.dock의 타일 배열(persistent-apps / recent-apps)에서 각 타일의
        /// tile-data -> bundle-identifier 문자열을 꺼내 <paramref name="into"/>에 담는다.
        /// 반환값은 **배열 원소 수**(번들 ID가 없는 타일 — 예: 폴더/URL 타일 — 도 자리를 차지하므로
        /// 개수는 개수대로 세야 한다). 번들 ID는 중복 제거용이고 개수는 폭 계산용이라 둘 다 필요하다.
        /// </summary>
        private int CopyPrefTileBundleIds(IntPtr key, IntPtr appId, System.Collections.Generic.HashSet<string> into)
        {
            if (key == IntPtr.Zero) return 0;
            IntPtr v = CFPreferencesCopyAppValue(key, appId);
            if (v == IntPtr.Zero) return 0;
            IntPtr tileDataKey = IntPtr.Zero;
            IntPtr bundleKey = IntPtr.Zero;
            try
            {
                if (CFGetTypeID(v) != CFArrayGetTypeID()) return 0;
                int count = (int)CFArrayGetCount(v);
                if (count <= 0) return 0;

                tileDataKey = CFStringCreateWithCString(IntPtr.Zero, "tile-data", kCFStringEncodingUTF8);
                bundleKey = CFStringCreateWithCString(IntPtr.Zero, "bundle-identifier", kCFStringEncodingUTF8);
                if (tileDataKey == IntPtr.Zero || bundleKey == IntPtr.Zero) return count;

                for (int i = 0; i < count; i++)
                {
                    // CFArrayGetValueAtIndex/CFDictionaryGetValue는 Get 규칙(소유권 없음) — 해제하지 않는다.
                    IntPtr tile = CFArrayGetValueAtIndex(v, i);
                    if (tile == IntPtr.Zero || CFGetTypeID(tile) != CFDictionaryGetTypeID()) continue;
                    IntPtr tileData = CFDictionaryGetValue(tile, tileDataKey);
                    if (tileData == IntPtr.Zero || CFGetTypeID(tileData) != CFDictionaryGetTypeID()) continue;
                    IntPtr bundle = CFDictionaryGetValue(tileData, bundleKey);
                    if (bundle == IntPtr.Zero || CFGetTypeID(bundle) != CFStringGetTypeID()) continue;
                    string id = CopyCFStringValue(bundle);
                    if (!string.IsNullOrEmpty(id)) into.Add(id);
                }
                return count;
            }
            finally
            {
                if (tileDataKey != IntPtr.Zero) CFRelease(tileDataKey);
                if (bundleKey != IntPtr.Zero) CFRelease(bundleKey);
                CFRelease(v); // CFPreferencesCopyAppValue는 Copy 규칙.
            }
        }

        /// <summary>
        /// ★ Dock 폭 공식의 마지막 미지수를 없애는 함수 — "지금 Dock에 타일이 생겨 있는 앱" 중
        /// 고정되지 않은 것들의 번들 ID를 <paramref name="into"/>에 합친다(합집합이므로 최근 사용
        /// 목록과 겹쳐도 중복되지 않는다).
        ///
        /// 정의상 정확하다: NSApplicationActivationPolicyRegular(=0)는 "Dock에 나타나고 메뉴바를 갖는
        /// 앱"이라는 뜻이고, LSUIElement 같은 백그라운드/에이전트 앱은 Accessory(1)/Prohibited(2)라
        /// 자동으로 빠진다. 직전 라운드가 대안으로 검토했다 실패한 "CGWindowList의 고유 소유자 이름
        /// 세기"와 달리 **창이 하나도 열려 있지 않은 앱도 정확히 포함**된다(그 앱도 Dock 타일은 있다).
        ///
        /// Finder는 제외한다 — Dock 맨 왼쪽 고정 타일로 이미 +1 세고 있고, 여기서 또 세면 두 번 센다.
        /// 우리 자신(StickMate)은 **일부러 제외하지 않는다**: 우리도 .regular 앱이라 실제로 Dock에
        /// 타일이 하나 생기고(실측 스크린샷에서 확인), 그 타일도 Dock 폭을 실제로 넓히기 때문이다.
        /// </summary>
        /// <returns>열거에 성공했으면 true. false면 호출부가 "모르는 만큼"의 상수 보정을 되살려야 한다.</returns>
        private bool TryAppendRunningRegularApps(System.Collections.Generic.HashSet<string> into,
            System.Collections.Generic.HashSet<string> pinnedIds)
        {
            try
            {
                IntPtr workspaceClass = ObjCGetClass("NSWorkspace");
                if (workspaceClass == IntPtr.Zero) return false;   // AppKit 미로드(배치 모드 등).

                IntPtr workspace = ObjCSendPtr(workspaceClass, ObjCSelector("sharedWorkspace"));
                if (workspace == IntPtr.Zero) return false;

                IntPtr apps = ObjCSendPtr(workspace, ObjCSelector("runningApplications"));
                if (apps == IntPtr.Zero) return false;

                IntPtr selCount = ObjCSelector("count");
                IntPtr selObjectAtIndex = ObjCSelector("objectAtIndex:");
                IntPtr selPolicy = ObjCSelector("activationPolicy");
                IntPtr selBundleId = ObjCSelector("bundleIdentifier");
                if (selCount == IntPtr.Zero || selObjectAtIndex == IntPtr.Zero
                    || selPolicy == IntPtr.Zero || selBundleId == IntPtr.Zero) return false;

                long count = ObjCSendNInt(apps, selCount).ToInt64();
                // 방어: 말도 안 되는 값이면(포인터를 정수로 오독했을 때의 증상) 조용히 포기한다.
                if (count < 0 || count > 4096) return false;

                for (long i = 0; i < count; i++)
                {
                    IntPtr app = ObjCSendPtrWithNUInt(apps, selObjectAtIndex, new IntPtr(i));
                    if (app == IntPtr.Zero) continue;
                    if (ObjCSendNInt(app, selPolicy).ToInt64() != NSApplicationActivationPolicyRegular) continue;

                    // NSString은 CFString과 toll-free bridged라 기존 CF 변환 헬퍼를 그대로 쓸 수 있다.
                    IntPtr bundleId = ObjCSendPtr(app, selBundleId);
                    if (bundleId == IntPtr.Zero) continue;
                    string id = CopyCFStringValue(bundleId);
                    if (string.IsNullOrEmpty(id)) continue;
                    if (id == "com.apple.finder") continue;        // 맨 왼쪽 고정 타일로 이미 셌다.
                    if (pinnedIds.Contains(id)) continue;          // 고정 타일로 이미 셌다.
                    into.Add(id);
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Dock실측] 실행 중 앱 목록 조회에 실패했습니다({e.GetType().Name}) — " +
                    "타일 개수를 정확히 세지 못하므로 StickConfig.dockExtraRunningAppTileEstimate 보정으로 폴백합니다.");
                return false;
            }
        }

        /// <summary>CFString(또는 toll-free bridged NSString)을 managed 문자열로. 소유권은 건드리지 않는다.</summary>
        private string CopyCFStringValue(IntPtr cfString)
        {
            if (cfString == IntPtr.Zero) return null;
            if (!CFStringGetCString(cfString, _ownerNameBuffer, _ownerNameBuffer.Length, kCFStringEncodingUTF8)) return null;
            int len = Array.IndexOf(_ownerNameBuffer, (byte)0);
            if (len < 0) len = _ownerNameBuffer.Length;
            return System.Text.Encoding.UTF8.GetString(_ownerNameBuffer, 0, len);
        }

        /// <summary>CFPreferences에서 CFString 값을 읽는다(없으면 null). 반환된 CFTypeRef는 Copy 규칙이라 해제한다.</summary>
        private string CopyPrefString(IntPtr key, IntPtr appId)
        {
            if (key == IntPtr.Zero) return null;
            IntPtr v = CFPreferencesCopyAppValue(key, appId);
            if (v == IntPtr.Zero) return null;
            try
            {
                if (CFGetTypeID(v) != CFStringGetTypeID()) return null;
                // CFString -> managed. TryGetString()과 같은 변환 규칙(버퍼 재사용, UTF-8, NUL 절단).
                if (!CFStringGetCString(v, _ownerNameBuffer, _ownerNameBuffer.Length, kCFStringEncodingUTF8)) return null;
                int len = Array.IndexOf(_ownerNameBuffer, (byte)0);
                if (len < 0) len = _ownerNameBuffer.Length;
                return System.Text.Encoding.UTF8.GetString(_ownerNameBuffer, 0, len);
            }
            finally { CFRelease(v); }
        }

        private bool CopyPrefBool(IntPtr key, IntPtr appId, bool defaultValue)
        {
            if (key == IntPtr.Zero) return defaultValue;
            IntPtr v = CFPreferencesCopyAppValue(key, appId);
            if (v == IntPtr.Zero) return defaultValue;
            try
            {
                if (CFGetTypeID(v) == CFBooleanGetTypeID()) return CFBooleanGetValue(v);
                // 일부 설정은 0/1 숫자로 저장돼 있다.
                if (CFGetTypeID(v) == CFNumberGetTypeID() && CFNumberGetValue(v, kCFNumberSInt32Type, out int n)) return n != 0;
                return defaultValue;
            }
            finally { CFRelease(v); }
        }

        private float CopyPrefNumber(IntPtr key, IntPtr appId, float defaultValue)
        {
            if (key == IntPtr.Zero) return defaultValue;
            IntPtr v = CFPreferencesCopyAppValue(key, appId);
            if (v == IntPtr.Zero) return defaultValue;
            try
            {
                if (CFGetTypeID(v) != CFNumberGetTypeID()) return defaultValue;
                if (CFNumberGetValueDouble(v, kCFNumberFloat64Type, out double d)) return (float)d;
                return defaultValue;
            }
            finally { CFRelease(v); }
        }

        private int CopyPrefArrayCount(IntPtr key, IntPtr appId)
        {
            if (key == IntPtr.Zero) return 0;
            IntPtr v = CFPreferencesCopyAppValue(key, appId);
            if (v == IntPtr.Zero) return 0;
            try
            {
                if (CFGetTypeID(v) != CFArrayGetTypeID()) return 0;
                return (int)CFArrayGetCount(v);
            }
            finally { CFRelease(v); }
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

        /// <summary>오른쪽 버튼 — 왼쪽과 동일한 조회 API, 버튼 번호만 다르다(캐릭터 우클릭 제어 메뉴용).</summary>
        public bool TryGetSecondaryButtonPressed(out bool pressed)
        {
            pressed = CGEventSourceButtonState(kCGEventSourceStateCombinedSessionState, kCGMouseButtonRight);
            return true;
        }

        // ============================================================================
        // IGlobalKeyStateService — 창 포커스와 무관한 전역 단축키 조회
        // (Platform/IGlobalKeyStateService.cs 문서의 "왜 필요한가"/"권한에 대하여" 참고)
        // ============================================================================

        /// <summary>
        /// CGEventSourceKeyState로 지금 그 키가 눌려 있는지 조회한다. TryGetPrimaryButtonPressed와
        /// 완전히 같은 성격의 순수 조회이며 어떤 이벤트도 주입하지 않는다. 지원 키는
        /// Platform.GlobalKey 열거형에 열거된 것뿐이다(그 문서의 "왜 필요한 것만 있는가" 참고).
        /// </summary>
        public bool TryGetKeyPressed(GlobalKey key, out bool pressed)
        {
            ushort code;
            switch (key)
            {
                case GlobalKey.Command: code = kVK_Command; break;
                case GlobalKey.Option:  code = kVK_Option;  break;
                case GlobalKey.Control: code = kVK_Control; break;
                case GlobalKey.Q:       code = kVK_ANSI_Q;  break;
                case GlobalKey.C:       code = kVK_ANSI_C;  break;
                case GlobalKey.D:       code = kVK_ANSI_D;  break;
                case GlobalKey.R:       code = kVK_ANSI_R;  break;
                case GlobalKey.B:       code = kVK_ANSI_B;  break;
                case GlobalKey.K:       code = kVK_ANSI_K;  break;
                case GlobalKey.G:       code = kVK_ANSI_G;  break;
                case GlobalKey.T:       code = kVK_ANSI_T;  break;
                case GlobalKey.X:       code = kVK_ANSI_X;  break;
                case GlobalKey.H:       code = kVK_ANSI_H;  break;
                case GlobalKey.S:       code = kVK_ANSI_S;  break;
                case GlobalKey.N:       code = kVK_ANSI_N;  break;
                case GlobalKey.J:       code = kVK_ANSI_J;  break;
                case GlobalKey.F:       code = kVK_ANSI_F;  break;
                case GlobalKey.A:       code = kVK_ANSI_A;  break;
                case GlobalKey.I:       code = kVK_ANSI_I;  break;
                case GlobalKey.P:       code = kVK_ANSI_P;  break;
                default:
                    pressed = false;
                    return false;
            }

            pressed = CGEventSourceKeyState(kCGEventSourceStateCombinedSessionState, code);
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

            var frameRect = new Rect((float)rect.Origin.X, (float)rect.Origin.Y,
                (float)rect.Size.Width, (float)rect.Size.Height);

            // ★★ 2026-09-01 — kCGWindowBounds는 **frame** 사각형이다(타이틀바 포함). Unity가 그리는 것은
            //    **콘텐츠** 사각형이므로, 창이 아직 보더리스가 아닌 동안에는 원점이 28pt 위로/높이가
            //    28pt 크게 보고되어 좌표계가 그만큼 어긋난다. 우리 자신의 **기동 2.3초 구간**이 실측으로
            //    확인된 그 구간이다(로그 26번째 줄: 창=(0,33,1512,1010), 같은 순간 clientSize=(1512,982)).
            //    판정은 플랫폼 중립 규칙 한 곳에 있다 — 근거와 산술은 OverlayContentRectPolicy 문서 참고.
            //    Windows판은 같은 부류를 이미 TryGetVisualWindowRect(DWM 확장 프레임)로 막고 있었다.
            Vector2 contentSize = _overlayContentSizeThisPass;
            bool contentSizeFromLibrary = contentSize.x > 0f && contentSize.y > 0f;
            if (!contentSizeFromLibrary)
            {
                // 부착 전(clientSize=(0,0))에는 백버퍼로 유도한다 — 기동 직후 몇 초를 틀린 원점으로
                // 보내지 않기 위해서다(근거는 정책 문서의 항등식).
                OverlayContentRectPolicy.TryDeriveContentSizeFromBackbuffer(
                    frameRect, Screen.width, Screen.height, out contentSize);
            }

            bool decorationStripped = OverlayContentRectPolicy.TryStripTopDecoration(
                frameRect, contentSize, OverlayContentRectPolicy.DefaultEpsilonPoints,
                out Rect osRect, out float strippedTopPoints);

            // ★ 진동(A↔B↔A↔B) 감시. 불감대는 1px 래칫만 막고 A/B 진동은 못 막는다(둘 다 불감대 밖이면
            //   영원히 계속된다) — 그래서 별도 안전장치를 플랫폼 중립 위치에 두고 양쪽이 호출한다.
            //   여기서 우리가 창을 재적용하지는 않으므로 "멈출" 대상은 없지만, **증거를 한 번 남긴다**.
            //   2026-09-01에 관측된 교대의 유력한 경로(같은 이름의 두 번째 인스턴스 창이 이름 폴백으로
            //   '내 창'이 되던 것)는 이번 라운드에 IsSelfProcessWindow 분리로 막혔다. 그래도 이 줄이
            //   다시 찍히면 **다른 원인이 남아 있다는 뜻**이므로, 그때의 상태를 통째로 남겨 둔다.
            if (_overlayRectOscillation.Observe(frameRect, OverlayContentRectPolicy.DefaultEpsilonPoints))
            {
                LogOverlayOscillationEvidence();
            }

            bool originMoved = Vector2.Distance(osRect.position, ScreenCoordinateConverter.OverlayOriginOsScreen) > 0.5f;

            // ★ 원점과 DPI 배율을 **한 번의 관측**으로 함께 보고한다(2026-08-29 Retina 대응 라운드).
            // 배율 = 창 폭(OS 포인트) / Screen.width(Unity 픽셀). 폴링마다 재측정되므로 창이 리사이즈되거나
            // 다른 배율의 모니터로 옮겨져도 자동으로 따라간다 — 하드코딩 0.5가 아닌 이유가 이것이다.
            // ★ 2026-09-01 — 원점 위생 검사(신고 "창에서 가끔 갑자기 떨어짐"의 근본 원인 3).
            // 화면 밖으로 대부분 빠져나간 사각형은 창 애니메이션 도중의 일시적 오독이므로 버리고
            // 직전 유효값을 유지한다. 판정/영구고착 방지는 ScreenCoordinateConverter가 담당한다.
            if (_hasDisplayBoundsThisPass)
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(osRect, _displayBoundsThisPass);
            }
            else
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(osRect);
            }

            // ★★ 2026-09-01 계측 정직성 수정 — 이 줄은 **거부된 보고까지 "갱신"으로 찍고 있었다.**
            //   실기 로그(/tmp/stickmate-run/stickmate.log)에서 정확히 이 모양이 21번 나왔다:
            //       874: [원점위생] ... 버렸습니다 — 보고=(x:-1007 ...), 유지 중인 원점=(0.00, 0.00)
            //       875: [MacWindowService] ... 갱신 — origin=(-1007.00, 0.00) ...   <- 갱신된 적 없다
            //   그 결과 로그만 읽은 사람은 "원점이 -1007로 튀었다"고 읽는다(장시간 페르소나가 보고한
            //   93/135/.../732pt 목록에 거부된 값이 섞여 들어간 직접 원인이다). 좌표계의 **진실**은
            //   ScreenCoordinateConverter.OverlayOriginOsScreen 하나뿐이므로 그 값을 찍고, 이번 보고가
            //   반영됐는지 여부를 같은 줄에 명시한다.
            //   ★ 거부된 보고는 여기서 **아예 찍지 않는다**(침묵이 아니다 — 거부는 [원점위생]이
            //     연속 1,2,4,8...회째로 이미 남긴다). 이 줄까지 거부마다 찍으면 전체화면 전환 한 번에
            //     같은 사건이 두 태그로 두 번씩 쌓인다(24시간 상주 앱 — 로그 예산을 늘리지 않는다).
            Vector2 effectiveOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            bool reportAccepted = Vector2.Distance(effectiveOrigin, osRect.position) <= 0.5f;
            if (!reportAccepted && _overlayOriginLogged) return;
            if (!_overlayOriginLogged || originMoved
                || Mathf.Abs(ScreenCoordinateConverter.AutoDpiScale - _lastLoggedDpiScale) > 0.01f)
            {
                _overlayOriginLogged = true;
                _lastLoggedDpiScale = ScreenCoordinateConverter.AutoDpiScale;
                Debug.Log($"[MacWindowService] 오버레이 창 원점/배율(Quartz 좌표) " +
                    $"{(reportAccepted ? "갱신" : "보고 **거부됨**(위생 검사) — 좌표계는 직전 값 유지")} — " +
                    $"실효 원점={effectiveOrigin}, 이번 보고={osRect.position}, " +
                    $"size=({osRect.width}x{osRect.height}), Screen=({Screen.width}x{Screen.height}) " +
                    $"-> desktopDpiScale(자동)={ScreenCoordinateConverter.AutoDpiScale:F3}. " +
                    // ★ frame(OS가 준 원본)과 content(우리가 쓰는 값)를 항상 나란히 남긴다. 둘이 다르면
                    //   그 순간 창에 타이틀바가 붙어 있었다는 뜻이고, 그것이 좌표 어긋남의 유일한 실측 단서다.
                    $"원본 frame=({frameRect.x},{frameRect.y} {frameRect.width}x{frameRect.height}), " +
                    $"콘텐츠크기={contentSize}({(contentSizeFromLibrary ? "라이브러리 clientSize" : "백버퍼 유도")}), " +
                    $"창장식 제거={(decorationStripped ? $"예(위 {strippedTopPoints:F0}pt — 타이틀바)" : "아니오(보더리스)")}. " +
                    "이 두 값이 커서<->월드 변환의 오프셋/배율 보정에 쓰입니다(ScreenCoordinateConverter).");
            }
        }

        // ============================================================================
        // ILocalClickCaptureService — 소유권/영역 부기(LocalClickCaptureGate에 위임)
        // ============================================================================
        //
        // ★ 사용자 신고 "마우스로 안 잡힘"의 진짜 원인 (2026-08-28, 리더가 Player.log로 특정)
        // ----------------------------------------------------------------------------
        // 직전 라운드까지 이 클래스는 ILocalClickCaptureService를 "실제 OS 히트테스트는 UniWindowController가
        // 하니까 부기는 필요 없다"는 이유로 **의도적으로 구현하지 않았다.** 그런데 이 서비스는 항상
        // FallbackPlatformWindowService 데코레이터로 감싸여 소비되고, 그 데코레이터는:
        //     ILocalClickCaptureService를 **자기가 구현**하면서 내부 서비스에 위임한다
        //     -> _innerClickCapture = (inner as ILocalClickCaptureService) == null (여기 미구현이므로)
        //     -> RequestLocalClickCapture(...)가 **항상 false**를 반환
        // 그래서 DragThrowController.OnMouseDown()의
        //     if (_clickCapture != null && !_clickCapture.RequestLocalClickCapture(...)) { 락 반환; return; }
        // 분기가 **매번 성립**했다. `_clickCapture`는 데코레이터라 non-null인데 요청은 false이므로,
        // 클릭은 정상 감지되는데도 ChangeState(Dragged)에 도달하지 못하고 조용히 되돌아간 것이다
        // (Player.log에 MouseDown/MouseUp만 찍히고 Dragged 전이가 전혀 없던 이유).
        //
        // 수정: Win32WindowService/NullPlatformWindowService와 **완전히 동일한 방식**으로 공용
        // LocalClickCaptureGate에 위임해 부기를 구현한다. 이는 리더가 범위 밖으로 지정한
        // "isHitTestEnabled로 ILocalClickCaptureService를 대체하는 리팩터링"이 아니라, 다른 플랫폼이
        // 이미 갖고 있던 동일 부기를 macOS에도 채워 넣어 데코레이터 계약을 만족시키는 것이다.
        // 실제 OS 히트테스트는 여전히 UniWindowController(hitTestType=Raycast)가 담당하며 이 부기와
        // 서로 간섭하지 않는다(ILocalClickCaptureService.cs 문서 상단 "핵심 한계" 참고).
        private readonly LocalClickCaptureGate _clickCaptureGate = new LocalClickCaptureGate();

        public bool RequestLocalClickCapture(Rect hitboxOsScreen, object owner)
            => _clickCaptureGate.TryRequestCapture(hitboxOsScreen, owner);

        public void UpdateLocalClickCaptureRegion(Rect hitboxOsScreen, object owner)
            => _clickCaptureGate.UpdateRegion(hitboxOsScreen, owner);

        public void ReleaseLocalClickCapture(object owner)
            => _clickCaptureGate.ReleaseCapture(owner);

        public bool IsLocalClickCaptureOwnedBy(object owner)
            => _clickCaptureGate.IsOwnedBy(owner);

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
                    if (!IsSelfProcessWindow(windowDict)) continue;   // 좌표계 출처 — PID 단독.

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

        /// <summary>
        /// 발판 폴링을 기다리지 않고 <b>지금 이 프레임에</b> 오버레이 창의 OS 사각형을 재측정해
        /// ScreenCoordinateConverter에 반영한다. MacOverlayStateEnforcer가 전체화면 재적합
        /// (최초 확장 / 해상도 변경 후 재무장)을 끝낸 직후 호출한다.
        ///
        /// 왜 즉시여야 하는가: 재적합은 창의 크기와 원점을 동시에 바꾼다. 그 사이 컨버터가 옛 원점/배율을
        /// 들고 있으면 커서<->월드 변환과 발판 좌표가 한 화면만큼 어긋나 캐릭터가 화면 밖으로 튄다.
        /// CGWindowListCopyWindowInfo 한 번짜리 순수 조회이며(어떤 창도 건드리지 않는다) 재적합
        /// 성공 시에만 불리므로 상주 비용이 없다.
        /// </summary>
        internal void ReportOverlayRectNow()
        {
            if (!TryGetSelfWindowRect(out Rect selfFrameRect)) return;
            if (selfFrameRect.width <= 0f || selfFrameRect.height <= 0f) return;

            // ★ 2026-09-01 — CaptureOverlayOrigin()과 **같은 규칙**으로 창 장식(타이틀바)을 걷어낸다.
            //   두 경로가 서로 다른 사각형을 보고하면 폴링 한 주기마다 좌표계가 28pt씩 튄다.
            OverlayContentRectPolicy.TryStripTopDecoration(
                selfFrameRect, ResolveOverlayContentSize(selfFrameRect),
                OverlayContentRectPolicy.DefaultEpsilonPoints, out Rect selfRect, out _);

            // 열거 패스 밖의 단발성 경로라 디스플레이 경계를 여기서 한 번 더 읽는다(순수 조회).
            if (TryGetMainDisplayBounds(out Rect display))
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(selfRect, display);
                return;
            }
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(selfRect);
        }

        /// <summary>
        /// 라이브러리가 보고하는 우리 창의 <b>콘텐츠(클라이언트) 크기</b>(OS 포인트). 부착 전에는 (0,0)이고,
        /// 그 경우 <see cref="OverlayContentRectPolicy"/>가 보정을 포기한다("모르면 건드리지 않는다").
        ///
        /// <para>왜 clientSize인가(실측): 기동 로그에서 CGWindow frame이 1512x<b>1010</b>이던 바로 그 순간
        /// 라이브러리는 <c>windowSize=clientSize=(1512,982)</c>를 보고했다. 즉 이 값은 타이틀바를 뺀
        /// 콘텐츠 크기이며, frame과의 차이 28pt가 곧 타이틀바다.</para>
        /// </summary>
        private Vector2 ReadControllerContentSize()
        {
            var controller = Controller;
            return controller != null ? controller.clientSize : Vector2.zero;
        }

        /// <summary>
        /// 열거 패스 밖의 단발성 경로(<see cref="ReportOverlayRectNow"/>/<see cref="DetectDesktopDpiScale"/>)가
        /// 쓰는 콘텐츠 크기 해석. 라이브러리 값이 먼저이고, 부착 전이면 백버퍼로 유도한다
        /// (열거 패스 안과 <b>같은 순서</b> — 두 경로가 다른 답을 내면 폴링마다 좌표계가 튄다).
        /// </summary>
        private Vector2 ResolveOverlayContentSize(Rect frameRect)
        {
            Vector2 size = ReadControllerContentSize();
            if (size.x > 0f && size.y > 0f) return size;
            OverlayContentRectPolicy.TryDeriveContentSizeFromBackbuffer(
                frameRect, Screen.width, Screen.height, out size);
            return size;
        }

        /// <summary>
        /// 오버레이 창 사각형이 두 값 사이를 오간다고 <b>확정된 순간</b>에 딱 한 번 부르는 증거 수집.
        ///
        /// <para><b>왜 이 로그가 필요한가(정직하게)</b>: 이 라운드에서 <b>확정된 것</b>은 "두 사각형이
        /// 같은 창의 frame이고 B는 타이틀바가 붙은 상태"까지다(산술 대조로 확정 — OverlayContentRectPolicy
        /// 문서). <b>확정되지 않은 것</b>은 "누가 창을 보더리스에서 빼는가"다. 우리 재적합 루프는 실기
        /// 로그에서 <b>단 1회</b> 실행되고 끝났으므로(전체화면 확장 시도 1/6, 재무장 0회) 우리 코드가
        /// 창을 다시 만지고 있지는 않다. 그래서 추측으로 고치는 대신, 확정 순간의 상태를 전부 남겨
        /// 다음 실기 1회로 범인이 갈리게 한다(가설은 클래스 하단 주석 참고).</para>
        /// </summary>
        private void LogOverlayOscillationEvidence()
        {
            var controller = Controller;
            string controllerState = controller == null
                ? "UniWindowController=없음"
                : $"isTransparent={controller.isTransparent}, isTopmost={controller.isTopmost}, " +
                  $"isClickThrough={controller.isClickThrough}, isHitTestEnabled={controller.isHitTestEnabled}, " +
                  $"isFreePositioningEnabled={controller.isFreePositioningEnabled}, " +
                  $"isZoomed={controller.isZoomed}, shouldFitMonitor={controller.shouldFitMonitor}, " +
                  $"windowSize={controller.windowSize}, clientSize={controller.clientSize}, " +
                  $"windowPosition={controller.windowPosition}";

            Debug.LogWarning("[MacWindowService] ★오버레이 창 기하 진동 확정 — " + _overlayRectOscillation.Diagnosis +
                $" | 지금 상태: {controllerState}, Screen=({Screen.width}x{Screen.height}), " +
                $"fullScreenMode={Screen.fullScreenMode}. " +
                "읽는 법: 두 사각형의 높이 차이가 28pt면 그것은 macOS 타이틀바이고, 곧 그 표본이 " +
                "**보더리스가 되기 전의 StickMate 창**이라는 뜻입니다. 그런 창이 우리 것이 아니라면 " +
                "같은 이름의 다른 인스턴스일 수 있습니다(2026-09-01에 실제로 그랬고, 이름 폴백은 " +
                "그 라운드에 제거됐습니다 — IsSelfProcessWindow). 우리 것이라면 SetBorderless/" +
                "SetTransparent 경로나 창 재부착을 의심하십시오. 이 줄은 프로세스당 한 번만 남습니다.");
        }

        /// <summary>
        /// 오버레이 창 사각형의 A↔B 진동 감시기. 판정은 플랫폼 중립 한 곳
        /// (<see cref="OverlayGeometryOscillationGuard"/>)에 있고 여기서는 관측만 한다 —
        /// Windows판 Enforcer도 같은 클래스를 쓴다(한쪽만 고쳐지는 이 저장소의 단골 실패 방지).
        /// </summary>
        private readonly OverlayGeometryOscillationGuard _overlayRectOscillation =
            new OverlayGeometryOscillationGuard();

        // 이번 열거 패스에서 읽은 라이브러리 콘텐츠 크기(창 장식 제거용). 패스당 네이티브 조회 1회.
        private Vector2 _overlayContentSizeThisPass;

        // CaptureOverlayOrigin()이 한 열거 패스 안에서 "가장 큰 자기 창"을 고르기 위한 작업 변수.
        private double _overlayOriginPassArea;
        // 이번 열거 패스에서 관측한 주 디스플레이 경계(원점 위생 검사에 넘긴다 — 새 시스템 호출 없음).
        private bool _hasDisplayBoundsThisPass;
        private Rect _displayBoundsThisPass;
        private bool _overlayOriginLogged;
        // 로그를 배율 변화에도 반응시키기 위한 직전 값(로그 스팸 방지 — 0.3초마다 도는 폴링이다).
        private float _lastLoggedDpiScale = -1f;
    }
}
#endif
