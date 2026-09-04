#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// ★ 2026-09-03 (리더 판정 (b)) — 이미 만들어진 <b>작업표시줄 버튼</b>을 셸에게 직접 지우게 한다.
    ///
    /// ============================================================================
    /// 왜 스타일 비트로는 안 되는가 (이 파일이 존재하는 이유)
    /// ============================================================================
    /// 형제 파일 <see cref="WindowsToolWindowStyleControl"/>이 우리 창에
    /// <c>WS_EX_TOOLWINDOW</c>를 얹어 <b>Alt+Tab</b>에서는 즉시 빠졌다. 그런데 셸은 창이
    /// <b>보여질 때</b> 버튼을 만들고 그 뒤의 <c>SetWindowLong</c>을 통보받지 않는다 —
    /// 그 사실은 <see cref="StickMate.Platform.AppSwitcherPresencePolicy.TakesEffectOnAlreadyShownWindow"/>에
    /// 실행 가능한 형태로 박혀 있다. Microsoft가 적어 둔 처방은 <c>SW_HIDE</c> → 스타일 →
    /// 다시 보이기지만, <b>그 경로는 기각됐다.</b>
    ///
    /// ============================================================================
    /// 왜 (a) ShowWindow 왕복이 아니라 (b) COM인가 — 리더 판정 근거 그대로
    /// ============================================================================
    /// <list type="bullet">
    ///   <item>(a)는 우리 창의 <b>표시 상태</b>를 바꾼다. 바로 그 순간
    ///     <see cref="WindowsOverlayStateEnforcer"/>와 <see cref="WindowsTopmostWatchdog"/>이
    ///     topmost·layered·클릭 관통을 재적용하려 다투고 있다. 이 저장소에는 <b>같은 종류의
    ///     충돌로 영구 비활성된 해소기가 이미 있다</b>(<see cref="WindowsLayeredHybridResolver"/>의
    ///     되돌림 경로). 같은 함정에 두 번 빠지지 않는다.</item>
    ///   <item>(b)는 <b>창 상태를 한 비트도 건드리지 않는다.</b> 셸에게 "이 창은 목록에 넣지
    ///     마라"만 말한다. 위치·크기·Z-order·스타일·표시 상태 전부 그대로다.</item>
    ///   <item><c>ITaskbarList</c>는 <b>문서화된 표준 COM</b>이라 이 저장소의 비문서 API 누적
    ///     (현재 <c>InternalGetWindowText</c> 1건)을 늘리지 않는다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 이 파일이 <b>할 수 있는 일은 하나뿐</b>이다 (원칙 3)
    /// ============================================================================
    /// <c>ITaskbarList::DeleteTab(우리 자신의 HWND)</c> — 그것 하나다. 인터페이스의 나머지 슬롯
    /// (<c>AddTab</c>/<c>ActivateTab</c>/<c>SetActiveAlt</c>)은 <b>vtable 순서를 맞추기 위해서만</b>
    /// 선언돼 있고 이름을 <c>Reserved…</c>로 바꿔 두었다 — 특히 <c>ActivateTab</c>은 <b>남의 창을
    /// 활성화</b>할 수 있어 원칙 2/3에 정면으로 걸린다. 이름을 지워 두면 "옆에 하나 더 부르는"
    /// 자연스러운 실수가 성립하지 않는다. 호출이 하나뿐이라는 것은
    /// <c>AppSwitcherPresenceTests</c>가 기계적으로 다시 센다.
    ///
    /// ============================================================================
    /// 실패는 조용히 (리더 지시 1)
    /// ============================================================================
    /// COM을 못 만들거나 <c>DeleteTab</c>이 실패해도 <b>앱은 그대로 돈다.</b> 버튼이 남는 것은
    /// 불편이지 고장이 아니다. 예외는 전부 삼키고 <b>한 번만</b> 로그로 남긴다. 이 경로가 죽어도
    /// 투명/항상위/클릭 관통/Alt+Tab 제외는 전혀 영향받지 않는다.
    ///
    /// ============================================================================
    /// 되돌릴 문 (리더 지시 2)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>재빌드 없이 끄기</b>: 환경변수 <c>STICKMATE_KEEP_TASKBAR_BUTTON=1</c>
    ///     (<see cref="WindowsLayeredHybridResolver"/>의 <c>STICKMATE_KEEP_LAYERED</c>와 같은 관례).</item>
    ///   <item><b>종료 시 정리</b>: <c>Application.quitting</c>에서 COM 참조를 해제한다.
    ///     씬 오브젝트에 의존하지 않는다(<see cref="StickMate.Platform.ReservedBarRevealDirector"/>의
    ///     종료 훅과 같은 방식 — 씬 배선에 기대면 씬이 바뀔 때 조용히 죽는다).</item>
    /// </list>
    /// <para>버튼을 <b>되살리는</b> 경로(<c>AddTab</c>)는 일부러 만들지 않았다. 종료 시점에는 창
    /// 자체가 사라지므로 되살릴 대상이 없고, 그 능력을 코드에 두면 위 "호출 하나" 보증이 깨진다.</para>
    ///
    /// ============================================================================
    /// ★ 실기 미확인 (리더 지시 4)
    /// ============================================================================
    /// 이 개발 머신에 Windows가 없다. <b>"고쳤다"가 아니라 "이렇게 동작할 것으로 판단한다,
    /// 실기 미확인"</b>이다. 특히 확인되지 않은 것 둘: (1) Mono의 COM 상호운용이 이 플레이어에서
    /// 실제로 도는가, (2) 부착 직후 시점에 셸이 이미 버튼을 만들어 두었는가. (2) 때문에
    /// <see cref="MaxAttempts"/>회까지 재시도한다.
    /// </summary>
    internal static class WindowsTaskbarButtonRemover
    {
        internal const string LogPrefix = "[작업표시줄버튼]";

        private const string OptOutEnvVar = "STICKMATE_KEEP_TASKBAR_BUTTON";

        /// <summary>재시도 횟수. 셸이 버튼을 만드는 시점과 우리 첫 프레임의 선후를 보장할 수 없어
        /// 몇 번만 더 물어본다. <b>상한이 있는 것이 핵심</b>이다 — 24시간 상주 앱에서 COM 호출이
        /// 영원히 반복되면 안 된다.</summary>
        private const int MaxAttempts = 3;

        /// <summary>재시도 간격(초).</summary>
        private const float AttemptIntervalSeconds = 2f;

        private static bool _optOutResolved;
        private static bool _optOut;
        private static object _comObject;
        private static ITaskbarList _list;
        private static bool _unavailable;
        private static bool _failureLogged;
        private static bool _successLogged;
        private static bool _quitHookInstalled;
        private static int _attempts;
        private static float _timer;
        private static IntPtr _hwnd = IntPtr.Zero;

        /// <summary>진단용 — 지금까지 <c>DeleteTab</c>을 성공적으로 부른 횟수.</summary>
        internal static int Attempts => _attempts;

        /// <summary>진단용 — COM 경로가 <b>이미 못 쓴다고 확인된</b> 상태인가.</summary>
        internal static bool KnownUnavailable => _unavailable;

        /// <summary>
        /// 매 프레임 호출. 내부에서 주기와 상한을 지킨다. 상한에 도달하면 <b>아무 일도 하지
        /// 않고 즉시 반환</b>하므로 상주 비용이 0으로 수렴한다.
        /// </summary>
        internal static void Tick(float unscaledDeltaTime)
        {
            // 상한/옵트아웃 판정은 <b>플랫폼 중립 규칙</b>이 내린다 — 이 파일은 활성 타깃이 macOS인
            // 이 머신에서 컴파일조차 되지 않으므로, 상한 규칙을 여기 두면 아무도 실행해 볼 수 없다.
            if (!AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(
                    _optOut, _unavailable, _attempts, MaxAttempts)) return;

            if (!_optOutResolved)
            {
                _optOutResolved = true;
                _optOut = ResolveOptOut();
                if (_optOut)
                {
                    Debug.Log($"{LogPrefix} {OptOutEnvVar}가 설정되어 있어 작업표시줄 버튼을 그대로 둡니다 " +
                        "— Alt+Tab 제외(WS_EX_TOOLWINDOW)는 별개이며 그대로 걸려 있습니다.");
                    return;
                }
            }

            if (_attempts > 0)
            {
                _timer += unscaledDeltaTime;
                if (_timer < AttemptIntervalSeconds) return;
            }
            _timer = 0f;

            if (_hwnd == IntPtr.Zero || !IsWindowSafe(_hwnd)) _hwnd = UniWinCNativeHandle.Resolve();
            IntPtr hwnd = _hwnd;
            if (hwnd == IntPtr.Zero) return;

            if (!EnsureTaskbarList()) return;

            try
            {
                _list.DeleteTab(hwnd);
            }
            catch (Exception e)
            {
                // 실패해도 앱은 정상 동작한다 — 버튼이 남는 것은 불편이지 고장이 아니다.
                _unavailable = true;
                ReleaseNow("DeleteTab 실패");
                LogFailureOnce($"DeleteTab이 실패했습니다({e.GetType().Name}: {e.Message}).");
                return;
            }

            _attempts++;
            if (!_successLogged)
            {
                _successLogged = true;
                Debug.Log($"{LogPrefix} ITaskbarList.DeleteTab 호출 성공 — 우리 창(0x{hwnd.ToInt64():X})을 " +
                    "작업표시줄 목록에서 뺐습니다. <b>창 상태는 한 비트도 바뀌지 않았습니다</b>(표시/위치/크기/" +
                    "Z-order/스타일 전부 그대로) — 셸에게 목록에서 빼라고 말했을 뿐입니다. " +
                    $"셸이 버튼을 만드는 시점과의 경합 때문에 최대 {MaxAttempts}회까지만 반복하고 멈춥니다. " +
                    "★ 앱이 안 보이면 <b>다시 실행</b>하세요 — 사용자 직접 숨김은 저장되지 않으므로 " +
                    "재실행이 확실한 탈출구입니다. 이 경로를 끄려면 " + OptOutEnvVar + "=1.");
            }

            if (_attempts >= MaxAttempts) ReleaseNow($"상한 {MaxAttempts}회 도달(정상 종료)");
        }

        /// <summary>COM 참조를 해제한다. 여러 번 불려도 안전하다.</summary>
        internal static void ReleaseNow(string why)
        {
            if (_comObject == null) { _list = null; return; }
            try
            {
                Marshal.ReleaseComObject(_comObject);
            }
            catch (Exception)
            {
                // 해제 실패로 종료를 막지 않는다.
            }
            _comObject = null;
            _list = null;
            if (!string.IsNullOrEmpty(why)) Debug.Log($"{LogPrefix} COM 참조 해제 — {why}.");
        }

        private static bool EnsureTaskbarList()
        {
            if (_list != null) return true;

            try
            {
                Type type = Type.GetTypeFromCLSID(new Guid(TaskbarListClsid));
                if (type == null)
                {
                    _unavailable = true;
                    LogFailureOnce("CLSID_TaskbarList에 대한 타입을 얻지 못했습니다(COM 미지원 런타임일 수 있습니다).");
                    return false;
                }

                object instance = Activator.CreateInstance(type);
                if (instance == null)
                {
                    _unavailable = true;
                    LogFailureOnce("TaskbarList COM 개체를 만들지 못했습니다.");
                    return false;
                }

                var list = instance as ITaskbarList;
                if (list == null)
                {
                    _unavailable = true;
                    try { Marshal.ReleaseComObject(instance); } catch (Exception) { }
                    LogFailureOnce("ITaskbarList로 QueryInterface하지 못했습니다.");
                    return false;
                }

                list.HrInit();
                _comObject = instance;
                _list = list;
                InstallQuitHook();
                return true;
            }
            catch (Exception e)
            {
                // Mono의 COM 상호운용이 이 플레이어에서 안 도는 경우가 여기로 온다.
                _unavailable = true;
                LogFailureOnce($"COM 초기화 중 예외({e.GetType().Name}: {e.Message}).");
                return false;
            }
        }

        private static void InstallQuitHook()
        {
            if (_quitHookInstalled) return;
            _quitHookInstalled = true;
            // 씬 오브젝트를 하나도 만들지 않는다 — 씬 배선에 기대면 씬이 바뀔 때 조용히 죽는다
            // (ReservedBarRevealDirector.InstallQuitHook과 같은 이유).
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => ReleaseNow("앱 종료");

        private static bool ResolveOptOut()
        {
            try
            {
                string v = Environment.GetEnvironmentVariable(OptOutEnvVar);
                return !string.IsNullOrEmpty(v) && v != "0";
            }
            catch (Exception) { return false; }
        }

        private static void LogFailureOnce(string why)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            Debug.LogWarning($"{LogPrefix} {why} 작업표시줄 버튼이 그대로 남습니다 — <b>불편이지 고장이 아닙니다</b>. " +
                "투명/항상위/클릭 관통과 Alt+Tab 제외(WS_EX_TOOLWINDOW)는 전혀 영향을 받지 않습니다. " +
                "정직한 실패 보고용 로그입니다.");
        }

        private static bool IsWindowSafe(IntPtr hwnd)
        {
            try { return IsWindow(hwnd); }
            catch (EntryPointNotFoundException) { return false; }
            catch (DllNotFoundException) { return false; }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        // ==================== COM 선언 ====================

        /// <summary>CLSID_TaskbarList. IID와 <b>끝 한 자리만</b> 다르다(344 vs 342) — 바꿔 적으면
        /// 개체 생성이 조용히 실패한다. 이 머신에서는 실행으로 확인할 수 없으므로
        /// <c>AppSwitcherPresenceTests</c>가 두 값이 서로 다르고 문서 값과 같은지를 대신 잠근다.</summary>
        internal const string TaskbarListClsid = "56FDF344-FD6D-11D0-958A-006097C9A090";

        /// <summary>IID_ITaskbarList.</summary>
        internal const string TaskbarListIid = "56FDF342-FD6D-11D0-958A-006097C9A090";

        /// <summary>
        /// <c>ITaskbarList</c>. <b>슬롯 순서가 곧 ABI</b>다 — 줄 순서를 바꾸면 엉뚱한 함수가 불린다.
        /// 우리가 쓰는 것은 <see cref="DeleteTab"/> 하나이고, 나머지는 순서를 맞추기 위한
        /// 자리표시자다(위 클래스 문서의 "할 수 있는 일은 하나뿐" 참고).
        /// </summary>
        [ComImport]
        [Guid(TaskbarListIid)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList
        {
            /// <summary>슬롯 0 — 초기화. 반드시 첫 호출이어야 한다.</summary>
            void HrInit();

            /// <summary>슬롯 1 — 원본 <c>AddTab</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot1(IntPtr hwnd);

            /// <summary>슬롯 2 — <c>DeleteTab</c>. <b>이 파일이 부르는 유일한 것.</b></summary>
            void DeleteTab(IntPtr hwnd);

            /// <summary>슬롯 3 — 원본 <c>ActivateTab</c>. <b>부르지 않는다</b> — 남의 창을 활성화할 수
            /// 있어 원칙 2/3 정면 위반이다(자리표시자).</summary>
            void ReservedSlot3(IntPtr hwnd);

            /// <summary>슬롯 4 — 원본 <c>SetActiveAlt</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot4(IntPtr hwnd);
        }
    }
}
#endif
