using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ R17d + design-motion §2-5/§2-6(2026-09-05) — 긴망토 밑단은 <b>기운 바닥선</b>에 눕고, 눌린 만큼 퍼지며, 눌린 점은 떨지 않는다.
    ///
    /// <para>처음 착지한 클램프는 스칼라 바닥(<c>p.y = max(p.y, −s·b)</c>)이었다. 웅크릴 때 상체가 피치 θ 로 함께 돌고 망토 컨테이너가 그 회전을
    /// 받으므로 화면의 수평 발바닥선은 컨테이너 안에서 <b>기운 선</b>이다 — 22° 에서 앞자락 3.00 pt 가 남은 채 뚫렸다(design-motion·persona-immersion
    /// 독립 확인). 이제 <see cref="AccessoryShapeBuilder.HemFloorLine"/>이 절편+기울기를 내고 <see cref="AccessoryShapeBuilder.PressHemToFloor"/>가 눕힌다.</para>
    ///
    /// <para><b>순수 기하 검사</b>(시간 없음). 피치는 <see cref="StickConfig"/>의 착지 무릎앉기 상체 피치(22°/30°)에서 읽고, 「최대 웅크림」은
    /// 물리 상한 = 엉덩이 높이(몸은 그보다 더 내려갈 수 없다)로 잡는다. 회전 부호는 렌더러와 같은 유도(회전의 right/up 기저 y 성분)를 쓰되
    /// <b>양쪽 부호 모두</b> 검사한다 — 어느 부호 규약이든 「루트 로컬 y ≥ 0」이 성립해야 식이 맞는 것이다. 숫자 사본 0.</para>
    /// </summary>
    public sealed class CapeHemFloorClampTests
    {
        private static AccessoryShapeBuilder.Rig Rig() => AccessorySilhouetteMetrics.Rig();

        /// <summary>발목선 = 루트 로컬 y 0(발바닥). 액세서리 좌표계의 원점 규약이다.</summary>
        private const float AnkleLocalY = 0f;

        private static Vector3[] BackPoints(in AccessoryShapeBuilder.Rig rig, int item)
        {
            List<AccessoryShapeBuilder.Shape> body = AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Shoulders, item);
            AccessoryShapeBuilder.Shape back = default;
            int found = 0;
            for (int i = 0; i < body.Count; i++)
            {
                if (!body[i].HasSway) continue;
                back = body[i];
                found++;
            }
            Assert.AreEqual(1, found, "망토 몸에 흔들 구간을 가진 조각(무대 뒤판)이 정확히 하나여야 합니다.");
            Assert.IsTrue(back.IsHandoff, "망토 뒤판이 인계본 조각이 아닙니다 — 이 검사의 전제(R17d 무대 도형)가 바뀌었습니다.");
            var copy = new Vector3[back.Points.Length];
            System.Array.Copy(back.Points, copy, copy.Length);
            return copy;
        }

        private static Vector3[] LongCape(in AccessoryShapeBuilder.Rig rig) => BackPoints(rig, AccessoryShapeBuilder.BackLongCape);

        private static float MinY(Vector3[] pts)
        {
            float m = float.MaxValue;
            for (int i = 0; i < pts.Length; i++) m = Mathf.Min(m, pts[i].y);
            return m;
        }

        private static float Width(Vector3[] pts)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < pts.Length; i++) { min = Mathf.Min(min, pts[i].x); max = Mathf.Max(max, pts[i].x); }
            return max - min;
        }

        /// <summary>렌더러와 같은 유도: 컨테이너 회전 R 의 기저 벡터 y 성분.</summary>
        private static void Pitch(float degrees, out float sin, out float cos)
        {
            Quaternion rot = Quaternion.Euler(0f, 0f, degrees);
            sin = (rot * Vector3.right).y;
            cos = (rot * Vector3.up).y;
        }

        /// <summary>컨테이너 점의 루트 로컬 y — 렌더러 배치식(컨테이너 = hip − R·hip + (0,b), 회전 R, 배율 1/s)에서 그대로.</summary>
        private static float RootLocalY(Vector3 p, float hipY, float s, float b, float sin, float cos)
            => hipY / s + sin * (p.x / s) + cos * (p.y / s - hipY / s) + b;

        private static float[] Pitches()
        {
            StickConfig config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                return new[] { config.landingCrouchTorsoPitchDegrees, config.landingCrouchTorsoPitchBraceDegrees };
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void 서_있을_때_긴망토_밑단은_발목선_위에_있다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float hem = MinY(LongCape(rig));
            Assert.Greater(hem, AnkleLocalY, $"긴망토 밑단({hem:F4})이 서 있는 자세에서 이미 발목선 아래입니다 — R17d 는 발목 잉크 원 윗변입니다.");
            Assert.Less(hem, rig.HeadRadius, $"긴망토 밑단({hem / rig.HeadRadius:F3} R)이 발목선에서 머리 반경보다 높습니다 — 발목 길이가 아닙니다.");
        }

        /// <summary>θ = 0 검산 — 기운 바닥선은 옛 스칼라식(−s·b)으로 접힌다(design-motion §2-5 「현행 식과 정확히 일치」).</summary>
        [Test]
        public void 피치가_0이면_바닥선은_옛_스칼라식과_같다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float b = -rig.HipY * 0.5f;
            Pitch(0f, out float sin, out float cos);
            AccessoryShapeBuilder.HemFloorLine(rig.HipY, 1f, b, sin, cos, out float intercept, out float slope);
            Assert.AreEqual(-b, intercept, 1e-5f, "θ=0 절편이 −s·b 가 아닙니다.");
            Assert.AreEqual(0f, slope, 1e-6f, "θ=0 기울기가 0 이 아닙니다.");

            Vector3[] a = LongCape(rig);
            Vector3[] c = LongCape(rig);
            int movedA = AccessoryShapeBuilder.ClampAboveFloor(a, -b);
            int movedC = AccessoryShapeBuilder.PressHemToFloor(c, intercept, slope, 0f);
            Assert.AreEqual(movedA, movedC, "θ=0 · spread 0 에서 옛 클램프와 새 눕히기의 받친 점 수가 다릅니다.");
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], c[i], $"{i}번 점: θ=0 · spread 0 이면 두 식이 같아야 합니다.");
        }

        /// <summary>음성 대조 — 서 있고(오프셋 0) 피치 0 이면 한 점도 안 움직인다.</summary>
        [Test]
        public void 몸_오프셋이_0이고_피치가_0이면_아무것도_바꾸지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            Vector3[] pts = LongCape(rig);
            var before = (Vector3[])pts.Clone();
            Pitch(0f, out float sin, out float cos);
            AccessoryShapeBuilder.HemFloorLine(rig.HipY, 1f, 0f, sin, cos, out float intercept, out float slope);
            Assert.AreEqual(0, AccessoryShapeBuilder.PressHemToFloor(pts, intercept, slope, AccessoryShapeBuilder.HemPressSpread), "몸이 내려앉지 않았는데 밑단이 움직였습니다.");
            for (int i = 0; i < pts.Length; i++) Assert.AreEqual(before[i], pts[i], $"{i}번 점이 바뀌었습니다.");
        }

        /// <summary>C5 — 상체 피치 22°/30°(양쪽 부호) × 웅크림(중간·물리 상한): 눕힌 뒤 <b>모든 점</b>의 루트 로컬 y ≥ 0(관통 없음), 눌린 점은 정확히 0.
        /// 양성 대조: 옛 스칼라식으로 눕히면 같은 자세에서 관통이 <b>남는다</b>(검출기가 살아 있다).</summary>
        [Test]
        public void 기운_바닥에서_눕힌_뒤_모든_점이_발바닥선_위이고_스칼라식은_관통을_남긴다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float hip = rig.HipY;
            float[] offsets = { -hip * 0.5f, -hip };
            int pressedTotal = 0, scalarLeaks = 0, cases = 0;
            foreach (float pitchDeg in Pitches())
            {
                Assert.Greater(pitchDeg, 0f, "착지 상체 피치가 0 이면 이 검사는 기운 바닥을 재지 않습니다.");
                foreach (float signed in new[] { pitchDeg, -pitchDeg })
                {
                    Pitch(signed, out float sin, out float cos);
                    foreach (float b in offsets)
                    {
                        cases++;
                        AccessoryShapeBuilder.HemFloorLine(hip, 1f, b, sin, cos, out float intercept, out float slope);
                        Vector3[] pts = LongCape(rig);
                        Vector3[] fresh = LongCape(rig);
                        int pressed = AccessoryShapeBuilder.PressHemToFloor(pts, intercept, slope, 0f);
                        pressedTotal += pressed;
                        for (int i = 0; i < pts.Length; i++)
                        {
                            float yRoot = RootLocalY(pts[i], hip, 1f, b, sin, cos);
                            Assert.GreaterOrEqual(yRoot, -1e-4f, $"피치 {signed}° 오프셋 {b:F3}: {i}번 점 루트 y {yRoot:F5} 가 발바닥선 아래입니다(관통).");
                            if (fresh[i] != pts[i]) Assert.AreEqual(0f, yRoot, 1e-4f, $"피치 {signed}° 오프셋 {b:F3}: 받친 {i}번 점이 발바닥선에 정확히 눕지 않았습니다.");
                        }

                        // 양성 대조 — 옛 스칼라식(θ=0 특수해)으로 같은 자세를 받치면 한쪽 자락이 뚫린 채 남는다.
                        Vector3[] scalar = LongCape(rig);
                        AccessoryShapeBuilder.ClampAboveFloor(scalar, -b);
                        for (int i = 0; i < scalar.Length; i++)
                            if (RootLocalY(scalar[i], hip, 1f, b, sin, cos) < -1e-3f) { scalarLeaks++; break; }
                    }
                }
            }
            Assert.Greater(cases, 0);
            Assert.Greater(pressedTotal, 0, "어느 자세에서도 받친 점이 없습니다 — 검사가 공허합니다.");
            Assert.Greater(scalarLeaks, 0, "옛 스칼라식이 어느 자세에서도 관통을 안 남깁니다 — 기운 바닥을 잴 필요가 없다는 뜻이거나 검출기가 죽었습니다.");
        }

        /// <summary>§2-6 (가) — 눌린 깊이만큼 퍼진다: 최대 웅크림에서 밑단 폭이 spread 0 보다 넓다(양성 대조), spread 0 이면 폭이 그대로다(음성 대조).</summary>
        [Test]
        public void 눌린_깊이만큼_밑단이_가로로_퍼진다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float b = -rig.HipY;
            Pitch(0f, out float sin, out float cos);
            AccessoryShapeBuilder.HemFloorLine(rig.HipY, 1f, b, sin, cos, out float intercept, out float slope);

            Vector3[] flat = LongCape(rig);
            Vector3[] spread = LongCape(rig);
            float widthAtRest = Width(flat);
            int movedFlat = AccessoryShapeBuilder.PressHemToFloor(flat, intercept, slope, 0f);
            int movedSpread = AccessoryShapeBuilder.PressHemToFloor(spread, intercept, slope, AccessoryShapeBuilder.HemPressSpread);
            Assert.AreEqual(movedFlat, movedSpread, "퍼짐은 받친 점 수를 바꾸지 않는다.");
            Assert.Greater(movedFlat, 0);
            Assert.AreEqual(widthAtRest, Width(flat), 1e-5f, "spread 0 이면 폭이 그대로여야 합니다(칼로 잘린 판자).");
            Assert.Greater(Width(spread), widthAtRest * 1.2f, $"spread {AccessoryShapeBuilder.HemPressSpread} 에서 밑단이 충분히 퍼지지 않았습니다({Width(spread) / widthAtRest:F2}배).");
            Assert.That(AccessoryShapeBuilder.HemPressSpread, Is.GreaterThan(0f).And.LessThan(1f), "spread 는 0(판자)과 1(길이 보존) 사이의 양식화 값이다(L-5).");
            for (int i = 0; i < spread.Length; i++)
                Assert.AreEqual(flat[i].y, spread[i].y, 1e-6f, "퍼짐은 y 를 건드리지 않는다(눕히는 높이는 같다).");
        }

        /// <summary>§2-6 (나) — 눌린 점의 흔들 진폭 감쇠: 안 눌린 점 1, 기준 깊이에서 0, 사이는 선형이고 단조 감소.</summary>
        [Test]
        public void 눌린_점의_흔들_감쇠는_깊이에_선형이고_기준_깊이에서_0이다()
        {
            float depthRef = 0.4f;
            Assert.AreEqual(1f, AccessoryShapeBuilder.HemPressDamping(0f, depthRef), 1e-6f);
            Assert.AreEqual(0.5f, AccessoryShapeBuilder.HemPressDamping(depthRef * 0.5f, depthRef), 1e-6f);
            Assert.AreEqual(0f, AccessoryShapeBuilder.HemPressDamping(depthRef, depthRef), 1e-6f);
            Assert.AreEqual(0f, AccessoryShapeBuilder.HemPressDamping(depthRef * 3f, depthRef), 1e-6f, "기준을 넘으면 0 에서 멈춘다(음수 진폭 금지).");
            float prev = 1f;
            for (int i = 1; i <= 10; i++)
            {
                float d = AccessoryShapeBuilder.HemPressDamping(depthRef * i / 10f, depthRef);
                Assert.LessOrEqual(d, prev + 1e-6f, "감쇠가 단조 감소가 아닙니다.");
                prev = d;
            }
            // 깊이는 바닥선에서 잰다 — 바닥 위의 점은 0.
            Assert.AreEqual(0f, AccessoryShapeBuilder.HemPressDepth(new Vector3(0f, 1f, 0f), 0.5f, 0f), 1e-6f);
            Assert.AreEqual(0.5f, AccessoryShapeBuilder.HemPressDepth(new Vector3(0f, 0f, 0f), 0.5f, 0f), 1e-6f);
            Assert.AreEqual(0.7f, AccessoryShapeBuilder.HemPressDepth(new Vector3(1f, 0f, 0f), 0.5f, 0.2f), 1e-6f, "기운 바닥선(slope)이 깊이에 들어간다.");
        }

        /// <summary>
        /// ★ 2026-09-06 perf-doc — <see cref="AccessoryShapeBuilder.HemCanReachFloor"/>가 <b>거짓이면
        /// <see cref="AccessoryShapeBuilder.PressHemToFloor"/>는 반드시 0</b>이다(한쪽으로만 안전한 판정).
        ///
        /// <para>이 성질이 <c>CharacterAccessoryRenderer.TickHemMotion</c>의 조기 반환을 정당화한다 —
        /// 깨지면 「관망 자세에서 밑단이 바닥을 뚫는데 아무것도 안 한다」가 된다.</para>
        ///
        /// <para><b>양성 대조를 함께 잠근다</b>: 판정이 무조건 거짓을 내면 이 검사는 공허하게 통과한다.
        /// 그래서 (가) 판정이 참인 자세가 실제로 있고 (나) 그때 실제로 눌리는지까지 센다.</para>
        /// </summary>
        [Test]
        public void 닿을_수_없다고_판정하면_한_점도_눌리지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float hip = rig.HipY;

            // 원본은 한 번만 굽는다(PressHemToFloor가 배열을 고치므로 매 판마다 복사해 쓴다).
            var items = new[] { AccessoryShapeBuilder.BackLongCape, AccessoryShapeBuilder.BackCape };
            var pristine = new Vector3[items.Length][];
            var work = new Vector3[items.Length][];
            for (int t = 0; t < items.Length; t++)
            {
                pristine[t] = BackPoints(rig, items[t]);
                work[t] = new Vector3[pristine[t].Length];
            }

            int falseCases = 0, trueCases = 0, trueAndPressed = 0;
            // 피치 −30~30° × 오프셋 0 ~ −엉덩이높이 — 정직한 직립부터 물리 상한 웅크림까지.
            for (int degTenth = -300; degTenth <= 300; degTenth += 5)
            {
                Pitch(degTenth / 10f, out float sin, out float cos);
                for (int k = 0; k <= 20; k++)
                {
                    float b = -hip * (k / 20f);
                    AccessoryShapeBuilder.HemFloorLine(hip, 1f, b, sin, cos, out float intercept, out float slope);

                    for (int t = 0; t < items.Length; t++)
                    {
                        System.Array.Copy(pristine[t], work[t], pristine[t].Length);
                        Vector3[] pts = work[t];

                        float minY = MinY(pts);
                        float maxAbsX = 0f;
                        for (int i = 0; i < pts.Length; i++) maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(pts[i].x));

                        bool canReach = AccessoryShapeBuilder.HemCanReachFloor(intercept, slope, minY, maxAbsX);
                        int pressed = AccessoryShapeBuilder.PressHemToFloor(pts, intercept, slope,
                            AccessoryShapeBuilder.HemPressSpread);

                        if (canReach)
                        {
                            trueCases++;
                            if (pressed > 0) trueAndPressed++;
                            continue;
                        }
                        falseCases++;
                        Assert.AreEqual(0, pressed,
                            $"피치 {degTenth / 10f}° 오프셋 {b:F4} item {items[t]}: 「닿을 수 없다」고 판정했는데 {pressed}점이 눌렸습니다 — 조기 반환이 그림을 지웁니다.");
                    }
                }
            }

            Assert.Greater(falseCases, 0, "「닿을 수 없다」가 한 번도 안 나왔습니다 — 이 검사가 아무것도 재지 않았습니다.");
            Assert.Greater(trueCases, 0, "「닿을 수 있다」가 한 번도 안 나왔습니다 — 판정이 무조건 거짓입니다(공허한 통과).");
            Assert.Greater(trueAndPressed, 0, "양성 대조: 「닿을 수 있다」인 자세 중 실제로 눌린 것이 하나도 없습니다.");
        }

        /// <summary>
        /// ★ 2026-09-06 perf-doc — <b>관망 자세(집중 세션 L0+L1)의 상시 미세 피치에서는 판정이 「닿을 수 없다」여야 한다.</b>
        /// 이것이 참이라야 조기 반환이 실제로 이득을 낸다(참이 아니면 최적화는 코드만 늘리고 아무것도 안 한다).
        ///
        /// <para>피치 범위는 <c>StickmanPoseAnimator</c>의 관망 자세 상수에서 <b>읽는다</b> — 숫자를 베끼면
        /// 그쪽이 바뀔 때 이 검사가 조용히 낡는다(협업 프로토콜: 프로덕션 상수를 테스트에 베끼지 않는다).
        /// 호흡 진폭도 <c>StickConfig</c>에서 읽는다.</para>
        /// </summary>
        [Test]
        public void 관망_자세_미세_피치에서는_밑단이_바닥선에_닿을_수_없다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float hip = rig.HipY;

            float maxLean = Mathf.Max(
                Mathf.Abs(StickmanPoseAnimator.FocusWatchStanceMaxLeanDegrees),
                Mathf.Abs(StickmanPoseAnimator.FocusWatchGestureMaxLeanDegrees));
            Assert.Greater(maxLean, 0f, "관망 자세의 기울임 상한이 0입니다 — 이 검사의 전제가 사라졌습니다.");

            float breath;
            StickConfig config = ScriptableObject.CreateInstance<StickConfig>();
            try { breath = config.idleBreathAmplitude; }
            finally { Object.DestroyImmediate(config); }
            Assert.Greater(breath, 0f, "호흡 진폭이 0입니다 — 이 검사가 최악을 못 잡습니다.");

            Vector3[] pts = LongCape(rig);   // 둘 중 밑단이 낮은 쪽(여유가 작은 쪽)만 재면 충분하다
            float minY = MinY(pts);
            float maxAbsX = 0f;
            for (int i = 0; i < pts.Length; i++) maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(pts[i].x));
            var work = new Vector3[pts.Length];

            int cases = 0;
            for (int degTenth = -Mathf.CeilToInt(maxLean * 10f); degTenth <= Mathf.CeilToInt(maxLean * 10f); degTenth++)
            {
                Pitch(degTenth / 10f, out float sin, out float cos);
                // 호흡 최저(몸이 가장 내려앉은 순간)가 최악이다.
                AccessoryShapeBuilder.HemFloorLine(hip, 1f, -breath, sin, cos, out float intercept, out float slope);
                cases++;
                Assert.IsFalse(AccessoryShapeBuilder.HemCanReachFloor(intercept, slope, minY, maxAbsX),
                    $"관망 자세 피치 {degTenth / 10f}° + 호흡 최저에서 「닿을 수 있다」가 나왔습니다 — 조기 반환이 안 걸립니다(최적화 무효).");
                System.Array.Copy(pts, work, pts.Length);
                Assert.AreEqual(0, AccessoryShapeBuilder.PressHemToFloor(work, intercept, slope,
                    AccessoryShapeBuilder.HemPressSpread), $"피치 {degTenth / 10f}°에서 실제로 눌렸습니다.");
            }
            Assert.Greater(cases, 1, "피치 스윕이 비었습니다.");

            // 음성 대조 — 착지 웅크림(몸 오프셋 큰 음수)에서는 반드시 「닿을 수 있다」가 나와야 한다.
            Pitch(0f, out float s0, out float c0);
            AccessoryShapeBuilder.HemFloorLine(hip, 1f, -hip, s0, c0, out float deepIntercept, out float deepSlope);
            Assert.IsTrue(AccessoryShapeBuilder.HemCanReachFloor(deepIntercept, deepSlope, minY, maxAbsX),
                "최대 웅크림에서도 「닿을 수 없다」입니다 — 판정이 무조건 거짓입니다.");
        }

        /// <summary>C6 — 짧은망토는 긴망토가 눌리는 얕은 웅크림에서 한 점도 안 눌린다(양성 대조: 같은 자세에서 긴망토는 눌린다).</summary>
        [Test]
        public void 짧은망토는_긴망토가_눌리는_얕은_웅크림에서_눌리지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float longMargin = MinY(LongCape(rig)) - AnkleLocalY;
            float b = -(longMargin * 2f);   // 긴망토 밑단 여유의 두 배만큼 내려앉음 — 긴망토는 눌리고 짧은망토(여유 ≫)는 안 눌린다
            Pitch(0f, out float sin, out float cos);
            AccessoryShapeBuilder.HemFloorLine(rig.HipY, 1f, b, sin, cos, out float intercept, out float slope);
            Vector3[] longPts = LongCape(rig);
            Vector3[] shortPts = BackPoints(rig, AccessoryShapeBuilder.BackCape);
            Assert.Greater(AccessoryShapeBuilder.PressHemToFloor(longPts, intercept, slope, AccessoryShapeBuilder.HemPressSpread), 0, "양성 대조: 긴망토가 안 눌립니다.");
            Assert.AreEqual(0, AccessoryShapeBuilder.PressHemToFloor(shortPts, intercept, slope, AccessoryShapeBuilder.HemPressSpread), "짧은망토가 눌렸습니다 — 길이 2단의 값어치(§2-7)가 사라집니다.");
        }
    }
}
