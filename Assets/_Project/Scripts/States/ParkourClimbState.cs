using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 벽 타기/모서리 매달리기 동작(아키텍처 0절, UX_FLOW.md 4절).
    ///
    /// 진입: WalkState의 Jump 판정이 "지금 딛고 있는 발판 경계 근처 + 진행방향에 그보다 눈에 띄게 높은
    /// 발판(벽)이 있음"을 감지했을 때(StickmanBlackboard.TryFindClimbableWall, UX_FLOW.md 26-2 "경계에서
    /// 점프 시도"의 자연스러운 확장 — AutoWanderController가 발생시키는 JumpRequested + 발판 경계 근접
    /// 조합이 실제 트리거다). IdleState의 "제자리 점프"는 방향 의도가 없어(MoveInputX==0) 이 판정을
    /// 의도적으로 건너뛴다(WalkState.cs 참고, UX_FLOW.md 4절 "애매하면 더 안전한 쪽을 선택" 원칙).
    ///
    /// 전이: 등반 완료 -> Idle/Walk 복귀(이동 입력 유무로 분기) / 잡을 곳이 사라짐(창 이동/닫힘) -> 즉시
    /// Fall(같은 프레임 대사도 자동 취소) / 외력 임계값 초과 -> Ragdoll(StickmanAgent.ReportExternalImpact가
    /// 상태와 무관하게 처리하는 단일 진입점, RagdollState.cs 참고).
    /// </summary>
    public sealed class ParkourClimbState : IStickmanState, IHasDialogueParams
    {
        private readonly StickmanBlackboard _blackboard;

        private int _direction;
        private bool _hasWall;
        private long _wallHandle;
        private float _wallTopWorldY;
        private float _startWorldY;
        private float _climbProgress;

        /// <summary>BUG-M7 파라미터 파이프라인 시연(UX_FLOW.md 31-2 #4) — 오를 거리(월드 유닛).</summary>
        public sealed class ParkourClimbDialogueParams
        {
            public float ClimbHeightUnits;
        }

        private readonly ParkourClimbDialogueParams _dialogueParams = new ParkourClimbDialogueParams();

        public object DialogueParams => _dialogueParams;

        public ParkourClimbState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.ParkourClimb;

        public void Enter(StateTransitionContext context)
        {
            _climbProgress = 0f;
            _direction = _blackboard.MoveInputX >= 0f ? 1 : -1;
            _startWorldY = _blackboard.Body != null ? _blackboard.Body.position.y : 0f;

            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            _hasWall = _blackboard.TryFindClimbableWall(info, _direction, out _wallHandle, out _wallTopWorldY);

            if (_blackboard.Body != null)
            {
                // 매달리기 도입부: 잔여 속도를 죽여 벽에 붙은 듯 고정한다. 이 상태 동안은(능동 상태와
                // 동일하게) 캐릭터 스스로 위치를 제어하므로 중력에 의한 낙하는 발생하지 않는다.
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = 0f;
                v.y = 0f;
                _blackboard.Body.linearVelocity = v;
            }

            // BUG-M7 대응 시연(UX_FLOW.md 31-2 #4) — 벽이 실제로 감지된 경우에만 유의미한 값이므로,
            // 감지 실패(_hasWall==false) 시에는 0으로 두어 "가뿐하네" 쪽으로 안전하게 수렴시킨다(어차피
            // 다음 Tick에서 곧바로 Fall로 전이되어 이 대사는 즉시 만료된다).
            _dialogueParams.ClimbHeightUnits = _hasWall ? Mathf.Max(0f, _wallTopWorldY - _startWorldY) : 0f;

            _ = new DialogueIntent(context, (id, dialogueParams) =>
            {
                var p = dialogueParams as ParkourClimbDialogueParams;
                float height = p != null ? p.ClimbHeightUnits : 0f;
                return height < 2.0f ? "가뿐하네" : "헉... 높다";
            });

            // TODO(Phase 2 렌더링): 양손 IK 그립 포즈, 손끝 마찰 먼지 파티클, 매달리기 Perlin 흔들림(UX_FLOW.md 4절).
        }

        public void Tick(float deltaTime)
        {
            if (_blackboard.Body == null)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            // 매 프레임 "잡을 곳"이 여전히 존재하는지 재확인 — 창이 이동했으면 목표 높이도 함께 갱신된다.
            if (!_hasWall || !_blackboard.TryGetFootholdTopWorldY(_wallHandle, out _wallTopWorldY))
            {
                // 잡을 곳이 사라짐(창 이동/닫힘) -> 즉시 Fall (UX_FLOW.md 4절 실패 처리 — 이 상태가 만든
                // 대사가 있었다면 TransitionGeneration 불일치로 같은 프레임에 자동 취소됨, 5절 계약).
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                return;
            }

            float climbDuration = _blackboard.Config != null ? _blackboard.Config.parkourClimbDuration : 0.5f;
            _climbProgress += climbDuration > 0f ? deltaTime / climbDuration : 1f;
            if (_climbProgress > 1f) _climbProgress = 1f;

            Vector2 pos = _blackboard.Body.position;
            pos.y = Mathf.Lerp(_startWorldY, _wallTopWorldY, _climbProgress);
            _blackboard.Body.position = pos;

            // BUG-P2-M1 대응(Major, docs/BUG_REPORT_PHASE2.md): Enter()의 1회성 속도 제로화만으로는
            // 부족하다 — Body는 여전히 일반 Dynamic Rigidbody2D라 매 FixedUpdate마다 중력이
            // linearVelocity.y에 조용히 계속 누적된다(등반 도중엔 위 pos.y Lerp가 매 프레임 위치를
            // 덮어써 화면상 안 보이지만, 등반 완료로 Idle/Walk에 전이된 직후 그 누적 속도가 그대로
            // 적용돼 착지 튐(pop)이 매번 재현됨). SnapToGround의 기존 관행(위치를 옮길 때마다 속도도
            // 함께 재확정)과 동일하게 여기서도 매 프레임 재확정한다.
            Vector2 v = _blackboard.Body.linearVelocity;
            v.y = 0f;
            _blackboard.Body.linearVelocity = v;

            if (_climbProgress >= 1f)
            {
                float deadzone = _blackboard.Config != null ? _blackboard.Config.moveInputDeadzone : 0.15f;
                StickmanStateId next = Mathf.Abs(_blackboard.MoveInputX) > deadzone ? StickmanStateId.Walk : StickmanStateId.Idle;
                _blackboard.Machine.ChangeState(next);
            }
        }

        public void Exit() { }
    }
}
