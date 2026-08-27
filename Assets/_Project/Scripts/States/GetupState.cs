using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// Ragdoll에서 감속이 완료된 뒤 능동 상태로 복귀하기 위한 과도 상태 (Active Ragdoll 하이브리드, 아키텍처 0절).
    ///
    /// 진입 조건 (Ragdoll -> Getup): RagdollState가 전신 속도 임계값 이하 지속 조건을 충족했을 때
    /// StickmanStateMachine.ChangeState(Getup)로 자동 진입한다 (직접 사용자 입력으로는 진입하지 않음).
    ///
    /// 이탈 조건 (-> Idle): 널브러진 사지 포즈에서 직립 목표 포즈로의 기상 IK 보간이 완료되었을 때.
    ///
    /// 주의(강제 재인터럽트): 기상 도중에도 새 외력이 StickConfig.ragdollForceThreshold를 넘으면
    /// 즉시 Ragdoll로 재전이될 수 있다. 이 경우 Getup 진입 시 만들어졌던 DialogueIntent(예: "으쌰...")는
    /// TransitionGeneration 불일치를 즉시 감지해 같은 프레임에 자동 만료된다 — 반쯤 일어나다 다시
    /// 얻어맞았는데 "으쌰!" 대사만 화면에 남는 텍스트-액션 불일치 버그를 원천 차단한다.
    /// </summary>
    public sealed class GetupState : IStickmanState
    {
        public StickmanStateId StateId => StickmanStateId.Getup;

        // TODO(Phase 2): 기상 모션 진행도(0~1).
        private float _getupProgress;

        public void Enter(StateTransitionContext context)
        {
            _getupProgress = 0f;
            // TODO(Phase 2): 현재 널브러진 관절 각도를 시작점으로, 직립 목표 포즈를 종료점으로 하는
            // IK 보간을 시작한다.
        }

        public void Tick(float deltaTime)
        {
            // TODO(Phase 2):
            // 1. _getupProgress를 진행시키며 포즈 보간.
            // 2. 진행 중 외력이 StickConfig.ragdollForceThreshold를 넘으면 즉시 StateMachine.ChangeState(Ragdoll).
            // 3. _getupProgress >= 1이면 StateMachine.ChangeState(Idle).
        }

        public void Exit() { }
    }
}
