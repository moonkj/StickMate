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
    public sealed class AutoWanderController : IMovementIntentSource, IPlannedDwellSource
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

        // ★ "주위 살피기" 최소 간격(2026-08-31 사용자 신고 "너무 자주함").
        //
        // 왜 트리거 조건이 아니라 유예 타이머인가 — 26-3의 조건("Idle 진입 후 지연시간 경과 시 그
        // 구간에 1회") 자체는 멀쩡하다. 문제는 그 위에 있는 26-1의 갈래다: Idle이 끝나면 25% 확률로
        // "Idle 연장"이 뽑히고, 연장은 EnterResting()을 다시 부르므로 **새 Idle 구간 = 새 추첨권**이
        // 된다. 그래서 한 번 쉬기 시작하면 2~6초마다 계속 나온다.
        //   실측(이 파일의 확률/지속시간 그대로 1시간 몬테카를로): 분당 9.7회 / 중앙값 간격 6.3초 /
        //   최소 간격 1.4초 -> 유예 30초에서 분당 1.8회 / 중앙값 32.9초.
        // 조건식을 건드리지 않고 유예만 얹는 이유는 26-3 스펙을 그대로 보존하기 위해서다
        // (StickConfig.wanderLookAroundCooldownSeconds = 0이면 정확히 예전 거동 = 네거티브 컨트롤).
        // 이 타이머는 **페이즈와 무관하게** Tick에서 줄어든다 — 걷는 동안에도 유예가 흐르지 않으면
        // "걷다 서면 매번 살피기"가 그대로 남는다.
        private float _lookAroundCooldownTimer;

        /// <summary>진단/테스트 창구 — 지금 남은 "주위 살피기" 유예(초). 0이면 다음 Idle에서 발동 가능.</summary>
        public float LookAroundCooldownRemaining => _lookAroundCooldownTimer;

        /// <summary>진단/테스트 창구 — 이 컨트롤러가 지금까지 실제로 올린 "주위 살피기" 신호 수.</summary>
        public int LookAroundRaisedCount { get; private set; }

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

        // ★ 되올라간 직후 "바로 다시 내려가기" 방지(2026-08-29, 사용자 신고 "독위로 가끔 올라오긴 하지만
        // 바로 다시 내려감"). 두 필드가 한 쌍이다:
        //   _lastSeenClimbMantleSequence — 블랙보드의 맨틀 완료 카운터를 마지막으로 본 값. 달라지면
        //     "방금 턱 위에 올라섰다"는 뜻이다(StickmanBlackboard.ClimbMantleSequence의 실측 근거 참고).
        //   _descendSuppressTimer — 이 시간(초) 동안 경계 추첨에서 **내려가는 갈래만** 제외한다.
        //     되올라가기와 "경계에서 돌아서기"는 그대로 두므로 화면 밖으로 걸어 나가는 경로는 없다.
        private int _lastSeenClimbMantleSequence;
        private float _descendSuppressTimer;

        private float _moveInputX;
        private bool _jumpRequestedThisTick;
        private bool _ledgeHangRequestedThisTick;
        private bool _hopDownRequestedThisTick;
        private bool _stepUpRequestedThisTick;

        // ==================== 진단/테스트 창구 (2026-08-30 R3-M1) ====================
        // 왜 필요한가: R3-M1은 "값이 맞는가"가 아니라 "**판정을 쓰는 쪽**이 그 값을 실제로 보는가"의
        // 문제였다. 설정 상수만 검사하는 테스트는 소비자가 유도를 그만 읽어도 초록불을 낸다.
        // 그래서 IsNearFootholdEdge가 계산한 것을 그대로 노출해, 테스트가 **소비자가 본 숫자**를
        // 단언할 수 있게 한다(이 프로젝트의 기존 진단 창구 관례 — CharacterPetRenderer.BallSpinDegrees,
        // CharacterInfoWindow.VisibleScreenRectOf 등과 같은 성격이며 제품 로직은 읽지 않는다).

        /// <summary>직전 <c>IsNearFootholdEdge</c> 호출이 실제로 쓴 경계 판정 거리(유도값).</summary>
        public float LastEdgeStopDistanceUsed { get; private set; }

        /// <summary>그 호출에서 잰 진행 방향 앞쪽 잔여 거리.</summary>
        public float LastRemainingToEdge { get; private set; }

        /// <summary>그 호출의 판정 결과("지금 경계 근처인가").</summary>
        public bool LastEdgeNear { get; private set; }

        /// <summary>그 호출의 진행 방향(+1 오른쪽 / -1 왼쪽). 어느 쪽 경계를 잰 표본인지 구분용.</summary>
        public int LastEdgeDirection { get; private set; }

        public float MoveInputX => _moveInputX;
        public bool JumpRequested => _jumpRequestedThisTick;
        public bool LedgeHangRequested => _ledgeHangRequestedThisTick;
        public bool HopDownRequested => _hopDownRequestedThisTick;
        public bool StepUpRequested => _stepUpRequestedThisTick;

        /// <summary>
        /// ★ 발화 자격 게이트(docs/UX_FLOW.md 5절 규칙 8)가 읽는 **계획 잔여 체류 시간**(초).
        ///
        /// 지어낸 값이 아니다 — 휴식/이동 길이는 각 페이즈 진입에서 이미 한 번 추첨되어 확정돼 있고
        /// (26-1: Idle 2~6초 / Walk 1.5~4초), 여기서는 그 확정값에서 경과분을 뺀 나머지를 그대로
        /// 노출할 뿐이다. 계획은 외부 사건(모서리 도달·피격·발판 소실)으로 깨질 수 있는데, 깨진
        /// 경우는 규칙 3-b(즉시 취소)와 4-c ③(즉시 컷)이 받는다 — 즉 <b>게이트가 다수(예측 가능한
        /// 종료)를, 즉시 컷이 소수(예측 불가 인터럽트)를</b> 담당한다.
        ///
        /// ★ 2026-09-01 — 경계 정지(_isEdgePaused)를 <b>별도의 갈래로</b> 답한다. 예전에는 정지 중에도
        ///   Walk 페이즈의 잔여(_moveDuration - _moveTimer)를 그대로 답했는데, 정지 중에는 _moveTimer가
        ///   멈추므로 그 값은 <b>지금 서 있는 시간</b>과 아무 관계가 없었다. 경계 정지는 길이가
        ///   BeginEdgePause()에서 <b>이미 추첨돼 확정된</b> 계획이므로, 그것을 답하는 것이 지어내기가
        ///   아니라 오히려 정직한 답이다. 정지가 끝나면 방향만 뒤집어 계속 걷는 경우가 많아 실제
        ///   정지 시간은 이보다 길 수 있지만, 게이트는 <b>짧게 잡는 쪽이 안전</b>하다(과대평가가
        ///   "말할 시간이 있다고 판정해 놓고 잘리는" 번쩍임을 만든다).
        /// </summary>
        public float PlannedDwellRemainingSeconds =>
            _phase == Phase.Resting
                ? Mathf.Max(0f, _restDuration - _restTimer)
                : _isEdgePaused
                    ? Mathf.Max(0f, _edgePauseDuration - _edgePauseTimer)
                    : Mathf.Max(0f, _moveDuration - _moveTimer);

        public AutoWanderController(StickmanBlackboard blackboard, StickConfig config, System.Random rng)
        {
            _blackboard = blackboard;
            _config = config;
            _rng = rng ?? new System.Random();
            // 생성 시점의 카운터를 기준선으로 잡는다 — 그렇지 않으면 과거의 등반 한 번을 "방금 올라섰다"로
            // 오인해 첫 Tick에 엉뚱하게 걷기 시작한다(테스트가 컨트롤러를 나중에 갈아 끼우는 경로가 있다).
            _lastSeenClimbMantleSequence = _blackboard != null ? _blackboard.ClimbMantleSequence : 0;
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

            if (_descendSuppressTimer > 0f) _descendSuppressTimer -= deltaTime;
            if (_lookAroundCooldownTimer > 0f) _lookAroundCooldownTimer -= deltaTime;
            ConsumeClimbMantleSignalIfAny();

            if (_phase == Phase.Resting) TickResting(deltaTime);
            else TickMoving(deltaTime);
        }

        /// <summary>
        /// ★ "되올라간 직후 곧바로 다시 내려감" 수정의 본체(2026-08-29). ParkourClimbState가 턱 위에
        /// 실제로 올라선 프레임에 올린 신호를 소비한다.
        ///
        /// 실측한 고장 순서(Logs, frame 번호는 실제 로그 값):
        ///   f=8925 아래 발판 경계에서 되올라가기 당첨 -> ParkourClimb 진입(f=8926)
        ///   f=8926 배회 AI는 등반을 모른 채 여전히 "경계에 서 있다"고 보고 경계 정지(0.45초)를 건다
        ///   f=8976 등반 도중 그 정지가 끝나며 **진행 방향을 바깥쪽으로 반전** + 경계 추첨권 리셋
        ///   f=8982 등반 완료. 맨틀 지점은 모서리에서 0.250유닛 안쪽인데 경계 판정 거리는 0.300이라
        ///          **올라선 그 프레임에 이미 경계** -> 뛰어내리기 추첨 -> f=8991 발을 뗌(약 0.15초 만에).
        ///
        /// 그래서 여기서 세 가지를 한다 — (1) 진행 중이던 경계 정지/뛰어내리기 확약 취소,
        /// (2) 진행 방향을 **올라선 방향(턱 안쪽)** 으로 되돌려 새 걷기 구간 시작(그 자리에 멈춰 서 있으면
        /// 쿨다운이 끝나는 순간 같은 일이 반복된다), (3) 내려가는 갈래만 쿨다운 동안 추첨에서 제외.
        ///
        /// StickConfig.postClimbDescendCooldown이 0 이하면 아무 것도 하지 않는다 = 예전 거동(네거티브 컨트롤).
        /// </summary>
        private void ConsumeClimbMantleSignalIfAny()
        {
            if (_blackboard == null) return;
            int sequence = _blackboard.ClimbMantleSequence;
            if (sequence == _lastSeenClimbMantleSequence) return;
            _lastSeenClimbMantleSequence = sequence;

            float cooldown = Cfg(c => c.postClimbDescendCooldown, 8f);
            if (cooldown <= 0f) return;

            _descendSuppressTimer = cooldown;
            int inward = _blackboard.ClimbMantleDirection >= 0 ? 1 : -1;
            EnterMoving(inward);
            Debug.Log($"[되올라가기] 안착 — 턱 안쪽({(inward > 0 ? "오른쪽" : "왼쪽")})으로 걸어 들어갑니다. " +
                $"되내려가기는 {cooldown:F1}초 동안 유예(경계에서 돌아서기/추가 되올라가기는 그대로).");
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
            // ★ 여기에 최소 간격(_lookAroundCooldownTimer)이 하나 더 걸린다 — 위 필드 선언부의 실측 참고.
            //   추첨권(_lookAroundFiredThisRest)은 유예에 막혔더라도 **함께 소모한다**. 안 그러면 유예가
            //   풀리는 순간 그 Idle 구간 안에서 곧바로 발동해 "가끔 두 번 연속"이 남는다.
            if (!_lookAroundFiredThisRest && _restTimer >= _lookAroundDelay && _restTimer < _restDuration)
            {
                _lookAroundFiredThisRest = true;
                if (_lookAroundCooldownTimer <= 0f)
                {
                    _lookAroundCooldownTimer = Mathf.Max(0f, Cfg(c => c.wanderLookAroundCooldownSeconds, 30f));
                    LookAroundRaisedCount++;
                    StickmanEventBus.RaiseWanderAmbientMotionRequested(WanderAmbientMotion.LookAround);
                }
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

        private void EnterMoving() => EnterMoving(0);

        /// <param name="forcedDirection">0이면 26-1대로 좌우 랜덤(경계 회피 포함). +1/-1이면 그 방향으로
        /// 강제한다 — 맨틀 직후 "올라선 턱 안쪽으로 걸어 들어가기"에만 쓴다.</param>
        private void EnterMoving(int forcedDirection)
        {
            _phase = Phase.Moving;
            _moveTimer = 0f;
            _moveDuration = Jitter(RandomRange(Cfg(c => c.wanderWalkDurationMin, 1.5f), Cfg(c => c.wanderWalkDurationMax, 4f)));
            _turnCheckTimer = 0f;
            _spontaneousTurnUsedThisPhase = false;
            _isEdgePaused = false;
            _edgeActionRolledThisLeg = false;
            _hopDownCommitted = false;
            _direction = forcedDirection != 0 ? (forcedDirection > 0 ? 1 : -1) : PickDirectionAvoidingEdge();
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
        /// (★ 2026-09-02: 아래 "isTrueScreenEdge인 쪽에서만"이라는 게이트가 멀티모니터에서 거짓이 되어
        ///  같은 증상이 되살아났다 — 지금은 <see cref="ResolveEffectiveEdgeBoundary"/>가 게이트 없이
        ///  언제나 클램프 한계를 반영한다. 그 함수 문서에 실측 로그와 근거가 있다.)
        /// **원시 발판 경계가 아니라 캐릭터가 실제로 갈 수 있는 한계**
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
            // ★ 2026-08-30 R3-M1 — 설정값을 그대로 쓰지 않고 **몸의 물리 반폭에서 유도한다.**
            // 설정값 0.300은 몸이 벽에 부딪혀 설 수 있는 이격(배율 0.75에서 0.305)보다 작아서,
            // Dock 물리 계단 옆면에 붙어 선 캐릭터가 이 밴드에 **물리적으로 들어갈 수 없었다** —
            // 그 결과 되올라가기 판정을 평가할 기회조차 없이 걷기 구간이 끝날 때까지 벽에 붙어 있었다.
            // 유도식/근거: Core/DockGeometry.ResolveEdgeStopDistance.
            float stopDistance = _blackboard != null
                ? _blackboard.EdgeStopDistanceWorld
                : Cfg(c => c.wanderEdgeStopDistance, 0.3f);
            float characterX = _blackboard.Body != null ? _blackboard.Body.position.x : 0f;
            bool hasWalkable = _blackboard.TryGetWalkableScreenBoundsWorld(out float walkableLeftX, out float walkableRightX);

            if (direction > 0)
            {
                float boundaryX = ResolveEffectiveEdgeBoundary(info.CurrentFootholdRightWorldX,
                    info.ScreenRightWorldX, hasWalkable, walkableRightX, 1, out isTrueScreenEdge);
                remainingToEdge = boundaryX - characterX;
            }
            else
            {
                float boundaryX = ResolveEffectiveEdgeBoundary(info.CurrentFootholdLeftWorldX,
                    info.ScreenLeftWorldX, hasWalkable, walkableLeftX, -1, out isTrueScreenEdge);
                remainingToEdge = characterX - boundaryX;
            }

            bool near = remainingToEdge <= stopDistance;

            // 진단 창구 갱신 — 제품 로직은 이 값을 읽지 않는다(테스트/로그 전용).
            LastEdgeStopDistanceUsed = stopDistance;
            LastRemainingToEdge = remainingToEdge;
            LastEdgeNear = near;
            LastEdgeDirection = direction >= 0 ? 1 : -1;

            return near;
        }

        /// <summary>
        /// ★★ 2026-09-02 — 사용자 신고 <i>"지금 멀티모니터 쓰는데 창에서 다른 모니터로 못넘어가는데도
        /// 끝 벽쪽에서 계속 걷고 있음. 제자리걸음인거지"</i>의 본체. <b>순수 함수</b>로 뽑아 EditMode가
        /// 씬 없이 직접 잰다(같은 판정을 테스트가 다시 적으면 어긋난다).
        ///
        /// ============================================================================
        /// 무엇이 고장났나 — 2026-08-29 러닝머신 수정에 <b>게이트가 하나 잘못 걸려 있었다</b>
        /// ============================================================================
        /// <para>그 라운드는 "화면 하드 클램프가 붙잡아 세우는 자리"를 경계로 삼아 러닝머신을 없앴다.
        /// 그런데 그 보정을 <c>isTrueScreenEdge</c>(= 지금 딛은 발판의 경계가 <b>모든 발판을 통틀어</b>
        /// 가장 바깥인가)일 때만 적용했다. 그 조건은 발판이 화면 하나에 다 들어 있을 때만 참이다.</para>
        ///
        /// <para><b>멀티모니터에서는 거짓이 된다</b>: 두 번째 모니터의 창도 발판으로 열거되므로
        /// <c>GroundInfo.ScreenRightWorldX</c>(전체 발판 통합 경계)가 우리 오버레이 화면 바깥까지
        /// 뻗는다. 그러면 화면을 꽉 채운 창 위를 걷고 있어도 그 창의 오른쪽 끝은 "통합 경계"가 아니라서
        /// 보정이 통째로 꺼지고, 클램프가 화면 끝 35.2pt 안쪽에서 몸을 붙잡는 동안 배회 AI는
        /// <b>아직 발판 끝까지 35.2pt 남았다</b>고 계산한다. 돌아서는 임계(≈24pt)에 영영 못 미쳐
        /// 걷기 애니메이션만 도는 제자리걸음이 된다(사용자 로그: 같은 자리에서 <c>상태=Walk</c> 5연속,
        /// 로그 throttle 2초 기준으로 최소 10초).</para>
        ///
        /// <para><b>처방</b>: 게이트를 없앤다. 클램프 한계는 통합 경계와 무관한 <b>물리적 사실</b>이므로
        /// 언제나 적용해도 된다 — 발판 경계가 클램프보다 안쪽이면 아무 것도 바뀌지 않는다(그 경우
        /// <c>clampBinds</c>가 false다). 그리고 클램프가 발판 끝보다 <b>앞에서</b> 막는다면 그 지점이
        /// 곧 이 화면의 물리적 끝이므로 <c>isTrueScreenEdge</c>도 참으로 돌려준다 — 그래야 그 자리에서
        /// 뛰어내리기/매달리기/되올라가기 추첨과 경계 점프가 그대로 금지되고(넘어갈 수 없는 벽이다),
        /// 남는 갈래는 <b>정지 후 반대 방향</b> 하나가 된다.</para>
        ///
        /// <para><b>떨림이 생기지 않는 이유</b>: 이 함수는 방향을 뒤집지 않는다. 돌아서기는 기존
        /// <c>BeginEdgePause</c> 경로(0.3~0.8초 정지 후 1회 반전)가 그대로 담당하므로 매 프레임
        /// 좌우가 뒤집히는 경로 자체가 없다.</para>
        /// </summary>
        /// <param name="footholdBoundaryX">지금 딛고 있는 발판 하나의 그 방향 경계(월드 X).</param>
        /// <param name="unionBoundaryX">모든 발판 통합 경계(월드 X) — <c>GroundInfo.Screen*WorldX</c>.</param>
        /// <param name="hasWalkable">화면 하드 클램프 한계를 조회할 수 있었는가.</param>
        /// <param name="walkableBoundaryX">그 방향으로 캐릭터가 실제로 갈 수 있는 한계(월드 X).</param>
        /// <param name="direction">+1 오른쪽 / -1 왼쪽.</param>
        /// <param name="isTrueScreenEdge">이 경계가 "더 갈 곳이 없는 화면의 끝"인가.</param>
        /// <returns>경계 판정에 실제로 써야 할 월드 X.</returns>
        internal static float ResolveEffectiveEdgeBoundary(float footholdBoundaryX, float unionBoundaryX,
            bool hasWalkable, float walkableBoundaryX, int direction, out bool isTrueScreenEdge)
        {
            bool unionEdge = Mathf.Abs(footholdBoundaryX - unionBoundaryX) <= ScreenEdgeEpsilon;
            bool clampBinds = hasWalkable && (direction > 0
                ? walkableBoundaryX < footholdBoundaryX
                : walkableBoundaryX > footholdBoundaryX);

            isTrueScreenEdge = unionEdge || clampBinds;
            return clampBinds ? walkableBoundaryX : footholdBoundaryX;
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
        ///   3. **되올라가기**(진행 방향에 stepUpMaxHeights x 신장 이하의 턱) — 내려갈 곳이 없을 때만 본다.
        ///      아래로 갈 수 있는 자리에서 위를 함께 보면 한 경계에서 방향이 왔다갔다한다.
        ///
        /// 반환값 true = 이번 프레임에 의도를 발행했으니 호출부는 즉시 return해야 한다.
        /// (뛰어내리기는 "확약"만 하고 아직 펄스를 내지 않는다 — 모서리 코앞까지 더 걸어가야 하므로
        ///  false를 돌려주고 바로 아래의 확약 블록이 그 걷기를 이어받는다.)
        /// </summary>
        private bool TryRollEdgeAction(GroundSensor.GroundInfo info)
        {
            // ★ 되올라간 직후 유예 구간(2026-08-29) — 내려가는 두 갈래(1·2)만 건너뛰고 되올라가기(3)와
            // 기존 배회 거동(정지 후 반대 방향)은 그대로 둔다. 이 구간에서도 경계에서 "돌아서기"는
            // 정상 동작하므로 화면 밖으로 걸어 나가지 않는다(ConsumeClimbMantleSignalIfAny 문서 참고).
            bool descendSuppressed = _descendSuppressTimer > 0f;

            // 1) 뛰어내리기 — 낙차가 작아 매달릴 이유가 없는 턱.
            float hopChance = Cfg(c => c.hopDownChance, 0.5f);
            if (!descendSuppressed && hopChance > 0f && _blackboard.TryFindHopDownTarget(info, _direction, out long hopHandle, out float hopTopY))
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
            if (!descendSuppressed && hangChance > 0f && _rng.NextDouble() < hangChance
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
                float maxHeight = ResolveStepUpMaxHeight();
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

        // ============================================================================
        // ★ 되올라가기 상한을 **실측 Dock 낙차**에서 유도한다 (2026-08-30 횡단 리뷰 M3)
        // ============================================================================
        // 리뷰가 찾아낸 사실: StickConfig의 되올라가기 상한 2.4(당시 절대 유닛)는 **이 개발 머신의 tilesize(49) 하나**에
        // 맞춰 고른 절대값이었다. macOS의 tilesize 범위는 16~128이고 낙차는 tilesize+18pt이므로,
        //     tilesize  16 → 0.83유닛 / 48 → 1.61 / 80 → 2.40(여기서 상한과 같아짐) / 128 → 3.57
        // tilesize 80 이상을 쓰는 사용자에게는 "한 번 Dock 아래로 내려가면 영영 못 올라온다"가
        // **고쳤다고 믿은 뒤에도 그대로 남아 있었다**(사용자가 세 번 신고한 그 증상). 절대값 하나로는
        // 어떤 값을 넣어도 누군가에게는 틀린다 — tilesize가 사용자 설정이기 때문이다.
        //
        // 그래서 상한 = max(설정 절대값, **실측 낙차** + 여유). 실측은 새 OS 조회가 아니라 이미
        // 열거돼 있는 발판 두 개(Dock 띠 / 바닥 안전망)의 상단 Y 차이다 — 권한도, 네이티브 호출도,
        // 좌표계 변환도 하나 늘지 않는다. Dock을 못 찾으면(자동 숨김 / 좌우 세로 Dock / 비-macOS /
        // 전체화면 감지 중) 예전과 100% 같은 절대값으로 되돌아간다.

        /// <summary>되올라갈 수 있는 최대 턱 높이(월드 유닛). 위 문단 참고.
        /// <para>★ 2026-09-02 — 설정값이 절대 유닛에서 <b>신장 배수</b>가 됐다(StickConfig.stepUpMaxHeights).
        /// 그래서 여기서 신장을 곱해 월드로 환산한 뒤 예전과 똑같이 DockGeometry에 넘긴다 —
        /// 유도식(max(설정, 실측 낙차 + 여유))은 한 줄도 바뀌지 않는다.</para></summary>
        private float ResolveStepUpMaxHeight()
        {
            float configured = _blackboard != null && _blackboard.Config != null
                ? _blackboard.Config.ResolveStepUpMaxHeightWorld(_blackboard.CharacterHeightWorld)
                : StickConfig.BaselineCharacterTotalHeight * 1.0551f;
            if (!TryMeasureDockDropWorldUnits(out float dockDrop)) return configured;

            float resolved = DockGeometry.ResolveStepUpMaxHeight(configured, dockDrop);

            // 설정값만으로는 못 올라오는 환경이라는 사실 자체를 한 번은 남긴다 — 이 로그가 뜬다는 것은
            // "이 사용자의 Dock에서는 stepUpMaxHeights 설정값이 무의미하다"는 뜻이고, 위 유도가 없었다면
            // 그대로 갇혔을 환경이라는 뜻이다.
            if (dockDrop > configured && !_loggedDockDropExceedsConfiguredStepUp)
            {
                _loggedDockDropExceedsConfiguredStepUp = true;
                Debug.LogWarning($"[되올라가기] 실측 Dock 낙차 {dockDrop:F3}유닛이 stepUpMaxHeights 환산값 " +
                    $"{configured:F3}을 넘습니다(Dock 아이콘이 큰 설정). 상한을 {resolved:F3}유닛으로 올려 " +
                    "되올라가기를 유지합니다 — 이 유도가 없으면 한 번 내려간 캐릭터가 영영 못 올라옵니다.");
            }
            return resolved;
        }

        private bool _loggedDockDropExceedsConfiguredStepUp;

        /// <summary>Dock 발판 상단 − 바닥 안전망 상단 = 지금 이 화면의 진짜 낙차(월드 유닛).
        /// 핸들의 의미와 이 측정을 여기 둔 이유는 Core/DockGeometry.cs 하단 주석 참고.</summary>
        private bool TryMeasureDockDropWorldUnits(out float dropWorldUnits)
        {
            dropWorldUnits = 0f;
            if (_blackboard == null) return false;

            if (!_blackboard.TryGetFootholdTopWorldY(
                    StickMate.Platform.FallbackPlatformWindowService.DockFootholdHandle, out float dockTopY)) return false;

            // 안전망은 Dock 좌우로 잘린 두 조각이고 둘의 상단 Y는 같은 단일 소스에서 나오므로 어느 쪽을
            // 재도 같다. 한쪽 조각이 폭 0으로 죽어 있는 배치(Dock이 화면 끝까지 넓은 경우)를 위해 둘 다 본다.
            float netTopY = 0f;
            if (!_blackboard.TryGetFootholdTopWorldY(
                    StickMate.Platform.FallbackPlatformWindowService.SyntheticFootholdHandle, out netTopY)
                && !_blackboard.TryGetFootholdTopWorldY(
                    StickMate.Platform.FallbackPlatformWindowService.SyntheticFootholdHandleRight, out netTopY))
            {
                return false;
            }

            float drop = dockTopY - netTopY;
            if (drop <= 0f || float.IsNaN(drop)) return false;
            dropWorldUnits = drop;
            return true;
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
