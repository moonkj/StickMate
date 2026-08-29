using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 캐릭터 창(장비 / 외형 / 보관함) — 2026-08-30 <b>리디자인 라운드</b>.
    /// 사용자 요청: "게임처럼 약간 첨부파일형태였음 좋겠어. 다만 능력치는 스트레스나, 뭔가 다른정보를
    /// 보여주면좋겠고. 탭을 하나 더만들어서 가지고있는 아이템 장비들을 보여주면좋을듯. 장비나, 행동들..
    /// 나중에 아이템으로 팔거니깐" / "캐릭터는 간단하지만 캐릭터창은 깔끔하고 요즘 게임 캐릭터창처럼
    /// 좋아야해". 세부 설계는 리더 + UX 디자이너 확정안을 그대로 구현했다.
    ///
    /// ============================================================================
    /// 참고 이미지(RPG 캐릭터 창)에서 가져온 것 / 바꾼 것
    /// ============================================================================
    /// 가져온 것: 좌측 고정 패널(이름·칭호·초상화·게이지) + 우측 탭 패널(카드 그리드 → 설명 카드 →
    /// 하단 2열x3행 스탯 블록)이라는 골격.
    /// 바꾼 것 — 이 앱에 <b>실제로 존재하는 사실</b>로만 채운다:
    ///   · HP 바 → <b>스트레스 바</b>(이 앱에 HP는 없다).
    ///   · 공격력/방어력/… → 근속 / 함께한 시간 / 격파 성공 / 대결 승리 / 활쏘기 명중 / 넘어진 횟수
    ///     (Core/CharacterStatsModel.cs). <b>스트레스와 "지금 상태"는 스탯 칸에서 뺐다</b> —
    ///     스트레스는 좌측 게이지와 값이 겹치고, "지금 상태"는 몇 초마다 바뀌어 그리드의 시선을
    ///     혼자 가져간다. 상태는 초상화 바로 아래 <b>프레즌스 라인</b>("지금 · 걷는 중")으로 옮겼다.
    ///   · 아이템 설명에 가짜 수치("방어력 +2")도, 없는 효과("매면 자세가 곧아진다")도 넣지 않는다
    ///     (Core/ItemCatalog.cs 문구 원칙).
    ///
    /// ============================================================================
    /// 색/여백/모서리는 이 파일이 고르지 않는다
    /// ============================================================================
    /// 전부 Interaction/UiChrome.cs의 토큰에서 온다(둥근 모서리 스프라이트도 거기서 굽는다).
    /// 여백은 4/8/12/16/24 다섯 단계, 글자는 22/14/12/11/10 다섯 단계 위계만 쓴다.
    ///
    /// ============================================================================
    /// 초상화 = 전용 미니 피규어의 실시간 촬영
    /// ============================================================================
    /// Interaction/CharacterPortraitStage.cs가 화면 밖 먼 좌표에 세운 미니 피규어를 전용 카메라로
    /// RenderTexture에 찍고, 여기서는 <see cref="RawImage"/>로 붙이기만 한다. 잉크색/착용 장비/포즈가
    /// 즉시 반영되고, <b>포즈는 프레즌스 문구와 같은 상태 스냅샷에서 파생</b>된다(서 있는 그림 옆에
    /// "넘어져 있는 중"이 적히는 어긋남을 구조적으로 막는다).
    ///
    /// ============================================================================
    /// 진입점 3개 (이번 라운드에서 건드리지 않았다)
    /// ============================================================================
    ///  1. 화면 우상단 톱니 아이콘(주 진입점) — Interaction/InfoGearIconWidget.cs.
    ///  2. 전역 단축키 ⌃⌥⌘I — Interaction/AppControlDirector.cs.
    ///  3. 캐릭터 우클릭 메뉴 [캐릭터 정보] — 같은 파일.
    ///
    /// ============================================================================
    /// 클릭 판정 — TodoPostItWidget과 같은 관례(새 메커니즘을 만들지 않는다)
    /// ============================================================================
    /// (1) uGUI Button + 자체 EventSystem 보강, (2) 창 사각형을 덮는 isTrigger BoxCollider2D,
    /// (3) 전역 폴링 히트테스트. 셋이 같은 핸들러를 부르고 <see cref="ActionDedupSeconds"/>로 중복을
    /// 막는다. <b>창이 닫히면 차단막은 반드시 꺼진다</b>(비침해 원칙 직결).
    /// 보관함 목록은 휠 대신 <b>[▲][▼] 버튼</b>으로 넘긴다 — 이 앱의 uGUI 입력은 "창을 클릭해 앱이
    /// 활성화된 상태"에서만 들어오므로 휠에만 기대면 못 넘기는 사용자가 생긴다(디자이너 지적).
    ///
    /// ============================================================================
    /// 매 프레임 할당 금지 (24시간 상주 앱)
    /// ============================================================================
    /// 닫혀 있으면 Update()는 첫 줄에서 돌아간다. 프레즌스 라인은 <b>상태가 바뀐 프레임에만</b>
    /// 문자열을 만들고, 나머지 수치는 <see cref="SlowRefreshInterval"/> 주기로만 다시 만든다.
    /// 초상화 카메라는 창이 열려 있는 동안만 돈다.
    /// </summary>
    public sealed class CharacterInfoWindow : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        private const int SortingOrderTopMost = 31000; // 포스트잇(30000)보다 위, 앱 제어 메뉴(32760)보다 아래.

        // ---- 창 골격(캔버스 유닛 == OS 포인트) ----
        private const float PanelWidth = 680f;
        private const float PanelHeight = 520f;
        private const float TitleHeight = 40f;
        private const float PanelMarginRight = 16f;
        private const float PanelMarginTop = 84f;   // 톱니 아이콘 바로 아래.
        private const float PanelMarginBottom = 16f;

        // ---- 2단 구성 ----
        private const float LeftWidth = 200f;
        private const float ColumnGap = 20f;
        private const float PortraitHeight = 238f;

        // ---- 우측 패널 ----
        private const float TabStripHeight = 32f;
        private const float StatsBlockHeight = 96f;
        private const float StatRowHeight = 28f;
        private const float EquipCardHeight = 66f;
        private const float BarHeight = 8f;

        // ---- 보관함 목록 ----
        private const float InventoryRowHeight = 24f;
        private const float InventoryRowGap = 3f;
        private const int InventoryVisibleRows = 8;
        private const float InventoryRailWidth = 24f;
        private const float StatusSlotWidth = 96f;   // 훗날 가격표가 들어올 자리(디자이너 확정 최소 폭).
        private const float InventoryDetailHeight = 76f;

        /// <summary>목록 한 줄의 설명 칸에 들어가는 글자 수 상한(한글 기준 실측 — 10pt 폰트에서
        /// 설명 칸 폭이 약 130pt다).</summary>
        private const int InventoryDescriptionChars = 12;

        private const float SlowRefreshInterval = 0.25f;
        private const float ClickPollInterval = 0.05f;
        private const float ActionDedupSeconds = 0.35f;

        private enum Tab { Equipment = 0, Appearance = 1, Inventory = 2 }
        private const int TabCount = 3;
        private const int StatCount = 6;

        private static readonly string[] TabNames = { "장비", "외형", "보관함" };
        private static readonly string[] StatLabels =
        {
            "근속", "함께한 시간", "격파 성공", "대결 승리", "활쏘기 명중", "넘어진 횟수",
        };

        private StickmanAgent _agent;
        private IGlobalPointerButtonService _buttonService;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _panel;
        private BoxCollider2D _clickBlocker;

        private Button _closeButton;
        private RectTransform _closeRect;
        private readonly Image[] _tabChips = new Image[TabCount];
        private readonly Text[] _tabLabels = new Text[TabCount];
        private readonly RectTransform[] _tabRects = new RectTransform[TabCount];
        private readonly GameObject[] _pages = new GameObject[TabCount];

        // 좌측 패널.
        private Text _nameTitle;
        private Text _rankTitle;
        private Image _portraitFrame;
        private Image _portraitBorder;
        private RawImage _portraitImage;
        private Text _portraitFallback;
        private CharacterPortraitStage _stage;
        private Text _presenceText;
        private RectTransform _stressFill;
        private Text _stressValue;
        private RectTransform _xpFill;
        private Text _xpValue;
        private Text _leftNote;

        // [장비] 탭.
        private sealed class EquipCard
        {
            public RectTransform Rect;
            public Image Surface;
            public Image Outline;
            public Text Title;
            public Text Status;
            public EquipmentSlot Slot;
        }
        private readonly EquipCard[] _equipCards = new EquipCard[EquipmentModel.SlotCount];
        private Text _equipDetailName;
        private Text _equipDetailMeta;
        private Text _equipDetailBody;
        private Image _equipActionSurface;
        private Image _equipActionOutline;
        private RectTransform _equipActionRect;
        private Text _equipActionLabel;

        // [외형] 탭.
        private InputField _nameInput;
        private readonly Image[] _inkSurfaces = new Image[2];
        private readonly Image[] _inkOutlines = new Image[2];
        private readonly RectTransform[] _inkRects = new RectTransform[2];
        private readonly Text[] _inkLabels = new Text[2];
        private Text _scaleValue;

        // [보관함] 탭 — 화면에 보이는 행은 고정 개수이고 내용만 갈아 끼운다(가상 목록).
        private sealed class InventoryRowView
        {
            public RectTransform Rect;
            public Image Surface;
            public Image Outline;
            public Image Dot;
            public Text Title;
            public Text Subtitle;
            public Text Description;
            public Text StatusSlot;
            public Text HeaderText;
            public int BoundCatalogIndex; // -1이면 헤더 행(클릭 대상 아님).
        }
        private readonly InventoryRowView[] _inventoryViews = new InventoryRowView[InventoryVisibleRows];
        private RectTransform _pageUpRect;
        private RectTransform _pageDownRect;
        private Text _pageIndicator;
        private Text _inventoryDetailName;
        private Text _inventoryDetailBody;
        private int _inventoryScroll;

        // 하단 스탯 블록.
        private readonly Text[] _statValues = new Text[StatCount];

        private bool _open;
        private Tab _tab = Tab.Equipment;
        private EquipmentSlot _selectedSlot = EquipmentSlot.Head;
        private int _selectedInventoryIndex;
        private float _slowTimer;
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private string _lastActionKey;
        private float _lastActionTime;
        private StickmanStateId _lastShownState = (StickmanStateId)(-1);
        private bool _hasShownState;
        private float _lastDpiScale = -1f;

        public bool IsOpen => _open;

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 라이벌 복제본에서 창이 두 벌 뜨지 않게 하는
            // 2차 방어(1차는 SceneBootstrapper의 컴포넌트 제거). TodoPostItWidget과 같은 관례.
            _agent = GetComponent<StickmanAgent>();
            if (_config == null && _agent != null) _config = _agent.Config;
            BuildUi();
            BuildPortraitStage();
        }

        private void Start()
        {
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<BaseInputModule>() : null;
            Debug.Log("[정보창] 준비 완료(3탭: 장비/외형/보관함) — 여는 방법 3가지: " +
                "(1) **화면 우상단 톱니 아이콘 클릭**(주 진입점), (2) 전역 단축키 **⌃⌥⌘I**, " +
                "(3) 캐릭터 우클릭 -> [캐릭터 정보]. " +
                $"입력 모듈={(module != null ? module.GetType().Name : "★없음(uGUI 클릭 불가)")}, " +
                $"전역 폴링 경로={(_buttonService != null ? "사용 가능" : "미지원 — uGUI 경로만")}, " +
                $"초상화 촬영장={(_stage != null ? "준비됨" : "없음")}.");
        }

        private void OnEnable()
        {
            StickmanEventBus.CharacterProgressionChanged += OnProgressionChanged;
            StickmanEventBus.CharacterEquipmentChanged += OnEquipmentChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.CharacterProgressionChanged -= OnProgressionChanged;
            StickmanEventBus.CharacterEquipmentChanged -= OnEquipmentChanged;
            // 창이 꺼진 채 차단막만 남으면 그 화면 영역이 이유 없이 클릭관통 해제로 남는다(비침해 원칙).
            if (_clickBlocker != null) _clickBlocker.enabled = false;
            if (_stage != null) _stage.SetRenderingEnabled(false);
        }

        private void OnDestroy()
        {
            if (_clickBlocker != null) Destroy(_clickBlocker.gameObject);
            if (_stage != null) Destroy(_stage.gameObject);
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
            _leftInitialized = false; // 창을 여는 그 클릭이 곧바로 행 클릭으로 오인되지 않게.
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_clickBlocker != null) _clickBlocker.enabled = true;
            SyncNameInputFromModel();
            EnsurePortraitTexture(force: true);
            if (_stage != null) _stage.SetRenderingEnabled(true);
            RefreshAll();
            Debug.Log($"[정보창] 열림({source}) — {CharacterProgressionModel.CharacterName} " +
                $"Lv.{CharacterProgressionModel.Level}({RankTitleFor(CharacterProgressionModel.Level)}), " +
                $"탭=[{TabNames[(int)_tab]}], 근속 {CharacterStatsModel.DaysTogether}일차.");
        }

        public void Close(string source)
        {
            if (!_open) return;
            _open = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_clickBlocker != null) _clickBlocker.enabled = false;
            if (_stage != null) _stage.SetRenderingEnabled(false);
            CommitNameInput();
            Debug.Log($"[정보창] 닫힘({source}).");
        }

        // ==================== 루프 ====================

        private void Update()
        {
            if (!_open) return; // 닫혀 있으면 아무 비용도 들이지 않는다.

            ApplyCanvasScaleFactor();
            SyncClickBlocker();
            TickGlobalClickPolling();
            TickPresenceLine();

            _slowTimer += Time.unscaledDeltaTime;
            if (_slowTimer < SlowRefreshInterval) return;
            _slowTimer = 0f;
            RefreshNumbers();
        }

        /// <summary>
        /// 프레즌스 라인 + 초상화 포즈를 <b>같은 상태 스냅샷</b>에서 파생시킨다(그림과 문구가 어긋날
        /// 경우의 수를 없앤다). 상태가 실제로 바뀐 프레임에만 문자열을 만든다.
        /// </summary>
        private void TickPresenceLine()
        {
            if (_presenceText == null) return;

            var machine = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.Machine : null;
            if (machine == null)
            {
                if (!_hasShownState) { _presenceText.text = "지금  ·  —"; _hasShownState = true; }
                return;
            }

            StickmanStateId id = machine.CurrentStateId;
            if (_hasShownState && id == _lastShownState) return;
            _lastShownState = id;
            _hasShownState = true;

            _presenceText.text = $"지금  ·  {StateLabel(id)}";
            if (_stage != null) _stage.SetPose(CharacterPortraitStage.PoseForState(id));
        }

        private void OnProgressionChanged()
        {
            if (!_open) return;
            RefreshNumbers();
        }

        private void OnEquipmentChanged()
        {
            if (!_open) return;
            RefreshEquipmentCards();
            RefreshInventoryList();
        }

        private void RefreshAll()
        {
            _hasShownState = false;
            TickPresenceLine();
            RefreshNumbers();
            RefreshEquipmentCards();
            RefreshInventoryList();
            RefreshAppearance();
            ApplyTabVisibility();
        }

        private void RefreshNumbers()
        {
            if (_nameTitle != null) _nameTitle.text = CharacterProgressionModel.CharacterName;
            if (_rankTitle != null)
            {
                _rankTitle.text = $"Lv.{CharacterProgressionModel.Level}  ·  {RankTitleFor(CharacterProgressionModel.Level)}";
            }

            float stress = StressGauge.CurrentLevel;
            SetBarFill(_stressFill, stress);
            StressMoodTier tier = StressGaugeRenderer.TierForLevel(stress, _config);
            if (_stressValue != null) _stressValue.text = $"{stress * 100f:F0}%  ·  {StressGaugeRenderer.TierLabel(tier)}";

            float need = CharacterProgressionModel.XpToNextLevel(_config);
            float have = CharacterProgressionModel.CurrentXp;
            SetBarFill(_xpFill, need > 0f ? Mathf.Clamp01(have / need) : 0f);
            if (_xpValue != null) _xpValue.text = $"{have:F0} / {need:F0}";

            if (_leftNote != null)
            {
                _leftNote.text =
                    $"누적 경험치 {CharacterProgressionModel.TotalXpEarned:F0}  ·  착용 {CountEquipped()}/{EquipmentModel.SlotCount}\n" +
                    "기록은 앱 전용 데이터 폴더에 자동 저장돼요.";
            }

            // 하단 스탯 6칸. 0인 항목은 숫자 대신 회색 "아직 없음"으로 — 0이 성취처럼 보이지 않게 한다.
            SetStat(0, $"{CharacterStatsModel.DaysTogether}일차", true);
            SetStat(1, CharacterStatsModel.FormatCompanionTime(), true);
            SetStat(2, CharacterStatsModel.BattleWins > 0 ? $"{CharacterStatsModel.BattleWins}번" : null, CharacterStatsModel.BattleWins > 0);
            SetStat(3, CharacterStatsModel.RivalWins > 0 ? $"{CharacterStatsModel.RivalWins}승" : null, CharacterStatsModel.RivalWins > 0);
            SetStat(4, CharacterStatsModel.TryGetArcheryAccuracy01(out float acc)
                ? $"{CharacterStatsModel.ArcheryBullseyes} / {CharacterStatsModel.ArcheryShots} ({acc * 100f:F0}%)"
                : "기록 없음", CharacterStatsModel.ArcheryShots > 0);
            SetStat(5, CharacterStatsModel.RagdollFalls > 0 ? $"{CharacterStatsModel.RagdollFalls}번" : null, CharacterStatsModel.RagdollFalls > 0);

            RefreshEquipDetail();
        }

        /// <summary>스탯 한 칸. <paramref name="value"/>가 null이면 회색 "아직 없음"으로 대신한다.</summary>
        private void SetStat(int index, string value, bool hasRecord)
        {
            if (index < 0 || index >= _statValues.Length || _statValues[index] == null) return;
            _statValues[index].text = value ?? "아직 없음";
            _statValues[index].color = hasRecord ? UiChrome.TextPrimary : UiChrome.TextTertiary;
        }

        private static int CountEquipped()
        {
            int n = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                if (EquipmentModel.IsEquipped((EquipmentSlot)i)) n++;
            }
            return n;
        }

        /// <summary>레벨 -> 칭호. 새 시스템이 아니라 <b>표시용 매핑 하나</b>다(리더 확정 — 과설계 금지).</summary>
        public static string RankTitleFor(int level)
        {
            if (level <= 2) return "갓 들어온 동료";
            if (level <= 4) return "적응 중인 동료";
            if (level <= 6) return "믿음직한 동료";
            if (level <= 9) return "없으면 허전한 동료";
            if (level <= 14) return "이 화면의 터줏대감";
            return "사실상 이 화면 주인";
        }

        private void RefreshEquipmentCards()
        {
            for (int i = 0; i < _equipCards.Length; i++)
            {
                EquipCard card = _equipCards[i];
                if (card == null) continue;

                EquipmentSlot slot = card.Slot;
                bool unlocked = EquipmentModel.IsUnlocked(slot, _config);
                bool equipped = unlocked && EquipmentModel.IsEquipped(slot);
                bool selected = slot == _selectedSlot;
                int needLevel = EquipmentModel.UnlockLevel(slot, _config);

                card.Title.text = EquipmentModel.ItemName(slot);
                // ★ 이모지(🔒)를 쓰지 않는다 — 내장 LegacyRuntime.ttf에 글리프가 없어 화면에서 빈칸이
                //   된다(직전 라운드 육안 검증에서 확인). ●/○는 이 폰트에 있어 그대로 쓴다.
                card.Status.text = !unlocked
                    ? $"{EquipmentModel.SlotName(slot)}  ·  Lv.{needLevel}에 열림"
                    : equipped ? $"{EquipmentModel.SlotName(slot)}  ·  ● 착용 중"
                               : $"{EquipmentModel.SlotName(slot)}  ·  ○ 미착용";

                card.Surface.color = selected ? UiChrome.AccentSurface
                    : unlocked ? UiChrome.CardSurface : UiChrome.CardSurfaceMuted;
                card.Outline.color = selected ? UiChrome.AccentBorder : UiChrome.CardBorder;
                card.Title.color = unlocked ? UiChrome.TextPrimary : UiChrome.TextTertiary;
                card.Status.color = unlocked ? UiChrome.TextSecondary : UiChrome.TextTertiary;
            }

            RefreshEquipDetail();
        }

        /// <summary>선택한 슬롯의 설명 카드. <b>잠긴 슬롯도 회색으로 계속 보여준다</b>(빈 카드로 가리면
        /// 다음 레벨의 동기부여가 사라진다 — 디자이너 지적). 문구는 전부 <see cref="ItemCatalog"/>에서
        /// 오므로 장비 탭과 보관함 탭이 다른 문장을 보여줄 수 있는 경로가 없다.</summary>
        private void RefreshEquipDetail()
        {
            ItemCatalogEntry entry = ItemCatalog.FindBySlot(_selectedSlot);
            if (entry == null) return;

            bool unlocked = EquipmentModel.IsUnlocked(_selectedSlot, _config);
            bool equipped = unlocked && EquipmentModel.IsEquipped(_selectedSlot);
            int needLevel = EquipmentModel.UnlockLevel(_selectedSlot, _config);

            if (_equipDetailName != null)
            {
                _equipDetailName.text = entry.DisplayName;
                _equipDetailName.color = unlocked ? UiChrome.TextPrimary : UiChrome.TextTertiary;
            }
            if (_equipDetailMeta != null)
            {
                _equipDetailMeta.text = unlocked
                    ? $"{entry.CategoryLabel}  ·  {(equipped ? "착용 중" : "보유")}"
                    : $"{entry.CategoryLabel}  ·  Lv.{needLevel}부터 착용할 수 있어요 (지금 Lv.{CharacterProgressionModel.Level})";
            }
            if (_equipDetailBody != null)
            {
                _equipDetailBody.text = entry.Description;
                _equipDetailBody.color = unlocked ? UiChrome.TextSecondary : UiChrome.TextTertiary;
            }

            if (_equipActionLabel != null)
            {
                _equipActionLabel.text = !unlocked ? $"Lv.{needLevel}에 열림" : equipped ? "벗기" : "착용하기";
                _equipActionLabel.color = !unlocked ? UiChrome.TextTertiary
                    : equipped ? UiChrome.TextSecondary : UiChrome.TextOnAccent;
            }
            if (_equipActionSurface != null)
            {
                _equipActionSurface.color = !unlocked ? UiChrome.CardSurfaceMuted
                    : equipped ? UiChrome.CardSurface : UiChrome.AccentSurface;
            }
            if (_equipActionOutline != null)
            {
                _equipActionOutline.color = unlocked && !equipped ? UiChrome.AccentBorder : UiChrome.CardBorder;
            }
        }

        // ==================== 보관함(가상 목록) ====================

        /// <summary>목록의 논리적 줄 수 = 헤더 2줄 + 카탈로그 전체.</summary>
        private static int InventoryLineCount => ItemCatalog.Count + 2;

        /// <summary>논리적 줄 번호 -> 카탈로그 인덱스. 헤더면 -1.
        /// 순서: [걸치는 것] 헤더 → 장비 4종(해제 레벨 순) → [할 줄 아는 것] 헤더 → 행동 13종.
        /// 카탈로그가 이미 그 순서로 정의되어 있어 재정렬하지 않는다(정렬 규칙이 두 곳에 생기지 않게).</summary>
        private static int CatalogIndexForLine(int line)
        {
            int equipmentCount = ItemCatalog.EquipmentCount;
            if (line <= 0) return -1;                              // "걸치는 것" 헤더
            if (line <= equipmentCount) return line - 1;           // 장비
            if (line == equipmentCount + 1) return -1;             // "할 줄 아는 것" 헤더
            return line - 2;                                       // 행동
        }

        private string HeaderTextForLine(int line)
        {
            if (line == 0)
            {
                return $"걸치는 것  ({ItemCatalog.UnlockedEquipmentCount(_config)}/{ItemCatalog.EquipmentCount})";
            }
            return $"할 줄 아는 것  ({ItemCatalog.ActionCount})";
        }

        private int MaxInventoryScroll => Mathf.Max(0, InventoryLineCount - InventoryVisibleRows);

        private void RefreshInventoryList()
        {
            _inventoryScroll = Mathf.Clamp(_inventoryScroll, 0, MaxInventoryScroll);

            for (int i = 0; i < _inventoryViews.Length; i++)
            {
                InventoryRowView view = _inventoryViews[i];
                if (view == null) continue;

                int line = _inventoryScroll + i;
                if (line >= InventoryLineCount)
                {
                    view.Rect.gameObject.SetActive(false);
                    continue;
                }
                view.Rect.gameObject.SetActive(true);

                int catalogIndex = CatalogIndexForLine(line);
                view.BoundCatalogIndex = catalogIndex;

                if (catalogIndex < 0)
                {
                    // 헤더 줄 — 표면을 지우고 제목만 남긴다.
                    view.Surface.color = new Color(0f, 0f, 0f, 0f);
                    view.Outline.color = new Color(0f, 0f, 0f, 0f);
                    view.Dot.color = new Color(0f, 0f, 0f, 0f);
                    view.Title.text = string.Empty;
                    view.Subtitle.text = string.Empty;
                    view.Description.text = string.Empty;
                    view.StatusSlot.text = string.Empty;
                    view.HeaderText.text = HeaderTextForLine(line);
                    continue;
                }

                ItemCatalogEntry entry = ItemCatalog.At(catalogIndex);
                if (entry == null) continue;

                bool owned = entry.IsOwned(_config);
                bool selected = catalogIndex == _selectedInventoryIndex;
                bool equipped = entry.IsEquipped();

                view.HeaderText.text = string.Empty;
                view.Title.text = entry.DisplayName;
                view.Subtitle.text = entry.CategoryLabel;
                view.Description.text = Ellipsize(entry.ShortDescription, InventoryDescriptionChars);
                view.StatusSlot.text = entry.ResolveStatusSlot(_config);

                view.Surface.color = selected ? UiChrome.AccentSurface
                    : owned ? UiChrome.CardSurface : UiChrome.CardSurfaceMuted;
                view.Outline.color = selected ? UiChrome.AccentBorder : UiChrome.CardBorder;
                view.Dot.color = equipped ? UiChrome.Accent : owned ? UiChrome.TextTertiary : UiChrome.TrackBackground;
                view.Title.color = owned ? UiChrome.TextPrimary : UiChrome.TextTertiary;
                view.Subtitle.color = UiChrome.TextTertiary;
                view.Description.color = owned ? UiChrome.TextSecondary : UiChrome.TextTertiary;
                view.StatusSlot.color = equipped ? UiChrome.TextOnAccent
                    : owned ? UiChrome.TextSecondary : UiChrome.TextTertiary;
            }

            if (_pageIndicator != null)
            {
                // 마지막 페이지는 스크롤이 상한에 걸려 한 페이지 분량이 채 안 되므로 올림으로 센다
                // (나눗셈으로만 세면 마지막 페이지에서 "2/3"처럼 어긋난다 — 육안 검증에서 확인).
                int page = Mathf.CeilToInt(_inventoryScroll / (float)InventoryVisibleRows) + 1;
                int pages = Mathf.Max(1, Mathf.CeilToInt((float)InventoryLineCount / InventoryVisibleRows));
                _pageIndicator.text = $"{page}\n/\n{pages}";
            }

            RefreshInventoryDetail();
        }

        /// <summary>목록 한 줄에 들어갈 길이로 자른다. 자동 줄바꿈에 맡기면 두 번째 줄이 행 높이에
        /// 걸려 <b>반쯤 잘린 글자</b>가 남는다(첫 육안 검증에서 실제로 그랬다) — 잘렸다는 사실을
        /// 말줄임표로 <b>드러내는</b> 편이 정직하고 깔끔하다. 전문은 아래 상세 카드가 보여준다.</summary>
        private static string Ellipsize(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
            return text.Substring(0, maxChars).TrimEnd() + "...";
        }

        private void RefreshInventoryDetail()
        {
            ItemCatalogEntry entry = ItemCatalog.At(_selectedInventoryIndex);
            if (entry == null) return;

            if (_inventoryDetailName != null)
            {
                _inventoryDetailName.text =
                    $"{entry.DisplayName}   ·   {entry.CategoryLabel}   ·   {entry.ResolveStatusSlot(_config)}";
            }
            if (_inventoryDetailBody != null) _inventoryDetailBody.text = entry.Description;
        }

        private void ScrollInventory(int delta)
        {
            int next = Mathf.Clamp(_inventoryScroll + delta * InventoryVisibleRows, 0, MaxInventoryScroll);
            if (next == _inventoryScroll) return;
            _inventoryScroll = next;
            RefreshInventoryList();
        }

        private void RefreshAppearance()
        {
            bool white = _config != null && _config.inkColor == StickmanInkColor.White;
            for (int i = 0; i < _inkSurfaces.Length; i++)
            {
                bool active = (i == 1) == white;
                if (_inkSurfaces[i] != null) _inkSurfaces[i].color = active ? UiChrome.AccentSurface : UiChrome.CardSurface;
                if (_inkOutlines[i] != null) _inkOutlines[i].color = active ? UiChrome.AccentBorder : UiChrome.CardBorder;
                if (_inkLabels[i] != null)
                {
                    _inkLabels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                    _inkLabels[i].color = active ? UiChrome.TextOnAccent : UiChrome.TextSecondary;
                }
            }

            if (_scaleValue != null)
            {
                float scale = _config != null ? _config.ResolveCharacterScale() : 1f;
                _scaleValue.text = $"{scale:F2} 배";
            }
        }

        private static void SetBarFill(RectTransform fill, float progress01)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
        }

        // ==================== 조작 ====================

        private void OnTabClicked(Tab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            ApplyTabVisibility();
            Debug.Log($"[정보창] 탭 전환 -> [{TabNames[(int)tab]}].");
        }

        private void ApplyTabVisibility()
        {
            for (int i = 0; i < _pages.Length; i++)
            {
                if (_pages[i] != null) _pages[i].SetActive(i == (int)_tab);
            }
            for (int i = 0; i < _tabLabels.Length; i++)
            {
                bool active = i == (int)_tab;
                if (_tabChips[i] != null) _tabChips[i].color = active ? UiChrome.CardSurface : new Color(1f, 1f, 1f, 0f);
                if (_tabLabels[i] == null) continue;
                _tabLabels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                _tabLabels[i].color = active ? UiChrome.TextPrimary : UiChrome.TextSecondary;
            }
        }

        /// <summary>슬롯 카드 클릭 = <b>선택</b>. 착용/해제는 설명 카드의 버튼 하나로만 한다 —
        /// "고른다"와 "입는다"를 같은 클릭에 겹치면, 설명을 읽으려고 눌렀을 뿐인데 옷이 벗겨진다.</summary>
        private void OnEquipCardClicked(EquipmentSlot slot)
        {
            if (_selectedSlot == slot) return;
            _selectedSlot = slot;
            RefreshEquipmentCards();
            Debug.Log($"[장비] 선택 -> {EquipmentModel.ItemName(slot)}({EquipmentModel.SlotName(slot)}).");
        }

        private void OnEquipActionClicked()
        {
            bool unlocked = EquipmentModel.IsUnlocked(_selectedSlot, _config);
            if (!unlocked)
            {
                Debug.Log($"[장비] {EquipmentModel.ItemName(_selectedSlot)}은(는) 아직 잠겨 있습니다 — " +
                    $"Lv.{EquipmentModel.UnlockLevel(_selectedSlot, _config)}에서 해제됩니다" +
                    $"(현재 Lv.{CharacterProgressionModel.Level}).");
                return;
            }

            if (!EquipmentModel.TryToggle(_selectedSlot, _config)) return;
            Debug.Log($"[장비] {EquipmentModel.ItemName(_selectedSlot)} " +
                $"{(EquipmentModel.IsEquipped(_selectedSlot) ? "착용" : "해제")} — 초상화와 캐릭터에 즉시 반영, 즉시 저장.");
            CharacterSaveStore.Save(); // "모든 토글은 즉시 반영(별도 저장 버튼 없음)".
            RefreshEquipmentCards();
            RefreshInventoryList();
        }

        private void OnInventoryRowClicked(int catalogIndex)
        {
            if (catalogIndex < 0 || _selectedInventoryIndex == catalogIndex) return;
            _selectedInventoryIndex = catalogIndex;
            RefreshInventoryList();
            ItemCatalogEntry entry = ItemCatalog.At(catalogIndex);
            if (entry != null) Debug.Log($"[보관함] 선택 -> {entry.DisplayName}({entry.CategoryLabel}).");
        }

        /// <summary>잉크색 전환 — 우클릭 메뉴 [잉크색]과 <b>같은 경로</b>를 쓴다
        /// (AppControlDirector.MenuAction.InkColor: config 값 변경 + StickmanAgent에 일괄 적용).
        /// 액세서리 선은 CharacterAccessoryRenderer가 색을 서명에 넣어 다음 프레임에 따라오고,
        /// 초상화는 촬영장이 배경색과 선 색을 함께 뒤집는다.</summary>
        private void OnInkButtonClicked(bool white)
        {
            if (_config == null) return;
            StickmanInkColor next = white ? StickmanInkColor.White : StickmanInkColor.Black;
            if (_config.inkColor == next) return;

            _config.inkColor = next;
            if (_agent != null) _agent.ApplyInkColorFromConfig();
            RefreshAppearance();
            ApplyPortraitTheme();
            Debug.Log($"[정보창] 잉크색 전환 -> {next} (초상화/캐릭터/액세서리에 즉시 반영).");
        }

        private void ApplyPortraitTheme()
        {
            if (_stage != null) _stage.RefreshTheme();
            bool whiteInk = _config != null && _config.inkColor == StickmanInkColor.White;
            if (_portraitFrame != null) _portraitFrame.color = CharacterPortraitStage.ResolveBackdropColor(_config);
            if (_portraitBorder != null)
            {
                _portraitBorder.color = whiteInk ? new Color(1f, 1f, 1f, 0.18f) : UiChrome.CardBorder;
            }
        }

        private void SyncNameInputFromModel()
        {
            if (_nameInput == null) return;
            if (_nameInput.text != CharacterProgressionModel.CharacterName)
            {
                _nameInput.text = CharacterProgressionModel.CharacterName;
            }
        }

        private void CommitNameInput()
        {
            if (_nameInput == null) return;
            if (_nameInput.text == CharacterProgressionModel.CharacterName) return;
            CharacterProgressionModel.SetCharacterName(_nameInput.text);
            SyncNameInputFromModel();
            CharacterSaveStore.Save();
            Debug.Log($"[정보창] 이름 변경 -> \"{CharacterProgressionModel.CharacterName}\".");
        }

        // ==================== 클릭 경로 3: 전역 폴링 ====================

        private void TickGlobalClickPolling()
        {
            if (_buttonService == null || _panel == null) return;

            _clickPollTimer += Time.unscaledDeltaTime;
            if (_clickPollTimer < ClickPollInterval) return;
            _clickPollTimer = 0f;

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            if (!_leftInitialized) { _leftInitialized = true; _leftPrev = left; return; }
            bool rising = left && !_leftPrev;
            _leftPrev = left;
            if (!rising) return;

            if (_agent == null || !_agent.TryGetCursorPosition(out Vector2 osScreen)) return;
            Vector2 cursor = ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _config);

            if (ContainsScreenPoint(_closeRect, cursor))
            {
                if (TryClaimAction("close")) Close("[X] 클릭");
                return;
            }

            for (int i = 0; i < _tabRects.Length; i++)
            {
                if (!ContainsScreenPoint(_tabRects[i], cursor)) continue;
                if (TryClaimAction("tab" + i)) OnTabClicked((Tab)i);
                return;
            }

            switch (_tab)
            {
                case Tab.Equipment:
                    for (int i = 0; i < _equipCards.Length; i++)
                    {
                        EquipCard card = _equipCards[i];
                        if (card == null || !ContainsScreenPoint(card.Rect, cursor)) continue;
                        if (TryClaimAction("equip" + i)) OnEquipCardClicked(card.Slot);
                        return;
                    }
                    if (ContainsScreenPoint(_equipActionRect, cursor))
                    {
                        if (TryClaimAction("equipAction")) OnEquipActionClicked();
                    }
                    return;

                case Tab.Appearance:
                    for (int i = 0; i < _inkRects.Length; i++)
                    {
                        if (!ContainsScreenPoint(_inkRects[i], cursor)) continue;
                        if (TryClaimAction("ink" + i)) OnInkButtonClicked(i == 1);
                        return;
                    }
                    return;

                case Tab.Inventory:
                    if (ContainsScreenPoint(_pageUpRect, cursor))
                    {
                        if (TryClaimAction("pageUp")) ScrollInventory(-1);
                        return;
                    }
                    if (ContainsScreenPoint(_pageDownRect, cursor))
                    {
                        if (TryClaimAction("pageDown")) ScrollInventory(1);
                        return;
                    }
                    for (int i = 0; i < _inventoryViews.Length; i++)
                    {
                        InventoryRowView view = _inventoryViews[i];
                        if (view == null || view.BoundCatalogIndex < 0) continue;
                        if (!ContainsScreenPoint(view.Rect, cursor)) continue;
                        if (TryClaimAction("inv" + i)) OnInventoryRowClicked(view.BoundCatalogIndex);
                        return;
                    }
                    return;
            }
        }

        private bool TryClaimAction(string key)
        {
            if (_lastActionKey == key && Time.unscaledTime - _lastActionTime < ActionDedupSeconds) return false;
            _lastActionKey = key;
            _lastActionTime = Time.unscaledTime;
            return true;
        }

        /// <summary>ScreenSpaceOverlay 캔버스에서는 RectTransform의 월드 좌표가 곧 스크린 픽셀 좌표다
        /// (AppControlDirector.HitTestMenuRow / TodoPostItWidget과 같은 전제).</summary>
        private static bool ContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return screenPoint.x >= corners[0].x && screenPoint.x <= corners[2].x &&
                   screenPoint.y >= corners[0].y && screenPoint.y <= corners[2].y;
        }

        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
            ClampPanelToScreen(target);
            EnsurePortraitTexture(force: false);
        }

        /// <summary>작은 화면에서 창 아래쪽이 잘리지 않게 높이를 화면에 맞춘다(리더 지시 4항).
        /// 폭은 건드리지 않는다 — 세로가 먼저 부족해지고, 폭까지 줄이면 3열 목록이 깨진다.</summary>
        private void ClampPanelToScreen(float scaleFactor)
        {
            if (_panel == null || scaleFactor <= 0f) return;
            float availableCanvasHeight = Screen.height / scaleFactor - PanelMarginTop - PanelMarginBottom;
            float height = Mathf.Min(PanelHeight, Mathf.Max(240f, availableCanvasHeight));
            if (Mathf.Approximately(_panel.sizeDelta.y, height)) return;
            _panel.sizeDelta = new Vector2(PanelWidth, height);
        }

        /// <summary>창이 보이는 동안만 창 사각형을 덮는 히트테스트용 콜라이더를 켠다(TodoPostItWidget과
        /// 같은 관례 — isTrigger라 캐릭터 물리에는 전혀 관여하지 않는다).</summary>
        private void SyncClickBlocker()
        {
            if (_clickBlocker == null || _panel == null) return;
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : Camera.main;
            if (cam == null) { _clickBlocker.enabled = false; return; }

            var corners = new Vector3[4];
            _panel.GetWorldCorners(corners);
            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(corners[0].x, corners[0].y, depth));
            Vector3 tr = cam.ScreenToWorldPoint(new Vector3(corners[2].x, corners[2].y, depth));

            _clickBlocker.enabled = true;
            _clickBlocker.transform.position = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, 0f);
            _clickBlocker.size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
        }

        // ==================== 초상화 ====================

        private void BuildPortraitStage()
        {
            Material lineMaterial = null;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            if (source != null) lineMaterial = source.sharedMaterial;

            _stage = CharacterPortraitStage.Create(_config, StickmanMetrics.Find(this), lineMaterial);
        }

        /// <summary>표시 크기와 화면 배율로 RT를 준비한다. 실패하면 검은 상자 대신 안내 문구를 띄운다.</summary>
        private void EnsurePortraitTexture(bool force)
        {
            if (_stage == null || _portraitImage == null) return;

            float dpi = ScreenCoordinateConverter.ResolveDpiScale(_config);
            if (!force && Mathf.Approximately(dpi, _lastDpiScale) && _stage.HasTexture) return;
            _lastDpiScale = dpi;

            Rect rect = _portraitImage.rectTransform.rect;
            float w = rect.width > 1f ? rect.width : LeftWidth - UiChrome.Space3 * 2f;
            float h = rect.height > 1f ? rect.height : PortraitHeight - UiChrome.Space3 * 2f;

            bool ok = _stage.TryEnsureTexture(w, h, dpi);
            _portraitImage.enabled = ok;
            if (ok) _portraitImage.texture = _stage.Texture;
            if (_portraitFallback != null) _portraitFallback.gameObject.SetActive(!ok);
        }

        // ==================== UI 구성(런타임 생성 — 씬/프리팹 수동 배선 없이도 동작) ====================

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("CharacterInfoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrderTopMost;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();

            Image panelImage = UiChrome.AddSurface(canvasGo.transform, "InfoPanel", UiChrome.PanelSurface, UiChrome.RadiusPanel);
            _panel = panelImage.rectTransform;
            _panel.anchorMin = new Vector2(1f, 1f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 1f);
            _panel.anchoredPosition = new Vector2(-PanelMarginRight, -PanelMarginTop);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            // 그림자를 패널의 첫 자식으로 넣어 패널 그림 뒤에 깔리게 한다(아주 옅게 — 리더 지시).
            Image shadow = UiChrome.AddShadow(_panel, "PanelShadow", UiChrome.RadiusPanel, 5f, new Vector2(0f, -3f));
            shadow.transform.SetAsFirstSibling();
            UiChrome.AddOutline(_panel, "PanelOutline", UiChrome.PanelBorder, UiChrome.RadiusPanel);

            BuildTitleBar(_panel);

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(_panel, false);
            var body = bodyGo.GetComponent<RectTransform>();
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(UiChrome.Space4, UiChrome.Space4);
            body.offsetMax = new Vector2(-UiChrome.Space4, -(TitleHeight + UiChrome.Space2));

            BuildLeftColumn(body);
            RectTransform right = BuildRightColumn(body);
            BuildTabs(right);
            BuildEquipmentPage(right);
            BuildAppearancePage(right);
            BuildInventoryPage(right);
            BuildStatsBlock(right);
            ApplyTabVisibility();

            // 클릭관통 차단막 — 씬 루트에 둔다(캐릭터의 자식으로 두면 캐릭터가 걷거나 랙돌로 회전할 때
            // 이 사각형까지 함께 돌아가 창의 화면 사각형과 어긋난다. TodoPostItWidget과 같은 이유).
            var blockerGo = new GameObject("CharacterInfoClickBlocker");
            _clickBlocker = blockerGo.AddComponent<BoxCollider2D>();
            _clickBlocker.isTrigger = true;
            _clickBlocker.enabled = false;

            canvasGo.SetActive(false);
        }

        private void BuildTitleBar(Transform parent)
        {
            var barGo = new GameObject("TitleBar", typeof(RectTransform));
            barGo.transform.SetParent(parent, false);
            var rt = barGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -TitleHeight);
            rt.offsetMax = Vector2.zero;

            Text title = UiChrome.AddText(barGo.transform, "Title", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, bold: true);
            UiChrome.Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(UiChrome.Space4, 0f);
            title.rectTransform.offsetMax = new Vector2(-TitleHeight, 0f);
            title.text = "내 책상 동료";

            Image divider = UiChrome.AddSurface(barGo.transform, "TitleDivider", UiChrome.Divider, 2);
            var drt = divider.rectTransform;
            drt.anchorMin = new Vector2(0f, 0f);
            drt.anchorMax = new Vector2(1f, 0f);
            drt.pivot = new Vector2(0.5f, 0f);
            drt.offsetMin = new Vector2(UiChrome.Space4, 0f);
            drt.offsetMax = new Vector2(-UiChrome.Space4, 1f);
            divider.raycastTarget = false;

            Image closeSurface = UiChrome.AddSurface(barGo.transform, "CloseButton", UiChrome.CardSurface, UiChrome.RadiusChip);
            _closeRect = closeSurface.rectTransform;
            _closeRect.anchorMin = new Vector2(1f, 0.5f);
            _closeRect.anchorMax = new Vector2(1f, 0.5f);
            _closeRect.pivot = new Vector2(1f, 0.5f);
            _closeRect.anchoredPosition = new Vector2(-UiChrome.Space3, 0f);
            _closeRect.sizeDelta = new Vector2(26f, 26f);
            UiChrome.AddOutline(_closeRect, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
            Text closeLabel = UiChrome.AddText(_closeRect, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(closeLabel.rectTransform);
            closeLabel.text = "✕";

            _closeButton = closeSurface.gameObject.AddComponent<Button>();
            _closeButton.targetGraphic = closeSurface;
            _closeButton.onClick.AddListener(() => { if (TryClaimAction("close")) Close("[X] 클릭"); });
        }

        // -------------------- 좌측 고정 패널 --------------------

        private void BuildLeftColumn(RectTransform body)
        {
            var go = new GameObject("LeftColumn", typeof(RectTransform));
            go.transform.SetParent(body, false);
            var left = go.GetComponent<RectTransform>();
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.sizeDelta = new Vector2(LeftWidth, 0f);
            left.anchoredPosition = Vector2.zero;

            _nameTitle = UiChrome.AddText(left, "Name", UiChrome.FontDisplay, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, bold: true);
            UiChrome.PlaceTopLeft(_nameTitle.rectTransform, 0f, 0f, LeftWidth, 32f);

            _rankTitle = UiChrome.AddText(left, "RankTitle", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(_rankTitle.rectTransform, 0f, -34f, LeftWidth, 18f);

            // 초상화 액자 — 바탕 + 1px 테두리 + RenderTexture. 캐릭터 그림 자체는 단순한 채로 둔다.
            _portraitFrame = UiChrome.AddSurface(left, "PortraitFrame",
                CharacterPortraitStage.ResolveBackdropColor(_config), UiChrome.RadiusPanel);
            UiChrome.PlaceTopLeft(_portraitFrame.rectTransform, 0f, -58f, LeftWidth, PortraitHeight);
            _portraitFrame.raycastTarget = false;
            _portraitBorder = UiChrome.AddOutline(_portraitFrame.rectTransform, "Border", UiChrome.CardBorder, UiChrome.RadiusPanel);

            var imageGo = new GameObject("PortraitImage", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(_portraitFrame.transform, false);
            UiChrome.Stretch(imageGo.GetComponent<RectTransform>(), UiChrome.Space3);
            _portraitImage = imageGo.GetComponent<RawImage>();
            _portraitImage.raycastTarget = false;
            _portraitImage.enabled = false;   // RT가 준비되면 켠다.

            _portraitFallback = UiChrome.AddText(_portraitFrame.rectTransform, "PortraitFallback",
                UiChrome.FontLabel, TextAnchor.MiddleCenter, UiChrome.TextTertiary, wrap: true);
            UiChrome.Stretch(_portraitFallback.rectTransform, UiChrome.Space4);
            _portraitFallback.text = "미리보기를 그릴 수 없어요";
            _portraitFallback.gameObject.SetActive(false);

            // 프레즌스 라인 — "이건 프로필 사진, 저건 실시간 상태"가 헷갈리지 않게 액자 <b>밖</b>에 둔다.
            _presenceText = UiChrome.AddText(left, "Presence", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(_presenceText.rectTransform, 0f, -300f, LeftWidth, 22f);

            _stressFill = BuildLabeledBar(left, "스트레스", -332f, UiChrome.WarmAccent, out _stressValue);
            _xpFill = BuildLabeledBar(left, "EXP", -374f, UiChrome.Accent, out _xpValue);

            _leftNote = UiChrome.AddText(left, "LeftNote", UiChrome.FontCaption, TextAnchor.UpperLeft,
                UiChrome.TextTertiary, wrap: true);
            UiChrome.PlaceTopLeft(_leftNote.rectTransform, 0f, -412f, LeftWidth, 44f);
        }

        /// <summary>라벨 한 줄 + 값(우측 정렬) + 그 아래 둥근 막대. 반환값은 채움 RectTransform.</summary>
        private RectTransform BuildLabeledBar(RectTransform parent, string label, float y, Color fillColor, out Text valueText)
        {
            Text l = UiChrome.AddText(parent, "BarLabel_" + label, UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(l.rectTransform, 0f, y, 100f, 16f);
            l.text = label;

            valueText = UiChrome.AddText(parent, "BarValue_" + label, UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(valueText.rectTransform, LeftWidth - 140f, y, 140f, 16f);

            Image track = UiChrome.AddSurface(parent, "BarTrack_" + label, UiChrome.TrackBackground, 4);
            UiChrome.PlaceTopLeft(track.rectTransform, 0f, y - 18f, LeftWidth, BarHeight);
            track.raycastTarget = false;

            Image fill = UiChrome.AddSurface(track.rectTransform, "Fill", fillColor, 4);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            fill.raycastTarget = false;
            return frt;
        }

        // -------------------- 우측 탭 패널 --------------------

        private RectTransform BuildRightColumn(RectTransform body)
        {
            var go = new GameObject("RightColumn", typeof(RectTransform));
            go.transform.SetParent(body, false);
            var right = go.GetComponent<RectTransform>();
            right.anchorMin = Vector2.zero;
            right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(LeftWidth + ColumnGap, 0f);
            right.offsetMax = Vector2.zero;
            return right;
        }

        /// <summary>세그먼트 컨트롤 형태의 탭 — 트랙 하나 위에 칩 3개(선택된 칩만 흰 표면).</summary>
        private void BuildTabs(RectTransform right)
        {
            Image trackImage = UiChrome.AddSurface(right, "TabStrip", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            UiChrome.PlaceTopLeft(trackImage.rectTransform, 0f, 0f, RightWidth(), TabStripHeight);
            trackImage.raycastTarget = false;

            float inner = UiChrome.Space1;
            float chipWidth = (RightWidth() - inner * 2f - UiChrome.Space1 * (TabCount - 1)) / TabCount;

            for (int i = 0; i < TabCount; i++)
            {
                Image chip = UiChrome.AddSurface(trackImage.rectTransform, "Tab" + TabNames[i], UiChrome.CardSurface, UiChrome.RadiusChip);
                var rt = chip.rectTransform;
                UiChrome.PlaceTopLeft(rt, inner + i * (chipWidth + UiChrome.Space1), -inner,
                    chipWidth, TabStripHeight - inner * 2f);

                Text label = UiChrome.AddText(rt, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter, UiChrome.TextSecondary);
                UiChrome.Stretch(label.rectTransform);
                label.text = TabNames[i];

                var button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = chip;
                int captured = i;
                button.onClick.AddListener(() => { if (TryClaimAction("tab" + captured)) OnTabClicked((Tab)captured); });

                _tabChips[i] = chip;
                _tabRects[i] = rt;
                _tabLabels[i] = label;
            }
        }

        /// <summary>탭 페이지 공통 틀 — 탭 줄 아래부터 스탯 블록 위까지.</summary>
        private RectTransform CreatePage(RectTransform right, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(right, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, StatsBlockHeight + UiChrome.Space4);
            rt.offsetMax = new Vector2(0f, -(TabStripHeight + UiChrome.Space2));
            return rt;
        }

        private void BuildEquipmentPage(RectTransform right)
        {
            RectTransform page = CreatePage(right, "EquipmentPage");
            _pages[(int)Tab.Equipment] = page.gameObject;

            float cardWidth = (RightWidth() - UiChrome.Space3) * 0.5f;

            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                int col = i % 2, row = i / 2;

                Image surface = UiChrome.AddSurface(page, "EquipCard" + i, UiChrome.CardSurface, UiChrome.RadiusCard);
                var rt = surface.rectTransform;
                UiChrome.PlaceTopLeft(rt, col * (cardWidth + UiChrome.Space3),
                    -row * (EquipCardHeight + UiChrome.Space3), cardWidth, EquipCardHeight);
                Image outline = UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

                Text title = UiChrome.AddText(rt, "Title", UiChrome.FontTitle, TextAnchor.UpperLeft, UiChrome.TextPrimary, bold: true);
                UiChrome.PlaceTopLeft(title.rectTransform, UiChrome.Space3, -UiChrome.Space3, cardWidth - UiChrome.Space3 * 2f, 20f);

                Text status = UiChrome.AddText(rt, "Status", UiChrome.FontCaption, TextAnchor.UpperLeft, UiChrome.TextSecondary);
                UiChrome.PlaceTopLeft(status.rectTransform, UiChrome.Space3, -38f, cardWidth - UiChrome.Space3 * 2f, 16f);

                var button = surface.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                button.onClick.AddListener(() =>
                {
                    if (TryClaimAction("equip" + (int)slot)) OnEquipCardClicked(slot);
                });

                _equipCards[i] = new EquipCard
                {
                    Rect = rt, Surface = surface, Outline = outline, Title = title, Status = status, Slot = slot,
                };
            }

            float detailTop = -(2f * (EquipCardHeight + UiChrome.Space3));
            Image detail = UiChrome.AddSurface(page, "EquipDetail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = new Vector2(0f, detailTop);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            float innerWidth = RightWidth() - UiChrome.Space4 * 2f;

            _equipDetailName = UiChrome.AddText(drt, "DetailName", UiChrome.FontTitle, TextAnchor.UpperLeft,
                UiChrome.TextPrimary, bold: true);
            UiChrome.PlaceTopLeft(_equipDetailName.rectTransform, UiChrome.Space4, -UiChrome.Space3, innerWidth, 20f);

            _equipDetailMeta = UiChrome.AddText(drt, "DetailMeta", UiChrome.FontCaption, TextAnchor.UpperLeft, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(_equipDetailMeta.rectTransform, UiChrome.Space4, -34f, innerWidth, 16f);

            _equipDetailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_equipDetailBody.rectTransform, UiChrome.Space4, -56f, innerWidth, 36f);

            _equipActionSurface = UiChrome.AddSurface(drt, "EquipAction", UiChrome.AccentSurface, UiChrome.RadiusChip);
            _equipActionRect = _equipActionSurface.rectTransform;
            UiChrome.PlaceTopLeft(_equipActionRect, UiChrome.Space4, -102f, 128f, 28f);
            _equipActionOutline = UiChrome.AddOutline(_equipActionRect, "Outline", UiChrome.AccentBorder, UiChrome.RadiusChip);
            _equipActionLabel = UiChrome.AddText(_equipActionRect, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter,
                UiChrome.TextOnAccent, bold: true);
            UiChrome.Stretch(_equipActionLabel.rectTransform);
            _equipActionLabel.text = "착용하기";
            var actionButton = _equipActionSurface.gameObject.AddComponent<Button>();
            actionButton.targetGraphic = _equipActionSurface;
            actionButton.onClick.AddListener(() => { if (TryClaimAction("equipAction")) OnEquipActionClicked(); });

            Text note = UiChrome.AddText(drt, "Note", UiChrome.FontCaption, TextAnchor.LowerRight, UiChrome.TextTertiary);
            UiChrome.Stretch(note.rectTransform);
            note.rectTransform.offsetMin = new Vector2(0f, UiChrome.Space3);
            note.rectTransform.offsetMax = new Vector2(-UiChrome.Space4, 0f);
            note.text = "장비는 구매가 아니라 레벨업으로 열려요";
        }

        private void BuildAppearancePage(RectTransform right)
        {
            RectTransform page = CreatePage(right, "AppearancePage");
            _pages[(int)Tab.Appearance] = page.gameObject;

            const float LabelWidth = 76f;
            const float FieldX = 84f;
            const float RowHeight = 28f;
            float rowStep = RowHeight + UiChrome.Space3;

            Text nameLabel = UiChrome.AddText(page, "NameLabel", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(nameLabel.rectTransform, 0f, 0f, LabelWidth, RowHeight);
            nameLabel.text = "이름";

            _nameInput = CreateInputField(page);
            UiChrome.PlaceTopLeft(_nameInput.GetComponent<RectTransform>(), FieldX, 0f, 220f, RowHeight);

            Text inkLabel = UiChrome.AddText(page, "InkLabel", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(inkLabel.rectTransform, 0f, -rowStep, LabelWidth, RowHeight);
            inkLabel.text = "잉크색";

            string[] inkNames = { "검정", "흰색" };
            for (int i = 0; i < 2; i++)
            {
                Image surface = UiChrome.AddSurface(page, "Ink" + inkNames[i], UiChrome.CardSurface, UiChrome.RadiusChip);
                var rt = surface.rectTransform;
                UiChrome.PlaceTopLeft(rt, FieldX + i * (92f + UiChrome.Space2), -rowStep, 92f, RowHeight);
                Image outline = UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
                Text label = UiChrome.AddText(rt, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter, UiChrome.TextSecondary);
                UiChrome.Stretch(label.rectTransform);
                label.text = inkNames[i];

                var button = surface.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                bool white = i == 1;
                button.onClick.AddListener(() => { if (TryClaimAction("ink" + (white ? 1 : 0))) OnInkButtonClicked(white); });

                _inkSurfaces[i] = surface;
                _inkOutlines[i] = outline;
                _inkRects[i] = rt;
                _inkLabels[i] = label;
            }

            // 크기 배율 — 읽기 전용이다(아래 안내가 그 이유를 사용자에게도 정직하게 말한다).
            Text scaleLabel = UiChrome.AddText(page, "ScaleLabel", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(scaleLabel.rectTransform, 0f, -rowStep * 2f, LabelWidth, RowHeight);
            scaleLabel.text = "크기 배율";

            _scaleValue = UiChrome.AddText(page, "ScaleValue", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary, bold: true);
            UiChrome.PlaceTopLeft(_scaleValue.rectTransform, FieldX, -rowStep * 2f, 140f, RowHeight);

            Image divider = UiChrome.AddSurface(page, "Divider", UiChrome.Divider, 2);
            UiChrome.PlaceTopLeft(divider.rectTransform, 0f, -rowStep * 3f, RightWidth(), 1f);
            divider.raycastTarget = false;

            Text entryTitle = UiChrome.AddText(page, "EntryTitle", UiChrome.FontLabel, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, bold: true);
            UiChrome.PlaceTopLeft(entryTitle.rectTransform, 0f, -rowStep * 3f - UiChrome.Space3, RightWidth(), 18f);
            entryTitle.text = "이 창을 여는 세 가지 방법";

            Text entryBody = UiChrome.AddText(page, "EntryBody", UiChrome.FontCaption, TextAnchor.UpperLeft,
                UiChrome.TextTertiary, wrap: true);
            UiChrome.PlaceTopLeft(entryBody.rectTransform, 0f, -rowStep * 3f - 36f, RightWidth(), 90f);
            // 슬라이더를 만들지 않은 이유를 화면에서도, 코드에서도 같은 말로 남긴다:
            // characterScale은 프리팹 지오메트리(뼈 길이/획 두께/콜라이더)에 **구워지는** 값이라
            // 런타임에 숫자만 바꾸면 그림은 그대로인 채 물리만 어긋난다. 슬라이더를 붙이면 "움직였는데
            // 아무 일도 안 일어난다"가 되고, 그것이 이 프로젝트가 가장 자주 겪은 실패다.
            entryBody.text =
                "화면 우상단 톱니 아이콘  ·  전역 단축키 ⌃⌥⌘I  ·  캐릭터 우클릭 → [캐릭터 정보]\n\n" +
                "이름과 잉크색은 바꾸는 즉시 반영되고 자동 저장돼요. 크기 배율은 캐릭터 뼈대에 구워지는 " +
                "값이라 앱 안에서는 바꿀 수 없어요 — 에디터의 [StickMate ▸ Resize Stickman]으로 다시 구워야 합니다.";
        }

        private void BuildInventoryPage(RectTransform right)
        {
            RectTransform page = CreatePage(right, "InventoryPage");
            _pages[(int)Tab.Inventory] = page.gameObject;

            float listWidth = RightWidth() - InventoryRailWidth - UiChrome.Space2;
            float rowStep = InventoryRowHeight + InventoryRowGap;

            for (int i = 0; i < InventoryVisibleRows; i++)
            {
                Image surface = UiChrome.AddSurface(page, "InvRow" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                var rt = surface.rectTransform;
                UiChrome.PlaceTopLeft(rt, 0f, -i * rowStep, listWidth, InventoryRowHeight);
                Image outline = UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);

                // 장비/행동을 완전히 같은 행 모양으로 그린다(디자이너 확정) —
                // ● 표식 / 이름 / 부제 / 설명 한 줄 / 상태 슬롯(96pt 고정, 훗날 가격표 자리).
                Image dot = UiChrome.AddSurface(rt, "Dot", UiChrome.TextTertiary, 3);
                UiChrome.PlaceTopLeft(dot.rectTransform, UiChrome.Space2, -(InventoryRowHeight - 6f) * 0.5f, 6f, 6f);
                dot.raycastTarget = false;

                float nameX = UiChrome.Space2 + 6f + UiChrome.Space2;
                Text title = UiChrome.AddText(rt, "Title", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextPrimary);
                UiChrome.PlaceTopLeft(title.rectTransform, nameX, 0f, 92f, InventoryRowHeight);

                Text subtitle = UiChrome.AddText(rt, "Subtitle", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary);
                UiChrome.PlaceTopLeft(subtitle.rectTransform, nameX + 94f, 0f, 44f, InventoryRowHeight);

                float descX = nameX + 94f + 46f;
                float descWidth = listWidth - descX - StatusSlotWidth - UiChrome.Space2;
                Text description = UiChrome.AddText(rt, "Description", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextSecondary);
                UiChrome.PlaceTopLeft(description.rectTransform, descX, 0f, Mathf.Max(40f, descWidth), InventoryRowHeight);
                // 줄바꿈하지 않는다 — 길이는 Ellipsize가 미리 자른다(위 상수 참고).
                description.horizontalOverflow = HorizontalWrapMode.Overflow;
                description.verticalOverflow = VerticalWrapMode.Truncate;

                Text statusSlot = UiChrome.AddText(rt, "StatusSlot", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.TextSecondary);
                UiChrome.PlaceTopLeft(statusSlot.rectTransform, listWidth - StatusSlotWidth - UiChrome.Space2, 0f,
                    StatusSlotWidth, InventoryRowHeight);

                Text header = UiChrome.AddText(rt, "Header", UiChrome.FontLabel, TextAnchor.MiddleLeft,
                    UiChrome.TextSecondary, bold: true);
                UiChrome.PlaceTopLeft(header.rectTransform, 0f, 0f, listWidth, InventoryRowHeight);

                var button = surface.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                int captured = i;
                button.onClick.AddListener(() =>
                {
                    InventoryRowView view = _inventoryViews[captured];
                    if (view == null || view.BoundCatalogIndex < 0) return;
                    if (TryClaimAction("inv" + captured)) OnInventoryRowClicked(view.BoundCatalogIndex);
                });

                _inventoryViews[i] = new InventoryRowView
                {
                    Rect = rt, Surface = surface, Outline = outline, Dot = dot, Title = title, Subtitle = subtitle,
                    Description = description, StatusSlot = statusSlot, HeaderText = header, BoundCatalogIndex = -1,
                };
            }

            // 페이지 버튼 — 휠에 기대지 않는다(클래스 문서 참고).
            float listHeight = InventoryVisibleRows * rowStep - InventoryRowGap;
            float railX = listWidth + UiChrome.Space2;

            _pageUpRect = BuildPagerButton(page, "PageUp", "▲", railX, 0f, () => ScrollInventory(-1), "pageUp");
            _pageDownRect = BuildPagerButton(page, "PageDown", "▼", railX, -(listHeight - InventoryRailWidth),
                () => ScrollInventory(1), "pageDown");

            _pageIndicator = UiChrome.AddText(page, "PageIndicator", UiChrome.FontCaption, TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(_pageIndicator.rectTransform, railX, -(InventoryRailWidth + UiChrome.Space2),
                InventoryRailWidth, listHeight - InventoryRailWidth * 2f - UiChrome.Space2 * 2f);

            Image detail = UiChrome.AddSurface(page, "InventoryDetail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = new Vector2(1f, 0f);
            drt.pivot = new Vector2(0.5f, 0f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = new Vector2(0f, InventoryDetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            float innerWidth = RightWidth() - UiChrome.Space4 * 2f;

            _inventoryDetailName = UiChrome.AddText(drt, "DetailName", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextPrimary, bold: true);
            UiChrome.PlaceTopLeft(_inventoryDetailName.rectTransform, UiChrome.Space4, -UiChrome.Space3, innerWidth, 18f);

            _inventoryDetailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontLabel, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_inventoryDetailBody.rectTransform, UiChrome.Space4, -34f, innerWidth, 30f);

            Text note = UiChrome.AddText(drt, "Note", UiChrome.FontCaption, TextAnchor.LowerRight, UiChrome.TextTertiary);
            UiChrome.Stretch(note.rectTransform);
            note.rectTransform.offsetMin = new Vector2(UiChrome.Space4, UiChrome.Space2);
            note.rectTransform.offsetMax = new Vector2(-UiChrome.Space4, 0f);
            // 지금 파는 것은 하나도 없다 — 그 사실을 화면에서도 숨기지 않는다.
            note.text = "지금은 파는 것이 없습니다";
        }

        private RectTransform BuildPagerButton(RectTransform page, string name, string glyph, float x, float y,
            UnityEngine.Events.UnityAction action, string dedupKey)
        {
            Image surface = UiChrome.AddSurface(page, name, UiChrome.CardSurface, UiChrome.RadiusChip);
            var rt = surface.rectTransform;
            UiChrome.PlaceTopLeft(rt, x, y, InventoryRailWidth, InventoryRailWidth);
            UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);

            Text label = UiChrome.AddText(rt, "Label", UiChrome.FontCaption, TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;

            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() => { if (TryClaimAction(dedupKey)) action(); });
            return rt;
        }

        private void BuildStatsBlock(RectTransform right)
        {
            var blockGo = new GameObject("StatsBlock", typeof(RectTransform));
            blockGo.transform.SetParent(right, false);
            var block = blockGo.GetComponent<RectTransform>();
            block.anchorMin = new Vector2(0f, 0f);
            block.anchorMax = new Vector2(1f, 0f);
            block.pivot = new Vector2(0.5f, 0f);
            block.offsetMin = Vector2.zero;
            block.offsetMax = new Vector2(0f, StatsBlockHeight);

            float cellWidth = (RightWidth() - UiChrome.Space3) * 0.5f;
            float rowGap = (StatsBlockHeight - StatRowHeight * 3f) * 0.5f;

            for (int i = 0; i < StatCount; i++)
            {
                int col = i % 2, row = i / 2;

                Image cell = UiChrome.AddSurface(block, "Stat" + i, UiChrome.SubtleSurface, UiChrome.RadiusChip);
                UiChrome.PlaceTopLeft(cell.rectTransform, col * (cellWidth + UiChrome.Space3),
                    -row * (StatRowHeight + rowGap), cellWidth, StatRowHeight);
                cell.raycastTarget = false;

                // 좌측 고정폭 라벨 + 우측 정렬 값 — 라벨 길이가 달라도 값이 세로로 줄맞춰진다(디자이너 지시).
                Text label = UiChrome.AddText(cell.rectTransform, "Label", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                    UiChrome.TextTertiary);
                UiChrome.PlaceTopLeft(label.rectTransform, UiChrome.Space3, 0f, 84f, StatRowHeight);
                label.text = StatLabels[i];

                Text value = UiChrome.AddText(cell.rectTransform, "Value", UiChrome.FontBody, TextAnchor.MiddleRight,
                    UiChrome.TextPrimary, bold: true);
                UiChrome.PlaceTopLeft(value.rectTransform, UiChrome.Space3 + 84f, 0f,
                    cellWidth - UiChrome.Space3 * 2f - 84f, StatRowHeight);
                value.text = "—";
                _statValues[i] = value;
            }
        }

        // ==================== 작은 유틸 ====================

        /// <summary>우측 패널의 실제 폭(캔버스 유닛). 창 폭에서 유도하므로 한 곳만 고치면 전부 따라온다.</summary>
        private static float RightWidth() => PanelWidth - UiChrome.Space4 * 2f - LeftWidth - ColumnGap;

        private InputField CreateInputField(Transform parent)
        {
            Image surface = UiChrome.AddSurface(parent, "NameInput", UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.AddOutline(surface.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);

            Text text = UiChrome.AddText(surface.rectTransform, "Text", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            UiChrome.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(UiChrome.Space3, 0f);
            text.rectTransform.offsetMax = new Vector2(-UiChrome.Space3, 0f);
            text.supportRichText = false;

            Text placeholder = UiChrome.AddText(surface.rectTransform, "Placeholder", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(UiChrome.Space3, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-UiChrome.Space3, 0f);
            placeholder.text = CharacterProgressionModel.DefaultCharacterName;
            placeholder.fontStyle = FontStyle.Italic;

            var input = surface.gameObject.AddComponent<InputField>();
            input.targetGraphic = surface;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = CharacterProgressionModel.MaxNameLength;
            input.lineType = InputField.LineType.SingleLine;
            input.text = CharacterProgressionModel.CharacterName;
            input.onEndEdit.AddListener(_ => CommitNameInput());
            return input;
        }

        /// <summary>TodoPostItWidget.EnsureEventSystem과 같은 이유/같은 구현 — 씬에 EventSystem이 있어도
        /// 입력 모듈이 없으면 Button.onClick이 영원히 발동하지 않으므로 그 자리에서 보강한다.</summary>
        private static void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current != null
                ? EventSystem.current
                : Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.GetComponent<BaseInputModule>() == null)
                {
                    existing.gameObject.AddComponent<StandaloneInputModule>();
                }
                return;
            }
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
        }

        // ==================== 상태 라벨 ====================

        /// <summary>상태 ID를 사람이 읽는 한 마디로. <b>대사가 아니다</b> — 원칙 1의 적용 대상이 아니며
        /// (DialogueIntent를 만들지 않는다), 상태가 확정된 뒤 그것을 그대로 옮겨 적을 뿐이다.
        /// 초상화 포즈도 <b>같은 상태 값</b>에서 파생된다(CharacterPortraitStage.PoseForState).</summary>
        public static string StateLabel(StickmanStateId id)
        {
            switch (id)
            {
                case StickmanStateId.Idle: return "가만히 있는 중";
                case StickmanStateId.Walk: return "걷는 중";
                case StickmanStateId.Jump: return "점프 중";
                case StickmanStateId.Fall: return "떨어지는 중";
                case StickmanStateId.LandingCrouch: return "착지하는 중";
                case StickmanStateId.ParkourClimb: return "벽 타는 중";
                case StickmanStateId.LedgeHang: return "매달려 내려가는 중";
                case StickmanStateId.Attack: return "공격 모션 중";
                case StickmanStateId.Ragdoll: return "넘어져 있는 중";
                case StickmanStateId.ThrowTumble: return "날아가는 중";
                case StickmanStateId.Getup: return "일어나는 중";
                case StickmanStateId.BattleMinigame: return "격파 놀이 중";
                case StickmanStateId.Dragged: return "붙잡혀 있는 중";
                case StickmanStateId.RodeoCursor: return "커서 타는 중";
                case StickmanStateId.WindowTheft: return "창 도둑 놀이 중";
                case StickmanStateId.Graffiti: return "낙서하는 중";
                case StickmanStateId.DesktopTidy: return "바탕화면 정리 중";
                case StickmanStateId.BlackholeSummon: return "블랙홀 소환 중";
                case StickmanStateId.WindowCrash: return "창 부수는 중";
                case StickmanStateId.TodoReminder: return "할일 알려주는 중";
                case StickmanStateId.FocusStart: return "집중 모드 시작";
                case StickmanStateId.FocusComplete: return "집중 모드 완료";
                case StickmanStateId.FocusCancelled: return "집중 모드 취소";
                case StickmanStateId.FocusNudge: return "딴짓 감시 중";
                case StickmanStateId.Sulky: return "부루퉁한 중";
                case StickmanStateId.Runaway: return "가출 중";
                case StickmanStateId.Archery: return "활 쏘는 중";
                default: return id.ToString();
            }
        }
    }
}
