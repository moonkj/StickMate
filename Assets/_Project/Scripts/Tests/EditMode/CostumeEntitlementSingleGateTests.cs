using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// ★★★ P1 통과조건 2 — <b>유료 권한(C층) 판정이 한 파일에만 산다</b>
    /// ============================================================================
    /// 정본: <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 1-5·1-6절(규칙 C-1/C-2) ·
    /// 10절 I-5 · 9-3절(P1 판정) / <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-1 ~ §E-4.
    ///
    /// <para><b>실측(계약서 M-7)</b>: 이 라운드 직전까지 <c>PackEntitlements.StateOf</c>의
    /// 프로덕션 소비자는 <b>0건</b>이었다. 코스튬 게이트가 <b>첫 번째</b>이고,
    /// <b>첫 소비자가 세우는 어법을 두 번째부터는 베낀다</b>. 그래서 계약서 I-5가 그 판정을
    /// <see cref="CostumeEntitlement"/> 한 파일에 가뒀다 — 두 곳이 되는 순간 「반만 열린」 상태
    /// (프롭은 뜨는데 포즈는 안 바뀜 / 시작엔 열렸는데 종료엔 닫힘)가 만들어지고,
    /// 그 화면은 <b>「기능이 아직 안 붙었다」와 구별되지 않는다</b>.</para>
    ///
    /// ============================================================================
    /// ★★ 이 파일의 본체는 <b>부재 단언</b>이다 — 썩으면 <b>조용히 초록</b>이 된다
    /// ============================================================================
    /// CLAUDE.md 경고 그대로다: 존재 단언용 니들은 썩으면 시끄럽게 빨개지지만
    /// <b>부재 단언용 니들은 썩으면 아무 소리 없이 통과한다.</b> 그래서 이 파일은 세 겹을 깐다.
    /// <list type="number">
    ///  <item><b>존재 대조</b> — 승인된 1파일에 실제 호출이 <b>1건 이상</b> 있는가.
    ///    없으면 「어디에도 없다」는 사실이 <b>「아무도 안 쓴다」</b>는 다른 사실로 바뀐 것이고,
    ///    그때는 이 감사의 부재 단언 전체를 폐기해야 한다.</item>
    ///  <item><b>스캐너 자가 대조</b> — 가짜 소스에 실제 호출을 심어 <b>찾아내는지</b>,
    ///    같은 문자열을 <b>주석 안에만</b> 두면 <b>안 찾는지</b>를 매 실행 증명한다.</item>
    ///  <item><b>식별자는 <c>nameof()</c>로 잡는다</b> — 문자열로 베끼면 개명 한 번에 조용히 죽는다.
    ///    파일도 이름으로 지목하지 않고 <b>«그 타입을 선언한 파일»</b>로 찾는다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★★ 주석 제거가 <b>필수</b>다 — 안 하면 첫 실행부터 거짓 빨강이다
    /// ============================================================================
    /// <c>Core/PackRegistry.cs</c>에 <c>&lt;see cref="PackEntitlements.StateOf"/&gt;</c>가
    /// <b>원래부터 주석으로</b> 있고, <see cref="CostumeEntitlement"/>·<see cref="CostumeResolver"/>의
    /// 클래스 문서도 그 어법을 설명하느라 같은 문자열을 쓴다. <b>주석 속 언급은 계획이지 코드가 아니다.</b>
    /// <see cref="EntitlementAuditSource.StripComments"/>가 정확히 그 용도이고,
    /// 아래 <see cref="스캐너는_주석_속_언급을_배선으로_세지_않는다"/>가 그 단계가 살아 있음을 증명한다.
    /// </summary>
    public sealed class CostumeEntitlementSingleGateTests
    {
        private const string LogPrefix = "[코스튬게이트]";

        /// <summary>★ 문자열로 베끼지 않는다 — 개명하면 컴파일이 먼저 깨진다.</summary>
        private static readonly string GateCall =
            nameof(PackEntitlements) + "." + nameof(PackEntitlements.StateOf);

        /// <summary>수식 없는 형태까지 잡는 넓은 그물(<c>using static</c>·별칭 대비).</summary>
        private static readonly string GateMember = nameof(PackEntitlements.StateOf);

        /// <summary>정적 전역을 전부 원복한다.
        /// <para>★ <b><c>[TearDown]</c>을 하나로 합쳐 둔다</b> — NUnit은 같은 클래스에 <c>[TearDown]</c>이
        /// 여럿이면 <b>실행 순서를 보장하지 않는다</b>. 순서에 기대는 정리 코드는 어느 날 조용히
        /// 뒤집히고, 그 증상은 «가끔 실패하는 테스트»다.</para></summary>
        [TearDown]
        public void TearDown()
        {
            PackEntitlements.ClearTestOverride();
            CostumeCatalog.ResetForTesting();
            for (int i = 0; i < _made.Count; i++) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        private readonly List<Object> _made = new List<Object>();

        // ====================================================================
        // 0. 소스 스캔 도구 — 「어느 파일이 이 타입을 선언하는가」로 파일을 찾는다
        // ====================================================================

        private sealed class Scan
        {
            internal readonly string Path;
            internal readonly string Raw;
            internal readonly string Stripped;

            internal Scan(string path)
            {
                Path = path;
                Raw = File.ReadAllText(path);
                Stripped = EntitlementAuditSource.StripComments(Raw);
            }

            internal string Name => System.IO.Path.GetFileName(Path);
        }

        private static List<Scan> ProductionScans()
        {
            string[] files = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(files.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {files.Length}개밖에 못 읽었습니다 " +
                $"(바닥값 {EntitlementAuditSource.MinProductionFileCount}) — 스캔이 공허합니다. " +
                "이 상태의 «0건»은 «없다»가 아니라 «못 본다»입니다.");

            var scans = new List<Scan>(files.Length);
            for (int i = 0; i < files.Length; i++) scans.Add(new Scan(files[i]));
            return scans;
        }

        /// <summary><paramref name="typeName"/>을 <b>선언한</b> 파일 정확히 1개를 돌려준다.
        /// <para>★ 파일명을 손으로 적지 않는 이유: 이 저장소는 <b>대소문자 오타 하나로</b>
        /// 「깨끗함」과 「경로가 틀림」을 구분 못 한 사고를 겪었다(TEAM.md 12번째 형태).
        /// 타입 선언으로 찾으면 파일이 옮겨 가거나 이름이 바뀌어도 <b>따라간다</b>.</para></summary>
        private static Scan DeclaringFile(List<Scan> scans, string typeName)
        {
            var hits = new List<Scan>();
            for (int i = 0; i < scans.Count; i++)
            {
                if (EntitlementAuditSource.DeclaresType(scans[i].Stripped, typeName)) hits.Add(scans[i]);
            }
            Assert.AreEqual(1, hits.Count,
                $"{LogPrefix} '{typeName}'을 선언한 프로덕션 파일이 {hits.Count}개입니다(기대 1). " +
                "0개면 이 감사는 아무 파일도 면제하지 못한 채 돌고, 2개 이상이면 타입이 갈라진 것입니다.");
            return hits[0];
        }

        // ====================================================================
        // 1. ★ 본체 — 게이트 참조는 승인된 한 파일에만 있다
        // ====================================================================

        [Test]
        public void 게이트_참조는_승인된_한_파일에만_있다()
        {
            List<Scan> scans = ProductionScans();

            Scan producer = DeclaringFile(scans, nameof(PackEntitlements));
            Scan consumer = DeclaringFile(scans, nameof(CostumeEntitlement));

            // ---- (1) 존재 대조 — 승인된 1파일에 실제 호출이 살아 있는가 ----
            int consumerCalls = EntitlementAuditSource.CountIdentifier(consumer.Stripped, GateCall);
            Assert.GreaterOrEqual(consumerCalls, 1,
                $"{LogPrefix} ★ <b>존재 대조 실패</b> — 승인된 소비자 '{consumer.Name}'에 " +
                $"'{GateCall}' 호출이 0건입니다.\n" +
                "그러면 아래 «다른 어디에도 없다»는 «게이트를 한 곳에 가뒀다»가 아니라 " +
                "<b>«아무도 게이트를 안 쓴다»</b>는 전혀 다른 사실입니다 — 이 테스트의 부재 단언을 " +
                "통째로 폐기하십시오(CLAUDE.md: 부재 단언은 썩으면 조용히 초록이 된다).");

            // ---- (2) 생산자 파일은 선언이라 면제된다. 그 사실도 실측으로 확인한다 ----
            Assert.GreaterOrEqual(EntitlementAuditSource.CountIdentifier(producer.Stripped, GateMember), 1,
                $"{LogPrefix} 생산자 '{producer.Name}'에 '{GateMember}' 선언이 안 보입니다 — " +
                "면제 대상을 잘못 골랐다는 뜻이고, 그러면 진짜 생산자가 위반으로 잡힙니다.");

            // ---- (3) 본론(부재) — 그 둘 말고는 0건 ----
            var violations = new List<string>();
            foreach (Scan s in scans)
            {
                if (ReferenceEquals(s, producer) || ReferenceEquals(s, consumer)) continue;

                int qualified = EntitlementAuditSource.CountIdentifier(s.Stripped, GateCall);
                int bare = EntitlementAuditSource.CountIdentifier(s.Stripped, GateMember);
                if (qualified == 0 && bare == 0) continue;

                violations.Add($"{s.Name} → 수식형 {qualified}건 / 무수식 {bare}건");
            }

            Assert.IsEmpty(violations,
                $"{LogPrefix} ★ <b>C층 판정이 두 곳 이상이 됐습니다</b>({violations.Count}파일):\n  · " +
                string.Join("\n  · ", violations) + "\n\n" +
                $"개방 판정의 유일한 입구는 '{consumer.Name}'입니다(계약서 I-5). 판정이 갈라지면 " +
                "「반만 열린」 상태가 만들어지고 — 프롭은 뜨는데 포즈는 안 바뀌거나, 세션 시작에는 " +
                "열렸는데 종료에는 닫힌 형태 — 그 화면은 <b>「기능이 아직 안 붙었다」와 구별되지 않습니다</b>.\n" +
                $"면제된 것은 생산자('{producer.Name}')와 승인된 소비자('{consumer.Name}') 둘뿐입니다.");

            // ---- (4) 주석 제거가 <b>이 저장소에서 실제로 일을 하는가</b>(관측 기록) ----
            int rawFiles = 0, strippedFiles = 0;
            var commentOnly = new List<string>();
            foreach (Scan s in scans)
            {
                bool inRaw = EntitlementAuditSource.ContainsIdentifier(s.Raw, GateCall);
                bool inCode = EntitlementAuditSource.ContainsIdentifier(s.Stripped, GateCall);
                if (inRaw) rawFiles++;
                if (inCode) strippedFiles++;
                if (inRaw && !inCode) commentOnly.Add(s.Name);
            }

            Debug.Log($"{LogPrefix} 프로덕션 {scans.Count}파일 — 게이트 호출 {consumerCalls}건이 " +
                      $"'{consumer.Name}' 한 곳에, 그 밖 0건. " +
                      $"주석 포함 스캔 {rawFiles}파일 vs 주석 제거 스캔 {strippedFiles}파일" +
                      (commentOnly.Count > 0
                          ? $" — 주석에만 있는 파일 {commentOnly.Count}개({string.Join(", ", commentOnly)})가 " +
                            "주석 제거 없이 돌렸을 때의 거짓 빨강 후보다."
                          : " — 오늘 주석 전용 언급은 0건이다(주석 제거의 값은 아래 합성 대조가 증명한다)."));
        }

        /// <summary>
        /// ★ 스캐너 자가 대조 — <b>결정적</b>이다(저장소 주석이 어떻게 바뀌든 흔들리지 않는다).
        /// <list type="bullet">
        ///  <item>실제 호출은 <b>찾는다</b>(못 찾으면 위 «0건»은 «없다»가 아니라 «못 본다»다).</item>
        ///  <item>같은 문자열이 <b>주석 안에만</b> 있으면 <b>안 찾는다</b>(찾으면 첫 실행부터 거짓 빨강이다).</item>
        /// </list>
        /// </summary>
        [Test]
        public void 스캐너는_주석_속_언급을_배선으로_세지_않는다()
        {
            string call = "        if (" + GateCall + "(id) == PackEntitlementState.Owned) return true;\n";

            // (가) 양성 — 실제 호출을 찾는다.
            string stripped = EntitlementAuditSource.StripComments(call);
            Assert.AreEqual(1, EntitlementAuditSource.CountIdentifier(stripped, GateCall),
                $"{LogPrefix} 스캐너가 <b>명백한 호출</b>을 못 찾았습니다 — " +
                "그러면 이 파일의 모든 «0건»은 «없다»가 아니라 «못 본다»입니다.");

            // (나) 음성 — 세 가지 주석 형태 전부에서 0건이어야 한다.
            string[] commentForms =
            {
                "    /// <see cref=\"" + GateCall + "\"/>\n    public void X() { }\n",
                "    // " + GateCall + " 를 여기서 부르지 마라\n    public void Y() { }\n",
                "    /* " + GateCall + " */\n    public void Z() { }\n",
            };
            for (int i = 0; i < commentForms.Length; i++)
            {
                string s = EntitlementAuditSource.StripComments(commentForms[i]);
                Assert.AreEqual(0, EntitlementAuditSource.CountIdentifier(s, GateCall),
                    $"{LogPrefix} 주석 형태 {i}에서 주석 속 언급이 <b>배선으로 세어졌습니다</b>. " +
                    "이 저장소에는 그런 주석이 실재하므로(예: PackRegistry의 <see cref>), " +
                    "이 감사는 첫 실행부터 거짓 빨강을 냅니다.");
            }

            // (다) 두 형태를 <b>한 파일에</b> 섞어도 코드 쪽만 센다.
            string mixed = EntitlementAuditSource.StripComments(commentForms[0] + call + commentForms[1]);
            Assert.AreEqual(1, EntitlementAuditSource.CountIdentifier(mixed, GateCall),
                $"{LogPrefix} 주석과 코드가 섞인 파일에서 셈이 틀렸습니다.");

            // (라) 낱말 경계 — <c>WhenStateOff</c> 같은 이름이 오탐되지 않는다(실재하는 이름이다).
            Assert.AreEqual(0, EntitlementAuditSource.CountIdentifier("int x = (int)WhenStateOff;", GateMember),
                $"{LogPrefix} 낱말 경계가 깨져 다른 식별자가 게이트로 잡혔습니다.");

            Debug.Log($"{LogPrefix} 스캐너 자가 대조 — 호출 1건 검출 / 주석 3형태 전부 0건 / 낱말 경계 정상.");
        }

        // ====================================================================
        // 2. 게이트가 <b>실제로</b> 어떻게 답하는가 (소스 형태가 아니라 동작)
        // ====================================================================

        /// <summary>답을 세면서 돌려주는 가짜 스토어. <b>호출 횟수</b>가 이 절의 본론이다 —
        /// 「무료는 안 묻는 것」은 반환값이 아니라 <b>묻지 않았다는 사실</b>로만 잴 수 있다.</summary>
        private sealed class CountingSource : IPackEntitlementSource
        {
            internal PackEntitlementState Answer;
            internal int Calls;

            internal CountingSource(PackEntitlementState answer) { Answer = answer; }

            public PackEntitlementState Query(string packId)
            {
                Calls++;
                return Answer;
            }
        }

        private PackDescriptor MakePack(string packId, bool paid)
        {
            var m = ScriptableObject.CreateInstance<StickPackManifestSO>();
            _made.Add(m);
            m.name = packId;
            m.packId = packId;
            m.entitlements = paid
                ? new[] { new PackEntitlementRef { channel = PackStoreChannel.Steam, entitlementId = "900001" } }
                : new PackEntitlementRef[0];
            return new PackDescriptor(m, 0);
        }

        private CostumeDescriptor MakeCostume(string key, CostumeSourceKind kind, string sourceId)
        {
            var m = ScriptableObject.CreateInstance<CostumeManifestSO>();
            _made.Add(m);
            m.name = key;
            m.costumeKey = key;
            m.requiresSchemaVersion = CostumeManifestSO.SchemaVersion;
            m.sourceKind = kind;
            m.sourceId = sourceId;
            m.displayNameKey = key + ".name";

            var faults = new List<string>();
            CostumeDescriptor[] loaded = CostumeCatalog.Build(new[] { m }, faults);
            Assert.IsEmpty(faults,
                $"{LogPrefix} 픽스처 코스튬이 거부됐습니다 — 아래 단언은 «코스튬이 없어서» " +
                "통과하게 됩니다:\n  · " + string.Join("\n  · ", faults));
            Assert.AreEqual(1, loaded.Length);
            return loaded[0];
        }

        /// <summary>
        /// ★★ <b>무료는 「안 묻는 것」이지 「<c>Owned</c>를 받는 것」이 아니다</b>(규칙 C-1 · M-8).
        /// <para>오늘 트리에서 물으면 <c>NullPackEntitlementSource</c>가 <c>Unknown</c>을 주고,
        /// <c>Unknown</c>은 «새로 시작 거부»라 <b>무료 팩까지 통째로 닫힌다</b> — 그리고 그 화면은
        /// «기능 미구현»과 완전히 같다. 그래서 <b>반환값이 아니라 「호출 0회」</b>를 잰다.</para>
        /// </summary>
        [Test]
        public void 무료_팩에는_스토어를_아예_묻지_않는다()
        {
            var source = new CountingSource(PackEntitlementState.NotOwned);   // 물으면 «닫힘»이 나올 답
            PackEntitlements.SetTestOverride(source);

            PackDescriptor free = MakePack("costumefixture.free", paid: false);
            Assert.IsTrue(CostumeEntitlement.IsOpen(free),
                $"{LogPrefix} 채널 항목이 0개인 팩이 닫혔습니다 — 무료가 «안 묻는 것»이 아니라 " +
                "«물어서 통과하는 것»이 되면, 스토어가 안 뜬 아침마다 무료 연출이 사라집니다.");
            Assert.AreEqual(0, source.Calls,
                $"{LogPrefix} ★ 무료 팩인데 스토어를 {source.Calls}번 물었습니다. " +
                "지금은 가짜 출처가 답을 주니 초록이지만, 출하 트리의 기본 출처는 <b>언제나 Unknown</b>이라 " +
                "그 순간 <b>무료 오피스까지 포함해 전원이 아무것도 못 봅니다</b>.");

            // 사유 키를 만드는 경로에서도 묻지 않는다(★ 문자열을 베끼지 않는다 — 아래
            // [닫힌_사유는_상황마다_서로_다른_키다]가 키 자체의 성질을 관계로만 잰다).
            CostumeDescriptor costume = MakeCostume("costumefixture.freecos", CostumeSourceKind.Pack,
                "costumefixture.free");
            CostumeEntitlement.ClosedReasonKey(costume, free);
            Assert.AreEqual(0, source.Calls,
                $"{LogPrefix} 사유 키를 만드느라 스토어를 물었습니다({source.Calls}회).");

            Debug.Log($"{LogPrefix} 무료 갈래 — 열림 / 스토어 조회 {source.Calls}회.");
        }

        /// <summary>
        /// <c>Unknown</c>은 <c>NotOwned</c>가 <b>아니지만</b>, «새로 시작»은 거부한다(§E-2).
        /// <para>★ 그리고 <b>사유 키가 서로 달라야 한다</b>(§E-2-a) — 같은 문구를 쓰면
        /// 오프라인 유저에게 <i>"당신은 안 샀습니다"</i>라고 말하게 된다.</para>
        /// </summary>
        [Test]
        public void 유료_팩은_보유일_때만_열리고_미확인은_미보유와_다른_사유를_준다()
        {
            PackDescriptor paid = MakePack("costumefixture.paid", paid: true);
            CostumeDescriptor costume = MakeCostume("costumefixture.paidcos", CostumeSourceKind.Pack,
                "costumefixture.paid");

            var owned = new CountingSource(PackEntitlementState.Owned);
            PackEntitlements.SetTestOverride(owned);
            Assert.IsTrue(CostumeEntitlement.IsOpen(costume, paid),
                $"{LogPrefix} 보유가 확인됐는데 닫혔습니다 — 산 사람이 못 보는 상태입니다.");
            Assert.GreaterOrEqual(owned.Calls, 1,
                $"{LogPrefix} 유료 팩인데 스토어를 <b>한 번도 안 물었습니다</b> — " +
                "그러면 위 «열림»은 판정이 아니라 우연입니다(무료 갈래로 새어 들어갔을 수 있습니다).");

            var notOwned = new CountingSource(PackEntitlementState.NotOwned);
            PackEntitlements.SetTestOverride(notOwned);
            Assert.IsFalse(CostumeEntitlement.IsOpen(costume, paid),
                $"{LogPrefix} 미보유가 확인됐는데 열렸습니다 — 유료 연출이 그대로 샙니다.");

            var unknown = new CountingSource(PackEntitlementState.Unknown);
            PackEntitlements.SetTestOverride(unknown);
            Assert.IsFalse(CostumeEntitlement.IsOpen(costume, paid),
                $"{LogPrefix} 미확인인데 «새로 시작»이 허용됐습니다(§E-2 위반).");

            // ★ 사유 키는 달라야 한다. 문자열을 베끼지 않고 <b>생산자에게 물어서</b> 대조한다.
            PackEntitlements.SetTestOverride(new CountingSource(PackEntitlementState.NotOwned));
            string notOwnedKey = CostumeEntitlement.ClosedReasonKey(costume, paid);
            PackEntitlements.SetTestOverride(new CountingSource(PackEntitlementState.Unknown));
            string unknownKey = CostumeEntitlement.ClosedReasonKey(costume, paid);

            Assert.AreEqual(PackEntitlements.ReasonKey(PackEntitlementState.NotOwned), notOwnedKey,
                $"{LogPrefix} 미보유 사유 키가 C층 생산자의 것과 다릅니다 — 사유 문구가 두 벌이 됐습니다.");
            Assert.AreEqual(PackEntitlements.ReasonKey(PackEntitlementState.Unknown), unknownKey,
                $"{LogPrefix} 미확인 사유 키가 C층 생산자의 것과 다릅니다.");
            Assert.AreNotEqual(notOwnedKey, unknownKey,
                $"{LogPrefix} ★ 미보유와 미확인이 <b>같은 문구</b>를 씁니다(§E-2-a 위반) — " +
                "스팀이 아직 안 뜬 아침에 정상 결제한 사용자에게 «당신은 안 샀습니다»라고 말하게 됩니다.");

            Debug.Log($"{LogPrefix} 유료 갈래 — Owned 열림 / NotOwned·Unknown 닫힘 / " +
                      $"사유 키 '{notOwnedKey}' ≠ '{unknownKey}'.");
        }

        /// <summary>
        /// 팩 서술자가 없으면 <b>닫힘</b>이다. 그 조합이 「고아 코호트」(아이템은 팩 소속이라 적었는데
        /// 매니페스트가 없다)이고, <b>살 방법이 없는 상태</b>라 열어 주면 «살 수도 없는 것을 보여 주는 것»이 된다.
        /// <para>★ 반대로 <b>기본 코호트 코스튬은 팩이 없어도 열린다</b> — 무료는 안 묻는 것이므로.
        /// 두 사실을 <b>같은 테스트</b>에 둔다: 하나만 보면 «null이면 닫힘»과 «null이면 열림» 중
        /// 어느 규칙이 도는지 알 수 없다.</para>
        /// </summary>
        [Test]
        public void 고아_코호트는_닫히고_기본코호트_코스튬은_팩_없이도_열린다()
        {
            var source = new CountingSource(PackEntitlementState.Owned);   // 물으면 «열림»이 나올 답
            PackEntitlements.SetTestOverride(source);

            Assert.IsFalse(CostumeEntitlement.IsOpen((PackDescriptor)null),
                $"{LogPrefix} 팩 서술자가 null인데 열렸습니다 — 고아 코호트는 살 방법이 없는 상태입니다.");

            CostumeDescriptor packCostume = MakeCostume("costumefixture.orphan", CostumeSourceKind.Pack,
                "costumefixture.nopack");
            Assert.IsFalse(CostumeEntitlement.IsOpen(packCostume, null),
                $"{LogPrefix} 팩 소속 코스튬이 매니페스트 없이 열렸습니다.");

            CostumeDescriptor baseCostume = MakeCostume("costumefixture.base", CostumeSourceKind.BaseTheme,
                ItemCatalog.ThemeOffice);
            Assert.IsTrue(CostumeEntitlement.IsOpen(baseCostume, null),
                $"{LogPrefix} 기본 코호트 코스튬이 팩이 없다는 이유로 닫혔습니다 — " +
                "무료 갈래는 물어볼 대상 자체가 없습니다(M-8).");
            Assert.AreEqual(0, source.Calls,
                $"{LogPrefix} 기본 코호트 갈래에서 스토어를 {source.Calls}번 물었습니다.");

            // null 코스튬은 «모르는 코스튬»이고 닫힘이다.
            Assert.IsFalse(CostumeEntitlement.IsOpen(null, null),
                $"{LogPrefix} 서술자가 null인데 열렸습니다 — «모르는 코스튬»은 닫힘입니다.");

            Debug.Log($"{LogPrefix} 고아 닫힘 / 기본 코호트 열림 / 스토어 조회 {source.Calls}회.");
        }

        /// <summary>
        /// 닫힌 이유는 <b>상황마다 다른 키</b>여야 한다. 같은 키를 쓰면 화면이 틀린 이유를 말한다 —
        /// 오프라인 유저에게 <i>"당신은 안 샀습니다"</i>, 고아 코호트에 <i>"미확인입니다"</i> 같은 식이다.
        ///
        /// <para>★ <b>기대값에 문자열을 한 개도 베끼지 않는다.</b> CLAUDE.md 규칙(식별자·상수를 문자열로
        /// 베끼지 마라)이 여기에 정면으로 걸린다 — 사유 키는 오늘 상수로 노출돼 있지 않으므로
        /// <b>값</b> 대신 <b>관계</b>를 잰다: (가) 두 «열림» 상황은 <b>같은</b> 키다,
        /// (나) 네 상황은 <b>서로 다른</b> 키다, (다) 전부 <b>키 모양</b>이다(원문 금지).
        /// 이 셋은 문구가 바뀌어도 살아남고, <b>계약이 깨지면 반드시 빨개진다</b>.</para>
        /// </summary>
        [Test]
        public void 닫힌_사유는_상황마다_서로_다른_키다()
        {
            PackDescriptor free = MakePack("costumefixture.rfree", paid: false);
            PackDescriptor paid = MakePack("costumefixture.rpaid", paid: true);
            CostumeDescriptor packCostume = MakeCostume("costumefixture.rpackcos", CostumeSourceKind.Pack,
                "costumefixture.rpaid");
            CostumeDescriptor baseCostume = MakeCostume("costumefixture.rbasecos", CostumeSourceKind.BaseTheme,
                ItemCatalog.ThemeOffice);

            PackEntitlements.SetTestOverride(new CountingSource(PackEntitlementState.NotOwned));
            string notOwned = CostumeEntitlement.ClosedReasonKey(packCostume, paid);
            PackEntitlements.SetTestOverride(new CountingSource(PackEntitlementState.Unknown));
            string unknown = CostumeEntitlement.ClosedReasonKey(packCostume, paid);

            string openViaFreePack = CostumeEntitlement.ClosedReasonKey(packCostume, free);
            string openViaBaseTheme = CostumeEntitlement.ClosedReasonKey(baseCostume, null);
            string noPack = CostumeEntitlement.ClosedReasonKey(packCostume, null);
            string unknownCostume = CostumeEntitlement.ClosedReasonKey(null, null);

            // (가) 두 «열림» 경로는 같은 사실을 말한다.
            Assert.AreEqual(openViaFreePack, openViaBaseTheme,
                $"{LogPrefix} 무료 팩과 기본 코호트가 <b>다른 «열림» 문구</b>를 씁니다 " +
                $"('{openViaFreePack}' vs '{openViaBaseTheme}') — 같은 사실에 두 문구가 생기면 " +
                "화면이 «무료인데 왜 다르게 쓰였나»를 만들게 됩니다.");

            // (나) 나머지 넷은 서로 달라야 한다.
            var distinct = new Dictionary<string, string>
            {
                { "열림", openViaFreePack },
                { "팩 매니페스트 없음(고아 코호트)", noPack },
                { "모르는 코스튬", unknownCostume },
                { "미보유 확인됨", notOwned },
                { "미확인(스토어를 못 물었다)", unknown },
            };
            var seen = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in distinct)
            {
                Assert.IsFalse(string.IsNullOrEmpty(pair.Value),
                    $"{LogPrefix} 상황 「{pair.Key}」의 사유 키가 비었습니다.");
                Assert.IsTrue(PackManifestKeys.IsWellFormed(pair.Value),
                    $"{LogPrefix} 상황 「{pair.Key}」의 사유가 <b>키 모양이 아닙니다</b>('{pair.Value}'). " +
                    "Core는 완성 문장을 만들지 않습니다 — 원문이 여기 박히면 로컬라이즈가 첫날부터 깨집니다.");
                Assert.IsFalse(seen.ContainsKey(pair.Value),
                    $"{LogPrefix} ★ 상황 「{pair.Key}」와 「{(seen.ContainsKey(pair.Value) ? seen[pair.Value] : "?")}」가 " +
                    $"<b>같은 사유 키</b>('{pair.Value}')를 씁니다. 화면이 틀린 이유를 말하게 됩니다 — " +
                    "특히 «미확인»과 «미보유»가 같아지면 스팀이 안 뜬 아침에 정상 결제자에게 " +
                    "«당신은 안 샀습니다»라고 말합니다(§E-2-a).");
                seen[pair.Value] = pair.Key;
            }

            Assert.AreEqual(distinct.Count, seen.Count,
                $"{LogPrefix} 사유 키 {distinct.Count}종을 기대했는데 {seen.Count}종만 나왔습니다.");

            Debug.Log($"{LogPrefix} 사유 키 {seen.Count}종 전부 구분됨: " +
                      string.Join(" / ", new List<string>(seen.Keys)));
        }

        // ====================================================================
        // 3. 인접 경계 감사 — 계약서가 <b>같은 라운드에</b> 요구한 부재 단언 2건
        // ====================================================================

        /// <summary>
        /// ★★ 규칙 C-3(계약서 3-5절) — <b>해석기와 개방 판정은 세이브를 절대 읽지 않는다.</b>
        /// 저장된 것은 <i>"입은 채 몇 분을 보냈는가"</i>이지 <i>"가졌는가"</i>가 아니다.
        /// 그 둘을 섞는 순간 <b>세이브 파일이 결제 우회 표적이 된다</b> — C층을 세이브에서 뺀 목적이
        /// 방어 추가가 아니라 <b>표적 제거</b>였다(§E-4-a).
        /// <para>★ <b>존재 대조 동반</b>: 같은 니들이 다른 프로덕션 파일에서는 <b>실제로 잡히는지</b>
        /// 먼저 보인다. 안 잡히면 아래 0건은 «없다»가 아니라 «못 본다»다.</para>
        /// </summary>
        [Test]
        public void 해석기와_개방판정은_세이브를_읽지_않는다()
        {
            List<Scan> scans = ProductionScans();
            string[] forbidden = { nameof(CharacterSaveStore), nameof(CostumeProgressModel) };

            // ---- 존재 대조: 이 니들이 저장소 어딘가에서 실제로 잡히는가 ----
            foreach (string needle in forbidden)
            {
                int files = 0;
                foreach (Scan s in scans)
                {
                    if (EntitlementAuditSource.ContainsIdentifier(s.Stripped, needle)) files++;
                }
                Assert.GreaterOrEqual(files, 1,
                    $"{LogPrefix} 니들 '{needle}'이 프로덕션 어디에서도 안 잡힙니다 — " +
                    "타입 이름이 바뀌었거나 스캐너가 눈이 멀었습니다. " +
                    "아래 «0건»은 «없다»가 아니라 «못 본다»이므로 이 단언을 폐기하십시오.");
            }

            // ---- 본론(부재) ----
            Scan resolver = DeclaringFile(scans, nameof(CostumeResolver));
            Scan gate = DeclaringFile(scans, nameof(CostumeEntitlement));

            var violations = new List<string>();
            foreach (Scan s in new[] { resolver, gate })
            {
                foreach (string needle in forbidden)
                {
                    int hits = EntitlementAuditSource.CountIdentifier(s.Stripped, needle);
                    if (hits > 0) violations.Add($"{s.Name} → {needle} {hits}건");
                }
            }

            Assert.IsEmpty(violations,
                $"{LogPrefix} ★ <b>개방 판정 경로가 세이브를 읽습니다</b>({violations.Count}건):\n  · " +
                string.Join("\n  · ", violations) + "\n\n" +
                "«몇 분 쌓았는가»가 «지금 무엇을 입고 있는가»의 입력이 되면, 세이브 파일 한 줄이 " +
                "<b>유료 연출을 여는 표적</b>이 됩니다(규칙 C-3 · §E-4-a).");

            Debug.Log($"{LogPrefix} C-3 — '{resolver.Name}'·'{gate.Name}'에서 세이브 참조 0건 " +
                      $"(니들 {forbidden.Length}종 전부 다른 파일에서는 검출됨).");
        }

        /// <summary>
        /// ★★ 규칙 C-7(계약서 7-2절) — <b>진화는 재화 축과 연결되지 않는다.</b>
        /// 코스튬 단계를 <b>동전으로 앞당길 수 없어야</b> <i>"돈으로 살 수 있는 것 중에 그냥 플레이해서
        /// 얻는 것보다 센 것은 없다"</i>가 연출 축에서도 지켜진다.
        /// <para>★ <b>존재 대조가 바로 옆 파일에 있다</b>: <see cref="CostumeProgressModel"/>은
        /// «오늘이 며칠인가»를 위해 재화 모델을 <b>실제로 참조한다</b>(그건 달력 사실이지 잔액이 아니다).
        /// 같은 스캐너가 거기서 비어 있지 않은 답을 내야 아래 0건이 뜻을 갖는다.</para>
        /// </summary>
        [Test]
        public void 진화_규칙은_재화_모델을_참조하지_않는다()
        {
            List<Scan> scans = ProductionScans();
            string needle = nameof(CurrencyModel);

            Scan rules = DeclaringFile(scans, nameof(CostumeEvolutionRules));
            Scan progress = DeclaringFile(scans, nameof(CostumeProgressModel));

            // ---- 존재 대조 — 같은 스캐너가 이웃 파일에서는 실제로 잡는다 ----
            int neighbourHits = EntitlementAuditSource.CountIdentifier(progress.Stripped, needle);
            Assert.GreaterOrEqual(neighbourHits, 1,
                $"{LogPrefix} 존재 대조 실패 — '{progress.Name}'에서 '{needle}'이 0건입니다. " +
                "그 파일은 일자 롤오버를 위해 재화 모델의 «오늘»을 읽도록 설계됐습니다. " +
                "0건이면 스캐너가 눈이 멀었거나 설계가 바뀐 것이고, 아래 부재 단언은 무효입니다.");

            // ---- 본론(부재) ----
            int hits = EntitlementAuditSource.CountIdentifier(rules.Stripped, needle);
            Assert.AreEqual(0, hits,
                $"{LogPrefix} ★ <b>진화 규칙이 재화 모델을 참조합니다</b>({hits}건, {rules.Name}). " +
                "동전으로 단계를 앞당길 수 있는 경로가 생기면 그건 페이투윈 축입니다(규칙 C-7).");

            // 소프트캡조차 <b>인자로 받는다</b>는 형태 — 모델을 조회하기 시작하면 참조가 생긴다.
            Assert.AreEqual(0, EntitlementAuditSource.CountIdentifier(rules.Stripped, nameof(CostumeProgressModel)),
                $"{LogPrefix} 진화 규칙이 누적 모델을 조회합니다 — " +
                "«어떤 모델도 조회하지 않는다»가 깨지면 C-7의 방어선이 한 겹 얇아집니다.");

            Debug.Log($"{LogPrefix} C-7 — '{rules.Name}'에서 '{needle}' 0건 " +
                      $"(존재 대조: 이웃 '{progress.Name}'에서는 {neighbourHits}건 검출).");
        }
    }
}
