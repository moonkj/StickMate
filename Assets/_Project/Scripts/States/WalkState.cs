using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: StickConfig.walkSpeed로 발판 위를 이동.
    /// 전이: 이동 입력 해제 -> Idle / 점프 입력 -> Jump / 발판 이탈(유예시간 초과) -> Fall /
    ///       공격 입력 -> Attack / 벽·모서리 근접(+상승 입력) -> ParkourClimb /
    ///       외력 임계값 초과 -> Ragdoll(강제 인터럽트).
    /// Phase 1 구현 범위: 이동/정지/점프 전이와 Fall 강제 전이(발판 이탈·화면 경계 이탈). Attack/
    /// ParkourClimb/Ragdoll 전이는 Phase 2/3에서 추가된다.
    /// </summary>
    public sealed class WalkState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        public WalkState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.Walk;

        public void Enter(StateTransitionContext context)
        {
            // TODO(Phase 2): 보행 IK/애니메이션 시작.
        }

        public void Tick(float deltaTime)
        {
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;
            if (_blackboard.GroundedTick(deltaTime, info)) return;

            // BUG-P1-M5 대응: 접지 중이거나 코요테 타임 이내일 때만 점프 허용(StickmanStateMachine.cs
            // 전이 규칙 주석 참고, Architect 결정으로 의도된 코요테 타임 채택).
            if (_blackboard.JumpPressed && _blackboard.IsWithinCoyoteTime(info))
            {
                // ParkourClimb 진입 판정(아키텍처 0절, UX_FLOW.md 4절/26-2): AutoWanderController가
                // 발판 경계에서 발생시키는 JumpRequested 펄스가, 마침 진행방향에 그보다 눈에 띄게 높은
                // 발판(벽)이 있을 때 자연스럽게 등반으로 이어지는 확장. info.Grounded를 명시적으로
                // 요구해 공중(코요테 타임)에서는 벽을 잡지 않도록 한다.
                if (info.Grounded)
                {
                    int climbDirection = _blackboard.MoveInputX >= 0f ? 1 : -1;
                    if (_blackboard.TryFindClimbableWall(info, climbDirection, out _, out _))
                    {
                        _blackboard.Machine.ChangeState(StickmanStateId.ParkourClimb);
                        return;
                    }
                }

                _blackboard.Machine.ChangeState(StickmanStateId.Jump);
                return;
            }

            float deadzone = _blackboard.Config != null ? _blackboard.Config.moveInputDeadzone : 0.15f;
            float move = _blackboard.MoveInputX;
            if (Mathf.Abs(move) <= deadzone)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            if (_blackboard.Body != null)
            {
                float speed = _blackboard.Config != null ? _blackboard.Config.walkSpeed : 2.5f;
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = move * speed;
                _blackboard.Body.linearVelocity = v;
            }
            // 좌우 반전(스프라이트 flip)/보행 애니메이션은 Phase 2 렌더링 레이어 담당 — 여기서는 물리 이동만.
        }

        public void Exit() { }
    }
}
