using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// 컬럼 2 「능력치 / STATUS」 + 「테마 세트 / SET」, 그리고 그 둘이 들어오면서 필요해진
    /// <b>컬럼 2 세로 스크롤</b>.
    ///
    /// ============================================================================
    /// 왜 이 조각이 생겼나
    /// ============================================================================
    /// <c>CharacterInfoWindow.cs</c>가 2026-09-05 이식 라운드에 이 자리를 <b>일부러 비워 뒀다</b>
    /// (그 파일의 「인계본 컬럼 2의 첫 블록은 능력치인데 그 런타임이 0줄이다」 문단). 사용자 확정
    /// *"1.0에 전부 넣어야함"*으로 그 자리를 채우는 라운드이고, 좌표 정본은
    /// <c>docs/UX_EQUIPMENT_WINDOW_3COL_PORT.md</c> §4-3-2, 문자열 정본은
    /// <c>docs/DESIGN_SYSTEMS_STATS.md</c> §0(교정 14/14) · §1-2 · §1-4 · §1-7 · §7-3 · §7-4다.
    ///
    /// <para>★ <b>파일을 쪼갤 때 파일명으로 소스를 찾는 감사가 눈이 먼다</b>(2026-09-02 실제 사고).
    /// 이 조각은 <c>CharacterInfoWindow.</c> 접두사 + <c>.cs</c> 확장자라는 기존 명명 규칙을
    /// 그대로 따랐고, 그 감사
    /// (<c>UiInteractionFramePacingHoldTests.ReadSurfaceSource</c> → <c>SourceConstantReader.ReadSurfaceText</c>)는
    /// 그 접두사로 시작하는 파일을 <b>전부</b> 이어 읽으므로 자동으로 따라온다. 접두사를 다르게
    /// 지으면 그 순간 감사가 이 파일을 못 본다.</para>
    ///
    /// ============================================================================
    /// ★ 세로 예산 — 설계 폭에서는 <b>들어간다</b>. 여유 16pt다
    /// ============================================================================
    /// 스탯 카드 4장(326pt)이 들어오면 컬럼 2가 넘친다는 것이 §4-3-2의 예상이었다
    /// (*"사용 750 &gt; 예산 695 → 스크롤한다"*). 우리 세트 패널이 인계본 193이 아니라 56이고
    /// 블록 사이 간격을 제목-내용 간격(8)보다 크게(12) 잡으면 <b>넘치지 않는다</b>:
    /// <code>
    /// 위 여백 20
    ///  + STATUS  18 + 8 + 326 = 352
    ///  + 12
    ///  + RECORD  18 + 8 + 21 + 8 + 104 = 159
    ///  + 12
    ///  + DISPLAY 18 + 8 + 25 = 51
    ///  + 12
    ///  + SET     18 + 8 + 56 = 82
    ///  + 아래 여백 20                          = 720   vs  BodyHeight 736   → 여유 16
    /// </code>
    /// 이 등식은 <c>CharacterStatColumnLayoutTests</c>가 <b>상수에서 다시 세서</b> 잠근다 —
    /// 숫자 하나를 늘리는 사람이 그 자리에서 알게 된다.
    ///
    /// ============================================================================
    /// ★★ 코스튬 3행이 켜져도 <b>잘리는 픽셀은 0</b>이다 — 「4pt 초과」는 전부 아래 여백이었다
    /// ============================================================================
    /// 세트 패널이 56 → 76pt가 되면 위 합이 <b>740</b>이고 <c>BodyHeight</c>는 736이다.
    /// <code>
    ///  + SET     18 + 8 + 76 = 102   (기존 82에서 +20)
    ///  합계                    = 740   vs  BodyHeight 736
    /// </code>
    /// ★ <b>2026-09-08 정정(ux-designer §14-2·§14-5 판정).</b> 여기 있던 *"4pt 초과 · 계약이 깨진다"*는
    /// <b>재는 양이 틀린 것이었다.</b> 위 합계는 「마지막 블록 뒤에 아래 여백 20을 더한 값」인데
    /// <b>아래 여백에는 그려지는 것이 하나도 없다.</b> 잉크가 실제로 끝나는 자리를 따로 세면:
    /// <code>
    ///  SET 블록  제목 618..636 · 패널 644..720
    ///        1행 655..671 · 2행 677..691 · 3행 697..711
    ///  720 ← ★ 여기서 잉크가 끝난다        vs  뷰포트 736  →  잘리는 잉크 <b>0pt</b>
    ///         잉크 아래로 보이는 여백 16 (= UiChrome.Space4)
    /// </code>
    /// ⇒ 3행이 켜져서 일어난 일은 <b>「잘림」이 아니라 「여백이 20에서 16으로 줄었다」</b>이고,
    /// 네 블록은 설계 크기에서 <b>전부 100% 보인다</b>. 그래서 4pt를 <b>내지 않는다</b> —
    /// 후보 넷(<see cref="ColPadY"/> 축소 · <see cref="SetRowGap"/> 축소 · 3행 높이 14→10 ·
    /// 창 높이 확대)이 전부 «결함 0건인 자리의 실제 품질을 깎는 거래»로 기각됐다(§14-4).
    ///
    /// <para>대신 계약을 <b>둘로 갈랐다</b>(§14-5). <c>CharacterStatColumnLayoutTests</c>와
    /// <c>CostumeProgressReadoutTests</c>가 <b>서로 다른 식으로</b> 이 둘을 잠근다:
    /// <code>
    ///  C1 「잉크 불가침」        ColPadY + Σ블록 + Σ간격  ≤ BodyHeight     720 ≤ 736
    ///  C2 「아래 여백 잔량」     BodyHeight − 잉크하단     ≥ Space4(16)     16 ≥ 16  ← 정확히 하한
    /// </code>
    /// 겉보기로 4pt를 내주지만 <b>C2가 여유 0이라 다음 1pt에서 즉시 빨개진다</b> — 예전의
    /// <c>Assert.Ignore</c>(건너뜀은 회귀를 못 잡는다)보다 <b>감시가 강해졌다</b>.</para>
    ///
    /// <para>★★ <b>3행은 「나중 일」이 아니라 오늘 도달 가능하다</b>(2026-09-07 실측 정정).
    /// 처음에 나는 *"실린 코스튬 매니페스트가 0개라 3행이 안 뜬다"*고 적었는데 <b>틀렸다</b> —
    /// 그 프로브가 죽어 있었다(<c>.asset</c>은 스크립트를 <b>이름이 아니라 GUID</b>로 가리키므로
    /// 이름으로 훑으면 0건이 나오고, 그 0건은 «정말 없다»와 <b>똑같이 생겼다</b>. 양성 대조
    /// — 같은 방식으로 <c>AccessoryDefSO</c>를 세면 42가 나와야 한다 — 가 0을 내는 것을 보고 잡았다).
    /// GUID로 다시 세니 <b>1개</b>다: <c>CostumeManifest_office</c>(기본 코호트 테마 <c>office</c>).
    /// 기본 코호트 코스튬은 <b>엔타이틀먼트를 묻지 않으므로</b>, 스탯 4부위를 <c>office</c> 테마로
    /// 갖춰 입으면 <b>지금 이 빌드에서 3행이 뜬다</b>.</para>
    ///
    /// ============================================================================
    /// 그래도 스크롤을 붙인 이유 — 설계 폭이 아닌 화면이 있다
    /// ============================================================================
    /// <c>ClampPanelToScreen</c>이 세로가 짧은 화면에서 창을 줄인다(§3-4: Windows 1920×1080@150%는
    /// 가용 688로 40pt 모자란다). 그때 컬럼 2 아래쪽은 <c>RectMask2D</c>에 잘린다.
    ///
    /// <para>★★ <b>2026-09-08 정정 — 여기 있던 *"스크롤이 그 구멍을 닫는다"*는 거짓이었다</b>
    /// (결함 W-1, <c>ux-designer</c> §14-6이 잡았다). 스크롤은 붙었는데 <b>뷰포트가 컴파일 타임
    /// 상수 736에 얼어붙어 있어서</b> <c>MaxCol2Scroll()</c>의 분모가 실제 Body가 아니었다:
    /// Windows 1366×768@100%(Body 670)에서 세트 패널 <b>2행이 통째로 안 보이는데 최대 스크롤은 0</b>,
    /// 1920×1080@150%(Body 622)에서는 세트 블록이 <b>4pt만</b> 보였다. <b>되찾을 방법이 없었다.</b>
    /// 그리고 그 문장 때문에 <b>다들 안심하고 있었다</b> — 이 저장소가 반복해 당한
    /// *"죽은 프로브가 산 프로브와 똑같이 생겼다"*의 UI판이다.</para>
    /// <para><b>고쳤다</b>: <see cref="SyncColumnLayout"/>이 Body의 실제 높이를 받아
    /// <c>Col2Viewport</c>·컬럼 루트·컬럼 3 페이지를 함께 줄인다(<see cref="_bodyHeight"/>).
    /// 이제 Body 670에서 최대 스크롤이 50, Body 622에서 118이 되어 <b>전부 도달한다</b>.
    /// 회귀는 <c>InfoWindowShrunkPanelReachTests</c>가 <b>실제로 밀어 보고</b> 잠근다.</para>
    /// <para><b>설계 크기에서는 최대 스크롤이 0이라 화면이 예전과 같다</b> — 3행이 켜져도 그렇다
    /// (<see cref="LayoutColumn2"/>의 «아래 여백은 뷰포트가 남긴 만큼만» 규칙이 4pt 가짜 스크롤을 없앴다).</para>
    ///
    /// <para>드래그가 두 벌인 이유는 컬럼 3 격자와 <b>같다</b> — 이 창의 실제 클릭 경로는 전역 폴링이고
    /// uGUI 이벤트는 앱이 활성화된 뒤에만 도착한다. 두 경로가 「잡은 순간의 content.y + 이동량」이라는
    /// 같은 절대값 공식을 쓰므로 동시에 돌아도 더해지지 않는다(<c>DragGridTo</c> 문단과 같은 유도).</para>
    ///
    /// ============================================================================
    /// ★ 임계 효과 문구(§1-5) 12칸은 <b>그리지 않는다</b>
    /// ============================================================================
    /// <see cref="EquipmentStatRules.IsTierEffectActive"/>가 오늘 <b>12칸 전부 false</b>이고
    /// <see cref="EquipmentStatRules.ActiveTierEffectCount"/>가 0이다. 문구를 그리면 화면이
    /// 「오라 이펙트 발현」처럼 <b>일어나지 않는 일</b>을 약속하게 된다(원칙 1 — 유예 자동 해금이
    /// 폐기됐는데 화면이 계속 약속하던 그 결함과 같은 형태).
    /// <b>효과가 하나라도 배선되면</b> 그 함수가 true를 돌려주기 시작하고, 그때 이 자리에
    /// <c>TierEffectText</c>를 켜면 된다 — 자리는 3행에 이미 있다(단계 칩 오른쪽).
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        // ==================== 치수 (§4-3-2) ====================

        /// <summary>주스탯 축의 개수. <b>숫자를 적지 않는다</b> — 규칙(<see cref="EquipmentStatRules"/>)이
        /// 정한 값을 그대로 쓴다. 그 값 자체가 저장 필드 <c>statTierReached[]</c>의 칸 수에서
        /// 유도돼 있어서, 세 축(규칙 · 저장 · 화면)이 <b>한 숫자</b>에 묶인다.</summary>
        internal const int StatAxisCount = EquipmentStatRules.StatCount;

        /// <summary>임계 눈금 개수 = 임계 단계 수. ★ <see cref="EquipmentStatRules.TierCount"/>는
        /// <c>readonly</c> 배열 길이라 <c>const</c>가 아니다 — 배열 크기는 저장 상한
        /// (<see cref="CurrencyRules.MaxStatTier"/>)에서 잡고, <b>둘이 같은지는 테스트가 매 실행 잰다</b>
        /// (Core가 자기 파일에 적어 둔 것과 같은 계약이다).</summary>
        internal const int StatTickCount = CurrencyRules.MaxStatTier;

        internal const float StatCardHeight = 74f;
        internal const float StatCardGap = 10f;
        internal const float StatCardPadX = 11f;
        internal const float StatCardPadY = 11f;

        /// <summary>스탯 카드 1행(이름 · 출처 · 총합 · 보너스).</summary>
        internal const float StatHeadHeight = 18f;

        /// <summary>1행 ↔ 게이지 ↔ 3행 사이 간격(§4-3-2 내부 검산의 8).</summary>
        internal const float StatRowGap = 8f;

        /// <summary>총합 게이지 두께. 컬럼 2의 STRESS 트랙(<see cref="TrackHeight"/> 4)보다 1pt 두껍다 —
        /// 인계본 값이고, 이 게이지만 <b>임계 눈금</b>을 얹기 때문이다.</summary>
        internal const float StatGaugeHeight = 5f;

        /// <summary>스탯 카드 3행(단계 칩 + 「중급까지 N」).</summary>
        internal const float StatFootHeight = 13f;

        /// <summary>단계 칩 높이(§4-3-2). 3행보다 높아서 위아래로 2.5pt씩 패딩을 파고든다 —
        /// 패딩이 <see cref="StatCardPadY"/> 11이라 카드 밖으로는 나가지 않는다.</summary>
        internal const float StatTierChipHeight = 18f;
        internal const float StatTierChipPadX = 7f;

        /// <summary>카드 내부 콘텐츠 폭.</summary>
        internal const float StatCardContentWidth = Col2ContentWidth - StatCardPadX * 2f;   // 234

        // 1행 오른쪽 두 칸 — 오른쪽 끝에서 안쪽으로 쌓는다(§7-3: 값도 증분도 최대 2자리).
        internal const float StatBonusWidth = 40f;    // "(+33)"
        internal const float StatTotalWidth = 28f;    // "41"
        internal const float StatValueGap = 4f;
        internal const float StatNameWidth = 54f;     // "관찰력"
        internal const float StatNameGap = 6f;

        /// <summary>3행 오른쪽 「중급까지 N」 칸.</summary>
        internal const float StatRemainWidth = 92f;

        /// <summary>단계 칩이 가질 수 있는 최대 폭 — 「중급까지 N」과 겹치지 않는 한계다.
        /// <b>굽는 쪽과 재는 쪽이 같은 값을 본다</b>(둘이 갈라지면 긴 낱말에서만 겹친다).</summary>
        internal const float StatTierChipMaxWidth = StatCardContentWidth - StatRemainWidth - StatValueGap;

        /// <summary>스탯 카드 4장이 실제로 먹는 높이.</summary>
        internal const float StatCardsHeight = StatAxisCount * StatCardHeight
                                              + (StatAxisCount - 1) * StatCardGap;          // 326

        /// <summary>섹션 제목 오른쪽 「장비 합 +24」 칸.</summary>
        internal const float StatSumWidth = 74f;

        /// <summary>블록(능력치 / 기록 / 표시 / 테마 세트) <b>사이</b> 간격.
        /// <para>★ 2026-09-05 <b>24 → 12</b>. 스탯 카드 326pt가 들어오면서 세로 예산이 20pt 모자랐고
        /// (계산은 클래스 문서), 줄일 수 있는 곳이 여기와 제목-내용 간격뿐이었다.
        /// <b>블록 사이가 제목-내용보다 커야</b> 제목이 자기 내용에 붙어 읽힌다 — 그 순서를 지키면서
        /// 24+12를 12+8로 내렸다. 인계본은 둘 다 14이고, 그 값이면 20pt 넘친다.</para></summary>
        internal const float Col2BlockGap = UiChrome.Space3;

        /// <summary>제목줄과 <b>그 내용</b> 사이 간격. 위 문단과 짝이다 — 항상
        /// <see cref="Col2BlockGap"/>보다 작아야 하고, 그 부등식을
        /// <c>CharacterStatColumnLayoutTests</c>가 잠근다.</summary>
        internal const float Col2LabelGap = UiChrome.Space2;

        /// <summary>구분선을 뺀 컬럼 2의 실제 표시 폭(뷰포트 폭).</summary>
        internal const float Col2ViewportWidth = Col2Width - DividerThickness;   // 291

        /// <summary>세트 패널 높이.</summary>
        internal const float SetPanelHeight = 56f;

        /// <summary>세트 패널 1행 「오피스 워커 3/4」.</summary>
        internal const float SetProgressHeight = 16f;

        /// <summary>세트 패널 2행 「4부위 합계 +8」 / 「미완성 · 기본 중립 대사」.</summary>
        internal const float SetDetailHeight = 14f;

        internal const float SetRowGap = 6f;

        /// <summary>2행의 위 끝. <b>1행에서 파생시킨다</b> — 세 값 중 하나가 바뀌면 따라온다.
        /// 검산: 11 + 16 + 6 + 14 + 9(아래 여백) = 56 = <see cref="SetPanelHeight"/>.</summary>
        internal const float SetDetailY = -(StatCardPadY + SetProgressHeight + SetRowGap);

        // ==================== 세트 패널 3행 — 코스튬 진화 진행률 (P5-b) ====================
        //
        // 정본: docs/DESIGN_SYSTEMS_COSTUME_EVOLUTION.md §7-2(표시 형식) · §7-4(자리 예산),
        //       docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md 7-5절(읽을 값 3개).
        //
        // ★ 이 행은 <b>있다가 없다가 한다</b>. 코스튬이 안 잡히면 행 자체가 없고 패널은 2행 56pt
        //   그대로다(§7-2: «팩 미소유 / 세트 미완성 → 퍼센트도 시간도 그리지 않는다»). 자리만
        //   비워 두면 «쌓이는데 0»이 아니라 «20pt짜리 빈 구멍»이 되고, 그 구멍은 코스튬을 안 갖춘
        //   <b>거의 모든 사용자에게 상시</b>다(1일차 무료 4종은 mil이라 office 코스튬이 안 선다).

        /// <summary>3행 높이. 2행과 같은 캡션 줄이라 <b>같은 상수를 쓴다</b> —
        /// 새 숫자를 만들면 캡션 줄 높이가 두 벌이 된다.</summary>
        internal const float SetCostumeHeight = SetDetailHeight;

        /// <summary>3행의 위 끝. <b>2행에서 파생시킨다</b>(<see cref="SetDetailY"/>와 같은 형태) —
        /// 위 줄 높이나 줄 간격이 바뀌면 따라온다.</summary>
        internal const float SetCostumeY = SetDetailY - (SetDetailHeight + SetRowGap);

        /// <summary>
        /// 3행이 <b>켜졌을 때</b>의 세트 패널 높이. 검산: 11 + 16 + 6 + 14 + 6 + 14 + 9 = 76
        /// (§7-4의 «56 → 76pt(+20)»와 같은 값이고, 그 등식을 <c>CostumeProgressReadoutTests</c>가
        /// <b>다른 식으로</b> 다시 세서 잠근다 — 여기서는 «2행 높이 + 간격 + 3행»으로,
        /// 저쪽에서는 «위 여백부터 아래 여백까지»로).
        /// <para>★ <b>세로 예산 — 잘리는 잉크는 0이다</b>(잉크 하단 720 ≤ <see cref="BodyHeight"/> 736,
        /// 아래로 보이는 여백 16 = <see cref="UiChrome.Space4"/>). 예전에 여기 적혀 있던 «4pt 초과»는
        /// 「잉크 + 아래 여백 20」을 재던 값이고, <b>그 여백에는 그려지는 것이 없다</b>(클래스 문서 §정정).
        /// 계약은 C1「잉크 불가침」 + C2「아래 여백 잔량 ≥ 16」 둘로 갈라져 있고 <b>C2는 지금 정확히
        /// 하한</b>이라, 이 값을 1pt만 키워도 테스트가 즉시 빨개진다.</para>
        /// </summary>
        internal const float SetPanelHeightWithCostume = SetPanelHeight + SetRowGap + SetCostumeHeight;

        /// <summary>3행 오른쪽 퍼센트 칸. 최댓값은 <c>99.9%</c> 다섯 글자다 —
        /// <b><c>100.0%</c>라는 화면은 이 설계에 존재하지 않는다</b>(§7-2: 마스터가 되는 순간
        /// 퍼센트 칸이 사라진다). 캡션 10pt에서 ASCII 다섯 글자는 30pt 안쪽이라 여유가 절반 이상이다.</summary>
        internal const float SetCostumePercentWidth = 44f;

        /// <summary>3행 두 칸 사이. 스탯 카드 1행이 쓰는 값과 <b>같은 상수</b>다.</summary>
        internal const float SetCostumeGap = StatValueGap;

        /// <summary>3행 왼쪽 누적 시간 칸. <b>남는 폭 전부</b>다 — 「9999시간 59분」까지 들어간다.</summary>
        internal const float SetCostumeTimeWidth =
            StatCardContentWidth - SetCostumePercentWidth - SetCostumeGap;   // 186

        /// <summary>
        /// Core가 <see cref="CostumeEvolutionRules.PercentTenthsToNextBoundary"/>로 돌려주는 값의
        /// <b>축척</b>(1/1000). ★ 임계값이 아니라 <b>단위</b>다 — 소수 1자리를 그리려면 화면이 이
        /// 축척을 알아야 하고, 그 하나에서 «자릿수 나누기»와 «100.0%가 뜨면 안 된다»는 상한이
        /// <b>둘 다</b> 나온다. 임계(10h/50h/100h)와 캡(240분)은 여기 한 글자도 없다 —
        /// 전부 <see cref="CostumeEvolutionRules"/>가 유일한 출처다.
        /// </summary>
        internal const int CostumePercentTenthsScale = 1000;

        /// <summary>1%p에 해당하는 천분율. 위 축척에서 유도한다.</summary>
        internal const int CostumePercentTenthsPerPoint = CostumePercentTenthsScale / 100;

        // 블록 색인 — 순서가 곧 화면의 위에서 아래다.
        private const int Col2BlockStatus = 0;
        private const int Col2BlockRecord = 1;
        private const int Col2BlockDisplay = 2;
        private const int Col2BlockSet = 3;
        private const int Col2BlockCount = 4;

        // ==================== 실물 ====================

        /// <summary>컬럼 2의 세로 스크롤. 컬럼 3 격자와 <b>같은 설정</b>이다(Clamped · 관성 없음).</summary>
        private ScrollRect _col2Scroll;
        private RectTransform _col2Viewport;
        private RectTransform _col2Content;

        private bool _col2Grabbed;
        private bool _col2Moved;
        private float _col2GrabScreenY;
        private float _col2StartContentY;

        /// <summary>세로 흐름의 한 덩어리. 블록 하나의 높이가 바뀌면 <b>아래 블록의 y가 전부
        /// 움직인다</b> — 그래서 좌표를 네 곳에 굳히지 않고 <see cref="LayoutColumn2"/> 한 곳에서
        /// 정한다(<see cref="LayoutCardGrid"/>와 같은 이유·같은 형태).</summary>
        private sealed class Col2Block
        {
            public RectTransform Rect;
            public float Height;
        }

        private readonly Col2Block[] _col2Blocks = new Col2Block[Col2BlockCount];

        /// <summary>스탯 카드 한 장의 부품. 문자열은 <b>입력이 바뀐 프레임에만</b> 다시 만든다 —
        /// <see cref="RefreshNumbers"/>가 0.25초마다 도는 경로라, 무조건 만들면 하루 종일 켜 두는
        /// 앱에서 쓰레기가 계속 쌓인다(카드 이름의 <see cref="ItemCard.NameSource"/>와 같은 장치).</summary>
        private sealed class StatCardView
        {
            public RectTransform Rect;
            public Text Name;
            public Text Source;
            public Text Total;
            public Text Bonus;
            public RectTransform GaugeFill;
            public readonly Image[] Ticks = new Image[StatTickCount];
            public Image ChipSurface;
            public RectTransform ChipRect;
            public Text ChipLabel;
            public Text Remain;

            /// <summary>마지막으로 그린 입력. <c>HasShown</c>이 false면 무조건 다시 그린다.</summary>
            public bool HasShown;
            public int ShownTotal;
            public int ShownBonus;
            public int ShownTier;
            public int ShownReachedTier;
        }

        private readonly StatCardView[] _statCards = new StatCardView[StatAxisCount];

        /// <summary>섹션 제목 오른쪽 「장비 합 +24」.</summary>
        private Text _statSumValue;

        /// <summary>세트 패널 — 준비 중 문구 한 벌과 진행도 두 줄 한 벌을 <b>둘 다</b> 만들어 두고
        /// 켜고 끈다. 문구를 그때그때 바꾸면 「준비 중」과 「미완성」이 같은 Text에 섞여
        /// 어느 쪽이 참인지 로그로 되짚을 수 없다.
        /// <para>★ 2026-09-06 정정 — <b>진행도 쪽이 실제로 뜬다.</b> 앞선 서술("기본 42종의 테마가
        /// 전부 미배정이라 항상 false")은 <c>coder-systems</c>가 기본 24종(스탯 4슬롯 × 6테마)에
        /// 테마를 배정하면서 <b>거짓이 됐다</b>(<see cref="ItemCatalog.ThemeOfItem"/>).
        /// <see cref="CharacterStatReadout.TryGetThemeProgress"/>는 <b>테마가 붙은 것을 한 부위라도
        /// 걸치면</b> true다. 준비 중 문구가 남는 경우는 「스탯 4슬롯에 테마가 붙은 것이 하나도 없다」
        /// — 아무것도 안 걸쳤거나 외형·팩 아이템만 걸쳤을 때다.</para>
        /// <para>★ 2026-09-08 — 문구(<c>SetPanelNotice</c>)는 이미 참인 문장으로 고쳐져 있고
        /// (「테마가 있는 장비를 아직 하나도 걸치지 않았습니다」), 이번 라운드가 고친 것은
        /// <b>표시 조건</b>이다: <c>!hasProgress &amp;&amp; !3행</c>. 근거는 <see cref="RefreshSetPanel"/>
        /// 안의 그 문단에 있다(§14-8 — 문안으로는 못 고치는 종류의 결함이다).</para></summary>
        private GameObject _setNoticeRoot;
        private GameObject _setReadyRoot;
        private Text _setProgress;
        private Text _setDetail;

        /// <summary>세트 패널 실물. <b>높이가 런타임에 바뀌는 유일한 면</b>이라 참조를 들고 있다 —
        /// 3행(코스튬 진행률)이 켜지면 56 → <see cref="SetPanelHeightWithCostume"/>가 된다.</summary>
        private RectTransform _setPanelRect;

        /// <summary>
        /// 세트 패널 3행 — 코스튬 진화 진행률(§7-2). <b>준비 중 문구·진행도 두 줄과 형제</b>이고
        /// 그 둘 중 무엇이 켜져 있든 <b>독립으로</b> 뜬다.
        ///
        /// <para>★ 형제여야 하는 이유가 실측에 있다: 팩 코호트 아이템은
        /// <see cref="ItemCatalog.ThemeOfItem"/>이 구조적으로 빈 테마를 주므로
        /// <see cref="CharacterStatReadout.TryGetThemeProgress"/>가 <c>false</c>다 ⇒ 팩 코스튬을
        /// 입은 사람에게는 <b>준비 중 문구</b> 쪽이 떠 있다. 3행을 진행도 묶음 안에 넣으면
        /// <b>유료 팩에서만 진행률이 통째로 안 보인다</b>.</para>
        /// </summary>
        private GameObject _setCostumeRoot;

        /// <summary>3행 왼쪽 — 누적 시간("12시간 30분").</summary>
        private Text _setCostumeTime;

        /// <summary>3행 오른쪽 — 다음 경계까지("37.4%"). <b>마스터면 빈 문자열</b>이다(§7-2:
        /// 마스터는 퍼센트를 그리지 않는다). <c>100.0%</c>라는 화면은 이 설계에 존재하지 않는다.</summary>
        private Text _setCostumePercent;

        // 3행 메모 — <see cref="RefreshNumbers"/>가 0.25초마다 도는 경로라 입력이 그대로면
        // 문자열을 만들지 않는다(스탯 카드의 HasShown/Shown* 와 같은 장치).
        private bool _setCostumeHasShown;
        private string _setCostumeShownKey;
        private int _setCostumeShownMinutes;

        // ==================== 굽기 ====================

        /// <summary>
        /// 컬럼 2 루트를 <b>스크롤 컨테이너</b>로 만든다. 세로 구분선(<c>Col2Rule</c>)은 뷰포트 <b>밖</b>에
        /// 둔다 — 안에 넣으면 콘텐츠와 함께 밀려 올라가 컬럼 경계가 중간에서 끊긴다.
        /// </summary>
        private RectTransform BuildColumn2Scroll(RectTransform col)
        {
            var viewportGo = new GameObject("Col2Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(col, false);
            _col2Viewport = viewportGo.GetComponent<RectTransform>();
            // ★ W-1 — 상수 736이 아니라 <b>지금 Body</b>다. 굽는 시점에는 둘이 같지만, 화면이 낮아
            //   창이 줄어든 뒤에 다시 구우면 갈라진다(<see cref="_bodyHeight"/> 문단).
            UiChrome.PlaceTopLeft(_col2Viewport, 0f, 0f, Col2ViewportWidth, _bodyHeight);

            var contentGo = new GameObject("Col2Content", typeof(RectTransform));
            contentGo.transform.SetParent(_col2Viewport, false);
            _col2Content = contentGo.GetComponent<RectTransform>();
            _col2Content.anchorMin = _col2Content.anchorMax = _col2Content.pivot = new Vector2(0f, 1f);
            _col2Content.sizeDelta = new Vector2(Col2ViewportWidth, _bodyHeight);
            _col2Content.anchoredPosition = Vector2.zero;

            _col2Scroll = col.gameObject.GetComponent<ScrollRect>();
            if (_col2Scroll == null) _col2Scroll = col.gameObject.AddComponent<ScrollRect>();
            _col2Scroll.viewport = _col2Viewport;
            _col2Scroll.content = _col2Content;
            _col2Scroll.horizontal = false;
            _col2Scroll.vertical = true;
            // 컬럼 3과 같은 이유로 Clamped + 관성 없음 — 전역 폴링 드래그와 계산이 같아야 한다.
            _col2Scroll.movementType = ScrollRect.MovementType.Clamped;
            _col2Scroll.inertia = false;
            _col2Scroll.scrollSensitivity = StatCardHeight * 0.5f;
            _col2Scroll.horizontalScrollbar = null;
            _col2Scroll.verticalScrollbar = null;
            return _col2Content;
        }

        /// <summary>블록 하나를 만들어 등록한다. y는 <see cref="LayoutColumn2"/>가 정하므로
        /// 여기서는 <b>폭과 높이만</b> 맞춘다.</summary>
        private RectTransform BuildCol2Block(RectTransform content, int index, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(content, false);
            var rt = go.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(rt, 0f, 0f, Col2ViewportWidth, height);
            _col2Blocks[index] = new Col2Block { Rect = rt, Height = height };
            return rt;
        }

        /// <summary>
        /// 「능력치 / STATUS」 블록 — 제목줄(+ 장비 합) + 스탯 카드 4장.
        /// <para>★ <b>「준비 중」 분기를 두지 않는다.</b> <see cref="EquipmentStatRules.CurrentBuild"/>는
        /// 아무것도 안 걸친 상태에서도 <c>BASE</c>(8/6/5/7)라는 <b>참인 값</b>을 돌려준다 — 값이 없는
        /// 상태가 존재하지 않으므로 그 분기는 도달 불가능한 코드가 된다.</para>
        /// </summary>
        private void BuildStatusBlock(RectTransform content)
        {
            RectTransform block = BuildCol2Block(content, Col2BlockStatus, "Col2Status",
                SectionLabelHeight + Col2LabelGap + StatCardsHeight);

            BuildSectionLabel(block, "능력치", "STATUS", Col2PadX, 0f);

            // 제목줄 오른쪽 「장비 합 +24」(§4-3-2 · §0 교정).
            _statSumValue = Label(block, "StatSum", UiChrome.FontLabel, TextAnchor.MiddleRight,
                UiChrome.Accent, Col2PadX + Col2ContentWidth - StatSumWidth, 0f,
                StatSumWidth, SectionLabelHeight, CharacterStatReadout.NoValue);
            _statSumValue.raycastTarget = false;

            var cardsGo = new GameObject("StatCards", typeof(RectTransform));
            cardsGo.transform.SetParent(block, false);
            var cards = cardsGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(cards, 0f, -(SectionLabelHeight + Col2LabelGap),
                Col2ViewportWidth, StatCardsHeight);
            for (int i = 0; i < _statCards.Length; i++) _statCards[i] = BuildStatCard(cards, i);
        }

        /// <summary>스탯 카드 한 장(폭 256 · 높이 74). 내부 좌표 검산은
        /// <c>11 + 18 + 8 + 5 + 8 + 13 + 11 = 74</c>(§4-3-2)이고, 그 등식을
        /// <c>CharacterStatCardLayoutTests</c>가 상수에서 직접 다시 센다.</summary>
        private StatCardView BuildStatCard(RectTransform parent, int index)
        {
            Image surface = UiChrome.AddSurface(parent, "StatCard" + index,
                UiChrome.CardSurfaceMuted, UiChrome.RadiusCard);
            RectTransform rt = surface.rectTransform;
            UiChrome.PlaceTopLeft(rt, Col2PadX, -index * (StatCardHeight + StatCardGap),
                Col2ContentWidth, StatCardHeight);
            surface.raycastTarget = false;
            UiChrome.AddOutline(rt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurfaceMuted), UiChrome.RadiusCard);

            // ---- 1행 ----
            float headY = -StatCardPadY;
            float bonusX = StatCardPadX + StatCardContentWidth - StatBonusWidth;
            float totalX = bonusX - StatValueGap - StatTotalWidth;
            float sourceX = StatCardPadX + StatNameWidth + StatNameGap;

            Text name = Label(rt, "Name", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                StatCardPadX, headY, StatNameWidth, StatHeadHeight, "—");
            Text source = Label(rt, "Source", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.TextTertiary, sourceX, headY,
                Mathf.Max(0f, totalX - sourceX - StatNameGap), StatHeadHeight, "");
            Text total = Label(rt, "Total", UiChrome.FontTitle, TextAnchor.MiddleRight, UiChrome.TextPrimary,
                totalX, headY, StatTotalWidth, StatHeadHeight, "—", bold: true);
            // ★ L-4 — 인계본의 초록 #8FBF6A는 우리 팔레트에 없다. 설계가 "임시로 Accent"로 못박았다.
            Text bonus = Label(rt, "Bonus", UiChrome.FontLabel, TextAnchor.MiddleRight, UiChrome.Accent,
                bonusX, headY, StatBonusWidth, StatHeadHeight, "");

            // ---- 2행: 총합 게이지 + 임계 눈금 ----
            float gaugeY = headY - StatHeadHeight - StatRowGap;
            Image track = UiChrome.AddSurface(rt, "GaugeTrack",
                UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.CardSurfaceMuted), UiChrome.RadiusDot);
            UiChrome.PlaceTopLeft(track.rectTransform, StatCardPadX, gaugeY,
                StatCardContentWidth, StatGaugeHeight);
            track.raycastTarget = false;

            Image fill = UiChrome.AddSurface(track.rectTransform, "Fill", UiChrome.Accent, UiChrome.RadiusDot);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.raycastTarget = false;

            var view = new StatCardView
            {
                Rect = rt, Name = name, Source = source, Total = total, Bonus = bonus, GaugeFill = fillRect,
            };

            for (int t = 0; t < view.Ticks.Length; t++)
            {
                // 반지름은 게이지 트랙과 같은 RadiusDot — 1pt 폭에 큰 반지름을 주면 9-슬라이스가 뭉갠다
                // (기록 구분선이 이미 쓰는 관례와 같다).
                Image tick = UiChrome.AddSurface(track.rectTransform, "Tick" + t,
                    UiChrome.PanelSurface, UiChrome.RadiusDot);
                UiChrome.PlaceTopLeft(tick.rectTransform, 0f, 0f, DividerThickness, StatGaugeHeight);
                tick.raycastTarget = false;
                view.Ticks[t] = tick;
            }

            // ---- 3행: 단계 칩 + 「중급까지 N」 ----
            float footY = gaugeY - StatGaugeHeight - StatRowGap;
            float chipY = footY + (StatTierChipHeight - StatFootHeight) * 0.5f;

            Image chip = UiChrome.AddSurface(rt, "TierChip",
                UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurfaceMuted), UiChrome.RadiusBadge);
            view.ChipRect = chip.rectTransform;
            UiChrome.PlaceTopLeft(view.ChipRect, StatCardPadX, chipY, StatTierChipMaxWidth, StatTierChipHeight);
            chip.raycastTarget = false;
            view.ChipSurface = chip;

            view.ChipLabel = UiChrome.AddText(view.ChipRect, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(view.ChipLabel.rectTransform);
            view.ChipLabel.text = "—";

            view.Remain = Label(rt, "Remain", UiChrome.FontCaption, TextAnchor.MiddleRight,
                UiChrome.TextTertiary, StatCardPadX + StatCardContentWidth - StatRemainWidth, footY,
                StatRemainWidth, StatFootHeight, "—");

            return view;
        }

        /// <summary>
        /// 「테마 세트 / SET」 블록. 진행도 두 줄(§1-7 · §7-4)과 준비 중 문구를 둘 다 굽는다.
        /// <para>★ <b>대사 풀 전환 / 테마 이펙트 / 유휴 동작 세 줄은 그리지 않는다.</b> §6 실측에서
        /// 그 셋은 런타임이 0줄이고(<c>Aura</c>·<c>arrowTrail</c>·<c>명상</c> 전수 0건), 「격파 성공 시」는
        /// 삭제된 콘텐츠를 가리키는 죽은 참조라 문구 교체 자체가 미정이다(§C-11 · §9 U-11).
        /// 없는 기능을 네 줄로 약속하는 것은 유예 자동 해금이 폐기된 것과 <b>같은 형태의 결함</b>이다.</para>
        /// </summary>
        private void BuildSetBlock(RectTransform content)
        {
            RectTransform block = BuildCol2Block(content, Col2BlockSet, "Col2Set",
                SectionLabelHeight + Col2LabelGap + SetPanelHeight);

            BuildSectionLabel(block, "테마 세트", "SET", Col2PadX, 0f);
            float panelY = -(SectionLabelHeight + Col2LabelGap);

            Image setPanel = UiChrome.AddSurface(block, "SetPanel", UiChrome.CardSurfaceMuted, UiChrome.RadiusCard);
            _setPanelRect = setPanel.rectTransform;
            UiChrome.PlaceTopLeft(_setPanelRect, Col2PadX, panelY, Col2ContentWidth, SetPanelHeight);
            setPanel.raycastTarget = false;
            // 테두리는 Stretch라 패널이 3행만큼 자라면 <b>따라 자란다</b>(AddOutline 안의 Stretch).
            UiChrome.AddOutline(_setPanelRect, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurfaceMuted), UiChrome.RadiusCard);

            // ★ 위 두 벌(준비 중 문구 / 진행도 두 줄)은 <b>Stretch가 아니라 위쪽 2행 영역에 고정</b>한다.
            //   Stretch로 두면 3행이 켜져 패널이 76pt가 되는 순간 가운데 정렬된 준비 중 문구가
            //   3행 위로 내려앉아 <b>글자가 겹친다</b>. 패널이 56일 때는 두 방식이 픽셀까지 같다.
            var noticeGo = new GameObject("SetNotice", typeof(RectTransform));
            noticeGo.transform.SetParent(_setPanelRect, false);
            UiChrome.PlaceTopLeft(noticeGo.GetComponent<RectTransform>(), 0f, 0f,
                Col2ContentWidth, SetPanelHeight);
            _setNoticeRoot = noticeGo;

            Text setNotice = UiChrome.AddText(noticeGo.transform, "Text", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.InkMeta, wrap: true);
            UiChrome.Stretch(setNotice.rectTransform, UiChrome.Space3);
            setNotice.text = SetPanelNotice;
            setNotice.raycastTarget = false;

            var readyGo = new GameObject("SetReady", typeof(RectTransform));
            readyGo.transform.SetParent(_setPanelRect, false);
            UiChrome.PlaceTopLeft(readyGo.GetComponent<RectTransform>(), 0f, 0f,
                Col2ContentWidth, SetPanelHeight);
            _setReadyRoot = readyGo;
            readyGo.SetActive(false);

            _setProgress = Label(readyGo.transform, "Progress", UiChrome.FontBody, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, StatCardPadX, -StatCardPadY, StatCardContentWidth,
                SetProgressHeight, CharacterStatReadout.NoValue);
            _setDetail = Label(readyGo.transform, "Detail", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.TextTertiary, StatCardPadX, SetDetailY, StatCardContentWidth,
                SetDetailHeight, CharacterStatReadout.SetIncomplete);

            BuildSetCostumeRow(_setPanelRect);
        }

        /// <summary>
        /// 세트 패널 3행 — 코스튬 진화 진행률(§7-2 · §7-4). <b>꺼진 채로 굽는다</b>:
        /// <see cref="CostumeResolver"/>가 <c>null</c>이면(= 지금 차림이 어떤 코스튬도 만들지 않으면)
        /// 이 행은 <b>존재하지 않아야 한다</b> — 자리만 비워 두면 20pt짜리 빈 구멍이 남고,
        /// 그 상태가 <b>기본값</b>이다(2026-09-07 실측: 실린 매니페스트는 <c>costume.office</c> 1개뿐이고
        /// 1일차 무료 4종은 <c>mil</c>이라, office 4부위를 갖추기 전에는 언제나 <c>null</c>이다).
        ///
        /// <para>★ <b>단계 이름을 그리지 않는다.</b> 「마스터」 말고는 사용자도 설계도 낱말을 주지 않았고
        /// (R26 §11 U-C5는 <c>design-narrative</c> 소관으로 열려 있다), 없는 낱말을 지어내면
        /// 화면이 규칙보다 먼저 말하게 된다. 그려도 되는 것으로 확정된 것은 <b>누적 시간과 퍼센트 둘</b>뿐이다.</para>
        /// </summary>
        private void BuildSetCostumeRow(RectTransform setPanel)
        {
            var costumeGo = new GameObject("SetCostume", typeof(RectTransform));
            costumeGo.transform.SetParent(setPanel, false);
            UiChrome.PlaceTopLeft(costumeGo.GetComponent<RectTransform>(), 0f, SetCostumeY,
                Col2ContentWidth, SetCostumeHeight);
            _setCostumeRoot = costumeGo;
            costumeGo.SetActive(false);

            _setCostumeTime = Label(costumeGo.transform, "Time", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary, StatCardPadX, 0f,
                SetCostumeTimeWidth, SetCostumeHeight, CharacterStatReadout.NoValue);

            // 퍼센트만 황동이다 — 이 줄에서 <b>자라는 값</b>은 그것 하나이고, 스탯 카드가
            // 「보너스」에 같은 색을 쓰는 것과 같은 자리다. 색 체계 최종 판단은 design-art.
            _setCostumePercent = Label(costumeGo.transform, "Percent", UiChrome.FontCaption,
                TextAnchor.MiddleRight, UiChrome.Accent,
                StatCardPadX + StatCardContentWidth - SetCostumePercentWidth, 0f,
                SetCostumePercentWidth, SetCostumeHeight, string.Empty);
        }

        // ==================== 배치 ====================

        /// <summary>
        /// ★ 컬럼 2 세로 좌표의 <b>유일한</b> 계산기. 블록 4개를 위에서 아래로 쌓고 스크롤 콘텐츠
        /// 높이를 정한다.
        /// <para>블록 하나의 높이만 바뀌어도 <b>아래 블록의 y가 전부</b> 움직인다 — 네 곳에 좌표를
        /// 굳혀 두면 그중 하나만 따라오지 않는 날이 온다(이 저장소가 폭 1042에서 실제로 당한 형태:
        /// 헤더는 갔는데 카드줄은 안 갔다). <see cref="LayoutCardGrid"/>와 같은 이유·같은 형태다.</para>
        /// </summary>
        private void LayoutColumn2()
        {
            if (_col2Content == null) return;

            float y = -ColPadY;
            for (int i = 0; i < _col2Blocks.Length; i++)
            {
                Col2Block block = _col2Blocks[i];
                if (block == null || block.Rect == null) continue;
                UiChrome.PlaceTopLeft(block.Rect, 0f, y, Col2ViewportWidth, block.Height);
                y -= block.Height + Col2BlockGap;
            }

            // ★ 잉크가 끝나는 자리 — 마지막 블록의 <b>아래 끝</b>이다(마지막 블록 뒤 간격은 뺀다).
            //   아래 여백은 아직 더하지 않았다. 이 구분이 아래 규칙의 전부다.
            float ink = -y - Col2BlockGap;
            float viewport = _col2Viewport != null ? _col2Viewport.rect.height : _bodyHeight;

            // ★★ 규칙 — <b>아래 여백은 「뷰포트가 남겨 준 만큼만」 준다</b>(ux-designer §14-5).
            //
            //   잉크가 뷰포트 안에 다 들어가는데도 «잉크 + 여백 20»으로 콘텐츠를 잡으면,
            //   여백 때문에 스크롤이 생긴다. 그러면 밀 수는 있는데 <b>새로 드러나는 것이 하나도 없는</b>
            //   가짜 어포던스가 된다 — 코스튬 3행이 켜졌을 때가 정확히 그 상태였다(잉크 720 ≤ 뷰포트 736
            //   인데 최대 스크롤 4). 게다가 그 4pt를 밀면 <c>SuppressedByGridDrag()</c>가 0.20초 참이 되어
            //   <b>컬럼 3 카드 버튼 클릭 1건이 삼켜진다</b>(§14-3의 대가 「가」·「나」).
            //
            //   잉크가 <b>실제로</b> 넘칠 때(블록이 늘거나 화면이 낮아 뷰포트가 줄었을 때)는 예전과 똑같이
            //   아래 여백 <see cref="ColPadY"/>를 붙여서 스크롤한다 — 스크롤 끝에서 마지막 블록이
            //   바닥에 딱 붙지 않게.
            float used = ink <= viewport ? viewport : ink + ColPadY;
            _col2Content.sizeDelta = new Vector2(Col2ViewportWidth, used);

            float max = MaxCol2Scroll();
            Vector2 p = _col2Content.anchoredPosition;
            float clamped = Mathf.Clamp(p.y, 0f, max);
            if (!Mathf.Approximately(p.y, clamped))
            {
                p.y = clamped;
                _col2Content.anchoredPosition = p;
            }
        }

        /// <summary>컬럼 2 content가 밀려날 수 있는 최대치(양수). 내용이 뷰포트에 들어가면 0이다 —
        /// 그때 이 창은 스크롤이 붙기 전과 <b>같은 화면</b>이다.</summary>
        private float MaxCol2Scroll()
        {
            if (_col2Content == null || _col2Viewport == null) return 0f;
            return Mathf.Max(0f, _col2Content.rect.height - _col2Viewport.rect.height);
        }

        // ==================== 전역 폴링 드래그 (컬럼 3 격자와 같은 형태) ====================

        private void ArmCol2Drag(Vector2 cursor)
        {
            _col2Grabbed = false;
            _col2Moved = false;
            if (Def(_tab).Page != TabPage.Cards) return;   // 컬럼 2는 카드 탭에만 있다.
            if (!_showCol2) return;                        // 좁은 창에서는 접혀 있다.
            if (_col2Content == null || _col2Viewport == null) return;
            if (MaxCol2Scroll() <= 0f) return;             // 넘치지 않으면 잡을 것도 없다.
            if (!ContainsScreenPoint(_col2Viewport, cursor)) return;

            _col2Grabbed = true;
            _col2GrabScreenY = cursor.y;
            _col2StartContentY = _col2Content.anchoredPosition.y;
        }

        private void DragCol2To(Vector2 cursor)
        {
            if (!_col2Grabbed || _col2Content == null) return;

            // 부호 유도는 DragGridTo와 같다: content 피벗이 (0,1)이라 y가 커질수록 아래가 드러나고,
            // 직접 조작이므로 커서를 위로 끌면 콘텐츠도 위로 간다.
            float delta = (cursor.y - _col2GrabScreenY) / CanvasScale();
            if (!_col2Moved && Mathf.Abs(delta) < GridDragThresholdPoints) return;
            _col2Moved = true;
            // 격자와 <b>같은 시계</b>를 쓴다 — 민 직후의 뗌이 이름 편집/잉크 스와치 클릭으로
            // 오인되면 안 되고, 그 억제는 SuppressedByGridDrag() 한 곳이 한다.
            _lastGridMoveTime = Time.unscaledTime;

            Vector2 p = _col2Content.anchoredPosition;
            p.y = Mathf.Clamp(_col2StartContentY + delta, 0f, MaxCol2Scroll());
            _col2Content.anchoredPosition = p;
        }

        private void EndCol2Drag()
        {
            _col2Grabbed = false;
            _col2Moved = false;
        }

        // ==================== 갱신 ====================

        /// <summary>
        /// 컬럼 2의 스탯/세트 값을 다시 쓴다. <see cref="RefreshNumbers"/>(0.25초 주기)에서 불린다.
        /// <para><b>입력이 그대로면 아무것도 하지 않는다</b> — 문자열을 만들지도, 폭을 재지도 않는다.
        /// 상주 앱이라 4Hz × 카드 4장 × 문자열 4개를 무조건 만들면 그게 곧 쓰레기다.</para>
        /// </summary>
        private void RefreshStatColumn()
        {
            // ★ 계산은 여기서 하지 않는다 — Core가 「정보창 스탯 카드가 부르는 유일한 입구」라고
            //   못박아 둔 함수 하나를 부른다(EquipmentStatRules.CurrentBuild).
            StatBuild build = EquipmentStatRules.CurrentBuild();

            if (_statSumValue != null)
            {
                string sumText = CharacterStatReadout.FormatEquipmentSum(
                    CharacterStatReadout.EquipmentSum(build.EquipmentBonus));
                if (_statSumValue.text != sumText) _statSumValue.text = sumText;
            }

            for (int i = 0; i < _statCards.Length; i++) RefreshStatCard(i, build);
            RefreshSetPanel(build);
        }

        private void RefreshStatCard(int index, in StatBuild build)
        {
            StatCardView card = _statCards[index];
            if (card == null || index >= EquipmentStatRules.StatCount) return;

            var stat = (CharacterStat)index;
            int total = build.Total.Of(stat);
            int bonus = build.EquipmentBonus.Of(stat);
            int tier = build.TierReached(stat);
            int reached = CurrencyModel.StatTierReached(index);

            if (card.HasShown && card.ShownTotal == total && card.ShownBonus == bonus
                && card.ShownTier == tier && card.ShownReachedTier == reached)
            {
                return;   // 바뀐 것이 없다 — 문자열도 폭 측정도 하지 않는다.
            }

            card.HasShown = true;
            card.ShownTotal = total;
            card.ShownBonus = bonus;
            card.ShownTier = tier;
            card.ShownReachedTier = reached;

            card.Name.text = EquipmentStatRules.StatName(stat);
            // 「모자 주스탯」 — 이 스탯이 어느 슬롯에서 오는가(§1-1). 슬롯 이름의 유일한 출처는
            // EquipmentModel이고, 슬롯↔스탯 대응의 유일한 출처는 EquipmentStatRules다.
            card.Source.text = $"{EquipmentModel.SlotName(EquipmentStatRules.StatSlotAt(index))} 주스탯";
            card.Total.text = CharacterStatReadout.FormatTotal(total);
            card.Bonus.text = CharacterStatReadout.FormatBonus(bonus);

            SetBarFill(card.GaugeFill, build.Progress01(stat));

            for (int t = 0; t < card.Ticks.Length; t++)
            {
                Image tick = card.Ticks[t];
                if (tick == null) continue;

                int tierIndex = t + 1;   // 눈금도 단계와 같은 1-기준이다.
                bool hasTick = tierIndex <= EquipmentStatRules.TierCount;
                if (tick.gameObject.activeSelf != hasTick) tick.gameObject.SetActive(hasTick);
                if (!hasTick) continue;

                RectTransform tickRect = tick.rectTransform;
                float fraction = Mathf.Clamp01(EquipmentStatRules.TierMark01(tierIndex));
                tickRect.anchorMin = tickRect.anchorMax = new Vector2(fraction, 0f);
                tickRect.pivot = new Vector2(0.5f, 0f);
                tickRect.anchoredPosition = Vector2.zero;
                tickRect.sizeDelta = new Vector2(DividerThickness, StatGaugeHeight);

                // H-8 — 한 번 넘긴 눈금만 황동. 판정은 CharacterStatReadout 한 곳에 있다.
                tick.color = CharacterStatReadout.IsTickLit(tierIndex, tier, reached)
                    ? UiChrome.Accent : UiChrome.PanelSurface;
            }

            string tierName = EquipmentStatRules.TierName(tier);
            card.ChipLabel.text = tierName;
            card.ChipSurface.color = tier > 0
                ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurfaceMuted)
                : UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.CardSurfaceMuted);
            card.ChipLabel.color = tier > 0 ? UiChrome.Accent : UiChrome.TextTertiary;

            // 칩 폭은 글자에서 <b>실제로 재서</b> 정한다(곱셈 상수 금지 — 영어화에서도 안 깨진다).
            float chipWidth = Mathf.Min(
                SettingsControls.MeasuredWidth(card.ChipLabel, tierName) + StatTierChipPadX * 2f,
                StatTierChipMaxWidth);
            card.ChipRect.sizeDelta = new Vector2(Mathf.Max(0f, chipWidth), StatTierChipHeight);

            card.Remain.text = CharacterStatReadout.FormatRemaining(total);
        }

        /// <summary>
        /// 세트 패널.
        ///
        /// <para>★ <b>완성 여부와 완성된 테마 이름은 계산하지 않는다</b> —
        /// <see cref="StatBuild.SetComplete"/> · <see cref="StatBuild.SetTheme"/>를 그대로 쓴다
        /// (<c>coder-systems</c> 인계: *"두 곳에서 같은 걸 계산하면 언젠가 갈라진다"*).
        /// 화면이 따로 내는 것은 <b>미완성일 때 몇 칸까지 왔는가</b>뿐이고, 그 숫자는 Core에 없다
        /// (§1-4의 「최빈값 개수」는 완성 판정에 필요 없어서 Core가 세지 않는다).</para>
        ///
        /// <para>그리고 그 둘이 <b>어긋나면 조용히 넘기지 않는다</b> — 아래 경고가 그 자리다.
        /// 「진행도가 4/4인데 Core는 미완성」은 규칙이 갈라졌다는 뜻이고, 그 상태로 「+8」을 그리면
        /// 화면이 붙지도 않은 보너스를 약속한다.</para>
        ///
        /// <para>스탯 4슬롯에 테마가 붙은 것이 <b>하나도 없을 때만</b> 준비 중 문구가 뜬다(아무것도
        /// 안 걸쳤거나 외형·팩만 걸쳤다) — <b>「0/4」를 그리지 않는다.</b> 아무것도 걸치지 않은
        /// 사람에게 진행 막대를 보여주는 것은 있지도 않은 목표를 만드는 일이다.
        /// <br/>★ 2026-09-06 정정: 여기 있던 *"테마가 하나도 배정되지 않은 오늘은"*은 R21 테마
        /// 배정(24종)으로 <b>거짓이 됐다</b>. 1일차 무료 4종이 전부 <c>mil</c>이라 그 넷을 걸치면
        /// 첫날부터 <b>4/4</b>가 뜬다(E2 — *"세트 완성의 첫 경험은 돈이 0원이다"*).</para>
        ///
        /// <para>★ 그리고 화면에 나가는 것은 <b>키가 아니라 이름</b>이다 —
        /// <see cref="ItemCatalog.ThemeDisplayName"/>. 키(<c>mil</c>)는 세트 판정용 안정 식별자라
        /// 한글 UI에 영문 소문자로 박히면 안 되고, 그 표는 <b>Core 한 곳</b>에 있다(화면이 자기 표를
        /// 들면 팩이 테마를 실어 오는 날 한쪽만 늘어난다).</para>
        /// </summary>
        private void RefreshSetPanel(in StatBuild build)
        {
            bool hasProgress = CharacterStatReadout.TryGetThemeProgress(
                ThemeOf(EquipmentSlot.Head), ThemeOf(EquipmentSlot.Eyes),
                ThemeOf(EquipmentSlot.Neck), ThemeOf(EquipmentSlot.Shoulders),
                out string themeName, out int matched, out int required);

            // ★ <b>조기 반환보다 앞</b>이다. 팩 코호트 아이템은 테마가 구조적으로 비어 있어
            //   hasProgress가 false이고(ItemCatalog.ThemeOfItem), 여기서 함께 반환하면
            //   <b>유료 팩 코스튬에서만 진행률이 통째로 사라진다</b>.
            // ★★ 그리고 준비 중 문구보다도 <b>앞</b>이다 — 문구의 조건이 이 결과에 달려 있다(바로 아래).
            bool hasCostumeRow = RefreshSetCostumeRow();

            // ★★ 2026-09-08 (ux-designer §14-8 확정) — <b>준비 중 문구는 «패널에 할 말이 하나도
            //   없을 때»만 뜬다.</b> 예전 조건은 <c>!hasProgress</c> 하나뿐이라, 팩 코호트 코스튬을
            //   입은 사람(테마가 구조적으로 비어 hasProgress = false)에게 <b>「아직 하나도 걸치지
            //   않았습니다」와 3행 진행률이 같은 패널에 동시에</b> 떴다.
            //
            //   ★ 이건 <b>문안 문제가 아니다</b>. 그 문장의 임무는 「패널이 지금 할 말이 없다」이고,
            //     3행이 있으면 그 전제 자체가 거짓이 된다 — <b>어떤 문장을 넣어도 안 고쳐진다.</b>
            //     그래서 문안을 바꾸지 않고 <b>표시 조건</b>을 고쳤다(신규 문자열 0개 · 신규 Text 0개).
            //
            //   ★ 그 상태(§14-8 표의 D)는 <b>오늘 도달 불가능하다</b> — 실린 팩 매니페스트가 0개다.
            //     그러나 사용자 확정 *"출시 이후부터 계속 추가팩 만들거야"*로 <b>첫 팩과 함께 반드시
            //     열린다</b>. 그때 이 자리에 무엇을 쓸지(§14-8 (나)의 1행 문안)는 design-narrative
            //     소관이라 <b>여기서 지어내지 않고 리더에게 올렸다</b>.
            bool notice = !hasProgress && !hasCostumeRow;
            if (_setNoticeRoot != null && _setNoticeRoot.activeSelf != notice)
            {
                _setNoticeRoot.SetActive(notice);
            }
            if (_setReadyRoot != null && _setReadyRoot.activeSelf != hasProgress)
            {
                _setReadyRoot.SetActive(hasProgress);
            }

            if (!hasProgress) return;

            // ★ 완성 여부의 유일한 출처는 Core다. 진행도는 표시용 보조일 뿐이다.
            bool complete = build.SetComplete;
            if (complete && !string.IsNullOrEmpty(build.SetTheme)) themeName = build.SetTheme;
            if (complete != (matched >= required))
            {
                Debug.LogError($"[정보창] 세트 판정이 갈라졌습니다 — Core={complete} / 진행도={matched}/{required}. " +
                    "EquipmentStatRules.IsSetComplete와 CharacterStatReadout.TryGetThemeProgress가 " +
                    "같은 규칙(빈 테마 제외 · 문자열 동등성)을 보고 있는지 확인하세요.");
            }

            // ★ 화면에는 키가 아니라 <b>사람이 읽을 이름</b>이 뜬다("mil 3/4" → "밀리터리 3/4").
            //   변환 규칙 한 곳은 CharacterStatReadout.ThemeLabel이고 그 안에서 Core의 표
            //   (ItemCatalog.ThemeDisplayName)를 본다 — 표는 Core, 표시 규칙은 표시 계층이다.
            string progress = CharacterStatReadout.FormatThemeProgress(
                CharacterStatReadout.ThemeLabel(themeName), matched, required);
            if (_setProgress != null && _setProgress.text != progress) _setProgress.text = progress;

            string detail = complete
                ? CharacterStatReadout.FormatSetBonus()
                : CharacterStatReadout.SetIncomplete;
            if (_setDetail != null && _setDetail.text != detail)
            {
                _setDetail.text = detail;
                _setDetail.color = complete ? UiChrome.Accent : UiChrome.TextTertiary;
            }
        }

        // ==================== 세트 패널 3행 — 코스튬 진화 진행률 ====================

        /// <summary>
        /// 3행을 다시 쓴다. <b>지금 입고 있는 코스튬</b>의 누적 시간과 다음 경계까지의 퍼센트다.
        ///
        /// ============================================================================
        /// 무엇을 보여주는가 — 「지금 입은 것」이다
        /// ============================================================================
        /// 이 행이 앉은 곳은 「테마 세트」 블록이고, 그 블록의 위 두 줄은 <b>지금 차림</b>을 말한다.
        /// 같은 상자 안에서 3행만 다른 코스튬의 이력을 말하면 <b>이름 없이 남의 숫자</b>가 된다
        /// (코스튬 이름은 <see cref="CostumeDescriptor.DisplayNameKey"/> 즉 로컬라이즈 «키»뿐이라
        /// 오늘 화면에 낼 낱말이 없다).
        /// <para>★ R26 §7-4가 <i>"여러 세트가 쌓여 있으면 가장 앞선 세트 1개를 권고한다"</i>라고
        /// 적었고 그 절이 곧바로 <i>"최종 판단은 <c>ux-designer</c>"</i>라고 넘겼다. 그 권고를 따르면
        /// 위 문단의 «이름 없는 남의 숫자» 문제가 생기므로 <b>지금 입은 것</b>으로 구현했고,
        /// <b>리더에게 보고했다</b>.</para>
        /// <para>★★ <b>2026-09-08 — <c>ux-designer</c>가 §14-7에서 그 권고를 기각하고 「지금 입은
        /// 코스튬」을 확정했다.</b> 그쪽이 낸 근거가 더 강하다: <see cref="CostumeResolver"/>의
        /// 기본 코호트 갈래는 <b>네 자리 테마가 전부 같고 비어 있지 않을 것</b>을 요구하므로,
        /// <b>3행이 뜨는 모든 경우에 그 숫자의 임자 이름이 바로 위 1행에 이미 인쇄돼 있다</b>
        /// (「오피스 워커 4/4」). ⇒ 3행은 이름 칸이 <b>필요 없고</b>, 그래서 폭 234를 시간·퍼센트
        /// 둘이 다 쓸 수 있다. 「가장 앞선 세트」로 가면 그 이름을 3행이 스스로 인쇄해야 하고,
        /// 팩 코스튬은 오늘 이름이 <b>로컬라이즈 키뿐</b>이라 «있다가 없다가 하는 줄»이 된다.
        /// <b>재검토 시점: 두 번째 코스튬 매니페스트가 실리는 날</b>(그때 열 질문은 「3행에 무엇을
        /// 넣나」가 아니라 「코스튬 목록 표면을 어디에 만드나」다).</para>
        ///
        /// ============================================================================
        /// 조회 빈도 — 0.25초다(매 프레임이 아니다)
        /// ============================================================================
        /// <see cref="CostumeResolver.Resolve"/>가 <i>"매 프레임 부르는 용도가 아니다"</i>라고 못박아
        /// 뒀다. 여기는 <see cref="RefreshNumbers"/> 경로(0.25초)이고 <b>정보창이 열려 있는 동안만</b>
        /// 돈다. 그리고 그 함수는 스탯 4슬롯 중 <b>첫 칸이 비면 즉시 반환</b>하므로 아무것도 안 걸친
        /// 사람에게는 카탈로그도 엔타이틀먼트도 건드리지 않는다.
        /// <para>★ <b>미해결로 남긴 것 1건(보고함)</b>: 엔타이틀먼트가 세션 도중 바뀌어도
        /// (팩 구매) 이 줄은 다음 갱신에 <b>따라온다</b> — 캐시를 안 두었기 때문이다. 대신 실제
        /// 스토어 조회원이 붙는 날에는 0.25초 조회가 비싸질 수 있다. 그때 캐시를 넣을 자리는
        /// «착용 변경마다 1회»(계약서 C-2)이고, 오늘 넣으면 <b>구매 직후 화면이 안 바뀌는</b>
        /// 반대쪽 결함이 생긴다. 오늘 출처는 <c>NullPackEntitlementSource</c>라 조회가 사실상 공짜다.</para>
        /// </summary>
        /// <returns>3행이 <b>지금 떠 있는가</b>. 준비 중 문구의 표시 조건이 이 값에 달려 있다
        /// (§14-8 — 「패널에 할 말이 하나도 없을 때만 문구」). 호출자가 <c>_setCostumeRoot.activeSelf</c>를
        /// 다시 읽게 하지 않는 이유는, 그러면 «켜는 곳»과 «읽는 곳»이 갈라져 한쪽만 낡기 때문이다.</returns>
        private bool RefreshSetCostumeRow()
        {
            if (_setCostumeRoot == null) return false;

            // 「입은 채」의 키를 세션 층과 <b>같은 함수</b>에서 받는다 — 화면이 자기 판정을 새로
            // 짜면 «화면은 쌓인다는데 실제로는 안 쌓이는» 상태가 만들어진다.
            string key = CostumeResolver.ResolveKey();
            bool show = !string.IsNullOrEmpty(key);

            SetCostumeRowVisible(show);
            if (!show) return false;

            int minutes = CostumeProgressModel.MinutesOf(key);
            if (_setCostumeHasShown && _setCostumeShownMinutes == minutes
                && string.Equals(_setCostumeShownKey, key, System.StringComparison.Ordinal))
            {
                return true;   // 바뀐 것이 없다 — 문자열을 만들지 않는다(행은 그대로 떠 있다).
            }

            _setCostumeHasShown = true;
            _setCostumeShownKey = key;
            _setCostumeShownMinutes = minutes;

            if (_setCostumeTime != null)
            {
                // 폭은 <b>실제로 재서</b> 줄인다(곱셈 상수 금지) — 「9999시간 59분」까지는 여유롭게
                // 들어가지만, 잘릴 때 오른쪽 퍼센트를 덮는 대신 말줄임표가 나야 한다.
                _setCostumeTime.text = UiChrome.Ellipsize(
                    _setCostumeTime, FormatCostumeFocusTime(minutes), SetCostumeTimeWidth);
            }

            if (_setCostumePercent != null) _setCostumePercent.text = FormatCostumePercent(minutes);
            return true;
        }

        /// <summary>
        /// 3행을 켜고 끄면서 <b>세트 패널과 블록 높이를 함께</b> 옮긴다.
        /// <para>★ 높이를 세 곳(패널 면 · 블록 · 컬럼 흐름)에 따로 굳히지 않는다 — 이 저장소가
        /// 폭 1042에서 당한 형태가 그것이다(헤더는 갔는데 카드줄은 안 갔다).
        /// <see cref="LayoutColumn2"/>가 세로 좌표의 유일한 계산기이므로 <b>블록 높이만 갈아 끼우고
        /// 그 함수를 다시 부른다</b>.</para>
        /// <para><b>상태가 바뀔 때만</b> 다시 배치한다 — 0.25초마다 <c>LayoutColumn2</c>를 부르면
        /// 스크롤 위치 클램프가 매번 돌아 사용자가 민 자리가 흔들린다.</para>
        /// </summary>
        private void SetCostumeRowVisible(bool show)
        {
            if (_setCostumeRoot == null || _setCostumeRoot.activeSelf == show) return;
            _setCostumeRoot.SetActive(show);

            if (!show)
            {
                // 다시 켜질 때 <b>반드시 다시 그리게</b> 한다 — 안 그러면 메모가 맞아떨어져
                // 옛 문자열이 그대로 뜬다.
                _setCostumeHasShown = false;
                _setCostumeShownKey = null;
            }

            float panelHeight = show ? SetPanelHeightWithCostume : SetPanelHeight;
            if (_setPanelRect != null) _setPanelRect.sizeDelta = new Vector2(Col2ContentWidth, panelHeight);

            Col2Block block = _col2Blocks[Col2BlockSet];
            if (block != null) block.Height = SectionLabelHeight + Col2LabelGap + panelHeight;

            LayoutColumn2();
        }

        /// <summary>
        /// 누적 분을 사람이 읽는 표기로("12시간 30분", 1시간 미만이면 "30분").
        /// <b><see cref="CharacterStatsModel.FormatCompanionTime"/>과 같은 규칙</b>이다(R26 §7-2:
        /// *"새 형식 0개, 재사용"*).
        /// <para>★ <b>그 함수를 부르지 못하는 이유</b>: 그쪽은 인자가 없고 자기 필드
        /// (<c>TotalCompanionSeconds</c>, 함께한 시간)를 그린다. 여기 값은 <b>다른 양</b>이라
        /// 넘길 자리가 없다. 그래서 규칙만 같게 다시 적고, <b>두 구현이 같은 답을 내는지</b>는
        /// <c>CostumeProgressReadoutTests</c>가 매 실행 대조한다 — 한쪽만 바뀌면 그 자리에서 빨개진다.
        /// (Core에 <c>FormatHourMinute(int)</c>를 두어 한 벌로 만드는 것이 옳지만 그 파일은
        /// 이 라운드의 소유가 아니라 <b>리더에게 보고했다</b>.)</para>
        /// </summary>
        internal static string FormatCostumeFocusTime(int focusMinutes)
        {
            if (focusMinutes < 0) focusMinutes = 0;
            int hours = focusMinutes / 60;
            int minutes = focusMinutes % 60;
            if (hours <= 0) return $"{minutes}분";
            return $"{hours}시간 {minutes}분";
        }

        /// <summary>
        /// 다음 경계까지의 퍼센트("37.4%"). <b>마스터면 빈 문자열</b>이다 — 그릴 값이 없다는 사실이
        /// <see cref="CostumeEvolutionRules.NoNextBoundary"/>로 온다(§7-2: 마스터는 누적 시간만 남는다).
        ///
        /// <para>★ <b>여기서 반올림하지 않는다.</b> 계산도 내림도 Core가 정수 나눗셈으로 끝냈고
        /// (<see cref="CostumeEvolutionRules.PercentTenthsToNextBoundary"/>), 이 함수는 천분율을
        /// <b>자릿수로 쪼개기만</b> 한다. <c>float</c>로 되돌려 <c>F1</c>로 찍으면 그 순간
        /// 반올림 방향이 두 곳이 되고, 어느 쪽이 이겼는지 화면만 봐서는 못 찾는다.</para>
        ///
        /// <para>★ 범위 밖 값은 <b>조용히 흘리지 않는다</b>. Core가 마스터가 아닌 동안
        /// <c>[0, 999]</c>를 구조적으로 보장하므로(그 함수의 클램프), 그 밖의 값이 오면 그것은
        /// <b>정상 상태가 아니라 계약 파기</b>다 — 그때만 신고한다. 마스터(-1)와 정상 범위는
        /// 여기서 <b>각각 이름 붙여</b> 가르므로 정상 사용자에게 경보가 뜨는 경로가 없다.</para>
        /// </summary>
        internal static string FormatCostumePercent(int focusMinutes)
        {
            int tenths = CostumeEvolutionRules.PercentTenthsToNextBoundary(focusMinutes);
            if (tenths == CostumeEvolutionRules.NoNextBoundary) return string.Empty;   // 마스터 — 정상

            if (tenths < 0 || tenths >= CostumePercentTenthsScale)
            {
                Debug.LogError($"[정보창] 코스튬 진행률이 그릴 수 없는 값입니다 — 누적 {focusMinutes}분에서 " +
                    $"천분율 {tenths}. CostumeEvolutionRules.PercentTenthsToNextBoundary는 마스터가 " +
                    "아닌 동안 [0, 999]를 보장해야 합니다(그 함수의 클램프). 100.0% 화면은 이 설계에 " +
                    "존재하지 않습니다 — 100.0% 표시와 마스터 도달은 동치입니다.");
                return string.Empty;
            }

            return $"{tenths / CostumePercentTenthsPerPoint}.{tenths % CostumePercentTenthsPerPoint}%";
        }

        /// <summary>이 슬롯에 지금 걸친 것의 테마 키(미착용/미배정이면 <c>null</c>).
        /// <b>카탈로그를 화면이 직접 뒤지는 유일한 자리</b>이고, 그것을
        /// <see cref="EquipmentStatRules.SlotLoadout"/>에 맡긴다 — 착용 여부·등급·부스탯·테마의
        /// 이음매가 Core에 한 벌만 있게.</summary>
        private static string ThemeOf(EquipmentSlot slot) => EquipmentStatRules.SlotLoadout(slot).Theme;
    }
}
