using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ <b>StickMate 설정창</b> 720×560 — docs/UX_FLOW.md 35-1(탭 구조/와이어프레임/역할 규정),
    /// 2026-09-01 사용자 승인 시안 그대로.
    ///
    /// ============================================================================
    /// 이 창이 존재하는 이유 (35-1-7 "목표")
    /// ============================================================================
    /// 이 앱이 삭제되는 유일한 진짜 이유는 <b>"방해받았다"</b>이다(UX_FLOW 8절 P1). 그 감정이 났을 때
    /// 사용자가 찾는 것은 "설정 어딘가"가 아니라 <b>"이 짓 그만하게 하는 스위치"</b> 하나이고, 그것이
    /// 도달 가능한 곳에 없으면 남는 선택지는 삭제뿐이다. 그래서 이 창의 첫 탭은 꾸미기가 아니라
    /// <b>[일반] — 이 앱이 화면에 있는 방식</b>이다.
    ///
    /// ============================================================================
    /// 탭 5개 (35-1-4) — 이번 라운드는 [일반][캐릭터]만 채운다
    /// ============================================================================
    /// 일반 / 캐릭터 / 이벤트 / 접근성·성능 / 데이터. 나머지 셋은 <b>비워 두되 숨기지 않는다</b> —
    /// 35-1-7의 "미구현은 회색 + 사유. 없는 척하는 것보다 낫다(로드맵이 곧 기대치 관리다)".
    ///
    /// ============================================================================
    /// 배타 규칙 / 탈출구 (35-1-7)
    /// ============================================================================
    ///  · 열면 정보창·부채꼴·팝오버를 <b>여는 쪽에서 한 번에</b> 거둔다(CharacterInfoWindow가
    ///    확립한 관례 — 진입점마다 정리 코드를 흩뿌리면 네 번째 진입점에서 반드시 샌다).
    ///  · 탈출구: [✕] / 창 밖 클릭 / 진입점 재선택. <b>ESC는 쓰지 않는다</b> — 이미 클릭관통 긴급
    ///    해제에 묶여 있다.
    ///  · 전체화면 감지 시 창·차단막 즉시 정리, 복귀해도 자동으로 다시 열지 않는다(원칙 2).
    ///
    /// ============================================================================
    /// ★ 값은 <b>배포 에셋에 쓰지 않는다</b>
    /// ============================================================================
    /// 이 창이 만지는 모든 값은 <see cref="AppSettingsModel"/> / <see cref="UiLayoutModel"/> /
    /// <see cref="CharacterAppearanceModel"/> / <see cref="CharacterScaleController"/>를 지난다.
    /// <c>StickConfig</c>의 직렬화 필드에 직접 쓰면 그 순간 출하 기본값이 오염된다(2026-08-31에 두 번
    /// 겪은 실패 모드 — <see cref="AppSettingsModel"/> 클래스 문서 참고).
    /// </summary>
    public sealed class SettingsWindow : MonoBehaviour, IExclusiveSurface
    {
        // ==================== 치수 (35-1-5 와이어프레임) ====================

        public const float PanelWidth = 720f;
        public const float PanelHeight = 560f;
        public const float HeaderHeight = 48f;
        public const float TabBarHeight = 40f;
        public const float FooterHeight = 34f;
        public const float ContentHeight = PanelHeight - HeaderHeight - TabBarHeight - FooterHeight; // 438
        public const float ContentPadX = 20f;
        public const float ContentPadTop = 16f;

        /// <summary>정보창(31900) 바로 위, 앱 제어 메뉴(32760) 아래. 설정창과 정보창은 상호 배타라
        /// 실제로 겹치지 않지만, 한 프레임의 전환 구간에서 설정창이 뒤로 숨지 않게 한다.</summary>
        private const int SortingOrderTopMost = 31950;

        private const float ClickPollInterval = 0.05f;
        /// <summary>같은 컨트롤의 연타를 한 번으로 접는 시간(초). ★ <b>public</b>인 이유: 같은 토글을
        /// 껐다 켜는 것을 검증하는 PlayMode 테스트가 이 창의 실제 대기 시간을 <b>숫자로 베끼지 않고</b>
        /// 참조해야 한다(CLAUDE.md — 프로덕션 상수 하드코딩 금지). 이 파일의 다른 테스트 관측점
        /// (<c>FeedClickForTests</c>/<c>*ScreenRect</c>)과 같은 사정이다 — PlayMode 어셈블리는
        /// <c>InternalsVisibleTo</c> 대상이 아니다.</summary>
        public const float ActionDedupSeconds = 0.35f;

        /// <summary>[지금 종료]의 2단 확인 시간 — <see cref="ActionCommandPopover"/>와 같은 값, 같은 이유
        /// (한 번의 오조준으로 앱이 꺼지면 안 된다).</summary>
        private const float QuitConfirmSeconds = 3f;

        /// <summary>[▲][▼] 한 번에 넘기는 양. 화면 높이에서 한 행쯤 겹쳐 남겨 맥락이 끊기지 않게 한다.</summary>
        private const float PageStep = ContentHeight - SettingsControls.RowHeight;

        // ==================== 탭 ====================

        public enum Tab { General = 0, Character = 1, Event = 2, Accessibility = 3, Data = 4 }

        private const int TabCount = 5;

        private static readonly string[] TabNames = { "일반", "캐릭터", "이벤트", "접근성 · 성능", "데이터" };

        // ==================== 탭바 배지 (docs/UI_SURFACE_SPEC.md 12) ====================

        /// <summary>탭 라벨 한 글자의 폭 근사(pt). 한글은 폭이 pt에 가깝다는 <see cref="SettingsControls"/>의
        /// 세그먼트 주석과 같은 근거다.</summary>
        private const float TabLabelCharWidth = 11f;

        /// <summary>탭 안쪽 좌우 여백. 배지가 붙어도 <b>양쪽 10pt 대칭</b>이 유지된다.</summary>
        private const float TabPadX = 10f;

        /// <summary>라벨 상자 높이(본문 12pt의 행 상자).</summary>
        private const float TabLabelHeight = 16f;

        /// <summary>
        /// 미구현 탭 라벨 오른쪽에 붙는 <b>보조 어절</b> — "기능이 없는 건가, 내가 못 찾은 건가"에
        /// <b>누르기 전에</b> 답하는 유일한 자리다(docs/UX_FLOW.md 43-3).
        ///
        /// <para>왜 기호가 아니라 글자인가: 이 앱의 자물쇠는 이미 <b>"놀면 열린다"</b>(장비 카드의
        /// <c>Lv.n에 열림</c>)라 미구현 탭에 붙이면 거짓 약속이 되고, 탭바의 도트는 관례상 <b>"새 것"</b>
        /// 이라 뜻이 뒤집히며, 밑줄은 이 앱에서 <b>"지금 여기"</b>다. 남는 것은 글자이고, 글자는
        /// 해독이 필요 없고 스크린리더가 읽는다.</para>
        ///
        /// <para>★ 캡션 접두사와 <b>같은 단어</b>다(<see cref="SettingsControls.NotBuiltWord"/>) —
        /// 탭에서 읽은 어휘가 행 캡션으로 그대로 이어진다. <b>public</b>인 이유: 회귀 테스트가 이
        /// 문자열을 <b>베끼지 않고</b> 참조해야 한다(CLAUDE.md).</para>
        /// </summary>
        public const string TabBadgeText = SettingsControls.NotBuiltWord;

        /// <summary>라벨과 배지 사이. <see cref="UiChrome.Space1"/>(4)은 글자 크기가 다른 두 덩어리를
        /// 붙여 놓기에 좁다 — 한 어구로 읽히되 두 덩어리인 것은 보여야 한다.</summary>
        private const float TabBadgeGap = UiChrome.Space2;

        /// <summary>배지 상자 폭. ★ 숫자를 손으로 적지 않는다 — 글자 수 × 캡션 폰트에서 파생시킨다
        /// (10pt 실측 모델로 33pt → 여유 7pt).</summary>
        private static readonly float TabBadgeWidth = TabBadgeText.Length * UiChrome.FontCaption;

        /// <summary>배지 상자 높이(캡션 10pt의 행 상자 — <c>SettingsControls.BeginRow</c>와 같은 값).</summary>
        private const float TabBadgeHeight = 14f;

        /// <summary>탭마다 "이 탭을 여는 순간"을 한 줄로 — 35-1-4의 정의를 그대로 화면에 쓴다.</summary>
        private static readonly string[] TabEyebrows =
        {
            "이 앱이 화면에 있는 방식",
            "이 캐릭터가 보이고 말하는 방식",
            "이 캐릭터가 알아서 하는 일의 범위",
            "내 눈과 내 컴퓨터에 맞추기",
            "내 것을 확인하고 지우기",
        };

        // ==================== 배선 ====================

        [SerializeField] private StickConfig _config;

        private StickmanAgent _agent;
        private IGlobalPointerButtonService _buttonService;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _panel;
        private BoxCollider2D _clickBlocker;
        private RectMask2D[] _masks = System.Array.Empty<RectMask2D>();

        private RectTransform _closeRect;
        private readonly RectTransform[] _tabRects = new RectTransform[TabCount];
        private readonly Text[] _tabLabels = new Text[TabCount];
        private readonly Image[] _tabUnderlines = new Image[TabCount];
        private readonly RectTransform[] _pages = new RectTransform[TabCount];
        private readonly float[] _pageHeights = new float[TabCount];
        private readonly float[] _pageScroll = new float[TabCount];
        private RectTransform _viewport;
        private RectTransform _pageUpRect;
        private RectTransform _pageDownRect;

        private readonly SettingsControlHost _host = new SettingsControlHost();

        // 값이 바뀌면 화면을 다시 칠해야 하는 부품들(다른 UI가 같은 값을 바꿀 수 있다).
        private SettingsSlider _scaleSlider;
        private SettingsSwatchRow _inkSwatches;
        private SettingsToggle _gearIconToggle;
        private SettingsToggle _autoHideToggle;
        private SettingsToggle _bubbleToggle;
        private SettingsSlider _fontSizeSlider;
        private SettingsSegment _visibleLengthSegment;
        private SettingsSlider _chatterSlider;

        /// <summary>말풍선을 끄면 함께 무효가 되는 세 행(42-11 판정 G).</summary>
        private SettingsRowGate _speechGate;
        private Text _gearWarnCaption;
        private Image _quitSurface;
        private Text _quitLabel;
        private Text _footerLeft;

        /// <summary>종료 버튼의 평상시 문구. 만드는 곳(<c>AddButtons</c>)과 되돌리는 곳
        /// (<c>ApplyQuitStyle</c>) 두 군데가 <b>같은 문자열</b>을 각자 적고 있었다 — 한쪽만 고치면
        /// "정말 종료?"에서 되돌아올 때 문구가 달라진다. 단축키 표기가 플랫폼별로 갈리면서
        /// 그 위험이 실제가 되므로 한 곳으로 합친다.</summary>
        private static string QuitLabelText => $"지금 종료 ({ShortcutLabel.Chord("Q")})";

        private bool _open;
        private Tab _tab = Tab.General;
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private int _dragIndex = -1;

        /// <summary>커서가 이 창 위에 있었거나 조작이 있었던 마지막 시각(프레임 페이싱 홀드용).
        /// <see cref="TickFramePacingHold"/> 참고.</summary>
        private float _lastSurfaceTouchTime = float.NegativeInfinity;

        private string _lastActionKey;
        private float _lastActionTime;
        private bool _quitArmed;
        private float _quitArmedAt;
        private bool _saveRequested;

        // 부채꼴 참조는 더 이상 이 창이 들고 있지 않다 — 배타 규칙의 집행이 ExclusiveSurfaces로
        // 옮겨가면서 여기서 부채꼴을 직접 아는 이유가 사라졌다(닫을 대상을 손으로 적지 않는다).
        private CharacterInfoWindow _infoWindow;

        /// <summary>설정창을 열 때 <b>내가 닫은</b> 정보창이 있었는가 — 닫을 때 그 자리로 돌려보내기 위해.
        /// 자세한 이유는 <see cref="RestoreInfoWindowIfNeeded"/>.</summary>
        private bool _restoreInfoWindowOnClose;

        private static readonly Vector3[] _corners = new Vector3[4];

        // ==================== 진단/테스트용 공개 상태 ====================

        public bool IsOpen => _open;

        // ★ 배타 표면 등록(2026-09-01) — 정보창과 같은 배선. 이 한 줄이 없어서 <c>I</c>를 눌러도
        //   설정창이 정보창 위에 남아 있었다(사용자 신고 "케릭터창도 겹쳐서보이는 문제있고").
        bool IExclusiveSurface.IsSurfaceOpen => _open;
        void IExclusiveSurface.CloseSurface(string reason) => Close(reason);
        public bool IsCanvasActive => _canvas != null && _canvas.gameObject.activeSelf;
        public bool IsClickBlockerEnabled => _clickBlocker != null && _clickBlocker.enabled;
        public Tab ActiveTab => _tab;
        public Vector2 PanelSizePoints => _panel != null ? _panel.sizeDelta : Vector2.zero;

        /// <summary>설정창이 지금 보여주고 있는 캐릭터 배율 — 테스트가 "두 UI가 같은 값을 가리키는가"를
        /// 확인하는 창구다(원칙 1).</summary>
        public float DisplayedCharacterScale => _scaleSlider != null ? _scaleSlider.Value : 0f;

        public Rect CharacterScaleTrackScreenRect => _scaleSlider != null
            ? SettingsControlHost.ScreenRectOf(_scaleSlider.TrackHitRect)
            : new Rect();

        public Rect TabScreenRect(Tab tab) => SettingsControlHost.ScreenRectOf(_tabRects[(int)tab]);

        /// <summary>탭 <b>버튼</b>의 지금 글자색 — "준비 중인 탭은 누르기 전에도 흐리다"(M7)를
        /// 회귀 테스트가 색 상수를 다시 적지 않고 확인하는 창구다.</summary>
        public Color TabLabelColor(Tab tab)
        {
            Text label = _tabLabels[(int)tab];
            return label != null ? label.color : Color.clear;
        }

        /// <summary>이 탭에 내용이 있는가(진단/테스트용 공개 — 판정은 <see cref="IsTabReady"/> 하나뿐이다).</summary>
        public static bool IsTabImplemented(Tab tab) => IsTabReady(tab);

        public Rect CloseButtonScreenRect => SettingsControlHost.ScreenRectOf(_closeRect);

        /// <summary>창 전체의 화면 사각형 — "창 밖"이 화면 안에 실제로 존재하는지 테스트가 확인하는 창구다
        /// (배치모드의 좁은 화면에서는 720×560 패널이 화면을 넘어 <b>바깥이 없을 수</b> 있다).</summary>
        public Rect PanelScreenRect => SettingsControlHost.ScreenRectOf(_panel);

        /// <summary>[▲]/[▼] 페이지 칩과 내용 영역의 화면 사각형 — "칩이 내용 위에 앉지 않는다"(소은 #7-b)를
        /// 회귀 테스트가 좌표로 확인하는 창구다. 칩이 꺼져 있어도 사각형은 나온다(그 자리를 재는 것이 목적).</summary>
        public Rect PageUpScreenRect => SettingsControlHost.ScreenRectOf(_pageUpRect);

        public Rect PageDownScreenRect => SettingsControlHost.ScreenRectOf(_pageDownRect);

        public Rect ContentViewportScreenRect => SettingsControlHost.ScreenRectOf(_viewport);

        /// <summary>잉크 스와치의 화면 사각형(0=검정, 1=흰색). 테스트가 "배포 에셋을 건드리지 않는가"를
        /// <b>실제 클릭 경로로</b> 확인하기 위해 필요하다 — 좌표를 손으로 적으면 레이아웃이 바뀔 때
        /// 조용히 엉뚱한 곳을 누른다.</summary>
        public Rect InkSwatchScreenRect(int index)
            => _inkSwatches != null && _inkSwatches.Rects != null && index >= 0 && index < _inkSwatches.Rects.Length
                ? SettingsControlHost.ScreenRectOf(_inkSwatches.Rects[index])
                : new Rect();

        /// <summary>말풍선 글자 크기 슬라이더의 [+] 버튼 사각형(테스트 전용).</summary>
        public Rect DialogueFontSizePlusScreenRect => _fontSizeSlider != null
            ? SettingsControlHost.ScreenRectOf(_fontSizeSlider.PlusRect)
            : new Rect();

        /// <summary>`대사 표시 시간` 세그먼트 i번 칸의 사각형(테스트 전용).</summary>
        public Rect DialogueVisibleLengthSegmentScreenRect(int index)
            => _visibleLengthSegment != null && _visibleLengthSegment.Rects != null
               && index >= 0 && index < _visibleLengthSegment.Rects.Length
                ? SettingsControlHost.ScreenRectOf(_visibleLengthSegment.Rects[index])
                : new Rect();

        /// <summary>`말풍선 표시` 토글의 사각형(테스트 전용).</summary>
        public Rect DialogueBubbleToggleScreenRect => _bubbleToggle != null
            ? SettingsControlHost.ScreenRectOf(_bubbleToggle.HitRect)
            : new Rect();

        /// <summary>말풍선을 끄면 함께 무효가 되는 세 행이 지금 활성인가(테스트 전용).</summary>
        public bool SpeechRowsEnabledForTests => _speechGate == null || _speechGate.Enabled;

        /// <summary>테스트 전용 — 실제 입력과 <b>완전히 같은</b> 처리 경로에 커서를 먹인다
        /// (PlayMode는 진짜 전역 클릭을 만들 수 없다. InfoGearIconWidget.FeedPointerForTests와 같은 사정).</summary>
        public void FeedClickForTests(Vector2 cursorUnityScreen) => FeedClick(cursorUnityScreen);

        // ==================== 수명 주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            if (_config == null && _agent != null) _config = _agent.Config;
            BuildUi();
        }

        private void Start()
        {
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;
            CharacterScaleController.Bind(_agent);
            RefreshAll();

            Debug.Log($"[설정창] 준비 완료({PanelWidth:F0}×{PanelHeight:F0}, 탭 5개: " +
                $"{string.Join("/", TabNames)}) — 여는 방법: (1) 정보창 헤더의 작은 톱니, " +
                $"(2) 전역 단축키 **{ShortcutLabel.Chord("P")}**. 이번 라운드는 [일반][캐릭터]만 내용이 있습니다. " +
                $"전역 폴링 경로={(_buttonService != null ? "사용 가능" : "미지원 — uGUI 경로만")}.");
            LogRoadmapNotes();
        }

        /// <summary>
        /// ★ 2026-09-01 — 미구현 행의 <b>내부 사정</b>은 여기(로그)에만 적는다.
        ///
        /// <para>화면의 사유 캡션이 "GlobalKey에 V가 없어 다음 라운드에 배선합니다" / "35-1-9 P3"처럼
        /// <b>개발자끼리 쓰는 말</b>을 그대로 렌더하고 있었다(페르소나 M6). 사용자는 GlobalKey도
        /// 35-1-9도 모르고, 읽히는 것은 "이 앱 미완성이구나" 하나다. 그렇다고 그 정보를 지우면 팀이
        /// 로드맵을 잃으므로, <b>독자를 나눈다</b> — 화면에는 사용자 문장, 로그에는 내부 식별자.</para>
        ///
        /// <para>이 규칙의 집행은 <c>SettingsUserFacingCopyTests</c>가 한다 — 다섯 탭의 <b>렌더된
        /// 문자열 전부</b>를 훑어 라틴 식별자/이슈번호/개발 어휘를 찾는다(비활성 탭 포함). 런타임에
        /// 문자열을 검사해 몰래 걸러내는 방식은 쓰지 않았다 — "⌃⌥⌘I" 같은 정당한 사용자 문구까지
        /// 오탐으로 망가뜨릴 수 있고, 조용히 고쳐 주는 방어는 다음 사람이 규칙을 배우지 못하게 한다.</para>
        /// </summary>
        private static void LogRoadmapNotes()
        {
            Debug.Log("[설정창/로드맵] 지금 회색으로 잠긴 행들의 내부 사정(사용자 화면에는 나오지 않습니다): " +
                $"[일반] 숨기기/보이기 단축키 {ShortcutLabel.Chord("V")} = GlobalKey에 V 항목이 없어 배선 대기 / " +
                "[일반] 로그인 자동 실행 = 네이티브 로그인 항목 등록(35-1-9 P3) / " +
                "[캐릭터] 포인트 컬러 팔레트 = 회의록 6 소관 / " +
                "[캐릭터] 말투(반말·존댓말) = 대사 두 벌 작성(35-3-3) / " +
                "[이벤트][접근성·성능][데이터] 3탭 = 35-1-9 P1/P2.");
        }

        private void OnEnable()
        {
            StickmanEventBus.CharacterScaleChanged += OnCharacterScaleChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.CharacterScaleChanged -= OnCharacterScaleChanged;
            // 창이 꺼진 채 차단막만 남으면 그 화면 영역이 이유 없이 클릭관통 해제로 남는다(비침해).
            if (_clickBlocker != null) _clickBlocker.enabled = false;
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_clickBlocker != null) Destroy(_clickBlocker.gameObject);
        }

        // ==================== 공개 진입점 ====================

        public void Toggle(string source)
        {
            if (_open) Close(source);
            else Open(source);
        }

        public void Open(string source)
        {
            if (_open) return;
            _open = true;
            _leftInitialized = false;   // 창을 여는 그 클릭이 곧바로 행 클릭으로 오인되지 않게.
            // 여는 그 순간은 정의상 조작 중이다 — 첫 커서 폴링(최대 0.05초)까지의 공백을 메운다.
            _lastSurfaceTouchTime = Time.unscaledTime;
            _dragIndex = -1;
            DisarmQuit();
            CloseOverlappingSurfaces($"설정창 열림({source})");
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_clickBlocker != null) _clickBlocker.enabled = true;
            RefreshAll();
            Debug.Log($"[설정창] 열림({source}) — 탭=[{TabNames[(int)_tab]}]. " +
                "[✕] / 창 밖 클릭으로 닫힙니다(ESC는 이 앱에서 다른 일에 묶여 있습니다).");
        }

        public void Close(string source)
        {
            if (!_open) return;
            _open = false;
            _dragIndex = -1;
            DisarmQuit();
            FlushPendingSave();
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_clickBlocker != null) _clickBlocker.enabled = false;
            Debug.Log($"[설정창] 닫힘({source}).");
            RestoreInfoWindowIfNeeded(source);
        }

        /// <summary>
        /// ★ 2026-09-01 — 설정창을 닫으면 <b>내가 밀어낸</b> 정보창을 그 자리로 돌려보낸다(페르소나 M8).
        ///
        /// <para>배타 규칙(<see cref="CloseOverlappingSurfaces"/>) 자체는 옳다. 문제는 <b>돌아갈 문이
        /// 없었다</b>는 것이다: 장비를 구경하다 [설정]을 누르면 정보창이 사라지고, [✕]를 누르면 빈
        /// 바탕화면만 남아 톱니 → [캐릭터]를 처음부터 다시 밟아야 했다. 진입 경로가 정보창 헤더의
        /// [설정] 칩 하나뿐인 창에서 이건 막다른 길이다.</para>
        ///
        /// <para>그래서 <b>시트(sheet)</b>처럼 행동하게 한다 — 얹힐 때 가려진 부모 창은 걷힐 때 돌아온다.
        /// 어떤 경로로 닫혔는지(=[✕]/창 밖 클릭/단축키 재입력)는 구분하지 않는다. 문자열 <c>source</c>로
        /// 분기하면 새 진입점이 생길 때마다 조용히 어긋나고, 사용자가 배우는 규칙도 하나여야 한다.</para>
        ///
        /// <para><b>단 하나의 예외는 전체화면 감지</b>다(원칙 2). 그 경로에서 정보창을 되살리면 게임 위에
        /// 방금 치운 창을 다시 얹는 셈이라 자동 숨김의 목적 자체가 뒤집힌다. <see cref="StickmanAgent.IsSuspended"/>를
        /// 직접 보므로 호출부가 그 사실을 잊어도 안전하다(문자열 사유에 기대지 않는다).</para>
        /// </summary>
        private void RestoreInfoWindowIfNeeded(string source)
        {
            bool restore = _restoreInfoWindowOnClose;
            _restoreInfoWindowOnClose = false;
            if (!restore) return;

            if (_agent != null && _agent.IsSuspended)
            {
                Debug.Log("[설정창] 정보창 복귀를 건너뜁니다 — 전체화면이 감지된 상태입니다(원칙 2). " +
                    "사용자가 부르지 않은 창이 게임 위로 돌아오는 것이 자동 숨김보다 나쁩니다.");
                return;
            }

            if (_infoWindow == null) _infoWindow = GetComponent<CharacterInfoWindow>();
            if (_infoWindow == null || _infoWindow.IsOpen) return;
            _infoWindow.Open($"설정창 닫힘({source}) — 열기 전에 보던 창으로 복귀");
        }

        /// <summary>
        /// 배타적 모달 — 이 창이 뜨면 <b>다른 모든 배타 표면</b>을 거둔다.
        /// <see cref="CharacterInfoWindow.CloseOverlappingSurfaces"/>와 <b>같은 규약</b>이며,
        /// 이제는 같은 규약이 아니라 <b>같은 코드</b>다(<see cref="ExclusiveSurfaces.CloseAllExcept"/>) —
        /// "규약"으로만 묶여 있던 동안 한쪽에만 상대가 등록돼 있어서 <c>I</c> 방향만 새고 있었다.
        ///
        /// <para>여기 남는 유일한 고유 로직은 <b>시트 복귀 예약</b>(M8)이다. 이건 배타 규칙이 아니라
        /// 이 창만의 성격이라 공통 집행 지점에 올리지 않는다.</para>
        /// </summary>
        private void CloseOverlappingSurfaces(string reason)
        {
            if (_infoWindow == null) _infoWindow = GetComponent<CharacterInfoWindow>();
            // 닫기 <b>전에</b> 기억한다 — 이 한 줄이 없으면 되돌아갈 자리가 사라진다(M8).
            _restoreInfoWindowOnClose = _infoWindow != null && _infoWindow.IsOpen;

            ExclusiveSurfaces.CloseAllExcept(this, reason);
        }

        // ==================== 루프 ====================

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측
            if (!_open) return;

            // ★★ 원칙 2 — 전체화면 게임이 감지되면 창과 차단막을 그 프레임에 거둔다. 복귀 시 자동으로
            //    다시 열지 않는다(정보창/팝오버와 같은 판단 — 사용자가 부르지 않은 창이 게임을 끄자마자
            //    튀어나오면 그 자체가 방해다).
            if (_agent != null && _agent.IsSuspended)
            {
                // 복귀 예약을 여기서 명시적으로 지운다 — RestoreInfoWindowIfNeeded의 IsSuspended 가드와
                // 이중이지만, 둘 중 하나가 사라져도 게임 위에 창이 되살아나지 않는다(원칙 2).
                _restoreInfoWindowOnClose = false;
                Close("전체화면 감지 — 자동 숨김(비침해 원칙 2)");
                return;
            }

            // ★★ 프레임 페이싱 홀드는 TickGlobalPointer() 안에 있다 — "창이 열려 있는 동안"이
            //    아니라 <b>"지금 이 창을 조작 중일 때"</b>만 걸어야 한다(근거: TickFramePacingHold
            //    문서. 정보창이 125분 열린 채 절전을 통째로 죽인 실측 사고와 같은 배선이었다).

            // 배율 적용 유예(랙돌/스펙터클 중) 풀기. ★ 이 창은 <b>구동자가 아니다</b> — 위 Update는
            // `if (!_open) return;`으로 시작하므로 창을 닫으면 여기가 안 돈다. 상시 구동자는
            // CharacterProgressionDirector이고(2026-09-01 구석 패널 삭제로 그쪽이 물려받았다),
            // 여기 한 줄은 "창이 열려 있는 동안 반응이 한 프레임도 늦지 않게" 하는 보조다.
            // 경과 시간 기반이라 두 곳에서 불려도 결과가 같다(멱등).
            CharacterScaleController.Tick();

            ApplyCanvasScaleFactor();
            SyncClickBlocker();
            TickQuitConfirm();
            TickGlobalPointer();
        }

        private void TickQuitConfirm()
        {
            if (!_quitArmed) return;
            if (Time.unscaledTime - _quitArmedAt < QuitConfirmSeconds) return;
            DisarmQuit();
        }

        private void TickGlobalPointer()
        {
            if (_buttonService == null || _panel == null) return;

            // 홀드 판정도 이 가드 뒤에 있다 — 전역 포인터 서비스가 없으면 커서를 관측할 수단이
            // 자체가 없다. 그 환경(에디터/Null 서비스)에서는 적응형 페이싱도 함께 꺼져 있으므로
            // 홀드가 없어서 생기는 손해가 없다.

            // 드래그(슬라이더) 중에는 폴링 간격을 없앤다 — 20Hz로 끌면 손잡이가 커서에서 뚝뚝 떨어진다.
            if (_dragIndex < 0)
            {
                _clickPollTimer += Time.unscaledDeltaTime;
                if (_clickPollTimer < ClickPollInterval) return;
                _clickPollTimer = 0f;
            }

            Vector2 osScreen = Vector2.zero;
            bool hasCursor = _agent != null && _agent.TryGetCursorPosition(out osScreen);
            Vector2 cursor = hasCursor
                ? ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _config)
                : Vector2.zero;

            TickFramePacingHold(hasCursor, cursor);

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            ProcessPointer(left, cursor, hasCursor);
        }

        /// <summary>
        /// "지금 이 창을 <b>조작 중</b>인가"를 프레임 페이싱에 알린다 —
        /// <see cref="CharacterInfoWindow"/>와 <b>같은 배선</b>이고 판정도 같은 플랫폼 중립 함수
        /// (<see cref="FramePacingPolicy.ShouldHoldForSurface"/>)를 쓴다. 두 창이 서로 다른 규칙을
        /// 갖게 되면 다음 사람이 어느 쪽이 진짜인지 알 수 없다.
        ///
        /// <para><b>왜 바꿨나</b>: 원래 <c>Update()</c>에서 무조건 걸려 있었다. 정보창의 실측
        /// (125분 열림 = 등급 전이 0회, 활성 등급 체류 100%)이 그 배선이 적응형 절전을 통째로
        /// 무력화한다는 것을 확정했고, 이 창은 <b>같은 패턴</b>이었다. 슬라이더 드래그가 끊기지
        /// 않는 이유는 <c>_dragIndex</c>가 그 자체로 "조작 중"이기 때문이다 — 커서가 창 밖으로
        /// 나가도 홀드가 유지된다.</para>
        /// </summary>
        private void TickFramePacingHold(bool hasCursor, Vector2 cursor)
        {
            // _quitArmed는 "정말 종료?"가 떠 있는 몇 초 — 그 순간의 클릭이 굼뜨면 안 된다.
            bool manipulating = _dragIndex >= 0 || _quitArmed;
            bool cursorOver = hasCursor && RectContainsScreenPoint(_panel, cursor);
            if (manipulating || cursorOver) _lastSurfaceTouchTime = Time.unscaledTime;

            if (FramePacingPolicy.ShouldHoldForSurface(cursorOver, manipulating,
                    Time.unscaledTime - _lastSurfaceTouchTime))
            {
                FramePacing.HoldActiveForInteraction();
            }
        }

        /// <summary>실제 입력과 테스트가 <b>공유하는</b> 포인터 처리(정보창과 같은 관례).</summary>
        private void ProcessPointer(bool buttonDown, Vector2 cursor, bool hasCursor)
        {
            bool prev = _leftPrev;
            if (!_leftInitialized)
            {
                _leftInitialized = true;
                _leftPrev = buttonDown;
                return;
            }
            _leftPrev = buttonDown;

            if (buttonDown && !prev)
            {
                if (hasCursor) FeedClick(cursor);
                return;
            }
            if (buttonDown && _dragIndex >= 0)
            {
                if (hasCursor) _host.DragTo(_dragIndex, cursor);
                return;
            }
            if (!buttonDown && prev && _dragIndex >= 0)
            {
                _dragIndex = -1;
                FlushPendingSave();   // 드래그가 끝난 시점에 한 번만 디스크를 두드린다.
            }
        }

        private void FeedClick(Vector2 cursor)
        {
            if (!_open) return;

            if (!RectContainsScreenPoint(_panel, cursor))
            {
                Close("창 밖 클릭");
                return;
            }

            if (ContainsScreenPoint(_closeRect, cursor))
            {
                if (TryClaimAction("close")) Close("[✕] 클릭");
                return;
            }

            for (int i = 0; i < TabCount; i++)
            {
                if (!ContainsScreenPoint(_tabRects[i], cursor)) continue;
                if (TryClaimAction("tab" + i)) SetTab((Tab)i, "탭 클릭");
                return;
            }

            if (ContainsScreenPoint(_pageUpRect, cursor))
            {
                if (TryClaimAction("pageUp")) ScrollPage(-1);
                return;
            }
            if (ContainsScreenPoint(_pageDownRect, cursor))
            {
                if (TryClaimAction("pageDown")) ScrollPage(+1);
                return;
            }

            _host.TryClick(cursor, out _dragIndex);
        }

        private bool TryClaimAction(string key)
        {
            if (_lastActionKey == key && Time.unscaledTime - _lastActionTime < ActionDedupSeconds) return false;
            _lastActionKey = key;
            _lastActionTime = Time.unscaledTime;
            return true;
        }

        // ==================== 값 동기화 ====================

        /// <summary>
        /// ★ 35-1-3 ①의 핵심 — 이 창 <b>밖</b>에서 배율이 바뀌면 슬라이더가 <b>같은 프레임에</b>
        /// 따라온다(저장 복원, 유예 해제 후 강제 적용 등). 반대 방향도 같은 이벤트로 흐른다.
        /// 값을 만지는 쪽들이 서로를 모른 채 같은 숫자를 가리키는 것이 이 구조의 목적이다.
        /// </summary>
        private void OnCharacterScaleChanged(CharacterScaleChangeEvent e)
        {
            if (_scaleSlider == null) return;
            _scaleSlider.SetValueSilently(e.Value);
        }

        private void RefreshAll()
        {
            if (_scaleSlider != null) _scaleSlider.SetValueSilently(CharacterScaleController.Value);
            if (_inkSwatches != null) _inkSwatches.SetIndexSilently(_config != null && _config.IsWhiteInk() ? 1 : 0);
            if (_gearIconToggle != null) _gearIconToggle.SetOn(AppSettingsModel.GearIconVisible);
            if (_autoHideToggle != null) _autoHideToggle.SetOn(AppSettingsModel.AutoHideOnFullscreen);
            if (_bubbleToggle != null) _bubbleToggle.SetOn(AppSettingsModel.ResolveDialogueBubbleEnabled(_config));
            if (_fontSizeSlider != null) _fontSizeSlider.SetValueSilently(AppSettingsModel.ResolveDialogueFontSize(_config));
            if (_visibleLengthSegment != null)
                _visibleLengthSegment.SetIndexSilently((int)AppSettingsModel.DialogueVisibleLength);
            if (_chatterSlider != null) _chatterSlider.SetValueSilently(AppSettingsModel.ChatterPercent);
            SyncSpeechGate();
            SyncGearWarning();
            ApplyTabVisibility();
        }

        /// <summary>★ 42-11 G — <c>말풍선 표시</c>가 꺼져 있으면 그 아래 세 행은 만져도 화면이 바뀌지
        /// 않는다. 활성인 채로 두면 "컨트롤이 움직이는데 화면이 약속과 다르다"가 된다.</summary>
        private void SyncSpeechGate()
        {
            if (_speechGate == null) return;
            _speechGate.SetEnabled(AppSettingsModel.ResolveDialogueBubbleEnabled(_config));
        }

        private void SyncGearWarning()
        {
            if (_gearWarnCaption == null) return;
            bool warn = !AppSettingsModel.GearIconVisible;
            if (_gearWarnCaption.gameObject.activeSelf != warn) _gearWarnCaption.gameObject.SetActive(warn);
        }

        /// <summary>슬라이더 드래그처럼 <b>연속으로 값이 바뀌는</b> 조작은 매 스텝 디스크를 두드리지 않는다
        /// (24시간 상주 앱). 토글/스와치는 즉시 저장하고, 슬라이더는 손을 뗄 때/창을 닫을 때 흘려보낸다.</summary>
        private void RequestSave() => _saveRequested = true;

        private void FlushPendingSave()
        {
            if (!_saveRequested) return;
            _saveRequested = false;
            CharacterSaveStore.Save();
        }

        // ==================== UI 구성 ====================

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("SettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // 씬 루트에 둔다 — 캐릭터 자손으로 두면 이 캔버스 안의 UI 이름이 "이름으로 캐릭터 파츠를
            // 찾는 코드"에 걸린다(2026-08-30에 부채꼴의 "Head"로 실제로 터진 사고).
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrderTopMost;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();

            // 그림 없는 컨테이너 + [그림자 → 본체(α1) → 보더] 형제 배치. 컨테이너에 Graphic을 붙이면
            // 그림자가 본체 위로 올라가 창 알파가 무너진다(InfoWindowPanelOpacityTests가 잠근 규칙).
            // 번짐 22 / 오프셋 -7. ★ 2026-09-01: 옛 값은 (18, -18)로 <b>오프셋이 번짐과 같았다</b> —
            // 감쇠 그림자에서 그 비율은 패널 실루엣과 어긋난 자리에 알파 1짜리 코어를 통째로 드러내
            // 다시 "두 번째 창"이 된다(UiChrome.AddShadow의 offset 문서).
            _panel = UiChrome.AddOpaquePanel(canvasGo.transform, "SettingsPanel", UiChrome.RadiusPanel,
                22f, new Vector2(0f, -7f), out Image panelImage);
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelImage.raycastTarget = true;   // 창 바탕을 눌러도 뒤(데스크톱)로 새지 않게.

            _host.Claim = TryClaimAction;
            _host.HitTest = ContainsScreenPoint;

            BuildHeader();
            BuildTabBar();
            BuildContent();
            BuildFooter();

            _masks = _panel.GetComponentsInChildren<RectMask2D>(true);

            // 클릭관통 차단막 — 씬 루트에 둔다(캐릭터 자식이면 캐릭터가 걷거나 구를 때 함께 돌아
            // 창의 화면 사각형과 어긋난다). isTrigger라 캐릭터 물리에는 관여하지 않는다.
            var blockerGo = new GameObject("SettingsClickBlocker");
            _clickBlocker = blockerGo.AddComponent<BoxCollider2D>();
            _clickBlocker.isTrigger = true;
            _clickBlocker.enabled = false;

            canvasGo.SetActive(false);
        }

        private void BuildHeader()
        {
            var barGo = new GameObject("Header", typeof(RectTransform));
            barGo.transform.SetParent(_panel, false);
            var bar = barGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(bar, 0f, 0f, PanelWidth, HeaderHeight);

            Text title = UiChrome.AddText(bar, "Title", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, bold: true);
            UiChrome.PlaceTopLeft(title.rectTransform, ContentPadX, -(HeaderHeight - 20f) * 0.5f, 320f, 20f);
            title.text = "StickMate 설정";

            Image close = UiChrome.AddSurface(bar, "Close", UiChrome.CardSurfaceMuted, UiChrome.RadiusChip);
            _closeRect = close.rectTransform;
            SettingsControls.PlaceTopRight(_closeRect, ContentPadX, -(HeaderHeight - 24f) * 0.5f, 24f, 24f);
            UiChrome.AddOutline(_closeRect, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurfaceMuted),
                UiChrome.RadiusChip);
            Text closeLabel = UiChrome.AddText(_closeRect, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter,
                UiChrome.TextSecondary);
            UiChrome.Stretch(closeLabel.rectTransform);
            closeLabel.text = "✕";
            var closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = close;
            closeButton.onClick.AddListener(() => { if (TryClaimAction("close")) Close("[✕] 클릭"); });

            AddHorizontalDivider(_panel, -HeaderHeight);
        }

        private void BuildTabBar()
        {
            var barGo = new GameObject("TabBar", typeof(RectTransform));
            barGo.transform.SetParent(_panel, false);
            var bar = barGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(bar, 0f, -HeaderHeight, PanelWidth, TabBarHeight);

            float x = ContentPadX;
            for (int i = 0; i < TabCount; i++)
            {
                // ★ 배지 유무 · 탭 폭 · (아래 ApplyTabVisibility의) 밑줄색이 전부 IsTabReady <b>하나</b>에서
                //   나온다. 그래서 탭이 채워지는 날 <b>아무도 아무것도 지우지 않아도</b> 배지가 사라진다.
                //   판정을 두 벌로 두면 반드시 한쪽만 갱신된다 — M7이 정확히 그 사고였다.
                bool ready = IsTabReady((Tab)i);
                float labelWidth = TabNames[i].Length * TabLabelCharWidth;
                float width = TabPadX * 2f + labelWidth
                    + (ready ? 0f : TabBadgeGap + TabBadgeWidth);

                var tabGo = new GameObject("Tab" + i, typeof(RectTransform));
                tabGo.transform.SetParent(bar, false);
                var rt = tabGo.GetComponent<RectTransform>();
                UiChrome.PlaceTopLeft(rt, x, 0f, width, TabBarHeight);
                _tabRects[i] = rt;

                // 탭 전체가 클릭 타깃이어야 한다(글자만 누르면 오조준이 잦다).
                Image hit = SettingsControls.AddHitArea(rt, "Hit");
                UiChrome.Stretch(hit.rectTransform);

                Text label = UiChrome.AddText(rt, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter,
                    UiChrome.InkTab(selected: false));
                // ★ Stretch가 아니라 <b>라벨 폭만큼의 상자</b>다. 이 상자는 옛 Stretch 상자
                //   (폭 20 + n×11)의 <b>정중앙 구간과 같아서</b> MiddleCenter 렌더 결과가 1pt도
                //   안 움직인다(배지가 붙어 부모가 넓어져도 마찬가지다 — 그래서 준비된 탭도 같은 길로
                //   보낸다). 왼쪽 정렬로 바꾸면 11f 근사 오차만큼 밀린다.
                UiChrome.PlaceTopLeft(label.rectTransform, TabPadX,
                    -(TabBarHeight - TabLabelHeight) * 0.5f, labelWidth, TabLabelHeight);
                label.text = TabNames[i];
                _tabLabels[i] = label;

                if (!ready)
                {
                    // 잉크는 <b>전 상태 상수</b>(InkMeta)다 — ApplyTabVisibility에 코드를 더하지 않고,
                    //   생성 시 1회 도색으로 끝난다(하루 종일 켜져 있는 앱: 프레임 비용 0).
                    Text badge = UiChrome.AddText(rt, "Badge", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                        UiChrome.InkMeta);
                    UiChrome.PlaceTopLeft(badge.rectTransform, TabPadX + labelWidth + TabBadgeGap,
                        -(TabBarHeight - TabBadgeHeight) * 0.5f, TabBadgeWidth, TabBadgeHeight);
                    badge.text = TabBadgeText;
                }

                // 활성 탭 밑줄 2pt(35-1-5). 비활성은 색만 투명하게 둔다 — 껐다 켜면 배치가 흔들린다.
                Image underline = UiChrome.AddSurface(rt, "Underline", UiChrome.Accent, 2);
                UiChrome.PlaceTopLeft(underline.rectTransform, 0f, -(TabBarHeight - 2f), width, 2f);
                underline.raycastTarget = false;
                _tabUnderlines[i] = underline;

                int captured = i;
                var button = hit.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.onClick.AddListener(() =>
                {
                    if (TryClaimAction("tab" + captured)) SetTab((Tab)captured, "탭 클릭");
                });

                x += width + UiChrome.Space1;
            }

            AddHorizontalDivider(_panel, -(HeaderHeight + TabBarHeight));
        }

        private void BuildContent()
        {
            var viewGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewGo.transform.SetParent(_panel, false);
            _viewport = viewGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(_viewport, ContentPadX, -(HeaderHeight + TabBarHeight),
                PanelWidth - ContentPadX * 2f, ContentHeight);

            for (int i = 0; i < TabCount; i++)
            {
                var pageGo = new GameObject("Page_" + TabNames[i], typeof(RectTransform));
                pageGo.transform.SetParent(_viewport, false);
                var page = pageGo.GetComponent<RectTransform>();
                UiChrome.PlaceTopLeft(page, 0f, 0f, SettingsControls.CardWidth, ContentHeight);
                _pages[i] = page;

                float y = -ContentPadTop;
                y = AddEyebrow(page, TabEyebrows[i], y);

                if (!IsTabReady((Tab)i)) y = BuildPlaceholderTab(page, y, (Tab)i);
                else if ((Tab)i == Tab.General) y = BuildGeneralTab(page, y);
                else y = BuildCharacterTab(page, y);

                _pageHeights[i] = -y + ContentPadTop;
            }

            BuildPageButtons();
            ApplyTabVisibility();
        }

        private static float AddEyebrow(RectTransform page, string text, float y)
        {
            Text eyebrow = UiChrome.AddText(page, "Eyebrow", UiChrome.FontLabel, TextAnchor.MiddleLeft,
                UiChrome.InkMeta);
            UiChrome.PlaceTopLeft(eyebrow.rectTransform, 2f, y, SettingsControls.CardWidth, 14f);
            eyebrow.text = text;
            return y - 20f;
        }

        // -------------------- [일반] --------------------

        private float BuildGeneralTab(RectTransform page, float y)
        {
            var display = new SettingsCardBuilder(page, "표시", y, _host);
            _autoHideToggle = display.AddToggle("general.autoHide", "전체화면 게임 감지 시 자동 숨김",
                AppSettingsModel.AutoHideOnFullscreen,
                on =>
                {
                    AppSettingsModel.SetAutoHideOnFullscreen(on);
                    CharacterSaveStore.Save();
                    Debug.Log($"[설정창] 전체화면 자동 숨김 {(on ? "켬" : "끔")} — 이 스위치는 캐릭터만이 " +
                        "아니라 StickmanAgent.IsSuspended를 통째로 좌우합니다(설정창/팝오버/부채꼴이 모두 " +
                        "그것을 봅니다). 끄면 전체화면 게임 위에 이 창들과 그 클릭관통 차단막" +
                        "(BoxCollider2D)까지 남습니다 — 절대 불변 원칙 2의 사용자 예외이므로 " +
                        "그 대가를 캡션에 적어 두었습니다.");
                },
                // ★ 2026-09-01(페르소나 J3) — 예전 캡션은 "게임 · 영상이 전체화면이 되면 즉시
                //   사라집니다."로 <b>캐릭터 얘기만</b> 했다. 그런데 이 스위치를 끄면 IsSuspended가
                //   영원히 false가 되어 창과 <b>720×560 클릭 차단막</b>도 게임 위에 남는다. 사용자는
                //   캐릭터가 남는 데 동의한 것이지 "클릭이 안 먹는 구멍"에 동의한 적이 없다.
                caption: "켜면 캐릭터도 열린 창도 함께 사라집니다. 끄면 창이 막는 클릭까지 그대로 남아요.");

            // ★ 캡션은 장식이 아니라 <b>이 버튼의 한계 고지</b>다 — SetCharacterVisibleNow의 XML 문서가
            //   "한계를 캡션 없이 숨기지 않는다"고 선언해 놓고 정작 caption을 안 넘겨, 그 한계가
            //   Debug.Log에만 있었다(페르소나 M9). 방해받아서 숨긴 캐릭터가 말없이 되살아나는 것은
            //   이 창이 존재하는 이유(클래스 문서 첫 문단) 바로 그 자리에서의 배신이다.
            display.AddButtons("general.hideNow", "지금 즉시", new[] { "숨기기", "보이기" },
                index => SetCharacterVisibleNow(index == 1),
                caption: "지금 한 번만 숨깁니다 — 전체화면 앱을 오갔다 오면 다시 나타나요.");

            display.AddToggle("general.hideHotkey", "숨기기 / 보이기 단축키", false, null,
                hotkey: ShortcutLabel.Chord("V"), enabled: false,
                disabledNote: DisabledReason.NotBuilt("이 단축키는 다음 업데이트에서 켜집니다."));
            y = display.Finish(y);

            var screenUi = new SettingsCardBuilder(page, "화면 위 UI", y, _host);
            // ★ 2026-09-01 — 여기 있던 "구석 크기 패널 (왼쪽 아래 모서리)" 토글을 <b>지웠다</b>.
            //   사용자가 세 번 요청한 것은 "끌 수 있게"가 아니라 <b>삭제</b>였고, 그 패널
            //   (CornerHoverPanel/SizeDialWidget)은 이 라운드에 통째로 제거됐다. 크기 조정은
            //   아래 [캐릭터] 탭의 "캐릭터 크기" 슬라이더 하나로 일원화된다.

            _gearIconToggle = screenUi.AddToggle("general.gearIcon", "톱니 아이콘",
                AppSettingsModel.GearIconVisible,
                on =>
                {
                    AppSettingsModel.SetGearIconVisible(on);
                    CharacterSaveStore.Save();
                    SyncGearWarning();
                    Debug.Log($"[설정창] 톱니 아이콘 {(on ? "켬" : "끔")} — " +
                        "끄면 정보창/설정창의 마우스 진입점이 사라지고 단축키만 남습니다.");
                });

            // 경고 캡션은 "끈 경우에만" 보인다(35-1-5의 조건부 줄). 자리는 <b>항상</b> 잡아 둔다 —
            // 나타났다 사라질 때 카드 높이가 바뀌면 아래 카드가 통째로 움직여, 끄는 순간 누르려던
            // 다음 버튼이 발밑에서 미끄러진다.
            _gearWarnCaption = screenUi.AddCaptionLine("GearWarn",
                // 시안의 "⚠" 기호는 뺐다 — 이 프로젝트의 UI 폰트(LegacyRuntime.ttf)에 U+26A0이 있다는
                // 보장이 없어 두부(□)가 될 수 있다. ▲/▼/✕는 정보창·팝오버에서 이미 렌더가 확인된
                // 글리프라 그대로 쓴다. 경고라는 사실은 색(WarmAccent)이 이미 말하고 있다.
                $"끄면 캐릭터 정보창은 {ShortcutLabel.Chord("I")} 로만 열 수 있어요.", UiChrome.WarmAccent);
            _gearWarnCaption.gameObject.SetActive(false);
            y = screenUi.Finish(y);

            var startStop = new SettingsCardBuilder(page, "시작 / 종료", y, _host);
            startStop.AddToggle("general.autoLaunch", "로그인할 때 자동 실행", false, null,
                enabled: false,
                disabledNote: DisabledReason.NotBuilt("이 기능은 다음 업데이트에 들어옵니다."));

            Image[] quitButtons = startStop.AddButtons("general.quit", "종료",
                new[] { QuitLabelText }, _ => OnQuitClicked());
            _quitSurface = quitButtons.Length > 0 ? quitButtons[0] : null;
            _quitLabel = _quitSurface != null ? _quitSurface.GetComponentInChildren<Text>() : null;
            y = startStop.Finish(y);

            return y;
        }

        /// <summary>
        /// "지금 즉시" 숨기기/보이기 — <see cref="StickmanBlackboard.SetCharacterVisible"/>를 쓴다.
        /// 이 통로는 가출(20절)이 쓰던 것과 <b>같은 것</b>이라 새 배관을 만들지 않는다.
        ///
        /// <para><b>한계를 캡션 없이 숨기지 않는다</b>: 이것은 <b>1회성 동작</b>이지 영구 상태가 아니다.
        /// 전체화면 감지가 왕복하면 <c>StickmanAgent.Resume()</c>이 렌더러를 되살린다(그쪽 소유권).
        /// 영구 숨김이 필요하면 <see cref="StickmanAgent"/>에 상태를 하나 두어야 하는데, 그 파일은
        /// 이번 라운드에 다른 작업자가 잡고 있어 리더 배정 사항으로 남긴다.</para>
        /// </summary>
        private void SetCharacterVisibleNow(bool visible)
        {
            StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
            if (bb == null || bb.SetCharacterVisible == null)
            {
                Debug.LogWarning("[설정창] 지금 즉시 숨기기/보이기 실패 — 캐릭터 블랙보드가 없습니다.");
                return;
            }
            bb.SetCharacterVisible(visible);
            Debug.Log($"[설정창] 캐릭터를 지금 즉시 {(visible ? "보입니다" : "숨깁니다")} — " +
                "1회성 동작입니다(전체화면 감지가 왕복하면 다시 나타납니다).");
        }

        private void OnQuitClicked()
        {
            if (!_quitArmed)
            {
                _quitArmed = true;
                _quitArmedAt = Time.unscaledTime;
                ApplyQuitStyle();
                Debug.Log($"[설정창] [지금 종료] 1차 클릭 — {QuitConfirmSeconds:F0}초 안에 다시 누르면 종료합니다.");
                return;
            }

            Debug.Log("[설정창] [지금 종료] 확정 — Application.Quit()을 호출합니다. 저장은 " +
                "CharacterProgressionDirector.OnApplicationQuit()이 담당하므로 데이터 손실이 없습니다.");
            FlushPendingSave();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void DisarmQuit()
        {
            if (!_quitArmed) return;
            _quitArmed = false;
            ApplyQuitStyle();
        }

        private void ApplyQuitStyle()
        {
            if (_quitLabel == null || _quitSurface == null) return;
            _quitLabel.text = _quitArmed ? "정말 종료?" : QuitLabelText;
            _quitLabel.color = _quitArmed ? UiChrome.WarmAccent : UiChrome.TextPrimary;
            _quitSurface.color = _quitArmed
                ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurface)
                : SettingsControls.ButtonSurfaceOnCard;
        }

        // -------------------- [캐릭터] --------------------

        private float BuildCharacterTab(RectTransform page, float y)
        {
            var look = new SettingsCardBuilder(page, "모양", y, _host);

            // ★ 크기는 반드시 단일 소스를 지난다(35-1-3 ①). 여기서 UiLayoutModel/Agent를 직접 부르면
            //   적용 게이트(랙돌 중 유예)가 이 경로에만 없는 상태가 되어 규칙이 두 벌이 된다.
            _scaleSlider = look.AddSlider("character.scale", "캐릭터 크기",
                StickConfig.MinCharacterScale, StickConfig.MaxCharacterScale, CharacterScaleController.ValueStep,
                CharacterScaleController.Value, v => v.ToString("0.00") + "×",
                v =>
                {
                    CharacterScaleController.Request(v, "설정창 슬라이더");
                    RequestSave();
                });

            var inkColors = new Color[2];
            inkColors[0] = _config != null ? Opaque(_config.primaryOutlineColor) : Color.black;
            inkColors[1] = _config != null ? Opaque(_config.whiteInkColor) : Color.white;
            _inkSwatches = look.AddSwatches("character.ink", "잉크색", inkColors,
                _config != null && _config.IsWhiteInk() ? 1 : 0, OnInkSwatchClicked);

            // 포인트 컬러는 회의록 6 소관 — 자리만 잡고 비활성(35-1-7 "없는 척하지 않는다").
            //
            // ★ 2026-09-01(페르소나 소은 #7-d) — 견본이 셋이었는데 <b>둘이 같은 색</b>으로 보였다:
            //   UiChrome.WarmAccent는 Accent와 <b>값이 완전히 같다</b>("두 번째 강조색을 발명하지
            //   않는다"는 팔레트 문서의 의도된 결정). 의도가 옳아도, 고를 수 있는 색을 나란히 늘어놓는
            //   자리에서 같은 색 두 개는 사용자에게 <b>표시 버그</b>로 읽힌다. 그래서 새 색을 만들지도
            //   말고 같은 색을 두 번 그리지도 말고, <b>지금 이 앱에 실제로 있는 두 색</b>만 놓는다.
            look.AddSwatches("character.point", "포인트 컬러 (눈 · 윤곽선)",
                new[] { UiChrome.Accent, UiChrome.TextPrimary }, -1, null,
                enabled: false,
                disabledNote: DisabledReason.NotBuilt("포인트 컬러 팔레트는 다음 업데이트에 들어옵니다."));
            y = look.Finish(y);

            var speech = new SettingsCardBuilder(page, "말과 행동", y, _host);

            // ★ 42-11 판정 G — <b>원인이 결과보다 먼저</b>. 이 토글이 카드 맨 아래에 있던 동안, 아래
            //   세 행이 왜 비활성인지 알려면 그 이유를 찾아 <b>아래로 내려가</b> 읽어야 했다.
            _bubbleToggle = speech.AddToggle("character.bubble", "말풍선 표시",
                AppSettingsModel.ResolveDialogueBubbleEnabled(_config),
                on =>
                {
                    AppSettingsModel.SetDialogueBubbleEnabled(on);
                    SyncSpeechGate();
                    CharacterSaveStore.Save();
                    Debug.Log($"[설정창] 말풍선 표시 {(on ? "켬" : "끔")} — 대사 생성 파이프라인은 그대로 돕니다" +
                        "(원칙 1의 행동-텍스트 싱크는 설정으로 끌 수 있는 물건이 아닙니다). 그리지 않을 뿐입니다." +
                        $" 아래 세 행은 {(on ? "다시 조절할 수 있습니다" : "지금 만져도 화면이 바뀌지 않으므로 함께 비활성이 됩니다")}.");
                });

            speech.AddSegment("character.tone", "말투", new[] { "반말", "존댓말" }, 0, null,
                enabled: false,
                disabledNote: DisabledReason.NotBuilt("말투 고르기는 다음 업데이트에 들어옵니다."));

            // 아래 세 행은 말풍선을 그리지 않으면 전부 무효다 — 한 손잡이로 함께 내린다(42-11 G).
            _speechGate = new SettingsRowGate("말풍선 표시를 켜면 조절할 수 있어요.");

            _fontSizeSlider = speech.AddSlider("character.fontSize", "말풍선 글자 크기",
                AppSettingsModel.MinDialogueFontSize, AppSettingsModel.MaxDialogueFontSize, 1f,
                AppSettingsModel.ResolveDialogueFontSize(_config), v => Mathf.RoundToInt(v) + "pt",
                v => { AppSettingsModel.SetDialogueFontSize(Mathf.RoundToInt(v)); RequestSave(); },
                gate: _speechGate);

            // ★ 2026-09-02(42-4) — 초 슬라이더 폐기. 값 라벨이 없는 것은 빠뜨린 게 아니라 판정이다:
            //   보여줄 정직한 숫자가 없다(효과가 대사마다 다르고 눈으로 0.3초를 잴 수 없다).
            _visibleLengthSegment = speech.AddSegment("character.visibleLength", "대사 표시 시간",
                DialogueVisibleLengthOptions, (int)AppSettingsModel.DialogueVisibleLength,
                i =>
                {
                    AppSettingsModel.SetDialogueVisibleLength((DialogueVisibleLength)i);
                    RequestSave();
                },
                caption: "대사가 떠 있는 시간은 글자 수에 맞춰 정해집니다. 천천히 읽는 편이면 늘려 두세요.",
                gate: _speechGate);

            _chatterSlider = speech.AddSlider("character.chatter", "잡담 빈도",
                0f, AppSettingsModel.MaxChatterPercent, 10f,
                AppSettingsModel.ChatterPercent, v => Mathf.RoundToInt(v) + "%",
                v => { AppSettingsModel.SetChatterPercent(Mathf.RoundToInt(v)); RequestSave(); },
                caption: "100%가 기본값입니다. 0%로 두면 혼잣말을 하지 않아요.",
                gate: _speechGate);

            SyncSpeechGate();
            y = speech.Finish(y);

            return y;
        }

        /// <summary>
        /// `대사 표시 시간` 세그먼트 문구(docs/UX_FLOW.md 42-7 확정형). <b>순서가 곧
        /// <see cref="DialogueVisibleLength"/>의 순서</b>다 — 둘이 어긋나면 사용자가 고른 칸과 저장되는
        /// 값이 갈린다.
        ///
        /// <para>`기본`이라는 낱말이 "배포 기본값이 어느 것인지"를 캡션 한 문장 없이 알려 준다.
        /// 폭 검산(42-7): 42 + 42 + 69 + 간격 8 = <b>161pt</b>, 라벨 상자(420pt)와 여유 71pt.</para>
        /// </summary>
        private static readonly string[] DialogueVisibleLengthOptions = { "기본", "길게", "아주 길게" };

        /// <summary>스와치 색은 <b>반드시 불투명</b>이어야 한다 — 반투명 판이 하나라도 얹히면 그 자리의
        /// 창 알파가 내려간다(SettingsControls 클래스 문서의 알파 규칙).</summary>
        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        /// <summary>
        /// 잉크색 전환 — 정보창 스와치/단축키 ⌃⌥⌘C와 <b>완전히 같은 경로</b>다.
        /// ★ <c>_config.inkColor</c>(직렬화 필드)에 절대 쓰지 않는다: 그 에셋은 프리팹 16개 컴포넌트에
        /// 배선된 배포 기본값이고, 에디터는 플레이 모드 중의 변경을 되돌리지 않는다(2026-08-31 R5).
        /// </summary>
        private void OnInkSwatchClicked(int index)
        {
            if (_config == null) return;
            StickmanInkColor next = index == 1 ? StickmanInkColor.White : StickmanInkColor.Black;
            if (_config.ResolveInkPreset() == next) return;

            _config.SetRuntimeInkColor(next);
            CharacterAppearanceModel.SetInkColor(next);
            if (_agent != null) _agent.ApplyInkColorFromConfig();
            CharacterSaveStore.Save();
            Debug.Log($"[설정창] 잉크색 전환 -> {next} (캐릭터/액세서리에 즉시 반영, 즉시 저장). " +
                "배포 에셋의 직렬화 필드는 건드리지 않습니다.");
        }

        // -------------------- 아직 비어 있는 탭 --------------------

        private float BuildPlaceholderTab(RectTransform page, float y, Tab tab)
        {
            var card = new SettingsCardBuilder(page, TabNames[(int)tab], y, _host);
            // ★ 문구는 <b>세 탭 공통</b>이라 세 탭 전부에서 참이어야 한다(43-1 ③). "이 스위치들"은
            //   [데이터] 탭의 `저장 파일 위치`가 스위치가 아니라서 거짓이 된다 — "여기 적힌 항목들"은
            //   바로 윗줄 라벨을 가리키고 컨트롤 종류를 약속하지 않는다.
            card.AddToggle("placeholder." + (int)tab, PlaceholderLabel(tab), false, null,
                enabled: false,
                disabledNote: DisabledReason.NotBuilt("여기 적힌 항목들은 다음 업데이트에 들어옵니다."));
            return card.Finish(y);
        }

        private static string PlaceholderLabel(Tab tab) => tab switch
        {
            Tab.Event => "방해 강도 프리셋 + 자동 발동 개별 토글",
            Tab.Accessibility => "윤곽선 강조 · 애니메이션 줄이기 · 저전력 렌더링",
            _ => "저장 파일 위치 · 초기화 · 네트워크(전부 기본 꺼짐)",
        };

        // -------------------- 푸터 / 페이지 --------------------

        private void BuildFooter()
        {
            AddHorizontalDivider(_panel, -(PanelHeight - FooterHeight));

            var footGo = new GameObject("Footer", typeof(RectTransform));
            footGo.transform.SetParent(_panel, false);
            var foot = footGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(foot, 0f, -(PanelHeight - FooterHeight), PanelWidth, FooterHeight);

            _footerLeft = UiChrome.AddText(foot, "SaveHint", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(_footerLeft.rectTransform, ContentPadX, -(FooterHeight - 14f) * 0.5f, 300f, 14f);
            _footerLeft.text = "변경은 즉시 저장됩니다.";

            Text right = UiChrome.AddText(foot, "HowToOpen", UiChrome.FontCaption, TextAnchor.MiddleRight,
                UiChrome.TextTertiary);
            SettingsControls.PlaceTopRight(right.rectTransform, ContentPadX, -(FooterHeight - 14f) * 0.5f, 380f, 14f);
            // 시안의 문구는 "톱니 아이콘 클릭 · ⌃⌥⌘,"였지만, <b>실제로 존재하는 경로</b>만 적는다 —
            // 없는 문을 알려 주는 것은 이 프로젝트가 원칙 1로 금지한 "표시와 실제의 불일치"다.
            // ★ 그 시안의 쉼표도 지금은 죽은 표기다 — 2026-09-01에 P로 옮겼다(⌃⌥⌘,는 macOS 접근성
            //   "대비 줄이기" 예약 조합이라 우리가 누를 때마다 사용자 OS 설정이 바뀌었다).
            // 부채꼴 5번째 버튼은 36-11이 "만들지 않는다"로 결론지어 두었으므로 리더 판단 사항으로 남겼다.
            right.text = $"이 창을 여는 방법: 캐릭터 정보창 [설정] · {ShortcutLabel.Chord("P")}";
        }

        /// <summary>
        /// [▲][▼] 페이지 넘김 — 33-7-8 보관함과 <b>같은 방식</b>이다. 휠/드래그 스크롤을 쓰지 않는 이유:
        /// 이 창 밖은 클릭관통이라 휠이 <b>밑에 있는 남의 앱</b>으로 새는 경계가 생긴다(비침해).
        /// </summary>
        private void BuildPageButtons()
        {
            // 오른쪽 끝이 [▼]가 되도록 <b>▲가 왼쪽</b>이다(시안의 [▲][▼] 순서).
            _pageDownRect = AddPageButton("PageDown", "▼", 0f);
            _pageUpRect = AddPageButton("PageUp", "▲", 30f);
            SyncPageButtons();
        }

        /// <summary>페이지 칩의 한 변(pt) — 탭바(40pt) 안에 8pt 여백을 남기고 앉는다.</summary>
        private const float PageButtonSize = 24f;

        /// <summary>
        /// ★ 2026-09-01(페르소나 소은 #7-b / 민지 M12) — 칩을 <b>탭바 오른쪽 끝</b>으로 옮겼다.
        ///
        /// <para>예전 자리는 패널 y 496~520pt였는데 <b>콘텐츠 뷰포트가 88~526pt</b>다. 즉 칩이
        /// 내용 <b>위에</b> 떠 있었고 그 자리를 비워 두는 여백이 없었다. 실물에서 [▼]가 "잡담 빈도"의
        /// "100%" 값 라벨을 덮었고, [▲]는 그 행의 [+] 스텝 버튼과 거의 붙어 <c>[−] ▬▬ [+] [▲] [▼]</c>로
        /// <b>같은 줄의 미세 조정 버튼</b>처럼 읽혔다(실제로는 페이지 스크롤이다 — 표시와 실제의 불일치).</para>
        ///
        /// <para>탭바는 오른쪽이 비어 있다(탭 5개가 x 20~489 — 미구현 탭 3개의 `준비 중` 배지를 포함한
        /// 값이고, 칩은 x 646~700이라 사이가 157pt 남는다). 콘텐츠 밖이면서 사용자가 이미
        /// 보는 자리라 거터를 새로 만들 필요도, 카드 폭(680)을 건드릴 필요도 없다 — 이 창의 세로
        /// 예산은 이미 꽉 차 있어서 거터를 만들려면 모든 행이 다시 계산된다.</para>
        /// </summary>
        private RectTransform AddPageButton(string name, string glyph, float xOffsetFromRight)
        {
            Image surface = UiChrome.AddSurface(_panel, name, UiChrome.CardSurfaceMuted, UiChrome.RadiusChip);
            SettingsControls.PlaceTopRight(surface.rectTransform, ContentPadX + xOffsetFromRight,
                -(HeaderHeight + (TabBarHeight - PageButtonSize) * 0.5f), PageButtonSize, PageButtonSize);
            UiChrome.AddOutline(surface.rectTransform, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurfaceMuted), UiChrome.RadiusChip);
            Text label = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;

            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            int direction = glyph == "▲" ? -1 : +1;
            string key = name;
            button.onClick.AddListener(() => { if (TryClaimAction(key)) ScrollPage(direction); });
            return surface.rectTransform;
        }

        private void ScrollPage(int direction)
        {
            int i = (int)_tab;
            float max = Mathf.Max(0f, _pageHeights[i] - ContentHeight);
            float next = Mathf.Clamp(_pageScroll[i] + direction * PageStep, 0f, max);
            if (Mathf.Approximately(next, _pageScroll[i])) return;
            _pageScroll[i] = next;
            ApplyScroll();
        }

        private void ApplyScroll()
        {
            RectTransform page = _pages[(int)_tab];
            if (page == null) return;
            page.anchoredPosition = new Vector2(0f, _pageScroll[(int)_tab]);
        }

        private void SyncPageButtons()
        {
            bool scrollable = _pageHeights[(int)_tab] > ContentHeight + 0.5f;
            if (_pageUpRect != null && _pageUpRect.gameObject.activeSelf != scrollable)
                _pageUpRect.gameObject.SetActive(scrollable);
            if (_pageDownRect != null && _pageDownRect.gameObject.activeSelf != scrollable)
                _pageDownRect.gameObject.SetActive(scrollable);
        }

        private void SetTab(Tab tab, string source)
        {
            if (_tab == tab) return;
            _tab = tab;
            DisarmQuit();
            FlushPendingSave();
            ApplyTabVisibility();
            Debug.Log($"[설정창] 탭 전환 -> [{TabNames[(int)tab]}]({source}).");
        }

        /// <summary>
        /// 이 탭에 <b>내용이 있는가</b> — 탭 <b>안쪽</b>(회색 행)과 탭 <b>버튼</b>이 같은 하나를 본다.
        ///
        /// <para>2026-09-01까지 이 판정은 <see cref="BuildPages"/>의 <c>switch</c> 안에만 있었고
        /// <see cref="ApplyTabVisibility"/>는 다섯 탭을 <b>똑같이</b> 칠했다. 그래서 회색 처리가 탭을
        /// <b>누른 뒤에야</b> 보였고, 첫 방문에서 5탭 중 3탭이 헛걸음이 됐다(페르소나 M7).
        /// 판정을 두 벌로 두면 반드시 한쪽만 갱신되므로 <b>여기 하나</b>로 합친다.</para>
        /// </summary>
        private static bool IsTabReady(Tab tab) => tab == Tab.General || tab == Tab.Character;

        private void ApplyTabVisibility()
        {
            for (int i = 0; i < TabCount; i++)
            {
                bool active = i == (int)_tab;
                bool ready = IsTabReady((Tab)i);
                if (_pages[i] != null && _pages[i].gameObject.activeSelf != active)
                    _pages[i].gameObject.SetActive(active);
                if (_tabLabels[i] != null)
                {
                    // 준비 중인 탭은 <b>고르기 전에</b> 그렇게 보인다. 다만 골랐을 때는 완전히 죽이지
                    // 않는다 — "내가 지금 어디에 있는지"는 빈 탭에서도 읽혀야 한다.
                    // ★ 고르지 않은 탭은 준비 여부로 <b>더 흐려지지 않는다</b>. 옛 코드의 2.35:1이
                    //   "죽은 탭에는 글자가 한 자도 없다"는 신고를 만들었다 — 글자는 있었다.
                    //   준비 중이라는 사실은 아래 밑줄과 탭 내용이 말한다.
                    _tabLabels[i].color = UiChrome.InkTab(active, ready);
                    _tabLabels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                }
                if (_tabUnderlines[i] != null)
                {
                    // 밑줄 색도 같은 말을 한다: 파란 밑줄(Accent)은 "여기 내용이 있다"의 표시였다.
                    _tabUnderlines[i].color = active
                        ? (ready ? UiChrome.Accent : UiChrome.NonTextMuted)
                        : Color.clear;
                }
            }
            ApplyScroll();
            SyncPageButtons();
        }

        private static void AddHorizontalDivider(RectTransform parent, float y)
        {
            Image divider = UiChrome.AddSurface(parent, "Divider", SettingsControls.DividerOnPanel, 2);
            UiChrome.PlaceTopLeft(divider.rectTransform, 0f, y, PanelWidth, 1f);
            divider.raycastTarget = false;
        }

        // ==================== 좌표 / 차단막 ====================

        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
        }

        private void SyncClickBlocker()
        {
            if (_clickBlocker == null || _panel == null) return;
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : Camera.main;
            if (cam == null) { _clickBlocker.enabled = false; return; }

            _panel.GetWorldCorners(_corners);
            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(_corners[0].x, _corners[0].y, depth));
            Vector3 tr = cam.ScreenToWorldPoint(new Vector3(_corners[2].x, _corners[2].y, depth));

            _clickBlocker.enabled = true;
            _clickBlocker.transform.position = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, 0f);
            _clickBlocker.size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
        }

        /// <summary>마스크에 잘린 자리는 <b>눌리지 않는다</b>(R2 M3의 규칙을 이 창에도 그대로 적용).
        /// 페이지를 넘겨 화면 밖으로 나간 행이 계속 눌리면 그것이 이 프로젝트가 "최악"이라고 부르는
        /// 패턴이다 — 안 보이는데 클릭은 먹는 UI.</summary>
        private bool ContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (!RectContainsScreenPoint(rt, screenPoint)) return false;
            if (_masks == null || rt == null) return true;
            for (int i = 0; i < _masks.Length; i++)
            {
                RectMask2D mask = _masks[i];
                if (mask == null || !mask.isActiveAndEnabled) continue;
                RectTransform maskRect = mask.rectTransform;
                if (maskRect == null || maskRect == rt || !rt.IsChildOf(maskRect)) continue;
                if (!RectContainsScreenPoint(maskRect, screenPoint)) return false;
            }
            return true;
        }

        private static bool RectContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(_corners);
            return screenPoint.x >= _corners[0].x && screenPoint.x <= _corners[2].x &&
                   screenPoint.y >= _corners[0].y && screenPoint.y <= _corners[2].y;
        }

        /// <summary>씬에 EventSystem이 있어도 입력 모듈이 없으면 Button.onClick이 영원히 발동하지 않는다
        /// (이 프로젝트가 실제로 밟았던 함정) — 다른 창들과 같은 보강.</summary>
        private static void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current != null
                ? EventSystem.current
                : FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.GetComponent<BaseInputModule>() == null)
                    existing.gameObject.AddComponent<StandaloneInputModule>();
                return;
            }
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
