using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 캐릭터 창(장비 / 외형 / 보관함 / 상점) — 2026-09-05 <b>인계본 3컬럼 이식 라운드</b>.
    /// 정본 좌표표는 <c>docs/UX_EQUIPMENT_WINDOW_3COL_PORT.md</c>이고, 이 파일은 그 표를 그대로 옮긴
    /// 것이다. 좌표/색/글자 크기를 여기서 새로 고르지 않는다 — 고치고 싶으면 그 문서를 먼저 고친다.
    /// 비교 기준 그림은 <c>docs/handoff/design_handoff_equipment_window/render/</c>의
    /// <c>spec_1042x802.png</c>다.
    ///
    /// ============================================================================
    /// 골격 (1042 × 802, 화면 중앙 모달) — L-1 / L-2 / L-8
    /// ============================================================================
    /// 헤더 66 + 본문 736. <b>옛 타이틀바 40은 헤더가 흡수했다</b>(L-2) — 제목이 두 벌이 되지 않고
    /// 세로 여유가 34 -> 74로 2.2배가 된다. 헤더는 왼쪽에 이름·<c>Lv.N</c>·EXP 진행선(L-5), 가운데
    /// 탭 4개, 오른쪽에 보유 칩·동전 칩·[설정]·[✕]를 놓고 <b>그 자식 사각형을 뺀 나머지가 드래그 표면</b>이다.
    ///
    /// 본문은 <b>3컬럼</b>이다(인계본 구조 그대로, 폭만 카드 열 하나를 뺀 값):
    ///  · 컬럼 1 (306) — 프리뷰 무대 265×238 / 착용 슬롯 4행 / 선택 상세 카드(주 버튼 없음, L-9)
    ///  · 컬럼 2 (292) — 기록(스트레스 게이지 + 4행) / 표시(이름·잉크) / 테마 세트 자리
    ///  · 컬럼 3 (444) — 카테고리 블록들의 <b>세로 스크롤</b>. 블록마다 카드 <b>2열 격자</b>(186×208)
    ///
    /// <b>가로 캐러셀은 폐기됐다</b>(L-1). 카드가 세로로 자유로워져 44 -> 58pt 아이콘이 들어간다.
    /// <b>탭별 가변 높이도 폐기됐다</b>(L-8) — 인계본 규칙 "창 크기와 헤더는 탭에 따라 변하지 않는다".
    ///
    /// ============================================================================
    /// 왜 우상단 앵커가 아니라 화면 중앙인가 / 왜 배경 딤을 깔지 않는가 (33-7-7)
    /// ============================================================================
    /// 802pt 높이는 톱니 아래(top 84)에서 시작하면 886pt라 어떤 노트북에도 들어가지 않는다.
    /// 그래서 중앙 정렬로 바꿨다. <b>2026-08-30 보강</b>: "중앙 <b>고정</b>"이던 부분만 리더가 뒤집었다 —
    /// 열릴 때는 여전히 화면 중앙이지만 <b>헤더를 잡으면 옮길 수 있다</b>(화면 밖으로는 못 나간다).
    /// ★★ <b>2026-09-07 — "옮긴 자리는 기억하지 않는다"가 뒤집혔다</b>(사용자 요청 PART1-1:
    /// <i>"이동한 위치는 창별로 저장되어 재시작 후에도 유지"</i>). 이제 <b>한 번이라도 옮겼으면</b>
    /// 그 자리에서 열리고(<see cref="RestorePanelPosition"/>), 옮긴 적이 없으면 예전 그대로 화면
    /// 중앙이다. 즉 33-7-7의 "열면 중앙"은 <b>기본값</b>으로 남고 규칙에서 내려왔다.
    /// 기구는 세 창이 공유한다(<see cref="UiWindowDrag"/>). 반대로 스펙의 배경색 <c>#dcdbd7</c>(<see cref="UiChrome.ScreenScrim"/>)은
    /// <b>깔지 않는다</b> — 그건 브라우저 프로토타입의 "지면"이지 모달 딤이 아니고, 우리가 화면 전체를
    /// 덮으면 유저의 작업 화면을 통째로 가려 <b>비침해 원칙 2 정면 위반</b>이 된다.
    ///
    /// ============================================================================
    /// 왜 헤더에 "ESC"라고 적지 않고 [✕]를 두는가
    /// ============================================================================
    /// 스펙은 우측 상단에 <c>ESC</c> 힌트를 두고 그 키로 닫는다. 그런데 이 프로젝트에서
    /// <see cref="KeyCode.Escape"/>는 이미 <b>클릭관통 긴급 해제</b>(Core/StickmanAgent)에 묶여 있다.
    /// 창 닫기를 같은 키에 겹치면 창을 닫을 때마다 <b>보이지 않는 부수효과</b>로 클릭관통이 꺼져
    /// 화면 전체의 클릭을 우리가 먹기 시작한다(원칙 2 직결). 그래서 그 자리에 [✕] 버튼을 둔다 —
    /// 있지도 않은 동작을 힌트로 <b>주장하지 않는</b> 쪽이 이 프로젝트의 문구 원칙이기도 하다.
    ///
    /// ============================================================================
    /// 카드는 <b>세로 스크롤 격자</b>다 (2026-09-05, 인계본 이식)
    /// ============================================================================
    /// 컬럼 3 전체가 <see cref="ScrollRect"/> 하나이고, 그 안에 카테고리 블록(제목줄 + 2열 격자)이
    /// 차례로 쌓인다. 카드 좌표는 <see cref="LayoutCardGrid"/>가 <b>한 곳에서</b> 계산한다 —
    /// 카테고리마다 아이템 수가 다르므로 레이아웃 그룹에 맡기면 블록 높이가 갈라진다.
    /// <b>다만</b> 이 창의 실제 클릭 경로는 uGUI가 아니라 전역 폴링이므로(아래 문단) 드래그도 폴링판이
    /// 한 벌 더 있다. 두 경로가 <b>절대값 공식</b>을 쓰므로 동시에 돌아도 더해지지 않는다.
    ///
    /// 그리고 착용/해제는 <b>카드 하단 버튼</b>이 한다(사용자 요청). 카드 본체 클릭은 여전히
    /// "고르기"뿐이다 — 격자를 밀다가 옷이 갈아입혀지는 사고를 구조적으로 없앤다.
    /// <b>상세 카드에는 주 버튼을 넣지 마라</b>(L-9 = 2026-09-01 사용자 신고).
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
    /// 한다. 액자는 <b>인계본 무대 값</b>(265×238 / 여백 8 / 반지름 <see cref="UiChrome.RadiusPanel"/>)이고,
    /// 그 결과 <see cref="PortraitContentSize"/>에서 파생되는 카메라 종횡비가 <b>1.044 → 1.122</b>로
    /// 함께 바뀐다(L-3 교차 레이어 영향 — 리더 승인 완료).
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
    public sealed partial class CharacterInfoWindow : MonoBehaviour, IExclusiveSurface
    {
        [SerializeField] private StickConfig _config;

        // ★ 2026-08-30: 31000 -> 31900. 이 창은 모달인데 부채꼴(31500)/팝오버(31700)보다 <b>아래</b>에
        // 깔려 있었다(디버거 실측) — 값 자체가 "모달"이라는 성격과 모순이었다. 말풍선(31000)과도 값이
        // 같아 Unity가 그리기 순서를 보장하지 않았다(동률 오버레이 캔버스는 생성 순서에 의존).
        private const int SortingOrderTopMost = 31900; // 팝오버(31700) 위, 앱 제어 메뉴(32760) 아래.

        // ==================== 인계본 3컬럼 확정 치수 (캔버스 유닛 == OS 포인트) ====================
        //
        // 정본: docs/UX_EQUIPMENT_WINDOW_3COL_PORT.md §4. 아래 값은 그 표의 <b>사본이 아니라 출처</b>다 —
        // 테스트는 이 상수를 참조하고, 숫자를 다시 적지 않는다(CLAUDE.md 하드코딩 금지).

        /// <summary>창 가로. ★ 2026-09-02 <b>880 → 1042</b>(사용자 확정), 2026-09-05 <b>유지</b>.
        /// <para>1042는 되돌리지 않는다. 그리고 그 숫자는 원래 <b>3컬럼 계산에서 나온 값</b>이다 —
        /// 인계본 1240 = 306 + 292 + 642에서 카드 열 하나(186 + 12)를 뺀 것이 정확히 1042다.
        /// 그래서 컬럼 1·2를 설계값 그대로 두면 카드가 <b>설계값 186.00pt</b>로 재현된다(§2-2).</para>
        /// <para>인계본의 1242는 Windows 1920×1080@150%(논리 1280)에서 <b>108.1%</b> = 화면보다 크다.
        /// 이 창은 바깥 클릭으로 닫히지 않으므로(사용자 확정) 화면을 다 덮으면 갇힌다.</para></summary>
        private const float PanelWidth = 1042f;

        /// <summary>창 세로 — <b>탭과 무관하게 고정</b>(L-8). 인계본 규칙이 명시적이다:
        /// "창 크기와 헤더는 탭에 따라 변하지 않는다".
        /// <para>옛 861은 <b>사라진 레이아웃</b>(섹션 4칸 × 156)의 예산이었다. 3컬럼에서는 세로 섹션이라는
        /// 단위 자체가 없어지므로 그대로 두면 59pt를 <b>예약만</b> 하게 된다 — P0-1이 고친 결함의 재범이다.
        /// 구조적 최소는 728pt이고 802에서 여유가 <b>74</b>다(§3-2/§3-6).</para></summary>
        internal const float PanelHeight = 802f;

        /// <summary>헤더 — <b>옛 타이틀바 40을 흡수했다</b>(L-2). 드래그 표면이 "타이틀바"에서
        /// "헤더 − 알려진 자식 사각형들"로 바뀐다(<see cref="TryBeginPanelDrag"/>).</summary>
        private const float HeaderHeight = 66f;

        internal const float BodyHeight = PanelHeight - HeaderHeight;   // 736

        /// <summary>화면 가장자리 여백(16pt). ★ 2026-09-07 — <b>숫자를 여기서 정하지 않는다</b>.
        /// 창 셋(정보창·설정창·집중 팝오버)이 같은 클램프를 쓰게 되면서 같은 16을 세 번 적을 뻔했고,
        /// 그러면 다음 라운드에 반드시 한 벌만 고쳐진다. 단일 출처는
        /// <see cref="UiWindowDrag.ScreenMarginPoints"/>이고 값은 예전과 <b>비트 동일</b>하다.</summary>
        private const float ScreenMargin = UiWindowDrag.ScreenMarginPoints;

        // ---- 헤더 내부 (§4-2) ----
        private const float HeaderPadLeft = 28f;
        private const float HeaderPadRight = 24f;
        private const float HeaderLevelWidth = 44f;
        private const float HeaderXpTrackHeight = 2f;
        private const float HeaderChipHeight = 32f;
        private const int HeaderChipRadius = 16;
        private const float HeaderChipY = 17f;
        private const float HeaderChipPadX = 12f;
        private const float HeaderCoinChipWidth = 112f;
        private const float HeaderOwnedChipWidth = 100f;
        private const float HeaderSettingsChipWidth = 48f;

        // ★ 오른쪽 칩의 <b>순서가 곧 우선순위</b>다. 창이 좁아지면 왼쪽부터 접히므로
        //   (<see cref="SyncHeaderChips"/>), 왼쪽일수록 덜 중요한 것을 둔다:
        //     [✕](절대 안 접는다) < [설정](설정창의 유일한 GUI 경로) < 동전 < 보유(순수 정보).
        //   인계본 그림은 「보유 · 동전 · ✕」 순이고, [설정]은 우리 것이라 인계본에 자리가 없다 —
        //   [✕] 옆에 붙이는 것은 옛 타이틀바에서 두 크롬 칩이 나란히 있던 배치 그대로다.
        private const float HeaderCloseChipInset = HeaderPadRight;
        private const float HeaderSettingsChipInset = HeaderCloseChipInset + HeaderChipHeight + UiChrome.Space3;
        private const float HeaderCoinChipInset = HeaderSettingsChipInset + HeaderSettingsChipWidth + UiChrome.Space3;
        private const float HeaderOwnedChipInset = HeaderCoinChipInset + HeaderCoinChipWidth + UiChrome.Space3;
        private const float HeaderChipBlockWidth = HeaderOwnedChipInset + HeaderOwnedChipWidth;
        private const float HeaderTabStripHeight = 34f;
        private const float HeaderTabHeight = 28f;
        private const float HeaderTabPadX = 14f;
        private const float HeaderTabGap = 2f;
        private const float HeaderTabStripPad = 3f;

        /// <summary>구분선/테두리 한 겹. UGUI는 테두리를 <b>안쪽</b>에 그리므로 치수를 늘리지 않는다.</summary>
        private const float DividerThickness = 1f;

        // ---- 3컬럼 골격 (§4-3) ----
        private const float Col1Width = 306f;
        private const float Col2X = Col1Width;                     // 306
        internal const float Col2Width = 292f;
        private const float Col3X = Col2X + Col2Width;             // 598
        private const float Col3Width = PanelWidth - Col3X;        // 444
        internal const float ColPadY = 20f;

        /// <summary>컬럼 하나가 세로로 쓸 수 있는 높이(위아래 패딩 뺀 값).</summary>
        private const float ColContentHeight = BodyHeight - ColPadY * 2f;   // 696

        // ---- 컬럼 1 — 프리뷰 / 착용 슬롯 / 상세 카드 ----
        private const float Col1PadX = 20f;

        /// <summary>컬럼 1 콘텐츠 폭 265 = 306 − 20(좌) − 20(우) − 1(구분선). 인계본 무대 폭과 같다.</summary>
        private const float Col1ContentWidth = Col1Width - Col1PadX * 2f - DividerThickness;   // 265

        private const float StageY = -ColPadY;                     // -20
        private const float StageHeight = 238f;
        private const float StagePadding = 8f;

        // ★ 2026-09-06 — 여기 있던 StageFloorGlowHeight(74f)를 지웠다. 유일한 소비자가
        //   BuildColumn1의 "StageFloorGlow" 겹이었고 그 겹이 사라졌다(아래 D1/D2 문단).
        //   상수만 남기면 다음 사람이 "무엇을 재던 값인가"를 다시 조사하게 된다(이 파일의
        //   CardsPerSection·SyncManualHide 삭제 때와 같은 관례).

        /// <summary>착용 슬롯 4행의 첫 줄 위 끝 = 무대 바닥 − <see cref="UiChrome.Space3"/>.</summary>
        private const float SlotRowsTopY = StageY - StageHeight - UiChrome.Space3;   // -270
        private const float SlotRowHeight = 46f;                   // ≥ MinTargetSizePoints 24 × 1.9
        private const float SlotRowStep = 52f;
        private const float SlotIconSize = 24f;

        /// <summary>상세 카드 높이(§4-3-1 검산: 실측 143 ≤ 상한 231).</summary>
        private const float DetailCardHeight = 143f;

        /// <summary>상세 카드는 컬럼 1 <b>바닥에 정렬</b>한다 — 남는 세로는 슬롯 행과 상세 카드
        /// 사이가 먹는다(§4-3-1의 Spacer).</summary>
        private const float DetailCardY = -(BodyHeight - ColPadY) + DetailCardHeight;   // -573

        // ---- 컬럼 2 — 능력치 / 기록 / 표시 / 테마 세트 ----
        // ★ 「능력치」 블록의 치수와 컬럼 2 스크롤은 CharacterInfoWindow.Stats.cs에 있다.
        private const float Col2PadX = 18f;
        internal const float Col2ContentWidth = Col2Width - Col2PadX * 2f;   // 256
        internal const float SectionLabelHeight = 18f;
        internal const float TrackHeight = 4f;
        internal const float RecordRowHeight = 26f;
        private const float SwatchSize = 12f;
        private const float SwatchGap = 8f;

        /// <summary>게이지 라벨 줄 높이(STRESS의 「이름 · 값」 줄). ★ 예전에는 <c>13f</c>가
        /// <b>세 곳</b>에 흩어져 있었다(라벨 · 값 · 트랙 y). 컬럼 2가 블록 단위로 높이를 계산하게 된
        /// 지금은 네 번째가 생기므로 하나로 모은다.</summary>
        internal const float GaugeLabelHeight = 13f;

        /// <summary>게이지 라벨 줄과 트랙 사이 간격.</summary>
        internal const float GaugeLabelGap = 4f;

        /// <summary>게이지 한 벌(라벨 줄 + 간격 + 트랙)이 세로로 먹는 높이 = 21.</summary>
        internal const float GaugeBlockHeight = GaugeLabelHeight + GaugeLabelGap + TrackHeight;

        /// <summary>「표시」 블록의 이름 줄 높이. ★ 예전에는 <c>25f</c>가 <b>네 곳</b>에 흩어져 있었고
        /// (히트 상자 · 라벨 · 입력 상자 · 다음 y 계산), 블록 높이를 계산해야 하는 지금은 그 다섯 번째가
        /// 생긴다. 폭 1042 사고(헤더는 따라갔는데 카드줄은 안 따라감)와 같은 형태를 미리 막는다.</summary>
        internal const float NameRowHeight = 25f;

        /// <summary>
        /// ★ 이름 줄 오른쪽 끝의 <b>연필 표식</b> 한 변(2026-09-06 사용자 신고 대응).
        ///
        /// <para><b>왜 이것이 필요했나</b>: 이름은 처음부터 고칠 수 있었는데, 그 사실을 알리는 것이
        /// <c>Color.clear</c> 히트 상자 하나뿐이었다. 사용자에게는 <b>그냥 글자</b>로 보였고
        /// (신고: <i>"캐릭터 이름도 설정할수 있어야 하는데 안됨"</i>), 눌러 볼 이유가 화면에 0개였다.
        /// 투명 히트 상자는 손가락 넓히기 관례이지 <b>어포던스가 아니다</b>.</para>
        ///
        /// <para><b>왜 hover가 아닌가</b>: <see cref="UiChrome.ControlFaceLiftHover"/> 문단이 이미
        /// 실측으로 닫은 문제다 — <b>처음 창을 열었을 때</b>가 정확히 hover를 못 받는 상황이라
        /// "기본 상태만으로 발견 가능해야 한다". 그래서 쉬는 상태에 그린다.</para>
        ///
        /// <para><b>왜 테두리가 아니라 글리프인가</b>: 같은 문단 (3)이 "테두리만 있는 것은 입력칸으로
        /// 읽힌다"고 말하지만, 이 자리의 바탕은 <see cref="UiChrome.PanelSurface"/>이고 기존 테두리
        /// 토큰(<see cref="UiChrome.PanelBorder"/> α0.16)을 얹으면 대비가 <b>1.65 : 1</b>로
        /// <see cref="UiChrome.MinNonTextContrast"/>(3.0)에 한참 못 미친다 — 보이지 않는 테두리를
        /// 그리는 것은 지금 상태와 같다. 3.0을 넘기려면 흰색 α≈0.33 이상의 <b>새 색</b>이 필요하고
        /// 그건 design-art 판정 사항이다. 반면 글리프는 이미 있는 <see cref="UiChrome.NonTextMuted"/>로
        /// <b>3.87 : 1</b>이 나온다(#6c7480 위 #141721) — 새 색 없이 하한을 넘는 유일한 길이다.
        /// 자물쇠 배지(<c>BuildLockGlyph</c>)가 같은 토큰으로 같은 일을 한다.</para>
        ///
        /// <para>14는 <see cref="UiChrome.MinTargetSizePoints"/>(24)보다 작지만 <b>클릭 대상이 아니다</b> —
        /// 누르는 것은 이름 줄 전체(<c>NameHit</c>, 212 × 25)이고 이 표식은 그 안에 얹힌 그림이다.</para>
        /// </summary>
        private const float NameEditGlyphSize = 14f;

        // ---- 컬럼 3 — 카테고리 블록의 세로 스크롤 + 카드 2열 격자 (§4-3-3) ----
        private const float Col3PadX = 26f;
        private const float Col3PadTop = 22f;
        private const float Col3PadBottom = 30f;

        /// <summary>세로 스크롤바가 쓰는 폭. 인계본이 콘텐츠 폭 계산에서 빼는 값이고, 이 8pt를 빼야
        /// 카드가 설계값 186.00으로 떨어진다.</summary>
        private const float Col3ScrollbarInset = 8f;

        private const float Col3ContentWidth =
            Col3Width - Col3PadX * 2f - Col3ScrollbarInset;   // 384

        private const int CardColumns = 2;
        private const float CardGap = 12f;

        /// <summary>카드 격자가 <b>설계대로 2열</b>로 성립하는 최소 컬럼 폭 = <see cref="Col3Width"/> 444.
        /// <para>좁은 창에서 무엇을 먼저 접을지 정하는 기준이다 — 컬럼 1·2보다 <b>카드의 2열 리듬</b>을
        /// 먼저 지킨다. 이 창의 목적이 "다른 것으로 갈아입는다"이고 착용 경로가 카드 버튼 하나뿐이라,
        /// 마지막까지 남아야 하는 것은 카드다.</para></summary>
        private const float MinGridColumnWidth = Col3PadX * 2f + Col3ScrollbarInset + CardWidth * 2f + CardGap;

        /// <summary>1열로 내려앉는 하한. 이 아래로는 접을 것이 더 없다.</summary>
        private const float MinSingleColumnGridWidth = Col3PadX * 2f + Col3ScrollbarInset + CardWidth;   // 246

        /// <summary>카드 폭 <b>186.00</b> — 인계본 3열(폭 1242)에서 나온 값과 소수점까지 같다(§2-2).</summary>
        private const float CardWidth = (Col3ContentWidth - CardGap * (CardColumns - 1)) / CardColumns;

        /// <summary>카드 높이. 세로 검산(§4-3-3): 13 + 78 + 9 + 16 + 9 + 15 + 3 + 13 + 9 + 29 + 13 = 207
        /// → 짝수 규칙으로 208.</summary>
        private const float CardHeight = 208f;

        private const float CardStepX = CardWidth + CardGap;    // 198
        private const float CardStepY = CardHeight + CardGap;   // 220

        /// <summary>카테고리 블록 제목줄: ● 이름 코드 ──── n/6.</summary>
        private const float CategoryHeaderHeight = 18f;
        private const float CategoryGridTopY = -(CategoryHeaderHeight + UiChrome.Space3);   // -30
        private const float CategoryBlockGap = UiChrome.Space6;                             // 24
        private const float CategoryCountWidth = 44f;
        private const float CategoryCountX = Col3ContentWidth - CategoryCountWidth;
        private const float CategoryDividerX = 168f;
        private const float CategoryDividerGap = 6f;
        private const float CategoryDividerWidth =
            CategoryCountX - CategoryDividerX - CategoryDividerGap;

        // ---- 카드 내부 (§4-3-3) ----
        private const float CardPadX = 13f;
        private const float CardContentWidth = CardWidth - CardPadX * 2f;   // 160
        private const float ThumbX = CardPadX;
        private const float ThumbY = -13f;
        private const float ThumbWidth = CardContentWidth;
        private const float ThumbHeight = 78f;

        /// <summary>카드 아이콘 정사각 크기. <b>44 → 58</b>(인계본 값 그대로, 면적 1.74배) —
        /// 인계본 Overview가 문제 ①로 꼽은 "아이템 아트 퀄리티"에 대한 가장 큰 단일 개선이다.
        /// 3컬럼에서 카테고리가 세로 섹션이 아니라 스크롤 안의 블록이 되면서 카드가 세로로
        /// 자유로워진 덕분에 가능해졌다.</summary>
        private const float IconSize = 58f;

        /// <summary>아이콘 획 두께. 핸드오프 스펙은 <b>40 viewBox 기준 1.7</b>이므로 <see cref="IconSize"/>가
        /// 커지면 <b>같은 비율로</b> 따라와야 형태가 원본과 같다(두께만 그대로 두면 선이 가늘어진다).</summary>
        private const float IconStroke = 1.7f * (IconSize / 40f);
        private const float LockBadgeWidth = 18f;
        private const float LockBadgeHeight = 17f;

        /// <summary>등급 리본 좌우 인셋 — <b>썸네일과 같은 자리</b>(<see cref="CardPadX"/> 13 → 폭 160).
        /// <para>★ 인계본 §4-3-3은 인셋 <b>14</b>(폭 158)라고 적는다. 1pt 차이는 화면에서 구별되지
        /// 않는 반면, 인셋을 두 곳에서 정하면 카드 폭이 바뀌는 날 한쪽만 따라간다 — 이 저장소가
        /// 헤더/카드줄 끝선에서 실제로 당한 형태다. 그래서 <b>썸네일에서 파생</b>시키고, 그 불변식을
        /// <c>RarityRibbonSurfaceTests</c>가 잠근다. 카드 모서리 반지름
        /// <see cref="UiChrome.RadiusCard"/> 12보다 커서 둥근 모서리 밖으로 삐져나오지도 않는다.</para></summary>
        private const float CardRibbonInset = CardPadX;
        private const float CardRibbonWidth = CardContentWidth;   // 160

        // 이름 줄: 왼쪽 이름 / 오른쪽 등급 낱말.
        private const float CardNameY = -100f;
        private const float CardTextHeight = 16f;
        private const float CardRarityWidth = 44f;
        private const float CardRarityX = CardPadX + CardContentWidth - CardRarityWidth;
        private const float CardNameGap = UiChrome.Space2;
        private const float CardNameWidth = CardRarityX - CardPadX - CardNameGap;

        // 상태 줄(보유/착용 중/LV.n) + 메타 줄(카테고리).
        private const float CardMetaY = -125f;
        private const float CardMetaHeight = 15f;
        private const float CardMetaWidth = CardContentWidth;
        private const float CardMetaX = CardPadX;
        private const float CardCategoryY = -143f;
        private const float CardCategoryHeight = 13f;

        /// <summary>카드 하단 [착용]/[해제] 버튼 — 이 창에서 옷을 갈아입히는 <b>유일한</b> 손잡이다.
        /// <para>★ 2026-09-01: 상세 패널에도 같은 버튼이 있었는데(사용자 신고 "각 장비별 착용버튼으로
        /// 했는데 왜 옛날처럼 하단에 착용상자가 따로 있음?") 그쪽을 걷어냈다. <b>되살리지 마라</b>(L-9).
        /// 상태→라벨/색 매핑은 여전히 <see cref="StyleActionButton"/> 한 곳뿐이다.</para></summary>
        private const float CardActionY = -165f;

        /// <summary>카드 [착용] 버튼 높이 — 인계본 값 <b>29</b>.
        /// <para>숫자를 그대로 적지 않는다. <see cref="UiChrome.MinTargetSizePoints"/>(WCAG 2.2 2.5.8
        /// Target Size (Minimum)) <b>위로 얼마</b>인지를 적는다 — 하한이 움직이면 이 값이 따라와야지,
        /// 리터럴을 베껴 두면 하한과 조용히 갈라진다(옛 값 22f가 정확히 그렇게 하한 아래에 있었다).</para>
        /// <para>여유 5는 인계본 29에서 우리 하한 24를 뺀 값이고, 그 결과가 하한을 실제로 넘는지는
        /// 정적 생성자가 <b>한 번 더</b> 비교한다.</para></summary>
        private const float CardActionHeadroom = 5f;
        private const float CardActionHeight = UiChrome.MinTargetSizePoints + CardActionHeadroom;   // 29
        private const float CardActionWidth = CardContentWidth;

        /// <summary>한 탭에 들어갈 수 있는 카테고리 수의 <b>상한</b>. 실제로 보여줄 수는 탭마다 다르고
        /// <see cref="SectionCountForTab"/>가 카탈로그에서 센다([장비] 4 / [외형] 2,
        /// 2026-09-06 [머리] 삭제 기준 — 그 전에는 [외형] 3이었다).</summary>
        private const int SectionCount = 4;

        // ★ 2026-09-01 — <b>CardsPerSection(=4) / CardCount(=16) 상수를 지웠다.</b>
        //   카테고리당 아이템 수는 이제 콘텐츠(에셋)가 정하는 <b>가변값</b>이고, 코드가 4라고 적어 두면
        //   에셋을 늘리는 순간 다섯 번째부터가 <b>예외도 경고도 없이 화면에서 사라진다</b>. 카드 수는
        //   <see cref="CardsInSection"/>가 카탈로그에서 세고, 배치는 <see cref="LayoutCardGrid"/>가 한다.

        private const int IconSetCount = 2;   // [장비]용 / [외형]용 — 카드 하나가 두 벌을 미리 갖는다.

        /// <summary>격자를 "끌었다"고 인정하는 최소 이동(캔버스 포인트). 이보다 작으면 손떨림이라
        /// 보고 클릭으로 처리한다 — 카드를 <b>누르려다</b> 1px 밀렸다고 착용이 취소되면 그게 더 나쁘다.</summary>
        private const float GridDragThresholdPoints = 4f;

        /// <summary>격자를 실제로 민 뒤 이만큼은 uGUI 클릭을 먹지 않는다. 스크롤을 멈춘 손가락 아래에
        /// 있던 카드가 <b>뗄 때</b> 눌리는 것을 막는다(웹 목업의 <c>moved</c> 플래그와 같은 목적).</summary>
        private const float GridClickSuppressSeconds = 0.20f;

        // ---- 보관함 / 준비 중 페이지 — 본문 전체 폭을 쓴다(3컬럼은 카드 탭 전용이다) ----
        private const float PagePadX = 26f;
        private const float PageContentWidth = PanelWidth - PagePadX * 2f;   // 990
        private const float PageTopY = -ColPadY;                             // -20
        private const float InventoryDetailHeight = 103f;

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
        private const float InventoryListWidth = PageContentWidth - InventoryRailWidth - UiChrome.Space2;

        /// <summary>보관함 목록이 세로로 쓰는 높이(마지막 줄 아래 틈은 빼고 센다).</summary>
        private const float InventoryListHeight =
            InventoryVisibleRows * (InventoryRowHeight + InventoryRowGap) - InventoryRowGap;   // 537

        /// <summary>보관함 상세 카드의 위 끝. 목록 아래 <see cref="UiChrome.Space3"/>.</summary>
        private const float InventoryDetailY = PageTopY - InventoryListHeight - UiChrome.Space3;   // -569

        // ★★ 2026-09-03 — 여기 있던 <c>CaptionKoreanAdvance = 11f</c>("10pt 한글 한 글자가 먹는 폭")를
        //    <b>지웠다</b>. 이름이 이미 자백하고 있다 — <b>한글</b> 한 글자다. 이 앱의 설명 문구에는
        //    한글·라틴·숫자가 섞여 있고 라틴 자폭은 절반 이하다. 그런데 아래 상한은 그 11f로
        //    <b>나눗셈</b>을 했다: 곱셈형(상자가 부푼다)과 반대로 <b>글자 수 상한이 과소</b>로 나와
        //    라틴 문구는 들어갈 자리가 남았는데도 잘렸다.
        //
        //    지금은 <see cref="InventoryDescriptionWidth"/>(pt)를 그대로 예산으로 쓰고
        //    <c>UiChrome.Ellipsize</c>가 실제 폭으로 자른다. 글자 수라는 중간 단위가 사라졌다.

        /// <summary>설명 칸의 x와 폭 — <see cref="BuildInventoryPage"/>와 글자 수 예산이 <b>같은 값</b>을
        /// 봐야 한다. 예전에는 build가 지역변수로 계산하고 글자 수는 24를 따로 적어 뒀는데, 창이
        /// 넓어지면 칸만 커지고 글이 옛 폭에서 잘려 오른쪽이 비어 보인다.</summary>
        private const float InventoryDescriptionX = UiChrome.Space2 + 6f + UiChrome.Space2 + 112f + 50f;   // 184
        private const float InventoryDescriptionWidth =
            InventoryListWidth - InventoryDescriptionX - StatusSlotWidth - UiChrome.Space2;

        /// <summary>설명 칸이 실제로 차지하는 폭 — <b>상자를 놓는 쪽과 글을 자르는 쪽이 이 하나를 본다.</b>
        /// 하한 40pt는 창이 극단적으로 좁아졌을 때 상자가 음수 폭이 되는 것을 막는다.
        /// ★ 2026-09-03 — 예전에는 상자만 이 하한을 통과하고 자르기는 <b>글자 수</b>로 따로 정해져,
        /// 둘이 서로 다른 단위였다(창을 넓히면 상자만 커졌다).</summary>
        private static float InventoryDescriptionBoxWidth => Mathf.Max(40f, InventoryDescriptionWidth);

        private const float SlowRefreshInterval = 0.25f;
        private const float ClickPollInterval = 0.05f;
        private const float ActionDedupSeconds = 0.35f;

        /// <summary>액자 <b>안쪽</b>(RawImage가 실제로 차지하는) 크기(pt) — 액자 테두리 여백을 뺀 값.
        /// 촬영장 카메라의 기본 종횡비가 이 값에서 파생되므로(<see cref="CharacterPortraitStage.DesignAspect"/>)
        /// 액자 크기를 바꾸면 그림 구도도 함께 따라온다. 숫자를 두 곳에 적지 않기 위한 단일 출처다.
        /// <para>33-7-6에서 (176−24)/(238−24)=0.710 → (204−16)/(196−16)=1.044로 바뀌었고,
        /// 2026-09-05 인계본 무대 이식(L-3)으로 (265−16)/(238−16)=<b>1.122</b>가 됐다.</para></summary>
        public static Vector2 PortraitContentSize => new Vector2(
            Col1ContentWidth - StagePadding * 2f,
            StageHeight - StagePadding * 2f);

        /// <summary>컬럼 2 「기록」 섹션의 행 수. 6 -> 5(2026-09-01 "넘어진 횟수 삭제") -> <b>4</b>
        /// (2026-09-02 격파 놀이 기능 삭제).
        /// <b>두 번 다 지운 것은 표시뿐이다</b> — <see cref="CharacterStatsModel.RagdollFalls"/>도
        /// <see cref="CharacterStatsModel.BattleWins"/>도 값은 그대로 살아 저장 파일을 왕복한다.
        /// 데이터를 함께 지우면 훗날 다른 화면에서 다시 쓸 때 계수 로직을 <b>처음부터 다시</b>
        /// 만들어야 하고, BattleWins 쪽은 그에 더해 저장 스키마 버전을 올려야 한다.
        ///
        /// <para>행을 빼도 <b>창 높이는 바뀌지 않는다</b> — 창 높이는 <see cref="PanelHeight"/> 고정이다(L-8).</para></summary>
        internal const int StatCount = 4;

        /// <summary>테마 세트 패널 문구 — <b>진행도를 말할 수 없을 때만</b> 쓴다
        /// (<see cref="CharacterStatReadout.TryGetThemeProgress"/>가 false = 착용 4부위에 테마가
        /// 하나도 없다). 진행도가 생기면 그 자리에 두 줄(<c>오피스 워커 3/4</c> · 완성/미완성)이
        /// 대신 뜬다 — <see cref="RefreshSetPanel"/>.
        /// <para>★ 2026-09-06 정정 — 옛 문구("테마 세트는 다음 업데이트에 들어옵니다")는 R21에서
        /// 42종 전량에 테마가 배정되며 거짓이 됐다(<see cref="ItemCatalog.ThemeOfItem"/>). 기능은
        /// 이미 출시돼 있고, 이 문구가 뜨는 유일한 이유는 「스탯 4슬롯에 테마 있는 장비를 하나도
        /// 안 걸쳤다」이다 — 그렇게 고쳤다.</para></summary>
        private const string SetPanelNotice = "테마가 있는 장비를 아직 하나도 걸치지 않았습니다.";

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

        /// <summary>컬럼 1 바닥의 선택 상세 카드. <b>주 버튼은 없다</b>(L-9).</summary>
        private RectTransform _sectionDetailRect;

        private RectTransform _closeRect;
        private RectTransform _settingsRect;   // 헤더의 작은 [설정] 칩 — 설정창의 주 진입점(36-11).
        private RectTransform _ownedChipRect;
        private RectTransform _coinChipRect;
        private Text _ownedChipValue;
        private Text _coinChipValue;

        /// <summary>드래그 손잡이 — <b>헤더 전체</b>(L-2로 타이틀바를 흡수했다). 실제 손잡이는
        /// 여기서 탭·칩·[✕]·[설정] 사각형을 뺀 나머지다(<see cref="TryBeginPanelDrag"/>).</summary>
        private RectTransform _titleBarRect;

        // ★ 2026-09-06 — 여기 있던 <c>_tabUnderlines</c>를 지웠다. 밑줄은 <b>준비 중 탭 전용</b>
        //   표식이었고, [상점] 본문이 생겨 준비 중 탭이 0개가 되면서 영원히 투명한 겹이 됐다
        //   (CharacterInfoWindow.Tabs.cs의 같은 문단).
        private readonly Text[] _tabLabels = new Text[TabCount];
        private readonly Image[] _tabSurfaces = new Image[TabCount];
        private readonly RectTransform[] _tabRects = new RectTransform[TabCount];

        // ---- 헤더 ----
        private Text _nameTitle;
        private Text _rankTitle;
        private RectTransform _xpFill;

        // ---- 컬럼 1 ----
        private Image _portraitFrame;
        private Image _portraitBorder;
        private RawImage _portraitImage;
        private Text _portraitFallback;
        private Text _previewLabel;
        private CharacterPortraitStage _stage;

        /// <summary>착용 슬롯 4행. 인덱스는 <b>카드 탭의 카테고리 순서</b>와 같다.</summary>
        private sealed class SlotRowView
        {
            public RectTransform Rect;
            public Image Surface;
            public Image Outline;
            public RectTransform IconRoot;
            public Text Label;
            public Text Name;
            public Text Value;
            public bool HasIcon;
        }
        private readonly SlotRowView[] _slotRows = new SlotRowView[SectionCount];

        // ---- 컬럼 2 ----
        /// <summary>컬럼 2 「표시」 블록의 편집 가능한 이름. 헤더의 <see cref="_nameTitle"/>은 읽기 전용
        /// 사본이고, <b>고칠 수 있는 자리는 이것 하나뿐</b>이다.</summary>
        private Text _nameLabel;

        private RectTransform _nameRect;
        private InputField _nameInput;
        private RectTransform _nameInputRect;

        /// <summary>이름이 고칠 수 있는 것임을 <b>쉬는 상태에서</b> 알리는 연필 표식
        /// (<see cref="NameEditGlyphSize"/> 문단이 근거 전문). 편집 중에는 라벨과 함께 내린다.</summary>
        private RectTransform _nameEditGlyph;
        private RectTransform _stressFill;
        private Text _stressValue;
        private readonly Text[] _statValues = new Text[StatCount];
        private readonly Image[] _inkRings = new Image[2];
        private readonly RectTransform[] _inkRects = new RectTransform[2];

        // ---- 우측: 카테고리 섹션 + 카드 ----
        private sealed class SectionView
        {
            /// <summary>카테고리 블록 한 덩어리(제목줄 + 카드 2열 격자). 탭마다 카테고리 수가 다르므로
            /// <b>남는 블록은 통째로 끈다</b> — 2026-08-30 표정(FACE) 삭제로 [외형] 탭이 3칸이 됐다.</summary>
            public GameObject Root;

            /// <summary>블록의 사각형. <see cref="LayoutCardGrid"/>가 y와 높이를 정한다.</summary>
            public RectTransform Rect;

            public Image Dot;
            public Text Title;
            public Text Code;
            public Text Count;

            /// <summary>제목줄의 가로 구분선. 폭이 컬럼에서 파생되므로 붙잡아 둔다.</summary>
            public Image Divider;

            /// <summary>지금 이 블록이 보여주고 있는 카테고리.</summary>
            public EquipmentSlot BoundSlot;

            public bool HasBoundSlot;

            /// <summary>이 블록이 쓰는 카드가 <see cref="_cards"/>의 어디부터 몇 장인가.</summary>
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

            /// <summary>등급 낱말(이름 줄 오른쪽). 2026-09-03의 「낱말이 없으면 등급 표시는 미완이다」를
            /// 카드에서 지키는 자리 — 3컬럼 골격이 확정되며 칸이 생겼다(UI_SURFACE_SPEC §15.14-d).</summary>
            public Text Rarity;

            /// <summary>상태 줄 — <c>보유</c> / <c>착용 중</c> / <c>LV.n</c>.</summary>
            public Text Meta;

            /// <summary>메타 줄 — 아이템 카테고리 라벨.</summary>
            public Text Category;

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

            /// <summary>★ 2026-09-07 — 아이콘 세트별로 <b>실제 그림을 구운 카탈로그 인덱스</b>
            /// (없으면 −1). <see cref="Item"/>은 <c>RefreshCards</c>가 매 갱신마다 다시 심는
            /// <b>표시용</b> 값이라 텍스트 경로를 증명하지만, 그림은 <c>BuildCard</c>가 <b>한 번</b>
            /// 구운 뒤 다시 굽지 않으므로 같은 인덱스를 쓴다는 보장이 코드만 봐서는 안 보인다.
            /// 이 배열이 그 사실을 기록해 회귀 테스트가 "그림 인덱스 == 텍스트 인덱스"를 직접
            /// 대조할 수 있게 한다(실사고: 은퇴 아이템 뒤 카드들의 그림이 한 칸 앞선 아이템을
            /// 그렸다 — CardIconIdentityTests). <c>BuildCard</c>의 세트 루프가 매번 채우므로
            /// (아래 <see cref="IconRoot"/>/<see cref="IconGraphics"/>와 같은 시점) 초기값은
            /// 굽지 않는다 — 다 채워지기 전에 읽는 소비자가 없다.</summary>
            public readonly int[] IconItem = new int[IconSetCount];

            /// <summary>해금 상태에서 되돌릴 <b>조각별 원래 색</b>(ItemCatalog가 정한 소재색).
            /// 잠긴 카드는 무채색 실루엣으로 덮어쓰므로, 덮어쓰기 전 색을 어딘가에 갖고 있어야 한다.
            /// 매 프레임 카탈로그를 다시 뒤지지 않으려고 카드가 굽는 시점에 한 번만 캐시한다.</summary>
            public readonly Color[][] IconBaseColors = new Color[IconSetCount][];
        }

        private readonly SectionView[] _sections = new SectionView[SectionCount];

        /// <summary>카드 실물. 개수는 <b>카탈로그가 정한다</b>(빌드 때 한 번만 센다) — 상수로 적으면
        /// 아이템 에셋을 늘렸을 때 다섯 번째부터가 조용히 사라진다.</summary>
        private ItemCard[] _cards = System.Array.Empty<ItemCard>();

        /// <summary>컬럼 1·2 — 카드 탭에서만 보인다([보관함]/[상점]은 본문 전체 폭을 쓴다).</summary>
        private GameObject _col1Root;
        private GameObject _col2Root;

        /// <summary>본문 사각형(헤더 아래 전부). <b>앵커 스트레치</b>라 패널이 줄면 함께 준다 —
        /// 그 사실이 결함 W-1의 출발점이었다(<see cref="_bodyHeight"/> 문단).
        /// 이 참조는 <b>진단/테스트 전용</b>이다.</summary>
        private RectTransform _bodyRect;

        private GameObject _sectionPage;
        private GameObject _inventoryPage;

        /// <summary>
        /// ★ 좁은 창에서의 <b>강등 사다리</b>(문서 §3-6의 가로축). 3컬럼은 설계 폭 1042의 것이고,
        /// <see cref="ClampPanelToScreen"/>이 창을 줄여도 <b>내용은 함께 접히지 않는다</b> —
        /// 그대로 두면 컬럼 3(x 598~)이 통째로 마스크 밖으로 나가 <b>아이템을 하나도 볼 수 없다</b>.
        ///
        /// <para>그래서 폭이 모자라면 <b>컬럼 2 → 컬럼 1 순서로 접는다</b>. 마지막까지 남는 것은
        /// 카드다 — 이 창의 목적이 "다른 것으로 갈아입는다"이고, 착용 경로가 카드 버튼 하나뿐이기
        /// 때문이다(컬럼 1·2에는 액션이 없다).</para>
        ///
        /// <para><b>카드 폭은 줄이지 않는다.</b> 카드 내부(썸네일·이름·버튼)가 설계 폭 186에 절대
        /// 좌표로 놓여 있어 폭을 바꾸면 내부가 따라오지 않는다. 대신 <b>열 수</b>를 2 → 1로 내린다.</para>
        /// </summary>
        private float _gridX = Col3X;

        private float _gridWidth = Col3Width;
        private float _gridContentWidth = Col3ContentWidth;
        private int _gridColumns = CardColumns;
        private bool _showCol1 = true;
        private bool _showCol2 = true;

        /// <summary>탭 스트립의 오른쪽 끝(헤더 좌표). 헤더 오른쪽 칩들이 여기와 겹치는지 판정한다.</summary>
        private float _tabStripRightEdge;

        /// <summary>컬럼 3의 세로 스크롤. 드래그/휠/클램프는 uGUI가 하고, 이 파일은
        /// <b>content 좌표만</b> 다룬다(전역 폴링 드래그도 같은 좌표를 쓴다).</summary>
        private ScrollRect _gridScroll;
        private RectTransform _gridViewport;
        private RectTransform _gridContent;

        private Text _detailName;
        private Text _detailMeta;
        private Text _detailBody;
        private Image _detailThumb;
        private Image _detailThumbOutline;

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

            /// <summary><see cref="Description"/>에 넣으려던 <b>자르기 전</b> 문자열.
            /// 이것이 그대로면 <see cref="UiChrome.Ellipsize"/>를 다시 부르지 않는다 —
            /// 그 함수는 폭을 재려고 <c>Text.text</c>를 여러 번 바꾸므로 호출부가 원본을 캐시해
            /// <b>내용이 실제로 바뀐 순간에만</b> 불러야 한다(그 함수의 호출부 규약).
            /// <see cref="ItemCard.NameSource"/>와 같은 장치다.</summary>
            public string DescriptionSource;
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

        /// <summary>창을 손으로 옮기는 기구 — <b>세 창이 공유하는 한 벌</b>(<see cref="UiWindowDrag"/>).
        /// 임계·클램프·저장이 전부 그 안에 있고, 이 창이 아는 것은 «헤더의 어디가 손잡이인가»뿐이다.</summary>
        private readonly UiWindowDrag _windowDrag = new UiWindowDrag(UiWindowId.CharacterInfo);

        private Vector2 _dragStartOffsetPoints;

        /// <summary>지금 컬럼 3 격자를 잡고 있는가.</summary>
        private bool _gridGrabbed;

        private float _gridGrabScreenY;
        private float _gridStartContentY;
        private bool _gridMoved;
        private float _lastGridMoveTime = -999f;

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
            Debug.Log($"[정보창] 준비 완료({PanelWidth:F0}×{PanelHeight:F0} 고정, 3컬럼 {Col1Width:F0}/{Col2Width:F0}/{Col3Width:F0}, 화면 중앙, {TabCount}탭: {TabNamesForLog()}, " +
                $"카드 {_cards.Length}장 + 장비 {ItemCatalog.ListedEquipmentCount}종" +
                $"(카탈로그 {ItemCatalog.EquipmentCount}종, 은퇴분 제외)) — 여는 방법 2가지: " +
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
            // ★ 2026-09-07 — 옛 <c>ResetPanelToCenter()</c> 자리다. 옮긴 적이 없으면 이 함수가
            //   하는 일이 예전과 <b>완전히 같다</b>(중앙). 옮긴 적이 있으면 그 자리에서 연다.
            RestorePanelPosition();
            _leftInitialized = false; // 창을 여는 그 클릭이 곧바로 카드 클릭으로 오인되지 않게.
            // 여는 그 순간은 정의상 조작 중이다 — 첫 커서 폴링(최대 0.05초)까지의 공백을 메운다.
            _lastSurfaceTouchTime = Time.unscaledTime;
            _hoveredCard = -1;
            _pendingEquipCard = -1;
            _pendingShopCard = -1;
            _pendingDlcCard = -1;
            ClearShopConfirm();        // 지난 세션의 「정말 살까요?」를 새로 연 창이 물려받지 않는다.
            EndGridDrag();
            EndShopDrag();
            EndDlcDrag();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_clickBlocker != null) _clickBlocker.enabled = true;
            EndNameEdit(commit: false);
            EnsurePortraitTexture(force: true);
            if (_stage != null) _stage.SetRenderingEnabled(true);
            RefreshAll();
            Debug.Log($"[정보창] 열림({source}) — {CharacterProgressionModel.CharacterName} " +
                $"Lv.{CharacterProgressionModel.Level}({RankTitleFor(CharacterProgressionModel.Level)}), " +
                $"탭=[{Def(_tab).Name}], 근속 {CharacterStatsModel.DaysTogether}일차.");
        }

        public void Close(string source)
        {
            if (!_open) return;
            _open = false;
            _draggingPanel = false;
            // ★ 확정하지 않고 놓는다 — 닫히는 창의 마지막 좌표를 저장하면 "옮긴 적 없는데 자리가
            //   바뀌었다"가 된다(전체화면 자동 숨김도 이 경로로 들어온다).
            _windowDrag.Cancel();
            _pendingEquipCard = -1;
            _pendingShopCard = -1;
            _pendingDlcCard = -1;
            ClearShopConfirm();        // 창을 닫는 것도 「가만히 두기」다 — 확정되지 않은 구매는 사라진다.
            EndGridDrag();
            EndShopDrag();
            EndDlcDrag();
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
            // 전체화면 게임 위에 이 창이 그대로 떠 있고, 히트테스트가 <b>커서 아래 Collider2D</b>를
            // 보므로(hitTestType=Raycast — InfoGearIconWidget.cs "클릭 판정" 절. 픽셀 알파를 보는 구조가
            // <b>아니다</b>) 씬 루트의 차단막이 살아 있는 한 그 영역의 클릭까지 먹는다.
            // Close()가 캔버스/차단막/초상화 촬영장을 한 번에 정리한다.
            // 복귀 시 강제로 다시 열지 않는다 — 사용자가 톱니로 다시 연다.
            //
            // ★★★ 2026-09-02 — <c>ArePanelsSuppressed</c>(등급 1 포함)로 바뀌었다. 이 창이 바로
            //    출시 Blocker의 실측 당사자다: 정보창 877x853pt가 화면 1512x982pt의 <b>면적 50.38%
            //    (세로 86.86%)</b>를 덮은 채, 카테고리를 선언하지 않은 전체화면 앱 위에 그대로 떠
            //    그 사각형의 클릭까지 먹었다(페르소나 `재현` 실기 재현). 게임 조건을 없애는 처방은
            //    2026-08-31 신고의 회귀라 금지이므로, 판정을 넓히는 대신 <b>등급</b>으로 갈랐다.
            if (_agent != null && _agent.ArePanelsSuppressed)
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
            // ★★ 2026-09-07 — 여기 있던 TickPresenceLine() 호출을 지웠다. 되살리지 마라 —
            //    프리뷰 무대 하단의 "지금 · 걷는 중" 상태 텍스트를 사용자가 명시적으로 없애 달라고
            //    했다("프리뷰화면에 자꾸 지금상태를 알려주는데 필요없음 지금걷는중 같은것들" ->
            //    "현재 상태 추적하지말고 다빼줘"). 상세는 StateLabel(StickmanStateId) 문서 참고.

            _slowTimer += Time.unscaledDeltaTime;
            if (_slowTimer < SlowRefreshInterval) return;
            _slowTimer = 0f;
            RefreshNumbers();
            // [상점]만 보는 두 가지(확인 단계 만료 · 잔액 변화). 그 탭이 아니면 첫 줄에서 돌아간다.
            if (Def(_tab).Page == TabPage.Shop) TickShopTab();
        }

        // ============================================================================
        // ★★ 2026-09-07 — 프레즌스 줄(프리뷰 무대 하단의 "지금 · 걷는 중" 상태 텍스트)을 통째로
        //    제거했다. 되살리지 마라 — 사용자 신고: "프리뷰화면에 자꾸 지금상태를 알려주는데
        //    필요없음 지금걷는중 같은것들" 이어서 "현재 상태 추적하지말고 다빼줘"(전체 삭제 확정,
        //    위치도 "캐릭터 정보창안 프리뷰화면하단"으로 직접 확정).
        //    지웠던 것: _presenceText(Text 컴포넌트) · TickPresenceLine()/WritePresence() ·
        //    관련 필드(_lastShownState/_hasShownState/_presenceHoldUntil) ·
        //    Update()/RefreshAll() 호출 · ApplyPortraitTheme의 색 동기화 ·
        //    TestApi.PresenceTextForTests · PortraitPaperDollTests.PresenceLineHoldsLongEnoughToBeRead.
        //    StateLabel(StickmanStateId)은 이 줄의 유일한 호출자였고(그 어휘를 대조하던 테스트
        //    PresenceLineHoldsLongEnoughToBeRead도 이 줄과 함께 삭제됐다), 지금은 호출자가 정말
        //    0개다 — 그래도 남겨 둔 것은 27개 상태를 사람이 읽는 한 마디로 옮기는 유일한 정본 표이기
        //    때문이다(중복 우려는 Core/StickMateDisplayNames 문서 참고, 그쪽은 별개 용도인
        //    행동 명령창 불가 이유 문구다).
        // ============================================================================

        private void OnProgressionChanged()
        {
            if (!_open) return;
            RefreshNumbers();
            RefreshCards();     // 레벨이 오르면 잠긴 카드가 열린다.
            RefreshDetail();
            RefreshInventoryList();
            RefreshShop();      // 레벨로 열린 상품은 [상점]에서 「보유 중」이 된다(같은 사실, 같은 프레임).
        }

        private void OnEquipmentChanged()
        {
            if (!_open) return;
            RefreshCards();
            RefreshDetail();
            RefreshInventoryList();
            // ★ [DLC] 카드의 착용 칩도 같은 사실을 말한다 — 빼면 다른 탭에서 갈아입고 온 사람에게
            //   「착용」이라고 적힌 칩이 이미 입고 있는 아이템 위에 남는다.
            RefreshDlc();
        }

        private void RefreshAll()
        {
            // ★ 가시성이 <b>먼저</b>다. RefreshCards가 캐러셀 폭을 즉시 다시 재는데
            //   (LayoutRebuilder.ForceRebuildLayoutImmediate), 꺼져 있는 페이지에서는 그 계산이 돌지 않아
            //   스크롤 한계가 옛 값으로 남는다.
            ApplyTabVisibility();
            ApplyPortraitTheme();   // 무대 바탕과 그 위 잉크는 잉크 프리셋에서 파생된다(L-6).
            RefreshNumbers();
            RefreshCards();
            RefreshDetail();
            RefreshInventoryList();
            RefreshShop();
            RefreshDlc();
            RefreshInkSwatches();
        }

        /// <summary>0.25초 주기로 다시 만드는 값만 여기 있다 — 카드/상세는 <b>사건이 있을 때만</b>
        /// 갱신한다(카드 수십 장의 문자열을 초당 4번 다시 만들 이유가 없다).</summary>
        private void RefreshNumbers()
        {
            string characterName = CharacterProgressionModel.CharacterName;
            if (_nameTitle != null) _nameTitle.text = characterName;
            if (_nameLabel != null && !_editingName) _nameLabel.text = characterName;
            if (_rankTitle != null) _rankTitle.text = $"Lv.{CharacterProgressionModel.Level}";

            // ★ 2026-09-06 [머리] 은퇴 — 화면에 적는 「보유 n / m」은 <b>보여주는 모집단</b>이다.
            //   전량(ItemCatalog.EquipmentCount)을 분모로 적으면 고를 수 없는 것이 분모에 남고,
            //   분자만 바꾸면 분자·분모가 서로 다른 것을 세게 된다(그 어긋남은 화면만 봐서는 못 찾는다).
            //   둘 다 Listed* 로 간다 — 같은 술어, 같은 모집단. 숫자는 여기 적지 않는다.
            int ownedItems = ItemCatalog.ListedUnlockedEquipmentCount(_config);
            if (_ownedChipValue != null) _ownedChipValue.text = $"{ownedItems} / {ItemCatalog.ListedEquipmentCount}";
            if (_coinChipValue != null) _coinChipValue.text = CurrencyModel.CoinBalance.ToString("N0");

            float stress = StressGauge.CurrentLevel;
            SetBarFill(_stressFill, stress);
            StressMoodTier tier = StressGaugeRenderer.TierForLevel(stress, _config);
            if (_stressValue != null) _stressValue.text = $"{stress * 100f:F0}%  ·  {StressGaugeRenderer.TierLabel(tier)}";

            float need = CharacterProgressionModel.XpToNextLevel(_config);
            float have = CharacterProgressionModel.CurrentXp;
            SetBarFill(_xpFill, need > 0f ? Mathf.Clamp01(have / need) : 0f);

            // 기록 4행. 0인 항목은 숫자 대신 회색 "아직 없음"으로 — 0이 성취처럼 보이지 않게 한다.
            SetStat(0, $"{CharacterStatsModel.DaysTogether}일차", true);
            SetStat(1, CharacterStatsModel.FormatCompanionTime(), true);
            SetStat(2, $"{ownedItems} / {ItemCatalog.ListedEquipmentCount}종", ownedItems > 0);
            SetStat(3, CharacterStatsModel.TryGetArcheryAccuracy01(out float acc)
                ? $"{CharacterStatsModel.ArcheryBullseyes} / {CharacterStatsModel.ArcheryShots} ({acc * 100f:F0}%)"
                : "기록 없음", CharacterStatsModel.ArcheryShots > 0);
            // ※ 표시에서 빠진 칸: 넘어진 횟수(2026-09-01) / 격파 성공(2026-09-02).
            //    CharacterStatsModel.RagdollFalls·BattleWins 둘 다 값은 계속 살아 있다.

            // 컬럼 2 「능력치 / STATUS」와 「테마 세트 / SET」 — 입력이 그대로면 문자열을 만들지
            // 않는다(CharacterInfoWindow.Stats.cs의 갱신 절 참고).
            RefreshStatColumn();
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
        /// <summary>
        /// ★★ 2026-09-06 (docs/UI_ALPHA_BLEED_POLICY.md §2-(4)/§4-3) — <b>이 링의 바탕은 견본 채움이고,
        /// 그 채움은 유저가 런타임에 고르는 색이다</b>(검정 또는 흰색). 고정 토큰을 쓰면 <b>둘 중
        /// 하나에서는 반드시 틀린다</b>. 실측한 옛 값:
        /// <code>
        ///                       검정 견본   흰 견본
        ///   비선택 PanelBorder    1.44 ✘     1.00 ✘   ← 흰 견본에서는 링이 아예 없다
        ///   선택   TextPrimary   19.06 ✔     1.10 ✘   ← 흰 잉크를 고르면 「지금 고른 것」이 사라진다
        /// </code>
        /// 이제 <b>방향까지 값이 정한다</b>: 비선택은 테두리의 문(<see cref="UiChrome.EdgeOnSurface"/>)이
        /// 흰쪽/검은쪽을 골라 <b>자기 목표 대비</b>를 만들고(숫자는 그 함수가 들고 있다),
        /// 선택은 밝은 견본에서 목탄으로 뒤집는다(1.10 → 14.77).
        /// <b>새 색 0개</b> — <see cref="UiChrome.InkContrastCharcoal"/>은 이미 "밝은 면 위의 대비 잉크"로
        /// 존재하는 토큰이다.
        /// </summary>
        private void RefreshInkSwatches()
        {
            bool white = _config != null && _config.IsWhiteInk();
            for (int i = 0; i < _inkRings.Length; i++)
            {
                bool active = (i == 1) == white;
                if (_inkRings[i] == null) continue;

                Color fill = InkSwatchFill(i);
                _inkRings[i].color = active ? SelectedRingOn(fill) : UiChrome.EdgeOnSurface(fill);
            }
        }

        /// <summary>「지금 고른 것」 링. 이름이 아니라 <b>값</b>으로 방향을 고른다 — 흰 잉크 견본처럼
        /// 밝은 채움 위에서는 흰 링이 1.10:1로 사라지므로 목탄으로 뒤집는다.
        /// <para>★ 기준을 <see cref="UiChrome.EdgeContrastTarget"/>으로 잡는다(하한 3.0이 아니라).
        /// 같은 자리의 <b>비선택</b> 링이 <see cref="UiChrome.EdgeOnSurface"/>로 그 목표를 맞추므로,
        /// 하한으로 재면 「고른 것」이 「안 고른 것」보다 <b>흐린</b> 구간이 생긴다.</para></summary>
        private static Color SelectedRingOn(Color fill)
            => UiChrome.ContrastRatio(UiChrome.TextPrimary, fill) >= UiChrome.EdgeContrastTarget
                ? UiChrome.TextPrimary
                : UiChrome.InkContrastCharcoal;

        /// <summary>링이 <b>실제로</b> 올라앉은 색 — 채움 <see cref="Image"/>에서 읽는다.
        /// 채움색 식을 여기서 다시 쓰면 두 곳이 갈라지고, 갈라지는 순간 링은 <b>없는 바탕</b>을
        /// 기준으로 계산된다.</summary>
        private Color InkSwatchFill(int index)
        {
            RectTransform rt = index >= 0 && index < _inkRects.Length ? _inkRects[index] : null;
            Image fill = rt != null ? rt.GetComponent<Image>() : null;
            if (fill != null) return fill.color;
            return index == 1 ? Color.white : Color.black;   // 아직 안 구워졌을 때의 안전값
        }

        private static void SetBarFill(RectTransform fill, float progress01)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
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
            // 액자 바탕은 촬영장의 배경색과 <b>같은 값</b>이어야 한다 — 다르면 8pt 테두리 여백에서
            // 색이 갈라진 이음매가 보인다. 그래서 33-1의 PortraitSurface를 직접 쓰지 않고 촬영장의
            // 판단을 그대로 따른다(색 결정이 두 곳으로 흩어지지 않게).
            Color backdrop = CharacterPortraitStage.ResolveBackdropColor(_config);
            if (_portraitFrame != null) _portraitFrame.color = backdrop;
            if (_portraitBorder != null)
            {
                // 옛 삼항식(흰 α0.18 / CardBorder)은 목탄 1.79 · 종이 1.02였다 — 흰 α는 밝은 무대에서
                // 원리상 실패한다. 이제 방향까지 규칙이 고른다(UI_ALPHA_BLEED_POLICY §4-3).
                _portraitBorder.color = UiChrome.EdgeOnSurface(backdrop);
            }

            // ★ L-6 — <b>임시값이다. design-art 후속 판정 대기.</b> 어두운 무대(흰 잉크)에서는 기존
            //   토큰이 그대로 맞지만, 밝은 무대(검은 잉크 = 출하 기본, 종이 바탕)에서는 TextTertiary가
            //   읽히지 않는다. 그래서 밝은 무대에서만 무대 바탕 위 잉크를 InkOnSurface로 뒤집는다 —
            //   그 함수는 면에서 잉크를 파생시키므로 호출부가 색을 고르지 않는다.
            if (_previewLabel != null)
            {
                _previewLabel.color = UiChrome.InkOnSurface(backdrop, UiChrome.InkRole.Meta, enabled: true);
            }
            if (_portraitFallback != null)
            {
                _portraitFallback.color = UiChrome.InkOnSurface(backdrop, UiChrome.InkRole.Meta, enabled: true);
            }
        }

        // ==================== 이름 인라인 편집 (33-7-8, 리더 승인) ====================

        private void BeginNameEdit()
        {
            if (_editingName || _nameInput == null || _nameLabel == null) return;
            _editingName = true;
            _nameLabel.gameObject.SetActive(false);
            // 표식은 라벨과 <b>같은 손잡이</b>로 움직인다 — 편집 중에는 입력칸이 그 자리를 쓴다.
            if (_nameEditGlyph != null) _nameEditGlyph.gameObject.SetActive(false);
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
            if (_nameEditGlyph != null) _nameEditGlyph.gameObject.SetActive(true);
            if (_nameLabel != null)
            {
                _nameLabel.gameObject.SetActive(true);
                _nameLabel.text = CharacterProgressionModel.CharacterName;
            }
            if (_nameTitle != null) _nameTitle.text = CharacterProgressionModel.CharacterName;
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
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            // 창 바탕을 눌러도 뒤(데스크톱)로 새지 않아야 한다 — 예전 InfoPanel Image가 하던 역할.
            panelImage.raycastTarget = true;

            BuildHeader(_panel);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(RectMask2D));
            bodyGo.transform.SetParent(_panel, false);
            var body = bodyGo.GetComponent<RectTransform>();
            // ★ 참조를 들고 있는 이유는 하나다 — 테스트가 «컬럼 뷰포트가 정말 Body를 따라갔는가»를
            //   <b>Body 자신을 재서</b> 확인할 수 있게(결함 W-1의 회귀 잠금). 프로덕션 경로는 이 값을
            //   읽지 않는다(그쪽은 _bodyHeight = 패널 − 헤더라는 <b>다른 계산</b>이고, 둘이 갈라지면
            //   그 자리에서 빨개져야 한다).
            _bodyRect = body;
            body.anchorMin = new Vector2(0f, 0f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.offsetMin = Vector2.zero;
            body.offsetMax = new Vector2(0f, -HeaderHeight);
            // 작은 화면에서 패널이 짧아져도 내용이 패널 밖으로 새어 나가지 않게 한다(ClampPanelToScreen).

            BuildColumn1(body);
            BuildColumn2(body);
            BuildSectionPage(body);
            BuildInventoryPage(body);
            BuildShopPage(body);
            BuildDlcPage(body);
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

        /// <summary>
        /// 헤더 66pt — <b>옛 타이틀바 40을 흡수했다</b>(L-2, §4-2).
        ///
        /// <para>왼쪽부터 이름 · <c>Lv.N</c> · EXP 진행선(L-5) · 탭 4개, 오른쪽 끝에서 안쪽으로
        /// [✕] · 동전 칩 · 보유 칩 · [설정]. <b>남는 자리가 드래그 표면</b>이다 —
        /// <see cref="TryBeginPanelDrag"/>가 "헤더 안 + 알려진 자식 밖"으로 기계적으로 판정한다.</para>
        ///
        /// <para>★ 이름은 <b>읽기 전용</b>이다. 편집은 컬럼 2의 「표시」 블록 한 곳뿐 — 같은 값을 두
        /// 곳에서 편집하면 한쪽이 조용히 낡는다(이 저장소가 반복해서 당한 형태).</para>
        /// </summary>
        private void BuildHeader(Transform parent)
        {
            var barGo = new GameObject("Header", typeof(RectTransform));
            barGo.transform.SetParent(parent, false);
            var rt = barGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -HeaderHeight);
            rt.offsetMax = Vector2.zero;
            _titleBarRect = rt;   // 드래그 손잡이 — 여기를 잡은 동안만 창이 움직인다.

            Image divider = UiChrome.AddSurface(parent, "HeaderRule",
                UiChrome.Flatten(UiChrome.Divider, UiChrome.PanelSurface), 2);
            // 폭을 못 박으면 좁은 화면에서 패널이 줄었을 때(ClampPanelToScreen) 구분선만 밖으로 삐져나온다.
            RectTransform dividerRect = divider.rectTransform;
            dividerRect.anchorMin = new Vector2(0f, 1f);
            dividerRect.anchorMax = new Vector2(1f, 1f);
            dividerRect.pivot = new Vector2(0.5f, 1f);
            dividerRect.offsetMin = new Vector2(0f, -HeaderHeight);
            dividerRect.offsetMax = new Vector2(0f, -(HeaderHeight - DividerThickness));
            divider.raycastTarget = false;

            // ---- 왼쪽: 이름 · Lv.N · EXP 진행선 ----
            _nameTitle = Label(barGo.transform, "CharacterName", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, HeaderPadLeft, -22f, 200f, 22f,
                CharacterProgressionModel.CharacterName, bold: true);
            _nameTitle.raycastTarget = false;

            float nameWidth = SettingsControls.MeasuredWidth(_nameTitle, CharacterProgressionModel.CharacterName);
            float levelX = HeaderPadLeft + nameWidth + UiChrome.Space2;

            _rankTitle = Label(barGo.transform, "LevelLabel", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.TextTertiary, levelX, -20f, HeaderLevelWidth, 16f, "Lv.1");
            _rankTitle.raycastTarget = false;

            // ★ L-5 — EXP 게이지는 헤더의 Lv.N <b>아래</b> 2pt 진행선이다. 컬럼 1에는 자리가 없고
            //   (무대 238 + 슬롯 4행 + 상세 카드가 예산을 다 쓴다), 여기가 레벨 바로 옆이라
            //   "이 숫자가 어디까지 찼는가"라는 인과가 붙는다.
            Image xpTrack = UiChrome.AddSurface(barGo.transform, "XpTrack",
                UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.PanelSurface), UiChrome.RadiusDot);
            UiChrome.PlaceTopLeft(xpTrack.rectTransform, levelX, -38f, HeaderLevelWidth, HeaderXpTrackHeight);
            xpTrack.raycastTarget = false;

            Image xpFill = UiChrome.AddSurface(xpTrack.rectTransform, "Fill", UiChrome.Accent, UiChrome.RadiusDot);
            _xpFill = xpFill.rectTransform;
            _xpFill.anchorMin = Vector2.zero;
            _xpFill.anchorMax = new Vector2(0f, 1f);
            _xpFill.pivot = new Vector2(0f, 0.5f);
            _xpFill.offsetMin = Vector2.zero;
            _xpFill.offsetMax = Vector2.zero;
            xpFill.raycastTarget = false;

            BuildTabs(barGo.transform, levelX + HeaderLevelWidth + UiChrome.Space6);

            // ---- 오른쪽: [✕] · 동전 칩 · 보유 칩 · [설정] ----
            // 스펙의 "ESC" 힌트 자리에 [✕]를 둔다 — 이유는 클래스 문서 참고(ESC는 이미 클릭관통
            // 긴급 해제에 묶여 있어서, 창 닫기를 겹치면 보이지 않는 부수효과가 생긴다).
            //
            // ★ 2026-09-02 — 면을 밝혔다. 근거·수치·왜 테두리가 아니라 면인지는 전부
            //   UiChrome "창을 닫는 법" 절 한 곳에 있다(세 표면 + 아래 [설정]이 같은 세 줄을 쓴다).
            Image closeSurface = UiChrome.AddSurface(barGo.transform, "CloseButton",
                UiChrome.ChromeButtonSurface, HeaderChipRadius);
            _closeRect = closeSurface.rectTransform;
            // 오른쪽 끝에 건다(고정 x였다면 좁은 화면에서 패널이 줄 때 [✕]만 창 밖에 남는다).
            _closeRect.anchorMin = _closeRect.anchorMax = _closeRect.pivot = new Vector2(1f, 1f);
            // ★ 32×32 — WCAG 2.2 SC 2.5.8 Target Size(Minimum, AA) 24×24를 넘는다(인계본 값 그대로).
            _closeRect.sizeDelta = new Vector2(HeaderChipHeight, HeaderChipHeight);
            _closeRect.anchoredPosition = new Vector2(-HeaderCloseChipInset, -HeaderChipY);
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

            // 동전 칩 — 인계본 재화 칩. 값은 CurrencyModel이 실제로 들고 있는 잔액이다.
            // ★ 2026-09-06부터 이 숫자에 <b>쓸 곳</b>이 생겼다([상점] 탭) — 라벨의 글리프는
            //   상점 가격표와 같은 한 자리(CharacterInfoWindow.Shop.cs의 CoinGlyph)에서 온다.
            _coinChipRect = BuildHeaderChip(barGo.transform, "CoinChip", HeaderCoinChipWidth,
                HeaderCoinChipInset,
                UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.PanelSurface),
                UiChrome.Flatten(UiChrome.AccentBorder, UiChrome.PanelSurface),
                UiChrome.Accent, CoinChipLabel, out _coinChipValue);

            // 보유 칩 — 잠금 해제한 장비 수 / 전체.
            _ownedChipRect = BuildHeaderChip(barGo.transform, "OwnedChip", HeaderOwnedChipWidth,
                HeaderOwnedChipInset,
                UiChrome.PanelSurface, UiChrome.Flatten(UiChrome.CardBorder, UiChrome.PanelSurface),
                UiChrome.TextPrimary, "보유", out _ownedChipValue);

            // ★ 2026-09-01 — 설정창(35-1)의 <b>주 진입점</b>. docs/UX_FLOW.md 36-11이 우클릭 메뉴 폐지에
            //   맞춰 "정보창 헤더의 작은 톱니"를 주 경로로 승격시켰다. 여기가 그 자리다.
            //   글자를 쓰는 이유: 이 프로젝트의 UI 폰트는 LegacyRuntime.ttf라 톱니 글리프(U+2699)가
            //   있다는 보장이 없고, 없으면 두부(□)가 뜬다.
            //   ★ 2026-09-02 — [설정]도 [닫기]와 <b>같은 면</b>을 쓴다(나란히 붙은 두 칩 중 하나만
            //     고치면 그 자리가 새로 어긋난다).
            Image settingsSurface = UiChrome.AddSurface(barGo.transform, "SettingsButton",
                UiChrome.ChromeButtonSurface, HeaderChipRadius);
            _settingsRect = settingsSurface.rectTransform;
            _settingsRect.anchorMin = _settingsRect.anchorMax = _settingsRect.pivot = new Vector2(1f, 1f);
            _settingsRect.sizeDelta = new Vector2(HeaderSettingsChipWidth, HeaderChipHeight);
            _settingsRect.anchoredPosition = new Vector2(-HeaderSettingsChipInset, -HeaderChipY);
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
        }

        /// <summary>헤더 오른쪽 재화/보유 칩 한 벌 — 라벨 + 값. <paramref name="rightInset"/>은
        /// 창 오른쪽 끝에서 칩 <b>오른쪽 모서리</b>까지의 거리다.
        ///
        /// <para>★ 2026-09-06 사용자 신고 — <i>"동전 보유 현황을 가운데 정렬, 지금 너무 밑으로 내려와 있음"</i>.
        /// 원인은 <b>피벗 불일치</b>였다. <see cref="Label"/>은 <see cref="UiChrome.PlaceTopLeft"/>를 쓰므로
        /// 넘기는 y는 상자의 <b>위 변</b> 좌표지, 상자의 <b>중심</b>이 아니다.
        /// 옛 식은 중앙식 <c>-(칩높이 - 글자높이) * 0.5</c>에 <b>글자 높이의 절반</b>을 한 번 더 뺐다
        /// (캡션 7 = 14/2, 값 8 = 16/2 — 두 상수가 정확히 반쪽이라는 것이 오타의 증거다).
        /// 그래서 두 상자 <b>위 변</b>이 나란히 −16, 즉 32pt 칩의 <b>정중앙</b>에 앉았고
        /// 글자는 칩의 <b>아래 절반</b>만 채웠다(값 상자는 아래 변이 −32로 칩 바닥선에 그대로 닿았다).
        /// 미세조정이 아니다 — 도입 커밋에 사유 주석이 없고, 아이콘도 균형을 맞출 상대도 이 칩에는 없다.</para>
        ///
        /// <para>지금 식은 순정 중앙 정렬이다: 캡션 위 변 −(32−14)/2 = −9, 아래 변 −23 → 중심 −16 = 칩 중심.
        /// 값 위 변 −(32−16)/2 = −8, 아래 변 −24 → 중심 −16. <b>두 칩(동전·보유)이 이 함수를 공유</b>하므로
        /// 여기서 한 번 고치면 양쪽이 함께 움직인다 — 반대로 <b>한쪽만 눈으로 보고 판정하면 안 된다</b>.</para></summary>
        private static RectTransform BuildHeaderChip(Transform parent, string name, float width,
            float rightInset, Color face, Color border, Color valueInk, string label, out Text value)
        {
            Image surface = UiChrome.AddSurface(parent, name, face, HeaderChipRadius);
            RectTransform rt = surface.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(width, HeaderChipHeight);
            rt.anchoredPosition = new Vector2(-rightInset, -HeaderChipY);
            surface.raycastTarget = false;
            UiChrome.AddOutline(rt, "Outline", border, HeaderChipRadius);

            Text caption = Label(rt, "Label", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.TextTertiary, HeaderChipPadX, -(HeaderChipHeight - 14f) * 0.5f,
                width - HeaderChipPadX * 2f, 14f, label);
            caption.raycastTarget = false;

            value = Label(rt, "Value", UiChrome.FontLabel, TextAnchor.MiddleRight, valueInk,
                HeaderChipPadX, -(HeaderChipHeight - 16f) * 0.5f,
                width - HeaderChipPadX * 2f, 16f, "—", bold: true);
            value.raycastTarget = false;
            return rt;
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

        // -------------------- 컬럼 1 — 프리뷰 무대 / 착용 슬롯 / 상세 카드 --------------------

        private void BuildColumn1(RectTransform body)
        {
            var go = new GameObject("Col1", typeof(RectTransform));
            go.transform.SetParent(body, false);
            var col = go.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(col, 0f, 0f, Col1Width, BodyHeight);
            _col1Root = go;

            Image columnDivider = UiChrome.AddSurface(col, "Col1Rule",
                UiChrome.Flatten(UiChrome.Divider, UiChrome.PanelSurface), 2);
            UiChrome.PlaceTopLeft(columnDivider.rectTransform, Col1Width - DividerThickness, 0f,
                DividerThickness, BodyHeight);
            columnDivider.raycastTarget = false;

            // ---- 프리뷰 무대 265 × 238 (L-3) ----
            // 바탕은 <b>잉크에서 파생</b>한다(§6-4) — 인계본의 "항상 어두운 무대"는 우리 규칙이 아니다.
            // 출하 기본은 검은 잉크 + 종이 바탕이고, 흰 잉크로 바꾸면 목탄으로 뒤집힌다.
            _portraitFrame = UiChrome.AddSurface(col, "Stage",
                CharacterPortraitStage.ResolveBackdropColor(_config), UiChrome.RadiusPanel);
            UiChrome.PlaceTopLeft(_portraitFrame.rectTransform, Col1PadX, StageY, Col1ContentWidth, StageHeight);
            _portraitFrame.raycastTarget = false;
            // ★ 생성값도 ApplyPortraitTheme와 <b>같은 문</b>을 쓴다 — 두 벌이면 한쪽만 고쳐진다
            //   (그 함수가 이 색을 매 테마 전환마다 덮어쓴다).
            _portraitBorder = UiChrome.AddOutline(_portraitFrame.rectTransform, "Border",
                UiChrome.EdgeOnSurface(CharacterPortraitStage.ResolveBackdropColor(_config)),
                UiChrome.RadiusPanel);

            // ============================================================================
            // ★★ 2026-09-06 (인계표 D1 / D2) — 무대 조명 두 겹을 <b>지웠다</b>. 되살리지 마라.
            // ============================================================================
            // 여기 "StageSheen"(<see cref="UiChrome.VerticalGradientFill"/> + PanelSheen α0.10)과
            // "StageFloorGlow"(<see cref="UiChrome.RadialGlow"/> + AccentSurface α0.14) 두 겹이 있었다.
            //
            // <b>왜 결함이었나</b>: 이 앱의 창은 전체화면 투명 오버레이라 프레임버퍼 알파가 곧 OS
            // 합성기의 마스크이고, uGUI의 Blend SrcAlpha OneMinusSrcAlpha는 알파 채널에도 같이 적용된다
            // (dstA' = srcA² + dstA(1−srcA) ⇒ 불투명 위에서 1 − α(1−α)).
            //     α0.10 → 0.9100 → 바탕화면 <b>9.00 %</b> 비침
            //     α0.14 → 0.8796 → 바탕화면 <b>12.04 %</b> 비침
            // 게다가 두 겹은 <b>84 %가 가려져 있었다</b> — 불투명 RawImage(RT)가 안쪽 249×222를
            // 나중에 덮으므로 실제로 보이던 것은 액자 테두리 8pt 띠뿐이었다. 즉 "조명"이 아니라
            // 「윗변의 밝은 선 + 바탕화면 구멍」이었다.
            //
            // <b>그림은 잃지 않았다</b>: 같은 조명이 촬영장(RenderTexture) <b>안</b>으로 옮겨갔고,
            // 알파 겹이 아니라 <b>정점 색(RGB)에 구운 램프</b>다 — 알파가 어디에서도 1 미만이 되지
            // 않으므로 블렌드 식과 무관하게 비침이 0이다(ux-designer 확정 §4-4 / 인계표 D3,
            // <see cref="CharacterPortraitStage.BackdropGlowColorAt"/>과 그 위 문단이 유도 전문).
            //
            // 이 삭제로 <see cref="UiChrome.VerticalGradientFill"/>의 «호출부 0건» 주석도 다시
            // 사실이 된다(D4). 회귀 잠금: Tests/EditMode/PortraitBackdropGlowAlphaTests.cs —
            // 그 파일의 <c>정보창_캔버스의_raw_알파_두_겹이_사라졌는가</c>가 이 자리의 소스를 읽는다.

            var imageGo = new GameObject("PortraitImage", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(_portraitFrame.transform, false);
            UiChrome.Stretch(imageGo.GetComponent<RectTransform>(), StagePadding);
            _portraitImage = imageGo.GetComponent<RawImage>();
            _portraitImage.raycastTarget = false;
            _portraitImage.enabled = false;   // RT가 준비되면 켠다.

            // ★ L-6 — 밝은 무대(검은 잉크)에서의 잉크색은 design-art 후속 판정 대기다. 임시로
            //   ApplyPortraitTheme()가 잉크에 따라 뒤집는다(밝은 바탕에서 TextTertiary는 안 읽힌다).
            _previewLabel = Label(_portraitFrame.rectTransform, "LabelPreview", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary, 14f, -14f, 120f, 12f, "PREVIEW");
            _previewLabel.raycastTarget = false;

            _portraitFallback = UiChrome.AddText(_portraitFrame.rectTransform, "PortraitFallback",
                UiChrome.FontBody, TextAnchor.MiddleCenter, UiChrome.TextTertiary, wrap: true);
            UiChrome.Stretch(_portraitFallback.rectTransform, UiChrome.Space4);
            _portraitFallback.text = "미리보기를 그릴 수 없어요";
            _portraitFallback.gameObject.SetActive(false);

            // ---- 착용 슬롯 4행 ----
            for (int i = 0; i < _slotRows.Length; i++) _slotRows[i] = BuildSlotRow(col, i);

            // ---- 상세 카드 (주 버튼 없음 — L-9) ----
            BuildDetailPanel(col);
        }

        /// <summary>착용 슬롯 한 줄 — 아이콘 / "카테고리 · 코드" / 착용 아이템 이름 / 등급 낱말.
        /// <para>인계본은 이 자리에 "집중력 +6" 같은 <b>스탯 기여값</b>을 적는데, 그 4스탯은
        /// 아직 런타임이 0줄이다(§1-3). 없는 값을 화면이 주장하지 않도록 지금은 <b>등급 낱말</b>을
        /// 적는다 — 실재하는 사실이고 같은 칸을 쓴다. 스탯이 들어오는 라운드(§8 4단계)에 교체한다.</para></summary>
        private SlotRowView BuildSlotRow(RectTransform col, int index)
        {
            Image surface = UiChrome.AddSurface(col, "SlotRow" + index, UiChrome.CardSurface, UiChrome.RadiusChip);
            var rt = surface.rectTransform;
            UiChrome.PlaceTopLeft(rt, Col1PadX, SlotRowsTopY - index * SlotRowStep,
                Col1ContentWidth, SlotRowHeight);
            surface.raycastTarget = false;
            Image outline = UiChrome.AddOutline(rt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);

            var iconGo = new GameObject("SlotIcon", typeof(RectTransform));
            iconGo.transform.SetParent(rt, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(SlotIconSize, SlotIconSize);
            irt.anchoredPosition = new Vector2(12f + SlotIconSize * 0.5f, 0f);

            float textX = 12f + SlotIconSize + 10f;
            float valueWidth = 74f;
            float textWidth = Col1ContentWidth - textX - valueWidth - 12f;

            Text label = Label(rt, "SlotLabel", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.TextTertiary, textX, -7f, textWidth, 12f, "—");
            Text name = Label(rt, "SlotName", UiChrome.FontBody, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, textX, -24f, textWidth, 16f, "—");
            Text value = Label(rt, "SlotValue", UiChrome.FontLabel, TextAnchor.MiddleRight,
                UiChrome.Accent, Col1ContentWidth - 12f - valueWidth, -16f, valueWidth, 14f, "—");

            return new SlotRowView
            {
                Rect = rt, Surface = surface, Outline = outline, IconRoot = irt,
                Label = label, Name = name, Value = value,
            };
        }

        // -------------------- 컬럼 2 — 능력치 / 기록 / 표시 / 테마 세트 --------------------

        /// <summary>
        /// ★ 2026-09-05 (2차) — 여기 있던 *"이 라운드는 자리를 만들지 않는다"*가 <b>닫혔다</b>.
        /// 사용자 확정 *"1.0에 전부 넣어야함"*으로 「능력치 / STATUS」 블록이 들어오고, 세트 패널이
        /// 고정 문구에서 <b>테마 진행도</b>로 바뀐다. 실물과 좌표는 <c>CharacterInfoWindow.Stats.cs</c>에 있다.
        ///
        /// <para>그 대가로 <b>컬럼 2가 세로로 넘친다</b>(§4-3-2 검산 그대로). 그래서 이 컬럼은 이제
        /// <see cref="ScrollRect"/> 하나이고, 네 블록의 y는 <see cref="LayoutColumn2"/> <b>한 곳</b>에서
        /// 정한다 — 「능력치」 높이가 계산기 유무로 바뀌면 아래 세 블록이 함께 움직여야 한다.</para>
        ///
        /// <para><b>블록 안의 좌표는 한 글자도 안 바뀌었다</b> — 각 블록은 자기 상단이 원점이고,
        /// 예전 코드가 쓰던 <c>x = Col2PadX</c>도 그대로다.</para>
        /// </summary>
        private void BuildColumn2(RectTransform body)
        {
            var go = new GameObject("Col2", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(body, false);
            var col = go.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(col, Col2X, 0f, Col2Width, BodyHeight);
            _col2Root = go;

            // 블록 사이 빈틈을 잡아도 끌리게 하는 투명 판 — 컬럼 3(<see cref="BuildSectionPage"/>)이
            // 쓰는 것과 <b>같은 장치</b>다. 그래픽이 없으면 uGUI 레이캐스트가 통과해 창 바탕이 잡히고,
            // 사용자에게는 "여기는 안 밀리네"로 보인다(휠도 같은 경로를 탄다).
            var col2Handle = go.GetComponent<Image>();
            col2Handle.color = Color.clear;
            col2Handle.raycastTarget = true;

            // 세로 구분선은 스크롤 <b>밖</b>이다 — 안에 넣으면 콘텐츠와 함께 밀려 경계가 중간에서 끊긴다.
            Image columnDivider = UiChrome.AddSurface(col, "Col2Rule",
                UiChrome.Flatten(UiChrome.Divider, UiChrome.PanelSurface), 2);
            UiChrome.PlaceTopLeft(columnDivider.rectTransform, Col2Width - DividerThickness, 0f,
                DividerThickness, BodyHeight);
            columnDivider.raycastTarget = false;

            RectTransform content = BuildColumn2Scroll(col);

            float x = Col2PadX;

            // ---- 능력치 / STATUS (§4-3-2 첫 블록) ----
            BuildStatusBlock(content);

            // ---- 기록 / RECORD ----
            RectTransform record = BuildCol2Block(content, Col2BlockRecord, "Col2Record",
                SectionLabelHeight + Col2LabelGap + GaugeBlockHeight + Col2LabelGap
                + StatCount * RecordRowHeight);
            float y = 0f;

            BuildSectionLabel(record, "기록", "RECORD", x, y);
            y -= SectionLabelHeight + Col2LabelGap;

            _stressFill = BuildGauge(record, "STRESS", x, y, UiChrome.TextPrimary, out _stressValue);
            y -= GaugeBlockHeight + Col2LabelGap;

            for (int i = 0; i < StatCount; i++)
            {
                Label(record, "RecordKey" + i, UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                    x, y, 110f, RecordRowHeight, StatLabels[i]);
                _statValues[i] = Label(record, "RecordValue" + i, UiChrome.FontBody, TextAnchor.MiddleRight,
                    UiChrome.TextPrimary, x + 110f, y, Col2ContentWidth - 110f, RecordRowHeight, "—");

                Image line = UiChrome.AddSurface(record, "RecordLine" + i,
                    UiChrome.Flatten(UiChrome.Divider, UiChrome.PanelSurface), 2);
                UiChrome.PlaceTopLeft(line.rectTransform, x, y - RecordRowHeight, Col2ContentWidth, DividerThickness);
                line.raycastTarget = false;
                y -= RecordRowHeight;
            }

            // ---- 표시 / DISPLAY — 이름 인라인 편집 + 잉크 스와치 ----
            // ★ 인계본은 이 둘을 [외형] 탭에 놓는다(§5). 그런데 잉크색 전환은 이 앱에서
            //   <b>유일한 GUI 경로</b>라 탭 하나에만 두면 발견 가능성이 떨어진다. 그래서 카드 탭
            //   양쪽에서 늘 보이는 컬럼 2에 둔다. 편집 자리는 <b>여기 하나뿐</b>이고 헤더 이름은
            //   읽기 전용이다 — 같은 값을 두 곳에서 고치면 한쪽이 조용히 낡는다.
            RectTransform display = BuildCol2Block(content, Col2BlockDisplay, "Col2Display",
                SectionLabelHeight + Col2LabelGap + NameRowHeight);
            y = 0f;

            BuildSectionLabel(display, "표시", "DISPLAY", x, y);
            y -= SectionLabelHeight + Col2LabelGap;

            float swatchRight = x + Col2ContentWidth;
            float nameWidth = Col2ContentWidth - (SwatchSize * 2f + SwatchGap) - UiChrome.Space3;

            // 히트 상자는 여전히 투명하다 — 손가락 넓히기용으로는 옳다. 달라진 것은 그것이
            // <b>유일한 어포던스가 아니게</b> 된 것이다(아래 연필 표식, NameEditGlyphSize 문단).
            Image nameHit = UiChrome.AddSurface(display, "NameHit", Color.clear, UiChrome.RadiusChip);
            _nameRect = nameHit.rectTransform;
            UiChrome.PlaceTopLeft(_nameRect, x, y, nameWidth, NameRowHeight);
            var nameButton = nameHit.gameObject.AddComponent<Button>();
            nameButton.targetGraphic = nameHit;
            nameButton.onClick.AddListener(() => { if (TryClaimAction("nameEdit")) BeginNameEdit(); });

            // 글자 상자는 표식 자리를 <b>비워 두고</b> 끝난다 — 긴 이름이 연필 위로 겹쳐 흐르면
            // 표식이 글자에 묻혀 없는 것과 같아진다(폭을 파생시키는 이유가 이것이다).
            float nameLabelWidth = nameWidth - NameEditGlyphSize - UiChrome.Space2;
            _nameLabel = Label(display, "NameEditable", UiChrome.FontBody, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, x, y, nameLabelWidth, NameRowHeight, CharacterProgressionModel.CharacterName);

            var glyphGo = new GameObject("NameEditGlyph", typeof(RectTransform));
            glyphGo.transform.SetParent(display, false);
            _nameEditGlyph = glyphGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(_nameEditGlyph, x + nameWidth - NameEditGlyphSize,
                y - (NameRowHeight - NameEditGlyphSize) * 0.5f, NameEditGlyphSize, NameEditGlyphSize);
            BuildNameEditGlyph(_nameEditGlyph);

            _nameInput = CreateInputField(display);
            _nameInputRect = _nameInput.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(_nameInputRect, x, y, nameWidth, NameRowHeight);
            _nameInputRect.gameObject.SetActive(false);

            for (int i = 0; i < 2; i++)
            {
                bool white = i == 1;
                float sx = swatchRight - (2 - i) * SwatchSize - (1 - i) * SwatchGap;
                var swatchGo = new GameObject(white ? "InkWhite" : "InkBlack", typeof(RectTransform), typeof(Image));
                swatchGo.transform.SetParent(display, false);
                var srt = swatchGo.GetComponent<RectTransform>();
                UiChrome.PlaceTopLeft(srt, sx, y - 6f, SwatchSize, SwatchSize);

                var fill = swatchGo.GetComponent<Image>();
                fill.sprite = UiChrome.Circle();
                fill.type = Image.Type.Simple;
                // ★ 2026-09-06 버그 수정: 예전엔 여기서 UiChrome.CardSurface/TextPrimary(다크 테마
                // UI 토큰)를 썼다 — CardSurface는 실제로 거의 검정(0.106,0.122,0.149), TextPrimary는
                // 거의 흰색(0.949,0.957,0.969)이라 "흰 잉크" 스와치가 어둡게, "검은 잉크" 스와치가
                // 밝게 그려졌다. 클릭 로직/색 적용 경로는 처음부터 옳았고(SetRuntimeInkColor →
                // ResolveInkColor → LineRenderer 전부 검증됨), 오직 이 미리보기 견본의 채움색만
                // 실제 잉크색이 아닌 UI 테마색을 잘못 참조해 "흰색을 누르면 검게, 검은색을 누르면
                // 희게 보인다"는 사용자 신고와 정확히 일치하는 시각적 반전을 냈다. 실제 잉크색
                // 필드로 교체(SettingsWindow.cs의 동일 스와치가 이미 쓰는 방식과 통일).
                fill.color = white
                    ? (_config != null ? _config.whiteInkColor : Color.white)
                    : (_config != null ? _config.primaryOutlineColor : Color.black);

                // 1.5px 링으로 "지금 이 색"을 표시한다(지름 12 기준 비율 = 1.5/12).
                // ★ 생성값도 <b>RefreshInkSwatches와 같은 규칙</b>으로 만든다(정책 §5-1 "생성값도 맞춰라").
                //   raw PanelBorder(α0.16)를 남기면 흰 견본에서 1.00:1인 링이 첫 갱신 전까지 살아 있고,
                //   그 화소의 창 알파가 내려가 바탕화면이 비친다.
                Image ring = UiChrome.AddCircle(srt, "Ring", SwatchSize,
                    UiChrome.EdgeOnSurface(fill.color), 1.5f);
                ring.raycastTarget = false;

                var button = swatchGo.AddComponent<Button>();
                button.targetGraphic = fill;
                button.onClick.AddListener(() => { if (TryClaimAction("ink" + (white ? 1 : 0))) OnInkSwatchClicked(white); });

                _inkRings[i] = ring;
                _inkRects[i] = srt;
            }

            // ---- 테마 세트 / SET ----
            BuildSetBlock(content);

            LayoutColumn2();
        }

        /// <summary>컬럼 2 섹션 제목 한 줄 — 브라스 바 3×14 + 한글 이름 + 영문 라벨.</summary>
        private static void BuildSectionLabel(RectTransform col, string title, string code, float x, float y)
        {
            Image bar = UiChrome.AddSurface(col, "SecBar_" + code, UiChrome.Accent, UiChrome.RadiusDot);
            UiChrome.PlaceTopLeft(bar.rectTransform, x, y - 2f, 3f, 14f);
            bar.raycastTarget = false;

            Text name = Label(col, "SecTitle_" + code, UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, x + 3f + UiChrome.Space2, y, 120f, SectionLabelHeight, title, bold: true);
            name.raycastTarget = false;

            float codeX = x + 3f + UiChrome.Space2 + SettingsControls.MeasuredWidth(name, title) + UiChrome.Space2;
            Text label = Label(col, "SecCode_" + code, UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.TextTertiary, codeX, y, 90f, SectionLabelHeight, code);
            label.raycastTarget = false;
        }

        /// <summary>라벨행(좌: 이름 / 우: 값) + 그 아래 4pt 트랙. 반환값은 채움 RectTransform.</summary>
        private RectTransform BuildGauge(RectTransform parent, string label, float x, float y,
            Color fillColor, out Text valueText)
        {
            Label(parent, "GaugeLabel_" + label, UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                x, y, 100f, GaugeLabelHeight, label);

            valueText = Label(parent, "GaugeValue_" + label, UiChrome.FontCaption, TextAnchor.MiddleRight,
                UiChrome.TextTertiary, x + 60f, y, Col2ContentWidth - 60f, GaugeLabelHeight, "—");

            // 트랙도 <b>미리 합성한 불투명색</b>이다(2026-08-31). TrackBackground(흰색 α0.09)를 그대로
            // 칠하면 게이지 막대 자리에서만 창 알파가 0.92로 내려간다 — 아래는 항상 창 바탕이라
            // 합성 결과 색은 같고 알파만 지켜진다(UiChrome.Flatten 문서 참고).
            Image track = UiChrome.AddSurface(parent, "GaugeTrack_" + label,
                UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.PanelSurface), UiChrome.RadiusDot);
            UiChrome.PlaceTopLeft(track.rectTransform, x, y - GaugeLabelHeight - GaugeLabelGap,
                Col2ContentWidth, TrackHeight);
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

        /// <summary>
        /// ★ 연필 표식 — 둥근 사각형 <b>두 조각</b>(몸통 + 촉)으로 만든다. 이 저장소에는 이미
        /// 같은 방식의 선례가 있다(<c>BuildLockGlyph</c>: 자물쇠를 몸통 + 고리로 만든다).
        ///
        /// <para>좌표 유도(부모 14 × 14, 중심 원점, 반쪽 = 7):
        /// 몸통 3.6 × 10을 Z −45°로 눕히면 축 방향 단위가 (0.707, 0.707)이다.
        /// 중심을 (1.0, 1.0)에 두면 <b>아래 끝</b>이 (1.0 − 3.54, 1.0 − 3.54) = (−2.54, −2.54),
        /// <b>위 끝</b>이 (4.54, 4.54)이고 폭 반쪽 1.8을 더해도 5.8 &lt; 7이라 상자 안이다.
        /// 촉은 3.6 × 3.6을 Z 45°로 돌린 마름모(대각 2.55)이고 (−4.1, −4.1)에 놓으면
        /// 끝이 (−6.65, −6.65)로 역시 7 안에 든다. <b>어느 조각도 상자를 넘지 않는다</b> —
        /// 넘으면 잘려서 연필이 아니라 막대 두 개로 보인다.</para>
        ///
        /// <para>두 조각 모두 <c>raycastTarget = false</c>다. 누름은 아래 깔린 <c>NameHit</c>이
        /// 받아야 하고, 여기서 클릭을 먹으면 <b>표식을 정확히 누른 사람만</b> 편집이 안 된다.</para>
        /// </summary>
        private static void BuildNameEditGlyph(RectTransform box)
        {
            Image body = UiChrome.AddSurface(box, "PencilBody", UiChrome.NonTextMuted, 1);
            RectTransform brt = body.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(3.6f, 10f);
            brt.anchoredPosition = new Vector2(1.0f, 1.0f);
            brt.localRotation = Quaternion.Euler(0f, 0f, -45f);
            body.raycastTarget = false;

            Image tip = UiChrome.AddSurface(box, "PencilTip", UiChrome.NonTextMuted, 1);
            RectTransform trt = tip.rectTransform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(3.6f, 3.6f);
            trt.anchoredPosition = new Vector2(-4.1f, -4.1f);
            trt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tip.raycastTarget = false;
        }

        private InputField CreateInputField(Transform parent)
        {
            Image surface = UiChrome.AddSurface(parent, "NameInput", UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.AddOutline(surface.rectTransform, "Outline",
                UiChrome.Flatten(UiChrome.PanelBorder, UiChrome.CardSurface), UiChrome.RadiusChip);

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
        ///
        /// <para>★ 2026-09-03 정정 — 원래 "초상화 포즈도 <b>같은 상태 값</b>에서 파생된다
        /// (CharacterPortraitStage.PoseForState)"라고 적혀 있었으나 <b>거짓</b>이다. 그 함수는
        /// 2026-09-02에 <b>프로덕션 호출자가 0개</b>가 됐고(정보창이 부르던 한 줄을 걷어냈다),
        /// <c>CharacterPortraitStage.SetPose</c>도 마찬가지다 — 같은 파일이 "새 프로덕션 호출자를
        /// 만들지 마라"고 명시적으로 못박고 있다. 즉 <b>액자는 상태를 따라가지 않고 이 글자만 따라간다.</b>
        /// 두 파일이 정면으로 어긋난 채 남아 있었다.</para>
        ///
        /// <para>★ 같은 이름이 두 벌이다 — <c>Core/StickMateDisplayNames.Of(StickmanStateId)</c>가
        /// 같은 27개 상태를 <b>다른 낱말</b>로 옮겨 행동 명령창의 "지금 ○○ 중이라 못 해요"에 쓴다
        /// (27개 중 19개가 다르다). 판정·통합안은 <c>docs/inspection/R2_거짓주석_전수조사.md</c> §2.
        /// <b>여기에 상태를 추가하면 저기에도 추가해야 한다</b> — 안 하면 저쪽은 "딴 일"로 조용히 샌다.</para>
        ///
        /// <para>★ 2026-09-07 — 이 함수의 <b>유일한 호출자였던 프레즌스 줄을 통째로 지웠다</b>
        /// (사용자 신고: "현재 상태 추적하지말고 다빼줘"). 그 어휘를 대조하던 테스트
        /// (<c>PortraitPaperDollTests.PresenceLineHoldsLongEnoughToBeRead</c>)도 같은 라운드에
        /// 함께 삭제됐다 — 지금 이 함수의 호출자는 <b>정말 0개</b>다. 그래도 지우지 않은 이유는
        /// 27개 상태를 사람이 읽는 한 마디로 옮기는 정본 표가 필요할 때 다시 만들지 않기 위해서다.
        /// <b>이 표를 다시 화면에 이어 붙이지 마라</b> — 사용자가 명시적으로 닫은 기능이다.</para></summary>
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
                case StickmanStateId.Attack: return "공격하는 중";
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
                case StickmanStateId.FocusStart: return "집중 모드 들어가는 중";
                case StickmanStateId.FocusComplete: return "집중 모드 마무리 중";
                case StickmanStateId.FocusCancelled: return "집중 모드 접는 중";
                case StickmanStateId.FocusNudge: return "딴짓 감시 중";
                case StickmanStateId.Sulky: return "부루퉁해 있는 중";
                case StickmanStateId.Runaway: return "가출 중";
                case StickmanStateId.Archery: return "활 쏘는 중";

                // ★ 2026-09-05 design-narrative N-4 — 예전에는 <c>id.ToString()</c>이었다. 상태를
                //   하나 늘리면 <b>영문 enum 이름이 사용자 화면에 뜬다</b>("지금 · WallSlide").
                //   지금 27=27이라 도달하지 않는 것은 <b>막는 장치가 아니라 우연</b>이다.
                //   낱말은 자매 표 <c>Core/StickMateDisplayNames</c>의 폴백("딴 일")과 같은 것을 쓴다.
                //   ★ 조용히 흘려보내지는 않는다 — 이 자리에 오는 것은 <b>정상값이 아니라 누락</b>이고,
                //     StateLabel은 상태가 바뀔 때만 불리므로 경고가 도배되지 않는다.
                default:
                    Debug.LogWarning($"[정보창] 프레즌스 라벨에 없는 상태 {id}입니다 — StateLabel에 " +
                        "한 줄을 더하세요(Core/StickMateDisplayNames에도 함께).");
                    return "딴 일 하는 중";
            }
        }
    }
}
