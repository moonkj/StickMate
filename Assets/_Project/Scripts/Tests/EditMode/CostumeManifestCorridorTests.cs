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
    /// ★★★ P1 통과조건 1 — <b>아홉 번째 코스튬을 프로덕션 <c>.cs</c> 0줄로 넣을 수 있는가</b>
    /// ============================================================================
    /// 정본: <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 2-3절(합격 기준) · 9-3절(P1 판정).
    /// 이것이 <c>CLAUDE.md</c> 절대 불변 원칙 4(<i>"신규 DLC는 기본 로직 무수정으로 매니페스트를 통해 추가"</i>)의
    /// <b>코스튬 판 실증</b>이고, <see cref="PackManifestCorridorTests"/>가 팩에 대해 하는 것과
    /// <b>같은 형태</b>다(그쪽은 일곱 번째 팩, 이쪽은 아홉 번째 코스튬).
    ///
    /// ============================================================================
    /// ★ 왜 <b>하드코딩 판</b>(음성 대조)이 이 파일의 절반인가 — 없으면 아무것도 안 지킨다
    /// ============================================================================
    /// <i>"아홉 번째가 실렸다"</i>는 초록은 <b>그 자체로는 아무것도 증명하지 않는다.</b>
    /// 검사가 애초에 무는지 모르기 때문이다 — TEAM.md §4의 그 문장
    /// (<i>"실패한 측정과 성공한 측정이 똑같이 생겼다"</i>)이 정확히 이 자리에서 나온다.
    ///
    /// <para>그래서 <see cref="HardcodedEightCostumeGate"/>를 <b>같은 입력·같은 단언 함수</b>에 나란히 세운다:</para>
    /// <list type="bullet">
    ///  <item><see cref="CostumeCatalog"/>는 아홉 번째를 <b>싣는다.</b></item>
    ///  <item>하드코딩 판은 아홉 번째에서 <b>넘어진다.</b></item>
    ///  <item>여덟까지는 <b>두 판이 구분되지 않는다</b> — 갈리는 지점이 「9번째」임이 증명된다.</item>
    /// </list>
    /// ⇒ <b>누군가 코스튬 목록을 코드에 박아 넣으면 이 파일이 빨개진다</b>는 것이 여기서 성립한다.
    /// 그 성질이 없으면 이 테스트는 원칙 4를 <b>지키는 척만</b> 한다.
    ///
    /// ============================================================================
    /// 왜 「아홉 번째」인가 — 8이 확정된 수다
    /// ============================================================================
    /// 계약서 7-1절: <i>"조형 상태가 <b>8팩 × 4</b> = 32개다"</i>. 즉 오늘 계획된 코스튬 상한이 8이고,
    /// <b>그 다음 하나</b>가 「통로인가 벽인가」를 가르는 자리다. 숫자 8 자체에 뜻이 있는 것이 아니다.
    ///
    /// ============================================================================
    /// 무엇을 재지 <b>않는가</b> (못 재는 것을 재는 척하지 않는다)
    /// ============================================================================
    ///  · <b>해석기</b>(<see cref="CostumeResolver"/>) — 차림→코스튬 판정은 <c>EquipmentModel</c> 전역
    ///    상태를 밟아야 해서 별도 라운드다. 이 파일은 <b>발견과 적재</b>만 잰다.
    ///  · <b>개방 판정</b> — <see cref="CostumeEntitlementSingleGateTests"/>가 잰다.
    ///  · <b>단계 수치</b> — <see cref="CostumeEvolutionRulesTests"/>가 잰다.
    ///  · <b>조형·색</b> — 매니페스트 에셋이 오늘 0개다(그 0은 정상 상태이고, 아래
    ///    <see cref="발견_경로는_살아_있고_두_자가_같은_수를_낸다"/>가 «0개»와 «못 본다»를 가른다).
    /// </summary>
    public sealed class CostumeManifestCorridorTests
    {
        private const string LogPrefix = "[코스튬통로]";

        /// <summary>계획된 코스튬 수(계약서 7-1절 「8팩 × 4상태」). <b>이 숫자에 뜻이 있는 것이 아니라</b>
        /// 「확정된 것」과 「그 다음 하나」를 가르는 자리라는 데 뜻이 있다.</summary>
        private const int ConfirmedCostumeCount = 8;

        /// <summary>합성 코스튬/팩 아이디 접두. <b>실제 키(<c>costume.office</c> 등)를 쓰지 않는다</b> —
        /// 쓰면 프로덕션 소스를 훑는 감사들이 자기 픽스처와 진짜 배선을 구분할 수 없다
        /// (<see cref="PackManifestCorridorTests"/>가 같은 이유로 <c>packfixture.</c>를 쓴다).</summary>
        private const string SyntheticPrefix = "costumefixture.";

        /// <summary>아홉 번째에만 주는 앵커 오프셋. <b>일부러 다른 값</b>이다 —
        /// 어딘가에 코스튬별 값이 박혀 있으면 이 값이 서술자까지 못 온다.</summary>
        private const float NinthAnchorOffsetXInH = 0.375f;

        [TearDown]
        public void TearDown()
        {
            // 전역 카탈로그를 원복한다 — 안 하면 이 파일의 합성 코스튬이 다른 테스트로 샌다.
            CostumeCatalog.ResetForTesting();
        }

        // ====================================================================
        // 합성 픽스처 — 매니페스트를 <b>실제 적재 경로</b>(CostumeCatalog.Build)로 태운다
        // ====================================================================

        private sealed class Fixture : IDisposable
        {
            internal readonly List<CostumeManifestSO> Manifests = new List<CostumeManifestSO>();
            private readonly List<Object> _made = new List<Object>();

            internal CostumeManifestSO Add(string key, CostumeSourceKind kind, string sourceId)
            {
                var m = ScriptableObject.CreateInstance<CostumeManifestSO>();
                _made.Add(m);
                m.name = key;
                m.costumeKey = key;
                m.requiresSchemaVersion = CostumeManifestSO.SchemaVersion;
                m.sourceKind = kind;
                m.sourceId = sourceId;
                m.displayNameKey = key + ".name";
                Manifests.Add(m);
                return m;
            }

            public void Dispose()
            {
                for (int i = 0; i < _made.Count; i++) Object.DestroyImmediate(_made[i]);
                _made.Clear();
                Manifests.Clear();
            }
        }

        /// <summary>합성 코스튬 <paramref name="costumeCount"/>개.
        /// <para>1번은 <b>기본 코호트 테마</b>(오늘 유일하게 4부위 에셋이 다 있는 갈래 — 계약서 2-2절 MVP),
        /// 나머지는 <b>팩</b>이다. 두 갈래를 한 픽스처에 섞는 이유는 적재기가 갈래를 가리지 않는다는 것을
        /// 같은 실행에서 함께 보이기 위해서다.</para>
        /// <para>★ <b>마지막 하나만</b>(확정 수를 넘을 때) 구조를 다르게 준다 — 프롭 조각 1개 +
        /// 마스터 단계 조형 + 다른 앵커 오프셋. 어딘가에 「코스튬은 이런 모양」이 박혀 있으면 여기서 걸린다.</para></summary>
        private static Fixture BuildFixture(int costumeCount)
        {
            var f = new Fixture();
            for (int i = 1; i <= costumeCount; i++)
            {
                CostumeManifestSO m = i == 1
                    ? f.Add(SyntheticPrefix + "c1", CostumeSourceKind.BaseTheme, ItemCatalog.ThemeOffice)
                    : f.Add(SyntheticPrefix + "c" + i, CostumeSourceKind.Pack, SyntheticPrefix + "pk" + i);

                if (i != costumeCount || costumeCount <= ConfirmedCostumeCount) continue;

                m.propShapes = new[] { ValidPropShape("fixture_prop") };
                m.propAnchorOffsetXInH = NinthAnchorOffsetXInH;
                m.stageShapes = new[]
                {
                    // ★ 단계 번호를 숫자로 적지 않는다 — 상태 수가 늘면 이 픽스처가 따라 움직인다.
                    new CostumeStageOverride
                    {
                        stage = CostumeEvolutionRules.MasterStage,
                        shapes = new[] { ValidPropShape("fixture_master") },
                    },
                };
            }
            return f;
        }

        /// <summary>
        /// 문법에 맞는 최소 좌표 스트림 1개(점 2개). <b>검사기를 새로 적지 않는다</b> —
        /// <see cref="AccessoryWornShapeReader.Validate"/>가 이 값을 실제로 돌려서 통과해야 한다.
        /// <para>스트림 문법: <c>pointCount, (x합, y합) × pointCount</c> /
        /// <c>합 := termCount, term × termCount</c> / <c>term := basis, gate, trig, coefCount, coef…</c></para>
        /// </summary>
        private static AccessoryWornShapeData ValidPropShape(string name)
        {
            const int basis = (int)AccessoryWornBasis.HeadRadius;
            const int gate = (int)AccessoryWornGate.Always;
            const int trig = (int)AccessoryWornTrig.None;

            return new AccessoryWornShapeData
            {
                name = name,
                terms = new float[]
                {
                    2f,                                        // 점 2개
                    1f, basis, gate, trig, 1f,  0.50f,         // p0.x = R × 0.50
                    1f, basis, gate, trig, 1f,  0.00f,         // p0.y
                    1f, basis, gate, trig, 1f, -0.50f,         // p1.x
                    1f, basis, gate, trig, 1f,  0.25f,         // p1.y
                },
            };
        }

        // ====================================================================
        // ★ 음성 대조용 하드코딩 판 — "만약 이렇게 만들었다면"을 실제로 세워 본다
        // ====================================================================

        /// <summary>
        /// ★ <b>이 클래스가 이 파일의 자(尺)다.</b> 코스튬 목록을 <c>switch</c>로 박아 둔,
        /// 이 라운드가 만들지 <b>않기로</b> 한 판. 여덟까지는 <see cref="CostumeCatalog"/>와
        /// 구분되지 않고 <b>아홉 번째에서만</b> 갈린다.
        ///
        /// <para>왜 필요한가: 통로 검사가 초록이어도 그것이 「통로라서 통과」인지
        /// 「검사가 아무것도 안 재서 통과」인지 알 수 없다. 이 판이 <b>같은 단언 함수에서 빨개짐</b>으로써
        /// 후자가 아님을 매 실행 증명한다.</para>
        /// </summary>
        private static class HardcodedEightCostumeGate
        {
            /// <summary>확정된 여덟 코스튬만 아는 목록. <b>일부러</b> 이렇게 짰다.</summary>
            internal static string SourceIdOf(string costumeKey)
            {
                switch (costumeKey)
                {
                    case SyntheticPrefix + "c1": return ItemCatalog.ThemeOffice;
                    case SyntheticPrefix + "c2": return SyntheticPrefix + "pk2";
                    case SyntheticPrefix + "c3": return SyntheticPrefix + "pk3";
                    case SyntheticPrefix + "c4": return SyntheticPrefix + "pk4";
                    case SyntheticPrefix + "c5": return SyntheticPrefix + "pk5";
                    case SyntheticPrefix + "c6": return SyntheticPrefix + "pk6";
                    case SyntheticPrefix + "c7": return SyntheticPrefix + "pk7";
                    case SyntheticPrefix + "c8": return SyntheticPrefix + "pk8";
                }
                return null;   // 모르는 코스튬
            }
        }

        /// <summary>두 판에 <b>똑같이</b> 던지는 질문. 통과 조건도 똑같다.</summary>
        private static bool ResolvesEveryCostume(Func<string, string> sourceIdOf,
            IList<CostumeManifestSO> manifests, out string firstFailure)
        {
            for (int i = 0; i < manifests.Count; i++)
            {
                string got = sourceIdOf(manifests[i].costumeKey);
                if (string.Equals(got, manifests[i].sourceId, StringComparison.Ordinal)) continue;
                firstFailure = $"'{manifests[i].costumeKey}' → 소속 '{got ?? "(모름)"}'" +
                               $"(기대 '{manifests[i].sourceId}')";
                return false;
            }
            firstFailure = null;
            return true;
        }

        private static Func<string, string> CatalogResolver(CostumeDescriptor[] loaded)
        {
            return key =>
            {
                for (int i = 0; i < loaded.Length; i++) if (loaded[i].CostumeKey == key) return loaded[i].SourceId;
                return null;
            };
        }

        private static CostumeDescriptor[] BuildOrFail(Fixture f, string what)
        {
            var faults = new List<string>();
            CostumeDescriptor[] loaded = CostumeCatalog.Build(f.Manifests, faults);
            Assert.IsEmpty(faults,
                $"{LogPrefix} {what}가 거부됐습니다({faults.Count}건):\n  · " + string.Join("\n  · ", faults));
            return loaded;
        }

        // ====================================================================
        // 1. 합격 기준 본체
        // ====================================================================

        [Test]
        public void 아홉번째_코스튬을_프로덕션_코드_한_줄도_안_고치고_넣을_수_있다()
        {
            using (Fixture eight = BuildFixture(ConfirmedCostumeCount))
            using (Fixture nine = BuildFixture(ConfirmedCostumeCount + 1))
            {
                CostumeDescriptor[] eightLoaded = BuildOrFail(eight, $"확정 {ConfirmedCostumeCount}코스튬");
                Assert.AreEqual(ConfirmedCostumeCount, eightLoaded.Length,
                    $"{LogPrefix} {ConfirmedCostumeCount}개를 넣었는데 {eightLoaded.Length}개만 실렸습니다.");

                var nineFaults = new List<string>();
                CostumeDescriptor[] nineLoaded = CostumeCatalog.Build(nine.Manifests, nineFaults);

                Assert.IsEmpty(nineFaults,
                    $"{LogPrefix} ★ <b>아홉 번째 코스튬이 거부됐습니다</b>({nineFaults.Count}건):\n  · " +
                    string.Join("\n  · ", nineFaults) + "\n\n" +
                    "불변 원칙 4는 「신규 DLC는 기본 로직 무수정으로 매니페스트를 통해 추가」입니다. " +
                    "여덟에서 멈추는 구조라면 그건 통로가 아니라 하드코딩입니다.");
                Assert.AreEqual(ConfirmedCostumeCount + 1, nineLoaded.Length,
                    $"{LogPrefix} 아홉 번째가 실리지 않았습니다({nineLoaded.Length}개).");

                // ---- 이름만 실린 것이 아니라 <b>데이터가 통과했는가</b> ----
                CostumeDescriptor ninth = null;
                for (int i = 0; i < nineLoaded.Length; i++)
                {
                    if (nineLoaded[i].CostumeKey == SyntheticPrefix + "c" + (ConfirmedCostumeCount + 1))
                    {
                        ninth = nineLoaded[i];
                    }
                }
                Assert.IsNotNull(ninth, $"{LogPrefix} 아홉 번째 코스튬을 적재 결과에서 못 찾았습니다.");

                Assert.AreEqual(CostumeSourceKind.Pack, ninth.SourceKind,
                    $"{LogPrefix} 아홉 번째의 소속 갈래가 안 실렸습니다.");
                Assert.AreEqual(SyntheticPrefix + "pk" + (ConfirmedCostumeCount + 1), ninth.SourceId,
                    $"{LogPrefix} 아홉 번째의 소속 아이디가 안 실렸습니다.");
                Assert.AreEqual(1, ninth.PropShapes.Count,
                    $"{LogPrefix} 아홉 번째의 프롭 조각이 서술자까지 오지 않았습니다 — " +
                    "적재는 됐는데 <b>데이터가 안 온 것</b>이고, 화면 증상은 「프롭이 없는 코스튬」입니다.");
                Assert.AreEqual(NinthAnchorOffsetXInH, ninth.PropAnchorOffsetXInH, 1e-6f,
                    $"{LogPrefix} 아홉 번째의 앵커 오프셋이 안 실렸습니다 — 픽스처는 <b>일부러</b> " +
                    "다른 값을 줬습니다. 어딘가에 코스튬별 값이 박혀 있으면 여기서 걸립니다.");
                Assert.AreEqual(1, ninth.StageShapes.Count,
                    $"{LogPrefix} 아홉 번째의 단계 조형이 안 실렸습니다.");
                Assert.AreEqual(CostumeEvolutionRules.MasterStage, ninth.StageShapes[0].stage,
                    $"{LogPrefix} 마스터 단계 조형이 다른 단계로 실렸습니다.");

                // ---- 아홉 번째가 들어와도 <b>기존 여덟이 그대로</b>인가(밀리지 않았는가) ----
                for (int i = 0; i < eightLoaded.Length; i++)
                {
                    Assert.AreEqual(eightLoaded[i].CostumeKey, nineLoaded[i].CostumeKey,
                        $"{LogPrefix} 아홉 번째가 들어오자 {i}번 자리가 바뀌었습니다 " +
                        "— 자리가 밀리면 세이브의 누적 시간이 다른 코스튬 것이 됩니다.");
                }

                Debug.Log($"{LogPrefix} 합성 {nineLoaded.Length}코스튬 적재 성공 — 프로덕션 .cs 변경 0줄. " +
                          $"아홉 번째: {ninth.CostumeKey} / 소속 {ninth.SourceKind}:{ninth.SourceId} / " +
                          $"프롭 {ninth.PropShapes.Count}조각 / 단계조형 {ninth.StageShapes.Count}건 / " +
                          $"앵커 {ninth.PropAnchorOffsetXInH:0.###}H");
            }
        }

        /// <summary>
        /// ★★ <b>이 테스트가 위 초록에 값을 준다.</b> 하드코딩 판이 같은 입력의 아홉 번째에서
        /// 넘어지지 않으면, 위의 통과는 「통로라서」가 아니라 「아무것도 안 재서」일 수 있다.
        /// </summary>
        [Test]
        public void 음성대조_하드코딩판은_같은_입력의_아홉번째에서_넘어진다()
        {
            using (Fixture nine = BuildFixture(ConfirmedCostumeCount + 1))
            {
                CostumeDescriptor[] loaded = BuildOrFail(nine, "대조의 전제(9코스튬 적재)");

                // (가) 통로 판 — 아홉 개 전부 푼다.
                bool corridorOk = ResolvesEveryCostume(CatalogResolver(loaded), nine.Manifests, out string corridorFail);
                Assert.IsTrue(corridorOk, $"{LogPrefix} 통로 판이 못 풀었습니다: {corridorFail}");

                // (나) 하드코딩 판 — <b>같은 단언 함수</b>에서 넘어져야 한다.
                bool hardcodedOk = ResolvesEveryCostume(HardcodedEightCostumeGate.SourceIdOf,
                    nine.Manifests, out string hardFail);
                Assert.IsFalse(hardcodedOk,
                    $"{LogPrefix} ★ <b>하드코딩 판이 아홉 번째를 통과했습니다.</b> " +
                    "그러면 이 파일의 모든 초록이 무효입니다 — 검사가 두 구조를 구분하지 못한다는 뜻이고, " +
                    "[아홉번째_코스튬을_…]의 통과도 '통로라서'가 아니라 '아무것도 안 재서'일 수 있습니다.");
                Assert.IsNotNull(hardFail);
                StringAssert.Contains("c" + (ConfirmedCostumeCount + 1), hardFail,
                    $"{LogPrefix} 하드코딩 판이 <b>아홉 번째가 아닌 곳</b>에서 넘어졌습니다({hardFail}). " +
                    "그렇다면 이 대조는 「8 vs 9」가 아니라 다른 무언가를 재고 있습니다.");

                // (다) 여덟까지는 두 판이 <b>구분되지 않는다</b> — 갈리는 지점이 9임의 증명.
                using (Fixture eight = BuildFixture(ConfirmedCostumeCount))
                {
                    CostumeDescriptor[] loaded8 = BuildOrFail(eight, $"{ConfirmedCostumeCount}코스튬");
                    Assert.IsTrue(ResolvesEveryCostume(CatalogResolver(loaded8), eight.Manifests, out _));
                    Assert.IsTrue(ResolvesEveryCostume(HardcodedEightCostumeGate.SourceIdOf, eight.Manifests, out _),
                        $"{LogPrefix} 하드코딩 판이 여덟에서 이미 넘어집니다 — 그러면 (나)의 빨강은 " +
                        "「9번째라서」가 아니라 픽스처가 원래 안 맞아서입니다.");
                }

                Debug.Log($"{LogPrefix} 음성 대조 통과 — 8코스튬에서는 두 판이 같고, " +
                          $"9코스튬에서 하드코딩 판만 넘어졌다: {hardFail}");
            }
        }

        /// <summary>
        /// 전역 조회 창구(<c>Find</c>/<c>FindByPack</c>/<c>FindByBaseTheme</c>)도 아홉 번째를 안다.
        /// <para>★ 위 두 테스트는 순수 <c>Build</c>만 탔다 — <b>다른 자</b>로 같은 사실을 다시 잰다.
        /// 색인이 <c>Build</c>와 갈라져 있으면(예: 조회가 자기 목록을 따로 든다) 여기서만 걸린다.</para>
        /// </summary>
        [Test]
        public void 전역_조회_창구도_아홉번째를_찾는다()
        {
            using (Fixture nine = BuildFixture(ConfirmedCostumeCount + 1))
            {
                var faults = new List<string>();
                CostumeCatalog.UseForTesting(nine.Manifests, faults);
                Assert.IsEmpty(faults,
                    $"{LogPrefix} 적재 결함 {faults.Count}건 — 아래 조회는 «카탈로그가 비어서» 실패합니다:\n  · " +
                    string.Join("\n  · ", faults));

                Assert.AreEqual(ConfirmedCostumeCount + 1, CostumeCatalog.Count,
                    $"{LogPrefix} 실린 코스튬 수가 다릅니다.");

                string ninthKey = SyntheticPrefix + "c" + (ConfirmedCostumeCount + 1);
                string ninthPack = SyntheticPrefix + "pk" + (ConfirmedCostumeCount + 1);

                Assert.IsNotNull(CostumeCatalog.Find(ninthKey),
                    $"{LogPrefix} 세이브 키 조회가 아홉 번째를 못 찾습니다 — 그러면 그 코스튬의 " +
                    "누적 분이 로드 정규화에서 <b>조용히 버려집니다</b>.");
                Assert.AreSame(CostumeCatalog.Find(ninthKey), CostumeCatalog.FindByPack(ninthPack),
                    $"{LogPrefix} 키 조회와 팩 조회가 <b>다른 서술자</b>를 돌려줍니다 — 색인이 둘로 갈렸습니다.");

                CostumeDescriptor first = CostumeCatalog.FindByBaseTheme(ItemCatalog.ThemeOffice);
                Assert.IsNotNull(first,
                    $"{LogPrefix} 기본 코호트 테마 조회가 1번 코스튬을 못 찾습니다 — " +
                    "아홉 번째가 들어오며 다른 갈래를 가렸다는 뜻입니다.");
                Assert.AreEqual(SyntheticPrefix + "c1", first.CostumeKey);
                Assert.IsFalse(first.IsPackSourced,
                    $"{LogPrefix} 기본 코호트 코스튬이 팩 소속으로 실렸습니다 — " +
                    "그러면 무료 갈래가 엔타이틀먼트를 묻게 되고, 오늘 트리에서는 «전원 아무것도 못 봄»입니다.");

                // 모르는 것은 모른다고 답한다(조회가 아무거나 돌려주면 위 초록이 무의미하다).
                Assert.IsNull(CostumeCatalog.Find(SyntheticPrefix + "없는코스튬"));
                Assert.IsNull(CostumeCatalog.FindByPack(SyntheticPrefix + "없는팩"));
                Assert.IsNull(CostumeCatalog.FindByBaseTheme(ItemCatalog.ThemeNeon));

                Debug.Log($"{LogPrefix} 전역 조회 3창구 — {CostumeCatalog.Count}개 적재, " +
                          $"아홉 번째 키/팩 조회 동일 서술자 확인.");
            }
        }

        // ====================================================================
        // 2. 발견 경로 — 「0개」와 「못 본다」를 가른다
        // ====================================================================

        /// <summary>
        /// 폴더 스캔이 <b>살아 있는 경로</b>인가. 오늘 코스튬 매니페스트 에셋은 <b>0개</b>인데,
        /// <b>0개는 「코스튬이 없다」와 「폴더 이름이 틀렸다」를 구분하지 못한다.</b>
        /// <list type="number">
        ///  <item><b>양성 대조</b> — 같은 폴더 상수로 <c>AccessoryDefSO</c>를 훑어 42개가 나오는가.
        ///    나오면 그 경로는 죽은 문자열이 아니다.</item>
        ///  <item><b>다른 자</b> — 엔진(<c>Resources.LoadAll</c>)이 센 수와, <c>.cs.meta</c>의 GUID로
        ///    <c>.asset</c>을 직접 훑어 센 수(파일시스템)를 대조한다. 한쪽이 눈이 멀면 두 수가 갈라진다.</item>
        /// </list>
        /// <para>★ 에셋은 스크립트를 <b>GUID</b>로 참조하므로 타입 이름을 <c>grep</c>하면 탐지력이
        /// <b>애초에 0</b>이다(TEAM.md §4 사고 #4의 형태). 그래서 GUID로 찾는다.</para>
        /// </summary>
        [Test]
        public void 발견_경로는_살아_있고_두_자가_같은_수를_낸다()
        {
            string folder = CostumeCatalog.ResourceFolder;
            Assert.IsNotEmpty(folder, $"{LogPrefix} 폴더 상수가 비었습니다.");
            Assert.AreEqual(PackRegistry.ResourceFolder, folder,
                $"{LogPrefix} 코스튬과 팩이 서로 다른 폴더를 봅니다 — " +
                "코스튬 매니페스트가 «있는데 안 보이는» 상태가 만들어집니다.");

            // (1) 양성 대조 — 이 폴더 상수로 실제로 무언가가 나오는가.
            AccessoryDefSO[] items = Resources.LoadAll<AccessoryDefSO>(folder);
            Assert.Greater(items.Length, 0,
                $"{LogPrefix} 폴더 '{folder}'에서 아이템 에셋이 0개 나왔습니다. " +
                "그렇다면 아래 «매니페스트 0개»는 «없다»가 아니라 «못 본다»입니다.");

            // (2) 엔진이 센 코스튬 매니페스트 수.
            CostumeManifestSO[] byEngine = Resources.LoadAll<CostumeManifestSO>(folder);

            // (3) 파일시스템이 센 수 — GUID로 직접.
            string[] byDisk = PackManifestLocalizationDebtTests.AssetsReferencingScript(nameof(CostumeManifestSO));
            string[] itemsByDisk = PackManifestLocalizationDebtTests.AssetsReferencingScript(nameof(AccessoryDefSO));

            Assert.AreEqual(items.Length, itemsByDisk.Length,
                $"{LogPrefix} 아이템 에셋 수가 두 자에서 다릅니다(엔진 {items.Length} / 디스크 {itemsByDisk.Length}). " +
                "한쪽 스캐너가 눈이 멀었으므로 이 테스트의 모든 수를 폐기하십시오.");
            Assert.AreEqual(byDisk.Length, byEngine.Length,
                $"{LogPrefix} 코스튬 매니페스트 수가 두 자에서 다릅니다" +
                $"(디스크 {byDisk.Length} / 엔진 {byEngine.Length}).");

            // 실린 것이 있다면 전부 결함 없이 실려야 한다(오늘은 0개이고 그건 정상이다).
            var faults = new List<string>();
            CostumeDescriptor[] shipped = CostumeCatalog.Build(byEngine, faults);
            Assert.IsEmpty(faults,
                $"{LogPrefix} 저장소에 실린 코스튬 매니페스트가 거부됐습니다({faults.Count}건):\n  · " +
                string.Join("\n  · ", faults));
            Assert.AreEqual(byEngine.Length, shipped.Length);

            Debug.Log($"{LogPrefix} 폴더 '{folder}' — 아이템 {items.Length}개(디스크 {itemsByDisk.Length}), " +
                      $"코스튬 매니페스트 {byEngine.Length}개(디스크 {byDisk.Length}). " +
                      "매니페스트 0개는 오늘의 정상 상태다(P4~P6이 채운다).");
        }

        // ====================================================================
        // 3. 문(門)이 실제로 무는가 — 통과만 재면 절반만 잰 것이다
        // ====================================================================

        /// <summary>
        /// ★★ <b>리더 판정 회귀</b>: 기본 코호트 <c>mil</c> 테마 코스튬은 <b>결함으로 거부된다</b>.
        ///
        /// <para>1일차 무상 4종(스탯 4슬롯 idx0)이 전부 <c>mil</c>이라, <c>mil</c> 테마 코스튬을 만들면
        /// <b>동전 0원 · 0일차</b>에 코스튬이 전원에게 열린다 — 유료 코스튬의 간판 연출이 그 자리에서 샌다.
        /// <b>이 거부는 결함이 아니라 정상 동작이고, 지우면 여기서 빨개진다.</b></para>
        ///
        /// <para>★ 같은 테스트에 <b>양성 대조 2건</b>을 붙인다:
        /// (가) <c>mil</c>이 실재하는 테마인지(<see cref="ItemCatalog.AllThemes"/>에 있는지) —
        /// 없는 테마라면 이 거부는 「mil 정책」이 아니라 「모르는 테마」를 잰 것이다.
        /// (나) <b>허용 목록에 있는 기본 코호트 테마는 통과하는지</b> — 안 통과하면 이 빨강은
        /// «mil이라서»가 아니라 «기본 코호트 갈래가 통째로 막혀서»다.</para>
        ///
        /// <para>★★ <b>2026-09-08 정정 — 이 테스트만으로는 유료 경계를 못 지킨다.</b>
        /// 여기서 재는 것은 <b><c>mil</c> 한 칸</b>뿐이고, 프로덕션이 <c>mil</c>만 막던 시절에는
        /// <c>cyber</c>·<c>neon</c>·<c>sport</c>·<c>ink</c>가 <b>전부 통과</b>했는데도 이 파일은 초록이었다.
        /// 그 구멍을 <see cref="기본코호트_코스튬은_허용목록_밖_테마를_전부_거부한다"/>가 막는다 —
        /// 이 테스트는 이제 <b>그 허용 목록의 한 사례</b>이고, «어느 테마 때문인지 사유가 문장에 남는가»를
        /// 잰다(허용 목록이 이유를 뭉뚱그리면 만든 사람이 못 고친다).</para>
        /// </summary>
        [Test]
        public void 기본코호트_mil_테마_코스튬은_결함으로_거부된다()
        {
            // (가) 전제: mil은 실재하는 테마다.
            Assert.Contains(ItemCatalog.ThemeMil, ItemCatalog.AllThemes(),
                $"{LogPrefix} '{ItemCatalog.ThemeMil}'가 테마 표에 없습니다 — " +
                "그러면 아래 거부는 «mil 정책»이 아니라 «모르는 테마»를 잰 것입니다.");

            // (나) 양성 대조: 허용 목록에 있는 기본 코호트 테마는 통과한다.
            //     ★ 테마 이름을 베끼지 않고 <b>허용 목록의 첫 항목</b>을 쓴다 — 목록이 바뀌면 따라간다.
            string[] allowed = CostumeCatalog.AllowedBaseThemes();
            Assert.IsNotEmpty(allowed,
                $"{LogPrefix} BaseTheme 허용 목록이 비었습니다 — 그러면 아래 «다른 테마는 통과» 대조를 " +
                "세울 수 없고, mil 빨강이 정책인지 갈래 고장인지 구별되지 않습니다.");
            using (var ok = new Fixture())
            {
                ok.Add(SyntheticPrefix + "cok", CostumeSourceKind.BaseTheme, allowed[0]);
                CostumeDescriptor[] loaded = BuildOrFail(ok, $"기본 코호트 '{allowed[0]}' 코스튬");
                Assert.AreEqual(1, loaded.Length,
                    $"{LogPrefix} 기본 코호트 갈래가 통째로 막혀 있습니다 — " +
                    "그러면 아래 mil 빨강은 정책이 아니라 갈래 고장입니다.");
            }

            // (본론) mil은 거부된다.
            using (var bad = new Fixture())
            {
                bad.Add(SyntheticPrefix + "cmil", CostumeSourceKind.BaseTheme, ItemCatalog.ThemeMil);
                var faults = new List<string>();
                CostumeDescriptor[] loaded = CostumeCatalog.Build(bad.Manifests, faults);

                Assert.IsEmpty(loaded,
                    $"{LogPrefix} ★ <b>mil 테마 코스튬이 실렸습니다.</b> 1일차 무상 4종이 전부 " +
                    $"'{ItemCatalog.ThemeMil}'이므로, 이 코스튬은 <b>동전 0원 · 0일차</b>에 전원에게 " +
                    "열립니다 — 유료 코스튬의 간판 연출이 그 자리에서 샙니다.\n" +
                    "정책을 바꾸기로 했다면 CostumeCatalog의 검사와 <b>이 테스트를 함께</b> 지우고, " +
                    "왜 열기로 했는지를 두 자리에 적으십시오.");
                Assert.IsNotEmpty(faults,
                    $"{LogPrefix} mil 코스튬이 <b>조용히</b> 빠졌습니다 — 결함 신고가 0건입니다. " +
                    "조용한 미적재는 «만든 사람이 영원히 못 찾는» 형태입니다.");
                StringAssert.Contains(ItemCatalog.ThemeMil, string.Join("\n", faults),
                    $"{LogPrefix} 결함 문장이 어느 테마 때문인지 말하지 않습니다.");

                Debug.Log($"{LogPrefix} mil 거부 확인 — {faults.Count}건 신고, 적재 0개.");
            }
        }

        /// <summary>
        /// ============================================================================
        /// ★★★ <b>P0 유료 경계 회귀 — 허용 목록 밖의 기본 코호트 테마는 「전부」 거부된다</b>
        /// ============================================================================
        /// <b>막은 구멍</b>(2026-09-08, <c>product-strategy</c> 실측 · <c>docs/strategy/CHANNEL_PRICING_DECISIONS.md</c> 54-1절):
        /// <see cref="CostumeCatalog"/>의 소속 감사는 원래 <c>mil</c> <b>하나만</b> 거부했고
        /// <c>ink</c>·<c>sport</c>·<c>cyber</c>·<c>neon</c>은 <b>통과</b>시켰다.
        /// 그런데 기본 42종의 6테마는 <b>전부 4/4가 인게임 재화로 살 수 있는 아이템</b>이라,
        /// 누군가 <c>costume.cyber</c>를 <see cref="CostumeSourceKind.BaseTheme"/>/<c>cyber</c>로 저작하면
        /// <b>$4.99 팩의 간판 연출이 동전 6,600(원형 B 1.7일)에 열린다</b> — 그리고 그 사고는
        /// <b>감사도 초록 · 테스트도 초록</b>인 채로 난다. 그것이 이 테스트가 존재하는 이유다.
        ///
        /// <para><b>피해자가 누구인지 적어 둔다</b>(그것을 못 적으면 위협이 아니라 불안이다):
        /// <b>개발자(매출)</b>다. 세이브를 고쳐 자기 동전을 올리는 일과 성질이 다르다 —
        /// 그쪽은 피해자가 자기 자신뿐이지만, 이쪽은 <b>유료 경계</b>가 무너진다.</para>
        ///
        /// ============================================================================
        /// 이 테스트가 <b>죽은 프로브</b>가 되지 않도록 세운 대조 5개
        /// ============================================================================
        /// <list type="number">
        ///  <item><b>허용 목록의 항목이 실재하는 테마인가</b> — 오타면 아래 «통과» 대조가 통째로 공허하다.</item>
        ///  <item><b>상품 골든</b> — 오늘 허용된 것은 <c>office</c> 하나다. 목록이 조용히 자라면 여기서 빨개진다
        ///        (허용 목록만 참조하는 테스트는 <b>프로덕션을 따라다니며 언제나 초록</b>이 된다).</item>
        ///  <item><b>거부 대상이 0개가 아닌가</b> — 0이면 아래 루프가 한 바퀴도 안 돌고 초록이 난다.</item>
        ///  <item><b>양성 대조</b> — 허용된 테마는 <b>실제로 실린다</b>. 안 실리면 아래 빨강은
        ///        «경계»가 아니라 «갈래가 통째로 고장»이다.</item>
        ///  <item><b>위협 실재 대조</b> — 거부하는 테마들이 정말로 «기본 42종만으로 4/4가 되는» 테마인가.
        ///        아니라면 이 거부는 경제 방어가 아니라 취향이다.</item>
        /// </list>
        /// </summary>
        [Test]
        public void 기본코호트_코스튬은_허용목록_밖_테마를_전부_거부한다()
        {
            string[] all = ItemCatalog.AllThemes();
            string[] allowed = CostumeCatalog.AllowedBaseThemes();

            Assert.IsNotEmpty(all, $"{LogPrefix} 테마 표가 비었습니다 — 아래 비교가 전부 공허합니다.");
            Assert.IsNotEmpty(allowed,
                $"{LogPrefix} BaseTheme 허용 목록이 비었습니다. 그러면 무료 코스튬 갈래가 " +
                "통째로 막힌 것이고, 출하된 costume.office가 실리지 않습니다.");

            // ---- 대조 ① 허용 목록의 항목이 실재하는 테마인가(오타 방지) ----
            for (int i = 0; i < allowed.Length; i++)
            {
                Assert.Contains(allowed[i], all,
                    $"{LogPrefix} 허용 목록의 '{allowed[i]}'가 ItemCatalog의 테마 표에 없습니다 — " +
                    "오타면 그 항목은 <b>영원히 아무것도 열지 않고</b>, 아래 «허용된 테마는 통과» 대조가 " +
                    "그 자리에서 거짓이 됩니다.");
            }

            // ---- 대조 ② 상품 골든: 오늘 허용된 것은 office 하나다 ----
            //   ★ 이 한 줄이 없으면 이 테스트는 프로덕션 목록을 그대로 따라다니며 <b>언제나 초록</b>이다.
            CollectionAssert.AreEquivalent(new[] { ItemCatalog.ThemeOffice }, allowed,
                $"{LogPrefix} ★★★ <b>BaseTheme 허용 목록이 바뀌었습니다</b>" +
                $"(지금: [{string.Join(", ", allowed)}]).\n" +
                "이 목록은 <b>무료/유료 경계</b>입니다 — 항목을 하나 더하면 그 테마의 코스튬이 " +
                "<b>인게임 재화만으로</b> 열립니다(기본 42종의 6테마는 전부 4/4가 동전으로 살 수 있습니다).\n" +
                "  · 늘리려면 product-strategy 판정을 먼저 받고 " +
                "docs/strategy/CHANNEL_PRICING_DECISIONS.md의 SKU 표를 <b>같은 라운드에</b> 고치십시오.\n" +
                "  · 그리고 이 골든을 <b>그 판정과 함께</b> 고치십시오(그냥 맞추지 말 것).");

            // ---- 대조 ③ 거부 대상이 0개가 아닌가 ----
            var denied = new List<string>();
            for (int i = 0; i < all.Length; i++)
            {
                if (!CostumeCatalog.IsAllowedBaseTheme(all[i])) denied.Add(all[i]);
            }
            Assert.IsNotEmpty(denied,
                $"{LogPrefix} 거부 대상 테마가 0개입니다 — 허용 목록이 테마 표 전체를 덮었다는 뜻이고, " +
                "그러면 아래 루프가 <b>한 바퀴도 안 돌고</b> 초록이 납니다(그것이 이 저장소의 거짓 통과 형태).");

            // ---- 대조 ⑤ 위협 실재: 거부하는 테마가 정말 «동전만으로 4/4»인가 ----
            var perTheme = new Dictionary<string, int>();
            foreach (ItemCatalogEntry e in ItemCatalog.Entries)
            {
                if (e.CohortId != ItemCatalog.BaseCohortId) continue;
                if (string.IsNullOrEmpty(e.Theme)) continue;
                perTheme.TryGetValue(e.Theme, out int n);
                perTheme[e.Theme] = n + 1;
            }
            Assert.IsNotEmpty(perTheme,
                $"{LogPrefix} 기본 코호트 아이템에서 테마가 하나도 안 세어졌습니다 — " +
                "아래 «4/4가 성립한다»는 판정이 공허합니다(카탈로그가 안 실렸을 수 있습니다).");
            for (int i = 0; i < denied.Count; i++)
            {
                perTheme.TryGetValue(denied[i], out int n);
                Assert.AreEqual(EquipmentStatRules.StatCount, n,
                    $"{LogPrefix} 테마 '{denied[i]}'의 기본 코호트 아이템이 {n}종입니다" +
                    $"(스탯 슬롯 {EquipmentStatRules.StatCount}개와 다름) — " +
                    "그러면 이 테마를 거부하는 근거(«동전만으로 4/4가 완성된다»)가 성립하지 않습니다. " +
                    "테마 배정이 바뀌었다면 <b>거부 정책의 근거부터</b> 다시 세우십시오.");
            }

            // ---- 대조 ④ 양성 대조: 허용된 테마는 실제로 실린다 ----
            for (int i = 0; i < allowed.Length; i++)
            {
                using (var ok = new Fixture())
                {
                    ok.Add(SyntheticPrefix + "callow" + i, CostumeSourceKind.BaseTheme, allowed[i]);
                    var faults = new List<string>();
                    CostumeDescriptor[] loaded = CostumeCatalog.Build(ok.Manifests, faults);
                    Assert.IsEmpty(faults,
                        $"{LogPrefix} 허용 목록의 '{allowed[i]}' 코스튬이 거부됐습니다({faults.Count}건):\n  · " +
                        string.Join("\n  · ", faults) + "\n" +
                        "허용 목록과 실제 검사가 갈라졌습니다 — 그러면 출하된 무료 코스튬이 " +
                        "<b>조용히 안 실리고</b>, 그 화면은 «기능 미구현»과 구별되지 않습니다.");
                    Assert.AreEqual(1, loaded.Length,
                        $"{LogPrefix} 허용 목록의 '{allowed[i]}' 코스튬이 결함 0건인데 실리지도 않았습니다 — 적재기 이상.");
                }
            }

            // ---- 본론: 허용 목록 밖 테마는 전부 거부되고 + 신고되고 + 사유가 테마 이름을 말한다 ----
            for (int i = 0; i < denied.Count; i++)
            {
                using (var bad = new Fixture())
                {
                    bad.Add(SyntheticPrefix + "cdeny" + i, CostumeSourceKind.BaseTheme, denied[i]);
                    var faults = new List<string>();
                    CostumeDescriptor[] loaded = CostumeCatalog.Build(bad.Manifests, faults);

                    Assert.IsEmpty(loaded,
                        $"{LogPrefix} ★★★ <b>허용 목록 밖의 테마 '{denied[i]}' 코스튬이 실렸습니다.</b>\n" +
                        $"'{denied[i]}' 4/4는 전부 기본 42종이라 이 코스튬은 <b>인게임 재화만으로</b> 열립니다 — " +
                        "$4.99 팩의 간판 연출이 그 자리에서 샙니다" +
                        "(CHANNEL_PRICING_DECISIONS 54-1: cyber 4/4 = 동전 6,600 · 원형 B 1.7일).\n" +
                        "유료로 팔 코스튬이면 sourceKind를 Pack으로, sourceId를 packId로 적으십시오.");
                    Assert.IsNotEmpty(faults,
                        $"{LogPrefix} '{denied[i]}' 코스튬이 <b>조용히</b> 빠졌습니다 — 결함 신고가 0건입니다. " +
                        "조용한 미적재는 «만든 사람이 영원히 못 찾는» 형태입니다.");
                    StringAssert.Contains(denied[i], string.Join("\n", faults),
                        $"{LogPrefix} '{denied[i]}'의 결함 문장이 <b>어느 테마 때문인지</b> 말하지 않습니다.");
                }
            }

            Debug.Log($"{LogPrefix} 허용 목록 [{string.Join(", ", allowed)}] 통과 / " +
                      $"목록 밖 [{string.Join(", ", denied)}] {denied.Count}개 전부 거부 · 신고 확인. " +
                      $"(거부 테마는 각각 기본 코호트 {EquipmentStatRules.StatCount}종 = 4/4 성립)");
        }

        /// <summary>
        /// 문이 무는 다른 형태들. <b>전부 「거부되고 + 신고된다」 두 가지를 함께</b> 본다 —
        /// 조용한 미적재는 만든 사람이 영원히 못 찾는 형태다.
        /// <para>★ 단계 범위는 <see cref="CostumeEvolutionRules.StageCount"/>를 <b>참조</b>해서 만든다.
        /// 숫자를 베끼면 상태 수가 늘어나는 날 이 테스트만 옛 값을 지킨다.</para>
        /// </summary>
        [Test]
        public void 결함_있는_매니페스트는_실리지_않고_반드시_신고된다()
        {
            // ★ 키 «모양»은 맞고 «실재»만 틀린 값을 쓴다. 한글을 넣으면 모양 검사에서 먼저 걸려
            //   「모르는 테마」 갈래를 <b>한 번도 안 밟고</b> 초록이 난다(라벨과 실제가 갈라진 감사).
            AssertRejected("모르는 기본 코호트 테마",
                f => f.Add(SyntheticPrefix + "cbad", CostumeSourceKind.BaseTheme, "nosuchtheme"));

            AssertRejected("sourceId에 원문(키 모양 위반)",
                f => f.Add(SyntheticPrefix + "craw2", CostumeSourceKind.BaseTheme, "사무직테마"));

            // ★ 실재하는 테마인데 <b>허용 목록 밖</b>. 「모르는 테마」와 갈래가 다르다 —
            //   이 줄이 없으면 위 「모르는 테마」 초록이 «유료 경계도 막힌다»로 오독된다.
            AssertRejected($"허용 목록 밖의 실재 테마('{ItemCatalog.ThemeCyber}' — 유료 팩과 같은 축)",
                f => f.Add(SyntheticPrefix + "ccyber", CostumeSourceKind.BaseTheme, ItemCatalog.ThemeCyber));

            AssertRejected("앱보다 새 스키마를 요구",
                f =>
                {
                    CostumeManifestSO m = f.Add(SyntheticPrefix + "cnew", CostumeSourceKind.Pack,
                        SyntheticPrefix + "pknew");
                    m.requiresSchemaVersion = CostumeManifestSO.SchemaVersion + 1;
                });

            AssertRejected("costumeKey 모양 위반",
                f => f.Add("Costume.Office", CostumeSourceKind.Pack, SyntheticPrefix + "pkx"));

            AssertRejected("displayNameKey에 원문",
                f =>
                {
                    CostumeManifestSO m = f.Add(SyntheticPrefix + "craw", CostumeSourceKind.Pack,
                        SyntheticPrefix + "pkraw");
                    m.displayNameKey = "사무직 코스튬";
                });

            AssertRejected($"단계 번호가 0~{CostumeEvolutionRules.MasterStage} 밖",
                f =>
                {
                    CostumeManifestSO m = f.Add(SyntheticPrefix + "cstage", CostumeSourceKind.Pack,
                        SyntheticPrefix + "pkstage");
                    m.stageShapes = new[]
                    {
                        new CostumeStageOverride { stage = CostumeEvolutionRules.StageCount, shapes = null },
                    };
                });

            AssertRejected("같은 단계를 두 번",
                f =>
                {
                    CostumeManifestSO m = f.Add(SyntheticPrefix + "cdup", CostumeSourceKind.Pack,
                        SyntheticPrefix + "pkdup");
                    m.stageShapes = new[]
                    {
                        new CostumeStageOverride { stage = CostumeEvolutionRules.MasterStage, shapes = null },
                        new CostumeStageOverride { stage = CostumeEvolutionRules.MasterStage, shapes = null },
                    };
                });

            AssertRejected("좌표 스트림 문법 위반",
                f =>
                {
                    CostumeManifestSO m = f.Add(SyntheticPrefix + "cprop", CostumeSourceKind.Pack,
                        SyntheticPrefix + "pkprop");
                    // 점 2개라고 선언해 놓고 좌표를 한 점 분량만 준다 — 「반쯤 읽힌 프롭」의 실물.
                    AccessoryWornShapeData broken = ValidPropShape("fixture_broken");
                    var terms = new List<float>(broken.terms);
                    terms.RemoveRange(terms.Count - 6, 6);
                    broken.terms = terms.ToArray();
                    m.propShapes = new[] { broken };
                });

            AssertRejected("costumeKey 중복",
                f =>
                {
                    f.Add(SyntheticPrefix + "csame", CostumeSourceKind.Pack, SyntheticPrefix + "pka");
                    f.Add(SyntheticPrefix + "csame", CostumeSourceKind.Pack, SyntheticPrefix + "pkb");
                });

            AssertRejected("같은 소속을 둘이 주장",
                f =>
                {
                    f.Add(SyntheticPrefix + "cone", CostumeSourceKind.Pack, SyntheticPrefix + "pksame");
                    f.Add(SyntheticPrefix + "ctwo", CostumeSourceKind.Pack, SyntheticPrefix + "pksame");
                });

            AssertRejected("목록에 빈 항목(깨진 에셋)",
                f => f.Manifests.Add(null));
        }

        /// <summary>결함 하나를 심고 <b>「안 실린다 + 신고된다」</b>를 함께 확인한다.</summary>
        private static void AssertRejected(string what, Action<Fixture> plant)
        {
            using (var f = new Fixture())
            {
                plant(f);
                int planted = f.Manifests.Count;

                var faults = new List<string>();
                CostumeDescriptor[] loaded = CostumeCatalog.Build(f.Manifests, faults);

                Assert.IsNotEmpty(faults,
                    $"{LogPrefix} 「{what}」가 <b>신고 없이</b> 지나갔습니다. " +
                    "결함은 고치지 않고 신고한다 — 조용히 버리면 증상이 사라지고 아무도 안 고칩니다.");
                Assert.Less(loaded.Length, planted,
                    $"{LogPrefix} 「{what}」를 심었는데 {planted}개가 전부 실렸습니다 — " +
                    "신고만 하고 <b>싣기는 실은</b> 상태이고, 그건 검증을 통과하지 않은 값이 화면에 닿는 경로입니다.");
            }
        }
    }
}
