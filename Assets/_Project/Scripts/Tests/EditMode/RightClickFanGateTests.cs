using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>캐릭터 우클릭 → 부채꼴</b>의 순수 판정과 기하를 씬 없이 잠근다 — 2026-09-05.
    /// 설계 정본: <c>docs/UX_RIGHTCLICK_FAN_MENU.md</c> · 테스트 설계:
    /// <c>docs/TEST_PLAN_FAN_AND_EQUIPMENT.md</c> F1 · F5c · F6′ · F7′ · F18 · F24(순수 절반).
    ///
    /// ============================================================================
    /// 이 파일이 EditMode인 이유
    /// ============================================================================
    /// 재는 대상이 전부 <b>OS 호출 0줄 순수 함수</b>다(테스트 설계 C-1). PlayMode로 옮기면 같은
    /// 판정을 씬·프레임·플랫폼 서비스를 거쳐 <b>간접적으로</b> 재게 되고, 그때는 «게이트가 틀렸다»와
    /// «씬이 안 떴다»가 구분되지 않는다.
    ///
    /// ============================================================================
    /// 숫자를 베끼지 않는다
    /// ============================================================================
    /// 각도·바이어스·예산은 전부 프로덕션 상수를 <b>참조</b>한다. 이 저장소가 2026-09-01에
    /// 하드코딩 잔존으로 4건을 잃은 형태(기준과 대상이 갈라져도 아무도 모른다)를 피하기 위해서다.
    /// 단 하나 예외가 <see cref="ShippedGearCenterPoints"/>인데, 그것은 <b>상수의 사본이 아니라
    /// 「출하 기본 화면이 이 자리였다」는 역사적 사실의 고정 표식</b>이고, 그래서 그 값이 바뀌면
    /// 이 테스트가 «회귀»를 말하는 것이 맞다(UX_FLOW 36-3-4).
    /// </summary>
    public sealed class RightClickFanGateTests
    {
        /// <summary>출하 기본 톱니 중심(pt) — UX_FLOW 36-3-4가 못박은 θ₀ = 225°의 그 자리.</summary>
        private static readonly Vector2 ShippedGearCenterPoints = new Vector2(1482f, 924f);

        /// <summary>실측 화면(pt). §1-4 스윕이 쓴 것과 같은 크기.</summary>
        private static readonly Vector2 ScreenPoints = new Vector2(1512f, 982f);

        // ==================================================================
        // F1 — 수용 조건 진리표 2⁶ = 64행 전수 (2026-09-14 E-4: 여섯째 항 «화면에 있는가»)
        // ==================================================================

        /// <summary>여섯 항의 64행을 <b>루프로</b> 만든다 — 손으로 적으면 그 표가 곧 두 번째 구현이 된다.</summary>
        private static bool Bit(int row, int index) => ((row >> index) & 1) != 0;

        [Test]
        public void F1_커서가_캐릭터_밖이면_64행_중_32행이_전부_거짓이다()
        {
            int falseRows = 0;
            int trueRowsOverall = 0;

            for (int row = 0; row < 64; row++)
            {
                bool cursorOver = Bit(row, 0);
                bool rising = Bit(row, 1);
                bool swallowAllows = Bit(row, 2);
                bool blocked = Bit(row, 3);
                bool primaryHeld = Bit(row, 4);
                bool onScreen = Bit(row, 5);

                bool open = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    cursorOver, rising, swallowAllows, blocked, primaryHeld, onScreen);

                if (open) trueRowsOverall++;
                if (cursorOver) continue;

                falseRows++;
                Assert.IsFalse(open,
                    $"커서가 캐릭터 밖인 행({row:00})이 부채꼴을 엽니다 — 캐릭터 밖 우클릭은 " +
                    "절대 잡히면 안 됩니다(비침해 원칙 2: 남의 앱 위에서 남의 문맥 메뉴를 가로챈다).");
            }

            Assert.AreEqual(32, falseRows, "커서 항이 거짓인 행은 정확히 32개여야 합니다(2⁵).");

            // ★ 양성 대조 — 「전부 거짓」이 «조건이 죽어서»가 아님을 같은 실행 안에서 증명한다.
            //   이 단언이 없으면 ShouldOpenFan이 항상 false를 돌려줘도 위 루프가 초록이다.
            Assert.AreEqual(1, trueRowsOverall,
                "64행 중 참인 행이 정확히 1개여야 합니다(여섯 항이 전부 원하는 값일 때 딱 한 조합). " +
                "0개면 이 테스트가 아무것도 재지 않은 것입니다.");
        }

        [Test]
        public void F1_음성대조_커서항을_상수참으로_바꾸면_최소_한_행이_뒤집힌다()
        {
            int flipped = 0;
            for (int row = 0; row < 64; row++)
            {
                bool real = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    Bit(row, 0), Bit(row, 1), Bit(row, 2), Bit(row, 3), Bit(row, 4), Bit(row, 5));

                // 커서 항만 상수 true로 치환한 식(= 그 항을 없앤 세상).
                bool withoutCursorTerm = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    true, Bit(row, 1), Bit(row, 2), Bit(row, 3), Bit(row, 4), Bit(row, 5));

                if (real != withoutCursorTerm) flipped++;
            }

            Assert.Greater(flipped, 0,
                "커서 항을 없애도 결과가 한 행도 안 바뀝니다 — 그 항이 실제로는 아무 일도 하지 않는다는 뜻입니다.");
        }

        // ==================================================================
        // ★★ 2026-09-14 — E-1: 넷째 항은 「허가를 받아도 억제되는가」
        //    (docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md §16-2 · §16-2b · §16-4 T-1)
        // ==================================================================
        //
        // 옛 F18(F18_전체화면_억제_중에는_어떤_조합도_열지_않는다)은 넷째 항 입력을 「지금 억제 중인가」로 두고
        // 그 16행이 전부 닫히는 것을 «원칙 2 정면 위반» 메시지로 정답화했다. 그런데 그 입력은 허가 없는 등급 1에서
        // 참이고 허가는 게이트 뒤에서 나므로, 그 테스트는 «등급 1 마우스 입구 0»이라는 P1 결함을 초록으로 잠그고 있었다.
        // 이제 넷째 항 입력은 UserSurfaceSummonPolicy.BlocksUserSummon에서 파생하고, 기대값은 아래 상수 표다
        // (정책 함수로 기대값을 만들지 않는다 — 생성기와 검사기가 같이 틀리는 형태를 피한다).

        /// <summary>(s, r, g) 8행의 인덱스 — s가 최상위 비트. s = HidesScreenSurfaces · r = 등급 1 이상(축 3) · g = 사용자 소환 임대.</summary>
        private static int SummonRow(bool s, bool r, bool g) => (s ? 4 : 0) | (r ? 2 : 0) | (g ? 1 : 0);

        /// <summary>★ 기대값 상수 표 — <c>BlocksUserSummon</c>. 인덱스 0..7 = (s,r,g) FFF FFT FTF FTT TFF TFT TTF TTT.
        /// 등급 2(전체화면 게임)·다른 가상 데스크톱(s = 참)에서만 막는다.</summary>
        private static readonly bool[] ExpectedBlocksUserSummon = { false, false, false, false, true, true, true, true };

        /// <summary>★ 대조용 상수 표 — <c>SuppressesPanels</c>(닫기 판정). 무허가 등급 1(FTF)에서 <b>참</b>인 것이 옛 게이트의
        /// 순환이었다. 이 표는 닫기 소비자의 계약이라 E-1이 한 칸도 바꾸지 않는다(§16-2b B2).</summary>
        private static readonly bool[] ExpectedSuppressesPanels = { false, false, true, false, true, true, true, true };

        [Test]
        public void 정책_BlocksUserSummon_진리표_8행이_상수_표와_같다()
        {
            int blockedRows = 0;
            int differsFromClosingRule = 0;

            for (int row = 0; row < 8; row++)
            {
                bool s = Bit(row, 2), r = Bit(row, 1), g = Bit(row, 0);
                Assert.AreEqual(row, SummonRow(s, r, g), "행 인덱스 조립이 틀렸습니다 — 아래 표 대조가 엉뚱한 칸을 봅니다.");

                bool blocks = UserSurfaceSummonPolicy.BlocksUserSummon(s, r, g);
                Assert.AreEqual(ExpectedBlocksUserSummon[row], blocks,
                    $"BlocksUserSummon(s={s}, r={r}, g={g})가 상수 표와 다릅니다 — " +
                    (ExpectedBlocksUserSummon[row]
                        ? "★ 등급 2(전체화면 게임)·다른 가상 데스크톱에서 사용자 소환이 뚫립니다(원칙 2 정면 위반)."
                        : "★ 허가를 받으면 풀리는 상태를 막습니다 — 등급 1 마우스 입구가 다시 0이 됩니다(P1 회귀)."));

                bool closes = UserSurfaceSummonPolicy.SuppressesPanels(s, r, g);
                Assert.AreEqual(ExpectedSuppressesPanels[row], closes,
                    $"SuppressesPanels(s={s}, r={r}, g={g})가 상수 표와 다릅니다 — 닫기 판정이 바뀌면 등급 1 진입 회수(B2)나 " +
                    "등급 2 포함관계가 흔들립니다.");

                if (blocks) blockedRows++;
                if (blocks != closes) differsFromClosingRule++;
            }

            Assert.AreEqual(4, blockedRows, "막는 행이 정확히 4개(s = 참 전부)여야 합니다.");
            // ★ 음성 대조 — 열기 판정과 닫기 판정은 정확히 한 칸(무허가 등급 1)에서만 갈린다.
            //   0이면 열기 판정이 옛 게이트 입력과 같아진 것이고(E-1 되돌림), 2 이상이면 닫기 판정까지 흔들린 것이다.
            Assert.AreEqual(1, differsFromClosingRule,
                "열기 판정과 닫기 판정이 갈리는 칸이 정확히 1개(s=거짓, r=참, g=거짓)여야 합니다 — " +
                "0이면 게이트가 다시 「지금 억제 중인가」를 묻고 있습니다(P1 회귀).");
        }

        [Test]
        public void F18_사용자_소환이_막힌_상태에서는_어떤_조합도_열지_않는다()
        {
            int checkedRows = 0;
            int openWhenUnblocked = 0;

            for (int row = 0; row < 16; row++)
            {
                bool cursorOver = Bit(row, 0);
                bool rising = Bit(row, 1);
                bool swallowAllows = Bit(row, 2);
                bool primaryHeld = Bit(row, 3);

                checkedRows++;
                Assert.IsFalse(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                        cursorOver, rising, swallowAllows, userSummonBlocked: true, primaryButtonHeld: primaryHeld, characterOnScreen: true),
                    $"사용자 소환이 막힌 상태(등급 2 전체화면 게임 · 다른 가상 데스크톱)인데 열립니다(행 {row:00}) — " +
                    "원칙 2 정면 위반입니다.");

                if (AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                        cursorOver, rising, swallowAllows, userSummonBlocked: false, primaryButtonHeld: primaryHeld, characterOnScreen: true))
                    openWhenUnblocked++;
            }

            Assert.AreEqual(16, checkedRows);
            // ★ 양성 대조 — 막힘을 풀면 정확히 한 조합(나머지 네 항이 전부 원하는 값)이 열린다.
            Assert.AreEqual(1, openWhenUnblocked,
                "막힘을 풀었을 때 열리는 조합이 정확히 1개여야 합니다 — 0이면 측정기가 죽었습니다.");
        }

        [Test]
        public void F18b_등급1_체류_무허가에서도_우클릭은_연다()
        {
            // ★ 정책과 게이트를 <b>합성해서</b> 재는 EditMode 테스트다(§16-4 T-1 c).
            //   입력은 정책 함수에서 파생하고, 기대값은 «연다»라는 상수다.
            bool blocked = UserSurfaceSummonPolicy.BlocksUserSummon(
                characterSuspended: false, panelRetreatActive: true, userSummonGranted: false);
            Assert.IsTrue(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    cursorOverCharacter: true, secondaryRisingEdge: true, swallowAllowsOpen: true,
                    userSummonBlocked: blocked, primaryButtonHeld: false, characterOnScreen: true),
                "★ 게임이 아닌 전체화면 앱(등급 1) 체류 중, 허가가 아직 없는 상태에서 캐릭터 우클릭이 닫혔습니다 — " +
                "허가는 게이트를 통과한 뒤에야 나므로 이 조합이 닫히면 등급 1 마우스 입구가 0입니다(P1, §15-1).");

            // ★ 음성 대조 — 옛 게이트 입력(닫기 판정)을 넣으면 같은 조합이 실제로 닫힌다. 이 단언이 없으면
            //   위 «연다»가 «게이트가 넷째 항을 아예 안 본다»와 구별되지 않는다.
            bool oldInput = UserSurfaceSummonPolicy.SuppressesPanels(false, true, false);
            Assert.IsFalse(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(true, true, true, oldInput, false, true),
                "옛 입력(SuppressesPanels)으로도 열립니다 — 게이트 넷째 항이 아무 일도 하지 않거나 이 대조가 결함을 재현하지 못합니다.");
        }

        [Test]
        public void F18c_원칙2_경계_s가_참이면_r과_g가_무엇이든_합성_게이트가_닫힌다()
        {
            int closedRows = 0;
            int openRows = 0;

            for (int row = 0; row < 8; row++)
            {
                bool s = Bit(row, 2), r = Bit(row, 1), g = Bit(row, 0);
                // ★ 여섯째 항은 참으로 둔다 — 이 표는 넷째 항 하나가 원칙 2를 따로 잠그는지를 잰다(여섯째 항이 대신 닫으면 안 보인다).
                bool open = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    true, true, true, UserSurfaceSummonPolicy.BlocksUserSummon(s, r, g), false, true);

                if (s)
                {
                    Assert.IsFalse(open,
                        $"★ s=참(등급 2 전체화면 게임 · 다른 가상 데스크톱)인데 r={r}, g={g}에서 캐릭터 우클릭이 열립니다 — " +
                        "원칙 2 정면 위반입니다(§16-2b B7 · B8).");
                    closedRows++;
                }
                else
                {
                    Assert.IsTrue(open,
                        $"s=거짓인데 r={r}, g={g}에서 캐릭터 우클릭이 닫힙니다 — §16-2b B1 · B3의 입구가 사라집니다.");
                    openRows++;
                }
            }

            Assert.AreEqual(4, closedRows, "s=참인 행이 4개여야 합니다.");
            Assert.AreEqual(4, openRows, "s=거짓인 행이 4개여야 합니다.");
        }

        [Test]
        public void F18d_게이트_호출부는_열기_판정을_넘기고_닫기_판정을_넘기지_않는다()
        {
            // ★ 변이 M1(호출부만 옛 값으로 되돌림)은 위 순수 판정 테스트로는 안 보인다 — 호출부를 소스로 잰다.
            // ★ 2026-09-14 (E-4) — 항별 값은 게이트 표본 생성자 한 곳에서 채워지고 게이트는 그 표본만 읽는다
            //   (F26c가 «게이트가 표본만 읽는다»를 잰다). 그래서 넷째 항의 입력을 잴 자리는 표본 생성자다.
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));
            string call = CallArgumentsOrFail(director,
                "var gate = new " + nameof(AppControlDirector.RightClickFanGateSample) + "(");

            // 존재 대조 — 같은 인자 목록에서 실재하는 이름을 먼저 찾는다(절단기 생존).
            StringAssert.Contains(nameof(AppControlDirector.RightClickFanGatePolicy.SwallowAllowsOpen), call,
                "게이트 호출 인자에서 삼킴 판정조차 못 찾았습니다 — 호출 절단이 틀렸습니다(아래 «없음» 판정 무효).");

            string openNeedle = "." + nameof(StickmanAgent.IsUserSummonBlocked);
            StringAssert.Contains(openNeedle, call,
                $"★ 게이트 호출부가 열기 판정({openNeedle})을 넘기지 않습니다:\n  {call.Trim()}");
            Assert.AreEqual(-1, call.IndexOf("." + nameof(StickmanAgent.ArePanelsSuppressed), System.StringComparison.Ordinal),
                "★ 게이트 호출부가 다시 닫기 판정(ArePanelsSuppressed)을 넘깁니다 — 허가 없는 등급 1에서 참이라 " +
                "허가 발급에 닿지 못합니다(P1 회귀, 변이 M1).");
            Assert.AreEqual(-1, call.IndexOf("." + nameof(StickmanAgent.HidesScreenSurfaces), System.StringComparison.Ordinal),
                "게이트 호출부가 결과값(HidesScreenSurfaces)을 직접 넘깁니다 — 지금 값은 같아도 두 정책 중 하나가 바뀌는 날 " +
                "게이트만 옛 규칙에 남습니다(§16-2 「파생식으로 두는 이유」).");
        }

        // ------------------------------------------------------------------
        // ★★ 2026-09-14 (E-4 · verify-change 생존 변이 V1·V2) — 닫기 소비자 명부를 하드코딩하지 않는다.
        //   옛 F18e는 파일 다섯 개를 손으로 적었고, 실제 닫기 소비자는 일곱이었다(할일 리마인더·창 크랙 누락).
        //   게다가 소비자가 열기 판정을 «다른 식으로» 쓰면(같은 값의 HidesScreenSurfaces) 글자 니들로는 안 보였다.
        //   이제 프로덕션 소스 전수를 스캔해 (가) 열기 판정을 읽는 파일 = 게이트 호출부 파일, (나) 표면을 닫는 가드가
        //   등급 2에서만 참인 값을 읽지 않음을 잰다. 명부는 스캔 결과를 로그로 남길 뿐 적어 두지 않는다.
        // ------------------------------------------------------------------

        private static readonly System.Text.RegularExpressions.Regex IfHeadRegex =
            new System.Text.RegularExpressions.Regex(@"(?<![A-Za-z0-9_])if\s*\(");

        /// <summary>표면·연출을 걷는 호출들. 톱니의 <c>ApplySuspendHide</c>처럼 «의도적으로 등급 2에만 걷는» 전용 경로는 여기 없다 —
        /// 앞 글자 경계(ASCII)로 긴 이름 속 부분 일치를 막는다(한글을 낱말 문자로 치는 <c>\b</c>는 쓰지 않는다).</summary>
        private static readonly System.Text.RegularExpressions.Regex CloserCallRegex =
            new System.Text.RegularExpressions.Regex(@"(?<![A-Za-z0-9_])(Close|Hide|CancelOverlay|ForceCloseAll|EnterFullscreenHiding)\s*\(");

        /// <summary>테스트 밖 프로덕션 소스 전수(주석 제거). 파일 명부를 쓰지 않는다 — 파일을 쪼개거나 새 소비자가 생겨도 스캔이 따라간다.</summary>
        private static System.Collections.Generic.List<(string Rel, string Src)> ProductionSourcesStripped()
        {
            var list = new System.Collections.Generic.List<(string Rel, string Src)>();
            string root = Root.Replace('\\', '/');
            foreach (string path in Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (!norm.EndsWith(".cs", System.StringComparison.Ordinal)) continue;
                if (norm.Contains("/Tests/")) continue;
                list.Add((norm.Substring(root.Length + 1), StripComments(File.ReadAllText(path).Replace("\r\n", "\n"))));
            }
            return list;
        }

        /// <summary><see cref="MatchingCloseOrFail"/>와 같은 규칙이지만 실패를 단언하지 않고 -1을 돌려준다(전수 스캔용 — 실패는 호출부가 센다).</summary>
        private static int TryMatchingClose(string src, int openIndex, char open, char close)
        {
            int depth = 0;
            for (int i = openIndex; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '"')
                {
                    bool verbatim = i > 0 && src[i - 1] == '@';
                    i++;
                    while (i < src.Length)
                    {
                        if (!verbatim && src[i] == '\\') { i += 2; continue; }
                        if (src[i] == '"')
                        {
                            if (verbatim && i + 1 < src.Length && src[i + 1] == '"') { i += 2; continue; }
                            break;
                        }
                        i++;
                    }
                    continue;
                }
                if (c == '\'')
                {
                    i++;
                    while (i < src.Length && src[i] != '\'')
                    {
                        if (src[i] == '\\') i++;
                        i++;
                    }
                    continue;
                }
                if (c == open) depth++;
                else if (c == close && --depth == 0) return i;
            }
            return -1;
        }

        /// <summary>조건에 <paramref name="token"/>이 들어 있고 본문(블록 또는 한 문장)이 표면을 걷는 호출을 하는 <c>if</c> 가드들의 조건 문자열.</summary>
        private static System.Collections.Generic.List<string> CloserGuards(string src, string token, ref int parseFailures)
        {
            var hits = new System.Collections.Generic.List<string>();
            foreach (System.Text.RegularExpressions.Match m in IfHeadRegex.Matches(src))
            {
                int open = m.Index + m.Length - 1;
                int close = TryMatchingClose(src, open, '(', ')');
                if (close < 0) { parseFailures++; continue; }
                string condition = src.Substring(open + 1, close - open - 1);
                if (condition.IndexOf(token, System.StringComparison.Ordinal) < 0) continue;

                int j = close + 1;
                while (j < src.Length && char.IsWhiteSpace(src[j])) j++;
                string statement;
                if (j < src.Length && src[j] == '{')
                {
                    int end = TryMatchingClose(src, j, '{', '}');
                    if (end < 0) { parseFailures++; continue; }
                    statement = src.Substring(j, end - j + 1);
                }
                else
                {
                    int end = src.IndexOf(';', j);
                    if (end < 0) { parseFailures++; continue; }
                    statement = src.Substring(j, end - j + 1);
                }
                if (CloserCallRegex.IsMatch(statement)) hits.Add(condition.Trim());
            }
            return hits;
        }

        [Test]
        public void F18e_닫기_소비자는_열기_판정을_읽지_않는다()
        {
            // ★ 변이 M5 · V1 — 닫기 소비자(표면 회수 · 자동 연출 억제)가 열기 판정을 읽으면 무허가 등급 1에서 회수·억제가 풀린다.
            //   «닫기 소비자 명부»를 적지 않는다 — 열기 판정을 읽어도 되는 곳은 게이트 호출부뿐이므로 그 밖의 독자 전부가 위반이다.
            var sources = ProductionSourcesStripped();
            Assert.Greater(sources.Count, 100, $"프로덕션 소스를 {sources.Count}개밖에 못 읽었습니다 — 경로가 틀렸습니다(스캔 공허).");

            string gateHead = nameof(AppControlDirector.RightClickFanGatePolicy) + "." +
                nameof(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan) + "(";
            string openNeedle = "." + nameof(StickmanAgent.IsUserSummonBlocked);
            string closeNeedle = "." + nameof(StickmanAgent.ArePanelsSuppressed);

            var gateFiles = new System.Collections.Generic.List<string>();
            var openReaders = new System.Collections.Generic.List<string>();
            var closeReaders = new System.Collections.Generic.List<string>();
            foreach ((string rel, string src) in sources)
            {
                if (src.IndexOf(gateHead, System.StringComparison.Ordinal) >= 0) gateFiles.Add(rel);
                if (src.IndexOf(openNeedle, System.StringComparison.Ordinal) >= 0) openReaders.Add(rel);
                if (src.IndexOf(closeNeedle, System.StringComparison.Ordinal) >= 0) closeReaders.Add(rel);
            }

            // 존재 대조 — 게이트 호출부를 찾았고, 그 파일은 열기 판정을 실제로 읽는다(니들 생존).
            Assert.IsNotEmpty(gateFiles, $"게이트 호출({gateHead})을 한 파일에서도 못 찾았습니다 — 스캐너가 죽었습니다.");
            foreach (string gate in gateFiles)
                CollectionAssert.Contains(openReaders, gate, $"게이트 호출부({gate})가 열기 판정을 읽지 않습니다 — 아래 «그 밖의 독자 0»이 무의미합니다.");
            Assert.IsNotEmpty(closeReaders, "닫기 판정을 읽는 프로덕션 파일이 0개입니다 — 스캐너가 죽었습니다.");

            var strays = new System.Collections.Generic.List<string>();
            foreach (string reader in openReaders)
                if (!gateFiles.Contains(reader)) strays.Add(reader);

            Debug.Log($"[F18e] 닫기 판정 독자 {closeReaders.Count}곳(스캔 도출): {string.Join(", ", closeReaders)} / " +
                $"열기 판정 독자: {string.Join(", ", openReaders)} / 게이트 호출부: {string.Join(", ", gateFiles)}");
            Assert.IsEmpty(strays,
                $"★ 열기 판정({openNeedle})을 게이트 호출부 밖에서 읽는 프로덕션 파일: {string.Join(", ", strays)} — " +
                "닫기 소비자가 열기 판정을 읽으면 무허가 등급 1에서 표면 회수·자동 연출 억제가 풀립니다(원칙 2, §16-2b B2 · 변이 M5·V1).");
        }

        [Test]
        public void F18f_닫기_가드는_열기_판정과_같은_값을_다른_식으로_쓰지_않는다()
        {
            // ★ 변이 V2 — 설정창 닫기 가드를 HidesScreenSurfaces로 바꾸면 값은 열기 판정과 같다(등급 2에서만 참). 글자 니들로는 안 보인다.
            var sources = ProductionSourcesStripped();
            string surfaceNeedle = "." + nameof(StickmanAgent.HidesScreenSurfaces);
            string openNeedle = "." + nameof(StickmanAgent.IsUserSummonBlocked);
            string closeNeedle = "." + nameof(StickmanAgent.ArePanelsSuppressed);

            int parseFailures = 0;
            int closeGuards = 0;
            var offenders = new System.Collections.Generic.List<string>();
            foreach ((string rel, string src) in sources)
            {
                foreach (string c in CloserGuards(src, surfaceNeedle, ref parseFailures)) offenders.Add($"{rel}: if ({c})");
                foreach (string c in CloserGuards(src, openNeedle, ref parseFailures)) offenders.Add($"{rel}: if ({c})");
                closeGuards += CloserGuards(src, closeNeedle, ref parseFailures).Count;
            }

            Assert.AreEqual(0, parseFailures, $"if 가드를 자르지 못한 곳이 {parseFailures}건 — 이 스캔 결과는 무효입니다.");
            Assert.Greater(closeGuards, 0, "존재 대조 — 닫기 판정으로 표면을 걷는 가드를 한 곳도 못 찾았습니다(스캐너가 죽었습니다).");
            Debug.Log($"[F18f] 닫기 판정으로 표면을 걷는 가드 {closeGuards}곳 · 다른 식으로 걷는 가드 {offenders.Count}곳.");
            Assert.IsEmpty(offenders,
                $"★ 표면을 걷는 가드가 닫기 판정({closeNeedle})이 아니라 등급 2에서만 참인 값을 읽습니다:\n  {string.Join("\n  ", offenders)}\n" +
                "그 값은 열기 판정과 같아서 등급 1 진입 순간 이미 떠 있던 표면·차단막이 회수되지 않습니다(원칙 2 · 변이 V2). " +
                "의도적으로 등급 2에만 걷는 표면은 닫기 호출이 아니라 전용 경로를 씁니다(톱니의 ApplySuspendHide).");
        }

        [Test]
        public void F18g_닫기_가드_스캐너_자기검증_잡을것만_잡는다()
        {
            // 알려진 표본에 먼저 교정한다 — 이게 없으면 F18f의 «0곳»이 «스캐너가 눈이 멀었다»와 구별되지 않는다.
            string surface = "." + nameof(StickmanAgent.HidesScreenSurfaces);
            int failures = 0;

            string v2Shape = "void U() { if (_agent != null && _agent.HidesScreenSurfaces)\n{\n _restore = false;\n Close(\"a\");\n return;\n} }";
            Assert.AreEqual(1, CloserGuards(v2Shape, surface, ref failures).Count, "잡아야 할 형태(블록 안 Close — V2)를 못 잡습니다.");

            string singleStatement = "void L() { if (Agent.HidesScreenSurfaces) Hide(); }";
            Assert.AreEqual(1, CloserGuards(singleStatement, surface, ref failures).Count, "한 문장 본문의 Hide()를 못 잡습니다.");

            string withInterpolation = "void L() { if (_agent.HidesScreenSurfaces) { Debug.Log($\"{(a ? 1 : 2)} 닫힘\"); CancelOverlay(); } }";
            Assert.AreEqual(1, CloserGuards(withInterpolation, surface, ref failures).Count, "보간 문자열 뒤의 닫기 호출을 못 잡습니다.");

            string gearDedicatedPath = "void L() { if (_agent.HidesScreenSurfaces)\n{\n ApplySuspendHide(\"x\");\n return;\n} }";
            Assert.AreEqual(0, CloserGuards(gearDedicatedPath, surface, ref failures).Count, "전용 경로(ApplySuspendHide)를 닫기 호출로 오인합니다.");

            string armOnly = "void U() { if (_agent.HidesScreenSurfaces) ArmReopenAfterSuspend(); }";
            Assert.AreEqual(0, CloserGuards(armOnly, surface, ref failures).Count, "닫기가 아닌 호출(ArmReopenAfterSuspend)을 닫기로 오인합니다.");

            string otherToken = "void U() { if (_agent.ArePanelsSuppressed) { Close(\"b\"); } }";
            Assert.AreEqual(0, CloserGuards(otherToken, surface, ref failures).Count, "조건에 없는 토큰을 잡습니다.");

            Assert.AreEqual(0, failures, $"교정 표본에서 파서 실패 {failures}건.");
        }

        [Test]
        public void F24_좌버튼을_잡고_있는_동안에는_열지_않는다()
        {
            for (int row = 0; row < 8; row++)
            {
                Assert.IsFalse(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                        Bit(row, 0), Bit(row, 1), Bit(row, 2), userSummonBlocked: false, primaryButtonHeld: true, characterOnScreen: true),
                    "좌버튼으로 잡고 있는 중에 부채꼴이 열립니다 — 던지려던 동작을 메뉴가 가로챕니다.");
            }

            // ★ 양성 대조 — 손을 떼면 같은 조합이 열린다.
            Assert.IsTrue(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                cursorOverCharacter: true, secondaryRisingEdge: true, swallowAllowsOpen: true,
                userSummonBlocked: false, primaryButtonHeld: false, characterOnScreen: true));
        }

        // ==================================================================
        // ★★ 2026-09-14 — E-4: 숨긴 캐릭터 입력(우클릭 여섯째 항 · 좌클릭 공통 입구)
        //    (docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md §16-2c, debugger 좌클릭 판독)
        // ==================================================================

        /// <summary>★ 기대값 상수 표 — <c>CharacterOnScreenForInput</c>. 인덱스 = (캐릭터 축 숨김 &lt;&lt; 1) | 가출 은신 : FF FT TF TT.
        /// 둘 다 거짓일 때만 «화면에 있다».</summary>
        private static readonly bool[] ExpectedCharacterOnScreen = { true, false, false, false };

        [Test]
        public void F26_캐릭터가_화면에_없으면_64행_중_어떤_조합도_열지_않는다()
        {
            int closedWhenOffScreen = 0;
            int openWhenOnScreen = 0;
            int flipped = 0;

            for (int row = 0; row < 64; row++)
            {
                bool onScreen = Bit(row, 5);
                bool open = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    Bit(row, 0), Bit(row, 1), Bit(row, 2), Bit(row, 3), Bit(row, 4), onScreen);

                if (!onScreen)
                {
                    Assert.IsFalse(open,
                        $"★ 캐릭터가 화면에 없는 행({row:00})이 부채꼴을 엽니다 — 보이지 않는 몸 자리 우클릭이 아래 앱의 메뉴와 겹쳐 뜹니다(§16-2c).");
                    closedWhenOffScreen++;
                }
                else if (open)
                {
                    openWhenOnScreen++;
                }

                // 음성 대조 — 여섯째 항만 참으로 바꾼 식(= 그 항을 없앤 세상)과 비교한다.
                bool withoutSixthTerm = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    Bit(row, 0), Bit(row, 1), Bit(row, 2), Bit(row, 3), Bit(row, 4), true);
                if (open != withoutSixthTerm) flipped++;
            }

            Assert.AreEqual(32, closedWhenOffScreen, "화면에 없는 행은 정확히 32개여야 합니다(2⁵).");
            Assert.AreEqual(1, openWhenOnScreen,
                "★ 양성 대조 — 화면에 있는 32행 중 정확히 한 조합(나머지 다섯 항이 전부 원하는 값)이 열려야 합니다. 0이면 측정기가 죽었습니다.");
            Assert.Greater(flipped, 0, "여섯째 항을 없애도 한 행도 안 바뀝니다 — 그 항이 실제로는 아무 일도 하지 않습니다.");
        }

        [Test]
        public void F26b_가시성_사실_4행이_상수_표와_같다()
        {
            for (int row = 0; row < 4; row++)
            {
                bool suspended = Bit(row, 1);
                bool hiddenByRunaway = Bit(row, 0);
                Assert.AreEqual(ExpectedCharacterOnScreen[row],
                    AppControlDirector.RightClickFanGatePolicy.CharacterOnScreenForInput(suspended, hiddenByRunaway),
                    $"CharacterOnScreenForInput(숨김={suspended}, 가출은신={hiddenByRunaway})가 상수 표와 다릅니다 — " +
                    (ExpectedCharacterOnScreen[row]
                        ? "보이는 캐릭터를 «화면에 없다»로 봅니다(평상시 우클릭 입구가 사라집니다)."
                        : "★ 보이지 않는 캐릭터를 «화면에 있다»로 봅니다(§16-2c B9·B10·B11 재발)."));
            }
        }

        [Test]
        public void F26c_호출부는_가시성_사실을_여섯째_항으로_따로_넘기고_게이트는_표본만_읽는다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));

            // (가) 표본 생성자가 가시성 사실을 캐릭터 축 · 가출 은신 두 공개 사실로 조립한다.
            string ctor = CallArgumentsOrFail(director, "var gate = new " + nameof(AppControlDirector.RightClickFanGateSample) + "(");
            string onScreenHead = nameof(AppControlDirector.RightClickFanGatePolicy.CharacterOnScreenForInput) + "(";
            StringAssert.Contains(onScreenHead, ctor,
                $"★ 게이트 표본이 가시성 사실({onScreenHead})을 채우지 않습니다:\n  {ctor.Trim()}");
            string visibility = CallArgumentsOrFail(ctor, onScreenHead);
            StringAssert.Contains("." + nameof(StickmanAgent.IsSuspended), visibility,
                "가시성 사실이 캐릭터 축(IsSuspended)을 읽지 않습니다 — 사용자 숨김·등급 2·다른 가상 데스크톱이 빠집니다.");
            StringAssert.Contains("." + nameof(StickMate.States.StickmanBlackboard.IsCharacterHiddenByRunaway), visibility,
                "가시성 사실이 가출 은신을 읽지 않습니다 — B11이 빠집니다.");

            // (나) 게이트는 표본의 값만 읽는다 — 진단·테스트가 보는 값이 게이트가 쓴 값이다.
            string gateArgs = CallArgumentsOrFail(director,
                nameof(AppControlDirector.RightClickFanGatePolicy) + "." + nameof(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan) + "(");
            string[] fields =
            {
                nameof(AppControlDirector.RightClickFanGateSample.CursorOverCharacter),
                nameof(AppControlDirector.RightClickFanGateSample.SwallowAllowsOpen),
                nameof(AppControlDirector.RightClickFanGateSample.UserSummonBlocked),
                nameof(AppControlDirector.RightClickFanGateSample.PrimaryButtonHeld),
                nameof(AppControlDirector.RightClickFanGateSample.CharacterOnScreen),
            };
            foreach (string field in fields)
            {
                StringAssert.Contains("gate." + field, gateArgs,
                    $"★ 게이트가 표본의 {field}를 읽지 않습니다 — 표본(진단·테스트가 보는 값)과 게이트가 쓴 값이 갈라집니다:\n  {gateArgs.Trim()}");
            }

            // (다) 0항에 섞지 않는다 — 커서 판정 본문은 기하만 본다.
            string cursorBody = MethodBodyOrFail(director, "private bool IsCursorOverCharacter(");
            StringAssert.Contains("OverlapPoint", cursorBody, "존재 대조 — 커서 판정 본문에서 기하 판정조차 못 찾았습니다(절단 무효).");
            Assert.AreEqual(-1, cursorBody.IndexOf("." + nameof(StickmanAgent.IsSuspended), System.StringComparison.Ordinal),
                "★ 0항(커서 ∈ 캐릭터)에 숨김이 섞였습니다 — EditMode가 기하와 가시성을 따로 못 돌리고, 항별 표본도 «커서가 밖»으로 잘못 말합니다.");
            Assert.AreEqual(-1, cursorBody.IndexOf(nameof(StickMate.States.StickmanBlackboard.IsCharacterHiddenByRunaway), System.StringComparison.Ordinal),
                "★ 0항에 가출 은신이 섞였습니다.");
        }

        [Test]
        public void F27_좌클릭_공통_입구는_캐릭터_축만_보고_잡기_시작보다_먼저_막는다()
        {
            string hitbox = StripComments(ReadSource("Interaction", "StickmanClickHitbox.cs"));
            string begin = MethodBodyOrFail(hitbox, "private void BeginPress(");
            string suspendedNeedle = "." + nameof(StickmanAgent.IsSuspended);

            int pressed = begin.IndexOf("_pressed = true", System.StringComparison.Ordinal);
            Assert.Greater(pressed, 0, "존재 대조 — BeginPress 본문에서 «잡기 시작» 대입을 못 찾았습니다(절단 무효).");
            int gate = begin.IndexOf(suspendedNeedle, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(gate, 0,
                "★ 좌클릭 공통 입구에 숨김 게이트가 없습니다 — 보이지 않는 몸 자리 클릭(아래 앱으로 간 클릭)이 드래그·가출 찾기·과자·활쏘기·스트레스 게이지를 건드립니다.");
            Assert.Less(gate, pressed, "★ 숨김 게이트가 «잡기 시작» 대입보다 뒤에 있습니다 — 막혀도 눌림 상태가 남습니다.");

            Assert.AreEqual(-1, begin.IndexOf(nameof(StickMate.States.StickmanBlackboard.IsCharacterHiddenByRunaway), System.StringComparison.Ordinal),
                "★ 좌클릭 입구가 가출 은신까지 막습니다 — 숨바꼭질 「찾기」 좌클릭이 죽습니다(캐릭터 축만 봐야 합니다).");
            Assert.AreEqual(-1, begin.IndexOf(nameof(AppControlDirector.RightClickFanGatePolicy.CharacterOnScreenForInput), System.StringComparison.Ordinal),
                "★ 좌클릭 입구가 우클릭의 «화면에 있는가» 식을 씁니다 — 그 식은 가출 은신을 포함해 「찾기」가 죽습니다.");

            string end = MethodBodyOrFail(hitbox, "private void EndPress(");
            StringAssert.Contains("_pressed = false", end, "존재 대조 — EndPress 본문 절단 무효.");
            Assert.AreEqual(-1, end.IndexOf(suspendedNeedle, System.StringComparison.Ordinal),
                "★ 놓기 경로에 숨김 게이트가 들어갔습니다 — 누른 채 숨은 경우 드래그가 끝나지 않습니다(뗄 때 경로는 건드리지 않는다).");
        }

        // ==================================================================
        // F5c — 삼킴 상태 fail-open (리더 판정 L-2)
        // ==================================================================

        [Test]
        public void F5c_삼킴상태_세_갈래가_실제로_갈린다()
        {
            // (가) 미지원 — 못 읽으면 «연다». 이것이 L-2의 전부다.
            Assert.IsTrue(AppControlDirector.RightClickFanGatePolicy.SwallowAllowsOpen(queried: false, swallowed: false),
                "삼킴 상태를 못 읽었는데 닫아걸었습니다 — fail-closed로 되돌린 변경입니다(리더 판정 L-2 위반). " +
                "그 실패는 «우클릭이 아예 안 먹는다»이고, 사용자가 신고한 바로 그 증상이며, 조용합니다.");

            // 미지원이면 swallowed 값이 무엇이든 결과가 같아야 한다(읽지 못한 값을 보면 안 된다).
            Assert.IsTrue(AppControlDirector.RightClickFanGatePolicy.SwallowAllowsOpen(queried: false, swallowed: true));

            // (나) 삼켜짐 — 연다.
            Assert.IsTrue(AppControlDirector.RightClickFanGatePolicy.SwallowAllowsOpen(queried: true, swallowed: true));

            // (다) ★ 명시적 미삼킴 — <b>이 한 갈래만</b> 닫는다. 여기가 1렌더프레임 경합을 닫는 자리다.
            Assert.IsFalse(AppControlDirector.RightClickFanGatePolicy.SwallowAllowsOpen(queried: true, swallowed: false),
                "OS가 «이 클릭은 우리 것이 아니다»라고 답했는데도 엽니다 — 게이트가 아무 일도 하지 않습니다.");
        }

        // ==================================================================
        // F6′ — Snap45 회귀 잠금 (§1-3-4 「유일한 함정」)
        // ==================================================================

        [Test]
        public void F6_바이어스가_0이면_SnapFanBaseAngle은_Snap45와_전_격자에서_같다()
        {
            int compared = 0;
            for (float x = 0f; x <= ScreenPoints.x; x += 37f)
            {
                for (float y = 0f; y <= ScreenPoints.y; y += 29f)
                {
                    var anchor = new Vector2(x, y);
                    float legacy = GearRadialMenuWidget.Snap45(ScreenPoints * 0.5f - anchor);
                    float now = GearRadialMenuWidget.SnapFanBaseAngle(anchor, ScreenPoints, 0f);
                    compared++;
                    Assert.AreEqual(legacy, now,
                        $"앵커 {anchor}에서 갈립니다 — 바이어스 0은 옛 식과 <b>비트 동일</b>해야 합니다.");
                }
            }
            Assert.Greater(compared, 0, "격자가 비어 있습니다 — 이 테스트가 아무것도 재지 않았습니다.");
        }

        [Test]
        public void F6_출하_기본_톱니자리의_기준각은_이_변경으로_움직이지_않는다()
        {
            float shipped = GearRadialMenuWidget.SnapFanBaseAngle(ShippedGearCenterPoints, ScreenPoints, 0f);
            Assert.AreEqual(225f, shipped, 0.001f,
                "출하 기본 톱니 자리의 θ₀가 225°가 아닙니다 — UX_FLOW 36-3-4가 못박은 기본 화면이 바뀌었습니다.");

            // ★ 음성 대조 — 바이어스를 <b>톱니 경로에</b> 태우면 그 화면이 실제로 깨진다.
            //   이 단언이 「분리된 함수」의 존재 이유 전부다(§1-3-4).
            float poisoned = GearRadialMenuWidget.SnapFanBaseAngle(
                ShippedGearCenterPoints, ScreenPoints, GearRadialMenuWidget.FanUpBiasPoints);
            Assert.AreNotEqual(shipped, poisoned,
                "바이어스를 태워도 톱니 기본각이 그대로입니다 — 바이어스가 아무 일도 하지 않거나 " +
                "이 테스트가 함정을 재현하지 못하고 있습니다.");
            Assert.AreEqual(180f, poisoned, 0.001f,
                "함정의 실제 값은 180°입니다(§1-3-4). 값이 다르면 기하 전제가 바뀐 것이므로 설계를 다시 보세요.");
        }

        /// <summary>
        /// ★★ 2026-09-06 — <b>이 단언의 성격이 등호에서 부등호로 바뀌었다.</b>
        ///
        /// <para>옛 판은 <c>FanUpBiasPoints == 위성 궤도 168 + 클램프 상자 반폭 28</c>이라는 <b>등호</b>였다.
        /// 사용자 신고 <i>"끄기 버튼만 따로 떨어져 있다"</i>로 위성이 폐지되어 실제 도달 반경이
        /// <b>148 + 28 = 176pt</b>로 줄었고, <c>SatelliteOrbitRadiusPoints</c>는 사라졌다.</para>
        ///
        /// <para><b>그래도 상수는 196 그대로 뒀다.</b> 유도식이 «앵커가 이만큼 떨어져 있으면 어느 방향으로
        /// 열어도 들어간다»는 <b>충분조건</b>이라 크게 잡는 쪽이 안전하고, 이 값을 줄이면 §1-3-3이
        /// 1pt 격자로 실측한 θ₀ 분포표가 통째로 낡는다 — 이번 라운드가 다시 잰 것은 <b>배치 사다리</b>이지
        /// θ₀ 분포가 아니다.</para>
        ///
        /// <para>그래서 «임의값이 아니다»를 지키는 방법도 바뀐다: <b>도달 반경 이상이면서, 그 두 배는
        /// 넘지 않는다</b>. 상한이 없으면 «크면 안전하다»가 무한정 커지는 변명이 되고, 그때 이 단언은
        /// 무엇이든 통과시킨다.</para>
        /// </summary>
        [Test]
        public void F6_바이어스는_도달_반경_이상이고_그_두_배를_넘지_않는다()
        {
            float clampHalf = (GearRadialMenuWidget.ButtonDiameterPoints
                + GearRadialMenuWidget.ClampBoxPaddingPoints) * 0.5f;
            float reach = GearRadialMenuWidget.OrbitRadiusPoints + clampHalf;

            Assert.GreaterOrEqual(GearRadialMenuWidget.FanUpBiasPoints, reach,
                $"FanUpBiasPoints({GearRadialMenuWidget.FanUpBiasPoints:F0})가 부채꼴 도달 반경({reach:F0}pt = " +
                $"궤도 {GearRadialMenuWidget.OrbitRadiusPoints:F0} + 클램프 상자 반폭 {clampHalf:F0})보다 작습니다 — " +
                "§1-3-3의 «이만큼 떨어져 있으면 어느 방향으로 열어도 들어간다»가 성립하지 않습니다.");
            Assert.LessOrEqual(GearRadialMenuWidget.FanUpBiasPoints, reach * 2f,
                $"FanUpBiasPoints({GearRadialMenuWidget.FanUpBiasPoints:F0})가 도달 반경의 두 배({reach * 2f:F0}pt)를 " +
                "넘습니다 — 그쯤 되면 «도달 반경에서 유도했다»가 더 이상 사실이 아니고 임의값입니다.");
        }

        // ==================================================================
        // F7′ — 화면 중앙선의 180° 반전이 사라졌는가
        // ==================================================================

        private static int CountSharpReversals(float upBias, float scanY, out float firstReversalX)
        {
            firstReversalX = float.NaN;
            int reversals = 0;
            float prev = GearRadialMenuWidget.SnapFanBaseAngle(new Vector2(0f, scanY), ScreenPoints, upBias);
            for (float x = 1f; x <= ScreenPoints.x; x += 1f)
            {
                float now = GearRadialMenuWidget.SnapFanBaseAngle(new Vector2(x, scanY), ScreenPoints, upBias);
                float delta = Mathf.Abs(Mathf.DeltaAngle(prev, now));
                if (delta >= 135f)
                {
                    reversals++;
                    if (float.IsNaN(firstReversalX)) firstReversalX = x;
                }
                prev = now;
            }
            return reversals;
        }

        [Test]
        public void F7_바이어스가_화면_중앙선의_180도_반전을_없앤다()
        {
            float centerY = ScreenPoints.y * 0.5f;

            // ★ 양성 대조 먼저 — 측정기가 살아 있는가. 바이어스 0에서는 반전이 실제로 잡혀야 한다.
            int legacyReversals = CountSharpReversals(0f, centerY, out float legacyX);
            Assert.Greater(legacyReversals, 0,
                "바이어스 0에서도 급반전이 0건입니다 — 스캐너가 죽었습니다(이 상태로는 아래 단언이 무의미합니다).");
            Assert.AreEqual(ScreenPoints.x * 0.5f, legacyX, 1.5f,
                "급반전 지점이 화면 가로 중앙 근처가 아닙니다 — 스캔 전제가 설계와 다릅니다.");

            // 본 단언 — 캐릭터 경로에서는 같은 스캔에 급반전이 0건이다.
            int biased = CountSharpReversals(GearRadialMenuWidget.FanUpBiasPoints, centerY, out _);
            Assert.AreEqual(0, biased,
                "캐릭터가 가장 자주 지나는 높이(화면 세로 중앙선)에서 방향이 여전히 뒤집힙니다 — " +
                "1pt 걸을 때마다 부채꼴이 좌우로 갈립니다(§1-3-2).");
        }

        [Test]
        public void F7_반전_지점은_중앙_위_바이어스만큼_이동한다()
        {
            float movedScanY = ScreenPoints.y * 0.5f + GearRadialMenuWidget.FanUpBiasPoints;
            int reversals = CountSharpReversals(GearRadialMenuWidget.FanUpBiasPoints, movedScanY, out _);
            Assert.Greater(reversals, 0,
                "반전이 사라진 것이 아니라 «중앙 위 바이어스만큼» 옮겨 간 것이어야 합니다 — " +
                "그 자리에서도 0건이면 스캔이 잘못됐거나 바이어스가 안 걸린 것입니다.");
        }

        // ==================================================================
        // 호명 반응 비트 — 붙잡는 시간은 대사 예산에서 나온다 (MOTION §1-8-4)
        // ==================================================================

        [Test]
        public void 대사가_없으면_붙잡기는_펼침_예산과_같다()
        {
            Assert.AreEqual(GearRadialMenuWidget.ExpandTotalSeconds,
                AppControlDirector.ReactionHoldSecondsFor(null), 1e-5f);
            Assert.AreEqual(GearRadialMenuWidget.ExpandTotalSeconds,
                AppControlDirector.ReactionHoldSecondsFor(string.Empty), 1e-5f);
        }

        [Test]
        public void 대사가_있으면_붙잡기는_그_대사의_노출_상한이다()
        {
            string line = AppControlDirector.PlaceholderReactionLine;

            // 기대값은 프로덕션 예산 함수에서 <b>독립적으로</b> 만든다(같은 상수를 두 번 타이핑하지 않는다).
            float expected = DialogueBudget.MaxVisibleSecondsFor(line,
                DialogueTiming.PopInSeconds, DialogueTiming.FadeOutSeconds);

            Assert.AreEqual(expected, AppControlDirector.ReactionHoldSecondsFor(line), 1e-5f,
                "붙잡는 시간이 대사 노출 상한과 갈라졌습니다 — 그만큼 말풍선이 메뉴에서 떨어져 나갑니다.");

            // ★ 양성 대조 — 그 값이 「대사 없음」 분기와 실제로 다르다(둘이 같으면 위 단언이 공허하다).
            Assert.Greater(expected, GearRadialMenuWidget.ExpandTotalSeconds,
                "대사 예산이 펼침 예산보다 크지 않습니다 — 두 분기가 구분되지 않습니다.");
        }

        [Test]
        public void 반응_대사_공급자는_기본적으로_비어_있다()
        {
            // design-narrative 문구 대기 — 지금은 아무 말도 하지 않는 것이 <b>정직한 상태</b>다.
            // 문구가 확정되면 이 단언을 함께 고쳐라(그때 이 줄이 「누가 문구를 넣었는가」를 묻는다).
            Assert.IsNull(AppControlDirector.ReactionLineProvider,
                "반응 대사 공급자가 꽂혀 있습니다 — 문구가 확정됐다면 이 테스트도 함께 갱신하세요.");
        }

        // ==================================================================
        // 소스 감사 — F3′ · F4 · F20c · 앵커
        // ==================================================================

        private static string Root => Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static string ReadSource(params string[] parts)
        {
            string path = Path.Combine(Root, Path.Combine(parts));
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        private static string StripComments(string source)
        {
            string[] lines = source.Split('\n');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;
                int idx = lines[i].IndexOf("//", System.StringComparison.Ordinal);
                sb.Append(idx >= 0 ? lines[i].Substring(0, idx) : lines[i]).Append('\n');
            }
            return sb.ToString();
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        [Test]
        public void F3_우클릭_수용_경로는_클릭관통을_추가로_해제하지_않는다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));

            // 부재 단언 — 우클릭을 받는 그 자리에서 관통을 건드리면 비침해가 실제로 나빠진다.
            Assert.AreEqual(-1, director.IndexOf("SetClickThrough", System.StringComparison.Ordinal),
                "우클릭 경로가 클릭관통을 직접 건드립니다 — 관통 해제는 히트테스트가 상시로 하고 있고, " +
                "여기서 추가로 해제하면 그만큼 남의 앱의 클릭을 더 먹습니다(원칙 2).");
            Assert.AreEqual(-1, director.IndexOf("ILocalClickCaptureService", System.StringComparison.Ordinal),
                "우클릭 경로가 별도의 클릭 포획 서비스를 씁니다 — 설계에 없는 삼킴 수단입니다(dev-platform §2-6에서 전부 기각).");

            // ★ 부재 단언에 존재 대조를 붙인다 — 프로브가 살아 있는지 같은 실행 안에서 증명한다.
            //   (같은 스캐너가 이 파일의 다른 실재 심볼은 실제로 찾아낸다.)
            StringAssert.Contains(nameof(IGlobalPointerButtonService), director,
                "우클릭 채널 자체가 이 파일에 없습니다 — 위 두 «없음» 판정이 무효입니다(파일을 잘못 읽었습니다).");
        }

        [Test]
        public void F4_우클릭_조회_호출부는_정확히_한_곳이다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));
            string needle = nameof(IGlobalPointerButtonService.TryGetSecondaryButtonPressed);

            Assert.AreEqual(1, CountOccurrences(director, needle),
                $"{needle} 호출부가 하나가 아닙니다 — 폴링 주체가 둘이 되면 «누가 우클릭을 먹었나»가 갈립니다.");

            // ★ 존재 대조 — 같은 스캐너가 좌클릭 조회도 실제로 찾아낸다(니들이 살아 있다).
            Assert.Greater(CountOccurrences(director, nameof(IGlobalPointerButtonService.TryGetPrimaryButtonPressed)), 0,
                "좌클릭 조회를 못 찾았습니다 — 스캐너가 죽었을 가능성이 높습니다.");
        }

        [Test]
        public void F20b_허가는_반드시_펼침보다_앞에서_난다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));

            int grant = director.IndexOf(nameof(StickmanAgent.TryGrantUserSummon), System.StringComparison.Ordinal);
            int expand = director.IndexOf(nameof(GearRadialMenuWidget.ExpandOrReanchor), System.StringComparison.Ordinal);

            Assert.Greater(grant, 0, "우클릭 경로에 등급 1 허가 발급이 없습니다 — 전체화면 앱 위에서 우클릭이 먹지 않습니다.");
            Assert.Greater(expand, 0, "우클릭 경로에 펼침 호출이 없습니다.");
            Assert.Less(grant, expand,
                "허가가 펼침보다 <b>뒤</b>에 있습니다 — 부채꼴은 자기 LateUpdate에서 ArePanelsSuppressed를 " +
                "폴링해 스스로 접으므로, 펼쳐지는 그 프레임에 회수되어 «우클릭이 안 먹는다»가 됩니다. " +
                "2026-09-03에 톱니에서 실제로 났던 증상입니다.");
        }

        [Test]
        public void F20c_허가_발급_지점은_네_곳이다()
        {
            string needle = nameof(StickmanAgent.TryGrantUserSummon);
            // ★ 2026-09-14 (E-2) — 정보창 사용자 열기가 넷째 발급 지점이 됐다
            //   (등급 1에서 단축키로 연 정보창이 그 프레임에 닫히던 옛 Ignore 갭).
            string[][] issuers =
            {
                new[] { "Interaction", "InfoGearIconWidget.cs" },
                new[] { "Interaction", "SettingsWindow.cs" },
                new[] { "Interaction", "AppControlDirector.cs" },
                new[] { "Interaction", "CharacterInfoWindow.cs" },
            };

            int found = 0;
            foreach (string[] parts in issuers)
            {
                string src = StripComments(ReadSource(parts));
                if (CountOccurrences(src, needle + "(") > 0) found++;
            }

            Assert.AreEqual(issuers.Length, found,
                $"허가 발급 지점이 {found}곳입니다 — 톱니 클릭 · 설정창 열기 · 캐릭터 우클릭 · 정보창 열기 네 곳이어야 합니다. " +
                "하나라도 빠지면 그 진입점은 등급 1(전체화면 앱 위)에서 통째로 죽습니다.");

            // ★ 음성 대조 — 존재하지 않는 심볼로 같은 스캐너를 돌리면 0곳이어야 한다(프로브 생존).
            int ghosts = 0;
            foreach (string[] parts in issuers)
            {
                if (CountOccurrences(StripComments(ReadSource(parts)), "ZzzNotARealSymbolQqq(") > 0) ghosts++;
            }
            Assert.AreEqual(0, ghosts, "가짜 심볼이 검출됐습니다 — 이 감사 전체가 무효입니다.");
        }

        [Test]
        public void F20e_설정창_자동_복귀는_비허가_진입점으로만_정보창을_연다()
        {
            // ★ 2026-09-14 (E-2 · §16-2b B5) — 정보창 사용자 열기가 허가를 내게 되면서, 설정창 닫힘 자동 복귀가 같은 진입점을
            //   쓰면 «우리가 스스로에게 발급하는 면제»가 된다. 런타임 관측은 PanelsOnlyTierMouseEntryTests의 B5가 한다.
            string settings = StripComments(ReadSource("Interaction", "SettingsWindow.cs"));
            string body = MethodBodyOrFail(settings, "private void RestoreInfoWindowIfNeeded(");

            // 존재 대조 — 같은 본문에서 억제 가드를 실제로 찾는다(절단기 생존).
            StringAssert.Contains("." + nameof(StickmanAgent.ArePanelsSuppressed), body,
                "자동 복귀 본문에서 억제 가드를 못 찾았습니다 — 본문 절단이 틀렸거나 가드가 사라졌습니다(아래 판정 무효).");

            string reopen = "." + nameof(CharacterInfoWindow.ReopenFromSheet) + "(";
            StringAssert.Contains(reopen, body,
                $"★ 설정창 자동 복귀가 비허가 진입점({reopen})으로 정보창을 열지 않습니다:\n{body}");

            string userOpen = "." + nameof(CharacterInfoWindow.Open) + "(";
            Assert.AreEqual(-1, body.IndexOf(userOpen, System.StringComparison.Ordinal),
                "★ 설정창 자동 복귀가 정보창 사용자 열기(Open)를 부릅니다 — 그 진입점은 등급 1 허가를 내므로 " +
                "«우리가 스스로에게 발급하는 면제»가 됩니다(§16-2b B5).");
            Assert.AreEqual(-1, body.IndexOf(nameof(StickmanAgent.TryGrantUserSummon), System.StringComparison.Ordinal),
                "★ 설정창 자동 복귀 본문이 허가를 직접 냅니다(§16-2b B5 위반).");
        }

        /// <summary>
        /// <paramref name="openIndex"/>의 여는 괄호와 짝인 닫는 괄호 위치. 문자열·문자 리터럴 안의 괄호는 세지 않는다.
        /// <para>한계: 보간 구멍 안에 또 따옴표가 든 문자열은 다루지 않는다 — 그런 본문을 자르게 되면 아래 <c>Assert.Fail</c>이나
        /// 호출부의 존재 대조가 먼저 빨개진다(조용히 틀린 본문을 돌려주지 않는다).</para>
        /// </summary>
        private static int MatchingCloseOrFail(string src, int openIndex, char open, char close, string what)
        {
            int depth = 0;
            for (int i = openIndex; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '"')
                {
                    bool verbatim = i > 0 && src[i - 1] == '@';
                    i++;
                    while (i < src.Length)
                    {
                        if (!verbatim && src[i] == '\\') { i += 2; continue; }
                        if (src[i] == '"')
                        {
                            if (verbatim && i + 1 < src.Length && src[i + 1] == '"') { i += 2; continue; }
                            break;
                        }
                        i++;
                    }
                    continue;
                }
                if (c == '\'')
                {
                    i++;
                    while (i < src.Length && src[i] != '\'')
                    {
                        if (src[i] == '\\') i++;
                        i++;
                    }
                    continue;
                }
                if (c == open) depth++;
                else if (c == close && --depth == 0) return i;
            }
            Assert.Fail($"{what}의 짝 괄호를 찾지 못했습니다 — 절단기가 본문을 자르지 못했습니다(아래 판정 무효).");
            return -1;
        }

        /// <summary>호출 <paramref name="head"/>(여는 괄호까지 포함)의 인자 목록 텍스트. 호출이 정확히 한 곳이어야 한다.</summary>
        private static string CallArgumentsOrFail(string src, string head)
        {
            Assert.AreEqual(1, CountOccurrences(src, head),
                $"호출 '{head}'가 정확히 한 곳이 아닙니다 — 절단 대상이 없거나 모호합니다.");
            int open = src.IndexOf(head, System.StringComparison.Ordinal) + head.Length - 1;
            int close = MatchingCloseOrFail(src, open, '(', ')', head);
            return src.Substring(open + 1, close - open - 1);
        }

        /// <summary>메서드 <paramref name="signature"/>의 본문 텍스트(중괄호 안). 선언이 정확히 한 곳이어야 한다.</summary>
        private static string MethodBodyOrFail(string src, string signature)
        {
            Assert.AreEqual(1, CountOccurrences(src, signature),
                $"선언 '{signature}'가 정확히 한 곳이 아닙니다 — 이름이 바뀌었거나 겹칩니다.");
            int sig = src.IndexOf(signature, System.StringComparison.Ordinal);
            int open = src.IndexOf('{', sig);
            Assert.Greater(open, sig, $"'{signature}' 뒤에서 본문 시작 중괄호를 못 찾았습니다.");
            int close = MatchingCloseOrFail(src, open, '{', '}', signature);
            return src.Substring(open + 1, close - open - 1);
        }

        [Test]
        public void 앵커는_머리가_아니라_몸_중심이다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));

            StringAssert.Contains("CharacterHeightWorld * 0.5f", director,
                "앵커가 «발밑 + 신장/2»(= 몸 중심)로 계산되지 않습니다. 머리 중심에 걸면 배율 1.00의 " +
                "아래 세 방향에서 버튼 히트원이 캐릭터 잡기영역을 −5.21pt 관통합니다(§1-2).");
        }

        [Test]
        public void F25_잡기_억제는_부채꼴의_판정식_하나만_부른다()
        {
            string hitbox = StripComments(ReadSource("Interaction", "StickmanClickHitbox.cs"));

            StringAssert.Contains(nameof(GearRadialMenuWidget.HitTest), hitbox,
                "부채꼴 버튼 위에서 캐릭터 잡기를 억제하는 가드가 없습니다 — 세로 일렬 폴백/평행이동에서 " +
                "같은 클릭이 «버튼 누름»과 «캐릭터 잡기»로 둘 다 해석됩니다(§5-4).");

            // ★ 판정식을 두 벌 쓰지 않았는가 — 히트박스가 자기 원·반지름 계산을 다시 만들지 않았다.
            Assert.AreEqual(-1, hitbox.IndexOf(nameof(GearRadialMenuWidget.OrbitRadiusPoints), System.StringComparison.Ordinal),
                "히트박스가 부채꼴의 궤도 상수를 직접 읽습니다 — 판정식이 두 벌이 되면 배치 사다리와 " +
                "조용히 갈라지고, 그 갈라짐은 «가끔 캐릭터가 안 잡힌다»로만 나타납니다.");
        }

        [Test]
        public void 대기_톱니는_사용자숨김과_가출_두_상태를_본다()
        {
            string gear = StripComments(ReadSource("Interaction", "InfoGearIconWidget.cs"));

            StringAssert.Contains(nameof(StickmanAgent.IsUserHiddenOnly), gear,
                "대기 톱니가 사용자 명시 숨김을 보지 않습니다.");
            StringAssert.Contains(nameof(RunawayDirector.IsRunawayActive), gear,
                "대기 톱니가 가출을 보지 않습니다 — 가출 Hidden은 최대 5400초(90분)이고 그동안 " +
                "macOS에는 마우스 진입점이 0개가 됩니다(ux-designer P0-2).");

            // ★★ 리더 판정 L-10 — 설정 토글은 이 조건에 <b>들어가면 안 된다</b>(자기 잠금이 된다).
            //   부재 단언이라 위 두 존재 단언이 프로브 생존을 함께 증명한다.
            Assert.AreEqual(-1, gear.IndexOf(nameof(AppSettingsModel.GearIconVisible), System.StringComparison.Ordinal),
                "★ 「톱니 아이콘」 설정 토글이 다시 게이트에 들어왔습니다 — 그 값은 세이브에 내려가고, " +
                "끄면 대기 톱니가 사라져 되돌리는 문(설정창)에 닿는 마우스 경로가 0이 됩니다. " +
                "2026-09-03 사용자 신고 «다시 나오게 할 방법이 없어»의 정확한 재발입니다(리더 판정 L-10).");
        }

        // ==================================================================
        // 대기 톱니 게이트 — 순수 정책 진리표 (test-engineer 개선 ①)
        // ==================================================================

        [Test]
        public void 대기톱니_진리표_4행_전수()
        {
            // 조건은 「사용자가 숨겼다」가 아니라 <b>「우클릭할 몸이 없다」</b>다 — 두 상태가 OR로 합쳐진다.
            Assert.IsFalse(InfoGearIconWidget.StandbyGearPolicy.ShouldShow(false, false),
                "캐릭터가 화면에 있는데 톱니가 섭니다 — 「평소에는 없다」가 이 라운드의 사용자 지시입니다.");
            Assert.IsTrue(InfoGearIconWidget.StandbyGearPolicy.ShouldShow(true, false),
                "사용자 명시 숨김에서 톱니가 없습니다 — 2026-09-03 확정(«메뉴버튼은 보여야지»)의 회귀입니다.");
            Assert.IsTrue(InfoGearIconWidget.StandbyGearPolicy.ShouldShow(false, true),
                "가출 중에 톱니가 없습니다 — macOS에서 최대 5400초(90분) 마우스 진입점 0입니다(P0-2).");
            Assert.IsTrue(InfoGearIconWidget.StandbyGearPolicy.ShouldShow(true, true));

            // ★ 음성 대조 — 각 항이 실제로 일한다. 항 하나를 상수 false로 죽이면 결과가 바뀌어야 한다.
            int flippedByHidden = 0, flippedByRunaway = 0;
            for (int row = 0; row < 4; row++)
            {
                bool hidden = Bit(row, 0), runaway = Bit(row, 1);
                bool real = InfoGearIconWidget.StandbyGearPolicy.ShouldShow(hidden, runaway);
                if (real != InfoGearIconWidget.StandbyGearPolicy.ShouldShow(false, runaway)) flippedByHidden++;
                if (real != InfoGearIconWidget.StandbyGearPolicy.ShouldShow(hidden, false)) flippedByRunaway++;
            }
            Assert.Greater(flippedByHidden, 0, "사용자 숨김 항이 아무 일도 하지 않습니다.");
            Assert.Greater(flippedByRunaway, 0,
                "가출 항이 아무 일도 하지 않습니다 — P0-2를 닫는 그 항입니다(ux-designer §1-7-3 가-1).");
        }

        [Test]
        public void 대기톱니_사유_문장은_네_상태를_전부_구분한다()
        {
            // 「왜 지금 서 있는가」를 테스트가 게이트를 <b>재구현하지 않고</b> 물어보는 창구(개선 ②).
            string[] said =
            {
                InfoGearIconWidget.StandbyGearPolicy.Describe(false, false),
                InfoGearIconWidget.StandbyGearPolicy.Describe(true, false),
                InfoGearIconWidget.StandbyGearPolicy.Describe(false, true),
                InfoGearIconWidget.StandbyGearPolicy.Describe(true, true),
            };
            CollectionAssert.AllItemsAreNotNull(said);
            CollectionAssert.AllItemsAreUnique(said,
                "네 상태 중 둘이 같은 문장을 냅니다 — 로그를 봐도 어느 쪽인지 못 가릅니다.");
            foreach (string s in said) Assert.IsNotEmpty(s);
        }

        [Test]
        public void 테스트_우회_훅은_프로덕션에서_한_번도_불리지_않는다()
        {
            // ★ 개선 ③ — 이 훅은 릴리스 런타임에 <c>public static</c>으로 실려 있다. 프로덕션이
            //   실수로 부르면 「대기 톱니」 규칙이 통째로 무력해지고, 그 실패는 <b>조용하다</b>
            //   (화면에 톱니가 그냥 보일 뿐이라 아무도 버그로 신고하지 않는다).
            string needle = nameof(InfoGearIconWidget.SetStandbyGateBypassedForTests);

            // 런타임 스크립트 + 에디터 스크립트(SceneBootstrapper 등)를 <b>둘 다</b> 훑는다 —
            // 씬 조립기가 부르면 그것도 프로덕션 경로다.
            var roots = new System.Collections.Generic.List<string> { Root };
            string editorRoot = Path.Combine(Application.dataPath, "Editor");
            if (Directory.Exists(editorRoot)) roots.Add(editorRoot);

            var offenders = new System.Collections.Generic.List<string>();
            int productionFilesScanned = 0;
            int testHits = 0;

            var allFiles = new System.Collections.Generic.List<string>();
            foreach (string r in roots) allFiles.AddRange(Directory.GetFiles(r, "*.cs", SearchOption.AllDirectories));

            foreach (string path in allFiles)
            {
                bool isTest = path.Replace('\\', '/').Contains("/Tests/");
                string src = StripComments(File.ReadAllText(path));
                int hits = CountOccurrences(src, needle);
                if (isTest) { testHits += hits; continue; }

                productionFilesScanned++;
                // 선언 자체는 프로덕션에 있어야 하므로 <b>호출부</b>만 센다.
                if (CountOccurrences(src, needle + "(") > CountOccurrences(src, "void " + needle + "("))
                    offenders.Add(Path.GetFileName(path));
            }

            // ★ 스캔이 죽지 않았음을 먼저 보인다(존재 대조) — 이 두 단언이 없으면 «0건»이
            //   «깨끗함»인지 «아무것도 안 읽음»인지 구분되지 않는다.
            Assert.Greater(productionFilesScanned, 0, "프로덕션 소스를 한 개도 못 읽었습니다 — 경로가 틀렸습니다.");
            Assert.Greater(testHits, 0,
                $"{needle} 호출을 테스트에서도 못 찾았습니다 — 니들이 죽었습니다(PlayMode 격리 픽스처가 부릅니다).");

            Assert.IsEmpty(offenders,
                $"프로덕션이 테스트 우회 훅을 부릅니다: {string.Join(", ", offenders)}. " +
                "그 순간 「평소에는 톱니가 없다」가 전원에게 거짓이 되고, 화면에는 그냥 톱니가 보일 뿐이라 " +
                "아무도 버그로 신고하지 않습니다.");
        }

        [Test]
        public void 차단막은_넓이0인_톱니_사각형의_위치를_삼키지_않는다()
        {
            // ★★ P0 회귀 잠금 (test-engineer 실측) — 톱니가 「평상시 숨김」이면 IconScreenRect는
            //   넓이 0이지만 <b>위치는 화면 우상단 그대로</b>다. 경계상자 합집합은 그 점의 위치까지
            //   삼켜서, 부채꼴과 우상단을 잇는 <b>보이지 않는 띠</b>가 차단막이 된다(원칙 2 위반).
            string gear = StripComments(ReadSource("Interaction", "InfoGearIconWidget.cs"));

            StringAssert.Contains("gearHits", gear,
                "차단막 계산에 「톱니 사각형이 실제로 넓이를 갖는가」 분기가 없습니다 — " +
                "넓이 0인 점의 위치가 합집합에 새어 화면을 가로지르는 클릭 흡수 띠가 됩니다.");

            // 조건부 없이 Union 한 줄로 되돌리면 그 띠가 그대로 돌아온다 — 그 형태를 부재로 못박는다.
            Assert.AreEqual(-1,
                gear.IndexOf("fanVisible ? Union(", System.StringComparison.Ordinal),
                "★ 차단막이 다시 «부채꼴이 보이면 무조건 Union»으로 되돌아갔습니다. " +
                "그 한 줄이 P0의 정확한 형태입니다(실측 640×480에서 정당 면적의 2.31배).");
        }

        [Test]
        public void 죽은_톱니_토글은_설정창에서_사라졌다()
        {
            string settings = StripComments(ReadSource("Interaction", "SettingsWindow.cs"));

            Assert.AreEqual(-1, settings.IndexOf("general.gearIcon", System.StringComparison.Ordinal),
                "「톱니 아이콘」 토글이 설정창에 남아 있습니다 — 게이트가 그 값을 더 이상 읽지 않으므로 " +
                "꺼도 아무 일도 일어나지 않고, 그러면서 캡션은 «끄면 진입점이 사라진다»고 거짓을 말합니다. " +
                "게다가 그 값은 세이브에 내려가 「되돌리는 문이 없는 저장 항목」이 됩니다(41-8 위반).");

            // ★ 존재 대조 — 같은 스캐너가 같은 카드의 다른 행은 실제로 찾아낸다(프로브 생존).
            StringAssert.Contains("general.gearHome", settings,
                "[톱니 위치] 행까지 사라졌습니다 — 그 행은 <b>남겨야 한다</b>(ux-designer W-5 철회): " +
                "톱니는 여전히 드래그로 옮길 수 있고 그 자리는 세이브에 영구히 앉습니다.");
        }

        [Test]
        public void 부채꼴은_자기_임대를_스스로_갱신한다()
        {
            string fan = StripComments(ReadSource("Interaction", "GearRadialMenuWidget.cs"));

            StringAssert.Contains(nameof(StickmanAgent.RenewUserSummonGrant), fan,
                "★ 부채꼴이 자기 임대를 갱신하지 않습니다(game-architect I-28). 톱니가 대신 보고하던 " +
                "그 코드는 톱니의 가시성 게이트 뒤에 있었고, 톱니가 「평상시 숨김」이 된 지금 그 조합은 " +
                "평상시입니다 — 등급 1에서 사용자가 쓰는 도중에 부채꼴이 스스로 걷힙니다.");
        }
    }
}
