using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 잉크색의 영속화(Core/CharacterAppearanceModel.cs + CharacterSaveStore 스키마 v7) 회귀 테스트 —
    /// 2026-08-31 R5 "잉크색 배포 에셋 오염" 수정의 나머지 절반.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 두 가지 사실
    /// ============================================================================
    ///  ① <b>고른 색이 재시작을 넘어 살아남는다.</b> 수정 전에는 세이브 스키마에 잉크색 필드가
    ///     아예 없어서, 빌드 사용자는 앱을 껐다 켤 때마다 검정으로 되돌아갔다(에셋 변경이 남는 것은
    ///     에디터뿐이라 아무도 눈치채지 못했다 — 에디터에서는 반대로 <b>영구 오염</b>이었다).
    ///  ② <b>구버전 저장 파일이 그대로 읽힌다.</b> v1~v6에는 잉크색 키가 없고, 그 부재는
    ///     "아직 고른 적 없다 = 배포 기본값을 쓴다"는 정확한 사실로 읽혀야 한다(UiLayoutPersistenceTests의
    ///     톱니 위치/캐릭터 크기와 같은 관례). 스키마를 올릴 때마다 사용자의 레벨/기록이 날아가지
    ///     않게 하는 이 프로젝트의 마이그레이션 관례를 v6→v7에서도 확인한다.
    ///
    /// 런타임 적용 쪽(배포 에셋 오염 금지)은 Tests/PlayMode/DeployedConfigAssetImmutabilityTests.cs가 잠근다.
    ///
    /// 파일 취급은 관례 그대로 — 대상은 언제나 <see cref="CharacterSaveStore.FilePath"/> 하나뿐이고
    /// (EditMode 스위트에서는 GlobalEditModeTestIsolation이 임시 폴더로 옮겨 둔다), 전후로 백업/복원한다.
    /// </summary>
    public sealed class InkColorPersistenceTests
    {
        private string _backup;
        private bool _hadFile;

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
        public void ResetBeforeEach()
        {
            if (File.Exists(CharacterSaveStore.FilePath)) File.Delete(CharacterSaveStore.FilePath);
            ResetModels();
        }

        private static void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            CharacterAppearanceModel.ResetForTesting();
        }

        // ============================================================================
        // ① 왕복
        // ============================================================================

        [Test]
        public void 고른_잉크색이_저장하고_다시_불러온_뒤에도_같다()
        {
            Assert.IsFalse(CharacterAppearanceModel.HasInkColor, "초기 상태는 '고른 적 없음'이어야 합니다.");

            CharacterAppearanceModel.SetInkColor(StickmanInkColor.White);
            Assert.IsTrue(CharacterAppearanceModel.IsDirty, "색이 바뀌었는데 저장 대상으로 표시되지 않았습니다.");
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");
            Assert.IsFalse(CharacterAppearanceModel.IsDirty, "저장했는데 여전히 저장 대상으로 남아 있습니다.");

            // 파일에 **이름**으로 적혔는가(열거형 순서가 바뀌어도 안 밀리게 한 결정의 확인).
            string json = File.ReadAllText(CharacterSaveStore.FilePath);
            StringAssert.Contains("\"inkColorName\": \"White\"", json,
                "잉크색이 이름 문자열로 기록되지 않았습니다 — 숫자로 적으면 열거형이 바뀌는 날 전원의 색이 밀립니다.");

            CharacterAppearanceModel.ResetForTesting();
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterAppearanceModel.HasInkColor,
                "고른 잉크색이 복원되지 않았습니다 — 빌드에서 재시작마다 검정으로 되돌아갑니다(이번 수정의 대상).");
            Assert.AreEqual(StickmanInkColor.White, CharacterAppearanceModel.InkColor);
            Assert.IsFalse(CharacterAppearanceModel.IsDirty, "복원 직후는 저장 대상이 아니어야 합니다(복원은 변화가 아니다).");
        }

        [Test]
        public void 같은_색을_다시_골라도_저장_대상이_되지_않는다()
        {
            CharacterAppearanceModel.SetInkColor(StickmanInkColor.White);
            Assert.IsTrue(CharacterSaveStore.Save());
            Assert.IsFalse(CharacterAppearanceModel.IsDirty);

            CharacterAppearanceModel.SetInkColor(StickmanInkColor.White);

            Assert.IsFalse(CharacterAppearanceModel.IsDirty,
                "같은 색을 다시 골랐는데 저장 대상으로 표시됐습니다 — 주기 저장이 매번 디스크를 두드리게 됩니다.");
        }

        // ============================================================================
        // ② 마이그레이션 — v6 이하 파일에는 잉크색 키가 없다
        // ============================================================================

        [Test]
        public void 구버전_v6_저장_파일은_색을_고른_적_없음으로_읽히고_나머지_값은_보존된다()
        {
            const string V6Json =
                "{\n" +
                "    \"version\": 6,\n" +
                "    \"level\": 11,\n" +
                "    \"currentXp\": 30.0,\n" +
                "    \"totalXpEarned\": 2200.0,\n" +
                "    \"characterName\": \"여섯번동료\",\n" +
                "    \"battleWins\": 5,\n" +
                "    \"gearPositionSaved\": true,\n" +
                "    \"gearCenterXPoints\": 120.0,\n" +
                "    \"gearCenterYPoints\": 64.0,\n" +
                "    \"characterScaleSaved\": true,\n" +
                "    \"characterScale\": 1.25,\n" +
                "    \"cornerPanelEnabled\": false,\n" +
                "    \"wornHead\": \"\",\n" +
                "    \"wornEyes\": \"\",\n" +
                "    \"wornNeck\": \"\",\n" +
                "    \"wornShoulders\": \"\",\n" +
                "    \"wornHair\": \"\",\n" +
                "    \"wornFx\": \"\",\n" +
                "    \"wornPet\": \"\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, V6Json);

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "v6 파일을 읽지 못했습니다 — 사용자의 진행도가 날아갑니다.");
            Assert.IsFalse(CharacterAppearanceModel.HasInkColor,
                "v6 파일에는 잉크색이 없으므로 '고른 적 없음'이어야 합니다 — 배포 기본값이 이겨야 합니다.");

            // v6이 담고 있던 값들은 v7 코드에서도 그대로 살아야 한다(마이그레이션이 남의 값을 밟지 않는다).
            Assert.AreEqual(11, CharacterProgressionModel.Level, "v6 파일의 레벨이 복원되지 않았습니다.");
            Assert.AreEqual(5, CharacterStatsModel.BattleWins, "v6 파일의 기록이 복원되지 않았습니다.");
            Assert.IsTrue(UiLayoutModel.HasCharacterScale, "v6 파일의 캐릭터 크기가 복원되지 않았습니다.");
            Assert.AreEqual(1.25f, UiLayoutModel.CharacterScale, 1e-4f);
            Assert.IsFalse(UiLayoutModel.CornerPanelEnabled, "v6 파일의 구석 패널 설정(꺼짐)이 뒤집혔습니다.");
        }

        [Test]
        public void 구버전_파일을_읽은_뒤_저장하면_최신_스키마로_올라가고_색은_여전히_고른_적_없음이다()
        {
            const string V4Json =
                "{\n" +
                "    \"version\": 4,\n" +
                "    \"level\": 3,\n" +
                "    \"currentXp\": 1.0,\n" +
                "    \"totalXpEarned\": 100.0,\n" +
                "    \"characterName\": \"넷동료\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, V4Json);
            CharacterSaveStore.Load();
            Assert.AreEqual(3, CharacterProgressionModel.Level, "사전 조건: v4 파일을 읽지 못했습니다.");

            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");

            string json = File.ReadAllText(CharacterSaveStore.FilePath);

            // ★ 2026-09-01 — 기대 버전을 숫자로 베껴 적지 않는다(예전에는 "version": 7이었고, 스키마가
            //   v8(설정창)으로 올라가자 잉크색과 무관하게 빨개졌다). 이 테스트가 실제로 주장하는 것은
            //   "v4 파일을 읽고 저장하면 <b>그 시점의 최신 버전</b>으로 올라간다"이지 특정 숫자가 아니다.
            Assert.Greater(CharacterSaveStore.CurrentVersion, 4,
                "이 테스트의 전제(픽스처가 구버전이다)가 깨졌습니다 — v4 픽스처를 손봐야 합니다.");
            StringAssert.Contains($"\"version\": {CharacterSaveStore.CurrentVersion}", json,
                "저장 파일이 현재 스키마 버전으로 올라가지 않았습니다.");
            StringAssert.Contains("\"inkColorSaved\": false", json,
                "색을 고른 적 없는데 저장 파일이 '골랐다'고 적었습니다 — 다음 로드에서 배포 기본값이 무시됩니다.");

            CharacterAppearanceModel.ResetForTesting();
            CharacterSaveStore.Load();
            Assert.IsFalse(CharacterAppearanceModel.HasInkColor,
                "왕복 후에도 '고른 적 없음'이 유지돼야 합니다.");
            Assert.AreEqual(3, CharacterProgressionModel.Level, "버전을 올리는 과정에서 레벨이 사라졌습니다.");
        }

        [Test]
        public void 알_수_없는_색_이름은_고른_적_없음으로_떨어진다()
        {
            // 훗날 색이 늘어난 버전에서 만든 파일을 (같은 스키마 버전으로) 읽는 상황의 축소판.
            // 모르는 이름을 0(Black)으로 뭉개면 "파일이 말하지 않은 것"을 화면이 보여주게 된다.
            const string StrangeJson =
                "{\n" +
                "    \"version\": 7,\n" +
                "    \"level\": 2,\n" +
                "    \"characterName\": \"이상한색\",\n" +
                "    \"inkColorSaved\": true,\n" +
                "    \"inkColorName\": \"Rainbow\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, StrangeJson);

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "파일 자체는 읽혀야 합니다(모르는 색 하나로 전부 버리면 안 됩니다).");
            Assert.AreEqual(2, CharacterProgressionModel.Level, "모르는 색 때문에 다른 값까지 버려졌습니다.");
            Assert.IsFalse(CharacterAppearanceModel.HasInkColor,
                "모르는 색 이름은 '고른 적 없음'으로 떨어져야 합니다(배포 기본값이 이긴다).");
        }

        // ============================================================================
        // 런타임 오버라이드 자체의 계약(에셋을 쓰지 않는 EditMode 사본으로 확인)
        // ============================================================================

        [Test]
        public void 런타임_잉크색은_직렬화_필드를_건드리지_않고_리졸버만_바꾼다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                config.inkColor = StickmanInkColor.Black;   // 배포 기본값 역할(이 인스턴스는 에셋이 아니다)
                config.primaryOutlineColor = Color.black;
                config.whiteInkColor = Color.white;

                Assert.IsFalse(config.HasRuntimeInkColor);
                Assert.AreEqual(StickmanInkColor.Black, config.ResolveInkPreset());
                Assert.IsFalse(config.IsWhiteInk());

                config.SetRuntimeInkColor(StickmanInkColor.White);

                Assert.AreEqual(StickmanInkColor.Black, config.inkColor,
                    "런타임 잉크색이 직렬화 필드를 덮었습니다 — 배포 에셋이라면 그대로 출하됩니다(R5 재발).");
                Assert.IsTrue(config.HasRuntimeInkColor);
                Assert.AreEqual(StickmanInkColor.White, config.ResolveInkPreset());
                Assert.IsTrue(config.IsWhiteInk());
                Assert.AreEqual(Color.white, config.ResolveInkColor(), "실효 선 색이 프리셋을 따라오지 않았습니다.");

                config.ClearRuntimeInkColor();

                Assert.IsFalse(config.HasRuntimeInkColor);
                Assert.AreEqual(StickmanInkColor.Black, config.ResolveInkPreset(),
                    "런타임 값을 지웠는데 배포 기본값으로 돌아오지 않았습니다.");
                Assert.AreEqual(Color.black, config.ResolveInkColor());
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
