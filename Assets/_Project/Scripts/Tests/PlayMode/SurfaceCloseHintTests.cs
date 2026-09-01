using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>화면이 거짓말을 하지 않는다</b> — 2026-09-02 (사용자 지시로 하루 만에 <b>뒤집힌</b> 파일).
    ///
    /// ============================================================================
    /// 이 파일의 이력 — 왜 정반대가 됐는가
    /// ============================================================================
    /// 이 파일은 원래 41-3의 <b>"닫는 법을 화면이 말한다"</b>를 잠갔다: 세 표면(정보창·설정창·팝오버)의
    /// [✕] 왼쪽에 <c>"창 밖을 클릭해도 닫혀요"</c>가 붙어 있는지, 좁은 팝오버에서는 제목에 밀려
    /// 지워지는지까지 쟀다.
    ///
    /// <para>그리고 <b>같은 날</b> 사용자 신고가 그 전제를 무너뜨렸다 — <i>"캐릭터창이나 다른 메뉴창들이
    /// 떠있을때 바탕화면을 클릭하면 꺼지는데 안꺼지고 사용자가 닫기전에는 안꺼져야함"</i>. 바깥 클릭이
    /// 더 이상 닫지 않으므로 그 문장은 <b>거짓</b>이 됐고, 문구와 동작을 함께 걷어냈다.</para>
    ///
    /// <para>그래서 이 파일이 잠그는 것도 뒤집혔다: 이제는 <b>그 문장이 다시 기어들어오지 않는지</b>를
    /// 잠근다. 이 실패 모드는 조용하다 — 문구는 계속 예쁘게 렌더되고, 사용자만 바탕화면을 여러 번
    /// 클릭하다 포기한다. 되살리기도 쉽다("힌트가 없네?" 하고 한 줄 되돌리면 끝이다).</para>
    ///
    /// ============================================================================
    /// 잠그는 것
    /// ============================================================================
    ///  ① <b>다섯 표면 어디에도</b> "창 밖/바깥을 클릭하면 닫힌다"는 문장이 없다.
    ///  ② ★ <b>네거티브 컨트롤</b> — ①의 스캐너가 실제로 글자를 훑고 있다(글자 0개를 훑고 "없다"고
    ///     말하는 것은 아무 조건도 아니다). 훑은 글자 수에 하한을 걸고, 일부러 심은 옛 문장을
    ///     <b>같은 스캐너가 잡아내는지</b>도 확인한다.
    ///  ③ 닫는 유일한 마우스 경로인 <b>[✕]가 세 표면 모두에 실재하고 실제로 닫는다</b>.
    ///     문구를 지웠으므로 이 버튼이 죽으면 탈출구가 <b>0개</b>가 된다.
    ///
    /// <para><b>왜 한 번에 다 열지 않는가</b>: 이 표면들은 전부 <see cref="IExclusiveSurface"/>라
    /// 하나를 열면 나머지가 닫히고, 닫힌 캔버스는 <c>SetActive(false)</c>라 <c>GameObject.Find</c>가
    /// 못 찾는다. 그 상태로 "거짓 문장 0건"을 세면 아무것도 안 훑고 초록이 된다 — 그래서
    /// <b>하나씩 열어 그때그때 훑는다</b>(그리고 ②의 하한이 그 실수를 다시 잡는다).</para>
    /// </summary>
    public sealed class SurfaceCloseHintTests
    {
        private const string LogPrefix = "[닫기문구-TEST]";

        /// <summary>"바깥 클릭으로 닫힌다"고 읽히는 문구 조각. 하나라도 화면에 있으면 거짓말이다.</summary>
        private static readonly string[] OutsideClickClaims =
        {
            "창 밖", "바깥을 클릭", "바깥 클릭", "빈 곳을 클릭", "바탕화면을 클릭",
        };

        private static readonly Rect AnchorRect = new Rect(400f, 400f, 44f, 44f);

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        // ==================== ① 다섯 표면 어디에도 그 문장이 없다 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator NoSurfaceClaimsThatClickingOutsideClosesIt()
        {
            yield return LoadScene();

            var info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            var settings = Object.FindFirstObjectByType<SettingsWindow>();
            var action = Object.FindFirstObjectByType<ActionCommandPopover>();
            var todo = Object.FindFirstObjectByType<TodoBoardPopover>();
            var focus = Object.FindFirstObjectByType<FocusSessionPopover>();
            Assert.IsNotNull(info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            Assert.IsNotNull(settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            Assert.IsNotNull(action, $"{LogPrefix} 씬에 ActionCommandPopover가 없습니다.");
            Assert.IsNotNull(todo, $"{LogPrefix} 씬에 TodoBoardPopover가 없습니다.");
            Assert.IsNotNull(focus, $"{LogPrefix} 씬에 FocusSessionPopover가 없습니다.");

            var offenders = new List<string>();
            int scanned = 0;

            info.Open("문구 검사");
            yield return null;
            scanned += ScanCanvas("CharacterInfoCanvas", offenders);
            info.Close("문구 검사 끝");
            yield return null;

            settings.Open("문구 검사");
            yield return null;
            scanned += ScanCanvas("SettingsCanvas", offenders);
            settings.Close("문구 검사 끝");
            yield return null;

            scanned += PopoverScan(action, "ActionCommandPopoverCanvas", offenders);
            yield return new WaitForSecondsRealtime(0.25f);
            scanned += PopoverScan(todo, "TodoBoardPopoverCanvas", offenders);
            yield return new WaitForSecondsRealtime(0.25f);
            scanned += PopoverScan(focus, "FocusSessionPopoverCanvas", offenders);
            yield return new WaitForSecondsRealtime(0.25f);

            // ★ 네거티브 컨트롤 (a) — 스캐너가 <b>빈 목록을 훑고</b> "없다"고 말하는 것을 막는다.
            //   다섯 표면의 크롬만 해도 제목/탭/버튼/캡션으로 수십 개다. 30은 넉넉히 낮은 하한이다.
            Assert.Greater(scanned, 30,
                $"{LogPrefix} 화면 글자를 {scanned}개밖에 못 찾았습니다 — 표면이 실제로 열리지 않았거나 " +
                "캔버스 이름이 바뀌었습니다. 이 상태의 \"거짓 문장 없음\"은 거짓 초록입니다.");

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 화면이 아직 <b>바깥 클릭으로 닫힌다</b>고 말하고 있습니다:\n  " +
                string.Join("\n  ", offenders) +
                "\n2026-09-02 사용자 지시로 그 동작을 세 표면에서 걷어냈습니다 — 문구만 남으면 " +
                "사용자는 바탕화면을 여러 번 클릭하다 포기합니다(원칙 1: 표시와 실제의 일치).");

            Debug.Log($"{LogPrefix} ① 통과 — 글자 {scanned}개를 훑어 거짓 문장 0건.");
        }

        // ==================== ② 네거티브 컨트롤 — 스캐너가 실제로 문다 ====================

        /// <summary>★ ①이 "어떤 문장이든 통과시키는" 빈 검사가 아니라는 증명. 실제 표면에 옛 문구를
        /// <b>일부러 심고</b> 같은 스캐너를 돌린다 — 반드시 잡혀야 한다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NegativeControl_TheScannerActuallyCatchesTheOldSentence()
        {
            yield return LoadScene();

            var info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            info.Open("네거티브 컨트롤");
            yield return null;

            GameObject canvas = GameObject.Find("CharacterInfoCanvas");
            Assert.IsNotNull(canvas, $"{LogPrefix} CharacterInfoCanvas를 찾지 못했습니다.");

            Text[] texts = canvas.GetComponentsInChildren<Text>(true);
            Assume.That(texts.Length, Is.GreaterThan(0), $"{LogPrefix} 정보창에 글자가 하나도 없습니다.");

            string original = texts[0].text;
            texts[0].text = "창 밖을 클릭해도 닫혀요";   // 2026-09-02 이전의 바로 그 문장.

            var offenders = new List<string>();
            ScanCanvas("CharacterInfoCanvas", offenders);
            texts[0].text = original;

            Assert.IsNotEmpty(offenders,
                $"{LogPrefix} 옛 문구를 <b>일부러 심었는데도</b> 스캐너가 잡지 못했습니다 — " +
                "그렇다면 ①의 \"없다\"는 어떤 화면에서도 통과하는 빈 조건입니다(거짓 초록).");

            info.Close("테스트 정리");
        }

        // ==================== ③ [✕]는 실재하고 실제로 닫는다 ====================

        /// <summary>★ 문구를 지운 대가로 <b>[✕]가 유일한 마우스 탈출구</b>가 됐다. 그것이 없거나
        /// 안 먹히면 사용자는 창을 영영 못 닫는다(이 앱은 Esc도 Cmd+W도 받지 못한다 —
        /// <see cref="UiChrome"/> "창을 닫는 법" 절).</summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TheCloseButtonIsTheSurvivingEscapeHatchOnEverySurface()
        {
            yield return LoadScene();

            var action = Object.FindFirstObjectByType<ActionCommandPopover>();
            Assert.IsNotNull(action, $"{LogPrefix} 씬에 ActionCommandPopover가 없습니다.");
            action.Open(AnchorRect, "탈출구 검사");
            yield return null;
            Assume.That(action.IsOpen, Is.True, $"{LogPrefix} 전제: 팝오버가 열려야 합니다.");

            Rect popoverClose = action.CloseButtonScreenRectForTests;
            Assert.Greater(popoverClose.width, 1f, $"{LogPrefix} 팝오버 [✕] 사각형이 비었습니다.");
            action.FeedClickForTests(popoverClose.center);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(action.IsOpen,
                $"{LogPrefix} 팝오버 [✕]를 눌렀는데 닫히지 않았습니다 — 바깥 클릭을 없앤 지금 " +
                "이것이 유일한 마우스 탈출구입니다.");

            var settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            settings.Open("탈출구 검사");
            yield return null;
            Rect settingsClose = settings.CloseButtonScreenRect;
            Assert.Greater(settingsClose.width, 1f, $"{LogPrefix} 설정창 [✕] 사각형이 비었습니다.");
            settings.FeedClickForTests(settingsClose.center);
            yield return null;
            Assert.IsFalse(settings.IsOpen, $"{LogPrefix} 설정창 [✕]를 눌렀는데 닫히지 않았습니다.");

            var info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            info.Open("탈출구 검사");
            yield return null;
            Rect infoClose = info.CloseButtonScreenRect;
            Assert.Greater(infoClose.width, 1f, $"{LogPrefix} 정보창 [✕] 사각형이 비었습니다.");
            info.FeedClickForTests(infoClose.center);
            yield return null;
            Assert.IsFalse(info.IsOpen, $"{LogPrefix} 정보창 [✕]를 눌렀는데 닫히지 않았습니다.");

            Debug.Log($"{LogPrefix} ③ 통과 — 세 표면 모두 [✕]로 실제로 닫힙니다.");
        }

        // ==================== 도구 ====================

        private static int PopoverScan(PopoverPanel popover, string canvasName, List<string> offenders)
        {
            popover.Open(AnchorRect, "문구 검사");
            int scanned = ScanCanvas(canvasName, offenders);
            popover.Close("문구 검사 끝");
            return scanned;
        }

        /// <summary>한 캔버스를 훑어 거짓 문장을 모은다. 돌려주는 값은 <b>훑은 글자 수</b> — 그것이 0이면
        /// "없다"는 결론 자체가 무의미하다는 것을 호출자가 알 수 있어야 한다.</summary>
        private static int ScanCanvas(string canvasName, List<string> offenders)
        {
            GameObject canvas = GameObject.Find(canvasName);
            if (canvas == null) return 0;

            int scanned = 0;
            foreach (Text t in canvas.GetComponentsInChildren<Text>(true))
            {
                if (string.IsNullOrEmpty(t.text)) continue;
                scanned++;
                for (int c = 0; c < OutsideClickClaims.Length; c++)
                {
                    if (!t.text.Contains(OutsideClickClaims[c])) continue;
                    offenders.Add($"{canvasName}/{t.gameObject.name}: \"{t.text}\"");
                    break;
                }
            }
            return scanned;
        }
    }
}
