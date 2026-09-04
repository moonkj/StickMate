using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    /// ★★ <b>M-4 — 푸터 [지금 종료] 칩의 「영어 × Windows」 최악 칸</b>을 러너 실측으로 닫는다
    /// (2026-09-05, test-engineer).
    ///
    /// ============================================================================
    /// 무엇이 아직 안 닫혀 있었나
    /// ============================================================================
    /// <c>docs/strategy/WINDOWS_STEAM_LAUNCH_CRITICAL_PATH.md §3 M-4</c> / <c>ROADMAP W-5</c>:
    /// <list type="bullet">
    ///   <item>이 칩의 라벨은 <b>플랫폼마다 물리적으로 다르다</b> —
    ///     <c>ShortcutLabel.MacModifiers</c>(<c>⌃⌥⌘</c>, 3자) vs
    ///     <c>ShortcutLabel.WindowsModifiers</c>(<c>Ctrl+Alt+Win+</c>, 13자).</item>
    ///   <item>2026-09-03에 <c>UiTextWidthModelTests</c>가 <b>한국어 문안 × Windows 표기</b>를 실측해
    ///     «150pt 글리프가 132pt 상자에 들어 있었다»를 잡아냈고, 프로덕션이
    ///     <c>Max(하한, 실측잉크 + 여백)</c>로 바뀌었다.</item>
    ///   <item><b>그런데 「영어 문안」 축은 그때도 안 쟀다.</b> 1.0 영어는 확정(D-5)이고 최악 칸은
    ///     «Windows × 영어»인데, 이 머신에서 그 칸을 <b>아무도 본 적이 없다</b>.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 이 파일이 스스로에게 거는 제약
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>프로덕션 상수를 숫자로 베끼지 않는다.</b> 칩 폭 하한은
    ///     <c>SettingsWindow.QuitButtonWidth</c>를 <b>리플렉션으로 읽고</b>, 여백은
    ///     <c>SettingsControls.ButtonPadX</c>, 창 치수는 <c>SettingsWindow.PanelWidth</c>/
    ///     <c>ContentPadX</c>를 직접 참조한다. 이름을 니들로 쓰는 곳(<c>"QuitButtonWidth"</c>)에는
    ///     <b>존재 단언 + 음성 대조</b>를 같은 테스트 안에 붙인다(CLAUDE.md 협업 프로토콜).</item>
    ///   <item><b>기대값을 프로덕션 함수로 만들지 않는다.</b> 잉크 폭은
    ///     <c>SettingsControls.MeasuredWidth</c>를 부르지 않고 이 파일이 직접
    ///     <c>Mathf.Ceil(Text.preferredWidth)</c>로 만든다(TEAM.md «생성기와 검사기가 같이 틀린다»).</item>
    ///   <item><b>문자열은 실물에서 파생한다.</b> 한국어 문안·조합키 표기를 손으로 적지 않고,
    ///     씬에 실제로 떠 있는 라벨에서 잘라 내 <c>ShortcutLabel.WindowsChord</c>로 갈아 끼운다.
    ///     <b>단 하나 예외가 영어 어간</b>이고, 그것은 아래 <see cref="LatinBaseCandidates"/>에
    ///     출처와 함께 격리해 둔다 — 이 저장소에 영어 로컬라이즈 리소스가 아직 없기 때문이다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 이 측정의 한계 — 정직하게 적는다
    /// ============================================================================
    /// <b>이 개발 머신에는 Windows 폰트 스택이 없다.</b> 여기서 재는 것은 Unity 내장 Arial의
    /// macOS 인스턴스이고, Windows 실기의 라틴 자폭(Segoe UI 폴백)과 한글 폴백(맑은 고딕)은
    /// 다르다. 그래서 이 파일이 증명하는 것은 <b>「Windows 표기로 13자 늘어난 라틴 문자열이
    /// 지금 배치의 예산 안에 있다」</b>이지 <b>「Windows 실기에서 픽셀이 이렇다」</b>가 아니다.
    ///
    /// <para>그럼에도 값을 하는 이유: 프로덕션이 상자를 <b>그 폰트가 잰 값</b>으로 만들기 때문에
    /// (<c>SettingsWindow.BuildFooter</c>), <b>글자가 칩 밖으로 새는 형태의 결함은 폰트와 무관하게
    /// 구조적으로 불가능</b>하다. 남는 위험은 «칩이 커져서 옆 글자를 덮는가» 하나뿐이고,
    /// 이 파일은 그 여유를 <b>pt로</b> 남긴다 — 여유가 자폭 차이보다 훨씬 크면 실기에서도 참이다.</para>
    /// </summary>
    public sealed class SettingsQuitChipLatinBudgetTests
    {
        private const string LogPrefix = "[종료칩라틴-TEST]";

        /// <summary>상자 폭은 <c>Ceil</c>로 만들어지고 <c>preferredWidth</c>는 float이라
        /// 마지막 자리에서 갈릴 수 있다. 캔버스 1pt = 1px 격자이므로 1pt 미만은 화면에서 같다.</summary>
        private const float MeasureEpsilon = 0.5f;

        /// <summary>
        /// <b>영어 어간 후보.</b> ★ 이 배열이 이 파일에서 유일하게 «실물에서 파생되지 않은» 문자열이다.
        ///
        /// <para><b>출처</b>: <c>docs/strategy/ROADMAP.md:1286</c>(W-5)가 영어 문안을
        /// <c>Quit now (Ctrl+Alt+Win+Q)</c>로 적고 있다. 저장소에 영어 로컬라이즈 리소스가 아직
        /// 없으므로(<c>docs/localization/PLAN_1.0.md</c> — 이관 전), 그 한 줄이 현재 유일한 근거다.</para>
        ///
        /// <para>나머지 둘은 <b>여유 확인용 스트레스</b>다. 문안 확정은 <c>ux-designer</c>/
        /// <c>design-narrative</c> 소관이고, 여기서는 «어간이 이만큼 길어져도 예산 안인가»만 본다.
        /// 이 셋 중 어느 것도 «확정된 문안»이 아니다 — 확정되면 그 문자열로 갈아 끼운다.</para>
        /// </summary>
        private static readonly string[] LatinBaseCandidates =
        {
            "Quit now",           // ROADMAP W-5가 적은 문안
            "Quit StickMate",     // 스트레스 — 앱 이름을 넣는 안
            "Quit the app now",   // 스트레스 — 이 자리에 올 법한 가장 긴 안
        };

        private SettingsWindow _settings;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            _settings = null;
            AppSettingsModel.ResetForTesting();
            yield return null;
        }

        private IEnumerator OpenSettings()
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

        private static List<Transform> Descendants(GameObject root, string exactName)
        {
            var found = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == exactName) found.Add(t);
            }
            return found;
        }

        /// <summary>이 테스트가 <b>직접</b> 만드는 잉크 폭. 프로덕션의 같은 이름 함수를 부르지 않는다.</summary>
        private static float InkWidth(Text text, string content)
        {
            text.text = content ?? string.Empty;
            return Mathf.Ceil(text.preferredWidth);
        }

        /// <summary>씬에 실제로 떠 있는 종료 칩의 라벨/사각형/닫기 안내를 집어 온다.</summary>
        private static void FindFooterParts(out Text quitLabel, out RectTransform chip, out Text closeHint,
                                            out RectTransform hintRect)
        {
            GameObject canvas = GameObject.Find("SettingsCanvas");
            Assert.IsNotNull(canvas, $"{LogPrefix} SettingsCanvas를 찾지 못했습니다.");

            List<Transform> quits = Descendants(canvas, "Quit");
            Assert.AreEqual(1, quits.Count, $"{LogPrefix} 푸터 [지금 종료] 칩을 {quits.Count}개 찾았습니다.");
            Transform labelT = quits[0].Find("Label");
            Assert.IsNotNull(labelT, $"{LogPrefix} 종료 칩에 Label이 없습니다.");
            quitLabel = labelT.GetComponent<Text>();
            Assert.IsNotNull(quitLabel, $"{LogPrefix} 종료 칩 Label에 Text가 없습니다.");
            chip = (RectTransform)quits[0];

            List<Transform> hints = Descendants(canvas, "CloseHint");
            Assert.AreEqual(1, hints.Count, $"{LogPrefix} 푸터 CloseHint를 {hints.Count}개 찾았습니다.");
            closeHint = hints[0].GetComponent<Text>();
            Assert.IsNotNull(closeHint, $"{LogPrefix} CloseHint에 Text가 없습니다.");
            hintRect = (RectTransform)hints[0];
        }

        // ====================================================================
        // ★ 양성/음성 대조 — 이것이 깨지면 아래 모든 숫자가 무효다
        // ====================================================================

        /// <summary>
        /// 실측 경로가 살아 있는가. 셋을 한 번에 못박는다.
        /// <list type="number">
        ///   <item><b>폰트가 실제로 잰다</b> — 배치모드에서 폰트가 안 올라오면 모든 폭이 0이 되고
        ///     "다 들어간다"가 <b>거짓 초록</b>이 된다.</item>
        ///   <item><b>Windows 표기 파생이 살아 있다</b> — macOS 호스트에서 치환이 0건이면 아래
        ///     «Windows 숫자»는 macOS 숫자를 다시 적은 것일 뿐이다.</item>
        ///   <item><b>리플렉션 니들이 살아 있다</b> — <c>"QuitButtonWidth"</c>는 실재해야 하고,
        ///     같은 호출로 <b>없는 이름</b>은 null이어야 한다(니들이 아무거나 집어 오지 않는다).</item>
        /// </list>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 양성음성대조_폰트와_표기파생과_폭상수_니들이_모두_살아있다()
        {
            yield return OpenSettings();

            FindFooterParts(out Text label, out _, out Text hint, out _);
            string hostText = label.text;
            Assert.IsNotEmpty(hostText, $"{LogPrefix} 종료 칩 라벨이 비었습니다.");

            // ① 폰트가 잰다 — 그리고 라틴과 한글을 다르게 잰다.
            float latin = InkWidth(label, "abcde");
            float hangul = InkWidth(label, "가나다라마");
            label.text = hostText;
            Assert.Greater(latin, 0f,
                $"{LogPrefix} 라틴 5자의 폭이 0입니다 — 폰트가 올라오지 않았습니다. " +
                "이 파일의 모든 숫자를 폐기하십시오.");
            Assert.Greater(hangul, 0f, $"{LogPrefix} 한글 5자의 폭이 0입니다(폰트 미로딩).");
            Assert.Greater(hangul, latin,
                $"{LogPrefix} 한글 5자({hangul:F1}pt)가 라틴 5자({latin:F1}pt)보다 넓지 않습니다 — " +
                "폰트가 폴백으로 대체되어 두 문자 계열을 구분하지 못하는 상태일 수 있습니다.");
            Assert.Greater(hint.preferredWidth, 0f,
                $"{LogPrefix} 푸터 닫기 안내의 폭이 0입니다 — 예산 계산이 공허해집니다.");

            // ② Windows 표기 파생이 살아 있다.
            string mac = ShortcutLabel.MacChord("Q");
            string win = ShortcutLabel.WindowsChord("Q");
            Assert.AreNotEqual(mac, win,
                $"{LogPrefix} MacChord와 WindowsChord가 같습니다 — 두 표기를 가르는 근거가 사라졌습니다.");
            if (!ShortcutLabel.HostUsesWindowsNotation)
            {
                StringAssert.Contains(mac, hostText,
                    $"{LogPrefix} macOS 호스트인데 라벨 «{hostText}»에 macOS 표기 «{mac}»가 없습니다 — " +
                    "치환 기반 파생이 죽었습니다(그러면 아래 Windows 숫자는 아무것도 아닙니다).");
            }

            // ③ 리플렉션 니들 — 실재 대조와 부재 대조를 같은 호출로 함께 본다.
            const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            FieldInfo real = typeof(SettingsWindow).GetField("QuitButtonWidth", Any);
            Assert.IsNotNull(real,
                $"{LogPrefix} SettingsWindow.QuitButtonWidth를 찾지 못했습니다 — 이름이 바뀌었다면 " +
                "아래 «하한» 검사는 아무것도 재지 않습니다(조용한 초록).");
            FieldInfo bogus = typeof(SettingsWindow).GetField("QuitButtonWidthThatDoesNotExist", Any);
            Assert.IsNull(bogus,
                $"{LogPrefix} 존재하지 않는 이름이 필드를 돌려줬습니다 — 이 리플렉션 프로브를 믿을 수 없습니다.");

            Debug.Log($"{LogPrefix} 대조 통과 — 5자 기준 라틴 {latin:F1}pt / 한글 {hangul:F1}pt " +
                      $"(폰트 {label.fontSize}pt, 굵기 {label.fontStyle}). 호스트 표기 «{mac}» → Windows «{win}».");
        }

        // ====================================================================
        // ★ 본체 — 영어 × Windows 최악 칸
        // ====================================================================

        /// <summary>
        /// <b>M-4 본체.</b> 영어 어간 + Windows 조합키 표기로 만든 라벨을 실측해,
        /// 프로덕션과 <b>같은 조립 규칙</b>(<c>Max(하한, 잉크 + 여백×2)</c>)으로 칩 폭을 다시 만든 뒤
        /// 푸터 아랫줄에서 <b>닫기 안내 글자를 덮지 않는가</b>를 본다.
        ///
        /// <para><b>왜 «칩 안에 들어가는가»가 아니라 «옆을 덮는가»인가</b>: 프로덕션이 상자를 실측으로
        /// 만들기 때문에 글자가 칩을 넘는 일은 <b>구조적으로</b> 없다(그 등식은 아래에서 함께 단언한다).
        /// 남는 실패 형태는 «칩이 커져서 왼쪽 글자를 덮는다» 하나뿐이고, 그것이 이 테스트가 재는 것이다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 종료칩은_영어문안과_Windows표기에서도_푸터_아랫줄_예산_안에_있다()
        {
            yield return OpenSettings();

            FindFooterParts(out Text label, out RectTransform chip, out Text hint, out RectTransform hintRect);

            string hostText = label.text;
            string macChord = ShortcutLabel.MacChord("Q");
            string winChord = ShortcutLabel.WindowsChord("Q");

            // ---- ① 문자열을 실물에서 파생한다 -------------------------------------------
            int paren = hostText.LastIndexOf('(');
            Assert.Greater(paren, 0,
                $"{LogPrefix} 라벨 «{hostText}»에서 조합키 괄호를 찾지 못했습니다 — 문안 형태가 바뀌었다면 " +
                "이 파생 규칙을 함께 고치십시오(지금 이 테스트는 아무것도 재지 못합니다).");
            string hostBase = hostText.Substring(0, paren).TrimEnd();
            string hostTail = hostText.Substring(paren);                       // "(⌃⌥⌘Q)" 또는 "(Ctrl+Alt+Win+Q)"
            Assert.IsNotEmpty(hostBase, $"{LogPrefix} 라벨 어간이 비었습니다.");

            string winTail = hostTail.Replace(macChord, winChord);
            StringAssert.Contains(ShortcutLabel.WindowsModifiers, winTail,
                $"{LogPrefix} Windows 꼬리 «{winTail}»에 Windows 수식자 표기가 없습니다 — 파생이 죽었습니다.");

            // ---- ② 프로덕션과 같은 조립 규칙으로 폭을 다시 만든다 -------------------------
            const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            FieldInfo floorField = typeof(SettingsWindow).GetField("QuitButtonWidth", Any);
            Assert.IsNotNull(floorField,
                $"{LogPrefix} SettingsWindow.QuitButtonWidth를 찾지 못했습니다(양성 대조 테스트를 먼저 보십시오).");
            float floor = (float)floorField.GetValue(null);
            Assert.Greater(floor, 0f, $"{LogPrefix} 칩 폭 하한이 {floor}pt입니다 — 하한이 비었습니다.");

            float pad = SettingsControls.ButtonPadX * 2f;

            // 푸터 아랫줄의 실제 경계.
            //   왼쪽: 닫기 안내 «글리프»의 오른쪽 끝(선언된 상자가 아니라 실제로 칠해지는 자리).
            //   오른쪽: 창 오른쪽 끝에서 ContentPadX 안쪽 — 칩은 여기에 오른쪽 정렬된다.
            float hintRight = hintRect.anchoredPosition.x + Mathf.Ceil(hint.preferredWidth);
            float chipRight = SettingsWindow.PanelWidth - SettingsWindow.ContentPadX;
            float available = chipRight - hintRight;
            Assert.Greater(available, 0f,
                $"{LogPrefix} 닫기 안내가 {hintRight:F1}pt까지 차서 칩 자리가 없습니다.");

            // ---- ③ 현재(한국어) 칩이 자기 라벨을 담는가 — 구조 등식 -----------------------
            float hostInk = InkWidth(label, hostText);
            Assert.AreEqual(Mathf.Max(floor, hostInk + pad), chip.rect.width, MeasureEpsilon,
                $"{LogPrefix} 칩 폭 {chip.rect.width:F1}pt가 «Max(하한 {floor:F0}, 잉크 {hostInk:F1} + 여백 " +
                $"{pad:F0})»와 다릅니다 — 프로덕션이 실측으로 상자를 만들지 않고 있거나 조립 규칙이 바뀌었습니다. " +
                "그렇다면 아래 영어 예측도 프로덕션과 다른 식으로 계산된 것이라 무효입니다.");

            // ---- ④ 영어 후보들을 잰다 ---------------------------------------------------
            var report = new System.Text.StringBuilder();
            report.Append(LogPrefix).Append(" 푸터 [지금 종료] 칩 — 영어 × Windows 예산 대조\n");
            report.Append($"    호스트 현행 «{hostText}»  잉크 {hostInk,6:F1}pt  칩 {chip.rect.width,6:F1}pt\n");
            report.Append($"    닫기 안내 «{hint.text}»  잉크 {Mathf.Ceil(hint.preferredWidth),6:F1}pt  " +
                          $"→ 글리프 끝 {hintRight:F1}pt\n");
            report.Append($"    푸터 아랫줄 가용 폭 {available:F1}pt  (창 {SettingsWindow.PanelWidth:F0} − " +
                          $"여백 {SettingsWindow.ContentPadX:F0} − 안내 끝 {hintRight:F1})\n");

            float worstNeeded = 0f;
            string worstText = string.Empty;
            foreach (string baseText in LatinBaseCandidates)
            {
                string enWin = baseText + " " + winTail;
                float ink = InkWidth(label, enWin);
                float need = Mathf.Max(floor, ink + pad);
                if (need > worstNeeded) { worstNeeded = need; worstText = enWin; }
                report.Append($"    «{enWin}»  잉크 {ink,6:F1}pt  → 칩 {need,6:F1}pt  " +
                              $"여유 {available - need,+7:F1}pt\n");
            }

            // 한국어 어간 + Windows 표기(2026-09-03에 실제로 터졌던 칸)도 같은 표에 남긴다.
            string koWin = hostBase + " " + winTail;
            float koWinInk = InkWidth(label, koWin);
            float koWinNeed = Mathf.Max(floor, koWinInk + pad);
            report.Append($"    «{koWin}»  잉크 {koWinInk,6:F1}pt  → 칩 {koWinNeed,6:F1}pt  " +
                          $"여유 {available - koWinNeed,+7:F1}pt   (한국어 × Windows, 2026-09-03 회귀 기준)\n");

            // ---- ⑤ 어간 예산 — 문안이 확정되지 않아도 다음 사람이 쓸 수 있는 숫자 ----------
            //  후보 목록이 낡아도 이 줄은 낡지 않는다. «어간에 몇 pt까지 쓸 수 있는가»가 답이다.
            float tailOnlyInk = InkWidth(label, " " + winTail);
            float baseBudget = available - pad - tailOnlyInk;
            float latinAdvance = InkWidth(label, "Quit nowQuit now") - InkWidth(label, "Quit now");  // 8자 증분
            latinAdvance /= 8f;
            label.text = hostText;   // 실물을 원상 복구한다.
            report.Append($"    ⇒ 어간 예산 {baseBudget:F1}pt  (가용 {available:F1} − 여백 {pad:F0} − " +
                          $"꼬리 «{winTail}» {tailOnlyInk:F1})\n");
            report.Append($"       라틴 평균 자폭 {latinAdvance:F2}pt 기준 약 {Mathf.FloorToInt(baseBudget / latinAdvance)}자");
            Debug.Log(report.ToString());

            // ---- ⑥ 판정 ------------------------------------------------------------------
            Assert.LessOrEqual(worstNeeded, available,
                $"{LogPrefix} 영어 최악 후보 «{worstText}»의 칩이 {worstNeeded:F1}pt를 필요로 하는데 푸터 " +
                $"아랫줄에는 {available:F1}pt뿐입니다 — {worstNeeded - available:F1}pt 초과. Windows·영어에서 " +
                "종료 칩이 닫기 안내 글자를 덮습니다. 칩 폭 상수가 아니라 문안·배치를 고쳐야 하고 " +
                "그것은 ux-designer 소관입니다.");
            Assert.LessOrEqual(koWinNeed, available,
                $"{LogPrefix} 한국어 × Windows 칸이 다시 넘쳤습니다({koWinNeed:F1}pt > {available:F1}pt) — " +
                "2026-09-03에 닫은 회귀가 되살아났습니다.");
            Assert.Greater(baseBudget, 0f,
                $"{LogPrefix} 어간 예산이 {baseBudget:F1}pt입니다 — 조합키 표기만으로 가용 폭을 다 씁니다.");
        }

        /// <summary>
        /// <b>하한 상수가 여전히 「하한」인가.</b> 2단 확인 문안(<c>정말 종료?</c>)은 평상시 문안보다
        /// 짧아서 <c>Max</c>의 왼쪽 항이 실제로 이긴다 — 그 자리가 없으면 확인 중에 칩이 쪼그라들어
        /// <b>누를 자리가 줄어든다</b>(탈출구는 이 창에서 가장 높은 단이다).
        ///
        /// <para>동시에 «하한이 상한처럼 쓰이고 있지는 않은가»를 본다. 하한이 영어 필요 폭보다 크면
        /// 칩이 문안과 무관하게 고정되고, 그때는 2026-09-03 이전의 「고정형」 결함으로 되돌아간다.</para>
        ///
        /// <para>★ <b>클릭으로 2단 확인을 켜지 않는다.</b> 그 상태에서 한 번 더 눌리면
        /// <c>Application.Quit()</c> + <c>EditorApplication.isPlaying = false</c>가 돌아 <b>러너 자체가
        /// 죽는다</b>. 문안은 <c>QuitConfirmText</c>를 리플렉션으로 읽어 온다 — 니들의 존재/부재 대조는
        /// 여기서도 같이 건다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 칩폭_하한상수는_짧은_문안에서만_이기고_긴_문안을_가두지_않는다()
        {
            yield return OpenSettings();

            FindFooterParts(out Text label, out RectTransform chip, out _, out _);
            string hostText = label.text;

            const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            FieldInfo floorField = typeof(SettingsWindow).GetField("QuitButtonWidth", Any);
            Assert.IsNotNull(floorField, $"{LogPrefix} QuitButtonWidth를 찾지 못했습니다.");
            float floor = (float)floorField.GetValue(null);
            float pad = SettingsControls.ButtonPadX * 2f;

            FieldInfo confirmField = typeof(SettingsWindow).GetField("QuitConfirmText", Any);
            Assert.IsNotNull(confirmField,
                $"{LogPrefix} SettingsWindow.QuitConfirmText를 찾지 못했습니다 — 2단 확인 문안이 다시 " +
                "호출부에 흩어졌다면 폭을 재는 쪽이 그 문안을 볼 수 없게 됩니다(그것이 이 상수를 만든 사유다).");
            Assert.IsNull(typeof(SettingsWindow).GetField("QuitConfirmTextThatDoesNotExist", Any),
                $"{LogPrefix} 존재하지 않는 이름이 필드를 돌려줬습니다 — 이 프로브를 믿을 수 없습니다.");
            var confirmText = (string)confirmField.GetValue(null);
            Assert.IsNotEmpty(confirmText, $"{LogPrefix} 2단 확인 문안이 비었습니다.");
            Assert.AreNotEqual(hostText, confirmText,
                $"{LogPrefix} 평상시 문안과 확인 문안이 같습니다 — 아래 «짧은 쪽» 대조가 공허합니다.");

            float confirmInk = InkWidth(label, confirmText);
            float hostInk = InkWidth(label, hostText);
            label.text = hostText;   // 실물을 원상 복구한다.

            Debug.Log($"{LogPrefix} 하한 대조 — 하한 {floor:F0}pt / 평상시 «{hostText}» 필요 {hostInk + pad:F1}pt / " +
                      $"확인 «{confirmText}» 필요 {confirmInk + pad:F1}pt / 현재 칩 {chip.rect.width:F1}pt.");

            Assert.Less(confirmInk + pad, floor,
                $"{LogPrefix} 확인 문안이 하한 없이도 {confirmInk + pad:F1}pt로 하한 {floor:F0}pt를 넘습니다 — " +
                "그렇다면 하한은 아무 일도 하지 않고 있고, 상수를 남긴 사유(짧은 문안에서 칩이 쪼그라들지 " +
                "않게)가 성립하지 않습니다.");
            Assert.AreEqual(Mathf.Max(floor, hostInk + pad), chip.rect.width, MeasureEpsilon,
                $"{LogPrefix} 칩 폭이 조립 규칙과 다릅니다 — 폭은 «평상시 문안 / 확인 문안 중 넓은 쪽»으로 " +
                "굽는 시점에 한 번만 정해져야 합니다.");
        }
    }
}
