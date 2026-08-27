#if UNITY_STANDALONE_OSX
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
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
    /// "바로 바탕화면에서 구동" 라운드(사용자 명시 요청, 2026-08-28) — 지금까지 CreateOverlayWindow()/
    /// SetClickThrough()/SetAlwaysOnTop()가 안전가드(NotSupportedException)로 막혀 있던 이유는 Unity
    /// 에디터 Play 모드의 게임뷰가 에디터 UI 안의 패널일 뿐 실제 OS 창이 아니라서, 진짜 투명/클릭관통
    /// 오버레이를 만들려면 (1) 독립 실행 빌드(Standalone Player)가 있어야 하고 (2) 그 빌드가 만드는 실제
    /// NSWindow를 네이티브 코드로 조작해야 했기 때문이다(CoreGraphics/CoreFoundation 공개 C ABI에는
    /// "쓰기" 수단이 없음 — 비공개 SkyLight API는 금지 대상). 이번 라운드에서 처음으로 실제 Standalone
    /// 빌드(Assets/Editor/BuildStandalone.cs)를 만들었으므로, 그 빌드가 만드는 진짜 NSWindow를 조작하는
    /// Objective-C 네이티브 플러그인(Assets/Plugins/macOS/StickMateOverlayPlugin.m,
    /// StickMateOverlayPlugin.bundle로 컴파일됨)을 추가하고 아래 세 메서드를 그 플러그인 호출로 교체했다.
    /// ============================================================================
    /// - 진짜 구현: EnumerateFootholds(), IsFullscreenAppActive(), ICursorPositionService(전역 커서 조회) —
    ///   기존과 동일하게 CoreGraphics/CoreFoundation 공개 C ABI만 사용하는 순수 조회 동작.
    /// - 신규 진짜 구현: CreateOverlayWindow()/SetClickThrough()/SetAlwaysOnTop() — 이제
    ///   [DllImport("StickMateOverlayPlugin")]로 네이티브 플러그인의 SM_IsMainWindowFound()/
    ///   SM_ConfigureOverlayWindow()를 호출해 실제 NSWindow.ignoresMouseEvents/NSWindow.level을 쓴다.
    ///   대상 창을 못 찾으면(SM_IsMainWindowFound()==0) 조용히 no-op하지 않고 NotSupportedException으로
    ///   즉시 실패를 알린다(이전 라운드들의 컨벤션과 동일 — StickmanAgent.Start()가 이 예외를 잡아 로그로
    ///   남기고 나머지 초기화를 계속하는 기존 처리 경로를 그대로 재사용).
    /// - 이 클래스 자신은 여전히 "다른 프로세스의 창"에는 절대 접근하지 않는다 — 네이티브 플러그인도
    ///   NSApplication.sharedApplication.windows(우리 프로세스 자신의 창 목록)만 순회한다
    ///   (StickMateOverlayPlugin.m 문서 주석 참고).
    ///
    /// ILocalClickCaptureService/IDesktopIconLayoutService는 이번 라운드에도 의도적으로 구현하지 않는다
    /// (요청 범위 밖) — FallbackPlatformWindowService가 `as` 캐스팅으로 null 처리해 안전하게 no-op/실패
    /// 취급하므로 컴파일/런타임 모두 문제 없다(Win32WindowService가 실제로 두 인터페이스 다 구현한 것과
    /// 다른 점 — macOS는 이번 라운드에 그 두 캐퍼빌리티까지는 손대지 않는다).
    /// </summary>
    public sealed class MacWindowService : IPlatformWindowService, ICursorPositionService
    {
        // ============================================================================
        // 네이티브 오버레이 플러그인 P/Invoke 선언(Assets/Plugins/macOS/StickMateOverlayPlugin.m).
        // DllImport 대상 이름은 확장자 없는 번들 이름 — Unity가 Standalone macOS 빌드에 포함시킨
        // StickMateOverlayPlugin.bundle을 찾아 로드한다(에디터에서는 PluginImporter가
        // SetCompatibleWithEditor(false)로 막아뒀으므로 로드되지 않는다 — 애초에 이 클래스 자체가
        // StickmanAgent.CreatePlatformService()의 `UNITY_STANDALONE_OSX && !UNITY_EDITOR` 분기에서만
        // 인스턴스화되므로 에디터에서 호출될 일도 없다).
        // ============================================================================
        private const string OverlayPluginName = "StickMateOverlayPlugin";

        [DllImport(OverlayPluginName)]
        private static extern void SM_ConfigureOverlayWindow(int makeClickThrough, int alwaysOnTop, int transparent);

        [DllImport(OverlayPluginName)]
        private static extern int SM_GetOverlayWindowLevel();

        [DllImport(OverlayPluginName)]
        private static extern int SM_IsMainWindowFound();

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

        [DllImport(CoreGraphicsLib)]
        private static extern uint CGMainDisplayID();

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

        // 클릭관통/항상위의 현재 목표 상태를 기억해둔다. 네이티브 SM_ConfigureOverlayWindow()는 두
        // 속성을 하나의 호출로 동시에 적용하는 단일 함수라(StickMateOverlayPlugin.m 참고),
        // SetClickThrough()/SetAlwaysOnTop()가 서로 독립적으로 호출되어도(IPlatformWindowService 계약상
        // 별개 메서드) 매번 "마지막으로 알려진 두 값 전부"를 함께 넘겨야 한 쪽 호출이 다른 쪽 상태를
        // 조용히 되돌리지 않는다. 투명(transparent)은 토글 개념이 아니라 오버레이의 항상 성립해야 하는
        // 성질이라 별도 상태 없이 항상 1(true)로 넘긴다.
        private bool _clickThroughEnabled;
        private bool _alwaysOnTopEnabled;

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

            IntPtr windowArray = CopyOnScreenWindowList();
            if (windowArray == IntPtr.Zero) return _footholdBuffer; // 조회 실패 — FallbackPlatformWindowService 안전망이 감싸므로 빈 리스트로도 안전.

            try
            {
                long count = CFArrayGetCount(windowArray);
                for (long i = 0; i < count; i++)
                {
                    IntPtr windowDict = CFArrayGetValueAtIndex(windowArray, i);
                    if (windowDict == IntPtr.Zero) continue;

                    // 일반 앱 창(kCGWindowLayer==0)만 채택 — 메뉴바/데스크톱 배경 등 시스템 레이어 제외.
                    // Win32의 "제목 있는 가시 창"(GetWindowTextLength!=0) 필터와 같은 목적의 휴리스틱.
                    if (!TryGetInt(windowDict, _keyWindowLayer, out int layer) || layer != 0) continue;

                    // 이 앱 자신(Unity 플레이어 프로세스)의 창은 발판 후보에서 제외.
                    if (IsSelfWindow(windowDict)) continue;

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
        // 네이티브 플러그인 배선 3종 — 이제 실제로 Objective-C 플러그인(StickMateOverlayPlugin.m)을
        // 호출해 우리 자신의 NSWindow를 조작한다("바로 바탕화면에서 구동" 라운드, 2026-08-28).
        // ============================================================================

        /// <summary>
        /// 네이티브 플러그인이 우리 자신의 Unity Player 메인 창을 실제로 찾을 수 있는지
        /// SM_IsMainWindowFound()로 확인한다. 찾았다면 초기 상태(클릭관통 OFF, 항상위 OFF, 투명 ON)를
        /// 곧바로 적용해둔다 — 클릭관통이 기본으로 꺼진 채 시작해야 사용자가 최소한의 반응 시간을 갖는다
        /// (StickmanAgent.Start()의 지연 로직과 이중 안전장치, 클래스 문서 "안전상 중요" 참고). 못 찾으면
        /// false를 반환할 뿐 예외를 던지지는 않는다 — 기존 컨벤션(BUG-P1-M3, StickmanAgent.Start()가
        /// 경고 로그만 남기고 계속 진행)을 그대로 유지한다. 반면 SetClickThrough/SetAlwaysOnTop은 이후에
        /// 대상 창이 없는 채로 호출되면 조용히 넘어가지 않고 예외를 던진다(아래 참고) — "오버레이 확보
        /// 자체의 실패"와 "확보된 오버레이의 속성 변경 실패"를 다른 강도로 취급한다.
        /// </summary>
        public bool CreateOverlayWindow()
        {
            bool found = SM_IsMainWindowFound() != 0;
            if (!found)
            {
                Debug.LogWarning("[MacWindowService] CreateOverlayWindow(): SM_IsMainWindowFound()==0 — " +
                    "네이티브 플러그인이 Unity Player의 메인 NSWindow를 찾지 못했습니다. 이후 " +
                    "SetClickThrough/SetAlwaysOnTop 호출이 모두 실패할 수 있습니다.");
                return false;
            }

            _clickThroughEnabled = false;
            _alwaysOnTopEnabled = false;
            SM_ConfigureOverlayWindow(0, 0, 1); // 투명은 항상 시도, 클릭관통/항상위는 안전하게 OFF로 시작.
            Debug.Log("[MacWindowService] CreateOverlayWindow(): 메인 NSWindow 확보 및 초기 상태 적용 완료 " +
                $"(clickThrough=false, alwaysOnTop=false, transparent=true, windowLevel={SM_GetOverlayWindowLevel()}).");
            return true;
        }

        /// <summary>
        /// SM_ConfigureOverlayWindow()로 실제 NSWindow.ignoresMouseEvents를 쓴다. 대상 창을 못 찾으면
        /// (SM_IsMainWindowFound()==0) 조용히 무시하지 않고 즉시 NotSupportedException을 던진다 — 이전
        /// 라운드들의 컨벤션과 동일: "위험한 부작용 없이 조용히 실패"가 아니라 "호출부가 반드시
        /// 알아채도록 즉시 예외로 실패"시킨다(StickMate.Core.StickmanAgent.Start()가 이 예외를 잡아
        /// 로그로 남기고 나머지 초기화를 계속하는 기존 처리 경로를 그대로 재사용).
        /// </summary>
        public void SetClickThrough(bool enabled)
        {
            if (SM_IsMainWindowFound() == 0)
            {
                throw new NotSupportedException(
                    "MacWindowService.SetClickThrough(): 네이티브 플러그인이 대상 NSWindow를 찾지 못해 " +
                    "클릭관통을 적용할 수 없습니다(SM_IsMainWindowFound()==0). StickMateOverlayPlugin.bundle이 " +
                    "빌드에 정상 포함되었는지, CreateOverlayWindow()가 먼저 호출되었는지 확인하세요.");
            }

            _clickThroughEnabled = enabled;
            SM_ConfigureOverlayWindow(_clickThroughEnabled ? 1 : 0, _alwaysOnTopEnabled ? 1 : 0, 1);
            Debug.Log($"[MacWindowService] SetClickThrough({enabled}) 적용 완료 — windowLevel={SM_GetOverlayWindowLevel()}.");
        }

        /// <summary>SetClickThrough와 동일한 실패 정책 — SM_ConfigureOverlayWindow()로 실제
        /// NSWindow.level(NSFloatingWindowLevel/NSNormalWindowLevel)을 쓴다.</summary>
        public void SetAlwaysOnTop(bool enabled)
        {
            if (SM_IsMainWindowFound() == 0)
            {
                throw new NotSupportedException(
                    "MacWindowService.SetAlwaysOnTop(): 네이티브 플러그인이 대상 NSWindow를 찾지 못해 " +
                    "항상위 설정을 적용할 수 없습니다(SM_IsMainWindowFound()==0). StickMateOverlayPlugin.bundle이 " +
                    "빌드에 정상 포함되었는지, CreateOverlayWindow()가 먼저 호출되었는지 확인하세요.");
            }

            _alwaysOnTopEnabled = enabled;
            SM_ConfigureOverlayWindow(_clickThroughEnabled ? 1 : 0, _alwaysOnTopEnabled ? 1 : 0, 1);
            Debug.Log($"[MacWindowService] SetAlwaysOnTop({enabled}) 적용 완료 — windowLevel={SM_GetOverlayWindowLevel()}.");
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
    }
}
#endif
