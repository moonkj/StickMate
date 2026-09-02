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
    /// E-9 #1 — <b>유료 권한(C층)은 세이브 파일에 적히지 않는다</b> (security, 2026-09-02)
    /// ============================================================================
    /// 규범: <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-8-b · §E-8-c,
    /// 구조 확정: <c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §8-2.
    ///
    /// <para><b>이 감사가 지키는 것은 「방어」가 아니라 「표적 제거」다.</b> 세이브는 평문 JSON이고
    /// 앞으로도 평문이다(§E-8-a: 암호화·HMAC·변조 감지 전부 기각 — 그 검사는 치터보다
    /// <b>우리 쓰기 버그</b>를 먼저 만나서 정직한 유저에게 "당신은 치터입니다"를 말하게 된다).
    /// 평문을 유지하는 대가로, <b>돈이 걸린 사실만은 그 파일에 아예 없어야 한다.</b>
    /// 필드 하나가 생기는 순간 메모장으로 DLC를 얻을 수 있고, 그때 잃는 사람은
    /// 유저 자신이 아니라 <b>개발자(매출)</b>다 — 정의서 위협표에서 유일하게 ★가 붙은 칸이다.</para>
    ///
    /// ============================================================================
    /// 무엇을 재고 무엇을 재지 않는가 — 이 구분이 이 감사의 전부다
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>재는 것: 필드 <u>이름</u>.</b> 직렬화 스키마에 소유·권한을 뜻하는 이름이 생겼는가.</item>
    ///  <item><b>재지 않는 것: <u>값</u>.</b> <c>wornHead = "pack.office.fedora"</c>는 <b>정상</b>이다
    ///    (§8-2-c). 착용 상태는 A/B/C 어느 층이든 세이브에 남아야 한다 — 남지 않으면 DLC를 산
    ///    사람이 앱을 껐다 켤 때마다 벗겨진다. 값을 금지하면 그 순간 이 감사는
    ///    <b>돈 낸 사람을 잠그는 장치</b>가 된다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 이 파일에 프로덕션 <b>타입 이름</b>을 니들로 베끼지 않았다
    /// ============================================================================
    /// 스키마 타입을 이름으로 찾으면 그 이름이 바뀌는 날 스캔이 <b>조용히 0건</b>이 되고,
    /// 그 0건은 "위반 없음"과 글자 하나 다르지 않다(이 저장소가 아홉 번 당한 형태).
    /// 그래서 <c>[Serializable]</c> 표식에서 타입을 <b>발견</b>하고, 발견 자체가 실패하면
    /// <b>먼저 빨개진다</b>. 그 발견 능력은 <see cref="양성대조_스캐너가_실제_세이브_스키마와_알려진_필드를_찾아낸다"/>가
    /// 매 실행 증명한다.
    ///
    /// <para>리플렉션은 한 줄도 쓰지 않는다 — 활성 빌드 타깃 반대편 파일은 타입이 존재하지 않아
    /// 리플렉션 감사가 구조적으로 눈이 먼다(CLAUDE.md 활성 빌드 타깃 규칙).</para>
    /// </summary>
    public sealed class EntitlementNotInSaveAuditTests
    {
        private const string LogPrefix = "[C층-세이브금지]";

        /// <summary>
        /// 스캔이 공허해지는 것을 막는 <b>바닥값</b>. 2026-09-02 실측은 프로덕션 전체
        /// <b>66개</b>다 — 세이브 저장소 48(SaveData 44 + TodoRecord 3 + VersionProbe 1) +
        /// 액세서리 정의 12 + 작업표시줄 원복 원장 6. 40 아래로 떨어졌다면 그건
        /// "필드가 줄었다"가 아니라 <b>파서가 눈이 멀었다</b>로 읽어야 한다.
        /// <para>정확한 개수를 등호로 걸지 <b>않는</b> 이유: 필드는 정상적으로 늘어난다.
        /// 등호는 기능 추가마다 빨개져서 몇 번 만에 꺼지고, 꺼진 감사는 없는 감사다.</para>
        /// </summary>
        private const int MinSchemaFieldCount = 40;

        /// <summary>
        /// 금지 토큰과 <b>각각의 근거</b>. 근거를 못 대는 토큰은 여기 있으면 안 된다 —
        /// 토큰이 늘어날수록 정직한 필드가 빨개질 확률만 오른다.
        /// <para>매칭은 <b>낱말 조각의 접두</b>다(<see cref="EntitlementAuditSource.CamelSegments"/>):
        /// <c>packs</c>·<c>packId</c>는 걸리고 <c>wornBackpack</c>은 걸리지 않는다.</para>
        /// </summary>
        private static readonly (string Token, string Why)[] ForbiddenTokens =
        {
            ("dlc",     "C층 그 자체. 스토어가 들고 있어야 할 사실이 디스크에 적히면 메모장이 지갑이 된다."),
            ("pack",    "DLC 판매 단위. 팩 보유를 파일에 적는 순간 팩 전체가 한 줄로 열린다."),
            ("entitle", "엔타이틀먼트. 스토어 조회 결과의 캐시는 §E-4-a가 파일·PlayerPrefs·레지스트리 전부 금지."),
            ("own",     "소유. <b>계약서의 owned보다 넓게 잡았다</b> — owned만 막으면 ownership/owner가 통과한다. " +
                        "오늘 스키마 48필드 중 이 접두에 걸리는 것은 0개라 넓혀도 오탐이 없다(실측)."),
            ("license", "라이선스 파일을 자체 발급하는 형태(§E-5가 금지). 세이브가 그 그릇이 되면 안 된다."),
            ("sku",     "상품 식별자. 세이브에 SKU가 들어간다는 것은 결제 사실이 들어간다는 뜻이다."),
        };

        /// <summary>
        /// ★ <b>일부러 비어 있다.</b> 정당한 이유로 위 토큰에 걸리는 필드가 생기면 여기에
        /// <b>근거와 함께</b> 등록한다(예: C층과 무관한 UI 상태 <c>seenPackIds</c>).
        /// <para>비어 있다는 사실 자체를 <see cref="면제표는_오늘_비어_있다"/>가 단언한다 —
        /// 이 저장소의 거짓 통과 #5(면제 목록이 비어 <c>foreach</c>가 아무것도 안 재고 초록)를
        /// 막기 위해서다. 면제가 생기는 날 그 테스트가 먼저 빨개져 <b>사람이 근거를 읽게</b> 만든다.</para>
        /// </summary>
        private static readonly (string Field, string Why)[] Exemptions = { };

        // ====================================================================
        // 스캔 — 순수 함수. 아래 네거티브 컨트롤이 <b>같은 함수</b>에 가짜 소스를 흘린다.
        // ====================================================================

        private struct SchemaScan
        {
            public List<string> TypeNames;
            public List<string> FieldNames;
            public List<string> Violations;
        }

        /// <summary>
        /// <paramref name="strippedSource"/>(주석 제거본)에서 직렬화 스키마를 발견하고
        /// 필드 이름을 금지 토큰과 대조한다. <b>값·문자열 리터럴은 보지 않는다.</b>
        /// </summary>
        private static SchemaScan Scan(string strippedSource)
        {
            var scan = new SchemaScan
            {
                TypeNames = EntitlementAuditSource.SerializableTypeNames(strippedSource),
                FieldNames = new List<string>(),
                Violations = new List<string>(),
            };

            var exempt = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string field, string why) in Exemptions) exempt[field] = why;

            foreach (string typeName in scan.TypeNames)
            {
                string body = EntitlementAuditSource.TypeBodyOrNull(strippedSource, typeName);
                if (body == null)
                {
                    scan.Violations.Add($"직렬화 타입 {typeName}의 본문을 잘라 내지 못했습니다 — " +
                        "파서가 이 타입을 통째로 못 보고 있습니다(위반 0건이 아니라 측정 0건입니다).");
                    continue;
                }

                foreach (string field in EntitlementAuditSource.PublicFieldNames(body))
                {
                    scan.FieldNames.Add(field);
                    if (exempt.ContainsKey(field)) continue;

                    List<string> segments = EntitlementAuditSource.CamelSegments(field);
                    foreach ((string token, string why) in ForbiddenTokens)
                    {
                        bool hit = false;
                        foreach (string segment in segments)
                        {
                            if (!segment.StartsWith(token, StringComparison.Ordinal)) continue;
                            hit = true;
                            break;
                        }
                        if (!hit) continue;

                        scan.Violations.Add($"{typeName}.{field} — 금지 토큰 '{token}'\n" +
                            $"      {why}");
                        break;
                    }
                }
            }
            return scan;
        }

        /// <summary>
        /// 세이브 스키마를 <b>선언하는</b> 프로덕션 파일. 파일명이 아니라 <b>선언</b>으로 찾는다:
        /// <c>[Serializable]</c> 타입 + <c>JsonUtility</c>를 함께 가진 후보 중
        /// <b>직렬화 필드가 가장 많은</b> 것. 순서(정렬)에 기대지 않는다.
        /// <para>2026-09-02 실측 후보 2개 — 세이브 저장소(48필드)와 작업표시줄 원복 원장(6필드).
        /// 원장은 유저 진행도가 아니라 <b>OS 상태 복구용 흔적</b>이지만, 그것도 디스크에 나가는
        /// 평문 JSON이므로 아래 본 감사는 <b>후보를 가리지 않고 전부</b> 훑는다. 이 함수는
        /// 양성 대조가 "가장 큰 스키마"를 특정하기 위해서만 쓴다.</para>
        /// </summary>
        private static string FindSchemaFile(out int candidateCount)
        {
            string found = null;
            int bestFieldCount = -1;
            candidateCount = 0;

            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
            {
                string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));
                List<string> types = EntitlementAuditSource.SerializableTypeNames(stripped);
                if (types.Count == 0) continue;
                if (stripped.IndexOf("JsonUtility", StringComparison.Ordinal) < 0) continue;
                candidateCount++;

                int fieldCount = Scan(stripped).FieldNames.Count;
                if (fieldCount <= bestFieldCount) continue;
                bestFieldCount = fieldCount;
                found = path;
            }
            return found;
        }

        // ====================================================================
        // 1. 본론
        // ====================================================================

        [Test]
        public void 세이브_스키마의_필드_이름에_유료권한_토큰이_하나도_없다()
        {
            string[] production = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(production.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {production.Length}개밖에 읽지 못했습니다 " +
                $"({EntitlementAuditSource.ScriptsRoot}). 경로가 바뀌었다면 여기서 멈추는 것이 맞습니다 — " +
                "그대로 두면 아래 모든 '없음' 판정이 '아무것도 안 봤음'이 됩니다.");

            Assert.IsNotEmpty(ForbiddenTokens,
                $"{LogPrefix} 금지 토큰표가 비었습니다 — 아래 대조가 통째로 공허해집니다(거짓 통과 #5).");

            string schemaPath = FindSchemaFile(out int candidates);
            Assert.IsNotNull(schemaPath,
                $"{LogPrefix} [Serializable] 타입과 JsonUtility를 함께 가진 프로덕션 파일을 찾지 못했습니다. " +
                "세이브 저장소가 옮겨졌거나 직렬화 방식이 바뀌었습니다 — 이 감사를 그 자리로 따라가게 " +
                "고치기 전에는 C층이 세이브에 새는지 아무도 보고 있지 않습니다.");

            var allViolations = new List<string>();
            var allFields = new List<string>();
            var report = new StringBuilder();
            report.Append(LogPrefix).Append(" 직렬화 스키마 스캔 (후보 파일 ").Append(candidates).Append("개)\n");

            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
            {
                string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));
                if (EntitlementAuditSource.SerializableTypeNames(stripped).Count == 0) continue;

                SchemaScan scan = Scan(stripped);
                allFields.AddRange(scan.FieldNames);
                foreach (string v in scan.Violations)
                    allViolations.Add($"  · {Path.GetFileName(path)} :: {v}");

                report.Append("  ").Append(Path.GetFileName(path)).Append('\t')
                      .Append(string.Join(", ", scan.TypeNames)).Append("\t필드 ")
                      .Append(scan.FieldNames.Count).Append("개\n");
            }

            report.Append("  합계 직렬화 필드 ").Append(allFields.Count).Append("개\n");
            Debug.Log(report.ToString());

            Assert.GreaterOrEqual(allFields.Count, MinSchemaFieldCount,
                $"{LogPrefix} 직렬화 필드를 {allFields.Count}개밖에 세지 못했습니다(바닥값 " +
                $"{MinSchemaFieldCount}개, 2026-09-02 실측 66개). 필드가 정말 줄어든 것이 아니라면 " +
                "파서가 눈이 먼 것입니다 — 이 상태의 '위반 0건'은 측정이 아닙니다.");

            Assert.IsEmpty(allViolations,
                $"{LogPrefix} 세이브 스키마에 <b>유료 권한을 뜻하는 필드</b>가 생겼습니다({allViolations.Count}건):\n"
                + string.Join("\n", allViolations) + "\n\n" +
                "이 파일은 평문 JSON이고 앞으로도 평문입니다(§E-8-a). 여기에 소유를 적으면 " +
                "메모장으로 DLC가 열립니다. C층 소유는 <b>스토어에만</b> 두고 매 실행 조회하세요" +
                "(§E-4-a: 파일·PlayerPrefs·레지스트리 어디에도 캐시하지 않는다).\n" +
                "★ 착용 <b>값</b>(wornHead = \"pack.office.fedora\")은 위반이 아닙니다 — 그건 " +
                "필드 이름이 아니라 값이고, 이 감사는 값을 보지 않습니다(§8-2-c).");
        }

        [Test]
        public void 면제표는_오늘_비어_있다()
        {
            Assert.AreEqual(0, Exemptions.Length,
                $"{LogPrefix} 면제가 {Exemptions.Length}건 생겼습니다. 이 테스트는 " +
                "면제가 <b>조용히</b> 늘어나는 것을 막기 위해 일부러 실패합니다 — " +
                "각 항목의 근거를 읽고, 정말 C층과 무관하다면 이 기대값을 함께 올리세요. " +
                "근거 없이 기대값만 올리면 이 감사는 그날부터 아무것도 지키지 않습니다.");
        }

        // ====================================================================
        // 2. 양성 대조 — "0건"이 능력을 증명한 뒤에만 값을 갖는다
        // ====================================================================

        /// <summary>
        /// ★ 위 테스트의 "위반 0건"은 <b>스캐너가 실제로 무언가를 봤을 때만</b> 뜻이 있다.
        /// 여기서 알려진 필드를 실제로 찾아 보여 그 능력을 매 실행 증명한다.
        /// <para>니들은 전부 <b>존재 단언</b>이다 — 프로덕션에서 이름이 바뀌면 여기서 <b>시끄럽게</b>
        /// 빨개진다(CLAUDE.md: 부재 단언용 니들은 썩으면 조용히 초록이 되지만 존재 단언은 그렇지 않다).</para>
        /// </summary>
        [Test]
        public void 양성대조_스캐너가_실제_세이브_스키마와_알려진_필드를_찾아낸다()
        {
            string schemaPath = FindSchemaFile(out _);
            Assert.IsNotNull(schemaPath, $"{LogPrefix} 세이브 스키마 파일을 찾지 못했습니다.");

            string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(schemaPath));
            SchemaScan scan = Scan(stripped);

            Assert.GreaterOrEqual(scan.TypeNames.Count, 2,
                $"{LogPrefix} 직렬화 타입을 {scan.TypeNames.Count}개밖에 발견하지 못했습니다 " +
                $"({string.Join(", ", scan.TypeNames)}). 2026-09-02 실측은 3개입니다.");

            // 알려진 앵커 — 하나라도 사라지면 스키마가 바뀐 것이고, 그때 이 감사도 함께 봐야 한다.
            foreach (string anchor in new[] { "version", "characterName", "wornHead" })
            {
                Assert.Contains(anchor, scan.FieldNames,
                    $"{LogPrefix} 알려진 세이브 필드 '{anchor}'을 찾지 못했습니다. " +
                    "필드가 실제로 사라졌다면 이 앵커를 갱신하고, 아니라면 파서가 깨진 것입니다 — " +
                    "어느 쪽이든 위 감사의 '위반 0건'을 지금은 믿을 수 없습니다.");
            }
        }

        // ====================================================================
        // 3. 네거티브 컨트롤 — 가짜 위반을 <b>같은 함수</b>에 흘린다
        // ====================================================================

        [Test]
        public void NegativeControl_소유_필드가_생기면_반드시_잡는다()
        {
            foreach (string field in new[]
                     {
                         "public string[] ownedDlcIds;",
                         "public bool packOfficePurchased;",
                         "public string entitlementState;",
                         "public string[] licenseKeys;",
                         "public int skuId;",
                         "public bool ownershipVerified;",
                     })
            {
                string fake = "[Serializable]\n" +
                              "private sealed class FakeSave\n" +
                              "{\n" +
                              "    public int version;\n" +
                              "    " + field + "\n" +
                              "}\n";
                SchemaScan scan = Scan(EntitlementAuditSource.StripComments(fake));
                Assert.IsNotEmpty(scan.Violations,
                    $"{LogPrefix} 스캐너가 위반 필드를 놓쳤습니다 → {field}\n" +
                    "이 상태에서는 프로덕션의 '위반 0건'이 아무 뜻도 없습니다.");
            }
        }

        [Test]
        public void NegativeControl_값이_C층_아이디여도_위반이_아니다()
        {
            // §8-2-c: 착용 상태는 C층 아이템이어도 세이브에 남아야 한다.
            //         남지 않으면 DLC를 <b>산</b> 사람이 껐다 켤 때마다 벗겨진다.
            string fake = "[Serializable]\n" +
                          "private sealed class FakeSave\n" +
                          "{\n" +
                          "    public string wornHead = \"pack.office.fedora\";\n" +
                          "    public string wornNeck = \"dlc.winter.scarf\";\n" +
                          "}\n";
            SchemaScan scan = Scan(EntitlementAuditSource.StripComments(fake));
            Assert.IsEmpty(scan.Violations,
                $"{LogPrefix} 값(문자열 리터럴)을 위반으로 셌습니다. 그렇게 되면 이 감사는 " +
                "DLC를 <b>정상 구매한</b> 사용자의 착용 상태 저장을 막는 장치가 됩니다 — " +
                "무단 사용을 막으려다 돈 낸 사람을 잠그는, 정의서가 가장 비싸다고 못박은 실패입니다.\n" +
                "잡힌 것: " + string.Join(" / ", scan.Violations));
        }

        [Test]
        public void NegativeControl_주석_속_필드는_위반이_아니다()
        {
            string fake = "[Serializable]\n" +
                          "private sealed class FakeSave\n" +
                          "{\n" +
                          "    // public bool ownedDlcPack;   ← 계획만 적어 둔 주석\n" +
                          "    /* public string entitlementCache; */\n" +
                          "    public int version;\n" +
                          "}\n";
            SchemaScan scan = Scan(EntitlementAuditSource.StripComments(fake));
            Assert.IsEmpty(scan.Violations,
                $"{LogPrefix} 주석 속 언급을 배선으로 셌습니다. 이 저장소의 프로덕션 주석에는 " +
                "'DLC'가 스무 군데 넘게 나오지만 배선은 0건입니다 — 주석을 세면 이 감사는 " +
                "첫날부터 빨간 채로 방치되고, 방치된 감사는 없는 감사입니다.\n" +
                "잡힌 것: " + string.Join(" / ", scan.Violations));
        }

        [Test]
        public void NegativeControl_backpack_같은_합성어는_오탐하지_않는다()
        {
            string fake = "[Serializable]\n" +
                          "private sealed class FakeSave\n" +
                          "{\n" +
                          "    public string wornBackpack;\n" +
                          "    public bool hasBackpackSlot;\n" +
                          "    public int downloadedCount;\n" +
                          "}\n";
            SchemaScan scan = Scan(EntitlementAuditSource.StripComments(fake));
            Assert.IsEmpty(scan.Violations,
                $"{LogPrefix} 'backpack'을 'pack'으로, 'downloaded'를 'own'으로 오탐했습니다 — " +
                "낱말 조각 분해(CamelSegments)가 깨졌습니다. 정직한 필드를 빨갛게 만드는 감사는 " +
                "몇 번 만에 꺼집니다.\n잡힌 것: " + string.Join(" / ", scan.Violations));
        }

        [Test]
        public void NegativeControl_직렬화_표식이_없는_클래스는_스캔하지_않는다()
        {
            string fake = "private sealed class NotSerialized\n" +
                          "{\n" +
                          "    public bool ownedDlcPack;\n" +
                          "}\n";
            SchemaScan scan = Scan(EntitlementAuditSource.StripComments(fake));
            Assert.IsEmpty(scan.TypeNames,
                $"{LogPrefix} [Serializable]이 없는 타입까지 스키마로 셌습니다. " +
                "그러면 런타임 전용 모델(디스크에 안 나가는 것)이 위반으로 잡혀 " +
                "감사가 엉뚱한 곳을 가리킵니다.");
            Assert.IsEmpty(scan.Violations, $"{LogPrefix} 위와 같음 — 위반이 잡혔습니다.");
        }
    }
}
