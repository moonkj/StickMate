using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 피동 상태(Active Ragdoll 하이브리드의 핵심, 아키텍처 0절): 전신 Rigidbody2D/Joint2D 시뮬레이션에
    /// 완전히 위임하고 모터/IK 힘 인가를 전부 중단한다.
    ///
    /// 진입 조건 (-> Ragdoll): 어떤 능동 상태(Idle/Walk/Jump/Fall/ParkourClimb/Attack)에서든
    /// 외력(피격/투척/낙하 충격량의 크기)이 StickConfig.ragdollForceThreshold 이상이면 즉시 강제 전이한다.
    /// 이는 인터럽트형 전이이므로 진행 중이던 모션/대사가 무엇이든 취소된다. 이 전이를 호출할 때는
    /// 반드시 StateMachine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true)로 호출해
    /// UI 레이어가 "정상 종료"와 "강제 취소"를 구분해 연출할 수 있게 한다 (UX_FLOW.md 5절/9절-2).
    /// 실제 진입 트리거는 StickmanAgent.ReportExternalImpact()(충돌 콜백 기반, Core/StickmanAgent.cs와
    /// Core/RagdollLimbImpactRelay.cs 참고)가 현재 상태와 무관하게 단일 진입점으로 처리한다 — 이 상태
    /// 자신은 "이미 Ragdoll로 확정된 뒤"의 물리 위임/기상 판정만 책임진다.
    ///
    /// 이탈 조건 (-> Getup): 몸통/사지 각 Rigidbody2D 속도 크기가 StickConfig.ragdollSettleSpeedThreshold
    /// 이하로 StickConfig.ragdollSettleHoldDuration초 이상 "지속"되어야 한다. 순간적으로만 느려졌다가
    /// 다시 빨라지면(예: 굴러가다 재가속) 카운터가 리셋되고 Ragdoll을 유지한다 — 오탐(너무 이른 기상) 방지.
    /// 속도 측정은 RagdollRig.GetMaxSpeed()(전신 중 최댓값)를 사용한다.
    /// </summary>
    public sealed class RagdollState : IStickmanState, IHasDialogueParams
    {
        private readonly StickmanBlackboard _blackboard;

        // ragdollSettleSpeedThreshold 이하 속도가 유지된 누적 시간(초).
        private float _settleTimer;

        /// <summary>
        /// BUG-M7 파라미터 파이프라인 시연(docs/UX_FLOW.md 31-2 #2). ImpactRatio = 이번 충격량 /
        /// ragdollForceThreshold — "임계값 대비 배율" 단위로 노출해 대사 매핑 함수가 31-2 표의
        /// 3구간(1.0~2.0 / 2.0~4.0 / 4.0 초과)을 그대로 재사용할 수 있게 한다.
        /// </summary>
        public sealed class RagdollDialogueParams
        {
            public float ImpactRatio;
        }

        private readonly RagdollDialogueParams _dialogueParams = new RagdollDialogueParams();

        public object DialogueParams => _dialogueParams;

        public RagdollState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.Ragdoll;

        public void Enter(StateTransitionContext context)
        {
            _settleTimer = 0f;

            // ★ 2026-09-01 (P9-b) 진입 충격량 배선 — 방향 스냅샷은 **읽는 즉시 지운다**(소비형).
            // 지우지 않으면 방향을 모르는 다음 진입(ReportExternalImpact(크기만) / 테스트의 직접
            // ChangeState / 원인 불명의 강제 랙돌)에서 지난번 타격의 방향으로 유령 충격량이 실린다.
            Vector2 hitDirection = _blackboard.LastImpactDirection;
            _blackboard.LastImpactDirection = Vector2.zero;

            // 판정용 원본 충격량(N·s)을 그대로 넘기면 임계값 5배 타격에서 초당 5바퀴가 나온다 —
            // 환산/클램프는 RagdollImpactResolver.ResolveEntryImpulse() 한 곳에만 있다(그 문서에 실측 근거).
            float entryImpulse = RagdollImpactResolver.ResolveEntryImpulse(
                _blackboard.Config, _blackboard.LastImpactMagnitude);

            // 모든 관절의 모터/목표 각도 추종을 끄고 전신을 순수 물리 낙하물로 전환한다. 방향이 0이거나
            // 충격량이 0이면 RagdollRig가 힘 인가 경로를 통째로 건너뛴다(= 기존 무인자 거동 그대로).
            _blackboard.GetRagdollRig()?.EnterRagdoll(hitDirection, entryImpulse);

            // BUG-M7 대응 시연(UX_FLOW.md 31-2 #2) — StickmanAgent.ReportExternalImpact()가 이 전이
            // 직전에 스냅샷해둔 충격량을 임계값 대비 배율로 환산해 파라미터로 노출하고, 그 값 하나로
            // "윽.../으악!/으아아아악?!" 세 갈래를 갈라 파생시킨다(같은 매핑 함수, 같은 스냅샷 — 31-1 원칙).
            // ★ 대사는 위에서 **행동(진입 충격량)이 확정된 뒤** 같은 스냅샷 하나에서 파생된다 —
            // 순서를 바꾸면 원칙 1(행동-텍스트 싱크)이 깨진다.
            float threshold = _blackboard.Config != null ? _blackboard.Config.ragdollForceThreshold : 8f;
            _dialogueParams.ImpactRatio = threshold > 0f ? _blackboard.LastImpactMagnitude / threshold : 0f;

            _ = new DialogueIntent(context, (id, dialogueParams) =>
            {
                var p = dialogueParams as RagdollDialogueParams;
                float ratio = p != null ? p.ImpactRatio : 0f;
                if (ratio < 2.0f) return "윽...!";
                if (ratio < 4.0f) return "으악!";
                return "으아아아악?!";
            });
        }

        public void Tick(float deltaTime)
        {
            RagdollRig rig = _blackboard.GetRagdollRig();
            if (rig == null) return;

            float speed = rig.GetMaxSpeed();
            float settleThreshold = _blackboard.Config != null ? _blackboard.Config.ragdollSettleSpeedThreshold : 0.3f;

            if (speed <= settleThreshold)
            {
                _settleTimer += deltaTime;
            }
            else
            {
                _settleTimer = 0f; // 오탐 방지: 순간적으로만 느려진 경우는 리셋하고 Ragdoll 유지
            }

            float holdDuration = _blackboard.Config != null ? _blackboard.Config.ragdollSettleHoldDuration : 0.5f;
            if (_settleTimer >= holdDuration)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Getup);
            }
        }

        public void Exit() { }
    }
}
