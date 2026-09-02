using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 회귀 잠금 — <b>포스트잇 펼침은 상한이 있고 스스로 접힌다.</b>
    /// (절대 불변 원칙 2: 비침해)
    ///
    /// ============================================================================
    /// 무엇이 출하돼 있었나 (docs/UX_WIDGETS.md §3-2, 2026-09-02)
    /// ============================================================================
    /// <c>TodoPostItWidget._expanded</c>를 되돌리는 경로가 <b>사용자 재클릭 하나뿐</b>이었다.
    /// 소프트캡 15건을 펼치면 카드가 220×472 = 103,840pt²(화면 6.994%)가 되고,
    /// <c>SyncClickThroughBlocker()</c>가 그 사각형 <b>전부</b>를 덮으므로 그만큼의 클릭관통이
    /// <b>무기한</b> 해제된 채 남았다. 24시간 상주 앱에서 "펼쳐 두고 잊는다"는 예외가 아니다.
    ///
    /// ============================================================================
    /// 이 파일이 재는 것 / 재지 않는 것
    /// ============================================================================
    /// 여기서는 <b>계약</b>만 잰다 — 상수의 정본이 하나인가, 접힘 사유가 로그에서 갈리는가.
    /// 실제로 8행에서 멈추고 시간이 지나면 접히는가는 PlayMode의
    /// <c>TodoPostItExpansionTests</c>가 <b>동작</b>으로 잰다. 두 파일은 서로를 대체하지 않는다.
    ///
    /// <para>소스 스캔이라 씬 조립도, 플랫폼도, 실행도 필요 없다(양 플랫폼 공통 코드를 재는 것이므로
    /// macOS/Windows 어느 쪽에서 돌려도 같은 답이다).</para>
    /// </summary>
    public sealed class TodoPostItExpansionAuditTests
    {
        private static string ReadScript(params string[] relative)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts");
            foreach (string part in relative) path = Path.Combine(path, part);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        [Test]
        public void 무입력_임계는_팝오버_상수를_그대로_참조한다()
        {
            // 값 자체를 다른 방법으로 다시 잰다 — 숫자를 베끼지 않고 두 상수가 같은 값인지 본다.
            // ★ 먼저 되돌린다: 이 정적 손잡이는 PlayMode 테스트들이 낮췄다 되돌리는 값이라
            //   실행 순서에 기대면 안 된다.
            PopoverPanel.ResetIdleAutoCloseSecondsForTests();
            Assert.AreEqual(PopoverPanel.DefaultIdleAutoCloseSeconds, TodoPostItWidget.IdleAutoCollapseSeconds,
                "포스트잇 펼침의 무입력 임계가 팝오버와 갈라졌다 — '자리를 비웠다'의 기준이 앱 안에 " +
                "두 벌이 되면 어느 쪽이 정본인지 아무도 모른다.");

            // ★ 같은 값인 것과 <b>같은 손잡이</b>인 것은 다르다. 두 곳에 180f를 각각 적어 둬도 위
            //   단언은 통과한다. 그래서 손잡이를 실제로 움직여 따라오는지 본다(양성 대조).
            try
            {
                PopoverPanel.SetIdleAutoCloseSecondsForTests(7.5f);
                Assert.AreEqual(7.5f, TodoPostItWidget.IdleAutoCollapseSeconds, 0.0001f,
                    "팝오버 임계를 7.5초로 바꿨는데 포스트잇이 따라오지 않는다 — 값을 복사한 것이다.");
            }
            finally
            {
                PopoverPanel.ResetIdleAutoCloseSecondsForTests();
            }

            // 그리고 그 동일성이 <b>우연</b>이 아니라 참조라는 것을 소스로 못박는다.
            // (두 곳에 180f를 각각 적어 두면 위 단언은 통과하고 다음 라운드에 조용히 갈라진다.)
            string source = ReadScript("Interaction", "TodoPostItWidget.cs");
            Assert.Greater(source.IndexOf("PopoverPanel.IdleAutoCloseSeconds", StringComparison.Ordinal), 0,
                "포스트잇이 PopoverPanel의 임계를 참조하지 않는다 — 값을 복사하지 마라.");
            Assert.AreEqual(0, CountOf(source, "180f"),
                "포스트잇에 180f 리터럴이 생겼다 — 임계의 정본은 PopoverPanel 하나여야 한다.");
        }

        [Test]
        public void 자동_접힘은_사용자_동작과_사유가_갈려_로그에_남는다()
        {
            string source = ReadScript("Interaction", "TodoPostItWidget.cs");

            // 부채꼴이 겪은 것과 같은 문제다: 사유를 갈라 찍지 않으면 로그만 보는 다음 사람이
            // "사용자가 접었다"로 읽는다. GearRadialMenuWidget.ModeLabel()과 같은 어법을 쓴다.
            Assert.Greater(source.IndexOf("\"무반응 자동\"", StringComparison.Ordinal), 0,
                "자동 접힘 사유 표기(\"무반응 자동\")가 없다 — 부채꼴과 어법이 갈라졌다.");
            Assert.Greater(source.IndexOf("\"사용자 동작\"", StringComparison.Ordinal), 0,
                "사용자 접힘 사유 표기(\"사용자 동작\")가 없다.");
            Assert.Greater(source.IndexOf("[투두] 펼침 접힘({reasonKind})", StringComparison.Ordinal), 0,
                "접힘 로그가 사유를 끼워 찍지 않는다.");

            // 부채꼴 쪽 문구와 실제로 같은지 대조한다 — 한쪽만 고쳐 어법이 갈라지는 것을 막는다.
            string gear = ReadScript("Interaction", "GearRadialMenuWidget.cs");
            Assert.Greater(gear.IndexOf("\"무반응 자동\"", StringComparison.Ordinal), 0,
                "부채꼴 쪽 \"무반응 자동\" 표기가 사라졌다 — 이 대조가 무의미해졌으니 두 곳을 함께 고쳐라.");
        }

        [Test]
        public void 펼침_행_상한이_집중_팝오버_면적을_넘지_않는다()
        {
            // docs/UX_WIDGETS.md §1-1의 기준: 상시 카드가 "사용자가 직접 연 창"보다 커서는 안 된다.
            // 집중 팝오버(244 × 252 = 61,488pt²)가 그 상한이다 — 숫자를 베끼지 않고 그쪽 소스에서 읽는다.
            string focus = ReadScript("Interaction", "FocusSessionPopover.cs");
            float focusArea = ReadFloatConst(focus, "Width") * ReadFloatConst(focus, "IdleHeight");
            Assert.Greater(focusArea, 0f, "집중 팝오버 치수를 읽지 못했다.");

            // 카드 기하는 프로덕션 상수에서 다시 계산한다(숫자 베끼기 금지).
            // panelHeight = PanelPadding + n*RowHeight + PanelPadding + RowHeight(헤더 한 줄)
            // ★ PanelWidth/RowHeight/PanelPadding은 private이라 여기서는 소스에서 읽어 온다 —
            //   상수를 public으로 열어 표면 API를 넓히는 것보다 이쪽이 싸다.
            string source = ReadScript("Interaction", "TodoPostItWidget.cs");
            float rowHeight = ReadFloatConst(source, "RowHeight");
            float panelWidth = ReadFloatConst(source, "PanelWidth");
            float padding = UiChrome.Space3;   // PanelPadding = UiChrome.Space3.
            Assert.Greater(source.IndexOf("PanelPadding = UiChrome.Space3", StringComparison.Ordinal), 0,
                "PanelPadding이 더 이상 UiChrome.Space3가 아니다 — 이 계산의 전제가 깨졌다.");

            int n = TodoPostItWidget.ExpandedMaxRows;
            float height = padding + n * rowHeight + padding + rowHeight;
            float area = panelWidth * height;

            Assert.Less(area, focusArea,
                $"펼침 상한 {n}행의 카드 면적이 {area:F0}pt²로 집중 팝오버 {focusArea:F0}pt²를 넘는다 — " +
                "상시 카드가 사용자가 직접 연 창보다 커졌다(docs/UX_WIDGETS.md §3-2(b)).");

            // 한 행만 더 늘려도 넘는다는 것까지 확인한다 — 상한이 '여유 있게 고른 값'이 아니라
            // **경계에 붙어 있는 값**임을 남긴다(다음 사람이 무심코 +1 하지 않도록).
            float areaPlusOne = panelWidth * (height + rowHeight);
            Assert.Greater(areaPlusOne, focusArea,
                $"{n + 1}행({areaPlusOne:F0}pt²)이 아직 상한 아래다 — 상한 근거가 바뀌었으니 " +
                "docs/UX_WIDGETS.md §3-2(b)의 산술과 이 테스트를 함께 갱신하라.");
        }

        /// <summary><c>private const float X = 220f;</c> 형태에서 숫자만 뽑는다.
        /// 값을 테스트에 베끼지 않기 위한 최소 파서다 — 형태가 바뀌면 그 자리에서 빨개진다.</summary>
        private static float ReadFloatConst(string source, string name)
        {
            string key = "const float " + name + " = ";
            int i = source.IndexOf(key, StringComparison.Ordinal);
            Assert.Greater(i, 0, $"상수 {name}을(를) 찾지 못했다 — 선언 형태가 바뀌었으면 이 파서를 고쳐라.");
            int start = i + key.Length;
            int end = source.IndexOfAny(new[] { 'f', ';' }, start);
            Assert.Greater(end, start, $"상수 {name}의 값을 읽지 못했다.");
            float value = float.Parse(source.Substring(start, end - start).Trim(),
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.Greater(value, 0f, $"상수 {name}이(가) 0 이하다 — 파서가 엉뚱한 곳을 읽었다.");
            return value;
        }
    }
}
