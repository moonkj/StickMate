using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>「어느 자로 잴 것인가」의 유일한 판정처</b> — 2026-09-03, 스펙 14-1(A군 5종 띠가
    /// 낱선 → 닫힌 채움 띠) 후속.
    ///
    /// ============================================================================
    /// 왜 자를 바꾸는가 — 빨강이 불편해서가 아니라 <b>도형의 범주가 바뀌었기 때문</b>이다
    /// ============================================================================
    /// <see cref="StickConfig.MinStrokeScreenPoints"/>(2.00pt)의 정의는 그 문서가 직접 못 박는다:
    /// <i>"그 선이 <b>그 자리의 유일한 잉크</b>인 획"의 하한</i>이고, 적용 대상은 <b>낱선</b>이며
    /// <b>비적용</b>은 <i>"채운 도형의 윤곽선"</i>이다. 채움의 윤곽선은
    /// <see cref="StickConfig.MinFillOutlineScreenPoints"/>(1.00pt)를 쓴다.
    ///
    /// <para>A군 5종(중절모·밀짚모자 띠 / 왕관 테 / 베레모 테 / 바가지 앞머리)은 2026-09-03에
    /// <c>loop=false, filled=false</c>에서 <c>loop=true, filled=true</c>가 됐다. 즉 <b>범주가
    /// 실제로 바뀌었다</b>. 그런데 이 어셈블리의 규칙 4 검사들은 여전히
    /// <see cref="AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii"/>(낱선 자, 0.3439 R)로 재고
    /// 있었고, 그 자로는 띠 두께 0.46 R이 <b>1.34획</b>으로 읽혀 "0 &lt; 간격 &lt; 1.5획"의 금지
    /// 구간에 들어간다. 채움 윤곽선 자(0.21818 R)로 재면 <b>2.11획</b>이다.</para>
    ///
    /// <para><b>그래서 자는 도형이 스스로 고른다</b> — <see cref="PenInR"/>의 분기 근거는
    /// <see cref="AccessoryShapeBuilder.Shape.Filled"/> 하나뿐이고, 이것은 프로덕션
    /// <c>CharacterAccessoryRenderer.AddShape</c>가 실제 펜 두께를 가르는 근거와 <b>같은 하나</b>다
    /// (<c>AddLine(..., isFillOutline: shape.Filled)</c>). 누가 <c>filled: true</c>를 지우면
    /// 자도 자동으로 낱선 쪽으로 되돌아가고, 그러면 지금 좌표는 <b>즉시 빨개진다</b> —
    /// 자를 느슨하게 바꾼 것이 조용한 초록으로 남지 않는다.</para>
    ///
    /// ============================================================================
    /// ★ 두 OS가 이 자에 어떻게 물리는가 (CLAUDE.md 플랫폼 동시 검토)
    /// ============================================================================
    /// 1.00pt 하한의 근거는 <b>Windows 표시배율 100%</b>(1pt = 1 물리픽셀)이고 macOS Retina에서는
    /// 1pt = 2 물리픽셀이다. 즉 <b>같은 자가 두 OS에서 다른 물리적 여유</b>를 뜻한다.
    /// <para>그런데 <b>출하 배율(0.75)에서는 두 하한 중 채움 쪽이 아예 안 물린다</b> — 비례 획
    /// 0.048 × 0.75 = 0.036이 1.00pt(0.02837)보다 두껍기 때문이다. 그래서 이 라운드의 자 교체는
    /// <b>OS 의존 하한 위에 서 있지 않다</b>. 반대로 <b>낱선 자는 그 배율에서 하한에 눌린다</b>
    /// (2.00pt = 0.05674 &gt; 0.036) — 즉 옛 자는 <i>"OS에서 선이 보이려면"</i>이라는 조건을
    /// <b>선이 아닌 것</b>에 적용하고 있었다. 이 사실을
    /// <see cref="AccessoryFilledBandRulerTests.출하_배율에서_채움_자는_OS_하한에_눌리지_않는다"/>가
    /// 매 실행 다시 잰다.</para>
    /// </summary>
    internal static class AccessoryFilledBandRuler
    {
        internal const string LogPrefix = "[띠자]";

        /// <summary>규칙 4의 "확실히 떨어졌다" 문턱(획 배수). 이 어셈블리의 기존 검사들과 같은 값.</summary>
        internal const float SeparationStrokes = 1.5f;

        /// <summary>규칙 4의 "겹쳤다" 문턱(R 배수). 기존 검사들과 같은 값.</summary>
        internal const float CoincidenceInR = 1e-4f;

        /// <summary>월드 유닛 허용오차. float32 누적 오차(≈3e-7)보다 두 자릿수 크고,
        /// 어떤 실제 좌표 차이(최소 0.0618 유닛)보다 세 자릿수 작다.</summary>
        internal const float FormToleranceWorld = 1e-5f;

        /// <summary>출하 배율에서 <b>낱선</b>이 실제로 그리는 폭(R 배수) — 2.00pt 하한.</summary>
        internal static float StrokePenInR => AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;

        /// <summary>출하 배율에서 <b>채움의 윤곽선</b>이 실제로 그리는 폭(R 배수) — 1.00pt 하한.</summary>
        internal static float FillOutlinePenInR =>
            AccessoryShapeBuilder.FillOutlineBudgetInHeadRadii(AccessoryShapeBuilder.ShippingCharacterScale);

        /// <summary>★ 그 도형이 화면에서 <b>실제로 쓰는</b> 펜. 분기 근거는 <c>Filled</c> 하나 —
        /// 프로덕션 렌더러가 두께를 가르는 근거와 같은 하나다.</summary>
        internal static float PenInR(in AccessoryShapeBuilder.Shape shape)
            => shape.Filled ? FillOutlinePenInR : StrokePenInR;

        /// <summary>규칙 4 — 겹치거나(간격 0) 확실히 떨어졌거나(≥ 1.5펜). 그 사이가 "최악"이다.</summary>
        internal static bool PassesRuleFour(float gapInR, float penInR)
            => gapInR < CoincidenceInR || gapInR >= penInR * SeparationStrokes;

        /// <summary>
        /// 「올린 띠」의 <b>아랫변</b> = 점 배열의 앞 절반. <b>개수를 숫자로 적지 않는다</b> —
        /// <see cref="AssertRaisedBandForm"/>이 잠그는 규약("아랫변 + 역순 윗변")에서 유도한다.
        /// <para>이것을 잘라 재야 "이음매가 겹쳤는가"를 볼 수 있다. 도형 전체로 재면
        /// <see cref="AccessorySilhouetteMetrics.MaxGapToShape"/>가 <b>윗변</b>까지 훑어
        /// 최댓값이 곧 띠 두께가 되고, 아랫변이 통째로 떠도 그 사실이 묻힌다.</para>
        /// </summary>
        internal static Vector3[] BottomEdge(in AccessoryShapeBuilder.Shape band)
        {
            Vector3[] p = band.Points;
            Assert.IsNotNull(p, $"{LogPrefix} 띠에 점이 없습니다.");
            Assert.AreEqual(0, p.Length % 2,
                $"{LogPrefix} 띠가 {p.Length}점(홀수)이라 아랫변을 가를 수 없습니다 — " +
                "AssertRaisedBandForm을 먼저 통과시키십시오.");

            int half = p.Length / 2;
            var bottom = new Vector3[half];
            System.Array.Copy(p, 0, bottom, 0, half);
            return bottom;
        }

        internal static List<AccessoryShapeBuilder.Shape> Build(
            in AccessoryShapeBuilder.Rig rig, EquipmentSlot slot, int item)
            => AccessorySilhouetteMetrics.Build(rig, slot, item);

        /// <summary>
        /// ★ <b>「올린 띠」 규약</b>(스펙 14-1) — 닫힌 채움 띠는 <b>아랫변 + 그 아랫변을
        /// <see cref="AccessoryShapeBuilder.AccentBandThicknessRatio"/>만큼 올려 역순으로 이은 윗변</b>이다.
        ///
        /// <para><b>점 수를 숫자로 적지 않는다.</b> 이 규약이 참이면 점 수는 자동으로 짝수이고
        /// <c>i</c>번과 <c>(n−1−i)</c>번이 짝이 된다 — 그 <b>짝의 y 차이가 정확히 띠 두께</b>라는
        /// 것만 재면 되고, 그 두께는 프로덕션 상수에서 온다. 실측으로 A군 5종 전부가 이 법칙을
        /// 만족한다(오차 4e-17, double 모형).</para>
        ///
        /// <param name="slantedTopCorners">윗변 꼭짓점 중 <b>수직 압출이 아닌</b> 것의 수.
        /// 베레모만 1이다 — 몸통 왼쪽 변이 기울어 있어 그 꼭짓점 하나를 변 위로 물리기 때문이고,
        /// 그 사유는 도형 주석에 적혀 있다. <b>이 수가 늘면 실패한다</b>: 누군가 말없이 띠를
        /// 비스듬히 만든 것이므로 "왜"를 먼저 적어야 한다.</param>
        /// </summary>
        internal static void AssertRaisedBandForm(in AccessoryShapeBuilder.Rig rig,
            in AccessoryShapeBuilder.Shape band, string label, int slantedTopCorners = 0)
        {
            Assert.IsTrue(band.Filled,
                $"{LogPrefix} {label}가 <b>채운 도형이 아닙니다</b>. 2026-09-03 스펙 14-1이 이 띠를 " +
                "낱선에서 닫힌 채움으로 바꿨고, 이 어셈블리의 규칙 4 판정은 그 사실 하나로 자를 고릅니다. " +
                "낱선으로 되돌렸다면 <b>좌표도 함께 되돌려야 합니다</b> — 지금 두께 " +
                $"({AccessoryShapeBuilder.AccentBandThicknessRatio:F2}R)는 낱선 자로 재면 " +
                $"{AccessoryShapeBuilder.AccentBandThicknessRatio / StrokePenInR:F2}획이라 " +
                "규칙 4가 '최악'이라고 못박은 금지 구간(0 < 간격 < 1.5획) 안입니다.");

            Assert.IsTrue(band.Loop,
                $"{LogPrefix} {label}가 닫힌 고리가 아닙니다 — 채움은 닫힌 도형에서만 뜻이 있습니다.");

            Vector3[] p = band.Points;
            Assert.IsNotNull(p, $"{LogPrefix} {label}에 점이 없습니다.");
            Assert.GreaterOrEqual(p.Length, 4,
                $"{LogPrefix} {label}가 {p.Length}점입니다 — 띠는 아랫변 2점 + 윗변 2점이 최소입니다.");
            Assert.AreEqual(0, p.Length % 2,
                $"{LogPrefix} {label}가 {p.Length}점(홀수)입니다 — 「아랫변 + 역순 윗변」 규약이면 " +
                "점 수는 반드시 짝수입니다. 홀수라면 이 도형은 더 이상 올린 띠가 아닙니다.");

            float rise = AccessoryShapeBuilder.AccentBandThicknessRatio * rig.HeadRadius;
            int half = p.Length / 2;
            int slanted = 0;
            float worstSlant = 0f;
            float worstRiseError = 0f;

            for (int i = 0; i < half; i++)
            {
                Vector3 lo = p[i];
                Vector3 hi = p[p.Length - 1 - i];

                worstRiseError = Mathf.Max(worstRiseError, Mathf.Abs(hi.y - lo.y - rise));
                Assert.AreEqual(lo.y + rise, hi.y, FormToleranceWorld,
                    $"{LogPrefix} {label}의 {i}번 아랫변 점과 짝인 {p.Length - 1 - i}번 윗변 점의 y 차이가 " +
                    $"{(hi.y - lo.y) / rig.HeadRadius:F4}R입니다 — 띠 두께 " +
                    $"({AccessoryShapeBuilder.AccentBandThicknessRatio:F2}R)와 달라졌습니다. " +
                    "윗변 좌표를 손으로 새로 적으면 아랫변을 고치는 날 띠만 옛 자리에 남습니다.");

                float dx = Mathf.Abs(hi.x - lo.x);
                if (dx > FormToleranceWorld)
                {
                    slanted++;
                    worstSlant = Mathf.Max(worstSlant, dx);
                }
            }

            Assert.AreEqual(slantedTopCorners, slanted,
                $"{LogPrefix} {label}의 윗변 꼭짓점 중 <b>수직 압출이 아닌</b> 것이 {slanted}개입니다" +
                $"(기대 {slantedTopCorners}개, 최대 이탈 {worstSlant / rig.HeadRadius:F4}R). " +
                "기울인 꼭짓점은 몸통 변 위로 물려야 하는 자리에서만 정당합니다(베레모 1개) — " +
                "늘었다면 그 사유를 도형 주석과 이 인자에 함께 적으십시오.");

            Debug.Log($"{LogPrefix} {label}: {p.Length}점(아랫변 {half} + 윗변 {half}), " +
                $"두께 {AccessoryShapeBuilder.AccentBandThicknessRatio:F2}R, " +
                $"y 법칙 최대 오차 {worstRiseError:E2} 유닛, 기울인 꼭짓점 {slanted}개.");
        }
    }

    /// <summary>
    /// ★ <b>자 그 자체의 대조</b> — 위 판정처가 실제로 두 자를 가르는지, 그리고 <b>자를 잘못 대면
    /// 금지 구간이 조용히 통과하는지</b>를 같은 스위트 안에서 증명한다.
    ///
    /// <para>이 파일이 하는 일은 "빨강을 없애는 것"이라 <b>양성 대조 없이는 아무것도 증명하지
    /// 못한다</b>. 느슨한 자로 바꿨을 때 초록이 되는 것은 당연하다 — 물어야 할 것은
    /// <i>"그 느슨함이 실제로 무엇을 통과시키는가"</i>이고, 아래 두 번째 검사가 그 답을 숫자로 낸다.</para>
    /// </summary>
    public sealed class AccessoryFilledBandRulerTests
    {
        private const string LogPrefix = AccessoryFilledBandRuler.LogPrefix;

        private static AccessoryShapeBuilder.Rig Rig() => AccessorySilhouetteMetrics.Rig();

        [Test]
        public void 두_자는_실제로_다르고_채움_쪽이_더_얇다()
        {
            float line = AccessoryFilledBandRuler.StrokePenInR;
            float fill = AccessoryFilledBandRuler.FillOutlinePenInR;

            Assert.Less(fill, line,
                $"{LogPrefix} 채움 윤곽선 자({fill:F5}R)가 낱선 자({line:F5}R)보다 얇지 않습니다 — " +
                "M6(두 하한 분리)이 무효화됐습니다. 두 자가 같아지면 이 파일의 모든 분기가 " +
                "아무 뜻도 갖지 않고, A군 5종 띠는 다시 규칙 4 금지 구간으로 돌아갑니다.");

            // ★ 두 자의 관계를 프로덕션 하한에서 <b>유도</b>해 다시 확인한다(숫자를 베끼지 않는다).
            Assert.Less(StickConfig.MinFillOutlineScreenPoints, StickConfig.MinStrokeScreenPoints,
                $"{LogPrefix} 하한 자체가 뒤집혔습니다 — 위 부등식이 우연히 성립했을 수 있습니다.");

            Debug.Log($"{LogPrefix} 출하 배율 {AccessoryShapeBuilder.ShippingCharacterScale:F2} — " +
                $"낱선 자 {line:F5}R({StickConfig.MinStrokeScreenPoints:F2}pt 하한) / " +
                $"채움 윤곽선 자 {fill:F5}R({StickConfig.MinFillOutlineScreenPoints:F2}pt 하한). " +
                $"1.5획 문턱: {line * AccessoryFilledBandRuler.SeparationStrokes:F5}R vs " +
                $"{fill * AccessoryFilledBandRuler.SeparationStrokes:F5}R.");
        }

        /// <summary>
        /// ★★ <b>OS 판정</b> — 1.00pt 하한의 근거는 Windows 100%(1pt = 1 물리픽셀)이고 macOS
        /// Retina는 2 물리픽셀이다. 그 차이가 이 라운드의 자 교체에 <b>물리는지</b>를 직접 잰다.
        ///
        /// <para>결론은 "안 물린다": 출하 배율에서 채움 자는 비례 획 그 자체라 하한이 개입하지
        /// 않는다. 반대로 <b>낱선 자는 그 배율에서 하한에 눌린다</b> — 즉 옛 자는
        /// <i>"OS에서 선이 보이려면"</i>이라는 조건이었고, 그것을 <b>선이 아닌 것</b>에 대고 있었다.</para>
        /// </summary>
        [Test]
        public void 출하_배율에서_채움_자는_OS_하한에_눌리지_않는다()
        {
            float shipping = AccessoryShapeBuilder.ShippingCharacterScale;

            // 하한을 뺀 <b>순수 비례</b> 값 — 배율이 약분되므로 배율에 무관하다.
            float proportional = AccessoryShapeBuilder.BaselineStrokeWidth
                                 / AccessoryShapeBuilder.BaselineHeadVisualRadius;

            Assert.AreEqual(proportional, AccessoryFilledBandRuler.FillOutlinePenInR, 1e-6f,
                $"{LogPrefix} 출하 배율({shipping:F2})에서 채움 윤곽선 자가 순수 비례값" +
                $"({proportional:F5}R)이 아닙니다 — 1.00pt 하한이 물리기 시작했습니다. " +
                "그렇다면 이 라운드의 자 교체는 <b>OS 픽셀 밀도에 의존</b>하게 되고, " +
                "Windows 100%(1pt = 1물리픽셀)와 macOS Retina(2물리픽셀)의 판정이 갈립니다. " +
                "그때는 두 OS에서 각각 실기 캡처로 다시 판정해야 합니다.");

            Assert.Greater(AccessoryFilledBandRuler.StrokePenInR, proportional + 1e-6f,
                $"{LogPrefix} 낱선 자가 순수 비례값과 같습니다 — 2.00pt 하한이 이 배율에서 " +
                "안 물린다는 뜻이고, 그렇다면 '옛 자는 OS 가시성 하한이었다'는 이 라운드의 " +
                "판정 근거가 사라집니다. 근거가 바뀌었으므로 자 교체를 다시 판정하십시오.");

            // 하한이 물리기 시작하는 배율 — 게이트 배율(0.60)과의 여유를 <b>숫자로</b> 남긴다.
            float crossover = StickConfig.MinFillOutlineScreenPoints
                / (StickConfig.ReferencePointsPerWorldUnitApprox * AccessoryShapeBuilder.BaselineStrokeWidth);
            Assert.Less(crossover, shipping,
                $"{LogPrefix} 채움 하한이 물리기 시작하는 배율({crossover:F4})이 출하 배율" +
                $"({shipping:F2}) 위입니다 — 위 단언과 모순이므로 계산이 깨졌습니다.");

            Debug.Log($"{LogPrefix} OS 판정 — 채움 자는 배율 {crossover:F4} <b>아래</b>에서만 " +
                $"1.00pt 하한에 눌린다(굽기 근사 {StickConfig.ReferencePointsPerWorldUnitApprox:F4}pt/유닛 기준). " +
                $"출하 {shipping:F2} · 사용자 저장 0.60은 그 위라 하한 무관 = <b>두 OS 동일 판정</b>. " +
                $"낱선 자는 같은 배율에서 하한에 눌린다({AccessoryFilledBandRuler.StrokePenInR:F5}R > " +
                $"비례 {proportional:F5}R) — 그것이 '옛 자는 OS 가시성 조건'이라는 뜻이다. " +
                "다이얼 최소 구간에서는 채움 자도 하한에 눌리고, 거기서는 Windows 100%가 " +
                "1물리픽셀 / macOS Retina가 2물리픽셀이라 실제 여유가 두 배 다르다.");
        }

        /// <summary>
        /// ★★ <b>양성 대조</b> — 자를 <b>잘못</b> 대면 무엇이 통과하는가.
        /// <para>두 자의 1.5획 문턱 사이 구간(<c>[1.5·채움자, 1.5·낱선자)</c>)에 있는 간격은
        /// <b>낱선이라면 금지 구간</b>인데 <b>채움 자로는 통과</b>한다. 그 구간이 실재함을 여기서
        /// 보이지 않으면, 이 라운드의 자 교체는 "무엇을 통과시키는지 모르는 완화"가 된다.</para>
        /// </summary>
        [Test]
        public void 양성대조_낱선에_채움_자를_대면_금지구간이_조용히_통과한다()
        {
            float lineThreshold = AccessoryFilledBandRuler.StrokePenInR * AccessoryFilledBandRuler.SeparationStrokes;
            float fillThreshold = AccessoryFilledBandRuler.FillOutlinePenInR * AccessoryFilledBandRuler.SeparationStrokes;

            Assert.Less(fillThreshold, lineThreshold,
                $"{LogPrefix} 두 문턱이 같습니다 — 아래 대조가 공허합니다(잴 구간이 없습니다).");

            // 창 한가운데를 <b>유도</b>한다. 숫자를 적으면 상수가 움직일 때 창 밖으로 나간다.
            float probe = (fillThreshold + lineThreshold) * 0.5f;
            Assert.GreaterOrEqual(probe, fillThreshold, $"{LogPrefix} 탐침이 창 아래로 나갔습니다.");
            Assert.Less(probe, lineThreshold, $"{LogPrefix} 탐침이 창 위로 나갔습니다.");

            Assert.IsFalse(
                AccessoryFilledBandRuler.PassesRuleFour(probe, AccessoryFilledBandRuler.StrokePenInR),
                $"{LogPrefix} 간격 {probe:F5}R을 <b>낱선 자</b>가 통과시켰습니다 — 옛 자가 이 구간을 " +
                "금지하지 않는다면, 자를 바꿔 초록이 된 것이 '범주가 바뀌어서'가 아니라 " +
                "'애초에 아무것도 안 막고 있어서'가 됩니다.");

            Assert.IsTrue(
                AccessoryFilledBandRuler.PassesRuleFour(probe, AccessoryFilledBandRuler.FillOutlinePenInR),
                $"{LogPrefix} 간격 {probe:F5}R을 <b>채움 자</b>도 막았습니다 — 두 자의 차이가 " +
                "실제로는 이 구간을 만들지 못한다는 뜻이고, 그렇다면 A군 5종이 초록이 된 이유가 " +
                "자 교체가 아니라 다른 무엇입니다. 그것을 먼저 찾으십시오.");

            Debug.Log($"{LogPrefix} 양성 대조 — 자를 잘못 대면 통과하는 간격 구간 = " +
                $"[{fillThreshold:F5}R, {lineThreshold:F5}R). 폭 {(lineThreshold - fillThreshold):F5}R " +
                $"= 낱선 획의 {(lineThreshold - fillThreshold) / AccessoryFilledBandRuler.StrokePenInR:F2}배. " +
                "채움 도형에 이 자를 대는 것은 옳지만, <b>낱선에 대면 이만큼이 조용히 새어 나간다</b>.");
        }

        /// <summary>
        /// ★ 자 선택이 <b>오직 <c>Filled</c> 하나</b>로 갈리는가 — 실제 도형 둘로 확인한다.
        /// <para>존재(채움 = <c>FedoraBand</c>)와 부재(낱선 = <c>BeanieCuff</c>)를 <b>같은 검사 안</b>에서
        /// 맞세운다. 한쪽만 두면 그 단언이 썩었을 때 조용히 초록이 된다(CLAUDE.md 부재 단언 규칙).</para>
        /// </summary>
        [Test]
        public void 자_선택은_오직_Filled_하나로_갈린다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();

            AccessoryShapeBuilder.Shape filledBand = AccessorySilhouetteMetrics.Find(
                AccessoryFilledBandRuler.Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora),
                "FedoraBand");
            AccessoryShapeBuilder.Shape strokeOnly = AccessorySilhouetteMetrics.Find(
                AccessoryFilledBandRuler.Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeanie),
                "BeanieCuff");

            // 존재 단언 — 지금 실제로 채움이다.
            Assert.IsTrue(filledBand.Filled,
                $"{LogPrefix} FedoraBand가 채움이 아닙니다 — 이 대조의 '채움 쪽'이 사라졌습니다.");
            Assert.AreEqual(AccessoryFilledBandRuler.FillOutlinePenInR,
                AccessoryFilledBandRuler.PenInR(filledBand), 1e-6f,
                $"{LogPrefix} 채운 도형에 낱선 자가 배정됐습니다.");

            // 부재 단언 — 지금 실제로 낱선이다. (털모자 접힌 단은 2026-09-02에 채움→낱선으로 바뀐
            // 도형이라, 이 저장소에 "낱선이 실재한다"는 사실의 가장 최근 증인이다.)
            Assert.IsFalse(strokeOnly.Filled,
                $"{LogPrefix} BeanieCuff가 채움이 됐습니다 — 이 대조의 '낱선 쪽'이 사라졌습니다. " +
                "낱선 도형이 하나도 없으면 위 자 분기는 영원히 한쪽만 타고, 그 사실을 아무도 못 봅니다.");
            Assert.AreEqual(AccessoryFilledBandRuler.StrokePenInR,
                AccessoryFilledBandRuler.PenInR(strokeOnly), 1e-6f,
                $"{LogPrefix} 낱선에 채움 자가 배정됐습니다 — 이것이 이 라운드가 막으려는 오용입니다.");

            Assert.AreNotEqual(AccessoryFilledBandRuler.PenInR(filledBand),
                AccessoryFilledBandRuler.PenInR(strokeOnly),
                $"{LogPrefix} 두 도형에 같은 자가 배정됐습니다 — 분기가 죽었습니다.");
        }
    }
}
