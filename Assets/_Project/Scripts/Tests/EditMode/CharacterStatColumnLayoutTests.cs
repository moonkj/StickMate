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

        private static float SetBlockHeight =>
            CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
            + CharacterInfoWindow.SetPanelHeight;

        private const int BlockCount = 4;

        private static float DesignContentHeight =>
            CharacterInfoWindow.ColPadY
            + StatusBlockHeight + RecordBlockHeight + DisplayBlockHeight + SetBlockHeight
            + (BlockCount - 1) * CharacterInfoWindow.Col2BlockGap
            + CharacterInfoWindow.ColPadY;

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
        /// ★ 설계 크기(1042 × 802)에서 컬럼 2가 <b>본문 높이 안에 들어간다</b>.
        /// <para>넘치면 스크롤이 받아 주지만(그래서 도달성은 안 죽는다), 스크롤바가 없는 컬럼이라
        /// 아래 블록의 <b>발견 가능성</b>이 떨어진다. 설계 크기에서는 안 넘치는 것이 계약이다.</para>
        /// </summary>
        [Test]
        public void 설계_크기에서_컬럼2가_본문_높이_안에_들어간다()
        {
            float body = CharacterInfoWindow.BodyHeight;
            float used = DesignContentHeight;

            Assert.LessOrEqual(used, body,
                $"컬럼 2 세로 예산 초과 — 사용 {used} vs 본문 {body} (초과 {used - body}pt).\n" +
                $"  능력치 {StatusBlockHeight} / 기록 {RecordBlockHeight} / 표시 {DisplayBlockHeight} / " +
                $"세트 {SetBlockHeight} / 블록 간격 {CharacterInfoWindow.Col2BlockGap} × {BlockCount - 1} / " +
                $"위아래 여백 {CharacterInfoWindow.ColPadY} × 2\n" +
                "  스크롤이 도달성은 지켜 주지만, 스크롤바가 없는 컬럼이라 아래 블록이 잘 안 보입니다. " +
                "리더에게 보고하고 무엇을 줄일지 정하십시오(coder-ui 규약: 억지로 넣지 말 것).");

            // 양성 대조 — 예산이 실제로 빡빡한지도 함께 적는다("항상 참"인 단언이 아니다).
            Assert.Greater(used, body * 0.9f,
                $"컬럼 2가 본문의 90% 미만만 쓰고 있습니다({used} / {body}) — 이 테스트가 " +
                "재고 있는 대상이 사라졌거나 블록 하나가 통째로 빠졌습니다.");
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
