using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// E-9 #3 — <b>돈 낸 사람을 잠그지 않는다</b>: C층 소유 판정의 3상태 계약 (security, 2026-09-02)
    /// ============================================================================
    /// 규범: <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-1(3상태) · §E-2(회수 금지) · §E-3(재시도).
    ///
    /// <para><b>이 감사는 무단 사용을 막는 장치가 아니다. 정반대다.</b>
    /// 스팀 클라이언트가 안 떠 있으면 <c>SteamAPI_Init</c>이 실패하고
    /// (Steamworks <c>steam_api.h</c>: <i>"A running Steam client is required"</i>),
    /// 그때 소박한 구현 <c>bool owned = SteamApps.BIsDlcInstalled(id);</c>는 <b>false</b>로 읽힌다.
    /// 즉 <b>정상 결제한 사용자 전원이 잠긴다.</b> 이 앱은 부팅 자동 실행 + 종일 상주가 정상 사용
    /// 형태라(§E-3), 로그인 직후 스팀이 아직 안 뜬 구간이 <b>매일</b> 발생한다.</para>
    ///
    /// <para>정의서 원칙: <b>정당한 유저를 한 명이라도 잠그는 조치는 무단 사용 열 건보다 비싸다.</b>
    /// 이 앱은 동료지 금고가 아니다.</para>
    ///
    /// ============================================================================
    /// ★ 오늘 이 테스트는 <c>Assert</c>.<c>Ignore</c>로 <b>보류</b>다 — 그리고 그건 의도다
    /// ============================================================================
    /// 2026-09-02 현재 C층(유료 권한) 코드는 <b>0줄</b>이다. 검사할 대상이 없다.
    /// CLAUDE.md 관례: <i>"아직 못 고친 갭은 <c>Assert.Fail</c>이 아니라 <c>Assert.Ignore</c>(사유 포함)로
    /// 남겨 러너에 '건너뜀'으로 계속 보이게 한다 — 잊히지 않게."</i>
    ///
    /// <para><b>역방향 장치 두 개를 서로 독립인 축으로 걸어 둔다</b>(TEAM.md가 기록한 열 번째 거짓 통과
    /// 형태 — "생성기와 검사기가 같은 잘못된 흉내를 공유해 서로를 확인해 주지 못했다"):</para>
    /// <list type="number">
    ///  <item><b>축 A — 타입 이름.</b> 프로덕션에 <c>Entitlement</c>/<c>Ownership</c>/<c>License</c>가
    ///    들어간 타입 선언이 생기면 아래 보류가 <b>스스로 풀려</b> 실검사로 돈다.</item>
    ///  <item><b>축 B — 스토어 SDK 표면.</b>
    ///    <see cref="스토어_SDK가_들어오면_이_경보가_먼저_울린다"/>가 <b>항상 실행</b>되며
    ///    <c>Steamworks</c>/<c>BIsDlcInstalled</c> 같은 식별자가 프로덕션에 나타나는 순간 <b>빨개진다.</b>
    ///    축 A가 눈이 멀어도(예: 타입 이름을 <c>PackAccess</c>로 지어 버리면) 축 B가 대신 알린다.</item>
    /// </list>
    ///
    /// <para>★ 오늘 <b>보류 분기가 실제로 도는 코드인지</b>는 아래 네거티브 컨트롤들이 증명한다 —
    /// 가짜 C층 소스를 같은 판정 함수에 흘려 위반을 잡는지/정상은 통과시키는지 매 실행 확인한다.
    /// 그렇게 하지 않으면 이 파일은 "언젠가 켜질 코드"라는 이름의 <b>한 번도 실행되지 않은 코드</b>가 된다.</para>
    ///
    /// ============================================================================
    /// ★ 다른 감사와의 충돌을 미리 적어 둔다 (C층 배선 라운드가 반드시 만난다)
    /// ============================================================================
    /// <c>OfflineFirstNetworkAuditTests</c>는 프로덕션 소스에서 <c>Steamworks</c>를 <b>금지 니들</b>로
    /// 잡고 있다(네트워크 0 원칙). 즉 C층을 배선하는 라운드는 그 감사도 함께 빨갛게 만든다.
    /// <b>그 실패는 버그가 아니라 설계된 관문이다</b> — 그 라운드는 화이트리스트 항목을 근거와 함께
    /// 등록해야 하고, 그 과정에서 "스팀 SDK가 네트워크를 여는가"를 사람이 한 번 판단하게 된다.
    /// 여기서 미리 적어 두는 이유는, 그날 그 빨강을 <b>귀찮은 오탐으로 오해해 니들만 지우는</b> 일을
    /// 막기 위해서다.
    ///
    /// <para>이 파일은 리플렉션 0줄 · 정규식 0줄이다(활성 빌드 타깃 사각지대 · 한글 낱말 경계 함정 회피).</para>
    /// </summary>
    public sealed class EntitlementFailOpenAuditTests
    {
        private const string LogPrefix = "[C층-페일오픈]";

        /// <summary>축 A — C층 <b>정책 타입</b>을 알아보는 이름 조각.</summary>
        private static readonly string[] PolicyTypeFragments = { "Entitlement", "Ownership", "License" };

        /// <summary>
        /// 축 B — C층 <b>배관</b>을 알아보는 스토어 SDK 식별자.
        /// 축 A와 <b>일부러</b> 겹치지 않게 골랐다. 두 축이 같은 신호를 보면 함께 눈이 먼다.
        /// </summary>
        private static readonly string[] StoreSdkIdentifiers =
        {
            "Steamworks",        // Steamworks.NET 네임스페이스
            "SteamApps",         // ISteamApps
            "BIsDlcInstalled",   // Valve: "not intended for granting in-game items"
            "BIsSubscribedApp",
            "SteamAPI",          // SteamAPI_Init / SteamAPI_RestartAppIfNecessary
            "StoreContext",      // Windows.Services.Store (MS 스토어 애드온)
            "StoreProduct",
            "StoreAppLicense",
        };

        /// <summary>3상태 중 <b>붕괴하면 안 되는</b> 상태 이름.</summary>
        private const string UnknownStateName = "Unknown";

        /// <summary><c>bool</c> 반환을 금지할 함수 이름 조각(§E-1-a).</summary>
        private static readonly string[] BoolBanFragments = { "Entitle", "Owned", "Dlc", "Ownership" };

        // ====================================================================
        // 탐지 — 두 축, 서로 독립
        // ====================================================================

        private struct Surface
        {
            public string File;
            public string Detail;
        }

        /// <summary>축 A: C층 정책 타입 선언.</summary>
        private static List<Surface> DetectPolicyTypes(IEnumerable<(string File, string Stripped)> sources)
        {
            var found = new List<Surface>();
            foreach ((string file, string stripped) in sources)
            {
                foreach (string fragment in PolicyTypeFragments)
                {
                    foreach (string name in EntitlementAuditSource.DeclaredTypeNamesContaining(stripped, fragment))
                    {
                        found.Add(new Surface { File = file, Detail = name });
                    }
                }
            }
            return found;
        }

        /// <summary>축 B: 스토어 SDK 식별자.</summary>
        private static List<Surface> DetectStoreSdk(IEnumerable<(string File, string Stripped)> sources)
        {
            var found = new List<Surface>();
            foreach ((string file, string stripped) in sources)
            {
                foreach (string id in StoreSdkIdentifiers)
                {
                    if (!EntitlementAuditSource.ContainsIdentifier(stripped, id)) continue;
                    found.Add(new Surface { File = file, Detail = id });
                }
            }
            return found;
        }

        // ====================================================================
        // 판정 — 순수 함수. 네거티브 컨트롤이 가짜 C층 소스를 여기에 흘린다.
        // ====================================================================

        /// <summary>
        /// C층 소스가 §E-1 3상태 계약을 지키는가. 어기는 항목을 사람이 읽을 문장으로 돌려준다.
        /// <para>정적으로 <b>잴 수 없는 것</b>은 여기 없다 — §E-2-a(NotOwned와 Unknown의 UI 문구가
        /// 서로 달라야 한다)는 문자열 내용 판정이라 소스 스캔의 사거리 밖이다. 그건 C층 배선 라운드의
        /// 기능 테스트가 맡는다. <b>못 재는 것을 재는 척하지 않는다.</b></para>
        /// </summary>
        private static List<string> Violations(IEnumerable<(string File, string Stripped)> sources)
        {
            var problems = new List<string>();
            var stateEnums = new List<string>();
            var files = new List<(string File, string Stripped)>(sources);

            // ---- V1: 3상태 열거형이 존재하고 Unknown을 가진다 (§E-1) ----
            foreach ((string file, string stripped) in files)
            {
                foreach (string fragment in PolicyTypeFragments)
                {
                    foreach (string name in EntitlementAuditSource.DeclaredTypeNamesContaining(stripped, fragment))
                    {
                        // 상태는 열거형이어야 한다. 같은 이름 조각을 가진 <b>클래스</b>
                        // (예: PackEntitlements 서비스 타입)는 3상태 계약의 대상이 아니다.
                        if (!DeclaresEnum(stripped, name)) continue;

                        string body = EntitlementAuditSource.TypeBodyOrNull(stripped, name);
                        if (body == null) continue;
                        stateEnums.Add(name);

                        if (!EntitlementAuditSource.ContainsIdentifier(body, UnknownStateName))
                        {
                            problems.Add($"{file}: {name}에 '{UnknownStateName}' 상태가 없습니다.\n" +
                                "      §E-1: 상태는 2개가 아니라 3개다. Unknown이 없으면 조회 실패가 " +
                                "NotOwned로 붕괴하고, 그 붕괴가 곧 '돈 낸 사람 잠그기'입니다.");
                        }
                    }
                }
            }

            // ---- V2: bool 반환 소유 판정 금지 (§E-1-a) ----
            foreach ((string file, string stripped) in files)
            {
                int lineNo = 0;
                foreach (string raw in stripped.Replace("\r\n", "\n").Split('\n'))
                {
                    lineNo++;
                    string line = raw.Trim();
                    if (line.IndexOf('(') < 0) continue;
                    if (line.IndexOf(" bool ", StringComparison.Ordinal) < 0) continue;
                    if (!line.StartsWith("public ", StringComparison.Ordinal)
                        && !line.StartsWith("internal ", StringComparison.Ordinal)
                        && !line.StartsWith("private ", StringComparison.Ordinal)
                        && !line.StartsWith("protected ", StringComparison.Ordinal)) continue;

                    int paren = line.IndexOf('(');
                    int start = paren;
                    while (start > 0 && IsIdentifierChar(line[start - 1])) start--;
                    if (start == paren) continue;
                    string name = line.Substring(start, paren - start);

                    foreach (string fragment in BoolBanFragments)
                    {
                        if (name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        problems.Add($"{file}:{lineNo}: bool을 돌려주는 소유 판정 '{name}'.\n" +
                            "      §E-1-a: 엔타이틀먼트 조회 결과를 bool로 반환하는 API를 만들지 않는다. " +
                            "bool로 두면 Unknown이 NotOwned로 붕괴하고, 스팀이 아직 안 뜬 아침마다 " +
                            "정상 구매자가 잠깁니다.");
                        break;
                    }
                }
            }

            // ---- V3: 세 갈래를 전부 명시한다 — switch의 default: 금지 (§E-1-b) ----
            foreach ((string file, string stripped) in files)
            {
                int from = 0;
                while (true)
                {
                    int at = stripped.IndexOf(UnknownStateName, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    from = at + 1;

                    int before = at - 1;
                    int after = at + UnknownStateName.Length;
                    if (before >= 0 && IsIdentifierChar(stripped[before])) continue;
                    if (after < stripped.Length && IsIdentifierChar(stripped[after])) continue;

                    int wStart = Math.Max(0, at - 700);
                    int wEnd = Math.Min(stripped.Length, after + 700);
                    string window = stripped.Substring(wStart, wEnd - wStart);
                    if (window.IndexOf("switch", StringComparison.Ordinal) < 0) continue;
                    if (window.IndexOf("default:", StringComparison.Ordinal) < 0) continue;

                    problems.Add($"{file}:{EntitlementAuditSource.LineNumberAt(stripped, at)}: " +
                        "상태 switch에 default:가 있습니다.\n" +
                        "      §E-1-b: default를 두지 말고 세 갈래를 전부 명시한다. default는 " +
                        "새 상태가 생겼을 때 <b>조용히</b> 한쪽으로 흡수해 버립니다 — " +
                        "이 저장소가 반복해 당한 '조용한 실패'와 같은 계열입니다.");
                    break;
                }
            }

            if (stateEnums.Count == 0)
            {
                problems.Add("C층 표면은 있는데 3상태 열거형을 하나도 찾지 못했습니다.\n" +
                    "      §E-1: EntitlementState { Owned, NotOwned, Unknown } 형태가 이 계약의 뿌리입니다.");
            }
            return problems;
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary><c>enum &lt;name&gt;</c> 선언이 있는가(낱말 경계 확인 — 정규식 없음).</summary>
        private static bool DeclaresEnum(string stripped, string name)
        {
            string needle = "enum " + name;
            int from = 0;
            while (true)
            {
                int at = stripped.IndexOf(needle, from, StringComparison.Ordinal);
                if (at < 0) return false;
                from = at + 1;

                int before = at - 1;
                int after = at + needle.Length;
                if (before >= 0 && IsIdentifierChar(stripped[before])) continue;
                if (after < stripped.Length && IsIdentifierChar(stripped[after])) continue;
                return true;
            }
        }

        private static List<(string File, string Stripped)> ProductionSources()
        {
            var list = new List<(string, string)>();
            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
                list.Add((Path.GetFileName(path), EntitlementAuditSource.StripComments(File.ReadAllText(path))));
            return list;
        }

        // ====================================================================
        // 1. 본론 — 오늘은 보류, C층이 들어오는 날 스스로 실검사로 돈다
        // ====================================================================

        [Test]
        public void C층_소유판정은_Unknown을_NotOwned로_붕괴시키지_않는다()
        {
            List<(string File, string Stripped)> sources = ProductionSources();
            Assert.GreaterOrEqual(sources.Count, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {sources.Count}개밖에 읽지 못했습니다 — " +
                "이 상태의 '보류'는 판단이 아니라 사고입니다.");

            List<Surface> policy = DetectPolicyTypes(sources);
            if (policy.Count == 0)
            {
                Assert.Ignore(
                    $"{LogPrefix} 유료 권한(C층) 코드가 아직 0줄이라 검사할 대상이 없습니다 " +
                    "(2026-09-02 security, ENTITLEMENT_CONTRACT §E-9 #3).\n" +
                    "★ 무엇이 이 보류를 되살리는가 — 두 축이 걸려 있습니다:\n" +
                    "  ① 축 A(자동): 프로덕션에 Entitlement/Ownership/License가 들어간 타입 선언이 " +
                    "생기는 순간, 이 테스트는 보류를 지나 실검사(§E-1 3상태 · §E-1-a bool 금지 · " +
                    "§E-1-b default 금지)로 돕니다. 아무도 켤 필요가 없습니다.\n" +
                    "  ② 축 B(동반): 같은 파일의 [스토어_SDK가_들어오면_이_경보가_먼저_울린다]가 " +
                    "항상 실행되며, Steamworks/BIsDlcInstalled 같은 식별자가 프로덕션에 나타나면 " +
                    "빨개집니다. 축 A가 이름을 못 알아봐도 축 B가 대신 알립니다.\n" +
                    "C층 배선 라운드는 이 두 축 중 하나를 반드시 건드리게 됩니다.");
            }

            var report = new StringBuilder();
            report.Append(LogPrefix).Append(" C층 표면 ").Append(policy.Count).Append("건\n");
            foreach (Surface s in policy) report.Append("  ").Append(s.File).Append('\t').Append(s.Detail).Append('\n');
            Debug.Log(report.ToString());

            List<string> problems = Violations(sources);
            Assert.IsEmpty(problems,
                $"{LogPrefix} C층 소유 판정이 3상태 계약을 어깁니다({problems.Count}건):\n  · " +
                string.Join("\n  · ", problems) + "\n\n" +
                "이 감사는 무단 사용을 막는 장치가 아닙니다 — <b>돈 낸 사람이 실수로 잠기는 것</b>을 " +
                "막는 장치입니다. 스팀이 아직 안 뜬 상태에서 조회가 실패하면 Unknown이어야 하고, " +
                "Unknown에서는 이미 착용 중인 것을 <b>회수하지 않습니다</b>(§E-2).");
        }

        /// <summary>
        /// ★ 위 보류의 <b>역방향 장치(축 B)</b>. 항상 실행되며 보류하지 않는다.
        ///
        /// <para>C층 배관(스토어 SDK)이 프로덕션에 들어오는 순간 <b>빨개진다.</b> 그 빨강의 뜻은
        /// "버그"가 아니라 <b>"위 보류를 이제 해제하라"</b>이다. 실패 메시지가 그 절차를 그대로 적어 둔다.</para>
        ///
        /// <para>이 테스트가 사라지면 <c>TestClaimExpiryAuditTests</c>의 Ignore 명부가 먼저 실패한다
        /// (명부가 동반 테스트의 <b>메서드 선언</b> 실재를 매 실행 확인한다). 즉 이 장치를 조용히
        /// 치울 수 있는 경로가 없다.</para>
        /// </summary>
        [Test]
        public void 스토어_SDK가_들어오면_이_경보가_먼저_울린다()
        {
            List<(string File, string Stripped)> sources = ProductionSources();
            Assert.GreaterOrEqual(sources.Count, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {sources.Count}개밖에 읽지 못했습니다 — " +
                "이 경보는 지금 아무것도 감시하지 못합니다.");
            Assert.IsNotEmpty(StoreSdkIdentifiers,
                $"{LogPrefix} 감시 식별자 목록이 비었습니다(거짓 통과 #5: 빈 목록은 아무것도 재지 않습니다).");

            List<Surface> hits = DetectStoreSdk(sources);
            var lines = new List<string>();
            foreach (Surface s in hits) lines.Add($"  · {s.File} → {s.Detail}");

            Assert.IsEmpty(lines,
                $"{LogPrefix} <b>스토어 SDK가 프로덕션에 들어왔습니다</b>({lines.Count}건):\n" +
                string.Join("\n", lines) + "\n\n" +
                "이건 축하할 실패입니다 — C층 배선이 시작됐다는 뜻입니다. 지금 할 일:\n" +
                "  ① 같은 파일의 [C층_소유판정은_Unknown을_NotOwned로_붕괴시키지_않는다]가 " +
                "보류를 지나 실검사로 도는지 확인하세요(축 A가 타입 이름을 알아봤는가). " +
                "여전히 '건너뜀'이면 PolicyTypeFragments에 실제 타입 이름 조각을 추가하세요.\n" +
                "  ② 그 확인이 끝나면 이 경보 메서드를 지우고, TestClaimExpiryAuditTests의 " +
                "Ignore 명부에서도 해당 항목을 함께 지우세요(명부가 자동 만료로 강제합니다).\n" +
                "  ③ OfflineFirstNetworkAuditTests도 같은 라운드에 빨개집니다 — 그건 설계된 " +
                "관문입니다. 니들을 지우지 말고 화이트리스트에 근거와 함께 등록하세요.");
        }

        // ====================================================================
        // 2. 네거티브 컨트롤 — 오늘 도는 유일한 검증. 보류 분기가 죽은 코드가 되지 않게 한다.
        // ====================================================================

        private static List<(string File, string Stripped)> Fake(string source)
            => new List<(string, string)> { ("Fake.cs", EntitlementAuditSource.StripComments(source)) };

        [Test]
        public void NegativeControl_계약을_지키는_C층_샘플은_위반이_0건이다()
        {
            string good =
                "public enum EntitlementState { Owned, NotOwned, Unknown }\n" +
                "public static class PackEntitlements\n" +
                "{\n" +
                "    public static EntitlementState Query(string packId)\n" +
                "    {\n" +
                "        return EntitlementState.Unknown;\n" +
                "    }\n" +
                "    public static string Describe(EntitlementState state)\n" +
                "    {\n" +
                "        switch (state)\n" +
                "        {\n" +
                "            case EntitlementState.Owned: return \"보유\";\n" +
                "            case EntitlementState.NotOwned: return \"구매하면 열립니다\";\n" +
                "            case EntitlementState.Unknown: return \"지금은 확인할 수 없습니다\";\n" +
                "        }\n" +
                "        return \"\";\n" +
                "    }\n" +
                "}\n";
            List<string> problems = Violations(Fake(good));
            Assert.IsEmpty(problems,
                $"{LogPrefix} 계약을 지키는 샘플에 위반이 잡혔습니다 — 이 감사는 C층 배선 라운드를 " +
                "부당하게 빨갛게 만들고, 그러면 몇 번 만에 꺼집니다.\n  · " +
                string.Join("\n  · ", problems));
        }

        [Test]
        public void NegativeControl_Unknown이_없는_2상태를_잡는다()
        {
            string bad = "public enum EntitlementState { Owned, NotOwned }\n";
            List<string> problems = Violations(Fake(bad));
            Assert.IsNotEmpty(problems,
                $"{LogPrefix} Unknown이 없는 2상태를 통과시켰습니다. 그 붕괴가 이 계약이 막으려는 " +
                "사고 그 자체입니다(스팀 미기동 → false → 정상 구매자 전원 잠김).");
        }

        [Test]
        public void NegativeControl_bool을_돌려주는_소유판정을_잡는다()
        {
            string bad =
                "public enum EntitlementState { Owned, NotOwned, Unknown }\n" +
                "public static class Gate\n" +
                "{\n" +
                "    public static bool IsDlcOwned(string id) => false;\n" +
                "}\n";
            List<string> problems = Violations(Fake(bad));
            Assert.IsNotEmpty(problems,
                $"{LogPrefix} bool 반환 소유 판정을 통과시켰습니다(§E-1-a).");
        }

        [Test]
        public void NegativeControl_상태_switch의_default를_잡는다()
        {
            string bad =
                "public enum EntitlementState { Owned, NotOwned, Unknown }\n" +
                "public static class Gate\n" +
                "{\n" +
                "    public static string Describe(EntitlementState s)\n" +
                "    {\n" +
                "        switch (s)\n" +
                "        {\n" +
                "            case EntitlementState.Owned: return \"보유\";\n" +
                "            case EntitlementState.Unknown: return \"확인 불가\";\n" +
                "            default: return \"미보유\";\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
            List<string> problems = Violations(Fake(bad));
            Assert.IsNotEmpty(problems,
                $"{LogPrefix} 상태 switch의 default:를 통과시켰습니다(§E-1-b). " +
                "default는 Unknown을 조용히 NotOwned 쪽으로 흡수합니다.");
        }

        [Test]
        public void NegativeControl_축B_스캐너는_스토어_식별자를_실제로_찾아낸다()
        {
            // ★ 양성 대조. 이것이 없으면 위 경보의 "0건"은 능력을 증명하지 못한 0건이다
            //   (이 저장소 실제 사고: strings|grep이 .NET UTF-16 문자열에 대해 탐지력이 애초에 0이었다).
            string fake = "using Steamworks;\n" +
                          "public static class Probe\n" +
                          "{\n" +
                          "    public static void Init() { SteamAPI.Init(); }\n" +
                          "}\n";
            List<Surface> hits = DetectStoreSdk(Fake(fake));
            Assert.IsNotEmpty(hits,
                $"{LogPrefix} 축 B 스캐너가 명백한 스토어 SDK 사용을 놓쳤습니다 — " +
                "그러면 위 경보의 '0건'은 '없다'가 아니라 '못 본다'입니다.");
        }

        [Test]
        public void NegativeControl_주석_속_스토어_언급은_경보가_아니다()
        {
            string fake = "public static class Plan\n" +
                          "{\n" +
                          "    /// <summary>훗날 Steamworks로 BIsDlcInstalled를 부를 자리.</summary>\n" +
                          "    public static void TODO() { }\n" +
                          "}\n";
            List<Surface> hits = DetectStoreSdk(Fake(fake));
            Assert.IsEmpty(hits,
                $"{LogPrefix} 주석 속 계획을 배선으로 셌습니다. 이 저장소의 프로덕션·문서 주석에는 " +
                "스팀 이야기가 이미 여러 군데 있어, 주석을 세면 이 경보는 첫날부터 빨간 채로 " +
                "방치됩니다 — 방치된 경보는 없는 경보입니다.");
        }

        [Test]
        public void NegativeControl_축A_스캐너는_C층_타입_선언을_찾아낸다()
        {
            string fake = "public enum PackEntitlementState { Owned, NotOwned, Unknown }\n";
            List<Surface> hits = DetectPolicyTypes(Fake(fake));
            Assert.IsNotEmpty(hits,
                $"{LogPrefix} 축 A 스캐너가 C층 타입 선언을 놓쳤습니다 — " +
                "그러면 위 보류는 C층이 들어와도 영원히 '건너뜀'으로 남습니다. " +
                "이 저장소의 Ignore 관례가 정확히 그것을 막으려고 만들어졌습니다.");
        }
    }
}
