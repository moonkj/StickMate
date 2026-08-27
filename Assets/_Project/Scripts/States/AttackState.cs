using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 공격 모션 재생(격파 미니게임/전투 로직과 연동, Phase 3).
    /// 진입: Idle/Walk 중 공격 입력.
    /// 전이: 모션 정상 종료 -> 진입 직전 능동 상태로 복귀 / 외력 임계값 초과 -> Ragdoll(강제 인터럽트,
    ///       예: 공격 모션 재생 도중 라이벌에게 선타를 맞음 — 이 경우 Attack이 만든 DialogueIntent는
    ///       TransitionGeneration 불일치로 같은 프레임에 자동 만료된다).
    /// </summary>
    public sealed class AttackState : IStickmanState
    {
        public StickmanStateId StateId => StickmanStateId.Attack;

        public void Enter(StateTransitionContext context)
        {
            // TODO(Phase 3): 공격 모션 재생 시작, 히트박스 활성화 타이밍 예약.
        }

        public void Tick(float deltaTime)
        {
            // TODO(Phase 3): 모션 진행도 추적, 완료 시 StateMachine.ChangeState(진입 직전 능동 상태)로 복귀.
        }

        public void Exit() { }
    }
}
