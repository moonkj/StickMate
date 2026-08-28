using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// Ragdoll에서 감속이 완료된 뒤 능동 상태로 복귀하기 위한 과도 상태 (Active Ragdoll 하이브리드, 아키텍처 0절).
    ///
    /// 진입 조건 (Ragdoll -> Getup): RagdollState가 전신 속도 임계값 이하 지속 조건을 충족했을 때
    /// StickmanStateMachine.ChangeState(Getup)로 자동 진입한다 (직접 사용자 입력으로는 진입하지 않음).
    ///
    /// 이탈 조건 (-> Idle): 널브러진 포즈에서 직립 중립 포즈로의 기상 보간(_getupProgress)이 완료되었을 때.
    ///
    /// 2026-08-28 근본 재구현: 예전에는 HingeJoint2D 모터의 비례 제어로 몸을 일으키려 했으나(수렴 보장
    /// 없음 — 반쯤 일어난 채 Idle로 넘어가거나 아예 못 일어나는 경로가 존재했다), 이제는 결정론적 보간
    /// 두 갈래를 같은 progress로 동시에 돌린다: RagdollRig.TickGetupRoot()가 루트(몸통) 회전각을,
    /// StickmanPoseAnimator.TickGetupPose()가 팔다리 각도를 각각 "널브러진 실제 각도 -> 직립 중립 각도"로
    /// 직접 보간한다. progress=1이면 반드시 정확히 직립 중립 포즈가 되므로 기상 실패라는 경로가 없다.
    /// 물리 모드는 진입 즉시 능동 모드(팔다리 Kinematic + 관절 비활성)로 되돌아가 있다 —
    /// StickmanBlackboard.TickPose()가 상태 ID를 보고 자동 처리하며, Getup 동안에는 루트 각도만
    /// 스냅하지 않고 이 상태의 보간에 맡긴다(그래야 "일어나는 모습"이 실제로 보인다).
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

            // 널브러진 현재 각도를 보간 시작점으로 캡처한다. 캡처 전에 EnterActiveMode()를 먼저 호출해
            // 팔다리를 Kinematic으로 되돌리는 것이 중요하다 — 그래야 캡처한 순간의 각도가 보간 도중
            // 물리에 의해 덮어써지지 않는다(루트 각도는 아직 스냅하지 않는다: 이 상태가 직접 보간한다).
            RagdollRig rig = _blackboard.GetRagdollRig();
            if (rig != null)
            {
                rig.EnterActiveMode();
                rig.BeginGetup();
            }
            _blackboard.GetPoseAnimator()?.CaptureGetupStartPose();
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

            // 루트 회전각과 팔다리 각도를 같은 progress로 동시에 직립 중립 포즈로 보간한다.
            rig.TickGetupRoot(_getupProgress);
            _blackboard.GetPoseAnimator()?.TickGetupPose(_getupProgress, _blackboard.BuildPoseSettings());

            if (_getupProgress >= 1f)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
            }
        }

        public void Exit() { }
    }
}
