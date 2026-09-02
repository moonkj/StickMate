using System.IO;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>세이브 파일을 여러 인스턴스가 동시에 쓰는 상황</b>의 회귀 잠금 —
    /// 2026-09-01 페르소나(재현) 실측 J1/J2.
    ///
    /// ============================================================================
    /// 왜 이것이 가상 시나리오가 아닌가
    /// ============================================================================
    /// 이 저장 파일은 <b>설계상 전 인스턴스 공유</b>다(.claude/skills/run-stickmate/SKILL.md).
    /// 사용자는 어제 빌드를 종일 켜 두고, 팀은 그 옆에서 새 빌드를 내고 테스트한다. 즉
    /// "구버전 인스턴스가 상주하는 동안 파일이 신버전으로 바뀐다"는 이 팀의 평상시 워크플로다.
    ///
    /// ============================================================================
    /// 무엇이 문제였나 (실측 2건)
    /// ============================================================================
    /// <b>J1-A 콜드 스타트</b>: 파일이 v8인데 v7 앱을 켜면 백업은 만들지만 <b>파일 자체는 v7로
    /// 되돌아갔다</b>(11:05:58 v8 → 11:06:03 v7, 설정창 키 10개 소실). 백업은 딱 한 번만 찍히므로,
    /// 그 뒤 신버전 인스턴스가 만든 변경은 어느 사본에도 남지 않는다 — 백업이 손실을 막아 주지 못한다.
    ///
    /// <b>J1-B 상주 인스턴스</b>(이쪽이 더 나쁘다): 이미 v7을 읽고 <b>돌고 있는</b> 인스턴스 밑에서
    /// 파일만 v8로 바뀌면 <b>15초 만에 v7로 재클로버됐다. 새 백업 0건, 경고 로그 0줄.</b>
    /// 다운그레이드 방어(m6)가 <c>Load()</c> 안에만 있었고 <c>Load()</c>는 기동 시 1회뿐이며,
    /// <c>Save()</c>는 대상 파일의 현재 버전을 <b>다시 읽지 않았기</b> 때문이다.
    ///
    /// <b>J2 임시 파일 공유</b>: 원자적 쓰기의 임시 파일 이름이 <c>stickmate_character.json.writing</c>
    /// 하나로 고정이라, 두 인스턴스의 저장이 겹치면 서로의 임시 파일을 밟았다(늦은 쪽 IOException,
    /// 또는 남의 내용/빈 파일이 본체로 승격 → 다음 Load()가 조용히 "새 캐릭터"로 시작).
    ///
    /// ============================================================================
    /// 지금의 계약
    /// ============================================================================
    ///   (1) <see cref="CharacterSaveStore.Save"/>는 <c>File.Replace</c> 직전에 디스크 파일의
    ///       <c>version</c>만 다시 읽는다. 그것이 자기 버전보다 높으면 <b>쓰지 않고 물러선다</b> —
    ///       false를 돌려주고, 사본을 한 번 남기고, 이번 실행의 저장을 보류하고, 경고를 남긴다.
    ///   (2) 물러선 저장은 모델을 "저장됨"으로 표시하지 않는다(<c>IsDirty</c>가 그대로 남는다).
    ///   (3) 읽을 수 없는 파일(손상/빈 파일)은 <b>막지 않는다</b> — 손상 파일 하나가 저장 기능을
    ///       영구히 잠그는 쪽이 더 나쁘고, 원래 계약도 "다음 저장이 정상 내용으로 덮어쓴다"였다.
    ///   (4) 임시 파일 이름에는 인스턴스 꼬리표(프로세스 아이디)가 들어간다 —
    ///       다른 인스턴스의 임시 파일을 자르거나 승격시킬 수 없다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// <see cref="NegativeControl_같은_버전_파일이면_평소대로_덮어쓴다"/>가 "가드가 모든 저장을
    /// 막아서 통과한 것"이 아님을 증명한다. J2 쪽은 옛 코드였다면 공유 임시 파일이
    /// <c>File.Replace</c>에 소모돼 사라지므로 <see cref="다른_인스턴스의_임시파일을_밟지_않는다"/>가
    /// 곧바로 실패한다(그 파일이 없어진다).
    ///
    /// 파일 취급은 이 폴더의 관례 그대로 — 전역 격리(GlobalEditModeTestIsolation)가 저장 경로를
    /// 임시 폴더로 옮겨 둔 상태를 전제로 하고, 그 사실을 사전 조건으로 <b>단언</b>한다
    /// (개발자/사용자의 진짜 저장 파일은 이 테스트가 열 수조차 없게 한다).
    /// </summary>
    public sealed class SaveConcurrentInstanceTests
    {
        /// <summary>절대 존재할 수 없는 미래 버전. 앱의 CurrentVersion이 무엇이든 이보다 작다.</summary>
        private const int FutureVersion = 9999;

        /// <summary>"다른(신버전) 인스턴스가 방금 써 놓은 파일". 우리가 해석할 수 없는 필드도 들어 있다.</summary>
        private static string FutureJson =>
            "{\n" +
            $"    \"version\": {FutureVersion},\n" +
            "    \"level\": 42,\n" +
            "    \"currentXp\": 777.0,\n" +
            "    \"characterName\": \"미래동료\",\n" +
            "    \"somethingFromTheFuture\": [1, 2, 3]\n" +
            "}";

        /// <summary>옛 코드가 쓰던 <b>전 인스턴스 공유</b> 임시 경로(J2의 문제 그 자체).</summary>
        private static string LegacySharedTempPath =>
            Path.Combine(SaveDirectory, "stickmate_character.json.writing");

        private static string SaveDirectory =>
            Path.GetDirectoryName(CharacterSaveStore.FilePath) ?? ".";

        private static string BackupCopyPath =>
            Path.Combine(SaveDirectory, $"character_save.v{FutureVersion}.backup.json");

        [OneTimeSetUp]
        public void RequireTemporaryDirectory()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 임시 폴더로 리디렉션되어 있지 않습니다(GlobalEditModeTestIsolation 확인). " +
                "이 테스트는 파일을 실제로 쓰고 지우므로, 그 상태에서는 사용자의 진짜 저장 파일을 " +
                "건드릴 수 있어 실행하지 않습니다.");
        }

        [OneTimeTearDown]
        public void CleanUpAfterFixture()
        {
            CleanFiles();
            CharacterSaveStore.Load();   // 정적 진단 플래그를 다음 픽스처에 물려주지 않는다.
            ResetModels();               // 모델 초기화는 그 뒤에(Load가 복원한 값이 새어 나가지 않게).
        }

        [SetUp]
        public void ResetBeforeEach()
        {
            CleanFiles();
            // 파일이 없는 상태에서 Load()를 한 번 돌리면 진단 플래그(NewerVersionFileDetected /
            // SaveSuspended)가 확실히 초기화된다 — 이 플래그들은 static이라 테스트 실행 순서에
            // 의존하면 안 된다(SaveDowngradeGuardTests와 같은 관례).
            CharacterSaveStore.Load();
            ResetModels();
        }

        private static void CleanFiles()
        {
            // 이 테스트가 만든 것만 지운다(전부 임시 폴더 안이다).
            if (File.Exists(CharacterSaveStore.FilePath)) File.Delete(CharacterSaveStore.FilePath);
            if (File.Exists(CharacterSaveStore.TempFilePath)) File.Delete(CharacterSaveStore.TempFilePath);
            if (File.Exists(LegacySharedTempPath)) File.Delete(LegacySharedTempPath);
            if (File.Exists(BackupCopyPath)) File.Delete(BackupCopyPath);
            // 직전 세대(2026-09-02) — 다른 픽스처가 남긴 것이 이 픽스처의 판정을 흔들지 않게.
            if (File.Exists(CharacterSaveStore.PreviousGenerationPath))
                File.Delete(CharacterSaveStore.PreviousGenerationPath);
        }

        private static void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            CharacterAppearanceModel.ResetForTesting();
            TodoListModel.ResetForTesting();
        }

        /// <summary>"이 인스턴스가 기동해서 정상 파일을 읽고 한동안 돌고 있는" 상태를 만든다.</summary>
        private static void StartResidentInstanceWithNormalFile()
        {
            CharacterProgressionModel.RestoreFromSave(12, 40f, 5000f, "지켜야할동료");
            Assert.IsTrue(CharacterSaveStore.Save(), "사전 조건: 정상 저장에 실패했습니다.");

            ResetModels();
            CharacterSaveStore.Load();
            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "사전 조건: 정상 파일을 읽지 못했습니다.");
            Assert.IsFalse(CharacterSaveStore.NewerVersionFileDetected,
                "사전 조건: 기동 시점에는 신버전 파일이 없어야 한다(J1-B는 그 뒤에 바뀌는 경로다).");
            Assert.IsFalse(CharacterSaveStore.SaveSuspended, "사전 조건: 기동 시점에 저장이 보류되면 안 된다.");
        }

        // ============================================================================
        // J1-B ★ 핵심 — 상주 인스턴스의 발밑에서 파일이 신버전으로 바뀐다
        // ============================================================================

        [Test]
        public void 상주_인스턴스는_발밑에서_신버전으로_바뀐_파일을_덮어쓰지_않는다()
        {
            StartResidentInstanceWithNormalFile();

            // ---- 여기서 다른 인스턴스(새 빌드)가 같은 파일에 저장한다 ----
            File.WriteAllText(CharacterSaveStore.FilePath, FutureJson);

            // 이 인스턴스는 그 사실을 모른 채 평소처럼 값이 바뀌고 주기 저장을 맞는다.
            CharacterProgressionModel.SetCharacterName("구버전이름");
            Assert.IsTrue(CharacterProgressionModel.IsDirty, "사전 조건: 저장할 변경이 있어야 한다.");

            LogAssert.ignoreFailingMessages = true;   // 이 경로는 경고를 남기는 것이 정상 동작이다.
            bool saved = CharacterSaveStore.Save();
            LogAssert.ignoreFailingMessages = false;

            string onDisk = File.ReadAllText(CharacterSaveStore.FilePath);
            Debug.Log($"[동시실행] 저장 반환={saved}, 보류={CharacterSaveStore.SaveSuspended}, " +
                $"신버전감지={CharacterSaveStore.NewerVersionFileDetected}, " +
                $"디스크 앞머리={onDisk.Substring(0, Mathf.Min(40, onDisk.Length)).Replace("\n", " ")}");

            Assert.IsFalse(saved, "덮어쓰지 않았는데 저장이 성공을 보고했습니다 — 호출부가 속습니다.");
            StringAssert.Contains("미래동료", onDisk,
                "구버전 인스턴스가 신버전 저장 파일을 덮어썼습니다 — 재현이 실측한 그 데이터 소실입니다" +
                "(11:07:52 v8 → 11:08:02 v7).");
            StringAssert.Contains($"\"version\": {FutureVersion}", onDisk,
                "파일의 스키마 버전이 구버전으로 되돌아갔습니다.");

            Assert.IsTrue(CharacterSaveStore.NewerVersionFileDetected,
                "덮어쓰기를 취소했는데 그 사실이 진단에 남지 않았습니다 — 실측에서 '경고 로그 0줄'이었던 그 상태입니다.");
            Assert.IsTrue(CharacterSaveStore.SaveSuspended,
                "이번 실행의 저장이 보류되지 않았습니다 — 60초 뒤 같은 시도를 되풀이하게 됩니다.");
            Assert.IsTrue(File.Exists(BackupCopyPath),
                "취소하면서 원본 사본을 남기지 않았습니다.");
            StringAssert.Contains("미래동료", File.ReadAllText(BackupCopyPath),
                "사본의 내용이 원본과 다릅니다.");

            // (2) 모델에게 거짓말하지 않는다 — 쓰지 않았으므로 여전히 미저장 상태여야 한다.
            Assert.IsTrue(CharacterProgressionModel.IsDirty,
                "쓰지 않았는데 모델을 '저장됨'으로 표시했습니다 — 그 거짓말은 나중에 진짜 저장 기회를 잡아먹습니다.");

            // 그리고 그 뒤의 주기 저장도 계속 물러선다(경고가 매분 반복되지도 않는다).
            Assert.IsFalse(CharacterSaveStore.Save(), "보류 뒤의 저장이 다시 시도됐습니다.");
            StringAssert.Contains("미래동료", File.ReadAllText(CharacterSaveStore.FilePath),
                "두 번째 주기 저장이 신버전 파일을 덮어썼습니다.");
        }

        [Test]
        public void NegativeControl_같은_버전_파일이면_평소대로_덮어쓴다()
        {
            // 가드가 "모든 저장을 막아서" 위 테스트가 통과한 것이 아님을 증명한다.
            StartResidentInstanceWithNormalFile();

            CharacterProgressionModel.SetCharacterName("새이름");
            Assert.IsTrue(CharacterSaveStore.Save(), "같은 버전 파일인데 저장이 막혔습니다.");
            Assert.IsFalse(CharacterProgressionModel.IsDirty, "정상 저장인데 모델이 미저장으로 남았습니다.");

            StringAssert.Contains("새이름", File.ReadAllText(CharacterSaveStore.FilePath),
                "정상 경로의 저장이 파일에 반영되지 않았습니다.");
            Assert.IsFalse(CharacterSaveStore.SaveSuspended, "정상 경로에서 저장이 보류됐습니다.");
            Assert.IsFalse(File.Exists(BackupCopyPath), "정상 경로에서 불필요한 사본이 생겼습니다.");
        }

        [Test]
        public void 손상되거나_빈_파일은_저장을_막지_않는다()
        {
            // (3) 계약 — 버전을 읽을 수 없으면 막지 않는다. 손상 파일 하나가 저장 기능을 영구히
            // 잠그면, 되돌릴 수 있는 사고가 되돌릴 수 없는 사고로 바뀐다.
            StartResidentInstanceWithNormalFile();

            File.WriteAllText(CharacterSaveStore.FilePath, "{\n    \"version\": 99");   // 쓰다 만 JSON
            CharacterProgressionModel.SetCharacterName("복구된이름");
            Assert.IsTrue(CharacterSaveStore.Save(), "손상된 파일 때문에 저장이 막혔습니다.");
            StringAssert.Contains("복구된이름", File.ReadAllText(CharacterSaveStore.FilePath),
                "손상된 파일을 정상 내용으로 덮어쓰지 못했습니다.");

            File.WriteAllText(CharacterSaveStore.FilePath, "   ");                      // 빈 파일
            CharacterProgressionModel.SetCharacterName("복구된이름2");
            Assert.IsTrue(CharacterSaveStore.Save(), "빈 파일 때문에 저장이 막혔습니다.");
            StringAssert.Contains("복구된이름2", File.ReadAllText(CharacterSaveStore.FilePath),
                "빈 파일을 정상 내용으로 덮어쓰지 못했습니다.");

            Assert.IsFalse(CharacterSaveStore.SaveSuspended, "손상 파일을 신버전으로 오판해 저장을 보류했습니다.");
        }

        // ============================================================================
        // J2 — 임시 파일은 인스턴스마다 다르다
        // ============================================================================

        [Test]
        public void 임시_파일_경로는_인스턴스마다_다르다()
        {
            string temp = CharacterSaveStore.TempFilePath;
            string tempName = Path.GetFileName(temp);
            string pid;
            using (var self = System.Diagnostics.Process.GetCurrentProcess())
            {
                pid = self.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            Debug.Log($"[동시실행] 임시 파일={tempName}, 이 프로세스 아이디={pid}");

            Assert.AreEqual(SaveDirectory, Path.GetDirectoryName(temp),
                "임시 파일이 저장 파일과 다른 폴더에 있습니다 — 다른 볼륨이면 File.Replace가 실패해 " +
                "비원자적 폴백으로 떨어집니다.");
            Assert.IsTrue(tempName.EndsWith(".writing"),
                "임시 파일 이름이 .writing으로 끝나지 않습니다 — 저장 파일/백업 명명 규칙과 겹칠 수 있습니다.");
            Assert.AreNotEqual("stickmate_character.json.writing", tempName,
                "임시 파일 이름이 전 인스턴스 공유 고정 이름 그대로입니다 — 두 인스턴스가 서로의 " +
                "임시 파일을 자르거나 승격시킬 수 있습니다(J2).");
            StringAssert.Contains(pid, tempName,
                "임시 파일 이름에 이 인스턴스를 가리키는 꼬리표가 없습니다.");
        }

        [Test]
        public void 다른_인스턴스의_임시파일을_밟지_않는다()
        {
            // 다른 인스턴스가 옛 공유 경로에 쓰다 만 상태를 그대로 만든다. 옛 코드는 이 파일을
            // FileMode.Create로 자르고 File.Replace로 **소모**했다(= 아래 단언에서 파일이 사라진다).
            const string OtherInstanceHalfWritten = "{ 다른 인스턴스가 쓰던 중이던 임시 파일 }";
            File.WriteAllText(LegacySharedTempPath, OtherInstanceHalfWritten);

            CharacterProgressionModel.RestoreFromSave(12, 40f, 5000f, "지켜야할동료");
            Assert.IsTrue(CharacterSaveStore.Save(), "다른 인스턴스의 임시 파일이 있을 때 저장이 실패했습니다.");
            Assert.IsTrue(CharacterSaveStore.LastSaveWasAtomic, "저장이 비원자적 폴백으로 떨어졌습니다.");

            Assert.IsTrue(File.Exists(LegacySharedTempPath),
                "다른 인스턴스의 임시 파일이 사라졌습니다 — 우리 저장이 그 파일을 밟았다는 뜻입니다(J2).");
            Assert.AreEqual(OtherInstanceHalfWritten, File.ReadAllText(LegacySharedTempPath),
                "다른 인스턴스의 임시 파일 내용이 바뀌었습니다.");

            string saved = File.ReadAllText(CharacterSaveStore.FilePath);
            StringAssert.Contains("지켜야할동료", saved,
                "저장 파일에 우리 내용이 들어가지 않았습니다.");
            StringAssert.DoesNotContain("다른 인스턴스", saved,
                "남의 임시 파일 내용이 본체로 승격됐습니다 — 이것이 '조용히 새 캐릭터로 시작'하는 경로입니다.");
        }
    }
}
