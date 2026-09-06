using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// 인용 감사 #4 — <b>「그 등식을 상수에서 직접 다시 센다」던 그 기계</b>
    /// (test-engineer, 2026-09-06)
    /// ============================================================================
    /// <c>Interaction/CharacterInfoWindow.Stats.cs</c>의 <c>BuildStatCard</c> 문서 원문:
    ///
    /// <para><i>"스탯 카드 한 장(폭 256 · 높이 74). 내부 좌표 검산은
    /// <c>11 + 18 + 8 + 5 + 8 + 13 + 11 = 74</c>(§4-3-2)이고, 그 등식을
    /// <c>CharacterStatCardLayoutTests</c>가 상수에서 직접 다시 센다."</i></para>
    ///
    /// ============================================================================
    /// ★★ 먼저 정정 — 「그 검산을 지금 아무도 안 한다」는 <b>거짓이었다</b>
    /// ============================================================================
    /// 인용 감사가 이 이름을 «실체 없음»으로 등재하면서 <i>"즉 74 = 11+18+8+5+8+13+11 검산은
    /// 지금 아무도 안 한다"</i>라고 적었다. <b>실측 결과 그 문장은 틀렸다.</b>
    /// <see cref="CharacterStatColumnLayoutTests.스탯_카드_내부_세로가_카드_높이와_정확히_같다"/>가
    /// <b>바로 그 일곱 항의 합</b>을 1차 상수에서 직접 만들어 <c>StatCardHeight</c>와 비교하고 있고,
    /// 그 절의 제목도 <i>"§4-3-2 검산 11 + 18 + 8 + 5 + 8 + 13 + 11 = 74"</i>다.
    ///
    /// <para><b>깨져 있던 것은 검산이 아니라 인용이다</b> — 클래스 이름이
    /// <c>…CardLayoutTests</c>가 아니라 <c>…ColumnLayoutTests</c>다
    /// (<c>docs/UX_CHARACTER_WINDOW_REFINE.md §149</c>는 애초에 <b>둘 다</b> 이름으로 예고했다).</para>
    ///
    /// ============================================================================
    /// 그래서 이 파일은 무엇을 새로 잠그는가 — <b>합이 아니라 좌표다</b>
    /// ============================================================================
    /// 저쪽은 <b>합</b>을 본다: 「일곱 항의 합 = 74」. 이 파일은 <b>배치</b>를 본다:
    /// <c>BuildStatCard</c>가 실제로 계산하는 <c>headY → gaugeY → footY → chipY</c> 사슬을
    /// 같은 식으로 다시 만들어 <b>각 줄이 카드 안에 있는가 · 서로 겹치지 않는가</b>를 잰다.
    ///
    /// <para>★ <b>합이 맞아도 배치는 틀릴 수 있다.</b> 실제 사례가 이 카드 안에 있다:
    /// 단계 칩은 높이 <c>StatTierChipHeight</c>(18)로 <b>3행(13)보다 크고</b>, 그 차이의 절반만큼
    /// 위로 올려 세로 가운데를 맞춘다. 즉 <b>칩은 «일곱 항»에 들어 있지 않다</b> — 합 검산은
    /// 칩이 카드 밖으로 나가도 <b>초록</b>이다. 그 파일 주석이 <i>"패딩이 StatCardPadY 11이라
    /// 카드 밖으로는 나가지 않는다"</i>라고 <b>말로만</b> 적어 둔 것이 정확히 이 자리다.</para>
    ///
    /// <para>그리고 <b>같은 등식</b>을 여기서도 한 번 더 센다 — 인용문이 «그 등식을 이 이름이
    /// 다시 센다»라고 <b>이름으로</b> 약속했기 때문이다. 두 파일이 같은 사실을 재는 것은 낭비가
    /// 아니라 <b>인용을 참으로 만드는 최소 조건</b>이다(프로덕션 주석을 고칠 권한이 이 라운드에
    /// 없다 — 고칠 수 있다면 인용을 <c>ColumnLayoutTests</c>로 바로잡는 쪽이 더 싸다).</para>
    ///
    /// <para><b>기준</b>: 상수는 <c>internal</c>이라 이름으로 참조한다
    /// (<c>Scripts/AssemblyInfo.cs</c>의 <c>InternalsVisibleTo</c>). 이름이 바뀌면 <b>컴파일이
    /// 깨진다</b> — 조용히 대상을 잃는 소스 매처보다 낫다(저쪽 파일과 같은 판단이다).</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 상수만 읽는다.</para>
    /// </summary>
    public sealed class CharacterStatCardLayoutTests
    {
        private const string LogPrefix = "[스탯카드배치]";
        private const float Tolerance = 0.001f;

        // ====================================================================
        // 대조 표본 — <b>프로덕션 상수를 부르지 않는</b> 숫자들
        // ====================================================================
        //
        // ★ 왜 리터럴인가: 아래 양성/음성 대조는 «고장 난 배치를 잡는가»를 증명한다.
        //   그 표본을 프로덕션 상수로 만들면, 프로덕션이 바뀌는 날 <b>표본도 함께 바뀌어</b>
        //   무엇을 증명했는지 알 수 없게 된다(TEAM.md: 기대값을 프로덕션에서 만들지 마라).
        //
        // ★ 대신 리터럴은 <b>썩는다</b>. 그래서 <see cref="표본이_프로덕션_상수와_여전히_같다"/>가
        //   여섯 개를 하나씩 프로덕션과 맞대어, 값이 움직이면 <b>어느 표본을 고쳐야 하는지</b>를
        //   이름으로 알려 준다. 이 짝이 없으면 이 파일은 «옛날 저장소»를 시험하게 된다.

        private const float SamplePadY = 11f;
        private const float SampleHeadHeight = 18f;
        private const float SampleRowGap = 8f;
        private const float SampleGaugeHeight = 5f;
        private const float SampleFootHeight = 13f;
        private const float SampleChipHeight = 18f;

        /// <summary>표본의 카드 높이 — <b>합에서 유도한다</b>(74를 따로 적지 않는다).</summary>
        private const float SampleCardHeight = SamplePadY + SampleHeadHeight + SampleRowGap
                                               + SampleGaugeHeight + SampleRowGap + SampleFootHeight
                                               + SamplePadY;

        // ====================================================================
        // 재현 — 프로덕션의 <b>식</b>을 다시 적는다(값을 부르지 않는다)
        // ====================================================================

        /// <summary>
        /// <c>BuildStatCard</c>의 세로 사슬. <b>인자로 받는다</b> — 그래야 아래 대조들이
        /// <b>같은 함수</b>에 고장 난 숫자를 흘려 탐지력을 증명할 수 있다.
        /// <para>y는 위가 0, 아래로 갈수록 음수다(<c>UiChrome.PlaceTopLeft</c> 규약).</para>
        /// </summary>
        internal readonly struct CardStack
        {
            public readonly float HeadY;
            public readonly float GaugeY;
            public readonly float FootY;
            public readonly float ChipY;

            public CardStack(float headY, float gaugeY, float footY, float chipY)
            {
                HeadY = headY;
                GaugeY = gaugeY;
                FootY = footY;
                ChipY = chipY;
            }
        }

        internal static CardStack Compute(float padY, float headHeight, float rowGap,
            float gaugeHeight, float footHeight, float chipHeight)
        {
            float headY = -padY;
            float gaugeY = headY - headHeight - rowGap;
            float footY = gaugeY - gaugeHeight - rowGap;
            float chipY = footY + (chipHeight - footHeight) * 0.5f;
            return new CardStack(headY, gaugeY, footY, chipY);
        }

        /// <summary>「위 y와 높이」로 표현된 한 줄.</summary>
        private readonly struct Row
        {
            public readonly string Name;
            public readonly float Top;
            public readonly float Height;

            public Row(string name, float top, float height)
            {
                Name = name;
                Top = top;
                Height = height;
            }

            public float Bottom => Top - Height;
        }

        private static List<Row> RowsOf(CardStack s, float headHeight, float gaugeHeight,
            float footHeight, float chipHeight)
            => new List<Row>
            {
                new Row("1행(이름·출처·총합·보너스)", s.HeadY, headHeight),
                new Row("2행(게이지)", s.GaugeY, gaugeHeight),
                new Row("3행(중급까지 N)", s.FootY, footHeight),
                new Row("3행 단계 칩", s.ChipY, chipHeight),
            };

        /// <summary>카드 밖으로 나갔거나 서로 겹치는 줄을 전부 모은다(<b>순수 함수</b>).</summary>
        internal static List<string> Violations(CardStack s, float cardHeight, float headHeight,
            float gaugeHeight, float footHeight, float chipHeight)
        {
            var problems = new List<string>();
            List<Row> rows = RowsOf(s, headHeight, gaugeHeight, footHeight, chipHeight);

            foreach (Row row in rows)
            {
                if (row.Top > Tolerance)
                    problems.Add($"{row.Name}이 카드 위로 나갑니다(top {row.Top:0.###} > 0).");
                if (row.Bottom < -cardHeight - Tolerance)
                    problems.Add($"{row.Name}이 카드 아래로 나갑니다" +
                                 $"(bottom {row.Bottom:0.###} < -{cardHeight:0.###}).");
            }

            // 겹침 — 칩은 3행과 <b>같은 줄</b>이므로 그 쌍만 예외다(칩이 3행을 감싸는 것이 설계다).
            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = i + 1; j < rows.Count; j++)
                {
                    bool sameLine = rows[i].Name.StartsWith("3행") && rows[j].Name.StartsWith("3행");
                    if (sameLine) continue;
                    bool overlap = rows[i].Bottom < rows[j].Top - Tolerance
                                   && rows[j].Bottom < rows[i].Top - Tolerance;
                    if (overlap)
                        problems.Add($"{rows[i].Name}과 {rows[j].Name}이 세로로 겹칩니다 " +
                                     $"([{rows[i].Bottom:0.###}, {rows[i].Top:0.###}] ∩ " +
                                     $"[{rows[j].Bottom:0.###}, {rows[j].Top:0.###}]).");
                }
            }
            return problems;
        }

        private static CardStack ProductionStack() => Compute(
            CharacterInfoWindow.StatCardPadY,
            CharacterInfoWindow.StatHeadHeight,
            CharacterInfoWindow.StatRowGap,
            CharacterInfoWindow.StatGaugeHeight,
            CharacterInfoWindow.StatFootHeight,
            CharacterInfoWindow.StatTierChipHeight);

        private static List<string> ProductionViolations() => Violations(
            ProductionStack(),
            CharacterInfoWindow.StatCardHeight,
            CharacterInfoWindow.StatHeadHeight,
            CharacterInfoWindow.StatGaugeHeight,
            CharacterInfoWindow.StatFootHeight,
            CharacterInfoWindow.StatTierChipHeight);

        // ====================================================================
        // 1. ★ 인용된 등식 — 「상수에서 직접 다시 센다」
        // ====================================================================

        [Test]
        public void 카드_세로_검산_일곱항의_합이_카드_높이와_같다()
        {
            float sum = CharacterInfoWindow.StatCardPadY        // 11
                        + CharacterInfoWindow.StatHeadHeight    // 18
                        + CharacterInfoWindow.StatRowGap        //  8
                        + CharacterInfoWindow.StatGaugeHeight   //  5
                        + CharacterInfoWindow.StatRowGap        //  8
                        + CharacterInfoWindow.StatFootHeight    // 13
                        + CharacterInfoWindow.StatCardPadY;     // 11

            Assert.AreEqual(CharacterInfoWindow.StatCardHeight, sum, Tolerance,
                $"{LogPrefix} §4-3-2 검산이 깨졌습니다 — 일곱 항의 합 {sum} vs 카드 높이 " +
                $"{CharacterInfoWindow.StatCardHeight}. 넘치면 3행이 카드 밖으로 새고, 모자라면 " +
                "아래가 비어 보입니다. 카드 높이를 바꿨다면 어느 항을 함께 바꿀지 먼저 정하세요.");
        }

        /// <summary>합이 맞는지와 <b>바닥에 정확히 닿는지</b>는 다른 질문이다.
        /// 마지막 줄의 아래가 아래쪽 패딩 선과 <b>정확히</b> 만나야 «검산»이 성립한다.</summary>
        [Test]
        public void 마지막_줄의_아래가_아래쪽_패딩선에_정확히_닿는다()
        {
            CardStack s = ProductionStack();
            float footBottom = s.FootY - CharacterInfoWindow.StatFootHeight;
            float padLine = -(CharacterInfoWindow.StatCardHeight - CharacterInfoWindow.StatCardPadY);

            Assert.AreEqual(padLine, footBottom, Tolerance,
                $"{LogPrefix} 3행 아래({footBottom})가 아래쪽 패딩선({padLine})과 어긋납니다. " +
                "합 검산이 맞는데 여기가 어긋난다면, 사슬 어딘가에 <b>합에는 안 들어간 항</b>이 " +
                "끼어들었다는 뜻입니다(예: 행 간격이 두 번이 아니라 세 번 적용).");
        }

        // ====================================================================
        // 2. ★ 새로 잠그는 것 — 좌표
        // ====================================================================

        [Test]
        public void 네_줄이_전부_카드_안에_있고_서로_겹치지_않는다()
        {
            CardStack s = ProductionStack();
            Debug.Log($"{LogPrefix} headY={s.HeadY} gaugeY={s.GaugeY} footY={s.FootY} chipY={s.ChipY} " +
                      $"cardHeight={CharacterInfoWindow.StatCardHeight}");

            Assert.IsEmpty(ProductionViolations(),
                $"{LogPrefix} 스탯 카드 내부 배치가 깨졌습니다:\n  " +
                string.Join("\n  ", ProductionViolations()) + "\n" +
                "카드 밖으로 나간 조각은 <b>부모에 마스크가 걸려 있으면 조용히 잘리고</b>, " +
                "안 걸려 있으면 옆 카드 위에 겹쳐 그려집니다. 둘 다 «정상 화면과 똑같이 생긴» " +
                "실패입니다.");
        }

        /// <summary>
        /// 단계 칩은 3행보다 <b>크다</b>. 그 차이가 위아래로 <b>똑같이</b> 나뉘어야 3행 텍스트가
        /// 칩 안에서 가운데로 읽힌다 — 한쪽으로 쏠리면 «칩이 글자를 덮은 것»처럼 보인다.
        /// </summary>
        [Test]
        public void 단계_칩이_3행의_세로_가운데에_놓인다()
        {
            CardStack s = ProductionStack();
            float footCenter = s.FootY - CharacterInfoWindow.StatFootHeight * 0.5f;
            float chipCenter = s.ChipY - CharacterInfoWindow.StatTierChipHeight * 0.5f;

            Assert.AreEqual(footCenter, chipCenter, Tolerance,
                $"{LogPrefix} 칩 중심({chipCenter})과 3행 중심({footCenter})이 어긋납니다.");

            Assert.GreaterOrEqual(CharacterInfoWindow.StatTierChipHeight,
                CharacterInfoWindow.StatFootHeight,
                $"{LogPrefix} 칩이 3행보다 작아졌습니다 — 그러면 위 «가운데 맞춤» 식의 부호가 " +
                "뒤집혀 칩이 3행 안으로 파고듭니다. 설계는 칩이 3행을 <b>감싸는</b> 쪽입니다.");
        }

        /// <summary>가로 검산 — 폭 256은 «내용 폭 + 패딩 두 번»이어야 한다.</summary>
        [Test]
        public void 카드_가로가_내용폭과_패딩_두_번으로_정확히_나뉜다()
        {
            float sum = CharacterInfoWindow.StatCardPadX
                        + CharacterInfoWindow.StatCardContentWidth
                        + CharacterInfoWindow.StatCardPadX;

            Assert.AreEqual(CharacterInfoWindow.Col2ContentWidth, sum, Tolerance,
                $"{LogPrefix} 카드 가로 검산이 깨졌습니다 — {sum} vs {CharacterInfoWindow.Col2ContentWidth}. " +
                "카드 폭은 컬럼2의 내용 폭을 그대로 쓰므로(BuildStatCard가 Col2ContentWidth를 넘긴다), " +
                "컬럼 폭을 바꾸면 카드 내부 전부가 따라 움직입니다.");
        }

        // ====================================================================
        // 3. 양성/음성 대조 — 같은 함수에 고장 난 숫자를 흘린다
        // ====================================================================

        /// <summary>
        /// ★ <b>이것이 이 파일의 존재 이유다.</b> 합 검산이 <b>통과하는</b> 숫자를 주면서
        /// 칩만 키운다 — 합은 그대로 74인데 칩이 카드 밖으로 나간다.
        /// 합 검산만 있는 세상에서는 이 상태가 <b>초록</b>이다.
        /// </summary>
        [Test]
        public void 양성대조_합은_맞는데_칩이_카드_밖으로_나가면_잡는다()
        {
            // 합 검산은 칩을 세지 않는다 — 칩을 40으로 키워도 «74 = 74»는 그대로 참이다.
            const float hugeChip = 40f;
            CardStack broken = Compute(SamplePadY, SampleHeadHeight, SampleRowGap,
                SampleGaugeHeight, SampleFootHeight, hugeChip);
            List<string> problems = Violations(broken, SampleCardHeight, SampleHeadHeight,
                SampleGaugeHeight, SampleFootHeight, hugeChip);

            Assert.IsNotEmpty(problems,
                $"{LogPrefix} 칩이 카드 밖으로 나갔는데 잡지 못했습니다 — 그러면 이 파일은 " +
                "합 검산의 사본일 뿐이고, 새로 잠그는 것이 하나도 없습니다.");

            bool mentionsChip = false;
            foreach (string p in problems) if (p.Contains("칩")) mentionsChip = true;
            Assert.IsTrue(mentionsChip,
                $"{LogPrefix} 잡긴 잡았는데 <b>칩</b>을 지목하지 못했습니다: " +
                string.Join(" / ", problems));
        }

        [Test]
        public void 양성대조_행_간격이_늘면_아래로_넘치는_것을_잡는다()
        {
            CardStack broken = Compute(SamplePadY, SampleHeadHeight, SampleRowGap + 12f,
                SampleGaugeHeight, SampleFootHeight, SampleChipHeight);   // 8 -> 20
            Assert.IsNotEmpty(Violations(broken, SampleCardHeight, SampleHeadHeight,
                    SampleGaugeHeight, SampleFootHeight, SampleChipHeight),
                $"{LogPrefix} 행 간격을 12pt 키웠는데도 «카드 안»이라고 했습니다 — " +
                "경계 검사가 죽었습니다.");
        }

        [Test]
        public void 양성대조_행이_서로_겹치면_잡는다()
        {
            // 1행을 크게 키우고 간격을 음수로 주면 1행과 2행이 실제로 겹친다.
            // 카드 높이는 넉넉히 줘서 «넘침»이 아니라 «겹침»만 남게 한다.
            const float tallHead = 40f;
            CardStack broken = Compute(SamplePadY, tallHead, -20f,
                SampleGaugeHeight, SampleFootHeight, SampleChipHeight);
            List<string> problems = Violations(broken, 200f, tallHead,
                SampleGaugeHeight, SampleFootHeight, SampleChipHeight);

            bool mentionsOverlap = false;
            foreach (string p in problems) if (p.Contains("겹칩니다")) mentionsOverlap = true;
            Assert.IsTrue(mentionsOverlap,
                $"{LogPrefix} 두 행이 겹치는데 겹침으로 보고하지 않았습니다: " +
                (problems.Count == 0 ? "(위반 0건)" : string.Join(" / ", problems)));
        }

        /// <summary>
        /// ★ 음성 대조 — 멀쩡한 배치를 <b>위반이라고 하지 않는가</b>.
        /// 오탐이 몇 건만 있어도 이런 감사는 곧 꺼진다.
        /// </summary>
        [Test]
        public void 음성대조_설계값_그대로면_위반이_0건이다()
        {
            CardStack good = Compute(SamplePadY, SampleHeadHeight, SampleRowGap,
                SampleGaugeHeight, SampleFootHeight, SampleChipHeight);
            List<string> problems = Violations(good, SampleCardHeight, SampleHeadHeight,
                SampleGaugeHeight, SampleFootHeight, SampleChipHeight);

            Assert.IsEmpty(problems,
                $"{LogPrefix} 설계값 그대로인데 위반이 나왔습니다: " + string.Join(" / ", problems) + "\n" +
                "★ 특히 «3행과 칩이 겹친다»가 나왔다면 그건 오탐입니다 — 칩은 3행을 <b>감싸도록</b> " +
                "설계됐고(칩 18 > 3행 13), 그 둘은 같은 줄입니다.");

            // 그리고 그 «같은 줄 예외»가 실제로 작동했는지 확인한다 — 예외가 없으면 위가 빨개진다.
            Assert.Less(good.ChipY - SampleChipHeight, good.FootY,
                $"{LogPrefix} 칩이 3행을 감싸지 않습니다 — 이 표본은 «같은 줄 예외»를 시험하지 " +
                "못하므로 위 단언이 무엇을 증명하는지 알 수 없습니다.");
            Assert.Greater(good.ChipY, good.FootY,
                $"{LogPrefix} 칩 위가 3행 위보다 아래입니다 — 같은 이유로 표본이 부적합합니다.");
        }

        /// <summary>
        /// ★ <b>표본의 유통기한.</b> 위 대조들은 전부 리터럴 표본으로 도는데, 프로덕션 상수가
        /// 움직이면 그 표본은 <b>더 이상 이 저장소를 시험하지 않는다</b>. 여기서 여섯 개를
        /// 하나씩 맞대어, 갈라지는 순간 <b>어느 표본을 고쳐야 하는지</b>를 이름으로 알려 준다.
        /// </summary>
        [Test]
        public void 표본이_프로덕션_상수와_여전히_같다()
        {
            void Pin(string sampleName, float sample, string productionName, float production) =>
                Assert.AreEqual(production, sample, Tolerance,
                    $"{LogPrefix} 표본 {sampleName}({sample})이 프로덕션 " +
                    $"{productionName}({production})과 갈라졌습니다. 위 양성/음성 대조가 " +
                    "«옛날 저장소»를 시험하고 있습니다 — 표본을 지금 값으로 고치세요. " +
                    "(표본을 프로덕션 상수로 대체하지는 마세요: 그러면 기대값이 대상과 함께 " +
                    "움직여 아무것도 증명하지 못합니다.)");

            Pin(nameof(SamplePadY), SamplePadY,
                nameof(CharacterInfoWindow.StatCardPadY), CharacterInfoWindow.StatCardPadY);
            Pin(nameof(SampleHeadHeight), SampleHeadHeight,
                nameof(CharacterInfoWindow.StatHeadHeight), CharacterInfoWindow.StatHeadHeight);
            Pin(nameof(SampleRowGap), SampleRowGap,
                nameof(CharacterInfoWindow.StatRowGap), CharacterInfoWindow.StatRowGap);
            Pin(nameof(SampleGaugeHeight), SampleGaugeHeight,
                nameof(CharacterInfoWindow.StatGaugeHeight), CharacterInfoWindow.StatGaugeHeight);
            Pin(nameof(SampleFootHeight), SampleFootHeight,
                nameof(CharacterInfoWindow.StatFootHeight), CharacterInfoWindow.StatFootHeight);
            Pin(nameof(SampleChipHeight), SampleChipHeight,
                nameof(CharacterInfoWindow.StatTierChipHeight), CharacterInfoWindow.StatTierChipHeight);
            Pin(nameof(SampleCardHeight), SampleCardHeight,
                nameof(CharacterInfoWindow.StatCardHeight), CharacterInfoWindow.StatCardHeight);
        }
    }
}
