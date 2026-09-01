#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using StickMate.Platform;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// 오버레이 창의 "항상 위"가 <b>OS 수준에서 실제로 살아 있는지</b>를 상시 감시하고, 풀렸으면
    /// 즉시 되돌리며, <b>전이 순간만</b> 진단 한 줄을 남기는 Windows 전용 계층.
    ///
    /// ============================================================================
    /// 이 파일이 생긴 이유 — 같은 버그 3번째 신고 (2026-09-01)
    /// ============================================================================
    /// 사용자 신고: "엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가 없어져버림
    /// <b>화면 뒤로 넘어 가는 거 같음</b>" / 재확인 "자동숨김이 아니라 창뒤로 넘어가는거야".
    ///
    /// 앞선 수정 2회(macOS 게임 카테고리, Windows 게임바 등록 대조)는 전부 <b>자동 숨김 판정</b>을
    /// 고쳤다. 그런데 사용자가 겪은 것은 숨김이 아니라 <b>z-order 강등</b>이었으므로 증상이 그대로였다.
    /// 이 파일은 그 진짜 경로를 담당한다.
    ///
    /// ============================================================================
    /// 왜 감시가 필요한가 — 강등시키는 주체가 우리가 아니다
    /// ============================================================================
    /// Windows에서 <c>WS_EX_TOPMOST</c>는 우리가 걸어도 남이 풀 수 있다:
    ///   · 다른 창이 자기를 topmost로 올리면 같은 밴드 안에서 우리 위로 갈 수 있다.
    ///   · Unity 플레이어가 <b>전체화면 계열 모드</b>(FullScreenWindow/ExclusiveFullScreen)로 떠 있으면,
    ///     포커스를 잃을 때 Unity 자신이 창의 z-order를 내려 다른 앱을 쓸 수 있게 한다.
    ///     (이 프로젝트의 ProjectSettings는 <c>fullscreenMode: 1</c> = FullScreenWindow이고,
    ///      WindowsOverlayStateEnforcer.TickFullScreenBounds가 Windowed로 내리는 호출을
    ///      "해상도가 다를 때"에만 하고 있었다 — 모니터 네이티브 해상도에서는 그 조건이 거짓이라
    ///      Windows에서만 전체화면 모드로 남았다. 같은 코드가 macOS에서 멀쩡했던 이유는 Retina 배율
    ///      때문에 그 조건이 항상 참이었기 때문이다.)
    /// 어느 쪽이든 <b>라이브러리는 스스로 되돌리지 않는다</b>: 네이티브 LibUniWinC.dll의 임포트에는
    /// <c>SetWindowPos</c>는 있어도 <c>SetWinEventHook</c>/<c>SetTimer</c> 계열이 없다(바이너리 확인).
    /// 그래서 감시자가 우리 쪽에 있어야 한다 — macOS의 <c>TickAllSpacesBehavior</c>와 같은 역할이다.
    ///
    /// ============================================================================
    /// 절대 불변 원칙 3(유저 자산 불변) — 이 파일이 지키는 방식
    /// ============================================================================
    /// · 여기 선언된 Win32 함수는 <b>전부 조회 전용</b>이다. <c>SetWindowPos</c>/<c>SetWindowLong</c>
    ///   같은 쓰기 계열은 <b>선언조차 하지 않는다</b> — 선언이 없으면 실수로도 남의 창을 건드릴 수 없다.
    /// · topmost 재적용은 우리 창에만 작용하는 <c>UniWindowController.isTopmost</c> 대입으로만 한다
    ///   (호출자가 넘긴 콜백). 이 파일에서 창을 바꾸는 코드는 0줄이다.
    /// · z-order 순위 계산은 <c>GetTopWindow</c>/<c>GetWindow</c> 순회이며 읽기만 한다.
    ///
    /// ============================================================================
    /// ★ 2026-09-01 3차 — 이 진단이 스스로 잡은 두 번째 경로: <b>밴드 안의 순위</b>
    /// ============================================================================
    /// 실기 로그(20260901d)가 <c>WS_EX_TOPMOST(전=True, 후=True)</c>인 채로
    /// <c>★ 우리가 아래(우리 #19 &gt; 전경 #18)</c>를 찍었다. 위 감시는 <b>비트</b>만 보므로 이 상태를
    /// 사건으로 취급조차 하지 않았다 — <c>ForegroundChanged</c> 한 줄에 우연히 딸려 나왔을 뿐이다.
    /// 그래서 이 라운드에서 <b>밴드 내 가림을 1급 관측 대상으로 승격</b>했다:
    ///   (1) <see cref="ScanBandOcclusion"/> — 우리 위에 있는 <b>보이는</b> topmost 창을 센다.
    ///       순회는 <b>우리 자신을 만나면 즉시 멈춘다</b>(실기 rank 19 → 20회). 예전의 818회 순회와 다르다.
    ///   (2) <see cref="StickMate.Platform.TopmostBandOcclusionPolicy"/> — 그 창이 <b>하단 예약 막대</b>
    ///       (작업표시줄)인지 아닌지를 가른다. 막대라면 macOS에서 Dock 창(layer 20)이 우리(layer 3) 위에
    ///       상시 있는 것과 <b>같은 정상 상태</b>이므로 경보가 아니다. 그 규칙 파일에 실측 근거가 있다.
    ///   (3) <see cref="StickMate.Platform.WatchTraceRing"/> — 가림이 <b>시작된 순간</b> 직전 몇 초의
    ///       관측을 함께 찍는다(리더 지시: "우리가 아래로 내려간 순간의 직전 이벤트를 남겨라").
    ///
    /// <para><b>이 라운드는 z-order를 고치지 않는다</b> — 밴드 안에서 위로 올라가는 유일한 수단인
    /// <c>SetWindowPos(HWND_TOPMOST)</c>는 작업표시줄/시작 메뉴 위에 영구히 올라앉게 만들어 원칙 2와
    /// 정면으로 충돌한다. 판단에 필요한 <b>숫자</b>를 먼저 실기에서 받는다.</para>
    /// </summary>
    internal sealed class WindowsTopmostWatchdog
    {
        #region Win32 선언 (전부 조회 전용 — 쓰기 계열은 선언 자체가 없다)

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>
        /// DWM 클로킹 조회. <b>Get</b>이라 원칙 3에 저촉되지 않는다(쓰기 계열은
        /// <c>DwmSetWindowAttribute</c>이며 이 파일에 선언조차 없다).
        ///
        /// <para>왜 필요한가: Win11의 셸 창들(<c>StartMenuExperienceHost</c>,
        /// <c>ShellExperienceHost</c>, 각종 XAML 팝업)은 <b>닫혀 있어도 파괴되지 않고</b>
        /// "클로킹"된 채로 topmost 밴드에 남는다. 이때 <c>IsWindowVisible</c>은 <b>true</b>다.
        /// 이걸 거르지 않으면 "시작 메뉴가 우리를 덮고 있다"는 <b>상시 거짓 경보</b>가 찍히고,
        /// 아래 <c>otherOccluderCount</c>가 영원히 0이 아니게 되어 판정 전체가 무의미해진다.</para></summary>
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute,
            out int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const uint GW_HWNDNEXT = 2;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int DWMWA_CLOAKED = 14;

        #endregion

        /// <summary>
        /// 감시 주기(초). 두 가지 요구가 부딪히는 값이라 근거를 남긴다:
        ///   · 24시간 상주 앱이므로 매 프레임 네이티브 호출은 금지(프로젝트 컨벤션).
        ///   · 그러나 "되돌리긴 하는데 눈에 보일 만큼 느리다"면 사용자에겐 버그가 안 고쳐진 것과 같다.
        /// 평상시 호출은 <c>IsWindow</c> + <c>GetWindowLong</c> + <c>GetForegroundWindow</c>
        /// <b>세 번뿐</b>이라(z-order 순위 계산 같은 비싼 작업은 아래 상한을 통과한 전이에서만 한다)
        /// 0.1초는 사실상 공짜이면서 사람이 창을 전환하고 눈이 반응하기 전에 복구가 끝난다.
        ///
        /// <para>★ 2026-09-01 2차 — 이 값은 <b>그대로 두는 것이 옳다</b>고 판단했다. "초당 10회 전체 창
        /// 열거"가 렉의 원인으로 지목됐지만, 열거를 하던 것은 <b>주기 폴링이 아니라 전이 로그</b>였다
        /// (<see cref="ResolveRelation"/> 문서). 주기 자체를 늘리면 복구가 눈에 보이게 느려지는
        /// 대가만 치르고 비용은 거의 안 준다. 실제 비용은 로그 상한과 순회 제거로 잘랐고, 남은 비용은
        /// 60초 요약이 <b>실측 ms로</b> 보고한다 — 그 숫자가 이 판단의 검증 수단이다.</para>
        ///
        /// <para>실제 해상도는 프레임률에 의해 위에서 눌린다(FramePacing이 유휴 시 프레임을 낮춘다).
        /// 그래서 로그에는 "관측 주기 상한"이 아니라 <b>실측 경과 시간</b>을 찍는다.</para>
        /// </summary>
        private const float WatchIntervalSeconds = 0.1f;

        /// <summary>
        /// z-order 순위 순회 상한.
        ///
        /// <para>★ 2026-09-01 2차 — 4000에서 256으로 낮췄다. 실기 로그에 <c>(열거 818개)</c>가 찍혔고,
        /// 그 818은 <b>버그가 아니라 진짜 최상위 창 수</b>다(숨은 메시지 창 포함, 데스크톱에서 흔한
        /// 규모). 문제는 그 수가 <b>앱 가동 시간에 비례해 늘어난다</b>는 것이다 — 사용자가 앱을 열수록
        /// 창이 늘고, 그래서 "켜둘수록 렉이 심해진다"에 그대로 기여한다. 아래
        /// <see cref="ResolveRelation"/>이 대부분의 경우 순회를 <b>0회</b>로 만들었으므로, 남은 순회는
        /// 같은 z-order 밴드 안의 비교뿐이고 그때는 256이면 충분하다. 넘으면 조용히 틀린 답을 내지 않고
        /// "상한 초과"라고 정직하게 보고한다.</para></summary>
        private const int MaxZOrderWalk = 256;

        /// <summary>
        /// 상세 [Z-ORDER] 한 줄 사이의 최소 간격(초).
        ///
        /// <para>★ 왜 필요한가(2026-09-01 2차, 이 진단이 스스로 만든 결함): 원래 이 감시자는
        /// <b>사건이 날 때마다 무조건</b> 상세 한 줄을 남겼다. 그런데 <c>DemotedAndRestored</c>는
        /// 원리적으로 <b>매 틱 반복될 수 있다</b> — 우리가 되돌리면 <c>_alive</c>가 다시 true가 되므로,
        /// 누군가 계속 강등시키는 환경에서는 초당 10회가 전부 "처음 발견한 강등"이 된다. 그 한 줄의
        /// 비용은 (창 수에 비례하는 z-order 순회 + 프로세스명 조회 2회 + 1KB 문자열 + 스택트레이스가
        /// 켜진 Debug.Log + Player.log 동기 쓰기)라 결코 싸지 않다.
        /// <b>진단은 필요하지만 그 비용이 증상을 만들면 안 된다</b>(리더 지시). 그래서 상세는 드물게,
        /// 대신 <b>누락된 사건은 집계로 반드시 보고</b>한다.</para></summary>
        private const float DetailLogMinIntervalSeconds = 5f;

        /// <summary>상세 한 줄의 프로세스 수명 총량. 여기 닿으면 이후로는 집계만 남긴다 —
        /// 24시간 상주 앱에서 진단이 무제한으로 자원을 먹는 경로를 원천 차단한다.</summary>
        private const int MaxDetailLogs = 40;

        /// <summary>억제된 사건과 <b>감시 자체의 비용</b>을 요약하는 주기(초).</summary>
        private const float SummaryIntervalSeconds = 60f;

        private float _timer;
        private TopmostWatchdogTracker _tracker;
        private int _reassertCount;
        private bool _staleHandleLogged;
        private bool _noHandleLogged;

        // ── 로그 상한 상태 ──────────────────────────────────────────────────────────
        private float _detailCooldown;
        private int _detailLogCount;
        private float _summaryTimer;
        private int _suppressedDemotion;
        private int _suppressedRestored;
        private int _suppressedForeground;
        private bool _capNoticeLogged;

        // ── 밴드 내 가림 감시(2026-09-01 3차) ───────────────────────────────────────
        private BandOcclusionTracker _occlusion;
        private readonly WatchTraceRing _trace = new WatchTraceRing();
        private float _bandScanTimer;
        private bool _lastSuspended;
        private IntPtr _lastOccluderHwnd;
        private BandRect _lastOccluderRect;
        private int _lastOtherOccluderCount;
        private bool _bandScanInconclusive;
        /// <summary>마지막으로 밴드 스캔이 <b>실제로</b> 돈 시각. 스캔은 항상위 비트가 살아 있을 때만
        /// 돌기 때문에(그렇지 않으면 "우리 위 = 전부 topmost"라는 순회 전제가 깨진다), 이 값이 없거나
        /// 오래됐으면 밴드 상태를 <b>현재형으로 말하면 안 된다</b> — 로그가 거짓말하는 가장 흔한 방식이다.</summary>
        private double _lastBandScanSeconds = double.NegativeInfinity;
        private int _occlusionDetailLogCount;
        private float _occlusionDetailCooldown;
        private int _suppressedOcclusion;
        /// <summary>막대가 <b>아닌</b> 창이 우리를 덮은 관측 수(요약 창 단위). 이 숫자가
        /// 리더 판단의 핵심 입력이다 — 0이면 "작업표시줄 말고는 아무도 우리를 안 덮는다"가 실증된다.</summary>
        private int _otherOccluderObservations;
        private int _bandScanCount;

        /// <summary>밴드 스캔 주기(초). 전경 창이 바뀐 틱에는 이 주기와 무관하게 <b>즉시</b> 스캔한다 —
        /// 밴드 순위를 뒤집는 것이 바로 그 활성화이기 때문이다. 주기 스캔은 활성화 없이 순위가 바뀌는
        /// 경로(새 topmost 창 생성 등)를 위한 그물이며, 1초면 24시간 상주 앱의 비용으로 충분히 싸다.</summary>
        private const float BandScanIntervalSeconds = 1f;

        /// <summary>밴드 가림 상세 로그 간격/수명. 가림은 <b>전이</b>에서만 찍히지만, 셸 팝업이 뜨고 지는
        /// 환경에서는 전이 자체가 잦을 수 있어 상한을 따로 둔다. 억제분은 60초 요약이 보고한다.</summary>
        private const float OcclusionDetailMinIntervalSeconds = 3f;
        private const int MaxOcclusionDetailLogs = 20;

        // ── 감시자 자기 비용 계측(2026-09-01) ───────────────────────────────────────
        // "진단이 렉의 원인인가"를 실기에서 **숫자로** 가르기 위한 최소 계측이다. Stopwatch의
        // 타임스탬프 조회는 QueryPerformanceCounter 한 번(수십 ns, 할당 0)이라 상시 켜 둬도 된다.
        private long _pollTicks, _logTicks, _worstLogTicks;
        private int _pollCount, _walkTotal, _walkWorst;

        /// <summary>
        /// 매 프레임 호출. 내부에서 주기를 지켜 실제 조회는 <see cref="WatchIntervalSeconds"/>마다만 한다.
        /// </summary>
        /// <param name="overlayHwnd">우리 오버레이 창 핸들(Win32WindowService가 확보해 넘긴다).</param>
        /// <param name="desiredTopmost">목표 상태.</param>
        /// <param name="suspended">전체화면 게임 감지로 숨어 있는 중인가(재적용을 보류한다).</param>
        /// <param name="reassertTopmost">topmost를 다시 거는 콜백. 이 파일은 창을 직접 건드리지 않는다.</param>
        /// <param name="describeOverlay">로그에 붙일 오버레이 창 상태 설명(라이브러리가 보고하는 값).</param>
        internal void Tick(float unscaledDeltaTime, IntPtr overlayHwnd, bool desiredTopmost,
            bool suspended, Action reassertTopmost, Func<string> describeOverlay)
        {
            // 상한 타이머는 실제 관측 여부와 무관하게 벽시계로 흘러야 한다(관측이 없으면 억제 집계가
            // 영원히 안 나가는 일을 막는다). 아래 세 줄은 float 덧셈 3회로, 비용이 없다.
            if (_detailCooldown > 0f) _detailCooldown -= unscaledDeltaTime;
            if (_occlusionDetailCooldown > 0f) _occlusionDetailCooldown -= unscaledDeltaTime;
            _bandScanTimer += unscaledDeltaTime;
            _summaryTimer += unscaledDeltaTime;

            _timer += unscaledDeltaTime;
            if (_timer < WatchIntervalSeconds) return;
            float elapsed = _timer;
            _timer = 0f;

            long pollStart = System.Diagnostics.Stopwatch.GetTimestamp();

            if (overlayHwnd == IntPtr.Zero)
            {
                if (!_noHandleLogged)
                {
                    _noHandleLogged = true;
                    Debug.LogWarning("[Z-ORDER] 오버레이 창 핸들이 아직 없습니다(_overlayHwnd == 0) — " +
                        "항상위 감시를 시작할 수 없습니다. 이 줄이 계속 남아 있으면 " +
                        "Win32WindowService.CreateOverlayWindow()가 MainWindowHandle을 못 잡은 것입니다.");
                }
                return;
            }

            if (!IsWindow(overlayHwnd))
            {
                if (!_staleHandleLogged)
                {
                    _staleHandleLogged = true;
                    Debug.LogWarning($"[Z-ORDER] 오버레이 창 핸들 0x{overlayHwnd.ToInt64():X}이(가) 더 이상 " +
                        "유효한 창이 아닙니다(IsWindow=false). 창이 재생성됐다는 뜻이며 항상위 감시가 " +
                        "엉뚱한 핸들을 보고 있습니다 — 이 줄이 보이면 핸들 재확보가 필요합니다.");
                }
                return;
            }
            _staleHandleLogged = false;

            bool aliveBefore = IsTopmostAlive(overlayHwnd);
            bool reasserted = TopmostRestorePolicy.ShouldReassert(desiredTopmost, aliveBefore, suspended);
            if (reasserted)
            {
                _reassertCount++;
                reassertTopmost?.Invoke();
            }

            // 재적용 직후의 진실을 다시 읽는다 — "되돌렸다"고 주장하지 않고 OS에게 확인받는다.
            bool aliveAfter = reasserted ? IsTopmostAlive(overlayHwnd) : aliveBefore;

            IntPtr foreground = GetForegroundWindow();
            double now = Time.realtimeSinceStartupAsDouble;

            TopmostWatchEvent evt = _tracker.Observe(desiredTopmost, aliveBefore, aliveAfter,
                foreground.ToInt64(), now, out double demotedForSeconds);

            // ── 흔적 기록 — 아래 밴드 스캔이 "가림 시작"을 잡았을 때 함께 찍힌다(리더 지시). ──
            //    할당 0: 링은 struct 배열이고 문자열은 로그 순간에만 만든다.
            if (suspended != _lastSuspended)
            {
                _lastSuspended = suspended;
                _trace.Record(suspended ? WatchTraceKind.SuspendedOn : WatchTraceKind.SuspendedOff, 0, now);
            }
            if (reasserted) _trace.Record(WatchTraceKind.Reasserted, overlayHwnd.ToInt64(), now);
            switch (evt)
            {
                case TopmostWatchEvent.DemotedAndRestored:
                case TopmostWatchEvent.Demoted:
                    _trace.Record(WatchTraceKind.TopmostLost, overlayHwnd.ToInt64(), now); break;
                case TopmostWatchEvent.Restored:
                    _trace.Record(WatchTraceKind.TopmostRestored, overlayHwnd.ToInt64(), now); break;
                case TopmostWatchEvent.ForegroundChanged:
                    _trace.Record(WatchTraceKind.ForegroundChanged, foreground.ToInt64(), now); break;
            }

            // ── 밴드 내 가림 감시 ──────────────────────────────────────────────────────
            //    전경이 바뀐 틱에는 주기를 기다리지 않는다: 밴드 순위를 뒤집는 것이 바로 그 활성화다.
            bool scanNow = _bandScanTimer >= BandScanIntervalSeconds
                           || evt == TopmostWatchEvent.ForegroundChanged;
            if (scanNow && aliveAfter)
            {
                _bandScanTimer = 0f;
                TickBandOcclusion(overlayHwnd, desiredTopmost, suspended, now, foreground);
            }

            _pollCount++;
            _pollTicks += System.Diagnostics.Stopwatch.GetTimestamp() - pollStart;
            TickSummary();

            if (evt == TopmostWatchEvent.None) return;

            // ★ 2026-09-01 2차 — 상세 한 줄은 상한을 통과해야만 남긴다. 통과하지 못한 사건은
            //   버리지 않고 종류별로 세어 두었다가 주기 요약이 반드시 보고한다(위 상수 문서 참고).
            if (_detailCooldown > 0f || _detailLogCount >= MaxDetailLogs)
            {
                switch (evt)
                {
                    case TopmostWatchEvent.DemotedAndRestored:
                    case TopmostWatchEvent.Demoted: _suppressedDemotion++; break;
                    case TopmostWatchEvent.Restored: _suppressedRestored++; break;
                    default: _suppressedForeground++; break;
                }
                return;
            }

            long logStart = System.Diagnostics.Stopwatch.GetTimestamp();
            _detailCooldown = DetailLogMinIntervalSeconds;
            _detailLogCount++;
            LogTransition(evt, overlayHwnd, foreground, aliveBefore, aliveAfter, reasserted,
                suspended, demotedForSeconds, elapsed, describeOverlay);
            long logTicks = System.Diagnostics.Stopwatch.GetTimestamp() - logStart;
            _logTicks += logTicks;
            if (logTicks > _worstLogTicks) _worstLogTicks = logTicks;
        }

        /// <summary>
        /// ★ 2026-09-01 3차 — <b>topmost 밴드 안에서 우리가 아래로 내려갔는가</b>를 관측하고,
        /// 전이 순간에만 한 줄 남긴다.
        ///
        /// <para>비트 감시(<see cref="TopmostRestorePolicy"/>)와 <b>완전히 다른 축</b>이다:
        /// 비트는 살아 있는데 순위만 뒤집히는 것이 실기 로그에서 실제로 관측된 상태다.</para>
        ///
        /// <para><b>여기서 창을 고치지 않는다.</b> 밴드 안에서 위로 올라가려면
        /// <c>SetWindowPos(HWND_TOPMOST)</c>뿐이고 그것은 작업표시줄/시작 메뉴 위에 영구히 올라앉는
        /// 행위라 원칙 2와 충돌한다. 대신 규칙
        /// <see cref="TopmostBandOcclusionPolicy.ShouldRaiseWithinBand"/>가 "지금이라면 올렸을까"를
        /// <b>모의 계산만</b> 해서 로그에 남긴다 — 배선 전에 실기 빈도를 먼저 받기 위해서다.</para>
        /// </summary>
        private void TickBandOcclusion(IntPtr overlayHwnd, bool desiredTopmost, bool suspended,
            double now, IntPtr foreground)
        {
            _bandScanCount++;
            _lastBandScanSeconds = now;
            BandOccluderKind kind = ScanBandOcclusion(overlayHwnd,
                out IntPtr topOccluder, out BandRect occluderRect, out int otherCount, out int walked);

            _walkTotal += walked;
            if (walked > _walkWorst) _walkWorst = walked;

            if (_bandScanInconclusive) return;   // 상한 초과 — 조용히 틀린 답을 내지 않는다.

            _lastOccluderHwnd = topOccluder;
            _lastOccluderRect = occluderRect;
            _lastOtherOccluderCount = otherCount;
            if (otherCount > 0) _otherOccluderObservations++;

            if (kind == BandOccluderKind.None) _trace.Record(WatchTraceKind.BandScanClear, 0, now);

            BandOcclusionEvent occEvt = _occlusion.Observe(kind, now, out double occludedFor);
            if (occEvt == BandOcclusionEvent.None) return;

            if (_occlusionDetailCooldown > 0f || _occlusionDetailLogCount >= MaxOcclusionDetailLogs)
            {
                _suppressedOcclusion++;
                return;
            }
            _occlusionDetailCooldown = OcclusionDetailMinIntervalSeconds;
            _occlusionDetailLogCount++;

            // ★ 로그 비용은 반드시 _logTicks로 간다. 여기서 _pollTicks에 섞으면 60초 요약의
            //   "감시 비용" 분해가 거짓말이 되고, 다음 라운드가 또 엉뚱한 곳을 의심하게 된다.
            long logStart = System.Diagnostics.Stopwatch.GetTimestamp();
            LogBandOcclusion(occEvt, kind, overlayHwnd, foreground, topOccluder, occluderRect,
                otherCount, walked, occludedFor, desiredTopmost, suspended, now);
            long logTicks = System.Diagnostics.Stopwatch.GetTimestamp() - logStart;
            _logTicks += logTicks;
            if (logTicks > _worstLogTicks) _worstLogTicks = logTicks;
        }

        /// <summary>
        /// 우리 위에 있는 <b>보이는</b> topmost 창을 찾는다.
        ///
        /// <para>순회는 <c>GetTopWindow(NULL)</c>에서 시작해 <b>우리 자신을 만나면 즉시 끝난다</b>.
        /// 우리가 topmost인 이상 우리 위의 창은 전부 topmost이므로 이 순회는 밴드의 앞부분만 지난다
        /// (실기 rank 19 → 20회). 앞 라운드의 818회 순회와는 성격이 다르다 — 그쪽은 <b>비-topmost</b>인
        /// 전경 창을 찾느라 목록 끝까지 갔다.</para>
        ///
        /// <para>거르는 것들과 그 이유:
        ///   · <c>IsWindowVisible == false</c> — 화면에 없다.
        ///   · <b>DWM 클로킹</b> — Win11 셸 창은 닫혀도 파괴되지 않고 클로킹된 채 남는데
        ///     <c>IsWindowVisible</c>은 true다. 안 거르면 "시작 메뉴가 항상 우리를 덮는다"는 상시 오보가 된다.
        ///   · 최소화 / 빈 사각형 / 우리 창과 겹치지 않는 사각형 — 가릴 수 없다.</para>
        /// </summary>
        /// <param name="topOccluder">우리를 덮는 창 중 <b>가장 위</b>의 것(없으면 0).</param>
        /// <param name="otherOccluderCount">그중 <b>하단 예약 막대가 아닌</b> 것의 개수.
        /// 시작 메뉴/알림 센터/플라이아웃이 여기에 잡힌다.</param>
        private BandOccluderKind ScanBandOcclusion(IntPtr overlay, out IntPtr topOccluder,
            out BandRect occluderRect, out int otherOccluderCount, out int walked)
        {
            topOccluder = IntPtr.Zero;
            occluderRect = default;
            otherOccluderCount = 0;
            walked = 0;
            _bandScanInconclusive = false;

            if (!GetWindowRect(overlay, out RECT ourRaw)) { _bandScanInconclusive = true; return BandOccluderKind.None; }
            BandRect ours = ToBandRect(ourRaw);

            if (!TryGetMonitorBand(overlay, out BandRect monitor, out int workBottom))
            {
                // 모니터 정보를 못 읽으면 "막대인지"를 가릴 수 없다. 이때 Other로 단정하면 거짓 경보가
                // 되므로 판정 자체를 보류한다.
                _bandScanInconclusive = true;
                return BandOccluderKind.None;
            }

            BandOccluderKind worst = BandOccluderKind.None;
            IntPtr h = GetTopWindow(IntPtr.Zero);
            bool foundSelf = false;

            while (h != IntPtr.Zero && walked < MaxZOrderWalk)
            {
                if (h == overlay) { foundSelf = true; break; }
                walked++;

                if (IsWindowVisible(h) && !IsIconic(h) && !IsCloaked(h) && GetWindowRect(h, out RECT r))
                {
                    BandRect rect = ToBandRect(r);
                    if (TopmostBandOcclusionPolicy.Overlaps(rect, ours))
                    {
                        BandOccluderKind kind =
                            TopmostBandOcclusionPolicy.Classify(rect, monitor, workBottom);
                        if (kind == BandOccluderKind.Other) otherOccluderCount++;

                        if (topOccluder == IntPtr.Zero)
                        {
                            topOccluder = h;
                            occluderRect = rect;
                        }
                        // Other가 있으면 그쪽이 진짜 버그이므로 종류 판정에서 우선한다.
                        if (worst != BandOccluderKind.Other) worst = kind;
                    }
                }

                h = GetWindow(h, GW_HWNDNEXT);
            }

            if (!foundSelf)
            {
                // 상한 안에서 우리 자신을 못 찾았다 = 우리가 밴드 깊숙이 내려갔거나 목록이 비정상이다.
                // 어느 쪽이든 "가림 없음"이라고 단정하면 안 된다.
                _bandScanInconclusive = true;
                return BandOccluderKind.None;
            }

            return worst;
        }

        /// <summary>모니터 전체 사각형과 작업영역 하단 — "하단 예약 막대" 판별의 유일한 근거.
        /// 기준 창을 <b>우리 창</b>으로 삼는다(멀티모니터에서 주 모니터를 쓰면 통째로 어긋난다).</summary>
        private static bool TryGetMonitorBand(IntPtr hWnd, out BandRect monitor, out int workAreaBottom)
        {
            monitor = default;
            workAreaBottom = 0;

            IntPtr h = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            if (h == IntPtr.Zero) return false;

            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(h, ref mi)) return false;

            monitor = ToBandRect(mi.rcMonitor);
            workAreaBottom = mi.rcWork.Bottom;
            return true;
        }

        private static BandRect ToBandRect(RECT r) => new BandRect(r.Left, r.Top, r.Right, r.Bottom);

        /// <summary>DWM 클로킹 여부. 조회 실패(구형 OS/권한)는 "클로킹 아님"으로 본다 —
        /// 진단이 조회 실패 때문에 창을 통째로 무시해 버리는 쪽이 더 나쁘다.</summary>
        private static bool IsCloaked(IntPtr hWnd)
        {
            try
            {
                return DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
                       && cloaked != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void LogBandOcclusion(BandOcclusionEvent occEvt, BandOccluderKind kind,
            IntPtr overlay, IntPtr foreground, IntPtr topOccluder, BandRect occluderRect,
            int otherCount, int walked, double occludedFor, bool desiredTopmost, bool suspended, double now)
        {
            string headline;
            switch (occEvt)
            {
                case BandOcclusionEvent.Started:
                    headline = kind == BandOccluderKind.ReservedBottomBar
                        ? "밴드 내 순위가 뒤집혔습니다 — 우리 위의 창은 **하단 예약 막대(작업표시줄)**입니다. " +
                          "macOS에서 Dock 창(layer 20)이 우리(layer 3) 위에 상시 있는 것과 같은 상태라, " +
                          "캐릭터가 막대 **윗면**에 서 있는 한 가려지지 않습니다(경보 아님)"
                        : "★ 밴드 내 가림 — 하단 예약 막대가 **아닌** topmost 창이 우리를 덮고 있습니다. " +
                          "이것이 '캐릭터가 사라진다'의 진짜 후보입니다";
                    break;
                case BandOcclusionEvent.KindChanged:
                    headline = kind == BandOccluderKind.Other
                        ? "★ 우리를 덮는 창이 작업표시줄에서 **다른 창**으로 바뀌었습니다"
                        : "우리를 덮는 창이 다시 하단 예약 막대뿐이 되었습니다";
                    break;
                default:
                    headline = $"밴드 내 가림 해소 — {occludedFor:F1}초 동안 우리 위에 창이 있었습니다";
                    break;
            }

            // ★ 리더 지시 — "우리가 아래로 내려간 순간의 직전 이벤트"가 반드시 이 줄에 있어야 한다.
            string trace = _trace.Describe(now);

            bool wouldRaise = TopmostBandOcclusionPolicy.ShouldRaiseWithinBand(
                desiredTopmost, suspended,
                barOccludesUs: kind == BandOccluderKind.ReservedBottomBar,
                characterInsideBar: false,      // ← 캐릭터 사각형 훅이 아직 없다(보고서의 후속 항목).
                otherOccluderCount: otherCount);

            Debug.Log($"[Z-ORDER/밴드] {headline} — " +
                $"덮는 창 {DescribeWindow(topOccluder)} rect={occluderRect}, " +
                $"막대 아닌 가림 {otherCount}개, 누적 에피소드 {_occlusion.EpisodeCount}회, " +
                $"우리 창 {DescribeWindow(overlay)} / 전경 창 {DescribeWindow(foreground)} / " +
                $"직전 이벤트: {trace} / 순회 {walked}개 / " +
                $"올림규칙(미배선) 모의판정={wouldRaise} " +
                $"(숨김중={suspended}, 목표항상위={desiredTopmost}).");
        }

        /// <summary>
        /// 주기 요약 — <b>억제된 사건</b>과 <b>감시자 자신의 실측 비용</b>을 한 줄로 남기고 집계를 비운다.
        ///
        /// <para>이 줄이 존재하는 이유는 두 가지다.
        /// (1) 상한 때문에 사건이 조용히 사라지면 진단이 거짓말을 하게 된다 — 억제분을 반드시 보고한다.
        /// (2) "진단 장치가 증상을 키우는가"를 <b>추측이 아니라 숫자로</b> 가른다. 이 줄의
        ///     <c>감시 비용</c>이 60초 중 수 ms 수준이면 이 감시자는 렉의 원인이 아니고,
        ///     수백 ms면 원인이다. 사용자는 로그 한 줄만 보내면 된다.</para>
        ///
        /// <para>비용: 60초에 한 번. 아무 일도 없었고 억제도 없었으면 <b>한 줄도 남기지 않는다</b>.</para>
        /// </summary>
        private void TickSummary()
        {
            if (_summaryTimer < SummaryIntervalSeconds) return;
            float window = _summaryTimer;
            _summaryTimer = 0f;

            int suppressed = _suppressedDemotion + _suppressedRestored + _suppressedForeground
                             + _suppressedOcclusion;
            // 조용한 정상 구간에서는 아무 것도 찍지 않는다(24시간 상주 규약).
            if (suppressed == 0 && _pollCount == 0) return;

            double toMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double pollMs = _pollTicks * toMs;
            double logMs = _logTicks * toMs;
            double worstLogMs = _worstLogTicks * toMs;
            double busyPercent = window > 0f ? (pollMs + logMs) / (window * 1000.0) * 100.0 : 0.0;

            if (suppressed > 0 || busyPercent >= 0.5 || _otherOccluderObservations > 0)
            {
                Debug.Log($"[Z-ORDER] 최근 {window:F0}초 요약 — " +
                    $"억제된 사건: 밀림 {_suppressedDemotion}회 / 복구 {_suppressedRestored}회 / " +
                    $"전경전환 {_suppressedForeground}회 / 밴드가림 {_suppressedOcclusion}회 " +
                    $"(상세는 {DetailLogMinIntervalSeconds:F0}초에 한 줄, " +
                    $"수명 {_detailLogCount}/{MaxDetailLogs}줄). " +
                    $"누적 밀림 {_tracker.DemotionCount}회 / 재적용 {_reassertCount}회. " +
                    // ★★ 리더 판단의 핵심 입력(2026-09-01 3차):
                    //    "지금 우리를 덮는 것이 작업표시줄뿐인가, 아니면 다른 창도 있는가."
                    //    아래 '막대 아닌 가림'이 계속 0이면 z-order를 건드릴 이유가 없다는 뜻이다.
                    $"밴드 스캔 {_bandScanCount}회 중 **막대 아닌 가림 {_otherOccluderObservations}회**, " +
                    $"현재 상태={DescribeOcclusionState()}. " +
                    // ★ 이것이 "진단이 렉의 원인인가"를 가르는 숫자다.
                    $"감시 비용: 관측 {_pollCount}회 {pollMs:F1}ms + 상세로그 {logMs:F1}ms" +
                    $"(최악 한 줄 {worstLogMs:F1}ms) = 창시간의 {busyPercent:F2}%. " +
                    // ★ '평균'을 쓰지 않는다: 순회는 이제 매 폴링이 아니라 밴드 스캔/전이 로그에서만
                    //   일어나므로 폴링 수로 나눈 평균은 항상 0에 가깝게 나와 실제 비용을 숨긴다.
                    $"z-order 순회 총 {_walkTotal}개(최악 한 번 {_walkWorst}개).");
            }

            if (_detailLogCount >= MaxDetailLogs && !_capNoticeLogged)
            {
                _capNoticeLogged = true;
                Debug.LogWarning($"[Z-ORDER] 상세 로그 수명 상한({MaxDetailLogs}줄)에 도달했습니다 — " +
                    "이후로는 위 60초 요약만 남깁니다. 이 줄이 보인다는 것은 항상위가 " +
                    "**지속적으로** 흔들리고 있다는 뜻이므로, 요약의 '누적 밀림' 증가 속도를 보세요.");
            }

            _suppressedDemotion = 0;
            _suppressedRestored = 0;
            _suppressedForeground = 0;
            _suppressedOcclusion = 0;
            _otherOccluderObservations = 0;
            _bandScanCount = 0;
            _pollTicks = 0;
            _logTicks = 0;
            _worstLogTicks = 0;
            _pollCount = 0;
            _walkTotal = 0;
            _walkWorst = 0;
        }

        /// <summary>지금 밴드 상태를 사람이 읽는 한 조각으로. 로그 두 곳이 같은 문장을 쓴다.</summary>
        private string DescribeOcclusionState()
        {
            if (double.IsNegativeInfinity(_lastBandScanSeconds))
            {
                return "미관측(항상위 비트가 살아 있을 때만 스캔한다 — 아직 한 번도 못 돌았다)";
            }

            double age = Time.realtimeSinceStartupAsDouble - _lastBandScanSeconds;
            string stamp = age > BandScanIntervalSeconds * 2 ? $" [{age:F1}초 전 관측]" : "";

            if (_bandScanInconclusive)
            {
                return $"판정 보류(순회 상한 {MaxZOrderWalk} 초과 또는 조회 실패){stamp}";
            }
            switch (_occlusion.Kind)
            {
                case BandOccluderKind.None: return $"우리 위에 아무 창도 없음{stamp}";
                case BandOccluderKind.ReservedBottomBar:
                    return $"하단 예약 막대만 위에 있음 0x{_lastOccluderHwnd.ToInt64():X} {_lastOccluderRect} " +
                           $"(macOS의 Dock 창과 같은 정상 상태){stamp}";
                default:
                    return $"★ 막대 아닌 창이 위에 있음 0x{_lastOccluderHwnd.ToInt64():X} {_lastOccluderRect} " +
                           $"(막대 아닌 가림 {_lastOtherOccluderCount}개){stamp}";
            }
        }

        /// <summary>OS가 보는 진실 — 라이브러리 캐시가 아니라 확장 스타일 비트를 직접 읽는다.</summary>
        private static bool IsTopmostAlive(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            return (exStyle & WS_EX_TOPMOST) != 0;
        }

        /// <summary>
        /// 기동 재적용 루프가 "이미 목표값이면 대입을 생략"할 때 쓰는 <b>실측</b> 되읽기.
        /// 핸들이 아직 없거나 유효하지 않으면 <c>false</c>를 돌려주고, 그 경우 호출자는 가드 없이
        /// 무조건 재적용해야 한다 — 모를 때는 거는 쪽이 안전하다.
        /// (라이브러리의 <c>isTopmost</c> 게터로 같은 판단을 하면 안 되는 이유는
        ///  <see cref="StickMate.Platform.TopmostRestorePolicy"/> 클래스 문서에 실측 근거와 함께 있다.)
        /// </summary>
        internal bool TryReadOsTopmost(IntPtr overlayHwnd, out bool topmost)
        {
            topmost = false;
            if (overlayHwnd == IntPtr.Zero || !IsWindow(overlayHwnd)) return false;
            topmost = IsTopmostAlive(overlayHwnd);
            return true;
        }

        /// <summary>
        /// 전이 한 줄. 리더 지시(2026-09-01)대로 <b>다음 신고 때 이 한 줄로 원인이 갈리게</b> 필요한
        /// 사실을 전부 담는다: 우리 WS_EX_TOPMOST 실측, 전경 창 핸들/프로세스/창 상태,
        /// z-order상 우리가 그 창보다 위인지 아래인지, 재적용 여부와 누적 횟수.
        /// </summary>
        private void LogTransition(TopmostWatchEvent evt, IntPtr overlay, IntPtr foreground,
            bool aliveBefore, bool aliveAfter, bool reasserted, bool suspended,
            double demotedForSeconds, float sampleElapsed, Func<string> describeOverlay)
        {
            string headline;
            switch (evt)
            {
                case TopmostWatchEvent.DemotedAndRestored:
                    headline = "밀림 감지 -> 같은 틱에 되돌렸습니다(사용자 눈에는 보이지 않아야 정상)";
                    break;
                case TopmostWatchEvent.Demoted:
                    headline = suspended
                        ? "밀림 감지 — 전체화면 게임 숨김 중이라 일부러 되돌리지 않습니다(원칙 2)"
                        : "★ 밀림 감지 — 되돌리기에 실패했습니다(재적용 후에도 WS_EX_TOPMOST가 없음)";
                    break;
                case TopmostWatchEvent.Restored:
                    headline = $"되돌리기 성공 — {demotedForSeconds * 1000.0:F0}ms 동안 밀려 있었습니다";
                    break;
                default:
                    headline = "전경 창 전환(항상위는 정상 유지 중)";
                    break;
            }

            string relation = ResolveRelation(overlay, foreground, aliveAfter, out int walked);

            Debug.Log($"[Z-ORDER] {headline} — " +
                $"우리 창 0x{overlay.ToInt64():X} WS_EX_TOPMOST(재적용 전={aliveBefore}, 후={aliveAfter}), " +
                $"재적용={(reasserted ? $"실행(누적 {_reassertCount}회)" : "안 함")}, " +
                $"누적 밀림 {_tracker.DemotionCount}회, {relation} (열거 {walked}개) / " +
                // ★ 2026-09-01 3차 — 위 relation은 "전경 창"과의 비교라 전경이 작업표시줄일 때
                //   ★를 찍고도 실제로는 가려지지 않는다(캐릭터는 막대 윗면에 선다). 그 오해를
                //   같은 줄에서 즉시 교정하려고 밴드 상태를 나란히 찍는다.
                $"밴드 상태: {DescribeOcclusionState()} / " +
                $"전경 창 {DescribeWindow(foreground)} / " +
                $"우리 창 {DescribeWindow(overlay)} / " +
                $"Screen.fullScreenMode={Screen.fullScreenMode}, Screen=({Screen.width}x{Screen.height}), " +
                $"숨김중={suspended}, 관측 간격 {sampleElapsed * 1000f:F0}ms" +
                (describeOverlay != null ? $" / 라이브러리 보고: {describeOverlay()}" : "") + ".");
        }

        /// <summary>
        /// "우리가 전경 창보다 위인가"를 <b>가능한 한 순회 없이</b> 판정한다.
        ///
        /// ============================================================================
        /// ★ 2026-09-01 2차 — 왜 순회를 없앴는가 (실기 로그 <c>(열거 818개)</c>의 정체)
        /// ============================================================================
        /// 이전 구현은 항상 <c>GetTopWindow(NULL)</c> + <c>GetWindow(GW_HWNDNEXT)</c>로 z-order를
        /// 처음부터 훑어 두 창의 순위를 찾았다. 그런데 <b>이 순회는 원리적으로 길 수밖에 없었다</b>:
        ///   · 우리 창은 topmost라 밴드 <b>맨 앞</b>에 있어 금방 찾힌다(실기 rank ≈ 15).
        ///   · 전경 창(엑셀 등)은 topmost가 아니라 <b>모든 topmost 창 뒤</b>에 있다. 그래서 순회는
        ///     항상 topmost 밴드 전체를 지나야 하고, 전경 창을 못 찾으면(<c>GetForegroundWindow</c>가
        ///     0을 주는 순간 — 잠금화면/UAC/포커스 공백 — 이 흔하다) <b>목록 끝까지</b> 간다.
        ///     실기의 818은 버그가 아니라 그 데스크톱의 진짜 최상위 창 수이고, 앱을 켜 둘수록 늘어난다.
        ///
        /// 그런데 <b>Windows의 z-order 규칙 자체가 답을 이미 알려준다</b>: <c>WS_EX_TOPMOST</c>인 창은
        /// 예외 없이 모든 비-topmost 창보다 앞이다. 그러므로 두 창의 topmost 비트만 읽으면
        /// (P/Invoke 1회) 대부분의 경우 순회 <b>0회</b>로 결론이 난다. 실제로 우리가 알고 싶은 것도
        /// "몇 번째인가"가 아니라 "가려지는가" 하나뿐이다.
        ///
        /// 같은 밴드에 함께 있을 때만 순회하며, 그때는 밴드 안 비교라 대개 수십 개에서 끝난다.
        /// 상한(<see cref="MaxZOrderWalk"/>)을 넘으면 <b>모른다고 정직하게</b> 보고한다.
        /// </summary>
        private string ResolveRelation(IntPtr overlay, IntPtr foreground, bool overlayTopmost, out int walked)
        {
            walked = 0;
            if (foreground == IntPtr.Zero) return "전경 창 없음(GetForegroundWindow=0) — 순위 비교 생략";
            if (overlay == foreground) return "전경 창이 우리 자신";

            bool foregroundTopmost = IsTopmostAlive(foreground);
            if (overlayTopmost != foregroundTopmost)
            {
                // 밴드가 다르면 순회할 필요가 없다 — topmost 밴드가 무조건 앞이다.
                return overlayTopmost
                    ? "우리가 위(topmost 밴드 / 전경 창은 일반 밴드 — 순회 0개)"
                    : "★ 우리가 아래(우리는 일반 밴드 / 전경 창이 topmost 밴드) — 캐릭터가 가려집니다";
            }

            ResolveZOrder(overlay, foreground, out int overlayRank, out int foregroundRank, out walked);
            _walkTotal += walked;
            if (walked > _walkWorst) _walkWorst = walked;

            if (overlayRank < 0 || foregroundRank < 0)
            {
                return $"같은 밴드지만 {MaxZOrderWalk}개 안에서 순위를 못 찾았습니다(판정 보류)";
            }
            return overlayRank < foregroundRank
                ? $"우리가 위(우리 #{overlayRank} < 전경 #{foregroundRank})"
                : $"★ 우리가 아래(우리 #{overlayRank} > 전경 #{foregroundRank}) — 캐릭터가 가려집니다";
        }

        /// <summary>
        /// z-order 앞에서 뒤로 순회하며 두 창의 순위를 찾는다(작을수록 앞 = 위).
        /// <c>GetTopWindow(NULL)</c> + <c>GetWindow(GW_HWNDNEXT)</c>는 최상위 창을 z-order 순서대로 준다.
        /// <b>같은 밴드 비교에서만</b> 불린다(<see cref="ResolveRelation"/> 참고). 상한은 반드시 둔다.
        /// </summary>
        private static void ResolveZOrder(IntPtr a, IntPtr b, out int rankA, out int rankB, out int walked)
        {
            rankA = -1;
            rankB = -1;
            walked = 0;

            IntPtr h = GetTopWindow(IntPtr.Zero);
            while (h != IntPtr.Zero && walked < MaxZOrderWalk)
            {
                if (h == a && rankA < 0) rankA = walked;
                if (h == b && rankB < 0) rankB = walked;
                walked++;
                if (rankA >= 0 && rankB >= 0) break;
                h = GetWindow(h, GW_HWNDNEXT);
            }
        }

        /// <summary>사람이 읽는 창 요약 — 핸들 / 프로세스명 / 사각형 / 창 상태.</summary>
        private static string DescribeWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "(없음)";
            if (!IsWindow(hWnd)) return $"0x{hWnd.ToInt64():X}(이미 파괴된 핸들)";

            GetWindowThreadProcessId(hWnd, out uint pid);
            string process = DescribeProcess(pid);

            if (!GetWindowRect(hWnd, out RECT r))
            {
                return $"0x{hWnd.ToInt64():X} {process} (사각형 조회 실패)";
            }

            string state;
            if (IsIconic(hWnd)) state = "최소화";
            else if (MatchesMonitor(hWnd, r)) state = "전체화면(모니터와 정확히 일치)";
            else if (IsZoomed(hWnd)) state = "최대화";
            else state = "일반";

            int ex = GetWindowLong(hWnd, GWL_EXSTYLE);
            return $"0x{hWnd.ToInt64():X} {process} ({r.Left},{r.Top} {r.Right - r.Left}x{r.Bottom - r.Top}) " +
                $"{state}, exStyle=0x{ex:X8}{((ex & WS_EX_TOPMOST) != 0 ? "(TOPMOST)" : "")}";
        }

        private static bool MatchesMonitor(IntPtr hWnd, RECT r)
        {
            IntPtr monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref mi)) return false;
            return r.Left == mi.rcMonitor.Left && r.Top == mi.rcMonitor.Top
                && r.Right == mi.rcMonitor.Right && r.Bottom == mi.rcMonitor.Bottom;
        }

        /// <summary>
        /// pid -> 프로세스명.
        ///
        /// <para>★ 2026-09-01 2차 — <b>작은 캐시를 붙였다.</b> <c>Process.GetProcessById</c>는 겉보기와
        /// 달리 싸지 않다(프로세스 스냅샷 조회 + 관리 객체 할당). 게다가 상대 프로세스가 응답하지 않으면
        /// <b>블로킹될 수 있어</b> 단일 프레임 스파이크의 후보가 된다. 사용자가 오가는 창은 보통 두세
        /// 개뿐이므로 4칸이면 사실상 전부 적중한다. 캐시가 stale해질 위험(pid 재사용)은 진단 문자열의
        /// 이름 하나가 틀리는 것뿐이라 감수한다 — 판정에는 쓰이지 않는 값이다.</para></summary>
        private static string DescribeProcess(uint pid)
        {
            if (pid == 0) return "pid?";

            for (int i = 0; i < ProcessNameCacheSize; i++)
            {
                if (_pidCache[i] == pid && _pidNameCache[i] != null) return _pidNameCache[i];
            }

            string described;
            try
            {
                using (var p = System.Diagnostics.Process.GetProcessById((int)pid))
                {
                    described = $"pid {pid} \"{p.ProcessName}\"";
                }
            }
            catch (Exception)
            {
                // 이미 종료됐거나 권한이 없는 프로세스 — 진단 문자열일 뿐이라 실패해도 그냥 넘어간다.
                described = $"pid {pid}";
            }

            _pidCache[_pidCacheCursor] = pid;
            _pidNameCache[_pidCacheCursor] = described;
            _pidCacheCursor = (_pidCacheCursor + 1) % ProcessNameCacheSize;
            return described;
        }

        private const int ProcessNameCacheSize = 4;
        private static readonly uint[] _pidCache = new uint[ProcessNameCacheSize];
        private static readonly string[] _pidNameCache = new string[ProcessNameCacheSize];
        private static int _pidCacheCursor;
    }
}
#endif
