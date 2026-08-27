using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 11절 라이벌 스틱맨 대결 — 플레이어(StickmanAgent)와 별개의 상태머신 인스턴스를
    /// 갖는 독립 컴포넌트. 관전 전용(유저 개입 없음, 클릭관통 그대로 유지 — 부분적 클릭관통 해제를
    /// 절대 쓰지 않는다, 10/12/13절과 명확히 분리된 설계 원칙)이라 최소 스코프로 구현한다:
    /// - 별도 StickmanBlackboard/StickmanStateMachine 인스턴스(플레이어와 물리적으로 다른 Rigidbody2D).
    /// - 발판(FootholdPoller)/카메라는 플레이어(StickmanAgent)와 공유 — "두 캐릭터가 발판을 공유"
    ///   요구사항을 발판 재열거 없이 참조 공유로 만족시킨다.
    /// - 추적 AI는 States/AutoWanderController.cs(배회용)를 재사용하지 않고 RivalPursuitIntentSource
    ///   (목표=플레이어 위치로 단순 추적)를 별도 사용.
    /// - Idle/Walk/Fall/Attack/Ragdoll/Getup 6종만 등록(점프/파쿠르는 최소 스코프에서 다루지 않음 —
    ///   RivalPursuitIntentSource.JumpRequested가 항상 false라 등록해도 어차피 트리거되지 않지만,
    ///   등록 자체를 생략해 "라이벌은 파쿠르를 하지 않는다"는 스코프를 코드로도 명확히 한다).
    ///
    /// 전투 판정은 이 컴포넌트가 "심판" 역할로 직접 수행한다(States.AttackState는 순수하게 "타격
    /// 모션을 재생하고 복귀"만 담당 — 누가 누구를 때리는지는 모른다). 근접 시 무작위로 선타를 정해
    /// RagdollImpactResolver(자신이 맞았을 때)/StickmanAgent.ReportExternalImpact(플레이어가 맞았을
    /// 때, Suspended 가드를 지키기 위해 반드시 이 공개 메서드를 거친다)로 피격을 적용한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RivalStickmanAgent : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        private Rigidbody2D _body;
        private Renderer[] _renderers;
        private StickmanBlackboard _blackboard;
        private StickmanStateMachine _machine;
        private RivalPursuitIntentSource _pursuit;
        private StickmanAgent _opponent;

        private bool _inDuel;
        private float _durationTimer;
        private float _attackCooldownTimer;
        private int _hitsTakenByRival;   // 라이벌이 맞은 횟수(=플레이어 승리 조건에 근접)
        private int _hitsTakenByPlayer;  // 플레이어가 맞은 횟수(=라이벌 승리 조건에 근접)

        public bool InDuel => _inDuel;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _body.simulated = false; // 스폰 전에는 완전히 비활성 — 관전 전용 스펙터클이 시작될 때만 등장.
            SetRenderersEnabled(false);
        }

        /// <summary>스폰 확률/쿨다운 판정은 Interaction/RivalEncounterDirector.cs가 전담한다 — 이 메서드는
        /// "지금 대결을 시작한다"는 확정된 사실만 받는다.</summary>
        public void BeginDuel(StickmanAgent opponent, Vector2 spawnWorldPos)
        {
            if (_inDuel || opponent == null) return;
            _opponent = opponent;

            _body.simulated = true;
            _body.position = spawnWorldPos;
            _body.linearVelocity = Vector2.zero;
            SetRenderersEnabled(true);

            EnsureMachineBuilt();

            _inDuel = true;
            _durationTimer = 0f;
            _attackCooldownTimer = 0f;
            _hitsTakenByRival = 0;
            _hitsTakenByPlayer = 0;

            StickmanEventBus.RaiseRivalDuelStarted();
        }

        private void EnsureMachineBuilt()
        {
            if (_blackboard != null) return; // 최초 1회만 구성(매 대결마다 재구성 금지 — GC/재탐색 방지).

            _blackboard = new StickmanBlackboard
            {
                Body = _body,
                MainCamera = _opponent.Blackboard.MainCamera,
                Config = _config,
                FootholdPoller = _opponent.Blackboard.FootholdPoller, // 발판 공유(11절 "두 캐릭터가 발판을 공유")
            };

            _pursuit = new RivalPursuitIntentSource(_blackboard, _config, () => _opponent.Blackboard.Body.position);
            _blackboard.IntentSource = _pursuit;

            var states = new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Idle, new IdleState(_blackboard) },
                { StickmanStateId.Walk, new WalkState(_blackboard) },
                { StickmanStateId.Fall, new FallState(_blackboard) },
                { StickmanStateId.Attack, new AttackState(_blackboard) },
                { StickmanStateId.Ragdoll, new RagdollState(_blackboard) },
                { StickmanStateId.Getup, new GetupState(_blackboard) },
            };

            _machine = new StickmanStateMachine(states);
            _blackboard.Machine = _machine;
            _machine.Start(StickmanStateId.Idle);
        }

        private void Update()
        {
            if (!_inDuel) return;

            // UX 11절 예외: "대결 중 전체화면 게임 감지 → 즉시 취소(6-4절 우선)". 라이벌은 플레이어의
            // StickmanStateMachine에 속하지 않아 StickmanAgent.Suspend()의 일반 처리 대상이 아니므로,
            // 여기서 직접 IsSuspended를 폴링해 동일한 우선순위를 보장한다.
            if (_opponent == null || _opponent.IsSuspended)
            {
                EndDuel(RivalDuelResult.Draw);
                return;
            }

            float dt = Time.deltaTime;
            _durationTimer += dt;

            _pursuit.Tick(dt);
            _machine.Tick(dt);
            TickCombatExchange(dt);

            float maxDuration = _config != null ? _config.rivalMaxDurationSeconds : 30f;
            if (_inDuel && _durationTimer >= maxDuration)
            {
                EndDuel(RivalDuelResult.Draw);
            }
        }

        private void TickCombatExchange(float dt)
        {
            if (_opponent == null || _body == null) return;

            _attackCooldownTimer -= dt;
            if (_attackCooldownTimer > 0f) return;

            float range = _config != null ? _config.rivalAttackRange : 1.0f;
            float dist = Mathf.Abs(_body.position.x - _opponent.Blackboard.Body.position.x);
            if (dist > range) return;

            _attackCooldownTimer = _config != null ? _config.rivalAttackCooldownSeconds : 1.2f;

            float threshold = _blackboard.Config != null ? _blackboard.Config.ragdollForceThreshold : 8f;
            float multiplier = _blackboard.Config != null ? _blackboard.Config.rivalAttackImpactMultiplier : 1.25f;
            float impulse = threshold * multiplier; // 항상 RAGDOLL 전이를 보장하는 확정적 충격량.

            bool rivalStrikes = Random.value < 0.5f; // 근접 시 무작위 선타(50:50) — "정교할 필요 없음".
            if (rivalStrikes)
            {
                TryPlayAttackAnimation();
                _opponent.ReportExternalImpact(impulse); // Suspended 가드를 지키기 위해 공개 메서드 경유.
                _hitsTakenByPlayer++;
            }
            else
            {
                RagdollImpactResolver.TryApplyImpact(_blackboard, impulse);
                _hitsTakenByRival++;
            }

            CheckDuelOutcome();
        }

        private void TryPlayAttackAnimation()
        {
            // 자신(라이벌)이 선타를 날릴 때만 자기 Attack 모션을 재생한다 — 맞는 쪽은 곧바로 Ragdoll로
            // 전이하므로 별도 처리가 필요 없다.
            var current = _machine.CurrentStateId;
            if (current == StickmanStateId.Idle || current == StickmanStateId.Walk)
            {
                _machine.ChangeState(StickmanStateId.Attack);
            }
        }

        private void CheckDuelOutcome()
        {
            int hitsToLose = _config != null ? _config.rivalDuelHitsToLose : 2;
            if (_hitsTakenByRival >= hitsToLose) { EndDuel(RivalDuelResult.PlayerWon); return; }
            if (_hitsTakenByPlayer >= hitsToLose) { EndDuel(RivalDuelResult.RivalWon); }
        }

        /// <summary>Interaction/RivalEncounterDirector.cs가 트레이 긴급정지 시 강제 종료를 요청할 때 사용.</summary>
        public void ForceEndDuel()
        {
            if (_inDuel) EndDuel(RivalDuelResult.Draw);
        }

        private void EndDuel(RivalDuelResult result)
        {
            _inDuel = false;
            _opponent = null;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false; // 물리 정지 — 다음 스폰까지 완전히 대기 상태로 되돌아간다.
            SetRenderersEnabled(false);
            // 승패와 무관하게 즉시 퇴장(실제 파일/창은 전혀 변경하지 않음, 원칙 3). 걸어 나가는 연출은
            // Phase 2+ 렌더링 레이어 담당 — 지금은 결과 통지 + 비활성화만 수행한다.
            StickmanEventBus.RaiseRivalDuelEnded(result);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = enabled;
            }
        }
    }
}
