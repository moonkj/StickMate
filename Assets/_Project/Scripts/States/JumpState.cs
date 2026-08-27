using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: StickConfig.jumpForce로 상승.
    /// 전이: 상승 속도가 0 이하로 전환(정점 통과) -> Fall / 외력 임계값 초과 -> Ragdoll(강제 인터럽트).
    /// </summary>
    public sealed class JumpState : IStickmanState
    {
        public StickmanStateId StateId => StickmanStateId.Jump;

        public void Enter(StateTransitionContext context)
        {
            // TODO(Phase 1): Rigidbody2D에 StickConfig.jumpForce만큼 상승 속도 부여.
        }

        public void Tick(float deltaTime)
        {
            // TODO(Phase 1): 수직 속도 부호 반전 감지 -> StateMachine.ChangeState(Fall).
        }

        public void Exit() { }
    }
}
