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
            // 이 세 줄이 이 절에 남은 전부다. 옛 좌표를 얼린 컨트롤 두 건은 아래 문단대로 폐기했다.
        }

        // ★ 2026-09-01(2차) — 아래 두 검사와 <c>OldHatBrim</c>을 <b>폐기했다</b>(날개 컨트롤과 같은 이유).
        //     · <c>컨트롤_옛_챙_뿌리는_규칙_1을_실제로_어긴다</c>
        //     · <c>챙을_두껍게_해도_천모자_실루엣이_바뀌지_않는다</c>
        //   그 둘은 "관은 그대로 두고 챙 뿌리만 두껍게 했다"는 <b>한 상수짜리 수정</b>을 재던 자다.
        //   이번 라운드는 야구모자를 통째로 다시 그렸다 — 커버선이 +0.62R에서 +0.06R로 내려오고
        //   관 옆벽이 −0.22R까지 감싸므로, 옛 챙 좌표는 <b>이제 존재하지 않는 관</b>에 매달린
        //   도형이고 실루엣 불변도 성립하지 않는다(관 자체가 움직였다).
        //   살려 두면 "역사상 존재한 적 없는 쌍"을 재게 된다 — 이 파일 머리말이 금지한 바로 그것이다.
        //   챙 뿌리가 여전히 획보다 두껍다는 사실은 위 <c>천모자_챙의_닫힘변이_획_하나보다_길다</c>가
        //   <b>살아 있는 도형에서</b> 계속 잠근다(실측 1.34획).

        // ============================================================================
        // 2. 중절모 — 크리스 (★ 2026-09-01(2차) 폐기)
        // ============================================================================
        // <c>FedoraCrease</c>는 <b>도형 자체가 사라졌다</b>. 커버선이 +0.58R -> +0.08R로 내려오며
        // 관이 낮아졌고, 그 관 위에서 규칙 1(잉크 1.5획)을 지키는 V는 관을 가로질러 <b>관을 두 쪽으로
        // 가르는 선</b>이 된다. 37-6 규칙 5 — "예산을 못 지키는 [선택] 디테일은 넣지 않는다".
        // 그래서 아래 세 검사와 <c>OldFedoraCrease</c>를 함께 지웠다:
        //     · <c>중절모_크리스의_잉크_사각형이_1_5획을_넘는다</c>
        //     · <c>컨트롤_옛_크리스_반폭은_규칙_1을_실제로_어긴다</c>
        //     · <c>크리스를_넓혀도_중절모_실루엣이_바뀌지_않는다</c>
        // 없는 도형을 찾는 검사는 <see cref="AccessorySilhouetteMetrics.Find"/>에서 예외로 죽고,
        // 그 실패는 "규칙을 어겼다"가 아니라 "검사가 낡았다"를 뜻해 신호를 오염시킨다.

        // ============================================================================
        // 3. 날개 — <b>한 쌍</b>이고, 두 깃이 등뼈와 한 몸으로 이어진다
        // ============================================================================
        //
        // ★ 2026-09-01(2차) 이 절의 뜻이 바뀌었다. 옛 검사는 "두 깃의 어깨 쪽 닫힘변이 획보다
        //   길다"(0.90 / 0.86획 -> 1.20획)만 봤는데, 그때 두 깃은 <b>둘 다 진행 반대쪽</b>으로만
        //   뻗는 겹친 도형이었다 — 즉 규칙 1은 지키면서 <b>날개 한 짝</b>을 그리고 있었고,
        //   리더 육안 검증이 카드에서 그것을 "나뭇잎 한 장 / 깃발"로 판정했다(Tasklist V7).
        //   규칙 1은 "획에 먹히는가"만 재지 "이름대로 보이는가"는 재지 않는다.
        //   그래서 옛 좌표를 얼려 둔 네거티브 컨트롤도 함께 폐기했다 — 그 좌표는 이제 존재하지
        //   않는 설계(한 짝짜리 날개)의 것이고, 살려 두면 되돌아갈 자리를 남기는 셈이다.

        [Test]
        public void 날개_두_깃이_좌우_한_쌍이다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            foreach (int item in new[]
            {
                AccessoryShapeBuilder.BackWings, AccessoryShapeBuilder.BackFairyWings,
            })
            {
                List<AccessoryShapeBuilder.Shape> wings = Build(rig, EquipmentSlot.Shoulders, item);
                Vector3[] a = AccessorySilhouetteMetrics.Find(wings, "WingFeatherA").Points;
                Vector3[] b = AccessorySilhouetteMetrics.Find(wings, "WingFeatherB").Points;
                string label = ItemCatalog.Item(EquipmentSlot.Shoulders, item).DisplayName;

                Assert.AreEqual(a.Length, b.Length, $"{label}: 두 깃의 점 수가 다릅니다.");
                for (int i = 0; i < a.Length; i++)
                {
                    // 한쪽은 점 순서가 뒤집혀 있다(두 짝의 회전 방향을 맞추기 위해서다).
                    Vector3 mirror = b[b.Length - 1 - i];
                    Assert.AreEqual(-a[i].x, mirror.x, 1e-5f,
                        $"{label}: {i}번 점이 좌우 대칭이 아닙니다 — 날개는 <b>쌍</b>이어야 합니다.");
                    Assert.AreEqual(a[i].y, mirror.y, 1e-5f,
                        $"{label}: {i}번 점의 y가 짝과 다릅니다.");
                }
            }
        }

        [Test]
        public void 날개_두_깃과_등뼈가_한_점에서_만난다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            foreach (int item in new[]
            {
                AccessoryShapeBuilder.BackWings, AccessoryShapeBuilder.BackFairyWings,
            })
            {
                List<AccessoryShapeBuilder.Shape> wings = Build(rig, EquipmentSlot.Shoulders, item);
                string label = ItemCatalog.Item(EquipmentSlot.Shoulders, item).DisplayName;

                Vector3 spineRoot = AccessorySilhouetteMetrics.Find(wings, "WingSpine").Points[0];
                Vector3[] a = AccessorySilhouetteMetrics.Find(wings, "WingFeatherA").Points;
                Vector3[] b = AccessorySilhouetteMetrics.Find(wings, "WingFeatherB").Points;

                Assert.AreEqual(spineRoot, b[0],
                    $"{label}: 등뼈의 시작점이 깃의 뿌리와 어긋났습니다 — 좌표를 새로 적으면 " +
                    "한쪽만 고쳐지는 순간 날개가 등에서 떨어집니다(37-6 규칙 4).");
                Assert.AreEqual(spineRoot, a[a.Length - 1],
                    $"{label}: 반대쪽 깃이 같은 뿌리에서 시작하지 않습니다 — 두 짝이 등 한가운데에서 " +
                    "만나지 않으면 '한 쌍'이 아니라 '떨어진 두 조각'이 됩니다.");
            }
        }

        [Test]
        public void 날개_두_깃이_규칙_1을_지킨다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            foreach (int item in new[]
            {
                AccessoryShapeBuilder.BackWings, AccessoryShapeBuilder.BackFairyWings,
            })
            {
                List<AccessoryShapeBuilder.Shape> wings = Build(rig, EquipmentSlot.Shoulders, item);
                string label = ItemCatalog.Item(EquipmentSlot.Shoulders, item).DisplayName;
                foreach (string name in new[] { "WingFeatherA", "WingFeatherB" })
                {
                    AccessoryShapeBuilder.Shape feather = AccessorySilhouetteMetrics.Find(wings, name);
                    Assert.IsNull(AccessoryStrokeBudgetTests.DescribeRuleOneViolation(feather, BudgetWorld(rig)),
                        $"{label} {name}이 규칙 1을 어깁니다.");
                    Assert.Greater(ClosingEdgeInStrokes(feather), 1f,
                        $"{label} {name}의 닫힘변이 {ClosingEdgeInStrokes(feather):F2}획입니다.");
                }
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

        // ★ 2026-09-01(3차) — <c>컨트롤_옛_끈_끝점은_배낭에서_떠_있었다</c>를 폐기했다.
        //   그 컨트롤은 "옛 끝점을 만드는 식"(경계 상자 모서리)만 얼리고 배낭 몸은 살아 있는 것을
        //   썼는데, 이번 라운드가 배낭을 통째로 다시 그렸다(폭 1.10 -> 1.56R, 몸+뚜껑+버클+끈).
        //   그래서 같은 식이 만드는 점이 새 몸에서는 2.13획 떨어진 자리가 된다 — 기록된 0.64획과
        //   전혀 다른 사실을 재게 되고, 그건 "역사상 존재한 적 없는 쌍"이다(이 파일 머리말의 금지 사항).
        //   끈 끝점이 몸의 실재 꼭짓점이라는 <b>계약</b>은 바로 위 검사가 살아 있는 도형에서 계속 잠근다.

    }
}
