using NUnit.Framework;
using UnityEditor;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>은퇴한 카테고리가 사람에게 보이는 모든 자리에서 빠졌는가</b> —
    /// 2026-09-06 사용자 지시 *"외형에서 머리 스타일은 전체 삭제"*.
    ///
    /// ============================================================================
    /// 이 파일이 겨누는 실패 형태
    /// ============================================================================
    /// 카테고리를 <b>데이터에서 지우지 않고</b> 화면에서만 뺐다. 그 방식의 고유한 함정은
    /// "어떤 화면은 따라왔고 어떤 화면은 안 따라왔다"이고, 그 둘은 <b>컴파일도 EditMode도
    /// 똑같이 초록</b>이다. 실제로 이 라운드에서 나온 형태가 그것이었다 — [외형] 탭에서는
    /// 빠졌는데 [보관함]에는 6종이 그대로 남아, 전체 해금 상태의 사용자가 "보이는데 못 입는"
    /// 물건 6개를 직접 마주쳤다.
    ///
    /// ============================================================================
    /// 왜 숫자를 적지 않는가
    /// ============================================================================
    /// 여기 <c>36</c>도 <c>42</c>도 적지 않는다. 이 저장소는 상한값/스키마 버전을 숫자로 베꼈다가
    /// 4건이 한꺼번에 깨진 적이 있다(CLAUDE.md 협업 프로토콜). 대신 <b>관계</b>를 잠근다:
    /// 표시 분모 = 전량 − 은퇴 슬롯의 종수, 그리고 분자와 분모가 <b>같은 모집단</b>.
    /// 슬롯을 하나 더 은퇴시키는 날 이 파일은 고칠 것이 없어야 한다.
    ///
    /// ★ 그리고 <b>부재 단언은 썩으면 조용히 초록이 된다</b>. 그래서 모든 부재 단언 앞에
    /// 양성 대조를 세운다 — "은퇴한 슬롯이 실제로 하나 이상 있고, 거기 아이템도 있다".
    /// 은퇴가 통째로 풀리면 그 대조가 <b>먼저</b> 빨개진다.
    /// </summary>
    public sealed class RetiredSlotSurfaceTests
    {
        private const string LogPrefix = "[은퇴슬롯]";
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static StickConfig LoadDefaultConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"{LogPrefix} 기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        [SetUp]
        public void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
        }

        [TearDown]
        public void ResetModelsAfter()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
        }

        /// <summary>은퇴한 슬롯의 종수 합 — 기대값을 <b>세서</b> 만든다.</summary>
        private static int RetiredItemCount()
        {
            int n = 0;
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                if (!EquipmentModel.IsRetiredSlot(slot)) continue;
                n += ItemCatalog.ItemCountIn(slot);
            }
            return n;
        }

        /// <summary>★ 양성 대조 본체. 이 픽스처의 나머지 단언은 전부 "은퇴한 것이 실재한다"를
        /// 전제로 하는 <b>부재</b> 단언이라, 전제가 사라지면 전부 공허하게 통과한다.</summary>
        [Test]
        public void 컨트롤_은퇴한_슬롯이_실재하고_그_안에_아이템도_있다()
        {
            int retiredSlots = 0;
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                if (EquipmentModel.IsRetiredSlot((EquipmentSlot)s)) retiredSlots++;
            }

            Assert.Greater(retiredSlots, 0,
                $"{LogPrefix} 은퇴한 슬롯이 하나도 없습니다 — 이 파일의 부재 단언이 전부 " +
                "0건을 세고 조용히 통과합니다(부재 단언이 썩는 방식).");
            Assert.Greater(RetiredItemCount(), 0,
                $"{LogPrefix} 은퇴한 슬롯에 아이템이 0종입니다 — 은퇴는 <b>데이터 삭제가 아니다</b>. " +
                "에셋이 사라졌다면 등급 파생/코호트 순위의 모집단이 함께 움직인 것이라 별건입니다.");
            Assert.Less(retiredSlots, EquipmentModel.SlotCount,
                $"{LogPrefix} 모든 슬롯이 은퇴했습니다 — 화면에 남는 것이 없습니다.");
        }

        /// <summary>표시 분모는 <b>파생값</b>이다: 전량 − 은퇴 슬롯의 종수.</summary>
        [Test]
        public void 표시_분모는_전량에서_은퇴_슬롯을_뺀_파생값이다()
        {
            Assert.AreEqual(ItemCatalog.EquipmentCount - RetiredItemCount(),
                ItemCatalog.ListedEquipmentCount,
                $"{LogPrefix} 화면 분모가 「전량 − 은퇴분」이 아닙니다. 숫자를 손으로 적어 두면 " +
                "슬롯을 하나 더 은퇴시키는 날 분모만 뒤처집니다.");

            // 공허 방지 — 두 값이 <b>실제로 다른가</b>. 같으면 위 뺄셈이 0을 뺀 것이다.
            Assert.Less(ItemCatalog.ListedEquipmentCount, ItemCatalog.EquipmentCount,
                $"{LogPrefix} 화면 분모와 카탈로그 전량이 같습니다 — 은퇴가 아무것도 빼지 않았습니다.");

            // 감사·골든이 쓰는 전량은 <b>움직이지 않았다</b>. 이 쪽이 은퇴를 따라가면
            // "은퇴시킨 자리는 검사도 안 한다"가 되어, 되살리는 날 잠금 없는 아이템이 돌아온다.
            int assetTotal = 0;
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                assetTotal += ItemCatalog.ItemCountIn((EquipmentSlot)s);
            }
            Assert.AreEqual(assetTotal, ItemCatalog.EquipmentCount,
                $"{LogPrefix} 카탈로그 전량이 슬롯별 종수 합과 어긋납니다 — 표시 필터가 데이터로 새어 " +
                "들어갔습니다(등급 파생의 모집단이 흔들립니다).");
        }

        /// <summary>★ 리더 지시 ①의 실측 — <b>분자와 분모가 같은 모집단</b>인가.
        /// 한쪽만 은퇴를 반영하면 「7 / 36」인데 고를 수 있는 것은 6개인 상태가 되고,
        /// 그 어긋남은 화면만 봐서는 못 찾는다.</summary>
        [Test]
        public void 표시_분자와_분모가_같은_모집단을_센다()
        {
            StickConfig config = LoadDefaultConfig();

            // 전부 열릴 만큼 올린다 — 이 상태에서 분자 == 분모여야 두 모집단이 같은 것이다.
            CharacterProgressionModel.AddXp(1000000f, config);

            Assert.AreEqual(ItemCatalog.EquipmentCount, ItemCatalog.UnlockedEquipmentCount(config),
                $"{LogPrefix} 준비 조건 실패 — 전량이 다 열리지 않았습니다. 아래 비교가 뜻을 잃습니다.");
            Assert.AreEqual(ItemCatalog.ListedEquipmentCount, ItemCatalog.ListedUnlockedEquipmentCount(config),
                $"{LogPrefix} 화면 분자({ItemCatalog.ListedUnlockedEquipmentCount(config)})와 " +
                $"분모({ItemCatalog.ListedEquipmentCount})가 다른 모집단을 세고 있습니다 — " +
                "한쪽만 은퇴를 반영한 상태입니다.");

            // 그리고 그 차이가 정확히 은퇴분이다(분자 쪽도 은퇴를 빼고 있다는 직접 증거).
            Assert.AreEqual(RetiredItemCount(),
                ItemCatalog.UnlockedEquipmentCount(config) - ItemCatalog.ListedUnlockedEquipmentCount(config),
                $"{LogPrefix} 분자에서 빠진 수가 은퇴분과 다릅니다.");
        }

        /// <summary>카탈로그의 모든 항목을 훑어 <see cref="ItemCatalog.IsListed"/>가 정확히
        /// 은퇴 슬롯만 거르는지 본다(전수 — 표본이 아니다).</summary>
        [Test]
        public void 목록_술어는_정확히_은퇴_슬롯만_거른다()
        {
            int filtered = 0;
            int kept = 0;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                Assert.IsNotNull(entry, $"{LogPrefix} 카탈로그 {i}번이 비었습니다.");

                bool retired = entry.Slot.HasValue && EquipmentModel.IsRetiredSlot(entry.Slot.Value);
                Assert.AreEqual(!retired, ItemCatalog.IsListed(entry),
                    $"{LogPrefix} [{entry.Id}]의 목록 노출 판정이 은퇴 여부와 어긋납니다.");

                if (retired) filtered++; else kept++;
            }

            Assert.AreEqual(RetiredItemCount(), filtered,
                $"{LogPrefix} 걸러진 항목이 {filtered}종인데 은퇴분은 {RetiredItemCount()}종입니다 — 열거가 샙니다.");
            Assert.Greater(kept, 0, $"{LogPrefix} 남은 항목이 0종입니다 — 목록이 통째로 비었습니다.");
        }

        /// <summary>★ [보관함] 목록 자체 — <b>어느 줄도</b> 은퇴한 카테고리를 가리키지 않는다.
        /// <para>창을 열지 않고 잰다: 줄 ↔ 카탈로그 매핑은 정적 사실이고, 그 매핑이 곧 화면에
        /// 무엇이 뜨는가다(<c>RefreshInventoryList</c>가 이 표만 보고 줄을 채운다).</para></summary>
        [Test]
        public void 보관함_목록에는_은퇴한_카테고리가_한_줄도_없다()
        {
            int lines = CharacterInfoWindow.InventoryLineCountForTests;
            Assert.Greater(lines, 2, $"{LogPrefix} 보관함 줄이 헤더뿐입니다 — 잴 것이 없습니다.");

            int headers = 0;
            int items = 0;
            int equipment = 0;
            for (int line = 0; line < lines; line++)
            {
                int catalogIndex = CharacterInfoWindow.InventoryCatalogIndexForLineForTests(line);
                if (catalogIndex < 0) { headers++; continue; }

                ItemCatalogEntry entry = ItemCatalog.At(catalogIndex);
                Assert.IsNotNull(entry,
                    $"{LogPrefix} {line}번 줄이 카탈로그 밖({catalogIndex})을 가리킵니다 — " +
                    "줄 표가 카탈로그와 어긋났습니다.");

                Assert.IsFalse(entry.Slot.HasValue && EquipmentModel.IsRetiredSlot(entry.Slot.Value),
                    $"{LogPrefix} 보관함 {line}번 줄이 은퇴한 카테고리의 [{entry.DisplayName}]를 " +
                    "보여줍니다 — 고를 수 없는 물건이 목록에 남았습니다.");

                items++;
                if (entry.Category == ItemCategory.Equipment) equipment++;
            }

            Assert.AreEqual(2, headers,
                $"{LogPrefix} 헤더 줄이 {headers}개입니다 — 「걸치는 것」/「할 줄 아는 것」 둘이어야 합니다.");
            Assert.AreEqual(ItemCatalog.ListedEquipmentCount, equipment,
                $"{LogPrefix} 목록의 장비 줄이 {equipment}개인데 화면 분모는 " +
                $"{ItemCatalog.ListedEquipmentCount}입니다 — 헤더가 목록과 다른 수를 말합니다.");
            Assert.AreEqual(ItemCatalog.ListedEquipmentCount + ItemCatalog.ActionCount, items,
                $"{LogPrefix} 목록 항목 줄이 {items}개입니다 — 장비 + 행동과 다릅니다.");
            Assert.AreEqual(items + headers, lines,
                $"{LogPrefix} 줄 수 계산이 맞지 않습니다(항목 {items} + 헤더 {headers} ≠ {lines}).");
        }

        /// <summary>재화로 <b>살 수 있는 목록</b>에도 은퇴분이 없어야 한다(리더 지시 ②).
        /// <para>2026-09-06 현재 그 목록은 <b>존재하지 않는다</b> — [상점] 탭은 문구 한 줄짜리
        /// 준비 중 페이지이고, <c>CurrencyModel.TryPurchaseItem</c>을 부르는 프로덕션 코드가
        /// 0건이다(<c>CurrencyRules</c> "배선은 아직 없다"). 이 테스트는 그 사실이 <b>조용히
        /// 바뀌는 것</b>을 막는다: 구매 경로가 배선되는 라운드에 이 단언이 먼저 빨개지고,
        /// 그때 <see cref="ItemCatalog.IsListed"/>를 함께 물려야 한다.</para></summary>
        [Test]
        public void 상점_구매_경로가_배선되면_이_단언이_먼저_빨개진다()
        {
            Assert.AreEqual(0, CurrencyModel.PurchasedItemIds.Count,
                $"{LogPrefix} 준비 조건 — 구매 이력이 비어 있어야 합니다.");

            // 살 수 있는 목록이 생기면 「무엇을 파는가」를 정하는 자리가 어딘가에 생긴다.
            // 그 라운드에 이 테스트를 <b>실제 목록 순회</b>로 바꾸고, 은퇴분이 0건인지 확인할 것.
            Assert.IsTrue(ItemCatalog.IsListed(ItemCatalog.At(0)),
                $"{LogPrefix} 목록 술어가 첫 장비조차 거릅니다 — 상점이 배선될 때 쓸 창구가 고장났습니다.");

            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                if (!EquipmentModel.IsRetiredSlot(slot)) continue;
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    Assert.IsFalse(ItemCatalog.IsListed(entry),
                        $"{LogPrefix} 은퇴한 [{entry.DisplayName}]가 목록 술어를 통과합니다 — " +
                        "상점이 배선되는 순간 「살 수는 있는데 못 입는」 물건이 됩니다.");
                }
            }
        }
    }
}
