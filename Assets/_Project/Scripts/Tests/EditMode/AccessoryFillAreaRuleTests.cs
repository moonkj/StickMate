using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>규칙 1-C — 색면 조건</b>(2026-09-02 신설, docs/CHARACTER_FORM_SPEC.md 20절).
    ///
    /// ============================================================================
    /// 규칙 1에 있던 구멍 — <b>두께를 재는 항목이 하나도 없었다</b>
    /// ============================================================================
    /// 기존 규칙 1의 검사는 정확히 둘이다: (A) 잉크 사각형의 <b>긴 변</b> ≥ 1.5획,
    /// (B) 양끝이 모두 꺾임인 <b>변의 길이</b> ≥ 1획. <b>둘 다 길이다.</b>
    /// 길이 5W · 두께 0.1W짜리 실오라기도 그 검사를 통과한다.
    /// 그런데 색면을 죽이는 것은 길이가 아니라 두께다 — 당시 채움 61개가 <b>전부 통과하고도</b>
    /// 38개의 색면 생존율이 30% 미만이던 이유가 이것이다(지금은 60개 — 2026-09-02 털모자 단이 낱선이 됐다).
    ///
    /// ============================================================================
    /// 무엇을 재는가 — 대리 지표가 아니라 <b>ρ_max를 직접</b>
    /// ============================================================================
    /// 폭 W인 펜이 경계에 <b>중심</b>을 두므로 도형은 안쪽으로 W/2를 잃는다. 따라서
    /// <code>
    ///   색면이 존재한다   ⟺  ρ_max &gt; W_out / 2
    ///   색면 폭 ≥ 획 하나 ⟺  ρ_max ≥ W_out        ← 이 프로젝트가 이미 쓰는 "1획 미만은 존재하지 않는다"
    /// </code>
    /// 여기서 ρ_max = 그 도형의 <b>최대 내접원 반경</b>이다.
    ///
    /// <para>★ <b>"bbox 짧은 변 / 2"로 대체하지 않는다.</b> 당시 채움 61개 전수 실측에서 그 값은 ρ_max의
    /// <b>상계</b>였다(ρ_max / (짧은변/2) = 최소 0.313 · 중앙 0.827 · 최대 1.000). 상계로 통과시키면
    /// <b>통과하고도 색면이 없는 도형</b>이 나온다 — 최악은 실제 두께가 bbox가 약속한 두께의
    /// 31%뿐인 도형이다. 그래서 격자 분지한정으로 ρ_max를 직접 잰다.</para>
    ///
    /// ============================================================================
    /// M6과 묶여 있다 — 순서가 바뀌면 30종을 두 번 다시 그린다
    /// ============================================================================
    /// 이 규칙을 <b>현행 펜</b>(하한 2.00pt)으로 적용하면 게이트 배율 0.60에서 <b>36개</b>가 즉시
    /// 위반이고, 그걸 맞추려 두껍게 그리면 이번엔 배율 1.00에서 뭉툭해진다.
    /// M6(채움 윤곽선 1.00pt)과 함께면 위반이 <b>4개</b>뿐이다 — 그 4개가 아래 면제 대장이다.
    ///
    /// <para><b>게이트</b>: 배율 0.60(사용자 저장) / 0.75(출하) / 1.00(다이얼 최대).
    /// M6 후 이 셋의 <c>W_out</c>은 <b>0.21818 R로 동일</b>하다(하한이 안 물린다).
    /// <b>경고</b>: 다이얼 최소 배율은 게이트가 아니라 보고만 한다 — 그 구간은 이 스펙이 이미
    /// "실루엣 전용"으로 선언한 곳이고, 거기 맞춰 두껍게 그리면 큰 배율이 망가진다.</para>
    /// </summary>
    public sealed class AccessoryFillAreaRuleTests
    {
        private const string LogPrefix = "[색면-TEST]";

        /// <summary>설계 목표(권장). 게이트는 1.00획이지만 오프라인 모형은 <b>낙관적</b>이라
        /// (실측: 야구모자 챙 예측 6.5% vs 실제 2.1% — 살아남은 색면이 2물리픽셀이라 대부분 AA 혼색으로
        /// 빠졌다) 여유 없이 1.00획에 붙은 도형은 화면에서 통과하지 못할 수 있다.
        /// 같은 이유로 headroom.py도 TARGET = 1.20을 쓴다.</summary>
        internal const float TargetStrokes = 1.20f;

        /// <summary>게이트 문턱 — "그 배율에서 획 하나보다 얇은 요소는 화면에 존재하지 않는다".</summary>
        internal const float GateStrokes = 1.00f;

        /// <summary>하드 게이트 배율. 0.60 = 사용자 저장값, 0.75 = 출하 기본, 1.00 = 다이얼 최대.
        /// 양 끝은 숫자를 적지 않고 배포 상수에서 유도한다.</summary>
        private static readonly float[] GateScales =
        {
            0.60f, AccessoryShapeBuilder.ShippingCharacterScale, StickConfig.MaxCharacterScale
        };

        // ============================================================================
        // 면제 대장 — 못 고친 것을 <b>이름으로</b> 남긴다(잊히지 않게)
        // ============================================================================

        /// <summary>규칙 1-C를 아직 통과하지 못하는 도형. 좌표 작업은 <b>장비 담당 소관</b>(리더 경유)이라
        /// 이 라운드에서 고치지 않는다. 대신 대장에 남기고, <b>고쳐지면 이 테스트가 실패</b>해서
        /// 대장을 지우게 만든다(낡은 면제가 조용히 남는 것이 이 저장소의 반복 실패다).</summary>
        private readonly struct Exemption
        {
            public readonly EquipmentSlot Slot;
            public readonly int Item;
            public readonly string ShapeName;
            public readonly float RecordedRhoInR;
            public readonly string Reason;

            public Exemption(EquipmentSlot slot, int item, string shapeName, float recordedRhoInR, string reason)
            {
                Slot = slot; Item = item; ShapeName = shapeName;
                RecordedRhoInR = recordedRhoInR; Reason = reason;
            }
        }

        /// <summary>
        /// 2026-09-02 M6 적용 후의 위반 전부 = <b>4개</b>. 필요 ρ_max는 0.21818 R(1.00획),
        /// 권장은 0.26182 R(1.20획)이다.
        /// </summary>
        private static readonly Exemption[] Ledger =
        {
            new Exemption(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesPatch, "PatchEye", 0.1855f,
                "안대의 가려진 눈. 필요 +18% / 권장 +41%. 좌표 작업은 장비 담당 소관(리더 경유)."),
            new Exemption(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesMonocle, "MonocleEye", 0.1855f,
                "외알안경 렌즈 안쪽 눈. PatchEye와 같은 좌표 계열이라 함께 움직인다."),
            new Exemption(EquipmentSlot.Hair, AccessoryShapeBuilder.HairNeat, "HairPart", 0.1940f,
                "단정한머리 가르마. 필요 +13% / 권장 +36%."),
            new Exemption(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBandana, "BandanaTail", 0.1941f,
                "반다나 자락 삼각형. 필요 +13% / 권장 +35%."),
        };

        private static bool IsExempt(EquipmentSlot slot, int item, string shapeName)
        {
            for (int i = 0; i < Ledger.Length; i++)
            {
                if (Ledger[i].Slot == slot && Ledger[i].Item == item && Ledger[i].ShapeName == shapeName) return true;
            }
            return false;
        }

        // ============================================================================
        // 1. 게이트 — 채운 도형은 자기 윤곽선으로 침식하고도 색면 폭이 획 하나 이상이다
        // ============================================================================

        private static IEnumerable<TestCaseData> FilledItems()
        {
            foreach ((EquipmentSlot slot, int item) in AccessoryStrokeBudgetTests.BudgetedKeys())
            {
                yield return new TestCaseData(slot, item)
                    .SetName($"규칙1C {slot} {item}번({ItemCatalog.Item(slot, item).DisplayName})");
            }
        }

        [TestCaseSource(nameof(FilledItems))]
        public void 채운_도형은_윤곽선_침식_후에도_색면_폭이_획_하나_이상이다(EquipmentSlot slot, int itemIndex)
        {
            AccessoryShapeBuilder.Rig rig = AccessoryStrokeBudgetTests.Rig();
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, itemIndex, rig);
            string label = $"{slot} {itemIndex}번({ItemCatalog.Item(slot, itemIndex).DisplayName})";

            int filled = 0;
            for (int i = 0; i < sink.Count; i++)
            {
                AccessoryShapeBuilder.Shape shape = sink[i];
                if (!shape.Filled) continue;   // 낱선 20개는 이 규칙의 대상이 아니다(2.00pt 그대로).
                filled++;

                float rhoInR = MaxInscribedRadius(shape.Points) / rig.HeadRadius;
                bool exempt = IsExempt(slot, itemIndex, shape.Name);

                for (int g = 0; g < GateScales.Length; g++)
                {
                    float wOut = AccessoryShapeBuilder.FillOutlineBudgetInHeadRadii(GateScales[g]);
                    bool violates = rhoInR < wOut * GateStrokes - 1e-4f;

                    if (exempt)
                    {
                        // ★ 네거티브 컨트롤 — 면제가 <b>아직도 필요한가</b>. 고쳐졌으면 여기서 실패해서
                        //   대장을 지우게 만든다(낡은 면제가 조용히 남으면 대장은 거짓말이 된다).
                        Assert.IsTrue(violates,
                            $"{LogPrefix} {label} '{shape.Name}'이 배율 {GateScales[g]:F2}에서 이제 규칙 1-C를 " +
                            $"통과합니다(ρ_max {rhoInR:F4}R ≥ {wOut:F4}R) — 면제 대장에서 이 줄을 지우십시오. " +
                            "고쳐진 면제를 남겨 두면 대장이 '아직 못 고친 것'의 목록이 아니게 됩니다.");
                        continue;
                    }

                    Assert.IsFalse(violates,
                        $"{LogPrefix} {label} '{shape.Name}'의 최대 내접원 반경이 {rhoInR:F4}R로 " +
                        $"배율 {GateScales[g]:F2}의 윤곽선 펜 {wOut:F4}R보다 얇습니다 " +
                        $"({rhoInR / wOut:F2}획 < {GateStrokes:F2}획).\n" +
                        "폭 W 펜은 경계에 중심을 두므로 이 도형은 안쪽으로 W/2를 잃습니다 — " +
                        $"화면에서 채움색이 아니라 윤곽색(채움 × {AccessoryShapeBuilder.FillOutlineShadeFactor:F2}) " +
                        "한 덩어리로 보입니다.\n" +
                        $"필요: ρ_max ≥ {wOut:F4}R(1획) / 권장: {wOut * TargetStrokes:F4}R(1.20획, " +
                        "오프라인 모형이 낙관적이라 여유를 둡니다).\n" +
                        "못 고치는 도형이면 Ledger에 사유와 실측 ρ_max를 함께 등재하십시오.");
                }
            }

            Assert.Greater(filled, 0,
                $"{LogPrefix} {label}에 채운 도형이 하나도 없습니다 — 이 검사가 공허하게 통과했습니다. " +
                "린트 목록에 든 아이템은 최소 1개의 채움을 가집니다(규칙 2).");
        }

        // ============================================================================
        // 2. 전수표 + 대장 정합 — 대장이 <b>실제 위반 집합과 정확히 같은가</b>
        // ============================================================================

        [Test]
        public void 면제_대장은_실제_위반_집합과_정확히_일치한다()
        {
            AccessoryShapeBuilder.Rig rig = AccessoryStrokeBudgetTests.Rig();
            float wOut = AccessoryShapeBuilder.FillOutlineBudgetInHeadRadii(GateScales[0]);

            // 게이트 세 배율의 W_out이 실제로 같은지부터 확인한다 — 그것이 M6의 숨은 이득이고,
            // 아래 "한 번만 돌면 된다"의 근거다. 깨지면 배율마다 다시 재야 한다.
            for (int g = 1; g < GateScales.Length; g++)
            {
                Assert.AreEqual(wOut, AccessoryShapeBuilder.FillOutlineBudgetInHeadRadii(GateScales[g]), 1e-5f,
                    $"{LogPrefix} 게이트 배율 {GateScales[0]:F2}와 {GateScales[g]:F2}의 윤곽선 펜이 다릅니다 " +
                    "— 채움 경계선 하한이 게이트 구간에서 물리기 시작했습니다(M6의 전제가 깨졌습니다).");
            }

            var violating = new List<string>();
            var table = new StringBuilder();
            int filledTotal = 0, belowTarget = 0;

            foreach ((EquipmentSlot slot, int item) in AccessoryStrokeBudgetTests.BudgetedKeys())
            {
                var sink = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.Append(sink, slot, item, rig);
                for (int i = 0; i < sink.Count; i++)
                {
                    if (!sink[i].Filled) continue;
                    filledTotal++;
                    float rhoInR = MaxInscribedRadius(sink[i].Points) / rig.HeadRadius;
                    float strokes = rhoInR / wOut;
                    if (strokes < GateStrokes - 1e-4f) violating.Add($"{slot}/{item}/{sink[i].Name}");
                    if (strokes < TargetStrokes) belowTarget++;
                    table.Append($"{rhoInR:F4}R {strokes:F2}획 {slot}/{item}/{sink[i].Name}\n");
                }
            }

            Assert.AreEqual(Ledger.Length, violating.Count,
                $"{LogPrefix} 면제 대장은 {Ledger.Length}줄인데 실제 위반은 {violating.Count}개입니다 " +
                $"[{string.Join(", ", violating)}] — 대장과 현실이 갈라졌습니다.");

            for (int i = 0; i < violating.Count; i++)
            {
                string[] parts = violating[i].Split('/');
                var slot = (EquipmentSlot)System.Enum.Parse(typeof(EquipmentSlot), parts[0]);
                Assert.IsTrue(IsExempt(slot, int.Parse(parts[1]), parts[2]),
                    $"{LogPrefix} 위반 '{violating[i]}'이 면제 대장에 없습니다 — " +
                    "새 도형이 규칙 1-C를 어긴 채 들어왔습니다.");
            }

            // 대장에 적어 둔 실측값이 지금 값과 같은가(도형이 바뀌었는데 대장만 옛 숫자면 거짓말이다).
            for (int i = 0; i < Ledger.Length; i++)
            {
                var sink = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.Append(sink, Ledger[i].Slot, Ledger[i].Item, rig);
                bool found = false;
                for (int k = 0; k < sink.Count; k++)
                {
                    if (sink[k].Name != Ledger[i].ShapeName) continue;
                    found = true;
                    float rhoInR = MaxInscribedRadius(sink[k].Points) / rig.HeadRadius;
                    Assert.AreEqual(Ledger[i].RecordedRhoInR, rhoInR, 0.002f,
                        $"{LogPrefix} 대장의 '{Ledger[i].ShapeName}' 실측값({Ledger[i].RecordedRhoInR:F4}R)이 " +
                        $"지금 값({rhoInR:F4}R)과 다릅니다 — 도형이 바뀌었으면 대장도 함께 갱신하십시오.");
                }
                Assert.IsTrue(found,
                    $"{LogPrefix} 대장이 가리키는 도형 '{Ledger[i].ShapeName}'이 " +
                    $"{Ledger[i].Slot} {Ledger[i].Item}번에 없습니다 — 이름이 바뀌었거나 삭제됐습니다.");
            }

            Debug.Log($"{LogPrefix} 채운 도형 {filledTotal}개 / 게이트 펜 {wOut:F5}R / " +
                $"위반 {violating.Count}개(전부 대장 등재) / 권장 1.20획 미달 {belowTarget}개.\n" +
                table.ToString());
        }

        // ============================================================================
        // 3. 경고 구간(다이얼 최소) — 게이트는 아니지만 <b>반드시 보이게</b> 한다
        // ============================================================================

        /// <summary>
        /// 배율 0.35~0.509는 게이트에서 뺀다(실루엣 전용 구간). 그러나 <b>게이트에서 빼는 것과
        /// 안 보이게 하는 것은 다르다</b> — 여기서 수를 세어 로그로 남기고,
        /// <b>"색면이 완전히 0인 도형"이 하나도 없다</b>는 것만 단언한다.
        /// M6 이전에는 그 수가 <b>32개</b>였고, 그것이 "전부 다 조잡하다"의 가장 정확한 물리적 서술이다.
        /// </summary>
        [Test]
        public void 경고구간_다이얼_최소_배율에서도_색면이_0인_도형은_없다()
        {
            AccessoryShapeBuilder.Rig rig = AccessoryStrokeBudgetTests.Rig();
            float wOut = AccessoryShapeBuilder.FillOutlineBudgetInHeadRadii(StickConfig.MinCharacterScale);
            float oldPen = AccessoryShapeBuilder.StrokeBudgetInHeadRadii(StickConfig.MinCharacterScale);

            var zeroArea = new List<string>();
            var belowOneStroke = new List<string>();
            int oldZeroArea = 0, filledTotal = 0;
            float tightestRatio = float.MaxValue;   // ρ_max ÷ (W/2). 1.00이면 색면이 정확히 0이다.
            string tightestName = "-";

            foreach ((EquipmentSlot slot, int item) in AccessoryStrokeBudgetTests.BudgetedKeys())
            {
                var sink = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.Append(sink, slot, item, rig);
                for (int i = 0; i < sink.Count; i++)
                {
                    if (!sink[i].Filled) continue;
                    filledTotal++;
                    float rhoInR = MaxInscribedRadius(sink[i].Points) / rig.HeadRadius;
                    if (rhoInR <= wOut * 0.5f) zeroArea.Add($"{slot}/{item}/{sink[i].Name}");
                    if (rhoInR < wOut) belowOneStroke.Add($"{slot}/{item}/{sink[i].Name}");
                    float ratio = rhoInR / (wOut * 0.5f);
                    if (ratio < tightestRatio) { tightestRatio = ratio; tightestName = $"{slot}/{item}/{sink[i].Name}"; }
                    if (rhoInR <= oldPen * 0.5f) oldZeroArea++;   // 낱선 하한을 그대로 썼다면(= M6 이전).
                }
            }

            Assert.IsEmpty(zeroArea,
                $"{LogPrefix} 다이얼 최소 배율에서 <b>색면이 완전히 0</b>인 도형이 " +
                $"{zeroArea.Count}개 있습니다 [{string.Join(", ", zeroArea)}] — " +
                "그 배율에서 그 장비는 색이 옅어진 것이 아니라 윤곽색 한 덩어리가 됩니다. " +
                "42종이 서로 다른 색을 배정받고도 화면에서는 같은 어두운 톤으로 수렴합니다.");

            // ★ 네거티브 컨트롤 — 낱선 하한(2.00pt)을 그대로 썼다면 실제로 0이 되는 도형이 있었다.
            //   이 수가 0이 되면 위 단언이 공허해진 것이므로 함께 알린다.
            Assert.Greater(oldZeroArea, 0,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 낱선 하한({oldPen:F4}R)을 써도 색면이 0인 도형이 " +
                "하나도 없습니다. 그렇다면 M6의 이 이득이 이 환경에서는 검증되지 않습니다.");

            // ★ 이 구간의 여유는 <b>매우 얇다</b>. 그 사실을 숫자로 남긴다 — 나중에 이 단언이 깨지면
            //   "왜 갑자기?"가 아니라 "여유가 원래 X%였다"에서 시작할 수 있게.
            //   (참고: 이 검산은 프리팹 굽기 근사 35.25pt/유닛을 쓴다. 실제 실행 화면은 40.9167이라
            //    펜이 16% 얇고 여유는 그만큼 넓다 — 즉 이 판정은 보수적이다.)
            Debug.Log($"{LogPrefix} 경고 구간(배율 {StickConfig.MinCharacterScale:F2}) — 펜 {wOut:F4}R. " +
                $"채운 도형 {filledTotal}개 중 색면 0 = <b>{zeroArea.Count}개</b> " +
                $"(낱선 하한을 그대로 썼다면 {oldZeroArea}개), 1획 미만 {belowOneStroke.Count}개. " +
                $"가장 빠듯한 도형 = {tightestName} (ρ_max ÷ (W/2) = {tightestRatio:F3}, " +
                $"여유 {(tightestRatio - 1f) * 100f:F1}%). " +
                "이 구간은 게이트가 아니라 경고입니다(실루엣 전용 구간).");
        }

        // ============================================================================
        // ρ_max — 격자 분지한정(branch and bound)
        // ============================================================================

        /// <summary>
        /// 다각형의 <b>최대 내접원 반경</b>. 거리 함수가 1-Lipschitz라는 사실을 쓴다:
        /// 간격 h인 격자에서 임의의 점은 어떤 격자점으로부터 h/√2 안에 있으므로,
        /// <c>ρ(p) &lt; 최대 − h/√2</c>인 격자점의 셀에는 더 나은 점이 있을 수 없다.
        /// 그 셀만 버리고 나머지를 3분할해 좁혀 간다.
        ///
        /// <para><b>왜 단순 세밀 격자가 아닌가</b>: 망토(ρ_max = 1.36R)까지 0.001R 격자로 훑으면
        /// 도형 하나에 수백만 점이다. 반대로 성기게 훑고 <b>최고점 하나만</b> 국소 정밀화하면
        /// 국소 최적에 갇힌다 — 실제로 그렇게 짜서 GoggleStrap을 0.2175R(참값 0.2300R)로 재고
        /// <b>있지도 않은 위반 1건</b>을 만들어냈다. 분지한정은 그 실패가 구조적으로 불가능하다.</para>
        /// </summary>
        internal static float MaxInscribedRadius(Vector3[] polygon)
        {
            if (polygon == null || polygon.Length < 3) return 0f;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < polygon.Length; i++)
            {
                minX = Mathf.Min(minX, polygon[i].x); maxX = Mathf.Max(maxX, polygon[i].x);
                minY = Mathf.Min(minY, polygon[i].y); maxY = Mathf.Max(maxY, polygon[i].y);
            }
            float span = Mathf.Max(maxX - minX, maxY - minY);
            if (span <= 0f) return 0f;

            const float Tolerance = 1f / 4000f;   // 0.00025 월드유닛 ≒ 0.001R.
            float h = span / 48f;
            var candidates = new List<Vector2>(4096);
            for (float x = minX; x <= maxX + h * 0.5f; x += h)
            {
                for (float y = minY; y <= maxY + h * 0.5f; y += h) candidates.Add(new Vector2(x, y));
            }

            float best = float.MinValue;
            var next = new List<Vector2>(4096);
            var values = new List<float>(4096);
            while (true)
            {
                values.Clear();
                for (int i = 0; i < candidates.Count; i++)
                {
                    float v = SignedDistance(candidates[i], polygon);
                    values.Add(v);
                    if (v > best) best = v;
                }
                if (h <= Tolerance || candidates.Count == 0) break;

                float cutoff = best - h * 0.70711f;   // 1-Lipschitz 한계.
                float nh = h / 3f;
                next.Clear();
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (values[i] < cutoff) continue;
                    for (int a = -1; a <= 1; a++)
                    {
                        for (int b = -1; b <= 1; b++)
                        {
                            if (a == 0 && b == 0) continue;   // 자기 자신은 이미 쟀다.
                            next.Add(new Vector2(candidates[i].x + a * nh, candidates[i].y + b * nh));
                        }
                    }
                }
                (candidates, next) = (next, candidates);
                h = nh;
            }
            return Mathf.Max(0f, best);
        }

        /// <summary>안이면 경계까지의 거리(+), 밖이면 −거리.</summary>
        private static float SignedDistance(Vector2 p, Vector3[] poly)
        {
            int n = poly.Length;
            bool inside = false;
            float min = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = new Vector2(poly[i].x, poly[i].y);
                Vector2 b = new Vector2(poly[(i + 1) % n].x, poly[(i + 1) % n].y);
                if ((a.y > p.y) != (b.y > p.y))
                {
                    float xCross = a.x + (p.y - a.y) * (b.x - a.x) / (b.y - a.y);
                    if (p.x < xCross) inside = !inside;
                }
                min = Mathf.Min(min, SegmentDistance(p, a, b));
            }
            return inside ? min : -min;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 v = b - a;
            float lenSq = v.sqrMagnitude;
            if (lenSq < 1e-12f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, v) / lenSq);
            return Vector2.Distance(p, a + v * t);
        }
    }
}
