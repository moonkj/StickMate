using NUnit.Framework;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 컬럼 2 「능력치」가 들어오면서 <b>세로 예산이 다시 빡빡해졌다</b>. 그 예산을 상수에서
    /// 다시 세어 잠근다.
    ///
    /// ============================================================================
    /// 왜 이 테스트가 필요한가 — 이 저장소가 실제로 당한 형태
    /// ============================================================================
    /// *"폭·높이 상수를 바꾸면 파생값이 조각마다 흩어져 있다. 실제 사고: 폭 1042가 헤더에는 갔는데
    /// 카드줄에는 안 가 캐러셀 4건이 깨졌다"*(<c>.claude/agents/coder-ui.md</c>).
    /// 컬럼 2는 이제 <b>블록 네 개가 세로로 쌓인다</b> — 어느 한 상수를 키우면 맨 아래 세트 패널이
    /// <c>RectMask2D</c>에 잘려 <b>조용히</b> 사라진다. 잘린 화면은 정상 화면과 똑같이 생겼다.
    ///
    /// <para><b>여기서 다시 세는 것이 요점이다.</b> 프로덕션의 합계 함수를 부르면 그 함수가 틀어질 때
    /// 기대값도 함께 틀어져 아무것도 못 잰다(TEAM.md 「생성기와 검사기가 같이 틀린다」).
    /// 이 파일은 <b>1차 상수만</b> 참조해 합을 <b>직접</b> 만든다.</para>
    ///
    /// <para>상수는 <c>internal</c>이라 리플렉션도 소스 스크래핑도 없이 이름으로 참조한다
    /// (<c>Scripts/AssemblyInfo.cs</c>의 <c>InternalsVisibleTo</c>). 이름이 바뀌면
    /// <b>컴파일이 깨진다</b> — 조용히 대상을 잃는 소스 매처보다 낫다.</para>
    /// </summary>
    public sealed class CharacterStatColumnLayoutTests
    {
        // 블록 높이를 여기서 다시 만든다 — 프로덕션의 BuildCol2Block 인자와 같은 식이지만
        // <b>다른 곳에 적혀 있어야</b> 한쪽만 바뀔 때 잡힌다.
        private static float StatusBlockHeight =>
            CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
            + CharacterInfoWindow.StatCardsHeight;

        private static float RecordBlockHeight =>
            CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
            + CharacterInfoWindow.GaugeBlockHeight + CharacterInfoWindow.Col2LabelGap
            + CharacterInfoWindow.StatCount * CharacterInfoWindow.RecordRowHeight;

        private static float DisplayBlockHeight =>
            CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
            + CharacterInfoWindow.NameRowHeight;

        /// <summary>세트 블록 높이. <b>패널 높이를 인자로 받는다</b> — 2행판(56)과 3행판(76) 둘 다
        /// 재야 하기 때문이다. ★ 2026-09-08까지 이 테스트는 <b>2행판만</b> 재고 있었고, 그래서
        /// <b>코스튬 3행이 켜져도 초록이었다</b>(같은 예산을 재는 <c>CostumeProgressReadoutTests</c>는
        /// 3행판을 재고 있었다 — <b>두 파일이 서로 다른 상태를 재고 있었다</b>는 사실 자체가 갭이었다).
        /// 지금은 <b>최악 상태</b>로 통일한다.</summary>
        private static float SetBlockHeight(float setPanelHeight) =>
            CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
            + setPanelHeight;

        private const int BlockCount = 4;

        /// <summary>
        /// ★ <b>잉크 하단</b> — 마지막 블록의 아래 끝(위 여백 + 블록 넷 + 블록 사이 간격 셋).
        /// <b>아래 여백은 더하지 않는다.</b> 그 구분이 계약 C1/C2의 전부다:
        /// 아래 여백에는 <b>그려지는 것이 하나도 없어서</b>, 거기까지 세면 «아무도 못 보는 4pt»를
        /// 초과로 신고하게 된다(ux-designer §14-2 실측: 설계 크기에서 잘리는 픽셀은 <b>0</b>이다).
        /// </summary>
        private static float InkBottom(float setPanelHeight) =>
            CharacterInfoWindow.ColPadY
            + StatusBlockHeight + RecordBlockHeight + DisplayBlockHeight + SetBlockHeight(setPanelHeight)
            + (BlockCount - 1) * CharacterInfoWindow.Col2BlockGap;

        /// <summary>★ <b>최악 상태</b>의 세트 패널 높이 = 코스튬 3행이 켜진 판.
        /// 이 창의 세로 예산은 «오늘 대부분이 보는 화면»이 아니라 <b>가장 큰 화면</b>에서 성립해야 한다.
        /// (3행은 오늘 도달 가능하다 — <c>CostumeManifest_office</c>가 실려 있고 기본 코호트
        /// 코스튬은 엔타이틀먼트를 묻지 않는다.)</summary>
        private static float WorstSetPanelHeight => CharacterInfoWindow.SetPanelHeightWithCostume;

        // ====================================================================
        // 1. 스탯 카드 내부 — §4-3-2 검산 11 + 18 + 8 + 5 + 8 + 13 + 11 = 74
        // ====================================================================

        [Test]
        public void 스탯_카드_내부_세로가_카드_높이와_정확히_같다()
        {
            float used = CharacterInfoWindow.StatCardPadY
                         + CharacterInfoWindow.StatHeadHeight
                         + CharacterInfoWindow.StatRowGap
                         + CharacterInfoWindow.StatGaugeHeight
                         + CharacterInfoWindow.StatRowGap
                         + CharacterInfoWindow.StatFootHeight
                         + CharacterInfoWindow.StatCardPadY;

            Assert.AreEqual(CharacterInfoWindow.StatCardHeight, used, 0.001f,
                $"스탯 카드 세로 검산이 깨졌습니다 — 내부 합 {used} vs 카드 높이 " +
                $"{CharacterInfoWindow.StatCardHeight}. 넘치면 3행이 카드 밖으로 새고, 모자라면 " +
                "아래가 비어 보입니다(§4-3-2 내부 검산).");
        }

        /// <summary>단계 칩(3행 왼쪽)과 「중급까지 N」(3행 오른쪽)이 <b>겹치지 않는다</b>.</summary>
        [Test]
        public void 단계_칩과_잔여_칸이_카드_폭_안에서_겹치지_않는다()
        {
            Assert.Greater(CharacterInfoWindow.StatTierChipMaxWidth, 0f,
                "칩이 들어갈 폭이 남지 않았습니다 — 「중급까지 N」 칸이 카드를 다 먹었습니다.");

            float used = CharacterInfoWindow.StatTierChipMaxWidth
                         + CharacterInfoWindow.StatValueGap
                         + CharacterInfoWindow.StatRemainWidth;
            Assert.LessOrEqual(used, CharacterInfoWindow.StatCardContentWidth + 0.001f,
                $"3행이 카드 콘텐츠 폭을 넘습니다 — {used} vs {CharacterInfoWindow.StatCardContentWidth}.");
        }

        /// <summary>1행 오른쪽 두 칸(총합 · 보너스)이 이름/출처 칸을 침범하지 않는다.</summary>
        [Test]
        public void 스탯_카드_1행이_가로로_겹치지_않는다()
        {
            float rightBlock = CharacterInfoWindow.StatTotalWidth + CharacterInfoWindow.StatValueGap
                               + CharacterInfoWindow.StatBonusWidth;
            float leftBlock = CharacterInfoWindow.StatNameWidth + CharacterInfoWindow.StatNameGap;

            Assert.Less(leftBlock + rightBlock, CharacterInfoWindow.StatCardContentWidth,
                "1행 왼쪽(이름)과 오른쪽(총합·보너스)이 겹칩니다 — 「출처」 칸이 음수 폭이 됩니다.");
        }

        /// <summary>세트 패널 두 줄이 패널 높이 안에 들어간다.</summary>
        [Test]
        public void 세트_패널_두_줄이_패널_높이_안에_들어간다()
        {
            float used = CharacterInfoWindow.StatCardPadY + CharacterInfoWindow.SetProgressHeight
                         + CharacterInfoWindow.SetRowGap + CharacterInfoWindow.SetDetailHeight;
            Assert.LessOrEqual(used, CharacterInfoWindow.SetPanelHeight,
                $"세트 패널 내부가 넘칩니다 — {used} vs {CharacterInfoWindow.SetPanelHeight}.");
            Assert.GreaterOrEqual(used, CharacterInfoWindow.SetPanelHeight * 0.5f,
                "패널이 내용의 두 배 넘게 큽니다 — 예약만 하는 자리가 됩니다(P0-1이 고친 결함의 재범).");
        }

        // ====================================================================
        // 2. 컬럼 2 세로 예산 — 설계 크기에서 잘리지 않는가
        // ====================================================================

        /// <summary>
        /// ★★ <b>계약 C1 「잉크 불가침」</b> — 설계 크기(1042 × 802)에서 <b>그려지는 것은 한 픽셀도
        /// 잘리지 않는다</b>. 재는 상태는 <b>최악(코스튬 3행이 켜진 판)</b>이다.
        ///
        /// <para>★ 2026-09-08 — 이 테스트는 예전에 «위 여백 + 블록 + <b>아래 여백</b> ≤ 736»을 쟀고,
        /// 그 식은 3행판에서 4pt를 초과로 신고했다. 그런데 <b>그 4pt는 전부 아래 여백</b>이고
        /// 여백에는 그려지는 것이 없다 — 스크롤을 끝까지 밀어도 <b>새로 드러나는 것이 없다</b>
        /// (ux-designer §14-2). <b>계약이 말한 해악(「아래 블록이 잘 안 보인다」)과 계약이 재는 양이
        /// 갈라져 있었다.</b> 그래서 폐기하지 않고 <b>둘로 갈랐다</b> — 이 테스트가 C1, 아래가 C2.</para>
        ///
        /// <para>★ 그리고 <b>재는 상태를 최악으로 바꿨다.</b> 예전에는 <c>SetPanelHeight</c>(2행판)로
        /// 재고 있어서 3행이 켜져도 초록이었다.</para>
        /// </summary>
        [Test]
        public void 최악_상태_설계_크기에서_컬럼2_잉크가_한_픽셀도_잘리지_않는다()
        {
            float body = CharacterInfoWindow.BodyHeight;
            float ink = InkBottom(WorstSetPanelHeight);

            // 전제 — 최악 상태가 기본 상태보다 실제로 크다(아니면 이 테스트가 아무것도 안 잰다).
            Assert.Greater(WorstSetPanelHeight, CharacterInfoWindow.SetPanelHeight,
                "3행판 세트 패널이 2행판보다 크지 않습니다 — 이 측정의 «최악 상태»가 사라졌습니다.");

            Assert.LessOrEqual(ink, body,
                $"[C1 잉크 불가침] 컬럼 2의 잉크가 본문 밖으로 나갑니다 — 잉크 하단 {ink} vs 본문 {body} " +
                $"(초과 {ink - body}pt).\n" +
                $"  능력치 {StatusBlockHeight} / 기록 {RecordBlockHeight} / 표시 {DisplayBlockHeight} / " +
                $"세트 {SetBlockHeight(WorstSetPanelHeight)} / 블록 간격 {CharacterInfoWindow.Col2BlockGap} × {BlockCount - 1} / " +
                $"위 여백 {CharacterInfoWindow.ColPadY}\n" +
                "  ★ 이건 «여백이 줄었다»가 아니라 «글자가 잘린다»입니다. 스크롤이 도달성은 지켜 주지만 " +
                "스크롤바가 없는 컬럼이라 아래 블록을 찾지 못합니다.\n" +
                "  리더에게 보고하고 무엇을 줄일지 정하십시오(coder-ui 규약: 억지로 넣지 말 것).");

            // 양성 대조 — 예산이 실제로 빡빡한지도 함께 적는다("항상 참"인 단언이 아니다).
            Assert.Greater(ink, body * 0.9f,
                $"컬럼 2가 본문의 90% 미만만 쓰고 있습니다({ink} / {body}) — 이 테스트가 " +
                "재고 있는 대상이 사라졌거나 블록 하나가 통째로 빠졌습니다.");
        }

        /// <summary>
        /// ★★ <b>계약 C2 「아래 여백 잔량」</b> — 잉크 아래로 <b>최소 <see cref="UiChrome.Space4"/></b>가
        /// 보인다. <b>지금 값은 정확히 하한</b>이라(16 ≥ 16), 누가 세로를 <b>1pt만</b> 얹어도 여기서 빨개진다.
        ///
        /// <para>겉보기로는 C1+C2가 예전 계약보다 4pt를 내주는 것처럼 보인다(예전: 잉크 ≤ 716 /
        /// 지금: 잉크 ≤ 720). 그러나 예전 자리에 있던 것은 <c>Assert.Ignore</c>였고 <b>건너뜀은
        /// 회귀를 못 잡는다</b>. C2는 막는다 — <b>감시 강도는 올라갔다</b>(§14-5).</para>
        ///
        /// <para>★ 하한을 <c>16</c>이라고 <b>숫자로 적지 않는다</b> — 간격 토큰
        /// <see cref="UiChrome.Space4"/>를 참조한다(CLAUDE.md 하드코딩 금지).</para>
        /// </summary>
        [Test]
        public void 최악_상태에서도_잉크_아래로_한_칸_여백이_남는다()
        {
            float body = CharacterInfoWindow.BodyHeight;
            float slack = body - InkBottom(WorstSetPanelHeight);

            Assert.GreaterOrEqual(slack, UiChrome.Space4,
                $"[C2 아래 여백 잔량] 잉크 아래 여백이 {slack}pt로 한 칸({UiChrome.Space4})보다 좁습니다 — " +
                "마지막 블록이 본문 아래 끝에 붙어 «잘린 것처럼» 읽힙니다. " +
                "여백은 정의상 가장자리에서 양보되는 공간이지만, 한 칸은 남겨야 «끝났다»가 읽힙니다(§14-5).");

            // ★ 이 계약이 지금 <b>얼마나 빡빡한가</b>를 함께 기록한다 — 여유가 커지면(블록이 빠졌거나
            //   이 계산기가 대상을 잃었으면) 그것도 알아야 한다.
            Assert.Less(slack, CharacterInfoWindow.ColPadY * 2f,
                $"잉크 아래 여백이 {slack}pt나 남습니다 — 컬럼 2에서 블록 하나가 통째로 빠졌거나 " +
                "이 계산기가 다른 것을 세고 있습니다(양성 대조 실패).");
        }

        /// <summary>블록 사이 간격은 제목-내용 간격보다 <b>커야</b> 한다 — 안 그러면 제목이
        /// 자기 내용이 아니라 위 블록에 붙어 읽힌다(근접성).</summary>
        [Test]
        public void 블록_사이가_제목과_내용_사이보다_넓다()
        {
            Assert.Greater(CharacterInfoWindow.Col2BlockGap, CharacterInfoWindow.Col2LabelGap,
                "블록 간격이 제목-내용 간격 이하입니다 — 섹션 제목이 위 블록에 붙어 읽힙니다.");
        }

        /// <summary>스탯 카드 묶음 높이가 <b>카드 수 × 높이 + 간격</b>과 같다 —
        /// 카드 하나를 늘렸는데 묶음 높이가 안 따라오면 마지막 카드가 블록 밖으로 나간다.</summary>
        [Test]
        public void 스탯_카드_묶음_높이가_카드_수에서_유도된다()
        {
            int n = CharacterInfoWindow.StatAxisCount;
            Assert.Greater(n, 0, "스탯 축이 0개입니다.");

            float expected = n * CharacterInfoWindow.StatCardHeight
                             + (n - 1) * CharacterInfoWindow.StatCardGap;
            Assert.AreEqual(expected, CharacterInfoWindow.StatCardsHeight, 0.001f,
                "카드 묶음 높이가 카드 수와 어긋났습니다 — 마지막 카드가 블록 밖으로 나갑니다.");
        }

        /// <summary>눈금 개수가 임계 단계 수와 같다 — 모자라면 마지막 임계가 게이지에 안 그려지고,
        /// 남으면 게이지에 근거 없는 눈금이 생긴다.</summary>
        [Test]
        public void 눈금_개수가_임계_단계_수와_같다()
        {
            Assert.AreEqual(StickMate.Core.EquipmentStatRules.TierCount,
                CharacterInfoWindow.StatTickCount,
                "게이지 눈금 수와 임계 단계 수가 다릅니다.");
        }

        /// <summary>뷰포트 폭이 컬럼 폭에서 세로 구분선만큼만 줄었다 —
        /// 구분선이 콘텐츠에 덮이거나 콘텐츠가 구분선을 넘지 않는다.</summary>
        [Test]
        public void 컬럼2_뷰포트가_구분선을_침범하지_않는다()
        {
            Assert.Less(CharacterInfoWindow.Col2ViewportWidth, CharacterInfoWindow.Col2Width,
                "뷰포트가 컬럼 폭 이상입니다 — 세로 구분선을 덮습니다.");
            Assert.GreaterOrEqual(CharacterInfoWindow.Col2ViewportWidth,
                CharacterInfoWindow.Col2ContentWidth,
                "뷰포트가 콘텐츠 폭보다 좁습니다 — 오른쪽 값(장비 합 등)이 잘립니다.");
        }
    }
}
