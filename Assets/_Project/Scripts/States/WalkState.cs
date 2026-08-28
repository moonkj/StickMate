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
            // 보행 애니메이션 시작(Architect 결정, 2026-08-28) — 매번 같은 자세(위상 0)에서 다리 흔들기를
            // 시작하도록 위상 타이머를 리셋하고, 다리/팔 관절에 물리적 각도 제한을 건다(실측 지적 대응,
            // WalkCycleAnimator.cs 클래스 문서 "각도 제한/모터 속도 상한" 참고 — 각도 제한 없이 모터만
            // 켜면 다리가 몸통 반대편까지 감겨버리는 사고가 실측으로 확인됐다). 실제 모터 구동은 Tick()에서
            // 매 프레임 수행.
            WalkCycleAnimator animator = _blackboard.GetWalkCycleAnimator();
            if (animator != null && _blackboard.Config != null)
            {
                animator.EnterWalking(_blackboard.Config.walkCycleLegAngleLimitDegrees,
                    _blackboard.Config.walkCycleArmAngleLimitDegrees);
            }
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

                // 보행 애니메이션(Architect 결정, 2026-08-28): 다리(+팔) HingeJoint2D를 실제 수평 속도에
                // 비례한 주파수의 사인파 목표각으로 구동한다 — 정교한 IK가 아니라 "걷는 것처럼 보이는"
                // 최소 절차적 애니메이션. WalkCycleAnimator.cs 클래스 문서 참고.
                WalkCycleAnimator animator = _blackboard.GetWalkCycleAnimator();
                if (animator != null && _blackboard.Config != null)
                {
                    animator.Tick(deltaTime, Mathf.Abs(v.x), _blackboard.Config.walkCycleFrequencyPerSpeed,
                        _blackboard.Config.walkCycleLegSwingDegrees, _blackboard.Config.walkCycleMotorGain,
                        _blackboard.Config.walkCycleMaxMotorTorque, _blackboard.Config.walkCycleMaxMotorSpeedDegPerSec);
                }
            }
            // 좌우 반전(스프라이트 flip)은 Phase 2 렌더링 레이어 담당 — 여기서는 물리 이동/보행 애니메이션만.
        }

        public void Exit()
        {
            // Idle/Fall/Jump/Ragdoll 등 어디로 전이하든 다리/팔 모터를 반드시 끈다(WalkCycleAnimator.cs
            // 클래스 문서 "RAGDOLL과의 충돌 방지" 참고) — StickmanStateMachine.ChangeState()는
            // isForcedInterrupt 여부와 무관하게 항상 새 상태 Enter() 이전에 이 Exit()을 먼저 호출하므로,
            // Ragdoll로 강제 인터럽트되는 경우에도 RagdollRig.EnterRagdoll()보다 항상 먼저 실행된다.
            _blackboard.GetWalkCycleAnimator()?.StopWalking();
        }
    }
}
