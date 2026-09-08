using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 부채꼴 [오늘 할일] 버튼에서 자라나는 팝오버 — docs/UX_WIDGETS.md <b>R6</b> 확정 설계.
    /// <b>500 × 512</b>, 같은 창 안에서 <b>일별 페이지 ↔ 달력 페이지</b>가 바뀐다.
    ///
    /// ============================================================================
    /// 탭을 늘리지 않는다 — 바뀌는 것은 <b>축척</b>이다 (UW-6-1 탭 계약)
    /// ============================================================================
    /// 탭은 <b>서로 다른 집합</b>에 쓴다(<c>완료함</c> = 모든 날짜의 완료 항목).
    /// <b>같은 집합의 다른 축척</b>(하루 ↔ 한 달)은 탭이 아니라 <b>페이지 전환</b>이고,
    /// 그 손잡이는 날짜 라벨 자체다(<c>9월 8일 (화) ▾</c>). 달력 탭을 새로 만들면
    /// "완료함에서 달력을 보면 무엇이 나오는가"라는 답할 수 없는 칸이 생긴다.
    ///
    /// ============================================================================
    /// 「오늘」은 <see cref="CurrencyModel.DayIndex"/> 하나에서만 온다 (UW-6-2 시계 단일성)
    /// ============================================================================
    /// 이 파일에는 <c>DateTime.Now</c>가 없다. 이유는 정확성이 아니라 <b>일치</b>다 — 하루 1회
    /// 동전(<see cref="CurrencyModel.TryPayTodoDailyCoins"/>)과 화면의 "오늘"이 같은 순간에
    /// 넘어가야 한다. 두 시계는 두 진실이 된다. 대가는 최대 60초 지연
    /// (<c>CurrencyDayRolloverTicker.CheckIntervalSeconds</c>)이고 받아들였다.
    /// <para><see cref="System.DateTime"/>을 쓰는 곳은 <see cref="DateOfDayIndex"/> 하나뿐인데,
    /// 그건 <b>시각을 읽는 것이 아니라</b> 이미 확정된 정수를 «몇 월 며칠 무슨 요일»로 환산하는
    /// 순수 함수다(에폭은 <see cref="CurrencyRules.LocalDayIndex"/>가 쓰는 바로 그 1970-01-01).</para>
    ///
    /// ============================================================================
    /// 탐색은 <b>저장하지 않는다</b> (persona-stress R-10)
    /// ============================================================================
    /// «지금 보고 있는 날짜»는 <b>이 컴포넌트의 필드</b>이고 세이브에 칸이 없다. 날짜를 아무리
    /// 넘겨도 <see cref="CharacterSaveStore.Save"/>가 불리지 않고 <see cref="TodoListModel.IsDirty"/>도
    /// 서지 않는다 — <see cref="TodoListModel"/>의 조회 API 5종이 읽기만 하기 때문이다.
    /// 저장은 «사용자가 내용을 바꿨을 때»(추가 / 완료 토글 / 삭제)에만 걸린다.
    ///
    /// ============================================================================
    /// 삭제는 "되돌리기"가 아니라 <b>인라인 확인 3초</b>다
    /// ============================================================================
    /// <see cref="TodoListModel"/>에는 복구 API가 없다. <see cref="TodoListModel.Add"/>로 되살리면 새 Id에
    /// 맨 뒤 순서라 <b>원래대로 돌아오지 않는다</b> — 되돌려주겠다고 써놓고 다른 결과를 주는 것이 이
    /// 프로젝트가 가장 싫어하는 형태다. 인라인 확인은 모델 변경 0으로 같은 안전을 준다.
    ///
    /// ============================================================================
    /// 빈 상태는 포스트잇과 <b>정반대</b>로 처리한다
    /// ============================================================================
    /// 포스트잇(앰비언트)은 0건이면 카드를 숨기지만, 이 패널은 사용자가 <b>직접 열었으므로</b> 숨기면
    /// 막다른 길이 된다. 목록 자리에 안내를 남기고 입력칸을 그대로 둔다(가짜 일러스트 없음).
    ///
    /// ============================================================================
    /// 2026-09-08 — 행 글자는 <b>흘러나가지 않고 잘린다</b> / 취소선은 <b>글리프가 아니라 선</b>이다
    /// ============================================================================
    /// ux-widgets 감사(docs/UX_WIDGETS.md <b>R6-0-2</b>)가 이 패널에서 결함 2건을 실측으로 찾았다.
    /// <list type="number">
    /// <item>라벨이 <c>Overflow</c>로 그려지고 마스크가 없어 <b>패널 밖으로 흘렀다</b> —
    ///   60자면 474pt까지 나가 차단막 밖 바탕화면에 글자가 그려졌다.
    ///   → <see cref="UiChrome.Ellipsize"/>(정보창이 같은 사고로 만든 함수)를 <see cref="RowLabelWidth"/>로 건다.</item>
    /// <item>취소선이 U+0336 결합문자 조립이라 <b>폰트 폴백에 기대고</b> 있었다(Windows 미확인).
    ///   → <see cref="TodoPostItWidget"/>가 이미 검증한 <b>1pt Image 선</b>으로 통일한다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 2026-09-08 — 완료 행을 <b>알파로 흐리지 않는다</b>
    /// ============================================================================
    /// 여기 있던 <c>TextPrimary</c> α0.5는 이 오버레이에서 «흐린 글자»가 아니라 <b>구멍</b>이다.
    /// uGUI 기본 셰이더는 알파에도 <c>SrcAlpha OneMinusSrcAlpha</c>를 적용하므로
    /// α0.5를 α1 위에 그리면 그 화소의 창 알파가 <c>0.5² + 1×0.5 = 0.75</c>로 <b>내려간다</b> —
    /// 유저 데스크톱이 25% 비친다(UiChrome 파일 머리 "알파 채널의 법칙" (2)).
    /// <see cref="TodoPostItWidget"/>는 같은 자리를 <b>색 토큰</b>으로 풀어 놨고
    /// (<c>미완료 TextPrimary / 완료 TextTertiary</c>) 그 처방을 그대로 옮겼다.
    /// R6에서 완료 행이 그 날짜에 <b>영구히</b> 남으므로 이 결함도 영구화될 참이었다.
    ///
    /// <b>쓸어담기 중복 금지</b>: <see cref="TodoListModel.SweepCompleted"/>는
    /// <see cref="TodoPostItWidget"/>가 이미 0.5초 주기로 부른다(카드가 숨겨져 있어도 돈다). 이 패널은
    /// 호출하지 않고 <see cref="StickmanEventBus.TodoListChanged"/>만 구독한다 — 청소 주체는 하나여야 한다.
    /// </summary>
    public sealed class TodoBoardPopover : PopoverPanel
    {
        // ====================================================================================
        // 치수 — docs/UX_WIDGETS.md R6-5. <b>역산이 아니라 안에서 밖으로 쌓았다</b>.
        //
        //   목표: 보이는 행 6 → 10(요구 ①에 대한 측정 가능한 답)
        //   목록 높이 = 10×33 + 9×4 = 366
        //   세로 스택 = 날짜네비 24 + 8 + 입력 30 + 8 + 366 + 4 + 푸터 12 = 452 ≤ 454 (여유 2)
        //   가로     = 라벨 368 + 체크 20 + 삭제 22 + 여백 → 행폭 442, + 레일 22 + 4 = 468, + 32 = 500
        //
        // ★ 이 저장소는 폭 상수를 바꿀 때 <b>파생값이 조각마다 흩어져</b> 깨진 적이 있다(폭 1042가
        //   헤더에는 갔는데 카드줄에는 안 갔다). 그래서 아래는 전부 Width/Height에서 파생시키고,
        //   숫자를 다시 적는 자리를 남기지 않는다. 검산은 각 줄 오른쪽 주석에 있다.
        // ====================================================================================

        private const float Width = 500f;
        private const float Height = 512f;
        private const float ContentWidth = Width - UiChrome.Space4 * 2f;   // 468.

        /// <summary>타이틀 줄 높이 — <see cref="PopoverPanel"/> 크롬의 값과 같아야 한다.
        /// <para>그쪽이 <c>private</c> 리터럴이라 참조할 수 없어 여기 이름을 하나 둔다. 대신 값이
        /// 갈라지면 바로 걸리도록 <see cref="ContentHeightForTests"/>가 <b>실측 Content 높이</b>를
        /// 내보내고, 테스트가 <see cref="ContentHeightPoints"/>와 대조한다 — 숫자를 베낀 곳이
        /// 조용히 썩지 않게 하는 이 저장소의 관례다.</para></summary>
        private const float TitleRowHeightPoints = 22f;

        /// <summary>패널 높이에서 Content가 <b>못 쓰는</b> 세로 합(위 크롬 + 아래 여백).</summary>
        private const float ChromeVerticalPoints =
            UiChrome.Space3 + TitleRowHeightPoints + UiChrome.Space2 + UiChrome.Space4;   // 58.

        /// <summary>설계 크기에서의 Content 높이. <b>작은 화면에서는 이보다 작아질 수 있다</b> —
        /// 그때 몇 행이 보이는지는 <see cref="ResolveVisibleRows"/>가 정한다.</summary>
        public const float ContentHeightPoints = Height - ChromeVerticalPoints;   // 454.

        // ---- 날짜 네비 줄 (R6-5-2 y=0, h=24) ----
        private const float NavRowHeight = UiChrome.MinTargetSizePoints;          // 24.
        private const float NavArrowSize = UiChrome.MinTargetSizePoints;          // 24.
        private const float DateToggleLeft = NavArrowSize + UiChrome.Space1;      // 28.
        private const float DateToggleWidth = 196f;
        private const float NextArrowLeft = DateToggleLeft + DateToggleWidth + UiChrome.Space1;   // 228.
        private const float DayChipWidth = CloseChipWidth;                        // 44 — 최소 타깃 통과.
        private const float DayChipHeight = CloseChipHeight;                      // 24.
        private const float DayChipGap = UiChrome.Space1;                         // [오늘]@376 · [내일]@424.

        // ---- 입력 줄 (R6-5-2 y=32, h=30) ----
        private const float InputRowTop = -(NavRowHeight + UiChrome.Space2);      // -32.
        private const float InputRowHeight = 30f;
        private const float AddButtonWidth = 66f;
        private const float InputToAddGap = UiChrome.Space2;                      // 입력 폭 468-66-8 = 394.

        // ---- 목록 (R6-5-2 y=70, h=366) ----
        private const int VisibleRows = 10;
        private const float RowHeight = 33f;
        private const float RowGap = UiChrome.Space1;
        private const float RailWidth = 22f;
        private const float RowRightInset = RailWidth + UiChrome.Space1;          // 26.
        private const float RowWidth = ContentWidth - RowRightInset;              // 442.
        private const float ListTop = InputRowTop - (InputRowHeight + UiChrome.Space2);   // -70.
        private const float ListHeight = VisibleRows * RowHeight + (VisibleRows - 1) * RowGap;   // 366.

        // ---- 푸터 ----
        private const float FooterHeight = 12f;
        /// <summary>일별 페이지 푸터는 Content 아래에서 2pt 띄운다(452 = 440+12, Content 454).</summary>
        private const float DailyFooterBottom = ContentHeightPoints - (440f + FooterHeight);   // 2.
        private const float CalendarFooterBottom = 0f;

        // ==================== 행 내부 가로 배분 — 숫자를 두 벌 두지 않는다 ====================
        // ★★ 2026-09-08 ux-widgets 감사 R6-0-2(가) — 여기에는 <b>162</b>(라벨 상자)와 <b>32</b>([✕] 왼끝)가
        //   서로를 모른 채 손으로 맞춰져 있었다. 그리고 상자를 잡아 봐야 소용이 없었다 —
        //   <see cref="UiChrome.AddText"/>는 <c>horizontalOverflow = Overflow</c>로 그리고 이 패널에는
        //   <c>RectMask2D</c>도 <c>Mask</c>도 없어서 <b>상자가 자르지 않기 때문</b>이다.
        //   이제 (가) 라벨 폭은 <see cref="RowWidth"/>에서 파생되고 (나) 그 <b>같은 상수</b>를
        //   <see cref="UiChrome.Ellipsize"/>가 예산으로 받는다.
        private const float RowBoxLeft = 10f;                                     // 체크박스 좌여백.
        private const float RowBoxSize = 20f;                                     // 체크박스 한 변.
        private const float RowLabelLeft = RowBoxLeft + RowBoxSize + UiChrome.Space2;   // 38.
        private const float RowLabelHeight = 18f;
        private const float RowDeleteSize = 22f;                                  // [✕] 한 변(최소 타깃).
        private const float RowDeleteRightInset = 10f;                            // 행 오른끝 ~ [✕] 오른끝 → [✕]@410.
        private const float RowLabelToDeleteGap = UiChrome.Space1;                // 라벨 상자 ~ [✕] 숨구멍.
        private const float RowDeleteLeft = RowWidth - RowDeleteRightInset - RowDeleteSize;  // 410.

        /// <summary>행 라벨 상자가 <b>오른쪽에서</b> 비워 두는 폭.</summary>
        private const float RowLabelRightInset = RowLabelToDeleteGap + RowDeleteSize + RowDeleteRightInset;  // 36.

        /// <summary>행 라벨 상자의 폭(pt). <b>배치와 말줄임이 같은 값을 쓴다.</b>
        /// <para>★ <c>public</c>인 이유: 이 값을 재는 감사 테스트가 숫자를 <b>베끼지 않게</b> 한다
        /// (CLAUDE.md 협업 프로토콜 — 상한값은 그 상수를 참조해 검증한다).
        /// <see cref="PopoverPanel.CloseChipWidth"/>가 같은 사정으로 이미 public이다.</para></summary>
        public const float RowLabelWidth = RowWidth - RowLabelLeft - RowLabelRightInset;   // 368.

        // ==================== 달력 페이지 (R6-5-3) ====================
        private const int CalendarColumns = 7;
        private const int CalendarWeekRows = 6;
        private const int CalendarCellCount = CalendarColumns * CalendarWeekRows;   // 42.
        private const float CalendarColumnGap = UiChrome.Space2;                    // 8.
        private const float CalendarRowGap = 6f;

        /// <summary>달력 셀 폭 — <b>정수로 떨어진다</b>: (468 − 6×8)/7 = 60.0 (R6-5-1 검산).</summary>
        private const float CalendarCellWidth =
            (ContentWidth - (CalendarColumns - 1) * CalendarColumnGap) / CalendarColumns;   // 60.

        private const float CalendarCellHeight = 58f;
        private const float WeekdayRowTop = -(NavRowHeight + UiChrome.Space2);      // -32.
        private const float WeekdayRowHeight = 16f;
        private const float CalendarGridTop = WeekdayRowTop - (WeekdayRowHeight + UiChrome.Space1);   // -52.
        private const float CalendarProgressBarHeight = 2f;
        private const float CalendarDotDiameter = 5f;

        /// <summary>삭제 확인이 열려 있는 시간. 지나면 조용히 취소된다.</summary>
        private const float DeleteConfirmSeconds = 3f;

        /// <summary>입력칸이 받는 글자 수 상한. <b>말줄임이 감당해야 하는 최악</b>이 이 값이다
        /// (R6-0-2 실측: 60자면 잉크 720pt로 패널 300 밖 474pt까지 흘렀다).
        /// <para>★ <c>public</c>인 이유: 그 최악을 재현하는 테스트가 60을 <b>손으로 베끼면</b>
        /// 상한이 바뀌는 날 조용히 «짧은 글자»를 넣고 초록이 된다.</para></summary>
        public const int InputCharacterLimit = 60;

        // ==================== 클릭 소유권 (2026-09-08 P0) ====================

        /// <summary>이 표면이 <see cref="UiClickArbiter"/>에 쓰는 이름.</summary>
        public const string ClickArbiterSurfaceId = nameof(TodoBoardPopover);

        /// <summary>이 표면의 «층». <b>캔버스 <c>sortingOrder</c> 숫자를 베끼지 않는다</b> —
        /// 그 상수는 <see cref="PopoverPanel"/>의 <c>private</c>이고, 여기 필요한 것은 절댓값이 아니라
        /// «누가 위인가»뿐이다. 모형과 실제가 갈라지는 것은 <see cref="CanvasSortingOrderForTests"/>를
        /// 포스트잇의 같은 창구와 대조하는 테스트가 막는다.</summary>
        public const int ClickArbiterLayer = UiClickArbiter.LayerOpenedPanel;

        private enum Tab { Active = 0, Archive = 1 }

        /// <summary>[날짜별] 탭 <b>안의</b> 페이지. 탭이 아니다(UW-6-1).</summary>
        private enum Page { Daily = 0, Calendar = 1 }

        private const float TabChipWidth = 46f;
        private const float TabChipHeight = 22f;
        private const float TabChipGap = 2f;

        private sealed class RowView
        {
            public RectTransform Rect;
            public Image Surface;
            public Image Box;          // 체크박스.
            public Image BoxCheck;
            public Text Label;
            /// <summary>완료 취소선 — 1pt <see cref="Image"/> 선. 결합문자(U+0336) 조립이 아니다.</summary>
            public Image Strike;
            /// <summary>말줄임을 <b>다시 계산해야 하는지</b> 판정하는 캐시(원본 문자열).
            /// <see cref="UiChrome.Ellipsize"/>는 폭을 재려고 <c>Text.text</c>를 여러 번 바꾸므로
            /// 내용이 실제로 바뀐 순간에만 부른다(그 함수의 «호출부 규약»).</summary>
            public string LastLabelSource;
            public bool LastCompleted;
            public Image DeleteButton;
            public Text DeleteGlyph;
            public RectTransform Confirm;
            public Image ConfirmYes;
            public Image ConfirmNo;
            public int BoundId = -1;
        }

        /// <summary>달력 셀 하나. <b>글자는 날짜 숫자 하나뿐</b>이고 상태는 도형으로만 말한다(R6-5-3).</summary>
        private sealed class CellView
        {
            public RectTransform Rect;
            public Image Surface;
            public Image Outline;
            public Text DayNumber;
            public Image BarTrack;
            public Image BarFill;
            public Image Dot;
            public int DayIndex = TodoItem.UnknownPlannedDay;
            public bool Selectable;
        }

        /// <summary>목록 한 칸. <b>항목이거나 구분선이다</b> — R6-7이 구분선에도 슬롯 하나를 준다
        /// ("미완료 4 + 구분 1 + 완료 8 = 13 > 10 → 2쪽"). 그래서 페이징 계산이 한 축으로 끝난다.</summary>
        private readonly struct Slot
        {
            public readonly TodoItem Item;
            public readonly int CompletedCount;   // 구분선일 때만 의미가 있다.

            public Slot(TodoItem item) { Item = item; CompletedCount = 0; }
            public Slot(int completedCount) { Item = null; CompletedCount = completedCount; }
            public bool IsDivider => Item == null;
        }

        private readonly RowView[] _rows = new RowView[VisibleRows];
        private readonly CellView[] _cells = new CellView[CalendarCellCount];
        private readonly List<TodoItem> _dayBuffer = new List<TodoItem>(32);
        private readonly List<Slot> _slots = new List<Slot>(32);

        private readonly Image[] _tabChips = new Image[2];
        private readonly Text[] _tabLabels = new Text[2];

        private RectTransform _content;
        private RectTransform _dailyPage;
        private RectTransform _calendarPage;

        private Image _prevDay;
        private Image _nextDay;
        private Image _dateToggle;
        private Text _dateToggleLabel;
        private Image _todayChip;
        private Image _tomorrowChip;
        private InputField _input;
        private Text _inputPlaceholder;
        private Image _addSurface;
        private Text _addLabel;
        private Text _emptyTitle;
        private Text _emptyBody;
        private Text _footer;
        private Image _railUp;
        private Image _railDown;

        private RectTransform _divider;
        private Image _dividerLine;
        private Text _dividerCaption;

        private Image _prevMonth;
        private Image _nextMonth;
        private Image _monthToggle;
        private Text _monthLabel;
        private Text _calendarFooter;

        private Tab _tab = Tab.Active;
        private Page _page = Page.Daily;
        private int _scroll;
        private int _confirmingId = -1;
        private float _confirmTimer;
        private bool _softCapHit;

        /// <summary>지금 보고 있는 날짜(에폭 이후 일수). <b>세이브에 칸이 없다</b>(R-10).</summary>
        private int _selectedDay = TodoItem.UnknownPlannedDay;

        /// <summary>선택이 «오늘»에 붙어 있는가. true면 자정에 날짜가 넘어갈 때 <b>따라간다</b>.
        /// 사용자가 특정 날짜를 골라 보고 있으면(false) 건드리지 않는다 — 읽고 있는 화면을 뺏지
        /// 않는다(UW-6-2).</summary>
        private bool _followToday = true;

        /// <summary>달력이 보여 주는 달 안의 아무 날(월을 지목하는 값).</summary>
        private int _calendarAnchorDay = TodoItem.UnknownPlannedDay;

        private int _visibleRowBudget = VisibleRows;
        private float _cellHeight = CalendarCellHeight;

        protected override Vector2 PanelSizePoints => new Vector2(Width, Height);
        protected override string TitleText => "오늘 할일";

        // ★★ 2026-09-08 (사용자 신고 "각 메뉴들의 창을 마우스로 끌어서 움직일수있게 변경했었는데
        //   지금은 또 안됨") — 이 팝오버는 <b>드래그가 애초에 안 붙어 있었다</b>.
        //   손잡이는 «패널 안이면서 어떤 버튼 위도 아닌» 자리다 — 버튼 목록은
        //   <see cref="PopoverPanel"/>가 빌드 직후 한 번 긁어 자동으로 들고 있으므로
        //   여기서 사각형을 손으로 적지 않는다.
        //
        // ★★★ 2026-09-08 R-5 — 그 «빌드 직후 한 번»이 이 라운드의 함정이었다.
        //   persona-stress 감사: "달력 UI 착수 시 확실히 터지는 자리 — _controlRects는 Awake 1회만
        //   긁는다. 나중에 만든 버튼은 드래그 제외 목록에 없고, FeedClick이 TryBeginWindowDrag에서
        //   return하므로 OnGlobalClick이 호출되지 않는다 → 날짜 칸을 누르면 날짜가 안 골라지고 창이 끌린다."
        //   <b>그래서 달력 42칸을 «필요할 때» 만들지 않고 <see cref="BuildContent"/>에서 전부 만든다.</b>
        //   기반 클래스의 스크레이프는 <c>BuildContent</c> <b>다음에</b> 돌고 꺼져 있는 버튼도
        //   함께 긁으므로(<c>GetComponentsInChildren&lt;Button&gt;(true)</c>), 이 순서만 지키면
        //   함정이 <b>구조적으로</b> 닫힌다. 지연 생성으로 바꾸는 사람이 있으면 그 순간 되살아난다 —
        //   <see cref="DragExcludedControlCountForTests"/>를 세는 테스트가 그것을 막는다.
        protected override bool WindowDragEnabled => true;

        protected override UiWindowId WindowDragId => UiWindowId.TodoBoard;

        /// <summary>지금 보고 있는 탭(0=할일, 1=완료함) — 테스트/진단 전용.</summary>
        public int ActiveTab => (int)_tab;

        /// <summary>지금 달력 페이지를 보고 있는가(같은 탭 안의 페이지 전환 — UW-6-1).</summary>
        public bool IsCalendarPage => _page == Page.Calendar;

        /// <summary>삭제 확인이 열려 있는 항목 Id(-1이면 없음).</summary>
        public int PendingDeleteId => _confirmingId;

        private void OnEnable() => StickmanEventBus.TodoListChanged += OnTodoListChanged;

        protected override void OnDisable()
        {
            base.OnDisable();
            StickmanEventBus.TodoListChanged -= OnTodoListChanged;
            UiClickArbiter.WithdrawSurface(ClickArbiterSurfaceId);
        }

        private void OnTodoListChanged()
        {
            if (IsOpen) RefreshContent();
        }

        // ====================================================================================
        // 날짜 — 정수 ↔ 달력. <b>여기에 시계가 없다</b>(UW-6-2).
        // ====================================================================================

        /// <summary>요일 이름. 인덱스는 <see cref="System.DayOfWeek"/> 그대로(0=일요일).</summary>
        public static readonly string[] WeekdayNames = { "일", "월", "화", "수", "목", "금", "토" };

        /// <summary>일자 번호의 기산점. <see cref="CurrencyRules.LocalDayIndex"/>가 쓰는 바로 그 날이다
        /// (그쪽 필드가 <c>private</c>이라 참조할 수 없어 같은 값을 하나 둔다 — 갈라지면
        /// <c>TodoBoardDateAxisLayoutTests</c>가 <see cref="CurrencyRules.LocalDayIndex"/>로 왕복해 잡는다).</summary>
        private static readonly System.DateTime DayIndexEpochUtc =
            new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

        /// <summary>일자 번호 → 그 날의 달력 날짜. <b>순수 함수</b>이고 벽시계를 읽지 않는다.</summary>
        public static System.DateTime DateOfDayIndex(int dayIndex) => DayIndexEpochUtc.AddDays(dayIndex);

        /// <summary>달력 날짜 → 일자 번호(<see cref="DateOfDayIndex"/>의 역함수).</summary>
        public static int DayIndexOfDate(System.DateTime date)
            => (int)System.Math.Floor((date.Date - DayIndexEpochUtc.Date).TotalDays);

        /// <summary>일별 페이지의 날짜 라벨(달력 토글이기도 하다) — <c>9월 8일 (화) ▾</c>.</summary>
        public static string FormatDayLabel(int dayIndex)
        {
            System.DateTime d = DateOfDayIndex(dayIndex);
            return $"{d.Month}월 {d.Day}일 ({WeekdayNames[(int)d.DayOfWeek]}) ▾";
        }

        /// <summary>달력 페이지의 달 라벨 — <c>2026년 9월</c>.</summary>
        public static string FormatMonthLabel(int dayIndex)
        {
            System.DateTime d = DateOfDayIndex(dayIndex);
            return $"{d.Year}년 {d.Month}월";
        }

        /// <summary>이 앱에서 «오늘»을 아는 유일한 창구. 0이면 <b>아직 날짜 축이 없다</b>
        /// (저장 파일을 읽고 <c>CurrencyDayRolloverTicker.CheckNow</c>가 돌기 전의 몇 프레임).</summary>
        private static int TodayIndex => CurrencyModel.DayIndex;

        /// <summary>날짜 축이 확정됐는가. false면 이 창은 <b>날짜가 생기기 전의 화면</b>으로 동작한다
        /// (현행 그대로 «전부 한 목록») — 0을 1970-01-01로 읽는 경로를 만들지 않는다(UW-6-4).</summary>
        private static bool HasDayAxis => TodayIndex != TodoItem.UnknownPlannedDay;

        /// <summary>탐색 하한 = <c>min(오늘, 가장 이른 예정일)</c>. 빈 달을 무한히 넘길 수 있으면
        /// 사용자는 그걸 고장으로 읽는다(R6-8 #5).</summary>
        private static int MinSelectableDay
        {
            get
            {
                int today = TodayIndex;
                return TodoListModel.TryGetEarliestPlannedDay(out int earliest) && earliest < today
                    ? earliest
                    : today;
            }
        }

        /// <summary>탐색 상한 = <b>내일</b>. 입력 정의역이 오늘·내일이므로 정의역이 정확히 닫힌다(R6-8 #6).</summary>
        private static int MaxSelectableDay => TodayIndex + 1;

        // ====================================================================================
        // UW-6-5 인계 — 소프트캡의 <b>정의</b>가 옮겨 갈 자리 (값은 design-systems 소관)
        // ====================================================================================
        //
        // ux-widgets R6-8 원문: *"todoActiveCountSoftCap = 15는 지금 «전체 미완료» 기준이다.
        // 날짜 축이 생기면 미완료가 여러 날에 흩어져 15가 쉽게 넘고, 경고가 오늘과 무관하게 뜬다.
        // 정의를 「오늘 페이지에 실제로 뜨는 것(오늘 예정 + 밀린 것 + 미상)」으로 옮겨야 한다."*
        //
        // ★ 이 라운드는 <b>세는 법만</b> 만든다. <see cref="TodoListModel.Add"/>가 쓰는 기준은
        //   아직 «전체 미완료»(<see cref="TodoListModel.UncompletedCount"/>) 그대로다 — 정의를
        //   바꾸는 것은 값(캡 숫자)과 한 몸이고, 그 값은 내 소관이 아니다. 숫자를 안 정한 채
        //   기준만 좁히면 «경고가 영영 안 뜨는» 반대쪽 사고가 된다.

        /// <summary>
        /// <b>오늘 페이지에 실제로 뜨는 미완료 개수</b> = 오늘 예정 + 밀린 것 + 날짜 미상.
        /// <para>화면이 세는 것과 <b>같은 집합</b>을 센다 — 이 함수와 <see cref="BuildSlots"/>가
        /// 같은 조회 API 3개(<see cref="TodoListModel.AppendOverdueItems"/> ·
        /// <see cref="TodoListModel.AppendUnknownPlannedDayItems"/> ·
        /// <see cref="TodoListModel.AppendItemsForDay"/>)를 지나므로, 경고 기준과 사용자가 보는
        /// 화면이 갈라질 자리가 없다.</para>
        /// <para><paramref name="todayIndex"/>가 «미상»이면 <see cref="TodoListModel.UncompletedCount"/>를
        /// 그대로 돌려준다 — 날짜 축이 서기 전에는 «오늘 페이지»가 곧 «전부»다.</para>
        /// <para><b>저장을 건드리지 않는다</b>(R-10) — 조회 API 3개가 전부 읽기 전용이다.</para>
        /// </summary>
        public static int CountTodaySurfaceUncompleted(int todayIndex)
        {
            if (todayIndex == TodoItem.UnknownPlannedDay) return TodoListModel.UncompletedCount;

            // 정적 버퍼를 재사용한다 — 하루 종일 켜져 있는 앱에서 세러 들어올 때마다 리스트를
            // 새로 굽지 않는다(Core/DockGeometry가 명문화한 무할당 관례. 메인 스레드 전용이고
            // 값을 즉시 세고 버리므로 재진입도 공유도 없다).
            CountBuffer.Clear();
            TodoListModel.AppendOverdueItems(todayIndex, CountBuffer);
            TodoListModel.AppendUnknownPlannedDayItems(CountBuffer);
            TodoListModel.AppendItemsForDay(todayIndex, CountBuffer);

            int n = 0;
            for (int i = 0; i < CountBuffer.Count; i++)
            {
                if (!CountBuffer[i].Completed) n++;
            }
            CountBuffer.Clear();
            return n;
        }

        private static readonly List<TodoItem> CountBuffer = new List<TodoItem>(32);

        /// <summary>이 날짜에 <b>새로 적을 수 있는가</b> — 오늘과 내일뿐이다(사용자 요구 ②).</summary>
        public static bool IsWritableDay(int dayIndex, int todayIndex)
            => todayIndex != TodoItem.UnknownPlannedDay
               && (dayIndex == todayIndex || dayIndex == todayIndex + 1);

        /// <summary>입력칸 안내 문구 — <b>대상 날짜를 말한다</b>(R6-3).</summary>
        public static string ResolveInputPlaceholder(int dayIndex, int todayIndex)
        {
            if (todayIndex == TodoItem.UnknownPlannedDay) return "할일을 적어보세요";
            if (dayIndex == todayIndex) return "오늘 할일을 적어보세요";
            if (dayIndex == todayIndex + 1) return "내일 할일을 적어보세요";
            return "이 날짜엔 새로 적을 수 없어요";
        }

        // ==================== 내용 만들기 ====================

        protected override void BuildContent(RectTransform content)
        {
            _content = content;
            BuildTabs();

            _dailyPage = NewPage(content, "DailyPage");
            _calendarPage = NewPage(content, "CalendarPage");

            BuildDailyPage(_dailyPage);
            BuildCalendarPage(_calendarPage);

            _calendarPage.gameObject.SetActive(false);

            // 설계 크기로 한 번 눕혀 둔다. 화면이 작아 클램프가 걸리면
            // PopoverPanel.ApplyPanelSize → OnPanelSizeChanged가 다시 부른다.
            LayoutForPanelSize(AppliedPanelSizePoints);
        }

        private static RectTransform NewPage(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            PlaceStretch(rt, 0f, 0f, 0f, 0f);
            return rt;
        }

        private void BuildTabs()
        {
            string[] names = { "날짜별", "완료함" };
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                Image chip = UiChrome.AddSurface(Panel, "Tab" + i, UiChrome.CardSurface, UiChrome.RadiusChip);

                // ★ 2026-09-08 — 탭 칩을 <b>우상단 앵커</b>로 옮겼다. 예전에는 <c>CloseChipLeft</c>
                //   (= 설계 폭에서 역산한 x)에 걸려 있어서, 패널이 화면 클램프로 줄어드는 순간
                //   [✕]는 따라 들어오는데 <b>탭 칩만 창 밖에 남았다</b>. 정보창이 2026-08-30에 겪은
                //   사고와 같은 형태이고, 기반 클래스가 이번 라운드에 제목/[✕]를 같은 이유로 이미
                //   오른쪽 앵커로 옮겼다 — 여기만 빠져 있었다.
                //   설계 폭에서의 자리는 <b>한 픽셀도 바뀌지 않는다</b>:
                //     탭1 오른끝 = 500 − (16+44+4) = 436, 왼끝 390 ... 아니라
                //     오른쪽 여백 = Space4 + CloseChipWidth + Space1 = 64 → 탭1 [390, 436]
                //     탭0 오른쪽 여백 = 64 + 46 + 2 = 112 → 탭0 [342, 388]  (R6-5-1 검산 «좌단 342» ✔)
                PlaceTopRight(chip.rectTransform,
                    UiChrome.Space4 + CloseChipWidth + UiChrome.Space1
                        + (1 - i) * (TabChipWidth + TabChipGap),
                    -UiChrome.Space3, TabChipWidth, TabChipHeight);

                UiChrome.AddOutline(chip.rectTransform, "Outline",
                    UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
                Text label = UiChrome.AddText(chip.rectTransform, "Label", UiChrome.FontCaption,
                    TextAnchor.MiddleCenter, UiChrome.TextSecondary);
                UiChrome.Stretch(label.rectTransform);
                label.text = names[i];
                _tabChips[i] = chip;
                _tabLabels[i] = label;
                WireGuarded(chip, "tab" + i, () => SelectTab((Tab)index));
            }
        }

        // ==================== 일별 페이지 ====================

        private void BuildDailyPage(RectTransform page)
        {
            // ---- 날짜 네비 줄 ----
            _prevDay = BuildGlyphButton(page, "PrevDay", "‹", "prevDay", () => StepDay(-1));
            UiChrome.PlaceTopLeft(_prevDay.rectTransform, 0f, 0f, NavArrowSize, NavRowHeight);

            _dateToggle = UiChrome.AddSurface(page, "DateToggle", UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(_dateToggle.rectTransform, DateToggleLeft, 0f, DateToggleWidth, NavRowHeight);
            UiChrome.AddOutline(_dateToggle.rectTransform, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
            _dateToggleLabel = UiChrome.AddText(_dateToggle.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextPrimary, bold: true);
            UiChrome.Stretch(_dateToggleLabel.rectTransform);
            WireGuarded(_dateToggle, "dateToggle", () => SelectPage(Page.Calendar));

            _nextDay = BuildGlyphButton(page, "NextDay", "›", "nextDay", () => StepDay(1));
            UiChrome.PlaceTopLeft(_nextDay.rectTransform, NextArrowLeft, 0f, NavArrowSize, NavRowHeight);

            _todayChip = BuildChipButton(page, "TodayChip", "오늘", "todayChip", JumpToToday);
            PlaceTopRight(_todayChip.rectTransform, DayChipWidth + DayChipGap, 0f, DayChipWidth, DayChipHeight);

            _tomorrowChip = BuildChipButton(page, "TomorrowChip", "내일", "tomorrowChip", JumpToTomorrow);
            PlaceTopRight(_tomorrowChip.rectTransform, 0f, 0f, DayChipWidth, DayChipHeight);

            // ---- 입력 줄 ----
            _input = CreateInputField(page);
            PlaceTopStretch(_input.GetComponent<RectTransform>(), 0f, AddButtonWidth + InputToAddGap,
                InputRowTop, InputRowHeight);
            _input.onEndEdit.AddListener(OnInputSubmitted);

            _addSurface = UiChrome.AddSurface(page, "Add", UiChrome.Accent, UiChrome.RadiusChip);
            PlaceTopRight(_addSurface.rectTransform, 0f, InputRowTop, AddButtonWidth, InputRowHeight);
            _addLabel = UiChrome.AddText(_addSurface.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.OnAccentSolid, bold: true);
            UiChrome.Stretch(_addLabel.rectTransform);
            _addLabel.text = "추가";
            WireGuarded(_addSurface, "add", AddFromInput);

            // ---- 목록 ----
            for (int i = 0; i < VisibleRows; i++) _rows[i] = BuildRow(page, i);
            BuildDivider(page);

            _emptyTitle = UiChrome.AddText(page, "EmptyTitle", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            PlaceTopStretch(_emptyTitle.rectTransform, 0f, 0f, ListTop - ListHeight * 0.5f + 20f, 18f);
            _emptyTitle.text = "오늘은 아직 비어 있어요";

            _emptyBody = UiChrome.AddText(page, "EmptyBody", UiChrome.FontLabel,
                TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            PlaceTopStretch(_emptyBody.rectTransform, 0f, 0f, ListTop - ListHeight * 0.5f, 16f);
            _emptyBody.text = "위에 적어두면 제가 가끔 챙겨줄게요.";

            // ---- 페이지 넘김 레일 ([▲][▼] — 휠에 기대지 않는다) ----
            _railUp = BuildRail(page, "RailUp", "▲", ListTop, () => Scroll(-1));
            _railDown = BuildRail(page, "RailDown", "▼", ListTop - ListHeight + RowHeight, () => Scroll(1));

            _footer = UiChrome.AddText(page, "Footer", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            PlaceBottomStretch(_footer.rectTransform, 0f, 0f, DailyFooterBottom, FooterHeight);
        }

        private RowView BuildRow(RectTransform page, int index)
        {
            var view = new RowView();
            view.Surface = UiChrome.AddSurface(page, "Row" + index, UiChrome.CardSurface, UiChrome.RadiusCard);
            view.Rect = view.Surface.rectTransform;
            PlaceTopStretch(view.Rect, 0f, RowRightInset, ListTop - index * (RowHeight + RowGap), RowHeight);
            UiChrome.AddOutline(view.Rect, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusCard);

            view.Box = UiChrome.AddSurface(view.Rect, "Box", UiChrome.SubtleSurface, 4);
            UiChrome.PlaceTopLeft(view.Box.rectTransform, RowBoxLeft, -(RowHeight - RowBoxSize) * 0.5f, RowBoxSize, RowBoxSize);
            UiChrome.AddOutline(view.Box.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.SubtleSurface), 4);
            view.BoxCheck = UiChrome.AddCircle(view.Box.rectTransform, "Dot", 10f, UiChrome.Accent);
            view.BoxCheck.gameObject.SetActive(false);

            view.Label = UiChrome.AddText(view.Rect, "Label", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            // ★ 라벨 상자도 <b>행을 따라 늘어나는 앵커</b>다. 고정 폭이면 패널이 줄었을 때
            //   상자만 [✕] 위로 올라탄다(기반 클래스가 제목 상자에서 같은 처방을 쓴다).
            PlaceTopStretch(view.Label.rectTransform, RowLabelLeft, RowLabelRightInset,
                -(RowHeight - RowLabelHeight) * 0.5f, RowLabelHeight);

            // ---- 취소선(완료 표시) ----
            // ★ 2026-09-08 R6-0-2(나) / docs/UX_WIDGETS.md UW-6-3 — 여기 있던 것은 글자마다 U+0336을
            //   끼워 넣는 <b>결합문자 조립</b>이었다. <see cref="TodoPostItWidget"/>는 같은 문제를 이미
            //   <b>1pt Image 선</b>으로 풀어 놨다. 신규 설계를 만들지 않고 그 처방을 그대로 옮긴다.
            //   (라벨 <b>뒤</b>가 아니라 <b>다음 형제</b>로 만든다: uGUI는 나중 형제가 위에 그려진다.)
            view.Strike = UiChrome.AddSurface(view.Rect, "Strike", UiChrome.TextTertiary, UiChrome.RadiusDot);
            RectTransform strikeRect = view.Strike.rectTransform;
            strikeRect.anchorMin = strikeRect.anchorMax = new Vector2(0f, 1f);
            strikeRect.pivot = new Vector2(0f, 0.5f);
            // 라벨은 행 안에서 세로 정중앙이다(위 PlaceTopStretch의 -(RowHeight - RowLabelHeight)*0.5).
            // 그래서 선의 y도 -RowHeight*0.5 하나로 떨어진다 — 두 식이 갈라질 여지를 두지 않는다.
            strikeRect.anchoredPosition = new Vector2(RowLabelLeft, -RowHeight * 0.5f);
            strikeRect.sizeDelta = new Vector2(0f, 1f);
            view.Strike.raycastTarget = false;
            view.Strike.gameObject.SetActive(false);

            view.DeleteButton = UiChrome.AddSurface(view.Rect, "Delete", new Color(0f, 0f, 0f, 0f), UiChrome.RadiusChip);
            PlaceTopRight(view.DeleteButton.rectTransform, RowDeleteRightInset,
                -(RowHeight - RowDeleteSize) * 0.5f, RowDeleteSize, RowDeleteSize);
            view.DeleteGlyph = UiChrome.AddText(view.DeleteButton.rectTransform, "Glyph", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            UiChrome.Stretch(view.DeleteGlyph.rectTransform);
            view.DeleteGlyph.text = "✕";

            // ---- 삭제 확인 오버레이(같은 자리에서 3초) ----
            var confirmGo = new GameObject("Confirm", typeof(RectTransform));
            confirmGo.transform.SetParent(view.Rect, false);
            view.Confirm = confirmGo.GetComponent<RectTransform>();
            UiChrome.Stretch(view.Confirm);

            Image confirmSurface = UiChrome.AddSurface(view.Confirm, "Surface", UiChrome.CardSurfaceMuted, UiChrome.RadiusCard);
            UiChrome.Stretch(confirmSurface.rectTransform);
            Text ask = UiChrome.AddText(view.Confirm, "Ask", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(ask.rectTransform, 10f, -(RowHeight - 16f) * 0.5f, 90f, 16f);
            ask.text = "삭제할까요?";

            view.ConfirmYes = UiChrome.AddSurface(view.Confirm, "Yes", UiChrome.Accent, UiChrome.RadiusChip);
            PlaceTopRight(view.ConfirmYes.rectTransform, UiChrome.Space2 + 52f + UiChrome.Space1,
                -(RowHeight - 22f) * 0.5f, 52f, 22f);
            Text yes = UiChrome.AddText(view.ConfirmYes.rectTransform, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.OnAccentSolid, bold: true);
            UiChrome.Stretch(yes.rectTransform);
            yes.text = "삭제";

            view.ConfirmNo = UiChrome.AddSurface(view.Confirm, "No", UiChrome.CardSurface, UiChrome.RadiusChip);
            PlaceTopRight(view.ConfirmNo.rectTransform, UiChrome.Space2, -(RowHeight - 22f) * 0.5f, 52f, 22f);
            UiChrome.AddOutline(view.ConfirmNo.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
            Text no = UiChrome.AddText(view.ConfirmNo.rectTransform, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(no.rectTransform);
            no.text = "취소";

            view.Confirm.gameObject.SetActive(false);

            int rowIndex = index;
            WireGuarded(view.Surface, "row" + index, () => OnRowClicked(rowIndex));
            WireGuarded(view.DeleteButton, "rowDelete" + index, () => OnDeleteClicked(rowIndex));
            WireGuarded(view.ConfirmYes, "rowYes" + index, () => OnConfirmDelete(rowIndex));
            WireGuarded(view.ConfirmNo, "rowNo" + index, CancelDeleteConfirm);
            return view;
        }

        /// <summary>미완료 그룹과 완료 그룹을 가르는 줄(R6-7). <b>슬롯 하나를 차지한다.</b>
        /// <para>선은 α&lt;1 토큰(<see cref="UiChrome.Divider"/>)을 그대로 쓰지 않고
        /// <see cref="UiChrome.Flatten"/>으로 <b>불투명 등가색</b>을 만들어 칠한다 — 이 오버레이에서
        /// α&lt;1은 그 화소의 창 알파를 끌어내려 데스크톱을 비춘다(위 클래스 문서).</para></summary>
        private void BuildDivider(RectTransform page)
        {
            var go = new GameObject("CompletedDivider", typeof(RectTransform));
            go.transform.SetParent(page, false);
            _divider = go.GetComponent<RectTransform>();
            PlaceTopStretch(_divider, 0f, RowRightInset, ListTop, RowHeight);

            _dividerLine = UiChrome.AddSurface(_divider, "Line",
                UiChrome.Flatten(UiChrome.Divider, UiChrome.PanelSurface), UiChrome.RadiusDot);
            RectTransform lineRect = _dividerLine.rectTransform;
            lineRect.anchorMin = new Vector2(0f, 0.5f);
            lineRect.anchorMax = new Vector2(1f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.offsetMin = new Vector2(0f, -0.5f);
            lineRect.offsetMax = new Vector2(0f, 0.5f);
            _dividerLine.raycastTarget = false;

            // 글자 뒤를 패널 바탕색으로 덮어 선을 끊는다(불투명 — 알파를 쓰지 않는다).
            Image capBg = UiChrome.AddSurface(_divider, "CaptionBackdrop", UiChrome.PanelSurface, UiChrome.RadiusDot);
            RectTransform bgRect = capBg.rectTransform;
            bgRect.anchorMin = bgRect.anchorMax = bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(80f, 14f);
            capBg.raycastTarget = false;

            _dividerCaption = UiChrome.AddText(_divider, "Caption", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            RectTransform capRect = _dividerCaption.rectTransform;
            capRect.anchorMin = capRect.anchorMax = capRect.pivot = new Vector2(0.5f, 0.5f);
            capRect.sizeDelta = new Vector2(80f, 14f);

            _divider.gameObject.SetActive(false);
        }

        private Image BuildRail(RectTransform page, string name, string glyph, float y, System.Action action)
        {
            Image surface = UiChrome.AddSurface(page, name, UiChrome.CardSurface, UiChrome.RadiusChip);
            PlaceTopRight(surface.rectTransform, 0f, y, RailWidth, RowHeight);
            UiChrome.AddOutline(surface.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
            Text label = UiChrome.AddText(surface.rectTransform, "Glyph", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;
            WireGuarded(surface, name, action);
            return surface;
        }

        private Image BuildGlyphButton(RectTransform parent, string name, string glyph,
            string actionKey, System.Action action)
        {
            Image surface = UiChrome.AddSurface(parent, name, UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.AddOutline(surface.rectTransform, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
            Text label = UiChrome.AddText(surface.rectTransform, "Glyph", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;
            WireGuarded(surface, actionKey, action);
            return surface;
        }

        private Image BuildChipButton(RectTransform parent, string name, string text,
            string actionKey, System.Action action)
        {
            Image surface = UiChrome.AddSurface(parent, name, UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.AddOutline(surface.rectTransform, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
            Text label = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(label.rectTransform);
            label.text = text;
            WireGuarded(surface, actionKey, action);
            return surface;
        }

        private InputField CreateInputField(Transform parent)
        {
            Image surface = UiChrome.AddSurface(parent, "TodoInput", UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.AddOutline(surface.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);

            Text text = UiChrome.AddText(surface.rectTransform, "Text", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            UiChrome.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(UiChrome.Space3, 0f);
            text.rectTransform.offsetMax = new Vector2(-UiChrome.Space3, 0f);
            text.supportRichText = false;

            _inputPlaceholder = UiChrome.AddText(surface.rectTransform, "Placeholder", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.Stretch(_inputPlaceholder.rectTransform);
            _inputPlaceholder.rectTransform.offsetMin = new Vector2(UiChrome.Space3, 0f);
            _inputPlaceholder.rectTransform.offsetMax = new Vector2(-UiChrome.Space3, 0f);
            _inputPlaceholder.text = "할일을 적어보세요";

            var input = surface.gameObject.AddComponent<InputField>();
            input.targetGraphic = surface;
            input.textComponent = text;
            input.placeholder = _inputPlaceholder;
            input.characterLimit = InputCharacterLimit;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        // ==================== 달력 페이지 ====================

        private void BuildCalendarPage(RectTransform page)
        {
            _prevMonth = BuildGlyphButton(page, "PrevMonth", "‹", "prevMonth", () => StepMonth(-1));
            UiChrome.PlaceTopLeft(_prevMonth.rectTransform, 0f, 0f, NavArrowSize, NavRowHeight);

            _monthToggle = UiChrome.AddSurface(page, "MonthToggle", UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(_monthToggle.rectTransform, DateToggleLeft, 0f, DateToggleWidth, NavRowHeight);
            UiChrome.AddOutline(_monthToggle.rectTransform, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
            _monthLabel = UiChrome.AddText(_monthToggle.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextPrimary, bold: true);
            UiChrome.Stretch(_monthLabel.rectTransform);
            WireGuarded(_monthToggle, "monthToggle", () => SelectPage(Page.Daily));

            _nextMonth = BuildGlyphButton(page, "NextMonth", "›", "nextMonth", () => StepMonth(1));
            PlaceTopRight(_nextMonth.rectTransform, 0f, 0f, NavArrowSize, NavRowHeight);

            for (int c = 0; c < CalendarColumns; c++)
            {
                Text head = UiChrome.AddText(page, "Weekday" + c, UiChrome.FontCaption,
                    TextAnchor.MiddleCenter, UiChrome.TextTertiary);
                UiChrome.PlaceTopLeft(head.rectTransform, c * (CalendarCellWidth + CalendarColumnGap),
                    WeekdayRowTop, CalendarCellWidth, WeekdayRowHeight);
                head.text = WeekdayNames[c];
            }

            // ★ 42칸을 <b>여기서 전부</b> 만든다 — 지연 생성하면 persona-stress R-5가 되살아난다
            //   (클래스 상단 WindowDragEnabled 문단 참고).
            for (int i = 0; i < CalendarCellCount; i++) _cells[i] = BuildCell(page, i);

            _calendarFooter = UiChrome.AddText(page, "CalendarFooter", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            PlaceBottomStretch(_calendarFooter.rectTransform, 0f, 0f, CalendarFooterBottom, FooterHeight);
        }

        private CellView BuildCell(RectTransform page, int index)
        {
            var cell = new CellView();
            cell.Surface = UiChrome.AddSurface(page, "Cell" + index, UiChrome.CardSurface, UiChrome.RadiusChip);
            cell.Rect = cell.Surface.rectTransform;
            UiChrome.PlaceTopLeft(cell.Rect,
                (index % CalendarColumns) * (CalendarCellWidth + CalendarColumnGap),
                CalendarGridTop - (index / CalendarColumns) * (CalendarCellHeight + CalendarRowGap),
                CalendarCellWidth, CalendarCellHeight);
            cell.Outline = UiChrome.AddOutline(cell.Rect, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);

            cell.DayNumber = UiChrome.AddText(cell.Rect, "Day", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextPrimary);
            UiChrome.Stretch(cell.DayNumber.rectTransform);

            // 진행 막대 — 셀 하단 2pt. «완료/전체»를 도형으로만 말한다(글자 0 · 숫자 0).
            cell.BarTrack = UiChrome.AddSurface(cell.Rect, "BarTrack",
                UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.CardSurface), UiChrome.RadiusDot);
            RectTransform track = cell.BarTrack.rectTransform;
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(UiChrome.Space2, UiChrome.Space1);
            track.offsetMax = new Vector2(-UiChrome.Space2, UiChrome.Space1 + CalendarProgressBarHeight);
            cell.BarTrack.raycastTarget = false;

            cell.BarFill = UiChrome.AddSurface(cell.BarTrack.rectTransform, "BarFill", UiChrome.Accent, UiChrome.RadiusDot);
            RectTransform fill = cell.BarFill.rectTransform;
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fill.sizeDelta = new Vector2(0f, 0f);
            cell.BarFill.raycastTarget = false;

            // 남은 것이 있다는 표시 — 우상단 점 하나. <b>개수는 세지 않는다</b>(달력은 지도지 보고서가 아니다).
            cell.Dot = UiChrome.AddCircle(cell.Rect, "Dot", CalendarDotDiameter, UiChrome.WarmAccent);
            RectTransform dot = cell.Dot.rectTransform;
            dot.anchorMin = dot.anchorMax = new Vector2(1f, 1f);
            dot.pivot = new Vector2(1f, 1f);
            dot.anchoredPosition = new Vector2(-UiChrome.Space1, -UiChrome.Space1);
            cell.Dot.gameObject.SetActive(false);

            int cellIndex = index;
            WireGuarded(cell.Surface, "cell" + index, () => OnCellClicked(cellIndex));
            return cell;
        }

        // ==================== 크기 변화 — 값 하나에서 배치가 따라온다 ====================

        /// <summary>이 패널 높이에서 <b>실제로 보이는 행 수</b>. <b>순수 함수</b>다(<c>Screen</c>을 읽지 않는다)
        /// — 화면이 작아 <see cref="PopoverPanel.ResolvePanelSizePoints"/>가 창을 줄이면 설계 상수 10이
        /// 아니라 이 값이 진실이 된다.</summary>
        public static int ResolveVisibleRows(float panelHeightPoints)
        {
            float room = panelHeightPoints - ChromeVerticalPoints + ListTop - FooterHeight - UiChrome.Space1;
            int rows = Mathf.FloorToInt((room + RowGap) / (RowHeight + RowGap));
            return Mathf.Clamp(rows, 1, VisibleRows);
        }

        /// <summary>이 패널 높이에서의 달력 셀 높이(pt). 6주는 <b>항상</b> 그린다 — 한 주를 숨기면
        /// 그 달의 마지막 날들이 통째로 사라진다.</summary>
        public static float ResolveCalendarCellHeight(float panelHeightPoints)
        {
            float room = panelHeightPoints - ChromeVerticalPoints + CalendarGridTop
                         - FooterHeight - UiChrome.Space1;
            float h = (room - (CalendarWeekRows - 1) * CalendarRowGap) / CalendarWeekRows;
            return Mathf.Clamp(h, 8f, CalendarCellHeight);
        }

        /// <summary>패널이 <b>실제로 줄거나 늘어난</b> 프레임에 기반 클래스가 부른다.</summary>
        protected override void OnPanelSizeChanged(Vector2 sizePoints)
        {
            LayoutForPanelSize(sizePoints);
            if (IsOpen) RefreshContent();
        }

        private void LayoutForPanelSize(Vector2 sizePoints)
        {
            _visibleRowBudget = ResolveVisibleRows(sizePoints.y);
            _cellHeight = ResolveCalendarCellHeight(sizePoints.y);

            if (_railDown != null)
            {
                float lastRowTop = ListTop - (_visibleRowBudget - 1) * (RowHeight + RowGap);
                PlaceTopRight(_railDown.rectTransform, 0f, lastRowTop, RailWidth, RowHeight);
            }

            for (int i = 0; i < CalendarCellCount; i++)
            {
                CellView cell = _cells[i];
                if (cell == null) continue;
                UiChrome.PlaceTopLeft(cell.Rect,
                    (i % CalendarColumns) * (CalendarCellWidth + CalendarColumnGap),
                    CalendarGridTop - (i / CalendarColumns) * (_cellHeight + CalendarRowGap),
                    CalendarCellWidth, _cellHeight);
            }
        }

        // ==================== 동작 ====================

        private void OnInputSubmitted(string _)
        {
            // Enter로 확정된 경우에만 추가한다 — 포커스를 잃은 것만으로 멋대로 추가하면
            // "적다 만 것"이 목록에 남는다.
            if (!Input.GetKey(KeyCode.Return) && !Input.GetKey(KeyCode.KeypadEnter)) return;
            if (UiClickArbiter.TryClaimFrame(ClickArbiterSurfaceId) && TryClaimAction("addEnter")) AddFromInput();
        }

        private void AddFromInput()
        {
            if (_input == null) return;
            string text = _input.text;
            if (string.IsNullOrWhiteSpace(text)) return;

            int today = TodayIndex;
            SyncSelectedDay(today);
            if (HasDayAxis && !IsWritableDay(_selectedDay, today))
            {
                Debug.Log($"[할일패널] 추가 거절 — {FormatDayLabel(_selectedDay)}은 열람 전용입니다" +
                    "(적을 수 있는 날은 오늘과 내일뿐입니다 — docs/UX_WIDGETS.md R6-3).");
                return;
            }

            int softCap = Config != null ? Config.todoActiveCountSoftCap : 15;
            int plannedDay = HasDayAxis ? _selectedDay : TodoItem.UnknownPlannedDay;
            _softCapHit = TodoListModel.Add(text, softCap, plannedDay);
            _input.text = string.Empty;
            _tab = Tab.Active;
            _page = Page.Daily;
            _scroll = 0;
            Debug.Log($"[할일패널] 추가 — \"{text.Trim()}\" → 일자 #{plannedDay}" +
                $"{(plannedDay == TodoItem.UnknownPlannedDay ? "(날짜 미상)" : string.Empty)} " +
                $"(미완료 {TodoListModel.UncompletedCount}건" +
                $"{(_softCapHit ? $", 소프트캡 {softCap}건 초과" : string.Empty)}). 저장 스키마 v13에 남습니다.");
            CharacterSaveStore.Save();   // 사용자가 적은 것은 즉시 남긴다(주기 저장만 믿지 않는다).
            RefreshContent();
        }

        private void SelectTab(Tab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            _page = Page.Daily;
            _scroll = 0;
            CancelDeleteConfirm();
            RefreshContent();
        }

        private void SelectPage(Page page)
        {
            if (_page == page) return;
            _page = page;
            _scroll = 0;
            CancelDeleteConfirm();
            if (page == Page.Calendar) _calendarAnchorDay = _selectedDay;
            RefreshContent();
        }

        /// <summary>날짜를 하루 옮긴다. <b>저장하지 않는다</b>(R-10) — 이 함수가 닿는 것은
        /// 이 컴포넌트의 필드 둘뿐이다.</summary>
        private void StepDay(int delta)
        {
            int today = TodayIndex;
            SyncSelectedDay(today);
            int next = Mathf.Clamp(_selectedDay + delta, MinSelectableDay, MaxSelectableDay);
            if (next == _selectedDay) return;
            _selectedDay = next;
            _followToday = HasDayAxis && next == today;
            _scroll = 0;
            CancelDeleteConfirm();
            RefreshContent();
        }

        private void JumpToToday()
        {
            _followToday = true;
            _selectedDay = TodayIndex;
            _scroll = 0;
            _page = Page.Daily;
            CancelDeleteConfirm();
            RefreshContent();
        }

        private void JumpToTomorrow()
        {
            _followToday = false;
            _selectedDay = TodayIndex + 1;
            _scroll = 0;
            _page = Page.Daily;
            CancelDeleteConfirm();
            RefreshContent();
        }

        private void StepMonth(int delta)
        {
            if (_calendarAnchorDay == TodoItem.UnknownPlannedDay) _calendarAnchorDay = _selectedDay;
            System.DateTime anchor = DateOfDayIndex(_calendarAnchorDay);
            System.DateTime moved = anchor.AddMonths(delta);
            int candidate = DayIndexOfDate(new System.DateTime(moved.Year, moved.Month, 1));
            int lastOfMonth = candidate + System.DateTime.DaysInMonth(moved.Year, moved.Month) - 1;

            // 그 달이 정의역과 하나도 겹치지 않으면 넘기지 않는다 — 빈 달을 무한히 넘길 수 있으면
            // 사용자는 그걸 고장으로 읽는다(R6-8 #5).
            if (lastOfMonth < MinSelectableDay || candidate > MaxSelectableDay) return;

            _calendarAnchorDay = candidate;
            RefreshContent();
        }

        private void OnCellClicked(int cellIndex)
        {
            CellView cell = _cells[cellIndex];
            if (cell == null || !cell.Selectable) return;
            _selectedDay = cell.DayIndex;
            _followToday = HasDayAxis && cell.DayIndex == TodayIndex;
            _page = Page.Daily;
            _scroll = 0;
            CancelDeleteConfirm();
            RefreshContent();
        }

        private void Scroll(int delta)
        {
            int max = Mathf.Max(0, _slots.Count - _visibleRowBudget);
            int next = Mathf.Clamp(_scroll + delta, 0, max);
            if (next == _scroll) return;
            _scroll = next;
            CancelDeleteConfirm();
            RefreshContent();
        }

        private void OnRowClicked(int rowIndex)
        {
            if (_tab == Tab.Archive) return;                 // 완료함은 읽기 전용.
            if (_confirmingId >= 0) return;                   // 확인 중에는 행 클릭을 먹지 않는다.
            RowView row = _rows[rowIndex];
            if (row.BoundId < 0) return;

            TodoListModel.ToggleComplete(row.BoundId);
            CharacterSaveStore.Save();
            RefreshContent();
        }

        private void OnDeleteClicked(int rowIndex)
        {
            if (_tab == Tab.Archive) return;
            RowView row = _rows[rowIndex];
            if (row.BoundId < 0) return;
            _confirmingId = row.BoundId;
            _confirmTimer = 0f;
            RefreshContent();
        }

        private void OnConfirmDelete(int rowIndex)
        {
            RowView row = _rows[rowIndex];
            if (row.BoundId < 0 || row.BoundId != _confirmingId) return;
            TodoListModel.Remove(row.BoundId);
            _confirmingId = -1;
            CharacterSaveStore.Save();
            Debug.Log("[할일패널] 삭제 확정 — 되돌리기가 아니라 인라인 확인을 쓰는 이유는 모델에 복구 API가 없어서다(32-6).");
            RefreshContent();
        }

        private void CancelDeleteConfirm()
        {
            if (_confirmingId < 0) return;
            _confirmingId = -1;
            RefreshContent();
        }

        protected override void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측
            base.Update();

            // ★ 2026-09-08 P0 — «지금 이 사각형의 클릭은 내 것이다»를 매 프레임 알린다.
            //   포스트잇(상시 카드)이 같은 클릭을 자기 전역 폴링으로 함께 먹던 사고를 여기서 막는다.
            //   조건이 «열려 있다»가 아니라 «차단막이 켜져 있다»인 이유: 그 사각형이 곧 이 앱이
            //   클릭관통을 해제한 영역이고, 중재가 답해야 하는 범위와 정확히 같다.
            if (IsOpen && IsClickBlockerEnabled)
            {
                UiClickArbiter.PublishSurface(ClickArbiterSurfaceId, ClickArbiterLayer, PanelScreenRect);
            }
            else
            {
                UiClickArbiter.WithdrawSurface(ClickArbiterSurfaceId);
            }

            if (!IsOpen) return;

            // 자정이 지나 «오늘»이 앞으로 갔는데 선택이 오늘에 붙어 있으면 따라간다(UW-6-2).
            if (_followToday && _selectedDay != TodayIndex)
            {
                _selectedDay = TodayIndex;
                RefreshContent();
            }

            if (_confirmingId < 0) return;
            _confirmTimer += Time.unscaledDeltaTime;
            if (_confirmTimer < DeleteConfirmSeconds) return;
            CancelDeleteConfirm();
        }

        protected override void OnOpened()
        {
            // 여는 그 프레임부터 소유권을 알린다 — 첫 Update를 기다리면 그 한 프레임 동안
            // 포스트잇이 팝오버 위 클릭을 먹을 수 있다.
            UiClickArbiter.PublishSurface(ClickArbiterSurfaceId, ClickArbiterLayer, PanelScreenRect);
        }

        protected override void OnClosing() => UiClickArbiter.WithdrawSurface(ClickArbiterSurfaceId);

        // ==================== 갱신 ====================

        /// <summary>«오늘»을 다시 읽고 선택을 정의역 안으로 접는다. <b>저장을 건드리지 않는다</b>.</summary>
        private void SyncSelectedDay(int today)
        {
            if (_followToday || _selectedDay == TodoItem.UnknownPlannedDay) _selectedDay = today;
            if (!HasDayAxis) { _selectedDay = today; return; }
            _selectedDay = Mathf.Clamp(_selectedDay, MinSelectableDay, MaxSelectableDay);
        }

        /// <summary>지금 페이지가 그려야 하는 슬롯 목록을 만든다(항목 + 완료 구분선).</summary>
        private void BuildSlots(int today)
        {
            _dayBuffer.Clear();
            _slots.Clear();

            if (_tab == Tab.Archive)
            {
                IReadOnlyList<TodoItem> archive = TodoListModel.CompletedArchive;
                for (int i = 0; i < archive.Count; i++) _slots.Add(new Slot(archive[i]));
                return;
            }

            if (!HasDayAxis)
            {
                // 날짜 축이 아직 없다(저장 파일을 읽고 롤오버 시계가 돌기 전) — 현행 그대로 전부 보여준다.
                // 0을 «1970-01-01»로 읽어 아무것도 못 찾는 화면을 만들지 않는다(UW-6-4).
                IReadOnlyList<TodoItem> active = TodoListModel.ActiveItems;
                for (int i = 0; i < active.Count; i++) _slots.Add(new Slot(active[i]));
                return;
            }

            if (_selectedDay == today)
            {
                // 오늘 페이지 = 밀린 것 + 날짜 미상 + 오늘 것(미완료 → 완료). 데이터는 옮기지 않고
                // 뷰가 합친다(R6-7) — 밀린 항목은 과거 날짜 페이지에도 그대로 남는다(달력이 원장이다).
                TodoListModel.AppendOverdueItems(today, _dayBuffer);
                TodoListModel.AppendUnknownPlannedDayItems(_dayBuffer);
            }
            TodoListModel.AppendItemsForDay(_selectedDay, _dayBuffer);

            int firstCompleted = -1;
            for (int i = 0; i < _dayBuffer.Count; i++)
            {
                if (!_dayBuffer[i].Completed) continue;
                firstCompleted = i;
                break;
            }

            for (int i = 0; i < _dayBuffer.Count; i++)
            {
                // 구분선은 <b>양쪽에 항목이 있을 때만</b> 넣는다(R6-7).
                if (i == firstCompleted && firstCompleted > 0)
                {
                    _slots.Add(new Slot(_dayBuffer.Count - firstCompleted));
                }
                _slots.Add(new Slot(_dayBuffer[i]));
            }
        }

        protected override void RefreshContent()
        {
            int today = TodayIndex;
            SyncSelectedDay(today);

            for (int i = 0; i < 2; i++)
            {
                bool on = (int)_tab == i;
                _tabChips[i].color = on ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.PanelSurface) : UiChrome.CardSurface;
                _tabLabels[i].color = on ? UiChrome.TextOnAccent : UiChrome.TextSecondary;
                _tabLabels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            }

            bool calendar = _tab == Tab.Active && _page == Page.Calendar;
            if (_dailyPage.gameObject.activeSelf == calendar) _dailyPage.gameObject.SetActive(!calendar);
            if (_calendarPage.gameObject.activeSelf != calendar) _calendarPage.gameObject.SetActive(calendar);

            if (calendar) { RefreshCalendar(today); return; }
            RefreshDaily(today);
        }

        private void RefreshDaily(int today)
        {
            BuildSlots(today);
            _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, _slots.Count - _visibleRowBudget));

            bool archive = _tab == Tab.Archive;
            int dividerSlot = -1;

            for (int i = 0; i < VisibleRows; i++)
            {
                RowView row = _rows[i];
                int slotIndex = _scroll + i;
                bool used = i < _visibleRowBudget && slotIndex < _slots.Count && !_slots[slotIndex].IsDivider;
                if (row.Rect.gameObject.activeSelf != used) row.Rect.gameObject.SetActive(used);
                if (!used)
                {
                    row.BoundId = -1;
                    if (i < _visibleRowBudget && slotIndex < _slots.Count) dividerSlot = i;
                    continue;
                }

                TodoItem item = _slots[slotIndex].Item;
                row.BoundId = item.Id;

                // ★ 2026-09-08 R6-0-2(가) — 글자 수가 아니라 <b>실제 폭</b>으로 자른다.
                //   내용이 실제로 바뀐 순간에만 부른다: UiChrome.Ellipsize는 폭을 재려고
                //   Text.text를 log2(길이)번 바꾸므로(그 함수의 «호출부 규약») 상주 앱의
                //   갱신 경로에서 매번 부르면 잘라 낸 문자열이 계속 새로 할당된다.
                bool labelChanged = row.LastCompleted != item.Completed
                    || !string.Equals(row.LastLabelSource, item.Text, System.StringComparison.Ordinal);
                if (labelChanged)
                {
                    row.LastLabelSource = item.Text;
                    row.LastCompleted = item.Completed;
                    // 예산은 «설계 상자»와 «지금 실제 상자» 중 <b>작은 쪽</b>이다. 화면이 좁아
                    // 패널이 줄면 상자도 함께 줄어드는데, 자르는 기준만 설계값에 남으면 글자가
                    // 다시 [✕] 위로 올라탄다(이 저장소가 폭 1042 때 당한 형태 그대로다).
                    float box = row.Label.rectTransform.rect.width;
                    row.Label.text = UiChrome.Ellipsize(row.Label, item.Text,
                        box > 1f ? Mathf.Min(box, RowLabelWidth) : RowLabelWidth);
                    ApplyStrikethrough(row);
                }

                // ★ 알파로 흐리지 않는다 — 위계는 <b>색 토큰</b>이 진다(클래스 문서 "알파" 절).
                row.Label.color = item.Completed ? UiChrome.TextTertiary : UiChrome.TextPrimary;
                if (row.BoxCheck.gameObject.activeSelf != item.Completed)
                    row.BoxCheck.gameObject.SetActive(item.Completed);

                bool deletable = !archive;
                if (row.DeleteButton.gameObject.activeSelf != deletable)
                    row.DeleteButton.gameObject.SetActive(deletable);

                bool confirming = item.Id == _confirmingId;
                if (row.Confirm.gameObject.activeSelf != confirming) row.Confirm.gameObject.SetActive(confirming);
            }

            // ---- 완료 구분선 ----
            bool showDivider = dividerSlot >= 0;
            if (_divider.gameObject.activeSelf != showDivider) _divider.gameObject.SetActive(showDivider);
            if (showDivider)
            {
                // ★ anchoredPosition으로 옮기지 않는다 — 이 상자는 가로가 <b>스트레치</b>라
                //   그 값이 좌우 여백까지 함께 밀어 버린다. 같은 배치 함수로 다시 눕힌다.
                PlaceTopStretch(_divider, 0f, RowRightInset,
                    ListTop - dividerSlot * (RowHeight + RowGap), RowHeight);
                _dividerCaption.text = $"완료 {_slots[_scroll + dividerSlot].CompletedCount}건";
            }

            // ---- 날짜 네비 ----
            bool axis = HasDayAxis;
            _dateToggleLabel.text = axis ? FormatDayLabel(_selectedDay) : "오늘";
            SetControlEnabled(_prevDay, axis && _selectedDay > MinSelectableDay);
            SetControlEnabled(_nextDay, axis && _selectedDay < MaxSelectableDay);
            SetChipSelected(_todayChip, axis && _selectedDay == today);
            SetChipSelected(_tomorrowChip, axis && _selectedDay == today + 1);
            SetInteractable(_dateToggle, axis);
            _dateToggleLabel.color = axis ? UiChrome.TextPrimary : UiChrome.DisabledControlInk;

            // ---- 입력 정의역 ----
            bool writable = !archive && (!axis || IsWritableDay(_selectedDay, today));
            _inputPlaceholder.text = archive ? "완료한 일은 여기서 적을 수 없어요"
                : ResolveInputPlaceholder(_selectedDay, today);
            if (_input.interactable != writable) _input.interactable = writable;
            SetInteractable(_addSurface, writable);
            _addSurface.color = writable ? UiChrome.Accent : UiChrome.CardSurfaceMuted;
            _addLabel.color = writable ? UiChrome.OnAccentSolid : UiChrome.DisabledControlInk;

            // ---- 빈 상태 ----
            bool empty = _slots.Count == 0;
            if (_emptyTitle.gameObject.activeSelf != empty) _emptyTitle.gameObject.SetActive(empty);
            if (_emptyBody.gameObject.activeSelf != empty) _emptyBody.gameObject.SetActive(empty);
            if (empty)
            {
                _emptyTitle.text = ResolveEmptyTitle(_selectedDay, today, archive);
                _emptyBody.text = ResolveEmptyBody(_selectedDay, today, archive);
                _emptyBody.gameObject.SetActive(!string.IsNullOrEmpty(_emptyBody.text));
            }

            bool needRail = _slots.Count > _visibleRowBudget;
            if (_railUp.gameObject.activeSelf != needRail) _railUp.gameObject.SetActive(needRail);
            if (_railDown.gameObject.activeSelf != needRail) _railDown.gameObject.SetActive(needRail);

            _footer.text = ResolveDailyFooter(today, archive);
            _footer.color = _softCapHit && !archive ? UiChrome.WarmAccent : UiChrome.TextTertiary;
        }

        /// <summary>푸터 한 줄 — 사실이 여럿 몰릴 때의 <b>우선순위가 고정</b>이다(R6-8).
        /// 소프트캡 경고가 그날 요약을 이긴다(정리해야 할 상태가 통계보다 급하다).</summary>
        private string ResolveDailyFooter(int today, bool archive)
        {
            if (archive) return "완료한 일은 지우지 않고 모아둬요.";
            if (_softCapHit) return "할일이 많아요. 먼저 정리해볼까요?";
            if (!HasDayAxis) return $"완료함 {TodoListModel.CompletedArchive.Count}건 · 앱을 껐다 켜도 남아요";

            TodoListModel.DayTally tally = TodoListModel.TallyForDay(_selectedDay);
            System.DateTime d = DateOfDayIndex(_selectedDay);
            return $"{d.Month}월 {d.Day}일 · 완료 {tally.Completed}건 · 남은 {tally.Remaining}건";
        }

        /// <summary>빈 상태 제목 — R6-8의 4문 그대로다. <b>«아무것도 안 했어요»는 금지</b>(17절 추궁 금지).</summary>
        public static string ResolveEmptyTitle(int dayIndex, int todayIndex, bool archive)
        {
            if (archive) return "완료한 일이 아직 없어요";
            if (todayIndex == TodoItem.UnknownPlannedDay) return "아직 비어 있어요";
            if (dayIndex < todayIndex) return "이 날은 적어둔 게 없어요";
            if (dayIndex > todayIndex) return "내일 할 일을 미리 적어둘 수 있어요";
            return "오늘은 아직 비어 있어요";
        }

        /// <inheritdoc cref="ResolveEmptyTitle"/>
        public static string ResolveEmptyBody(int dayIndex, int todayIndex, bool archive)
        {
            if (archive) return "체크한 일이 여기 모입니다.";
            if (todayIndex == TodoItem.UnknownPlannedDay || dayIndex == todayIndex)
                return "위에 적어두면 제가 가끔 챙겨줄게요.";
            return string.Empty;
        }

        private void RefreshCalendar(int today)
        {
            if (_calendarAnchorDay == TodoItem.UnknownPlannedDay) _calendarAnchorDay = _selectedDay;

            System.DateTime anchor = DateOfDayIndex(_calendarAnchorDay);
            int firstOfMonth = _calendarAnchorDay - (anchor.Day - 1);
            int lead = (int)DateOfDayIndex(firstOfMonth).DayOfWeek;
            int cell0 = firstOfMonth - lead;

            int minDay = MinSelectableDay;
            int maxDay = MaxSelectableDay;
            int monthCompleted = 0;
            int monthRemaining = 0;

            for (int i = 0; i < CalendarCellCount; i++)
            {
                CellView cell = _cells[i];
                int dayIndex = cell0 + i;
                System.DateTime d = DateOfDayIndex(dayIndex);
                bool inMonth = d.Month == anchor.Month && d.Year == anchor.Year;
                bool inDomain = HasDayAxis && dayIndex >= minDay && dayIndex <= maxDay;

                cell.DayIndex = dayIndex;
                cell.Selectable = inMonth && inDomain;
                cell.DayNumber.text = d.Day.ToString();

                // 정의역 밖(그리고 이웃 달의 흘러넘친 칸)은 <b>흐림 + 클릭 불가</b>다(R6-8 #6).
                cell.DayNumber.color = cell.Selectable ? UiChrome.TextPrimary : UiChrome.DisabledControlInk;

                bool selected = cell.Selectable && dayIndex == _selectedDay;
                bool isToday = cell.Selectable && dayIndex == today;
                cell.Surface.color = selected
                    ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.PanelSurface)
                    : UiChrome.CardSurface;
                cell.Outline.color = isToday
                    ? UiChrome.Flatten(UiChrome.AccentBorder, UiChrome.CardSurface)
                    : UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface);

                TodoListModel.DayTally tally = cell.Selectable
                    ? TodoListModel.TallyForDay(dayIndex)
                    : default;
                if (inMonth)
                {
                    monthCompleted += tally.Completed;
                    monthRemaining += tally.Remaining;
                }

                // ★ 미래에는 막대를 그리지 않는다 — 빈 막대는 «실패한 날»로 읽힌다.
                //   미래는 평가 대상이 아니다(R6-5-3).
                bool showBar = tally.HasAny && dayIndex <= today;
                if (cell.BarTrack.gameObject.activeSelf != showBar) cell.BarTrack.gameObject.SetActive(showBar);
                if (showBar)
                {
                    float ratio = tally.Total > 0 ? (float)tally.Completed / tally.Total : 0f;
                    cell.BarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
                }

                bool showDot = tally.Remaining > 0;
                if (cell.Dot.gameObject.activeSelf != showDot) cell.Dot.gameObject.SetActive(showDot);
            }

            _monthLabel.text = FormatMonthLabel(_calendarAnchorDay);
            _calendarFooter.text = $"{anchor.Month}월 · 완료 {monthCompleted}건 · 남은 {monthRemaining}건";

            int daysInMonth = System.DateTime.DaysInMonth(anchor.Year, anchor.Month);
            SetControlEnabled(_prevMonth, HasDayAxis && firstOfMonth > minDay);
            SetControlEnabled(_nextMonth, HasDayAxis && firstOfMonth + daysInMonth - 1 < maxDay);
        }

        /// <summary>버튼만 껐다 켠다(색은 건드리지 않는다).</summary>
        private static void SetInteractable(Image surface, bool on)
        {
            if (surface == null) return;
            var button = surface.GetComponent<Button>();
            if (button != null && button.interactable != on) button.interactable = on;
        }

        /// <summary>정의역 끝에 닿은 화살표 — 끄고 <b>흐리게</b> 만든다. 흐림은 색 토큰이 진다
        /// (알파를 쓰지 않는다 — 클래스 문서 "알파" 절).</summary>
        private static void SetControlEnabled(Image surface, bool on)
        {
            if (surface == null) return;
            SetInteractable(surface, on);
            Text glyph = surface.GetComponentInChildren<Text>(true);
            if (glyph != null) glyph.color = on ? UiChrome.TextSecondary : UiChrome.DisabledControlInk;
        }

        private static void SetChipSelected(Image chip, bool on)
        {
            if (chip == null) return;
            chip.color = on ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.PanelSurface) : UiChrome.CardSurface;
            Text label = chip.GetComponentInChildren<Text>(true);
            if (label != null) label.color = on ? UiChrome.TextOnAccent : UiChrome.TextSecondary;
        }

        /// <summary>완료 항목에 취소선을 긋는다. 레거시 uGUI <see cref="Text"/>에는
        /// <c>&lt;s&gt;</c>가 없어서(리치텍스트는 b/i/size/color뿐) 글자 폭을 재서 1pt 선을 직접 놓는다.
        ///
        /// <para>★ 2026-09-08 — <see cref="TodoPostItWidget"/>의 같은 이름 메서드가 <b>정본</b>이고
        /// 이것은 그 이식본이다. 여기 있던 U+0336 결합문자 조립은 <b>폰트 폴백에 기대는 방식</b>이라
        /// Windows에서 두부가 될지 미확인이었다(docs/UX_WIDGETS.md R6-0-2(나)·R6-11 #3).
        /// R6에서 완료 행이 그 날짜에 <b>영구히</b> 남게 되면 그 위험이 «2.5초»에서 «영구»로 승격된다.</para>
        ///
        /// <para>폭 측정은 <see cref="UiChrome.Ellipsize"/> 직후, 즉 <b>내용이 바뀐 순간에만</b> 한다.
        /// 상자 폭으로 한 번 더 클램프하는 것은 방어다 — 말줄임이 어떤 이유로 통과되더라도
        /// 선만은 상자를 넘지 않는다.</para></summary>
        private static void ApplyStrikethrough(RowView row)
        {
            if (row.Strike == null) return;

            bool on = row.LastCompleted;
            if (row.Strike.gameObject.activeSelf != on) row.Strike.gameObject.SetActive(on);
            if (!on) return;

            float width = Mathf.Min(row.Label.preferredWidth, row.Label.rectTransform.rect.width);
            row.Strike.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width), 1f);
        }

        // ==================== 배치 도구 ====================

        /// <summary>부모의 <b>아래쪽에 가로로 걸친</b> 상자 — 패널이 줄면 따라 올라온다.</summary>
        private static void PlaceBottomStretch(RectTransform rt, float left, float right,
            float bottom, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + height);
        }

        /// <summary>uGUI 버튼 배선 + <b>프레임 소유권</b>. 같은 프레임에 다른 표면이 이미 그 클릭을
        /// 먹었으면 여기서 멈춘다(<see cref="UiClickArbiter"/>).
        /// <para>uGUI 경로에는 좌표 판정을 걸지 않는다 — <c>GraphicRaycaster</c>가 캔버스
        /// <c>sortingOrder</c>로 이미 z-순서를 풀었기 때문이다. 여기서 필요한 것은 시간 축뿐이다.</para></summary>
        private void WireGuarded(Image surface, string actionKey, System.Action action)
            => Wire(surface, actionKey, () =>
            {
                if (UiClickArbiter.TryClaimFrame(ClickArbiterSurfaceId)) action();
            });

        // ==================== 전역 폴링 경로 ====================

        protected override void OnGlobalClick(Vector2 cursor)
        {
            // ★ 이 클릭은 이 패널 사각형 안이다(기반 클래스가 이미 걸렀다). 그러니 <b>어디에 떨어졌든</b>
            //   이 창의 것이고, 여기서 프레임을 잡아 포스트잇이 같은 클릭을 함께 먹지 못하게 한다.
            //   빈자리에 떨어진 클릭도 잡는다 — 「내 창 위인데 아래 카드가 반응했다」가 신고 원문이다.
            if (!UiClickArbiter.TryClaimClick(ClickArbiterSurfaceId, ClickArbiterLayer, cursor)) return;

            for (int i = 0; i < 2; i++)
            {
                if (!ContainsScreenPoint(_tabChips[i].rectTransform, cursor)) continue;
                if (TryClaimAction("tab" + i)) SelectTab((Tab)i);
                return;
            }

            if (_tab == Tab.Active && _page == Page.Calendar) { OnCalendarClick(cursor); return; }
            OnDailyClick(cursor);
        }

        private void OnDailyClick(Vector2 cursor)
        {
            if (HitControl(_prevDay, cursor, "prevDay", () => StepDay(-1))) return;
            if (HitControl(_nextDay, cursor, "nextDay", () => StepDay(1))) return;
            if (HitControl(_dateToggle, cursor, "dateToggle", () => SelectPage(Page.Calendar))) return;
            if (HitControl(_todayChip, cursor, "todayChip", JumpToToday)) return;
            if (HitControl(_tomorrowChip, cursor, "tomorrowChip", JumpToTomorrow)) return;
            if (HitControl(_addSurface, cursor, "add", AddFromInput)) return;
            if (HitControl(_railUp, cursor, "RailUp", () => Scroll(-1))) return;
            if (HitControl(_railDown, cursor, "RailDown", () => Scroll(1))) return;

            for (int i = 0; i < VisibleRows; i++)
            {
                RowView row = _rows[i];
                if (row.BoundId < 0 || !ContainsScreenPoint(row.Rect, cursor)) continue;

                if (row.Confirm.gameObject.activeSelf)
                {
                    if (ContainsScreenPoint(row.ConfirmYes.rectTransform, cursor))
                    {
                        if (TryClaimAction("rowYes" + i)) OnConfirmDelete(i);
                        return;
                    }
                    if (ContainsScreenPoint(row.ConfirmNo.rectTransform, cursor))
                    {
                        if (TryClaimAction("rowNo" + i)) CancelDeleteConfirm();
                    }
                    return;
                }

                if (row.DeleteButton.gameObject.activeSelf && ContainsScreenPoint(row.DeleteButton.rectTransform, cursor))
                {
                    if (TryClaimAction("rowDelete" + i)) OnDeleteClicked(i);
                    return;
                }
                if (TryClaimAction("row" + i)) OnRowClicked(i);
                return;
            }
        }

        private void OnCalendarClick(Vector2 cursor)
        {
            if (HitControl(_prevMonth, cursor, "prevMonth", () => StepMonth(-1))) return;
            if (HitControl(_nextMonth, cursor, "nextMonth", () => StepMonth(1))) return;
            if (HitControl(_monthToggle, cursor, "monthToggle", () => SelectPage(Page.Daily))) return;

            for (int i = 0; i < CalendarCellCount; i++)
            {
                CellView cell = _cells[i];
                if (cell == null || !cell.Selectable) continue;
                if (!ContainsScreenPoint(cell.Rect, cursor)) continue;
                int index = i;
                if (TryClaimAction("cell" + i)) OnCellClicked(index);
                return;
            }
        }

        /// <summary>«이 컨트롤 위인가 → 맞으면 중복 제거를 거쳐 실행»을 한 줄로. 꺼져 있는(비활성)
        /// 컨트롤은 <see cref="ContainsScreenPoint"/>가 <c>activeInHierarchy</c>로 걸러 준다.</summary>
        private bool HitControl(Image surface, Vector2 cursor, string actionKey, System.Action action)
        {
            if (surface == null || !ContainsScreenPoint(surface.rectTransform, cursor)) return false;
            var button = surface.GetComponent<Button>();
            if (button != null && !button.interactable) return true;   // 눌러도 아무 일도 없다(먹기는 한다).
            if (TryClaimAction(actionKey)) action();
            return true;
        }

        // ==================== 테스트 진입점 ====================

        /// <summary>테스트/진단 전용 — 입력칸에 글자를 넣고 [추가]와 같은 경로로 확정한다.</summary>
        public void AddForTests(string text)
        {
            if (_input != null) _input.text = text;
            AddFromInput();
        }

        /// <summary>화면에 실제로 보이는 <b>항목 행</b>의 개수(빈 행·구분선 제외).</summary>
        public int VisibleRowCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < VisibleRows; i++)
                {
                    if (_rows[i] != null && _rows[i].Rect.gameObject.activeSelf) n++;
                }
                return n;
            }
        }

        /// <summary>지금 화면 크기에서 <b>실제로 쓸 수 있는</b> 행 수(설계 상수가 아니다).</summary>
        public int VisibleRowBudgetForTests => _visibleRowBudget;

        /// <summary>설계 상 행 상한(10). 테스트가 숫자를 베끼지 않게 한다.</summary>
        public static int DesignVisibleRows => VisibleRows;

        /// <summary>달력 칸 수(6×7). 같은 사정으로 public이다.</summary>
        public static int CalendarCellCountForTests => CalendarCellCount;

        /// <summary>지금 보고 있는 날짜(에폭 이후 일수).</summary>
        public int SelectedDayIndexForTests => _selectedDay;

        /// <summary>선택이 «오늘»에 붙어 있는가(자정에 따라가는가).</summary>
        public bool FollowsTodayForTests => _followToday;

        /// <summary>이 창이 아는 «오늘» — <see cref="CurrencyModel.DayIndex"/> 그대로다(두 벌이 아니다).</summary>
        public int TodayIndexForTests => TodayIndex;

        /// <summary>Content 상자의 <b>실측</b> 높이(pt). <see cref="ContentHeightPoints"/>와 대조해
        /// 기반 클래스 크롬이 바뀐 것을 잡는다.</summary>
        public float ContentHeightForTests => _content != null ? _content.rect.height : 0f;

        /// <summary>이 팝오버 캔버스의 <c>sortingOrder</c> — <b>이름으로 찾지 않는다</b>.
        /// <see cref="UiClickArbiter"/>의 층 모형이 실제 z-순서와 같은지 대조하는 창구다.</summary>
        public int CanvasSortingOrderForTests
        {
            get
            {
                Canvas canvas = Panel != null ? Panel.GetComponentInParent<Canvas>() : null;
                return canvas != null ? canvas.sortingOrder : 0;
            }
        }

        /// <summary>i번째로 보이는 행이 물고 있는 항목 Id(-1 = 없음).</summary>
        public int RowItemId(int index)
            => index >= 0 && index < VisibleRows && _rows[index] != null ? _rows[index].BoundId : -1;

        /// <summary>i번째 행 사각형(Unity 스크린 픽셀) — 테스트가 실제 클릭 경로로 누른다.</summary>
        public Rect RowScreenRect(int index)
            => index >= 0 && index < VisibleRows && _rows[index] != null
                ? ScreenRectOf(_rows[index].Rect)
                : new Rect();

        /// <summary>i번째 행의 삭제 [✕] 사각형.</summary>
        public Rect RowDeleteScreenRect(int index)
            => index >= 0 && index < VisibleRows && _rows[index] != null
                ? ScreenRectOf(_rows[index].DeleteButton.rectTransform)
                : new Rect();

        /// <summary>삭제 확인의 [삭제] 사각형.</summary>
        public Rect RowConfirmYesScreenRect(int index)
            => index >= 0 && index < VisibleRows && _rows[index] != null
                ? ScreenRectOf(_rows[index].ConfirmYes.rectTransform)
                : new Rect();

        /// <summary>[추가] 버튼 사각형 — 포스트잇과의 클릭 충돌을 재현하는 테스트가 여기를 누른다.</summary>
        public Rect AddButtonScreenRect => ScreenRectOf(_addSurface != null ? _addSurface.rectTransform : null);

        /// <summary>날짜 라벨(= 달력 토글) 사각형.</summary>
        public Rect DateToggleScreenRect => ScreenRectOf(_dateToggle != null ? _dateToggle.rectTransform : null);

        /// <summary>[‹] / [›] 사각형.</summary>
        public Rect PrevDayScreenRect => ScreenRectOf(_prevDay != null ? _prevDay.rectTransform : null);

        /// <inheritdoc cref="PrevDayScreenRect"/>
        public Rect NextDayScreenRect => ScreenRectOf(_nextDay != null ? _nextDay.rectTransform : null);

        /// <summary>[오늘] / [내일] 칩 사각형.</summary>
        public Rect TodayChipScreenRect => ScreenRectOf(_todayChip != null ? _todayChip.rectTransform : null);

        /// <inheritdoc cref="TodayChipScreenRect"/>
        public Rect TomorrowChipScreenRect => ScreenRectOf(_tomorrowChip != null ? _tomorrowChip.rectTransform : null);

        /// <summary>i번째 탭 칩 사각형 — 칩이 창 밖으로 나가지 않는지 재는 창구.</summary>
        public Rect TabChipScreenRect(int index)
            => index >= 0 && index < 2 && _tabChips[index] != null
                ? ScreenRectOf(_tabChips[index].rectTransform)
                : new Rect();

        /// <summary>i번째 달력 칸 사각형.</summary>
        public Rect CalendarCellScreenRect(int index)
            => index >= 0 && index < CalendarCellCount && _cells[index] != null
                ? ScreenRectOf(_cells[index].Rect)
                : new Rect();

        /// <summary>i번째 달력 칸이 가리키는 일자 번호.</summary>
        public int CalendarCellDayIndex(int index)
            => index >= 0 && index < CalendarCellCount && _cells[index] != null
                ? _cells[index].DayIndex
                : TodoItem.UnknownPlannedDay;

        /// <summary>i번째 달력 칸을 누를 수 있는가(정의역 안 · 이 달).</summary>
        public bool CalendarCellSelectable(int index)
            => index >= 0 && index < CalendarCellCount && _cells[index] != null && _cells[index].Selectable;

        /// <summary>i번째 달력 칸의 진행 막대가 켜져 있는가.</summary>
        public bool CalendarCellBarActive(int index)
            => index >= 0 && index < CalendarCellCount && _cells[index] != null
               && _cells[index].BarTrack.gameObject.activeSelf;

        /// <summary>i번째 달력 칸의 «남은 것» 점이 켜져 있는가.</summary>
        public bool CalendarCellDotActive(int index)
            => index >= 0 && index < CalendarCellCount && _cells[index] != null
               && _cells[index].Dot.gameObject.activeSelf;

        /// <summary>완료 구분선이 켜져 있는가.</summary>
        public bool CompletedDividerActive => _divider != null && _divider.gameObject.activeSelf;

        /// <summary>완료 구분선에 적힌 글자.</summary>
        public string CompletedDividerText => _dividerCaption != null ? _dividerCaption.text : null;

        /// <summary>입력칸 안내 문구(지금 화면에 실제로 그려진 것).</summary>
        public string InputPlaceholderText => _inputPlaceholder != null ? _inputPlaceholder.text : null;

        /// <summary>[추가]를 지금 누를 수 있는가(정의역 밖 날짜에서는 꺼진다).</summary>
        public bool AddButtonEnabledForTests
        {
            get
            {
                Button button = _addSurface != null ? _addSurface.GetComponent<Button>() : null;
                return button != null && button.interactable;
            }
        }

        /// <summary>입력칸이 지금 글자를 받는가.</summary>
        public bool InputEnabledForTests => _input != null && _input.interactable;

        /// <summary>푸터에 실제로 그려진 글자.</summary>
        public string FooterText => _footer != null ? _footer.text : null;

        /// <summary>달력 페이지 푸터 글자.</summary>
        public string CalendarFooterText => _calendarFooter != null ? _calendarFooter.text : null;

        /// <summary>날짜 라벨에 실제로 그려진 글자.</summary>
        public string DateToggleText => _dateToggleLabel != null ? _dateToggleLabel.text : null;

        /// <summary>달 라벨에 실제로 그려진 글자.</summary>
        public string MonthLabelText => _monthLabel != null ? _monthLabel.text : null;

        private RowView RowOrNull(int index)
            => index >= 0 && index < VisibleRows ? _rows[index] : null;

        /// <summary>i번째 행에 <b>실제로 그려진</b> 라벨 문자열(말줄임 적용 후) — 테스트/진단 전용.
        /// 원본이 아니라 화면에 있는 것을 돌려준다: 둘이 갈라지는 것이 이 결함의 본체였다.</summary>
        public string RowLabelText(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Label != null ? row.Label.text : null;
        }

        /// <summary>i번째 행 라벨에 실제로 칠해진 색 — <b>알파가 1인지</b>를 테스트가 직접 잰다.</summary>
        public Color RowLabelColor(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Label != null ? row.Label.color : Color.clear;
        }

        /// <summary>i번째 행 라벨의 <b>실측 잉크 폭</b>(pt). 폰트가 직접 잰 값이다
        /// (<see cref="Text.preferredWidth"/>) — 글자 수 모형이 아니다.</summary>
        public float RowLabelInkWidth(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Label != null ? row.Label.preferredWidth : 0f;
        }

        /// <summary>i번째 행 라벨 <b>상자</b>의 화면 사각형.</summary>
        public Rect RowLabelScreenRect(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Label != null ? ScreenRectOf(row.Label.rectTransform) : new Rect();
        }

        /// <summary>i번째 행의 취소선이 켜져 있는가.</summary>
        public bool RowStrikeActive(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Strike != null && row.Strike.gameObject.activeSelf;
        }

        /// <summary>i번째 행 취소선의 <b>로컬</b> 크기(pt) — 폭·두께를 그대로 잰다.</summary>
        public Vector2 RowStrikeSizePoints(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Strike != null ? row.Strike.rectTransform.sizeDelta : Vector2.zero;
        }

        /// <summary>i번째 행 취소선의 화면 사각형.</summary>
        public Rect RowStrikeScreenRect(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Strike != null ? ScreenRectOf(row.Strike.rectTransform) : new Rect();
        }
    }
}
