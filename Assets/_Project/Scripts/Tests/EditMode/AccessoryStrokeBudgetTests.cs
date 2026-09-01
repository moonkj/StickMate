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
    /// 적용 범위 — 2026-09-01 <b>30종 중 26종</b>(전수 실측 후 넣을 수 있는 것을 전부 넣었다)
    /// ============================================================================
    /// 처음에는 HAIR·FX 반짝임 둘뿐이었고, 같은 날 EYES 6종(불투명 바이저 재설계, 38-7 E2)과
    /// NECK 방울 목걸이가 한 줄씩 합류했다.
    ///
    /// <b>2026-09-01 마지막 라운드에서 30종을 전수 실측</b>해 통과하는 것을 남김없이 넣었다:
    /// HEAD 왕관·베레모·밀짚모자 / NECK 펜던트·반다나 / BACK 짧은망토·긴망토·판초·요정날개.
    /// "일부만 덮으면 다음에 또 조용히 위반이 들어온다"는 것이 이 확장의 이유다.
    ///
    /// <b>같은 날 마지막 정리 라운드</b>가 천모자·중절모·날개·배낭 4종의 위반을 닫아 22 -> 26종이 됐다
    /// (각각 챙 뿌리 두께 / 크리스 반폭 / 깃의 어깨 꼭짓점 / 끈 끝점 — 전부 한두 상수다).
    ///
    /// 남은 4종(HEAD 털모자 / NECK 나비넥타이·줄무늬타이·목도리)은
    /// <b>아직 통과하지 못한다</b> — 여기에 넣으면 지금 당장 빨간불이 되고, 빨간불이 상시화되면
    /// 테스트가 신호이기를 그만둔다. 대신 그 4종은 <see cref="AccessoryRuleOneCoverageTests"/>의
    /// <b>면제 대장(ledger)</b>에 도형 이름과 실측값까지 적혀 있고, 거기서
    /// <b>면제되지 않은 나머지 도형은 전부 검사된다</b>. 즉 "린트에서 빠진 아이템"은 이제 없다 —
    /// 아이템 단위로 빠지는 것이 아니라 <b>도형 단위로 면제</b>되고, 면제는 하나하나 근거가 적혀 있다.
    ///
    /// 그것들이 재설계되는 라운드에 <see cref="BudgetedItems"/>에 한 줄씩 추가하는 것이 이 파일의 사용법이고,
    /// 그때 면제 대장에서 지우는 것을 잊으면 커버리지 검사가 빨개진다(그쪽이 스스로 잡는다).
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
        /// 획 예산(규칙 1)을 <b>이미 통과하는</b> 아이템 전부. 2026-09-01 전수 실측 기준 <b>30종 중 26종</b>.
        /// <para>★ EYES 6종 — 이날 <b>불투명 바이저</b>로 전면 재설계되면서(38-7 E2) 처음으로 통과했다.
        /// 가장 빠듯한 자리는 고글 좌우 변 1.12획과 동그란안경 팟 간격 1.51획이다.</para>
        /// <para>★ NECK 방울 목걸이 — 규칙 1 위반(잉크 사각형 0.99획)으로 지목돼 채움 + 지름 1.63획으로 고쳤다.</para>
        /// <para>★ <b>이번 확장분 9종</b>(실측값은 잉크 사각형의 최솟값):
        /// HEAD 왕관 4.94획 · 베레모 6.40획 · 밀짚모자 4.54획 /
        /// NECK 펜던트 3.61획 · 반다나 3.09획 /
        /// BACK 짧은망토 11.56획 · 긴망토 15.95획 · 판초 8.93획 · 요정날개 3.20획.
        /// 아홉 종 다 <b>고치지 않고도 이미 통과</b>했다 — 지금까지 재 보지 않았을 뿐이다.
        /// 그게 이 확장의 요점이다: 통과하는데 빠져 있는 자리가 커버리지 구멍이다.</para>
        /// <para>★ <b>2026-09-01 마지막 정리분 4종</b>(이쪽은 반대로 <b>고쳐서</b> 들어왔다):
        /// HEAD 천모자(챙 뿌리 닫힘변 0.29 -> 1.11획) · 중절모(크리스 잉크 1.26 -> 1.68획) /
        /// BACK 날개(두 깃의 어깨 닫힘변 0.90·0.86 -> 1.20·1.20획) ·
        /// 배낭(끈 잉크 1.32 -> 2.30획, 덤으로 끈 끝점이 몸에서 0.64획 떠 있던 규칙 4 결함도 닫혔다).</para>
        /// <para><b>못 들어온 4종</b>과 그 이유(도형 이름 · 실측값)는
        /// <see cref="AccessoryRuleOneCoverageTests"/>의 면제 대장에 한 줄씩 적혀 있다.
        /// 넣으면 지금 당장 빨간불이 되고, 빨간불이 상시화되면 테스트가 신호이기를 그만둔다.</para>
        /// </summary>
        private static IEnumerable<TestCaseData> BudgetedItems()
        {
            foreach (TestCaseData hair in HairItems()) yield return hair;

            // ---- HEAD. 남은 면제는 털모자 띠(0.58획) 하나뿐이다 — 천모자·중절모는 2026-09-01
            //      마지막 정리 라운드가 고쳐서 여기 합류했다(챙 뿌리 닫힘변 0.29 -> 1.11획 /
            //      크리스 잉크 사각형 1.26 -> 1.68획).
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap).SetName("HEAD 천모자");
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora).SetName("HEAD 중절모");
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCrown).SetName("HEAD 왕관");
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret).SetName("HEAD 베레모");
            yield return new TestCaseData(EquipmentSlot.Head, AccessoryShapeBuilder.HeadStraw).SetName("HEAD 밀짚모자");

            // ---- NECK. 나비넥타이·줄무늬타이는 면제 대장에 있다.
            //      목도리는 2026-09-01(2차) "카드 단독 판독" 라운드가 재설계하며 합류했다
            //      (띠 좌우 변 0.99 -> 1.22획 / 자락 폭 0.64 -> 1.2획 이상).
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
            float span = Mathf.Max(max.x - min.x, max.y - min.y);
            if (span < w * 1.5f)
            {
                return $"'{shape.Name}'의 잉크 사각형이 {span / w:F2}획입니다 — 1.5획 미만이면 화면에서 " +
                    "'뚱뚱한 점' 하나로 보입니다(37-6 규칙 1).";
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
            float minRadius = float.MaxValue, maxRadius = 0f, topY = float.MinValue;
            for (int i = 0; i < sink.Count; i++)
            {
                Vector3[] pts = sink[i].Points;
                for (int k = 0; k < pts.Length; k++)
                {
                    float radius = new Vector2(pts[k].x, pts[k].y - rig.HeadCenterY).magnitude / rig.HeadRadius;
                    minRadius = Mathf.Min(minRadius, radius);
                    maxRadius = Mathf.Max(maxRadius, radius);
                    topY = Mathf.Max(topY, pts[k].y);
                }
            }
            Assert.LessOrEqual(minRadius, 1f - w,
                $"{label}이 두피 링 안쪽으로 1획({w:F3}R)만큼도 들어오지 않습니다(가장 안쪽 {minRadius:F3}R) — " +
                "머리에서 자란 것이 아니라 위에 얹힌 것으로 보입니다(37-6 규칙 4). " +
                "옛 값(단정 1.13R / 곱슬 1.10R)이 정확히 이 실패였습니다.");
            Assert.GreaterOrEqual(maxRadius, 1.05f,
                $"{label}이 두피 링 바깥으로 나오지 않습니다(가장 바깥 {maxRadius:F3}R) — 머리 안에 묻힙니다.");

            // 액자(CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR = 1.80R) 안에 들어와야 한다.
            float topInR = (topY - rig.HeadCenterY) / rig.HeadRadius;
            Assert.LessOrEqual(topInR, 1.75f,
                $"{label}의 꼭대기가 머리 중심 위 {topInR:F2}R입니다 — 초상화 액자가 1.80R까지만 " +
                "담으므로 1.75R을 넘으면 정보창에서 잘립니다.");
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
                            $"(검사점 {probe}). 이마선(HairlineCrestRatio)을 올려야 합니다.");
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
            Assert.AreEqual(arm * 2f, Vector3.Distance(vertical[0], vertical[1]), 1e-5f);
            Assert.AreEqual(arm * 2f, Vector3.Distance(horizontal[0], horizontal[1]), 1e-5f);
        }
    }
}
