using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    /// ★ 설정창에 <b>개발자끼리 쓰는 말</b>이 렌더되지 않는다 — 2026-09-01 페르소나(민지) M6 / 재현 J3.
    ///
    /// ============================================================================
    /// M6 — 실제로 화면에 나가던 문장들
    /// ============================================================================
    ///  · "준비 중 — 이 단축키는 아직 없습니다(<b>GlobalKey에 V가 없어</b> 다음 라운드에 <b>배선</b>합니다)"
    ///  · "준비 중 — 로그인 항목 등록은 네이티브 작업이라 별도 라운드입니다(<b>35-1-9 P3</b>)"
    /// 사용자는 GlobalKey도 35-1-9도 모른다. 읽히는 것은 "이 앱 미완성이구나" 하나다.
    ///
    /// <b>왜 소스가 아니라 렌더된 문자열을 보는가</b>: 사유 캡션은 <c>ComposeCaption</c>이 접두사를 붙여
    /// 조립한다. 소스의 문자열 리터럴만 보면 조립 결과를 놓치고, 무엇보다 <b>실제로 화면에 있는 것</b>이
    /// 이 테스트가 지키려는 사실이다. 비활성 탭 3개도 <c>includeInactive: true</c>로 함께 본다 —
    /// 첫 방문자가 가장 먼저 누르는 곳이 하필 그 3개다.
    ///
    /// <b>판정 규칙과 그 한계</b>: 한글 UI에서 <b>길이 3 이상의 라틴 문자 덩어리</b>는 사실상 내부
    /// 식별자다(단축키는 ⌃⌥⌘ + 한 글자, 단위는 "pt"/"s"라 걸리지 않는다). 제품명 같은 정당한 예외는
    /// <see cref="AllowedLatinWords"/>에 <b>명시적으로</b> 적는다 — 런타임에 문자열을 몰래 걸러 내는
    /// 방식(정규식 스크러버)을 쓰지 않은 이유가 이것이다. 조용히 고쳐 주면 다음 사람은 규칙을 배우지
    /// 못하고, "⌃⌥⌘I" 같은 정당한 문구까지 오탐으로 망가진다.
    /// </summary>
    public sealed class SettingsUserFacingCopyTests
    {
        private const string LogPrefix = "[설정창카피-TEST]";

        /// <summary>화면에 나와도 되는 라틴 단어(제품명 등). 늘릴 때는 <b>사용자가 읽을 말인가</b>만 본다.
        /// <para>★ 2026-09-01 — <b>Windows 조합키 이름</b> 셋이 합류했다. 위 문단의 전제("단축키는
        /// ⌃⌥⌘ + 한 글자라 걸리지 않는다")는 <b>macOS에서만</b> 참이다: Windows 빌드에서 같은 안내는
        /// <c>Ctrl+Alt+Win+I</c>로 렌더된다(Core/ShortcutLabel). 이 셋은 그 플랫폼 키보드에 실제로
        /// 각인된 이름이므로 "사용자가 읽을 말"이고, 여기 적어 두지 않으면 Windows에서만 이 검사가
        /// 빨개진다 — 이 저장소가 반복해서 겪은 "한 플랫폼에서만 조용히 어긋나는" 실패다.</para></summary>
        private static readonly string[] AllowedLatinWords = { "StickMate", "Ctrl", "Alt", "Win" };

        private static readonly Regex LatinRun = new Regex("[A-Za-z]{3,}");
        private static readonly Regex IssueCode = new Regex(@"\d+-\d+");

        /// <summary>내부에서만 쓰는 개발 어휘. "라운드"는 이 팀의 작업 단위이지 사용자의 시간 단위가 아니다.</summary>
        private static readonly string[] BannedWords = { "라운드", "배선", "P1/P2" };

        private SettingsWindow _window;

        private IEnumerator LoadAndOpen()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            _window.Open("테스트");
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator CloseWindow()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            AppSettingsModel.ResetForTesting();
            yield return null;
        }

        private static GameObject SettingsCanvas()
        {
            GameObject go = GameObject.Find("SettingsCanvas");
            Assert.IsNotNull(go, $"{LogPrefix} 씬에서 SettingsCanvas를 찾지 못했습니다 — " +
                "이름이 바뀌었다면 이 테스트도 함께 고쳐야 합니다.");
            return go;
        }

        // ==================== M6 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NoInternalDeveloperStringIsRenderedInAnyTab()
        {
            yield return LoadAndOpen();

            var offenders = new List<string>();
            foreach (Text t in SettingsCanvas().GetComponentsInChildren<Text>(true))
            {
                string s = t.text;
                if (string.IsNullOrWhiteSpace(s)) continue;

                foreach (Match m in LatinRun.Matches(s))
                {
                    if (System.Array.IndexOf(AllowedLatinWords, m.Value) >= 0) continue;
                    offenders.Add($"[{Path(t.transform)}] 라틴 식별자 \"{m.Value}\" — \"{s}\"");
                }
                if (IssueCode.IsMatch(s))
                    offenders.Add($"[{Path(t.transform)}] 내부 이슈번호 — \"{s}\"");
                for (int i = 0; i < BannedWords.Length; i++)
                {
                    if (s.Contains(BannedWords[i]))
                        offenders.Add($"[{Path(t.transform)}] 개발 어휘 \"{BannedWords[i]}\" — \"{s}\"");
                }
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 사용자 화면에 내부 문자열이 {offenders.Count}건 렌더되고 있습니다:\n  " +
                string.Join("\n  ", offenders) +
                "\n팀이 알아야 할 사정은 SettingsWindow.LogRoadmapNotes()(로그)에 적고, 화면에는 " +
                "사용자 문장만 남깁니다.");
        }

        // ==================== J3 ====================

        /// <summary>
        /// ★ 전체화면 자동 숨김을 <b>끄면</b> 캐릭터만 남는 것이 아니라 창과 그 <b>클릭 차단막</b>까지
        /// 게임 위에 남는다(<c>StickmanAgent.IsSuspended</c>가 이 토글에 매달려 있고, 설정창/팝오버/
        /// 부채꼴이 모두 그것을 본다). 사용자는 캐릭터가 남는 데 동의한 것이지 "클릭이 안 먹는 구멍"에
        /// 동의한 적이 없다 — 캡션이 그 대가를 말해야 한다(원칙 2 + 원칙 1).
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AutoHideToggleCaptionDisclosesTheClickBlockerCost()
        {
            yield return LoadAndOpen();

            Text caption = FindCaption("Row_general.autoHide");
            Assert.IsNotNull(caption,
                $"{LogPrefix} 자동 숨김 토글의 캡션 줄을 찾지 못했습니다 — 행 이름이나 캡션 구조가 " +
                "바뀌었다면 이 테스트도 함께 고쳐야 합니다.");

            string s = caption.text;
            StringAssert.Contains("클릭", s,
                $"{LogPrefix} 자동 숨김 캡션이 <b>클릭</b>을 말하지 않습니다(\"{s}\") — 이 스위치를 끄면 " +
                "전체화면 게임 위에 창과 클릭 차단막이 남습니다. 캐릭터 얘기만 하면 사용자는 그 대가를 " +
                "고지받지 못한 채 동의하게 됩니다(J3).");
            StringAssert.Contains("창", s,
                $"{LogPrefix} 자동 숨김 캡션이 <b>창</b>을 말하지 않습니다(\"{s}\").");
        }

        private static Text FindCaption(string rowName)
        {
            foreach (Transform t in SettingsCanvas().GetComponentsInChildren<Transform>(true))
            {
                if (t.name != rowName) continue;
                Transform cap = t.Find("Caption");
                if (cap != null) return cap.GetComponent<Text>();
            }
            return null;
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            Transform p = t.parent;
            for (int guard = 0; p != null && guard < 4; guard++, p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
