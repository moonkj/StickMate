using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 획 예산 린트의 <b>커버리지 대장(ledger)</b> — 2026-09-01.
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패는 "위반"이 아니라 <b>"검사에서 빠진 것"</b>이다
    /// ============================================================================
    /// <c>AccessoryStrokeBudgetTests.BudgetedItems</c>는 <b>통과하는 아이템만</b> 목록에 넣는
    /// 방식이라, 아직 못 고친 아이템은 목록 밖에 남는다. 그 자체는 옳다(빨간불이 상시화되면 테스트는
    /// 신호이기를 그만둔다). 문제는 <b>목록 밖이 조용하다</b>는 것이다:
    /// <list type="number">
    ///   <item>실제로는 통과하는데 아무도 재 보지 않아 빠져 있는 아이템이 생긴다
    ///         (2026-09-01 실측: 펜던트·반다나·왕관·베레모·밀짚모자·망토 4종 — <b>9종</b>이 그랬다).</item>
    ///   <item>아이템 하나가 도형 하나 때문에 빠지면 <b>그 아이템의 나머지 도형 전부</b>가 함께 검사에서
    ///         사라진다(털모자 폼폼 하나 때문에 HEAD가 통째로 빠져 있던 것이 정확히 이 사고다).</item>
    ///   <item>새 DLC 아이템은 아무 목록에도 안 적으면 <b>영원히 검사되지 않는다</b>.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 해결 — 면제는 <b>아이템</b>이 아니라 <b>도형</b> 단위로, 그리고 반드시 적어야 한다
    /// ============================================================================
    /// 여기서는 <b>전 카테고리 전 아이템의 모든 도형</b>을 훑고, <see cref="Waivers"/>에 이름이 적혀
    /// 있는 도형만 건너뛴다. 그래서
    /// <list type="bullet">
    ///   <item>새 아이템/새 도형은 <b>기본이 검사</b>다. 아무것도 안 적으면 검사된다(구멍이 안 생긴다).</item>
    ///   <item>면제는 <b>도형 하나</b>씩이라, 털모자의 폼폼·관은 검사되고 띠만 빠진다.</item>
    ///   <item>면제가 <b>고쳐지면 빨간불</b>이 된다(<see cref="면제된_도형은_아직_실제로_위반한다"/>) —
    ///         "고쳤는데 대장에서 지우는 걸 잊는" 경로가 막힌다. 대장이 스스로 낡지 않는다.</item>
    ///   <item>면제가 <b>가리키는 도형이 사라지면</b> 빨간불이다(이름을 바꾸면 면제만 남아
    ///         새 도형을 조용히 덮는다 — 커버리지 구멍이 다시 열리는 가장 흔한 길).</item>
    /// </list>
    ///
    /// ============================================================================
    /// 2026-09-01 실측 — 30종 중 <b>26종 완전 통과</b>, 4종이 도형 <b>9개</b>를 면제받는다
    /// ============================================================================
    /// 처음 이 대장을 세운 라운드는 22종 통과 / 면제 14개였고, <b>그때 새로 발견한 위반 6건</b>
    /// (HEAD 천모자 챙 0.29획 · 털모자 띠 0.58획 · 중절모 크리스 1.26획 / BACK 날개 0.90·0.86획 ·
    /// 배낭 끈 1.32획)은 전부 그 라운드의 소유권 밖이라 실측값만 여기 적어 두었다.
    ///
    /// <b>같은 날 마지막 정리 라운드가 그중 5개를 닫았다</b>(천모자 챙 뿌리 0.10R→0.38R ·
    /// 중절모 크리스 반폭 0.30→0.40 · 날개 두 깃의 어깨 꼭짓점 · 배낭 끈 끝점을 몸의 실재 꼭짓점으로).
    /// 그래서 <b>대장이 스스로 줄었다</b> — 아래 <see cref="면제된_도형은_아직_실제로_위반한다"/>가
    /// "고쳤는데 면제로 남기는" 길을 막고 있으므로, 고친 라운드는 여기서 지울 수밖에 없다.
    /// 그것이 이 대장의 설계 의도이고, 이번이 그 설계가 <b>실제로 작동한 첫 사례</b>다.
    ///
    /// 남은 면제 9개는 NECK 3종(나비넥타이·줄무늬타이·목도리) 8개와 HEAD 털모자 띠 1개다.
    /// 털모자 띠는 리더가 <b>백로그</b>로 뺐다(실루엣 재설계 — 아래 사유 참고).
    /// </summary>
    public sealed class AccessoryRuleOneCoverageTests
    {
        /// <summary>몸 도형이 있는 자리 = <c>AccessoryShapeBuilder.Append</c>가 아는 자리.
        /// FX/PET은 <c>AppearanceShapeBuilder</c> 소관이라 이 린트의 대상이 아니다.</summary>
        private static readonly EquipmentSlot[] BodySlots =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck,
            EquipmentSlot.Shoulders, EquipmentSlot.Hair,
        };

        /// <summary>
        /// 아직 규칙 1을 못 지키는 <b>도형</b>과 그 실측값. 한 줄이 곧 하나의 빚(debt)이다.
        /// <para>고칠 때 할 일은 둘: <b>여기서 지우고</b>, 그 아이템의 다른 도형도 다 통과하면
        /// <c>AccessoryStrokeBudgetTests.BudgetedItems</c>에 한 줄 넣는다. 둘 중 하나만 하면
        /// 아래 검사들이 빨개져서 알려 준다.</para>
        /// </summary>
        private static readonly Waiver[] Waivers =
        {
            // ---- HEAD. 천모자 챙(0.29획) · 중절모 크리스(1.26획)는 2026-09-01 마지막 정리
            //      라운드가 고쳐서 여기서 <b>빠졌다</b>. 남은 하나는 리더가 백로그로 뺀 실루엣 재설계다.
            new Waiver(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeanie, "BeanieBand",
                "띠 좌우 변이 0.58획(띠 높이 0.20R). 1.0획을 채우려면 높이가 0.344R이어야 하는데, " +
                "밑변은 이 모자의 커버선(HatCoverLocalY 0.42R — HAIR 클리핑이 읽는다)이고 " +
                "윗변을 올리면 관이 밀려 폼폼 꼭대기가 액자(1.80R)를 넘는다. 관 높이까지 함께 " +
                "다시 잡아야 하는 큰 수정이라 리더 판단 대상(2026-09-01 리더 결정: 백로그)."),

            // ---- NECK (3종 8도형)
            new Waiver(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBowTie, "BowTieKnot",
                "매듭 잉크 사각형 0.91획 / 변 0.53획. RoundedBox 8각 근사가 이 크기에서 통째로 먹힌다."),
            new Waiver(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped, "TieKnot",
                "매듭 잉크 사각형 1.40획 / 밑변 0.95획."),
            new Waiver(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped, "TieBlade",
                "끝 V의 세 변이 0.60~0.87획. blade 폭 0.15R이 획의 87%뿐이다."),
            new Waiver(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped, "TieStripeA",
                "줄무늬 잉크 사각형 0.87획 — 화면에 존재하지 않는 선이다."),
            new Waiver(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped, "TieStripeB",
                "줄무늬 잉크 사각형 0.87획."),
            new Waiver(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckScarf, "ScarfWrap",
                "띠의 좌우 변이 0.99획(높이 0.34R). 1.0획에 1%만큼 못 미친다."),
            new Waiver(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckScarf, "ScarfTailA",
                "자락 폭 0.22R = 0.64획. 두 자락이 같은 값이라 함께 고쳐야 한다."),
            new Waiver(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckScarf, "ScarfTailB",
                "자락 폭 0.22R = 0.64획."),

            // ---- BACK. 날개 두 깃(0.90 / 0.86획)과 배낭 어깨끈(1.32획)은 2026-09-01 마지막 정리
            //      라운드가 고쳐서 여기서 빠졌다. 이 카테고리에는 이제 면제가 하나도 없다.
        };

        private readonly struct Waiver
        {
            public readonly EquipmentSlot Slot;
            public readonly int Item;
            public readonly string ShapeName;
            public readonly string Reason;

            public Waiver(EquipmentSlot slot, int item, string shapeName, string reason)
            {
                Slot = slot;
                Item = item;
                ShapeName = shapeName;
                Reason = reason;
            }

            public override string ToString() => $"{Slot} {Item}번 '{ShapeName}'";
        }

        // ============================================================================
        // 1. 본체 — 면제되지 않은 <b>모든</b> 도형이 규칙 1을 지킨다
        // ============================================================================

        [Test]
        public void 면제되지_않은_모든_도형이_획_예산을_지킨다()
        {
            AccessoryShapeBuilder.Rig rig = AccessoryStrokeBudgetTests.Rig();
            float w = AccessoryStrokeBudgetTests.BudgetWorld(rig);

            var failures = new List<string>();
            int checkedShapes = 0;

            ForEachShape(rig, (slot, item, shape) =>
            {
                if (IsWaived(slot, item, shape.Name)) return;
                checkedShapes++;

                string violation = AccessoryStrokeBudgetTests.DescribeRuleOneViolation(shape, w);
                if (violation != null)
                {
                    failures.Add($"{Label(slot, item)} {violation}");
                }
            });

            // 검사한 도형 수가 무너지면(예: 열거가 조용히 0개를 돌려주면) 이 검사는 아무것도 잡지 않는다.
            Assert.Greater(checkedShapes, 50,
                $"검사한 도형이 {checkedShapes}개뿐입니다 — 열거가 깨졌거나 카탈로그가 비었습니다. " +
                "이 검사가 초록인 것이 '위반이 없다'는 뜻이 되려면 도형을 실제로 훑어야 합니다.");

            Assert.IsEmpty(failures,
                $"면제 목록에 없는 도형이 규칙 1을 어깁니다(37-6 규칙 1).\n  - " +
                string.Join("\n  - ", failures) +
                "\n고치거나, 못 고칠 이유를 Waivers에 실측값과 함께 적으십시오.");
        }

        // ============================================================================
        // 2. 대장이 낡지 않게 — 면제는 스스로 만료된다
        // ============================================================================

        /// <summary>
        /// ★ 네거티브 컨트롤 겸 <b>자동 만료</b>. 면제된 도형이 <b>지금도</b> 실제로 위반해야 한다.
        /// <para>둘을 동시에 증명한다: (a) 규칙 검사기가 살아 있다(모든 면제가 실제로 걸린다),
        /// (b) 고쳐진 빚이 대장에 남아 <b>다음 위반을 조용히 덮는</b> 일이 없다.</para>
        /// </summary>
        [Test]
        public void 면제된_도형은_아직_실제로_위반한다()
        {
            AccessoryShapeBuilder.Rig rig = AccessoryStrokeBudgetTests.Rig();
            float w = AccessoryStrokeBudgetTests.BudgetWorld(rig);

            for (int i = 0; i < Waivers.Length; i++)
            {
                Waiver waiver = Waivers[i];
                AccessoryShapeBuilder.Shape shape = FindShape(rig, waiver.Slot, waiver.Item, waiver.ShapeName);
                string violation = AccessoryStrokeBudgetTests.DescribeRuleOneViolation(shape, w);

                Assert.IsNotNull(violation,
                    $"{waiver} 이(가) 이제 규칙 1을 통과합니다 — 축하합니다. " +
                    "Waivers에서 이 줄을 지우고, 그 아이템의 다른 도형도 전부 통과하면 " +
                    "AccessoryStrokeBudgetTests.BudgetedItems에 한 줄 넣으십시오. " +
                    "면제를 남겨 두면 그 도형은 앞으로 어떤 위반을 저질러도 조용합니다.\n" +
                    $"(대장에 적힌 사유: {waiver.Reason})");
            }
        }

        /// <summary>면제가 <b>실재하는 도형</b>을 가리킨다. 도형 이름이 바뀌면 면제만 남아
        /// 새 이름의 도형이 검사 대상이 되는데, 그건 좋다 — 나쁜 것은 <b>낡은 면제가 남는 것</b>이다.
        /// 낡은 면제는 언젠가 우연히 같은 이름이 생기면 그 도형을 통째로 가린다.</summary>
        [Test]
        public void 면제_목록이_실재하는_도형만_가리킨다()
        {
            AccessoryShapeBuilder.Rig rig = AccessoryStrokeBudgetTests.Rig();
            for (int i = 0; i < Waivers.Length; i++)
            {
                Waiver waiver = Waivers[i];
                Assert.DoesNotThrow(() => FindShape(rig, waiver.Slot, waiver.Item, waiver.ShapeName),
                    $"{waiver} 에 해당하는 도형이 없습니다 — 이름이 바뀌었거나 도형이 사라졌습니다. " +
                    "낡은 면제는 지우십시오.");
            }
        }

        // ============================================================================
        // 3. 커버리지 구멍 자체를 막는다 — 통과하는데 목록에 없으면 빨간불
        // ============================================================================

        /// <summary>
        /// ★ 이 라운드의 핵심. <b>면제가 하나도 없는 아이템</b>은 반드시
        /// <c>AccessoryStrokeBudgetTests.BudgetedItems</c>에 들어와 있어야 한다.
        /// <para>2026-09-01에 9종이 "통과하는데 아무도 재 보지 않아 빠져 있던" 상태였다.
        /// 그 상태가 다시 생기면 여기가 잡는다 — 사람이 기억할 일이 아니다.</para>
        /// </summary>
        [Test]
        public void 면제가_없는_아이템은_전부_린트_목록에_들어와_있다()
        {
            var budgeted = new HashSet<(EquipmentSlot, int)>(AccessoryStrokeBudgetTests.BudgetedKeys());
            var missing = new List<string>();

            ForEachItem((slot, item) =>
            {
                if (HasAnyWaiver(slot, item)) return;
                if (budgeted.Contains((slot, item))) return;
                missing.Add(Label(slot, item));
            });

            Assert.IsEmpty(missing,
                "면제가 하나도 없는데 획 예산 린트 목록에 빠져 있는 아이템이 있습니다: " +
                string.Join(", ", missing) +
                ". AccessoryStrokeBudgetTests.BudgetedItems에 한 줄씩 넣으십시오 — " +
                "지금 통과하는 것을 잠가 두지 않으면 다음 변경이 조용히 깨뜨립니다.");
        }

        /// <summary>반대 방향 — 면제가 있는 아이템이 린트 목록에 들어와 있으면 안 된다.
        /// 그 상태는 <c>AccessoryStrokeBudgetTests.모든_도형이_획_예산을_지킨다</c>가
        /// <b>상시 빨간불</b>이 된다는 뜻이고, 상시 빨간불은 신호가 아니다.</summary>
        [Test]
        public void 면제가_있는_아이템은_린트_목록에_없다()
        {
            var budgeted = new HashSet<(EquipmentSlot, int)>(AccessoryStrokeBudgetTests.BudgetedKeys());
            var wrong = new List<string>();

            ForEachItem((slot, item) =>
            {
                if (HasAnyWaiver(slot, item) && budgeted.Contains((slot, item))) wrong.Add(Label(slot, item));
            });

            Assert.IsEmpty(wrong,
                "아직 면제가 남은 아이템이 획 예산 린트 목록에 들어 있습니다: " + string.Join(", ", wrong) +
                ". 둘 중 하나가 틀렸습니다 — 도형을 고쳤다면 Waivers에서 지우고, 아니면 목록에서 빼십시오.");
        }

        /// <summary>
        /// ★ 래칫(ratchet). 커버리지는 <b>줄어들 수 없다</b>.
        /// <para>수치를 못 박는 이유: 위 검사들은 "면제와 목록이 서로 맞는가"만 보므로,
        /// 누가 아이템을 목록에서 빼고 면제를 한 줄 늘리면 <b>전부 초록인 채로</b> 커버리지가 준다.
        /// 그 경로를 막는 것은 숫자뿐이다. 늘리는 것은 언제나 환영이고, 그때 이 값을 올리면 된다.</para>
        /// </summary>
        [Test]
        public void 커버리지가_2026_09_01_수준_아래로_내려가지_않는다()
        {
            // 2026-09-01 마지막 정리 라운드에서 22 -> 26종 / 면제 14 -> 9개로 올라갔다.
            // 래칫은 <b>올라간 자리에서 다시 잠근다</b> — 옛 값으로 두면 방금 갚은 빚을 다시 질 수 있다.
            const int itemsAtRatchet = 26;      // 30종 중 완전 통과
            const int waivedShapesAtRatchet = 9;

            int items = 0, clean = 0;
            ForEachItem((slot, item) =>
            {
                items++;
                if (!HasAnyWaiver(slot, item)) clean++;
            });

            Assert.AreEqual(30, items,
                $"몸 도형이 있는 아이템이 {items}종입니다(2026-09-01 기준 30종 = 5카테고리 × 6). " +
                "카테고리나 종수가 바뀌었다면 아래 래칫 값도 함께 다시 잡아야 합니다.");

            Assert.GreaterOrEqual(clean, itemsAtRatchet,
                $"획 예산을 완전히 통과하는 아이템이 {clean}종으로 줄었습니다(래칫 {itemsAtRatchet}종). " +
                "면제를 늘려 초록을 만드는 것은 커버리지를 파는 일입니다.");

            Assert.LessOrEqual(Waivers.Length, waivedShapesAtRatchet,
                $"면제 도형이 {Waivers.Length}개로 늘었습니다(래칫 {waivedShapesAtRatchet}개). " +
                "새 위반은 면제가 아니라 수정으로 닫으십시오.");
        }

        // ============================================================================
        // 보조
        // ============================================================================

        private static void ForEachItem(System.Action<EquipmentSlot, int> visit)
        {
            for (int s = 0; s < BodySlots.Length; s++)
            {
                EquipmentSlot slot = BodySlots[s];
                int count = ItemCatalog.ItemCountIn(slot);
                for (int i = 0; i < count; i++) visit(slot, i);
            }
        }

        private static void ForEachShape(in AccessoryShapeBuilder.Rig rig,
            System.Action<EquipmentSlot, int, AccessoryShapeBuilder.Shape> visit)
        {
            // in 파라미터는 람다가 캡처할 수 없어 한 번 복사한다(readonly struct라 안전).
            AccessoryShapeBuilder.Rig local = rig;
            var sink = new List<AccessoryShapeBuilder.Shape>();
            ForEachItem((slot, item) =>
            {
                sink.Clear();
                AccessoryShapeBuilder.Append(sink, slot, item, local);
                Assert.Greater(sink.Count, 0, $"{Label(slot, item)}: 도형이 하나도 없습니다.");
                for (int k = 0; k < sink.Count; k++) visit(slot, item, sink[k]);
            });
        }

        private static AccessoryShapeBuilder.Shape FindShape(in AccessoryShapeBuilder.Rig rig,
            EquipmentSlot slot, int item, string name)
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, item, rig);
            return AccessorySilhouetteMetrics.Find(sink, name);
        }

        private static bool IsWaived(EquipmentSlot slot, int item, string shapeName)
        {
            for (int i = 0; i < Waivers.Length; i++)
            {
                if (Waivers[i].Slot == slot && Waivers[i].Item == item && Waivers[i].ShapeName == shapeName)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasAnyWaiver(EquipmentSlot slot, int item)
        {
            for (int i = 0; i < Waivers.Length; i++)
            {
                if (Waivers[i].Slot == slot && Waivers[i].Item == item) return true;
            }
            return false;
        }

        private static string Label(EquipmentSlot slot, int item)
            => $"{slot} {item}번({ItemCatalog.Item(slot, item).DisplayName})";
    }
}
