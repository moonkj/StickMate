#if UNITY_STANDALONE_WIN
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Debug = UnityEngine.Debug;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// Windows용 가상 데스크톱 <b>소속 조회</b> — <c>IVirtualDesktopManager</c>의 슬롯 0
    /// (<c>IsWindowOnCurrentVirtualDesktop</c>) <b>하나만</b> 부른다.
    ///
    /// ============================================================================
    /// ★★ 무엇을 <b>안</b> 하는가 — 이것이 이 파일에서 가장 중요한 문단이다
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>비공개 API를 쓰지 않는다.</b> <c>IVirtualDesktopManager</c>는 Microsoft Learn에
    ///     문서화된 <b>공개</b> 인터페이스다(Windows 10 1607+). 커뮤니티가 쓰는 비문서
    ///     <c>IVirtualDesktopManagerInternal</c>·<c>IVirtualDesktopNotificationService</c>는
    ///     <b>등장하지 않는다</b> — 그쪽은 빌드마다 IID가 바뀌어 OS 업데이트마다 깨지고,
    ///     비문서 API가 쌓이면 백신 휴리스틱 프로필이 나빠진다(사용자 실기는 AhnLab V3다).</item>
    ///   <item><b>창을 옮기지 않는다.</b> 같은 인터페이스의 슬롯 2가
    ///     「이 창을 저 데스크톱으로 옮겨라」인데, 이 파일은 그것을 <b>이름으로도 선언하지 않고</b>
    ///     <see cref="IVirtualDesktopManager.ReservedSlot2"/> 자리표시자로만 채운다
    ///     (<c>WindowsSystemAudioActivityProbe</c>·<c>WindowsTaskbarButtonRemover</c>와 같은 관례).
    ///     사용자가 어느 데스크톱에서 앱을 켰는지는 <b>사용자의 결정</b>이다.</item>
    ///   <item><b>남의 창을 묻지 않는다.</b> 인자로 받는 것은 언제나 우리 오버레이 <c>HWND</c> 하나다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 1차 출처 (Microsoft Learn, <c>shobjidl_core.h</c>) — <b>이 머신에서 실행 검증은 불가능하다</b>
    /// ============================================================================
    /// <code>
    ///   CoCreateInstance(CLSID_VirtualDesktopManager, ..., IID_IVirtualDesktopManager, &amp;mgr)
    ///     → mgr->IsWindowOnCurrentVirtualDesktop(hwnd, &amp;onCurrentDesktop)   // BOOL
    /// </code>
    /// <para>★ <b>이 파일은 이 개발 머신에서 한 번도 실행되지 않는다.</b> 확인한 것은
    /// <c>Tools/CrossCompile/xcheck.sh win</c>의 <b>컴파일 0에러</b>까지이고, 실제 거동은
    /// <b>미확인</b>이다. "고쳤다"가 아니라 "이렇게 동작할 것으로 판단한다, 실기 미확인"이 정직하다.</para>
    ///
    /// ============================================================================
    /// ★ 알려진 <b>거짓 음성</b>과 그 처리 — 전부 「모름」으로 접는다
    /// ============================================================================
    /// <list type="bullet">
    ///   <item>Windows 8.1 이하 / 가상 데스크톱이 없는 환경 → 개체 생성이 실패한다.</item>
    ///   <item>창이 아직 없거나(기동 직후 <c>_overlayHwnd == 0</c>) 최상위 창이 아니면
    ///     <c>E_INVALIDARG</c>류가 돌아온다.</item>
    ///   <item>탐색기(explorer.exe)가 재시작하면 캐시해 둔 프록시가 끊긴다
    ///     (<c>RPC_E_DISCONNECTED</c> / <c>RPC_S_SERVER_UNAVAILABLE</c>).</item>
    /// </list>
    /// 전부 <see cref="VirtualDesktopMembership.Unknown"/>이고, 그때 소비 측은 <b>숨지 않는다</b>
    /// (<see cref="VirtualDesktopSuspendPolicy"/>의 보수 규칙). 끊긴 프록시는 <b>다음 폴링에 한 번
    /// 다시 만들어 본다</b> — 다만 <see cref="MaxConsecutiveFailures"/>번 연속 실패하면 영구히
    /// 포기한다(24시간 상주 앱에서 1.5초마다 <c>CoCreateInstance</c>를 재시도하는 것이 더 나쁘다).
    /// </summary>
    internal sealed class WindowsVirtualDesktopProbe
    {
        private const string LogPrefix = "[가상데스크톱]";

        /// <summary>이만큼 연속 실패하면 영구 포기한다. 「끊긴 프록시 한 번 재생성」은 살리고
        /// 「영원한 재시도 루프」는 막는 최소값이다(1.5초 폴링 × 5 = 7.5초 안에 판정이 끝난다).</summary>
        internal const int MaxConsecutiveFailures = 5;

        /// <summary>
        /// ★ 2026-09-05 (perf-doc 권고) — <b>한 번의 조회가 이 시간을 넘으면 「실패」로 센다.</b>
        ///
        /// ============================================================================
        /// 왜 이 워치독이 필요한가 — 실패보다 <b>느린 성공</b>이 더 나쁘다
        /// ============================================================================
        /// <see cref="Query"/>는 메인 스레드에서 <b>셸(explorer.exe)에 동기 요청</b>을 보낸다.
        /// 셸이 응답만 하고 <b>느리면</b> HRESULT는 <c>S_OK</c>이므로 기존 실패 카운터에 아무것도
        /// 잡히지 않고, 그 대가는 <b>매 폴링마다 프레임이 통째로 멈추는 것</b>이다. 24시간 상주 앱에서
        /// 이건 "가끔 히칭한다"는 신고로 나타난다.
        ///
        /// <para><b>왜 8ms인가</b>: 60fps 프레임 예산 16.7ms의 <b>절반</b>이다. 이 값을 넘으면 그
        /// 프레임은 이미 vsync를 놓칠 확률이 높다. 정상 경로의 COM 호출은 프로세스 내 프록시 왕복이라
        /// 수십 μs 수준이므로 <b>정상값과 두 자릿수 이상 떨어져 있다</b> — 문턱이 애매한 구간에
        /// 놓이지 않는다. 다만 <b>이 값은 실기로 교정된 것이 아니다</b>(이 개발 머신에 Windows가 없다).
        /// 실기 로그의 <c>[스톨구간] … 가상데스크톱</c> 칸이 첫 실측을 주면 그때 조정한다.</para>
        ///
        /// <para><b>새 정책을 만들지 않는다</b>: 초과는 <see cref="MaxConsecutiveFailures"/>와
        /// <see cref="_unavailable"/> 래치를 <b>그대로</b> 쓴다. 즉 「느린 셸이 5회 연속」이면 조회를
        /// 영구히 멈추고, 그 뒤 소속은 <see cref="VirtualDesktopMembership.Unknown"/> =
        /// <b>이 기능이 없던 동작</b>이다(캐릭터는 숨지 않는다).</para>
        ///
        /// <para><b>정직한 한계</b>: 「연속」이므로 빠른 조회가 한 번만 섞여도 카운터가 0으로 돌아간다.
        /// 즉 <b>격번으로 느린 셸은 이 래치에 걸리지 않는다</b> — 그 경우는 래치가 아니라
        /// <c>[스톨구간]</c> 원장이 잡아야 하고, 그것이 위 계측을 <b>필수</b>로 둔 이유다.</para>
        /// </summary>
        internal const double SlowQueryBudgetMs = 8.0;

        private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;

        // ---- COM 식별자. 1차 출처는 Microsoft Learn / shobjidl_core.h 다.
        //      한 글자만 틀려도 개체 생성이나 QueryInterface가 **조용히** 실패한다. 이 머신에서는
        //      실행으로 확인할 수 없으므로 EditMode 계약 테스트가 소스에서 뽑아
        //      «서로 다르고 문서 값과 같은지»를 대신 잠근다.

        /// <summary><c>CLSID_VirtualDesktopManager</c>.</summary>
        internal const string VirtualDesktopManagerClsid = "AA509086-5CA9-4C25-8F95-589D3C07B48A";

        /// <summary><c>IID_IVirtualDesktopManager</c>.</summary>
        internal const string VirtualDesktopManagerIid = "A5CD92FF-29BE-454C-8D04-D82879FB3F1B";

        private const int S_OK = 0;

        private IVirtualDesktopManager _manager;
        private object _managerComObject;
        private bool _unavailable;
        private bool _failureLogged;
        private bool _quitHookInstalled;
        private int _consecutiveFailures;

        /// <summary>
        /// <paramref name="topLevelWindow"/>가 지금 활성인 가상 데스크톱에 있는가.
        /// <b>조회에 실패하면 <see cref="VirtualDesktopMembership.Unknown"/></b>이다 — 그것이
        /// 「다른 데스크톱」과 같은 값이 되면 조회 실패가 캐릭터를 영원히 숨긴다.
        ///
        /// <para>★ 2026-09-05 — 이 메서드 전체가 <c>[스톨구간]</c> 원장의
        /// <see cref="StallSection.VirtualDesktop"/> 칸에 <b>자기시간</b>으로 쌓인다. 여기서 부르는
        /// COM 호출은 <b>메인 스레드에서 셸(explorer.exe)로 가는 동기 요청</b>이라, 셸이 느려지면
        /// 그 프레임이 통째로 멈춘다. 계측이 없으면 그 시간은 <c>기타로직</c>이라는 잔차로 흘러가
        /// <b>원인이 남의 프로세스인데 원장이 우리 코드를 가리킨다</b>(<c>RecordFullscreenProbe</c>
        /// 문서의 「원장의 17%가 비어 있었다」와 같은 형태). 비용은 진입/이탈당
        /// <c>Stopwatch.GetTimestamp()</c> 1회씩이고 <b>할당은 0</b>이다.</para>
        /// </summary>
        internal VirtualDesktopMembership Query(IntPtr topLevelWindow)
        {
            if (_unavailable || topLevelWindow == IntPtr.Zero) return VirtualDesktopMembership.Unknown;

            // ★ 계측(필수). 아래 워치독은 «5회 연속»에만 반응하므로 격번으로 느린 셸은 못 잡는다 —
            //   그 축을 잡는 것은 래치가 아니라 이 원장이다(SlowQueryBudgetMs 문서의 「정직한 한계」).
            using var __stall = global::StickMate.Platform.StallAttribution.Section(
                global::StickMate.Platform.StallSection.VirtualDesktop);   // [스톨구간] 계측

            // ★ 워치독(권고). EnsureManager()의 CoCreateInstance도 셸에 가므로 함께 잰다 —
            //   느린 것이 «생성»인지 «조회»인지는 위 원장의 평균/최대가 나중에 말해 준다.
            long startTicks = Stopwatch.GetTimestamp();

            try
            {
                if (!EnsureManager()) return VirtualDesktopMembership.Unknown;

                int hr = _manager.IsWindowOnCurrentVirtualDesktop(topLevelWindow, out int onCurrentDesktop);
                if (hr != S_OK)
                {
                    // 프록시가 끊겼을 수 있다(탐색기 재시작). 놓고 다음 폴링에 한 번 다시 만들어 본다.
                    RecordFailure($"IsWindowOnCurrentVirtualDesktop이 0x{hr:X8}을 돌려주었습니다");
                    return VirtualDesktopMembership.Unknown;
                }

                // ★ 성공했지만 «예산을 넘게 느렸다»면 그것도 실패로 센다. 여기서 0으로 되돌리면
                //   느린 셸이 영원히 카운터를 비워 래치가 절대 닫히지 않는다.
                if (!RecordSlowQueryIfOverBudget(startTicks)) _consecutiveFailures = 0;

                return onCurrentDesktop != 0
                    ? VirtualDesktopMembership.CurrentDesktop
                    : VirtualDesktopMembership.OtherDesktop;
            }
            catch (Exception e)
            {
                // Mono/IL2CPP의 COM 상호운용이 안 도는 경우도 여기로 온다. 조회 실패는 「모름」이지
                // 「다른 데스크톱」이 아니다.
                RecordFailure($"소속 조회 중 예외({e.GetType().Name}: {e.Message})");
                return VirtualDesktopMembership.Unknown;
            }
        }

        /// <summary>
        /// 이번 조회가 <see cref="SlowQueryBudgetMs"/>를 넘었으면 기존 실패 카운터에 한 번 반영하고
        /// <c>true</c>를 돌려준다(예산 안이면 <c>false</c>).
        ///
        /// <para><b>여기서는 프록시를 놓지 않는다</b> — <see cref="RecordFailure"/>와 갈라지는 유일한
        /// 지점이고, 그게 이 함수가 따로 있는 이유다. 실패는 「프록시가 끊겼을 수 있다」이므로 놓고 다시
        /// 만드는 것이 처방이지만, <b>느림은 프록시가 멀쩡한데 상대가 느린 것</b>이다. 그때 프록시를
        /// 놓으면 다음 폴링이 <c>CoCreateInstance</c>부터 다시 하게 되어 <b>느린 셸에 더 무거운 요청을
        /// 보내는</b> 셈이 된다 — 증상을 계측이 키우는 그 사고다.</para>
        /// </summary>
        private bool RecordSlowQueryIfOverBudget(long startTicks)
        {
            double elapsedMs = (Stopwatch.GetTimestamp() - startTicks) * MillisecondsPerTick;
            if (elapsedMs <= SlowQueryBudgetMs) return false;

            _consecutiveFailures++;
            if (_consecutiveFailures < MaxConsecutiveFailures) return true;

            _unavailable = true;
            LogFailureOnce($"조회가 {elapsedMs:F1}ms 걸렸습니다(예산 {SlowQueryBudgetMs:F0}ms, 셸 응답 지연) — " +
                $"{MaxConsecutiveFailures}회 연속 예산 초과");
            return true;
        }

        private bool EnsureManager()
        {
            if (_manager != null) return true;

            try
            {
                Type type = Type.GetTypeFromCLSID(new Guid(VirtualDesktopManagerClsid));
                if (type == null)
                {
                    _unavailable = true;
                    LogFailureOnce("CLSID_VirtualDesktopManager에 대한 타입을 얻지 못했습니다(COM 미지원 런타임일 수 있습니다)");
                    return false;
                }

                object instance = Activator.CreateInstance(type);
                if (!(instance is IVirtualDesktopManager manager))
                {
                    _unavailable = true;
                    ReleaseSafe(instance);
                    LogFailureOnce("IVirtualDesktopManager로 QueryInterface하지 못했습니다(Windows 8.1 이하일 수 있습니다)");
                    return false;
                }

                _managerComObject = instance;
                _manager = manager;
                InstallQuitHook();
                return true;
            }
            catch (Exception e)
            {
                RecordFailure($"COM 초기화 중 예외({e.GetType().Name}: {e.Message})");
                return false;
            }
        }

        /// <summary>실패를 세고, 관리자 프록시를 놓아 다음 폴링에 한 번 다시 만들어 보게 한다.
        /// <see cref="MaxConsecutiveFailures"/>를 넘으면 영구히 포기한다(로그는 한 번만).</summary>
        private void RecordFailure(string why)
        {
            ReleaseManager();
            _consecutiveFailures++;
            if (_consecutiveFailures < MaxConsecutiveFailures) return;

            _unavailable = true;
            LogFailureOnce(why + $" — {MaxConsecutiveFailures}회 연속 실패");
        }

        private void InstallQuitHook()
        {
            if (_quitHookInstalled) return;
            _quitHookInstalled = true;
            // 씬 오브젝트를 만들지 않는다 — 씬 배선에 기대면 씬이 바뀔 때 조용히 죽는다
            // (WindowsSystemAudioActivityProbe.InstallQuitHook과 같은 이유).
            UnityEngine.Application.quitting += ReleaseManager;
        }

        private void ReleaseManager()
        {
            ReleaseSafe(_managerComObject);
            _managerComObject = null;
            _manager = null;
        }

        private static void ReleaseSafe(object comObject)
        {
            if (comObject == null) return;
            try { Marshal.ReleaseComObject(comObject); }
            catch (Exception) { /* 해제 실패로 종료나 다음 폴링을 막지 않는다. */ }
        }

        private void LogFailureOnce(string why)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            Debug.LogWarning($"{LogPrefix} {why}. 가상 데스크톱 소속을 «모름»으로 보고합니다 — " +
                "캐릭터는 <b>숨지 않고</b> 지금까지와 똑같이 동작합니다(보수 규칙). " +
                "데스크톱을 전환해도 스스로 숨지 않는 것뿐이며, 다른 기능은 영향을 받지 않습니다.");
        }

        // ==================== COM 선언 ====================
        // ★ 슬롯 순서가 곧 ABI다. 줄 순서를 바꾸면 엉뚱한 함수가 불린다.
        //   부르지 않는 슬롯도 반드시 자리를 채워야 한다(WindowsSystemAudioActivityProbe와 같은 관례).
        //   ★★ 슬롯 2는 원본이 「이 창을 저 데스크톱으로 옮겨라」다 — 이름으로도 적지 않는다.

        [ComImport]
        [Guid(VirtualDesktopManagerIid)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IVirtualDesktopManager
        {
            /// <summary>슬롯 0 — <c>IsWindowOnCurrentVirtualDesktop</c>. <b>이 파일이 부르는 유일한 것.</b>
            /// <para><c>PreserveSig</c>다: 실패 HRESULT를 예외가 아니라 값으로 받는다. 24시간 상주 앱에서
            /// 1.5초마다 예외를 던지고 잡는 것은 비용도 로그도 낭비다.</para></summary>
            [PreserveSig]
            int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);

            /// <summary>슬롯 1 — 원본 <c>GetWindowDesktopId</c>. <b>부르지 않는다</b>(자리표시자).
            /// 데스크톱 GUID를 알 필요가 없다 — 우리가 묻는 것은 «지금 보이는가» 하나뿐이다.</summary>
            [PreserveSig]
            int ReservedSlot1(IntPtr topLevelWindow, out Guid desktopId);

            /// <summary>슬롯 2 — 원본은 <b>창을 다른 데스크톱으로 옮기는</b> 쓰기 API다.
            /// <b>절대 부르지 않는다</b>(자리표시자). 우리 창이라도 마찬가지다 — 어느 데스크톱에서
            /// 앱을 켰는지는 사용자의 결정이고, 이 계약은 <b>읽기 전용</b>이다.</summary>
            [PreserveSig]
            int ReservedSlot2(IntPtr topLevelWindow, ref Guid desktopId);
        }
    }
}
#endif
