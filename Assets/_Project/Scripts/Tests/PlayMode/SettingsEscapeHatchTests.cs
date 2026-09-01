using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ <b>탈출구</b>(원칙 4) — 2026-09-02 (docs/UX_FLOW.md 41-2 / 41-3).
    ///
    /// ============================================================================
    /// 무엇이 잘못됐었나 — 끄는 법이 둘인데 <b>맞는 쪽이 안 보였다</b>
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><c>[지금 종료]</c>는 [일반] 탭 <c>시작 / 종료</c> 카드 안에 있었고, 그 카드는 페이지가
    ///        넘친 92pt 구간(뷰포트 하단보다 24~48pt 아래)에 앉아 <b>첫 화면에서 안 보였다</b>.
    ///        게다가 이 창에는 세로 스크롤이 없다(휠 핸들러 참조 0건).</item>
    ///  <item>반면 <b>잘 보이는</b> 종료 버튼은 [행동 명령] 팝오버 푸터에 있었는데, 그 창은 스스로
    ///        <i>"캐릭터에게 지금 시킬 수 있어요"</i>라고 선언한다 → 앱 종료가 <b>여섯 번째 놀이
    ///        항목</b>으로 읽혔다.</item>
    ///  <item>그리고 <c>Cmd+W</c>는 우리 창이 아니라 <b>뒤에 있던 Finder 창을 닫았다</b>(우리 창은
    ///        layer=101 전체화면 1장뿐이라 그 안의 창들은 키보드 포커스를 못 받는다). 닫는 법은
    ///        이미 동작하고 있었다 — <b>화면이 안 알려줬을 뿐이다</b>.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 = 41-2의 <b>게이트 A</b>
    /// ============================================================================
    /// <b>"[지금 종료]가 5개 탭 전부에서, 스크롤 없이 보인다"</b>. 이 게이트가 초록이 되기 전까지
    /// [행동 명령] 창의 <c>[✕ 앱 종료]</c>를 <b>빼면 안 된다</b> — 이 앱에는 Dock 아이콘도 메뉴바
    /// 아이콘도 없어서, 순서를 뒤집으면 마우스만 쓰는 사용자의 종료 수단이 0이 되는 순간이 생긴다.
    ///
    /// <para>좌표를 손으로 적지 않는다 — <see cref="SettingsWindow.QuitButtonScreenRect"/>/
    /// <c>ContentViewportScreenRect</c>가 프로덕션이 실제로 그린 사각형을 준다. 푸터가 한 번
    /// 움직이면 숫자를 베낀 테스트는 조용히 엉뚱한 곳을 잰다.</para>
    ///
    /// <para><b>네거티브 컨트롤</b>: <see cref="NegativeControl_카드_안에_있으면_뷰포트_밖_단언이_실제로_빨개진다"/>가
    /// "고치기 전"(버튼이 스크롤 영역 안에 있는 상태)을 재현해 이 단언이 실제로 무언가를 잡는지 민다.</para>
    /// </summary>
    public sealed class SettingsEscapeHatchTests
    {
        private const string LogPrefix = "[탈출구-TEST]";

        private SettingsWindow _settings;

        private IEnumerator LoadAndOpen()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");

            _settings.Open("테스트");
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator CloseEverything()
        {
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            _settings = null;
            AppSettingsModel.ResetForTesting();
            yield return null;
        }

        // ==================== 게이트 A ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator QuitButtonIsVisibleOnEveryTabWithoutScrolling()
        {
            yield return LoadAndOpen();

            Rect panel = _settings.PanelScreenRect;
            Rect viewport = _settings.ContentViewportScreenRect;
            Assert.Greater(panel.width, 1f, $"{LogPrefix} 창 사각형이 비었습니다.");
            Assert.Greater(viewport.width, 1f, $"{LogPrefix} 내용 영역 사각형이 비었습니다.");

            foreach (SettingsWindow.Tab tab in System.Enum.GetValues(typeof(SettingsWindow.Tab)))
            {
                // 실제 사용자와 <b>같은 경로</b>로 탭을 고른다(직접 상태를 만지면 탭 전환이
                // 부수적으로 하는 일 — DisarmQuit/저장 플러시/레일 동기화 — 이 빠진다).
                _settings.FeedClickForTests(_settings.TabScreenRect(tab).center);
                yield return null;
                Assume.That(_settings.ActiveTab, Is.EqualTo(tab),
                    $"{LogPrefix} [{tab}] 탭으로 전환되지 않았습니다.");

                Rect quit = _settings.QuitButtonScreenRect;

                Assert.Greater(quit.width, 1f,
                    $"{LogPrefix} [{tab}] 탭에서 [지금 종료] 사각형이 비었습니다 — 탈출구가 " +
                    "이 탭에는 존재하지 않는다는 뜻입니다(원칙 4).");

                Assert.IsTrue(panel.Overlaps(quit) && quit.xMin >= panel.xMin - 0.5f
                        && quit.xMax <= panel.xMax + 0.5f
                        && quit.yMin >= panel.yMin - 0.5f && quit.yMax <= panel.yMax + 0.5f,
                    $"{LogPrefix} [{tab}] 탭에서 [지금 종료]({quit})가 창({panel}) 밖으로 나갔습니다.");

                Assert.IsFalse(quit.Overlaps(viewport),
                    $"{LogPrefix} [{tab}] 탭에서 [지금 종료]({quit})가 스크롤되는 내용 영역" +
                    $"({viewport}) 안에 있습니다 — 그 자리에 있으면 페이지가 넘치는 순간 다시 " +
                    "잘려 나갑니다. [일반] 탭은 앞으로도 항상 넘칩니다(41-2 ①-a).");
            }
        }

        /// <summary>★ 네거티브 컨트롤 — "뷰포트 밖" 단언이 실제로 무언가를 잡는가.
        /// 내용 영역 <b>안</b>에 있는 부품(카드 안의 [숨기기]/[보이기] 버튼과 같은 층인 페이지 컨테이너)을
        /// 같은 잣대로 재면 <b>반드시 실패해야 한다</b>. 실패하지 않으면 위 검사는 아무 조건도 아니다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NegativeControl_카드_안에_있으면_뷰포트_밖_단언이_실제로_빨개진다()
        {
            yield return LoadAndOpen();

            Rect viewport = _settings.ContentViewportScreenRect;
            Rect insideContent = _settings.CharacterScaleTrackScreenRect;   // [캐릭터] 탭 슬라이더 트랙

            _settings.FeedClickForTests(_settings.TabScreenRect(SettingsWindow.Tab.Character).center);
            yield return null;
            insideContent = _settings.CharacterScaleTrackScreenRect;

            Assume.That(insideContent.width, Is.GreaterThan(1f),
                $"{LogPrefix} 전제: 내용 영역 안에 있는 부품의 사각형을 얻지 못했습니다.");

            Assert.IsTrue(insideContent.Overlaps(viewport),
                $"{LogPrefix} 내용 영역 안에 있어야 할 부품({insideContent})이 뷰포트({viewport})와 " +
                "겹치지 않습니다 — 그렇다면 위 게이트 A의 '뷰포트 밖' 단언은 어떤 배치에서도 통과하는 " +
                "빈 조건이 됩니다(거짓 초록).");
        }

        // ==================== 41-3 — 닫는 법을 화면이 말한다 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SettingsWindowTellsHowToCloseItInBothHeaderAndFooter()
        {
            yield return LoadAndOpen();

            List<Text> hints = TextsSaying(UiChrome.CloseHintText);
            Assert.GreaterOrEqual(hints.Count, 1,
                $"{LogPrefix} 설정창 헤더에 \"{UiChrome.CloseHintText}\"가 없습니다 — 이 앱은 " +
                "Esc도 Cmd+W도 받지 못하고(포커스 없는 오버레이), Cmd+W는 <b>뒤에 있던 남의 창</b>을 " +
                "닫습니다. 닫는 법은 이미 동작하므로 화면이 그것을 말하기만 하면 됩니다.");

            bool footerSentence = false;
            foreach (Text t in AllTexts())
            {
                if (string.IsNullOrEmpty(t.text)) continue;
                if (t.text.Contains("창 밖 아무 곳이나 클릭하면 닫혀요")) footerSentence = true;
            }
            Assert.IsTrue(footerSentence,
                $"{LogPrefix} 설정창 푸터에 긴 닫기 문장이 없습니다 — 이 창은 머무는 시간이 가장 길고, " +
                "푸터가 이미 '여는 방법'을 적고 있어 여는 법 ↔ 닫는 법이 한 자리에서 짝을 이룹니다.");
        }

        /// <summary>★ "안 되는 키를 적지 않는다"는 규칙을 잠근다. 화면에 <c>Esc</c>가 적히면
        /// 사용자는 <b>그 문장을 읽고 나서</b> 그 키를 시도한다 — 그리고 아무 일도 안 일어난다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NoSurfaceAdvertisesAKeyThatThisAppCannotReceive()
        {
            yield return LoadAndOpen();

            var offenders = new List<string>();
            foreach (Text t in AllTexts())
            {
                string s = t.text;
                if (string.IsNullOrEmpty(s)) continue;
                if (s.Contains("Esc") || s.Contains("ESC") || s.Contains("esc")
                    || s.Contains("Cmd+W") || s.Contains("⌘W"))
                {
                    offenders.Add($"[{t.gameObject.name}] \"{s}\"");
                }
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 이 앱이 <b>받지 못하는</b> 키가 화면에 적혀 있습니다:\n  " +
                string.Join("\n  ", offenders) +
                "\n우리 창은 layer=101 전체화면 1장뿐이고 그 안의 창들은 키보드 포커스를 못 받습니다. " +
                "안 되는 키를 적으면 사용자는 그 키를 시도합니다 — 되는 것만 적습니다.");
        }

        // ==================== 도구 ====================

        private static GameObject SettingsCanvas()
        {
            GameObject go = GameObject.Find("SettingsCanvas");
            Assert.IsNotNull(go, $"{LogPrefix} 씬에서 SettingsCanvas를 찾지 못했습니다.");
            return go;
        }

        private static IEnumerable<Text> AllTexts()
            => SettingsCanvas().GetComponentsInChildren<Text>(true);

        private static List<Text> TextsSaying(string exact)
        {
            var found = new List<Text>();
            foreach (Text t in AllTexts())
            {
                if (t.text == exact) found.Add(t);
            }
            return found;
        }
    }
}
