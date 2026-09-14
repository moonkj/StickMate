using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 (verify-change 2차) — 소스 감사용 C# 텍스트 스캐너(테스트 전용).
    ///
    /// <para><b>왜 생겼나.</b> 렌더 간격 쓰기 감사가 <c>Platform/</c> 아래만, 그리고 <c>"OnDemandRendering.renderFrameInterval = "</c>
    /// 한 가지 표기만 셌다. 공백 하나(<c>=  2</c>)·줄바꿈·완전 한정 이름·<c>using</c> 별칭·복합 대입(<c>+=</c>)만으로 조용히
    /// 빠져나갔고, 반대로 로그 문자열 <c>$"renderFrameInterval={x}"</c>은 코드처럼 보였다. 판정 전에 <b>주석·문자열·문자
    /// 리터럴을 공백으로 지워</b> "코드로서 실재하는 것"만 남긴다. 줄바꿈은 보존한다(줄 번호·위치가 그대로다).</para>
    ///
    /// <para><b>다루는 표기</b>: <c>//</c>·<c>/* */</c> 주석, <c>"…"</c>(이스케이프 포함), <c>@"…"</c>(<c>""</c> 이스케이프),
    /// <c>$"…"</c>·<c>$@"…"</c>·<c>@$"…"</c>, <c>'…'</c>. C# 11 원시 문자열(<c>"""</c>)은 Unity 6의 C# 9에 없어 다루지 않는다.</para>
    ///
    /// <para><b>보간 구멍 <c>{…}</c>.</b> 기본은 <b>코드로 되돌려</b> 다시 스캔한다 — 구멍 안의 대입도 실제 대입이라 쓰기 탐지기는
    /// 그것을 봐야 한다. ★ 4차(verify-change 3차 V5): <b>배선 감사</b>는 반대다. 같은 파일의 로그
    /// <c>Debug.Log($"…{SessionExitMarker.IsStarted}…")</c>가 조건식 니들을 채워, 조건에서 그 사실을 지워도 초록이었다.
    /// 배선 감사는 <c>blankInterpolationHoles: true</c>로 구멍까지 지운 코드를 본다.</para>
    ///
    /// <para>평문(보간 아님) 문자열 리터럴의 <b>내용</b>은 <c>plainLiterals</c>로 돌려준다 — 리플렉션으로 이름을 넘기는
    /// 우회(<c>GetProperty("renderFrameInterval")</c>)를 잡거나, 문자열 내용 자체를 단언해야 할 때 쓴다.</para>
    /// </summary>
    internal static class SourceTextScanner
    {
        /// <summary>Assets 아래 모든 프로덕션 C# 파일(테스트 폴더 제외, 정렬됨).</summary>
        public static IReadOnlyList<string> ProductionSourceFilesUnderAssets()
        {
            var result = new List<string>();
            foreach (string path in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.IndexOf("/Tests/", StringComparison.Ordinal) >= 0) continue;
                result.Add(path);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>주석·문자열·문자 리터럴 내용을 공백으로 지운 소스(보간 구멍은 코드로 남긴다). 줄바꿈은 남긴다.</summary>
        public static string BlankCommentsAndStrings(string source, List<string> plainLiterals)
            => BlankCommentsAndStrings(source, plainLiterals, blankInterpolationHoles: false);

        /// <param name="blankInterpolationHoles">참이면 보간 구멍 안까지 지운다(배선 감사용 — 로그 문자열이 니들을 채우지 않게).</param>
        public static string BlankCommentsAndStrings(string source, List<string> plainLiterals, bool blankInterpolationHoles)
        {
            if (string.IsNullOrEmpty(source)) return source ?? string.Empty;
            var buffer = new StringBuilder(source);
            int i = 0;
            ScanCode(source, buffer, ref i, plainLiterals, stopAtUnbalancedCloseBrace: false, blankHoles: blankInterpolationHoles);
            return buffer.ToString();
        }

        /// <summary>지운 코드에서 식별자 토큰 수(앞뒤가 식별자 문자가 아닌 곳, <c>@</c> 접두 허용).</summary>
        public static int CountIdentifier(string code, string identifier)
            => Regex.Matches(code, @"(?<![\w@])@?" + Regex.Escape(identifier) + @"(?!\w)", RegexOptions.CultureInvariant).Count;

        /// <summary>지운 코드에서 멤버 접근 토큰 수(<c>.X</c>, 사이 공백·줄바꿈 허용). 호출·메서드 그룹·<c>nameof</c> 안 전부 센다.</summary>
        public static int CountMemberAccess(string code, string member)
            => Regex.Matches(code, @"\.\s*@?" + Regex.Escape(member) + @"(?!\w)", RegexOptions.CultureInvariant).Count;

        /// <summary>
        /// 지운 코드에서 블록 본문 멤버(<c>{ … }</c>)를 시그니처부터 짝이 맞는 닫는 중괄호까지 잘라 준다. 못 찾으면 null.
        /// 문자열·주석이 지워진 코드라 중괄호 계수가 흔들리지 않는다. 식 본문(<c>=&gt;</c>) 멤버에는 쓰지 않는다.
        /// </summary>
        public static string BlockMemberBody(string code, string signature)
        {
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            if (start < 0) return null;
            int open = code.IndexOf('{', start + signature.Length);
            if (open < 0) return null;
            int depth = 0;
            for (int k = open; k < code.Length; k++)
            {
                if (code[k] == '{') depth++;
                else if (code[k] == '}' && --depth == 0) return code.Substring(start, k + 1 - start);
            }
            return null;
        }

        private static void Blank(StringBuilder buffer, int from, int to)
        {
            for (int k = Math.Max(0, from); k < to && k < buffer.Length; k++)
            {
                if (buffer[k] != '\n' && buffer[k] != '\r') buffer[k] = ' ';
            }
        }

        private static bool StartsString(string s, int i)
        {
            char c = s[i];
            if (c == '"') return true;
            if (c != '@' && c != '$') return false;
            if (i + 1 < s.Length && s[i + 1] == '"') return true;
            return i + 2 < s.Length && (s[i + 1] == '@' || s[i + 1] == '$') && s[i + 1] != c && s[i + 2] == '"';
        }

        private static void ScanCode(string s, StringBuilder buffer, ref int i, List<string> plainLiterals,
            bool stopAtUnbalancedCloseBrace, bool blankHoles)
        {
            int depth = 0;
            while (i < s.Length)
            {
                char c = s[i];
                char next = i + 1 < s.Length ? s[i + 1] : '\0';

                if (c == '/' && next == '/')
                {
                    int end = s.IndexOf('\n', i);
                    if (end < 0) end = s.Length;
                    Blank(buffer, i, end);
                    i = end;
                    continue;
                }
                if (c == '/' && next == '*')
                {
                    int end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? s.Length : end + 2;
                    Blank(buffer, i, end);
                    i = end;
                    continue;
                }
                if (c == '\'')
                {
                    int end = i + 1;
                    while (end < s.Length && s[end] != '\'' && s[end] != '\n')
                    {
                        if (s[end] == '\\') end++;
                        end++;
                    }
                    end = Math.Min(end + 1, s.Length);
                    Blank(buffer, i + 1, end - 1);
                    i = end;
                    continue;
                }
                if (StartsString(s, i))
                {
                    ScanString(s, buffer, ref i, plainLiterals, blankHoles);
                    continue;
                }
                if (stopAtUnbalancedCloseBrace)
                {
                    if (c == '{') depth++;
                    else if (c == '}')
                    {
                        if (depth == 0) return;   // 보간 구멍의 끝 — i는 '}'를 가리킨 채 돌아간다.
                        depth--;
                    }
                }
                i++;
            }
        }

        private static void ScanString(string s, StringBuilder buffer, ref int i, List<string> plainLiterals, bool blankHoles)
        {
            bool verbatim = false, interpolated = false;
            while (s[i] != '"')
            {
                if (s[i] == '@') verbatim = true;
                else if (s[i] == '$') interpolated = true;
                i++;
            }
            i++;   // 여는 따옴표

            var text = new StringBuilder();
            int segmentStart = i;
            while (i < s.Length)
            {
                char c = s[i];
                if (verbatim && c == '"')
                {
                    if (i + 1 < s.Length && s[i + 1] == '"') { text.Append('"'); i += 2; continue; }
                    Blank(buffer, segmentStart, i);
                    i++;
                    if (!interpolated) plainLiterals?.Add(text.ToString());
                    return;
                }
                if (!verbatim && c == '\\')
                {
                    if (i + 1 < s.Length) text.Append(s[i + 1]);
                    i += 2;
                    continue;
                }
                if (!verbatim && (c == '"' || c == '\n'))
                {
                    Blank(buffer, segmentStart, i);
                    i++;
                    if (!interpolated) plainLiterals?.Add(text.ToString());
                    return;
                }
                if (interpolated && c == '{')
                {
                    if (i + 1 < s.Length && s[i + 1] == '{') { text.Append('{'); i += 2; continue; }
                    // ★ 4차 — 구멍을 여는 '{'까지 지운다(닫는 '}'는 다음 조각과 함께 지워진다). 여는 쪽을 남겼더니 보간 문자열
                    //   하나마다 짝 없는 '{'가 코드에 남아, 중괄호로 본문을 자르는 감사(BlockMemberBody)가 끝을 찾지 못했다.
                    Blank(buffer, segmentStart, i + 1);
                    i++;
                    int holeStart = i;
                    ScanCode(s, buffer, ref i, plainLiterals, stopAtUnbalancedCloseBrace: true, blankHoles: blankHoles);   // 구멍 안은 코드다.
                    if (blankHoles) Blank(buffer, holeStart, i);
                    segmentStart = i;
                    continue;
                }
                text.Append(c);
                i++;
            }
            Blank(buffer, segmentStart, s.Length);
        }
    }
}
