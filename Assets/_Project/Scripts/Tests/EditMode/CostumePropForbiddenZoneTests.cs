using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 출하 코스튬 프롭의 <b>금지대</b> 감사(PR-4 / E-7-d) — 2026-09-08.
    ///
    /// ============================================================================
    /// 규칙과 그 숫자가 어디서 오는가
    /// ============================================================================
    /// <b>비접합 조각 쌍은 「닿거나(d = 0) 또는 2.00 W_P 이상 떨어져야」 한다.</b>
    /// 그 사이(금지대)에 있으면 두 선이 <b>붙지도 떨어지지도 않은</b> 상태로 보여
    /// 화면에서 한 줄로 뭉개지거나 서로를 흐린다.
    ///
    /// <para><c>W_P</c>는 프롭 획 폭이고, 이 저장소에서 그 값은
    /// <c>CostumePropRenderer.StrokeWidthRatio</c> 하나다 — <b>숫자로 베끼지 않고 소스에서 읽어 온다.</b>
    /// 베끼면 획이 바뀌는 날 이 검사가 조용히 옛 자로 잰다.</para>
    ///
    /// ============================================================================
    /// ★★ 양성 대조가 <b>둘</b>인 이유 — 하나였으면 고칠 필요 없는 것 4건을 고칠 뻔했다
    /// ============================================================================
    /// <c>design-motion</c>이 자기 검사기의 결함을 자백했다: 첫 판이 <b>끝점-선분 거리만</b> 재서
    /// <b>실제로 가로지르는(= 닿은) 쌍 4건</b>을 위반으로 <b>거짓 신고</b>했다.
    /// 그래서 이 파일은 대조를 둘 세운다:
    /// <list type="number">
    ///   <item><b>(가)</b> 금지대 한복판에 놓은 쌍이 <b>실제로 잡히는가</b>.</item>
    ///   <item><b>(나)</b> 십자로 가로지르는 쌍이 <b>0으로 나오는가</b>(= 위반이 아니다).</item>
    /// </list>
    /// 둘 다 있어야 「위반 0건」이 「규칙을 지킨다」와 「검사가 아무것도 안 한다」에서 갈린다.
    ///
    /// <para>거리는 <b>선분-선분</b> 거리다. 교차하면 0이 나오므로 (나)가 구조적으로 성립한다 —
    /// 끝점-선분 거리로 바꾸는 순간 그 함정이 되살아나고, (나)가 그 회귀를 잡는다.</para>
    /// </summary>
    public sealed class CostumePropForbiddenZoneTests
    {
        private const string LogPrefix = "[프롭금지대]";

        /// <summary>금지대 하한을 획 폭의 몇 배로 잡는가(PR-4). 규칙 쪽 숫자라 여기 적는 것이 맞다.</summary>
        private const float MinGapInStrokeWidths = 2.00f;

        /// <summary>프롭 획 폭 W_P(H 배수) — 렌더러 소스에서 읽어 온다(<c>private const</c>라 참조 불가).</summary>
        private static float StrokeWidthInH()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Interaction", "CostumePropRenderer.cs");
            Assert.IsTrue(System.IO.File.Exists(path), $"{LogPrefix} 렌더러 소스를 못 찾았다: {path}");
            string src = System.IO.File.ReadAllText(path);
            const string key = "private const float StrokeWidthRatio = ";
            int i = src.IndexOf(key, System.StringComparison.Ordinal);
            Assert.Greater(i, 0, $"{LogPrefix} StrokeWidthRatio 선언을 못 찾았다 — 파서를 고쳐라. " +
                "못 찾은 채 통과시키면 이 감사가 빈 검사가 된다.");
            int start = i + key.Length;
            int end = src.IndexOf('f', start);
            Assert.IsTrue(float.TryParse(src.Substring(start, end - start).Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float w) && w > 0f,
                $"{LogPrefix} StrokeWidthRatio 값을 읽지 못했다.");
            return w;
        }

        // ==================== 기하 (선분-선분) ====================

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
            => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        private static bool OnSpan(Vector2 p, Vector2 q, Vector2 r)
            => Mathf.Min(p.x, r.x) - 1e-6f <= q.x && q.x <= Mathf.Max(p.x, r.x) + 1e-6f
            && Mathf.Min(p.y, r.y) - 1e-6f <= q.y && q.y <= Mathf.Max(p.y, r.y) + 1e-6f;

        /// <summary>두 선분이 만나는가. ★ <b>이 함수가 없으면 「가로지르는 쌍」이 위반으로 잡힌다.</b></summary>
        private static bool Intersects(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
        {
            float d1 = Cross(p1, q1, p2), d2 = Cross(p1, q1, q2);
            float d3 = Cross(p2, q2, p1), d4 = Cross(p2, q2, q1);
            if ((d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f)) return true;
            if (Mathf.Abs(d1) < 1e-6f && OnSpan(p1, p2, q1)) return true;
            if (Mathf.Abs(d2) < 1e-6f && OnSpan(p1, q2, q1)) return true;
            if (Mathf.Abs(d3) < 1e-6f && OnSpan(p2, p1, q2)) return true;
            if (Mathf.Abs(d4) < 1e-6f && OnSpan(p2, q1, q2)) return true;
            return false;
        }

        private static float PointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float l2 = d.sqrMagnitude;
            float t = l2 <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, d) / l2);
            return Vector2.Distance(p, a + t * d);
        }

        private static float SegmentDistance(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
        {
            if (Intersects(p1, q1, p2, q2)) return 0f;
            return Mathf.Min(
                Mathf.Min(PointToSegment(p1, p2, q2), PointToSegment(q1, p2, q2)),
                Mathf.Min(PointToSegment(p2, p1, q1), PointToSegment(q2, p1, q1)));
        }

        private static List<(Vector2, Vector2)> Segments(Vector3[] pts, bool loop)
        {
            var s = new List<(Vector2, Vector2)>();
            for (int i = 0; i + 1 < pts.Length; i++) s.Add((pts[i], pts[i + 1]));
            if (loop && pts.Length > 2) s.Add((pts[pts.Length - 1], pts[0]));
            return s;
        }

        private static float ShapeDistance(List<(Vector2, Vector2)> a, List<(Vector2, Vector2)> b)
        {
            float best = float.PositiveInfinity;
            foreach ((Vector2 a0, Vector2 a1) in a)
                foreach ((Vector2 b0, Vector2 b1) in b)
                    best = Mathf.Min(best, SegmentDistance(a0, a1, b0, b1));
            return best;
        }

        // ==================== 양성 대조 ====================

        [Test]
        public void 양성대조_가_금지대_한복판_쌍이_실제로_잡힌다()
        {
            float w = StrokeWidthInH();
            var a = new List<(Vector2, Vector2)> { (new Vector2(0f, 0f), new Vector2(1f, 0f)) };
            var b = new List<(Vector2, Vector2)> { (new Vector2(0f, w), new Vector2(1f, w)) };
            float d = ShapeDistance(a, b);

            Assert.Greater(d, 0f, $"{LogPrefix} 대조 쌍이 닿아 버렸다 — 금지대 표본이 아니다.");
            Assert.Less(d, MinGapInStrokeWidths * w,
                $"{LogPrefix} ★ 양성 대조 실패 — 금지대 한복판({d:F4} H)이 하한 " +
                $"{MinGapInStrokeWidths * w:F4} H 밖으로 잡힌다. 그러면 아래 「위반 0건」은 " +
                "검사가 아무것도 안 한다는 뜻일 수 있다.");
            Debug.Log($"{LogPrefix} ① 양성 대조(가) 통과 — d={d:F4} H, 하한 {MinGapInStrokeWidths * w:F4} H.");
        }

        [Test]
        public void 양성대조_나_가로지르는_쌍은_위반이_아니다()
        {
            var a = new List<(Vector2, Vector2)> { (new Vector2(0f, 0f), new Vector2(1f, 1f)) };
            var b = new List<(Vector2, Vector2)> { (new Vector2(0f, 1f), new Vector2(1f, 0f)) };
            Assert.AreEqual(0f, ShapeDistance(a, b), 1e-6f,
                $"{LogPrefix} ★ 십자로 가로지르는 쌍의 거리가 0이 아니다 — 거리 함수가 «교차»를 " +
                "못 보고 있다. 그 상태로 출하 에셋을 재면 <b>고칠 필요 없는 쌍을 위반으로 신고</b>한다" +
                "(design-motion이 첫 판에서 실제로 4건을 거짓 신고했다).");

            // T자 접합(끝점이 다른 선분 위)도 「닿음」이다.
            var t1 = new List<(Vector2, Vector2)> { (new Vector2(0f, 0f), new Vector2(1f, 0f)) };
            var t2 = new List<(Vector2, Vector2)> { (new Vector2(0.5f, 0f), new Vector2(0.5f, 1f)) };
            Assert.AreEqual(0f, ShapeDistance(t1, t2), 1e-6f, $"{LogPrefix} T자 접합이 0이 아니다.");
            Debug.Log($"{LogPrefix} ② 양성 대조(나) 통과 — 교차·T자 접합 모두 d=0.");
        }

        // ==================== 본 감사 ====================

        [Test]
        public void 출하_코스튬_프롭이_금지대를_지킨다()
        {
            float w = StrokeWidthInH();
            float minGap = MinGapInStrokeWidths * w;

            CostumeCatalog.ResetForTesting();
            IReadOnlyList<CostumeDescriptor> costumes = CostumeCatalog.Costumes;
            Assert.IsNotEmpty(costumes,
                $"{LogPrefix} 실린 코스튬이 0개다 — 이 감사가 아무것도 안 잰다. " +
                "에셋이 Resources/Items에 있는지 확인하라(CostumeCatalog.ResourceFolder).");

            int auditedGroups = 0, auditedPairs = 0, touching = 0;
            var violations = new List<string>();

            foreach (CostumeDescriptor costume in costumes)
            {
                // 기본 조형 + 단계별 조형을 각각 «한 화면에 함께 그려지는 집합»으로 본다.
                var groups = new List<(string, IReadOnlyList<AccessoryWornShapeData>)>
                {
                    ($"{costume.CostumeKey}/base", costume.PropShapes),
                };
                foreach (CostumeStageOverride ov in costume.StageShapes)
                    if (ov.shapes != null && ov.shapes.Length > 0)
                        groups.Add(($"{costume.CostumeKey}/stage{ov.stage}", ov.shapes));

                foreach ((string label, IReadOnlyList<AccessoryWornShapeData> shapes) in groups)
                {
                    if (shapes == null || shapes.Count < 2) continue;
                    auditedGroups++;

                    var built = new List<(string, List<(Vector2, Vector2)>)>();
                    foreach (AccessoryWornShapeData sh in shapes)
                    {
                        // H = 1 프레임이라 좌표가 곧 H 배수다.
                        Assert.IsTrue(AccessoryWornShapeReader.TryBuild(sh, AccessoryWornFrame.Unit,
                            false, out Vector3[] pts, out string err),
                            $"{LogPrefix} {label}의 '{sh.name}' 해석 실패: {err}");
                        built.Add((sh.name, Segments(pts, sh.loop)));
                    }

                    for (int i = 0; i < built.Count; i++)
                    {
                        for (int j = i + 1; j < built.Count; j++)
                        {
                            auditedPairs++;
                            float d = ShapeDistance(built[i].Item2, built[j].Item2);
                            if (d <= 0f) { touching++; continue; }              // 닿음 — 허용
                            if (d >= minGap - 1e-6f) continue;                  // 충분히 떨어짐 — 허용
                            violations.Add($"  {label}: {built[i].Item1} ↔ {built[j].Item1} " +
                                $"d={d:F4} H = {d / w:F2} W_P (하한 {MinGapInStrokeWidths:F2} W_P)");
                        }
                    }
                }
            }

            Assert.Greater(auditedPairs, 0,
                $"{LogPrefix} 잰 쌍이 0개다 — 조각이 실리지 않았다는 뜻이고, 「위반 0」은 무의미하다.");
            Assert.IsEmpty(violations,
                $"{LogPrefix} 금지대 위반 {violations.Count}건 — 두 선이 «붙지도 떨어지지도 않은» " +
                "자리에 있어 화면에서 한 줄로 뭉개지거나 서로를 흐린다.\n" +
                string.Join("\n", violations));

            Debug.Log($"{LogPrefix} ③ 통과 — 집합 {auditedGroups}개 · 쌍 {auditedPairs}개 " +
                      $"(닿음 {touching}) · 위반 0. 하한 {minGap:F4} H = {MinGapInStrokeWidths:F2} W_P.");
        }
    }
}
