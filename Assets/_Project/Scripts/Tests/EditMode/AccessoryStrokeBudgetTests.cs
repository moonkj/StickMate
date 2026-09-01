using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 획 예산(Stroke Budget) 회귀 — 2026-09-01, docs/UX_FLOW.md 37-1 / 37-6 규칙 1.
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// ux-designer의 전수 진단이 밝힌 근본 원인은 취향이 아니라 <b>계측 가능한 규칙 위반</b>이었다:
    /// 이 앱의 액세서리 획은 화면상 2pt 하한이 걸려 배율 0.75에서 <b>0.344R</b>인데, 도형 좌표는
    /// 선 굵기가 0인 것처럼 설계돼 있었다. 그래서 "설계상 존재하지만 화면에는 없는" 디테일이
    /// 측정 26개 중 25개였다(가독 판정 1개).
    ///
    /// 값을 고치는 것만으로는 재발한다 — 다음 DLC가 같은 실수를 한다. 그래서 규칙을 <b>테스트로</b> 못박는다.
    ///
    /// ============================================================================
    /// 적용 범위 — 2026-09-01(3차) <b>30종 전부</b>
    /// ============================================================================
    /// 처음에는 HAIR·FX 반짝임 둘뿐이었고, 같은 날 EYES 6종(불투명 바이저 재설계, 38-7 E2)과
    /// NECK 방울 목걸이가 한 줄씩 합류했다. 이후 전수 실측(+9종) → 정리 라운드(+4종) → 목도리(+1종)로
    /// 27종까지 올라갔고, <b>장비 30종 도형 재설계</b>(docs/EQUIPMENT_SHAPE_SPEC.md)가 마지막 3종을 닫았다:
    ///
    /// <list type="bullet">
    ///   <item>HEAD 털모자 — 옛 띠(좌우 변 0.58획)가 <b>접힌 단</b>(높이 0.46R = 1.34획)이 됐다.</item>
    ///   <item>NECK 나비넥타이 — 매듭 잉크 사각형 0.91 → <b>1.63획</b>(반폭 0.13R → 0.28R).</item>
    ///   <item>NECK 줄무늬타이 — blade 폭 0.87 → <b>1.98획</b>, 줄무늬 2개(각 0.87획) → 채운 띠 1개.</item>
    /// </list>
    ///
    /// 그래서 <see cref="AccessoryRuleOneCoverageTests"/>의 <b>면제 대장이 비었다</b>. 대장이 비었다는
    /// 것은 "린트에서 빠진 도형이 하나도 없다"는 뜻이지 "앞으로 면제가 없다"는 뜻이 아니다 —
    /// 다음에 못 고치는 도형이 생기면 대장에 한 줄 적고 이 목록에서 그 아이템을 빼라.
    ///
    /// ============================================================================
    /// "최단 선분"을 그대로 재지 않는 이유
    /// ============================================================================
    /// 진단표는 도형별 최단 선분을 쟀지만, 그 지표를 그대로 단언하면 <b>매끄러운 곡선을 금지</b>하게 된다
    /// (곡선을 촘촘히 쪼갤수록 최단 선분은 짧아지지만 그림은 오히려 좋아진다).
    /// 실제 실패 모드는 "짧은 선분"이 아니라 <b>양끝이 모두 꺾임인 짧은 선분</b>, 즉
    /// <i>그리려다 만 점</i>이다. 그래서 이 파일은 그것을 잰다(<see cref="AssertNoStubSegments"/>).
    /// 곡선의 부드러움(연속된 완만한 꺾임)은 통과시키고, 획에 먹히는 꺾임만 잡는다.
    /// </summary>
    public sealed class AccessoryStrokeBudgetTests
    {
        /// <summary>꺾임으로 셀 최소 방향 변화(도). 이보다 완만하면 "같은 획이 이어진다"로 본다.
        /// <para><b>internal인 이유</b>: 커버리지 검사(<see cref="AccessoryRuleOneCoverageTests"/>)와
        /// 폼폼 검사가 같은 문턱을 봐야 한다. 각자 45를 적어 두면 문턱을 옮길 때 한쪽만 옮겨진다.</para></summary>
        internal const float CornerDegrees = 45f;

        /// <summary>배율 1.0 프리팹 실측 리그. 예산은 <b>배율에 무관한 R 배수</b>로 검산하므로
        /// 리그 배율이 아니라 <see cref="AccessoryShapeBuilder.ShippingCharacterScale"/>이 기준이다.</summary>
        internal static AccessoryShapeBuilder.Rig Rig(float facing = 1f)
        {
            const float H = StickConfig.BaselineCharacterTotalHeight;
            const float R = AccessoryShapeBuilder.BaselineHeadVisualRadius;
            return new AccessoryShapeBuilder.Rig(R, H - R,
                AccessoryShapeBuilder.BaselineShoulderLocalY,
                AccessoryShapeBuilder.BaselineHipLocalY, facing);
        }

        /// <summary>획 예산(월드 유닛) — 위 리그의 R에 대한 배수를 실제 길이로 바꾼 값.</summary>
        internal static float BudgetWorld(in AccessoryShapeBuilder.Rig rig)
            => AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii * rig.HeadRadius;

        /// <summary>도형 무리의 <b>최고점</b>(머리 중심 기준 R 배수) — 액자가 실제로 자르는 양이다.
        /// <para>단언과 네거티브 컨트롤이 <b>같은 함수</b>를 쓴다. 재는 방법을 두 번 적으면 두 자가
        /// 갈라지고, 그 순간 컨트롤은 자기가 무엇을 증명하는지 모르게 된다.</para></summary>
        internal static float TopInR(in AccessoryShapeBuilder.Rig rig,
            IList<AccessoryShapeBuilder.Shape> shapes)
        {
            float topY = float.MinValue;
            for (int i = 0; i < shapes.Count; i++)
            {
                Vector3[] pts = shapes[i].Points;
                if (pts == null) continue;
                for (int k = 0; k < pts.Length; k++) topY = Mathf.Max(topY, pts[k].y);
            }
            return (topY - rig.HeadCenterY) / rig.HeadRadius;
        }

        /// <summary>초상화 액자가 담기로 <b>정의한</b> 최고점(머리 중심 기준 R 배수).
        /// <c>private const</c>라 리플렉션이 닿지 않아 소스에서 읽는다
        /// (<see cref="SourceConstantReader"/>에 이유 전문).</summary>
        private static float PortraitFrameTopInR() => SourceConstantReader.ReadFloat(
            SourceConstantReader.PortraitStagePath, "TallestAccessoryAboveHeadCenterInR");

        /// <summary>HAIR 전종. 아래 <b>머리카락 전용</b> 검사(두피 부착·눈동자 회피)가 이 목록을 쓴다 —
        /// 다른 카테고리를 여기 넣으면 그 카테고리에 머리카락의 규칙을 요구하게 된다.</summary>
        private static IEnumerable<TestCaseData> HairItems()
        {
            yield return new TestCaseData(EquipmentSlot.Hair, AccessoryShapeBuilder.HairCowlick).SetName("HAIR 삐친머리");
            yield return new TestCaseData(EquipmentSlot.Hair, AccessoryShapeBuilder.HairNeat).SetName("HAIR 단정한머리");
            yield return new TestCaseData(EquipmentSlot.Hair, AccessoryShapeBuilder.HairCurly).SetName("HAIR 곱슬");
            yield return new TestCaseData(EquipmentSlot.Hair, AccessoryShapeBuilder.HairBald).SetName("HAIR 민머리");
            // 2026-09-01 신규 2종 — <b>임시 플레이스홀더지만</b> 규칙은 처음부터 지킨다. 여기 넣어 두지
            // 않으면 "나중에 넣자"가 영영 안 오고, 다음 DLC가 같은 실패를 반복한다(이 파일의 사용법).
            yield return new TestCaseData(EquipmentSlot.Hair, AccessoryShapeBuilder.HairBowl).SetName("HAIR 바가지머리");
            yield return new TestCaseData(EquipmentSlot.Hair, AccessoryShapeBuilder.HairPonytail).SetName("HAIR 포니테일");
        }

        /// <summary>
        /// 획 예산(규칙 1)을 통과하는 아이템 전부 — <b>2026-09-01(3차) 기준 30종 전부</b>.
        /// <para>가장 빠듯한 자리(실측): NECK 줄무늬타이 줄무늬 1.33획 · HEAD 털모자 접힌 단 1.34획 ·
        /// HEAD 야구모자 챙 닫힘변 1.34획 · NECK 반다나 자락 1.40획 · BACK 망토 옷깃 띠 1.28획
        /// (카드 정규화에서 <b>1.24획</b>까지 줄어드는 이 스펙 최악의 자리다).</para>
        /// <para>여기 빠진 아이템이 하나라도 생기면
        /// <see cref="AccessoryRuleOneCoverageTests.면제가_없는_아이템은_전부_린트_목록에_들어와_있다"/>가
        /// 빨간불을 낸다 — 사람이 기억할 일이 아니다.</para>
        /// </summary>
        private static IEnumerable<TestCaseData> BudgetedItems()
        {
            foreach (TestCaseData hair in HairItems()) yield return hair;

            // ---- HEAD 6종 전부. ★ 2026-09-01(3차) 털모자가 합류했다 — 옛 띠(좌우 변 0.58획)가
            //      접힌 단(cuff, 높이 0.46R = 1.34획)이 되면서 마지막 HEAD 면제가 닫혔다.
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap).SetName("HEAD 천모자");
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeanie).SetName("HEAD 털모자");
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora).SetName("HEAD 중절모");
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCrown).SetName("HEAD 왕관");
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret).SetName("HEAD 베레모");
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadStraw).SetName("HEAD 밀짚모자");

            // ---- NECK 6종 전부. ★ 2026-09-01(3차) 나비넥타이(매듭 0.91 -> 1.63획)와
            //      줄무늬타이(blade 폭 0.87 -> 1.98획, 줄무늬 2개 -> 채운 띠 1개)가 합류했다.
            yield return new TestCaseData(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBowTie).SetName("NECK 나비넥타이");
            yield return new TestCaseData(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped).SetName("NECK 줄무늬타이");
            yield return new TestCaseData(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckScarf).SetName("NECK 목도리");
            yield return new TestCaseData(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell).SetName("NECK 방울목걸이");
            yield return new TestCaseData(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckPendant).SetName("NECK 펜던트");
            yield return new TestCaseData(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBandana).SetName("NECK 반다나");

            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesSunglasses).SetName("EYES 선글라스");
            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesRound).SetName("EYES 동그란안경");
            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesGoggles).SetName("EYES 고글");
            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesMonocle).SetName("EYES 외알안경");
            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesBrowline).SetName("EYES 뿔테안경");
            yield return new TestCaseData(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesPatch).SetName("EYES 안대");

            // ---- BACK(EquipmentSlot.Shoulders). ★ 2026-09-01 마지막 정리 라운드에서 날개·배낭이
            //      합류해 이 카테고리는 <b>6종 전부</b>가 들어와 있다(면제 0개).
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackWings).SetName("BACK 날개");
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackBackpack).SetName("BACK 배낭");
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackCape).SetName("BACK 짧은망토");
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackLongCape).SetName("BACK 긴망토");
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackPoncho).SetName("BACK 판초");
            yield return new TestCaseData(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackFairyWings).SetName("BACK 요정날개");
        }

        /// <summary>위 목록을 <b>키(자리, 번호)</b>로만 훑는다. 커버리지 검사가 "린트에 들어온 아이템"을
        /// <b>이 목록 하나에서만</b> 읽게 하려는 것이다 — 두 벌로 적으면 한쪽만 늘어난다(이 저장소가
        /// 반복해서 겪은 이중 정의 계열 실패).</summary>
        internal static IEnumerable<(EquipmentSlot Slot, int Item)> BudgetedKeys()
        {
            foreach (TestCaseData data in BudgetedItems())
            {
                yield return ((EquipmentSlot)data.Arguments[0], (int)data.Arguments[1]);
            }
        }

        // ============================================================================
        // 0. 예산 자체가 유도값인가 (숫자를 손으로 적어 두면 신장/획이 바뀔 때 예산만 옛 값으로 남는다)
        // ============================================================================

        [Test]
        public void 획_예산은_실측_상수에서_유도된다()
        {
            Assert.AreEqual(0.048f,
                AccessoryShapeBuilder.StrokeWidthRatio * StickConfig.BaselineCharacterTotalHeight, 1e-6f,
                "액세서리 획의 비례 두께가 0.048(배율 1.0 실측)에서 벗어났습니다 — 단일 정의처를 옮기는 " +
                "과정에서 값이 바뀌면 몸/초상화/카드의 그림이 한꺼번에 달라집니다.");

            float shipping = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;
            float expectedPt = AccessoryShapeBuilder.BaselineHeadVisualRadius
                * AccessoryShapeBuilder.ShippingCharacterScale
                * StickConfig.ReferencePointsPerWorldUnitApprox * shipping;
            Assert.AreEqual(StickConfig.MinStrokeScreenPoints, expectedPt, 0.01f,
                "출하 기본 배율(0.75)에서 액세서리 획은 화면상 하한(2pt)에 걸려 있어야 합니다 — " +
                "이 등식이 깨지면 37-1의 실측표 전체가 다른 배율을 말하게 됩니다.");

            // 다이얼 최소(0.35)는 획이 0.74R이라 어떤 디테일도 불가능한 <b>실루엣 전용 구간</b>이다.
            // 그 사실을 테스트가 박제해 둔다 — 훗날 "왜 작은 배율에서 디테일이 안 보이나"의 답이 여기 있다.
            Assert.Greater(AccessoryShapeBuilder.StrokeBudgetInHeadRadii(0.35f), 0.7f,
                "다이얼 최소 배율의 획 예산이 0.7R 아래로 내려왔습니다 — 37-1-1의 전제가 바뀌었습니다.");
            Assert.Less(AccessoryShapeBuilder.StrokeBudgetInHeadRadii(1.5f), shipping,
                "배율을 키웠는데 획 예산(R 배수)이 줄지 않았습니다.");
        }

        // ============================================================================
        // 1. 규칙 1 — 그리려다 만 점이 없다 / 도형이 획보다 크다
        // ============================================================================

        [TestCaseSource(nameof(BudgetedItems))]
        public void 모든_도형이_획_예산을_지킨다(EquipmentSlot slot, int itemIndex)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float w = BudgetWorld(rig);
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, itemIndex, rig);

            string label = $"{slot} {itemIndex}번({ItemCatalog.Item(slot, itemIndex).DisplayName})";
            Assert.Greater(sink.Count, 0, $"{label}: 도형이 하나도 없습니다.");

            for (int i = 0; i < sink.Count; i++)
            {
                string violation = DescribeRuleOneViolation(sink[i], w);
                Assert.IsNull(violation, $"{label} {violation}");
            }
        }

        /// <summary>
        /// 도형 하나가 규칙 1을 어기는지 — 어기면 <b>사람이 읽는 설명</b>을, 통과하면 null을 돌려준다.
        /// <para><b>왜 던지지 않고 돌려주는가</b>: 같은 규칙을 커버리지 대장의 네거티브 컨트롤
        /// ("면제된 도형이 <b>아직도</b> 위반하는가")이 반대 방향으로 써야 한다. 규칙을 두 번 구현하면
        /// 두 자가 갈라지고, 그 순간 대장은 자기가 무엇을 면제하는지 모르게 된다.</para>
        /// </summary>
        internal static string DescribeRuleOneViolation(in AccessoryShapeBuilder.Shape shape, float w)
        {
            Vector3[] p = shape.Points;
            if (p == null || p.Length < 2) return $"'{shape.Name}'의 점이 2개 미만입니다.";

            Bounds(p, out Vector2 min, out Vector2 max);
            // ★ 2026-09-02 이름 정정 — 이것은 잉크 사각형의 <b>긴 변</b>이다(max(폭, 높이)).
            //   예전 이름("잉크 사각형")은 넓이/두께를 재는 것처럼 읽혀 규칙 1의 구멍을 가렸다:
            //   길이 5W · 두께 0.1W짜리 실오라기도 이 검사를 통과한다. 규칙 1에는 "가로질러 얼마나
            //   두꺼운가"를 재는 항목이 <b>하나도 없었고</b>, 그것이 61개가 전부 통과하고도 38개의
            //   색면이 30% 미만이던 이유다. 두께는 규칙 1-C(AccessoryFillAreaRuleTests)가
            //   최대 내접원 반경 ρ_max를 직접 재서 맡는다 — 이 검사는 "도형이 최소한의 크기는
            //   되는가"라는 <b>다른 목적</b>을 그대로 수행한다(값 1.5획 유지).
            float longSide = Mathf.Max(max.x - min.x, max.y - min.y);
            if (longSide < w * 1.5f)
            {
                return $"'{shape.Name}'의 잉크 사각형 <b>긴 변</b>이 {longSide / w:F2}획입니다 — " +
                    "1.5획 미만이면 화면에서 '뚱뚱한 점' 하나로 보입니다(37-6 규칙 1-A). " +
                    "※ 이 검사는 길이만 봅니다. 두께는 규칙 1-C(ρ_max)가 따로 잡습니다.";
            }

            return DescribeStubSegment(shape, w);
        }

        /// <summary>양끝이 <b>모두 꺾임</b>인데 획보다 짧은 선분 = 그리려다 만 점.</summary>
        private static string DescribeStubSegment(in AccessoryShapeBuilder.Shape shape, float w)
        {
            Vector3[] p = shape.Points;
            int n = p.Length;
            var corner = new bool[n];
            for (int i = shape.Loop ? 0 : 1; i < (shape.Loop ? n : n - 1); i++)
            {
                corner[i] = TurnDegrees(p[(i - 1 + n) % n], p[i], p[(i + 1) % n]) >= CornerDegrees;
            }

            int segments = shape.Loop ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % n;
                if (!corner[i] || !corner[j]) continue;
                float len = Vector3.Distance(p[i], p[j]);
                if (len >= w) continue;
                return $"'{shape.Name}'의 {i}->{j} 선분이 {len / w:F2}획입니다. 양끝이 모두 꺾임이므로 " +
                    "이 선분은 독립된 획으로 읽혀야 하는데, 획 하나보다 짧으면 통째로 먹혀 사라집니다.";
            }
            return null;
        }

        internal static float TurnDegrees(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector2 v1 = new Vector2(b.x - a.x, b.y - a.y);
            Vector2 v2 = new Vector2(c.x - b.x, c.y - b.y);
            if (v1.sqrMagnitude < 1e-12f || v2.sqrMagnitude < 1e-12f) return 0f;
            return Vector2.Angle(v1, v2);
        }

        private static void Bounds(Vector3[] pts, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < pts.Length; i++)
            {
                min = Vector2.Min(min, new Vector2(pts[i].x, pts[i].y));
                max = Vector2.Max(max, new Vector2(pts[i].x, pts[i].y));
            }
        }

        // ============================================================================
        // 2. 규칙 2·4·5 — 채움 / 부착 / 구성 정원
        // ============================================================================

        [TestCaseSource(nameof(HairItems))]
        public void 머리카락이_두피에_붙어있고_채워져_있다(EquipmentSlot slot, int itemIndex)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float w = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;   // R 배수
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, itemIndex, rig);
            string label = $"{slot} {itemIndex}번({ItemCatalog.Item(slot, itemIndex).DisplayName})";

            // 규칙 5 — 아이템 하나의 구성은 2~4개.
            Assert.That(sink.Count, Is.InRange(2, 4),
                $"{label}의 도형이 {sink.Count}개입니다 — 정원은 2~4개입니다(37-6 규칙 5). " +
                "5개를 넘으면 배율 0.75에서 서로 먹고, 1개면 실루엣만 있고 식별 특징이 없습니다.");

            // 규칙 2 — 두피를 가려야 하는 물건이므로 반드시 채운다.
            bool anyFilled = false;
            int accentCount = 0;
            for (int i = 0; i < sink.Count; i++)
            {
                if (sink[i].Filled) anyFilled = true;
                if (sink[i].Tone == AccessoryShapeBuilder.Accent) accentCount++;
            }
            Assert.IsTrue(anyFilled,
                $"{label}에 채움 도형이 없습니다 — 머리카락이 선화면 두피 링이 그대로 비쳐 " +
                "'머리 위에 그은 호'가 됩니다(37-6 규칙 2).");

            // 규칙 3-2 — 보조색은 "형제 셋과 나를 가르는 단 한 부분"에만.
            Assert.AreEqual(1, accentCount,
                $"{label}의 보조색 도형이 {accentCount}개입니다 — 정확히 1개여야 합니다(37-6 규칙 3).");

            // 규칙 4 — 두피 링(1.0R)을 최소 1획 파고든다. 어중간한 간격(0 < 간격 < 1획)이 최악이다.
            float minRadius = float.MaxValue, maxRadius = 0f;
            for (int i = 0; i < sink.Count; i++)
            {
                Vector3[] pts = sink[i].Points;
                for (int k = 0; k < pts.Length; k++)
                {
                    float radius = new Vector2(pts[k].x, pts[k].y - rig.HeadCenterY).magnitude / rig.HeadRadius;
                    minRadius = Mathf.Min(minRadius, radius);
                    maxRadius = Mathf.Max(maxRadius, radius);
                }
            }
            Assert.LessOrEqual(minRadius, 1f - w,
                $"{label}이 두피 링 안쪽으로 1획({w:F3}R)만큼도 들어오지 않습니다(가장 안쪽 {minRadius:F3}R) — " +
                "머리에서 자란 것이 아니라 위에 얹힌 것으로 보입니다(37-6 규칙 4). " +
                "옛 값(단정 1.13R / 곱슬 1.10R)이 정확히 이 실패였습니다.");
            Assert.GreaterOrEqual(maxRadius, 1.05f,
                $"{label}이 두피 링 바깥으로 나오지 않습니다(가장 바깥 {maxRadius:F3}R) — 머리 안에 묻힙니다.");

            // ★ 2026-09-01 — 여기 1.75f가 <b>하드코딩</b>돼 있었다. CLAUDE.md "테스트에 프로덕션 상수를
            //   숫자로 베끼지 않는다" 위반이고, 게다가 실제 액자 값(1.80R)과 <b>다른 숫자</b>였다.
            //   같은 메시지가 "액자가 1.80R까지만 담으므로 1.75R을 넘으면 잘린다"고 적고 있었으니
            //   문장 안에서 이미 앞뒤가 맞지 않았다. 이제 액자 상수를 그대로 읽는다.
            float topInR = TopInR(rig, sink);
            float frameTopInR = PortraitFrameTopInR();
            Assert.LessOrEqual(topInR, frameTopInR,
                $"{label}의 꼭대기가 머리 중심 위 {topInR:F3}R인데 초상화 액자의 기준 최고점은 " +
                $"{frameTopInR:F3}R입니다.\n" +
                "그 상수는 '지금 그릴 수 있는 가장 높은 점'으로 정의돼 있고, 액자 세로 크기를 거기서 " +
                "역산합니다(CharacterPortraitStage: 최고점 = H + 0.80R, 여백 5%). 넘으면 액자가 " +
                "그만큼 작게 계산되어 정보창에서 정수리가 잘립니다.\n" +
                "더 높이 솟는 것이 의도라면 CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR을 " +
                "함께 올리십시오 — 액자는 그 한 줄만 고치면 따라옵니다.");
        }

        /// <summary>
        /// 머리카락 <b>채움</b>은 눈동자를 덮으면 안 된다.
        /// <para>왜 테스트인가: 채움 면의 레이어는 <c>SortHair − 1 = 5</c>이고 눈동자(States/EyeController)도
        /// 5라 <b>동률</b>이다. 동률은 그리기 순서가 미정이라는 것이 이 프로젝트의 33-2-0 함정이다.
        /// 레이어를 옮겨 피하는 대신 <b>겹칠 일 자체가 없다</b>는 것을 기하로 잠근다 —
        /// 그 편이 다른 카테고리의 레이어 표를 건드리지 않는다.</para>
        /// </summary>
        [TestCaseSource(nameof(HairItems))]
        public void 머리카락_채움이_눈동자를_덮지_않는다(EquipmentSlot slot, int itemIndex)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, itemIndex, rig);

            // 눈동자 원(중심 + 상하좌우 끝)이 전부 채움 밖에 있어야 한다.
            float ex = rig.HeadRadius * AccessoryShapeBuilder.EyeOffsetXInHeadRadii;
            float ey = rig.HeadCenterY + rig.HeadRadius * AccessoryShapeBuilder.EyeOffsetYInHeadRadii;
            float er = rig.HeadRadius * (0.030f / AccessoryShapeBuilder.BaselineHeadVisualRadius);

            for (int i = 0; i < sink.Count; i++)
            {
                if (!sink[i].Filled) continue;
                foreach (float sx in new[] { 1f, -1f })
                {
                    foreach (Vector2 d in new[]
                    {
                        Vector2.zero, new Vector2(er, 0f), new Vector2(-er, 0f),
                        new Vector2(0f, er), new Vector2(0f, -er),
                    })
                    {
                        var probe = new Vector2(sx * ex + d.x, ey + d.y);
                        Assert.IsFalse(Contains(sink[i].Points, probe),
                            $"{slot} {itemIndex}번 '{sink[i].Name}'의 채움이 눈동자를 덮습니다 " +
                            $"(검사점 {probe}). 두피 안쪽 호(HairInnerRadiusRatio)가 끝나는 각도를 " +
                            "얼굴 쪽으로 더 열어야 합니다 — 얼굴은 그 호가 끝나는 앞·아래 사분면입니다.");
                    }
                }
            }
        }

        private static bool Contains(Vector3[] poly, Vector2 p)
        {
            bool inside = false;
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = poly[i], b = poly[(i + 1) % n];
                if ((a.y > p.y) != (b.y > p.y))
                {
                    float x = a.x + (p.y - a.y) * (b.x - a.x) / (b.y - a.y);
                    if (p.x < x) inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// ★ 2026-09-01(2차) 사용자 신고 <b>"머리스타일 옵션도 이정도 퀄이 되어야지"</b>의 정체를 잠근다.
        ///
        /// <para>조잡함은 취향이 아니라 <b>숫자 하나</b>였다. 배율 0.75에서 머리 지름은 획 5.82개뿐인데,
        /// 옛 5종은 정수리에서 두피 링 위로 <b>획 하나보다 얇게</b> 덮고 있었다 —
        /// 삐친 0.64획 · 단정 0.41획 · 곱슬 0.81획 · 포니테일 0.47획(통과는 바가지 1.51획 하나).
        /// 머리카락 윤곽선과 두피 링은 <b>각각 1획</b>이라, 그 사이가 1.5획 미만이면 화면에서 두 선이
        /// 한 줄로 뭉쳐 <b>"머리에 씌운 뚜껑"</b>이 된다. 페르소나가 유일하게 읽어낸 것이 통과한
        /// 바가지머리였다는 사실이 이 진단의 대조군이다.</para>
        ///
        /// <para>측정 구간이 정수리 <b>한 점</b>이 아니라 60~120도인 이유: 삐친머리는 봉우리와 골이
        /// 번갈아 도는 실루엣 자체가 정체라 90도가 골에 걸린다. 그 골 양옆의 봉우리(66·114도)가
        /// 덩어리를 만들므로, "정수리 <b>부근</b>에 획 1.5개짜리 덩어리가 있는가"를 묻는 것이 옳다.</para>
        ///
        /// <para>민머리는 제외한다 — <b>덩어리가 없는 것</b>이 그 아이템의 정체다(테 2조각).</para>
        /// </summary>
        [TestCase(0, TestName = "HAIR 삐친머리")]
        [TestCase(1, TestName = "HAIR 단정한머리")]
        [TestCase(2, TestName = "HAIR 곱슬")]
        [TestCase(4, TestName = "HAIR 바가지머리")]
        [TestCase(5, TestName = "HAIR 포니테일")]
        public void 머리카락이_정수리에서_두피_링_위로_1_5획_이상_덮는다(int itemIndex)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float wInR = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;
            float[] profile = AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Hair, itemIndex);

            float thickest = 0f;
            for (int b = 0; b < profile.Length; b++)
            {
                float deg = (b + 0.5f) * AccessorySilhouetteMetrics.BucketDegrees;
                if (deg < 60f || deg > 120f) continue;
                thickest = Mathf.Max(thickest, profile[b]);
            }

            float coverInStrokes = (thickest - 1f) / wInR;
            Assert.GreaterOrEqual(coverInStrokes, 1.5f,
                $"{ItemCatalog.Item(EquipmentSlot.Hair, itemIndex).DisplayName}이 정수리 부근에서 두피 링 위로 " +
                $"{coverInStrokes:F2}획만 덮습니다(가장 두꺼운 자리 {thickest:F2}R). 1.5획 미만이면 머리카락 윤곽선과 " +
                "두피 링이 화면에서 한 줄로 뭉쳐 '머리에 씌운 뚜껑'이 됩니다 — 사용자가 '조잡하다'고 " +
                "지적한 바로 그 상태입니다(EQUIPMENT_SHAPE_SPEC 4-1).");
            // ★ 2026-09-01 — 여기 있던 상한 단언을 <b>지웠다</b>. 지운 이유(디버거 규명 + 실측 재현):
            //
            //   (1) <b>차원이 다른 두 양을 비교</b>하고 있었다. thickest는 60~120도 구간의 <b>반경</b>인데
            //       상대인 HairCapMaxRatio는 근거를 "초상화 액자"라고 적었고, 액자가 자르는 것은
            //       <b>높이(y)</b>다. 돔에서는 정수리 반경 ≈ 높이라 둘이 우연히 같았지만, 삐친머리는
            //       봉우리가 66도·114도로 <b>기울어</b> 있어 그 등식이 깨진다
            //       (실측: 반경 1.78R인데 높이는 1.6261R).
            //   (2) 그래서 <b>실패 메시지가 사실이 아니었다</b> — 높이 1.6261R은 액자 1.80R 안이라
            //       잘리지 않는다. 같은 파일의 <b>진짜 높이 단언</b>(머리카락이_두피에_붙어있고_채워져_있다)은
            //       통과하고 있었다. <b>두 단언이 서로를 반증하고 있었다.</b>
            //   (3) HairCapMaxRatio는 <c>capRatio</c>(<b>돔 반경</b> 파라미터)의 범위 문서이고,
            //       삐친머리는 애초에 돔이 아니다(AccessoryShapeBuilder: "돔이 반경 일정한 호가 아니라
            //       봉우리 5개다"). 돔이 없는 헤어에 돔 반경 상한을 걸던 <b>범주 오류</b>였다.
            //   (4) 그 상수는 프로덕션에서 <b>한 번도 강제되지 않는다</b>(선언 + <param> 주석 + 이 테스트뿐).
            //
            //   높이 상한은 위 단언이 <b>헤어 6종 전부</b>에 걸고 있다(여기는 5종이었다). 즉 이 줄을
            //   지워서 잃는 커버리지는 없고, 오히려 민머리가 더해진다.
            //
            //   ※ 반경 상한을 되살리려면 <b>액자와 무관한 별도 근거</b>를 세우고 이름도 바꿔야 한다 —
            //     지금 이름·주석·사용처가 서로 다른 것을 가리킨다.
        }

        /// <summary>
        /// 머리카락이 <b>머리를 옆으로 감싼다</b> — 얹지 말고 감싼다(37-6 규칙 4의 측정 가능한 형태).
        ///
        /// <para>이것이 "덩어리"와 "뚜껑"을 가르는 두 번째 축이다. 정수리만 두꺼워지고 옆이 없으면
        /// 그냥 두꺼운 모자다. 옛 6종은 <b>하나도</b> 턱 근처까지 내려오지 않았다(최저 −0.68R).
        /// 지금은 여섯 종 다 관자놀이 바깥(|x| ≥ 0.85R)이면서 머리 중심 아래(y ≤ 0.05R)인 자리에
        /// 잉크를 갖는다 — 모자 카테고리에 거는 것과 <b>같은 판정선</b>이다.</para>
        ///
        /// <para>민머리도 포함한다. 덩어리가 없는 것이 정체이긴 하지만, 남은 테 2조각은
        /// <b>관자놀이와 뒤통수</b>에 있어야 "깎고 남은 머리"로 읽힌다.</para>
        /// </summary>
        [TestCaseSource(nameof(HairItems))]
        public void 머리카락이_머리를_옆으로_감싼다(EquipmentSlot slot, int itemIndex)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, itemIndex, rig);

            bool wraps = false;
            float lowest = float.MaxValue;
            for (int i = 0; i < sink.Count; i++)
            {
                Vector3[] pts = sink[i].Points;
                for (int k = 0; k < pts.Length; k++)
                {
                    float x = pts[k].x / rig.HeadRadius;
                    float y = (pts[k].y - rig.HeadCenterY) / rig.HeadRadius;
                    lowest = Mathf.Min(lowest, y);
                    if (Mathf.Abs(x) >= 0.85f && y <= 0.05f) wraps = true;
                }
            }

            Assert.IsTrue(wraps,
                $"{ItemCatalog.Item(slot, itemIndex).DisplayName}에 관자놀이 바깥(|x| ≥ 0.85R)이면서 " +
                $"머리 중심 아래(y ≤ 0.05R)인 잉크가 없습니다(가장 낮은 잉크 {lowest:F2}R) — " +
                "머리를 감싼 것이 아니라 위에 얹힌 것입니다(37-6 규칙 4).");
        }

        /// <summary>
        /// ★ 모자도 <b>얹지 말고 감싼다</b> — 사용자 신고 "장비들 모양이 너무 조잡해"의 HEAD 쪽 정체.
        ///
        /// <para>옛 6종의 커버선은 전부 머리 중심 <b>위</b>였다(캡 +0.62 / 털모자 +0.42 / 중절모 +0.58 /
        /// 베레모 +0.46 / 밀짚 +0.56 R). 즉 모자가 머리 위쪽 1/3에만 얹혀 있었고, 그래서 "쓴 것"이
        /// 아니라 "올려 둔 것"으로 보였다. 참고 이미지의 모자는 전부 머리를 <b>옆으로 감싸고 뒤로 뻗는다</b>.</para>
        ///
        /// <para>판정선은 머리카락에 쓰는 것과 <b>같다</b>: 관자놀이 바깥(|x| ≥ 0.85R)이면서 머리 중심
        /// 아래(y ≤ 0.05R)인 잉크가 있는가. 두 카테고리가 같은 자를 쓰는 것이 중요하다 —
        /// 모자와 머리는 같은 머리 위에서 서로를 자르는 사이다.</para>
        ///
        /// <para><b>왕관은 면제다.</b> 스스로 "얹는 물건"이라 선언하기 때문이고, 그 선언은 if 분기가
        /// 아니라 <see cref="AccessoryShapeBuilder.HatCoverLocalY"/>가 돌려주는 +∞다. 그래서 이 검사도
        /// 아이템 이름이 아니라 <b>커버선이 유한한가</b>로 면제를 가른다 — 새 모자가 늘어도 규약이 따라온다.</para>
        /// </summary>
        [TestCase(0, TestName = "HEAD 야구모자")]
        [TestCase(1, TestName = "HEAD 털모자")]
        [TestCase(2, TestName = "HEAD 중절모")]
        [TestCase(3, TestName = "HEAD 왕관")]
        [TestCase(4, TestName = "HEAD 베레모")]
        [TestCase(5, TestName = "HEAD 밀짚모자")]
        public void 모자가_머리를_감싸고_커버선이_머리_중심_언저리까지_내려온다(int itemIndex)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, EquipmentSlot.Head, itemIndex, rig);
            string label = ItemCatalog.Item(EquipmentSlot.Head, itemIndex).DisplayName;

            float cover = AccessoryShapeBuilder.HatCoverLocalY(itemIndex, rig);
            if (float.IsPositiveInfinity(cover))
            {
                // 왕관 — 얹는 물건이라고 스스로 선언한 아이템. 감쌈을 요구하지 않는다.
                return;
            }

            float coverInR = (cover - rig.HeadCenterY) / rig.HeadRadius;
            Assert.LessOrEqual(coverInR, 0.10f,
                $"{label}의 커버선이 머리 중심 위 {coverInR:F2}R입니다 — 0.10R을 넘으면 모자가 " +
                "머리 위쪽 1/3에만 얹힌 것이고, 그 밑으로 머리카락이 통째로 드러납니다(옛 6종이 그 상태였습니다).");

            bool wraps = false;
            for (int i = 0; i < sink.Count; i++)
            {
                Vector3[] pts = sink[i].Points;
                for (int k = 0; k < pts.Length; k++)
                {
                    float x = pts[k].x / rig.HeadRadius;
                    float y = (pts[k].y - rig.HeadCenterY) / rig.HeadRadius;
                    if (Mathf.Abs(x) >= 0.85f && y <= 0.05f) wraps = true;
                }
            }
            Assert.IsTrue(wraps,
                $"{label}에 관자놀이 바깥(|x| ≥ 0.85R)이면서 머리 중심 아래(y ≤ 0.05R)인 잉크가 없습니다 — " +
                "머리에 씌운 것이 아니라 위에 올려 둔 것입니다(37-6 규칙 4).");
        }

        /// <summary>점 하나가 <b>양옆 두 점을 잇는 선</b>에서 가장 멀리 벗어난 거리(월드 유닛).
        /// 물결 한 굽이의 깊이를 재는 자다 — 폭(x 진폭)만 재면 커튼이 기울어졌을 때 값이 부풀려진다.</summary>
        private static float DeepestBend(in AccessoryShapeBuilder.Shape shape)
        {
            Vector3[] p = shape.Points;
            int n = p.Length;
            float worst = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = p[(i - 1 + n) % n], b = p[i], c = p[(i + 1) % n];
                var dir = new Vector2(c.x - a.x, c.y - a.y);
                float len = dir.magnitude;
                if (len < 1e-6f) continue;
                float cross = (b.x - a.x) * dir.y - (b.y - a.y) * dir.x;
                worst = Mathf.Max(worst, Mathf.Abs(cross) / len);
            }
            return worst;
        }

        // ============================================================================
        // 3. 사용자 지적의 핵심 — 곱슬은 곱슬이어야 한다
        // ============================================================================

        /// <summary>
        /// 옛 곱슬의 웨이브 진폭은 0.16R = 0.93pt인데 획 <b>반폭</b>이 1.00pt였다.
        /// 즉 물결이 획 안에 통째로 매몰되어 <b>곱슬 ≡ 단정</b>(가르마만 뺀 것)이었다.
        /// 그 등식이 되살아나지 않도록 두 가지를 함께 잠근다: 진폭 자체와, 두 실루엣의 실제 차이.
        /// </summary>
        [Test]
        public void 곱슬은_획보다_큰_물결을_갖고_단정과_기하학적으로_다르다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float wInR = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;

            Assert.GreaterOrEqual(AccessoryShapeBuilder.CurlAmplitudeRatio * 2f, wInR * 1.5f,
                $"곱슬 웨이브의 마루-골 차이가 {AccessoryShapeBuilder.CurlAmplitudeRatio * 2f / wInR:F2}획입니다 — " +
                "1.5획 미만이면 두 물결이 붙어 한 덩어리가 되고, 곱슬은 그냥 호가 됩니다(37-3 (A) 2).");
            Assert.Greater(AccessoryShapeBuilder.CurlAmplitudeRatio, wInR * 0.5f,
                "웨이브 진폭이 획 반폭보다 작습니다 — 물결이 자기 획 안에 매몰됩니다(옛 0.16R의 실패 그대로).");

            // ★ 2026-09-01(2차) — 상수만 재면 <b>상수가 좌표를 만들지 않게 된 순간</b> 이 검사가
            //   공허해진다. 물결은 정수리에서 <b>커튼</b>으로 옮겨 갔고(정수리에 실으면 뚜껑 방지
            //   하한과 액자 상한이 동시에 만족 불가능하다 — EQUIPMENT_SHAPE_SPEC 4-3), 그 뒤로
            //   좌표는 표에서 온다. 그래서 <b>출하 도형에서 직접</b> 굽이의 깊이를 잰다.
            float measured = DeepestBend(AccessorySilhouetteMetrics.Find(
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Hair, AccessoryShapeBuilder.HairCurly),
                "HairMass")) / rig.HeadRadius;
            Assert.GreaterOrEqual(measured, AccessoryShapeBuilder.CurlAmplitudeRatio,
                $"곱슬 커튼에서 가장 깊은 굽이가 {measured:F3}R({measured / wInR:F2}획)뿐입니다 — " +
                $"선언한 진폭 {AccessoryShapeBuilder.CurlAmplitudeRatio:F3}R보다 얕으면 상수가 거짓말을 하는 것이고, " +
                "곱슬은 다시 '물결 없는 덩어리'가 됩니다.");

            // 실제로 그려지는 두 실루엣이 얼마나 다른가 — 같은 각도에서의 반경 차이 최대값.
            float maxDelta = MaxRadiusDelta(rig, AccessoryShapeBuilder.HairCurly, AccessoryShapeBuilder.HairNeat);
            Assert.Greater(maxDelta, wInR,
                $"곱슬과 단정한머리의 실루엣 반경 차이가 최대 {maxDelta / wInR:F2}획뿐입니다 — " +
                "획 하나보다 작으면 두 아이템이 화면에서 같은 그림입니다.");
        }

        /// <summary>
        /// 여섯 종이 서로 다른 실루엣인지 — "형제들과 구분되는 것이 아이템의 존재 이유"(규칙 7-3).
        /// <para>★ 2026-09-01 <b>4종 -> 6종</b>. 바가지머리/포니테일이 빠져 있던 이유는 이 파일이
        /// "바가지머리의 식별 특징은 내부 선이라 반경 지표로는 원리적으로 못 잡는다"고 적어 둔 것인데,
        /// 같은 날 페르소나가 실물에서 "바가지 ≡ 단정"을 확인했다. 원인은 둘이었다:
        /// <b>(1)</b> 도형 — 정체가 실루엣이 아니라 내부 선이었다(<see cref="AccessoryShapeBuilder.BowlSilhouette"/>로
        /// 재설계). <b>(2)</b> 지표 — 옛 프로파일은 <b>정점만</b> 상반구에서 훑어, 잉크가 없는 각도를
        /// 0으로 세는 바람에 <b>같은 쌍을 3.77획으로 부풀렸다</b>. 지금은 변을 조밀 표본하고 360도를 다 본다
        /// (<see cref="AccessorySilhouetteMetrics"/>). 그 두 가지를 함께 고쳐야 이 검사가 신호가 된다.</para>
        /// <para>지표가 실제로 빨간불을 낼 수 있다는 증명은
        /// <see cref="AccessorySilhouetteDistinctionTests"/>의 네거티브 컨트롤에 있다.</para>
        /// </summary>
        [Test]
        public void 머리_6종이_서로_구분된다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float wInR = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;
            int[] items =
            {
                AccessoryShapeBuilder.HairCowlick, AccessoryShapeBuilder.HairNeat,
                AccessoryShapeBuilder.HairCurly, AccessoryShapeBuilder.HairBald,
                AccessoryShapeBuilder.HairBowl, AccessoryShapeBuilder.HairPonytail,
            };

            for (int a = 0; a < items.Length; a++)
            {
                for (int b = a + 1; b < items.Length; b++)
                {
                    float delta = MaxRadiusDelta(rig, items[a], items[b]);
                    Assert.Greater(delta, wInR,
                        $"{ItemCatalog.Item(EquipmentSlot.Hair, items[a]).DisplayName}와 " +
                        $"{ItemCatalog.Item(EquipmentSlot.Hair, items[b]).DisplayName}의 실루엣 차이가 " +
                        $"{delta / wInR:F2}획뿐입니다.");
                }
            }
        }

        /// <summary>두 아이템의 실루엣 반경 프로파일이 가장 크게 벌어지는 값(R 배수).
        /// 계측 자체는 <see cref="AccessorySilhouetteMetrics"/>가 갖는다 — NECK 검사와 <b>같은 자</b>를
        /// 써야 두 카테고리의 수치를 나란히 읽을 수 있다.</summary>
        private static float MaxRadiusDelta(in AccessoryShapeBuilder.Rig rig, int itemA, int itemB)
            => AccessorySilhouetteMetrics.MaxRadiusDelta(
                AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Hair, itemA),
                AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Hair, itemB));

        // ============================================================================
        // 4. FX 반짝임 — 갈래가 획보다 <b>확실히</b> 길다
        // ============================================================================

        [Test]
        public void 반짝임_십자는_획보다_확실히_크다()
        {
            float wInR = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;
            float arm = AppearanceShapeBuilder.SparkleArmInR;

            Assert.GreaterOrEqual(arm, wInR * 2f,
                $"반짝임 한 갈래가 {arm / wInR:F2}획입니다 — 옛 값 0.34R은 정확히 1.00획이라 " +
                "4갈래 반짝임이 아니라 '뚱뚱한 십자 점'이었습니다(37-3 (F)(1)).");

            // 갈래가 커진 만큼 발동 높이도 함께 올라가야 한다 — 아래 갈래 끝이 머리 링을 뚫으면 안 된다.
            Vector3[] vertical = AppearanceShapeBuilder.SparkleStroke(arm, 0);
            Vector3[] horizontal = AppearanceShapeBuilder.SparkleStroke(arm, 1);
            float across = arm * AppearanceShapeBuilder.SparkleHorizontalArmRatio;
            Assert.AreEqual(arm * 2f, Vector3.Distance(vertical[0], vertical[1]), 1e-5f);
            Assert.AreEqual(across * 2f, Vector3.Distance(horizontal[0], horizontal[1]), 1e-5f);

            // ★ 2026-09-01 — 가로와 세로가 <b>달라야</b> 한다. 옛 값은 정확히 같아서 화면에 뜨는 그림이
            //   반짝임이 아니라 <b>더하기 기호</b>였다(docs/EQUIPMENT_SHAPE_SPEC_FXPET.md 4-2).
            Assert.Greater((arm - across) * 2f, wInR,
                $"가로 갈래와 세로 갈래의 길이 차가 {((arm - across) * 2f) / wInR:F2}획입니다 — " +
                "1획 미만이면 두 획이 같은 길이로 보여 4갈래 별이 아니라 '＋'가 됩니다.");

            // 짧은 쪽도 여전히 규칙 1을 지켜야 한다(짧게 만드는 것이 목적이지 없애는 것이 아니다).
            Assert.GreaterOrEqual(across, wInR * 1.5f,
                $"가로 갈래가 {across / wInR:F2}획입니다 — 1.5획 미만이면 둥근 캡에 먹혀 갈래가 사라집니다.");
        }

        // ============================================================================
        // ★ 액자 높이 — 네거티브 컨트롤 (2026-09-01 삐친머리 오탐 라운드)
        // ============================================================================

        /// <summary>
        /// 높이로 바꾼 단언이 <b>실제로 무언가를 잡는지</b> 증명한다.
        /// <para>리더 지시: "높이로 바꿨는데 어떤 좌표도 못 잡으면 그건 또 하나의 거짓 초록이다."
        /// 실제로 이 저장소는 같은 파일에서 <c>StringAssert.Contains</c>가 <b>XML 주석에만</b> 걸려
        /// Windows 단언이 한 번도 구현을 검사한 적이 없던 사고를 겪었다.</para>
        /// </summary>
        [Test]
        public void 네거티브_컨트롤_액자_높이_단언이_실제로_잡는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float frame = PortraitFrameTopInR();

            // (a) 액자를 <b>한 획</b> 넘는 합성 도형은 반드시 잡힌다.
            float over = frame + AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;
            var tooTall = new List<AccessoryShapeBuilder.Shape>
            {
                new AccessoryShapeBuilder.Shape("TooTall", new[]
                {
                    new Vector3(0f, rig.HeadCenterY),
                    new Vector3(0f, rig.HeadCenterY + rig.HeadRadius * over),
                }, false, 10),
            };
            Assert.Greater(TopInR(rig, tooTall), frame,
                "액자를 한 획 넘는 도형을 넣었는데 측정이 액자 안이라고 합니다 — 측정이 고장났습니다.");

            // (b) 액자 <b>바로 아래</b>는 잡히지 않는다(과민하지 않다).
            var justUnder = new List<AccessoryShapeBuilder.Shape>
            {
                new AccessoryShapeBuilder.Shape("JustUnder", new[]
                {
                    new Vector3(0f, rig.HeadCenterY),
                    new Vector3(0f, rig.HeadCenterY + rig.HeadRadius * (frame - 0.01f)),
                }, false, 10),
            };
            Assert.LessOrEqual(TopInR(rig, justUnder), frame,
                "액자 바로 아래인데 넘었다고 합니다 — 이 단언은 정상 아이템도 막게 됩니다.");

            // (c) ★ 이번 오탐의 핵심 — <b>반경</b>으로 재면 잡히고 <b>높이</b>로 재면 안 잡히는
            //     실제 사례가 삐친머리다. 그 차이가 실재함을 못 박는다.
            List<AccessoryShapeBuilder.Shape> cowlick = AccessorySilhouetteMetrics.Build(
                rig, EquipmentSlot.Hair, AccessoryShapeBuilder.HairCowlick);
            float[] profile = AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Hair,
                AccessoryShapeBuilder.HairCowlick);

            float crownRadius = 0f;
            for (int b = 0; b < profile.Length; b++)
            {
                float deg = (b + 0.5f) * AccessorySilhouetteMetrics.BucketDegrees;
                if (deg < 60f || deg > 120f) continue;
                crownRadius = Mathf.Max(crownRadius, profile[b]);
            }
            float cowlickTop = TopInR(rig, cowlick);

            Assert.Greater(crownRadius, cowlickTop,
                "삐친머리의 정수리 반경이 높이보다 크지 않습니다 — 봉우리가 기울어 있다는 전제가 " +
                "깨졌다면 이 라운드의 진단 자체를 다시 해야 합니다.");
            Assert.Less(cowlickTop, frame,
                $"삐친머리 높이 {cowlickTop:F4}R이 액자 {frame:F4}R를 넘습니다 — 옛 실패 메시지" +
                "(\"정보창에서 정수리가 잘립니다\")가 사실이 되었다는 뜻이므로 재진단이 필요합니다.");

            Debug.Log($"[액자] 삐친머리 — 정수리 반경 {crownRadius:F4}R vs 실제 높이 {cowlickTop:F4}R " +
                      $"(액자 {frame:F4}R, 여유 {frame - cowlickTop:F4}R). 옛 반경 상한은 이 차이 때문에 오탐했다.");
        }

        /// <summary>
        /// ★ 액자 상수가 <b>슬랙이 되지 않았는가</b> — 30종 중 실제 최고점과 한 획 이내여야 한다.
        /// <para>위 단언이 "아무것도 안 잡는 느슨한 상한"이 되는 것을 막는다. 액자 상수는
        /// "지금 그릴 수 있는 가장 높은 점"으로 <b>정의</b>돼 있으므로, 실제 최고점이 그보다 한 획
        /// 넘게 낮아졌다면 그 정의가 이미 낡은 것이다(액자만 필요 이상으로 커져 캐릭터가 작아진다).</para>
        /// </summary>
        [Test]
        public void 액자_기준_최고점이_실제_최고_아이템과_한_획_이내다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float frame = PortraitFrameTopInR();
            float stroke = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;

            float tallest = float.MinValue;
            string tallestLabel = "(없음)";
            foreach (TestCaseData item in BudgetedItems())
            {
                var slot = (EquipmentSlot)item.Arguments[0];
                var index = (int)item.Arguments[1];
                float top = TopInR(rig, AccessorySilhouetteMetrics.Build(rig, slot, index));
                if (top <= tallest) continue;
                tallest = top;
                tallestLabel = $"{slot} {index}번({ItemCatalog.Item(slot, index).DisplayName})";
            }

            Assert.LessOrEqual(tallest, frame,
                $"{tallestLabel}의 꼭대기 {tallest:F4}R이 액자 기준 {frame:F4}R을 넘습니다 — " +
                "CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR을 함께 올리십시오.");

            Assert.LessOrEqual(frame - tallest, stroke,
                $"액자 기준({frame:F4}R)이 실제 최고점({tallestLabel} {tallest:F4}R)보다 " +
                $"{frame - tallest:F4}R 높습니다 — 한 획({stroke:F4}R)을 넘는 슬랙입니다.\n" +
                "그 상수는 '지금 그릴 수 있는 가장 높은 점'으로 정의돼 있고 액자 크기를 거기서 " +
                "역산하므로, 슬랙이 크면 <b>액자만 커지고 캐릭터가 그만큼 작게</b> 그려집니다. " +
                "가장 높던 아이템이 낮아졌다면 그 상수도 함께 내리십시오.");

            Debug.Log($"[액자] 30종 최고점 = {tallestLabel} {tallest:F4}R / 액자 기준 {frame:F4}R " +
                      $"(슬랙 {frame - tallest:F4}R, 한 획 {stroke:F4}R).");
        }

        /// <summary>액자 상수를 <b>소스에서 실제로</b> 읽는가 — 못 찾으면 조용히 0이 되면 안 된다.</summary>
        [Test]
        public void 네거티브_컨트롤_액자_상수를_소스에서_실제로_읽는다()
        {
            Assert.IsTrue(SourceConstantReader.TryReadFloat(SourceConstantReader.PortraitStagePath,
                "TallestAccessoryAboveHeadCenterInR", out float frame),
                "액자 상수를 못 찾았습니다 — 이름이 바뀌었다면 이 테스트도 함께 갱신해야 합니다.");
            Assert.Greater(frame, 1f, $"액자 상수를 {frame}로 읽었습니다 — 머리 반경보다 커야 합니다.");

            Assert.IsFalse(SourceConstantReader.TryReadFloat(SourceConstantReader.PortraitStagePath,
                "존재하지않는액자상수Xyz", out float _),
                "없는 상수를 '찾았다'고 보고했습니다 — 정규식이 아무거나 물고 있습니다.");
        }

    }
}
