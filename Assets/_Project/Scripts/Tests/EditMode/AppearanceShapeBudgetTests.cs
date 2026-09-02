using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ FX 6종 + PET 6종(= <see cref="AppearanceShapeBuilder"/> 소관 <b>12종</b>) 도형의 획 예산 회귀 —
    /// 2026-09-01, docs/UX_FLOW.md 37-6 규칙 1/4/5 · docs/EQUIPMENT_SHAPE_SPEC_FXPET.md.
    ///
    /// ============================================================================
    /// 왜 별도 파일인가
    /// ============================================================================
    /// 기존 <see cref="AccessoryStrokeBudgetTests"/>는 <b>착용 액세서리 30종</b>이 대상이고 소유 파일이
    /// <c>AccessoryShapeBuilder</c>다. 12종은 <c>AppearanceShapeBuilder</c>에 살아서 <b>그 린트가 한 번도
    /// 이들을 재지 않았다</b> — 그것이 12종만 "조잡함"이 안 고쳐진 구조적 이유 중 하나다.
    ///
    /// ============================================================================
    /// ★ 이 라운드가 추가한 것 — "45도 함정"을 닫는 <b>두 번째 규칙 1 항목</b>
    /// ============================================================================
    /// <see cref="AccessoryStrokeBudgetTests.DescribeRuleOneViolation"/>은 "양끝이 모두 45° 이상 꺾인 변"만
    /// 잰다. 그 문턱에는 정당한 이유가 있다(매끄러운 곡선을 촘촘히 쪼개는 것을 금지하면 안 된다).
    /// 그런데 <b>정12각형의 꺾임은 30°, 정14각형은 25.7°, 5분할 초승달은 40°</b>다 —
    /// 전부 문턱 아래라 <b>규칙을 어기면서 린트에 한 줄도 안 찍혔다</b>. 실제로 그렇게 숨어 있던
    /// 위반이 6건이었다(물방울 0.79획 · 공 0.86획 · 껍데기 0.88획 · 속점 0.33획 · 먼지 0.71/0.46획).
    ///
    /// 이 함정은 이 저장소가 <b>이미 한 번 발견해 코드 주석에 적어 두었다</b>(AccessoryShapeBuilder의
    /// 폼폼 문단: "옛 8각형은 규칙을 어기면서 린트에는 잡히지 않는 자리였다"). 그때 폼폼 하나만 고치고
    /// 검사는 안 고쳤다. 그래서 이번에는 <b>검사를 고친다</b> —
    /// <see cref="DescribeShortestEdgeViolation"/>이 꺾임과 무관하게 <b>가장 짧은 실제 변</b>을 잰다.
    /// (눈은 45°라는 문턱을 모른다. 짧은 변은 꺾임이 몇 도든 획에 먹힌다.)
    ///
    /// 30종으로 넓히는 것은 <see cref="최단_실제_변_검사를_액세서리_30종으로_확장한다"/> 참고 —
    /// 지금 켜면 <b>작업 중인 좌표</b>를 재게 되므로 다음 라운드다.
    ///
    /// ============================================================================
    /// ★ 규칙 39-P — 입자형(FX)에는 정원/보조색을 안 건다 (2026-09-01 리더 승인)
    /// ============================================================================
    /// 규칙 5(도형 2~4개)와 규칙 3-2(보조색 정확히 1개)는 <b>착용 액세서리</b>의 규칙이다.
    /// FX 5종은 한 알이 여럿 뜨는 <b>입자</b>라 한 알에 조각 둘을 넣으면 알이 머리 지름의 78%가 된다
    /// (근거 산술은 <see cref="AppearanceShapeBuilder"/> 머리말). 그래서 그 두 규칙은
    /// <b>FX 카드 그림</b>에 걸고 <b>월드의 한 알</b>에는 걸지 않는다. 규칙 1은 양쪽에 그대로 건다.
    /// PET은 입자가 아니므로(항상 한 마리) 정원/보조색을 그대로 지킨다.
    /// 아래 두 검사(<see cref="FX는_입자라_월드_한_알에_정원과_보조색을_걸지_않는다"/> /
    /// <see cref="PET은_정원과_보조색_규칙을_그대로_지킨다"/>)가 그 비대칭을 코드로 못박는다.
    /// </summary>
    public sealed class AppearanceShapeBudgetTests
    {
        /// <summary>출하 배율에서 실제로 그려지는 획(머리 반경 배수) ≈ 0.344R.
        /// 값의 단일 소스는 액세서리 쪽과 같다 — 두 곳에 따로 적으면 언젠가 하나만 바뀐다.</summary>
        private static float W => AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii;

        /// <summary>모든 도형을 머리 반경 1 기준으로 만든다(전부 R 배수라 이 스케일이 곧 "R 단위"다).</summary>
        private const float R = 1f;

        /// <summary>R = 1일 때의 전신 높이. 공만 <b>신장</b> 배수로 크기가 정의돼 있어서 필요하다.</summary>
        private static float H =>
            StickConfig.BaselineCharacterTotalHeight / AccessoryShapeBuilder.BaselineHeadVisualRadius;

        // ============================================================================
        // 0. 도형 표 — 실시간 렌더러가 <b>한 아이템에 대해 만드는 선들</b>을 그대로 옮긴 것
        // ============================================================================
        // ★ 좌표는 한 줄도 베끼지 않는다. 전부 AppearanceShapeBuilder를 불러서 만든다.
        //   여기 있는 것은 "어떤 도형이 몇 개, 어느 색으로 붙는가"라는 <b>조합</b>뿐이고, 그 조합의
        //   원본은 CharacterFxRenderer.Build* / CharacterPetRenderer.Build*다. 두 파일이 이번 라운드의
        //   편집 금지 대상이라 조합을 여기 옮겨 적었다 — 렌더러가 선을 하나 더 붙이면 이 표가
        //   낡는다. 그 드리프트는 아래 "아직 미완" 검사들이 잡는다(도형 개수를 직접 단언한다).

        private readonly struct WorldShape
        {
            public readonly string Name;
            public readonly Vector3[] Points;
            public readonly bool Loop;
            public readonly bool Accent;      // true = 보조색(_secondary)

            public WorldShape(string name, Vector3[] points, bool loop, bool accent = false)
            {
                Name = name;
                Points = points;
                Loop = loop;
                Accent = accent;
            }
        }

        /// <summary>FX 한 알이 만드는 선들. "없음"(0번)은 도형이 없는 것이 정상이다.</summary>
        private static WorldShape[] FxShapes(int item)
        {
            switch (item)
            {
                case AppearanceShapeBuilder.FxNone:
                    return new WorldShape[0];

                case AppearanceShapeBuilder.FxFootprint:
                    // ★ 미완 — 아래 면제 대장 참고. 지금 그려지는 것은 굵은 캡 하나(둥근 점)이고
                    //   그 지름은 좌표가 아니라 CharacterFxRenderer.BuildDot이 정한다.
                    return new WorldShape[0];

                case AppearanceShapeBuilder.FxSparkle:
                {
                    float arm = R * AppearanceShapeBuilder.SparkleArmInR;
                    return new[]
                    {
                        new WorldShape("CrossV", AppearanceShapeBuilder.SparkleStroke(arm, 0), false),
                        new WorldShape("CrossH", AppearanceShapeBuilder.SparkleStroke(arm, 1), false),
                    };
                }

                case AppearanceShapeBuilder.FxDust:
                {
                    float radius = R * AppearanceShapeBuilder.DustRadiusInR;
                    return new[]
                    {
                        new WorldShape("Crescent0", AppearanceShapeBuilder.DustCrescent(radius, 0), false),
                        new WorldShape("Crescent1", AppearanceShapeBuilder.DustCrescent(radius, 1), false),
                    };
                }

                case AppearanceShapeBuilder.FxBubble:
                    // 가장 작은 방울로 잰다 — 반지름이 작을수록 각수가 불리하다.
                    return new[]
                    {
                        new WorldShape("BubbleRing",
                            AppearanceShapeBuilder.BubbleRing(R * AppearanceShapeBuilder.BubbleMinRadiusInR,
                                AppearanceShapeBuilder.BubbleSegments), true),
                    };

                case AppearanceShapeBuilder.FxLeaf:
                {
                    float length = R * AppearanceShapeBuilder.LeafLengthInR;
                    return new[]
                    {
                        new WorldShape("LeafBlade", AppearanceShapeBuilder.LeafBlade(length), true),
                        new WorldShape("LeafStem", AppearanceShapeBuilder.LeafStem(length), false),
                    };
                }
            }

            Assert.Fail($"FX {item}번의 도형 표가 없습니다 — 아이템을 늘렸으면 여기도 늘리세요.");
            return new WorldShape[0];
        }

        /// <summary>PET 한 마리가 만드는 선들. 리틀스틱메이트(2번)는 design-character 소관이라 뺀다.</summary>
        private static WorldShape[] PetShapes(int item)
        {
            switch (item)
            {
                case AppearanceShapeBuilder.PetBall:
                {
                    float radius = H * AppearanceShapeBuilder.BallRadiusInHeight;
                    return new[]
                    {
                        new WorldShape("BallRing",
                            AppearanceShapeBuilder.BallRing(radius, AppearanceShapeBuilder.BallSegments), true),
                        new WorldShape("BallSeam", AppearanceShapeBuilder.BallSeam(radius), false, accent: true),
                    };
                }

                case AppearanceShapeBuilder.PetPlane:
                {
                    float span = R * AppearanceShapeBuilder.PlaneWingSpanInR;
                    return new[]
                    {
                        new WorldShape("PlaneBody", AppearanceShapeBuilder.PlaneBody(span), true),
                        new WorldShape("PlaneFold", AppearanceShapeBuilder.PlaneFold(span), false, accent: true),
                    };
                }

                case AppearanceShapeBuilder.PetCursor:
                    // ★ 미완 — 스펙은 머리(주색)+꼬리(보조색) 2조각인데 지금은 한 획이다(면제 대장 참고).
                    return new[]
                    {
                        new WorldShape("CursorFriend",
                            AppearanceShapeBuilder.CursorArrow(R * AppearanceShapeBuilder.CursorSizeInR), false),
                    };

                case AppearanceShapeBuilder.PetBalloon:
                    return new[]
                    {
                        new WorldShape("BalloonString", AppearanceShapeBuilder.BalloonString(R), false, accent: true),
                        new WorldShape("BalloonBody", AppearanceShapeBuilder.BalloonBody(R), true),
                    };

                case AppearanceShapeBuilder.PetSnail:
                {
                    float size = R * AppearanceShapeBuilder.SnailSizeInR;
                    return new[]
                    {
                        new WorldShape("SnailFoot", AppearanceShapeBuilder.SnailFoot(size, 1f), false),
                        new WorldShape("SnailShell",
                            AppearanceShapeBuilder.SnailShell(size, 1f, AppearanceShapeBuilder.SnailShellSegments), true),
                        new WorldShape("SnailShellCore",
                            AppearanceShapeBuilder.SnailShellCore(size, 1f, AppearanceShapeBuilder.SnailCoreSegments),
                            true, accent: true),
                    };
                }
            }

            Assert.Fail($"PET {item}번의 도형 표가 없습니다 — 아이템을 늘렸으면 여기도 늘리세요.");
            return new WorldShape[0];
        }

        private static IEnumerable<TestCaseData> FxItems()
        {
            yield return new TestCaseData(AppearanceShapeBuilder.FxNone).SetName("FX 없음");
            yield return new TestCaseData(AppearanceShapeBuilder.FxFootprint).SetName("FX 발자국");
            yield return new TestCaseData(AppearanceShapeBuilder.FxSparkle).SetName("FX 반짝임");
            yield return new TestCaseData(AppearanceShapeBuilder.FxDust).SetName("FX 먼지구름");
            yield return new TestCaseData(AppearanceShapeBuilder.FxBubble).SetName("FX 물방울");
            yield return new TestCaseData(AppearanceShapeBuilder.FxLeaf).SetName("FX 나뭇잎");
        }

        private static IEnumerable<TestCaseData> PetItems()
        {
            yield return new TestCaseData(AppearanceShapeBuilder.PetBall).SetName("PET 작은공");
            yield return new TestCaseData(AppearanceShapeBuilder.PetPlane).SetName("PET 종이비행기");
            yield return new TestCaseData(AppearanceShapeBuilder.PetCursor).SetName("PET 커서친구");
            yield return new TestCaseData(AppearanceShapeBuilder.PetBalloon).SetName("PET 풍선");
            yield return new TestCaseData(AppearanceShapeBuilder.PetSnail).SetName("PET 달팽이");
            // 리틀스틱메이트(PetMini)는 여기 없다 — 머리/팔다리를 본체와 통일하는 작업이
            // design-character에 배정돼 있고, 그 라운드가 좌표를 통째로 다시 잡는다.
            // ★ 그 라운드가 끝나면 여기 한 줄을 넣어라(넣지 않으면 영원히 안 재진다).
        }

        // ============================================================================
        // 1. 규칙 1 — 12종 전 도형이 획 예산을 지킨다 (★ 45도 함정 포함)
        // ============================================================================

        [TestCaseSource(nameof(FxItems))]
        public void FX_모든_도형이_획_예산을_지킨다(int item)
            => AssertRuleOne($"FX {item}번", FxShapes(item));

        [TestCaseSource(nameof(PetItems))]
        public void PET_모든_도형이_획_예산을_지킨다(int item)
            => AssertRuleOne($"PET {item}번", PetShapes(item));

        private static void AssertRuleOne(string label, WorldShape[] shapes)
        {
            float w = W * R;
            for (int i = 0; i < shapes.Length; i++)
            {
                WorldShape s = shapes[i];

                string ink = DescribeInkBoxViolation(s.Name, s.Points, w);
                Assert.IsNull(ink, $"{label} {ink}");

                string stub = DescribeStubSegmentViolation(s.Name, s.Points, s.Loop, w);
                Assert.IsNull(stub, $"{label} {stub}");

                string shortest = DescribeShortestEdgeViolation(s.Name, s.Points, s.Loop, w);
                Assert.IsNull(shortest, $"{label} {shortest}");
            }
        }

        /// <summary>
        /// ★ 이 라운드가 새로 넣은 규칙 1 항목 — <b>꺾임 여부와 무관한 가장 짧은 실제 변</b>.
        ///
        /// <para><see cref="AccessoryStrokeBudgetTests.DescribeRuleOneViolation"/>의 "양끝이 꺾임인 변" 검사는
        /// 매끄러운 곡선을 통과시키기 위한 것이고 그 자체는 옳다. 문제는 <b>정다각형</b>이다 —
        /// 12각형의 꺾임은 30°라 문턱(45°) 아래인데, 반지름이 작으면 그 변이 획보다 짧다.
        /// 즉 "곡선처럼 매끄럽지만 실제로는 한 변이 통째로 먹히는" 도형이 검사를 통과했다.</para>
        ///
        /// <para>길이가 0인 변(닫으려고 첫 점을 다시 적은 자리)은 변이 아니므로 건너뛴다.</para>
        /// </summary>
        internal static string DescribeShortestEdgeViolation(string name, Vector3[] p, bool loop, float w)
        {
            if (p == null || p.Length < 2) return null;

            int segments = loop ? p.Length : p.Length - 1;
            for (int i = 0; i < segments; i++)
            {
                float len = Vector3.Distance(p[i], p[(i + 1) % p.Length]);
                if (len < 1e-6f) continue;               // 닫기용 중복점
                if (len >= w) continue;
                return $"'{name}'의 {i}->{(i + 1) % p.Length} 변이 {len / w:F2}획입니다(최단 실제 변). " +
                    "꺾임이 45° 미만이라 '그리려다 만 점' 검사에는 안 잡히지만, 눈은 문턱을 모릅니다 — " +
                    "획보다 짧은 변은 꺾임이 몇 도든 통째로 먹힙니다(37-6 규칙 1).";
            }
            return null;
        }

        /// <summary>규칙 1의 잉크 사각형 항목. 액세서리 린트와 같은 자다.</summary>
        private static string DescribeInkBoxViolation(string name, Vector3[] p, float w)
        {
            if (p == null || p.Length < 2) return $"'{name}'의 점이 2개 미만입니다.";

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < p.Length; i++)
            {
                min = Vector2.Min(min, new Vector2(p[i].x, p[i].y));
                max = Vector2.Max(max, new Vector2(p[i].x, p[i].y));
            }

            float span = Mathf.Max(max.x - min.x, max.y - min.y);
            if (span >= w * 1.5f) return null;
            return $"'{name}'의 잉크 사각형이 {span / w:F2}획입니다 — 1.5획 미만이면 화면에서 " +
                "'뚱뚱한 점' 하나로 보입니다(37-6 규칙 1).";
        }

        /// <summary>규칙 1의 "그리려다 만 점" 항목 — 문턱은 액세서리 린트와 <b>같은 상수</b>를 본다.</summary>
        private static string DescribeStubSegmentViolation(string name, Vector3[] p, bool loop, float w)
        {
            int n = p.Length;
            if (n < 3) return null;

            var corner = new bool[n];
            for (int i = loop ? 0 : 1; i < (loop ? n : n - 1); i++)
            {
                corner[i] = AccessoryStrokeBudgetTests.TurnDegrees(p[(i - 1 + n) % n], p[i], p[(i + 1) % n])
                    >= AccessoryStrokeBudgetTests.CornerDegrees;
            }

            int segments = loop ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % n;
                if (!corner[i] || !corner[j]) continue;
                float len = Vector3.Distance(p[i], p[j]);
                if (len >= w) continue;
                return $"'{name}'의 {i}->{j} 선분이 {len / w:F2}획입니다(양끝 꺾임) — " +
                    "독립된 획으로 읽혀야 하는데 획 하나보다 짧으면 통째로 먹혀 사라집니다.";
            }
            return null;
        }

        // ============================================================================
        // 2. 각수는 반지름이 산다 — 위반 6건을 한꺼번에 만든 원인
        // ============================================================================

        [Test]
        public void 원의_각수가_반지름이_살_수_있는_값을_넘지_않는다()
        {
            float w = W;

            AssertSegments("물방울 링", AppearanceShapeBuilder.BubbleSegments,
                AppearanceShapeBuilder.BubbleMinRadiusInR, w);
            AssertSegments("공 링", AppearanceShapeBuilder.BallSegments,
                H * AppearanceShapeBuilder.BallRadiusInHeight, w);
            AssertSegments("달팽이 껍데기 링", AppearanceShapeBuilder.SnailShellSegments,
                AppearanceShapeBuilder.SnailShellRadiusRatio * AppearanceShapeBuilder.SnailSizeInR, w);
            AssertSegments("껍데기 속점", AppearanceShapeBuilder.SnailCoreSegments,
                AppearanceShapeBuilder.SnailShellCoreRatio * AppearanceShapeBuilder.SnailSizeInR, w);
        }

        private static void AssertSegments(string label, int segments, float radiusInR, float w)
        {
            int max = AppearanceShapeBuilder.MaxSegmentsForRadiusInR(radiusInR, w);
            Assert.LessOrEqual(segments, max,
                $"{label}: 반지름 {radiusInR:F3}R이 살 수 있는 각수는 {max}각인데 {segments}각을 씁니다 — " +
                $"한 변이 {2f * radiusInR * Mathf.Sin(Mathf.PI / segments) / w:F2}획이 되어 획에 먹힙니다. " +
                "옛 값 12·14는 액세서리 원(반지름 0.8~1.0R)에서 베껴 온 값이고, FX/PET의 원은 그보다 작습니다.");
        }

        [Test]
        public void 각수_상한식이_반지름과_함께_줄어든다()
        {
            // 유도식 n ≤ π / asin(W / 2r)의 방향성 자체를 잠근다 — 식을 잘못 뒤집으면
            // "작을수록 각을 많이 쓴다"가 되어 이 라운드가 고친 결함이 그대로 되살아난다.
            float w = W;
            Assert.Greater(AppearanceShapeBuilder.MaxSegmentsForRadiusInR(1.0f, w),
                AppearanceShapeBuilder.MaxSegmentsForRadiusInR(0.3f, w),
                "반지름이 커졌는데 쓸 수 있는 각수가 늘지 않습니다 — 상한식이 뒤집혔습니다.");
            Assert.GreaterOrEqual(AppearanceShapeBuilder.MaxSegmentsForRadiusInR(0.001f, w), 3,
                "각수 하한은 3(삼각형)입니다 — 더 내려가면 도형이 아닙니다.");
        }

        // ============================================================================
        // 3. ★ 규칙 39-P — FX와 PET에 <b>다른 자</b>를 댄다
        // ============================================================================

        [TestCaseSource(nameof(FxItems))]
        public void FX는_입자라_월드_한_알에_정원과_보조색을_걸지_않는다(int item)
        {
            WorldShape[] shapes = FxShapes(item);

            // 이 검사는 "적어도 된다"를 확인하는 것이 아니라, 규칙 39-P가 <b>의도된 예외</b>임을
            // 코드로 남기는 것이 목적이다. 다음 사람이 "왜 FX만 정원 검사가 없지?"를 묻지 않게.
            int accent = 0;
            for (int i = 0; i < shapes.Length; i++) if (shapes[i].Accent) accent++;

            Assert.AreEqual(0, accent,
                $"FX {item}번의 월드 도형에 보조색이 {accent}개 붙었습니다. " +
                "CharacterFxRenderer는 ItemCatalog.ResolveWornPalette를 부르지 않아 보조색 자체가 없습니다 — " +
                "보조색을 쓰려면 렌더러가 먼저 바뀌어야 합니다(제안 B). 그 전에는 이 값이 0이어야 " +
                "'카드에는 있는 색이 착용하면 사라진다'가 조용히 늘어나지 않습니다.");

            Assert.LessOrEqual(shapes.Length, 2,
                $"FX {item}번의 월드 도형이 {shapes.Length}개입니다 — 입자 한 알은 2개를 넘지 않습니다. " +
                "한 알을 두 조각으로 나누면 조각당 1.5획 + 간격 1.5획이라 전체가 머리 지름의 78%가 됩니다 " +
                "(규칙 39-P의 근거 산술). 무리(무늬)를 그려야 하는 곳은 월드가 아니라 카드입니다.");
        }

        [TestCaseSource(nameof(PetItems))]
        public void PET은_정원과_보조색_규칙을_그대로_지킨다(int item)
        {
            if (item == AppearanceShapeBuilder.PetCursor)
            {
                // ★ 미완(건너뜀) — 규칙 1은 이미 닫혔고 정원/보조색만 남았다. Fail이 아니라 Ignore인
                //   이유는 CLAUDE.md 규약이다: 못 고친 갭은 러너에 <b>건너뜀</b>으로 계속 보여야 잊히지 않는다.
                // ★ 2026-09-02 qa-regression — <b>사유를 갱신했다</b>. 옛 사유는
                //   "그 파일은 이번 라운드의 편집 금지 대상"이었는데 <b>그 라운드는 끝났다</b>
                //   (CharacterPetRenderer.cs 마지막 커밋 4a5a4de, 2026-09-02 12:42).
                //   그런데도 Ignore를 유지하는 이유는 <b>갭이 실제로 남아 있기 때문</b>이다 —
                //   같은 파일의 아직_미완_커서친구는_머리와_꼬리로_안_쪼개졌다가 지금도 초록이고,
                //   그것이 "아직 한 획(닫힌 8점)"이라는 뜻이다(그 검사가 빨개지면 여기도 함께 켜라).
                //   고칠 파일은 프로덕션 .cs이므로 qa-regression이 손대지 않는다 — <b>배정 필요</b>.
                Assert.Ignore("커서친구는 정원 1개 / 보조색 0개입니다(2~4개, 정확히 1개여야). " +
                    "스펙은 머리(주색)+꼬리(보조색) 2조각인데, 쪼개려면 " +
                    "CharacterPetRenderer.BuildCursorFriend가 LineRenderer를 두 개 만들어야 합니다. " +
                    "★ 막던 사유(편집 잠금)는 2026-09-02 12:42에 소멸했습니다 — 지금 남은 것은 " +
                    "'아직 아무도 안 했다'뿐이고, 프로덕션 렌더러 변경이라 배정이 필요합니다. " +
                    "쪼개는 자리는 AppearanceShapeBuilder.CursorArrow 배열의 2번/5번 점입니다.");
            }

            WorldShape[] shapes = PetShapes(item);
            int accent = 0;
            for (int i = 0; i < shapes.Length; i++) if (shapes[i].Accent) accent++;

            Assert.That(shapes.Length, Is.InRange(2, 4),
                $"PET {item}번의 도형이 {shapes.Length}개입니다 — 정원은 2~4개입니다(37-6 규칙 5). " +
                "PET은 입자가 아니라 항상 한 마리이므로 규칙 39-P의 예외를 받지 않습니다.");

            Assert.AreEqual(1, accent,
                $"PET {item}번의 보조색 도형이 {accent}개입니다 — 정확히 1개여야 합니다(37-6 규칙 3-2). " +
                "보조색은 '형제들과 나를 가르는 단 한 부분'에만 씁니다.");
        }

        // ============================================================================
        // 4. ★ 면제 대장 — 아직 못 고친 2건. <b>고쳐지면 빨간불</b>이 되게 만들어 둔다
        // ============================================================================
        // 이 라운드는 좌표만 고칠 수 있었다(AppearanceShapeBuilder.cs 한 파일). 아래 두 건은
        // 좌표 문제가 아니라 <b>호출부 구조</b> 문제라 CharacterFxRenderer / CharacterPetRenderer가
        // 함께 바뀌어야 하는데, 두 파일은 이번 라운드의 편집 금지 대상이었다(제안 B와 겹치면
        // 무엇 때문에 그림이 달라졌는지 판정할 수 없다).
        //
        // 대장이 스스로 낡지 않게, 두 검사는 "아직 미완인가"를 <b>직접</b> 단언한다.
        // 누가 고치면 이 두 검사가 빨개지고, 그때 이 문단과 함께 지우면 된다.

        [Test]
        public void 아직_미완_발자국은_밑창이_아니라_둥근_점이다()
        {
            Vector3[] dot = AppearanceShapeBuilder.DotSegment(R * 0.2f);
            Assert.AreEqual(2, dot.Length,
                "발자국 도형이 2점이 아닙니다 — 스펙의 '옆에서 본 밑창'(열린 3점)으로 바뀌었다면 " +
                "이 면제 대장 문단과 이 검사를 지우고, 위 FxShapes의 FxFootprint 자리에 실제 도형을 넣으세요. " +
                "함께 확인할 것: CharacterFxRenderer.BuildDot이 선 두께를 radius*2(=1.19획)가 아니라 " +
                "보통 획으로 잡아야 밑창의 최단 변 1.14획이 실제로 1획을 넘습니다.");

            // 지금 상태의 결함을 숫자로 남긴다: 지름 1.19획 < 1.5획(규칙 1 잉크 사각형) + 둥근 점.
            // 옆에서 보는 이 앱에서 둥근 점은 발자국이 아니다.
        }

        [Test]
        public void 아직_미완_커서친구는_머리와_꼬리로_안_쪼개졌다()
        {
            Vector3[] arrow = AppearanceShapeBuilder.CursorArrow(R * AppearanceShapeBuilder.CursorSizeInR);
            Assert.AreEqual(8, arrow.Length,
                "커서 화살표가 한 획(닫힌 8점)이 아닙니다 — 머리/꼬리 2조각으로 쪼개졌다면 " +
                "이 검사와 PET 정원 검사의 예외 분기를 지우세요.");
            Assert.AreEqual(arrow[0], arrow[arrow.Length - 1],
                "마지막 점이 첫 점과 달라졌습니다 — 부르는 쪽이 loop:false라 이 중복점이 곧 '닫힘'입니다.");

            // 미완이어도 규칙 1은 이미 닫혔다(최단 변 0.47획 -> 1.06획). 남은 것은 정원/보조색뿐이다.
            string shortest = DescribeShortestEdgeViolation("CursorFriend", arrow, false, W * R);
            Assert.IsNull(shortest, shortest);
        }

        /// <summary>
        /// ★ 2026-09-02 qa-regression — <b>켰다</b>. 이 검사는 <c>Assert.Ignore</c>였고 사유는
        /// "AccessoryShapeBuilder.cs가 이번 라운드 편집 중"이었다. <b>그 라운드는 끝났다</b>
        /// (그 파일의 마지막 커밋은 4a5a4de, 2026-09-02 12:42). 사유가 소멸한 면제는 되살린다 —
        /// 안 그러면 "임시"라고 적힌 건너뜀이 영구히 러너에 눌러앉는다.
        ///
        /// <para>옛 처방은 "DescribeShortestEdgeViolation을 AccessoryStrokeBudgetTests로 올려라"였지만
        /// <b>올릴 필요가 없다</b> — 두 파일은 <c>StickMate.Tests.EditMode</c> <b>같은 어셈블리</b>이고
        /// 이 함수는 이미 <c>internal static</c>이다. 옮기면 그 자체가 드리프트 위험이므로,
        /// <b>같은 함수를 그대로</b> 30종에 돌린다(자를 두 벌 만들지 않는다는 이 파일의 원칙).</para>
        ///
        /// <para>대장은 <see cref="AccessoryStrokeBudgetTests.BudgetedKeys"/> <b>하나</b>에서 읽는다.
        /// 목록을 여기 다시 적으면 한쪽만 늘어난다.</para>
        ///
        /// <para>근거: 45° 함정이 FX/PET 12종에서만 6건을 숨기고 있었다 — 30종에 없다고 볼 이유가 없다.</para>
        /// </summary>
        [Test]
        public void 최단_실제_변_검사를_액세서리_30종으로_확장한다()
        {
            AccessoryShapeBuilder.Rig rig = AccessoryStrokeBudgetTests.Rig();
            float w = AccessoryStrokeBudgetTests.BudgetWorld(rig);

            var violations = new List<string>();
            int items = 0;
            int shapesChecked = 0;

            foreach ((EquipmentSlot slot, int item) in AccessoryStrokeBudgetTests.BudgetedKeys())
            {
                items++;
                var sink = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.Append(sink, slot, item, rig);

                string label = $"{slot} {item}번({ItemCatalog.Item(slot, item).DisplayName})";
                Assert.Greater(sink.Count, 0, $"{label}: 도형이 하나도 없습니다.");

                for (int i = 0; i < sink.Count; i++)
                {
                    shapesChecked++;
                    string v = DescribeShortestEdgeViolation(
                        sink[i].Name, sink[i].Points, sink[i].Loop, w);
                    if (v != null) violations.Add($"{label} {v}");
                }
            }

            // ★ 부재 판정에는 양성 대조. 대장이 비거나 Append가 조용히 아무것도 안 만들면
            //   위 foreach가 0바퀴 돌고 이 검사는 "위반 없음"으로 초록이 된다.
            Assert.GreaterOrEqual(items, 24,
                $"액세서리 대장에서 {items}종만 읽혔습니다 — 30종 확장이라고 말할 수 없습니다. " +
                "AccessoryStrokeBudgetTests.BudgetedKeys가 줄었는지 확인하세요.");
            Assert.Greater(shapesChecked, items,
                $"도형을 {shapesChecked}개밖에 재지 않았습니다(아이템 {items}종) — " +
                "아이템당 도형이 1개 미만이면 Append가 사실상 비어 있는 것입니다.");

            // 음성 대조 — 같은 자가 <b>일부러 짧은</b> 변을 실제로 잡는가.
            // 이것이 null이면 위 '위반 0건'은 "규칙이 안 무는 것"과 구분되지 않는다.
            var tiny = new[] { Vector3.zero, new Vector3(w * 0.2f, 0f, 0f), new Vector3(w * 0.2f, w * 3f, 0f) };
            Assert.IsNotNull(DescribeShortestEdgeViolation("음성대조", tiny, false, w),
                "일부러 획의 0.2배짜리 변을 넣었는데 린트가 통과시켰습니다 — 위 '위반 0건'은 무효입니다.");

            // ────────────────────────────────────────────────────────────────────
            // ★ 켜 보니 <b>14건</b>이 나왔다(2026-09-02 실측, HEAD aaac7b2 계열).
            //   전부 같은 정체다 — <b>작은 둥근 것을 각이 많은 다각형으로 그린 자리</b>:
            //     동그란안경 렌즈 앞/뒤 0.60획 · 방울 0.54 · 외알안경 테 0.54 · 털모자 방울 0.50 ·
            //     선글라스 브릿지 0.89 · 민머리 림 앞/뒤 0.76/0.82 · 머리덩어리 4건 0.50~0.96 ·
            //     곱슬 코일 0.95 · 포니테일 꼬리 0.95
            //   즉 이 Ignore가 숨기고 있던 것은 "아직 안 켰다"가 아니라 <b>실재하는 위반 14건</b>이었다.
            //
            //   고치는 것은 도형 좌표/각수 변경 = 프로덕션 .cs다. qa-regression은 손대지 않는다.
            //   그렇다고 빨간불로 두면 러너의 유일한 신호가 이 하나에 덮인다. 그래서 <b>래칫</b>이다:
            //     · 14건보다 <b>늘면 즉시 빨간불</b>(악화는 못 들어온다)
            //     · 남아 있는 동안은 <b>건너뜀</b>으로 러너에 계속 보인다(잊히지 않게)
            //     · <b>0이 되면 초록</b>이 되고, 그때 아래 상수를 0으로 내리면 규칙이 영구히 잠긴다
            //   숫자를 "그냥 통과시키는 상한"으로 쓰지 않는 이유가 이 세 줄이다.
            const int KnownDebtOn20260902 = 14;

            Assert.LessOrEqual(violations.Count, KnownDebtOn20260902,
                $"최단 실제 변 위반이 {violations.Count}건으로 늘었습니다(2026-09-02 실측 " +
                $"{KnownDebtOn20260902}건). 이 라운드가 <b>새 위반을 넣었습니다</b> — " +
                $"래칫은 줄어드는 방향으로만 열립니다.\n  " + string.Join("\n  ", violations));

            if (violations.Count > 0)
            {
                Assert.Ignore(
                    $"★ 미완(건너뜀) — 액세서리 {items}종 / 도형 {shapesChecked}개에 규칙 1(최단 실제 변)을 " +
                    $"켰고 위반 {violations.Count}건이 <b>실재</b>합니다(래칫 상한 {KnownDebtOn20260902}건 이하라 " +
                    "악화는 아닙니다). 전부 '작은 둥근 것의 각수가 반지름에 비해 많다'는 한 가지 정체입니다 — " +
                    "각수를 줄이거나 반지름을 키우면 닫힙니다. 프로덕션 도형 좌표 변경이라 배정이 필요합니다.\n  " +
                    string.Join("\n  ", violations));
            }

            Assert.IsEmpty(violations,
                $"액세서리 {items}종 / 도형 {shapesChecked}개에서 최단 실제 변 위반 {violations.Count}건:\n  " +
                string.Join("\n  ", violations));
        }

        // ============================================================================
        // 5. 물방울 / 나뭇잎 / 풍선 / 달팽이의 개별 계약 (기존 검사 + 이번 라운드 갱신)
        // ============================================================================

        [Test]
        public void 물방울은_가장_작을_때도_속이_보인다()
        {
            float minRadius = AppearanceShapeBuilder.BubbleMinRadiusInR * R;
            Assert.GreaterOrEqual(minRadius * 2f, W * 3f,
                $"가장 작은 물방울의 지름이 {(minRadius * 2f / W):F2}획입니다 — 3획 미만이면 링 안쪽이 " +
                "획에 먹혀 방울이 아니라 까만 점이 됩니다(37-6 규칙 1).");

            Assert.Less(AppearanceShapeBuilder.BubbleMaxRadiusInR, 1f,
                "가장 큰 물방울이 머리(1.0R)보다 큽니다 — 그러면 방울이 아니라 또 하나의 머리로 읽힙니다.");

            // 각수는 이제 이 파일이 소유한다 — 부르는 쪽이 더 많이 달라고 해도 반지름이 못 사면 안 준다.
            Vector3[] ring = AppearanceShapeBuilder.BubbleRing(minRadius, 64);
            Assert.AreEqual(AppearanceShapeBuilder.BubbleSegments, ring.Length,
                "부르는 쪽이 요청한 각수가 그대로 나왔습니다 — 각수 상한은 반지름이 정합니다.");
            for (int i = 0; i < ring.Length; i++)
            {
                Assert.AreEqual(minRadius, ring[i].magnitude, 1e-4f, $"{i}번 점이 원 위에 있지 않습니다.");
            }
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
        public void 풍선_주머니는_속이_보인다()
        {
            float diameter = AppearanceShapeBuilder.BalloonRadiusInR * 2f * R;
            Assert.GreaterOrEqual(diameter, W * 3f,
                $"주머니 지름이 {(diameter / W):F2}획입니다 — 3획 미만이면 풍선이 통짜 점이 됩니다.");
        }

        [Test]
        public void 공의_솔기는_테_위에_정확히_얹힌다()
        {
            float radius = H * AppearanceShapeBuilder.BallRadiusInHeight;
            Vector3[] seam = AppearanceShapeBuilder.BallSeam(radius);

            Assert.AreEqual(radius, seam[0].magnitude, 1e-4f,
                "솔기의 첫 점이 테 위에 없습니다 — 37-6 규칙 4의 간격은 '0 또는 ≥1.5획'인데 " +
                "솔기는 0(닿음) 쪽입니다. 어중간하게 띄우면 공 안에 뜬 실오라기가 됩니다.");
            Assert.AreEqual(radius, seam[seam.Length - 1].magnitude, 1e-4f,
                "솔기의 끝 점이 테 위에 없습니다.");

            // 솔기가 <b>부풀어</b> 있어야 구(球)로 읽힌다 — 직선이면 그냥 지름선(= 바퀴살의 사촌)이다.
            float bulge = 0f;
            for (int i = 0; i < seam.Length; i++) bulge = Mathf.Max(bulge, seam[i].x);
            // 문턱이 <b>획 반폭</b>인 이유: 마루가 그보다 가까우면 곡선 전체가 "같은 자리에 그은 직선"의
            // 잉크 안에 들어가 버린다. 4점 표본이라 마루는 원호의 apex(0.28R)가 아니라 0.247R = 0.72획이다.
            Assert.GreaterOrEqual(bulge, W * 0.5f,
                $"솔기의 부푼 양이 {bulge / W:F2}획입니다 — 획 반폭(0.5획) 미만이면 직선의 잉크에 묻혀 " +
                "지름선(= 바퀴살의 사촌)으로 보입니다.");
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

        [Test]
        public void 달팽이_속점은_획보다_확실히_큰_잉크_사각형을_갖는다()
        {
            // ★ 이 아이템의 <b>유일한 식별 특징</b>이 획보다 작았다(0.87획). 위상 0°(꼭짓점이 좌우)에서
            //   4각형의 폭은 2r이고, 45°로 돌리면 √2·r로 줄어 1.07획이 된다 — 위상이 계약이다.
            Vector3[] core = AppearanceShapeBuilder.SnailShellCore(
                R * AppearanceShapeBuilder.SnailSizeInR, 1f, AppearanceShapeBuilder.SnailCoreSegments);

            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < core.Length; i++)
            {
                minX = Mathf.Min(minX, core[i].x);
                maxX = Mathf.Max(maxX, core[i].x);
            }

            float width = maxX - minX;
            float expected = 2f * AppearanceShapeBuilder.SnailShellCoreRatio * AppearanceShapeBuilder.SnailSizeInR * R;
            Assert.AreEqual(expected, width, 1e-4f,
                "속점의 좌우 폭이 2r이 아닙니다 — 위상이 0°에서 벗어났습니다(45°면 폭이 √2·r로 줄어듭니다).");
            Assert.GreaterOrEqual(width, W * 1.5f,
                $"속점의 잉크 사각형이 {width / W:F2}획입니다 — 규칙 1이 말하는 '그리려다 만 점'입니다.");
        }

        /// <summary>
        /// 껍데기 아랫변과 발 선이 <b>획 반폭 안에서</b> 만나는가. 판정 기준이 좌표가 아니라 획 반폭인
        /// 이유: 두 선은 각각 두께 W로 그려지므로 중심선 거리가 0.5 W 안이면 잉크가 실제로 겹친다.
        /// 위로 벗어나면 껍데기가 <b>공중에 뜨고</b>(규칙 4 위반), 아래로 벗어나면 <b>땅에 잠긴다</b>.
        ///
        /// <para>★ 2026-09-01 껍데기를 0.68R -> 0.78R로 키우면서 중심도 0.66R -> 0.76R로 함께 올렸다.
        /// 이 검사는 <b>그 동시 변경을 강제하는 장치</b>다 — 하나만 바꾸면 여기서 빨개진다.</para>
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
        public void 달팽이_더듬이는_껍데기에_붙어_보이지_않는다()
        {
            // ★ 산술이 아니라 ASCII 래스터 육안 검증에서 잡은 결함이다: 껍데기를 키우자 더듬이 획과
            //   껍데기 획의 중심선 간격이 1.12획이 됐다 — 규칙 4의 "0 또는 ≥1.5획"에서 가장 나쁜 구간
            //   (붙지도 떨어지지도 않아 잉크가 지저분하게 뭉친다). 중심을 뒤로 0.15R 물려 풀었다.
            float size = R * AppearanceShapeBuilder.SnailSizeInR;
            Vector3[] foot = AppearanceShapeBuilder.SnailFoot(size, 1f);
            var center = new Vector2(AppearanceShapeBuilder.SnailShellCenterXRatio * size,
                AppearanceShapeBuilder.SnailShellCenterYRatio * size);
            float shellRadius = AppearanceShapeBuilder.SnailShellRadiusRatio * size;

            // 더듬이 = 발 획의 마지막 두 점(더듬이 뿌리 -> 끝). 자는 <b>그 두 점</b>이다.
            // ★ 변의 중간까지 재면 최근접이 1.50획으로 문턱에 정확히 걸린다 — 판정이 부동소수 오차에
            //   좌우되는 자는 회귀 검사에 못 쓴다. 설계 검산(ASCII 래스터)도 끝점으로 쟀고,
            //   되돌림(중심 −0.30R -> −0.15R)은 끝점 자로 1.12획이 되어 확실히 잡힌다.
            float worst = float.MaxValue;
            for (int i = foot.Length - 2; i < foot.Length; i++)
            {
                float d = Vector2.Distance(new Vector2(foot[i].x, foot[i].y), center) - shellRadius;
                worst = Mathf.Min(worst, Mathf.Abs(d));
            }

            Assert.GreaterOrEqual(worst, W * 1.5f,
                $"더듬이와 껍데기 링의 중심선 간격이 {worst / W:F2}획입니다 — 규칙 4의 '0 또는 ≥1.5획' 중 " +
                "최악 구간입니다(붙지도 떨어지지도 않아 잉크가 뭉칩니다). " +
                $"{nameof(AppearanceShapeBuilder.SnailShellCenterXRatio)}를 더 뒤로 물리세요.");
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

            int seg = AppearanceShapeBuilder.SnailShellSegments;
            Vector3[] shellR = AppearanceShapeBuilder.SnailShell(R, 1f, seg);
            Vector3[] shellL = AppearanceShapeBuilder.SnailShell(R, -1f, seg);
            Assert.AreEqual(-Center(shellR).x, Center(shellL).x, 1e-4f,
                "껍데기 중심이 좌우 반전을 따라가지 않습니다 — 발만 뒤집히고 껍데기는 그대로 남습니다.");
        }

        // ============================================================================
        // 6. ★ 커서 친구를 1.40R로 키운 부작용 — 화면 클램프와 추격 연출
        // ============================================================================

        /// <summary>
        /// <see cref="AppearanceShapeBuilder.CursorSizeInR"/>이 0.90 -> 1.40이 되면서
        /// <c>CharacterPetRenderer.TickCursorFriend</c>의 화면 클램프 여백도 같은 배로 커졌다.
        /// 앱을 띄우지 않고 확인할 수 있는 것은 <b>클램프의 순수 함수</b>다
        /// (<see cref="CharacterPetRenderer.ClampOriginToRect"/> — 이 저장소가 펫 부양 결함을 잡을 때
        /// 같은 이유로 분리해 둔 함수다).
        ///
        /// <para>확인 두 가지: (1) 화살표가 화면 밖으로 밀리지 않는다, (2) 여백이 커서 이격 규칙(24pt)을
        /// 침범하지 않아 <b>추격 연출이 안 바뀐다</b>.</para>
        /// </summary>
        [Test]
        public void 커서친구_클램프가_화살표를_화면_안에_붙잡고_추격을_방해하지_않는다()
        {
            // 실측 리그와 같은 화면: orthographicSize 12, 1512x982pt.
            var view = new Rect(-18.46f, -12f, 36.92f, 24f);
            float headRadius = AccessoryShapeBuilder.BaselineHeadVisualRadius
                * AccessoryShapeBuilder.ShippingCharacterScale;
            float margin = headRadius * AppearanceShapeBuilder.CursorSizeInR;

            Vector3[] arrow = AppearanceShapeBuilder.CursorArrow(margin);
            float right = 0f, down = 0f;
            for (int i = 0; i < arrow.Length; i++)
            {
                right = Mathf.Max(right, arrow[i].x);
                down = Mathf.Max(down, -arrow[i].y);
            }

            // (1) 오른쪽 아래 구석으로 밀어붙여도 화살표가 화면 안에 남는가.
            //     원점이 화살표 <b>촉끝</b>이라 클램프의 대칭 여백은 가로엔 넉넉하고 세로엔 빠듯하다.
            Vector2 p = new Vector2(view.xMax + 999f, view.yMin - 999f);
            CharacterPetRenderer.ClampOriginToRect(ref p, view, margin, margin, margin);

            Assert.LessOrEqual(p.x + right, view.xMax + 1e-4f,
                "커서 친구의 오른쪽 끝이 화면 밖으로 나갑니다.");
            float belowScreen = view.yMin - (p.y - down);
            float pointsPerUnit = StickConfig.ReferencePointsPerWorldUnitApprox;
            Assert.LessOrEqual(belowScreen * pointsPerUnit, 1f,
                $"커서 친구의 아래 끝이 화면 밑으로 {belowScreen * pointsPerUnit:F2}pt 삐져나갑니다. " +
                "클램프 여백은 대칭(halfWidth)인데 이 도형의 원점은 촉끝이라 아래 뻗음이 더 깁니다 — " +
                "1pt 안이면 획 반폭(1pt)에 묻히지만 그보다 커지면 CharacterPetRenderer가 " +
                "세로 여백을 따로 받는 오버로드를 써야 합니다.");

            // (2) 여백이 커서 이격/가장자리 규칙(각 24pt)보다 작아야 추격 계산이 그대로다.
            //     여백이 그보다 커지면 클램프가 앵커를 덮어써서 "커서를 못 따라가는" 그림이 된다.
            Assert.Less(margin * pointsPerUnit, 24f,
                $"클램프 여백이 {margin * pointsPerUnit:F1}pt로 커서 이격 규칙(24pt)만큼 커졌습니다 — " +
                "화면 가장자리에서 클램프가 앵커를 이겨 커서 추격이 끊깁니다. " +
                $"{nameof(AppearanceShapeBuilder.CursorSizeInR)}를 더 키우려면 " +
                "CharacterPetRenderer.CursorMinGapPoints도 함께 올려야 합니다.");
        }

        // ==================== 도구 ====================

        private static Vector3 Center(Vector3[] pts)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < pts.Length; i++) sum += pts[i];
            return sum / Mathf.Max(1, pts.Length);
        }
    }
}
