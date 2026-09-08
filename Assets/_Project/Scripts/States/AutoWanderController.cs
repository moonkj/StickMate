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

        // ==================== 밧줄 경계 진단 (2026-09-08 신설) ====================
        //
        // ★★ 왜 여기에 두는가 — <b>계기가 한쪽 플랫폼에만 있었다.</b>
        // 사용자가 "윈도우에서는 밧줄 동작안함"을 두 번 신고했는데, 리더는 두 번 다 소스 추론으로만
        // 답했다. 원격으로 볼 수 있는 유일한 발판 계기 <c>[발판리포트]</c>가
        // <c>Platform/MacOS/MacOverlayStateEnforcer.cs</c>에만 있어 <b>Windows 로그에는 아무것도
        // 남지 않았기 때문</b>이다. 그리고 정작 답이 필요했던 질문은 발판 목록이 아니라 이 두 개였다:
        //   (1) 그 경계가 <b>화면 자체의 끝</b>이었는가(그러면 예전 게이트가 추첨을 통째로 막았다),
        //   (2) 밧줄 <b>대역에 드는 벽</b>이 하나라도 있었는가.
        // 그래서 계기를 플랫폼 전용 파일이 아니라 <b>판정이 실제로 일어나는 이 자리</b>에 둔다 —
        // 여기는 플랫폼 분기가 0건이라 macOS/Windows가 자동으로 같은 로그를 남긴다
        // (CLAUDE.md "정책 판정은 플랫폼 중립 위치에, 플랫폼 전용 코드는 사실 조회만"과 같은 논지).
        //
        // ★ 상주 앱이므로 <b>요약해서</b> 남긴다 — 매 경계마다 찍으면 로그가 잠긴다(PlayerLogPolicy).
        //   주기마다 «몇 번 만났고 그중 몇 번이 화면 끝이었고 벽은 몇 번 찾았나»를 한 줄로 접는다.
        private const float RopeEdgeDiagnosticIntervalSeconds = 60f;
        private float _ropeEdgeDiagTimer;
        private int _ropeEdgeEncounters;        // 경계 근처에서 추첨 자격을 얻은 횟수
        private int _ropeEdgeTrueScreenEdges;   // 그중 화면 자체의 끝이었던 횟수
        private int _ropeEdgeWallFound;         // 밧줄 대역 벽을 실제로 찾은 횟수
        private int _ropeEdgeRollWon;           // 찾은 뒤 확률까지 통과한 횟수
        private float _ropeEdgeLastBandLow;     // 마지막으로 쓴 대역 하한(파쿠르 상한)
        private float _ropeEdgeLastBandHigh;    // 마지막으로 쓴 대역 상한(밧줄 상한)

        // ==================== 목표 지향 등반 (2026-09-08 사용자 지시로 신설) ====================
        //
        // 사용자 원문: "그냥 바닥에 캐릭터가 위치할때 전체 화면을 한번 스캔한후 창들의 위치 및 창 상단의
        // 좌표만 인식하면 확률로 밧줄 던지면 되는거 아니야?" / "목표창 아래로 가서 던짐".
        //
        // 왜 필요했는지(= 기존 경계 도달형이 Windows에서 구조적으로 죽어 있던 이유)는
        // StickmanBlackboard.TryPickClimbTarget 위 문단에 한 곳으로 모아 뒀다.
        //
        // ★ 기존 경계 기반 경로는 <b>한 줄도 건드리지 않는다</b> — 이 신설분은 그 위에 «또 하나의
        //   기회»를 더한다. macOS 실측 거동(120초/회)은 경계 경로에서 나온 값이라 그대로 살아 있다.
        private float _climbSeekTimer;
        private bool _climbTargetActive;
        private long _climbTargetHandle;
        private float _climbTargetApproachX;
        private int _climbTargetDirection;
        private bool _climbTargetIsRope;        // false면 손 등반(파쿠르) 대역이다
        private float _climbTargetGiveUpTimer;  // 못 가고 있는 시간 — 오래 끌면 포기한다
        // 진단 카운터 — [밧줄진단] 한 줄에 함께 접어 남긴다(그 함수 문단 참고).
        private int _climbSeekWon;          // 목표까지 걸어가 실제로 던진 횟수
        private int _climbSeekMissNoWall;   // 훑었는데 대역에 드는 벽이 없던 횟수
        private int _climbSeekLostRoll;     // 벽은 있었는데 추첨에서 진 횟수

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
        // ★ 밧줄 등반(2026-09-07, docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md 1-C) — StepUpRequested와
        // 완전히 동일한 1프레임 펄스 계약이되 별도 채널이다(IMovementIntentSource.RopeClimbRequested
        // 문서 참고 — 소비자가 손 등반/밧줄 등반을 구분해야 한다).
        private bool _ropeClimbRequestedThisTick;

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
        public bool RopeClimbRequested => _ropeClimbRequestedThisTick;

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
            _ropeClimbRequestedThisTick = false;

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
            // ★ 대사는 **행동이 확정된 뒤** 그 결과에서 파생한다(절대 불변 원칙 1을 로그에도 적용).
            //   부채꼴 제자리 대기가 걸리면 EnterMoving은 Resting으로 되돌아가므로, 여기서 _phase를
            //   읽지 않고 "걸어 들어갑니다"를 찍으면 화면과 로그가 갈라진다.
            string outcome = _phase == Phase.Moving
                ? $"턱 안쪽({(inward > 0 ? "오른쪽" : "왼쪽")})으로 걸어 들어갑니다"
                : "부채꼴 메뉴가 떠 있어 그 자리에서 대기합니다(메뉴가 닫히면 평소대로 이어집니다)";
            Debug.Log($"[되올라가기] 안착 — {outcome}. " +
                $"되내려가기는 {cooldown:F1}초 동안 유예(경계에서 돌아서기/추가 되올라가기는 그대로).");
        }

        // ==================== Resting (26-1, 26-3) ====================

        private void EnterResting()
        {
            _phase = Phase.Resting;
            _restTimer = 0f;
            // ★ 2026-09-06 집중 세션 — "한 번 서면 더 오래 선다"(2~6초 -> 4~11초). 추첨 구조는 그대로고
            //   **읽는 값만** 바뀐다(아래 IsFocusAmbientActive 문서 참고).
            bool focus = IsFocusAmbientActive;
            float idleMin = focus
                ? Cfg(c => c.focusSessionIdleDurationMin, 4f)
                : Cfg(c => c.wanderIdleDurationMin, 2f);
            float idleMax = focus
                ? Cfg(c => c.focusSessionIdleDurationMax, 11f)
                : Cfg(c => c.wanderIdleDurationMax, 6f);
            _restDuration = Jitter(RandomRange(idleMin, idleMax));
            _lookAroundFiredThisRest = false;
            _lookAroundDelay = RandomRange(Cfg(c => c.wanderLookAroundDelayMin, 1f), Cfg(c => c.wanderLookAroundDelayMax, 2.5f));
            _moveInputX = 0f;
        }

        // ============================================================================
        // ★★ 집중 세션 앰비언트 (2026-09-06, docs/UX_MOTION_FOCUS_SESSION.md 4-3 / 4-4)
        // ============================================================================
        // 페르소나(소은) 지적 "집중모드 25분 세션의 99.87%가 평소와 똑같다"의 발행자 쪽 절반이다.
        //
        // ★ 이 파일에 **새 타이머도 새 추첨도 하나 늘지 않았다.** 발행 창구는 아래 TickResting의
        //   기존 1회 추첨 그대로이고, 세션 중에는 그 추첨이 읽는 **값 두 개**(쿨다운·어휘)와 Idle
        //   길이/걷기 확률이 바뀔 뿐이다. 빈도를 평소보다 **높이지 않는 것**이 설계 제약이었다 —
        //   2026-08-31 사용자 신고 "너무 자주함"의 체감 하한(분당 1.8회 ≈ 33초)이 그 근거다.
        //
        // ★ 걷기를 죽이지 않는다(0.75 -> 0.40). 파쿠르/뛰어내리기/매달리기로 이어지는 경로가 전부
        //   걷기에서 갈라져 나오므로, 0으로 내리면 25분 동안 그 연출들이 **구조적으로 도달 불가**가
        //   된다. 25분 중 3.2분은 여전히 돌아다닌다.

        /// <summary>집중 세션 앰비언트가 지금 켜져 있는가(마스터 스위치 × 세션 진행 여부).
        /// 판정의 정본은 <see cref="StickmanBlackboard.IsFocusSessionAmbientActive"/> 한 곳이며
        /// 여기서 다시 해석하지 않는다 — 자세/어휘/수용이 각자 판단하면 "반만 꺼진" 상태가 생긴다.</summary>
        private bool IsFocusAmbientActive => _blackboard != null && _blackboard.IsFocusSessionAmbientActive;

        /// <summary>
        /// ★ 2026-09-06 사용자 지시 <i>"메뉴를 펼쳤을때는 캐릭터가 제자리대기."</i> —
        /// 부채꼴이 떠 있는 동안 <b>새 걷기 구간을 시작하지 않는가</b>.
        /// 판정의 정본은 <see cref="StickmanBlackboard.IsRadialMenuHoldActive"/> 한 곳이며
        /// 여기서 다시 해석하지 않는다(<see cref="IsFocusAmbientActive"/>와 같은 어법).
        ///
        /// <para>★ <b>이 클래스는 여전히 수평 이동의 소유자다.</b> 부채꼴이 캐릭터를 움직이는 것이
        /// 아니라, 부채꼴이 «떠 있다»는 <b>사실</b>을 배회 AI가 읽고 자기 추첨을 바꾸는 형태다 —
        /// 소유권을 넘기면 «누가 캐릭터를 움직였는가»가 두 곳에서 판정된다.</para>
        /// </summary>
        private bool IsRadialMenuHolding => _blackboard != null && _blackboard.IsRadialMenuHoldActive;

        /// <summary>
        /// ★ 2026-09-07 design-motion — 사용자가 <b>자리를 비운 동안</b> 걷기 확률만 낮추는가.
        /// 판정의 정본은 <see cref="StickmanBlackboard.IsViewerAwayWanderActive"/> 한 곳이며
        /// 여기서 다시 해석하지 않는다(<see cref="IsFocusAmbientActive"/> · <see cref="IsRadialMenuHolding"/>와
        /// 같은 어법).
        ///
        /// <para><b>이것도 «묶어두기»가 아니라 분포 변경이다</b> — 집중 세션 절과 같은 이유로 0이 아니라
        /// 0.15다. 0으로 내리면 파쿠르·뛰어내리기·매달리기가 밤새 <b>구조적으로 도달 불가</b>가 된다.</para>
        ///
        /// <para>★ <b>Idle 길이(<c>wanderIdleDurationMin/Max</c>)는 건드리지 않는다.</b> 집중 세션은
        /// 그 값을 4~11초로 늘렸고 그만큼 복귀 지연이 커졌는데, 자리 비움에서 같은 수를 쓰면
        /// <b>"돌아왔는데 캐릭터가 11초간 안 움직인다"</b>가 된다. 확률만 낮추면 복귀 지연 p99가
        /// 13.37 -> 13.79초로 거의 변하지 않는다(design-motion 시뮬레이션).</para>
        /// </summary>
        private bool IsViewerAwayForWander => _blackboard != null && _blackboard.IsViewerAwayWanderActive;

        /// <summary>
        /// ★ 2026-09-07 코스튬 × 집중 세션 — <b>소환 오브젝트가 지금 화면에 실제로 서 있는가</b>.
        ///
        /// <para><b>왜 「몰입기인가」가 아니라 「프롭이 섰는가」인가</b>: 프롭은 몰입기 진입 프레임의
        /// 좌표에 <b>고정</b>되므로, 캐릭터가 걸어가면 <b>프롭만 덩그러니 남는다</b> —
        /// 「걷기 0」과 「프롭 고정」은 한 묶음이다. 반대로 자리가 좁아 프롭을 못 세웠으면(폴백 F4)
        /// 붙어 있을 물건이 없으므로 걷기를 낮출 이유도 없다. 즉 이 조건의 <b>정확한 사실</b>은
        /// 구간이 아니라 배치 결과이고, 그 값은
        /// <c>Interaction.CostumePropRenderer.PropPlaced</c> 한 곳에만 있다 — 여기서 다시 해석하지 않는다
        /// (<see cref="IsFocusAmbientActive"/>와 같은 어법이며, 코스튬 포즈 층도 <b>같은 값</b>을 본다.
        /// 자세와 배회가 각자 판단하면 「반만 몰입한」 상태가 생긴다).</para>
        ///
        /// <para>★ <b>2026-09-07 P4에서 이사 완료.</b> P3에서는 이 파일이 프롭 렌더러를 직접 캐시
        /// 조회했다(<c>StickmanBlackboard</c>가 다른 담당자 소유라 손대지 못했다). 지금은 같은 어법의
        /// 다른 셋과 똑같이 <b>블랙보드에 한 줄 위임</b>한다 —
        /// <c>StickmanBlackboard.IsCostumeImmersionActive</c>는 코스튬 포즈 층도 보는 <b>같은 한 판정</b>이다.
        /// 사실이 옮겨간 것이 아니라(정본은 여전히 <c>PropPlaced</c>) 조회 지점만 모인 것이다.</para>
        /// </summary>
        private bool IsCostumeImmersionHolding
            => _blackboard != null && _blackboard.IsCostumeImmersionActive;

        /// <summary>자리 비움 걷기 감쇄를 <b>이미 로그로 알렸는가</b>(엣지에서만 한 줄 — 24시간 상주
        /// 앱에서 추첨마다 찍으면 하룻밤에 수천 줄이다). 입력이 돌아오면 조용히 내려가고, 다시
        /// 비우면 그때 한 줄이 더 나간다.</summary>
        private bool _awayWanderNoticeLogged;

        /// <summary>커서 좌표를 <b>실제로</b> 읽을 수 있는가 — G3(화면 쪽 돌아보기)의 추첨 자격이다.
        /// 읽기 전용 조회이며(<see cref="CursorProvider"/>는 StickmanAgent.TryGetCursorPosition),
        /// 실패하면 G3를 추첨에서 빼고 그 가중치를 G1에 합친다. 없는 대상을 향해 돌아보는 그림은
        /// 절대 불변 원칙 1 위반이다.</summary>
        private bool CanSeeCursor => CursorProvider != null && CursorProvider(out _);

        /// <summary>
        /// ★ G3(화면 쪽 돌아보기)를 <b>지금 추첨에 넣어도 되는가</b> — 2026-09-08.
        ///
        /// <para>조건이 <b>둘</b>이고 둘 다 «없는 것을 향한 자세»를 막는 같은 성격이다:</para>
        /// <list type="number">
        ///   <item><b>커서를 읽을 수 있는가</b>. 못 읽으면 돌아볼 대상이 없다(절대 불변 원칙 1).</item>
        ///   <item><b>몰입기가 아닌가</b>. 몰입기에는 프롭이 <b>진입 프레임 좌표에 고정</b>돼 있어서,
        ///     여기서 방향이 뒤집히면 <b>프롭이 「뒤」가 된다</b> — 뒤쪽으로 제한된 종이비행기 궤도가
        ///     그 순간 책상을 뚫고 지나간다(2026-09-08 coder 발견 → 리더 판정 (나)).
        ///     의미상으로도 맞다: 「몰입기」는 캐릭터가 작업에 빠져 있는 구간이라 <b>이미 배회 걷기가 0</b>이고,
        ///     그 구간에 「화면 쪽으로 돌아본다」가 도는 것 자체가 구간의 뜻과 어긋난다.</item>
        /// </list>
        ///
        /// <para>★ <b>각도만 죽여 어정쩡하게 남기지 않는다.</b> 그러면 「무언가 하려다 만 자세」가 생긴다 —
        /// 프롭이 없으면 코스튬 모션을 <b>통째로</b> 끄는(폴백 키포즈를 안 만드는) 그 원칙과 같은 형태로,
        /// 여기서는 <b>추첨에서 뺀다</b>. 빠진 가중치는 <c>FocusAmbientGestures.Draw</c>가 G1에 합치므로
        /// 가중치 합이 언제나 1이고 <b>분포가 조용히 찌그러지지 않는다</b> — 새 분기도, 새 어휘도 없다.</para>
        /// </summary>
        public bool ScreenGlanceAllowed => CanSeeCursor && !IsCostumeImmersionHolding;

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
                    // ★ 2026-09-06 — 여기가 **유일한 발행 창구**다. 집중 세션 중에는 쿨다운 값과
                    //   어휘만 바뀌고, 추첨권/지연/1회 계약은 한 줄도 바뀌지 않는다.
                    bool focus = IsFocusAmbientActive;
                    _lookAroundCooldownTimer = Mathf.Max(0f, focus
                        ? Cfg(c => c.focusAmbientGestureCooldownSeconds, 28f)
                        : Cfg(c => c.wanderLookAroundCooldownSeconds, 30f));
                    LookAroundRaisedCount++;
                    WanderAmbientMotion motion = focus
                        ? FocusAmbientGestures.Draw(_rng.NextDouble(), ScreenGlanceAllowed)
                        : WanderAmbientMotion.LookAround;
                    StickmanEventBus.RaiseWanderAmbientMotionRequested(motion);
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
            // ★ 2026-09-06 집중 세션 — 걷기 확률만 낮춘다(0.75 -> 0.40). **분포 변경이지 묶어두기가
            //   아니다**: 파쿠르·뛰어내리기·매달리기는 전부 걷기에서 갈라지므로 0으로 내리면 그
            //   연출들이 구조적으로 도달 불가가 된다(25분 중 3.2분은 여전히 걷는다).
            //   남은 확률은 여기서도 평소와 똑같이 "Idle 연장"이 흡수한다 — 갈래 구조 무변경.
            // ★ 2026-09-06 «메뉴 펼침 = 제자리 대기» — 집중 세션과 **완전히 같은 어법**이다:
            //   갈래 구조도 추첨도 그대로 두고 **읽는 확률만** 0으로 본다. 남은 확률은 여기서도
            //   평소와 똑같이 "Idle 연장"이 흡수하므로 새 분기도, 새 타이머도 늘지 않는다.
            // ★ 2026-09-07 자리 비움 — 위 둘과 **또 같은 어법**이다: 갈래도 추첨도 그대로 두고
            //   읽는 확률만 낮춘다(0.75 -> 0.15). 사다리 순서는 부채꼴 0 > 자리비움 0.15 >
            //   집중 0.40 > 평소 0.75로 **단조 내림차순**이라, 두 조건이 겹쳐도 "더 조용한 쪽"이
            //   자동으로 이긴다 — 우선순위를 따로 판정하는 코드가 필요 없다.
            //   ★ Idle 길이는 여기서도 안 건드린다(IsViewerAwayForWander 문서의 복귀 지연 항목).
            // ★ 2026-09-07 코스튬 몰입기 — 위 셋과 **또 같은 어법**이다: 갈래도 추첨도 그대로 두고
            //   읽는 확률만 낮춘다(기본 0). 사다리는 부채꼴 0 ≥ 몰입기 0 ≥ 자리비움 0.15 ≥
            //   집중 0.40 ≥ 평소 0.75로 여전히 **단조 내림차순**이라 우선순위 판정 코드가 필요 없다.
            //   ★ 여기만 0인 이유는 앞의 셋과 다르다: 저쪽은 「덜 돌아다닌다」이고 이쪽은
            //   **「고정된 프롭 곁을 떠나면 그림이 깨진다」**는 기하 제약이다.
            //   그래서 이 값을 올리면 파쿠르 도달성이 아니라 **연출 자체**가 손상된다
            //   (costumeImmersionWalkChance 툴팁에 되돌리는 조건을 적어 뒀다).
            //   ★ Idle 길이는 여기서도 안 건드린다(위 셋과 같다).
            bool hold = IsRadialMenuHolding;
            bool immersion = !hold && IsCostumeImmersionHolding;
            bool away = !hold && !immersion && IsViewerAwayForWander;
            float walkChance = hold ? 0f
                : immersion           ? Cfg(c => c.costumeImmersionWalkChance, 0f)
                : away                ? Cfg(c => c.awayWanderWalkChance,     0.15f)
                : IsFocusAmbientActive ? Cfg(c => c.focusSessionWalkChance,   0.4f)
                :                        Cfg(c => c.wanderPostIdleWalkChance, 0.75f);
            float jumpChance = hold ? 0f : Cfg(c => c.wanderPostIdleJumpChance, 0f);

            LogAwayWanderEdge(away, walkChance);

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
                // ★ 2026-09-06 — 집중 세션 중에는 이 갈래가 신호를 내지 않는다. 기지개(만세)는
                //   관망 자세와 정면으로 충돌하는 그림이고(팔짱을 낀 채 만세를 할 수는 없다),
                //   세션 중 어휘의 소유자는 위 «단 하나의 추첨»이다. 수신 측
                //   (StickmanBlackboard.BeginIdleAmbientMotion)도 같은 이유로 거르지만, 구조적으로
                //   언제나 거부될 신호를 발행하는 것 자체가 죽은 배관이라 여기서 멈춘다.
                _consecutiveIdleExtensions++;
                if (_consecutiveIdleExtensions >= 3 && !IsFocusAmbientActive)
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

        /// <summary>
        /// 자리 비움 감쇄가 <b>켜지는 그 한 번</b>만 로그를 남긴다.
        ///
        /// <para>왜 엣지인가: 이 추첨은 Idle 구간마다(실측 약 8초 주기) 돌고, 자리 비움은 하룻밤
        /// 이어진다 — 매번 찍으면 하룻밤에 수천 줄이고 그때마다 문자열 보간이 할당된다. 24시간 상주
        /// 앱에서 로그도 자원이다(FramePacing의 등급 전이 요약이 같은 이유로 요약형이다).</para>
        ///
        /// <para>꺼지는 엣지는 <b>일부러 조용하다</b>. 사용자가 돌아오면 화면에서 캐릭터가 다시 걷는
        /// 것이 보이므로 로그가 알려 줄 것이 없고, 자리 비움 체류 비율은 이미
        /// <c>[FramePacing/적응형]</c> 주기 요약의 "자리비움 %"가 말해 준다.</para>
        /// </summary>
        private void LogAwayWanderEdge(bool away, float walkChance)
        {
            if (away == _awayWanderNoticeLogged) return;
            _awayWanderNoticeLogged = away;
            if (!away) return;

            Debug.Log(
                $"[배회] 자리 비움 감지(무입력 {Platform.FramePacing.LastPresence.SecondsSinceUserInput:F0}초 " +
                $">= {Platform.FramePacingPolicy.AwaySeconds:F0}) — 걷기 확률 " +
                $"{Cfg(c => c.wanderPostIdleWalkChance, 0.75f):0.##} -> {walkChance:0.##}로 낮춥니다.\n" +
                "      Idle 길이·두리번 주기·경계 행동은 그대로이고, 입력이 들어오면 다음 추첨부터 " +
                "즉시 평소대로 돌아옵니다.");
        }

        // ==================== Moving (26-1, 26-2) ====================

        private void EnterMoving() => EnterMoving(0);

        /// <param name="forcedDirection">0이면 26-1대로 좌우 랜덤(경계 회피 포함). +1/-1이면 그 방향으로
        /// 강제한다 — 맨틀 직후 "올라선 턱 안쪽으로 걸어 들어가기"에만 쓴다.</param>
        private void EnterMoving(int forcedDirection)
        {
            // ★ 걷기로 들어오는 **모든 문**이 여기를 지난다 — 추첨(ResolvePostIdleBranch)뿐 아니라
            //   맨틀 직후 강제 진입(ConsumeClimbMantleSignalIfAny)도 마찬가지다. 그래서 조건을
            //   호출부마다 흩지 않고 이 한 자리에 둔다(호출부에 두면 다음에 생기는 세 번째 문이
            //   조용히 새어 나간다).
            if (IsRadialMenuHolding) { EnterResting(); return; }

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
            // ★ 2026-09-06 — 이미 걷고 있던 구간을 «정상 종료»시킨다. 새 정지 로직을 만들지 않고
            //   걷기 지속시간이 다 됐을 때와 **똑같은 문**(EnterResting)으로 나간다 — 그래서
            //   화면에서 보이는 것도 평소에 걷다 서는 그 그림 그대로다(IdleState.Enter가 잔여
            //   수평 속도를 지우는 것까지 동일). 뛰어내리기 확약은 이 구간과 함께 끝난다 —
            //   남기면 다음 구간이 경계에서 서지 않고 그대로 발을 뗀다.
            if (IsRadialMenuHolding)
            {
                _hopDownCommitted = false;
                EnterResting();
                return;
            }

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
            TickRopeEdgeDiagnostic();

            // ★★★ 목표 지향 등반 — 경계 판정보다 <b>먼저</b> 본다(필드 문단 참고).
            //   이겼으면 이번 틱의 이동을 이 목표가 가져간다(경계 행동과 섞이면 두 의도가 같은
            //   프레임에 나가 «어디로 가는지 모르는» 그림이 된다).
            if (TickClimbSeek(info)) return;

            if (info.Grounded && IsNearFootholdEdge(info, _direction, out bool isTrueScreenEdge, out float remainingToEdge))
            {
                // ★ 경계 행동 추첨 — 한 걷기 구간당 1회(위 _edgeActionRolledThisLeg 주석 참고). 아래 세
                // 갈래를 **낙차/높이로 먼저 가른 뒤** 그 갈래의 확률로만 추첨한다. 공통 전제 두 가지:
                //   (1) 화면 자체의 끝이 아니다(끝에서 바깥으로 나가면 몸이 화면 밖으로 나간다),
                //   (2) 실제로 갈 곳이 있다(내려앉을 발판 / 올라설 턱이 실존할 때만 추첨한다).
                // 추첨에 떨어지거나 조건이 안 맞으면 아래 기존 분기(점프 시도 / 정지 후 반대 방향)로
                // 그대로 흘러간다 — 즉 세 확률을 전부 0으로 두면 예전 거동과 100% 동일하다.
                // ★★★ 2026-09-08 — 전제 (1)이 <b>밧줄 등반에는 적용되지 않는다.</b>
                //   사용자 신고 2회: "윈도우에서는 밧줄 동작안함"(2회 연속, 상한을 화면 비례로
                //   바꾼 뒤에도 그대로였다).
                //
                //   ■ 원인 — Windows에는 <b>바닥에 내부 경계가 하나도 없다</b>
                //   macOS Dock은 화면 <b>가운데</b> 띠다(실측 x 254~1259 / 1512pt). 그래서 좌우로
                //   안전망 조각이 남고 <b>내부 경계가 2개</b> 생긴다 — 배회 AI가 그 자리에서 경계
                //   행동을 추첨한다. Windows 작업표시줄은 <b>화면 전폭</b>이라(GetMonitorInfo의
                //   rcMonitor/rcWork 차, Win32WindowService가 IReservedBottomBarService로 정확히
                //   준다) 안전망 조각이 폭 0으로 죽고, 그 발판의 좌우 끝이 <b>곧 화면 끝</b>이다.
                //   ⇒ isTrueScreenEdge가 항상 true → 이 게이트에서 <b>추첨 자체가 한 번도 안 돌았다</b>.
                //   FallbackPlatformWindowService.AppendBottomSafetyNet의 QA 시험벽 블록이 "Dock이
                //   없으면 가짜 경계를 직접 만들어야 한다 — 내부 경계가 없으면 배회 AI가 그 자리에
                //   도달하는 판정을 아예 하지 않으므로"라고 적어 둔 것이 정확히 이 사실이다.
                //
                //   ■ 왜 밧줄만 푸는가 — 전제 (1)의 근거가 밧줄에는 성립하지 않는다
                //   "끝에서 바깥으로 나가면 몸이 화면 밖으로 나간다"는 <b>내려가거나 뛰는</b> 갈래의
                //   이야기다. 밧줄은 <b>위로</b> 오르고, 목표 벽은 정의상 화면 안에 있으며
                //   (TryFindRopeClimbWallWide가 발판 목록에서만 고르고, 상한은
                //   ResolveRopeClimbMaxHeight가 화면 클램프 상단으로 자른다), 오른 뒤 올라서는
                //   자리도 그 벽의 상단이다. 즉 이 갈래로는 몸이 화면 밖으로 나갈 수 없다.
                //   하강 3갈래(뛰어내리기/매달리기/되올라가기)는 <b>그대로 막는다</b> —
                //   TryRollEdgeAction이 atTrueScreenEdge를 받아 자기 안에서 가른다.
                //
                //   ■ macOS 무회귀: macOS는 Dock 덕에 isTrueScreenEdge=false인 자리에서 이미
                //   추첨하고 있었고, 그 경로는 한 줄도 바뀌지 않는다. 늘어난 것은 "화면 끝에서도
                //   밧줄만 한 번 더 본다"뿐이다(실측 120초/회는 전부 내부 경계에서 나온 값이다).
                if (!_edgeActionRolledThisLeg)
                {
                    _edgeActionRolledThisLeg = true;
                    if (TryRollEdgeAction(info, isTrueScreenEdge)) return;
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
        /// <remarks>★ 2026-09-08 — <c>internal</c>에서 <c>public</c>으로. 이유는
        /// <see cref="ResolveStepUpMaxHeightStatic"/>·<see cref="ResolveRopeClimbMaxHeight"/>와 같다:
        /// <b>PlayMode 테스트 어셈블리는 InternalsVisibleTo 대상이 아니다.</b>
        /// 「화면 전폭 발판에서도 밧줄은 평가되고 뛰어내리기는 여전히 막힌다」가 이 함수의
        /// <c>isTrueScreenEdge</c>를 <b>전제 검증</b>으로 직접 읽는다 — 그 전제가 거짓이면 그 테스트의
        /// 초록은 이 결함과 무관한 다른 것을 재고 있다는 뜻이라, 읽을 수 있어야 한다.</remarks>
        public static float ResolveEffectiveEdgeBoundary(float footholdBoundaryX, float unionBoundaryX,
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
        /// <summary>
        /// ★★★ 목표 지향 등반 — «주기마다 화면을 훑어 벽을 고르고, 그 앞까지 걸어가 던진다».
        /// <para>반환 true = 이번 틱의 이동 의도를 이 함수가 확정했으니 호출부는 즉시 return한다.</para>
        ///
        /// <para>3단계다:
        /// <list type="number">
        ///   <item><b>고르기</b> — 주기가 차면 화면 전체 발판에서 대역에 드는 벽을 하나 고르고
        ///     <c>ropeClimbChance</c>로 추첨한다. 지면 아무 일도 없고 다음 주기를 기다린다.</item>
        ///   <item><b>걸어가기</b> — 그 벽의 «내 쪽 세로 모서리» 앞까지 이동한다. 이 동안 경계 행동은
        ///     평가하지 않는다(호출부가 즉시 return하므로). 오래 못 가면 포기한다 — 창이 화면 밖으로
        ///     밀려났거나 사이에 다른 발판이 생겨 길이 막혔을 수 있고, 그때 영원히 매달리면
        ///     배회 자체가 멈춘다.</item>
        ///   <item><b>던지기</b> — 도착하면 대역에 따라 밧줄/손 등반 펄스를 낸다. 도착 판정과
        ///     소비자(WalkState)의 재확인은 <b>같은 메서드</b>를 쓴다
        ///     (<see cref="StickmanBlackboard.TryVerifyClimbTargetAtBody"/>).</item>
        /// </list></para>
        /// </summary>
        private bool TickClimbSeek(GroundSensor.GroundInfo info)
        {
            if (_blackboard == null || !info.Grounded)
            {
                // 공중에 있는 동안은 목표를 유지할 이유가 없다(등반 중이거나 떨어지는 중이다).
                _climbTargetActive = false;
                return false;
            }

            float stepUpMax = ResolveStepUpMaxHeight();
            float ropeMax = ResolveRopeClimbMaxHeight(_blackboard, info.GroundWorldY);
            float minClimb = Cfg(c => c.parkourDetectionRadius, 0.5f);

            // ---- (2)(3) 진행 중인 목표가 있으면 그쪽이 우선이다 ----
            if (_climbTargetActive)
            {
                float bandLow = _climbTargetIsRope ? stepUpMax : minClimb;
                float bandHigh = _climbTargetIsRope ? ropeMax : stepUpMax;

                if (_blackboard.TryVerifyClimbTargetAtBody(info, _climbTargetDirection, _climbTargetHandle,
                        bandLow, bandHigh, out float verifiedTopY))
                {
                    // 도착 — 펄스를 낸다. 방향은 목표를 바라보는 쪽으로 확정한다.
                    _direction = _climbTargetDirection;
                    _moveInputX = _climbTargetDirection;
                    if (_climbTargetIsRope) _ropeClimbRequestedThisTick = true;
                    else _stepUpRequestedThisTick = true;
                    _climbSeekWon++;

                    Debug.Log($"[등반목표] 도착 — {(_climbTargetIsRope ? "밧줄" : "손 등반")}, 벽핸들={_climbTargetHandle}, " +
                        $"높이={(verifiedTopY - info.GroundWorldY):F3}유닛, 방향={(_climbTargetDirection > 0 ? "오른쪽" : "왼쪽")}. " +
                        "목표창 아래까지 걸어와 지금 던집니다(2026-09-08 사용자 지시 '목표창 아래로 가서 던짐').");

                    _climbTargetActive = false;
                    return true;
                }

                // 아직 도착 전 — 목표 쪽으로 걷는다.
                float bodyX = _blackboard.Body != null ? _blackboard.Body.position.x : 0f;
                int toward = _climbTargetApproachX >= bodyX ? 1 : -1;
                _direction = toward;
                _moveInputX = toward;

                _climbTargetGiveUpTimer += Time.deltaTime;
                float giveUp = Mathf.Max(5f, Cfg(c => c.climbSeekIntervalSeconds, 100f) * 0.5f);
                if (_climbTargetGiveUpTimer > giveUp)
                {
                    Debug.Log($"[등반목표] 포기 — 벽핸들={_climbTargetHandle}까지 {giveUp:F0}초 안에 도달하지 못했습니다. " +
                        "창이 화면 밖으로 밀렸거나 길이 막혔을 수 있습니다. 배회를 계속하고 다음 주기에 다시 고릅니다.");
                    _climbTargetActive = false;
                    return false;
                }
                return true;
            }

            // ---- (1) 주기가 차면 고른다 ----
            _climbSeekTimer += Time.deltaTime;
            float interval = Mathf.Max(1f, Cfg(c => c.climbSeekIntervalSeconds, 100f));
            if (_climbSeekTimer < interval) return false;
            _climbSeekTimer = 0f;

            float chance = RopeClimbQaOverride.ChanceOverride ?? Cfg(c => c.ropeClimbChance, 0.85f);
            if (!(chance > 0f)) return false;

            // 손 등반 대역과 밧줄 대역을 <b>한 번에</b> 훑는다 — 사용자 신고가 둘 다였고
            // (밧줄 x2 · 파쿠르 x1), 원인이 하나라 처방도 하나여야 한다.
            if (!_blackboard.TryPickClimbTarget(info, minClimb, ropeMax,
                    out long handle, out float topY, out float approachX, out int approachDir))
            {
                _climbSeekMissNoWall++;
                return false;
            }

            if (!(_rng.NextDouble() < chance)) { _climbSeekLostRoll++; return false; }

            float height = topY - info.GroundWorldY;
            _climbTargetActive = true;
            _climbTargetHandle = handle;
            _climbTargetApproachX = approachX;
            _climbTargetDirection = approachDir;
            _climbTargetIsRope = height > stepUpMax;
            _climbTargetGiveUpTimer = 0f;

            Debug.Log($"[등반목표] 선정 — {(_climbTargetIsRope ? "밧줄" : "손 등반")} 대역, 벽핸들={handle}, " +
                $"높이={height:F3}유닛(손 등반 상한 {stepUpMax:F2} / 밧줄 상한 {ropeMax:F2}), " +
                $"접근점 x={approachX:F3}, 바라볼 방향={(approachDir > 0 ? "오른쪽" : "왼쪽")}. " +
                "화면 전체를 훑어 골랐습니다 — 발판 경계에 도달할 필요가 없으므로 작업표시줄처럼 " +
                "화면 전폭인 바닥에서도 동작합니다.");
            return true;
        }

        /// <summary>
        /// ★ 밧줄 경계 진단 한 줄 — <b>양쪽 플랫폼에서 같은 코드로 남는다</b>(필드 문단 참고).
        /// <para>읽는 법 — 이 줄 하나로 "왜 밧줄이 안 나오는가"의 갈래가 전부 갈린다:</para>
        /// <list type="bullet">
        ///   <item><c>경계=0</c> — 애초에 경계 근처에 <b>도달하지 못했다</b>. 발판 구성 문제다.</item>
        ///   <item><c>경계&gt;0 인데 화면끝=경계</c> — 만난 경계가 <b>전부 화면 자체의 끝</b>이다.
        ///     Windows 작업표시줄처럼 <b>전폭 발판</b>이면 이렇게 된다(내부 경계가 0개).
        ///     2026-09-08 이전에는 이 상태에서 추첨이 <b>한 번도 돌지 않았다</b>.</item>
        ///   <item><c>벽발견=0</c> — 경계엔 갔는데 <b>대역에 드는 벽이 없다</b>. 함께 찍는 대역
        ///     [하한~상한]과 화면의 창 높이를 비교하면 바로 판정된다(지형 문제).</item>
        ///   <item><c>벽발견&gt;0, 성공=0</c> — 벽은 있는데 <b>확률에서 계속 졌다</b>(빈도 문제).</item>
        /// </list>
        /// </summary>
        private void TickRopeEdgeDiagnostic()
        {
            _ropeEdgeDiagTimer += Time.deltaTime;
            if (_ropeEdgeDiagTimer < RopeEdgeDiagnosticIntervalSeconds) return;
            _ropeEdgeDiagTimer = 0f;

            // 아무 일도 없었으면 조용히 있는다 — 상주 앱에서 «변화 없음»을 매 분 찍지 않는다.
            if (_ropeEdgeEncounters == 0 && _climbSeekWon == 0 && _climbSeekMissNoWall == 0 && _climbSeekLostRoll == 0) return;

            Debug.Log($"[밧줄진단] 최근 {RopeEdgeDiagnosticIntervalSeconds:F0}초 — 경계 도달 {_ropeEdgeEncounters}회 " +
                $"(그중 화면끝 {_ropeEdgeTrueScreenEdges}회) / 대역 벽 발견 {_ropeEdgeWallFound}회 / 추첨 성공 {_ropeEdgeRollWon}회. " +
                $"대역=[{_ropeEdgeLastBandLow:F2} ~ {_ropeEdgeLastBandHigh:F2}]유닛(파쿠르 상한 초과 ~ 밧줄 상한) " +
                $"| 목표지향: 던짐 {_climbSeekWon}회 / 벽없음 {_climbSeekMissNoWall}회 / 추첨패 {_climbSeekLostRoll}회. " +
                "★ 화면끝이 경계 도달과 같으면 이 화면에는 «내부 경계»가 없다는 뜻입니다(예: Windows 작업표시줄처럼 " +
                "발판이 화면 전폭). 벽 발견이 0이면 확률이 아니라 지형 문제입니다 — 대역에 드는 높이의 창이 " +
                "화면에 하나도 없습니다.");

            _ropeEdgeEncounters = 0;
            _ropeEdgeTrueScreenEdges = 0;
            _ropeEdgeWallFound = 0;
            _ropeEdgeRollWon = 0;
            _climbSeekWon = 0;
            _climbSeekMissNoWall = 0;
            _climbSeekLostRoll = 0;
        }

        /// <param name="atTrueScreenEdge">지금 경계가 <b>화면 자체의 끝</b>인가. true면 하강 3갈래
        /// (뛰어내리기·매달리기·되올라가기)는 <b>전부 건너뛴다</b> — 그쪽으로 가면 몸이 화면 밖으로
        /// 나간다. <b>밧줄 등반만</b> 그대로 평가한다(위로 오르고 목표 벽은 정의상 화면 안이라
        /// 그 근거가 성립하지 않는다. 호출부의 2026-09-08 문단 참고).</param>
        private bool TryRollEdgeAction(GroundSensor.GroundInfo info, bool atTrueScreenEdge)
        {
            // ★ 되올라간 직후 유예 구간(2026-08-29) — 내려가는 두 갈래(1·2)만 건너뛰고 되올라가기(3)와
            // 기존 배회 거동(정지 후 반대 방향)은 그대로 둔다. 이 구간에서도 경계에서 "돌아서기"는
            // 정상 동작하므로 화면 밖으로 걸어 나가지 않는다(ConsumeClimbMantleSignalIfAny 문서 참고).
            bool descendSuppressed = _descendSuppressTimer > 0f;

            float maxHeight = ResolveStepUpMaxHeight();
            float ropeMaxHeight = ResolveRopeClimbMaxHeight(_blackboard, info.GroundWorldY);

            // ============================================================================
            // ★★★ 근본 재설계(2026-09-07 3차, §10 "독립 우선순위") —
            // docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md §1-C/§9 참고.
            // ============================================================================
            // 리더가 실기(macOS)에서 §9-4-A(확률 0→0.20)+§9-4-B(hop-down 좁은 보강) 적용 후에도 장시간
            // 관찰한 결과 자연발동이 전혀 없었다 — 캐릭터가 Dock↔안전망을 무한 왕복(뛰어내리기<->되올라
            // 가기)했을 뿐, 로프 등반은 단 한 번도 평가되지 않았다(발판핸들 순환 로그로 확정). 재조사
            // 결과 §1-C가 우려한 "내려갈곳없음 ∩ 초고벽" 곱셈 압축은 이 사고의 주 원인이 **아니었다**
            // (§9-2 실측 논증: TryFindHopDownTarget(아래)과 TryFindClimbableWall(위)은 같은 방향·같은
            // 경계에서 물리적으로 상호배타적이라 "내려갈 곳이 있어서 로프 평가가 막히는" 상황 자체가
            // 드물다) — 진짜 원인은 그 경계의 벽 높이 자체가 로프 대역에 들지 못했다는 것(파쿠르 상한
            // 아래에 고정)과, 대역에 드는 벽이 다른 곳(Finder 창)에 있어도 좁은 탐색 폭으로는 못
            // 찾는다는 것(TryFindRopeClimbWallWide 문서 참고, §9-4-D) 두 가지였다.
            //
            // 그럼에도 사용자가 "독립 우선순위 승격 근본재설계"를 명시적으로 재지시했다 — 재검토 결과
            // §1-C의 "내려갈 수 있는데도 로프를 던지는 부자연스러움" 우려는 아래 설계로 완화된다:
            //   - 로프 벽 탐색+추첨을 hop-down/hang **앞에서, 독립적으로** 한 번 시도한다("내려갈 곳
            //     없음" 전제를 완전히 뗀다 — 내려갈 곳 존재 여부와 무관하게 매 다리(leg)마다 로프부터
            //     본다).
            //   - 다만 ropeClimbChance 자체가 낮은 확률(기본 0.20)로 남아 있어, 내려갈 곳이 있는
            //     경계에서도 80%는 여전히 기존 하강 갈래로 그대로 흘러간다 — "가끔 이기는" 정도로
            //     그친다(사용자 제안 "동시에 굴려서 확률적으로 등반도 가끔 이기게 한다"와 같은 효과를,
            //     정규화 가중치 대신 "먼저 한 번 굴려보고 지면 기존 체인으로" 방식으로 더 단순하게
            //     구현했다 — 가중치 정규화(rope와 hop의 확률을 더해 비율로 나누는 방식)는 검토했으나
            //     hop 자신의 체감 확률까지 h²/(h+r) 형태로 줄어드는 부작용이 있어 채택하지 않았다.
            //     이 "선-로프-후-체인" 방식은 로프 후보가 없으면 완전한 no-op이라 hop/hang의 체감
            //     빈도가 100% 그대로 보존된다).
            //   - stepUpChance 게이트에서도 뗐다 — 예전 코드는 로프 추첨이 되올라가기 블록의 else
            //     안에 있어 "stepUpChance(0.85) 통과 → 벽이 로프 대역 → ropeClimbChance(0.20) 통과"
            //     라는 원치 않은 결합이 있었다(§9-3 실측: 실효 확률 = stepUpChance × ropeClimbChance,
            //     주석은 "별도 추첨"이라 적어 놓고 실제로는 안 그랬다). 지금은 로프 자신의 확률 하나만
            //     본다.
            //
            // "진동"(§1-C) 우려 검산 — 이 재설계가 새로 여는 반복 패턴이 없는 이유:
            //   (a) 로프로 올라간 직후 곧바로 되내려가는 것 — postClimbDescendCooldown(3-C, 기존
            //       ReportClimbMantleCompleted/ConsumeClimbMantleSignalIfAny 재사용)이 이미 차단한다.
            //       이 재설계는 그 신호 경로를 한 줄도 바꾸지 않았다.
            //   (b) 같은 다리(leg)에서 로프를 여러 번 재시도 — 이 함수를 감싸는 TickMoving 쪽 게이트
            //       (_edgeActionRolledThisLeg)가 한 다리당 이 함수 호출 자체를 1회로 제한한다. 로프가
            //       그 1회 시도에서 지면(기본 80%) 하강 체인으로 흘러가고, 다음 시도는 다음 다리
            //       (경계 반전 0.3~0.8초 대기 후)에서만 온다 — 기존 hop/hang/stepUp과 완전히 같은
            //       재시도 주기라 새로운 "빠른 반복" 경로가 아니다.
            //   → 두 안전장치 모두 이 라운드가 손대지 않았으므로 기존 검증(EdgeHopDownTests 등)이
            //     그대로 유효하다. (a)는 RopeClimbState.Tick()의 ReportClimbMantleCompleted 호출을
            //     직접 재확인했다(ParkourClimbState.Tick()의 동일 호출과 같은 형태) — 로프도 손 등반과
            //     똑같이 이 신호를 올린다. 새 쿨다운을 별도로 추가하지 않은 것은 누락이 아니라 "이미 충분하다"는
            //     검산 결과다(불필요한 상태를 늘리지 않는다 — RopeClimbIndependentPriorityTests가 이
            //     결론을 실측으로 잠근다).
            //
            // §9-4-B(hop-down 원천봉쇄 좁은 보강)는 이 재설계로 **대체돼 제거됐다** — 로프가 이제
            // hop-down보다 먼저, 독립적으로 평가되므로 "hop-down이 로프 평가를 원천봉쇄"할 여지 자체가
            // 없다(로프가 이겼으면 이 함수가 이미 return true했고, 로프가 없거나 졌으면 그때 비로소
            // hop-down 차례다). 죽은 이중 탐색(TryFindClimbableWall 프레임당 최대 2회 호출) 비용도
            // 함께 없어졌다.
            // ============================================================================
            // ★ 진단 계기(2026-09-08) — 판정 «전에» 표본을 센다. 필드 문단 참고.
            _ropeEdgeEncounters++;
            if (atTrueScreenEdge) _ropeEdgeTrueScreenEdges++;
            _ropeEdgeLastBandLow = maxHeight;
            _ropeEdgeLastBandHigh = ropeMaxHeight;

            float ropeClimbChance = RopeClimbQaOverride.ChanceOverride ?? Cfg(c => c.ropeClimbChance, 0.85f);
            // out 변수를 <b>먼저</b> 선언한다 — `A && TryFind(out x)` 형태로 쓰면 A가 단락될 때
            // x가 확정 할당되지 않아 뒤에서 못 읽는다(C# 확정 할당 규칙).
            long ropeWallHandle = 0L;
            float ropeWallTopY = 0f;
            bool ropeWallFound = ropeClimbChance > 0f
                && _blackboard.TryFindRopeClimbWallWide(info, _direction, out ropeWallHandle, out ropeWallTopY, maxHeight, ropeMaxHeight);
            if (ropeWallFound) _ropeEdgeWallFound++;
            if (ropeWallFound && _rng.NextDouble() < ropeClimbChance)
            {
                _ropeEdgeRollWon++;
                _ropeClimbRequestedThisTick = true;
                _moveInputX = _direction;
                Debug.Log($"[밧줄등반] 결정(독립 우선순위, 근본재설계 §10) — 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}, " +
                    $"벽 높이={(ropeWallTopY - info.GroundWorldY):F3}유닛(파쿠르 상한 {maxHeight:F2} 초과, 밧줄 상한 " +
                    $"{ropeMaxHeight:F2}), 벽 발판핸들={ropeWallHandle}. 내려갈 곳 존재 여부와 무관하게 평가됨.");
                return true;
            }

            // ---- 이하 하강/되올라가기 체인 — 로프가 후보 없음 또는 이번엔 추첨에서 졌을 때만 도달한다 ----

            // ★★ 2026-09-08 — 화면 자체의 끝이면 여기서 끝낸다. 이 세 갈래는 전부 «경계 바깥/아래로
            //   몸을 옮기는» 행동이라, 화면 끝에서 하면 몸이 화면 밖으로 나간다(호출부가 예전에
            //   isTrueScreenEdge로 통째로 막던 그 근거 그대로다 — 없어진 것이 아니라 <b>여기로
            //   내려왔다</b>. 위 밧줄 블록만 그 근거가 성립하지 않아 앞에 두었다).
            //   ⇒ 반환 false: 호출부의 기존 분기(정지 후 반대 방향)로 그대로 흘러간다.
            if (atTrueScreenEdge) return false;

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

            // 3) 되올라가기 — 손 등반 대역(파쿠르 상한 이내)의 턱이 있을 때. ★ 이 분기가 없으면 한 번
            // Dock 아래로 내려간 캐릭터가 영영 못 올라온다(경계 점프 확률이 기본 0이라 ParkourClimb를
            // 유발할 다른 경로가 없다) — 2026-08-29 사용자 지시의 핵심 절반이다.
            //
            // ★ 근본재설계(2026-09-07 3차, §10) — 로프 등반은 더 이상 이 블록의 else가 아니다(위에서
            // 이미 독립적으로 한 번 평가됐다). 그래서 이 블록은 로프 등반이 생기기 이전의 원래 형태
            // (파쿠르 상한 이내면 되올라가기, 아니면 아무 것도 안 함)로 돌아간다 — else 분기가 사라져
            // wallHeight > maxHeight인 경우 이 함수는 조용히 아래 return false로 흘러가고, 호출부
            // (TickMoving)가 기존처럼 "정지 후 반대 방향"을 진행한다.
            float stepUpChance = Cfg(c => c.stepUpChance, 0.5f);
            if (stepUpChance > 0f && _rng.NextDouble() < stepUpChance
                && _blackboard.TryFindClimbableWall(info, _direction, out long wallHandle, out float wallTopY))
            {
                float wallHeight = wallTopY - info.GroundWorldY;
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
            float resolved = ResolveStepUpMaxHeightStatic(_blackboard);

            // 설정값만으로는 못 올라오는 환경이라는 사실 자체를 한 번은 남긴다 — 이 로그가 뜬다는 것은
            // "이 사용자의 Dock에서는 stepUpMaxHeights 설정값이 무의미하다"는 뜻이고, 위 유도가 없었다면
            // 그대로 갇혔을 환경이라는 뜻이다.
            float configured = _blackboard != null && _blackboard.Config != null
                ? _blackboard.Config.ResolveStepUpMaxHeightWorld(_blackboard.CharacterHeightWorld)
                : StickConfig.BaselineCharacterTotalHeight * 1.0551f;
            if (resolved > configured && !_loggedDockDropExceedsConfiguredStepUp)
            {
                _loggedDockDropExceedsConfiguredStepUp = true;
                Debug.LogWarning($"[되올라가기] 실측 Dock 낙차가 stepUpMaxHeights 환산값 " +
                    $"{configured:F3}을 넘습니다(Dock 아이콘이 큰 설정). 상한을 {resolved:F3}유닛으로 올려 " +
                    "되올라가기를 유지합니다 — 이 유도가 없으면 한 번 내려간 캐릭터가 영영 못 올라옵니다.");
            }
            return resolved;
        }

        private bool _loggedDockDropExceedsConfiguredStepUp;

        /// <summary>
        /// ★ 2026-09-07 — 위 <see cref="ResolveStepUpMaxHeight"/>(인스턴스, 1회성 경고 로그 포함)에서
        /// <b>순수 계산만</b> 뽑아낸 정적 핵심(docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md 6-B).
        ///
        /// 왜 뽑아냈나: WalkState가 손 등반/밧줄 등반/포기 3구간을 가르려면 <b>이 컨트롤러가 트리거
        /// 판정에 쓰는 것과 정확히 같은 파쿠르 상한</b>을 소비 시점에도 써야 한다("판정을 쓰는 쪽이
        /// 그 값을 실제로 보는가" — 이 저장소가 이미 여러 번 겪은 "같은 값의 두 번째 계산원" 함정,
        /// <c>ResolveEffectiveEdgeBoundary</c> 사고와 같은 계열). WalkState는 이 컨트롤러의 인스턴스를
        /// 모르므로(IMovementIntentSource로만 참조) 정적 메서드로 공유한다. 1회성 경고 로그는 컨트롤러
        /// 인스턴스 상태(<see cref="_loggedDockDropExceedsConfiguredStepUp"/>)라 이 정적 버전에는
        /// 없다 — WalkState가 매 프레임 호출해도 로그가 늘지 않는다.
        ///
        /// <para>★ <c>public</c>인 이유: PlayMode 테스트 어셈블리(<c>StickMate.Tests.PlayMode</c>)는
        /// <c>InternalsVisibleTo</c> 대상이 아니다(<c>Scripts/AssemblyInfo.cs</c>는 EditMode만 허용 —
        /// ArcheryRenderer.SolveGravity 등과 같은 이유). 부작용이 전혀 없는 순수 함수라 노출해도
        /// 위험이 없다.</para>
        /// </summary>
        public static float ResolveStepUpMaxHeightStatic(StickmanBlackboard blackboard)
        {
            float configured = blackboard != null && blackboard.Config != null
                ? blackboard.Config.ResolveStepUpMaxHeightWorld(blackboard.CharacterHeightWorld)
                : StickConfig.BaselineCharacterTotalHeight * 1.0551f;
            if (!TryMeasureDockDropWorldUnits(blackboard, out float dockDrop)) return configured;
            return DockGeometry.ResolveStepUpMaxHeight(configured, dockDrop);
        }

        /// <summary>
        /// ★ 밧줄 등반 상한(월드 유닛, 2026-09-07 — docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md 1-B) —
        /// <c>min(ropeClimbMaxHeights × H, 화면 클램프 상단 − 시작 Y)</c> 중 더 작은 쪽.
        ///
        /// 화면 클램프 상단은 새로 계산하지 않는다 — <see cref="StickmanBlackboard.TryGetWalkableScreenTopWorldY"/>
        /// (StickmanBlackboard.ComputeScreenClampOsBounds의 <c>MinY</c>를 그대로 읽는 조회 하나)를
        /// 쓴다. 그 값에는 이미 화면 여유(ScreenClampMarginOsPx)가 포함돼 있으므로 여기서 별도
        /// 여유를 더 빼지 않는다 — 두 번 빼면 상한이 필요 이상으로 좁아진다.
        ///
        /// <paramref name="startWorldY"/>는 등반이 <b>시작되는</b> Y(지금 캐릭터가 서 있는 발판의
        /// 지면 Y)다 — AutoWanderController는 <c>info.GroundWorldY</c>를, WalkState도 같은 값을 넘긴다.
        ///
        /// 화면 클램프를 구할 수 없으면(카메라/몸 미배선) 설정값만으로 판정한다 — 클램프가 없다고
        /// 등반 자체를 막을 이유는 없다(TryGetWalkableScreenBoundsWorld가 실패할 때 원시 경계로
        /// 되돌아가는 것과 같은 폴백 어법).
        ///
        /// <para>★ <c>public</c>인 이유는 <see cref="ResolveStepUpMaxHeightStatic"/>과 같다 —
        /// PlayMode 테스트 어셈블리는 InternalsVisibleTo 대상이 아니다.</para>
        /// </summary>
        public static float ResolveRopeClimbMaxHeight(StickmanBlackboard blackboard, float startWorldY)
        {
            float h = blackboard != null ? blackboard.CharacterHeightWorld : StickConfig.BaselineCharacterTotalHeight;
            float configuredHeights = blackboard != null && blackboard.Config != null
                ? blackboard.Config.ropeClimbMaxHeights : 60f;
            // 「몇 초까지」 천장 — 이 값 ÷ ropeClimbSpeedHeightsPerSecond 가 곧 최대 등반 소요다.
            float byDuration = Mathf.Max(0f, configuredHeights) * h;

            // ★★★ 2026-09-08 — 주 상한을 「신장 배수 고정값」에서 「화면 도달범위 비례」로 옮겼다.
            //   사용자 지시: "6.6배는 너무 작은거 같아 화면자체가 캐릭터보다 많이 큰데 6.6배면 너무
            //   작은거 같은데 화면 해상도에 따라 계산해서 나눠야할거같은데".
            //
            //   실측이 그 지적을 뒷받침한다(배포 기본 배율, macOS 1512x982pt):
            //     화면 높이 23.99유닛 = 13.0H / 발밑(Dock)→화면상단 도달범위 22.16유닛 = 12.1H
            //     옛 상한 6.6H = 12.14유닛 → 도달범위의 **55%**밖에 못 덮었다.
            //   화면이 커질수록 도달범위(H 단위)만 늘고 고정 배수는 그대로라 커버율이 더 떨어진다 —
            //   해상도 의존이 **잘못된 방향으로** 걸려 있었다. 비율로 잡으면 어느 해상도에서나 같다.
            //
            //   ★ 도달범위를 못 구하면(카메라/몸 미배선) 소요시간 천장만으로 판정한다 — 클램프가
            //     없다고 등반 자체를 막을 이유는 없다(아래 기존 폴백 어법 그대로 유지).
            float resolved = byDuration;
            if (blackboard != null && blackboard.TryGetWalkableScreenTopWorldY(out float screenTopWorldY))
            {
                float reach = screenTopWorldY - startWorldY;
                float fraction = blackboard.Config != null ? blackboard.Config.ropeClimbMaxScreenFraction : 0.90f;
                float byScreen = reach * Mathf.Clamp01(fraction);
                resolved = Mathf.Min(byDuration, byScreen);
            }

            return Mathf.Max(0f, resolved);
        }

        /// <summary>Dock 발판 상단 − 바닥 안전망 상단 = 지금 이 화면의 진짜 낙차(월드 유닛).
        /// 핸들의 의미와 이 측정을 여기 둔 이유는 Core/DockGeometry.cs 하단 주석 참고.
        /// ★ 2026-09-07 — 정적으로 뽑혔다(<see cref="ResolveStepUpMaxHeightStatic"/> 참고). 인스턴스
        /// 상태를 하나도 쓰지 않으므로(블랙보드 조회뿐) 정적 전환에 거동 변화가 없다.</summary>
        private static bool TryMeasureDockDropWorldUnits(StickmanBlackboard blackboard, out float dropWorldUnits)
        {
            dropWorldUnits = 0f;
            if (blackboard == null) return false;

            if (!blackboard.TryGetFootholdTopWorldY(
                    StickMate.Platform.FallbackPlatformWindowService.DockFootholdHandle, out float dockTopY)) return false;

            // 안전망은 Dock 좌우로 잘린 두 조각이고 둘의 상단 Y는 같은 단일 소스에서 나오므로 어느 쪽을
            // 재도 같다. 한쪽 조각이 폭 0으로 죽어 있는 배치(Dock이 화면 끝까지 넓은 경우)를 위해 둘 다 본다.
            float netTopY = 0f;
            if (!blackboard.TryGetFootholdTopWorldY(
                    StickMate.Platform.FallbackPlatformWindowService.SyntheticFootholdHandle, out netTopY)
                && !blackboard.TryGetFootholdTopWorldY(
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
