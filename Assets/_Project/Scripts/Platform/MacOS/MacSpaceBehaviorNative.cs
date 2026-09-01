#if UNITY_STANDALONE_OSX
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// ============================================================================
    /// 오버레이 창을 "모든 Space(다른 앱의 전체화면 Space 포함)"에 띄우는 네이티브 배선 (2026-08-31)
    /// ============================================================================
    /// 사용자 신고: "다른 앱을 전체화면으로 만들고 클릭하면 캐릭터가 사라진다."
    /// 디버거 실측 결론(원인 A): UniWinC 네이티브(LibUniWinC)의 setTopmost가 우리 창에
    /// <c>collectionBehavior = [.fullScreenAuxiliary]</c> 만 걸고 <c>.canJoinAllSpaces</c>를 걸지 않는다.
    /// <c>.fullScreenAuxiliary</c>는 <b>자기 앱의</b> 전체화면 Space에만 따라붙는 플래그라, 타 앱이
    /// 전체화면으로 만든 별도 Space로 사용자가 전환하면 우리 창은 원래 Space에 남아 화면에서 사라진다.
    ///
    /// 채택안(R1, 저위험):
    ///   (1) 앱의 activation policy를 <b>accessory</b>로 바꾼다. Regular 앱은 창을 띄우거나 활성화될 때
    ///       macOS가 Space를 그 앱의 Space로 <b>전환</b>해버려서, canJoinAllSpaces를 걸어도 "타 앱
    ///       전체화면 위에 얹혀 있는" 상태가 유지되지 않는다. accessory(= LSUIElement와 같은 등급)는
    ///       Space 전환을 유발하지 않는 보조 앱 등급이라 이 연출의 전제 조건이다.
    ///   (2) collectionBehavior에 <c>.canJoinAllSpaces</c>를 더한다.
    /// (고위험안 R2 — object_setClass로 NSWindow를 NSPanel로 개조 — 는 리더가 명시적으로 기각했다.
    ///  런타임 클래스 스위즐링은 Unity의 뷰 계층/이벤트 경로 전체에 영향을 주고 실패 시 하드 크래시다.)
    ///
    /// 트레이드오프(리더 결정 + 사용자 승인 완료, 버그가 아님):
    ///   accessory 앱은 <b>Dock 아이콘과 Cmd-Tab 목록에서 사라진다.</b> 바탕화면 상주 펫이라는 컨셉과
    ///   오히려 부합한다. 종료 경로는 Dock에 의존한 적이 없어 그대로 살아 있다 —
    ///   Interaction/AppControlDirector.cs의 두 경로(전역 단축키 Ctrl+Opt+Cmd+Q, 캐릭터 우클릭 ->
    ///   [앱 종료])는 둘 다 Dock/메뉴바와 무관하며, 특히 단축키는 CGEventSourceKeyState 폴링이라 앱이
    ///   활성 상태가 아니어도 동작한다.
    ///
    /// ============================================================================
    /// 왜 Swift 네이티브가 아니라 여기(C#)인가
    /// ============================================================================
    /// 이 저장소에는 LibUniWinC의 Swift 소스가 없다. UniWinC는 UPM git 패키지로 들어와 있고
    /// (Packages/manifest.json), 패키지에는 <b>이미 서명된 LibUniWinC.bundle 바이너리만</b> 들어 있다.
    /// 즉 네이티브를 고치려면 상류 포크 + Xcode 빌드 파이프라인 신설이 필요한데, 그건 "기존 빌드
    /// 파이프라인을 건드리지 않는 범위"라는 제약을 정면으로 위반한다. 그래서 이미 이 프로젝트가
    /// MacWindowService(NSWorkspace 조회)에서 검증해 쓰고 있는 Objective-C 런타임 P/Invoke 방식으로
    /// <b>우리 앱 자신의</b> NSApplication/NSWindow에만 같은 설정을 건다. 라이브러리를 수정하지 않으므로
    /// 패키지 업데이트에도 깨지지 않는다.
    ///
    /// ★ 재적용이 필수인 이유: LibUniWinC의 setTopmost()는 호출될 때마다 collectionBehavior를 통째로
    /// 덮어쓴다. MacOverlayStateEnforcer가 isTopmost를 재적용할 때마다 우리 플래그가 날아가므로,
    /// 이 클래스의 <see cref="EnsureAllSpacesBehavior"/>는 그 직후에 다시 불려야 하고, 이후에도 낮은
    /// 빈도의 감시로 유지되어야 한다(플래그가 이미 맞으면 쓰기를 아예 하지 않는다).
    ///
    /// 비침해 원칙 확인: 여기서 만지는 객체는 전부 <b>우리 프로세스의</b> NSApplication과 그 창들이다.
    /// 타 프로세스의 창을 조회하지도, 이동/변경하지도 않는다(CLAUDE.md 절대 불변 원칙 3).
    /// </summary>
    internal static class MacSpaceBehaviorNative
    {
        private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

        // ★ 마샬링 규칙은 MacWindowService.cs의 "마샬링 안전 규칙"과 동일하다: 정수/포인터/BOOL 반환만
        // 쓰고 구조체·부동소수 반환은 절대 선언하지 않는다(아키텍처별 반환 규약이 갈려 하드 크래시).
        // objc_msgSend는 시그니처마다 별도 선언을 둔다 — 하나를 여러 시그니처에 재사용하면 안 된다.

        [DllImport(ObjCLib, CharSet = CharSet.Ansi, EntryPoint = "objc_getClass")]
        private static extern IntPtr ObjCGetClass(string name);

        [DllImport(ObjCLib, CharSet = CharSet.Ansi, EntryPoint = "sel_registerName")]
        private static extern IntPtr ObjCSelector(string name);

        /// <summary>[receiver selector] — 객체 포인터 반환(sharedApplication/windows).</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr ObjCSendPtr(IntPtr receiver, IntPtr selector);

        /// <summary>[receiver selector] — NSInteger/NSUInteger 반환(count/collectionBehavior/activationPolicy).</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr ObjCSendNInt(IntPtr receiver, IntPtr selector);

        /// <summary>[receiver selector:index] — NSUInteger 인자, 객체 포인터 반환(objectAtIndex:).</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr ObjCSendPtrWithNUInt(IntPtr receiver, IntPtr selector, IntPtr index);

        /// <summary>[receiver selector:value] — NSUInteger 인자, 반환 없음(setCollectionBehavior:).</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern void ObjCSendVoidWithNUInt(IntPtr receiver, IntPtr selector, IntPtr value);

        /// <summary>[receiver selector:policy] — NSInteger 인자, BOOL 반환(setActivationPolicy:).
        /// BOOL은 macOS에서 1바이트라 I1을 명시한다(MacWindowService.cs "마샬링 함정" 절과 같은 이유).</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool ObjCSendBoolWithNInt(IntPtr receiver, IntPtr selector, IntPtr policy);

        /// <summary>NSApplicationActivationPolicyAccessory — Dock 타일/메뉴바 없이 창만 띄우는 보조 앱.</summary>
        private const long NSApplicationActivationPolicyAccessory = 1;

        // NSWindowCollectionBehavior 비트(AppKit 헤더의 고정 리터럴 — 심볼 조회 불필요).
        private const long NSWindowCollectionBehaviorCanJoinAllSpaces = 1L << 0;
        private const long NSWindowCollectionBehaviorMoveToActiveSpace = 1L << 1;
        private const long NSWindowCollectionBehaviorFullScreenPrimary = 1L << 7;
        private const long NSWindowCollectionBehaviorFullScreenAuxiliary = 1L << 8;

        /// <summary>우리가 유지해야 하는 최소 비트 집합.</summary>
        private const long RequiredBehavior =
            NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary;

        /// <summary>
        /// 반드시 꺼야 하는 비트. 둘 다 우리가 켜는 비트와 AppKit 상 <b>상호 배타</b>라, 켜진 채로 남으면
        /// 동작이 미정의가 된다(실측 근거: 첫 적용 시 Unity 기본 창의 값이 0x80 =
        /// <c>.fullScreenPrimary</c>였고, OR만 하면 0x181 = primary와 auxiliary가 동시에 켜진 값이 됐다).
        ///   - <c>.moveToActiveSpace</c>  vs <c>.canJoinAllSpaces</c>: "따라다닌다" vs "모든 곳에 있다"
        ///   - <c>.fullScreenPrimary</c>  vs <c>.fullScreenAuxiliary</c>: "내가 전체화면이 된다" vs
        ///     "남의 전체화면 옆에 얹힌다". 오버레이는 후자여야 하고, 애초에 우리 창에는 전체화면 버튼이
        ///     없다(LibUniWinC의 setTopmost도 결국 auxiliary만 남긴다 — 같은 최종 상태로 수렴한다).
        /// </summary>
        private const long ForbiddenBehavior =
            NSWindowCollectionBehaviorMoveToActiveSpace | NSWindowCollectionBehaviorFullScreenPrimary;

        // 셀렉터/클래스는 프로세스 수명 동안 불변이라 한 번만 등록해 캐시한다(감시 주기마다
        // sel_registerName 문자열 마샬링을 반복하지 않기 위함 — 24시간 상주 앱, 상시 할당 금지).
        private static bool _selectorsReady;
        private static IntPtr _clsNSApplication;
        private static IntPtr _selSharedApplication;
        private static IntPtr _selSetActivationPolicy;
        private static IntPtr _selActivationPolicy;
        private static IntPtr _selWindows;
        private static IntPtr _selCount;
        private static IntPtr _selObjectAtIndex;
        private static IntPtr _selCollectionBehavior;
        private static IntPtr _selSetCollectionBehavior;

        /// <summary>activation policy는 앱 시작 시 1회만 바꾸면 되므로 래치를 둔다.</summary>
        private static bool _activationPolicyApplied;

        /// <summary>같은 실패를 매 감시 주기마다 로그로 도배하지 않기 위한 래치.</summary>
        private static bool _failureLogged;

        /// <summary>직전에 로그로 남긴 전이(from -> to). 같은 전이의 반복은 침묵시킨다.</summary>
        private static long _lastLoggedFrom = -1;
        private static long _lastLoggedTo = -1;

        private static void EnsureSelectors()
        {
            if (_selectorsReady) return;
            _clsNSApplication = ObjCGetClass("NSApplication");
            _selSharedApplication = ObjCSelector("sharedApplication");
            _selSetActivationPolicy = ObjCSelector("setActivationPolicy:");
            _selActivationPolicy = ObjCSelector("activationPolicy");
            _selWindows = ObjCSelector("windows");
            _selCount = ObjCSelector("count");
            _selObjectAtIndex = ObjCSelector("objectAtIndex:");
            _selCollectionBehavior = ObjCSelector("collectionBehavior");
            _selSetCollectionBehavior = ObjCSelector("setCollectionBehavior:");
            _selectorsReady = true;
        }

        private static IntPtr SharedApplication()
        {
            EnsureSelectors();
            if (_clsNSApplication == IntPtr.Zero) return IntPtr.Zero;
            return ObjCSendPtr(_clsNSApplication, _selSharedApplication);
        }

        /// <summary>
        /// activation policy를 accessory로 1회 전환한다. 이미 accessory면 아무것도 하지 않는다.
        /// 반드시 메인 스레드에서 호출할 것(AppKit 규약) — 호출자는 MonoBehaviour.Update다.
        /// </summary>
        internal static void ApplyAccessoryActivationPolicyOnce()
        {
            if (_activationPolicyApplied) return;

            try
            {
                IntPtr app = SharedApplication();
                if (app == IntPtr.Zero)
                {
                    LogFailureOnce("NSApplication.sharedApplication을 얻지 못했습니다");
                    return;
                }

                long before = ObjCSendNInt(app, _selActivationPolicy).ToInt64();
                if (before == NSApplicationActivationPolicyAccessory)
                {
                    _activationPolicyApplied = true;
                    return;
                }

                bool ok = ObjCSendBoolWithNInt(app, _selSetActivationPolicy,
                    new IntPtr(NSApplicationActivationPolicyAccessory));
                long after = ObjCSendNInt(app, _selActivationPolicy).ToInt64();
                _activationPolicyApplied = after == NSApplicationActivationPolicyAccessory;

                Debug.Log($"[전체화면동거] activation policy 전환 {(_activationPolicyApplied ? "성공" : "실패")} — " +
                    $"{before} -> {after} (setActivationPolicy 반환={ok}). " +
                    "accessory(1)가 되면 Dock 아이콘/Cmd-Tab에서 사라지는 대신 타 앱 전체화면 Space 위에 " +
                    "머무를 수 있습니다(의도된 트레이드오프). 종료는 Ctrl+Opt+Cmd+Q 또는 캐릭터 우클릭 -> [앱 종료].");
            }
            catch (Exception e)
            {
                LogFailureOnce($"activation policy 전환 중 예외({e.GetType().Name}: {e.Message})");
            }
        }

        /// <summary>
        /// 우리 앱의 모든 NSWindow에 <c>.canJoinAllSpaces | .fullScreenAuxiliary</c>가 걸려 있도록 보장한다.
        /// 이미 맞으면 <b>쓰기를 하지 않고</b> false를 돌려준다(감시 호출이 잦아도 부작용/로그가 없다).
        /// </summary>
        /// <param name="inspected">검사한 창 수(진단용).</param>
        /// <returns>실제로 collectionBehavior를 고쳐 쓴 창이 하나라도 있으면 true.</returns>
        internal static bool EnsureAllSpacesBehavior(out int inspected)
        {
            inspected = 0;
            try
            {
                IntPtr app = SharedApplication();
                if (app == IntPtr.Zero)
                {
                    LogFailureOnce("NSApplication.sharedApplication을 얻지 못했습니다");
                    return false;
                }

                IntPtr windows = ObjCSendPtr(app, _selWindows);
                if (windows == IntPtr.Zero) return false;

                long count = ObjCSendNInt(windows, _selCount).ToInt64();
                bool changedAny = false;
                for (long i = 0; i < count; i++)
                {
                    IntPtr window = ObjCSendPtrWithNUInt(windows, _selObjectAtIndex, new IntPtr(i));
                    if (window == IntPtr.Zero) continue;
                    inspected++;

                    long current = ObjCSendNInt(window, _selCollectionBehavior).ToInt64();

                    long desired = (current | RequiredBehavior) & ~ForbiddenBehavior;
                    if (desired == current) continue;

                    ObjCSendVoidWithNUInt(window, _selSetCollectionBehavior, new IntPtr(desired));
                    long readBack = ObjCSendNInt(window, _selCollectionBehavior).ToInt64();
                    changedAny = true;

                    // 같은 전이(0x100 -> 0x101)가 재적용 루프마다 반복되므로 값이 달라질 때만 남긴다 —
                    // 24시간 상주 앱에서 정상 동작을 로그로 도배하지 않기 위한 기존 컨벤션.
                    if (current == _lastLoggedFrom && readBack == _lastLoggedTo) continue;
                    _lastLoggedFrom = current;
                    _lastLoggedTo = readBack;

                    Debug.Log($"[전체화면동거] collectionBehavior 재적용 — 창 #{i}: 0x{current:X} -> 0x{readBack:X} " +
                        $"(목표 0x{desired:X}, canJoinAllSpaces={(readBack & NSWindowCollectionBehaviorCanJoinAllSpaces) != 0}, " +
                        $"fullScreenAuxiliary={(readBack & NSWindowCollectionBehaviorFullScreenAuxiliary) != 0}). " +
                        "LibUniWinC의 setTopmost가 호출될 때마다 이 값을 덮어쓰므로 재적용 자체는 정상 동작이다 — " +
                        "같은 전이가 반복되면 이 줄은 더 남기지 않는다.");
                }
                return changedAny;
            }
            catch (Exception e)
            {
                LogFailureOnce($"collectionBehavior 적용 중 예외({e.GetType().Name}: {e.Message})");
                return false;
            }
        }

        private static void LogFailureOnce(string what)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            Debug.LogWarning($"[전체화면동거] {what} — 타 앱 전체화면 Space 위 표시가 동작하지 않을 수 있습니다. " +
                "그 외 기능(투명/항상위/클릭관통)에는 영향이 없습니다.");
        }
    }
}
#endif
