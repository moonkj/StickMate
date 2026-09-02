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
    /// E-9 #2 — <b>QA 해금 스위치는 유료 경계를 넘지 않는다</b> (security, 2026-09-02)
    /// ============================================================================
    /// 규범: <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-6(문법적 격리).
    /// 짝: <c>EquipmentDebugUnlockReleaseGateTests</c>(스위치가 <b>릴리스에서 꺼지는가</b>) —
    /// 그쪽은 <b>값</b>을 재고, 이쪽은 <b>도달 범위</b>를 잰다. 둘 다 있어야 한다:
    /// 스위치가 꺼져도 <b>C층 판정에 배선되어 있으면</b> 언젠가 켜지는 날 DLC가 함께 열리고,
    /// 배선이 없어도 릴리스에서 켜져 있으면 성장 요소가 통째로 무의미해진다.
    ///
    /// ============================================================================
    /// 왜 이것이 "재화 치트"가 아니라 <b>유료 경계</b> 문제인가
    /// ============================================================================
    /// 이 앱은 네트워크 0 · 싱글플레이다. 세이브를 고쳐 동전을 올리면 <b>잃는 사람은 자기 자신뿐</b>이라
    /// 막을 가치가 낮다(§E-8). 그러나 <c>UnlockAll</c>이 <b>C층 판정</b>에까지 걸려 있으면
    /// 환경변수 한 줄(<c>STICKMATE_UNLOCK_ALL=1</c>)이 <b>결제 경계</b>를 넘고,
    /// 그때 잃는 사람은 유저가 아니라 <b>개발자(매출)</b>다.
    /// 정의서 위협표에서 ★가 붙은 유일한 칸이 정확히 여기다.
    ///
    /// ============================================================================
    /// 오늘의 실측(2026-09-02) — 이 숫자가 기대값이다
    /// ============================================================================
    /// 선언 파일 1개, 그 <b>바깥</b> 참조는 <b>정확히 2파일</b>
    /// (<c>ItemCatalog.cs</c>의 <c>ItemCatalogEntry.IsOwned</c> / <c>EquipmentModel.cs</c>의
    /// <c>IsItemOwned</c>). 둘 다 A·B층 보유 판정이고, 둘 다 <b><c>UnlockAll</c> 한 멤버만</b> 읽는다.
    ///
    /// <para>★ 파일 이름을 <b>기대값</b>으로만 쓰고 <b>탐지</b>에는 쓰지 않는다 — 선언 파일은
    /// 이름이 아니라 <c>class</c> 선언으로 찾는다. 이름으로 찾으면 리팩터링 한 번에
    /// 스캔이 <b>조용히 0건</b>이 되고, 그 0건은 "위반 없음"과 똑같이 생겼다.</para>
    ///
    /// <para>리플렉션 0줄(활성 빌드 타깃 사각지대 회피) · 정규식 0줄(.NET <c>\b</c>가 한글을 낱말로
    /// 세는 함정 회피 — 이 저장소가 그 함정으로 참조 46%를 놓친 적이 있다).</para>
    /// </summary>
    public sealed class UnlockSwitchScopeAuditTests
    {
        private const string LogPrefix = "[해금스위치범위]";

        /// <summary>감사 대상 스위치 타입 이름. <b>선언이 실재하는지</b>를 같은 테스트가 확인한다
        /// (존재 단언 — 썩으면 조용히 초록이 되는 대신 시끄럽게 빨개진다).</summary>
        private const string SwitchTypeName = "EquipmentDebugUnlock";

        /// <summary>선언 파일 밖에서 읽어도 되는 <b>유일한</b> 멤버.</summary>
        private const string AllowedMember = "UnlockAll";

        /// <summary>
        /// 선언 파일 밖 참조가 허용된 파일. <b>기대값</b>이지 탐지 수단이 아니다.
        /// 여기 없는 파일이 스위치를 읽으면 실패한다 — C층 배선이 이 목록에 몰래 끼지 못하게 하는 잠금.
        /// </summary>
        private static readonly string[] AllowedReferrerFiles =
        {
            "ItemCatalog.cs",
            "EquipmentModel.cs",
        };

        /// <summary>
        /// 스위치 참조 주변에서 <b>원래 규칙이 함께 살아 있는지</b> 확인하는 앵커.
        /// <para>§E-6-a의 뜻은 "<c>UnlockAll</c>은 합집합의 한 가지일 뿐, 판정 전체가 아니다"이다.
        /// 즉 스위치가 꺼지면 <b>원래 요구 레벨 규칙으로 돌아와야</b> 한다. 그 규칙이 같은 자리에
        /// 남아 있는지를 이 앵커로 잰다.</para>
        /// <para>★ 이건 <b>존재 단언</b>이다. 요구 레벨 규칙의 이름이 바뀌면 이 감사는 <b>빨개진다</b>
        /// (조용히 초록이 되지 않는다). 그때 앵커를 갱신하는 것이 올바른 조치다.</para>
        /// </summary>
        private const string OwnershipRuleAnchor = "Level";

        /// <summary>앵커를 찾을 때 참조 위치 앞뒤로 보는 글자 수. 넉넉히 잡는다 —
        /// 좁게 잡으면 정직한 리팩터링(예: <c>if (UnlockAll) return true;</c> + 그 아래 레벨 검사)에
        /// 빨개져서, 감사가 몇 번 만에 꺼진다.</summary>
        private const int AnchorWindow = 320;

        // ====================================================================
        // 스캔 — 순수 함수. 네거티브 컨트롤이 같은 함수에 가짜 소스를 흘린다.
        // ====================================================================

        private struct Reference
        {
            public string File;
            public int Line;
            public string Member;
            public bool RuleAnchorNearby;
        }

        /// <summary>
        /// 주석 제거본에서 <paramref name="typeName"/>의 <b>낱말 단위</b> 등장을 전부 찾아
        /// 뒤따르는 멤버 이름과 "원래 규칙 앵커가 근처에 있는가"를 함께 기록한다.
        /// </summary>
        private static List<Reference> FindReferences(string fileLabel, string strippedSource, string typeName)
        {
            var hits = new List<Reference>();
            int from = 0;
            while (true)
            {
                int at = strippedSource.IndexOf(typeName, from, StringComparison.Ordinal);
                if (at < 0) break;
                from = at + 1;

                int before = at - 1;
                int after = at + typeName.Length;
                bool leftOk = before < 0 || !IsIdentifierChar(strippedSource[before]);
                bool rightOk = after >= strippedSource.Length || !IsIdentifierChar(strippedSource[after]);
                if (!leftOk || !rightOk) continue;

                // 선언 자체(`class EquipmentDebugUnlock`)는 참조가 아니다.
                int lineStart = strippedSource.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
                string head = strippedSource.Substring(lineStart, Math.Max(0, at - lineStart));
                if (head.IndexOf("class ", StringComparison.Ordinal) >= 0) continue;

                string member = "(멤버 없음)";
                int p = after;
                while (p < strippedSource.Length && (strippedSource[p] == ' ' || strippedSource[p] == '\t')) p++;
                if (p < strippedSource.Length && strippedSource[p] == '.')
                {
                    p++;
                    int start = p;
                    while (p < strippedSource.Length && IsIdentifierChar(strippedSource[p])) p++;
                    if (p > start) member = strippedSource.Substring(start, p - start);
                }

                int wStart = Math.Max(0, at - AnchorWindow);
                int wEnd = Math.Min(strippedSource.Length, after + AnchorWindow);
                string window = strippedSource.Substring(wStart, wEnd - wStart);

                hits.Add(new Reference
                {
                    File = fileLabel,
                    Line = EntitlementAuditSource.LineNumberAt(strippedSource, at),
                    Member = member,
                    RuleAnchorNearby = window.IndexOf(OwnershipRuleAnchor, StringComparison.Ordinal) >= 0,
                });
            }
            return hits;
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary><paramref name="typeName"/>을 선언하는 프로덕션 파일 전부(정상은 1개).</summary>
        private static List<string> DeclaringFiles(string typeName)
        {
            var found = new List<string>();
            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
            {
                string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));
                if (EntitlementAuditSource.DeclaresType(stripped, typeName)) found.Add(path);
            }
            return found;
        }

        // ====================================================================
        // 1. 본론 — 도달 범위
        // ====================================================================

        [Test]
        public void 해금_스위치는_선언파일_밖에서_정확히_보유판정_두_곳만_읽는다()
        {
            string[] production = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(production.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {production.Length}개밖에 읽지 못했습니다 — " +
                "아래 '참조 2곳뿐'은 측정이 아니라 착시가 됩니다.");

            List<string> declaring = DeclaringFiles(SwitchTypeName);
            Assert.AreEqual(1, declaring.Count,
                $"{LogPrefix} {SwitchTypeName} 선언 파일이 {declaring.Count}개입니다(기대 1개). " +
                "0개라면 타입 이름이 바뀐 것이고, 그러면 이 감사는 그 순간부터 아무것도 보고 있지 " +
                "않습니다 — 이름을 고치기 전에는 '참조 0건'을 믿으면 안 됩니다.\n" +
                "찾은 것: " + string.Join(", ", declaring));

            string declaringFile = Path.GetFileName(declaring[0]);
            var references = new List<Reference>();
            var report = new StringBuilder();
            report.Append(LogPrefix).Append(' ').Append(SwitchTypeName)
                  .Append(" 선언=").Append(declaringFile).Append('\n');

            foreach (string path in production)
            {
                if (string.Equals(path, declaring[0], StringComparison.Ordinal)) continue;
                string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));
                references.AddRange(FindReferences(Path.GetFileName(path), stripped, SwitchTypeName));
            }

            foreach (Reference r in references)
            {
                report.Append("  ").Append(r.File).Append(':').Append(r.Line)
                      .Append('\t').Append(SwitchTypeName).Append('.').Append(r.Member)
                      .Append(r.RuleAnchorNearby ? "\t(원래 규칙 근처)" : "\t★원래 규칙 없음")
                      .Append('\n');
            }
            Debug.Log(report.ToString());

            var referrerFiles = new List<string>();
            foreach (Reference r in references)
                if (!referrerFiles.Contains(r.File)) referrerFiles.Add(r.File);
            referrerFiles.Sort(StringComparer.Ordinal);

            var expected = new List<string>(AllowedReferrerFiles);
            expected.Sort(StringComparer.Ordinal);

            var unexpected = new List<string>();
            foreach (string f in referrerFiles) if (!expected.Contains(f)) unexpected.Add(f);
            var missing = new List<string>();
            foreach (string f in expected) if (!referrerFiles.Contains(f)) missing.Add(f);

            Assert.IsEmpty(unexpected,
                $"{LogPrefix} 허용되지 않은 파일이 QA 해금 스위치를 읽습니다: {string.Join(", ", unexpected)}\n" +
                "★ 그 파일이 <b>유료 권한(C층) 판정</b>이라면 이건 결제 경계가 환경변수 한 줄로 " +
                "열린다는 뜻입니다(§E-6-a/§E-6-b: C층 판정 함수의 본문과 호출 그래프 어디에도 " +
                $"{SwitchTypeName}이 나타나지 않는다).\n" +
                "A·B층(무료 레벨 해금)을 하나 더 늘린 것이라면 위 AllowedReferrerFiles에 추가하되, " +
                "추가하기 전에 그 자리가 정말 무료 경계 안인지 확인하세요.");

            Assert.IsEmpty(missing,
                $"{LogPrefix} 있어야 할 참조가 사라졌습니다: {string.Join(", ", missing)}\n" +
                "스위치를 실제로 걷어낸 것이라면 AllowedReferrerFiles에서도 지우세요. " +
                "그냥 두면 이 감사는 <b>존재하지 않는 것</b>을 지키게 되고, 그 초록은 아무 뜻이 없습니다.");

            var wrongMember = new List<string>();
            foreach (Reference r in references)
            {
                if (string.Equals(r.Member, AllowedMember, StringComparison.Ordinal)) continue;
                wrongMember.Add($"  · {r.File}:{r.Line} → {SwitchTypeName}.{r.Member}");
            }
            Assert.IsEmpty(wrongMember,
                $"{LogPrefix} 선언 파일 밖에서 {AllowedMember} 외의 멤버를 읽습니다:\n" +
                string.Join("\n", wrongMember) + "\n" +
                "특히 테스트 강제값(SetTestOverride 계열)을 프로덕션이 부르면, 스위치가 " +
                "릴리스에서 꺼진다는 보장(EquipmentDebugUnlockReleaseGateTests)이 우회됩니다.");

            var ruleGone = new List<string>();
            foreach (Reference r in references)
            {
                if (r.RuleAnchorNearby) continue;
                ruleGone.Add($"  · {r.File}:{r.Line}");
            }
            Assert.IsEmpty(ruleGone,
                $"{LogPrefix} 스위치 참조 근처({AnchorWindow}자)에 원래 규칙 앵커 '{OwnershipRuleAnchor}'가 " +
                "없습니다:\n" + string.Join("\n", ruleGone) + "\n" +
                "§E-6-a: UnlockAll은 합집합의 <b>한 가지</b>여야 하고 판정 전체일 수 없습니다. " +
                "스위치가 꺼지면 원래 요구 레벨 규칙으로 돌아와야 합니다.\n" +
                "★ 규칙 이름을 바꾼 것이라면 OwnershipRuleAnchor를 갱신하세요 — " +
                "이 실패는 '위험'이 아니라 '앵커가 늙었다'일 수도 있습니다.");

            Assert.IsNotEmpty(references,
                $"{LogPrefix} 스위치 참조를 한 건도 못 찾았습니다. 이 저장소는 실제로 2곳에서 읽으므로 " +
                "0건은 '없다'가 아니라 <b>스캐너가 눈이 멀었다</b>입니다.");
        }

        // ====================================================================
        // 2. 테스트 강제값의 접근성 래칫 (§E-6-c)
        // ====================================================================

        /// <summary>
        /// §E-6-c: C층에는 테스트 오버라이드를 두지 않는다. 두어야 한다면 <c>internal</c>이다.
        ///
        /// <para><b>실측(2026-09-02, 출하 어셈블리 IL로도 확인)</b>: 프로덕션에 <c>SetTestOverride</c>
        /// 선언이 2건 있고 그중 <b>1건이 public</b>이다(<c>StickMateDevTools</c>).
        /// 즉 <b>같은 집에 두 관례가 공존한다.</b> 이 테스트는 그 사실을 고치라고 요구하지 않는다 —
        /// 그건 프로덕션 변경이고 내 소관이 아니다. 대신 <b>늘어나지 못하게</b> 잠근다.</para>
        ///
        /// <para>C층이 후자 관례를 따라가면 리플렉션 한 줄이 크랙이 된다. 그날 이 테스트가 먼저 빨개진다.</para>
        /// </summary>
        [Test]
        public void 테스트_강제값을_공개로_여는_스위치는_늘어나지_않는다()
        {
            const int MeasuredTotal = 2;      // 2026-09-02 실측
            const int MaxPublic = 1;          // 그중 public. 이 방향으로만 잠근다.

            var all = new List<string>();
            var pub = new List<string>();

            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
            {
                string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));
                foreach (string raw in stripped.Replace("\r\n", "\n").Split('\n'))
                {
                    string line = raw.Trim();
                    int paren = line.IndexOf('(');
                    if (paren <= 0) continue;

                    int end = paren;
                    int start = end;
                    while (start > 0 && IsIdentifierChar(line[start - 1])) start--;
                    if (start == end) continue;

                    string name = line.Substring(start, end - start);
                    if (name.IndexOf("Override", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (name.IndexOf("Test", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    bool isDeclaration = line.StartsWith("public ", StringComparison.Ordinal)
                                         || line.StartsWith("internal ", StringComparison.Ordinal)
                                         || line.StartsWith("private ", StringComparison.Ordinal)
                                         || line.StartsWith("protected ", StringComparison.Ordinal);
                    if (!isDeclaration) continue;

                    string entry = $"{Path.GetFileName(path)} :: {name}";
                    all.Add(entry);
                    if (line.StartsWith("public ", StringComparison.Ordinal)) pub.Add(entry);
                }
            }

            Debug.Log($"{LogPrefix} 테스트 강제값 선언 {all.Count}건 / 그중 public {pub.Count}건\n  " +
                      string.Join("\n  ", all));

            // ★ 비공허성 — 0건이면 스캐너가 눈이 먼 것이지 "안전"이 아니다.
            Assert.GreaterOrEqual(all.Count, MeasuredTotal,
                $"{LogPrefix} 테스트 강제값 선언을 {all.Count}건밖에 못 찾았습니다(2026-09-02 실측 " +
                $"{MeasuredTotal}건). 이름 관례가 바뀌었다면 탐지 조건을 고치세요 — " +
                "그 전까지 아래 래칫은 아무것도 재지 않습니다.");

            Assert.LessOrEqual(pub.Count, MaxPublic,
                $"{LogPrefix} <b>public</b> 테스트 강제값이 {pub.Count}건입니다(상한 {MaxPublic}건):\n  " +
                string.Join("\n  ", pub) + "\n" +
                "§E-6-c: 새 스위치, 특히 유료 권한(C층) 판정의 강제값은 <b>internal</b>이어야 합니다. " +
                "public이면 프로덕션 어셈블리 밖에서 부를 수 있고, 그 한 줄이 결제 경계를 무력화합니다.");
        }

        // ====================================================================
        // 3. 네거티브 컨트롤
        // ====================================================================

        [Test]
        public void NegativeControl_새_파일이_스위치를_읽으면_잡는다()
        {
            string fake = "public static class FakeEntitlementGate\n" +
                          "{\n" +
                          "    public static bool HasPack(string id)\n" +
                          "        => EquipmentDebugUnlock.UnlockAll || StoreLevel(id);\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.IsNotEmpty(hits,
                $"{LogPrefix} 새 파일의 스위치 참조를 놓쳤습니다 — 그러면 C층이 이 스위치에 " +
                "배선되어도 이 감사는 계속 초록입니다.");
        }

        [Test]
        public void NegativeControl_주석_속_스위치_언급은_참조가_아니다()
        {
            string fake = "public static class FakeGate\n" +
                          "{\n" +
                          "    /// <summary><see cref=\"EquipmentDebugUnlock.UnlockAll\"/>과 무관하다.</summary>\n" +
                          "    // EquipmentDebugUnlock을 여기서는 쓰지 않는다.\n" +
                          "    public static bool Always() => true;\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.IsEmpty(hits,
                $"{LogPrefix} 주석 속 언급을 참조로 셌습니다. 이 저장소의 두 참조 파일은 " +
                "본문 1줄 + 주석 여러 줄이라, 주석을 세면 개수 기대값이 통째로 어긋납니다.");
        }

        [Test]
        public void NegativeControl_한글이_붙어도_참조를_놓치지_않는다()
        {
            // .NET 정규식 \b는 한글을 낱말 문자로 세어 경계를 만들지 않는다.
            // 이 저장소는 그 함정으로 참조 534건 중 245건(46%)을 놓친 적이 있다.
            string fake = "public static class FakeGate\n" +
                          "{\n" +
                          "    public static bool X() => EquipmentDebugUnlock.UnlockAll;\n" +
                          "    public static string 설명 = \"EquipmentDebugUnlock를 읽는다\";\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.GreaterOrEqual(hits.Count, 2,
                $"{LogPrefix} 한글이 바로 뒤에 붙은 참조를 놓쳤습니다({hits.Count}건, 기대 2건 이상). " +
                "낱말 경계 판정이 유니코드에 물들면 이 감사의 탐지력이 절반으로 떨어집니다.");
        }

        [Test]
        public void NegativeControl_타입이름의_부분문자열은_참조가_아니다()
        {
            string fake = "public static class FakeGate\n" +
                          "{\n" +
                          "    public static bool X() => EquipmentDebugUnlockTests.Helper();\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.IsEmpty(hits,
                $"{LogPrefix} 'EquipmentDebugUnlockTests'를 스위치 참조로 셌습니다 — " +
                "낱말 경계가 오른쪽에서 깨졌습니다.");
        }

        [Test]
        public void NegativeControl_원래_규칙이_사라진_참조를_잡는다()
        {
            string fake = "public static class FakeGate\n" +
                          "{\n" +
                          "    public static bool IsOwned() => EquipmentDebugUnlock.UnlockAll;\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.AreEqual(1, hits.Count, $"{LogPrefix} 참조를 1건 찾지 못했습니다.");
            Assert.IsFalse(hits[0].RuleAnchorNearby,
                $"{LogPrefix} 원래 규칙이 하나도 없는데 앵커를 찾았다고 보고했습니다 — " +
                "이 검사는 '스위치가 판정 전체가 되는 것'을 잡으라고 있습니다.");
        }

        [Test]
        public void NegativeControl_선언줄은_참조로_세지_않는다()
        {
            string fake = "public static class EquipmentDebugUnlock\n" +
                          "{\n" +
                          "    public static bool UnlockAll => false;\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.IsEmpty(hits,
                $"{LogPrefix} 선언 줄 자체를 참조로 셌습니다 — 선언 파일을 제외해도 " +
                "개수 기대값이 어긋납니다.");
        }
    }
}
