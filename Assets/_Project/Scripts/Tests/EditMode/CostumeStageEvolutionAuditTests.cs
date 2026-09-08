using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>진화가 실제로 눈에 보이는가</b> — 출하 코스튬 전수 감사(2026-09-08 R30).
    ///
    /// ============================================================================
    /// 왜 이 파일이 생겼는가 — 「침묵이 곧 결함」이었다
    /// ============================================================================
    /// <c>docs/EQUIPMENT_SHAPE_SPEC_COSTUME_PROPS_R29.md</c> 0-1절 <b>R29-2</b>:
    /// <i>"진화 4단계인데 그림은 2개뿐이다 … 사용자는 50h·100h에서 아무 변화도 못 본다"</i>.
    /// <c>CostumePropRenderer.ResolveStageShapes</c>는 <b>단계 번호가 정확히 일치하는 오버라이드만</b>
    /// 쓰고 없으면 <c>propShapes</c>로 떨어진다 — 그래서 <c>stage 0</c> 하나만 적어도
    /// <b>모든 테스트가 초록</b>이었다. 그 초록이 곧 「100시간을 쌓아도 화면이 안 바뀐다」다.
    ///
    /// <para>R29 7절이 <c>test-engineer</c>에게 넘긴 문장 그대로다:
    /// <i>"제안: 「단계별 그림이 실제로 다른가」를 보는 테스트가 없다."</i></para>
    ///
    /// ============================================================================
    /// ★ 기대값을 프로덕션 함수로 만들지 않는다
    /// ============================================================================
    /// 단계 해석을 <c>CostumePropRenderer</c>에서 부르지 <b>않고</b> 여기서 다시 적는다.
    /// 그 함수를 부르면 「해석이 틀어져도 기대값이 함께 틀어져」 아무것도 못 잰다
    /// (TEAM.md 「생성기와 검사기가 같이 틀린다」). 대신 <b>규칙 한 줄</b>을 옮겨 적고,
    /// 그 규칙이 렌더러와 같은지는 <see cref="단계_해석_규칙이_렌더러_소스와_같은_문장이다"/>가
    /// <b>소스 텍스트</b>로 확인한다.
    /// </summary>
    public sealed class CostumeStageEvolutionAuditTests
    {
        private const string LogPrefix = "[코스튬진화]";

        /// <summary>단계 해석 — <b>렌더러 규칙의 독립 재구현</b>(프로덕션 함수를 부르지 않는다).
        /// 단계 번호가 정확히 일치하고 조각이 비지 않은 오버라이드만 쓰고, 없으면 기본 조각.</summary>
        private static IReadOnlyList<AccessoryWornShapeData> ShapesForStage(CostumeDescriptor c, int stage)
        {
            IReadOnlyList<CostumeStageOverride> overrides = c.StageShapes;
            for (int i = 0; i < overrides.Count; i++)
            {
                CostumeStageOverride o = overrides[i];
                if (o.stage == stage && o.shapes != null && o.shapes.Length > 0) return o.shapes;
            }
            return c.PropShapes;
        }

        /// <summary>그 단계 그림의 <b>지문</b> — 조각 이름 + 실제 점 좌표. 이름만 세면
        /// 「이름은 같은데 좌표가 통째로 바뀐」 경우를 못 보고, 좌표만 세면 순서 바뀜을 진화로 읽는다.</summary>
        private static string Fingerprint(IReadOnlyList<AccessoryWornShapeData> shapes)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < shapes.Count; i++)
            {
                AccessoryWornShapeData sh = shapes[i];
                Assert.IsTrue(AccessoryWornShapeReader.TryBuild(sh, AccessoryWornFrame.Unit,
                    false, out Vector3[] pts, out string err),
                    $"{LogPrefix} '{sh.name}' 해석 실패: {err}");
                sb.Append(sh.name).Append(':').Append(sh.loop ? 'L' : 'l')
                  .Append(sh.filled ? 'F' : 'f').Append(sh.tone).Append('[');
                for (int p = 0; p < pts.Length; p++)
                {
                    sb.Append(pts[p].x.ToString("F4")).Append(',').Append(pts[p].y.ToString("F4")).Append(';');
                }
                sb.Append("]|");
            }
            return sb.ToString();
        }

        // ====================================================================
        // 1. 양성 대조 — 지문 비교기가 실제로 「같음」을 잡는가
        // ====================================================================

        /// <summary>
        /// ★ 아래 본 감사는 <b>「전부 다르다」</b>는 단언이다. 비교기가 무엇이든 다르다고 답하면
        /// 그 초록은 아무것도 증명하지 않는다. 그래서 <b>일부러 단계 조형을 안 채운</b> 합성 코스튬을
        /// 같은 비교기에 넣어 <b>4단계 중 서로 다른 그림이 2개</b>로 나오는지 먼저 보인다 —
        /// 그것이 R29-2가 신고한 <b>출하 오피스의 원래 상태</b>이기도 하다.
        /// </summary>
        [Test]
        public void 양성대조_단계_조형을_안_채우면_그림이_2개로_나온다()
        {
            var m = ScriptableObject.CreateInstance<CostumeManifestSO>();
            try
            {
                m.name = "costumefixture.stagegap";
                m.costumeKey = "costumefixture.stagegap";
                m.requiresSchemaVersion = CostumeManifestSO.SchemaVersion;
                m.sourceKind = CostumeSourceKind.Pack;
                m.sourceId = "costumefixture.pkstagegap";
                m.displayNameKey = "costumefixture.stagegap.name";
                m.propShapes = new[] { Line("A", 0f, 0f, 1f, 0f), Line("B", 0f, 0.5f, 1f, 0.5f) };
                m.stageShapes = new[]
                {
                    new CostumeStageOverride { stage = 0, shapes = new[] { Line("A", 0f, 0f, 1f, 0f) } },
                };

                var faults = new List<string>();
                CostumeDescriptor[] built = CostumeCatalog.Build(new[] { m }, faults);
                Assert.IsEmpty(faults, $"{LogPrefix} 대조 픽스처가 거부됐다: {string.Join(" / ", faults)}");
                Assert.AreEqual(1, built.Length);

                var seen = new HashSet<string>();
                for (int s = 0; s < CostumeEvolutionRules.StageCount; s++)
                {
                    seen.Add(Fingerprint(ShapesForStage(built[0], s)));
                }
                Assert.AreEqual(2, seen.Count,
                    $"{LogPrefix} ★ 양성 대조 실패 — 단계 0만 채운 코스튬에서 서로 다른 그림이 " +
                    $"{seen.Count}개로 나왔다(기대 2). 비교기가 「같음」을 못 보거나 「다름」을 못 본다. " +
                    "그러면 아래 본 감사의 초록은 아무것도 증명하지 않는다.");
                Debug.Log($"{LogPrefix} ① 양성 대조 통과 — 단계 0만 채운 코스튬 = 서로 다른 그림 {seen.Count}개.");
            }
            finally { Object.DestroyImmediate(m); CostumeCatalog.ResetForTesting(); }
        }

        private static AccessoryWornShapeData Line(string name, float x0, float y0, float x1, float y1)
            => new AccessoryWornShapeData
            {
                name = name,
                strokeMult = 1f,
                swayStart = -1,
                terms = new[]
                {
                    2f,
                    1f, (float)AccessoryWornBasis.Height, 0f, 0f, 1f, x0,
                    1f, (float)AccessoryWornBasis.Height, 0f, 0f, 1f, y0,
                    1f, (float)AccessoryWornBasis.Height, 0f, 0f, 1f, x1,
                    1f, (float)AccessoryWornBasis.Height, 0f, 0f, 1f, y1,
                },
            };

        // ====================================================================
        // 2. 본 감사
        // ====================================================================

        /// <summary>
        /// ★ 출하 코스튬은 <b>단계 4개가 전부 다른 그림</b>이어야 한다.
        /// 같은 그림이 둘이면 그 임계(10h / 50h / 100h)를 넘긴 사용자에게 <b>아무 일도 안 일어난다</b>.
        /// </summary>
        [Test]
        public void 출하_코스튬은_진화_4단계가_서로_다른_그림이다()
        {
            CostumeCatalog.ResetForTesting();
            IReadOnlyList<CostumeDescriptor> costumes = CostumeCatalog.Costumes;
            Assert.IsNotEmpty(costumes,
                $"{LogPrefix} 실린 코스튬이 0개다 — 이 감사가 아무것도 안 잰다. " +
                "Resources/Items 아래 CostumeManifest 에셋을 확인하라.");

            var report = new List<string>();
            var faults = new List<string>();
            foreach (CostumeDescriptor c in costumes)
            {
                var byStage = new List<string>();
                var counts = new List<int>();
                for (int s = 0; s < CostumeEvolutionRules.StageCount; s++)
                {
                    IReadOnlyList<AccessoryWornShapeData> shapes = ShapesForStage(c, s);
                    counts.Add(shapes.Count);
                    byStage.Add(Fingerprint(shapes));
                }

                for (int a = 0; a < byStage.Count; a++)
                {
                    for (int b = a + 1; b < byStage.Count; b++)
                    {
                        if (byStage[a] != byStage[b]) continue;
                        faults.Add($"  {c.CostumeKey}: 단계 {a}와 단계 {b}가 <b>같은 그림</b>이다" +
                            $"(조각 {counts[a]}개). 그 사이 임계를 넘긴 사용자는 화면에서 아무 변화도 못 본다.");
                    }
                }

                // 진화는 자라는 방향이어야 한다 — 줄어들면 「번 것을 빼앗겼다」로 읽힌다.
                for (int s = 1; s < counts.Count; s++)
                {
                    if (counts[s] >= counts[s - 1]) continue;
                    faults.Add($"  {c.CostumeKey}: 단계 {s}의 조각이 {counts[s]}개로 " +
                        $"단계 {s - 1}({counts[s - 1]}개)보다 <b>줄었다</b>.");
                }
                report.Add($"{c.CostumeKey} {string.Join("/", counts)}선");
            }

            Assert.IsEmpty(faults,
                $"{LogPrefix} 진화가 화면에 안 나타나는 코스튬이 있다({faults.Count}건):\n" +
                string.Join("\n", faults) + "\n\n" +
                "CostumePropRenderer.ResolveStageShapes는 <b>단계 번호가 정확히 일치하는</b> 오버라이드만 " +
                "쓰고 없으면 propShapes로 떨어진다 — 그래서 stageShapes에 단계를 안 적으면 " +
                "<b>조용히</b> 이전 단계와 같은 그림이 된다.");

            Debug.Log($"{LogPrefix} ② 통과 — {string.Join(" · ", report)} " +
                      $"(단계 {CostumeEvolutionRules.StageCount}개가 전부 서로 다른 그림).");
        }

        /// <summary>
        /// ★ 위 감사가 옮겨 적은 <b>단계 해석 규칙</b>이 렌더러와 같은 문장인가.
        /// 규칙을 두 벌로 적었으므로 갈라짐을 잡는 자리가 필요하다 —
        /// <b>타입이 아니라 소스 텍스트</b>를 읽는다(플랫폼·타깃 무관).
        /// </summary>
        [Test]
        public void 단계_해석_규칙이_렌더러_소스와_같은_문장이다()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Interaction", "CostumePropRenderer.cs");
            Assert.IsTrue(System.IO.File.Exists(path), $"{LogPrefix} 렌더러 소스를 못 찾았다: {path}");
            string src = System.IO.File.ReadAllText(path);

            // 양성 대조 — 이 파서가 실제로 무언가를 찾는가(못 찾으면 아래 판정이 공허하다).
            StringAssert.Contains("ResolveStageShapes", src,
                $"{LogPrefix} 렌더러에서 ResolveStageShapes를 못 찾았다 — 이름이 바뀌었다면 " +
                "위 감사의 재구현도 함께 확인해야 한다.");

            const string needle = "if (o.stage == stage && o.shapes != null && o.shapes.Length > 0) return o.shapes;";
            StringAssert.Contains(needle, src,
                $"{LogPrefix} ★ 렌더러의 단계 해석 한 줄이 바뀌었다. " +
                "이 파일의 ShapesForStage는 그 한 줄의 <b>독립 재구현</b>이라 함께 고쳐야 한다 — " +
                "안 고치면 이 감사가 <b>없는 규칙</b>을 재고, 그 초록은 화면과 무관하다.");
            StringAssert.Contains("return costume.PropShapes;", src,
                $"{LogPrefix} 렌더러의 폴백(오버라이드 없으면 기본 조각)이 바뀌었다.");

            Debug.Log($"{LogPrefix} ③ 통과 — 단계 해석 규칙 2줄이 렌더러 소스와 같다.");
        }
    }
}
