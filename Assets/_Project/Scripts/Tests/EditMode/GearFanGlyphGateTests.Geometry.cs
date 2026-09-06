using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 부채꼴 글리프 계측 리그 — <b>기하 연산</b>(거리 · 덩어리 · 래스터).
    /// 되읽기는 <c>GearFanGlyphGateTests.Rig.cs</c>, 판정은 <c>GearFanGlyphGateTests.cs</c>.
    ///
    /// <para><b>거리를 해석적으로 잰다</b>(래스터 거리변환이 아니라). 이유는 FG-3의 문턱값이
    /// 정확히 <b>3.00pt</b>이고 현행 카탈로그의 최악 간극이 <b>정확히 3.00pt</b>이기 때문이다 —
    /// 격자 양자화 0.04pt짜리 자로 재면 「통과」와 「미달」이 자의 눈금 안에서 뒤집힌다.</para>
    /// </summary>
    public sealed partial class GearFanGlyphGateTests
    {
        private const int SegmentSamples = 1024;   // 선분 ↔ 원호 거리 표본
        private const int ArcSamples = 2048;       // 원호 ↔ 원호 · 반경 최대값 표본

        private static Vector2 Polar(float degrees, float radius)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r) * radius, Mathf.Sin(r) * radius);
        }

        private static float PointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-12f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// 점에서 <b>잉크 영역</b>까지의 거리(음수 = 잉크 안).
        ///
        /// <para>★★ <b>원호의 잘린 끝은 둥근 캡이 아니라 「평평한 반경 방향 변」이다.</b>
        /// <see cref="Image.Type.Filled"/>/<see cref="Image.FillMethod.Radial360"/>은 <b>중심을 지나는
        /// 직선</b>으로 스프라이트를 자르기 때문이다. 이것을 「중심선 ⊕ 원판」으로 모형화하면 잘린
        /// 자리에 반지름 W/2짜리 <b>없는 잉크</b>가 생겨 간극을 실제보다 좁게 본다 —
        /// 전원 기호의 세로획↔틈이 정확히 그 자리이고, 그 오차가 <b>0.49pt</b>다
        /// (틀린 모형 3.15 vs 참값 3.635 = 설계 문서 §6-⑤의 닫힌 식 <c>(d/2−W)·sin(틈/2)−W/2</c>).
        /// 그 방향의 오차는 «거짓 빨강»을 낳으므로 조용하지 않지만, 자(尺)가 틀린 채로 초록이 뜨는
        /// 날을 만들지 않기 위해 여기서 정확히 잰다.</para>
        /// </summary>
        private static float InkDistance(Vector2 p, in InkCore c)
        {
            if (!c.IsArc) return PointToSegment(p, c.A, c.B) - c.HalfThickness;

            Vector2 v = p - c.Center;
            float d = v.magnitude;
            float span = c.EndDeg - c.StartDeg;
            if (span >= 359.999f) return Mathf.Abs(d - c.MedialRadius) - c.HalfThickness;

            float theta = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (Mathf.Repeat(theta - c.StartDeg, 360f) <= span)
                return Mathf.Abs(d - c.MedialRadius) - c.HalfThickness;

            float inner = c.MedialRadius - c.HalfThickness, outer = c.MedialRadius + c.HalfThickness;
            float e0 = PointToSegment(p, c.Center + Polar(c.StartDeg, inner), c.Center + Polar(c.StartDeg, outer));
            float e1 = PointToSegment(p, c.Center + Polar(c.EndDeg, inner), c.Center + Polar(c.EndDeg, outer));
            return Mathf.Min(e0, e1);
        }

        private static bool SegmentsCross(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
        {
            if ((q1 - p1).sqrMagnitude < 1e-12f || (q2 - p2).sqrMagnitude < 1e-12f) return false;
            float d1 = Cross(q2 - p2, p1 - p2), d2 = Cross(q2 - p2, q1 - p2);
            float d3 = Cross(q1 - p1, p2 - p1), d4 = Cross(q1 - p1, q2 - p1);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));

            float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        }

        private static float SegmentToSegment(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
        {
            if (SegmentsCross(p1, q1, p2, q2)) return 0f;
            return Mathf.Min(
                Mathf.Min(PointToSegment(p1, p2, q2), PointToSegment(q1, p2, q2)),
                Mathf.Min(PointToSegment(p2, p1, q1), PointToSegment(q2, p1, q1)));
        }

        /// <summary>두 잉크 <b>영역</b> 사이의 간극. 음수면 겹친다(= 용접된 한 덩어리).
        /// <para>캡슐 ↔ 캡슐만 닫힌 식이고, 원호가 끼면 한쪽 중심선을 조밀 표본해
        /// <see cref="InkDistance"/>로 잰다. 표본 간격이 0.01pt 아래라 최솟값 근방의 2차 오차는
        /// 1e-5pt 수준이다(FG-3 문턱 3.00pt에 견줘 무의미).</para></summary>
        internal static float CoreGap(in InkCore a, in InkCore b)
        {
            if (!a.IsArc && !b.IsArc)
                return SegmentToSegment(a.A, a.B, b.A, b.B) - a.HalfThickness - b.HalfThickness;

            if (a.IsArc && b.IsArc)
            {
                // ★ 이 갈래는 현행 카탈로그에 <b>존재하지 않는다</b>(글리프마다 링이 최대 하나).
                //   원호 A의 잘린 끝만 「중심선 ⊕ 원판」으로 근사되므로 결과가 <b>보수적</b>이다.
                float best = float.MaxValue, spanA = a.EndDeg - a.StartDeg;
                for (int i = 0; i <= ArcSamples; i++)
                {
                    Vector2 p = a.Center + Polar(a.StartDeg + spanA * i / ArcSamples, a.MedialRadius);
                    best = Mathf.Min(best, InkDistance(p, b));
                }
                return best - a.HalfThickness;
            }

            InkCore arc = a.IsArc ? a : b;
            InkCore seg = a.IsArc ? b : a;
            float worst = float.MaxValue;
            for (int i = 0; i <= SegmentSamples; i++)
            {
                worst = Mathf.Min(worst,
                    InkDistance(Vector2.Lerp(seg.A, seg.B, i / (float)SegmentSamples), arc));
            }
            return worst - seg.HalfThickness;
        }

        private static bool CoreContains(in InkCore c, Vector2 p) => InkDistance(p, c) <= 0f;

        /// <summary>원점(심볼 상자 중심)에서 이 코어의 잉크가 가장 멀리 뻗는 거리 — FG-1의 자.
        /// <para>원호는 <b>바깥 반지름</b>을 각도 구간 안에서 훑는다. 부채꼴 영역의 가장 먼 점은
        /// 언제나 바깥 호 위(끝의 모서리 포함)에 있다.</para></summary>
        private static float CoreMaxRadius(in InkCore c)
        {
            if (!c.IsArc) return Mathf.Max(c.A.magnitude, c.B.magnitude) + c.HalfThickness;

            float outer = c.MedialRadius + c.HalfThickness;
            float best = 0f, span = c.EndDeg - c.StartDeg;
            for (int i = 0; i <= ArcSamples; i++)
            {
                best = Mathf.Max(best, (c.Center + Polar(c.StartDeg + span * i / ArcSamples, outer)).magnitude);
            }
            return best;
        }

        // ================================================================================
        // 덩어리(blob) — FG-3
        // ================================================================================

        /// <summary>「겹쳐서 한 덩어리」로 묶는다. <b>맞닿기만 한 쌍(간극 0)은 묶지 않는다</b> —
        /// 옛 스틱맨의 머리↔몸통이 정확히 <b>접선</b>이라 「한 덩어리」로도 「떨어진 둘」로도
        /// 판정되지 않는 애매한 자리였고, 이번 재조형이 그 애매함을 없앤 것 자체가 잠글 대상이다.</summary>
        internal static int[] BlobsOf(IReadOnlyList<InkCore> cores)
        {
            var parent = new int[cores.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }

            for (int i = 0; i < cores.Count; i++)
            {
                for (int j = i + 1; j < cores.Count; j++)
                {
                    if (CoreGap(cores[i], cores[j]) >= -WeldTolerance) continue;
                    int ri = Find(i), rj = Find(j);
                    if (ri != rj) parent[ri] = rj;
                }
            }

            var labels = new int[cores.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = Find(i);
            return labels;
        }

        // ================================================================================
        // 래스터 — FG-6(실루엣) · FG-8(광학 크기)만 여기서 잰다
        // ================================================================================

        private const int RasterCellsPerPoint = 24;
        private const float RasterWindowPoints = 34f;
        private static readonly int RasterN = Mathf.RoundToInt(RasterWindowPoints * RasterCellsPerPoint);

        private static float CellCenter(int index)
            => (index + 0.5f) / RasterCellsPerPoint - RasterWindowPoints * 0.5f;

        private static int CellIndex(float points)
            => Mathf.FloorToInt((points + RasterWindowPoints * 0.5f) * RasterCellsPerPoint);

        internal static bool[] RasterOf(IReadOnlyList<InkCore> cores)
        {
            var mask = new bool[RasterN * RasterN];
            foreach (InkCore c in cores)
            {
                float reach = c.IsArc ? c.MedialRadius + c.HalfThickness : c.HalfThickness;
                Vector2 lo, hi;
                if (c.IsArc)
                {
                    lo = new Vector2(c.Center.x - reach, c.Center.y - reach);
                    hi = new Vector2(c.Center.x + reach, c.Center.y + reach);
                }
                else
                {
                    lo = new Vector2(Mathf.Min(c.A.x, c.B.x) - reach, Mathf.Min(c.A.y, c.B.y) - reach);
                    hi = new Vector2(Mathf.Max(c.A.x, c.B.x) + reach, Mathf.Max(c.A.y, c.B.y) + reach);
                }

                int i0 = Mathf.Max(0, CellIndex(lo.x)), i1 = Mathf.Min(RasterN - 1, CellIndex(hi.x) + 1);
                int j0 = Mathf.Max(0, CellIndex(lo.y)), j1 = Mathf.Min(RasterN - 1, CellIndex(hi.y) + 1);
                Assert.IsTrue(i0 <= i1 && j0 <= j1,
                    $"{LogPrefix} 조각 '{c.Piece}'이 래스터 창(±{RasterWindowPoints * 0.5f:F0}pt) 밖에 있습니다 — " +
                    "창을 넓히지 않으면 아래 실루엣/잉크 숫자는 무효입니다.");

                for (int j = j0; j <= j1; j++)
                {
                    float y = CellCenter(j);
                    int row = j * RasterN;
                    for (int i = i0; i <= i1; i++)
                    {
                        if (mask[row + i]) continue;
                        if (CoreContains(c, new Vector2(CellCenter(i), y))) mask[row + i] = true;
                    }
                }
            }
            return mask;
        }

        private static void MeasureRaster(Glyph g)
        {
            g.Mask = RasterOf(g.Cores);

            int cells = 0, minI = int.MaxValue, maxI = int.MinValue, minJ = int.MaxValue, maxJ = int.MinValue;
            for (int j = 0; j < RasterN; j++)
            {
                int row = j * RasterN;
                for (int i = 0; i < RasterN; i++)
                {
                    if (!g.Mask[row + i]) continue;
                    cells++;
                    if (i < minI) minI = i;
                    if (i > maxI) maxI = i;
                    if (j < minJ) minJ = j;
                    if (j > maxJ) maxJ = j;
                }
            }

            Assert.Greater(cells, 0, $"{LogPrefix} {g}의 잉크가 0셀입니다 — 되읽기가 아무것도 못 봤습니다.");

            float cell = 1f / RasterCellsPerPoint;
            g.InkCells = cells;
            g.InkAreaPoints = cells * cell * cell;
            g.InkWidth = (maxI - minI) * cell;
            g.InkHeight = (maxJ - minJ) * cell;
            g.InkDiagonal = Mathf.Sqrt(g.InkWidth * g.InkWidth + g.InkHeight * g.InkHeight);
            g.InkPercent = g.InkAreaPoints / (SymbolBoxPoints * SymbolBoxPoints) * 100f;

            g.RMax = 0f;
            foreach (InkCore c in g.Cores) g.RMax = Mathf.Max(g.RMax, CoreMaxRadius(c));
        }

        /// <summary>쌍별 실루엣 지표 — FG-6. 고유 잉크는 <b>둘 중 작은 쪽</b>이다
        /// (한쪽이 다른 쪽에 삼켜지는 것이 문제이므로).</summary>
        internal static void Silhouette(bool[] a, bool[] b, out float iou, out float uniqueMin)
        {
            int inter = 0, onlyA = 0, onlyB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] && b[i]) inter++;
                else if (a[i]) onlyA++;
                else if (b[i]) onlyB++;
            }
            int union = inter + onlyA + onlyB;
            iou = union == 0 ? 0f : inter / (float)union;
            int areaA = inter + onlyA, areaB = inter + onlyB;
            uniqueMin = Mathf.Min(areaA == 0 ? 0f : onlyA / (float)areaA,
                                  areaB == 0 ? 0f : onlyB / (float)areaB);
        }

        /// <summary>★ FG-4의 「100 % 가려진 조각 금지」 — <b>Image 단위</b>로 센다.
        /// 옛 체크리스트의 <c>Strike</c>가 정확히 이것이었다: <c>Line2</c> 위에 같은 자리·같은 색으로
        /// 그려져 <b>화면에 존재하지 않는 Image 한 개</b>였다.
        /// <para>이 함수는 <b>부재 단언</b>의 심장이라 반드시 <b>존재 대조</b>와 짝지어 쓴다 —
        /// 같은 함수에 고의로 겹친 조각을 먹여 잡히는지 확인한다.</para></summary>
        internal static List<string> FullyHiddenImages(IReadOnlyList<InkCore> cores)
        {
            var hidden = new List<string>();
            for (int i = 0; i < cores.Count; i++)
            {
                InkCore c = cores[i];
                float reach = c.IsArc ? c.MedialRadius + c.HalfThickness : c.HalfThickness;
                Vector2 lo = c.IsArc
                    ? new Vector2(c.Center.x - reach, c.Center.y - reach)
                    : new Vector2(Mathf.Min(c.A.x, c.B.x) - reach, Mathf.Min(c.A.y, c.B.y) - reach);
                Vector2 hi = c.IsArc
                    ? new Vector2(c.Center.x + reach, c.Center.y + reach)
                    : new Vector2(Mathf.Max(c.A.x, c.B.x) + reach, Mathf.Max(c.A.y, c.B.y) + reach);

                int i0 = Mathf.Max(0, CellIndex(lo.x)), i1 = Mathf.Min(RasterN - 1, CellIndex(hi.x) + 1);
                int j0 = Mathf.Max(0, CellIndex(lo.y)), j1 = Mathf.Min(RasterN - 1, CellIndex(hi.y) + 1);

                bool anyOwn = false, anyExposed = false;
                for (int j = j0; j <= j1 && !anyExposed; j++)
                {
                    float y = CellCenter(j);
                    for (int k = i0; k <= i1 && !anyExposed; k++)
                    {
                        var p = new Vector2(CellCenter(k), y);
                        if (!CoreContains(c, p)) continue;
                        anyOwn = true;

                        bool covered = false;
                        for (int other = 0; other < cores.Count && !covered; other++)
                        {
                            if (other == i) continue;
                            covered = CoreContains(cores[other], p);
                        }
                        if (!covered) anyExposed = true;
                    }
                }

                if (anyOwn && !anyExposed) hidden.Add($"{cores[i].Piece}#{i}");
            }
            return hidden;
        }
    }
}
