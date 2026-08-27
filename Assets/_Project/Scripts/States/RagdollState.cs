using StickMate.Core;

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
    ///
    /// 이탈 조건 (-> Getup): 몸통/사지 각 Rigidbody2D 속도 크기가 StickConfig.ragdollSettleSpeedThreshold
    /// 이하로 StickConfig.ragdollSettleHoldDuration초 이상 "지속"되어야 한다. 순간적으로만 느려졌다가
    /// 다시 빨라지면(예: 굴러가다 재가속) 카운터가 리셋되고 Ragdoll을 유지한다 — 오탐(너무 이른 기상) 방지.
    ///
    /// Phase 0에서는 스캐폴딩이므로 실제 속도 측정/누적 타이머 로직은 비워두고 TODO로 남긴다.
    /// </summary>
    public sealed class RagdollState : IStickmanState
    {
        public StickmanStateId StateId => StickmanStateId.Ragdoll;

        // TODO(Phase 2): ragdollSettleSpeedThreshold 이하 속도가 유지된 누적 시간(초).
        private float _settleTimer;

        public void Enter(StateTransitionContext context)
        {
            _settleTimer = 0f;
            // TODO(Phase 2): 모든 Joint2D의 모터/목표 각도 추종을 끄고 전신을 순수 물리 낙하물로 전환.
            // 대사 정책: 이 상태에서 DialogueIntent를 만든다면 "피격/충격" 계열 대사만 허용
            // (예: 비명, 신음) — 상태가 Ragdoll로 확정된 뒤에만 파생되므로 원칙 1을 자동 준수한다.
        }

        public void Tick(float deltaTime)
        {
            // TODO(Phase 2):
            // 1. 전신 Rigidbody2D 속도 크기 측정.
            // 2. StickConfig.ragdollSettleSpeedThreshold 이하이면 _settleTimer += deltaTime, 아니면 0으로 리셋.
            // 3. _settleTimer >= StickConfig.ragdollSettleHoldDuration 이면 StateMachine.ChangeState(Getup).
        }

        public void Exit() { }
    }
}
