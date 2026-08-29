using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 보관함 카탈로그(Core/ItemCatalog.cs) 회귀 테스트 — 2026-08-30 정보창 리디자인 라운드.
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가
    /// ============================================================================
    ///  (1) <b>이중 정의 금지</b>: 장비 항목의 이름/슬롯이름/해제레벨이 <see cref="EquipmentModel"/>과
    ///      한 글자라도 달라지면 실패한다. 이 프로젝트가 이미 두 번 겪은 "같은 사실이 두 곳에 적혀
    ///      조용히 어긋나는" 실패 유형의 직접 잠금이다.
    ///  (2) <b>거짓말 금지</b>: 설명 문구에 이 앱에 존재하지 않는 수치("방어력 +2")를 넣으면 실패한다.
    ///  (3) <b>탈출구 명시</b>: 방해가 될 수 있는 행동(로데오 커서)의 설명에는 빠져나오는 방법이
    ///      문장 안에 있어야 한다(불변 원칙 계열 — 디자이너가 코드로 확인한 규칙).
    ///  (4) 행동 항목은 <b>레벨과 무관하게 항상 보유</b>다(이미 단축키/메뉴로 쓸 수 있으므로).
    /// </summary>
    public class ItemCatalogTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static StickConfig LoadDefaultConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        [SetUp]
        public void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
        }

        [Test]
        public void 장비_항목의_이름과_슬롯과_해제레벨은_EquipmentModel과_같다()
        {
            StickConfig config = LoadDefaultConfig();
            int found = 0;

            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                ItemCatalogEntry entry = ItemCatalog.FindBySlot(slot);
                Assert.IsNotNull(entry, $"{EquipmentModel.ItemName(slot)} 슬롯의 카탈로그 항목이 없습니다.");
                Assert.AreEqual(ItemCategory.Equipment, entry.Category);
                Assert.AreEqual(EquipmentModel.ItemName(slot), entry.DisplayName,
                    "카탈로그가 장비 이름을 따로 들고 있습니다 — EquipmentModel 하나에서만 나와야 합니다.");
                Assert.AreEqual(EquipmentModel.SlotName(slot), entry.CategoryLabel,
                    "슬롯 이름이 두 곳에 따로 적혀 있습니다.");
                Assert.AreEqual(EquipmentModel.UnlockLevel(slot, config), entry.ResolveUnlockLevel(config),
                    "해제 레벨이 두 곳에 따로 적혀 있습니다.");
                found++;
            }

            Assert.AreEqual(EquipmentModel.SlotCount, found, "장비 항목 수가 슬롯 수와 다릅니다.");
            Assert.AreEqual(EquipmentModel.SlotCount, ItemCatalog.EquipmentCount);
        }

        [Test]
        public void 행동_항목은_레벨과_무관하게_항상_보유다()
        {
            StickConfig config = LoadDefaultConfig();
            Assert.AreEqual(1, CharacterProgressionModel.Level, "전제: Lv.1에서 시작.");

            int actions = 0;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                if (entry.Category != ItemCategory.Action) continue;
                actions++;

                Assert.IsTrue(entry.IsOwned(config),
                    $"[{entry.DisplayName}]이 Lv.1에서 잠겨 있습니다 — 행동은 이미 단축키/메뉴로 쓸 수 있으므로 " +
                    "잠긴 척하면 그것이 거짓말입니다.");
                Assert.IsNull(entry.ResolveUnlockLevel(config), "행동에는 해제 레벨이 없어야 합니다.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.ActionStatus),
                    $"[{entry.DisplayName}]의 상태 슬롯 문구가 비어 있습니다(목록 오른쪽 칸이 빈칸이 됩니다).");
            }

            Assert.AreEqual(ItemCatalog.ActionCount, actions);
            Assert.GreaterOrEqual(actions, 13,
                "행동 항목이 13개보다 적습니다 — 보관함이 빈 화면이 될 수 있어 디자이너가 13개 이상을 요구했습니다.");
        }

        [Test]
        public void 설명에_이_앱에_없는_전투_수치를_적지_않는다()
        {
            // "방어력 +2" 같은 문구는 이 앱에 존재하지 않는 시스템을 있는 것처럼 말한다.
            var banned = new Regex("(공격력|방어력|명중률\\s*\\+|회피|체력|HP|데미지|스탯\\s*\\+|\\+\\s*\\d+\\s*(포인트|pt))");

            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Description),
                    $"[{entry.DisplayName}]의 설명이 비어 있습니다.");
                Assert.IsFalse(banned.IsMatch(entry.Description),
                    $"[{entry.DisplayName}]의 설명이 이 앱에 없는 전투 수치를 언급합니다: \"{entry.Description}\"");
            }
        }

        [Test]
        public void 방해가_될_수_있는_행동에는_탈출구가_문장에_들어_있다()
        {
            // 근거: Interaction/RodeoCursorWatcher.cs — 하차 후 커서가 다시 멈춰야만 재발동한다
            //       (즉 사용자가 커서를 움직이면 빠져나올 수 있다). 문구가 그 사실을 말해야 한다.
            ItemCatalogEntry rodeo = FindById("action.rodeo_cursor");
            StringAssert.Contains("떨어진다", rodeo.Description,
                "로데오 커서 설명에 빠져나오는 방법이 없습니다 — 탈출구를 암시하지 않는 문구는 쓰지 않습니다.");

            ItemCatalogEntry runaway = FindById("action.runaway");
            StringAssert.Contains("돌아온다", runaway.Description,
                "가출 설명에 돌아오게 하는 방법이 없습니다.");
        }

        [Test]
        public void 항목_아이디는_중복되지_않고_비어_있지_않다()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                string id = ItemCatalog.At(i).Id;
                Assert.IsFalse(string.IsNullOrWhiteSpace(id), "빈 Id가 있습니다 — 훗날 상점 SKU가 될 값입니다.");
                Assert.IsTrue(seen.Add(id), $"Id가 중복됩니다: {id}");
            }
        }

        [Test]
        public void 상태_슬롯_문구는_장비와_행동이_같은_자리를_쓴다()
        {
            StickConfig config = LoadDefaultConfig();

            ItemCatalogEntry hat = ItemCatalog.FindBySlot(EquipmentSlot.Head);
            StringAssert.Contains("Lv.", hat.ResolveStatusSlot(config),
                "Lv.1에서 잠긴 장비의 상태 슬롯은 '몇 레벨에 열리는지'를 보여줘야 합니다.");

            ItemCatalogEntry archery = FindById("action.archery");
            Assert.AreEqual("⌃⌥⌘A", archery.ResolveStatusSlot(config),
                "직접 부를 수 있는 행동의 상태 슬롯에는 단축키가 나와야 합니다.");

            ItemCatalogEntry tidy = FindById("action.desktop_tidy");
            Assert.AreEqual(ItemCatalogEntry.AutoOnlyStatus, tidy.ResolveStatusSlot(config),
                "자율 발동 전용 행동의 상태 슬롯 문구가 다릅니다.");
        }

        [Test]
        public void 해제된_장비를_착용하면_상태_슬롯이_착용_중으로_바뀐다()
        {
            StickConfig config = LoadDefaultConfig();
            int need = EquipmentModel.UnlockLevel(EquipmentSlot.Head, config);

            // 해제 레벨까지 올린다(연속 레벨업 이월 경로를 그대로 쓴다).
            float bulk = 0f;
            for (int lv = 1; lv < need; lv++) bulk += CharacterProgressionModel.XpToNextLevel(lv, config);
            CharacterProgressionModel.AddXp(bulk + 1f, config);

            ItemCatalogEntry hat = ItemCatalog.FindBySlot(EquipmentSlot.Head);
            Assert.AreEqual("보유", hat.ResolveStatusSlot(config));

            Assert.IsTrue(EquipmentModel.TryToggle(EquipmentSlot.Head, config));
            Assert.AreEqual("착용 중", hat.ResolveStatusSlot(config));
            Assert.AreEqual(1, ItemCatalog.UnlockedEquipmentCount(config),
                "해제된 장비 수가 헤더 표기(걸치는 것 n/4)와 어긋납니다.");
        }

        private static ItemCatalogEntry FindById(string id)
        {
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                if (ItemCatalog.At(i).Id == id) return ItemCatalog.At(i);
            }
            Assert.Fail($"카탈로그에 {id} 항목이 없습니다.");
            return null;
        }
    }
}
