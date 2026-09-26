using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>사용자 허가(임대)는 사용자가 부르지 않은 표면을 절대 드러내지 않는다</b> — 2026-09-15 N-20 잠금(씬 없음).
    /// 설계 정본: <c>docs/systems/AUTO_SURFACE_LEASE_AXIS.md</c> §3-2 · §6-3 · ROADMAP §60-9 N-20 해제 조건 2.
    /// 런타임 관측은 <c>Tests/PlayMode/AutoSurfaceLeaseAxisTests</c>가 한다.
    ///
    /// ============================================================================
    /// T-A — 진리표
    /// ============================================================================
    /// 기대값은 <b>설계 문서의 상수 표</b>에서 온다. 정책 함수로 기대값을 만들지 않는다(TEAM.md 「생성기와 검사기가 같이 틀린다」).
    /// 같은 루프에서 ① 옛 창구 ⊆ 새 창구(8행) ② 허가에 대한 단조(4쌍) ③ 옛 창구와 갈리는 칸이 정확히 FFT · FTT 둘을 함께 단언한다.
    ///
    /// ============================================================================
    /// T-B — 소비자 분류 스캔(독자 명부를 손으로 적지 않는다)
    /// ============================================================================
    /// 옛 창구(<c>.ArePanelsSuppressed</c>) 독자는 프로덕션 소스 전수 스캔으로 뽑는다. 아래 <see cref="Classification"/>은 독자 명부가 아니라
    /// <b>설계 결정</b>(어느 표면이 사용자가 여는 것이고 어느 것이 자동인가, §2-2)이고, 스캔 결과와 <b>양방향</b>으로 대조한다 —
    /// 표에 없는 독자는 빨강(새 소비자가 분류 없이 생김), 표에 있는데 읽지 않으면 빨강(표가 낡음).
    /// <list type="bullet">
    ///   <item><b>(나) 자동 표면</b>: 새 창구 호출(인자에 옛 창구와 허가가 둘 다)이 있고, 그 호출 <b>밖</b>에서 옛 창구를 읽는 곳이 0이고,
    ///     조건에 등급 2 전용 값(<c>HidesScreenSurfaces</c> · <c>IsUserSummonBlocked</c>)이나 춤 축(<c>IsForeignFullscreenAppPresent</c>)을
    ///     새 창구 없이 쓰는 <c>if</c>가 0이다. ★ <b>가드 본문을 보지 않는다</b> — 본문이 <c>return;</c>뿐인 가드도 잡는다
    ///     (verify-change E-4가 적발한 F18f 사각지대: 리마인더 · 설정창 자동 재오픈의 가드가 정확히 그 형태다).</item>
    ///   <item><b>(가) 사용자 표면</b>: 옛 창구를 읽고 새 창구는 읽지 않는다. 새 창구는 허가가 참이면 참이라, 사용자 표면이 읽으면
    ///     허가가 나는 순간 스스로 닫혀 등급 1 입구가 다시 사라진다(E-1·E-2 회귀).</item>
    /// </list>
    /// 설정창 한 파일에 두 분류가 있어(재오픈 (나) · 닫기/시트 복귀 (가)) 그 형만 <b>메서드 단위</b>로 잰다.
    ///
    /// <para>★ <b>주석 · 문자열 처리</b>: 이 스캐너는 줄 단위 <c>//</c> 절단(F18e 계열)이 아니라 작은 어휘기로 주석과 문자열 · 문자 리터럴을
    /// 공백으로 지운다(길이 · 줄바꿈 보존). 줄 단위 절단은 문자열 속 <c>//</c>에서 줄 나머지를 버리고, 보간 구멍 속 따옴표에서 괄호 짝이 틀어진다.</para>
    ///
    /// ============================================================================
    /// T-C — 스캐너 자기 교정
    /// ============================================================================
    /// 알려진 표본(잡을 것 · 잡지 말 것)으로 먼저 교정한다 — 이게 없으면 T-B의 «위반 0»이 «스캐너가 눈이 멀었다»와 구별되지 않는다.
    ///
    /// <para>식별자 규칙(CLAUDE.md): 형 · 프로퍼티 · 정책 함수 이름은 전부 <c>nameof</c>다. private 메서드 서명만 문자열이고,
    /// T-B가 그 서명의 절단 성공(존재)을 같은 테스트 안에서 먼저 단언한다.</para>
    /// </summary>
    public sealed class UnsummonedSurfaceAxisTests
    {
        // ==================================================================
        // T-A — 진리표 (인덱스 = s·4 + r·2 + g, 순서 FFF FFT FTF FTT TFF TFT TTF TTT)
        // ==================================================================

        /// <summary>설계 §3-2 표의 옛 창구 행(<c>SuppressesPanels</c> = s ∨ (r ∧ ¬g)).</summary>
        private static readonly bool[] ExpectedPanelsSuppressed = { false, false, true, false, true, true, true, true };

        /// <summary>설계 §3-2 표의 새 창구 행(<c>SuppressesUnsummonedSurfaces</c> = s ∨ r ∨ g).</summary>
        private static readonly bool[] ExpectedUnsummonedSuppressed = { false, true, true, true, true, true, true, true };

        /// <summary>설계 §3-2 「P와 갈리는 칸은 정확히 2개」 — 이력 칸과 결함 칸.</summary>
        private static readonly string[] ExpectedDivergentCells = { "FFT", "FTT" };

        /// <summary>두 인자 함수 자체의 표(인덱스 = 옛 창구·2 + 허가) — 도달 가능한 네 조합 전부.</summary>
        private static readonly bool[] ExpectedTwoInputTable = { false, true, true, true };

        private static bool Bit(int row, int index) => ((row >> index) & 1) != 0;

        private static string CellName(int row) =>
            (Bit(row, 2) ? "T" : "F") + (Bit(row, 1) ? "T" : "F") + (Bit(row, 0) ? "T" : "F");

        [Test]
        public void TA_진리표_8행이_설계_상수와_같고_옛_창구를_포함하며_허가에_단조이고_옛_창구와_정확히_두_칸이_갈린다()
        {
            Assert.AreEqual(8, ExpectedPanelsSuppressed.Length, "설계 상수 표(옛 창구)가 8행이 아닙니다.");
            Assert.AreEqual(8, ExpectedUnsummonedSuppressed.Length, "설계 상수 표(새 창구)가 8행이 아닙니다.");

            var actual = new bool[8];
            var divergent = new List<string>();
            int panelsTrueRows = 0;
            for (int row = 0; row < 8; row++)
            {
                bool s = Bit(row, 2), r = Bit(row, 1), g = Bit(row, 0);
                bool p = UserSurfaceSummonPolicy.SuppressesPanels(s, r, g);
                bool u = UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(p, g);
                actual[row] = u;

                Assert.AreEqual(ExpectedPanelsSuppressed[row], p,
                    $"{CellName(row)}: 옛 창구(SuppressesPanels)가 설계 표와 다릅니다 — 이 진리표의 입력이 흔들렸습니다(아래 판정 무효).");
                Assert.AreEqual(ExpectedUnsummonedSuppressed[row], u,
                    $"★ {CellName(row)}: 새 창구가 설계 표와 다릅니다(기대 {ExpectedUnsummonedSuppressed[row]}, 실제 {u}). " +
                    "FTT가 거짓이면 사용자가 창을 연 동안 자동 표면이 발표 화면 위로 되살아납니다(N-20).");
                if (p)
                {
                    panelsTrueRows++;
                    Assert.IsTrue(u, $"★ {CellName(row)}: 포함 위반 — 옛 창구가 억제하는 칸을 새 창구가 풀었습니다(등급 1 진입 회수 · 등급 2 포함이 깨짐).");
                }
                if (p != u) divergent.Add(CellName(row));
            }

            int expectedPanelsTrueRows = ExpectedPanelsSuppressed.Count(b => b);
            Assert.Greater(expectedPanelsTrueRows, 0, "설계 표의 옛 창구 참 칸이 0 — 포함 단언이 아무것도 재지 않습니다.");
            Assert.AreEqual(expectedPanelsTrueRows, panelsTrueRows, "포함 단언이 설계 표의 참 칸 전부에서 돌지 않았습니다.");

            int pairs = 0;
            for (int sr = 0; sr < 4; sr++)
            {
                int withoutGrant = sr * 2, withGrant = sr * 2 + 1;
                pairs++;
                Assert.IsFalse(actual[withoutGrant] && !actual[withGrant],
                    $"★ 단조 위반 — {CellName(withoutGrant)} → {CellName(withGrant)}: 허가가 켜지자 억제가 풀렸습니다(사용자 허가가 부르지 않은 표면을 드러냄).");
            }
            Assert.AreEqual(4, pairs, "허가 단조 쌍이 4개가 아닙니다.");

            // 음성 대조 — 0칸이면 편승으로 되돌아간 것(M-b), 3칸 이상이면 억제 규칙이 흔들린 것.
            CollectionAssert.AreEqual(ExpectedDivergentCells, divergent,
                $"★ 옛 창구와 갈리는 칸이 설계와 다릅니다: [{string.Join(", ", divergent)}] (기대 [{string.Join(", ", ExpectedDivergentCells)}]).");

            for (int k = 0; k < 4; k++)
            {
                bool p = Bit(k, 1), g = Bit(k, 0);
                Assert.AreEqual(ExpectedTwoInputTable[k], UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(p, g),
                    $"★ 두 인자 표 (옛 창구={p}, 허가={g})가 설계(= 옛 창구 ∨ 허가)와 다릅니다.");
            }
        }

        // ==================================================================
        // T-B — 소비자 분류 스캔
        // ==================================================================

        private enum Axis { UserSummoned, Unsummoned }

        private sealed class Entry
        {
            public readonly string TypeName;
            public readonly string MethodSignature;   // null이면 형 전체
            public readonly Axis Axis;
            public readonly string Why;

            public Entry(string typeName, string methodSignature, Axis axis, string why)
            {
                TypeName = typeName;
                MethodSignature = methodSignature;
                Axis = axis;
                Why = why;
            }

            public string Label => MethodSignature == null ? TypeName : $"{TypeName} «{MethodSignature}»";
        }

        /// <summary>
        /// <b>설계 결정 표</b>(§2-2의 #1~#9) — 독자 명부가 아니다. 독자는 스캔이 뽑고 이 표와 양방향으로 맞춘다.
        /// 새 자동 표면(예: N-21 우클릭 사전 안내의 자발 노출)이 옛 창구나 새 창구를 읽기 시작하면 여기 (나)로 올린다.
        /// </summary>
        private static readonly Entry[] Classification =
        {
            new Entry(nameof(CharacterInfoWindow), null, Axis.UserSummoned, "#1 정보창 닫기 — 사용자가 연다(부채꼴 [캐릭터] · ⌃⌥⌘I)"),
            new Entry(nameof(GearRadialMenuWidget), null, Axis.UserSummoned, "#2 부채꼴 — 캐릭터 우클릭 · 톱니"),
            new Entry(nameof(PopoverPanel), null, Axis.UserSummoned, "#3 팝오버 3종 — 부채꼴 버튼만 연다"),
            new Entry(nameof(SettingsWindow), "private void Update(", Axis.UserSummoned, "#4 설정창 닫기 — 사용자가 연 창"),
            new Entry(nameof(SettingsWindow), "private void RestoreInfoWindowIfNeeded(", Axis.UserSummoned, "#5 시트 복귀 — 사용자가 설정창을 닫은 결과(E-2)"),
            new Entry(nameof(SettingsWindow), "private void TickReopenAfterSuspend(", Axis.Unsummoned, "#6 설정창 자동 재오픈 — 자동(적발 ③)"),
            new Entry(nameof(TodoPostItWidget), null, Axis.Unsummoned, "#7 메모 카드 + 클릭 차단막 — 상시 HUD, 이 파일의 허가 경로 0"),
            new Entry(nameof(TodoReminderDirector), null, Axis.Unsummoned, "#8 할 일 리마인더 — 밀어내기"),
            new Entry(nameof(WindowCrashDirector), null, Axis.Unsummoned, "#9 창 크랙 오버레이 — 자동"),
            // ★ 2026-09-26 (해제 조건 7-b) — 그라피티가 새 창구 독자로 합류했다. 이 파일에는 두 자리가 있다:
            //   층 1 명령 가능 판정(GetAvailability)과 층 2 낙서 수명(MonitorRegion). 둘 다 (나)이므로 형 전체 항목
            //   하나로 충분하고, «두 자리 각각에 가드가 있는가»는 아래 메서드 단위 단언 둘이 따로 잰다
            //   (형 전체 항목만 두면 한 자리에만 가드가 있어도 통과한다 — #9 크랙에서 이미 겪은 사각지대다).
            new Entry(nameof(GraffitiDirector), null, Axis.Unsummoned, "#10 그라피티 — 명령 가능 판정(층 1) + 낙서 오버레이 수명(층 2)"),
        };

        private static readonly string PanelsRead = "." + nameof(StickmanAgent.ArePanelsSuppressed);
        private static readonly string GrantRead = "." + nameof(StickmanAgent.IsUserSummonGrantActive);

        /// <summary>자동 표면 가드에 새 창구 대신 들어가면 안 되는 값 — 등급 2 전용 두 값과 춤 축(§3-5 기각안 (가) · (나)).</summary>
        private static readonly string[] ReplacementReads =
        {
            "." + nameof(StickmanAgent.HidesScreenSurfaces),
            "." + nameof(StickmanAgent.IsUserSummonBlocked),
            "." + nameof(StickmanAgent.IsForeignFullscreenAppPresent),
        };

        private static readonly Regex UnsummonedCallRegex =
            new Regex(@"\." + Regex.Escape(nameof(UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces)) + @"\s*\(");

        /// <summary>앞 글자 경계는 ASCII로 건다 — .NET <c>\b</c>는 한글을 낱말 문자로 센다(TEAM.md 사고).</summary>
        private static readonly Regex IfHeadRegex = new Regex(@"(?<![A-Za-z0-9_])if\s*\(");

        private static Regex MemberRead(string dotted) => new Regex(Regex.Escape(dotted) + "(?![A-Za-z0-9_])");

        private static bool DeclaresType(string sanitized, string typeName) =>
            new Regex(@"(?<![A-Za-z0-9_])(class|struct)\s+" + Regex.Escape(typeName) + "(?![A-Za-z0-9_])").IsMatch(sanitized);

        private static int CountOrdinal(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++) if (text[i] == '\n') line++;
            return line;
        }

        private static string LineTextAt(string text, int index)
        {
            int start = text.LastIndexOf('\n', System.Math.Max(0, index - 1)) + 1;
            int end = text.IndexOf('\n', index);
            if (end < 0) end = text.Length;
            return Regex.Replace(text.Substring(start, end - start), @"\s+", " ").Trim();
        }

        /// <summary>
        /// 주석 · 문자열 · 문자 리터럴을 공백으로 지운다(길이 · 줄바꿈 보존, 따옴표 자체는 남김).
        /// 보간 문자열(<c>$"…{식}…"</c>)의 구멍은 코드로 읽는다 — 구멍 안에 또 문자열이 있어도 짝이 틀어지지 않는다.
        /// </summary>
        private static string Sanitize(string source)
        {
            string src = source.Replace("\r\n", "\n");
            char[] o = src.ToCharArray();
            int n = src.Length;
            var verbatim = new List<bool>();
            var inHole = new List<bool>();
            var depth = new List<int>();
            int i = 0;
            while (i < n)
            {
                int top = verbatim.Count - 1;
                char c = src[i];
                char next = i + 1 < n ? src[i + 1] : '\0';

                if (top >= 0 && !inHole[top])
                {
                    // 보간 문자열의 글자 부분
                    if (c == '{')
                    {
                        if (next == '{') { o[i] = ' '; o[i + 1] = ' '; i += 2; continue; }
                        inHole[top] = true;
                        depth[top] = 0;
                        i++;
                        continue;
                    }
                    if (c == '"')
                    {
                        if (verbatim[top] && next == '"') { o[i] = ' '; o[i + 1] = ' '; i += 2; continue; }
                        verbatim.RemoveAt(top);
                        inHole.RemoveAt(top);
                        depth.RemoveAt(top);
                        i++;
                        continue;
                    }
                    if (!verbatim[top] && c == '\\' && i + 1 < n)
                    {
                        o[i] = ' ';
                        if (src[i + 1] != '\n') o[i + 1] = ' ';
                        i += 2;
                        continue;
                    }
                    if (c != '\n') o[i] = ' ';
                    i++;
                    continue;
                }

                // 코드(최상위 또는 보간 구멍 안)
                if (c == '/' && next == '/')
                {
                    while (i < n && src[i] != '\n') { o[i] = ' '; i++; }
                    continue;
                }
                if (c == '/' && next == '*')
                {
                    o[i] = ' '; o[i + 1] = ' '; i += 2;
                    while (i < n && !(src[i] == '*' && i + 1 < n && src[i + 1] == '/'))
                    {
                        if (src[i] != '\n') o[i] = ' ';
                        i++;
                    }
                    if (i < n) { o[i] = ' '; if (i + 1 < n) o[i + 1] = ' '; i += 2; }
                    continue;
                }
                if (top >= 0)
                {
                    if (c == '{') { depth[top]++; i++; continue; }
                    if (c == '}')
                    {
                        if (depth[top] == 0) inHole[top] = false;
                        else depth[top]--;
                        i++;
                        continue;
                    }
                }
                bool interpolated = (c == '$' && next == '"')
                    || (c == '$' && next == '@' && i + 2 < n && src[i + 2] == '"')
                    || (c == '@' && next == '$' && i + 2 < n && src[i + 2] == '"');
                if (interpolated)
                {
                    bool isVerbatim = next == '@' || c == '@';
                    i += next == '"' ? 2 : 3;
                    verbatim.Add(isVerbatim);
                    inHole.Add(false);
                    depth.Add(0);
                    continue;
                }
                if (c == '@' && next == '"')
                {
                    i += 2;
                    while (i < n)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < n && src[i + 1] == '"') { o[i] = ' '; o[i + 1] = ' '; i += 2; continue; }
                            break;
                        }
                        if (src[i] != '\n') o[i] = ' ';
                        i++;
                    }
                    i++;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    i++;
                    while (i < n && src[i] != quote && src[i] != '\n')
                    {
                        if (src[i] == '\\' && i + 1 < n) { o[i] = ' '; o[i + 1] = ' '; i += 2; continue; }
                        o[i] = ' ';
                        i++;
                    }
                    i++;
                    continue;
                }
                i++;
            }
            return new string(o);
        }

        /// <summary>지운 텍스트에서 여는 괄호의 짝(문자열은 이미 비었으므로 괄호만 센다). 못 찾으면 -1.</summary>
        private static int MatchClose(string sanitized, int openIndex, char open, char close)
        {
            int depth = 0;
            for (int i = openIndex; i < sanitized.Length; i++)
            {
                char c = sanitized[i];
                if (c == open) depth++;
                else if (c == close && --depth == 0) return i;
            }
            return -1;
        }

        /// <summary>서명(여는 괄호까지 포함)이 정확히 한 번 나오면 그 메서드 본문의 중괄호 범위를 돌려준다.</summary>
        private static bool TryMethodBody(string sanitized, string signature, out int bodyOpen, out int bodyClose)
        {
            bodyOpen = bodyClose = -1;
            if (CountOrdinal(sanitized, signature) != 1) return false;
            int sig = sanitized.IndexOf(signature, System.StringComparison.Ordinal);
            int paramClose = MatchClose(sanitized, sig + signature.Length - 1, '(', ')');
            if (paramClose < 0) return false;
            int brace = sanitized.IndexOf('{', paramClose);
            if (brace < 0) return false;
            for (int k = paramClose + 1; k < brace; k++)
                if (!char.IsWhiteSpace(sanitized[k])) return false;   // 식 본문(=>) 등은 이 절단기가 다루지 않는다 — 조용히 틀린 본문을 돌려주지 않는다.
            int end = MatchClose(sanitized, brace, '{', '}');
            if (end < 0) return false;
            bodyOpen = brace;
            bodyClose = end;
            return true;
        }

        private sealed class ScopeReport
        {
            public int Calls;
            public int ValidCalls;
            public int PanelReads;
            public int ParseFailures;
            public readonly List<string> Violations = new List<string>();
        }

        /// <summary>(나) 범위 판정. 가드 본문은 보지 않는다 — <c>return;</c>만 있는 가드도 조건만으로 잡는다.</summary>
        private static ScopeReport AnalyzeUnsummonedScope(string sanitized)
        {
            var report = new ScopeReport();
            var validSpans = new List<(int Open, int Close)>();
            var allSpans = new List<(int Open, int Close)>();
            Regex panels = MemberRead(PanelsRead);
            Regex grant = MemberRead(GrantRead);

            foreach (Match m in UnsummonedCallRegex.Matches(sanitized))
            {
                int open = m.Index + m.Length - 1;
                int close = MatchClose(sanitized, open, '(', ')');
                if (close < 0) { report.ParseFailures++; continue; }
                report.Calls++;
                allSpans.Add((open, close));
                string args = sanitized.Substring(open + 1, close - open - 1);
                if (panels.IsMatch(args) && grant.IsMatch(args))
                {
                    report.ValidCalls++;
                    validSpans.Add((open, close));
                }
            }

            foreach (Match m in panels.Matches(sanitized))
            {
                report.PanelReads++;
                bool insideCall = allSpans.Any(s => m.Index > s.Open && m.Index < s.Close);
                if (!insideCall)
                    report.Violations.Add($"새 창구 밖에서 옛 창구를 읽음 ({LineOf(sanitized, m.Index)}행: {LineTextAt(sanitized, m.Index)})");
            }

            foreach (Match m in IfHeadRegex.Matches(sanitized))
            {
                int open = m.Index + m.Length - 1;
                int close = MatchClose(sanitized, open, '(', ')');
                if (close < 0) { report.ParseFailures++; continue; }
                bool guardHasValidCall = validSpans.Any(s => s.Open > open && s.Open < close);
                if (guardHasValidCall) continue;
                string condition = sanitized.Substring(open + 1, close - open - 1);
                foreach (string replacement in ReplacementReads)
                {
                    if (MemberRead(replacement).IsMatch(condition))
                        report.Violations.Add($"새 창구 없이 {replacement.Substring(1)}로 가드 ({LineOf(sanitized, m.Index)}행: if ({Regex.Replace(condition, @"\s+", " ").Trim()}))");
                }
            }
            return report;
        }

        private sealed class ClassificationReport
        {
            public readonly List<string> Readers = new List<string>();
            public readonly List<string> Unclassified = new List<string>();
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> Summary = new List<string>();
            public int UnsummonedScopesChecked;
            public int UserScopesChecked;
        }

        private static ClassificationReport Classify(IList<(string Rel, string San)> files, IList<Entry> table)
        {
            var report = new ClassificationReport();
            Regex panels = MemberRead(PanelsRead);

            // ① 스캔 → 표: 옛 창구 독자마다 분류가 있는가
            foreach ((string rel, string san) in files)
            {
                MatchCollection reads = panels.Matches(san);
                if (reads.Count == 0) continue;
                report.Readers.Add(rel);

                List<string> declared = table.Select(e => e.TypeName).Distinct().Where(t => DeclaresType(san, t)).ToList();
                if (declared.Count == 0)
                {
                    report.Unclassified.Add($"{rel} — 분류 표에 없는 형이 옛 창구를 {reads.Count}곳 읽습니다");
                    continue;
                }
                if (declared.Count > 1)
                {
                    report.Problems.Add($"{rel} — 분류 표의 형이 한 파일에 둘 이상 선언돼 파일 단위로 가를 수 없습니다: {string.Join(", ", declared)}");
                    continue;
                }

                string type = declared[0];
                List<Entry> methodEntries = table.Where(e => e.TypeName == type && e.MethodSignature != null).ToList();
                bool wholeType = table.Any(e => e.TypeName == type && e.MethodSignature == null);
                if (wholeType && methodEntries.Count > 0)
                {
                    report.Problems.Add($"{type} — 분류 표에 형 전체 항목과 메서드 항목이 섞여 있습니다");
                    continue;
                }
                if (wholeType) continue;

                var bodies = new List<(int Open, int Close)>();
                foreach (Entry e in methodEntries)
                    if (TryMethodBody(san, e.MethodSignature, out int bo, out int bc)) bodies.Add((bo, bc));
                foreach (Match m in reads)
                {
                    if (!bodies.Any(b => m.Index > b.Open && m.Index < b.Close))
                        report.Unclassified.Add($"{rel}:{LineOf(san, m.Index)} — {type}의 분류 표 메서드 밖에서 옛 창구를 읽습니다: {LineTextAt(san, m.Index)}");
                }
            }

            // ② 표 → 코드: 항목마다 실재하고, 읽고, 분류 규칙을 지키는가
            foreach (Entry e in table)
            {
                List<(string Rel, string San)> typeFiles = files.Where(f => DeclaresType(f.San, e.TypeName)).ToList();
                if (typeFiles.Count == 0)
                {
                    report.Problems.Add($"{e.Label} — 형 선언을 프로덕션 소스에서 찾지 못했습니다(표가 낡았습니다)");
                    continue;
                }

                string scope;
                if (e.MethodSignature == null)
                {
                    scope = string.Join("\n", typeFiles.Select(f => f.San));
                }
                else
                {
                    int total = typeFiles.Sum(f => CountOrdinal(f.San, e.MethodSignature));
                    if (total != 1)
                    {
                        report.Problems.Add($"{e.Label} — 서명이 {total}곳입니다(기대 1) — 메서드 단위로 자를 수 없습니다");
                        continue;
                    }
                    (string Rel, string San) owner = typeFiles.First(f => CountOrdinal(f.San, e.MethodSignature) == 1);
                    if (!TryMethodBody(owner.San, e.MethodSignature, out int bo, out int bc))
                    {
                        report.Problems.Add($"{e.Label} — 본문을 자르지 못했습니다({owner.Rel})");
                        continue;
                    }
                    scope = owner.San.Substring(bo, bc - bo + 1);
                }

                int panelReads = panels.Matches(scope).Count;
                if (panelReads == 0)
                    report.Problems.Add($"{e.Label} — 분류 표에 있는데 옛 창구를 읽지 않습니다({e.Why}). 다른 값으로 바뀌었거나 표가 낡았습니다");

                if (e.Axis == Axis.Unsummoned)
                {
                    report.UnsummonedScopesChecked++;
                    ScopeReport sr = AnalyzeUnsummonedScope(scope);
                    if (sr.ParseFailures > 0)
                        report.Problems.Add($"{e.Label} (나) — 괄호를 자르지 못한 곳 {sr.ParseFailures}건(이 항목 판정 무효)");
                    if (sr.ValidCalls == 0)
                        report.Problems.Add($"{e.Label} (나) — 새 창구 호출(인자에 {PanelsRead.Substring(1)}와 {GrantRead.Substring(1)}가 둘 다)이 없습니다({e.Why})");
                    foreach (string v in sr.Violations) report.Problems.Add($"{e.Label} (나) — {v}");
                    report.Summary.Add($"(나) {e.Label}: 새 창구 호출 {sr.ValidCalls}/{sr.Calls} · 옛 창구 읽기 {sr.PanelReads} · 위반 {sr.Violations.Count}");
                }
                else
                {
                    report.UserScopesChecked++;
                    int calls = UnsummonedCallRegex.Matches(scope).Count;
                    if (calls > 0)
                        report.Problems.Add($"{e.Label} (가) — 사용자가 여는 표면이 새 창구를 {calls}곳 읽습니다. 허가가 참인 순간 스스로 닫혀 등급 1 입구가 사라집니다(E-1·E-2 회귀)");
                    report.Summary.Add($"(가) {e.Label}: 옛 창구 읽기 {panelReads} · 새 창구 호출 {calls}");
                }
            }
            return report;
        }

        private static List<(string Rel, string San)> ProductionSourcesSanitized()
        {
            var list = new List<(string Rel, string San)>();
            string assets = Application.dataPath.Replace('\\', '/');
            string[] roots =
            {
                Path.Combine(Application.dataPath, "_Project", "Scripts"),
                Path.Combine(Application.dataPath, "Editor"),
            };
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string norm = path.Replace('\\', '/');
                    if (norm.Contains("/Tests/")) continue;
                    string rel = norm.StartsWith(assets, System.StringComparison.Ordinal) ? norm.Substring(assets.Length + 1) : norm;
                    list.Add((rel, Sanitize(File.ReadAllText(path))));
                }
            }
            return list;
        }

        /// <summary>프로덕션 소스 <b>원문</b>(지우지 않은 것). 리터럴을 세는 검사는 <see cref="Sanitize"/>를 쓸 수 없다 —
        /// 그 함수가 문자열을 공백으로 지우므로 «리터럴이 몇 번 있는가»가 구조적으로 항상 0이 된다(docs/TEAM.md 같은 가족 보강 규칙 4).</summary>
        private static List<(string Rel, string Raw)> ProductionSourcesRaw()
        {
            var list = new List<(string Rel, string Raw)>();
            string assets = Application.dataPath.Replace('\\', '/');
            string[] roots =
            {
                Path.Combine(Application.dataPath, "_Project", "Scripts"),
                Path.Combine(Application.dataPath, "Editor"),
            };
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string norm = path.Replace('\\', '/');
                    if (norm.Contains("/Tests/")) continue;
                    string rel = norm.StartsWith(assets, System.StringComparison.Ordinal) ? norm.Substring(assets.Length + 1) : norm;
                    list.Add((rel, File.ReadAllText(path)));
                }
            }
            return list;
        }

        /// <summary>
        /// ★ 해제 조건 7-b — <b>사유 문구는 프로덕션에 정확히 한 번만 있다.</b> 설계는 같은 가드 식을 받는 형제 명령이
        /// <b>같은 상수</b>를 재사용하라고 요구한다(<c>docs/narrative/CRACK_COMMAND_DISABLED_REASON.md</c> §7).
        ///
        /// <para>★ 왜 <c>Assert.AreSame</c>으로는 부족한가(이 검사를 그렇게 쓰려다 고쳤다): C#은 <b>같은 어셈블리의 동일한
        /// 리터럴을 인터닝</b>한다. 그래서 누군가 문구를 Director마다 한 벌씩 <b>같은 글자로</b> 복제해도 두 상수는
        /// <c>AreSame</c>이 참이다 — 즉 «공유»와 «복제»를 가를 수 없다. 갈라야 하는 것은 값이 아니라 <b>선언 개수</b>이므로
        /// 소스 원문에서 리터럴을 센다.</para>
        ///
        /// <para>니들은 프로덕션 상수에서 <b>런타임에 조립</b>한다(글자를 베끼지 않는다 — CLAUDE.md). 그리고 이 검사는
        /// «정확히 1»이라는 <b>개수 단언</b>이라 니들이 죽으면 0이 되어 스스로 빨개지지만, 그 빨강이 «복제가 없다»와
        /// 헷갈리지 않게 <b>양성 대조</b>를 붙인다 — 같은 방법·같은 도구로 다른 사유 문구(빈 자리 없음)가 잡히는지 먼저 보인다.</para>
        /// </summary>
        [Test]
        public void 사유_문구는_프로덕션에_정확히_한_번_있고_크랙과_그라피티가_그_하나를_가리킨다()
        {
            Assert.IsNotEmpty(UnsummonedSurfaceCommandReason.Text,
                "7-b 사유 상수가 비었습니다 — 회색 타일이 이유를 말하지 못합니다(36-7 조용한 실패 금지).");
            Assert.AreEqual(UnsummonedSurfaceCommandReason.Text, WindowCrashDirector.UnsummonedSurfacesSuppressedReason,
                "★ 크랙의 사유 상수가 공용 상수와 다른 글자입니다 — 같은 상황에 두 명령이 다른 말을 합니다(사유 문서 §7).");
            Assert.AreEqual(UnsummonedSurfaceCommandReason.Text, UnsummonedSurfaceCommandReason.WhileSuppressed.Reason,
                "미리 만든 결과의 사유가 상수와 다릅니다 — 화면에 뜨는 글자는 이 결과 쪽입니다.");
            Assert.IsFalse(UnsummonedSurfaceCommandReason.WhileSuppressed.IsReady,
                "7-b 사유가 «가능»으로 만들어져 있습니다 — 회색이어야 할 타일이 눌립니다.");

            List<(string Rel, string Raw)> raw = ProductionSourcesRaw();
            Assert.Greater(raw.Count, 100, $"프로덕션 소스를 {raw.Count}개밖에 못 읽었습니다 — 경로가 틀렸습니다(스캔 공허).");

            string needle = "\"" + UnsummonedSurfaceCommandReason.Text + "\"";
            string control = "\"" + GraffitiDirector.NoEmptyRegionReason + "\"";
            int total = 0, controlTotal = 0;
            var owners = new List<string>();
            foreach ((string rel, string text) in raw)
            {
                int n = CountOrdinal(text, needle);
                if (n > 0) owners.Add($"{rel}×{n}");
                total += n;
                controlTotal += CountOrdinal(text, control);
            }

            Assert.Greater(controlTotal, 0,
                "★ 양성 대조 실패 — 같은 방법으로 다른 사유 문구(빈 자리 없음)도 0건입니다. 리터럴 계수 자체가 죽었으므로 " +
                "아래 «정확히 1»은 아무것도 재지 못합니다.");
            Debug.Log($"[7-b] 사유 문구 리터럴 {total}곳: {string.Join(", ", owners)} · 양성 대조(다른 사유) {controlTotal}곳 · 스캔 {raw.Count}파일");
            Assert.AreEqual(1, total,
                $"★ 사유 문구 리터럴이 프로덕션에 {total}곳입니다(기대 1): {string.Join(", ", owners)}.\n" +
                "0이면 상수가 사라졌거나 글자가 바뀐 것이고, 2 이상이면 형제 명령이 같은 글자를 <b>복제</b>한 것입니다 — " +
                "복제는 한쪽만 고치는 날 같은 상황에 다른 말이 뜨게 만듭니다(사유 문서 §7).");
            StringAssert.Contains(nameof(UnsummonedSurfaceCommandReason) + ".cs", owners[0],
                $"★ 사유 문구가 공용 상수 파일이 아닌 곳에 있습니다: {owners[0]}. 글자는 " +
                $"{nameof(UnsummonedSurfaceCommandReason)}에만 두고 나머지는 그것을 참조해야 합니다(사유 문서 §7 2).");
        }

        [Test]
        public void TB_옛_창구_독자는_스캔으로_뽑아_분류_표와_양방향으로_맞추고_자동_표면은_return_가드까지_새_창구만_읽는다()
        {
            List<(string Rel, string San)> files = ProductionSourcesSanitized();
            Assert.Greater(files.Count, 100, $"프로덕션 소스를 {files.Count}개밖에 못 읽었습니다 — 경로가 틀렸습니다(스캔 공허).");

            // ★ 존재 단언 — private 메서드 서명(불가피한 문자열 니들)이 실제로 한 곳에서 잘린다. 이게 없으면 아래 «위반 0»이 «못 잘랐다»와 같아진다.
            foreach (Entry e in Classification.Where(x => x.MethodSignature != null))
            {
                int total = 0;
                bool cut = false;
                foreach ((string rel, string san) in files)
                {
                    if (!DeclaresType(san, e.TypeName)) continue;
                    int c = CountOrdinal(san, e.MethodSignature);
                    total += c;
                    if (c == 1 && TryMethodBody(san, e.MethodSignature, out _, out _)) cut = true;
                }
                Assert.AreEqual(1, total, $"{e.Label} — 서명이 정확히 한 곳이 아닙니다({total}). 이름이 바뀌었으면 분류 표를 고치십시오(아래 판정 무효).");
                Assert.IsTrue(cut, $"{e.Label} — 본문을 자르지 못했습니다(아래 판정 무효).");
            }

            ClassificationReport report = Classify(files, Classification);
            Debug.Log($"[T-B] 옛 창구({PanelsRead}) 독자 {report.Readers.Count}파일(스캔 도출): {string.Join(", ", report.Readers)}\n  " +
                string.Join("\n  ", report.Summary));

            Assert.IsNotEmpty(report.Readers, "옛 창구를 읽는 프로덕션 파일이 0개입니다 — 스캐너가 죽었습니다.");
            Assert.AreEqual(Classification.Count(e => e.Axis == Axis.Unsummoned), report.UnsummonedScopesChecked + CountSkipped(report, Axis.Unsummoned),
                "(나) 항목 일부가 판정에 들어가지 않았습니다.");
            Assert.Greater(report.UnsummonedScopesChecked, 0, "(나) 범위를 하나도 재지 않았습니다.");
            Assert.Greater(report.UserScopesChecked, 0, "(가) 범위를 하나도 재지 않았습니다.");

            Assert.IsEmpty(report.Unclassified,
                "★ 분류 표에 없는 옛 창구 독자:\n  " + string.Join("\n  ", report.Unclassified) +
                "\n사용자가 부르지 않는 자동 표면이면 새 창구(UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces)로 옮기고 (나)로, " +
                "사용자가 여는 표면이면 (가)로 표에 올리십시오(docs/systems/AUTO_SURFACE_LEASE_AXIS.md §2-2).");
            Assert.IsEmpty(report.Problems,
                "★ 자동 표면 임대 축 위반:\n  " + string.Join("\n  ", report.Problems) +
                "\n자동 표면이 옛 창구(또는 등급 2 전용 값)를 읽으면 등급 1에서 사용자가 창을 연 동안 발표 화면 위로 되살아납니다(원칙 2 · N-20).");

            AssertCrackCommandJudgementUsesUnsummonedAxis(files);
            AssertGraffitiCommandJudgementUsesUnsummonedAxis(files);
            AssertGraffitiOverlayLifetimeUsesUnsummonedAxis(files);
        }

        /// <summary>
        /// ★ 해제 조건 A1(설계 §9-1) — <b>메서드 단위 존재 단언</b>. 위 표의 #9는 형 전체 (나)라서, 크랙 오버레이 가드(TickOverlay)만 새 창구를
        /// 읽어도 통과한다. 명령 크랙 판정(<c>GetAvailability</c> — 명령창 타일과 ⌃⌥⌘X의 <c>ForceTriggerNow</c>가 함께 부른다)에서 가드가
        /// 빠지면(변이 M-f) 그 스캔은 눈이 멀기 때문에 이 메서드 본문을 따로 잰다. 표에 메서드 항목으로 넣지 않는 이유: 한 형에 형 전체 항목과
        /// 메서드 항목을 섞으면 분류기가 거부한다(<see cref="Classify"/>).
        /// <para>식별자는 전부 <c>nameof</c>로 조립하고, 서명과 순서 앵커가 본문에서 <b>실재함</b>을 같은 자리에서 먼저 단언한다(CLAUDE.md 니들 규칙).
        /// 순서: 숨김 게이트 &lt; A1 가드 &lt; 락 검사 — 숨김은 A1 사유 문구를 채워도 풀리지 않는 지속 사유라서 먼저 보이고, 락·상태 사유는
        /// A1 사유보다 뒤에 보인다(PlayMode R5d가 런타임으로 같은 것을 잰다).</para>
        /// </summary>
        private static void AssertCrackCommandJudgementUsesUnsummonedAxis(IList<(string Rel, string San)> files)
        {
            string label = nameof(WindowCrashDirector) + "." + nameof(WindowCrashDirector.GetAvailability);
            string signature = "public " + nameof(CommandAvailability) + " " + nameof(WindowCrashDirector.GetAvailability) + "(";

            List<(string Rel, string San)> owners = files.Where(f => DeclaresType(f.San, nameof(WindowCrashDirector))).ToList();
            Assert.AreEqual(1, owners.Count, $"{label} — {nameof(WindowCrashDirector)} 선언 파일이 {owners.Count}개입니다(기대 1).");
            (string rel, string san) = owners[0];
            Assert.AreEqual(1, CountOrdinal(san, signature), $"{label} — 서명 «{signature}»이 정확히 한 곳이 아닙니다({rel}). 아래 판정 무효.");
            Assert.IsTrue(TryMethodBody(san, signature, out int open, out int close), $"{label} — 본문을 자르지 못했습니다({rel}). 아래 판정 무효.");
            string body = san.Substring(open, close - open + 1);

            ScopeReport scope = AnalyzeUnsummonedScope(body);
            Debug.Log($"[T-B] {label}: 새 창구 호출 {scope.ValidCalls}/{scope.Calls} · 옛 창구 읽기 {scope.PanelReads} · 위반 {scope.Violations.Count} · 괄호 실패 {scope.ParseFailures}");
            Assert.AreEqual(0, scope.ParseFailures, $"{label} — 괄호를 자르지 못한 곳이 있습니다(판정 무효).");
            Assert.AreEqual(1, scope.ValidCalls,
                $"★ {label} — 명령 크랙 판정에 새 창구 호출(인자에 {PanelsRead.Substring(1)}와 {GrantRead.Substring(1)}가 둘 다)이 {scope.ValidCalls}곳입니다(기대 1). " +
                "없으면 등급 1에서 사용자가 연 명령창의 [창 부수기]가 «가능»으로 보이고, 누르면 휘두르기만 하고 금은 다음 프레임에 취소됩니다(A1 · 원칙 1).");
            Assert.IsEmpty(scope.Violations, $"★ {label} — " + string.Join(" / ", scope.Violations));

            int call = UnsummonedCallRegex.Match(body).Index;
            string hiddenAnchor = nameof(HiddenCharacterCommandGate) + "." + nameof(HiddenCharacterCommandGate.BlocksNow);
            string lockAnchor = nameof(SpectacleEventLock) + "." + nameof(SpectacleEventLock.IsActive);
            int hidden = body.IndexOf(hiddenAnchor, System.StringComparison.Ordinal);
            int lockCheck = body.IndexOf(lockAnchor, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(hidden, 0, $"{label} — 순서 앵커 «{hiddenAnchor}»가 본문에 없습니다(앵커가 죽었습니다 — 순서 판정 무효).");
            Assert.GreaterOrEqual(lockCheck, 0, $"{label} — 순서 앵커 «{lockAnchor}»가 본문에 없습니다(앵커가 죽었습니다 — 순서 판정 무효).");
            Assert.Less(hidden, call, $"★ {label} — A1 가드가 숨김 게이트보다 앞입니다. 숨김은 A1 사유 문구를 채워도 풀리지 않는 지속 사유라서 먼저 보여야 합니다.");
            Assert.Less(call, lockCheck, $"★ {label} — A1 가드가 락 검사보다 뒤입니다. «지금 ○○ 중이에요»가 먼저 떠 기다리면 될 것처럼 보였다가 아니게 됩니다.");
        }

        /// <summary>
        /// ★ 해제 조건 <b>7-b 층 1</b>(설계 정본 <c>docs/systems/UNSUMMONED_EFFECTS_INVARIANT.md</c> 「4. 판정 ③」) —
        /// <b>그라피티 명령 판정의 메서드 단위 존재 단언과 순서 단언.</b> 위 표의 #10은 형 전체 (나)라서 층 2(낙서 수명)만
        /// 새 창구를 읽어도 통과한다. 그래서 명령 판정(<c>GetAvailability</c> — [낙서하기] 타일과 ⌃⌥⌘G의
        /// <c>ForceTriggerNow</c>가 함께 부른다)의 본문을 따로 잰다. A1(크랙)과 같은 형태다.
        /// <para>순서: 숨김 게이트 &lt; 7-b 가드 &lt; 락 · 상태 · 대상 검사. 숨김은 7-b 사유 문구를 채워도 풀리지 않는
        /// 지속 사유라서 먼저 보이고, 「지금 ○○ 중이에요」·「낙서할 빈 자리가 없어요」는 7-b 사유보다 뒤에 보인다 —
        /// 짧은 사유가 긴 사유를 가리면 «기다리거나 자리를 비우면 되겠다»는 거짓 인상을 준다(PlayMode R6d가 런타임으로 잰다).</para>
        /// <para>앵커는 전부 <c>nameof</c>다 — 대상 검사 앵커도 공개 상수(<see cref="GraffitiDirector.NoEmptyRegionReason"/>)를
        /// 참조하므로 문자열 니들이 하나도 없고, 이름이 바뀌면 조용히 초록이 되는 대신 이 파일이 컴파일되지 않는다.</para>
        /// </summary>
        private static void AssertGraffitiCommandJudgementUsesUnsummonedAxis(IList<(string Rel, string San)> files)
        {
            string label = nameof(GraffitiDirector) + "." + nameof(GraffitiDirector.GetAvailability);
            string signature = "public " + nameof(CommandAvailability) + " " + nameof(GraffitiDirector.GetAvailability) + "(";

            List<(string Rel, string San)> owners = files.Where(f => DeclaresType(f.San, nameof(GraffitiDirector))).ToList();
            Assert.AreEqual(1, owners.Count, $"{label} — {nameof(GraffitiDirector)} 선언 파일이 {owners.Count}개입니다(기대 1).");
            (string rel, string san) = owners[0];
            Assert.AreEqual(1, CountOrdinal(san, signature), $"{label} — 서명 «{signature}»이 정확히 한 곳이 아닙니다({rel}). 아래 판정 무효.");
            Assert.IsTrue(TryMethodBody(san, signature, out int open, out int close), $"{label} — 본문을 자르지 못했습니다({rel}). 아래 판정 무효.");
            string body = san.Substring(open, close - open + 1);

            ScopeReport scope = AnalyzeUnsummonedScope(body);
            Debug.Log($"[T-B] {label}: 새 창구 호출 {scope.ValidCalls}/{scope.Calls} · 옛 창구 읽기 {scope.PanelReads} · 위반 {scope.Violations.Count} · 괄호 실패 {scope.ParseFailures}");
            Assert.AreEqual(0, scope.ParseFailures, $"{label} — 괄호를 자르지 못한 곳이 있습니다(판정 무효).");
            Assert.AreEqual(1, scope.ValidCalls,
                $"★ {label} — 그라피티 명령 판정에 새 창구 호출(인자에 {PanelsRead.Substring(1)}와 {GrantRead.Substring(1)}가 둘 다)이 {scope.ValidCalls}곳입니다(기대 1). " +
                "없으면 등급 1에서 사용자가 연 명령창의 [낙서하기]와 ⌃⌥⌘G가 «가능»으로 보이고, 발표·회의 화면 위에 낙서가 시작됩니다(7-b · 원칙 2).");
            Assert.IsEmpty(scope.Violations, $"★ {label} — " + string.Join(" / ", scope.Violations));

            int call = UnsummonedCallRegex.Match(body).Index;
            string hiddenAnchor = nameof(HiddenCharacterCommandGate) + "." + nameof(HiddenCharacterCommandGate.BlocksNow);
            string lockAnchor = nameof(SpectacleEventLock) + "." + nameof(SpectacleEventLock.IsActive);
            string stateAnchor = nameof(StickmanStateId);
            string targetAnchor = nameof(GraffitiDirector.NoEmptyRegionReason);
            int hidden = body.IndexOf(hiddenAnchor, System.StringComparison.Ordinal);
            int lockCheck = body.IndexOf(lockAnchor, System.StringComparison.Ordinal);
            int stateCheck = body.IndexOf(stateAnchor, System.StringComparison.Ordinal);
            int targetCheck = body.IndexOf(targetAnchor, System.StringComparison.Ordinal);

            // 존재 단언 먼저 — 앵커가 죽으면 아래 순서 비교는 «-1 < N»으로 조용히 통과한다.
            Assert.GreaterOrEqual(hidden, 0, $"{label} — 순서 앵커 «{hiddenAnchor}»가 본문에 없습니다(앵커가 죽었습니다 — 순서 판정 무효).");
            Assert.GreaterOrEqual(lockCheck, 0, $"{label} — 순서 앵커 «{lockAnchor}»가 본문에 없습니다(앵커가 죽었습니다 — 순서 판정 무효).");
            Assert.GreaterOrEqual(stateCheck, 0, $"{label} — 순서 앵커 «{stateAnchor}»가 본문에 없습니다(앵커가 죽었습니다 — 순서 판정 무효).");
            Assert.GreaterOrEqual(targetCheck, 0, $"{label} — 순서 앵커 «{targetAnchor}»가 본문에 없습니다(앵커가 죽었습니다 — 순서 판정 무효).");

            Assert.Less(hidden, call, $"★ {label} — 7-b 가드가 숨김 게이트보다 앞입니다. 숨김은 7-b 사유 문구를 채워도 풀리지 않는 지속 사유라서 먼저 보여야 합니다.");
            Assert.Less(call, lockCheck, $"★ {label} — 7-b 가드가 연출 락 검사보다 뒤입니다. «지금 ○○ 중이에요»가 먼저 떠 기다리면 될 것처럼 보였다가 아니게 됩니다.");
            Assert.Less(call, stateCheck, $"★ {label} — 7-b 가드가 상태 검사보다 뒤입니다. «지금 △△ 중이라 못 해요»가 먼저 떠 상태가 풀리면 될 것처럼 보였다가 아니게 됩니다.");
            Assert.Less(call, targetCheck, $"★ {label} — 7-b 가드가 대상(빈 자리) 검사보다 뒤입니다. «낙서할 빈 자리가 없어요»가 먼저 떠 자리를 비우면 될 것처럼 보였다가 아니게 됩니다.");
        }

        /// <summary>
        /// ★ 해제 조건 <b>7-b 층 2</b>(설계 정본 「3. 판정 ②」의 층 표) — <b>낙서 수명 감시가 억제 창구를 읽고 기존 취소 경로로 걷는가.</b>
        /// 층 1만 있으면 «시작하지 않는다»는 지켜지지만, 등급 0에서 사용자가 시킨 낙서가 그려지는 동안 발표가 시작되면
        /// 그 낙서는 남의 전체화면 앱 위에 남는다.
        /// <para>취소 호출 앵커는 private 메서드라 문자열 니들이 불가피하다 → <b>같은 테스트에서 존재 대조</b>를 붙인다:
        /// 그 이름이 파일 전체에 실재함을 먼저 단언하고(니들 생존), 그다음 그것이 <b>이 메서드 본문 안</b>에 있는지를 단언한다.
        /// 존재 대조 없이 본문 단언만 두면, 이름이 바뀐 날 «본문에 없다»가 «니들이 죽었다»와 구별되지 않는다(CLAUDE.md).</para>
        /// </summary>
        private static void AssertGraffitiOverlayLifetimeUsesUnsummonedAxis(IList<(string Rel, string San)> files)
        {
            const string monitorSignature = "private void MonitorRegion(";
            const string cancelCall = "CancelDrawing(";
            string label = nameof(GraffitiDirector) + " «" + monitorSignature + "»";

            List<(string Rel, string San)> owners = files.Where(f => DeclaresType(f.San, nameof(GraffitiDirector))).ToList();
            Assert.AreEqual(1, owners.Count, $"{label} — {nameof(GraffitiDirector)} 선언 파일이 {owners.Count}개입니다(기대 1).");
            (string rel, string san) = owners[0];

            // 존재 단언(니들 생존) — 서명과 취소 호출이 이 파일에 실재한다.
            Assert.AreEqual(1, CountOrdinal(san, monitorSignature), $"{label} — 서명이 정확히 한 곳이 아닙니다({rel}). 이름이 바뀌었으면 이 감사도 함께 고치십시오(아래 판정 무효).");
            Assert.Greater(CountOrdinal(san, cancelCall), 0,
                $"{label} — 취소 호출 니들 «{cancelCall}»이 파일 어디에도 없습니다({rel}) — 니들이 죽었습니다. 아래 «본문에 있다»는 판정 무효입니다.");
            Assert.IsTrue(TryMethodBody(san, monitorSignature, out int open, out int close), $"{label} — 본문을 자르지 못했습니다({rel}). 아래 판정 무효.");
            string body = san.Substring(open, close - open + 1);

            ScopeReport scope = AnalyzeUnsummonedScope(body);
            Debug.Log($"[T-B] {label}: 새 창구 호출 {scope.ValidCalls}/{scope.Calls} · 옛 창구 읽기 {scope.PanelReads} · 위반 {scope.Violations.Count} · 괄호 실패 {scope.ParseFailures}");
            Assert.AreEqual(0, scope.ParseFailures, $"{label} — 괄호를 자르지 못한 곳이 있습니다(판정 무효).");
            Assert.AreEqual(1, scope.ValidCalls,
                $"★ {label} — 낙서 수명 감시에 새 창구 호출이 {scope.ValidCalls}곳입니다(기대 1). " +
                "없으면 명령으로 시작한 낙서가 발표가 시작된 뒤에도 남의 화면 위에 남습니다(7-b 층 2 · 원칙 2).");
            Assert.IsEmpty(scope.Violations, $"★ {label} — " + string.Join(" / ", scope.Violations));
            Assert.Greater(body.IndexOf(cancelCall, System.StringComparison.Ordinal), 0,
                $"★ {label} — 본문이 기존 취소 경로(«{cancelCall}»)를 부르지 않습니다. 층 2는 새 상태·새 연출을 만들지 않고 " +
                "기존 취소 하나를 재사용해야 합니다(Cancelled 발행 · Idle 전이 · 락 반납 · 쿨다운 미적용이 전부 그 경로에 있습니다).");
        }

        /// <summary>② 단계에서 형을 못 찾거나 서명을 못 잘라 규칙 판정 전에 건너뛴 항목 수(합계 대조용).</summary>
        private static int CountSkipped(ClassificationReport report, Axis axis) =>
            Classification.Where(e => e.Axis == axis)
                .Count(e => report.Problems.Any(p => p.StartsWith(e.Label + " — 형 선언") || p.StartsWith(e.Label + " — 서명이") || p.StartsWith(e.Label + " — 본문을")));

        // ==================================================================
        // T-C — 스캐너 자기 교정
        // ==================================================================

        [Test]
        public void TC_스캐너_자기교정_잡을_것만_잡는다()
        {
            // ---------- (나) 범위 판정 ----------
            ScopeReport oldReturnGuard = AnalyzeUnsummonedScope(Sanitize("void U() { if (_player.ArePanelsSuppressed || _player.IsSuspended) return; }"));
            Assert.AreEqual(1, oldReturnGuard.Violations.Count, "잡아야 할 형태(옛 창구 return; 가드 — F18f 사각지대 · 변이 M-e)를 못 잡습니다.");
            Assert.AreEqual(0, oldReturnGuard.ValidCalls, "새 창구 호출이 없는데 있다고 셉니다.");

            ScopeReport hidesReturnGuard = AnalyzeUnsummonedScope(Sanitize("void T() { if (!_armed) return; if (_agent.HidesScreenSurfaces) return; }"));
            Assert.AreEqual(1, hidesReturnGuard.Violations.Count, "잡아야 할 형태(HidesScreenSurfaces return; 가드)를 못 잡습니다.");

            ScopeReport blockedBlockGuard = AnalyzeUnsummonedScope(Sanitize("void T() { if (_player.IsUserSummonBlocked || _player.IsSuspended) { CancelOverlay(); return; } }"));
            Assert.AreEqual(1, blockedBlockGuard.Violations.Count, "잡아야 할 형태(IsUserSummonBlocked 블록 가드)를 못 잡습니다.");

            ScopeReport danceGuard = AnalyzeUnsummonedScope(Sanitize("void T() { if (_agent.IsForeignFullscreenAppPresent) return; }"));
            Assert.AreEqual(1, danceGuard.Violations.Count, "잡아야 할 형태(춤 축 재사용 가드)를 못 잡습니다.");

            ScopeReport localBypass = AnalyzeUnsummonedScope(Sanitize("void U() { bool p = _agent.ArePanelsSuppressed; if (p) return; }"));
            Assert.AreEqual(1, localBypass.Violations.Count, "잡아야 할 형태(지역 변수로 옛 창구 우회)를 못 잡습니다.");

            ScopeReport good = AnalyzeUnsummonedScope(Sanitize(
                "void U() {\n    if (UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(\n            _player.ArePanelsSuppressed, _player.IsUserSummonGrantActive)\n        || _player.IsSuspended) return;\n}"));
            Assert.AreEqual(0, good.Violations.Count, "올바른 호출형(여러 줄 · || IsSuspended)을 위반으로 오인합니다: " + string.Join(" / ", good.Violations));
            Assert.AreEqual(1, good.ValidCalls, "올바른 호출형을 유효 호출로 세지 못합니다.");
            Assert.AreEqual(1, good.PanelReads, "호출 인자 속 옛 창구 읽기를 세지 못합니다.");

            ScopeReport missingGrant = AnalyzeUnsummonedScope(Sanitize("void U() { if (UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(_agent.ArePanelsSuppressed, false)) return; }"));
            Assert.AreEqual(0, missingGrant.ValidCalls, "허가 인자가 빠진 호출을 유효 호출로 셉니다.");

            ScopeReport redundant = AnalyzeUnsummonedScope(Sanitize(
                "void U() { if (_agent.HidesScreenSurfaces || UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(_agent.ArePanelsSuppressed, _agent.IsUserSummonGrantActive)) return; }"));
            Assert.AreEqual(0, redundant.Violations.Count, "새 창구와 한 조건에 있는 중복 억제(더 좁은 값)를 위반으로 오인합니다.");

            ScopeReport longerName = AnalyzeUnsummonedScope(Sanitize("void U() { if (_agent.ArePanelsSuppressedForTests) return; }"));
            Assert.AreEqual(0, longerName.PanelReads, "긴 이름 속 부분 일치를 옛 창구 읽기로 셉니다.");

            // ---------- 주석 · 문자열 · 보간 구멍 속 따옴표 ----------
            string noisy =
                "// if (_agent.ArePanelsSuppressed) return;\n" +
                "/* if (_agent.HidesScreenSurfaces) return; */\n" +
                "void U() {\n" +
                "  Debug.Log(\"_agent.ArePanelsSuppressed // 문자열\");\n" +
                "  Debug.Log($\"{(a ? \"x\" : \"y\")} _agent.HidesScreenSurfaces {{중괄호}}\");\n" +
                "  Debug.Log(@\"줄 \"\"따옴표\"\" _agent.ArePanelsSuppressed\");\n" +
                "  char q = '\"';\n" +
                "  if (UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(_agent.ArePanelsSuppressed, _agent.IsUserSummonGrantActive)) return;\n" +
                "}";
            ScopeReport noisyReport = AnalyzeUnsummonedScope(Sanitize(noisy));
            Assert.AreEqual(0, noisyReport.Violations.Count, "주석 · 문자열 속 이름을 코드로 읽습니다: " + string.Join(" / ", noisyReport.Violations));
            Assert.AreEqual(1, noisyReport.PanelReads, "주석 · 문자열을 지운 뒤 진짜 읽기 1곳만 남아야 합니다.");
            Assert.AreEqual(1, noisyReport.ValidCalls, "지우기 과정이 진짜 호출까지 지웠습니다.");

            ScopeReport afterNestedQuotes = AnalyzeUnsummonedScope(Sanitize("void U() { Debug.Log($\"{(a ? \"x\" : \"y\")}\"); if (_agent.ArePanelsSuppressed) return; }"));
            Assert.AreEqual(1, afterNestedQuotes.Violations.Count, "보간 구멍 속 따옴표 뒤의 진짜 가드를 놓칩니다(지우기가 코드를 삼켰습니다).");

            // ---------- 분류 판정(양방향) — 합성 파일 묶음 ----------
            var table = new[]
            {
                new Entry("AutoThing", null, Axis.Unsummoned, "합성 (나)"),
                new Entry("UserThing", null, Axis.UserSummoned, "합성 (가)"),
                new Entry("Split", "private void Tick(", Axis.Unsummoned, "합성 메서드 (나)"),
                new Entry("Split", "private void Update(", Axis.UserSummoned, "합성 메서드 (가)"),
            };
            const string autoGood = "sealed class AutoThing { void U() { if (UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(_a.ArePanelsSuppressed, _a.IsUserSummonGrantActive)) return; } }";
            const string userGood = "sealed class UserThing { void U() { if (_a != null && _a.ArePanelsSuppressed) { Close(\"x\"); } } }";
            const string splitGood = "sealed class Split {\n private void Tick() { if (UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(_a.ArePanelsSuppressed, _a.IsUserSummonGrantActive)) return; }\n private void Update() { if (_a.ArePanelsSuppressed) { Close(\"y\"); return; } }\n}";
            const string unrelated = "sealed class Unrelated { void U() { /* _a.ArePanelsSuppressed */ } }";

            List<(string Rel, string San)> Files(params (string Rel, string Src)[] raw) => raw.Select(r => (r.Rel, Sanitize(r.Src))).ToList();

            ClassificationReport clean = Classify(Files(("A.cs", autoGood), ("B.cs", userGood), ("C.cs", splitGood), ("D.cs", unrelated)), table);
            Assert.IsEmpty(clean.Unclassified, "올바른 합성 묶음에서 미분류를 냅니다: " + string.Join(" / ", clean.Unclassified));
            Assert.IsEmpty(clean.Problems, "올바른 합성 묶음에서 위반을 냅니다: " + string.Join(" / ", clean.Problems));
            Assert.AreEqual(3, clean.Readers.Count, "주석 속 이름만 있는 파일을 독자로 셌거나 진짜 독자를 놓쳤습니다: " + string.Join(", ", clean.Readers));

            ClassificationReport newReader = Classify(Files(("A.cs", autoGood), ("B.cs", userGood), ("C.cs", splitGood),
                ("E.cs", "sealed class NewAuto { void U() { if (_a.ArePanelsSuppressed) return; } }")), table);
            Assert.AreEqual(1, newReader.Unclassified.Count(u => u.StartsWith("E.cs")), "분류 표에 없는 새 독자를 못 잡습니다(명부를 손으로 적은 것과 같아집니다).");

            ClassificationReport outsideMethod = Classify(Files(("A.cs", autoGood), ("B.cs", userGood),
                ("C.cs", splitGood.Replace("\n}", "\n private void Other() { if (_a.ArePanelsSuppressed) return; }\n}"))), table);
            Assert.AreEqual(1, outsideMethod.Unclassified.Count, "메서드 단위 형에서 표 밖 메서드의 독자를 못 잡습니다.");

            ClassificationReport revertedOld = Classify(Files(("A.cs", autoGood.Replace(
                "UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(_a.ArePanelsSuppressed, _a.IsUserSummonGrantActive)", "_a.ArePanelsSuppressed")),
                ("B.cs", userGood), ("C.cs", splitGood)), table);
            Assert.IsTrue(revertedOld.Problems.Any(p => p.StartsWith("AutoThing")), "(나)를 옛 창구 return; 가드로 되돌린 것을 못 잡습니다(M-e).");

            ClassificationReport revertedHides = Classify(Files(("A.cs", autoGood.Replace(
                "UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(_a.ArePanelsSuppressed, _a.IsUserSummonGrantActive)", "_a.HidesScreenSurfaces")),
                ("B.cs", userGood), ("C.cs", splitGood)), table);
            Assert.IsTrue(revertedHides.Problems.Any(p => p.StartsWith("AutoThing")), "(나)를 HidesScreenSurfaces return; 가드로 바꾼 것을 못 잡습니다.");

            ClassificationReport methodReverted = Classify(Files(("A.cs", autoGood), ("B.cs", userGood), ("C.cs", splitGood.Replace(
                "private void Tick() { if (UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(_a.ArePanelsSuppressed, _a.IsUserSummonGrantActive)) return; }",
                "private void Tick() { if (_a.ArePanelsSuppressed) return; }"))), table);
            Assert.IsTrue(methodReverted.Problems.Any(p => p.StartsWith("Split «private void Tick(»")), "메서드 단위 (나)를 옛 창구로 되돌린 것을 못 잡습니다(M-d · M-e 형태).");
            Assert.IsFalse(methodReverted.Problems.Any(p => p.StartsWith("Split «private void Update(»")), "같은 형의 (가) 메서드까지 위반으로 오인합니다.");

            ClassificationReport userMoved = Classify(Files(("A.cs", autoGood), ("B.cs", userGood.Replace(
                "_a.ArePanelsSuppressed", "UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(_a.ArePanelsSuppressed, _a.IsUserSummonGrantActive)")),
                ("C.cs", splitGood)), table);
            Assert.IsTrue(userMoved.Problems.Any(p => p.StartsWith("UserThing")), "(가) 사용자 표면을 새 창구로 옮긴 것을 못 잡습니다(E-1·E-2 회귀 형태).");

            var staleTable = table.Concat(new[] { new Entry("Ghost", null, Axis.Unsummoned, "사라진 형") }).ToArray();
            ClassificationReport stale = Classify(Files(("A.cs", autoGood), ("B.cs", userGood), ("C.cs", splitGood)), staleTable);
            Assert.IsTrue(stale.Problems.Any(p => p.StartsWith("Ghost")), "표에 있는데 코드에 없는 항목을 못 잡습니다.");
        }
    }
}
