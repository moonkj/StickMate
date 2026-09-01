using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 정지 대기.
    /// 전이: 이동 입력 -> Walk / 점프 입력(+접지) -> Jump / 공격 입력 -> Attack /
    ///       벽·모서리 근접(+상승 입력) -> ParkourClimb / 외력 임계값 초과 -> Ragdoll(강제 인터럽트).
    /// Phase 1 구현 범위: 이동/점프 입력에 의한 Walk/Jump 전이와, 발판 이탈/화면 경계 이탈에 의한
    /// Fall 강제 전이만 다룬다. Attack/ParkourClimb/Ragdoll 전이는 Phase 2/3에서 추가된다.
    /// </summary>
    public sealed class IdleState : IStickmanState, IHasDialogueParams
    {
        private readonly StickmanBlackboard _blackboard;

        /// <summary>유휴 혼잣말(26-3절)이 이번 전이에서 고른 대사 줄 번호 스냅샷. 이 상태가 대사 매핑
        /// 함수에 구조적으로 노출하는 유일한 파라미터다(States/AttackState.AttackDialogueParams와 동일
        /// 관례) — Dialogue/AmbientChatter.cs 클래스 문서의 "원칙 1을 어기지 않는 방식" 참고.</summary>
        private readonly AmbientChatter.ChatterParams _chatterParams = new AmbientChatter.ChatterParams();

        public object DialogueParams => _chatterParams;

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
            // 유휴 혼잣말(docs/UX_FLOW.md 26-3절 "살아있는 느낌"). 확률/쿨다운 추첨과 대사 줄 선택은
            // TryRollChatter가 **텍스트를 만들기 전에** 전부 끝내고 그 결과를 _chatterParams에 스냅샷으로
            // 남긴다. 그래서 아래 DialogueIntent의 매핑 함수는 난수를 전혀 쓰지 않는 순수 함수이며,
            // "이 텍스트가 어느 Enter() 호출의 어느 파라미터에서 나왔는지"가 항상 역추적된다
            // (UX_FLOW.md 31-1/31-3). 추첨에 떨어지면 대사 자체가 만들어지지 않는다.
            //
            // ★ 2026-09-01 — 발판 상실 공중 유예(GroundLossHang)에서 되돌아온 전이는 **추첨하지 않는다.**
            // 그건 새 유휴 에피소드가 아니라 같은 에피소드의 복귀다(창 열거가 한 번 튀었다가 돌아온 것뿐).
            // 여기서 추첨을 돌리면 열거가 튈 때마다 말풍선 확률이 새로 생겨, 사용자가 아무 것도 하지
            // 않았는데 대사 빈도가 올라간다 — 이번 라운드의 리더 결정("연출만, 대사 없이")과 정면으로
            // 어긋나고, 사용자가 반복적으로 불만을 표해온 "요청하지 않은 대사"가 정확히 이것이다.
            if (context.From != StickmanStateId.GroundLossHang
                && AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle, _chatterParams))
            {
            // ★ 2026-09-01 — 발화 자격 게이트(UX_FLOW.md 5절 규칙 8)는 TryRollChatter 안에 있다.
            //   서술 대사는 상태가 끝나면 즉시 컷되므로(규칙 4-c ③), "0.08초 번쩍이고 사라지는
            //   글자"가 새 노이즈가 되지 않도록 **발화 시점에** 막는다. 계획 잔여 체류는 지어낸
            //   값이 아니라 두 사실의 결합이다: 배회 AI가 이 페이즈에 진입할 때 확정해 둔 길이의
            //   나머지(States/IPlannedDwellSource.cs)와, **그 계획이 지금 이 상태를 서술하는가**
            //   (StickmanBlackboard.PlannedDwellRemainingSecondsFor). 이동 의도가 이미 이 상태를
            //   부정하고 있으면 잔여는 0이다 — 다음 Tick에 실제로 나가기 때문이다.
            //   막히면 아래 분기 자체가 false가 되어 대사가 만들어지지 않는다 — 침묵은 거짓말이 아니다.
                _ = new DialogueIntent(context, AmbientChatter.Resolve);
            }
        }

        public void Tick(float deltaTime)
        {
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;
            if (_blackboard.GroundedTick(deltaTime, info)) return;

            // BUG-P1-M5 대응: 접지 중이거나 코요테 타임 이내일 때만 점프 허용(StickmanStateMachine.cs
            // 전이 규칙 주석 참고, Architect 결정으로 의도된 코요테 타임 채택).
            // 참고: Idle의 점프(AutoWanderController "제자리 점프" 26-1)는 방향 의도가 없는 수직 점프이므로
            // ParkourClimb 판정(방향 필요)을 의도적으로 건너뛴다 — 애매한 방향으로 벽을 잡으려다 허공에
            // 뜬 채 멈추는 것보다 평범한 수직 점프가 안전하다(UX_FLOW.md 4절 원칙). 파쿠르 진입은
            // WalkState.cs(진행 방향이 명확한 경우)만 담당한다.
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
