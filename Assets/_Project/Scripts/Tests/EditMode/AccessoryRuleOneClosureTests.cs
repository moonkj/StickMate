using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 규칙 1(획 예산) 위반 <b>4건 마감</b> — 2026-09-01 마지막 정리 라운드.
    ///
    /// ============================================================================
    /// 무엇을 고쳤고, 왜 그 값인가
    /// ============================================================================
    /// 앞 라운드가 커버리지를 도형 단위 대장으로 넓히면서 드러난 6건 중, 리더가 "한두 상수 수정"으로
    /// 분류한 4건이다(나머지 둘 중 하나는 이미 고쳤고, 털모자 띠는 실루엣 재설계라 백로그).
    ///
    /// <list type="table">
    ///   <item><term>HEAD 천모자 <c>HatBrim</c></term>
    ///         <description>챙을 닫는 변(4→0) 0.29획 → <b>1.11획</b>. 챙 뿌리 두께 0.10R → 0.38R.</description></item>
    ///   <item><term>HEAD 중절모 <c>FedoraCrease</c></term>
    ///         <description>잉크 사각형 1.26획 → <b>1.68획</b>. 크리스 반폭 비율 0.30 → 0.40.</description></item>
    ///   <item><term>BACK 날개 <c>WingFeatherA/B</c></term>
    ///         <description>어깨 쪽 닫힘변 0.90 / 0.86획 → <b>1.20 / 1.20획</b>.
    ///         아래쪽 안쪽 꼭짓점만 뒤·아래로 옮겼다(어깨에 붙는 첫 점은 등뼈와 공유라 못 움직인다).</description></item>
    ///   <item><term>BACK 배낭 <c>PackStrap</c></term>
    ///         <description>잉크 사각형 1.32획 → <b>2.30획</b>. 끈 끝점을 배낭 몸의 <b>실재하는 꼭짓점</b>으로.</description></item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 덤으로 닫힌 규칙 4 잠복 결함 1건 — 이게 이 라운드에서 가장 중요한 발견이다
    /// ============================================================================
    /// 배낭 끈의 옛 끝점 <c>(cx+hw, cyp+hh)</c>는 배낭 몸(팔각형)의 <b>모따기 바깥</b>이었다.
    /// 즉 끈이 배낭에서 <b>0.64획 떠 있었다</b> — 규칙 4가 "최악"이라고 못박은 <c>0 &lt; 간격 &lt; 1획</c>
    /// 구간 한가운데다(붙은 것도 뗀 것도 아니라 "선을 잘못 그은 실수"로 읽힌다).
    /// 규칙 1을 고치는 가장 자연스러운 방법이 그 결함까지 함께 닫았다: 끈을 <b>실재하는 꼭짓점</b>까지
    /// 내리면 잉크 사각형이 1.5획을 넘고 간격이 정확히 0이 된다. 중절모 띠·베레모 테가 쓴 규약과 같다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤의 형태 — <b>옛 상수</b>를 얼리고 나머지는 살아 있는 것에서 받는다
    /// ============================================================================
    /// "옛 도형" 전체를 좌표로 박제하면, 나중에 관/배낭 같은 <b>이웃</b>이 움직였을 때 컨트롤이
    /// <b>역사상 존재한 적 없는 쌍</b>을 재게 된다(2026-09-01 펜던트 컨트롤이 실제로 그렇게 깨졌다).
    /// 그래서 여기서는 <b>실제로 바뀐 상수 하나</b>만 얼리고 이웃은 살아 있는 리그에서 받는다 —
    /// 비교하는 두 도형이 언제나 같은 세계에 있으므로 쌍이 어긋날 자리가 없다.
    /// 얼린 상수가 지금 값과 같아지면(= 누가 되돌렸으면) 컨트롤이 스스로 빨개진다.
    /// </summary>
    public sealed class AccessoryRuleOneClosureTests
    {
        /// <summary>출하 배율(0.75)의 획 예산(R 배수). 다른 액세서리 검사와 <b>같은 자</b>를 쓴다.</summary>
        private static float W => AccessorySilhouetteMetrics.StrokeInR;

        private static AccessoryShapeBuilder.Rig Rig() => AccessorySilhouetteMetrics.Rig();

        private static float BudgetWorld(in AccessoryShapeBuilder.Rig rig)
            => AccessoryStrokeBudgetTests.BudgetWorld(rig);

        private static List<AccessoryShapeBuilder.Shape> Build(
            in AccessoryShapeBuilder.Rig rig, EquipmentSlot slot, int item)
            => AccessorySilhouetteMetrics.Build(rig, slot, item);

        private static float ClosingEdgeInStrokes(in AccessoryShapeBuilder.Shape shape)
        {
            Vector3[] p = shape.Points;
            return Vector3.Distance(p[p.Length - 1], p[0]) / (W * Rig().HeadRadius);
        }

        // ============================================================================
        // 1. 천모자 — 챙 뿌리
        // ============================================================================

        /// <summary>옛 챙 뿌리 두께(R 배수). <b>이 값 하나만</b> 얼린다.</summary>
        private const float OldHatBrimRootDropRatio = 0.10f;

        [Test]
        public void 천모자_챙의_닫힘변이_획_하나보다_길다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            AccessoryShapeBuilder.Shape brim = AccessorySilhouetteMetrics.Find(
                Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap), "HatBrim");

            Assert.IsNull(AccessoryStrokeBudgetTests.DescribeRuleOneViolation(brim, BudgetWorld(rig)),
                "천모자 챙이 규칙 1을 어깁니다.");

            float closing = ClosingEdgeInStrokes(brim);
            Assert.Greater(closing, 1f,
                $"챙을 닫는 변이 {closing:F2}획입니다(옛 값 0.29획) — 양끝이 모두 꺾임인 변이라 " +
                "획 하나보다 짧으면 챙 뒤쪽 끝이 통째로 먹혀 사라집니다(37-6 규칙 1).");
            Assert.Less(closing, 1.6f,
                $"챙 뿌리가 {closing:F2}획까지 두꺼워졌습니다 — 챙이 아니라 이마를 덮는 판이 됩니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 두께로 되돌린 챙은 <b>지금 리그에서도</b> 실제로 위반한다.</summary>
        [Test]
        public void 컨트롤_옛_챙_뿌리는_규칙_1을_실제로_어긴다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            Assert.AreNotEqual(OldHatBrimRootDropRatio, AccessoryShapeBuilder.HatBrimRootDropRatio,
                "챙 뿌리 두께가 옛 값으로 되돌아갔습니다 — 이 컨트롤이 재현하려는 결함이 곧 현재 상태입니다.");

            AccessoryShapeBuilder.Shape old = OldHatBrim(rig);
            string violation = AccessoryStrokeBudgetTests.DescribeRuleOneViolation(old, BudgetWorld(rig));
            Assert.IsNotNull(violation,
                "옛 챙(뿌리 0.10R)이 규칙 1을 통과한다고 나옵니다 — 검사기가 눈이 멀었거나 " +
                "재현 좌표가 실제 옛 도형과 다릅니다(기록: 닫힘변 0.29획).");

            Assert.AreEqual(0.29f, ClosingEdgeInStrokes(old), 0.01f,
                "옛 챙의 닫힘변이 0.29획으로 측정되지 않습니다 — 대장에 적힌 실측값과 어긋납니다.");
        }

        /// <summary>
        /// 챙을 두껍게 해도 <b>실루엣은 한 구간도 움직이지 않는다</b>. 두꺼워지는 방향이 아래(관 반대쪽)이고
        /// 그 자리는 관이 이미 더 멀리 뻗어 있는 각도라, 반경 프로파일의 최댓값이 바뀌지 않기 때문이다.
        /// <para>이 사실이 중요한 이유: 모자 6종 15쌍의 실루엣 구분도(최소 2.94획)가 이 수정으로
        /// 흔들리지 않는다는 보증이 여기서 나온다.</para>
        /// </summary>
        [Test]
        public void 챙을_두껍게_해도_천모자_실루엣이_바뀌지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> live = Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap);

            var before = new List<AccessoryShapeBuilder.Shape>
            {
                AccessorySilhouetteMetrics.Find(live, "HatCrown"),   // 이웃은 살아 있는 것을 쓴다
                OldHatBrim(rig),
            };

            float delta = AccessorySilhouetteMetrics.MaxRadiusDelta(
                AccessorySilhouetteMetrics.ProfileOf(rig, before, rig.HeadCenterY),
                AccessorySilhouetteMetrics.ProfileOf(rig, live, rig.HeadCenterY));

            Assert.AreEqual(0f, delta, 1e-6f,
                $"챙 수정으로 실루엣이 {delta / W:F2}획 움직였습니다 — 이 수정은 관 안쪽(아래)으로만 " +
                "두꺼워지므로 반경 프로파일이 바뀔 수 없습니다. 바뀌었다면 다른 것도 함께 바뀐 것입니다.");
        }

        /// <summary>옛 챙 — 바뀐 상수 하나만 옛 값으로 두고 나머지는 <b>살아 있는 상수</b>에서 받는다.</summary>
        private static AccessoryShapeBuilder.Shape OldHatBrim(in AccessoryShapeBuilder.Rig rig)
        {
            float r = rig.HeadRadius;
            float brimY = AccessoryShapeBuilder.HatBrimLocalY(rig);
            float halfW = r * AccessoryShapeBuilder.HatCrownHalfWidthRatio;
            return new AccessoryShapeBuilder.Shape("OldHatBrim", new[]
            {
                rig.F(-halfW * 0.35f, brimY),
                rig.F(halfW * 0.85f, brimY + r * 0.02f),
                rig.F(r * AccessoryShapeBuilder.HatBrimReachRatio,
                    brimY - r * AccessoryShapeBuilder.HatBrimDropRatio),
                rig.F(halfW * 0.85f, brimY - r * 0.14f),
                rig.F(-halfW * 0.35f, brimY - r * OldHatBrimRootDropRatio),
            }, true, AccessoryShapeBuilder.SortHead, tone: AccessoryShapeBuilder.Accent, filled: true);
        }

        // ============================================================================
        // 2. 중절모 — 크리스
        // ============================================================================

        /// <summary>옛 크리스 반폭 비율(관 반폭의 배수).</summary>
        private const float OldFedoraCreaseHalfWidthRatio = 0.30f;

        [Test]
        public void 중절모_크리스의_잉크_사각형이_1_5획을_넘는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> fedora = Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora);
            AccessoryShapeBuilder.Shape crease = AccessorySilhouetteMetrics.Find(fedora, "FedoraCrease");

            Assert.IsNull(AccessoryStrokeBudgetTests.DescribeRuleOneViolation(crease, BudgetWorld(rig)),
                "중절모 크리스가 규칙 1을 어깁니다.");

            Vector2 extent = AccessorySilhouetteMetrics.ExtentInR(rig, crease.Points);
            float span = Mathf.Max(extent.x, extent.y) / W;
            Assert.Greater(span, 1.5f,
                $"크리스의 잉크 사각형이 {span:F2}획입니다(옛 값 1.26획) — 1.5획 미만이면 V가 아니라 " +
                "'뚱뚱한 점' 하나로 뭉갭니다.");
            Assert.Greater(span, 1.6f,
                $"크리스가 {span:F2}획으로 문턱에 너무 붙어 있습니다 — 최소 수정안(0.36 = 1.51획)은 " +
                "여유가 0.5%뿐이라 좌표 한 자리만 건드려도 다시 넘어갑니다. 그래서 0.40을 골랐습니다.");

            // 크리스는 관 <b>안쪽</b>에 있어야 한다. 밖으로 나가면 실루엣이 바뀌고 관을 뚫는다.
            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(fedora, "FedoraCrown");
            float crownHalfX = 0f;
            for (int i = 0; i < crown.Points.Length; i++)
            {
                crownHalfX = Mathf.Max(crownHalfX, Mathf.Abs(crown.Points[i].x));
            }
            for (int i = 0; i < crease.Points.Length; i++)
            {
                Assert.Less(Mathf.Abs(crease.Points[i].x), crownHalfX,
                    "크리스가 관 밖으로 나갔습니다 — 눌린 자국은 관 위에 있어야 합니다.");
            }
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 반폭은 <b>지금 관에서도</b> 1.26획으로 실제로 위반한다.</summary>
        [Test]
        public void 컨트롤_옛_크리스_반폭은_규칙_1을_실제로_어긴다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            Assert.AreNotEqual(OldFedoraCreaseHalfWidthRatio,
                AccessoryShapeBuilder.FedoraCreaseHalfWidthRatio,
                "크리스 반폭이 옛 값으로 되돌아갔습니다.");

            AccessoryShapeBuilder.Shape old = OldFedoraCrease(rig);
            Vector2 extent = AccessorySilhouetteMetrics.ExtentInR(rig, old.Points);
            Assert.AreEqual(1.26f, Mathf.Max(extent.x, extent.y) / W, 0.01f,
                "옛 크리스가 1.26획으로 측정되지 않습니다 — 대장의 실측값과 어긋납니다.");
            Assert.IsNotNull(AccessoryStrokeBudgetTests.DescribeRuleOneViolation(old, BudgetWorld(rig)),
                "옛 크리스가 규칙 1을 통과한다고 나옵니다 — 검사기가 눈이 멀었습니다.");
        }

        [Test]
        public void 크리스를_넓혀도_중절모_실루엣이_바뀌지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> live = Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora);

            var before = new List<AccessoryShapeBuilder.Shape>();
            for (int i = 0; i < live.Count; i++)
            {
                before.Add(live[i].Name == "FedoraCrease" ? OldFedoraCrease(rig) : live[i]);
            }

            float delta = AccessorySilhouetteMetrics.MaxRadiusDelta(
                AccessorySilhouetteMetrics.ProfileOf(rig, before, rig.HeadCenterY),
                AccessorySilhouetteMetrics.ProfileOf(rig, live, rig.HeadCenterY));

            Assert.AreEqual(0f, delta, 1e-6f,
                $"크리스 수정으로 실루엣이 {delta / W:F2}획 움직였습니다 — 크리스는 관 위 변 안쪽에만 " +
                "있으므로 바깥 윤곽에 영향을 줄 수 없습니다.");
        }

        private static AccessoryShapeBuilder.Shape OldFedoraCrease(in AccessoryShapeBuilder.Rig rig)
        {
            float r = rig.HeadRadius;
            float brimY = rig.HeadCenterY + r * AccessoryShapeBuilder.FedoraBrimLineRatio;
            float crownHalf = r * AccessoryShapeBuilder.FedoraCrownHalfWidthRatio;
            float crownTop = brimY + r * AccessoryShapeBuilder.FedoraCrownHeightRatio;
            return new AccessoryShapeBuilder.Shape("OldFedoraCrease", new[]
            {
                rig.F(-crownHalf * OldFedoraCreaseHalfWidthRatio, crownTop),
                rig.F(0f, crownTop - r * AccessoryShapeBuilder.FedoraCreaseDropRatio),
                rig.F(crownHalf * OldFedoraCreaseHalfWidthRatio, crownTop),
            }, false, AccessoryShapeBuilder.SortHead, tone: AccessoryShapeBuilder.Shade);
        }

        // ============================================================================
        // 3. 날개 — 두 깃의 어깨 쪽 닫힘변
        // ============================================================================

        [Test]
        public void 날개_두_깃의_어깨_닫힘변이_획_하나보다_길다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> wings =
                Build(rig, EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackWings);

            foreach (string name in new[] { "WingFeatherA", "WingFeatherB" })
            {
                AccessoryShapeBuilder.Shape feather = AccessorySilhouetteMetrics.Find(wings, name);
                Assert.IsNull(AccessoryStrokeBudgetTests.DescribeRuleOneViolation(feather, BudgetWorld(rig)),
                    $"{name}이 규칙 1을 어깁니다.");
                Assert.Greater(ClosingEdgeInStrokes(feather), 1f,
                    $"{name}의 어깨 쪽 닫힘변이 {ClosingEdgeInStrokes(feather):F2}획입니다 " +
                    "(옛 값 A 0.90 / B 0.86획) — 어깨 뿌리가 획에 먹혀 뭉개집니다.");
            }
        }

        /// <summary>
        /// 어깨에 붙는 <b>첫 점</b>은 등뼈(<c>WingSpine</c>)의 시작점과 같은 자리다.
        /// <para>닫힘변을 늘리는 가장 쉬운 방법이 "첫 점을 옮기는 것"인데, 그러면 등뼈가 깃에서 떨어져
        /// 규칙 4(부착)를 어긴다. 그래서 아래쪽 꼭짓점만 움직였다는 사실을 잠근다 —
        /// 다음 사람이 같은 값을 반대쪽에서 만들려다 이 조건을 깨는 것을 막는다.</para>
        /// </summary>
        [Test]
        public void 날개_등뼈는_깃의_어깨_꼭짓점에_그대로_붙어_있다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> wings =
                Build(rig, EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackWings);

            Vector3 spineRoot = AccessorySilhouetteMetrics.Find(wings, "WingSpine").Points[0];
            Vector3 featherRoot = AccessorySilhouetteMetrics.Find(wings, "WingFeatherA").Points[0];

            Assert.AreEqual(spineRoot, featherRoot,
                "등뼈의 시작점이 큰 깃의 어깨 꼭짓점과 어긋났습니다 — 두 선이 같은 자리에서 시작해야 " +
                "날개가 등에서 자란 것으로 보입니다(37-6 규칙 4).");
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 꼭짓점은 <b>지금 리그에서도</b> 0.90 / 0.86획이다.</summary>
        [Test]
        public void 컨트롤_옛_날개_꼭짓점은_규칙_1을_실제로_어긴다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float sy = rig.ShoulderY;

            // 얼린 것은 <b>바뀐 꼭짓점 하나</b>씩이고, 나머지 네 점은 살아 있는 상수에서 받는다.
            List<AccessoryShapeBuilder.Shape> live =
                Build(rig, EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackWings);

            var cases = new[]
            {
                new { Name = "WingFeatherA", Old = new Vector2(-0.45f, -0.18f), Expected = 0.90f },
                new { Name = "WingFeatherB", Old = new Vector2(-0.30f, -0.34f), Expected = 0.86f },
            };

            foreach (var c in cases)
            {
                Vector3[] now = AccessorySilhouetteMetrics.Find(live, c.Name).Points;
                var oldPoints = new Vector3[now.Length];
                for (int i = 0; i < now.Length - 1; i++) oldPoints[i] = now[i];
                oldPoints[now.Length - 1] = rig.F(r * c.Old.x, sy + r * c.Old.y);

                Assert.AreNotEqual(oldPoints[now.Length - 1], now[now.Length - 1],
                    $"{c.Name}의 아래 꼭짓점이 옛 값으로 되돌아갔습니다.");

                var old = new AccessoryShapeBuilder.Shape("Old" + c.Name, oldPoints, true,
                    AccessoryShapeBuilder.SortBack, filled: true);

                float closing = Vector3.Distance(oldPoints[oldPoints.Length - 1], oldPoints[0])
                                / (W * rig.HeadRadius);
                Assert.AreEqual(c.Expected, closing, 0.01f,
                    $"옛 {c.Name}의 닫힘변이 {closing:F2}획으로 측정됩니다 — 대장의 실측값({c.Expected:F2}획)과 " +
                    "어긋나므로 이 컨트롤은 다른 도형을 재고 있습니다.");
                Assert.IsNotNull(AccessoryStrokeBudgetTests.DescribeRuleOneViolation(old, BudgetWorld(rig)),
                    $"옛 {c.Name}이 규칙 1을 통과한다고 나옵니다 — 검사기가 눈이 멀었습니다.");
            }
        }

        // ============================================================================
        // 4. 배낭 — 어깨끈 (규칙 1 + 규칙 4가 <b>한 수정으로</b> 함께 닫힌다)
        // ============================================================================

        [Test]
        public void 배낭_어깨끈이_획_예산을_지킨다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            AccessoryShapeBuilder.Shape strap = AccessorySilhouetteMetrics.Find(
                Build(rig, EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackBackpack), "PackStrap");

            Assert.IsNull(AccessoryStrokeBudgetTests.DescribeRuleOneViolation(strap, BudgetWorld(rig)),
                "배낭 어깨끈이 규칙 1을 어깁니다.");

            Vector2 extent = AccessorySilhouetteMetrics.ExtentInR(rig, strap.Points);
            Assert.Greater(Mathf.Max(extent.x, extent.y) / W, 1.5f,
                $"어깨끈의 잉크 사각형이 {Mathf.Max(extent.x, extent.y) / W:F2}획입니다(옛 값 1.32획).");
        }

        /// <summary>
        /// ★ 끈의 끝점은 배낭 몸의 <b>실재하는 꼭짓점</b>이다(간격 정확히 0).
        /// <para>규칙 4가 금지하는 것은 <c>0 &lt; 간격 &lt; 1획</c>이지 겹침이 아니다.
        /// 중절모 띠·베레모 테가 같은 규약을 쓴다 — 좌표를 새로 적지 않으면 어긋날 자리가 없다.</para>
        /// </summary>
        [Test]
        public void 배낭_어깨끈의_끝점은_배낭_몸의_꼭짓점_그_자체다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> pack =
                Build(rig, EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackBackpack);

            AccessoryShapeBuilder.Shape body = AccessorySilhouetteMetrics.Find(pack, "PackBody");
            AccessoryShapeBuilder.Shape strap = AccessorySilhouetteMetrics.Find(pack, "PackStrap");
            Vector3 tip = strap.Points[strap.Points.Length - 1];

            bool shared = false;
            for (int i = 0; i < body.Points.Length; i++) shared |= body.Points[i] == tip;

            Assert.IsTrue(shared,
                "어깨끈의 끝점이 배낭 몸의 꼭짓점 중 하나가 아닙니다 — 좌표를 따로 적으면 " +
                "둘 중 하나만 움직이는 순간 끈이 배낭에서 떠 버립니다(옛 값이 정확히 그 상태였다).");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 옛 끝점은 배낭 몸에서 <b>0.64획 떠 있었다</b>(규칙 4의 최악 구간).
        /// <para>얼린 것은 "옛 끝점을 만드는 식"(모따기 <b>바깥</b>의 경계 상자 모서리)이고,
        /// 배낭 몸은 살아 있는 것을 쓴다 — 배낭 비율이 바뀌면 두 값이 함께 움직이므로
        /// 이 컨트롤이 존재한 적 없는 쌍을 재는 일이 없다.</para>
        /// </summary>
        [Test]
        public void 컨트롤_옛_끈_끝점은_배낭에서_떠_있었다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            AccessoryShapeBuilder.Shape body = AccessorySilhouetteMetrics.Find(
                Build(rig, EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackBackpack), "PackBody");

            float cx = -r * AccessoryShapeBuilder.PackCenterBackRatio;
            float cyp = rig.ShoulderY - rig.TorsoLength * AccessoryShapeBuilder.PackDropInTorso;
            float hw = r * AccessoryShapeBuilder.PackHalfWidthRatio;
            float hh = rig.TorsoLength * AccessoryShapeBuilder.PackHalfHeightInTorso;
            Vector3 oldTip = rig.F(cx + hw, cyp + hh);          // 팔각형 모따기 <b>바깥</b>의 모서리

            float gap = AccessorySilhouetteMetrics.MaxGapToShape(rig, new[] { oldTip, oldTip }, body) / W;

            Assert.That(gap, Is.GreaterThan(0f).And.LessThan(1f),
                $"옛 끈 끝점의 간격이 {gap:F2}획으로 측정됩니다 — 기록은 0.64획(규칙 4의 최악 구간)입니다. " +
                "이 값이 0이거나 1획을 넘으면 이 컨트롤이 재현하려는 결함 자체가 다른 것입니다.");
            Assert.AreEqual(0.64f, gap, 0.02f, "옛 간격이 기록된 0.64획과 다릅니다.");
        }
    }
}
