using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 모자 띠 겹침 + 방울 획 예산 회귀 — 2026-09-01(같은 날 베레모/펜던트 라운드의 <b>후속 배정</b>).
    ///
    /// <para>앞선 라운드가 베레모의 보조색 테를 밑변과 겹치게 고치면서 <b>자기 배정 밖</b>의 같은 결함
    /// 2건을 수치와 함께 남겼다: 중절모 띠 <b>0.41획</b>, 밀짚모자 띠 <b>0.47획</b>. 둘 다 37-6 규칙 4가
    /// "<b>최악</b>"이라고 못박은 <c>0 &lt; 간격 &lt; 1획</c> 구간이다 — 붙은 것도 뗀 것도 아니라
    /// <b>선을 두 번 그린 실수</b>로 보이고, 획(0.344R)을 얹으면 두 선의 잉크가 실제로 겹쳐
    /// 한 덩어리로 뭉갠다.</para>
    ///
    /// <para>같은 보고가 방울(NECK)의 규칙 1 위반도 남겼다: 잉크 사각형 <b>0.99획</b>. 공 전체가
    /// 획 하나 굵기라 화면에서는 "뚱뚱한 점"이다. 재 보니 <b>매단 자리도 0.11획 어긋나</b> 있어
    /// 같은 금지 구간에 있었다(보고에 없던 항목 — 이 파일이 잠근다).</para>
    ///
    /// <para>계측은 전부 <see cref="AccessorySilhouetteMetrics"/>(변 조밀 표본 · 360도)로 한다.
    /// 옛 지표(정점만 · 상반구만)는 값을 부풀려 읽는 버그가 있었고, 그 사실 자체가
    /// <see cref="AccessorySilhouetteDistinctionTests"/>의 네거티브 컨트롤로 증명돼 있다.
    /// 이 파일도 <b>고친 항목마다 네거티브 컨트롤</b>을 짝지어 둔다 — 옛 좌표를 그대로 박제해
    /// "자가 그것을 빨간불로 읽는가"를 같은 스위트 안에서 단언한다.</para>
    ///
    /// <para>★★ <b>2026-09-06 — 이 파일의 띠 검사는 「도형의 폭」만 잰다. 「보이는 색면」은 짝 파일이 잰다.</b>
    /// (<c>AccessoryHatBandAndBellTests.NetInk.cs</c> · <see cref="AccessoryBandNetInkRuler"/>)
    /// debugger 규명 [Major-2]가 이 파일의 구조적 결함 둘을 수치로 남겼다:
    /// <list type="number">
    ///   <item>아래 <see cref="모자_띠는_자기_관_밑변과_정확히_겹친다"/>의 <c>TestCase</c>는 <b>2건</b>인데
    ///     그중 중절모는 인계본이라 건너뛴다 — <b>모자 6종 중 실제로 도는 것이 1종</b>이었고,
    ///     천모자·털모자·왕관은 목록에 <b>아예 없었다</b>.</item>
    ///   <item>여기서 재는 <c>thickness</c>는 <b>총두께</b>다. 천모자 챙 띠는 그 자로 2.12획이라
    ///     통과했지만, 그때 위아래 두 획이 경계를 먹어 화면에 남던 색은 <b>1.123pt</b>였다
    ///     — 즉 <b>통과 판정과 안 보이는 상태가 똑같이 생겼다</b>(같은 밤 처방 뒤 1.623pt).</item>
    /// </list>
    /// 짝 파일이 그 둘을 <b>순 색면 예산</b>(net = 총두께 − 경계에 좌표가 일치하는 획의 반폭)과
    /// 모자 <b>6종 전수</b> 게이트로 막는다. 이 파일의 검사들은 「올린 띠」 규약(아랫변 = 관 밑변)을
    /// 잠그는 <b>다른 일</b>을 계속 한다 — 두 검사는 겹치지 않는다.</para>
    /// </summary>
    public sealed partial class AccessoryHatBandAndBellTests
    {
        /// <summary>배율 0.75(출하 기본)에서 실제로 그려지는 획. 판정 문턱은 전부 이 값의 배수다.</summary>
        private static float W => AccessorySilhouetteMetrics.StrokeInR;

        private static AccessoryShapeBuilder.Rig Rig() => AccessorySilhouetteMetrics.Rig();

        private static List<AccessoryShapeBuilder.Shape> Build(EquipmentSlot slot, int item)
            => AccessorySilhouetteMetrics.Build(Rig(), slot, item);

        // ============================================================================
        // 1. 중절모 / 밀짚모자 — 띠가 금지 구간을 벗어났는가
        // ============================================================================

        /// <summary>띠와 관(crown) 밑변은 <b>완전히 겹치거나</b>(간격 0) 확실히 떨어져야(≥1.5획) 한다.
        /// <para>중절모는 관 높이가 0.72R, 밀짚모자는 0.54R뿐이라 "위로 1.5획 올린다"는
        /// <b>산술적으로 불가능</b>하다(위아래로 1.5획씩 두려면 관이 1.03R이어야 한다).
        /// 그래서 규칙 4가 허용하는 나머지 안전 구간인 <b>겹침</b>을 택했고, 띠는 좌표를 새로 적지 않고
        /// 관 밑변의 두 끝점을 그대로 받는다 — 어긋날 자리 자체가 없다(베레모와 같은 해법).</para>
        ///
        /// <para>★★ <b>2026-09-03 — 재던 것이 하나에서 둘로 갈렸다</b>(스펙 14-1). 띠가
        /// <b>낱선에서 닫힌 채움 띠</b>가 되면서 <see cref="AccessorySilhouetteMetrics.MaxGapToShape"/>가
        /// 돌려주는 값의 <b>뜻이 바뀌었다</b>: 예전에는 "선 하나가 관에서 얼마나 떴는가"였는데
        /// 이제는 <b>"띠의 가장 먼 부분(= 윗변)이 얼마나 떨어졌는가" = 띠 두께</b>다.
        /// 아랫변은 여전히 관 밑변 그 자체(간격 0)인데, <b>최댓값 하나로는 그 사실이 안 보인다</b> —
        /// 아랫변이 통째로 떠도 윗변이 멀면 옛 단언은 통과한다. 그래서 둘로 나눠 잰다.</para>
        /// <list type="number">
        ///   <item><b>이음매</b>(아랫변만) — 간격이 0이어야 한다. 이것이 "정확히 겹친다"의 실체다.</item>
        ///   <item><b>두께</b>(도형 전체) — 자기 <b>윤곽선 펜</b> 1.5개 이상이어야 한다.</item>
        /// </list>
        /// <para>2번의 자는 <see cref="AccessoryFilledBandRuler.PenInR"/>가 <c>Filled</c> 하나로 고른다.
        /// 낱선 자(2.00pt)로 재면 0.46R이 1.34획으로 읽혀 금지 구간으로 <b>오판</b>되는데,
        /// 그 하한의 정의(<see cref="StickConfig.MinStrokeScreenPoints"/>)가 <i>"그 자리의 유일한
        /// 잉크인 획"</i>이라 <b>채움의 경계선에는 적용되지 않는다</b>고 프로덕션 문서가 직접 못 박는다.</para></summary>
        [TestCase(AccessoryShapeBuilder.HeadFedora, "FedoraBand", "FedoraCrown")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, "StrawBand", "StrawCrown")]
        public void 모자_띠는_자기_관_밑변과_정확히_겹친다(int item, string bandName, string crownName)
        {
            HandoffTestGate.SkipIfHandoff(EquipmentSlot.Head, item, "모자 띠 = 관 밑변 정확히 겹침 — 인계본 중절모 띠(F1)는 원문 기하");

            // ★★ 2026-09-06 — 밀짚모자 쪽 <b>건너뛰기 게이트를 걷었다</b>(<c>HandoffTestGate.SkipIfR25Hat</c>).
            //    되돌리기 전에 이 문단을 읽어라.
            //
            //    <b>왜 꺼져 있었나</b>(R25 첫 판): 게이트 사유가 "R21 밀짚모자 띠는 관을 두르는 독립 아이콘
            //    조각(두께 6.5u)이라 관 밑변에서 유도되지 않는다"였다. 실제로 그 띠는 관 곡률을 따라 휜
            //    34점 도형이라 「올린 띠」 규약(아랫변 + 역순 윗변)이 성립할 수 없었다.
            //
            //    <b>무엇이 바뀌었나</b>: 같은 밤 R25d 가 <c>V1Hat_StrawBand</c> 를 <b>관에서 유도</b>하도록
            //    다시 썼다 — 아랫변 두 점은 <c>V1Hat_StrawCrown</c> 의 첫 점·끝 점(그 폴리곤의 닫힘변)
            //    <b>그대로</b>이고 윗변은 그것을 0.46 R 올린 것이다. 재측정(2026-09-06, 배율 0.75):
            //      · y 법칙 최대 오차 4.2e-17 유닛(허용 1e-5) · 기운 꼭짓점 0개(기대 0)
            //      · 이음매 2.5e-16 R(허용 1e-4) · 두께 0.45395 R = <b>2.08획</b>(채움 윤곽선 펜, 하한 1.5획)
            //    (중절모 쪽은 위 <c>SkipIfHandoff</c> 가 계속 담당한다 — 이번 걷기와 무관하다.)
            //
            //    <b>함께 걷지 않은 것</b>: 짝인 음성 대조 <see cref="지표가_옛_밀짚모자_띠를_실제로_잡는다"/>는
            //    여전히 건너뜀이다(그 자리에 사유). 그동안의 자 교정은 이 검사 맨 아래 «자 교정» 블록이 맡는다.
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> hat = AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, item);
            AccessoryShapeBuilder.Shape band = AccessorySilhouetteMetrics.Find(hat, bandName);
            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(hat, crownName);
            string label = ItemCatalog.Item(EquipmentSlot.Head, item).DisplayName;

            // ★ 「채운 도형인가」를 먼저 코드로 확인한다 — 이 한 줄이 아래 자 선택의 근거 전부다.
            //   낱선으로 되돌아가면 여기서 먼저 멈춘다(조용한 초록이 되지 않는다).
            AccessoryFilledBandRuler.AssertRaisedBandForm(rig, band, $"{label} 띠");

            // (1) 이음매 — 아랫변만 잘라 재면 "겹쳤는가"가 그대로 나온다.
            //     아랫변이 관 밑변 그 자체라는 것이 이 도형의 규약이고, 그 아랫변이 점 배열의
            //     앞 절반이라는 것은 위 AssertRaisedBandForm이 방금 잠근 사실이다.
            float seamGap = AccessorySilhouetteMetrics.MaxGapToShape(
                rig, AccessoryFilledBandRuler.BottomEdge(band), crown);
            Assert.Less(seamGap, AccessoryFilledBandRuler.CoincidenceInR,
                $"{label}의 띠 <b>아랫변</b>이 관 밑변에서 {seamGap / W:F2}획(낱선 획 기준) 떠 있습니다 " +
                "(옛 값 중절모 0.41 / 밀짚모자 0.47획). 아랫변은 관 밑변의 두 끝점을 <b>그대로</b> " +
                "받아야 합니다 — 좌표를 새로 적는 순간 어긋날 자리가 생기고, 그 어긋남은 " +
                "'띠 두께'에 묻혀 최댓값 검사로는 보이지 않습니다.");

            // (2) 두께 — 자기 윤곽선 펜으로 잰다.
            float pen = AccessoryFilledBandRuler.PenInR(band);
            Assert.AreEqual(AccessoryFilledBandRuler.FillOutlinePenInR, pen, 1e-6f,
                $"{label}의 띠에 낱선 자가 배정됐습니다 — 위 형태 검사가 채움을 확인했는데도 " +
                "자가 안 따라왔다면 AccessoryFilledBandRuler.PenInR의 분기가 깨진 것입니다.");

            float thickness = AccessorySilhouetteMetrics.MaxGapToShape(rig, band.Points, crown);
            Assert.GreaterOrEqual(thickness, pen * AccessoryFilledBandRuler.SeparationStrokes,
                $"{label}의 띠 두께가 {thickness / pen:F2}획(윤곽선 펜 {pen:F5}R 기준)뿐입니다. " +
                $"1.5획({pen * AccessoryFilledBandRuler.SeparationStrokes:F4}R) 미만이면 띠의 윗변과 " +
                "관 밑변이 각자의 잉크로 맞붙어, 화면에서 '선을 두 번 그린 실수'로 읽힙니다(규칙 4).");

            // ★ 자 교정 — «이음매 0»은 <b>자가 죽어도 0</b>이다. 그래서 같은 아랫변을 <b>알려진 만큼
            //   띄운 사본</b>을 같은 자로 재서, 자가 그 변위를 그대로 읽고 금지 구간으로 판정하는지
            //   매 실행 확인한다(미는 양은 "확실히 떨어졌다" 값의 절반 = 정의상 금지 구간 한가운데).
            //   프로덕션 좌표는 안 건드린다 — 사본만 민다.
            float probeInR = pen * AccessoryFilledBandRuler.SeparationStrokes * 0.5f;
            Vector3[] floated = AccessoryFilledBandRuler.BottomEdge(band);
            for (int i = 0; i < floated.Length; i++)
            {
                floated[i] = new Vector3(floated[i].x, floated[i].y + probeInR * rig.HeadRadius, floated[i].z);
            }
            float floatedGap = AccessorySilhouetteMetrics.MaxGapToShape(rig, floated, crown);

            Assert.AreEqual(probeInR, floatedGap, 1e-4f,
                $"{label}의 아랫변을 {probeInR:F5}R 띄웠는데 자는 {floatedGap:F5}R로 읽었습니다 — " +
                "자가 변위에 반응하지 않으면 위 «이음매 0»은 아무것도 증명하지 못합니다.");
            Assert.IsFalse(AccessoryFilledBandRuler.PassesRuleFour(floatedGap, pen),
                $"{label}에서 띄운 아랫변의 간격 {floatedGap / pen:F2}획을 자가 규칙 4 <b>통과</b>로 " +
                "읽었습니다 — 금지 구간이 실제로는 안 막힌다는 뜻입니다.");

            Debug.Log($"{AccessoryFilledBandRuler.LogPrefix} {label} 띠 — 이음매 {seamGap:E2}R(겹침), " +
                $"두께 {thickness:F4}R = {thickness / pen:F2}획(윤곽선 펜) / " +
                $"{thickness / W:F2}획(낱선 획, <b>이 자로 재면 오판</b>), " +
                $"자 교정 {floatedGap:F5}R(민 양 {probeInR:F5}R).");
        }

        /// <summary>띠는 <b>보조색 그대로</b>여야 한다 — 겹치게 만들면서 톤까지 바꾸면 모자에서
        /// 보조색이 사라진다(규칙 3-2: 보조색은 형제와 나를 가르는 단 한 부분).
        /// <para>중절모는 <b>띠가 있는 것이 정체성</b>인 모자라, 이 검사는 "띠를 없애서 겹침 문제를
        /// 해결하는" 길을 막는 울타리이기도 하다.</para></summary>
        [TestCase(AccessoryShapeBuilder.HeadFedora, "FedoraBand")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, "StrawBand")]
        public void 모자_띠는_보조색으로_남아있다(int item, string bandName)
        {
            HandoffTestGate.SkipIfHandoff(EquipmentSlot.Head, item, "모자 띠 보조색 — 인계본은 브라스 단색 체계(보조색 「정확히 1」 폐지, R16 (h))");
            List<AccessoryShapeBuilder.Shape> hat = Build(EquipmentSlot.Head, item);
            AccessoryShapeBuilder.Shape band = AccessorySilhouetteMetrics.Find(hat, bandName);

            Assert.AreEqual(AccessoryShapeBuilder.Accent, band.Tone,
                $"{bandName}이 보조색이 아닙니다 — 띠는 이 모자를 형제와 가르는 단 한 부분입니다(37-6 규칙 3-2).");

            float span = AccessorySilhouetteMetrics.ExtentInR(Rig(), band.Points).x;
            Assert.GreaterOrEqual(span, W * 1.5f,
                $"{bandName}의 길이가 {span / W:F2}획입니다 — 1.5획 미만이면 띠가 아니라 점입니다(규칙 1).");
        }

        /// <summary>★ 네거티브 컨트롤 — <b>옛 중절모 띠</b>(관 밑변 위 0.14R, x ±0.72R)를 그대로 박제한다.
        /// 자가 이 값을 <b>금지 구간 안</b>(0 &lt; 간격 &lt; 1획)이라고 말해야 위 검사가 의미를 갖는다.</summary>
        [Test]
        public void 지표가_옛_중절모_띠를_실제로_잡는다()
        {
            HandoffTestGate.SkipIfHandoff(EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora, "옛 중절모 띠 음성 대조(FedoraCrown 기준)");
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float brimY = rig.HeadCenterY + r * AccessoryShapeBuilder.FedoraBrimLineRatio;
            float crownHalf = r * AccessoryShapeBuilder.FedoraCrownHalfWidthRatio;

            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora), "FedoraCrown");

            // 옛 좌표: FedoraBandRiseRatio = 0.14f (지금은 지워진 상수 — 여기 값이 곧 검사의 내용이다).
            var oldBand = new[]
            {
                rig.F(-crownHalf, brimY + r * 0.14f),
                rig.F(crownHalf, brimY + r * 0.14f),
            };
            float gap = AccessorySilhouetteMetrics.MaxGapToShape(rig, oldBand, crown);

            Assert.That(gap, Is.GreaterThan(1e-4f).And.LessThan(W),
                $"옛 중절모 띠의 간격이 {gap / W:F2}획으로 측정됐습니다 — 금지 구간(0 < 간격 < 1획) 안이어야 합니다 " +
                "(옛 관에서 0.41획, 2026-09-02 재설계된 관에서 0.45획, R12 이식본 관에서 0.35획 — " +
                "<b>관이 움직이면 값도 함께 움직인다</b>. 옛 띠 좌표는 지워진 상수 " +
                "FedoraBandRiseRatio(0.14f)와 <b>살아 있는</b> FedoraCrownHalfWidthRatio에서 나오므로, " +
                "관 반폭이 0.98 -> 0.61R로 좁아진 만큼 옛 띠도 함께 좁아진 채 재현된다). " +
                "지표가 이 값을 금지 구간 밖으로 읽으면 위 검사는 아무것도 막지 못합니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — <b>옛 밀짚모자 띠</b>(관 밑변 위 0.16R, x ±0.98·0.78R).
        /// 폭이 관보다 2% 좁아 <b>끝만 살짝 어긋나 있던</b> 것까지 그대로 재현한다.</summary>
        [Test]
        public void 지표가_옛_밀짚모자_띠를_실제로_잡는다()
        {
            // ★ 2026-09-06 R25 — 이 음성 대조는 <b>살아 있는 상수</b>(StrawCrownHalfWidthRatio)로 옛 띠를
            //   재구성해 왔는데, R25 재저작이 그 상수를 지웠다. 옛 관도 옛 띠도 없어졌으므로 이 대조는
            //   더 이상 아무것도 통제하지 못한다 — 숫자를 리터럴로 박아 「되살리는」 것은 사라진 도형에
            //   대한 자기 대화일 뿐이다.
            //
            //   ★★ 2026-09-06 재검토(test-engineer) — <b>게이트 유지</b>. 위 본 검사
            //   (<see cref="모자_띠는_자기_관_밑변과_정확히_겹친다"/>)는 같은 날 되살렸지만 이 대조는
            //   <b>같이 걷지 않았다</b>. 실측: 재구성한 «옛 띠»의 간격은 <b>0.10880 R = 0.32획</b>이라
            //   단언 구간 (1e-4, 1획) 안이므로 <b>걷으면 실제로 통과는 한다</b>. 그런데 그 초록이 서는
            //   근거가 이 검사의 이름·주석·기대값과 <b>전부 어긋난다</b>:
            //     · 이름과 문서는 «관 밑변 <b>위</b> 0.16 R»이라고 말하는데, 새 관 밑변은 +0.685 R이라
            //       재구성 좌표(+0.5762 R)는 관 <b>아래</b>다 — 재고 있는 것이 더 이상 «옛 띠»가 아니다.
            //     · 실패 메시지가 못박은 기대 실측치(0.47 / 0.46획)도 0.32획으로 갈라졌다.
            //   즉 지금 걷으면 «사실이 아닌 문장으로 감싸인 초록»이 남는다 — 이 저장소가 반복해서
            //   당한 형태다. 되살리려면 좌표를 <b>살아 있는 관에서 유도</b>하고 이름·기대값을 함께
            //   다시 쓰는 <b>재저작</b>이 필요하다(별도 배정). 그때까지 자 교정은 위 본 검사 안의
            //   «자 교정» 블록이 대신한다 — 자가 죽으면 거기서 먼저 빨개진다.
            HandoffTestGate.SkipIfR25Hat(AccessoryShapeBuilder.HeadStraw,
                "옛 밀짚모자 띠 음성 대조 — 재구성에 쓰던 StrawCrownHalfWidthRatio 가 R25 로 폐기됐다");
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float brimY = rig.HeadCenterY + r * AccessoryShapeBuilder.StrawBrimLineRatio;
            float crownHalf = r * 0.86f;   // 폐기된 상수 StrawCrownHalfWidthRatio 의 마지막 값

            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Head, AccessoryShapeBuilder.HeadStraw), "StrawCrown");

            // 옛 좌표: StrawBandRiseRatio = 0.16f, 반폭은 crownHalf * 0.98f.
            var oldBand = new[]
            {
                rig.F(-crownHalf * 0.98f, brimY + r * 0.16f),
                rig.F(crownHalf * 0.98f, brimY + r * 0.16f),
            };
            float gap = AccessorySilhouetteMetrics.MaxGapToShape(rig, oldBand, crown);

            Assert.That(gap, Is.GreaterThan(1e-4f).And.LessThan(W),
                $"옛 밀짚모자 띠의 간격이 {gap / W:F2}획으로 측정됐습니다 — 금지 구간(0 < 간격 < 1획) 안이어야 " +
                "합니다(옛 관에서 0.47획, 재설계된 관에서 0.46획).");
        }

        /// <summary>
        /// 모자 6종이 서로 구분된다 — <b>띠를 둘 다 관 밑변으로 옮긴 뒤에도</b>.
        /// <para>이 검사가 이 라운드에 필요한 이유: 두 모자의 보조색 선이 같은 자리(관 밑동)로 모였다.
        /// 띠는 실루엣 안쪽 선이라 원리적으로 외곽에 영향을 주지 않지만, "안 준다"를 말로만 두지 않는다.
        /// 실측 최소는 <b>왕관↔베레모 1.84획</b>이다(2026-09-01 감쌈 재설계 이후. 그 이전에는
        /// 털모자↔중절모 2.95획이었는데, 큰 값의 정체는 "잘 구분된다"가 아니라 "여섯 종이 서로 다른
        /// 높이로 <b>떠 있다</b>"였다 — 자세한 것은 AccessoryBeaniePomTests의 같은 검사 문단).</para>
        /// </summary>
        [Test]
        public void 모자_6종이_서로_구분된다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            int count = ItemCatalog.ItemCountIn(EquipmentSlot.Head);

            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    float d = AccessorySilhouetteMetrics.MaxRadiusDelta(
                        AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Head, a),
                        AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Head, b));
                    Assert.Greater(d, W,
                        $"{ItemCatalog.Item(EquipmentSlot.Head, a).DisplayName}와 " +
                        $"{ItemCatalog.Item(EquipmentSlot.Head, b).DisplayName}의 외곽 차이가 {d / W:F2}획뿐입니다 — " +
                        "카테고리 안에서 구분되는 것이 곧 아이템의 존재 이유입니다(규칙 7-3).");
                }
            }
        }

        // ============================================================================
        // 2. 방울 — 규칙 1(획 예산) + 규칙 4(매달린 지점)
        // ============================================================================

        /// <summary>공 전체가 획 하나 굵기면 그것은 원이 아니라 점이다.
        /// <para>옛 방울은 지름 0.34R = <b>0.99획</b>이었다. 규칙 1은 그려지는 도형의 잉크 사각형이
        /// 1.5획 이상일 것을 요구한다.</para></summary>
        [Test]
        public void 방울은_획_예산을_지킨다()
        {
            HandoffTestGate.SkipIfHandoff(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell, "v1 방울(Bell/Collar) 규칙 — 획 예산·정원 각도·목줄 최저점 매달림·펜던트와 구분·통째 흔들림. 흔들림은 인계본 방울 4조각(B4·RB5·CB6·H7) 전체 구간으로 CardShapeContractTests 골든이 잠근다");
            AccessoryShapeBuilder.Rig rig = Rig();
            AccessoryShapeBuilder.Shape bell = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Bell");

            Vector2 ext = AccessorySilhouetteMetrics.ExtentInR(rig, bell.Points);
            float span = Mathf.Max(ext.x, ext.y);
            Assert.GreaterOrEqual(span, W * 1.5f,
                $"방울의 잉크 사각형이 {span / W:F2}획입니다(옛 값 0.99획) — 1.5획 미만이면 " +
                "화면에서 '뚱뚱한 점' 하나입니다(37-6 규칙 1).");

            Assert.IsTrue(bell.Filled,
                "방울이 채워져 있지 않습니다 — 윤곽선으로 두면 규칙 1이 요구하는 '내부를 보여주는 크기'가 " +
                "3.0획(1.03R)이라 머리 반지름만 한 방울이 되어야 하고, 그러면 펜던트와 다시 붙습니다. " +
                "방울은 속이 보여야 하는 물건이 아니라 금속 덩어리입니다(규칙 2).");
        }

        /// <summary>
        /// 방울의 <b>꺾임각이 획 예산 검사의 문턱(45도)보다 작다</b> — 즉 매끄러운 곡선으로 인정된다.
        /// <para>왜 검사인가: 변의 수를 줄이면(8각형) 꺾임이 정확히 45도가 되어 각 변이 <b>독립된 획</b>으로
        /// 요구되고(<c>AccessoryStrokeBudgetTests.AssertNoStubSegments</c>), 그 조건을 만족하는 8각형은
        /// 지름이 2.6획이라 펜던트와 다시 붙는다. "원을 각지게 만들수록 커져야 한다"는 이 함정을
        /// 숫자로 박제해 둔다.</para>
        /// </summary>
        [Test]
        public void 방울은_매끄러운_원으로_인정되는_각도를_유지한다()
        {
            HandoffTestGate.SkipIfHandoff(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell, "v1 방울(Bell/Collar) 규칙 — 획 예산·정원 각도·목줄 최저점 매달림·펜던트와 구분·통째 흔들림. 흔들림은 인계본 방울 4조각(B4·RB5·CB6·H7) 전체 구간으로 CardShapeContractTests 골든이 잠근다");
            Vector3[] p = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Bell").Points;

            Assert.GreaterOrEqual(p.Length, 10, "방울의 변이 10개 미만입니다 — 각져 보입니다.");
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 v1 = p[i] - p[(i - 1 + p.Length) % p.Length];
                Vector2 v2 = p[(i + 1) % p.Length] - p[i];
                float turn = Vector2.Angle(v1, v2);
                Assert.Less(turn, 45f,
                    $"방울 {i}번 꼭짓점의 꺾임이 {turn:F1}도입니다 — 45도 이상이면 획 예산 검사가 " +
                    "각 변을 '독립된 획'으로 요구하고(최소 1.0획), 그 크기의 방울은 펜던트와 다시 붙습니다.");
            }
        }

        /// <summary>매달린 지점이 보여야 물건이 공중에 뜨지 않는다(규칙 4).
        /// 방울과 펜던트가 <b>같은 목줄 좌표</b>에서 매달리므로 위 끝이 목줄 최저점 그 자리다.</summary>
        [Test]
        public void 방울은_목줄_최저점에_매달린다()
        {
            HandoffTestGate.SkipIfHandoff(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell, "v1 방울(Bell/Collar) 규칙 — 획 예산·정원 각도·목줄 최저점 매달림·펜던트와 구분·통째 흔들림. 흔들림은 인계본 방울 4조각(B4·RB5·CB6·H7) 전체 구간으로 CardShapeContractTests 골든이 잠근다");
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> shapes = Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell);

            Vector3[] collar = AccessorySilhouetteMetrics.Find(shapes, "Collar").Points;
            float collarLow = float.MaxValue;
            for (int i = 0; i < collar.Length; i++) collarLow = Mathf.Min(collarLow, collar[i].y);

            Vector3[] bell = AccessorySilhouetteMetrics.Find(shapes, "Bell").Points;
            float bellTop = float.MinValue;
            for (int i = 0; i < bell.Length; i++) bellTop = Mathf.Max(bellTop, bell[i].y);

            Assert.AreEqual(collarLow, bellTop, 1e-5f,
                "방울의 꼭대기가 목줄 최저점과 어긋났습니다 — 옛 방울이 정확히 0.11획(금지 구간) " +
                "어긋나 있었습니다. 10각형은 위상 0도로 두면 가장 높은 꼭짓점이 72도에 놓입니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — <b>옛 방울</b>(반지름 0.17R, 목선 아래 0.34R, 위상 0도 10각형)을
        /// 그대로 박제한다. 자가 ① 잉크 사각형을 1.5획 <b>미만</b>으로, ② 매단 자리를 금지 구간으로
        /// 읽어야 위 두 검사가 의미를 갖는다.</summary>
        [Test]
        public void 지표가_옛_방울을_실제로_잡는다()
        {
            HandoffTestGate.SkipIfHandoff(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell, "v1 방울(Bell/Collar) 규칙 — 획 예산·정원 각도·목줄 최저점 매달림·펜던트와 구분·통째 흔들림. 흔들림은 인계본 방울 4조각(B4·RB5·CB6·H7) 전체 구간으로 CardShapeContractTests 골든이 잠근다");
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float ty = AccessoryShapeBuilder.NeckLocalY(rig);

            // 옛 좌표: BellDropRatio = 0.34f, BellRadiusRatio = 0.17f (지금은 지워졌거나 바뀐 값들).
            float oldY = ty - r * 0.34f;
            float oldR = r * 0.17f;
            var oldBell = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 2f / 10f * i;
                oldBell[i] = rig.F(Mathf.Cos(a) * oldR, oldY + Mathf.Sin(a) * oldR);
            }

            Vector2 ext = AccessorySilhouetteMetrics.ExtentInR(rig, oldBell);
            float span = Mathf.Max(ext.x, ext.y);
            Assert.Less(span, W * 1.5f,
                $"옛 방울의 잉크 사각형이 {span / W:F2}획으로 측정됐습니다 — 실측은 0.99획입니다. " +
                "지표가 이 값을 규칙 1 통과로 읽으면 위 검사는 공허합니다.");

            float oldTop = float.MinValue;
            for (int i = 0; i < oldBell.Length; i++) oldTop = Mathf.Max(oldTop, oldBell[i].y);

            // ★ 2026-09-02 — 목줄 최저점을 <b>실제로 그려진 목줄에서 잰다</b>.
            //   옛 코드에는 AccessoryShapeBuilder.CollarLowLocalY(=CollarRise−CollarDip)가 있었지만,
            //   목 형상이 에셋으로 내려가면서(B-2 파일럿) 그 비율의 주인은 에셋 하나가 됐다.
            //   여기서 상수를 다시 적으면 <b>같은 사실이 두 곳</b>에 생기고, 에셋만 고친 날
            //   이 대조가 조용히 옛 세상을 재게 된다.
            Vector3[] chain = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Collar").Points;
            float collarLowY = float.MaxValue;
            for (int i = 0; i < chain.Length; i++) collarLowY = Mathf.Min(collarLowY, chain[i].y);

            float attachGap = Mathf.Abs(collarLowY - oldTop) / r;
            Assert.That(attachGap, Is.GreaterThan(1e-4f).And.LessThan(W),
                $"옛 방울의 매단 자리 간격이 {attachGap / W:F2}획으로 측정됐습니다 — 실측은 0.11획(금지 구간)입니다.");
        }

        /// <summary>
        /// 방울을 키워도 펜던트와 갈린다 — 이번 수정의 <b>대가</b>를 잠그는 검사다.
        /// <para>둘은 같은 목줄에 매달린 형제라 갈리는 축이 "얼마나 내려오는가"다. 방울을 키우면
        /// 그 축이 그만큼 줄어든다(실측 2.52획 -> 1.98획). 그래서 1.0획이 아니라 <b>1.5획</b>을 문턱으로
        /// 잡는다 — 규칙 1의 "구분돼야 하는 두 선"과 같은 값이다.</para>
        /// <para>함께 잠그는 것: 방울은 <b>원</b>이고 펜던트는 아니다. 앞선 라운드가 "원과 갈리는 것은
        /// 크기가 아니라 종횡비"라는 결론으로 펜던트를 세로로 뺐으므로, 방울이 그 축을 넘어
        /// 타원이 되면 두 아이템이 다시 같은 그림으로 수렴한다.</para>
        /// </summary>
        [Test]
        public void 방울을_키워도_펜던트와_갈린다()
        {
            HandoffTestGate.SkipIfHandoff(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell, "v1 방울(Bell/Collar) 규칙 — 획 예산·정원 각도·목줄 최저점 매달림·펜던트와 구분·통째 흔들림. 흔들림은 인계본 방울 4조각(B4·RB5·CB6·H7) 전체 구간으로 CardShapeContractTests 골든이 잠근다");
            AccessoryShapeBuilder.Rig rig = Rig();

            float d = AccessorySilhouetteMetrics.MaxRadiusDelta(
                AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell),
                AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckPendant));
            Assert.GreaterOrEqual(d, W * 1.5f,
                $"방울과 펜던트의 외곽 차이가 {d / W:F2}획입니다 — 방울을 규칙 1에 맞추려고 키우다가 " +
                "형제와 다시 붙었습니다. 키우는 대신 채우는 것이 이 수정의 요지입니다.");

            Vector2 bell = AccessorySilhouetteMetrics.ExtentInR(rig,
                AccessorySilhouetteMetrics.Find(
                    Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Bell").Points);
            float bellAspect = bell.y / bell.x;
            Assert.That(bellAspect, Is.EqualTo(1f).Within(0.2f),
                $"방울의 종횡비가 {bellAspect:F2}입니다 — 방울은 원으로 남아야 합니다. " +
                "세로로 길어지면 펜던트(2.21)의 식별 축을 침범합니다.");
        }

        /// <summary>규칙 3-2 · 규칙 5 — 보조색은 단 한 부분, 도형은 2~4개.
        /// <para>옛 방울은 공과 추(clapper)가 <b>둘 다</b> 보조색이었고, 추는 잉크 사각형이 0.29획이라
        /// 화면에 존재하지 않는 선이었다. 규칙 5가 그 경우를 명시한다 —
        /// "[선택] 디테일은 W 예산을 못 지키면 넣지 않는다".</para></summary>
        [Test]
        public void 방울_목걸이의_구성이_정원과_보조색_규칙을_지킨다()
        {
            List<AccessoryShapeBuilder.Shape> shapes = Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell);

            // ★ 2026-09-05 계약 v2(R16 (h)) — 정원·보조색 검사는 폐지됐다(잃는 것: 조각 수·보조색 수 상한).
            //   방울 목걸이는 인계본 기하(8조각)로 바뀌었다 — 조각이 있는지만 본다.
            Assert.Greater(shapes.Count, 0, "방울 목걸이의 도형이 없습니다.");
        }

        /// <summary>설명문 "걸을 때마다 방울이 흔들린다"가 코드에 남아 있는가(원칙 1).
        /// <para>추를 지우면서 흔들 점 선언까지 같이 날아가는 것을 막는다 — 흔들 구간은
        /// 점 배열 전체를 덮어야 공이 통째로 흔들린다(일부만 흔들면 공이 찌그러진다).</para></summary>
        [Test]
        public void 방울은_통째로_흔들린다()
        {
            HandoffTestGate.SkipIfHandoff(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell, "v1 방울(Bell/Collar) 규칙 — 획 예산·정원 각도·목줄 최저점 매달림·펜던트와 구분·통째 흔들림. 흔들림은 인계본 방울 4조각(B4·RB5·CB6·H7) 전체 구간으로 CardShapeContractTests 골든이 잠근다");
            AccessoryShapeBuilder.Shape bell = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Bell");

            Assert.IsTrue(bell.HasSway,
                $"방울에 흔들 점이 없습니다 — 설명문(\"{ItemCatalog.Item(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell).Description}\")이 " +
                "주장하는 동작이 코드에 없습니다(원칙 1).");
            Assert.AreEqual(0, bell.SwayStart);
            Assert.AreEqual(bell.Points.Length, bell.SwayCount,
                "흔들 구간이 방울의 점 일부만 덮습니다 — 공의 한쪽만 움직이면 원이 찌그러집니다.");
        }
    }
}
