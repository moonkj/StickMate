using System;
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
    /// ★ 부채꼴 ④ [행동] → <b>행동 명령창</b> 회귀 — docs/UX_FLOW.md <b>36-6/36-7/36-10</b>.
    /// 2026-08-31 사용자 지시("기어아이콘에 메뉴하나 추가해서 행동들은 거기서 클릭하면 창 하나가 떠서
    /// 행동 명령 내릴수 있게")의 산출물을 잠근다.
    ///
    /// 무엇을 절대 조건으로 두는가:
    ///  ① <b>씬에 실제로 배선돼 있다</b> — 33-9 #10 / 34-9 #10 / 36-13 #11이 세 번 연속 경고한
    ///     "신규 컴포넌트가 프리팹에 없어 런타임 부재" Blocker의 재발 방지선이다.
    ///  ② <b>회색 처리와 실제 판정이 같은 함수 하나에서 나온다</b>(36-7 절대 규칙). 진실 두 벌 금지.
    ///  ③ <b>(다) 개발 전용 명령이 하나도 없다</b>(36-2). 새면 이 창은 디버그 패널이 된다.
    ///  ④ <b>마우스만으로 도달하는 종료 경로가 존재하고 2단 확인을 거친다</b>(36-10).
    ///  ⑤ <b>[돌아와!]는 가출 중에만 보인다</b> — 없는 상태를 사용자에게 가르치지 않는다(17절).
    /// </summary>
    public sealed class ActionCommandPopoverTests
    {
        private ActionCommandPopover _popover;
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
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var found = UnityEngine.Object.FindObjectsByType<ActionCommandPopover>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length,
                $"씬의 ActionCommandPopover가 {found.Length}개입니다 — 1개여야 합니다. " +
                "0개라면 Assets/Editor/SceneBootstrapper.cs가 이 컴포넌트를 붙이지 않은 것이고, " +
                "그러면 부채꼴 ④[행동]을 눌러도 경고만 남고 창이 뜨지 않습니다(36-13 #11).");
            _popover = found[0];
            yield return null;
        }

        // ==================== ① 배선 ====================

        /// <summary>부채꼴이 <see cref="GetComponent"/>로 찾으므로 <b>같은 GameObject</b>에 있어야 한다.</summary>
        [UnityTest]
        public IEnumerator PopoverIsWiredOnTheSameObjectAsTheFan()
        {
            yield return LoadSceneAndResolve();

            var fan = _popover.GetComponent<GearRadialMenuWidget>();
            Assert.IsNotNull(fan,
                "행동 명령창이 부채꼴과 다른 GameObject에 있습니다 — GearRadialMenuWidget이 GetComponent로 " +
                "찾으므로 ④ 버튼이 아무 일도 하지 않게 됩니다.");

            Assert.IsFalse(_popover.IsOpen, "아무도 부르지 않았는데 행동 명령창이 열려 있습니다.");
            Assert.IsFalse(_popover.IsClickBlockerEnabled,
                "창이 닫혀 있는데 클릭관통 차단막이 켜져 있습니다 — 그 화면 영역의 클릭관통이 이유 없이 " +
                "해제된 채 남습니다(비침해 원칙 2).");
        }

        // ==================== ② 진실 한 벌 ====================

        /// <summary>
        /// ★★ 36-7 절대 규칙 — 타일이 회색인지(<c>IsCommandReady</c>)와 실제로 실행 가능한지
        /// (<c>GetAvailability</c>)가 <b>언제나</b> 같아야 한다. 이 둘이 갈라지는 순간 "눌러도 아무 일
        /// 없는 버튼" 또는 "회색인데 눌리는 버튼"이 생기고, 둘 다 원칙 1 위반이다.
        /// </summary>
        [UnityTest]
        public IEnumerator GreyOutAlwaysMatchesTheRealAvailabilityJudgement()
        {
            yield return LoadSceneAndResolve();
            _popover.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 테스트");
            yield return null;
            yield return new WaitForSecondsRealtime(0.3f);   // 0.25초 안전 폴링이 한 번은 돌게 한다.

            foreach (ActionCommandPopover.Command command in
                (ActionCommandPopover.Command[])Enum.GetValues(typeof(ActionCommandPopover.Command)))
            {
                CommandAvailability truth = _popover.GetAvailability(command);
                Assert.AreEqual(truth.IsReady, _popover.IsCommandReady(command),
                    $"[{command}] 타일의 회색 처리({_popover.IsCommandReady(command)})와 실제 판정" +
                    $"({truth.IsReady})이 다릅니다 — 진실이 두 벌입니다(36-7 절대 규칙).");

                if (truth.IsReady) continue;
                Assert.IsNotEmpty(truth.Reason,
                    $"[{command}]가 불가인데 이유가 비어 있습니다 — 조용한 실패는 금지입니다(36-7).");
            }

            _popover.Close("테스트 종료");
            yield return null;
        }

        /// <summary>모든 명령 타일이 화면 안에서 실제 크기를 갖는다(0×0이면 영원히 못 누른다).</summary>
        [UnityTest]
        public IEnumerator EveryCommandTileHasAClickableRect()
        {
            yield return LoadSceneAndResolve();
            _popover.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 테스트");
            yield return new WaitForSecondsRealtime(0.25f);

            Rect panel = _popover.PanelScreenRect;
            foreach (ActionCommandPopover.Command command in
                (ActionCommandPopover.Command[])Enum.GetValues(typeof(ActionCommandPopover.Command)))
            {
                Rect tile = _popover.CommandScreenRect(command);
                Assert.Greater(tile.width, 1f, $"[{command}] 타일의 폭이 0입니다 — 누를 수 없습니다.");
                Assert.Greater(tile.height, 1f, $"[{command}] 타일의 높이가 0입니다 — 누를 수 없습니다.");
                Assert.IsTrue(panel.Overlaps(tile),
                    $"[{command}] 타일이 패널 밖에 있습니다(타일 {tile}, 패널 {panel}).");
            }

            _popover.Close("테스트 종료");
            yield return null;
        }

        // ==================== ③ (다) 격리 ====================

        /// <summary>
        /// ★ 36-2 격리 규칙 1 — (다) 5개는 행동 명령창에 <b>UI를 만들지 않는다. 예외 없음.</b>
        /// 이름으로 확인하는 이유: 누군가 "편의상" 타일을 하나 더 붙이는 것이 이 창을 디버그 패널로
        /// 되돌리는 가장 흔한 경로이고, 그건 코드 리뷰보다 테스트가 먼저 잡아야 한다.
        /// </summary>
        [Test]
        public void ActionWindowExposesNoDeveloperOnlyCommand()
        {
            string[] names = Enum.GetNames(typeof(ActionCommandPopover.Command));
            Assert.AreEqual(6, names.Length,
                $"명령 개수가 {names.Length}개입니다 — 36-1이 (가)로 분류한 것은 6개(말 걸기/활쏘기/격파/" +
                "그라피티/창 도둑/창 부수기)이고 [돌아와!]는 타일이 아니라 헤더 칩입니다.");
            Assert.AreEqual(6, ActionCommandPopover.CommandCount);

            // (다) 개발 전용 5개 + 가출 발동은 어떤 이름으로도 여기 들어오면 안 된다.
            string[] forbidden =
            {
                "Hardware", "Stress", "Todo", "Focus", "Diagnostics", "Runaway", "Demo", "Debug",
            };
            foreach (string name in names)
            {
                foreach (string bad in forbidden)
                {
                    Assert.IsFalse(name.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0,
                        $"명령 [{name}]이 (다) 개발 전용 분류로 보입니다 — 36-2 격리 규칙 1은 " +
                        "\"행동 명령창·설정창 어디에도 UI를 만들지 않는다. 예외 없음\"입니다.");
                }
            }
        }

        // ==================== ④ 종료 경로 ====================

        /// <summary>
        /// ★ 36-10 — 우클릭 메뉴가 사라진 뒤 <b>마우스만으로 도달하는 유일한 종료 경로</b>다.
        /// 1차 클릭은 종료하지 않고 라벨만 바꾼다(<see cref="TodoBoardPopover"/>의 삭제 확인과 같은 패턴).
        /// </summary>
        [UnityTest]
        public IEnumerator QuitButtonExistsAndRequiresTwoSteps()
        {
            yield return LoadSceneAndResolve();
            _popover.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 테스트");
            yield return new WaitForSecondsRealtime(0.25f);

            Rect quit = _popover.QuitButtonScreenRect;
            Assert.Greater(quit.width, 1f,
                "[✕ 앱 종료] 버튼이 없습니다 — 우클릭 메뉴가 폐지된 지금, 전역 단축키가 동작하지 않는 " +
                "환경에서는 앱을 끌 방법이 사라집니다(원칙 2·4 위반).");
            Assert.IsFalse(_popover.IsQuitArmed, "열자마자 종료 확인이 켜져 있습니다.");

            // 명령 타일에서 충분히 떨어져 있어야 한다(오조준으로 앱이 꺼지면 안 된다).
            foreach (ActionCommandPopover.Command command in
                (ActionCommandPopover.Command[])Enum.GetValues(typeof(ActionCommandPopover.Command)))
            {
                Assert.IsFalse(_popover.CommandScreenRect(command).Overlaps(quit),
                    $"[✕ 앱 종료]가 [{command}] 타일과 겹칩니다 — 오조준으로 앱이 꺼질 수 있습니다.");
            }

            // 1차 클릭: 실제 클릭 경로 그대로 먹인다. 여기서 앱이 꺼지면 테스트 러너가 죽으므로,
            // "꺼지지 않았다"는 것 자체가 이 단언의 통과 조건이기도 하다.
            _popover.FeedClickForTests(quit.center);
            yield return null;
            Assert.IsTrue(_popover.IsQuitArmed,
                "1차 클릭에도 확인 상태가 되지 않았습니다 — 2단 확인이 동작하지 않습니다.");
            Assert.IsTrue(_popover.IsOpen, "1차 클릭에 창이 닫혔습니다.");

            // 3초가 지나면 조용히 취소된다(열어두고 잊어버려도 다음 클릭이 종료가 되지 않게).
            yield return new WaitForSecondsRealtime(ActionCommandPopover.QuitConfirmSeconds + 0.3f);
            Assert.IsFalse(_popover.IsQuitArmed,
                $"{ActionCommandPopover.QuitConfirmSeconds:F0}초가 지났는데 종료 확인이 그대로입니다 — " +
                "나중에 무심코 누른 클릭이 앱을 꺼버립니다.");

            _popover.Close("테스트 종료");
            yield return null;
        }

        /// <summary>확인 유지 시간은 <see cref="TodoBoardPopover"/>와 <b>같은 3초</b>여야 한다 —
        /// 앱 안에서 "되돌릴 수 없는 행동"의 확인 방식이 두 벌이 되지 않게(36-10).</summary>
        [Test]
        public void QuitConfirmWindowMatchesTheTodoDeleteConfirmPattern()
        {
            Assert.AreEqual(3f, ActionCommandPopover.QuitConfirmSeconds, 0.001f,
                "종료 2단 확인 시간이 3초가 아닙니다 — TodoBoardPopover.DeleteConfirmSeconds와 같아야 합니다.");
        }

        // ==================== ⑤ [돌아와!] ====================

        /// <summary>가출 중이 아니면 칩이 <b>없다</b> — 평소엔 늘 비활성인 칸을 남겨 없는 상태를
        /// 사용자에게 가르치지 않는다(36-6 / 17절 "빈 상태를 굳이 보여주지 않는다").</summary>
        [UnityTest]
        public IEnumerator RecallChipIsHiddenWhileTheCharacterIsHome()
        {
            yield return LoadSceneAndResolve();
            _popover.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 테스트");
            yield return new WaitForSecondsRealtime(0.3f);

            var runaway = UnityEngine.Object.FindFirstObjectByType<RunawayDirector>();
            Assert.IsNotNull(runaway, "씬에 RunawayDirector가 없습니다.");
            Assume.That(runaway.IsRunawayActive, Is.False, "테스트 시작 시점에 이미 가출 중입니다.");

            Assert.IsFalse(_popover.IsRecallChipVisible,
                "가출 중이 아닌데 [돌아와!] 칩이 보입니다 — 없는 상태를 사용자에게 가르치게 됩니다.");
            Assert.AreNotEqual("지금 가출 중이에요", _popover.StatusCaption,
                "가출 중이 아닌데 헤더가 가출 중이라고 말합니다(원칙 1).");

            _popover.Close("테스트 종료");
            yield return null;
        }

        /// <summary>헤더 캡션은 <b>실제 값에서만</b> 파생한다 — 셋 중 하나여야 하고 비어 있으면 안 된다.</summary>
        [UnityTest]
        public IEnumerator StatusCaptionIsAlwaysDerivedFromRealState()
        {
            yield return LoadSceneAndResolve();
            _popover.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 테스트");
            yield return new WaitForSecondsRealtime(0.3f);

            string caption = _popover.StatusCaption;
            Assert.IsNotEmpty(caption, "헤더 상태 캡션이 비어 있습니다 — 상시 존재해야 레이아웃 점프가 없습니다(36-6).");

            int readyCount = 0;
            foreach (ActionCommandPopover.Command command in
                (ActionCommandPopover.Command[])Enum.GetValues(typeof(ActionCommandPopover.Command)))
            {
                if (_popover.GetAvailability(command).IsReady) readyCount++;
            }

            if (readyCount > 0)
            {
                Assert.AreEqual("지금 시킬 수 있어요", caption,
                    $"실행 가능한 명령이 {readyCount}개인데 헤더가 다르게 말합니다(원칙 1).");
            }
            else
            {
                Assert.AreEqual("지금은 다른 일 하는 중이에요", caption,
                    "전부 불가인데 헤더가 시킬 수 있다고 말합니다(원칙 1).");
            }

            _popover.Close("테스트 종료");
            yield return null;
        }
    }
}
