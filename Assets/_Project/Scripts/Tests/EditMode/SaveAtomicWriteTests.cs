using System.IO;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>저장 파일은 원자적으로 쓰인다</b> — 2026-08-31 R5 "비원자적 쓰기" 수정의 회귀 잠금.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 저장이 <c>File.WriteAllText(FilePath, json)</c> 한 줄이었다. 이 호출은 대상 파일을 <b>먼저 0바이트로
    /// 자른 뒤</b> 내용을 쓴다. 그 사이에 크래시/강제 종료/전원 차단이 나면 파일에는 반쯤 쓰인 JSON이
    /// 남고, 다음 실행의 <see cref="CharacterSaveStore.Load"/>는 그것을 파싱하지 못해 "기본값(Lv.1 /
    /// 빈 할일)으로 시작"으로 떨어진다 — 레벨·장비·기록·<b>사용자가 적은 오늘 할일</b>이 통째로 사라진다.
    /// 하루 종일 켜져 있고 60초마다 저장하는 앱이라, 강제 종료가 그 창을 때릴 기회는 매일 수백 번 있다.
    ///
    /// ============================================================================
    /// 지금의 계약
    /// ============================================================================
    ///   (1) 저장은 임시 파일에 전량을 쓰고 <c>File.Replace</c>로 <b>한 번에</b> 갈아끼운다.
    ///       그래서 어느 순간에 죽어도 저장 경로에는 <b>옛 파일 아니면 새 파일</b>만 존재한다.
    ///   (2) 정상 저장이 끝나면 임시 파일은 남지 않는다(다음 실행이 쓰레기를 보지 않는다).
    ///   (3) 임시 파일까지만 쓰이고 교체 전에 죽은 상황(= 이 테스트가 흉내내는 시나리오)에서
    ///       <b>원본은 한 바이트도 훼손되지 않는다</b>. 그 뒤의 Load()는 옛 내용을 정확히 복원한다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// <see cref="NegativeControl_옛_방식으로_반쯤_쓰면_실제로_전손된다"/>가 같은 검사 방법으로
    /// "옛 방식(대상 파일에 직접 쓰다 중단)"의 피해를 재현한다 — (3)의 통과가 "아무 일도 안 일어나서"
    /// 얻어진 것이 아님을 증명한다.
    /// </summary>
    public sealed class SaveAtomicWriteTests
    {
        /// <summary>쓰다 만 JSON. 중괄호가 닫히지 않아 JsonUtility가 해석할 수 없다(= 전손 조건).</summary>
        private const string TruncatedJson = "{\n    \"version\": 7,\n    \"level\": 12,\n    \"characterNa";

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
            CleanTemp();
            ResetModels();
        }

        [SetUp]
        public void ResetBeforeEach()
        {
            CleanTemp();
            if (File.Exists(CharacterSaveStore.FilePath)) File.Delete(CharacterSaveStore.FilePath);
            CharacterSaveStore.Load();   // 정적 진단 플래그 초기화(SaveDowngradeGuardTests와 같은 관례)
            ResetModels();
        }

        private static void CleanTemp()
        {
            if (File.Exists(CharacterSaveStore.TempFilePath)) File.Delete(CharacterSaveStore.TempFilePath);

            // ★ 2026-09-02 — 직전 세대 파일도 함께 치운다. 이것이 남아 있으면 아래 네거티브 컨트롤이
            //   "본체가 깨졌는데도" 세대에서 복구돼 초록이 된다(= 전손을 감지하지 못하는 검사가 된다).
            //   EditMode 스위트는 임시 폴더 하나를 전 픽스처가 공유하므로 다른 픽스처가 남긴 것도 온다.
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

        /// <summary>"며칠 키운 캐릭터"를 만들고 저장한다(잃으면 아픈 상태를 실제로 디스크에 올린다).</summary>
        private static void SaveGrownCharacter()
        {
            CharacterProgressionModel.RestoreFromSave(12, 40f, 5000f, "지켜야할동료");
            CharacterStatsModel.RestoreFromSave(9, 30, 7, 4200f, 5, 1788038056L);
            CharacterAppearanceModel.SetInkColor(StickmanInkColor.White);
            Assert.IsTrue(CharacterSaveStore.Save(), "사전 조건: 저장에 실패했습니다.");
        }

        // ============================================================================
        // (1)(2) 정상 경로
        // ============================================================================

        [Test]
        public void 정상_저장은_원자적_교체로_끝나고_임시_파일을_남기지_않는다()
        {
            SaveGrownCharacter();

            Debug.Log($"[ATOMIC] 저장 경로={CharacterSaveStore.FilePath}, 임시={CharacterSaveStore.TempFilePath}, " +
                $"원자적={CharacterSaveStore.LastSaveWasAtomic}, 임시 존재={File.Exists(CharacterSaveStore.TempFilePath)}.");

            Assert.IsTrue(CharacterSaveStore.LastSaveWasAtomic,
                "저장이 원자적 교체 경로로 끝나지 않았습니다(폴백으로 물러섰습니다) — 이 플랫폼에서 File.Replace가 " +
                "동작하지 않는다는 뜻이므로, 쓰기 도중 강제 종료에 대한 보호가 사라집니다.");
            Assert.IsFalse(File.Exists(CharacterSaveStore.TempFilePath),
                "정상 저장이 끝났는데 임시 파일이 남아 있습니다 — 다음 실행이 쓰레기 파일을 보게 됩니다.");

            // 내용도 정상이어야 한다(원자적이지만 비어 있으면 아무 의미가 없다).
            ResetModels();
            CharacterSaveStore.Load();
            Assert.IsTrue(CharacterSaveStore.LoadedFromFile);
            Assert.AreEqual(12, CharacterProgressionModel.Level);
            Assert.AreEqual("지켜야할동료", CharacterProgressionModel.CharacterName);
            Assert.AreEqual(StickmanInkColor.White, CharacterAppearanceModel.InkColor);
        }

        [Test]
        public void 저장_파일이_아직_없는_첫_저장도_정상_동작한다()
        {
            Assert.IsFalse(File.Exists(CharacterSaveStore.FilePath), "사전 조건: 저장 파일이 없어야 합니다.");

            SaveGrownCharacter();

            Assert.IsTrue(File.Exists(CharacterSaveStore.FilePath), "첫 저장에서 파일이 만들어지지 않았습니다.");
            Assert.IsFalse(File.Exists(CharacterSaveStore.TempFilePath), "첫 저장 뒤 임시 파일이 남았습니다.");
            Assert.Greater(new FileInfo(CharacterSaveStore.FilePath).Length, 50,
                "첫 저장 파일이 사실상 비어 있습니다 — 빈 파일을 만든 뒤 교체가 일어나지 않았습니다.");

            ResetModels();
            CharacterSaveStore.Load();
            Assert.AreEqual(12, CharacterProgressionModel.Level, "첫 저장 내용이 복원되지 않았습니다.");
        }

        // ============================================================================
        // (3) ★ 핵심 — 쓰기 도중 강제 종료를 흉내낸다
        // ============================================================================

        [Test]
        public void 임시_파일까지만_쓰이고_교체_전에_죽어도_원본이_그대로_남는다()
        {
            SaveGrownCharacter();
            string original = File.ReadAllText(CharacterSaveStore.FilePath);

            // ---- 여기부터가 "다음 주기 저장이 시작됐다가 프로세스가 죽은" 순간의 재현 ----
            // 새 코드는 (a) 임시 파일에 전량 쓰기 → (b) File.Replace 순서로 동작한다. 그래서 (a)와 (b)
            // 사이에서 죽으면 디스크에는 "완전한 옛 파일 + 쓰다 만 임시 파일"이 남는다. 그 상태를
            // 그대로 만든다(임시 파일에는 일부러 잘린 JSON을 넣어, 만에 하나 그것이 읽히면 들키게 한다).
            File.WriteAllText(CharacterSaveStore.TempFilePath, TruncatedJson);

            Debug.Log($"[ATOMIC] 강제 종료 재현 — 임시 파일 {new FileInfo(CharacterSaveStore.TempFilePath).Length}바이트(잘린 JSON), " +
                $"원본 {new FileInfo(CharacterSaveStore.FilePath).Length}바이트.");

            // ---- 다음 실행이 하는 일: 그냥 Load() ----
            ResetModels();
            CharacterSaveStore.Load();

            Assert.AreEqual(original, File.ReadAllText(CharacterSaveStore.FilePath),
                "쓰기 도중 강제 종료 시나리오에서 저장 파일 원본이 훼손됐습니다 — 이것이 이번 수정이 막는 바로 그 사고입니다.");
            Assert.IsTrue(CharacterSaveStore.LoadedFromFile,
                "강제 종료 뒤 저장 파일을 읽지 못했습니다 — 사용자에게는 '캐릭터가 초기화됐다'로 보입니다.");
            Assert.AreEqual(12, CharacterProgressionModel.Level, "레벨이 사라졌습니다.");
            Assert.AreEqual("지켜야할동료", CharacterProgressionModel.CharacterName, "이름이 사라졌습니다.");
            Assert.AreEqual(9, CharacterStatsModel.BattleWins, "기록이 사라졌습니다.");
            Assert.AreEqual(StickmanInkColor.White, CharacterAppearanceModel.InkColor, "잉크색이 사라졌습니다.");

            // 그리고 다음 저장은 그 쓰레기 임시 파일을 딛고 정상적으로 끝난다(고아 파일이 쌓이지 않는다).
            Assert.IsTrue(CharacterSaveStore.Save(), "강제 종료 잔재가 있는 상태에서 다음 저장이 실패했습니다.");
            Assert.IsTrue(CharacterSaveStore.LastSaveWasAtomic, "다음 저장이 폴백 경로로 떨어졌습니다.");
            Assert.IsFalse(File.Exists(CharacterSaveStore.TempFilePath),
                "다음 저장 뒤에도 잘린 임시 파일이 남아 있습니다.");
        }

        // ============================================================================
        // 네거티브 컨트롤 — 위 검사가 실제로 전손을 잡아내는가
        // ============================================================================

        [Test]
        public void NegativeControl_옛_방식으로_반쯤_쓰면_실제로_전손된다()
        {
            SaveGrownCharacter();

            // 옛 코드(File.WriteAllText(FilePath, json))가 쓰다 중단된 상태 = 대상 파일 자체가 잘린다.
            File.WriteAllText(CharacterSaveStore.FilePath, TruncatedJson);

            ResetModels();
            CharacterSaveStore.Load();

            Debug.Log($"[ATOMIC] 네거티브 컨트롤 — 대상 파일을 직접 잘라 쓴 뒤 Load(): " +
                $"LoadedFromFile={CharacterSaveStore.LoadedFromFile}, Level={CharacterProgressionModel.Level}, " +
                $"이름={CharacterProgressionModel.CharacterName}.");

            Assert.IsFalse(CharacterSaveStore.LoadedFromFile,
                "대상 파일을 반쯤 쓴 상태인데 정상 로드로 판정됐습니다 — 그러면 위 테스트의 검사 방법이 " +
                "전손을 감지하지 못한다는 뜻이라 그 통과가 무의미합니다.");
            Assert.AreEqual(1, CharacterProgressionModel.Level,
                "전손 상황인데 레벨이 남아 있습니다 — 검사 방법의 민감도를 의심해야 합니다.");
            Assert.AreNotEqual("지켜야할동료", CharacterProgressionModel.CharacterName);
        }
    }
}
