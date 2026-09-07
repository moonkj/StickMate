using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 보유 = 레벨 파생 ∪ 상점 구매 (<see cref="ItemCatalogEntry.IsOwned"/>, DESIGN_SYSTEMS_STATS §20-2-a).
    ///
    /// <para><c>Core/ItemCatalog.cs</c>·<c>Core/CurrencyModel.cs</c>의 주석이 「Tests/EditMode/ItemOwnershipUnionTests 가 이 문장을
    /// 매 실행 잠근다」고 적어 두었는데 그 파일이 없었다(security S-2 실측, 2026-09-05 — 거짓 주석). 이 파일이 그 약속을 실물로 만든다.</para>
    ///
    /// <para>잠그는 것: (1) 기본 코호트 아이템은 요구 레벨에 닿으면 보유이고 미달이면 아니다, (2) 상점 구매는 레벨 파생 보유에 <b>더해지지</b>
    /// 대체하지 않는다 — 구매 이력이 비어 있어도 레벨 파생 항은 살아 있다(v9 세이브 호환의 근거), (3) QA 해금 스위치가 켜지면 위 둘이
    /// 가려진다(그래서 이 파일은 스위치를 강제로 끈다 — 양성 대조). 팩 아이템(다른 코호트)이 아직 없어 그 항은 지금 공집합이다.</para>
    ///
    /// <para>레벨·아이디·개수는 전부 카탈로그에서 읽는다 — 숫자·식별자 사본 0.</para>
    /// </summary>
    public sealed class ItemOwnershipUnionTests
    {
        private StickConfig _config;

        [SetUp]
        public void SetUp()
        {
            EquipmentDebugUnlock.SetTestOverride(false);
            CharacterProgressionModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            _config = ScriptableObject.CreateInstance<StickConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            // ★ 2026-09-06 — null이 아니라 <b>false</b>다. null은 "실제 판정으로 되돌린다"이고 에디터의
            //   실제 판정은 <b>켜짐</b>이라, 여기서 null을 넣으면 알파벳 순으로 뒤에 오는 픽스처가
            //   QA 해금이 켜진 채로 돈다(잠금 단언이 조용히 공허해진다).
            //   EditMode 스위트의 규약은 GlobalEditModeTestIsolation이 세운 <b>꺼짐</b>이다.
            EquipmentDebugUnlock.SetTestOverride(false);
            CharacterProgressionModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private static void SetLevel(int level)
            => CharacterProgressionModel.RestoreFromSave(level, 0f, 0f, CharacterProgressionModel.CharacterName);

        /// <summary>기본 코호트의 장비(요구 레벨이 있는 것) 전부.</summary>
        private static List<ItemCatalogEntry> BaseCohortEquipment()
        {
            var list = new List<ItemCatalogEntry>();
            foreach (ItemCatalogEntry e in ItemCatalog.Entries)
            {
                if (e.Category != ItemCategory.Equipment || !e.RequiredLevel.HasValue) continue;
                if (e.CohortId != ItemCatalog.BaseCohortId) continue;
                list.Add(e);
            }
            Assert.Greater(list.Count, 0, "기본 코호트 장비가 하나도 없습니다 — 카탈로그가 비었습니다.");
            return list;
        }

        [Test]
        public void 기본_코호트_아이템은_요구_레벨에_닿으면_보유이고_미달이면_아니다()
        {
            List<ItemCatalogEntry> items = BaseCohortEquipment();
            int maxRequired = 1, aboveOne = 0, atOne = 0;
            foreach (ItemCatalogEntry e in items)
            {
                maxRequired = Mathf.Max(maxRequired, e.RequiredLevel.Value);
                if (e.RequiredLevel.Value > 1) aboveOne++; else atOne++;
            }
            Assert.Greater(aboveOne, 0, "요구 레벨이 1보다 큰 장비가 없습니다 — 「미달이면 거짓」 대조가 공허합니다.");
            Assert.Greater(atOne, 0, "처음부터 보유하는(요구 레벨 1) 장비가 없습니다 — 「닿으면 참」 대조가 공허합니다.");

            // 요구 레벨에 정확히 닿으면 참, 하나 아래면 거짓 — 아이템마다.
            foreach (ItemCatalogEntry e in items)
            {
                int need = e.RequiredLevel.Value;
                SetLevel(need);
                Assert.IsTrue(e.IsOwned(_config), $"{e.Id}: 요구 레벨 {need}에 닿았는데 보유가 아닙니다.");
                if (need > 1)
                {
                    SetLevel(need - 1);
                    Assert.IsFalse(e.IsOwned(_config), $"{e.Id}: 요구 레벨 {need} 미달(Lv.{need - 1})인데 보유입니다.");
                }
            }

            // 최고 요구 레벨에 서면 기본 코호트 전부가 보유다(사용자 확정 차단선 「캡은 기본 42종만으로 도달」의 보유 쪽 절반).
            SetLevel(maxRequired);
            foreach (ItemCatalogEntry e in items) Assert.IsTrue(e.IsOwned(_config), $"{e.Id}: Lv.{maxRequired}(최고 요구 레벨)에서 보유가 아닙니다.");
        }

        [Test]
        public void 상점_구매는_레벨_파생_보유에_더해지지_대체하지_않는다()
        {
            List<ItemCatalogEntry> items = BaseCohortEquipment();
            ItemCatalogEntry locked = null, free = null;
            foreach (ItemCatalogEntry e in items)
            {
                if (e.RequiredLevel.Value > 1 && (locked == null || e.RequiredLevel.Value > locked.RequiredLevel.Value)) locked = e;
                if (e.RequiredLevel.Value == 1 && free == null) free = e;
            }
            Assert.IsNotNull(locked, "요구 레벨 > 1 인 장비가 없습니다 — 합집합 대조가 공허합니다.");
            Assert.IsNotNull(free, "요구 레벨 1 장비가 없습니다 — 레벨 파생 항의 생존을 잴 대상이 없습니다.");

            // v9 호환의 근거: 구매 이력이 비어 있어도(= 키가 없는 옛 세이브) 레벨 파생 항은 그대로 살아 있다.
            Assert.AreEqual(0, CurrencyModel.PurchasedItemIds.Count, "시작 상태에 구매 이력이 있습니다 — ResetForTesting 이 비우지 않았습니다.");
            SetLevel(1);
            Assert.IsTrue(free.IsOwned(_config), $"{free.Id}: 구매 이력이 비어 있는데 레벨 파생 보유가 죽었습니다(합집합이 「대체」로 바뀌었습니다).");
            Assert.IsFalse(locked.IsOwned(_config), $"{locked.Id}: Lv.1 에서 이미 보유입니다 — 아래 구매 대조가 뜻을 잃습니다.");

            // 상점 구매 = 합집합의 둘째 항. 값(0코인)은 잔액과 무관하게 이력만 남긴다.
            Assert.IsTrue(CurrencyModel.TryPurchaseItem(locked.Id, 0), $"{locked.Id}: 구매가 거절됐습니다.");
            Assert.IsTrue(CurrencyModel.IsPurchasedItem(locked.Id));
            Assert.IsTrue(locked.IsOwned(_config), $"{locked.Id}: 샀는데 레벨 미달이라고 보유가 아닙니다 — 구매 항이 합집합에 안 들어갑니다.");
            Assert.IsTrue(free.IsOwned(_config), $"{free.Id}: 다른 아이템을 사자 레벨 파생 보유가 사라졌습니다 — 「대체」의 증상입니다.");

            // 같은 것을 두 번 살 수 없다(경제 원칙 E-1) — 이력이 한 줄이어야 아래 합집합이 「집합」이다.
            Assert.IsFalse(CurrencyModel.TryPurchaseItem(locked.Id, 0));
            Assert.AreEqual(1, CurrencyModel.PurchasedItemIds.Count);
        }

        /// <summary>
        /// ★ 2026-09-07 P0 회귀 잠금 — 실기 신고 "종이비행기 펫샀는데 착용이 안됨"의 재현.
        ///
        /// <para>위 <c>상점_구매는_레벨_파생_보유에_더해지지_대체하지_않는다</c>는 <see cref="ItemCatalogEntry.IsOwned"/>
        /// 까지만 대조하고 끝난다. 실제 사고는 그 <b>한 단계 아래</b>에 있었다 — 착용을 실제로 거는
        /// <see cref="EquipmentModel.TryWear"/>는 <see cref="ItemCatalogEntry.IsOwned"/>가 아니라
        /// <see cref="EquipmentModel.IsItemOwned"/>를 보는데, 그 메서드는 레벨만 보고 구매 이력을
        /// 보지 않았다. 그래서 보관함 카드는 "보유"로 뜨고 착용 버튼도 눌리는데(관문 통과),
        /// <see cref="EquipmentModel.TryWear"/>가 조용히 false를 돌려줘 클릭이 아무 효과가 없었다.
        /// 이 테스트는 <b>카탈로그 단언이 아니라 실제 착용 API 호출 결과</b>를 대조해 그 층을 잠근다 —
        /// 카드가 "보유"라고 말하는 아이템은 반드시 <see cref="EquipmentModel.TryWear"/>로도 걸쳐져야 한다.</para>
        /// </summary>
        [Test]
        public void 상점에서_산_아이템은_실제로_착용된다()
        {
            List<ItemCatalogEntry> items = BaseCohortEquipment();
            ItemCatalogEntry locked = null;
            foreach (ItemCatalogEntry e in items)
            {
                if (e.RequiredLevel.Value > 1 && (locked == null || e.RequiredLevel.Value > locked.RequiredLevel.Value)) locked = e;
            }
            Assert.IsNotNull(locked, "요구 레벨 > 1 인 장비가 없습니다 — 이 대조가 공허합니다.");
            Assert.IsTrue(locked.Slot.HasValue, $"{locked.Id}: 장비인데 슬롯이 없습니다.");

            SetLevel(1);

            // 음성 대조 — 사기 전에는 착용 관문이 실제로 막혀 있어야 이 테스트가 뭔가를 재는 것이다.
            Assert.IsFalse(EquipmentModel.TryWear(locked.Slot.Value, locked.ItemIndex, _config),
                $"{locked.Id}: 사기 전인데 착용이 됩니다 — 레벨 잠금 자체가 깨져 있어 아래 양성 대조가 뜻을 잃습니다.");
            Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(locked.Slot.Value));

            Assert.IsTrue(CurrencyModel.TryPurchaseItem(locked.Id, 0), $"{locked.Id}: 구매가 거절됐습니다.");
            Assert.IsTrue(locked.IsOwned(_config), $"{locked.Id}: 샀는데 카탈로그가 보유로 안 봅니다 — 카드 자체가 잠긴 채로 뜹니다.");

            // 양성 대조 — 실기 신고가 정확히 이 줄에서 실패했다: 카드는 "보유"인데 착용 API가 false를 돌려줬다.
            Assert.IsTrue(EquipmentModel.TryWear(locked.Slot.Value, locked.ItemIndex, _config),
                $"{locked.Id}: 카탈로그는 보유라는데 EquipmentModel.TryWear가 거절합니다 — " +
                "IsItemOwned가 상점 구매를 안 보고 레벨만 보는 회귀입니다(실기 신고: 구매했는데 착용 안됨).");
            Assert.AreEqual(locked.ItemIndex, EquipmentModel.WornIndex(locked.Slot.Value),
                $"{locked.Id}: TryWear는 참을 돌려줬는데 실제 착용 슬롯이 그 아이템이 아닙니다.");
        }

        /// <summary>양성 대조 — QA 해금 스위치가 켜지면 위 두 검사의 「거짓」쪽이 전부 가려진다. 그래서 SetUp 이 스위치를 강제로 끈다.</summary>
        [Test]
        public void 컨트롤_해금_스위치가_켜지면_미달_아이템도_보유로_보인다()
        {
            List<ItemCatalogEntry> items = BaseCohortEquipment();
            ItemCatalogEntry locked = null;
            foreach (ItemCatalogEntry e in items) if (e.RequiredLevel.Value > 1) { locked = e; break; }
            Assert.IsNotNull(locked);
            SetLevel(1);
            Assert.IsFalse(locked.IsOwned(_config), "스위치 OFF 인데 미달 아이템이 보유입니다 — 강제 OFF 가 안 먹었습니다.");
            EquipmentDebugUnlock.SetTestOverride(true);
            Assert.IsTrue(locked.IsOwned(_config), "스위치 ON 인데 미달 아이템이 보유가 아닙니다 — 스위치가 IsOwned 에 안 물려 있습니다.");
        }
    }
}
