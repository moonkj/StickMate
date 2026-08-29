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
        private CanvasScaler _scaler;   // Retina 대응 — 캔버스 1유닛 == OS 포인트 1로 맞춘다(ApplyCanvasScaleFactor).
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

        // ---- 클릭 경로 2(전역 폴링). 클래스 문서 "★ 2026-08-29 실배선 라운드" 참고.
        private const float ClickPollInterval = 0.05f;   // 20Hz — AppControlDirector와 같은 주기.
        private const float ActionDedupSeconds = 0.35f;  // 두 경로가 같은 클릭을 두 번 처리하지 않게.
        private StickmanAgent _agent;
        private StickConfig _agentConfig;
        private IGlobalPointerButtonService _buttonService;
        private BoxCollider2D _clickThroughBlocker;      // 카드가 보이는 동안만 켜지는 히트테스트용.
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private string _lastActionKey;
        private float _lastActionTime;

        private void Awake()
        {
            _featureEnabled = _featureEnabledDefault;
            // 같은 GameObject의 StickmanAgent만 쓴다(씬 전체 탐색 폴백 없음) — 라이벌 복제본에 이
            // 위젯이 남아 있어도 카드가 두 벌 뜨지 않게 하는 2차 방어. 1차 방어는 SceneBootstrapper가
            // 라이벌에서 이 컴포넌트를 제거하는 것이다.
            _agent = GetComponent<StickmanAgent>();
            if (_config == null && _agent != null) _config = _agent.Config;
            BuildUi();
        }

        private void Start()
        {
            _agentConfig = _agent != null ? _agent.Config : _config;
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;

            // ★ EventSystem.currentInputModule을 보면 안 된다 — 그 프로퍼티는 EventSystem.Update()가
            // 한 번 돌아야 채워지므로 Start() 시점에는 모듈이 멀쩡히 붙어 있어도 null로 보인다(첫
            // 실측에서 "입력 모듈 없음"으로 잘못 보고했다). 컴포넌트 존재 자체를 확인한다.
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<BaseInputModule>() : null;
            Debug.Log("[투두] 포스트잇 위젯 준비 완료 — " +
                $"EventSystem={(EventSystem.current != null ? "있음" : "★없음(uGUI 클릭 불가)")}, " +
                $"입력 모듈={(module != null ? module.GetType().Name + "(활성=" + module.isActiveAndEnabled + ")" : "★없음(Button.onClick이 영원히 발동하지 않는다)")}, " +
                $"전역 폴링 경로={(_buttonService != null ? "사용 가능" : "미지원 — uGUI 경로만")}, " +
                $"클릭관통 차단막={(_clickThroughBlocker != null ? "준비됨" : "★없음")}. " +
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
            ApplyCanvasScaleFactor(); // 배율은 실행 중에 바뀔 수 있다(모니터 이동/시작 직후 창 확장).
            TickGlobalClickPolling();
            SyncClickThroughBlocker();

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
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();

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
            _hideButton.onClick.AddListener(() => { if (TryClaimAction("hide")) OnHideClicked(); });

            var rowContainerGo = new GameObject("Rows", typeof(RectTransform));
            rowContainerGo.transform.SetParent(panelGo.transform, false);
            _rowContainer = rowContainerGo.GetComponent<RectTransform>();
            _rowContainer.anchorMin = Vector2.zero;
            _rowContainer.anchorMax = Vector2.one;
            _rowContainer.offsetMin = Vector2.zero;
            _rowContainer.offsetMax = new Vector2(0f, -RowHeight); // 상단 [숨기기] 줄 아래부터 시작

            _moreButton = CreateSmallButton(rowContainerGo.transform, "MoreButton", "[+N개 더보기]", new Vector2(0f, 1f), Vector2.zero);
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
            row.CheckboxButton.onClick.AddListener(() =>
            {
                if (TryClaimAction("row" + row.TodoId)) OnRowCheckboxClicked(row);
            });
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
