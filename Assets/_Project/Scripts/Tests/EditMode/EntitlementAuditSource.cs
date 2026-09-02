using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// 엔타이틀먼트 감사 3종의 <b>공용 소스 도구</b> (security, 2026-09-02 — E-9)
    /// ============================================================================
    /// <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-9가 요구한 감사 세 건
    /// (<see cref="EntitlementNotInSaveAuditTests"/> · <see cref="UnlockSwitchScopeAuditTests"/> ·
    /// <see cref="EntitlementFailOpenAuditTests"/>)이 공유하는 스캐너다.
    ///
    /// ============================================================================
    /// ★ 왜 <b>소스 텍스트</b>만 읽고 리플렉션을 한 줄도 쓰지 않는가
    /// ============================================================================
    /// CLAUDE.md 활성 빌드 타깃 규칙: 활성 타깃 반대편 플랫폼 파일은 <b>타입이 존재하지 않는다.</b>
    /// 리플렉션 감사는 없는 타입을 셀 수 없고, 그 0건은 "깨끗함"과 구분되지 않는다.
    /// 스토어 SDK(스팀)를 붙이는 라운드는 거의 확실히 플랫폼 분기를 만들 것이므로,
    /// C층 감사는 처음부터 타깃 무관하게 짜 둔다.
    ///
    /// ============================================================================
    /// ★ 왜 정규식을 한 번도 쓰지 않는가
    /// ============================================================================
    /// .NET의 <c>\b</c>는 <b>유니코드 낱말 경계</b>라 한글을 낱말 문자로 센다. 이 저장소는
    /// 2026-09-02에 그 함정으로 참조 534건 중 245건(46%)을 놓친 적이 있다
    /// (<c>TestClaimExpiryAuditTests</c> 클래스 문서). 이 파일이 훑는 소스에는 한글 주석이
    /// 가득하므로 같은 함정이 그대로 열려 있다. 그래서 <see cref="ContainsIdentifier"/>가
    /// 앞뒤 한 글자만 보고 <c>[A-Za-z0-9_]</c>가 아니면 경계로 인정한다.
    ///
    /// ============================================================================
    /// ★ 왜 <c>TestClaimExpiryAuditTests.StripComments</c>를 재사용하지 않는가 (일부러 중복이다)
    /// ============================================================================
    /// 그쪽 구현은 검증되어 있고 같은 어셈블리라 <c>internal</c>로 부를 수 있다. 그래도 따로 둔다 —
    /// TEAM.md 4절이 기록한 <b>열 번째 거짓 통과 형태</b>("생성기와 검사기가 같은 잘못된 흉내를
    /// 공유해 서로를 확인해 주지 못했다")를 피하기 위해서다. 두 스캐너가 <b>독립</b>이면
    /// 한쪽이 눈이 멀 때 다른 쪽이 다른 답을 낸다. 대신 이 파일의 도구는 전부
    /// <b>자기 네거티브 컨트롤</b>을 가진 테스트에서만 쓰인다(각 감사의 양성/음성 대조 테스트).
    /// </summary>
    internal static class EntitlementAuditSource
    {
        internal const string LogPrefix = "[엔타이틀먼트감사]";

        /// <summary>스캔이 공허해지는 것을 막는 바닥값. 오늘 프로덕션 <c>.cs</c>는 197개다 —
        /// 절반 아래로 떨어지면 그건 "위반이 없다"가 아니라 "스캐너가 눈이 멀었다"이다.</summary>
        internal const int MinProductionFileCount = 150;

        internal static string ScriptsRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts");

        /// <summary>프로덕션 <c>.cs</c> 전량(<c>Tests/</c> 제외). 경로는 절대경로다.</summary>
        internal static string[] ProductionSourceFiles()
        {
            string root = ScriptsRoot;
            string testsDir = Path.Combine(root, "Tests") + Path.DirectorySeparatorChar;
            var result = new List<string>();
            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (path.StartsWith(testsDir, StringComparison.Ordinal)) continue;
                result.Add(path);
            }
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        /// <summary>
        /// C# 주석(<c>//</c>, <c>/* */</c>)만 지우고 <b>문자열/문자 리터럴은 보존</b>한다.
        /// <para>주석을 안 지우면 이 저장소는 통째로 거짓 실패한다 — 프로덕션 주석에 "DLC"가
        /// 20군데 넘게 등장하지만(2026-09-02 실측) <b>배선은 0건</b>이다. 주석 속 언급은 계획이지
        /// 코드가 아니다.</para>
        /// <para>지운 자리에 <c>'\n'</c>을 남겨 줄 수가 보존된다(줄 번호 보고용).</para>
        /// </summary>
        internal static string StripComments(string source)
        {
            if (source == null) return string.Empty;
            var sb = new StringBuilder(source.Length);
            int i = 0, n = source.Length;
            bool inString = false, verbatim = false;
            char quote = '"';

            while (i < n)
            {
                char c = source[i];

                if (!inString)
                {
                    if (c == '/' && i + 1 < n && source[i + 1] == '/')
                    {
                        while (i < n && source[i] != '\n') i++;
                        continue;
                    }
                    if (c == '/' && i + 1 < n && source[i + 1] == '*')
                    {
                        i += 2;
                        while (i + 1 < n && !(source[i] == '*' && source[i + 1] == '/'))
                        {
                            if (source[i] == '\n') sb.Append('\n');
                            i++;
                        }
                        i = Math.Min(i + 2, n);
                        continue;
                    }
                    if (c == '"' || c == '\'')
                    {
                        inString = true;
                        quote = c;
                        verbatim = c == '"' && i > 0 && source[i - 1] == '@';
                        sb.Append(c);
                        i++;
                        continue;
                    }
                    sb.Append(c);
                    i++;
                    continue;
                }

                if (!verbatim && c == '\\' && i + 1 < n)
                {
                    sb.Append(c).Append(source[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == quote)
                {
                    if (verbatim && i + 1 < n && source[i + 1] == quote)
                    {
                        sb.Append(c).Append(c);
                        i += 2;
                        continue;
                    }
                    inString = false;
                    sb.Append(c);
                    i++;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// <paramref name="haystack"/> 안에 <paramref name="identifier"/>가 <b>낱말 단위</b>로
        /// 나오는가. 정규식 없이 앞뒤 한 글자만 본다 — 한글이 붙어 있어도 매치가 사라지지 않는다.
        /// </summary>
        internal static bool ContainsIdentifier(string haystack, string identifier)
            => CountIdentifier(haystack, identifier) > 0;

        /// <summary>낱말 단위 등장 횟수.</summary>
        internal static int CountIdentifier(string haystack, string identifier)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(identifier)) return 0;
            int count = 0, from = 0;
            while (true)
            {
                int at = haystack.IndexOf(identifier, from, StringComparison.Ordinal);
                if (at < 0) return count;
                int before = at - 1;
                int after = at + identifier.Length;
                bool leftOk = before < 0 || !IsIdentifierChar(haystack[before]);
                bool rightOk = after >= haystack.Length || !IsIdentifierChar(haystack[after]);
                if (leftOk && rightOk) count++;
                from = at + 1;
            }
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary>
        /// <paramref name="typeName"/>이라는 이름의 <c>class</c>/<c>struct</c>/<c>enum</c>/
        /// <c>interface</c> <b>선언</b>이 있는가. 문자열 리터럴과 사용처는 걸리지 않는다.
        /// </summary>
        internal static bool DeclaresType(string strippedSource, string typeName)
        {
            foreach (string keyword in new[] { "class", "struct", "enum", "interface" })
            {
                int from = 0;
                while (true)
                {
                    int at = strippedSource.IndexOf(keyword, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    from = at + 1;

                    int before = at - 1;
                    if (before >= 0 && IsIdentifierChar(strippedSource[before])) continue;

                    int p = at + keyword.Length;
                    if (p >= strippedSource.Length || IsIdentifierChar(strippedSource[p])) continue;
                    while (p < strippedSource.Length && (strippedSource[p] == ' ' || strippedSource[p] == '\t')) p++;

                    int start = p;
                    while (p < strippedSource.Length && IsIdentifierChar(strippedSource[p])) p++;
                    if (p == start) continue;
                    if (string.CompareOrdinal(strippedSource, start, typeName, 0, p - start) == 0
                        && p - start == typeName.Length)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 이름이 <paramref name="fragment"/>를 <b>부분 문자열</b>로 포함하는 타입 선언 이름을 전부 모은다
        /// (대소문자 무시). C층 탐지처럼 "아직 이름을 모르는 것"을 찾을 때 쓴다.
        /// </summary>
        internal static List<string> DeclaredTypeNamesContaining(string strippedSource, string fragment)
        {
            var hits = new List<string>();
            foreach (string keyword in new[] { "class", "struct", "enum", "interface" })
            {
                int from = 0;
                while (true)
                {
                    int at = strippedSource.IndexOf(keyword, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    from = at + 1;

                    int before = at - 1;
                    if (before >= 0 && IsIdentifierChar(strippedSource[before])) continue;

                    int p = at + keyword.Length;
                    if (p >= strippedSource.Length || IsIdentifierChar(strippedSource[p])) continue;
                    while (p < strippedSource.Length && (strippedSource[p] == ' ' || strippedSource[p] == '\t')) p++;

                    int start = p;
                    while (p < strippedSource.Length && IsIdentifierChar(strippedSource[p])) p++;
                    if (p == start) continue;

                    string name = strippedSource.Substring(start, p - start);
                    if (name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) hits.Add(name);
                }
            }
            return hits;
        }

        /// <summary>
        /// 중괄호를 세어 <paramref name="typeName"/> 선언의 <b>본문</b>을 잘라 낸다.
        /// 못 찾으면 <c>null</c>(호출자가 그것을 실패로 다룬다 — 조용한 빈 문자열 금지).
        /// </summary>
        internal static string TypeBodyOrNull(string strippedSource, string typeName)
        {
            foreach (string keyword in new[] { "class", "struct", "enum", "interface" })
            {
                int from = 0;
                while (true)
                {
                    int at = strippedSource.IndexOf(keyword + " " + typeName, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    from = at + 1;

                    int before = at - 1;
                    if (before >= 0 && IsIdentifierChar(strippedSource[before])) continue;

                    int after = at + keyword.Length + 1 + typeName.Length;
                    if (after < strippedSource.Length && IsIdentifierChar(strippedSource[after])) continue;

                    int open = strippedSource.IndexOf('{', after);
                    if (open < 0) continue;

                    int depth = 0;
                    for (int p = open; p < strippedSource.Length; p++)
                    {
                        if (strippedSource[p] == '{') depth++;
                        else if (strippedSource[p] == '}')
                        {
                            depth--;
                            if (depth == 0) return strippedSource.Substring(open + 1, p - open - 1);
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// <c>[Serializable]</c>(또는 <c>[System.Serializable]</c>) 바로 뒤에 오는 <b>타입 선언 이름</b>을
        /// 전부 모은다. 세이브 스키마를 <b>이름으로 하드코딩하지 않고</b> 찾기 위한 것이다 —
        /// 훗날 <c>PurchaseRecord</c> 같은 직렬화 타입이 추가돼도 스캔이 저절로 따라간다.
        /// <para>왜 중요한가: 스키마 타입 이름을 감사에 베껴 두면 그 이름이 바뀌는 날
        /// 스캔이 <b>조용히 0건</b>이 되고, 그 0건은 "위반 없음"과 구분되지 않는다.</para>
        /// </summary>
        internal static List<string> SerializableTypeNames(string strippedSource)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(strippedSource)) return names;

            const string marker = "Serializable]";
            int from = 0;
            while (true)
            {
                int at = strippedSource.IndexOf(marker, from, StringComparison.Ordinal);
                if (at < 0) break;
                from = at + marker.Length;

                string name = NextDeclaredTypeNameOrNull(strippedSource, from);
                if (name != null && !names.Contains(name)) names.Add(name);
            }
            return names;
        }

        /// <summary><paramref name="from"/> 이후 <b>가장 먼저</b> 나오는 타입 선언의 이름.</summary>
        private static string NextDeclaredTypeNameOrNull(string strippedSource, int from)
        {
            int best = int.MaxValue;
            string bestName = null;

            foreach (string keyword in new[] { "class", "struct", "enum", "interface" })
            {
                int cursor = from;
                while (true)
                {
                    int at = strippedSource.IndexOf(keyword, cursor, StringComparison.Ordinal);
                    if (at < 0) break;
                    cursor = at + 1;

                    int before = at - 1;
                    if (before >= 0 && IsIdentifierChar(strippedSource[before])) continue;

                    int p = at + keyword.Length;
                    if (p >= strippedSource.Length || IsIdentifierChar(strippedSource[p])) continue;
                    while (p < strippedSource.Length && (strippedSource[p] == ' ' || strippedSource[p] == '\t')) p++;

                    int start = p;
                    while (p < strippedSource.Length && IsIdentifierChar(strippedSource[p])) p++;
                    if (p == start) break;

                    if (at < best)
                    {
                        best = at;
                        bestName = strippedSource.Substring(start, p - start);
                    }
                    break;
                }
            }
            return bestName;
        }

        /// <summary>
        /// 타입 본문에서 <c>public &lt;형&gt; &lt;이름&gt;;</c> 꼴의 <b>필드 이름</b>만 모은다.
        /// 메서드(<c>(</c> 포함)·식 본문(<c>=&gt;</c>)·<c>const</c>·<c>static</c>은 제외한다
        /// (JsonUtility가 직렬화하는 것은 인스턴스 public 필드뿐이다).
        /// </summary>
        internal static List<string> PublicFieldNames(string typeBody)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(typeBody)) return names;

            foreach (string raw in typeBody.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("public ", StringComparison.Ordinal)) continue;
                if (line.IndexOf('(') >= 0) continue;
                if (line.IndexOf("=>", StringComparison.Ordinal) >= 0) continue;
                if (!line.EndsWith(";", StringComparison.Ordinal)) continue;
                if (line.IndexOf(" const ", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf(" static ", StringComparison.Ordinal) >= 0) continue;

                string body = line.Substring(0, line.Length - 1);
                int eq = body.IndexOf('=');
                if (eq >= 0) body = body.Substring(0, eq);

                string[] parts = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;          // public + 형 + 이름
                names.Add(parts[parts.Length - 1]);
            }
            return names;
        }

        /// <summary>
        /// 식별자를 <b>낱말 조각</b>으로 쪼갠다(전부 소문자). <c>wornBackpack</c> → <c>worn</c>·<c>backpack</c>,
        /// <c>DLCPack</c> → <c>dlc</c>·<c>pack</c>.
        /// <para>왜 부분 문자열 매칭을 쓰지 않는가: <c>backpack</c>에 <c>pack</c>이 들어 있다.
        /// 그대로 부분 문자열로 잡으면 <b>정직한 필드가 빨개지고</b>, 그런 감사는 몇 번 만에 꺼진다
        /// (정의서: 정당한 유저를 잠그는 조치는 무단 사용 열 건보다 비싸다 — 감사도 같다).</para>
        /// </summary>
        internal static List<string> CamelSegments(string identifier)
        {
            var segments = new List<string>();
            if (string.IsNullOrEmpty(identifier)) return segments;

            var current = new StringBuilder();
            for (int i = 0; i < identifier.Length; i++)
            {
                char c = identifier[i];
                if (c == '_')
                {
                    if (current.Length > 0) { segments.Add(current.ToString().ToLowerInvariant()); current.Length = 0; }
                    continue;
                }
                if (c >= 'A' && c <= 'Z')
                {
                    if (current.Length == 0) { current.Append(c); continue; }
                    char last = current[current.Length - 1];
                    bool lastUpper = last >= 'A' && last <= 'Z';
                    bool nextLower = i + 1 < identifier.Length
                                     && identifier[i + 1] >= 'a' && identifier[i + 1] <= 'z';
                    if (!lastUpper || nextLower)
                    {
                        segments.Add(current.ToString().ToLowerInvariant());
                        current.Length = 0;
                    }
                    current.Append(c);
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0) segments.Add(current.ToString().ToLowerInvariant());
            return segments;
        }

        /// <summary>줄 번호(1-기반). 실패 메시지가 "어디"를 가리킬 수 있게 한다.</summary>
        internal static int LineNumberAt(string source, int index)
        {
            int line = 1;
            int stop = Math.Min(index, source.Length);
            for (int i = 0; i < stop; i++) if (source[i] == '\n') line++;
            return line;
        }
    }
}
