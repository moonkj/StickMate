using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 발화 자격 게이트(docs/UX_FLOW.md 5절 규칙 8)의 <b>사각지대</b> 회귀 테스트 —
    /// 2026-09-01 실측.
    ///
    /// ============================================================================
    /// 무엇이 잡혔나
    /// ============================================================================
    /// <code>
    /// frame=11110 표시 (BattleMinigame) "어... 힘이 다 샜다"  가독예산 1.18초
    /// frame=11111 교체 — 이전 노출 0.02초, 새 "여기 좋네"(Idle)
    /// frame=11112 즉시 컷 (Idle) "여기 좋네" 노출 0.02초
    /// frame=11114 제거, 노출 0.05초
    /// </code>
    /// <b>4프레임 안에 글자 블록 두 개가 각각 20ms씩 번쩍였다</b> — 규칙 8이 없애려던 바로 그
    /// 현상이, 규칙 8이 들어간 그 빌드에서.
    ///
    /// ============================================================================
    /// 근본 원인 한 줄
    /// ============================================================================
    /// <c>PlannedWanderDwellRemainingSeconds</c>는 <b>배회 AI 페이즈의 잔여</b>이지 <b>이 상태의
    /// 잔여</b>가 아니었다. 둘이 같은 값인 것은 상태가 배회 페이즈 전환으로 들어왔을 때뿐이고,
    /// 격파 종료 → Idle / Getup → Idle / LandingCrouch → Idle·Walk / ParkourClimb 완료 → Idle /
    /// GroundLossHang 복귀 → Idle·Walk 는 <b>전부 배회가 Moving 한복판인데 Idle로 들어오는</b>
    /// 경로다. 그때 게이트에 물으면 "2.8초 남았다"고 답하고 실제 체류는 1프레임이다.
    ///
    /// 예전에는 그중 <b>한 경로만</b>(<c>context.From != GroundLossHang</c>) 손으로 막혀 있었다.
    /// 이 테스트가 잠그는 것은 그 손패치가 아니라, 경로를 열거하지 않는 규칙 하나다:
    /// <b>이동 의도가 이 상태를 이미 부정하고 있으면 계획 잔여는 0이다.</b>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// 모든 검사가 짝으로 되어 있다 — 의도가 <b>일치</b>할 때는 반드시 원래 잔여를 그대로 답하고
    /// 대사도 나와야 한다. 그렇지 않으면 "항상 0을 답한다 / 항상 침묵한다"는 오답이 통과한다.
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(배회 AI·대사 파이프라인에 플랫폼 분기 없음).</para>
    /// </summary>
    public sealed class PlannedDwellStateScopeTests
    {
        /// <summary>
        /// 이동 의도와 계획 잔여를 테스트가 직접 정하는 스텁. 실제 <see cref="AutoWanderController"/>와
        /// 같은 두 인터페이스를 구현하므로 블랙보드에서 보이는 모양이 프로덕션과 동일하다.
        /// </summary>
        private sealed class StubIntent : IMovementIntentSource, IPlannedDwellSource
        {
            public float MoveInputX { get; set; }
            public float PlannedDwellRemainingSeconds { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        /// <summary>계획을 모르는 소스(<see cref="IPlannedDwellSource"/> 미구현) — NaN 경로 확인용.</summary>
        private sealed class PlanlessIntent : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private const float PhaseRemaining = 2.8f; // 실측 로그의 "게이트에게 물으면 2.8초" 그 값.

        private StickConfig _config;
        private StickmanBlackboard _blackboard;
        private StubIntent _intent;
        private Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            AppSettingsModel.ResetForTesting();

            // 잡담 추첨의 난수를 결정론으로 고정한다(전역 상태이므로 TearDown에서 원복).
            _randomState = Random.state;
            Random.InitState(20260901);

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.dialogueBubbleEnabled = true;
            _config.idleChatterChance = 1f;
            _config.walkChatterChance = 1f;
            _config.ambientChatterCooldownSeconds = 0f;

            _intent = new StubIntent { PlannedDwellRemainingSeconds = PhaseRemaining };
            _blackboard = new StickmanBlackboard { Config = _config, IntentSource = _intent };
        }

        [TearDown]
        public void TearDown()
        {
            Random.state = _randomState;
            _blackboard = null;
            _intent = null;
            if (_config != null) Object.DestroyImmediate(_config);
            _config = null;
            AppSettingsModel.ResetForTesting();
        }

        /// <summary>데드존을 확실히 넘는 이동 의도(프로덕션 배회 AI는 ±1을 낸다).</summary>
        private float MovingInput => _config.moveInputDeadzone + 1f;

        // ==================== 1. 상태 범위 질의 자체 ====================

        [Test]
        public void 이동_의도가_데드존_밖이면_Idle의_계획_잔여는_0이다()
        {
            _intent.MoveInputX = MovingInput;

            Assert.AreEqual(0f, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f,
                "배회가 '걷는 중'인데 Idle로 들어온 경우다 — Idle은 다음 Tick에 곧바로 Walk로 나간다. " +
                "그런데 게이트에는 배회 페이즈 잔여를 그대로 답하고 있었다(실측 2.8초 vs 실제 1프레임).");

            // ★ 네거티브 컨트롤 — "항상 0"이 아니다. 의도와 일치하는 Walk에는 잔여를 그대로 답한다.
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Walk), 1e-6f,
                "이동 의도와 일치하는 Walk에까지 0을 답하면 배회 중 대사가 통째로 사라진다.");
        }

        [Test]
        public void 이동_의도가_데드존_안이면_Walk의_계획_잔여가_0이다()
        {
            _intent.MoveInputX = 0f;

            Assert.AreEqual(0f, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Walk), 1e-6f,
                "정지 의도인데 Walk에 잔여를 답하면 Walk가 다음 Tick에 Idle로 나가면서 서술 대사가 컷된다.");

            // ★ 네거티브 컨트롤 — 반대쪽(Idle)은 그대로여야 한다.
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f);
        }

        [Test]
        public void 데드존_경계는_상태의_탈출_조건과_같은_판정을_쓴다()
        {
            // IdleState.Tick / WalkState.Tick 은 둘 다 Abs(move) > deadzone / <= deadzone 으로 나눈다.
            // 게이트가 다른 경계를 쓰면 그 차이만큼 "게이트는 남았다는데 상태는 이미 나간" 틈이 생긴다.
            _intent.MoveInputX = _config.moveInputDeadzone; // 경계값 = 아직 "정지"다.
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f,
                "데드존 경계값은 상태 쪽에서 '정지'로 취급된다 — 게이트도 같아야 한다.");
            Assert.AreEqual(0f, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Walk), 1e-6f);

            _intent.MoveInputX = _config.moveInputDeadzone + 1e-3f; // 경계 바로 바깥 = "이동"이다.
            Assert.AreEqual(0f, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle), 1e-6f);
            Assert.AreEqual(PhaseRemaining, _blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Walk), 1e-6f);
        }

        [Test]
        public void 배회_계획이_서술하지_않는_상태에는_모른다고_답한다()
        {
            _intent.MoveInputX = MovingInput;

            Assert.IsTrue(float.IsNaN(_blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.BattleMinigame)),
                "격파는 자기 게이지 길이를 스스로 알고 게이트에 직접 넘긴다 — 배회 잔여는 그 상태에 " +
                "대해 아무 말도 하지 않으므로 0(=침묵 강제)이 아니라 NaN(=모름)이어야 한다.");
            Assert.IsTrue(float.IsNaN(_blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.ParkourClimb)));
        }

        [Test]
        public void 계획을_모르는_소스면_상태가_일치해도_NaN이다()
        {
            _blackboard.IntentSource = new PlanlessIntent { MoveInputX = 0f };

            Assert.IsTrue(float.IsNaN(_blackboard.PlannedDwellRemainingSecondsFor(StickmanStateId.Idle)),
                "IPlannedDwellSource를 구현하지 않은 소스(테스트 스텁 다수)는 계획을 모른다 — " +
                "게이트는 그때 막지 않는다(규칙 8은 최적화이지 검열이 아니다).");
        }

        // ==================== 2. 게이트가 실제로 그 값을 본다 ====================

        [Test]
        public void 게이트는_상태_범위_잔여를_보고_어긋난_Idle_잡담을_보류한다()
        {
            var target = new AmbientChatter.ChatterParams();
            _intent.MoveInputX = MovingInput; // "배회는 걷는 중인데 Idle로 들어왔다"

            Assert.IsFalse(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target),
                "다음 프레임에 Walk로 나갈 Idle인데 서술 잡담을 허용했다 — 화면에서 20ms 번쩍인다.");

            // ★ 네거티브 컨트롤 — 의도가 일치하면 같은 조건에서 반드시 발화한다.
            _intent.MoveInputX = 0f;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            Assert.IsTrue(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target),
                "의도가 일치하는 평범한 Idle에서까지 침묵하면 캐릭터가 통째로 벙어리가 된다 — " +
                "그러면 위 단언은 아무것도 검사하지 않는다.");
        }

        [Test]
        public void 보류된_발화는_쿨다운을_소비하지_않는다()
        {
            var target = new AmbientChatter.ChatterParams();
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            _intent.MoveInputX = MovingInput;

            Assert.IsFalse(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, target));
            Assert.AreEqual(0f, _blackboard.NextChatterAllowedUnscaledTime,
                "게이트에 막힌 발화가 쿨다운을 태우면, 어긋난 진입이 잦은 구간에서 캐릭터가 " +
                "말할 수 있는 순간까지 통째로 잃는다(막힌 발화는 추첨이 없었던 것으로 되돌린다).");
        }

        // ==================== 3. 실제 IdleState 진입 경로 (구현을 검사한다) ====================

        /// <summary>대사를 만들지 않는 출발 상태.</summary>
        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) => StateId = id;
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        private int EnterIdleAndCountDialogue(float moveInputX, StickmanStateId from)
        {
            var idle = new IdleState(_blackboard);
            var machine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { from, new SilentState(from) },
                { StickmanStateId.Idle, idle },
            });
            _blackboard.Machine = machine;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            _intent.MoveInputX = moveInputX;

            int count = 0;
            void OnRequested(DialogueIntent _) => count++;
            StickmanEventBus.DialogueRequested += OnRequested;
            try
            {
                machine.Start(from);
                machine.ChangeState(StickmanStateId.Idle);
            }
            finally
            {
                StickmanEventBus.DialogueRequested -= OnRequested;
                machine.ChangeState(from); // 살아 있는 DialogueIntent의 정적 구독을 정리한다.
            }
            return count;
        }

        [Test]
        public void 격파_종료에서_들어온_Idle은_배회가_걷는_중이면_침묵한다()
        {
            Assert.AreEqual(0, EnterIdleAndCountDialogue(MovingInput, StickmanStateId.BattleMinigame),
                "실측 frame=11111의 그 대사다 — 격파가 끝나 Idle로 들어왔지만 배회는 Moving 한복판이라 " +
                "Idle은 다음 Tick에 곧바로 Walk로 나간다. 손으로 막아 둔 GroundLossHang 한 경로 말고 " +
                "나머지 네 경로가 전부 이 모양이었다.");
        }

        [Test]
        public void 네거티브_배회가_쉬는_중이면_같은_진입에서도_말한다()
        {
            Assert.AreEqual(1, EnterIdleAndCountDialogue(0f, StickmanStateId.BattleMinigame),
                "배회 계획과 상태가 일치하는데도 침묵하면 위 검사는 '어떤 Idle이든 침묵'으로 " +
                "통과한다 — 그러면 아무것도 검사하지 않는 단언이 된다.");
        }

        [Test]
        public void 착지에서_들어온_Idle도_같은_규칙을_받는다()
        {
            // 손패치가 아니라 규칙이라는 것의 의미: 진입 상태를 열거하지 않았으므로 다른 경로도
            // 자동으로 같은 판정을 받는다.
            Assert.AreEqual(0, EnterIdleAndCountDialogue(MovingInput, StickmanStateId.LandingCrouch));
            Assert.AreEqual(0, EnterIdleAndCountDialogue(MovingInput, StickmanStateId.Getup));
        }
    }
}
