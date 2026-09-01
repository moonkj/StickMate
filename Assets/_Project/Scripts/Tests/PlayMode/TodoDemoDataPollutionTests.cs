using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 2026-08-31 버그 회귀 — <b>할일 알림 데모(⌃⌥⌘J)가 사용자의 진짜 목록을 오염시켰다.</b>
    ///
    /// ============================================================================
    /// 무엇이 버그였나 (ux-designer가 36-1의 12행에서 지목)
    /// ============================================================================
    /// <see cref="TodoReminderDirector.ForceTriggerNow"/>는 목록이 비어 있으면 데모 할일 3건
    /// ("보고서 초안 쓰기"/"장보기"/"세탁물 찾기")을 <see cref="TodoListModel.Add"/>로 <b>실제 목록에
    /// 넣고 저장 파일에까지 남겼다</b>. 그 시절의 논거는 "Add 호출자가 프로젝트 전체에 0건이라 투두
    /// 기능이 도달 불가능하다"였는데, 부채꼴 ③ <see cref="TodoBoardPopover"/>에 입력칸이 생기면서
    /// <b>그 전제가 사실이 아니게 됐다</b>. 남은 것은 사용자가 적지 않은 항목이 자기 목록에 나타나는
    /// 것뿐이며, 이는 게이트로 숨기고 말고와 무관한 <b>데이터 오염</b>이다(CLAUDE.md 원칙 1·3).
    ///
    /// <b>왜 개발 게이트로 숨기는 것만으로는 부족한가</b>: 게이트는 에디터와 개발 빌드에서 언제나
    /// 열려 있다. 즉 우리 팀이 이 앱을 쓰는 내내 그 오염은 계속 일어난다. 데모 경로가 진짜 저장소에
    /// 쓰는 것 자체가 잘못이므로 <b>쓰기를 지웠다</b>.
    /// </summary>
    public sealed class TodoDemoDataPollutionTests
    {
        private TodoReminderDirector _director;
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
            TodoListModel.ResetForTesting();
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _director = Object.FindFirstObjectByType<TodoReminderDirector>();
            Assert.IsNotNull(_director, "씬에 TodoReminderDirector가 없습니다.");
            yield return null;
        }

        /// <summary>
        /// ★ 핵심 회귀 — 목록이 <b>비어 있는 상태</b>에서 데모 경로를 불러도 목록은 여전히 비어 있어야
        /// 한다. 이것이 실패하면 사용자가 앱을 켤 때마다 자기가 적지 않은 할일 3건을 보게 된다.
        /// </summary>
        [UnityTest]
        public IEnumerator ForcedReminderNeverWritesFakeTodosIntoTheRealList()
        {
            yield return LoadSceneAndResolve();

            TodoListModel.ResetForTesting();
            yield return null;
            Assume.That(TodoListModel.UncompletedCount, Is.EqualTo(0), "사전 조건: 목록이 비어 있어야 합니다.");

            _director.ForceTriggerNow("PlayMode 오염 회귀 테스트");
            yield return null;
            yield return null;

            Assert.AreEqual(0, TodoListModel.UncompletedCount,
                $"데모 경로가 할일 {TodoListModel.UncompletedCount}건을 사용자의 진짜 목록에 넣었습니다 — " +
                "데모는 진짜 데이터에 쓰지 않습니다(2026-08-31 수정). 할일이 들어오는 유일한 경로는 " +
                "부채꼴 ③ [오늘 할일]의 입력칸입니다.");
            Assert.AreEqual(0, TodoListModel.ActiveItems.Count,
                "미완료는 0인데 활성 목록에 항목이 남아 있습니다(완료 처리된 가짜 항목).");
        }

        /// <summary>
        /// 반복 호출로도 새지 않는다 — "한 번은 괜찮은데 여러 번 누르면 쌓인다"는 형태의 재발을 막는다.
        /// </summary>
        [UnityTest]
        public IEnumerator RepeatedForcedRemindersStillLeaveTheListEmpty()
        {
            yield return LoadSceneAndResolve();

            TodoListModel.ResetForTesting();
            yield return null;

            for (int i = 0; i < 5; i++)
            {
                _director.ForceTriggerNow($"PlayMode 반복 {i}");
                yield return null;
            }

            Assert.AreEqual(0, TodoListModel.UncompletedCount,
                $"데모 경로를 5번 불렀더니 할일 {TodoListModel.UncompletedCount}건이 쌓였습니다.");
        }

        /// <summary>
        /// 네거티브 대조 — 진짜 사용자 경로(<see cref="TodoListModel.Add"/>, 부채꼴 ③ 입력칸이 부르는
        /// 바로 그 함수)는 <b>여전히 정상 동작</b>해야 한다. 위 두 테스트가 "쓰기 경로를 통째로 부숴서"
        /// 통과하는 것이 아님을 보장한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRealUserEntryPathStillWorks()
        {
            yield return LoadSceneAndResolve();

            TodoListModel.ResetForTesting();
            yield return null;

            TodoListModel.Add("사용자가 직접 적은 할일", 15);
            Assert.AreEqual(1, TodoListModel.UncompletedCount,
                "사용자 입력 경로(TodoListModel.Add)가 동작하지 않습니다 — 오염을 막으려다 기능을 껐습니다.");
        }

        /// <summary>
        /// 강조할 할일이 없으면 리마인더는 <b>거절되고 이유를 말한다</b>(조용한 no-op이 아니다).
        /// 36-7의 판정 구조를 이 Director도 똑같이 따르는지 확인한다.
        /// </summary>
        [UnityTest]
        public IEnumerator EmptyListIsReportedAsAReasonNotASilentNoOp()
        {
            yield return LoadSceneAndResolve();

            TodoListModel.ResetForTesting();
            yield return null;

            CommandAvailability availability = _director.GetAvailability();
            Assert.IsFalse(availability.IsReady, "할일이 0건인데 리마인더가 가능하다고 답했습니다.");
            Assert.AreEqual(TodoReminderDirector.NoTodoReason, availability.Reason,
                "할일이 없다는 이유가 화면에 쓸 수 있는 문장으로 나오지 않았습니다.");

            Assert.IsFalse(_director.ForceTriggerNow("PlayMode 거절 확인"),
                "불가 판정인데 ForceTriggerNow가 성공을 보고했습니다 — 판정과 실행이 두 벌입니다(36-7).");
        }
    }
}
