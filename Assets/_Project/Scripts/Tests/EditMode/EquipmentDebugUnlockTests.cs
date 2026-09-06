using NUnit.Framework;
using StickMate.Core;
using UnityEditor;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 임시 QA 스위치(<see cref="EquipmentDebugUnlock"/>) 회귀 테스트 — 2026-08-31 사용자 요청
    /// "장비창은 일단 전부다 잠금없이 열어줘(임시로)".
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가
    /// ============================================================================
    ///  (1) <b>켜면 진짜로 열린다</b>: 장비 전종이 Lv.1에서 보유·착용 가능해야 한다. 하나라도 안 열리면
    ///      사용자가 "전체 동작 확인"을 못 한다(요청의 목적 자체가 무너진다).
    ///  (2) <b>끄면 원래대로 돌아온다</b>: 요구 레벨 규칙을 <b>지우지 않았다</b>는 증거. 이게 없으면
    ///      "임시"라는 말이 지켜졌는지 아무도 확인할 수 없다.
    ///  (3) <b>문구가 같이 따라온다</b>: 열려 있는데 "Lv.20에 열림"이라 적혀 있으면 그게 거짓말이다
    ///      (원칙 1 계열). 우회를 보유 판정 한 곳에서만 한 이유가 이것이다.
    ///  (4) <b>두 뿌리가 같은 말을 한다</b>: <see cref="ItemCatalogEntry.IsOwned"/>(UI)와
    ///      <see cref="EquipmentModel.IsItemOwned"/>(착용)가 어긋나면 카드는 잠겼는데 눌리거나
    ///      그 반대가 된다.
    ///  (5) ★ <b>스위치가 여는 것은 「요구 레벨」 하나뿐이다</b>(2026-09-06 추가). 은퇴한 카테고리
    ///      (<see cref="EquipmentModel.IsRetiredSlot"/> — 사용자 지시 *"외형에서 머리 스타일은 전체 삭제"*)는
    ///      <b>레벨로 잠긴 것이 아니라 고를 수 있는 자리에서 빠진 것</b>이라, 이 스위치보다 위에 있다.
    ///      여기서 걸쳐지면 QA 빌드에서만 <b>화면 어디에도 없는 머리가 몸에 붙는</b> 상태가 되고,
    ///      그건 정보창에서 벗을 수 없다(탈출구가 없다).
    ///
    /// <para>스위트 전체는 <c>GlobalEditModeTestIsolation</c>이 스위치를 <b>꺼 둔</b> 상태로 돈다.
    /// 이 파일만 켠 상태를 직접 만들어 보고, 끝나면 반드시 되돌린다.</para>
    /// </summary>
    public sealed class EquipmentDebugUnlockTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static StickConfig LoadDefaultConfig()
            => AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);

        /// <summary>
        /// ★ 2026-09-06 — <c>CurrencyModel.ResetForTesting()</c>이 여기 <b>빠져 있었다</b>.
        ///
        /// <para>이 픽스처의 <c>스위치를_끄면_요구_레벨_규칙이_그대로_살아_있다</c>는
        /// <c>Assert.IsFalse(crown.IsOwned(config))</c>로 <b>왕관(<c>equip.head.crown</c>, Lv.20)</b>을
        /// 겨눈다. 그런데 <c>IsOwned</c>는 «레벨 파생 ∪ <b>상점 구매분</b>»이고
        /// (DESIGN_SYSTEMS_STATS §20-2-a), 알파벳 순으로 <b>앞</b>에 도는
        /// <c>CurrencyRulesTests</c>·<c>EquipmentMigrationTests</c>가 <b>바로 그 왕관</b>을 산다.
        /// 그쪽에도 TearDown을 달았지만(같은 라운드), <b>측정하는 쪽이 자기 전제를 남에게 맡기지
        /// 않는다</b> — <c>ItemCatalogTests</c>가 오늘 같은 이유로 같은 조치를 받았다.</para>
        ///
        /// <para>이 한 줄이 없으면 실패 메시지가 <i>"스위치를 껐는데도 Lv.20 아이템이 Lv.1에서 열려
        /// 있습니다"</i>로 뜬다 — 원인은 재화 쪽인데 <b>QA 해금 스위치를 지목하는 거짓 빨강</b>이고,
        /// 오늘 밤 실제로 그 형태로 시간을 썼다.</para>
        /// </summary>
        [SetUp]
        public void Reset()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
        }

        [TearDown]
        public void RestoreSuiteDefault()
        {
            // 스위트 규약(제품 규칙으로 검증)을 반드시 되돌린다 — 여기서 새면 뒤 테스트가 조용히 물러진다.
            EquipmentDebugUnlock.SetTestOverride(false);
            EquipmentModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
        }

        [Test]
        public void 스위치를_켜면_Lv1에서_전_장비가_보유이고_착용된다()
        {
            EquipmentDebugUnlock.SetTestOverride(true);
            StickConfig config = LoadDefaultConfig();
            Assert.AreEqual(1, CharacterProgressionModel.Level, "전제: Lv.1에서 시작.");

            int checkedItems = 0;
            int wornItems = 0;      // 실제로 걸쳐 본 것(양성 대조) — 0이면 아래 "은퇴" 분기가 전부를 삼킨 것이다.
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                for (int i = 0; i < EquipmentModel.ItemCount(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);

                    // ★ 보유 판정은 은퇴와 무관하다 — 은퇴는 "고를 수 있는 자리"에서 뺀 것이지
                    //   데이터를 뺀 것이 아니다(EquipmentModel.IsRetiredSlot 문단). 그래서 이 두 줄은
                    //   머리 6종에도 그대로 적용된다.
                    Assert.IsTrue(entry.IsOwned(config),
                        $"[{entry.DisplayName}]가 Lv.1에서 잠겨 있습니다 — 스위치를 켠 목적이 " +
                        "장비 전종을 눌러 보는 것입니다.");
                    Assert.IsTrue(EquipmentModel.IsItemOwned(slot, i),
                        $"[{entry.DisplayName}] 카드는 열렸는데 착용 판정은 잠겨 있습니다(두 뿌리가 어긋납니다).");

                    // 실제로 걸쳐진다 — "보유"까지만 열고 착용에서 막히면 QA가 성립하지 않는다.
                    EquipmentModel.TryWear(slot, i, config);

                    if (EquipmentModel.IsRetiredSlot(slot))
                    {
                        // ★ 2026-09-06 — 여기가 이 스위치의 <b>천장</b>이다. 해금 스위치가 우회하는 것은
                        //   요구 레벨(ItemCatalogEntry.RequiredLevel) 하나뿐이고, 은퇴는 그 축이 아니다.
                        //   이 단언이 뒤집히면 QA 빌드에서만 화면에 없는 머리가 몸에 붙는다 —
                        //   그 상태는 정보창 어디에서도 벗을 수 없다(EquipmentModel.IsRetiredSlot 문단).
                        Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(slot),
                            $"[{entry.DisplayName}]가 걸쳐졌습니다 — 은퇴한 카테고리" +
                            $"([{EquipmentModel.SlotName(slot)}])는 QA 해금 스위치로도 열리지 않아야 합니다.");
                    }
                    else
                    {
                        Assert.AreEqual(i, EquipmentModel.WornIndex(slot),
                            $"[{entry.DisplayName}]를 걸치지 못했습니다.");
                        wornItems++;
                    }
                    checkedItems++;
                }
            }

            Assert.AreEqual(ItemCatalog.EquipmentCount, checkedItems,
                "확인한 장비 수가 카탈로그가 말하는 장비 수와 다릅니다.");
            // ★ (개선 R2, 2026-09-01 리더 판정) 여기 원래 `AreEqual(42, checkedItems)`가 있었다.
            // 프로덕션 상수(ItemCatalog.EquipmentCount)를 숫자로 베낀 것이라 CLAUDE.md 위반이었고,
            // 바로 윗줄이 이미 그 상수로 같은 것을 검증하고 있어 중복이기도 했다. 무엇보다 장비
            // 종수가 바뀌는 순간 이 줄이 <b>무관한 라운드를 막는다</b> — 사람이 한 번 보게 하는
            // 지뢰선이 아니라 그냥 지뢰였다.
            //
            // 대신 "공허하지 않은가"만 성질로 남긴다: 카탈로그가 비면 윗줄이 0 == 0으로 통과해
            // 이 테스트 전체가 아무것도 안 보게 되는데, 그건 숫자를 베끼지 않고도 막을 수 있다.
            Assert.Greater(checkedItems, 0,
                "장비를 하나도 확인하지 못했습니다 — 카탈로그가 비었거나 슬롯 순회가 깨졌습니다. "
                + "이 상태면 위 EquipmentCount 단언이 0 == 0으로 공허하게 통과합니다.");

            // ★ 양성 대조 — 위 루프의 "은퇴" 분기는 <b>부재 단언</b>(걸쳐지지 않는다)이라, 어느 날
            //   IsRetiredSlot이 전 슬롯에 참이 되면 <b>착용을 한 번도 안 재고</b> 조용히 초록이 된다.
            //   숫자를 적지 않고 "적어도 하나는 실제로 걸쳐졌다"만 못 박는다(CLAUDE.md 부재 단언 규칙).
            Assert.Greater(wornItems, 0,
                "은퇴하지 않은 카테고리에서 실제로 걸쳐 본 장비가 0개입니다 — 스위치가 착용까지 " +
                "여는지를 이 테스트가 더 이상 재지 않습니다(EquipmentModel.IsRetiredSlot 범위를 확인하세요).");
            Assert.AreEqual(ItemCatalog.EquipmentCount, ItemCatalog.UnlockedEquipmentCount(config),
                "보관함 헤더의 보유 수(분자)가 전체 장비 수(분모)와 다릅니다 — 스위치가 켜져 있으면 " +
                "둘이 같아야 합니다. 숫자를 적지 않고 세는 이유는 아이템이 늘 때마다 여기가 뒤처지기 때문입니다.");
        }

        [Test]
        public void 스위치를_켜면_상태_문구도_같이_열린다()
        {
            // 우회를 착용 시점에서만 했다면 여기서 걸린다: 눌리는데 "Lv.20에 열림"이라 적힌 상태.
            EquipmentDebugUnlock.SetTestOverride(true);
            StickConfig config = LoadDefaultConfig();

            ItemCatalogEntry crown = ItemCatalog.Item(EquipmentSlot.Head, 3);
            Assert.AreEqual(20, crown.RequiredLevel, "전제: 왕관은 Lv.20 아이템이다.");
            StringAssert.DoesNotContain("Lv.", crown.ResolveStatusSlot(config),
                "열려 있는 아이템이 아직 '몇 레벨에 열림'이라고 말합니다 — 눌리는 것과 적힌 것이 어긋납니다.");
        }

        [Test]
        public void 스위치를_끄면_요구_레벨_규칙이_그대로_살아_있다()
        {
            // "임시"의 증거. 규칙을 지운 것이 아니라 앞에 스위치를 하나 둔 것뿐이라는 것을 못 박는다.
            EquipmentDebugUnlock.SetTestOverride(false);
            StickConfig config = LoadDefaultConfig();

            ItemCatalogEntry crown = ItemCatalog.Item(EquipmentSlot.Head, 3);

            // ★ 2026-09-06 — <b>전제를 먼저 분리해 낸다</b>. 아래 IsOwned 단언은 «레벨 파생 ∪ 구매분»
            //   합집합이라(DESIGN_SYSTEMS_STATS §20-2-a) 구매 이력 한 줄만으로도 뒤집힌다. 그때
            //   아래 메시지가 나가면 <b>애먼 QA 해금 스위치를 지목</b>하게 되고, 실제로 오늘 밤
            //   그 형태로 시간을 썼다. 이 줄이 있으면 원인이 어느 모듈인지가 실패 문구에 바로 적힌다.
            //   (아이디를 문자열로 베끼지 않는다 — 카탈로그가 말하는 그 아이디를 그대로 쓴다.)
            Assert.IsFalse(CurrencyModel.IsPurchasedItem(crown.Id),
                $"[{crown.Id}]가 상점 구매 이력에 남아 있습니다 — 이건 해금 스위치 문제가 아니라 " +
                "앞선 픽스처가 구매 이력을 흘린 것입니다(SetUp의 CurrencyModel.ResetForTesting과 " +
                "CurrencyRulesTests/EquipmentMigrationTests의 TearDown을 확인하세요).");

            Assert.IsFalse(crown.IsOwned(config), "스위치를 껐는데도 Lv.20 아이템이 Lv.1에서 열려 있습니다.");
            Assert.IsFalse(EquipmentModel.IsItemOwned(EquipmentSlot.Head, 3));
            Assert.AreEqual("Lv.20에 열림", crown.ResolveStatusSlot(config));
            Assert.IsFalse(EquipmentModel.TryWear(EquipmentSlot.Head, 3, config),
                "잠긴 아이템이 걸쳐졌습니다.");

            // 처음부터 보유인 것은 스위치와 무관하게 열려 있어야 한다(네거티브 컨트롤).
            ItemCatalogEntry cap = ItemCatalog.Item(EquipmentSlot.Head, 0);
            Assert.IsTrue(cap.IsOwned(config), "Lv.1 아이템까지 잠갔습니다 — 우회가 아니라 파괴입니다.");
        }

        [Test]
        public void 행동_항목은_스위치와_무관하게_항상_보유다()
        {
            // 행동에는 RequiredLevel이 없다 — 스위치가 그 경로를 건드리지 않았는지 본다.
            StickConfig config = LoadDefaultConfig();
            foreach (bool on in new[] { true, false })
            {
                EquipmentDebugUnlock.SetTestOverride(on);
                for (int i = 0; i < ItemCatalog.Count; i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.At(i);
                    if (entry.Category != ItemCategory.Action) continue;

                    Assert.IsTrue(entry.IsOwned(config), $"[{entry.DisplayName}] (스위치 {on})");
                    Assert.IsNull(entry.ResolveUnlockLevel(config),
                        $"[{entry.DisplayName}]에 해제 레벨이 생겼습니다 (스위치 {on}).");
                }
            }
        }
    }
}
