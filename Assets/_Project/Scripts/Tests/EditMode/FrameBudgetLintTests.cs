using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ "프레임 수 = 시간" 함정 자동 감시 (2026-09-01 신설).
    ///
    /// <para><b>왜 린트인가.</b> 이 저장소는 <b>하루에 세 번</b> 같은 함정에 빠졌다. 배치 모드
    /// (<c>-batchmode -nographics</c>) PlayMode는 렌더링이 없어 2,000~9,000fps로 돈다
    /// (실측 0.11~0.45ms/프레임). 그래서 <c>for (int i = 0; i &lt; N; i++) yield return null;</c>로 잡은
    /// "N프레임 예산"이 실제로는 밀리초다:</para>
    /// <code>
    ///     60프레임 = 0.007 ~ 0.027초
    ///    120프레임 = 0.013 ~ 0.054초
    ///    240프레임 = 0.026 ~ 0.108초
    ///    900프레임 = 0.099 ~ 0.405초
    /// </code>
    /// <para>사례: <c>CornerHoverPanelTests</c>(180프레임 예산 vs 0.126초짜리 등장 연출 — 10/10
    /// 결정적 실패를 네 라운드 동안 "불안정한 테스트"로 오진), <c>CharacterVisualHalfWidthTests</c>
    /// (900프레임 표본이 통째로 착지 동작 안에 갇힘), <c>CornerHoverPanelTests</c>의 폴링 감시
    /// (120프레임 = 0.013초 &lt; 폴링 주기 0.05초 → 폴링이 한 번도 안 돌았다),
    /// <c>AccessoryFillRenderingTests</c>(60프레임 = sway 한 주기의 1.1% → 정지 화면 한 장을 봤다),
    /// <c>CharacterAppearanceLayerTests</c>의 네거티브 컨트롤(240프레임 예산의 미발동 확률 약 7%).
    /// 사람이 리뷰로 잡는 데 네 번 실패했으므로 <b>기계가 잡는다</b>.</para>
    ///
    /// <para><b>무엇을 잡고 무엇을 안 잡나.</b> 프레임 수가 <b>정당한</b> 자리도 많다 — "다음 프레임에
    /// 반영되는가", "LateUpdate 재구성이 한 바퀴 돌았는가" 같은 <b>구조적</b> 대기가 그렇다. 그래서
    /// 이 린트는 <b>큰</b> 프레임 예산만(<see cref="MaxFramesWithoutJustification"/> 초과) 잡는다.
    /// 실측상 이 저장소의 정당한 대기는 전부 30프레임 이하였고, 시간 기반 대상을 프레임으로 잰
    /// 결함은 전부 60프레임 이상이었다 — 이 경계에서 오탐 0건/미탐 0건이다.</para>
    ///
    /// <para><b>정말 프레임이 맞는 자리라면</b> 루프 위(5줄 이내)나 같은 줄에
    /// <see cref="JustificationMarker"/> 표식과 <b>근거</b>를 적으면 통과한다. 표식만 붙이고 근거를
    /// 안 적는 것을 막을 방법은 없지만, 적어도 "생각 없이 프레임 수를 적는" 기본 경로는 막힌다.</para>
    ///
    /// <para><b>대안은 <see cref="StickMate.Tests.PlayMode.TestClock"/></b> — 초 단위 예산/상태 대기를
    /// 공용으로 제공한다. CLAUDE.md에 규칙으로도 확정돼 있다.</para>
    /// </summary>
    public sealed class FrameBudgetLintTests
    {
        private const string LogPrefix = "[프레임예산린트]";

        /// <summary>이 프레임 수를 <b>넘는</b> 대기/표본 루프는 근거 표식 없이는 통과하지 못한다.</summary>
        public const int MaxFramesWithoutJustification = 30;

        /// <summary>"이 자리는 프레임이 맞다"고 선언하는 표식(근거를 함께 적을 것).</summary>
        public const string JustificationMarker = "프레임예산-OK";

        private static string TestsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts", "Tests");

        // ====================================================================
        // 본 검사
        // ====================================================================

        [Test]
        public void 테스트_소스에_큰_프레임수_대기가_남아있지_않다()
        {
            Assert.IsTrue(Directory.Exists(TestsRoot),
                $"{LogPrefix} 테스트 소스 폴더를 찾지 못했습니다: {TestsRoot}");

            string[] files = Directory.GetFiles(TestsRoot, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 50,
                $"{LogPrefix} 스캔 대상이 {files.Length}개뿐입니다 — 경로가 틀렸다면 이 린트는 " +
                "아무것도 안 보고 초록이 됩니다(거짓 통과).");

            var violations = new List<string>();
            int scannedLoops = 0;

            foreach (string file in files)
            {
                // .NET의 8.3 이름 잔재로 "*.cs"가 ".cs.meta"까지 무는 환경이 있다 — 확장자를 다시 본다.
                if (!file.EndsWith(".cs", System.StringComparison.Ordinal)) continue;

                string source = File.ReadAllText(file);
                foreach (FrameLoop loop in FindFrameWaitLoops(source))
                {
                    scannedLoops++;
                    if (loop.Frames <= MaxFramesWithoutJustification) continue;
                    if (loop.Justified) continue;

                    float fastSeconds = loop.Frames * 0.00011f;
                    float slowSeconds = loop.Frames * 0.00045f;
                    violations.Add(
                        $"  · {Path.GetFileName(file)}:{loop.Line} — {loop.Frames}프레임 대기/표본" +
                        $"(배치 모드 실측 환산 {fastSeconds:F3}~{slowSeconds:F3}초)\n" +
                        $"      {loop.HeaderText}");
                }
            }

            // 네거티브 컨트롤 — 스캐너가 실제로 뭔가를 보고 있는가.
            Assert.Greater(scannedLoops, 10,
                $"{LogPrefix} 파일 {files.Length}개에서 프레임 대기 루프를 {scannedLoops}개밖에 " +
                "찾지 못했습니다 — 파서가 고장 난 것입니다(고장 난 파서는 언제나 초록입니다).");

            Debug.Log($"{LogPrefix} 파일 {files.Length}개 / 프레임 대기 루프 {scannedLoops}개 스캔, " +
                $"위반 {violations.Count}건 (상한 {MaxFramesWithoutJustification}프레임).");

            if (violations.Count == 0) return;

            Assert.Fail($"{LogPrefix} 시간을 프레임 수로 잰 대기/표본이 {violations.Count}건 남아 있습니다.\n" +
                string.Join("\n", violations) + "\n\n" +
                "배치 모드(-batchmode -nographics)는 0.11~0.45ms/프레임으로 돕니다. 위 예산은 초 단위 " +
                "연출/물리/폴링/자율 전이를 재기에는 수십 배 짧아, 지금 초록이라도 겨냥한 구간을 " +
                "한 번도 못 본 <b>거짓 통과</b>일 수 있습니다.\n" +
                "  · 고치는 법: StickMate.Tests.PlayMode.TestClock.SampleForSeconds / WaitUntil / " +
                "WaitForState 로 예산을 <b>초</b>로 잡으세요.\n" +
                $"  · 정말 프레임이 맞는 자리라면(예: \"다음 프레임에 반영되는가\") 루프 위에 " +
                $"\"{JustificationMarker}\" 표식과 근거를 적으면 통과합니다.");
        }

        // ====================================================================
        // 파서 자기검증 — 이 린트 자체가 거짓 초록이 되지 않게
        // ====================================================================

        /// <summary>
        /// 스캐너를 <b>알려진 입력</b>에 돌려 (a) 잡아야 할 것을 잡고 (b) 잡으면 안 되는 것을
        /// 안 잡는지 확인한다. 이게 없으면 파서가 조용히 망가져도 본 검사는 영원히 초록이다.
        /// </summary>
        [Test]
        public void 스캐너_자기검증_잡을것만_잡는다()
        {
            const string Sample = @"
class Sample
{
    const int SampleFrames = 900;

    IEnumerator A()
    {
        for (int f = 0; f < SampleFrames; f++) { yield return null; }        // (1) 잡아야 함
        for (int i = 0; i < 120; i++)                                        // (2) 잡아야 함
        {
            yield return null;
        }
        for (int i = 0; i < 8; i++) yield return null;                       // (3) 작아서 통과
        for (int i = 0; i < 4096; i++) Total += i;                           // (4) yield 없음 - 무시
        for (int i = 0; i < 300; i++) yield return new WaitForSeconds(0.05f); // (5) 시간 기준 - 무시
        for (int i = 0; i < items.Length; i++) { yield return null; }        // (6) 해석 불가 - 무시
        // for (int i = 0; i < 999; i++) yield return null;                  // (7) 주석 - 무시
        Log(""for (int i = 0; i < 888; i++) yield return null;"");           // (8) 문자열 - 무시
        // 프레임예산-OK: 여기는 프레임이 맞다(근거).
        for (int i = 0; i < 500; i++) { yield return null; }                 // (9) 표식 - 통과
    }
}";
            var found = new List<FrameLoop>(FindFrameWaitLoops(Sample));
            var flagged = new List<FrameLoop>();
            foreach (FrameLoop l in found)
                if (l.Frames > MaxFramesWithoutJustification && !l.Justified) flagged.Add(l);

            string dump = "";
            foreach (FrameLoop l in found) dump += $"\n    line {l.Line}: {l.Frames}프레임, 근거표식={l.Justified}";
            Debug.Log($"{LogPrefix} 자기검증 스캔 결과{dump}");

            Assert.AreEqual(2, flagged.Count,
                $"{LogPrefix} 표본에서 잡아야 할 위반은 (1)900프레임과 (2)120프레임 <b>둘</b>인데 " +
                $"{flagged.Count}건을 잡았습니다.{dump}\n" +
                "너무 적게 잡으면 본 검사가 거짓 초록이 되고, 너무 많이 잡으면 정당한 프레임 대기까지 " +
                "무차별로 막습니다.");
            Assert.AreEqual(900, flagged[0].Frames, $"{LogPrefix} const int 상수(900)를 못 풀었습니다.");
            Assert.AreEqual(120, flagged[1].Frames, $"{LogPrefix} 리터럴(120)을 못 읽었습니다.");
        }

        // ====================================================================
        // 스캐너
        // ====================================================================

        private struct FrameLoop
        {
            public int Line;
            public int Frames;
            public bool Justified;
            public string HeaderText;
        }

        /// <summary>
        /// <c>for (...)</c> 루프 중 <b>본문이 <c>yield return null</c>만으로 프레임을 흘리는</b> 것을
        /// 찾아 반복 횟수를 푼다. 주석/문자열은 먼저 지운 뒤 본다(주석에 적힌 예시 코드를 잡지 않도록).
        /// </summary>
        private static IEnumerable<FrameLoop> FindFrameWaitLoops(string rawSource)
        {
            string code = BlankOutCommentsAndStrings(rawSource);
            Dictionary<string, int> constants = CollectIntConstants(code);
            var result = new List<FrameLoop>();

            for (int i = 0; i + 3 < code.Length; i++)
            {
                if (code[i] != 'f' || code[i + 1] != 'o' || code[i + 2] != 'r') continue;
                if (i > 0 && (char.IsLetterOrDigit(code[i - 1]) || code[i - 1] == '_')) continue;
                int open = SkipSpaces(code, i + 3);
                if (open >= code.Length || code[open] != '(') continue;
                int close = MatchBracket(code, open, '(', ')');
                if (close < 0) continue;

                string header = code.Substring(open + 1, close - open - 1);
                if (!TryResolveIterations(header, constants, out int frames)) continue;

                string body = ExtractBody(code, close + 1);
                if (body == null) continue;
                if (body.IndexOf("yield return null", System.StringComparison.Ordinal) < 0) continue;
                // 안에서 시간 기준으로 기다리고 있다면 이 린트의 대상이 아니다.
                if (body.IndexOf("yield return new WaitFor", System.StringComparison.Ordinal) >= 0) continue;

                int line = LineOf(rawSource, i);
                result.Add(new FrameLoop
                {
                    Line = line,
                    Frames = frames,
                    Justified = HasJustificationNear(rawSource, line),
                    HeaderText = LineText(rawSource, line).Trim(),
                });
            }
            return result;
        }

        /// <summary><c>i &lt; N</c> / <c>i &lt;= N</c> 형태에서 N을 푼다(리터럴 또는 같은 파일의 const int).
        /// 그 밖(배열 길이, 매개변수 등)은 시간과 무관한 순회일 확률이 높아 대상에서 뺀다.</summary>
        private static bool TryResolveIterations(string header, Dictionary<string, int> constants, out int frames)
        {
            frames = 0;
            Match m = Regex.Match(header, @"[;]\s*\w+\s*<=?\s*([A-Za-z_]\w*|\d+)\s*(?:;|&&)");
            if (!m.Success) return false;
            string bound = m.Groups[1].Value;
            if (int.TryParse(bound, out frames)) return true;
            return constants.TryGetValue(bound, out frames);
        }

        private static Dictionary<string, int> CollectIntConstants(string code)
        {
            var map = new Dictionary<string, int>();
            foreach (Match m in Regex.Matches(code, @"const\s+int\s+(\w+)\s*=\s*(\d+)\s*;"))
                map[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
            return map;
        }

        /// <summary>루프 본문을 뽑는다 — <c>{ }</c> 블록이면 대응 괄호까지, 아니면 다음 <c>;</c>까지.</summary>
        private static string ExtractBody(string code, int from)
        {
            int p = SkipSpaces(code, from);
            if (p >= code.Length) return null;
            if (code[p] == '{')
            {
                int end = MatchBracket(code, p, '{', '}');
                return end < 0 ? null : code.Substring(p, end - p + 1);
            }
            int semi = code.IndexOf(';', p);
            return semi < 0 ? null : code.Substring(p, semi - p + 1);
        }

        private static int SkipSpaces(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        private static int MatchBracket(string s, int open, char oc, char cc)
        {
            int depth = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == oc) depth++;
                else if (s[i] == cc && --depth == 0) return i;
            }
            return -1;
        }

        /// <summary>주석과 문자열 리터럴을 <b>같은 길이의 공백</b>으로 지운다(줄 번호를 보존해야 한다).</summary>
        private static string BlankOutCommentsAndStrings(string s)
        {
            var sb = new StringBuilder(s);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, inVerbatim = false;
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                char next = i + 1 < sb.Length ? sb[i + 1] : '\0';

                if (inLine)
                {
                    if (c == '\n') { inLine = false; continue; }
                    if (c != '\r') sb[i] = ' ';
                    continue;
                }
                if (inBlock)
                {
                    if (c == '*' && next == '/') { sb[i] = ' '; sb[i + 1] = ' '; i++; inBlock = false; continue; }
                    if (c != '\n' && c != '\r') sb[i] = ' ';
                    continue;
                }
                if (inVerbatim)
                {
                    if (c == '"' && next == '"') { sb[i] = ' '; sb[i + 1] = ' '; i++; continue; }
                    if (c == '"') { sb[i] = ' '; inVerbatim = false; continue; }
                    if (c != '\n' && c != '\r') sb[i] = ' ';
                    continue;
                }
                if (inStr)
                {
                    if (c == '\\' && i + 1 < sb.Length) { sb[i] = ' '; sb[i + 1] = ' '; i++; continue; }
                    if (c == '"') { sb[i] = ' '; inStr = false; continue; }
                    sb[i] = ' ';
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\' && i + 1 < sb.Length) { sb[i] = ' '; sb[i + 1] = ' '; i++; continue; }
                    if (c == '\'') { sb[i] = ' '; inChar = false; continue; }
                    sb[i] = ' ';
                    continue;
                }

                if (c == '/' && next == '/') { sb[i] = ' '; sb[i + 1] = ' '; i++; inLine = true; continue; }
                if (c == '/' && next == '*') { sb[i] = ' '; sb[i + 1] = ' '; i++; inBlock = true; continue; }
                if (c == '@' && next == '"') { sb[i] = ' '; sb[i + 1] = ' '; i++; inVerbatim = true; continue; }
                if (c == '$' && next == '@' && i + 2 < sb.Length && sb[i + 2] == '"')
                { sb[i] = ' '; sb[i + 1] = ' '; sb[i + 2] = ' '; i += 2; inVerbatim = true; continue; }
                if (c == '"') { sb[i] = ' '; inStr = true; continue; }
                if (c == '\'') { sb[i] = ' '; inChar = true; continue; }
            }
            return sb.ToString();
        }

        private static int LineOf(string s, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < s.Length; i++) if (s[i] == '\n') line++;
            return line;
        }

        private static string LineText(string s, int line)
        {
            string[] lines = s.Split('\n');
            return line - 1 >= 0 && line - 1 < lines.Length ? lines[line - 1] : "";
        }

        private static bool HasJustificationNear(string s, int line)
        {
            string[] lines = s.Split('\n');
            for (int i = Mathf.Max(0, line - 6); i < Mathf.Min(lines.Length, line); i++)
                if (lines[i].Contains(JustificationMarker)) return true;
            return false;
        }
    }
}
