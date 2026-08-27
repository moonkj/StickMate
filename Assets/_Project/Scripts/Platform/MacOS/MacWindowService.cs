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
    /// 이번 라운드의 의도적 범위(Architect/Debugger 지시, docs/BUG_REPORT_PHASE0.md m8 해소) — Win32와
    /// "같은 수준"으로 맞춘다: 창 열거(EnumerateFootholds)는 진짜로 동작, 진짜 분리 오버레이/클릭관통은
    /// Win32의 BUG-B1과 동일하게 다음 과제로 명시적으로 남긴다.
    /// ============================================================================
    /// - 진짜 구현: EnumerateFootholds(), IsFullscreenAppActive(), ICursorPositionService(전역 커서 조회).
    ///   전부 CoreGraphics/CoreFoundation의 공개 C ABI 함수만 사용하는 순수 조회 동작이라, 네이티브
    ///   Objective-C++ 플러그인(.bundle) 없이도 안전하게 실동작한다.
    /// - 안전가드(진짜 구현 아님): CreateOverlayWindow()/SetClickThrough()/SetAlwaysOnTop() — 실제
    ///   NSWindow(클릭관통=NSWindow.ignoresMouseEvents, 항상위=NSWindow.level)를 조작하려면 Cocoa
    ///   오브젝트에 접근하는 네이티브 플러그인이 반드시 필요한데, 이번 라운드는 그 플러그인 빌드가
    ///   범위 밖이다(Architect 지시). CoreGraphics C ABI만으로는 "다른 프로세스는 물론 우리 자신의
    ///   NSWindow조차" 클릭관통/레벨을 바꿀 수 있는 공개 수단이 없다(비공개 SkyLight API는 금지 대상).
    ///   Win32WindowService의 BUG-B1 가드(NotSupportedException)와 동일한 패턴으로 재사용한다.
    ///
    /// ILocalClickCaptureService/IDesktopIconLayoutService는 이번 라운드에 의도적으로 구현하지 않는다
    /// (요청 범위 밖) — FallbackPlatformWindowService가 `as` 캐스팅으로 null 처리해 안전하게 no-op/실패
    /// 취급하므로 컴파일/런타임 모두 문제 없다(Win32WindowService가 실제로 두 인터페이스 다 구현한 것과
    /// 다른 점 — macOS는 이번 라운드에 그 두 캐퍼빌리티까지는 손대지 않는다).
    /// </summary>
    public sealed class MacWindowService : IPlatformWindowService, ICursorPositionService
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

        // Win32WindowService의 _usingUnsafeSelfWindowFallback과 대응하는 필드는 두지 않는다 — macOS는
        // "조건부로 위험을 감수하면 실제 클릭관통이 걸리는" 경로 자체가 없다(네이티브 플러그인 부재로
        // NSWindow에 접근할 방법이 전혀 없음). 아래 SetClickThrough/SetAlwaysOnTop은 항상 무조건
        // NotSupportedException을 던진다 — Win32처럼 "나중에 진짜 오버레이가 생기면 이 가드가 조건부로
        // 풀린다"가 아니라, "네이티브 플러그인을 새로 만들기 전까지는 원천적으로 불가능"이라는 뜻이라
        // 가드 조건 없이 항상 던지는 편이 더 정직하다.
        private int _overlayWindowId = -1;

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
        // 안전가드 3종 — Win32WindowService의 BUG-B1 가드 패턴 재사용. 진짜 구현하지 않는다.
        // ============================================================================

        /// <summary>
        /// Win32와 달리 "우리 자신의 창 핸들을 재사용하는 위험한 폴백"조차 시도하지 않는다 — Win32는
        /// Process.MainWindowHandle이라는 관리 코드 API로 즉시 핸들을 얻을 수 있지만, 이는 Windows
        /// 전용 구현이라(.NET BCL 문서상 비-Windows 플랫폼에서 지원 안 됨) macOS에서는 애초에 호출할
        /// 수 없다. 대신 우리가 이미 만든 읽기 전용 열거 파이프라인(CGWindowListCopyWindowInfo)에서
        /// "ownerPID==우리 자신"인 창을 찾아 그 CGWindowID를 기록해두는 것으로 대체한다 — 이 값은
        /// 어디에도 쓰이지 않고(SetClickThrough/SetAlwaysOnTop이 아래에서 무조건 거부하므로) 순수하게
        /// "오버레이로 쓸 창을 찾았는지"에 대한 진단 정보 역할만 한다.
        /// </summary>
        public bool CreateOverlayWindow()
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
                    if (!IsSelfWindow(windowDict)) continue;

                    if (TryGetInt(windowDict, _keyWindowNumber, out int windowNumber))
                    {
                        _overlayWindowId = windowNumber;
                        return true;
                    }
                }
            }
            finally
            {
                CFRelease(windowArray);
            }

            return false; // 자기 자신의 온스크린 창을 못 찾음(예: 완전히 최소화됨) — StickmanAgent.Start()가 경고 로그만 남기고 계속 진행.
        }

        /// <summary>
        /// 항상 실패(NotSupportedException). 실제 클릭 관통은 NSWindow.ignoresMouseEvents(Cocoa)를
        /// 조작해야 하는데, 이 파일이 쓰는 CoreGraphics/CoreFoundation 공개 C ABI에는 그런 쓰기 API가
        /// 없다(비공개 SkyLight 프레임워크의 CGSSetWindowAlpha류는 심사 거부/차단 대상이라 사용 금지).
        /// Objective-C++ 네이티브 플러그인(.bundle)으로 NSWindow 참조를 얻어야만 가능하며, 그 플러그인
        /// 빌드는 이번 라운드 범위 밖이다(Architect 지시) — Win32WindowService.SetClickThrough()의
        /// BUG-B1 가드와 동일한 목적: "위험한 부작용 없이 조용히 실패"가 아니라 "호출부가 반드시
        /// 알아채도록 즉시 예외로 실패"시킨다(StickMate.Core.StickmanAgent.Start()가 이 예외를 잡아
        /// 로그로 남기고 나머지 초기화를 계속하는 기존 처리 경로를 그대로 재사용).
        /// </summary>
        public void SetClickThrough(bool enabled)
        {
            throw new NotSupportedException(
                "MacWindowService.SetClickThrough(): 실제 NSWindow 클릭관통 조작은 Objective-C++ 네이티브 " +
                "플러그인이 있어야 가능합니다(CoreGraphics/CoreFoundation 공개 C ABI만으로는 불가능, " +
                "docs/BUG_REPORT_PHASE0.md m8). 네이티브 플러그인 구현은 이번 라운드 범위 밖입니다.");
        }

        /// <summary>SetClickThrough와 동일한 이유로 항상 실패(NotSupportedException) — 실제 항상위 고정은
        /// NSWindow.level 조작이 필요하며 동일하게 네이티브 플러그인 전제 조건이다.</summary>
        public void SetAlwaysOnTop(bool enabled)
        {
            throw new NotSupportedException(
                "MacWindowService.SetAlwaysOnTop(): 실제 NSWindow 레벨(항상위) 조작은 Objective-C++ 네이티브 " +
                "플러그인이 있어야 가능합니다(CoreGraphics/CoreFoundation 공개 C ABI만으로는 불가능, " +
                "docs/BUG_REPORT_PHASE0.md m8). 네이티브 플러그인 구현은 이번 라운드 범위 밖입니다.");
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
