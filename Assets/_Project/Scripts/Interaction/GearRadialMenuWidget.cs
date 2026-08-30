using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>부채꼴 메뉴의 세 칸. 값(0/1/2)이 곧 부채꼴에서의 슬롯 순서다(θ₀+60 / θ₀ / θ₀−60).</summary>
    public enum GearMenuButton
    {
        FocusMode = 0,
        Character = 1,
        Todo = 2,
    }

    /// <summary>접힘의 종류 — <b>움직임이 서로 달라야</b> "내가 접었다"와 "시간이 지나 닫혔다"가 구분된다.</summary>
    public enum GearMenuCollapseMode
    {
        /// <summary>사용자 동작(기어 재클릭 / 바깥 클릭 / 버튼 선택). 0.13초, 반지름까지 빨려 들어감.</summary>
        User,

        /// <summary>기어를 옮기기 시작함. 0.08초, 가장 빠르게 치운다.</summary>
        Drag,

        /// <summary>6초 무반응. 0.26초, 제자리에서 스르르(반지름 고정, 알파만).</summary>
        Auto,
    }

    /// <summary>
    /// ★ 톱니를 짧게 클릭했을 때 <b>촤르륵 펼쳐지는 원버튼 3개</b> — docs/UX_FLOW.md <b>32절</b> 확정 설계.
    /// 2026-08-30 사용자 원문: "기어메뉴를 클릭했을때 집중모드 버튼 캐릭터 버튼 오늘 할일 버튼 3가지가
    /// 촤르륵 원버튼 3개가 나오고 각 버튼을 클릭했을때 세부 메뉴로 들어가도록".
    ///
    /// ============================================================================
    /// 왜 LineRenderer 선화가 아니라 uGUI인가 (32-9 (E))
    /// ============================================================================
    /// 이 앱의 시각 요소는 두 부류다. <b>감상하는 것</b>(캐릭터/톱니/타이머 링)은 선화, <b>읽고 눌러야
    /// 하는 것</b>(우클릭 메뉴/포스트잇/캐릭터 창)은 불투명 표면 + 테두리 + 그림자다. 부채꼴 버튼은
    /// 후자다 — 사용자의 임의의 바탕화면(사진/코드 편집기) 위에 그려지는데 1.5pt 선 한 겹으로는 대비를
    /// 보장할 방법이 없고, <b>흰 잉크 프리셋에서는 밝은 배경 위에서 사실상 사라진다</b>. 그래서 심볼
    /// 색도 잉크색을 따라가지 않고 <see cref="UiChrome"/>의 고정 색을 쓴다.
    ///
    /// ============================================================================
    /// 입력은 이 컴포넌트가 폴링하지 않는다 (소유권 단일화)
    /// ============================================================================
    /// 커서/버튼 폴링은 <see cref="InfoGearIconWidget"/> 한 곳에서만 한다. 여기는 그 결과를 받아
    /// <see cref="HitTest"/>로 "어느 버튼인가"만 답하고 그림을 그린다. 폴링 주체가 둘이 되면 "누가
    /// 클릭을 먹었는가"가 두 곳에서 판정되는데, 이 프로젝트는 그 함정을 이미 여러 번 밟았다.
    ///
    /// ============================================================================
    /// 화면 밖으로 나가지 않는다 — <b>부채꼴 전체를 회전</b>시킨다 (32-1)
    /// ============================================================================
    /// 개별 버튼을 화면 안으로 밀어 넣으면 모서리에서 세 버튼이 한 점으로 뭉개져 히트 원이 겹치고,
    /// 그러면 <b>보이는 것과 실제로 눌리는 것이 달라진다</b>(먼저 검사되는 버튼이 이긴다). 그래서
    /// 형태를 유지한 채 문제를 푼다: ① θ₀를 ±15°씩 최대 ±90°까지 돌려보고 → ② 세로 일렬 폴백 →
    /// ③ 지름 축소(44→36) → ④ 그래도 안 되면 <b>세 버튼을 같은 벡터로</b> 평행이동(형태 보존).
    ///
    /// 기준각 θ₀는 사분면 부호가 아니라 <b>(화면 중심 − 기어 중심)의 실제 각도를 45° 단위로 스냅</b>한
    /// 값이다. 부호 방식이면 기어가 화면 위쪽 한가운데 있을 때 아래로 곧게 못 펼치고, 중앙선 근처에서
    /// 1픽셀 이동에 방향이 90° 튄다. 그리고 <b>펼치는 순간 한 번 계산해 그 열림이 끝날 때까지 고정</b>한다.
    /// </summary>
    public sealed class GearRadialMenuWidget : MonoBehaviour
    {
        public const int ButtonCount = 3;

        // ==================== 확정 수치 (docs/UX_FLOW.md 32-1 / 32-2) ====================

        public const float ButtonDiameterPoints = 44f;
        public const float ShrunkDiameterPoints = 36f;
        public const float HoverScale = 48f / 44f;
        public const float OrbitRadiusPoints = 62f;
        public const float ButtonAngleStepDegrees = 60f;
        public const float HitPaddingPoints = 4f;
        public const float ScreenMarginPoints = 8f;

        /// <summary>
        /// 화면 <b>위쪽</b>만 여백이 다르다 — macOS 메뉴바(노치 기준 최대 약 38pt)를 덮지 않기 위해서다.
        /// 톱니 자신이 같은 이유로 위에서 58pt에 놓이는데(InfoGearIconWidget.MarginTopPoints), 그 아래
        /// 62pt 궤도를 도는 버튼은 위로 뻗으면 메뉴바를 가릴 수 있다(실측 스크린샷에서 실제로 그랬다).
        /// </summary>
        public const float TopMarginPoints = 40f;

        public const float LabelHeightPoints = 16f;
        public const float LabelGapPoints = 4f;
        public const float ClampBoxExtraWidthPoints = 12f;

        public const float ExpandSecondsPerButton = 0.19f;
        public const float ExpandStaggerSeconds = 0.055f;
        public const float AlphaFadeInSeconds = 0.11f;
        public const float LabelDelaySeconds = 0.10f;
        public const float LabelFadeSeconds = 0.12f;
        public const float StartRadiusFraction = 0.35f;
        public const float StartScale = 0.62f;

        public const float CollapseUserSeconds = 0.13f;
        public const float CollapseDragSeconds = 0.08f;
        public const float CollapseAutoSeconds = 0.26f;
        public const float AutoCollapseIdleSeconds = 6f;

        public const float HoverSeconds = 0.09f;
        public const float PressFlashSeconds = 0.09f;
        public const float MinClickableProgress = 0.5f;

        public const float ColumnFallbackSpacingPoints = 52f;
        public const float RotationSearchStepDegrees = 15f;
        public const float RotationSearchMaxDegrees = 90f;

        /// <summary>
        /// 부채꼴 <b>전체</b>를 이만큼까지는 평행이동해서라도 화면 안에 넣는다 — 세로 일렬로 무너지기 전에.
        ///
        /// 왜 필요한가(실측으로 드러난 기하 모순): 톱니의 기본 위치는 화면 오른쪽 끝에서 30pt다. 실측
        /// 화면(1512×982pt)에서 클램프 상자가 화면 안에 들어오려면 버튼 중심이 θ∈[153°, 256°]에 있어야
        /// 하는데, 그 창은 103°이고 부채꼴은 120°가 필요하다 — <b>어떤 각도로도 회전만으로는 성립하지
        /// 않는다</b>. 실제로 이 단계가 없을 때 기본 위치에서 곧장 세로 일렬 폴백으로 떨어지는 것을
        /// 스크린샷으로 확인했다(= 사용자가 보게 될 기본 화면이 폴백이 된다).
        ///
        /// 평행이동은 <b>형태를 완전히 보존</b>한다(세 버튼의 상대 위치가 그대로다). 32-1이 금지한 것은
        /// 버튼을 <b>따로따로</b> 밀어 호를 찌그러뜨리는 일이지 강체 이동이 아니다.
        /// </summary>
        public const float MaxGroupShiftPoints = 48f;

        /// <summary>세 버튼이 전부 안착하기까지(0.19 + 0.055×2 = 0.30초).</summary>
        public static float ExpandTotalSeconds => ExpandSecondsPerButton + ExpandStaggerSeconds * (ButtonCount - 1);

        /// <summary>포스트잇(30000)·캐릭터 창(31000)보다 위, 앱 제어 메뉴(32760)보다 아래 —
        /// 부채꼴은 방금 사용자가 부른 것이라 다른 상시 패널에 가리면 안 되지만, 긴급 종료 수단
        /// (제어 메뉴)을 가려서는 안 된다.</summary>
        private const int SortingOrder = 31500;
        private const float SymbolStroke = 2.0f;
        private const float SymbolBoxPoints = 24f;

        private static readonly string[] Labels = { "집중 모드", "캐릭터", "오늘 할일" };

        // ==================== 내부 상태 ====================

        private enum Phase { Hidden, Expanding, Open, Collapsing }

        private sealed class ButtonView
        {
            public RectTransform Root;       // 원 + 심볼(스케일 대상).
            public RectTransform Group;      // 원 + 라벨을 함께 옮기는 컨테이너.
            public Image Surface;
            public Image Border;
            public Image Flash;
            public RectTransform Symbol;
            public Image[] SymbolParts;        // 상태에 따라 색이 바뀌는 획(TextPrimary <-> Accent).
            public Image[] SymbolFixedParts;   // 색이 고정된 획(체크마크 = 완료를 뜻하는 Accent) — 알파만 따라간다.
            public Image RingTrack;          // 집중 모드 전용 — 세션 중에는 잔여 시간 호가 된다.
            public Image RingFill;
            public RectTransform LabelPill;
            public Image LabelSurface;
            public Image LabelBorder;
            public Text LabelText;
            public RectTransform Badge;      // 오늘 할일 전용 미완료 배지.
            public Image BadgeSurface;
            public Text BadgeText;

            public Vector2 CenterPoints;     // 최종 안착 위치(캔버스 포인트, 좌하단 원점).
            public float Progress;
            public float Hover;              // 0~1.
            public float FlashTimer = -1f;
            public float LabelWidth = 44f;
            public bool CollapsingNow;
        }

        private StickmanAgent _agent;
        private StickConfig _config;
        private FocusWatchDirector _focusDirector;
        private FocusSessionPopover _focusPopover;
        private TodoBoardPopover _todoPopover;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _root;
        private readonly ButtonView[] _buttons = new ButtonView[ButtonCount];

        private Phase _phase = Phase.Hidden;
        private GearMenuCollapseMode _collapseMode = GearMenuCollapseMode.User;
        private float _timer;
        private float _idleTimer;
        private int _hoverIndex = -1;
        private int _activeIndex = -1;       // 팝오버를 띄운 채 남아 있는 버튼(-1 = 없음).
        private float _diameterPoints = ButtonDiameterPoints;
        private float _baseAngleDegrees = 225f;
        private Vector2 _gearCenterPoints;
        private Vector2 _screenPointsAtLayout;
        private float _labelClockTimer;
        private int _lastShownRemainingSeconds = -1;
        private int _lastShownBadgeCount = -1;

        // ==================== 공개 상태 ====================

        /// <summary>펼쳐져 있는가(펼치는 중 + 팝오버 앵커 상태 포함). 클릭을 받는 상태의 기준.</summary>
        public bool IsExpanded => _phase == Phase.Expanding || _phase == Phase.Open;

        /// <summary>그림이 화면에 남아 있는가(접히는 중 포함).</summary>
        public bool IsVisible => _phase != Phase.Hidden;

        /// <summary>팝오버를 띄운 채 남아 있는 버튼(-1 = 없음).</summary>
        public int AnchoredButton => _activeIndex;

        /// <summary>세 버튼의 <b>클램프 상자</b>를 모두 덮는 사각형(Unity 스크린 픽셀). 톱니가 클릭관통
        /// 차단 콜라이더를 이만큼 넓혀야 버튼 클릭이 밑의 앱으로 새지 않는다.</summary>
        public Rect UnionScreenRect { get; private set; }

        /// <summary>지금 부채꼴이 쓰는 기준각(도). 회귀 테스트가 45° 스냅을 직접 확인한다.</summary>
        public float BaseAngleDegrees => _baseAngleDegrees;

        /// <summary>지금 버튼 지름(포인트) — 축소 폴백이 걸렸는지 알 수 있다.</summary>
        public float ButtonDiameter => _diameterPoints;

        public float ButtonProgress(int index)
            => index >= 0 && index < ButtonCount && _buttons[index] != null ? _buttons[index].Progress : 0f;

        /// <summary>버튼 중심(Unity 스크린 픽셀).</summary>
        public Vector2 ButtonScreenCenter(int index)
        {
            if (index < 0 || index >= ButtonCount || _buttons[index] == null) return Vector2.zero;
            return PointsToScreen(_buttons[index].CenterPoints);
        }

        /// <summary>버튼 원의 클릭 판정 사각형(Unity 스크린 픽셀) — 팝오버 앵커 계산에도 쓴다.</summary>
        public Rect ButtonScreenRect(int index)
        {
            Vector2 c = ButtonScreenCenter(index);
            float r = (_diameterPoints * 0.5f) * PixelsPerPoint;
            return new Rect(c.x - r, c.y - r, r * 2f, r * 2f);
        }

        /// <summary>세 버튼 <b>중심</b> 사이의 최소 거리(포인트) — 겹침 회귀 테스트용.
        /// 아직 <see cref="BuildUi"/> 전(Awake 이전)이면 <see cref="float.MaxValue"/>를 돌려준다:
        /// "가장 좁은 간격"의 항등원이라 어떤 최소값 단언도 통과시키지 않고 조용히 넘어가지 않는다.</summary>
        public float MinimumCenterSpacingPoints()
        {
            float min = float.MaxValue;
            for (int i = 0; i < ButtonCount; i++)
            {
                if (_buttons[i] == null) continue;
                for (int j = i + 1; j < ButtonCount; j++)
                {
                    if (_buttons[j] == null) continue;
                    float d = Vector2.Distance(_buttons[i].CenterPoints, _buttons[j].CenterPoints);
                    if (d < min) min = d;
                }
            }
            return min;
        }

        /// <summary>버튼의 클램프 상자(캔버스 포인트, 좌하단 원점) — 라벨까지 포함한 실제 차지 영역.
        /// 범위 밖/미생성이면 빈 사각형(<see cref="Rect.Contains"/>가 항상 false) — 형제 접근자
        /// <see cref="ButtonScreenCenter"/>/<see cref="ButtonProgress"/>와 같은 가드 규약이다.</summary>
        public Rect ClampBoxPoints(int index)
        {
            if (index < 0 || index >= ButtonCount || _buttons[index] == null) return new Rect();
            return BoxFor(_buttons[index].CenterPoints, _diameterPoints, _buttons[index].LabelWidth);
        }

        // ==================== 수명 주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _config = _agent != null ? _agent.Config : null;
            BuildUi();
        }

        private void Start()
        {
            _focusDirector = GetComponent<FocusWatchDirector>();
            _focusPopover = GetComponent<FocusSessionPopover>();
            _todoPopover = GetComponent<TodoBoardPopover>();
            Debug.Log("[부채꼴] 준비 완료 — 톱니를 짧게 클릭하면 [집중 모드]/[캐릭터]/[오늘 할일] 원버튼 " +
                $"3개가 Ø{ButtonDiameterPoints:F0}pt, 궤도 {OrbitRadiusPoints:F0}pt, 간격 " +
                $"{ButtonAngleStepDegrees:F0}도로 {ExpandTotalSeconds:F2}초 동안 촤르륵 펼쳐집니다. " +
                $"기준각은 (화면 중심 − 기어 중심)을 45도 단위로 스냅해 펼침 순간에 고정하고, 화면 밖이면 " +
                $"부채꼴 전체를 ±{RotationSearchStepDegrees:F0}도씩 최대 ±{RotationSearchMaxDegrees:F0}도까지 " +
                $"회전해 봅니다. {AutoCollapseIdleSeconds:F0}초 동안 커서가 부채꼴 밖이면 자동으로 접힙니다.");
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        // ==================== 열기 / 닫기 ====================

        /// <summary>
        /// 톱니 클릭 프레임(t=0)에 불린다 — 회전이 끝나기를 기다리지 않는다(32-9 (B)). 클릭 후
        /// 100ms 안에 아무 변화가 없으면 사용자는 "안 먹었다"고 판단해 한 번 더 누르고, 그 두 번째
        /// 클릭은 토글 접힘이 되어 메뉴가 깜빡인다 — 실패 모드가 구조적으로 존재하게 된다.
        /// </summary>
        public void Expand(Vector2 gearCenterUnityScreen)
        {
            if (IsExpanded) return;

            _gearCenterPoints = ScreenToPoints(gearCenterUnityScreen);
            ComputeLayout();

            _phase = Phase.Expanding;
            _timer = 0f;
            _idleTimer = 0f;
            _activeIndex = -1;
            _hoverIndex = -1;
            for (int i = 0; i < ButtonCount; i++)
            {
                _buttons[i].Progress = 0f;
                _buttons[i].Hover = 0f;
                _buttons[i].CollapsingNow = false;
            }
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            RefreshDynamicContent(force: true);
            ApplyVisuals();
        }

        public void Collapse(GearMenuCollapseMode mode, string reason)
        {
            if (_phase == Phase.Hidden || _phase == Phase.Collapsing) return;

            ClosePopovers(reason);
            _activeIndex = -1;
            _phase = Phase.Collapsing;
            _collapseMode = mode;
            _timer = 0f;
            _hoverIndex = -1;
            for (int i = 0; i < ButtonCount; i++) _buttons[i].CollapsingNow = true;
            Debug.Log($"[부채꼴] 접힘({ModeLabel(mode)}) — {reason}.");
        }

        private static string ModeLabel(GearMenuCollapseMode mode) => mode switch
        {
            GearMenuCollapseMode.Drag => "이동 시작",
            GearMenuCollapseMode.Auto => "무반응 자동",
            _ => "사용자 동작",
        };

        private static float CollapseSecondsFor(GearMenuCollapseMode mode) => mode switch
        {
            GearMenuCollapseMode.Drag => CollapseDragSeconds,
            GearMenuCollapseMode.Auto => CollapseAutoSeconds,
            _ => CollapseUserSeconds,
        };

        // ==================== 버튼 발동 ====================

        /// <summary>
        /// 눌린 버튼을 실행한다. <b>캐릭터</b>는 세 개가 모두 접히고 창이 뜬다. <b>집중 모드/오늘 할일</b>은
        /// 나머지 2개만 접히고 누른 버튼이 활성 스타일로 남아, 그 버튼에서 팝오버가 자라난다 —
        /// 팝오버에 꼬리를 그리지 않고도 "이 창은 저 버튼에서 나왔다"를 보여주는 가장 싼 방법이다(32-3).
        /// </summary>
        public void Activate(int index)
        {
            if (index < 0 || index >= ButtonCount) return;
            _buttons[index].FlashTimer = 0f;

            // 이미 그 버튼으로 팝오버가 떠 있으면 재클릭 = 완전 종료(32-3).
            if (_activeIndex == index)
            {
                Collapse(GearMenuCollapseMode.User, $"[{Labels[index]}] 재클릭");
                return;
            }

            switch ((GearMenuButton)index)
            {
                case GearMenuButton.Character:
                    ActivateCharacter();
                    return;
                case GearMenuButton.FocusMode:
                    AnchorPopover(index, OpenFocusPopover());
                    return;
                default:
                    AnchorPopover(index, OpenTodoPopover());
                    return;
            }
        }

        private void ActivateCharacter()
        {
            var window = GetComponent<CharacterInfoWindow>();
            if (window == null)
            {
                Debug.LogWarning("[부채꼴] [캐릭터] — CharacterInfoWindow가 없어 건너뜁니다.");
                return;
            }
            window.Toggle("부채꼴 메뉴 [캐릭터]");
            Collapse(GearMenuCollapseMode.User, "[캐릭터] 선택");
        }

        private bool OpenFocusPopover()
        {
            if (_focusPopover == null) _focusPopover = GetComponent<FocusSessionPopover>();
            if (_focusPopover == null)
            {
                Debug.LogWarning("[부채꼴] [집중 모드] — FocusSessionPopover가 없어 건너뜁니다.");
                return false;
            }
            _focusPopover.Open(ButtonScreenRect((int)GearMenuButton.FocusMode), "부채꼴 [집중 모드]");
            return true;
        }

        private bool OpenTodoPopover()
        {
            if (_todoPopover == null) _todoPopover = GetComponent<TodoBoardPopover>();
            if (_todoPopover == null)
            {
                Debug.LogWarning("[부채꼴] [오늘 할일] — TodoBoardPopover가 없어 건너뜁니다.");
                return false;
            }
            _todoPopover.Open(ButtonScreenRect((int)GearMenuButton.Todo), "부채꼴 [오늘 할일]");
            return true;
        }

        /// <summary>누른 버튼만 남기고 나머지를 접는다.</summary>
        private void AnchorPopover(int index, bool opened)
        {
            if (!opened)
            {
                Collapse(GearMenuCollapseMode.User, $"[{Labels[index]}] 열기 실패");
                return;
            }

            _activeIndex = index;
            _phase = Phase.Open;
            _timer = ExpandTotalSeconds;
            for (int i = 0; i < ButtonCount; i++)
            {
                if (i == index) { _buttons[i].CollapsingNow = false; _buttons[i].Progress = 1f; continue; }
                _buttons[i].CollapsingNow = true;
            }
            _collapseMode = GearMenuCollapseMode.User;
            Debug.Log($"[부채꼴] [{Labels[index]}] 선택 — 나머지 2개는 접히고 이 버튼만 활성 스타일로 남습니다.");
        }

        private void ClosePopovers(string reason)
        {
            if (_focusPopover != null) _focusPopover.Close(reason);
            if (_todoPopover != null) _todoPopover.Close(reason);
        }

        private bool AnyPopoverOpen()
            => (_focusPopover != null && _focusPopover.IsOpen) || (_todoPopover != null && _todoPopover.IsOpen);

        // ==================== 히트 테스트 ====================

        /// <summary>커서 아래 버튼(없으면 -1). <b>원</b> 판정이다 — 동그란 버튼을 사각형으로 재면
        /// 모서리에서 "안 눌리는 자리를 눌렀는데 눌린다".</summary>
        public int HitTest(Vector2 cursorUnityScreen)
        {
            if (!IsExpanded) return -1;
            Vector2 p = ScreenToPoints(cursorUnityScreen);
            float r = _diameterPoints * 0.5f + HitPaddingPoints;
            float rSqr = r * r;
            for (int i = 0; i < ButtonCount; i++)
            {
                ButtonView b = _buttons[i];
                if (b.Progress < MinClickableProgress || b.CollapsingNow) continue;
                if ((p - b.CenterPoints).sqrMagnitude <= rSqr) return i;
            }
            return -1;
        }

        /// <summary>커서가 부채꼴 영역(클램프 상자 합집합) 안인가 — 자동 접힘 타이머의 기준.</summary>
        public bool ContainsCursor(Vector2 cursorUnityScreen)
        {
            if (!IsVisible) return false;
            Vector2 p = ScreenToPoints(cursorUnityScreen);
            for (int i = 0; i < ButtonCount; i++)
            {
                if (_buttons[i].CollapsingNow) continue;
                if (ClampBoxPoints(i).Contains(p)) return true;
            }
            return false;
        }

        public void SetHover(int index)
        {
            // 아직 안 보이는 버튼이 호버 강조를 먹으면 클릭 판정과 같은 종류의 거짓말이 된다(32-9 (A)).
            if (index >= 0 && (index >= ButtonCount || _buttons[index] == null ||
                _buttons[index].Progress < MinClickableProgress || _buttons[index].CollapsingNow))
                index = -1;
            _hoverIndex = index;
        }

        /// <summary>커서가 부채꼴 안에 있다는 신호 — 6초 자동 접힘 타이머를 되돌린다.</summary>
        public void KeepAlive() => _idleTimer = 0f;

        // ==================== 매 프레임 ====================

        private void LateUpdate()
        {
            // ★★ 절대 불변 원칙 2(비침해) — 전체화면 게임이 감지되면 상시 표면을 <b>즉시</b> 거둔다.
            // StickmanAgent.Suspend()는 Awake에서 캐시한 캐릭터 렌더러만 끄므로, 씬 루트에 사는 이
            // 캔버스는 그 배열에 없어 그대로 남았다. 히트테스트가 커서 아래 픽셀 알파를 보는 구조라
            // (Platform/macOS의 MacWindowService) 남아 있으면 보이기만 하는 게 아니라 전체화면 게임의
            // 클릭까지 먹는다. 애니메이션(0.13초 접힘)이 아니라 Hide()로 한 프레임에 치우는 이유:
            // 접힘 연출은 "사용자가 닫았다"는 뜻인데 여기는 사용자 동작이 아니고, 그 0.13초 동안에도
            // 클릭을 먹기 때문이다. 복귀는 강제로 다시 열지 않는다 — 톱니만 돌아오고 메뉴는 사용자가
            // 다시 부른다(WindowCrashDirector가 오버레이를 되살리지 않는 것과 같은 판단).
            // (이미 접혀 있으면 캔버스도 꺼져 있어 거둘 것이 없다 — 평소의 비용 0을 유지한다.)
            if (_phase == Phase.Hidden) return;

            if (_agent != null && _agent.IsSuspended)
            {
                ClosePopovers("전체화면 감지 — 자동 숨김");
                Hide();
                Debug.Log("[부채꼴] 전체화면 감지 — 부채꼴과 팝오버를 즉시 거둡니다(비침해 원칙 2).");
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _timer += dt;

            switch (_phase)
            {
                case Phase.Expanding:
                    if (_timer >= ExpandTotalSeconds) _phase = Phase.Open;
                    break;
                case Phase.Collapsing:
                    if (_timer >= CollapseSecondsFor(_collapseMode)) { Hide(); return; }
                    break;
            }

            TickAutoCollapse(dt);
            TickAnchoredPopover();
            RefreshDynamicContent(force: false);
            ApplyVisuals();
        }

        /// <summary>6초 무반응 자동 접힘. 팝오버가 떠 있는 동안에는 돌지 않는다 — 사용자가 읽고 있는
        /// 창을 시간으로 닫아버리면 그건 편의가 아니라 사고다.</summary>
        private void TickAutoCollapse(float dt)
        {
            if (_phase != Phase.Open || _activeIndex >= 0 || AnyPopoverOpen()) { _idleTimer = 0f; return; }
            _idleTimer += dt;
            if (_idleTimer < AutoCollapseIdleSeconds) return;
            Collapse(GearMenuCollapseMode.Auto, $"{AutoCollapseIdleSeconds:F0}초 동안 커서가 부채꼴 밖");
        }

        /// <summary>팝오버가 스스로 닫혔으면(✕ / 바깥 클릭) 남아 있던 버튼도 함께 거둔다.</summary>
        private void TickAnchoredPopover()
        {
            if (_activeIndex < 0) return;
            if (AnyPopoverOpen()) { _idleTimer = 0f; return; }
            Collapse(GearMenuCollapseMode.User, "팝오버가 닫힘");
        }

        private void Hide()
        {
            _phase = Phase.Hidden;
            _activeIndex = -1;
            _hoverIndex = -1;
            for (int i = 0; i < ButtonCount; i++) _buttons[i].Progress = 0f;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            UnionScreenRect = new Rect(PointsToScreen(_gearCenterPoints), Vector2.zero);
        }

        // ==================== 애니메이션 / 그리기 ====================

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private static float EaseInQuad(float t) => t * t;
        private static float EaseInOutSine(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t));
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        private void ApplyVisuals()
        {
            ApplyCanvasScaleFactor();

            float collapseSeconds = CollapseSecondsFor(_collapseMode);
            float collapseK = Mathf.Clamp01(_timer / Mathf.Max(0.001f, collapseSeconds));
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            bool anyVisible = false;

            for (int i = 0; i < ButtonCount; i++)
            {
                ButtonView b = _buttons[i];
                float radiusFactor = 1f;
                float scale = 1f;
                float alpha;

                if (b.CollapsingNow)
                {
                    if (_collapseMode == GearMenuCollapseMode.Auto)
                    {
                        // 제자리에서 스르르 — 반지름/스케일을 건드리지 않는다(사용자 접힘과 움직임이 달라야 한다).
                        float e = EaseInOutSine(collapseK);
                        alpha = 1f - e;
                    }
                    else
                    {
                        float e = EaseInQuad(collapseK);
                        radiusFactor = 1f - 0.5f * e;
                        scale = 1f - 0.28f * e;
                        alpha = 1f - e;
                    }
                    b.Progress = alpha;
                }
                else if (_phase == Phase.Expanding)
                {
                    float start = i * ExpandStaggerSeconds;
                    float p = Mathf.Clamp01((_timer - start) / ExpandSecondsPerButton);
                    float eased = EaseOutBack(p);
                    radiusFactor = StartRadiusFraction + (1f - StartRadiusFraction) * eased;
                    scale = StartScale + (1f - StartScale) * eased;
                    alpha = Mathf.Clamp01((_timer - start) / AlphaFadeInSeconds);
                    b.Progress = p;
                }
                else
                {
                    alpha = 1f;
                    b.Progress = 1f;
                }

                // 호버는 진행도와 무관한 별도 보간(0.09초) — 스케일에 곱해진다.
                float hoverTarget = i == _hoverIndex ? 1f : 0f;
                b.Hover = Mathf.MoveTowards(b.Hover, hoverTarget, Time.unscaledDeltaTime / HoverSeconds);
                scale *= Mathf.Lerp(1f, HoverScale, EaseOutQuad(b.Hover));

                Vector2 center = _gearCenterPoints + (b.CenterPoints - _gearCenterPoints) * radiusFactor;
                b.Group.anchoredPosition = center;
                b.Root.localScale = new Vector3(scale, scale, 1f);

                float labelAlpha = b.CollapsingNow || _phase != Phase.Expanding
                    ? alpha
                    : alpha * Mathf.Clamp01((_timer - i * ExpandStaggerSeconds - LabelDelaySeconds) / LabelFadeSeconds);

                ApplyButtonStyle(b, i, alpha, labelAlpha);

                if (alpha <= 0.001f) continue;
                anyVisible = true;
                Rect box = BoxFor(center, _diameterPoints, b.LabelWidth);
                if (box.xMin < minX) minX = box.xMin;
                if (box.yMin < minY) minY = box.yMin;
                if (box.xMax > maxX) maxX = box.xMax;
                if (box.yMax > maxY) maxY = box.yMax;
            }

            UnionScreenRect = anyVisible
                ? new Rect(PointsToScreen(new Vector2(minX, minY)),
                    new Vector2((maxX - minX) * PixelsPerPoint, (maxY - minY) * PixelsPerPoint))
                : new Rect(PointsToScreen(_gearCenterPoints), Vector2.zero);
        }

        private void ApplyButtonStyle(ButtonView b, int index, float alpha, float labelAlpha)
        {
            bool active = index == _activeIndex;
            float hover = EaseOutQuad(b.Hover);

            Color surface = Color.Lerp(UiChrome.CardSurface, UiChrome.AccentSurface, active ? 1f : hover);
            Color border = Color.Lerp(UiChrome.CardBorder, UiChrome.AccentBorder, active ? 1f : hover);
            Color symbol = Color.Lerp(UiChrome.TextPrimary, UiChrome.Accent, active ? 1f : hover);

            b.Surface.color = Fade(surface, alpha);
            b.Border.color = Fade(border, alpha);
            for (int i = 0; i < b.SymbolParts.Length; i++)
            {
                Image part = b.SymbolParts[i];
                if (part == null) continue;
                part.color = Fade(symbol, alpha);
            }
            if (b.SymbolFixedParts != null)
            {
                for (int i = 0; i < b.SymbolFixedParts.Length; i++)
                {
                    Image part = b.SymbolFixedParts[i];
                    if (part == null) continue;
                    part.color = Fade(UiChrome.Accent, alpha);
                }
            }

            if (b.FlashTimer >= 0f)
            {
                b.FlashTimer += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(b.FlashTimer / PressFlashSeconds);
                b.Flash.color = new Color(UiChrome.Accent.r, UiChrome.Accent.g, UiChrome.Accent.b, 0.35f * k * alpha);
                if (b.FlashTimer >= PressFlashSeconds) b.FlashTimer = -1f;
            }
            else if (b.Flash.color.a > 0f)
            {
                b.Flash.color = new Color(0f, 0f, 0f, 0f);
            }

            b.LabelSurface.color = Fade(UiChrome.PanelSurface, labelAlpha * 0.92f);
            b.LabelBorder.color = Fade(UiChrome.PanelBorder, labelAlpha);
            b.LabelText.color = Fade(active ? UiChrome.TextPrimary : UiChrome.TextSecondary, labelAlpha);

            if (b.Badge != null && b.Badge.gameObject.activeSelf)
            {
                b.BadgeSurface.color = Fade(UiChrome.Accent, alpha);
                b.BadgeText.color = Fade(UiChrome.OnAccentSolid, alpha);
            }
            if (b.RingFill != null && b.RingFill.gameObject.activeSelf)
            {
                b.RingFill.color = Fade(UiChrome.WarmAccent, alpha);
                b.RingTrack.color = Fade(UiChrome.TrackBackground, alpha);
            }
        }

        private static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(alpha));

        /// <summary>
        /// 라벨/배지/잔여 시간 호는 <b>실제 값에서만</b> 파생한다(원칙 1의 UI판). 링이 3할 남았는데
        /// 라벨이 다른 값이면 그 자체로 회귀 실패다 — 그래서 둘을 같은 스냅샷에서 한 번에 쓴다.
        /// </summary>
        private void RefreshDynamicContent(bool force)
        {
            _labelClockTimer += Time.unscaledDeltaTime;
            bool tick = force || _labelClockTimer >= 1f;
            if (tick) _labelClockTimer = 0f;

            // ---- 오늘 할일 배지 ----
            ButtonView todo = _buttons[(int)GearMenuButton.Todo];
            int uncompleted = TodoListModel.UncompletedCount;
            bool showBadge = uncompleted > 0;
            if (todo.Badge.gameObject.activeSelf != showBadge) todo.Badge.gameObject.SetActive(showBadge);
            if (showBadge && (tick || _lastShownBadgeCount != uncompleted))
            {
                _lastShownBadgeCount = uncompleted;
                todo.BadgeText.text = uncompleted >= 10 ? "9+" : uncompleted.ToString();
            }

            // ---- 집중 모드 잔여 시간 ----
            if (!tick) return;
            if (_focusDirector == null) _focusDirector = GetComponent<FocusWatchDirector>();

            ButtonView focus = _buttons[(int)GearMenuButton.FocusMode];
            bool running = _focusDirector != null && _focusDirector.IsSessionActive
                && _focusDirector.SessionDurationSeconds > 0f;

            if (focus.RingFill.gameObject.activeSelf != running) focus.RingFill.gameObject.SetActive(running);
            if (!running)
            {
                _lastShownRemainingSeconds = -1;
                if (focus.LabelText.text != Labels[(int)GearMenuButton.FocusMode])
                    focus.LabelText.text = Labels[(int)GearMenuButton.FocusMode];
                return;
            }

            int remaining = Mathf.Max(0, Mathf.CeilToInt(_focusDirector.RemainingSeconds));
            if (remaining == _lastShownRemainingSeconds) return;
            _lastShownRemainingSeconds = remaining;

            focus.RingFill.fillAmount = Mathf.Clamp01(_focusDirector.RemainingSeconds / _focusDirector.SessionDurationSeconds);
            focus.LabelText.text = $"집중 · {remaining / 60:00}:{remaining % 60:00}";
        }

        // ==================== 배치 계산 ====================

        /// <summary>(화면 중심 − 기어 중심)의 실제 각도를 45° 단위로 스냅한다 — 사분면 부호로 4방향만
        /// 쓰면 위쪽 한가운데에서 아래로 곧게 못 펼치고 중앙선에서 방향이 90° 튄다(32-9 (C)).</summary>
        public static float Snap45(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return 225f;
            float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(degrees / 45f) * 45f;
            return Mathf.Repeat(snapped, 360f);
        }

        /// <summary>
        /// 화면 밖 방지 사다리(32-1 + 실측 보강):
        ///  ① θ₀ 그대로 → ② θ₀ ±15°씩 최대 ±90° 회전 → ③ <b>부채꼴 전체 평행이동</b>(형태 보존,
        ///  <see cref="MaxGroupShiftPoints"/>까지) → ④ 지름 축소(44→36) 후 ①~③ 반복 →
        ///  ⑤ 세로 일렬 폴백 → ⑥ 세로 일렬 + 평행이동.
        /// 개별 버튼 클램프는 어느 단계에서도 쓰지 않는다 — 그것만이 히트 원 겹침을 만든다.
        /// </summary>
        private void ComputeLayout()
        {
            _screenPointsAtLayout = ScreenSizePoints();
            _baseAngleDegrees = Snap45(_screenPointsAtLayout * 0.5f - _gearCenterPoints);

            if (TrySearchRotation(ButtonDiameterPoints, allowShift: false)) return;
            if (TrySearchRotation(ButtonDiameterPoints, allowShift: true)) return;
            if (TrySearchRotation(ShrunkDiameterPoints, allowShift: false)) return;
            if (TrySearchRotation(ShrunkDiameterPoints, allowShift: true)) return;
            if (TryColumn(ButtonDiameterPoints)) return;
            if (TryColumn(ShrunkDiameterPoints)) return;

            PlaceColumn(ShrunkDiameterPoints);
            ShiftGroupIntoScreen();
        }

        private bool TrySearchRotation(float diameter, bool allowShift)
        {
            for (float offset = 0f; offset <= RotationSearchMaxDegrees + 0.01f; offset += RotationSearchStepDegrees)
            {
                if (TryFan(_baseAngleDegrees + offset, diameter, allowShift)) return true;
                if (offset > 0f && TryFan(_baseAngleDegrees - offset, diameter, allowShift)) return true;
            }
            return false;
        }

        private bool TryFan(float baseDegrees, float diameter, bool allowShift)
        {
            Vector2 shift = Vector2.zero;
            if (allowShift)
            {
                shift = RequiredShift(baseDegrees, diameter);
                if (shift.magnitude > MaxGroupShiftPoints) return false;
            }

            for (int i = 0; i < ButtonCount; i++)
            {
                if (!IsBoxOnScreen(BoxFor(FanCenter(baseDegrees, i) + shift, diameter, _buttons[i].LabelWidth)))
                    return false;
            }

            for (int i = 0; i < ButtonCount; i++) _buttons[i].CenterPoints = FanCenter(baseDegrees, i) + shift;
            _baseAngleDegrees = Mathf.Repeat(baseDegrees, 360f);
            _diameterPoints = diameter;
            return true;
        }

        private Vector2 FanCenter(float baseDegrees, int index)
        {
            float a = (baseDegrees + (1 - index) * ButtonAngleStepDegrees) * Mathf.Deg2Rad;
            return _gearCenterPoints + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * OrbitRadiusPoints;
        }

        /// <summary>이 각도의 부채꼴을 화면 안으로 넣는 데 필요한 <b>최소 평행이동</b>.</summary>
        private Vector2 RequiredShift(float baseDegrees, float diameter)
        {
            Rect union = BoxFor(FanCenter(baseDegrees, 0), diameter, _buttons[0].LabelWidth);
            for (int i = 1; i < ButtonCount; i++)
            {
                Rect box = BoxFor(FanCenter(baseDegrees, i), diameter, _buttons[i].LabelWidth);
                union = Rect.MinMaxRect(Mathf.Min(union.xMin, box.xMin), Mathf.Min(union.yMin, box.yMin),
                    Mathf.Max(union.xMax, box.xMax), Mathf.Max(union.yMax, box.yMax));
            }
            return ShiftToFit(union);
        }

        private bool TryColumn(float diameter)
        {
            PlaceColumn(diameter);
            for (int i = 0; i < ButtonCount; i++)
            {
                if (!IsBoxOnScreen(BoxFor(_buttons[i].CenterPoints, diameter, _buttons[i].LabelWidth))) return false;
            }
            _diameterPoints = diameter;
            return true;
        }

        /// <summary>
        /// 세로 일렬 폴백 — 화면 안쪽 수직 방향. 간격은 32-1의 52pt를 <b>하한</b>으로 쓰되 실제로는
        /// "지름 + 라벨 간격 + 라벨 높이 + 여유 4"보다 좁아지지 않게 한다: 52pt는 Ø44에서 라벨 알약이
        /// 아래 버튼 원에 파고드는 값이라(실측 스크린샷에서 실제로 겹쳤다) 글자가 읽히지 않는다.
        /// </summary>
        private void PlaceColumn(float diameter)
        {
            float spacing = Mathf.Max(ColumnFallbackSpacingPoints,
                diameter + LabelGapPoints + LabelHeightPoints + UiChrome.Space1);
            float sign = _gearCenterPoints.y > _screenPointsAtLayout.y * 0.5f ? -1f : 1f;
            for (int i = 0; i < ButtonCount; i++)
            {
                _buttons[i].CenterPoints = new Vector2(
                    _gearCenterPoints.x,
                    _gearCenterPoints.y + sign * (OrbitRadiusPoints + i * spacing));
            }
            _diameterPoints = diameter;
        }

        /// <summary>세 버튼을 <b>같은 벡터로</b> 평행이동해 화면 안으로 넣는다(형태 보존 — 개별 클램프 금지).</summary>
        private void ShiftGroupIntoScreen()
        {
            Rect union = BoxFor(_buttons[0].CenterPoints, _diameterPoints, _buttons[0].LabelWidth);
            for (int i = 1; i < ButtonCount; i++)
            {
                Rect box = BoxFor(_buttons[i].CenterPoints, _diameterPoints, _buttons[i].LabelWidth);
                union = Rect.MinMaxRect(Mathf.Min(union.xMin, box.xMin), Mathf.Min(union.yMin, box.yMin),
                    Mathf.Max(union.xMax, box.xMax), Mathf.Max(union.yMax, box.yMax));
            }

            Vector2 shift = ShiftToFit(union);
            for (int i = 0; i < ButtonCount; i++) _buttons[i].CenterPoints += shift;
        }

        /// <summary>이 사각형을 화면 여백 안으로 넣는 최소 이동 벡터(들어와 있으면 0).</summary>
        private Vector2 ShiftToFit(Rect union)
        {
            var shift = Vector2.zero;
            if (union.xMin < ScreenMarginPoints) shift.x = ScreenMarginPoints - union.xMin;
            else if (union.xMax > _screenPointsAtLayout.x - ScreenMarginPoints)
                shift.x = _screenPointsAtLayout.x - ScreenMarginPoints - union.xMax;
            if (union.yMin < ScreenMarginPoints) shift.y = ScreenMarginPoints - union.yMin;
            else if (union.yMax > _screenPointsAtLayout.y - TopMarginPoints)
                shift.y = _screenPointsAtLayout.y - TopMarginPoints - union.yMax;
            return shift;
        }

        /// <summary>버튼이 실제로 차지하는 상자(원 + 라벨 알약). 라벨까지 화면 안에 들어와야
        /// "잘린 메뉴"가 안 나온다(32-1).</summary>
        private static Rect BoxFor(Vector2 center, float diameter, float labelWidth)
        {
            float width = Mathf.Max(diameter, labelWidth) + ClampBoxExtraWidthPoints;
            float height = diameter + LabelGapPoints + LabelHeightPoints;
            float top = center.y + diameter * 0.5f;
            return new Rect(center.x - width * 0.5f, top - height, width, height);
        }

        private bool IsBoxOnScreen(Rect box)
            => box.xMin >= ScreenMarginPoints && box.yMin >= ScreenMarginPoints
               && box.xMax <= _screenPointsAtLayout.x - ScreenMarginPoints
               && box.yMax <= _screenPointsAtLayout.y - TopMarginPoints;

        // ==================== 좌표 변환 ====================

        private float PixelsPerPoint => ScreenCoordinateConverter.CanvasToUnityScreen(1f, _config);

        private Vector2 ScreenToPoints(Vector2 unityScreen) => new Vector2(
            ScreenCoordinateConverter.UnityScreenToCanvas(unityScreen.x, _config),
            ScreenCoordinateConverter.UnityScreenToCanvas(unityScreen.y, _config));

        private Vector2 PointsToScreen(Vector2 points) => new Vector2(
            ScreenCoordinateConverter.CanvasToUnityScreen(points.x, _config),
            ScreenCoordinateConverter.CanvasToUnityScreen(points.y, _config));

        private Vector2 ScreenSizePoints() => ScreenToPoints(new Vector2(Screen.width, Screen.height));

        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
        }

        // ==================== 도형 만들기 ====================

        private void BuildUi()
        {
            var canvasGo = new GameObject("GearRadialMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            // ★★ 씬 루트에 단다(캐릭터의 자식이 아니다) — InfoGearIconWidget._container와 같은 전례.
            // 이유 둘: (1) ScreenSpaceOverlay 캔버스는 화면 좌표계에 사는 물건이라 걷고 넘어지는
            // 캐릭터의 Transform 계보에 속할 이유가 애초에 없다. (2) 2026-08-30 회귀 — 이 캔버스가
            // 캐릭터 루트 아래 있었기 때문에, 그 안의 "Head"라는 UI 자손이 이름으로 캐릭터 파츠를
            // 찾는 코드에 잡혀 머리·몸통이 영영 안 움직였다. 계층을 분리하면 이 UI가 어떤 이름을
            // 쓰든 캐릭터 탐색에 구조적으로 걸릴 수 없다. 정리는 OnDestroy가 책임진다.
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();

            var rootGo = new GameObject("Fan", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            _root = rootGo.GetComponent<RectTransform>();
            _root.anchorMin = _root.anchorMax = _root.pivot = Vector2.zero;
            _root.sizeDelta = Vector2.zero;

            for (int i = 0; i < ButtonCount; i++) _buttons[i] = BuildButton(i);
            canvasGo.SetActive(false);
        }

        private ButtonView BuildButton(int index)
        {
            var view = new ButtonView();
            float d = ButtonDiameterPoints;

            var groupGo = new GameObject("Button" + index, typeof(RectTransform));
            groupGo.transform.SetParent(_root, false);
            view.Group = groupGo.GetComponent<RectTransform>();
            view.Group.anchorMin = view.Group.anchorMax = view.Group.pivot = new Vector2(0.5f, 0.5f);
            view.Group.sizeDelta = Vector2.zero;

            // 원 묶음(스케일 대상) — 라벨은 스케일에서 제외한다(호버로 글자가 커지면 어수선하다).
            var circleGo = new GameObject("Circle", typeof(RectTransform));
            circleGo.transform.SetParent(view.Group, false);
            view.Root = circleGo.GetComponent<RectTransform>();
            view.Root.anchorMin = view.Root.anchorMax = view.Root.pivot = new Vector2(0.5f, 0.5f);
            view.Root.sizeDelta = new Vector2(d, d);

            UiChrome.AddCircle(view.Root, "Shadow", d + 4f, UiChrome.PanelShadow).rectTransform.anchoredPosition
                = new Vector2(0f, -1.5f);
            view.Surface = UiChrome.AddCircle(view.Root, "Surface", d, UiChrome.CardSurface);
            view.Border = UiChrome.AddCircle(view.Root, "Border", d, UiChrome.CardBorder, 1.2f);
            view.Flash = UiChrome.AddCircle(view.Root, "Flash", d, new Color(0f, 0f, 0f, 0f));

            var symbolGo = new GameObject("Symbol", typeof(RectTransform));
            symbolGo.transform.SetParent(view.Root, false);
            view.Symbol = symbolGo.GetComponent<RectTransform>();
            view.Symbol.anchorMin = view.Symbol.anchorMax = view.Symbol.pivot = new Vector2(0.5f, 0.5f);
            view.Symbol.sizeDelta = new Vector2(SymbolBoxPoints, SymbolBoxPoints);

            view.SymbolParts = (GearMenuButton)index switch
            {
                GearMenuButton.FocusMode => BuildStopwatchSymbol(view),
                GearMenuButton.Character => BuildStickmanSymbol(view),
                _ => BuildChecklistSymbol(view),
            };

            // ---- 라벨 알약(원 아래 4pt) ----
            var pillGo = new GameObject("Label", typeof(RectTransform));
            pillGo.transform.SetParent(view.Group, false);
            view.LabelPill = pillGo.GetComponent<RectTransform>();
            view.LabelPill.anchorMin = view.LabelPill.anchorMax = view.LabelPill.pivot = new Vector2(0.5f, 0.5f);

            view.LabelSurface = UiChrome.AddSurface(view.LabelPill, "PillSurface", UiChrome.PanelSurface, 8);
            UiChrome.Stretch(view.LabelSurface.rectTransform);
            view.LabelSurface.raycastTarget = false;
            view.LabelBorder = UiChrome.AddOutline(view.LabelPill, "PillBorder", UiChrome.PanelBorder, 8);

            view.LabelText = UiChrome.AddText(view.LabelPill, "PillText", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(view.LabelText.rectTransform);
            view.LabelText.text = Labels[index];

            // 폭은 실제 글자 폭에서 구한다(고정값으로 박으면 라벨이 길어질 때 잘린다). 한 번만 잰다.
            view.LabelWidth = Mathf.Max(d, view.LabelText.preferredWidth + UiChrome.Space3);
            view.LabelPill.sizeDelta = new Vector2(view.LabelWidth, LabelHeightPoints);
            view.LabelPill.anchoredPosition = new Vector2(0f, -(d * 0.5f + LabelGapPoints + LabelHeightPoints * 0.5f));

            // ---- 오늘 할일 미완료 배지 ----
            if ((GearMenuButton)index == GearMenuButton.Todo)
            {
                var badgeGo = new GameObject("Badge", typeof(RectTransform));
                badgeGo.transform.SetParent(view.Root, false);
                view.Badge = badgeGo.GetComponent<RectTransform>();
                view.Badge.anchorMin = view.Badge.anchorMax = view.Badge.pivot = new Vector2(0.5f, 0.5f);
                view.Badge.sizeDelta = new Vector2(16f, 16f);
                view.Badge.anchoredPosition = new Vector2(15f, 15f);
                view.BadgeSurface = UiChrome.AddCircle(view.Badge, "BadgeSurface", 16f, UiChrome.Accent);
                view.BadgeText = UiChrome.AddText(view.Badge, "BadgeText", 9, TextAnchor.MiddleCenter,
                    UiChrome.OnAccentSolid, bold: true);
                UiChrome.Stretch(view.BadgeText.rectTransform);
                view.Badge.gameObject.SetActive(false);
            }

            return view;
        }

        /// <summary>① 집중 모드 — 스톱워치(용두 + 링 + 바늘 2). 세션이 돌면 이 링이 그대로 잔여 시간 호가 된다.</summary>
        private Image[] BuildStopwatchSymbol(ButtonView view)
        {
            Transform p = view.Symbol;
            var crown = UiChrome.AddStroke(p, "Crown", 6f, 4f, 0f, new Vector2(0f, 11.5f), UiChrome.TextPrimary);
            view.RingTrack = UiChrome.AddCircle(p, "Ring", 20f, UiChrome.TextPrimary, SymbolStroke);

            // 잔여 시간 호 — 같은 링 위에 겹쳐 그린다(세션 중에만 켠다).
            view.RingFill = UiChrome.AddCircle(p, "RingFill", 20f, UiChrome.WarmAccent, SymbolStroke);
            view.RingFill.type = Image.Type.Filled;
            view.RingFill.fillMethod = Image.FillMethod.Radial360;
            view.RingFill.fillOrigin = (int)Image.Origin360.Top;
            view.RingFill.fillClockwise = true;
            view.RingFill.fillAmount = 1f;
            view.RingFill.gameObject.SetActive(false);

            var minute = UiChrome.AddStroke(p, "MinuteHand", 6.5f, SymbolStroke, 90f, new Vector2(0f, 3.25f), UiChrome.TextPrimary);
            float hourAngle = -30f * Mathf.Deg2Rad;
            var hour = UiChrome.AddStroke(p, "HourHand", 5f, SymbolStroke, -30f,
                new Vector2(Mathf.Cos(hourAngle) * 2.5f, Mathf.Sin(hourAngle) * 2.5f), UiChrome.TextPrimary);

            return new[] { crown, view.RingTrack, minute, hour };
        }

        /// <summary>② 캐릭터 — 미니 스틱맨. <b>영원히 같은 그림</b>이다(장비/포즈를 반영하면 내비게이션
        /// 표지로서의 식별성을 잃는다 — 32-4 ②).</summary>
        private Image[] BuildStickmanSymbol(ButtonView view)
        {
            Transform p = view.Symbol;

            // ★★ 2026-08-30 회귀의 <b>생산자 측</b> 수정 — 부품 이름에 "Icon" 접두사를 붙인다.
            // 예전 이름은 "Head"/"ArmL"/"LegL"이었고, 그중 "Head"가 프리팹 캐릭터의 머리 앵커와
            // 글자 그대로 같은 이름이었다. 이름으로 캐릭터 파츠를 찾는 소비자(StickmanPoseAnimator /
            // StickmanMetrics / EyeController / DialogueBubbleRenderer / CharacterAccessoryRenderer)가
            // 이 UI 원을 진짜 머리로 착각해 캐릭터 머리·몸통이 영영 안 움직였다.
            // 소비자 쪽은 탐색 범위를 좁혀 이미 막았지만, 그 방어는 "지금의 계층 규약"에 기대는 것이라
            // 여기서 이름 충돌 자체를 없앤다(위 BuildUi의 씬 루트 부착과 합쳐 이중 차단).
            var head = UiChrome.AddCircle(p, "IconHead", 7f, UiChrome.TextPrimary, 1.8f);
            head.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            var spine = UiChrome.AddStroke(p, "IconSpine", 9f, 1.8f, 90f, Vector2.zero, UiChrome.TextPrimary);
            var shoulder = new Vector2(0f, 3.5f);
            var pelvis = new Vector2(0f, -4.5f);
            var armL = UiChrome.AddStroke(p, "IconArmL", 6f, 1.8f, -140f, shoulder + Polar(-140f, 3f), UiChrome.TextPrimary);
            var armR = UiChrome.AddStroke(p, "IconArmR", 6f, 1.8f, -40f, shoulder + Polar(-40f, 3f), UiChrome.TextPrimary);
            var legL = UiChrome.AddStroke(p, "IconLegL", 7f, 1.8f, -106f, pelvis + Polar(-106f, 3.5f), UiChrome.TextPrimary);
            var legR = UiChrome.AddStroke(p, "IconLegR", 7f, 1.8f, -74f, pelvis + Polar(-74f, 3.5f), UiChrome.TextPrimary);

            return new[] { head, spine, armL, armR, legL, legR };
        }

        /// <summary>③ 오늘 할일 — 체크리스트(글줄 3 + 빈 박스 2 + 체크마크 + 취소선).</summary>
        private Image[] BuildChecklistSymbol(ButtonView view)
        {
            Transform p = view.Symbol;
            var line0 = UiChrome.AddStroke(p, "Line0", 9f, SymbolStroke, 0f, new Vector2(5.5f, 7f), UiChrome.TextPrimary);
            var line1 = UiChrome.AddStroke(p, "Line1", 9f, SymbolStroke, 0f, new Vector2(5.5f, 0f), UiChrome.TextPrimary);
            var line2 = UiChrome.AddStroke(p, "Line2", 9f, SymbolStroke, 0f, new Vector2(5.5f, -7f), UiChrome.TextPrimary);
            var strike = UiChrome.AddStroke(p, "Strike", 9f, 1.4f, 0f, new Vector2(5.5f, -7f), UiChrome.TextPrimary);

            Image box0 = AddSmallBox(p, "Box0", new Vector2(-6f, 7f));
            Image box1 = AddSmallBox(p, "Box1", new Vector2(-6f, 0f));

            var checkVertex = new Vector2(-7f, -8f);
            var checkShort = UiChrome.AddStroke(p, "CheckShort", 3.2f, 1.6f, 135f,
                checkVertex + Polar(135f, 1.6f), UiChrome.Accent);
            var checkLong = UiChrome.AddStroke(p, "CheckLong", 6f, 1.6f, 45f,
                checkVertex + Polar(45f, 3f), UiChrome.Accent);

            // 체크마크는 Accent 고정(완료를 뜻하는 유일한 색) — 심볼 색 보간 대상에서 제외하고
            // 알파만 따라가게 한다.
            view.SymbolFixedParts = new[] { checkShort, checkLong };
            return new[] { line0, line1, line2, strike, box0, box1 };
        }

        private static Image AddSmallBox(Transform parent, string name, Vector2 center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(4.5f, 4.5f);
            rt.anchoredPosition = center;
            var image = go.GetComponent<Image>();
            image.sprite = UiChrome.RoundedOutline(2, 1);
            image.type = Image.Type.Sliced;
            image.color = UiChrome.TextPrimary;
            image.raycastTarget = false;
            return image;
        }

        private static Vector2 Polar(float degrees, float radius)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r) * radius, Mathf.Sin(r) * radius);
        }
    }
}
