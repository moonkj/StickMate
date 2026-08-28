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
            // 보행 애니메이션 시작(2026-08-28 근본 재구현) — 매번 같은 자세(위상 0 = Idle 중립)에서
            // 다리 흔들기를 시작하도록 위상 타이머만 리셋한다. 예전에 여기서 걸던 HingeJoint2D 각도
            // 제한/모터 설정은 전부 사라졌다: 팔다리가 Kinematic이 되어 관절 모터라는 개념 자체가
            // 없어졌기 때문이다(States/StickmanPoseAnimator.cs 클래스 문서 참고). 실제 각도 세팅은
            // Tick()이 매 프레임 수행한다.
            _blackboard.GetPoseAnimator()?.ResetWalkPhase();
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

                // 보행 애니메이션(2026-08-28 근본 재구현): 다리/팔의 transform.localRotation을 실제
                // 수평 속도에 비례한 주파수의 사인파로 **직접** 세팅한다(물리 모터 구동 아님) — 정교한
                // IK가 아니라 "걷는 것처럼 보이는" 최소 절차적 애니메이션이라는 스코프는 그대로지만,
                // 이제 계산한 각도가 곧 실제 각도라 오버슈트/무너짐이 원천적으로 불가능하다.
                // States/StickmanPoseAnimator.cs 클래스 문서 참고.
                StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
                if (pose != null && _blackboard.Config != null)
                {
                    pose.TickWalkPose(deltaTime, Mathf.Abs(v.x), _blackboard.BuildPoseSettings(),
                        _blackboard.Config.walkCycleFrequencyPerSpeed, _blackboard.Config.walkCycleLegSwingDegrees,
                        _blackboard.Config.walkCycleArmSwingRatio, _blackboard.PoseSmoothingRate,
                        _blackboard.WalkSpeedSmoothingRate, _blackboard.Config.walkBounceAmplitude);
                }
            }
            // 좌우 반전(스프라이트 flip)은 Phase 2 렌더링 레이어 담당 — 여기서는 물리 이동/보행 애니메이션만.
        }

        public void Exit()
        {
            // 예전에는 여기서 다리/팔 모터를 끄고 각도 제한을 원복해야 했지만(모터 기반 구현), 이제는
            // 할 일이 없다: Walk를 벗어나면 다음 프레임부터 StickmanBlackboard.TickPose()가 현재 상태
            // ID를 보고 자동으로 Idle 중립 포즈(또는 RAGDOLL 물리 위임)를 적용한다. 상태별 정리 코드를
            // 빠뜨려서 생기는 버그 자체가 구조적으로 사라진 것이 이번 재구현의 핵심 이득 중 하나다.
        }
    }
}
