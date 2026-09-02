using System.IO;
using NUnit.Framework;
using UnityEditor;
using StickMate.Core;
using StickMate.Dialogue;
using UnityEngine;

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
            // v7/v8 필드의 하위 호환도 여기서 검증하므로 그 모델들도 함께 초기화한다 —
            // 정적 상태가 앞선 테스트에서 새어 들어오면 "파일이 말한 것"과 "직전 상태"를 구분할 수 없다.
            CharacterAppearanceModel.ResetForTesting();
            AppSettingsModel.ResetForTesting();
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
            // ★ 2026-09-01 — 예전에는 이 단언이 기대 버전을 숫자로 베껴 적었고("version": 7),
            //   스키마가 v8(설정창)으로 올라가자 마이그레이션과 무관하게 빨개졌다.
            //   그 빨간색은 정보가 없다 — 고치는 사람은 "숫자만 바꾸면 되는 잡음"으로 학습하고,
            //   다음에 같은 자리가 진짜 데이터 손실로 빨개졌을 때도 같은 손놀림으로 넘길 위험이 생긴다.
            //   그래서 이제 상수를 참조한다. "버전을 올리면서 하위 호환을 잊는" 사고는 숫자 비교가
            //   아니라 아래 <c>v7_파일을_읽어도_설정창_값이_꺼지지_않는다</c> 같은
            //   버전별 하위 호환 테스트가 잡는다(그쪽이 진짜 잠금장치다).
            StringAssert.Contains($"\"version\": {CharacterSaveStore.CurrentVersion}", json,
                "저장 파일이 현재 스키마 버전으로 기록되지 않았습니다.");

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
        /// ★ v6 신설 필드의 하위 호환 + <b>삭제된 기능의 설정 키가 남아 있어도 안전한가</b>.
        ///
        /// <para>2026-09-01 좌하단 구석 호버 패널이 사용자 요청으로 통째로 삭제됐다. 그 패널의 설정
        /// <c>cornerPanelEnabled</c>는 <b>이미 배포된 v6+ 저장 파일에 실제로 들어 있다</b>. 기능을
        /// 지울 때 가장 흔한 사고가 "그 키를 읽던 코드까지 지워서 옛 파일이 안 열리는 것"이라,
        /// 이 프로젝트는 스키마 버전을 올리는 대신 <b>값만 왕복시키는 쪽</b>을 택했다
        /// (<see cref="UiLayoutModel.CornerPanelEnabled"/> 문서). 이 테스트가 그 선택을 잠근다.</para>
        ///
        /// <para>함께 잠그는 원래 계약: <c>characterScaleSaved</c>는 없으면 false로 채워지고 그 false는
        /// "아직 크기를 고른 적 없다"는 정확한 사실이라 v3 톱니 위치 때와 같은 방식으로 성립한다.</para>
        /// </summary>
        [Test]
        public void v5_파일은_구석_패널_키가_없어도_그대로_열린다()
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
            Assert.AreEqual(7, CharacterProgressionModel.Level, "v5 파일의 레벨이 사라졌습니다.");
            Assert.IsFalse(UiLayoutModel.HasCharacterScale,
                "v5 파일에 없는 크기가 '사용자가 고른 값'으로 복원됐습니다.");
        }

        /// <summary>
        /// ★ <b>삭제된 기능의 설정이 남아 있는 실제 사용자 파일</b>이 경고 없이 열린다 —
        /// 2026-09-01 구석 호버 패널 삭제 라운드의 하위 호환 잠금.
        ///
        /// <para>이 파일은 그 패널을 <b>꺼 둔</b> 사용자의 파일이다(<c>cornerPanelEnabled: false</c>).
        /// 기능이 사라졌으므로 그 값이 무엇이든 화면은 똑같아야 하고, 무엇보다 <b>같은 파일에 실려 있는
        /// 다른 값들</b>(레벨·이름·사용자가 고른 캐릭터 크기)이 그 키 하나 때문에 유실되면 안 된다.</para>
        ///
        /// <para>스키마 버전은 이 라운드에서 <b>올리지 않았다</b> — 저장 필드를 그대로 두고 읽는 쪽만
        /// 죽였기 때문이다. 그래서 마이그레이션도 새로 생기지 않는다.</para>
        /// </summary>
        [Test]
        public void 삭제된_구석_패널_설정이_적힌_파일도_다른_값을_잃지_않는다()
        {
            string json =
                "{\n" +
                "    \"version\": 6,\n" +
                "    \"level\": 12,\n" +
                "    \"characterName\": \"구버전동료\",\n" +
                "    \"characterScaleSaved\": true,\n" +
                "    \"characterScale\": 1.25,\n" +
                "    \"cornerPanelEnabled\": false,\n" +
                "    \"wornHead\": \"\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, json);
            UiLayoutModel.ResetForTesting();

            Assert.DoesNotThrow(() => CharacterSaveStore.Load(),
                "삭제된 기능의 설정 키가 들어 있는 저장 파일에서 로드가 터졌습니다.");

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile,
                "구석 패널 설정이 적힌 파일을 통째로 버렸습니다 — 그 사용자의 레벨/이름/크기가 전부 날아갑니다.");
            Assert.AreEqual(12, CharacterProgressionModel.Level, "같은 파일의 레벨이 사라졌습니다.");
            Assert.IsTrue(UiLayoutModel.HasCharacterScale, "같은 파일의 캐릭터 크기 선택이 사라졌습니다.");
            Assert.AreEqual(1.25f, UiLayoutModel.CharacterScale, 0.0001f,
                "같은 파일의 캐릭터 크기 값이 바뀌었습니다.");

            // 저장을 한 번 더 해도 파일이 깨지지 않는다(죽은 필드가 왕복만 하는지 확인).
            Assert.IsTrue(CharacterSaveStore.Save(), "삭제 기능 키가 남은 상태에서 재저장에 실패했습니다.");
            Assert.DoesNotThrow(() => CharacterSaveStore.Load(),
                "재저장한 파일을 다시 읽는 데 실패했습니다.");
            Assert.AreEqual(1.25f, UiLayoutModel.CharacterScale, 0.0001f,
                "왕복 후 캐릭터 크기가 달라졌습니다.");
        }

        /// <summary>
        /// ★ v8 신설 필드(설정창)의 하위 호환 — v6의 <c>cornerPanelEnabled</c>가 겪었던 것과
        /// <b>완전히 같은 종류의 함정</b>이 두 개 더 생겼다. <c>autoHideOnFullscreen</c>/<c>gearIconVisible</c>는 기본이
        /// true인데, v7 이하 파일에는 그 키가 아예 없어 JsonUtility가 false로 채운다 — 그대로 읽으면
        /// 업데이트만 했는데 옛 사용자의 <b>전체화면 자동 숨김이 꺼지고</b>(절대 불변 원칙 2 위반) 톱니
        /// 아이콘이 사라진다. 나머지 말풍선 4종은 "고른 적 있는가 + 값" 두 벌이라 false가 곧 정확한
        /// 사실이므로 여기서 함께 확인만 한다.
        ///
        /// <para>네거티브 컨트롤: CharacterSaveStore.Load()의 <c>hasAppSettings ? ... : true</c> 삼항을
        /// <c>data.autoHideOnFullscreen</c>로 되돌리면 이 테스트가 즉시 실패한다.</para>
        ///
        /// <para>이 테스트가 <b>v8 라운드의 진짜 잠금장치</b>다. 예전에는 "저장 파일이 v7로 기록되는가"라는
        /// 숫자 단언이 그 역할을 한다고 적혀 있었지만, 그 단언은 버전 숫자가 바뀐 사실만 알려줄 뿐
        /// 하위 호환이 지켜졌는지는 한 글자도 검증하지 못했다(2026-09-01 근본 원인).</para>
        /// </summary>
        [Test]
        public void v7_파일을_읽어도_설정창_값이_꺼지지_않는다()
        {
            string json =
                "{\n" +
                "    \"version\": 7,\n" +
                "    \"level\": 9,\n" +
                "    \"characterName\": \"일곱동료\",\n" +
                "    \"inkColorSaved\": true,\n" +
                "    \"inkColorName\": \"White\",\n" +
                "    \"wornHead\": \"\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, json);
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "v7 파일을 통째로 버렸습니다.");
            Assert.AreEqual(9, CharacterProgressionModel.Level, "v7 파일의 레벨이 사라졌습니다.");
            Assert.IsTrue(CharacterAppearanceModel.HasInkColor, "v7 파일의 잉크색 선택이 사라졌습니다.");

            Assert.IsTrue(AppSettingsModel.AutoHideOnFullscreen,
                "v7 파일(그 키가 애초에 없는 파일)을 읽었더니 전체화면 자동 숨김이 꺼졌습니다 — " +
                "JsonUtility가 채운 false를 사용자의 선택으로 오해한 것입니다(CharacterSaveStore." +
                "FirstVersionWithAppSettings 분기 확인). 원칙 2(비침해) 직결입니다.");
            Assert.IsTrue(AppSettingsModel.GearIconVisible,
                "v7 파일을 읽었더니 톱니 아이콘이 숨겨졌습니다 — 설정 진입점이 통째로 사라집니다.");

            Assert.IsFalse(AppSettingsModel.HasDialogueFontSize,
                "v7 파일에 없는 말풍선 글자 크기가 '사용자가 고른 값'으로 복원됐습니다.");
            Assert.IsFalse(AppSettingsModel.HasDialogueBubbleEnabled,
                "v7 파일에 없는 말풍선 on/off가 '사용자가 고른 값'으로 복원됐습니다.");
        }

        /// <summary>
        /// ★ <b>v10 신설 필드(표시 모니터 선택)의 하위 호환</b> — 2026-09-02.
        /// CLAUDE.md: "저장 스키마 <c>CurrentVersion</c>을 올리는 라운드는 <c>vN-1</c> 구버전 파일을
        /// 읽었을 때 신규 필드가 안전한 기본값으로 채워지는지 검증하는 하위 호환 테스트 1건을 반드시 동반한다."
        ///
        /// <para>v10은 <c>preferredMonitorSaved</c>(bool) + <c>preferredMonitorKey</c>(string) 두 벌을
        /// 더한다. 형태가 v6 <c>characterScaleSaved</c> / v7 <c>inkColorSaved</c>와 같아서 하위 호환이
        /// <b>저절로</b> 성립한다 — v9 파일에 그 키가 없으면 JsonUtility가 false/null로 채우고,
        /// 그 false는 <b>"아직 고른 적 없다 = 기본값인 가장 왼쪽 모니터를 쓴다"</b>는 정확한 사실이다.
        /// (기본이 <c>true</c>인 <c>autoHideOnFullscreen</c>류와 달리 뜻이 뒤집히지 않으므로
        ///  버전 분기가 필요 없다 — 그 사실을 여기서 <b>실행으로</b> 확인한다.)</para>
        ///
        /// <para>여기서 잠그는 것도 둘이다: (1) 신규 필드가 안전한 기본값이 되는가,
        /// (2) 같은 파일의 <b>다른 v9 값들이 그대로 살아남는가</b>. (2)가 없으면 (1)은
        /// "파일을 아예 안 읽어서" 통과한다.</para>
        /// </summary>
        [Test]
        public void v9_파일을_읽어도_표시_모니터가_고른_적_없음으로_떨어진다()
        {
            string json =
                "{\n" +
                "    \"version\": 9,\n" +
                "    \"level\": 13,\n" +
                "    \"characterName\": \"아홉동료\",\n" +
                "    \"autoHideOnFullscreen\": true,\n" +
                "    \"gearIconVisible\": true,\n" +
                "    \"dialogueFontSizeSaved\": true,\n" +
                "    \"dialogueFontSize\": 20,\n" +
                "    \"wornHead\": \"\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, json);
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "v9 파일을 통째로 버렸습니다.");

            Assert.IsFalse(AppSettingsModel.HasPreferredOverlayMonitor,
                "v9 파일에 없는 표시 모니터 선택이 '사용자가 고른 값'으로 복원됐습니다 — " +
                "그러면 존재하지 않는 키로 매칭을 시도해 매번 폴백 로그만 남습니다.");
            Assert.IsEmpty(AppSettingsModel.PreferredOverlayMonitorKey,
                "고른 적이 없는데 키가 비어 있지 않습니다.");

            // ★ "안전한 기본값으로 채워진다"를 <b>플래그가 아니라 실제 판정 결과</b>로 확인한다.
            //   플래그만 보면 "false이긴 한데 그래서 어느 화면에 뜨는가"가 검증되지 않는다.
            //   사용자 확정 기본값은 <b>가장 왼쪽</b>이다(2026-09-02 "기본은 왼쪽").
            var monitors = new System.Collections.Generic.List<StickMate.Platform.OsMonitorFact>
            {
                // 일부러 목록 순서를 오른쪽 먼저로 둔다 — OS 열거는 정렬을 보장하지 않으므로,
                // 정책이 "0번"이 아니라 x 최솟값을 고르는지가 여기서 갈린다(음성 대조).
                new StickMate.Platform.OsMonitorFact(new Rect(1920f, 0f, 2560f, 1440f), new Rect(1920f, 0f, 2560f, 1440f), true, "R"),
                new StickMate.Platform.OsMonitorFact(new Rect(0f, 0f, 1920f, 1080f), new Rect(0f, 0f, 1920f, 1080f), false, "L"),
            };
            var choice = StickMate.Platform.OverlayMonitorChoicePolicy.Resolve(
                monitors, AppSettingsModel.PreferredOverlayMonitorKey);
            Assert.AreEqual(StickMate.Platform.OverlayMonitorChoiceSource.StartSlotDefault, choice.Source,
                "고른 적 없는 저장 파일인데 기본값 경로로 가지 않았습니다.");
            Assert.AreEqual(1, choice.Index,
                "기본값이 <b>축의 시작(가장 왼쪽)</b>이 아닙니다. 목록 1번이 x=0으로 가장 왼쪽인데 " +
                "0번(x=1920, 주 모니터 플래그까지 붙어 있다)을 골랐다면 정책이 여전히 " +
                "'인덱스 0' 또는 '주 모니터'를 기본값으로 보고 있는 것입니다.");

            // ★ 위 단언이 "파일을 아예 안 읽어서" 통과한 것이 아님을 보인다(음성 대조).
            Assert.AreEqual(13, CharacterProgressionModel.Level, "v9 파일의 레벨이 사라졌습니다.");
            Assert.IsTrue(AppSettingsModel.HasDialogueFontSize,
                "v9 파일의 말풍선 글자 크기 선택이 함께 지워졌습니다 — 필요 이상으로 버렸습니다.");
            Assert.AreEqual(20, AppSettingsModel.DialogueFontSize);
            Assert.IsTrue(AppSettingsModel.AutoHideOnFullscreen);
        }

        /// <summary>
        /// ★ v10 <b>왕복</b> — 고른 값이 저장되고 다시 읽힌다. 위 테스트가 "없을 때"를 잠그므로
        /// 이것이 "있을 때"를 잠근다(둘 중 하나만 있으면 반대쪽이 조용히 죽는다).
        /// </summary>
        [Test]
        public void v10_표시_모니터_선택이_저장되고_다시_읽힌다()
        {
            string key = StickMate.Platform.OverlayMonitorChoicePolicy.SlotSaveName(
                StickMate.Platform.OverlayMonitorSlot.End);
            AppSettingsModel.SetPreferredOverlayMonitor(key);
            CharacterSaveStore.Save();

            AppSettingsModel.ResetForTesting();
            Assert.IsFalse(AppSettingsModel.HasPreferredOverlayMonitor, "초기화가 되지 않았습니다(테스트 전제).");

            CharacterSaveStore.Load();
            Assert.IsTrue(AppSettingsModel.HasPreferredOverlayMonitor,
                "저장했는데 다시 읽히지 않았습니다 — 사용자가 고른 모니터가 재시작마다 사라집니다.");
            Assert.AreEqual(key, AppSettingsModel.PreferredOverlayMonitorKey);
        }

        /// <summary>
        /// ★ v9 신설 필드(<c>대사 표시 시간</c> 3단)의 하위 호환 — 2026-09-02(docs/UX_FLOW.md 42절).
        ///
        /// <para>v8 파일에는 <c>dialogueVisibleSeconds</c>(초, 1.5~6.0)가 들어 있다. v9는 그 필드를
        /// <b>읽지 않는다</b> — 마이그레이션 매핑이 <b>"저장된 값 전부 → 기본(100%)"</b>이기 때문이다.
        /// 근거: 그 슬라이더는 2.5초를 넘는 구간에서 화면을 한 톨도 바꾸지 못했으므로(35줄 전수 실측
        /// 0/35) 옛 값의 대부분은 "사용자가 고른 뜻"이 아니라 <b>아무 일도 일어나지 않던 숫자</b>다.
        /// 억지로 배율로 환산하면 <b>겪어본 적 없는 화면</b>을 새로 만들어 주게 된다.</para>
        ///
        /// <para>여기서 잠그는 것은 둘이다: (1) 신규 필드가 <b>안전한 기본값</b>으로 채워지는가,
        /// (2) 같은 파일의 <b>다른 v8 값들은 그대로 살아남는가</b>(= 파일을 통째로 버리거나 전부
        /// 기본값으로 밀어 버리지 않았는가). (2)가 없으면 (1)은 "아무것도 안 읽어서" 통과한다.</para>
        ///
        /// <para>네거티브 컨트롤: <c>CharacterSaveStore.SaveData</c>에 <c>dialogueVisibleSeconds</c>를
        /// 되살려 <c>AppSettingsModel</c>에 흘려 넣으면 <c>HasDialogueVisibleLength</c> 단언이 즉시
        /// 실패한다(고른 적 없음이 고른 값으로 뒤바뀐다).</para>
        /// </summary>
        [Test]
        public void v8_파일을_읽어도_대사_표시_시간이_기본으로_떨어진다()
        {
            string json =
                "{\n" +
                "    \"version\": 8,\n" +
                "    \"level\": 11,\n" +
                "    \"characterName\": \"여덟동료\",\n" +
                "    \"autoHideOnFullscreen\": true,\n" +
                "    \"gearIconVisible\": true,\n" +
                "    \"dialogueFontSizeSaved\": true,\n" +
                "    \"dialogueFontSize\": 22,\n" +
                "    \"dialogueVisibleSecondsSaved\": true,\n" +
                "    \"dialogueVisibleSeconds\": 6.0,\n" +
                "    \"wornHead\": \"\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, json);
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "v8 파일을 통째로 버렸습니다.");

            Assert.IsFalse(AppSettingsModel.HasDialogueVisibleLength,
                "v8의 초 값(6.0)이 '사용자가 고른 대사 표시 시간'으로 되살아났습니다 — 그 값은 화면을 " +
                "한 톨도 바꾸지 못하던 죽은 숫자입니다(UX_FLOW.md 42-1).");
            Assert.AreEqual(DialogueVisibleLength.Default, AppSettingsModel.DialogueVisibleLength,
                "v8 파일을 읽었더니 대사 표시 시간이 `기본`이 아닙니다 — 사용자가 겪어본 적 없는 " +
                "화면을 새로 만들어 준 것입니다.");
            Assert.AreEqual(DialogueBudget.MinVisibleScale,
                AppSettingsModel.ResolveDialogueVisibleScale(), 1e-4f);

            // ★ 위 단언이 "파일을 아예 안 읽어서" 통과한 것이 아님을 보인다.
            Assert.AreEqual(11, CharacterProgressionModel.Level, "v8 파일의 레벨이 사라졌습니다.");
            Assert.IsTrue(AppSettingsModel.HasDialogueFontSize,
                "v8 파일의 말풍선 글자 크기 선택이 함께 지워졌습니다 — 마이그레이션이 필요 이상으로 " +
                "많이 버렸습니다.");
            Assert.AreEqual(22, AppSettingsModel.DialogueFontSize);
            Assert.IsTrue(AppSettingsModel.AutoHideOnFullscreen);
            Assert.IsTrue(AppSettingsModel.GearIconVisible);
        }

        /// <summary>v9 왕복 — 사용자가 고른 대사 표시 시간이 재시작을 넘어 살아남는다.
        /// (하위 호환만 보면 "언제나 기본으로 떨어뜨리기"라는 오답이 통과한다.)</summary>
        [Test]
        public void v9_왕복은_사용자가_고른_대사_표시_시간을_보존한다()
        {
            AppSettingsModel.SetDialogueVisibleLength(DialogueVisibleLength.VeryLong);
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");

            AppSettingsModel.ResetForTesting();
            Assert.IsFalse(AppSettingsModel.HasDialogueVisibleLength, "사전 조건 — 초기화됐어야 한다.");

            CharacterSaveStore.Load();
            Assert.IsTrue(AppSettingsModel.HasDialogueVisibleLength,
                "왕복 후 사용자의 선택이 사라졌습니다 — 껐다 켜면 초기화됩니다.");
            Assert.AreEqual(DialogueVisibleLength.VeryLong, AppSettingsModel.DialogueVisibleLength);
        }

        /// <summary>v6 왕복 — 사용자가 고른 캐릭터 크기가 재시작을 넘어 살아남는다.
        /// (같은 v6에 들어왔던 구석 패널 on/off는 2026-09-01 기능 삭제로 단언 대상에서 뺐다.)</summary>
        [Test]
        public void v6_왕복은_사용자가_고른_크기를_보존한다()
        {
            UiLayoutModel.ResetForTesting();
            UiLayoutModel.SetCharacterScale(1.35f);
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");

            UiLayoutModel.ResetForTesting();
            Assert.IsFalse(UiLayoutModel.HasCharacterScale, "리셋 전제가 바뀌었습니다.");

            CharacterSaveStore.Load();
            Assert.IsTrue(UiLayoutModel.HasCharacterScale);
            Assert.AreEqual(1.35f, UiLayoutModel.CharacterScale, 0.0001f);
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
