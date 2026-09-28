using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 보유 = QA 해금 스위치 ∪ 레벨 파생 (<see cref="ItemCatalogEntry.IsOwned"/> ·
    /// <see cref="EquipmentModel.IsItemOwned"/>). <b>구매는 항이 아니다.</b>
    ///
    /// <para><b>2026-09-28 DLC 폐지 R1</b>(사용자 확정: DLC·재화 폐지, 1회 구매 전환)에서 두 자리의
    /// 셋째 항 <c>CurrencyModel.IsPurchasedItem</c>을 <b>같은 커밋에서</b> 뗐다. 이 파일은 그 전까지
    /// 「구매는 레벨 파생에 <b>더해진다</b>」를 잠그고 있었고, 지금은 그 반대인
    /// 「<b>구매는 아무것도 열지 않는다</b>」를 잠근다. 레벨 항은 그대로다 — 그래서 이 파일의 단언은
    /// 부재 쪽만 있지 않고 <b>같은 아이템에서 존재 쪽과 짝</b>을 이룬다(구매만 하면 안 열리고,
    /// 레벨이 차면 열린다).</para>
    ///
    /// <para><b>왜 두 자리를 함께 재는가</b> — 2026-09-07 P0(실기 신고 "종이비행기 펫샀는데 착용이
    /// 안됨")의 원인이 <b>두 자리의 항 구성이 갈린 것</b>이었다. 카드는 보유로 보이는데
    /// <see cref="EquipmentModel.TryWear"/>가 조용히 false를 돌려줘 <b>클릭이 로그 0줄로 아무 일도
    /// 안 했다</b>. 그때는 <see cref="EquipmentModel.IsItemOwned"/>가 항을 <b>덜</b> 갖고 있었고,
    /// 반대 방향(한쪽만 레벨 항을 잃음)도 같은 침묵을 만든다. 그래서 여기서는
    /// <see cref="ItemCatalogEntry.IsOwned"/>와 <see cref="EquipmentModel.IsItemOwned"/>의 답을
    /// <b>장비 전종에서 직접 대조</b>한다 — 어느 쪽이 어느 방향으로 갈려도 걸린다.</para>
    ///
    /// <para>잠그는 것: (1) 기본 코호트 아이템은 요구 레벨에 닿으면 보유이고 미달이면 아니다,
    /// (2) 구매 이력이 있어도 보유가 되지 않고 <b>레벨이 차면</b> 된다(두 자리 모두),
    /// (3) 구매만 한 아이템은 <see cref="EquipmentModel.TryWear"/>로 걸쳐지지 않고 레벨이 차면 걸쳐진다,
    /// (4) 두 보유 판정 자리가 장비 전종에서 같은 답을 낸다,
    /// (5) QA 해금 스위치는 R1과 무관하게 살아 있고 <b>두 자리를 함께</b> 연다
    ///     (그래서 이 파일은 스위치를 강제로 끈다 — 양성 대조).</para>
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

        /// <summary>두 판정 자리를 대조할 모집단 — <b>양쪽이 같은 카탈로그 행을 가리키는</b> 장비만.
        /// <para><see cref="ItemCatalogEntry.IsOwned"/>의 첫 항 <c>!RequiredLevel.HasValue</c>는
        /// <b>행동 12종</b>용이라 여기 들어오지 않는다(행동은 슬롯이 없고 잠금 개념도 없다).
        /// 장비는 <c>ForEquipment</c>가 <c>int requiredLevel</c>을 받으므로 언제나 값이 있다 —
        /// 그래서 두 자리의 남은 항 구성이 정확히 같아야 하고 이 대조가 성립한다.</para>
        /// <para>★ <b>(슬롯, 자리)로 되찾을 수 없는 항목은 뺀다.</b>
        /// <see cref="EquipmentModel.IsItemOwned"/>는 <b>(슬롯, 자리)</b>로 카탈로그를 되찾는 반면
        /// 카탈로그 쪽은 <b>엔트리 자신</b>에게 묻는다. 팩 항목이 자리 번호를 기본 42종과 나눠 쓰면 그
        /// 되찾기가 다른 행을 가리킬 수 있고, 그건 이 라운드(구매 항 제거)와 <b>무관한</b> 주소 문제라
        /// 여기서 빨개지면 원인을 오독하게 된다. 그래서 되찾기가 <b>같은 객체</b>를 주는 항목만 센다 —
        /// 기본 42종은 전부 여기 들어온다(아래 하한 단언).</para></summary>
        private static List<ItemCatalogEntry> AddressableEquipment(out int skipped)
        {
            var list = new List<ItemCatalogEntry>();
            skipped = 0;
            foreach (ItemCatalogEntry e in ItemCatalog.Entries)
            {
                if (e.Category != ItemCategory.Equipment || !e.Slot.HasValue) continue;
                if (!ReferenceEquals(ItemCatalog.Item(e.Slot.Value, e.ItemIndex), e)) { skipped++; continue; }
                list.Add(e);
            }
            Assert.Greater(list.Count, 0, "슬롯이 붙은 장비가 하나도 없습니다 — 카탈로그가 비었거나 되찾기가 통째로 깨졌습니다.");
            Assert.GreaterOrEqual(list.Count, BaseCohortEquipment().Count,
                $"(슬롯, 자리)로 되찾을 수 있는 장비가 기본 코호트보다 적습니다(되찾기 실패 {skipped}건) — 카탈로그 주소 체계가 깨졌습니다.");
            return list;
        }

        /// <summary>안 가진 것 중 요구 레벨이 <b>가장 높은</b> 것(레벨 1에서 확실히 잠긴 자리).
        /// <para>★ <b>은퇴한 아이템은 고르지 않는다</b>(<see cref="EquipmentModel.IsRetiredItem"/> —
        /// 머리 카테고리 전체와 이펙트 「없음」). 그것들은 <see cref="EquipmentModel.TryWear"/>가
        /// <b>보유와 무관하게</b> 거절하므로, 골라 버리면 착용 쪽 존재 단언이 R1과 아무 상관 없는
        /// 이유로 빨개진다. 지금은 최고 요구 레벨이 머리가 아니지만 그건 <b>우연</b>이고,
        /// 요구 레벨 표가 한 칸 바뀌면 우연이 끝난다.</para></summary>
        private static ItemCatalogEntry HighestLockedItem(List<ItemCatalogEntry> items)
        {
            ItemCatalogEntry locked = null;
            foreach (ItemCatalogEntry e in items)
            {
                if (e.RequiredLevel.Value <= 1) continue;
                if (e.Slot.HasValue && EquipmentModel.IsRetiredItem(e.Slot.Value, e.ItemIndex)) continue;
                if (locked == null || e.RequiredLevel.Value > locked.RequiredLevel.Value) locked = e;
            }
            Assert.IsNotNull(locked, "요구 레벨 > 1 이고 은퇴하지 않은 장비가 없습니다 — 「구매해도 안 열린다」 대조가 공허합니다.");
            return locked;
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

        /// <summary>
        /// ★ <b>DLC 폐지 R1 경계</b> — 구매 이력은 보유 판정에 들어가지 않는다. 그리고 <b>레벨은 그대로
        /// 연다</b>(같은 아이템에서 두 방향을 함께 재므로 이 검사는 부재 단언만으로 조용히 초록이
        /// 되지 않는다).
        ///
        /// <para>이 테스트의 이전 판은 이름이 <c>상점_구매는_레벨_파생_보유에_더해지지_대체하지_않는다</c>였고
        /// 「샀으면 보유다」를 단언했다. R1이 그 항을 뗐으므로 그 단언은 <b>지금 빨강이 되는 것이 옳다</b> —
        /// 이 파일이 그 자리를 반대 방향으로 바꿔 잠근다(두 벌로 늘리지 않는다).</para>
        /// </summary>
        [Test]
        public void 구매_이력은_보유_판정에_관여하지_않고_레벨이_차면_열린다()
        {
            List<ItemCatalogEntry> items = BaseCohortEquipment();
            ItemCatalogEntry locked = HighestLockedItem(items);
            ItemCatalogEntry free = null;
            foreach (ItemCatalogEntry e in items) if (e.RequiredLevel.Value == 1) { free = e; break; }
            Assert.IsNotNull(free, "요구 레벨 1 장비가 없습니다 — 레벨 파생 항의 생존을 잴 대상이 없습니다.");
            Assert.IsTrue(locked.Slot.HasValue, $"{locked.Id}: 장비인데 슬롯이 없습니다.");

            Assert.AreEqual(0, CurrencyModel.PurchasedItemIds.Count, "시작 상태에 구매 이력이 있습니다 — ResetForTesting 이 비우지 않았습니다.");
            SetLevel(1);

            // 존재 단언 — 레벨 항은 살아 있다(구매 이력이 비어 있어도 Lv.1 아이템은 보유).
            Assert.IsTrue(free.IsOwned(_config), $"{free.Id}: Lv.1 아이템이 보유가 아닙니다 — 레벨 항이 죽었습니다.");
            Assert.IsTrue(EquipmentModel.IsItemOwned(free.Slot.Value, free.ItemIndex),
                $"{free.Id}: 카탈로그는 보유라는데 EquipmentModel 은 아니라고 합니다 — 두 자리가 갈렸습니다.");

            // 음성 대조 — 사기 전에 이미 보유면 아래가 뜻을 잃는다.
            Assert.IsFalse(locked.IsOwned(_config), $"{locked.Id}: Lv.1 에서 이미 보유입니다 — 아래 구매 대조가 뜻을 잃습니다.");

            // 구매가 <b>실제로 일어났는지</b>를 먼저 못박는다. 이 줄이 없으면 아래 「열리지 않았다」가
            // 「구매 자체가 실패했다」와 똑같이 생긴다(거짓 초록).
            Assert.IsTrue(CurrencyModel.TryPurchaseItem(locked.Id, 0), $"{locked.Id}: 구매가 거절됐습니다 — 이 검사가 구매 없는 상태를 재게 됩니다.");
            Assert.IsTrue(CurrencyModel.IsPurchasedItem(locked.Id), $"{locked.Id}: 구매가 성공했다는데 이력에 없습니다.");
            Assert.AreEqual(1, CurrencyModel.PurchasedItemIds.Count);

            // ★ R1 경계 — 구매 이력이 있는데도 레벨 미달이면 보유가 아니다. 두 자리 모두.
            Assert.IsFalse(locked.IsOwned(_config),
                $"{locked.Id}: 구매 이력만으로 카탈로그가 보유라고 합니다 — ItemCatalogEntry.IsOwned 에 구매 항이 되살아났습니다(DLC 폐지 R1).");
            Assert.IsFalse(EquipmentModel.IsItemOwned(locked.Slot.Value, locked.ItemIndex),
                $"{locked.Id}: 구매 이력만으로 EquipmentModel 이 보유라고 합니다 — EquipmentModel.IsItemOwned 에 구매 항이 되살아났습니다(DLC 폐지 R1).");

            // 다른 아이템을 산 것이 레벨 파생 보유를 건드리지 않는다.
            Assert.IsTrue(free.IsOwned(_config), $"{free.Id}: 다른 아이템을 사자 레벨 파생 보유가 사라졌습니다.");

            // ★ 존재 단언(짝) — 같은 아이템이 <b>레벨로는</b> 열린다. 구매 이력은 그대로 남아 있다.
            SetLevel(locked.RequiredLevel.Value);
            Assert.IsTrue(locked.IsOwned(_config), $"{locked.Id}: 요구 레벨 {locked.RequiredLevel.Value}에 닿았는데 보유가 아닙니다 — 레벨 항이 죽었습니다.");
            Assert.IsTrue(EquipmentModel.IsItemOwned(locked.Slot.Value, locked.ItemIndex),
                $"{locked.Id}: 요구 레벨에 닿았는데 EquipmentModel 이 보유가 아니라고 합니다.");
            Assert.IsTrue(CurrencyModel.IsPurchasedItem(locked.Id), $"{locked.Id}: 구매 이력이 사라졌습니다 — R1은 이력을 지우지 않습니다(필드 정리는 다른 라운드).");
        }

        /// <summary>
        /// ★ 2026-09-07 P0 회귀 잠금의 <b>R1판</b> — 실기 신고 "종이비행기 펫샀는데 착용이 안됨"이 겨눈 층은
        /// <see cref="ItemCatalogEntry.IsOwned"/>가 아니라 <b>착용을 실제로 거는</b>
        /// <see cref="EquipmentModel.TryWear"/>였다. 그 층은 R1 뒤에도 그대로 재야 한다 — 방향만 뒤집힌다:
        /// <b>구매만 한 것은 걸쳐지지 않고</b>, <b>레벨이 차면 걸쳐진다</b>.
        ///
        /// <para>이 테스트가 없으면 「카드는 잠겼는데 착용은 된다」(한쪽만 항을 더 가진 반대 방향의
        /// 침묵)를 아무도 못 본다.</para>
        /// </summary>
        [Test]
        public void 구매만_한_아이템은_착용되지_않고_레벨이_차면_착용된다()
        {
            ItemCatalogEntry locked = HighestLockedItem(BaseCohortEquipment());
            Assert.IsTrue(locked.Slot.HasValue, $"{locked.Id}: 장비인데 슬롯이 없습니다.");
            // (은퇴 여부는 HighestLockedItem 이 이미 걸렀다 — 그 문단에 이유가 있다.)

            SetLevel(1);

            // 음성 대조 — 사기 전에는 잠금이 실제로 막고 있어야 한다.
            Assert.IsFalse(EquipmentModel.TryWear(locked.Slot.Value, locked.ItemIndex, _config),
                $"{locked.Id}: 사기 전인데 착용이 됩니다 — 레벨 잠금 자체가 깨져 있어 아래 대조가 뜻을 잃습니다.");
            Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(locked.Slot.Value));

            Assert.IsTrue(CurrencyModel.TryPurchaseItem(locked.Id, 0), $"{locked.Id}: 구매가 거절됐습니다.");
            Assert.IsTrue(CurrencyModel.IsPurchasedItem(locked.Id), $"{locked.Id}: 구매가 성공했다는데 이력에 없습니다.");

            // ★ R1 경계 — 샀어도 걸쳐지지 않는다(그리고 카드도 보유라고 말하지 않는다 ⇒ 두 층이 같은 말을 한다).
            Assert.IsFalse(locked.IsOwned(_config), $"{locked.Id}: 구매만으로 카드가 보유라고 합니다(DLC 폐지 R1).");
            Assert.IsFalse(EquipmentModel.TryWear(locked.Slot.Value, locked.ItemIndex, _config),
                $"{locked.Id}: 구매만 했는데 착용이 됩니다 — EquipmentModel.IsItemOwned 에 구매 항이 되살아났습니다(DLC 폐지 R1).");
            Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(locked.Slot.Value),
                $"{locked.Id}: TryWear 는 거절했는데 착용 슬롯이 움직였습니다.");

            // ★ 존재 단언(짝) — 레벨이 차면 같은 호출이 실제로 걸친다. 이 줄이 없으면 위 전부가
            //   「착용 경로가 통째로 죽었다」와 구별되지 않는다.
            SetLevel(locked.RequiredLevel.Value);
            Assert.IsTrue(locked.IsOwned(_config), $"{locked.Id}: 요구 레벨에 닿았는데 카드가 보유가 아닙니다.");
            Assert.IsTrue(EquipmentModel.TryWear(locked.Slot.Value, locked.ItemIndex, _config),
                $"{locked.Id}: 요구 레벨에 닿았는데 EquipmentModel.TryWear 가 거절합니다 — 착용 경로가 막혔습니다.");
            Assert.AreEqual(locked.ItemIndex, EquipmentModel.WornIndex(locked.Slot.Value),
                $"{locked.Id}: TryWear 는 참을 돌려줬는데 실제 착용 슬롯이 그 아이템이 아닙니다.");
        }

        /// <summary>
        /// ★ <b>모듈 이음매 하나</b> — 같은 사실(보유)을 두 곳에서 계산하는 것이 2026-09-07 P0의 원인이었다.
        /// 장비 전종에서 <see cref="ItemCatalogEntry.IsOwned"/>와 <see cref="EquipmentModel.IsItemOwned"/>가
        /// <b>같은 답</b>을 내는지 세 상태에서 대조한다: ① Lv.1 + 전량 구매 이력, ② 최고 요구 레벨,
        /// ③ QA 해금 스위치 ON.
        /// <para>어느 쪽이 항을 하나 잃거나 더 가지면 방향에 관계없이 이 검사가 그 아이디를 지목한다.</para>
        /// </summary>
        [Test]
        public void 두_보유_판정_자리는_장비_전종에서_같은_답을_낸다()
        {
            List<ItemCatalogEntry> items = AddressableEquipment(out int skipped);
            if (skipped > 0)
            {
                Debug.Log($"[보유 이음매] (슬롯, 자리) 되찾기가 다른 행을 준 항목 {skipped}건을 대조에서 뺐습니다 " +
                          "— 이 라운드(구매 항 제거)와 무관한 주소 문제입니다.");
            }
            int maxRequired = 1;
            foreach (ItemCatalogEntry e in items)
            {
                if (e.RequiredLevel.HasValue) maxRequired = Mathf.Max(maxRequired, e.RequiredLevel.Value);
            }

            // ① Lv.1 + 전량 구매 이력 — 구매 항이 어느 한쪽에만 되살아나면 여기서 갈린다.
            SetLevel(1);
            int purchased = 0;
            foreach (ItemCatalogEntry e in items)
            {
                if (CurrencyModel.TryPurchaseItem(e.Id, 0)) purchased++;
            }
            Assert.Greater(purchased, 0, "한 건도 구매되지 않았습니다 — 이 상태가 구매 항을 재지 못합니다.");
            Assert.AreEqual(purchased, CurrencyModel.PurchasedItemIds.Count, "구매 성공 수와 이력 줄 수가 다릅니다.");

            int owned = 0, notOwned = 0;
            foreach (ItemCatalogEntry e in items)
            {
                bool viaCatalog = e.IsOwned(_config);
                bool viaModel = EquipmentModel.IsItemOwned(e.Slot.Value, e.ItemIndex);
                Assert.AreEqual(viaCatalog, viaModel,
                    $"{e.Id}(Lv.1 · 구매 이력 있음): 카탈로그 {viaCatalog} vs EquipmentModel {viaModel} — 두 보유 판정 자리의 항 구성이 갈렸습니다(2026-09-07 P0의 형태).");
                if (viaCatalog) owned++; else notOwned++;
            }
            Assert.Greater(owned, 0, "Lv.1 에서 보유가 하나도 없습니다 — 「같다」가 전부 거짓끼리 같은 것이라 공허합니다.");
            Assert.Greater(notOwned, 0, "Lv.1 에서 미보유가 하나도 없습니다 — 「같다」가 전부 참끼리 같은 것이라 공허합니다.");

            // ② 최고 요구 레벨 — 레벨 항이 한쪽에서만 죽으면 여기서 갈린다.
            SetLevel(maxRequired);
            foreach (ItemCatalogEntry e in items)
            {
                Assert.AreEqual(e.IsOwned(_config), EquipmentModel.IsItemOwned(e.Slot.Value, e.ItemIndex),
                    $"{e.Id}(Lv.{maxRequired}): 두 보유 판정 자리가 갈렸습니다 — 레벨 항이 한쪽에서만 살아 있습니다.");
            }

            // ③ QA 해금 스위치 — R1과 독립된 첫째 항이 두 자리에 <b>함께</b> 물려 있는가.
            SetLevel(1);
            EquipmentDebugUnlock.SetTestOverride(true);
            foreach (ItemCatalogEntry e in items)
            {
                Assert.IsTrue(e.IsOwned(_config), $"{e.Id}: QA 해금 ON 인데 카탈로그가 미보유입니다.");
                Assert.IsTrue(EquipmentModel.IsItemOwned(e.Slot.Value, e.ItemIndex),
                    $"{e.Id}: QA 해금 ON 인데 EquipmentModel 이 미보유입니다 — 스위치가 한 자리에만 물려 있습니다.");
            }
        }

        /// <summary>양성 대조 — QA 해금 스위치가 켜지면 위 검사들의 「거짓」쪽이 전부 가려진다. 그래서 SetUp 이 스위치를 강제로 끈다.
        /// <para>★ 이 스위치는 <b>DLC 폐지 R1과 무관</b>하다(합집합 첫 항이고, 사용자가 "다시 잠그라고 할 때까지
        /// 모든 아이템을 열어 달라"고 지시한 그 경로다 — 환경변수 <c>STICKMATE_UNLOCK_ALL</c>).
        /// 구매 항을 뗀 뒤에도 <b>그대로 살아 있어야</b> 하고, 착용까지 열어야 한다.</para></summary>
        [Test]
        public void 컨트롤_해금_스위치가_켜지면_미달_아이템도_보유로_보인다()
        {
            List<ItemCatalogEntry> items = BaseCohortEquipment();
            ItemCatalogEntry locked = null;
            foreach (ItemCatalogEntry e in items) if (e.RequiredLevel.Value > 1) { locked = e; break; }
            Assert.IsNotNull(locked);
            Assert.IsTrue(locked.Slot.HasValue, $"{locked.Id}: 장비인데 슬롯이 없습니다.");
            SetLevel(1);
            Assert.IsFalse(locked.IsOwned(_config), "스위치 OFF 인데 미달 아이템이 보유입니다 — 강제 OFF 가 안 먹었습니다.");
            Assert.IsFalse(EquipmentModel.IsItemOwned(locked.Slot.Value, locked.ItemIndex),
                "스위치 OFF 인데 EquipmentModel 이 미달 아이템을 보유로 봅니다.");

            EquipmentDebugUnlock.SetTestOverride(true);
            Assert.IsTrue(locked.IsOwned(_config), "스위치 ON 인데 미달 아이템이 보유가 아닙니다 — 스위치가 IsOwned 에 안 물려 있습니다.");
            Assert.IsTrue(EquipmentModel.IsItemOwned(locked.Slot.Value, locked.ItemIndex),
                "스위치 ON 인데 EquipmentModel.IsItemOwned 가 미달 아이템을 미보유로 봅니다 — 스위치가 착용 경로에 안 물려 있습니다(R1이 이 항을 건드리면 안 된다).");
            if (!EquipmentModel.IsRetiredItem(locked.Slot.Value, locked.ItemIndex))
            {
                Assert.IsTrue(EquipmentModel.TryWear(locked.Slot.Value, locked.ItemIndex, _config),
                    $"{locked.Id}: QA 해금 ON 인데 착용이 거절됐습니다 — 스위치가 TryWear 아래까지 닿지 않습니다.");
            }
        }
    }
}
