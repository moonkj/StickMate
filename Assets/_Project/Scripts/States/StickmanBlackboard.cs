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
        /// Attack(전투) 상태의 IHasDialogueParams 스냅샷 입력값(BUG-M7 파이프라인, docs/BUG_REPORT_PHASE3.md
        /// Minor 1 대응). 호출자(현재는 Interaction/RivalStickmanAgent.cs)가 Machine.ChangeState(Attack)를
        /// 호출하기 직전에 이번 타격 이후 "몇 대 더 맞아야 결판나는지"를 계산해 이 필드에 써두면,
        /// AttackState.Enter()가 그 값을 그대로 스냅샷해 "한 발 더!"(&gt;=1)/"오늘은 여기까지"(0) 대사를
        /// 파생시킨다. 아무도 세팅하지 않고 ChangeState(Attack)만 호출하면 기본값 0("오늘은 여기까지")
        /// 그대로 유지된다 — 값을 채우지 않는 호출부에서도 안전한 기본 동작.
        /// </summary>
        public int AttackShotsRemaining;

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
        /// </summary>
        public bool TryFindLandingCrossing(Vector2 prevFootWorldPos, Vector2 currFootWorldPos, out long handle, out float landingWorldY)
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            return GroundSensor.TryFindLandingCrossing(MainCamera, prevFootWorldPos, currFootWorldPos, footholds, Config,
                out handle, out landingWorldY);
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
                SnapToGround(info);
                return false;
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

        private void SnapToGround(GroundSensor.GroundInfo info)
        {
            if (Body == null) return;
            Vector2 pos = Body.position;
            if (Mathf.Abs(pos.y - info.GroundWorldY) > 0.001f)
            {
                Body.position = new Vector2(pos.x, info.GroundWorldY);
            }
            if (Body.linearVelocity.y < 0f)
            {
                Vector2 v = Body.linearVelocity;
                v.y = 0f;
                Body.linearVelocity = v;
            }
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

        /// <summary>화면 경계에서 남겨둘 여유(OS 포인트). 캐릭터 몸이 절반쯤 걸치는 것도 막는다.</summary>
        private const float ScreenClampMarginOsPx = 8f;

        /// <summary>이 시간(초) 넘게 Fall이 이어지면 "유효 발판을 완전히 잃었다"고 보고 리스폰한다.</summary>
        private const float LostCharacterRescueSeconds = 6f;

        private float _fallStuckTimer;
        private long _lastReportedFootholdHandle = long.MinValue;

        /// <summary>
        /// 매 프레임 마지막에 호출 — (1) 캐릭터 OS 좌표를 오버레이 창(=화면) 안으로 하드 클램프하고,
        /// (2) 그래도 발판을 완전히 잃은 채 오래 낙하 중이면 화면 중앙 지면으로 강제 복귀시킨다.
        /// </summary>
        public void EnforceScreenBoundsAndRescue(float deltaTime)
        {
            if (Body == null || MainCamera == null) return;

            float dpi = Config != null ? Mathf.Max(0.0001f, Config.desktopDpiScale) : 1f;
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            float screenW = (Screen.width > 0 ? Screen.width : 1920) * dpi;
            float screenH = (Screen.height > 0 ? Screen.height : 1080) * dpi;

            Vector2 os = ScreenCoordinateConverter.WorldToOsScreen(MainCamera, Body.position, Config, out float depth);
            float minX = origin.x + ScreenClampMarginOsPx;
            float maxX = origin.x + screenW - ScreenClampMarginOsPx;
            float minY = origin.y + ScreenClampMarginOsPx;
            float maxY = origin.y + screenH - ScreenClampMarginOsPx;

            float clampedX = Mathf.Clamp(os.x, minX, maxX);
            float clampedY = Mathf.Clamp(os.y, minY, maxY);
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
                Debug.Log($"[화면클램프] 캐릭터가 화면 밖으로 나가려 해 되돌렸습니다 — OS ({os.x:F1},{os.y:F1}) -> " +
                    $"({clampedX:F1},{clampedY:F1}), 화면=({origin.x:F0},{origin.y:F0} {screenW:F0}x{screenH:F0}), " +
                    $"상태={(Machine != null ? Machine.CurrentStateId.ToString() : "?")}.");
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
        /// 캐릭터를 화면 가로 중앙으로 옮기고, 그 X에서 딛을 수 있는 가장 높은 발판(없으면 합성 안전망이
        /// 항상 있으므로 사실상 항상 존재한다) 위에 세운 뒤 Idle로 되돌린다. 리더 지시 7항.
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
            if (TryGetGroundSurfaceWorldY(probe, out float surfaceY)) targetY = surfaceY;

            Body.position = new Vector2(centerWorld.x, targetY);
            Body.transform.position = new Vector3(centerWorld.x, targetY, Body.transform.position.z);
            Body.linearVelocity = Vector2.zero;
            CurrentFootholdHandle = 0L; // 재획득하도록 초기화
            ResetGroundLossTimer();
            Machine?.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            Debug.Log($"[캐릭터구조] {LostCharacterRescueSeconds}초 이상 착지하지 못해 강제 복귀시켰습니다 — " +
                $"월드 {before} -> ({centerWorld.x:F3},{targetY:F3}) (화면 가로 중앙의 지면). " +
                "사용자가 캐릭터를 잃어버리지 않게 하는 최종 안전망입니다(리더 지시 7항).");
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
                _ragdollRig = new RagdollRig(Body.transform);
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

            // 눈은 상태와 무관하게 항상 갱신한다(머리의 자식이라 RAGDOLL 중에도 머리를 따라간다).
            // 지금은 항상 정면 — 다음 라운드에 커서 추적을 여기에 연결한다(EyeController.cs 문서의
            // "다음 라운드 배선 지점" 참고).
            GetEyeController()?.LookForward();

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
    }
}
