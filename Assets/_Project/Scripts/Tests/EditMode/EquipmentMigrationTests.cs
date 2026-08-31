using System.IO;
using NUnit.Framework;
using UnityEditor;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 착용 상태 v1~v4 → v5 마이그레이션 회귀 테스트 — 2026-08-30 32종 확장 라운드.
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가
    /// ============================================================================
    /// 이 라운드에서 착용 상태의 <b>모양이 바뀌었다</b>: bool 4개(카테고리당 착용 여부) →
    /// 카테고리 8개 × 아이템 아이디. 지금까지의 버전 올림(v2/v3/v4)은 "새 필드가 없으면 0/null이고
    /// 그 값이 곧 정확한 사실"이라서 하위 호환이 <b>저절로</b> 성립했지만, 이번에는 아니다 —
    /// 옛 필드와 새 필드가 <b>다른 자리</b>에 있어서 명시적으로 옮겨 주지 않으면 며칠 키운 사용자가
    /// 로그인하자마자 <b>맨몸</b>이 된다(그리고 다음 자동 저장이 그 맨몸을 파일에 굳혀 버린다).
    ///
    /// 그래서 네 버전을 <b>각각</b> 검증한다(v1/v2/v3/v4). 같은 코드가 도는 것처럼 보이지만,
    /// 실제로 파일에 들어 있는 필드 집합이 버전마다 다르고 JsonUtility가 채워 주는 기본값도 다르다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    ///  · CharacterSaveStore.RestoreEquipment()의 v1~v4 분기에서 <b>신규 4카테고리를 미착용으로
    ///    지정하는 네 줄</b>을 지우면 <c>옛_파일을_읽으면_신규_카테고리는_직전_상태가_아니라_미착용이다</c>가
    ///    실패한다(직전 세션의 차림이 남는다).
    ///  · 마이그레이션 분기 자체를 지우고 v5 필드만 읽게 하면 v1~v4 네 테스트가 전부 실패한다.
    ///
    /// 파일 취급은 이 프로젝트 관례 그대로 — 실행 중인 실제 앱의 저장 파일 경로를 쓰므로 전후로
    /// 백업/복원한다.
    /// </summary>
    public sealed class EquipmentMigrationTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private string _backup;
        private bool _hadFile;

        private static StickConfig LoadDefaultConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        [OneTimeSetUp]
        public void BackupRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            _hadFile = File.Exists(path);
            _backup = _hadFile ? File.ReadAllText(path) : null;
        }

        [OneTimeTearDown]
        public void RestoreRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            if (_hadFile) File.WriteAllText(path, _backup);
            else if (File.Exists(path)) File.Delete(path);
            ResetModels();
        }

        [SetUp]
        public void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            TodoListModel.ResetForTesting();
        }

        // ============================================================================
        // 옛 파일들 — 각 버전이 실제로 담고 있던 필드만 적는다(없던 필드를 적으면 검증이 무의미해진다)
        // ============================================================================

        private const string V1Json =
            "{\n" +
            "    \"version\": 1,\n" +
            "    \"level\": 9,\n" +
            "    \"currentXp\": 42.0,\n" +
            "    \"totalXpEarned\": 1500.0,\n" +
            "    \"characterName\": \"최초동료\",\n" +
            "    \"equippedHead\": true,\n" +
            "    \"equippedEyes\": false,\n" +
            "    \"equippedNeck\": true,\n" +
            "    \"equippedShoulders\": false\n" +
            "}";

        private const string V2Json =
            "{\n" +
            "    \"version\": 2,\n" +
            "    \"level\": 7,\n" +
            "    \"currentXp\": 10.0,\n" +
            "    \"totalXpEarned\": 1200.0,\n" +
            "    \"characterName\": \"기록동료\",\n" +
            "    \"equippedHead\": false,\n" +
            "    \"equippedEyes\": true,\n" +
            "    \"equippedNeck\": false,\n" +
            "    \"equippedShoulders\": true,\n" +
            "    \"battleWins\": 3,\n" +
            // ★ \"rivalWins\"는 **일부러 남겨 둔다** — 라이벌 기능 전체 삭제(2026-08-30)로 코드에는
            //   더 이상 이 필드가 없지만, 실제 사용자의 옛 저장 파일에는 이 키가 들어 있다.
            //   JsonUtility가 모르는 키를 조용히 무시한다는 사실이 깨지면 로드가 통째로 실패하므로,
            //   이 픽스처가 그 회귀를 잡는 유일한 안전망이다. "정리"하지 말 것.
            "    \"rivalWins\": 1,\n" +
            "    \"archeryShots\": 5,\n" +
            "    \"archeryBullseyes\": 2,\n" +
            "    \"companionSeconds\": 120.0,\n" +
            "    \"ragdollFalls\": 4,\n" +
            "    \"firstRunUnixSeconds\": 1788038056\n" +
            "}";

        private const string V3Json =
            "{\n" +
            "    \"version\": 3,\n" +
            "    \"level\": 12,\n" +
            "    \"currentXp\": 5.0,\n" +
            "    \"totalXpEarned\": 4000.0,\n" +
            "    \"characterName\": \"톱니동료\",\n" +
            "    \"equippedHead\": true,\n" +
            "    \"equippedEyes\": true,\n" +
            "    \"equippedNeck\": true,\n" +
            "    \"equippedShoulders\": true,\n" +
            "    \"battleWins\": 1,\n" +
            "    \"rivalWins\": 0,\n" +
            "    \"archeryShots\": 0,\n" +
            "    \"archeryBullseyes\": 0,\n" +
            "    \"companionSeconds\": 60.0,\n" +
            "    \"ragdollFalls\": 0,\n" +
            "    \"firstRunUnixSeconds\": 1788038056,\n" +
            "    \"gearPositionSaved\": true,\n" +
            "    \"gearCenterXPoints\": 300.0,\n" +
            "    \"gearCenterYPoints\": 120.0\n" +
            "}";

        private const string V4Json =
            "{\n" +
            "    \"version\": 4,\n" +
            "    \"level\": 6,\n" +
            "    \"currentXp\": 3.0,\n" +
            "    \"totalXpEarned\": 2000.0,\n" +
            "    \"characterName\": \"할일동료\",\n" +
            "    \"equippedHead\": false,\n" +
            "    \"equippedEyes\": false,\n" +
            "    \"equippedNeck\": false,\n" +
            "    \"equippedShoulders\": true,\n" +
            "    \"battleWins\": 0,\n" +
            "    \"rivalWins\": 0,\n" +
            "    \"archeryShots\": 0,\n" +
            "    \"archeryBullseyes\": 0,\n" +
            "    \"companionSeconds\": 0.0,\n" +
            "    \"ragdollFalls\": 0,\n" +
            "    \"firstRunUnixSeconds\": 1788038056,\n" +
            "    \"gearPositionSaved\": false,\n" +
            "    \"gearCenterXPoints\": 0.0,\n" +
            "    \"gearCenterYPoints\": 0.0,\n" +
            "    \"todos\": [ { \"id\": 1, \"text\": \"보고서 초안\", \"completed\": false } ],\n" +
            "    \"todoArchive\": []\n" +
            "}";

        /// <summary>옛 파일의 bool은 <b>그 카테고리의 기본 아이템(0번)</b>이 된다 — 옛 시절에는 카테고리
        /// 안에 아이템이 하나뿐이었고, 그 하나가 지금의 0번이다.</summary>
        private static void AssertMigrated(EquipmentSlot slot, bool wasEquipped)
        {
            if (wasEquipped)
            {
                Assert.AreEqual(0, EquipmentModel.WornIndex(slot),
                    $"[{EquipmentModel.SlotName(slot)}]를 걸치고 있던 사용자가 업데이트 후 벗겨졌습니다 " +
                    "(또는 고르지 않은 다른 아이템이 걸쳐졌습니다).");
            }
            else
            {
                Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(slot),
                    $"[{EquipmentModel.SlotName(slot)}]를 벗고 있던 사용자에게 없던 아이템이 생겼습니다.");
            }
        }

        /// <summary>옛 파일에 존재한 적이 없는 3카테고리는 전부 미착용이어야 한다 — 업데이트만 했는데
        /// 머리가 바뀌면 "내가 안 했는데 얼굴이 달라졌다"가 된다.</summary>
        private static void AssertNewCategoriesUnworn()
        {
            EquipmentSlot[] added =
            {
                EquipmentSlot.Hair, EquipmentSlot.Fx, EquipmentSlot.Pet,
            };
            for (int i = 0; i < added.Length; i++)
            {
                Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(added[i]),
                    $"옛 파일에는 [{EquipmentModel.SlotName(added[i])}]가 존재한 적이 없는데 뭔가 걸쳐졌습니다.");
            }
        }

        [Test]
        public void v1_파일의_착용_상태가_기본_아이템으로_승격된다()
        {
            File.WriteAllText(CharacterSaveStore.FilePath, V1Json);
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "가장 오래된 파일을 읽지 못했습니다.");
            Assert.AreEqual(9, CharacterProgressionModel.Level, "레벨이 함께 날아갔습니다.");
            AssertMigrated(EquipmentSlot.Head, true);
            AssertMigrated(EquipmentSlot.Eyes, false);
            AssertMigrated(EquipmentSlot.Neck, true);
            AssertMigrated(EquipmentSlot.Shoulders, false);
            AssertNewCategoriesUnworn();

            Assert.AreEqual("equip.head.cap", EquipmentModel.WornItemId(EquipmentSlot.Head),
                "승격 대상이 그 카테고리의 기본 아이템이 아닙니다.");
        }

        [Test]
        public void v2_파일의_착용_상태와_기록이_함께_살아남는다()
        {
            File.WriteAllText(CharacterSaveStore.FilePath, V2Json);
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile);
            AssertMigrated(EquipmentSlot.Head, false);
            AssertMigrated(EquipmentSlot.Eyes, true);
            AssertMigrated(EquipmentSlot.Neck, false);
            AssertMigrated(EquipmentSlot.Shoulders, true);
            AssertNewCategoriesUnworn();

            Assert.AreEqual(3, CharacterStatsModel.BattleWins,
                "착용 마이그레이션이 다른 필드 복원을 밀어냈습니다.");
        }

        [Test]
        public void v3_파일은_네_카테고리_모두_착용으로_올라온다()
        {
            File.WriteAllText(CharacterSaveStore.FilePath, V3Json);
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile);
            for (int i = 0; i <= (int)EquipmentSlot.Shoulders; i++) AssertMigrated((EquipmentSlot)i, true);
            AssertNewCategoriesUnworn();

            Assert.IsTrue(UiLayoutModel.HasGearCenter, "v3의 톱니 위치가 함께 날아갔습니다.");
        }

        [Test]
        public void v4_파일은_할일과_착용_상태가_모두_살아남는다()
        {
            File.WriteAllText(CharacterSaveStore.FilePath, V4Json);
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile);
            AssertMigrated(EquipmentSlot.Head, false);
            AssertMigrated(EquipmentSlot.Eyes, false);
            AssertMigrated(EquipmentSlot.Neck, false);
            AssertMigrated(EquipmentSlot.Shoulders, true);
            AssertNewCategoriesUnworn();

            Assert.AreEqual(1, TodoListModel.ActiveItems.Count, "v4의 할일이 사라졌습니다 — 사용자의 진짜 일정입니다.");
            Assert.AreEqual("보고서 초안", TodoListModel.ActiveItems[0].Text);
        }

        [Test]
        public void 옛_파일을_읽으면_신규_카테고리는_직전_상태가_아니라_미착용이다()
        {
            // ★ 네거티브 컨트롤의 본체. 로드 전에 신규 카테고리를 일부러 채워 둔다 —
            //   마이그레이션이 "옛 파일에 없는 것은 건드리지 않는다"로 구현돼 있으면 이 값이 그대로 남고,
            //   파일이 말한 적 없는 차림을 화면이 보여주게 된다.
            StickConfig config = LoadDefaultConfig();
            CharacterProgressionModel.AddXp(100000f, config);   // 전부 보유할 만큼 올린다.
            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Hair, 2, config), "준비 조건 실패 — 머리를 걸치지 못했습니다.");
            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Pet, 3, config), "준비 조건 실패 — 펫을 걸치지 못했습니다.");

            File.WriteAllText(CharacterSaveStore.FilePath, V4Json);
            CharacterSaveStore.Load();

            AssertNewCategoriesUnworn();
        }

        [Test]
        public void v5_왕복은_카테고리마다_고른_아이템을_아이디로_보존한다()
        {
            StickConfig config = LoadDefaultConfig();
            CharacterProgressionModel.AddXp(100000f, config);

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Head, 2, config));      // 중절모
            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Hair, 3, config));      // 민머리
            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Fx, 1, config));        // 발자국
            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Eyes, EquipmentModel.NotWorn, config));
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");

            // 저장 파일이 <b>아이디</b>를 적었는지 직접 본다 — 인덱스를 적으면 훗날 표 중간에 아이템을
            // 하나 끼워 넣는 날 전원의 착용물이 한 칸씩 밀린다.
            string json = File.ReadAllText(CharacterSaveStore.FilePath);
            StringAssert.Contains("equip.head.fedora", json, "착용 아이템이 아이디로 저장되지 않았습니다.");
            // ★ 2026-08-31 v6(구석 호버 패널: 캐릭터 크기 + 패널 on/off)로 올라갔다.
            StringAssert.Contains("\"version\": 6", json, "저장 파일이 v6로 기록되지 않았습니다.");

            EquipmentModel.ResetForTesting();
            CharacterSaveStore.Load();

            Assert.AreEqual(2, EquipmentModel.WornIndex(EquipmentSlot.Head));
            Assert.AreEqual(3, EquipmentModel.WornIndex(EquipmentSlot.Hair));
            Assert.AreEqual(1, EquipmentModel.WornIndex(EquipmentSlot.Fx));
            Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(EquipmentSlot.Eyes),
                "벗어 둔 카테고리가 착용으로 되살아났습니다.");
            Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(EquipmentSlot.Pet));
        }

        /// <summary>
        /// ★ v6 신설 필드(캐릭터 크기 / 구석 패널 on/off)의 하위 호환 — <b>"없으면 기본값"이 저절로
        /// 성립하지 않는 유일한 종류의 필드</b>가 여기 하나 있다.
        ///
        /// <c>characterScaleSaved</c>는 없으면 false로 채워지고 그 false는 "아직 크기를 고른 적 없다"는
        /// 정확한 사실이라 v3 톱니 위치 때와 같은 방식으로 성립한다. 그런데 <c>cornerPanelEnabled</c>는
        /// <b>기본이 true</b>라 없으면 false로 채워져 뜻이 정확히 뒤집힌다 — 업데이트만으로 옛 사용자의
        /// 구석 패널이 조용히 꺼진다. 그래서 Load가 버전으로 분기하는지 여기서 잠근다.
        /// </summary>
        [Test]
        public void v5_파일을_읽어도_구석_패널이_꺼지지_않는다()
        {
            string json =
                "{\n" +
                "    \"version\": 5,\n" +
                "    \"level\": 7,\n" +
                "    \"characterName\": \"옛파일\",\n" +
                "    \"wornHead\": \"\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, json);
            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "v5 파일을 통째로 버렸습니다.");
            Assert.IsTrue(UiLayoutModel.CornerPanelEnabled,
                "v5 파일(그 키가 애초에 없는 파일)을 읽었더니 구석 호버 패널이 꺼졌습니다 — " +
                "JsonUtility가 채운 false를 사용자의 선택으로 오해한 것입니다(CharacterSaveStore." +
                "FirstVersionWithCornerPanel 분기 확인).");
            Assert.IsFalse(UiLayoutModel.HasCharacterScale,
                "v5 파일에 없는 크기가 '사용자가 고른 값'으로 복원됐습니다.");
        }

        [Test]
        public void v6_왕복은_사용자가_고른_크기와_패널_설정을_보존한다()
        {
            UiLayoutModel.ResetForTesting();
            UiLayoutModel.SetCharacterScale(1.35f);
            UiLayoutModel.SetCornerPanelEnabled(false);
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");

            UiLayoutModel.ResetForTesting();
            Assert.IsTrue(UiLayoutModel.CornerPanelEnabled, "리셋 기본값 전제가 바뀌었습니다.");

            CharacterSaveStore.Load();
            Assert.IsTrue(UiLayoutModel.HasCharacterScale);
            Assert.AreEqual(1.35f, UiLayoutModel.CharacterScale, 0.0001f);
            Assert.IsFalse(UiLayoutModel.CornerPanelEnabled,
                "사용자가 끈 구석 패널이 재시작 후 다시 켜졌습니다.");
        }

        [Test]
        public void 모르는_아이템_아이디는_미착용으로_떨어진다()
        {
            // 훗날 표에서 빠진 아이템 / 손상된 파일. 없는 아이템을 억지로 다른 것으로 바꿔치기하면
            // 사용자가 고른 적 없는 차림이 되므로, 그 카테고리만 조용히 비운다.
            string json =
                "{\n" +
                "    \"version\": 5,\n" +
                "    \"level\": 30,\n" +
                "    \"currentXp\": 0.0,\n" +
                "    \"totalXpEarned\": 0.0,\n" +
                "    \"characterName\": \"미래아이템\",\n" +
                "    \"wornHead\": \"equip.head.somethingThatDoesNotExist\",\n" +
                "    \"wornEyes\": \"equip.eyes.goggles\",\n" +
                "    \"wornNeck\": \"\",\n" +
                "    \"wornShoulders\": \"\",\n" +
                "    \"wornHair\": \"\",\n" +
                "    \"wornFx\": \"\",\n" +
                "    \"wornPet\": \"\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, json);
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "모르는 아이디 하나 때문에 파일 전체가 버려졌습니다.");
            Assert.AreEqual(30, CharacterProgressionModel.Level, "레벨까지 함께 날아갔습니다.");
            Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(EquipmentSlot.Head),
                "모르는 아이디가 엉뚱한 아이템으로 대체됐습니다.");
            Assert.AreEqual(2, EquipmentModel.WornIndex(EquipmentSlot.Eyes),
                "같은 파일의 정상 아이디까지 함께 버려졌습니다.");
        }
    }
}
