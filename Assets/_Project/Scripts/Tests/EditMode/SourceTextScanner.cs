using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 (verify-change 2차) — 소스 감사용 C# 텍스트 스캐너(테스트 전용).
    ///
    /// <para><b>왜 생겼나.</b> 렌더 간격 쓰기 감사가 <c>Platform/</c> 아래만, 그리고 <c>"OnDemandRendering.renderFrameInterval = "</c>
    /// 한 가지 표기만 셌다. 공백 하나(<c>=  2</c>)·줄바꿈·완전 한정 이름·<c>using</c> 별칭·복합 대입(<c>+=</c>)만으로 조용히
    /// 빠져나갔고, 반대로 로그 문자열 <c>$"renderFrameInterval={x}"</c>은 코드처럼 보였다. 판정 전에 <b>주석·문자열·문자
    /// 리터럴을 공백으로 지워</b> "코드로서 실재하는 것"만 남긴다. 줄바꿈은 보존한다(줄 번호가 그대로다).</para>
    ///
    /// <para><b>다루는 표기</b>: <c>//</c>·<c>/* */</c> 주석, <c>"…"</c>(이스케이프 포함), <c>@"…"</c>(<c>""</c> 이스케이프),
    /// <c>$"…"</c>·<c>$@"…"</c>·<c>@$"…"</c>(보간 구멍 <c>{…}</c> 안은 <b>코드</b>로 되돌려 다시 스캔한다), <c>'…'</c>.
    /// C# 11 원시 문자열(<c>"""</c>)은 Unity 6의 C# 9에 없어 다루지 않는다.</para>
    ///
    /// <para>평문(보간 아님) 문자열 리터럴의 <b>내용</b>은 <c>plainLiterals</c>로 돌려준다 — 리플렉션으로 속성 이름을 넘기는
    /// 우회(<c>GetProperty("renderFrameInterval")</c>)를 잡기 위해서다.</para>
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

        /// <summary>주석·문자열·문자 리터럴 내용을 공백으로 지운 소스. 줄바꿈은 남긴다.</summary>
        public static string BlankCommentsAndStrings(string source, List<string> plainLiterals)
        {
            if (string.IsNullOrEmpty(source)) return source ?? string.Empty;
            var buffer = new StringBuilder(source);
            int i = 0;
            ScanCode(source, buffer, ref i, plainLiterals, stopAtUnbalancedCloseBrace: false);
            return buffer.ToString();
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

        private static void ScanCode(string s, StringBuilder buffer, ref int i, List<string> plainLiterals, bool stopAtUnbalancedCloseBrace)
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
                    ScanString(s, buffer, ref i, plainLiterals);
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

        private static void ScanString(string s, StringBuilder buffer, ref int i, List<string> plainLiterals)
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
                    Blank(buffer, segmentStart, i);
                    i++;
                    ScanCode(s, buffer, ref i, plainLiterals, stopAtUnbalancedCloseBrace: true);   // 구멍 안은 코드다.
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
