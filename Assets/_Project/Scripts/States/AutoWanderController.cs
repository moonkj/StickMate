using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// IMovementIntentSource의 정식 구현체 — docs/UX_FLOW.md 26절(자율 배회 AI 행동 설계, UX Designer
    /// 작성, BUG-P1-B2 Blocker 긴급 대응)의 수치를 그대로 반영한다. 리더 지시로 UX 스펙 도착 전에는
    /// "제자리에서 좌우 랜덤 방향을 2~5초마다 바꾸는" 최소 임시 구현으로 시작했으나, 26절 스펙이 도착해
    /// 이 클래스가 최종(정식) 구현으로 교체되었다 — Tasklist.md 참고.
    ///
    /// FootholdPoller/GroundSensor와 같은 컨벤션을 따라 MonoBehaviour가 아닌 순수 C# 클래스로 작성한다.
    /// StickmanAgent.Update()가 매 프레임 Tick(deltaTime)을 호출해 내부 타이머를 갱신하고, 그 결과를
    /// IMovementIntentSource로 노출한다.
    ///
    /// 개체별 독립 RNG(26-3, Phase 5 세포분열 대비 "모든 개체가 같은 시드로 동기화되어 보이면 안 됨"):
    /// 호출자(StickmanAgent)가 인스턴스마다 서로 다른 System.Random을 주입한다.
    /// </summary>
    public sealed class AutoWanderController : IMovementIntentSource
    {
        /// <summary>StickmanAgent.TryGetCursorPosition과 시그니처가 동일한 델리게이트(out 매개변수라
        /// System.Func로 표현할 수 없어 별도 선언). 26-4 커서 근접 반응 훅 예약용.</summary>
        public delegate bool CursorPositionQuery(out Vector2 osScreenPosition);

        /// <summary>
        /// 26-4(커서 근접 반응) 훅 — UX Designer 판단으로 Phase 2로 연기 확정(지금은 렌더링 레이어가
        /// 없어 체감 가치가 0). 지금은 아무도 이 값을 읽지 않는다 — StickmanAgent가 생성 직후 이 자리에
        /// TryGetCursorPosition을 미리 연결해두기만 하고, Phase 2에서 실제 반응 로직(150px 반경, Walk
        /// 중일 때만, Walk 타이머 일시정지 + 0.4~0.8초 MoveInputX=0 고정)만 채우면 되게 한다.
        /// </summary>
        public CursorPositionQuery CursorProvider { get; set; }

        private enum Phase
        {
            Resting, // Idle 페이즈 — moveInputDeadzone 판정으로 자동으로 IdleState를 유발(26-1)
            Moving,  // Walk 페이즈 — 마찬가지로 WalkState를 자동 유발
        }

        // 26-2: 발판 경계 도달 여부를 "이미 그 발판의 진짜 끝(화면 자체의 끝)인지"와 비교할 때 쓰는 허용 오차.
        private const float ScreenEdgeEpsilon = 0.01f;

        private readonly StickmanBlackboard _blackboard;
        private readonly StickConfig _config;
        private readonly System.Random _rng;

        private Phase _phase;

        // Resting(Idle) 페이즈 상태 —————————————————————————————————————
        private float _restTimer;
        private float _restDuration;
        private bool _lookAroundFiredThisRest;
        private float _lookAroundDelay;

        // "Idle 연장"이 연속으로 선택된 횟수(26-3 앉기/하품 트리거 조건).
        private int _consecutiveIdleExtensions;

        // Moving(Walk) 페이즈 상태 —————————————————————————————————————
        private float _moveTimer;
        private float _moveDuration;
        private float _turnCheckTimer;
        private bool _spontaneousTurnUsedThisPhase;
        private int _direction; // -1(왼쪽) 또는 1(오른쪽)

        // 경계 정지(90% 분기) 서브 상태.
        private bool _isEdgePaused;
        private float _edgePauseTimer;
        private float _edgePauseDuration;

        private float _moveInputX;
        private bool _jumpRequestedThisTick;

        public float MoveInputX => _moveInputX;
        public bool JumpRequested => _jumpRequestedThisTick;

        public AutoWanderController(StickmanBlackboard blackboard, StickConfig config, System.Random rng)
        {
            _blackboard = blackboard;
            _config = config;
            _rng = rng ?? new System.Random();
            EnterResting();
        }

        /// <summary>매 프레임 호출(StickmanAgent.Update()가 배선). 26-7: JumpRequested는 이 호출마다
        /// 새로 계산되어 최대 1프레임만 true를 유지한다(펄스 계약).</summary>
        public void Tick(float deltaTime)
        {
            _jumpRequestedThisTick = false;

            if (_phase == Phase.Resting) TickResting(deltaTime);
            else TickMoving(deltaTime);
        }

        // ==================== Resting (26-1, 26-3) ====================

        private void EnterResting()
        {
            _phase = Phase.Resting;
            _restTimer = 0f;
            _restDuration = Jitter(RandomRange(Cfg(c => c.wanderIdleDurationMin, 2f), Cfg(c => c.wanderIdleDurationMax, 6f)));
            _lookAroundFiredThisRest = false;
            _lookAroundDelay = RandomRange(Cfg(c => c.wanderLookAroundDelayMin, 1f), Cfg(c => c.wanderLookAroundDelayMax, 2.5f));
            _moveInputX = 0f;
        }

        private void TickResting(float deltaTime)
        {
            _restTimer += deltaTime;
            _moveInputX = 0f;

            // 26-3: 두리번거리기 — Idle 진입 후 지연시간 경과 시 1회만 발동, Idle이 그 전에 끝나면 자연히 취소됨.
            if (!_lookAroundFiredThisRest && _restTimer >= _lookAroundDelay && _restTimer < _restDuration)
            {
                _lookAroundFiredThisRest = true;
                StickmanEventBus.RaiseWanderAmbientMotionRequested(WanderAmbientMotion.LookAround);
            }

            if (_restTimer < _restDuration) return;
            ResolvePostIdleBranch();
        }

        /// <summary>26-1: Idle 종료 후 Walk 75% / Idle 연장 20% / 제자리 점프 5%(가중치 랜덤).</summary>
        private void ResolvePostIdleBranch()
        {
            float walkChance = Cfg(c => c.wanderPostIdleWalkChance, 0.75f);
            float jumpChance = Cfg(c => c.wanderPostIdleJumpChance, 0.05f);

            double roll = _rng.NextDouble();
            if (roll < walkChance)
            {
                _consecutiveIdleExtensions = 0;
                EnterMoving();
            }
            else if (roll < walkChance + jumpChance)
            {
                // 제자리 점프: 방향 없이(MoveInputX 유지 0) 점프 펄스만 발동. 실제 StickmanStateMachine이
                // Idle->Jump->Fall->Idle을 물리적으로 처리하는 동안, 이 컨트롤러는 독립적으로 새 Idle
                // 구간을 이어간다(States는 IntentSource가 무엇을 하든 값만 소비하므로 되먹임이 필요 없음).
                _consecutiveIdleExtensions = 0;
                _jumpRequestedThisTick = true;
                EnterResting();
            }
            else
            {
                // Idle 연장 — 26-3: 연속 3회 이상이면 15% 확률로 앉기/하품 트리거.
                _consecutiveIdleExtensions++;
                if (_consecutiveIdleExtensions >= 3)
                {
                    float sitChance = Cfg(c => c.wanderRestExtendSitChance, 0.15f);
                    if (_rng.NextDouble() < sitChance)
                    {
                        StickmanEventBus.RaiseWanderAmbientMotionRequested(WanderAmbientMotion.SitAndYawn);
                    }
                }
                EnterResting();
            }
        }

        // ==================== Moving (26-1, 26-2) ====================

        private void EnterMoving()
        {
            _phase = Phase.Moving;
            _moveTimer = 0f;
            _moveDuration = Jitter(RandomRange(Cfg(c => c.wanderWalkDurationMin, 1.5f), Cfg(c => c.wanderWalkDurationMax, 4f)));
            _turnCheckTimer = 0f;
            _spontaneousTurnUsedThisPhase = false;
            _isEdgePaused = false;
            _direction = PickDirectionAvoidingEdge();
            _moveInputX = _direction;
        }

        private void TickMoving(float deltaTime)
        {
            if (_isEdgePaused)
            {
                TickEdgePause(deltaTime);
                return;
            }

            _moveTimer += deltaTime;
            _moveInputX = _direction;

            // 26-1: 즉흥 방향전환 — 0.5초마다 8% 확률, 같은 Walk 페이즈 내 최대 1회.
            _turnCheckTimer += deltaTime;
            float turnCheckInterval = Cfg(c => c.wanderTurnCheckInterval, 0.5f);
            if (!_spontaneousTurnUsedThisPhase && _turnCheckTimer >= turnCheckInterval)
            {
                _turnCheckTimer -= turnCheckInterval;
                float turnChance = Cfg(c => c.wanderSpontaneousTurnChance, 0.08f);
                if (_rng.NextDouble() < turnChance)
                {
                    _spontaneousTurnUsedThisPhase = true;
                    _direction = -_direction;
                    _moveInputX = _direction;
                }
            }

            // 26-2: 발판 경계 판정. 접지 중일 때만 의미가 있다 — 공중(점프/낙하 중)에는 판정하지 않는다.
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (info.Grounded && IsNearFootholdEdge(info, _direction, out bool isTrueScreenEdge))
            {
                // 화면 자체의 물리적 끝(더 이상 발판이 없음)에서는 점프 확률을 항상 0으로 강제 —
                // 그렇지 않으면 화면 밖으로 뛰어내리는 결과가 된다(26-2 표 마지막 행).
                float jumpChance = isTrueScreenEdge ? 0f : Cfg(c => c.wanderEdgeJumpAttemptChance, 0.10f);
                if (jumpChance > 0f && _rng.NextDouble() < jumpChance)
                {
                    // 10%: 정지 대신 진행 방향을 유지한 채 점프 펄스만 발동("파쿠르 예고" 이스터에그).
                    // 착지할 발판이 없으면 Fall로 이어지므로 BUG-P1-B1의 화면 하단 폴백 발판이 반드시
                    // 선행되어야 "허공에 뜬 채 사라짐"으로 보이지 않는다(UX_FLOW.md 26-7 순서 의존성).
                    _jumpRequestedThisTick = true;
                    _moveInputX = _direction;
                }
                else
                {
                    // 90%: 즉시 정지 -> 랜덤 대기 -> 반대 방향 전환.
                    BeginEdgePause();
                }
                return;
            }

            if (_moveTimer >= _moveDuration)
            {
                EnterResting();
            }
        }

        private void BeginEdgePause()
        {
            _isEdgePaused = true;
            float min = Cfg(c => c.wanderEdgeTurnPauseMin, 0.3f);
            float max = Cfg(c => c.wanderEdgeTurnPauseMax, 0.8f);
            _edgePauseDuration = Jitter(RandomRange(min, max));
            _edgePauseTimer = 0f;
            _moveInputX = 0f;
        }

        private void TickEdgePause(float deltaTime)
        {
            _moveInputX = 0f;
            _edgePauseTimer += deltaTime;
            if (_edgePauseTimer < _edgePauseDuration) return;

            _isEdgePaused = false;
            _direction = -_direction;
            _moveInputX = _direction;

            // 경계 바운스는 26-1 통계상 "새 Walk 페이즈"로 집계하지 않는다(_moveTimer는 정지 중 멈춰
            // 있었다) — 남아 있던 Walk 지속시간을 그대로 이어간다. 이미 다 써버렸다면 여기서 Idle로.
            if (_moveTimer >= _moveDuration) EnterResting();
        }

        // ==================== 경계 판정 유틸 (26-7) ====================

        /// <summary>
        /// 진행 방향 앞쪽으로 "지금 딛고 있는 발판"의 잔여 길이가 wanderEdgeStopDistance 이하인지 판정한다.
        /// isTrueScreenEdge: 그 발판의 경계가 전체 발판 통합 경계(화면 자체의 끝)와 일치하는지 —
        /// 옆에 다른 발판이 더 있다면 false(그 발판만의 끝일 뿐 화면의 끝은 아님).
        /// </summary>
        private bool IsNearFootholdEdge(GroundSensor.GroundInfo info, int direction, out bool isTrueScreenEdge)
        {
            float stopDistance = Cfg(c => c.wanderEdgeStopDistance, 0.3f);
            float characterX = _blackboard.Body != null ? _blackboard.Body.position.x : 0f;

            float remaining;
            if (direction > 0)
            {
                remaining = info.CurrentFootholdRightWorldX - characterX;
                isTrueScreenEdge = Mathf.Abs(info.CurrentFootholdRightWorldX - info.ScreenRightWorldX) <= ScreenEdgeEpsilon;
            }
            else
            {
                remaining = characterX - info.CurrentFootholdLeftWorldX;
                isTrueScreenEdge = Mathf.Abs(info.CurrentFootholdLeftWorldX - info.ScreenLeftWorldX) <= ScreenEdgeEpsilon;
            }

            return remaining <= stopDistance;
        }

        /// <summary>26-1: 최초(또는 매 Walk 페이즈 시작 시) 진행 방향은 좌우 50:50 랜덤. 단, 지금 위치가
        /// 이미 화면 경계에 붙어 있으면 26-2 로직을 재사용해 안쪽 방향으로 강제한다.</summary>
        private int PickDirectionAvoidingEdge()
        {
            int dir = _rng.NextDouble() < 0.5 ? -1 : 1;

            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (!info.Grounded) return dir;

            bool huggingRightScreenEdge = IsNearFootholdEdge(info, 1, out bool rightIsScreenEdge) && rightIsScreenEdge;
            bool huggingLeftScreenEdge = IsNearFootholdEdge(info, -1, out bool leftIsScreenEdge) && leftIsScreenEdge;
            if (huggingRightScreenEdge && !huggingLeftScreenEdge) return -1;
            if (huggingLeftScreenEdge && !huggingRightScreenEdge) return 1;
            return dir;
        }

        // ==================== 공용 난수/지터/설정 유틸 ====================

        private float RandomRange(float min, float max)
        {
            if (max <= min) return min;
            return min + (float)_rng.NextDouble() * (max - min);
        }

        /// <summary>26-3 지터 원칙: Idle/Walk 지속시간·경계 정지 대기시간에 ±wanderDurationJitterRatio를 추가로 곱한다.</summary>
        private float Jitter(float baseValue)
        {
            float ratio = Cfg(c => c.wanderDurationJitterRatio, 0.175f);
            float factor = 1f + (float)((_rng.NextDouble() * 2.0 - 1.0) * ratio);
            return Mathf.Max(0.01f, baseValue * factor);
        }

        private float Cfg(System.Func<StickConfig, float> selector, float fallback)
        {
            return _config != null ? selector(_config) : fallback;
        }
    }
}
