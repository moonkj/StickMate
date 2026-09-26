using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★★ <b>행동 명령창 헤더 한 줄 — 다섯 규칙을 실제 창에서 잰다</b> (2026-09-26).
    /// 정본: <c>docs/narrative/ACTION_POPOVER_HEADER_REASON.md</c> §0 · 구현: <c>SetStatusCaption</c>.
    ///
    /// ============================================================================
    /// 무엇이 거짓이었나
    /// ============================================================================
    /// 종전 헤더는 누를 수 있는 칸이 0이면 <b>이유를 묻지 않고</b> 규칙 4를 썼다. 네 칸이 전부
    /// <b>자리·대상 사유</b>(과녁 자리 없음 · 빈 자리 없음 · 작은 창 없음 · 전체화면 가드)로 회색이면
    /// 캐릭터는 한가히 걷는데 헤더가 <b>캐릭터 상태를 지어낸다</b> — 절대 불변 원칙 1이 금지한 모양이다.
    /// <b>신설된 것은 규칙 5 하나</b>이고, 규칙 4는 글자가 아니라 <b>조건</b>이 바뀌었다.
    ///
    /// ============================================================================
    /// 4와 5의 경계가 이 파일의 본체다
    /// ============================================================================
    /// 판정은 <b>상태만</b> 본다 — <b>연출 락은 보지 않는다</b>. 락 보유 13종 중 <b>크랙만</b> 금
    /// 수명까지 락을 들고 있어, 스윙 뒤 <b>약 2.6초 동안 캐릭터는 Idle로 돌아와 걷는데 락이 남는다</b>
    /// (설계 §3-5 락 보유자 전수 표). 아래 크랙 테스트가 그 구간이다.
    ///
    /// ============================================================================
    /// ★★ 2026-09-26 — <b>첫 판에서 이 픽스처가 빨강 3건을 냈다. 원인 셋과 처방</b>
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>준비해 놓고 다시 기다렸다</b>(직접 원인). 상태를 세우거나 «가능»을 확인한 뒤
    ///     <c>WaitForSecondsRealtime</c>으로 한 번 더 쉬었고, 그 사이 물리가 몸을 <c>Fall</c>·
    ///     <c>LandingCrouch</c>로 밀어 넣어 측정 시점의 전제가 무너졌다(관측된 실패 사유가 정확히
    ///     <c>상태=Fall</c> · <c>상태=LandingCrouch</c>였다).
    ///     ⇒ <b>측정 직전에 동기 갱신</b>(<see cref="ForceRefresh"/>)을 돌리고 그 뒤로는
    ///     <b>한 프레임도 흘리지 않는다</b>.</item>
    ///   <item><b>접지를 보지 않고 시작했다.</b> 몸이 정착하기 전에 시작하면 0.1초 안에 이탈한다.
    ///     ⇒ 낙서 라운드가 세운 형태를 그대로 빌린다 — <b>접지 게이트</b>
    ///     (<see cref="StandsStillOnGround"/>) + <b>3회 재시도</b>.</item>
    ///   <item><b><c>Assume</c>이 환경 사고를 숨겼다.</b> 상호배제 락은 <b>static이고 PlayMode는 도메인을
    ///     공유</b>하므로 <b>앞 순서의 다른 픽스처가 흘린 락</b>이 남으면 이 픽스처가 한꺼번에 흔들린다.
    ///     ⇒ <c>Assume</c>을 <b>전부 제거</b>했다(이 파일에 0개). 대신 <see cref="OpenPopover"/>가
    ///     <b>강제 해제를 먼저 돌리고</b> <c>Assert</c>로 못박는다 — 환경 사고를 <b>결정론적 준비</b>로
    ///     바꾸는 것이고, 「전제가 안 맞아 조용히 통과」도 같이 없어진다.</item>
    /// </list>
    ///
    /// <para>★ <b>전제 검사는 본 단언보다 항상 앞에 둔다.</b> 뒤에 두면 「전제 붕괴」가 「음성 대조
    /// 실패」로 보고되어 오진을 부른다(낙서 라운드 실제 사고).</para>
    ///
    /// <para>★ 문구는 <b>전부 프로덕션 상수 참조</b>다(문자열 복제 0). 대기는 <b>전부 벽시계</b>다 —
    /// 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로 돌아 프레임 수 예산이 실제로는 0.01초밖에
    /// 안 되는 경우가 있었다(CLAUDE.md).</para>
    /// </summary>
    public sealed class ActionCommandPopoverHeaderCaptionTests
    {
        private const string LogPrefix = "[헤더문구]";

        /// <summary>안전 폴링(0.25초)이 <b>한 번은</b> 돌게 하는 벽시계 여유.</summary>
        private const float PollSettleSeconds = 0.4f;

        /// <summary>«하나라도 누를 수 있게 되는» 것을 기다리는 벽시계 예산.</summary>
        private const float ReadyBudgetSeconds = 3f;

        /// <summary>«접지 + 비바쁨»이 <b>끊기지 않고</b> 이어져야 하는 시간.</summary>
        private const float SettleSeconds = 0.2f;

        /// <summary>안정 상태를 기다리는 기본 벽시계 예산.</summary>
        private const float StabilizeBudgetSeconds = 3f;

        /// <summary>크랙 잔여 구간 안에서 쓰는 <b>짧은</b> 예산 — 금 수명(3초)을 넘기면 표본이 무효가 된다.
        /// 스윙 0.4 + 이 예산 0.8 = 1.2초로 여유가 1.8초 남는다.</summary>
        private const float CrackStabilizeBudgetSeconds = 0.8f;

        /// <summary>물리 이탈은 <b>가드 판정이 아니라 전제 붕괴</b>라서 재시도한다(낙서 라운드 형태).</summary>
        private const int StartAttempts = 3;

        private static readonly Rect AnchorRect = new Rect(400f, 400f, 44f, 44f);

        private ActionCommandPopover _popover;
        private StickmanAgent _agent;
        private bool _lockHeld;

        [OneTimeSetUp]
        public void RequireIsolatedSaveFile()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다. " +
                "이대로 진행하면 개발자의 실제 저장 파일을 읽고 씁니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        /// <summary>상호배제 락은 <b>단언이 중간에 터져도</b> 반드시 풀린다 — 남기면 뒤따르는 모든
        /// 테스트가 «전부 불가»로 보여 원인 불명 연쇄 실패가 된다.</summary>
        [TearDown]
        public void ReleaseSpectacleLock() => ReleaseStrandedLock("픽스처 정리");

        // ====================================================================
        // 규칙 1 — 가출 중
        // ====================================================================

        /// <summary>
        /// 가출은 <b>사다리 맨 위</b>다. 가출 중에 캐릭터를 숨긴 경우에도 사용자에게 필요한 것은
        /// [돌아와!] 칩이고, 헤더가 그 칩을 설명해야 한다(설계 §0 순서 근거).
        ///
        /// <para>★ <c>IsRunawayActive</c>는 <b>상태에서만</b> 파생되므로(«상태 == Runaway») 상태를 직접
        /// 세워 결정론적으로 잰다 — 진짜 가출 감독을 돌리면 추첨·타이머에 의존해 불안정해진다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 규칙1_가출_중이면_가출_문구다()
        {
            yield return OpenPopover();

            // 전이 이벤트가 <b>같은 호출 안에서</b> RefreshContent를 돌린다(OnStateTransitioned) —
            // 여기부터 단언까지 <b>yield가 없다</b>.
            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Runaway, isForcedInterrupt: true);

            Assert.AreEqual(StickmanStateId.Runaway, CurrentState(),
                $"{LogPrefix} 전제 — 가출 상태로 세우지 못했습니다({Snapshot()}).");
            Assert.AreEqual(ActionCommandPopover.RunawayCaption, _popover.StatusCaption,
                $"{LogPrefix} 가출 중인데 헤더가 다르게 말합니다({Snapshot()}).");
            Assert.IsTrue(_popover.IsRecallChipVisible,
                $"{LogPrefix} 가출 중인데 [돌아와!] 칩이 없습니다 — 헤더가 설명할 대상이 사라집니다.");

            _popover.Close("테스트 종료");
            yield return null;
        }

        // ====================================================================
        // 규칙 2 — 캐릭터가 안 보임
        // ====================================================================

        /// <summary>
        /// 숨김은 가출 다음이다. 문구는 <b>새로 짓지 않고</b> 타일 네 칸이 이미 쓰는
        /// <see cref="HiddenCharacterCommandGate.HiddenReason"/>을 그대로 쓴다 — 같은 사실을 두 곳에서
        /// 다른 말로 적으면 design-narrative가 글자를 고칠 때 한쪽만 남는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 규칙2_캐릭터가_숨어_있으면_숨었다고_말한다()
        {
            yield return OpenPopover();
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 전제 — 새 씬인데 캐릭터가 이미 숨어 있습니다.");

            _agent.SetUserHidden(true, "PlayMode 테스트 — 헤더 규칙 2");
            yield return new WaitForSecondsRealtime(PollSettleSeconds);
            ForceRefresh("규칙 2 측정");   // ← 이 뒤로 yield 없음.

            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 전제 — 사용자 명시 숨김이 서지 않았습니다.");
            Assert.AreEqual(HiddenCharacterCommandGate.HiddenReason, _popover.StatusCaption,
                $"{LogPrefix} 캐릭터가 숨었는데 헤더가 다르게 말합니다({Snapshot()}).");

            // 숨김은 규칙 4·5보다 <b>앞</b>이다 — 뒤로 밀리면 «왜 안 되지»의 답이 사라진다.
            Assert.AreNotEqual(ActionCommandPopover.BusyCaption, _popover.StatusCaption,
                $"{LogPrefix} 숨김이 규칙 4에 가려졌습니다({Snapshot()}).");
            Assert.AreNotEqual(ActionCommandPopover.NothingAvailableCaption, _popover.StatusCaption,
                $"{LogPrefix} 숨김이 규칙 5에 가려졌습니다({Snapshot()}).");

            _agent.SetUserHidden(false, "PlayMode 테스트 — 원복");
            _popover.Close("테스트 종료");
            yield return null;
        }

        // ====================================================================
        // 규칙 3 — 하나라도 누를 수 있음
        // ====================================================================

        [UnityTest]
        public IEnumerator 규칙3_누를_수_있는_칸이_있으면_시킬_수_있다고_말한다()
        {
            yield return OpenPopover();

            float deadline = Time.realtimeSinceStartup + ReadyBudgetSeconds;   // 벽시계 예산.
            while (Time.realtimeSinceStartup < deadline && ReadyCount() == 0) yield return null;

            ForceRefresh("규칙 3 측정");   // ← 이 뒤로 yield 없음.

            // ★ 전제를 본 단언보다 <b>앞</b>에 둔다. 그리고 Assume이 아니라 <b>Assert</b>다 —
            //   «접지 + 비바쁨 + 락 비움»을 결정론적으로 준비한 뒤에도 네 칸이 전부 회색이면 그것은
            //   조용히 넘길 환경 잡음이 아니라 <b>보고할 사실</b>이다.
            Assert.Greater(ReadyCount(), 0,
                $"{LogPrefix} 전제 — 준비를 마쳤는데도 {ReadyBudgetSeconds:F0}초 안에 누를 수 있는 칸이 " +
                $"하나도 생기지 않았습니다({Snapshot()}).");

            Assert.AreEqual(ActionCommandPopover.ReadyCaption, _popover.StatusCaption,
                $"{LogPrefix} 누를 수 있는 칸이 있는데 헤더가 다르게 말합니다({Snapshot()}).");

            _popover.Close("테스트 종료");
            yield return null;
        }

        // ====================================================================
        // 규칙 4 — 전부 불가 + 캐릭터 상태가 바쁨
        // ====================================================================

        /// <summary>
        /// 캐릭터가 실제로 <b>다른 일을 하는 중</b>일 때는 이 문구가 <b>참</b>이다. 이 라운드가 문구를
        /// 없앤 것이 아니라 <b>조건을 붙인 것</b>임을 여기서 못박는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 규칙4_캐릭터_상태가_바쁘면_다른_일_하는_중이라고_말한다()
        {
            yield return OpenPopover();

            // 상태 하나만 세운다(락은 잡지 않는다) — 규칙 4가 <b>상태로</b> 성립함을 보이는 것이 목적이다.
            // 전이 이벤트가 같은 호출 안에서 갱신하므로 여기부터 단언까지 yield가 없다.
            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);

            Assert.IsTrue(ActionCommandPopover.IsBusyHeaderState(CurrentState()),
                $"{LogPrefix} 전제 — 세운 상태가 바쁨으로 분류되지 않습니다({Snapshot()}).");
            Assert.AreEqual(0, ReadyCount(),
                $"{LogPrefix} 전제 — 캐릭터가 바쁜데 누를 수 있는 칸이 남아 있습니다({Snapshot()}).");

            Assert.AreEqual(ActionCommandPopover.BusyCaption, _popover.StatusCaption,
                $"{LogPrefix} 캐릭터가 실제로 바쁜데 헤더가 그렇게 말하지 않습니다({Snapshot()}). " +
                "이 라운드는 규칙 4에 조건을 붙였을 뿐이고 문구를 없앤 것이 아닙니다.");

            _popover.Close("테스트 종료");
            yield return null;
        }

        // ====================================================================
        // 규칙 5 — 전부 불가인데 캐릭터는 한가하다 (★ 이 라운드가 고친 것)
        // ====================================================================

        /// <summary>
        /// ★★ <b>4와 5의 경계.</b> 네 칸이 전부 <b>캐릭터 상태와 무관한 사유</b>로 회색이고 캐릭터가
        /// Idle·Walk면 규칙 5여야 한다. 종전 트리에서는 이 자리에서 규칙 4가 떴고 그것이 신고된 거짓이다.
        ///
        /// <para>결정론을 위해 네 Director를 걷어 전원 «이 빌드에는 없는 기능이에요»로 만든다(설계 §2
        /// 마지막 행). 실제 배포에서 이 장면을 만드는 것은 <b>자리·대상 사유</b>이지만 그쪽은 창 배치·
        /// 바탕화면 상태에 의존해 배치모드에서 결정론적으로 세울 수 없다 — <b>헤더는 사유의 종류를 보지
        /// 않으므로</b>(readyCount와 상태만 본다) 이 경로가 같은 분기를 잰다. 그 사실 자체를 아래에서
        /// <b>사유로</b> 확인한다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 규칙5_전부_회색인데_캐릭터가_한가하면_시킬_게_없다고_말한다()
        {
            yield return OpenPopover();

            DestroyCommandDirectors();
            yield return null;                      // 파괴는 프레임 끝에 확정된다.
            yield return PrepareCalmMeasurement("규칙 5", StabilizeBudgetSeconds);   // ← 이 뒤로 yield 없음.

            // ---- 전제: 캐릭터는 한가하고, 네 칸은 전부 회색이며, 그 사유는 상태 사유가 아니다 ----
            Assert.IsFalse(ActionCommandPopover.IsBusyHeaderState(CurrentState()),
                $"{LogPrefix} 전제 — 캐릭터가 한가한 상태로 머물지 못했습니다({Snapshot()}).");
            Assert.AreEqual(0, ReadyCount(), $"{LogPrefix} 전제 — 네 칸이 전부 회색이 아닙니다({Snapshot()}).");

            foreach (ActionCommandPopover.Command command in Commands())
            {
                Assert.AreEqual(CommandAvailability.MissingReason, _popover.CommandReason(command),
                    $"{LogPrefix} 전제 — [{command}]의 사유가 배선 누락 사유가 아닙니다({Snapshot()}). " +
                    "이 테스트는 «캐릭터 상태와 무관한 사유»로 회색인 장면을 재려는 것입니다.");
                Assert.AreNotEqual(StickMateDisplayNames.BusyText(CurrentState()),
                    _popover.CommandReason(command),
                    $"{LogPrefix} 전제 — [{command}]가 상태 바쁨 사유로 회색입니다({Snapshot()}).");
            }

            // ---- 본체 ----
            Assert.AreEqual(ActionCommandPopover.NothingAvailableCaption, _popover.StatusCaption,
                $"{LogPrefix} ★ 네 칸이 전부 회색인데 캐릭터는 한가합니다 — 규칙 5 문구여야 합니다({Snapshot()}).");
            Assert.AreNotEqual(ActionCommandPopover.BusyCaption, _popover.StatusCaption,
                $"{LogPrefix} ★ 헤더가 캐릭터 상태를 지어냈습니다({Snapshot()}) — 캐릭터는 아무 일도 하지 " +
                "않는데 «다른 일 하는 중»이라고 말합니다. 수정 전 트리에서는 정확히 여기가 빨개집니다.");

            _popover.Close("테스트 종료");
            yield return null;
        }

        /// <summary>
        /// ★★ <b>크랙 금이 남은 약 2.6초.</b> 락은 크랙이 쥐고 있는데 캐릭터는 이미 Idle로 돌아와 있다 —
        /// 락 보유 13종 중 <b>크랙만</b> 상태보다 락이 오래 남는다(설계 §3-5 전수 표). 헤더가 락을 보면
        /// 이 구간에서 한가한 캐릭터에게 «다른 일 하는 중»을 띄운다.
        ///
        /// <para>★ 락을 <b>테스트가 직접</b> 잡아 결정론적으로 재현한다 — 진짜 크랙은 전경 창을
        /// 요구하므로 배치모드에서 발동 자체가 불안정하다. 잰 것은 <b>«락만 남고 상태는 한가»라는
        /// 조건</b>이고, 그 조건이 규칙 4를 부르지 않아야 한다는 것이 설계 판정의 내용 그 자체다.
        /// 실제 크랙은 여기에 오버레이 활성이 더해져 크랙 칸의 사유만 달라진다 — 헤더 분기에는 영향이
        /// 없다(readyCount 동일).</para>
        ///
        /// <para>두 경계는 <b>설정을 참조</b>하고 숫자를 베끼지 않는다. 대기는 벽시계다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 규칙5_크랙_금이_남은_구간은_락만_남아도_시킬_게_없다고_말한다()
        {
            yield return OpenPopover();
            Assert.IsNotNull(_agent.Config, $"{LogPrefix} 전제 — 에이전트에 StickConfig가 없습니다.");

            float swing = _agent.Config.windowCrashSwingDuration;
            float overlay = _agent.Config.windowCrashOverlayDurationSeconds;
            Assert.Greater(overlay, swing,
                $"{LogPrefix} 전제 — 금 수명({overlay:F2}초)이 스윙({swing:F2}초)보다 길어야 «락만 남는» " +
                "잔여 구간이 존재합니다. 설정이 뒤집혔다면 이 라운드의 근거(§3-5)를 다시 봐야 합니다.");

            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.WindowCrash, this),
                $"{LogPrefix} 테스트가 크랙 락을 잡지 못했습니다({Snapshot()}).");
            _lockHeld = true;
            float started = Time.realtimeSinceStartup;

            // ---- ⑵ 불변식(음성 대조) — 칸 문장은 <b>스윙 구간에도</b> 같아야 한다 ----------------
            // ★ 「문장은 그것이 뜰 수 있는 구간 <b>전체</b>에서 참이어야 한다」를 잠그는 자리다.
            //   잔여 구간에서만 재면 스윙 동안 다른 문장이 떠도 이 픽스처는 초록이다.
            ForceRefresh("크랙 스윙 구간 측정");   // ← 이 뒤로 yield 없음.
            float swingElapsed = Time.realtimeSinceStartup - started;
            Assert.Less(swingElapsed, swing,
                $"{LogPrefix} 전제 — 스윙 구간({swing:F2}초) 안에서 재지 못했습니다(경과 {swingElapsed:F3}초).");
            Assert.AreEqual(StickMateDisplayNames.WindowCrashBusyText,
                _popover.CommandReason(ActionCommandPopover.Command.Archery),
                $"{LogPrefix} ⑵ 스윙 구간의 형제 칸 문장이 크랙 전용 문장이 아닙니다({Snapshot()}). " +
                "같은 락이 쥐고 있는 동안에는 구간이 달라도 같은 문장이어야 합니다.");

            yield return new WaitForSecondsRealtime(swing);
            // 스윙이 끝난 «잔여 구간»에서 측정한다. 재안정은 짧은 예산으로 — 금 수명을 넘기면 표본 무효다.
            yield return PrepareCalmMeasurement("크랙 잔여 구간", CrackStabilizeBudgetSeconds);   // ← 이 뒤로 yield 없음.
            float elapsed = Time.realtimeSinceStartup - started;

            // ---- 전제 (전부 본 단언보다 앞) ----
            Assert.Greater(elapsed, swing,
                $"{LogPrefix} 전제 — 스윙({swing:F2}초)이 끝나기 전에 쟀습니다(경과 {elapsed:F2}초).");
            Assert.Less(elapsed, overlay,
                $"{LogPrefix} 전제 — 잔여 구간을 지나쳤습니다(경과 {elapsed:F2}초 ≥ 금 수명 {overlay:F2}초). " +
                "기계가 느렸거나 재안정이 예산을 다 썼습니다 — 이 표본은 크랙 잔여 구간을 잰 것이 아닙니다.");
            Assert.IsTrue(SpectacleEventLock.IsActive, $"{LogPrefix} 전제 — 락이 풀렸습니다({Snapshot()}).");
            Assert.AreEqual(SpectacleEventKind.WindowCrash, SpectacleEventLock.ActiveKind,
                $"{LogPrefix} 전제 — 락의 주인 연출이 크랙이 아닙니다({Snapshot()}).");
            Assert.IsFalse(ActionCommandPopover.IsBusyHeaderState(CurrentState()),
                $"{LogPrefix} 전제 — 캐릭터가 한가한 상태로 머물지 못했습니다({Snapshot()}).");
            Assert.AreEqual(0, ReadyCount(), $"{LogPrefix} 전제 — 락 중인데 누를 수 있는 칸이 있습니다({Snapshot()}).");

            // ---- ⑴ 양성 대조 + 잔여 구간 문장: 칸들이 실제로 «크랙 락» 세계를 보고 있다 ----
            //   두 창구로 <b>같은 칸</b>을 잰다: 함수 경로(BusyText)와 상수 경로(WindowCrashBusyText).
            //   둘이 갈라지면 「배열 칸 덮어쓰기」 계약(완성 문장을 미리 만들어 두고 같은 인스턴스를
            //   돌려준다)이 깨진 것이고, 그때는 여기서 먼저 빨개진다.
            Assert.AreEqual(StickMateDisplayNames.BusyText(SpectacleEventKind.WindowCrash),
                _popover.CommandReason(ActionCommandPopover.Command.Archery),
                $"{LogPrefix} 전제 — 칸 사유가 크랙 락 사유가 아닙니다({Snapshot()}). 다른 사유로 우연히 " +
                "회색이면 이 표본은 크랙 잔여 구간을 잰 것이 아닙니다.");
            Assert.AreEqual(StickMateDisplayNames.WindowCrashBusyText,
                _popover.CommandReason(ActionCommandPopover.Command.Archery),
                $"{LogPrefix} ⑴ 금 잔여 구간의 형제 칸 문장이 크랙 전용 문장이 아닙니다({Snapshot()}).");

            // ---- 본체 ----
            Assert.AreEqual(ActionCommandPopover.NothingAvailableCaption, _popover.StatusCaption,
                $"{LogPrefix} ★ 크랙 금 잔여 구간인데 규칙 5 문구가 아닙니다({Snapshot()}).");
            Assert.AreNotEqual(ActionCommandPopover.BusyCaption, _popover.StatusCaption,
                $"{LogPrefix} ★ 헤더가 <b>연출 락</b>을 캐릭터 상태로 읽었습니다({Snapshot()}). 락은 앱의 " +
                "배타 토큰이고 캐릭터 상태가 아닙니다 — 이 구간에서 캐릭터는 한가히 걷습니다(설계 §3-5).");

            _popover.Close("테스트 종료");
            yield return null;
        }

        // ====================================================================
        // ⑶ 푸터 한 줄 — 빈 띠가 조용히 초록이 되지 않게
        // ====================================================================

        /// <summary>
        /// 푸터 한 줄이 <b>실제로 글자를 갖고 있는가</b> — <b>존재 단언</b>이다.
        ///
        /// <para>★ <b>부재로 짜면 안 된다</b>: 이 자리는 2026-09-26까지 상자만 있고 <c>.text</c> 대입이
        /// <b>0건</b>이라 28pt <b>빈 띠</b>였다. 「무엇이 없다」를 재는 테스트였다면 그 빈 띠가 그대로
        /// <b>조용히 초록</b>이었을 것이다.</para>
        ///
        /// <para>문구는 <b>프로덕션 상수를 참조</b>한다 — design-narrative가 글자를 바꾸는 날 이 단언이
        /// 조용히 초록으로 남지 않는다(글자는 확정 대기다).</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 푸터_한_줄은_실제로_글자를_갖는다()
        {
            yield return OpenPopover();
            ForceRefresh("푸터 측정");   // ← 이 뒤로 yield 없음.

            Assert.IsNotEmpty(ActionCommandPopover.FooterHintText,
                $"{LogPrefix} 푸터 상수가 비었습니다 — 상수를 비우면 아래 단언이 «빈 문자열 == 빈 문자열»로 " +
                "통과해 빈 띠를 그대로 승인합니다.");
            Assert.AreEqual(ActionCommandPopover.FooterHintText, _popover.FooterHint,
                $"{LogPrefix} 푸터 한 줄에 글자가 없습니다(실제=«{_popover.FooterHint}»). 세로 예산은 이미 " +
                "이 줄을 세고 있으므로, 글자가 없으면 28pt 빈 띠가 창을 아래로 무겁게 만들 뿐입니다.");

            // 음성 대조 짝 — 모든 Text에 같은 글자를 넣어도 통과하는 경로를 막는다.
            Assert.AreNotEqual(ActionCommandPopover.FooterHintText, _popover.StatusCaption,
                $"{LogPrefix} 헤더와 푸터가 같은 문장입니다({Snapshot()}) — 두 자리가 서로 다른 것을 " +
                "말해야 이 단언이 «푸터를 실제로 읽었다»를 증명합니다.");

            _popover.Close("테스트 종료");
            yield return null;
        }

        // ====================================================================
        // 준비 — 결정론적으로 만들고, 측정 직전에 동기 갱신한다
        // ====================================================================

        /// <summary>
        /// 씬을 올리고, <b>남은 락을 강제로 풀고</b>, <b>접지가 안정될 때까지 기다린 뒤</b> 창을 연다.
        ///
        /// <para>★ 락 해제를 <c>Assume</c>이 아니라 <b>강제 해제 + <c>Assert</c></b>로 두는 이유:
        /// 상호배제 락은 static이고 PlayMode는 도메인을 공유한다. 우리 <c>TearDown</c>은 <b>우리 뒤</b>만
        /// 치울 뿐 <b>앞 순서 픽스처의 누출</b>은 못 막는다. 그 누출이 남으면 이 픽스처 전체가 한꺼번에
        /// 흔들리고, <c>Assume</c>은 그것을 <b>조용한 건너뜀</b>으로 덮는다.</para>
        /// </summary>
        private IEnumerator OpenPopover()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var found = UnityEngine.Object.FindObjectsByType<ActionCommandPopover>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length,
                $"{LogPrefix} 씬의 ActionCommandPopover가 {found.Length}개입니다 — 1개여야 합니다.");
            _popover = found[0];

            _agent = UnityEngine.Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            Assert.IsNotNull(_agent.Blackboard, $"{LogPrefix} 에이전트에 블랙보드가 없습니다.");
            Assert.IsNotNull(_agent.Blackboard.Machine, $"{LogPrefix} 에이전트에 상태 기계가 없습니다.");

            ReleaseStrandedLock("픽스처 준비 — 앞 순서가 흘린 락");
            Assert.IsFalse(SpectacleEventLock.IsActive,
                $"{LogPrefix} 전제 — 강제 해제를 했는데도 상호배제 락이 남아 있습니다" +
                $"(주인={SpectacleEventLock.CurrentOwner}). 이 상태로는 네 칸이 전부 락 사유로 회색이라 " +
                "헤더 규칙 3·4·5를 가를 수 없습니다.");

            yield return WaitUntilStandingStill("창 열기 전", loud: true, budget: StabilizeBudgetSeconds);

            _popover.Open(AnchorRect, "PlayMode 테스트 — 헤더 문구");
            yield return new WaitForSecondsRealtime(PollSettleSeconds);
            Assert.IsTrue(_popover.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
        }

        /// <summary>
        /// «접지 + 비바쁨»을 만들고 <b>측정 직전 동기 갱신</b>까지 마친다.
        /// <b>반환 뒤에는 단언까지 yield가 없어야 한다</b> — 한 프레임만 흘려도 물리가 상태를 바꾼다.
        ///
        /// <para>물리 이탈은 <b>가드 판정이 아니라 전제 붕괴</b>라서 <see cref="StartAttempts"/>회
        /// 재시도한다(낙서 라운드가 같은 함정을 겪고 세운 형태).</para>
        /// </summary>
        private IEnumerator PrepareCalmMeasurement(string what, float budget)
        {
            for (int attempt = 1; attempt <= StartAttempts; attempt++)
            {
                bool last = attempt == StartAttempts;
                yield return WaitUntilStandingStill($"{what}(시도 {attempt})", loud: last, budget: budget);
                if (StandsStillOnGround() || last) break;

                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                yield return new WaitForSecondsRealtime(PollSettleSeconds);
            }

            ForceRefresh(what);
        }

        /// <summary>«발판 위 정지»가 <see cref="SettleSeconds"/> 동안 <b>끊기지 않고</b> 이어질 때까지.
        /// <para>★ 락은 보지 않는다 — 크랙 테스트는 <b>자기가 락을 쥔 채</b> 이 함수를 쓴다.</para></summary>
        private IEnumerator WaitUntilStandingStill(string what, bool loud, float budget)
        {
            float stableSince = -1f;
            float deadline = Time.realtimeSinceStartup + budget;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (StandsStillOnGround()) { if (stableSince < 0f) stableSince = Time.realtimeSinceStartup; }
                else stableSince = -1f;
                if (stableSince >= 0f && Time.realtimeSinceStartup - stableSince >= SettleSeconds) yield break;
                yield return null;
            }

            if (!loud) yield break;
            Assert.IsTrue(StandsStillOnGround(),
                $"{LogPrefix} {what} 전제 — {budget:F1}초 안에 «발판 위 정지»가 {SettleSeconds:F2}초 " +
                $"이어지지 않았습니다({Snapshot()}).");
        }

        /// <summary>명령을 받을 수 있는 <b>발판 위 정지</b> 상태인가. 접지를 보지 않고 시작하면 몸이
        /// 정착 중일 때 시작해 0.1초 안에 <c>Fall</c>로 밀려난다(첫 판 실측 실패 사유가 그것이었다).</summary>
        private bool StandsStillOnGround() =>
            !ActionCommandPopover.IsBusyHeaderState(CurrentState()) && _agent.Blackboard.SenseGround().Grounded;

        /// <summary>열려 있는 창에 <b>동기 갱신</b>을 한 번 먹인다 — <c>PopoverPanel.Open</c>은 이미 열려
        /// 있으면 <c>RefreshContent()</c>만 돌리고 즉시 돌아온다. 0.25초 폴링을 기다리며 <b>프레임을
        /// 흘리지 않기 위한</b> 창구다(그 기다림이 첫 판 빨강 3건의 직접 원인이었다).</summary>
        private void ForceRefresh(string why) => _popover.Open(AnchorRect, $"헤더 측정 동기 갱신 — {why}");

        /// <summary>우리 락이든 남이 흘린 락이든 <b>무조건</b> 푼다(락은 static이고 씬 로드로 초기화되지
        /// 않는다 — 주인 객체가 파괴되면 <c>Release(owner)</c>를 부를 코드가 영원히 사라진다).</summary>
        private void ReleaseStrandedLock(string why)
        {
            if (_lockHeld)
            {
                _lockHeld = false;
                SpectacleEventLock.Release(this);
            }

            if (!SpectacleEventLock.IsActive) return;
            object stranded = SpectacleEventLock.CurrentOwner;
            SpectacleEventLock.Release(stranded);
            Debug.Log($"{LogPrefix} {why} — 상호배제 락이 {stranded}에게 잡힌 채 남아 있어 풀었습니다.");
        }

        // ====================================================================
        // 도구
        // ====================================================================

        private StickmanStateId CurrentState() =>
            _agent != null && _agent.Blackboard != null && _agent.Blackboard.Machine != null
                ? _agent.Blackboard.Machine.CurrentStateId
                : default;

        private static ActionCommandPopover.Command[] Commands() =>
            (ActionCommandPopover.Command[])System.Enum.GetValues(typeof(ActionCommandPopover.Command));

        private int ReadyCount()
        {
            int ready = 0;
            foreach (ActionCommandPopover.Command command in Commands())
            {
                if (_popover.GetAvailability(command).IsReady) ready++;
            }
            return ready;
        }

        /// <summary>네 명령 Director를 걷어 전원 <see cref="CommandAvailability.Missing"/>으로 만든다.
        /// <para>안전한 이유: 이 네 타입을 참조하는 프로덕션 코드는 모두 <c>FindFirstObjectByType</c> +
        /// null 검사 경로이고(명령창 자신과 <c>AppControlDirector</c>), 매 프레임 역참조하는 렌더러는
        /// 없다. 씬은 테스트마다 다시 로드되므로 이 파괴는 다음 테스트로 새 나가지 않는다.</para></summary>
        private void DestroyCommandDirectors()
        {
            DestroyIfPresent(UnityEngine.Object.FindFirstObjectByType<ArcheryDirector>());
            DestroyIfPresent(UnityEngine.Object.FindFirstObjectByType<GraffitiDirector>());
            DestroyIfPresent(UnityEngine.Object.FindFirstObjectByType<WindowTheftDirector>());
            DestroyIfPresent(UnityEngine.Object.FindFirstObjectByType<WindowCrashDirector>());
        }

        private static void DestroyIfPresent(MonoBehaviour director)
        {
            Assert.IsNotNull(director,
                "걷어낼 Director가 씬에 없습니다 — 이 테스트의 전제(네 칸이 배선돼 있다가 사라진다)가 " +
                "성립하지 않습니다. 배선 자체가 빠진 것이라면 그쪽이 먼저 고쳐질 결함입니다.");
            UnityEngine.Object.Destroy(director);
        }

        /// <summary>실패 메시지에 붙이는 진단 한 줄 — 헤더·칸·상태·접지·락을 <b>한 번에</b> 보여 준다.
        /// 첫 판에서 «상태=Fall»·«상태=LandingCrouch 락=WindowCrash»를 이 줄이 알려 줘 원인이 잡혔다.</summary>
        private string Snapshot()
        {
            StickmanStateId state = CurrentState();
            string reasons = string.Empty;
            foreach (ActionCommandPopover.Command command in Commands())
            {
                reasons += $" [{command}]{(_popover.IsCommandReady(command) ? "가능" : "«" + _popover.CommandReason(command) + "»")}";
            }
            bool grounded = _agent != null && _agent.Blackboard != null && _agent.Blackboard.SenseGround().Grounded;
            return $"헤더=«{_popover.StatusCaption}» 가능칸={ReadyCount()} 상태={state} " +
                $"바쁨판정={ActionCommandPopover.IsBusyHeaderState(state)} 접지={grounded} " +
                $"숨김={(_agent != null && _agent.IsSuspended)} " +
                $"락={(SpectacleEventLock.IsActive ? SpectacleEventLock.ActiveKind.ToString() : "없음")} ·{reasons}";
        }
    }
}
