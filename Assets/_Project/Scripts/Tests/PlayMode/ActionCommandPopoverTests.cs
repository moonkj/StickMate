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

        // ==================== ⑥ 명령을 눌러도 창은 남는다 (2026-09-02) ====================

        /// <summary>
        /// ★★ 2026-09-02 사용자 신고 — "행동 메뉴에서 활쏘기 한번 누르면 메뉴가
        /// 사라져버리는데 유지되어야함". 명령 타일 실행은 창을 닫지 않아야 한다.
        ///
        /// <para>★ 이 테스트의 <b>네거티브 컨트롤 3중</b>(오늘 밤 거짓 초록 7건 중 하나가
        /// 바로 이 함수의 조기 반환이었다):</para>
        /// <list type="number">
        ///   <item>"열려 있다"만 재면 <b>클릭이 아예 당도하지 않았을 때도 초록</b>이다 — 그래서
        ///     누른 타일에 <b>접수 플래시가 실제로 켜졌음</b>을 먼저 요구한다(= 실행까지 성공했다).</item>
        ///   <item>플래시 프로브가 <b>항상 true</b>면 그것도 거짓 초록이다 — 누르지 <b>않은</b> 타일은
        ///     꺼져 있어야 하고, 누른 타일도 <see cref="ActionCommandPopover.AcceptFlashSeconds"/> 뒤에는 꺼져야 한다.</item>
        ///   <item>"닫히지 않았다"는 <b>닫기 자체가 고장나도</b> 초록이다 — 마지막에 실제 [✕] 클릭
        ///     경로로 닫아 보고, <b>차단막까지</b> 거둬졌는지 확인한다.</item>
        /// </list>
        /// </summary>
        [UnityTest]
        public IEnumerator ExecutingACommandKeepsTheWindowOpen()
        {
            yield return LoadSceneAndResolve();
            _popover.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 테스트");
            yield return new WaitForSecondsRealtime(0.3f);

            // [말 걸기]를 고른다: 상호배제 락을 잡지 않고 <b>같은 상태로 재진입</b>만 하므로,
            // 실행 뒤에도 창의 다른 값이 거의 변하지 않는다 — 즉 "접수 플래시"가 유일한 피드백인
            // 가장 까다로운 경우다. 여기가 통하면 나머지는 상태 변화로 더 크게 말한다.
            const ActionCommandPopover.Command pressed = ActionCommandPopover.Command.SayNow;
            const ActionCommandPopover.Command untouched = ActionCommandPopover.Command.Graffiti;

            float deadline = Time.realtimeSinceStartup + 3f;   // 벽시계 예산(프레임 수 금지).
            while (Time.realtimeSinceStartup < deadline && !_popover.GetAvailability(pressed).IsReady)
            {
                yield return null;
            }
            Assume.That(_popover.GetAvailability(pressed).IsReady, Is.True,
                "[말 걸기]가 3초 안에 실행 가능해지지 않았습니다 — 캐릭터가 Idle/Walk가 아닌 상태에 " +
                "머무르고 있어 이 테스트의 전제(실행 가능한 타일을 누른다)가 성립하지 않습니다.");

            Assert.IsFalse(_popover.IsCommandAcceptFlashing(pressed),
                "누르기도 전에 접수 플래시가 켜져 있습니다 — 이 프로브는 항상 true라 아무것도 증명하지 못합니다.");

            // 실제 클릭 경로 그대로 먹인다(좌표를 손으로 적지 않고 타일 사각형에서 얻는다).
            // ★ 아래 세 단언은 <b>yield 없이</b> 즉시 잰다 — FeedClick은 동기 경로이고, 한 프레임이라도
            //   흘리면 배치모드의 큰 dt가 접수 플래시를 이미 태워버려 "안 켜졌다"로 오판될 수 있다
            //   (CLAUDE.md: 이 저장소의 배치모드 PlayMode는 프레임 예산이 신뢰할 수 없다).
            _popover.FeedClickForTests(_popover.CommandScreenRect(pressed).center);

            // ① 클릭이 실제로 당도해 <b>실행까지 성공</b>했다.
            Assert.IsTrue(_popover.IsCommandAcceptFlashing(pressed),
                $"[{pressed}] 타일을 실제 클릭 경로로 눌렀는데 접수 플래시가 켜지지 않았습니다 — " +
                "클릭이 아예 당도하지 않았거나 실행이 거절됐습니다. 그 상태에서의 \"창이 열려 있다\"는 " +
                "거짓 초록입니다.");
            Assert.IsFalse(_popover.IsCommandRejectShaking(pressed),
                $"[{pressed}]가 실행됐다면서 동시에 거절 흔들림도 동작했습니다 — 둘은 배타적이어야 합니다.");

            // ② 프로브가 "항상 true"가 아니다 — 누르지 않은 칸은 꺼져 있다.
            Assert.IsFalse(_popover.IsCommandAcceptFlashing(untouched),
                $"누르지 않은 [{untouched}] 타일까지 접수 플래시 중입니다 — 플래시가 특정 타일을 " +
                "가리키지 못하면 \"눌렸다\" 신호로 쓸 수 없습니다.");

            // ★ 핵심 단언 — 닫기 예약이 <b>서지조차 않았다</b>.
            //   IsOpen만 재면 관측 시점이 지연(SuccessCloseDelaySeconds)보다 이르기만 해도 초록이 된다.
            Assert.IsFalse(_popover.IsCloseScheduled,
                "명령 실행이 여전히 닫기를 예약했습니다 — 2026-09-02 사용자 지시는 \"메뉴가 유지되어야함\"입니다.");

            // 지연 예산의 3배를 벽시계로 기다린다(프레임 수 기준 대기 금지 — CLAUDE.md).
            yield return new WaitForSecondsRealtime(
                ActionCommandPopover.SuccessCloseDelaySeconds * 3f + ActionCommandPopover.AcceptFlashSeconds + 0.2f);

            Assert.IsTrue(_popover.IsOpen,
                "명령을 눌렀다고 행동 명령창이 닫혔습니다 — 2026-09-02 사용자 지시 위반입니다.");
            Assert.IsTrue(_popover.IsCanvasActive,
                "IsOpen은 true인데 캔버스가 꺼져 있습니다 — 플래그만 남고 화면에는 아무것도 없습니다.");
            Assert.IsFalse(_popover.IsCommandAcceptFlashing(pressed),
                $"접수 플래시가 {ActionCommandPopover.AcceptFlashSeconds:F2}초가 지나도 꺼지지 않았습니다 — " +
                "타일 바닥이 액센트로 영영 물들어 있게 됩니다.");

            // ③ 네거티브 컨트롤 — 닫기 경로 자체는 여전히 살아 있다.
            //   이게 죽어 있었다면 위의 "열려 있다" 단언은 공짜로 초록이었을 것이다.
            _popover.FeedClickForTests(_popover.CloseButtonScreenRectForTests.center);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsFalse(_popover.IsOpen,
                "[✕]를 실제 클릭 경로로 눌렀는데도 창이 닫히지 않습니다 — 이 경우 위의 \"유지된다\" " +
                "단언은 아무것도 증명하지 못합니다(거짓 초록).");
            Assert.IsFalse(_popover.IsClickBlockerEnabled,
                "창이 닫혔는데 클릭관통 차단막이 남았습니다(비침해 원칙 2).");
        }

        /// <summary>
        /// ★ "활쏘기 도중에 활쏘기를 또 누르면?" — 창을 유지하기로 한 이상 이 상황이
        /// <b>일상이 된다</b>(종전에는 창이 이미 닫혀 있어 발생하지 않던 경로다).
        ///
        /// <para>상호배제 락을 테스트가 직접 잡아 <b>결정론적</b>으로 재현한다 — 진짜 활쏘기를
        /// 돌리면 과녁 자리 추첨에 의존해 불안정해진다. 이유 문구는 <b>프로덕션 함수에서
        /// 가져온다</b>(CLAUDE.md: 테스트에 프로덕션 상수를 숫자/문자로 베끼지 않는다).</para>
        /// </summary>
        [UnityTest]
        public IEnumerator PressingACommandWhileBusyExplainsWhyAndStillKeepsTheWindowOpen()
        {
            yield return LoadSceneAndResolve();
            _popover.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 테스트");
            yield return new WaitForSecondsRealtime(0.3f);

            Assume.That(SpectacleEventLock.IsActive, Is.False, "테스트 시작 시점에 이미 스펙터클 락이 잡혀 있습니다.");
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Archery, this),
                "테스트가 상호배제 락을 잡지 못했습니다.");
            _lockHeld = true;

            yield return new WaitForSecondsRealtime(0.4f);   // 0.25초 안전 폴링이 한 번은 돌게 한다.

            const ActionCommandPopover.Command target = ActionCommandPopover.Command.Archery;
            string expected = StickMateDisplayNames.BusyText(SpectacleEventKind.Archery);

            Assert.IsFalse(_popover.IsCommandReady(target),
                "스펙터클 락이 잡혀 있는데 [활쏘기] 타일이 여전히 실행 가능으로 그려져 있습니다 — " +
                "창을 유지하기로 한 이상 연출 도중의 갱신이 멈추면 그것이 곧 \"회색인데 눌리는 버튼\"입니다.");
            Assert.AreEqual(expected, _popover.CommandReason(target),
                "연출 도중의 불가 사유가 프로덕션 판정과 다릅니다(36-7 진실 한 벌).");

            _popover.FeedClickForTests(_popover.CommandScreenRect(target).center);
            yield return null;

            Assert.IsTrue(_popover.IsCommandRejectShaking(target),
                "연출 도중에 같은 명령을 다시 눌렀는데 거절 흔들림이 없습니다 — 조용한 실패는 금지입니다(36-7).");
            Assert.IsFalse(_popover.IsCommandAcceptFlashing(target),
                "거절됐는데 접수 플래시가 켜졌습니다 — 사용자에게 된 것처럼 보입니다.");
            Assert.IsFalse(_popover.IsCloseScheduled, "거절이 창 닫기를 예약했습니다.");
            Assert.IsTrue(_popover.IsOpen, "거절 후 창이 닫히면 이유를 읽을 시간이 없습니다.");
            Assert.AreEqual(expected, _popover.CommandReason(target),
                "거절 문구가 프로덕션 판정과 다릅니다.");

            _popover.Close("테스트 종료");
            yield return null;
        }

        private bool _lockHeld;

        /// <summary>테스트가 잡은 상호배제 락은 <b>단언이 중간에 터져도</b> 반드시 풀린다 —
        /// 남기면 뒤따라 도는 모든 테스트가 "전부 불가"로 보여 원인 불명 연쇄 실패가 된다.</summary>
        [TearDown]
        public void ReleaseSpectacleLock()
        {
            if (!_lockHeld) return;
            _lockHeld = false;
            SpectacleEventLock.Release(this);
        }
    }
}
