using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: StickConfig.gravityScale에 따라 자유낙하.
    /// 전이: 발판 착지 감지 -> Idle/Walk(착지 시 이동 입력 유무로 분기) /
    ///       화면 경계를 완전히 벗어남 -> (Phase 1) 화면 밖 처리 루틴 /
    ///       외력 임계값 초과 -> Ragdoll(강제 인터럽트, 예: 낙하 중 피격).
    /// </summary>
    public sealed class FallState : IStickmanState
    {
        public StickmanStateId StateId => StickmanStateId.Fall;

        public void Enter(StateTransitionContext context)
        {
            // TODO(Phase 1): 낙하 포즈 전환.
        }

        public void Tick(float deltaTime)
        {
            // TODO(Phase 1): 발판 재탐지(StickConfig.fallGraceDuration 유예 적용) 후 착지 전이.
        }

        public void Exit() { }
    }
}
