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

    /// <summary>종료 단계. <b>배열 순서가 곧 실행 순서다.</b></summary>
    public enum AppShutdownStep
    {
        /// <summary>작업표시줄 자동 숨김 원복(원칙 3 승인 예외의 조건 — "종료 시 원복").</summary>
        RestoreReservedBar = 0,
        /// <summary>동결 워치독 스레드 정지.</summary>
        StopFreezeWatchdog = 1,
        /// <summary>정상 종료 표지(<see cref="SessionExitMarker"/>) — 앞 단계가 전부 돈 뒤에야 "정상 종료"다.</summary>
        MarkCleanExit = 2,
    }

    /// <summary>
    /// ★ 2026-09-14 — 종료 순서의 <b>단 한 곳</b>(플랫폼 중립).
    ///
    /// <para><b>왜 생겼나(persona-stress R-1).</b> 종료 훅이 파일마다 따로 <c>Application.quitting</c>에 붙어 있었고,
    /// 동결 워치독 정지는 합류를 최대 1초 기다렸다. 실행 순서가 구독 순서라는 우연에 기대고 있었고, 무엇보다
    /// Windows 세션 종료(<c>WM_ENDSESSION</c>)는 처리 직후 CSRSS가 프로세스를 끊는다 — 원복보다 워치독 대기가 먼저
    /// 오면 원복이 누락된다. 그래서 순서를 <see cref="Order"/> 한 줄로 명시하고 두 경로(정상 종료 / 세션 종료)가
    /// 같은 순서를 쓴다. <c>SessionEndShutdownTests</c>가 순서와 멱등을 실행으로 잠근다.</para>
    ///
    /// <para><b>범위 — "한 곳"은 이 순서의 단계에 한한다(3차 정정).</b> 2차 보고의 "quitting 구독 1곳"은 틀린 문장이었다.
    /// 이 순서가 모은 것은 <see cref="AppShutdownStep"/>의 단계(원복·워치독 정지·정상 종료 표지)뿐이고, 프로덕션에는
    /// 이 순서 밖의 <c>Application.quitting</c> 구독이 5곳 더 있다 — 트레이 아이콘 제거·숨은 창 파괴,
    /// 작업표시줄 버튼 제거기·시스템 오디오 탐침·가상 데스크톱 탐침의 COM 해제, 스팀 <c>SteamAPI.Shutdown</c>.
    /// 이들은 <b>세션 종료 경로에 넣지 않는다</b>(파일별 사유는 <c>SessionEndShutdownTests</c>의 종료 구독 대장). 공통 이유:
    /// 세션 종료면 그 정리의 상대(탐색기·작업표시줄·오디오 세션·스팀 클라이언트)도 함께 끝나 남는 흔적이 없고,
    /// <c>WM_ENDSESSION</c>은 보낸 메시지(SendMessage) 처리 중이라 COM 외부 호출이 실패할 수 있어(RPC_E_CANTCALLOUT_ININPUTSYNCCALL)
    /// 원복·표지 경로를 오염시킨다. 새 구독이 생기면 그 대장이 빨개져 결정을 강제한다.</para>
    ///
    /// <para><b>멱등.</b> 두 경로가 모두 올 수 있다(세션 종료 뒤 Unity가 quitting까지 부르는 경우). 원복은
    /// <c>ReservedBarRevealDirector</c>의 "이번 실행이 바꿨는가" 상태로 두 번째 호출이 시스템에 쓰지 않고, 워치독
    /// 정지는 여러 번 불러도 안전하고, 표지는 같은 줄을 다시 쓸 뿐이다.</para>
    ///
    /// <para><b>정상 종료 표지(3차 D, R-6).</b> 마지막 단계다. quitting에만 찍으면 Unity가 로그오프·시스템 종료에서
    /// quitting을 부르지 않는 경우(실기 미확인) 매일 밤 Windows 종료가 비정상으로 오판된다 — 그래서 이 순서 안에 둔다.
    /// 세션 종료에서도 마지막이어도 되는 이유: 모든 단계가 <c>WM_ENDSESSION</c> 처리 안에서 동기로 돌고, OS는 처리가
    /// 돌아온 <b>뒤에</b> 프로세스를 끊는다.</para>
    /// </summary>
    public static class AppShutdownSequence
    {
        private static readonly AppShutdownStep[] s_order =
        {
            AppShutdownStep.RestoreReservedBar,   // ★ 맨 앞 — 세션 종료는 곧 강제 종료된다.
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

        /// <summary>테스트 전용 — 단계 실행을 가로챈다(순서 검증용).</summary>
        internal static Action<AppShutdownStep, AppShutdownTrigger> ExecutorOverrideForTesting;

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

        private static void Execute(AppShutdownStep step, AppShutdownTrigger trigger)
        {
            switch (step)
            {
                case AppShutdownStep.RestoreReservedBar:
                    ReservedBarRevealDirector.RunShutdown(trigger);
                    break;
                case AppShutdownStep.StopFreezeWatchdog:
                    // 종료 중에는 프레임이 멈춘다 — 워치독을 세워 거짓 정지 줄을 막는다.
                    FreezeWatchdog.Stop(WatchdogJoinMilliseconds(trigger));
                    break;
                case AppShutdownStep.MarkCleanExit:
                    // 기동이 표지를 켜지 않았으면(에디터·모바일·기동 실패) 아무것도 하지 않는다.
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

        /// <summary>
        /// 지금 세션이 정말 끝나는가. <c>WM_ENDSESSION</c>의 wParam이 TRUE일 때만이다 — FALSE는 "종료가 취소됐다"는
        /// 통지라 원복하면 안 된다(MS 문서: "If the session is being ended, this parameter is TRUE … Otherwise, it is FALSE").
        /// </summary>
        public static bool IsSessionEndingNow(uint message, long wParam) => message == WmEndSession && wParam != 0;

        /// <summary>
        /// 트레이를 끈 사용자에게 세션 종료 수신 창을 따로 세울 것인가. 세션 종료에서 할 일이 있을 때만 —
        /// 이번 실행이 작업표시줄 자동 숨김을 <b>실제로 바꿨거나</b>, 정상 종료 표지가 <b>켜져 있을 때</b>(3차 D, R-6:
        /// 수신자가 없으면 트레이를 끈 사용자의 매일 밤 종료가 비정상으로 오판된다). 둘 다 아니면 숨은 창도 만들지 않는다.
        /// </summary>
        public static bool NeedsReceiverWithoutTray(bool trayOptedOut, bool reservedBarChangedThisSession, bool sessionExitMarkerStarted)
            => trayOptedOut && (reservedBarChangedThisSession || sessionExitMarkerStarted);
    }
}
