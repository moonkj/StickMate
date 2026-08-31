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

        // ★ 맨틀(mantle) — 등반이 끝났을 때 **턱 위에 실제로 올라서 있게** 하는 수평 이동(2026-08-29).
        // 왜 필요했나: 이 상태는 원래 y만 보간하고 x는 손대지 않았다. 그런데 진입 조건(TryFindClimbableWall)은
        // "지금 딛고 있는 발판의 경계 근처"일 뿐이라, 등반이 끝난 캐릭터는 여전히 **아래 발판 쪽 x**에
        // 있다 — 즉 턱 위가 아니라 턱 옆 허공이다. 그러면 다음 프레임의 접지 판정이 실패해 곧바로
        // 다시 떨어진다(등반이 통째로 무효화됨). 실제로 이 경로는 wanderEdgeJumpAttemptChance 기본값이
        // 0이 되면서 아무도 밟지 않아 드러나지 않았을 뿐이고, 2026-08-29에 "뛰어내린 뒤 다시 올라오기"를
        // 붙이면서 처음으로 상시 경로가 되어 발견되었다.
        private float _startWorldX;
        private bool _hasMantleTarget;

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
            _startWorldX = _blackboard.Body != null ? _blackboard.Body.position.x : 0f;

            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            _hasWall = _blackboard.TryFindClimbableWall(info, _direction, out _wallHandle, out _wallTopWorldY);
            _hasMantleTarget = TryComputeMantleTargetX(out float mantleTargetX);

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

            Debug.Log($"[벽타기] 진입 — 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}, " +
                $"벽핸들={_wallHandle}, 시작 월드=({_startWorldX:F3},{_startWorldY:F3}), " +
                $"벽 상단 Y={_wallTopWorldY:F3}(오를 높이 {(_hasWall ? _wallTopWorldY - _startWorldY : 0f):F3}유닛), " +
                $"올라설 X={(_hasMantleTarget ? mantleTargetX.ToString("F3") : "없음(수평 이동 생략)")}.");

            // TODO(Phase 2 렌더링): 양손 IK 그립 포즈, 손끝 마찰 먼지 파티클, 매달리기 Perlin 흔들림(UX_FLOW.md 4절).
        }

        /// <summary>
        /// 등반이 끝났을 때 서 있어야 할 x — 붙잡은 턱의 **가까운 쪽 모서리에서 안쪽으로
        /// StickConfig.parkourMantleInset만큼 들어간 지점**이다. 매 프레임 다시 계산한다(창이 옆으로
        /// 움직이면 올라설 자리도 함께 움직여야 하므로, 잡을 곳 재확인과 같은 계약).
        ///
        /// "가까운 쪽 모서리"는 진행 방향의 **반대편** 모서리다: 오른쪽으로 오르면 그 턱의 왼쪽 끝,
        /// 왼쪽으로 오르면 오른쪽 끝. 턱이 inset보다 좁으면 반대편 끝을 넘지 않도록 클램프한다.
        /// </summary>
        private bool TryComputeMantleTargetX(out float targetX)
        {
            targetX = _startWorldX;
            if (!_hasWall) return false;
            if (!_blackboard.TryGetFootholdEdgeWorld(_wallHandle, -_direction, out _, out float nearEdgeX)) return false;
            if (!_blackboard.TryGetFootholdEdgeWorld(_wallHandle, _direction, out _, out float farEdgeX)) return false;

            // ★ 2026-08-31 — 설정값을 직접 읽지 않는다. 인셋은 이제 경계 판정 거리와 같은 입력에서
            // 유도되는 값이고(StickmanBlackboard.ParkourMantleInsetWorld), 그 유도가 없으면 캐릭터
            // 크기 다이얼로 배율을 1.125 넘게 올리는 순간 "올라선 자리가 이미 경계"가 다시 성립한다.
            // 폴백(Config 없음)도 그 프로퍼티가 코드 기본값 0.6으로 받친다.
            float inset = _blackboard.ParkourMantleInsetWorld;
            float desired = nearEdgeX + _direction * Mathf.Max(0f, inset);
            targetX = Mathf.Clamp(desired, Mathf.Min(nearEdgeX, farEdgeX), Mathf.Max(nearEdgeX, farEdgeX));
            return true;
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
            // 맨틀 수평 이동(위 _startWorldX 필드 주석 참고). 목표를 매 프레임 다시 구해 창이 움직여도
            // 따라간다. 구하지 못하면(테스트 리그 등 발판 조회 실패) 예전처럼 x를 건드리지 않는다.
            if (_hasMantleTarget && TryComputeMantleTargetX(out float mantleTargetX))
            {
                pos.x = Mathf.Lerp(_startWorldX, mantleTargetX, _climbProgress);
            }
            // 몸 위치를 쓰는 유일한 창구(StickmanBlackboard.MoveBodyToWorld) — Rigidbody2D.position만
            // 쓰면 그 프레임에 그려지는 Transform이 낡은 좌표로 남는다(autoSyncTransforms 꺼짐).
            // 여기는 0.5초 보간이라 프레임당 이동량이 작지만, 창이 갑자기 크게 움직이면 그만큼 튄다.
            _blackboard.MoveBodyToWorld(pos);

            // BUG-P2-M1 대응(Major, docs/BUG_REPORT_PHASE2.md): Enter()의 1회성 속도 제로화만으로는
            // 부족하다 — Body는 여전히 일반 Dynamic Rigidbody2D라 매 FixedUpdate마다 중력이
            // linearVelocity.y에 조용히 계속 누적된다(등반 도중엔 위 pos.y Lerp가 매 프레임 위치를
            // 덮어써 화면상 안 보이지만, 등반 완료로 Idle/Walk에 전이된 직후 그 누적 속도가 그대로
            // 적용돼 착지 튐(pop)이 매번 재현됨). SnapToGround의 기존 관행(위치를 옮길 때마다 속도도
            // 함께 재확정)과 동일하게 여기서도 매 프레임 재확정한다.
            // x도 함께 0으로 확정한다 — 이제 이 상태가 x를 직접 구동하므로(맨틀), 진입 직전 걷던 속도가
            // 남아 있으면 매 프레임 위치 대입과 물리 적분이 서로 밀어내며 미세하게 어긋난다.
            _blackboard.Body.linearVelocity = Vector2.zero;

            if (_climbProgress >= 1f)
            {
                // 올라선 발판을 즉시 고착한다 — 이게 없으면 다음 프레임의 접지 판정이 핸들 0(미획득)
                // 상태로 목록 첫 매치를 새로 고르게 되고, 마침 아래 발판이 먼저 걸리면 방금 오른 턱을
                // 두고 도로 내려간 것처럼 보인다. GroundedTick의 "접지 획득" 경로와 같은 취지다.
                _blackboard.CurrentFootholdHandle = _wallHandle;
                _blackboard.ReportFootholdChangeIfNeeded("벽타기 완료 — 턱 위에 올라섬");
                _blackboard.ResetGroundLossTimer();

                // ★ 배회 AI에게 "방금 턱 위로 올라섰다"를 알린다(2026-08-29). 이 신호가 없으면 등반을
                // 유발했던 경계 판정이 그대로 살아 있어, 배회 AI가 진행 방향을 방금 올라온 바깥쪽으로
                // 뒤집고 곧바로 같은 모서리로 다시 뛰어내린다(StickmanBlackboard.ClimbMantleSequence의
                // 실측 근거 주석 참고). 상태 머신을 구독시키지 않고 블랙보드 카운터로 알리는 이유도 거기 적었다.
                _blackboard.ReportClimbMantleCompleted(_direction);

                Debug.Log($"[벽타기] 완료 — 올라선 월드=({pos.x:F3},{pos.y:F3}), 발판핸들={_wallHandle}, " +
                    $"올라선 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}(맨틀 신호 #{_blackboard.ClimbMantleSequence}).");

                float deadzone = _blackboard.Config != null ? _blackboard.Config.moveInputDeadzone : 0.15f;
                StickmanStateId next = Mathf.Abs(_blackboard.MoveInputX) > deadzone ? StickmanStateId.Walk : StickmanStateId.Idle;
                _blackboard.Machine.ChangeState(next);
            }
        }

        public void Exit() { }
    }
}
