using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 실루엣 계측 자(尺) — 2026-09-01. 이 어셈블리의 여러 검사가 <b>같은 자</b>를 쓰게 하려고 뽑았다.
    ///
    /// <para>왜 새로 만들었나: 옛 지표(<c>AccessoryStrokeBudgetTests.RadiusProfile</c>)는 도형의
    /// <b>정점만</b> 훑고 <b>상반구(y ≥ 0)만</b> 봤다. 그래서 두 가지를 동시에 틀렸다.</para>
    /// <list type="number">
    ///   <item>긴 변 하나가 여러 각도 구간을 지나가도 <b>양 끝점만</b> 기록됐다 — 가로지르는 선은 안 보인다.</item>
    ///   <item>잉크가 없는 구간을 <b>반경 0</b>으로 세는 바람에, 한쪽에만 정점이 놓인 구간이
    ///         "1.3R 차이"로 계산돼 <b>실제로는 같은 그림인 쌍이 3.77획으로 부풀었다</b>
    ///         (바가지머리 vs 단정한머리의 실측 차이는 0.58획이었다).</item>
    /// </list>
    /// <para>그래서 여기서는 <b>변을 조밀 표본</b>하고 <b>360도 전부</b>를 본다. 빈 구간을 0으로 세는 것은
    /// 그대로 두되(한쪽에만 잉크가 있는 각도는 <b>실제로</b> 눈에 보이는 차이다), 이제 그 판정이
    /// "정점이 그 구간에 없다"가 아니라 "그 방향으로 잉크가 뻗지 않는다"를 뜻한다.</para>
    ///
    /// <para>기준 배율은 언제나 출하 기본값(<see cref="AccessoryShapeBuilder.ShippingCharacterScale"/> = 0.75)이고,
    /// 값은 전부 머리 반경 R 배수다 — 37-6 규칙 1이 검산 배율을 그렇게 고정했다.</para>
    /// </summary>
    internal static class AccessorySilhouetteMetrics
    {
        /// <summary>각도 구간 폭(도). 5도 × 72 = 360도.</summary>
        internal const float BucketDegrees = 5f;

        internal const int BucketCount = 72;

        /// <summary>변 하나를 몇 조각으로 나눠 표본할지. 이 세트의 가장 긴 변(약 2.2R)에서도
        /// 표본 간격이 0.035R(획의 1/10)이라 각도 구간(5도)보다 촘촘하다.</summary>
        private const int SamplesPerEdge = 64;

        /// <summary>배율 0.75에서의 획 예산(R 배수). 판정 문턱은 전부 이 값의 배수로 쓴다.</summary>
        internal static float StrokeInR => AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;

        /// <summary>배율 1.0 프리팹 실측 리그. 예산은 배율 무관한 R 배수라 리그 배율은 1.0으로 둔다.</summary>
        internal static AccessoryShapeBuilder.Rig Rig(float facing = 1f)
        {
            const float H = StickConfig.BaselineCharacterTotalHeight;
            const float R = AccessoryShapeBuilder.BaselineHeadVisualRadius;
            return new AccessoryShapeBuilder.Rig(R, H - R,
                AccessoryShapeBuilder.BaselineShoulderLocalY,
                AccessoryShapeBuilder.BaselineHipLocalY, facing);
        }

        /// <summary>그 카테고리의 도형이 <b>매달린 기준점</b>의 로컬 y. 반경을 여기서 잰다 —
        /// 목걸이를 머리 중심에서 재면 모든 목걸이가 "머리 아래 먼 곳"으로 뭉쳐 차이가 묻힌다.</summary>
        internal static float AnchorLocalY(in AccessoryShapeBuilder.Rig rig, EquipmentSlot slot)
            => slot == EquipmentSlot.Neck || slot == EquipmentSlot.Shoulders
                ? AccessoryShapeBuilder.NeckLocalY(rig)
                : rig.HeadCenterY;

        internal static List<AccessoryShapeBuilder.Shape> Build(
            in AccessoryShapeBuilder.Rig rig, EquipmentSlot slot, int item)
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, item, rig);
            return sink;
        }

        internal static float[] Profile(in AccessoryShapeBuilder.Rig rig, EquipmentSlot slot, int item)
            => ProfileOf(rig, Build(rig, slot, item), AnchorLocalY(rig, slot));

        /// <summary>
        /// 각도 구간별 <b>가장 멀리 뻗은 잉크</b>의 반경(R 배수). "그 방향으로 실루엣이 어디까지 가는가".
        /// </summary>
        internal static float[] ProfileOf(in AccessoryShapeBuilder.Rig rig,
            IList<AccessoryShapeBuilder.Shape> shapes, float centerY)
        {
            var profile = new float[BucketCount];
            for (int s = 0; s < shapes.Count; s++)
            {
                Vector3[] pts = shapes[s].Points;
                if (pts == null || pts.Length < 2) continue;

                int edges = shapes[s].Loop ? pts.Length : pts.Length - 1;
                for (int e = 0; e < edges; e++)
                {
                    Vector3 a = pts[e];
                    Vector3 b = pts[(e + 1) % pts.Length];
                    for (int k = 0; k <= SamplesPerEdge; k++)
                    {
                        float t = k / (float)SamplesPerEdge;
                        var v = new Vector2(Mathf.Lerp(a.x, b.x, t),
                            Mathf.Lerp(a.y, b.y, t) - centerY);
                        float deg = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                        if (deg < 0f) deg += 360f;
                        int bucket = Mathf.Clamp((int)(deg / BucketDegrees), 0, BucketCount - 1);
                        profile[bucket] = Mathf.Max(profile[bucket], v.magnitude / rig.HeadRadius);
                    }
                }
            }
            return profile;
        }

        /// <summary>두 프로파일이 가장 크게 벌어지는 값(R 배수).</summary>
        internal static float MaxRadiusDelta(float[] a, float[] b)
        {
            float max = 0f;
            for (int i = 0; i < a.Length; i++) max = Mathf.Max(max, Mathf.Abs(a[i] - b[i]));
            return max;
        }

        /// <summary>선(<paramref name="line"/>) 위의 모든 점에서 <paramref name="shape"/>의 변까지의
        /// 거리 중 <b>가장 먼 값</b>(R 배수). 0이면 두 선이 완전히 겹친다(= 화면에서 선 하나).</summary>
        internal static float MaxGapToShape(in AccessoryShapeBuilder.Rig rig,
            Vector3[] line, in AccessoryShapeBuilder.Shape shape)
        {
            float worst = 0f;
            for (int i = 0; i < line.Length - 1; i++)
            {
                for (int k = 0; k <= SamplesPerEdge; k++)
                {
                    float t = k / (float)SamplesPerEdge;
                    var p = new Vector2(Mathf.Lerp(line[i].x, line[i + 1].x, t),
                        Mathf.Lerp(line[i].y, line[i + 1].y, t));

                    float best = float.MaxValue;
                    Vector3[] pts = shape.Points;
                    int edges = shape.Loop ? pts.Length : pts.Length - 1;
                    for (int e = 0; e < edges; e++)
                    {
                        best = Mathf.Min(best, PointToSegment(p, pts[e], pts[(e + 1) % pts.Length]));
                    }
                    worst = Mathf.Max(worst, best);
                }
            }
            return worst / rig.HeadRadius;
        }

        private static float PointToSegment(Vector2 p, Vector3 a, Vector3 b)
        {
            var ab = new Vector2(b.x - a.x, b.y - a.y);
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-12f) return Vector2.Distance(p, new Vector2(a.x, a.y));
            float t = Mathf.Clamp01(Vector2.Dot(p - new Vector2(a.x, a.y), ab) / len2);
            return Vector2.Distance(p, new Vector2(a.x, a.y) + ab * t);
        }

        /// <summary>도형의 잉크 사각형(R 배수). 가로/세로를 따로 돌려준다 — 종횡비를 재려면 둘이 필요하다.</summary>
        internal static Vector2 ExtentInR(in AccessoryShapeBuilder.Rig rig, Vector3[] pts)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < pts.Length; i++)
            {
                min = Vector2.Min(min, new Vector2(pts[i].x, pts[i].y));
                max = Vector2.Max(max, new Vector2(pts[i].x, pts[i].y));
            }
            return (max - min) / rig.HeadRadius;
        }

        internal static AccessoryShapeBuilder.Shape Find(
            IList<AccessoryShapeBuilder.Shape> shapes, string name)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].Name == name) return shapes[i];
            }
            throw new System.InvalidOperationException(
                $"도형 '{name}'을 찾지 못했습니다 — 이름이 바뀌었다면 이 검사도 함께 갱신해야 합니다.");
        }
    }
}
