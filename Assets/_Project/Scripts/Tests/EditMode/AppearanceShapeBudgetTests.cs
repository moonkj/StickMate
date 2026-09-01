using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ FX/PET 신규 4종(물방울·나뭇잎·풍선·달팽이) 도형의 <b>획 예산</b> 회귀 —
    /// 2026-09-01, docs/UX_FLOW.md 37-6 규칙 1/4/5.
    ///
    /// ============================================================================
    /// 왜 별도 파일인가
    /// ============================================================================
    /// 기존 <see cref="AccessoryStrokeBudgetTests"/>는 <b>착용 액세서리</b>(모자/안경/목/등/머리)를
    /// 대상으로 하고 그 카테고리들은 지금 다른 라운드가 재설계 중이다. FX/PET 도형은 소유 파일이
    /// 다르므로(Interaction/AppearanceShapeBuilder.cs) 검사도 여기서 따로 든다 — 두 라운드가 같은
    /// 파일을 다투지 않게 하는 것이 목적이다.
    ///
    /// ============================================================================
    /// 무엇을 잡는가
    /// ============================================================================
    /// 출하 배율 0.75에서 실제로 그려지는 획은 <b>0.344R</b>이다(화면상 2pt 하한 때문에 순수 비례보다
    /// 굵다). 도형 좌표를 "선 굵기 0"인 것처럼 설계하면 다음 세 가지가 조용히 일어난다:
    ///   · 획보다 짧은 선분 -> 화면에서 통째로 먹힌다.
    ///   · 지름이 3획 미만인 윤곽 -> 속이 안 보이는 <b>검은 덩어리</b>가 된다(방울이 점이 된다).
    ///   · 간격이 1.5획 미만인 두 선 -> 붙어서 하나로 읽힌다(껍데기 속 무늬가 사라진다).
    /// 그리고 규칙 4의 <b>부착</b>: 조각끼리 만나야 하는 자리는 좌표가 실제로 만나야 한다
    /// (풍선 매듭이 벌어지면 주머니가 끈에서 떨어져 날아간다).
    /// </summary>
    public sealed class AppearanceShapeBudgetTests
    {
        /// <summary>출하 배율에서 실제로 그려지는 획(머리 반경 배수) ≈ 0.344R.
        /// 값의 단일 소스는 액세서리 쪽과 같다 — 두 곳에 따로 적으면 언젠가 하나만 바뀐다.</summary>
        private static float W => AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;

        /// <summary>모든 도형을 머리 반경 1 기준으로 만든다(전부 R 배수라 이 스케일이 곧 "R 단위"다).</summary>
        private const float R = 1f;

        // ==================== 물방울 ====================

        [Test]
        public void 물방울은_가장_작을_때도_속이_보인다()
        {
            float minRadius = AppearanceShapeBuilder.BubbleMinRadiusInR * R;
            Assert.GreaterOrEqual(minRadius * 2f, W * 3f,
                $"가장 작은 물방울의 지름이 {(minRadius * 2f / W):F2}획입니다 — 3획 미만이면 링 안쪽이 " +
                "획에 먹혀 방울이 아니라 까만 점이 됩니다(37-6 규칙 1).");

            Assert.Less(AppearanceShapeBuilder.BubbleMaxRadiusInR, 1f,
                "가장 큰 물방울이 머리(1.0R)보다 큽니다 — 그러면 방울이 아니라 또 하나의 머리로 읽힙니다.");

            Vector3[] ring = AppearanceShapeBuilder.BubbleRing(minRadius, 12);
            Assert.AreEqual(12, ring.Length);
            for (int i = 0; i < ring.Length; i++)
            {
                Assert.AreEqual(minRadius, ring[i].magnitude, 1e-4f, $"{i}번 점이 원 위에 있지 않습니다.");
            }
        }

        // ==================== 나뭇잎 ====================

        [Test]
        public void 나뭇잎의_모든_선분이_획보다_길다()
        {
            float len = AppearanceShapeBuilder.LeafLengthInR * R;
            Vector3[] blade = AppearanceShapeBuilder.LeafBlade(len);
            Vector3[] stem = AppearanceShapeBuilder.LeafStem(len);

            float shortest = ShortestSegment(blade, closed: true);
            Assert.GreaterOrEqual(shortest, W,
                $"잎몸의 가장 짧은 선분이 {(shortest / W):F2}획입니다 — 1획 미만이면 그 변이 사라져 " +
                "잎이 뭉툭한 덩어리가 됩니다.");

            float stemLength = Vector3.Distance(stem[0], stem[1]);
            Assert.GreaterOrEqual(stemLength, W,
                $"잎자루가 {(stemLength / W):F2}획입니다 — 1획 미만이면 잎몸의 캡에 그대로 먹힙니다.");
        }

        [Test]
        public void 나뭇잎_잎자루는_잎몸에_정확히_붙어_있다()
        {
            float len = AppearanceShapeBuilder.LeafLengthInR * R;
            Vector3[] blade = AppearanceShapeBuilder.LeafBlade(len);
            Vector3[] stem = AppearanceShapeBuilder.LeafStem(len);

            float gap = float.MaxValue;
            for (int i = 0; i < blade.Length; i++) gap = Mathf.Min(gap, Vector3.Distance(blade[i], stem[0]));

            Assert.AreEqual(0f, gap, 1e-5f,
                $"잎자루 뿌리가 잎몸에서 {gap:F5}R 떨어져 있습니다 — 37-6 규칙 4가 금지한 '떠 있는 조각'입니다.");
        }

        // ==================== 풍선 ====================

        [Test]
        public void 풍선_매듭에서_끈과_주머니가_정확히_만난다()
        {
            Vector3[] str = AppearanceShapeBuilder.BalloonString(R);
            Vector3[] body = AppearanceShapeBuilder.BalloonBody(R);

            Assert.AreEqual(Vector3.zero, str[0], "풍선 끈의 원점이 (0,0)이 아닙니다 — " +
                "회전 중심이 '묶인 자리'라는 전제가 깨지면 흔들 때 끈이 몸을 뚫습니다.");

            float knotGap = Vector3.Distance(str[str.Length - 1], body[0]);
            Assert.AreEqual(0f, knotGap, 1e-5f,
                $"끈 끝과 주머니 매듭이 {knotGap:F5}R 벌어져 있습니다 — 주머니가 끈에서 떨어져 뜹니다.");
        }

        [Test]
        public void 풍선의_모든_선분이_획보다_길고_주머니는_속이_보인다()
        {
            Vector3[] str = AppearanceShapeBuilder.BalloonString(R);
            Vector3[] body = AppearanceShapeBuilder.BalloonBody(R);

            float shortestString = ShortestSegment(str, closed: false);
            Assert.GreaterOrEqual(shortestString, W,
                $"풍선 끈의 가장 짧은 선분이 {(shortestString / W):F2}획입니다.");

            float shortestBody = ShortestSegment(body, closed: true);
            Assert.GreaterOrEqual(shortestBody, W,
                $"주머니 외곽의 가장 짧은 선분이 {(shortestBody / W):F2}획입니다.");

            float diameter = AppearanceShapeBuilder.BalloonRadiusInR * 2f * R;
            Assert.GreaterOrEqual(diameter, W * 3f,
                $"주머니 지름이 {(diameter / W):F2}획입니다 — 3획 미만이면 풍선이 통짜 점이 됩니다.");
        }

        // ==================== 달팽이 ====================

        [Test]
        public void 달팽이_발의_모든_선분이_획보다_길다()
        {
            Vector3[] foot = AppearanceShapeBuilder.SnailFoot(R, 1f);
            float shortest = ShortestSegment(foot, closed: false);
            Assert.GreaterOrEqual(shortest, W,
                $"달팽이 발/더듬이의 가장 짧은 선분이 {(shortest / W):F2}획입니다 — " +
                "1획 미만인 마디는 화면에서 통째로 사라집니다.");
        }

        [Test]
        public void 달팽이_껍데기의_바깥링과_속점이_붙어_보이지_않는다()
        {
            float outer = AppearanceShapeBuilder.SnailShellRadiusRatio * R;
            float core = AppearanceShapeBuilder.SnailShellCoreRatio * R;
            float gap = outer - core;

            Assert.GreaterOrEqual(gap, W * 1.5f,
                $"껍데기 바깥 링과 속 점의 간격이 {(gap / W):F2}획입니다 — 1.5획 미만이면 두 선이 " +
                "붙어 한 덩어리로 읽혀 '이 아이템을 구별해 주는 한 부분'(37-6 규칙 3-2)이 사라집니다.");
        }

        /// <summary>
        /// 껍데기 아랫변과 발 선이 <b>획 반폭 안에서</b> 만나는가. 판정 기준이 좌표가 아니라 획 반폭인
        /// 이유: 두 선은 각각 두께 W로 그려지므로 중심선 거리가 0.5 W 안이면 잉크가 실제로 겹친다.
        /// 위로 벗어나면 껍데기가 <b>공중에 뜨고</b>(규칙 4 위반), 아래로 벗어나면 <b>땅에 잠긴다</b>.
        /// </summary>
        [Test]
        public void 달팽이_껍데기는_발_선에_닿아_있고_땅에_잠기지_않는다()
        {
            float centerY = AppearanceShapeBuilder.SnailShellCenterYRatio * R;
            float radius = AppearanceShapeBuilder.SnailShellRadiusRatio * R;
            float centerlineGap = centerY - radius;   // + 면 떠 있음, − 면 발 선 아래로 파고듦
            float halfStroke = W * 0.5f;

            Assert.LessOrEqual(centerlineGap, halfStroke,
                $"껍데기 아랫변이 발 선보다 {centerlineGap:F4}R 위에 있습니다(획 반폭 {halfStroke:F4}R) — " +
                "두 획의 잉크가 만나지 않아 껍데기가 공중에 뜬 원으로 보입니다(37-6 규칙 4).");
            Assert.GreaterOrEqual(centerlineGap, -halfStroke,
                $"껍데기 아랫변이 발 선보다 {-centerlineGap:F4}R 아래입니다(획 반폭 {halfStroke:F4}R) — " +
                "껍데기가 지면 밑으로 잠겨 그려집니다.");
        }

        [Test]
        public void 달팽이는_좌우_반전이_x만_뒤집는다()
        {
            Vector3[] right = AppearanceShapeBuilder.SnailFoot(R, 1f);
            Vector3[] left = AppearanceShapeBuilder.SnailFoot(R, -1f);
            Assert.AreEqual(right.Length, left.Length);

            for (int i = 0; i < right.Length; i++)
            {
                Assert.AreEqual(-right[i].x, left[i].x, 1e-5f, $"{i}번 점의 x가 대칭이 아닙니다.");
                Assert.AreEqual(right[i].y, left[i].y, 1e-5f,
                    $"{i}번 점의 y가 좌우 반전에서 바뀌었습니다 — 뒤집으면 안 되는 축입니다.");
            }

            Vector3[] shellR = AppearanceShapeBuilder.SnailShell(R, 1f, 12);
            Vector3[] shellL = AppearanceShapeBuilder.SnailShell(R, -1f, 12);
            Assert.AreEqual(-Center(shellR).x, Center(shellL).x, 1e-4f,
                "껍데기 중심이 좌우 반전을 따라가지 않습니다 — 발만 뒤집히고 껍데기는 그대로 남습니다.");
        }

        // ==================== 도구 ====================

        private static float ShortestSegment(Vector3[] pts, bool closed)
        {
            float shortest = float.MaxValue;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                shortest = Mathf.Min(shortest, Vector3.Distance(pts[i], pts[i + 1]));
            }
            if (closed && pts.Length > 2)
            {
                shortest = Mathf.Min(shortest, Vector3.Distance(pts[pts.Length - 1], pts[0]));
            }
            return shortest;
        }

        private static Vector3 Center(Vector3[] pts)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < pts.Length; i++) sum += pts[i];
            return sum / Mathf.Max(1, pts.Length);
        }
    }
}
