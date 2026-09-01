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

        #endregion

        /// <summary>
        /// 감시 주기(초). 두 가지 요구가 부딪히는 값이라 근거를 남긴다:
        ///   · 24시간 상주 앱이므로 매 프레임 네이티브 호출은 금지(프로젝트 컨벤션).
        ///   · 그러나 "되돌리긴 하는데 눈에 보일 만큼 느리다"면 사용자에겐 버그가 안 고쳐진 것과 같다.
        /// 평상시 호출은 <c>GetWindowLong</c> + <c>GetForegroundWindow</c> <b>두 번뿐</b>이라(z-order
        /// 순위 계산 같은 비싼 작업은 전이 순간에만 한다) 0.1초는 사실상 공짜이면서 사람이 창을
        /// 전환하고 눈이 반응하기 전에 복구가 끝난다. 같은 파일의 디스플레이 토폴로지 관측
        /// (<c>TopologySampleIntervalSeconds</c>)과 같은 값이라 새 상수 등급을 만들지도 않는다.
        ///
        /// <para>실제 해상도는 프레임률에 의해 위에서 눌린다(FramePacing이 유휴 시 프레임을 낮춘다).
        /// 그래서 로그에는 "관측 주기 상한"이 아니라 <b>실측 경과 시간</b>을 찍는다.</para>
        /// </summary>
        private const float WatchIntervalSeconds = 0.1f;

        /// <summary>z-order 순위 순회 상한. 순회 도중 다른 프로세스가 z-order를 바꾸면 이론상 목록이
        /// 흔들릴 수 있으므로 무한 루프 방지용 안전 상한을 둔다(전형적인 데스크톱은 수백 개).</summary>
        private const int MaxZOrderWalk = 4000;

        private float _timer;
        private TopmostWatchdogTracker _tracker;
        private int _reassertCount;
        private bool _staleHandleLogged;
        private bool _noHandleLogged;

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
            _timer += unscaledDeltaTime;
            if (_timer < WatchIntervalSeconds) return;
            float elapsed = _timer;
            _timer = 0f;

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
            if (evt == TopmostWatchEvent.None) return;

            LogTransition(evt, overlayHwnd, foreground, aliveBefore, aliveAfter, reasserted,
                suspended, demotedForSeconds, elapsed, describeOverlay);
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

            ResolveZOrder(overlay, foreground, out int overlayRank, out int foregroundRank, out int walked);
            string relation;
            if (overlayRank < 0 || foregroundRank < 0) relation = "z-order 순위 조회 실패";
            else if (overlay == foreground) relation = "전경 창이 우리 자신";
            else relation = overlayRank < foregroundRank
                ? $"우리가 위(우리 #{overlayRank} < 전경 #{foregroundRank})"
                : $"★ 우리가 아래(우리 #{overlayRank} > 전경 #{foregroundRank}) — 캐릭터가 가려집니다";

            Debug.Log($"[Z-ORDER] {headline} — " +
                $"우리 창 0x{overlay.ToInt64():X} WS_EX_TOPMOST(재적용 전={aliveBefore}, 후={aliveAfter}), " +
                $"재적용={(reasserted ? $"실행(누적 {_reassertCount}회)" : "안 함")}, " +
                $"누적 밀림 {_tracker.DemotionCount}회, {relation} (열거 {walked}개) / " +
                $"전경 창 {DescribeWindow(foreground)} / " +
                $"우리 창 {DescribeWindow(overlay)} / " +
                $"Screen.fullScreenMode={Screen.fullScreenMode}, Screen=({Screen.width}x{Screen.height}), " +
                $"숨김중={suspended}, 관측 간격 {sampleElapsed * 1000f:F0}ms" +
                (describeOverlay != null ? $" / 라이브러리 보고: {describeOverlay()}" : "") + ".");
        }

        /// <summary>
        /// z-order 앞에서 뒤로 순회하며 두 창의 순위를 찾는다(작을수록 앞 = 위).
        /// <c>GetTopWindow(NULL)</c> + <c>GetWindow(GW_HWNDNEXT)</c>는 최상위 창을 z-order 순서대로 준다.
        /// 전이 순간에만 부르므로 비용은 신경 쓰지 않지만, 상한은 반드시 둔다.
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

        /// <summary>pid -> 프로세스명. 전이 순간에만 부르므로 관리 API의 할당을 감수한다
        /// (P/Invoke를 더 늘리지 않는 쪽을 택했다 — 이 파일의 감사 표면을 좁게 유지한다).</summary>
        private static string DescribeProcess(uint pid)
        {
            if (pid == 0) return "pid?";
            try
            {
                using (var p = System.Diagnostics.Process.GetProcessById((int)pid))
                {
                    return $"pid {pid} \"{p.ProcessName}\"";
                }
            }
            catch (Exception)
            {
                // 이미 종료됐거나 권한이 없는 프로세스 — 진단 문자열일 뿐이라 실패해도 그냥 넘어간다.
                return $"pid {pid}";
            }
        }
    }
}
#endif
