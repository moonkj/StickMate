using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: StickConfig.gravityScale에 따라 자유낙하(중력 자체는 Rigidbody2D 설정으로 처리 —
    /// 이 상태는 착지/화면이탈 감지만 담당).
    /// 전이: 발판 착지 감지 -> Idle/Walk(착지 시 이동 입력 유무로 분기) /
    ///       화면(발판 좌우 범위) 이탈 -> Fall 유지(사실상 no-op) /
    ///       외력 임계값 초과 -> Ragdoll(강제 인터럽트, Phase 2).
    /// </summary>
    public sealed class FallState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        // 착지 확정 유예 타이머. StickmanStateMachine.cs 전이 규칙 주석("Fall -> Idle/Walk : 발판 착지
        // 감지(fallGraceDuration 유예 적용)")을 그대로 반영 — 스쳐 지나가는 한 프레임짜리 접촉만으로
        // 착지가 확정돼 바로 다음 프레임에 다시 Fall로 되돌아가는 채터링(chattering)을 막기 위함.
        // StickConfig.fallGraceDuration은 "발판을 잃을 때"(StickmanBlackboard.GroundedTick)와
        // "착지를 확정할 때"(여기) 양쪽에 재사용되는 공용 히스테리시스 값이다.
        private float _landingConfirmTimer;

        public FallState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.Fall;

        public void Enter(StateTransitionContext context)
        {
            _landingConfirmTimer = 0f;
            // TODO(Phase 2): 낙하 포즈(팔다리 늘어짐) 전환 — Active Ragdoll IK 블렌딩.
        }

        public void Tick(float deltaTime)
        {
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return; // 이미 Fall이라 사실상 no-op이지만 안전하게 유지

            if (!info.Grounded)
            {
                _landingConfirmTimer = 0f;
                return;
            }

            _landingConfirmTimer += deltaTime;
            float grace = _blackboard.Config != null ? _blackboard.Config.fallGraceDuration : 0.1f;
            if (_landingConfirmTimer < grace) return;

            _blackboard.ResetGroundLossTimer();
            float deadzone = _blackboard.Config != null ? _blackboard.Config.moveInputDeadzone : 0.15f;
            StickmanStateId next = Mathf.Abs(_blackboard.MoveInputX) > deadzone ? StickmanStateId.Walk : StickmanStateId.Idle;
            _blackboard.Machine.ChangeState(next);
        }

        public void Exit() { }
    }
}
