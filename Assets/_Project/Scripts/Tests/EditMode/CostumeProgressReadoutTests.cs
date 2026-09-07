using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 정보창 「테마 세트」 블록 <b>3행 — 코스튬 진화 진행률</b>(P5-b)의 표시 계약.
    ///
    /// 정본: <c>docs/DESIGN_SYSTEMS_COSTUME_EVOLUTION.md</c> §7-2(형식) · §7-3(내림) · §7-4(자리) ·
    /// §9(I-C8 · I-C8′), <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 7-5절.
    ///
    /// ============================================================================
    /// ★ 임계(10h / 50h / 100h)와 캡(240분)을 <b>숫자로 한 글자도 적지 않는다</b>
    /// ============================================================================
    /// 전부 <see cref="CostumeEvolutionRules"/>의 상수를 <b>참조</b>해서 만든다(I-C5 · CLAUDE.md).
    /// 2026-09-01에 <c>MaxCharacterScale</c>과 저장 스키마 버전이 하드코딩 잔존으로 4건 깨졌고,
    /// 그때 확정된 규칙이다. 상수는 <c>public</c>/<c>internal</c>이라 리플렉션도 소스 스크래핑도
    /// 없이 이름으로 참조한다 — <b>이름이 바뀌면 컴파일이 깨진다</b>(조용히 대상을 잃는 매처보다 낫다).
    ///
    /// ============================================================================
    /// ★ 기대값의 출처 — 프로덕션 함수가 <b>아니다</b>
    /// ============================================================================
    /// 문자열 앵커 <c>"12시간 30분"</c> · <c>"99.6%"</c> · <c>"99.9%"</c>는 <b>설계 문서가 손으로 낸 값</b>이고
    /// (§7-2 예시 · §7-3 표), 여기서 프로덕션 함수로 다시 만들지 않는다. 그러면 그 함수가 틀어질 때
    /// 기대값도 함께 틀어져 <b>아무것도 못 잰다</b>(TEAM.md 「생성기와 검사기가 같이 틀린다」).
    ///
    /// <para>★ <c>"99.6%"</c>가 특히 그렇다 — <b>통짜 100h 기준이면 99.8%</b>다. 두 숫자가 <b>둘 다
    /// 그럴듯해서</b> 설계 초판이 어긋난 채로 통과했다(§0-3). 아래 <c>I_C8′</c> 테스트는 맞는 값을
    /// 단언하는 동시에 <b>틀린 값이 나오지 않는지</b>도 같이 못박는다 — 그래야 «분모가 6,000으로
    /// 되돌아가는» 회귀가 조용히 지나가지 않는다.</para>
    /// </summary>
    public sealed class CostumeProgressReadoutTests
    {
        // ====================================================================
        // 0. 교정 — 아래 숫자가 서 있는 바닥부터 확인한다
        // ====================================================================

        /// <summary>임계표가 단조가 아니면 구간 span이 0이나 음수가 되고, 그 아래 모든 계산이 무의미해진다.
        /// <b>이 교정이 깨지면 이 파일의 나머지 숫자를 전부 폐기해야 한다.</b></summary>
        [SetUp]
        public void 교정()
        {
            Assert.IsTrue(CostumeEvolutionRules.BoundariesAreStrictlyIncreasing(),
                "임계표가 단조 증가가 아닙니다 — 이 파일의 모든 구간 계산이 무의미해집니다.");
            Assert.Greater(CostumeEvolutionRules.StageCount, 1,
                "단계가 하나뿐입니다 — 「다음 경계까지」라는 표시 자체가 성립하지 않습니다.");
        }

        // ====================================================================
        // 1. 자리 — 세트 패널 3행의 세로/가로 검산 (§7-4)
        // ====================================================================

        /// <summary>세트 패널 아래 여백. <b>2행판에서 역산</b>한다 — 새 숫자를 만들지 않는다.</summary>
        private static float SetPanelBottomPad =>
            CharacterInfoWindow.SetPanelHeight
            - (CharacterInfoWindow.StatCardPadY + CharacterInfoWindow.SetProgressHeight
               + CharacterInfoWindow.SetRowGap + CharacterInfoWindow.SetDetailHeight);

        /// <summary>
        /// 3행판 패널 높이를 <b>다른 식으로</b> 다시 센다(§7-4 검산 <c>11 + 16 + 6 + 14 + 6 + 14 + 9 = 76</c>).
        /// <para>프로덕션은 «2행판 + 간격 + 3행»으로 유도하고, 여기서는 «위 여백부터 아래 여백까지»로
        /// 쌓는다. 두 경로가 같은 값을 내야 한다 — 한쪽만 고치면 여기서 갈린다.</para>
        /// </summary>
        [Test]
        public void 코스튬_3행판_패널_높이가_위여백부터_아래여백까지_합과_같다()
        {
            Assert.Greater(SetPanelBottomPad, 0f,
                "2행판 패널에 아래 여백이 남아 있지 않습니다 — 이 검산의 전제가 깨졌습니다.");

            float used = CharacterInfoWindow.StatCardPadY
                         + CharacterInfoWindow.SetProgressHeight
                         + CharacterInfoWindow.SetRowGap
                         + CharacterInfoWindow.SetDetailHeight
                         + CharacterInfoWindow.SetRowGap
                         + CharacterInfoWindow.SetCostumeHeight
                         + SetPanelBottomPad;

            Assert.AreEqual(CharacterInfoWindow.SetPanelHeightWithCostume, used, 0.001f,
                $"3행판 세트 패널 세로 검산이 깨졌습니다 — 내부 합 {used} vs 선언 " +
                $"{CharacterInfoWindow.SetPanelHeightWithCostume}. 넘치면 3행이 패널 밖으로 새고, " +
                "모자라면 아래가 비어 보입니다(§7-4).");
        }

        /// <summary>3행이 2행을 <b>덮지 않고</b>, 패널 아래로도 <b>새지 않는다</b>.</summary>
        [Test]
        public void 코스튬_3행이_2행과_겹치지_않고_패널_안에_들어간다()
        {
            float detailBottom = -CharacterInfoWindow.SetDetailY + CharacterInfoWindow.SetDetailHeight;
            float costumeTop = -CharacterInfoWindow.SetCostumeY;

            Assert.GreaterOrEqual(costumeTop, detailBottom,
                $"3행이 2행 위로 올라탔습니다 — 3행 위 끝 {costumeTop} vs 2행 아래 끝 {detailBottom}. " +
                "글자 두 줄이 같은 자리에 겹쳐 그려집니다.");

            float costumeBottom = costumeTop + CharacterInfoWindow.SetCostumeHeight;
            Assert.LessOrEqual(costumeBottom, CharacterInfoWindow.SetPanelHeightWithCostume + 0.001f,
                $"3행이 패널 밖으로 나갑니다 — 3행 아래 끝 {costumeBottom} vs 패널 " +
                $"{CharacterInfoWindow.SetPanelHeightWithCostume}.");
        }

        /// <summary>3행 두 칸(누적 시간 · 퍼센트)이 카드 콘텐츠 폭을 <b>정확히</b> 나눠 쓴다.</summary>
        [Test]
        public void 코스튬_3행_두_칸이_콘텐츠_폭을_정확히_나눠_쓴다()
        {
            float used = CharacterInfoWindow.SetCostumeTimeWidth
                         + CharacterInfoWindow.SetCostumeGap
                         + CharacterInfoWindow.SetCostumePercentWidth;

            Assert.AreEqual(CharacterInfoWindow.StatCardContentWidth, used, 0.001f,
                $"3행 가로 검산이 깨졌습니다 — {used} vs {CharacterInfoWindow.StatCardContentWidth}. " +
                "남으면 오른쪽이 뜨고, 모자라면 시간 칸이 퍼센트를 덮습니다.");

            Assert.Greater(CharacterInfoWindow.SetCostumeTimeWidth,
                CharacterInfoWindow.SetCostumePercentWidth,
                "누적 시간 칸이 퍼센트 칸보다 좁습니다 — 「9999시간 59분」이 먼저 잘립니다.");
        }

        /// <summary>
        /// ★★ <b>세로 예산 — 계약 C1 「잉크 불가침」 + C2 「아래 여백 잔량」</b>(2026-09-08 §14-5 확정).
        ///
        /// <para>★ <b>여기 있던 <c>Assert.Ignore</c>는 걷어냈다.</b> 그 「알려진 갭 4pt」는
        /// <b>재는 양이 틀린 것</b>이었다 — 위 여백부터 <b>아래 여백까지</b> 세어 736과 비교했는데,
        /// <b>아래 여백에는 그려지는 것이 하나도 없다.</b> 잉크가 실제로 끝나는 자리(세트 패널의
        /// 아래 테두리)는 <b>720</b>이고 뷰포트는 736이라 <b>설계 크기에서 잘리는 픽셀은 0</b>이다
        /// (ux-designer §14-2 좌표표). 계약이 스스로 적어 둔 피해
        /// (*"아래 블록의 발견 가능성이 떨어진다"*)는 설계 크기에서 <b>발생하지 않는다</b>.</para>
        ///
        /// <para>그래서 계약을 <b>폐기하지 않고 둘로 갈랐다</b>. 그리고 이것은 <b>완화가 아니라 강화</b>다:
        /// <c>Assert.Ignore</c>는 <b>회귀를 하나도 못 막지만</b>(건너뜀은 실패가 아니다) 아래 둘은 막고,
        /// <b>C2는 지금 정확히 하한</b>(16 ≥ 16)이라 <b>다음 1pt에서 즉시 빨개진다</b>.</para>
        ///
        /// <para>★ <b>같은 계약을 <c>CharacterStatColumnLayoutTests</c>도 잰다 — 일부러 그렇다.</b>
        /// 저쪽은 «위 여백 + 블록 넷 + 간격 셋»으로 한 번에 세고, 여기서는 <b>블록을 하나씩 밟아
        /// 내려가며 마지막 패널의 아래 테두리 좌표</b>를 구한다(§14-2 좌표표와 같은 걸음). 두 경로가
        /// 같은 값을 내야 하고, 한쪽만 고치면 여기서 갈린다.</para>
        /// </summary>
        [Test]
        public void 코스튬_3행이_켜져도_잉크가_본문_밖으로_나가지_않는다()
        {
            float delta = CharacterInfoWindow.SetPanelHeightWithCostume
                          - CharacterInfoWindow.SetPanelHeight;
            Assert.Greater(delta, 0f, "3행판이 2행판보다 크지 않습니다 — 이 측정의 대상이 사라졌습니다.");

            float body = CharacterInfoWindow.BodyHeight;
            float twoRowInk = Column2InkBottom(CharacterInfoWindow.SetPanelHeight);
            float threeRowInk = Column2InkBottom(CharacterInfoWindow.SetPanelHeightWithCostume);

            // 교정 — 이 계산기가 실제로 컬럼 2를 세고 있는가(2행판이 3행판보다 정확히 delta만큼 짧다).
            Assert.AreEqual(delta, threeRowInk - twoRowInk, 0.001f,
                $"3행 증분이 잉크 하단에 그대로 전해지지 않았습니다 — 잉크 차 {threeRowInk - twoRowInk} vs " +
                $"패널 차 {delta}. 이 계산기가 다른 것을 세고 있습니다. 아래 판정 전부 무효입니다.");
            Assert.Greater(twoRowInk, body * 0.9f,
                $"컬럼 2가 본문의 90% 미만만 쓰고 있습니다({twoRowInk} / {body}) — 블록 하나가 통째로 빠졌습니다.");

            // ★★ 이 라운드 판정의 <b>바닥</b>을 못박는다: «사용 높이 = 잉크 + 아래 여백».
            //   예전 계약이 잰 것은 왼쪽이고, 지금 계약이 재는 것은 오른쪽의 첫 항이다.
            //   둘의 차가 정확히 ColPadY가 아니면 «4pt는 전부 여백이었다»는 판정 근거가 무너진다.
            Assert.AreEqual(Column2UsedHeight(CharacterInfoWindow.SetPanelHeightWithCostume),
                threeRowInk + CharacterInfoWindow.ColPadY, 0.001f,
                "«사용 높이 = 잉크 하단 + 아래 여백»이 깨졌습니다 — 예전 계약이 재던 4pt가 " +
                "정말 아래 여백이었는지 더 이상 말할 수 없습니다(§14-2 판정의 전제).");

            // ---- C1 잉크 불가침 ----
            Assert.LessOrEqual(threeRowInk, body,
                $"[C1 잉크 불가침] 코스튬 3행이 켜지면 잉크가 본문 밖으로 나갑니다 — " +
                $"잉크 하단 {threeRowInk} vs 본문 {body} (초과 {threeRowInk - body}pt).\n" +
                $"  2행판 잉크 {twoRowInk} + 3행 증분 {delta}.\n" +
                "  ★ 이건 «여백이 줄었다»가 아니라 «글자가 잘린다»입니다. 줄일 수 있는 곳: " +
                "Col2BlockGap · SetCostumeHeight · SetRowGap — 전부 ux-designer/design-systems 소관이라 " +
                "coder-ui가 임의로 못 정합니다(규약: 억지로 넣지 말고 보고).");

            // ---- C2 아래 여백 잔량 ----
            float slack = body - threeRowInk;
            Assert.GreaterOrEqual(slack, UiChrome.Space4,
                $"[C2 아래 여백 잔량] 잉크 아래 여백이 {slack}pt로 한 칸({UiChrome.Space4})보다 좁습니다 — " +
                "세트 패널이 본문 아래 끝에 붙어 «잘린 것처럼» 읽힙니다.\n" +
                "  이 계약은 2026-09-08 기준 <b>정확히 하한</b>이었습니다(16 = 16). 지금 빨간 것은 " +
                "누군가 컬럼 2 세로에 1pt 이상을 얹었다는 뜻입니다.");
        }

        /// <summary>
        /// ★ 잉크 하단 — 블록을 <b>하나씩 밟아 내려가며</b> 마지막(세트) 패널의 아래 테두리 좌표를 구한다.
        /// <c>CharacterStatColumnLayoutTests</c>가 «위 여백 + Σ블록 + Σ간격»으로 한 번에 세는 것과
        /// <b>일부러 다른 걸음</b>이다(§14-2 좌표표: 20 → 372 → 384 → 543 → 555 → 606 → 618 → 720).
        /// <b>아래 여백은 더하지 않는다</b> — 거기에는 그려지는 것이 없다.
        /// </summary>
        private static float Column2InkBottom(float setPanelHeight)
        {
            float y = CharacterInfoWindow.ColPadY;                     // 위 여백

            // 능력치 / STATUS
            y += CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
                 + CharacterInfoWindow.StatCardsHeight;
            y += CharacterInfoWindow.Col2BlockGap;

            // 기록 / RECORD
            y += CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
                 + CharacterInfoWindow.GaugeBlockHeight + CharacterInfoWindow.Col2LabelGap
                 + CharacterInfoWindow.StatCount * CharacterInfoWindow.RecordRowHeight;
            y += CharacterInfoWindow.Col2BlockGap;

            // 표시 / DISPLAY
            y += CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
                 + CharacterInfoWindow.NameRowHeight;
            y += CharacterInfoWindow.Col2BlockGap;

            // 테마 세트 / SET — 제목줄 + 간격 + 패널. 그 패널의 아래 테두리가 <b>잉크의 끝</b>이다.
            y += CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
                 + setPanelHeight;
            return y;
        }

        /// <summary>컬럼 2가 쓰는 세로 높이. <b>1차 상수만</b> 참조해 직접 쌓는다 —
        /// 프로덕션의 <c>LayoutColumn2</c>를 부르면 그쪽이 틀어질 때 기대값도 같이 틀어진다.</summary>
        private static float Column2UsedHeight(float setPanelHeight)
        {
            const int blockCount = 4;
            float status = CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
                           + CharacterInfoWindow.StatCardsHeight;
            float record = CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
                           + CharacterInfoWindow.GaugeBlockHeight + CharacterInfoWindow.Col2LabelGap
                           + CharacterInfoWindow.StatCount * CharacterInfoWindow.RecordRowHeight;
            float display = CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
                            + CharacterInfoWindow.NameRowHeight;
            float set = CharacterInfoWindow.SectionLabelHeight + CharacterInfoWindow.Col2LabelGap
                        + setPanelHeight;

            return CharacterInfoWindow.ColPadY
                   + status + record + display + set
                   + (blockCount - 1) * CharacterInfoWindow.Col2BlockGap
                   + CharacterInfoWindow.ColPadY;
        }

        // ====================================================================
        // 2. 퍼센트 — 형식 · 분모 · 범위 (§7-2 · §7-3 · I-C8 · I-C8′)
        // ====================================================================

        /// <summary>
        /// ★★ <b>I-C8′ — 분모는 「현재 구간 span」이지 6,000이 아니다.</b>
        ///
        /// <para>【앵커】§7-3 표: 누적 <c>5,990분</c>(= 마스터 경계 − 10분) → <b>99.6%</b>.
        /// <b>통짜 100h 기준이면 99.8%</b>이고, 그 둘이 <b>둘 다 그럴듯해서</b> 설계 초판이
        /// 어긋난 채로 통과했다(§0-3). 그래서 맞는 값과 <b>틀린 값</b>을 같은 테스트에서 함께 못박는다.</para>
        /// </summary>
        [Test]
        public void 퍼센트의_분모가_현재_구간이지_전체_100시간이_아니다()
        {
            const int back = 10;
            int minutes = CostumeEvolutionRules.Stage3BoundaryMinutes - back;

            Assert.AreEqual(CostumeEvolutionRules.MasterStage - 1,
                CostumeEvolutionRules.StageOf(minutes),
                "앵커가 마지막 구간 안에 있지 않습니다 — 아래 비교의 전제가 깨졌습니다.");

            Assert.AreEqual("99.6%", CharacterInfoWindow.FormatCostumePercent(minutes),
                $"누적 {minutes}분의 진행률이 §7-3 표와 다릅니다. 「구간 기준」이면 99.6%이고 " +
                "「통짜 100h 기준」이면 99.8%입니다 — 분모가 현재 구간 span에서 전체로 되돌아갔는지 " +
                "확인하십시오(I-C8′).");

            Assert.AreNotEqual("99.8%", CharacterInfoWindow.FormatCostumePercent(minutes),
                "통짜 100h 기준(누적 ÷ 6,000)으로 되돌아갔습니다 — §7-2가 못박은 " +
                "「현재 단계의 다음 경계까지」가 깨졌습니다(I-C8′).");
        }

        /// <summary>구간 정확히 절반이면 <c>50.0%</c>다 — 축척(1/1000)과 자릿수 나누기가
        /// 둘 다 맞아야만 나오는 값이고, <b>입력은 상수에서 유도</b>한다.</summary>
        [Test]
        public void 구간_한가운데는_50_0퍼센트다()
        {
            int start = CostumeEvolutionRules.Stage1BoundaryMinutes;
            int end = CostumeEvolutionRules.Stage2BoundaryMinutes;
            Assert.AreEqual(0, (end - start) % 2, "구간 길이가 홀수라 한가운데가 정수가 아닙니다.");

            Assert.AreEqual("50.0%", CharacterInfoWindow.FormatCostumePercent(start + (end - start) / 2),
                "구간 한가운데가 50.0%가 아닙니다 — 천분율 축척이나 자릿수 나누기가 어긋났습니다.");
        }

        /// <summary>【앵커】§7-3 표 — 마스터 직전 한 칸은 <b>99.9%</b>이고, 마스터는 <b>퍼센트를 안 그린다</b>.
        /// 이 둘이 «100.0% 표시 ≡ 마스터 도달»이라는 이 기능의 약속 그 자체다(§7-2 · §7-3-a).</summary>
        [Test]
        public void 마스터_직전은_99_9퍼센트이고_마스터는_퍼센트를_그리지_않는다()
        {
            Assert.AreEqual("99.9%",
                CharacterInfoWindow.FormatCostumePercent(CostumeEvolutionRules.Stage3BoundaryMinutes - 1),
                "마스터 직전 한 칸이 99.9%가 아닙니다 — round로 바뀌면 여기가 100.0%가 되고, " +
                "「100.0%인데 마스터가 아닌」 화면이 생깁니다(§7-3).");

            Assert.AreEqual(string.Empty,
                CharacterInfoWindow.FormatCostumePercent(CostumeEvolutionRules.Stage3BoundaryMinutes),
                "마스터에서 퍼센트를 그리고 있습니다 — §7-2는 마스터면 누적 시간만 남긴다고 못박았습니다.");
        }

        /// <summary>
        /// ★ <b>전수</b> — 0분부터 마스터 직전까지 <c>100.0%</c>가 <b>한 번도</b> 나오지 않는다(I-C8).
        /// <para>양성 대조를 함께 붙인다: 이 순회가 실제로 <b>서로 다른 값</b>을 만들어 내는지
        /// (<c>0.0%</c>와 <c>99.9%</c>가 둘 다 나왔는지) 확인한다. 안 하면 «전부 빈 문자열이라
        /// 조용히 초록»과 구별되지 않는다(거짓 통과 #5의 형태).</para>
        /// </summary>
        [Test]
        public void 마스터가_아닌_동안_100_0퍼센트가_한_번도_나오지_않는다()
        {
            bool sawZero = false;
            bool sawMax = false;
            int drawn = 0;

            for (int minutes = 0; minutes < CostumeEvolutionRules.Stage3BoundaryMinutes; minutes++)
            {
                string percent = CharacterInfoWindow.FormatCostumePercent(minutes);
                Assert.IsNotEmpty(percent,
                    $"누적 {minutes}분은 마스터가 아닌데 퍼센트를 그리지 않았습니다 — " +
                    "「그릴 값이 없다」가 마스터가 아닌 자리로 샜습니다.");
                Assert.AreNotEqual("100.0%", percent,
                    $"누적 {minutes}분에서 100.0%가 떴는데 마스터가 아닙니다 — " +
                    "「100.0% 표시 ≡ 마스터 도달」이 깨졌습니다(I-C8 · §7-3-a의 클램프).");

                drawn++;
                if (percent == "0.0%") sawZero = true;
                if (percent == "99.9%") sawMax = true;
            }

            Assert.AreEqual(CostumeEvolutionRules.Stage3BoundaryMinutes, drawn,
                "순회한 칸 수가 마스터 경계와 다릅니다 — 위 «없음» 판정의 모집단이 비었습니다.");
            Assert.IsTrue(sawZero, "전 구간에서 0.0%가 한 번도 안 나왔습니다 — 이 순회가 실제로 " +
                "값을 만들고 있는지 의심스럽습니다(양성 대조 실패).");
            Assert.IsTrue(sawMax, "전 구간에서 99.9%가 한 번도 안 나왔습니다 — 내림 최댓값이 " +
                "999에 닿지 못했다는 뜻이고, 그러면 위 «100.0% 없음» 판정이 공허합니다(양성 대조 실패).");
        }

        /// <summary>퍼센트를 그리는가 / 안 그리는가가 <b>마스터 여부와 정확히 동치</b>다.
        /// 마스터 위쪽(캡을 넘겨 저장된 파일 등)에서도 «빈 문자열»이 유지된다.</summary>
        [Test]
        public void 퍼센트_없음과_마스터_도달이_동치다()
        {
            int master = CostumeEvolutionRules.Stage3BoundaryMinutes;
            int[] samples =
            {
                0, 1,
                CostumeEvolutionRules.Stage1BoundaryMinutes - 1,
                CostumeEvolutionRules.Stage1BoundaryMinutes,
                CostumeEvolutionRules.Stage2BoundaryMinutes - 1,
                CostumeEvolutionRules.Stage2BoundaryMinutes,
                master - 1, master, master + 1,
                master + CostumeEvolutionRules.DailySoftCapMinutes,
                master * 10,
            };

            foreach (int minutes in samples)
            {
                bool blank = CharacterInfoWindow.FormatCostumePercent(minutes).Length == 0;
                bool isMaster = CostumeEvolutionRules.StageOf(minutes) == CostumeEvolutionRules.MasterStage;
                Assert.AreEqual(isMaster, blank,
                    $"누적 {minutes}분 — 마스터 여부({isMaster})와 «퍼센트를 안 그린다»({blank})가 " +
                    "어긋났습니다. 둘은 동치여야 합니다(§7-2).");
            }
        }

        /// <summary>손상된 파일이 음수를 들고 와도 화면이 <c>-0.-1%</c> 같은 것을 그리지 않는다.
        /// 음수는 Core가 이미 0단계로 보므로 <b>0.0%</b>가 정확한 답이다.</summary>
        [Test]
        public void 음수_누적도_0_0퍼센트로_그린다()
        {
            Assert.AreEqual("0.0%", CharacterInfoWindow.FormatCostumePercent(-1));
            Assert.AreEqual("0.0%", CharacterInfoWindow.FormatCostumePercent(int.MinValue / 2));
        }

        // ====================================================================
        // 3. 누적 시간 — 형식이 「함께한 시간」과 같은 규칙인가 (§7-2 「새 형식 0개, 재사용」)
        // ====================================================================

        /// <summary>【앵커】§7-2 예시 문자열 <c>"12시간 30분"</c>과 1시간 미만 규칙.</summary>
        [Test]
        public void 누적_시간이_설계_예시_문자열과_같다()
        {
            Assert.AreEqual("12시간 30분", CharacterInfoWindow.FormatCostumeFocusTime(12 * 60 + 30),
                "§7-2의 표시 예시와 다릅니다.");
            Assert.AreEqual("30분", CharacterInfoWindow.FormatCostumeFocusTime(30),
                "1시간 미만인데 「0시간」을 그리고 있습니다(§7-2: 1시간 미만이면 분만).");
            Assert.AreEqual("0분", CharacterInfoWindow.FormatCostumeFocusTime(0),
                "누적 0분은 «아직 이 코스튬으로 집중한 적이 없다»는 정확한 사실입니다 — " +
                "그대로 그려야 합니다(§7-2).");
            Assert.AreEqual("0분", CharacterInfoWindow.FormatCostumeFocusTime(-5),
                "음수 누적이 음수 시간으로 새어 나갑니다.");
        }

        /// <summary>
        /// ★ <b>두 구현이 같은 답을 내는가</b> — 「함께한 시간」(<see cref="CharacterStatsModel"/>)과
        /// 코스튬 누적은 <b>다른 양</b>이라 함수를 공유하지 못한다(그쪽은 인자가 없다).
        /// 규칙만 같게 두 벌로 적었으므로 <b>여기서 매 실행 대조</b>한다 — 한쪽만 바뀌면 빨개진다.
        /// <para>★ Core에 <c>FormatHourMinute(int)</c>를 두어 한 벌로 만드는 것이 옳고,
        /// 그 파일은 이 라운드 소유가 아니라 <b>리더에게 보고했다</b>. 이 테스트는 그때까지의 이음매다.</para>
        /// </summary>
        [Test]
        public void 누적_시간_표기가_함께한_시간과_같은_규칙이다()
        {
            int[] samples =
            {
                0, 1, 59, 60, 61, 90, 750,
                CostumeEvolutionRules.Stage1BoundaryMinutes - 1,
                CostumeEvolutionRules.Stage1BoundaryMinutes,
                CostumeEvolutionRules.Stage2BoundaryMinutes,
                CostumeEvolutionRules.Stage3BoundaryMinutes - 1,
                CostumeEvolutionRules.Stage3BoundaryMinutes,
                CostumeEvolutionRules.Stage3BoundaryMinutes + CostumeEvolutionRules.DailySoftCapMinutes,
            };

            try
            {
                foreach (int minutes in samples)
                {
                    CharacterStatsModel.ResetForTesting();
                    if (minutes > 0) CharacterStatsModel.AddCompanionSeconds(minutes * 60f);

                    Assert.AreEqual(CharacterStatsModel.FormatCompanionTime(),
                        CharacterInfoWindow.FormatCostumeFocusTime(minutes),
                        $"{minutes}분에서 두 표기가 갈렸습니다 — 「함께한 시간」과 코스튬 누적이 " +
                        "서로 다른 규칙으로 시간을 적고 있습니다(§7-2: 새 형식 0개, 재사용).");
                }
            }
            finally
            {
                CharacterStatsModel.ResetForTesting();
            }
        }

        // ====================================================================
        // 4. 빈 상태 — 오늘 사용자가 실제로 보는 화면
        // ====================================================================

        /// <summary>
        /// ★ 오늘의 기본값 = <b>누적 기록이 하나도 없는 상태</b>(1일차 무료 4종은 <c>mil</c>이라
        /// 실린 <c>costume.office</c>가 서지 않는다). 그때 누적 조회가 던지지 않고 <c>0</c>을 내고,
        /// 그 0이 화면에서 <c>"0분"</c> / <c>"0.0%"</c>로 <b>깨지지 않고</b> 그려진다.
        /// <para>(행 자체가 뜨는지 여부는 <see cref="CostumeResolver"/> 소관이라 그쪽 라운드의 테스트가 잰다.
        /// ★ 2026-09-07 실측 정정 — 실린 매니페스트는 <b>0개가 아니라 1개</b>다(<c>costume.office</c>).
        /// 처음 세었을 때 0이 나온 것은 <c>.asset</c>이 스크립트를 <b>GUID로</b> 가리키는데 이름으로
        /// 훑었기 때문이고, 그 0건은 «정말 없다»와 똑같이 생겼다 — <c>AccessoryDefSO</c>를 같은 방식으로
        /// 세면 42가 나와야 하는데 0이 나온 것을 보고 잡았다.)</para>
        /// </summary>
        [Test]
        public void 코스튬_기록이_하나도_없어도_표시가_깨지지_않는다()
        {
            Assert.AreEqual(0, CostumeProgressModel.MinutesOf(null),
                "null 키에서 0이 아닌 값이 나왔습니다.");
            Assert.AreEqual(0, CostumeProgressModel.MinutesOf(string.Empty),
                "빈 키에서 0이 아닌 값이 나왔습니다.");
            Assert.AreEqual(0, CostumeProgressModel.MinutesOf("costume.그런것은없다"),
                "모르는 키에서 0이 아닌 값이 나왔습니다.");

            int minutes = CostumeProgressModel.MinutesOf("costume.그런것은없다");
            Assert.AreEqual("0분", CharacterInfoWindow.FormatCostumeFocusTime(minutes));
            Assert.AreEqual("0.0%", CharacterInfoWindow.FormatCostumePercent(minutes),
                "누적 0분을 «—»나 빈칸으로 지웠습니다 — §7-2는 0.0%를 다른 값과 같은 잉크로 " +
                "그대로 그리라고 못박았습니다(«잔액 0 → 0» 선례 승계).");
        }

        // ====================================================================
        // 5. 축척 상수 — 화면이 든 단위가 Core가 내는 단위와 같은가
        // ====================================================================

        /// <summary>1%p = 천분율 10칸. 화면 상수 두 개가 서로에게서 유도되는지 확인한다 —
        /// 하나만 손대면 <c>3.74%</c>처럼 자릿수가 통째로 밀린다.</summary>
        [Test]
        public void 퍼센트_축척_상수가_서로에게서_유도된다()
        {
            Assert.AreEqual(CharacterInfoWindow.CostumePercentTenthsScale / 100,
                CharacterInfoWindow.CostumePercentTenthsPerPoint,
                "천분율 축척과 «1%p당 칸 수»가 어긋났습니다 — 소수점 자리가 밀립니다.");

            // 축척이 실제 반환값의 단위와 같은가 — 구간 시작은 0칸이어야 한다.
            Assert.AreEqual(0,
                CostumeEvolutionRules.PercentTenthsToNextBoundary(
                    CostumeEvolutionRules.Stage2BoundaryMinutes),
                "구간 시작인데 천분율이 0이 아닙니다 — 이 파일이 든 축척의 전제가 깨졌습니다.");
        }
    }
}
