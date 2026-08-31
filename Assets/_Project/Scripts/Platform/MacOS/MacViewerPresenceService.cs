#if UNITY_STANDALONE_OSX
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// macOS용 <see cref="IViewerPresenceService"/> — "지금 이 화면을 볼 수 있는 사람이 있는가"를
    /// CoreGraphics/IOKit/Foundation에 <b>읽기 전용</b>으로 묻는다. 값을 쓰는 API는 한 개도 없다
    /// (CLAUDE.md 원칙 3, 그리고 P/Invoke는 Platform/ 아래에만 둔다는 프로젝트 불변 원칙).
    ///
    /// <para><b>왜 별도 파일인가</b>: <see cref="MacWindowService"/>에 붙일 수도 있었지만 그 파일은
    /// 1,600줄이 넘고 발판/Dock/커서 조회가 모두 들어 있어 성격이 다르다. 또한 이 라운드에 다른
    /// 에이전트가 그 파일을 동시에 편집 중이었다(충돌 회피). 여기서 쓰는 함수는 전부 독립적인 C ABI라
    /// 중복 선언 비용이 사실상 0이다.</para>
    ///
    /// <para><b>폴링 주기</b>: 이 클래스는 스스로 주기를 정하지 않는다. 호출부(<see cref="FramePacing"/>)가
    /// 0.25~0.5초 간격으로만 부른다 — "네이티브 열거는 매 프레임 금지, 디바운스/주기 폴링" 컨벤션.</para>
    ///
    /// <para><b>권한</b>: 여기 쓰이는 세 조회는 모두 접근성/화면기록 권한이 필요 없다.
    /// <c>CGEventSourceSecondsSinceLastEventType</c>은 이벤트를 <b>가로채는</b> 것이 아니라 "마지막
    /// 이벤트로부터 몇 초 지났는가"라는 스칼라 하나만 돌려주므로 입력 감시가 아니다.</para>
    /// </summary>
    internal sealed class MacViewerPresenceService : IViewerPresenceService
    {
        private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string IOKitLib = "/System/Library/Frameworks/IOKit.framework/IOKit";
        private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

        // ---- CoreGraphics: 디스플레이 슬립 / 사용자 무입력 시간 ----------------------------------

        [DllImport(CoreGraphicsLib)]
        private static extern uint CGMainDisplayID();

        /// <summary>boolean_t(=int) 반환. 디스플레이가 잠들어 있으면 0이 아니다.</summary>
        [DllImport(CoreGraphicsLib)]
        private static extern int CGDisplayIsAsleep(uint display);

        /// <summary>
        /// CFTimeInterval(=double) 반환. sourceState는 kCGEventSourceStateCombinedSessionState(0),
        /// eventType은 kCGAnyInputEventType(0xFFFFFFFF)을 쓴다 — 키/마우스/트랙패드를 모두 합친
        /// "마지막 사람 입력으로부터 지난 초".
        /// </summary>
        [DllImport(CoreGraphicsLib)]
        private static extern double CGEventSourceSecondsSinceLastEventType(int sourceState, uint eventType);

        /// <summary>
        /// ★ 두 소스를 <b>둘 다 읽어 작은 쪽을 쓴다</b>(2026-08-31 실측으로 결정).
        ///
        /// <para>같은 순간에 두 값이 크게 갈리는 것을 직접 관측했다:
        /// <code>
        ///   combined(0) = 23,979초(6.7시간)      hid(1) = 403.7초(6.7분)
        /// </code>
        /// 즉 <b>한쪽만 읽으면 "6.7시간 자리 비움"으로 오판</b>할 수 있다. 자리 비움 오판의 대가는
        /// "사용자가 보고 있는데 15fps로 그리는 것"이라 이번 사용자 신고("부드럽지 않다")를 정면으로
        /// 되살린다. 반대 방향 오판(절감을 못 하는 것)은 그냥 전기를 조금 더 쓰는 것뿐이다.
        /// <b>비대칭한 대가에는 비대칭한 보수성으로 답한다</b> — 언제나 작은 값(=더 최근에 입력이
        /// 있었다는 쪽)을 믿는다.</para>
        ///
        /// <para>두 값 모두 <b>실제 입력이 들어오면 즉시 0으로 리셋된다</b>는 것도 실측했다(제자리
        /// 마우스 이벤트 1회 주입 후 두 값 모두 0.5초로 떨어짐). 즉 자리 비움 등급에서 사용자가
        /// 돌아오면 다음 폴링(최대 0.2초) 안에 반드시 깨어난다.</para>
        /// </summary>
        private const int CombinedSessionState = 0;
        private const int HidSystemState = 1;
        private const uint AnyInputEventType = 0xFFFFFFFFu;

        // ---- IOKit: 전원 종류 ---------------------------------------------------------------------

        /// <summary>
        /// 남은 배터리 시간 추정(초). 두 개의 약속된 특수값이 있다:
        /// -1.0 = kIOPSTimeRemainingUnknown(배터리인데 아직 추정 불가),
        /// -2.0 = kIOPSTimeRemainingUnlimited(<b>AC 전원</b> — 배터리가 아예 없는 데스크톱도 이 값).
        /// 즉 "배터리 구동 중"은 <b>-2.0이 아닌 모든 경우</b>다. 배터리 잔량 대신 이 한 함수만 쓰는 이유는
        /// 전원 종류만 알면 충분하고, 잔량을 읽으려면 CFArray/CFDictionary 순회가 필요해 할당이 생기기
        /// 때문이다(24시간 상주 앱에서 주기 폴링 경로에 할당을 두지 않는다).
        /// </summary>
        [DllImport(IOKitLib)]
        private static extern double IOPSGetTimeRemainingEstimate();

        private const double TimeRemainingUnlimited = -2.0;

        // ---- Objective-C 런타임: NSProcessInfo.isLowPowerModeEnabled ------------------------------

        [DllImport(ObjCLib, EntryPoint = "objc_getClass")]
        private static extern IntPtr ObjCGetClass([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(ObjCLib, EntryPoint = "sel_registerName")]
        private static extern IntPtr ObjCSelector([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr ObjCSendPtr(IntPtr receiver, IntPtr selector);

        /// <summary>BOOL(1바이트) 반환용. arm64/x86_64 모두 하위 바이트에 결과가 온다.</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern byte ObjCSendBool(IntPtr receiver, IntPtr selector);

        /// <summary>respondsToSelector: — 셀렉터가 없는 OS 버전에서 메시지를 보내 죽지 않도록 먼저 묻는다.</summary>
        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern byte ObjCSendRespondsTo(IntPtr receiver, IntPtr selector, IntPtr arg);

        // ------------------------------------------------------------------------------------------

        // 저전력 모드 조회에 한 번이라도 실패하면 다시 시도하지 않는다(매번 예외를 던지며 로그를
        // 더럽히지 않기 위해). 실패는 "저전력 아님"으로 안전하게 폴백한다.
        private bool _lowPowerProbeBroken;
        private IntPtr _processInfo = IntPtr.Zero;
        private IntPtr _selLowPower = IntPtr.Zero;

        private bool _warnedOnce;

        public bool TryGetPresence(out ViewerPresenceSnapshot snapshot)
        {
            snapshot = default;
            try
            {
                // ★ 주 디스플레이 ID를 캐시하지 않는다(2026-08-31 R5 Major 1). CGMainDisplayID()는
                // 디스플레이 구성이 바뀌면 값이 바뀐다 — 클램셸(덮개 닫고 외장 모니터), 모니터
                // 연결/해제, 미러링 전환. 24시간 사는 이 인스턴스가 시작 시점의 ID를 붙들고 있으면
                // 이미 꺼진 내장 패널을 계속 물어보게 되고, 그 답은 Valid=false가 아니라
                // "Valid=true인데 낡은 true"라 정책의 안전설계("모르면 내려가지 않는다")로도 못 막아
                // DisplayOff(4fps)에 영구 고착된다(복구 = 앱 재시작). 캐시로 아낄 것도 없다 —
                // 초당 2~5회 호출되는 자리이고 이 조회는 비용이 사실상 0이다.
                bool asleep = CGDisplayIsAsleep(CGMainDisplayID()) != 0;

                double idleCombined = CGEventSourceSecondsSinceLastEventType(CombinedSessionState, AnyInputEventType);
                double idleHid = CGEventSourceSecondsSinceLastEventType(HidSystemState, AnyInputEventType);
                double idle = Math.Min(Sanitize(idleCombined), Sanitize(idleHid));
                // 음수/NaN 같은 이상값은 "모름"으로 낮춰 잡는다(정책이 Away로 잘못 내려가지 않게).
                float idleSeconds = double.IsPositiveInfinity(idle) ? -1f : (float)idle;

                double remaining = IOPSGetTimeRemainingEstimate();
                bool onBattery = Math.Abs(remaining - TimeRemainingUnlimited) > 0.001;

                snapshot = new ViewerPresenceSnapshot(asleep, idleSeconds, ProbeLowPowerMode(), onBattery);
                return true;
            }
            catch (Exception e)
            {
                if (!_warnedOnce)
                {
                    _warnedOnce = true;
                    Debug.LogWarning($"[프레임페이싱/presence] macOS 관측 실패({e.GetType().Name}) — " +
                        "적응형 프레임 등급을 끄고 항상 활성(60fps)으로 동작합니다. " + e.Message);
                }
                return false;
            }
        }

        /// <summary>이상값(NaN/음수)을 "모름"(양의 무한대)으로 바꿔 최솟값 연산에서 자동으로 밀려나게 한다.</summary>
        private static double Sanitize(double seconds)
            => double.IsNaN(seconds) || seconds < 0.0 ? double.PositiveInfinity : seconds;

        private bool ProbeLowPowerMode()
        {
            if (_lowPowerProbeBroken) return false;
            try
            {
                if (_processInfo == IntPtr.Zero)
                {
                    IntPtr cls = ObjCGetClass("NSProcessInfo");
                    if (cls == IntPtr.Zero) { _lowPowerProbeBroken = true; return false; }
                    _processInfo = ObjCSendPtr(cls, ObjCSelector("processInfo"));
                    if (_processInfo == IntPtr.Zero) { _lowPowerProbeBroken = true; return false; }

                    _selLowPower = ObjCSelector("isLowPowerModeEnabled");
                    IntPtr selResponds = ObjCSelector("respondsToSelector:");
                    if (_selLowPower == IntPtr.Zero || selResponds == IntPtr.Zero
                        || ObjCSendRespondsTo(_processInfo, selResponds, _selLowPower) == 0)
                    {
                        // 이 OS 버전에는 저전력 모드 개념이 없다 — 기능만 조용히 끈다.
                        _lowPowerProbeBroken = true;
                        return false;
                    }
                }
                return ObjCSendBool(_processInfo, _selLowPower) != 0;
            }
            catch
            {
                _lowPowerProbeBroken = true;
                return false;
            }
        }
    }
}
#endif
