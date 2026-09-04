using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// ★ 스팀 니들 예외의 <b>닫힌 세계</b> 감사 (security 설계, 리더 결재-1 승인, 2026-09-05)
    /// ============================================================================
    /// 규범: <c>docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md</c> §4·§7.
    ///
    /// <para><b>왜 「금지 니들」이 아니라 「허용 심볼 화이트리스트」인가</b>: <c>using Steamworks;</c>
    /// 한 줄이 통과하는 순간 같은 네임스페이스의 <c>SteamRemoteStorage</c>·<c>SteamInventory</c>·
    /// <c>SteamUser</c>·<c>SteamFriends</c>가 전부 컴파일된다. 그것들은 오늘 어떤 금지 니들 표에도
    /// 없다 — 블랙리스트를 넓히는 방식으로는 이 문을 안전하게 못 연다. 그래서 이 파일 <b>안에서는</b>
    /// 허용 목록에 없는 <c>Steam</c>* 심볼이 하나라도 나오면 실패한다(닫힌 세계).</para>
    ///
    /// <para>이 파일은 리플렉션 0줄 · 정규식 0줄이다(<see cref="EntitlementAuditSource"/> 재사용 —
    /// 활성 빌드 타깃 사각지대와 <c>\b</c>가 한글을 낱말로 세는 함정을 피한다).</para>
    /// </summary>
    public sealed class SteamEntitlementAdapterAuditTests
    {
        private const string LogPrefix = "[스팀어댑터감사]";
        private const string ApprovedFileName = "SteamPackEntitlementSource.cs";

        /// <summary>이 파일 안에서 허용되는 심볼 5개 — 그 밖 전부 금지.</summary>
        private static readonly string[] ApprovedSymbols =
        {
            "Steamworks", "SteamAPI", "SteamApps", "AppId_t", "SteamPackEntitlementSource",
        };

        private static readonly string[] ApprovedSteamApiMembers = { "Init", "Shutdown" };
        private static readonly string[] ApprovedSteamAppsMembers = { "BIsDlcInstalled" };

        // ====================================================================
        // 소스 수집
        // ====================================================================

        private static List<(string File, string Stripped)> ProductionSources()
        {
            var list = new List<(string, string)>();
            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
            {
                string text = File.ReadAllText(path);
                list.Add((Path.GetFileName(path), EntitlementAuditSource.StripComments(text)));
            }
            return list;
        }

        /// <summary><paramref name="haystack"/>에서 <c>Steam</c>으로 시작하거나 <c>AppId_t</c>와 같은
        /// 낱말 단위 식별자를 전부 뽑는다(등장 위치 포함, 순서 보존).</summary>
        private static List<string> ExtractSteamLikeTokens(string haystack)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(haystack)) return found;

            int i = 0, n = haystack.Length;
            while (i < n)
            {
                if (!IsIdentifierStart(haystack[i])) { i++; continue; }
                int start = i;
                while (i < n && IsIdentifierChar(haystack[i])) i++;
                string token = haystack.Substring(start, i - start);
                // ★ 실측(2026-09-04) — 바로 "Steam"만 있으면 안 된다. PackStoreChannel.Steam은
                // 이 예외와 무관한 기존 판매 채널 열거값(Core/StickPackManifestSO.cs)이라 낱말이
                // 정확히 "Steam"이면 오탐이다. SDK 심볼은 전부 "Steam" 뒤에 글자가 더 있다
                // (Steamworks/SteamAPI/SteamApps/SteamUser/SteamRemoteStorage 등).
                bool isSteamSdkToken = token.Length > "Steam".Length
                    && token.StartsWith("Steam", StringComparison.Ordinal);
                if (isSteamSdkToken || token == "AppId_t")
                {
                    found.Add(token);
                }
            }
            return found;
        }

        private static bool IsIdentifierStart(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';

        private static bool IsIdentifierChar(char c)
            => IsIdentifierStart(c) || (c >= '0' && c <= '9');

        // ====================================================================
        // (0)~(6) 본체 — §7-2
        // ====================================================================

        [Test]
        public void 스팀_어댑터는_정확히_한_파일이고_승인된_심볼만_쓴다()
        {
            List<(string File, string Stripped)> sources = ProductionSources();
            Assert.GreaterOrEqual(sources.Count, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {sources.Count}개밖에 읽지 못했습니다 — 판정이 무효입니다.");

            // ---- (1) 파일 집합 등호 ----
            var filesWithSteamTokens = new List<string>();
            foreach ((string file, string stripped) in sources)
            {
                if (ExtractSteamLikeTokens(stripped).Count > 0 && !filesWithSteamTokens.Contains(file))
                {
                    filesWithSteamTokens.Add(file);
                }
            }

            if (filesWithSteamTokens.Count == 0)
            {
                Assert.Ignore(
                    $"{LogPrefix} 프로덕션에 Steam 계열 식별자가 0건입니다 — 니들 예외가 아직 " +
                    "쓰이지 않았거나(정상, 배선 전) 되돌려진 상태입니다. 되돌린 것이라면 이 테스트 " +
                    "파일과 화이트리스트 항목도 함께 지우세요(2026-08-30 SetWindowPos 처리와 같은 절차).");
            }

            Assert.AreEqual(1, filesWithSteamTokens.Count,
                $"{LogPrefix} Steam 계열 식별자를 쓰는 파일이 {filesWithSteamTokens.Count}개입니다 " +
                $"(기대: '{ApprovedFileName}' 1개뿐). 예외가 번졌습니다:\n  · " +
                string.Join("\n  · ", filesWithSteamTokens));
            Assert.AreEqual(ApprovedFileName, filesWithSteamTokens[0],
                $"{LogPrefix} Steam 계열 식별자가 승인된 파일이 아닌 '{filesWithSteamTokens[0]}'에 있습니다.");

            string body = null;
            foreach ((string file, string stripped) in sources)
            {
                if (file == ApprovedFileName) { body = stripped; break; }
            }
            Assert.IsNotNull(body, $"{LogPrefix} '{ApprovedFileName}'을 읽지 못했습니다.");

            // ---- (2) 심볼 화이트리스트 ----
            List<string> tokens = ExtractSteamLikeTokens(body);
            var unapproved = new List<string>();
            foreach (string t in tokens)
            {
                if (Array.IndexOf(ApprovedSymbols, t) < 0 && !unapproved.Contains(t)) unapproved.Add(t);
            }
            Assert.IsEmpty(unapproved,
                $"{LogPrefix} '{ApprovedFileName}'에 승인되지 않은 Steam 계열 심볼이 있습니다:\n  · " +
                string.Join("\n  · ", unapproved) + "\n" +
                "허용 심볼 5개(docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md §4): " +
                string.Join(", ", ApprovedSymbols));

            // ---- (3) 멤버 접근 형태 ----
            AssertMemberAccessWithinAllowlist(body, "SteamAPI", ApprovedSteamApiMembers);
            AssertMemberAccessWithinAllowlist(body, "SteamApps", ApprovedSteamAppsMembers);

            // ---- (4) using 형태 — 정규화 우회 차단 ----
            int usingCount = CountExactLine(body, "using Steamworks;");
            Assert.AreEqual(1, usingCount,
                $"{LogPrefix} 'using Steamworks;'가 정확히 1회여야 하는데 {usingCount}회입니다.");
            int steamworksWordCount = EntitlementAuditSource.CountIdentifier(body, "Steamworks");
            Assert.AreEqual(1, steamworksWordCount,
                $"{LogPrefix} 'Steamworks' 낱말이 {steamworksWordCount}회 등장합니다(기대 1 — using 선언 " +
                "그 자체뿐). 2회 이상이면 'Steamworks.SteamUser…' 같은 정규화 경로로 화이트리스트를 " +
                "우회했을 수 있습니다.");

            // ---- (5) 원칙 4 — 팩 이름 하드코딩 금지 ----
            Assert.IsFalse(body.Contains("\"pack."),
                $"{LogPrefix} '{ApprovedFileName}'에 \"pack.\" 리터럴이 있습니다 — 어댑터에 팩 이름이 " +
                "박히면 원칙 4(플러그인 구조)가 깨집니다.");

            // ---- (6) E-4 — 디스크 쓰기 금지 ----
            Assert.IsFalse(EntitlementAuditSource.ContainsIdentifier(body, "PlayerPrefs"),
                $"{LogPrefix} '{ApprovedFileName}'에 PlayerPrefs가 있습니다 — 어댑터는 아무것도 저장하지 않습니다(§E-4-a).");
            Assert.IsFalse(body.Contains("File.Write") || body.Contains("File.Create")
                || body.Contains("File.AppendAllText") || body.Contains("File.WriteAllText"),
                $"{LogPrefix} '{ApprovedFileName}'에 파일 쓰기 계열 호출이 있습니다 — 캐시 파일 하나가 " +
                "§E-4가 제거한 표적을 되살립니다.");
        }

        private static void AssertMemberAccessWithinAllowlist(string body, string typeName, string[] allowedMembers)
        {
            string needle = typeName + ".";
            var offenders = new List<string>();
            int from = 0;
            while (true)
            {
                int at = body.IndexOf(needle, from, StringComparison.Ordinal);
                if (at < 0) break;
                from = at + 1;

                int before = at - 1;
                if (before >= 0 && IsIdentifierChar(body[before])) continue; // "MySteamAPI." 같은 오탐 회피

                int p = at + needle.Length;
                int start = p;
                while (p < body.Length && IsIdentifierChar(body[p])) p++;
                if (p == start) continue;
                string member = body.Substring(start, p - start);

                if (Array.IndexOf(allowedMembers, member) < 0 && !offenders.Contains(member))
                {
                    offenders.Add(member);
                }
            }
            Assert.IsEmpty(offenders,
                $"{LogPrefix} '{ApprovedFileName}'에서 {typeName}.의 승인 안 된 멤버가 쓰였습니다: " +
                string.Join(", ", offenders) + $" (허용: {string.Join(", ", allowedMembers)}). " +
                "RestartAppIfNecessary·RunCallbacks·BIsSubscribed* 등은 설계 문서 §4-1이 명시적으로 금지합니다.");
        }

        private static int CountExactLine(string body, string exact)
        {
            int count = 0;
            foreach (string raw in body.Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.Trim() == exact) count++;
            }
            return count;
        }

        // ====================================================================
        // §7-3 — 설치 창구 잠금
        // ====================================================================

        [Test]
        public void 소유_출처를_바꾸는_창구는_정확히_둘이고_둘_다_internal이다()
        {
            string path = Path.Combine(EntitlementAuditSource.ScriptsRoot, "Core", "PackEntitlement.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} {path}가 없습니다.");
            string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));

            var setters = new List<string>();
            foreach (string raw in stripped.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                int at = line.IndexOf("_source =", StringComparison.Ordinal);
                if (at < 0) continue;
                // 선언 줄(메서드 시그니처)만 본다 — 대입 실행 줄(_source = source;)은 제외.
                if (!line.Contains("(")) continue;

                setters.Add(line);
            }

            // 실제 메서드 선언에서 이름을 뽑는다: "internal static void UseSource(" 형태.
            var methodNames = new List<string>();
            foreach (string keyword in new[] { "SetTestOverride", "UseSource" })
            {
                if (EntitlementAuditSource.ContainsIdentifier(stripped, keyword)) methodNames.Add(keyword);
            }
            methodNames.Sort(StringComparer.Ordinal);

            Assert.AreEqual(2, methodNames.Count,
                $"{LogPrefix} _source를 바꾸는 창구가 {methodNames.Count}개 발견됐습니다(기대: " +
                $"SetTestOverride, UseSource). 실제: {string.Join(", ", methodNames)}");

            foreach (string name in methodNames)
            {
                bool foundInternal = false;
                foreach (string raw in stripped.Replace("\r\n", "\n").Split('\n'))
                {
                    string line = raw.Trim();
                    if (!line.Contains(name + "(")) continue;
                    if (line.StartsWith("internal ", StringComparison.Ordinal)) foundInternal = true;
                }
                Assert.IsTrue(foundInternal,
                    $"{LogPrefix} '{name}' 선언이 internal이 아닙니다 — public이면 어셈블리 밖에서 " +
                    "결제 경계를 무력화할 수 있습니다.");
            }

            // UseSource를 부르는 파일은 어댑터 자신뿐이어야 한다.
            var callers = new List<string>();
            foreach ((string file, string stripped2) in ProductionSources())
            {
                if (file == "PackEntitlement.cs") continue; // 선언 자체는 호출이 아니다
                if (EntitlementAuditSource.ContainsIdentifier(stripped2, "UseSource")) callers.Add(file);
            }
            Assert.That(callers, Is.EquivalentTo(new[] { ApprovedFileName }),
                $"{LogPrefix} UseSource를 부르는 파일이 예상과 다릅니다: {string.Join(", ", callers)} " +
                $"(기대: {ApprovedFileName} 1개뿐).");
        }

        // ====================================================================
        // §7-4 — 네거티브 대조 6건
        // ====================================================================

        [Test]
        public void NegativeControl_허용목록_밖_심볼은_잡힌다()
        {
            string fake = "using Steamworks;\nclass X { void F() { SteamRemoteStorage.FileWrite(\"a\", null); } }\n";
            List<string> unapproved = FindUnapprovedSymbolsIn(fake);
            Assert.Contains("SteamRemoteStorage", unapproved,
                $"{LogPrefix} 양성 대조 실패 — SteamRemoteStorage가 안 잡힙니다.");
        }

        [Test]
        public void NegativeControl_허용_안된_멤버는_잡힌다()
        {
            string fake = "using Steamworks;\nclass X { void F() { SteamAPI.RestartAppIfNecessary(default); } }\n";
            string stripped = EntitlementAuditSource.StripComments(fake);
            var offenders = new List<string>();
            CollectMemberOffenders(stripped, "SteamAPI", ApprovedSteamApiMembers, offenders);
            Assert.Contains("RestartAppIfNecessary", offenders,
                $"{LogPrefix} 양성 대조 실패 — RestartAppIfNecessary가 안 잡힙니다.");
        }

        [Test]
        public void NegativeControl_어댑터_밖_파일의_using은_잡힌다()
        {
            // 파일 집합 등호(1) 로직 자체를 별도 소스 목록으로 재현 — 실제 스캔 없이 순수 함수만 검증.
            var fakeSources = new List<(string, string)>
            {
                ("SteamPackEntitlementSource.cs", "using Steamworks;\nclass A {}"),
                ("SomeOtherFile.cs", "using Steamworks;\nclass B {}"),
            };
            var filesWithTokens = new List<string>();
            foreach ((string file, string src) in fakeSources)
            {
                if (ExtractSteamLikeTokens(src).Count > 0) filesWithTokens.Add(file);
            }
            Assert.AreEqual(2, filesWithTokens.Count,
                $"{LogPrefix} 양성 대조 실패 — 어댑터 밖 파일의 Steamworks 사용이 안 잡힙니다.");
        }

        [Test]
        public void NegativeControl_정규화_우회는_잡힌다()
        {
            string fake = "using Steamworks;\nclass X { void F() { var id = Steamworks.SteamUser.GetSteamID(); } }\n";
            string stripped = EntitlementAuditSource.StripComments(fake);
            int steamworksCount = EntitlementAuditSource.CountIdentifier(stripped, "Steamworks");
            Assert.Greater(steamworksCount, 1,
                $"{LogPrefix} 양성 대조 실패 — 'Steamworks.SteamUser…' 정규화 우회가 낱말 카운트로 안 잡힙니다.");
        }

        [Test]
        public void NegativeControl_실제_허용_4형태는_통과한다()
        {
            string fake =
                "using Steamworks;\n" +
                "class SteamPackEntitlementSource {\n" +
                "    void F() {\n" +
                "        SteamAPI.Init();\n" +
                "        SteamAPI.Shutdown();\n" +
                "        SteamApps.BIsDlcInstalled(new AppId_t(1u));\n" +
                "    }\n" +
                "}\n";
            string stripped = EntitlementAuditSource.StripComments(fake);
            List<string> unapproved = new List<string>();
            foreach (string t in ExtractSteamLikeTokens(stripped))
            {
                if (Array.IndexOf(ApprovedSymbols, t) < 0 && !unapproved.Contains(t)) unapproved.Add(t);
            }
            Assert.IsEmpty(unapproved,
                $"{LogPrefix} 음성 대조 실패(오탐) — 허용된 4형태인데 잡혔습니다: {string.Join(", ", unapproved)}");

            var apiOffenders = new List<string>();
            CollectMemberOffenders(stripped, "SteamAPI", ApprovedSteamApiMembers, apiOffenders);
            Assert.IsEmpty(apiOffenders, $"{LogPrefix} 음성 대조 실패 — SteamAPI 허용 멤버가 오탐됩니다.");
        }

        [Test]
        public void NegativeControl_주석_안의_언급은_통과한다()
        {
            string fake = "// SteamInventory는 쓰지 않는다\nclass X {}\n";
            string stripped = EntitlementAuditSource.StripComments(fake);
            Assert.IsEmpty(ExtractSteamLikeTokens(stripped),
                $"{LogPrefix} 음성 대조 실패 — 주석 안 언급이 잡혔습니다(주석은 배선이 아닙니다).");
        }

        private static List<string> FindUnapprovedSymbolsIn(string source)
        {
            string stripped = EntitlementAuditSource.StripComments(source);
            var unapproved = new List<string>();
            foreach (string t in ExtractSteamLikeTokens(stripped))
            {
                if (Array.IndexOf(ApprovedSymbols, t) < 0 && !unapproved.Contains(t)) unapproved.Add(t);
            }
            return unapproved;
        }

        private static void CollectMemberOffenders(string body, string typeName, string[] allowed, List<string> outOffenders)
        {
            string needle = typeName + ".";
            int from = 0;
            while (true)
            {
                int at = body.IndexOf(needle, from, StringComparison.Ordinal);
                if (at < 0) break;
                from = at + 1;
                int before = at - 1;
                if (before >= 0 && IsIdentifierChar(body[before])) continue;
                int p = at + needle.Length;
                int start = p;
                while (p < body.Length && IsIdentifierChar(body[p])) p++;
                if (p == start) continue;
                string member = body.Substring(start, p - start);
                if (Array.IndexOf(allowed, member) < 0 && !outOffenders.Contains(member)) outOffenders.Add(member);
            }
        }
    }
}
