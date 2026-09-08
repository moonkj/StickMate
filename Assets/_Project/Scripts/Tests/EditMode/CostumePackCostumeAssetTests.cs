using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>유료 코스튬 3종(사이버 · 광부 · 대마법사) 실물 에셋</b> — 2026-09-08 R30.
    /// <see cref="CostumeOfficeAssetTests"/>가 무료 오피스에 대해 하는 것과 <b>같은 형태</b>다.
    ///
    /// ============================================================================
    /// ★★★ 이 파일의 존재 이유는 조형이 아니라 <b>$4.99</b>다
    /// ============================================================================
    /// <c>docs/strategy/CHANNEL_PRICING_DECISIONS.md</c> 54-1절(<c>product-strategy</c> 실측)이
    /// 신고한 구멍: 예전 <c>CostumeCatalog.AuditSource</c>는 기본 코호트 <b><c>mil</c>만</b>
    /// 거부하고 <c>ink</c>·<c>sport</c>·<c>office</c>·<c>cyber</c>·<c>neon</c>은 통과시켰다.
    ///
    /// <para>⇒ 누군가 <c>costume.cyber</c>를 <see cref="CostumeSourceKind.BaseTheme"/>/<c>cyber</c>로
    /// 저작했다면 <b>감사도 초록 · 테스트도 초록</b>이었을 것이고, $4.99 팩의 간판 연출이
    /// <b>동전 6,600(원형 B 1.7일)</b>에 열렸을 것이다 — <c>cyber</c> 4/4(왕관·외알안경·펜던트·긴망토)가
    /// <b>전부 기본 42종</b>이기 때문이다.</para>
    ///
    /// <para>★ 2026-09-08 — security 라운드가 <see cref="CostumeCatalog.AllowedBaseThemes"/>
    /// 화이트리스트로 <b>이 구멍을 막았다</b>(지금 허용은 <c>office</c> 하나뿐). 아래
    /// <see cref="구멍_닫힘_BaseTheme_사이버는_프로덕션_감사를_거부한다"/>가 그것을 <b>매 실행
    /// 증명</b>한다 — 화이트리스트가 다시 느슨해지면 이 테스트가 빨개진다. <c>product-strategy</c>가
    /// 리더에게 올린 문장 그대로: <i>「$4.99 상품 다섯 개의 간판이, 코드가 아니라 저작 규율
    /// 하나에 매달려 있다.」</i> 이제 그 저작 규율은 코드(화이트리스트)로 강제된다.</para>
    ///
    /// ============================================================================
    /// 골든의 출처
    /// ============================================================================
    ///  · <b>소속·가격</b> — <c>docs/strategy/CHANNEL_PRICING_DECISIONS.md</c> 55절 SKU 표
    ///  · <b>단계별 선 수</b> — <c>docs/EQUIPMENT_SHAPE_SPEC_COSTUME_PROPS.md</c> 3-1 / 3-3 / 3-4
    ///  · <b>각도표</b> — <c>docs/UX_MOTION_COSTUME_FOCUS.md</c> 16-3절 「전사용 표」
    ///    (<c>A(앞팔) = right*</c>, <c>B(뒷팔) = left*</c> — 뒤집으면 팔이 통째로 바뀐다)
    /// </summary>
    public sealed class CostumePackCostumeAssetTests
    {
        private const string LogPrefix = "[코스튬팩]";

        /// <summary>출하 라인업 — <b>상품 판정</b>이다(조형이 아니다).
        /// 항목: (costumeKey, 소속 갈래, sourceId).</summary>
        private static readonly (string Key, CostumeSourceKind Kind, string SourceId)[] Lineup =
        {
            ("costume.office", CostumeSourceKind.BaseTheme, "office"),      // $0    — 기본 42종
            ("costume.cyber",  CostumeSourceKind.Pack,      "pack.cyber"),  // $4.99
            ("costume.mine",   CostumeSourceKind.Pack,      "pack.mine"),   // $4.99
            ("costume.arcane", CostumeSourceKind.Pack,      "pack.arcane"), // $4.99
        };

        /// <summary>단계별 조각 수 — 설계 3-1 / 3-3 / 3-4의 S0/S1/S2/S3.</summary>
        private static readonly Dictionary<string, int[]> GoldenStageCounts = new Dictionary<string, int[]>
        {
            { "costume.cyber",  new[] { 3, 6, 8, 11 } },
            { "costume.mine",   new[] { 3, 8, 11, 13 } },
            { "costume.arcane", new[] { 5, 9, 13, 17 } },
        };

        /// <summary>각도표 — (B어깨, B팔꿈치, A어깨, A팔꿈치, 기울임, dY, propFrame).
        /// <c>CostumeKeypose</c>의 <b>필드 선언 순서 그대로</b>다.</summary>
        private static readonly Dictionary<string, float[,]> GoldenKeys = new Dictionary<string, float[,]>
        {
            { "costume.cyber", new[,]
                {
                    {  48.0f, 68.0f,  62.0f, 56.0f, -2.0f,  0.000f, 0f },
                    {  44.0f, 72.0f,  80.0f, 36.5f, -1.0f,  0.000f, 0f },
                    {  63.0f, 34.5f,  58.0f, 60.0f, -1.0f,  0.000f, 0f },
                    {  56.0f, 58.0f,  70.0f, 44.0f,  2.0f, -0.018f, 0f },
                } },
            { "costume.mine", new[,]
                {
                    { -24.0f, 28.0f,  86.0f, 34.0f, -1.0f,  0.000f, 0f },
                    { -30.0f, 24.0f, 142.0f, 52.0f, -4.0f,  0.018f, 0f },
                    { -18.0f, 34.0f,  78.5f, 70.0f,  5.0f, -0.030f, 1f },   // ★ R2-1 · 타격 섬광
                    { -20.0f, 30.0f,  39.5f, 58.5f,  2.0f, -0.020f, 0f },   // ★ R2-2
                } },
            { "costume.arcane", new[,]
                {
                    { -34.0f,  96.0f, -30.0f, 92.0f, -2.0f, 0.000f, 0f },
                    { -32.0f,  92.0f, -34.0f, 84.0f,  1.5f, 0.000f, 0f },
                    { -40.0f, 100.0f, -26.0f, 88.0f,  1.0f, 0.000f, 0f },   // ★ R2-3 (102°는 접선 절벽 1.5° — 기각됨)
                    { -28.0f,  88.0f,  16.0f, 84.0f, -3.0f, 0.020f, 0f },
                } },
        };

        private static CostumeDescriptor Load(string key)
        {
            CostumeCatalog.ResetForTesting();
            CostumeDescriptor c = CostumeCatalog.Find(key);
            Assert.IsNotNull(c,
                $"{LogPrefix} '{key}'가 실리지 않았다. CostumeCatalog가 훑는 폴더는 " +
                $"'{CostumeCatalog.ResourceFolder}' 하나이고, 다른 폴더에 두면 <b>조용히</b> 안 실린다 — " +
                "그 화면은 「기능 미구현」과 구별되지 않는다. 결함이 있었다면 로드가 LogError로 사유를 남겼다.");
            return c;
        }

        // ====================================================================
        // 1. ★★★ P0 — 소속(sourceKind)
        // ====================================================================

        [Test]
        public void 출하_라인업의_소속이_상품_판정과_같다()
        {
            CostumeCatalog.ResetForTesting();
            IReadOnlyList<CostumeDescriptor> shipped = CostumeCatalog.Costumes;

            // 양성 대조 — 골든 표가 두 갈래를 <b>둘 다</b> 담고 있는가.
            //   한 갈래뿐이면 아래 비교는 「Pack인가」가 아니라 「전부 같은 값인가」를 재는 것이 된다.
            bool hasBase = false, hasPack = false;
            foreach ((string _, CostumeSourceKind kind, string _2) in Lineup)
            {
                hasBase |= kind == CostumeSourceKind.BaseTheme;
                hasPack |= kind == CostumeSourceKind.Pack;
            }
            Assert.IsTrue(hasBase && hasPack,
                $"{LogPrefix} 골든 라인업이 갈래를 하나만 담고 있다 — 그러면 이 비교는 " +
                "「소속이 맞는가」가 아니라 「전부 같은 값인가」를 재는 것이다.");

            var seen = new List<string>();
            foreach (CostumeDescriptor c in shipped) seen.Add(c.CostumeKey);

            foreach ((string key, CostumeSourceKind kind, string sourceId) in Lineup)
            {
                CostumeDescriptor c = CostumeCatalog.Find(key);
                Assert.IsNotNull(c, $"{LogPrefix} 라인업의 '{key}'가 실리지 않았다. 실린 것: {string.Join(", ", seen)}");
                Assert.AreEqual(kind, c.SourceKind,
                    $"{LogPrefix} ★★★ '{key}'의 소속이 {c.SourceKind}다(기대 {kind}).\n" +
                    "  · Pack이어야 할 것이 BaseTheme이면 <b>$4.99 팩의 간판 연출이 기본 42종만으로 열린다</b>" +
                    "(CHANNEL_PRICING_DECISIONS 54-1: cyber 4/4 = 동전 6,600 · 원형 B 1.7일).\n" +
                    "  · 그리고 그 저작은 <b>CostumeCatalog.AuditSource를 통과한다</b> — 감사는 mil만 막는다. " +
                    "즉 이 단언이 그것을 잡는 <b>유일한 계기</b>다.\n" +
                    "  · 값을 바꾸려면 product-strategy 판정을 먼저 받고 55절 SKU 표와 <b>함께</b> 고쳐라.");
                Assert.AreEqual(sourceId, c.SourceId,
                    $"{LogPrefix} '{key}'의 sourceId가 '{c.SourceId}'다(기대 '{sourceId}'). " +
                    "Pack 소속의 sourceId는 <b>packId</b>이고 그것은 엔타이틀먼트 키다 — " +
                    "틀리면 산 사람에게도 안 열린다.");
                Assert.AreEqual(CostumeManifestSO.SchemaVersion, c.ManifestSchemaVersion,
                    $"{LogPrefix} '{key}'가 요구하는 스키마가 현재 판과 다르다 — " +
                    "낮게 적으면 구버전 앱이 전용 모션만 조용히 빼고 싣는다.");
            }

            Assert.AreEqual(Lineup.Length, shipped.Count,
                $"{LogPrefix} 실린 코스튬이 {shipped.Count}종인데 골든 라인업은 {Lineup.Length}종이다 " +
                $"(실린 것: {string.Join(", ", seen)}).\n" +
                "코스튬을 늘리는 것 자체는 프로덕션 .cs 0줄이다(원칙 4는 그대로다). " +
                "다만 <b>무료/유료 경계는 상품 판정</b>이라 여기 한 줄을 더하게 해 뒀다 — " +
                "그 한 줄이 없으면 새 코스튬이 어느 쪽으로 저작됐는지 아무도 안 본다.");

            Debug.Log($"{LogPrefix} ① 통과 — {shipped.Count}종: " +
                      string.Join(" · ", System.Array.ConvertAll(Lineup, e => $"{e.Key}={e.Kind}/{e.SourceId}")));
        }

        /// <summary>
        /// ★ 2026-09-08 — security 라운드가 <c>CostumeCatalog.AllowedBaseThemes()</c> 화이트리스트로
        /// 구멍을 막았다. 이 테스트는 이제 <b>구멍이 실재하지 않음을 매 실행 증명</b>한다(부재 단언의
        /// 대조는 여전히 유효하다 — 화이트리스트가 다시 느슨해지면 이 테스트가 빨개져야 한다).
        /// 원래 이름(<c>구멍_실증_...</c>)과 반대 방향 단언이므로 이름도 뒤집었다 —
        /// <see cref="CostumeCatalog.AllowedBaseThemes"/>를 참고하라.
        /// </summary>
        [Test]
        public void 구멍_닫힘_BaseTheme_사이버는_프로덕션_감사를_거부한다()
        {
            var m = ScriptableObject.CreateInstance<CostumeManifestSO>();
            try
            {
                m.name = "costumefixture.cybertheme";
                m.costumeKey = "costumefixture.cybertheme";
                m.requiresSchemaVersion = CostumeManifestSO.SchemaVersion;
                m.sourceKind = CostumeSourceKind.BaseTheme;
                m.sourceId = ItemCatalog.ThemeCyber;                  // ★ 유료 팩과 같은 축의 테마
                m.displayNameKey = "costumefixture.cybertheme.name";

                var faults = new List<string>();
                CostumeDescriptor[] built = CostumeCatalog.Build(new[] { m }, faults);

                // 음성 대조 — 같은 감사가 mil은 실제로 막는가(막는 능력 자체가 살아 있는가).
                var milFaults = new List<string>();
                var mil = ScriptableObject.CreateInstance<CostumeManifestSO>();
                try
                {
                    mil.name = "costumefixture.miltheme";
                    mil.costumeKey = "costumefixture.miltheme";
                    mil.requiresSchemaVersion = CostumeManifestSO.SchemaVersion;
                    mil.sourceKind = CostumeSourceKind.BaseTheme;
                    mil.sourceId = ItemCatalog.ThemeMil;
                    mil.displayNameKey = "costumefixture.miltheme.name";
                    CostumeCatalog.Build(new[] { mil }, milFaults);
                }
                finally { Object.DestroyImmediate(mil); }

                Assert.IsNotEmpty(milFaults,
                    $"{LogPrefix} 감사가 '{ItemCatalog.ThemeMil}'조차 안 막는다 — " +
                    "그러면 아래 «cyber는 통과한다»는 「구멍」이 아니라 「감사가 통째로 죽었다」는 뜻이다.");

                Assert.IsNotEmpty(faults,
                    $"{LogPrefix} BaseTheme/'{ItemCatalog.ThemeCyber}'가 다시 결함 0건으로 통과한다 — " +
                    "CostumeCatalog.AllowedBaseThemes()에서 cyber가 빠졌는지, 혹은 화이트리스트 자체가 " +
                    "무너졌는지 확인하라. $4.99 팩 간판이 동전 6,600에 새는 구멍이 재발한 것이다.");
                Assert.IsEmpty(built,
                    $"{LogPrefix} 결함을 신고하고도 매니페스트를 실었다 — 신고와 적재가 갈렸다.");

                Debug.Log($"{LogPrefix} ② 구멍 닫힘 확인 — BaseTheme/'{ItemCatalog.ThemeCyber}'는 " +
                          $"{faults.Count}건으로 거부되고 '{ItemCatalog.ThemeMil}'도 {milFaults.Count}건으로 " +
                          "거부된다. ⇒ 화이트리스트가 $4.99 경계를 지키고 있다.");
            }
            finally { Object.DestroyImmediate(m); CostumeCatalog.ResetForTesting(); }
        }

        /// <summary>
        /// ★ 팩 코스튬의 <c>sourceId</c>는 <b>packId</b>이지 기본 코호트 테마 이름이 아니다.
        /// 테마 이름을 적으면 <see cref="CostumeResolver"/>가 <see cref="PackRegistry.FindByCohort"/>로
        /// 찾을 팩이 없어 <b>영원히 null</b>이고, 그 화면은 「기능 미구현」과 같아 보인다.
        /// <para><b>골든이 아니라 구조 규칙</b>이다 — 실린 에셋에서 전부 파생된다.</para>
        /// </summary>
        [Test]
        public void 팩_코스튬의_소속은_기본코호트_테마_이름이_아니다()
        {
            string[] themes = ItemCatalog.AllThemes();
            Assert.IsNotEmpty(themes, $"{LogPrefix} 테마 표가 비었다 — 아래 비교가 공허하다(거짓 통과 #5).");

            CostumeCatalog.ResetForTesting();
            var violations = new List<string>();
            int packCount = 0;
            foreach (CostumeDescriptor c in CostumeCatalog.Costumes)
            {
                if (c.SourceKind != CostumeSourceKind.Pack) continue;
                packCount++;
                for (int i = 0; i < themes.Length; i++)
                {
                    if (c.SourceId != themes[i]) continue;
                    violations.Add($"  {c.CostumeKey} → sourceId '{c.SourceId}' 는 기본 코호트 테마 이름이다");
                }
            }
            Assert.Greater(packCount, 0,
                $"{LogPrefix} 팩 소속 코스튬이 0종이다 — 이 감사가 아무것도 안 잰다.");
            Assert.IsEmpty(violations,
                $"{LogPrefix} 팩 코스튬이 테마 이름을 소속으로 적었다({violations.Count}건):\n" +
                string.Join("\n", violations) + "\n" +
                "Pack 갈래의 sourceId는 packId다. 테마 이름을 적으면 해석기가 그 코스튬을 " +
                "<b>영원히 못 돌려준다</b>.");

            Debug.Log($"{LogPrefix} ③ 통과 — 팩 코스튬 {packCount}종, 테마 {themes.Length}개와 충돌 0건.");
        }

        // ====================================================================
        // 2. 조형 — 단계별 선 수
        // ====================================================================

        [Test]
        public void 팩_코스튬_프롭이_설계_단계별_선수와_같다()
        {
            foreach (KeyValuePair<string, int[]> e in GoldenStageCounts)
            {
                CostumeDescriptor c = Load(e.Key);
                int[] want = e.Value;
                Assert.AreEqual(CostumeEvolutionRules.StageCount, want.Length,
                    $"{LogPrefix} 골든이 단계 수와 안 맞는다.");

                var got = new int[CostumeEvolutionRules.StageCount];
                for (int s = 0; s < want.Length; s++)
                {
                    CostumeStageOverride? found = null;
                    foreach (CostumeStageOverride ov in c.StageShapes)
                    {
                        if (ov.stage == s) found = ov;
                    }
                    Assert.IsTrue(found.HasValue,
                        $"{LogPrefix} '{e.Key}'에 단계 {s} 조형이 없다 — 렌더러는 그 단계에서 " +
                        "propShapes로 떨어져 <b>이전 단계와 같은 그림</b>을 그린다.");
                    got[s] = found.Value.shapes.Length;
                    Assert.AreEqual(want[s], got[s],
                        $"{LogPrefix} '{e.Key}' 단계 {s}가 {got[s]}선이다(설계 {want[s]}선).");

                    foreach (AccessoryWornShapeData sh in found.Value.shapes)
                    {
                        Assert.IsTrue(AccessoryWornShapeReader.Validate(sh, out string err),
                            $"{LogPrefix} '{e.Key}' 단계 {s}의 '{sh.name}' 좌표 스트림이 문법에 안 맞는다: {err}");
                    }
                }

                // propShapes(폴백 기본 조각)는 S1과 같은 그림이다 — 출하 오피스의 관례.
                Assert.AreEqual(want[1], c.PropShapes.Count,
                    $"{LogPrefix} '{e.Key}'의 propShapes가 {c.PropShapes.Count}선이다(S1 {want[1]}선과 같아야 한다). " +
                    "propShapes는 단계 오버라이드가 없을 때의 폴백이고, 오피스가 S1을 두는 관례를 따른다.");
                Assert.AreEqual(0f, c.PropAnchorOffsetXInH, 0.0001f,
                    $"{LogPrefix} '{e.Key}'의 좌표는 이미 캐릭터 루트 기준이라 앵커 오프셋이 0이다.");

                Debug.Log($"{LogPrefix} ④ '{e.Key}' 단계별 {string.Join("/", got)}선 · " +
                          $"기본 {c.PropShapes.Count}선.");
            }
        }

        // ====================================================================
        // 3. 모션 — 각도표
        // ====================================================================

        [Test]
        public void 팩_코스튬_키포즈가_설계_각도표와_같다()
        {
            foreach (KeyValuePair<string, float[,]> e in GoldenKeys)
            {
                CostumeDescriptor c = Load(e.Key);
                CostumeKeyposeTableSO table = c.Keyposes;
                Assert.IsNotNull(table,
                    $"{LogPrefix} '{e.Key}'에 전용 모션 표가 연결돼 있지 않다 — " +
                    "프롭만 서고 캐릭터는 기존 관망 자세로 남는다(그 화면은 「모션 미구현」과 구별되지 않는다).");
                Assert.IsTrue(table.IsUsable(out string error), $"{LogPrefix} '{e.Key}' 표가 거부됐다: {error}");
                Assert.AreEqual(CostumeFocusRhythm.RestingStepsPerSecond, table.stepsPerSecond,
                    $"{LogPrefix} '{e.Key}'는 «쉬는» 스텝레이트로 돈다(절정 소구간만 코드가 올린다).");

                float[,] want = e.Value;
                Assert.AreEqual(want.GetLength(0), table.keyposes.Length,
                    $"{LogPrefix} '{e.Key}'의 키 수가 다르다.");
                Assert.IsEmpty(table.stageKeyposeStart ?? new int[0],
                    $"{LogPrefix} '{e.Key}'의 stageKeyposeStart가 비어 있지 않다 — " +
                    "설계 16-3은 전 단계가 같은 표를 쓴다(단계별로 자르면 초기 사용자만 동작이 짧아져 " +
                    "「보상」이 아니라 「결핍」으로 읽힌다).");

                for (int k = 0; k < want.GetLength(0); k++)
                {
                    CostumeKeypose key = table.keyposes[k];
                    Assert.AreEqual(want[k, 0], key.leftUpperArm, 0.001f, $"{LogPrefix} {e.Key} K{k} B(뒷팔) 어깨각.");
                    Assert.AreEqual(want[k, 1], key.leftLowerArm, 0.001f, $"{LogPrefix} {e.Key} K{k} B(뒷팔) 팔꿈치.");
                    Assert.AreEqual(want[k, 2], key.rightUpperArm, 0.001f, $"{LogPrefix} {e.Key} K{k} A(앞팔) 어깨각.");
                    Assert.AreEqual(want[k, 3], key.rightLowerArm, 0.001f, $"{LogPrefix} {e.Key} K{k} A(앞팔) 팔꿈치.");
                    Assert.AreEqual(want[k, 4], key.leanDegrees, 0.001f, $"{LogPrefix} {e.Key} K{k} 기울임.");
                    Assert.AreEqual(want[k, 5], key.bodyOffsetY, 0.0001f, $"{LogPrefix} {e.Key} K{k} dY(H 배수).");
                    Assert.AreEqual((int)want[k, 6], key.propFrame, $"{LogPrefix} {e.Key} K{k} propFrame.");

                    // 리그 한계 — SceneBootstrapper의 어깨각 범위와 규칙 B 팔꿈치 상한(설계 16-3 표).
                    Assert.IsTrue(key.leftUpperArm >= -60f && key.leftUpperArm <= 150f
                               && key.rightUpperArm >= -60f && key.rightUpperArm <= 150f,
                        $"{LogPrefix} {e.Key} K{k} 어깨각이 리그 범위[-60, 150] 밖이다.");
                    Assert.IsTrue(key.leftLowerArm >= 0f && key.rightLowerArm >= 0f,
                        $"{LogPrefix} {e.Key} K{k} 팔꿈치 굽힘이 음수다(규약상 항상 ≥ 0).");
                    Assert.IsTrue(key.bodyOffsetY == 0f
                               || (Mathf.Abs(key.bodyOffsetY) >= 0.017f - 1e-4f
                                && Mathf.Abs(key.bodyOffsetY) <= 0.032f + 1e-4f),
                        $"{LogPrefix} {e.Key} K{k} bodyOffsetY={key.bodyOffsetY}가 설계 대역 " +
                        "{0} ∪ [0.017, 0.032] H 밖이다(하한 아래는 안 보이고 상한 위는 「몸이 뛴다」).");
                }
                Debug.Log($"{LogPrefix} ⑤ '{e.Key}' {table.keyposes.Length}키 × 7값 대조 통과 " +
                          $"(스텝레이트 {table.stepsPerSecond}/초).");
            }
        }

        /// <summary>
        /// ★ <b>선언은 됐는데 아무도 안 읽는 값</b>이 하나 있다 — 광부 K2의 <c>propFrame = 1</c>.
        /// 설계(<c>UX_MOTION_COSTUME_FOCUS</c> 16-3 · <c>EQUIPMENT_SHAPE_SPEC_COSTUME_PROPS</c> 3-3)는
        /// 그 프레임에 <b>타격 섬광 3획</b>을 켜라고 적었지만,
        /// <b><c>CostumeManifestSO</c>에 그 조각을 담을 자리가 없고 렌더러도 이 값을 안 읽는다.</b>
        /// <para>조용히 0으로 바꾸면 설계 의도가 사라지고, 조용히 두면 <b>안 되는 줄 아무도 모른다</b>.
        /// 그래서 <b>「건너뜀」으로 러너에 계속 보이게</b> 한다(CLAUDE.md 플랫폼 갭과 같은 처방).</para>
        /// </summary>
        [Test]
        public void 광부_propFrame_1은_아직_아무도_안_읽는다()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Interaction", "CostumePropRenderer.cs");
            Assert.IsTrue(System.IO.File.Exists(path), $"{LogPrefix} 렌더러 소스를 못 찾았다: {path}");
            string src = System.IO.File.ReadAllText(path);

            // 양성 대조 — 같은 파서가 실재하는 이름은 찾는가(못 찾으면 아래 «없다»는 «못 본다»다).
            StringAssert.Contains("ResolveStageShapes", src,
                $"{LogPrefix} 파서가 렌더러에서 아무것도 못 찾는다 — 아래 판정이 공허하다.");

            CostumeDescriptor mine = Load("costume.mine");
            bool anyFrame = false;
            foreach (CostumeKeypose k in mine.Keyposes.keyposes) anyFrame |= k.propFrame != 0;
            Assert.IsTrue(anyFrame,
                $"{LogPrefix} 광부 표에 propFrame != 0인 키가 없다 — 설계 16-3은 K2에 1을 적었다.");

            if (src.Contains("propFrame"))
            {
                Assert.Pass($"{LogPrefix} 렌더러가 propFrame을 읽기 시작했다 — " +
                    "이제 타격 섬광 3획(EQUIPMENT_SHAPE_SPEC_COSTUME_PROPS 3-3)을 매니페스트에 " +
                    "담을 자리가 필요하고, 이 테스트를 <b>실제 검증</b>으로 승격해야 한다.");
            }

            Assert.Ignore($"{LogPrefix} ★ 미배선 갭(고의로 「건너뜀」으로 남긴다) — " +
                "광부 K2가 propFrame=1을 선언하지만 CostumePropRenderer는 이 값을 <b>한 번도 안 읽는다</b>" +
                "(소스 히트 0건). 매니페스트에도 「프레임별 조각」 자리가 없다. " +
                "⇒ 타격 섬광 3획이 화면에 안 뜬다. 조각 자리를 만들면 " +
                "CostumeManifestSO.SchemaVersion v2→v3이라 <b>되돌릴 수 없는 결정</b>이고 " +
                "game-architect·리더 판정이 먼저다 — 이 라운드 범위 밖으로 남긴다.");
        }
    }
}
