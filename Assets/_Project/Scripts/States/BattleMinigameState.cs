using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상호작용 상태: 격파 미니게임(docs/UX_FLOW.md 10절) — 기 모으기 게이지(1.5~2초) →
    /// 스위트스팟(70~85%) → 클릭 판정 → 성공/실패(재도전 최대 3회) → 5초 무입력 타임아웃.
    ///
    /// 진입: Interaction/BattleMinigameDirector가 (유휴 저확률 추첨 또는 트레이 메뉴 수동 트리거로)
    /// 부분적 클릭관통 해제 + SpectacleEventLock을 확보했을 때만 Machine.ChangeState(BattleMinigame)를
    /// 호출한다. 클릭 판정 대상은 캐릭터 자신의 히트박스를 재사용한다(UX 10절 "캐릭터/오브젝트의 화면
    /// 히트박스 영역" 중 "캐릭터" 쪽 — 실제 소환 오브젝트의 별도 콜라이더/스프라이트는 아직 렌더링
    /// 레이어가 없어 이번 라운드에 구현하지 않는다, WanderAmbientMotionRequested류 패턴과 동일).
    ///
    /// 클릭 입력 경로: Interaction/BattleMinigameDirector가 StickmanClickHitbox.MouseDown을 구독해
    /// blackboard.BattleClickSignaled를 세팅하고, 이 상태의 Tick()이 매 프레임 그 신호를 소비한다
    /// (DragThrowState의 DragReleaseSignaled와 동일한 컨벤션).
    ///
    /// [self-transition, Architect 지시 2026-08-27 — Tasklist.md 교차 레이어 로그] "릴리즈 순간"
    /// (클릭으로 성공/실패가 갈리는 그 프레임)의 대사(UX_FLOW.md 31-2 표 #5)는 DialogueIntent가 오직
    /// Enter() 안에서만 만들어질 수 있다는 원칙(31-1/9절-1)에 예외를 두지 않는다. 대신 RagdollState가
    /// 반복 피격 때 쓰는 것과 동일한 패턴을 재사용한다: 판정에 필요한 파라미터(chargeRatio)를 재전이
    /// 직전에 필드에 기록해두고, 같은 상태로 자기 자신을 다시 ChangeState()해 Exit()→Enter()를
    /// 재실행시킨다 — "판정 순간"과 "전이 확정 순간"이 코드 구조상 같은 프레임의 같은 사건이 된다.
    /// TickCharging()이 판정을 직접 내리지 않고 TriggerResolution()으로 자기-전이만 시키면,
    /// 실제 판정(성공/실패/재도전/소진)과 대사 파생은 전부 Enter()의 ResolveOutcome()이 담당한다.
    /// </summary>
    public sealed class BattleMinigameState : IStickmanState, IHasDialogueParams
    {
        private enum Phase { Charging, Resolving }

        private readonly StickmanBlackboard _blackboard;

        private Phase _phase;
        private float _chargeElapsed;
        private float _chargeDuration;
        private int _retryCount;
        private float _noInputTimer; // 이벤트 시작 후 무클릭 누적 시간(10절 "5초 이상 클릭 입력이 전혀 없으면 자동 취소")
        private float _resolveTimer;
        private bool _terminal; // 이번 Resolving이 끝나면 종료(Idle 복귀)인지, 재도전인지.

        // self-transition 패턴용 보류 파라미터 — TriggerResolution()이 기록하고 다음 Enter()가 소비한다.
        private bool _pendingResolution;
        private float _pendingChargeRatio;

        /// <summary>
        /// UX_FLOW.md 31-2 표 #5 대응 파라미터. chargeRatio는 릴리즈(클릭) 확정 순간의 게이지 비율
        /// (0~1) 스냅샷이며, 성공/실패 판정(스위트스팟 70~85% 기준)과는 별개 축이다 — 이 대사는 "게이지가
        /// 얼마나 꽉 찼는지"에 대한 감탄사라 표에 명시된 임계값(0.9)을 그대로 쓴다.
        /// </summary>
        public sealed class BattleDialogueParams
        {
            public float ChargeRatio;
        }

        private readonly BattleDialogueParams _dialogueParams = new BattleDialogueParams();

        public object DialogueParams => _dialogueParams;

        public StickmanStateId StateId => StickmanStateId.BattleMinigame;

        public BattleMinigameState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Enter(StateTransitionContext context)
        {
            if (_pendingResolution)
            {
                _pendingResolution = false;
                ResolveOutcome(_pendingChargeRatio, context);
                return;
            }

            // 최초 진입(Director가 트리거) — 새 대결 사이클을 시작한다.
            _retryCount = 0;
            _noInputTimer = 0f;
            BeginCharge();

            // 10절 "1) 캐릭터 준비 자세 → 대사 '좋아, 간다'" — 상태 확정 이후에만 파생되는 고정 텍스트.
            _ = new DialogueIntent(context, id => "좋아, 간다");
        }

        private void BeginCharge()
        {
            _phase = Phase.Charging;
            _chargeElapsed = 0f;

            float min = _blackboard.Config != null ? _blackboard.Config.battleChargeDurationMin : 1.5f;
            float max = _blackboard.Config != null ? _blackboard.Config.battleChargeDurationMax : 2.0f;
            _chargeDuration = max > min ? Random.Range(min, max) : min;

            _blackboard.BattleClickSignaled = false;
        }

        public void Tick(float deltaTime)
        {
            _noInputTimer += deltaTime;
            float inputTimeout = _blackboard.Config != null ? _blackboard.Config.battleInputTimeoutSeconds : 5f;
            if (_noInputTimer >= inputTimeout)
            {
                // "유저가 다른 작업으로 이탈"로 간주 — 부분적 클릭관통 해제는 Interaction/
                // BattleMinigameDirector가 이 상태의 Exit(=StateTransitioned, To!=BattleMinigame)을
                // 구독해 원복한다.
                StickmanEventBus.RaiseBattleMinigamePhaseChanged(BattleMinigamePhase.Exhausted);
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            if (_phase == Phase.Charging) TickCharging(deltaTime);
            else TickResolving(deltaTime);
        }

        private void TickCharging(float deltaTime)
        {
            _chargeElapsed += deltaTime;
            float ratio = _chargeDuration > 0f ? Mathf.Clamp01(_chargeElapsed / _chargeDuration) : 1f;

            if (_blackboard.BattleClickSignaled)
            {
                _blackboard.BattleClickSignaled = false;
                _noInputTimer = 0f; // 맞았든 틀렸든 "클릭 입력이 있었다"는 사실 자체로 무입력 타이머 리셋.
                TriggerResolution(ratio);
                return;
            }

            if (ratio >= 1f)
            {
                // 끝까지 클릭이 전혀 없었음 -> 미스(실패)로 취급(ratio=1.0 스냅샷, 무한정 같은 게이지에 머무르지 않게 함).
                TriggerResolution(1f);
            }
        }

        /// <summary>
        /// "릴리즈 순간"의 실제 판정(성공/실패/재도전/소진)과 대사 파생은 여기서 직접 하지 않는다 —
        /// chargeRatio 스냅샷만 기록해두고 같은 상태로 자기 자신을 재전이시켜, Enter()의
        /// ResolveOutcome()이 그 값을 읽어 처리하게 한다(위 클래스 주석의 self-transition 패턴).
        /// </summary>
        private void TriggerResolution(float chargeRatio)
        {
            _pendingChargeRatio = chargeRatio;
            _pendingResolution = true;
            _blackboard.Machine.ChangeState(StickmanStateId.BattleMinigame, isForcedInterrupt: false);
        }

        /// <summary>
        /// self-transition으로 재실행된 Enter() 안에서만 호출된다. 성공/실패/재도전/소진 판정과
        /// StickmanEventBus 통지, "릴리즈 순간" DialogueIntent 생성을 모두 이 시점(=전이 확정 시점)에서
        /// 함께 처리해 판정과 대사 파생이 항상 같은 프레임의 같은 사건이 되게 한다.
        /// </summary>
        private void ResolveOutcome(float chargeRatio, StateTransitionContext context)
        {
            _dialogueParams.ChargeRatio = chargeRatio;

            float sweetStart = _blackboard.Config != null ? _blackboard.Config.battleSweetSpotStart : 0.70f;
            float sweetEnd = _blackboard.Config != null ? _blackboard.Config.battleSweetSpotEnd : 0.85f;
            bool success = chargeRatio >= sweetStart && chargeRatio <= sweetEnd;

            _phase = Phase.Resolving;
            _resolveTimer = 0f;

            if (success)
            {
                _terminal = true;
                StickmanEventBus.RaiseBattleMinigamePhaseChanged(BattleMinigamePhase.Success);
            }
            else
            {
                _retryCount++;
                int maxRetries = _blackboard.Config != null ? _blackboard.Config.battleMaxRetries : 3;
                if (_retryCount > maxRetries)
                {
                    _terminal = true;
                    StickmanEventBus.RaiseBattleMinigamePhaseChanged(BattleMinigamePhase.Exhausted);
                }
                else
                {
                    _terminal = false;
                    StickmanEventBus.RaiseBattleMinigamePhaseChanged(BattleMinigamePhase.Fail);
                }
            }

            // UX_FLOW.md 31-2 표 #5 — chargeRatio 스냅샷만으로 파생(성공/실패 판정과는 별개 축, 임계값
            // 0.9는 표 원문 그대로).
            _ = new DialogueIntent(context, (id, dialogueParams) =>
            {
                var p = dialogueParams as BattleDialogueParams;
                float ratio = p != null ? p.ChargeRatio : 0f;
                return ratio >= 0.9f ? "필살기다!" : "어... 어라?";
            });
        }

        private void TickResolving(float deltaTime)
        {
            _resolveTimer += deltaTime;
            float delay = _terminal
                ? (_blackboard.Config != null ? _blackboard.Config.battleSuccessResolveDelaySeconds : 1.0f)
                : (_blackboard.Config != null ? _blackboard.Config.battleFailRetryDelaySeconds : 1.5f);
            if (_resolveTimer < delay) return;

            if (_terminal) _blackboard.Machine.ChangeState(StickmanStateId.Idle);
            else BeginCharge();
        }

        public void Exit() { }
    }
}
