using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 실루엣 구분 회귀 — 2026-09-01. 페르소나(소은)가 실물 스크린샷에서 확인한 결함 3건을 잠근다.
    ///
    /// <para>이 파일이 잡으려는 실패는 "규칙 위반"이 아니라 <b>"이름이 다른데 그림이 같다"</b>이다.
    /// 37-6 규칙 5의 [필수] 조항 — "실루엣 1개, <b>이것만으로 아이템이 식별돼야 한다</b>" — 이 무너지는 자리다.</para>
    ///
    /// <list type="number">
    ///   <item><b>바가지머리 ≡ 단정한머리</b>. 배율 0.75에서 반경 차 0.20R = <b>0.58획</b>.
    ///         두 아이템 모두 정체가 실루엣이 아니라 <b>내부 선</b>이었다.</item>
    ///   <item><b>펜던트(마름모) ≡ 방울(원)</b>. 2pt 획이 0.40×0.60R 마름모의 꼭짓점을 둥글려
    ///         "조금 큰 동그라미"가 됐다. 외곽 차 <b>0.54획</b>.</item>
    ///   <item><b>베레모 보조색 테</b>가 자기 밑변과 0.01~0.26획. 규칙 4가 "최악"이라고 못박은
    ///         <c>0 &lt; 간격 &lt; 1획</c> 구간이라 밑단이 아니라 <b>관을 가로지르는 띠</b>로 읽혔다.</item>
    /// </list>
    ///
    /// <para><b>지표를 함께 고쳤다.</b> 옛 반경 프로파일은 정점만·상반구만 봐서 (1)번 쌍을 3.77획으로
    /// 부풀리고 있었다 — 도형만 고치고 지표를 그대로 뒀다면 이 파일은 처음부터 초록불이라 아무것도
    /// 지키지 못한다. 그래서 <b>세 결함마다 네거티브 컨트롤</b>(옛 좌표를 그대로 박제한 검사)을 함께 둔다:
    /// 지표가 실제로 빨간불을 낼 수 있음을 같은 스위트 안에서 증명한다.</para>
    /// </summary>
    public sealed class AccessorySilhouetteDistinctionTests
    {
        /// <summary>"화면에서 다른 그림"의 문턱. 규칙 1의 "그려지는 요소 ≥ 1.0획"과 같은 값이다 —
        /// 두 실루엣의 차이가 획 하나보다 작으면 그 차이는 획 안에 통째로 매몰된다.</summary>
        private static float W => AccessorySilhouetteMetrics.StrokeInR;

        private static AccessoryShapeBuilder.Rig Rig(float facing = 1f)
            => AccessorySilhouetteMetrics.Rig(facing);

        private static float Delta(in AccessoryShapeBuilder.Rig rig, EquipmentSlot slot, int a, int b)
            => AccessorySilhouetteMetrics.MaxRadiusDelta(
                AccessorySilhouetteMetrics.Profile(rig, slot, a),
                AccessorySilhouetteMetrics.Profile(rig, slot, b));

        private static string Name(EquipmentSlot slot, int item)
            => ItemCatalog.Item(slot, item).DisplayName;

        // ============================================================================
        // 1. 바가지머리 — 정체를 내부 선에서 실루엣으로 옮겼다
        // ============================================================================

        [Test]
        public void 바가지머리와_단정한머리가_실루엣으로_갈린다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float d = Delta(rig, EquipmentSlot.Hair,
                AccessoryShapeBuilder.HairBowl, AccessoryShapeBuilder.HairNeat);

            Assert.Greater(d, W,
                $"바가지머리와 단정한머리의 실루엣 차이가 {d / W:F2}획뿐입니다(옛 값 0.58획). " +
                "두 아이템의 정체가 다시 '실루엣이 아니라 내부 선'으로 돌아갔다는 뜻입니다 — " +
                "페르소나가 실물에서 '두 장이 같은 그림'이라고 확인한 그 상태입니다(37-6 규칙 5 [필수]).");
        }

        // ============================================================================
        // ★ 옛 머리카락 도형의 박제 — 2026-09-01(2차) HAIR 재설계로 프로덕션에서 사라진 코드
        // ============================================================================
        // 아래 상수들과 <see cref="OldHairSilhouette"/>는 <b>옛 프로덕션 코드를 그대로 옮겨 얼린 것</b>이다
        // (<c>HairSpanStartDegrees</c> −16 / <c>HairSpanEndDegrees</c> 196 / <c>HairlineEdgeRatio</c> −0.06
        //  / <c>HairlineCrestRatio</c> 0.50 / <c>HairlineHalfWidthRatio</c> 0.88 / <c>HairlineSegments</c> 6
        //  / <c>HairCapSegments</c> 14). 프로덕션에서는 폐기됐다 — "돔 + 커튼 2 + 두피 안쪽 호"로
        // 구성 자체가 바뀌었고, 옛 구성은 <b>반경이 일정한 극좌표 호</b>라 상수 조정으로는 고칠 수
        // 없었기 때문이다(docs/EQUIPMENT_SHAPE_SPEC.md 4-2).
        //
        // 여기 남긴 이유는 하나다: 이 파일의 네거티브 컨트롤이 <b>역사를 재현</b>하기 때문이다.
        // 살아 있는 상수를 참조하면 컨트롤이 "옛 그림"이 아니라 "현재 그림"을 재게 되어 스스로 무너진다.
        // (같은 사고가 이 파일의 펜던트 컨트롤에서 실제로 한 번 났다 — 아래 2절 문단 참고.)
        private const float OldHairSpanStartDegrees = -16f;
        private const float OldHairSpanEndDegrees = 196f;
        private const int OldHairCapSegments = 14;
        private const int OldHairlineSegments = 6;
        private const float OldHairlineHalfWidthRatio = 0.88f;
        private const float OldHairlineEdgeRatio = -0.06f;
        private const float OldHairlineCrestRatio = 0.50f;
        private const float OldBowlCapRadiusRatio = 1.34f;
        private const float OldNeatCapRadiusRatio = 1.14f;
        private const float OldNeatFrontLiftRatio = 0.16f;

        /// <summary>옛 <c>AccessoryShapeBuilder.HairSilhouette</c> — 돔 + 포물선 이마선.</summary>
        private static Vector3[] OldHairSilhouette(in AccessoryShapeBuilder.Rig rig,
            float baseRadiusRatio, float frontLiftRatio)
        {
            float r = rig.HeadRadius;
            float hc = rig.HeadCenterY;
            var pts = new Vector3[OldHairCapSegments + 1 + OldHairlineSegments + 1];

            for (int i = 0; i <= OldHairCapSegments; i++)
            {
                float t = i / (float)OldHairCapSegments;
                float rad = Mathf.Lerp(OldHairSpanStartDegrees, OldHairSpanEndDegrees, t) * Mathf.Deg2Rad;
                float radius = baseRadiusRatio + frontLiftRatio * (1f - t);
                pts[i] = rig.F(Mathf.Cos(rad) * radius * r, hc + Mathf.Sin(rad) * radius * r);
            }
            for (int i = 0; i <= OldHairlineSegments; i++)
            {
                float u = -1f + 2f * i / OldHairlineSegments;
                float y = OldHairlineEdgeRatio + (OldHairlineCrestRatio - OldHairlineEdgeRatio) * (1f - u * u);
                pts[OldHairCapSegments + 1 + i] =
                    rig.F(u * OldHairlineHalfWidthRatio * r, hc + y * r);
            }
            return pts;
        }

        private static List<AccessoryShapeBuilder.Shape> Frozen(string name, Vector3[] points)
            => new List<AccessoryShapeBuilder.Shape>
            {
                new AccessoryShapeBuilder.Shape(name, points, true,
                    AccessoryShapeBuilder.SortHair, filled: true),
            };

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>지표가 실제로 빨간불을 낼 수 있는가.</b>
        /// <para>옛 바가지머리(형제와 같은 돔을 반경만 1.34R로 키운 것)와 <b>옛 단정한머리</b>를
        /// 나란히 재구성해 같은 자로 잰다. 이 값이 1획을 넘어 버리면 지표가 다시 눈이 먼 것이고,
        /// 위 검사는 공허하게 초록불이 된다.</para>
        /// <para>★ 2026-09-01(2차) — <b>비교 대상도 함께 얼렸다.</b> 처음 작성본은 옛 바가지머리만
        /// 재구성하고 단정한머리는 <b>살아 있는 도형</b>을 읽었다. 그래서 HAIR 6종이 "덩어리"로
        /// 재설계되자(커튼이 −2.12R까지 내려간다) 이 검사가 실패했다 — 실제로 잰 것이
        /// "옛 바가지 vs 새 단정", 즉 <b>역사상 존재한 적 없는 쌍</b>이었기 때문이다.
        /// 이 파일의 펜던트 컨트롤이 같은 사고를 이미 한 번 겪고 같은 방식으로 고쳤다(아래 2절).</para>
        /// </summary>
        [Test]
        public void 지표가_옛_바가지머리를_실제로_잡는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();

            Assert.AreNotEqual(OldBowlCapRadiusRatio, AccessoryShapeBuilder.BowlCapRadiusRatio,
                "바가지머리 돔 반경이 옛 값으로 되돌아갔습니다 — 이 컨트롤이 재현하려는 결함이 곧 현재 상태입니다.");

            List<AccessoryShapeBuilder.Shape> oldBowl =
                Frozen("OldBowlCap", OldHairSilhouette(rig, OldBowlCapRadiusRatio, 0.02f));
            List<AccessoryShapeBuilder.Shape> oldNeat =
                Frozen("OldNeatCap", OldHairSilhouette(rig, OldNeatCapRadiusRatio, OldNeatFrontLiftRatio));

            float d = AccessorySilhouetteMetrics.MaxRadiusDelta(
                AccessorySilhouetteMetrics.ProfileOf(rig, oldBowl, rig.HeadCenterY),
                AccessorySilhouetteMetrics.ProfileOf(rig, oldNeat, rig.HeadCenterY));

            Assert.Less(d, W,
                $"옛 바가지머리와 옛 단정한머리의 차이가 {d / W:F2}획으로 측정됐습니다 — 페르소나 실측은 0.58획입니다. " +
                "지표가 다시 부풀리고 있습니다(옛 지표는 정점만·상반구만 봐서 같은 쌍을 3.77획으로 셌습니다). " +
                "이 검사가 실패하면 위 '갈린다' 검사들은 전부 공허합니다.");
        }

        /// <summary>앞머리 선은 실루엣의 안쪽 경계와 <b>정확히 겹친다</b>(간격 0).
        /// <para>규칙 4가 "최악"이라고 못박은 것은 <c>0 &lt; 간격 &lt; 1획</c>이지 겹침이 아니다.
        /// 겹치면 화면에서 선 하나(보조색)로 읽히고, 조금이라도 어긋나면 그 순간
        /// "선을 두 번 그린 실수"가 된다 — 베레모가 정확히 그 상태였다(아래 3절).</para></summary>
        [Test]
        public void 바가지_앞머리_선은_실루엣_경계와_정확히_겹친다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> bowl =
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Hair, AccessoryShapeBuilder.HairBowl);

            AccessoryShapeBuilder.Shape mass = AccessorySilhouetteMetrics.Find(bowl, "HairMass");
            AccessoryShapeBuilder.Shape fringe = AccessorySilhouetteMetrics.Find(bowl, "HairFringe");

            float gap = AccessorySilhouetteMetrics.MaxGapToShape(rig, fringe.Points, mass);
            Assert.That(gap, Is.LessThan(1e-4f).Or.GreaterThanOrEqualTo(W * 1.5f),
                $"앞머리 선이 실루엣 경계에서 {gap / W:F2}획 떨어져 있습니다 — 0(겹침)이거나 1.5획 이상이어야 합니다. " +
                "그 사이는 붙은 것도 뗀 것도 아니라 선을 두 번 그린 실수로 보입니다(37-6 규칙 4).");
        }

        /// <summary>
        /// 앞머리 선은 <b>획을 얹은 뒤에도</b> 눈동자 위에 있다.
        /// <para>기존 <c>머리카락_채움이_눈동자를_덮지_않는다</c>는 폴리곤만 본다(획 두께가 빠져 있다).
        /// 옛 앞머리 띠는 기하로는 0.370R이었지만 획 반폭을 더하면 0.198R까지 내려와 눈동자 위끝
        /// (0.227R)을 <b>0.029R 파고들었다</b> — 눈이 꺼져 있어 안 보였을 뿐인 잠복 결함이다.
        /// 여기서는 바가지머리에 한해 그 계산을 실제로 한다.</para>
        /// </summary>
        [Test]
        public void 바가지_앞머리는_획을_얹어도_눈동자_위에_있다()
        {
            // 눈동자 반지름 0.030(배율 1.0 프리팹 실측) -> R 배수.
            const float PupilRadiusInR = 0.030f / AccessoryShapeBuilder.BaselineHeadVisualRadius;
            float pupilTop = AccessoryShapeBuilder.EyeOffsetYInHeadRadii + PupilRadiusInR;
            float inkBottom = AccessoryShapeBuilder.BowlFringeLineRatio - W * 0.5f;

            Assert.Greater(inkBottom, pupilTop,
                $"앞머리 선의 잉크 아래끝이 {inkBottom:F3}R인데 눈동자 위끝은 {pupilTop:F3}R입니다 — " +
                "폴리곤은 안 겹쳐도 화면에서는 겹칩니다(획 반폭 " + (W * 0.5f).ToString("F3") + "R). " +
                "Editor/SceneBootstrapper.BakeEyes를 true로 되돌리는 날 바가지머리만 눈을 덮습니다.");
        }

        // ============================================================================
        // 2. 펜던트 — 원과 갈리는 것은 크기가 아니라 종횡비다
        // ============================================================================

        [Test]
        public void 펜던트와_방울이_실루엣으로_갈린다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float d = Delta(rig, EquipmentSlot.Neck,
                AccessoryShapeBuilder.NeckPendant, AccessoryShapeBuilder.NeckBell);

            Assert.Greater(d, W,
                $"펜던트 목걸이와 방울 목걸이의 외곽 차이가 {d / W:F2}획뿐입니다(옛 값 0.54획). " +
                "둘은 같은 목줄을 쓰고 매달린 것만 다르므로, 매달린 것이 안 갈리면 두 아이템이 같은 그림입니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 마름모(반폭 0.20R × 반높이 0.30R, 목선 아래 0.40R)와
        /// <b>옛 방울</b>(반지름 0.17R, 목선 아래 0.34R, 추 포함)을 그대로 재구성해 같은 자로 잰다.
        /// 1획을 넘으면 지표가 눈이 먼 것이다.
        ///
        /// <para>★ 2026-09-01(같은 날 후속 라운드) — <b>양쪽을 다 박제하도록 고쳤다.</b> 처음 작성본은
        /// 옛 펜던트만 재구성하고 방울은 <b>살아 있는 도형</b>을 읽었다. 그래서 뒤이어 방울이
        /// 규칙 1(잉크 사각형 0.99획)을 고치느라 커지자 이 검사가 1.12획으로 <b>실패</b>했다 —
        /// 실제로 잰 것이 "옛 펜던트 vs 새 방울", 즉 <b>역사상 존재한 적 없는 쌍</b>이었기 때문이다.
        /// 역사를 재현하는 컨트롤은 <b>비교 대상도 함께 얼려야</b> 한다. 지금 값은 0.56획이다.</para></summary>
        [Test]
        public void 지표가_옛_펜던트를_실제로_잡는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float ty = AccessoryShapeBuilder.NeckLocalY(rig);
            float py = ty - r * 0.40f;
            float phw = r * 0.20f;
            float phh = r * 0.30f;

            // 목줄은 그때도 지금도 같은 CollarCurve다(펜던트/방울이 공유하는 하나) — 살아 있는 것을 쓴다.
            Vector3[] collar = AccessorySilhouetteMetrics.Find(
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Neck,
                    AccessoryShapeBuilder.NeckPendant), "Chain").Points;

            var old = new List<AccessoryShapeBuilder.Shape>
            {
                new AccessoryShapeBuilder.Shape("OldChain", collar, false, AccessoryShapeBuilder.SortNeck),
                new AccessoryShapeBuilder.Shape("OldPendant", new[]
                {
                    rig.F(0f, py + phh), rig.F(phw, py), rig.F(0f, py - phh), rig.F(-phw, py),
                }, true, AccessoryShapeBuilder.SortNeck, tone: AccessoryShapeBuilder.Accent, filled: true),
            };

            // 옛 방울: BellDropRatio 0.34f / BellRadiusRatio 0.17f / 10각형(위상 0도) + 추 0.10R.
            float oldBellY = ty - r * 0.34f;
            float oldBellR = r * 0.17f;
            var oldBellPts = new Vector3[10];
            for (int i = 0; i < oldBellPts.Length; i++)
            {
                float a = Mathf.PI * 2f / oldBellPts.Length * i;
                oldBellPts[i] = rig.F(Mathf.Cos(a) * oldBellR, oldBellY + Mathf.Sin(a) * oldBellR);
            }
            var oldBell = new List<AccessoryShapeBuilder.Shape>
            {
                new AccessoryShapeBuilder.Shape("OldCollar", collar, false, AccessoryShapeBuilder.SortNeck),
                new AccessoryShapeBuilder.Shape("OldBell", oldBellPts, true, AccessoryShapeBuilder.SortNeck,
                    tone: AccessoryShapeBuilder.Accent),
                new AccessoryShapeBuilder.Shape("OldBellClapper", new[]
                {
                    rig.F(0f, oldBellY - oldBellR), rig.F(0f, oldBellY - oldBellR - r * 0.10f),
                }, false, AccessoryShapeBuilder.SortNeck, tone: AccessoryShapeBuilder.Accent),
            };

            float d = AccessorySilhouetteMetrics.MaxRadiusDelta(
                AccessorySilhouetteMetrics.ProfileOf(rig, old, ty),
                AccessorySilhouetteMetrics.ProfileOf(rig, oldBell, ty));

            Assert.Less(d, W,
                $"옛 펜던트와 옛 방울의 차이가 {d / W:F2}획으로 측정됐습니다 — 실측은 0.56획입니다. " +
                "지표가 부풀리고 있으면 위 '갈린다' 검사가 공허합니다.");
        }

        /// <summary>마름모는 <b>세로로 길어야</b> 원과 갈린다 — 원은 어느 방향에서도 반경이 같다.
        /// 크기만 키우면 "조금 더 큰 동그라미"가 될 뿐이라는 것이 이번 결함의 교훈이다.</summary>
        [Test]
        public void 펜던트는_원이_아니다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            Vector2 pendant = AccessorySilhouetteMetrics.ExtentInR(rig,
                AccessorySilhouetteMetrics.Find(
                    AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Neck,
                        AccessoryShapeBuilder.NeckPendant), "Pendant").Points);
            Vector2 bell = AccessorySilhouetteMetrics.ExtentInR(rig,
                AccessorySilhouetteMetrics.Find(
                    AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Neck,
                        AccessoryShapeBuilder.NeckBell), "Bell").Points);

            float pendantAspect = pendant.y / pendant.x;
            float bellAspect = bell.y / bell.x;

            Assert.GreaterOrEqual(pendantAspect, 2f,
                $"펜던트의 종횡비가 {pendantAspect:F2}뿐입니다(옛 값 1.50) — 방울({bellAspect:F2})과 " +
                "같은 '동그란 덩어리'로 읽힙니다. 보관함 설명이 말하는 '마름모'가 되려면 세로로 길어야 합니다.");

            // 빗변이 획보다 짧으면 꼭짓점이 통째로 둥글려진다(옛 1.05획이 정확히 그 자리였다).
            Vector3[] pts = AccessorySilhouetteMetrics.Find(
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Neck,
                    AccessoryShapeBuilder.NeckPendant), "Pendant").Points;
            for (int i = 0; i < pts.Length; i++)
            {
                float edge = Vector3.Distance(pts[i], pts[(i + 1) % pts.Length]) / rig.HeadRadius;
                Assert.GreaterOrEqual(edge, W * 1.5f,
                    $"펜던트의 {i}번 빗변이 {edge / W:F2}획입니다 — 1.5획 미만이면 양끝 꼭짓점의 " +
                    "획이 서로 만나 마름모가 타원으로 뭉개집니다(37-6 규칙 1).");
            }
        }

        /// <summary>매달린 지점이 보여야 물건이 공중에 뜨지 않는다(규칙 4). 위 꼭짓점은
        /// 목줄이 가장 처지는 점 <b>그 자리</b>여야 한다.</summary>
        [Test]
        public void 펜던트는_목줄_최저점에_매달린다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> shapes =
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckPendant);

            Vector3[] chain = AccessorySilhouetteMetrics.Find(shapes, "Chain").Points;
            float chainLow = float.MaxValue;
            for (int i = 0; i < chain.Length; i++) chainLow = Mathf.Min(chainLow, chain[i].y);

            Vector3[] pendant = AccessorySilhouetteMetrics.Find(shapes, "Pendant").Points;
            float pendantTop = float.MinValue;
            for (int i = 0; i < pendant.Length; i++) pendantTop = Mathf.Max(pendantTop, pendant[i].y);

            Assert.AreEqual(chainLow, pendantTop, 1e-5f,
                "펜던트의 위 꼭짓점이 목줄 최저점과 어긋났습니다 — 매달린 지점이 안 보이면 " +
                "장식이 가슴 앞 공중에 뜬 것으로 보입니다(37-6 규칙 4).");
        }

        [Test]
        public void 목_6종이_서로_구분된다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            int count = ItemCatalog.ItemCountIn(EquipmentSlot.Neck);

            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    float d = Delta(rig, EquipmentSlot.Neck, a, b);
                    Assert.Greater(d, W,
                        $"{Name(EquipmentSlot.Neck, a)}와 {Name(EquipmentSlot.Neck, b)}의 외곽 차이가 " +
                        $"{d / W:F2}획뿐입니다 — 카테고리 안에서 구분되는 것이 곧 아이템의 존재 이유입니다(규칙 7-3).");
                }
            }
        }

        // ============================================================================
        // 3. 베레모 — 보조색 테를 밑변 그 자체로
        // ============================================================================

        [Test]
        public void 베레모_보조색_테는_자기_밑변과_정확히_겹친다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> beret =
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret);

            AccessoryShapeBuilder.Shape body = AccessorySilhouetteMetrics.Find(beret, "BeretBody");
            AccessoryShapeBuilder.Shape rim = AccessorySilhouetteMetrics.Find(beret, "BeretRim");

            float gap = AccessorySilhouetteMetrics.MaxGapToShape(rig, rim.Points, body);
            Assert.That(gap, Is.LessThan(1e-4f).Or.GreaterThanOrEqualTo(W * 1.5f),
                $"베레모 테가 관 실루엣에서 {gap / W:F2}획 떨어져 있습니다(옛 값 0.26획). " +
                "0 < 간격 < 1획은 규칙 4가 '최악'이라고 못박은 구간이라, 테가 밑단이 아니라 " +
                "관을 가로지르는 띠로 읽혀 베레모가 '띠 두른 정모'가 됩니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 테(x −1.10R ~ +0.84R, y = brimY)를 그대로 재구성한다.
        /// 지표가 그것을 <b>금지 구간 안</b>이라고 말해야 위 검사가 의미를 갖는다.</summary>
        [Test]
        public void 지표가_옛_베레모_테를_실제로_잡는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float brimY = rig.HeadCenterY + r * AccessoryShapeBuilder.BeretBrimLineRatio;

            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret),
                "BeretBody");

            var oldRim = new[] { rig.F(-r * 1.10f, brimY), rig.F(r * 0.84f, brimY) };
            float gap = AccessorySilhouetteMetrics.MaxGapToShape(rig, oldRim, crown);

            Assert.That(gap, Is.GreaterThan(1e-4f).And.LessThan(W),
                $"옛 베레모 테의 간격이 {gap / W:F2}획으로 측정됐습니다 — 실측은 0.26획(금지 구간)입니다. " +
                "지표가 이 값을 금지 구간 밖으로 읽으면 위 검사는 아무것도 막지 못합니다.");
        }
    }
}
