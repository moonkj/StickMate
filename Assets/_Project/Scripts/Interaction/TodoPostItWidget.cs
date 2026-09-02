using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 17절 투두 말풍선 "포스트잇 모드"(다건 노출) — 화면 한쪽에 고정된 카드로 최대
    /// StickConfig.todoPostItMaxVisibleRows줄을 보여주고 체크박스 클릭으로 완료 처리한다.
    ///
    /// ============================================================================
    /// 기술 요구사항(17절 Coder 참고 — 반드시 지킬 것): 이 위젯의 체크박스 클릭 판정은 완전히 독립적인
    /// 경로다.
    /// ============================================================================
    /// - UX_FLOW.md 17절은 "포스트잇은 15절 부분적 클릭관통 해제 단일 소유자 락(Core.SpectacleEventLock/
    ///   Platform.ILocalClickCaptureService)을 재사용하지 말고 별도의 독립 항상-위 위젯 창으로 구현할
    ///   것"을 권고했다. 지금 아키텍처에는 진짜 별도 OS 위젯 창(HWND/NSWindow)을 만들 인프라가 없어
    ///   (BUG-B1, 진짜 분리 오버레이 자체가 아직 없음) 최소 스코프로는 이 앱 자신의 Unity UI(Canvas
    ///   ScreenSpaceOverlay)로 포스트잇을 표현한다.
    /// - 체크박스(Button)의 클릭 판정은 uGUI의 자체 GraphicRaycaster + EventSystem만으로 처리된다.
    ///   이 파일은 Core.SpectacleEventLock, Platform.ILocalClickCaptureService,
    ///   Interaction.StickmanClickHitbox 중 어느 것도 참조하지 않는다(grep으로 검증 가능) — 캐릭터의
    ///   "부분적 클릭관통 해제" 인프라(동적 히트박스 추적, 단일 소유자 락)와는 아무 관계가 없다. 포스트잇은
    ///   고정 위치의 독립 UI이므로 그 락 경쟁에 끼워 넣을 이유가 없다는 17절의 논리를 코드 구조로도 강제한다.
    /// - 대사(원칙 1)와도 무관하다 — 포스트잇 체크박스는 DialogueIntent를 만들지 않는다(캐릭터 상태 전이가
    ///   아니라 순수 UI 상호작용이므로 애초에 적용 대상이 아니다).
    ///
    /// 데이터 소스는 Core.TodoListModel(정적) — 이 위젯은 StickmanEventBus.TodoListChanged를 구독해
    /// 뷰만 갱신한다("들고 다니는 모드"를 담당하는 Interaction/TodoReminderDirector.cs와 데이터를 공유하되
    /// 서로의 존재를 모른다).
    ///
    /// ============================================================================
    /// ★ 2026-08-29 실배선 라운드 — 체크박스 클릭이 <b>구조적으로 불가능</b>했던 두 가지를 고쳤다
    /// ============================================================================
    /// 리더가 "uGUI 클릭이 Play 모드에서 실제로 발동하는지 한 번도 검증된 적 없다"고 지목한 항목을
    /// 실측한 결과, <b>두 겹으로 막혀 있었다</b>. 둘 중 하나만 고쳐도 여전히 클릭이 안 된다.
    ///
    /// (1) <b>EventSystem에 입력 모듈이 없었다.</b> Assets/Editor/SceneBootstrapper.cs의
    ///     EnsureEventSystem()이 EventSystem만 붙이고 StandaloneInputModule은 "이 프로젝트에는 uGUI
    ///     Canvas가 하나도 없다"는 (당시엔 맞았던) 이유로 일부러 생략했다. 입력 모듈이 없으면
    ///     EventSystem은 포인터 이벤트를 아예 처리하지 않으므로 Button.onClick이 <b>영원히</b> 발동하지
    ///     않는다. 게다가 이 위젯의 EnsureEventSystem()은 "EventSystem.current가 null일 때만" 자기
    ///     것을 만들기 때문에, 씬에 (모듈 없는) EventSystem이 이미 있다는 이유로 조용히 건너뛰었다.
    ///     -> SceneBootstrapper가 StandaloneInputModule을 함께 붙이도록 고쳤고, 이 위젯도 기존
    ///        EventSystem에 모듈이 없으면 보강하도록 바꿨다.
    ///
    /// (2) <b>OS 레벨에서 클릭이 이 창까지 오지 않았다.</b> 이 앱의 창은 클릭관통이고, 관통을 푸는
    ///     판정은 UniWindowController의 hitTestType=Raycast가 <b>커서 아래 Collider2D 유무</b>로 한다
    ///     (Assets/Editor/SceneBootstrapper.cs의 ConfigureUniWindowController 문서). uGUI Graphic은
    ///     Collider2D가 아니므로 포스트잇 카드 위는 계속 관통 상태였고, 클릭은 카드를 통과해 밑의 다른
    ///     앱으로 갔다.
    ///     -> Interaction/AppControlDirector.cs의 메뉴 차단막(_menuBlocker)과 <b>같은 관례</b>로,
    ///        카드가 보이는 동안만 카드의 화면 사각형을 덮는 isTrigger BoxCollider2D를 켠다.
    ///
    /// 그리고 (2)를 고쳐도 macOS에서 비활성 앱의 첫 클릭이 "앱 활성화"에만 소비될 수 있으므로
    /// (Interaction/StickmanClickHitbox.cs 클래스 문서의 acceptsFirstMouse 문제), AppControlDirector가
    /// 메뉴 행 클릭에 쓰는 것과 <b>같은 전역 폴링 히트테스트</b>를 두 번째 경로로 함께 둔다. 두 경로는
    /// 같은 핸들러를 부르고 <see cref="ActionDedupSeconds"/> 창으로 중복을 막는다 — StickmanClickHitbox의
    /// "이중 입력 경로" 관례 그대로다.
    ///
    /// 이 변경은 17절/25절-17의 "포스트잇은 15절 단일 소유자 락을 공유하지 않는다"를 그대로 지킨다:
    /// 아래 코드는 Core.SpectacleEventLock / Platform.ILocalClickCaptureService /
    /// Interaction.StickmanClickHitbox 중 어느 것도 여전히 참조하지 않는다(grep으로 검증 가능).
    /// 새로 참조하는 것은 전역 <b>조회</b> 서비스(IGlobalPointerButtonService)뿐이고, 그것은 락이 아니다.
    /// </summary>
    public sealed class TodoPostItWidget : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;
        [SerializeField] private bool _featureEnabledDefault = true;

        private const int SortingOrderTopMost = 30000;

        // ============================================================================
        // ★ 2026-09-01 P0-2 — 이 카드는 앱에서 <b>유일하게 디자인 시스템 밖</b>에 있었다
        // ============================================================================
        // 실측(docs/UI_SURFACE_SPEC.md §4): 표면 <c>new Color(1, 0.95, 0.6, 0.92)</c> · 직각 모서리 ·
        // 그림자 0 · 테두리 0 · <c>fontSize = 14/12</c> 생 리터럴 · 색 생 리터럴 · 폰트 직접 로드.
        //
        // 그중 <b>알파 0.92가 치명적</b>이었다. 이 앱의 창 뒤에는 유저의 진짜 데스크톱이 있어서
        // α&lt;1은 "반투명해 보인다"가 아니라 <b>카드 색 자체가 배경에 따라 변한다</b>는 뜻이다:
        //     진한 파랑 데스크톱(#3b4fd8) 위 → #efe59e / 흰 문서(#ffffff) 위 → #fff3a1  (ΔL 11.1%p)
        // 정보창·설정창·팝오버는 2026-08-31에 정확히 이 이유로 전부 α=1이 됐는데, 포스트잇만
        // 그 라운드에서 빠져 <b>폐기된 알파 유리 규약이 여기만 살아남았다</b>.
        //
        // 이제 다른 창과 같은 <see cref="UiChrome.AddOpaquePanel"/> 구조[본체(α1) → 보더]를
        // 쓴다. "노란 포스트잇"이라는 정체성은 표면 전체가 아니라 <b>왼쪽 4pt 띠</b>가 진다 —
        // 색은 남고 알파 문제는 사라진다.

        private const float RowHeight = 28f;          // 26 -> 28 (12pt 글자 + 위아래 8)
        private const float PanelWidth = 220f;
        private const float PanelPadding = UiChrome.Space3;   // 8 -> 12 (토큰)

        /// <summary>화면 구석에서 카드까지의 기본 여백(가로·세로 공통).
        /// <list type="bullet">
        ///  <item><b>가로</b>: 이 값은 <b>화면 여백</b>이지 화면 오른쪽 끝에서의 거리가 아니다. OS가 예약한
        ///        <b>측면</b> 띠(우측 도킹 작업표시줄 48~62pt / 우측 Dock)가 있으면 그 두께가 앞에 더 붙고,
        ///        톱니와 겹치면 거기서 더 밀린다 — <see cref="ResolveRightInsetBoundsPoints"/> ·
        ///        <see cref="ResolveRightInsetPoints"/>. 측면 띠가 0이거나 <b>못 쟀으면</b> 이 값 그대로다.</item>
        ///  <item><b>세로</b>: 이 값은 <b>화면 여백</b>이지 화면 꼭대기에서의 거리가 아니다. OS가 예약한
        ///        상단 띠가 있으면 그 두께가 앞에 더 붙는다 — <see cref="ResolveTopInsetPoints"/>.
        ///        예약 띠가 0이면 세로도 이 값 그대로다(회귀 없음이 구조적으로 보장된다).</item>
        /// </list>
        /// <para>★ <b>public</b>인 이유는 <see cref="ExpandedMaxRows"/>와 같다 — 테스트가 이 값을
        /// 숫자로 베끼지 않고 참조해야 한다(CLAUDE.md — 프로덕션 상수 하드코딩 금지).</para></summary>
        public const float PanelInsetPoints = UiChrome.Space4;   // 16

        /// <summary>"이건 메모다"를 말하는 왼쪽 세로 띠. 표면 전체를 노랗게 칠하는 대신 색만 남긴다.</summary>
        private const float AccentStripeWidth = 4f;

        /// <summary>포스트잇 노랑 — <b>띠에만</b> 쓴다(글자 배경으로 쓰지 않으므로 대비 규칙 대상이 아니다).</summary>
        private static readonly Color PostItStripe = new Color(0.961f, 0.843f, 0.431f, 1f);   // #f5d76e

        /// <summary>그림자 등급은 팝오버 3종과 같다 — 같은 위계(작고 화면에 떠 있는 카드)이므로
        /// 같은 값이어야 한다. <see cref="PopoverPanel"/>이 이 두 값을 바꾸면 여기도 함께 바꾼다.</summary>
        /// <summary>헤더의 [숨기기] 칩. 32-1 최소 클릭 타깃보다 작지만 <b>파괴적이지 않은</b> 행동이고
        /// (그 세션 동안만 숨긴다) 카드 폭이 220pt뿐이라 여기서는 칩 크기로 둔다.</summary>
        private const float ChipWidth = 52f;
        private const float ChipHeight = 20f;

        // ============================================================================
        // ★ 2026-09-02 P0 — 펼침이 <b>무기한</b> 화면 6.994%의 클릭관통을 해제하고 있었다
        // ============================================================================
        // 근거: docs/UX_WIDGETS.md §3-2. 실측(기준 화면 1512×982pt = 1,484,784pt²):
        //
        //   접힘 4행        : 220 × 164 =  36,080pt² = 2.430%
        //   펼침 @15건      : 220 × 472 = 103,840pt² = 6.994%   ← 상한이 없어 항목 수만큼 자란다
        //   펼침 @상한 8행  : 220 × 276 =  60,720pt² = 4.089%
        //
        // <b>SyncClickThroughBlocker()가 패널 사각형 전부를 덮으므로 저 면적은 그대로
        // "클릭관통이 해제된 면적"이다.</b> 그리고 <c>_expanded</c>를 되돌리는 경로가
        // <b>사용자 재클릭 하나뿐</b>이었다 — 타이머도 무입력 자동 접힘도 없었다.
        // 24시간 상주 앱에서 "펼쳐 두고 잊는다"는 예외가 아니라 기본값이다(절대 불변 원칙 2).
        //
        // 고친 것은 둘이다: (1) 펼침 행 상한, (2) 무입력 자동 접힘.

        /// <summary>
        /// 펼쳤을 때의 행 상한. <b>8</b>인 근거는 면적 비교다 —
        /// 10행이면 73,040pt²(4.919%)로 <b>집중 팝오버 244×252(61,488pt², 4.141%)를 넘는다.</b>
        /// 상시 카드가 "사용자가 직접 연 창"보다 커서는 안 된다. 8행은 60,720pt²(4.089%)로 그 아래다.
        /// <para>★ <b>public</b>인 이유: 테스트가 이 값을 숫자로 베끼지 않고 참조해야 한다
        /// (CLAUDE.md — 프로덕션 상수 하드코딩 금지).</para>
        /// <para>★ 상한을 넘는 나머지는 이 카드가 안내하지 <b>않는다</b>. docs/UX_WIDGETS.md §3-2(b)는
        /// 그 자리에 <c>[전체 보기]</c>(→ 할일 팝오버)를 두자고 제안했지만 그것은 새 문구 + 팝오버
        /// 배선이라 이 라운드 범위가 아니다(리더 판정: 행 상한 + 자동 접힘만). 미구현으로 보고했다.</para>
        /// </summary>
        public const int ExpandedMaxRows = 8;

        /// <summary>
        /// 펼침이 스스로 접히는 무입력 임계(초). <b>값의 정본은 하나다</b> —
        /// <see cref="PopoverPanel.DefaultIdleAutoCloseSeconds"/>(180초)를 그대로 참조한다.
        /// 새 상수를 만들면 두 표면의 "자리를 비웠다" 기준이 조용히 갈라진다.
        /// <para>그 상수 문단이 왜 180인지 이미 적어 뒀다: 부채꼴의 6초는 "훑는 동안" 닫혀 사고가 되고,
        /// 3분은 사람이 화면 앞에서 만들 수 있는 무입력이 아니라 <b>자리를 뜬 것</b>이다.</para>
        /// <para>테스트가 이 값을 낮추려면 <see cref="PopoverPanel.SetIdleAutoCloseSecondsForTests"/>를
        /// 쓴다 — 3분을 진짜로 기다리는 테스트는 만들지 않는다.</para>
        /// </summary>
        public static float IdleAutoCollapseSeconds => PopoverPanel.IdleAutoCloseSeconds;

        /// <summary>무입력 시계를 다시 재는 주기(초). <see cref="PopoverPanel"/>과 같은 0.25초 —
        /// 3분짜리 판정에 매 프레임 OS 커서를 물을 이유가 없다.</summary>
        private const float IdlePollInterval = 0.25f;

        /// <summary>이보다 작은 커서 이동은 손떨림/좌표 반올림으로 본다(픽셀). PopoverPanel과 같은 값.</summary>
        private const float IdleCursorEpsilonPixels = 2f;

        // ---- 접힘 사유. <b>자동 접힘이 "사용자가 닫았다"로 읽히면 안 된다.</b> 부채꼴이 같은 문제를
        //      겪고 GearRadialMenuWidget.ModeLabel()로 사유를 갈랐다 — 그 어법을 그대로 쓴다.
        //      열거형 대신 상수 문자열 두 개인 이유: 값이 둘뿐이라 switch가 생기지 않고,
        //      switch가 없으면 "새 항목이 default:로 조용히 사라지는" 실패 모드 자체가 없다.
        private const string CollapseReasonUser = "사용자 동작";
        private const string CollapseReasonAuto = "무반응 자동";

        private sealed class RowWidgets
        {
            public GameObject Root;
            public Button CheckboxButton;
            public Text Label;
            public int TodoId;

            /// <summary>완료 항목의 취소선. 레거시 uGUI <c>Text</c>에는 취소선 태그가 없어
            /// <b>폭을 재서 직접 긋는다</b>. 글자 내용이나 완료 여부가 바뀐 순간에만 다시 잰다.</summary>
            public Image Strike;

            public string LastLabelText;
            public bool LastCompleted;
        }

        private Canvas _canvas;
        private CanvasScaler _scaler;   // Retina 대응 — 캔버스 1유닛 == OS 포인트 1로 맞춘다(ApplyCanvasScaleFactor).
        private RectTransform _panelRoot;
        private RectTransform _rowContainer;
        private readonly List<RowWidgets> _rows = new List<RowWidgets>();
        private Text _moreLabel;
        private Button _moreButton;
        private Button _hideButton;

        private bool _featureEnabled;
        private bool _sessionHidden; // 17절 "[숨기기]" — 그 세션 동안만, 데이터는 유지.
        private bool _hiddenForFullscreen; // 원칙 2 자동 숨김(사용자 의사가 아니므로 _sessionHidden과 별개).
        private bool _expanded;
        private float _sweepTimer;

        // ---- 펼침 무입력 자동 접힘(위 상수 문단이 근거).
        private float _idleSeconds;
        private float _idlePollTimer;
        private Vector2 _lastCursorSample;
        private bool _hasCursorSample;
        private bool _hasTestCursor;
        private Vector2 _testCursor;

        // ---- 클릭 경로 2(전역 폴링). 클래스 문서 "★ 2026-08-29 실배선 라운드" 참고.
        private const float ClickPollInterval = 0.05f;   // 20Hz — AppControlDirector와 같은 주기.
        private const float ActionDedupSeconds = 0.35f;  // 두 경로가 같은 클릭을 두 번 처리하지 않게.
        private StickmanAgent _agent;
        private StickConfig _agentConfig;

        /// <summary>같은 GameObject의 톱니 — 카드가 그 히트 사각형을 비켜 앉는다(51-9-3).
        /// 씬 전체 탐색 폴백을 두지 않는 이유는 <see cref="_agent"/>와 같다(복제본 방어).</summary>
        private InfoGearIconWidget _gear;
        private IGlobalPointerButtonService _buttonService;
        private BoxCollider2D _clickThroughBlocker;      // 카드가 보이는 동안만 켜지는 히트테스트용.
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private string _lastActionKey;
        private float _lastActionTime;

        // ==================== 관측 창구(테스트/진단 전용, 읽기만) ====================

        /// <summary>카드가 실제로 화면에 켜져 있는가. 플래그가 아니라 GameObject의 <b>실제 상태</b>를
        /// 읽는다 — 원칙 2 회귀 테스트가 "플래그는 맞는데 화면엔 남아 있는" 경우를 잡아야 한다.</summary>
        public bool IsCardVisible => _panelRoot != null && _panelRoot.gameObject.activeInHierarchy;

        /// <summary>클릭관통 차단막이 켜져 있는가. 카드가 안 보이는데 이것이 켜져 있으면
        /// "안 보이는데 클릭만 먹는" 최악의 형태다.</summary>
        public bool IsClickBlockerEnabled => _clickThroughBlocker != null && _clickThroughBlocker.enabled;

        /// <summary>지금 <b>실제로 켜져 있는</b> 할일 행 수. 플래그가 아니라 GameObject의 상태를 센다 —
        /// 행 상한 회귀 테스트는 "숫자는 맞는데 화면엔 15줄이 있는" 경우를 잡아야 한다.</summary>
        public int VisibleRowCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (_rows[i]?.Root != null && _rows[i].Root.activeSelf) n++;
                }
                return n;
            }
        }

        /// <summary>펼침 상태(진단/테스트 전용).</summary>
        public bool IsExpandedForTests => _expanded;

        /// <summary>지금까지 누적된 무입력 시간(초) — 진단/테스트 창구.</summary>
        public float IdleSecondsForTests => _idleSeconds;

        /// <summary>테스트 전용 — 무입력 판정이 볼 커서를 이 자리에 고정한다.
        /// PlayMode는 진짜 OS 커서를 원하는 자리에 <b>붙잡아 둘</b> 수 없다(테스트 도중 사람이 마우스를
        /// 건드리면 시계가 리셋된다). <see cref="PopoverPanel.FeedIdleCursorForTests"/>와 같은 관례다.</summary>
        public void FeedIdleCursorForTests(Vector2 cursorUnityScreen)
        {
            _hasTestCursor = true;
            _testCursor = cursorUnityScreen;
        }

        /// <summary>주입한 커서를 걷고 실제 OS 커서로 되돌린다.</summary>
        public void ClearIdleCursorForTests() => _hasTestCursor = false;

        private void Awake()
        {
            _featureEnabled = _featureEnabledDefault;
            // 같은 GameObject의 StickmanAgent만 쓴다(씬 전체 탐색 폴백 없음) — 복제본에 이
            // 위젯이 남아 있어도 카드가 두 벌 뜨지 않게 하는 2차 방어. 1차 방어는 애초에 사본에
            // 이 컴포넌트를 배치하지 않는 것이다.
            _agent = GetComponent<StickmanAgent>();
            if (_config == null && _agent != null) _config = _agent.Config;
            BuildUi();
        }

        private void Start()
        {
            _agentConfig = _agent != null ? _agent.Config : _config;
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;
            _gear = GetComponent<InfoGearIconWidget>();

            // ★ EventSystem.currentInputModule을 보면 안 된다 — 그 프로퍼티는 EventSystem.Update()가
            // 한 번 돌아야 채워지므로 Start() 시점에는 모듈이 멀쩡히 붙어 있어도 null로 보인다(첫
            // 실측에서 "입력 모듈 없음"으로 잘못 보고했다). 컴포넌트 존재 자체를 확인한다.
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<BaseInputModule>() : null;
            Debug.Log("[투두] 포스트잇 위젯 준비 완료 — " +
                $"EventSystem={(EventSystem.current != null ? "있음" : "★없음(uGUI 클릭 불가)")}, " +
                $"입력 모듈={(module != null ? module.GetType().Name + "(활성=" + module.isActiveAndEnabled + ")" : "★없음(Button.onClick이 영원히 발동하지 않는다)")}, " +
                $"전역 폴링 경로={(_buttonService != null ? "사용 가능" : "미지원 — uGUI 경로만")}, " +
                $"클릭관통 차단막={(_clickThroughBlocker != null ? "준비됨" : "★없음")}, " +
                $"톱니 회피={(_gear != null ? $"배선됨(지금 가로 인셋 {ResolveRightInsetPoints():F2}pt)" : "★없음 — 같은 GameObject에 톱니가 없어 카드가 톱니를 덮을 수 있습니다")}. " +
                $"현재 미완료 {TodoListModel.UncompletedCount}건(0이면 17절 '빈 상태 예외'로 카드를 숨긴다).");
        }

        private void OnEnable()
        {
            StickmanEventBus.TodoListChanged += OnTodoListChanged;
            RefreshView();
        }

        private void OnDisable()
        {
            StickmanEventBus.TodoListChanged -= OnTodoListChanged;
            // 카드가 꺼진 채 차단막만 켜져 있으면 화면의 그 영역이 이유 없이 클릭관통 해제 상태로
            // 남는다 — 비침해 원칙(CLAUDE.md 2) 직결이라 반드시 함께 끈다.
            if (_clickThroughBlocker != null) _clickThroughBlocker.enabled = false;
        }

        private void OnDestroy()
        {
            if (_clickThroughBlocker != null) Destroy(_clickThroughBlocker.gameObject);
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측

            // ★★ 절대 불변 원칙 2(비침해) — 전체화면 앱이 감지되면 카드와 차단막을 그 프레임에 거둔다.
            //
            // 이 가드가 <b>없었다</b>(2026-09-01 실측 지적: 이 파일의 전체화면 감지 참조가 0건).
            // 같은 종류의 차단막을 가진 다른 표면들(정보창/설정창/톱니/팝오버)은 전부 폴링하는데
            // 이 하나만 빠져 있었다. StickmanAgent.Suspend()는 Awake에서 캐시한 <b>캐릭터 렌더러만</b> 끄고, 이
            // 카드는 씬 루트 캔버스 + 씬 루트 차단막이라 그 배열에 없다. 게다가 StickmanAgent가
            // SetAlwaysOnTop(true)를 켜므로 전체화면 앱 <b>위에</b> 카드가 그대로 뜨고,
            // SyncClickThroughBlocker()가 매 프레임 차단막을 켜므로 그 영역의 클릭까지 먹는다.
            //
            // ★ 2026-09-02 — 읽는 창구가 <c>IsSuspended</c>에서 <c>ArePanelsSuppressed</c>로 바뀌었다.
            // 카테고리를 선언하지 않은 전체화면 앱(화상회의·발표 부류)에서는 <c>IsSuspended</c>가
            // <b>영원히 false</b>다. 그 등급(등급 1)에서도 이 카드는 남의 화면 위에 뜬 채 자기 사각형의
            // 클릭을 계속 먹었다 — 등급 1은 <b>패널만</b> 걷는 축이고 이 차단막이 정확히 그 대상이다.
            // 톱니는 등급 2에 남는다(복구가 톱니 1클릭이라 그것까지 걷으면 안전판이 자기를 지운다).
            //
            // 정보창/설정창과 달리 <b>복귀하면 다시 나타난다</b>. 저 둘은 "사용자가 연 창"이라 전체화면을
            // 끄자마자 튀어나오면 그 자체가 방해지만, 이 카드는 할 일이 있는 동안 늘 떠 있는 상시
            // HUD다(톱니 아이콘이 복귀하는 것과 같은 판단).
            if (_agent != null && _agent.ArePanelsSuppressed)
            {
                if (!_hiddenForFullscreen) EnterFullscreenHiding();
                return;
            }
            if (_hiddenForFullscreen) ExitFullscreenHiding();

            ApplyCanvasScaleFactor(); // 배율은 실행 중에 바뀔 수 있다(모니터 이동/시작 직후 창 확장).
            SyncPanelInsetToGear();   // 톱니는 드래그로 움직인다 — 자리를 매 프레임 다시 푼다.
            TickGlobalClickPolling();
            SyncClickThroughBlocker();
            TickIdleAutoCollapse();

            // 완료 유예 정리(17절 "짧은 유지 후 목록에서 자동 정리")는 매 프레임 돌 필요가 없다 —
            // 다른 저빈도 폴러들과 동일한 절제 컨벤션으로 0.5초 주기로 제한.
            _sweepTimer += Time.deltaTime;
            if (_sweepTimer < 0.5f) return;
            _sweepTimer = 0f;
            float linger = _config != null ? _config.todoCompletedLingerSeconds : 2.5f;
            TodoListModel.SweepCompleted(linger);
        }

        // ==================== 클릭 경로 2: 전역 폴링 히트테스트 ====================

        /// <summary>
        /// AppControlDirector.TickRightClickMenu()의 행 히트테스트와 <b>같은 방식</b>이다 — 창 포커스와
        /// 무관한 전역 버튼 상태 + 전역 커서 좌표로 직접 판정한다. uGUI 경로가 살아 있으면 둘 중 먼저
        /// 잡는 쪽이 처리하고 <see cref="TryClaimAction"/>이 중복을 막는다.
        /// </summary>
        private void TickGlobalClickPolling()
        {
            if (_buttonService == null || _panelRoot == null || !_panelRoot.gameObject.activeSelf) return;

            _clickPollTimer += Time.unscaledDeltaTime;
            if (_clickPollTimer < ClickPollInterval) return;
            _clickPollTimer = 0f;

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            // ★ 여기만 홀드를 거는 이유 — 이 포스트잇은 <b>할 일이 있으면 하루 종일 떠 있는 상시
            // HUD</b>다. 다른 UI들처럼 "보이는 동안"으로 걸면 Calm 등급이 영영 성립하지 않아
            // 적응형 절감이 통째로 무력화된다(정보창/부채꼴/구석패널은 전부 수명이 짧거나 커서가
            // 붙어 있어야 유지되는 표면이라 사정이 다르다).
            // 그래서 조건은 "보인다"가 아니라 <b>지금 버튼이 눌려 있다</b> = 실제 상호작용이다.
            // 이 위젯에는 커서를 따라다니는 것이 없고(위치 고정, 드래그 없음) 남은 것은 클릭뿐이라
            // 이 범위로 충분하다. 눌린 동안 20Hz로 재호출되므로 0.5초 홀드가 끊기지 않는다.
            // 효과는 등급 유지보다 <b>즉시 재평가</b> 쪽이 크다 — 클릭 직후 다음 관측 폴링(최대
            // 0.2초)까지 절반 프레임레이트로 시작하던 구간이 사라진다.
            if (left) FramePacing.HoldActiveForInteraction();

            if (!_leftInitialized) { _leftInitialized = true; _leftPrev = left; return; }
            bool rising = left && !_leftPrev;
            _leftPrev = left;
            if (!rising) return;

            if (_agent == null || !_agent.TryGetCursorPosition(out Vector2 osScreen)) return;
            Vector2 cursor = ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _agentConfig);

            if (_hideButton != null && _hideButton.gameObject.activeInHierarchy &&
                ContainsScreenPoint(_hideButton.GetComponent<RectTransform>(), cursor))
            {
                if (TryClaimAction("hide")) OnHideClicked();
                return;
            }

            if (_moreButton != null && _moreLabel != null && _moreLabel.gameObject.activeSelf &&
                ContainsScreenPoint(_moreButton.GetComponent<RectTransform>(), cursor))
            {
                if (TryClaimAction("more")) OnMoreClicked();
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                RowWidgets row = _rows[i];
                if (row?.Root == null || !row.Root.activeSelf) continue;
                if (!ContainsScreenPoint(row.Root.GetComponent<RectTransform>(), cursor)) continue;
                if (TryClaimAction("row" + row.TodoId)) OnRowCheckboxClicked(row);
                return;
            }
        }

        /// <summary>
        /// ScreenSpaceOverlay 캔버스의 스케일을 현재 화면 배율에 맞춘다 — **캔버스 1유닛 == OS 포인트 1**.
        /// 근거는 ScreenCoordinateConverter.ResolveCanvasScaleFactor() 문서 참고(2026-08-29 Retina 대응,
        /// 리더 지시 5항). 이 위젯은 화면 우상단 앵커 + 고정 오프셋으로만 배치되므로 스크린 픽셀 <-> 캔버스
        /// 유닛 환산이 필요한 코드가 없다(아래 ContainsScreenPoint/SyncClickThroughBlocker는 GetWorldCorners를
        /// 쓰는데, 그 값에는 캔버스 루트의 localScale(=scaleFactor)이 이미 곱해져 있어 스크린 픽셀 그대로다).
        /// </summary>
        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_agentConfig);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
        }

        // ==================== 톱니 회피 (docs/UX_FLOW.md 51-9-3) ====================

        /// <summary>
        /// ★★ <b>카드가 톱니를 비켜 준다</b> — 2026-09-02, docs/UX_FLOW.md 51-9-3.
        ///
        /// ============================================================================
        /// 왜 z를 뒤집는 선택지가 <b>존재하지 않는가</b> (기술적 사실)
        /// ============================================================================
        /// 이 카드·부채꼴·캐릭터창은 <c>ScreenSpaceOverlay</c> 캔버스이고 <b>톱니만 월드
        /// <c>LineRenderer</c></b>다. Overlay 캔버스는 카메라가 그린 <b>모든</b> 월드 지오메트리
        /// 뒤가 아니라 <b>위</b>에 그려지며, 둘의 <c>sortingOrder</c>는 애초에 비교되지 않는다.
        /// 즉 톱니의 40을 30001로 올려도 <b>아무 일도 일어나지 않는다.</b>
        /// 화면 표면 4종 중 톱니 하나만 다른 세계에 살고, 그래서 <b>z 경쟁에서 구조적으로 항상 진다</b>.
        /// 비켜 세우는 것 말고 방법이 없고, 비키는 쪽은 <b>조건부 표면</b>(할 일이 있을 때만 뜨는 이 카드)이지
        /// <b>상시 진입점</b>(톱니)이 아니다.
        ///
        /// ============================================================================
        /// 왜 상수 −58이 아니라 파생식인가
        /// ============================================================================
        /// <b>톱니는 사용자가 드래그로 옮길 수 있다.</b> 상수로 박으면 톱니를 화면 왼쪽으로 옮긴
        /// 사용자는 아무 이유 없이 가로 42pt를 잃는다. 그래서 <b>톱니의 라이브 히트 사각형</b>에서
        /// 매 프레임 다시 푼다.
        /// <code>
        ///   겹치지 않으면 : 기준선                                (측면 띠가 없으면 16 — 평상시 그대로)
        ///   겹치면       : (화면폭 − 톱니히트.xMin) + 8           (8 = GearRadialMenuWidget.ScreenMarginPoints)
        /// </code>
        /// <para>★ 2026-09-03 — "16"이던 자리가 <b>기준선</b>(<see cref="ResolveRightInsetBoundsPoints"/>)으로
        /// 바뀌었다. OS가 화면 <b>오른쪽</b>에 예약한 띠를 카드가 덮지 않게 하려면 기준선 자체가
        /// 그 띠 바깥이어야 하고, <b>겹침 판정도 같은 기준선</b>을 봐야 한다.</para>
        ///
        /// <para><b>기본 톱니 위치에서의 검산</b>(톱니 히트 좌변이 오른쪽 끝에서 49.82pt일 때):
        /// 49.82 + 8 = <b>57.82</b> → 카드 우변 57.82 &gt; 톱니 히트 좌변 49.82, 여유 8.00pt.
        /// 카드 좌변 57.82 + 220 = 277.82pt(1512 화면에서 x = 1234.2). ✔</para>
        ///
        /// <para><b>왜 세로가 아니라 가로인가</b>: 세로로 밀면 필요 오프셋이 카드 높이(1행 28 ~ 8행 248pt)에
        /// 따라 계속 변하는데, 가로는 <b>폭이 220으로 고정</b>이라 한 번 풀면 끝난다. 게다가 세로로 밀면
        /// 카드가 구석에서 떨어져 나와 "구석 메모"라는 읽기 자체가 깨진다.</para>
        ///
        /// <para>★ <b>이 처방이 함께 없애는 것 — 이중 발동 구역 47.0pt²</b>(51-9-4). 지금 출하돼 있는
        /// 결함이다: <c>x 24.00~49.82 / y 38.18~40.00</c>(우상단 기준)에서 이 카드의 <b>[숨기기] 칩</b>
        /// (uGUI <c>Button</c>)과 <b>톱니 히트 사각형</b>(전역 폴링 + <c>BoxCollider2D</c>)이 겹쳐,
        /// 그 좁은 띠를 클릭하면 <b>카드가 숨겨지는 동시에 부채꼴이 펼쳐졌다</b>. 첫 실행에는 할 일이
        /// 0건이라 카드가 안 떠서 안 걸렸고, 할 일을 하나 넣는 순간 생겼다. 칩은 패널의 자식이므로
        /// 패널이 밀리면 칩도 함께 밀려 <b>겹침이 0이 된다</b>(패널 본체는 <c>raycastTarget = false</c>라
        /// 원래부터 클릭을 먹지 않는다).</para>
        ///
        /// <para><b>발진(oscillation)이 없는 이유</b>: 겹침 판정의 <b>가로</b> 입력은 언제나 <b>기본
        /// 인셋(16)으로 놓은 카드</b>다. 판정이 자기 출력에 의존하지 않으므로 "밀었다 → 안 겹치네 →
        /// 되돌림 → 겹치네" 진동이 구조적으로 불가능하다.</para>
        ///
        /// <para>★ <b>세로는 사정이 다르고, 그래서 반대로 한다</b>(2026-09-03). 세로 위치는 톱니가
        /// 아니라 <b>OS 예약 띠</b>가 정하므로(<see cref="ResolveTopInsetPoints"/>) 이 판정의 출력이
        /// 세로에 되먹임되지 않는다. 되먹임이 없으니 <b>실제 배치값을 그대로 쓰는 것이 안전하고,
        /// 안 쓰면 틀린다</b> — 세로만 기본값 16에 묶어 두면 배치가 띠 아래로 내려간 만큼 판정이
        /// 뒤처져 <b>발산 구간</b>이 생기고 그 안에서 카드가 톱니를 다시 덮는다(51-9-3 재발).</para>
        /// </summary>
        private void SyncPanelInsetToGear()
        {
            if (_panelRoot == null) return;

            float inset = ResolveRightInsetPoints();
            var want = new Vector2(-inset, -ResolveTopInsetPoints());
            if ((_panelRoot.anchoredPosition - want).sqrMagnitude < 0.0001f) return;
            _panelRoot.anchoredPosition = want;
        }

        /// <summary>
        /// ★★ <b>카드 상단이 화면 위 끝에서 얼마나 떨어지는가</b>(OS 포인트, 아래로 자란다) —
        /// 2026-09-03, docs/UX_FLOW.md 41-1 ③의 다섯 번째 소비처.
        ///
        /// ============================================================================
        /// 무엇이 문제였나 (persona-newcomer 신고 + 실측 일치)
        /// ============================================================================
        /// 세로 위치가 <see cref="PanelInsetPoints"/>(16)로 굳어 있어서 카드가 <b>macOS 메뉴 막대의
        /// 아래 절반을 덮었다</b>:
        /// <code>
        ///   메뉴 막대 0 ~ 33pt / 카드 상단 16pt  ->  겹침 17pt = 띠의 51.5%   (절대 불변 원칙 2)
        /// </code>
        /// 16은 <b>화면 여백</b>으로 고른 값이지 "메뉴바를 피한 값"이 아니었다 — 톱니가 2026-09-02에
        /// 옛 <c>MarginTopPoints = 58</c>을 버린 것과 <b>정확히 같은 병</b>이고, 같은 처방을 쓴다.
        ///
        /// ============================================================================
        /// 배선 — 사실 조회 / 판정이 갈라져 있다
        /// ============================================================================
        /// 사실 조회는 <see cref="ReservedTopBarProbe"/>(플랫폼별: macOS <c>visibleFrame</c> 뺄셈 /
        /// Windows <c>rcWork.Top − rcMonitor.Top</c>), 판정은 <see cref="SurfaceSafeAreaPolicy"/>
        /// (플랫폼 중립). <b>여기서 산수를 다시 하지 않는다</b> — "화면 맨 위(y=0)로 가고 싶다"고
        /// 요청하면 정책이 갈 수 있는 가장 위를 돌려주고, 이 함수는 중심을 상단 모서리로 되돌리기만 한다.
        /// 정책이 <c>Platform/MacOS/</c> 안에 있으면 Windows가 물리적으로 호출할 수 없다
        /// (CLAUDE.md의 <c>FullscreenSuspendPolicy.cs</c> 사고).
        ///
        /// <para><b>세 환경에서의 값</b>(여백 = <see cref="PanelInsetPoints"/> 16):
        /// <code>
        ///   macOS 메뉴 막대 33pt        -> 33 + 16 = 49    (겹침 17pt -> 0pt)
        ///   Windows 상단 도킹 40pt      -> 40 + 16 = 56
        ///   예약 띠 없음(자동 숨김/하단·좌·우 도킹) -> 0 + 16 = 16   ★ 한 픽셀도 바뀌지 않는다
        /// </code>
        /// 마지막 줄이 이 변경의 <b>회귀 없음 보증</b>이다 — 띠가 0인 환경에서는 옛 상수와 같은 값이
        /// 나오는 것이 아니라 <b>같은 식</b>이 된다.</para>
        ///
        /// <para>★ <b>이 함수의 결과는 톱니에 의존하지 않는다.</b> 그래서
        /// <see cref="ResolveRightInsetPoints"/>가 이것을 겹침 판정에 써도 발진이 생기지 않는다
        /// (판정 → 가로 인셋 → 이 값, 이라는 되먹임 경로가 없다).</para>
        ///
        /// <para><b>실패는 0이다</b> — 띠를 못 물으면 <see cref="ReservedTopBarProbe"/>가 0을 돌려주고
        /// 이 함수는 16을 돌려준다. 짐작값으로 메우지 않는다.</para>
        /// </summary>
        private float ResolveTopInsetPoints()
        {
            float reserved = ReservedTopBarProbe.TopInsetPoints(_agent != null ? _agent.PlatformService : null);
            float screenHeightPoints = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.height, _agentConfig);
            float panelHeight = _panelRoot != null ? _panelRoot.sizeDelta.y : 0f;

            // 화면 높이를 아직 못 읽는 병적인 순간에는 정책이 요청값(0)을 그대로 돌려주므로 직접 푼다
            // (InfoGearIconWidget.DefaultCenterPoints와 같은 관례).
            if (screenHeightPoints <= 0f) return reserved + PanelInsetPoints;

            // "화면 맨 위(y=0)로 가고 싶다" -> 정책이 갈 수 있는 가장 위의 <b>중심</b>을 돌려준다.
            float centerTopDown = SurfaceSafeAreaPolicy.ClampTopDownCenterY(
                0f, panelHeight, screenHeightPoints, reserved, PanelInsetPoints);
            return centerTopDown - panelHeight * 0.5f;   // 중심 -> 상단 모서리(카드 피벗이 상단이다).
        }

        /// <summary>위 문서의 파생식. 톱니를 못 찾거나 톱니가 안 보이면 <b>기준선</b>
        /// (<see cref="ResolveRightInsetBoundsPoints"/>) 그대로다 — 실패가 카드를 이상한 자리로 보내지 않는다.</summary>
        private float ResolveRightInsetPoints()
        {
            // ★★ 기준선과 왼쪽 한계를 <b>한 번에</b> 받는다. 아래 겹침 판정과 반환값이 같은 값을 쓰도록
            //   강제하는 것이 이 호출의 목적이다(두 벌이 되면 반드시 한쪽만 고쳐진다).
            ResolveRightInsetBoundsPoints(out float baseInset, out float maxInset);

            if (_gear == null || !_gear.isActiveAndEnabled) return baseInset;

            Rect hit = _gear.IconScreenRect;                    // Unity 스크린 픽셀(원점 좌하단).
            if (hit.width <= 0f || hit.height <= 0f) return baseInset;
            if (Screen.width <= 0 || Screen.height <= 0) return baseInset;

            // 화면 <b>우상단</b>이 원점인 pt 좌표로 옮긴다 — 카드의 앵커/피벗과 같은 계라 비교가 산수 없이 된다.
            float gearNear = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.width - hit.xMin, _agentConfig);
            float gearFar = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.width - hit.xMax, _agentConfig);
            float gearTop = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.height - hit.yMax, _agentConfig);
            float gearBottom = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.height - hit.yMin, _agentConfig);

            // 판정 입력은 언제나 <b>기준선에 놓인 카드</b>다 — 톱니에 밀린 <i>결과</i>가 아니다(발진 방지, 위 문서).
            //
            // ★★ 2026-09-03 — <b>세로는 기본값이 아니라 실제 배치값을 쓴다</b>. 여기가
            //   <see cref="PanelInsetPoints"/>로 굳어 있으면 배치(<see cref="SyncPanelInsetToGear"/>)가
            //   카드를 예약 띠 아래로 내린 만큼 판정이 <b>뒤처져</b> 발산 구간이 생기고, 그 구간에서
            //   카드가 톱니를 다시 덮는다(51-9-3 재발). 실측 발산 구간(메뉴 막대 33pt, 톱니 중심 y):
            //   <c>1행 115.82~148.82pt / 8행 311.82~344.82pt</c> — 폭이 정확히 띠 두께(33)와 같다.
            //   Windows 상단 도킹 40pt에서는 <c>115.82~155.82 / 311.82~351.82</c>로 더 넓다.
            //   그래서 <b>배치와 판정이 같은 한 곳</b>(<see cref="ResolveTopInsetPoints"/>)에서 값을 뽑는다.
            //   가로와 달리 되먹임이 없다 — 이 값은 톱니를 보지 않는다.
            //
            // ★★ 2026-09-03(같은 날, 가로축) — <b>가로도 같은 병에 걸린다</b>. 아래 두 줄이
            //   <see cref="PanelInsetPoints"/>로 굳어 있고 반환값만 기준선을 쓰면 발산 구간이 <b>둘</b>
            //   생기는데, 근변 구간은 아래 클램프 하한이 삼키고 <b>원변 구간</b>
            //   (톱니 우변이 기준선+카드폭 근처)만 남아 <b>카드가 톱니를 덮는다</b>
            //   (<see cref="ResolveRightInsetBoundsPoints"/> 문서의 계산표).
            //   그래서 <c>baseInset</c> 하나가 판정과 배치 양쪽에 들어간다.
            float panelHeight = _panelRoot.sizeDelta.y;
            float panelTop = ResolveTopInsetPoints();
            bool overlapX = gearFar < baseInset + PanelWidth && gearNear > baseInset;
            bool overlapY = gearTop < panelTop + panelHeight && gearBottom > panelTop;
            if (!overlapX || !overlapY) return baseInset;

            float required = gearNear + GearRadialMenuWidget.ScreenMarginPoints;

            // 밀다가 화면 왼쪽 밖으로 나가지는 않는다(톱니를 화면 한가운데로 옮긴 사용자).
            return Mathf.Clamp(required, baseInset, maxInset);
        }

        /// <summary>
        /// ★★ <b>카드의 가로 기준선</b>(톱니를 보지 않은, 평상시 자리)과 <b>왼쪽 한계</b>를 한 번에 낸다 —
        /// 2026-09-03, docs/UX_FLOW.md 41-1 ③ 가로축.
        ///
        /// ============================================================================
        /// 무엇이 문제였나
        /// ============================================================================
        /// 이 카드는 화면 <b>오른쪽</b>에 <see cref="PanelInsetPoints"/>(16)로 붙는데, Windows 작업표시줄을
        /// <b>우측에 도킹</b>하면 그 띠가 통상 48~62pt다. 16 &lt; 48이므로 카드가 그 띠를 <b>통째로</b> 덮었다.
        /// 상단 프로브로는 원리상 못 잡는다 — 우측 도킹이면 상단 차이가 0이라 "띠 없음"이 <b>참</b>이다.
        ///
        /// ============================================================================
        /// 배선 — 사실 조회 / 판정이 갈라져 있다 (세로축과 같은 관례)
        /// ============================================================================
        /// 사실 조회는 <see cref="ReservedEdgeProbe"/>(네 변을 한 번에), 판정은
        /// <see cref="SurfaceSafeAreaPolicy.ClampRightAnchoredInset"/>(플랫폼 중립). <b>여기서 산수를
        /// 다시 하지 않는다</b> — "여백 16으로 오른쪽에 붙고 싶다"고 요청하면 정책이 실제로 가져야 하는
        /// 인셋을 돌려준다.
        ///
        /// <para><b>★ 「측정된 0」과 「미측정 0」을 가른다.</b> 값은 같지만 사실은 정반대다
        /// (<see cref="ReservedEdgeInsets.MeasuredEdges"/>). 좌·우 어느 쪽도 측정 비트가 없으면
        /// <b>아무것도 바꾸지 않는다</b> — 화면 폭에서 카드 폭을 빼서 "아마 48pt쯤 되겠지"라고 메우는 것은
        /// <see cref="ReservedEdgeProbe"/>의 <i>"실패는 0이다 / 짐작값으로 메우지 않는다"</i> 규약을
        /// 정면으로 깬다. 추정값이 실제보다 크면 멀쩡한 화면을 낭비하고, 작으면 그대로 덮는다.</para>
        ///
        /// <para><b>세 환경에서의 값</b>(여백 = <see cref="PanelInsetPoints"/> 16):
        /// <code>
        ///   우측 도킹 작업표시줄 48pt -> 48 + 16 = 64
        ///   우측 도킹 작업표시줄 62pt -> 62 + 16 = 78
        ///   측면 띠 없음(측정된 0) / 못 잼(미측정) -> 16   ★ 한 픽셀도 바뀌지 않는다
        /// </code>
        /// 마지막 줄이 이 변경의 <b>회귀 없음 보증</b>이고, 두 경로 모두 <b>float32 비트 동일</b>하다:
        /// 미측정은 <c>PanelInsetPoints</c>를 <b>그대로</b> 돌려주고(정책을 아예 안 부른다),
        /// 측정된 0은 정책이 <c>W − ((W − 16 − 110) + 110)</c>을 계산하는데 화면 폭이 2^24보다 훨씬
        /// 작은 정수/반정수라 중간값이 전부 정확히 표현되어 <b>정확히 16</b>이 나온다.</para>
        ///
        /// ============================================================================
        /// ★ 가로축 발산 구간 — 세로축과 <b>서명(폭 == 띠 두께)은 같은데 개수가 둘이고, 하나는 흡수된다</b>
        /// ============================================================================
        /// 배치만 기준선으로 옮기고 <b>겹침 판정을 옛 상수 16에 남겨 두면</b> 판정이 배치보다 뒤처진
        /// 구간이 생긴다. 세로는 하나였는데 가로는 <b>둘</b>이고, <b>그중 하나는 최종 클램프가 삼킨다</b>.
        /// (우측 띠 R, 기준선 B = R + 16, 카드 폭 220, 톱니 여유 8. 좌표는 화면 오른쪽 끝 기준 pt.)
        /// <code>
        ///   ① 근변 구간  톱니 좌변 gearNear ∈ (16, B]        폭 = B − 16 = R
        ///        옛 판정 "겹친다" -> gearNear+8 로 밀려는데, 아래 <c>Mathf.Clamp(..., baseInset, ...)</c>의
        ///        <b>하한이 기준선</b>이라 R+8 이하는 통째로 흡수된다. 남는 차이는
        ///        <b>gearNear ∈ (B−8, B]</b>(폭 8 = 톱니 여유)뿐이고, 거기서도 결과는
        ///        <b>필요보다 최대 8pt 더 왼쪽</b>이다 — 화면 낭비이지 <b>침해가 아니다</b>.
        ///        ★ 즉 <b>①은 우측 띠를 절대 덮지 않는다</b>(결과가 언제나 기준선 ≥ R 이상이다).
        ///   ② 원변 구간  톱니 우변 gearFar ∈ [16+220, B+220)  폭 = B − 16 = R   ← 띠 두께와 같다
        ///        여기는 <b>흡수되지 않는다</b>. 옛 판정이 "안 겹친다"고 말해 카드가 기준선에 그대로 남고,
        ///        그 자리는 톱니 위다 -> <b>카드가 톱니를 덮는다</b>(51-9-3 재발). 이쪽이 진짜다.
        ///
        ///   검산(게임 뷰 640pt · 우측 띠 62 · 기준선 78 · 1행 카드):
        ///     ② gearFar = 267(구간 [236, 298)의 한가운데) -> gearNear = 306.64
        ///        옛 판정: 267 &lt; 236 이 거짓 -> 안 민다 -> 카드 [78, 298] 이 톱니 [267, 306.64] 를 덮는다.
        ///        지금 코드: 267 &lt; 298 이 참 -> 306.64 + 8 = 314.64 로 민다 -> 여유 정확히 8.00pt.
        ///     ① gearNear = 74 -> 옛 판정 82 / 지금 78. 차이 4pt는 낭비이고 침해가 아니다.
        /// </code>
        /// 그래서 이 함수의 <c>baseInset</c> <b>하나</b>가 판정과 배치 양쪽에 들어간다.
        /// (②를 <c>Tests/PlayMode/TodoPostItReservedTopBarTests</c>의
        /// <c>우측_띠_발산_구간에서_배치와_판정이_갈라지지_않는다</c>가 실측으로 잠근다.)
        /// </summary>
        /// <param name="baseInset">톱니를 보지 않은 가로 인셋(화면 오른쪽 끝 -> 카드 우변, OS 포인트).</param>
        /// <param name="maxInset">톱니를 피해 왼쪽으로 밀 수 있는 한계. 좌측 예약 띠를 <b>측정했으면</b>
        /// 그 앞에서 멈춘다(못 쟀으면 예전대로 화면 왼쪽 끝까지).</param>
        private void ResolveRightInsetBoundsPoints(out float baseInset, out float maxInset)
        {
            baseInset = PanelInsetPoints;
            float screenWidth = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.width, _agentConfig);
            maxInset = Mathf.Max(PanelInsetPoints, screenWidth - PanelWidth);
            if (screenWidth <= 0f) return;

            ReservedEdgeInsets edges = ReservedEdgeProbe.Insets(_agent != null ? _agent.PlatformService : null);

            // ★ 미측정이면 여기서 끝이다 — 요청값(옛 상수)이 그대로 나간다. 0으로 읽고 배치하는 것도,
            //   짐작으로 메우는 것도 하지 않는다. "측정된 0"은 아래로 내려가 정책을 지나지만 결과는 같다.
            if (!edges.IsMeasured(ReservedEdge.Left) && !edges.IsMeasured(ReservedEdge.Right)) return;

            float left = edges.PointsFor(ReservedEdge.Left);
            baseInset = SurfaceSafeAreaPolicy.ClampRightAnchoredInset(
                PanelInsetPoints, PanelWidth, screenWidth, left, edges.PointsFor(ReservedEdge.Right),
                PanelInsetPoints);
            maxInset = Mathf.Max(baseInset, screenWidth - left - PanelWidth);
        }

        /// <summary>지금 적용 중인 가로 인셋(OS 포인트) — 진단/테스트 창구. 읽기만 한다.</summary>
        public float RightInsetPointsForTests => _panelRoot != null ? -_panelRoot.anchoredPosition.x : 0f;

        /// <summary>지금 적용 중인 <b>세로</b> 인셋(OS 포인트, 화면 위 끝 -> 카드 상단) — 진단/테스트 창구.
        /// 읽기만 한다. 예약 띠가 0이면 <see cref="PanelInsetPoints"/>와 같다.</summary>
        public float TopInsetPointsForTests => _panelRoot != null ? -_panelRoot.anchoredPosition.y : 0f;

        /// <summary>ScreenSpaceOverlay 캔버스에서는 RectTransform의 월드 좌표가 곧 스크린 픽셀 좌표다
        /// (AppControlDirector.HitTestMenuRow와 같은 전제 — CanvasScaler.scaleFactor가 1이 아니어도
        /// 캔버스 루트의 localScale에 이미 반영돼 있어 그대로 성립한다).</summary>
        private static bool ContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null) return false;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return screenPoint.x >= corners[0].x && screenPoint.x <= corners[2].x &&
                   screenPoint.y >= corners[0].y && screenPoint.y <= corners[2].y;
        }

        /// <summary>같은 클릭이 uGUI 경로와 전역 폴링 경로로 두 번 들어와 체크가 즉시 원복되는 것을 막는다.</summary>
        private bool TryClaimAction(string key)
        {
            if (_lastActionKey == key && Time.unscaledTime - _lastActionTime < ActionDedupSeconds) return false;
            _lastActionKey = key;
            _lastActionTime = Time.unscaledTime;
            return true;
        }

        // ==================== 펼침 무입력 자동 접힘 ====================

        /// <summary>
        /// 펼쳐 둔 채 잊힌 카드를 스스로 접는다(<see cref="IdleAutoCollapseSeconds"/>).
        /// <b>접히는 것은 펼침뿐이다</b> — 카드 자체를 숨기지 않는다. 할 일이 있으면 늘 떠 있는 상시
        /// HUD라 시간으로 사라지면 그게 사고다(<see cref="EnterFullscreenHiding"/>와는 다른 판단).
        ///
        /// <para>★ <b>"무입력"의 정의가 팝오버와 다르다.</b> PopoverPanel은 "화면 어디서든 커서가
        /// 움직였는가"로 재는데, 그 정의를 여기 그대로 쓰면 <b>이 결함이 안 고쳐진다</b>: 사용자가
        /// 자기 일을 하며 마우스를 움직이는 내내 시계가 리셋되어 펼침이 하루 종일 남는다. 신고된 피해가
        /// 정확히 그것이다. 그래서 부채꼴의 자동 접힘과 같은 축 — <b>"이 표면에서 무슨 일이 있었는가"</b>
        /// 로 잰다(GearRadialMenuWidget.TickAutoCollapse는 "커서가 부채꼴 밖"으로 잰다).
        /// 시계를 되돌리는 것은 둘뿐이다: <b>카드 위에서 커서가 움직였다</b>, 또는 <b>카드를 눌렀다</b>.</para>
        ///
        /// <para>★ 키보드는 시계를 멈추지 <b>않는다</b>(PopoverPanel은 <c>Input.anyKey</c>로 멈춘다).
        /// 저쪽에는 할일 입력칸이 있어서 타이핑 중에 닫히면 사고지만, <b>이 카드에는 입력칸이 없다</b> —
        /// 여기서 키보드를 입력으로 치면 "남의 문서를 쓰는 동안 펼침이 안 접힌다"가 되어 위와 같은
        /// 실패로 돌아간다.</para>
        ///
        /// <para>카드가 안 보이는 동안(전체화면 감지 · [숨기기] · 빈 상태)에는 재지 않는다.
        /// 그때는 차단막이 이미 꺼져 있어 침해가 0이고, 시계는 다시 보일 때 이어서 돈다.</para>
        /// </summary>
        private void TickIdleAutoCollapse()
        {
            if (!_expanded) { NoteInteraction(); return; }
            if (_panelRoot == null || !_panelRoot.gameObject.activeSelf) return;

            _idlePollTimer += Time.unscaledDeltaTime;
            if (_idlePollTimer < IdlePollInterval) return;
            float elapsed = _idlePollTimer;
            _idlePollTimer = 0f;

            if (TryGetIdleCursor(out Vector2 cursor))
            {
                bool moved = !_hasCursorSample ||
                    (cursor - _lastCursorSample).sqrMagnitude > IdleCursorEpsilonPixels * IdleCursorEpsilonPixels;
                bool onCard = ContainsScreenPoint(_panelRoot, cursor);
                _hasCursorSample = true;
                _lastCursorSample = cursor;
                if (moved && onCard) { _idleSeconds = 0f; return; }
            }

            _idleSeconds += elapsed;
            if (_idleSeconds < IdleAutoCollapseSeconds) return;

            CollapseExpansion(CollapseReasonAuto,
                $"카드 위에서 {IdleAutoCollapseSeconds:F0}초 동안 아무 일도 없었습니다 — " +
                "펼쳐 둔 만큼의 클릭관통 해제 면적을 무기한 남기지 않습니다(원칙 2)");
        }

        /// <summary>무입력 판정이 볼 커서. 주입된 값이 있으면 그것을 쓴다
        /// (<see cref="PopoverPanel.FeedIdleCursorForTests"/>와 같은 관례).</summary>
        private bool TryGetIdleCursor(out Vector2 cursorUnityScreen)
        {
            if (_hasTestCursor) { cursorUnityScreen = _testCursor; return true; }
            if (_agent != null && _agent.TryGetCursorPosition(out Vector2 osScreen))
            {
                cursorUnityScreen = ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _agentConfig);
                return true;
            }
            cursorUnityScreen = default;
            return false;
        }

        /// <summary>사용자가 이 카드를 실제로 만졌다 — 무입력 시계를 0으로 되돌린다.</summary>
        private void NoteInteraction()
        {
            _idleSeconds = 0f;
            _idlePollTimer = 0f;
            _hasCursorSample = false;
        }

        /// <summary>
        /// 펼침을 되돌린다. <b>사유를 로그에 갈라 찍는다</b> — 자동 접힘이 "사용자가 닫았다"로 읽히면
        /// 다음 사람이 로그로 사용자 의사를 잘못 읽는다(부채꼴 <c>[부채꼴] 접힘(무반응 자동)</c>과 같은 어법).
        /// </summary>
        private void CollapseExpansion(string reasonKind, string detail)
        {
            if (!_expanded) return;
            _expanded = false;
            NoteInteraction();
            Debug.Log($"[투두] 펼침 접힘({reasonKind}) — {detail}.");
            RefreshView();
        }

        /// <summary>
        /// 카드가 보이는 동안만 카드의 화면 사각형을 덮는 히트테스트용 콜라이더를 켠다 —
        /// 이것이 없으면 UniWindowController의 Raycast 히트테스트가 카드 위를 계속 "관통"으로 판정해
        /// 클릭이 밑의 다른 앱으로 새고 이 앱은 클릭이 있었다는 사실조차 모른다(클래스 문서 (2)).
        /// isTrigger인 이유는 AppControlDirector의 메뉴 차단막과 동일하다: 히트테스트에는 잡히지만
        /// 물리 충돌은 절대 일으키지 않는다(캐릭터가 포스트잇에 부딪혀 튕기면 안 된다).
        /// </summary>
        private void SyncClickThroughBlocker()
        {
            if (_clickThroughBlocker == null || _panelRoot == null) return;

            bool visible = _panelRoot.gameObject.activeSelf;
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : Camera.main;
            if (!visible || cam == null)
            {
                _clickThroughBlocker.enabled = false;
                return;
            }

            var corners = new Vector3[4];
            _panelRoot.GetWorldCorners(corners); // Overlay 캔버스 -> 스크린 픽셀.
            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(corners[0].x, corners[0].y, depth));
            Vector3 tr = cam.ScreenToWorldPoint(new Vector3(corners[2].x, corners[2].y, depth));

            _clickThroughBlocker.enabled = true;
            _clickThroughBlocker.transform.position = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, 0f);
            _clickThroughBlocker.size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
        }

        /// <summary>전체화면 감지 — 카드와 차단막을 한 프레임에 거둔다(원칙 2).
        /// <see cref="_sessionHidden"/>(사용자가 누른 [숨기기])와 <b>다른 플래그</b>인 이유: 이것은
        /// 사용자의 의사가 아니라 강제 숨김이라, 게임이 끝나면 되돌아와야 한다.</summary>
        private void EnterFullscreenHiding()
        {
            _hiddenForFullscreen = true;
            if (_clickThroughBlocker != null) _clickThroughBlocker.enabled = false;
            if (_panelRoot != null) _panelRoot.gameObject.SetActive(false);
            Debug.Log("[투두] 전체화면 감지 — 포스트잇 카드와 클릭 차단막을 거둡니다(비침해 원칙 2).");
        }

        /// <summary>전체화면이 끝났다 — 상시 HUD이므로 원래 규칙(RefreshView)대로 되돌린다.
        /// 여기서 SetActive(true)를 직접 하지 않는 이유: 그 사이 할 일이 0건이 됐을 수도 있고,
        /// 표시 여부의 진실은 <see cref="RefreshView"/> 한 곳에만 있어야 한다.</summary>
        private void ExitFullscreenHiding()
        {
            _hiddenForFullscreen = false;
            RefreshView();
            Debug.Log("[투두] 전체화면 종료 — 포스트잇 표시 규칙을 다시 적용합니다.");
        }

        private void OnTodoListChanged() => RefreshView();

        /// <summary>설정창(7절, 아직 미구현)이 기능 자체를 끌 때 호출할 공개 진입점(17절 "기능 자체를
        /// 설정에서 완전히 끌 수 있는 토글 제공").</summary>
        public void SetFeatureEnabled(bool enabled)
        {
            _featureEnabled = enabled;
            RefreshView();
        }

        private void OnHideClicked()
        {
            NoteInteraction();
            _sessionHidden = true;
            RefreshView();
        }

        private void OnMoreClicked()
        {
            NoteInteraction();
            if (_expanded)
            {
                CollapseExpansion(CollapseReasonUser, "[+N개 더보기] 재클릭");
                return;
            }

            _expanded = true;
            Debug.Log($"[투두] 펼침({CollapseReasonUser}) — 최대 {ExpandedMaxRows}행까지 보여줍니다. " +
                $"카드 위에서 {IdleAutoCollapseSeconds:F0}초 동안 아무 일도 없으면 자동으로 접힙니다.");
            RefreshView();
        }

        private void OnRowCheckboxClicked(RowWidgets row)
        {
            NoteInteraction();
            Debug.Log($"[투두] 체크박스 클릭 — 항목 #{row.TodoId} \"{row.Label.text}\" 토글. " +
                "★ 이 로그가 찍힌다는 것은 포스트잇 uGUI/전역폴링 클릭 경로가 실제로 살아 있다는 증거다.");
            TodoListModel.ToggleComplete(row.TodoId);
            // TodoListModel.ToggleComplete가 TodoListChanged를 발행해 OnTodoListChanged -> RefreshView로
            // 이어지므로 여기서 직접 RefreshView를 부를 필요는 없다(단일 갱신 경로 유지).
        }

        private void RefreshView()
        {
            if (_panelRoot == null) return;

            var items = TodoListModel.ActiveItems;
            // _hiddenForFullscreen이 여기 들어가야 한다 — TodoListChanged는 전체화면 게임 중에도
            // 날아오고(다른 경로가 할 일을 정리할 수 있다), 그때 RefreshView가 카드를 되살리면
            // Update()의 가드가 있어도 한 프레임 동안 게임 위에 카드가 뜬다.
            bool visible = _featureEnabled && !_sessionHidden && !_hiddenForFullscreen
                && items.Count > 0; // 17절 빈 상태 예외
            _panelRoot.gameObject.SetActive(visible);
            if (!visible) return;

            int maxRows = _config != null ? Mathf.Max(1, _config.todoPostItMaxVisibleRows) : 4;
            // ★ 펼쳐도 <see cref="ExpandedMaxRows"/>를 넘지 않는다(위 P0 문단이 근거).
            //   Mathf.Max로 감싸는 이유: 설정이 접힘 행 수를 상한보다 크게 잡았다면 펼침이 접힘보다
            //   <b>적게</b> 보여주는 역전이 생긴다. 펼침은 절대 접힘보다 좁아지지 않는다.
            int rowCap = _expanded ? Mathf.Max(maxRows, ExpandedMaxRows) : maxRows;
            int visibleCount = Mathf.Min(rowCap, items.Count);

            EnsureRowCount(visibleCount);
            for (int i = 0; i < _rows.Count; i++)
            {
                RowWidgets row = _rows[i];
                if (i >= visibleCount)
                {
                    row.Root.SetActive(false);
                    continue;
                }

                TodoItem item = items[i];
                row.Root.SetActive(true);
                row.TodoId = item.Id;
                string box = item.Completed ? "☑" : "☐"; // 완료: ☑ / 미완료: ☐
                string wanted = box + " " + item.Text;

                // ★ 알파로 흐리지 않는다 — α<1은 이 오버레이에서 그 글자 위 창 알파를 그대로 끌어내려
                //   "완료 항목 자리만 데스크톱이 비치는" 결함이 된다(UiChrome '알파 채널의 법칙').
                //   위계는 <b>크기와 색 토큰</b>이 진다: 미완료 T4(12 Primary) / 완료 T7(10 Tertiary).
                row.Label.fontSize = item.Completed ? UiChrome.FontCaption : UiChrome.FontBody;
                row.Label.color = item.Completed ? UiChrome.TextTertiary : UiChrome.TextPrimary;

                bool changed = row.LastCompleted != item.Completed
                    || !string.Equals(row.LastLabelText, wanted, System.StringComparison.Ordinal);
                if (changed)
                {
                    row.Label.text = wanted;
                    row.LastLabelText = wanted;
                    row.LastCompleted = item.Completed;
                    ApplyStrikethrough(row, item.Completed);
                }
            }

            int hiddenCount = items.Count - visibleCount;
            bool showMore = !_expanded && hiddenCount > 0;
            _moreLabel.gameObject.SetActive(showMore);
            // 대괄호를 붙이지 않는다 — 이 앱의 다른 어떤 버튼도 라벨에 대괄호를 쓰지 않는다.
            if (showMore) _moreLabel.text = "+" + hiddenCount + "개 더보기";

            LayoutRows(visibleCount);
        }

        private void EnsureRowCount(int count)
        {
            while (_rows.Count < count) _rows.Add(CreateRow());
        }

        /// <summary>완료 항목에 취소선을 긋는다. 레거시 uGUI <c>Text</c>에는 <c>&lt;s&gt;</c>가 없어서
        /// (리치텍스트는 b/i/size/color뿐) 글자 폭을 재서 1pt 선을 직접 놓는다. 폭 측정은
        /// 내용이 바뀐 순간에만 한다 — 상주 카드라 매 갱신 재측정은 낭비다.</summary>
        private static void ApplyStrikethrough(RowWidgets row, bool completed)
        {
            if (row.Strike == null) return;
            row.Strike.gameObject.SetActive(completed);
            if (!completed) return;

            float width = Mathf.Min(row.Label.preferredWidth, row.Label.rectTransform.rect.width);
            var rt = row.Strike.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(0f, width), 1f);
        }

        /// <summary>행의 왼쪽 시작 x — 안쪽 여백 + 노란 띠 폭. 글자가 띠 위로 올라타지 않게 한다.</summary>
        private const float RowX = PanelPadding + AccentStripeWidth;

        private void LayoutRows(int visibleCount)
        {
            float y = -PanelPadding;
            for (int i = 0; i < visibleCount; i++)
            {
                var rt = _rows[i].Root.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(RowX, y);
                y -= RowHeight;
            }
            if (_moreLabel.gameObject.activeSelf)
            {
                var rt = _moreButton.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(RowX, y);
                y -= RowHeight;
            }

            float panelHeight = Mathf.Abs(y) + PanelPadding + RowHeight; // + 헤더(숨기기 버튼) 한 줄
            _panelRoot.sizeDelta = new Vector2(PanelWidth, panelHeight);
        }

        // ==================== UI 구성(런타임 생성 — 씬/프리팹 수동 배선 없이도 동작) ====================

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("TodoPostItCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrderTopMost;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();

            // ★ P0-2 — 다른 창과 <b>같은 구조</b>[본체(α1) → 보더]. 컨테이너에는 Graphic을 붙이지
            //   않는다(UiChrome.AddOpaquePanel 문서). ★ 2026-09-02: 그림자 겹은 전부 삭제됐다.
            _panelRoot = UiChrome.AddOpaquePanel(canvasGo.transform, "PostItPanel", UiChrome.RadiusPanel,
                out _);
            GameObject panelGo = _panelRoot.gameObject;
            _panelRoot.anchorMin = new Vector2(1f, 1f);
            _panelRoot.anchorMax = new Vector2(1f, 1f);
            _panelRoot.pivot = new Vector2(1f, 1f);
            // 화면 우상단 기본 위치(17절 "유저가 위치 지정 가능"은 후속 과제).
            // ★ 가로 인셋은 매 프레임 SyncPanelInsetToGear()가 톱니 실측에서, 세로 인셋은
            //   ResolveTopInsetPoints()가 예약 띠 실측에서 다시 푼다. 여기서 좌표를 직접 쓰지 않고
            //   <b>같은 함수를 한 번 부르는</b> 이유: 두 벌이 되면 반드시 한쪽만 고쳐진다.
            //   (Awake 시점이라 플랫폼 서비스도 톱니도 아직 없어 결과는 (16, 16) — 옛 값 그대로다.)
            _panelRoot.sizeDelta = new Vector2(PanelWidth, RowHeight);
            SyncPanelInsetToGear();

            // "이건 메모다"를 남기는 왼쪽 노란 띠. 위아래를 모서리 반지름만큼 들여 놓아야 둥근 모서리
            // 바깥으로 삐져나오지 않는다(직선 구간에만 놓는다).
            Image stripe = UiChrome.AddSurface(_panelRoot, "AccentStripe", PostItStripe, UiChrome.RadiusDot);
            var srt = stripe.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.offsetMin = new Vector2(0f, UiChrome.RadiusPanel);
            srt.offsetMax = new Vector2(AccentStripeWidth, -UiChrome.RadiusPanel);
            stripe.raycastTarget = false;

            _hideButton = CreateSmallButton(panelGo.transform, "HideButton", "숨기기", new Vector2(1f, 1f),
                new Vector2(-UiChrome.Space2, -UiChrome.Space1), chip: true);
            _hideButton.onClick.AddListener(() => { if (TryClaimAction("hide")) OnHideClicked(); });

            var rowContainerGo = new GameObject("Rows", typeof(RectTransform));
            rowContainerGo.transform.SetParent(panelGo.transform, false);
            _rowContainer = rowContainerGo.GetComponent<RectTransform>();
            _rowContainer.anchorMin = Vector2.zero;
            _rowContainer.anchorMax = Vector2.one;
            _rowContainer.offsetMin = Vector2.zero;
            _rowContainer.offsetMax = new Vector2(0f, -RowHeight); // 상단 [숨기기] 줄 아래부터 시작

            _moreButton = CreateSmallButton(rowContainerGo.transform, "MoreButton", "+N개 더보기", new Vector2(0f, 1f), Vector2.zero);
            _moreButton.onClick.AddListener(() => { if (TryClaimAction("more")) OnMoreClicked(); });
            _moreLabel = _moreButton.GetComponentInChildren<Text>();
            _moreLabel.gameObject.SetActive(false);
            _moreButton.gameObject.SetActive(true);

            panelGo.SetActive(false); // 초기에는 빈 상태 — RefreshView가 실제 표시 여부를 결정.

            // 클릭관통 차단막(클래스 문서 (2)). 씬 루트에 둔다 — 캐릭터의 자식으로 두면 캐릭터가
            // 걷거나 랙돌로 회전할 때 이 사각형까지 함께 돌아가 카드의 화면 사각형과 어긋난다.
            var blockerGo = new GameObject("TodoPostItClickBlocker");
            _clickThroughBlocker = blockerGo.AddComponent<BoxCollider2D>();
            _clickThroughBlocker.isTrigger = true;
            _clickThroughBlocker.enabled = false;
        }

        private RowWidgets CreateRow()
        {
            var rowGo = new GameObject("TodoRow", typeof(RectTransform));
            rowGo.transform.SetParent(_rowContainer, false);
            var rt = rowGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            // 좌측 노란 띠(4pt) 위에 글자가 올라타지 않도록 행 폭에서 띠 몫을 뺀다.
            rt.sizeDelta = new Vector2(PanelWidth - RowX - PanelPadding, RowHeight);

            var buttonGo = new GameObject("Checkbox", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(rowGo.transform, false);
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = Vector2.zero;
            buttonRt.anchorMax = Vector2.one;
            buttonRt.offsetMin = Vector2.zero;
            buttonRt.offsetMax = Vector2.zero;
            buttonGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // 거의 투명 — 히트테스트 대상용 배경

            Text label = UiChrome.AddText(buttonGo.transform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            UiChrome.Stretch(label.rectTransform);
            label.verticalOverflow = VerticalWrapMode.Truncate;

            // 취소선 — 글자와 같은 줄 가운데. 완료 항목에서만 켜진다(ApplyStrikethrough).
            Image strike = UiChrome.AddSurface(buttonGo.transform, "Strike", UiChrome.TextTertiary, UiChrome.RadiusDot);
            var strt = strike.rectTransform;
            strt.anchorMin = strt.anchorMax = new Vector2(0f, 0.5f);
            strt.pivot = new Vector2(0f, 0.5f);
            strt.anchoredPosition = Vector2.zero;
            strt.sizeDelta = new Vector2(0f, 1f);
            strike.raycastTarget = false;
            strike.gameObject.SetActive(false);

            var row = new RowWidgets
            {
                Root = rowGo,
                CheckboxButton = buttonGo.GetComponent<Button>(),
                Label = label,
                Strike = strike,
            };
            row.CheckboxButton.onClick.AddListener(() =>
            {
                if (TryClaimAction("row" + row.TodoId)) OnRowCheckboxClicked(row);
            });
            return row;
        }

        /// <summary>작은 버튼 한 개. <paramref name="chip"/>이면 <see cref="UiChrome.RadiusChip"/> 칩
        /// (표면 CardSurface + 테두리 CardBorder)으로, 아니면 <b>글자만</b>인 링크형으로 만든다.
        /// 색·폰트·크기는 전부 <see cref="UiChrome"/> 토큰이다 — 이 파일에 생 리터럴을 남기지 않는다.</summary>
        private Button CreateSmallButton(Transform parent, string name, string text, Vector2 anchor,
            Vector2 anchoredPos, bool chip = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;

            var surface = go.GetComponent<Image>();
            if (chip)
            {
                rt.sizeDelta = new Vector2(ChipWidth, ChipHeight);
                surface.sprite = UiChrome.RoundedFill(UiChrome.RadiusChip);
                surface.type = Image.Type.Sliced;
                surface.color = UiChrome.CardSurface;
                UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
            }
            else
            {
                rt.sizeDelta = new Vector2(PanelWidth - RowX - PanelPadding, RowHeight - UiChrome.Space1);
                surface.color = new Color(0f, 0f, 0f, 0.001f);   // 거의 투명 — 히트테스트 대상용 배경
            }

            Text label = UiChrome.AddText(go.transform, "Label", UiChrome.FontCaption,
                chip ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft,
                chip ? UiChrome.TextTertiary : UiChrome.Accent);   // 누를 수 있는 글자는 강조색으로 표시한다.
            UiChrome.Stretch(label.rectTransform);
            label.text = text;

            return go.GetComponent<Button>();
        }

        /// <summary>uGUI 클릭 판정에는 씬에 EventSystem이 있어야 한다 — 아직 씬 조립이 없는 개발 단계라
        /// 이 위젯이 직접 보장한다(이미 하나 있으면 만들지 않음). 이 EventSystem은 캐릭터 클릭 감지
        /// (StickmanClickHitbox, OnMouseDown 기반 물리 레이캐스트)와 아무 관련이 없다 — uGUI 전용이다.</summary>
        private static void EnsureEventSystem()
        {
            // EventSystem.current는 그 컴포넌트의 OnEnable이 돌아야 채워진다 — 이 위젯의 Awake()가
            // 씬의 EventSystem보다 먼저 돌면 null로 보여서 **두 번째 EventSystem을 만들어버린다**
            // (Unity가 "There are 2 event systems in the scene" 경고를 내는 상태). 실행 순서에
            // 의존하지 않도록 씬에서 직접 찾는다.
            EventSystem existing = EventSystem.current != null
                ? EventSystem.current
                : Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                // ★ 여기서 그냥 return하던 것이 체크박스가 한 번도 눌리지 않았던 원인 절반이다
                // (클래스 문서 (1)): 씬에는 EventSystem이 이미 있었지만 **입력 모듈이 없어서**
                // 포인터 이벤트가 아예 처리되지 않았고, 이 메서드는 "EventSystem이 있으니 됐다"고
                // 판단해 조용히 넘어갔다. 모듈이 없으면 그 자리에 보강한다.
                if (existing.GetComponent<BaseInputModule>() == null)
                {
                    existing.gameObject.AddComponent<StandaloneInputModule>();
                    Debug.Log("[투두] 씬의 EventSystem에 입력 모듈이 없어 StandaloneInputModule을 보강했습니다 — " +
                        "이것이 없으면 Button.onClick이 영원히 발동하지 않습니다.");
                }
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
        }
    }
}
