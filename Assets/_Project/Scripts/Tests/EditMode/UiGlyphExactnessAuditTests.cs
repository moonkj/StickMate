using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Dialogue;
using StickMate.Interaction;   // GearRadialMenuWidget — 축소 폴백 배율을 <상수에서> 가져온다(숫자를 베끼지 않는다).
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

        /// <summary>
        /// ★★ 2026-09-02 <c>test-engineer</c> — <b>이 검사는 분모가 0인 채로 통과하고 있었다.</b>
        ///
        /// <para>옛 이름은 <c>Text_fontSize_대입_리터럴이…</c>였고 정규식이
        /// <c>.fontSize = &lt;정수&gt;;</c> <b>하나</b>만 셌다. 실측(2026-09-02, 프로덕션 .cs 196개):
        /// <b>그 형태는 0건이다.</b> 즉 <c>foreach</c>가 한 번도 돌지 않고
        /// <c>Assert.IsEmpty(빈 목록)</c>으로 초록이 됐다 — docs/TEAM.md 거짓 통과 5번
        /// ("면제 목록이 비어 foreach가 아무것도 안 재고 초록")과 <b>같은 형태</b>다.</para>
        ///
        /// <para><b>고친 방식</b>: 대상을 <c>.fontSize</c> 대입 <b>전부</b>로 넓히고, 각 대입을
        /// <b>분류</b>한다. 분모가 구조적으로 0이 될 수 없고(하한을 단언한다), 분류되지 않는 새 형태가
        /// 나타나면 <b>조용히 통과하지 않고 실패</b>한다. 실측 4건의 내역:
        /// <list type="bullet">
        ///   <item><c>= &lt;정수&gt;</c> — 여기서 정책을 직접 건다(현재 0건, 하지만 내일 생길 수 있다).</item>
        ///   <item><c>UiChrome.Font*</c> 상수 경유 — 상수 자체를 (1)이 잠근다.</item>
        ///   <item><c>fontSize</c> 파라미터/식 경유 — 호출부 리터럴을 (2)가, 말풍선 런타임 계산을 (3)이 잠근다.</item>
        /// </list>
        /// 그 세 형제 검사가 <b>실재하는지</b>를 같은 검사 안에서 리플렉션으로 못박는다(존재 단언) —
        /// "다른 데서 잠근다"가 이름이 바뀐 뒤에도 참인 척하지 못하게.</para>
        /// </summary>
        [Test]
        public void fontSize_대입은_전부_정책을_거치거나_기준배율에서_정수픽셀이다()
        {
            // ── 형제 검사 존재 단언 ────────────────────────────────────────────────
            // 아래 분류가 "저쪽에서 잠근다"고 말하므로, 저쪽이 실재하는지 여기서 확인한다.
            // 이름이 바뀌면 <b>시끄럽게</b> 빨개진다(부재 단언과 달리 조용히 초록이 되지 않는다).
            foreach (string sibling in new[]
                     {
                         nameof(UiChrome_타이포_상수가_기준배율에서_정수픽셀이다),
                         nameof(AddText에_넘긴_폰트_리터럴이_기준배율에서_정수픽셀이다),
                         nameof(말풍선_폰트는_스냅_정책을_거쳐서_반환된다),
                         // ★ 2026-09-02 — literal 갈래(정책 술어를 실제로 거는 유일한 갈래)의
                         //   프로덕션 분모는 0이다. 그 갈래가 <b>동작은 한다</b>는 증명이 저기 있다.
                         //   지워지면 이 검사의 '위반 0건'이 다시 해석 불가능해진다.
                         nameof(양성_대조_리터럴_갈래가_합성_소스에서_실제로_빨개진다),
                     })
            {
                Assert.IsNotNull(GetType().GetMethod(sibling),
                    $"{LogPrefix} 형제 검사 '{sibling}'을 찾지 못했습니다 — 아래 분류가 " +
                    "'저쪽에서 잠근다'고 주장하는데 저쪽이 없습니다. 그 주장이 거짓이 되는 순간입니다.");
            }

            var scan = new FontSizeScan();
            foreach (string file in ProductionFiles())
            {
                ScanFontSizeAssignments(StripCommentLines(File.ReadAllText(file)),
                    Path.GetFileName(file), scan);
            }

            Debug.Log($"{LogPrefix} fontSize 대입 {scan.Total}건 — 리터럴 {scan.Literal} / " +
                $"상수경유 {scan.ViaConstant} / 런타임경유 {scan.ViaRuntime} / " +
                $"미분류 {scan.Unclassified.Count}, 위반 {scan.Bad.Count}건.");

            // ★ 분모 하한 — 이 검사가 "아무것도 안 보고 초록"이 되는 길을 막는다.
            //   ★★ 2026-09-02 2차 — 하한을 1에서 <b>실측 기준선 4</b>로 올렸다. 1이면 파일 3개가
            //   스캔에서 빠져도(경로 오타·정규식 축소·주석 제거기 오작동) 통과한다 —
            //   "0건 = 깨끗"의 사촌인 "1건만 봤는데 다 봤다고 함"이다.
            //   내려가면 <b>왜 내려갔는지</b>를 사람이 확인하고 이 숫자를 함께 내려야 한다.
            Assert.GreaterOrEqual(scan.Total, MeasuredFontSizeAssignmentBaseline,
                $"{LogPrefix} .fontSize 대입이 {scan.Total}건뿐입니다 — 실측 기준선은 " +
                $"{MeasuredFontSizeAssignmentBaseline}건(말풍선 2 / UiChrome 1 / 포스트잇 1, 2026-09-02)입니다. " +
                "정규식이나 경로 계산이 깨졌을 가능성이 크고, 그 상태의 '위반 0건'은 무의미합니다. " +
                "정말로 대입이 줄었다면 이 기준선도 함께 내리십시오(그 판단은 사람이 합니다).");

            Assert.IsEmpty(scan.Unclassified,
                $"{LogPrefix} 분류되지 않는 fontSize 대입 형태가 생겼습니다 — 어느 검사도 이것을 잠그지 " +
                $"않습니다. 정책을 거치게 하거나 이 분류에 한 줄 추가하십시오:\n  " +
                string.Join("\n  ", scan.Unclassified));

            Assert.IsEmpty(scan.Bad,
                $"{LogPrefix} ★ 배율 {Scale:F3}에서 비정수 픽셀이 되는 fontSize 대입:\n  " +
                string.Join("\n  ", scan.Bad));
        }

        // ============================================================================
        // ★★ 2026-09-02 <c>test-engineer</c> — 위 검사의 <b>literal 갈래는 분모가 0이다</b>
        // ============================================================================

        /// <summary>2026-09-02 실측: 프로덕션 전체의 <c>.fontSize =</c> 대입 건수.
        /// <para>말풍선 2 / UiChrome 1 / 포스트잇 1. 이 값은 <b>바깥에서 온 관측값</b>이지
        /// 프로덕션 상수의 사본이 아니다 — 그래서 여기 적어도 CLAUDE.md 규약에 걸리지 않는다.</para></summary>
        private const int MeasuredFontSizeAssignmentBaseline = 4;

        /// <summary><c>.fontSize = …</c> 대입 하나하나의 분류 누계.</summary>
        private sealed class FontSizeScan
        {
            public int Total, Literal, ViaConstant, ViaRuntime;
            public readonly List<string> Bad = new List<string>();
            public readonly List<string> Unclassified = new List<string>();
        }

        /// <summary>
        /// <paramref name="source"/>의 <c>.fontSize</c> 대입을 <b>전부</b> 찾아 분류하고,
        /// 리터럴이면 정책 술어(<see cref="UiGlyphScalePolicy.IsExactAtReferenceScale"/>)를 실제로 건다.
        ///
        /// <para><b>따로 뽑은 이유</b>: 프로덕션을 훑는 검사와 <b>합성 소스</b>를 훑는 양성 대조가
        /// <b>같은 코드</b>를 지나가게 하기 위해서다. 대조가 다른 코드를 쓰면
        /// docs/TEAM.md의 열 번째 형태(<i>"생성기와 검사기가 같이 틀린다"</i>)가 그대로 재현된다.</para>
        /// </summary>
        private static void ScanFontSizeAssignments(string source, string label, FontSizeScan scan)
        {
            foreach (Match m in Regex.Matches(source, @"\.fontSize\s*=\s*([^;]+);"))
            {
                scan.Total++;
                string rhs = m.Groups[1].Value.Trim();
                string where = $"{label}:{LineOf(source, m.Index)}";

                if (Regex.IsMatch(rhs, @"^\d+$"))
                {
                    scan.Literal++;
                    int pt = int.Parse(rhs, CultureInfo.InvariantCulture);
                    if (!UiGlyphScalePolicy.IsExactAtReferenceScale(pt))
                        scan.Bad.Add($"{where} — {Describe(pt)}");
                }
                else if (rhs.Contains("UiChrome.Font", StringComparison.Ordinal))
                {
                    scan.ViaConstant++;   // (1) UiChrome_타이포_상수… 가 상수 자체를 잠근다.
                }
                else if (Regex.IsMatch(rhs, @"\bfontSize\b"))
                {
                    scan.ViaRuntime++;    // (2) AddText 호출부 리터럴 + (3) 말풍선 런타임 계산이 잠근다.
                }
                else
                {
                    scan.Unclassified.Add($"{where} — `.fontSize = {rhs};`");
                }
            }
        }

        /// <summary>
        /// ★★ <b>양성 대조 — literal 갈래가 실제로 빨개지는가.</b>
        ///
        /// <para><b>왜 필요한가.</b> 위 검사에서 정책 술어를 <b>실제로 거는 유일한 갈래</b>는
        /// <c>literal</c>이다. 그런데 프로덕션 실측 4건은 <b>전부</b> 상수 경유/런타임 경유라
        /// <c>literal</c>의 분모는 <b>0</b>이다 — 즉 그 갈래는
        /// <i>"한 번도 실행되지 않은 채"</i> 초록에 기여하고 있다. 위반이 0건인 이유가
        /// <b>"위반이 없어서"</b>인지 <b>"검사가 그 자리에 도달한 적이 없어서"</b>인지
        /// 프로덕션 스캔만으로는 <b>구분할 수 없다</b>(docs/TEAM.md 거짓 통과 5번과 같은 형태).</para>
        ///
        /// <para><b>어떻게 가르는가.</b> 같은 <see cref="ScanFontSizeAssignments"/>에
        /// <b>합성 소스</b>를 먹인다. 두 방향을 <b>모두</b> 본다 —
        /// 잔차가 있는 pt는 <c>Bad</c>에 <b>들어가야</b> 하고, 잔차 0인 pt는 <b>안 들어가야</b> 한다.
        /// 한쪽만 보면 "무조건 담는 검사"와 "무조건 안 담는 검사"를 구분하지 못한다.</para>
        ///
        /// <para>★ 쓰는 pt는 <b>손으로 고르지 않는다</b> — <see cref="UiGlyphScalePolicy"/>에게
        /// 물어서 고른다. 기준 배율이 바뀌면 이 대조도 <b>스스로</b> 따라 움직인다.</para>
        /// </summary>
        [Test]
        public void 양성_대조_리터럴_갈래가_합성_소스에서_실제로_빨개진다()
        {
            int exact = -1, inexact = -1;
            for (int pt = 1; pt <= 64 && (exact < 0 || inexact < 0); pt++)
            {
                if (UiGlyphScalePolicy.IsExactAtReferenceScale(pt)) { if (exact < 0) exact = pt; }
                else if (inexact < 0) inexact = pt;
            }

            Assert.Greater(exact, 0,
                $"{LogPrefix} 배율 {Scale:F3}에서 잔차 0인 pt를 1~64에서 찾지 못했습니다 — " +
                "이 대조가 성립하지 않습니다.");
            Assert.Greater(inexact, 0,
                $"{LogPrefix} 배율 {Scale:F3}에서 잔차가 <b>있는</b> pt를 1~64에서 찾지 못했습니다. " +
                "배율이 정수가 되어 모든 pt가 정확해졌다면 이 감사 전체가 무의미해진 것이고, " +
                "그때는 파일을 지우는 것이 맞습니다 — 조용히 통과시키지 않습니다.");

            // (가) 잔차가 있는 리터럴 -> literal 갈래를 타고 Bad에 담겨야 한다.
            var red = new FontSizeScan();
            ScanFontSizeAssignments($"label.fontSize = {inexact};\n", "합성(잔차있음)", red);

            Assert.AreEqual(1, red.Total, $"{LogPrefix} 합성 소스에서 대입을 1건으로 세지 못했습니다 " +
                $"({red.Total}건) — 정규식이 이 형태를 못 봅니다.");
            Assert.AreEqual(1, red.Literal, $"{LogPrefix} 합성 리터럴이 literal 갈래로 분류되지 " +
                $"않았습니다(literal={red.Literal}, 상수경유={red.ViaConstant}, 런타임경유={red.ViaRuntime}, " +
                $"미분류={red.Unclassified.Count}).");
            Assert.AreEqual(1, red.Bad.Count,
                $"{LogPrefix} ★ 잔차가 있는 {inexact}pt를 넣었는데 위반으로 잡히지 않았습니다 " +
                $"({red.Bad.Count}건). 이것이 빨간불이면 프로덕션 스캔의 '위반 0건'은 " +
                "'위반이 없다'가 아니라 '검사가 죽었다'입니다.");

            // (나) 잔차 0인 리터럴 -> 같은 갈래를 타지만 Bad에는 담기지 않아야 한다.
            //     (가)만 있으면 "무조건 담는 검사"도 초록이 된다.
            var green = new FontSizeScan();
            ScanFontSizeAssignments($"label.fontSize = {exact};\n", "합성(잔차0)", green);

            Assert.AreEqual(1, green.Literal,
                $"{LogPrefix} 잔차 0인 합성 리터럴이 literal 갈래로 분류되지 않았습니다.");
            Assert.IsEmpty(green.Bad,
                $"{LogPrefix} 잔차 0인 {exact}pt를 위반으로 잡았습니다 — 이 검사는 <b>무엇이든</b> " +
                "위반이라고 하고 있습니다(그러면 위 (가)의 빨강도 아무 뜻이 없습니다).");

            Debug.Log($"{LogPrefix} 양성 대조 — 배율 {Scale:F3}에서 " +
                $"{inexact}pt(잔차 있음) → 위반 1건 / {exact}pt(잔차 0) → 위반 0건. " +
                "literal 갈래는 프로덕션 분모가 0이지만 <b>동작은 한다</b>는 것이 여기서 증명된다.");
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

            // ★ 2026-09-02 — 요구를 <b>UiGlyphScalePolicy.SnapPoints 호출</b>에서
            //   <b>하한을 아는 스냅</b>으로 바꾼다(UX_FLOW §44-2).
            //   이유: 종전 요구는 <b>결함을 잠그고 있었다</b>. UiGlyphScalePolicy.SnapPoints는 하한을
            //   모르기 때문에 배율 1.25 / 1.75에서 방금 건 하한 13pt를 <b>12pt로 내렸다</b> —
            //   즉 이 감사를 통과하는 유일한 배선이 곧 신고된 결함이었다. 감사는 "그 함수를 불러라"가
            //   아니라 "그 규칙을 거쳐라"를 요구해야 한다.
            string snapCall = nameof(DialogueBubbleRenderer.SnapPointsNotBelow) + "(";
            StringAssert.Contains(snapCall, body,
                $"{LogPrefix} ★ 말풍선 폰트 크기가 스냅 정책을 거치지 않고 반환됩니다. 이 값은 " +
                "(사용자 설정) × (캐릭터 배율) × (만화 배율)의 런타임 곱이라 어떤 홀수든 나올 수 있고, " +
                "정적 상수로는 막을 수 없습니다 — 반드시 마지막에 " +
                $"{nameof(DialogueBubbleRenderer)}.{nameof(DialogueBubbleRenderer.SnapPointsNotBelow)}" +
                "(하한을 아는 스냅)로 스냅하세요.");

            // ★ 그리고 그 스냅이 <b>규칙을 다시 구현하지 않았는지</b>까지 본다 — 규칙이 두 벌이 되면
            //   반드시 갈라진다(이 저장소가 반복해 겪은 사고). 사실 조회는 UiGlyphScalePolicy만 한다.
            int snapDecl = src.IndexOf("int " + nameof(DialogueBubbleRenderer.SnapPointsNotBelow) + "(",
                StringComparison.Ordinal);
            Assert.Greater(snapDecl, 0, $"{LogPrefix} {nameof(DialogueBubbleRenderer.SnapPointsNotBelow)} " +
                "선언을 찾지 못했습니다 — 이름이 바뀌었다면 이 감사도 함께 고쳐야 합니다(허위 통과 방지).");
            int snapEnd = src.IndexOf("\n        }", snapDecl, StringComparison.Ordinal);
            Assert.Greater(snapEnd, snapDecl, $"{LogPrefix} 스냅 함수 본문 끝을 찾지 못했습니다.");
            StringAssert.Contains(nameof(UiGlyphScalePolicy) + ".", src.Substring(snapDecl, snapEnd - snapDecl),
                $"{LogPrefix} ★ 하한을 아는 스냅이 {nameof(UiGlyphScalePolicy)}를 한 번도 묻지 않습니다 — " +
                "\"이 배율에서 잔차 0인 pt는 무엇인가\"라는 규칙을 <b>복사</b>한 것입니다. " +
                "규칙이 두 벌이 되면 다음 라운드에 반드시 한 벌만 고쳐집니다.");

            // 네거티브 컨트롤 — 하한이 실제로 인자로 넘어가는가(상수 0을 넘기면 감사가 무의미해진다).
            StringAssert.Contains("floor", body,
                $"{LogPrefix} 스냅에 하한이 넘어가지 않습니다 — 하한을 아는 스냅에 하한을 안 주면 " +
                "종전 결함(하한이 스냅에 뚫림)이 그대로 돌아옵니다.");
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

        // ============================================================================
        // ★★ (5) 2026-09-06 (dev-platform) — <b>조상 transform 배율</b>: 이 감사군 전체의 사각지대
        // ============================================================================
        //
        // 무엇이 사각지대였나:
        //   · 위 (1)(2)(3)은 전부 <b>소스에 적힌 pt</b>만 본다. 그 글자가 런타임에 어떤 스케일 아래로
        //     들어가는지는 정적으로 알 수 없다 — 원리적 한계이고, 그래서 이 종류의 결함에 <b>영원히
        //     초록</b>이었다.
        //   · 실기 프로브(OverlayCompositionVerdict의 GLYPH-SCALE 줄)는 그 사각지대를 덮을 수
        //     있었지만, 계산이 `pt × canvasScale` 한 줄이라 <b>같은 사각지대를 공유</b>하고 있었다.
        //     2026-09-06에 부채꼴 메뉴 Ø36 축소 폴백이 처음으로 화면에 나오면서 실물이 생겼다.
        //
        // 아래 네 검사는 <b>고친 계산이 실제로 빨개지는가</b>를 양방향으로 못박는다.

        /// <summary>1~64pt에서 기준 배율 잔차가 <b>0인</b> 첫 pt(양성 대조의 "옛 계산이 초록이던 자리").</summary>
        private static int FirstExactPointAtReferenceScale()
        {
            for (int pt = 1; pt <= 64; pt++)
            {
                if (UiGlyphScalePolicy.IsExactAtReferenceScale(pt)) return pt;
            }
            return -1;
        }

        /// <summary>1~64pt에서 기준 배율 잔차가 <b>있는</b> 첫 pt(중립 처방 문구를 뽑는 데 쓴다).</summary>
        private static int FirstInexactPointAtReferenceScale()
        {
            for (int pt = 1; pt <= 64; pt++)
            {
                if (!UiGlyphScalePolicy.IsExactAtReferenceScale(pt)) return pt;
            }
            return -1;
        }

        /// <summary>
        /// 부채꼴 메뉴 <b>축소 폴백</b>이 버튼 묶음에 거는 균일 배율 —
        /// <c>ShrunkDiameterPoints / ButtonDiameterPoints</c>.
        /// <para>★ 0.8182를 숫자로 베끼지 않는다(CLAUDE.md). 두 상수를 참조하므로 폴백 지름이
        /// 바뀌는 날 이 검사가 <b>스스로 따라온다</b>.</para>
        /// </summary>
        private static float ShrinkFallbackTransformScale
            => GearRadialMenuWidget.ShrunkDiameterPoints / GearRadialMenuWidget.ButtonDiameterPoints;

        /// <summary>글리프 판정에 필요한 항만 채운 관측 한 장. 나머지 항목이 다른 줄을 띄우든
        /// 이 검사는 <c>GLYPH-SCALE</c> 줄만 본다.</summary>
        private static OverlayCompositionSnapshot GlyphSnapshot(int points, float canvasScale,
            float transformScale, string label)
            => new OverlayCompositionSnapshot
            {
                CanvasScaleFactor = canvasScale,
                SampleFontSizePoints = points,
                SampleTransformScale = transformScale,
                SampleSurfaceLabel = label,
            };

        private static OverlayCompositionVerdict.Line GlyphLine(OverlayCompositionSnapshot s)
        {
            List<OverlayCompositionVerdict.Line> lines = OverlayCompositionVerdict.Diagnose(s);
            foreach (OverlayCompositionVerdict.Line l in lines)
            {
                if (l.Code == "GLYPH-SCALE") return l;
            }
            Assert.Fail($"{LogPrefix} 판정에 GLYPH-SCALE 줄이 없습니다 — 실기 로그에서 글리프 " +
                $"리샘플을 가를 수 없다는 뜻입니다. 실제 줄: " +
                string.Join(" / ", lines.ConvertAll(l => l.Code)));
            return default;
        }

        /// <summary>
        /// ★★ <b>양성 대조 — 축소 폴백을 실기 진단이 실제로 「번짐」으로 찍는가.</b>
        ///
        /// <para><b>수정 전에는 거짓 초록이었다는 것을 같은 검사 안에서 증명한다.</b> 옛 계산은
        /// <c>requestedPixels = pt × canvasScale</c> 한 줄이었으므로, <b>기준 배율에서 잔차 0인 pt</b>를
        /// 고르면 조상에 어떤 <c>localScale</c>이 걸려 있든 <b>반드시 "리샘플 없음"</b>이 나왔다.
        /// 아래 (가)가 그 옛 계산을 <b>직접 재현</b>해 "정수"임을 보이고, (다)가 새 판정이 같은 입력에서
        /// <b>빨강</b>을 내는 것을 보인다 — 두 값을 <b>서로 다른 방법</b>으로 재서 대조한다.</para>
        ///
        /// <para>(라)의 음성 대조가 없으면 "무엇을 넣어도 빨간 검사"와 구분되지 않는다.</para>
        /// </summary>
        [Test]
        public void 양성_대조_축소_폴백의_transform_배율을_실기_진단이_번짐으로_찍는다()
        {
            float shrink = ShrinkFallbackTransformScale;
            Assert.Less(shrink, 1f - UiGlyphScalePolicy.ExactnessEpsilon,
                $"{LogPrefix} 축소 폴백 배율이 {shrink:F4}로 1과 같습니다 — 축소가 사라졌다면 이 대조는 " +
                "성립하지 않습니다. 폴백 지름이 기준 지름과 같아진 것이라면 이 검사를 지우는 것이 맞고, " +
                "상수 참조가 끊긴 것이라면 여기서 멈추는 것이 맞습니다.");

            int pt = FirstExactPointAtReferenceScale();
            Assert.Greater(pt, 0, $"{LogPrefix} 배율 {Scale:F3}에서 잔차 0인 pt를 찾지 못했습니다.");

            // (가) 옛 계산 재현 — transform 항이 없는 한 줄. 이 값이 정수라는 것이 곧 <거짓 초록>이다.
            float oldRequestedPixels = pt * Scale;
            Assert.AreEqual(Mathf.Round(oldRequestedPixels), oldRequestedPixels, 1e-3f,
                $"{LogPrefix} 양성 대조의 전제가 깨졌습니다 — {pt}pt × {Scale:F3}가 정수가 아닙니다. " +
                "이 대조는 <옛 계산이 초록이던 자리>에서만 뜻이 있습니다.");
            Assert.IsTrue(UiGlyphScalePolicy.IsExact(pt, Scale),
                $"{LogPrefix} 두 인자 술어가 {pt}pt를 잔차 있음으로 봅니다 — 하위호환이 깨졌습니다.");

            // (나) 새 술어 — 같은 pt에 축소 배율을 실으면 잔차가 드러나야 한다.
            Assert.IsFalse(UiGlyphScalePolicy.IsExact(pt, Scale, shrink),
                $"{LogPrefix} ★ {pt}pt × {Scale:F3} × {shrink:F4} = " +
                $"{pt * Scale * shrink:F4}px인데 정수 격자로 판정했습니다 — 셋째 인자가 계산에 " +
                "들어가지 않고 있습니다(인자만 늘리고 본문은 안 고친 형태).");
            Assert.IsFalse(UiGlyphScalePolicy.IsResampleFree(pt, Scale, shrink),
                $"{LogPrefix} ★ 아틀라스 {UiGlyphScalePolicy.AtlasPixels(pt, Scale)}px / 화면 " +
                $"{UiGlyphScalePolicy.DisplayedPixels(pt, Scale, shrink):F2}px인데 <리샘플 없음>이라고 " +
                "답했습니다. 이 술어가 이러면 실기 프로브의 초록은 아무 뜻이 없습니다.");

            // (다) 실기 판정기 — 빨간 줄이 실제로 나오는가.
            OverlayCompositionVerdict.Line red =
                GlyphLine(GlyphSnapshot(pt, Scale, shrink, "합성(축소 폴백)"));
            Assert.AreEqual(CompositionFault.Blur, red.Fault,
                $"{LogPrefix} ★★ 축소 폴백({shrink:F4}배)이 걸린 표면인데 판정이 " +
                $"'{red.Fault}'입니다. 이것이 바로 2026-09-06 이전의 상태입니다 — 진단 도구가 " +
                $"자기 사각지대를 초록으로 덮고 있었습니다. 줄 본문: {red.Text}");

            // (라) 음성 대조 — transform 배율만 1로 되돌리면 같은 pt가 초록이어야 한다.
            //     이것이 없으면 위 (다)는 "무엇이든 빨간 검사"와 구분되지 않는다.
            OverlayCompositionVerdict.Line green =
                GlyphLine(GlyphSnapshot(pt, Scale, 1f, "합성(스케일 없음)"));
            Assert.AreEqual(CompositionFault.None, green.Fault,
                $"{LogPrefix} transform 배율 1인데 번짐으로 찍었습니다 — 오탐입니다. " +
                $"그러면 (다)의 빨강도 아무 뜻이 없습니다. 줄 본문: {green.Text}");

            Debug.Log($"{LogPrefix} 양성 대조(transform) — {pt}pt @ 배율 {Scale:F3}: " +
                $"옛 계산 {oldRequestedPixels:F2}px(정수 = 거짓 초록) / " +
                $"새 계산 아틀라스 {UiGlyphScalePolicy.AtlasPixels(pt, Scale)}px → 화면 " +
                $"{UiGlyphScalePolicy.DisplayedPixels(pt, Scale, shrink):F2}px = " +
                $"{UiGlyphScalePolicy.ResampleRatio(pt, Scale, shrink):F4}배 리샘플 → 판정 {red.Fault}.");
        }

        /// <summary>
        /// ★ <b>하위호환</b> — 셋째 인자를 안 준 기존 호출부가 예전과 같은 답을 내는가.
        /// <para>그리고 <c>transformScale = 1</c>에서는 <b>레이아웃 질문</b>(정수 격자)과
        /// <b>렌더 질문</b>(리샘플 없음)이 같은 답이어야 한다 — 두 술어를 나눈 것이 기존 계약을
        /// 조용히 바꾸지 않았다는 증명이다.</para>
        /// </summary>
        [Test]
        public void transform_인자는_기본값_1로_기존_두_인자_호출부와_같은_답을_낸다()
        {
            int compared = 0;
            foreach (float canvasScale in new[] { 1f, 1.25f, Scale, 1.75f, 2f, 3f })
            {
                for (int pt = 1; pt <= 64; pt++)
                {
                    bool two = UiGlyphScalePolicy.IsExact(pt, canvasScale);
                    bool three = UiGlyphScalePolicy.IsExact(pt, canvasScale, 1f);
                    Assert.AreEqual(two, three,
                        $"{LogPrefix} {pt}pt @ 배율 {canvasScale:F3}에서 두 인자({two})와 " +
                        $"셋째 인자 1({three})의 답이 다릅니다 — 하위호환이 깨졌습니다.");

                    Assert.AreEqual(two, UiGlyphScalePolicy.IsResampleFree(pt, canvasScale, 1f),
                        $"{LogPrefix} {pt}pt @ 배율 {canvasScale:F3}, transform 1에서 " +
                        "<정수 격자>와 <리샘플 없음>의 답이 갈렸습니다. 조상 스케일이 없으면 두 질문은 " +
                        "같은 질문입니다 — 갈렸다면 새 산술이 옛 계약을 바꾼 것입니다.");
                    compared++;
                }
            }

            // 미관측(0)·불량(NaN/음수)도 1과 같게 접히는가 — 관측 실패를 결함으로 바꾸면 오탐이 된다.
            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.AreEqual(UiGlyphScalePolicy.IsExact(13, Scale, 1f),
                    UiGlyphScalePolicy.IsExact(13, Scale, bad),
                    $"{LogPrefix} 미관측 transform 배율({bad})이 1로 접히지 않습니다 — " +
                    "재 보지 않은 항이 결함으로 보고됩니다(오탐).");
            }

            Debug.Log($"{LogPrefix} 하위호환 대조 {compared}쌍 — 두 인자 == 셋째 인자 1.");
        }

        /// <summary>
        /// ★ <b>비대칭 잠금</b> — 아틀라스는 transform을 <b>모르고</b> 화면만 안다.
        /// <para>이 성질이 무너지면(누가 <c>AtlasPixels</c>에 transform 항을 넣으면) 리샘플 비가
        /// 항상 1이 되어 이 라운드의 수정이 통째로 무효가 된다 — <b>조용히</b> 무효가 된다.</para>
        /// </summary>
        [Test]
        public void 아틀라스는_transform_배율을_보지_않고_화면만_본다()
        {
            float shrink = ShrinkFallbackTransformScale;
            foreach (int pt in new[] { 8, 10, 12, 14, 20 })
            {
                int atlasNeutral = UiGlyphScalePolicy.AtlasPixels(pt, Scale);
                Assert.AreEqual(atlasNeutral, UiGlyphScalePolicy.AtlasPixels(pt, Scale),
                    $"{LogPrefix} AtlasPixels가 같은 입력에 다른 답을 냅니다.");

                float displayedNeutral = UiGlyphScalePolicy.DisplayedPixels(pt, Scale, 1f);
                float displayedShrunk = UiGlyphScalePolicy.DisplayedPixels(pt, Scale, shrink);
                Assert.AreEqual(displayedNeutral * shrink, displayedShrunk, 1e-3f,
                    $"{LogPrefix} 화면 픽셀이 transform 배율에 비례하지 않습니다 — " +
                    "메시 쪽에 배율이 안 걸렸다는 뜻이고, 그러면 이 진단은 아무것도 재지 않습니다.");

                Assert.AreEqual(displayedShrunk / atlasNeutral,
                    UiGlyphScalePolicy.ResampleRatio(pt, Scale, shrink), 1e-3f,
                    $"{LogPrefix} 리샘플 비가 <화면 ÷ 아틀라스>와 다릅니다.");
            }
        }

        /// <summary>
        /// ★ <b>처방이 갈리는 이유</b> — 축소 폴백에서는 <b>어떤 pt로도</b> 잔차를 없앨 수 없다.
        ///
        /// <para>그래서 판정 문구가 "pt를 배율에 맞추세요"라고 말하면 <b>틀린 처방</b>이다.
        /// 아래는 그 사실을 1~64pt 전수로 확인하고, 실제 판정 문구가 두 갈래로 갈리는지까지 본다.</para>
        ///
        /// <para>★ 부재 단언(「pt 권고가 <b>없다</b>」)만 두면 문구가 바뀌는 날 <b>조용히 초록</b>이
        /// 된다(CLAUDE.md). 그래서 같은 검사 안에 <b>존재 단언</b>을 함께 둔다 — 중립 갈래에서는
        /// 그 권고가 <b>실재</b>해야 한다. 둘 중 하나만 틀려도 여기서 멈춘다.</para>
        /// </summary>
        [Test]
        public void 축소_폴백에서는_pt를_옮겨도_잔차가_사라지지_않는다()
        {
            float shrink = ShrinkFallbackTransformScale;

            var survivors = new List<string>();
            for (int pt = 1; pt <= 64; pt++)
            {
                if (UiGlyphScalePolicy.IsResampleFree(pt, Scale, shrink))
                {
                    survivors.Add($"{pt}pt → {UiGlyphScalePolicy.ResampleRatio(pt, Scale, shrink):F4}배");
                }
            }
            Assert.IsEmpty(survivors,
                $"{LogPrefix} 배율 {Scale:F3} × 축소 {shrink:F4}에서 잔차 0인 pt가 발견됐습니다 " +
                $"({survivors.Count}건: {string.Join(", ", survivors)}). 그렇다면 판정 문구의 " +
                "「pt로는 못 고친다」는 단정이 거짓이 됩니다 — 문구를 함께 고쳐야 합니다.");

            // ---- 두 갈래 문구가 실제로 갈리는가 ----
            int inexactPt = FirstInexactPointAtReferenceScale();
            Assert.Greater(inexactPt, 0, $"{LogPrefix} 배율 {Scale:F3}에서 잔차가 있는 pt를 못 찾았습니다.");
            int advisedPt = UiGlyphScalePolicy.SnapPoints(inexactPt, Scale);

            string neutralText = GlyphLine(GlyphSnapshot(inexactPt, Scale, 1f, "합성(중립)")).Text;
            string shrunkText = GlyphLine(GlyphSnapshot(
                FirstExactPointAtReferenceScale(), Scale, shrink, "합성(축소 폴백)")).Text;

            // 존재 단언 — 중립 갈래는 "몇 pt로 옮겨라"를 실제로 말한다.
            string ptAdvice = $"예: {advisedPt}pt";
            StringAssert.Contains(ptAdvice, neutralText,
                $"{LogPrefix} 중립 갈래 문구에서 pt 권고('{ptAdvice}')를 찾지 못했습니다 — " +
                "문구 형식이 바뀌었다면 아래 부재 단언이 <아무것도 안 보고 초록>이 됩니다. " +
                $"실제 문구: {neutralText}");

            // 부재 단언 — 축소 갈래는 그 권고를 하면 안 된다(틀린 처방이므로).
            StringAssert.DoesNotContain(ptAdvice, shrunkText,
                $"{LogPrefix} ★ 축소 폴백 판정이 pt 권고('{ptAdvice}')를 하고 있습니다. " +
                "이 조합에서는 어떤 pt로도 잔차가 0이 되지 않으므로 그건 <틀린 처방>이고, " +
                $"틀린 처방을 내는 진단은 없는 진단보다 나쁩니다. 실제 문구: {shrunkText}");

            Assert.AreNotEqual(neutralText, shrunkText,
                $"{LogPrefix} 중립 갈래와 축소 갈래의 문구가 같습니다 — 처방이 갈리지 않았습니다.");

            Debug.Log($"{LogPrefix} 처방 분기 — 중립: pt 권고 있음('{ptAdvice}') / " +
                $"축소 {shrink:F4}배: pt 권고 없음, 1~64pt 전수에 잔차 0 없음.");
        }
    }
}
