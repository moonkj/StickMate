using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ <b>닫는 법을 화면이 말한다</b> — 2026-09-02 (docs/UX_FLOW.md 41-3).
    ///
    /// <para>실측(결정적): 정보창을 연 상태에서 <c>Cmd+W</c>를 누르면 <b>정보창은 그대로이고 뒤에 있던
    /// Finder 창이 닫혔다</b>. 저장 안 한 문서였으면 진짜 사고다. 원인은 고칠 수 있는 종류가 아니다 —
    /// <c>CGWindowList</c> 실측으로 우리 OS 창은 <c>layer=101</c> 전체화면 <b>1장뿐</b>이고 정보창·설정창·
    /// 팝오버는 그 안에 그려진 <b>그림</b>이라 키보드 포커스를 받지 못한다. 키를 가로채는 방향은
    /// 검토하지 않는다(포커스를 받으면 클릭관통이 깨져 원칙 2가 무너진다).</para>
    ///
    /// <para>그래서 처방은 <b>문패</b>다: <c>[✕]</c> 바로 왼쪽에 같은 문장 하나. 문장의 <b>'도'</b>가
    /// <c>[✕]</c>를 전제하므로 한 문장이 두 경로를 다 가르친다.</para>
    ///
    /// ============================================================================
    /// ★ 이 파일이 함께 잠그는 <b>설계 오류 정정</b>
    /// ============================================================================
    /// 41-3 ③은 "팝오버 3종 중 가장 좁은 것"을 <b>480(행동창)</b>으로 적었지만 사실이 아니다 —
    /// 실제 최소는 <see cref="FocusSessionPopover"/> <b>244pt</b>, 다음이
    /// <see cref="TodoBoardPopover"/> <b>300pt</b>다. 그래서 41-3 ④가 "실제로는 발생하지 않는다"고 적어
    /// 둔 예외(<i>좁아서 힌트가 제목과 겹치면 힌트를 먼저 지운다</i>)가 <b>실제로 발생한다</b>.
    /// 아래 ③이 그 사실을 <b>단언으로</b> 붙잡아 둔다 — 동시에 "폭 판정이 진짜로 동작한다"는
    /// 네거티브 컨트롤이기도 하다(어떤 폭에서도 힌트가 붙는다면 ①의 통과는 아무 조건도 아니다).
    /// </summary>
    public sealed class SurfaceCloseHintTests
    {
        private const string LogPrefix = "[닫기힌트-TEST]";

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        // ==================== ① 행동 명령 팝오버(480) — 힌트가 [✕] 바로 왼쪽에 있다 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator WideActionPopoverShowsTheHintImmediatelyLeftOfTheCloseButton()
        {
            yield return LoadScene();

            var popover = Object.FindFirstObjectByType<ActionCommandPopover>();
            Assert.IsNotNull(popover, $"{LogPrefix} 씬에 ActionCommandPopover가 없습니다.");

            popover.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 테스트");
            yield return null;
            yield return null;

            Text hint = popover.CloseHintTextForTests;
            Assert.IsNotNull(hint,
                $"{LogPrefix} 480pt 팝오버에 닫기 힌트가 없습니다 — 이 앱은 Esc도 Cmd+W도 받지 못하고, " +
                "Cmd+W는 뒤에 있던 남의 창을 닫습니다. 닫는 법은 이미 동작하므로 화면이 말하기만 하면 됩니다.");
            Assert.AreEqual(UiChrome.CloseHintText, hint.text,
                $"{LogPrefix} 세 표면이 같은 문장을 써야 합니다 — 문구가 갈리면 사용자는 다른 규칙인가를 의심합니다.");

            Rect hintRect = PopoverPanel.ScreenRectOf(hint.rectTransform);
            Rect closeRect = popover.CloseButtonScreenRectForTests;
            Assert.Greater(closeRect.width, 1f, $"{LogPrefix} [✕] 사각형이 비었습니다.");

            Assert.LessOrEqual(hintRect.xMax, closeRect.xMin + 0.5f,
                $"{LogPrefix} 힌트({hintRect})가 [✕]({closeRect})를 침범했습니다 — 글자는 자기가 설명하는 " +
                "버튼을 <b>가리켜야</b> 하지 덮으면 안 됩니다.");

            Text title = popover.TitleTextForTests;
            Assert.IsNotNull(title, $"{LogPrefix} 제목 글자를 찾지 못했습니다.");
            Rect titleRect = PopoverPanel.ScreenRectOf(title.rectTransform);
            Assert.LessOrEqual(titleRect.xMax, hintRect.xMin + 0.5f,
                $"{LogPrefix} 제목 상자({titleRect})와 힌트 상자({hintRect})가 겹칩니다 — 두 글자가 " +
                "포개지면 둘 다 못 읽습니다(41-3 ④: 그럴 땐 힌트를 먼저 지운다).");

            popover.Close("테스트 정리");
        }

        // ==================== ② 정보창(880) ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator InfoWindowTitleBarSaysHowToCloseIt()
        {
            yield return LoadScene();

            var info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");

            info.Open("PlayMode 테스트");
            yield return null;
            yield return null;

            bool found = false;
            foreach (Text t in info.GetComponentsInChildren<Text>(true))
            {
                if (t.text == UiChrome.CloseHintText) { found = true; break; }
            }

            // 정보창 캔버스는 씬 루트에 있으므로 이름으로도 한 번 더 찾는다.
            if (!found)
            {
                GameObject canvas = GameObject.Find("CharacterInfoCanvas");
                if (canvas != null)
                {
                    foreach (Text t in canvas.GetComponentsInChildren<Text>(true))
                    {
                        if (t.text == UiChrome.CloseHintText) { found = true; break; }
                    }
                }
            }

            Assert.IsTrue(found,
                $"{LogPrefix} 정보창 타이틀바에 \"{UiChrome.CloseHintText}\"가 없습니다 — 민지가 " +
                "Cmd+W를 누른 그 순간 시선은 <b>창의 오른쪽 위</b>에 있었습니다. 답이 거기 있어야 합니다.");

            info.Close("테스트 정리");
        }

        // ==================== ③ 좁은 팝오버(244) — 힌트를 <b>일부러</b> 붙이지 않는다 ====================

        /// <summary>★ 네거티브 컨트롤 겸 설계 오류 정정. 폭 판정이 실제로 동작하지 않으면(= 어떤
        /// 폭에서도 힌트를 붙이면) 244pt 팝오버에서 힌트가 제목 <c>집중 모드 · 진행 중</c>을 통째로
        /// 덮는다. 여기서 "안 붙는다"가 확인되어야 ①의 "붙는다"가 의미를 갖는다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NarrowFocusPopoverDropsTheHintBecauseTheTitleWins()
        {
            yield return LoadScene();

            var focus = Object.FindFirstObjectByType<FocusSessionPopover>();
            Assert.IsNotNull(focus, $"{LogPrefix} 씬에 FocusSessionPopover가 없습니다.");

            focus.Open(new Rect(400f, 400f, 44f, 44f), "PlayMode 테스트");
            yield return null;
            yield return null;

            Assert.IsNull(focus.CloseHintTextForTests,
                $"{LogPrefix} 244pt 팝오버에까지 힌트가 붙었습니다 — 그 폭에서는 힌트(최소 " +
                $"{UiChrome.CloseHintMinWidth}pt)와 제목이 같은 줄에 함께 앉을 수 없습니다. " +
                "41-3 ④가 정한 대로 <b>힌트를 먼저 지웁니다</b>(제목이 우선). " +
                "★ 이 창의 닫기 안내는 별도 배정 필요 — 41-3 ③의 '팝오버 최소 폭 480'은 사실이 아닙니다.");

            focus.Close("테스트 정리");
        }
    }
}
