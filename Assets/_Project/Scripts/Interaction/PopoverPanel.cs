using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 부채꼴 버튼에서 자라나는 <b>팝오버</b>의 공통 뼈대 — docs/UX_FLOW.md 32-3 / 32-7.
    /// <see cref="FocusSessionPopover"/>와 <see cref="TodoBoardPopover"/>가 이걸 상속한다.
    ///
    /// ============================================================================
    /// 왜 캐릭터 창(680×520)이 아니라 팝오버인가
    /// ============================================================================
    /// "25분 타이머 켜기"와 "할일 하나 체크"는 2초짜리 심부름이다. 2초 심부름에 화면 1/6을 덮는 것이
    /// 곧 이 앱이 스스로 금지한 업무 방해다(원칙 2). 크기는 다르되 크롬(모서리 반지름/여백/글자 크기/
    /// [✕] 위치)은 캐릭터 창과 100% 같은 <see cref="UiChrome"/> 토큰만 쓴다 — 사용자는 바운딩 박스가
    /// 아니라 시각 언어로 같은 가족임을 읽는다.
    ///
    /// ============================================================================
    /// 클릭 경로는 이 프로젝트의 기존 3중 관례 그대로
    /// ============================================================================
    ///  (1) uGUI <see cref="Button"/> — 앱이 활성일 때의 표준 경로.
    ///  (2) 전역 폴링 + 사각형 히트테스트 — macOS에서 비활성 앱의 첫 클릭이 "앱 활성화"에만 소비되는
    ///      경우에도 버튼이 먹게 한다(CharacterInfoWindow와 같은 구현).
    ///  (3) <see cref="TryClaimAction"/> 0.35초 중복 제거 — (1)과 (2)가 같은 클릭에 둘 다 반응해
    ///      한 번 누른 것이 두 번 처리되는 사고를 막는다.
    /// <b>차단막(BoxCollider2D)은 열려 있는 동안, 패널 사각형만</b> 덮고 닫히는 즉시 꺼진다 —
    /// 그 밖의 화면은 100% 클릭관통 그대로다(비침해).
    /// </summary>
    public abstract class PopoverPanel : MonoBehaviour, IExclusiveSurface
    {
        // ==================== 공통 상수 (32-2 / 32-3) ====================

        /// <summary>버튼에서 자라나는 시간. 0.16초, scale 0.92 -> 1, alpha 0 -> 1.</summary>
        protected const float GrowSeconds = 0.16f;

        /// <summary>닫힐 때. 여는 것보다 빨라야 한다(닫기는 "취소"라 즉시성이 가치다).</summary>
        protected const float ShrinkSeconds = 0.12f;

        protected const float GrowStartScale = 0.92f;

        /// <summary>누른 버튼 원 가장자리에서 이만큼 띄운다.</summary>
        protected const float AnchorGapPoints = 10f;

        /// <summary>화면 여백 — 팝오버 전체가 이 안에 들어온다.
        /// <para>★ <b>public</b>인 이유: 안전영역 클램프를 검증하는 테스트가 이 값을 <b>숫자로 베끼지
        /// 않고</b> 참조해야 한다(CLAUDE.md — 프로덕션 상수 하드코딩 금지). <c>ActionDedupSeconds</c>가
        /// 같은 사정으로 public이다.</para></summary>
        public const float ScreenMarginPoints = 12f;

        protected const float ClickPollInterval = 0.05f;
        protected const float ActionDedupSeconds = 0.35f;

        // ==================== 무입력 자동 닫힘 (2026-09-01 페르소나 J5) ====================
        //
        // ★ 왜 이것이 절전이 아니라 <b>비침해(원칙 2)</b> 문제인가:
        //   "톱니 → [오늘 할일] → 자리 비움"이면 부채꼴의 6초 자동 접힘이 <b>무력화</b>된다 —
        //   GearRadialMenuWidget.TickAutoCollapse가 `AnyPopoverOpen()`이면 타이머를 리셋하기 때문이다
        //   (읽고 있는 창을 시간으로 닫지 않겠다는, 그 자체로는 옳은 규칙). 그런데 이 클래스에는
        //   자기 몫의 자동 닫힘이 없었으므로 팝오버와 <b>그 클릭관통 차단막</b>이 밤새 남았다.
        //   바탕화면 한 조각의 클릭관통이 밤새 해제된 채 남는 것은 이 앱이 스스로 금지한 침해다.
        //
        // ★ 왜 부채꼴의 6초가 아니라 3분인가: 부채꼴 버튼은 <b>지나가는 표적</b>이지만 팝오버는
        //   사용자가 지금 읽고 쓰는 내용이다. 6초는 "할일을 훑는 동안" 닫히고, 그건 편의가 아니라
        //   사고다(위 TickAutoCollapse 문서와 같은 판단). 3분은 사람이 화면 앞에서 만들 수 있는
        //   무입력이 아니다 — 자리를 뜬 것이다.
        //
        // ★ 무입력의 근거를 <b>스스로</b> 관측한다(FramePacing.LastPresence를 읽지 않는다):
        //   그쪽은 "진단/테스트 창구"로 선언된 값이고, 적응형 페이싱이 꺼져 있으면 갱신되지 않아
        //   "관측값이 없다"와 "입력이 있었다"가 구분되지 않는다. 그 상태에서 닫지 <b>못하는</b> 쪽으로
        //   기울면 정확히 이 버그가 되살아난다. 여기서는 신호가 없을수록 닫히는 쪽으로 기운다.

        /// <summary>무입력이 이만큼 이어지면 팝오버가 스스로 닫힌다(초).</summary>
        public const float DefaultIdleAutoCloseSeconds = 180f;

        private static float _idleAutoCloseSeconds = DefaultIdleAutoCloseSeconds;

        /// <summary>지금 쓰이는 임계(초). 제품은 기본값을 그대로 쓰고 <b>테스트만</b> 낮춘다 —
        /// 3분을 진짜로 기다리는 테스트는 만들지 않는다.</summary>
        public static float IdleAutoCloseSeconds => _idleAutoCloseSeconds;

        public static void SetIdleAutoCloseSecondsForTests(float seconds)
            => _idleAutoCloseSeconds = Mathf.Max(0.05f, seconds);

        public static void ResetIdleAutoCloseSecondsForTests()
            => _idleAutoCloseSeconds = DefaultIdleAutoCloseSeconds;

        /// <summary>무입력 시계를 다시 재는 주기(초). 3분짜리 판정에 매 프레임 OS 커서를 물을 이유가 없다.</summary>
        private const float IdlePollInterval = 0.25f;

        /// <summary>이보다 작은 커서 이동은 손떨림/좌표 반올림으로 본다(픽셀).</summary>
        private const float IdleCursorEpsilonPixels = 2f;

        /// <summary>포스트잇/캐릭터 창보다 위, 부채꼴보다도 위 — 팝오버는 방금 사용자가 부른 것이다.</summary>
        private const int SortingOrder = 31700;

        // ==================== 공통 상태 ====================

        protected StickmanAgent Agent;
        protected StickConfig Config;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private CanvasGroup _group;
        private RectTransform _panel;
        private BoxCollider2D _clickBlocker;
        private IGlobalPointerButtonService _buttonService;

        private bool _open;
        private float _animTimer;
        private bool _closing;
        private Vector2 _anchorCenterScreen;
        private float _anchorRadiusScreen;
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private string _lastActionKey;
        private float _lastActionTime;

        private float _idleSeconds;
        private float _idlePollTimer;
        private Vector2 _lastCursorSample;
        private bool _hasCursorSample;
        private bool _hasTestCursor;
        private Vector2 _testCursor;

        public bool IsOpen => _open;

        // ★ 배타 표면 등록(2026-09-01) — 이 한 벌로 FocusSessionPopover/TodoBoardPopover/
        //   ActionCommandPopover 셋이 전부 등록된다. 명시적 구현이라 공개 API는 그대로다.
        bool IExclusiveSurface.IsSurfaceOpen => _open;
        void IExclusiveSurface.CloseSurface(string reason) => Close(reason);

        /// <summary>지금까지 누적된 무입력 시간(초) — 진단/테스트 창구.</summary>
        public float IdleSecondsForTests => _idleSeconds;

        /// <summary>패널이 실제로 켜져 있는가(진단/테스트 전용) — 플래그가 아니라 GameObject의 실제 상태.</summary>
        public bool IsCanvasActive => _canvas != null && _canvas.gameObject.activeSelf;

        /// <summary>클릭관통 차단막이 켜져 있는가(진단/테스트 전용). 팝오버가 사라진 뒤에도 이것이
        /// 남으면 그 화면 영역의 클릭관통이 영영 해제된 채 남는다(비침해 원칙 2).</summary>
        public bool IsClickBlockerEnabled => _clickBlocker != null && _clickBlocker.enabled;

        /// <summary>패널 사각형(Unity 스크린 픽셀) — 바깥 클릭 판정/차단막이 쓰는 값.</summary>
        public Rect PanelScreenRect { get; private set; }

        protected RectTransform Panel => _panel;

        protected abstract Vector2 PanelSizePoints { get; }
        protected abstract string TitleText { get; }

        /// <summary>내용은 자식이 만든다. <paramref name="content"/>는 여백이 적용된 안쪽 영역이다.</summary>
        protected abstract void BuildContent(RectTransform content);

        /// <summary>열릴 때/값이 바뀔 때 화면을 실제 값으로 다시 칠한다.</summary>
        protected abstract void RefreshContent();

        /// <summary>전역 폴링이 잡은 클릭(패널 안). 자식이 자기 버튼 사각형과 대조한다.</summary>
        protected abstract void OnGlobalClick(Vector2 cursorUnityScreen);

        /// <summary>느린 갱신(0.25초 주기) — 타이머 같은 실시간 값이 있는 팝오버만 쓴다.</summary>
        protected virtual void TickSlow() { }

        // ==================== 수명 주기 ====================

        protected virtual void Awake()
        {
            Agent = GetComponent<StickmanAgent>();
            Config = Agent != null ? Agent.Config : null;
            BuildChrome();
        }

        protected virtual void Start()
        {
            _buttonService = Agent != null ? Agent.PlatformService as IGlobalPointerButtonService : null;
        }

        protected virtual void OnDisable()
        {
            // 팝오버가 꺼진 채 차단막만 남으면 그 영역이 이유 없이 클릭관통 해제로 남는다(비침해).
            if (_clickBlocker != null) _clickBlocker.enabled = false;
        }

        protected virtual void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_clickBlocker != null) Destroy(_clickBlocker.gameObject);
        }

        // ==================== 열기 / 닫기 ====================

        /// <param name="anchorScreenRect">자라나기 시작할 버튼 원의 사각형(Unity 스크린 픽셀).</param>
        public void Open(Rect anchorScreenRect, string source)
        {
            _anchorCenterScreen = anchorScreenRect.center;
            _anchorRadiusScreen = Mathf.Max(anchorScreenRect.width, anchorScreenRect.height) * 0.5f;

            if (_open && !_closing) { RefreshContent(); return; }

            _open = true;
            _closing = false;
            _animTimer = 0f;
            _leftInitialized = false;   // 여는 그 클릭이 곧바로 행 클릭으로 오인되지 않게.
            NoteUserActivity();         // 무입력 시계는 열리는 순간부터 다시 센다.
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_clickBlocker != null) _clickBlocker.enabled = true;
            UpdatePlacement();
            RefreshContent();
            OnOpened();
            Debug.Log($"[팝오버] {TitleText} 열림({source}) — {PanelSizePoints.x:F0}×{PanelSizePoints.y:F0}pt. " +
                "[✕] / 바깥 클릭 / 버튼 재클릭으로 닫힙니다.");
        }

        public void Close(string reason)
        {
            if (!_open || _closing) return;
            _closing = true;
            _animTimer = 0f;
            OnClosing();
            Debug.Log($"[팝오버] {TitleText} 닫힘 — {reason}.");
        }

        protected virtual void OnOpened() { }
        protected virtual void OnClosing() { }

        private void Hide()
        {
            _open = false;
            _closing = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_clickBlocker != null) _clickBlocker.enabled = false;
        }

        // ==================== 루프 ====================

        private float _slowTimer;

        protected virtual void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측
            if (!_open) return;

            // ★★ 절대 불변 원칙 2(비침해) — 전체화면 게임이 감지되면 즉시 거둔다.
            // StickmanAgent.Suspend()가 끄는 것은 Awake에서 캐시한 캐릭터 렌더러뿐이라, 이 캔버스와
            // 씬 루트 BoxCollider2D 차단막은 그대로 남아 있었다. macOS 히트테스트가 커서 아래 픽셀
            // 알파를 보므로 남아 있으면 <b>전체화면 게임 위에서 클릭까지 먹는다</b>.
            // 접힘 연출(0.12초) 대신 Hide()로 한 프레임에 치우는 이유: 이건 사용자가 닫은 게 아니고,
            // 그 0.12초 동안에도 차단막이 살아 있기 때문이다. 복귀 시 강제로 다시 열지는 않는다
            // (WindowCrashDirector가 오버레이를 되살리지 않는 것과 같은 판단).
            if (Agent != null && Agent.IsSuspended)
            {
                if (!_closing) OnClosing();   // 이미 닫히는 중이면 Close()가 이미 불렀다(이중 호출 금지).
                Hide();
                Debug.Log($"[팝오버] {TitleText} — 전체화면 감지로 즉시 닫힘(차단막 포함, 비침해 원칙 2).");
                return;
            }

            // ★ 반드시 먼저 확인하고 빠져나간다: 이 프레임에 접힘이 끝나 Hide()가 돌았는데도 아래
            //   SyncClickBlocker()가 이어서 돌면, 그 안의 `enabled = !_closing`이 (Hide가 _closing을
            //   false로 되돌린 뒤라) 차단막을 <b>도로 켜버린다</b>. 그러면 팝오버가 사라진 뒤에도 그
            //   화면 영역의 클릭관통이 영영 해제된 채 남는다(비침해 위반).
            //   PlayMode의 InfoGearRadialMenuTests가 실제로 이 사고를 잡아냈다.
            if (!TickAnimation()) return;

            ApplyCanvasScaleFactor();
            UpdatePlacement();
            SyncClickBlocker();
            TickGlobalClickPolling();
            if (TickIdleAutoClose()) return;   // 이번 프레임에 스스로 닫았다.

            _slowTimer += Time.unscaledDeltaTime;
            if (_slowTimer < 0.25f) return;
            _slowTimer = 0f;
            TickSlow();
        }

        /// <summary>
        /// 무입력 자동 닫힘(위 상수 문단이 근거) — <b>true면 이번 프레임에 닫았다</b>.
        ///
        /// <para>입력의 정의는 "커서가 움직였거나 눌렸다"이다. 키보드만 두드리는 사용자는 이 시계를
        /// 멈추지 못하는데, 그것은 의도된 선택이다 — 이 창은 2초짜리 심부름용이고(클래스 문서),
        /// 3분 동안 커서가 1px도 안 움직였다면 이 창은 이미 쓰이고 있지 않다.</para>
        /// </summary>
        private bool TickIdleAutoClose()
        {
            // 키보드도 입력이다 — [오늘 할일]의 입력칸에 타이핑하는 동안 창이 닫히면 그건 사고다.
            // Input.anyKey는 <b>이 앱이 포커스를 가졌을 때만</b> 참이므로, 남의 앱에서 치는 키는
            // 여기 잡히지 않는다(그게 우리가 원하는 정의다 — 이 창을 쓰고 있는 손만 시계를 멈춘다).
            if (Input.anyKey) { NoteUserActivity(); return false; }

            _idlePollTimer += Time.unscaledDeltaTime;
            if (_idlePollTimer < IdlePollInterval) return false;
            float elapsed = _idlePollTimer;
            _idlePollTimer = 0f;

            if (TryGetIdleCursor(out Vector2 cursor))
            {
                if (!_hasCursorSample ||
                    (cursor - _lastCursorSample).sqrMagnitude > IdleCursorEpsilonPixels * IdleCursorEpsilonPixels)
                {
                    _hasCursorSample = true;
                    _lastCursorSample = cursor;
                    _idleSeconds = 0f;
                    return false;
                }
            }

            _idleSeconds += elapsed;
            if (_idleSeconds < IdleAutoCloseSeconds) return false;

            Close($"무입력 {IdleAutoCloseSeconds:F0}초 — 자리를 비운 것으로 보고 차단막까지 거둡니다(원칙 2)");
            return true;
        }

        /// <summary>무입력 판정이 볼 커서. 주입된 값이 있으면 그것을 쓴다 — PlayMode는 진짜 OS 커서를
        /// 원하는 자리에 <b>붙잡아 둘</b> 수 없고(테스트 도중 사람이 마우스를 건드리면 시계가 리셋된다),
        /// 그렇다고 판정 로직을 테스트용으로 우회하면 "테스트만 통과하는 코드"가 된다.
        /// <see cref="InfoGearIconWidget.FeedHoverCursorForTests"/>와 같은 관례다.</summary>
        private bool TryGetIdleCursor(out Vector2 cursorUnityScreen)
        {
            if (_hasTestCursor) { cursorUnityScreen = _testCursor; return true; }
            if (Agent != null && Agent.TryGetCursorPosition(out Vector2 osScreen))
            {
                cursorUnityScreen = ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, Config);
                return true;
            }
            cursorUnityScreen = default;
            return false;
        }

        /// <summary>테스트 전용 — 무입력 판정이 볼 커서를 이 자리에 고정한다.</summary>
        public void FeedIdleCursorForTests(Vector2 cursorUnityScreen)
        {
            _hasTestCursor = true;
            _testCursor = cursorUnityScreen;
        }

        /// <summary>주입한 커서를 걷고 실제 OS 커서로 되돌린다.</summary>
        public void ClearIdleCursorForTests() => _hasTestCursor = false;

        /// <summary>사용자가 이 창을 실제로 만졌다 — 무입력 시계를 0으로 되돌린다.
        /// 자식 팝오버가 자기만의 입력 경로(예: 텍스트 입력)를 가질 때 직접 부를 수 있게 protected다.</summary>
        protected void NoteUserActivity()
        {
            _idleSeconds = 0f;
            _idlePollTimer = 0f;
            _hasCursorSample = false;
        }

        /// <summary>false면 이번 프레임에 완전히 닫혔다는 뜻 — 호출자는 즉시 빠져나가야 한다.</summary>
        private bool TickAnimation()
        {
            _animTimer += Time.unscaledDeltaTime;
            if (_closing)
            {
                float k = Mathf.Clamp01(_animTimer / ShrinkSeconds);
                SetGrow(1f - k);
                if (k < 1f) return true;
                Hide();
                return false;
            }

            float t = Mathf.Clamp01(_animTimer / GrowSeconds);
            SetGrow(t);
            return true;
        }

        private void SetGrow(float t)
        {
            if (_group != null) _group.alpha = t;
            if (_panel == null) return;
            float s = Mathf.Lerp(GrowStartScale, 1f, t);
            _panel.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>
        /// 누른 버튼에서 <b>화면 안쪽</b>으로 눕힌다. 부채꼴이 이미 화면 안쪽으로 열린 뒤이므로 이
        /// 규칙만으로 자연히 안쪽에 앉고, 마지막에 화면 여백 12pt로 전체를 클램프한다.
        /// </summary>
        private void UpdatePlacement()
        {
            if (_panel == null) return;

            float pxPerPoint = ScreenCoordinateConverter.CanvasToUnityScreen(1f, Config);
            Vector2 size = PanelSizePoints * pxPerPoint;
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Vector2 dir = screenCenter - _anchorCenterScreen;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.down;
            dir.Normalize();

            // 팝오버 중심까지의 거리 = 버튼 반지름 + 간격 + 그 방향의 패널 반폭.
            float halfExtent = Mathf.Abs(dir.x) * size.x * 0.5f + Mathf.Abs(dir.y) * size.y * 0.5f;
            Vector2 center = _anchorCenterScreen + dir * (_anchorRadiusScreen + AnchorGapPoints * pxPerPoint + halfExtent);

            float margin = ScreenMarginPoints * pxPerPoint;
            float minX = margin + size.x * 0.5f;
            center.x = Mathf.Clamp(center.x, minX, Mathf.Max(minX, Screen.width - minX));

            // ★ 2026-09-02 — 세로는 <b>대칭이 아니다</b>. 옛 코드는 네 변에 똑같이 12pt를 줘서 팝오버를
            //   상단 y=12pt에 앉혔고, macOS 메뉴바(y 0~33pt)를 <b>21pt 덮었다</b>(원칙 2 위반).
            //   위쪽만 OS 예약 띠만큼 더 밀어낸다 — 아래쪽은 그대로다(Dock은 캐릭터의 발판이다).
            float topInsetPx = ReservedTopBarProbe.TopInsetPoints(Agent != null ? Agent.PlatformService : null)
                * pxPerPoint;
            center.y = SurfaceSafeAreaPolicy.ClampCenterY(center.y, size.y, Screen.height, topInsetPx, margin);

            PanelScreenRect = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
            _panel.anchoredPosition = new Vector2(
                ScreenCoordinateConverter.UnityScreenToCanvas(center.x, Config),
                ScreenCoordinateConverter.UnityScreenToCanvas(center.y, Config));
        }

        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(Config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
        }

        private void SyncClickBlocker()
        {
            if (_clickBlocker == null || _panel == null) return;
            Camera cam = Agent != null && Agent.Blackboard != null ? Agent.Blackboard.MainCamera : Camera.main;
            if (cam == null) { _clickBlocker.enabled = false; return; }

            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(PanelScreenRect.xMin, PanelScreenRect.yMin, Mathf.Abs(cam.transform.position.z)));
            Vector3 tr = cam.ScreenToWorldPoint(new Vector3(PanelScreenRect.xMax, PanelScreenRect.yMax, Mathf.Abs(cam.transform.position.z)));
            _clickBlocker.enabled = !_closing;
            _clickBlocker.transform.position = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, 0f);
            _clickBlocker.size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
        }

        private void TickGlobalClickPolling()
        {
            if (_buttonService == null || _closing) return;

            _clickPollTimer += Time.unscaledDeltaTime;
            if (_clickPollTimer < ClickPollInterval) return;
            _clickPollTimer = 0f;

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            if (!_leftInitialized) { _leftInitialized = true; _leftPrev = left; return; }
            bool rising = left && !_leftPrev;
            _leftPrev = left;
            if (!rising) return;

            if (Agent == null || !Agent.TryGetCursorPosition(out Vector2 osScreen)) return;
            Vector2 cursor = ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, Config);
            FeedClick(cursor);
        }

        /// <summary>
        /// 테스트 전용 진입점 — 실제 입력과 <b>완전히 같은 처리 경로</b>에 커서를 먹인다(PlayMode는
        /// 진짜 전역 클릭을 만들 수 없다 — InfoGearIconWidget.FeedPointerForTests와 같은 사정).
        /// </summary>
        public void FeedClickForTests(Vector2 cursorUnityScreen) => FeedClick(cursorUnityScreen);

        private void FeedClick(Vector2 cursor)
        {
            if (!_open || _closing) return;
            NoteUserActivity();
            if (!PanelScreenRect.Contains(cursor))
            {
                Close("팝오버 바깥 클릭");
                return;
            }
            if (ContainsScreenPoint(CloseButtonRect, cursor))
            {
                if (TryClaimAction("close")) Close("[✕] 클릭");
                return;
            }
            OnGlobalClick(cursor);
        }

        // ==================== 공통 도구 ====================

        protected bool TryClaimAction(string key)
        {
            // uGUI 경로의 클릭도 여기를 지난다 — 무입력 시계를 되돌릴 유일한 공통 길목이다.
            NoteUserActivity();
            if (_lastActionKey == key && Time.unscaledTime - _lastActionTime < ActionDedupSeconds) return false;
            _lastActionKey = key;
            _lastActionTime = Time.unscaledTime;
            return true;
        }

        /// <summary>
        /// <see cref="RectTransform.GetWorldCorners"/>가 <b>채워 주는</b> 4칸 버퍼 — 호출마다
        /// <c>new Vector3[4]</c>를 만들지 않는다(Core/DockGeometry.cs가 명문화한 무할당 관례. 이 앱은
        /// 하루 종일 켜져 있고, TodoBoardPopover의 행 판정은 클릭 한 번에 이 함수를 여러 번 부른다).
        ///
        /// 왜 재사용이 안전한가: (1) 값은 <b>호출 즉시 읽고 버린다</b> — 두 함수 모두 같은 문장 안에서
        /// Rect/bool로 환원하고 버퍼 참조를 밖으로 내보내지 않는다. (2) Unity의 Transform API는 메인
        /// 스레드 전용이라 이 정적 버퍼에 동시 진입할 경로가 없다. (3) 재진입도 없다(둘 다 다른 사용자
        /// 코드를 호출하지 않는다).
        /// </summary>
        private static readonly Vector3[] CornerBuffer = new Vector3[4];

        /// <summary>어떤 부품의 화면 사각형(Unity 스크린 픽셀). 테스트가 실제 클릭 경로로 누를 좌표를
        /// 여기서 얻는다 — 좌표를 손으로 적어두면 레이아웃이 바뀔 때 조용히 엉뚱한 곳을 누른다.</summary>
        public static Rect ScreenRectOf(RectTransform rt)
        {
            if (rt == null) return new Rect();
            rt.GetWorldCorners(CornerBuffer);
            return Rect.MinMaxRect(CornerBuffer[0].x, CornerBuffer[0].y, CornerBuffer[2].x, CornerBuffer[2].y);
        }

        /// <summary>ScreenSpaceOverlay 캔버스에서는 RectTransform의 월드 좌표가 곧 스크린 픽셀 좌표다.</summary>
        protected static bool ContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(CornerBuffer);
            return screenPoint.x >= CornerBuffer[0].x && screenPoint.x <= CornerBuffer[2].x &&
                   screenPoint.y >= CornerBuffer[0].y && screenPoint.y <= CornerBuffer[2].y;
        }

        /// <summary>같은 클릭을 uGUI와 전역 폴링이 둘 다 처리하지 않게, 버튼 배선은 이 한 곳을 지난다.</summary>
        protected void Wire(Image surface, string actionKey, System.Action action)
        {
            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() => { if (TryClaimAction(actionKey)) action(); });
        }

        // ==================== 크롬 만들기 ====================

        private void BuildChrome()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject(GetType().Name + "Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            // ★★ 씬 루트에 단다(캐릭터의 자식이 아니다) — InfoGearIconWidget._container / 부채꼴
            // 캔버스와 같은 전례. ScreenSpaceOverlay 캔버스는 화면 좌표계에 사는 물건이라 걷고 넘어지는
            // 캐릭터의 Transform 계보에 속할 이유가 없고, 캐릭터 자손으로 두면 이 캔버스 안의 UI 이름이
            // 이름으로 캐릭터 파츠를 찾는 코드(StickmanPoseAnimator/StickmanMetrics/EyeController 등)에
            // 걸릴 수 있다 — 2026-08-30에 부채꼴의 "Head"로 실제로 터진 사고다. 차단막(아래 Blocker)은
            // 이미 씬 루트이며, 정리는 OnDestroy가 둘 다 책임진다.
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();
            _group = canvasGo.AddComponent<CanvasGroup>();

            // ★ 2026-08-31 "뒤 창이 비쳐 보인다" 회귀 수정 — 정보창(CharacterInfoWindow)과 같은 원인이
            //   이 창에도 남아 있었다. 옛 코드는 패널 <b>Image의 자식으로</b> 그림자를 달고
            //   SetAsFirstSibling()으로 뒤로 보내려 했지만, uGUI는 <b>부모 Graphic을 자식보다 먼저</b>
            //   그리므로 형제 순서를 어떻게 바꿔도 그림자는 본체 <b>위</b>를 벗어나지 못한다. 그 결과
            //   창 알파가 1 → 0.7525(키 α0.55) → <b>0.6202</b>(앰비언트 α0.28)로 무너져 사용자의
            //   데스크톱이 <b>38% 비쳐 들었다</b>(UiChrome 파일 머리 "알파 채널의 법칙" (2)).
            //   AddOpaquePanel은 그림 없는 컨테이너에 [본체 → 보더]를 <b>형제로</b> 배치해 같은 그림을
            //   창 알파 1.0으로 만든다. 반환값이 컨테이너이므로 아래 배치 코드는 그대로다.
            //   ★ 2026-09-02 — 그림자 겹은 전부 삭제됐다(사용자 지시). 이 창의 둘레는 보더 1px뿐이다.
            _panel = UiChrome.AddOpaquePanel(canvasGo.transform, "Panel", UiChrome.RadiusPanel, out _);
            // 앵커를 좌하단에 두고 피벗을 가운데로 — anchoredPosition이 곧 "캔버스 포인트 좌표의 중심"이 된다.
            _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = PanelSizePoints;

            // ★ 2026-09-02 (41-3 / C3) — 타이틀 줄의 가로 예산을 <b>여기 한 곳</b>에서 나눈다.
            //   이 베이스에 넣으면 팝오버 3종이 자동으로 닫기 힌트를 얻는다(ExclusiveSurfaces가
            //   인터페이스로 자동 등록한 것과 같은 정신 — "잊을 자리를 안 만든다").
            float closeLeft = PanelSizePoints.x - UiChrome.Space4 - 22f;
            float hintRight = closeLeft - UiChrome.CloseHintGap;
            float hintRoom = hintRight - (UiChrome.Space4 + TitleReservePoints + UiChrome.Space1);
            bool showCloseHint = hintRoom >= UiChrome.CloseHintMinWidth;
            float hintWidth = showCloseHint ? Mathf.Min(UiChrome.CloseHintWidth, hintRoom) : 0f;
            // 힌트를 지운 경우의 제목 폭은 <b>예전과 한 픽셀도 다르지 않다</b>(closeLeft - 16 - 4 = 422 @480).
            float titleWidth = showCloseHint
                ? hintRight - hintWidth - UiChrome.Space1 - UiChrome.Space4
                : closeLeft - UiChrome.Space1 - UiChrome.Space4;

            Text title = UiChrome.AddText(_panel, "Title", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, bold: true);
            _titleText = title;
            UiChrome.PlaceTopLeft(title.rectTransform, UiChrome.Space4, -UiChrome.Space3, titleWidth, 22f);
            title.text = TitleText;

            if (showCloseHint)
            {
                _closeHint = UiChrome.AddText(_panel, "CloseHint", UiChrome.FontCaption,
                    TextAnchor.MiddleRight, UiChrome.InkMeta);
                UiChrome.PlaceTopLeft(_closeHint.rectTransform, hintRight - hintWidth, -UiChrome.Space3,
                    hintWidth, 22f);
                _closeHint.text = UiChrome.CloseHintText;
                _closeHint.raycastTarget = false;   // 글자는 버튼이 아니다 — 눌러도 창 밖 클릭 판정을 가리지 않는다.
            }

            Image close = UiChrome.AddSurface(_panel, "Close", UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(close.rectTransform, closeLeft, -UiChrome.Space3, 22f, 22f);
            UiChrome.AddOutline(close.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
            Text closeLabel = UiChrome.AddText(close.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(closeLabel.rectTransform);
            closeLabel.text = "✕";
            CloseButtonRect = close.rectTransform;
            Wire(close, "close", () => Close("[✕] 클릭"));

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_panel, false);
            var content = contentGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(content, UiChrome.Space4, -(UiChrome.Space3 + 22f + UiChrome.Space2),
                PanelSizePoints.x - UiChrome.Space4 * 2f,
                PanelSizePoints.y - (UiChrome.Space3 + 22f + UiChrome.Space2) - UiChrome.Space4);
            BuildContent(content);

            var blockerGo = new GameObject(GetType().Name + "Blocker");
            _clickBlocker = blockerGo.AddComponent<BoxCollider2D>();
            _clickBlocker.isTrigger = true;   // 캐릭터 물리에는 전혀 관여하지 않는다.
            _clickBlocker.enabled = false;

            canvasGo.SetActive(false);
        }

        /// <summary>[✕] 버튼 사각형 — 전역 폴링 경로가 이 사각형을 직접 검사한다.</summary>
        protected RectTransform CloseButtonRect { get; private set; }

        /// <summary>
        /// 닫기 힌트를 얹기 전에 <b>제목에 먼저 떼어 주는</b> 가로 폭(pt).
        ///
        /// <para>★ 41-3 ③은 "팝오버 3종 중 가장 좁은 것"을 480(행동창)으로 적었지만 <b>사실이 아니다</b> —
        /// 실제 최소는 <see cref="FocusSessionPopover"/> 244pt이고 <see cref="TodoBoardPopover"/>가 300pt다.
        /// 그래서 41-3 ④가 규칙만 정해 두고 "실제로 발생하지 않는다"고 적은 예외가 <b>실제로 발생한다</b>.
        /// 그 예외 그대로 처리한다: 자리가 모자라면 <b>힌트를 먼저 지운다</b>(제목이 우선).</para>
        ///
        /// <para>84 = 한글 6자(FontTitle 14pt). 이 값에서 [행동 명령](480)과 [오늘 할일](300)은 힌트를
        /// 얻고, [집중 모드](244)는 못 얻는다 — 그쪽 제목은 진행 중일 때 <c>집중 모드 · 진행 중</c>으로
        /// 늘어나 140pt를 쓰므로 애초에 남는 자리가 없다.</para>
        /// </summary>
        private const float TitleReservePoints = 84f;

        private Text _closeHint;

        /// <summary>닫기 힌트 글자(없으면 null) — 진단/테스트 창구.</summary>
        public Text CloseHintTextForTests => _closeHint;

        /// <summary>제목 글자 — 힌트와 제목이 <b>겹치지 않는가</b>를 재는 창구(좌표를 손으로 적으면
        /// 팝오버 폭이 한 번 바뀔 때 조용히 엉뚱한 곳을 잰다).</summary>
        public Text TitleTextForTests => _titleText;

        /// <summary>[✕] 버튼의 화면 사각형 — 힌트가 그 <b>왼쪽</b>에 있는지 재는 창구.</summary>
        public Rect CloseButtonScreenRectForTests => ScreenRectOf(CloseButtonRect);

        private Text _titleText;

        /// <summary>제목을 실제 상태에 맞춰 바꾼다(예: "집중 모드" -> "집중 모드 · 진행 중").
        /// 값이 실제로 바뀐 프레임에만 쓴다 — 24시간 상주 앱이다.</summary>
        protected void SetTitle(string text)
        {
            if (_titleText == null || _titleText.text == text) return;
            _titleText.text = text;
        }

        /// <summary>씬에 EventSystem이 있어도 입력 모듈이 없으면 Button.onClick이 영원히 발동하지
        /// 않는다(이 프로젝트가 실제로 밟았던 함정) — TodoPostItWidget/CharacterInfoWindow와 같은 보강.</summary>
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
