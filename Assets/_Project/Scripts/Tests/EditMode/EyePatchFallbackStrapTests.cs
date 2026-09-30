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
    /// 드러난 눈은 −0.62 R(왼쪽) — 외알안경과 같은 「오른쪽 착용」이다.</para>
    ///
    /// <para>★★ <b>2026-09-30 §16-8 — 끈이 「좌상 → 우하 한 가닥」이 됐다.</b> 사용자 신고
    /// *"안대 고정하는 끈 방향이 이상하고"* / *"왼쪽상단 오른쪽 하단이 맞지 않아?"*.
    /// 옛 끈은 두 끝이 <b>둘 다 뒤쪽</b>(111°·249°)이고 가운데 변이 천의 뒤변이라 끝에서 끝까지의 방향이
    /// <b>−90.0°(수직)</b>였고 카드에서 화살표 「&lt;」로 읽혔다. 지금은 위 끝 111° → 천의 <b>뒤위</b> 꼭짓점 →
    /// 천의 <b>앞아래</b> 꼭짓점 → 꼬리 <see cref="AccessoryShapeBuilder.PatchStrapTailDegrees"/>이고
    /// 끝에서 끝까지 <b>−48.7°</b>다. ⇒ 옛 판정 「끈 끝이 앞쪽(+x) 반원에 있으면 얼굴을 가로지른다」는
    /// <b>꼬리에 적용되지 않는다</b>. 꼬리가 넘어가면 안 되는 선은 「천보다 뒤」가 아니라
    /// <b>드러난 눈</b>이고, 그것은 아래에서 눈의 오른쪽 끝보다 앞(+x)인지로 잰다.</para>
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
            // ★ 꼬리는 위 끝의 거울이 아니다(§16-8) — 각도·반경을 각자의 상수에서 유도한다.
            Vector2 wantTail = Project(head, scale, AccessoryShapeBuilder.PatchStrapTailDegrees, AccessoryShapeBuilder.PatchStrapTailReachRatio);
            var gotTop = new Vector2(strap.Values[0], strap.Values[1]);
            var gotTail = new Vector2(strap.Values[6], strap.Values[7]);

            Assert.AreEqual(wantTop.x, gotTop.x, Tolerance, $"끈 위 끝 x: 에셋 {gotTop} / 상수 {AccessoryShapeBuilder.PatchStrapDegrees}°에서 유도 {wantTop}");
            Assert.AreEqual(wantTop.y, gotTop.y, Tolerance, $"끈 위 끝 y: 에셋 {gotTop} / 유도 {wantTop}");
            Assert.AreEqual(wantTail.x, gotTail.x, Tolerance, $"끈 꼬리 x: 에셋 {gotTail} / 상수 {AccessoryShapeBuilder.PatchStrapTailDegrees}°에서 유도 {wantTail}");
            Assert.AreEqual(wantTail.y, gotTail.y, Tolerance, $"끈 꼬리 y: 에셋 {gotTail} / 유도 {wantTail}");
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

        /// <summary>
        /// E-1 + §16-8 — 한쪽 가리개는 오른쪽(+x) 눈에, 끈은 <b>왼쪽 위에서 오른쪽 아래로</b> 한 가닥이고
        /// 드러난 눈 위를 지나가지 않는다.
        /// <para>★ 이 검사가 <b>사용자 신고 3건의 회귀 잠금</b>이다 — *"안대 고정하는 끈 방향이 이상하고"* /
        /// *"왼쪽상단 오른쪽 하단이 맞지 않아?"*. 방향을 눈이 아니라 <b>각도</b>로 잰다.</para>
        /// </summary>
        [Test]
        public void 안대_끈은_왼쪽_위에서_오른쪽_아래로_간다()
        {
            Assert.Greater(AccessoryShapeBuilder.PatchCenterRatio, 0f, "안대 천이 보는 사람 오른쪽(+x)에 있지 않습니다(E-1).");
            Assert.Greater(AccessoryShapeBuilder.PatchStrapDegrees, 90f, "끈 위 끝이 왼쪽 위(2사분면)가 아닙니다.");
            Assert.Less(AccessoryShapeBuilder.PatchStrapDegrees, 180f);
            Assert.Greater(AccessoryShapeBuilder.PatchStrapTailDegrees, 270f, "끈 꼬리가 오른쪽 아래(4사분면)가 아닙니다.");
            Assert.Less(AccessoryShapeBuilder.PatchStrapTailDegrees, 360f);

            var shapes = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Rig rig = AccessoryCardIcon.CardRig();
            AccessoryShapeBuilder.Append(shapes, EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesPatch, rig);
            AccessoryShapeBuilder.Shape cover = AccessorySilhouetteMetrics.Find(shapes, "PatchCover");
            AccessoryShapeBuilder.Shape strap = AccessorySilhouetteMetrics.Find(shapes, "PatchStrap");
            AccessoryShapeBuilder.Shape drawnEye = AccessorySilhouetteMetrics.Find(shapes, "PatchEye");

            Vector3 top = strap.Points[0];
            Vector3 tail = strap.Points[strap.Points.Length - 1];

            // ⑴ 위 끝은 천보다 뒤(−x)에 있고, ⑵ 꼬리는 천의 앞아래 꼭짓점 쪽으로 나간다.
            float coverMinX = float.MaxValue, coverMaxX = float.MinValue;
            foreach (Vector3 p in cover.Points)
            {
                coverMinX = Mathf.Min(coverMinX, p.x);
                coverMaxX = Mathf.Max(coverMaxX, p.x);
            }
            Assert.Less(top.x, coverMinX, "끈 위 끝이 천보다 앞에 있습니다 — 얼굴을 가로지릅니다.");
            Assert.Greater(tail.x, coverMinX, "끈 꼬리가 천보다 뒤로 갔습니다 — 좌상→우하 한 가닥이 아닙니다.");

            // ⑶ 끝에서 끝까지의 방향이 「오른쪽 아래」다. 옛 도형은 두 끝이 둘 다 뒤쪽이라 dx = 0(수직)이었다.
            float dx = tail.x - top.x, dy = tail.y - top.y;
            Assert.Greater(dx, 0f, "끈이 오른쪽으로 내려가지 않습니다 — 옛 「<」 화살표 모양이 돌아왔습니다.");
            Assert.Less(dy, 0f, "끈이 아래로 내려가지 않습니다.");
            float degrees = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            Assert.That(degrees, Is.InRange(-75f, -20f),
                $"끈 전체 방향이 {degrees:F1}°입니다 — 좌상→우하 대각선(−75°~−20°) 밖입니다.");

            // ⑷ 양성 대조 — 옛 아래 끝(360−111°, 1.02R)을 넣으면 위 판정이 실제로 빨개진다(비교기가 살아 있다).
            float oldRad = (360f - AccessoryShapeBuilder.PatchStrapDegrees) * Mathf.Deg2Rad;
            float oldTailX = rig.HeadRadius * Mathf.Cos(oldRad) * AccessoryShapeBuilder.PatchStrapReachRatio * rig.Facing;
            Assert.Less(oldTailX - top.x, 1e-4f,
                "옛 아래 끝이 지금도 오른쪽으로 갑니다 — 이 대조가 뜻이 없습니다(둘 다 뒤쪽이어야 옛 결함입니다).");

            // ⑸ 꼬리는 드러난 눈을 침범하지 않는다(눈은 반대쪽 −x에 있다).
            float eyeCx = 0f, eyeMaxX = float.MinValue;
            foreach (Vector3 p in drawnEye.Points)
            {
                eyeCx += p.x;
                eyeMaxX = Mathf.Max(eyeMaxX, p.x);
            }
            Assert.Less(eyeCx / drawnEye.Points.Length, 0f, "드러난 눈이 보는 사람 왼쪽(−x)에 있지 않습니다(E-1).");
            Assert.Greater(tail.x, eyeMaxX, "끈 꼬리가 드러난 눈 위로 넘어왔습니다.");
        }
    }
}
