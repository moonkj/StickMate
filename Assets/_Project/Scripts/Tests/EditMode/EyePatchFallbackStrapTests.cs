using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 안대 폴백 아이콘의 끈 각도 — 코드 상수에서 <b>재유도</b>해 에셋과 맞춘다 (2026-09-05, verify-change 커밋 차단 사유).
    ///
    /// <para>사고: R13-P2 가 몸의 끈 각도를 122° → 111°(<see cref="AccessoryShapeBuilder.PatchStrapDegrees"/>)로 내렸는데
    /// <c>equip_eyes_patch.asset</c>의 폴백 끈 끝점은 122° 그대로였다. 폴백은 몸의 단순화라 각도가 갈리면 「다른 그림」이다.</para>
    ///
    /// <para><b>재유도 방법 — 숫자를 베끼지 않는다.</b> 폴백 40×40 프레임(배율 s · 머리 중심)은 에셋 안의 <b>눈 원반</b>에서 역산한다:
    /// 눈은 몸에서 반지름 <see cref="AccessoryShapeBuilder.DrawnEyeRadiusRatio"/>·R, 중심 x = −<see cref="AccessoryShapeBuilder.DrawnEyeOffsetRatio"/>·R
    /// 이므로 에셋 눈의 지름이 곧 <c>2·r·s</c>, 눈 중심 + 오프셋·s 가 곧 머리 중심이다. 그 프레임에
    /// <c>HeadPolar(PatchStrapDegrees, PatchStrapReachRatio)</c>와 그 거울(360°−θ)을 놓으면 끈 끝점이 나온다.
    /// 상수가 바뀌면 기대값이 따라 바뀌고, 에셋이 안 따라오면 여기서 빨개진다.</para>
    ///
    /// <para><b>E-1 방향 점검</b>(§14-10-0): 안대 천은 <see cref="AccessoryShapeBuilder.PatchCenterRatio"/> = +0.62 R(보는 사람 오른쪽),
    /// 드러난 눈은 −0.62 R(왼쪽) — 외알안경과 같은 「오른쪽 착용」이다. 끈 두 끝은 머리 원 위 111°/249°(뒤쪽 위·아래)에서
    /// 천의 <b>뒤 두 꼭짓점</b>으로 들어오므로 얼굴(앞쪽 +x)을 가로지르지 않는다 — 아래 검사가 끝점 x 가 천보다 뒤임을 함께 본다.</para>
    /// </summary>
    public sealed class EyePatchFallbackStrapTests
    {
        private const float ViewBox = 40f;

        /// <summary>폴백 격자 한 칸의 1/20 — 에셋이 소수 둘째 자리라 그 반올림 여유다.</summary>
        private const float Tolerance = 0.05f;

        private static ItemIconPart[] Icon()
        {
            ItemIconPart[] icon = ItemCatalog.Item(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesPatch).Icon;
            Assert.IsNotNull(icon, "안대 폴백 아이콘이 없습니다.");
            Assert.AreEqual(3, icon.Length, "안대 폴백은 천·끈·눈 3조각입니다(몸 도형과 같은 순서).");
            return icon;
        }

        /// <summary>폴백 프레임(배율·머리 중심)을 에셋의 눈 원반에서 역산한다.</summary>
        private static void Frame(ItemIconPart eye, out float scale, out Vector2 headCenter)
        {
            Assert.IsTrue(eye.HasPoints, "폴백 눈이 다각형이 아닙니다.");
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            int n = eye.PointCount;
            for (int i = 0; i < n; i++)
            {
                float x = eye.Values[i * 2], y = eye.Values[i * 2 + 1];
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            float diameter = Mathf.Max(maxX - minX, maxY - minY);
            scale = diameter / (2f * AccessoryShapeBuilder.DrawnEyeRadiusRatio);           // 40격자 / R
            var eyeCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            // 눈은 머리 중심에서 −오프셋(보는 사람 왼쪽)에 있으므로 머리 중심은 눈의 오른쪽이다.
            headCenter = new Vector2(eyeCenter.x + AccessoryShapeBuilder.DrawnEyeOffsetRatio * scale, eyeCenter.y);
        }

        private static Vector2 Project(Vector2 headCenter, float scale, float degrees, float reachRatio)
        {
            float rad = degrees * Mathf.Deg2Rad;
            // 40 viewBox 는 y 가 아래로 자란다 — 몸의 +y 가 격자의 −y 다.
            return new Vector2(headCenter.x + Mathf.Cos(rad) * reachRatio * scale,
                headCenter.y - Mathf.Sin(rad) * reachRatio * scale);
        }

        [Test]
        public void 폴백_끈_끝점은_코드의_끈_각도에서_유도한_자리와_같다()
        {
            ItemIconPart[] icon = Icon();
            ItemIconPart strap = icon[1];
            ItemIconPart eye = icon[2];
            Assert.AreEqual(4, strap.PointCount, "폴백 끈은 4점(끝·천 뒤 꼭짓점 2·끝)입니다.");

            Frame(eye, out float scale, out Vector2 head);
            Assert.Greater(scale, 0f);

            Vector2 wantTop = Project(head, scale, AccessoryShapeBuilder.PatchStrapDegrees, AccessoryShapeBuilder.PatchStrapReachRatio);
            Vector2 wantBottom = Project(head, scale, 360f - AccessoryShapeBuilder.PatchStrapDegrees, AccessoryShapeBuilder.PatchStrapReachRatio);
            var gotTop = new Vector2(strap.Values[0], strap.Values[1]);
            var gotBottom = new Vector2(strap.Values[6], strap.Values[7]);

            Assert.AreEqual(wantTop.x, gotTop.x, Tolerance, $"끈 위 끝 x: 에셋 {gotTop} / 상수 {AccessoryShapeBuilder.PatchStrapDegrees}°에서 유도 {wantTop}");
            Assert.AreEqual(wantTop.y, gotTop.y, Tolerance, $"끈 위 끝 y: 에셋 {gotTop} / 유도 {wantTop}");
            Assert.AreEqual(wantBottom.x, gotBottom.x, Tolerance, $"끈 아래 끝 x: 에셋 {gotBottom} / 유도 {wantBottom}");
            Assert.AreEqual(wantBottom.y, gotBottom.y, Tolerance, $"끈 아래 끝 y: 에셋 {gotBottom} / 유도 {wantBottom}");
        }

        /// <summary>★ 양성 대조 — 옛 각도(122°)의 끝점은 같은 프레임에서 지금 에셋과 <b>확실히</b> 다르다(비교기가 살아 있다).</summary>
        [Test]
        public void 옛_각도로_유도하면_지금_에셋과_다르다()
        {
            ItemIconPart[] icon = Icon();
            Frame(icon[2], out float scale, out Vector2 head);
            const float oldDegrees = 122f;   // R13-P2 이전 값 — 박제(사고의 기록), 프로덕션 상수가 아니다.
            Assert.AreNotEqual(oldDegrees, AccessoryShapeBuilder.PatchStrapDegrees, "옛 각도가 지금 상수와 같으면 이 대조는 뜻이 없습니다.");
            Vector2 oldTop = Project(head, scale, oldDegrees, AccessoryShapeBuilder.PatchStrapReachRatio);
            var gotTop = new Vector2(icon[1].Values[0], icon[1].Values[1]);
            Assert.Greater(Vector2.Distance(oldTop, gotTop), Tolerance * 10f,
                "옛 각도에서 유도한 끝점이 지금 에셋과 거의 같습니다 — 에셋이 아직 122° 이거나 비교기가 무딥니다.");
        }

        /// <summary>E-1 — 한쪽 가리개는 오른쪽(+x) 눈에, 끈은 얼굴을 가로지르지 않는다(끝점이 천보다 뒤·머리 원 위).</summary>
        [Test]
        public void 안대는_오른쪽_착용이고_끈은_얼굴을_가로지르지_않는다()
        {
            Assert.Greater(AccessoryShapeBuilder.PatchCenterRatio, 0f, "안대 천이 보는 사람 오른쪽(+x)에 있지 않습니다(E-1).");
            Assert.Greater(AccessoryShapeBuilder.PatchStrapDegrees, 90f, "끈 끝이 앞쪽(+x) 반원에 있습니다 — 얼굴을 가로지릅니다.");
            Assert.Less(AccessoryShapeBuilder.PatchStrapDegrees, 180f);

            var shapes = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Rig rig = AccessoryCardIcon.CardRig();
            AccessoryShapeBuilder.Append(shapes, EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesPatch, rig);
            AccessoryShapeBuilder.Shape cover = AccessorySilhouetteMetrics.Find(shapes, "PatchCover");
            AccessoryShapeBuilder.Shape strap = AccessorySilhouetteMetrics.Find(shapes, "PatchStrap");
            AccessoryShapeBuilder.Shape drawnEye = AccessorySilhouetteMetrics.Find(shapes, "PatchEye");

            float coverMinX = float.MaxValue;
            foreach (Vector3 p in cover.Points) coverMinX = Mathf.Min(coverMinX, p.x);
            Assert.Less(strap.Points[0].x, coverMinX, "끈 위 끝이 천보다 앞에 있습니다 — 얼굴을 가로지릅니다.");
            Assert.Less(strap.Points[strap.Points.Length - 1].x, coverMinX, "끈 아래 끝이 천보다 앞에 있습니다 — 얼굴을 가로지릅니다.");

            float eyeCx = 0f;
            foreach (Vector3 p in drawnEye.Points) eyeCx += p.x;
            Assert.Less(eyeCx / drawnEye.Points.Length, 0f, "드러난 눈이 보는 사람 왼쪽(−x)에 있지 않습니다(E-1).");
        }
    }
}
