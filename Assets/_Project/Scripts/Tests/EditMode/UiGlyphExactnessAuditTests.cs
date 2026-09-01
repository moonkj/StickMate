using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>UI 폰트 pt는 전부 "정수 픽셀로 구워지는" 크기여야 한다</b> — 소스 정적 감사.
    ///
    /// ============================================================================
    /// 무엇을 잠그는가
    /// ============================================================================
    /// 사용자 신고(Windows 실기, 사진 첨부): <b>"여전히 창 겹침현상 텍스트도 다 번져보임"</b>.
    /// 실기 로그 <c>[GLYPH-SCALE]</c>가 원인을 확정했다 — 캔버스 배율 1.5에서 <b>홀수 pt</b>는
    /// <c>round(pt×1.5)</c>px로 구워진 뒤 비정수 배로 다시 확대되어 획이 번진다.
    /// 그래서 <b>모든 UI 폰트 pt × 기준 캔버스 배율이 정수</b>임을 소스에서 직접 확인한다.
    ///
    /// ============================================================================
    /// 왜 런타임 테스트가 아니라 소스 감사인가
    /// ============================================================================
    /// 이 계약이 실제로 깨지는 방식은 <b>내일 누가 새 창에 15pt 라벨 한 줄을 넣는 날</b>이다.
    /// 그 창은 아직 존재하지 않으므로 런타임 테스트가 볼 수 없다. 게다가 배치모드 테스트의 캔버스
    /// 배율은 1이라 <b>모든 정수 pt가 잔차 0</b>이다 — 런타임으로는 원리적으로 재현조차 안 된다.
    /// (<c>CharacterScaleSingleOwnerSourceTests</c>가 같은 이유로 쓰는 기법이다.)
    ///
    /// ============================================================================
    /// ★ 상수를 숫자로 베끼지 않는다 (CLAUDE.md)
    /// ============================================================================
    /// 이 파일에는 <b>1.5도 "짝수"도 없다</b>. 기준 배율은
    /// <see cref="UiGlyphScalePolicy.ReferenceCanvasScale"/>을 참조하고, "정수 픽셀인가"는
    /// <see cref="UiGlyphScalePolicy.IsExactAtReferenceScale"/>이 판정한다. 그래야 기준 배율이
    /// 1.25로 바뀌는 날 이 감사가 <b>스스로 "4의 배수"를 요구</b>하게 되고, 지금 통과하는 pt들이
    /// 그날 즉시 빨갛게 뜬다.
    /// </summary>
    public sealed class UiGlyphExactnessAuditTests
    {
        private const string LogPrefix = "[글리프정수-TEST]";

        /// <summary>타이포 계층 상수(<c>Font*</c>)가 사는 파일.
        /// <para>★ 2026-09-01 해소: 이 상수가 처음 생길 때는 <c>UiChrome.cs</c>를 다른 에이전트가
        /// 편집 중이라 위반을 <c>Assert.Ignore</c>(사유 포함)로 남겨 두었다. 같은 날 그 파일 소유자가
        /// <c>FontDisplay 19→20 / FontTitle 13→14 / FontLabel 11→12</c>로 옮겼으므로
        /// <b>이제는 실단언</b>이다 — 홀수 pt가 하나라도 되돌아오면 여기서 멈춘다.</para></summary>
        private const string TypographyConstantsFile = "UiChrome.cs";

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static float Scale => UiGlyphScalePolicy.ReferenceCanvasScale;

        // ============================================================================
        // 소스 수집
        // ============================================================================

        private static List<string> ProductionFiles()
        {
            string testsRoot = (Path.Combine(ScriptsRoot, "Tests") + Path.DirectorySeparatorChar).Replace('\\', '/');
            var files = new List<string>(Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories));
            files.RemoveAll(p => p.Replace('\\', '/').StartsWith(testsRoot, StringComparison.Ordinal));
            Assert.GreaterOrEqual(files.Count, 40,
                $"{LogPrefix} 스캔 대상 파일이 비정상적으로 적습니다({files.Count}) — 경로 계산 오류로 허위 통과할 위험.");
            return files;
        }

        /// <summary>주석 <b>줄</b>만 지운다(줄 안의 꼬리 주석은 건드리지 않는다 — 문자열 속 "//"를
        /// 잘못 자르지 않기 위해서다). 이 저장소의 문서 주석이 옛 호출부를 그대로 인용하고 있어
        /// 이 단계가 없으면 감사가 주석을 코드로 착각한다.</summary>
        private static string StripCommentLines(string source)
        {
            var sb = new StringBuilder(source.Length);
            foreach (string line in source.Split('\n'))
            {
                string t = line.TrimStart();
                bool comment = t.StartsWith("//", StringComparison.Ordinal)
                            || t.StartsWith("*", StringComparison.Ordinal)
                            || t.StartsWith("/*", StringComparison.Ordinal);
                sb.Append(comment ? string.Empty : line).Append('\n');
            }
            return sb.ToString();
        }

        private static int LineOf(string source, int index) => source.Substring(0, index).Split('\n').Length;

        /// <summary><paramref name="open"/>(여는 괄호 위치)에서 짝이 맞는 닫는 괄호까지의 인자들을
        /// 최상위 콤마로 쪼갠다. 문자열 리터럴 안의 콤마/괄호는 세지 않는다.</summary>
        private static List<string> SplitArguments(string source, int open)
        {
            var args = new List<string>();
            var cur = new StringBuilder();
            int depth = 0;
            bool inString = false, inChar = false;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                bool structural = false;
                if (inString) { if (c == '"' && source[i - 1] != '\\') inString = false; }
                else if (inChar) { if (c == '\'' && source[i - 1] != '\\') inChar = false; }
                else if (c == '"') inString = true;
                else if (c == '\'') inChar = true;
                else if (c == '(' || c == '[')
                {
                    depth++;
                    structural = depth == 1;          // 여는 괄호 자신은 인자에 넣지 않는다.
                }
                else if (c == ')' || c == ']')
                {
                    depth--;
                    if (depth == 0) { args.Add(cur.ToString().Trim()); return args; }
                }
                else if (c == ',' && depth == 1)
                {
                    args.Add(cur.ToString().Trim());
                    cur.Clear();
                    structural = true;
                }

                if (!structural) cur.Append(c);
            }
            return args;   // 괄호 짝이 안 맞는다 — 호출부가 인자 개수로 잡아낸다(허위 통과 방지).
        }

        private static bool TryPoints(string expression, out int points)
            => int.TryParse(expression.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out points);

        private static string Describe(int points)
        {
            float px = points * Scale;
            return $"{points}pt × 배율 {Scale:F3} = {px:F2}px (정수 아님, " +
                   $"권장 {UiGlyphScalePolicy.SnapPoints(points, Scale)}pt)";
        }

        // ============================================================================
        // (1) UiChrome 타이포 계층 상수 — 하드 잠금 (2026-09-01 스킵 해소)
        // ============================================================================

        [Test]
        public void UiChrome_타이포_상수가_기준배율에서_정수픽셀이다()
        {
            string path = null;
            foreach (string f in ProductionFiles())
            {
                if (Path.GetFileName(f) == TypographyConstantsFile) { path = f; break; }
            }
            Assert.IsNotNull(path, $"{LogPrefix} {TypographyConstantsFile}을(를) 찾지 못했습니다.");

            string src = StripCommentLines(File.ReadAllText(path));
            var rx = new Regex(@"const\s+int\s+(Font\w*)\s*=\s*(\d+)\s*;");
            var found = new List<string>();
            var bad = new List<string>();
            foreach (Match m in rx.Matches(src))
            {
                int pt = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                found.Add($"{m.Groups[1].Value}={pt}");
                if (!UiGlyphScalePolicy.IsExactAtReferenceScale(pt))
                    bad.Add($"{m.Groups[1].Value}: {Describe(pt)}");
            }

            Assert.GreaterOrEqual(found.Count, 3,
                $"{LogPrefix} {TypographyConstantsFile}에서 Font* 상수를 {found.Count}개밖에 못 찾았습니다 — " +
                "상수 이름 규약이 바뀌었다면 이 감사도 함께 고쳐야 합니다(허위 통과 방지).");
            Debug.Log($"{LogPrefix} {TypographyConstantsFile} 타이포 계층: {string.Join(", ", found)} " +
                      $"(기준 배율 {Scale:F3}).");

            Assert.IsEmpty(bad,
                $"{LogPrefix} ★ {TypographyConstantsFile}의 타이포 계층에 기준 배율 {Scale:F3}에서 " +
                $"비정수 픽셀로 구워지는 pt가 있습니다({bad.Count}건). 이 상수들은 창 제목/탭/라벨/캐릭터 " +
                "이름 등 앱 안의 거의 모든 글자를 지배하므로, 여기 하나가 홀수로 돌아오면 사용자가 신고한 " +
                "'텍스트가 다 번져보임'이 그대로 재발합니다. <b>캔버스 배율을 정수로 스냅해 고치지 마세요</b> " +
                "— 그건 이미 해결된 '글씨가 잘 안 보임' 신고를 되살립니다(UiGlyphScalePolicy 문서):\n  " +
                string.Join("\n  ", bad));
        }

        // ============================================================================
        // (2) 창/위젯이 직접 넘긴 폰트 리터럴 — 하드 잠금
        // ============================================================================

        [Test]
        public void AddText에_넘긴_폰트_리터럴이_기준배율에서_정수픽셀이다()
        {
            var bad = new List<string>();
            int scanned = 0;
            foreach (string file in ProductionFiles())
            {
                string name = Path.GetFileName(file);
                if (name == TypographyConstantsFile) continue;   // AddText 정의부(파라미터)라 리터럴이 없다.
                string src = StripCommentLines(File.ReadAllText(file));
                foreach (Match m in Regex.Matches(src, @"AddText\s*\("))
                {
                    int open = src.IndexOf('(', m.Index);
                    List<string> args = SplitArguments(src, open);
                    // ★ 허위 통과 방지: 인자를 못 쪼갰으면 <조용히 건너뛰지 않고> 즉시 실패한다.
                    //   AddText의 필수 인자는 5개(parent, name, fontSize, anchor, color)다.
                    Assert.GreaterOrEqual(args.Count, 5,
                        $"{LogPrefix} {name}:{LineOf(src, m.Index)}의 AddText 인자를 파싱하지 못했습니다" +
                        $"(추출 {args.Count}개) — 이 호출부가 감사에서 빠지면 감사 자체가 거짓 초록이 됩니다.");
                    if (!TryPoints(args[2], out int pt)) continue;   // UiChrome.FontBody 같은 상수는 (1)이 본다.
                    scanned++;
                    if (!UiGlyphScalePolicy.IsExactAtReferenceScale(pt))
                        bad.Add($"{name}:{LineOf(src, m.Index)} — {Describe(pt)}");
                }
            }

            Debug.Log($"{LogPrefix} AddText 리터럴 {scanned}건 검사 — 위반 {bad.Count}건.");
            Assert.IsEmpty(bad,
                $"{LogPrefix} ★ 캔버스 배율 {Scale:F3}에서 비정수 픽셀로 구워지는 폰트 리터럴이 있습니다 " +
                "— 사용자가 신고한 '텍스트 번짐'의 직접 원인입니다. 배율을 정수로 스냅하지 말고 " +
                $"pt를 옮기세요(UiGlyphScalePolicy 문서 참고):\n  {string.Join("\n  ", bad)}");
        }

        [Test]
        public void Text_fontSize_대입_리터럴이_기준배율에서_정수픽셀이다()
        {
            var bad = new List<string>();
            int scanned = 0;
            foreach (string file in ProductionFiles())
            {
                string src = StripCommentLines(File.ReadAllText(file));
                foreach (Match m in Regex.Matches(src, @"\.fontSize\s*=\s*(\d+)\s*;"))
                {
                    int pt = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    scanned++;
                    if (!UiGlyphScalePolicy.IsExactAtReferenceScale(pt))
                        bad.Add($"{Path.GetFileName(file)}:{LineOf(src, m.Index)} — {Describe(pt)}");
                }
            }

            Debug.Log($"{LogPrefix} fontSize 직접 대입 {scanned}건 검사 — 위반 {bad.Count}건.");
            Assert.IsEmpty(bad,
                $"{LogPrefix} ★ 배율 {Scale:F3}에서 비정수 픽셀이 되는 fontSize 대입:\n  " +
                string.Join("\n  ", bad));
        }

        // ============================================================================
        // (3) 런타임에 계산되는 유일한 폰트 — 말풍선은 규칙을 <반드시> 통과해야 한다
        // ============================================================================

        [Test]
        public void 말풍선_폰트는_스냅_정책을_거쳐서_반환된다()
        {
            string path = null;
            foreach (string f in ProductionFiles())
            {
                if (Path.GetFileName(f) == "DialogueBubbleRenderer.cs") { path = f; break; }
            }
            Assert.IsNotNull(path, $"{LogPrefix} DialogueBubbleRenderer.cs를 찾지 못했습니다.");

            string src = StripCommentLines(File.ReadAllText(path));
            int decl = src.IndexOf("int ResolveFontSize()", StringComparison.Ordinal);
            Assert.Greater(decl, 0, $"{LogPrefix} ResolveFontSize() 선언을 찾지 못했습니다 — 이름이 바뀌었다면 " +
                "이 감사도 함께 고쳐야 합니다(허위 통과 방지).");

            // 선언부터 다음 메서드 경계 전까지의 본문에 스냅 호출이 있어야 한다.
            int end = src.IndexOf("\n        }", decl, StringComparison.Ordinal);
            Assert.Greater(end, decl, $"{LogPrefix} ResolveFontSize() 본문 끝을 찾지 못했습니다.");
            string body = src.Substring(decl, end - decl);

            StringAssert.Contains(nameof(UiGlyphScalePolicy) + "." + nameof(UiGlyphScalePolicy.SnapPoints), body,
                $"{LogPrefix} ★ 말풍선 폰트 크기가 스냅 정책을 거치지 않고 반환됩니다. 이 값은 " +
                "(사용자 설정) × (캐릭터 배율) × (만화 배율)의 런타임 곱이라 어떤 홀수든 나올 수 있고, " +
                "정적 상수로는 막을 수 없습니다 — 반드시 마지막에 " +
                $"{nameof(UiGlyphScalePolicy)}.{nameof(UiGlyphScalePolicy.SnapPoints)}로 스냅하세요.");
        }

        // ============================================================================
        // (4) 규칙 자체의 성질 — 감사가 기대는 순수 함수를 먼저 잠근다
        // ============================================================================

        [Test]
        public void 스냅_결과는_기준배율에서_항상_정수픽셀이다()
        {
            for (int pt = 1; pt <= 64; pt++)
            {
                int snapped = UiGlyphScalePolicy.SnapPoints(pt, Scale);
                Assert.IsTrue(UiGlyphScalePolicy.IsExactAtReferenceScale(snapped),
                    $"{LogPrefix} {pt}pt -> {snapped}pt인데 배율 {Scale:F3}에서 여전히 잔차가 남습니다.");
                Assert.LessOrEqual(Mathf.Abs(snapped - pt), 1,
                    $"{LogPrefix} {pt}pt를 {snapped}pt로 옮겼습니다 — 기준 배율에서 1pt를 넘게 움직이면 " +
                    "레이아웃이 깨질 수 있으니 규칙을 재검토해야 합니다.");
            }
        }

        [Test]
        public void 정수_배율에서는_스냅이_항등이다_macOS_무회귀()
        {
            // macOS는 Retina 2배(또는 1배)라 <모든 정수 pt가 이미 잔차 0>이다. 이 라운드의 어떤 변경도
            // macOS 화면을 건드리지 않는다는 사실을 규칙 수준에서 잠근다.
            foreach (float integerScale in new[] { 1f, 2f, 3f })
            {
                for (int pt = 1; pt <= 64; pt++)
                {
                    Assert.AreEqual(pt, UiGlyphScalePolicy.SnapPoints(pt, integerScale),
                        $"{LogPrefix} 배율 {integerScale}에서 {pt}pt가 바뀌었습니다 — 정수 배율에서는 " +
                        "어떤 pt도 리샘플되지 않으므로 스냅은 항등이어야 합니다(macOS 회귀).");
                }
            }
        }

        [Test]
        public void 스냅은_멱등이며_이미_정확한_값을_바꾸지_않는다()
        {
            for (int pt = 1; pt <= 64; pt++)
            {
                int once = UiGlyphScalePolicy.SnapPoints(pt, Scale);
                Assert.AreEqual(once, UiGlyphScalePolicy.SnapPoints(once, Scale),
                    $"{LogPrefix} 스냅이 멱등이 아닙니다({pt} -> {once} -> ...).");
                if (UiGlyphScalePolicy.IsExactAtReferenceScale(pt))
                    Assert.AreEqual(pt, once, $"{LogPrefix} 이미 잔차 0인 {pt}pt를 건드렸습니다.");
            }
        }

        [Test]
        public void 정확한_pt_간격이_실제로_전부_정확하다()
        {
            int step = UiGlyphScalePolicy.ExactPointStep(Scale);
            Assert.Greater(step, 0, $"{LogPrefix} 배율 {Scale:F3}에서 잔차 0인 pt 간격을 찾지 못했습니다.");
            for (int k = 1; k * step <= 64; k++)
            {
                Assert.IsTrue(UiGlyphScalePolicy.IsExactAtReferenceScale(k * step),
                    $"{LogPrefix} 간격 {step}의 배수 {k * step}pt가 정확하지 않습니다 — " +
                    "ExactPointStep이 거짓말을 하고 있습니다.");
            }
            Debug.Log($"{LogPrefix} 기준 배율 {Scale:F3}에서 안전한 pt 간격 = {step} " +
                      $"(즉 {step}의 배수만 잔차 0).");
        }
    }
}
