using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 설정 저장소(<c>PlayerPrefs</c>) <b>허용 명부</b> 감사 — 원칙 3 · 흔적 목록 축 (2026-09-26, test-engineer).
    ///
    /// <para><b>왜 또 있는가</b>(기존 <see cref="GearMenuOnboardingSeenStoreTests"/>와 일부러 겹친다).
    /// 그쪽 감사의 목적은 <b>테스트 격리</b>다 — 계수를 거치지 않는 설정 저장소 호출을 막는다. 그래서
    /// 새 저장소는 「허용 파일로 등재」하면 통과하고, <b>키 개수는 세지 않는다</b>(싱크의 쓰기 함수는 어떤 키든 받는다).
    /// 이 파일의 목적은 다르다: <b>앱이 사용자 기계에 남기는 흔적의 개수</b>를 잠근다.</para>
    ///
    /// <para><b>무엇이 걸려 있나.</b> Windows에서 <c>PlayerPrefs</c>는 <b>레지스트리</b>에 쓴다
    /// (<c>HKCU\Software\&lt;회사&gt;\&lt;제품&gt;</c>). 그래서 저장소가 하나 늘면 레지스트리 값이 늘고,
    /// 흔적 목록(<c>docs/security/PERSISTENT_WRITE_INVENTORY.md</c>의 R 행)과 스토어·신뢰 문구의
    /// 「레지스트리에 값이 몇 개」가 <b>함께 거짓</b>이 된다. 코드가 조용히 늘고 문서만 낡는 것을 막는 자리다.</para>
    ///
    /// <para><b>예정된 두 번째 저장소.</b> <c>docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md</c>가
    /// 작업표시줄 원복 토큰 저장소(Windows 전용)를 명세한다. 아직 <c>.cs</c>가 0줄이다. 착지하면
    /// <see cref="저장소_타입은_허용_명부에_등재된_파일에만_선언된다"/>와 키 개수 잠금이 <b>빨강</b>이 되어
    /// 명부·흔적 목록·문구를 같이 고치게 만든다 — 「착지하면 조용히 통과」가 되지 않는다.</para>
    ///
    /// <para><b>계수 기준</b>(실패 메시지에도 적는다): 판정 대상은 <see cref="SourceTextScanner.ProductionSourceFilesUnderAssets"/>가
    /// 주는 <c>Assets</c> 아래 <c>.cs</c> 전부(경로에 <c>/Tests/</c>가 든 파일 제외)다. 각 파일을
    /// <see cref="SourceTextScanner.BlankCommentsAndStrings(string, List{string})"/>로 <b>주석·문자열·문자 리터럴을 공백으로 지운 뒤</b>
    /// 식별자 토큰으로 센다(보간 구멍은 코드로 남는다 — 구멍 안의 호출은 실제 호출이다). 즉 주석·로그 문구의
    /// <c>PlayerPrefs</c>는 세지 않고, 코드로 실재하는 것만 센다. 세는 단위는 <b>발생 수</b>(줄 수가 아니다).</para>
    ///
    /// <para><b>원리 한계</b>(<see cref="SourceTextScanner"/> 문서와 같다): 글자 스캐너라 계산된 이름 리플렉션·
    /// 유니코드 이스케이프 식별자는 못 본다. 또 이 감사의 범위는 <c>Assets</c> 아래뿐이다 — <c>Packages/</c>의
    /// 서드파티 소스는 보지 않는다(흔적 목록이 별도로 실측했다: UniWinC 패키지 소스의 설정 저장소 적중 0).</para>
    /// </summary>
    public sealed class PlayerPrefsAllowlistAuditTests
    {
        private const string LogPrefix = "[설정저장소명부]";

        /// <summary>계수 기준을 실패 메시지마다 같은 문장으로 붙인다 — 「무엇을 어떻게 셌나」가 없으면 수치가 옮겨질 때 뜻을 잃는다.</summary>
        private const string CountingBasis =
            "계수 기준: Assets 아래 .cs 전부(경로에 /Tests/ 든 파일 제외) · 주석·문자열·문자 리터럴을 공백으로 지운 코드 · " +
            "식별자 토큰의 발생 수(줄 수 아님) · 보간 구멍은 코드로 남김. Packages/ 는 범위 밖.";

        /// <summary>흔적 목록·스토어 문구를 같이 고치라는 지시. 키가 늘거나 저장소가 늘면 이 문장을 읽게 된다.</summary>
        private const string InventoryOrder =
            "이 값을 늘렸다면 코드만 고치고 끝내지 마라 — Windows에서 PlayerPrefs는 레지스트리에 쓴다. " +
            "(1) docs/security/PERSISTENT_WRITE_INVENTORY.md 의 레지스트리 행(R-*)과 0절 개수, " +
            "(2) 「레지스트리에 값이 몇 개」를 말하는 스토어·신뢰 문구(docs/marketing/TRUST_AND_SIGNING.md 등), " +
            "(3) 이 파일의 허용 명부를 **같은 라운드에** 함께 고쳐야 한다(리더 경유).";

        /// <summary>
        /// 허용 명부 — 설정 저장소를 부를 수 있는 파일과 그 파일에서 허용되는 멤버, 그리고 그 파일이 쓰는 키의 개수.
        /// ★ 파일 이름과 멤버 이름은 <c>nameof</c>로 조립한다(문자열로 베끼지 않는다). 기대 개수만 이 테스트의 상수다.
        /// </summary>
        private sealed class AllowedStore
        {
            public string FileName;
            public string[] AllowedMembers;
            public int ExpectedKeyCount;
            public string Why;
        }

        private static IReadOnlyList<AllowedStore> Allowlist() => new[]
        {
            new AllowedStore
            {
                FileName = nameof(PlayerPrefsGearMenuOnboardingSeenStore) + ".cs",
                AllowedMembers = new[] { nameof(PlayerPrefs.GetInt), nameof(PlayerPrefs.SetInt), nameof(PlayerPrefs.Save) },
                ExpectedKeyCount = 1,
                Why = "톱니 부채꼴 「최초 1회 안내 봤음」 플래그 1개(흔적 목록 R-1). 정수 읽기·쓰기·저장만 쓴다 — " +
                      "지우기(Delete/DeleteAll)나 다른 값 형식이 생기면 하위 호환과 흔적 목록이 함께 흔들린다.",
            },
        };

        /// <summary>명부 전체의 키 개수 = 앱이 레지스트리(Windows)·plist(macOS)에 남기는 <b>우리 값</b>의 개수.</summary>
        private const int ExpectedTotalKeyCount = 1;

        /// <summary>명부에 오른 저장소 파일 수. 예정된 토큰 저장소가 착지하면 2가 된다(그때 이 상수도 함께 올린다).</summary>
        private const int ExpectedStoreFileCount = 1;

        /// <summary>스캔 경로가 죽었는지 보는 바닥값(테스트 소유). 실제 프로덕션 파일은 이보다 훨씬 많다.</summary>
        private const int MinimumProductionFileCount = 100;

        private static string PrefsTypeName => nameof(PlayerPrefs);

        /// <summary>에디터 전용 설정 저장소 이름. 프로덕션에 <b>한 번도 없었다</b> — 부재 단언이라 합성 양성 대조를 짝으로 붙인다.</summary>
        private const string EditorPrefsTypeName = "EditorPrefs";

        // ==================================================================
        // 도구
        // ==================================================================

        private sealed class Scanned
        {
            public string Path;
            public string FileName;
            public string Code;                 // 주석·문자열을 지운 코드
            public string Raw;                  // 원문
            public List<string> PlainLiterals;  // 평문 문자열 리터럴의 내용
            public int PrefsIdentifierCount;
            public List<string> PrefsMembers;
            public List<string> PrefsTypeDeclarations;
        }

        /// <summary>한 파일을 판정용 형태로 읽는다. 실제 스캔과 합성 음성 대조가 <b>같은 함수</b>를 쓴다.</summary>
        private static Scanned Scan(string path, string fileName, string raw)
        {
            var literals = new List<string>();
            string code = SourceTextScanner.BlankCommentsAndStrings(raw, literals);
            var members = new List<string>();
            foreach (Match m in Regex.Matches(code, @"(?<![\w@])" + Regex.Escape(PrefsTypeName) + @"\s*\.\s*@?(\w+)"))
                members.Add(m.Groups[1].Value);
            var declarations = new List<string>();
            foreach (Match m in Regex.Matches(code, @"(?<![\w@])class\s+(\w*" + Regex.Escape(PrefsTypeName) + @"\w*)"))
                declarations.Add(m.Groups[1].Value);
            return new Scanned
            {
                Path = path,
                FileName = fileName,
                Code = code,
                Raw = raw,
                PlainLiterals = literals,
                PrefsIdentifierCount = SourceTextScanner.CountIdentifier(code, PrefsTypeName),
                PrefsMembers = members,
                PrefsTypeDeclarations = declarations,
            };
        }

        private static List<Scanned> ScanProduction()
        {
            var all = new List<Scanned>();
            foreach (string path in SourceTextScanner.ProductionSourceFilesUnderAssets())
                all.Add(Scan(path, Path.GetFileName(path), File.ReadAllText(path)));

            Assert.GreaterOrEqual(all.Count, MinimumProductionFileCount,
                $"{LogPrefix} ★ 프로덕션 소스가 {all.Count}개뿐입니다 — 스캔 경로가 죽었습니다. 이 실행의 모든 0건은 무효입니다. {CountingBasis}");
            return all;
        }

        private static IReadOnlyList<string> AllowedFileNames() => Allowlist().Select(a => a.FileName).ToArray();

        // ==================================================================
        // ① 호출 자리 — 명부 밖에서는 부르지 않는다
        // ==================================================================

        [Test]
        public void 설정_저장소_호출은_허용_명부의_파일과_멤버_안에만_있다()
        {
            IReadOnlyList<AllowedStore> allowlist = Allowlist();
            Assert.IsNotEmpty(allowlist, $"{LogPrefix} ★ 허용 명부가 비었습니다 — 아래 순회는 아무것도 재지 않습니다(빈 목록 통과 금지).");
            Assert.AreEqual(ExpectedStoreFileCount, allowlist.Count,
                $"{LogPrefix} 허용 명부의 파일 수가 {allowlist.Count}개입니다(기대 {ExpectedStoreFileCount}). {InventoryOrder}");

            List<Scanned> all = ScanProduction();
            IReadOnlyList<string> allowed = AllowedFileNames();

            var offenders = new List<string>();
            var foundAllowed = new Dictionary<string, Scanned>(StringComparer.Ordinal);
            foreach (Scanned s in all)
            {
                bool isAllowed = allowed.Contains(s.FileName, StringComparer.Ordinal);
                if (isAllowed)
                {
                    Assert.IsFalse(foundAllowed.ContainsKey(s.FileName),
                        $"{LogPrefix} ★ 명부의 '{s.FileName}'과 같은 이름의 파일이 둘 이상입니다 — 어느 것을 잰 것인지 알 수 없어 판정 무효입니다.");
                    foundAllowed[s.FileName] = s;
                    continue;
                }
                if (s.PrefsIdentifierCount > 0)
                    offenders.Add($"{s.Path.Replace('\\', '/')} ({s.PrefsIdentifierCount}건)");
            }

            // 존재 대조 — 명부의 파일을 실제로 찾았고, 그 안에서 스캐너가 호출을 **본다**.
            foreach (AllowedStore entry in allowlist)
            {
                Assert.IsTrue(foundAllowed.TryGetValue(entry.FileName, out Scanned store),
                    $"{LogPrefix} ★ 명부의 '{entry.FileName}'을 프로덕션에서 찾지 못했습니다 — 이름이 바뀌었거나 사라졌습니다. " +
                    $"이 실행의 「명부 밖 0건」은 무효입니다. {CountingBasis}");
                Assert.GreaterOrEqual(store.PrefsIdentifierCount, 1,
                    $"{LogPrefix} ★ '{entry.FileName}' 안에서 {PrefsTypeName} 호출을 하나도 못 봤습니다 — 스캐너가 눈이 먼 상태라 " +
                    $"명부 밖 0건이 아무것도 증명하지 못합니다(부재 단언의 짝 존재 대조). {CountingBasis}");

                // 별칭·완전 한정 이름으로 새는 형태: 이름 등장 수와 멤버 접근 수가 같아야 한다.
                Assert.AreEqual(store.PrefsIdentifierCount, store.PrefsMembers.Count,
                    $"{LogPrefix} '{entry.FileName}'에 멤버 접근이 아닌 {PrefsTypeName} 사용이 있습니다(별칭 등) — " +
                    $"이름 {store.PrefsIdentifierCount}건 · 멤버 접근 {store.PrefsMembers.Count}건. {CountingBasis}");

                // 허용 멤버 밖은 0, 그리고 허용 멤버는 각각 실제로 쓰인다(둘 다 없으면 명부가 낡은 것이다).
                string[] unexpected = store.PrefsMembers.Distinct(StringComparer.Ordinal)
                    .Where(m => !entry.AllowedMembers.Contains(m, StringComparer.Ordinal)).ToArray();
                Assert.IsEmpty(unexpected,
                    $"{LogPrefix} '{entry.FileName}'이 명부에 없는 {PrefsTypeName} 멤버를 씁니다: [{string.Join(", ", unexpected)}]. " +
                    $"허용: [{string.Join(", ", entry.AllowedMembers)}]. 근거: {entry.Why}\n{InventoryOrder}");
                foreach (string member in entry.AllowedMembers)
                {
                    Assert.Contains(member, store.PrefsMembers,
                        $"{LogPrefix} ★ '{entry.FileName}'에서 명부가 허용한 멤버 '{member}'를 한 번도 쓰지 않습니다 — " +
                        "명부가 실제 코드보다 넓습니다(넓은 명부는 새 호출을 조용히 통과시킨다). 명부를 좁히거나 코드를 확인하십시오.");
                }
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 허용 명부 밖에서 설정 저장소({PrefsTypeName})를 부릅니다:\n  " + string.Join("\n  ", offenders) +
                "\n앱이 사용자 기계에 남기는 흔적이 늘어난다는 뜻입니다(Windows에서는 레지스트리 값). " +
                $"정말 필요하면 이 파일의 허용 명부에 등재하고 키 개수를 함께 올리십시오.\n{InventoryOrder}\n{CountingBasis}");
        }

        /// <summary>음성 대조 — 명부 밖 파일에 호출을 심으면 <b>같은 판정 함수</b>가 실제로 잡는가.
        /// (Unity 변이 실행 없이도 탐지 경로가 살아 있음을 이 파일 안에서 증명한다.)</summary>
        [Test]
        public void 음성대조_명부_밖_파일에_심은_호출은_같은_판정에_걸린다()
        {
            string injected =
                "namespace X { internal static class Y { internal static void Z() { " +
                PrefsTypeName + ".SetInt(\"k\", 1); " + PrefsTypeName + ".Save(); } } }";
            Scanned s = Scan("/synthetic/NotOnTheAllowlist.cs", "NotOnTheAllowlist.cs", injected);

            Assert.IsFalse(AllowedFileNames().Contains(s.FileName, StringComparer.Ordinal),
                $"{LogPrefix} ★ 합성 파일 이름이 명부에 들어 있습니다 — 이 음성 대조가 아무것도 증명하지 못합니다.");
            Assert.AreEqual(2, s.PrefsIdentifierCount,
                $"{LogPrefix} ★ 심은 호출 2건을 세지 못했습니다({s.PrefsIdentifierCount}건) — 판정 함수가 죽었습니다. {CountingBasis}");
            CollectionAssert.AreEquivalent(new[] { nameof(PlayerPrefs.SetInt), nameof(PlayerPrefs.Save) }, s.PrefsMembers,
                $"{LogPrefix} ★ 심은 멤버를 그대로 읽지 못했습니다: [{string.Join(", ", s.PrefsMembers)}].");

            // 그리고 주석·문자열 안의 같은 글자는 세지 않는다(계수 기준의 반대편).
            Scanned quiet = Scan("/synthetic/OnlyText.cs", "OnlyText.cs",
                "namespace X { internal static class Y { /* " + PrefsTypeName + ".SetInt */ " +
                "internal static string S = \"" + PrefsTypeName + ".Save()\"; } }");
            Assert.AreEqual(0, quiet.PrefsIdentifierCount,
                $"{LogPrefix} ★ 주석·문자열 안의 글자를 코드로 셌습니다({quiet.PrefsIdentifierCount}건) — 계수 기준이 깨졌습니다. {CountingBasis}");
        }

        // ==================================================================
        // ② 키 개수 — 흔적 개수의 자물쇠
        // ==================================================================

        [Test]
        public void 설정_저장소_키는_명부에_적힌_개수만큼만_있다()
        {
            List<Scanned> all = ScanProduction();
            IReadOnlyList<AllowedStore> allowlist = Allowlist();
            int total = 0;

            foreach (AllowedStore entry in allowlist)
            {
                Scanned store = all.SingleOrDefault(s => string.Equals(s.FileName, entry.FileName, StringComparison.Ordinal));
                Assert.IsNotNull(store,
                    $"{LogPrefix} ★ 명부의 '{entry.FileName}'을 찾지 못했습니다 — 아래 키 개수는 무효입니다. {CountingBasis}");

                // 방법 A: 원문에서 const string 선언을 읽는다.
                var declared = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match m in Regex.Matches(store.Raw, @"(?<![\w@])const\s+string\s+(\w+)\s*=\s*""((?:[^""\\]|\\.)*)""\s*;"))
                    declared[m.Groups[1].Value] = m.Groups[2].Value;

                // 방법 B: 스캐너가 돌려준 평문 문자열 리터럴.
                string[] literals = store.PlainLiterals.Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

                Assert.AreEqual(entry.ExpectedKeyCount, declared.Count,
                    $"{LogPrefix} '{entry.FileName}'의 키 상수가 {declared.Count}개입니다(기대 {entry.ExpectedKeyCount}): " +
                    $"[{string.Join(", ", declared.Keys)}]. 근거: {entry.Why}\n{InventoryOrder}");

                // 두 방법이 갈라지면 어느 쪽도 못 믿는다 — 인라인 키 문자열이나 키 아닌 리터럴이 들어온 신호다.
                CollectionAssert.AreEquivalent(declared.Values.ToArray(), literals,
                    $"{LogPrefix} '{entry.FileName}'의 키 상수와 평문 문자열 리터럴이 다릅니다.\n" +
                    $"  const string: [{string.Join(" | ", declared.Values)}]\n  리터럴: [{string.Join(" | ", literals)}]\n" +
                    "키를 상수 없이 인라인으로 넘겼거나, 키가 아닌 문자열이 이 파일에 들어왔습니다. 후자라면 명부의 기대 개수를 함께 갱신하십시오.\n" +
                    InventoryOrder);

                total += declared.Count;
            }

            Assert.AreEqual(ExpectedTotalKeyCount, total,
                $"{LogPrefix} 앱이 설정 저장소에 쓰는 키가 총 {total}개입니다(기대 {ExpectedTotalKeyCount}). " +
                $"이 수가 곧 Windows 레지스트리에 남는 **우리 값**의 개수다.\n{InventoryOrder}");
        }

        // ==================================================================
        // ③ 저장소 타입 — 새 저장소가 조용히 늘지 않는다 (예정된 토큰 저장소 대비)
        // ==================================================================

        [Test]
        public void 저장소_타입은_허용_명부에_등재된_파일에만_선언된다()
        {
            List<Scanned> all = ScanProduction();
            IReadOnlyList<string> allowed = AllowedFileNames();

            var declaringFiles = new List<string>();
            var outside = new List<string>();
            foreach (Scanned s in all)
            {
                if (s.PrefsTypeDeclarations.Count == 0) continue;
                declaringFiles.Add(s.FileName);
                if (!allowed.Contains(s.FileName, StringComparer.Ordinal))
                    outside.Add($"{s.Path.Replace('\\', '/')}: [{string.Join(", ", s.PrefsTypeDeclarations)}]");
            }

            // 존재 대조 — 이 규칙이 실제로 무언가를 보고 있다(지금은 온보딩 저장소와 그 안의 싱크).
            Assert.IsNotEmpty(declaringFiles,
                $"{LogPrefix} ★ 이름에 {PrefsTypeName}가 든 타입 선언을 하나도 못 찾았습니다 — 이 규칙이 눈이 먼 상태입니다. {CountingBasis}");

            Assert.IsEmpty(outside,
                $"{LogPrefix} 허용 명부 밖의 파일이 설정 저장소 타입을 선언합니다:\n  " + string.Join("\n  ", outside) + "\n" +
                "예정된 작업표시줄 원복 토큰 저장소(docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md)가 이 자리로 들어온다. " +
                "착지시켰다면 이 파일의 허용 명부에 등재하고, 그 저장소의 키를 키 개수 잠금에 더하십시오 — " +
                $"등재 없이 통과하면 흔적 목록과 스토어 문구가 조용히 거짓이 된다.\n{InventoryOrder}");
        }

        // ==================================================================
        // ④ 에디터 전용 설정 저장소는 프로덕션에 없다 (부재 단언 + 합성 양성 대조)
        // ==================================================================

        [Test]
        public void 에디터_전용_설정_저장소는_프로덕션에_없다()
        {
            List<Scanned> all = ScanProduction();

            var hits = new List<string>();
            foreach (Scanned s in all)
            {
                int n = SourceTextScanner.CountIdentifier(s.Code, EditorPrefsTypeName);
                if (n > 0) hits.Add($"{s.Path.Replace('\\', '/')} ({n}건)");
            }

            // ★ 짝 존재 대조: 이 이름은 저장소에 한 번도 없었다. 그래서 실제 적중으로 니들을 증명할 수 없다 —
            //   같은 니들·같은 계수 함수가 합성 코드에서 1을 세는지 먼저 보인다. 이게 없으면 니들이 썩어도 조용히 초록이다.
            string probe = "namespace X { class Y { void Z() { " + EditorPrefsTypeName + ".SetInt(\"k\", 1); } } }";
            var probeLiterals = new List<string>();
            string probeCode = SourceTextScanner.BlankCommentsAndStrings(probe, probeLiterals);
            Assert.AreEqual(1, SourceTextScanner.CountIdentifier(probeCode, EditorPrefsTypeName),
                $"{LogPrefix} ★ 합성 양성 대조 실패 — '{EditorPrefsTypeName}' 니들이 죽었습니다. 아래 0건은 무효입니다. {CountingBasis}");

            Assert.IsEmpty(hits,
                $"{LogPrefix} 프로덕션이 에디터 전용 설정 저장소({EditorPrefsTypeName})를 씁니다:\n  " + string.Join("\n  ", hits) +
                $"\n출하본에 들어가면 흔적 목록 밖의 쓰기가 된다.\n{CountingBasis}");
        }
    }
}
