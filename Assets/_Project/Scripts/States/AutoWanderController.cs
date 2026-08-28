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

        // 경계 행동 추첨(매달려 내려가기 / 뛰어내리기 / 되올라가기)은 한 Walk 페이즈(정확히는 "한
        // 방향으로 걷는 한 구간")당 **통틀어 최대 1회**만 한다. 매 프레임 다시 뽑으면 경계에 머무는 몇
        // 프레임 동안 확률이 사실상 1이 되어 "일부만 하게" 하려는 설정 자체가 무의미해진다. 세 갈래를
        // 각각 따로 뽑지 않고 하나로 묶은 이유도 같다 — 한 경계에서 주사위를 세 번 굴리면 "아무 것도
        // 안 할 확률"이 설정값의 곱으로 떨어져 캐릭터가 경계마다 뭔가를 하게 된다.
        private bool _edgeActionRolledThisLeg;

        // ★ 뛰어내리기 "확약" 서브 상태(2026-08-29). 추첨에 당첨된 순간 바로 발을 떼지 않고, 모서리
        // 코앞(hopDownEdgeCommitDistance)까지 계속 걸어간 뒤에 펄스를 낸다. 경계 판정 거리(0.3유닛)에서
        // 곧장 Fall로 보내면 아직 발판 한복판인데 낙하가 시작돼 "바닥을 뚫고 내려가는" 것처럼 보인다
        // (제자리 재착지 자체는 2026-08-29 2차 수정의 drop-through가 막는다 — States/WalkState.cs의
        // "발을 뗍니다" 블록 주석 참고. 그래서 이 확약이 지금 맡는 역할은 연출뿐이다).
        private bool _hopDownCommitted;

        private float _moveInputX;
        private bool _jumpRequestedThisTick;
        private bool _ledgeHangRequestedThisTick;
        private bool _hopDownRequestedThisTick;
        private bool _stepUpRequestedThisTick;

        public float MoveInputX => _moveInputX;
        public bool JumpRequested => _jumpRequestedThisTick;
        public bool LedgeHangRequested => _ledgeHangRequestedThisTick;
        public bool HopDownRequested => _hopDownRequestedThisTick;
        public bool StepUpRequested => _stepUpRequestedThisTick;

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
            _ledgeHangRequestedThisTick = false;
            _hopDownRequestedThisTick = false;
            _stepUpRequestedThisTick = false;

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

        /// <summary>
        /// 26-1: Idle 종료 후 Walk / Idle 연장 / 제자리 점프(가중치 랜덤). 세 갈래의 확률은 전부
        /// StickConfig가 정하며, 이 메서드에는 하드코딩된 확률이 하나도 없다.
        ///
        /// ★ 2026-08-28 사용자 피드백 "이상하게 점프도 하고" 대응 — StickConfig.wanderPostIdleJumpChance의
        /// 기본값이 0.05 -> 0으로 내려갔다. 즉 기본 상태에서 이 분기는 절대 선택되지 않고, 남은 확률은
        /// 전부 "Idle 연장"으로 흡수된다(Walk 75% / Idle 연장 25%). 분기 자체를 지우지 않은 이유는
        /// UX 26-1이 정식으로 설계한 행동이기 때문이다 — 설정값 하나만 되돌리면 그대로 되살아난다.
        /// </summary>
        private void ResolvePostIdleBranch()
        {
            float walkChance = Cfg(c => c.wanderPostIdleWalkChance, 0.75f);
            float jumpChance = Cfg(c => c.wanderPostIdleJumpChance, 0f);

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
            _edgeActionRolledThisLeg = false;
            _hopDownCommitted = false;
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
            if (!info.Grounded)
            {
                // 공중으로 나갔다면 뛰어내리기 확약은 이미 소임을 다했다(펄스로 발을 뗐거나, 그냥 걸어서
                // 모서리를 넘어갔거나). 확약이 남아 있으면 다음 경계에서 걷기만 하고 서지 않게 되므로
                // 반드시 여기서 해제한다.
                _hopDownCommitted = false;
                // 발이 땅에서 떨어진 순간부터는 "새 걷기 구간"으로 본다 — 추첨을 한 번으로 제한하는
                // 이유는 "같은 모서리에 머무는 동안 매 프레임 재추첨하지 않기" 하나뿐인데, 공중으로
                // 나갔다는 것은 그 모서리를 이미 떠났다는 뜻이다. 이 리셋이 없으면 뛰어내린 직후 착지한
                // 발판에서는 추첨 자체를 못 해, 되올라가려면 반대편 경계까지 한 번 왕복해야 한다.
                _edgeActionRolledThisLeg = false;
            }
            if (info.Grounded && IsNearFootholdEdge(info, _direction, out bool isTrueScreenEdge, out float remainingToEdge))
            {
                // ★ 경계 행동 추첨 — 한 걷기 구간당 1회(위 _edgeActionRolledThisLeg 주석 참고). 아래 세
                // 갈래를 **낙차/높이로 먼저 가른 뒤** 그 갈래의 확률로만 추첨한다. 공통 전제 두 가지:
                //   (1) 화면 자체의 끝이 아니다(끝에서 바깥으로 나가면 몸이 화면 밖으로 나간다),
                //   (2) 실제로 갈 곳이 있다(내려앉을 발판 / 올라설 턱이 실존할 때만 추첨한다).
                // 추첨에 떨어지거나 조건이 안 맞으면 아래 기존 분기(점프 시도 / 정지 후 반대 방향)로
                // 그대로 흘러간다 — 즉 세 확률을 전부 0으로 두면 예전 거동과 100% 동일하다.
                if (!_edgeActionRolledThisLeg && !isTrueScreenEdge)
                {
                    _edgeActionRolledThisLeg = true;
                    if (TryRollEdgeAction(info)) return;
                }

                // ★ 뛰어내리기 확약 중이면 정지/반전하지 않고 모서리 코앞까지 계속 걸어간 뒤 발을 뗀다
                // (2026-08-29). 이 블록이 아래 기존 분기보다 먼저 와야 한다 — 그렇지 않으면 확약해두고도
                // 90% 분기(정지 후 반대 방향)에 먼저 걸려 그 자리에서 돌아서 버린다.
                if (_hopDownCommitted)
                {
                    _moveInputX = _direction;
                    float commitDistance = Cfg(c => c.hopDownEdgeCommitDistance, 0.12f);
                    if (remainingToEdge <= commitDistance)
                    {
                        // 실제 상태 전이(수평 속도 부여 + Fall)는 WalkState가 한다 — 이 클래스는 "의도"만
                        // 만든다는 계약 유지.
                        _hopDownCommitted = false;
                        _hopDownRequestedThisTick = true;
                    }
                    return;
                }

                // 화면 자체의 물리적 끝(더 이상 발판이 없음)에서는 점프 확률을 항상 0으로 강제 —
                // 그렇지 않으면 화면 밖으로 뛰어내리는 결과가 된다(26-2 표 마지막 행).
                // ★ 2026-08-28: StickConfig.wanderEdgeJumpAttemptChance의 기본값도 0.10 -> 0이 되어,
                // 화면 끝이 아닌 발판 경계에서도 기본적으로는 점프하지 않고 항상 아래 90% 분기(정지 후
                // 반대 방향 전환)로만 간다(사용자 피드백 "이상하게 점프도 하고"). 배회(걷기/서기) 자체는
                // 그대로다 — 꺼진 것은 점프뿐이다.
                float jumpChance = isTrueScreenEdge ? 0f : Cfg(c => c.wanderEdgeJumpAttemptChance, 0f);
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
            // 반대 방향으로 도는 순간부터는 "새 걷기 구간"이다 — 반대쪽 경계에서 다시 추첨한다.
            _edgeActionRolledThisLeg = false;
            _hopDownCommitted = false;

            // 경계 바운스는 26-1 통계상 "새 Walk 페이즈"로 집계하지 않는다(_moveTimer는 정지 중 멈춰
            // 있었다) — 남아 있던 Walk 지속시간을 그대로 이어간다. 이미 다 써버렸다면 여기서 Idle로.
            if (_moveTimer >= _moveDuration) EnterResting();
        }

        // ==================== 경계 판정 유틸 (26-7) ====================

        /// <summary>
        /// 진행 방향 앞쪽으로 "지금 딛고 있는 발판"의 잔여 길이가 wanderEdgeStopDistance 이하인지 판정한다.
        /// isTrueScreenEdge: 그 발판의 경계가 전체 발판 통합 경계(화면 자체의 끝)와 일치하는지 —
        /// 옆에 다른 발판이 더 있다면 false(그 발판만의 끝일 뿐 화면의 끝은 아님).
        ///
        /// ★ 2026-08-29 수정 — "화면 물리적 끝에서 제자리 걷기"(러닝머신) 대응.
        /// isTrueScreenEdge인 쪽에서는 **원시 발판 경계가 아니라 캐릭터가 실제로 갈 수 있는 한계**
        /// (StickmanBlackboard.TryGetWalkableScreenBoundsWorld — 화면 하드 클램프가 붙잡아 세우는 바로
        /// 그 X)까지의 거리로 잔여 길이를 잰다. 예전에는 원시 경계(=화면 끝)에서 쟀기 때문에, 클램프가
        /// 화면 끝에서 약 58pt 안쪽에 캐릭터를 세워두는 동안 이 판정에 필요한 거리(0.3유닛 ≈ 24pt)가
        /// 영영 성립하지 않아 캐릭터가 걷기 애니메이션만 돌린 채 클램프를 계속 밀었다(Walk 지속시간이
        /// 만료돼야 겨우 풀렸다). 두 값이 다시 어긋나지 않도록 클램프와 이 조회는 블랙보드 안의
        /// 같은 계산식 하나에서 파생된다.
        ///
        /// 화면 끝이 아닌(isTrueScreenEdge == false) 평범한 발판 경계는 예전 그대로 원시 경계로
        /// 잰다 — 그 경계는 캐릭터가 실제로 딛고 넘어설 수 있는 지점이라 클램프와 무관하다.
        /// 안전 방향으로만 좁히도록 Min/Max를 쓰므로, 발판 경계가 클램프보다 안쪽이면 아무 것도 바뀌지 않는다.
        /// </summary>
        private bool IsNearFootholdEdge(GroundSensor.GroundInfo info, int direction, out bool isTrueScreenEdge,
            out float remainingToEdge)
        {
            float stopDistance = Cfg(c => c.wanderEdgeStopDistance, 0.3f);
            float characterX = _blackboard.Body != null ? _blackboard.Body.position.x : 0f;
            bool hasWalkable = _blackboard.TryGetWalkableScreenBoundsWorld(out float walkableLeftX, out float walkableRightX);

            if (direction > 0)
            {
                float boundaryX = info.CurrentFootholdRightWorldX;
                isTrueScreenEdge = Mathf.Abs(boundaryX - info.ScreenRightWorldX) <= ScreenEdgeEpsilon;
                if (isTrueScreenEdge && hasWalkable) boundaryX = Mathf.Min(boundaryX, walkableRightX);
                remainingToEdge = boundaryX - characterX;
            }
            else
            {
                float boundaryX = info.CurrentFootholdLeftWorldX;
                isTrueScreenEdge = Mathf.Abs(boundaryX - info.ScreenLeftWorldX) <= ScreenEdgeEpsilon;
                if (isTrueScreenEdge && hasWalkable) boundaryX = Mathf.Max(boundaryX, walkableLeftX);
                remainingToEdge = characterX - boundaryX;
            }

            return remainingToEdge <= stopDistance;
        }

        /// <summary>
        /// ★ 경계에서 무엇을 할지 한 번만 정한다(2026-08-29, 사용자 결정 "낙차가 작으면 뛰어내리게 한다").
        ///
        /// 우선순위와 그 근거:
        ///   1. **뛰어내리기**(낙차 [hopDownMinDropHeight, 매달리기 최소치)) — 가장 먼저 물어야 한다.
        ///      두 판정이 서로 다른 발판을 고를 수 있는데(예: 1유닛 아래 턱 + 5유닛 아래 바닥), 실제로
        ///      발이 먼저 닿는 면은 언제나 더 가까운 쪽이다. 매달리기를 먼저 채택하면 매달린 몸이 그
        ///      가까운 발판을 파고든다(StickmanBlackboard.TryFindHopDownTarget 문서 참고).
        ///   2. **매달려 내려가기**(낙차 >= 매달리기 최소치) — 기존 동작 그대로.
        ///   3. **되올라가기**(진행 방향에 stepUpMaxHeight 이하의 턱) — 내려갈 곳이 없을 때만 본다.
        ///      아래로 갈 수 있는 자리에서 위를 함께 보면 한 경계에서 방향이 왔다갔다한다.
        ///
        /// 반환값 true = 이번 프레임에 의도를 발행했으니 호출부는 즉시 return해야 한다.
        /// (뛰어내리기는 "확약"만 하고 아직 펄스를 내지 않는다 — 모서리 코앞까지 더 걸어가야 하므로
        ///  false를 돌려주고 바로 아래의 확약 블록이 그 걷기를 이어받는다.)
        /// </summary>
        private bool TryRollEdgeAction(GroundSensor.GroundInfo info)
        {
            // 1) 뛰어내리기 — 낙차가 작아 매달릴 이유가 없는 턱.
            float hopChance = Cfg(c => c.hopDownChance, 0.5f);
            if (hopChance > 0f && _blackboard.TryFindHopDownTarget(info, _direction, out long hopHandle, out float hopTopY))
            {
                if (_rng.NextDouble() < hopChance)
                {
                    _hopDownCommitted = true;
                    _moveInputX = _direction;
                    Debug.Log($"[뛰어내리기] 결정 — 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}, " +
                        $"낙차={(info.GroundWorldY - hopTopY):F3}유닛(매달리기 최소치 {_blackboard.LedgeHangMinDropDepth:F3}보다 작음), " +
                        $"내려앉을 발판핸들={hopHandle}. 모서리 코앞까지 걸어간 뒤 발을 뗍니다.");
                }
                // 추첨 성공이든 실패든 "여기서는 뛰어내리기 갈래였다"가 확정이다. 성공했으면 확약 블록이
                // 이어받고(그래서 false), 실패했으면 기존 배회 행동(정지 후 반대 방향)으로 흘러간다.
                return false;
            }

            // 2) 매달려 내려가기 — 손끝~발끝 거리보다 깊은 낙차(기존 동작).
            float hangChance = Cfg(c => c.ledgeHangChance, 0.35f);
            if (hangChance > 0f && _rng.NextDouble() < hangChance
                && _blackboard.TryFindDescendTarget(info, _direction, out _, out _))
            {
                // 실제 상태 전이는 WalkState가 한다(이 클래스는 "의도"만 만든다는 계약 유지).
                // 진행 방향을 그대로 유지해 매달리는 쪽을 바라보게 한다.
                _ledgeHangRequestedThisTick = true;
                _moveInputX = _direction;
                return true;
            }

            // 3) 되올라가기 — 내려갈 곳이 없고 진행 방향에 낮은 턱이 있을 때. ★ 이 분기가 없으면 한 번
            // Dock 아래로 내려간 캐릭터가 영영 못 올라온다(경계 점프 확률이 기본 0이라 ParkourClimb를
            // 유발할 다른 경로가 없다) — 2026-08-29 사용자 지시의 핵심 절반이다.
            float stepUpChance = Cfg(c => c.stepUpChance, 0.5f);
            if (stepUpChance > 0f && _rng.NextDouble() < stepUpChance
                && _blackboard.TryFindClimbableWall(info, _direction, out long wallHandle, out float wallTopY))
            {
                float wallHeight = wallTopY - info.GroundWorldY;
                float maxHeight = Cfg(c => c.stepUpMaxHeight, 1.5f);
                if (wallHeight <= maxHeight)
                {
                    _stepUpRequestedThisTick = true;
                    _moveInputX = _direction;
                    Debug.Log($"[되올라가기] 결정 — 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}, " +
                        $"턱 높이={wallHeight:F3}유닛(상한 {maxHeight:F2}), 턱 발판핸들={wallHandle}.");
                    return true;
                }
            }

            return false;
        }

        /// <summary>26-1: 최초(또는 매 Walk 페이즈 시작 시) 진행 방향은 좌우 50:50 랜덤. 단, 지금 위치가
        /// 이미 화면 경계에 붙어 있으면 26-2 로직을 재사용해 안쪽 방향으로 강제한다.</summary>
        private int PickDirectionAvoidingEdge()
        {
            int dir = _rng.NextDouble() < 0.5 ? -1 : 1;

            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (!info.Grounded) return dir;

            bool huggingRightScreenEdge = IsNearFootholdEdge(info, 1, out bool rightIsScreenEdge, out _) && rightIsScreenEdge;
            bool huggingLeftScreenEdge = IsNearFootholdEdge(info, -1, out bool leftIsScreenEdge, out _) && leftIsScreenEdge;
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
