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
    /// ★★★ 이 라운드의 합격 기준 하나 — <b>일곱 번째 팩을 프로덕션 <c>.cs</c> 0줄로 넣을 수 있는가</b>
    /// ============================================================================
    /// 사용자 확정 2026-09-03: <b>"6팩모두"</b> + <b>"출시 이후부터 계속 추가팩 만들거야"</b>.
    /// 두 번째가 설계를 바꾼다 — 필요한 것은 「6팩짜리 통로」가 아니라 <b>「N팩짜리 통로」</b>다.
    /// <c>CLAUDE.md</c> 절대 불변 원칙 4(<i>"신규 DLC는 기본 로직 무수정으로 매니페스트를 통해 추가"</i>)가
    /// 이제 <b>사업 모델 자체</b>이므로, 그 원칙을 산문이 아니라 <b>측정</b>으로 옮긴다.
    ///
    /// ============================================================================
    /// ★ 왜 <b>양성 대조</b>가 이 파일의 절반인가
    /// ============================================================================
    /// "일곱 번째가 들어갔다"는 초록은 <b>아무것도 증명하지 않는다</b> — 검사가 애초에 무는지 모르기 때문이다.
    /// 이 저장소가 반복해 당한 형태가 정확히 그것이다(TEAM.md §4:
    /// <i>"실패한 측정과 성공한 측정이 똑같이 생겼다"</i>).
    /// 그래서 같은 입력에 <b>하드코딩 판</b>(<see cref="HardcodedSixPackGate"/>)을 나란히 세운다:
    /// <list type="bullet">
    ///  <item><see cref="PackRegistry"/> 는 일곱 번째를 <b>싣는다.</b></item>
    ///  <item>하드코딩 판은 일곱 번째에서 <b>넘어진다.</b></item>
    /// </list>
    /// 둘 다 같은 단언 함수를 통과시키므로, 이 검사가 "무는 검사"라는 것이 <b>매 실행 증명된다.</b>
    ///
    /// ============================================================================
    /// 무엇을 재지 <b>않는가</b> (못 재는 것을 재는 척하지 않는다)
    /// ============================================================================
    ///  · <b>조형·색</b>. 이 라운드의 범위는 통로다(리더 지시). 도형은 <c>design-equipment</c> 인계본
    ///    이식 라운드가 채운다.
    ///  · <b>스토어 실기</b>. 스팀 SDK는 0줄이고 그 사실을 <c>OfflineFirstNetworkAuditTests</c> 가 잠근다.
    /// </summary>
    public sealed class PackManifestCorridorTests
    {
        private const string LogPrefix = "[팩통로]";

        /// <summary>합성 팩 개수의 출발점. <b>이 숫자에 뜻이 있는 것이 아니라</b>
        /// "확정된 6개"와 "그 다음 하나"를 가르는 자리라는 데 뜻이 있다.</summary>
        private const int ConfirmedPackCount = 6;

        /// <summary>합성 팩 아이디 접두. 실제 팩 아이디(<c>pack.office</c> 등)를 쓰지 <b>않는다</b> —
        /// 그러면 아래 <see cref="프로덕션에_팩_아이디가_리터럴로_박혀_있지_않다"/> 의 스캐너가
        /// 자기 픽스처와 진짜 배선을 구분할 수 없다.</summary>
        private const string SyntheticPrefix = "packfixture.";

        // ====================================================================
        // 합성 픽스처 — 매니페스트도 아이템도 <b>실제 변환 경로</b>로 만든다
        // ====================================================================

        private sealed class Fixture : IDisposable
        {
            internal readonly List<StickPackManifestSO> Manifests = new List<StickPackManifestSO>();
            internal readonly List<ItemCatalogEntry> Items = new List<ItemCatalogEntry>();
            private readonly List<Object> _made = new List<Object>();

            /// <summary>팩 하나를 통째로 만든다 — 매니페스트 1개 + 아이템 <paramref name="itemCount"/>개.
            /// <para>아이템은 <c>ItemCatalog.EntryFrom</c> 을 <b>실제로 태워서</b> 만든다.
            /// 합성 항목을 직접 조립하면 재는 것이 "판정"이 되고 "에셋 → 항목 배선"은 사각지대로 남는다
            /// (<c>ItemRarityDerivationTests</c> 가 같은 이유로 같은 경로를 쓴다).</para></summary>
            internal void AddPack(int ordinal, int itemCount)
            {
                var m = ScriptableObject.CreateInstance<StickPackManifestSO>();
                _made.Add(m);
                m.name = $"fixture_pack_{ordinal}";
                m.packId = SyntheticPrefix + "p" + ordinal;
                m.cohortId = ordinal;                       // 0 = 기본 코호트이므로 1부터
                m.requiresSchemaVersion = StickPackManifestSO.SchemaVersion;
                m.packVersion = 1;
                m.displayNameKey = SyntheticPrefix + "p" + ordinal + ".name";
                m.descriptionKey = SyntheticPrefix + "p" + ordinal + ".desc";
                m.paletteOrigin = PackPaletteOrigin.DedicatedHue;
                m.paletteHueDegrees = ordinal * 30f;        // 서로 다른 각
                m.primaryColor = Color.gray;
                m.secondaryColor = Color.gray;
                m.declaredItemCount = itemCount;
                m.itemIndexBase = ordinal * 100;            // 서로 겹치지 않는 자리
                m.dialogueSetKey = SyntheticPrefix + "p" + ordinal + ".tone";
                m.soundKeys = new[] { "land", "parkour" };
                m.entitlements = new[]
                {
                    new PackEntitlementRef
                    {
                        channel = PackStoreChannel.Steam,
                        entitlementId = "90000" + ordinal,
                    },
                };
                Manifests.Add(m);

                for (int i = 0; i < itemCount; i++)
                {
                    var def = ScriptableObject.CreateInstance<AccessoryDefSO>();
                    _made.Add(def);
                    def.itemId = m.packId + ".item" + i;
                    def.slot = (EquipmentSlot)(i % EquipmentModel.SlotCount);
                    def.itemIndex = m.itemIndexBase + i;
                    def.displayName = def.itemId;
                    def.description = def.itemId;
                    def.requiredLevel = ItemCatalog.PackRequiredLevel;
                    def.cohortId = m.cohortId;
                    def.declaredRarity = ItemCatalog.MaxDeclaredRarityForPack;
                    Items.Add(ItemCatalog.EntryFrom(def));
                }
            }

            /// <summary>기본 42종 흉내 — 코호트를 <b>적지 않는다</b>(실제 <c>.asset</c> 과 같은 상태).</summary>
            internal void AddBaseItems(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var def = ScriptableObject.CreateInstance<AccessoryDefSO>();
                    _made.Add(def);
                    def.itemId = "fixture.base." + i;
                    def.slot = EquipmentSlot.Head;
                    def.itemIndex = i;
                    def.displayName = def.itemId;
                    def.description = def.itemId;
                    def.requiredLevel = 1 + i;
                    Items.Add(ItemCatalog.EntryFrom(def));
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < _made.Count; i++) Object.DestroyImmediate(_made[i]);
                _made.Clear();
            }
        }

        private static Fixture BuildFixture(int packCount)
        {
            var f = new Fixture();
            f.AddBaseItems(6);
            for (int p = 1; p <= packCount; p++)
            {
                // ★ 마지막 팩만 아이템 수를 다르게 준다. "6종"이 어딘가에 박혀 있으면 여기서 걸린다.
                f.AddPack(p, p == packCount && packCount > ConfirmedPackCount ? 4 : 6);
            }
            return f;
        }

        // ====================================================================
        // ★ 양성 대조용 하드코딩 판 — "만약 이렇게 만들었다면" 을 실제로 세워 본다
        // ====================================================================

        /// <summary>
        /// ★ <b>이 클래스가 이 파일의 자(尺)다.</b> 팩 목록을 <c>switch</c> 로 박아 둔,
        /// 이 라운드가 만들지 <b>않기로</b> 한 판. 여섯까지는 <see cref="PackRegistry"/> 와
        /// 구분되지 않고 <b>일곱 번째에서만</b> 갈린다.
        ///
        /// <para>왜 필요한가: 통로 검사가 초록이어도 그것이 "통로라서 통과"인지
        /// "검사가 아무것도 안 재서 통과"인지 알 수 없다. 이 판이 <b>같은 단언에서 빨개짐</b>으로써
        /// 후자가 아님을 매 실행 증명한다.</para>
        /// </summary>
        private static class HardcodedSixPackGate
        {
            /// <summary>확정된 여섯 팩만 아는 목록. <b>일부러</b> 이렇게 짰다.</summary>
            internal static int CohortOf(string packId)
            {
                switch (packId)
                {
                    case SyntheticPrefix + "p1": return 1;
                    case SyntheticPrefix + "p2": return 2;
                    case SyntheticPrefix + "p3": return 3;
                    case SyntheticPrefix + "p4": return 4;
                    case SyntheticPrefix + "p5": return 5;
                    case SyntheticPrefix + "p6": return 6;
                }
                return -1;   // 모르는 팩
            }
        }

        /// <summary>두 판에 <b>똑같이</b> 던지는 질문. 통과 조건도 똑같다.</summary>
        private static bool ResolvesEveryPack(Func<string, int> cohortOf, IList<StickPackManifestSO> manifests,
            out string firstFailure)
        {
            for (int i = 0; i < manifests.Count; i++)
            {
                int got = cohortOf(manifests[i].packId);
                if (got == manifests[i].cohortId) continue;
                firstFailure = $"'{manifests[i].packId}' → 코호트 {got}(기대 {manifests[i].cohortId})";
                return false;
            }
            firstFailure = null;
            return true;
        }

        private static Func<string, int> RegistryResolver(PackDescriptor[] loaded)
        {
            return id =>
            {
                for (int i = 0; i < loaded.Length; i++) if (loaded[i].PackId == id) return loaded[i].CohortId;
                return -1;
            };
        }

        // ====================================================================
        // 1. 합격 기준 본체
        // ====================================================================

        [Test]
        public void 일곱번째_팩을_프로덕션_코드_한_줄도_안_고치고_넣을_수_있다()
        {
            using (Fixture six = BuildFixture(ConfirmedPackCount))
            using (Fixture seven = BuildFixture(ConfirmedPackCount + 1))
            {
                var sixFaults = new List<string>();
                PackDescriptor[] sixLoaded = PackRegistry.Build(six.Manifests, six.Items, sixFaults);
                Assert.IsEmpty(sixFaults,
                    $"{LogPrefix} 확정 {ConfirmedPackCount}팩조차 실리지 않습니다:\n  · " +
                    string.Join("\n  · ", sixFaults));
                Assert.AreEqual(ConfirmedPackCount, sixLoaded.Length,
                    $"{LogPrefix} {ConfirmedPackCount}팩을 넣었는데 {sixLoaded.Length}개만 실렸습니다.");

                var sevenFaults = new List<string>();
                PackDescriptor[] sevenLoaded = PackRegistry.Build(seven.Manifests, seven.Items, sevenFaults);

                Assert.IsEmpty(sevenFaults,
                    $"{LogPrefix} ★ <b>일곱 번째 팩이 거부됐습니다</b>({sevenFaults.Count}건):\n  · " +
                    string.Join("\n  · ", sevenFaults) + "\n\n" +
                    "사용자 확정은 「출시 이후부터 계속 추가팩」입니다. 여섯에서 멈추는 구조라면 " +
                    "그건 통로가 아니라 하드코딩입니다.");

                Assert.AreEqual(ConfirmedPackCount + 1, sevenLoaded.Length,
                    $"{LogPrefix} 일곱 번째가 실리지 않았습니다({sevenLoaded.Length}개).");

                // 조인이 실제로 붙었는가 — 개수만 맞고 코호트가 안 붙으면 "실렸다"가 거짓이다.
                PackDescriptor seventh = null;
                for (int i = 0; i < sevenLoaded.Length; i++)
                {
                    if (sevenLoaded[i].CohortId == ConfirmedPackCount + 1) seventh = sevenLoaded[i];
                }
                Assert.IsNotNull(seventh,
                    $"{LogPrefix} 일곱 번째 팩의 코호트({ConfirmedPackCount + 1})를 색인에서 못 찾았습니다.");
                Assert.AreEqual(4, seventh.ResolvedItemCount,
                    $"{LogPrefix} 일곱 번째 팩의 아이템 수가 조인되지 않았습니다. " +
                    "픽스처는 <b>일부러</b> 4종으로 만들었습니다 — 어딘가에 「팩은 6종」이 박혀 있으면 여기서 걸립니다.");
                Assert.AreEqual(seventh.DeclaredItemCount, seventh.ResolvedItemCount,
                    $"{LogPrefix} 선언 개수와 조인 개수가 다릅니다.");

                Debug.Log($"{LogPrefix} 합성 {ConfirmedPackCount + 1}팩 적재 성공 — " +
                          $"프로덕션 .cs 변경 0줄. 일곱 번째: {seventh.PackId} / 코호트 {seventh.CohortId} / " +
                          $"아이템 {seventh.ResolvedItemCount}종 / 자리 {seventh.ItemIndexBase}~");
            }
        }

        [Test]
        public void 양성대조_하드코딩판은_같은_입력의_일곱번째에서_넘어진다()
        {
            using (Fixture seven = BuildFixture(ConfirmedPackCount + 1))
            {
                var faults = new List<string>();
                PackDescriptor[] loaded = PackRegistry.Build(seven.Manifests, seven.Items, faults);
                Assert.IsEmpty(faults, $"{LogPrefix} 대조의 전제가 깨졌습니다(적재 결함 {faults.Count}건).");

                // (가) 통로 판 — 일곱 개 전부 푼다.
                bool corridorOk = ResolvesEveryPack(RegistryResolver(loaded), seven.Manifests, out string corridorFail);
                Assert.IsTrue(corridorOk,
                    $"{LogPrefix} 통로 판이 못 풀었습니다: {corridorFail}");

                // (나) 하드코딩 판 — <b>같은 단언 함수</b>에서 넘어져야 한다.
                bool hardcodedOk = ResolvesEveryPack(HardcodedSixPackGate.CohortOf, seven.Manifests, out string hardFail);
                Assert.IsFalse(hardcodedOk,
                    $"{LogPrefix} ★ <b>하드코딩 판이 일곱 번째를 통과했습니다.</b> " +
                    "그러면 이 파일의 모든 초록이 무효입니다 — 검사가 두 구조를 구분하지 못한다는 뜻이고, " +
                    "위 [일곱번째_팩을_...] 의 통과도 '통로라서'가 아니라 '아무것도 안 재서'일 수 있습니다.");
                Assert.IsNotNull(hardFail);
                StringAssert.Contains("p" + (ConfirmedPackCount + 1), hardFail,
                    $"{LogPrefix} 하드코딩 판이 <b>일곱 번째가 아닌 곳</b>에서 넘어졌습니다({hardFail}). " +
                    "그렇다면 이 대조는 「6 vs 7」이 아니라 다른 무언가를 재고 있습니다.");

                // (다) 여섯까지는 두 판이 <b>구분되지 않는다</b> — 대조가 7에서만 갈린다는 증명.
                using (Fixture six = BuildFixture(ConfirmedPackCount))
                {
                    var f6 = new List<string>();
                    PackDescriptor[] loaded6 = PackRegistry.Build(six.Manifests, six.Items, f6);
                    Assert.IsEmpty(f6);
                    Assert.IsTrue(ResolvesEveryPack(RegistryResolver(loaded6), six.Manifests, out _));
                    Assert.IsTrue(ResolvesEveryPack(HardcodedSixPackGate.CohortOf, six.Manifests, out _),
                        $"{LogPrefix} 하드코딩 판이 여섯에서 이미 넘어집니다 — 그러면 (나)의 빨강은 " +
                        "「7번째라서」가 아니라 픽스처가 원래 안 맞아서입니다.");
                }

                Debug.Log($"{LogPrefix} 양성 대조 통과 — 6팩에서는 두 판이 같고, 7팩에서 하드코딩 판만 넘어졌다: {hardFail}");
            }
        }

        // ====================================================================
        // 2. 발견 경로 — <b>같은 사실을 서로 다른 두 자로 잰다</b>
        // ====================================================================

        /// <summary>
        /// ★ 폴더 스캔이 <b>살아 있는 경로</b>인가. 오늘 매니페스트 에셋은 0개인데,
        /// <b>0개는 "팩이 없다"와 "폴더 이름이 틀렸다"를 구분하지 못한다.</b>
        ///
        /// <para>그래서 두 가지를 함께 한다:</para>
        /// <list type="number">
        ///  <item><b>양성 대조</b> — 같은 폴더 상수로 <c>AccessoryDefSO</c> 를 훑어 42개가 나오는지 본다.
        ///    나오면 그 경로는 죽은 문자열이 아니다.</item>
        ///  <item><b>다른 자</b> — <c>Resources.LoadAll</c>(엔진)이 센 매니페스트 수와,
        ///    <c>.cs.meta</c> 의 GUID로 <c>.asset</c> 을 직접 훑어 센 수(파일시스템)를 대조한다.
        ///    한쪽이 눈이 멀면 두 수가 갈라진다.</item>
        /// </list>
        /// <para>★ 애셋은 스크립트를 <b>GUID</b>로 참조하므로 타입 이름을 <c>grep</c> 하면
        /// 탐지력이 <b>애초에 0</b>이다(TEAM.md §4 사고 #4의 형태). 그래서 GUID로 찾는다.</para>
        /// </summary>
        [Test]
        public void 발견_경로는_살아_있고_두_자가_같은_수를_낸다()
        {
            string folder = PackRegistry.ResourceFolder;
            Assert.IsNotEmpty(folder, $"{LogPrefix} 폴더 상수가 비었습니다.");

            // (1) 양성 대조 — 이 폴더 상수로 실제로 무언가가 나오는가.
            AccessoryDefSO[] items = Resources.LoadAll<AccessoryDefSO>(folder);
            Assert.Greater(items.Length, 0,
                $"{LogPrefix} 폴더 '{folder}' 에서 아이템 에셋이 0개 나왔습니다. " +
                "그렇다면 아래 '매니페스트 0개'는 '없다'가 아니라 '못 본다'입니다.");

            // (2) 엔진이 센 매니페스트 수.
            StickPackManifestSO[] byEngine = Resources.LoadAll<StickPackManifestSO>(folder);

            // (3) 파일시스템이 센 수 — GUID로 직접.
            string[] byDisk = PackManifestLocalizationDebtTests.AssetsReferencingScript(nameof(StickPackManifestSO));
            string[] itemsByDisk = PackManifestLocalizationDebtTests.AssetsReferencingScript(nameof(AccessoryDefSO));

            Assert.AreEqual(items.Length, itemsByDisk.Length,
                $"{LogPrefix} 아이템 에셋 수가 두 자에서 다릅니다(엔진 {items.Length} / 디스크 {itemsByDisk.Length}). " +
                "한쪽 스캐너가 눈이 멀었으므로 이 테스트의 모든 수를 폐기하십시오.");

            Assert.AreEqual(byDisk.Length, byEngine.Length,
                $"{LogPrefix} 매니페스트 수가 두 자에서 다릅니다(디스크 {byDisk.Length} / 엔진 {byEngine.Length}).");

            Debug.Log($"{LogPrefix} 폴더 '{folder}' — 아이템 {items.Length}개(디스크 {itemsByDisk.Length}), " +
                      $"매니페스트 {byEngine.Length}개(디스크 {byDisk.Length}). " +
                      "매니페스트 0개는 오늘의 정상 상태다(조형 라운드가 채운다).");
        }

        // ====================================================================
        // 3. 통로가 <b>진짜</b>인가 — 프로덕션에 팩 아이디가 박혀 있으면 통로가 아니다
        // ====================================================================

        /// <summary>
        /// ★ 팩 아이디를 프로덕션 소스에 <b>리터럴로</b> 적는 순간 통로가 닫힌다.
        /// 일곱 번째 팩은 그 목록에 없을 것이고, 그 결과는 <b>조용한 미적재</b>다.
        ///
        /// <para><b>면제는 손으로 적지 않는다</b> — <see cref="PackEntitlements.ReasonKey"/> 를
        /// 세 상태에 대해 <b>실제로 불러</b> 얻는다. 문자열을 베껴 두면 그 키가 바뀌는 날
        /// 감사가 <b>조용히</b> 낡고, 이 저장소는 그 형태로 여러 번 당했다.</para>
        ///
        /// <para>★ <b>부재 단언이므로 대조를 붙인다</b>: 같은 스캐너를 <b>이 테스트 파일</b>에 흘려
        /// 합성 팩 아이디를 실제로 찾아내는지 먼저 보인다. 못 찾으면 아래 '0건'은
        /// '없다'가 아니라 '못 본다'이고, 그때는 이 테스트가 실패한다.</para>
        /// </summary>
        [Test]
        public void 프로덕션에_팩_아이디가_리터럴로_박혀_있지_않다()
        {
            var exempt = new List<string>();
            foreach (PackEntitlementState s in Enum.GetValues(typeof(PackEntitlementState)))
            {
                exempt.Add(PackEntitlements.ReasonKey(s));
            }
            Assert.IsNotEmpty(exempt, $"{LogPrefix} 면제 목록이 비었습니다(거짓 통과 #5: 빈 목록은 아무것도 재지 않습니다).");

            // --- 대조(양성): 스캐너가 팩 아이디 모양을 실제로 찾아내는가 ---
            //     하드코딩 판이 실제로 이렇게 생겼을 것이라는 가짜 소스를 같은 함수에 흘린다.
            const string fakeHardcoded =
                "static readonly string[] Packs = { \"pack.office\", \"pack.military\" };\n" +
                "switch (id) { case \"pack.graffiti\": return 3; }\n";
            List<string> inFake = PackIdLiterals(fakeHardcoded, exempt);
            Assert.AreEqual(3, inFake.Count,
                $"{LogPrefix} 스캐너가 <b>명백한 하드코딩</b>에서 팩 아이디를 {inFake.Count}건만 찾았습니다(기대 3). " +
                "그러면 아래 프로덕션 '0건'은 '없다'가 아니라 '못 본다'입니다: " + string.Join(", ", inFake));

            // --- 대조(음성): 면제 키는 잡히지 않는다(잡히면 이 감사는 첫날부터 빨간 채로 방치된다) ---
            List<string> exemptOnly = PackIdLiterals("return \"" + exempt[0] + "\";", exempt);
            Assert.IsEmpty(exemptOnly,
                $"{LogPrefix} 면제 키 '{exempt[0]}' 가 위반으로 잡혔습니다 — 방치된 경보는 없는 경보입니다.");

            // --- 본론(부재): 프로덕션에는 없어야 한다 ---
            var violations = new List<string>();
            string[] production = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(production.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs 를 {production.Length}개밖에 못 읽었습니다 — 스캔이 공허합니다.");

            foreach (string path in production)
            {
                string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));
                foreach (string literal in PackIdLiterals(stripped, exempt))
                {
                    violations.Add($"{Path.GetFileName(path)} → \"{literal}\"");
                }
            }

            Assert.IsEmpty(violations,
                $"{LogPrefix} 프로덕션 소스에 팩 아이디가 리터럴로 박혔습니다({violations.Count}건):\n  · " +
                string.Join("\n  · ", violations) + "\n\n" +
                "팩은 <b>에셋</b>으로 늘어납니다. 코드가 아이디를 알면 일곱 번째 팩은 그 목록에 없고, " +
                "증상은 예외가 아니라 <b>조용한 미적재</b>입니다.\n" +
                "면제된 것은 PackEntitlements.ReasonKey 가 실제로 돌려주는 사유 키뿐입니다: " +
                string.Join(", ", exempt));

            Debug.Log($"{LogPrefix} 팩 아이디 리터럴 — 프로덕션 {production.Length}파일에서 0건 " +
                      $"(양성 대조: 가짜 하드코딩에서 {inFake.Count}건 검출). 면제 {exempt.Count}건: " +
                      string.Join(", ", exempt));
        }

        /// <summary><c>"pack..."</c> 또는 <c>"packfixture..."</c> 꼴의 <b>문자열 리터럴</b>을 모은다.
        /// 정규식을 쓰지 않는다(.NET <c>\b</c> 가 한글을 낱말로 세는 함정 — 이 저장소가 46% 미탐을 낸 적이 있다).</summary>
        private static List<string> PackIdLiterals(string source, List<string> exempt)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(source)) return found;

            // ★ 따옴표 <b>짝</b>을 맞추지 않는다. 이 저장소의 소스에는 보간 문자열 안의 \" 이스케이프가
            //   흔하고, 짝 맞추기는 거기서 밀린다(그리고 밀린 결과는 '깨끗함'과 똑같이 생겼다).
            //   대신 «따옴표 + 접두» 로 시작해 <b>아이디 글자가 아닌 것</b>이 나올 때까지만 읽는다.
            foreach (string prefix in new[] { "\"pack.", "\"" + SyntheticPrefix })
            {
                int from = 0;
                while (true)
                {
                    int at = source.IndexOf(prefix, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    from = at + 1;

                    int p = at + 1;                       // 여는 따옴표 다음
                    int start = p;
                    while (p < source.Length && IsIdChar(source[p])) p++;
                    if (p == start) continue;

                    string inner = source.Substring(start, p - start);
                    if (inner.EndsWith(".", StringComparison.Ordinal)) inner = inner.Substring(0, inner.Length - 1);
                    if (inner.Length == 0) continue;
                    if (exempt.Contains(inner)) continue;
                    if (!found.Contains(inner)) found.Add(inner);
                }
            }
            return found;
        }

        private static bool IsIdChar(char c)
            => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_';
    }
}
