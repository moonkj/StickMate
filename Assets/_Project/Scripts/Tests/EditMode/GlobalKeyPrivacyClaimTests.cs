using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-02 — <c>Platform/IGlobalKeyStateService.cs</c>의 <b>프라이버시 논거</b>를 잠근다.
    ///
    /// <para>그 문서는 "키로거 형태가 되지 않는다"의 유일한 근거이고, 맥앱스토어·스팀 심사나
    /// 프라이버시 문의가 들어오는 날 <b>그대로 제출된다</b>. 그런데 두 군데의 숫자가 각각
    /// <b>"동작키 4개"</b>(실제 17) · <b>"열거된 11개 키"</b>(실제 20)로 썩어 있었다 —
    /// 기능이 늘 때 enum만 늘고 문장은 아무도 안 봤기 때문이다.</para>
    ///
    /// <para><b>그래서 숫자를 고치는 것만으로는 부족하다.</b> 같은 방식으로 다시 썩는다.
    /// 여기서는 두 가지를 잠근다:</para>
    /// <list type="number">
    ///   <item><b>문장의 숫자 ↔ enum 실측</b>. 기대값을 이 테스트에 적지 않는다 —
    ///         <c>Enum.GetValues</c>가 정본이고 문장이 따라간다.</item>
    ///   <item><b>논거 자체</b>. "개수가 적어서 안전하다"가 아니라 <b>"동작키는 조합키 3개가 눌린
    ///         동안에만 조회된다"</b>가 실제 보장이므로, <c>AppControlDirector</c>의 모든 동작키
    ///         조회가 <c>chord &amp;&amp;</c> 뒤에 있는지를 소스로 확인한다. 개수는 늘어도 이 게이트는
    ///         늘지 않는다.</item>
    /// </list>
    ///
    /// <para>★ 니들(정규식)이 죽으면 <b>조용히 초록</b>이 되는 자리라, 각 검사는 먼저
    /// <b>매치가 0건이 아님</b>을 단언한다(CLAUDE.md "부재 단언은 썩으면 조용히 초록이 된다").</para>
    /// </summary>
    public sealed class GlobalKeyPrivacyClaimTests
    {
        private static string Root => Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static string ReadSource(params string[] parts)
        {
            string path = Path.Combine(Root, Path.Combine(parts));
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        /// <summary>줄 주석을 걷어낸다. 논거를 재는 검사(②)의 대상은 <b>코드</b>이고, 이 저장소의
        /// 주석은 코드 조각을 길게 인용하므로 그대로 두면 전부 오탐이 된다.</summary>
        private static string StripComments(string source)
        {
            string[] lines = source.Split('\n');
            var sb = new StringBuilder();
            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                int idx = line.IndexOf("//", StringComparison.Ordinal);
                sb.Append(idx >= 0 ? line.Substring(0, idx) : line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>조합키. 이 셋만 "다른 키를 물어봐도 되는지"를 여는 열쇠이고, 나머지는 전부 동작키다.
        /// 문자열이 아니라 <b>enum 값</b>으로 적는다 — 이름이 바뀌면 컴파일이 먼저 막는다.</summary>
        private static readonly GlobalKey[] Modifiers =
            { GlobalKey.Command, GlobalKey.Option, GlobalKey.Control };

        private static int TotalKeys => Enum.GetValues(typeof(GlobalKey)).Length;
        private static int ActionKeys => TotalKeys - Modifiers.Length;

        // ============================================================================
        // ① 문장의 숫자 ↔ enum 실측
        // ============================================================================

        private static List<int> NumbersFrom(string source, string pattern)
        {
            var found = new List<int>();
            foreach (Match m in Regex.Matches(source, pattern))
            {
                found.Add(int.Parse(m.Groups[1].Value));
            }
            return found;
        }

        [Test]
        public void 문서가_말하는_조합키_수가_실제와_같다()
        {
            string doc = ReadSource("Platform", "IGlobalKeyStateService.cs");
            List<int> claims = NumbersFrom(doc, @"조합키\s*(\d+)");

            Assert.Greater(claims.Count, 0,
                "문서에서 \"조합키 N\" 표현을 하나도 찾지 못했습니다 — 문장이 바뀌었다면 이 검사도 " +
                "함께 고치십시오. 못 찾은 것을 통과로 넘기면 이 잠금이 조용히 죽습니다.");

            foreach (int n in claims)
            {
                Assert.AreEqual(Modifiers.Length, n,
                    $"문서가 조합키를 {n}개라고 적었지만 실제는 {Modifiers.Length}개입니다. " +
                    "이 문장은 심사·프라이버시 문의에 그대로 제출됩니다.");
            }
        }

        [Test]
        public void 문서가_말하는_동작키_수가_실제와_같다()
        {
            string doc = ReadSource("Platform", "IGlobalKeyStateService.cs");
            List<int> claims = NumbersFrom(doc, @"동작키\s*(\d+)");

            Assert.Greater(claims.Count, 0,
                "문서에서 \"동작키 N\" 표현을 하나도 찾지 못했습니다 — 니들이 죽었습니다.");

            foreach (int n in claims)
            {
                Assert.AreEqual(ActionKeys, n,
                    $"문서가 동작키를 {n}개라고 적었지만 실제는 {ActionKeys}개입니다. " +
                    "(2026-09-02에 이 숫자가 4로 남아 4.25배 틀린 채 굳어 있었다.)");
            }
        }

        [Test]
        public void 문서가_말하는_전체_키_수가_실제와_같다()
        {
            string doc = ReadSource("Platform", "IGlobalKeyStateService.cs");
            List<int> claims = NumbersFrom(doc, @"열거된\s*(\d+)\s*개\s*키");

            Assert.Greater(claims.Count, 0,
                "문서에서 \"열거된 N개 키\" 표현을 찾지 못했습니다 — 니들이 죽었습니다.");

            foreach (int n in claims)
            {
                Assert.AreEqual(TotalKeys, n,
                    $"문서가 전체 {n}개라고 적었지만 GlobalKey는 {TotalKeys}개입니다.");
            }
        }

        [Test]
        public void 문서가_말하는_개발게이트_키_수가_실제_배선과_같다()
        {
            string doc = ReadSource("Platform", "IGlobalKeyStateService.cs");
            List<int> claims = NumbersFrom(doc, @"그중\s*(\d+)\s*개는\s*개발\s*게이트");

            Assert.Greater(claims.Count, 0,
                "문서에서 \"그중 N개는 개발 게이트\" 표현을 찾지 못했습니다 — 니들이 죽었습니다.");

            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));
            int gated = Regex.Matches(director, @"dev\s*&&\s*chord\s*&&\s*IsKeyDown").Count;

            Assert.Greater(gated, 0,
                "AppControlDirector에서 개발 게이트가 걸린 키 조회를 한 줄도 찾지 못했습니다 — " +
                "게이트가 사라졌거나(중대) 이 검사의 니들이 죽었습니다. 둘 다 그냥 넘길 수 없습니다.");

            foreach (int n in claims)
            {
                Assert.AreEqual(gated, n,
                    $"문서는 개발 게이트 뒤의 키를 {n}개라고 적었지만 실제 배선은 {gated}개입니다. " +
                    "(같은 형태의 사고: 배너의 \"이 6개\"는 고치고 폴링 주석의 \"6키\"는 안 고쳐, " +
                    "한 파일이 서로 다른 개수를 말했다.)");
            }
        }

        // ============================================================================
        // ② 논거 자체 — 동작키는 조합키 뒤에서만 조회된다
        // ============================================================================

        /// <summary>
        /// ★ 이것이 실제 보장이다. 동작키가 몇 개가 되든, 조합키 3개가 눌리지 않으면 네이티브 조회
        /// 자체가 일어나지 않는다(C# 단락 평가). 이 성질이 깨지면 "사용자가 다른 앱에서 타이핑하는
        /// 내용은 관측될 수 없다"가 거짓이 된다.
        /// </summary>
        [Test]
        public void 동작키_조회는_전부_조합키_게이트_뒤에_있다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));

            var modifierNames = new HashSet<string>();
            foreach (GlobalKey m in Modifiers) modifierNames.Add(m.ToString());

            var reads = Regex.Matches(director, @"^.*IsKeyDown\(GlobalKey\.([A-Za-z0-9_]+)\).*$",
                RegexOptions.Multiline);
            Assert.Greater(reads.Count, 0,
                "AppControlDirector에서 키 조회를 한 줄도 찾지 못했습니다 — 니들이 죽었습니다. " +
                "이 상태로 통과시키면 '게이트가 다 있다'가 아니라 '아무것도 안 쟀다'입니다.");

            int checkedActions = 0;
            foreach (Match m in reads)
            {
                string key = m.Groups[1].Value;
                if (modifierNames.Contains(key)) continue;   // 조합키 자체는 게이트의 구성 요소다.

                checkedActions++;
                StringAssert.Contains("chord &&", m.Value,
                    $"동작키 {key}를 조합키 게이트 없이 조회하고 있습니다:\n  {m.Value.Trim()}\n" +
                    "이 한 줄이 프라이버시 논거를 통째로 무너뜨립니다 — 조합을 누르지 않은 사용자의 " +
                    "타건이 20Hz로 관측됩니다.");
            }

            Assert.Greater(checkedActions, 0,
                "동작키 조회를 한 줄도 검사하지 못했습니다 — 모두 조합키로 분류됐다면 " +
                "Modifiers 정의가 잘못됐습니다.");
        }

        /// <summary>
        /// ★ 양성 대조. 위 검사가 <b>실제로 문다는 것</b>을 같은 판정식으로 증명한다 —
        /// 게이트 없는 줄을 하나 만들어 넣으면 같은 검사가 빨개져야 한다. 이걸 안 하면
        /// "0건 위반"이 "탐지력 0"과 구별되지 않는다.
        /// </summary>
        [Test]
        public void 게이트_없는_줄을_넣으면_같은_검사가_문다()
        {
            const string fake = "            bool z = IsKeyDown(GlobalKey.Z);\n";
            var reads = Regex.Matches(fake, @"^.*IsKeyDown\(GlobalKey\.([A-Za-z0-9_]+)\).*$",
                RegexOptions.Multiline);

            Assert.AreEqual(1, reads.Count, "양성 대조 입력에서 조회 줄을 찾지 못했습니다 — 정규식이 죽었습니다.");
            Assert.IsFalse(reads[0].Value.Contains("chord &&"),
                "게이트가 없는 줄인데 있다고 판정했습니다 — 판정식이 고장났습니다.");
        }
    }
}
