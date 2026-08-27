using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.Platform;

namespace StickMate.States
{
    /// <summary>
    /// 가출(docs/UX_FLOW.md 20절, 24절 "반항 2단계") — 스트레스 게이지가 확정 임계값에 도달하면
    /// Interaction/RunawayDirector.cs가 확률이 아니라 확정으로 이 상태에 진입시킨다.
    ///
    /// 페이즈: Fleeing(뛰어가는 애니메이션 홀드, 실제 이동 연출은 Phase2+ 렌더링) → Hidden(은신처로
    /// 텔레포트 + 렌더러 숨김, 클릭으로 발견 대기 + 자동 타임아웃) → Found(발견됨, 렌더러 다시 표시,
    /// 간식 대기) → Reconciling(간식을 받아 화해 대사) 또는 SelfReturning(자동 타임아웃/수동 소환/
    /// 긴급 강제소환으로 스스로 복귀하는 대사) → Idle.
    ///
    /// 대사(원칙 1): Fleeing 최초 진입에서 "나 안 해!"(확정 상태에서 파생, 예고형 아님 — 이미 삐져서
    /// 뛰쳐나가는 중이라는 현재형 사실). Reconciling/SelfReturning 진입 대사도 BattleMinigameState/
    /// WindowTheftState와 동일한 self-transition 패턴(Architect 결정, Tasklist.md 교차 레이어 로그)을
    /// 재사용한다 — 페이즈 전환 순간의 판정과 그 판정에서 파생된 대사가 항상 같은 프레임의 같은
    /// Enter() 호출 안에서 함께 확정되도록 강제한다.
    ///
    /// 물리(20절 "찾기" 요구사항과의 접점): Hidden 동안 캐릭터를 Kinematic으로 전환한다 —
    /// Rigidbody2D.simulated=false(StickmanAgent.Suspend()가 전체화면 은닉에 쓰는 방식)가 아니라
    /// Kinematic을 쓰는 이유는, simulated=false가 Physics2D 레이캐스트/쿼리 대상에서 콜라이더를
    /// 완전히 제외시킬 수 있어(Unity 공식 문서) Interaction/StickmanClickHitbox.cs의 OnMouseDown이
    /// 더 이상 발동하지 않게 될 위험이 있기 때문이다 — "화면 구석을 클릭해서 찾는다"는 20절의 핵심
    /// 상호작용이 Kinematic이어야만 계속 동작한다(DragThrowState와 동일한 안전한 전례).
    ///
    /// 렌더러 숨김/표시는 StickmanBlackboard.SetCharacterVisible(Core/StickmanAgent.cs가 자신의 기존
    /// private SetRenderersEnabled를 노출)만 사용하고, StickmanAgent._isSuspended(전체화면 은닉)와는
    /// 완전히 독립이다 — 20절 예외 상태: "가출 상태는 이미 화면에 안 보이는 상태이므로 전체화면 감지가
    /// 와도 특별히 취소할 필요는 없다"를 그대로 반영해, 이 상태를 StickmanAgent.Suspend()의 강제
    /// Idle 목록에 넣지 않았다(Tasklist.md 교차 레이어 영향 로그에 판단 근거 기록) — Suspended가 되면
    /// StickmanStateMachine.Tick() 자체가 건너뛰어지므로 이 상태의 내부 타이머(자동 복귀 타임아웃
    /// 포함)도 함께 멈췄다가 재개된다. 이는 "숨어있는 동안 방치돼도 안 들키게" 보호하는 셈이라 20절
    /// 취지와 상충하지 않는다.
    ///
    /// BUG-P5-M1 대응(Major, docs/BUG_REPORT_PHASE5.md — Debugger 발견): 위 "완전히 독립"이라는 설계
    /// 의도와 달리, 실제로는 StickmanAgent.Resume()이 이 상태를 전혀 모른 채 무조건
    /// SetRenderersEnabled(true)를 호출했다 — Hidden 페이즈 중 전체화면 Suspend/Resume이 한 번이라도
    /// 왕복하면 아직 발견되지 않은 캐릭터가 강제로 노출됐다. 지금은 HideCharacterAtHideSpot()/
    /// ShowCharacterRevealed()/RestoreCharacter()/Exit()가 StickmanBlackboard.IsCharacterHiddenByRunaway
    /// 플래그를 함께 관리해, Resume()이 그 플래그를 확인하고 자기 복원 호출을 건너뛰도록 최소 접점 하나만
    /// 추가했다(전체 독립성 설계 자체는 유지 — Resume()이 "묻는" 것이지 RunawayState가 Suspend 경로에
    /// 개입하는 것은 아니다).
    /// </summary>
    public sealed class RunawayState : IStickmanState
    {
        private enum Phase
        {
            Fleeing,
            Hidden,
            Found,
            Reconciling,
            SelfReturning,
        }

        private readonly StickmanBlackboard _blackboard;
        private Phase _phase;
        private float _timer;
        private float _totalHiddenTimer; // Hidden+Found를 통틀어 누적 — 자동 복귀 타임아웃의 절대 상한.
        private float _hintPulseTimer;
        private Vector2 _preHideWorldPos;
        private Vector2 _hideSpotWorldPos;

        // self-transition 패턴용 보류 파라미터.
        private bool _pendingReconciled;
        private bool _pendingSelfReturn;
        private string _pendingSelfReturnText;

        public StickmanStateId StateId => StickmanStateId.Runaway;

        public RunawayState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Enter(StateTransitionContext context)
        {
            _timer = 0f;

            if (_pendingReconciled)
            {
                _pendingReconciled = false;
                _phase = Phase.Reconciling;
                RestoreCharacter();
                StickmanEventBus.RaiseRunawayLifecycleChanged(RunawayLifecyclePhase.Reconciled, default);
                // 간식을 못 이기는 척 받아먹는 확정 리액션에서 파생된 화해 대사(20절) — 예고 아님.
                _ = new DialogueIntent(context, id => "흥... 그럼 한 입만이다");
                return;
            }

            if (_pendingSelfReturn)
            {
                _pendingSelfReturn = false;
                string text = _pendingSelfReturnText;
                _pendingSelfReturnText = null;
                _phase = Phase.SelfReturning;
                RestoreCharacter();
                StickmanEventBus.RaiseRunawayLifecycleChanged(RunawayLifecyclePhase.SelfReturned, default);
                _ = new DialogueIntent(context, id => text);
                return;
            }

            // 최초 진입 — Interaction/RunawayDirector.cs가 은신처 월드 좌표를 미리 세팅해둔다.
            _phase = Phase.Fleeing;
            _totalHiddenTimer = 0f;
            _hintPulseTimer = 0f;
            _hideSpotWorldPos = _blackboard.PendingRunawayHideWorldPos;
            _preHideWorldPos = _blackboard.Body != null ? _blackboard.Body.position : Vector2.zero;

            if (_blackboard.Body != null)
            {
                _blackboard.Body.linearVelocity = Vector2.zero;
                _blackboard.Body.bodyType = RigidbodyType2D.Kinematic;
            }

            // 신호 정리 — 이전 사이클에서 소비되지 못한 펄스가 새 사이클로 새는 것을 방지.
            _blackboard.RunawayFoundSignaled = false;
            _blackboard.RunawaySnackOfferedSignaled = false;
            _blackboard.RunawayManualRecallSignaled = false;
            _blackboard.RunawayForceSummonSignaled = false;
            _blackboard.IsCharacterHiddenByRunaway = false; // BUG-P5-M1 대응 — Fleeing 시작 시점엔 아직 숨지 않았으므로 명시적으로 false.

            StickmanEventBus.RaiseRunawayLifecycleChanged(RunawayLifecyclePhase.Fleeing, default);
            // "나 안 해!" — 확정된 RUNAWAY 전이에서 파생된 현재형 선언(예고형 아님, 20절 원문 그대로).
            _ = new DialogueIntent(context, id => "나 안 해!");
        }

        public void Tick(float deltaTime)
        {
            // 긴급 강제소환(24절)은 어느 페이즈에서든 최우선으로 즉시 처리한다("종료"가 아니라 "소환").
            if (_blackboard.RunawayForceSummonSignaled && _phase != Phase.Reconciling && _phase != Phase.SelfReturning)
            {
                _blackboard.RunawayForceSummonSignaled = false;
                TriggerSelfReturn("어... 알았어, 갈게");
                return;
            }

            switch (_phase)
            {
                case Phase.Fleeing: TickFleeing(deltaTime); break;
                case Phase.Hidden: TickHidden(deltaTime); break;
                case Phase.Found: TickFound(deltaTime); break;
                case Phase.Reconciling: TickHold(deltaTime, _blackboard.Config != null ? _blackboard.Config.runawayReconcileHoldSeconds : 1.5f); break;
                case Phase.SelfReturning: TickHold(deltaTime, _blackboard.Config != null ? _blackboard.Config.runawaySelfReturnHoldSeconds : 1.2f); break;
            }
        }

        private void TickFleeing(float deltaTime)
        {
            _timer += deltaTime;
            float duration = _blackboard.Config != null ? _blackboard.Config.runawayFleeDurationSeconds : 1.2f;
            if (_timer < duration) return;

            HideCharacterAtHideSpot();
            _phase = Phase.Hidden;
            _timer = 0f;
            // 아직 화면에 보이는 채로 뛰어가는 동안(Fleeing) 캐릭터를 클릭한 것은 "찾은 것"이 아니다 —
            // Interaction/RunawayDirector.cs는 페이즈를 모르고 CurrentStateId==Runaway로만 판정하므로,
            // 은신 시작 경계에서 묵은 신호를 명시적으로 버려 오판을 막는다.
            _blackboard.RunawayFoundSignaled = false;
            StickmanEventBus.RaiseRunawayLifecycleChanged(RunawayLifecyclePhase.Hidden, HideSpotOsScreenSnapshot());
        }

        private void TickHidden(float deltaTime)
        {
            _totalHiddenTimer += deltaTime;
            _hintPulseTimer += deltaTime;

            float hintInterval = _blackboard.Config != null ? _blackboard.Config.runawayHintPulseIntervalSeconds : 8f;
            if (_hintPulseTimer >= hintInterval)
            {
                _hintPulseTimer = 0f;
                StickmanEventBus.RaiseRunawayHintPulseRequested(HideSpotOsScreenSnapshot());
            }

            if (_blackboard.RunawayFoundSignaled)
            {
                _blackboard.RunawayFoundSignaled = false;
                _phase = Phase.Found;
                _timer = 0f;
                ShowCharacterRevealed();
                StickmanEventBus.RaiseRunawayLifecycleChanged(RunawayLifecyclePhase.Found, HideSpotOsScreenSnapshot());
                return;
            }

            if (CheckAutoReturnOrManualRecall()) return;
        }

        private void TickFound(float deltaTime)
        {
            _totalHiddenTimer += deltaTime;

            if (_blackboard.RunawaySnackOfferedSignaled)
            {
                _blackboard.RunawaySnackOfferedSignaled = false;
                float relief = _blackboard.Config != null ? _blackboard.Config.runawaySnackStressRelief : 0.5f;
                StressGauge.Add(-relief); // "상당량 감소, 완전 리셋은 아님"(20절) — 음수 delta로 부분 감소만.
                _pendingReconciled = true;
                _blackboard.Machine.ChangeState(StickmanStateId.Runaway, isForcedInterrupt: false);
                return;
            }

            CheckAutoReturnOrManualRecall();
        }

        /// <summary>Hidden/Found 공통: 수동 소환 신호 또는 절대 상한 타임아웃 도달 시 자진 복귀를 트리거한다.</summary>
        private bool CheckAutoReturnOrManualRecall()
        {
            if (_blackboard.RunawayManualRecallSignaled)
            {
                _blackboard.RunawayManualRecallSignaled = false;
                TriggerSelfReturn("어... 알았어, 갈게");
                return true;
            }

            float autoReturn = _blackboard.Config != null ? _blackboard.Config.runawayAutoReturnSeconds : 5400f;
            if (_totalHiddenTimer >= autoReturn)
            {
                TriggerSelfReturn("심심해서 왔어...");
                return true;
            }
            return false;
        }

        private void TriggerSelfReturn(string dialogueText)
        {
            _pendingSelfReturn = true;
            _pendingSelfReturnText = dialogueText;
            _blackboard.Machine.ChangeState(StickmanStateId.Runaway, isForcedInterrupt: false);
        }

        private void TickHold(float deltaTime, float holdSeconds)
        {
            _timer += deltaTime;
            if (_timer >= holdSeconds)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
            }
        }

        private void HideCharacterAtHideSpot()
        {
            if (_blackboard.Body != null)
            {
                _blackboard.Body.position = _hideSpotWorldPos;
            }
            _blackboard.SetCharacterVisible?.Invoke(false);
            // BUG-P5-M1 대응 — Hidden 페이즈 진입을 StickmanAgent.Resume()에 알려, 이 구간 중 전체화면
            // Suspend/Resume이 왕복해도 Resume()이 렌더러를 무조건 복원하지 않도록 한다(StickmanBlackboard.
            // IsCharacterHiddenByRunaway 문서 참고).
            _blackboard.IsCharacterHiddenByRunaway = true;
        }

        private void ShowCharacterRevealed()
        {
            _blackboard.SetCharacterVisible?.Invoke(true);
            _blackboard.IsCharacterHiddenByRunaway = false; // BUG-P5-M1 대응 — 발견됨(Found)부터는 다시 일반 가시성 규칙을 따른다.
        }

        private void RestoreCharacter()
        {
            _blackboard.SetCharacterVisible?.Invoke(true);
            _blackboard.IsCharacterHiddenByRunaway = false; // BUG-P5-M1 대응 — Reconciling/SelfReturning 진입 시에도 동일하게 해제.
            if (_blackboard.Body != null)
            {
                _blackboard.Body.position = _preHideWorldPos;
            }
        }

        private Vector2 HideSpotOsScreenSnapshot()
        {
            if (_blackboard.MainCamera == null) return default;
            return ScreenCoordinateConverter.WorldToOsScreen(_blackboard.MainCamera, _hideSpotWorldPos, _blackboard.Config, out _);
        }

        public void Exit()
        {
            // 방어적 복구(DragThrowState.Exit()와 동일한 정신) — 정상 경로는 이미 위에서 처리하지만,
            // 예기치 못한 강제 인터럽트로 이 상태를 빠져나가는 경우에도 캐릭터가 숨은 채/Kinematic으로
            // 얼어붙은 채 남지 않게 한다.
            _blackboard.SetCharacterVisible?.Invoke(true);
            _blackboard.IsCharacterHiddenByRunaway = false; // BUG-P5-M1 대응 — 강제 인터럽트로 빠져나가는 경우에도 플래그가 새지 않게 방어적으로 해제.
            if (_blackboard.Body != null && _blackboard.Body.bodyType == RigidbodyType2D.Kinematic)
            {
                _blackboard.Body.bodyType = RigidbodyType2D.Dynamic;
            }
        }
    }
}
