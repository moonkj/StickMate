using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ §8 — <b>커버선 ≥ 착용선</b> 회귀 게이트(HEAD 6종). 2026-09-06 R25c 결함 수정 직후 신설.
    ///
    /// <para><b>무엇을 잠그나.</b> <see cref="AccessoryShapeBuilder.HatCoverLocalY"/>는 그 모자가
    /// <b>머리카락을 어디서 자를지</b>를 선언한다(<c>AppendClippedBelowCover</c>가 이 선 <b>위쪽</b>
    /// 머리카락을 잘라 낸다). 모자 채움은 머리 꼭대기에서 <b>H-2 착용선</b>까지 덮는다. 그래서
    /// 커버선이 착용선보다 <b>낮으면</b> 그 사이는 <b>머리카락도 모자도 없는 민머리 띠</b>가 된다
    /// (<c>docs/EQUIPMENT_HANDOFF_PORT_SPEC.md</c> §14-16-5-a: <i>"커버선이 실제 착용선보다 높으면
    /// 머리카락이 덜 잘려 모자 채움 뒤에 숨고, 낮으면 … 민머리 띠가 생긴다. 낮추는 방향으로는
    /// 건드리지 마라"</i>).</para>
    ///
    /// <para><b>왜 지금 생겼나.</b> 오늘 밤 실제로 그 사고가 났다 — R25가 인계본 조각으로 모자 6종을
    /// 통째로 올렸는데 커버선 상수는 <b>옛 낮은 값에 그대로 머물렀다</b>
    /// (<c>HatBrimLineRatio</c> 0.06 · <c>BeanieBandTopRatio</c> −0.06 · <c>FedoraBrimLineRatio</c> 0.08 ·
    /// <c>BeretBrimLineRatio</c> 0.02 · <c>StrawBrimLineRatio</c> 0.08). 천모자 기준 민머리 띠가
    /// <b>0.2410 R</b>이었고(프로덕션 주석 실측: 노출 면적 0.667 R² · 최대 세로 0.372 R),
    /// <b>그것을 보는 테스트가 하나도 없었다.</b> 커버선을 재는 기존 검사
    /// (<c>AccessoryShapeCatalogTests</c> · <c>ItemCatalogAssetParityTests</c> ·
    /// <c>ShapeCoverageGuardTests</c>)는 전부 <b>커버선끼리</b>, 또는 <b>커버선과 카탈로그 플래그</b>만
    /// 대조했다 — 커버선을 <b>모자 도형</b>과 맞대 본 자가 없었다. 이 파일이 그 자다.</para>
    ///
    /// <para><b>같은 자를 쓴다.</b> 짝 파일(§0~§7)의 스캔라인 자를 <b>그대로</b> 쓴다 —
    /// <see cref="ChordCoveredAt"/>(덮임 술어) · <see cref="FrontFillPolygonsInR"/>(프로덕션 조각 읽기) ·
    /// <see cref="Measure"/>(착용선 보고값) · <see cref="ShiftedCopy"/>(사본 변이). 새 계산 방식을
    /// 만들지 않았고, 좌표·상수는 한 개도 이 파일에 베끼지 않았다(단 하나의 예외는 §8-6의
    /// <b>폐기된 옛 값</b> 표인데, 그것은 프로덕션에 <b>더 이상 없는</b> 이력 고정물이고 §8-6이
    /// 「지금 값과 다르다」를 같은 테스트 안에서 대조로 못박는다).</para>
    ///
    /// <para><b>양자화를 어떻게 다뤘나</b>(§14-16-3 — 이 절을 안 읽고 짜면 <b>거짓 빨강</b>이 난다).
    /// 격자 0.004로 내려가며 훑는 자가 돌려주는 착용선은 참값보다 <b>최대 한 격자 크다</b>.
    /// 출하 커버선 5종은 그 <b>보고값을 소수 4자리로 잘라</b> 넣은 값이라, 보고값과 그대로 비교하면
    /// 5종 전부가 <b>1.3e-5 R 차이로 실패</b>한다 — 실제로는 참 착용선보다 +0.0010 ~ +0.0038 R
    /// <b>위</b>인데도 그렇다. 그래서 이 게이트의 본체(§8-2)는 격자를 <b>안 쓴다</b>: 커버선
    /// <b>그 높이에서 덮임 술어를 직접 평가</b>한다. 그것이 「커버선 ≥ 착용선」과 <b>정확히 같은 명제</b>다
    /// (덮임이 꼭대기부터 한 구간이면, 커버선이 그 구간 안에 있다 ⟺ 커버선 ≥ 구간의 밑단).</para>
    ///
    /// <para><b>파이썬 검산기와 대조돼 있다</b>(2026-09-06, <c>design/equipment/verify/r23_crown.py</c>의
    /// 정확 스캔라인 위에서 이 판정 순서를 그대로 재현): 현행 5종 <b>전부 통과</b>(정확 여유
    /// 천모자 +0.0030 · 털모자 +0.0028 · 중절모 +0.0010 · 베레모 +0.0022 · 밀짚모자 +0.0038 R),
    /// 변이 3종(커버선 −0.05 / 모자 +0.20 / 옛 값) <b>전부 적발</b>. 두 구현은 서로 독립이라
    /// 같은 함정에 같이 빠지지 않는다.</para>
    ///
    /// <para><b>왕관은 면제다.</b> <see cref="AccessoryShapeBuilder.HatCoverLocalY"/>가
    /// <see cref="AccessoryShapeBuilder.NothingCovered"/>(+∞)를 돌려준다 — 얹는 물건이라 밑이 뚫려 있고
    /// 머리 모양이 함께 보이는 것이 <b>옳다</b>. 면제를 조건 분기로 적지 않고 <b>프로덕션 값에서
    /// 읽어</b>(§8-1) 「면제가 하나뿐인가」를 존재·부재 대조로 잠근다.</para>
    ///
    /// <para><b>유저 자산 불변.</b> 읽기만 한다. 변이는 <c>double[]</c> 사본과 <b>지역 double</b> 위에서만
    /// 일어나고 <see cref="AccessoryShapeBuilder"/>는 한 줄도 건드리지 않는다.</para>
    /// </summary>
    public sealed partial class AccessoryHatWearLineBandTests
    {
        // ====================================================================
        // 8-0. 상수 — 이 절이 새로 들이는 것은 «정확 스캔 격자» 하나뿐이다
        // ====================================================================

        /// <summary>정확 스캔 격자 — <c>r23_crown.py</c> ④(<c>h2_wear(step=0.0002)</c>)와 같다.
        /// <b>판정에는 안 쓴다</b>(판정은 커버선 그 높이에서 직접 평가한다). 「커버선이 참 착용선보다
        /// 얼마나 위인가」를 <b>로그로 남기는</b> 데만 쓴다 — 그 여유가 얼마나 얇은지
        /// (가장 빠듯한 중절모 +0.0010 R) 사람이 봐야 다음 라운드가 모자를 함부로 안 옮긴다.</summary>
        private const double FineScanStepInR = 0.0002;

        /// <summary>양성 대조에서 커버선을 내리는 폭. 참 착용선까지의 실측 여유(최대 +0.0038 R)보다
        /// <b>한 자릿수 크게</b> 잡는다 — 여유에 딱 붙여 잡으면 설계가 조금만 안전 쪽으로 움직여도
        /// 대조가 거짓 빨강을 낸다.</summary>
        private const double CoverLineDropForControlInR = 0.05;

        /// <summary>양성 대조에서 <b>모자만</b> 올리는 폭. §7의 <c>push</c>와 같은 크기다 —
        /// 오늘 밤 사고(모자는 올라갔는데 커버선이 안 따라옴)의 <b>부호까지 같은</b> 재현이다.</summary>
        private const double HatRiseForControlInR = 0.20;

        // ====================================================================
        // 8-1. 프로덕션 커버선 읽기
        // ====================================================================

        /// <summary>그 모자의 커버선을 <b>머리 중심 기준 R 배수</b>로. 착용선을 재는 좌표계
        /// (<see cref="FrontFillPolygonsInR"/>)와 <b>같은 변환</b>을 쓴다 — 다른 변환을 쓰면 두 숫자가
        /// 서로 다른 자리를 가리키면서 비교되는 것처럼 보인다.</summary>
        private static double CoverLineInR(int item)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float cover = AccessoryShapeBuilder.HatCoverLocalY(item, rig);
            return ((double)cover - rig.HeadCenterY) / rig.HeadRadius;
        }

        private static bool IsCoverExempt(int item)
            => float.IsPositiveInfinity(AccessoryShapeBuilder.HatCoverLocalY(item, Rig()));

        /// <summary>커버선 판정 결과. <b>「못 쟀다」와 「띠가 생겼다」를 형태로 가른다.</b></summary>
        private enum BaldBandVerdict
        {
            Pass,

            /// <summary>앞층 채움 조각이 아예 없다 — 잴 대상이 없다.</summary>
            NoFrontFill,

            /// <summary>★ <b>본 결함.</b> 커버선 그 높이에서 모자가 이미 머리를 안 덮는다
            /// = 커버선이 착용선보다 <b>낮다</b> = 그 사이가 민머리 띠다.</summary>
            UncoveredAtCoverLine,

            /// <summary>커버선 <b>위쪽</b> 어딘가에서 덮임이 끊겼다. 커버선 값은 멀쩡한데
            /// <b>모자가 움직인</b> 경우가 여기다(오늘 밤 사고의 거울상).</summary>
            GapAboveCoverLine,
        }

        // ====================================================================
        // 8-2. 판정 — 격자를 쓰지 않는다(§14-16-3의 양자화 함정)
        // ====================================================================

        /// <summary>「커버선 아래에 민머리 띠가 없는가」.
        ///
        /// <list type="number">
        ///   <item><b>커버선 그 높이에서 정확히 평가한다.</b> 격자 표본이 아니라 <c>y = 커버선</c>에서
        ///         <see cref="ChordCoveredAt"/>를 부른다 — 양자화 오차가 <b>0</b>이다.
        ///         이것이 「커버선 ≥ 착용선」과 같은 명제인 이유는 클래스 문서에 있다.</item>
        ///   <item><b>꼭대기 → 커버선 사이에 빈 칸이 없는가</b>(격자 <see cref="ScanStepInR"/>).
        ///         ①만으로는 「위가 끊기고 아래에서 다시 덮인 모자」를 통과로 읽는다 —
        ///         짝 파일 §5의 <c>NotFromHeadTop</c>과 같은 병이고, 실제로 옛 v1 밀짚모자가 그랬다.</item>
        /// </list>
        /// <para><paramref name="firstGapY"/>는 <b>어디서</b> 끊겼는지다. 실패 메시지에 높이가 없으면
        /// 「어딘가 틀렸다」까지만 알 수 있고, 그 상태로는 커버선을 고칠지 도형을 고칠지 못 정한다.</para></summary>
        private static BaldBandVerdict JudgeBaldBand(List<double[]> polys, double coverInR, out double firstGapY)
        {
            firstGapY = double.NaN;
            if (polys == null || polys.Count == 0) return BaldBandVerdict.NoFrontFill;

            double headR = HeadCoverRadiusInR;
            var scratch = new List<double>(32);

            if (!ChordCoveredAt(polys, coverInR, headR, scratch))
            {
                firstGapY = coverInR;
                return BaldBandVerdict.UncoveredAtCoverLine;
            }

            int steps = (int)Math.Floor((headR - coverInR) / ScanStepInR);
            for (int i = 0; i <= steps; i++)
            {
                double y = headR - i * ScanStepInR;
                if (y <= coverInR) break;
                if (!ChordCoveredAt(polys, y, headR, scratch))
                {
                    firstGapY = y;
                    return BaldBandVerdict.GapAboveCoverLine;
                }
            }
            return BaldBandVerdict.Pass;
        }

        /// <summary>커버선이 <b>참</b> 착용선보다 얼마나 위인가(R 배수). 격자 <see cref="FineScanStepInR"/>로
        /// 커버선에서 아래로 훑어 첫 덮임 구간의 밑단을 찾는다 — <c>r23_crown.py</c> ④와 같은 자다.
        /// <para>판정용이 아니라 <b>로그용</b>이다. 덮이지 않으면 <see cref="double.NaN"/>.</para></summary>
        private static double ExactMarginBelowCoverLine(List<double[]> polys, double coverInR)
        {
            double headR = HeadCoverRadiusInR;
            var scratch = new List<double>(32);
            if (!ChordCoveredAt(polys, coverInR, headR, scratch)) return double.NaN;

            double y = coverInR;
            while (y > ChinLimitInR && ChordCoveredAt(polys, y, headR, scratch)) y -= FineScanStepInR;
            return coverInR - (y + FineScanStepInR);
        }

        // ====================================================================
        // 8-3. 자 교정 — 모자 좌표가 하나도 안 들어간다. 여기가 깨지면 §8 전체가 무효다
        // ====================================================================

        /// <summary>★ <b>손으로 답을 아는 도형</b>으로 커버선 게이트를 교정하고, <b>네 갈래를 전부 낸다</b>.
        ///
        /// <para>실제 모자 6종은 (지금은) 전부 통과라, 나머지 세 갈래가 코드에서 죽어 있어도
        /// <b>6종 검사만으로는 영원히 안 드러난다</b>. 짝 파일 §4 (마)(바)와 같은 처방이다.</para>
        /// <list type="bullet">
        ///   <item>(가) 넓은 판(밑변 +0.30) · 커버선 +0.31 → <b>Pass</b>. 커버선이 판 안이다.</item>
        ///   <item>(나) 같은 판 · 커버선 +0.29 → <b>UncoveredAtCoverLine</b>. 판 밑으로 0.01 R 내려갔다
        ///         = 민머리 띠 0.01 R. <b>이 갈래가 오늘 밤 사고 그 자체다.</b></item>
        ///   <item>(다) 위아래로 갈라진 판(+0.30~+0.60 과 +0.90~+3.00) · 커버선 +0.31 →
        ///         <b>GapAboveCoverLine</b>. 커버선 높이는 덮여 있지만 그 <b>위</b>가 뚫렸다 —
        ///         ①만 보는 자는 이것을 통과로 읽는다.</item>
        ///   <item>(라) 빈 목록 → <b>NoFrontFill</b>. 「대상 없이 돌고 초록」을 형태로 막는다.</item>
        /// </list></summary>
        [Test]
        public void 자_교정_커버선_게이트가_답을_아는_도형에서_네_갈래를_전부_낸다()
        {
            var widePlate = new List<double[]>
            {
                new[] { -2.0, 0.30, 2.0, 0.30, 2.0, 3.0, -2.0, 3.0 },
            };

            Assert.AreEqual(BaldBandVerdict.Pass, JudgeBaldBand(widePlate, 0.31, out _),
                "(가) 밑변 +0.30인 넓은 판에서 커버선 +0.31이 통과가 아닙니다 — 게이트가 " +
                "<b>정상인 모자를 떨어뜨리고</b> 있습니다. 이 상태의 6종 초록/빨강은 둘 다 뜻이 없습니다.");

            Assert.AreEqual(BaldBandVerdict.UncoveredAtCoverLine,
                JudgeBaldBand(widePlate, 0.29, out double gapLow),
                "(나) 커버선을 판 밑변 아래(+0.29)로 내렸는데 민머리 띠로 판정되지 않았습니다 — " +
                "<b>이 게이트의 존재 이유가 죽었습니다.</b> 오늘 밤 사고(커버선이 착용선보다 낮음)를 " +
                "영원히 못 잡습니다.");
            Assert.AreEqual(0.29, gapLow, 1e-9,
                "(나) 끊긴 높이가 커버선 그 자리로 보고되지 않았습니다 — 실패 메시지의 좌표를 " +
                "믿을 수 없게 됩니다.");

            var splitPlate = new List<double[]>
            {
                new[] { -2.0, 0.90, 2.0, 0.90, 2.0, 3.0, -2.0, 3.0 },
                new[] { -2.0, 0.30, 2.0, 0.30, 2.0, 0.60, -2.0, 0.60 },
            };
            Assert.AreEqual(BaldBandVerdict.GapAboveCoverLine,
                JudgeBaldBand(splitPlate, 0.31, out double gapHigh),
                "(다) 커버선 위가 뚫린 도형(+0.60~+0.90 공백)이 통과로 읽혔습니다 — " +
                "커버선 높이 한 점만 보는 자는 「위가 비었는데 아래에서 덮인 모자」를 못 봅니다.");
            Assert.IsTrue(gapHigh > 0.60 && gapHigh < 0.90,
                $"(다) 끊긴 높이가 {gapHigh:F4}로 보고됐습니다 — 공백 구간(+0.60~+0.90) 안이어야 합니다.");

            Assert.AreEqual(BaldBandVerdict.NoFrontFill, JudgeBaldBand(new List<double[]>(), 0.31, out _),
                "(라) 앞층 채움이 0개인데 판정이 통과 쪽으로 흘렀습니다 — " +
                "이것이 이 저장소가 반복해 온 «대상이 비어 아무것도 안 재고 초록»입니다.");
        }

        // ====================================================================
        // 8-4. 본체 — HEAD 6종
        // ====================================================================

        /// <summary>★ <b>면제가 왕관 하나뿐이고, 그 면제가 「잴 것이 없어서」가 아닌가</b>.
        ///
        /// <para>CLAUDE.md: <i>"부재 단언은 썩으면 조용히 초록이 된다"</i>. 「왕관은 예외」라고만 적어 두면
        /// 다른 모자가 실수로 +∞가 돼도(= 머리카락을 하나도 안 자름 → 머리카락이 모자를 뚫고 나온다)
        /// 러너는 계속 초록이다. 그래서 세 방향을 <b>대조로</b> 못박는다.</para>
        /// <list type="number">
        ///   <item><b>존재</b> — 면제는 <b>정확히 1종</b>이고 그것은 왕관이다. 값은 프로덕션 상수
        ///         <see cref="AccessoryShapeBuilder.NothingCovered"/>와 <b>같은 것</b>이어야 한다.</item>
        ///   <item><b>부재</b> — 나머지 다섯은 <b>유한</b>하다. 하나라도 +∞가 되면 그 모자는
        ///         <see cref="ShapeCoverageGuard"/>의 그물(<c>default</c> 갈래)도 안 거치고 조용히
        ///         「왕관 취급」을 받는다.</item>
        ///   <item>★ <b>왕관의 면제는 「선언」이지 「측정 불가」가 아니다</b> — 왕관도 착용선은
        ///         실재한다(실측 +0.4392 R). 면제는 그 사실을 알고도 <b>일부러</b> 안 자르는 선택이다.
        ///         왕관의 착용선이 사라지면(= 앞층 채움이 머리를 아예 안 덮으면) 면제의 뜻도 달라진다.</item>
        /// </list></summary>
        [Test]
        public void 커버선_면제는_왕관_하나뿐이고_나머지_다섯은_유한하다()
        {
            var exempt = new List<int>();
            var report = new StringBuilder();

            for (int i = 0; i < GatedHeadItems.Length; i++)
            {
                int item = GatedHeadItems[i];
                float cover = AccessoryShapeBuilder.HatCoverLocalY(item, Rig());
                bool isExempt = float.IsPositiveInfinity(cover);
                if (isExempt) exempt.Add(item);
                report.AppendLine($"  {Label(item)}: 커버선 " +
                                  (isExempt ? "+∞(면제)" : CoverLineInR(item).ToString("F4", CultureInfo.InvariantCulture) + " R"));
            }

            Debug.Log("[커버선 표]\n" + report);

            // (1) 존재 — 면제는 하나뿐이고 왕관이다.
            Assert.AreEqual(1, exempt.Count,
                $"커버선 면제(+∞)가 {exempt.Count}종입니다. 승인된 면제는 <b>왕관 하나뿐</b>입니다 " +
                "(AccessoryShapeBuilder.HatCoverLocalY의 case HeadCrown — 얹는 물건이라 밑이 뚫려 있다).\n" +
                "0종이면 왕관이 머리카락을 자르기 시작한 것이고, 2종 이상이면 어떤 모자가 " +
                "<b>조용히 왕관 취급</b>을 받고 있는 것입니다(= 머리카락이 모자를 뚫고 나옵니다).");
            Assert.AreEqual(AccessoryShapeBuilder.HeadCrown, exempt[0],
                $"면제받은 것이 왕관이 아니라 {Label(exempt[0])}입니다 — 면제의 주인이 바뀌었습니다.");
            Assert.AreEqual(AccessoryShapeBuilder.NothingCovered,
                AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadCrown, Rig()),
                "왕관의 커버선이 프로덕션 상수 NothingCovered와 다른 값입니다 — " +
                "면제가 «선언»이 아니라 우연한 큰 수가 됐다면 그것은 다른 사실입니다.");

            // (2) 부재 — 나머지 다섯은 유한하다.
            for (int i = 0; i < GatedHeadItems.Length; i++)
            {
                int item = GatedHeadItems[i];
                if (item == AccessoryShapeBuilder.HeadCrown) continue;
                Assert.IsFalse(IsCoverExempt(item),
                    $"{Label(item)}의 커버선이 +∞입니다 — 이 모자는 머리카락을 잘라야 합니다. " +
                    "HatCoverLocalY의 switch에서 이 번호의 case가 사라지면 default로 떨어져 " +
                    "+∞가 되고, 화면에서는 머리카락이 모자를 뚫고 나옵니다.");
            }

            // (3) 왕관의 면제는 «측정 불가»가 아니다 — 착용선은 실재한다.
            HatCoverage crown = Measure(FrontFillPolygonsInR(AccessoryShapeBuilder.HeadCrown, out _));
            Assert.IsTrue(crown.HasFrontFill && crown.Covered,
                "왕관에 앞층 채움 덮임이 없습니다 — 그러면 「덮는데도 일부러 안 자른다」는 면제의 " +
                "전제가 사라집니다. 면제는 <b>측정 불가</b>가 아니라 <b>선택</b>이어야 합니다.");
            Assert.IsFalse(double.IsNaN(crown.WearLineInR),
                "왕관의 착용선이 측정되지 않았습니다.");
            Debug.Log($"[커버선 면제] {Label(AccessoryShapeBuilder.HeadCrown)} — 커버선 +∞ · " +
                      $"착용선 {crown.WearLineInR:F4} R(실재한다). 머리카락을 일부러 남긴다.");
        }

        /// <summary>★★ <b>본체 — 커버선 아래에 민머리 띠가 없다</b>(왕관 제외 5종).
        ///
        /// <para>판정은 커버선 <b>그 높이</b>에서 직접 한다(격자 오차 0). 통과의 뜻은
        /// 「커버선이 착용선 <b>위</b>이거나 같다」이고, 그것이 곧 <i>"머리카락이 잘린 자리를
        /// 모자가 이어받는다"</i>이다.</para>
        ///
        /// <para>여유가 얇다는 것도 함께 로그로 남긴다 — 실측 +0.0010 ~ +0.0038 R. 모자를
        /// <b>한 격자(0.004 R)만 내려도</b> 다섯 중 넷이 이 게이트를 깬다.</para></summary>
        [TestCase(AccessoryShapeBuilder.HeadCap, TestName = "HEAD 야구모자(천모자)")]
        [TestCase(AccessoryShapeBuilder.HeadBeanie, TestName = "HEAD 털모자")]
        [TestCase(AccessoryShapeBuilder.HeadFedora, TestName = "HEAD 중절모")]
        [TestCase(AccessoryShapeBuilder.HeadBeret, TestName = "HEAD 베레모")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, TestName = "HEAD 밀짚모자")]
        public void 커버선_아래에_민머리_띠가_없다(int item)
        {
            Assert.IsFalse(IsCoverExempt(item),
                $"{Label(item)}의 커버선이 +∞라 이 검사가 대상 없이 돌았습니다 — " +
                "면제 목록이 늘어난 것이라면 커버선_면제는_왕관_하나뿐이다가 먼저 말합니다.");

            List<double[]> polys = FrontFillPolygonsInR(item, out int totalPieces);
            Assert.Greater(totalPieces, 0, $"{Label(item)}가 조각을 하나도 안 만듭니다.");

            double cover = CoverLineInR(item);
            BaldBandVerdict verdict = JudgeBaldBand(polys, cover, out double gapY);
            double reportedWear = Measure(polys).WearLineInR;
            double exactMargin = ExactMarginBelowCoverLine(polys, cover);

            Debug.Log($"[커버선] {Label(item)} 커버선 {cover:F4} R · 착용선 보고값 {reportedWear:F4} R" +
                      $"(격자 {ScanStepInR}) · 참 착용선까지 여유 " +
                      (double.IsNaN(exactMargin) ? "—" : $"{exactMargin:+0.0000;-0.0000} R") +
                      $" · 앞층 채움 {polys.Count}개 · 판정 {verdict}.");

            Assert.AreEqual(BaldBandVerdict.Pass, verdict,
                $"{Label(item)}에 <b>머리카락도 모자도 없는 민머리 띠</b>가 생깁니다.\n" +
                $"  커버선 = {cover:F4} R · 착용선 보고값 = {reportedWear:F4} R · " +
                $"끊긴 높이 = {gapY:F4} R · 판정 = {verdict}\n" +
                "  · UncoveredAtCoverLine = 커버선이 착용선보다 <b>낮다</b>. 머리카락은 커버선 위가 " +
                "잘려 나가는데 모자는 거기까지 안 내려와, 그 사이가 그대로 맨머리로 보입니다.\n" +
                "  · GapAboveCoverLine = 커버선은 멀쩡한데 <b>모자가 움직였다</b>(커버선 위가 끊겼다).\n" +
                "  고치는 순서: (1) 모자 도형을 의도적으로 옮겼다면 그 모자의 커버선 상수를 " +
                "<b>새 착용선으로 함께 올린다</b> — 오늘 밤 사고가 정확히 이 한 걸음을 빠뜨린 것입니다.\n" +
                "  (2) 도형을 안 건드렸는데 이게 떴다면 커버선 상수가 낮춰진 것입니다. " +
                "docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §14-16-5-a: <b>«낮추는 방향으로는 건드리지 마라»</b>.\n" +
                "  참고: 통과선을 이 파일에서 낮추는 것은 고치는 것이 아닙니다 — " +
                "이 판정에는 여유 상수가 <b>없습니다</b>(커버선 그 높이에서 직접 잽니다).");

            Assert.IsFalse(double.IsNaN(exactMargin),
                $"{Label(item)}의 참 착용선까지의 여유를 못 쟀습니다 — 위 판정이 Pass인데 " +
                "여유가 NaN이면 두 자가 서로 다른 것을 보고 있다는 뜻입니다.");
            Assert.GreaterOrEqual(exactMargin, 0.0,
                $"{Label(item)}의 여유가 {exactMargin:F4} R로 음수입니다 — 판정과 로그가 어긋났습니다.");
        }

        /// <summary>「<c>HatCoverLocalY(모자) ≥ 그 모자의 H-2 착용선</c>」의 <b>글자 그대로</b>인 형태.
        ///
        /// <para>위 §8-4가 정본 판정이고 이쪽은 <b>같은 사실의 읽기 쉬운 형태</b>다. 두 자
        /// (커버선 상수 ↔ 착용선 보고값)가 <b>같은 자리를 가리키는지</b>도 여기서 확인된다 —
        /// 어긋나면 둘 중 하나가 딴 것을 보고 있다.</para>
        ///
        /// <para>★ <b>보고값을 그대로 쓰면 안 된다</b>(§14-16-3). 내려가며 훑는 자의 착용선은 참값보다
        /// <b>최대 한 격자 크다</b>. 출하 5종의 커버선은 그 보고값을 소수 4자리로 <b>자른</b> 값이라
        /// 보고값보다 정확히 <c>1.3e-5 R</c> 작다 — 보정 없이 <c>커버선 ≥ 보고값</c>을 걸면
        /// <b>5종 전부 거짓 빨강</b>이 난다(실제로는 참 착용선보다 +0.0010~+0.0038 R 위인데도).
        /// 그래서 하한을 <c>보고값 − 격자</c>로 둔다. 그 한 격자만큼은 이 검사가 봐주는 것이고,
        /// 봐주지 않는 정확한 판정이 §8-4다.</para></summary>
        [TestCase(AccessoryShapeBuilder.HeadCap, TestName = "HEAD 야구모자(천모자)")]
        [TestCase(AccessoryShapeBuilder.HeadBeanie, TestName = "HEAD 털모자")]
        [TestCase(AccessoryShapeBuilder.HeadFedora, TestName = "HEAD 중절모")]
        [TestCase(AccessoryShapeBuilder.HeadBeret, TestName = "HEAD 베레모")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, TestName = "HEAD 밀짚모자")]
        public void 커버선이_H2_착용선_보고값에서_한_격자_넘게_내려가지_않았다(int item)
        {
            List<double[]> polys = FrontFillPolygonsInR(item, out _);
            HatCoverage m = Measure(polys);
            double cover = CoverLineInR(item);

            Assert.IsTrue(m.HasFrontFill && m.Covered,
                $"{Label(item)}의 착용선을 못 쟀습니다 — 비교 대상이 없는데 통과로 흘러가면 안 됩니다.");

            Assert.Greater(cover, m.WearLineInR - ScanStepInR,
                $"{Label(item)}의 커버선 {cover:F6} R이 H-2 착용선 보고값 {m.WearLineInR:F6} R에서 " +
                $"격자({ScanStepInR}) 넘게 내려갔습니다 — 그 차이가 <b>민머리 띠의 높이</b>입니다 " +
                $"({m.WearLineInR - cover:F4} R ≈ 획 {(m.WearLineInR - cover) / AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii:F2}개).\n" +
                "  커버선은 머리카락을 자르는 선이고 착용선은 모자가 덮기를 멈추는 선입니다. " +
                "전자가 후자보다 낮으면 그 사이에 아무것도 없습니다.\n" +
                "  docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §14-16-5-a — <b>«낮추는 방향으로는 건드리지 마라»</b>.");

            // 반대쪽은 «안전한 방향»이라 실패시키지 않는다 — 다만 벌어지면 보이게 남긴다.
            // (커버선이 착용선보다 훨씬 높으면 머리카락이 모자 채움 뒤에 숨을 뿐 화면 결함이 아니다.)
            double drift = cover - m.WearLineInR;
            if (drift > ScanStepInR)
            {
                Debug.Log($"[커버선 ↑드리프트] {Label(item)} 커버선이 착용선 보고값보다 {drift:F4} R 위입니다 — " +
                          "안전한 방향이라 실패는 아니지만, 머리카락이 그만큼 더 잘려 모자 뒤에 숨습니다. " +
                          "의도한 것인지 design-equipment가 한 번 봐야 합니다.");
            }
        }

        // ====================================================================
        // 8-5. 양성 대조 — 자가 죽으면 여기가 먼저 빨개진다
        // ====================================================================

        /// <summary>★ <b>커버선만 내리면 실제로 빨개지는가</b>(5종 전부, 매 실행).
        ///
        /// <para>좌표가 그대로인 한 §8-4는 <b>자가 죽어도 초록</b>이다. 여기서 커버선 값을
        /// <b>지역 변수에서만</b> <see cref="CoverLineDropForControlInR"/>만큼 내려 판정이 실제로
        /// 뒤집히는지 매번 증명한다. 프로덕션 상수는 읽기만 한다.</para>
        ///
        /// <para>사유까지 못박는다 — 커버선을 내리면 <b>커버선 그 높이</b>에서 먼저 걸리므로
        /// 반드시 <see cref="BaldBandVerdict.UncoveredAtCoverLine"/>다(파이썬 교차검산에서도 5종 전부
        /// 그 사유였다). 다른 사유가 나오면 판정 순서가 바뀐 것이다.</para>
        ///
        /// <para>★ <b>이 테스트가 7번째 모자의 안전망이기도 하다.</b> 위 <c>[TestCase]</c> 목록은
        /// 사람이 손으로 채우는 표라 새 모자가 들어와도 <b>조용히 5종만</b> 볼 수 있다. 여기는
        /// <see cref="GatedHeadItems"/>를 <b>돌면서</b> 면제가 아닌 전부에 대해 「손대지 않은 상태는
        /// 통과」를 요구하므로, 표를 못 채운 새 모자도 <b>이 자리에서는</b> 반드시 재어진다
        /// (그리고 <c>게이트_표가_HEAD_카탈로그_전부를_덮는다</c>가 표 자체의 누락을 따로 잡는다).</para></summary>
        [Test]
        public void 양성_대조_커버선만_내리면_민머리_띠가_잡힌다()
        {
            var report = new StringBuilder();

            for (int i = 0; i < GatedHeadItems.Length; i++)
            {
                int item = GatedHeadItems[i];
                if (item == AccessoryShapeBuilder.HeadCrown) continue;

                List<double[]> polys = FrontFillPolygonsInR(item, out _);
                double cover = CoverLineInR(item);

                BaldBandVerdict asIs = JudgeBaldBand(polys, cover, out _);
                BaldBandVerdict dropped =
                    JudgeBaldBand(polys, cover - CoverLineDropForControlInR, out double gapY);

                report.AppendLine($"  {Label(item)}: 그대로={asIs} · 커버선 −{CoverLineDropForControlInR:F2}R=" +
                                  $"{dropped}(끊긴 높이 {gapY:F4})");

                Assert.AreEqual(BaldBandVerdict.Pass, asIs,
                    $"{Label(item)}가 손대지 않은 상태에서 이미 실패입니다 — " +
                    "양성 대조 전에 그 회귀부터 보십시오(커버선_아래에_민머리_띠가_없다가 자세히 말합니다).");

                Assert.AreEqual(BaldBandVerdict.UncoveredAtCoverLine, dropped,
                    $"{Label(item)}의 커버선을 <b>{CoverLineDropForControlInR:F2} R 내렸는데</b> 판정이 " +
                    $"{dropped}입니다 — UncoveredAtCoverLine이어야 합니다.\n" +
                    "Pass가 나왔다면 이 게이트는 커버선을 <b>안 보고 있습니다</b> — 위 5종의 초록은 전부 무효입니다.\n" +
                    "(실측 여유는 +0.0010~+0.0038 R이라 이 폭이면 다섯 모두 반드시 깨집니다.)");
            }

            Debug.Log($"[커버선 양성 대조 −{CoverLineDropForControlInR:F2} R]\n" + report);
        }

        /// <summary>★★ <b>오늘 밤 사고의 형태 그대로</b> — 모자만 올리고 커버선을 두면 잡히는가.
        ///
        /// <para>R25는 모자 6종을 통째로 <b>올렸고</b> 커버선 상수는 <b>안 따라갔다</b>. 여기서
        /// 같은 일을 사본 위에서 재현한다: 앞층 채움 좌표를 <see cref="HatRiseForControlInR"/>만큼
        /// 올리고 커버선은 프로덕션 값 그대로 둔다. 판정은 반드시 뒤집혀야 한다.</para>
        ///
        /// <para><b>사유는 못박지 않는다.</b> 짝 파일 §7이 남긴 교훈 — 평행이동한 도형의 «어디서 끊기는가»는
        /// 모자마다 다르고, 사유를 못박았던 첫 판이 3종에서 <b>거짓 빨강</b>을 냈다. 여기서는
        /// 「통과가 아니다」만 요구하고 사유는 로그로 남긴다(파이썬 교차검산에서는 5종 전부
        /// UncoveredAtCoverLine이었지만, 그 사실에 계약을 걸지 않는다).</para></summary>
        [Test]
        public void 양성_대조_모자만_올리고_커버선을_두면_민머리_띠가_잡힌다()
        {
            var report = new StringBuilder();

            for (int i = 0; i < GatedHeadItems.Length; i++)
            {
                int item = GatedHeadItems[i];
                if (item == AccessoryShapeBuilder.HeadCrown) continue;

                List<double[]> polys = FrontFillPolygonsInR(item, out _);
                double cover = CoverLineInR(item);

                BaldBandVerdict risen =
                    JudgeBaldBand(ShiftedCopy(polys, +HatRiseForControlInR), cover, out double gapY);

                report.AppendLine($"  {Label(item)}: 모자만 +{HatRiseForControlInR:F2}R → {risen}" +
                                  $"(끊긴 높이 {gapY:F4}, 커버선 {cover:F4})");

                Assert.AreNotEqual(BaldBandVerdict.Pass, risen,
                    $"{Label(item)}를 <b>{HatRiseForControlInR:F2} R 올렸는데</b> 커버선은 그대로인 채로 " +
                    "판정이 통과입니다.\n" +
                    "이것이 <b>오늘 밤 실제로 난 사고의 형태</b>입니다 — R25가 모자를 올리고 커버선을 " +
                    "안 올려 천모자에 0.2410 R짜리 민머리 띠가 생겼습니다. 이 대조가 통과로 읽히면 " +
                    "같은 사고가 다시 나도 러너는 <b>조용히 초록</b>입니다.");
            }

            Debug.Log($"[커버선 양성 대조 · 모자만 +{HatRiseForControlInR:F2} R]\n" + report);
        }

        // ====================================================================
        // 8-6. 회귀 — 2026-09-06 R25 사고 재현
        // ====================================================================

        /// <summary>★ <b>사고 당시의 커버선 값을 지금 도형에 대 보면 전부 민머리 띠인가</b>.
        ///
        /// <para>아래 다섯 숫자는 <b>프로덕션 상수가 아니다</b> — R25c가 지운 <b>옛 값</b>이고
        /// (<c>git show HEAD:…/AccessoryShapeBuilder.cs</c>), 그래서 여기 적혀 있어도
        /// 「프로덕션 상수를 테스트에 베끼지 마라」에 걸리지 않는다. 이력 고정물이다.</para>
        ///
        /// <para>다만 이력 고정물은 <b>썩는다</b> — 언젠가 프로덕션이 우연히 같은 값이 되면 이 테스트는
        /// 「사고가 재현된다」가 아니라 「지금이 사고다」를 뜻하게 된다. 그래서 <b>같은 테스트 안에서</b>
        /// 지금 값이 옛 값과 다르다는 것을 함께 못박는다(CLAUDE.md의 존재·부재 대조).</para></summary>
        [Test]
        public void 회귀_R25_이전_옛_커버선_값은_지금_도형에서_전부_민머리_띠다()
        {
            // 옛 값(R25 이전). 지금 프로덕션에는 없다 — 아래 (2)가 그 사실을 매번 확인한다.
            var historical = new Dictionary<int, double>
            {
                { AccessoryShapeBuilder.HeadCap, 0.06 },      // 옛 HatBrimLineRatio
                { AccessoryShapeBuilder.HeadBeanie, -0.06 },  // 옛 BeanieBandTopRatio
                { AccessoryShapeBuilder.HeadFedora, 0.08 },   // 옛 FedoraBrimLineRatio
                { AccessoryShapeBuilder.HeadBeret, 0.02 },    // 옛 BeretBrimLineRatio
                { AccessoryShapeBuilder.HeadStraw, 0.08 },    // 옛 StrawBrimLineRatio
            };

            var report = new StringBuilder();
            double stroke = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;

            foreach (KeyValuePair<int, double> pair in historical)
            {
                int item = pair.Key;
                double old = pair.Value;

                List<double[]> polys = FrontFillPolygonsInR(item, out _);
                double now = CoverLineInR(item);
                double wear = Measure(polys).WearLineInR;

                BaldBandVerdict verdict = JudgeBaldBand(polys, old, out _);
                report.AppendLine($"  {Label(item)}: 옛 커버선 {old:+0.0000;-0.0000} → {verdict} " +
                                  $"(띠 높이 {wear - old:F4} R ≈ 획 {(wear - old) / stroke:F2}개) · " +
                                  $"지금 {now:F4}");

                // (1) 존재 — 옛 값은 지금 도형에서 «반드시» 민머리 띠다.
                Assert.AreNotEqual(BaldBandVerdict.Pass, verdict,
                    $"{Label(item)}에 <b>사고 당시의 커버선 {old:F4} R</b>을 넣었는데 판정이 통과입니다.\n" +
                    "그 값은 실제로 화면에 민머리 띠를 만들었던 값입니다(천모자 기준 0.2410 R). " +
                    "이 대조가 통과로 읽히면 이 게이트는 <b>이미 일어난 사고조차</b> 못 잡는다는 뜻입니다.");

                // (2) 부재 — 지금 값은 그 옛 값이 아니다(이력 고정물이 «현재»로 둔갑하지 않게).
                Assert.Greater(Math.Abs(now - old), ScanStepInR,
                    $"{Label(item)}의 <b>지금</b> 커버선 {now:F4} R이 사고 당시 값 {old:F4} R과 " +
                    "격자 안에서 같습니다 — 이력 고정물이 아니라 <b>현재 상태</b>가 됐습니다. " +
                    "이 테스트가 아니라 커버선_아래에_민머리_띠가_없다를 먼저 보십시오(회귀가 났습니다).");
            }

            Assert.AreEqual(GatedHeadItems.Length - 1, historical.Count,
                $"이력 표가 {historical.Count}종인데 면제(왕관)를 뺀 게이트 대상은 " +
                $"{GatedHeadItems.Length - 1}종입니다 — 새 모자가 들어왔다면 이 회귀 표에도 " +
                "그 모자의 «사고 당시 값»을 넣거나, 넣을 이력이 없다는 것을 여기서 밝혀야 합니다.");

            Debug.Log("[회귀 · R25 이전 커버선]\n" + report);
        }
    }
}
