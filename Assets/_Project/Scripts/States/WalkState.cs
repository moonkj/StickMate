using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: StickConfig.walkSpeed로 발판 위를 이동.
    /// 전이: 이동 입력 해제 -> Idle / 점프 입력 -> Jump / 발판 이탈(유예시간 초과) -> Fall /
    ///       공격 입력 -> Attack / 벽·모서리 근접(+상승 입력) -> ParkourClimb /
    ///       외력 임계값 초과 -> Ragdoll(강제 인터럽트).
    /// </summary>
    public sealed class WalkState : IStickmanState
    {
        public StickmanStateId StateId => StickmanStateId.Walk;

        public void Enter(StateTransitionContext context)
        {
            // TODO(Phase 1): 보행 IK/애니메이션 시작.
        }

        public void Tick(float deltaTime)
        {
            // TODO(Phase 1): StickConfig.walkSpeed 기반 이동, 발판 경계/입력 변화 감지 후 전이.
        }

        public void Exit() { }
    }
}
