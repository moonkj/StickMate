using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: StickConfig.jumpForce로 상승.
    /// 전이: 상승 속도가 0 이하로 전환(정점 통과) -> Fall / 화면(발판 좌우 범위) 이탈 -> Fall /
    ///       외력 임계값 초과 -> Ragdoll(강제 인터럽트, Phase 2).
    /// </summary>
    public sealed class JumpState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        public JumpState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.Jump;

        public void Enter(StateTransitionContext context)
        {
            if (_blackboard.Body == null) return;
            float jumpForce = _blackboard.Config != null ? _blackboard.Config.jumpForce : 6f;
            // 배율 반영 속도 — 공중 수평 이동도 지상 보행과 같은 속도여야 착지 순간 속도가 튀지 않는다.
            float walkSpeed = _blackboard.Config != null ? _blackboard.Config.ResolveWalkSpeed() : 2.5f;
            // 수평 속도는 입력 방향을 그대로 반영해 자연스러운 점프 궤적을 만든다(제자리 점프 방지).
            _blackboard.Body.linearVelocity = new Vector2(_blackboard.MoveInputX * walkSpeed, jumpForce);
        }

        public void Tick(float deltaTime)
        {
            if (_blackboard.Body == null) return;

            // 점프 중에도 화면(발판 좌우 범위) 이탈 검사는 계속 수행 — FootholdPoller 캐시만 참조하므로
            // OS 재호출은 없다(BUG-M3 컨벤션).
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;

            if (_blackboard.Body.linearVelocity.y <= 0f)
            {
                // 정점 통과(상승 속도가 0 이하로 전환) -> Fall.
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
            }
        }

        public void Exit() { }
    }
}
