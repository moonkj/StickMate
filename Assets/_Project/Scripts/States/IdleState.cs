using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 정지 대기.
    /// 전이: 이동 입력 -> Walk / 점프 입력(+접지) -> Jump / 공격 입력 -> Attack /
    ///       벽·모서리 근접(+상승 입력) -> ParkourClimb / 외력 임계값 초과 -> Ragdoll(강제 인터럽트).
    /// </summary>
    public sealed class IdleState : IStickmanState
    {
        public StickmanStateId StateId => StickmanStateId.Idle;

        public void Enter(StateTransitionContext context)
        {
            // TODO(Phase 1): 정지 포즈 IK 타겟 설정.
            // TODO(Phase 2): 필요 시 new DialogueIntent(context, id => "...") 로 유휴 잡담 대사 생성.
        }

        public void Tick(float deltaTime)
        {
            // TODO(Phase 1): 이동/점프/공격 입력 및 발판 근접을 감지해 StateMachine.ChangeState 호출.
        }

        public void Exit() { }
    }
}
