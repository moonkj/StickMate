using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>「기본 코호트」 모집단을 세는 단 한 곳</b> — 2026-09-08, 첫 유료 팩(<c>pack.cyber</c>) 착지 라운드.
    ///
    /// ============================================================================
    /// 왜 생겼나 — 「카탈로그 = 42종」이 <b>13개 파일에 흩어져 있었다</b>
    /// ============================================================================
    /// 팩 아이템 4종(HEAD/EYES/NECK/BACK 각 6번, 코호트 2)을 <c>Resources/Items</c> 에 놓자
    /// EditMode 30건이 한꺼번에 빨개졌다. 원인은 전부 같았다: 각 파일이
    /// <c>for (i &lt; ItemCatalog.ItemCountIn(slot))</c> 로 카탈로그 전량을 돌면서 그 결과를
    /// <b>「출하 42종」이라고 부르고 있었다</b>. 즉 <b>같은 사실(모집단이 무엇인가)이 13곳에서
    /// 각자 계산되고 있었다</b> — CLAUDE.md 가 반복해서 금지하는 그 형태다.
    ///
    /// <para>그래서 판정을 여기 하나로 모은다. 다음 팩(<c>pack.mine</c> · <c>pack.arcane</c> …)이
    /// 들어오는 날 고칠 곳이 <b>이 파일</b>이고, 그때 13개 파일은 아무 일도 하지 않는다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 <b>하지 않는</b> 것 — 팩 축을 다시 세지 않는다
    /// ============================================================================
    /// 팩 아이템의 <b>개수</b>와 <b>자리 번호</b>는 <c>PackRegistry.Build</c> 가
    /// <c>declaredItemCount</c> / <c>itemIndexBase</c> 로 <b>더 정확하게</b> 잰다
    /// (<c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §18-3-5 단계 3 권고). 여기서 또 세면 같은 사실이
    /// 두 곳에 앉고, 그때 한쪽만 고쳐지는 날 «검사는 통과했는데 팩이 반쯤 실린» 상태가 만들어진다.
    /// <see cref="PackCountIn"/> 은 <b>보고용</b>이지 계약이 아니다.
    ///
    /// ============================================================================
    /// 죽은 자가 되지 않게 — <see cref="BaseCohortScopeSelfTests"/>
    /// ============================================================================
    /// 이 헬퍼가 조용히 «아무것도 거르지 않는» 상태가 되면 13개 파일이 <b>전부 함께</b> 눈이 먼다.
    /// 그래서 같은 파일 아래에 자기 교정 테스트를 둔다: 기본 42종을 실제로 다 세는가(양성),
    /// 그리고 카탈로그 전량과 <b>다른 답</b>을 낼 수 있는가(음성).
    /// </summary>
    internal static class BaseCohortScope
    {
        /// <summary>이 자리의 아이템이 기본 코호트(무료 42종)인가. 자리에 아이템이 없으면 false.</summary>
        internal static bool IsBase(EquipmentSlot slot, int itemIndex)
        {
            ItemCatalogEntry e = ItemCatalog.Item(slot, itemIndex);
            return e != null && e.CohortId == ItemCatalog.BaseCohortId;
        }

        internal static bool IsBase(ItemCatalogEntry entry)
            => entry != null && entry.CohortId == ItemCatalog.BaseCohortId;

        /// <summary>이 카테고리의 <b>기본 코호트</b> 아이템 자리 번호 — 오름차순.
        /// <para>자리 번호를 돌려주는 이유: 팩이 0번대를 침범하면(그건 결함이다)
        /// «개수만큼 앞에서부터»가 조용히 팩을 포함하게 된다. 번호를 그대로 넘기면 그 경로가 없다.</para></summary>
        internal static IEnumerable<int> ItemsIn(EquipmentSlot slot)
        {
            int n = ItemCatalog.ItemCountIn(slot);
            for (int i = 0; i < n; i++)
            {
                if (IsBase(slot, i)) yield return i;
            }
        }

        /// <summary>이 카테고리의 기본 코호트 아이템 수(출하 6종).</summary>
        internal static int CountIn(EquipmentSlot slot)
        {
            int n = ItemCatalog.ItemCountIn(slot), found = 0;
            for (int i = 0; i < n; i++)
            {
                if (IsBase(slot, i)) found++;
            }
            return found;
        }

        /// <summary>이 카테고리의 <b>팩 코호트</b> 아이템 수. ★ 보고용 — 계약은 PackRegistry 가 쥔다.</summary>
        internal static int PackCountIn(EquipmentSlot slot) => ItemCatalog.ItemCountIn(slot) - CountIn(slot);

        /// <summary>장비 전량 중 기본 코호트(= 출하 42종).</summary>
        internal static int EquipmentCount
        {
            get
            {
                int n = 0;
                for (int s = 0; s < ItemCatalog.SlotCount; s++) n += CountIn((EquipmentSlot)s);
                return n;
            }
        }

        /// <summary>장비 전량 중 팩 코호트. ★ 보고용.</summary>
        internal static int PackEquipmentCount => ItemCatalog.EquipmentCount - EquipmentCount;

        /// <summary>사람이 읽는 한 줄 — 이 헬퍼를 쓰는 검사의 로그에 붙인다.
        /// 러너 출력만 봐도 «무엇을 세고 무엇을 뺐는지»가 남는다.</summary>
        internal static string Describe()
            => $"기본 코호트 {EquipmentCount}종 / 팩 코호트 {PackEquipmentCount}종 " +
               $"(전량 {ItemCatalog.EquipmentCount}종)";
    }

    /// <summary>
    /// ★ <see cref="BaseCohortScope"/> 자기 교정. 이 헬퍼가 13개 파일의 모집단을 정하므로,
    /// <b>여기가 눈이 멀면 그 13개가 전부 함께 눈이 먼다</b>.
    /// </summary>
    public sealed class BaseCohortScopeSelfTests
    {
        private const string LogPrefix = "[코호트범위]";

        /// <summary>(양성) 기본 코호트가 <b>카테고리마다 같은 수</b>이고 그 합이 장비 전량에서
        /// 팩을 뺀 값과 같다. 자리 번호는 0부터 빈틈없이 이어진다(팩이 0번대를 침범하면 여기서 난다).</summary>
        [Test]
        public void 기본_코호트가_카테고리마다_같은_수이고_자리가_앞에서부터_이어진다()
        {
            int total = 0, perSlot = -1;
            for (int s = 0; s < ItemCatalog.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                var indices = new List<int>(BaseCohortScope.ItemsIn(slot));

                Assert.Greater(indices.Count, 0,
                    $"{LogPrefix} {slot}에 기본 코호트 아이템이 하나도 없습니다 — " +
                    "이 헬퍼를 쓰는 모든 검사가 빈 목록을 돌게 됩니다.");
                if (perSlot < 0) perSlot = indices.Count;
                Assert.AreEqual(perSlot, indices.Count,
                    $"{LogPrefix} {slot}의 기본 코호트가 {indices.Count}종입니다(다른 카테고리는 {perSlot}종). " +
                    "카테고리마다 같은 수라는 전제가 깨지면 «42종»을 유도하는 검사들이 함께 틀어집니다.");

                for (int k = 0; k < indices.Count; k++)
                {
                    Assert.AreEqual(k, indices[k],
                        $"{LogPrefix} {slot}의 기본 코호트 자리가 0부터 이어지지 않습니다(자리 {indices[k]}). " +
                        "팩이 0번대를 침범했거나 표에 구멍이 있습니다.");
                }

                Assert.AreEqual(indices.Count, BaseCohortScope.CountIn(slot),
                    $"{LogPrefix} {slot}에서 ItemsIn 과 CountIn 이 다른 답을 냅니다.");
                total += indices.Count;
            }

            Assert.AreEqual(total, BaseCohortScope.EquipmentCount,
                $"{LogPrefix} 카테고리 합({total})과 EquipmentCount({BaseCohortScope.EquipmentCount})가 다릅니다.");
            Assert.AreEqual(ItemCatalog.EquipmentCount,
                BaseCohortScope.EquipmentCount + BaseCohortScope.PackEquipmentCount,
                $"{LogPrefix} 기본 + 팩이 카탈로그 전량과 다릅니다 — 세다 흘린 자리가 있습니다.");

            Debug.Log($"{LogPrefix} {BaseCohortScope.Describe()} · 카테고리당 기본 {perSlot}종.");
        }

        /// <summary>
        /// (음성) <b>이 헬퍼가 실제로 무언가를 거르는가</b>. 팩이 실린 트리에서는 전량과 답이 갈려야 하고,
        /// 팩이 하나도 없는 트리에서는 같아야 한다 — <b>둘 중 어느 쪽인지를 매 실행 명시</b>한다.
        /// <para>«전량과 같다»만 단언하면 팩이 실려도 초록이고, «다르다»만 단언하면 팩을 뺀 날 빨개진다.
        /// 그래서 <see cref="ItemCatalog.EquipmentCount"/> 와의 <b>차</b>가 팩 코호트 수와 정확히 같은지를 본다 —
        /// 이 등식은 팩이 0개일 때도 참이고, 팩이 늘어도 참이다.</para>
        /// </summary>
        [Test]
        public void 헬퍼가_거르는_양이_팩_코호트_수와_정확히_같다()
        {
            int packs = 0;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e == null || e.Category != ItemCategory.Equipment) continue;
                if (e.CohortId != ItemCatalog.BaseCohortId) packs++;
            }

            Assert.AreEqual(packs, BaseCohortScope.PackEquipmentCount,
                $"{LogPrefix} 전량 순회로 센 팩 {packs}종과 헬퍼가 뺀 {BaseCohortScope.PackEquipmentCount}종이 " +
                "다릅니다 — 둘 중 하나가 코호트를 잘못 읽고 있습니다.");

            if (packs == 0)
            {
                Assert.AreEqual(ItemCatalog.EquipmentCount, BaseCohortScope.EquipmentCount,
                    $"{LogPrefix} 팩이 0종인데 헬퍼가 전량과 다른 답을 냈습니다.");
                Debug.Log($"{LogPrefix} 팩 0종 — 헬퍼와 전량이 같은 답({ItemCatalog.EquipmentCount}).");
                return;
            }

            Assert.Less(BaseCohortScope.EquipmentCount, ItemCatalog.EquipmentCount,
                $"{LogPrefix} 팩이 {packs}종 실렸는데 헬퍼가 전량과 같은 답을 냈습니다 — " +
                "거르지 않고 있으므로 이 헬퍼를 쓰는 검사 전부가 팩을 «출하 42종»으로 셉니다.");
            Debug.Log($"{LogPrefix} 팩 {packs}종을 실제로 걸렀다 — {BaseCohortScope.Describe()}.");
        }
    }
}
