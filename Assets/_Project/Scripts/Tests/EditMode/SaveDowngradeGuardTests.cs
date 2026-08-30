using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ **다운그레이드(구버전 앱이 신버전 저장 파일을 만남) 데이터 소실 방어** 회귀 테스트 —
    /// 2026-08-30 횡단 리뷰 m6.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// <see cref="CharacterSaveStore.Load"/>의 `data.version &gt; CurrentVersion` 분기는 **테스트가
    /// 0건**이었고, 동작은 "조용히 return"이었다. 그 결과:
    ///     신버전 파일 발견 → 아무 말 없이 기본값(Lv.1 / 빈 할일)으로 시작 →
    ///     다음 주기 저장(60초)이 그 기본값을 신버전 파일 **위에 덮어씀** → 성장·기록·할일 전손.
    /// 사용자에게는 "앱을 켰더니 캐릭터가 초기화됐다"로 보인다. 앱을 되돌려 실행하는 일은 흔하다
    /// (베타 롤백, 두 대의 기기에서 iCloud로 같은 파일을 공유, 개발 중 브랜치 전환).
    ///
    /// ============================================================================
    /// 지금의 계약 (CharacterSaveStore의 "다운그레이드 방어" 문단이 근거)
    /// ============================================================================
    ///   (1) 원본을 <c>character_save.v{N}.backup.json</c>으로 **복사**한다(지우지도 옮기지도 않는다 —
    ///       절대 불변 원칙 3 정적 감사가 파일 삭제/이동 API를 금지한다).
    ///   (2) 이미 백업이 있으면 덮어쓰지 않는다(첫 백업이 가장 값지다).
    ///   (3) 백업에 실패하면 이번 실행의 <see cref="CharacterSaveStore.Save"/>를 **보류**한다.
    ///   (4) 어느 경우에도 모델은 기본값으로 시작한다(신버전 스키마를 해석할 수 없으므로).
    ///
    /// 네거티브 컨트롤: HandleNewerVersionFile() 호출을 지우고 예전처럼 `return`으로 되돌리면
    /// 아래 <c>신버전_파일은_저장으로_덮이기_전에_백업된다</c>가 즉시 실패한다(백업 파일이 생기지 않음).
    ///
    /// 파일 취급은 이 프로젝트의 관례 그대로 — 실행 중인 실제 앱의 저장 파일 경로를 쓰므로 전후로
    /// 백업/복원하고, 이 테스트가 만든 백업 사본도 반드시 치운다.
    /// </summary>
    public sealed class SaveDowngradeGuardTests
    {
        /// <summary>절대 존재할 수 없는 미래 버전. 앱의 CurrentVersion이 무엇이든 이보다 작다.</summary>
        private const int FutureVersion = 9999;

        private string _backup;
        private bool _hadFile;

        private static string BackupCopyPath =>
            Path.Combine(Path.GetDirectoryName(CharacterSaveStore.FilePath) ?? ".",
                $"character_save.v{FutureVersion}.backup.json");

        private static string FutureJson =>
            "{\n" +
            $"    \"version\": {FutureVersion},\n" +
            "    \"level\": 42,\n" +
            "    \"currentXp\": 777.0,\n" +
            "    \"totalXpEarned\": 99999.0,\n" +
            "    \"characterName\": \"미래동료\",\n" +
            "    \"equippedHead\": true,\n" +
            "    \"equippedEyes\": true,\n" +
            "    \"equippedNeck\": true,\n" +
            "    \"equippedShoulders\": true,\n" +
            "    \"somethingFromTheFuture\": [1, 2, 3]\n" +
            "}";

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

            CleanBackupCopy();
            ResetModels();
        }

        [SetUp]
        public void ResetBeforeEach()
        {
            CleanBackupCopy();
            // 저장 파일을 지운 뒤 Load()를 한 번 돌려 정적 진단 플래그(NewerVersionFileDetected /
            // SaveSuspended)를 확실히 초기화한다 — 이 플래그들은 static이라 테스트 실행 순서에
            // 의존하면 안 된다(파일이 없으면 Load()는 플래그만 리셋하고 즉시 돌아온다).
            if (File.Exists(CharacterSaveStore.FilePath)) File.Delete(CharacterSaveStore.FilePath);
            CharacterSaveStore.Load();
            ResetModels();
        }

        private static void CleanBackupCopy()
        {
            // 테스트가 만든 사본만 지운다(사용자 저장 파일은 절대 건드리지 않는다).
            if (File.Exists(BackupCopyPath)) File.Delete(BackupCopyPath);
        }

        private static void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
        }

        // ============================================================================

        [Test]
        public void 신버전_파일은_저장으로_덮이기_전에_백업된다()
        {
            File.WriteAllText(CharacterSaveStore.FilePath, FutureJson);

            LogAssert.ignoreFailingMessages = true;   // 이 경로는 경고 로그를 내는 것이 정상 동작이다.
            CharacterSaveStore.Load();
            LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(CharacterSaveStore.NewerVersionFileDetected,
                "신버전 저장 파일을 만났는데 그 사실이 기록되지 않았습니다 — 진단조차 불가능합니다.");
            Assert.IsFalse(CharacterSaveStore.LoadedFromFile,
                "해석할 수 없는 신버전 스키마를 읽은 것으로 표시됐습니다.");
            Assert.AreEqual(1, CharacterProgressionModel.Level,
                "신버전 파일에서 값을 억지로 읽어 왔습니다 — 스키마를 모르는 채 읽으면 안 됩니다.");

            Assert.IsTrue(File.Exists(BackupCopyPath),
                $"백업 사본이 만들어지지 않았습니다({BackupCopyPath}) — 이 상태로 주기 저장이 돌면 " +
                "사용자의 성장/기록/할일이 통째로 사라집니다(m6의 데이터 소실 경로 그 자체).");
            Assert.AreEqual(CharacterSaveStore.NewerVersionBackupPath, BackupCopyPath,
                "백업 경로가 진단 속성과 다릅니다.");

            StringAssert.Contains("미래동료", File.ReadAllText(BackupCopyPath),
                "백업 사본의 내용이 원본과 다릅니다 — 껍데기만 만들고 내용을 잃었습니다.");

            // 원본은 그대로 남아 있어야 한다(복사이지 이동이 아니다).
            StringAssert.Contains("미래동료", File.ReadAllText(CharacterSaveStore.FilePath),
                "원본 저장 파일이 사라지거나 바뀌었습니다 — 이 코드는 복사만 해야 합니다.");
        }

        [Test]
        public void 백업이_성공했으면_저장은_정상_진행된다()
        {
            File.WriteAllText(CharacterSaveStore.FilePath, FutureJson);

            LogAssert.ignoreFailingMessages = true;
            CharacterSaveStore.Load();
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(CharacterSaveStore.SaveSuspended,
                "백업에 성공했는데도 저장이 보류됐습니다 — 구버전 앱에서 논 시간이 전부 버려집니다.");
            Assert.IsTrue(CharacterSaveStore.Save(),
                "백업이 있는데도 저장에 실패했습니다.");

            // 덮어써도 백업이 남아 있으므로 데이터는 살아 있다 — 그것이 이 설계의 요점이다.
            StringAssert.Contains("미래동료", File.ReadAllText(BackupCopyPath),
                "저장이 백업 사본까지 덮어썼습니다 — 백업이 의미를 잃었습니다.");
        }

        [Test]
        public void 백업이_이미_있으면_두번째_실행이_덮어쓰지_않는다()
        {
            // 1회차: 신버전 파일 → 백업 생성.
            File.WriteAllText(CharacterSaveStore.FilePath, FutureJson);
            LogAssert.ignoreFailingMessages = true;
            CharacterSaveStore.Load();
            LogAssert.ignoreFailingMessages = false;
            Assert.IsTrue(File.Exists(BackupCopyPath), "1회차에서 백업이 만들어지지 않았습니다.");

            // 2회차: 원본이 그 사이 (구버전 앱의 저장으로) 오염된 상태를 재현한다.
            // 같은 파일 버전을 유지한 채 내용만 바뀐 경우 — 백업이 덮이면 원본 데이터가 최종 소실된다.
            File.WriteAllText(CharacterSaveStore.FilePath, FutureJson.Replace("미래동료", "오염된값"));
            LogAssert.ignoreFailingMessages = true;
            CharacterSaveStore.Load();
            LogAssert.ignoreFailingMessages = false;

            StringAssert.Contains("미래동료", File.ReadAllText(BackupCopyPath),
                "두 번째 실행이 백업을 덮어썼습니다 — 가장 값진 '첫 백업'을 잃었습니다.");
            StringAssert.DoesNotContain("오염된값", File.ReadAllText(BackupCopyPath),
                "백업이 나중 내용으로 갱신됐습니다 — 백업의 목적(최초 상태 보존)에 어긋납니다.");
        }

        [Test]
        public void 정상_버전_파일에서는_다운그레이드_방어가_아무것도_하지_않는다()
        {
            // 네거티브 컨트롤 — 방어가 정상 경로까지 건드리면 그게 새 버그다.
            CharacterProgressionModel.ResetForTesting();
            Assert.IsTrue(CharacterSaveStore.Save(), "정상 저장에 실패했습니다.");

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "정상 파일을 읽지 못했습니다.");
            Assert.IsFalse(CharacterSaveStore.NewerVersionFileDetected,
                "현재 버전 파일인데 신버전으로 오판했습니다.");
            Assert.IsFalse(CharacterSaveStore.SaveSuspended, "정상 경로에서 저장이 보류됐습니다.");
            Assert.IsFalse(File.Exists(BackupCopyPath), "정상 경로에서 불필요한 백업 파일이 생겼습니다.");
        }

        [Test]
        public void 버전0_이하_파일은_예전대로_조용히_무시된다()
        {
            // 이 경로는 "손상/미완성 파일"이지 다운그레이드가 아니다 — 백업 대상이 아니다.
            File.WriteAllText(CharacterSaveStore.FilePath,
                "{\n    \"version\": 0,\n    \"level\": 5\n}");

            CharacterSaveStore.Load();

            Assert.IsFalse(CharacterSaveStore.LoadedFromFile);
            Assert.IsFalse(CharacterSaveStore.NewerVersionFileDetected,
                "version 0(손상)을 신버전으로 오판했습니다.");
            Assert.AreEqual(1, CharacterProgressionModel.Level);
        }
    }
}
