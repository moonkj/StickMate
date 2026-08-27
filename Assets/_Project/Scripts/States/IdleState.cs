using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 정지 대기.
    /// 전이: 이동 입력 -> Walk / 점프 입력(+접지) -> Jump / 공격 입력 -> Attack /
    ///       벽·모서리 근접(+상승 입력) -> ParkourClimb / 외력 임계값 초과 -> Ragdoll(강제 인터럽트).
    /// Phase 1 구현 범위: 이동/점프 입력에 의한 Walk/Jump 전이와, 발판 이탈/화면 경계 이탈에 의한
    /// Fall 강제 전이만 다룬다. Attack/ParkourClimb/Ragdoll 전이는 Phase 2/3에서 추가된다.
    /// </summary>
    public sealed class IdleState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        public IdleState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.Idle;

        public void Enter(StateTransitionContext context)
        {
            // 정지 시 잔여 수평 속도를 제거해 미끄러지듯 멈추는 것을 방지.
            if (_blackboard.Body != null)
            {
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = 0f;
                _blackboard.Body.linearVelocity = v;
            }
            // TODO(Phase 2): 필요 시 new DialogueIntent(context, id => "...") 로 유휴 잡담 대사 생성.
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
                _blackboard.Machine.ChangeState(StickmanStateId.Jump);
                return;
            }

            float deadzone = _blackboard.Config != null ? _blackboard.Config.moveInputDeadzone : 0.15f;
            if (Mathf.Abs(_blackboard.MoveInputX) > deadzone)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Walk);
            }
        }

        public void Exit() { }
    }
}
