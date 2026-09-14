using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>종료가 어디서 왔나.</summary>
    public enum AppShutdownTrigger
    {
        /// <summary>Unity <c>Application.quitting</c>(정상 종료).</summary>
        ApplicationQuitting = 0,
        /// <summary>OS 세션 종료(로그오프·시스템 종료·재시작) — Windows <c>WM_ENDSESSION</c>(wParam=TRUE).</summary>
        SessionEnding = 1,
    }

    /// <summary>종료 단계. <b>실행 순서는 <see cref="AppShutdownSequence.Order"/> 배열 순서다</b>(열거 값 순서가 아니다).</summary>
    public enum AppShutdownStep
    {
        /// <summary>작업표시줄 자동 숨김 원복(원칙 3 승인 예외의 조건 — "종료 시 원복"). <b>시스템을 바꾸는 단계 중 맨 앞.</b></summary>
        RestoreReservedBar = 0,
        /// <summary>동결 워치독 스레드 정지.</summary>
        StopFreezeWatchdog = 1,
        /// <summary>정상 종료 표지(<see cref="SessionExitMarker"/>, 디스크 동기화) — 앞 단계가 전부 돈 뒤에야 "정상 종료"다.</summary>
        MarkCleanExit = 2,
        /// <summary>
        /// ★ 4차 — 종료 시작 표지(<see cref="SessionExitMarker.WriteExitStarted"/>, 디스크 동기화 <b>없음</b>). 순서의 <b>맨 앞</b>.
        /// 원복 도중 끊겨도 다음 실행이 "비정상 종료"로 오판하지 않게 한다.
        /// </summary>
        MarkExitStarted = 3,
    }

    /// <summary>
    /// ★ 2026-09-14 — 종료 순서의 <b>단 한 곳</b>(플랫폼 중립).
    ///
    /// <para><b>왜 생겼나(persona-stress R-1).</b> 종료 훅이 파일마다 따로 <c>Application.quitting</c>에 붙어 있었고,
    /// 동결 워치독 정지는 합류를 최대 1초 기다렸다. 실행 순서가 구독 순서라는 우연에 기대고 있었고, 무엇보다
    /// Windows 세션 종료(<c>WM_ENDSESSION</c>)는 처리 직후 프로세스가 끊긴다 — 원복보다 워치독 대기가 먼저
    /// 오면 원복이 누락된다. 그래서 순서를 <see cref="Order"/> 한 줄로 명시하고 두 경로(정상 종료 / 세션 종료)가
    /// 같은 순서를 쓴다. <c>SessionEndShutdownTests</c>가 순서와 멱등을 실행으로 잠근다.</para>
    ///
    /// <para><b>범위 — "한 곳"은 이 순서의 단계에 한한다(3차 정정).</b> 2차 보고의 "quitting 구독 1곳"은 틀린 문장이었다.
    /// 이 순서가 모은 것은 <see cref="AppShutdownStep"/>의 단계뿐이고, 프로덕션에는 이 순서 밖의 종료 훅이 더 있다
    /// (파일별 결정·사유는 <c>SessionEndShutdownTests</c>의 종료 구독 대장). 새 훅이 생기면 그 대장이 빨개져 결정을 강제한다.</para>
    ///
    /// <para><b>멱등.</b> 두 경로가 모두 올 수 있다(세션 종료 뒤 Unity가 quitting까지 부르는 경우). 원복은
    /// <c>ReservedBarRevealDirector</c>의 "이번 실행이 바꿨는가" 상태로 두 번째 호출이 시스템에 쓰지 않고, 워치독
    /// 정지는 여러 번 불러도 안전하고, 표지는 같은 줄을 다시 쓸 뿐이다.</para>
    ///
    /// <para><b>표지 두 번(3차 D·4차).</b> 맨 끝 "정상 종료"(R-6: quitting에만 찍으면 Unity가 로그오프·시스템 종료에서
    /// quitting을 부르지 않는 경우 — 실기 미확인 — 매일 밤 Windows 종료가 비정상으로 오판된다). 4차에서 맨 앞에 "종료 시작"을
    /// 더했다: 셸이 먼저 내려가 원복 단계의 동기 셸 호출(<c>SHAppBarMessage</c>)이 돌아오지 않은 채 강제 종료되면(추정 경로)
    /// 맨 끝 표지가 영영 안 남는다 — 그때 "실행 중"이 아니라 "종료 시작"이 남아 비정상으로 오판하지 않는다.</para>
    ///
    /// <para><b>"종료 시작"이 원복보다 앞이어도 되는가 — 판단(4차).</b> R-1이 원복 앞에서 막은 것은 <b>기다리는 단계</b>(워치독 합류
    /// 최대 1초)다. 종료 시작 표지는 우리 폴더의 한 줄을 OS에 넘기고 끝나며 <c>Flush(true)</c>를 하지 않는다(프로세스가 끊겨도
    /// OS가 받은 쓰기는 남는다 — 잃는 것은 전원 차단뿐). 사용자 자산·시스템 설정을 바꾸지 않으므로 "원복이 시스템 변경 중 맨
    /// 앞"이라는 원칙 3 조건(<c>docs/TASKBAR_REVEAL.md</c> §2-2)과 충돌하지 않는다고 판단했다. <b>대가</b>: 원복 전에 파일 생성·쓰기
    /// 한 번(보안 소프트웨어 검사가 붙으면 수 ms 추정 — Windows 실측 없음). 디스크 쓰기 자체가 멈추는 환경이면 원복도 늦어진다
    /// — 그 경우는 흔적 파일(2-3)이 다음 실행에서 갚는다.</para>
    ///
    /// <para><b>진행 저장이 합류할 자리(coder 설계 예고, 4차 기록).</b> 종료 시작 표지 → 원복 → 워치독 정지 → <b>(진행 저장)</b> → 정상 종료 표지.
    /// 원복은 시스템을 바꾸는 단계 중 맨 앞이어야 하고(R-1), 정상 종료 표지는 모든 단계가 끝난 뒤여야 한다. 진행 저장 중 끊기면 표지는
    /// "종료 시작"으로 남는다(다음 실행이 비정상으로 보지 않는다 — 저장 누락 여부는 저장 계층이 따로 판단해야 한다).</para>
    /// </summary>
    public static class AppShutdownSequence
    {
        private static readonly AppShutdownStep[] s_order =
        {
            AppShutdownStep.MarkExitStarted,      // ★ 맨 앞 — 동기화 없는 한 줄. 원복 도중 끊겨도 "비정상"으로 남지 않는다(4차).
            AppShutdownStep.RestoreReservedBar,   // ★ 시스템을 바꾸는 단계 중 맨 앞 — 세션 종료는 곧 강제 종료된다.
            AppShutdownStep.StopFreezeWatchdog,
            AppShutdownStep.MarkCleanExit,        // ★ 맨 끝 — 앞 단계가 전부 돈 뒤에야 "정상 종료"다.
        };

        /// <summary>실행 순서(읽기 전용).</summary>
        public static IReadOnlyList<AppShutdownStep> Order => s_order;

        /// <summary>정상 종료에서 워치독 합류를 기다리는 최대 시간(ms). 종료 중 프레임이 멈춰 거짓 정지 줄이 찍히는 것을 막는다.</summary>
        public const int QuitWatchdogJoinMilliseconds = 1000;

        /// <summary>경로별 워치독 합류 대기. 세션 종료는 <b>기다리지 않는다</b>(신호만 보낸다) — 그 사이 프로세스가 끊긴다.</summary>
        public static int WatchdogJoinMilliseconds(AppShutdownTrigger trigger)
            => trigger == AppShutdownTrigger.SessionEnding ? 0 : QuitWatchdogJoinMilliseconds;

        private static bool s_quitHookInstalled;

        /// <summary>로그 꼬리표.</summary>
        public const string LogTag = "[종료순서]";

        /// <summary>테스트 전용 — 단계 실행을 가로챈다(순서 검증용).</summary>
        internal static Action<AppShutdownStep, AppShutdownTrigger> ExecutorOverrideForTesting;

        /// <summary>테스트 전용 — 세션 종료 처리 뒤의 앱 종료 요청을 가로챈다(에디터에서 <c>Application.Quit</c>은 무시되므로 관측이 필요하다).</summary>
        internal static Action QuitRequesterOverrideForTesting;

        /// <summary><c>Application.quitting</c>에 이 순서를 한 번만 건다. 여러 곳에서 불러도 안전하다.</summary>
        public static void EnsureQuitHookInstalled()
        {
            if (s_quitHookInstalled) return;
            s_quitHookInstalled = true;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting() => Run(AppShutdownTrigger.ApplicationQuitting);

        /// <summary>정해진 순서로 전 단계를 돈다. 한 단계가 던져도 다음 단계는 돈다(종료를 막지 않는다).</summary>
        public static void Run(AppShutdownTrigger trigger)
        {
            foreach (AppShutdownStep step in s_order)
            {
                try
                {
                    Action<AppShutdownStep, AppShutdownTrigger> overrideExecutor = ExecutorOverrideForTesting;
                    if (overrideExecutor != null) overrideExecutor(step, trigger);
                    else Execute(step, trigger);
                }
                catch (Exception)
                {
                    // 한 단계의 실패가 다음 단계를 막지 않는다 — 특히 세션 종료는 곧 프로세스가 끊긴다.
                }
            }
        }

        /// <summary>
        /// ★ 4차(coder 설계 교차 발견 X1) — Windows <c>WM_ENDSESSION</c>(wParam=TRUE) 처리의 <b>유일한</b> 진입점(트레이 창 프로시저가 동기로 부른다).
        /// 종료 순서를 돌고, <b>이어서 앱 종료를 요청한다</b>.
        ///
        /// <para><b>왜 종료를 요청하나.</b> MS 문서(WM_ENDSESSION): wParam이 TRUE면 "the session can end any time after all applications have
        /// returned from processing this message", lParam <c>ENDSESSION_CLOSEAPP</c>(0x1)는 "If wParam is TRUE, the application must shut down"
        /// (Restart Manager — 세션 자체는 계속된다), <c>ENDSESSION_CRITICAL</c>(0x40000000)는 "The application is forced to shut down".
        /// 즉 wParam=TRUE의 모든 경우에 이 앱은 끝나야 한다. 그런데 Restart Manager 경로나, 다른 앱이 종료를 붙잡아 사용자가 취소한
        /// 경로에서는 프로세스가 <b>살아남을 수 있다</b>(실기 미확인) — 그러면 작업표시줄은 이미 원복돼 실행 중인데 자동 숨김이 돌아온
        /// 상태가 되고(원칙 3 예외의 "실행 중에만 해제" 조건이 뒤집힘), 표지는 "정상 종료"라 이후 크래시가 정상 종료로 오판된다.</para>
        ///
        /// <para><b>복귀 경로를 두지 않은 이유.</b> 살아남은 뒤 자동 숨김 해제를 다시 거는 경로는, 복귀 직후 실제로 끊기면 작업표시줄이
        /// 앱 없이 드러난 채 남는다 — 끊길지 계속될지를 미리 알 방법이 없다. 그래서 <b>살아남은 상태 자체를 없앤다</b>: 원복·표지 뒤 앱이
        /// 스스로 끝난다. Unity <c>Application.quitting</c>이 같은 순서를 한 번 더 돌지만 멱등이다. 승인된 <c>ABM_SETSTATE</c> 형태는 늘지 않는다.</para>
        ///
        /// <para><b>lParam으로 분기하지 않는 이유.</b> 위 인용대로 wParam=TRUE면 플래그와 무관하게 끝나야 하므로 판정이 달라지지 않는다.
        /// 플래그는 원인 진단용으로 로그에만 남긴다(<see cref="SessionEndPolicy.DescribeEndSessionFlags"/>). 로그는 순서 <b>뒤에</b> 찍는다 — 원복 앞에 쓰기를 늘리지 않는다.</para>
        ///
        /// <para><b>남는 경로(정직하게).</b> <c>Application.wantsToQuit</c> 구독자가 종료를 취소하면 살아남는다 — 현재 프로덕션 0건이고, 생기면
        /// 종료 구독 대장(<c>SessionEndShutdownTests</c>)이 빨개져 결정을 강제한다.</para>
        /// </summary>
        public static void HandleSessionEnding(long endSessionFlags)
        {
            Run(AppShutdownTrigger.SessionEnding);
            try
            {
                Debug.Log($"{LogTag} Windows 세션 종료 통보(lParam={SessionEndPolicy.DescribeEndSessionFlags(endSessionFlags)}) — " +
                    "종료 순서를 마쳤고 앱 종료를 요청합니다(세션이 계속되더라도 원복된 채 도는 실행을 남기지 않기 위해서).");
            }
            catch (Exception)
            {
                // 로그 실패가 종료 요청을 막지 않는다.
            }
            try
            {
                Action requester = QuitRequesterOverrideForTesting;
                if (requester != null) requester();
                else Application.Quit();
            }
            catch (Exception)
            {
                // 종료 요청 실패로 창 프로시저가 던지지 않는다.
            }
        }

        private static void Execute(AppShutdownStep step, AppShutdownTrigger trigger)
        {
            switch (step)
            {
                case AppShutdownStep.MarkExitStarted:
                    // 기동이 표지를 켜지 않았으면(에디터·모바일·기동 실패) 아무것도 하지 않는다. 동기화 없음 — 원복을 기다리게 하지 않는다.
                    SessionExitMarker.WriteExitStarted(trigger);
                    break;
                case AppShutdownStep.RestoreReservedBar:
                    ReservedBarRevealDirector.RunShutdown(trigger);
                    break;
                case AppShutdownStep.StopFreezeWatchdog:
                    // 종료 중에는 프레임이 멈춘다 — 워치독을 세워 거짓 정지 줄을 막는다.
                    FreezeWatchdog.Stop(WatchdogJoinMilliseconds(trigger));
                    break;
                case AppShutdownStep.MarkCleanExit:
                    SessionExitMarker.WriteCleanExit(trigger);
                    break;
            }
        }
    }

    /// <summary>
    /// ★ 2026-09-14 — Windows 세션 종료 메시지 판정(순수 함수, 플랫폼 중립 위치 — 이 머신에서 실행해 검증한다).
    /// 값은 <c>winuser.h</c> 고정 리터럴이다.
    /// </summary>
    public static class SessionEndPolicy
    {
        /// <summary><c>WM_QUERYENDSESSION</c> — 건드리지 않는다(DefWindowProc가 TRUE = 종료 허용).</summary>
        public const uint WmQueryEndSession = 0x0011;

        /// <summary><c>WM_ENDSESSION</c>.</summary>
        public const uint WmEndSession = 0x0016;

        /// <summary><c>ENDSESSION_CLOSEAPP</c>(lParam 비트) — Restart Manager. MS 문서 값.</summary>
        public const long EndSessionCloseApp = 0x00000001;

        /// <summary><c>ENDSESSION_CRITICAL</c>(lParam 비트) — 강제 종료. MS 문서 값.</summary>
        public const long EndSessionCritical = 0x40000000;

        /// <summary><c>ENDSESSION_LOGOFF</c>(lParam 비트) — 로그오프. MS 문서 값.</summary>
        public const long EndSessionLogoff = 0x80000000;

        /// <summary>
        /// lParam을 사람이 읽는 한 줄로(진단 로그 전용 — 판정에 쓰지 않는다, <see cref="AppShutdownSequence.HandleSessionEnding"/> 문서).
        /// MS 문서: "this parameter is a bit mask … do not test for equality", 0이면 "the system is shutting down or restarting".
        /// </summary>
        public static string DescribeEndSessionFlags(long lParam)
        {
            long flags = lParam & 0xFFFFFFFFL;   // 64비트 LPARAM 부호 확장을 걷어낸다.
            string hex = "0x" + flags.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
            if (flags == 0) return hex + "(시스템 종료 또는 재시작 — 어느 쪽인지 알 수 없음)";
            var parts = new List<string>(4);
            if ((flags & EndSessionCloseApp) != 0) parts.Add("ENDSESSION_CLOSEAPP(Restart Manager — 세션은 계속됨)");
            if ((flags & EndSessionCritical) != 0) parts.Add("ENDSESSION_CRITICAL(강제 종료)");
            if ((flags & EndSessionLogoff) != 0) parts.Add("ENDSESSION_LOGOFF(로그오프)");
            long unknown = flags & ~(EndSessionCloseApp | EndSessionCritical | EndSessionLogoff);
            if (unknown != 0) parts.Add("알 수 없는 비트 0x" + unknown.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
            return hex + "(" + string.Join(" | ", parts) + ")";
        }

        /// <summary>
        /// 지금 세션이 정말 끝나는가. <c>WM_ENDSESSION</c>의 wParam이 TRUE일 때만이다 — FALSE는 "종료가 취소됐다"는
        /// 통지라 원복하면 안 된다(MS 문서: "If the session is being ended, this parameter is TRUE … Otherwise, it is FALSE").
        /// </summary>
        public static bool IsSessionEndingNow(uint message, long wParam) => message == WmEndSession && wParam != 0;

        /// <summary>
        /// 트레이를 끈 사용자에게 세션 종료 수신 창을 따로 세울 것인가(순수 규칙). 세션 종료에서 할 일이 있을 때만 —
        /// 이번 실행이 작업표시줄 자동 숨김을 <b>실제로 바꿨거나</b>, 정상 종료 표지가 <b>켜져 있을 때</b>(3차 D, R-6:
        /// 수신자가 없으면 트레이를 끈 사용자의 매일 밤 종료가 비정상으로 오판된다). 둘 다 아니면 숨은 창도 만들지 않는다.
        /// </summary>
        public static bool NeedsReceiverWithoutTray(bool trayOptedOut, bool reservedBarChangedThisSession, bool sessionExitMarkerStarted)
            => trayOptedOut && (reservedBarChangedThisSession || sessionExitMarkerStarted);

        /// <summary>
        /// ★ 4차(verify-change 3차 V5) — 트레이 파일이 부르는 <b>유일한</b> 판정. 사실 두 가지(이번 실행이 작업표시줄을 바꿨는가,
        /// 정상 종료 표지가 켜졌는가)를 <b>여기서</b> 읽는다. 3차에는 트레이 파일이 두 사실을 인자로 모아 넘겼는데, 표지 인자를
        /// <c>false</c>로 바꿔도 초록이었다(같은 파일 로그 문자열이 감사 니들을 채웠다). 이제 트레이는 옵트아웃 여부만 넘기고,
        /// 사실 수집은 이 중립 함수가 해서 EditMode가 <b>실행해</b> 잠근다.
        /// </summary>
        public static bool ShouldCreateReceiverWithoutTrayNow(bool trayOptedOut)
            => NeedsReceiverWithoutTray(trayOptedOut, ReservedBarRevealDirector.ChangedThisSession, SessionExitMarker.IsStarted);
    }
}
