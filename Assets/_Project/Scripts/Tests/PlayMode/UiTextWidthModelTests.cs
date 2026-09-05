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
    /// ★★ <b>「글자 수 × 폭」 모형을 걷어낸 자리</b>를 실물에서 잠근다 — 2026-09-03.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 이 저장소의 창 6곳이 상자 폭을 <c>문자열.Length × 상수</c>로 정하고 있었고, 그 상수는 전부
    /// <b>한글 자간에서 교정</b>된 값이었다(주석이 그렇게 자백하고 있었다). 라틴 자폭은 그 절반
    /// 이하라 상자가 글리프의 두 배 가까이 부풀었고, 왼쪽→오른쪽으로 쌓이는 탭바에서는 그 부풀음이
    /// 누적되어 <b>창 밖으로 밀려났다</b>. 즉 넘침은 «영어가 길어서»가 아니라 <b>모형이 만든 것</b>이다.
    /// 반대로 나눗셈형(보관함 설명 글자 수 상한)은 <b>과소</b>로 나와 자리가 남는데도 글을 잘랐다.
    ///
    /// <para>정답은 같은 저장소가 이미 적어 뒀다 — <c>UiChrome.Ellipsize</c>는
    /// <c>Text.preferredWidth</c>(폰트가 실제로 잰 값)를 쓴다. 이 파일은 그 경로가
    /// <b>실제 창에 도달했는지</b>를 씬에서 확인한다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 스스로에게 거는 제약
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>기대값을 프로덕션 함수로 만들지 않는다.</b> 폭 기대값은
    ///     <c>SettingsControls.MeasuredWidth</c>를 부르지 않고 <c>Mathf.Ceil(text.preferredWidth)</c>로
    ///     <b>테스트가 직접</b> 만든다. 프로덕션 함수를 쓰면 그 함수가 틀어질 때 기대값도 함께
    ///     틀어져 아무것도 못 잰다(TEAM.md «생성기와 검사기가 같이 틀린다»).</item>
    ///   <item><b>모든 0건·부재 판정에 양성 대조를 붙인다.</b> 폰트가 안 올라오면 모든 폭이 0이 되고
    ///     "다 들어간다"가 <b>거짓 초록</b>이 된다 — 그래서 먼저 폰트가 한글과 라틴을 다른 폭으로
    ///     재는지부터 증명한다.</item>
    ///   <item><b>폐기된 상수는 «역사»로만 적는다.</b> <c>11f</c>/<c>9f</c>/<c>14f</c>는 지금 프로덕션에
    ///     없다. 여기 적는 것은 프로덕션 상수의 복사가 아니라 <b>되돌리면 어떻게 되는가</b>를 보이는
    ///     음성 대조다(그 값이 살아 있다면 부재 단언 쪽이 먼저 실패한다).</item>
    /// </list>
    /// </summary>
    public sealed class UiTextWidthModelTests
    {
        private const string LogPrefix = "[글자폭모형-TEST]";

        /// <summary>렌더러가 잰 폭과 상자 폭을 견주는 허용 오차(pt). 상자 폭은 <c>Ceil</c>로 만들어졌고
        /// <c>preferredWidth</c>는 float이므로, 같은 값이라도 마지막 자리에서 갈릴 수 있다.
        /// 1pt 미만이면 화면에서 구분되지 않는다(캔버스 1pt = 1px 격자).</summary>
        private const float MeasureEpsilon = 0.5f;

        // ==================== 폐기된 모형(역사) — 음성 대조 전용 ====================

        /// <summary>설정창 탭 라벨의 옛 «한 글자 폭»(<c>SettingsWindow.TabLabelCharWidth</c>, 삭제됨).</summary>
        private const float RetiredSettingsTabCharWidth = 11f;

        /// <summary>정보창 탭의 옛 식은 <c>글자 수 × UiChrome.FontTitle + 4f</c>였다(삭제됨).
        /// 계수가 폰트 크기 그 자체였으므로 여기서도 <c>UiChrome.FontTitle</c>로 재현한다.</summary>
        private const float RetiredInfoTabPad = 4f;

        /// <summary>보관함 설명 칸의 옛 «10pt 한글 한 글자 폭»(<c>CaptionKoreanAdvance</c>, 삭제됨).</summary>
        private const float RetiredCaptionKoreanAdvance = 11f;

        private SettingsWindow _settings;
        private CharacterInfoWindow _info;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            if (_info != null && _info.IsOpen) _info.Close("테스트 정리");
            _settings = null;
            _info = null;
            AppSettingsModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private IEnumerator OpenSettings()
        {
            yield return LoadScene();
            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            _settings.Open("테스트");
            yield return null;
            yield return null;
        }

        private IEnumerator OpenInfo()
        {
            yield return LoadScene();
            _info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            _info.Toggle("테스트");
            Assert.IsTrue(_info.IsOpen, $"{LogPrefix} 정보창이 열리지 않았습니다.");
            yield return null;
            yield return null;
        }

        private static GameObject CanvasNamed(string name)
        {
            GameObject go = GameObject.Find(name);
            Assert.IsNotNull(go, $"{LogPrefix} {name}을(를) 찾지 못했습니다.");
            return go;
        }

        /// <summary>이름이 정확히 일치하는 자손을 전부 모은다(꺼진 것 포함).</summary>
        private static List<Transform> Descendants(GameObject root, string exactName)
        {
            var found = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == exactName) found.Add(t);
            }
            return found;
        }

        /// <summary>테스트가 <b>직접</b> 만드는 폭 기대값. 프로덕션의 같은 이름 함수를 부르지 않는다.</summary>
        private static float InkWidth(Text text, string content)
        {
            text.text = content ?? string.Empty;
            return Mathf.Ceil(text.preferredWidth);
        }

        // ====================================================================
        // ★ 양성 대조 — 이것이 깨지면 아래 모든 숫자가 무효다
        // ====================================================================

        /// <summary>
        /// 폰트가 <b>실제로</b> 폭을 재는가, 그리고 한글과 라틴을 <b>다르게</b> 재는가.
        ///
        /// <para>이 검사가 없으면 배치모드에서 폰트가 안 올라와 모든 <c>preferredWidth</c>가 0이 되어도
        /// "다 들어간다"가 초록이 된다 — 이 저장소 거짓 통과의 4번 형태(부재 판정에 양성 대조가 없다).
        /// 그리고 한글:라틴 폭비가 1에 가깝다면 애초에 «글자 수 모형»이 틀렸다는 이 라운드의 전제가
        /// 성립하지 않으므로, 그 사실도 여기서 함께 못박는다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 양성대조_폰트가_한글과_라틴을_다른_폭으로_잰다()
        {
            yield return LoadScene();

            var host = new GameObject("폭측정", typeof(Canvas));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Text probe = UiChrome.AddText(host.transform, "Probe", UiChrome.FontBody,
                    TextAnchor.MiddleLeft, UiChrome.TextPrimary);

                float hangul = InkWidth(probe, "가나다라마");
                float latin = InkWidth(probe, "abcde");
                float space = InkWidth(probe, "     ");

                Assert.Greater(hangul, 0f,
                    $"{LogPrefix} 한글 5자의 폭이 0입니다 — 폰트가 올라오지 않았습니다. " +
                    "이 파일의 모든 숫자를 폐기하십시오.");
                Assert.Greater(latin, 0f, $"{LogPrefix} 라틴 5자의 폭이 0입니다(폰트 미로딩).");

                Debug.Log($"{LogPrefix} 5자 기준 실측 폭({UiChrome.FontBody}pt) — " +
                          $"한글 {hangul:F1}pt / 라틴 {latin:F1}pt / 공백 {space:F1}pt. " +
                          $"한글:라틴 = {hangul / latin:F2} : 1");

                Assert.Greater(hangul, latin,
                    $"{LogPrefix} 한글 5자({hangul:F1}pt)가 라틴 5자({latin:F1}pt)보다 넓지 않습니다. " +
                    "그렇다면 «글자 수 상한은 한글에서만 맞다»는 이 라운드의 전제가 틀린 것이고, " +
                    "모형 교체의 근거 문단을 다시 써야 합니다.");
                Assert.Less(space, latin,
                    $"{LogPrefix} 공백 5칸이 라틴 5자만큼 넓습니다 — 그렇다면 글자 수 모형이 " +
                    "«접근성 · 성능»의 공백에 한글 한 글자를 물린 것도 오류가 아니게 됩니다.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ====================================================================
        // A. 설정창 탭바
        // ====================================================================

        /// <summary>
        /// 설정창 탭 상자가 <b>실측 잉크 폭</b>으로 만들어졌고, 한국어 현행 배치가 창 안에 있는가.
        ///
        /// <para>탭마다 «상자 폭 == 테스트가 스스로 잰 잉크 폭»을 단언한다. 이것이 통과하면
        /// 프로덕션이 글자 수가 아니라 폰트에게 물었다는 것이 <b>구조적으로</b> 증명된다 —
        /// 글자 수 모형으로는 이 등식을 우연히 맞출 수 없다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설정창_탭상자는_실측_잉크폭과_같고_한국어는_창안에_있다()
        {
            yield return OpenSettings();

            GameObject canvas = CanvasNamed("SettingsCanvas");
            var report = new System.Text.StringBuilder();
            report.Append(LogPrefix).Append(" 설정창 탭바 — 옛 모형(글자수 × ")
                  .Append(RetiredSettingsTabCharWidth).Append("pt) 대 실측\n");

            float end = 0f;
            float prevRight = float.NegativeInfinity;
            int probed = 0;
            float retiredTotal = 0f;
            float measuredTotal = 0f;

            for (int i = 0; i < 16; i++)
            {
                List<Transform> tabs = Descendants(canvas, "Tab" + i);
                if (tabs.Count == 0) break;
                Assert.AreEqual(1, tabs.Count, $"{LogPrefix} Tab{i}이 {tabs.Count}개입니다.");

                var tabRect = (RectTransform)tabs[0];
                Transform labelT = tabs[0].Find("Label");
                Assert.IsNotNull(labelT, $"{LogPrefix} Tab{i}에 Label이 없습니다.");
                var label = labelT.GetComponent<Text>();
                var labelRect = (RectTransform)labelT;

                float ink = InkWidth(label, label.text);
                Assert.AreEqual(ink, labelRect.rect.width, MeasureEpsilon,
                    $"{LogPrefix} 탭 «{label.text}»의 라벨 상자가 {labelRect.rect.width:F1}pt인데 " +
                    $"실측 잉크는 {ink:F1}pt입니다 — 상자를 폰트가 아니라 다른 규칙(글자 수?)으로 " +
                    "정하고 있습니다.");

                float retired = label.text.Length * RetiredSettingsTabCharWidth;
                retiredTotal += retired;
                measuredTotal += ink;
                report.Append("    ").Append(label.text).Append("\t옛 ").Append($"{retired,6:F1}")
                      .Append("pt\t실측 ").Append($"{ink,6:F1}").Append("pt\t차 ")
                      .Append($"{ink - retired,+6:F1}").Append("pt\n");

                // 겹치지 않는가 — 탭이 왼쪽에서 오른쪽으로 순서대로 놓여야 한다.
                float left = tabRect.anchoredPosition.x;
                float right = left + tabRect.rect.width;
                Assert.GreaterOrEqual(left, prevRight - MeasureEpsilon,
                    $"{LogPrefix} 탭 {i}(«{label.text}»)가 앞 탭과 겹칩니다.");
                prevRight = right;
                end = right;

                // 배지가 붙은 탭은 라벨과 배지가 겹치면 안 된다.
                Transform badgeT = tabs[0].Find("Badge");
                if (badgeT != null)
                {
                    var badge = badgeT.GetComponent<Text>();
                    var badgeRect = (RectTransform)badgeT;
                    float badgeInk = InkWidth(badge, badge.text);
                    Assert.AreEqual(badgeInk, badgeRect.rect.width, MeasureEpsilon,
                        $"{LogPrefix} 배지 «{badge.text}» 상자({badgeRect.rect.width:F1}pt)가 " +
                        $"실측 잉크({badgeInk:F1}pt)와 다릅니다.");
                    Assert.GreaterOrEqual(badgeRect.anchoredPosition.x,
                        labelRect.anchoredPosition.x + labelRect.rect.width - MeasureEpsilon,
                        $"{LogPrefix} 탭 «{label.text}»의 배지가 라벨 위로 올라탔습니다.");
                    Assert.LessOrEqual(badgeRect.anchoredPosition.x + badgeRect.rect.width,
                        tabRect.rect.width + MeasureEpsilon,
                        $"{LogPrefix} 탭 «{label.text}»의 배지가 자기 탭 상자를 넘어 옆 탭을 침범합니다.");
                }
                probed++;
            }

            Assert.Greater(probed, 0, $"{LogPrefix} 탭을 한 개도 찾지 못했습니다(검사가 공허합니다).");

            report.Append("    합계\t옛 ").Append($"{retiredTotal:F1}").Append("pt\t실측 ")
                  .Append($"{measuredTotal:F1}").Append("pt\t차 ").Append($"{measuredTotal - retiredTotal:+0.0;-0.0}")
                  .Append("pt\n    마지막 탭 끝 ").Append($"{end:F1}").Append("pt / 창 폭 ")
                  .Append($"{SettingsWindow.PanelWidth:F0}").Append("pt / 여유 ")
                  .Append($"{SettingsWindow.PanelWidth - end:F1}").Append("pt");
            Debug.Log(report.ToString());

            Assert.LessOrEqual(end, SettingsWindow.PanelWidth,
                $"{LogPrefix} 탭 {probed}개가 {end:F1}pt에서 끝나 창 폭 " +
                $"{SettingsWindow.PanelWidth:F0}pt를 넘었습니다 — 탭바는 마스크 밖이라 잘리지도 않고 " +
                "창 밖에 글자가 뜹니다.");
        }

        // ====================================================================
        // B. 정보창 탭바
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 정보창_탭상자는_실측_잉크폭에_여백만_더한_값이다()
        {
            yield return OpenInfo();

            GameObject canvas = CanvasNamed("CharacterInfoCanvas");
            var report = new System.Text.StringBuilder();
            report.Append(LogPrefix).Append(" 정보창 탭바 — 옛 모형(글자수 × ")
                  .Append(UiChrome.FontTitle).Append("pt + ").Append(RetiredInfoTabPad).Append("pt) 대 실측\n");

            // ★ 2026-09-05 — 옛 한계선은 <c>TabBottomLine</c>(우측 컬럼 밑줄)이었다. 3컬럼 이식으로
            //   탭이 헤더 안의 <b>칩 스트립</b>이 되면서 그 물건이 사라졌다. 지금 탭이 넘치면 안 되는
            //   상대는 <b>스트립 상자 자신</b>이다 — 탭은 그 안에 들어 있어야 하고, 넘치면 헤더 오른쪽
            //   칩 무리와 겹쳐 "탭을 눌렀는데 [설정]이 열린다"가 된다.
            List<Transform> strips = Descendants(canvas, "TabStrip");
            Assert.AreEqual(1, strips.Count, $"{LogPrefix} TabStrip을 {strips.Count}개 찾았습니다.");
            var lineRect = (RectTransform)strips[0];
            float limit = lineRect.rect.width;
            Assert.Greater(limit, 0f, $"{LogPrefix} 탭 스트립 폭이 {limit:F1}pt입니다(측정 실패).");

            int probed = 0;
            float end = 0f;
            float pad = float.NaN;
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Tab", System.StringComparison.Ordinal)) continue;
                if (t.name == "TabStrip") continue;
                Transform labelT = t.Find("Label");
                if (labelT == null) continue;
                var label = labelT.GetComponent<Text>();
                if (label == null || label.fontSize != UiChrome.FontTitle) continue;

                var rect = (RectTransform)t;
                float ink = InkWidth(label, label.text);
                float thisPad = (rect.rect.width - ink) * 0.5f;

                // 여백은 탭마다 같아야 한다 — 다르면 폭 규칙이 두 벌이라는 뜻이다.
                if (float.IsNaN(pad)) pad = thisPad;
                Assert.AreEqual(pad, thisPad, MeasureEpsilon,
                    $"{LogPrefix} 탭 «{label.text}»의 좌우 여백이 {thisPad:F1}pt로 다른 탭({pad:F1}pt)과 " +
                    "다릅니다 — 폭 규칙이 한 벌이 아닙니다.");
                Assert.GreaterOrEqual(rect.rect.width, ink - MeasureEpsilon,
                    $"{LogPrefix} 탭 «{label.text}» 상자({rect.rect.width:F1}pt)가 잉크({ink:F1}pt)보다 좁습니다.");

                float retired = label.text.Length * UiChrome.FontTitle + RetiredInfoTabPad;
                report.Append("    ").Append(label.text).Append("\t옛 ").Append($"{retired,6:F1}")
                      .Append("pt\t실측상자 ").Append($"{rect.rect.width,6:F1}").Append("pt\t차 ")
                      .Append($"{rect.rect.width - retired,+6:F1}").Append("pt\n");

                end = Mathf.Max(end, rect.anchoredPosition.x + rect.rect.width);
                probed++;
            }

            Assert.Greater(probed, 1, $"{LogPrefix} 정보창 탭을 {probed}개만 찾았습니다(검사가 공허합니다).");
            report.Append("    마지막 탭 끝 ").Append($"{end:F1}").Append("pt / 스트립 폭 ")
                  .Append($"{limit:F1}").Append("pt / 여유 ").Append($"{limit - end:F1}").Append("pt");
            Debug.Log(report.ToString());

            Assert.LessOrEqual(end, limit,
                $"{LogPrefix} 탭이 {end:F1}pt에서 끝나 스트립 상자({limit:F1}pt)를 넘겼습니다 — " +
                "마지막 탭이 상자 밖으로 나가 헤더 오른쪽 칩과 겹칩니다.");
        }

        // ====================================================================
        // C. 설정창 세그먼트·버튼 칩
        // ====================================================================

        /// <summary>
        /// 행 오른쪽 칩(세그먼트/버튼)의 <b>글자가 칩 밖으로 새지 않는가</b>.
        ///
        /// <para>이 칩들은 오른쪽 끝에서 왼쪽으로 쌓이므로, 부풀린 폭은 그대로 <b>라벨 쪽으로</b>
        /// 자란다. 그래서 잉크가 상자를 넘는지와 함께, 칩 덩어리가 행 라벨을 덮지 않는지도 본다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설정창_칩의_글자는_칩안에_있고_라벨을_덮지_않는다()
        {
            yield return OpenSettings();

            GameObject canvas = CanvasNamed("SettingsCanvas");
            int probed = 0;
            float worstSlack = float.MaxValue;
            string worstChip = string.Empty;

            foreach (Transform row in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (!row.name.StartsWith("Row_", System.StringComparison.Ordinal)) continue;

                Transform rowLabelT = row.Find("Label");
                float labelRight = 0f;
                if (rowLabelT != null)
                {
                    var rowLabel = rowLabelT.GetComponent<Text>();
                    // 라벨 상자는 420pt로 넉넉히 잡혀 있다 — 실제로 글자가 차지한 만큼만 본다.
                    labelRight = ((RectTransform)rowLabelT).anchoredPosition.x
                                 + (rowLabel != null ? rowLabel.preferredWidth : 0f);
                }

                var rowRect = (RectTransform)row;
                foreach (Transform chip in row)
                {
                    bool isChip = chip.name.StartsWith("Seg", System.StringComparison.Ordinal)
                                  || chip.name.StartsWith("Btn", System.StringComparison.Ordinal);
                    if (!isChip) continue;
                    Transform chipLabelT = chip.Find("Label");
                    if (chipLabelT == null) continue;
                    var chipLabel = chipLabelT.GetComponent<Text>();
                    if (chipLabel == null) continue;

                    var chipRect = (RectTransform)chip;
                    float ink = InkWidth(chipLabel, chipLabel.text);
                    Assert.GreaterOrEqual(chipRect.rect.width, ink - MeasureEpsilon,
                        $"{LogPrefix} 칩 «{chipLabel.text}»({row.name})의 상자가 {chipRect.rect.width:F1}pt인데 " +
                        $"글자는 {ink:F1}pt입니다 — 글자가 칩 밖으로 샙니다.");

                    // 오른쪽 정렬이므로 칩의 왼쪽 끝 = 행 폭 − |anchoredPosition.x| − 폭.
                    float chipLeft = rowRect.rect.width + chipRect.anchoredPosition.x - chipRect.rect.width;
                    float slack = chipLeft - labelRight;
                    if (slack < worstSlack) { worstSlack = slack; worstChip = $"{row.name}/«{chipLabel.text}»"; }
                    probed++;
                }
            }

            Assert.Greater(probed, 0,
                $"{LogPrefix} 세그먼트/버튼 칩을 한 개도 찾지 못했습니다 — 이름 규칙이 바뀌었다면 " +
                "이 검사는 지금 아무것도 재지 않습니다(거짓 초록).");
            Debug.Log($"{LogPrefix} 칩 {probed}개 검사. 라벨↔칩 최소 여유 {worstSlack:F1}pt ({worstChip})");
            Assert.Greater(worstSlack, 0f,
                $"{LogPrefix} {worstChip}의 칩이 행 라벨 글자를 {-worstSlack:F1}pt 덮습니다.");
        }

        // ====================================================================
        // D. 보관함 설명 칸
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 보관함_설명은_글자수가_아니라_칸_폭으로_잘린다()
        {
            yield return OpenInfo();

            // [보관함] 탭으로 — 실제 클릭 경로로 간다(테스트 전용 분기를 만들지 않는다).
            Rect tabRect = _info.TabScreenRect(2);
            Assert.Greater(tabRect.width, 0f, $"{LogPrefix} [보관함] 탭 사각형이 비었습니다.");
            _info.FeedClickForTests(tabRect.center);
            yield return null;
            yield return null;

            GameObject canvas = CanvasNamed("CharacterInfoCanvas");
            int probed = 0;
            int truncated = 0;
            int wouldTruncateByRetiredModel = 0;
            float boxWidth = 0f;

            foreach (Transform t in Descendants(canvas, "Description"))
            {
                var text = t.GetComponent<Text>();
                if (text == null || string.IsNullOrEmpty(text.text)) continue;
                var rect = (RectTransform)t;
                boxWidth = rect.rect.width;

                float ink = text.preferredWidth;
                Assert.LessOrEqual(ink, boxWidth + MeasureEpsilon,
                    $"{LogPrefix} 설명 «{text.text}»의 잉크가 {ink:F1}pt로 칸 {boxWidth:F1}pt를 넘칩니다 — " +
                    "말줄임이 걸리지 않았습니다.");

                if (text.text.EndsWith(UiChrome.Ellipsis, System.StringComparison.Ordinal)) truncated++;
                probed++;
            }

            Assert.Greater(probed, 0,
                $"{LogPrefix} 설명이 채워진 보관함 줄을 한 개도 찾지 못했습니다 — 검사가 공허합니다.");

            // 옛 모형이라면 몇 줄이 잘렸을까 — 같은 카탈로그 문구로 계산해 대조한다.
            int retiredChars = Mathf.Max(8, Mathf.FloorToInt(boxWidth / RetiredCaptionKoreanAdvance));
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                if (entry == null || string.IsNullOrEmpty(entry.ShortDescription)) continue;
                if (entry.ShortDescription.Length > retiredChars) wouldTruncateByRetiredModel++;
            }

            Debug.Log($"{LogPrefix} 보관함 설명 — 칸 폭 {boxWidth:F1}pt. 화면에 뜬 {probed}줄 중 " +
                      $"{truncated}줄이 폭 기준으로 잘렸다. 옛 글자 수 모형이었다면 상한 {retiredChars}자 " +
                      $"기준으로 카탈로그 전체에서 {wouldTruncateByRetiredModel}건이 잘렸을 것이다.");

            Assert.Greater(retiredChars, 0,
                $"{LogPrefix} 옛 모형의 글자 수 상한이 0 이하로 나왔습니다 — 대조가 공허합니다.");
        }

        // ====================================================================
        // E. 최악 칸 — [지금 종료] × Windows 표기
        // ====================================================================

        /// <summary>
        /// ★★ <b>이 앱에서 가장 위험한 칸</b>이다. 라벨이 플랫폼마다 물리적으로 다르고
        /// (<c>ShortcutLabel.MacModifiers</c> 3자 vs <c>WindowsModifiers</c> 13자), 그 라벨은
        /// <c>HorizontalWrapMode.Overflow</c>라 넘쳐도 <b>잘리지 않고 칩 밖으로 흘러</b>
        /// "테두리를 뚫고 나온 글자"가 된다.
        ///
        /// <para>이 개발 머신에서 Windows 빌드를 실행할 수 없으므로, <b>표기만 Windows 것으로 바꾼
        /// 같은 문자열</b>을 같은 폰트로 재서 필요한 폭을 구한다. 폰트 폴백까지 같지는 않지만
        /// (Windows의 한글 폴백은 다른 글꼴이다), <b>라틴 부분이 10자 늘어난다</b>는 사실은
        /// 플랫폼과 무관하게 참이고 그것이 이 칸을 깨뜨리는 요인이다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 종료칩은_자기_라벨을_담고_Windows_표기의_필요폭도_푸터에_들어간다()
        {
            yield return OpenSettings();

            GameObject canvas = CanvasNamed("SettingsCanvas");
            List<Transform> quits = Descendants(canvas, "Quit");
            Assert.AreEqual(1, quits.Count, $"{LogPrefix} 푸터 [지금 종료] 칩을 {quits.Count}개 찾았습니다.");
            Transform labelT = quits[0].Find("Label");
            Assert.IsNotNull(labelT, $"{LogPrefix} 종료 칩에 Label이 없습니다.");
            var label = labelT.GetComponent<Text>();
            var chip = (RectTransform)quits[0];

            string hostText = label.text;
            Assert.IsNotEmpty(hostText, $"{LogPrefix} 종료 칩 라벨이 비었습니다.");

            float hostInk = InkWidth(label, hostText);
            Assert.LessOrEqual(hostInk + SettingsControls.ButtonPadX * 2f, chip.rect.width + MeasureEpsilon,
                $"{LogPrefix} 종료 칩({chip.rect.width:F1}pt)이 자기 라벨 «{hostText}»" +
                $"({hostInk:F1}pt + 여백 {SettingsControls.ButtonPadX * 2f:F0}pt)를 담지 못합니다.");

            // Windows 표기로 바꾼 같은 문안. 문구를 베끼지 않고 <b>실물에서 파생</b>한다.
            string winText = hostText.Replace(ShortcutLabel.Chord("Q"), ShortcutLabel.WindowsChord("Q"));
            if (!ShortcutLabel.HostUsesWindowsNotation)
            {
                Assert.AreNotEqual(hostText, winText,
                    $"{LogPrefix} macOS 호스트인데 Windows 표기로 바뀐 것이 없습니다 — 파생이 죽었습니다" +
                    "(그러면 아래 Windows 숫자는 macOS 숫자를 다시 적은 것일 뿐입니다).");
            }
            float winInk = InkWidth(label, winText);
            label.text = hostText;   // 실물을 원상 복구한다.

            float winNeeded = winInk + SettingsControls.ButtonPadX * 2f;

            // 푸터 아랫줄에서 이 칩이 쓸 수 있는 자리 = 창 폭 − 좌우 여백 − 닫기 안내 글자.
            List<Transform> hints = Descendants(canvas, "CloseHint");
            Assert.AreEqual(1, hints.Count, $"{LogPrefix} 푸터 CloseHint를 {hints.Count}개 찾았습니다.");
            var hint = hints[0].GetComponent<Text>();
            float hintRight = ((RectTransform)hints[0]).anchoredPosition.x + hint.preferredWidth;
            float available = SettingsWindow.PanelWidth - SettingsWindow.ContentPadX - hintRight;

            Debug.Log($"{LogPrefix} 종료 칩 — 호스트 «{hostText}» 잉크 {hostInk:F1}pt / 칩 {chip.rect.width:F1}pt. " +
                      $"Windows 표기 «{winText}» 잉크 {winInk:F1}pt → 필요 폭 {winNeeded:F1}pt. " +
                      $"푸터 아랫줄 가용 폭 {available:F1}pt(안내 글자 끝 {hintRight:F1}pt).");

            Assert.Greater(available, 0f,
                $"{LogPrefix} 푸터 아랫줄에 종료 칩 자리가 없습니다(안내 글자가 {hintRight:F1}pt까지 찹니다).");
            Assert.LessOrEqual(winNeeded, available,
                $"{LogPrefix} Windows 표기의 종료 칩이 {winNeeded:F1}pt를 필요로 하는데 푸터 아랫줄에는 " +
                $"{available:F1}pt뿐입니다 — Windows에서 종료 칩이 닫기 안내 글자를 덮습니다. " +
                "칩 폭이 아니라 문안·배치를 고쳐야 하고 그것은 ux-designer 소관입니다.");
        }

        // ====================================================================
        // F. [일반] 탭 넘침 전제 — 다른 테스트가 이것 위에 서 있다
        // ====================================================================

        /// <summary>
        /// <c>SettingsDisabledSurfaceTests.RailChipsGoDeadAtTheEndsInsteadOfLookingClickable</c>은
        /// <b>"[일반] 탭은 내용이 넘친다"</b>를 전제로 [▲][▼]의 죽은/산 잉크를 검사한다.
        /// 그 전제가 거짓이 되는 날 그 테스트는 <b>실패하지 않고 공허해진다</b> — 이 저장소가 가장
        /// 자주 당한 형태다. 그래서 전제를 <b>여기서 숫자로</b> 잠근다.
        ///
        /// <para>이 라운드는 폭만 건드렸으므로 세로 예산은 바뀌지 않아야 한다. 넘침이 0이 되면
        /// 그것 자체가 이 라운드가 세로에 손댔다는 신호다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 일반탭은_여전히_넘친다_레일칩_검사의_전제()
        {
            yield return OpenSettings();

            float overflow = _settings.PageOverflowPointsForTests(SettingsWindow.Tab.General);
            Debug.Log($"{LogPrefix} [일반] 탭 넘침 {overflow:F1}pt (뷰포트 " +
                      $"{SettingsWindow.ContentHeight:F0}pt).");

            Assert.Greater(overflow, 0f,
                $"{LogPrefix} [일반] 탭이 더 이상 넘치지 않습니다({overflow:F1}pt). " +
                "SettingsDisabledSurfaceTests의 레일 칩 검사가 «맨 위에서 [▼]는 살아 있다»를 " +
                "확인할 수 없게 되어 조용히 공허해집니다 — 그 테스트를 함께 고치십시오.");

            // 대조군: 넘치지 않는 탭이 실제로 있는가(이 계측기가 항상 양수를 내는 것은 아닌가).
            float dataOverflow = _settings.PageOverflowPointsForTests(SettingsWindow.Tab.Data);
            Debug.Log($"{LogPrefix} 대조 — [데이터] 탭 넘침 {dataOverflow:F1}pt.");
            Assert.Less(dataOverflow, overflow,
                $"{LogPrefix} 준비 중인 [데이터] 탭이 [일반] 탭만큼 넘칩니다 — 계측기가 탭을 " +
                "구분하지 못하는 것 아닌지 의심해야 합니다(위 단언이 공허할 수 있습니다).");
        }
    }
}
