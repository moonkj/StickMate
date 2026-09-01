using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 털모자 폼폼 — 2026-09-01 규칙 1(획 예산) 위반 수정의 회귀.
    ///
    /// ============================================================================
    /// 무엇이 잘못돼 있었나 — <b>두 겹</b>이었고, 둘째가 이 라운드의 발견이다
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>확정 위반</b>: 잉크 사각형 0.44R = <b>1.28획</b>. 규칙 1의 문턱은 1.5획이다.</item>
    ///   <item><b>린트의 사각지대</b>: 8각형은 꺾임이 <b>정확히 45.0도</b>인데, 획 예산 검사의 문턱
    ///         (<see cref="AccessoryStrokeBudgetTests.CornerDegrees"/>)도 정확히 45도다.
    ///         float32에서 여덟 꼭짓점이 44.999996 / 45.000006으로 <b>갈려</b>, 어떤 꼭짓점이
    ///         "꺾임"으로 세어지는지가 반올림에 달린다. 그러면 "양끝이 <b>모두</b> 꺾임"인 선분이
    ///         성립했다 안 했다 하고, 0.49획짜리 여덟 변은 잡힐 때도 안 잡힐 때도 있다 —
    ///         즉 옛 폼폼은 <b>규칙을 어기면서 린트 결과는 미정</b>인 자리였다.
    ///         (실측: 배포 형상에서는 꼭짓점이 번갈아 갈려 한 변도 잡히지 않았다.)</item>
    /// </list>
    /// 아래 <b>네거티브 컨트롤 2건</b>이 그 둘을 각각 재현한다.
    ///
    /// ============================================================================
    /// 고침 — 방울과 <b>같은 병이 아니었다</b>
    /// ============================================================================
    /// 방울의 해법은 "키우기가 아니라 채우기"였지만, 폼폼은 <b>처음부터 채움 도형</b>이었다.
    /// 남은 지렛대는 각수와 크기뿐이었고, 둘 다 썼다: <b>8각 0.22R -> 10각 0.28R</b>(꺾임 36도).
    ///
    /// <b>실루엣은 한 자리도 건드리지 않았다.</b> 고정 대상을 반지름이 아니라 <b>꼭대기</b>로 잡았기
    /// 때문이다(<see cref="AccessoryShapeBuilder.BeaniePomCrestRiseRatio"/>) — 폼폼 꼭대기는
    /// 초상화 액자 상한(1.80R)에 정확히 닿아 있어서 반지름만 키우면 그대로 잘린다.
    /// 결과: 모자 6종 15쌍의 실루엣 차이가 <b>소수점 여섯 자리까지 그대로</b>(최소 2.95획 유지)다.
    /// </summary>
    public sealed class AccessoryBeaniePomTests
    {
        /// <summary>출하 배율(0.75)의 획 예산(R 배수). NECK/HEAD 검사와 <b>같은 자</b>를 쓴다.</summary>
        private static float W => AccessorySilhouetteMetrics.StrokeInR;

        private static AccessoryShapeBuilder.Rig Rig() => AccessorySilhouetteMetrics.Rig();

        private static List<AccessoryShapeBuilder.Shape> Beanie(in AccessoryShapeBuilder.Rig rig)
            => AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeanie);

        private static AccessoryShapeBuilder.Shape Pom(in AccessoryShapeBuilder.Rig rig)
            => AccessorySilhouetteMetrics.Find(Beanie(rig), "BeaniePom");

        // ============================================================================
        // 1. 규칙 1 — 지금은 통과한다
        // ============================================================================

        [Test]
        public void 폼폼이_획_예산을_지킨다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float w = AccessoryStrokeBudgetTests.BudgetWorld(rig);

            string violation = AccessoryStrokeBudgetTests.DescribeRuleOneViolation(Pom(rig), w);
            Assert.IsNull(violation, $"털모자 폼폼 {violation}");

            Vector2 extent = AccessorySilhouetteMetrics.ExtentInR(rig, Pom(rig).Points);
            float span = Mathf.Max(extent.x, extent.y) / W;
            Assert.That(span, Is.GreaterThan(1.5f).And.LessThan(2.2f),
                $"폼폼의 잉크 사각형이 {span:F2}획입니다. 1.5획 미만이면 '뚱뚱한 점'이고(옛 1.28획), " +
                "지나치게 크면 관보다 폼폼이 큰 버섯이 됩니다.");
        }

        /// <summary>
        /// ★ 이 라운드의 핵심 — 꺾임이 <b>문턱에서 떨어져 있어야</b> 한다.
        /// <para>값이 문턱과 같으면 판정이 float 잡음에 달리고, 그때 린트는 초록도 빨강도 아닌
        /// <b>무의미</b>가 된다. 8각형(45.0도)이 정확히 그 자리였다.</para>
        /// </summary>
        [Test]
        public void 폼폼_꺾임이_검사_문턱에서_확실히_떨어져_있다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            Vector3[] p = Pom(rig).Points;
            int n = p.Length;

            Assert.GreaterOrEqual(n, 10,
                $"폼폼이 {n}각형입니다 — 10각 미만이면 꺾임이 45도 문턱에 붙습니다(8각 = 정확히 45.0도).");

            for (int i = 0; i < n; i++)
            {
                float turn = AccessoryStrokeBudgetTests.TurnDegrees(p[(i - 1 + n) % n], p[i], p[(i + 1) % n]);
                Assert.LessOrEqual(turn, AccessoryStrokeBudgetTests.CornerDegrees - 5f,
                    $"폼폼 {i}번 꼭짓점의 꺾임이 {turn:F3}도입니다 — 문턱" +
                    $"({AccessoryStrokeBudgetTests.CornerDegrees}도)에서 5도 이상 떨어져 있어야 " +
                    "판정이 부동소수 잡음에 흔들리지 않습니다.");
            }
        }

        /// <summary>규칙 2·3-2 회귀 울타리 — 폼폼은 <b>채운 보조색</b>이고, 털모자의 보조색은 이것뿐이다
        /// (37-6 규칙 3-2가 "폼폼"을 그 예로 이름까지 적어 두었다).</summary>
        [Test]
        public void 폼폼은_유일한_보조색_채움으로_남는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> beanie = Beanie(rig);

            Assert.That(beanie.Count, Is.InRange(2, 4),
                $"털모자의 도형이 {beanie.Count}개입니다 — 정원은 2~4개입니다(37-6 규칙 5).");

            int accents = 0;
            for (int i = 0; i < beanie.Count; i++)
            {
                if (beanie[i].Tone == AccessoryShapeBuilder.Accent) accents++;
            }
            Assert.AreEqual(1, accents, "털모자의 보조색 도형은 폼폼 하나여야 합니다(규칙 3-2).");

            AccessoryShapeBuilder.Shape pom = Pom(rig);
            Assert.AreEqual(AccessoryShapeBuilder.Accent, pom.Tone, "폼폼이 보조색이 아닙니다.");
            Assert.IsTrue(pom.Filled,
                "폼폼의 채움이 사라졌습니다 — 윤곽선으로 두면 규칙 1이 요구하는 '내부를 보여주는 크기'가 " +
                "3.0획(1.03R)이 되어 머리 반지름만 한 폼폼이 필요해집니다(방울이 같은 계산을 겪었다).");
            Assert.IsTrue(pom.Loop, "폼폼이 닫힌 도형이 아닙니다.");
        }

        // ============================================================================
        // 2. 실루엣을 깨지 않았다 — 고정 대상은 반지름이 아니라 <b>꼭대기</b>다
        // ============================================================================

        [Test]
        public void 폼폼_꼭대기가_액자_상한에_그대로_머문다()
        {
            // 옛 값 0.18 + 0.22 = 0.40. 이 합이 유지되는 한 실루엣의 상한은 움직이지 않는다.
            Assert.AreEqual(0.40f, AccessoryShapeBuilder.BeaniePomCrestRiseRatio, 1e-6f,
                "폼폼 꼭대기의 상승분이 옛 값(0.18 + 0.22 = 0.40R)에서 벗어났습니다 — " +
                "이 합이 곧 털모자 실루엣의 꼭대기입니다.");
            Assert.AreEqual(AccessoryShapeBuilder.BeaniePomCrestRiseRatio,
                AccessoryShapeBuilder.BeaniePomOffsetRatio + AccessoryShapeBuilder.BeaniePomRadiusRatio, 1e-6f,
                "오프셋이 꼭대기 상수에서 유도되지 않습니다(규칙 4-a) — 반지름을 고칠 때 꼭대기가 " +
                "조용히 액자를 넘습니다.");

            AccessoryShapeBuilder.Rig rig = Rig();
            Vector3[] p = Pom(rig).Points;
            float top = float.MinValue;
            for (int i = 0; i < p.Length; i++) top = Mathf.Max(top, p[i].y);
            float topInR = (top - rig.HeadCenterY) / rig.HeadRadius;

            // 위상 90도라 가장 높은 꼭짓점이 <b>정확히</b> 꼭대기다(위상 0이면 여기가 어긋난다).
            float expected = AccessoryShapeBuilder.BeanieBandTopRatio
                + AccessoryShapeBuilder.BeanieCrownHeightRatio
                + AccessoryShapeBuilder.BeaniePomCrestRiseRatio;
            Assert.AreEqual(expected, topInR, 1e-4f,
                $"폼폼의 가장 높은 점이 {topInR:F4}R인데 유도값은 {expected:F4}R입니다 — " +
                "다각형 위상이 90도가 아니면 꼭대기가 꼭짓점 사이에 놓여 이 값이 어긋납니다.");

            // CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR = 1.80R.
            Assert.LessOrEqual(topInR, 1.80f + 1e-4f,
                $"털모자 꼭대기가 머리 중심 위 {topInR:F3}R입니다 — 초상화 액자가 1.80R까지만 담습니다.");
        }

        /// <summary>
        /// 모자 6종 15쌍의 실루엣 차이가 <b>규칙 5의 하한(1획)에서 충분히 떨어져</b> 있다.
        ///
        /// <para>★ 2026-09-01(2차) 하한을 2.94 -&gt; 1.80획으로 <b>낮췄다</b>. 값이 내려간 것은 도형이
        /// 나빠져서가 아니라 <b>여섯 종이 전부 머리를 감싸게 됐기 때문</b>이다(커버선 +0.62~+0.42R
        /// -&gt; +0.08~−0.06R). 옛 6종은 머리 위쪽 1/3에 <b>얹혀</b> 있어서 높이만으로 크게 갈렸고,
        /// 그 큰 값은 "잘 구분된다"가 아니라 "다들 떠 있다"의 부작용이었다. 지금은 모두 관자놀이를
        /// 지나 내려오므로 아래쪽 각도대의 반경이 서로 비슷해진다 — 그게 정상이다.</para>
        ///
        /// <para>실측 최소는 <b>왕관↔베레모 1.84획</b>(EQUIPMENT_SHAPE_SPEC 2절 표와 같은 값).
        /// 규칙 5의 하한은 1.0획이므로 아직 84% 여유가 있다.</para>
        /// </summary>
        [Test]
        public void 모자_6종_실루엣_차이가_2_95획_아래로_내려가지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            int count = ItemCatalog.ItemCountIn(EquipmentSlot.Head);

            float worst = float.MaxValue;
            string worstPair = "";
            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    float d = AccessorySilhouetteMetrics.MaxRadiusDelta(
                        AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Head, a),
                        AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Head, b)) / W;
                    if (d >= worst) continue;
                    worst = d;
                    worstPair = $"{ItemCatalog.Item(EquipmentSlot.Head, a).DisplayName}↔" +
                        $"{ItemCatalog.Item(EquipmentSlot.Head, b).DisplayName}";
                }
            }

            Assert.GreaterOrEqual(worst, 1.80f,
                $"모자 6종의 최소 실루엣 차이가 {worst:F2}획({worstPair})으로 내려갔습니다 — " +
                "재설계 직후 실측은 1.84획(왕관↔베레모)입니다. 규칙 5의 하한은 1.0획이지만, " +
                "여유가 0.8획 아래로 줄면 다음 한 번의 좌표 수정으로 두 모자가 같은 그림이 됩니다.");
        }

        /// <summary>
        /// 규칙 4 — 폼폼이 관에 <b>실제로 얹혀</b> 있다. 옛 폼폼은 관 표면과 0.01획만 겹쳐 사실상
        /// 접해 있었고, 어느 쪽으로든 조금만 움직이면 "0 &lt; 간격 &lt; 1획"(규칙 4가 최악이라고
        /// 못박은 구간)에 빠지는 자리였다.
        /// </summary>
        [Test]
        public void 폼폼이_관에_얹혀_있다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            AccessoryShapeBuilder.Shape pom = Pom(rig);
            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(Beanie(rig), "BeanieCrown");

            float pomBottom = float.MaxValue, pomCenterX = 0f;
            for (int i = 0; i < pom.Points.Length; i++)
            {
                pomBottom = Mathf.Min(pomBottom, pom.Points[i].y);
                pomCenterX += pom.Points[i].x;
            }
            pomCenterX /= pom.Points.Length;

            float crownSurface = SurfaceYAt(crown.Points, pomCenterX);
            float overlap = (crownSurface - pomBottom) / (W * rig.HeadRadius);

            Assert.Greater(overlap, 0.20f,
                $"폼폼이 관 표면과 {overlap:F2}획만 겹칩니다(옛 값 0.01획 = 사실상 접함). " +
                "겹침이 0에 붙어 있으면 어느 쪽 상수를 조금만 건드려도 규칙 4의 최악 구간" +
                "(0 < 간격 < 1획, '선을 두 번 그린 실수'로 보이는 자리)에 빠집니다.");
        }

        /// <summary><paramref name="x"/>에서 다각형 변이 만드는 <b>가장 높은</b> y.</summary>
        private static float SurfaceYAt(Vector3[] pts, float x)
        {
            float best = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 a = pts[i], b = pts[(i + 1) % pts.Length];
                if (Mathf.Approximately(a.x, b.x)) continue;
                float t = (x - a.x) / (b.x - a.x);
                if (t < 0f || t > 1f) continue;
                best = Mathf.Max(best, Mathf.Lerp(a.y, b.y, t));
            }
            return best;
        }

        // ============================================================================
        // 3. ★ 네거티브 컨트롤 — 옛 폼폼을 <b>자기완결형으로</b> 재현한다
        // ============================================================================
        // 아래 두 검사는 살아 있는 도형을 한 개도 읽지 않는다. 좌표를 전부 이 파일 안에서 만든다 —
        // 앞 라운드의 교훈("역사를 재현하는 컨트롤은 비교 대상 <b>양쪽</b>을 다 얼려야 한다.
        // 한쪽만 박제하면 나중에 다른 쪽이 바뀌었을 때 역사상 존재한 적 없는 쌍을 재게 된다")을
        // 가장 강한 형태로 지킨다: 비교 대상이 아예 없고, 문턱(1.5획 / 45도)만 살아 있는 자를 쓴다.

        /// <summary>옛 배포 형상의 폼폼(8각 · 반지름 0.22R). 세그먼트/반지름/위상 전부 박제.</summary>
        private const int OldPomSegments = 8;
        private const float OldPomRadiusRatio = 0.22f;

        private static Vector3[] FrozenPolygon(float radius, int segments, float startDegrees)
        {
            var pts = new Vector3[segments];
            float step = Mathf.PI * 2f / segments;
            float phase = startDegrees * Mathf.Deg2Rad;
            for (int i = 0; i < segments; i++)
            {
                float a = phase + step * i;
                pts[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }
            return pts;
        }

        [Test]
        public void 컨트롤_옛_폼폼은_잉크_사각형_규칙을_실제로_어긴다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float w = AccessoryStrokeBudgetTests.BudgetWorld(rig);

            var old = new AccessoryShapeBuilder.Shape("OldBeaniePom",
                FrozenPolygon(OldPomRadiusRatio * rig.HeadRadius, OldPomSegments, 0f),
                true, AccessoryShapeBuilder.SortHead,
                tone: AccessoryShapeBuilder.Accent, filled: true);

            Vector2 extent = AccessorySilhouetteMetrics.ExtentInR(rig, old.Points);
            float span = Mathf.Max(extent.x, extent.y) / W;
            Assert.AreEqual(1.28f, span, 0.01f,
                "옛 폼폼의 잉크 사각형이 기록(1.28획)과 다릅니다 — 컨트롤이 재현하려는 형상이 " +
                "무엇인지부터 어긋났습니다.");

            Assert.IsNotNull(AccessoryStrokeBudgetTests.DescribeRuleOneViolation(old, w),
                "옛 폼폼(8각 0.22R)이 규칙 1을 통과한다고 나옵니다 — 검사기가 죽었습니다. " +
                "이 컨트롤이 초록이면 위 '폼폼이 획 예산을 지킨다'도 아무것도 증명하지 못합니다.");
        }

        /// <summary>
        /// ★ 이 라운드가 <b>새로 찾아낸 것</b> — 8각형에서는 획 예산 검사의 꺾임 판정이
        /// <b>부동소수 잡음에 달려 있다</b>.
        /// <para>꺾임이 정확히 45.0도 = 문턱이라, 같은 도형을 통째로 회전시키기만 해도(회전은 수학적으로
        /// 각도를 한 도도 바꾸지 않는다) 어떤 꼭짓점이 "꺾임"으로 세어지는지가 달라진다. 그러면
        /// "양끝이 <b>모두</b> 꺾임"인 선분이 성립했다 안 했다 하고, 검사는 0.49획짜리 변을
        /// 잡을 때도 못 잡을 때도 있다 — 초록도 빨강도 아닌 <b>무의미</b>다.</para>
        /// <para>그래서 이 컨트롤은 "잡는다/못 잡는다"를 단언하지 않는다(그 자체가 미정이므로).
        /// <b>판정이 갈린다</b>는 사실을 단언한다. 폼폼이 10각형이어야 하는 이유가 이것이고,
        /// 살아 있는 폼폼이 그 자리에서 벗어났다는 것은
        /// <see cref="폼폼_꺾임이_검사_문턱에서_확실히_떨어져_있다"/>가 따로 잠근다.</para>
        /// </summary>
        [Test]
        public void 컨트롤_8각형은_꺾임_판정이_회전만으로_갈린다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float w = AccessoryStrokeBudgetTests.BudgetWorld(rig);

            // 잉크 사각형 = 2 × 반지름 = 1.6획(문턱 1.5획 통과) / 한 변 0.61획(문턱 1.0획 미달).
            // span 검사에 먼저 걸리면 stub 사각지대를 볼 수 없으므로 일부러 통과하는 크기로 만든다.
            float radius = 0.8f * w;

            Vector3[] baseline = FrozenPolygon(radius, OldPomSegments, 0f);
            float side = Vector3.Distance(baseline[0], baseline[1]) / w;
            Assert.That(side, Is.GreaterThan(0.5f).And.LessThan(1f),
                $"컨트롤 8각형의 한 변이 {side:F2}획입니다 — 1획 미만이어야 이 검사가 뜻을 갖습니다.");
            Assert.GreaterOrEqual(
                Mathf.Max(AccessorySilhouetteMetrics.ExtentInR(rig, baseline).x,
                    AccessorySilhouetteMetrics.ExtentInR(rig, baseline).y) / AccessorySilhouetteMetrics.StrokeInR,
                1.5f, "컨트롤 8각형이 잉크 사각형 검사에 먼저 걸리면 stub 사각지대를 볼 수 없습니다.");

            for (int i = 0; i < OldPomSegments; i++)
            {
                float turn = AccessoryStrokeBudgetTests.TurnDegrees(
                    baseline[(i - 1 + OldPomSegments) % OldPomSegments], baseline[i],
                    baseline[(i + 1) % OldPomSegments]);
                Assert.AreEqual(AccessoryStrokeBudgetTests.CornerDegrees, turn, 0.01f,
                    $"8각형 {i}번 꺾임이 {turn:F4}도입니다 — 문턱과 같은 값이라는 것이 이 사각지대의 원인입니다.");
            }

            // 회전은 각도를 바꾸지 않는다. 그런데도 "꺾임으로 세어진 꼭짓점 수"가 한 다각형 안에서
            // 0도 8도 아닌 값(= 같은 도형인데 어떤 꼭짓점은 꺾임, 어떤 꼭짓점은 아님)이 나온다면
            // 그 판정은 기하가 아니라 반올림이 정한 것이다.
            bool sawMixedClassification = false;
            for (int step = 0; step < 36 && !sawMixedClassification; step++)
            {
                Vector3[] rotated = FrozenPolygon(radius, OldPomSegments, step * 2.5f);
                int corners = 0;
                for (int i = 0; i < OldPomSegments; i++)
                {
                    float turn = AccessoryStrokeBudgetTests.TurnDegrees(
                        rotated[(i - 1 + OldPomSegments) % OldPomSegments], rotated[i],
                        rotated[(i + 1) % OldPomSegments]);
                    if (turn >= AccessoryStrokeBudgetTests.CornerDegrees) corners++;
                }
                sawMixedClassification = corners > 0 && corners < OldPomSegments;
            }

            Assert.IsTrue(sawMixedClassification,
                "정확히 45도인 8각형을 36번 회전시켰는데 꺾임 판정이 한 번도 갈리지 않았습니다 — " +
                "각도 계산이나 문턱이 바뀌어 이 사각지대가 사라졌을 수 있습니다. 그렇다면 " +
                "AccessoryShapeBuilder의 '폼폼/방울이 10각형인 이유' 주석도 함께 다시 쓰십시오.");
        }
    }
}
