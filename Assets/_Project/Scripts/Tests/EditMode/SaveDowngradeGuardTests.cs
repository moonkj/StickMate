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
    ///   (3) 백업 성공/실패와 <b>무관하게</b> 이번 실행의 <see cref="CharacterSaveStore.Save"/>를 **보류**한다.
    ///   (4) 어느 경우에도 모델은 기본값으로 시작한다(신버전 스키마를 해석할 수 없으므로).
    ///
    /// <para>★ <b>(3)은 2026-09-01에 뒤집힌 계약이다</b>(페르소나 재현 J1 실측 A). 원래는 "백업에
    /// 성공했으면 저장은 정상 진행"이었고, 이 파일에도 그것을 잠그는 테스트가 있었다
    /// (<c>백업이_성공했으면_저장은_정상_진행된다</c>). 그 판단은 "구버전 앱이 나중에 켜진다"는
    /// <b>직렬</b> 시나리오만 가정한 것이다. 실제 워크플로는 세이브 파일 하나를 여러 인스턴스가
    /// 공유하고(.claude/skills/run-stickmate/SKILL.md), 신버전 인스턴스가 <b>아직 돌고 있는 채로</b>
    /// 구버전이 파일을 되돌리는 일이 실제로 일어난다(실측: 11:05:58 v8 → 11:06:03 v7, 설정창 키 10개
    /// 소실). 백업은 가장 처음 한 번만 찍히므로 그 뒤의 변경은 어느 사본에도 없다 — 즉 백업이 그
    /// 손실을 막아 주지 못한다. 그래서 아래 테스트도 <c>백업에_성공해도_신버전_원본은_덮어쓰지_않는다</c>로
    /// 뒤집었다. "이미 켜져 있는 인스턴스 밑에서 파일이 바뀌는" 나머지 절반은
    /// <see cref="SaveConcurrentInstanceTests"/>가 맡는다(저장 직전 버전 재확인).</para>
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

            // ★ 2026-09-01 — 정적 진단 플래그를 다음 픽스처에 물려주지 않는다.
            // 새 계약에서는 이 픽스처의 테스트들이 <b>SaveSuspended=true를 남긴 채</b> 끝난다
            // (신버전 파일을 만나면 이번 실행의 저장을 보류하는 것이 그 계약이다). 그대로 두면
            // 뒤에 도는 지속성 테스트들의 Save()가 전부 false를 받아, 이 라운드와 아무 상관 없는
            // 곳에서 빨개진다(그 픽스처들의 SetUp은 모델만 초기화하고 Load()를 부르지 않는다).
            // Load()는 첫 줄에서 플래그 3종을 초기화하므로 한 번 부르는 것으로 충분하다.
            // 모델 초기화는 그 <b>뒤</b>에 한다 — Load()가 복원한 값이 다음 픽스처로 새어 나가지 않게.
            CharacterSaveStore.Load();
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

            // ★ 2026-09-02 — 직전 세대(stickmate_character.prev.json)도 함께. 이 픽스처의
            //   "version 0" 테스트는 <b>못 읽는 본체</b>를 만들어 놓고 기본값으로 떨어지는지를 보는데,
            //   세대 파일이 남아 있으면 새 복구 경로가 그것을 집어 테스트가 다른 것을 재게 된다.
            if (File.Exists(CharacterSaveStore.PreviousGenerationPath))
                File.Delete(CharacterSaveStore.PreviousGenerationPath);
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
        public void 백업에_성공해도_신버전_원본은_덮어쓰지_않는다()
        {
            // 위 클래스 문서의 ★ 문단이 이 뒤집기의 근거다(실측 있음).
            File.WriteAllText(CharacterSaveStore.FilePath, FutureJson);

            LogAssert.ignoreFailingMessages = true;
            CharacterSaveStore.Load();
            LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(File.Exists(BackupCopyPath), "사전 조건: 백업이 만들어져야 합니다.");
            Assert.IsTrue(CharacterSaveStore.SaveSuspended,
                "신버전 파일을 만났는데 저장이 보류되지 않았습니다 — 다음 주기 저장(60초)이 그 파일을 " +
                "구버전 스키마로 되돌립니다. 그때 사라지는 값은 백업이 찍힌 뒤에 생긴 것이라 어느 " +
                "사본에도 남지 않습니다.");
            Assert.IsFalse(CharacterSaveStore.Save(),
                "보류 상태인데 저장이 성공을 보고했습니다 — 호출부가 '저장됐다'로 속습니다.");

            StringAssert.Contains("미래동료", File.ReadAllText(CharacterSaveStore.FilePath),
                "구버전 앱이 신버전 저장 파일을 덮어썼습니다 — 재현이 실측한 그 데이터 소실입니다.");
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
