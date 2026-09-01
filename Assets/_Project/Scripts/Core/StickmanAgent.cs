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

        /// <summary>
        /// ★ 몸 바깥에서 잉크를 얹는 부품(액세서리/펫/FX)의 <b>단일 창구</b>(2026-08-31).
        /// 위 <see cref="_renderers"/>는 Awake 시점 스냅샷이라 <b>런타임 생성되는 것을 영원히 못 본다</b> —
        /// 그 하나의 뿌리에서 원칙 2 위반(전체화면 감지 시 몸 없는 모자가 남는다) / 획 두께 하한 미적용 /
        /// 화면 여백 누락이 동시에 나왔다. 소비자 셋(숨기기·획두께·시각반폭)은 전부 이 창구를 본다.
        /// </summary>
        private readonly CharacterVisualRegistry _dynamicVisuals = new CharacterVisualRegistry();

        /// <summary>테스트/진단용 — 위 단일 창구. 읽는 쪽은 <c>Refresh()</c>를 먼저 부른다.</summary>
        public CharacterVisualRegistry DynamicVisuals => _dynamicVisuals;

        /// <summary>물리 반폭 실측용 콜라이더 캐시(2026-08-30 R3-M1). Awake에서 한 번만 모은다.</summary>
        private Collider2D[] _colliders;
        private LineRenderer[] _lineRenderers; // 색 프리셋 일괄 갱신(ApplyInkColor) 대상 캐시.
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
        /// Phase 3 Interaction 레이어(드래그&던지기/로데오 커서/격파 미니게임 컨트롤러)가
        /// 읽기 전용으로 접근하기 위한 통로. 이 프로퍼티들을 새로 추가한 이유: UX_FLOW.md 10~13절 기능들은
        /// 의도적으로 StickmanAgent 밖의 별도 컴포넌트(Interaction/*)로 구현되었는데(관심사 분리 — Core는
        /// Phase 3 개별 기능의 존재 자체를 몰라도 된다), 그 컴포넌트들이 상태 전이를 트리거하거나(Machine),
        /// 부분적 클릭관통 해제를 요청하거나(PlatformService as ILocalClickCaptureService), 전체화면
        /// Suspend 여부를 확인하려면(IsSuspended — "전체화면 감지 시 즉시 취소" 요구사항)
        /// 최소한의 읽기 접근이 필요하다. 전부 이미 존재하던 private 필드를 그대로 노출할 뿐 새 로직은 없다.
        /// </summary>
        public StickmanBlackboard Blackboard => _blackboard;

        /// <summary>부분적 클릭관통 해제(ILocalClickCaptureService)로 캐스팅해 쓰기 위한 통로.</summary>
        public IPlatformWindowService PlatformService => _platformService;

        /// <summary>
        /// 이 에이전트가 쓰는 설정 에셋. Interaction/AppControlDirector.cs(앱 제어 메뉴/단축키)가
        /// 잉크색·로데오·진단로그 토글을 위해 읽고 쓴다 — Blackboard.Config로도 같은 인스턴스에
        /// 닿을 수 있지만, "설정을 바꾸는" 소비자가 상태머신용 블랙보드를 경유하는 것은 의미가
        /// 어긋나므로 별도 통로로 노출한다(기존 private 필드를 그대로 내보낼 뿐 새 로직은 없다).
        /// </summary>
        public StickConfig Config => _config;

        /// <summary>전체화면 게임 감지로 현재 Suspended 상태인지 — "전체화면 감지 시 즉시 취소"가
        /// 필요한데 아래 Suspend()의 일반 처리(상태머신 강제 전이) 대상이 아닌 소비자들이 직접
        /// 폴링한다(WindowCrashDirector의 오버레이 수명, 정보창/부채꼴/팝오버의 자동 닫기 등).</summary>
        public bool IsSuspended => _isSuspended;

        // ============================================================================
        // ★ 캐릭터 전신 높이의 단일 소스 (리더 지시 2026-08-29 — 크기 조정 가능해야 함)
        // ============================================================================
        // 왜 필요한가: 사용자가 "캐릭터 사이즈가 지금의 절반 정도 되어야 하고 추후 조정 가능해야 한다"고
        // 요구했다. 머리 위 연출(말풍선/하드웨어 이모트)과 정면 연출(격파 미니게임)이 지금처럼 절대
        // 유닛 상수로 위치를 잡고 있으면, 캐릭터 크기가 바뀌는 순간 전부 다시 겹친다 — 실제로 이번
        // 라운드의 "이모트가 머리와 겹친다"가 정확히 그 방식(값 하나만 올림)으로 생긴 버그다.
        //
        // 왜 루트 CapsuleCollider2D인가: Assets/Editor/SceneBootstrapper.cs의 BuildStickmanPrefab이
        // 전신 높이를 `totalHeight`로 계산한 뒤 **그대로** 루트 물리 캡슐에 대입한다
        // (`capsule.size = new Vector2(0.4f, totalHeight)`). 즉 프리팹 안에 이미 정답이 들어 있어
        // 부트스트래퍼를 고치지 않고도 읽을 수 있고, 지오메트리를 바꾸면 이 값이 자동으로 따라온다.
        //
        // 루트에는 캡슐이 둘이다 — 물리 몸통(isTrigger=false, 높이=totalHeight)과 클릭용 GrabArea
        // (isTrigger=true, 높이=totalHeight + 여유*2). **isTrigger가 아닌 쪽**만 골라야 정확하다.
        //
        // 렌더러 바운즈로 재지 않는 이유: 포즈에 따라(팔을 들면) 매 프레임 값이 흔들려 머리 위 연출이
        // 같이 떨린다. 전신 높이는 "이 캐릭터의 규격"이지 "지금 자세의 크기"가 아니다.

        // ★★ 2026-08-29 통합(리더 지시) — 생산자는 Core/StickmanMetrics.cs 하나다.
        // ============================================================================
        // 이 프로퍼티와 StickmanMetrics가 같은 라운드에 각각 만들어져 **캐릭터 치수의 단일 소스가 두
        // 개**가 되어 있었다(이 프로젝트가 이미 여러 번 겪은 실패 유형 — Dock 구간 이중 계산,
        // 씬 지면 Y vs 발판 상수 이중 정의). 크기 배율(StickConfig.characterScale)을 소유하는 쪽이
        // StickmanMetrics이므로 그쪽을 유일한 생산자로 삼고, 이 프로퍼티는 **얇은 위임**만 남긴다.
        //
        // 이 프로퍼티를 지우지 않는 이유: 이미 커밋된 렌더러 3종(DialogueBubbleRenderer /
        // HardwareReactionRenderer / BattleMinigameRenderer)이 이 이름으로 값을 읽고 있다. 위임으로
        // 바꾸면 그 파일들을 한 줄도 건드리지 않고 값이 하나로 수렴한다.
        //
        // 두 구현의 실질적 차이는 없었다 — 둘 다 루트의 **비-트리거** CapsuleCollider2D.size.y를 읽는다
        // (GrabArea는 isTrigger=true라 전신보다 위아래로 더 크므로 반드시 제외해야 한다).
        // StickmanMetrics 쪽은 거기에 더해 머리 중심/머리 반경/어깨/엉덩이 높이와 크기 배율까지
        // 함께 실측하므로, 새 코드는 이 프로퍼티가 아니라 StickmanMetrics를 직접 쓰는 편이 낫다.

        private StickmanMetrics _metrics;
        private bool _metricsWarningLogged;

        /// <summary>
        /// 캐릭터 실측 치수 조회 창구(Core/StickmanMetrics.cs). 프리팹에 없으면(손으로 조립한 테스트
        /// 리그 등) 즉석에서 붙여준다 — 그래야 "치수를 재는 곳은 언제나 한 군데"라는 불변식이
        /// 폴백 경로에서도 깨지지 않는다(StickmanMetrics 자신이 계층 실측 실패 시 배율 1.0 비율로
        /// 되메우므로 0이 나올 수 없다).
        /// </summary>
        public StickmanMetrics Metrics
        {
            get
            {
                if (_metrics != null) return _metrics;
                _metrics = GetComponent<StickmanMetrics>();
                if (_metrics == null)
                {
                    if (!_metricsWarningLogged)
                    {
                        _metricsWarningLogged = true;
                        Debug.LogWarning("[StickmanAgent] 프리팹에 StickmanMetrics가 없어 런타임에 부착합니다 — " +
                            "Editor/SceneBootstrapper.cs가 굽는 프리팹이라면 --force로 다시 구우세요.");
                    }
                    _metrics = gameObject.AddComponent<StickmanMetrics>();
                }
                return _metrics;
            }
        }

        /// <summary>
        /// 캐릭터 전신 높이(월드 유닛) — 발끝(로컬 y=0)부터 정수리까지. 머리 위/정면 연출은
        /// <b>전부 이 값의 비율</b>로 자기 위치를 잡는다(절대 유닛 상수를 두지 않는다).
        /// 소비자: Dialogue/DialogueBubbleRenderer, Interaction/HardwareReactionRenderer,
        /// Interaction/BattleMinigameRenderer.
        ///
        /// ★ 값의 생산자는 <see cref="Metrics"/>(Core/StickmanMetrics.cs) 하나뿐이다 — 위 통합 문단 참고.
        /// </summary>
        public float CharacterTotalHeightWorld => Metrics.TotalHeight;

        /// <summary>
        /// RAGDOLL 강제 인터럽트의 단일 진입점(아키텍처 0절). 몸통이든 사지든 어떤 파츠가 외력(충돌)을
        /// 받으면 이 메서드로 통지되어, 충격량 크기가 StickConfig.ragdollForceThreshold 이상이면 현재
        /// 능동 상태가 무엇이든(Idle/Walk/Jump/Fall/ParkourClimb/Attack) 즉시 Ragdoll로 강제 전이한다.
        /// Getup 도중에도 다시 호출되면 재인터럽트된다 — ChangeState는 이미 Ragdoll이어도 Enter()를 다시
        /// 실행해 _settleTimer를 리셋하므로, "계속 얻어맞으면 계속 ragdoll" 동작이 별도 코드 없이
        /// 보장된다(GetupState.cs 참고). 루트 파츠는 OnCollisionEnter2D가 직접 호출하고, 사지 등
        /// 비루트 파츠는 RagdollLimbImpactRelay.cs를 부착하면 같은 경로로 통지된다(실제 프리팹 배선은
        /// Phase 2 범위 밖). Phase 3부터는 판정식 자체를 States.RagdollImpactResolver로 위임한다 —
        /// States/DragThrowState.cs(던진 속도 기반)/RodeoCursorState.cs(거친 흔들기)도
        /// 동일한 판정식을 써야 해서, 이
        /// MonoBehaviour 메서드에서만 로직을 갖고 있으면 다른 순수 C# 클래스에서 재사용할 수 없었다.
        /// 공개 시그니처는 전혀 바뀌지 않았다 — 기존 호출부(OnCollisionEnter2D 등) 무수정으로 계속 동작한다.
        /// </summary>
        public void ReportExternalImpact(float impulseMagnitude)
        {
            if (_isSuspended || _machine == null || _config == null) return;
            RagdollImpactResolver.TryApplyImpact(_blackboard, impulseMagnitude);
        }

        /// <summary>
        /// ★ 2026-09-01 (P9-b) 위 메서드에 <b>방향</b>을 얹은 오버로드. 가드/판정은 완전히 같고,
        /// 넘긴 방향이 RAGDOLL 진입 순간 가슴 높이에 실릴 충격량의 방향이 된다(팔다리가 그 반대로
        /// 휘둘린다 — RagdollRig.EnterRagdoll의 지렛대 문서 참고).
        ///
        /// 방향을 <b>모르는</b> 호출자는 위 무방향 버전을 그대로 쓴다. 추정한 방향으로 때리는 것보다
        /// 안 때리는 쪽이 정직하고, 무방향 경로는 P9-a 이전과 비트 단위로 같은 거동을 유지한다.
        /// </summary>
        /// <param name="hitDirection">캐릭터가 밀려나는 방향(월드, 정규화 불필요).</param>
        public void ReportExternalImpact(float impulseMagnitude, Vector2 hitDirection)
        {
            if (_isSuspended || _machine == null || _config == null) return;
            RagdollImpactResolver.TryApplyImpact(_blackboard, impulseMagnitude, hitDirection);
        }

        /// <summary>
        /// ★ 충돌 콜백 전용 통지(2026-08-29, "무릎앉아 착지" 라운드). <see cref="ReportExternalImpact"/>와
        /// 같은 판정을 쓰되, 그 앞에 "이건 외력이 아니라 내 착지다"라는 예외 하나가 추가된 경로다 —
        /// 판정과 근거는 전부 States/RagdollImpactResolver.TryApplyCollisionImpact 문서에 있다.
        /// 루트(아래 OnCollisionEnter2D)와 비루트 파츠(Core/RagdollLimbImpactRelay)가 함께 쓴다.
        /// </summary>
        public void ReportCollisionImpact(Collision2D collision, float impulseMagnitude)
        {
            if (_isSuspended || _machine == null || _config == null) return;
            RagdollImpactResolver.TryApplyCollisionImpact(_blackboard, collision, impulseMagnitude);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_body == null) return;
            ReportCollisionImpact(collision, collision.relativeVelocity.magnitude * _body.mass);
        }

        private void Awake()
        {
            // ★ 2026-08-31 R3 Blocker 2 — 세션은 언제나 **배포 기본 배율**에서 출발한다.
            // StickConfig는 에셋이라 씬을 다시 로드해도 같은 인스턴스가 살아 있다. 앞선 씬(또는
            // 앞선 테스트 케이스)이 지정한 런타임 배율을 여기서 지우지 않으면 그 값이 다음 씬으로
            // 조용히 새어 들어간다 — PlayMode 스위트 전체가 0.35배로 돌던 사고의 전파 경로다.
            if (_config != null) _config.ClearRuntimeCharacterScale();

            // ★ 2026-08-31 R5 — 잉크색도 정확히 같은 이유로 세션 시작에 지운다(사용자가 고른 색은
            // 아래 Start()에서 저장 파일로부터 다시 얹는다. 에셋에는 어느 쪽도 기록되지 않는다).
            if (_config != null) _config.ClearRuntimeInkColor();

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
            // ★ 2026-08-30 R3-M1 — 물리 반폭 실측용 캐시. 계층의 콜라이더 개수는 런타임에 변하지 않으므로
            // 여기서 한 번만 모으고, 매 주기에는 "루트 몸에 붙어 있고 트리거가 아닌 것"만 골라 바운즈를 잰다
            // (필터를 여기서 미리 하지 않는 이유: _body 배선이 이 시점보다 뒤에 끝나는 경로가 있다).
            _colliders = GetComponentsInChildren<Collider2D>(true);
            // 색 프리셋 일괄 갱신용 캐시(사용자 요청 "흰색 or 검은색 선택", 2026-08-28).
            // 몸통/머리링/눈/팔다리가 전부 LineRenderer라 이 배열 하나면 캐릭터 전체를 덮는다.
            _lineRenderers = GetComponentsInChildren<LineRenderer>(true);
            // ★ 표식이 먼저다 — CacheBakedStrokeWidths가 선마다 "어느 하한 소속인가"를 함께 굽는다.
            MarkHeadRingAsFillOutline();
            CacheBakedStrokeWidths();

            // ★ 단일 창구 배선(_dynamicVisuals 문서). 부품 목록을 여기 적지 않는다 —
            //   ICharacterVisualSource를 구현한 것이 스스로 잡힌다(StickmanBlackboard의
            //   ICharacterInkExtentProvider 수집과 같은 관례). 그래야 다섯 번째 부품이 생겨도
            //   이 줄을 고칠 필요가 없다.
            _dynamicVisuals.BindSources(GetComponentsInChildren<ICharacterVisualSource>(true));
            // 하한은 카메라/화면에서 나오므로 첫 프레임(액세서리가 굽기 전)부터 유효해야 한다.
            RefreshStrokeFloors();

            // ★ "프리팹이 어떤 배율로 구워졌는가"를 <b>어떤 다이얼 조작보다 먼저</b> 한 번만 캐싱한다.
            // 루트 localScale이 아직 1인 이 시점의 실측 배율이 정확히 그 값이다. 이걸 놓치면
            // config.characterScale이 "구워진 배율"과 "원하는 배율" 두 의미를 겸한 채 갈라져,
            // 두 번째 다이얼 조작부터 크기가 조용히 어긋난다(2026-08-30 디버거가 지목한 함정).
            StickmanMetrics bakedMetrics = Metrics;
            BakedCharacterScale = bakedMetrics != null ? bakedMetrics.Scale : 1f;
            if (BakedCharacterScale <= 0.0001f || float.IsNaN(BakedCharacterScale)) BakedCharacterScale = 1f;

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
                // 무릎앉아 착지(사용자 명시 요청 2026-08-29) — FallState가 착지를 확정하면서 낙하 높이가
                // StickConfig.landingSoftAbsorbThresholdHeights x 신장 이상일 때만 전이시킨다. 등록을 빠뜨리면
                // ChangeState가 BUG-M2 방어 코드(에러 로그 + 현재 상태 유지)를 밟아 연출이 통째로 사라진다.
                { StickmanStateId.LandingCrouch, new LandingCrouchState(_blackboard) },
                { StickmanStateId.ParkourClimb, new ParkourClimbState(_blackboard) },
                // 매달려 내려가기(ParkourClimb의 하강 방향, 사용자 명시 요청 2026-08-28).
                { StickmanStateId.LedgeHang, new LedgeHangState(_blackboard) },
                // Phase 3: AttackState도 나머지 상태와 동일하게 블랙보드 주입 생성자로 전환.
                // ※ 2026-08-30 현재 ChangeState(Attack)를 부르는 런타임 생산자는 0개다(States/AttackState.cs 문서).
                { StickmanStateId.Attack, new AttackState(_blackboard) },
                { StickmanStateId.Ragdoll, new RagdollState(_blackboard) },
                { StickmanStateId.Getup, new GetupState(_blackboard) },
                // Phase 3 신규(UX_FLOW.md 10/12/13절) — 전부 Interaction/* 컨트롤러가 부분적 클릭관통
                // 해제/SpectacleEventLock을 확보한 뒤에만 ChangeState를 호출한다(States/*.cs는 그 획득
                // 절차를 전혀 모른다). 11종을 전부 등록해두는 이유는 위와 동일(BUG-M2 방어 코드를 밟을
                // 일 자체를 없앰).
                { StickmanStateId.BattleMinigame, new BattleMinigameState(_blackboard) },
                { StickmanStateId.Dragged, new DragThrowState(_blackboard) },
                // ★ 던지기 공중 회전(사용자 명시 요청 2026-08-29) — DragThrowState가 놓는 순간
                // "깨끗하게 던져진 자유 비행"이면 이 상태로 보낸다. 등록을 빠뜨리면 ChangeState가
                // BUG-M2 방어 코드(에러 로그 + 현재 상태 유지)를 밟아, 던져도 아무 일이 일어나지 않고
                // 캐릭터가 Dragged에 고착된 것처럼 보인다.
                { StickmanStateId.ThrowTumble, new ThrowTumbleState(_blackboard) },
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
                // 활쏘기(사용자 명시 요청 2026-08-29) — 3발 x 3단계 페이즈 머신 + 매 프레임 포즈 구동이라
                // TimedSpectacleState("부수 효과 없는 순수 타이머")를 재사용할 수 없다(States/ArcheryState.cs
                // 클래스 문서에 판단 근거). 등록을 빠뜨리면 ChangeState가 BUG-M2 방어 코드를 밟아
                // 연출이 통째로 사라진다.
                { StickmanStateId.Archery, new ArcheryState(_blackboard) },
                // ★ 발판 상실 공중 유예(2026-09-01) — Idle/Walk가 발판을 잃은 순간
                // StickmanBlackboard.GroundedTick()이 여기로 승격시킨다. 등록을 빠뜨리면 ChangeState가
                // BUG-M2 방어 코드(에러 로그 + 현재 상태 유지)를 밟아 연출이 통째로 사라지고,
                // 유예 붙잡음은 그대로라 **정지 화면**(= 이번 라운드가 고치려는 그 그림)으로 되돌아간다.
                { StickmanStateId.GroundLossHang, new GroundLossHangState(_blackboard) },
            };

            // BUG-P1-M2 대응(Major, docs/BUG_REPORT_PHASE1.md): 생성과 "최초 상태 활성화"를 분리했다.
            // 생성자는 더 이상 즉시 ChangeState를 호출하지 않으므로, blackboard.Machine을 먼저 완전히
            // 배선한 뒤에 Start()를 호출하면 "초기 상태의 Enter()가 무엇을 참조하든 Machine이 null일 수
            // 있는" 경우의 수 자체가 구조적으로 사라진다(우연이 아니라 보증).
            _machine = new StickmanStateMachine(states);
            _blackboard.Machine = _machine;
            _machine.Start(StickmanStateId.Idle);

            // 근본 재구현(2026-08-28, States/RagdollRig.cs 클래스 문서 참고): 물리 모드/포즈를 첫
            // FixedUpdate보다 먼저 확정해둔다. 프리팹이 이미 능동 모드 기본값(루트 FreezeRotation,
            // 팔다리 Kinematic, 관절 비활성)으로 저장돼 있지만, 여기서 한 번 더 명시적으로 적용해
            // 프리팹이 어떤 상태로 저장돼 있든 런타임 첫 프레임부터 반드시 직립 중립 포즈로 시작하게
            // 만든다("우연히 맞는 것"이 아니라 코드로 보증).
            _blackboard.SnapToIdlePose();
        }

        private void Start()
        {
            // BUG-P1-M3 대응(Major, docs/BUG_REPORT_PHASE1.md): 반환값을 버리지 않고 확인한다. 실패해도
            // 여기서 흐름을 막지는 않는다(에디터/Null 폴백 등은 애초에 오버레이 개념이 없어 항상 true) —
            // 다만 실패를 조용히 삼키지 않고 로그로 남겨, 가설 H4(부트스트랩 타이밍에 핸들이 Zero) 같은
            // 진단 사각지대를 없앤다.
            // 색 프리셋 적용(사용자 요청, 2026-08-28) — 프리팹에 저장된 색이 무엇이든 런타임에는 항상
            // StickConfig.inkColor가 이긴다. 덕분에 프리팹/씬을 다시 생성하지 않고 에셋 값만 바꿔도
            // 흰색/검은색을 전환할 수 있다.
            // ★ 2026-08-31 R5 — 사용자가 고른 잉크색은 에셋이 아니라 저장 파일이 기억한다
            //   (Core/CharacterAppearanceModel.cs). 저장 파일 로드는 CharacterProgressionDirector.Awake가
            //   하므로 모든 Awake가 끝난 이 시점에는 이미 복원돼 있다 — 여기서 런타임 오버라이드로
            //   얹은 뒤 한 번에 적용한다(빌드에서 재시작마다 검정으로 되돌아가던 결함의 수정).
            if (_config != null && CharacterAppearanceModel.HasInkColor)
            {
                _config.SetRuntimeInkColor(CharacterAppearanceModel.InkColor);
            }
            ApplyInkColorFromConfig();

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
                //
                // ★ 2026-08-30 윈도우 지원 라운드 — BUG-B1(Blocker) 해소로 이 자리의 의미가 바뀌었다.
                // 이전까지 Windows에서는 Win32WindowService가 "진짜 분리된 오버레이 창"을 갖지 못한
                // 스텁이라(게임 자신의 창을 재사용) 이 호출이 NotSupportedException으로 **의도적으로
                // 실패**하도록 막혀 있었다. 이번 라운드에 Windows도 macOS와 동일한 경로
                // (UniWindowController, com.kirurobo.uniwinc)로 통일되어 두 플랫폼 모두 진짜 투명/
                // 항상위/클릭관통 오버레이가 실제로 켜진다 — 그 안전 가드는 제거됐다.
                //
                // 그런데도 try/catch를 남겨두는 이유(가드의 부활이 아니라 다른 실패 모드):
                // 두 구현체 모두 "씬에서 UniWindowController를 찾지 못하면" NotSupportedException을
                // 던진다(조용한 no-op 금지 컨벤션). 그건 배선 사고이지 플랫폼 미지원이 아니므로,
                // 여기서 잡아 경고로 남기고 나머지 초기화는 계속한다(BUG-P1-M3와 같은 태도).
                _platformService.SetAlwaysOnTop(true);
            }
            catch (System.NotSupportedException ex)
            {
                Debug.LogWarning("[StickmanAgent] 항상위 배선 실패 — 오버레이 컨트롤러를 찾지 못했습니다" +
                                  "(씬에 UniWindowController가 배치되어 있는지 확인하세요): " + ex.Message);
            }

            // 비침해 원칙 2: 클릭 관통 기본 ON — 다만 위 클래스 상단 "클릭 관통 긴급 종료 안전장치"
            // 문서가 설명하듯, 켜지는 순간 우리 창을 다시 클릭할 수 없게 될 위험이 있어 즉시 켜지 않고
            // ClickThroughSafetyDelaySeconds만큼 지연시킨다(그 사이 EmergencyDisableKey/Update() 참고로
            // 언제든 되돌릴 수 있음을 사용자가 확인할 시간을 번다).
            StartCoroutine(EnableClickThroughAfterSafetyDelay());
        }

        /// <summary>
        /// StickConfig.inkColor 프리셋을 캐릭터 전체(몸통/머리 링/눈동자/팔다리)에 일괄 적용한다.
        /// 눈동자도 같은 색을 쓰는 근거는 StickConfig.ResolveInkColor() 문서 참고(머리 안쪽이 비어 있어
        /// 눈은 '배경 위의 잉크 점'이므로 잉크와 같은 색이어야 보인다).
        /// </summary>
        public void ApplyInkColorFromConfig()
        {
            if (_config == null) return;
            ApplyInkColor(_config.ResolveInkColor());
        }

        /// <summary>
        /// 캐릭터의 모든 LineRenderer 색을 한 번에 바꾼다. 다음 라운드에 설정 UI나 토글 단축키를 붙일 때
        /// 이 메서드 하나만 호출하면 된다(리더 지시: "런타임 갱신 메서드가 존재하는 것까지").
        /// </summary>
        public void ApplyInkColor(Color color)
        {
            if (_lineRenderers == null) return;
            int applied = 0;
            for (int i = 0; i < _lineRenderers.Length; i++)
            {
                LineRenderer lr = _lineRenderers[i];
                if (lr == null) continue;
                lr.startColor = color;
                lr.endColor = color;
                applied++;
            }
            Debug.Log($"[StickmanAgent] 캐릭터 선 색 적용 — 프리셋={( _config != null ? _config.ResolveInkPreset().ToString() : "?")}, " +
                $"색=({color.r:F2},{color.g:F2},{color.b:F2}), LineRenderer {applied}개 갱신.");
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

        // ============================================================================
        // ★★ 물리 주기 훅 (2026-09-02) — 발 떼기 이송이 마찰과 **같은 주기**로 돌게 하는 유일한 배선
        // ============================================================================
        // 이 컴포넌트에는 원래 FixedUpdate가 없었다. 상태 로직은 전부 Update(프레임)에서 돌고,
        // 물리에 주는 지시는 "다음 FixedUpdate가 읽어갈 값을 프레임 끝에 세워 둔다"는 관례로 처리해
        // 왔다(중력 억제/포즈). 그 관례는 **프레임당 한 번이면 충분한** 값에만 성립한다.
        //
        // 발 떼기 이송은 그 부류가 아니다. 되돌리려는 마찰이 FixedUpdate마다 걸리므로, 프레임당 1회
        // 재적용은 프레임이 길어질수록 지고 프레임이 유예(0.25초)를 삼키면 0회가 된다 — 절전 등급
        // DisplayOff(4fps = 250ms/프레임)에서는 결정적으로 그렇다. 그래서 이 한 가지만 물리 주기에
        // 싣는다(유도와 계측: States/StickmanBlackboard.TickStepOffCarry 문서).
        //
        // ★ 여기에 다른 로직을 늘리지 마라. 상태 머신은 프레임 주기가 계약이고(포즈/대사/센서가 모두
        //   그 전제 위에 있다), 물리 주기에서 상태를 바꾸면 한 프레임에 여러 번 전이하게 된다.
        // ★ Suspend 중에는 돌지 않는다 — Update가 조기 return하는 것과 같은 이유이며, 그때는
        //   SetBodiesSimulated(false)로 물리 자체가 멎어 있어 속도를 쓰는 것 자체가 무의미하다.
        private void FixedUpdate()
        {
            if (_isSuspended || _blackboard == null) return;
            _blackboard.TickStepOffCarry();
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Agent);   // [스톨구간] 계측
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

            // ★ 잉크 바닥 클리어런스 리프트를 **상태 로직보다 먼저** 벗긴다(2026-08-31).
            // 리프트는 "그 프레임의 그림"에만 존재해야 한다 — 얹힌 채로 두면 아래의 접지 센서/스냅이
            // 발이 발판에서 떠 있는 것으로 오판해 기상 도중 Fall로 보낼 수 있다
            // (StickmanBlackboard.ReleaseInkFloorClearanceLift 문서의 두 임계값 참고).
            _blackboard.ReleaseInkFloorClearanceLift();

            // ★ 접지 중 중력 억제도 **상태 로직보다 먼저** 벗긴다(2026-09-01, 신고 "창에서 가끔 갑자기
            // 떨어짐"의 근본 원인 1). 위 리프트와 완전히 같은 관례이며 이유도 같다 — 얹힌 채로 두면
            // 상태/연출 코드가 gravityScale을 읽을 때 0을 보게 되고(ThrowTumbleState의 포물선 계산 등),
            // 무엇보다 "중력이 꺼진 채 갇히는" 상태가 남을 수 있다. 다시 얹는 것은 이 Update의 맨 끝
            // 한 곳뿐이다(StickmanBlackboard.ApplyGroundedGravitySuppression 문서 참고).
            _blackboard.ReleaseGroundedGravitySuppression();

            _machine.Tick(dt);

            // ★★ 접지 유지 안전망(2026-08-30, 디버거 — 사용자 신고 "갑자기 독 아래로 떨어지면서
            // 관절이 이상하게 꺾임"). 상태가 스스로 GroundedTick()을 부르지 않아도 **여기 한 곳에서**
            // 대신 불러준다. TickPose()가 "포즈는 상태 ID 하나로 한 곳에서 결정된다"를 보장하는 것과
            // 정확히 같은 이유이며(상태가 14개가 넘고 하나라도 빠뜨리면 그 상태에서만 깨진다),
            // 실제로 Attack/Getup/BattleMinigame이 그렇게 빠져 있었다.
            // 상태가 이미 불렀으면 프레임 번호로 감지해 중복하지 않는다
            // (StickmanBlackboard.TickGroundKeepingSafetyNet 문서 참고).
            _blackboard.TickGroundKeepingSafetyNet(dt);

            // 상태 로직이 끝난 뒤(= 이번 프레임의 최종 상태가 확정된 뒤) 물리 모드와 팔다리 포즈를
            // 그 상태에 맞게 재적용한다. 멱등이며 상태 ID만 보고 판단하므로, 강제 인터럽트/전체화면
            // 취소/외부 ChangeState 등 어떤 경로로 상태가 바뀌어도 물리 모드가 상태와 어긋난 채
            // 남을 수 없다(StickmanBlackboard.TickPose() 문서 참고).
            _blackboard.TickPose(dt);

            // ★★ 잉크 바닥 클리어런스(2026-08-31, 디버거가 원인 확정한 GETUP 발판 관통).
            // 반드시 **접지 안전망과 포즈 확정 뒤**여야 한다: (a) 안전망이 먼저 돌면 그 SnapToGround가
            // 리프트를 도로 눌러 내리고, (b) 포즈가 확정되기 전에 재면 이번 프레임에 그려질 자세가
            // 아니라 지난 프레임 자세의 깊이를 보정하게 된다.
            // 위 ReleaseInkFloorClearanceLift와 짝이다(얹기/벗기기를 한 프레임 안에서 닫는다).
            _blackboard.TickInkFloorClearance();

            // 화면 클램프가 쓸 "지금 몸이 실제로 얼마나 넓은가"를 갱신한다(포즈 확정 직후여야 정확하다).
            TickVisualHalfWidth(dt);

            // ★ 리더 지시 6·7항(2026-08-28) — 화면 밖 소실 방지. 반드시 **마지막**에 호출한다:
            // 어떤 상태가 어떤 이유로 몸을 옮겼든(드래그/던지기/랙돌/순간이동성 스냅) 그 결과를 여기서
            // 화면 안으로 되돌리고, 오래 착지하지 못하면 강제 복귀시킨다
            // (StickmanBlackboard.EnforceScreenBoundsAndRescue 문서 참고).
            _blackboard.EnforceScreenBoundsAndRescue(dt);

            // ★ 맨 마지막 — 이번 프레임의 **최종** 상태/위치가 확정된 뒤에만 중력 억제를 얹는다.
            // Unity 프레임 순서가 FixedUpdate -> Update이므로, 여기서 세운 값이 다음 물리 스텝을
            // 지배한다 = 다음 프레임이 250ms로 튀어도 접지 중이면 세로 적분이 0이다.
            // 위 ReleaseGroundedGravitySuppression()과 짝이다(얹기/벗기기를 한 프레임 안에서 닫는다).
            _blackboard.ApplyGroundedGravitySuppression();
        }

        /// <summary>
        /// 마지막 보험 — 이 컴포넌트가 어떤 이유로든(비활성화/씬 언로드) 더 이상 Update를 돌지 않게 되면
        /// 얹어 둔 중력 억제를 즉시 벗긴다. Update의 "맨 앞에서 벗기고 맨 끝에서 얹는다" 규율은 Update가
        /// 계속 돈다는 전제 위에 있으므로, 그 전제가 깨지는 유일한 지점을 여기서 닫는다
        /// (중력이 꺼진 채 갇히는 것은 이 수정이 막으려는 버그보다 심각하다).
        /// </summary>
        private void OnDisable()
        {
            _blackboard?.ReleaseGroundedGravitySuppression();
        }

        // ============================================================================
        // 캐릭터 시각적 반폭 추적 (2026-08-28, 리더 관찰 "화면 왼쪽 끝에서 잘려 보인다")
        // ============================================================================
        // 화면 하드 클램프(StickmanBlackboard.EnforceScreenBoundsAndRescue)는 루트(=발 중심)만 보므로,
        // 벌린 팔/머리의 실제 폭을 모르면 가장자리에서 몸이 반쯤 잘린다. 포즈에 따라 폭이 계속 바뀌므로
        // 상수로 둘 수 없고, 그렇다고 매 프레임 렌더러 12개의 bounds를 합치는 것도 24시간 상주 앱에서는
        // 불필요한 낭비다 — 그래서 이 간격으로만 갱신한다(가장자리에 닿기까지는 최소 수백 ms가 걸리므로
        // 0.25초면 항상 최신값이나 다름없다).
        private const float VisualHalfWidthRefreshInterval = 0.25f;
        private float _visualHalfWidthTimer = float.MaxValue;

        // ★★ 2026-08-31 — 이 계산에는 <b>두 개의 결함이 동시에</b> 있었고, 서로를 가리고 있었다.
        //
        // (1) <c>Renderer.bounds</c>를 썼다 — 이 프로젝트가 "쓰면 안 된다"고 문서화한 바로 그 API다
        //     (Tests/PlayMode/StickmanInkBounds: LineRenderer.bounds는 실제 잉크보다 약 1.0유닛
        //     부풀려져 있고, 그 부풀림을 실측으로 오독한 것이 사용자가 세 번 신고한 40pt 바닥 인셋의
        //     원인이었다). 부풀림은 루트 스케일을 따라가므로 배율 2.00에서 반폭이 실제 잉크의 2.6배
        //     (과대분 1.81유닛 ≈ 실사용 74pt)였다 — 캐릭터 폭보다 넓은 여백을 남기고 돌아섰다.
        // (2) 이 계산에 <b>액세서리가 한 개도 안 들어갔다</b>(Awake 캐시 배열 문제). 긴 망토는 배율
        //     2.00에서 몸보다 0.30유닛 더 튀어나오는데 아무도 그것을 몰랐다.
        //
        // 지금까지 망토가 잘리지 않은 이유는 순전히 (1)의 부풀림이 (2)의 돌출을 우연히 덮고 있어서다.
        // <b>둘을 반드시 함께 고쳐야 한다</b> — (1)만 고치면 그 순간 망토가 잘리고, (2)만 고치면
        // 과대 여백이 그대로 남는다.
        //
        // 대신 쓰는 방법: <b>지금 실제로 그리는 정점</b>에서 잰다. 액세서리가 자기 잉크 최저 Y를 답할
        // 때 쓰는 것과 같은 기법이며(Interaction/CharacterAccessoryRenderer.TryGetLowestInkWorldY),
        // 정점은 중심선이므로 획 반두께를 더해야 실제 잉크 가장자리가 된다.
        private void TickVisualHalfWidth(float deltaTime)
        {
            _visualHalfWidthTimer += deltaTime;
            if (_visualHalfWidthTimer < VisualHalfWidthRefreshInterval) return;
            _visualHalfWidthTimer = 0f;

            // 화면/카메라에서 나오는 값이라 여기서 함께 갱신한다(획 두께 하한의 단일 소스).
            RefreshStrokeFloors();

            if (_body == null || _blackboard == null) return;

            float centerX = _body.position.x;
            float halfWidth = 0f;

            if (_lineRenderers != null)
            {
                for (int i = 0; i < _lineRenderers.Length; i++)
                {
                    halfWidth = Mathf.Max(halfWidth, MeasureInkHalfWidth(_lineRenderers[i], centerX));
                }
            }

            // 몸 바깥의 잉크 — 단일 창구(_dynamicVisuals 문서). <b>몸에 붙은 것만</b> 센다:
            // 펫/FX는 몸과 독립으로 돌아다니거나 땅에 남으므로 "내 몸이 얼마나 넓은가"가 아니다
            // (Core/CharacterVisualRegistry.CharacterVisualAnchor 참고).
            _dynamicVisuals.Refresh();
            for (int i = 0; i < _dynamicVisuals.Count; i++)
            {
                CharacterVisualRegistry.Entry e = _dynamicVisuals[i];
                if (e.Anchor != CharacterVisualAnchor.BodyAttached) continue;
                if (e.Line != null)
                {
                    halfWidth = Mathf.Max(halfWidth, MeasureInkHalfWidth(e.Line, centerX));
                    continue;
                }
                // 채움 면(MeshRenderer)의 bounds는 <b>실제 메시 정점</b>에서 나오므로 부풀지 않는다
                // — LineRenderer.bounds와 달리 그대로 쓸 수 있다(액세서리의 최저 Y 계산과 같은 판단).
                Renderer r = e.Renderer;
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(b.max.x - centerX));
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(centerX - b.min.x));
            }

            _blackboard.CharacterVisualHalfWidthWorld = halfWidth;

            TickPhysicalHalfWidth();
        }

        /// <summary>선 하나가 <paramref name="centerX"/>에서 좌우로 뻗은 <b>실제 잉크</b> 반폭.
        /// 정점은 중심선이라 획 반두께를 더한다. 두께는 Transform 스케일을 따라가지 않으므로
        /// (2026-08-30 실측) 월드 단위 그대로다.</summary>
        private static float MeasureInkHalfWidth(LineRenderer lr, float centerX)
        {
            if (lr == null || !lr.enabled || !lr.gameObject.activeInHierarchy) return 0f;
            int count = lr.positionCount;
            if (count <= 0) return 0f;

            float maxDx = 0f;
            bool world = lr.useWorldSpace;
            Transform t = lr.transform;
            for (int q = 0; q < count; q++)
            {
                Vector3 p = lr.GetPosition(q);
                float x = world ? p.x : t.TransformPoint(p).x;
                float dx = Mathf.Abs(x - centerX);
                if (dx > maxDx) maxDx = dx;
            }
            return maxDx + Mathf.Max(lr.startWidth, lr.endWidth) * 0.5f;
        }

        // ============================================================================
        // 캐릭터 물리적 반폭 추적 (2026-08-30, R3-M1 "Dock 되올라오기 밴드 근접 충돌")
        // ============================================================================
        // 위 시각 반폭과 **다른 값**이다. 시각 반폭은 "화면 밖으로 잘리지 않게" 렌더러 바운즈를 재고,
        // 이쪽은 "벽에 얼마나 가까이 설 수 있는가"를 재기 때문에 **비-트리거 콜라이더만** 본다.
        // 실제로 지배하는 형상은 루트 캡슐(반폭 0.2 x 배율)이 아니라 머리 CircleCollider2D
        // (반경 StickConfig.BaselineBodyPhysicsHalfWidth x 배율 = 0.4 x 배율)다.
        // 잡기 영역(GrabArea)은 isTrigger라 물리 충돌을 일으키지 않으므로 반드시 제외해야 한다 —
        // 포함하면 반폭이 0.3(배율 0.75)으로 부풀어 경계 판정이 필요 이상으로 일찍 걸린다.
        //
        // 갱신 주기를 시각 반폭과 공유하는 이유: 이 값은 포즈가 아니라 **콜라이더 크기**에서 나오므로
        // 사실상 상수이고, 유일하게 바뀌는 경우가 캐릭터 크기 배율 변경이다(그때도 0.25초면 충분하다).
        //
        // ★ 왜 Collider2D.bounds(월드 AABB)를 쓰지 않고 형상 치수를 직접 읽는가 — 두 오염원 때문이다:
        //   (1) RAGDOLL로 몸이 누우면 세로 1.7유닛짜리 캡슐의 AABB가 가로로 0.85까지 벌어진다.
        //   (2) 유휴 "주위 살피기"는 머리 Transform을 좌우로 최대 0.06유닛(키의 3.5%) 민다 — 시각
        //       전용 연출인데 그 순간 표본을 뜨면 경계 판정 거리가 프레임마다 달라진다.
        // 형상 치수는 둘 다에 면역이고(회전/포즈 무관), 우리가 알고 싶은 것 — "몸통이 벽에 닿을 때
        // 루트 원점이 벽에서 얼마나 떨어지는가" — 과 정확히 일치한다.
        private void TickPhysicalHalfWidth()
        {
            if (_colliders == null || _blackboard == null) return;

            float halfWidth = 0f;
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider2D c = _colliders[i];
                if (c == null || !c.enabled || c.isTrigger) continue;
                // 루트 몸에 붙어 있는 것만 — 팔다리는 각자 Kinematic Rigidbody2D를 갖고 있어 정적
                // 지형을 밀어내지 못하므로(능동 상태에서 벽에 막히는 것은 루트 몸뿐이다) 세면 안 된다.
                if (c.attachedRigidbody != _body) continue;

                float localHalf;
                switch (c)
                {
                    case CircleCollider2D circle: localHalf = circle.radius; break;
                    case CapsuleCollider2D capsule:
                        localHalf = (capsule.direction == CapsuleDirection2D.Vertical
                            ? capsule.size.x : capsule.size.y) * 0.5f;
                        break;
                    case BoxCollider2D box: localHalf = box.size.x * 0.5f; break;
                    default: localHalf = c.bounds.extents.x; break; // 알 수 없는 형상은 보수적으로 AABB.
                }
                halfWidth = Mathf.Max(halfWidth, localHalf * Mathf.Abs(c.transform.lossyScale.x));
            }
            if (halfWidth > 0f) _blackboard.CharacterPhysicalHalfWidthWorld = halfWidth;
        }

        // ============================================================================
        // ★ 캐릭터 크기 런타임 적용 (2026-08-31 — docs/UX_FLOW.md 34-3 크기 다이얼)
        // ============================================================================
        //
        // 지금까지 StickConfig.characterScale은 <b>에디터 전용</b>이었다(값을 바꾼 뒤 메뉴
        // StickMate/Rebuild All로 프리팹과 씬을 다시 구워야 반영). 다이얼이 붙으면 그 전제가 깨진다 —
        // 다이얼에 2.00×라고 떠 있는데 캐릭터가 그대로면 <b>절대 불변 원칙 1(행동-텍스트 싱크) 정면
        // 위반</b>이다. 그래서 여기서 런타임 반영 경로를 연다.
        //
        // 물리는 배율 전 구간(0.35~2.00)에서 안전하다는 것이 2026-08-30 디버거 실측 결론이다
        // (질량이 스케일을 안 따라가 랙돌 임계값이 배율 불변 / breakForce가 Infinity라 관절 파단 불가 /
        // 루트 원점이 발바닥이라 접지 오차 0 / RAGDOLL 중에 바꿔도 관절 구속 오차 증가 0).
        // 안전하지 <b>않은</b> 것은 물리가 아니라 파생 레이어였고, 그래서 아래 다섯 가지를
        // <b>한 프레임에 원자적으로</b> 처리한다(순서까지 그 실측이 정한 것이다):
        //
        //   1. root.localScale = v / 구워진 배율            — 지오메트리 전체
        //   2. config.SetRuntimeCharacterScale(v)            — ResolveWalkSpeed()의 유일한 소스.
        //      이걸 빼먹으면 보폭은 2.67배가 되는데 보행 속도가 그대로라 <b>발이 미끄러진다</b>
        //      (망토 흔들림/FX 크기도 같은 값을 읽는다).
        //   3. metrics.Remeasure()                           — Measure()는 1회 캐싱이라 부르지 않으면
        //      0.8초 내내 옛 값을 돌려준다(실측). 스케일 대입과 <b>같은 프레임</b>이어야 한다.
        //   4. 전 LineRenderer 두께 재대입(2.0pt 하한)        — LineRenderer의 width는 Transform 스케일을
        //      <b>따라가지 않는다</b>(실측: 배율 3종에서 두께 0.02888 고정). 안 고치면 배율 2.00에서
        //      거미처럼 가늘어지고 0.35에서 뭉툭해진다.
        //   5. 시각 반폭 즉시 재측정                          — 0.25초 주기 그대로 두면 화면 가장자리에서
        //      최대 250ms 동안 옛 반폭으로 판정하다가 갱신 순간 루트가 한 프레임에 2.29유닛 순간이동한다.
        //
        // ★ Rigidbody2D.mass는 <b>일부러 건드리지 않는다</b>. 지금 안 따라가는 덕분에 랙돌 진입 임계가
        //   순수 속도 임계(8유닛/s)로 축약되어 배율 불변이다. s²로 재계산하면 배율 2.00에서 임계 속도가
        //   1.1유닛/s로 떨어져 <b>걷기만 해도 랙돌</b>이 된다(실측 근거로 "고칠 것 없음"이 정답인 항목).

        /// <summary>프리팹이 <b>구워진</b> 배율. 루트 localScale이 1일 때의 실측 배율이며 앱 수명 동안
        /// 변하지 않는다. 이 값이 필요한 이유: config의 배율은 다이얼이 덮어쓰는 순간부터
        /// "지금 원하는 배율"이 되어 "구워진 배율"을 더 이상 말해 주지 못한다(그러면 2회차 조작부터
        /// localScale = v / ResolveCharacterScale()이 틀린 값을 준다).</summary>
        public float BakedCharacterScale { get; private set; }

        /// <summary>지금 적용돼 있는 배율(= 구워진 배율 × 루트 localScale).</summary>
        public float CurrentCharacterScale => BakedCharacterScale * Mathf.Abs(transform.localScale.y);

        /// <summary>
        /// 구워진 획 두께(<b>월드 유닛</b>). 배율이 바뀔 때마다 이 값에 비율을 곱해 다시 대입한다 —
        /// 마지막에 대입한 값에 또 곱하면 오차가 누적된다.
        ///
        /// <para>★ <see cref="LineRenderer.widthMultiplier"/>가 아니라 <see cref="LineRenderer.startWidth"/>를
        /// 쓰는 이유(실측으로 정한 것이다): 프리팹은 <c>startWidth</c>에 실제 두께를 굽고 multiplier는
        /// <b>1.0 그대로</b> 남긴다. multiplier를 만지면 "화면상 최소 2.0pt"라는 하한을 <b>배수</b>와
        /// 비교하게 되어 단위가 어긋난다 — 실제로 그렇게 짜서 배율 0.35에서 하한이 전혀 걸리지 않는
        /// 상태를 실행으로 잡았다. 두께는 길이 단위로 다뤄야 하한도 길이 단위로 비교된다.</para>
        /// </summary>
        private float[] _bakedStrokeWidths;

        /// <summary>구워진 선마다 "채움 경계선인가"(= 어느 하한을 쓰는가). <see cref="_bakedStrokeWidths"/>와
        /// 같은 인덱스다. 계층은 런타임에 변하지 않으므로 Awake에서 한 번만 굽는다 —
        /// 매 배율 변경마다 <c>TryGetComponent</c>를 다시 부르지 않기 위해서다.</summary>
        private bool[] _bakedStrokeIsFillOutline;

        private void CacheBakedStrokeWidths()
        {
            if (_lineRenderers == null) return;
            _bakedStrokeWidths = new float[_lineRenderers.Length];
            _bakedStrokeIsFillOutline = new bool[_lineRenderers.Length];
            for (int i = 0; i < _lineRenderers.Length; i++)
            {
                LineRenderer lr = _lineRenderers[i];
                _bakedStrokeWidths[i] = lr != null ? lr.startWidth : 0f;
                _bakedStrokeIsFillOutline[i] = FillOutlineStroke.Is(lr);
            }
        }

        /// <summary>
        /// ★ 머리 링을 <b>채움 경계선</b>으로 표식한다(2026-09-02 M6, 부수 효과 M5 흡수).
        ///
        /// <para>링은 잉크로 꽉 찬 머리 원반(<c>HeadFill</c>)의 <b>경계선</b>이므로 정의상 채움 윤곽선
        /// 집합이다. 2.00pt 하한은 배율 0.6461 아래에서 링을 눌러 <b>획을 보이게 하는 대신 머리를
        /// 키우고</b> 있었다(범주 오류). 1.00pt로 내리면 그 구간이 0.3231 아래로 내려가 다이얼 전
        /// 구간에서 링이 순수 비례가 되고, 배율 0.60의 몸통 획 ÷ 머리 지름이 22.04% →
        /// <b>22.291%</b>(팀 확정 목표 22.3%)가 된다.</para>
        ///
        /// <para><b>프리팹에 굽지 않고 여기서 붙이는 이유</b>는 <see cref="FillOutlineStroke"/> 문서에
        /// 있다 — 구우면 <c>Rebuild All</c> 전까지 변경이 조용히 무효가 된다.</para>
        ///
        /// <para>이름으로 찾는 것은 이 리그의 <b>기존 치수 계약 C1</b>이다
        /// (<see cref="StickmanMetrics.HeadRingObjectName"/>). 못 찾으면 조용히 넘어간다 —
        /// 링이 없는 최소 리그(테스트 스텁)에서도 에이전트는 돌아야 한다.</para>
        /// </summary>
        private void MarkHeadRingAsFillOutline()
        {
            if (_lineRenderers == null) return;
            for (int i = 0; i < _lineRenderers.Length; i++)
            {
                LineRenderer lr = _lineRenderers[i];
                if (lr == null || lr.gameObject.name != StickmanMetrics.HeadRingObjectName) continue;
                FillOutlineStroke.Mark(lr);
            }
        }

        /// <summary>
        /// ★ 캐릭터 배율을 <b>지금 이 프레임에</b> 적용한다. 위 문단의 5단계를 순서대로 한 번에 한다.
        /// </summary>
        /// <param name="desiredScale">다이얼 값(StickConfig.Min/MaxCharacterScale로 clamp된다).</param>
        /// <param name="reason">로그에 남길 출처(다이얼/복원/테스트).</param>
        /// <returns>실제로 바뀌었으면 true(같은 값이면 아무것도 하지 않고 false).</returns>
        public bool ApplyCharacterScale(float desiredScale, string reason)
        {
            if (float.IsNaN(desiredScale) || desiredScale <= 0f) return false;
            float v = Mathf.Clamp(desiredScale, StickConfig.MinCharacterScale, StickConfig.MaxCharacterScale);

            float baked = BakedCharacterScale;
            if (baked <= 0.0001f || float.IsNaN(baked)) baked = 1f;

            float factor = v / baked;
            Vector3 current = transform.localScale;
            bool sameTransform = Mathf.Approximately(current.x, factor) && Mathf.Approximately(current.y, factor);
            bool sameConfig = _config == null || Mathf.Approximately(_config.ResolveCharacterScale(), v);
            if (sameTransform && sameConfig) return false;

            // (1) 지오메트리. 루트 원점이 발바닥이라 균일 스케일해도 발이 뜨거나 박히지 않는다.
            transform.localScale = new Vector3(factor, factor, 1f);

            // (2) 보행 속도/보폭/망토 흔들림의 단일 소스.
            // ★ 2026-08-31 R3 Blocker 2 — 여기서 `_config.characterScale = v`로 **직렬화 필드**에 쓰면
            //   그 순간 배포 에셋(DefaultStickConfig.asset)이 메모리에서 오염되고, 에디터가 그것을
            //   저장하면 전 사용자에게 그대로 나간다. 직렬화되지 않는 런타임 필드에만 쓴다
            //   (StickConfig의 "이번 실행의 배율은 이 에셋에 기록되지 않는다" 문단이 근거).
            if (_config != null) _config.SetRuntimeCharacterScale(v);

            // (3) 실측 치수 캐시 무효화 — 반드시 같은 프레임에.
            StickmanMetrics metrics = Metrics;
            if (metrics != null) metrics.Remeasure();

            // (4) 획 두께(Transform 스케일을 안 따라간다).
            ApplyStrokeWidthsForScale(v);

            // (5) 화면 클램프가 쓰는 반폭을 즉시 다시 잰다(주기를 기다리지 않는다).
            _visualHalfWidthTimer = float.MaxValue;
            TickVisualHalfWidth(0f);

            Debug.Log($"[크기] 캐릭터 배율 {v:F2}× 적용({reason}) — 구워진 배율 {baked:F3}, 루트 스케일 {factor:F3}, " +
                $"전신 높이 {(metrics != null ? metrics.TotalHeight : 0f):F3}유닛, 보행 속도 " +
                $"{(_config != null ? _config.ResolveWalkSpeed() : 0f):F3}유닛/s, 물리 반폭 " +
                $"{(_blackboard != null ? _blackboard.CharacterPhysicalHalfWidthWorld : 0f):F3}, 맨틀 인셋 " +
                $"{(_blackboard != null ? _blackboard.ParkourMantleInsetWorld : 0f):F3}.");
            return true;
        }

        /// <summary>
        /// 획 두께를 배율에 맞춰 다시 대입한다. <b>화면상 최소 두께</b> 아래로는 내려가지 않되,
        /// 그 하한은 <b>선의 역할에 따라 둘로 갈린다</b>(2026-09-02 M6):
        /// 낱선은 <see cref="StickConfig.MinStrokeScreenPoints"/>(2.00pt),
        /// 채운 도형의 경계선은 <see cref="StickConfig.MinFillOutlineScreenPoints"/>(1.00pt).
        /// Assets/Editor/SceneBootstrapper.cs가 프리팹을 구울 때 쓰는 것과 <b>같은 상수</b>다
        /// (상수를 두 곳에 적지 않으려고 StickConfig로 올렸다).
        ///
        /// <para><b>★★ 이 메서드가 M6의 급소다.</b> 여기 <b>두 경로</b>가 모든 선을 하한으로
        /// 되올리므로, 둘 중 <b>한 곳만</b> 역할을 알면 렌더러가 1.00pt로 그린 직후 여기서 2.00pt로
        /// 되돌아간다 — <b>화면은 하나도 안 바뀌는데 테스트는 초록</b>인 상태가 된다.
        /// 그래서 두 경로 모두 <see cref="FillOutlineStroke"/> 표식을 본다.</para>
        /// </summary>
        private void ApplyStrokeWidthsForScale(float scale)
        {
            if (_lineRenderers == null || _bakedStrokeWidths == null) return;

            float baked = BakedCharacterScale;
            if (baked <= 0.0001f || float.IsNaN(baked)) baked = 1f;
            float ratio = scale / baked;
            // 액세서리/펫/FX가 이 프레임에 다시 구우면서 읽어갈 값이므로 여기서 먼저 최신화한다.
            RefreshStrokeFloors();
            float floorWorld = _minStrokeWorldWidth;
            float fillOutlineFloorWorld = _minFillOutlineWorldWidth;

            // (1) 프리팹에 구워진 몸의 선. ★ 머리 링이 여기 들어 있고, 그것만 채움 경계선이다.
            for (int i = 0; i < _lineRenderers.Length && i < _bakedStrokeWidths.Length; i++)
            {
                LineRenderer lr = _lineRenderers[i];
                if (lr == null || _bakedStrokeWidths[i] <= 0f) continue;
                bool isFillOutline = _bakedStrokeIsFillOutline != null
                    && i < _bakedStrokeIsFillOutline.Length && _bakedStrokeIsFillOutline[i];
                float width = Mathf.Max(_bakedStrokeWidths[i] * ratio,
                    isFillOutline ? fillOutlineFloorWorld : floorWorld);
                lr.startWidth = width;
                lr.endWidth = width;
            }

            // ★ 2026-08-31 — 몸 바깥의 잉크에도 <b>같은 규칙</b>을 건다(단일 창구).
            //   여기서는 <b>올리기만</b> 한다(비율 재계산 금지): 액세서리/펫/FX의 두께는 각자
            //   StickmanMetrics에서 비례로 유도하고 있고, 그 비례값은 소유자가 다시 구울 때
            //   자기 하한(MinStrokeWorldWidth / MinFillOutlineWorldWidth)을 이미 반영한다.
            //   여기서 비율까지 다시 곱하면 같은 배율이 두 번 걸린다.
            //   이 훑기는 "다시 굽기 전까지의 한 프레임"을 메우는 안전망이다.
            _dynamicVisuals.Refresh();
            for (int i = 0; i < _dynamicVisuals.Count; i++)
            {
                LineRenderer lr = _dynamicVisuals[i].Line;
                if (lr == null) continue;
                // ★ 여기서 역할을 안 물으면 액세서리 채움 경계선이 이 한 줄에 도로 2.00pt가 된다.
                float lineFloor = FillOutlineStroke.Is(lr) ? fillOutlineFloorWorld : floorWorld;
                if (lr.startWidth < lineFloor) lr.startWidth = lineFloor;
                if (lr.endWidth < lineFloor) lr.endWidth = lineFloor;
            }
        }

        /// <summary>
        /// ★ 화면상 최소 획 두께(<see cref="StickConfig.MinStrokeScreenPoints"/>)를 월드 유닛으로
        /// 환산한 값 — <b>몸/액세서리/펫/FX가 공유하는 단일 소스</b>(2026-08-31).
        ///
        /// <para>왜 공개하는가: 이 하한은 지금까지 몸에만 걸려 있었고, 액세서리(전신높이×0.0211)와
        /// 펫/FX(전신높이×0.022)는 <b>하한 없는 순수 비례</b>였다. 그래서 출하 기본 배율 0.75에서도
        /// 액세서리 획이 1.47pt로 하한(2pt) 미달이었고, 다이얼 최소값 0.35에서는 0.69pt —
        /// 몸의 1/6이라 왕관 지그재그·방울·외알안경 체인·배낭 끈이 안티에일리어싱에 묻혔다.
        /// 부품마다 하한을 따로 적으면 반드시 어긋나므로 여기 하나만 읽게 한다.</para>
        ///
        /// <para>값은 카메라 직교 크기/화면 높이/DPI에서만 나오므로 사실상 상수다 —
        /// 매 호출 재계산하지 않고 <see cref="VisualHalfWidthRefreshInterval"/> 주기로 갱신한 값을
        /// 돌려준다(FX 조각 생성처럼 자주 도는 경로에서 Camera.main/Screen을 반복해 읽지 않기 위해).</para>
        /// </summary>
        public float MinStrokeWorldWidth => _minStrokeWorldWidth > 0f
            ? _minStrokeWorldWidth
            : StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

        /// <summary>
        /// ★ <b>채운 도형의 경계선</b> 전용 하한(월드 유닛) —
        /// <see cref="StickConfig.MinFillOutlineScreenPoints"/>의 환산값(2026-09-02 M6).
        /// 위 <see cref="MinStrokeWorldWidth"/>와 <b>같은 pt/유닛</b>으로 환산하므로 둘의 비는 언제나
        /// 1:2다 — 환산을 두 번 적으면 화면이 바뀔 때 한쪽만 따라간다.
        /// <para>소비자: <see cref="Interaction.CharacterAccessoryRenderer"/>(채움 도형의 윤곽선),
        /// 그리고 이 클래스의 두 되올리기 경로.</para>
        /// </summary>
        public float MinFillOutlineWorldWidth => _minFillOutlineWorldWidth > 0f
            ? _minFillOutlineWorldWidth
            : StickConfig.MinFillOutlineScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

        private float _minStrokeWorldWidth;
        private float _minFillOutlineWorldWidth;

        /// <summary>화면상 최소 획 두께 <b>두 종류</b>를 월드 유닛으로 환산해 캐시한다. 카메라의 직교
        /// 크기와 화면 높이(포인트)를 실측해서 쓰므로, 프리팹을 구울 때의 근사(창 높이 846pt 고정)보다
        /// 정확하다. 카메라가 없으면 그 근사로 되메운다 — 0을 흘리면 하한이 조용히 사라진다.
        /// <para>한 함수에서 둘을 함께 구하는 이유: pt/유닛 환산은 <b>하나</b>이고 하한만 둘이다.
        /// 함수를 둘로 쪼개면 카메라를 두 번 읽게 되고, 그 사이에 화면이 바뀌면 두 하한이 서로 다른
        /// 디스플레이를 말하게 된다.</para></summary>
        private void RefreshStrokeFloors()
        {
            float pointsPerWorldUnit = ResolvePointsPerWorldUnit();
            _minStrokeWorldWidth = StickConfig.MinStrokeScreenPoints / pointsPerWorldUnit;
            _minFillOutlineWorldWidth = StickConfig.MinFillOutlineScreenPoints / pointsPerWorldUnit;
        }

        /// <summary>월드 1유닛이 몇 OS 포인트인가(실측). 못 재면 프리팹 굽기와 같은 근사로 되메운다.</summary>
        private float ResolvePointsPerWorldUnit()
        {
            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null || !cam.orthographic || Screen.height <= 0)
                return StickConfig.ReferencePointsPerWorldUnitApprox;

            // 화면 높이(OS 포인트) = Unity 픽셀 높이 × (OS 포인트 / Unity 픽셀).
            float screenHeightPoints = Screen.height * ScreenCoordinateConverter.ResolveDpiScale(_config);
            if (screenHeightPoints <= 1f) return StickConfig.ReferencePointsPerWorldUnitApprox;

            float pointsPerWorldUnit = screenHeightPoints / (2f * cam.orthographicSize);
            return pointsPerWorldUnit <= 0.0001f
                ? StickConfig.ReferencePointsPerWorldUnitApprox
                : pointsPerWorldUnit;
        }

        private void TickFullscreenSuspend(float deltaTime)
        {
            _fullscreenPollTimer += deltaTime;
            float interval = _config != null ? Mathf.Max(0.1f, _config.fullscreenPollInterval) : 1f;
            if (_fullscreenPollTimer < interval) return;
            _fullscreenPollTimer = 0f;

            // ★ 2026-09-01 설정창 [일반] "전체화면 게임 감지 시 자동 숨김" 토글의 <b>유일한 게이트</b>.
            //   기본값은 켬이고(AppSettingsModel), 끄는 것은 사용자의 명시적 선택이다 — 절대 불변 원칙 2를
            //   사용자가 스스로 면제하는 자리이므로 그 판단을 코드가 대신하지 않는다.
            //   판정을 여기 한 줄로 둔 이유: Suspend/Resume의 대칭이 자동으로 성립한다. 숨어 있는 동안
            //   토글을 끄면 fullscreenActive가 false가 되어 다음 폴링에서 Resume()이 정확히 한 번 돈다.
            bool fullscreenActive = _platformService.IsFullscreenAppActive() && AppSettingsModel.AutoHideOnFullscreen;
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
            // IsSuspended를 직접 폴링해 별도로 취소한다(다른 Director들의 IsSuspended 폴링과 동일 패턴).
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
            //
            // ★★ 2026-09-02 정정 — 위 문장은 **deltaTime 누적 타이머에만** 참이었다. 이 코드베이스에는
            //    벽시계 절대 기한(Time.time + duration)이 딱 하나 있고(drop-through 유예 / 발 떼기 이송,
            //    States/StickmanBlackboard.cs), 그것은 Tick을 건너뛰어도 계속 흘러간다. 하강 도중
            //    전체화면이 0.25초 창에 겹치면 Resume 시점에 둘 다 만료돼 그 하강이 조용히 무효가 됐다
            //    (결과는 자기회복 — Dock에 도로 착지 — 이지만 문서화된 계약이 거짓이었다).
            //    아래 한 줄이 잔여 시간을 얼려 Resume에서 재기점한다 = 이제 계약이 실제로 참이다.
            _blackboard?.SuspendAbsoluteTimeWindows();

            SetBodiesSimulated(false); // 물리 시뮬레이션도 함께 멈춰 숨겨진 동안 위치가 흐트러지지 않게 함.
            SetRenderersEnabled(false);

            // 2026-08-31 성능 라운드: 물리/렌더러를 껐어도 앱은 여전히 초당 60번 "빈 화면"을 그려 OS
            // 컴포지터에 제출한다. 이 앱은 전체화면 투명 오버레이라 프레임 제출 1회 = 화면 전체 재합성이고,
            // 그 비용은 하필 사용자가 전체화면 게임을 하는 바로 그 순간에 부과된다(비침해 원칙 2의 구멍).
            // 숨겨져 있는 동안에는 부드러움이 아무 의미가 없으므로 프레임을 더 깊게 조인다.
            // 에디터/테스트에서는 FramePacing이 적용된 적이 없어 이 호출이 통째로 no-op이다.
            Platform.FramePacing.SetSuspended(true);

            // ★ 2026-08-29 (리더 지시) — 여기에 로그가 **한 줄도 없었다**. 사용자 신고 "캐릭터가 안
            // 보이다가 클릭하면 나타난다"를 조사할 때 "전체화면 Suspend 때문인가?"를 가릴 수단이
            // 전혀 없어서 Player.log 전수를 뒤져야 했다. 캐릭터가 화면에서 사라지는 것은 이 앱에서
            // 가장 눈에 띄는 사건이므로 사유와 함께 반드시 남긴다(판정 근거 창 이름/bounds는 바로 앞
            // 줄에 [전체화면판정] 로그로 남는다 — Platform/MacOS/MacWindowService.cs 참고).
            Debug.Log($"[전체화면숨김] 전체화면 앱이 감지되어 캐릭터를 숨기고 물리를 멈춥니다(비침해 원칙 2) — " +
                $"숨기기 직전 상태={current}, 몸 렌더러 {( _renderers != null ? _renderers.Length : 0)}개 + " +
                $"액세서리/펫/FX {_dynamicVisuals.Count}개 비활성화. " +
                "직전 [전체화면판정] 줄에 어느 창 때문인지가 적혀 있습니다.");
            // TODO(Phase 2 렌더링 레이어): 즉시 on/off 대신 ≤200ms 페이드 아웃/인 연출 추가.
        }

        private void Resume()
        {
            _isSuspended = false;
            // Suspend()에서 얼려 둔 절대 기한을 **지금**을 기준으로 다시 세운다(그 주석 참고) —
            // 숨어 있던 시간만큼 창이 뒤로 밀린다. 물리를 켜기 전에 해야 첫 FixedUpdate가 이미
            // 재기점된 창을 본다.
            _blackboard?.ResumeAbsoluteTimeWindows();
            SetBodiesSimulated(true);
            // BUG-P5-M1 대응(Major, docs/BUG_REPORT_PHASE5.md): 예전에는 여기서 무조건
            // SetRenderersEnabled(true)를 호출해, 가출(RunawayState) Hidden 페이즈 중 전체화면 Suspend/
            // Resume이 왕복하면 아직 발견되지 않은 캐릭터가 강제로 다시 보이게 되는 버그가 있었다.
            // RunawayState가 자신의 은신 가시성 의도를 IsCharacterHiddenByRunaway로 알려오는 동안은
            // 이 무조건 복원을 건너뛴다(StickmanBlackboard.IsCharacterHiddenByRunaway 문서 참고) —
            // "Suspend/Resume의 렌더러 제어"가 "Runaway의 렌더러 제어"를 마지막에 실행됐다는 이유만으로
            // 덮어쓰지 않게 한다.
            bool hiddenByRunaway = _blackboard != null && _blackboard.IsCharacterHiddenByRunaway;
            if (!hiddenByRunaway)
            {
                SetRenderersEnabled(true);
            }

            // Suspend()에서 조였던 프레임 상한을 평소 값으로 되돌린다(위 주석 참고).
            Platform.FramePacing.SetSuspended(false);

            // Suspend()와 짝을 이루는 로그(위 주석 참고). 가출 은신 중이라 일부러 감춰둔 경우를
            // 명시적으로 구분해 적는다 — 그러지 않으면 "Resume 했는데도 캐릭터가 안 보인다"가
            // 또 원인 불명 신고가 된다.
            Debug.Log("[전체화면숨김] 해제 — 물리를 재개했습니다. " +
                (hiddenByRunaway
                    ? "단, 지금은 가출(Runaway) 은신 중이라 캐릭터는 일부러 계속 숨겨둡니다(클릭해 찾으면 나타납니다)."
                    : "캐릭터를 다시 보이게 했습니다."));
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

        /// <summary>
        /// 캐릭터를 그 프레임에 보이게/안 보이게 한다. 전체화면 자동 숨김(원칙 2)과 가출 은신이
        /// 공유하는 유일한 통로다(<see cref="StickmanBlackboard.SetCharacterVisible"/>).
        ///
        /// ★ 2026-08-31 — <b>숨기기는 몸 바깥의 잉크까지 같은 프레임에</b> 끈다.
        /// 예전에는 Awake에서 캐시한 몸 12개만 껐고, 액세서리/펫/FX는 자기 HeadOutline을 관찰해
        /// 0.18~0.25초에 걸쳐 <b>페이드아웃</b>했다. 그 사이 사용자가 방금 켠 전체화면 게임 위에
        /// "몸 없는 모자·망토·펫 공·반짝임"이 그대로 떠 있었다(실측: SUSPEND+0f에서 몸 0개 / 액세서리
        /// 12개 / 펫 12개 / FX 12개). 가출 숨바꼭질에서는 그것들이 숨은 자리를 알려줬다.
        /// 원칙 2는 "감지 시 자동 숨김"이지 "감지 후 0.25초 뒤 숨김"이 아니다.
        ///
        /// <para>★ 반대로 <b>다시 보이게 할 때는 몸만</b> 켠다(의도된 비대칭). 지금 무엇을 그려야 하는가
        /// (장비를 착용 중인가/해금됐는가/랙돌인가/펫 아이템이 무엇인가)는 각 소유자가 우리보다 정확히
        /// 안다 — 여기서 무조건 켜면 벗어 둔 장비의 옛 렌더러까지 한 프레임 번쩍인다. 소유자의
        /// 페이드인은 원칙 위반이 아니다(숨기는 쪽만 즉시여야 한다).</para>
        /// </summary>
        private void SetRenderersEnabled(bool enabled)
        {
            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null) _renderers[i].enabled = enabled;
                }
            }

            if (enabled) return;

            _dynamicVisuals.Refresh();
            for (int i = 0; i < _dynamicVisuals.Count; i++)
            {
                Renderer r = _dynamicVisuals[i].Renderer;
                if (r != null) r.enabled = false;
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
