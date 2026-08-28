using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickMate.Platform;
using StickMate.States;
#if UNITY_STANDALONE_WIN
using StickMate.Platform.Windows;
#endif
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using StickMate.Platform.MacOS;
#endif
#if UNITY_IOS || UNITY_ANDROID
using StickMate.Platform.Mobile;
#endif

namespace StickMate.Core
{
    /// <summary>
    /// Phase 1 코어 루프의 실제 진입점. 플랫폼 서비스 선택, 발판 폴러/상태머신 생성, 매 프레임 입력
    /// 스냅샷, 클릭 관통 기본 ON 배선, 전체화면 감지 → Suspended 처리를 모두 이 MonoBehaviour가
    /// 조율한다. Rigidbody2D가 붙은 캐릭터 루트 오브젝트에 부착한다(씬/프리팹 배선은 Phase 2+에서 진행).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class StickmanAgent : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        private Rigidbody2D _body;
        private Rigidbody2D[] _allBodies; // BUG-P1-M6: Suspend()/Resume()가 전신(Phase 2 다중 파츠 Ragdoll 대비)을 순회하기 위한 캐시.
        private Camera _mainCamera;
        private IPlatformWindowService _platformService;
        private ICursorPositionService _cursorService; // 지원하는 구현체에서만 non-null (분리된 경로, ICursorPositionService.cs 참고)
        private FootholdPoller _footholdPoller;
        private StickmanStateMachine _machine;
        private StickmanBlackboard _blackboard;
        private Renderer[] _renderers;
        private AutoWanderController _autoWander; // BUG-P1-B2: 키보드 입력을 대체하는 자율 배회 소스(docs/UX_FLOW.md 26절, 매 프레임 Tick 필요).

        private float _fullscreenPollTimer;
        private bool _isSuspended;

        // ============================================================================
        // 클릭 관통 긴급 종료 안전장치("바로 바탕화면에서 구동" 라운드, 사용자 명시 요청, 2026-08-28).
        // 클릭관통이 켜지면 마우스 클릭이 우리 창을 그대로 통과해버려, 그 순간부터는 클릭으로 우리
        // 창에 다시 포커스를 줄 방법이 원천적으로 사라진다(Accessibility 권한 없이는 전역 핫키도 불가능 —
        // Unity Input 시스템은 우리 창이 키보드 포커스를 가진 동안만 입력을 받는다). 두 겹의 방어선을 둔다:
        // (1) 앱 시작 직후 ClickThroughSafetyDelaySeconds 동안은 클릭관통을 끈 채로 유지해, 그 사이
        //     사용자가 아무 데도 클릭하지 않으면 우리 창이 여전히 키 윈도우 상태를 유지한다.
        // (2) 그 동안(그리고 그 이후에도 우리 창이 키보드 포커스를 유지하는 한) EmergencyDisableKey를
        //     누르면 즉시 클릭관통을 강제로 끈다.
        // 한계(정직하게 기록): 이 두 장치 모두 "우리 창이 키보드 포커스를 잃지 않았을 때"만 유효하다 —
        // 클릭관통 상태에서 사용자가 다른 창을 클릭해 포커스가 넘어가면 이후에는 이 앱 안에서 되돌릴
        // 방법이 없다(실제 배포판이라면 메뉴바 아이콘/전역 단축키 같은 별도 UX가 필요 — 이번 라운드
        // 범위 밖). 그 경우의 최종 안전망은 터미널에서 프로세스를 직접 종료하는 것뿐이다.
        // ============================================================================
        private const float ClickThroughSafetyDelaySeconds = 5f;
        private const KeyCode EmergencyDisableKey = KeyCode.Escape;
        private bool _clickThroughDefaultEnabled;

        /// <summary>
        /// 클릭 관통(SetClickThrough)과 완전히 독립된 커서 좌표 조회 경로(UX_FLOW.md 9절-3).
        /// 지원하지 않는 플랫폼/구현체(모바일 등)에서는 항상 false.
        /// </summary>
        public bool TryGetCursorPosition(out Vector2 osScreenPosition)
        {
            if (_cursorService != null) return _cursorService.TryGetGlobalCursorPosition(out osScreenPosition);
            osScreenPosition = default;
            return false;
        }

        /// <summary>
        /// Phase 3 Interaction 레이어(드래그&던지기/로데오 커서/격파 미니게임 컨트롤러, 라이벌 스틱맨 AI)가
        /// 읽기 전용으로 접근하기 위한 통로. 이 프로퍼티들을 새로 추가한 이유: UX_FLOW.md 10~13절 기능들은
        /// 의도적으로 StickmanAgent 밖의 별도 컴포넌트(Interaction/*)로 구현되었는데(관심사 분리 — Core는
        /// Phase 3 개별 기능의 존재 자체를 몰라도 된다), 그 컴포넌트들이 상태 전이를 트리거하거나(Machine),
        /// 부분적 클릭관통 해제를 요청하거나(PlatformService as ILocalClickCaptureService), 전체화면
        /// Suspend 여부를 확인하려면(IsSuspended, 라이벌 대결의 "전체화면 감지 시 즉시 취소" 요구사항)
        /// 최소한의 읽기 접근이 필요하다. 전부 이미 존재하던 private 필드를 그대로 노출할 뿐 새 로직은 없다.
        /// </summary>
        public StickmanBlackboard Blackboard => _blackboard;

        /// <summary>부분적 클릭관통 해제(ILocalClickCaptureService)로 캐스팅해 쓰기 위한 통로.</summary>
        public IPlatformWindowService PlatformService => _platformService;

        /// <summary>전체화면 게임 감지로 현재 Suspended 상태인지 — 라이벌 대결(11절) "전체화면 감지 시
        /// 즉시 취소" 요구사항을 Interaction/RivalStickmanAgent.cs가 직접 폴링하기 위해 필요하다(라이벌은
        /// 플레이어의 StickmanStateMachine에 속하지 않으므로 아래 Suspend()의 일반 처리 대상이 아니다).</summary>
        public bool IsSuspended => _isSuspended;

        /// <summary>
        /// RAGDOLL 강제 인터럽트의 단일 진입점(아키텍처 0절). 몸통이든 사지든 어떤 파츠가 외력(충돌)을
        /// 받으면 이 메서드로 통지되어, 충격량 크기가 StickConfig.ragdollForceThreshold 이상이면 현재
        /// 능동 상태가 무엇이든(Idle/Walk/Jump/Fall/ParkourClimb/Attack) 즉시 Ragdoll로 강제 전이한다.
        /// Getup 도중에도 다시 호출되면 재인터럽트된다 — ChangeState는 이미 Ragdoll이어도 Enter()를 다시
        /// 실행해 _settleTimer를 리셋하므로, "계속 얻어맞으면 계속 ragdoll" 동작이 별도 코드 없이
        /// 보장된다(GetupState.cs 참고). 루트 파츠는 OnCollisionEnter2D가 직접 호출하고, 사지 등
        /// 비루트 파츠는 RagdollLimbImpactRelay.cs를 부착하면 같은 경로로 통지된다(실제 프리팹 배선은
        /// Phase 2 범위 밖). Phase 3부터는 판정식 자체를 States.RagdollImpactResolver로 위임한다 —
        /// States/DragThrowState.cs(던진 속도 기반)/RodeoCursorState.cs(거친 흔들기)/
        /// Interaction/RivalStickmanAgent.cs(라이벌 자신의 피격)도 동일한 판정식을 써야 해서, 이
        /// MonoBehaviour 메서드에서만 로직을 갖고 있으면 다른 순수 C# 클래스에서 재사용할 수 없었다.
        /// 공개 시그니처는 전혀 바뀌지 않았다 — 기존 호출부(OnCollisionEnter2D 등) 무수정으로 계속 동작한다.
        /// </summary>
        public void ReportExternalImpact(float impulseMagnitude)
        {
            if (_isSuspended || _machine == null || _config == null) return;
            RagdollImpactResolver.TryApplyImpact(_blackboard, impulseMagnitude);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_body == null) return;
            ReportExternalImpact(collision.relativeVelocity.magnitude * _body.mass);
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            // BUG-P1-M6 대응(Major, docs/BUG_REPORT_PHASE1.md): SetRenderersEnabled와 대칭을 맞춰
            // Suspend()/Resume()도 전신(Phase 2 다중 파츠 Active Ragdoll 대비)을 순회하도록 여기서 1회 캐싱.
            _allBodies = GetComponentsInChildren<Rigidbody2D>(true);

            _mainCamera = Camera.main;
            // BUG-P1-M1 대응(Major): 재획득 로직까지는 아니지만, 최소한 씬에 MainCamera 태그가 없어
            // 접지 판정이 영구 무력화될 수 있는 흔한 실수를 조용히 넘기지 않고 즉시 알린다.
            if (_mainCamera == null)
            {
                Debug.LogError("[StickmanAgent] Camera.main이 null입니다 — 씬에 MainCamera 태그가 붙은 카메라가 " +
                                "없으면 접지 판정이 불가능해 캐릭터가 무한 낙하할 수 있습니다(BUG-P1-M1).");
            }

            _renderers = GetComponentsInChildren<Renderer>(true);

            _platformService = CreatePlatformService();
            _cursorService = _platformService as ICursorPositionService;
            _footholdPoller = new FootholdPoller(_platformService, _config);

            _blackboard = new StickmanBlackboard
            {
                Body = _body,
                MainCamera = _mainCamera,
                Config = _config,
                FootholdPoller = _footholdPoller,
            };

            // BUG-P1-B2 대응(Blocker): 키보드 입력을 완전히 폐기하고 docs/UX_FLOW.md 26절 자율 배회 AI
            // 스펙의 정식 구현으로 대체. 인스턴스마다 독립된 RNG를 주입해(26-3) 향후 Phase 5 세포분열로
            // 여러 개체가 동시에 존재해도 전부 같은 패턴으로 움직이지 않게 한다.
            _autoWander = new AutoWanderController(_blackboard, _config, new System.Random(System.Guid.NewGuid().GetHashCode()));
            _blackboard.IntentSource = _autoWander;
            // 26-4 훅 예약(Phase 2 커서 근접 반응 선반영 스펙) — 지금은 AutoWanderController가 이 값을
            // 읽지 않는다. Phase 2에서 실제 반응 로직을 채울 때 다시 배선할 필요가 없도록 미리 연결만 해둔다.
            _autoWander.CursorProvider = TryGetCursorPosition;
            // Phase 3: 드래그&던지기(DragThrowState)/로데오 커서(RodeoCursorState)가 커서 월드 좌표를
            // 조회하기 위한 별도 배선(같은 메서드 그룹을 가리키는 다른 델리게이트 인스턴스일 뿐).
            _blackboard.CursorProvider = TryGetCursorPosition;
            // Phase 5(UX_FLOW.md 20절): 가출(RunawayState)이 은신 중 렌더러를 숨기고 발견 시 다시
            // 보이게 하기 위한 통로. 이미 존재하는 private SetRenderersEnabled를 그대로 노출할 뿐
            // 새 메서드를 만들지 않는다(CursorProvider와 동일한 관례).
            _blackboard.SetCharacterVisible = SetRenderersEnabled;

            var states = new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Idle, new IdleState(_blackboard) },
                { StickmanStateId.Walk, new WalkState(_blackboard) },
                { StickmanStateId.Jump, new JumpState(_blackboard) },
                { StickmanStateId.Fall, new FallState(_blackboard) },
                { StickmanStateId.ParkourClimb, new ParkourClimbState(_blackboard) },
                // Phase 3: AttackState도 나머지 상태와 동일하게 블랙보드 주입 생성자로 전환(실제 Tick()
                // 완료/복귀 로직이 이번에 함께 구현됨 — Interaction/RivalStickmanAgent.cs의 유일한 사용처).
                { StickmanStateId.Attack, new AttackState(_blackboard) },
                { StickmanStateId.Ragdoll, new RagdollState(_blackboard) },
                { StickmanStateId.Getup, new GetupState(_blackboard) },
                // Phase 3 신규(UX_FLOW.md 10/12/13절) — 전부 Interaction/* 컨트롤러가 부분적 클릭관통
                // 해제/SpectacleEventLock을 확보한 뒤에만 ChangeState를 호출한다(States/*.cs는 그 획득
                // 절차를 전혀 모른다). 11종을 전부 등록해두는 이유는 위와 동일(BUG-M2 방어 코드를 밟을
                // 일 자체를 없앰).
                { StickmanStateId.BattleMinigame, new BattleMinigameState(_blackboard) },
                { StickmanStateId.Dragged, new DragThrowState(_blackboard) },
                { StickmanStateId.RodeoCursor, new RodeoCursorState(_blackboard) },
                // Phase 4 신규(UX_FLOW.md 27절) — 창 도둑만 자체 대사/페이즈 로직이 있어 전용 State
                // 클래스(WindowTheftState)를 쓰고, 나머지 4개(그라피티/청소부/블랙홀/크래시 스윙)는
                // "물리/입력 변경 없는 순수 타이머" 공통 형태라 하나의 재사용 클래스(TimedSpectacleState)를
                // 지속시간 선택자만 다르게 주입해 인스턴스화한다(States/TimedSpectacleState.cs 문서 참고).
                { StickmanStateId.WindowTheft, new WindowTheftState(_blackboard) },
                { StickmanStateId.Graffiti, new TimedSpectacleState(_blackboard, StickmanStateId.Graffiti,
                    cfg => UnityEngine.Random.Range(cfg.graffitiHoldDurationMin, cfg.graffitiHoldDurationMax)) },
                { StickmanStateId.DesktopTidy, new TimedSpectacleState(_blackboard, StickmanStateId.DesktopTidy,
                    cfg => cfg.desktopTidyDurationSeconds) },
                { StickmanStateId.BlackholeSummon, new TimedSpectacleState(_blackboard, StickmanStateId.BlackholeSummon,
                    cfg => cfg.blackholeDurationSeconds) },
                { StickmanStateId.WindowCrash, new TimedSpectacleState(_blackboard, StickmanStateId.WindowCrash,
                    cfg => cfg.windowCrashSwingDuration) },
                // Phase 5 신규(UX_FLOW.md 17/18/19절) — 전부 TimedSpectacleState의 일반화된 대사 지원을
                // 재사용한다(Phase 4의 4개 등록과 동일한 컨벤션, States/TimedSpectacleState.cs 문서 참고).
                // 가출(Runaway)만 다중 페이즈/텔레포트/렌더러 토글이 필요해 전용 States/RunawayState.cs를 쓴다.
                { StickmanStateId.TodoReminder, new TimedSpectacleState(_blackboard, StickmanStateId.TodoReminder,
                    cfg => cfg.todoReminderHoldSeconds, TodoListModel.ConsumePendingReminderText) },
                { StickmanStateId.FocusStart, new TimedSpectacleState(_blackboard, StickmanStateId.FocusStart,
                    cfg => cfg.pomodoroStartPoseHoldSeconds, cfg => "좋아, 감시 시작") },
                { StickmanStateId.FocusComplete, new TimedSpectacleState(_blackboard, StickmanStateId.FocusComplete,
                    cfg => cfg.pomodoroCompletePoseHoldSeconds, cfg => "수고했어!") },
                { StickmanStateId.FocusCancelled, new TimedSpectacleState(_blackboard, StickmanStateId.FocusCancelled,
                    cfg => cfg.pomodoroCancelPoseHoldSeconds, cfg => "그래 쉬자") },
                { StickmanStateId.FocusNudge, new TimedSpectacleState(_blackboard, StickmanStateId.FocusNudge,
                    cfg => cfg.pomodoroNudgeDialogueHoldSeconds, cfg => "어? 딴 데 보고 있네?") },
                { StickmanStateId.Sulky, new TimedSpectacleState(_blackboard, StickmanStateId.Sulky,
                    cfg => cfg.stressSulkyHoldSeconds, cfg => "아 몰라...") },
                { StickmanStateId.Runaway, new RunawayState(_blackboard) },
            };

            // BUG-P1-M2 대응(Major, docs/BUG_REPORT_PHASE1.md): 생성과 "최초 상태 활성화"를 분리했다.
            // 생성자는 더 이상 즉시 ChangeState를 호출하지 않으므로, blackboard.Machine을 먼저 완전히
            // 배선한 뒤에 Start()를 호출하면 "초기 상태의 Enter()가 무엇을 참조하든 Machine이 null일 수
            // 있는" 경우의 수 자체가 구조적으로 사라진다(우연이 아니라 보증).
            _machine = new StickmanStateMachine(states);
            _blackboard.Machine = _machine;
            _machine.Start(StickmanStateId.Idle);
        }

        private void Start()
        {
            // BUG-P1-M3 대응(Major, docs/BUG_REPORT_PHASE1.md): 반환값을 버리지 않고 확인한다. 실패해도
            // 여기서 흐름을 막지는 않는다(에디터/Null 폴백 등은 애초에 오버레이 개념이 없어 항상 true) —
            // 다만 실패를 조용히 삼키지 않고 로그로 남겨, 가설 H4(부트스트랩 타이밍에 핸들이 Zero) 같은
            // 진단 사각지대를 없앤다.
            bool overlayReady = _platformService.CreateOverlayWindow();
            if (!overlayReady)
            {
                Debug.LogWarning("[StickmanAgent] CreateOverlayWindow() 실패 — 오버레이 핸들을 확보하지 못했습니다(BUG-P1-M3).");
            }

            _clickThroughDefaultEnabled = _config != null ? _config.clickThroughDefaultEnabled : true;
            try
            {
                // 항상위는 클릭관통과 달리 "우리 창을 다시 조작할 수단을 잃는" 위험이 없으므로(마우스/
                // 키보드 입력은 그대로 받는다) 지연 없이 즉시 적용한다.
                // 주의(BUG-B1, docs/BUG_REPORT_PHASE0.md Blocker): Win32WindowService는 아직 진짜
                // 분리된 오버레이 창이 없어(게임 자신의 창을 재사용하는 스텁), 안전 가드가
                // NotSupportedException을 던지도록 막아뒀다. 진짜 오버레이 HWND 구현 전까지 Windows
                // 에서는 이 호출이 의도적으로 실패한다 — 버그가 아니라 "게임 창 자체가 최상단 고정되는"
                // 훨씬 나쁜 결과를 막기 위한 임시 안전장치다. macOS는 이제 실제 네이티브 플러그인으로
                // 진짜 동작한다(Platform/MacOS/MacWindowService.cs).
                _platformService.SetAlwaysOnTop(true);
            }
            catch (System.NotSupportedException ex)
            {
                Debug.LogWarning("[StickmanAgent] 항상위 배선을 건너뜀 — 진짜 오버레이 창 구현 전까지 " +
                                  "안전 가드가 활성화되어 있습니다(BUG-B1 참고): " + ex.Message);
            }

            // 비침해 원칙 2: 클릭 관통 기본 ON — 다만 위 클래스 상단 "클릭 관통 긴급 종료 안전장치"
            // 문서가 설명하듯, 켜지는 순간 우리 창을 다시 클릭할 수 없게 될 위험이 있어 즉시 켜지 않고
            // ClickThroughSafetyDelaySeconds만큼 지연시킨다(그 사이 EmergencyDisableKey/Update() 참고로
            // 언제든 되돌릴 수 있음을 사용자가 확인할 시간을 번다).
            StartCoroutine(EnableClickThroughAfterSafetyDelay());
        }

        private IEnumerator EnableClickThroughAfterSafetyDelay()
        {
            yield return new WaitForSeconds(ClickThroughSafetyDelaySeconds);
            ApplyClickThrough(_clickThroughDefaultEnabled);
        }

        /// <summary>
        /// "앱 시작 시 SetClickThrough 호출 지점"(비침해 원칙 2)과 EmergencyDisableKey 긴급 해제
        /// 경로가 공유하는 단일 진입점 — BUG-B1 가드 실패를 동일한 방식으로 흡수한다.
        /// </summary>
        private void ApplyClickThrough(bool enabled)
        {
            try
            {
                _platformService.SetClickThrough(enabled);
            }
            catch (System.NotSupportedException ex)
            {
                Debug.LogWarning("[StickmanAgent] 클릭 관통 배선을 건너뜀 — 진짜 오버레이 창 구현 전까지 " +
                                  "안전 가드가 활성화되어 있습니다(BUG-B1 참고): " + ex.Message);
            }
        }

        private void Update()
        {
            // 클릭 관통 긴급 종료 안전장치(클래스 상단 문서 참고) — Suspended 여부와 무관하게 항상 먼저
            // 확인한다(다른 모든 early-return보다 위에 둬서, 어떤 상태에서도 이 키만은 항상 반응하게).
            // Unity Input 시스템은 우리 창이 키보드 포커스를 가진 동안만 이 입력을 받을 수 있다는 한계가
            // 있다(전역 핫키가 아님 — 클릭관통으로 포커스를 완전히 잃으면 이 경로도 함께 무력화된다,
            // 클래스 상단 문서의 "한계" 절 참고).
            if (Input.GetKeyDown(EmergencyDisableKey))
            {
                Debug.Log($"[StickmanAgent] {EmergencyDisableKey} 눌림 — 클릭 관통 긴급 강제 OFF.");
                ApplyClickThrough(false);
            }

            float dt = Time.deltaTime;

            TickFullscreenSuspend(dt);
            if (_isSuspended) return; // Suspended 동안 Tick 자체를 건너뛰어 상태/파라미터/물리를 그대로 보존.

            _footholdPoller.Tick(dt);

            // BUG-P1-B2 대응(Blocker): 예전에는 여기서 UnityEngine.Input을 직접 폴링해 블랙보드에
            // 대입했지만, 이제 유일한 이동 의도 출처는 IMovementIntentSource(_autoWander)이며
            // blackboard.MoveInputX/JumpPressed는 그 소스를 읽는 계산된 프로퍼티다(StickmanBlackboard.cs
            // 참고) — 여기서는 그 소스의 내부 타이머만 갱신해주면 된다.
            _autoWander.Tick(dt);

            _machine.Tick(dt);
        }

        private void TickFullscreenSuspend(float deltaTime)
        {
            _fullscreenPollTimer += deltaTime;
            float interval = _config != null ? Mathf.Max(0.1f, _config.fullscreenPollInterval) : 1f;
            if (_fullscreenPollTimer < interval) return;
            _fullscreenPollTimer = 0f;

            bool fullscreenActive = _platformService.IsFullscreenAppActive();
            if (fullscreenActive && !_isSuspended) Suspend();
            else if (!fullscreenActive && _isSuspended) Resume();
        }

        private void Suspend()
        {
            _isSuspended = true;

            // Phase 3 예외(UX_FLOW.md 10/12/13절): 격파 미니게임/드래그&던지기/로데오 커서는 "능동 개입"
            // 스펙터클이라 전체화면 감지 시 일반 Suspend(상태 보존 후 재개)가 아니라 즉시 취소되어야
            // 한다 — "비침해 원칙이 항상 이 기능들보다 우선"이라고 세 절 모두 명시적으로 못박았다.
            // RAGDOLL/GETUP/ParkourClimb 등 물리 기반 상태는 아래의 일반 Suspend(보존)를 그대로 유지한다.
            // ChangeState(Idle, isForcedInterrupt:true)가 각 상태의 Exit()을 실행시켜 Kinematic->Dynamic
            // 복구(DragThrowState/RodeoCursorState) 및 StateTransitioned 발행(Interaction 컨트롤러들의
            // 락 해제 트리거, DragThrowController/BattleMinigameDirector/RodeoCursorWatcher 참고)을
            // 자연스럽게 유발한다 — 이 메서드는 그 사실만 트리거할 뿐 락 해제 자체에는 관여하지 않는다.
            // Phase 4 확장(UX_FLOW.md 27절 각 절, "전체화면 게임 감지 시 즉시 취소" 공통 예외 상태):
            // 창 도둑/그라피티/청소부/블랙홀/크래시(캐릭터 스윙 쪽)도 동일한 이유로 이 강제 목록에 편입.
            // 창 크래시 오버레이 자체(3초 수명)는 이 상태와 독립적이라 Interaction/WindowCrashDirector.cs가
            // IsSuspended를 직접 폴링해 별도로 취소한다(RivalStickmanAgent의 IsSuspended 폴링과 동일 패턴).
            StickmanStateId current = _machine.CurrentStateId;
            if (current == StickmanStateId.Dragged || current == StickmanStateId.RodeoCursor ||
                current == StickmanStateId.BattleMinigame || current == StickmanStateId.WindowTheft ||
                current == StickmanStateId.Graffiti || current == StickmanStateId.DesktopTidy ||
                current == StickmanStateId.BlackholeSummon || current == StickmanStateId.WindowCrash)
            {
                _machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }

            // 상태/파라미터 보존(UX_FLOW.md 6-4절/9절-4, "IDLE 리셋 금지"): 상태 인스턴스를 파괴하거나
            // Idle로 되돌리지 않고 단순히 Tick 호출 자체를 건너뛴다 — 진행 중이던 상태의 내부 타이머
            // (예: FallState._landingConfirmTimer)가 그대로 멈춰 있다가 Resume() 이후 이어서 진행된다.
            SetBodiesSimulated(false); // 물리 시뮬레이션도 함께 멈춰 숨겨진 동안 위치가 흐트러지지 않게 함.
            SetRenderersEnabled(false);
            // TODO(Phase 2 렌더링 레이어): 즉시 on/off 대신 ≤200ms 페이드 아웃/인 연출 추가.
        }

        private void Resume()
        {
            _isSuspended = false;
            SetBodiesSimulated(true);
            // BUG-P5-M1 대응(Major, docs/BUG_REPORT_PHASE5.md): 예전에는 여기서 무조건
            // SetRenderersEnabled(true)를 호출해, 가출(RunawayState) Hidden 페이즈 중 전체화면 Suspend/
            // Resume이 왕복하면 아직 발견되지 않은 캐릭터가 강제로 다시 보이게 되는 버그가 있었다.
            // RunawayState가 자신의 은신 가시성 의도를 IsCharacterHiddenByRunaway로 알려오는 동안은
            // 이 무조건 복원을 건너뛴다(StickmanBlackboard.IsCharacterHiddenByRunaway 문서 참고) —
            // "Suspend/Resume의 렌더러 제어"가 "Runaway의 렌더러 제어"를 마지막에 실행됐다는 이유만으로
            // 덮어쓰지 않게 한다.
            if (_blackboard == null || !_blackboard.IsCharacterHiddenByRunaway)
            {
                SetRenderersEnabled(true);
            }
            // Minor m4 대응(docs/BUG_REPORT_PHASE1.md): Suspended 동안 FootholdPoller.Tick()도 함께
            // 건너뛰어(Update() 조기 return) 캐시가 오래됐을 수 있다 — 재개 즉시 최신 발판으로 갱신해
            // 다음 폴링 주기(최대 footholdPollInterval)까지 스테일 캐시로 서 있는 것처럼 보이지 않게 한다.
            _footholdPoller.PollImmediately();
        }

        // BUG-P1-M6 대응(Major): 루트 하나의 Rigidbody2D만 토글하던 것을 전신(Phase 2 다중 파츠 Active
        // Ragdoll 대비, Awake()에서 GetComponentsInChildren<Rigidbody2D>(true)로 캐싱)으로 일반화 —
        // SetRenderersEnabled와 대칭을 맞춘다.
        private void SetBodiesSimulated(bool simulated)
        {
            if (_allBodies == null) return;
            for (int i = 0; i < _allBodies.Length; i++)
            {
                if (_allBodies[i] != null) _allBodies[i].simulated = simulated;
            }
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = enabled;
            }
        }

        private IPlatformWindowService CreatePlatformService()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // BUG-P1-B1 대응(Blocker, docs/BUG_REPORT_PHASE1.md): Win32WindowService.EnumerateFootholds()가
            // "제목 있는 가시 창"을 하나도 못 찾으면(모든 창 최소화 등 흔한 상황) 빈 리스트를 반환해
            // GroundedTick/CheckScreenBoundsOrFall 둘 다 무력화되고 캐릭터가 화면 밖으로 무한 낙하한다.
            // FallbackPlatformWindowService 데코레이터로 감싸 "화면 하단 합성 발판 1개" 안전망을 항상
            // 보장한다(NullPlatformWindowService의 더미 발판과 동일한 개념을 실제 데스크톱 구현체에 이식).
            //
            // `&& !UNITY_EDITOR`(2026-08-28, Architect 대칭 보강): macOS 네이티브 창 열거 작업 중 Coder가
            // 실측으로 확인한 사실 — Unity 에디터는 활성 빌드 타깃이 그 플랫폼이면 에디터 컴파일
            // 컨텍스트 자체에도 해당 STANDALONE 심볼이 함께 정의된다(UNITY_EDITOR와 배타가 아님). 이
            // 프로젝트의 활성 빌드 타깃이 지금까지 계속 macOS여서 이 Windows 분기가 에디터에서 실제로
            // 컴파일된 적이 없었을 뿐, 나중에 Windows 개발자가 이 프로젝트를 열고 활성 빌드 타깃을
            // Windows로 바꾸는 순간 이 가드 없이는 에디터 Play/배치모드 실측이 전부 조용히
            // Win32WindowService로 바뀌어(NullPlatformWindowService의 더미 발판을 안 쓰게 됨) 지금까지
            // 쌓인 모든 실측 검증 전제가 깨진다. macOS 분기(아래)의 동일 가드와 대칭을 맞춘다.
            return new FallbackPlatformWindowService(new Win32WindowService(), _config);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            // macOS 네이티브 창 열거 도입(docs/BUG_REPORT_PHASE0.md m8 해소, Tasklist.md "macOS 네이티브
            // 창 열거" 절 참고) — Win32와 동일하게 "제목/레이어 필터를 통과한 창을 하나도 못 찾으면
            // GroundedTick/CheckScreenBoundsOrFall이 무력화되어 무한 낙하"하는 동일한 위험이 있으므로
            // 동일한 FallbackPlatformWindowService 안전망으로 감싼다.
            //
            // `&& !UNITY_EDITOR`가 반드시 필요한 이유(Win32 분기와의 차이점, 실측으로 확인됨): Unity
            // 에디터는 "활성 빌드 타깃"이 macOS Standalone으로 설정되어 있으면 에디터 컴파일 컨텍스트
            // 자체에도 UNITY_EDITOR와 UNITY_STANDALONE_OSX가 동시에 정의된다(Windows 대상일 때
            // UNITY_STANDALONE_WIN이 마찬가지로 에디터에도 동시 정의되는 것과 같은 매커니즘 — Win32
            // 분기에 이 가드가 없는 것은 이 프로젝트의 활성 빌드 타깃이 지금까지 macOS였고 Windows
            // 브랜치가 에디터에서 실제로 컴파일된 적이 없었기 때문일 뿐, 안전해서가 아니다). 이 프로젝트의
            // 모든 실측 플레이테스트(PlayMode 스모크 테스트, EditMode 13종)는 에디터 배치모드로
            // 실행되며 NullPlatformWindowService의 더미 발판에 의존하고 있으므로, 이 가드가 없으면
            // 에디터 실행 시에도 MacWindowService(CoreGraphics P/Invoke)가 조용히 활성화되어 그 실측
            // 결과가 전부 달라진다 — 실제 macOS 앱 빌드(Player)에서만 MacWindowService를 쓰도록
            // `!UNITY_EDITOR`로 명시적으로 분리한다.
            //
            // BUG-P1-R5-B3 조사 대응(Architect 실측 진단, 2026-08-28) — Retina 화면에서 실제 OS 창
            // 좌표(AppKit 포인트)와 Unity Screen.width/height(백킹 픽셀)가 서로 다른 단위를 쓰는 문제를
            // 보정하기 위해, 이 서비스를 안전망으로 감싸기 전에 실제 화면의 backingScaleFactor를 조회해
            // `_config.desktopDpiScale`에 1회 적용한다(MacWindowService.DetectDesktopDpiScale() 문서
            // 참고). `_config`는 씬에 배선된 ScriptableObject 인스턴스를 그대로 참조하므로 이 대입은
            // 그 자산 "파일"을 수정하는 게 아니라 이번 실행의 메모리상 값만 갱신한다 — 다음 실행 때는
            // 다시 이 코드가 그 시점 화면 기준으로 재계산한다.
            var macService = new MacWindowService();
            if (_config != null)
            {
                _config.desktopDpiScale = macService.DetectDesktopDpiScale();
                Debug.Log($"[StickmanAgent] macOS 실제 화면 배율 감지 — desktopDpiScale={_config.desktopDpiScale:F3}로 설정(1.0=비Retina, 0.5=Retina 2x).");
            }
            return new FallbackPlatformWindowService(macService, _config);
#elif UNITY_IOS || UNITY_ANDROID
            // 모바일 발판/배경 설정 자체(SetBackdropScreenshot/AddUserDefinedFoothold)는 UX 온보딩
            // 흐름이 별도로 호출한다(docs/UX_FLOW.md 1-B/3절) — 여기서는 서비스 인스턴스만 만들어 배선한다.
            // 주의: 이 서비스는 FallbackPlatformWindowService로 감싸지 않는다 — EnumerateFootholds()의
            // 빈 결과는 버그가 아니라 "유저가 아직 발판을 탭 지정하지 않음"이라는 의도된 신호이고,
            // ScreenshotBackdropPlatformService.IsConfigured가 이 상태를 감지해 온보딩을 노출해야 한다
            // (UX_FLOW.md 3절/9절-7). 여기서 항상 발판이 있는 것처럼 위장하면 그 온보딩 게이트가
            // 조용히 무력화된다.
            return new ScreenshotBackdropPlatformService();
#else
            // 에디터(모든 활성 빌드 타깃 공통) 및 그 외 미지원 조합 폴백. macOS 실빌드는 위
            // UNITY_STANDALONE_OSX && !UNITY_EDITOR 분기가 전담하므로 이 분기로 내려오지 않는다.
            // NullPlatformWindowService는 이미 항상 더미 발판을 반환하므로 FallbackPlatformWindowService로
            // 감쌀 필요가 없다(불필요한 간접 계층 추가 방지).
            return new NullPlatformWindowService();
#endif
        }
    }
}
