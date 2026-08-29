using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 캐릭터 정보 / 장비 창 — docs/UX_FLOW.md 7절 "설정창 와이어프레임"이 <b>이 프로젝트에서 처음
    /// 실제로 지어진</b> 라운드(2026-08-29 사용자 요청: "캐릭터 장비 착용 및 캐릭터 정보 볼수있는 창").
    ///
    /// ============================================================================
    /// 7절 원안과 이번 구현의 차이 — 그리고 그 이유
    /// ============================================================================
    /// · 탭은 <b>[정보] / [장비] 2개만</b> 만든다. 원안의 [일반]/[스킨·DLC]/[모드]/[모바일] 4탭 중
    ///   지금 실제로 소비자가 있는 것은 없다(모바일 백드롭 모드/대결 모드는 아직 코드가 없다).
    ///   빈 탭을 억지로 채우면 "있는 척하는 UI"가 되어 그게 더 나쁘다(리더 확정 범위).
    /// · 원안의 <b>"구매"를 "레벨업 해제"로 치환</b>했다. 근거는 Core/EquipmentModel.cs 클래스 문서
    ///   ("원안을 왜 그대로 쓰지 않았는가") 참고 — 결제 백엔드도 외부 아트 에셋도 없고, 결제 UI를
    ///   흉내만 내는 것은 사용자에게 거짓 약속이 된다.
    /// · 원안이 요구한 골격(제목바 + 탭 + [X] 닫기, "모든 조작은 즉시 반영, 별도 저장 버튼 없음")은
    ///   그대로 지킨다.
    ///
    /// ============================================================================
    /// 진입점 3개 (주 진입점은 톱니 아이콘)
    /// ============================================================================
    ///  1. <b>화면 우상단 톱니 아이콘 클릭</b> — Interaction/InfoGearIconWidget.cs (주 진입점,
    ///     2026-08-29 사용자 요청 "바탕화면 오른쪽 상단에 기어 표시같은걸 띄워놓고 클릭하면 기어가
    ///     회전하면서 캐릭터 창이 나오게끔"). 톱니가 한 바퀴 돈 뒤 이 창을 연다.
    ///  2. 전역 단축키 <b>⌃⌥⌘I</b> (Info) — Interaction/AppControlDirector.cs.
    ///  3. 캐릭터 우클릭 메뉴 <b>[캐릭터 정보]</b> — 같은 파일.
    /// 이 프로젝트의 다른 모든 기능이 "단축키 + 메뉴" 이중 경로를 갖는 관례를 그대로 따른다.
    ///
    /// ============================================================================
    /// 클릭 판정 — TodoPostItWidget과 <b>같은 관례</b>(새 메커니즘을 만들지 않는다)
    /// ============================================================================
    /// (1) uGUI Button + 자체 EventSystem 보강, (2) 창 사각형을 덮는 isTrigger BoxCollider2D
    /// (UniWindowController의 Raycast 히트테스트가 이걸 보고 그 영역만 클릭관통을 푼다),
    /// (3) 전역 폴링 히트테스트(macOS 비활성 앱의 첫 클릭이 앱 활성화에만 소비되는 경우 대비).
    /// 세 경로가 같은 핸들러를 부르고 <see cref="ActionDedupSeconds"/> 창으로 중복을 막는다.
    ///
    /// <b>창이 닫히면 완전히 원래대로</b>: 차단막 콜라이더는 창이 열려 있는 동안만 enabled=true다
    /// (OnDisable/닫기에서 반드시 끈다 — 비침해 원칙 직결).
    ///
    /// ============================================================================
    /// 불변 원칙 1(행동-텍스트 싱크) — 무관하다
    /// ============================================================================
    /// 이 창은 대사 시스템이 아니다. DialogueIntent를 만들지 않고 상태 전이를 일으키지도 않는다.
    /// "지금 상태" 표시는 상태머신이 이미 확정한 사실을 <b>읽어서</b> 보여줄 뿐이라, 오히려 원칙 1이
    /// 요구하는 방향(행동 → 텍스트)과 같은 방향이다.
    ///
    /// ============================================================================
    /// 매 프레임 할당 금지 (24시간 상주 앱)
    /// ============================================================================
    /// 창이 닫혀 있으면 Update()는 첫 줄에서 돌아간다. 열려 있을 때도 "지금 상태" 라벨은
    /// <b>상태 ID가 실제로 바뀐 프레임에만</b> 문자열을 만든다(매 프레임 갱신하되 매 프레임
    /// 할당하지는 않는다). 나머지 수치는 <see cref="SlowRefreshInterval"/> 주기로만 다시 만든다.
    /// </summary>
    public sealed class CharacterInfoWindow : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        private const int SortingOrderTopMost = 31000; // 포스트잇(30000)보다 위, 앱 제어 메뉴(32760)보다 아래.
        private const float PanelWidth = 430f;         // 캔버스 유닛 == OS 포인트.
        private const float PanelHeight = 318f;   // 장비 탭 4행(46+4) x 4 + 안내 한 줄이 딱 들어가는 높이.
        private const float TitleHeight = 30f;
        private const float TabHeight = 28f;
        private const float Padding = 12f;
        private const float RowHeight = 26f;
        private const float EquipRowHeight = 46f;
        private const float SlowRefreshInterval = 0.25f;
        private const float ClickPollInterval = 0.05f;
        private const float ActionDedupSeconds = 0.35f;

        /// <summary>창 위치 — 화면 우상단(톱니 아이콘 바로 아래). 톱니와 같은 모서리를 쓰면
        /// "그 아이콘이 이 창을 연다"는 관계가 위치로도 읽힌다.</summary>
        private const float PanelMarginRight = 16f;
        private const float PanelMarginTop = 84f;

        private enum Tab { Info = 0, Equipment = 1 }

        private StickmanAgent _agent;
        private IGlobalPointerButtonService _buttonService;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _panel;
        private BoxCollider2D _clickBlocker;

        private Button _closeButton;
        private Button[] _tabButtons;
        private Text[] _tabLabels;
        private RectTransform[] _tabRects;
        private GameObject _infoPage;
        private GameObject _equipPage;

        // 정보 탭 위젯.
        private InputField _nameInput;
        private Text _levelText;
        private RectTransform _xpFill;
        private Text _xpText;
        private Text _stateText;
        private RectTransform _stressFill;
        private Text _stressText;
        private Text _settingsSummary;

        // 장비 탭 위젯.
        private sealed class EquipRow
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Frame;
            public Text Title;
            public Text Status;
            public EquipmentSlot Slot;
        }
        private readonly EquipRow[] _equipRows = new EquipRow[EquipmentModel.SlotCount];

        private bool _open;
        private Tab _tab = Tab.Info;
        private float _slowTimer;
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private string _lastActionKey;
        private float _lastActionTime;
        private StickmanStateId _lastShownState = (StickmanStateId)(-1);
        private bool _hasShownState;

        public bool IsOpen => _open;

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 라이벌 복제본에서 창이 두 벌 뜨지 않게 하는
            // 2차 방어(1차는 SceneBootstrapper의 컴포넌트 제거). TodoPostItWidget과 같은 관례.
            _agent = GetComponent<StickmanAgent>();
            if (_config == null && _agent != null) _config = _agent.Config;
            BuildUi();
        }

        private void Start()
        {
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<BaseInputModule>() : null;
            Debug.Log("[정보창] 준비 완료 — 여는 방법 3가지: " +
                "(1) **화면 우상단 톱니 아이콘 클릭**(주 진입점), (2) 전역 단축키 **⌃⌥⌘I**, " +
                "(3) 캐릭터 우클릭 -> [캐릭터 정보]. " +
                $"입력 모듈={(module != null ? module.GetType().Name : "★없음(uGUI 클릭 불가)")}, " +
                $"전역 폴링 경로={(_buttonService != null ? "사용 가능" : "미지원 — uGUI 경로만")}.");
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
        }

        private void OnDestroy()
        {
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
            _leftInitialized = false; // 창을 여는 그 클릭이 곧바로 행 클릭으로 오인되지 않게.
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_clickBlocker != null) _clickBlocker.enabled = true;
            SyncNameInputFromModel();
            RefreshAll();
            Debug.Log($"[정보창] 열림({source}) — {CharacterProgressionModel.CharacterName} " +
                $"Lv.{CharacterProgressionModel.Level}, 탭=[{(_tab == Tab.Info ? "정보" : "장비")}].");
        }

        public void Close(string source)
        {
            if (!_open) return;
            _open = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_clickBlocker != null) _clickBlocker.enabled = false;
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
            TickStateLabel();

            _slowTimer += Time.unscaledDeltaTime;
            if (_slowTimer < SlowRefreshInterval) return;
            _slowTimer = 0f;
            RefreshNumbers();
        }

        /// <summary>"지금 상태"만은 매 프레임 본다 — 단, <b>상태가 실제로 바뀐 프레임에만</b> 문자열을
        /// 만든다(클래스 문서 "매 프레임 할당 금지").</summary>
        private void TickStateLabel()
        {
            if (_stateText == null) return;
            var machine = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.Machine : null;
            if (machine == null)
            {
                if (!_hasShownState) { _stateText.text = "— (상태머신 없음)"; _hasShownState = true; }
                return;
            }

            StickmanStateId id = machine.CurrentStateId;
            if (_hasShownState && id == _lastShownState) return;
            _lastShownState = id;
            _hasShownState = true;
            _stateText.text = StateLabel(id);
        }

        private void OnProgressionChanged()
        {
            if (!_open) return;
            RefreshNumbers();
        }

        private void OnEquipmentChanged()
        {
            if (!_open) return;
            RefreshEquipmentRows();
        }

        private void RefreshAll()
        {
            _hasShownState = false;
            TickStateLabel();
            RefreshNumbers();
            RefreshEquipmentRows();
            ApplyTabVisibility();
        }

        private void RefreshNumbers()
        {
            if (_levelText != null) _levelText.text = $"Lv.{CharacterProgressionModel.Level}";

            float need = CharacterProgressionModel.XpToNextLevel(_config);
            float have = CharacterProgressionModel.CurrentXp;
            if (_xpText != null) _xpText.text = $"{have:F0} / {need:F0} XP";
            SetBarFill(_xpFill, need > 0f ? Mathf.Clamp01(have / need) : 0f);

            float stress = StressGauge.CurrentLevel;
            SetBarFill(_stressFill, stress);
            if (_stressText != null)
            {
                StressMoodTier tier = StressGaugeRenderer.TierForLevel(stress, _config);
                _stressText.text = $"{stress * 100f:F0}%  ({StressGaugeRenderer.TierLabel(tier)})";
            }

            if (_settingsSummary != null)
            {
                float scale = _config != null ? _config.characterScale : 1f;
                string ink = _config != null && _config.inkColor == StickmanInkColor.White ? "흰색" : "검정";
                _settingsSummary.text =
                    $"크기 배율 {scale:F2}   ·   잉크색 {ink}   ·   누적 경험치 {CharacterProgressionModel.TotalXpEarned:F0}\n" +
                    $"착용 중 {CountEquipped()}/{EquipmentModel.SlotCount}   ·   저장 위치: 앱 전용 데이터 폴더";
            }
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

        private void RefreshEquipmentRows()
        {
            for (int i = 0; i < _equipRows.Length; i++)
            {
                EquipRow row = _equipRows[i];
                if (row == null) continue;

                EquipmentSlot slot = row.Slot;
                bool unlocked = EquipmentModel.IsUnlocked(slot, _config);
                bool equipped = unlocked && EquipmentModel.IsEquipped(slot);
                int need = EquipmentModel.UnlockLevel(slot, _config);

                row.Title.text = $"{EquipmentModel.ItemName(slot)}  ({EquipmentModel.SlotName(slot)})";
                // ★ 이모지(🔒)를 쓰지 않는다 — 이 프로젝트의 UI 폰트는 Unity 내장 LegacyRuntime.ttf라
                //   이모지 글리프가 없어 실제 화면에서 **빈칸**으로 나온다(첫 육안 검증에서 확인).
                //   ●/○ 같은 기하 도형은 이 폰트에 있어 그대로 쓴다(AppControlDirector의 스트레스 점과 동일).
                row.Status.text = !unlocked
                    ? $"잠김  ·  Lv.{need} 부터 착용할 수 있어요"
                    : equipped ? "● 착용 중  (클릭하면 벗어요)" : "○ 미착용  (클릭하면 입어요)";

                // 현재 착용 중인 슬롯은 테두리로 구분한다(7절이 "적용중"을 시각적으로 구분하라고 요구).
                row.Frame.color = !unlocked
                    ? new Color(0f, 0f, 0f, 0.05f)
                    : equipped ? new Color(0.20f, 0.55f, 0.95f, 0.22f)
                               : new Color(0f, 0f, 0f, 0.10f);
                row.Title.color = unlocked ? Color.black : new Color(0.45f, 0.45f, 0.45f, 1f);
                row.Status.color = unlocked ? new Color(0.25f, 0.25f, 0.25f, 1f) : new Color(0.55f, 0.55f, 0.55f, 1f);
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
            Debug.Log($"[정보창] 탭 전환 -> [{(tab == Tab.Info ? "정보" : "장비")}].");
        }

        private void ApplyTabVisibility()
        {
            if (_infoPage != null) _infoPage.SetActive(_tab == Tab.Info);
            if (_equipPage != null) _equipPage.SetActive(_tab == Tab.Equipment);
            for (int i = 0; i < _tabLabels.Length; i++)
            {
                if (_tabLabels[i] == null) continue;
                bool active = i == (int)_tab;
                _tabLabels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                _tabLabels[i].color = active ? Color.black : new Color(0.45f, 0.45f, 0.45f, 1f);
                var img = _tabButtons[i].GetComponent<Image>();
                if (img != null) img.color = active ? new Color(0f, 0f, 0f, 0.10f) : new Color(0f, 0f, 0f, 0.02f);
            }
        }

        private void OnEquipRowClicked(EquipRow row)
        {
            if (row == null) return;
            bool unlocked = EquipmentModel.IsUnlocked(row.Slot, _config);
            if (!unlocked)
            {
                Debug.Log($"[장비] {EquipmentModel.ItemName(row.Slot)} 은(는) 아직 잠겨 있습니다 — " +
                    $"Lv.{EquipmentModel.UnlockLevel(row.Slot, _config)} 에서 해제됩니다(현재 Lv.{CharacterProgressionModel.Level}).");
                return;
            }

            if (!EquipmentModel.TryToggle(row.Slot, _config)) return;
            Debug.Log($"[장비] {EquipmentModel.ItemName(row.Slot)} " +
                $"{(EquipmentModel.IsEquipped(row.Slot) ? "착용" : "해제")} — 즉시 반영되고 즉시 저장됩니다.");
            CharacterSaveStore.Save(); // 7절 "모든 토글은 즉시 반영(별도 저장 버튼 없음)".
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

            if (_closeButton != null && ContainsScreenPoint(_closeButton.GetComponent<RectTransform>(), cursor))
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

            if (_tab == Tab.Equipment)
            {
                for (int i = 0; i < _equipRows.Length; i++)
                {
                    EquipRow row = _equipRows[i];
                    if (row == null || !row.Root.activeInHierarchy) continue;
                    if (!ContainsScreenPoint(row.Rect, cursor)) continue;
                    if (TryClaimAction("equip" + i)) OnEquipRowClicked(row);
                    return;
                }
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
            if (rt == null) return false;
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

            var panelGo = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(1f, 1f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 1f);
            _panel.anchoredPosition = new Vector2(-PanelMarginRight, -PanelMarginTop);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            // 거의 불투명한 밝은 패널 — 어떤 바탕화면 위에서도 글자가 읽혀야 한다(AppControlDirector와 같은 근거).
            panelGo.GetComponent<Image>().color = new Color(0.97f, 0.97f, 0.97f, 0.97f);

            BuildTitleBar(panelGo.transform);
            BuildTabs(panelGo.transform);
            BuildInfoPage(panelGo.transform);
            BuildEquipmentPage(panelGo.transform);
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
            var barGo = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(parent, false);
            var rt = barGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -TitleHeight);
            rt.offsetMax = Vector2.zero;
            barGo.GetComponent<Image>().color = new Color(0.20f, 0.55f, 0.95f, 0.16f);

            // "바탕화면에서 살고 있는 코워커(동료)"라는 컨셉을 제목에 반영한다(2026-08-29 사용자 원문).
            Text title = CreateText(barGo.transform, "Title", 13, TextAnchor.MiddleLeft);
            title.rectTransform.anchorMin = Vector2.zero;
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(Padding, 0f);
            title.rectTransform.offsetMax = new Vector2(-TitleHeight, 0f);
            title.text = "내 책상 동료 — StickMate";
            title.fontStyle = FontStyle.Bold;

            _closeButton = CreateButton(barGo.transform, "CloseButton", "✕", 14);
            var crt = _closeButton.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0.5f);
            crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-6f, 0f);
            crt.sizeDelta = new Vector2(24f, 22f);
            _closeButton.onClick.AddListener(() => { if (TryClaimAction("close")) Close("[X] 클릭"); });
        }

        private void BuildTabs(Transform parent)
        {
            _tabButtons = new Button[2];
            _tabLabels = new Text[2];
            _tabRects = new RectTransform[2];
            string[] names = { "정보", "장비" };
            float tabWidth = 84f;

            for (int i = 0; i < 2; i++)
            {
                Button b = CreateButton(parent, "Tab" + names[i], names[i], 13);
                var rt = b.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(tabWidth, TabHeight);
                rt.anchoredPosition = new Vector2(Padding + i * (tabWidth + 4f), -(TitleHeight + 4f));
                _tabButtons[i] = b;
                _tabRects[i] = rt;
                _tabLabels[i] = b.GetComponentInChildren<Text>();
                int captured = i;
                b.onClick.AddListener(() => { if (TryClaimAction("tab" + captured)) OnTabClicked((Tab)captured); });
            }
        }

        private RectTransform CreatePage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Padding, Padding);
            rt.offsetMax = new Vector2(-Padding, -(TitleHeight + TabHeight + 8f));
            return rt;
        }

        private void BuildInfoPage(Transform parent)
        {
            RectTransform page = CreatePage(parent, "InfoPage");
            _infoPage = page.gameObject;
            float y = 0f;

            // 이름 — 유일하게 uGUI 전용 경로다(키보드 입력은 전역 폴링으로 흉내 낼 수 없다).
            CreateLabeledRow(page, "이름", y, out RectTransform nameSlot);
            _nameInput = CreateInputField(nameSlot);
            _nameInput.onEndEdit.AddListener(_ => CommitNameInput());
            y -= RowHeight + 6f;

            // 레벨 + XP 바.
            CreateLabeledRow(page, "레벨", y, out RectTransform levelSlot);
            _levelText = CreateText(levelSlot, "LevelValue", 13, TextAnchor.MiddleLeft);
            _levelText.rectTransform.anchorMin = Vector2.zero;
            _levelText.rectTransform.anchorMax = new Vector2(0f, 1f);
            _levelText.rectTransform.pivot = new Vector2(0f, 0.5f);
            _levelText.rectTransform.sizeDelta = new Vector2(50f, 0f);
            _levelText.fontStyle = FontStyle.Bold;
            _xpFill = CreateBar(levelSlot, "XpBar", 54f, 150f, new Color(0.20f, 0.60f, 0.95f, 0.85f));
            _xpText = CreateText(levelSlot, "XpText", 11, TextAnchor.MiddleLeft);
            _xpText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _xpText.rectTransform.anchorMax = new Vector2(0f, 1f);
            _xpText.rectTransform.pivot = new Vector2(0f, 0.5f);
            _xpText.rectTransform.anchoredPosition = new Vector2(212f, 0f);
            _xpText.rectTransform.sizeDelta = new Vector2(120f, 0f);
            _xpText.color = new Color(0.30f, 0.30f, 0.30f, 1f);
            y -= RowHeight + 6f;

            // 지금 상태(매 프레임 갱신).
            CreateLabeledRow(page, "지금 상태", y, out RectTransform stateSlot);
            _stateText = CreateText(stateSlot, "StateValue", 13, TextAnchor.MiddleLeft);
            _stateText.rectTransform.anchorMin = Vector2.zero;
            _stateText.rectTransform.anchorMax = Vector2.one;
            _stateText.rectTransform.offsetMin = Vector2.zero;
            _stateText.rectTransform.offsetMax = Vector2.zero;
            y -= RowHeight + 6f;

            // 스트레스 게이지 — 19절 "원할 때(설정창)" 채널. 상시 노출이 아니라 이 창을 열었을 때만
            // 수치를 보여주므로, 19절이 금지한 "상시 게이지 UI"에 해당하지 않는다.
            CreateLabeledRow(page, "스트레스", y, out RectTransform stressSlot);
            _stressFill = CreateBar(stressSlot, "StressBar", 0f, 150f, new Color(0.92f, 0.55f, 0.20f, 0.85f));
            _stressText = CreateText(stressSlot, "StressText", 11, TextAnchor.MiddleLeft);
            _stressText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _stressText.rectTransform.anchorMax = new Vector2(0f, 1f);
            _stressText.rectTransform.pivot = new Vector2(0f, 0.5f);
            _stressText.rectTransform.anchoredPosition = new Vector2(158f, 0f);
            _stressText.rectTransform.sizeDelta = new Vector2(160f, 0f);
            _stressText.color = new Color(0.30f, 0.30f, 0.30f, 1f);
            y -= RowHeight + 10f;

            // 지금 적용된 설정값 요약(사용자 요청 항목).
            var sep = new GameObject("Separator", typeof(RectTransform), typeof(Image));
            sep.transform.SetParent(page, false);
            var srt = sep.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.offsetMin = new Vector2(0f, y - 1f);
            srt.offsetMax = new Vector2(0f, y);
            sep.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
            y -= 8f;

            _settingsSummary = CreateText(page, "SettingsSummary", 11, TextAnchor.UpperLeft);
            _settingsSummary.rectTransform.anchorMin = new Vector2(0f, 1f);
            _settingsSummary.rectTransform.anchorMax = new Vector2(1f, 1f);
            _settingsSummary.rectTransform.pivot = new Vector2(0.5f, 1f);
            _settingsSummary.rectTransform.offsetMin = new Vector2(0f, y - 40f);
            _settingsSummary.rectTransform.offsetMax = new Vector2(0f, y);
            _settingsSummary.color = new Color(0.35f, 0.35f, 0.35f, 1f);

            Text hint = CreateText(page, "Hint", 10, TextAnchor.LowerLeft);
            hint.rectTransform.anchorMin = Vector2.zero;
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.sizeDelta = new Vector2(0f, 16f);
            hint.text = "우상단 톱니 아이콘 · ⌃⌥⌘I · 캐릭터 우클릭 [캐릭터 정보] — 어느 쪽으로도 열고 닫을 수 있어요";
            hint.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        private void BuildEquipmentPage(Transform parent)
        {
            RectTransform page = CreatePage(parent, "EquipmentPage");
            _equipPage = page.gameObject;

            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                var rowGo = new GameObject("EquipRow" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                rowGo.transform.SetParent(page, false);
                var rt = rowGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(0f, -(EquipRowHeight + i * (EquipRowHeight + 4f)));
                rt.offsetMax = new Vector2(0f, -(i * (EquipRowHeight + 4f)));

                var frame = rowGo.GetComponent<Image>();
                Text title = CreateText(rowGo.transform, "Title", 13, TextAnchor.UpperLeft);
                title.rectTransform.anchorMin = Vector2.zero;
                title.rectTransform.anchorMax = Vector2.one;
                title.rectTransform.offsetMin = new Vector2(10f, 0f);
                title.rectTransform.offsetMax = new Vector2(-10f, -5f);
                title.fontStyle = FontStyle.Bold;

                Text status = CreateText(rowGo.transform, "Status", 11, TextAnchor.LowerLeft);
                status.rectTransform.anchorMin = Vector2.zero;
                status.rectTransform.anchorMax = Vector2.one;
                status.rectTransform.offsetMin = new Vector2(10f, 5f);
                status.rectTransform.offsetMax = new Vector2(-10f, 0f);

                var row = new EquipRow
                {
                    Root = rowGo, Rect = rt, Frame = frame, Title = title, Status = status, Slot = slot,
                };
                _equipRows[i] = row;

                int captured = i;
                rowGo.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (TryClaimAction("equip" + captured)) OnEquipRowClicked(row);
                });
            }

            Text note = CreateText(page, "Note", 10, TextAnchor.LowerLeft);
            note.rectTransform.anchorMin = Vector2.zero;
            note.rectTransform.anchorMax = new Vector2(1f, 0f);
            note.rectTransform.pivot = new Vector2(0.5f, 0f);
            note.rectTransform.sizeDelta = new Vector2(0f, 16f);
            // 원안(7절)의 "구매"를 레벨업 해제로 치환했다는 사실을 사용자에게도 정직하게 알린다.
            note.text = "장비는 구매가 아니라 레벨업으로 열려요 — 켜 두기만 해도 조금씩 자랍니다.";
            note.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        private void CreateLabeledRow(RectTransform page, string label, float y, out RectTransform valueSlot)
        {
            Text l = CreateText(page, "Label_" + label, 12, TextAnchor.MiddleLeft);
            l.rectTransform.anchorMin = new Vector2(0f, 1f);
            l.rectTransform.anchorMax = new Vector2(0f, 1f);
            l.rectTransform.pivot = new Vector2(0f, 1f);
            l.rectTransform.anchoredPosition = new Vector2(0f, y);
            l.rectTransform.sizeDelta = new Vector2(74f, RowHeight);
            l.text = label;
            l.color = new Color(0.45f, 0.45f, 0.45f, 1f);

            var slotGo = new GameObject("Value_" + label, typeof(RectTransform));
            slotGo.transform.SetParent(page, false);
            valueSlot = slotGo.GetComponent<RectTransform>();
            valueSlot.anchorMin = new Vector2(0f, 1f);
            valueSlot.anchorMax = new Vector2(1f, 1f);
            valueSlot.pivot = new Vector2(0f, 1f);
            valueSlot.offsetMin = new Vector2(78f, y - RowHeight);
            valueSlot.offsetMax = new Vector2(0f, y);
        }

        /// <summary>배경 + 채움 두 겹 막대. 채움은 anchorMax.x로 비율을 표현한다(폭 재계산 불필요).</summary>
        private RectTransform CreateBar(RectTransform parent, string name, float x, float width, Color fillColor)
        {
            var bgGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(parent, false);
            var bg = bgGo.GetComponent<RectTransform>();
            bg.anchorMin = new Vector2(0f, 0.5f);
            bg.anchorMax = new Vector2(0f, 0.5f);
            bg.pivot = new Vector2(0f, 0.5f);
            bg.anchoredPosition = new Vector2(x, 0f);
            bg.sizeDelta = new Vector2(width, 10f);
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.10f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bgGo.transform, false);
            var fill = fillGo.GetComponent<RectTransform>();
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fillGo.GetComponent<Image>().color = fillColor;
            return fill;
        }

        private InputField CreateInputField(RectTransform parent)
        {
            var go = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(170f, 0f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);

            Text text = CreateText(go.transform, "Text", 13, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(6f, 0f);
            text.rectTransform.offsetMax = new Vector2(-6f, 0f);
            text.supportRichText = false;

            Text placeholder = CreateText(go.transform, "Placeholder", 13, TextAnchor.MiddleLeft);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(6f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-6f, 0f);
            placeholder.text = CharacterProgressionModel.DefaultCharacterName;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color(0.6f, 0.6f, 0.6f, 1f);

            var input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = CharacterProgressionModel.MaxNameLength;
            input.lineType = InputField.LineType.SingleLine;
            input.text = CharacterProgressionModel.CharacterName;
            return input;
        }

        private static Button CreateButton(Transform parent, string name, string label, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.06f);

            Text t = CreateText(go.transform, "Label", fontSize, TextAnchor.MiddleCenter);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            t.text = label;
            return go.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            // 이 프로젝트에는 TextMeshPro가 없다 — TodoPostItWidget/AppControlDirector와 같은 내장 폰트.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.black;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
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
        /// (DialogueIntent를 만들지 않는다), 상태가 확정된 뒤 그것을 그대로 옮겨 적을 뿐이다.</summary>
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
