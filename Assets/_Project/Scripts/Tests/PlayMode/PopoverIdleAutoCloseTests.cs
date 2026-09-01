using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 팝오버는 <b>결국 스스로 닫힌다</b> — 2026-09-01 페르소나(재현) J5, 절대 불변 원칙 2.
    ///
    /// ============================================================================
    /// 무엇이 새고 있었나
    /// ============================================================================
    /// 부채꼴의 6초 자동 접힘은 <c>AnyPopoverOpen()</c>이면 타이머를 리셋한다(읽고 있는 창을 시간으로
    /// 닫지 않겠다는, 그 자체로는 옳은 규칙). 그런데 <see cref="PopoverPanel"/>에는 자기 몫의 자동
    /// 닫힘이 <b>없었다</b>. 그래서 "톱니 → [오늘 할일] → 자리 비움"이면 부채꼴과 팝오버, 그리고
    /// <b>팝오버의 클릭관통 차단막</b>이 밤새 남았다 — 바탕화면 한 조각의 클릭관통이 밤새 해제된 채
    /// 남는 것은 이 앱이 스스로 금지한 침해다.
    ///
    /// ============================================================================
    /// 왜 커서를 주입하는가
    /// ============================================================================
    /// 무입력 판정은 <b>실제 OS 커서</b>를 본다. PlayMode는 진짜 커서를 원하는 자리에 붙잡아 둘 수
    /// 없어서(테스트 도중 사람이 마우스를 건드리면 시계가 리셋된다) 관측 <b>소스</b>만 주입한다 —
    /// 판정·닫기·차단막 해제는 전부 제품 경로 그대로다.
    /// </summary>
    public sealed class PopoverIdleAutoCloseTests
    {
        private const string LogPrefix = "[팝오버무입력-TEST]";

        /// <summary>테스트용 임계. 3분을 진짜로 기다리는 테스트는 만들지 않는다.</summary>
        private const float TestIdleSeconds = 0.5f;

        private TodoBoardPopover _popover;

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _popover = Object.FindFirstObjectByType<TodoBoardPopover>();
            Assert.IsNotNull(_popover, $"{LogPrefix} 씬에 TodoBoardPopover가 없습니다.");
            PopoverPanel.SetIdleAutoCloseSecondsForTests(TestIdleSeconds);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            PopoverPanel.ResetIdleAutoCloseSecondsForTests();
            if (_popover != null)
            {
                _popover.ClearIdleCursorForTests();
                if (_popover.IsOpen) _popover.Close("테스트 정리");
            }
            _popover = null;
            yield return null;
        }

        private void OpenAtScreenCenter()
        {
            var anchor = new Rect(Screen.width * 0.5f - 22f, Screen.height * 0.5f - 22f, 44f, 44f);
            _popover.Open(anchor, "테스트");
            Assert.IsTrue(_popover.IsOpen, $"{LogPrefix} 팝오버가 열리지 않았습니다.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AbandonedPopoverClosesItselfAndReleasesTheClickBlocker()
        {
            yield return LoadScene();
            OpenAtScreenCenter();

            // 커서를 한 자리에 못 박는다 = 자리 비움.
            _popover.FeedIdleCursorForTests(new Vector2(12f, 12f));

            yield return new WaitForSecondsRealtime(TestIdleSeconds * 0.4f);
            Assert.IsTrue(_popover.IsOpen,
                $"{LogPrefix} 임계({TestIdleSeconds:F2}초)의 절반도 안 지났는데 닫혔습니다 — " +
                "읽는 도중에 창이 사라지는 것은 편의가 아니라 사고입니다.");

            yield return new WaitForSecondsRealtime(TestIdleSeconds + 0.6f);

            Assert.IsFalse(_popover.IsOpen,
                $"{LogPrefix} 무입력 {TestIdleSeconds:F2}초가 지났는데 팝오버가 그대로 열려 있습니다 — " +
                "부채꼴의 자동 접힘은 팝오버가 떠 있는 동안 무력화되므로, 이 창이 스스로 닫지 않으면 " +
                "밤새 남습니다(J5).");
            Assert.IsFalse(_popover.IsClickBlockerEnabled,
                $"{LogPrefix} 팝오버는 닫혔는데 클릭관통 차단막이 살아 있습니다 — 그 화면 영역의 " +
                "클릭관통이 영영 해제된 채 남습니다(비침해 원칙 2).");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator MovingTheCursorKeepsThePopoverOpen()
        {
            yield return LoadScene();
            OpenAtScreenCenter();

            // 임계의 세 배가 넘는 시간 동안, 임계보다 짧은 간격으로 커서를 계속 옮긴다.
            for (int i = 0; i < 10; i++)
            {
                _popover.FeedIdleCursorForTests(new Vector2(20f + i * 17f, 20f));
                yield return new WaitForSecondsRealtime(TestIdleSeconds * 0.3f);
            }

            Assert.IsTrue(_popover.IsOpen,
                $"{LogPrefix} 커서가 계속 움직이는 동안(총 {TestIdleSeconds * 3f:F1}초) 팝오버가 닫혔습니다 — " +
                "무입력 자동 닫힘이 <b>사용 중</b>인 창까지 닫고 있습니다.");
            Assert.Less(_popover.IdleSecondsForTests, TestIdleSeconds,
                $"{LogPrefix} 커서가 움직였는데 무입력 시계가 {_popover.IdleSecondsForTests:F2}초까지 쌓였습니다.");
        }
    }
}
