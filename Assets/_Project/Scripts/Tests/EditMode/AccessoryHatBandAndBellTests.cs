using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 모자 띠 겹침 + 방울 획 예산 회귀 — 2026-09-01(같은 날 베레모/펜던트 라운드의 <b>후속 배정</b>).
    ///
    /// <para>앞선 라운드가 베레모의 보조색 테를 밑변과 겹치게 고치면서 <b>자기 배정 밖</b>의 같은 결함
    /// 2건을 수치와 함께 남겼다: 중절모 띠 <b>0.41획</b>, 밀짚모자 띠 <b>0.47획</b>. 둘 다 37-6 규칙 4가
    /// "<b>최악</b>"이라고 못박은 <c>0 &lt; 간격 &lt; 1획</c> 구간이다 — 붙은 것도 뗀 것도 아니라
    /// <b>선을 두 번 그린 실수</b>로 보이고, 획(0.344R)을 얹으면 두 선의 잉크가 실제로 겹쳐
    /// 한 덩어리로 뭉갠다.</para>
    ///
    /// <para>같은 보고가 방울(NECK)의 규칙 1 위반도 남겼다: 잉크 사각형 <b>0.99획</b>. 공 전체가
    /// 획 하나 굵기라 화면에서는 "뚱뚱한 점"이다. 재 보니 <b>매단 자리도 0.11획 어긋나</b> 있어
    /// 같은 금지 구간에 있었다(보고에 없던 항목 — 이 파일이 잠근다).</para>
    ///
    /// <para>계측은 전부 <see cref="AccessorySilhouetteMetrics"/>(변 조밀 표본 · 360도)로 한다.
    /// 옛 지표(정점만 · 상반구만)는 값을 부풀려 읽는 버그가 있었고, 그 사실 자체가
    /// <see cref="AccessorySilhouetteDistinctionTests"/>의 네거티브 컨트롤로 증명돼 있다.
    /// 이 파일도 <b>고친 항목마다 네거티브 컨트롤</b>을 짝지어 둔다 — 옛 좌표를 그대로 박제해
    /// "자가 그것을 빨간불로 읽는가"를 같은 스위트 안에서 단언한다.</para>
    /// </summary>
    public sealed class AccessoryHatBandAndBellTests
    {
        /// <summary>배율 0.75(출하 기본)에서 실제로 그려지는 획. 판정 문턱은 전부 이 값의 배수다.</summary>
        private static float W => AccessorySilhouetteMetrics.StrokeInR;

        private static AccessoryShapeBuilder.Rig Rig() => AccessorySilhouetteMetrics.Rig();

        private static List<AccessoryShapeBuilder.Shape> Build(EquipmentSlot slot, int item)
            => AccessorySilhouetteMetrics.Build(Rig(), slot, item);

        // ============================================================================
        // 1. 중절모 / 밀짚모자 — 띠가 금지 구간을 벗어났는가
        // ============================================================================

        /// <summary>띠와 관(crown) 밑변은 <b>완전히 겹치거나</b>(간격 0) 확실히 떨어져야(≥1.5획) 한다.
        /// <para>중절모는 관 높이가 0.72R, 밀짚모자는 0.54R뿐이라 "위로 1.5획 올린다"는
        /// <b>산술적으로 불가능</b>하다(위아래로 1.5획씩 두려면 관이 1.03R이어야 한다).
        /// 그래서 규칙 4가 허용하는 나머지 안전 구간인 <b>겹침</b>을 택했고, 띠는 좌표를 새로 적지 않고
        /// 관 밑변의 두 끝점을 그대로 받는다 — 어긋날 자리 자체가 없다(베레모와 같은 해법).</para></summary>
        [TestCase(AccessoryShapeBuilder.HeadFedora, "FedoraBand", "FedoraCrown")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, "StrawBand", "StrawCrown")]
        public void 모자_띠는_자기_관_밑변과_정확히_겹친다(int item, string bandName, string crownName)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> hat = AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, item);
            AccessoryShapeBuilder.Shape band = AccessorySilhouetteMetrics.Find(hat, bandName);
            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(hat, crownName);

            float gap = AccessorySilhouetteMetrics.MaxGapToShape(rig, band.Points, crown);
            Assert.That(gap, Is.LessThan(1e-4f).Or.GreaterThanOrEqualTo(W * 1.5f),
                $"{ItemCatalog.Item(EquipmentSlot.Head, item).DisplayName}의 띠가 관 밑변에서 " +
                $"{gap / W:F2}획 떨어져 있습니다(옛 값 중절모 0.41 / 밀짚모자 0.47획). " +
                "0 < 간격 < 1획은 규칙 4가 '최악'이라고 못박은 구간입니다 — 획을 얹으면 두 선의 잉크가 " +
                "겹쳐 절반은 주색 절반은 보조색인 굵은 막대 하나로 뭉갭니다.");
        }

        /// <summary>띠는 <b>보조색 그대로</b>여야 한다 — 겹치게 만들면서 톤까지 바꾸면 모자에서
        /// 보조색이 사라진다(규칙 3-2: 보조색은 형제와 나를 가르는 단 한 부분).
        /// <para>중절모는 <b>띠가 있는 것이 정체성</b>인 모자라, 이 검사는 "띠를 없애서 겹침 문제를
        /// 해결하는" 길을 막는 울타리이기도 하다.</para></summary>
        [TestCase(AccessoryShapeBuilder.HeadFedora, "FedoraBand")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, "StrawBand")]
        public void 모자_띠는_보조색으로_남아있다(int item, string bandName)
        {
            List<AccessoryShapeBuilder.Shape> hat = Build(EquipmentSlot.Head, item);
            AccessoryShapeBuilder.Shape band = AccessorySilhouetteMetrics.Find(hat, bandName);

            Assert.AreEqual(AccessoryShapeBuilder.Accent, band.Tone,
                $"{bandName}이 보조색이 아닙니다 — 띠는 이 모자를 형제와 가르는 단 한 부분입니다(37-6 규칙 3-2).");

            float span = AccessorySilhouetteMetrics.ExtentInR(Rig(), band.Points).x;
            Assert.GreaterOrEqual(span, W * 1.5f,
                $"{bandName}의 길이가 {span / W:F2}획입니다 — 1.5획 미만이면 띠가 아니라 점입니다(규칙 1).");
        }

        /// <summary>★ 네거티브 컨트롤 — <b>옛 중절모 띠</b>(관 밑변 위 0.14R, x ±0.72R)를 그대로 박제한다.
        /// 자가 이 값을 <b>금지 구간 안</b>(0 &lt; 간격 &lt; 1획)이라고 말해야 위 검사가 의미를 갖는다.</summary>
        [Test]
        public void 지표가_옛_중절모_띠를_실제로_잡는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float brimY = rig.HeadCenterY + r * AccessoryShapeBuilder.FedoraBrimLineRatio;
            float crownHalf = r * AccessoryShapeBuilder.FedoraCrownHalfWidthRatio;

            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora), "FedoraCrown");

            // 옛 좌표: FedoraBandRiseRatio = 0.14f (지금은 지워진 상수 — 여기 값이 곧 검사의 내용이다).
            var oldBand = new[]
            {
                rig.F(-crownHalf, brimY + r * 0.14f),
                rig.F(crownHalf, brimY + r * 0.14f),
            };
            float gap = AccessorySilhouetteMetrics.MaxGapToShape(rig, oldBand, crown);

            Assert.That(gap, Is.GreaterThan(1e-4f).And.LessThan(W),
                $"옛 중절모 띠의 간격이 {gap / W:F2}획으로 측정됐습니다 — 금지 구간(0 < 간격 < 1획) 안이어야 합니다 " +
                "(옛 관에서 0.41획, 재설계된 관에서 0.45획 — 관이 움직이면 값도 함께 움직인다). " +
                "지표가 이 값을 금지 구간 밖으로 읽으면 위 검사는 아무것도 막지 못합니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — <b>옛 밀짚모자 띠</b>(관 밑변 위 0.16R, x ±0.98·0.78R).
        /// 폭이 관보다 2% 좁아 <b>끝만 살짝 어긋나 있던</b> 것까지 그대로 재현한다.</summary>
        [Test]
        public void 지표가_옛_밀짚모자_띠를_실제로_잡는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float brimY = rig.HeadCenterY + r * AccessoryShapeBuilder.StrawBrimLineRatio;
            float crownHalf = r * AccessoryShapeBuilder.StrawCrownHalfWidthRatio;

            AccessoryShapeBuilder.Shape crown = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Head, AccessoryShapeBuilder.HeadStraw), "StrawCrown");

            // 옛 좌표: StrawBandRiseRatio = 0.16f, 반폭은 crownHalf * 0.98f.
            var oldBand = new[]
            {
                rig.F(-crownHalf * 0.98f, brimY + r * 0.16f),
                rig.F(crownHalf * 0.98f, brimY + r * 0.16f),
            };
            float gap = AccessorySilhouetteMetrics.MaxGapToShape(rig, oldBand, crown);

            Assert.That(gap, Is.GreaterThan(1e-4f).And.LessThan(W),
                $"옛 밀짚모자 띠의 간격이 {gap / W:F2}획으로 측정됐습니다 — 금지 구간(0 < 간격 < 1획) 안이어야 " +
                "합니다(옛 관에서 0.47획, 재설계된 관에서 0.46획).");
        }

        /// <summary>
        /// 모자 6종이 서로 구분된다 — <b>띠를 둘 다 관 밑변으로 옮긴 뒤에도</b>.
        /// <para>이 검사가 이 라운드에 필요한 이유: 두 모자의 보조색 선이 같은 자리(관 밑동)로 모였다.
        /// 띠는 실루엣 안쪽 선이라 원리적으로 외곽에 영향을 주지 않지만, "안 준다"를 말로만 두지 않는다.
        /// 실측 최소는 <b>왕관↔베레모 1.84획</b>이다(2026-09-01 감쌈 재설계 이후. 그 이전에는
        /// 털모자↔중절모 2.95획이었는데, 큰 값의 정체는 "잘 구분된다"가 아니라 "여섯 종이 서로 다른
        /// 높이로 <b>떠 있다</b>"였다 — 자세한 것은 AccessoryBeaniePomTests의 같은 검사 문단).</para>
        /// </summary>
        [Test]
        public void 모자_6종이_서로_구분된다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            int count = ItemCatalog.ItemCountIn(EquipmentSlot.Head);

            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    float d = AccessorySilhouetteMetrics.MaxRadiusDelta(
                        AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Head, a),
                        AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Head, b));
                    Assert.Greater(d, W,
                        $"{ItemCatalog.Item(EquipmentSlot.Head, a).DisplayName}와 " +
                        $"{ItemCatalog.Item(EquipmentSlot.Head, b).DisplayName}의 외곽 차이가 {d / W:F2}획뿐입니다 — " +
                        "카테고리 안에서 구분되는 것이 곧 아이템의 존재 이유입니다(규칙 7-3).");
                }
            }
        }

        // ============================================================================
        // 2. 방울 — 규칙 1(획 예산) + 규칙 4(매달린 지점)
        // ============================================================================

        /// <summary>공 전체가 획 하나 굵기면 그것은 원이 아니라 점이다.
        /// <para>옛 방울은 지름 0.34R = <b>0.99획</b>이었다. 규칙 1은 그려지는 도형의 잉크 사각형이
        /// 1.5획 이상일 것을 요구한다.</para></summary>
        [Test]
        public void 방울은_획_예산을_지킨다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            AccessoryShapeBuilder.Shape bell = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Bell");

            Vector2 ext = AccessorySilhouetteMetrics.ExtentInR(rig, bell.Points);
            float span = Mathf.Max(ext.x, ext.y);
            Assert.GreaterOrEqual(span, W * 1.5f,
                $"방울의 잉크 사각형이 {span / W:F2}획입니다(옛 값 0.99획) — 1.5획 미만이면 " +
                "화면에서 '뚱뚱한 점' 하나입니다(37-6 규칙 1).");

            Assert.IsTrue(bell.Filled,
                "방울이 채워져 있지 않습니다 — 윤곽선으로 두면 규칙 1이 요구하는 '내부를 보여주는 크기'가 " +
                "3.0획(1.03R)이라 머리 반지름만 한 방울이 되어야 하고, 그러면 펜던트와 다시 붙습니다. " +
                "방울은 속이 보여야 하는 물건이 아니라 금속 덩어리입니다(규칙 2).");
        }

        /// <summary>
        /// 방울의 <b>꺾임각이 획 예산 검사의 문턱(45도)보다 작다</b> — 즉 매끄러운 곡선으로 인정된다.
        /// <para>왜 검사인가: 변의 수를 줄이면(8각형) 꺾임이 정확히 45도가 되어 각 변이 <b>독립된 획</b>으로
        /// 요구되고(<c>AccessoryStrokeBudgetTests.AssertNoStubSegments</c>), 그 조건을 만족하는 8각형은
        /// 지름이 2.6획이라 펜던트와 다시 붙는다. "원을 각지게 만들수록 커져야 한다"는 이 함정을
        /// 숫자로 박제해 둔다.</para>
        /// </summary>
        [Test]
        public void 방울은_매끄러운_원으로_인정되는_각도를_유지한다()
        {
            Vector3[] p = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Bell").Points;

            Assert.GreaterOrEqual(p.Length, 10, "방울의 변이 10개 미만입니다 — 각져 보입니다.");
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 v1 = p[i] - p[(i - 1 + p.Length) % p.Length];
                Vector2 v2 = p[(i + 1) % p.Length] - p[i];
                float turn = Vector2.Angle(v1, v2);
                Assert.Less(turn, 45f,
                    $"방울 {i}번 꼭짓점의 꺾임이 {turn:F1}도입니다 — 45도 이상이면 획 예산 검사가 " +
                    "각 변을 '독립된 획'으로 요구하고(최소 1.0획), 그 크기의 방울은 펜던트와 다시 붙습니다.");
            }
        }

        /// <summary>매달린 지점이 보여야 물건이 공중에 뜨지 않는다(규칙 4).
        /// 방울과 펜던트가 <b>같은 목줄 좌표</b>에서 매달리므로 위 끝이 목줄 최저점 그 자리다.</summary>
        [Test]
        public void 방울은_목줄_최저점에_매달린다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> shapes = Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell);

            Vector3[] collar = AccessorySilhouetteMetrics.Find(shapes, "Collar").Points;
            float collarLow = float.MaxValue;
            for (int i = 0; i < collar.Length; i++) collarLow = Mathf.Min(collarLow, collar[i].y);

            Vector3[] bell = AccessorySilhouetteMetrics.Find(shapes, "Bell").Points;
            float bellTop = float.MinValue;
            for (int i = 0; i < bell.Length; i++) bellTop = Mathf.Max(bellTop, bell[i].y);

            Assert.AreEqual(collarLow, bellTop, 1e-5f,
                "방울의 꼭대기가 목줄 최저점과 어긋났습니다 — 옛 방울이 정확히 0.11획(금지 구간) " +
                "어긋나 있었습니다. 10각형은 위상 0도로 두면 가장 높은 꼭짓점이 72도에 놓입니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — <b>옛 방울</b>(반지름 0.17R, 목선 아래 0.34R, 위상 0도 10각형)을
        /// 그대로 박제한다. 자가 ① 잉크 사각형을 1.5획 <b>미만</b>으로, ② 매단 자리를 금지 구간으로
        /// 읽어야 위 두 검사가 의미를 갖는다.</summary>
        [Test]
        public void 지표가_옛_방울을_실제로_잡는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float r = rig.HeadRadius;
            float ty = AccessoryShapeBuilder.NeckLocalY(rig);

            // 옛 좌표: BellDropRatio = 0.34f, BellRadiusRatio = 0.17f (지금은 지워졌거나 바뀐 값들).
            float oldY = ty - r * 0.34f;
            float oldR = r * 0.17f;
            var oldBell = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 2f / 10f * i;
                oldBell[i] = rig.F(Mathf.Cos(a) * oldR, oldY + Mathf.Sin(a) * oldR);
            }

            Vector2 ext = AccessorySilhouetteMetrics.ExtentInR(rig, oldBell);
            float span = Mathf.Max(ext.x, ext.y);
            Assert.Less(span, W * 1.5f,
                $"옛 방울의 잉크 사각형이 {span / W:F2}획으로 측정됐습니다 — 실측은 0.99획입니다. " +
                "지표가 이 값을 규칙 1 통과로 읽으면 위 검사는 공허합니다.");

            float oldTop = float.MinValue;
            for (int i = 0; i < oldBell.Length; i++) oldTop = Mathf.Max(oldTop, oldBell[i].y);

            // ★ 2026-09-02 — 목줄 최저점을 <b>실제로 그려진 목줄에서 잰다</b>.
            //   옛 코드에는 AccessoryShapeBuilder.CollarLowLocalY(=CollarRise−CollarDip)가 있었지만,
            //   목 형상이 에셋으로 내려가면서(B-2 파일럿) 그 비율의 주인은 에셋 하나가 됐다.
            //   여기서 상수를 다시 적으면 <b>같은 사실이 두 곳</b>에 생기고, 에셋만 고친 날
            //   이 대조가 조용히 옛 세상을 재게 된다.
            Vector3[] chain = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Collar").Points;
            float collarLowY = float.MaxValue;
            for (int i = 0; i < chain.Length; i++) collarLowY = Mathf.Min(collarLowY, chain[i].y);

            float attachGap = Mathf.Abs(collarLowY - oldTop) / r;
            Assert.That(attachGap, Is.GreaterThan(1e-4f).And.LessThan(W),
                $"옛 방울의 매단 자리 간격이 {attachGap / W:F2}획으로 측정됐습니다 — 실측은 0.11획(금지 구간)입니다.");
        }

        /// <summary>
        /// 방울을 키워도 펜던트와 갈린다 — 이번 수정의 <b>대가</b>를 잠그는 검사다.
        /// <para>둘은 같은 목줄에 매달린 형제라 갈리는 축이 "얼마나 내려오는가"다. 방울을 키우면
        /// 그 축이 그만큼 줄어든다(실측 2.52획 -> 1.98획). 그래서 1.0획이 아니라 <b>1.5획</b>을 문턱으로
        /// 잡는다 — 규칙 1의 "구분돼야 하는 두 선"과 같은 값이다.</para>
        /// <para>함께 잠그는 것: 방울은 <b>원</b>이고 펜던트는 아니다. 앞선 라운드가 "원과 갈리는 것은
        /// 크기가 아니라 종횡비"라는 결론으로 펜던트를 세로로 뺐으므로, 방울이 그 축을 넘어
        /// 타원이 되면 두 아이템이 다시 같은 그림으로 수렴한다.</para>
        /// </summary>
        [Test]
        public void 방울을_키워도_펜던트와_갈린다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();

            float d = AccessorySilhouetteMetrics.MaxRadiusDelta(
                AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell),
                AccessorySilhouetteMetrics.Profile(rig, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckPendant));
            Assert.GreaterOrEqual(d, W * 1.5f,
                $"방울과 펜던트의 외곽 차이가 {d / W:F2}획입니다 — 방울을 규칙 1에 맞추려고 키우다가 " +
                "형제와 다시 붙었습니다. 키우는 대신 채우는 것이 이 수정의 요지입니다.");

            Vector2 bell = AccessorySilhouetteMetrics.ExtentInR(rig,
                AccessorySilhouetteMetrics.Find(
                    Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Bell").Points);
            float bellAspect = bell.y / bell.x;
            Assert.That(bellAspect, Is.EqualTo(1f).Within(0.2f),
                $"방울의 종횡비가 {bellAspect:F2}입니다 — 방울은 원으로 남아야 합니다. " +
                "세로로 길어지면 펜던트(2.21)의 식별 축을 침범합니다.");
        }

        /// <summary>규칙 3-2 · 규칙 5 — 보조색은 단 한 부분, 도형은 2~4개.
        /// <para>옛 방울은 공과 추(clapper)가 <b>둘 다</b> 보조색이었고, 추는 잉크 사각형이 0.29획이라
        /// 화면에 존재하지 않는 선이었다. 규칙 5가 그 경우를 명시한다 —
        /// "[선택] 디테일은 W 예산을 못 지키면 넣지 않는다".</para></summary>
        [Test]
        public void 방울_목걸이의_구성이_정원과_보조색_규칙을_지킨다()
        {
            List<AccessoryShapeBuilder.Shape> shapes = Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell);

            Assert.That(shapes.Count, Is.InRange(2, 4),
                $"방울 목걸이의 도형이 {shapes.Count}개입니다 — 정원은 2~4개입니다(37-6 규칙 5).");

            int accent = 0;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].Tone == AccessoryShapeBuilder.Accent) accent++;
            }
            Assert.AreEqual(1, accent,
                $"방울 목걸이의 보조색 도형이 {accent}개입니다 — 정확히 1개여야 합니다(37-6 규칙 3-2).");
        }

        /// <summary>설명문 "걸을 때마다 방울이 흔들린다"가 코드에 남아 있는가(원칙 1).
        /// <para>추를 지우면서 흔들 점 선언까지 같이 날아가는 것을 막는다 — 흔들 구간은
        /// 점 배열 전체를 덮어야 공이 통째로 흔들린다(일부만 흔들면 공이 찌그러진다).</para></summary>
        [Test]
        public void 방울은_통째로_흔들린다()
        {
            AccessoryShapeBuilder.Shape bell = AccessorySilhouetteMetrics.Find(
                Build(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell), "Bell");

            Assert.IsTrue(bell.HasSway,
                $"방울에 흔들 점이 없습니다 — 설명문(\"{ItemCatalog.Item(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell).Description}\")이 " +
                "주장하는 동작이 코드에 없습니다(원칙 1).");
            Assert.AreEqual(0, bell.SwayStart);
            Assert.AreEqual(bell.Points.Length, bell.SwayCount,
                "흔들 구간이 방울의 점 일부만 덮습니다 — 공의 한쪽만 움직이면 원이 찌그러집니다.");
        }
    }
}
