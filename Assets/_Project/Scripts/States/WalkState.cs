using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

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
    public sealed class WalkState : IStickmanState, IHasDialogueParams
    {
        private readonly StickmanBlackboard _blackboard;

        /// <summary>보행 혼잣말(26-3절)이 이번 전이에서 고른 대사 줄 번호 스냅샷 — IdleState와 동일한
        /// 파라미터 파이프라인(Dialogue/AmbientChatter.cs 참고). Idle과 쿨다운 타이머를 공유한다.</summary>
        private readonly AmbientChatter.ChatterParams _chatterParams = new AmbientChatter.ChatterParams();

        public object DialogueParams => _chatterParams;

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

            // 보행 혼잣말(26-3절) — IdleState.Enter()와 동일한 파이프라인. 대사 표와 확률만 다르고
            // 매핑 함수는 같은 AmbientChatter.Resolve 하나를 공유한다(UX_FLOW.md 31-1 "같은 매핑 함수
            // 안의 분기만 허용" — Idle/Walk 분기는 그 함수 내부의 상태 ID 분기다).
            if (AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Walk, _chatterParams))
            {
                _ = new DialogueIntent(context, AmbientChatter.Resolve);
            }
        }

        public void Tick(float deltaTime)
        {
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;
            if (_blackboard.GroundedTick(deltaTime, info)) return;

            // ★ 매달려 내려가기 진입 판정(2026-08-28, 사용자 명시 요청 "내려갈때도 매달려서 내려가는형태로").
            // 점프 분기보다 **먼저** 본다 — 둘 다 발판 경계에서 성립할 수 있는데, 매달리기 펄스는
            // AutoWanderController가 "내려갈 발판이 실제로 있다"까지 확인한 뒤에만 발생시키므로 더
            // 구체적인 의도다(펄스가 없으면 이 블록은 통째로 건너뛰어져 기존 거동과 완전히 같다).
            // info.Grounded와 목적지 존재를 여기서 **다시** 확인하는 이유: 의도가 만들어진 프레임과
            // 소비되는 프레임 사이에 창이 닫히거나 발판을 잃었을 수 있고, 그 경우 매달릴 모서리 자체가
            // 없는 채로 상태에 진입하게 된다(States/LedgeHangState.cs의 안전 규칙 선행 조건).
            if (_blackboard.LedgeHangPressed && info.Grounded)
            {
                int hangDirection = _blackboard.MoveInputX >= 0f ? 1 : -1;
                if (_blackboard.TryFindDescendTarget(info, hangDirection, out _, out _))
                {
                    _blackboard.Machine.ChangeState(StickmanStateId.LedgeHang);
                    return;
                }
            }

            // ★ 그냥 뛰어내리기 진입 판정(2026-08-29, 사용자 결정 "낙차가 작으면 뛰어내리게 한다").
            // 매달리기와 같은 자리에서, 같은 이유로(의도가 만들어진 프레임과 소비되는 프레임 사이에 창이
            // 닫혔을 수 있다) 목적지를 **다시** 확인한다. 새 상태를 만들지 않고 기존 Fall로 보내는 것이
            // 이 동작의 전부다 — 매달리기와 달리 잡을 곳도, 유지시간도, 페이즈도 없기 때문이다.
            // 안전 규칙은 전부 기존 것을 그대로 물려받는다: 착지 확정은 FallState의 스윕 교차 판정이,
            // 화면 밖 금지와 무한 낙하 금지는 StickmanBlackboard.EnforceScreenBoundsAndRescue가 맡는다.
            if (_blackboard.HopDownPressed && info.Grounded)
            {
                int hopDirection = _blackboard.MoveInputX >= 0f ? 1 : -1;
                if (_blackboard.TryFindHopDownTarget(info, hopDirection, out long hopHandle, out float hopTopY)
                    && _blackboard.Body != null)
                {
                    // ── (1) 방금 떠난 발판을 짧은 시간 착지 후보에서 제외한다(drop-through).
                    // ★ 이 장치가 없으면 뛰어내리기가 통째로 무효가 된다(2026-08-29 실측으로 확인).
                    // 왜: 서 있는 몸은 발판 상단선에 **정확히** 스냅돼 있다(StickmanBlackboard.SnapToGround).
                    // 그 상태로 Fall에 들어가면 FallState의 스윕 교차 판정이 보는 선분은 "상단선 위(같은
                    // 높이) -> 상단선 아래"가 되고, x가 아직 그 발판 위라 **방금 떠난 그 발판을 위에서
                    // 관통한 것**으로 인정된다. 실측 로그: `[FallState] 착지 확정 — 발판핸들=8001,
                    // 낙하높이=0.00유닛` — 즉 제자리에서 도로 착지했다. 수평 속도만으로는 못 막는다
                    // (모서리까지 남은 거리를 지나가기 전에 이미 첫 프레임의 교차가 성립한다).
                    //
                    // ★ 2026-08-29 2차 수정 — 왜 순간이동 대신 이 방식인가(리더 지시 "발을 뗄 때 앞으로
                    // 튀는 거리를 줄여라", 채택 근거):
                    //   · 예전 구현은 몸을 "모서리 + hopDownProbeOutward"로 **순간이동**시켜 위 문제를
                    //     피했다. 실측 전진량 11.900 -> 12.210 = 0.31유닛(약 25pt)을 한 프레임에 건너뛰었고,
                    //     사용자가 반복적으로 신고해온 순간이동성 아티팩트("갑자기 순간이동", "마우스로
                    //     끌었는데 갑자기 다른창위로 올라감")와 같은 종류의 현상이었다.
                    //   · 값을 줄여 0.15유닛 이하로 낮추는 선택지도 있었지만, 그래도 순간이동은 남는다.
                    //     플랫포머의 표준 관행인 drop-through("아래로 내려가기" 중에는 방금 떠난 발판을
                    //     통과시킨다)를 쓰면 **전진량을 0으로** 만들 수 있어 이쪽을 채택했다.
                    //   · 스윕 판정 계약은 깨지지 않는다: GroundSensor.TryFindLandingCrossing에 기본값 0인
                    //     ignoreHandle 파라미터가 하나 늘었을 뿐이고, 그 값을 채우는 곳은 이 블록 하나다.
                    //     매달리기 해제/던지기/일반 낙하는 유예를 설정하지 않으므로 전혀 영향이 없다.
                    //   · 유예는 시간이 지나면 스스로 풀린다(해제 호출 없음) — 어떤 경로로 상태가 바뀌어도
                    //     "무시가 영구히 남는" 사고가 구조적으로 불가능하다.
                    float ignoreDuration = _blackboard.Config != null
                        ? _blackboard.Config.hopDownDropThroughIgnoreDuration
                        : 0.25f;
                    _blackboard.BeginDropThroughIgnore(info.GroundedFootholdHandle, ignoreDuration);

                    // ── (2) "살짝 앞으로 내딛는" 느낌 — 수평 속도 한 번. FallState는 x속도를 건드리지
                    // 않으므로(중력만 y에 작용) 이 한 번의 부여가 착지까지 그대로 유지되고, 몸은 이 속도로
                    // **걸어서** 모서리를 넘는다(순간이동 없음). y는 0으로 확정한다: 걸어 나가듯 떨어져야지
                    // 위로 뛰어오르면 "점프"가 되어버린다.
                    // ★ 배율 반영 속도(StickConfig.ResolveWalkSpeed 문서 — 보폭이 배율에 비례하므로 속도도 함께
                    // 비례해야 보행 사이클 주파수가 유지되고 디딤발이 미끄러지지 않는다).
                    float walkSpeed = _blackboard.Config != null ? _blackboard.Config.ResolveWalkSpeed() : 2.5f;
                    float scale = _blackboard.Config != null ? _blackboard.Config.hopDownStepOffSpeedScale : 0.8f;
                    Vector2 before = _blackboard.Body.position;
                    _blackboard.Body.linearVelocity = new Vector2(hopDirection * walkSpeed * scale, 0f);

                    float edgeWorldX = hopDirection > 0 ? info.CurrentFootholdRightWorldX : info.CurrentFootholdLeftWorldX;
                    Debug.Log($"[뛰어내리기] 발을 뗍니다 — 방향={(hopDirection > 0 ? "오른쪽" : "왼쪽")}, " +
                        $"모서리 월드X={edgeWorldX:F3}, 발 뗀 X={before.x:F3}(모서리까지 남은 거리 " +
                        $"{Mathf.Abs(edgeWorldX - before.x):F3}유닛), 전진 {Mathf.Abs(_blackboard.Body.position.x - before.x):F3}유닛" +
                        $"(순간이동 없음 — drop-through {ignoreDuration:F2}초로 발판핸들 {info.GroundedFootholdHandle} 통과), " +
                        $"월드Y={before.y:F3}, 낙차={(info.GroundWorldY - hopTopY):F3}유닛, " +
                        $"예상 착지 발판핸들={hopHandle}(상단 Y={hopTopY:F3}), " +
                        $"내딛는 수평속도={(hopDirection * walkSpeed * scale):F2}유닛/초.");

                    _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                    return;
                }
            }

            // ★ 되올라가기 진입 판정(2026-08-29) — 뛰어내린 캐릭터가 다시 올라오는 유일한 경로다.
            // 아래 점프 분기 안의 ParkourClimb 판정과 목적지는 같지만, 벽을 못 찾았을 때 **점프로
            // 흘러내리지 않는다**는 점이 다르다(IMovementIntentSource.StepUpRequested 문서 참고).
            if (_blackboard.StepUpPressed && info.Grounded)
            {
                int stepUpDirection = _blackboard.MoveInputX >= 0f ? 1 : -1;
                if (_blackboard.TryFindClimbableWall(info, stepUpDirection, out _, out _))
                {
                    _blackboard.Machine.ChangeState(StickmanStateId.ParkourClimb);
                    return;
                }
            }

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
                float speed = _blackboard.Config != null ? _blackboard.Config.ResolveWalkSpeed() : 2.5f;
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = move * speed;
                _blackboard.Body.linearVelocity = v;

                // 보행 애니메이션(2026-08-28): 8개 키포즈 표를 Catmull-Rom으로 보간해 다리/팔의
                // transform.localRotation을 **직접** 세팅한다(물리 모터 구동 아님, 사인파도 아님).
                // 사이클 주파수는 임의 계수가 아니라 여기서 넘기는 실제 수평 속도에서 역산되므로
                // 디딤발이 바닥에서 미끄러지지 않는다. States/StickmanPoseAnimator.cs 문서 참고.
                StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
                if (pose != null && _blackboard.Config != null)
                {
                    pose.TickWalkPose(deltaTime, Mathf.Abs(v.x), _blackboard.BuildPoseSettings(),
                        _blackboard.PoseSmoothingRate, _blackboard.WalkSpeedSmoothingRate,
                        _blackboard.Config.walkFootGroundingBlend,
                        _blackboard.Config.walkPoseAmplitudeScale, _blackboard.Config.walkStrideScale);
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
