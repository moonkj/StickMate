using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 벽 타기/모서리 매달리기/구르기 동작 묶음.
    /// 진입: Idle/Walk/Jump 중 벽·모서리 발판이 StickConfig.parkourDetectionRadius 이내로 근접 + 상승 입력.
    /// 전이: 동작 정상 종료 -> Idle/Walk 복귀 / 외력 임계값 초과 -> Ragdoll(강제 인터럽트, 예: 등반 중 추락 충격).
    /// </summary>
    public sealed class ParkourClimbState : IStickmanState
    {
        public StickmanStateId StateId => StickmanStateId.ParkourClimb;

        public void Enter(StateTransitionContext context)
        {
            // TODO(Phase 2): 벽/모서리 IK 그립 포인트 계산 및 부착.
        }

        public void Tick(float deltaTime)
        {
            // TODO(Phase 2): 등반/매달리기/구르기 진행도 추적, 완료 시 이전 능동 상태로 복귀.
        }

        public void Exit() { }
    }
}
