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
    /// [알려진 설계 한계, 정직하게 문서화] UX_FLOW.md 31-2 표 #5는 "chargeRatio(릴리즈 확정 순간 값)"
    /// 파라미터로 릴리즈 시점의 대사("필살기다!"/"어... 어라?")를 파생시키는 예시를 제시하지만, 그
    /// 시점(클릭 순간)은 이 상태의 Enter()가 아니라 Tick() 도중이다 — DialogueIntent는 오직 Enter()
    /// 안에서만 생성 가능하다는 원칙(31-1/9절-1)과 정면으로 충돌하므로, "같은 상태 안에서 여러 차례
    /// 반복되는 판정 각각에 스냅샷 대사를 붙이는" 일반해가 아직 없다(이 표 자체도 "지금 구현 대상은
    /// 아님"이라 명시했었다). Enter()의 고정 대사("좋아, 간다")만 구현하고, 성공/실패/소진 각 결과는
    /// StickmanEventBus.BattleMinigamePhaseChanged 이벤트로만 알린다 — 실제 리액션 텍스트/애니메이션은
    /// 이 설계 질문이 해소된 뒤 Phase 2+ 렌더링 레이어와 함께 다음 라운드에 추가하길 권고(Architect 조율 요청).
    /// </summary>
    public sealed class BattleMinigameState : IStickmanState
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

        public StickmanStateId StateId => StickmanStateId.BattleMinigame;

        public BattleMinigameState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Enter(StateTransitionContext context)
        {
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
                // BattleMinigameDirector가 이 상태의 Exit(=StateTransitioned)을 구독해 원복한다.
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
                ResolveClick(ratio);
                return;
            }

            if (ratio >= 1f)
            {
                // 끝까지 클릭이 전혀 없었음 -> 미스(실패)로 취급(무한정 같은 게이지에 머무르지 않게 함).
                ResolveOutcome(success: false);
            }
        }

        private void ResolveClick(float ratio)
        {
            float sweetStart = _blackboard.Config != null ? _blackboard.Config.battleSweetSpotStart : 0.70f;
            float sweetEnd = _blackboard.Config != null ? _blackboard.Config.battleSweetSpotEnd : 0.85f;
            bool success = ratio >= sweetStart && ratio <= sweetEnd;
            ResolveOutcome(success);
        }

        private void ResolveOutcome(bool success)
        {
            _phase = Phase.Resolving;
            _resolveTimer = 0f;

            if (success)
            {
                _terminal = true;
                StickmanEventBus.RaiseBattleMinigamePhaseChanged(BattleMinigamePhase.Success);
                return;
            }

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
