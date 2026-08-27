using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// Ragdoll에서 감속이 완료된 뒤 능동 상태로 복귀하기 위한 과도 상태 (Active Ragdoll 하이브리드, 아키텍처 0절).
    ///
    /// 진입 조건 (Ragdoll -> Getup): RagdollState가 전신 속도 임계값 이하 지속 조건을 충족했을 때
    /// StickmanStateMachine.ChangeState(Getup)로 자동 진입한다 (직접 사용자 입력으로는 진입하지 않음).
    ///
    /// 이탈 조건 (-> Idle): 널브러진 사지 포즈에서 직립 목표 포즈로의 기상 IK 보간(_getupProgress, 아래
    /// RagdollRig.TickGetup 참고)이 완료되었을 때.
    ///
    /// 주의(강제 재인터럽트): 기상 도중에도 새 외력이 StickConfig.ragdollForceThreshold를 넘으면
    /// StickmanAgent.ReportExternalImpact()가 현재 상태와 무관하게 즉시 Ragdoll로 재전이시킨다(단일
    /// 진입점 — 이 상태 자신은 별도의 재인터럽트 감지 로직을 두지 않는다). 이 경우 Getup 진입 시
    /// 만들어졌던 DialogueIntent(예: "으쌰...")는 TransitionGeneration 불일치를 즉시 감지해 같은
    /// 프레임에 자동 만료된다 — 반쯤 일어나다 다시 얻어맞았는데 "으쌰!" 대사만 화면에 남는
    /// 텍스트-액션 불일치 버그를 원천 차단한다.
    /// </summary>
    public sealed class GetupState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        // 기상 모션 진행도(0~1).
        private float _getupProgress;

        public GetupState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.Getup;

        public void Enter(StateTransitionContext context)
        {
            _getupProgress = 0f;
            // 현재 널브러진 관절 각도를 시작점으로 캡처 — 직립 목표 포즈(0도)로의 IK 보간 시작.
            _blackboard.GetRagdollRig()?.BeginGetup();
            // TODO(Phase 2 렌더링): 필요 시 new DialogueIntent(context, id => "으쌰...")로 기상 대사 생성.
        }

        public void Tick(float deltaTime)
        {
            RagdollRig rig = _blackboard.GetRagdollRig();
            if (rig == null)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            float duration = _blackboard.Config != null ? _blackboard.Config.getupDuration : 0.6f;
            _getupProgress += duration > 0f ? deltaTime / duration : 1f;
            if (_getupProgress > 1f) _getupProgress = 1f;

            float motorGain = _blackboard.Config != null ? _blackboard.Config.getupMotorGain : 6f;
            float maxTorque = _blackboard.Config != null ? _blackboard.Config.getupMaxMotorTorque : 50f;
            rig.TickGetup(_getupProgress, motorGain, maxTorque);

            if (_getupProgress >= 1f)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
            }
        }

        public void Exit() { }
    }
}
