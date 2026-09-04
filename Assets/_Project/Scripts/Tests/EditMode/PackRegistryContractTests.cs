using System;
using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// 팩 매니페스트가 <b>거부되는 조건</b> — 전부 「조용히 반쯤 실리는 것」을 막는 자리다
    /// ============================================================================
    /// 이 파일의 모든 검사는 같은 모양이다: <b>정상 픽스처에서 한 칸만 망가뜨리고</b>,
    /// (가) 결함이 신고되는가 (나) 그 팩이 <b>안 실리는가</b> 를 함께 본다.
    /// 신고만 하고 싣는 구조는 최악이다 — 화면은 뜨는데 뜻이 다르고, 그건 화면만 봐서는 못 찾는다.
    ///
    /// <para>★ <b>정상 픽스처가 정말 정상인지</b>를 <see cref="정상_픽스처는_결함이_0건이다"/> 가
    /// 먼저 증명한다. 그것 없이는 아래 "빨개졌다"들이 <b>원래부터 빨간 것</b>일 수 있다
    /// (이 저장소가 반복해 당한 형태: 실패한 측정과 성공한 측정이 똑같이 생겼다).</para>
    ///
    /// <para><b>메시지 문자열을 베껴 단언하지 않는다</b>(CLAUDE.md). 문구가 바뀌면 검사가 조용히
    /// 낡기 때문이다. 대신 <b>결함 수</b>와 <b>적재 결과</b>, 그리고 <b>서로 다른 결함이 서로 다른
    /// 문장을 낸다</b>는 관계만 잰다.</para>
    /// </summary>
    public sealed class PackRegistryContractTests
    {
        private const string LogPrefix = "[팩계약]";

        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void DestroySynthetics()
        {
            for (int i = 0; i < _made.Count; i++) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        // ====================================================================
        // 픽스처
        // ====================================================================

        private StickPackManifestSO Manifest(int ordinal)
        {
            var m = ScriptableObject.CreateInstance<StickPackManifestSO>();
            _made.Add(m);
            m.name = "fixture_" + ordinal;
            m.packId = "packfixture.p" + ordinal;
            m.cohortId = ordinal;
            m.requiresSchemaVersion = StickPackManifestSO.SchemaVersion;
            m.packVersion = 1;
            m.displayNameKey = "packfixture.p" + ordinal + ".name";
            m.descriptionKey = "packfixture.p" + ordinal + ".desc";
            m.paletteOrigin = PackPaletteOrigin.DedicatedHue;
            m.paletteHueDegrees = ordinal * 31f;
            m.primaryColor = Color.gray;
            m.secondaryColor = Color.gray;
            m.declaredItemCount = 2;
            m.itemIndexBase = 10 * ordinal;
            m.dialogueSetKey = "packfixture.p" + ordinal + ".tone";
            m.soundKeys = new[] { "land" };
            m.entitlements = new[]
            {
                new PackEntitlementRef { channel = PackStoreChannel.Steam, entitlementId = "7770" + ordinal },
            };
            return m;
        }

        private ItemCatalogEntry[] ItemsFor(params StickPackManifestSO[] manifests)
        {
            var items = new List<ItemCatalogEntry>();
            foreach (StickPackManifestSO m in manifests)
            {
                for (int i = 0; i < m.declaredItemCount; i++)
                {
                    var def = ScriptableObject.CreateInstance<AccessoryDefSO>();
                    _made.Add(def);
                    def.itemId = m.packId + ".i" + i;
                    def.slot = EquipmentSlot.Neck;
                    def.itemIndex = m.itemIndexBase + i;
                    def.displayName = def.itemId;
                    def.description = def.itemId;
                    def.requiredLevel = ItemCatalog.PackRequiredLevel;
                    def.cohortId = m.cohortId;
                    def.declaredRarity = ItemCatalog.MaxDeclaredRarityForPack;
                    items.Add(ItemCatalog.EntryFrom(def));
                }
            }
            return items.ToArray();
        }

        private static PackDescriptor[] Load(StickPackManifestSO[] manifests, ItemCatalogEntry[] items,
            out List<string> faults)
        {
            faults = new List<string>();
            return PackRegistry.Build(manifests, items, faults);
        }

        private static bool Contains(PackDescriptor[] loaded, string packId)
        {
            for (int i = 0; i < loaded.Length; i++) if (loaded[i].PackId == packId) return true;
            return false;
        }

        /// <summary>한 칸만 망가뜨린 뒤 (가) 결함이 늘고 (나) 그 팩이 안 실리는지 함께 본다.</summary>
        private void AssertRejected(StickPackManifestSO broken, StickPackManifestSO[] all,
            ItemCatalogEntry[] items, string what)
        {
            PackDescriptor[] loaded = Load(all, items, out List<string> faults);
            Assert.IsNotEmpty(faults,
                $"{LogPrefix} {what} — 결함이 하나도 신고되지 않았습니다. 조용한 실패입니다.");
            Assert.IsFalse(Contains(loaded, broken.packId),
                $"{LogPrefix} {what} — 결함을 신고하고도 <b>실었습니다</b>. " +
                "신고만 하고 싣는 것이 가장 나쁩니다: 화면은 뜨는데 뜻이 다르고 아무도 안 고칩니다.\n  · " +
                string.Join("\n  · ", faults));
            Debug.Log($"{LogPrefix} {what} — 거부 확인, 결함 {faults.Count}건. 첫 줄: {faults[0]}");
        }

        // ====================================================================
        // 0. 이 파일의 전제 — 정상은 정상이어야 한다
        // ====================================================================

        [Test]
        public void 정상_픽스처는_결함이_0건이다()
        {
            StickPackManifestSO a = Manifest(1), b = Manifest(2);
            var all = new[] { a, b };
            PackDescriptor[] loaded = Load(all, ItemsFor(a, b), out List<string> faults);

            Assert.IsEmpty(faults,
                $"{LogPrefix} 정상 픽스처에 결함이 잡혔습니다 — 그러면 이 파일의 모든 '빨개졌다'가 " +
                $"원래부터 빨간 것과 구분되지 않습니다:\n  · " + string.Join("\n  · ", faults));
            Assert.AreEqual(2, loaded.Length);
            Assert.IsTrue(Contains(loaded, a.packId) && Contains(loaded, b.packId));
        }

        [Test]
        public void 팩이_하나도_없으면_결함_없이_0개다()
        {
            // 오늘의 실제 상태다. "0개"가 결함으로 신고되면 출하 첫날부터 에러 로그가 뜬다.
            PackDescriptor[] loaded = Load(new StickPackManifestSO[0], new ItemCatalogEntry[0], out List<string> faults);
            Assert.IsEmpty(faults, $"{LogPrefix} 팩 0개가 결함으로 신고됐습니다:\n  · " + string.Join("\n  · ", faults));
            Assert.AreEqual(0, loaded.Length);
        }

        // ====================================================================
        // 1. 등급을 무너뜨리는 것들
        // ====================================================================

        [Test]
        public void 기본_코호트를_쓰는_팩은_거부된다()
        {
            StickPackManifestSO m = Manifest(1);
            m.cohortId = ItemCatalog.BaseCohortId;
            AssertRejected(m, new[] { m }, ItemsFor(), "기본 코호트 사용");
        }

        [Test]
        public void 같은_코호트를_쓰는_두_팩은_뒤쪽이_거부된다()
        {
            StickPackManifestSO a = Manifest(1), b = Manifest(2);
            b.cohortId = a.cohortId;
            AssertRejected(b, new[] { a, b }, ItemsFor(a), "코호트 중복");
        }

        [Test]
        public void 주인_없는_코호트를_가진_아이템은_신고된다()
        {
            // 아이템은 팩 소속이라고 적었는데 그 번호의 매니페스트가 없다.
            StickPackManifestSO ghost = Manifest(9);
            ItemCatalogEntry[] orphans = ItemsFor(ghost);

            PackDescriptor[] loaded = Load(new StickPackManifestSO[0], orphans, out List<string> faults);
            Assert.AreEqual(0, loaded.Length);
            Assert.IsNotEmpty(faults,
                $"{LogPrefix} 고아 코호트가 신고되지 않았습니다. 그 아이템은 카탈로그에 보이고 등급도 " +
                "나오지만 <b>살 방법이 없습니다</b> — 조용한 결함입니다.");

            // 음성 대조: 주인이 있으면 신고되지 않는다(같은 아이템, 매니페스트만 추가).
            PackDescriptor[] ok = Load(new[] { ghost }, orphans, out List<string> okFaults);
            Assert.IsEmpty(okFaults,
                $"{LogPrefix} 주인이 있는데도 고아로 신고했습니다 — 감사가 정직한 팩을 빨갛게 만듭니다:\n  · " +
                string.Join("\n  · ", okFaults));
            Assert.AreEqual(1, ok.Length);
        }

        // ====================================================================
        // 2. 자리·정체
        // ====================================================================

        [Test]
        public void 같은_자리번호를_쓰는_두_팩은_뒤쪽이_거부된다()
        {
            StickPackManifestSO a = Manifest(1), b = Manifest(2);
            b.itemIndexBase = a.itemIndexBase;
            AssertRejected(b, new[] { a, b }, ItemsFor(a), "자리 번호 중복");
        }

        [Test]
        public void 기본_자리대를_침범하는_팩은_거부된다()
        {
            StickPackManifestSO m = Manifest(1);
            m.itemIndexBase = 0;
            AssertRejected(m, new[] { m }, ItemsFor(), "0번대 침범");
        }

        /// <summary>
        /// ★ 이 검사만 <see cref="AssertRejected"/> 를 못 쓴다 — 두 팩의 <c>packId</c> 가 <b>같으므로</b>
        /// 「그 아이디가 실렸는가」로는 앞뒤를 구분할 수 없다(앞쪽은 정상적으로 실려 있다).
        /// 그래서 <b>개수</b>로 판정한다. 이 자리를 <c>AssertRejected</c> 로 두면
        /// <b>초록이 나올 수 없는 검사</b>가 되고, 그런 검사는 며칠 안에 지워진다.
        /// </summary>
        [Test]
        public void 같은_아이디를_쓰는_두_팩은_뒤쪽이_거부된다()
        {
            StickPackManifestSO a = Manifest(1), b = Manifest(2);
            b.packId = a.packId;

            PackDescriptor[] loaded = Load(new[] { a, b }, ItemsFor(a), out List<string> faults);
            Assert.IsNotEmpty(faults, $"{LogPrefix} packId 중복이 신고되지 않았습니다.");
            Assert.AreEqual(1, loaded.Length,
                $"{LogPrefix} packId 가 겹친 팩 둘이 함께 실렸습니다({loaded.Length}개). " +
                "packId 는 엔타이틀먼트 키라, 겹친 채로 실리면 한쪽을 산 사람이 다른 쪽도 갖게 됩니다.");
            Assert.AreEqual(a.cohortId, loaded[0].CohortId,
                $"{LogPrefix} 살아남은 것이 <b>앞쪽</b>이 아닙니다 — 뒤에 놓인 매니페스트가 " +
                "앞의 것을 밀어내면 폴더 순서가 상품 정의를 바꾸게 됩니다.");
        }

        /// <summary>
        /// ★ <c>itemIndexBase</c> 가 <b>장식이 아니라 보증</b>인가. 선언과 실물이 갈라지면
        /// 다음 팩을 만드는 사람이 빈 자리를 잘못 고른다 — 그 표를 읽고 판단하기 때문이다.
        /// <para>겹침 자체는 <c>ItemCatalog</c> 가 크게 신고하지만 <b>어느 팩의 선언이 거짓이었는지</b>는
        /// 거기서 알 수 없다. 그래서 여기서 먼저 잡는다.</para>
        /// </summary>
        [Test]
        public void 선언한_자리대_아래에_앉은_아이템이_있으면_거부된다()
        {
            StickPackManifestSO m = Manifest(1);
            ItemCatalogEntry[] items = ItemsFor(m);          // itemIndexBase..+n 에 정상 배치
            m.itemIndexBase += 5;                            // 선언만 위로 올린다 = 실물이 아래에 남는다

            AssertRejected(m, new[] { m }, items, "자리대 선언과 실물 불일치");

            // 음성 대조 — 선언을 실물에 맞추면 지나간다(감사가 정직한 팩을 막지 않는다).
            StickPackManifestSO ok = Manifest(2);
            PackDescriptor[] loaded = Load(new[] { ok }, ItemsFor(ok), out List<string> faults);
            Assert.IsEmpty(faults, $"{LogPrefix} 자리대가 맞는 팩을 거부했습니다:\n  · " + string.Join("\n  · ", faults));
            Assert.AreEqual(1, loaded.Length);
        }

        [Test]
        public void 선언한_아이템_수와_실제_수가_다르면_거부된다()
        {
            StickPackManifestSO m = Manifest(1);
            ItemCatalogEntry[] items = ItemsFor(m);      // 선언 개수만큼 만들고
            m.declaredItemCount += 1;                    // 선언만 올린다 = 마지막 종이 빠진 모양
            AssertRejected(m, new[] { m }, items, "선언 개수 불일치");
        }

        // ====================================================================
        // 3. 번역 부채 — 매니페스트는 <b>키</b>만 담는다
        // ====================================================================

        [Test]
        public void 표시_이름에_원문을_적으면_거부되고_키_모양_결함과는_다른_문장이_나온다()
        {
            StickPackManifestSO korean = Manifest(1);
            korean.displayNameKey = "오피스 워커";
            PackDescriptor[] a = Load(new[] { korean }, ItemsFor(), out List<string> koreanFaults);
            Assert.AreEqual(0, a.Length,
                $"{LogPrefix} 원문이 들어간 매니페스트가 실렸습니다. 팩이 계속 늘어나면 팩마다 " +
                "(이름+설명) 2건씩 번역 부채가 자랍니다.");
            Assert.IsNotEmpty(koreanFaults);

            StickPackManifestSO shaped = Manifest(1);
            shaped.displayNameKey = "Pack.Office.Name";   // ASCII지만 키 모양이 아니다(대문자)
            Load(new[] { shaped }, ItemsFor(), out List<string> shapeFaults);
            Assert.IsNotEmpty(shapeFaults);

            Assert.AreNotEqual(koreanFaults[0], shapeFaults[0],
                $"{LogPrefix} 「원문을 적었다」와 「키 모양이 아니다」가 같은 문장을 냅니다. " +
                "고치는 방법이 다른 두 결함이라 구분해서 말해야 합니다.");

            Debug.Log($"{LogPrefix} 원문 거부: {koreanFaults[0]}\n{LogPrefix} 모양 거부: {shapeFaults[0]}");
        }

        [Test]
        public void 사운드_키와_대사_세트_키도_같은_자를_탄다()
        {
            StickPackManifestSO m = Manifest(1);
            m.soundKeys = new[] { "land", "착지" };
            AssertRejected(m, new[] { m }, ItemsFor(), "사운드 키에 원문");

            StickPackManifestSO n = Manifest(2);
            n.dialogueSetKey = "사이버 아포칼립스";
            AssertRejected(n, new[] { n }, ItemsFor(), "대사 세트 키에 원문");
        }

        [Test]
        public void 키_판정자는_알려진_값에서_교정된다()
        {
            // ★ 계산기를 먼저 교정한다(TEAM.md §4 공통 처방). 교정이 깨지면 위 판정 전부 무효다.
            Assert.IsTrue(PackManifestKeys.IsWellFormed("pack.office.name"));
            Assert.IsTrue(PackManifestKeys.IsWellFormed("a"));
            Assert.IsTrue(PackManifestKeys.IsWellFormed("pack_office"));
            Assert.IsFalse(PackManifestKeys.IsWellFormed(null));
            Assert.IsFalse(PackManifestKeys.IsWellFormed(""));
            Assert.IsFalse(PackManifestKeys.IsWellFormed(".pack"));
            Assert.IsFalse(PackManifestKeys.IsWellFormed("pack."));
            Assert.IsFalse(PackManifestKeys.IsWellFormed("pack..office"));
            Assert.IsFalse(PackManifestKeys.IsWellFormed("Pack.Office"));
            Assert.IsFalse(PackManifestKeys.IsWellFormed("pack office"));
            Assert.IsFalse(PackManifestKeys.IsWellFormed("오피스"));
            Assert.IsFalse(PackManifestKeys.IsWellFormed(new string('a', PackManifestKeys.MaxKeyLength + 1)));

            Assert.IsTrue(PackManifestKeys.ContainsNonAscii("오피스"));
            Assert.IsTrue(PackManifestKeys.ContainsNonAscii("pack.오피스"));
            Assert.IsFalse(PackManifestKeys.ContainsNonAscii("pack.office"));
        }

        // ====================================================================
        // 4. 팔레트 정원 — 「색을 어디서 얻었는가」를 매니페스트가 말할 수 있어야 한다
        // ====================================================================

        [Test]
        public void 같은_색상각을_두_팩이_배정받으면_거부되고_재사용_선언이면_지나간다()
        {
            StickPackManifestSO a = Manifest(1), b = Manifest(2);
            b.paletteHueDegrees = a.paletteHueDegrees;
            AssertRejected(b, new[] { a, b }, ItemsFor(a), "색상각 배정 중복");

            // 음성 대조 — <b>재사용이라고 선언하면</b> 같은 각이어도 지나간다.
            //   이 자리가 리더 질의("새 팩이 색을 어디서 얻는가")의 답이 앉는 곳이다.
            StickPackManifestSO c = Manifest(1), d = Manifest(2);
            d.paletteHueDegrees = c.paletteHueDegrees;
            d.paletteOrigin = PackPaletteOrigin.ReusedCatalogColor;
            PackDescriptor[] loaded = Load(new[] { c, d }, ItemsFor(c, d), out List<string> faults);
            Assert.IsEmpty(faults,
                $"{LogPrefix} 재사용 선언인데도 각 중복으로 거부했습니다 — 그러면 매니페스트가 " +
                "「기존 정원 재사용」을 표현할 수 없습니다:\n  · " + string.Join("\n  · ", faults));
            Assert.AreEqual(2, loaded.Length);
            Assert.AreEqual(PackPaletteOrigin.ReusedCatalogColor, loaded[1].PaletteOrigin);
        }

        // ====================================================================
        // 5. 판매 창구
        // ====================================================================

        [Test]
        public void 두_팩이_같은_스토어_식별자를_가리키면_거부된다()
        {
            StickPackManifestSO a = Manifest(1), b = Manifest(2);
            b.entitlements = new[]
            {
                new PackEntitlementRef { channel = PackStoreChannel.Steam, entitlementId = a.entitlements[0].entitlementId },
            };
            AssertRejected(b, new[] { a, b }, ItemsFor(a), "스토어 식별자 중복");
        }

        [Test]
        public void 빈_식별자는_무료가_아니라_결함이다()
        {
            StickPackManifestSO m = Manifest(1);
            m.entitlements = new[]
            {
                new PackEntitlementRef { channel = PackStoreChannel.Steam, entitlementId = "" },
            };
            AssertRejected(m, new[] { m }, ItemsFor(), "빈 식별자");
        }

        [Test]
        public void 채널을_두_번_적으면_거부된다()
        {
            StickPackManifestSO m = Manifest(1);
            m.entitlements = new[]
            {
                new PackEntitlementRef { channel = PackStoreChannel.Steam, entitlementId = "1" },
                new PackEntitlementRef { channel = PackStoreChannel.Steam, entitlementId = "2" },
            };
            AssertRejected(m, new[] { m }, ItemsFor(), "채널 중복");
        }

        [Test]
        public void 채널이_비어_있어도_팩_자체는_실린다()
        {
            // 「아직 상품이 아닌 팩」은 정상 상태다 — 조형이 먼저 오고 SKU가 나중에 온다.
            StickPackManifestSO m = Manifest(1);
            m.entitlements = new PackEntitlementRef[0];
            PackDescriptor[] loaded = Load(new[] { m }, ItemsFor(m), out List<string> faults);
            Assert.IsEmpty(faults, $"{LogPrefix} 채널 없는 팩을 거부했습니다:\n  · " + string.Join("\n  · ", faults));
            Assert.AreEqual(1, loaded.Length);
            Assert.AreEqual(0, loaded[0].Entitlements.Count);
        }

        // ====================================================================
        // 6. 앞으로 올 팩이 <b>옛 앱</b>에 놓이는 조합
        // ====================================================================

        [Test]
        public void 앱보다_새로운_스키마를_요구하는_팩은_반쯤_읽히지_않고_거부된다()
        {
            StickPackManifestSO m = Manifest(1);
            m.requiresSchemaVersion = StickPackManifestSO.SchemaVersion + 1;
            AssertRejected(m, new[] { m }, ItemsFor(), "앱보다 새로운 스키마");

            // 같은 값이면 실린다(경계).
            StickPackManifestSO ok = Manifest(2);
            ok.requiresSchemaVersion = StickPackManifestSO.SchemaVersion;
            PackDescriptor[] loaded = Load(new[] { ok }, ItemsFor(ok), out List<string> faults);
            Assert.IsEmpty(faults);
            Assert.AreEqual(1, loaded.Length);
        }

        [Test]
        public void 깨진_에셋_참조는_죽지_않고_신고된다()
        {
            StickPackManifestSO good = Manifest(1);
            PackDescriptor[] loaded = Load(new StickPackManifestSO[] { null, good }, ItemsFor(good),
                out List<string> faults);
            Assert.IsNotEmpty(faults, $"{LogPrefix} 빈 매니페스트 항목이 조용히 넘어갔습니다.");
            Assert.AreEqual(1, loaded.Length,
                $"{LogPrefix} 깨진 항목 하나가 멀쩡한 팩까지 못 싣게 만들었습니다.");
        }

        // ====================================================================
        // 7. 조인 창구 — 「이 아이템이 어느 팩 것인가」는 한 곳에서만 답한다
        // ====================================================================

        [Test]
        public void 기본_아이템은_팩_소속이_아니고_그건_결함이_아니다()
        {
            var def = ScriptableObject.CreateInstance<AccessoryDefSO>();
            _made.Add(def);
            def.itemId = "fixture.base.0";
            def.slot = EquipmentSlot.Head;
            def.itemIndex = 0;
            def.displayName = def.itemId;
            def.description = def.itemId;
            def.requiredLevel = 1;
            ItemCatalogEntry baseEntry = ItemCatalog.EntryFrom(def);

            Assert.IsFalse(PackRegistry.TryFindPackOfItem(baseEntry, out PackDescriptor found),
                $"{LogPrefix} 기본 아이템이 팩 소속으로 잡혔습니다.");
            Assert.IsNull(found);
            Assert.IsNull(PackRegistry.FindByCohort(ItemCatalog.BaseCohortId),
                $"{LogPrefix} 기본 코호트가 팩으로 조회됐습니다 — 기본 42종은 C층 대상이 아닙니다.");
            Assert.IsNull(PackRegistry.Find(null));
            Assert.IsNull(PackRegistry.Find(""));
        }

        [Test]
        public void 서술자는_에셋_배열을_복사해_들고_있다()
        {
            StickPackManifestSO m = Manifest(1);
            PackDescriptor[] loaded = Load(new[] { m }, ItemsFor(m), out List<string> faults);
            Assert.IsEmpty(faults);

            string before = loaded[0].SoundKeys[0];
            m.soundKeys[0] = "MUTATED";
            Assert.AreEqual(before, loaded[0].SoundKeys[0],
                $"{LogPrefix} 서술자가 에셋의 배열을 그대로 물고 있습니다 — 누가 한 칸만 써도 " +
                "에디터에서 에셋이 조용히 더러워집니다(AccessoryDefSO.BuildIcon이 같은 이유로 복사합니다).");
        }
    }
}
