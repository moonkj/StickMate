using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 집중 모드 <b>시간 자유 입력</b>의 범위 계약 — docs/UX_WIDGETS.md §R5-3-3.
    ///
    /// ============================================================================
    /// 왜 이 파일이 필요한가 — 하한이 <b>두 곳</b>에 살고 있다
    /// ============================================================================
    /// 스테퍼의 하한 1분은 이 팝오버가 고른 값이 아니라
    /// <see cref="FocusWatchDirector"/>의 <c>MinimumSessionSeconds</c>(60초)가 정한 값이다.
    /// 설계 §R5-6 #15는 그 상수를 <c>public</c>으로 승격해 팝오버가 <b>파생</b>시키라고 요구했지만,
    /// 그 파일은 이 라운드에 다른 담당자가 편집 중이라 손대지 않았다.
    ///
    /// <para>그래서 값이 두 곳에 있고, <b>갈라져도 아무도 모르는</b> 상태가 된다 — 디렉터 하한이
    /// 120초로 올라가면 UI는 여전히 「1분」을 제안하고 <c>StartFocusSession</c>은 조용히 2분으로
    /// 올려 버린다. 화면의 숫자가 거짓이 되는 형태(원칙 1)다. 이 파일이 <b>디렉터 소스를 직접 읽어</b>
    /// 그 갈라짐을 그 라운드에 빨갛게 만든다.</para>
    ///
    /// <para>승격이 끝나면 <c>FocusSessionPopover.MinimumSessionMinutes</c>를
    /// <c>(int)(FocusWatchDirector.MinimumSessionSeconds / 60f)</c>로 바꾸고 이 파일의 ①을 지워라 —
    /// 그때는 컴파일러가 같은 일을 공짜로 한다.</para>
    ///
    /// <para>소스 스캔이라 씬 조립도, 플랫폼도, 실행도 필요 없다(양 플랫폼 공통 코드를 재는 것이므로
    /// macOS/Windows 어느 쪽에서 돌려도 같은 답이다).</para>
    /// </summary>
    public sealed class FocusSessionDurationRangeTests
    {
        private const string LogPrefix = "[집중시간범위]";

        private static string ReadScript(params string[] relative)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts");
            foreach (string part in relative) path = Path.Combine(path, part);
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        /// <summary><c>const float X = 60f;</c> 형태에서 숫자만 뽑는다. 음수도 읽는다.
        /// <b>존재 단언</b>이다 — 니들이 썩으면 조용히 초록이 되는 게 아니라 여기서 실패한다.</summary>
        private static float ReadFloatConst(string source, string name)
        {
            string key = "const float " + name + " = ";
            int i = source.IndexOf(key, StringComparison.Ordinal);
            Assert.Greater(i, 0,
                $"{LogPrefix} 상수 {name}을(를) 찾지 못했다 — 이름이나 선언 형태가 바뀌었으면 " +
                "이 파서를 고쳐라. 못 찾은 채로 통과시키면 이 대조가 빈 검사가 된다.");
            int start = i + key.Length;
            int end = source.IndexOfAny(new[] { 'f', ';' }, start);
            Assert.Greater(end, start, $"{LogPrefix} 상수 {name}의 값을 읽지 못했다.");
            string raw = source.Substring(start, end - start).Trim();
            Assert.IsTrue(float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value),
                $"{LogPrefix} 상수 {name}의 값 \"{raw}\"을(를) 숫자로 읽지 못했다.");
            return value;
        }

        // ==================== ① 하한은 디렉터가 정한다 ====================

        [Test]
        public void 자유입력_하한은_디렉터의_세션_하한과_같은_값이다()
        {
            string director = ReadScript("Interaction", "FocusWatchDirector.cs");
            float directorSeconds = ReadFloatConst(director, "MinimumSessionSeconds");

            // 파서가 엉뚱한 곳을 읽지 않았는지 — 세션 하한이 0 이하이거나 하루보다 길 수는 없다.
            Assert.Greater(directorSeconds, 0f, $"{LogPrefix} 디렉터 하한이 {directorSeconds}초로 읽혔다 — 파서가 틀렸다.");
            Assert.Less(directorSeconds, 86400f, $"{LogPrefix} 디렉터 하한이 {directorSeconds}초로 읽혔다 — 파서가 틀렸다.");

            Assert.AreEqual(directorSeconds / 60f, FocusSessionPopover.MinimumSessionMinutes, 0.0001f,
                $"{LogPrefix} 팝오버 하한 {FocusSessionPopover.MinimumSessionMinutes}분과 디렉터 하한 " +
                $"{directorSeconds:F0}초({directorSeconds / 60f:F2}분)가 갈라졌다.\n" +
                "  · UI가 제안한 길이를 StartFocusSession이 조용히 올려 버리면 화면의 숫자가 거짓이 된다(원칙 1).\n" +
                "  · 고치는 방향은 <b>팝오버가 디렉터를 따라가는</b> 쪽이다 — UI가 디렉터보다 좁을 이유는 있어도 " +
                "넓을 이유는 없다(§R5-3-3).");

            Debug.Log($"{LogPrefix} ① 통과 — 디렉터 {directorSeconds:F0}초 = 팝오버 " +
                $"{FocusSessionPopover.MinimumSessionMinutes}분.");
        }

        // ==================== ② 상한은 다이얼 얼굴에서 파생한다 ====================

        [Test]
        public void 자유입력_상한은_다이얼_한_바퀴에서_파생한다()
        {
            Assert.Greater(FocusSessionPopover.DialSpanSeconds, 0f,
                $"{LogPrefix} 다이얼 한 바퀴가 {FocusSessionPopover.DialSpanSeconds}초다.");

            // 61분은 호가 한 바퀴를 넘어 눈금판이 거짓이 된다 — 상한은 얼굴 그 자체여야 한다.
            Assert.AreEqual(FocusSessionPopover.DialSpanSeconds / 60f,
                FocusSessionPopover.MaximumSessionMinutes, 0.0001f,
                $"{LogPrefix} 상한 {FocusSessionPopover.MaximumSessionMinutes}분이 다이얼 한 바퀴 " +
                $"({FocusSessionPopover.DialSpanSeconds / 60f:F0}분)와 다르다 — 상한을 넘는 세션은 " +
                "호가 한 바퀴를 넘어 <b>숫자판이 거짓</b>이 된다(§R5-1-2).");

            Assert.Less(FocusSessionPopover.MinimumSessionMinutes, FocusSessionPopover.MaximumSessionMinutes,
                $"{LogPrefix} 하한이 상한 이상이다 — 스테퍼가 아무 값도 못 고른다.");

            Debug.Log($"{LogPrefix} ② 통과 — 정의역 {FocusSessionPopover.MinimumSessionMinutes}~" +
                $"{FocusSessionPopover.MaximumSessionMinutes}분이 다이얼 " +
                $"{FocusSessionPopover.DialSpanSeconds / 60f:F0}분 한 바퀴 안에 정확히 닫힌다(넘침 0).");
        }

        // ==================== ③ 두 칩 무리가 서로의 길이를 세지 않는다 (함정 ①) ====================

        [Test]
        public void 시간칩과_민감도칩은_각자_자기_배열_길이를_센다()
        {
            string source = ReadScript("Interaction", "FocusSessionPopover.cs");

            // ★ <b>존재 단언</b>만 쓴다. "for (int i = 0; i < 3; i++)가 없다"는 부재 단언은 썩으면
            //   조용히 초록이 되고(CLAUDE.md), 게다가 표현 방식만 바꾼 같은 버그를 못 본다.
            //   실제로 [직접] 칩이 눌리는가는 PlayMode의 FocusSessionDialTests가 <b>동작</b>으로 잰다 —
            //   이 단언은 그 동작 검사의 대체물이 아니라, 다음 사람이 왜 루프가 둘인지 알게 하는 못이다.
            Assert.Greater(source.IndexOf("i < _durationChips.Length", StringComparison.Ordinal), 0,
                $"{LogPrefix} 시간 칩 루프가 자기 배열의 Length를 세지 않는다 — 칩 개수가 바뀌는 순간 " +
                "마지막 칩이 <b>전역 폴링 경로에서만</b> 조용히 죽는다(uGUI 경로로는 눌린다).");
            Assert.Greater(source.IndexOf("i < _sensitivityChips.Length", StringComparison.Ordinal), 0,
                $"{LogPrefix} 민감도 칩 루프가 자기 배열의 Length를 세지 않는다.");

            Debug.Log($"{LogPrefix} ③ 통과 — 두 칩 무리가 각자 자기 길이를 센다.");
        }

        // ==================== ④ 진행 페이지가 패널 밖으로 나가지 않는다 ====================

        [Test]
        public void 진행_페이지는_대기_페이지와_같은_높이다()
        {
            string source = ReadScript("Interaction", "FocusSessionPopover.cs");
            float idle = ReadFloatConst(source, "IdleHeight");
            float running = ReadFloatConst(source, "RunningHeight");

            // PopoverPanel.BuildChrome()은 Content 사각형을 <b>Awake 시점의 PanelSizePoints.y</b> 하나로
            // 잡아 두고 다시 만들지 않는다. 대기(252)로 잡힌 Content가 진행(224) 패널 위에 얹히면
            // Content가 패널 아래로 28pt 삐져나가고, 그 아래에 무언가를 그리는 순간 <b>패널 밖에</b>
            // 그려진다. 두 높이를 같게 두면 그 함정이 산술적으로 닫힌다(§R5-4-1).
            Assert.AreEqual(idle, running, 0.001f,
                $"{LogPrefix} 대기 {idle:F0} / 진행 {running:F0} — 두 페이지 높이가 갈라졌다. " +
                "Content 사각형은 Awake에 한 번만 만들어지므로, 더 큰 쪽으로 잡힌 Content가 더 작은 " +
                $"페이지에서 {Mathf.Abs(idle - running):F0}pt 삐져나간다(R2-2-1이 잡아 둔 그 함정).");

            Debug.Log($"{LogPrefix} ④ 통과 — 대기/진행 모두 {idle:F0}pt, Content 넘침 0.");
        }
    }
}
