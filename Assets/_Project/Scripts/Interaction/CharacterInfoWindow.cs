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
    public sealed class CharacterInfoWindow : MonoBehaviour, IExclusiveSurface
    {
        [SerializeField] private StickConfig _config;

        // ★ 2026-08-30: 31000 -> 31900. 이 창은 모달인데 부채꼴(31500)/팝오버(31700)보다 <b>아래</b>에
        // 깔려 있었다(디버거 실측) — 값 자체가 "모달"이라는 성격과 모순이었다. 말풍선(31000)과도 값이
        // 같아 Unity가 그리기 순서를 보장하지 않았다(동률 오버레이 캔버스는 생성 순서에 의존).
        private const int SortingOrderTopMost = 31900; // 팝오버(31700) 위, 앱 제어 메뉴(32760) 아래.

        // ==================== 33-7-2 확정 치수 (캔버스 유닛 == OS 포인트) ====================

        private const float PanelWidth = 880f;
        private const float TitleHeight = 40f;

        /// <summary>본문 마지막 요소(상세 패널) 아래에 남기는 여백. 예전에 861/696/103에서 <b>역산되던</b>
        /// 값을 상수로 꺼냈다 — 이제 창 높이가 탭마다 달라지므로(<see cref="PanelHeightForTab"/>)
        /// 이 값이 파생의 출발점이 되어야 한다. 861 = 40 + 696 + 103 + <b>22</b>.</summary>
        private const float BodyBottomMargin = 22f;

        /// <summary>가장 높은 탭(섹션 4개)의 창 높이 = 종전 고정값 861. 화면 클램프의 상한이자
        /// 캔버스/차단막 계산의 기준으로만 쓴다.</summary>
        private const float PanelMaxHeight = TitleHeight + SectionCount * SectionStep - SectionsTopY
                                             + DetailHeight + BodyBottomMargin;   // 861

        private const float BodyHeight = PanelMaxHeight - TitleHeight;   // 821 — 페이지 컨테이너 크기(마스크가 자른다)
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
        private const float DetailHeight = 103f;

        // ★ 2026-09-01 P0-1 — <b>DetailY 상수(-696)를 지웠다.</b>
        //
        //   -696은 "섹션 4개분(4 × 156 = 624)을 다 쓴 뒤"라는 뜻이었고, 그래서 섹션이 3개뿐인 [외형]
        //   탭에서도 <b>없는 4번째 섹션의 자리를 예약</b>했다. 마지막 카드 아래에 176pt(창 높이 861의
        //   20.4%)가 비었다 — 취향 문제가 아니라 상한(SectionCount)을 고정 예산으로 쓴 레이아웃 버그다
        //   (docs/UI_SURFACE_SPEC.md §3.1: 예측 176 vs 캡처 실측 175).
        //
        //   이제 상세 패널의 y와 창 높이는 <b>그 탭이 실제로 보여줄 섹션 수</b>에서 파생된다.
        //   [보관함]은 섹션이 아니라 20줄 목록이라 종전 최대 높이를 그대로 쓴다(빈칸이 없다).

        /// <summary>이 탭의 본문이 세로로 몇 칸(SectionStep)을 차지하는가.
        /// 카드 탭은 실제 카테고리 수, [보관함]은 목록이 쓰는 최대치.</summary>
        private static int LayoutStepsForTab(Tab tab)
            => tab == Tab.Inventory ? SectionCount : SectionCountForTab(tab);

        /// <summary>상세 패널의 위 끝(본문 좌표, 아래가 음수).</summary>
        private static float DetailYForTab(Tab tab) => SectionsTopY - LayoutStepsForTab(tab) * SectionStep;

        /// <summary>이 탭에서의 창 높이. [장비]/[보관함] 861, [외형] <b>705</b>(= 861 − 156).</summary>
        private static float PanelHeightForTab(Tab tab)
            => TitleHeight - DetailYForTab(tab) + DetailHeight + BodyBottomMargin;

        /// <summary>탭 전환 시 창 높이가 바뀌는 데 걸리는 시간. 순간이동하면 화면 중앙 고정 창이
        /// "깜빡 튄" 것처럼 보인다 — 부채꼴 호버(0.09초)보다 조금 길고 눈이 따라갈 수 있는 값.</summary>
        private const float PanelHeightAnimateSeconds = 0.12f;

        // ---- 카드 ----
        // ★ 2026-09-01 사용자 신고("장비카드가 어설픈데서 절반 짤려있어서 더 이상함. 좀더 오른쪽까지
        //   채워져야함"): 걸침(peek) 자체는 옳았지만 <b>자르는 선의 위치</b>가 틀렸다. 뷰포트가 520.5라
        //   섹션 오른쪽 끝(592)에서 71.5pt 못 미친 <b>허공</b>에서 카드가 잘렸다 — 바로 위 "n / 6"
        //   카운터가 592에서 끝나는데 카드줄만 520.5에서 끝나니 "더 있다"가 아니라 "깨졌다"로 읽힌다.
        //   이제 자르는 선을 592(= 카운터 오른쪽 끝)에 맞추고, 그 폭에 3.5장이 떨어지도록 카드를 키운다.
        private const float CardGap = 9f;
        private const float CardWidth = 161f;
        private const float CardStep = CardWidth + CardGap;   // 170

        /// <summary>캐러셀 뷰포트에 <b>온전히</b> 들어오는 카드 수. 나머지 한 장은 일부러 걸치게 둔다.</summary>
        private const int CarouselFullCards = 3;

        /// <summary>걸치는 카드가 보이는 비율. 이제 <b>입력이 아니라 결과</b>다 — 자르는 선을
        /// 열 오른쪽 끝(592)에 못 박았으므로 비율은 거기서 떨어진다(161pt 카드의 50.9%).
        /// 0.8을 넘으면 온전한 카드로 보여 다시 "이게 전부"가 되고, 너무 작으면 가장자리 그림자로
        /// 보인다 — 그 창은 <c>CardWidth</c>로 맞춘다.</summary>
        private const float CarouselPeekFraction =
            (CarouselViewportWidth - CardStep * CarouselFullCards) / CardWidth;   // 0.509

        /// <summary>
        /// ★ 캐러셀 뷰포트 폭 = <b>섹션 폭 그대로</b>. 이 창의 오른쪽 열에 있는 모든 것(구분선+"n / 6"
        /// 카운터, 상세 패널)이 592에서 끝나므로 카드줄도 같은 선에서 끝나야 한다.
        ///
        /// <para><b>왜 이 상수가 한 번 520.5였는가</b>(2026-09-01 오전): 592는 <see cref="CardStep"/> 150
        /// 짜리 카드가 <b>정확히 4장</b> 들어가고 1pt만 남는 폭이라 "모자는 4개구나"로 확정됐다
        /// (페르소나 M1 — 6종 중 2종이 발견되지 않음). 그래서 카드는 그대로 두고 <b>창문만</b> 520.5로
        /// 좁혀 마지막 카드를 반쯤 걸치게 했다.</para>
        ///
        /// <para><b>왜 되돌리는가</b>(같은 날 사용자 신고 "어설픈데서 절반 짤려있어서 더 이상함"):
        /// 걸침은 <b>모서리에 걸려야</b> "계속된다"로 읽힌다. 520.5는 아무 모서리도 아닌 허공이라
        /// 오른쪽에 71.5pt를 비워 둔 채 카드만 동강난 그림이 됐다. 이제 자르는 선을 열 끝에 두고
        /// 카드 폭(141→161)으로 3.5장을 맞춘다 — 발견 단서는 유지하고 어중간함만 없앤다.
        /// 덤으로 이름 칸이 70→90pt가 되어 P0-5의 한글 7자(≈84pt) 이름이 말줄임 없이 들어간다.</para>
        /// </summary>
        private const float CarouselViewportWidth = RightContentWidth;   // 592

        private const float CardHeight = 108f;
        private const float CardTopInSection = -28f;
        private const float ThumbX = 11f;

        // ★ 2026-09-01 — 카드 하단에 [착용]/[해제] 버튼이 들어오면서 <b>같은 108pt 안에서</b> 내부를
        //   다시 나눴다. 카드를 키울 수 없는 이유는 세로 예산이 이미 정확히 꽉 차 있어서다:
        //   섹션 4개 × SectionStep 156 = 624 = SectionsTopY(-72) ~ DetailYForTab([장비])(-696) 사이 전부.
        //   그래서 썸네일 62 -> 54, 이름줄 16 -> 14로 줄이고 남은 22pt를 버튼에 준다.
        private const float ThumbY = -8f;
        /// <summary>카드 좌우 여백(<see cref="ThumbX"/>)을 뺀 나머지 — <b>숫자를 따로 적지 않는다</b>.
        /// 카드 폭이 바뀌면 썸네일·하단 버튼·이름 칸이 전부 따라온다.</summary>
        private const float ThumbWidth = CardWidth - ThumbX * 2f;   // 139
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

        // ★ 2026-09-01 P0-5 — 이름 상자와 메타 상자가 <b>맞닿아 있었다</b>.
        //   이름 x 11..89(폭 78, Overflow) / 메타 x 89..130 → 두 상자 사이 간격 <b>0pt</b>.
        //   "리틀스틱메이트"(한글 7자 ≈ 84pt)는 78을 6pt 넘겨 "착용 중"과 부딪혔다
        //   (캡처 실측 간격 1.2pt. 다른 카드는 40pt 이상이라 그 카드만 깨져 보였다).
        //   이제 간격을 Space2로 <b>못 박고</b> 이름 폭을 거기서 뺀다 — 숫자를 두 곳에 적지 않는다.
        //   ★ 2026-09-01 오후: 메타 칸 폭("착용 중" 41pt)을 <b>원본</b>으로 두고 x를 오른쪽 여백에서
        //   역산한다. 예전에는 x가 원본이라 카드 폭이 커져도 메타가 왼쪽에 붙은 채 남았다.
        private const float CardMetaWidth = 41f;
        private const float CardMetaX = CardWidth - ThumbX - CardMetaWidth;          // 109
        private const float CardNameGap = UiChrome.Space2;                           // 8
        private const float CardNameWidth = CardMetaX - ThumbX - CardNameGap;        // 90

        /// <summary>카드 하단 [착용]/[해제] 버튼 — 이 창에서 옷을 갈아입히는 <b>유일한</b> 손잡이다.
        /// <para>★ 2026-09-01: 상세 패널에도 같은 버튼이 있었는데(사용자 신고 "각 장비별 착용버튼으로
        /// 했는데 왜 옛날처럼 하단에 착용상자가 따로 있음?") 그쪽을 걷어냈다. 상태→라벨/색 매핑은
        /// 여전히 <see cref="StyleActionButton"/> 한 곳뿐이다.</para></summary>
        private const float CardActionY = -80f;

        /// <summary>★ 2026-09-02: 22 → <b>24</b>. 리터럴이 아니라 <see cref="UiChrome.MinTargetSizePoints"/>
        /// (WCAG 2.2 2.5.8 Target Size (Minimum))에서 가져온다 — 하한이 움직이면 여기가 따라와야지,
        /// 숫자를 베껴 두면 하한과 조용히 갈라진다.
        /// <para>세로 예산 검산: <see cref="CardActionY"/> 80 + 24 = 104 ≤ <see cref="CardHeight"/> 108.
        /// 카드 아래 여백이 6 → <b>4pt</b>로 줄지만 넘치지 않는다.</para></summary>
        private const float CardActionHeight = UiChrome.MinTargetSizePoints;
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

        /// <summary>페이지 지시자 상자 높이. ★ 2026-09-02 — 예전에는 <b>레일 전체(473pt)</b>가 상자였고
        /// MiddleCenter라 숫자가 [▲]에서 219pt 떨어진 <b>허공</b>에 떴다(45-9-a). 위에서 아래로 읽으므로
        /// [▲] 바로 밑에 붙인다 — "[▲]를 누르면 이 숫자가 준다"는 인과가 그제서야 붙는다.</summary>
        private const float InventoryPageIndicatorHeight = 16f;
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

        /// <summary>좌측 스탯 행 수. 6 -> 5(2026-09-01 "넘어진 횟수 삭제") -> <b>4</b>
        /// (2026-09-02 격파 놀이 기능 삭제).
        /// <b>두 번 다 지운 것은 표시뿐이다</b> — <see cref="CharacterStatsModel.RagdollFalls"/>도
        /// <see cref="CharacterStatsModel.BattleWins"/>도 값은 그대로 살아 저장 파일을 왕복한다.
        /// 데이터를 함께 지우면 훗날 다른 화면에서 다시 쓸 때 계수 로직을 <b>처음부터 다시</b>
        /// 만들어야 하고, BattleWins 쪽은 그에 더해 저장 스키마 버전을 올려야 한다.
        ///
        /// <para>행을 빼도 <b>창 높이는 바뀌지 않는다</b>: 이 창의 높이는 우측 컬럼이 정하고
        /// (<see cref="PanelHeightForTab"/> ← PanelMaxHeight), 좌측 스탯은 그보다 훨씬 위에서 끝난다.
        /// 즉 여기서 한 행이 빠져도 "푸터 아래 빈 띠" 같은 것은 생기지 않는다 — 같은 이유로
        /// 2026-09-01의 6→5도 창 치수를 건드리지 않았다.</para></summary>
        private const int StatCount = 4;

        private static readonly string[] TabNames = { "장비", "외형", "보관함" };
        private static readonly string[] StatLabels =
        {
            // ※ 4번째 칸은 "대결 승리"였다 — 라이벌 기능 전체 삭제(2026-08-30)로 영구 0이 되는
            //    죽은 칸이 되어 "보유 장비"(레벨에 따라 실제로 늘어나는 값)로 교체했다.
            // ※ "넘어진 횟수"(옛 6번째 칸)는 2026-09-01 사용자 요청으로 <b>표시만</b> 뺐다.
            //    CharacterStatsModel.RagdollFalls는 그대로 세고 있다(위 StatCount 문서 참고).
            // ※ "격파 성공"(옛 3번째 칸)은 2026-09-02 격파 놀이 기능 삭제로 뺐다. 대결 승리와 같은
            //    이유(영구 0이 되는 죽은 칸)다. BattleWins 값 자체는 저장 파일에 그대로 남는다.
            "근속", "함께한 시간", "보유 장비", "활쏘기 명중",
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

        /// <summary>[장비]/[외형] 탭의 상세 패널. 탭마다 y가 달라지므로 붙잡아 둔다(P0-1).</summary>
        private RectTransform _sectionDetailRect;

        /// <summary>지금 화면에 있는 창 높이. 탭 전환 시 목표값(<see cref="PanelHeightForTab"/>)으로
        /// <see cref="PanelHeightAnimateSeconds"/> 동안 이동한다. 0이면 아직 한 번도 안 정해진 상태.</summary>
        private float _panelHeightPoints;

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

            /// <summary><see cref="Name"/>에 넣으려던 <b>자르기 전</b> 문자열. 이것이 그대로면
            /// <see cref="UiChrome.Ellipsize"/>를 다시 부르지 않는다 — 그 함수는 폭을 재려고
            /// <c>Text.text</c>를 여러 번 바꾸고 잘린 문자열을 새로 할당하므로, 4Hz 갱신 루프에서
            /// 무조건 부르면 카드 24장 × 4회/초의 쓰레기가 계속 쌓인다(상주 앱 규약).</summary>
            public string NameSource;

            /// <summary>카드 하단 [착용]/[해제] 버튼(2026-09-01 사용자 요청). 카드 <b>본체</b> 클릭은
            /// 지금까지처럼 "고르기"만 하고, 옷을 갈아입히는 것은 이 버튼뿐이다 — 캐러셀을 밀다가
            /// 옷이 갈아입혀지는 사고를 구조적으로 없앤다.</summary>
            public Image ActionSurface;

            public Image ActionOutline;
            public RectTransform ActionRect;
            public Text ActionLabel;

            /// <summary>★ 2026-09-02 — 잠긴 카드의 칩을 <b>진짜로</b> 비활성으로 만들기 위해 들고 있는다.
            /// 코드는 예전부터 잠긴 클릭을 무시했지만(<see cref="OnActionClicked"/>) <c>interactable</c>은
            /// <c>true</c>였다. 그래서 그 칩은 "동작하지 않는데 활성인 척하는 컨트롤"이었고,
            /// WCAG 2.2 1.4.11이 비활성 컴포넌트에 주는 <b>면제를 받을 자격이 없었다</b>.</summary>
            public Button ActionButton;
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
        private Image _pageUpOutline;
        private Text _pageUpLabel;
        private Image _pageDownOutline;
        private Text _pageDownLabel;
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

        /// <summary>커서가 이 창 위에 있었거나 조작이 있었던 마지막 시각(프레임 페이싱 홀드용).
        /// <see cref="TickFramePacingHold"/> 참고.</summary>
        private float _lastSurfaceTouchTime = float.NegativeInfinity;

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

        // ★ 2026-09-02 — 여기 있던 <c>_menu</c>/<c>_gear</c> 캐시를 지웠다. 유일한 사용처가
        //   "창 밖 클릭" 탈출구의 <b>예외 판정</b>(톱니/부채꼴 위 클릭은 창을 닫지 않는다)이었는데,
        //   그 탈출구 자체가 사용자 지시로 사라졌다. 배타 규칙은 <see cref="ExclusiveSurfaces"/>가
        //   인터페이스로 처리하므로 이 창이 이웃을 직접 알 이유가 더는 없다.

        private string _lastActionKey;
        private float _lastActionTime;
        private StickmanStateId _lastShownState = (StickmanStateId)(-1);
        private bool _hasShownState;

        /// <summary>이 시각(unscaled)까지는 프레즌스 문구를 바꾸지 않는다 — <see cref="TickPresenceLine"/>.</summary>
        private float _presenceHoldUntil;
        private float _lastDpiScale = -1f;

        /// <summary>히트테스트/호버 폴링이 돌려쓰는 코너 버퍼 — 이 앱은 하루 종일 켜져 있어서
        /// 0.05초마다 <c>new Vector3[4]</c>를 20번씩 만드는 것도 상시 쓰레기가 된다.</summary>
        private static readonly Vector3[] _corners = new Vector3[4];

        /// <summary>이 창 안의 모든 <see cref="RectMask2D"/> — 빌드 때 한 번만 모은다. 전역 폴링
        /// 히트테스트가 "마스크에 잘린 자리는 누를 수 없다"를 판단하는 근거(R2 M3).</summary>
        private RectMask2D[] _masks = System.Array.Empty<RectMask2D>();

        /// <summary>카드의 [착용] 버튼이 <b>전부</b> 잘려 닿을 수 없는 상태인가 — 상태가 바뀔 때만 경고한다(로그 도배 방지).</summary>
        private bool _actionUnreachable;

        /// <summary>아이콘 한 파츠를 그릴 때 돌려쓰는 점 버퍼(가장 긴 파츠보다 넉넉하게).</summary>
        private static readonly Vector2[] _iconPoints = new Vector2[64];

        public bool IsOpen => _open;

        // ★ 배타 표면 등록(2026-09-01) — 목록을 손으로 적지 않기 위한 유일한 배선. 명시적 구현이라
        //   이 창의 공개 API(Open/Close/Toggle/IsOpen)는 한 톨도 바뀌지 않는다.
        bool IExclusiveSurface.IsSurfaceOpen => _open;
        void IExclusiveSurface.CloseSurface(string reason) => Close(reason);

        /// <summary>창이 실제로 켜져 있는가(진단/테스트 전용) — 플래그가 아니라 GameObject의 실제 상태.</summary>
        public bool IsCanvasActive => _canvas != null && _canvas.gameObject.activeSelf;

        /// <summary>클릭관통 차단막이 켜져 있는가(진단/테스트 전용, 비침해 원칙 2 검증용).</summary>
        public bool IsClickBlockerEnabled => _clickBlocker != null && _clickBlocker.enabled;

        /// <summary>차단막(BoxCollider2D)이 실제로 덮고 있는 월드 영역 — <b>비침해(원칙 2) 실측 창구</b>.
        /// <para>★ 2026-09-02부터 창 밖 클릭이 창을 닫지 않으므로 이 사각형은 <b>사용자가 [✕]를 누를
        /// 때까지</b> 남는다. 그래서 "패널 사각형에서 한 픽셀도 넓지 않다"가 예전보다 훨씬 중요해졌다 —
        /// 테스트가 이 값을 패널 화면 사각형과 직접 대조한다.</para></summary>
        public Bounds ClickBlockerWorldBounds
            => _clickBlocker != null && _clickBlocker.enabled ? _clickBlocker.bounds : default;

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
            Debug.Log($"[정보창] 준비 완료({PanelWidth:F0}×{PanelHeightForTab(Tab.Equipment):F0}(외형 탭은 {PanelHeightForTab(Tab.Appearance):F0}) 화면 중앙, 3탭: 장비/외형/보관함, " +
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
            // 여는 그 순간은 정의상 조작 중이다 — 첫 커서 폴링(최대 0.05초)까지의 공백을 메운다.
            _lastSurfaceTouchTime = Time.unscaledTime;
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
        /// ★ 배타적 모달(2026-08-30) — 이 창이 뜨는 순간 <b>다른 모든 배타 표면</b>을 거둔다.
        /// <b>정리 책임을 여는 쪽 한 곳에만</b> 둔다. 진입점(부채꼴 [캐릭터] / 단축키 ⌃⌥⌘I / 우클릭
        /// 메뉴)마다 정리 코드를 흩뿌리면 네 번째 진입점이 생길 때 또 샌다 — 실제로 단축키 경로가
        /// 아무것도 닫지 않아 캔버스 3개(창 + 부채꼴 + 팝오버)가 동시에 뜨는 화면이 재현됐다.
        ///
        /// <para>★★ 2026-09-01 — <b>여기 있던 손으로 적은 목록을 통째로 걷어냈다</b>
        /// (사용자 신고 "케릭터창도 겹쳐서보이는 문제있고"). 종전 구현은
        /// <c>if (_menu != null) { _menu.ForceCloseAll(reason); return; }</c>로 <b>조기 반환</b>했고,
        /// 부채꼴이 있는 정식 조립에서는 그 아래 줄이 절대 실행되지 않았다. 그래서 목록에 빠져 있던
        /// <see cref="SettingsWindow"/>를 "아래에 한 줄 추가"하는 자연스러운 수정은 화면에서 아무
        /// 효과가 없었을 것이다. 목록과 조기 반환을 함께 없애는 것이 이 버그의 근본 수정이다 —
        /// 자세한 근거는 <see cref="IExclusiveSurface"/>.</para>
        /// </summary>
        private void CloseOverlappingSurfaces(string reason)
            => ExclusiveSurfaces.CloseAllExcept(this, reason);

        // ==================== 루프 ====================

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측
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

            // ★★ 프레임 페이싱 홀드는 여기가 아니라 TickGlobalPointer() 안에 있다 —
            //    "창이 열려 있는 동안"이 아니라 <b>"지금 이 창을 조작 중일 때"</b>만 걸어야 하기
            //    때문이다(근거: TickFramePacingHold 문서의 125분 실측). 커서 표본을 뜨는 곳이
            //    거기 하나뿐이라 판정도 그 자리에서 한다.

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
        /// 프레즌스 줄 — <b>좌측 컬럼에서 유일하게 움직이는 것</b>(2026-09-02부터).
        ///
        /// ============================================================================
        /// ★★ 여기 있던 초상화 포즈 갱신 한 줄을 걷어냈다 (docs/UX_FLOW.md 45-1)
        /// ============================================================================
        /// 사용자 신고: "캐릭터창에서 보이는 캐릭터는 장비 착용 모습<b>만</b> 적용되서 보여줘야하는데
        /// 가끔 움직임". "만"이 범위를 닫는다 — 액자의 주제는 "무엇을 걸쳤는가" 하나다.
        /// 옛 근거("그림과 문구를 같은 스냅샷에서 파생시켜 어긋남을 막는다")는 전수 대조로 반증됐다:
        /// 4버킷 그림이 28행 문구를 <b>실제로 그리는</b> 상태는 3개(10.7%)뿐이었다.
        /// 이제 <b>일치는 전부 글자가 진다</b> — 액자는 장비/해금/잉크/키에만 반응한다.
        ///
        /// ============================================================================
        /// ★ 그래서 이 줄에 최소 노출(hold)이 필요해졌다 — 그림을 멈춘 것의 직접 결과다
        /// ============================================================================
        /// 실측(45-3-b): 이 줄은 <b>분당 17.4~21.7회</b> 바뀌고, 폭주 구간에서는 2.11초 동안 문구가
        /// 4개 지나갔다(최단 노출 <b>0.22초</b>). 그림이 멈추면 사용자가 장비를 비교하며 쳐다보는
        /// 자리에서 <b>유일하게 깜빡이는 것</b>이 이 줄이 된다.
        ///
        /// 규칙은 셋뿐이다:
        /// <list type="number">
        ///   <item>상태가 바뀌면 <b>즉시</b> 쓴다(지연 0 — 거짓말을 만들지 않는다).</item>
        ///   <item>쓴 순간부터 <c>T_hold</c> 동안 바꾸지 않는다.</item>
        ///   <item>만료되면 <b>그 순간의 현재 상태를 다시 읽어</b> 필요하면 갱신한다
        ///         (놓치지 않는다 — 45-3-c의 검산에서 벽 타기 1.12초는 그대로 표시됐다).</item>
        /// </list>
        ///
        /// <c>T_hold</c>는 <b>새 상수를 만들지 않는다</b> — 말풍선이 이미 쓰는 가독예산
        /// (<see cref="StickMate.Dialogue.DialogueBudget.ReadingSeconds"/>)을 그대로 재사용한다.
        /// 새 숫자를 여기 적으면 "몇 초면 읽히는가"의 정의가 두 곳으로 갈라진다.
        /// 재는 대상은 <b>바뀌는 부분(상태 한 마디)</b>이다 — "지금  ·  " 접두는 한 번도 변하지 않아
        /// 눈이 다시 읽지 않는다.
        ///
        /// <para><b>원칙 1 위반이 아니다.</b> hold는 <b>확정된 과거 상태만</b> 쓰고 미래를 예고하지
        /// 않는다. 원칙 1이 금지하는 것은 "말해 놓고 안 하기"이지 "하고 나서 말하기"가 아니며,
        /// <see cref="StateLabel"/>은 그 자신의 문서가 밝히듯 <b>대사가 아니다</b>
        /// (<c>DialogueIntent</c>를 만들지 않는다).</para>
        ///
        /// <para><b>남는 대가(숨기지 않는다)</b>: hold 중에는 문구가 최대 <c>T_hold</c>만큼 낡는다.
        /// 그 대가는 <b>읽을 수 없는 문구</b>보다 작다 — 0.22초짜리 문구의 정보량은 0이다.</para>
        /// </summary>
        private void TickPresenceLine()
        {
            if (_presenceText == null) return;

            var machine = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.Machine : null;
            if (machine == null)
            {
                if (!_hasShownState) { WritePresence("—"); }
                return;
            }

            StickmanStateId id = machine.CurrentStateId;
            if (_hasShownState && id == _lastShownState) return;
            // hold가 살아 있으면 <b>아무것도 하지 않는다</b> — _lastShownState도 건드리지 않는다.
            // 만료되는 프레임에 이 함수가 다시 와서 그때의 현재 상태를 읽는 것이 규칙 3이다.
            if (_hasShownState && Time.unscaledTime < _presenceHoldUntil) return;

            _lastShownState = id;
            WritePresence(StateLabel(id));
        }

        /// <summary>프레즌스 줄에 실제로 쓰는 곳 <b>한 군데</b>. 여기서만 hold 시계를 다시 감는다 —
        /// 쓰는 곳과 시계를 감는 곳이 갈라지면 반드시 한쪽만 갱신된다.</summary>
        private void WritePresence(string label)
        {
            _presenceText.text = $"지금  ·  {label}";
            _hasShownState = true;
            _presenceHoldUntil = Time.unscaledTime + StickMate.Dialogue.DialogueBudget.ReadingSeconds(label);
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
            _presenceHoldUntil = 0f;   // 방금 연 창의 첫 문구는 지난 세션의 시계에 막히지 않는다.
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

            // 스탯 4행. 0인 항목은 숫자 대신 회색 "아직 없음"으로 — 0이 성취처럼 보이지 않게 한다.
            SetStat(0, $"{CharacterStatsModel.DaysTogether}일차", true);
            SetStat(1, CharacterStatsModel.FormatCompanionTime(), true);
            int ownedItems = ItemCatalog.UnlockedEquipmentCount(_config);
            SetStat(2, $"{ownedItems} / {ItemCatalog.EquipmentCount}종", ownedItems > 0);
            SetStat(3, CharacterStatsModel.TryGetArcheryAccuracy01(out float acc)
                ? $"{CharacterStatsModel.ArcheryBullseyes} / {CharacterStatsModel.ArcheryShots} ({acc * 100f:F0}%)"
                : "기록 없음", CharacterStatsModel.ArcheryShots > 0);
            // ※ 표시에서 빠진 칸: 넘어진 횟수(2026-09-01) / 격파 성공(2026-09-02).
            //    CharacterStatsModel.RagdollFalls·BattleWins 둘 다 값은 계속 살아 있다.
        }

        /// <summary>스탯 한 칸. <paramref name="value"/>가 null이면 회색 "아직 없음"으로 대신한다.</summary>
        private void SetStat(int index, string value, bool hasRecord)
        {
            if (index < 0 || index >= _statValues.Length || _statValues[index] == null) return;
            _statValues[index].text = value ?? "아직 없음";
            _statValues[index].color = UiChrome.InkTitle(hasRecord);
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

            // 이름은 상자(70pt)를 넘으면 말줄임한다 — Overflow로 흘리면 오른쪽 메타("착용 중")와
            // 물리적으로 겹친다(P0-5). 내용이 바뀐 순간에만 다시 계산한다(ItemCard.NameSource 문서).
            string wantedName = owned ? entry.DisplayName : "???";
            if (!string.Equals(card.NameSource, wantedName, System.StringComparison.Ordinal))
            {
                card.NameSource = wantedName;
                card.Name.text = UiChrome.Ellipsize(card.Name, wantedName, CardNameWidth);
            }
            card.Name.color = UiChrome.InkTitle(owned);

            if (!owned)
            {
                // "LV.20" — 잠긴 카드의 메타는 <b>언제 열리는지</b> 하나만 말한다.
                card.Meta.text = $"LV.{entry.RequiredLevel}";
                card.Meta.color = UiChrome.InkMeta;
                card.Surface.color = UiChrome.CardSurfaceMuted;
                card.Thumb.color = UiChrome.ThumbSurfaceLocked;
                // 잠김 = <b>무채색 실루엣</b>. 해금 전에 소재색을 미리 보여주면 잠금 연출이 무의미해진다.
                SetIconColor(card, iconSet, new Color(UiChrome.TextTertiary.r, UiChrome.TextTertiary.g,
                    UiChrome.TextTertiary.b, 0.34f));
            }
            else
            {
                card.Meta.text = worn ? "착용 중" : "보유";
                card.Meta.color = worn ? tint : UiChrome.InkMeta;
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

            // 카드 하단 버튼 — 이 창의 유일한 착용 손잡이(상세 패널은 읽기 전용이다).
            StyleActionButton(card.ActionSurface, card.ActionOutline, card.ActionLabel, card.ActionButton, owned, worn);
        }

        /// <summary>
        /// [착용]/[해제] 버튼 한 벌의 스타일 — 33-7-4의 상태 표. 상태→라벨/색 매핑이 존재하는
        /// <b>유일한 자리</b>다.
        ///
        /// <para>★ 2026-09-01 <b>강조 등급 재조정</b> — 예전에는 이 함수가 두 자리(카드 하단 / 상세 패널)를
        /// 강조 등급으로 나눠 칠했다. 사용자 신고("각 장비별 착용버튼으로 했는데 왜 옛날처럼 하단에
        /// 착용상자가 따로 있음?")로 <b>상세 패널의 중복 버튼을 걷어내면서</b> 자리가 하나로 줄었고,
        /// 등급 파라미터도 함께 지웠다 — 분기 하나짜리 등급은 다음 사람에게 "두 자리가 있다"는 거짓말이 된다.
        /// 이제 <b>카드 버튼이 이 창의 1차 행동</b>이다.</para>
        ///
        /// <para>★ 흰 채움으로 되돌리지 <b>않는다</b> — P0-4가 실측으로 걷어낸 이유가 살아 있다:
        /// 한 화면에 이 막대가 12개 뜨는데 유저가 고르는 대상은 <i>아이템</i>이다. <b>1차 행동이라는 것은
        /// 경쟁자가 없다는 뜻이지 가장 밝아야 한다는 뜻이 아니다.</b></para>
        ///
        /// <para>★★ <b>2026-09-02 — 그렇다고 이대로 둘 수도 없었다.</b> "조용한 칩"이 조용한 정도를
        /// 넘어 <b>면이 아예 없는</b> 지점까지 가 있었다(실측, 각 상태의 <b>진짜</b> 바탕 기준):
        /// <code>
        ///   착용 #32353C on #1B1F26 = 1.35 : 1      글리프 11.14 : 1
        ///   해제 #243143 on #1B1F26 = 1.26 : 1      글리프  7.16 : 1
        ///   잠김 #15181E on #15181E = <b>1.00 : 1</b>      글리프  5.73 : 1
        /// </code>
        /// 글자는 셋 다 잘 읽혔다 — <b>고칠 것은 잉크가 아니라 면</b>이다([✕]와 같은 결함, 같은 처방).
        /// 잠김이 1.00인 것은 잠긴 카드의 <b>바탕 자체</b>가 CardSurfaceMuted로 바뀌기 때문이다.</para>
        ///
        /// <para><b>어둡게 해서 구분할 수는 없다</b> — 카드 바탕이 이미 어두워 순검정까지 내려가도
        /// 최대 1.27:1이다. 3.0은 아래쪽에 존재하지 않는다. 그래서 면은 반드시 밝아지고, 두 활성
        /// 상태는 밝기가 아니라 <b>색상</b>으로 갈린다(<see cref="UiChrome.CardActionSurface"/> /
        /// <see cref="UiChrome.CardActionSurfaceWorn"/>, 각각 4.49 / 4.48 : 1).</para>
        ///
        /// <para><b>P0-4 가드는 그대로 통과한다</b>(이게 핵심이다): 새 두 면의 휘도는 0.2355 / 0.2349로,
        /// 흰 채움과 카드 바탕의 중간값 0.4584의 <b>절반</b>이다. 접근성 하한을 넘기면서도 카드에서
        /// 가장 밝은 것은 여전히 아이템 쪽이다.</para>
        ///
        /// <para>색은 전부 불투명값이다 — 투명 오버레이에서 알파를 겹치면 그 자리만 뒤 창이 비친다
        /// (UiChrome '알파 채널의 법칙'). 테두리도 생 <c>CardBorder</c>가 아니라
        /// <see cref="UiChrome.Flatten"/>을 거친다.</para>
        /// </summary>
        private static void StyleActionButton(Image surface, Image outline, Text label, Button button, bool owned, bool worn)
        {
            // ★ 잠긴 칩은 <b>실제로</b> 비활성이다 — 클릭은 예전부터 무시됐다(OnActionClicked).
            //   이 한 줄이 있어야 WCAG 2.2 1.4.11의 "inactive user interface components" 면제를
            //   정당하게 받는다. 없으면 그 칩은 1.00:1짜리 <b>활성</b> 컨트롤로 남는다.
            if (button != null) button.interactable = owned;

            // ★ 2026-09-02 — <b>면</b>을 고친다. 잉크는 멀쩡했다(11.14 / 7.16 / 5.73:1).
            //   고치기 전 면은 1.35 / 1.26 / <b>1.00</b> : 1 이었고, 셋 다 자체 하한 3.0 미달이다.
            //   특히 잠김은 칩과 카드 바탕이 <b>같은 RGB</b>였다 — 오늘 밤 [✕](1.00:1)와 같은 결함이다.
            //   면을 먼저 정하고 잉크를 그 면에서 <b>파생</b>시킨다. 순서가 뒤집히면 둘이 갈라진다.
            Color face = !owned ? UiChrome.CardSurfaceMuted
                : worn ? UiChrome.CardActionSurfaceWorn
                       : UiChrome.CardActionSurface;

            if (surface != null) surface.color = face;

            if (label != null)
            {
                // 잠긴 카드에 "LV.20"이라고 적지 않는다 — 바로 위 메타 줄이 이미 그 숫자를 말하고 있다.
                label.text = !owned ? "잠김" : worn ? "해제" : "착용";
                // 면에서 파생 — 밝은 면 위에서는 InkOnSurface가 알아서 어두운 잉크로 뒤집는다.
                label.color = UiChrome.InkOnSurface(face,
                    owned ? UiChrome.InkRole.Title : UiChrome.InkRole.Meta, enabled: owned);
            }
            if (outline != null)
            {
                // ★ 생 CardBorder/AccentBorder(α<1)를 그대로 얹지 않는다 — 그 화소의 창 알파가
                //   0.91로 내려가 <b>유저의 바탕화면이 9% 비친다</b>(어두운 배경일수록 더 안 보였다).
                //   Flatten이 겉보기 색을 그대로 두고 α=1만 보장한다.
                outline.color = UiChrome.Flatten(
                    !owned ? UiChrome.CardBorder : worn ? UiChrome.AccentBorder : UiChrome.CardBorder,
                    face);
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

        /// <summary>선택 상세 패널(33-7-4). <b>읽기 전용</b>이다 — 이름·슬롯/보유 상태·설명, 그리고
        /// 잠겼다면 <b>왜 잠겼는지</b>를 말하는 것이 전부고, 옷을 갈아입히는 것은 카드 하단 버튼이 한다.
        ///
        /// <para>★ 2026-09-01 — 여기 있던 [착용]/[해제] 버튼을 걷어냈다(사용자 신고: "각 장비별
        /// 착용버튼으로 했는데 왜 옛날처럼 하단에 착용상자가 따로 있음?"). 카드 버튼을 넣으면서 이쪽을
        /// 안 걷어내 <b>같은 동작을 하는 버튼이 두 개</b>였다.</para>
        ///
        /// <para>★ <b>패널 자체는 남긴다.</b> 잠긴 아이템도 선택은 되고, 이 패널이 "왜 잠겼는지"를 알 수
        /// 있는 <b>유일한</b> 경로다 — 카드에는 이름(<c>???</c>)과 요구 레벨 숫자뿐이고 설명문이 없다.
        /// 버튼이 사라진 자리에는 아무것도 채우지 않는다(메타 줄은 172..502pt라 원래 닿지 않던 칸이다).</para>
        /// </summary>
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
                _detailName.color = UiChrome.InkTitle(owned);
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
                _detailBody.color = UiChrome.InkBody(owned);
            }
        }

        // ==================== 보관함(가상 목록) ====================

        /// <summary>목록의 논리적 줄 수 = 헤더 2줄 + 카탈로그 전체(장비 42 + 행동 12 = 54).
        /// <para>2026-09-02 격파 놀이 삭제로 행동이 13 → 12가 됐다.</para>
        /// <para>★ 2026-09-02 — 여기 "장비 32"라고 적혀 있었다. 실제는 <b>42종</b>이고
        /// (<c>Resources/Items/*.asset</c> 42개), 페이지 수가 32든 42든 3이라 <b>화면에는 티가 나지
        /// 않았다</b>. 숫자를 손으로 적지 않는 것이 원칙이지만 주석은 예외가 없어 이렇게 샌다 —
        /// 다음 사람이 이 숫자로 계산하면 10종을 잃는다.</para></summary>
        private static int InventoryLineCount => ItemCatalog.Count + 2;

        /// <summary>논리적 줄 번호 -> 카탈로그 인덱스. 헤더면 -1.
        /// 순서: [걸치는 것] 헤더 → 장비 전부 → [할 줄 아는 것] 헤더 → 행동 전부.
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
                // 도트만 글자가 아니다 — 나머지 셋은 전부 같은 사다리에서 나온다.
                view.Dot.color = entry.Slot.HasValue
                    ? (worn ? UiChrome.CategoryTint(entry.Slot.Value)
                            : owned ? UiChrome.NonTextMuted : UiChrome.TrackBackground)
                    : UiChrome.NonTextMuted;
                view.Title.color = UiChrome.InkTitle(owned);
                view.Subtitle.color = UiChrome.InkMeta;
                view.Description.color = UiChrome.InkBody(owned);
                view.StatusSlot.color = worn && entry.Slot.HasValue ? UiChrome.CategoryTint(entry.Slot.Value)
                    : UiChrome.InkMeta;
            }

            if (_pageIndicator != null)
            {
                // 마지막 페이지는 스크롤이 상한에 걸려 한 페이지 분량이 채 안 되므로 올림으로 센다
                // (나눗셈으로만 세면 마지막 페이지에서 "2/3"처럼 어긋난다 — 육안 검증에서 확인).
                int page = Mathf.CeilToInt(_inventoryScroll / (float)InventoryVisibleRows) + 1;
                int pages = Mathf.Max(1, Mathf.CeilToInt((float)InventoryLineCount / InventoryVisibleRows));
                // ★ 2026-09-02 — 예전에는 $"{page}\n/\n{pages}"였다. 폭 부족 줄바꿈이 아니라
                //   <b>명시적 개행</b>이었고(이 Text는 HorizontalWrapMode.Overflow라 애초에 줄바꿈을
                //   하지 않는다), 세로로 쌓인 1 / 3은 "3 중 1"이 아니라 <b>분수 ⅓</b>으로 읽혔다.
                //   깨진 글자가 아니라 <b>다른 뜻</b>이라 더 나쁘다(45-9-a).
                _pageIndicator.text = $"{page} / {pages}";
            }

            // 칩의 겉모습과 클릭 처리가 <b>같은 하나</b>(CanScrollInventory)를 본다 — 두 벌로 두면
            // 반드시 한쪽만 갱신되고, 그게 곧 표시-실제 불일치다(SettingsWindow.SyncPageButtons와 같은 규칙).
            ApplyPagerEnabled(_pageUpOutline, _pageUpLabel, CanScrollInventory(-1));
            ApplyPagerEnabled(_pageDownOutline, _pageDownLabel, CanScrollInventory(+1));

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

        /// <summary>그 방향으로 <b>실제로 움직일 수 있는가</b>. 겉모습과 클릭 처리가 이 하나를 본다.
        ///
        /// <para>★ 설정창(<c>SettingsWindow.CanScroll</c>)에서 그대로 베끼지 <b>않았다</b>: 그쪽은
        /// 연속 스크롤(<c>float</c>)이라 <c>0.5f</c> 여유를 두지만 여기는 <b>줄 단위 정수</b>다.
        /// 정수에는 부동소수 경계가 없으므로 그 여유를 옮겨 적으면 뜻 없는 마법수가 하나 는다.</para>
        ///
        /// <para>레일을 <b>숨기는 분기도 넣지 않았다</b>. 카탈로그가 <see cref="InventoryVisibleRows"/>줄
        /// 이하로 줄면 양쪽 칩이 죽고 지시자가 <c>1 / 1</c>이 되는 것으로 충분하고, 그게 더 정직하다
        /// (레일 양 끝 캡이라 하나가 사라지면 막대 자체가 고장 난 것처럼 보인다).</para></summary>
        private bool CanScrollInventory(int direction)
            => direction < 0 ? _inventoryScroll > 0 : _inventoryScroll < MaxInventoryScroll;

        /// <summary>끝에 닿은 칩을 <b>죽이되 지우지 않는다</b>.
        ///
        /// <para>바꾸는 것은 <b>테두리와 글리프</b>뿐이고 <b>면은 그대로</b>다. 그리고 합성 바탕이
        /// 설정창과 <b>다르다</b> — 저쪽 칩 면은 <c>CardSurfaceMuted</c>, 이쪽은 <c>CardSurface</c>다.
        /// <c>CardBorder</c>/<c>Divider</c>는 알파 색이라 <b>어느 면 위에 올리느냐로 결과가 달라진다</b>.
        /// 설정창의 결과색을 그대로 옮기면 테두리만 미묘하게 어긋난다(14.5-a).</para>
        ///
        /// <para>글리프는 산문이 아니라 <b>기호</b>이므로 아이콘 사다리(<see cref="UiChrome.InkIcon"/>)를
        /// 쓴다.</para></summary>
        private static void ApplyPagerEnabled(Image outline, Text glyph, bool enabled)
        {
            if (outline == null || glyph == null) return;

            Color edge = UiChrome.Flatten(enabled ? UiChrome.CardBorder : UiChrome.Divider,
                UiChrome.CardSurface);
            if (outline.color != edge) outline.color = edge;

            Color ink = UiChrome.InkIcon(enabled);
            if (glyph.color != ink) glyph.color = ink;
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
            ApplyTabDetailPlacement();   // 창 높이보다 먼저 — ApplyTabDetailPlacement 문서 참고.

            bool sections = _tab != Tab.Inventory;
            if (_sectionPage != null) _sectionPage.SetActive(sections);
            if (_inventoryPage != null) _inventoryPage.SetActive(!sections);

            for (int i = 0; i < TabCount; i++)
            {
                bool active = i == (int)_tab;
                if (_tabLabels[i] != null)
                {
                    _tabLabels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                    _tabLabels[i].color = UiChrome.InkTab(active);
                }
                if (_tabUnderlines[i] != null)
                {
                    _tabUnderlines[i].color = active ? UiChrome.TextPrimary : Color.clear;
                }
            }
        }

        /// <summary>카드 <b>본체</b> 클릭 = <b>선택</b>(아래 상세 패널이 그 아이템을 설명한다).
        /// 착용/해제는 <b>그 카드 하단의 버튼</b>만 한다 — "고른다"와 "입는다"를 같은 클릭에 겹치면,
        /// 설명을 읽으려고 눌렀을 뿐인데 옷이 갈아입혀진다.
        /// <para>2026-09-01 이전에는 이 자리에 "착용은 상세 패널의 버튼 하나로만"이라고 적혀 있었다.
        /// 그 버튼은 카드 버튼이 들어온 뒤로 중복이었고 지금은 없다.</para></summary>
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

        /// <summary>착용/해제를 <b>실제로 수행</b>하는 단 하나의 자리. 진입점은
        /// <see cref="OnCardEquipClicked"/> 하나뿐이다(상세 패널의 중복 버튼은 2026-09-01에 걷어냈다).
        /// 선택 상태(<c>_selectedSlot</c>/<c>_selectedItem</c>)를 읽으므로 호출 전에 그 둘이 대상 아이템을
        /// 가리키고 있어야 한다.</summary>
        private void OnActionClicked()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(_selectedSlot, _selectedItem);
            if (entry == null) return;

            if (!entry.IsOwned(_config))
            {
                // 33-7-4: 잠긴 항목은 버튼 클릭만 무시한다(선택은 되고 설명도 보인다).
                Debug.Log($"[{TabNames[(int)_tab]}] {entry.DisplayName}{KoreanParticle.Topic(entry.DisplayName)} 아직 잠겨 있습니다 — " +
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
                (replaced != null
                    ? $"(같은 카테고리의 {replaced.DisplayName}{KoreanParticle.Topic(replaced.DisplayName)} 자동 해제)"
                    : string.Empty) +
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

            // 홀드 판정도 이 가드 뒤에 있다 — 전역 포인터 서비스가 없으면 커서를 관측할 수단이
            // 자체가 없다. 그 환경(에디터/Null 서비스)에서는 적응형 페이싱도 함께 꺼져 있으므로
            // 홀드가 없어서 생기는 손해가 없다.

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
            TickFramePacingHold(hasCursor, cursor);

            // 끄는 중에는 카드를 다시 칠하지 않는다(패널 이동도, 캐러셀 밀기도 마찬가지다).
            if (hasCursor && !_draggingPanel && !_carouselMoved) UpdateHover(cursor);

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            ProcessPointer(left, cursor, hasCursor);
        }

        /// <summary>
        /// "지금 이 창을 <b>조작 중</b>인가"를 프레임 페이싱에 알린다. 판정은 플랫폼 중립 한 곳
        /// (<see cref="FramePacingPolicy.ShouldHoldForSurface"/>)이고 여기서는 사실만 모은다.
        ///
        /// <para><b>★ 2026-09-01 — 원래 이 홀드는 <c>Update()</c>에서 무조건 걸려 있었고, 그것이
        /// 적응형 절전을 통째로 죽였다.</b> 사용자 로그에서 정보창이 <b>125분</b> 열려 있는 동안
        /// 등급 전이가 0회 / 활성 등급 체류 100%였고, 창을 닫은 직후 전이가 재개되며 GPU 점유 추정이
        /// 약 2.5배 떨어졌다. "정보창은 수명이 짧다"는 전제가 실측으로 반증된 것이다. 자리비움(3분
        /// 무입력)이 이 홀드를 이기게 돼 있지만, 사용자가 <b>다른 앱에서 계속 타이핑</b>하면 그
        /// 시계는 3분에 닿지 않는다 — 홀드를 깨는 경로가 실질적으로 없었다.</para>
        ///
        /// <para>반응성을 해치지 않는 근거(왜 이 경계인가)는 정책 함수 문서에 있다. 요약하면:
        /// 절감 등급은 게임 루프가 아니라 렌더 간격만 바꾸므로 <b>입력 처리 주기는 그대로</b>이고,
        /// 커서가 창에 닿는 순간의 복귀 지연이 최대 0.07초(폴링 0.05초 + 1프레임)다.</para>
        /// </summary>
        private void TickFramePacingHold(bool hasCursor, Vector2 cursor)
        {
            // 커서가 창 밖으로 나가도 계속되는 조작들 — 이것들은 사각형 판정으로 잡을 수 없다.
            bool manipulating = _draggingPanel || _carouselSection >= 0 || _editingName;
            bool cursorOver = hasCursor && RectContainsScreenPoint(_panel, cursor);
            if (manipulating || cursorOver) _lastSurfaceTouchTime = Time.unscaledTime;

            if (FramePacingPolicy.ShouldHoldForSurface(cursorOver, manipulating,
                    Time.unscaledTime - _lastSurfaceTouchTime))
            {
                FramePacing.HoldActiveForInteraction();
            }
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

        /// <summary>[✕] 버튼의 화면 사각형. ★ 2026-09-02부터 창 밖 클릭이 닫지 않으므로 <b>이 앱에서
        /// 이 창을 닫는 유일한 마우스 경로</b>다(Esc/Cmd+W는 포커스 없는 오버레이라 못 받는다 —
        /// <see cref="UiChrome"/> "창을 닫는 법" 절). 그래서 테스트가 좌표를 손으로 적지 않고
        /// 반드시 이 자리를 눌러 본다.</summary>
        public Rect CloseButtonScreenRect => RawScreenRectOf(_closeRect);

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
                // ★ 클릭 경로가 <b>둘</b>이다(Button.onClick + 이 폴링). 한쪽만 가드하면 다른 쪽이
                //   그대로 뚫린다 — 두 경로가 같은 CanScrollInventory를 본다(45-9-b ④).
                if (ContainsScreenPoint(_pageUpRect, cursor))
                {
                    if (CanScrollInventory(-1) && TryClaimAction("pageUp")) ScrollInventory(-1);
                    return;
                }
                if (ContainsScreenPoint(_pageDownRect, cursor))
                {
                    if (CanScrollInventory(+1) && TryClaimAction("pageDown")) ScrollInventory(+1);
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

            // ★ 2026-09-02 사용자 지시 — 여기까지 왔다는 것은 어떤 컨트롤에도 맞지 않았다는 뜻이고,
            //   <b>패널 안이든 밖이든 아무 일도 하지 않는다</b>. 2026-08-30에 신설했던 "창 밖 클릭"
            //   탈출구(33-7-9 ③)를 사용자 신고로 걷어냈다: "캐릭터창이나 다른 메뉴창들이 떠있을때
            //   바탕화면을 클릭하면 꺼지는데 안꺼지고 사용자가 닫기전에는 안꺼져야함".
            //   근거와 그 대가는 <see cref="UiChrome"/>의 "창을 닫는 법" 절 한 곳에 모아 뒀다.
            //
            //   ★ 그 클릭을 <b>먹지는 않는다</b>: 차단막(<see cref="_clickBlocker"/>)은 패널 사각형만
            //     덮으므로 창 밖 좌표에는 콜라이더가 없고, 히트테스트(hitTestType=Raycast)가 그대로
            //     밑의 앱에 넘긴다. "안 닫히는 것"과 "클릭을 뺏는 것"은 다른 문제이고, 후자면 원칙 2
            //     위반이다(Tests/PlayMode/SurfaceOutsideClickTests가 그 경계를 픽셀로 잠근다).
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

        /// <summary>그 카드가 가리키는 슬롯. 섹션→슬롯 규칙(<see cref="SectionSlot"/>)을 테스트가
        /// <b>베껴 적지 않게</b> 하는 창구다 — 카테고리를 더하거나 지우면 그 규칙만 바뀌어야 한다.
        /// 카드가 없으면 false.</summary>
        public bool TryGetCardSlotForTests(int index, out EquipmentSlot slot)
        {
            ItemCard card = CardAt(index);
            slot = card != null ? SectionSlot(_tab, card.Section) : default;
            return card != null;
        }

        /// <summary>카드의 <b>잘리기 전</b> 화면 사각형. 캐러셀 밖으로 밀려난 카드도 값이 나온다 —
        /// "보이지 않는데 눌리는가"를 재려면 그 자리를 알아야 한다.</summary>
        public Rect CardRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Rect);

        /// <summary>카드가 캐러셀 마스크에 <b>잘리고 남은</b> 화면 사각형(전부 잘리면 넓이 0).
        /// "반쯤 걸친 카드가 있는가" — 즉 이 창의 유일한 발견 단서(<see cref="CarouselViewportWidth"/>)가
        /// 실제로 화면에 있는가를 회귀 테스트가 숫자로 확인하는 창구다.</summary>
        public Rect CardVisibleScreenRect(int index) => VisibleScreenRectOf(CardAt(index)?.Rect);

        /// <summary>카드 하단 [착용]/[해제] 버튼의 잘리기 전 화면 사각형.</summary>
        public Rect CardEquipButtonRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.ActionRect);

        /// <summary>그 카드의 [착용] 버튼이 지금 화면에 보이는 넓이 비율(0 = 통째로 잘림).
        /// <para>★ 2026-09-01 — 상세 패널 버튼을 걷어내면서 <c>ActionButtonVisibleFraction</c>이 갈 곳을
        /// 잃었다. "보이지 않는 것은 눌리지 않는다"(R2 M3)는 그 버튼의 성질이 아니라 <b>이 창의 규칙</b>이라,
        /// 살아남은 버튼 쪽으로 관측 창구를 옮겨 회귀를 그대로 유지한다.</para></summary>
        public float CardEquipButtonVisibleFraction(int index)
        {
            RectTransform rt = CardAt(index)?.ActionRect;
            if (rt == null || !rt.gameObject.activeInHierarchy) return 0f;
            rt.GetWorldCorners(_corners);
            float full = (_corners[2].x - _corners[0].x) * (_corners[2].y - _corners[0].y);
            if (full <= 0f) return 0f;
            Rect visible = VisibleScreenRectOf(rt);   // _corners를 다시 쓰므로 full을 먼저 잰다.
            return Mathf.Clamp01(visible.width * visible.height / full);
        }

        /// <summary>지금 이 지점을 누르면 그 카드의 [착용] 버튼이 반응하는가(마스크까지 본 판정).</summary>
        public bool IsCardEquipButtonHittableAt(int index, Vector2 cursorUnityScreen)
            => ContainsScreenPoint(CardAt(index)?.ActionRect, cursorUnityScreen);

        // ---- P0-4 / P0-5 회귀용 관측 창구 ----

        /// <summary>카드 하단 버튼의 <b>표면색</b>. P0-4 회귀가 "카드 버튼이 화면에서 가장 밝은 면이
        /// 아니다"를 이 값으로 확인한다.</summary>
        public Color CardActionSurfaceColor(int index) => CardAt(index)?.ActionSurface?.color ?? Color.clear;

        /// <summary>카드 하단 버튼의 <b>라벨색</b>. 조용해진 표면 위에서도 읽히는지 확인한다.</summary>
        public Color CardActionLabelColor(int index) => CardAt(index)?.ActionLabel?.color ?? Color.clear;

        /// <summary>상세 패널 안에 살아 있는 <see cref="Button"/> 수. 회귀 테스트가 "걷어낸 중복 착용
        /// 버튼이 되살아나지 않았다"를 <b>색이나 라벨이 아니라 존재 여부</b>로 확인하는 창구다.
        /// 패널을 못 찾으면 −1(관측 전제 자체가 깨진 것과 0을 구별한다).
        /// <para>진단/테스트 전용 — <c>GetComponentsInChildren</c>은 할당하므로 매 프레임 경로에서
        /// 부르지 않는다(상주 앱 규약).</para></summary>
        public int DetailPanelButtonCountForTests
            => _sectionDetailRect != null ? _sectionDetailRect.GetComponentsInChildren<Button>(true).Length : -1;

        /// <summary>상세 패널이 지금 말하고 있는 이름 — 잠긴 아이템이면 <c>???</c>.</summary>
        public string DetailNameTextForTests => _detailName != null ? _detailName.text : null;

        /// <summary>상세 패널 메타 줄(<c>카테고리 · 착용 중|보유 중|Lv.n에 열림</c>).</summary>
        public string DetailMetaTextForTests => _detailMeta != null ? _detailMeta.text : null;

        /// <summary>상세 패널 설명문 — 잠긴 아이템이면 <b>왜 잠겼는지</b>가 여기에만 있다.</summary>
        public string DetailBodyTextForTests => _detailBody != null ? _detailBody.text : null;

        /// <summary>화면 픽셀 ÷ 이 값 = 캔버스 포인트. 테스트가 화면 사각형을 pt로 되돌릴 때 쓴다.</summary>
        public float CanvasScaleForTests => CanvasScale();

        /// <summary>세로 한 칸(카테고리 섹션)의 높이. 창 높이가 섹션 수에서 파생되는지 확인할 때 쓴다.</summary>
        public float SectionStepPoints => SectionStep;

        /// <summary>카드 이름 상자 / 메타 상자의 화면 사각형(잘리기 전).</summary>
        public Rect CardNameRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Name?.rectTransform);

        public Rect CardMetaRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Meta?.rectTransform);

        /// <summary>카드 이름이 <b>실제로 그려질 때</b> 차지하는 폭(캔버스 포인트). 상자 폭이 아니라
        /// 폰트가 잰 값이라, 말줄임이 안 걸리면 상자를 넘는 것이 이 값에서 바로 보인다.</summary>
        public float CardNameInkWidthPoints(int index)
        {
            Text t = CardAt(index)?.Name;
            return t != null ? t.preferredWidth : 0f;
        }

        /// <summary>카드에 지금 표시된 이름(말줄임이 걸렸으면 잘린 쪽).</summary>
        public string CardNameTextForTests(int index) => CardAt(index)?.Name?.text ?? string.Empty;

        /// <summary>말줄임 전 원본 이름.</summary>
        public string CardNameSourceForTests(int index) => CardAt(index)?.NameSource ?? string.Empty;

        /// <summary>캐러셀 한 줄(잡고 미는 자리)의 화면 사각형.</summary>
        public Rect CarouselRowScreenRect(int section)
            => RawScreenRectOf(section >= 0 && section < _sections.Length ? _sections[section]?.RowRect : null);

        /// <summary>섹션 헤더의 "n / 6" 카운터 사각형. 이 창 오른쪽 열의 <b>오른쪽 끝선</b>을 정의하는
        /// 요소이고, 카드줄 바로 위에 있다 — 회귀 테스트가 그 끝선을 숫자로 베끼지 않고 물어보는 통로.</summary>
        public Rect SectionCountScreenRect(int section)
            => RawScreenRectOf(section >= 0 && section < _sections.Length
                ? _sections[section]?.Count?.rectTransform : null);

        /// <summary>지금 밀려 있는 양(캔버스 포인트, 왼쪽으로 밀면 음수).</summary>
        public float CarouselOffsetPoints(int section)
        {
            SectionView view = section >= 0 && section < _sections.Length ? _sections[section] : null;
            return view != null && view.Content != null ? view.Content.anchoredPosition.x : 0f;
        }

        /// <summary>이 카테고리에서 밀 수 있는 최대치(양수). 0이면 카드가 화면에 다 들어온다는 뜻이다.</summary>
        public float CarouselMaxScrollPoints(int section)
            => MaxCarouselScroll(section >= 0 && section < _sections.Length ? _sections[section] : null);

        // ==================== P0-1 회귀용 관측 창구 ====================

        // ==================== 진단/테스트 전용 — 프레즌스 줄 / 보관함 레일 (2026-09-02) ====================

        /// <summary>프레즌스 줄이 <b>지금 화면에 쓰고 있는</b> 문자열. hold 회귀가 이 값의 변화 횟수를 센다.</summary>
        public string PresenceTextForTests => _presenceText != null ? _presenceText.text : null;

        /// <summary>이 창이 쓰는 초상화 촬영장. "액자에 상태가 도달하지 않는다"를 재는 창구다 —
        /// 테스트가 씬 전체를 뒤져 촬영장 두 개(정보창/호버 패널) 중 어느 쪽인지 헷갈릴 일이 없다.</summary>
        public CharacterPortraitStage PortraitStageForTests => _stage;

        /// <summary>보관함 페이지 지시자 문자열.</summary>
        public string PageIndicatorTextForTests => _pageIndicator != null ? _pageIndicator.text : null;

        /// <summary>지시자가 <b>실제로 그려질 때</b> 차지하는 폭(캔버스 포인트) — 폰트가 잰 값이다.
        /// 설계가 Arial advance 0.556em을 가정해 19.46pt로 계산했는데, 그 가정을 여기서 <b>실제 폰트로</b>
        /// 확인한다(레일 폭 <see cref="InventoryRailWidthPoints"/>를 넘으면 그때가 진짜 줄바꿈 문제다).</summary>
        public float PageIndicatorInkWidthPoints => _pageIndicator != null ? _pageIndicator.preferredWidth : 0f;

        /// <summary>지시자 상자의 화면 사각형(잘리기 전). "허공에 뜨지 않았는가"를 [▲]와의 거리로 잰다.</summary>
        public Rect PageIndicatorRawScreenRect
            => RawScreenRectOf(_pageIndicator != null ? _pageIndicator.rectTransform : null);

        /// <summary>페이지 칩의 화면 사각형. <paramref name="direction"/>이 음수면 [▲], 양수면 [▼].</summary>
        public Rect PagerChipRawScreenRect(int direction)
            => RawScreenRectOf(direction < 0 ? _pageUpRect : _pageDownRect);

        /// <summary>페이지 칩 글리프 색 — 죽은 칩과 산 칩이 <b>실제로 다른지</b>를 재는 창구.</summary>
        public Color PagerGlyphColorForTests(int direction)
        {
            Text t = direction < 0 ? _pageUpLabel : _pageDownLabel;
            return t != null ? t.color : Color.clear;
        }

        /// <summary>페이지 칩 테두리 색.</summary>
        public Color PagerOutlineColorForTests(int direction)
        {
            Image i = direction < 0 ? _pageUpOutline : _pageDownOutline;
            return i != null ? i.color : Color.clear;
        }

        /// <summary>레일 폭(캔버스 포인트). 테스트가 24를 베껴 적지 않게 하는 창구다.</summary>
        public float InventoryRailWidthPoints => InventoryRailWidth;

        /// <summary>지금 보관함 스크롤(줄 단위)과 그 상한 — 칩의 겉모습이 <b>이 값에서</b> 나오는지 확인한다.</summary>
        public int InventoryScrollForTests => _inventoryScroll;

        public int MaxInventoryScrollForTests => MaxInventoryScroll;

        /// <summary>두 클릭 경로(<see cref="BuildPagerButton"/>의 <c>onClick</c>과 <see cref="FeedClick"/>의
        /// 폴링)가 <b>둘 다 보는</b> 그 판정. 칩의 겉모습도 여기서 나오므로, 테스트는 "겉모습 == 이 값"을
        /// 확인하는 것만으로 <b>표시-실제 일치</b>를 잠글 수 있다.</summary>
        public bool CanScrollInventoryForTests(int direction) => CanScrollInventory(direction);

        /// <summary>진단/테스트 전용 — 페이지 이동을 <b>클릭 핸들러가 부르는 바로 그 함수</b>로 부른다.
        ///
        /// <para>★ 왜 클릭 대신 이것이 필요한가(2026-09-02 실측): 배치모드 PlayMode의 화면은
        /// <b>640×480</b>이라 880pt 창이 608pt로 줄고, 우측 레일(패널 좌단 기준 x≈850)이
        /// <c>Body</c> 마스크(16..624) <b>밖으로 통째로 잘린다</b>. 그래서 그 자리는
        /// <b>물리적으로 눌리지 않는다</b>("보이지 않는 것은 눌리지 않는다" 규칙이 정상 작동한 결과다).
        /// 클릭으로 검증하려 들면 테스트는 초록도 빨강도 아닌 <b>거짓 빨강</b>을 낸다.</para>
        ///
        /// <para>가드는 여기서 재현하지 않는다 — 세 번째 사본을 만들면 그것이 곧 다음 결함이다.
        /// 가드는 <see cref="CanScrollInventoryForTests"/>로 따로 확인한다.</para></summary>
        public void ScrollInventoryForTests(int direction) => ScrollInventory(direction);

        /// <summary>탭 버튼의 화면 사각형 — 테스트가 <b>실제 클릭 경로</b>로 탭을 누를 수 있게 연다
        /// (<c>_tabRects</c>를 리플렉션으로 뒤지던 관례를 대체한다).</summary>
        public Rect TabScreenRect(int index)
            => RawScreenRectOf(index >= 0 && index < _tabRects.Length ? _tabRects[index] : null);

        /// <summary>지금 탭이 실제로 보여주는 카테고리 섹션 수(<see cref="Tab.Inventory"/>면 0).</summary>
        public int VisibleSectionCount => _tab == Tab.Inventory ? 0 : SectionCountForTab(_tab);

        /// <summary>지금 탭에서 창이 목표로 하는 높이(캔버스 포인트). 애니메이션 중인 실제 높이는
        /// <see cref="PanelSizePoints"/>가 준다 — 둘을 나눠 두어야 "다 줄었는가"를 기다릴 수 있다.</summary>
        public float TargetPanelHeightPoints => PanelHeightForTab(_tab);

        /// <summary>높이 애니메이션이 지금 도달한 값(<b>화면 클램프 전</b>).
        /// <para><see cref="PanelSizePoints"/>는 <see cref="ClampPanelToScreen"/>이 화면 높이로 자른
        /// <b>뒤</b>의 값이라, 화면이 낮은 실행 환경(배치모드 등)에서는 목표에 영원히 닿지 않는다 —
        /// "애니메이션이 끝났는가"를 그걸로 판정하면 테스트가 환경에 따라 거짓 실패한다.</para></summary>
        public float AnimatedPanelHeightPoints => _panelHeightPoints;

        /// <summary>
        /// ★ <b>마지막 카드 줄 아래 끝</b>과 <b>상세 패널 위 끝</b> 사이의 빈 높이(캔버스 포인트).
        ///
        /// <para>P0-1이 고친 결함이 정확히 이 값이었다: [장비](섹션 4개)에서는 20pt인데
        /// [외형](섹션 3개)에서는 <b>176pt</b>였다 — 없는 4번째 섹션의 자리를 예약했기 때문이다.
        /// 회귀 테스트는 "두 탭에서 이 값이 같다"를 본다. 숫자를 베끼지 않고 <b>탭끼리 비교</b>하므로
        /// 상수를 바꿔도 테스트가 따라온다.</para>
        /// </summary>
        public float SectionsToDetailGapPoints
        {
            get
            {
                if (_tab == Tab.Inventory) return float.NaN;
                int last = SectionCountForTab(_tab) - 1;
                if (last < 0 || last >= _sections.Length) return float.NaN;
                Rect row = RawScreenRectOf(_sections[last]?.RowRect);
                Rect detail = RawScreenRectOf(_sectionDetailRect);
                if (row.height <= 0f || detail.height <= 0f) return float.NaN;
                return (row.yMin - detail.yMax) / CanvasScale();   // 화면 y는 위가 양수.
            }
        }

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
            TickPanelHeight();
            float height = Mathf.Min(_panelHeightPoints, Mathf.Max(MinPanelHeight, Screen.height / scaleFactor - ScreenMargin * 2f));
            float width = Mathf.Min(PanelWidth, Mathf.Max(MinPanelWidth, Screen.width / scaleFactor - ScreenMargin * 2f));
            if (!Mathf.Approximately(_panel.sizeDelta.x, width) || !Mathf.Approximately(_panel.sizeDelta.y, height))
            {
                _panel.sizeDelta = new Vector2(width, height);
                SyncActionReachability();
            }

            Vector2 clamped = ClampPanelPosition(_panel.anchoredPosition, scaleFactor);
            if (clamped != _panel.anchoredPosition) _panel.anchoredPosition = clamped;
        }

        /// <summary>
        /// ★ P0-1 — 탭이 요구하는 높이로 창을 <b>부드럽게</b> 옮긴다. 창은 화면 중앙 고정(피벗 0.5)이라
        /// 위아래로 균등하게 줄어든다.
        /// <para>새 문자열/객체를 만들지 않는다 — 상주 앱의 Update 경로다.</para>
        /// </summary>
        private void TickPanelHeight()
        {
            float target = PanelHeightForTab(_tab);
            if (_panelHeightPoints <= 0f) { _panelHeightPoints = target; return; }   // 첫 프레임은 즉시.
            if (Mathf.Approximately(_panelHeightPoints, target)) { _panelHeightPoints = target; return; }

            // 0.12초에 <b>가장 큰 단(SectionStep)</b>을 지나가는 속도. 단이 작으면 그만큼 빨리 끝난다.
            float speed = SectionStep / PanelHeightAnimateSeconds;
            _panelHeightPoints = Mathf.MoveTowards(_panelHeightPoints, target,
                speed * Mathf.Max(0f, Time.unscaledDeltaTime));
        }

        /// <summary>탭이 요구하는 자리로 상세 패널을 옮긴다. 창 높이는 <see cref="TickPanelHeight"/>가
        /// 뒤따라 줄어들지만 상세 패널은 <b>즉시</b> 올라가야 한다 — 늦으면 그 프레임에 본문 마스크
        /// 밖으로 나가 패널이 잠깐 사라진다.</summary>
        private void ApplyTabDetailPlacement()
        {
            if (_sectionDetailRect == null) return;
            Vector2 p = _sectionDetailRect.anchoredPosition;
            float y = DetailYForTab(_tab == Tab.Inventory ? Tab.Equipment : _tab);
            if (Mathf.Approximately(p.y, y)) return;
            p.y = y;
            _sectionDetailRect.anchoredPosition = p;
        }

        /// <summary>창 중심이 화면 밖으로 나가지 않는 범위로 자른다 — 드래그와 화면 크기 변화가
        /// <b>같은 규칙</b>을 쓴다. 좌표계는 화면 중앙 원점이고, 창이 화면만큼 커지면 이동량은 0이 된다.</summary>
        private Vector2 ClampPanelPosition(Vector2 desired, float scaleFactor)
        {
            if (_panel == null || scaleFactor <= 0f) return desired;
            float sf = scaleFactor;
            Vector2 size = _panel.sizeDelta;
            float maxX = Mathf.Max(0f, (Screen.width / sf - size.x) * 0.5f - ScreenMargin);

            // ★ 2026-09-02 (41-1) — 세로는 <b>대칭이 아니다</b>. 옛 코드의 대칭 클램프는 이 창을 위로
            //   44.5pt 끌어올리게 허용했고, 그러면 창 위쪽이 OS y=16pt에 앉아 macOS 메뉴바(0~33)를
            //   17pt 덮는다(팝오버와 같은 결함, 같은 원인). 아래쪽 한계는 건드리지 않는다 —
            //   Dock을 덮는 것은 macOS의 모든 앱이 하는 표준 동작이고, 이 앱은 그 위를 발판으로도 쓴다.
            float topInset = ReservedTopBarProbe.TopInsetPoints(_agent != null ? _agent.PlatformService : null);
            float y = SurfaceSafeAreaPolicy.ClampCenterOriginOffsetY(
                desired.y, size.y, Screen.height / sf, topInset, ScreenMargin);
            return new Vector2(Mathf.Clamp(desired.x, -maxX, maxX), y);
        }

        /// <summary>
        /// 화면이 낮아 [착용]/[해제] 버튼이 <b>하나도 남김없이</b> 잘리면 한 번만 경고한다. 클릭은 이미
        /// <see cref="ContainsScreenPoint"/>가 막으므로 "안 보이는데 눌린다"는 없어졌지만, 그 화면에서는
        /// 아이템을 갈아입을 수단 자체가 사라진다는 사실은 조용히 넘길 일이 아니다(33-7-9 페이지 폴백 미구현).
        ///
        /// <para>★ 2026-09-01 — 감시 대상을 상세 패널 버튼에서 <b>카드 하단 버튼들</b>로 옮겼다. 상세 패널의
        /// 중복 버튼을 걷어내면서 착용 경로가 카드 버튼뿐이 됐기 때문이다. <b>하나라도</b> 보이면 아직
        /// 갈아입을 수 있으므로 경고하지 않는다.</para>
        ///
        /// <para>창 크기가 <b>바뀔 때만</b> 불린다(<see cref="ClampPanelToScreen"/>) — 카드 수만큼 도는
        /// 이 루프를 매 프레임 경로에 두면 상주 앱 규약을 어긴다.</para>
        /// </summary>
        private void SyncActionReachability()
        {
            bool anyActive = false;
            bool anyReachable = false;
            for (int i = 0; i < _cards.Length; i++)
            {
                ItemCard card = _cards[i];
                if (card == null || card.ActionRect == null) continue;
                if (!card.ActionRect.gameObject.activeInHierarchy) continue;
                anyActive = true;
                if (CardEquipButtonVisibleFraction(i) > 0f) { anyReachable = true; break; }
            }

            bool unreachable = anyActive && !anyReachable;
            if (unreachable == _actionUnreachable) return;
            _actionUnreachable = unreachable;
            if (!unreachable) return;

            Debug.LogWarning("[정보창] 화면 세로가 짧아 카드의 [착용] 버튼이 전부 가려졌습니다 — " +
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
            //                          └ 그림자 2겹(검정 α0.55 / α0.28)
            //   그런데 uGUI는 <b>부모 Graphic을 자식보다 먼저</b> 그린다. SetAsFirstSibling()은 형제
            //   순서만 정할 뿐이라, 두 그림자는 <b>패널 본체 위</b>에 얹혀 있었다. 그리고 투명 오버레이의
            //   프레임버퍼 알파는 UI/Default의 `Blend SrcAlpha OneMinusSrcAlpha`가 알파 채널에도 그대로
            //   적용되어 <b>겹을 쌓을수록 내려간다</b>(UiChrome 파일 머리 "알파 채널의 법칙" 참고):
            //       0(빈 화면) → 0.9216(본체 α0.96) → 0.7172(키 그림자) → <b>0.5948</b>(앰비언트)
            //   = 유저의 데스크톱이 <b>40.5%</b> 비쳐 들었다. 어두운 팔레트(34-1)에서는 가릴 밝기가
            //   없어 체감 밝기가 549% 튀었고, 그래서 밝은 팔레트 시절에는 같은 결함이 보이지 않았다.
            //
            //   이제 패널은 <b>그림 없는 컨테이너</b>이고 [본체(α1) → 보더]가 형제로 놓인다.
            //   _panel이 여전히 "움직이고 크기가 정해지는 사각형"이라는 계약은 그대로다 —
            //   드래그/클램프/히트테스트/차단막 코드는 한 줄도 바뀌지 않는다.
            //
            // ★ 2026-09-02 — 그림자 겹은 사라졌다(사용자 지시 "캐릭터창 둘레로도 그림자들이 있는데
            //   다 없애줘 깔끔하게"). 이 창 둘레를 만드는 것은 이제 보더 1px뿐이다.
            _panel = UiChrome.AddOpaquePanel(canvasGo.transform, "InfoPanel", UiChrome.RadiusPanel,
                out Image panelImage);
            // 33-7-7: 화면 중앙 모달. 배경 딤은 깔지 않는다(클래스 문서 참고).
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panelHeightPoints = PanelHeightForTab(_tab);
            _panel.sizeDelta = new Vector2(PanelWidth, _panelHeightPoints);
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

            // ★ 2026-09-02 — 상자 폭 200 → 180. [닫기]가 24 → 44로 넓어지면서 [설정]이 −68로 밀렸고,
            //   패널이 최소 폭(MinPanelWidth 320)까지 줄면 [설정]의 왼쪽 끝이 208pt가 된다 — 옛 200폭
            //   상자(16~216)와 8pt 겹친다. 글자는 MiddleLeft + Overflow라 상자 폭을 줄여도 <b>그림이
            //   한 픽셀도 바뀌지 않고</b>("내 책상 동료"는 x≈106에서 끝난다), 좁은 화면에서의 상자 겹침만
            //   사라진다(16~196 대 208 = 12pt 여유 = 옛 값과 같다).
            Text title = Label(barGo.transform, "Title", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, 16f, -13f, 180f, 14f, "내 책상 동료", bold: true);
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
            //
            // ★ 2026-09-02 — 면을 밝혔다. 근거·수치·왜 테두리가 아니라 면인지는 전부
            //   UiChrome "창을 닫는 법" 절 한 곳에 있다(세 표면 + 아래 [설정]이 같은 세 줄을 쓴다).
            Image closeSurface = UiChrome.AddSurface(barGo.transform, "CloseButton",
                UiChrome.ChromeButtonSurface, UiChrome.RadiusChip);
            _closeRect = closeSurface.rectTransform;
            // 오른쪽 끝에 건다(고정 x였다면 좁은 화면에서 패널이 줄 때 [✕]만 창 밖에 남는다).
            // 880 폭에서의 결과 좌표는 예전과 같다(오른쪽에서 16, 위에서 8).
            _closeRect.anchorMin = _closeRect.anchorMax = _closeRect.pivot = new Vector2(1f, 1f);
            // ★ 44×24 — WCAG 2.2 SC 2.5.8 Target Size(Minimum, AA) 24×24를 넘고, [설정]과 같은
            //   사각형이 된다. 낱말([닫기])로 바꾸는 안은 보류됐지만 칩은 미리 그 크기로 맞춰 둔다.
            _closeRect.sizeDelta = new Vector2(44f, 24f);
            _closeRect.anchoredPosition = new Vector2(-16f, -8f);
            Text closeLabel = UiChrome.AddText(_closeRect, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter,
                UiChrome.InkOnSurface(UiChrome.ChromeButtonSurface, UiChrome.InkRole.Title, enabled: true));
            UiChrome.Stretch(closeLabel.rectTransform);
            closeLabel.text = "✕";

            var closeButton = closeSurface.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeSurface;
            // ★ ColorTint를 끈다. pressed는 targetGraphic.color에 0.7843을 곱하는데, 밝은 면에서는
            //   그 곱셈이 대비를 <b>내린다</b>(면 5.26 → 3.47, 글리프 5.59 → 3.68 = MinTextContrast 미달).
            //   어두운 칩에서는 같은 곱셈이 대비를 올려서 이 함정이 보이지 않았다 — 면을 밝히는 순간
            //   부호가 뒤집힌다.
            closeButton.transition = Selectable.Transition.None;
            closeButton.onClick.AddListener(() => { if (TryClaimAction("close")) Close("[✕] 클릭"); });

            // ★ 2026-09-01 — 설정창(35-1)의 <b>주 진입점</b>. docs/UX_FLOW.md 36-11이 우클릭 메뉴 폐지에
            //   맞춰 "정보창 헤더의 작은 톱니"를 주 경로로 승격시켰다. 여기가 그 자리다.
            //   글자를 쓰는 이유: 이 프로젝트의 UI 폰트는 LegacyRuntime.ttf라 톱니 글리프(U+2699)가
            //   있다는 보장이 없고, 없으면 두부(□)가 뜬다. 아이콘을 선으로 그리는 방법도 있지만
            //   24pt 칩 안의 톱니는 결국 읽히지 않는다 — 32-1이 "심볼만 있는 원은 반드시 오독된다"고
            //   적어 둔 그 문제다.
            //   ★ 2026-09-02 — [설정]도 [닫기]와 <b>같은 면</b>을 쓴다. 리더가 실행 중인 빌드의 픽셀에서
            //     직접 재니 이 칩의 바탕도 창 바탕과 <b>1.01:1</b>이었다 — 닫기와 <b>같은 결함</b>이다.
            //     나란히 붙은 두 칩 중 하나만 고치면 그 자리가 새로 어긋난다.
            Image settingsSurface = UiChrome.AddSurface(barGo.transform, "SettingsButton",
                UiChrome.ChromeButtonSurface, UiChrome.RadiusChip);
            _settingsRect = settingsSurface.rectTransform;
            _settingsRect.anchorMin = _settingsRect.anchorMax = _settingsRect.pivot = new Vector2(1f, 1f);
            _settingsRect.sizeDelta = new Vector2(44f, 24f);
            // [닫기]가 24 → 44로 자라 왼쪽 끝이 −60이 됐다. 두 칩 사이 8pt를 유지하려면 −68이다.
            _settingsRect.anchoredPosition = new Vector2(-68f, -8f);
            // 글자 크기는 [✕]와 <b>같은 등급</b>(FontBody 12)이다 — 앱 전체 설정의 주 진입점이 닫기 버튼보다
            // 작게 그려져 있었다(페르소나 M2). 10pt(FontCaption)는 이 디자인 시스템에서 캡션/카운트 전용
            // 최소 등급이라, 그 자리에 있는 것만으로 "부수적인 것"이라고 말한다.
            Text settingsLabel = UiChrome.AddText(_settingsRect, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter,
                UiChrome.InkOnSurface(UiChrome.ChromeButtonSurface, UiChrome.InkRole.Title, enabled: true));
            UiChrome.Stretch(settingsLabel.rectTransform);
            settingsLabel.text = "설정";

            var settingsButton = settingsSurface.gameObject.AddComponent<Button>();
            settingsButton.targetGraphic = settingsSurface;
            settingsButton.transition = Selectable.Transition.None;   // [닫기]와 같은 이유(§ColorTint 함정).
            settingsButton.onClick.AddListener(() =>
            {
                if (TryClaimAction("settings")) OpenSettings("정보창 헤더 [설정]");
            });

            // ★ 2026-09-02 — 여기 있던 닫기 힌트("창 밖을 클릭해도 닫혀요")를 <b>같은 날 걷어냈다</b>.
            //   같은 라운드에서 바깥 클릭이 더 이상 닫지 않게 됐으므로 그 문장은 거짓이 됐고, 화면이
            //   거짓말을 하느니 아무 말도 안 하는 쪽이 낫다. 닫는 자리는 바로 오른쪽 [✕]다.
            //   ★ 2026-09-02 후속 — "칩이 버튼으로 안 읽힌다"(면 1.01:1)는 대체 <b>문구</b>가 아니라
            //     <b>면</b>으로 고쳤다. 지금 두 칩 모두 UiChrome.ChromeButtonSurface(5.26:1)다.
            //     근거는 UiChrome "창을 닫는 법" 절.
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

            // ---- 스탯 4행 ----
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

                Text label = UiChrome.AddText(rt, "Label", UiChrome.FontTitle, TextAnchor.UpperCenter,
                    UiChrome.InkTab(selected: false));
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
                Text code = Label(section, "Code", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.InkMeta,
                    90f, -3f, 46f, 12f, "—");

                Image divider = UiChrome.AddSurface(section, "Divider", UiChrome.Divider, 2);
                UiChrome.PlaceTopLeft(divider.rectTransform, 142f, -9f, 402f, 1f);
                divider.raycastTarget = false;

                Text count = Label(section, "Count", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.InkMeta,
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
            // 폭은 섹션과 <b>같다</b>(CarouselViewportWidth = RightContentWidth). 마지막 카드는 그 끝선에
            // 걸려 반쯤 잘리고, 그 걸침이 이 창의 유일한 "더 있다" 단서다 — 그 상수 문서 참고.
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
                    ThumbX, CardNameY, CardNameWidth, CardTextHeight, "—"),
                Meta = Label(rt, "Meta", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.InkMeta,
                    CardMetaX, CardNameY, CardMetaWidth, CardTextHeight, "—"),
            };

            // ---- 카드 하단 [착용]/[해제] ---- 이 창의 <b>유일한</b> 착용 손잡이다(상세 패널의
            //   중복 버튼은 2026-09-01 사용자 신고로 걷어냈다). 1차 행동이지만 P0-4의 조용한 칩을
            //   그대로 유지한다 — 이유는 StyleActionButton 문서 참고(한 화면에 12개가 반복된다).
            card.ActionSurface = UiChrome.AddSurface(rt, "Action",
                UiChrome.CardActionSurface, UiChrome.RadiusChip);
            card.ActionRect = card.ActionSurface.rectTransform;
            UiChrome.PlaceTopLeft(card.ActionRect, ThumbX, CardActionY, CardActionWidth, CardActionHeight);
            card.ActionOutline = UiChrome.AddOutline(card.ActionRect, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardActionSurface), UiChrome.RadiusChip);
            card.ActionLabel = UiChrome.AddText(card.ActionRect, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter,
                UiChrome.InkOnSurface(UiChrome.CardActionSurface, UiChrome.InkRole.Title, enabled: true),
                bold: true);
            UiChrome.Stretch(card.ActionLabel.rectTransform);
            card.ActionLabel.text = "착용";

            var actionButton = card.ActionSurface.gameObject.AddComponent<Button>();
            actionButton.targetGraphic = card.ActionSurface;
            // ★ Unity 기본 ColorTint는 pressed에 ×0.7843137을 곱한다. 새 면은 <b>밝아서</b> 그 곱이
            //   어두운 잉크(#0B1016)와의 대비를 무너뜨린다 — 실측:
            //     착용 #838589 5.19:1 → pressed #67696C <b>3.45:1</b>
            //     해제 #5087CC 5.18:1 → pressed #3F6AA0 <b>3.44:1</b>
            //   즉 <b>누르고 있는 동안 글자가 AA 미달</b>이 된다. 상태 색은 StyleActionButton이 값으로
            //   정하므로 uGUI의 자동 틴트는 꺼 둔다([✕]와 같은 처방).
            actionButton.transition = Selectable.Transition.None;
            card.ActionButton = actionButton;
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
                    case ItemIconPartKind.Polygon:
                    {
                        // 몸 경로(AccessoryCardIcon)와 <b>같은 순서</b>로 그린다: 면을 먼저 깔고 그 위에
                        // 윤곽선. 순서를 바꾸면 채움이 획을 반쯤 덮어 도형이 가늘어 보인다.
                        int count = Mathf.Min(part.PointCount, _iconPoints.Length);
                        for (int i = 0; i < count; i++)
                        {
                            _iconPoints[i] = FromViewBox(v[i * 2], v[i * 2 + 1], 40f, 40f, IconSize, IconSize);
                        }

                        // 규약상 마지막 점이 첫 점과 같다. 삼각분할에 중복점을 넣으면 퇴화 삼각형이 생긴다.
                        int fillCount = count;
                        if (fillCount > 1 && _iconPoints[fillCount - 1] == _iconPoints[0]) fillCount--;

                        AccessoryCardIcon.AddFill(root, "Fill", _iconPoints, fillCount, part.Color);
                        UiChrome.AddPolyline(root, "Seg", _iconPoints, count, IconStroke,
                            AccessoryShapeBuilder.FillOutlineColor(part.Color));
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

                    // ★ 2026-09-02 — 종류가 늘면(Polygon이 2026-09-02에 실제로 늘었다) 그 조각만
                    //   조용히 빠진다. 아이콘 한 조각이 빠진 그림은 "원래 그런 아이콘"으로 읽혀
                    //   아무도 신고하지 않는다 — 그래서 코드가 대신 신고한다.
                    default:
                        ShapeCoverageGuard.ReportUnknownIconKind(part.Kind);
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

            Image bodyImage = UiChrome.AddSurface(badge, "LockBody", UiChrome.NonTextMuted, 2);
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
                IconStroke * (renderW / viewW), UiChrome.NonTextMuted);
        }

        private void BuildDetailPanel(RectTransform page)
        {
            Image detail = UiChrome.AddSurface(page, "Detail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            _sectionDetailRect = drt;
            UiChrome.PlaceTopLeft(drt, RightPadX, DetailYForTab(_tab), RightContentWidth, DetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            _detailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                15f, -14f, 150f, 17f, "—", bold: true);
            // ★ 2026-09-01 오후 — 폭 330(= 172..502)은 <b>오른쪽 끝의 [착용] 버튼(525..577)을 피하려고</b>
            //   정한 값이었다. 그 버튼을 걷어낸 뒤 502..577의 75pt가 아무도 쓰지 않는 칸으로 남았다.
            //   이제 설명문과 <b>같은 오른쪽 끝</b>에서 끝나게 파생시킨다 — "Lv.9에 열림"처럼 긴 잠김
            //   문구가 그만큼 덜 밀린다. 숫자 330은 사라졌다.
            const float DetailPadX = 15f;
            const float DetailMetaX = 172f;
            _detailMeta = Label(drt, "DetailMeta", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                DetailMetaX, -14f, RightContentWidth - DetailPadX - DetailMetaX, 17f, "—");   // 405

            _detailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_detailBody.rectTransform, 15f, -42f, RightContentWidth - 30f, 48f);
            _detailBody.lineSpacing = 1.6f;   // 스펙 line-height 1.6.

            // ★ 여기에 [착용]/[해제] 버튼을 다시 만들지 마라(2026-09-01 사용자 신고로 걷어냈다).
            //   착용 손잡이는 카드 하단 하나뿐이고, 이 패널은 "고른 것이 무엇이고 왜 잠겼는가"만 말한다.
            //   되살아나면 InfoWindowSurfaceRegressionTests의 DetailPanelHasNoEquipButton이 잡는다.
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
                Image dot = UiChrome.AddSurface(rt, "Dot", UiChrome.NonTextMuted, UiChrome.RadiusDot);
                UiChrome.PlaceTopLeft(dot.rectTransform, UiChrome.Space2, -(InventoryRowHeight - 6f) * 0.5f, 6f, 6f);
                dot.raycastTarget = false;

                float nameX = UiChrome.Space2 + 6f + UiChrome.Space2;
                Text title = Label(rt, "Title", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    nameX, 0f, 110f, InventoryRowHeight, string.Empty);
                Text subtitle = Label(rt, "Subtitle", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.InkMeta,
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

            _pageUpRect = BuildPagerButton(page, "PageUp", "▲", railX, SectionsTopY, -1, "pageUp",
                out _pageUpOutline, out _pageUpLabel);
            _pageDownRect = BuildPagerButton(page, "PageDown", "▼", railX,
                SectionsTopY - (listHeight - InventoryRailWidth), +1, "pageDown",
                out _pageDownOutline, out _pageDownLabel);

            _pageIndicator = Label(page, "PageIndicator", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.InkMeta, railX, SectionsTopY - (InventoryRailWidth + UiChrome.Space2),
                InventoryRailWidth, InventoryPageIndicatorHeight, "1 / 1");

            Image detail = UiChrome.AddSurface(page, "InventoryDetail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            UiChrome.PlaceTopLeft(drt, RightPadX, DetailYForTab(Tab.Inventory), RightContentWidth, DetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            _inventoryDetailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, 15f, -14f, RightContentWidth - 30f, 17f, "—", bold: true);

            _inventoryDetailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_inventoryDetailBody.rectTransform, 15f, -42f, RightContentWidth - 30f, 34f);
            _inventoryDetailBody.lineSpacing = 1.6f;

            // 지금 파는 것은 하나도 없다 — 그 사실을 화면에서도 숨기지 않는다.
            Label(drt, "Note", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.InkMeta,
                RightContentWidth - 215f, -DetailHeight + 26f, 200f, 14f, "지금은 파는 것이 없습니다");
        }

        /// <summary>페이지 칩 하나. ★ 2026-09-02 — 테두리와 글리프를 <b>밖으로 내보낸다</b>.
        /// 예전에는 둘을 지역 변수로 버려서 "끝에 닿았다"를 칠할 대상 자체가 없었고, 그래서 1페이지의
        /// [▲]가 [▼]와 <b>픽셀 단위로 동일</b>한 채 눌러도 조용히 아무 일도 안 했다(45-9-b).</summary>
        private RectTransform BuildPagerButton(RectTransform page, string name, string glyph, float x, float y,
            int direction, string dedupKey, out Image outlineOut, out Text labelOut)
        {
            Image surface = UiChrome.AddSurface(page, name, UiChrome.CardSurface, UiChrome.RadiusChip);
            var rt = surface.rectTransform;
            UiChrome.PlaceTopLeft(rt, x, y, InventoryRailWidth, InventoryRailWidth);
            Image outline = UiChrome.AddOutline(rt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);

            Text label = UiChrome.AddText(rt, "Label", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.InkIcon(true));
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;

            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() =>
            {
                if (!CanScrollInventory(direction)) return;   // 죽은 칩은 <b>아무 일도 하지 않는다</b>.
                if (TryClaimAction(dedupKey)) ScrollInventory(direction);
            });
            outlineOut = outline;
            labelOut = label;
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
                TextAnchor.MiddleLeft, UiChrome.InkMeta);
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
