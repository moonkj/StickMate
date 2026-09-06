using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 집중 세션 앰비언트 3층 + 팔짱 기하 재설계(2026-09-06,
    /// docs/UX_MOTION_FOCUS_SESSION.md). 페르소나(소은) 지적 <i>"집중모드 25분 세션의 99.87%가
    /// 평소와 똑같다"</i>에 대한 구현을 잠근다.
    ///
    /// <para><b>이 파일이 재는 것 다섯 가지</b>
    /// <list type="number">
    ///   <item><b>팔짱의 부호</b> — 재설계의 전부다. 옛 값은 «가장 앞에 나온 손 = 가장 낮은 손»이라
    ///     기하학적으로 <b>반드시</b> "허리 앞에 손 모으기"가 됐다. 손 물림의 <b>부호</b>가 뒤집혔는지를
    ///     프리팹 실측 + 프로덕션 상수로 정방향 계산(FK)해서 본다.</item>
    ///   <item><b>안경 손끝의 돌출</b> — 옛 파라미터는 배율 0.75에서 0.13pt라 손이 머리에 묻혔다.
    ///     네 배율 전부에서 머리 실루엣 밖으로 나오는지 본다(네거티브 컨트롤 포함).</item>
    ///   <item><b>어휘 분리</b> — 세션 안/밖에서 어휘가 섞이지 않는가, 그리고 커서를 못 읽으면
    ///     G3가 추첨에서 실제로 빠지는가(원칙 1).</item>
    ///   <item><b>박자</b> — 25분 세션에서 걷기가 줄되 <b>사라지지 않고</b>, 제스처 침묵이 40초를
    ///     넘지 않는가. 전부 <b>벽시계 초</b> 기준이다(프레임 수 예산 금지 — CLAUDE.md).</item>
    ///   <item><b>마스터 스위치</b> — 끄면 세션 중에도 100% 평소 거동인가(네거티브 컨트롤 규약).</item>
    /// </list></para>
    ///
    /// <para><b>숫자를 베끼지 않는다</b>: 각도는 <see cref="StickmanPoseAnimator"/>의 public const와
    /// 솔버에서, 치수는 <b>실제로 구워진 프리팹</b>에서, 확률/시간은 <see cref="StickConfig"/>와
    /// <see cref="FocusAmbientGestures"/>에서 읽는다. 이 파일에 적힌 숫자는 <b>합격선</b>뿐이다.</para>
    ///
    /// <para><b>플랫폼</b>: 중립. 자산을 <b>읽기만</b> 한다(절대 불변 원칙 3).</para>
    /// </summary>
    public sealed class FocusSessionAmbientTests
    {
        private const string LogPrefix = "[집중앰비언트]";
        private const string PrefabAssetPath = "Assets/_Project/Prefabs/Stickman.prefab";
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        // ====================================================================
        // 리그 실측 — 프리팹에서 읽는다(값을 옮겨 적지 않는다)
        // ====================================================================

        private struct Rig
        {
            public Vector2 Shoulder;      // 어깨 부착점(루트 로컬)
            public float ArmUpper;        // 상완 길이
            public float ArmLower;        // 전완 길이
            public float HipY;            // 엉덩이 y(몸통비의 분모 시작점)
            public float HeadCenterY;     // 머리 중심 y
            public float HeadVisualRadius;// 머리 <b>시각</b> 반경(외곽선 폴리라인 실측)
            public float ArmStrokeWidth;  // 팔 획 두께
            public float BakedScale;      // 프리팹이 구워진 배율

            public float Reach => ArmUpper + ArmLower;
            public float TorsoLength => Shoulder.y - HipY;
        }

        private static Rig LoadRig()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            Assert.IsNotNull(prefab, $"{LogPrefix} 프리팹을 찾지 못했습니다: {PrefabAssetPath}");

            Transform arm = prefab.transform.Find("LeftArm");
            Transform armLower = arm != null ? arm.Find("LeftArmLower") : null;
            Transform leg = prefab.transform.Find("LeftLeg");
            Transform head = prefab.transform.Find("Head");
            Assert.IsNotNull(arm, $"{LogPrefix} 프리팹에 LeftArm이 없습니다.");
            Assert.IsNotNull(armLower, $"{LogPrefix} 프리팹에 LeftArmLower가 없습니다.");
            Assert.IsNotNull(leg, $"{LogPrefix} 프리팹에 LeftLeg이 없습니다.");
            Assert.IsNotNull(head, $"{LogPrefix} 프리팹에 Head가 없습니다.");

            // 프로덕션(StickmanPoseAnimator.BuildSegment)이 읽는 것과 **같은 출처**를 쓴다.
            var shoulderJoint = arm.GetComponent<HingeJoint2D>();
            var hipJoint = leg.GetComponent<HingeJoint2D>();
            var upperBox = arm.GetComponent<BoxCollider2D>();
            var lowerBox = armLower.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(upperBox, $"{LogPrefix} LeftArm에 BoxCollider2D가 없습니다.");
            Assert.IsNotNull(lowerBox, $"{LogPrefix} LeftArmLower에 BoxCollider2D가 없습니다.");

            var rig = new Rig
            {
                Shoulder = shoulderJoint != null ? shoulderJoint.connectedAnchor : (Vector2)arm.localPosition,
                ArmUpper = upperBox.size.y,
                ArmLower = lowerBox.size.y,
                HipY = hipJoint != null ? hipJoint.connectedAnchor.y : leg.localPosition.y,
                HeadCenterY = head.localPosition.y,
                HeadVisualRadius = MeasureHeadVisualRadius(head),
                ArmStrokeWidth = ReadArmStrokeWidth(arm),
                BakedScale = MeasureBakedScale(prefab),
            };

            Assert.Greater(rig.Reach, 0f, $"{LogPrefix} 팔 길이를 못 읽었습니다.");
            Assert.Greater(rig.TorsoLength, 0f, $"{LogPrefix} 몸통 길이(어깨 − 엉덩이)가 0 이하입니다.");
            Assert.Greater(rig.HeadVisualRadius, 0f, $"{LogPrefix} 머리 시각 반경을 못 읽었습니다.");
            return rig;
        }

        /// <summary>머리 <b>시각</b> 반경 — 물리 콜라이더(더 크다)가 아니라 <b>실제로 그려지는</b>
        /// 외곽선 폴리라인에서 잰다. 손끝이 가려지는지는 그림이 정하지 물리가 정하지 않는다.</summary>
        private static float MeasureHeadVisualRadius(Transform head)
        {
            float radius = 0f;
            foreach (LineRenderer line in head.GetComponentsInChildren<LineRenderer>(true))
            {
                for (int i = 0; i < line.positionCount; i++)
                {
                    Vector3 p = line.GetPosition(i);
                    radius = Mathf.Max(radius, new Vector2(p.x, p.y).magnitude);
                }
            }
            return radius;
        }

        private static float ReadArmStrokeWidth(Transform arm)
        {
            var line = arm.GetComponent<LineRenderer>();
            return line != null ? line.startWidth : 0f;
        }

        /// <summary>프리팹이 구워진 배율 — <see cref="StickmanMetrics.Scale"/>과 같은 식.</summary>
        private static float MeasureBakedScale(GameObject prefab)
        {
            float height = 0f;
            foreach (CapsuleCollider2D c in prefab.GetComponents<CapsuleCollider2D>())
            {
                if (c != null && !c.isTrigger) height = Mathf.Max(height, c.size.y);
            }
            Assert.Greater(height, 0f, $"{LogPrefix} 프리팹에서 전신 높이를 못 읽었습니다.");
            return height / StickConfig.BaselineCharacterTotalHeight;
        }

        /// <summary>어깨/팔꿈치 각도로부터 손끝 좌표를 낸다(정방향 운동학).
        /// 각도 규약은 <see cref="StickmanPoseAnimator"/>와 같다: 0 = 곧게 아래, 마디 끝 방향 =
        /// (sinθ, −cosθ). 아래 마디는 위 마디의 <b>자식</b>이므로 절대각 = θu + θl이다
        /// (<c>HangHandReachAboveRoot</c>가 쓰는 것과 같은 합성).</summary>
        private static void ForwardKinematics(in Rig rig, float upperDegrees, float lowerDegrees,
            out Vector2 elbow, out Vector2 hand)
        {
            float u = upperDegrees * Mathf.Deg2Rad;
            elbow = rig.Shoulder + new Vector2(rig.ArmUpper * Mathf.Sin(u), -rig.ArmUpper * Mathf.Cos(u));
            float l = (upperDegrees + lowerDegrees) * Mathf.Deg2Rad;
            hand = elbow + new Vector2(rig.ArmLower * Mathf.Sin(l), -rig.ArmLower * Mathf.Cos(l));
        }

        /// <summary>몸통비 — 0.00 = 엉덩이, 1.00 = 어깨. 0.50이 허리, 0.75가 가슴이다.</summary>
        private static float TorsoRatio(in Rig rig, Vector2 point) => (point.y - rig.HipY) / rig.TorsoLength;

        // ====================================================================
        // (1) ★★ 팔짱 — 이번 재설계의 전부인 «부호»
        // ====================================================================

        /// <summary>
        /// V1 — 최전방 손이 <b>가슴 높이</b>이고 아래 팔이 그 <b>안쪽으로</b> 들어간다.
        ///
        /// <para>옛 값(어깨 −14 ∓ 5.5 / 팔꿈치 104 ± 11)에서는 물림이 <b>음수</b>였다: 가장 앞에 나온
        /// 손이 가장 낮은 손이라 정의상 "허리 앞에 두 손 모으기"다. 페르소나가 본 것은 튜닝 오차가
        /// 아니라 그 부호였다.</para>
        /// </summary>
        [Test]
        public void 팔짱은_최전방_손이_가슴_높이이고_아래_팔이_안쪽으로_물린다()
        {
            Rig rig = LoadRig();

            StickmanPoseAnimator.ResolveCrossArmAngles(1f, out float frontUpper, out float frontElbow);
            StickmanPoseAnimator.ResolveCrossArmAngles(-1f, out float backUpper, out float backElbow);
            ForwardKinematics(rig, frontUpper, frontElbow, out Vector2 frontElbowPos, out Vector2 frontHand);
            ForwardKinematics(rig, backUpper, backElbow, out Vector2 backElbowPos, out Vector2 backHand);

            float mesh = frontHand.x - backHand.x;
            float frontRatio = TorsoRatio(rig, frontHand);
            float backRatio = TorsoRatio(rig, backHand);
            float forearmAngleGap = Mathf.Abs((frontUpper + frontElbow) - (backUpper + backElbow));

            Debug.Log($"{LogPrefix} 팔짱 실측(배율 {rig.BakedScale:F2} 프리팹) — " +
                $"A 어깨 {frontUpper:F1}° 팔꿈치 {frontElbow:F1}° 손 몸통비 {frontRatio:F3} / " +
                $"B 어깨 {backUpper:F1}° 팔꿈치 {backElbow:F1}° 손 몸통비 {backRatio:F3}. " +
                $"손 물림(위손x − 아래손x) = {mesh:F4}(팔 획의 {mesh / rig.ArmStrokeWidth:F2}배), " +
                $"두 전완 절대각 차 = {forearmAngleGap:F1}°. " +
                $"팔꿈치 좌표 A{frontElbowPos} / B{backElbowPos}.");

            Assert.Greater(mesh, 0f,
                $"{LogPrefix} 손 물림이 {mesh:F4}로 음수입니다 — 가장 앞에 나온 손이 가장 낮은 손이라는 뜻이고, " +
                "그 배치에서는 각도를 어떻게 흔들어도 «허리 앞에 두 손을 모은» 그림밖에 안 나옵니다" +
                "(2026-09-06 이전의 결함 그 자체). StickmanPoseAnimator의 스태거 부호를 확인하십시오.");

            // 물림이 팔 획 하나보다 작으면 화면에서 두 손이 겹쳐 하나로 보인다.
            Assert.Greater(mesh, rig.ArmStrokeWidth,
                $"{LogPrefix} 손 물림 {mesh:F4}가 팔 획 두께 {rig.ArmStrokeWidth:F4}보다 작습니다 — " +
                "두 손이 화면에서 한 덩어리로 뭉칩니다.");

            // 합격선 0.65 = "가슴(0.75)에 가깝다"의 설계 하한(문서 3-4 판정 지표). 허리는 0.50이다.
            Assert.GreaterOrEqual(frontRatio, 0.65f,
                $"{LogPrefix} 최전방 손의 몸통비가 {frontRatio:F3}입니다(합격선 0.65, 허리=0.50 / 가슴=0.75) — " +
                "이 높이면 «팔짱»이 아니라 «손 모으기»로 읽힙니다.");

            Assert.Greater(frontRatio, backRatio,
                $"{LogPrefix} 최전방 손({frontRatio:F3})이 아래 팔의 손({backRatio:F3})보다 높지 않습니다 — " +
                "«위 팔이 최전방»이라는 이 자세의 유일한 판별 신호가 사라집니다.");

            // 두 전완이 나란하면 "팔 두 개"가 아니라 "굵은 선 하나"로 읽힌다(옛 값은 11°였다).
            Assert.Greater(forearmAngleGap, 20f,
                $"{LogPrefix} 두 전완의 절대각 차가 {forearmAngleGap:F1}°뿐입니다 — 두 선이 거의 나란해 " +
                "한 팔만 그린 것처럼 보입니다.");
        }

        /// <summary>
        /// V9 — 팔 배정. <c>NeutralSign &gt; 0</c>(Idle 중립에서 <b>앞쪽</b>에 있는 팔)이 A(최전방)여야
        /// 한다. 반대로 배정하면 결과 그림은 같아 보여도 <b>전이 이동량이 2.1배</b>(51.5° → 108.5°)라
        /// 팔이 두 배로 요란하게 휘둘린다.
        /// </summary>
        [Test]
        public void 팔짱_배정은_앞쪽_팔이_A다()
        {
            StickmanPoseAnimator.ResolveCrossArmAngles(1f, out float frontUpper, out float frontElbow);
            StickmanPoseAnimator.ResolveCrossArmAngles(-1f, out float backUpper, out float backElbow);

            Assert.AreEqual(StickmanPoseAnimator.FocusCrossFrontArmUpperDegrees, frontUpper, 1e-4f,
                $"{LogPrefix} NeutralSign>0 팔의 어깨각이 A(최전방)의 값이 아닙니다.");
            Assert.AreEqual(StickmanPoseAnimator.FocusCrossFrontElbowDegrees,
                Mathf.Abs(frontElbow), 1e-4f,
                $"{LogPrefix} NeutralSign>0 팔의 팔꿈치가 A의 값이 아닙니다.");
            Assert.AreEqual(StickmanPoseAnimator.FocusCrossBackArmUpperDegrees, backUpper, 1e-4f,
                $"{LogPrefix} NeutralSign<0 팔의 어깨각이 B(안쪽)의 값이 아닙니다.");
            Assert.AreEqual(StickmanPoseAnimator.FocusCrossBackElbowDegrees,
                Mathf.Abs(backElbow), 1e-4f,
                $"{LogPrefix} NeutralSign<0 팔의 팔꿈치가 B의 값이 아닙니다.");

            Assert.Greater(frontUpper, backUpper,
                $"{LogPrefix} 앞쪽 팔의 어깨각({frontUpper:F1}°)이 뒤쪽 팔({backUpper:F1}°)보다 뒤에 있습니다 — " +
                "스태거 부호가 뒤집혔습니다(옛 코드가 정확히 이 상태였고, 그것이 결함의 원인이었습니다).");

            // 역할 교대(G1 안착)는 두 팔의 값을 정확히 맞바꾸기만 해야 한다 — 새 각도가 생기면 안 된다.
            StickmanPoseAnimator.ResolveCrossArmAngles(1f, out float swappedUpper, out float swappedElbow,
                rolesSwapped: true);
            Assert.AreEqual(backUpper, swappedUpper, 1e-4f,
                $"{LogPrefix} 역할 교대 후 앞쪽 팔이 B의 어깨각을 받지 않았습니다.");
            Assert.AreEqual(backElbow, swappedElbow, 1e-4f,
                $"{LogPrefix} 역할 교대 후 앞쪽 팔이 B의 팔꿈치를 받지 않았습니다.");

            float travel = Mathf.Abs(frontUpper - backUpper);
            Debug.Log($"{LogPrefix} 팔 배정 확인 — A(앞) 어깨 {frontUpper:F1}° / B(뒤) 어깨 {backUpper:F1}°, " +
                $"두 팔의 어깨각 차 {travel:F1}°. 배정을 뒤집으면 전이 이동량이 2.1배가 됩니다.");
        }

        /// <summary>
        /// 뒷짐(P2) — <b>손이 몸통선 뒤</b>에 있고, 두 전완이 완전히 겹치지 않으며, 어깨가 관절 한계
        /// 안이고, 팔꿈치가 팔짱보다 얕다(= 크리즈 여유가 더 크다).
        ///
        /// <para>★ <b>두 손이 «떨어져» 있는지는 재지 않는다</b> — 뒷짐은 정의상 등 뒤에서 손을 모으는
        /// 자세이고 실측 간격도 획 하나 안(1.6pt)이다. 스태거의 목적은 손을 벌리는 것이 아니라
        /// <b>두 전완 선이 정확히 포개져 팔이 하나로 보이는 것</b>을 막는 것뿐이다. 그래서 각도 차를
        /// 잰다. 여기서 "손이 획 하나 이상 떨어져야 한다"고 쓰면 뒷짐이 아닌 자세를 요구하게 된다.</para>
        /// </summary>
        [Test]
        public void 뒷짐은_손이_몸통_뒤에_있고_두_전완이_포개지지_않는다()
        {
            Rig rig = LoadRig();

            StickmanPoseAnimator.ResolveWatchStanceArmAngles(true, 1f, false,
                out float frontUpper, out float frontElbow);
            StickmanPoseAnimator.ResolveWatchStanceArmAngles(true, -1f, false,
                out float backUpper, out float backElbow);
            ForwardKinematics(rig, frontUpper, frontElbow, out Vector2 frontElbowPos, out Vector2 frontHand);
            ForwardKinematics(rig, backUpper, backElbow, out Vector2 backElbowPos, out Vector2 backHand);

            Assert.Less(frontHand.x, rig.Shoulder.x,
                $"{LogPrefix} 뒷짐인데 손이 몸통 앞({frontHand.x:F4})에 있습니다 — 뒷짐이 아닙니다.");
            Assert.Less(backHand.x, rig.Shoulder.x,
                $"{LogPrefix} 뒷짐인데 반대쪽 손이 몸통 앞({backHand.x:F4})에 있습니다.");
            Assert.Less(frontElbowPos.x, rig.Shoulder.x,
                $"{LogPrefix} 뒷짐인데 팔꿈치가 몸통선 뒤로 나가지 않았습니다 — 상완이 몸통 획에 묻혀 " +
                "«팔이 없는» 그림이 됩니다.");

            float forearmGap = Mathf.Abs((frontUpper + frontElbow) - (backUpper + backElbow));
            Assert.Greater(forearmGap, 2f,
                $"{LogPrefix} 두 전완의 절대각 차가 {forearmGap:F2}°뿐입니다 — 두 선이 포개져 팔이 " +
                "하나로 보입니다(스태거가 사라졌습니다).");

            float backLimit = ReadBootstrapperConst("ShoulderSwingBackLimitDegrees");
            float deepest = Mathf.Max(Mathf.Abs(frontUpper), Mathf.Abs(backUpper));
            Assert.Less(deepest, backLimit,
                $"{LogPrefix} 뒷짐의 어깨각 {deepest:F1}°가 관절 뒤쪽 한계 {backLimit:F0}°를 넘습니다 — " +
                "랙돌로 넘어가는 순간 팔이 한계까지 튕겨 돌아옵니다.");

            Assert.Less(Mathf.Abs(frontElbow), StickmanPoseAnimator.FocusCrossFrontElbowDegrees,
                $"{LogPrefix} 뒷짐의 팔꿈치가 팔짱보다 깊습니다 — 크리즈 여유가 더 나빠집니다.");

            Debug.Log($"{LogPrefix} 뒷짐 실측 — 앞팔 어깨 {frontUpper:F1}° 팔꿈치 {Mathf.Abs(frontElbow):F1}° " +
                $"손 {frontHand}, 뒤팔 어깨 {backUpper:F1}° 팔꿈치 {Mathf.Abs(backElbow):F1}° 손 {backHand}, " +
                $"팔꿈치 {frontElbowPos}/{backElbowPos}. 손 몸통비 {TorsoRatio(rig, frontHand):F3}, " +
                $"두 전완 절대각 차 {forearmGap:F1}°, 어깨 뒤쪽 한계 {backLimit:F0}° 대비 {deepest:F1}°.");
        }

        /// <summary>SceneBootstrapper(Editor 어셈블리 private)의 상수를 소스에서 읽는다 —
        /// <c>LimbCurveGeometryTests.ReadBootstrapperConst</c>와 같은 어법이다(값을 옮겨 적지 않는다).</summary>
        private static float ReadBootstrapperConst(string name)
        {
            string path = System.IO.Path.Combine(Application.dataPath, "Editor", "SceneBootstrapper.cs");
            Assert.IsTrue(System.IO.File.Exists(path), $"{LogPrefix} SceneBootstrapper.cs를 찾지 못했습니다: {path}");
            string src = System.IO.File.ReadAllText(path);
            var m = System.Text.RegularExpressions.Regex.Match(src,
                @"const\s+float\s+" + System.Text.RegularExpressions.Regex.Escape(name) + @"\s*=\s*([-\d.]+)f");
            Assert.IsTrue(m.Success, $"{LogPrefix} SceneBootstrapper.cs에서 상수 {name}을 못 읽었습니다.");
            return Mathf.Abs(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        // ====================================================================
        // (2) ★ 안경 밀어올리기 — "안경이 없다"가 아니라 "손이 없다"였다
        // ====================================================================

        /// <summary>
        /// V3 — 손끝이 <b>모든 배율에서</b> 머리 실루엣 밖으로 나온다.
        /// 머리는 불투명이고 팔보다 <b>위</b>에 그려지므로(HeadFill/HeadOutline의 sortingOrder가 더 크다),
        /// 실루엣 안에 있는 손끝은 화면에 존재하지 않는 것과 같다.
        /// </summary>
        [Test]
        public void 안경_손끝이_모든_배율에서_머리_실루엣_밖으로_나온다()
        {
            Rig rig = LoadRig();

            StickmanPoseAnimator.SolveFocusGlassesAngles(rig.Shoulder, rig.ArmUpper, rig.ArmLower,
                rig.HeadCenterY, out float upper, out float elbow);
            ForwardKinematics(rig, upper, elbow, out _, out Vector2 hand);

            var headCenter = new Vector2(rig.Shoulder.x, rig.HeadCenterY);
            float distance = Vector2.Distance(hand, headCenter);
            float protrusionBaked = distance - rig.HeadVisualRadius;

            // 각도는 배율에 무관하고 기하는 배율에 선형이다 — 구워진 배율의 돌출을 배율비로 환산한다.
            var scales = new List<float> { StickConfig.MinCharacterScale, 0.60f, rig.BakedScale, StickConfig.MaxCharacterScale };
            float worstPt = float.PositiveInfinity;
            float worstScale = 0f;
            foreach (float scale in scales)
            {
                float pt = protrusionBaked * (scale / rig.BakedScale) * StickConfig.ReferencePointsPerWorldUnitApprox;
                if (pt < worstPt) { worstPt = pt; worstScale = scale; }
            }

            Debug.Log($"{LogPrefix} 안경 밀어올리기 — 어깨 {upper:F1}° / 팔꿈치 {Mathf.Abs(elbow):F1}°, " +
                $"손끝이 머리 중심에서 {distance:F4}(반경 {rig.HeadVisualRadius:F4}) = 밖으로 {protrusionBaked:F4}유닛. " +
                $"화면상 최소 돌출 {worstPt:F2}pt(배율 {worstScale:F2}).");

            Assert.Greater(protrusionBaked, 0f,
                $"{LogPrefix} 손끝이 머리 실루엣 <b>안</b>에 있습니다({protrusionBaked:F4}유닛) — " +
                "머리는 불투명이고 팔보다 위에 그려지므로 손이 화면에 나오지 않습니다.");

            // 합격선 0.5pt: 획 하한(2pt)의 1/4. 이보다 작으면 안티에일리어싱에 먹힌다.
            Assert.Greater(worstPt, 0.5f,
                $"{LogPrefix} 배율 {worstScale:F2}에서 손끝 돌출이 {worstPt:F2}pt뿐입니다 — " +
                "MOTION_SPEC 13절이 손차양에서 잡아낸 것과 같은 병(손이 머리에 묻힘)입니다.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>옛 파라미터</b>(앞 0.30 · 높이 고정)를 같은 검사에 넣으면 실제로 걸린다.
        /// 이것이 없으면 위 테스트는 "무엇을 넣어도 통과하는" 검사와 구분되지 않는다.
        /// </summary>
        [Test]
        public void 네거티브_컨트롤_옛_안경_파라미터는_같은_검사에_걸린다()
        {
            Rig rig = LoadRig();

            // 옛 목표점: x = 팔 길이 × 0.30, y = 머리 중심 <b>고정</b>(비율 없음).
            const float legacyForwardRatio = 0.30f;
            var legacyTarget = new Vector2(rig.Shoulder.x + rig.Reach * legacyForwardRatio, rig.HeadCenterY);
            float legacyDistance = Vector2.Distance(legacyTarget, new Vector2(rig.Shoulder.x, rig.HeadCenterY));
            float legacyPt = (legacyDistance - rig.HeadVisualRadius)
                * StickConfig.ReferencePointsPerWorldUnitApprox;

            Assert.Less(legacyPt, 0.5f,
                $"{LogPrefix} 옛 파라미터가 합격선을 통과했습니다({legacyPt:F2}pt) — 이 검사가 아무것도 " +
                "잡아내지 못한다는 뜻입니다(리그 치수가 바뀌었거나 합격선이 틀렸습니다).");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 옛 파라미터(앞 {legacyForwardRatio:F2}, 높이 고정)의 " +
                $"손끝 돌출은 배율 {rig.BakedScale:F2}에서 {legacyPt:F2}pt뿐이었습니다(합격선 0.5pt).");
        }

        // ====================================================================
        // (3) ★ 어휘 — 세션 안/밖이 섞이지 않는다 + G3 폴백(원칙 1)
        // ====================================================================

        [Test]
        public void 제스처_가중치_합이_정확히_1이다()
        {
            Assert.AreEqual(1f, FocusAmbientGestures.WeightSum, 1e-5f,
                $"{LogPrefix} 가중치 합이 {FocusAmbientGestures.WeightSum:F4}입니다 — 1이 아니면 " +
                "마지막 갈래(G4)가 남은 구간을 통째로 흡수해 설계 분포가 조용히 찌그러집니다.");
        }

        /// <summary>
        /// ★ 커서를 못 읽으면 G3가 추첨에서 <b>실제로</b> 빠진다(원칙 1: 없는 대상을 향해 돌아보지 않는다).
        /// 그리고 빠진 몫은 G1이 흡수해 <b>어떤 롤도 갈래를 잃지 않는다</b>.
        /// </summary>
        [Test]
        public void 커서를_못_읽으면_G3가_추첨에서_빠지고_G1이_그_몫을_받는다()
        {
            const int samples = 20000;
            var withCursor = new Dictionary<WanderAmbientMotion, int>();
            var withoutCursor = new Dictionary<WanderAmbientMotion, int>();

            for (int i = 0; i < samples; i++)
            {
                double roll = (i + 0.5) / samples;
                Bump(withCursor, FocusAmbientGestures.Draw(roll, cursorAvailable: true));
                Bump(withoutCursor, FocusAmbientGestures.Draw(roll, cursorAvailable: false));
            }

            Assert.AreEqual(4, withCursor.Count,
                $"{LogPrefix} 커서가 있는데 어휘가 {withCursor.Count}종만 나왔습니다 — 추첨이 갈래를 잃었습니다.");
            Assert.IsFalse(withoutCursor.ContainsKey(WanderAmbientMotion.FocusScreenGlance),
                $"{LogPrefix} 커서를 못 읽는데도 «화면 쪽 돌아보기»가 뽑혔습니다 — 없는 대상을 향해 " +
                "돌아보는 그림이고, 절대 불변 원칙 1 위반입니다.");
            Assert.AreEqual(3, withoutCursor.Count,
                $"{LogPrefix} 커서가 없을 때 어휘가 {withoutCursor.Count}종입니다(기대 3종).");

            float glanceShare = withCursor[WanderAmbientMotion.FocusScreenGlance] / (float)samples;
            float recrossWith = withCursor[WanderAmbientMotion.FocusRecross] / (float)samples;
            float recrossWithout = withoutCursor[WanderAmbientMotion.FocusRecross] / (float)samples;

            Assert.AreEqual(FocusAmbientGestures.ScreenGlanceWeight, glanceShare, 0.005f,
                $"{LogPrefix} G3 실측 비율 {glanceShare:F3}이 가중치와 다릅니다.");
            Assert.AreEqual(recrossWith + glanceShare, recrossWithout, 0.005f,
                $"{LogPrefix} G3가 빠진 몫({glanceShare:F3})을 G1이 받지 않았습니다 — 분포가 찌그러졌습니다.");

            Debug.Log($"{LogPrefix} 어휘 분포(균등 롤 {samples}회) — 커서 있음 " +
                $"G1 {recrossWith:F3} / G2 {withCursor[WanderAmbientMotion.FocusRingCheck] / (float)samples:F3} / " +
                $"G3 {glanceShare:F3} / G4 {withCursor[WanderAmbientMotion.FocusStanceSwap] / (float)samples:F3}. " +
                $"커서 없음 G1 {recrossWithout:F3}(= G1 + G3).");
        }

        private static void Bump(Dictionary<WanderAmbientMotion, int> counts, WanderAmbientMotion m)
        {
            counts.TryGetValue(m, out int n);
            counts[m] = n + 1;
        }

        // ====================================================================
        // (4) ★★ 마스터 스위치 + 어휘 분리 — 실제 세션을 켜서 잰다
        // ====================================================================

        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) => StateId = id;
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        /// <summary>
        /// 세션이 <b>실제로 켜진</b> 블랙보드를 만든다. 감시자의 타이머 로직은 한 줄도 건드리지 않고
        /// (그쪽은 오늘 밤 이미 올바르게 고쳐졌다) <c>IsSessionActive</c>의 <b>사실</b>만 세운다.
        ///
        /// <para>리플렉션으로 세운 뒤 <b>공개 프로퍼티로 되읽어 확인</b>한다 — 프로퍼티 모양이 바뀌면
        /// 조용히 통과하는 대신 여기서 빨간불이 난다(부재 단언이 조용히 초록이 되는 그 함정 방지).</para>
        /// </summary>
        private static StickmanBlackboard BuildSessionBlackboard(StickConfig config, GameObject host,
            bool sessionActive)
        {
            var body = host.AddComponent<Rigidbody2D>();
            var director = host.AddComponent<FocusWatchDirector>();

            if (sessionActive)
            {
                PropertyInfo prop = typeof(FocusWatchDirector).GetProperty(nameof(FocusWatchDirector.IsSessionActive));
                Assert.IsNotNull(prop, $"{LogPrefix} FocusWatchDirector.IsSessionActive 프로퍼티가 없습니다.");
                prop.SetValue(director, true);
                Assert.IsTrue(director.IsSessionActive,
                    $"{LogPrefix} 세션을 켜지 못했습니다 — 이 상태로는 아래 단언이 아무것도 증명하지 못합니다.");
            }

            var blackboard = new StickmanBlackboard { Config = config, Body = body };
            var machine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Idle, new SilentState(StickmanStateId.Idle) },
            });
            blackboard.Machine = machine;
            machine.Start(StickmanStateId.Idle);
            return blackboard;
        }

        [Test]
        public void 세션_중에는_집중_어휘만_받고_세션_밖에서는_평소_어휘만_받는다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            var inside = new GameObject(nameof(세션_중에는_집중_어휘만_받고_세션_밖에서는_평소_어휘만_받는다) + "_In");
            var outside = new GameObject(nameof(세션_중에는_집중_어휘만_받고_세션_밖에서는_평소_어휘만_받는다) + "_Out");
            try
            {
                StickmanBlackboard inSession = BuildSessionBlackboard(config, inside, sessionActive: true);
                StickmanBlackboard noSession = BuildSessionBlackboard(config, outside, sessionActive: false);

                Assert.IsTrue(inSession.IsFocusSessionAmbientActive,
                    $"{LogPrefix} 세션이 켜졌는데 앰비언트가 활성이 아닙니다(마스터 스위치 기본값 확인).");
                Assert.IsFalse(noSession.IsFocusSessionAmbientActive,
                    $"{LogPrefix} 세션이 없는데 앰비언트가 활성입니다.");

                Assert.IsTrue(inSession.BeginIdleAmbientMotion(WanderAmbientMotion.FocusRecross),
                    $"{LogPrefix} 세션 중인데 집중 어휘가 거부됐습니다 — 제스처 층이 통째로 죽습니다.");
                Assert.IsFalse(inSession.BeginIdleAmbientMotion(WanderAmbientMotion.LookAround),
                    $"{LogPrefix} 세션 중에 평소 «손차양»이 수용됐습니다 — 팔짱을 낀 채 이마에 손을 얹는 " +
                    "그림이 되고, 관망 자세가 그 순간 깨집니다.");
                Assert.IsFalse(inSession.BeginIdleAmbientMotion(WanderAmbientMotion.SitAndYawn),
                    $"{LogPrefix} 세션 중에 «기지개»(만세)가 수용됐습니다 — 관망 자세와 정면 충돌합니다.");

                Assert.IsTrue(noSession.BeginIdleAmbientMotion(WanderAmbientMotion.LookAround),
                    $"{LogPrefix} 세션 밖에서 평소 어휘가 거부됐습니다 — 기존 유휴 연출이 죽었습니다(회귀).");
                Assert.IsFalse(noSession.BeginIdleAmbientMotion(WanderAmbientMotion.FocusStanceSwap),
                    $"{LogPrefix} 세션 밖에서 집중 어휘가 수용됐습니다 — 세션이 아닌데 팔짱을 낍니다.");

                Assert.AreEqual(FocusAmbientGestures.RecrossSeconds, RestartAndReadDuration(inSession,
                    WanderAmbientMotion.FocusRecross), 1e-4f,
                    $"{LogPrefix} G1의 지속 시간이 어휘 표와 다릅니다 — 박자표의 «평평한 구간» 길이가 어긋납니다.");
                Assert.AreEqual(FocusAmbientGestures.StanceSwapSeconds, RestartAndReadDuration(inSession,
                    WanderAmbientMotion.FocusStanceSwap), 1e-4f,
                    $"{LogPrefix} G4의 지속 시간이 어휘 표와 다릅니다.");
            }
            finally
            {
                Object.DestroyImmediate(inside);
                Object.DestroyImmediate(outside);
                Object.DestroyImmediate(config);
            }
        }

        private static float RestartAndReadDuration(StickmanBlackboard blackboard, WanderAmbientMotion motion)
        {
            Assert.IsTrue(blackboard.BeginIdleAmbientMotion(motion),
                $"{LogPrefix} {motion} 재생을 시작하지 못했습니다.");
            return blackboard.IdleAmbientDurationSeconds;
        }

        /// <summary>
        /// V7 ★ 네거티브 컨트롤 — 마스터 스위치를 끄면 <b>세션이 켜져 있어도</b> 100% 평소 거동이다.
        /// 이 저장소의 규약: "끄면 예전 그대로"가 말이 아니라 코드로 보장돼야 한다.
        /// </summary>
        [Test]
        public void 마스터_스위치를_끄면_세션_중에도_평소_거동으로_돌아간다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            var host = new GameObject(nameof(마스터_스위치를_끄면_세션_중에도_평소_거동으로_돌아간다));
            try
            {
                StickmanBlackboard blackboard = BuildSessionBlackboard(config, host, sessionActive: true);

                // 양성 대조 — 끄기 «전»에는 실제로 켜져 있다. 이게 없으면 아래 false가 스위치 때문인지
                // 원래부터 꺼져 있었기 때문인지 구분할 수 없다.
                Assert.IsTrue(blackboard.IsFocusSessionAmbientActive,
                    $"{LogPrefix} 전제가 깨졌습니다 — 스위치를 끄기 전인데 이미 비활성입니다.");

                config.focusSessionAmbientEnabled = false;

                Assert.IsTrue(blackboard.IsFocusSessionActive,
                    $"{LogPrefix} 스위치를 껐더니 «세션 진행 중»이라는 사실 자체가 사라졌습니다 — " +
                    "스위치는 연출만 꺼야 하고 타이머/링은 그대로여야 합니다(그쪽은 감시자 소유입니다).");
                Assert.IsFalse(blackboard.IsFocusSessionAmbientActive,
                    $"{LogPrefix} 마스터 스위치를 껐는데 앰비언트가 계속 활성입니다.");
                Assert.IsTrue(blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.LookAround),
                    $"{LogPrefix} 스위치를 껐는데 평소 어휘가 돌아오지 않았습니다 — «끄면 예전 거동»이 거짓입니다.");
                Assert.IsFalse(blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.FocusRecross),
                    $"{LogPrefix} 스위치를 껐는데 집중 어휘가 여전히 수용됩니다.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(config);
            }
        }

        // ====================================================================
        // (5) ★ 박자 — 25분 세션 시뮬레이션(전부 벽시계 초)
        // ====================================================================

        private struct SessionStats
        {
            public float WalkSeconds;
            public float IdleSeconds;
            public int GestureCount;
            public float MaxSilenceSeconds;
            public float MeanGapSeconds;
            public int StanceSwapCount;
        }

        /// <summary>
        /// <see cref="AutoWanderController"/>의 Resting/Moving 갈래를 <b>그대로</b> 재현한다(그쪽은
        /// 씬/발판 폴러가 필요한 배선 경로라 EditMode에서 직접 몰 수 없다 —
        /// <c>IdleAmbientLookAroundInvariantTests</c>가 세운 선례와 같은 어법이고, 재현이 맞는지는
        /// <b>평소 설정으로 돌렸을 때 문서의 실측값이 나오는가</b>로 검증한다).
        /// </summary>
        private static SessionStats Simulate(StickConfig c, bool focus, int seed)
        {
            const float totalSeconds = 25f * 60f;
            var rng = new System.Random(seed);
            var stats = new SessionStats();
            var fireTimes = new List<float>(128);

            float t = 0f;
            float lastFire = float.NegativeInfinity;
            float cooldown = focus ? c.focusAmbientGestureCooldownSeconds : c.wanderLookAroundCooldownSeconds;
            float idleMin = focus ? c.focusSessionIdleDurationMin : c.wanderIdleDurationMin;
            float idleMax = focus ? c.focusSessionIdleDurationMax : c.wanderIdleDurationMax;
            float walkChance = focus ? c.focusSessionWalkChance : c.wanderPostIdleWalkChance;

            while (t < totalSeconds)
            {
                // EnterResting()
                float rest = Jitter(rng, c, Range(rng, idleMin, idleMax));
                float delay = Range(rng, c.wanderLookAroundDelayMin, c.wanderLookAroundDelayMax);

                // TickResting() — 추첨권은 유예에 막혀도 함께 소모된다(컨트롤러와 같은 규칙).
                if (delay < rest)
                {
                    float fireAt = t + delay;
                    if (fireAt - lastFire >= cooldown)
                    {
                        fireTimes.Add(fireAt);
                        lastFire = fireAt;
                        if (focus)
                        {
                            WanderAmbientMotion drawn = FocusAmbientGestures.Draw(rng.NextDouble(), true);
                            if (drawn == WanderAmbientMotion.FocusStanceSwap) stats.StanceSwapCount++;
                        }
                    }
                }
                t += rest;
                stats.IdleSeconds += rest;

                // ResolvePostIdleBranch() — Walk / (제자리 점프 0%) / Idle 연장
                if (rng.NextDouble() < walkChance)
                {
                    float walk = Jitter(rng, c, Range(rng, c.wanderWalkDurationMin, c.wanderWalkDurationMax));
                    t += walk;
                    stats.WalkSeconds += walk;
                }
            }

            stats.GestureCount = fireTimes.Count;
            float prev = 0f;
            float maxGap = 0f;
            float sumGap = 0f;
            for (int i = 0; i < fireTimes.Count; i++)
            {
                float gap = fireTimes[i] - prev;
                maxGap = Mathf.Max(maxGap, gap);
                if (i > 0) sumGap += gap;
                prev = fireTimes[i];
            }
            stats.MaxSilenceSeconds = maxGap;
            stats.MeanGapSeconds = fireTimes.Count > 1 ? sumGap / (fireTimes.Count - 1) : float.PositiveInfinity;
            return stats;
        }

        private static float Range(System.Random rng, float min, float max)
            => max <= min ? min : min + (float)rng.NextDouble() * (max - min);

        private static float Jitter(System.Random rng, StickConfig c, float baseValue)
        {
            float factor = 1f + (float)((rng.NextDouble() * 2.0 - 1.0) * c.wanderDurationJitterRatio);
            return Mathf.Max(0.01f, baseValue * factor);
        }

        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"{LogPrefix} 배포 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        /// <summary>
        /// V6 ★ 걷기가 <b>줄되 사라지지 않는다</b>. 파쿠르·뛰어내리기·매달리기는 전부 걷기에서
        /// 갈라져 나오므로, 걷기를 0으로 만들면 25분 동안 그 연출들이 <b>구조적으로 도달 불가</b>가 된다.
        /// </summary>
        [Test]
        public void 세션_중_걷기가_줄지만_사라지지_않는다()
        {
            StickConfig c = LoadDeployedConfig();

            float focusWalk = 0f, normalWalk = 0f, focusIdleShare = 0f;
            const int runs = 12;
            for (int i = 0; i < runs; i++)
            {
                SessionStats f = Simulate(c, focus: true, seed: 20260906 + i);
                SessionStats n = Simulate(c, focus: false, seed: 20260906 + i);
                focusWalk += f.WalkSeconds;
                normalWalk += n.WalkSeconds;
                focusIdleShare += f.IdleSeconds / (f.IdleSeconds + f.WalkSeconds);
            }
            focusWalk /= runs;
            normalWalk /= runs;
            focusIdleShare /= runs;

            Debug.Log($"{LogPrefix} 25분 세션 시뮬레이션 {runs}회 평균 — 평소 걷기 {normalWalk:F0}초 / " +
                $"세션 중 걷기 {focusWalk:F0}초(설계 목표 192초 근방), 세션 중 Idle 점유율 {focusIdleShare * 100f:F1}%.");

            Assert.Greater(focusWalk, 60f,
                $"{LogPrefix} 세션 중 걷기가 {focusWalk:F0}초뿐입니다 — 이 정도면 파쿠르/뛰어내리기/매달리기가 " +
                "사실상 도달 불가입니다. 이건 «분포 변경»이 아니라 «묶어두기»입니다.");
            Assert.Less(focusWalk, normalWalk * 0.6f,
                $"{LogPrefix} 세션 중 걷기({focusWalk:F0}초)가 평소({normalWalk:F0}초)에서 충분히 줄지 " +
                "않았습니다 — 관망 자세를 볼 시간이 그만큼 사라집니다.");
            Assert.Greater(focusIdleShare, 0.8f,
                $"{LogPrefix} 세션 중 Idle 점유율이 {focusIdleShare * 100f:F1}%입니다(설계 87%) — " +
                "관망 자세가 덮는 시간이 설계보다 짧습니다.");

            // 재현 검증 — 평소 설정에서는 문서의 실측값(510초 근방)이 나와야 한다. 이게 없으면
            // 위 비교는 "재현이 틀렸는데도 통과하는" 검사가 된다.
            Assert.That(normalWalk, Is.EqualTo(510f).Within(120f),
                $"{LogPrefix} 평소 설정 재현값이 {normalWalk:F0}초로 문서 실측(510초)에서 크게 벗어났습니다 — " +
                "시뮬레이션이 실제 갈래를 재현하지 못하고 있어 위 단언들이 아무것도 증명하지 못합니다.");
        }

        /// <summary>
        /// V5 ★ 침묵 구간 — 연속 무발동이 지나치게 길어지지 않고, 그렇다고 <b>평소보다 잦아지지도
        /// 않는다</b>(2026-08-31 사용자 신고 "너무 자주함"의 체감 하한을 다시 넘지 않는 것이 설계 제약).
        ///
        /// <para>★ <b>합격선 48초의 유도</b>(설계 문서의 34.5초를 그대로 쓰면 안 된다 — 실측으로 확인):
        /// 문서의 34.5초는 «쿨다운 28 + 걷기 최대 4 + 추첨 지연 최대 2.5»라는 <b>이상적</b> 최댓값인데,
        /// 실제 컨트롤러에는 «유예에 막혀도 추첨권은 소모된다»는 규칙이 있어서 막힌 Idle 구간 하나가
        /// 통째로 버려진다. 그래서 최악에는 Idle 한 사이클(최대 11 × 지터 1.175 ≈ 12.9초)이 더 붙는다:
        /// 28 + 12.9 + 4.7 + 2.9 ≈ 48.5. 이건 세션 전용 문제가 아니다 — <b>평소 경로가 이미 같은
        /// 형태</b>이며 오히려 더 길다(아래 대조 단언). 그 사실을 숨기지 않고 대조로 못박는다.</para>
        /// </summary>
        [Test]
        public void 제스처_침묵이_평소보다_길어지지_않고_잦아지지도_않는다()
        {
            StickConfig c = LoadDeployedConfig();

            float worstSilence = 0f, normalWorstSilence = 0f;
            float meanGapSum = 0f, countSum = 0f, normalCountSum = 0f, swapSum = 0f;
            const int runs = 12;
            for (int i = 0; i < runs; i++)
            {
                SessionStats f = Simulate(c, focus: true, seed: 424242 + i);
                SessionStats n = Simulate(c, focus: false, seed: 424242 + i);
                worstSilence = Mathf.Max(worstSilence, f.MaxSilenceSeconds);
                normalWorstSilence = Mathf.Max(normalWorstSilence, n.MaxSilenceSeconds);
                meanGapSum += f.MeanGapSeconds;
                countSum += f.GestureCount;
                normalCountSum += n.GestureCount;
                swapSum += f.StanceSwapCount;
            }
            float meanGap = meanGapSum / runs;
            float count = countSum / runs;
            float normalCount = normalCountSum / runs;
            float swaps = swapSum / runs;
            float swapPeriodMinutes = swaps > 0f ? 25f / swaps : float.PositiveInfinity;

            Debug.Log($"{LogPrefix} 25분 세션 {runs}회 — 제스처 {count:F1}회(평소 어휘였다면 {normalCount:F1}회), " +
                $"평균 간격 {meanGap:F1}초, 최장 침묵 {worstSilence:F1}초(평소 {normalWorstSilence:F1}초), " +
                $"관망 자세 토글 {swaps:F1}회(평균 {swapPeriodMinutes:F1}분마다).");

            Assert.Less(worstSilence, 48f,
                $"{LogPrefix} 최장 침묵이 {worstSilence:F1}초입니다 — 그 구간에는 화면이 «집중 중»이라는 " +
                "말을 아무것도 하지 않습니다(유도된 상한 48초, 위 문서 참고).");
            Assert.Less(worstSilence, normalWorstSilence * 1.25f,
                $"{LogPrefix} 세션 중 최장 침묵({worstSilence:F1}초)이 평소({normalWorstSilence:F1}초)보다 " +
                "길어졌습니다 — 세션 중에는 화면이 상태를 <b>더</b> 말해야 하는데 반대로 갔습니다.");
            Assert.Less(count, normalCount * 1.35f,
                $"{LogPrefix} 세션 중 제스처가 {count:F1}회로 평소({normalCount:F1}회)보다 크게 잦아졌습니다 — " +
                "2026-08-31 «너무 자주함» 신고의 자리로 되돌아갑니다.");
            Assert.That(swapPeriodMinutes, Is.EqualTo(3.5f).Within(1.5f),
                $"{LogPrefix} 관망 자세 토글이 평균 {swapPeriodMinutes:F1}분마다입니다(설계 3.5분) — " +
                "너무 잦으면 안절부절못하는 그림이고, 너무 드물면 뒷짐(P2)이 없는 것과 같습니다.");
        }

        // ====================================================================
        // (6) ★ 원칙 1 — L2 제스처에서 파생된 대사는 <b>0건</b>이다
        // ====================================================================

        /// <summary>
        /// 절대 불변 원칙 1은 <i>"대사는 <b>상태 전이가 확정된 뒤</b> 그 상태로부터만 파생한다"</i>이다.
        /// L2 제스처 4종은 <b>상태 전이가 아니다</b>(Idle 위에 얹는 포즈 층 —
        /// <c>FocusWatchTier.Glance</c>와 같은 성격). 전이가 없으면 파생할 상태가 없으므로 대사도 없다.
        ///
        /// <para>실무적 근거도 같은 방향이다: 앰비언트에 대사를 달면 25분에 44번(≈34초마다) 말하게 되고,
        /// 상주 앱에서 그건 <b>비침해 원칙 2</b>로도 진다.</para>
        ///
        /// <para><b>이 단언이 빨개졌다면</b> 누군가 집중 제스처에 대사를 묶은 것이다. 고치는 방법은
        /// 이 테스트를 지우는 것이 아니라 <b>리더 판정을 받는 것</b>이다 — 세션 중 대사가 필요하면
        /// 그것은 별도의 「세션 하트비트」 <b>상태</b>여야 하고 그건 <c>design-narrative</c> 소관이다.</para>
        /// </summary>
        [Test]
        public void 집중_제스처에_묶인_대사가_한_줄도_없다()
        {
            FieldInfo field = typeof(StickMate.Dialogue.AmbientChatter).GetField("IdleLineMotionRequirement",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field,
                $"{LogPrefix} AmbientChatter.IdleLineMotionRequirement를 찾지 못했습니다 — 자격 축의 " +
                "이름/형태가 바뀌었다면 이 검사는 아무것도 훑지 못하고 조용히 초록이 됩니다.");

            var requirements = field.GetValue(null) as WanderAmbientMotion?[];
            Assert.IsNotNull(requirements, $"{LogPrefix} 자격 축의 원소 타입이 바뀌었습니다.");
            Assert.Greater(requirements.Length, 0, $"{LogPrefix} 자격 축이 비어 있습니다(빈 집합 통과 방지).");

            // 양성 대조 — 이 축이 «실제로 모션을 거는» 표라는 사실을 먼저 확인한다. 전부 null이면
            // 아래 "집중 어휘 0건"은 표가 비어서 통과한 것과 구분되지 않는다.
            int gated = 0;
            var offenders = new List<string>();
            for (int i = 0; i < requirements.Length; i++)
            {
                if (!requirements[i].HasValue) continue;
                gated++;
                if (FocusAmbientGestures.IsFocusGesture(requirements[i].Value))
                    offenders.Add($"{i}번 줄 → {requirements[i].Value}");
            }
            Assert.Greater(gated, 0,
                $"{LogPrefix} 모션에 걸린 대사가 한 줄도 없습니다 — 이 축이 죽었다는 뜻이고, " +
                "그러면 아래 단언은 «집중 어휘가 없다»가 아니라 «아무 어휘도 없다»를 확인할 뿐입니다.");

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 집중 세션 제스처에 대사가 묶였습니다: {string.Join(", ", offenders)}.\n" +
                "L2 제스처는 상태 전이가 아니므로 파생할 상태가 없습니다(절대 불변 원칙 1). " +
                "세션 중 대사가 필요하면 별도의 「세션 하트비트」 상태로 만들어야 하고, 그 판단은 " +
                "리더 라우팅 항목입니다(design-narrative 소관).");

            Debug.Log($"{LogPrefix} 원칙 1 확인 — 모션 조건부 대사 {gated}줄 중 집중 어휘에 묶인 줄 0개.");
        }

        // ====================================================================
        // (7) ★ 소유권 계약 — 관망 자세는 «포즈만» 바꾼다
        // ====================================================================

        /// <summary>
        /// 리더 인계 계약 2개를 잠근다.
        /// <list type="number">
        ///   <item><b>수평 이동 소유권</b>은 배회 AI에 그대로 있다 — Idle은 자기소유 목록 어디에도 없다.</item>
        ///   <item><b><see cref="SpectacleEventLock"/>을 잡지 않는다</b> — 관망 자세는 상태 전이가 아니라
        ///     Idle 위의 포즈 층이다. 락을 잡으면 세션 25분 내내 파쿠르·춤·활쏘기가 <b>조용히</b> 막힌다.</item>
        /// </list>
        /// </summary>
        [Test]
        public void 관망_자세는_수평이동과_스펙터클_락을_소유하지_않는다()
        {
            Assert.IsFalse(StickmanBlackboard.IsHorizontalMotionSelfManaged(StickmanStateId.Idle),
                $"{LogPrefix} Idle이 수평 자기소유가 됐습니다 — 관망 자세는 포즈만 바꿔야 하고 " +
                "위치는 배회 AI가 계속 소유합니다.");
            Assert.IsFalse(StickmanBlackboard.IsFacingSelfManaged(StickmanStateId.Idle),
                $"{LogPrefix} Idle이 방향 자기소유가 됐습니다 — G3의 방향 전환은 Idle에서 MoveInputX가 " +
                "0이라 덮이지 않는다는 사실에 얹혀 있고, 목록에 넣으면 배회 AI가 걷기 시작할 때 " +
                "방향을 못 바꿉니다.");

            var config = ScriptableObject.CreateInstance<StickConfig>();
            var host = new GameObject(nameof(관망_자세는_수평이동과_스펙터클_락을_소유하지_않는다));
            try
            {
                // 양성 대조 — 락 자체는 살아 있다(잡으면 실제로 잡힌다). 이 대조가 없으면 아래 IsActive
                // false는 "락이 죽었다"와 구분되지 않는다.
                object owner = new object();
                Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Dance, owner),
                    $"{LogPrefix} 스펙터클 락을 잡지 못했습니다 — 이 대조가 없으면 아래 단언은 무의미합니다.");
                SpectacleEventLock.Release(owner);
                Assert.IsFalse(SpectacleEventLock.IsActive, $"{LogPrefix} 락 해제가 되지 않았습니다.");

                StickmanBlackboard blackboard = BuildSessionBlackboard(config, host, sessionActive: true);
                Assert.IsTrue(blackboard.BeginIdleAmbientMotion(WanderAmbientMotion.FocusStanceSwap),
                    $"{LogPrefix} 제스처를 시작하지 못했습니다.");

                Assert.IsFalse(SpectacleEventLock.IsActive,
                    $"{LogPrefix} 관망 자세/제스처가 스펙터클 락을 잡았습니다({SpectacleEventLock.ActiveKind}) — " +
                    "세션 25분 내내 다른 모든 연출이 조용히 막힙니다.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(config);
            }
        }
    }
}
