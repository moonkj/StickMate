using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 캐릭터 창(장비 / 외형 / 보관함) — 2026-08-30 <b>외부 디자인 핸드오프 이식 라운드</b>.
    /// 설계 확정본은 docs/UX_FLOW.md <b>33-7절</b>이고, 이 파일은 그 좌표표를 그대로 옮긴 것이다.
    /// 좌표/색/글자 크기를 여기서 새로 고르지 않는다 — 고치고 싶으면 33-7을 먼저 고친다.
    ///
    /// ============================================================================
    /// 골격 (880 × 861, 화면 중앙 모달)
    /// ============================================================================
    /// 타이틀바 40 + 본문 821. 본문은 좌측 244(상시 노출) + 우측 636(탭 3개).
    ///  · 좌측: 이름(+잉크색 스와치·인라인 편집) / 초상화 / 프레즌스 / 게이지 2종 / 스탯 5행.
    ///  · 우측: 탭바 → 카테고리 섹션 4개(각각 <b>가로 카드 캐러셀</b>) → 선택 상세.
    /// 옛 [정보] 탭은 좌측 컬럼으로 <b>흡수</b>됐다(탭이 아니라 항상 보인다).
    ///
    /// ============================================================================
    /// 왜 우상단 앵커가 아니라 화면 중앙인가 / 왜 배경 딤을 깔지 않는가 (33-7-7)
    /// ============================================================================
    /// 880×861은 톱니 아래(top 84)에서 시작하면 84+861=945pt라 어떤 노트북에도 들어가지 않는다.
    /// 그래서 중앙 정렬로 바꿨다. <b>2026-08-30 보강</b>: "중앙 <b>고정</b>"이던 부분만 리더가 뒤집었다 —
    /// 열릴 때는 여전히 화면 중앙이지만 <b>타이틀바를 잡으면 옮길 수 있다</b>(화면 밖으로는 못 나간다).
    /// 옮긴 자리는 기억하지 않는다. 반대로 스펙의 배경색 <c>#dcdbd7</c>(<see cref="UiChrome.ScreenScrim"/>)은
    /// <b>깔지 않는다</b> — 그건 브라우저 프로토타입의 "지면"이지 모달 딤이 아니고, 우리가 화면 전체를
    /// 덮으면 유저의 작업 화면을 통째로 가려 <b>비침해 원칙 2 정면 위반</b>이 된다.
    ///
    /// ============================================================================
    /// 왜 타이틀바에 "ESC"라고 적지 않고 [✕]를 두는가
    /// ============================================================================
    /// 스펙은 우측 상단에 <c>ESC</c> 힌트를 두고 그 키로 닫는다. 그런데 이 프로젝트에서
    /// <see cref="KeyCode.Escape"/>는 이미 <b>클릭관통 긴급 해제</b>(Core/StickmanAgent)에 묶여 있다.
    /// 창 닫기를 같은 키에 겹치면 창을 닫을 때마다 <b>보이지 않는 부수효과</b>로 클릭관통이 꺼져
    /// 화면 전체의 클릭을 우리가 먹기 시작한다(원칙 2 직결). 그래서 그 자리에 [✕] 버튼을 둔다 —
    /// 있지도 않은 동작을 힌트로 <b>주장하지 않는</b> 쪽이 이 프로젝트의 문구 원칙이기도 하다.
    ///
    /// ============================================================================
    /// 카드는 <b>가로로 미는 캐러셀</b>이다 (2026-09-01)
    /// ============================================================================
    /// 카테고리당 카드가 4장으로 고정이던 시절에는 격자 배치였다. 아이템이 늘면서 그 전제가 깨졌고,
    /// 지금은 카테고리마다 <b>개수가 다르다</b>. 그래서 한 카테고리 = <see cref="ScrollRect"/> 한 줄이고,
    /// 배치는 <see cref="HorizontalLayoutGroup"/>, 폭은 <see cref="ContentSizeFitter"/>, 잘라내기는
    /// <see cref="RectMask2D"/>가 한다 — 포인터 처리를 새로 짜지 않는다.
    /// <b>다만</b> 이 창의 실제 클릭 경로는 uGUI가 아니라 전역 폴링이므로(아래 문단) 드래그도 폴링판이
    /// 한 벌 더 있다. 두 경로가 <b>절대값 공식</b>을 쓰므로 동시에 돌아도 더해지지 않는다.
    ///
    /// 그리고 착용/해제는 이제 <b>카드 하단 버튼</b>이 한다(사용자 요청). 카드 본체 클릭은 여전히
    /// "고르기"뿐이다 — 캐러셀을 밀다가 옷이 갈아입혀지는 사고를 구조적으로 없앤다.
    ///
    /// ============================================================================
    /// 아이콘은 데이터, 그리기는 여기
    /// ============================================================================
    /// 40×40 썸네일 도형은 <see cref="ItemIconPart"/>(Core/ItemCatalog.cs)에 <b>SVG viewBox 좌표
    /// 그대로</b> 들어 있고, 이 파일이 화면 좌표로 뒤집어 <see cref="UiChrome.AddPolyline"/>로 그린다.
    /// 탭을 바꿔도 아이콘을 다시 굽지 않는다 — 카드 하나가 [장비]용/[외형]용 아이콘 <b>두 벌</b>을
    /// 미리 갖고 있고 켜고 끄기만 한다(탭 전환 때마다 300개 넘는 GameObject를 다시 만들지 않으려고).
    ///
    /// ============================================================================
    /// 초상화 = 전용 미니 피규어의 실시간 촬영 (신규 SVG 금지 — 33-7-6)
    /// ============================================================================
    /// Interaction/CharacterPortraitStage.cs가 찍은 RenderTexture를 <see cref="RawImage"/>로 붙이기만
    /// 한다. 액자만 스펙 값(204×196 / 여백 8 / 반지름 8)으로 바꿨고, 그 결과 <see cref="PortraitContentSize"/>
    /// 에서 파생되는 카메라 종횡비가 <b>0.710 → 1.044</b>로 함께 바뀐다(교차 레이어 영향 — 보고 완료).
    ///
    /// ============================================================================
    /// 클릭 판정 / 매 프레임 할당 금지 (기존 관례 그대로)
    /// ============================================================================
    /// (1) uGUI Button + 자체 EventSystem 보강, (2) 창 사각형을 덮는 isTrigger BoxCollider2D,
    /// (3) 전역 폴링 히트테스트. 셋이 같은 핸들러를 부르고 <see cref="ActionDedupSeconds"/>로 중복을
    /// 막는다. <b>창이 닫히면 차단막은 반드시 꺼진다</b>. 닫혀 있으면 Update()는 첫 줄에서 돌아가고,
    /// 열려 있어도 문자열은 상태가 바뀐 프레임과 <see cref="SlowRefreshInterval"/> 주기에만 만든다.
    /// 히트테스트가 쓰는 코너 배열은 <see cref="_corners"/> 하나를 돌려쓴다(폴링 경로 할당 0).
    /// </summary>
    public sealed class CharacterInfoWindow : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        // ★ 2026-08-30: 31000 -> 31900. 이 창은 모달인데 부채꼴(31500)/팝오버(31700)보다 <b>아래</b>에
        // 깔려 있었다(디버거 실측) — 값 자체가 "모달"이라는 성격과 모순이었다. 말풍선(31000)과도 값이
        // 같아 Unity가 그리기 순서를 보장하지 않았다(동률 오버레이 캔버스는 생성 순서에 의존).
        private const int SortingOrderTopMost = 31900; // 팝오버(31700) 위, 앱 제어 메뉴(32760) 아래.

        // ==================== 33-7-2 확정 치수 (캔버스 유닛 == OS 포인트) ====================

        private const float PanelWidth = 880f;
        private const float PanelHeight = 861f;
        private const float TitleHeight = 40f;
        private const float BodyHeight = PanelHeight - TitleHeight;   // 821
        private const float ScreenMargin = 16f;

        // ---- 좌측 컬럼 ----
        private const float LeftWidth = 244f;
        private const float LeftPadX = 20f;
        private const float LeftContentWidth = LeftWidth - LeftPadX * 2f;   // 204
        private const float NameY = -22f;
        private const float SubY = -50f;
        private const float PortraitY = -83f;
        private const float PortraitHeight = 196f;
        private const float PortraitPadding = 8f;
        private const float PresenceY = -297f;
        private const float StressLabelY = -330f;
        private const float StressTrackY = -348f;
        private const float XpLabelY = -364f;
        private const float XpTrackY = -382f;
        private const float TrackHeight = 4f;
        private const float StatsTopY = -404f;
        private const float StatsFirstRowY = -408f;
        private const float StatRowStep = 32f;
        private const float StatRowHeight = 31f;
        private const float SwatchSize = 12f;
        private const float SwatchGap = 8f;

        // ---- 우측 컬럼 ----
        private const float RightX = LeftWidth;                        // 244
        private const float RightWidth = PanelWidth - LeftWidth;       // 636
        private const float RightPadX = 22f;
        private const float RightContentWidth = RightWidth - RightPadX * 2f; // 592
        private const float TabStripY = -22f;
        private const float TabStripHeight = 32f;
        private const float TabGap = 22f;
        private const float TabUnderlineHeight = 2f;
        private const float SectionsTopY = -72f;
        private const float SectionStep = 156f;
        private const float SectionHeight = 136f;
        private const float DetailY = -696f;
        private const float DetailHeight = 103f;

        // ---- 카드 ----
        private const float CardWidth = 141f;
        private const float CardStep = 150f;

        /// <summary>카드 사이 간격 — <see cref="HorizontalLayoutGroup.spacing"/>에 그대로 들어간다.
        /// 상수를 새로 적지 않고 <b>기존 두 값에서 뺀다</b>: 그래야 카드 폭을 고쳤을 때 간격만
        /// 옛 값으로 남는 일이 없다(격자 시절의 CardStep 리듬이 캐러셀에서도 그대로 유지된다).</summary>
        private const float CardGap = CardStep - CardWidth;   // 9

        /// <summary>캐러셀 뷰포트에 <b>온전히</b> 들어오는 카드 수. 나머지 한 장은 일부러 걸치게 둔다.</summary>
        private const int CarouselFullCards = 3;

        /// <summary>걸치는 카드가 보이는 비율. 너무 작으면 "가장자리 그림자"로 보이고, 0.8을 넘으면
        /// 온전한 카드처럼 보여 다시 "이게 전부"가 된다 — 반쯤이 가장 분명하게 <b>잘렸다</b>고 읽힌다.</summary>
        private const float CarouselPeekFraction = 0.5f;

        /// <summary>
        /// ★ 2026-09-01 — 캐러셀 뷰포트 폭. 섹션 폭(<see cref="RightContentWidth"/> 592)보다 <b>일부러 좁다</b>.
        ///
        /// <para>592는 <see cref="CardStep"/> 150짜리 카드가 <b>정확히 4장</b> 들어가고(0/150/300/450 →
        /// 오른쪽 끝 591) 1pt가 남는 폭이었다. 카테고리마다 6종이 있어도 화면에는 4장이 <b>딱 맞게</b>
        /// 놓이므로 "모자는 4개구나"로 확정되고, 5·6번째는 통째로 밖에 있어 발견되지 않는다
        /// (페르소나 M1 — 콘텐츠의 1/3이 도달 불가). 스크롤은 원래 됐다. 없었던 것은 <b>단서</b>다.</para>
        ///
        /// <para>그래서 카드 폭·간격·개수는 한 줄도 건드리지 않고 <b>창문만 좁힌다</b>: 마지막 카드가
        /// 반쯤 잘린 채 남아 "오른쪽에 더 있다"가 그림 하나로 전달된다. 화살표 칩이나 스크롤바보다
        /// 싸고(새 히트 영역 0개), <see cref="MaxCarouselScroll"/>·드래그·휠은 그대로다.</para>
        /// </summary>
        private const float CarouselViewportWidth =
            CardStep * CarouselFullCards + CardWidth * CarouselPeekFraction;   // 520.5

        private const float CardHeight = 108f;
        private const float CardTopInSection = -28f;
        private const float ThumbX = 11f;

        // ★ 2026-09-01 — 카드 하단에 [착용]/[해제] 버튼이 들어오면서 <b>같은 108pt 안에서</b> 내부를
        //   다시 나눴다. 카드를 키울 수 없는 이유는 세로 예산이 이미 정확히 꽉 차 있어서다:
        //   섹션 4개 × SectionStep 156 = 624 = SectionsTopY(-72) ~ DetailY(-696) 사이 전부.
        //   그래서 썸네일 62 -> 54, 이름줄 16 -> 14로 줄이고 남은 22pt를 버튼에 준다.
        private const float ThumbY = -8f;
        private const float ThumbWidth = 119f;
        private const float ThumbHeight = 54f;

        /// <summary>썸네일(119×54pt) 안에서 아이콘이 차지하는 정사각 크기.
        /// <para>40 -> 50(2026-08-30 "아이콘이 조잡") -> <b>44</b>(2026-09-01 카드 하단 버튼).
        /// 썸네일 높이의 81%라는 <b>비율</b>은 50/62와 같게 유지했다 — 줄어든 것은 썸네일이지
        /// 아이콘이 차지하는 몫이 아니다.</para></summary>
        private const float IconSize = 44f;

        /// <summary>아이콘 획 두께. 핸드오프 스펙은 <b>40 viewBox 기준 1.7</b>이므로 <see cref="IconSize"/>가
        /// 커지면 <b>같은 비율로</b> 따라와야 형태가 원본과 같다(두께만 그대로 두면 선이 가늘어진다).</summary>
        private const float IconStroke = 1.7f * (IconSize / 40f);
        private const float LockBadgeWidth = 18f;
        private const float LockBadgeHeight = 17f;
        private const float CardNameY = -64f;
        private const float CardTextHeight = 14f;

        /// <summary>카드 하단 [착용]/[해제] 버튼. 상세 패널의 같은 버튼과 <b>같은 스타일 표</b>를 쓴다
        /// (<see cref="StyleActionButton"/>) — 두 자리가 다른 색으로 같은 상태를 말하지 않게.</summary>
        private const float CardActionY = -80f;
        private const float CardActionHeight = 22f;
        private const float CardActionWidth = ThumbWidth;   // 썸네일과 같은 폭 = 카드 좌우 여백 11pt와 정렬

        /// <summary>한 탭에 들어갈 수 있는 카테고리 수의 <b>상한</b>. 실제로 보여줄 수는 탭마다 다르고
        /// <see cref="SectionCountForTab"/>가 카탈로그에서 센다([장비] 4 / [외형] 3, 2026-08-30 기준).</summary>
        private const int SectionCount = 4;

        // ★ 2026-09-01 — <b>CardsPerSection(=4) / CardCount(=16) 상수를 지웠다.</b>
        //   카테고리당 아이템 수는 이제 콘텐츠(에셋)가 정하는 <b>가변값</b>이고, 코드가 4라고 적어 두면
        //   에셋을 늘리는 순간 다섯 번째부터가 <b>예외도 경고도 없이 화면에서 사라진다</b>. 카드 수는
        //   <see cref="CardsInSection"/>가 카탈로그에서 세고, 배치는 HorizontalLayoutGroup이 한다.

        private const int IconSetCount = 2;   // [장비]용 / [외형]용 — 카드 하나가 두 벌을 미리 갖는다.

        /// <summary>가로 캐러셀을 "끌었다"고 인정하는 최소 이동(캔버스 포인트). 이보다 작으면 손떨림이라
        /// 보고 클릭으로 처리한다 — 카드를 <b>누르려다</b> 1px 밀렸다고 착용이 취소되면 그게 더 나쁘다.</summary>
        private const float CarouselDragThresholdPoints = 4f;

        /// <summary>캐러셀을 실제로 민 뒤 이만큼은 uGUI 클릭을 먹지 않는다. 스크롤을 멈춘 손가락 아래에
        /// 있던 카드가 <b>뗄 때</b> 눌리는 것을 막는다(웹 목업의 <c>moved</c> 플래그와 같은 목적).</summary>
        private const float CarouselClickSuppressSeconds = 0.20f;

        // ---- 보관함 ----
        private const float InventoryRowHeight = 24f;
        private const float InventoryRowGap = 3f;
        private const int InventoryVisibleRows = 20;
        private const float InventoryRailWidth = 24f;
        private const float StatusSlotWidth = 96f;   // 훗날 가격표가 들어올 자리(디자이너 확정 최소 폭).
        private const float InventoryListWidth = RightContentWidth - InventoryRailWidth - UiChrome.Space2;

        /// <summary>목록 한 줄의 설명 칸에 들어가는 글자 수 상한(10pt 한글 기준 실측 — 칸 폭 약 264pt).</summary>
        private const int InventoryDescriptionChars = 24;

        private const float SlowRefreshInterval = 0.25f;
        private const float ClickPollInterval = 0.05f;
        private const float ActionDedupSeconds = 0.35f;

        /// <summary>액자 <b>안쪽</b>(RawImage가 실제로 차지하는) 크기(pt) — 액자 테두리 여백을 뺀 값.
        /// 촬영장 카메라의 기본 종횡비가 이 값에서 파생되므로(<see cref="CharacterPortraitStage.DesignAspect"/>)
        /// 액자 크기를 바꾸면 그림 구도도 함께 따라온다. 숫자를 두 곳에 적지 않기 위한 단일 출처다.
        /// 33-7-6에서 (176−24)/(238−24)=0.710 → (204−16)/(196−16)=<b>1.044</b>로 바뀌었다.</summary>
        public static Vector2 PortraitContentSize => new Vector2(
            LeftContentWidth - PortraitPadding * 2f,
            PortraitHeight - PortraitPadding * 2f);

        private enum Tab { Equipment = 0, Appearance = 1, Inventory = 2 }
        private const int TabCount = 3;

        /// <summary>좌측 스탯 행 수. 6 -> <b>5</b>(2026-09-01 사용자 요청 "넘어진 횟수 삭제").
        /// <b>지운 것은 표시뿐이다</b> — <see cref="CharacterStatsModel.RagdollFalls"/> 카운터는 그대로
        /// 살아 있다(오늘 낮에 긴 망토 걸림 오계수를 고치고 0으로 리셋한 그 값이다). 데이터를 함께
        /// 지우면 훗날 다른 화면에서 다시 쓸 때 계수 로직을 <b>처음부터 다시</b> 만들어야 한다.</summary>
        private const int StatCount = 5;

        private static readonly string[] TabNames = { "장비", "외형", "보관함" };
        private static readonly string[] StatLabels =
        {
            // ※ 4번째 칸은 "대결 승리"였다 — 라이벌 기능 전체 삭제(2026-08-30)로 영구 0이 되는
            //    죽은 칸이 되어 "보유 장비"(레벨에 따라 실제로 늘어나는 값)로 교체했다.
            // ※ "넘어진 횟수"(옛 6번째 칸)는 2026-09-01 사용자 요청으로 <b>표시만</b> 뺐다.
            //    CharacterStatsModel.RagdollFalls는 그대로 세고 있다(위 StatCount 문서 참고).
            "근속", "함께한 시간", "격파 성공", "보유 장비", "활쏘기 명중",
        };

        /// <summary>자물쇠 배지의 고리(스펙 SVG viewBox 20×21, 호는 미리 5점으로 샘플링).
        /// 몸통은 채운 둥근 사각형이라 <see cref="UiChrome.AddSurface"/>로 따로 그린다.</summary>
        private static readonly float[] LockShackle =
        {
            6.5f, 9.5f, 6.5f, 6.8f, 7.525f, 4.325f, 10f, 3.3f, 12.475f, 4.325f, 13.5f, 6.8f, 13.5f, 9.5f,
        };

        private StickmanAgent _agent;
        private IGlobalPointerButtonService _buttonService;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _panel;
        private BoxCollider2D _clickBlocker;

        private RectTransform _closeRect;
        private RectTransform _settingsRect;   // 헤더의 작은 [설정] 칩 — 설정창의 주 진입점(36-11).
        private RectTransform _titleBarRect;   // 드래그 손잡이(2026-08-30).
        private readonly Image[] _tabUnderlines = new Image[TabCount];
        private readonly Text[] _tabLabels = new Text[TabCount];
        private readonly RectTransform[] _tabRects = new RectTransform[TabCount];

        // ---- 좌측 컬럼 ----
        private Text _nameTitle;
        private RectTransform _nameRect;
        private InputField _nameInput;
        private RectTransform _nameInputRect;
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
        private readonly Text[] _statValues = new Text[StatCount];
        private readonly Image[] _inkRings = new Image[2];
        private readonly RectTransform[] _inkRects = new RectTransform[2];

        // ---- 우측: 카테고리 섹션 + 카드 ----
        private sealed class SectionView
        {
            /// <summary>섹션 한 덩어리(제목줄 + 가로 카드 캐러셀). 탭마다 카테고리 수가 다르므로
            /// <b>남는 섹션은 통째로 끈다</b> — 2026-08-30 표정(FACE) 삭제로 [외형] 탭이 3칸이 됐다.</summary>
            public GameObject Root;

            public Image Dot;
            public Text Title;
            public Text Code;
            public Text Count;

            /// <summary>가로 캐러셀. 드래그/관성/클램프는 uGUI의 <see cref="ScrollRect"/>에 맡기고
            /// 이 파일은 <b>content 좌표만</b> 다룬다(전역 폴링 드래그도 같은 좌표를 쓴다).</summary>
            public ScrollRect Row;

            /// <summary>캐러셀 줄의 사각형. <see cref="ScrollRect"/>의 <c>rectTransform</c>은 protected라
            /// 밖에서 못 읽는다 — 전역 폴링 히트테스트가 쓸 손잡이를 따로 들고 있는다.</summary>
            public RectTransform RowRect;

            public RectTransform Content;

            /// <summary>지금 이 섹션이 보여주고 있는 카테고리. 카테고리가 바뀌면(탭 전환) 스크롤을
            /// 처음으로 되돌린다 — 그러지 않으면 아이템이 적은 카테고리로 넘어갔을 때 <b>빈 칸만</b> 보인다.</summary>
            public EquipmentSlot BoundSlot;

            public bool HasBoundSlot;

            /// <summary>이 섹션이 쓰는 카드가 <see cref="_cards"/>의 어디부터 몇 장인가.</summary>
            public int FirstCard;

            public int CardCount;
        }

        private sealed class ItemCard
        {
            /// <summary>이 카드가 맡은 섹션/자리. 예전에는 배열 인덱스를 <c>CardsPerSection</c>으로
            /// 나눠 계산했는데, 카테고리마다 아이템 수가 달라진 뒤로 그 나눗셈이 성립하지 않는다 —
            /// 자기 자리는 <b>카드가 직접 들고 있는다</b>.</summary>
            public int Section;

            public int Item;

            public RectTransform Rect;
            public Image Surface;
            public Image Outline;
            public Image Thumb;
            public RectTransform LockBadge;
            public Text Name;
            public Text Meta;

            /// <summary>카드 하단 [착용]/[해제] 버튼(2026-09-01 사용자 요청). 카드 <b>본체</b> 클릭은
            /// 지금까지처럼 "고르기"만 하고, 옷을 갈아입히는 것은 이 버튼뿐이다 — 캐러셀을 밀다가
            /// 옷이 갈아입혀지는 사고를 구조적으로 없앤다.</summary>
            public Image ActionSurface;

            public Image ActionOutline;
            public RectTransform ActionRect;
            public Text ActionLabel;
            public readonly RectTransform[] IconRoot = new RectTransform[IconSetCount];
            public readonly Image[][] IconGraphics = new Image[IconSetCount][];

            /// <summary>해금 상태에서 되돌릴 <b>조각별 원래 색</b>(ItemCatalog가 정한 소재색).
            /// 잠긴 카드는 무채색 실루엣으로 덮어쓰므로, 덮어쓰기 전 색을 어딘가에 갖고 있어야 한다.
            /// 매 프레임 카탈로그를 다시 뒤지지 않으려고 카드가 굽는 시점에 한 번만 캐시한다.</summary>
            public readonly Color[][] IconBaseColors = new Color[IconSetCount][];
        }

        private readonly SectionView[] _sections = new SectionView[SectionCount];

        /// <summary>카드 실물. 개수는 <b>카탈로그가 정한다</b>(빌드 때 한 번만 센다) — 상수로 적으면
        /// 아이템 에셋을 늘렸을 때 다섯 번째부터가 조용히 사라진다.</summary>
        private ItemCard[] _cards = System.Array.Empty<ItemCard>();
        private GameObject _sectionPage;
        private GameObject _inventoryPage;

        private Text _detailName;
        private Text _detailMeta;
        private Text _detailBody;
        private Image _actionSurface;
        private Image _actionOutline;
        private RectTransform _actionRect;
        private Text _actionLabel;

        // ---- 보관함(가상 목록) ----
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
        private int _selectedInventoryIndex;

        private bool _open;
        private Tab _tab = Tab.Equipment;
        private EquipmentSlot _selectedSlot = EquipmentSlot.Head;
        private int _selectedItem;
        private int _hoveredCard = -1;
        private bool _editingName;
        private float _slowTimer;
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private bool _draggingPanel;
        private Vector2 _dragGrabOffsetPoints;
        private Vector2 _dragStartOffsetPoints;

        /// <summary>지금 잡고 있는 캐러셀의 섹션 번호(-1이면 안 잡았다).</summary>
        private int _carouselSection = -1;

        private float _carouselGrabScreenX;
        private float _carouselStartContentX;
        private bool _carouselMoved;
        private float _lastCarouselMoveTime = -999f;

        /// <summary>누른 자리가 카드 하단 버튼이면 <b>뗄 때까지 보류</b>한다(-1이면 없음).
        /// 착용을 누름이 아니라 뗌에 붙이는 이유는 하나다 — 그 사이에 카드를 밀었다면 그건
        /// 스크롤이지 착용이 아니다. 창의 다른 버튼들은 지금까지처럼 누름에 반응한다.</summary>
        private int _pendingEquipCard = -1;

        /// <summary>배타 규칙("이 창이 뜨면 부채꼴/팝오버는 접힌다")과 "창 밖 클릭" 예외가 쓰는 이웃 —
        /// 둘 다 같은 GameObject에 있고, 없을 수도 있으므로(테스트 조립) 늦게 한 번만 찾는다.</summary>
        private GearRadialMenuWidget _menu;

        /// <summary>구석 호버 패널(배타적 모달의 네 번째 표면). 지연 조회 — 이 컴포넌트가 붙지 않은
        /// 조립(테스트 씬 등)에서는 계속 null이고 <see cref="CloseOverlappingSurfaces"/>가 건너뛴다.</summary>
        private CornerHoverPanel _cornerPanel;
        private InfoGearIconWidget _gear;
        private string _lastActionKey;
        private float _lastActionTime;
        private StickmanStateId _lastShownState = (StickmanStateId)(-1);
        private bool _hasShownState;
        private float _lastDpiScale = -1f;

        /// <summary>히트테스트/호버 폴링이 돌려쓰는 코너 버퍼 — 이 앱은 하루 종일 켜져 있어서
        /// 0.05초마다 <c>new Vector3[4]</c>를 20번씩 만드는 것도 상시 쓰레기가 된다.</summary>
        private static readonly Vector3[] _corners = new Vector3[4];

        /// <summary>이 창 안의 모든 <see cref="RectMask2D"/> — 빌드 때 한 번만 모은다. 전역 폴링
        /// 히트테스트가 "마스크에 잘린 자리는 누를 수 없다"를 판단하는 근거(R2 M3).</summary>
        private RectMask2D[] _masks = System.Array.Empty<RectMask2D>();

        /// <summary>[착용] 버튼이 통째로 잘려 닿을 수 없는 상태인가 — 상태가 바뀔 때만 경고한다(로그 도배 방지).</summary>
        private bool _actionUnreachable;

        /// <summary>아이콘 한 파츠를 그릴 때 돌려쓰는 점 버퍼(가장 긴 파츠보다 넉넉하게).</summary>
        private static readonly Vector2[] _iconPoints = new Vector2[64];

        public bool IsOpen => _open;

        /// <summary>창이 실제로 켜져 있는가(진단/테스트 전용) — 플래그가 아니라 GameObject의 실제 상태.</summary>
        public bool IsCanvasActive => _canvas != null && _canvas.gameObject.activeSelf;

        /// <summary>클릭관통 차단막이 켜져 있는가(진단/테스트 전용, 비침해 원칙 2 검증용).</summary>
        public bool IsClickBlockerEnabled => _clickBlocker != null && _clickBlocker.enabled;

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 복제본에서 창이 두 벌 뜨지 않게 하는
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
            // ★ 2026-09-01 — 이 안내는 <b>없어진 문</b>을 광고하고 있었다: "(3) 캐릭터 우클릭"은 2026-08-31에
            //   폐지됐고, AppControlDirector.LogStartupBanner()가 <b>같은 부팅 로그에서</b> "우클릭 메뉴는
            //   폐지됐습니다"라고 말한다 — 두 문장이 서로를 반박했다(페르소나 M11). "(1) 톱니 클릭"도
            //   부정확했다(톱니는 부채꼴을 열 뿐, 정보창까지는 2클릭). 로그도 원칙 1의 적용 대상이다.
            Debug.Log($"[정보창] 준비 완료({PanelWidth:F0}×{PanelHeight:F0} 화면 중앙, 3탭: 장비/외형/보관함, " +
                $"카드 {_cards.Length}장 + 장비 {ItemCatalog.EquipmentCount}종) — 여는 방법 2가지: " +
                "(1) **화면 우상단 톱니 아이콘 -> 부채꼴 [캐릭터]**(주 진입점, 2클릭), " +
                $"(2) 전역 단축키 **{ShortcutLabel.Chord("I")}**. " +
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
            // 캔버스가 씬 루트로 나갔으므로(BuildUi 주석) 캐릭터가 사라져도 자동으로 따라 죽지 않는다 —
            // 여기서 명시적으로 거둔다. 컴포넌트만 제거되는 경로에서도 이 OnDestroy가 돌아
            // 캔버스가 남지 않는다.
            if (_canvas != null) Destroy(_canvas.gameObject);
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
            CloseOverlappingSurfaces($"캐릭터 창 열림({source})");
            ResetPanelToCenter();      // 33-7-7의 "열면 화면 중앙"은 유지한다(드래그는 그 뒤의 이야기).
            _leftInitialized = false; // 창을 여는 그 클릭이 곧바로 카드 클릭으로 오인되지 않게.
            _hoveredCard = -1;
            _pendingEquipCard = -1;
            EndCarouselDrag();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_clickBlocker != null) _clickBlocker.enabled = true;
            EndNameEdit(commit: false);
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
            _draggingPanel = false;
            _pendingEquipCard = -1;
            EndCarouselDrag();
            EndNameEdit(commit: true);
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_clickBlocker != null) _clickBlocker.enabled = false;
            if (_stage != null) _stage.SetRenderingEnabled(false);
            Debug.Log($"[정보창] 닫힘({source}).");
        }

        /// <summary>
        /// ★ 배타적 모달(2026-08-30) — 이 창이 뜨는 순간 부채꼴 메뉴와 팝오버 2종을 거둔다.
        /// <b>정리 책임을 여는 쪽 한 곳에만</b> 둔다. 진입점(부채꼴 [캐릭터] / 단축키 ⌃⌥⌘I / 우클릭
        /// 메뉴)마다 정리 코드를 흩뿌리면 네 번째 진입점이 생길 때 또 샌다 — 실제로 단축키 경로가
        /// 아무것도 닫지 않아 캔버스 3개(창 + 부채꼴 + 팝오버)가 동시에 뜨는 화면이 재현됐다.
        /// </summary>
        private void CloseOverlappingSurfaces(string reason)
        {
            // ★ 2026-08-31 추가 — 네 번째 표면(구석 호버 패널). 이 줄이 없으면 다이얼을 <b>끌고 있는
            // 동안</b>에는 그 패널의 자기 치유(DetectionArmed)가 통째로 멈춰 있어(끌다가 영역을 벗어나는
            // 것이 정상 동작이라 일부러 그렇게 만들어져 있다) 액자 두 개가 겹친 채 남고 초상화 카메라가
            // 2대 동시에 돈다. 이 메서드 문서의 "네 번째 진입점이 생기면 또 샌다"가 실제로 일어난 지점이라
            // 아래 부채꼴 분기의 early-return **위에** 둔다(부채꼴이 있든 없든 반드시 실행되어야 한다).
            if (_cornerPanel == null) _cornerPanel = GetComponent<CornerHoverPanel>();
            if (_cornerPanel != null) _cornerPanel.ForceHide(reason);

            if (_menu == null) _menu = GetComponent<GearRadialMenuWidget>();
            if (_menu != null)
            {
                _menu.ForceCloseAll(reason);
                return;
            }

            // 부채꼴이 없는 조립(테스트 씬 등)에서도 팝오버는 남아 있을 수 있다.
            var focus = GetComponent<FocusSessionPopover>();
            if (focus != null) focus.Close(reason);
            var todo = GetComponent<TodoBoardPopover>();
            if (todo != null) todo.Close(reason);
        }

        // ==================== 루프 ====================

        private void Update()
        {
            if (!_open) return; // 닫혀 있으면 아무 비용도 들이지 않는다.

            // ★★ 절대 불변 원칙 2(비침해) — 전체화면 게임이 감지되면 창을 닫는다.
            // StickmanAgent.Suspend()는 Awake에서 캐시한 캐릭터 렌더러만 끄고, 이 창은 씬 루트 캔버스
            // + 씬 루트 차단막이라 그 배열에 없다. 게다가 StickmanAgent가 SetAlwaysOnTop(true)를 켜므로
            // 전체화면 게임 위에 880×861 창이 그대로 떠 있고, 히트테스트가 픽셀 알파 기반이라 그
            // 영역의 클릭까지 먹는다. Close()가 캔버스/차단막/초상화 촬영장을 한 번에 정리한다.
            // 복귀 시 강제로 다시 열지 않는다 — 사용자가 톱니로 다시 연다.
            if (_agent != null && _agent.IsSuspended)
            {
                Close("전체화면 감지 — 자동 숨김(비침해 원칙 2)");
                return;
            }

            // ★★ 2026-08-31 — 사용자 신고 "기어 설정창조차 클릭하면 약간 렉걸린듯이 움직임".
            //
            // 이 창이 열려 있다는 것은 <b>사용자가 지금 이것을 보고 있다는 관측된 사실</b>이다. 그런데
            // 적응형 프레임 페이싱은 그 사실을 몰랐고, "캐릭터가 Idle + 최근 2초 무입력"만 보고 Calm
            // 등급으로 내려갔다 — 창을 **읽는 동안**이 정확히 그 조건이다(마우스를 안 움직인다).
            // Windows에서 Calm은 targetFrameRate를 60->30으로 나눠 **게임 루프 자체를 30Hz**로 만들고,
            // 아래 TickGlobalPointer()는 Update()마다 OS 커서를 한 번 폴링하므로 드래그가 커서를
            // 계단식으로 따라오게 된다. 게다가 복귀는 다음 관측 폴링(최대 0.2초)에나 일어나서,
            // **모든 상호작용의 첫 0.2초**가 절반 프레임레이트로 시작했다.
            //
            // 결합은 이 한 줄뿐이다(UI -> FramePacing 단방향). 홀드는 만료 시각 방식이라 이 창이
            // 어떤 경로로 죽어도 0.5초 뒤 저절로 풀린다 — 해제 책임이 존재하지 않는다.
            // 이 홀드는 Calm만 이긴다: 전체화면 숨김/화면 꺼짐/자리비움(3분)은 그대로 이긴다
            // (FramePacingPolicy.DecideTier의 우선순위 문서 참고 — 원칙 2와 24시간 상주 절감 보호).
            FramePacing.HoldActiveForInteraction();

            ApplyCanvasScaleFactor();
            SyncClickBlocker();
            TickGlobalPointer();
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
            RefreshCards();     // 레벨이 오르면 잠긴 카드가 열린다.
            RefreshDetail();
            RefreshInventoryList();
        }

        private void OnEquipmentChanged()
        {
            if (!_open) return;
            RefreshCards();
            RefreshDetail();
            RefreshInventoryList();
        }

        private void RefreshAll()
        {
            _hasShownState = false;
            // ★ 가시성이 <b>먼저</b>다. RefreshCards가 캐러셀 폭을 즉시 다시 재는데
            //   (LayoutRebuilder.ForceRebuildLayoutImmediate), 꺼져 있는 페이지에서는 그 계산이 돌지 않아
            //   스크롤 한계가 옛 값으로 남는다.
            ApplyTabVisibility();
            TickPresenceLine();
            RefreshNumbers();
            RefreshCards();
            RefreshDetail();
            RefreshInventoryList();
            RefreshInkSwatches();
        }

        /// <summary>0.25초 주기로 다시 만드는 값만 여기 있다 — 카드/상세는 <b>사건이 있을 때만</b>
        /// 갱신한다(카드 수십 장의 문자열을 초당 4번 다시 만들 이유가 없다).</summary>
        private void RefreshNumbers()
        {
            if (_nameTitle != null && !_editingName) _nameTitle.text = CharacterProgressionModel.CharacterName;
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

            // 스탯 5행. 0인 항목은 숫자 대신 회색 "아직 없음"으로 — 0이 성취처럼 보이지 않게 한다.
            SetStat(0, $"{CharacterStatsModel.DaysTogether}일차", true);
            SetStat(1, CharacterStatsModel.FormatCompanionTime(), true);
            SetStat(2, CharacterStatsModel.BattleWins > 0 ? $"{CharacterStatsModel.BattleWins}번" : null, CharacterStatsModel.BattleWins > 0);
            int ownedItems = ItemCatalog.UnlockedEquipmentCount(_config);
            SetStat(3, $"{ownedItems} / {ItemCatalog.EquipmentCount}종", ownedItems > 0);
            SetStat(4, CharacterStatsModel.TryGetArcheryAccuracy01(out float acc)
                ? $"{CharacterStatsModel.ArcheryBullseyes} / {CharacterStatsModel.ArcheryShots} ({acc * 100f:F0}%)"
                : "기록 없음", CharacterStatsModel.ArcheryShots > 0);
            // ※ 옛 5번 칸(넘어진 횟수)은 표시에서 빠졌다. CharacterStatsModel.RagdollFalls는 계속 센다.
        }

        /// <summary>스탯 한 칸. <paramref name="value"/>가 null이면 회색 "아직 없음"으로 대신한다.</summary>
        private void SetStat(int index, string value, bool hasRecord)
        {
            if (index < 0 || index >= _statValues.Length || _statValues[index] == null) return;
            _statValues[index].text = value ?? "아직 없음";
            _statValues[index].color = hasRecord ? UiChrome.TextPrimary : UiChrome.TextQuaternary;
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

        // ==================== 카테고리 섹션 + 카드 ====================

        /// <summary>탭이 보여주는 <paramref name="section"/>번째 카테고리. "외형 계열"의 정의는
        /// <see cref="EquipmentModel.IsAppearanceSlot"/> 하나뿐이라 여기서 숫자를 다시 적지 않는다.</summary>
        private static EquipmentSlot SectionSlot(Tab tab, int section)
        {
            bool wantAppearance = tab == Tab.Appearance;
            int found = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (EquipmentModel.IsAppearanceSlot(slot) != wantAppearance) continue;
                if (found == section) return slot;
                found++;
            }
            return EquipmentSlot.Head;
        }

        /// <summary>이 탭이 실제로 보여줄 카테고리 수. 숫자를 적지 않고 <b>센다</b> —
        /// 카테고리를 지우거나 더할 때 여기와 표가 어긋나면 빈 제목줄이 남거나 한 칸이 사라진다
        /// (2026-08-30 표정 삭제가 정확히 그 경우였다).</summary>
        private static int SectionCountForTab(Tab tab)
        {
            bool wantAppearance = tab == Tab.Appearance;
            int n = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                if (EquipmentModel.IsAppearanceSlot((EquipmentSlot)i) == wantAppearance) n++;
            }
            return Mathf.Min(n, SectionCount);
        }

        private static int IconSetForTab(Tab tab) => tab == Tab.Appearance ? 1 : 0;

        /// <summary>
        /// 이 섹션 자리가 <b>두 탭을 통틀어</b> 최대 몇 장의 카드를 필요로 하는가.
        /// 카드는 탭을 바꿔도 다시 굽지 않는 재사용 자원이므로(클래스 문서), 한 섹션의 카드 풀은
        /// [장비]쪽 카테고리와 [외형]쪽 카테고리 중 <b>많은 쪽</b>에 맞춘다.
        /// <para>숫자를 적지 않고 <see cref="ItemCatalog"/>에서 <b>센다</b> — 아이템 에셋을 늘리는 것만으로
        /// 카드가 따라 늘어나야 원칙 4("신규 콘텐츠는 기본 로직 무수정")가 실제로 성립한다.</para>
        /// </summary>
        private static int CardsInSection(int section)
        {
            int n = 0;
            if (section < SectionCountForTab(Tab.Equipment))
            {
                n = Mathf.Max(n, ItemCatalog.ItemCountIn(SectionSlot(Tab.Equipment, section)));
            }
            if (section < SectionCountForTab(Tab.Appearance))
            {
                n = Mathf.Max(n, ItemCatalog.ItemCountIn(SectionSlot(Tab.Appearance, section)));
            }
            return n;
        }

        private void RefreshCards()
        {
            if (_tab == Tab.Inventory) return;   // 목록 탭에는 카드가 없다.
            int set = IconSetForTab(_tab);

            int visible = SectionCountForTab(_tab);
            for (int s = 0; s < SectionCount; s++)
            {
                SectionView view = _sections[s];
                if (view == null) continue;
                if (view.Root != null && view.Root.activeSelf != (s < visible)) view.Root.SetActive(s < visible);
                if (s >= visible) continue;

                EquipmentSlot slot = SectionSlot(_tab, s);
                Color tint = UiChrome.CategoryTint(slot);
                view.Dot.color = tint;
                view.Title.text = EquipmentModel.SlotName(slot);
                view.Code.text = EquipmentModel.SlotCode(slot);
                view.Count.text = $"{EquipmentModel.OwnedItemCount(slot)} / {EquipmentModel.ItemCount(slot)}";

                // 카테고리가 바뀌었으면 캐러셀을 처음으로 되돌린다 — 아이템이 적은 카테고리로
                // 넘어갔을 때 스크롤이 남아 있으면 <b>빈 자리</b>가 보인다.
                if (!view.HasBoundSlot || view.BoundSlot != slot)
                {
                    view.HasBoundSlot = true;
                    view.BoundSlot = slot;
                    ResetCarousel(view);
                }

                int items = ItemCatalog.ItemCountIn(slot);
                for (int c = 0; c < view.CardCount; c++)
                {
                    ItemCard card = _cards[view.FirstCard + c];
                    if (card == null) continue;

                    bool used = c < items;
                    if (card.Rect.gameObject.activeSelf != used) card.Rect.gameObject.SetActive(used);
                    if (!used) continue;
                    ApplyCardStyle(card, slot, c, set);
                }

                // 활성 카드 수가 바뀌면 가로 폭이 달라진다 — 다음 캔버스 갱신까지 기다리면
                // 그 한 프레임 동안 스크롤 한계가 옛 값이라 끝까지 밀리지 않는다.
                if (view.Content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(view.Content);
            }
        }

        private static void ResetCarousel(SectionView view)
        {
            if (view == null || view.Content == null) return;
            Vector2 p = view.Content.anchoredPosition;
            if (Mathf.Approximately(p.x, 0f)) return;
            p.x = 0f;
            view.Content.anchoredPosition = p;
        }

        /// <summary>33-7-3 카드 상태 5종 스타일 표를 그대로 옮긴 유일한 자리.</summary>
        private void ApplyCardStyle(ItemCard card, EquipmentSlot slot, int itemIndex, int iconSet)
        {
            ItemCatalogEntry entry = ItemCatalog.Item(slot, itemIndex);
            for (int i = 0; i < IconSetCount; i++)
            {
                if (card.IconRoot[i] != null) card.IconRoot[i].gameObject.SetActive(i == iconSet);
            }
            if (entry == null) return;

            bool owned = entry.IsOwned(_config);
            bool worn = entry.IsEquipped();
            bool selected = slot == _selectedSlot && itemIndex == _selectedItem;
            bool hovered = _hoveredCard >= 0 && _cards[_hoveredCard] == card;
            Color tint = UiChrome.CategoryTint(slot);

            card.Name.text = owned ? entry.DisplayName : "???";
            card.Name.color = owned ? UiChrome.TextPrimary : UiChrome.TextDisabled;

            if (!owned)
            {
                // "LV.20" — 잠긴 카드의 메타는 <b>언제 열리는지</b> 하나만 말한다.
                card.Meta.text = $"LV.{entry.RequiredLevel}";
                card.Meta.color = UiChrome.TextDisabled;
                card.Surface.color = UiChrome.CardSurfaceMuted;
                card.Thumb.color = UiChrome.ThumbSurfaceLocked;
                // 잠김 = <b>무채색 실루엣</b>. 해금 전에 소재색을 미리 보여주면 잠금 연출이 무의미해진다.
                SetIconColor(card, iconSet, new Color(UiChrome.TextTertiary.r, UiChrome.TextTertiary.g,
                    UiChrome.TextTertiary.b, 0.34f));
            }
            else
            {
                card.Meta.text = worn ? "착용 중" : "보유";
                card.Meta.color = worn ? tint : UiChrome.TextQuaternary;
                card.Surface.color = UiChrome.CardSurface;
                // 착용 중 썸네일 바탕은 <b>카테고리 틴트가 아니라 강조색 wash</b>다(2026-08-30).
                // 같은 라운드에 아이템별 소재색이 들어오면서, 카테고리 틴트를 그대로 깔면 그 카테고리의
                // 틴트를 쓰는 아이콘(나비넥타이=초록, 짧은망토=보라, 발자국=초록)이 <b>제 배경색과
                // 같은 색</b>이 되어 형태가 사라진다. 착용 테두리(CardBorderWorn)도 이미 강조색이므로
                // 바탕도 같은 계열로 맞추는 편이 "지금 걸치고 있는 칸"이라는 신호가 하나로 읽힌다.
                // 카테고리는 섹션 헤더의 틴트 도트와 슬롯 코드가 이미 말하고 있다.
                // ★ 2026-08-31 — wash를 <b>미리 합성한 불투명색</b>으로 넣는다. AccentSurface(α0.14)를
                //   그대로 칠하면 이 119x62pt 썸네일 위에서만 창 알파가 0.88로 내려가 <b>착용 중인 칸에만</b>
                //   뒤 창이 12% 비친다(UiChrome '알파 채널의 법칙'). 아래에 있는 것은 항상 불투명한
                //   CardSurface이므로 합성 결과 색은 완전히 같다.
                card.Thumb.color = worn ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurface)
                    : UiChrome.CardSurfaceMuted;
                // 해금됐으면 <b>아이템 고유의 소재색</b>으로 되돌린다(2026-08-30). 예전에는 착용 여부에 따라
                // 아이콘 전체를 카테고리 틴트/잉크 한 색으로 덮어써서 32칸이 전부 같은 색으로 보였다.
                // "착용 중"은 이미 테두리(CardBorderWorn) + 썸네일 wash + 메타 문구 셋이 말하고 있다.
                RestoreIconColors(card, iconSet);
            }

            // 테두리 우선순위: 선택 > hover > 착용 중 > 기본. (스펙 1.4 표의 "선택됨이 최우선")
            card.Outline.color = selected ? UiChrome.TextPrimary
                : hovered ? UiChrome.CardBorderHover
                : worn && owned ? UiChrome.CardBorderWorn
                : UiChrome.CardBorder;

            if (card.LockBadge != null) card.LockBadge.gameObject.SetActive(!owned);

            // 카드 하단 버튼 — 상세 패널의 [착용] 버튼과 <b>같은 표</b>를 쓴다.
            StyleActionButton(card.ActionSurface, card.ActionOutline, card.ActionLabel, owned, worn);
        }

        /// <summary>
        /// [착용]/[해제] 버튼 한 벌의 스타일 — 33-7-4의 상태 표. <b>카드 하단 버튼과 상세 패널 버튼이
        /// 이 함수 하나를 공유한다</b>. 두 벌로 두면 같은 상태를 두 자리가 다른 색으로 말하게 된다
        /// (이 프로젝트가 반복해서 겪은 이중 정의 계열 실패이고, 여기서는 <b>화면에서 바로 보인다</b>).
        /// <para>색은 전부 불투명값이다 — 투명 오버레이에서 알파를 겹치면 그 자리만 뒤 창이 비친다
        /// (UiChrome '알파 채널의 법칙').</para>
        /// </summary>
        private static void StyleActionButton(Image surface, Image outline, Text label, bool owned, bool worn)
        {
            if (label != null)
            {
                // 잠긴 카드에 "LV.20"이라고 적지 않는다 — 바로 위 메타 줄이 이미 그 숫자를 말하고 있다.
                label.text = !owned ? "잠김" : worn ? "해제" : "착용";
                label.color = !owned ? UiChrome.TextDisabled
                    : worn ? UiChrome.TextPrimary : UiChrome.OnAccentSolid;
            }
            if (surface != null)
            {
                surface.color = !owned ? UiChrome.CardSurfaceMuted
                    : worn ? UiChrome.CardSurface : UiChrome.TextPrimary;
            }
            if (outline != null)
            {
                outline.color = owned ? UiChrome.TextPrimary : UiChrome.CardBorder;
            }
        }

        /// <summary>조각별 원래 소재색으로 되돌린다.</summary>
        private static void RestoreIconColors(ItemCard card, int iconSet)
        {
            Image[] graphics = card.IconGraphics[iconSet];
            Color[] baseColors = card.IconBaseColors[iconSet];
            if (graphics == null || baseColors == null) return;
            int count = Mathf.Min(graphics.Length, baseColors.Length);
            for (int i = 0; i < count; i++)
            {
                if (graphics[i] != null) graphics[i].color = baseColors[i];
            }
        }

        private static void SetIconColor(ItemCard card, int iconSet, Color color)
        {
            Image[] graphics = card.IconGraphics[iconSet];
            if (graphics == null) return;
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null) graphics[i].color = color;
            }
        }

        /// <summary>선택 상세 패널(33-7-4). <b>잠긴 아이템도 선택은 된다</b> — 왜 잠겼는지 알 수 있는
        /// 유일한 경로이기 때문이다. 버튼 클릭만 무시한다.</summary>
        private void RefreshDetail()
        {
            if (_tab == Tab.Inventory) return;
            ItemCatalogEntry entry = ItemCatalog.Item(_selectedSlot, _selectedItem);
            if (entry == null) return;

            bool owned = entry.IsOwned(_config);
            bool worn = entry.IsEquipped();

            if (_detailName != null)
            {
                _detailName.text = owned ? entry.DisplayName : "???";
                _detailName.color = owned ? UiChrome.TextPrimary : UiChrome.TextDisabled;
            }
            if (_detailMeta != null)
            {
                _detailMeta.text = !owned
                    ? $"{entry.CategoryLabel}  ·  Lv.{entry.RequiredLevel}에 열림"
                    : $"{entry.CategoryLabel}  ·  {(worn ? "착용 중" : "보유 중")}";
            }
            if (_detailBody != null)
            {
                _detailBody.text = owned
                    ? entry.Description
                    : $"레벨 {entry.RequiredLevel}이 되면 열립니다. 지금은 실루엣만 보입니다.";
                _detailBody.color = owned ? UiChrome.TextSecondary : UiChrome.TextDisabled;
            }

            StyleActionButton(_actionSurface, _actionOutline, _actionLabel, owned, worn);
        }

        // ==================== 보관함(가상 목록) ====================

        /// <summary>목록의 논리적 줄 수 = 헤더 2줄 + 카탈로그 전체(장비 32 + 행동 13).</summary>
        private static int InventoryLineCount => ItemCatalog.Count + 2;

        /// <summary>논리적 줄 번호 -> 카탈로그 인덱스. 헤더면 -1.
        /// 순서: [걸치는 것] 헤더 → 장비 32종 → [할 줄 아는 것] 헤더 → 행동 13종.
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
                return $"걸치는 것  ({ItemCatalog.UnlockedEquipmentCount(_config)} / {ItemCatalog.EquipmentCount})";
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
                    view.Surface.color = Color.clear;
                    view.Outline.color = Color.clear;
                    view.Dot.color = Color.clear;
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
                bool worn = entry.IsEquipped();

                view.HeaderText.text = string.Empty;
                view.Title.text = owned ? entry.DisplayName : "???";
                view.Subtitle.text = entry.CategoryLabel;
                view.Description.text = owned ? Ellipsize(entry.ShortDescription, InventoryDescriptionChars) : string.Empty;
                view.StatusSlot.text = entry.ResolveStatusSlot(_config);

                view.Surface.color = selected ? UiChrome.CardSurface
                    : owned ? UiChrome.CardSurface : UiChrome.CardSurfaceMuted;
                view.Outline.color = selected ? UiChrome.TextPrimary
                    : worn ? UiChrome.CardBorderWorn : UiChrome.CardBorder;
                view.Dot.color = entry.Slot.HasValue
                    ? (worn ? UiChrome.CategoryTint(entry.Slot.Value)
                            : owned ? UiChrome.TextQuaternary : UiChrome.TrackBackground)
                    : UiChrome.TextQuaternary;
                view.Title.color = owned ? UiChrome.TextPrimary : UiChrome.TextDisabled;
                view.Subtitle.color = UiChrome.TextQuaternary;
                view.Description.color = owned ? UiChrome.TextSecondary : UiChrome.TextDisabled;
                view.StatusSlot.color = worn && entry.Slot.HasValue ? UiChrome.CategoryTint(entry.Slot.Value)
                    : owned ? UiChrome.TextTertiary : UiChrome.TextDisabled;
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
        /// 걸려 <b>반쯤 잘린 글자</b>가 남는다 — 잘렸다는 사실을 말줄임표로 <b>드러내는</b> 편이
        /// 정직하고 깔끔하다. 전문은 아래 상세 카드가 보여준다.</summary>
        private static string Ellipsize(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
            return text.Substring(0, maxChars).TrimEnd() + "...";
        }

        private void RefreshInventoryDetail()
        {
            ItemCatalogEntry entry = ItemCatalog.At(_selectedInventoryIndex);
            if (entry == null) return;
            bool owned = entry.IsOwned(_config);

            if (_inventoryDetailName != null)
            {
                _inventoryDetailName.text = owned
                    ? $"{entry.DisplayName}   ·   {entry.CategoryLabel}   ·   {entry.ResolveStatusSlot(_config)}"
                    : $"???   ·   {entry.CategoryLabel}   ·   {entry.ResolveStatusSlot(_config)}";
            }
            if (_inventoryDetailBody != null)
            {
                _inventoryDetailBody.text = owned
                    ? entry.Description
                    : $"레벨 {entry.RequiredLevel}이 되면 열립니다. 지금은 실루엣만 보입니다.";
            }
        }

        private void ScrollInventory(int delta)
        {
            int next = Mathf.Clamp(_inventoryScroll + delta * InventoryVisibleRows, 0, MaxInventoryScroll);
            if (next == _inventoryScroll) return;
            _inventoryScroll = next;
            RefreshInventoryList();
        }

        private void RefreshInkSwatches()
        {
            bool white = _config != null && _config.IsWhiteInk();
            for (int i = 0; i < _inkRings.Length; i++)
            {
                bool active = (i == 1) == white;
                if (_inkRings[i] != null)
                {
                    _inkRings[i].color = active ? UiChrome.TextPrimary : UiChrome.PanelBorder;
                }
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
            EndNameEdit(commit: true);
            ApplyTabVisibility();   // 카드 갱신보다 먼저(RefreshAll과 같은 이유 — 그 문단 참고).

            // 선택이 이 탭에 없는 카테고리를 가리키고 있으면 첫 카테고리로 옮긴다 — 그러지 않으면
            // [외형] 탭에서 [장비] 아이템의 설명이 보인다(화면과 상세가 다른 말을 하는 상태).
            if (tab != Tab.Inventory)
            {
                bool wantAppearance = tab == Tab.Appearance;
                if (EquipmentModel.IsAppearanceSlot(_selectedSlot) != wantAppearance)
                {
                    _selectedSlot = SectionSlot(tab, 0);
                    _selectedItem = 0;
                }
                RefreshCards();
                RefreshDetail();
            }

            Debug.Log($"[정보창] 탭 전환 -> [{TabNames[(int)tab]}].");
        }

        private void ApplyTabVisibility()
        {
            bool sections = _tab != Tab.Inventory;
            if (_sectionPage != null) _sectionPage.SetActive(sections);
            if (_inventoryPage != null) _inventoryPage.SetActive(!sections);

            for (int i = 0; i < TabCount; i++)
            {
                bool active = i == (int)_tab;
                if (_tabLabels[i] != null)
                {
                    _tabLabels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                    _tabLabels[i].color = active ? UiChrome.TextPrimary : UiChrome.TabInactive;
                }
                if (_tabUnderlines[i] != null)
                {
                    _tabUnderlines[i].color = active ? UiChrome.TextPrimary : Color.clear;
                }
            }
        }

        /// <summary>카드 클릭 = <b>선택</b>. 착용/해제는 상세 패널의 버튼 하나로만 한다 —
        /// "고른다"와 "입는다"를 같은 클릭에 겹치면, 설명을 읽으려고 눌렀을 뿐인데 옷이 갈아입혀진다.</summary>
        private void OnCardClicked(int cardIndex)
        {
            ItemCard card = CardAt(cardIndex);
            if (card == null) return;
            EquipmentSlot slot = SectionSlot(_tab, card.Section);
            int item = card.Item;
            if (_selectedSlot == slot && _selectedItem == item) return;

            _selectedSlot = slot;
            _selectedItem = item;
            RefreshCards();
            RefreshDetail();
            Debug.Log($"[{TabNames[(int)_tab]}] 선택 -> {EquipmentModel.ItemName(slot, item)}({EquipmentModel.SlotName(slot)}).");
        }

        /// <summary>
        /// ★ 카드 하단 [착용]/[해제] — 2026-09-01 사용자 요청("착용 버튼을 각 장비 하단에").
        ///
        /// <para><b>같은 카테고리 안의 상호배타</b>는 여기서 새로 만들지 않는다. 착용 상태가
        /// <c>EquipmentModel</c>의 <b>카테고리당 정수 한 칸</b>이라 모자 하나를 걸치면 그 칸이
        /// 덮어써지고 앞의 모자는 <b>구조적으로</b> 벗겨진다 — 이 버튼은 그 기존 경로
        /// (<see cref="EquipmentModel.ToggleItem"/> -> 저장 -> 이벤트)를 그대로 탈 뿐이다.
        /// 여기에 "다른 것을 벗긴다"는 코드를 한 줄이라도 더 쓰면 규칙이 두 곳에 생긴다.</para>
        ///
        /// <para>고르기(카드 본체 클릭)와 입기(이 버튼)를 나눈 이유는 두 가지다: 설명을 읽으려고 눌렀을
        /// 뿐인데 옷이 갈아입혀지는 것을 막고, <b>캐러셀을 밀다가</b> 착용되는 것을 막는다.</para>
        /// </summary>
        private void OnCardEquipClicked(int cardIndex)
        {
            ItemCard card = CardAt(cardIndex);
            if (card == null) return;
            EquipmentSlot slot = SectionSlot(_tab, card.Section);

            // 선택도 이 카드로 옮긴다 — 버튼을 눌렀는데 아래 상세 패널이 다른 아이템을 설명하고 있으면
            // 화면이 두 가지를 동시에 말하게 된다.
            _selectedSlot = slot;
            _selectedItem = card.Item;
            OnActionClicked();
        }

        private ItemCard CardAt(int index)
            => index >= 0 && index < _cards.Length ? _cards[index] : null;

        private void OnActionClicked()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(_selectedSlot, _selectedItem);
            if (entry == null) return;

            if (!entry.IsOwned(_config))
            {
                // 33-7-4: 잠긴 항목은 버튼 클릭만 무시한다(선택은 되고 설명도 보인다).
                Debug.Log($"[{TabNames[(int)_tab]}] {entry.DisplayName}은(는) 아직 잠겨 있습니다 — " +
                    $"Lv.{entry.RequiredLevel}에서 열립니다(현재 Lv.{CharacterProgressionModel.Level}).");
                return;
            }

            // ★ 2026-09-01(페르소나 소은 #4-a) — 사건은 <b>둘</b>인데 서술이 하나였다: 같은 카테고리의
            //   앞 아이템이 자동으로 벗겨지는데 로그는 "털모자 착용"만 말했다. 화면에서는 강조가 옆
            //   카드로 옮겨가는 것이 보이지만, 그 카드가 <b>캐러셀 밖</b>이면 피드백이 0이라 이 한
            //   조각이 유일한 단서가 된다. 벗겨진 쪽은 토글 <b>전에</b>만 알 수 있다.
            int replacedItem = EquipmentModel.WornIndex(_selectedSlot);
            if (!EquipmentModel.ToggleItem(_selectedSlot, _selectedItem, _config)) return;

            bool nowWorn = entry.IsEquipped();
            ItemCatalogEntry replaced = nowWorn && replacedItem != EquipmentModel.NotWorn
                && replacedItem != _selectedItem ? ItemCatalog.Item(_selectedSlot, replacedItem) : null;
            Debug.Log($"[{TabNames[(int)_tab]}] {entry.DisplayName} {(nowWorn ? "착용" : "해제")}" +
                (replaced != null ? $"(같은 카테고리의 {replaced.DisplayName}은(는) 자동 해제)" : string.Empty) +
                " — 초상화와 캐릭터에 즉시 반영, 즉시 저장.");
            CharacterSaveStore.Save(); // "모든 토글은 즉시 반영(별도 저장 버튼 없음)".
            RefreshCards();
            RefreshDetail();
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

        /// <summary>잉크색 전환 — 우클릭 메뉴 [잉크색] / 단축키 ⌃⌥⌘C와 <b>같은 경로</b>를 쓴다.
        /// 33-7-8에서 [외형] 탭이 카테고리로 꽉 차면서 이 버튼이 갈 곳을 잃었고, 없애면
        /// 잉크색 전환의 <b>유일한 GUI 경로</b>가 사라지므로(남는 건 단축키뿐 = 발견 불가능)
        /// 좌측 이름 블록으로 옮겼다 — 리더 승인 사항.</summary>
        private void OnInkSwatchClicked(bool white)
        {
            if (_config == null) return;
            StickmanInkColor next = white ? StickmanInkColor.White : StickmanInkColor.Black;
            if (_config.ResolveInkPreset() == next) return;

            // ★ 2026-08-31 R5 — 예전에는 여기서 `_config.inkColor = next`로 **직렬화 필드**에 썼다.
            //   그 _config는 프리팹 16개 컴포넌트에 배선된 배포 에셋 그 자체라, 에디터에서 한 번
            //   눌러 보고 프로젝트를 저장하면 출하 기본값이 바뀌어 전 사용자에게 나갔다
            //   (characterScale에서 이미 겪은 것과 같은 실패 모드 — StickConfig의 해당 문단 참고).
            //   이제 (1) 이번 실행의 값은 [NonSerialized] 런타임 오버라이드에, (2) 사용자의 선택은
            //   저장 파일(CharacterAppearanceModel, 스키마 v7)에 각각 남는다.
            _config.SetRuntimeInkColor(next);
            CharacterAppearanceModel.SetInkColor(next);
            if (_agent != null) _agent.ApplyInkColorFromConfig();
            RefreshInkSwatches();
            ApplyPortraitTheme();
            CharacterSaveStore.Save();   // "모든 토글은 즉시 반영(별도 저장 버튼 없음)".
            Debug.Log($"[정보창] 잉크색 전환 -> {next} (초상화/캐릭터/액세서리에 즉시 반영, 즉시 저장).");
        }

        private void ApplyPortraitTheme()
        {
            if (_stage != null) _stage.RefreshTheme();
            bool whiteInk = _config != null && _config.IsWhiteInk();
            // 액자 바탕은 촬영장의 배경색과 <b>같은 값</b>이어야 한다 — 다르면 8pt 테두리 여백에서
            // 색이 갈라진 이음매가 보인다. 그래서 33-1의 PortraitSurface를 직접 쓰지 않고 촬영장의
            // 판단을 그대로 따른다(색 결정이 두 곳으로 흩어지지 않게).
            if (_portraitFrame != null) _portraitFrame.color = CharacterPortraitStage.ResolveBackdropColor(_config);
            if (_portraitBorder != null)
            {
                _portraitBorder.color = whiteInk ? new Color(1f, 1f, 1f, 0.18f) : UiChrome.CardBorder;
            }
        }

        // ==================== 이름 인라인 편집 (33-7-8, 리더 승인) ====================

        private void BeginNameEdit()
        {
            if (_editingName || _nameInput == null) return;
            _editingName = true;
            _nameTitle.gameObject.SetActive(false);
            _nameInputRect.gameObject.SetActive(true);
            _nameInput.text = CharacterProgressionModel.CharacterName;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_nameInput.gameObject);
            _nameInput.ActivateInputField();
            Debug.Log("[정보창] 이름 편집 시작 — 그 자리에서 고치고 Enter(또는 창 닫기)로 확정됩니다.");
        }

        private void EndNameEdit(bool commit)
        {
            if (_nameInput == null) return;
            if (commit && _editingName && _nameInput.text != CharacterProgressionModel.CharacterName)
            {
                CharacterProgressionModel.SetCharacterName(_nameInput.text);
                CharacterSaveStore.Save();
                Debug.Log($"[정보창] 이름 변경 -> \"{CharacterProgressionModel.CharacterName}\".");
            }
            _editingName = false;
            if (_nameInputRect != null) _nameInputRect.gameObject.SetActive(false);
            if (_nameTitle != null)
            {
                _nameTitle.gameObject.SetActive(true);
                _nameTitle.text = CharacterProgressionModel.CharacterName;
            }
        }

        // ==================== 클릭 경로 3: 전역 폴링 ====================

        private void TickGlobalPointer()
        {
            if (_buttonService == null || _panel == null) return;

            // 드래그 중에만 폴링 간격을 없앤다 — 20Hz로 창을 끌면 커서에서 창이 뚝뚝 끊겨 떨어진다.
            // 평소에는 예전 그대로 ClickPollInterval(0.05초)로 눌러 둔다(하루 종일 켜져 있는 앱이다).
            if (!_draggingPanel && _carouselSection < 0)
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
            // 끄는 중에는 카드를 다시 칠하지 않는다(패널 이동도, 캐러셀 밀기도 마찬가지다).
            if (hasCursor && !_draggingPanel && !_carouselMoved) UpdateHover(cursor);

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            ProcessPointer(left, cursor, hasCursor);
        }

        /// <summary>
        /// 실제 입력과 테스트가 <b>공유하는</b> 포인터 처리(InfoGearIconWidget.ProcessPointer와 같은 관례).
        /// 누름 = 타이틀바 드래그 시작 또는 클릭 처리, 누른 채 이동 = 창 이동, 뗌 = 드래그 종료.
        /// </summary>
        private void ProcessPointer(bool buttonDown, Vector2 cursor, bool hasCursor)
        {
            bool prev = _leftPrev;
            if (!_leftInitialized)
            {
                // 창을 여는 그 클릭이 곧바로 카드 클릭/드래그로 오인되지 않게 첫 표본은 버린다.
                _leftInitialized = true;
                _leftPrev = buttonDown;
                return;
            }
            _leftPrev = buttonDown;

            if (buttonDown && !prev)
            {
                if (!hasCursor) return;
                if (TryBeginPanelDrag(cursor)) return;   // 타이틀바를 잡았으면 클릭 처리로 넘기지 않는다.

                // 캐러셀은 <b>잡아만 둔다</b> — 누름을 삼키지 않는다. 삼키면 카드를 한 번 눌러
                // 고르는 것 자체가 불가능해진다(대부분의 누름은 드래그가 아니라 클릭이다).
                ArmCarouselDrag(cursor);
                FeedClick(cursor);
                return;
            }
            if (buttonDown)
            {
                if (!hasCursor) return;
                if (_draggingPanel) DragPanelTo(cursor);
                else DragCarouselTo(cursor);
                return;
            }
            if (!buttonDown && prev)
            {
                ResolvePendingEquip(cursor, hasCursor);
                EndCarouselDrag();
                EndPanelDrag();
            }
        }

        // ==================== 가로 카드 캐러셀 (2026-09-01) ====================
        //
        // 배치·클램프·휠은 uGUI <see cref="ScrollRect"/>가 한다. 그런데 이 창의 <b>실제</b> 클릭 경로는
        // 전역 폴링이다(uGUI 이벤트는 앱이 활성화된 뒤에만 도착한다 — 타이틀바 드래그를 폴링으로 짠
        // 것과 같은 사정). 그래서 드래그도 한 벌 더 있다.
        //
        // 두 경로가 <b>싸우지 않는</b> 이유: 아래는 "잡은 순간의 content.x + 커서 이동량"이라는
        // <b>절대값</b> 공식이다. ScrollRect의 드래그도 같은 형태(시작 위치 + 이동량)이고 클램프도
        // 같으므로, 둘이 동시에 돌아도 계산 결과가 같다(더해지지 않는다). 그래서 관성(inertia)을
        // 끄고 MovementType을 Clamped로 둔다 — 탄성/감속이 붙는 순간 그 등식이 깨진다.

        private void ArmCarouselDrag(Vector2 cursor)
        {
            _carouselSection = -1;
            _carouselMoved = false;
            if (_tab == Tab.Inventory) return;

            int visible = SectionCountForTab(_tab);
            for (int s = 0; s < visible; s++)
            {
                SectionView view = _sections[s];
                if (view == null || view.Row == null || view.Content == null) continue;
                if (!ContainsScreenPoint(view.RowRect, cursor)) continue;

                _carouselSection = s;
                _carouselGrabScreenX = cursor.x;
                _carouselStartContentX = view.Content.anchoredPosition.x;
                return;
            }
        }

        private void DragCarouselTo(Vector2 cursor)
        {
            if (_carouselSection < 0) return;
            SectionView view = _sections[_carouselSection];
            if (view == null || view.Content == null || view.Row == null) return;

            float delta = (cursor.x - _carouselGrabScreenX) / CanvasScale();
            if (!_carouselMoved && Mathf.Abs(delta) < CarouselDragThresholdPoints) return;
            _carouselMoved = true;
            _lastCarouselMoveTime = Time.unscaledTime;

            Vector2 p = view.Content.anchoredPosition;
            p.x = Mathf.Clamp(_carouselStartContentX + delta, -MaxCarouselScroll(view), 0f);
            view.Content.anchoredPosition = p;
        }

        private void EndCarouselDrag()
        {
            _carouselSection = -1;
            _carouselMoved = false;
        }

        /// <summary>content가 왼쪽으로 밀려날 수 있는 최대치(양수). 카드가 뷰포트를 넘지 않으면 0이다.</summary>
        private static float MaxCarouselScroll(SectionView view)
        {
            if (view == null || view.Content == null || view.Row == null || view.Row.viewport == null) return 0f;
            return Mathf.Max(0f, view.Content.rect.width - view.Row.viewport.rect.width);
        }

        /// <summary>누름 때 보류해 둔 카드 착용을 <b>뗄 때</b> 확정한다. 미는 동안 손가락 아래로 지나간
        /// 카드가 눌리지 않도록, 밀었으면 취소하고 커서가 그 버튼 위에 남아 있을 때만 실행한다.</summary>
        private void ResolvePendingEquip(Vector2 cursor, bool hasCursor)
        {
            int pending = _pendingEquipCard;
            _pendingEquipCard = -1;
            if (pending < 0 || _carouselMoved || !hasCursor) return;

            ItemCard card = CardAt(pending);
            if (card == null || !card.Rect.gameObject.activeInHierarchy) return;
            if (!ContainsScreenPoint(card.ActionRect, cursor)) return;
            if (TryClaimAction("equip" + pending)) OnCardEquipClicked(pending);
        }

        /// <summary>방금 캐러셀을 민 직후인가 — uGUI <see cref="Button.onClick"/>(뗄 때 발동)이
        /// 스크롤의 마지막 손짓을 클릭으로 오인하지 않게 하는 유일한 관문.</summary>
        private bool SuppressedByCarousel()
            => Time.unscaledTime - _lastCarouselMoveTime < CarouselClickSuppressSeconds;

        // ==================== 타이틀바 드래그 (2026-08-30 — 33-7-7 결정의 일부 번복) ====================
        //
        // 33-7-7/34-7은 "화면 중앙 고정 모달"로 확정했고 드래그 코드는 처음부터 <b>없었다</b>(버그가
        // 아니라 미구현이었다). 사용자가 "끌면 옮겨져야 하는데 고정돼 있다"고 해서 리더가 뒤집었다 —
        // <b>열릴 때는 여전히 화면 중앙</b>에서 시작하고, 타이틀바를 잡은 동안만 옮길 수 있다.
        // 옮긴 자리는 기억하지 않는다(다음에 열면 다시 중앙 — "열면 중앙" 규칙을 그대로 지킨다).
        // 클릭 경로가 전역 폴링인 것과 같은 이유로 드래그도 전역 폴링을 쓴다(uGUI 이벤트는 앱이
        // 활성화된 뒤에만 도착한다 — 이 앱은 그 전제를 둘 수 없다).

        private bool TryBeginPanelDrag(Vector2 cursor)
        {
            if (_titleBarRect == null || _panel == null) return false;
            if (!RectContainsScreenPoint(_titleBarRect, cursor)) return false;
            if (RectContainsScreenPoint(_closeRect, cursor)) return false;   // [✕]는 버튼이지 손잡이가 아니다.
            if (RectContainsScreenPoint(_settingsRect, cursor)) return false; // [설정]도 마찬가지.

            // 잡은 지점과 창 중심의 차이를 기억한다 — 드래그가 시작될 때 창이 커서로 순간이동하지 않게.
            _dragGrabOffsetPoints = _panel.anchoredPosition - ScreenToPanelPoints(cursor, CanvasScale());
            _dragStartOffsetPoints = _panel.anchoredPosition;
            _draggingPanel = true;
            return true;
        }

        private void DragPanelTo(Vector2 cursor)
        {
            if (_panel == null) return;
            float sf = CanvasScale();
            _panel.anchoredPosition = ClampPanelPosition(ScreenToPanelPoints(cursor, sf) + _dragGrabOffsetPoints, sf);
        }

        private void EndPanelDrag()
        {
            if (!_draggingPanel) return;
            _draggingPanel = false;
            Vector2 p = _panel != null ? _panel.anchoredPosition : Vector2.zero;
            if ((p - _dragStartOffsetPoints).sqrMagnitude < 0.25f) return;   // 제자리 클릭은 이동이 아니다.
            Debug.Log($"[정보창] 이동 완료 — 화면 중앙에서 ({p.x:F0}, {p.y:F0})pt 옮긴 자리입니다. " +
                "다시 열면 중앙에서 시작합니다.");
        }

        /// <summary>화면 중앙을 원점으로 하는 캔버스 좌표(패널 anchoredPosition과 <b>같은 계</b>).</summary>
        private static Vector2 ScreenToPanelPoints(Vector2 cursorUnityScreen, float scaleFactor)
            => new Vector2((cursorUnityScreen.x - Screen.width * 0.5f) / scaleFactor,
                           (cursorUnityScreen.y - Screen.height * 0.5f) / scaleFactor);

        private float CanvasScale()
        {
            float sf = _scaler != null ? _scaler.scaleFactor : 1f;
            return sf > 0f ? sf : 1f;
        }

        private void ResetPanelToCenter()
        {
            _draggingPanel = false;
            if (_panel != null) _panel.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// 테스트 전용 진입점 — 실제 입력과 <b>같은 처리 경로</b>에 커서를 먹인다(PlayMode는 진짜
        /// 전역 클릭을 만들 수 없다 — PopoverPanel.FeedClickForTests와 같은 사정).
        /// </summary>
        public void FeedClickForTests(Vector2 cursorUnityScreen)
        {
            if (_open) FeedClick(cursorUnityScreen);
        }

        /// <summary>테스트 전용 — 버튼 상태와 커서를 <b>실제 입력과 같은 처리 경로</b>에 먹인다
        /// (드래그는 누름/이동/뗌의 연속이라 단발 클릭 진입점으로는 재현할 수 없다).</summary>
        public void FeedPointerForTests(bool buttonDown, Vector2 cursorUnityScreen)
        {
            if (_open) ProcessPointer(buttonDown, cursorUnityScreen, hasCursor: true);
        }

        /// <summary>진단/테스트 전용 — 창의 현재 위치(화면 중앙 원점, 캔버스 포인트).</summary>
        public Vector2 PanelOffsetPoints => _panel != null ? _panel.anchoredPosition : Vector2.zero;

        /// <summary>진단/테스트 전용 — 창의 현재 크기(캔버스 포인트).</summary>
        public Vector2 PanelSizePoints => _panel != null ? _panel.sizeDelta : Vector2.zero;

        /// <summary>진단/테스트 전용 — 지금 타이틀바를 잡고 끌고 있는가.</summary>
        public bool IsDraggingPanel => _draggingPanel;

        /// <summary>진단/테스트 전용 — 드래그 손잡이(타이틀바)의 화면 사각형.</summary>
        public Rect TitleBarScreenRect => RawScreenRectOf(_titleBarRect);

        /// <summary>진단/테스트 전용 — 창 전체의 화면 사각형("화면 안에 들어왔는가"를 재는 창구).</summary>
        public Rect PanelScreenRect => RawScreenRectOf(_panel);

        /// <summary>헤더의 [설정] 칩 화면 사각형 — 설정창의 주 진입점이자, 설정창을 닫았을 때
        /// 이 창으로 <b>돌아오는지</b>를 검증하는 테스트가 실제로 누를 자리다(M8).</summary>
        public Rect SettingsChipScreenRect => RawScreenRectOf(_settingsRect);

        private static Rect RawScreenRectOf(RectTransform rt)
        {
            if (rt == null || rt.gameObject == null || !rt.gameObject.activeInHierarchy) return new Rect();
            rt.GetWorldCorners(_corners);
            return Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
        }

        private void FeedClick(Vector2 cursor)
        {
            if (ContainsScreenPoint(_settingsRect, cursor))
            {
                if (TryClaimAction("settings")) OpenSettings("정보창 헤더 [설정]");
                return;
            }

            if (ContainsScreenPoint(_closeRect, cursor))
            {
                if (TryClaimAction("close")) Close("[✕] 클릭");
                return;
            }

            if (ContainsScreenPoint(_nameRect, cursor) && !_editingName)
            {
                if (TryClaimAction("nameEdit")) BeginNameEdit();
                return;
            }
            for (int i = 0; i < _inkRects.Length; i++)
            {
                if (!ContainsScreenPoint(_inkRects[i], cursor)) continue;
                if (TryClaimAction("ink" + i)) OnInkSwatchClicked(i == 1);
                return;
            }

            for (int i = 0; i < _tabRects.Length; i++)
            {
                if (!ContainsScreenPoint(_tabRects[i], cursor)) continue;
                if (TryClaimAction("tab" + i)) OnTabClicked((Tab)i);
                return;
            }

            if (_tab == Tab.Inventory)
            {
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

            for (int i = 0; i < _cards.Length; i++)
            {
                ItemCard card = _cards[i];
                if (card == null || !card.Rect.gameObject.activeInHierarchy) continue;
                if (ContainsScreenPoint(card.ActionRect, cursor))
                {
                    // 누른 순간에는 아무 일도 하지 않는다 — 착용은 <b>뗄 때</b> 확정한다
                    // (그 사이에 카드를 밀었다면 그건 스크롤이다. _pendingEquipCard 문서 참고).
                    _pendingEquipCard = i;
                    return;
                }
                if (!ContainsScreenPoint(card.Rect, cursor)) continue;
                if (TryClaimAction("card" + i)) OnCardClicked(i);
                return;
            }
            if (ContainsScreenPoint(_actionRect, cursor))
            {
                if (TryClaimAction("action")) OnActionClicked();
                return;
            }

            // ★ 33-7-9의 세 번째 탈출구 — <b>창 밖 클릭</b>(2026-08-30 신설). 여기까지 왔다는 것은 어떤
            // 컨트롤에도 맞지 않았다는 뜻이라, 패널 안이면 "빈 자리"고 밖이면 닫는다. 이게 없어서 실제
            // 탈출구가 [✕] 하나뿐이었다(ESC는 클릭관통 긴급 해제에 선점 — 클래스 문서 참고).
            if (RectContainsScreenPoint(_panel, cursor)) return;

            // 톱니/부채꼴은 예외다. 그쪽도 같은 클릭에 반응하므로(톱니는 뗀 순간 창을 닫는다) 여기서
            // 먼저 닫으면 한 번의 클릭이 두 번 처리된다.
            if (IsOnGearSurface(cursor)) return;
            if (TryClaimAction("outside")) Close("창 밖 클릭");
        }

        /// <summary>커서가 톱니 아이콘이나 펼쳐진 부채꼴 위인가 — "창 밖 클릭"의 유일한 예외.
        /// 배타 규칙(<see cref="CloseOverlappingSurfaces"/>) 덕에 부채꼴이 이 창과 함께 떠 있을 일은
        /// 없지만, 톱니는 창이 열려 있는 동안에도 항상 화면에 있다.</summary>
        private bool IsOnGearSurface(Vector2 cursor)
        {
            if (_gear == null) _gear = GetComponent<InfoGearIconWidget>();
            if (_gear != null && _gear.IsIconVisible && _gear.InteractiveScreenRect.Contains(cursor)) return true;
            if (_menu == null) _menu = GetComponent<GearRadialMenuWidget>();
            return _menu != null && _menu.ContainsCursor(cursor);
        }

        /// <summary>
        /// 카드 hover(33-7-3). <b>있으면 좋은 것이지 필수가 아니다</b> — 이 앱의 uGUI 입력은 창을 클릭해
        /// 앱이 활성화된 뒤에만 정상 도착하고, 전역 커서 조회도 플랫폼에 따라 없을 수 있다.
        /// hover가 한 프레임도 오지 않아도 선택/착용은 클릭만으로 온전히 동작한다.
        /// 바뀐 프레임에만 테두리 두 장을 다시 칠한다(문자열/할당 없음).
        /// </summary>
        private void UpdateHover(Vector2 cursor)
        {
            // 목록 탭에는 카드가 없다. 남아 있던 hover는 지우기만 하면 된다 — 카드가 숨겨져 있어
            // 다시 칠할 필요가 없고, 탭을 되돌아오면 RefreshCards가 -1 상태로 전부 다시 칠한다.
            if (_tab == Tab.Inventory)
            {
                _hoveredCard = -1;
                return;
            }

            int found = -1;
            for (int i = 0; i < _cards.Length; i++)
            {
                ItemCard card = _cards[i];
                if (card == null || !card.Rect.gameObject.activeInHierarchy) continue;
                if (!ContainsScreenPoint(card.Rect, cursor)) continue;
                found = i;
                break;
            }
            if (found == _hoveredCard) return;

            int previous = _hoveredCard;
            _hoveredCard = found;
            RestyleCard(previous);
            RestyleCard(found);
        }

        private void RestyleCard(int index)
        {
            ItemCard card = CardAt(index);
            if (card == null || !card.Rect.gameObject.activeSelf) return;
            ApplyCardStyle(card, SectionSlot(_tab, card.Section), card.Item, IconSetForTab(_tab));
        }

        private bool TryClaimAction(string key)
        {
            if (_lastActionKey == key && Time.unscaledTime - _lastActionTime < ActionDedupSeconds) return false;
            _lastActionKey = key;
            _lastActionTime = Time.unscaledTime;
            return true;
        }

        /// <summary>
        /// ScreenSpaceOverlay 캔버스에서는 RectTransform의 월드 좌표가 곧 스크린 픽셀 좌표다
        /// (AppControlDirector.HitTestMenuRow / TodoPostItWidget과 같은 전제).
        ///
        /// ★ 2026-08-30(R2 M3): <b>마스크에 잘린 자리는 눌리지 않는다.</b> 세로가 짧은 화면에서
        /// <see cref="ClampPanelToScreen"/>이 패널을 줄이면 본문 아래쪽([착용] 버튼 포함)이
        /// <see cref="RectMask2D"/>에 잘려 <b>화면에서 사라진다</b>. 그런데 이 전역 폴링 경로는
        /// 마스크를 모르는 순수 사각형 판정이라, 예전에는 보이지도 않는 버튼이 그대로 눌렸다 —
        /// 이 프로젝트가 "최악의 형태"라고 부르는 패턴이다(안 보이는데 클릭은 먹는 UI).
        /// uGUI 배선 쪽은 <see cref="RectMask2D"/>가 <c>ICanvasRaycastFilter</c>라 원래부터 막혀 있었고,
        /// 이 함수만 빠져 있었다. 부분적으로 잘린 컨트롤은 <b>보이는 부분만</b> 계속 눌린다.
        /// </summary>
        private bool ContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (!RectContainsScreenPoint(rt, screenPoint)) return false;
            return IsUnclipped(rt, screenPoint);
        }

        /// <summary>마스크를 <b>보지 않는</b> 날 사각형 판정(마스크 사각형 자신을 잴 때 쓴다).</summary>
        private static bool RectContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(_corners);
            return screenPoint.x >= _corners[0].x && screenPoint.x <= _corners[2].x &&
                   screenPoint.y >= _corners[0].y && screenPoint.y <= _corners[2].y;
        }

        /// <summary>이 지점이 조상 마스크 <b>전부</b>의 안쪽인가. 마스크 목록은 빌드 때 한 번만
        /// 모으고(폴링 경로 할당 0), 조상 여부는 <see cref="Transform.IsChildOf"/>로 확인한다.</summary>
        private bool IsUnclipped(RectTransform rt, Vector2 screenPoint)
        {
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

        /// <summary>이 부품이 마스크에 잘리고 <b>남은</b> 화면 사각형(전부 잘리면 넓이 0).
        /// 진단/테스트 전용 — "보이는 만큼만 눌린다"를 숫자로 확인하는 창구다.</summary>
        public Rect VisibleScreenRectOf(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return new Rect();
            rt.GetWorldCorners(_corners);
            float xMin = _corners[0].x, yMin = _corners[0].y, xMax = _corners[2].x, yMax = _corners[2].y;

            if (_masks != null)
            {
                for (int i = 0; i < _masks.Length; i++)
                {
                    RectMask2D mask = _masks[i];
                    if (mask == null || !mask.isActiveAndEnabled) continue;
                    RectTransform maskRect = mask.rectTransform;
                    if (maskRect == null || maskRect == rt || !rt.IsChildOf(maskRect)) continue;

                    maskRect.GetWorldCorners(_corners);
                    xMin = Mathf.Max(xMin, _corners[0].x);
                    yMin = Mathf.Max(yMin, _corners[0].y);
                    xMax = Mathf.Min(xMax, _corners[2].x);
                    yMax = Mathf.Min(yMax, _corners[2].y);
                }
            }
            if (xMax <= xMin || yMax <= yMin) return new Rect();
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>[착용]/[해제] 버튼이 지금 화면에 보이는 넓이 비율(0 = 통째로 잘림). 진단/테스트용.</summary>
        public float ActionButtonVisibleFraction
        {
            get
            {
                if (_actionRect == null || !_actionRect.gameObject.activeInHierarchy) return 0f;
                _actionRect.GetWorldCorners(_corners);
                float full = (_corners[2].x - _corners[0].x) * (_corners[2].y - _corners[0].y);
                if (full <= 0f) return 0f;
                Rect visible = VisibleScreenRectOf(_actionRect);
                return Mathf.Clamp01(visible.width * visible.height / full);
            }
        }

        /// <summary>[착용] 버튼의 <b>잘리기 전</b> 화면 사각형 — 테스트가 "안 보이는 자리"를 정확히
        /// 눌러 보기 위해 필요하다(좌표를 손으로 적으면 레이아웃이 바뀔 때 엉뚱한 곳을 누른다).</summary>
        public Rect ActionButtonRawScreenRect
        {
            get
            {
                if (_actionRect == null || !_actionRect.gameObject.activeInHierarchy) return new Rect();
                _actionRect.GetWorldCorners(_corners);
                return Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
            }
        }

        /// <summary>지금 이 지점을 누르면 [착용] 버튼이 반응하는가(전역 폴링과 <b>같은</b> 판정).</summary>
        public bool IsActionButtonHittableAt(Vector2 cursorUnityScreen)
            => ContainsScreenPoint(_actionRect, cursorUnityScreen);

        // ==================== 진단/테스트 전용 — 카드 캐러셀 ====================
        //
        // 좌표를 테스트가 손으로 적으면 레이아웃이 바뀔 때 엉뚱한 곳을 누르게 된다([착용] 버튼 쪽에서
        // 이미 배운 것). 그래서 <b>지금 화면에 있는 사각형</b>을 그대로 내준다.

        /// <summary>지금 존재하는 카드 수(탭과 무관한 <b>풀</b> 크기).</summary>
        public int CardCountForTests => _cards.Length;

        /// <summary>이 카드가 지금 탭에서 실제로 쓰이고 있는가(카테고리마다 개수가 다르다).</summary>
        public bool IsCardVisibleForTests(int index)
        {
            ItemCard card = CardAt(index);
            return card != null && card.Rect != null && card.Rect.gameObject.activeInHierarchy;
        }

        public int CardSectionForTests(int index) => CardAt(index)?.Section ?? -1;

        public int CardItemForTests(int index) => CardAt(index)?.Item ?? -1;

        /// <summary>카드의 <b>잘리기 전</b> 화면 사각형. 캐러셀 밖으로 밀려난 카드도 값이 나온다 —
        /// "보이지 않는데 눌리는가"를 재려면 그 자리를 알아야 한다.</summary>
        public Rect CardRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Rect);

        /// <summary>카드가 캐러셀 마스크에 <b>잘리고 남은</b> 화면 사각형(전부 잘리면 넓이 0).
        /// "반쯤 걸친 카드가 있는가" — 즉 이 창의 유일한 발견 단서(<see cref="CarouselViewportWidth"/>)가
        /// 실제로 화면에 있는가를 회귀 테스트가 숫자로 확인하는 창구다.</summary>
        public Rect CardVisibleScreenRect(int index) => VisibleScreenRectOf(CardAt(index)?.Rect);

        /// <summary>카드 하단 [착용]/[해제] 버튼의 잘리기 전 화면 사각형.</summary>
        public Rect CardEquipButtonRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.ActionRect);

        /// <summary>지금 이 지점을 누르면 그 카드의 [착용] 버튼이 반응하는가(마스크까지 본 판정).</summary>
        public bool IsCardEquipButtonHittableAt(int index, Vector2 cursorUnityScreen)
            => ContainsScreenPoint(CardAt(index)?.ActionRect, cursorUnityScreen);

        /// <summary>캐러셀 한 줄(잡고 미는 자리)의 화면 사각형.</summary>
        public Rect CarouselRowScreenRect(int section)
            => RawScreenRectOf(section >= 0 && section < _sections.Length ? _sections[section]?.RowRect : null);

        /// <summary>지금 밀려 있는 양(캔버스 포인트, 왼쪽으로 밀면 음수).</summary>
        public float CarouselOffsetPoints(int section)
        {
            SectionView view = section >= 0 && section < _sections.Length ? _sections[section] : null;
            return view != null && view.Content != null ? view.Content.anchoredPosition.x : 0f;
        }

        /// <summary>이 카테고리에서 밀 수 있는 최대치(양수). 0이면 카드가 화면에 다 들어온다는 뜻이다.</summary>
        public float CarouselMaxScrollPoints(int section)
            => MaxCarouselScroll(section >= 0 && section < _sections.Length ? _sections[section] : null);

        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
            ClampPanelToScreen(target);
            EnsurePortraitTexture(force: false);
        }

        /// <summary>이보다 더 줄이면 좌측 컬럼(244)조차 담지 못한다 — 세로 하한과 같은 값으로 맞췄다.</summary>
        private const float MinPanelWidth = 320f;
        private const float MinPanelHeight = 320f;

        /// <summary>작은 화면에서 창이 화면 밖으로 나가지 않게 <b>가로·세로 모두</b> 줄인다.
        /// 예전에는 세로만 줄이고 폭은 항상 880이라 640폭 화면에서 좌우로 각각 120pt씩 흘러나갔다
        /// (2026-08-30 디버거 실측). 잘리는 것은 본문 오른쪽/아래쪽이고 <see cref="RectMask2D"/>가
        /// 패널 밖으로 삐져나오는 그림을 막는다(타이틀바의 [✕]/구분선은 패널 폭을 따라가게 앵커를
        /// 오른쪽/양끝에 걸어 뒀다 — 안 그러면 그 둘만 창 밖에 떠 있게 된다).
        /// 33-7-9가 적어 둔 "[▲][▼] 2섹션 페이지 모드" 폴백은 아직 없다.
        /// 크기를 줄인 뒤에는 드래그로 옮겨 둔 자리도 다시 화면 안으로 끌어들인다.</summary>
        private void ClampPanelToScreen(float scaleFactor)
        {
            if (_panel == null || scaleFactor <= 0f) return;
            float height = Mathf.Min(PanelHeight, Mathf.Max(MinPanelHeight, Screen.height / scaleFactor - ScreenMargin * 2f));
            float width = Mathf.Min(PanelWidth, Mathf.Max(MinPanelWidth, Screen.width / scaleFactor - ScreenMargin * 2f));
            if (!Mathf.Approximately(_panel.sizeDelta.x, width) || !Mathf.Approximately(_panel.sizeDelta.y, height))
            {
                _panel.sizeDelta = new Vector2(width, height);
                SyncActionReachability();
            }

            Vector2 clamped = ClampPanelPosition(_panel.anchoredPosition, scaleFactor);
            if (clamped != _panel.anchoredPosition) _panel.anchoredPosition = clamped;
        }

        /// <summary>창 중심이 화면 밖으로 나가지 않는 범위로 자른다 — 드래그와 화면 크기 변화가
        /// <b>같은 규칙</b>을 쓴다. 좌표계는 화면 중앙 원점이고, 창이 화면만큼 커지면 이동량은 0이 된다.</summary>
        private Vector2 ClampPanelPosition(Vector2 desired, float scaleFactor)
        {
            if (_panel == null || scaleFactor <= 0f) return desired;
            float sf = scaleFactor;
            Vector2 size = _panel.sizeDelta;
            float maxX = Mathf.Max(0f, (Screen.width / sf - size.x) * 0.5f - ScreenMargin);
            float maxY = Mathf.Max(0f, (Screen.height / sf - size.y) * 0.5f - ScreenMargin);
            return new Vector2(Mathf.Clamp(desired.x, -maxX, maxX), Mathf.Clamp(desired.y, -maxY, maxY));
        }

        /// <summary>
        /// 화면이 낮아 [착용]/[해제] 버튼이 통째로 잘리면 <b>한 번만</b> 경고한다. 클릭은 이미
        /// <see cref="ContainsScreenPoint"/>가 막으므로 "안 보이는데 눌린다"는 없어졌지만, 그 화면에서는
        /// 아이템을 갈아입을 수단 자체가 사라진다는 사실은 조용히 넘길 일이 아니다(33-7-9 페이지 폴백 미구현).
        /// </summary>
        private void SyncActionReachability()
        {
            if (_actionRect == null) return;
            bool unreachable = _actionRect.gameObject.activeInHierarchy && ActionButtonVisibleFraction <= 0f;
            if (unreachable == _actionUnreachable) return;
            _actionUnreachable = unreachable;
            if (!unreachable) return;

            Debug.LogWarning("[정보창] 화면 세로가 짧아 상세 패널의 [착용] 버튼이 완전히 가려졌습니다 — " +
                             "그 자리를 눌러도 반응하지 않습니다(보이지 않는 것은 눌리지 않는다). " +
                             "33-7-9의 [▲][▼] 페이지 폴백이 들어오기 전까지는 창을 띄울 세로 공간이 더 필요합니다.");
        }

        /// <summary>창이 보이는 동안만 창 사각형을 덮는 히트테스트용 콜라이더를 켠다(TodoPostItWidget과
        /// 같은 관례 — isTrigger라 캐릭터 물리에는 전혀 관여하지 않는다).</summary>
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

            // ★ 2026-08-30: 여기 넘겨야 하는 것은 "캔버스 유닛 -> Unity 픽셀" 배율이다(= 이 창의
            //   CanvasScaler.scaleFactor). 예전에는 그 역수인 ResolveDpiScale()을 넘겨 Retina에서
            //   RT가 표시 크기의 1/2로 만들어졌고, 그것이 사용자가 신고한 "픽셀이 다 깨져보임"의
            //   원인이었다(CharacterPortraitStage.TryEnsureTexture 문서에 실측 유도 전문).
            float pixelsPerCanvasUnit = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!force && Mathf.Approximately(pixelsPerCanvasUnit, _lastDpiScale) && _stage.HasTexture) return;
            _lastDpiScale = pixelsPerCanvasUnit;

            Rect rect = _portraitImage.rectTransform.rect;
            Vector2 design = PortraitContentSize;
            float w = rect.width > 1f ? rect.width : design.x;
            float h = rect.height > 1f ? rect.height : design.y;

            bool ok = _stage.TryEnsureTexture(w, h, pixelsPerCanvasUnit);
            _portraitImage.enabled = ok;
            if (ok) _portraitImage.texture = _stage.Texture;
            if (_portraitFallback != null) _portraitFallback.gameObject.SetActive(!ok);
        }

        // ==================== UI 구성(런타임 생성 — 씬/프리팹 수동 배선 없이도 동작) ====================

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("CharacterInfoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            // ★★ 씬 루트에 둔다(캐릭터의 자식이 아니다) — 아래 차단막과 같은 이유에 더해,
            // 캐릭터 자손으로 두면 이 캔버스 안의 UI 이름이 <b>이름으로 캐릭터 파츠를 찾는 코드</b>
            // (StickmanPoseAnimator / StickmanMetrics / EyeController / DialogueBubbleRenderer /
            // CharacterAccessoryRenderer)에 걸릴 수 있다. 2026-08-30에 부채꼴 메뉴의 "Head"라는 UI
            // 자손이 정확히 그 사고를 냈다(캐릭터 머리·몸통이 영영 안 움직임). 정리는 OnDestroy가 책임진다.
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrderTopMost;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();

            // ★★ 2026-08-31 회귀 수정 — 사용자 신고 "창이 여러 개로 겹쳐 보임"(스크린샷: 이 창 뒤로
            //   날씨 위젯의 파란 그라데이션과 24°가 그대로 읽힘).
            //
            //   예전 구조는 이랬다:  InfoPanel(Image, α0.96)
            //                          └ PanelShadow(검정 α0.55) / PanelShadowAmbient(검정 α0.28)
            //   그런데 uGUI는 <b>부모 Graphic을 자식보다 먼저</b> 그린다. SetAsFirstSibling()은 형제
            //   순서만 정할 뿐이라, 두 그림자는 <b>패널 본체 위</b>에 얹혀 있었다. 그리고 투명 오버레이의
            //   프레임버퍼 알파는 UI/Default의 `Blend SrcAlpha OneMinusSrcAlpha`가 알파 채널에도 그대로
            //   적용되어 <b>겹을 쌓을수록 내려간다</b>(UiChrome 파일 머리 "알파 채널의 법칙" 참고):
            //       0(빈 화면) → 0.9216(본체 α0.96) → 0.7172(키 그림자) → <b>0.5948</b>(앰비언트)
            //   = 유저의 데스크톱이 <b>40.5%</b> 비쳐 들었다. 어두운 팔레트(34-1)에서는 가릴 밝기가
            //   없어 체감 밝기가 549% 튀었고, 그래서 밝은 팔레트 시절에는 같은 결함이 보이지 않았다.
            //
            //   이제 패널은 <b>그림 없는 컨테이너</b>이고 [그림자 → 본체(α1) → 보더]가 형제로 놓인다.
            //   _panel이 여전히 "움직이고 크기가 정해지는 사각형"이라는 계약은 그대로다 —
            //   드래그/클램프/히트테스트/차단막 코드는 한 줄도 바뀌지 않는다.
            _panel = UiChrome.AddOpaquePanel(canvasGo.transform, "InfoPanel", UiChrome.RadiusPanel,
                18f, new Vector2(0f, -18f), out Image panelImage);
            // 33-7-7: 화면 중앙 모달. 배경 딤은 깔지 않는다(클래스 문서 참고).
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            // 창 바탕을 눌러도 뒤(데스크톱)로 새지 않아야 한다 — 예전 InfoPanel Image가 하던 역할.
            panelImage.raycastTarget = true;

            BuildTitleBar(_panel);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(RectMask2D));
            bodyGo.transform.SetParent(_panel, false);
            var body = bodyGo.GetComponent<RectTransform>();
            body.anchorMin = new Vector2(0f, 0f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.offsetMin = Vector2.zero;
            body.offsetMax = new Vector2(0f, -TitleHeight);
            // 작은 화면에서 패널이 짧아져도 내용이 패널 밖으로 새어 나가지 않게 한다(ClampPanelToScreen).

            BuildLeftColumn(body);
            RectTransform right = BuildRightColumn(body);
            BuildTabs(right);
            BuildSectionPage(right);
            BuildInventoryPage(right);
            ApplyTabVisibility();

            // 클릭관통 차단막 — 씬 루트에 둔다(캐릭터의 자식으로 두면 캐릭터가 걷거나 랙돌로 회전할 때
            // 이 사각형까지 함께 돌아가 창의 화면 사각형과 어긋난다. TodoPostItWidget과 같은 이유).
            // 히트테스트가 쓸 마스크 목록은 여기서 한 번만 모은다(폴링 경로에서 탐색하지 않는다).
            _masks = _panel.GetComponentsInChildren<RectMask2D>(true);

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
            _titleBarRect = rt;   // 드래그 손잡이 — 여기를 잡은 동안만 창이 움직인다.

            Text title = Label(barGo.transform, "Title", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, 16f, -13f, 200f, 14f, "내 책상 동료", bold: true);
            title.raycastTarget = false;

            Image divider = UiChrome.AddSurface(parent, "TitleDivider", UiChrome.CardBorder, 2);
            // 폭을 못 박으면 좁은 화면에서 패널이 줄었을 때(ClampPanelToScreen) 구분선만 밖으로 삐져나온다.
            RectTransform dividerRect = divider.rectTransform;
            dividerRect.anchorMin = new Vector2(0f, 1f);
            dividerRect.anchorMax = new Vector2(1f, 1f);
            dividerRect.pivot = new Vector2(0.5f, 1f);
            dividerRect.offsetMin = new Vector2(0f, -TitleHeight);
            dividerRect.offsetMax = new Vector2(0f, -(TitleHeight - 1f));
            divider.raycastTarget = false;

            // 스펙의 "ESC" 힌트 자리에 [✕]를 둔다 — 이유는 클래스 문서 참고(ESC는 이미 클릭관통
            // 긴급 해제에 묶여 있어서, 창 닫기를 겹치면 보이지 않는 부수효과가 생긴다).
            Image closeSurface = UiChrome.AddSurface(barGo.transform, "CloseButton", UiChrome.CardSurfaceMuted, UiChrome.RadiusChip);
            _closeRect = closeSurface.rectTransform;
            // 오른쪽 끝에 건다(고정 x였다면 좁은 화면에서 패널이 줄 때 [✕]만 창 밖에 남는다).
            // 880 폭에서의 결과 좌표는 예전과 같다(오른쪽에서 16, 위에서 8).
            _closeRect.anchorMin = _closeRect.anchorMax = _closeRect.pivot = new Vector2(1f, 1f);
            _closeRect.sizeDelta = new Vector2(24f, 24f);
            _closeRect.anchoredPosition = new Vector2(-16f, -8f);
            UiChrome.AddOutline(_closeRect, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
            Text closeLabel = UiChrome.AddText(_closeRect, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            UiChrome.Stretch(closeLabel.rectTransform);
            closeLabel.text = "✕";

            var closeButton = closeSurface.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeSurface;
            closeButton.onClick.AddListener(() => { if (TryClaimAction("close")) Close("[✕] 클릭"); });

            // ★ 2026-09-01 — 설정창(35-1)의 <b>주 진입점</b>. docs/UX_FLOW.md 36-11이 우클릭 메뉴 폐지에
            //   맞춰 "정보창 헤더의 작은 톱니"를 주 경로로 승격시켰다. 여기가 그 자리다.
            //   글자를 쓰는 이유: 이 프로젝트의 UI 폰트는 LegacyRuntime.ttf라 톱니 글리프(U+2699)가
            //   있다는 보장이 없고, 없으면 두부(□)가 뜬다. 아이콘을 선으로 그리는 방법도 있지만
            //   24pt 칩 안의 톱니는 결국 읽히지 않는다 — 32-1이 "심볼만 있는 원은 반드시 오독된다"고
            //   적어 둔 그 문제다.
            Image settingsSurface = UiChrome.AddSurface(barGo.transform, "SettingsButton",
                UiChrome.CardSurfaceMuted, UiChrome.RadiusChip);
            _settingsRect = settingsSurface.rectTransform;
            _settingsRect.anchorMin = _settingsRect.anchorMax = _settingsRect.pivot = new Vector2(1f, 1f);
            _settingsRect.sizeDelta = new Vector2(44f, 24f);
            _settingsRect.anchoredPosition = new Vector2(-48f, -8f);
            UiChrome.AddOutline(_settingsRect, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
            // 글자 크기는 [✕]와 <b>같은 등급</b>(FontBody 12)이다 — 앱 전체 설정의 주 진입점이 닫기 버튼보다
            // 작게 그려져 있었다(페르소나 M2). 10pt(FontCaption)는 이 디자인 시스템에서 캡션/카운트 전용
            // 최소 등급이라, 그 자리에 있는 것만으로 "부수적인 것"이라고 말한다.
            Text settingsLabel = UiChrome.AddText(_settingsRect, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(settingsLabel.rectTransform);
            settingsLabel.text = "설정";

            var settingsButton = settingsSurface.gameObject.AddComponent<Button>();
            settingsButton.targetGraphic = settingsSurface;
            settingsButton.onClick.AddListener(() =>
            {
                if (TryClaimAction("settings")) OpenSettings("정보창 헤더 [설정]");
            });
        }

        /// <summary>
        /// 설정창을 연다. <b>이 창을 여기서 닫지 않는다</b> — 배타 규칙의 집행은 <see cref="SettingsWindow.Open"/>
        /// 한 곳에 있다(진입점마다 정리 코드를 흩뿌리면 네 번째 진입점에서 반드시 샌다는, 이 파일이
        /// 이미 한 번 배운 교훈).
        /// </summary>
        private void OpenSettings(string source)
        {
            var settings = GetComponent<SettingsWindow>();
            if (settings == null)
            {
                Debug.LogWarning("[정보창] [설정]을 눌렀지만 SettingsWindow 컴포넌트가 없습니다 — " +
                    "Assets/Editor/SceneBootstrapper.cs의 EnsurePrefabComponents가 이 컴포넌트를 " +
                    "프리팹에 붙이는지 확인하세요(33-9 #10 / 34-9 #10과 같은 함정).");
                return;
            }
            settings.Open(source);
        }

        // -------------------- 좌측 고정 컬럼 --------------------

        private void BuildLeftColumn(RectTransform body)
        {
            var go = new GameObject("LeftColumn", typeof(RectTransform));
            go.transform.SetParent(body, false);
            var left = go.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(left, 0f, 0f, LeftWidth, BodyHeight);

            Image columnDivider = UiChrome.AddSurface(body, "ColumnDivider", UiChrome.CardBorder, 2);
            UiChrome.PlaceTopLeft(columnDivider.rectTransform, LeftWidth - 1f, 0f, 1f, BodyHeight);
            columnDivider.raycastTarget = false;

            // ---- 이름 블록: 이름(인라인 편집) + 잉크색 스와치 2개 ----
            float swatchRight = LeftPadX + LeftContentWidth;
            float nameWidth = LeftContentWidth - (SwatchSize * 2f + SwatchGap) - UiChrome.Space3;

            _nameTitle = Label(left, "Name", UiChrome.FontDisplay, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                LeftPadX, NameY, nameWidth, 25f, CharacterProgressionModel.CharacterName, bold: true);

            // 이름 글자 자체는 raycastTarget이 아니므로(UiChrome 관례) 클릭을 받을 투명 판을 겹친다.
            Image nameHit = UiChrome.AddSurface(left, "NameHit", Color.clear, UiChrome.RadiusChip);
            _nameRect = nameHit.rectTransform;
            UiChrome.PlaceTopLeft(_nameRect, LeftPadX, NameY, nameWidth, 25f);
            var nameButton = nameHit.gameObject.AddComponent<Button>();
            nameButton.targetGraphic = nameHit;
            nameButton.onClick.AddListener(() => { if (TryClaimAction("nameEdit")) BeginNameEdit(); });

            _nameInput = CreateInputField(left);
            _nameInputRect = _nameInput.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(_nameInputRect, LeftPadX, NameY, nameWidth, 25f);
            _nameInputRect.gameObject.SetActive(false);

            for (int i = 0; i < 2; i++)
            {
                bool white = i == 1;
                float x = swatchRight - (2 - i) * SwatchSize - (1 - i) * SwatchGap;
                var swatchGo = new GameObject(white ? "InkWhite" : "InkBlack", typeof(RectTransform), typeof(Image));
                swatchGo.transform.SetParent(left, false);
                var srt = swatchGo.GetComponent<RectTransform>();
                UiChrome.PlaceTopLeft(srt, x, NameY - 6f, SwatchSize, SwatchSize);

                var fill = swatchGo.GetComponent<Image>();
                fill.sprite = UiChrome.Circle();
                fill.type = Image.Type.Simple;
                fill.color = white ? UiChrome.CardSurface : UiChrome.TextPrimary;

                // 1.5px 링으로 "지금 이 색"을 표시한다(지름 12 기준 비율 = 1.5/12).
                Image ring = UiChrome.AddCircle(srt, "Ring", SwatchSize, UiChrome.PanelBorder, 1.5f);
                ring.raycastTarget = false;

                var button = swatchGo.AddComponent<Button>();
                button.targetGraphic = fill;
                button.onClick.AddListener(() => { if (TryClaimAction("ink" + (white ? 1 : 0))) OnInkSwatchClicked(white); });

                _inkRings[i] = ring;
                _inkRects[i] = srt;
            }

            _rankTitle = Label(left, "RankTitle", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                LeftPadX, SubY, LeftContentWidth, 15f, "Lv.1");

            // ---- 초상화 액자 (33-7-6: 204×196 / 여백 8 / 반지름 8) ----
            _portraitFrame = UiChrome.AddSurface(left, "PortraitFrame",
                CharacterPortraitStage.ResolveBackdropColor(_config), 8);
            UiChrome.PlaceTopLeft(_portraitFrame.rectTransform, LeftPadX, PortraitY, LeftContentWidth, PortraitHeight);
            _portraitFrame.raycastTarget = false;
            _portraitBorder = UiChrome.AddOutline(_portraitFrame.rectTransform, "Border", UiChrome.CardBorder, 8);

            var imageGo = new GameObject("PortraitImage", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(_portraitFrame.transform, false);
            UiChrome.Stretch(imageGo.GetComponent<RectTransform>(), PortraitPadding);
            _portraitImage = imageGo.GetComponent<RawImage>();
            _portraitImage.raycastTarget = false;
            _portraitImage.enabled = false;   // RT가 준비되면 켠다.

            _portraitFallback = UiChrome.AddText(_portraitFrame.rectTransform, "PortraitFallback",
                UiChrome.FontBody, TextAnchor.MiddleCenter, UiChrome.TextTertiary, wrap: true);
            UiChrome.Stretch(_portraitFallback.rectTransform, UiChrome.Space4);
            _portraitFallback.text = "미리보기를 그릴 수 없어요";
            _portraitFallback.gameObject.SetActive(false);

            // ---- 프레즌스 + 게이지 2종 ----
            _presenceText = Label(left, "Presence", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                LeftPadX, PresenceY, LeftContentWidth, 15f, "지금  ·  —");

            _stressFill = BuildGauge(left, "STRESS", StressLabelY, StressTrackY, UiChrome.TextPrimary, out _stressValue);
            _xpFill = BuildGauge(left, "EXP", XpLabelY, XpTrackY, UiChrome.Accent, out _xpValue);

            // ---- 스탯 5행 ----
            Image statsTop = UiChrome.AddSurface(left, "StatsTopLine", UiChrome.Divider, 2);
            UiChrome.PlaceTopLeft(statsTop.rectTransform, LeftPadX, StatsTopY, LeftContentWidth, 1f);
            statsTop.raycastTarget = false;

            for (int i = 0; i < StatCount; i++)
            {
                float y = StatsFirstRowY - i * StatRowStep;
                Label(left, "StatKey" + i, UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                    LeftPadX, y, 100f, StatRowHeight, StatLabels[i]);
                _statValues[i] = Label(left, "StatValue" + i, UiChrome.FontBody, TextAnchor.MiddleRight,
                    UiChrome.TextPrimary, LeftPadX + 100f, y, LeftContentWidth - 100f, StatRowHeight, "—");

                Image line = UiChrome.AddSurface(left, "StatLine" + i, UiChrome.Divider, 2);
                UiChrome.PlaceTopLeft(line.rectTransform, LeftPadX, y - StatRowHeight, LeftContentWidth, 1f);
                line.raycastTarget = false;
            }
        }

        /// <summary>라벨행(좌: 이름 / 우: 값) + 그 아래 4pt 트랙. 반환값은 채움 RectTransform.</summary>
        private RectTransform BuildGauge(RectTransform parent, string label, float labelY, float trackY,
            Color fillColor, out Text valueText)
        {
            Label(parent, "GaugeLabel_" + label, UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                LeftPadX, labelY, 100f, 13f, label);

            valueText = Label(parent, "GaugeValue_" + label, UiChrome.FontCaption, TextAnchor.MiddleRight,
                UiChrome.TextTertiary, LeftPadX + 60f, labelY, LeftContentWidth - 60f, 13f, "—");

            // 트랙도 <b>미리 합성한 불투명색</b>이다(2026-08-31). TrackBackground(흰색 α0.09)를 그대로
            // 칠하면 게이지 막대 자리에서만 창 알파가 0.92로 내려간다 — 아래는 항상 창 바탕이라
            // 합성 결과 색은 같고 알파만 지켜진다(UiChrome.Flatten 문서 참고).
            Image track = UiChrome.AddSurface(parent, "GaugeTrack_" + label,
                UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.PanelSurface), UiChrome.RadiusDot);
            UiChrome.PlaceTopLeft(track.rectTransform, LeftPadX, trackY, LeftContentWidth, TrackHeight);
            track.raycastTarget = false;

            Image fill = UiChrome.AddSurface(track.rectTransform, "Fill", fillColor, UiChrome.RadiusDot);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            fill.raycastTarget = false;
            return frt;
        }

        // -------------------- 우측 탭 컬럼 --------------------

        private RectTransform BuildRightColumn(RectTransform body)
        {
            var go = new GameObject("RightColumn", typeof(RectTransform));
            go.transform.SetParent(body, false);
            var right = go.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(right, RightX, 0f, RightWidth, BodyHeight);
            return right;
        }

        /// <summary>밑줄 탭(스펙 1.3) — 칩/배경 없이 라벨 + 활성 탭 2px 밑줄 하나.</summary>
        private void BuildTabs(RectTransform right)
        {
            float x = RightPadX;
            for (int i = 0; i < TabCount; i++)
            {
                float width = TabLabelWidth(TabNames[i]);

                Image hit = UiChrome.AddSurface(right, "Tab" + TabNames[i], Color.clear, UiChrome.RadiusChip);
                var rt = hit.rectTransform;
                UiChrome.PlaceTopLeft(rt, x, TabStripY, width, TabStripHeight);

                Text label = UiChrome.AddText(rt, "Label", UiChrome.FontTitle, TextAnchor.UpperCenter, UiChrome.TabInactive);
                UiChrome.Stretch(label.rectTransform);
                label.text = TabNames[i];

                Image underline = UiChrome.AddSurface(rt, "Underline", Color.clear, 2);
                UiChrome.PlaceTopLeft(underline.rectTransform, 0f, -(TabStripHeight - TabUnderlineHeight),
                    width, TabUnderlineHeight);
                underline.raycastTarget = false;

                var button = hit.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                int captured = i;
                button.onClick.AddListener(() => { if (TryClaimAction("tab" + captured)) OnTabClicked((Tab)captured); });

                _tabRects[i] = rt;
                _tabLabels[i] = label;
                _tabUnderlines[i] = underline;
                x += width + TabGap;
            }

            Image line = UiChrome.AddSurface(right, "TabBottomLine", UiChrome.CardBorder, 2);
            UiChrome.PlaceTopLeft(line.rectTransform, RightPadX, TabStripY - TabStripHeight + 1f, RightContentWidth, 1f);
            line.raycastTarget = false;
        }

        /// <summary>내장 폰트에는 폭 조회 API가 마땅치 않아 <b>글자 수 × 글자 크기</b>로 잡는다 —
        /// 한글은 정사각에 가까워 이 근사가 잘 맞고, 탭은 셋뿐이라 오차가 누적되지 않는다.</summary>
        private static float TabLabelWidth(string label) => label.Length * UiChrome.FontTitle + 4f;

        // -------------------- 카테고리 섹션 페이지([장비]/[외형] 공용) --------------------

        private void BuildSectionPage(RectTransform right)
        {
            var pageGo = new GameObject("SectionPage", typeof(RectTransform));
            pageGo.transform.SetParent(right, false);
            var page = pageGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(page, 0f, 0f, RightWidth, BodyHeight);
            _sectionPage = pageGo;

            // 카드 총량은 카탈로그가 정한다 — 빌드 때 한 번만 세고, 그 뒤로는 배열이 고정된다.
            var cards = new System.Collections.Generic.List<ItemCard>(SectionCount * 6);

            for (int s = 0; s < SectionCount; s++)
            {
                var sectionGo = new GameObject("Section" + s, typeof(RectTransform));
                sectionGo.transform.SetParent(page, false);
                var section = sectionGo.GetComponent<RectTransform>();
                UiChrome.PlaceTopLeft(section, RightPadX, SectionsTopY - s * SectionStep,
                    RightContentWidth, SectionHeight);

                Image dot = UiChrome.AddSurface(section, "Dot", UiChrome.Accent, UiChrome.RadiusDot);
                UiChrome.PlaceTopLeft(dot.rectTransform, 0f, -6f, 7f, 7f);
                dot.raycastTarget = false;

                Text title = Label(section, "Name", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    15f, -2f, 70f, 14f, "—", bold: true);
                Text code = Label(section, "Code", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextQuaternary,
                    90f, -3f, 46f, 12f, "—");

                Image divider = UiChrome.AddSurface(section, "Divider", UiChrome.Divider, 2);
                UiChrome.PlaceTopLeft(divider.rectTransform, 142f, -9f, 402f, 1f);
                divider.raycastTarget = false;

                Text count = Label(section, "Count", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.TextQuaternary,
                    548f, -3f, 44f, 12f, "0 / 4");

                var view = new SectionView
                {
                    Root = sectionGo, Dot = dot, Title = title, Code = code, Count = count,
                };
                _sections[s] = view;

                RectTransform content = BuildCardRow(view, section);

                view.FirstCard = cards.Count;
                view.CardCount = CardsInSection(s);
                for (int c = 0; c < view.CardCount; c++)
                {
                    cards.Add(BuildCard(content, s, c, cards.Count));
                }
            }

            _cards = cards.ToArray();
            BuildDetailPanel(page);
        }

        /// <summary>
        /// ★ 가로 카드 캐러셀 한 줄 — 2026-09-01 사용자 요청("마우스로 잡고 밀면 카드들이 넘어가는 형태").
        ///
        /// <para>포인터 이벤트를 손으로 짜지 않는다. <see cref="ScrollRect"/>가 드래그·클램프·휠을 이미
        /// 갖고 있고, 배치는 <see cref="HorizontalLayoutGroup"/>이, 폭은 <see cref="ContentSizeFitter"/>가,
        /// 잘라내기는 <see cref="RectMask2D"/>가 한다. 이 파일이 새로 만드는 것은 <b>하나도 없다</b>.</para>
        ///
        /// <para><b>관성(inertia)을 끄고 Clamped로 두는 이유</b>는 취향이 아니다 — 전역 폴링 드래그와
        /// 계산이 <b>같아야</b> 두 경로가 동시에 돌아도 결과가 어긋나지 않는다(<see cref="DragCarouselTo"/> 문단).</para>
        ///
        /// <para>뷰포트에 <b>투명한 Image</b>를 깔아 두는 이유: 카드 사이 9pt 틈을 잡아도 끌리게 하기
        /// 위해서다. 그 자리에 그래픽이 없으면 uGUI 레이캐스트가 통과해 창 바탕이 잡히고, 사용자에게는
        /// "여기는 안 밀리네"로 보인다.</para>
        /// </summary>
        private static RectTransform BuildCardRow(SectionView view, RectTransform section)
        {
            var rowGo = new GameObject("CardRow", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            rowGo.transform.SetParent(section, false);
            var row = rowGo.GetComponent<RectTransform>();
            // 폭이 섹션(592)이 아니라 CarouselViewportWidth인 이유는 그 상수 문서 참고 — 마지막 카드를
            // 반쯤 잘라 "더 있다"를 보이게 하는 것이 이 창의 유일한 발견 단서다.
            UiChrome.PlaceTopLeft(row, 0f, CardTopInSection, CarouselViewportWidth, CardHeight);

            var handle = rowGo.GetComponent<Image>();
            handle.color = Color.clear;
            handle.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(row, false);
            var viewport = viewportGo.GetComponent<RectTransform>();
            UiChrome.Stretch(viewport);

            var contentGo = new GameObject("Content", typeof(RectTransform),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewport, false);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            content.sizeDelta = new Vector2(0f, CardHeight);
            content.anchoredPosition = Vector2.zero;

            var layout = contentGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = CardGap;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;    // 카드는 자기 폭(141)을 지킨다 — 개수로 늘어나는 것은 줄이다.
            layout.childControlHeight = false;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = rowGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = CardStep * 0.5f;
            scroll.horizontalScrollbar = null;
            scroll.verticalScrollbar = null;

            view.Row = scroll;
            view.RowRect = row;
            view.Content = content;
            return content;
        }

        private ItemCard BuildCard(RectTransform content, int sectionIndex, int columnIndex, int cardIndex)
        {
            Image surface = UiChrome.AddSurface(content, "Card" + cardIndex, UiChrome.CardSurface, UiChrome.RadiusCard);
            var rt = surface.rectTransform;
            // x는 HorizontalLayoutGroup이 정한다 — 여기서는 <b>크기와 피벗</b>만 맞춰 준다.
            UiChrome.PlaceTopLeft(rt, 0f, 0f, CardWidth, CardHeight);
            Image outline = UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            Image thumb = UiChrome.AddSurface(rt, "Thumb", UiChrome.CardSurfaceMuted, UiChrome.RadiusThumb);
            UiChrome.PlaceTopLeft(thumb.rectTransform, ThumbX, ThumbY, ThumbWidth, ThumbHeight);
            thumb.raycastTarget = false;

            var card = new ItemCard
            {
                Section = sectionIndex,
                Item = columnIndex,
                Rect = rt,
                Surface = surface,
                Outline = outline,
                Thumb = thumb,
                Name = Label(rt, "Name", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    ThumbX, CardNameY, 78f, CardTextHeight, "—"),
                Meta = Label(rt, "Meta", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.TextQuaternary,
                    89f, CardNameY, 41f, CardTextHeight, "—"),
            };

            // ---- 카드 하단 [착용]/[해제] ----
            card.ActionSurface = UiChrome.AddSurface(rt, "Action", UiChrome.TextPrimary, UiChrome.RadiusChip);
            card.ActionRect = card.ActionSurface.rectTransform;
            UiChrome.PlaceTopLeft(card.ActionRect, ThumbX, CardActionY, CardActionWidth, CardActionHeight);
            card.ActionOutline = UiChrome.AddOutline(card.ActionRect, "Outline", UiChrome.TextPrimary, UiChrome.RadiusChip);
            card.ActionLabel = UiChrome.AddText(card.ActionRect, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.OnAccentSolid, bold: true);
            UiChrome.Stretch(card.ActionLabel.rectTransform);
            card.ActionLabel.text = "착용";

            var actionButton = card.ActionSurface.gameObject.AddComponent<Button>();
            actionButton.targetGraphic = card.ActionSurface;
            actionButton.onClick.AddListener(() =>
            {
                if (SuppressedByCarousel()) return;   // 방금 민 손짓의 끝을 클릭으로 오인하지 않는다.
                if (TryClaimAction("equip" + cardIndex)) OnCardEquipClicked(cardIndex);
            });

            // [장비]용/[외형]용 아이콘을 미리 두 벌 굽는다(클래스 문서 "탭을 바꿔도 다시 굽지 않는다").
            for (int set = 0; set < IconSetCount; set++)
            {
                Tab tab = set == 1 ? Tab.Appearance : Tab.Equipment;
                // 이 탭에 없는 섹션(=[외형]의 4번째)에는 구울 것이 없다. 예전에는 SectionSlot의
                // 폴백(Head)이 돌아와 <b>모자 아이콘</b>을 몰래 한 벌 더 굽고 있었다.
                bool inThisTab = sectionIndex < SectionCountForTab(tab);
                EquipmentSlot slot = inThisTab ? SectionSlot(tab, sectionIndex) : EquipmentSlot.Head;
                ItemCatalogEntry entry = inThisTab ? ItemCatalog.Item(slot, columnIndex) : null;

                var iconGo = new GameObject("Icon" + set, typeof(RectTransform));
                iconGo.transform.SetParent(thumb.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(IconSize, IconSize);
                irt.anchoredPosition = Vector2.zero;

                if (entry != null) BuildCardArt(irt, slot, columnIndex, entry);
                card.IconRoot[set] = irt;
                Image[] graphics = iconGo.GetComponentsInChildren<Image>(true);
                card.IconGraphics[set] = graphics;
                var baseColors = new Color[graphics.Length];
                for (int g = 0; g < graphics.Length; g++)
                {
                    baseColors[g] = graphics[g] != null ? graphics[g].color : UiChrome.IconInk;
                }
                card.IconBaseColors[set] = baseColors;
            }

            // 자물쇠 배지 — 썸네일 우하단에 살짝 걸치게(스펙 right −4 / bottom −3).
            Image badge = UiChrome.AddSurface(thumb.rectTransform, "LockBadge", UiChrome.ThumbSurfaceLocked, UiChrome.RadiusBadge);
            var brt = badge.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 0f);
            brt.sizeDelta = new Vector2(LockBadgeWidth, LockBadgeHeight);
            brt.anchoredPosition = new Vector2(4f, -3f);
            badge.raycastTarget = false;
            BuildLockGlyph(brt);
            card.LockBadge = brt;
            card.LockBadge.gameObject.SetActive(false);

            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() =>
            {
                if (SuppressedByCarousel()) return;
                if (TryClaimAction("card" + cardIndex)) OnCardClicked(cardIndex);
            });
            return card;
        }

        /// <summary>40×40 viewBox(y가 아래로) -> 부모 중심 기준 화면 좌표(y가 위로).</summary>
        private static Vector2 FromViewBox(float x, float y, float viewWidth, float viewHeight,
            float renderWidth, float renderHeight)
        {
            return new Vector2(
                (x - viewWidth * 0.5f) * (renderWidth / viewWidth),
                (viewHeight * 0.5f - y) * (renderHeight / viewHeight));
        }

        /// <summary>
        /// ★ 2026-09-01 (로드맵 P0-a) — 카드 썸네일을 <b>몸에 붙는 것과 같은 도형</b>으로 그린다.
        ///
        /// <para>지금까지 한 아이템은 그림을 두 벌 갖고 있었다: 카드는 손으로 배치한 40×40 SVG
        /// (<see cref="ItemCatalogEntry.Icon"/>), 몸은 절차적 계산(<see cref="AccessoryShapeBuilder"/>).
        /// 그래서 도형을 고칠 때마다 카드만 옛 모양으로 남았다 — 이번 라운드에 머리 4종을 다시 그리므로,
        /// 통합하지 않으면 사용자가 지적한 "카드와 실제가 다름"이 <b>오히려 더 심해진다</b>.</para>
        ///
        /// <para><b>폴백은 남긴다.</b> 새 경로가 도형을 못 만들면(FX/PET처럼 몸 도형이 없는 카테고리가
        /// 정상적으로 여기 해당한다) 옛 아이콘을 그대로 그린다. 즉 새 경로가 통째로 틀려도 카드가
        /// 비지 않는다 — <see cref="AccessoryDefSO.icon"/>을 이번에 지우지 않은 이유가 이것이다.</para>
        /// </summary>
        private static void BuildCardArt(RectTransform root, EquipmentSlot slot, int itemIndex,
            ItemCatalogEntry entry)
        {
            // 색은 <b>카탈로그 색 그대로</b>다(몸의 WornColor 변환을 태우지 않는다). 착용 색 정책은
            // 로드맵 P5의 몫이고, 도형 통합과 색 정책을 한 라운드에 같이 바꾸면 카드 그림이 달라진
            // 이유가 좌표 때문인지 색 때문인지 판정할 수 없게 된다.
            if (AccessoryCardIcon.TryBuild(root, slot, itemIndex, IconSize, IconStroke,
                    entry.PrimaryColor, entry.SecondaryColor))
            {
                return;
            }
            BuildIcon(root, entry.Icon);
        }

        private static void BuildIcon(RectTransform root, ItemIconPart[] parts)
        {
            if (parts == null) return;
            for (int p = 0; p < parts.Length; p++)
            {
                ItemIconPart part = parts[p];
                float[] v = part.Values;
                if (v == null) continue;

                switch (part.Kind)
                {
                    case ItemIconPartKind.Polyline:
                    {
                        int count = Mathf.Min(part.PointCount, _iconPoints.Length);
                        for (int i = 0; i < count; i++)
                        {
                            _iconPoints[i] = FromViewBox(v[i * 2], v[i * 2 + 1], 40f, 40f, IconSize, IconSize);
                        }
                        UiChrome.AddPolyline(root, "Seg", _iconPoints, count, IconStroke, part.Color);
                        break;
                    }
                    case ItemIconPartKind.Ring:
                        UiChrome.AddCircle(root, "Ring", v[2] * 2f * IconScale, part.Color, IconStroke,
                            FromViewBox(v[0], v[1], 40f, 40f, IconSize, IconSize));
                        break;
                    case ItemIconPartKind.DashedRing:
                        BuildDashedRing(root, v[0], v[1], v[2], part.Color);
                        break;
                    case ItemIconPartKind.Dot:
                        UiChrome.AddCircle(root, "Dot", v[2] * 2f * IconScale, part.Color, 0f,
                            FromViewBox(v[0], v[1], 40f, 40f, IconSize, IconSize));
                        break;
                }
            }
        }

        /// <summary>viewBox(40) -> 실제 아이콘 크기 배율. 반지름처럼 <b>길이</b>인 값은 전부 이걸 곱해야 한다
        /// (좌표는 <see cref="FromViewBox"/>가 이미 환산한다 — 반지름은 그 경로를 타지 않아 예전에는
        /// IconSize == 40이라 우연히 맞고 있었다).</summary>
        private const float IconScale = IconSize / 40f;

        /// <summary>점선 원(FX "없음" 전용). 링 스프라이트에는 점선이 없어 짧은 호 8개로 그린다.</summary>
        private static void BuildDashedRing(RectTransform root, float cx, float cy, float r, Color color)
        {
            const int dashes = 8;
            const int pointsPerDash = 3;
            Vector2 center = FromViewBox(cx, cy, 40f, 40f, IconSize, IconSize);
            float radius = r * (IconSize / 40f);

            for (int d = 0; d < dashes; d++)
            {
                float start = d * (Mathf.PI * 2f / dashes);
                for (int i = 0; i < pointsPerDash; i++)
                {
                    float a = start + (Mathf.PI / dashes) * (i / (float)(pointsPerDash - 1));
                    _iconPoints[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                }
                UiChrome.AddPolyline(root, "Dash", _iconPoints, pointsPerDash, IconStroke, color);
            }
        }

        /// <summary>자물쇠 14×15(스펙 viewBox 20×21) — 채운 몸통 + 고리 호.</summary>
        private static void BuildLockGlyph(RectTransform badge)
        {
            const float viewW = 20f, viewH = 21f, renderW = 14f, renderH = 15f;

            Image bodyImage = UiChrome.AddSurface(badge, "LockBody", UiChrome.TextQuaternary, 2);
            var brt = bodyImage.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(14f * (renderW / viewW), 10f * (renderH / viewH));
            brt.anchoredPosition = FromViewBox(10f, 14.5f, viewW, viewH, renderW, renderH);
            bodyImage.raycastTarget = false;

            int count = LockShackle.Length / 2;
            for (int i = 0; i < count; i++)
            {
                _iconPoints[i] = FromViewBox(LockShackle[i * 2], LockShackle[i * 2 + 1], viewW, viewH, renderW, renderH);
            }
            UiChrome.AddPolyline(badge, "LockShackle", _iconPoints, count,
                IconStroke * (renderW / viewW), UiChrome.TextQuaternary);
        }

        private void BuildDetailPanel(RectTransform page)
        {
            Image detail = UiChrome.AddSurface(page, "Detail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            UiChrome.PlaceTopLeft(drt, RightPadX, DetailY, RightContentWidth, DetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            _detailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                15f, -14f, 150f, 17f, "—", bold: true);
            _detailMeta = Label(drt, "DetailMeta", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                172f, -14f, 330f, 17f, "—");

            _detailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_detailBody.rectTransform, 15f, -42f, RightContentWidth - 30f, 48f);
            _detailBody.lineSpacing = 1.6f;   // 스펙 line-height 1.6.

            _actionSurface = UiChrome.AddSurface(drt, "Action", UiChrome.TextPrimary, UiChrome.RadiusChip);
            _actionRect = _actionSurface.rectTransform;
            UiChrome.PlaceTopLeft(_actionRect, RightContentWidth - 15f - 52f, -13f, 52f, 24f);
            _actionOutline = UiChrome.AddOutline(_actionRect, "Outline", UiChrome.TextPrimary, UiChrome.RadiusChip);
            _actionLabel = UiChrome.AddText(_actionRect, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter,
                UiChrome.OnAccentSolid);
            UiChrome.Stretch(_actionLabel.rectTransform);
            _actionLabel.text = "착용";

            var actionButton = _actionSurface.gameObject.AddComponent<Button>();
            actionButton.targetGraphic = _actionSurface;
            actionButton.onClick.AddListener(() => { if (TryClaimAction("action")) OnActionClicked(); });
        }

        // -------------------- 보관함 페이지 --------------------

        private void BuildInventoryPage(RectTransform right)
        {
            var pageGo = new GameObject("InventoryPage", typeof(RectTransform));
            pageGo.transform.SetParent(right, false);
            var page = pageGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(page, 0f, 0f, RightWidth, BodyHeight);
            _inventoryPage = pageGo;

            float rowStep = InventoryRowHeight + InventoryRowGap;

            for (int i = 0; i < InventoryVisibleRows; i++)
            {
                Image surface = UiChrome.AddSurface(page, "InvRow" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                var rt = surface.rectTransform;
                UiChrome.PlaceTopLeft(rt, RightPadX, SectionsTopY - i * rowStep, InventoryListWidth, InventoryRowHeight);
                Image outline = UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);

                // 장비/행동을 완전히 같은 행 모양으로 그린다(디자이너 확정) —
                // ● 표식 / 이름 / 부제 / 설명 한 줄 / 상태 슬롯(96pt 고정, 훗날 가격표 자리).
                Image dot = UiChrome.AddSurface(rt, "Dot", UiChrome.TextQuaternary, UiChrome.RadiusDot);
                UiChrome.PlaceTopLeft(dot.rectTransform, UiChrome.Space2, -(InventoryRowHeight - 6f) * 0.5f, 6f, 6f);
                dot.raycastTarget = false;

                float nameX = UiChrome.Space2 + 6f + UiChrome.Space2;
                Text title = Label(rt, "Title", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    nameX, 0f, 110f, InventoryRowHeight, string.Empty);
                Text subtitle = Label(rt, "Subtitle", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextQuaternary,
                    nameX + 112f, 0f, 48f, InventoryRowHeight, string.Empty);

                float descX = nameX + 112f + 50f;
                float descWidth = InventoryListWidth - descX - StatusSlotWidth - UiChrome.Space2;
                Text description = Label(rt, "Description", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                    UiChrome.TextSecondary, descX, 0f, Mathf.Max(40f, descWidth), InventoryRowHeight, string.Empty);
                // 줄바꿈하지 않는다 — 길이는 Ellipsize가 미리 자른다(위 상수 참고).
                description.horizontalOverflow = HorizontalWrapMode.Overflow;
                description.verticalOverflow = VerticalWrapMode.Truncate;

                Text statusSlot = Label(rt, "StatusSlot", UiChrome.FontCaption, TextAnchor.MiddleRight,
                    UiChrome.TextTertiary, InventoryListWidth - StatusSlotWidth - UiChrome.Space2, 0f,
                    StatusSlotWidth, InventoryRowHeight, string.Empty);

                Text header = Label(rt, "Header", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    0f, 0f, InventoryListWidth, InventoryRowHeight, string.Empty, bold: true);

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

            // 페이지 버튼 — 휠에 기대지 않는다(클래스 문서 참고: 우리 창은 앱이 활성일 때만 휠을 받는다).
            float listHeight = InventoryVisibleRows * rowStep - InventoryRowGap;
            float railX = RightPadX + InventoryListWidth + UiChrome.Space2;

            _pageUpRect = BuildPagerButton(page, "PageUp", "▲", railX, SectionsTopY, () => ScrollInventory(-1), "pageUp");
            _pageDownRect = BuildPagerButton(page, "PageDown", "▼", railX,
                SectionsTopY - (listHeight - InventoryRailWidth), () => ScrollInventory(1), "pageDown");

            _pageIndicator = Label(page, "PageIndicator", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.TextQuaternary, railX, SectionsTopY - (InventoryRailWidth + UiChrome.Space2),
                InventoryRailWidth, listHeight - InventoryRailWidth * 2f - UiChrome.Space2 * 2f, "1\n/\n1");

            Image detail = UiChrome.AddSurface(page, "InventoryDetail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            UiChrome.PlaceTopLeft(drt, RightPadX, DetailY, RightContentWidth, DetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            _inventoryDetailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, 15f, -14f, RightContentWidth - 30f, 17f, "—", bold: true);

            _inventoryDetailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_inventoryDetailBody.rectTransform, 15f, -42f, RightContentWidth - 30f, 34f);
            _inventoryDetailBody.lineSpacing = 1.6f;

            // 지금 파는 것은 하나도 없다 — 그 사실을 화면에서도 숨기지 않는다.
            Label(drt, "Note", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.TextQuaternary,
                RightContentWidth - 215f, -DetailHeight + 26f, 200f, 14f, "지금은 파는 것이 없습니다");
        }

        private RectTransform BuildPagerButton(RectTransform page, string name, string glyph, float x, float y,
            UnityEngine.Events.UnityAction action, string dedupKey)
        {
            Image surface = UiChrome.AddSurface(page, name, UiChrome.CardSurface, UiChrome.RadiusChip);
            var rt = surface.rectTransform;
            UiChrome.PlaceTopLeft(rt, x, y, InventoryRailWidth, InventoryRailWidth);
            UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);

            Text label = UiChrome.AddText(rt, "Label", UiChrome.FontCaption, TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;

            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() => { if (TryClaimAction(dedupKey)) action(); });
            return rt;
        }

        // ==================== 작은 유틸 ====================

        /// <summary>좌상단 원점 배치 + 문자열까지 한 번에 — 이 파일에만 100번 넘게 나오는 조합이다.</summary>
        private static Text Label(Transform parent, string name, int fontSize, TextAnchor anchor, Color color,
            float x, float y, float width, float height, string text, bool bold = false)
        {
            Text t = UiChrome.AddText(parent, name, fontSize, anchor, color, bold);
            UiChrome.PlaceTopLeft(t.rectTransform, x, y, width, height);
            t.text = text;
            return t;
        }

        private InputField CreateInputField(Transform parent)
        {
            Image surface = UiChrome.AddSurface(parent, "NameInput", UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.AddOutline(surface.rectTransform, "Outline", UiChrome.PanelBorder, UiChrome.RadiusChip);

            Text text = UiChrome.AddText(surface.rectTransform, "Text", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            UiChrome.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(UiChrome.Space2, 0f);
            text.rectTransform.offsetMax = new Vector2(-UiChrome.Space2, 0f);
            text.supportRichText = false;

            Text placeholder = UiChrome.AddText(surface.rectTransform, "Placeholder", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextQuaternary);
            UiChrome.Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(UiChrome.Space2, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-UiChrome.Space2, 0f);
            placeholder.text = CharacterProgressionModel.DefaultCharacterName;
            placeholder.fontStyle = FontStyle.Italic;

            var input = surface.gameObject.AddComponent<InputField>();
            input.targetGraphic = surface;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = CharacterProgressionModel.MaxNameLength;
            input.lineType = InputField.LineType.SingleLine;
            input.text = CharacterProgressionModel.CharacterName;
            input.onEndEdit.AddListener(_ => EndNameEdit(commit: true));
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
                case StickmanStateId.GroundLossHang: return "허둥대는 중";
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
