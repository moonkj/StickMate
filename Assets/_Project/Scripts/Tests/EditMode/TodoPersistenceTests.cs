using System.IO;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 할일 목록의 영속화(Core/TodoListModel.cs + Core/CharacterSaveStore.cs <b>스키마 v4</b>) 회귀 테스트
    /// — 2026-08-30 리더 결정. [오늘 할일] 팝오버가 생기면서 이 목록이 <b>사용자가 자기 진짜 일정을 처음
    /// 적는 입구</b>가 됐다. 앱을 끄면 조용히 사라지는 할일 목록은 기능 실패다.
    ///
    /// 잠그는 절대 조건:
    ///  ① 적어둔 할일과 완료함이 저장 -> 로드 왕복에서 그대로 살아남는다(내용/완료 여부/순서).
    ///  ② <b>v1/v2/v3 저장 파일이 그대로 읽힌다</b> — 할일 필드가 없으면 "적어둔 할일이 없다"이지
    ///     "파일이 깨졌다"가 아니다. 스키마를 올릴 때마다 사용자의 레벨/기록이 날아가지 않게 하는
    ///     이 프로젝트의 관례(UiLayoutPersistenceTests와 같은 정신)를 이어서 확인한다.
    ///  ③ 복원 뒤에 추가한 항목이 <b>기존 항목과 같은 Id를 갖지 않는다</b> — 같으면 한 줄을 체크했는데
    ///     다른 줄이 체크되는 사고가 난다.
    ///  ④ 지난 세션의 "완료 유예 중" 항목이 <b>영원히 유예 상태로 굳지 않는다</b>(완료 시각은 지난
    ///     세션의 Time.unscaledTime이라 이번 실행에서는 의미가 없다).
    ///
    /// 파일 취급은 관례 그대로 — 실제 앱의 저장 파일을 전후로 백업/복원하고 대상은
    /// <see cref="CharacterSaveStore.FilePath"/> 하나뿐이다.
    /// </summary>
    public sealed class TodoPersistenceTests
    {
        private const int SoftCap = 15;

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
        public void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            TodoListModel.ResetForTesting();
        }

        [Test]
        public void 적어둔_할일이_저장하고_다시_불러온_뒤에도_같다()
        {
            TodoListModel.Add("보고서 초안", SoftCap);
            TodoListModel.Add("장보기", SoftCap);
            TodoListModel.Add("세탁물 찾기", SoftCap);
            int thirdId = TodoListModel.ActiveItems[2].Id;
            TodoListModel.ToggleComplete(thirdId);

            Assert.IsTrue(TodoListModel.IsDirty, "할일을 적었는데 저장 대상으로 표시되지 않았습니다.");
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");
            Assert.IsFalse(TodoListModel.IsDirty, "저장했는데 여전히 저장 대상으로 남아 있습니다.");

            TodoListModel.ResetForTesting();
            Assert.AreEqual(0, TodoListModel.ActiveItems.Count, "준비 조건 실패 — 메모리가 비워지지 않았습니다.");

            CharacterSaveStore.Load();

            Assert.AreEqual(3, TodoListModel.ActiveItems.Count,
                "재시작 후 할일 개수가 다릅니다 — 사용자가 적어둔 목록이 사라졌습니다.");
            Assert.AreEqual("보고서 초안", TodoListModel.ActiveItems[0].Text, "순서/내용이 바뀌었습니다.");
            Assert.AreEqual("장보기", TodoListModel.ActiveItems[1].Text);
            Assert.AreEqual("세탁물 찾기", TodoListModel.ActiveItems[2].Text);
            Assert.IsTrue(TodoListModel.ActiveItems[2].Completed, "체크해둔 항목이 미완료로 되살아났습니다.");
            Assert.AreEqual(2, TodoListModel.UncompletedCount, "미완료 개수가 다릅니다.");
            Assert.IsFalse(TodoListModel.IsDirty, "복원 직후는 저장 대상이 아니어야 합니다(복원은 변화가 아니다).");
        }

        [Test]
        public void 완료함도_함께_보존된다()
        {
            TodoListModel.Add("끝낸 일", SoftCap);
            int id = TodoListModel.ActiveItems[0].Id;
            TodoListModel.ToggleComplete(id);
            TodoListModel.SweepCompleted(0f);   // 유예 0초 -> 즉시 완료함으로.

            Assert.AreEqual(0, TodoListModel.ActiveItems.Count, "준비 조건 실패 — 완료함으로 옮겨지지 않았습니다.");
            Assert.AreEqual(1, TodoListModel.CompletedArchive.Count);

            Assert.IsTrue(CharacterSaveStore.Save());
            TodoListModel.ResetForTesting();
            CharacterSaveStore.Load();

            Assert.AreEqual(1, TodoListModel.CompletedArchive.Count,
                "완료함이 사라졌습니다 — 17절의 데이터 보존 원칙이 깨집니다.");
            Assert.AreEqual("끝낸 일", TodoListModel.CompletedArchive[0].Text);
        }

        [Test]
        public void 복원_뒤_추가한_항목은_기존과_다른_Id를_받는다()
        {
            TodoListModel.Add("첫째", SoftCap);
            TodoListModel.Add("둘째", SoftCap);
            int lastId = TodoListModel.ActiveItems[1].Id;
            Assert.IsTrue(CharacterSaveStore.Save());

            TodoListModel.ResetForTesting();
            CharacterSaveStore.Load();
            TodoListModel.Add("셋째", SoftCap);

            int newId = TodoListModel.ActiveItems[2].Id;
            Assert.Greater(newId, lastId,
                "복원 후 추가한 항목이 기존 Id와 겹쳤습니다 — 한 줄을 체크했는데 다른 줄이 체크됩니다.");
            for (int i = 0; i < TodoListModel.ActiveItems.Count; i++)
            {
                for (int j = i + 1; j < TodoListModel.ActiveItems.Count; j++)
                {
                    Assert.AreNotEqual(TodoListModel.ActiveItems[i].Id, TodoListModel.ActiveItems[j].Id,
                        "목록 안에 같은 Id가 두 개 있습니다.");
                }
            }
        }

        [Test]
        public void 지난_세션의_완료_유예는_이번_실행에서_정상적으로_걷힌다()
        {
            TodoListModel.Add("어제 체크한 일", SoftCap);
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[0].Id);
            Assert.IsTrue(CharacterSaveStore.Save());

            TodoListModel.ResetForTesting();
            CharacterSaveStore.Load();

            Assert.AreEqual(1, TodoListModel.ActiveItems.Count, "준비 조건 실패 — 복원되지 않았습니다.");
            Assert.IsTrue(TodoListModel.ActiveItems[0].Completed);

            // 완료 시각이 지난 세션 값 그대로였다면 (지금 - 큰 값)이 음수라 영원히 걷히지 않는다.
            TodoListModel.SweepCompleted(0.001f);
            Assert.AreEqual(0, TodoListModel.ActiveItems.Count,
                "지난 세션의 완료 항목이 영원히 유예 상태로 굳었습니다 — 완료함으로 넘어가야 합니다.");
            Assert.AreEqual(1, TodoListModel.CompletedArchive.Count);
        }

        [Test]
        public void 구버전_v3_저장_파일은_할일_없음으로_읽힌다()
        {
            // v3에는 할일 필드가 아예 없다 — JsonUtility가 null로 채우고, 그 null은 "적어둔 할일이
            // 없다"는 정확한 사실이다(파일 전체가 무효가 되어 레벨까지 날아가면 안 된다).
            const string V3Json =
                "{\n" +
                "    \"version\": 3,\n" +
                "    \"level\": 9,\n" +
                "    \"currentXp\": 4.0,\n" +
                "    \"totalXpEarned\": 900.0,\n" +
                "    \"characterName\": \"옛동료\",\n" +
                "    \"equippedHead\": true,\n" +
                "    \"equippedEyes\": false,\n" +
                "    \"equippedNeck\": false,\n" +
                "    \"equippedShoulders\": false,\n" +
                "    \"battleWins\": 3,\n" +
                "    \"rivalWins\": 1,\n" +
                "    \"archeryShots\": 5,\n" +
                "    \"archeryBullseyes\": 2,\n" +
                "    \"companionSeconds\": 120.0,\n" +
                "    \"ragdollFalls\": 4,\n" +
                "    \"firstRunUnixSeconds\": 1788038056,\n" +
                "    \"gearPositionSaved\": true,\n" +
                "    \"gearCenterXPoints\": 300.0,\n" +
                "    \"gearCenterYPoints\": 120.0\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, V3Json);

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "구버전 파일을 읽지 못했습니다 — 사용자의 진행도가 날아갑니다.");
            Assert.AreEqual(9, CharacterProgressionModel.Level, "구버전 파일의 레벨이 복원되지 않았습니다.");
            Assert.IsTrue(UiLayoutModel.HasGearCenter, "v3의 톱니 위치가 복원되지 않았습니다.");
            Assert.AreEqual(0, TodoListModel.ActiveItems.Count, "구버전 파일에 없던 할일이 생겼습니다.");
            Assert.AreEqual(0, TodoListModel.CompletedArchive.Count);
        }

        [Test]
        public void 구버전_v1_저장_파일도_그대로_읽힌다()
        {
            const string V1Json =
                "{\n" +
                "    \"version\": 1,\n" +
                "    \"level\": 2,\n" +
                "    \"currentXp\": 1.0,\n" +
                "    \"totalXpEarned\": 30.0,\n" +
                "    \"characterName\": \"처음동료\",\n" +
                "    \"equippedHead\": false,\n" +
                "    \"equippedEyes\": false,\n" +
                "    \"equippedNeck\": false,\n" +
                "    \"equippedShoulders\": false\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, V1Json);

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "v1 파일을 읽지 못했습니다.");
            Assert.AreEqual(2, CharacterProgressionModel.Level);
            Assert.AreEqual(0, TodoListModel.ActiveItems.Count);
            Assert.IsFalse(UiLayoutModel.HasGearCenter);
        }

        [Test]
        public void 빈_목록을_저장해도_다음_로드에서_비어_있다()
        {
            // 네거티브 컨트롤 — "저장했더니 어디선가 항목이 생겨난다"가 아님을 확인한다.
            Assert.IsTrue(CharacterSaveStore.Save());
            TodoListModel.ResetForTesting();
            CharacterSaveStore.Load();

            Assert.AreEqual(0, TodoListModel.ActiveItems.Count);
            Assert.AreEqual(0, TodoListModel.CompletedArchive.Count);
        }
    }
}
