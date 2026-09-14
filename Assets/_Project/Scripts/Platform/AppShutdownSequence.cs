using System;
using System.Collections.Generic;
using System.Threading;
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
        /// 순서 도중 끊긴 실행을 다음 실행이 "비정상 종료"로 오판하지 않게 한다.
        /// </summary>
        MarkExitStarted = 3,
        /// <summary>
        /// ★ 5차 — 진행 저장 합류 자리(coder 설계 채택안). <see cref="AppShutdownSequence.RegisterSaveHandler"/>로 등록된 처리기를 부른다.
        /// 등록된 처리기가 없으면 아무것도 하지 않는다. 워치독 정지 뒤·정상 종료 표지 앞.
        /// </summary>
        FlushProgressSave = 4,
    }

    /// <summary>
    /// ★ 5차 — 진행 저장 합류 계약(단일 슬롯). 종료 순서의 <see cref="AppShutdownStep.FlushProgressSave"/> 단계가 부른다.
    ///
    /// <para><b>반환값</b>: 저장했거나 저장할 것이 없었으면 <c>true</c>, 저장을 시도했는데 실패했으면 <c>false</c>(진단 로그 한 줄만 남긴다).
    /// <b>실패해도 순서는 멈추지 않고 정상 종료 표지까지 간다</b> — 표지는 "종료 순서를 끝까지 돌았다(크래시가 아니다)"를 말할 뿐이고,
    /// 저장 누락 판단은 저장 계층의 몫이다. 던져도 같다.</para>
    ///
    /// <para><b>★ 5-b — 코드가 보장하는 것.</b>
    /// (1) <b>재진입 없음</b> — 처리기가 도는 동안 처리기는 다시 불리지 않는다(<see cref="AppShutdownSequence"/>의 걸쇠). 처리기가 기다리는 사이 같은 스레드에
    /// 다른 종료 순서가 끼어들면(Windows는 동기 호출이 돌아오기를 기다리는 동안 보낸 메시지를 처리할 수 있다 — 이 앱의 경로에서 실재하는지는 1차 문서로 미확인)
    /// 그 순서는 처리기를 <b>건너뛰고</b> 정상 종료 표지도 <b>쓰지 않는다</b> — 저장이 끝나기 전에 끊기면 다음 실행이 <c>ExitStartedNotFinished</c>로 읽어야 사실이다.
    /// 다른 스레드의 동시 호출도 같은 걸쇠로 건너뛴다. 처리기가 실패를 알리거나 던져도 걸쇠는 풀린다.
    /// (2) <b>순차</b> — 두 호출은 겹치지 않는다((1)의 결과).
    /// (3) <b>호출 시점</b> — 그 순서의 종료 시작 표지·작업표시줄 원복·워치독 정지 단계가 불린 <b>뒤</b>다(각 단계의 성공 여부와 무관).
    /// (4) ★ 5-c(리더 판정) <b>정상 종료 표지가 쓰인 뒤에는 불리지 않는다</b> — 같은 프로세스(마지막 기동 표지 이후)에서 정상 종료 표지가 디스크에 <b>실제로 쓰였으면</b>
    /// 그 뒤 어떤 순서도 처리기를 부르지 않는다(<see cref="AppShutdownSequence.ShouldRunStep"/> — 그 사실은 진행 저장 단계 <b>직전에</b> 읽는다). 그래서 "표지는
    /// <c>clean-exit</c>인데 저장 도중 프로세스가 죽었다"는 이 순서에서 생기지 않는다 — 표지가 저장보다 앞서 거짓말하지 않는다.</para>
    ///
    /// <para><b>보장하지 않는 것.</b>
    /// (a) <b>횟수</b> — 보통 <b>한 번</b>이다(세션 종료 처리가 표지를 쓰면 뒤따르는 quitting 순서는 처리기를 건너뛴다). 다시 불리는 경우는 전부 <b>표지가 아직 쓰이지 않은</b> 때다:
    /// 첫 순서가 정상 종료 표지를 <b>쓰지 못했을</b> 때(쓰기 실패 — 표지가 거짓말하지 않았으므로 다시 부른다, 5-c 리더 판단), 첫 순서의 처리기가 끝난 뒤 표지를 쓰기
    /// <b>전</b>에 다른 입구의 순서가 끼어들 때(중첩), 표지가 꺼진 실행(에디터·기동 실패 — <see cref="SessionExitMarker.IsStarted"/>가 거짓이라 표지가 영영 안 쓰인다).
    /// 코드가 횟수를 세지는 않는다. 걸쇠에 걸린 순서에서는 불리지 않는다.
    /// (b) <b>입구 순서</b> — 중첩되면 quitting 경로 호출이 세션 종료 경로 호출보다 먼저 올 수 있다(<paramref name="trigger"/>로 구별하라).
    /// (c) <b>표지 뒤 몇 프레임분의 진행</b> — 정상 종료 표지가 선 뒤(예: 세션 종료 처리 → 앱 종료 요청 → quitting 사이)의 진행은 이 처리기로 저장되지 않는다
    /// (리더 판정 5-c: 그 증분보다 "표지는 깨끗한데 저장 도중 죽음"을 만들지 않는 쪽이 우선). 저장 실패의 정직성은 저장 계층 진단의 몫이다.
    /// (d) <b>끝까지 돈다</b> — 세션 종료 경로에서는 처리기 도중에도 OS가 프로세스를 끝낼 수 있다(아래 시간). 그때 표지는 <c>exit-started</c>로 남는다.</para>
    ///
    /// <para><b>스레드·시간.</b> 두 입구 모두 Unity 메인 스레드다(quitting / 메인 스레드가 만든 창의 프로시저). 세션 종료 경로에서는 <c>WM_ENDSESSION</c> 처리
    /// <b>안에서</b> 불린다 — 동기로, 짧게 끝내라. ★ 5-b 정정: 5차까지 이 자리의 "MS 문서상 세션 종료 예산 5초"는 표의 한 칸만 참이었다. MS 문서
    /// (<see href="https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms700677(v=vs.85)">Application Shutdown Changes in Windows Vista</see> 표 1·2,
    /// <see href="https://learn.microsoft.com/en-us/windows/win32/shutdown/shutdown-changes-for-windows-vista">Shutdown Changes for Windows Vista</see>):
    /// 보이는 최상위 창도 차단 사유 문자열도 없는 앱은 <c>WM_ENDSESSION</c>에 5초를 받고, 넘기면 종료된다. 보이는 최상위 창이 있는 앱은 비위급 종료에서 필요한
    /// 만큼 쓸 수 있지만 5초 뒤 차단 UI가 뜨고(사용자가 끝낼 수 있다), 위급 종료에서는 30초 뒤 종료된다. 이 앱은 보이는 오버레이 창이 있지만 통보를 받는 창은
    /// 숨은 창이다 — Windows가 이 앱을 어느 칸으로 분류하는지, Windows 10/11의 실제 수치는 <b>실기 미확인</b>이다. 그러니 가장 짧은 5초를 상한으로 잡고,
    /// 그 안이라도 사용자 화면에 차단 UI가 뜨지 않게 짧게 끝내라.</para>
    /// </summary>
    public delegate bool AppShutdownSaveHandler(AppShutdownTrigger trigger);

    /// <summary>
    /// ★ 2026-09-14 — 종료 순서의 <b>단 한 곳</b>(플랫폼 중립).
    ///
    /// <para><b>왜 생겼나(persona-stress R-1).</b> 종료 훅이 파일마다 따로 <c>Application.quitting</c>에 붙어 있었고,
    /// 동결 워치독 정지는 합류를 최대 1초 기다렸다. 실행 순서가 구독 순서라는 우연에 기대고 있었고, 무엇보다
    /// Windows 세션 종료(<c>WM_ENDSESSION</c>)는 처리 직후 프로세스가 끊긴다 — 원복보다 워치독 대기가 먼저
    /// 오면 원복이 누락된다. 그래서 순서를 <see cref="Order"/> 한 줄로 명시하고 두 경로(정상 종료 / 세션 종료)가
    /// 같은 순서를 쓴다. <c>SessionEndShutdownTests</c>가 순서와 재실행 동작을 실행으로 잠근다.</para>
    ///
    /// <para><b>범위 — "한 곳"은 이 순서의 단계에 한한다(3차 정정).</b> 2차 보고의 "quitting 구독 1곳"은 틀린 문장이었다.
    /// 이 순서가 모은 것은 <see cref="AppShutdownStep"/>의 단계뿐이고, 프로덕션에는 이 순서 밖의 종료 훅이 더 있다
    /// (파일별 결정·사유는 <c>SessionEndShutdownTests</c>의 종료 구독 대장). 새 훅이 생기면 그 대장이 빨개져 결정을 강제한다.</para>
    ///
    /// <para><b>두 번 돌 때 — 단계마다 다르다(5차 정정).</b> 두 경로가 모두 올 수 있다(세션 종료를 처리한 뒤 앱 종료를 요청하므로
    /// Unity가 quitting에서 같은 순서를 한 번 더 돌린다). 4차까지 이 자리에 "표지는 같은 줄을 다시 쓸 뿐"이라 적혀 있었는데
    /// <b>거짓이었다</b>(verify-change 4차 하니스 D) — 두 번째 순서의 맨 앞 "종료 시작"이 첫 순서가 남긴 "정상 종료"를 덮어쓰고, 그 틈에
    /// 끊기면 다음 실행의 판정이 "원복 도중 끊김"과 구별되지 않았다. 5차부터:
    /// (1) 원복 — <c>ReservedBarRevealDirector</c>의 "이번 실행이 바꿨는가" 상태로, 이미 원복했으면 시스템에 쓰지 않고 <b>첫 원복이
    /// 실패했으면 두 번째 순서가 다시 시도한다</b>. (2) 워치독 정지 — 여러 번 불러도 안전하다. (3) 진행 저장 — ★ 5-c(리더 판정): 이번 실행이 정상 종료 표지를
    /// 이미 썼으면 <b>건너뛴다</b>(5-b까지는 다시 불렀고, 그 저장 도중 끊기면 표지가 저장보다 앞서 거짓말했다 — <see cref="AppShutdownSaveHandler"/> 보장 (4)).
    /// (4) 표지 두 단계 — <b>이번 실행이 정상 종료 표지를 이미 썼으면
    /// 건너뛴다</b>(<see cref="ShouldRunStep"/>). 첫 순서가 정상 종료 표지를 못 썼으면(쓰기 실패·끊김) 두 번째 순서가 다시 쓴다
    /// (쓰기 실패 경로는 <c>SessionEndShutdownTests.S1_…</c>가 실제로 실패시켜 잠근다). ★ 5-b: 그 사실은 순서 시작이 아니라 <b>각 단계 직전에</b> 읽는다.</para>
    ///
    /// <para><b>★ 5-b — 중첩(한 순서가 끝나기 전에 다른 입구의 순서가 같은 스레드에서 끼어들 때).</b> Windows는 동기 호출(작업표시줄 원복의 셸 호출 등)이
    /// 돌아오기를 기다리는 동안 보낸 메시지(<c>WM_ENDSESSION</c> 포함)를 처리할 수 있다. 이 앱의 호출 경로에서 실제로 그런지는 1차 문서로 확인하지 못했고
    /// (<c>SHAppBarMessage</c> 내부 대기 방식 비공개 — verify-change 5차 실행 탐침 A·B·E가 끼어들면 무엇이 깨지는지 보였다), <b>실재한다고 가정하고 막는다</b>.
    /// 충돌은 리더가 정한 우선순위로 푼다:
    /// (1) <c>WM_ENDSESSION</c> 처리가 <b>반환되기 전에</b> 원복이 끝나 있을 것 — 끼어든 순서도 원복 단계를 건너뛰지 않는다(바깥 쓰기가 아직 안 돌아왔어도 같은 값을
    /// 한 번 더 쓴다; 반환 뒤에는 언제든 프로세스가 끝날 수 있다). (2) 진행 저장 처리기는 재진입되지 않고, 저장이 도는 동안에는 어떤 순서도 정상 종료 표지를
    /// 쓰지 않으며, ★ 5-c 정상 종료 표지가 쓰인 뒤에는 어떤 순서도 저장을 부르지 않는다(걸쇠 + <see cref="ShouldRunStep"/>). (3) 표지의 <c>trigger=</c>는 그 줄을 실제로 쓴 순서의 것이다 — "이미 썼는가"를 표지 단계 직전에 읽어
    /// 재개된 바깥 순서가 안쪽 순서의 표지를 덮지 않는다. (4) 같은 값 중복 쓰기는 앞 셋을 해치지 않을 때만 줄인다 — 바깥 원복 쓰기가 돌아왔을 때 안쪽이 이미
    /// 원복·흔적 닫기를 마쳤으면 바깥은 흔적을 다시 쓰지 않는다(<c>ReservedBarRevealDirector.RunShutdown</c>).
    /// 끼어드는 자리별 결과(바깥 입구 2 × 자리 8)는 <c>SessionEndShutdownTests.재진입_…</c> 표가 실행으로 잠근다.</para>
    ///
    /// <para><b>표지 두 번(3차 D·4차).</b> 맨 끝 "정상 종료"(R-6: quitting에만 찍으면 Unity가 로그오프·시스템 종료에서
    /// quitting을 부르지 않는 경우 — 실기 미확인 — 매일 밤 Windows 종료가 비정상으로 오판된다). 4차에서 맨 앞에 "종료 시작"을
    /// 더했다: 셸이 먼저 내려가 원복 단계의 동기 셸 호출(<c>SHAppBarMessage</c>)이 돌아오지 않은 채 강제 종료되면(추정 경로)
    /// 맨 끝 표지가 영영 안 남는다 — 그때 "실행 중"이 아니라 "종료 시작"이 남아 비정상으로 오판하지 않는다. "종료 시작"이 남았다는 것은
    /// "맨 앞 표지와 맨 끝 표지 사이에서 끊겼다"까지만 말한다 — 원복·워치독 정지·진행 저장 중 어디서인지는 표지로 가를 수 없다
    /// (원복 여부는 흔적 파일 <c>active</c>가 말한다, <c>docs/TASKBAR_REVEAL.md</c> 6절 8번).</para>
    ///
    /// <para><b>"종료 시작"이 원복보다 앞이어도 되는가 — 판단(4차).</b> R-1이 원복 앞에서 막은 것은 <b>기다리는 단계</b>(워치독 합류
    /// 최대 1초)다. 종료 시작 표지는 우리 폴더의 한 줄을 OS에 넘기고 끝나며 <c>Flush(true)</c>를 하지 않는다(프로세스가 끊겨도
    /// OS가 받은 쓰기는 남는다 — 잃는 것은 전원 차단뿐). 사용자 자산·시스템 설정을 바꾸지 않으므로 "원복이 시스템 변경 중 맨
    /// 앞"이라는 원칙 3 조건(<c>docs/TASKBAR_REVEAL.md</c> §2-2)과 충돌하지 않는다고 판단했다. <b>대가</b>: 원복 전에 파일 생성·쓰기
    /// 한 번(보안 소프트웨어 검사가 붙으면 수 ms 추정 — Windows 실측 없음). 디스크 쓰기 자체가 멈추는 환경이면 원복도 늦어진다
    /// — 그 경우는 흔적 파일(2-3)이 다음 실행에서 갚는다.</para>
    ///
    /// <para><b>진행 저장 자리(5차, coder 설계 채택안).</b> 종료 시작 표지 → 원복 → 워치독 정지 → <b>진행 저장</b> → 정상 종료 표지.
    /// 원복은 시스템을 바꾸는 단계 중 맨 앞이어야 하고(R-1), 정상 종료 표지는 모든 단계가 끝난 뒤여야 한다. 진행 저장 중 끊기면 표지는
    /// "종료 시작"으로 남는다(다음 실행이 비정상으로 보지 않는다 — 저장 누락 여부는 저장 계층이 따로 판단해야 한다). 계약은
    /// <see cref="AppShutdownSaveHandler"/> 단일 슬롯이고, 의존 방향은 저장 계층(Interaction) → 이 순서(Platform)다.</para>
    /// </summary>
    public static class AppShutdownSequence
    {
        private static readonly AppShutdownStep[] s_order =
        {
            AppShutdownStep.MarkExitStarted,      // ★ 맨 앞 — 동기화 없는 한 줄. 순서 도중 끊겨도 "비정상"으로 남지 않는다(4차).
            AppShutdownStep.RestoreReservedBar,   // ★ 시스템을 바꾸는 단계 중 맨 앞 — 세션 종료는 곧 강제 종료된다.
            AppShutdownStep.StopFreezeWatchdog,
            AppShutdownStep.FlushProgressSave,    // ★ 5차 — 등록된 처리기가 없으면 아무것도 하지 않는다.
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

        /// <summary>진행 저장 처리기 슬롯(단일). <see cref="Interlocked"/>로만 바꾼다.</summary>
        private static AppShutdownSaveHandler s_saveHandler;

        /// <summary>★ 5-b — 진행 저장 처리기 걸쇠(0 = 쉼, 1 = 도는 중). <see cref="Interlocked"/>로만 바꾼다.</summary>
        private static int s_saveInProgress;

        /// <summary>★ 5-b — 진행 저장 처리기가 지금 도는 중인가. 이 동안에는 처리기를 다시 부르지 않고 정상 종료 표지도 쓰지 않는다(<see cref="ShouldRunStep"/>).</summary>
        internal static bool ProgressSaveInProgress => Volatile.Read(ref s_saveInProgress) != 0;

        /// <summary>로그 꼬리표.</summary>
        public const string LogTag = "[종료순서]";

        /// <summary>테스트 전용 — 단계 실행을 가로챈다(순서 검증용). 표지 단계 가드(<see cref="ShouldRunStep"/>)는 가로채기 <b>앞에서</b> 걸린다.</summary>
        internal static Action<AppShutdownStep, AppShutdownTrigger> ExecutorOverrideForTesting;

        /// <summary>
        /// ★ 5-b 테스트 전용 — 각 단계의 가드를 <b>평가하기 전에</b> 불린다(실제 실행기와 함께 쓴다). 앞 단계가 기다리는 사이 다른 종료 순서가 끼어드는 중첩을
        /// 단계 경계마다 흉내 내는 자리다. 프로덕션에서는 언제나 null이다.
        /// </summary>
        internal static Action<AppShutdownStep, AppShutdownTrigger> BeforeStepForTesting;

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

        // ------------------------------------------------------------------ 진행 저장 합류 계약 (5차)

        /// <summary>
        /// 진행 저장 처리기를 등록한다. <b>슬롯이 비었을 때만</b> <c>true</c> — 이미 누가(같은 처리기라도) 차지했거나 <c>null</c>이면 <c>false</c>이고
        /// 아무것도 바꾸지 않는다. 해제할 때 <b>등록한 대리자 인스턴스 그대로</b>를 넘겨야 하므로 필드에 보관하라
        /// (메서드 그룹을 다시 쓰면 새 인스턴스가 만들어져 <see cref="UnregisterSaveHandler"/>가 거절한다).
        /// </summary>
        public static bool RegisterSaveHandler(AppShutdownSaveHandler handler)
        {
            if (handler == null) return false;
            return Interlocked.CompareExchange(ref s_saveHandler, handler, null) == null;
        }

        /// <summary>
        /// 진행 저장 처리기를 해제한다. 슬롯에 든 것이 <b>같은 참조</b>일 때만 <c>true</c> — 같은 메서드를 가리키는 다른 대리자 인스턴스
        /// (<see cref="Delegate.Equals(object)"/>는 참)는 거절한다. 남이 등록한 처리기를 실수로 떼지 않게 하려는 것이다.
        /// </summary>
        public static bool UnregisterSaveHandler(AppShutdownSaveHandler handler)
        {
            if (handler == null) return false;
            return ReferenceEquals(Interlocked.CompareExchange(ref s_saveHandler, null, handler), handler);
        }

        /// <summary>테스트 전용 — 슬롯과 저장 걸쇠를 비운다.</summary>
        internal static void ResetSaveHandlerForTesting()
        {
            Interlocked.Exchange(ref s_saveHandler, null);
            Interlocked.Exchange(ref s_saveInProgress, 0);
        }

        // ------------------------------------------------------------------ 순서

        /// <summary>
        /// ★ 5차·5-b — 이 단계를 지금 돌리는가(순수 규칙). 호출부(<see cref="Run"/>)는 두 사실을 <b>그 단계 직전에</b> 읽어 넘긴다.
        /// <list type="bullet">
        /// <item>종료 시작 표지 — 이번 실행이 정상 종료 표지를 이미 썼으면 건너뛴다(쓴 표지를 "종료 시작"으로 덮지 않는다, 5차).</item>
        /// <item>정상 종료 표지 — 이미 썼으면 건너뛴다(5차). ★ 5-b: <b>진행 저장이 도는 중이어도</b> 건너뛴다 — 저장 처리기가 기다리는 사이 끼어든 순서가
        /// 저장이 끝나기 전에 "정상 종료"를 남기면, 그 뒤 끊겼을 때 다음 실행이 저장 미완을 정상 종료로 읽는다. 저장을 시작한 순서가 마친 뒤 쓴다.</item>
        /// <item>진행 저장 — ★ 5-c(리더 판정): 정상 종료 표지를 이미 썼으면 건너뛴다. 표지가 선 뒤에 저장이 돌다 끊기면 "표지는 깨끗한데 저장 도중 죽음"이 된다
        /// (표지가 저장보다 앞서 거짓말한다). 첫 순서의 표지는 그 순서의 저장 뒤에만 쓰이므로 잃는 것은 사이 몇 프레임분 증분뿐이다. 표지 쓰기가 <b>실패</b>했으면
        /// 기록이 서지 않으므로 다음 순서가 저장을 다시 부른다(표지가 거짓말하지 않았다). 재진입은 이 규칙이 아니라 걸쇠가 막는다.</item>
        /// <item>원복·워치독 정지 — 항상 돈다. 원복은 첫 순서가 실패했으면 두 번째 순서가 재시도해야 하고(기존 설계), 끼어든 순서도 건너뛰지 않는다
        /// (<c>WM_ENDSESSION</c> 반환 전 원복 완료 — 5-b 우선순위 1).</item>
        /// </list>
        /// </summary>
        /// <param name="step">단계.</param>
        /// <param name="cleanExitAlreadyMarkedThisRun">이번 실행(기동 표지 이후)이 정상 종료 표지를 이미 디스크에 썼는가 —
        /// <see cref="SessionExitMarker.CleanExitWrittenThisRun"/>.</param>
        /// <param name="progressSaveInProgress">진행 저장 처리기가 지금 도는 중인가 — <see cref="ProgressSaveInProgress"/>.</param>
        public static bool ShouldRunStep(AppShutdownStep step, bool cleanExitAlreadyMarkedThisRun, bool progressSaveInProgress)
        {
            switch (step)
            {
                case AppShutdownStep.MarkExitStarted:
                    return !cleanExitAlreadyMarkedThisRun;
                case AppShutdownStep.FlushProgressSave:
                    return !cleanExitAlreadyMarkedThisRun;   // ★ 5-c — 정상 종료 표지가 쓰인 뒤에는 처리기를 부르지 않는다.
                case AppShutdownStep.MarkCleanExit:
                    return !cleanExitAlreadyMarkedThisRun && !progressSaveInProgress;
                default:
                    return true;
            }
        }

        /// <summary>정해진 순서로 전 단계를 돈다. 한 단계가 던져도 다음 단계는 돈다(종료를 막지 않는다).</summary>
        public static void Run(AppShutdownTrigger trigger)
        {
            foreach (AppShutdownStep step in s_order)
            {
                Action<AppShutdownStep, AppShutdownTrigger> beforeStep = BeforeStepForTesting;
                beforeStep?.Invoke(step, trigger);

                // ★ 5-b — 두 사실은 단계마다 그 직전에 읽는다. 5차는 순서 시작 때 한 번 읽어, 이 순서가 기다리는 사이 끼어든 순서가 정상 종료 표지를
                //   쓰고 나면 재개된 이 순서가 낡은 "아직 안 썼다"로 그 줄을 자기 trigger로 덮었다(verify-change 5차 재진입 탐침 A·B).
                if (!ShouldRunStep(step, SessionExitMarker.CleanExitWrittenThisRun, ProgressSaveInProgress)) continue;
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
        /// ★ 5차(verify-change 4차 X1g) — 창 프로시저의 세션 종료 분기(메시지 판정 + 처리). Windows 트레이 호스트 창 프로시저가
        /// <b>맨 앞에서 이 함수 하나만</b> 부르고 돌려받은 값만 쓴다. <c>WM_ENDSESSION</c>이고 wParam이 참이면 <b>lParam 값과 무관하게</b>
        /// <see cref="HandleSessionEnding"/>을 정확히 한 번 부르고 <c>true</c>(프로시저는 0을 돌려준다), 아니면 아무것도 하지 않고 <c>false</c>
        /// (프로시저는 다음 분기·DefWindowProc로 넘긴다).
        ///
        /// <para><b>왜 옮겼나.</b> 4차에는 트레이 파일이 판정과 호출을 직접 이었고, 거기에 <c>if (lParam.ToInt64() == 0)</c> 한 조건을 붙여
        /// 로그오프·Restart Manager·강제 종료에서 원복·표지를 건너뛰는 변경이 전량 초록이었다(Windows 파일은 이 머신에서 실행되지 않는다).
        /// 분기 규칙을 여기로 옮겨 EditMode가 lParam 조합으로 <b>실행해</b> 잠그고, 트레이 쪽은 "이 한 줄만 부른다"를 형태로 본다.</para>
        /// </summary>
        public static bool TryHandleSessionEndMessage(uint message, long wParam, long lParam)
        {
            if (!SessionEndPolicy.IsSessionEndingNow(message, wParam)) return false;
            HandleSessionEnding(lParam);
            return true;
        }

        /// <summary>
        /// ★ 4차(coder 설계 교차 발견 X1) — Windows <c>WM_ENDSESSION</c>(wParam=TRUE) 처리(창 프로시저는 <see cref="TryHandleSessionEndMessage"/>를 거쳐 온다).
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
        /// 스스로 끝난다. 그 종료 요청으로 Unity <c>Application.quitting</c>이 같은 순서를 한 번 더 돈다 — 원복은 이미 끝났으면 시스템에
        /// 쓰지 않고, <b>표지 두 단계는 건너뛰어</b> 이 처리가 남긴 <c>clean-exit trigger=SessionEnding</c>이 그대로 남으며(5차,
        /// <see cref="ShouldRunStep"/>), ★ 5-c: 진행 저장 처리기도 부르지 않는다(정상 종료 표지가 이미 섰다 — 표지가 저장보다 앞서 거짓말하지 않게). 이 처리의 표지 쓰기가
        /// 실패했으면 quitting 순서가 진행 저장과 표지를 다시 돈다. 승인된 <c>ABM_SETSTATE</c> 형태는 늘지 않는다.</para>
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
                case AppShutdownStep.FlushProgressSave:
                    RunSaveHandler(trigger);
                    break;
                case AppShutdownStep.MarkCleanExit:
                    SessionExitMarker.WriteCleanExit(trigger);
                    break;
            }
        }

        private static void RunSaveHandler(AppShutdownTrigger trigger)
        {
            AppShutdownSaveHandler handler = Volatile.Read(ref s_saveHandler);
            if (handler == null) return;   // 등록 전 — 아무것도 하지 않는다(로그 없음: 24시간 상주 앱, 무의미한 줄 금지).

            // ★ 5-b 걸쇠 — 처리기가 이미 도는 중이면(그 처리기가 기다리는 사이 끼어든 중첩 순서, 또는 다른 스레드) 부르지 않는다.
            //   이 순서의 정상 종료 표지는 ShouldRunStep이 막는다 — 저장을 시작한 순서가 저장을 마친 뒤 쓴다.
            if (Interlocked.CompareExchange(ref s_saveInProgress, 1, 0) != 0)
            {
                try
                {
                    Debug.Log($"{LogTag} 진행 저장이 이미 진행 중입니다(경로={trigger}, 중첩 종료) — 이 순서는 저장과 정상 종료 표지를 건너뜁니다. " +
                        "저장을 시작한 순서가 마친 뒤 표지를 씁니다.");
                }
                catch (Exception)
                {
                    // 로그 실패가 다음 단계를 막지 않는다.
                }
                return;
            }
            try
            {
                if (!handler(trigger))
                {
                    Debug.LogWarning($"{LogTag} 진행 저장 처리기가 실패를 알렸습니다(경로={trigger}) — 종료 순서는 계속합니다.");
                }
            }
            catch (Exception e)
            {
                try
                {
                    Debug.LogWarning($"{LogTag} 진행 저장 처리기가 예외를 던졌습니다(경로={trigger}, {e.GetType().Name}) — 종료 순서는 계속합니다.");
                }
                catch (Exception)
                {
                    // 로그 실패가 다음 단계를 막지 않는다.
                }
            }
            finally
            {
                // 실패를 알려도·던져도 풀린다 — 안 풀리면 뒤따르는 순서가 처리기도 정상 종료 표지도 영영 건너뛴다.
                Interlocked.Exchange(ref s_saveInProgress, 0);
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
        /// ★ 4차(verify-change 3차 V5) — 두 사실(이번 실행이 작업표시줄을 바꿨는가, 정상 종료 표지가 켜졌는가)을 <b>여기서</b> 읽는다.
        /// 5차부터 트레이 파일은 이 함수도 직접 부르지 않는다 — <see cref="SessionEndReceiverGate.EnsureReceiverWithoutTray"/>가 부른다.
        /// </summary>
        public static bool ShouldCreateReceiverWithoutTrayNow(bool trayOptedOut)
            => NeedsReceiverWithoutTray(trayOptedOut, ReservedBarRevealDirector.ChangedThisSession, SessionExitMarker.IsStarted);
    }

    /// <summary>수신 창 확보 한 번의 결과(트레이 파일은 이 값으로 로그만 고른다).</summary>
    public enum SessionEndReceiverOutcome
    {
        /// <summary>이 프로세스에서 이미 판정했다 — 아무것도 하지 않았다.</summary>
        AlreadyEvaluated = 0,
        /// <summary>세울 필요가 없다(트레이가 켜져 있거나, 세션 종료에서 할 일이 없다) — 창을 만들지 않았다.</summary>
        NotNeeded = 1,
        /// <summary>숨은 수신 창을 세웠다.</summary>
        Created = 2,
        /// <summary>세워야 했는데 만들지 못했다(생성기가 거짓을 돌려줬거나 던졌다).</summary>
        CreateFailed = 3,
    }

    /// <summary>
    /// ★ 5차(verify-change 4차 V5g) — 트레이를 끈 사용자의 세션 종료 수신 창을 <b>세울지 결정하는 경로 전체</b>(한 번만 판정 · 옵트아웃 ·
    /// 두 사실 · 생성 호출). Windows 트레이 파일은 옵트아웃 여부와 "창을 만드는 손"만 넘기고 돌려받은 결과로 로그를 고를 뿐이다.
    ///
    /// <para><b>왜 옮겼나.</b> 4차에는 한 번만 판정하는 걸쇠와 판정 호출 줄이 트레이 파일에 있었고, 그 줄 <b>앞에</b>
    /// <c>if (!ReservedBarRevealDirector.ChangedThisSession) return;</c> 한 줄을 넣으면 "트레이를 끈 사용자의 밤 종료가 매일 비정상으로 오판"되는
    /// 결함(V5)이 되살아나는데 전량 초록이었다(판정 줄 형태만 봤다). 경로 전체를 여기로 옮겨 EditMode가 <b>실행해</b> 잠근다
    /// (<c>SessionEndShutdownTests</c>). 트레이 쪽 남은 몇 줄은 이 머신에서 실행되지 않으므로 허용 형태(모양 전체)로 본다.</para>
    ///
    /// <para><b>한 번만 판정하는 이유.</b> 두 사실은 기동(<c>BeforeSceneLoad</c> 원복 판정 / <c>AfterSceneLoad</c> 표지)에서 정해지고, 트레이 첫
    /// <c>Update</c>보다 먼저 끝난다. 매 프레임 다시 볼 이유가 없고, 24시간 상주 앱의 틱에 판정을 남기지 않는다.</para>
    /// </summary>
    public static class SessionEndReceiverGate
    {
        private static int s_evaluated;

        /// <param name="trayOptedOut">트레이 아이콘을 끈 사용자인가(<c>STICKMATE_NO_TRAY_ICON</c>).</param>
        /// <param name="createHiddenReceiverWindow">숨은 수신 창을 실제로 만드는 손(플랫폼 사실). 세워야 할 때만 한 번 불린다.
        /// 성공이면 <c>true</c>. 던지면 <see cref="SessionEndReceiverOutcome.CreateFailed"/>로 삼킨다(Tick을 깨지 않는다).</param>
        public static SessionEndReceiverOutcome EnsureReceiverWithoutTray(bool trayOptedOut, Func<bool> createHiddenReceiverWindow)
        {
            if (Interlocked.Exchange(ref s_evaluated, 1) != 0) return SessionEndReceiverOutcome.AlreadyEvaluated;
            if (!SessionEndPolicy.ShouldCreateReceiverWithoutTrayNow(trayOptedOut)) return SessionEndReceiverOutcome.NotNeeded;

            bool created;
            try
            {
                created = createHiddenReceiverWindow != null && createHiddenReceiverWindow();
            }
            catch (Exception)
            {
                created = false;
            }
            return created ? SessionEndReceiverOutcome.Created : SessionEndReceiverOutcome.CreateFailed;
        }

        /// <summary>테스트 전용 — "아직 판정하지 않았다"로 되돌린다.</summary>
        internal static void ResetForTesting() => Interlocked.Exchange(ref s_evaluated, 0);
    }
}
