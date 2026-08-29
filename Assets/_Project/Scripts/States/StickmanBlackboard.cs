using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.States
{
    /// <summary>
    /// Idle/Walk/Jump/Fall 등 능동 상태들이 공유하는 실행 컨텍스트("블랙보드").
    ///
    /// 왜 필요한가: IStickmanState 인터페이스는 Phase 0에서 확정된 계약(Enter/Tick/Exit)이라 시그니처를
    /// 바꿀 수 없다(팀 컨벤션, Coder 작업 지침). 상태별로 Rigidbody2D/카메라/설정/발판 폴러/상태머신을
    /// 전부 개별 생성자 인자로 늘어놓는 대신, 이 블랙보드 하나만 주입받게 해 향후 필드가 늘어나도
    /// IStickmanState 계약이나 각 상태 생성자 시그니처를 다시 건드릴 필요가 없게 한다.
    ///
    /// 이동 의도(MoveInputX/JumpPressed)는 더 이상 UnityEngine.Input을 직접 읽지 않는다(BUG-P1-B2 대응) —
    /// StickmanAgent(Core)가 매 프레임 IntentSource(IMovementIntentSource, 현재는 AutoWanderController)의
    /// Tick()만 갱신해주고, 아래 두 프로퍼티는 그 소스를 그대로 읽어 계산된다. 여러 상태가 각자 입력을
    /// 폴링하는 중복은 여전히 없다.
    /// </summary>
    public sealed class StickmanBlackboard
    {
        public Rigidbody2D Body;
        public Camera MainCamera;
        public StickConfig Config;
        public StickmanStateMachine Machine;
        public FootholdPoller FootholdPoller;

        /// <summary>
        /// StickmanAgent.ReportExternalImpact()가 RAGDOLL로 강제 전이시키기 직전에 기록하는 충격량
        /// 스냅샷(UX_FLOW.md 31-2 #2 "Ragdoll impactMagnitude" 파라미터용). RagdollState.Enter()가
        /// 이 값을 StickConfig.ragdollForceThreshold로 나눈 배율을 IHasDialogueParams로 노출해,
        /// 대사 강도와 실제 충격 강도가 항상 같은 파라미터에서 파생되도록 한다(31-1 원칙).
        /// </summary>
        public float LastImpactMagnitude;

        /// <summary>
        /// 이동 의도의 유일한 출처(BUG-P1-B2 대응, docs/BUG_REPORT_PHASE1.md Blocker). 예전에는
        /// StickmanAgent.Update()가 UnityEngine.Input.GetAxisRaw/GetButtonDown을 직접 읽어
        /// MoveInputX/JumpPressed 필드에 대입했지만, 키보드 의존은 실제 분리 오버레이(WS_EX_NOACTIVATE)가
        /// 완성되는 순간 영구 정지가 확정되는 구조적 결함이었다(가설 H6). 지금은 이 인터페이스(현재는
        /// docs/UX_FLOW.md 26절 배회 행동 스펙을 구현한 AutoWanderController)를 통해서만 아래 두 프로퍼티가
        /// 계산되며, StickmanAgent/State들은 그 출처가 키보드인지 AI인지 전혀 모른다.
        /// </summary>
        public IMovementIntentSource IntentSource;

        /// <summary>-1(왼쪽)~1(오른쪽). IntentSource에서 매 프레임 조회(더 이상 필드로 직접 대입되지 않음).</summary>
        public float MoveInputX => IntentSource != null ? IntentSource.MoveInputX : 0f;

        /// <summary>이번 프레임에 점프가 요청되었는지. IntentSource에서 매 프레임 조회.</summary>
        public bool JumpPressed => IntentSource != null && IntentSource.JumpRequested;

        /// <summary>이번 프레임에 "모서리를 붙잡고 매달려 내려가기"가 요청되었는지(JumpPressed와 동일한
        /// 1프레임 펄스 계약). IntentSource에서 매 프레임 조회 — WalkState가 소비한다.</summary>
        public bool LedgeHangPressed => IntentSource != null && IntentSource.LedgeHangRequested;

        /// <summary>이번 프레임에 "낙차가 작은 턱에서 그냥 앞으로 뛰어내리기"가 요청되었는지(위 두 펄스와
        /// 동일한 1프레임 계약). IntentSource에서 매 프레임 조회 — WalkState가 소비한다.</summary>
        public bool HopDownPressed => IntentSource != null && IntentSource.HopDownRequested;

        /// <summary>이번 프레임에 "낮은 턱을 기어올라 되돌아가기"가 요청되었는지(동일한 1프레임 계약).
        /// IntentSource에서 매 프레임 조회 — WalkState가 소비해 ParkourClimb로 보낸다.</summary>
        public bool StepUpPressed => IntentSource != null && IntentSource.StepUpRequested;

        /// <summary>
        /// StickmanAgent.TryGetCursorPosition과 동일한 시그니처(CursorPositionQuery, UX_FLOW.md 9절-3
        /// 전역 커서 폴링 채널 재사용) — 드래그&던지기(DragThrowState)/로데오 커서(RodeoCursorState)가
        /// 커서 월드 좌표를 조회하기 위해 사용한다(Phase 3). AutoWanderController.CursorProvider(26-4 훅)와는
        /// 별개의 델리게이트 인스턴스지만 둘 다 같은 StickmanAgent.TryGetCursorPosition 메서드 그룹을 가리킨다.
        /// </summary>
        public CursorPositionQuery CursorProvider;

        /// <summary>
        /// 드래그&던지기(12절) "놓기" 신호. DragThrowController(Interaction)가 마우스업/트레이 긴급정지
        /// 발생 시 true로 세팅하고, DragThrowState.Tick()이 다음 틱에 읽는 즉시 false로 되돌리는 1회성
        /// 펄스 계약이다(IMovementIntentSource.JumpRequested와 동일한 소비-후-리셋 원칙, 다만 리셋 주체가
        /// 컨트롤러가 아니라 소비자 자신이라는 점만 다르다 — 이 신호는 매 프레임이 아니라 이벤트성으로만
        /// 세팅되므로 컨트롤러 쪽에서 "다음 프레임에 리셋"할 고정된 타이밍이 없다).
        /// </summary>
        public bool DragReleaseSignaled;

        /// <summary>
        /// 격파 미니게임(10절) 클릭 판정 신호. BattleMinigameDirector(Interaction)가 캐릭터 히트박스
        /// 클릭을 감지하면 true로 세팅하고, BattleMinigameState.Tick()이 다음 틱에 소비 후 false로
        /// 되돌린다(DragReleaseSignaled와 동일한 소비-후-리셋 펄스 계약).
        /// </summary>
        public bool BattleClickSignaled;

        /// <summary>
        /// 격파 미니게임(10절) "기 모으기" 게이지의 현재 채움 비율(0~1) — <b>순수 렌더 힌트</b>다.
        /// BattleMinigameState.TickCharging()이 매 프레임 자기가 이미 계산한 값을 그대로 여기에
        /// 복사해두고, Interaction/BattleMinigameRenderer가 읽어 게이지 바를 그린다.
        ///
        /// 왜 이벤트가 아니라 블랙보드 필드인가: 이 값은 "매 프레임 바뀌는 연속량"이라 이벤트로 쏘면
        /// 초당 60회 델리게이트 호출이 되고, 무엇보다 <b>판정에는 전혀 쓰이지 않는다</b>(성공/실패는
        /// 여전히 상태 내부의 _chargeElapsed만으로 결정된다). 렌더러가 이 필드를 못 읽거나 잘못 읽어도
        /// 게임 판정은 1비트도 달라지지 않는다 — SetCharacterVisible(가출 렌더러 토글)과 같은
        /// "상태 → 렌더링 레이어 단방향 통보" 관례를 따른다.
        /// </summary>
        public float BattleChargeRatio;

        /// <summary>
        /// 지금 게이지를 그려야 하는지(=Charging 페이즈인지). Resolving(판정 후 대기/재도전 간격) 동안은
        /// false가 되어 게이지가 사라지고, 다음 재도전에서 다시 true로 돌아온다. 게이지 유무 자체가
        /// "지금 클릭이 판정에 먹히는 구간인가"의 시각 신호라 사용자가 헛클릭하지 않게 해준다.
        /// </summary>
        public bool BattleChargeGaugeVisible;

        /// <summary>
        /// Attack(전투) 상태의 IHasDialogueParams 스냅샷 입력값(BUG-M7 파이프라인, docs/BUG_REPORT_PHASE3.md
        /// Minor 1 대응). 호출자(현재는 Interaction/RivalStickmanAgent.cs)가 Machine.ChangeState(Attack)를
        /// 호출하기 직전에 이번 타격 이후 "몇 대 더 맞아야 결판나는지"를 계산해 이 필드에 써두면,
        /// AttackState.Enter()가 그 값을 그대로 스냅샷해 "한 발 더!"(&gt;=1)/"오늘은 여기까지"(0) 대사를
        /// 파생시킨다. 아무도 세팅하지 않고 ChangeState(Attack)만 호출하면 기본값 0("오늘은 여기까지")
        /// 그대로 유지된다 — 값을 채우지 않는 호출부에서도 안전한 기본 동작.
        /// </summary>
        public int AttackShotsRemaining;

        /// <summary>
        /// 유휴 혼잣말(docs/UX_FLOW.md 26-3절, Dialogue/AmbientChatter.cs)의 다음 발화 허용 시각
        /// (Time.unscaledTime 기준). Idle과 Walk가 **하나의 타이머를 공유**한다 — 둘은 2~6초마다
        /// 번갈아 일어나므로 각자 쿨다운을 두면 확률이 낮아도 체감상 수다스러워진다.
        /// </summary>
        public float NextChatterAllowedUnscaledTime;

        /// <summary>
        /// "지금 즉시 혼잣말을 하라"는 강제 발화 펄스(Interaction/AppControlDirector.cs의 데모 단축키
        /// Ctrl+Opt+Cmd+B). AmbientChatter.TryRollChatter()가 소비 즉시 리셋하며, 소비되면 확률/쿨다운을
        /// 모두 건너뛴다(DragReleaseSignaled/BattleClickSignaled와 동일한 1프레임 펄스 계약).
        /// </summary>
        public bool ForcedChatterSignaled;

        /// <summary>
        /// 가출(20절) 은신처 월드 좌표 스냅샷 — Interaction/RunawayDirector.cs가 ChangeState(Runaway)
        /// 직전에 세팅하고, States/RunawayState.cs의 초기 Enter()가 1회 소비한다(AttackShotsRemaining과
        /// 동일한 "펄스 세팅 → 소비" 관례).
        /// </summary>
        public Vector2 PendingRunawayHideWorldPos;

        /// <summary>가출 상태에서 "발견됨"(캐릭터 히트박스 클릭) 1회성 신호. RunawayDirector가
        /// StickmanClickHitbox.MouseDown을 구독해 세팅하고, RunawayState.Tick()이 Hidden 페이즈에서만
        /// 소비한다(다른 페이즈 중 세팅되면 조용히 무시 — DragReleaseSignaled와 동일한 소비-후-리셋 펄스).</summary>
        public bool RunawayFoundSignaled;

        /// <summary>가출 상태에서 "간식을 줌"(Found 페이즈 전용) 1회성 신호. 실제 UI(간식 주기 버튼)는
        /// Phase2+ 렌더링 담당 — 지금은 Interaction/RunawayDirector.cs가 공개한 OfferSnack()이 이 신호를 세팅한다.</summary>
        public bool RunawaySnackOfferedSignaled;

        /// <summary>가출 상태에서 "돌아오라고 부르기"(트레이 수동 소환, 20절) 1회성 신호.</summary>
        public bool RunawayManualRecallSignaled;

        /// <summary>가출 상태에서 트레이 긴급정지로 인한 "강제 소환"(24절 — 인질극의 '종료'와 달리 가출은
        /// '즉시 강제 복귀') 1회성 신호. 다른 복귀 신호와 달리 어떤 페이즈에서도 즉시 처리된다.</summary>
        public bool RunawayForceSummonSignaled;

        /// <summary>
        /// 캐릭터 렌더러 표시/숨김 제어(가출 20절 전용 통로). StickmanAgent.Awake()가 자신의 기존
        /// private SetRenderersEnabled(bool)를 그대로 이 델리게이트에 연결한다(CursorProvider와 동일한
        /// "이미 있는 private 메서드를 블랙보드로 노출" 패턴 — 새 메서드를 만들지 않는다). Suspend()/
        /// Resume()의 전체화면 은닉과는 별개의 독립 스위치다 — RunawayState는 이 필드만 사용하고
        /// StickmanAgent._isSuspended 플래그에는 관여하지 않는다(20절 예외: 가출 중 전체화면 감지가
        /// 와도 강제 취소하지 않고 그냥 함께 멈췄다가(Suspended는 Tick 자체를 건너뜀) 재개된다).
        /// </summary>
        public System.Action<bool> SetCharacterVisible;

        /// <summary>
        /// BUG-P5-M1 대응(Major, docs/BUG_REPORT_PHASE5.md) — RunawayState의 Hidden 페이즈가 렌더러를
        /// 숨긴 동안(아직 발견되지 않음)임을 StickmanAgent.Resume()에 알리는 통로. 기존 문제: Resume()은
        /// 항상 SetRenderersEnabled(true)를 무조건 호출했는데, 가출 Hidden 구간 중 전체화면 Suspend/
        /// Resume이 한 번이라도 왕복하면 이 무조건 호출이 RunawayState의 독립적인 은신 가시성 의도를
        /// 덮어써 아직 못 찾은 캐릭터가 강제로 노출됐다(20절 핵심 상호작용 위반).
        /// RunawayState.HideCharacterAtHideSpot()이 true로 세팅하고, ShowCharacterRevealed()/
        /// RestoreCharacter()/Exit()가 false로 되돌린다 — Resume()은 이 플래그가 true인 동안만
        /// SetRenderersEnabled(true) 호출을 건너뛴다("Suspend/Resume의 렌더러 제어"와 "Runaway의 렌더러
        /// 제어"가 서로의 존재를 알게 되는 최소 접점). IStickmanState에 훅을 추가하는 대안(Enter/Exit
        /// 외 제3의 메서드)도 검토했으나, 그러면 인터페이스를 구현하는 다른 10여 개 상태 전부가 영향
        /// 범위에 들어와 이 필드 하나만 추가하는 쪽이 더 침습적이지 않다고 판단했다(BUG_REPORT_PHASE5.md
        /// 수정 제안 (b) 채택, Coder 판단).
        /// </summary>
        public bool IsCharacterHiddenByRunaway;

        /// <summary>
        /// CursorProvider(OS 화면 좌표)를 이 블랙보드의 MainCamera/Config로 Unity 월드 좌표로 역변환한다.
        /// ScreenCoordinateConverter의 "cameraDepth는 같은 호출 세트 안에서 재사용" 규칙을 지키기 위해,
        /// Body 위치를 기준점으로 삼아 depth를 산출한 뒤 그 depth로 커서 좌표를 되돌린다(SenseGround()가
        /// 발판 좌표를 되돌릴 때 쓰는 것과 동일한 패턴).
        /// </summary>
        public bool TryGetCursorWorldPosition(out Vector2 worldPos)
        {
            worldPos = default;
            if (CursorProvider == null || MainCamera == null || Body == null) return false;
            if (!CursorProvider(out Vector2 osScreen)) return false;

            _ = ScreenCoordinateConverter.WorldToOsScreen(MainCamera, Body.position, Config, out float depth);
            Vector3 world = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, osScreen, depth, Config);
            worldPos = world;
            return true;
        }

        // Idle/Walk(지상 상태)에서 발판을 잃은 뒤 실제로 Fall로 전이하기까지의 유예 누적 시간.
        // Idle<->Walk를 오가는 동안에도 값이 보존되어야 발판 경계에서 상태가 왔다갔다 할 때마다
        // 유예 타이머가 리셋되는 오탐을 막을 수 있어(상태 인스턴스 밖인) 블랙보드에 둔다.
        private float _groundLossTimer;

        // Active Ragdoll(아키텍처 0절) 파츠 캐시. Ragdoll/Getup 두 상태가 공유하므로 블랙보드가
        // 최초 1회만 구성해 보관한다(매 프레임 GetComponentsInChildren 재탐색 금지 컨벤션 준수).
        private RagdollRig _ragdollRig;

        // 능동 상태 절차적 팔다리 포즈 드라이버(2026-08-28 근본 재구현). GetRagdollRig()와 동일한 지연
        // 생성/캐싱 패턴 — Idle/Walk/Getup을 포함한 모든 능동 상태가 공유한다.
        private StickmanPoseAnimator _poseAnimator;

        // 머리 안 눈동자 점 제어(States/EyeController.cs). 같은 지연 생성/캐싱 패턴.
        private EyeController _eyeController;

        // 마지막으로 확정된 바라보는 방향(+1 오른쪽 / -1 왼쪽). 이동 의도가 불감대 이하일 때는 갱신하지
        // 않고 그대로 유지한다(정지 중에 방향이 흔들리지 않게).
        private float _facingSign = 1f;

        /// <summary>
        /// ★ 지금 실제로 딛고 있는 발판의 핸들(0 = 없음/공중). 리더 지시 3~5항의 "발판 고착" 상태.
        /// - 착지 확정(FallState.ConfirmLanding)에서만 설정된다 = 발판 전환은 낙하->착지로만 일어난다.
        /// - Fall 진입(FallState.Enter)에서 0으로 지워진다 = 공중에서는 어떤 발판도 붙잡고 있지 않다.
        /// - 이 값이 0이 아니면 SenseGround()는 그 핸들의 발판만 접지 후보로 본다(GroundSensor.Sense의
        ///   preferredHandle 문서 참고). 그 발판이 사라지거나 X 범위를 벗어나면 즉시 Grounded=false가
        ///   되고 GroundedTick()이 Fall로 보낸다.
        /// 드래그/랙돌/로데오처럼 몸을 임의 위치로 옮기는 상태를 거쳐 오면 값이 낡아 있을 수 있는데,
        /// 그때는 접지 판정이 실패해 fallGraceDuration(0.1초) 뒤 Fall -> Enter에서 0으로 초기화 ->
        /// 재획득으로 **스스로 회복된다**(고착 상태가 남지 않는다).
        /// </summary>
        public long CurrentFootholdHandle;

        /// <summary>FootholdPoller의 캐시(= OS를 직접 호출하지 않는 저렴한 조회)를 이용해 접지 상태를 계산한다.</summary>
        public GroundSensor.GroundInfo SenseGround()
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            Vector2 foot = Body != null ? Body.position : Vector2.zero;
            return GroundSensor.Sense(MainCamera, foot, footholds, Config, CurrentFootholdHandle);
        }

        /// <summary>
        /// 스윕 착지 판정(GroundSensor.TryFindLandingCrossing 문서 참고 — 헤드라인 기능 "창 위 착지"가
        /// 실제로 성립하게 만드는 연속 교차 검사). FallState가 매 프레임 호출한다.
        ///
        /// ★ 2026-08-29 — 뛰어내리기 직후의 drop-through 유예를 여기서 함께 적용한다
        /// (<see cref="DropThroughIgnoredFootholdHandle"/> 문서 참고). 이 한 곳에서 넘겨주므로
        /// FallState는 "무시 핸들"이라는 개념 자체를 알 필요가 없다.
        /// </summary>
        public bool TryFindLandingCrossing(Vector2 prevFootWorldPos, Vector2 currFootWorldPos, out long handle, out float landingWorldY)
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            return GroundSensor.TryFindLandingCrossing(MainCamera, prevFootWorldPos, currFootWorldPos, footholds, Config,
                out handle, out landingWorldY, DropThroughIgnoredFootholdHandle);
        }

        // ============================================================================
        // ★ 뛰어내리기 직후 "방금 떠난 발판" 통과 유예 (drop-through, 2026-08-29)
        // ============================================================================
        // 왜 필요한가 / 왜 이 방식인가는 States/WalkState.cs의 "발을 뗍니다" 블록 주석과
        // StickConfig.hopDownDropThroughIgnoreDuration의 Tooltip에 근거까지 적어뒀다. 요약하면:
        // 서 있는 몸은 발판 상단선에 정확히 스냅돼 있어 그대로 Fall에 들어가면 스윕 교차가 방금 떠난
        // 발판을 다시 잡는다. 예전에는 몸을 모서리 바깥으로 **순간이동**시켜 이를 피했지만(실측 0.31유닛),
        // 순간이동은 사용자가 반복 지적해온 아티팩트라 "잠깐 착지 후보에서 빼기"로 대체했다.
        //
        // 적용 범위는 의도적으로 최소다: 이 값을 세팅하는 곳은 WalkState의 뛰어내리기 블록 **한 곳**뿐이고,
        // 읽는 곳은 FallState의 착지 확정 두 경로뿐이다. 매달리기 해제/던지기/일반 낙하는 이 값을 절대
        // 세팅하지 않으므로 0(무시 없음)이라 기존 동작과 100% 동일하다.

        private long _dropThroughIgnoredHandle;
        private float _dropThroughIgnoreUntilTime = float.NegativeInfinity;

        /// <summary>
        /// 지금 착지 후보에서 제외해야 할 발판 핸들(유예가 끝났거나 애초에 없으면 0 = 제외 없음).
        /// 0은 이 프로젝트 전체에서 "발판 없음"을 뜻하는 관례값이라 그대로 재사용한다.
        /// </summary>
        public long DropThroughIgnoredFootholdHandle =>
            Time.time <= _dropThroughIgnoreUntilTime ? _dropThroughIgnoredHandle : 0L;

        /// <summary>주어진 핸들이 지금 drop-through 유예로 착지 후보에서 빠져 있는지.</summary>
        public bool IsFootholdDropThroughIgnored(long handle)
        {
            return handle != 0L && handle == DropThroughIgnoredFootholdHandle;
        }

        /// <summary>
        /// 지금부터 durationSeconds 동안 그 발판을 착지 후보에서 제외한다(뛰어내리기 전용).
        /// 유예는 시간이 지나면 스스로 풀리므로 해제 호출이 필요 없다 — 어떤 경로로 상태가 바뀌어도
        /// "무시가 영구히 남는" 사고가 구조적으로 불가능하다.
        /// </summary>
        public void BeginDropThroughIgnore(long footholdHandle, float durationSeconds)
        {
            if (footholdHandle == 0L || durationSeconds <= 0f) return;
            _dropThroughIgnoredHandle = footholdHandle;
            _dropThroughIgnoreUntilTime = Time.time + durationSeconds;
        }

        /// <summary>
        /// probeWorldPos의 x에서 딛을 수 있는 지면(가장 높은 발판 상단)의 월드 Y.
        /// GroundSensor.TryGetSurfaceWorldY 문서 참고 — 커서가 지면보다 아래에 있을 때 캐릭터를 그리로
        /// 옮겨 지면 밑에 가두는 사고(Fall 영구 고착)를 막기 위해 드래그&던지기/로데오가 쓴다.
        /// </summary>
        public bool TryGetGroundSurfaceWorldY(Vector2 probeWorldPos, out float surfaceWorldY)
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            return GroundSensor.TryGetSurfaceWorldY(MainCamera, probeWorldPos, footholds, Config, out surfaceWorldY);
        }

        /// <summary>
        /// probeWorldPos의 x에서 **가장 낮은** 발판 상단(= 그 x에서의 바닥)의 월드 Y.
        /// GroundSensor.TryGetFloorWorldY 문서 참고 — 드래그/로데오의 "지면 아래로는 끌고 내려가지
        /// 않는다" 소프트 클램프가 써야 하는 값이다(가장 높은 표면을 쓰면 캐릭터를 위쪽 창으로
        /// 끌어올려 버린다 — 사용자 신고 "마우스로 끌었는데 갑자기 다른 창 위로 올라감").
        /// </summary>
        public bool TryGetFloorWorldY(Vector2 probeWorldPos, out float floorWorldY)
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            return GroundSensor.TryGetFloorWorldY(MainCamera, probeWorldPos, footholds, Config, out floorWorldY);
        }

        /// <summary>
        /// Idle/Walk 공용 지상 로직: 접지 중이면 유예 타이머를 리셋하고 위치를 발판에 스냅한다.
        /// 접지가 아니면 유예 타이머를 누적하다가 StickConfig.fallGraceDuration을 넘기면 Fall로
        /// 강제 전이한다(발판 경계의 미세한 흔들림으로 인한 오탐 방지, StickConfig.cs 문서 참고).
        /// </summary>
        /// <returns>이번 호출로 Fall 전이가 발생했으면 true(호출부는 나머지 로직을 생략해야 함).</returns>
        public bool GroundedTick(float deltaTime, GroundSensor.GroundInfo info)
        {
            if (info.Grounded)
            {
                // 발판 "획득": 아직 붙잡은 발판이 없는 상태(0)에서 처음 접지하면 그 발판으로 고착한다.
                // 이 한 줄이 없으면 앱 시작 직후처럼 낙하->착지를 한 번도 거치지 않은 구간에서
                // CurrentFootholdHandle이 0으로 남아, 매 프레임 목록 첫 매치를 새로 고르는 예전 동작이
                // 그대로 살아난다 — 새 창이 열릴 때 그 창 상단으로 순간이동하는 증상(사용자 신고 3번)의
                // 잔여 경로다. 획득 이후에는 GroundSensor.Sense()가 이 핸들만 보므로 재선택이 불가능하다.
                if (CurrentFootholdHandle == 0L && info.GroundedFootholdHandle != 0L)
                {
                    CurrentFootholdHandle = info.GroundedFootholdHandle;
                    ReportFootholdChangeIfNeeded("접지 획득(공중을 거치지 않은 최초 접지)");
                }
                _groundLossTimer = 0f;
                // 스냅이 상한을 넘어 "발판을 놓고 Fall"로 갔으면 그 사실을 호출부에 그대로 전달한다
                // (호출부 계약: true = 이번 호출로 Fall 전이가 일어났으니 나머지 로직을 생략하라).
                return SnapToGround(info);
            }

            _groundLossTimer += deltaTime;
            float grace = Config != null ? Config.fallGraceDuration : 0.1f;
            if (_groundLossTimer < grace) return false;

            _groundLossTimer = 0f;
            // 리더 지시: 발판을 잃는 순간을 **사유와 함께** 남긴다(로그가 유일한 판별 수단).
            Debug.Log($"[발판상실] 딛고 있던 발판(핸들={CurrentFootholdHandle})이 {grace:F2}초 동안 접지 조건을 " +
                "만족하지 못해 Fall로 전이합니다 — 사유는 (a) 그 창이 닫히거나 다른 창에 완전히 가려져 " +
                "발판 목록에서 사라짐, (b) 창이 움직여 캐릭터 X가 그 창의 X 범위를 벗어남, " +
                "(c) 창이 세로로 이동해 상단선이 허용오차 밖으로 벗어남 중 하나다.");
            Machine.ChangeState(StickmanStateId.Fall);
            return true;
        }

        public void ResetGroundLossTimer() => _groundLossTimer = 0f;

        /// <summary>
        /// Idle/Walk의 Jump 전이가 실제로 확인해야 할 조건: "접지 중이거나, 발판을 벗어난 지
        /// StickConfig.coyoteTimeDuration 이내"(BUG-P1-M5 대응, Architect 결정 — 의도된 코요테 타임으로
        /// 채택). 이전에는 이 조건을 별도로 확인하지 않고 "GroundedTick이 아직 Fall로 강제 전이시키지
        /// 않았다"는 사실 하나만으로 점프를 암묵적으로 허용했는데, 그 판단 기준(fallGraceDuration)이
        /// 발판 이탈 판정과 점프 허용 판정이라는 서로 다른 두 목적에 재사용되고 있었다. 이제는 별도
        /// 필드(coyoteTimeDuration)로 명시적으로 판정한다 — GroundedTick 호출 직후(같은 프레임)에만
        /// 호출해야 정확하다(같은 _groundLossTimer 값을 공유).
        /// </summary>
        public bool IsWithinCoyoteTime(GroundSensor.GroundInfo info)
        {
            if (info.Grounded) return true;
            float coyote = Config != null ? Config.coyoteTimeDuration : 0.1f;
            return _groundLossTimer <= coyote;
        }

        /// <summary>
        /// 접지 중 캐릭터 발을 발판 상단선에 정착(settle)시킨다.
        ///
        /// ============================================================================
        /// ★ 2026-08-29 — 이동 거리 상한(리더 지시). "미세 정착"과 "순간이동"을 코드로 구분한다.
        /// ============================================================================
        /// 이 함수는 원래 "0.001유닛보다 어긋나 있으면 발판 상단 Y를 그냥 대입"이었고 **이동 거리에
        /// 상한이 전혀 없었다**. 미세 정착이 목적인 함수가 원리적으로는 화면 끝까지 순간이동시킬 수
        /// 있는 형태였다는 뜻이다. 지금은 상한을 넘으면 끌어올리지 않고 **발판을 놓고 Fall로 보낸다** —
        /// 딛고 있던 발판이 캐릭터를 지나쳐 커졌다면 캐릭터는 공중에 남는 것이 물리적으로 맞다.
        ///
        /// 상한을 위/아래로 나누지 않은 이유(리더가 판단을 요구한 항목): 두 경우의 **올바른 처리가
        /// 똑같이 "Fall"**이기 때문이다. 위로 크게 끌려가는 것은 명백한 순간이동이고, 아래로 크게
        /// 내려가는 것은 애초에 스냅이 아니라 낙하로 처리돼야 한다(그리고 Fall에 들어가면
        /// GroundSensor.TryFindLandingCrossing의 스윕 교차 판정이 정확한 착지면을 다시 잡아준다 —
        /// 아래로 억지로 대입하는 것보다 이 경로가 언제나 더 정확하다). 값이 하나면 어긋날 일도 없다.
        ///
        /// ★ 정직한 한계 — 이 상한은 지금 코드에서는 **방어적 불변식**이지 이번 신고의 원인 제거가
        /// 아니다. GroundSensor.Sense()는 발이 발판 상단의 ±groundSnapTolerance(에셋 20 OS-pt ≈ 0.49
        /// 월드유닛) 안에 있을 때만 Grounded=true를 주고 GroundWorldY도 그때의 그 발판 상단이므로,
        /// 현재 배선에서 이 함수가 옮길 수 있는 거리는 이미 그 허용오차로 묶여 있다(= 화면 높이만큼
        /// 끌어올리는 일은 이 경로로는 일어날 수 없다). 실제 신고 원인은 RescueToSafeGround였다
        /// (그 함수 문서의 실측 로그 근거 참고). 그럼에도 상한을 두는 값어치는 분명하다: "무엇을
        /// 접지로 볼 것인가(groundSnapTolerance)"와 "몸을 얼마나 순간이동시켜도 되는가"는 서로 다른
        /// 두 결정인데 지금까지 전자 하나에 묶여 있었다. 누가 groundSnapTolerance를 올리는 순간
        /// 순간이동 허용치가 조용히 함께 커지는 구조였고, 이 상한이 그 연결을 끊는다.
        /// </summary>
        /// <returns>상한 초과로 발판을 놓고 Fall로 전이했으면 true.</returns>
        private bool SnapToGround(GroundSensor.GroundInfo info)
        {
            if (Body == null) return false;
            Vector2 pos = Body.position;
            float delta = info.GroundWorldY - pos.y; // + = 위로 끌어올림, - = 아래로 내림
            float maxSnap = Config != null ? Mathf.Max(0f, Config.groundSnapMaxDistanceWorld) : 0.6f;

            if (Mathf.Abs(delta) > maxSnap)
            {
                Debug.Log($"[스냅상한초과] 접지 스냅이 상한을 넘어 발판을 놓고 낙하시킵니다 — " +
                    $"{(delta > 0f ? "위로" : "아래로")} {Mathf.Abs(delta):F3}유닛(상한 {maxSnap:F2}) 이동 요구, " +
                    $"발 월드Y={pos.y:F3}, 발판 상단 월드Y={info.GroundWorldY:F3}, 발판핸들={CurrentFootholdHandle}. " +
                    "딛고 있던 발판이 캐릭터를 지나쳐 크게 움직였다는 뜻이라, 끌고 가지 않고 공중에 남깁니다.");
                CurrentFootholdHandle = 0L;
                ReportFootholdChangeIfNeeded("접지 스냅 상한 초과 — 발판을 놓고 낙하");
                _groundLossTimer = 0f;
                Machine?.ChangeState(StickmanStateId.Fall);
                return true;
            }

            if (Mathf.Abs(delta) > 0.001f)
            {
                Body.position = new Vector2(pos.x, info.GroundWorldY);
            }
            if (Body.linearVelocity.y < 0f)
            {
                Vector2 v = Body.linearVelocity;
                v.y = 0f;
                Body.linearVelocity = v;
            }
            return false;
        }

        // ============================================================================
        // ★ 리더 지시 6·7항 — 화면 밖 소실 방지(하드 클램프) + 최종 안전망(리스폰)
        // ============================================================================
        // 사용자 신고 4번: "그러다가 갑자기 화면 밖으로 사라져버림". 캐릭터를 영영 잃어버리는 것이
        // 가장 치명적이므로 이 규칙은 다른 모든 로직보다 **나중에, 무조건** 적용된다
        // (StickmanAgent.Update()가 상태 Tick을 전부 끝낸 뒤 마지막에 호출한다 —
        // 어떤 상태가 어떤 이유로 몸을 옮겼든 그 결과를 여기서 되돌린다).
        //
        // 왜 기존 CheckScreenBoundsOrFall로는 부족한가: 그 검사는 "발판들의 좌우 범위"를 화면으로
        // 간주한다. 그런데 실제 창은 화면 경계를 넘어갈 수 있어서(창을 화면 밖으로 반쯤 끌어다 놓는
        // 흔한 상황) 그 범위 자체가 화면 밖까지 뻗고, 그러면 캐릭터가 그 위를 걸어 화면 밖으로
        // 나가버린다. MacWindowService가 발판을 디스플레이 경계로 잘라내는 것이 1차 방어이고,
        // 여기가 그와 독립적인 2차(최종) 방어다.

        /// <summary>화면 경계에서 남겨둘 최소 여유(OS 포인트). 아래 CharacterVisualHalfWidthWorld가
        /// 더해져 실제 여유가 결정된다.</summary>
        private const float ScreenClampMarginOsPx = 8f;

        /// <summary>
        /// ★ 2026-08-28 (리더 추가 관찰: "캐릭터가 화면 왼쪽 끝에서 잘려 보인다") — 캐릭터의 **시각적
        /// 반폭**(월드 유닛). 화면 하드 클램프는 루트(=발 중심) 좌표만 보므로, 이 값을 더하지 않으면
        /// 가장자리에서 팔/머리가 화면 밖으로 잘린다. Core/StickmanAgent가 자신의 렌더러 바운즈에서
        /// 주기적으로 갱신한다(포즈에 따라 팔 벌린 너비가 바뀌므로 상수로 둘 수 없다). 0이면 예전처럼
        /// 루트만 클램프한다(테스트/폴백 경로에서도 안전).
        /// </summary>
        public float CharacterVisualHalfWidthWorld;

        /// <summary>이 시간(초) 넘게 Fall이 이어지면 "유효 발판을 완전히 잃었다"고 보고 리스폰한다.</summary>
        private const float LostCharacterRescueSeconds = 6f;

        private float _fallStuckTimer;
        private long _lastReportedFootholdHandle = long.MinValue;

        // [화면클램프] 로그 throttle(2026-08-28 로그 정리 라운드). 이 로그는 "이상 신호"라
        // StickConfig.verboseDiagnosticsLogging과 무관하게 항상 남겨야 하지만, 클램프는 캐릭터가
        // 가장자리를 계속 밀 때 **매 프레임** 성립할 수 있어 그대로 두면 초당 수십 줄이 쏟아진다.
        // 최소 간격만 두어 "무슨 일이 있었다"는 신호는 잃지 않으면서 홍수만 막는다.
        private const float ScreenClampLogMinIntervalSeconds = 2f;
        private float _lastScreenClampLogTime = float.NegativeInfinity;

        // ============================================================================
        // ★ 화면 클램프 경계의 단일 소스 (2026-08-29 — "화면 물리적 끝에서 제자리 걷기" 수정)
        // ============================================================================
        // 증상: 캐릭터가 화면 좌/우 끝까지 걸어가면 걷기 애니메이션만 돌고 위치는 변하지 않는
        // "러닝머신"이 되어, Walk 지속시간(1.5~4초)이 만료될 때까지 스스로 풀리지 않았다.
        //
        // 원인: 아래 하드 클램프는 캐릭터 루트를 화면 끝에서 (ScreenClampMarginOsPx + 시각 반폭)만큼
        // 안쪽에 가둔다(실측 약 58pt). 그런데 배회 AI의 경계 판정(AutoWanderController.IsNearFootholdEdge)은
        // wanderEdgeStopDistance(0.3유닛 ≈ 24pt)를 **발판의 원시 경계**(= 화면 끝)에서 쟀다.
        // 58 > 24라서 "경계 근처"가 영영 성립하지 않았고, 캐릭터는 돌아설 이유를 못 찾은 채 클램프만
        // 계속 밀었다.
        //
        // 해법: "캐릭터가 물리적으로 갈 수 있는 한계"를 이 클래스가 **하나의 계산식**으로만 만들고
        // (ComputeScreenClampOsBounds), 클램프 본체와 조회 API(TryGetWalkableScreenBoundsWorld)가
        // 둘 다 그것만 읽게 한다. 두 곳이 각자 계산하면 반드시 다시 어긋난다 — 이 프로젝트가 이미
        // 두 번 겪은 실패 유형이다(BUG-P1-R4-B1 씬 지면 Y vs 발판 상수 이중 정의,
        // BUG-P1-R5-B2 Dock 구간 이중 계산 → 단일 소스화로 해결).

        /// <summary>
        /// 화면 하드 클램프가 캐릭터 루트를 가두는 OS 좌표 경계 묶음.
        /// 생산자는 <see cref="ComputeScreenClampOsBounds"/> 하나뿐이다.
        /// </summary>
        private readonly struct ScreenClampOsBounds
        {
            public readonly float MinX;
            public readonly float MaxX;
            public readonly float MinY;
            public readonly float MaxY;
            /// <summary>좌우에 실제로 적용된 여유(기본 여유 + 시각 반폭, OS 포인트) — 로그용.</summary>
            public readonly float SideMargin;
            public readonly float HalfWidthOsPx;
            public readonly Vector2 Origin;
            public readonly float ScreenW;
            public readonly float ScreenH;

            public ScreenClampOsBounds(float minX, float maxX, float minY, float maxY,
                float sideMargin, float halfWidthOsPx, Vector2 origin, float screenW, float screenH)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
                SideMargin = sideMargin;
                HalfWidthOsPx = halfWidthOsPx;
                Origin = origin;
                ScreenW = screenW;
                ScreenH = screenH;
            }
        }

        /// <summary>
        /// 하드 클램프 경계(OS 좌표)를 계산하는 **유일한** 곳. MainCamera가 null이면 호출하지 말 것
        /// (두 호출부 모두 앞에서 null을 걸러낸다).
        /// </summary>
        private ScreenClampOsBounds ComputeScreenClampOsBounds()
        {
            float dpi = Config != null ? Mathf.Max(0.0001f, Config.desktopDpiScale) : 1f;
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            float screenW = (Screen.width > 0 ? Screen.width : 1920) * dpi;
            float screenH = (Screen.height > 0 ? Screen.height : 1080) * dpi;

            // 시각적 반폭을 OS 픽셀로 환산해 좌우 여유에 더한다 — 이게 없으면 루트(발)는 화면 안인데
            // 벌린 팔과 머리가 화면 밖으로 잘린다(2026-08-28 리더 관찰). 카메라의 "월드 1유닛 = 몇
            // Unity 픽셀"에 desktopDpiScale을 곱하면 OS 포인트가 된다(ScreenCoordinateConverter와 같은
            // 환산 규칙).
            float pxPerWorldUnit = MainCamera != null && MainCamera.orthographic && MainCamera.orthographicSize > 0f
                ? (Screen.height * 0.5f) / MainCamera.orthographicSize
                : 0f;
            float halfWidthOsPx = Mathf.Max(0f, CharacterVisualHalfWidthWorld) * pxPerWorldUnit * dpi;

            float sideMargin = ScreenClampMarginOsPx + halfWidthOsPx;
            float minX = origin.x + sideMargin;
            float maxX = origin.x + screenW - sideMargin;
            if (minX > maxX) { minX = maxX = origin.x + screenW * 0.5f; } // 화면보다 캐릭터가 넓은 병리적 경우

            float minY = origin.y + ScreenClampMarginOsPx;
            // ★ 아래쪽 여유는 0이다(2026-08-28). 이유: 안전망 발판이 화면 최하단 근처로 내려온 뒤로는
            // 이 클램프가 **지면과 싸운다**. 실측으로 재현된 사고: 640x480 테스트 화면에서 8 OS px는
            // 0.4월드유닛이라 지면(0.245유닛)보다 위에 있었고, RAGDOLL이 지면에 내려앉을 때마다 클램프가
            // 매 프레임 위로 되돌리며 세로 속도를 0으로 만들어 **영원히 안정되지 못했다**(GETUP 미도달로
            // StickmanRagdollRecoveryTests가 빨간불). 이 클램프의 목적은 "캐릭터를 화면 밖에서
            // 잃어버리지 않는다"이고 그 목적에는 경계 자체(여유 0)로 충분하다 — 발판/지면은 언제나
            // 화면 안에 있으므로 정상 동작에서는 아예 발동하지 않고, 진짜로 화면 아래로 빠져나가는
            // 경우만 잡는다.
            float maxY = origin.y + screenH;

            return new ScreenClampOsBounds(minX, maxX, minY, maxY, sideMargin, halfWidthOsPx, origin, screenW, screenH);
        }

        /// <summary>
        /// ★ "캐릭터가 물리적으로 갈 수 있는 좌우 한계"(Unity 월드 X) — 화면 하드 클램프가 실제로
        /// 캐릭터를 붙잡아 세우는 바로 그 X다. 클램프 본체와 **완전히 같은 계산식**
        /// (<see cref="ComputeScreenClampOsBounds"/>)에서 파생되므로 두 값이 어긋날 수 없다.
        ///
        /// 소비자: AutoWanderController.IsNearFootholdEdge() — 발판의 경계가 곧 화면의 끝인 쪽
        /// (isTrueScreenEdge)에서는 **원시 발판 경계가 아니라 이 한계**까지의 거리로 "경계 근처"를
        /// 판정해야, 클램프에 닿기 전에 스스로 멈추고 돌아선다(위 문단의 러닝머신 증상 수정).
        ///
        /// 주의: 반환값은 **캐릭터 루트(발 중심)** 기준이며 시각 반폭이 이미 반영돼 있다
        /// (CharacterVisualHalfWidthWorld가 포즈에 따라 변하므로 프레임마다 조금씩 달라질 수 있다 —
        /// 캐싱하지 말고 필요할 때마다 물어볼 것).
        /// </summary>
        /// <returns>Body/MainCamera가 없어 계산할 수 없으면 false(그 경우 out 값은 무의미).</returns>
        public bool TryGetWalkableScreenBoundsWorld(out float leftWorldX, out float rightWorldX)
        {
            leftWorldX = 0f;
            rightWorldX = 0f;
            if (Body == null || MainCamera == null) return false;

            // 왕복 정밀도를 위해 클램프와 같은 depth(카메라 거리)를 쓴다 — ScreenCoordinateConverter의
            // 계약(WorldToOsScreen이 준 depth를 그대로 OsScreenToWorld에 넘길 것)을 그대로 지킨다.
            Vector2 os = ScreenCoordinateConverter.WorldToOsScreen(MainCamera, Body.position, Config, out float depth);
            ScreenClampOsBounds b = ComputeScreenClampOsBounds();
            leftWorldX = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, new Vector2(b.MinX, os.y), depth, Config).x;
            rightWorldX = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, new Vector2(b.MaxX, os.y), depth, Config).x;
            return true;
        }

        /// <summary>
        /// 매 프레임 마지막에 호출 — (1) 캐릭터 OS 좌표를 오버레이 창(=화면) 안으로 하드 클램프하고,
        /// (2) 그래도 발판을 완전히 잃은 채 오래 낙하 중이면 화면 중앙 지면으로 강제 복귀시킨다.
        /// </summary>
        public void EnforceScreenBoundsAndRescue(float deltaTime)
        {
            if (Body == null || MainCamera == null) return;

            Vector2 os = ScreenCoordinateConverter.WorldToOsScreen(MainCamera, Body.position, Config, out float depth);

            // 경계 계산은 전부 ComputeScreenClampOsBounds()에 있다 — 그 함수가 **유일한 생산자**이고
            // TryGetWalkableScreenBoundsWorld()도 같은 것을 읽는다(두 함수의 문서 참고).
            ScreenClampOsBounds b = ComputeScreenClampOsBounds();

            float clampedX = Mathf.Clamp(os.x, b.MinX, b.MaxX);
            float clampedY = Mathf.Clamp(os.y, b.MinY, b.MaxY);
            bool clamped = !Mathf.Approximately(clampedX, os.x) || !Mathf.Approximately(clampedY, os.y);
            if (clamped)
            {
                Vector3 world = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, new Vector2(clampedX, clampedY), depth, Config);
                Body.position = new Vector2(world.x, world.y);
                Body.transform.position = new Vector3(world.x, world.y, Body.transform.position.z);
                Vector2 v = Body.linearVelocity;
                if (!Mathf.Approximately(clampedX, os.x)) v.x = 0f;   // 벽에 막힌 것처럼 수평 관성 제거
                if (clampedY < os.y) v.y = 0f;                        // 아래로 뚫고 나가던 관성 제거
                Body.linearVelocity = v;
                if (Time.unscaledTime - _lastScreenClampLogTime >= ScreenClampLogMinIntervalSeconds)
                {
                    _lastScreenClampLogTime = Time.unscaledTime;
                    Debug.Log($"[화면클램프] 캐릭터가 화면 밖으로 나가려 해 되돌렸습니다 — OS ({os.x:F1},{os.y:F1}) -> " +
                        $"({clampedX:F1},{clampedY:F1}), 좌우여유={b.SideMargin:F1}pt(기본 {ScreenClampMarginOsPx:F0} + 시각반폭 {b.HalfWidthOsPx:F1}), " +
                        $"화면=({b.Origin.x:F0},{b.Origin.y:F0} {b.ScreenW:F0}x{b.ScreenH:F0}), " +
                        $"상태={(Machine != null ? Machine.CurrentStateId.ToString() : "?")}. " +
                        $"(같은 로그는 최소 {ScreenClampLogMinIntervalSeconds}초 간격으로만 남깁니다)");
                }
            }

            // (2) 최종 안전망 — 오래 낙하 중이면(= 어떤 발판에도 착지하지 못하는 상황) 강제 복귀.
            bool falling = Machine != null && Machine.CurrentStateId == StickmanStateId.Fall;
            _fallStuckTimer = falling ? _fallStuckTimer + deltaTime : 0f;
            if (_fallStuckTimer >= LostCharacterRescueSeconds)
            {
                _fallStuckTimer = 0f;
                RescueToSafeGround();
            }
        }

        /// <summary>
        /// 캐릭터를 화면 가로 중앙으로 옮기고, 그 X의 **바닥**(= 그 x에서 가장 낮은 발판 상단) 위에 세운
        /// 뒤 Idle로 되돌린다. 리더 지시 7항.
        ///
        /// ============================================================================
        /// ★★ 2026-08-29 — 사용자 신고 "창이 최대이면 갑자기 제일위로 순간이동해서 떨어짐"의 **진짜 원인**
        /// ============================================================================
        /// 이 함수는 원래 TryGetGroundSurfaceWorldY(= 그 x에서 **가장 높은** 발판 상단)로 복귀 지점을
        /// 골랐다. 평소에는 그 값이 Dock 상단이라 아무 문제가 없었는데, 사용자가 창 하나를 **최대화**하면
        /// 그 창의 상단이 곧 화면 꼭대기가 되고, 화면 가로 중앙에서 "가장 높은 발판 상단" = 화면 꼭대기가
        /// 된다. 그래서 구조 안전망이 캐릭터를 화면 최상단으로 **순간이동**시켰다.
        ///
        /// 실측 증거(Player.log / Player-prev.log, 2026-08-29):
        ///   · Player-prev.log — [캐릭터구조] 15회 중 **15회 전부** 복귀 지점이 월드 (0.000, 11.193).
        ///     11.193은 최대화된 Cursor 창 상단(OS y=33) = 화면 꼭대기다.
        ///   · Player.log — 24회 중 6회가 11.193(그 창이 목록에 있던 구간), 18회는 -10.167(Dock 상단,
        ///     정상). 즉 "최대화된 창이 있을 때만" 최상단으로 튄다 — 신고 문구 그대로다.
        ///
        /// 왜 이 안전망이 그렇게 자주 돌았는가(= 증상이 반복된 이유): Dock 가로 구간의 화면 최하단은
        /// 물리 바닥(PhysicsGround, 월드 -11.02)이 논리 발판(Dock 상단 -10.167)보다 0.855유닛 아래에
        /// 있어서 "물리적으로는 떠받쳐지지만 논리적으로는 접지하지 않는" 사각지대다(그 설계 근거는
        /// Assets/Editor/SceneBootstrapper.cs의 CreateGroundCollider 문서 참고 — 랙돌이 화면 밖으로
        /// 사라지는 더 나쁜 실패를 막기 위한 의도적 선택이고, 회수는 이 안전망에 맡긴다고 명시돼 있다).
        /// 그 사각지대로 흘러든 캐릭터는 6초 뒤 여기로 오고, 여기가 캐릭터를 화면 꼭대기로 올려놓고,
        /// 거기서 다시 떨어져 같은 사각지대로 돌아오는 **무한 루프**가 됐다.
        ///
        /// 해법: "가장 높은 표면"이 아니라 **"그 x의 바닥"**(TryGetFloorWorldY)으로 복귀시킨다. 안전망의
        /// 목적은 "잃어버린 캐릭터를 딛을 수 있는 곳에 돌려놓는다"이지 "가장 높은 곳에 올려놓는다"가
        /// 아니다. 바닥은 정의상 화면 하단(Dock/합성 안전망)이므로 창을 아무리 최대화해도 이 함수가
        /// 캐릭터를 위로 끌어올릴 수 없다.
        ///
        /// 같은 클래스의 과거 수정과 정확히 같은 교훈이다: 드래그 순간이동("마우스로 끌었는데 갑자기
        /// 다른 창 위로 올라감")도 원인이 TryGetSurfaceWorldY(가장 높은 표면)였고 TryGetFloorWorldY로
        /// 바꿔 고쳤다(GroundSensor.TryGetFloorWorldY 문서). 이 함수만 예전 호출부로 남아 있었다.
        /// </summary>
        public void RescueToSafeGround()
        {
            if (Body == null || MainCamera == null) return;

            float dpi = Config != null ? Mathf.Max(0.0001f, Config.desktopDpiScale) : 1f;
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            float centerOsX = origin.x + (Screen.width > 0 ? Screen.width : 1920) * dpi * 0.5f;

            Vector2 before = Body.position;
            _ = ScreenCoordinateConverter.WorldToOsScreen(MainCamera, before, Config, out float depth);
            Vector3 centerWorld = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, new Vector2(centerOsX, origin.y), depth, Config);

            float targetY = centerWorld.y;
            var probe = new Vector2(centerWorld.x, before.y);
            // ★ 가장 높은 표면(TryGetGroundSurfaceWorldY)이 아니라 **바닥**을 쓴다 — 위 문서의 실측 근거.
            if (TryGetFloorWorldY(probe, out float floorY)) targetY = floorY;

            Body.position = new Vector2(centerWorld.x, targetY);
            Body.transform.position = new Vector3(centerWorld.x, targetY, Body.transform.position.z);
            Body.linearVelocity = Vector2.zero;
            CurrentFootholdHandle = 0L; // 재획득하도록 초기화
            ResetGroundLossTimer();
            Machine?.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            Debug.Log($"[캐릭터구조] {LostCharacterRescueSeconds}초 이상 착지하지 못해 강제 복귀시켰습니다 — " +
                $"월드 {before} -> ({centerWorld.x:F3},{targetY:F3}) (화면 가로 중앙의 **바닥**). " +
                "사용자가 캐릭터를 잃어버리지 않게 하는 최종 안전망입니다(리더 지시 7항). " +
                "복귀 지점은 그 x에서 가장 낮은 발판 상단이므로, 창을 최대화해도 화면 꼭대기로 " +
                "올라가지 않습니다(2026-08-29 수정 — 이 함수 문서의 실측 근거 참고).");
        }

        /// <summary>딛고 있는 발판이 바뀔 때마다 이전->이후를 한 줄로 남긴다(리더 지시: 순간이동 추적용).</summary>
        public void ReportFootholdChangeIfNeeded(string reason)
        {
            if (CurrentFootholdHandle == _lastReportedFootholdHandle) return;
            long before = _lastReportedFootholdHandle;
            _lastReportedFootholdHandle = CurrentFootholdHandle;
            if (before == long.MinValue) return; // 최초 1회는 "변경"이 아니다
            Debug.Log($"[발판변경] {before} -> {CurrentFootholdHandle} ({reason}).");
        }

        /// <summary>
        /// 모든 발판의 좌우 범위(GroundInfo.ScreenLeft/RightWorldX)를 벗어났는지 검사해 벗어났다면
        /// Fall로 강제 전이한다. Idle/Walk/Jump/Fall 공통으로 호출된다.
        /// </summary>
        public bool CheckScreenBoundsOrFall(GroundSensor.GroundInfo info)
        {
            if (!info.HasAnyFoothold || Body == null) return false;
            float x = Body.position.x;
            if (x >= info.ScreenLeftWorldX && x <= info.ScreenRightWorldX) return false;

            // 이미 Fall이면 재전이를 걸지 않는다 — ChangeState는 같은 상태로도 매번 Exit()/Enter()를
            // 재실행하고 TransitionGeneration을 증가시키므로(BUG_REPORT_PHASE0.md Minor m3), FallState가
            // 화면 밖에 머무는 동안 매 프레임 자기 자신으로 재전이하는 불필요한 처리를 피한다.
            if (Machine.CurrentStateId != StickmanStateId.Fall)
            {
                Machine.ChangeState(StickmanStateId.Fall);
            }
            return true;
        }

        /// <summary>
        /// Active Ragdoll 파츠 캐시(Rigidbody2D/HingeJoint2D) — RagdollState/GetupState가 공유한다.
        /// 최초 필요 시 1회만 Body.transform을 루트로 GetComponentsInChildren 탐색을 수행해 캐싱한다.
        /// </summary>
        public RagdollRig GetRagdollRig()
        {
            if (_ragdollRig == null && Body != null)
            {
                // 바라보는 방향을 **매번 물어보는** 형태로 넘긴다(값 복사 금지) — RAGDOLL 진입 시점의
                // 최신 방향으로 해부학 관절 제한을 좌우 반전해야 하기 때문이다(RagdollRig의
                // EnableJointsWithAnatomicalLimits "좌우 반전" 문서, 2026-08-29 "이상하게 넘어짐" 수정).
                _ragdollRig = new RagdollRig(Body.transform, () => FacingSign);
            }
            return _ragdollRig;
        }

        /// <summary>
        /// 능동 상태 절차적 포즈 드라이버 캐시(StickmanPoseAnimator.cs) — GetRagdollRig()와 동일한 지연
        /// 생성/캐싱 패턴. 최초 필요 시 1회만 Body.transform을 루트로 이름 기반 관절 탐색을 수행한다.
        /// </summary>
        public StickmanPoseAnimator GetPoseAnimator()
        {
            if (_poseAnimator == null && Body != null)
            {
                _poseAnimator = new StickmanPoseAnimator(Body.transform);
            }
            return _poseAnimator;
        }

        /// <summary>
        /// 매 프레임(StickmanAgent.Update()가 _machine.Tick() 직후에 1회) 호출되는 **물리 모드 + 포즈의
        /// 단일 진실 공급원**. 2026-08-28 근본 재구현의 핵심 배선이다.
        ///
        /// 왜 각 상태의 Enter/Exit이 아니라 여기인가: 상태가 14개가 넘고, 어느 하나라도 물리 모드 복구를
        /// 빠뜨리면 그 상태에서만 캐릭터가 다시 무너진다(실제로 예전 구현이 그렇게 무너졌다). 게다가
        /// 전체화면 Suspend의 강제 취소, 테스트의 직접 ChangeState, ReportExternalImpact의 강제 인터럽트
        /// 등 상태 밖에서 상태가 바뀌는 경로가 여럿이다. "지금 상태 ID가 무엇인가"만 보고 매 프레임
        /// 멱등적으로 재적용하면 그 모든 경로가 자동으로 커버된다.
        ///
        /// 규칙:
        ///   Ragdoll  -> 전신 물리 위임(RagdollRig.EnterRagdoll). 포즈에 일절 개입하지 않는다.
        ///   Getup    -> 능동 모드로 되돌리되 루트 각도는 스냅하지 않는다(GetupState가 직접 보간).
        ///   Walk     -> 능동 모드 + 직립 스냅. 팔다리 각도는 WalkState.Tick()이 이미 사인파로 세팅했다.
        ///   그 외 능동 -> 능동 모드 + 직립 스냅 + Idle 중립 포즈(졸라맨 직립 실루엣).
        /// </summary>
        public void TickPose(float deltaTime)
        {
            RagdollRig rig = GetRagdollRig();
            StickmanPoseAnimator pose = GetPoseAnimator();
            if (rig == null || pose == null || Machine == null) return;

            // 바라보는 방향 갱신 — 이동 의도가 불감대를 넘을 때만 바꾼다(0 근처에서 부호가 떨리면
            // 캐릭터가 좌우로 깜빡인다). 뚜렷한 의도가 없으면 마지막 방향을 그대로 유지한다.
            float deadzone = Config != null ? Config.moveInputDeadzone : 0.15f;
            float move = MoveInputX;
            if (Mathf.Abs(move) > deadzone)
            {
                _facingSign = move >= 0f ? 1f : -1f;
                pose.SetFacing(_facingSign);
                GetEyeController()?.SetFacing(_facingSign);
            }

            // ★ 눈 커서 추적(2026-08-28 배선 완료, 사용자 명시 요청 "마우스 위치에 따라 눈도 움직여야").
            // 상태와 무관하게 **항상** 갱신한다 — 눈은 머리의 자식이므로 RAGDOLL로 머리가 뒹구는 동안에도
            // 머리를 따라 함께 움직이고(자동), 그 위에서 EyeController가 머리 로컬 공간으로 변환된
            // 시선 방향을 계속 적용한다(뒤집힌 머리에서도 화면상 커서 쪽을 본다).
            TickEyeTracking(deltaTime);

            if (Machine.CurrentStateId == StickmanStateId.Ragdoll)
            {
                // EnterRagdoll()이 아니라 멱등 버전을 쓴다 — 전자는 진입 이벤트마다 각속도를 절반으로
                // 깎으므로 매 프레임 호출하면 RAGDOLL이 회전하지 못한다(RagdollRig.cs 참고).
                rig.EnsureRagdollMode();
                return;
            }

            // RAGDOLL -> 능동 모드로 막 전환된 프레임에는, 물리가 마음대로 굴려놓은 실제 각도에서
            // 스무딩이 이어지도록 보간 상태값을 동기화한다(안 하면 랙돌 이전의 낡은 각도에서 튄다).
            if (rig.EnterActiveMode()) pose.SyncFromTransform();
            if (Machine.CurrentStateId == StickmanStateId.Getup) return;

            rig.SnapRootUpright();
            if (Machine.CurrentStateId == StickmanStateId.Walk) return;

            // 매달려 내려가기 — 중립 Idle 포즈가 아니라 "팔을 위로 뻗어 모서리를 잡고 몸이 아래로
            // 늘어진" 전용 포즈를 적용한다(States/StickmanPoseAnimator.ApplyLedgeHangPose). Walk와 달리
            // 상태 자신이 아니라 여기서 적용하는 이유: 이 포즈는 Idle 중립 포즈의 자리를 대체하는 것이라
            // 위 Idle 분기와 같은 층에 두는 편이 "상태 ID 하나로 포즈가 결정된다"는 이 메서드의 계약과
            // 정확히 일치한다.
            if (Machine.CurrentStateId == StickmanStateId.LedgeHang)
            {
                pose.ApplyLedgeHangPose(deltaTime, BuildLedgeHangPoseSettings(), PoseSmoothingRate);
                return;
            }

            pose.ApplyIdlePose(deltaTime, BuildPoseSettings(), PoseSmoothingRate);
        }

        /// <summary>
        /// 보간 없이 즉시 직립 중립 포즈로 스냅한다(StickmanAgent.Awake() 전용) — 첫 프레임부터
        /// 확정된 자세로 시작하게 만든다. 매 프레임 경로인 TickPose()와 달리 deltaTime이 없는 시점이라
        /// 지수 감쇠를 적용할 수 없으므로 별도 진입점으로 둔다.
        /// </summary>
        public void SnapToIdlePose()
        {
            RagdollRig rig = GetRagdollRig();
            StickmanPoseAnimator pose = GetPoseAnimator();
            if (rig == null || pose == null) return;
            rig.EnterActiveMode();
            rig.SnapRootUpright();
            pose.ApplyIdlePoseImmediate(BuildPoseSettings());
            GetEyeController()?.LookForward();
        }

        /// <summary>
        /// 눈 커서 추적 1프레임 갱신 + 진단 로그. TickPose()에서만 호출된다.
        ///
        /// 진단(리더 지시 "커서 위치에 따른 눈동자 오프셋 값을 로그로 찍어 검증"): 실제 눈 움직임은
        /// 사용자만 볼 수 있으므로, 커서/머리 좌표와 실제 적용된 오프셋을 함께 남긴다. 상주 앱의 로그를
        /// 더럽히지 않도록 **시작 직후 EyeLogSampleCount회만** 남기고 그 뒤로는 조용해진다
        /// (StickConfig.verboseDiagnosticsLogging을 켜면 EyeLogIntervalSeconds 주기로 계속 남는다 —
        /// [발판리포트]/[창진단]과 동일한 스위치 컨벤션).
        /// </summary>
        private void TickEyeTracking(float deltaTime)
        {
            EyeController eyes = GetEyeController();
            if (eyes == null) return;

            bool hasCursor = TryGetCursorWorldPosition(out Vector2 cursorWorld);
            eyes.TickLookAt(hasCursor, cursorWorld, deltaTime, BuildEyeTrackingSettings());

            bool verbose = Config != null && Config.verboseDiagnosticsLogging;
            if (!verbose && _eyeLogSamplesLeft <= 0) return;

            _eyeLogTimer += deltaTime;
            if (_eyeLogTimer < EyeLogIntervalSeconds) return;
            _eyeLogTimer = 0f;
            if (!verbose) _eyeLogSamplesLeft--;

            Vector2 offset = eyes.CurrentPupilOffset;
            Vector2 headWorld = Vector2.zero;
            float distance = -1f;
            if (Body != null)
            {
                headWorld = Body.position;
                if (hasCursor) distance = Vector2.Distance(cursorWorld, headWorld);
            }
            Debug.Log($"[눈추적] 커서={(hasCursor ? cursorWorld.ToString("F2") : "(조회 실패)")}, " +
                $"몸통={headWorld.ToString("F2")}, 거리={distance:F2}유닛, " +
                $"눈동자오프셋={offset.ToString("F4")}(길이 {offset.magnitude:F4}), " +
                $"시선={eyes.CurrentLookDirection.ToString("F3")}, 눈발견={eyes.HasEyes}, " +
                $"상태={(Machine != null ? Machine.CurrentStateId.ToString() : "?")}.");
        }

        /// <summary>StickConfig의 눈 추적 튜닝 값 묶음(미배선 경로에서도 안전한 기본값 사용).</summary>
        public EyeController.EyeTrackingSettings BuildEyeTrackingSettings()
        {
            if (Config == null) return EyeController.EyeTrackingSettings.Default;
            return new EyeController.EyeTrackingSettings(
                Config.eyeTrackingEnabled,
                Config.eyeMaxPupilOffset,
                Config.eyeTrackingFollowRate,
                Config.eyeTrackingNeutralRadiusWorld,
                Config.eyeTrackingFullRangeWorld);
        }

        // 눈 추적 진단 로그 상태(TickEyeTracking 문서 참고).
        private const float EyeLogIntervalSeconds = 2f;
        private const int EyeLogInitialSamples = 6;
        private int _eyeLogSamplesLeft = EyeLogInitialSamples;
        private float _eyeLogTimer;

        /// <summary>머리 안 눈동자 점 제어 캐시 — GetRagdollRig()와 동일한 지연 생성/캐싱 패턴.</summary>
        public EyeController GetEyeController()
        {
            if (_eyeController == null && Body != null)
            {
                _eyeController = new EyeController(Body.transform);
            }
            return _eyeController;
        }

        /// <summary>Idle 중립 다리 벌림 각도(도) — Config 미배선 시에도 안전한 기본값을 쓴다.</summary>
        public float IdleLegSpreadDegrees => Config != null ? Config.idleLegSpreadDegrees : 12f;

        /// <summary>Idle 중립 팔 벌림 각도(도).</summary>
        public float IdleArmSpreadDegrees => Config != null ? Config.idleArmSpreadDegrees : 40f;

        /// <summary>
        /// StickConfig에서 포즈 각도 설정 묶음을 구성한다(readonly struct — 매 프레임 호출 경로라 힙
        /// 할당이 없다). Config가 배선되지 않은 테스트/폴백 경로에서도 안전하도록 각 값에 기본값을 둔다.
        /// </summary>
        public StickmanPoseAnimator.PoseSettings BuildPoseSettings()
        {
            return new StickmanPoseAnimator.PoseSettings(
                IdleLegSpreadDegrees,
                IdleArmSpreadDegrees,
                Config != null ? Config.idleKneeBendDegrees : 4f,
                Config != null ? Config.idleElbowBendDegrees : 10f,
                Config != null ? Config.idleBreathAmplitude : 0.012f,
                Config != null ? Config.idleBreathFrequencyHz : 0.8f,
                Config != null ? Config.idleBreathArmDegrees : 1.5f);
        }

        /// <summary>실측 검증/디버그용 — 마지막으로 확정된 바라보는 방향 부호(+1 오른쪽 / -1 왼쪽).</summary>
        public float FacingSign => _facingSign;

        /// <summary>팔다리 각도 지수 감쇠 계수(1/초).</summary>
        public float PoseSmoothingRate => Config != null ? Config.poseSmoothingRate : 35f;

        /// <summary>보행 주파수 입력 속도의 지수 감쇠 계수(1/초).</summary>
        public float WalkSpeedSmoothingRate => Config != null ? Config.walkSpeedSmoothingRate : 6f;

        /// <summary>
        /// ParkourClimb 진입 판정(아키텍처 0절 파쿠르, UX_FLOW.md 4절). 지금 딛고 있는 발판의 진행방향
        /// 경계에 근접(parkourDetectionRadius 이내)했고, 그 경계 너머 가까이에 상단이 눈에 띄게 더 높은
        /// (parkourDetectionRadius 이상) 다른 발판이 있으면 "벽"으로 판정한다. 비슷하거나 더 낮은 발판은
        /// 파쿠르가 아니라 평범한 점프/낙하 대상이므로 제외한다. 좌표 변환/발판 순회는 전부 GroundSensor에
        /// 위임한다(States/*.cs가 직접 좌표 변환식을 만들지 않는 기존 컨벤션 유지).
        /// </summary>
        public bool TryFindClimbableWall(GroundSensor.GroundInfo info, int direction, out long wallHandle, out float wallTopWorldY)
        {
            wallHandle = 0L;
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 foot = Body != null ? Body.position : Vector2.zero;
            bool found = GroundSensor.TryFindClimbableWall(MainCamera, foot, info, direction, footholds, Config,
                out PlatformFoothold wall, out wallTopWorldY);
            if (found) wallHandle = wall.Handle;
            return found;
        }

        /// <summary>
        /// ParkourClimb 등반 도중, handle로 식별된 발판이 여전히 존재하는지 매 프레임 재확인하고 존재하면
        /// 그 발판의 최신 상단 월드 Y를 반환한다(창이 이동했을 수 있으므로 매 프레임 재계산). 존재하지
        /// 않으면 false — "잡을 곳이 사라짐(창 이동/닫힘)" 실패 처리(UX_FLOW.md 4절)에 사용한다.
        /// </summary>
        public bool TryGetFootholdTopWorldY(long handle, out float topWorldY)
        {
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 refPos = Body != null ? Body.position : Vector2.zero;
            return GroundSensor.TryGetFootholdTopWorldY(MainCamera, refPos, handle, footholds, Config, out topWorldY);
        }

        /// <summary>
        /// 매달려 내려가기(LedgeHang) 진입 판정 — TryFindClimbableWall의 반대 방향. 진행방향 경계 바깥으로
        /// 내려섰을 때 실제로 내려앉을 더 낮은 발판이 있는지 확인한다(없으면 매달리지 않고 기존 배회
        /// 거동대로 돌아선다). 좌표 변환/발판 순회는 전부 GroundSensor에 위임(BUG-M5 컨벤션).
        /// </summary>
        public bool TryFindDescendTarget(GroundSensor.GroundInfo info, int direction, out long targetHandle, out float targetTopWorldY)
        {
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 foot = Body != null ? Body.position : Vector2.zero;
            float outward = Config != null ? Config.ledgeHangEdgeOffset : 0.14f;
            // 상한 없음(0) — 매달리기는 "깊으면 깊을수록" 성립한다. 하한만이 안전 조건이다.
            return GroundSensor.TryFindDescendTarget(MainCamera, foot, info, direction, footholds, Config, outward,
                LedgeHangMinDropDepth, 0f, out targetHandle, out targetTopWorldY);
        }

        /// <summary>
        /// ★ 뛰어내리기(HopDown) 진입 판정(2026-08-29, 사용자 결정 "낙차가 작으면 뛰어내리게 한다").
        /// 위 <see cref="TryFindDescendTarget"/>와 **같은 함수**를 쓰되 낙차 밴드만 반대편을 물어본다:
        /// [<see cref="StickConfig.hopDownMinDropHeight"/>, <see cref="HopDownMaxDropHeight"/>).
        ///
        /// 두 판정은 구조적으로 상호 배타다 — HopDownMaxDropHeight의 기본값이 곧
        /// <see cref="LedgeHangMinDropDepth"/>이기 때문이다. 그래서 "둘 다 성립"(어느 쪽을 할지 모호)도
        /// "둘 다 불성립"(내려갈 곳이 있는데 아무 것도 안 함)도 생기지 않는다. 다만 목적지가 여러 개인
        /// 배치에서는 서로 **다른 발판**을 고를 수 있으므로(예: 1유닛 아래에 턱, 5유닛 아래에 바닥),
        /// 호출부(AutoWanderController)는 반드시 이쪽을 **먼저** 물어야 한다 — 실제로 발이 먼저 닿는
        /// 면은 언제나 더 가까운 쪽이고, 그 위로 매달려 지나가면 몸이 발판을 파고든다.
        /// </summary>
        public bool TryFindHopDownTarget(GroundSensor.GroundInfo info, int direction, out long targetHandle, out float targetTopWorldY)
        {
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 foot = Body != null ? Body.position : Vector2.zero;
            float outward = Config != null ? Config.hopDownProbeOutward : 0.2f;
            float minDrop = Config != null ? Config.hopDownMinDropHeight : 0.35f;
            return GroundSensor.TryFindDescendTarget(MainCamera, foot, info, direction, footholds, Config, outward,
                Mathf.Max(0.0001f, minDrop), HopDownMaxDropHeight, out targetHandle, out targetTopWorldY);
        }

        /// <summary>
        /// 매달리기가 성립하려면 필요한 **최소 낙차**(월드 유닛). 손끝~발끝 거리(<see cref="LedgeHangDropDepth"/>)가
        /// 본질이지만, 파쿠르 감지 반경보다 작아지는 퇴화 배치(팔을 못 찾는 폴백 등)에서 판정이 무의미해지지
        /// 않도록 그 값으로 바닥을 받쳐둔다 — 2026-08-29 이전에 GroundSensor 안에 하드코딩돼 있던
        /// Mathf.Max(detectionRadius, dropDepth)를 그대로 옮겨온 것이라 기존 거동과 100% 동일하다.
        /// 이 값이 곧 "매달리기 / 뛰어내리기"의 분기 임계값이다.
        /// </summary>
        public float LedgeHangMinDropDepth
        {
            get
            {
                float detectionRadius = Config != null ? Config.parkourDetectionRadius : 0.5f;
                return Mathf.Max(detectionRadius, LedgeHangDropDepth);
            }
        }

        /// <summary>
        /// 뛰어내리기로 처리할 낙차의 상한(이 값 자신은 제외). StickConfig.hopDownMaxDropHeight가 0 이하면
        /// <see cref="LedgeHangMinDropDepth"/>를 자동으로 쓴다(권장 기본값) — 그래야 두 밴드가 정확히
        /// 맞물려 틈도 겹침도 생기지 않는다.
        /// </summary>
        public float HopDownMaxDropHeight
        {
            get
            {
                float configured = Config != null ? Config.hopDownMaxDropHeight : 0f;
                return configured > 0f ? configured : LedgeHangMinDropDepth;
            }
        }

        /// <summary>
        /// 매달렸을 때 **발끝이 모서리보다 얼마나 아래로 내려가는가**(월드 유닛) = 손끝~발끝 거리.
        /// 한 값이 두 곳에서 동시에 쓰인다(그래서 여기 한 곳에서만 계산한다):
        ///   (1) LedgeHangState가 "손이 모서리에 정확히 닿는" 루트 Y를 정할 때 (모서리 Y − 이 값),
        ///   (2) TryFindDescendTarget이 "내려갈 발판이 매달린 발보다 아래인가"를 판정할 때.
        /// 프리팹의 실제 어깨 높이/팔 길이에서 유도되므로 목 길이나 팔 길이를 바꿔도 자동으로 따라온다.
        /// 팔을 찾지 못하는 폴백 경로(테스트 리그 등)에서는 파쿠르 감지 반경으로 되돌린다.
        /// </summary>
        public float LedgeHangDropDepth
        {
            get
            {
                StickmanPoseAnimator pose = GetPoseAnimator();
                float reach = pose != null ? pose.HangHandReachAboveRoot(BuildLedgeHangPoseSettings()) : 0f;
                return reach > 0.0001f ? reach : (Config != null ? Config.parkourDetectionRadius : 0.5f);
            }
        }

        /// <summary>
        /// 매달린 동안 매 프레임 호출 — 붙잡은 발판이 여전히 존재하는지 재확인하고, 존재하면 그 발판의
        /// 최신 상단 Y와 (direction 쪽) 모서리 X를 함께 돌려준다. 창이 옆으로 움직이면 붙잡은 손도 따라
        /// 움직여야 하므로 Y만으로는 부족하다. false면 "잡을 곳이 사라짐" -> 즉시 낙하.
        /// </summary>
        public bool TryGetFootholdEdgeWorld(long handle, int direction, out float topWorldY, out float edgeWorldX)
        {
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 refPos = Body != null ? Body.position : Vector2.zero;
            return GroundSensor.TryGetFootholdEdgeWorld(MainCamera, refPos, handle, direction, footholds, Config,
                out topWorldY, out edgeWorldX);
        }

        /// <summary>
        /// 매달리기 포즈 설정 묶음을 StickConfig에서 구성한다(BuildPoseSettings와 동일한 패턴 — Config가
        /// 없는 테스트/폴백 경로에서도 안전하도록 각 값에 기본값을 둔다).
        /// </summary>
        public StickmanPoseAnimator.LedgeHangPoseSettings BuildLedgeHangPoseSettings()
        {
            return new StickmanPoseAnimator.LedgeHangPoseSettings(
                Config != null ? Config.ledgeHangArmSpreadDegrees : 11f,
                Config != null ? Config.ledgeHangElbowBendDegrees : 8f,
                Config != null ? Config.ledgeHangLegSpreadDegrees : 6f,
                Config != null ? Config.ledgeHangKneeBendDegrees : 14f,
                Config != null ? Config.ledgeHangSwayAmplitudeDegrees : 5f,
                Config != null ? Config.ledgeHangSwayFrequencyHz : 0.9f);
        }
    }
}
