using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// docs/UX_FLOW.md 27-1절 윈도우 창 도둑 — 대상 창을 붙잡고 미는/당기는 시늉을 2회 시도한 뒤
    /// 반드시 포기하는 순수 연출(성공 케이스 자체가 설계에 없음). 대상 창 선정/실제 이동·닫힘 감시는
    /// Interaction/WindowTheftDirector.cs가 전담하고, 이 상태는 "확정된 시도 진행 상황"만 표현한다.
    ///
    /// 대사(원칙 1): 1/2회차 시도 자체는 대사가 없다(UX 원문에 명시된 대사가 없음 — 순수 애니메이션).
    /// 2회 시도 후 "포기" 만큼은 확정된 실패 상태에서 파생되어야 하므로, RagdollState(반복 피격)와
    /// 동일한 self-transition 패턴(Architect 결정, Tasklist.md 교차 레이어 로그)을 재사용한다 — Attempt2가
    /// 끝나는 순간 자기 자신에게 재전이해 Enter()를 다시 실행시키고, 그 재실행된 Enter()가 GiveUp 페이즈로
    /// 진입하며 대사를 만든다. Tick() 도중에는 어떤 DialogueIntent도 생성하지 않는다(9절-1/31-1 원칙).
    /// </summary>
    public sealed class WindowTheftState : IStickmanState
    {
        private enum Phase
        {
            Attempt1,
            Attempt2,
            GiveUp,
        }

        private readonly StickmanBlackboard _blackboard;
        private Phase _phase;
        private float _timer;

        // Attempt2 -> GiveUp 자기 재전이를 요청하기 위한 1회성 신호. Enter()가 이 값을 소비해 진입 페이즈를
        // 결정한다(false=최초 진입=Attempt1, true=self-transition에 의한 재진입=GiveUp).
        private bool _pendingGiveUp;

        public WindowTheftState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.WindowTheft;

        public void Enter(StateTransitionContext context)
        {
            _timer = 0f;

            if (_pendingGiveUp)
            {
                _pendingGiveUp = false;
                _phase = Phase.GiveUp;

                // 확정된 실패(2회 시도 소진) 상태에서 파생된 유일한 대사 — 원칙 1 그대로.
                // 종류=Reaction: "방금 포기했다"는 다음 상태에서도 참이다(5절 규칙 4-a).
            _ = new DialogueIntent(context, (id) => DialogueLine.React("헥헥... 안 되겠다..."));
                return;
            }

            _phase = Phase.Attempt1;
        }

        public void Tick(float deltaTime)
        {
            // ★ 2026-08-29 — TimedSpectacleState.Tick()과 같은 이유로 같은 수정을 적용한다(그 파일
            // 주석 참고). 이 상태가 서는 발판(Dock/타 앱 창 상단)은 물리 콜라이더가 없는 논리 발판이라,
            // GroundedTick()을 안 부르면 시도 도중 자유낙하해 랙돌로 강제 취소된다(실측: 0.5초 만에
            // 발생, 착지 충격량이 임계 초과). 이 결함 때문에 창 도둑이 Dock/창 위에서는 한 번도 완주된
            // 적이 없었다.
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;
            if (_blackboard.GroundedTick(deltaTime, info)) return;

            _timer += deltaTime;
            float attemptDuration = _blackboard.Config != null ? _blackboard.Config.windowTheftAttemptDuration : 1.2f;
            float giveUpDuration = _blackboard.Config != null ? _blackboard.Config.windowTheftGiveUpDuration : 1.5f;

            switch (_phase)
            {
                case Phase.Attempt1:
                    if (_timer >= attemptDuration)
                    {
                        _timer = 0f;
                        _phase = Phase.Attempt2; // 대사 없는 순수 페이즈 전환 — 새 DialogueIntent를 만들지 않으므로 self-transition 불필요.
                    }
                    break;

                case Phase.Attempt2:
                    if (_timer >= attemptDuration)
                    {
                        // 포기 대사는 Enter() 안에서만 만들 수 있으므로(원칙 1), 자기 자신에게 재전이시켜
                        // Enter()를 다시 태운다(RagdollState의 반복 피격과 동일한 self-transition 패턴).
                        _pendingGiveUp = true;
                        _blackboard.Machine.ChangeState(StickmanStateId.WindowTheft, isForcedInterrupt: false);
                    }
                    break;

                case Phase.GiveUp:
                    if (_timer >= giveUpDuration)
                    {
                        _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                    }
                    break;
            }
        }

        public void Exit() { }
    }
}
