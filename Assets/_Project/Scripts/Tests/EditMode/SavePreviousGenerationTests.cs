using System;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>원자적 교체가 거절되는 환경에서도 직전 저장은 살아남는다</b> —
    /// 2026-09-02, 사용자 Windows 실기 로그의 회귀 잠금.
    ///
    /// ============================================================================
    /// 무엇이 관측됐나 (실기)
    /// ============================================================================
    /// <code>
    /// [성장] 저장 파일을 원자적으로 교체하지 못해 직접 쓰기로 물러섰습니다
    ///        (IOException: 바꿀 파일을 제거할 수 없습니다).
    /// </code>
    /// 그 문장은 OS 메시지 테이블의 ERROR_UNABLE_TO_REMOVE_REPLACED(1175)다 —
    /// <c>ReplaceFile</c>의 <b>첫 걸음(대상 치우기)</b>이 거절됐다는 뜻이고, 그 순간에도
    /// 직접 쓰기는 성공했다(= "쓸 수는 있는데 치울 수는 없는" 상태). 옛 폴백은 바로 그 자리에서
    /// 대상 파일을 잘라 쓰기 시작해 <b>손상 창을 스스로 열었다</b>. 하루 1,440회 저장하는 앱이다.
    ///
    /// ============================================================================
    /// 지금의 계약
    /// ============================================================================
    ///   (1) 정상 저장은 <c>File.Replace</c> 한 번으로 끝나고 <b>직전 세대</b>를 남긴다.
    ///   (2) 교체가 몇 번 거절돼도 짧은 재시도가 원자성을 지켜 낸다(1175는 대개 곧 풀린다).
    ///   (3) 사다리를 전부 소진하면 그림자 커밋으로 물러서되, <b>덮어쓰기 전에</b> 지금 내용을
    ///       직전 세대로 대피시킨다 — 순서가 이 보증의 전부다.
    ///   (4) 그 덮어쓰기 <b>도중</b> 프로세스가 사라져도 다음 실행은 직전 저장으로 되돌아간다.
    ///   (5) 본체가 이미 깨져 있으면 그것으로 직전 세대를 덮지 <b>않는다</b>(마지막 복구원 보호).
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 — 이 파일의 존재 이유
    /// ============================================================================
    /// 원자성/백업 보증은 <b>깨지는 순간에만</b> 값어치가 있다. 그래서 여기서는 상태를 밖에서
    /// 흉내내지 않고 <b>프로덕션 순서를 그대로 밟다가 덮어쓰기 도중에 멈춘다</b>
    /// (<see cref="CharacterSaveStore.SimulateDeathDuringOverwriteForTesting"/>).
    /// 그리고 <see cref="NegativeControl_직전_세대가_없으면_같은_사고가_전손이_된다"/>가
    /// <b>같은 검사 방법으로</b> 옛 상태(세대 없음)의 피해를 재현해, (4)의 통과가 "아무 일도
    /// 안 일어나서" 얻어진 것이 아님을 증명한다.
    ///
    /// 상수는 하나도 베껴 적지 않는다 — 재시도 예산은
    /// <see cref="CharacterSaveStore.AtomicCommitAttemptBudget"/>을, 세대 경로는
    /// <see cref="CharacterSaveStore.PreviousGenerationPath"/>를 참조한다(CLAUDE.md 협업 프로토콜).
    /// </summary>
    public sealed class SavePreviousGenerationTests
    {
        private const string NameA = "1세대동료";
        private const string NameB = "2세대동료";

        [OneTimeSetUp]
        public void RequireTemporaryDirectory()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 임시 폴더로 리디렉션되어 있지 않습니다(GlobalEditModeTestIsolation 확인). " +
                "이 테스트는 파일을 실제로 쓰고 지우므로 그 상태에서는 실행하지 않습니다.");
        }

        [SetUp]
        public void ResetBeforeEach()
        {
            CleanFiles();
            CharacterSaveStore.Load();     // 정적 진단 플래그 초기화(이 폴더의 관례)
            ResetModels();
            CharacterSaveStore.ForceAtomicCommitFailuresForTesting(0);
            CharacterSaveStore.SimulateDeathDuringOverwriteForTesting(-1);
        }

        [TearDown]
        public void ClearInjectionsAfterEach()
        {
            // 주입이 다음 픽스처로 새어 나가면 무관한 테스트가 폴백 경로를 타게 된다.
            CharacterSaveStore.ForceAtomicCommitFailuresForTesting(0);
            CharacterSaveStore.SimulateDeathDuringOverwriteForTesting(-1);
        }

        [OneTimeTearDown]
        public void CleanUpAfterFixture()
        {
            CleanFiles();
            CharacterSaveStore.Load();
            ResetModels();
        }

        private static void CleanFiles()
        {
            if (File.Exists(CharacterSaveStore.FilePath)) File.Delete(CharacterSaveStore.FilePath);
            if (File.Exists(CharacterSaveStore.TempFilePath)) File.Delete(CharacterSaveStore.TempFilePath);
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

        /// <summary>"며칠 키운 캐릭터"를 만들어 디스크에 올린다(잃으면 아픈 상태를 실제로 만든다).</summary>
        private static void SaveGeneration(int level, string name)
        {
            CharacterProgressionModel.RestoreFromSave(level, 40f, 5000f, name);
            Assert.IsTrue(CharacterSaveStore.Save(), $"사전 조건: '{name}' 저장에 실패했습니다.");
        }

        private static string LoadedName()
        {
            ResetModels();
            CharacterSaveStore.Load();
            return CharacterProgressionModel.CharacterName;
        }

        // ============================================================================
        // (1)(2) 원자성을 지키는 구간
        // ============================================================================

        [Test]
        public void 첫_저장은_남길_직전_세대가_없다()
        {
            SaveGeneration(11, NameA);

            Assert.IsTrue(CharacterSaveStore.LastSaveWasAtomic, "첫 저장이 원자적 경로로 끝나지 않았습니다.");
            Assert.IsFalse(CharacterSaveStore.LastSaveKeptPreviousGeneration,
                "저장된 것이 없는데 직전 세대를 남겼다고 보고했습니다.");
            Assert.IsFalse(File.Exists(CharacterSaveStore.PreviousGenerationPath),
                "첫 저장이 빈 파일을 직전 세대로 남겼습니다 — 복구원 자리를 쓰레기가 차지합니다.");
        }

        [Test]
        public void 두_번째_저장부터_직전_세대가_남고_그것은_바로_앞_내용이다()
        {
            SaveGeneration(11, NameA);
            SaveGeneration(12, NameB);

            Assert.IsTrue(CharacterSaveStore.LastSaveWasAtomic, "두 번째 저장이 원자적 경로로 끝나지 않았습니다.");
            Assert.IsTrue(CharacterSaveStore.LastSaveKeptPreviousGeneration,
                "원자적 교체가 직전 세대를 남기지 못했습니다 — 이 플랫폼에서 File.Replace의 백업 인자가 " +
                "동작하지 않는다는 뜻이고, 그러면 폴백 경로의 복구원이 한 세대 낡습니다.");

            string previous = File.ReadAllText(CharacterSaveStore.PreviousGenerationPath);
            StringAssert.Contains(NameA, previous, "직전 세대에 바로 앞 내용이 들어 있지 않습니다.");
            StringAssert.DoesNotContain(NameB, previous, "직전 세대가 이번 내용으로 채워졌습니다(세대가 밀리지 않았습니다).");
            StringAssert.Contains(NameB, File.ReadAllText(CharacterSaveStore.FilePath), "본체가 갱신되지 않았습니다.");
        }

        [Test]
        public void 교체가_몇_번_거절돼도_재시도가_원자성을_지킨다()
        {
            SaveGeneration(11, NameA);

            // 실기 1175와 같은 성질(잠깐 막혔다 풀린다)을 만든다 — 예산보다 적게 막는다.
            int transient = CharacterSaveStore.AtomicCommitAttemptBudget - 1;
            Assert.Greater(transient, 0, "재시도 예산이 0이면 이 테스트는 아무것도 재지 않습니다.");
            CharacterSaveStore.ForceAtomicCommitFailuresForTesting(transient);

            SaveGeneration(12, NameB);

            Assert.IsTrue(CharacterSaveStore.LastSaveWasAtomic,
                $"교체가 {transient}회 거절됐을 뿐인데 원자성을 포기했습니다 — 재시도가 동작하지 않습니다.");
            Assert.IsFalse(File.Exists(CharacterSaveStore.TempFilePath), "재시도 성공 뒤 임시 파일이 남았습니다.");
            Assert.AreEqual(0, CharacterSaveStore.ConsecutiveAtomicCommitFailures,
                "원자적으로 성공했는데 연속 실패 카운터가 남아 있습니다.");
            Assert.AreEqual(NameB, LoadedName(), "재시도로 저장한 내용이 복원되지 않았습니다.");
        }

        // ============================================================================
        // (3) 사다리를 전부 소진했을 때 — 그림자 커밋
        // ============================================================================

        [Test]
        public void 교체가_아예_안_되는_환경에서는_대피시킨_뒤_덮어쓴다()
        {
            SaveGeneration(11, NameA);

            CharacterSaveStore.ForceAtomicCommitFailuresForTesting(CharacterSaveStore.AtomicCommitAttemptBudget);
            LogAssert.ignoreFailingMessages = true;   // 이 경로는 경고 로그를 내는 것이 정상 동작이다.
            SaveGeneration(12, NameB);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(CharacterSaveStore.LastSaveWasAtomic,
                "교체를 예산만큼 전부 막았는데 원자적으로 끝났다고 보고했습니다 — 주입이 물지 않았습니다.");
            Assert.AreEqual(1, CharacterSaveStore.ConsecutiveAtomicCommitFailures,
                "연속 실패가 세어지지 않았습니다(로그 도배 방지와 진단이 함께 죽습니다).");
            Assert.IsTrue(CharacterSaveStore.LastSaveKeptPreviousGeneration,
                "그림자 커밋이 직전 세대를 남기지 않았습니다 — 이 경로에는 손상 창이 있고, " +
                "그 창을 받아 낼 것이 사라집니다.");

            // ★ 순서 검증: 대피가 덮어쓰기보다 <b>먼저</b>여야만 세대에 옛 내용이 들어간다.
            StringAssert.Contains(NameA, File.ReadAllText(CharacterSaveStore.PreviousGenerationPath),
                "직전 세대에 옛 내용이 없습니다 — 덮어쓴 뒤에 대피했다는 뜻이고, 그러면 보증이 무효입니다.");
            Assert.AreEqual(NameB, LoadedName(), "폴백 경로에서 저장 자체가 반영되지 않았습니다.");
        }

        // ============================================================================
        // (4) ★ 핵심 — 그 덮어쓰기 도중에 실제로 멈춘다
        // ============================================================================

        [Test]
        public void 덮어쓰기_도중_프로세스가_사라져도_직전_저장으로_되돌아간다()
        {
            SaveGeneration(11, NameA);

            CharacterSaveStore.ForceAtomicCommitFailuresForTesting(CharacterSaveStore.AtomicCommitAttemptBudget);
            CharacterSaveStore.SimulateDeathDuringOverwriteForTesting(40);   // JSON 앞 40바이트만 쓰고 멈춘다

            CharacterProgressionModel.RestoreFromSave(12, 40f, 5000f, NameB);
            LogAssert.ignoreFailingMessages = true;
            bool saved = CharacterSaveStore.Save();
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(saved,
                "쓰기 도중 사라졌는데 저장 성공으로 보고했습니다 — 모델이 '디스크에 반영됨'으로 표시되면 " +
                "다음 주기 저장이 그 변경을 다시 시도하지 않습니다.");

            string wrecked = File.ReadAllText(CharacterSaveStore.FilePath);
            Assert.Less(wrecked.Length, 200, "본체가 잘리지 않았습니다 — 사고 상황이 만들어지지 않았습니다.");
            Assert.IsTrue(File.Exists(CharacterSaveStore.PreviousGenerationPath), "직전 세대가 없습니다.");

            Debug.Log($"[세대] 사고 직후 디스크 — 본체 {wrecked.Length}바이트(잘림), " +
                $"직전 세대 {new FileInfo(CharacterSaveStore.PreviousGenerationPath).Length}바이트.");

            // ---- 다음 실행이 하는 일: 그냥 Load() ----
            Assert.AreEqual(NameA, LoadedName(),
                "쓰기 도중 강제 종료 뒤 직전 저장으로 되돌아가지 못했습니다 — 사용자에게는 " +
                "'캐릭터가 초기화됐다'로 보입니다. 이것이 이번 라운드가 막는 바로 그 사고입니다.");
            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "복구했는데 로드 성공으로 보고하지 않았습니다.");
            Assert.IsTrue(CharacterSaveStore.LoadedFromPreviousGeneration,
                "직전 세대로 복구했는데 그 사실이 진단에 남지 않았습니다.");
            Assert.AreEqual(11, CharacterProgressionModel.Level, "레벨이 직전 세대의 값이 아닙니다.");
        }

        [Test]
        public void NegativeControl_직전_세대가_없으면_같은_사고가_전손이_된다()
        {
            // 위 테스트와 <b>완전히 같은 검사 방법</b>으로, 복구원만 없앤다.
            SaveGeneration(11, NameA);

            CharacterSaveStore.ForceAtomicCommitFailuresForTesting(CharacterSaveStore.AtomicCommitAttemptBudget);
            CharacterSaveStore.SimulateDeathDuringOverwriteForTesting(40);

            CharacterProgressionModel.RestoreFromSave(12, 40f, 5000f, NameB);
            LogAssert.ignoreFailingMessages = true;
            CharacterSaveStore.Save();
            LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(File.Exists(CharacterSaveStore.PreviousGenerationPath), "사전 조건: 세대가 있어야 합니다.");
            File.Delete(CharacterSaveStore.PreviousGenerationPath);   // 옛 코드에는 이것이 아예 없었다

            LogAssert.ignoreFailingMessages = true;
            string name = LoadedName();
            LogAssert.ignoreFailingMessages = false;

            Debug.Log($"[세대] 네거티브 컨트롤 — 세대를 없앤 뒤 Load(): " +
                $"LoadedFromFile={CharacterSaveStore.LoadedFromFile}, Level={CharacterProgressionModel.Level}, 이름={name}.");

            Assert.IsFalse(CharacterSaveStore.LoadedFromFile,
                "복구원이 하나도 없는데 정상 로드로 판정됐습니다 — 그러면 위 테스트의 검사 방법이 전손을 " +
                "감지하지 못한다는 뜻이라 그 통과가 무의미합니다.");
            Assert.AreNotEqual(NameA, name, "복구원이 없는데 옛 이름이 살아났습니다(검사 민감도 의심).");
            Assert.AreEqual(1, CharacterProgressionModel.Level, "전손 상황인데 레벨이 남아 있습니다.");
        }

        // ============================================================================
        // (5) 마지막 복구원 보호
        // ============================================================================

        [Test]
        public void 이미_깨진_본체로_온전한_직전_세대를_덮지_않는다()
        {
            // 사고가 두 번 연달아 나는 상황: 본체는 이미 잘려 있고, 세대에만 온전한 내용이 있다.
            SaveGeneration(11, NameA);
            SaveGeneration(12, NameB);      // 세대 = NameA, 본체 = NameB
            File.WriteAllText(CharacterSaveStore.FilePath, "{ \"version\": 9, \"level\": 12, \"characterNa");

            CharacterSaveStore.ForceAtomicCommitFailuresForTesting(CharacterSaveStore.AtomicCommitAttemptBudget);
            CharacterProgressionModel.RestoreFromSave(13, 40f, 5000f, "3세대동료");
            LogAssert.ignoreFailingMessages = true;
            Assert.IsTrue(CharacterSaveStore.Save(), "폴백 경로에서 저장이 실패했습니다.");
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(CharacterSaveStore.LastSaveKeptPreviousGeneration,
                "깨진 본체를 세대로 대피시켜 놓고 '세대를 남겼다'고 보고했습니다.");
            StringAssert.Contains(NameA, File.ReadAllText(CharacterSaveStore.PreviousGenerationPath),
                "깨진 본체가 온전한 직전 세대를 덮었습니다 — 마지막 복구원을 사고가 잡아먹습니다.");
        }

        // ============================================================================
        // 없어진 파일은 세대로 되살리지 않는다 (의도적 경계)
        // ============================================================================

        [Test]
        public void 본체가_아예_없으면_세대를_뒤지지_않고_새_캐릭터로_시작한다()
        {
            SaveGeneration(11, NameA);
            SaveGeneration(12, NameB);
            Assert.IsTrue(File.Exists(CharacterSaveStore.PreviousGenerationPath), "사전 조건: 세대가 있어야 합니다.");

            File.Delete(CharacterSaveStore.FilePath);   // 사용자가 캐릭터를 초기화하려고 파일을 치운 모양

            ResetModels();
            CharacterSaveStore.Load();

            Assert.IsFalse(CharacterSaveStore.LoadedFromFile,
                "파일을 지웠는데 세대에서 되살아났습니다 — '지웠는데 돌아온다'가 됩니다.");
            Assert.IsFalse(CharacterSaveStore.LoadedFromPreviousGeneration);
            Assert.AreEqual(1, CharacterProgressionModel.Level);
        }
    }
}
