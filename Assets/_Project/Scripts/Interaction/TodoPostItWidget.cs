using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;

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
    /// </summary>
    public sealed class TodoPostItWidget : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;
        [SerializeField] private bool _featureEnabledDefault = true;

        private const int SortingOrderTopMost = 30000;
        private const float RowHeight = 26f;
        private const float PanelWidth = 220f;
        private const float PanelPadding = 8f;

        private sealed class RowWidgets
        {
            public GameObject Root;
            public Button CheckboxButton;
            public Text Label;
            public int TodoId;
        }

        private Canvas _canvas;
        private RectTransform _panelRoot;
        private RectTransform _rowContainer;
        private readonly List<RowWidgets> _rows = new List<RowWidgets>();
        private Text _moreLabel;
        private Button _moreButton;
        private Button _hideButton;

        private bool _featureEnabled;
        private bool _sessionHidden; // 17절 "[숨기기]" — 그 세션 동안만, 데이터는 유지.
        private bool _expanded;
        private float _sweepTimer;

        private void Awake()
        {
            _featureEnabled = _featureEnabledDefault;
            BuildUi();
        }

        private void OnEnable()
        {
            StickmanEventBus.TodoListChanged += OnTodoListChanged;
            RefreshView();
        }

        private void OnDisable()
        {
            StickmanEventBus.TodoListChanged -= OnTodoListChanged;
        }

        private void Update()
        {
            // 완료 유예 정리(17절 "짧은 유지 후 목록에서 자동 정리")는 매 프레임 돌 필요가 없다 —
            // 다른 저빈도 폴러들과 동일한 절제 컨벤션으로 0.5초 주기로 제한.
            _sweepTimer += Time.deltaTime;
            if (_sweepTimer < 0.5f) return;
            _sweepTimer = 0f;
            float linger = _config != null ? _config.todoCompletedLingerSeconds : 2.5f;
            TodoListModel.SweepCompleted(linger);
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
            _sessionHidden = true;
            RefreshView();
        }

        private void OnMoreClicked()
        {
            _expanded = !_expanded;
            RefreshView();
        }

        private void OnRowCheckboxClicked(RowWidgets row)
        {
            TodoListModel.ToggleComplete(row.TodoId);
            // TodoListModel.ToggleComplete가 TodoListChanged를 발행해 OnTodoListChanged -> RefreshView로
            // 이어지므로 여기서 직접 RefreshView를 부를 필요는 없다(단일 갱신 경로 유지).
        }

        private void RefreshView()
        {
            if (_panelRoot == null) return;

            var items = TodoListModel.ActiveItems;
            bool visible = _featureEnabled && !_sessionHidden && items.Count > 0; // 17절 빈 상태 예외
            _panelRoot.gameObject.SetActive(visible);
            if (!visible) return;

            int maxRows = _config != null ? Mathf.Max(1, _config.todoPostItMaxVisibleRows) : 4;
            int visibleCount = _expanded ? items.Count : Mathf.Min(maxRows, items.Count);

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
                row.Label.text = box + " " + item.Text;
                Color c = row.Label.color;
                c.a = item.Completed ? 0.5f : 1f; // 완료 항목은 반투명(취소선 대신 최소 스코프 근사, 17절)
                row.Label.color = c;
            }

            int hiddenCount = items.Count - visibleCount;
            bool showMore = !_expanded && hiddenCount > 0;
            _moreLabel.gameObject.SetActive(showMore);
            if (showMore) _moreLabel.text = "[+" + hiddenCount + "개 더보기]";

            LayoutRows(visibleCount);
        }

        private void EnsureRowCount(int count)
        {
            while (_rows.Count < count) _rows.Add(CreateRow());
        }

        private void LayoutRows(int visibleCount)
        {
            float y = -PanelPadding;
            for (int i = 0; i < visibleCount; i++)
            {
                var rt = _rows[i].Root.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(PanelPadding, y);
                y -= RowHeight;
            }
            if (_moreLabel.gameObject.activeSelf)
            {
                var rt = _moreButton.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(PanelPadding, y);
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

            var panelGo = new GameObject("PostItPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            _panelRoot = panelGo.GetComponent<RectTransform>();
            _panelRoot.anchorMin = new Vector2(1f, 1f);
            _panelRoot.anchorMax = new Vector2(1f, 1f);
            _panelRoot.pivot = new Vector2(1f, 1f);
            _panelRoot.anchoredPosition = new Vector2(-16f, -16f); // 화면 우상단 기본 위치(17절 "유저가 위치 지정 가능"은 후속 과제)
            _panelRoot.sizeDelta = new Vector2(PanelWidth, RowHeight);
            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(1f, 0.95f, 0.6f, 0.92f); // 포스트잇 톤 플레이스홀더(디자이너 확정 전까지)

            _hideButton = CreateSmallButton(panelGo.transform, "HideButton", "[숨기기]", new Vector2(1f, 1f), new Vector2(-6f, -4f));
            _hideButton.onClick.AddListener(OnHideClicked);

            var rowContainerGo = new GameObject("Rows", typeof(RectTransform));
            rowContainerGo.transform.SetParent(panelGo.transform, false);
            _rowContainer = rowContainerGo.GetComponent<RectTransform>();
            _rowContainer.anchorMin = Vector2.zero;
            _rowContainer.anchorMax = Vector2.one;
            _rowContainer.offsetMin = Vector2.zero;
            _rowContainer.offsetMax = new Vector2(0f, -RowHeight); // 상단 [숨기기] 줄 아래부터 시작

            _moreButton = CreateSmallButton(rowContainerGo.transform, "MoreButton", "[+N개 더보기]", new Vector2(0f, 1f), Vector2.zero);
            _moreButton.onClick.AddListener(OnMoreClicked);
            _moreLabel = _moreButton.GetComponentInChildren<Text>();
            _moreLabel.gameObject.SetActive(false);
            _moreButton.gameObject.SetActive(true);

            panelGo.SetActive(false); // 초기에는 빈 상태 — RefreshView가 실제 표시 여부를 결정.
        }

        private RowWidgets CreateRow()
        {
            var rowGo = new GameObject("TodoRow", typeof(RectTransform));
            rowGo.transform.SetParent(_rowContainer, false);
            var rt = rowGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(PanelWidth - PanelPadding * 2f, RowHeight);

            var buttonGo = new GameObject("Checkbox", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(rowGo.transform, false);
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = Vector2.zero;
            buttonRt.anchorMax = Vector2.one;
            buttonRt.offsetMin = Vector2.zero;
            buttonRt.offsetMax = Vector2.zero;
            buttonGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // 거의 투명 — 히트테스트 대상용 배경

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.black;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            var row = new RowWidgets
            {
                Root = rowGo,
                CheckboxButton = buttonGo.GetComponent<Button>(),
                Label = label,
            };
            row.CheckboxButton.onClick.AddListener(() => OnRowCheckboxClicked(row));
            return row;
        }

        private Button CreateSmallButton(Transform parent, string name, string text, Vector2 anchor, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(90f, RowHeight - 4f);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            return go.GetComponent<Button>();
        }

        /// <summary>uGUI 클릭 판정에는 씬에 EventSystem이 있어야 한다 — 아직 씬 조립이 없는 개발 단계라
        /// 이 위젯이 직접 보장한다(이미 하나 있으면 만들지 않음). 이 EventSystem은 캐릭터 클릭 감지
        /// (StickmanClickHitbox, OnMouseDown 기반 물리 레이캐스트)와 아무 관련이 없다 — uGUI 전용이다.</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
        }
    }
}
