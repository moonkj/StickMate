using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ [상점] 배선(2026-09-06) — <b>무엇을 파는가 / 얼마인가 / 사면 무슨 일이 일어나는가</b>.
    ///
    /// <para>이 파일이 생기기 전까지 <c>CurrencyModel.TryPurchaseItem</c>을 부르는 <b>프로덕션 코드가
    /// 0건</b>이었다(테스트만 불렀다). 그래서 <c>RetiredSlotSurfaceTests</c>는 「상점이 배선되면 이
    /// 단언이 먼저 빨개진다」는 <b>예약석</b>만 둘 수 있었고, 실제 목록을 순회할 수 없었다.
    /// <b>여기가 그 예약석을 실물로 바꾼 자리다</b> — 이제 화면이 실제로 내놓는 목록을 훑는다.</para>
    ///
    /// ============================================================================
    /// 숫자를 베끼지 않는다
    /// ============================================================================
    /// 가격 600 / 1,400 / 3,200 / 9,600을 여기 적으면 이 파일이 프로덕션이 아니라 <b>옛 숫자</b>를
    /// 지키게 된다. 기대값은 <see cref="CurrencyRules"/>의 <b>이름 붙은 상수 넷</b>에서 만들고,
    /// <b>그 상수를 고르는 방식은 프로덕션과 다르게</b> 짠다(프로덕션은 <c>PriceCoins</c>의 switch,
    /// 여기는 등급→상수 표) — 같은 함수로 기대값을 만들면 그 함수가 틀어질 때 기대값도 함께 틀어져
    /// <b>아무것도 못 잰다</b>(TEAM.md 「생성기와 검사기가 같이 틀린다」).
    ///
    /// ============================================================================
    /// 이 파일이 <b>검증하지 못하는 것</b> — 숨기지 않는다
    /// ============================================================================
    /// <b>화면</b>이다. 카드 라벨·비활성 표시·확인 단계는 창을 띄워야 보이므로
    /// <c>Tests/PlayMode/ShopTabSurfaceTests</c>가 맡는다. 여기서 잠그는 것은 <b>판정과 결과</b>다.
    /// </summary>
    public sealed class ShopPurchaseFlowTests
    {
        private const string LogPrefix = "[상점-TEST]";

        private StickConfig _config;

        [SetUp]
        public void SetUp()
        {
            // QA 해금 스위치가 켜져 있으면 전 아이템이 「보유 중」이 되어 아래 대조가 전부 공허해진다.
            EquipmentDebugUnlock.SetTestOverride(false);
            CharacterProgressionModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
            _config = ScriptableObject.CreateInstance<StickConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            // ★ 2026-09-06 — null이 아니라 <b>false</b>다(같은 정정을 ItemOwnershipUnionTests /
            //   EquipmentStatLockedWornTests도 받았다). null은 강제 해제가 아니라 «실제 판정으로 복귀»이고,
            //   에디터의 실제 판정은 개발 구성이라 <b>켜짐</b>이다. EditMode 스위트의 규약은
            //   GlobalEditModeTestIsolation이 세운 <b>꺼짐</b>이라, null로 되돌리면 알파벳 순으로 뒤에 오는
            //   픽스처 전부가 «전 아이템 보유» 상태에서 돌게 된다.
            EquipmentDebugUnlock.SetTestOverride(false);
            CharacterProgressionModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private static void SetLevel(int level)
            => CharacterProgressionModel.RestoreFromSave(level, 0f, 0f, CharacterProgressionModel.CharacterName);

        /// <summary>잔액만 갈아 끼운다. <b>지급 경로를 타지 않는다</b> — 일일 상한·창·쿨다운이
        /// 끼어들면 이 파일이 재는 것이 「구매」인지 「수급」인지 갈리지 않는다.</summary>
        private static void SetBalance(int coins)
            => CurrencyModel.RestoreFromSave(new CurrencySaveState { CoinBalance = coins });

        /// <summary>지금 상점이 내놓는 상품 전부(화면이 쓰는 그 목록 그대로).</summary>
        private static List<ItemCatalogEntry> ShopMerchandise()
        {
            var list = new List<ItemCatalogEntry>();
            for (int i = 0; i < CharacterInfoWindow.ShopEntryCountForTests; i++)
            {
                int catalogIndex = CharacterInfoWindow.ShopCatalogIndexForTests(i);
                Assert.GreaterOrEqual(catalogIndex, 0, $"{LogPrefix} 상점 {i}번 자리가 카탈로그 밖을 가리킵니다.");
                ItemCatalogEntry entry = ItemCatalog.At(catalogIndex);
                Assert.IsNotNull(entry, $"{LogPrefix} 상점 {i}번 자리의 카탈로그 항목이 null입니다.");
                list.Add(entry);
            }
            return list;
        }

        /// <summary>등급 → 가격. <b>프로덕션의 <c>PriceCoins</c>를 부르지 않는다</b> — 같은 상수를
        /// 다른 방식으로 고른다(위 클래스 문서의 이유).</summary>
        private static int ExpectedPrice(ItemRarity rarity)
        {
            if (rarity == ItemRarity.Common) return CurrencyRules.CommonPriceCoins;
            if (rarity == ItemRarity.Rare) return CurrencyRules.RarePriceCoins;
            if (rarity == ItemRarity.Epic) return CurrencyRules.EpicPriceCoins;
            if (rarity == ItemRarity.Legendary) return CurrencyRules.LegendaryPriceCoins;
            Assert.Fail($"{LogPrefix} 등급 {(int)rarity}에 대응하는 가격 상수가 이 표에 없습니다 — " +
                        "등급 단이 늘었는데 가격표가 안 따라왔습니다.");
            return 0;
        }

        // ================================================================================
        // 1. 무엇을 파는가
        // ================================================================================

        /// <summary>★ 리더 지시 ② — 재화로 <b>살 수 있는 목록</b>에 은퇴한 것이 없어야 한다.
        /// <para><c>RetiredSlotSurfaceTests</c>가 「목록이 생기면 실제 순회로 바꿀 것」이라고 예약해 둔
        /// 그 검사다. 판정은 <see cref="ItemCatalog.IsListed"/> 하나가 하므로 여기서 술어를 다시 만들지
        /// 않고, <b>결과</b>만 훑는다.</para>
        /// <para>★ 2026-09-06 후속 — 단위가 <b>카테고리에서 아이템으로</b> 내려왔다(사용자 지시
        /// *"이펙트 없음은 왜 있는거야 삭제해줘 장비창에서"*). 카테고리 단위 술어만 남기면 «살아 있는
        /// 카테고리 안의 은퇴한 한 장»이 매대에 실려도 이 순회가 통과시킨다.</para></summary>
        [Test]
        public void 상품_목록에는_은퇴한_것이_한_건도_없다()
        {
            // 양성 대조 ① — 은퇴한 것이 실제로 있어야 아래 부재 단언이 뜻을 갖는다.
            // 은퇴 여부를 묻는 자리는 EquipmentModel.IsRetiredItem 하나다(여기에 목록을 다시 적지 않는다).
            int retiredItems = 0;
            int retiredSlotItems = 0;
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                int count = ItemCatalog.ItemCountIn(slot);
                for (int i = 0; i < count; i++)
                {
                    if (EquipmentModel.IsRetiredItem(slot, i)) retiredItems++;
                }
                if (EquipmentModel.IsRetiredSlot(slot)) retiredSlotItems += count;
            }
            Assert.Greater(retiredItems, 0,
                $"{LogPrefix} 은퇴한 장비가 0종입니다 — 이 검사가 공허해집니다.");

            // 양성 대조 ② — 그중 <b>아이템 단위</b> 은퇴가 실재하는가. 슬롯 단위만 남으면 이 순회는
            // 「카테고리가 통째로 빠졌다」만 재고 「목록 가운데 한 장이 빠졌다」는 못 잰다 —
            // 그 상태에서 이펙트 「없음」이 매대로 돌아와도 아무도 빨개지지 않는다.
            Assert.Greater(retiredItems - retiredSlotItems, 0,
                $"{LogPrefix} 아이템 단위 은퇴가 0건입니다 — 은퇴가 다시 카테고리 단위로만 남았습니다.");

            List<ItemCatalogEntry> shop = ShopMerchandise();
            Assert.Greater(shop.Count, 0, $"{LogPrefix} 상점이 아무것도 팔지 않습니다 — 선반이 통째로 비었습니다.");

            foreach (ItemCatalogEntry entry in shop)
            {
                Assert.IsTrue(entry.Slot.HasValue,
                    $"{LogPrefix} [{entry.Id}]에는 슬롯이 없습니다 — 가격이 정의되지 않는 것이 상품으로 실렸습니다.");
                Assert.IsFalse(EquipmentModel.IsRetiredItem(entry.Slot.Value, entry.ItemIndex),
                    $"{LogPrefix} 은퇴한 [{entry.DisplayName}]가 상점에 실렸습니다 — " +
                    "「살 수는 있는데 못 입는」 물건입니다.");
            }
        }

        /// <summary>상품은 <b>장비 · 기본 코호트</b>뿐이다(행동은 등급이 없어 가격이 없고,
        /// DLC 팩은 동전 경제 밖이다 — 결제 백엔드가 이 프로젝트에 없다).</summary>
        [Test]
        public void 상품은_장비_기본코호트만이고_행동과_DLC팩은_실리지_않는다()
        {
            List<ItemCatalogEntry> shop = ShopMerchandise();

            // 양성 대조 — 카탈로그에 행동이 실제로 있어야 「행동은 안 실린다」가 뜻을 갖는다.
            Assert.Greater(ItemCatalog.ActionCount, 0,
                $"{LogPrefix} 카탈로그에 행동이 0종입니다 — 이 검사가 공허해집니다.");

            foreach (ItemCatalogEntry entry in shop)
            {
                Assert.AreEqual(ItemCategory.Equipment, entry.Category,
                    $"{LogPrefix} [{entry.Id}]는 장비가 아닙니다 — 등급도 가격도 없는 것이 실렸습니다.");
                Assert.AreEqual(ItemCatalog.BaseCohortId, entry.CohortId,
                    $"{LogPrefix} [{entry.Id}]는 팩 코호트({entry.CohortId})입니다 — " +
                    "결제 백엔드가 없는 상태로 DLC가 동전에 노출됐습니다.");
            }

            // 목록 길이를 <b>독립적으로</b> 다시 센다 — «프로덕션 필터»(CharacterInfoWindow의
            // IsShopMerchandise / ItemCatalog.IsListed)를 부르지 않고 조건을 여기서 다시 조립한다.
            // ★ 다만 «무엇이 은퇴했는가»는 EquipmentModel.IsRetiredItem에게 묻는다 — 그 사실을 여기서
            //   다시 정의하면 술어가 두 곳에 생기고, 그게 정확히 2026-09-06에 난 사고다:
            //   이 줄이 IsRetiredSlot(카테고리 단위)로 남아 있어 이펙트 「없음」 한 장을 세지 못했고,
            //   «상점 목록 35 vs 독립 계수 36»이라는 빨강이 <b>프로덕션이 아니라 이 파일 탓으로</b> 떴다.
            int expected = 0;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e == null || e.Category != ItemCategory.Equipment) continue;
                if (!e.Slot.HasValue) continue;
                if (EquipmentModel.IsRetiredItem(e.Slot.Value, e.ItemIndex)) continue;
                if (e.CohortId != ItemCatalog.BaseCohortId) continue;
                expected++;
            }
            Assert.AreEqual(expected, shop.Count,
                $"{LogPrefix} 상점 목록이 {shop.Count}종인데 독립 계수는 {expected}종입니다 — 열거가 샙니다.");

            // 오늘의 사실 하나 더: 팩이 0개이므로 상점 모집단은 화면 분모와 같아야 한다.
            Assert.AreEqual(ItemCatalog.ListedEquipmentCount, shop.Count,
                $"{LogPrefix} 상점({shop.Count})과 화면 분모({ItemCatalog.ListedEquipmentCount})가 다른 " +
                "모집단을 셉니다 — 팩이 실렸거나 한쪽만 은퇴를 반영했습니다.");
        }

        // ================================================================================
        // 2. 얼마인가
        // ================================================================================

        /// <summary>가격의 출처는 <see cref="CurrencyRules"/> 하나다 —
        /// 화면이 자기 가격표를 갖고 있으면 그 순간 같은 사실이 두 곳에서 산다.</summary>
        [Test]
        public void 가격은_등급에서_파생되고_공짜_상품이_없다()
        {
            // 상수 자체의 불변식 — 0원이 하나라도 있으면 그 등급은 「공짜」가 된다.
            Assert.Greater(CurrencyRules.CommonPriceCoins, 0, $"{LogPrefix} 일반 가격이 0 이하입니다.");
            Assert.Less(CurrencyRules.CommonPriceCoins, CurrencyRules.RarePriceCoins,
                $"{LogPrefix} 가격이 등급 순으로 오르지 않습니다(일반 ≥ 희귀).");
            Assert.Less(CurrencyRules.RarePriceCoins, CurrencyRules.EpicPriceCoins,
                $"{LogPrefix} 가격이 등급 순으로 오르지 않습니다(희귀 ≥ 영웅).");
            Assert.Less(CurrencyRules.EpicPriceCoins, CurrencyRules.LegendaryPriceCoins,
                $"{LogPrefix} 가격이 등급 순으로 오르지 않습니다(영웅 ≥ 전설).");

            var seenRarities = new HashSet<ItemRarity>();
            foreach (ItemCatalogEntry entry in ShopMerchandise())
            {
                ItemRarity rarity = ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex);
                seenRarities.Add(rarity);

                int price = CharacterInfoWindow.ShopPriceCoins(entry);
                Assert.AreEqual(ExpectedPrice(rarity), price,
                    $"{LogPrefix} [{entry.DisplayName}]({ItemCatalog.RarityName(rarity)})의 상점 가격이 " +
                    "등급 상수와 다릅니다 — 화면이 자기 가격표를 갖기 시작했습니다.");
                Assert.Greater(price, 0, $"{LogPrefix} [{entry.DisplayName}]가 0원입니다.");
            }

            // 네 단이 전부 매대에 있어야 위 대조가 네 상수를 다 지나간다.
            Assert.AreEqual(4, seenRarities.Count,
                $"{LogPrefix} 매대에 등급이 {seenRarities.Count}종뿐입니다 — 가격 상수 넷 중 일부가 " +
                "이 검사를 지나가지 않았습니다.");
        }

        // ================================================================================
        // 3. 사면 무슨 일이 일어나는가
        // ================================================================================

        /// <summary>이 파일이 쓰는 「아직 안 가진 상품」 — 요구 레벨이 가장 높은 것.</summary>
        private ItemCatalogEntry FirstLockedItem()
        {
            ItemCatalogEntry locked = null;
            foreach (ItemCatalogEntry e in ShopMerchandise())
            {
                if (!e.RequiredLevel.HasValue || e.RequiredLevel.Value <= 1) continue;
                if (locked == null || e.RequiredLevel.Value > locked.RequiredLevel.Value) locked = e;
            }
            Assert.IsNotNull(locked, $"{LogPrefix} 요구 레벨 > 1 인 상품이 없습니다 — 살 것이 없습니다.");
            return locked;
        }

        [Test]
        public void 상태는_보유_구매가능_잔액부족_셋으로_갈린다()
        {
            SetLevel(1);
            ItemCatalogEntry locked = FirstLockedItem();
            int price = CharacterInfoWindow.ShopPriceCoins(locked);

            SetBalance(price - 1);
            Assert.AreEqual(CharacterInfoWindow.ShopState.Insufficient,
                CharacterInfoWindow.ResolveShopState(locked, _config, CurrencyModel.CoinBalance),
                $"{LogPrefix} 가격 {price}에 잔액 {price - 1}인데 「잔액 부족」이 아닙니다.");

            SetBalance(price);
            Assert.AreEqual(CharacterInfoWindow.ShopState.Affordable,
                CharacterInfoWindow.ResolveShopState(locked, _config, CurrencyModel.CoinBalance),
                $"{LogPrefix} 가격과 잔액이 같은데 살 수 없다고 합니다 — 경계가 한 칸 어긋났습니다.");

            // 레벨로 열려도 「보유」다 — 화면은 어떻게 얻었는지를 구별하지 않는다(설계 §3-3 S3/S3b).
            SetLevel(locked.RequiredLevel.Value);
            Assert.AreEqual(CharacterInfoWindow.ShopState.Owned,
                CharacterInfoWindow.ResolveShopState(locked, _config, CurrencyModel.CoinBalance),
                $"{LogPrefix} 요구 레벨에 닿았는데 아직 팔고 있습니다.");
        }

        [Test]
        public void 구매하면_잔액이_정확히_가격만큼_줄고_즉시_보유가_된다()
        {
            SetLevel(1);
            ItemCatalogEntry locked = FirstLockedItem();
            int price = CharacterInfoWindow.ShopPriceCoins(locked);

            SetBalance(price + 7);   // 거스름돈이 남는 값 — 「전액이 사라진다」류 결함을 함께 잡는다.
            Assert.IsFalse(locked.IsOwned(_config), $"{LogPrefix} 사기 전인데 이미 보유입니다.");

            Assert.IsTrue(CurrencyModel.TryPurchaseItem(locked.Id, price),
                $"{LogPrefix} 잔액이 충분한데 구매가 거절됐습니다.");

            Assert.AreEqual(7, CurrencyModel.CoinBalance,
                $"{LogPrefix} 잔액이 정확히 가격({price})만큼 줄지 않았습니다.");
            Assert.IsTrue(locked.IsOwned(_config),
                $"{LogPrefix} 샀는데 보유가 아닙니다 — IsOwned 합집합에 구매 항이 안 들어갔습니다.");
            Assert.AreEqual(CharacterInfoWindow.ShopState.Owned,
                CharacterInfoWindow.ResolveShopState(locked, _config, CurrencyModel.CoinBalance),
                $"{LogPrefix} 샀는데 상점이 아직 팔고 있습니다.");
            Assert.IsTrue(CurrencyModel.IsDirty,
                $"{LogPrefix} 구매가 저장 대상으로 표시되지 않았습니다 — 껐다 켜면 산 것이 사라집니다.");
        }

        [Test]
        public void 중복_구매는_차단되고_잔액이_그대로다()
        {
            SetLevel(1);
            ItemCatalogEntry locked = FirstLockedItem();
            int price = CharacterInfoWindow.ShopPriceCoins(locked);

            SetBalance(price * 3);
            Assert.IsTrue(CurrencyModel.TryPurchaseItem(locked.Id, price));
            int after = CurrencyModel.CoinBalance;

            Assert.IsFalse(CurrencyModel.TryPurchaseItem(locked.Id, price),
                $"{LogPrefix} 같은 것을 두 번 샀습니다(경제 원칙 E-1 위반).");
            Assert.AreEqual(after, CurrencyModel.CoinBalance,
                $"{LogPrefix} 중복 구매가 거절됐는데 잔액이 줄었습니다 — 돈만 사라집니다.");
            Assert.AreEqual(1, CurrencyModel.PurchasedItemIds.Count,
                $"{LogPrefix} 구매 이력이 {CurrencyModel.PurchasedItemIds.Count}줄입니다 — 집합이 아닙니다.");
        }

        [Test]
        public void 잔액이_모자라면_구매가_실패하고_잔액이_그대로다()
        {
            SetLevel(1);
            ItemCatalogEntry locked = FirstLockedItem();
            int price = CharacterInfoWindow.ShopPriceCoins(locked);

            SetBalance(price - 1);
            Assert.IsFalse(CurrencyModel.TryPurchaseItem(locked.Id, price),
                $"{LogPrefix} 1동전 모자란데 구매가 성사됐습니다.");
            Assert.AreEqual(price - 1, CurrencyModel.CoinBalance,
                $"{LogPrefix} 실패한 구매가 잔액을 건드렸습니다.");
            Assert.IsFalse(locked.IsOwned(_config),
                $"{LogPrefix} 실패한 구매로 보유가 됐습니다 — 공짜로 열렸습니다.");
            Assert.AreEqual(0, CurrencyModel.PurchasedItemIds.Count,
                $"{LogPrefix} 실패한 구매가 이력에 남았습니다.");
        }

        /// <summary>★ 음수 잔액이 생길 수 있는 경로가 없는지 — 매대 전체를 <b>가진 돈만큼</b> 쓸어 담아 본다.
        /// <para>중간에 한 번이라도 음수가 되면 화면에 「−300동전」을 그릴 자리가 없다
        /// (<c>CurrencyRules.ClampCoinBalance</c> 문서).</para></summary>
        [Test]
        public void 매대를_전부_쓸어담아도_잔액이_음수가_되지_않는다()
        {
            SetLevel(1);
            List<ItemCatalogEntry> shop = ShopMerchandise();

            int total = 0;
            foreach (ItemCatalogEntry e in shop) total += CharacterInfoWindow.ShopPriceCoins(e);
            SetBalance(total / 2);   // 절반만 준다 — 중간에 반드시 못 사는 지점이 온다.

            int bought = 0, refused = 0;
            foreach (ItemCatalogEntry e in shop)
            {
                if (e.IsOwned(_config)) continue;
                int price = CharacterInfoWindow.ShopPriceCoins(e);
                bool ok = CurrencyModel.CoinBalance >= price && CurrencyModel.TryPurchaseItem(e.Id, price);
                if (ok) bought++; else refused++;
                Assert.GreaterOrEqual(CurrencyModel.CoinBalance, 0,
                    $"{LogPrefix} [{e.DisplayName}] 구매 뒤 잔액이 음수({CurrencyModel.CoinBalance})가 됐습니다.");
            }

            Assert.Greater(bought, 0, $"{LogPrefix} 절반의 예산으로 한 개도 못 샀습니다 — 이 검사가 공허합니다.");
            Assert.Greater(refused, 0, $"{LogPrefix} 절반의 예산으로 전부 샀습니다 — 거절 경로를 지나가지 않았습니다.");
        }
    }
}
