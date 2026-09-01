using NUnit.Framework;
using StickMate.States;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 상체 기울임(<see cref="StickmanPoseAnimator.SetBodyLean"/>) 회귀 잠금 — 2026-09-01
    /// 사용자 참고 이미지("달리며 넘어지는 졸라맨") 라운드.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 이 프로젝트에서 몸통 오브젝트의 <c>localRotation</c>은 <b>한 번도 세팅된 적이 없었다</b>
    /// (전수 검색으로 확인 — <c>localPosition</c>만 썼다). 그래서 (1) 달릴 때 상체가 앞으로 기우는
    /// 그림이 원천적으로 나올 수 없었고, (2) 유휴 "주위 살피기"의 머리 좌우 이동은 목을 함께 기울일
    /// 배관이 없어 기본값 0으로 꺼져 있었다(StickConfig.idleAmbientLookHeadShiftRatio 문서가
    /// "값을 되살리려면 먼저 목을 함께 기울이는 배관부터 만들어야 한다"고 예고한 그 배관이다).
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 두 개의 절대 조건
    /// ============================================================================
    ///  (A) <b>회전 중심은 엉덩이다</b> — 상체가 아무리 기울어도 <b>다리는 한 톨도 움직이지 않는다</b>.
    ///      몸통 오브젝트의 원점은 몸통 선의 <b>중점</b>이라, localRotation만 주면 자기 중점을 축으로
    ///      돌아 아랫배가 뒤로 빠지고 다리와 어긋난다. 아래
    ///      <see cref="발밑_피벗이었다면_엉덩이가_실제로_움직인다"/>가 "엉덩이가 아닌 축을 썼다면
    ///      이 검사가 실제로 깨진다"를 같은 식으로 계산해 보여주는 네거티브 컨트롤이다.
    ///  (B) <b>머리는 목 위에 남는다</b> — 머리 중심이 몸통(=목) 선 위에서 벗어나지 않는다.
    ///      옛 머리 좌우 이동이 0으로 꺼진 이유가 정확히 이 조건 위반이었다.
    ///
    /// 그 위에 속도 연동/자동 복귀/피격 임펄스까지 <b>실제 제품 코드를 그대로 돌려</b> 확인한다.
    /// 물리도 씬도 없이 성립하는 이유는 Tests/EditMode/WalkAmplitudeSpeedScalingTests.cs와 같다
    /// (StickmanPoseAnimator는 순수 C# 클래스이고 입력이 Transform 실측뿐이다).
    /// </summary>
    public sealed class BodyLeanHipPivotTests
    {
        // 가짜 리그 치수 — 실제 프리팹 비례를 흉내만 낸다(단언은 전부 이 값들로부터 유도된다).
        private const float HipY = 0.70f;
        private const float ShoulderY = 1.32f;
        private const float TorsoTopY = 1.40f;
        private const float HeadY = 1.62f;
        private const float TorsoCenterY = (HipY + TorsoTopY) * 0.5f;
        private const float LegUpperLength = 0.375f;
        private const float LegLowerLength = 0.375f;
        private const float ArmUpperLength = 0.285f;
        private const float ArmLowerLength = 0.285f;
        private const float LimbWidth = 0.05f;

        private const float Dt = 1f / 60f;
        private const float SmoothingRate = 35f;
        private const float SpeedSmoothingRate = 6f;
        private const float LeanSmoothingRate = 12f;   // StickConfig.bodyLeanSmoothingRate 출하값.
        private const float CommandSpeed = 2.5f;

        private const float Tol = 1e-5f;

        private GameObject _root;
        private Transform _torso;
        private Transform _head;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            _torso = null;
            _head = null;
        }

        // ============================================================================
        // (A) 엉덩이 피벗 — 다리 불변
        // ============================================================================

        [Test]
        public void 상체를_기울여도_다리와_발끝이_한_톨도_움직이지_않는다()
        {
            StickmanPoseAnimator pose = BuildRig();
            SettleIdle(pose);

            Vector3 leftUpper = Find("LeftLeg").localPosition;
            Vector3 leftLower = Find("LeftLegLower").localPosition;
            Vector3 rightUpper = Find("RightLeg").localPosition;
            pose.GetFootWorldPositions(out Vector2 footLeftBefore, out Vector2 footRightBefore);

            pose.SetBodyLean(20f);
            // 기울인 **뒤에도** 포즈 틱을 한 번 더 돌린다 — 다리 좌표를 실제로 다시 쓰는 경로
            // (ApplyAngle)가 기울임 상태에서 돌아야 이 검사가 의미를 갖는다.
            pose.ApplyIdlePose(Dt, StaticPoseSettings(), SmoothingRate);

            Assert.AreEqual(20f, pose.BodyLeanDegrees, 1e-4f, "요청한 기울임이 적용되지 않았습니다.");

            AssertSame(leftUpper, Find("LeftLeg").localPosition, "왼다리 부착점");
            AssertSame(leftLower, Find("LeftLegLower").localPosition, "왼무릎");
            AssertSame(rightUpper, Find("RightLeg").localPosition, "오른다리 부착점");

            pose.GetFootWorldPositions(out Vector2 footLeftAfter, out Vector2 footRightAfter);
            Assert.AreEqual(footLeftBefore.x, footLeftAfter.x, Tol, "상체를 기울였는데 왼발이 움직였습니다.");
            Assert.AreEqual(footLeftBefore.y, footLeftAfter.y, Tol, "상체를 기울였는데 왼발 높이가 변했습니다.");
            Assert.AreEqual(footRightBefore.x, footRightAfter.x, Tol, "상체를 기울였는데 오른발이 움직였습니다.");
            Assert.AreEqual(footRightBefore.y, footRightAfter.y, Tol, "상체를 기울였는데 오른발 높이가 변했습니다.");

            Debug.Log($"[LEAN-TEST] 20도 기울임 — 다리/발끝 불변 확인. 발(월드) L={footLeftAfter} R={footRightAfter}");
        }

        [Test]
        public void 몸통과_머리와_어깨는_엉덩이를_축으로_정확히_회전한다()
        {
            StickmanPoseAnimator pose = BuildRig();
            SettleIdle(pose);

            const float Degrees = 18f;
            pose.SetBodyLean(Degrees);
            pose.ApplyIdlePose(Dt, StaticPoseSettings(), SmoothingRate);

            float sin = Mathf.Sin(Degrees * Mathf.Deg2Rad);
            float cos = Mathf.Cos(Degrees * Mathf.Deg2Rad);

            // 엉덩이 피벗 회전의 정의 그대로: (x, y) = 엉덩이 + R·(중립 - 엉덩이).
            AssertPivotRotated(_torso.localPosition, TorsoCenterY, sin, cos, "몸통");
            AssertPivotRotated(_head.localPosition, HeadY, sin, cos, "머리");
            AssertPivotRotated(Find("LeftArm").localPosition, ShoulderY, sin, cos, "왼쪽 어깨");
            AssertPivotRotated(Find("RightArm").localPosition, ShoulderY, sin, cos, "오른쪽 어깨");

            // 몸통 자체도 같은 각도만큼 돌아 있어야 한다(액세서리 레이어가 이 값을 읽어 따라간다).
            Assert.AreEqual(-Degrees, Mathf.DeltaAngle(0f, _torso.localEulerAngles.z), 1e-3f,
                "몸통 Transform의 회전각이 요청한 기울임과 다릅니다 — 액세서리(모자/망토)가 이 값을 읽습니다.");
        }

        /// <summary>네거티브 컨트롤 — "엉덩이가 아니라 발밑(루트 원점)을 축으로 돌렸다면" 이 검사가
        /// 실제로 깨진다는 것을 같은 기하로 보여준다. 그래야 위 (A)의 통과가 "조건이 헐거워서"가 아님이
        /// 같은 파일에서 증명된다.</summary>
        [Test]
        public void 발밑_피벗이었다면_엉덩이가_실제로_움직인다()
        {
            const float Degrees = 18f;
            float sin = Mathf.Sin(Degrees * Mathf.Deg2Rad);

            // 발밑(y=0)을 축으로 돌렸다면 다리 부착점(엉덩이)의 x가 이만큼 움직인다.
            float hipDriftIfFootPivot = HipY * sin;
            Assert.Greater(hipDriftIfFootPivot, Tol * 100f,
                "네거티브 컨트롤이 성립하지 않습니다 — 발밑 피벗 가정에서도 엉덩이가 안 움직인다면 " +
                "이 리그의 치수가 잘못됐습니다.");

            StickmanPoseAnimator pose = BuildRig();
            SettleIdle(pose);
            float hipBefore = Find("LeftLeg").localPosition.x;
            pose.SetBodyLean(Degrees);
            pose.ApplyIdlePose(Dt, StaticPoseSettings(), SmoothingRate);
            float hipAfter = Find("LeftLeg").localPosition.x;

            Assert.AreEqual(hipBefore, hipAfter, Tol,
                $"엉덩이가 움직였습니다 — 발밑 피벗이었다면 {hipDriftIfFootPivot:F4}유닛 움직였을 값입니다.");
            Debug.Log($"[LEAN-TEST] 네거티브 컨트롤 — 발밑 피벗이었다면 엉덩이가 {hipDriftIfFootPivot:F4}유닛 " +
                $"움직였을 자리에서 실측 0.0000유닛.");
        }

        // ============================================================================
        // (B) 머리는 목 위에 남는다
        // ============================================================================

        [Test]
        public void 머리_중심은_기울어진_몸통_선_위에_남는다()
        {
            StickmanPoseAnimator pose = BuildRig();
            SettleIdle(pose);

            foreach (float degrees in new[] { -20f, -8f, 0f, 8f, 20f })
            {
                pose.SetBodyLean(degrees);
                pose.ApplyIdlePose(Dt, StaticPoseSettings(), SmoothingRate);

                // 몸통 선의 방향(월드) — 이 선의 연장선 위에 머리 중심이 있어야 목이 이어져 보인다.
                Vector3 hip = _root.transform.TransformPoint(new Vector3(0f, HipY, 0f));
                Vector3 torsoTop = _torso.TransformPoint(new Vector3(0f, (TorsoTopY - HipY) * 0.5f, 0f));
                Vector3 dir = (torsoTop - hip).normalized;
                Vector3 headCenter = _head.position;
                Vector3 rel = headCenter - hip;
                float perpendicular = Mathf.Abs(rel.x * dir.y - rel.y * dir.x); // 2D 외적 = 선까지의 수직거리.

                Assert.Less(perpendicular, 1e-4f,
                    $"{degrees:F0}도 기울임에서 머리 중심이 몸통(목) 선에서 {perpendicular:F5}유닛 벗어났습니다 — " +
                    "옛 머리 좌우 이동(idleAmbientLookHeadShiftRatio)이 0으로 꺼진 바로 그 결함입니다.");
            }
        }

        [Test]
        public void 기울임_방향은_바라보는_방향을_따른다()
        {
            StickmanPoseAnimator pose = BuildRig();
            SettleIdle(pose);

            pose.SetFacing(1f);
            pose.SetBodyLean(15f);
            pose.ApplyIdlePose(Dt, StaticPoseSettings(), SmoothingRate);
            float headRight = _head.localPosition.x;

            pose.SetFacing(-1f);
            pose.SetBodyLean(15f);           // 같은 "앞으로" 요청.
            pose.ApplyIdlePose(Dt, StaticPoseSettings(), SmoothingRate);
            float headLeft = _head.localPosition.x;

            Assert.Greater(headRight, 0f, "오른쪽을 보고 앞으로 기울였는데 머리가 앞(+x)으로 가지 않았습니다.");
            Assert.Less(headLeft, 0f, "왼쪽을 보고 앞으로 기울였는데 머리가 앞(-x)으로 가지 않았습니다.");
            Assert.AreEqual(headRight, -headLeft, Tol, "좌우 기울임이 정확히 대칭이 아닙니다.");
        }

        // ============================================================================
        // (C) 속도 연동 / 자동 복귀 / 피격 임펄스
        // ============================================================================

        [Test]
        public void 걷는_속도가_빠를수록_상체가_더_기운다()
        {
            const float LeanAtFullSpeed = 10f;

            StickmanPoseAnimator slow = BuildRig();
            float slowLean = WalkAndMeasureLean(slow, 0.15f, LeanAtFullSpeed, out float slowSpeed01);
            TearDown();

            StickmanPoseAnimator fast = BuildRig();
            float fastLean = WalkAndMeasureLean(fast, 1f, LeanAtFullSpeed, out float fastSpeed01);

            Debug.Log($"[LEAN-TEST] 속도비 0.15 -> 속도정규화 {slowSpeed01:F3} / 기울임 {slowLean:F2}도, " +
                $"속도비 1.00 -> {fastSpeed01:F3} / {fastLean:F2}도");

            Assert.Greater(fastLean, slowLean + 1f,
                $"빠를 때({fastLean:F2}도)가 느릴 때({slowLean:F2}도)보다 확실히 더 기울지 않았습니다 — " +
                "속도 연동이 끊겼습니다.");
            // 설계식 그대로: 기울임 = 최대각 x 속도정규화(진폭 유도와 같은 값을 쓴다).
            Assert.AreEqual(LeanAtFullSpeed * fastSpeed01, fastLean, 0.6f,
                "명령 속도에서의 기울임이 설계식(최대각 x 속도정규화)과 다릅니다.");
            Assert.LessOrEqual(fastLean, LeanAtFullSpeed + 0.01f, "기울임이 설계 최대치를 넘었습니다.");
        }

        [Test]
        public void 걷기를_벗어나면_아무도_요청하지_않아_정확히_직립으로_돌아온다()
        {
            StickmanPoseAnimator pose = BuildRig();
            WalkAndMeasureLean(pose, 1f, 10f, out _);
            Assert.Greater(Mathf.Abs(pose.BodyLeanDegrees), 3f, "준비 실패 — 걷는 동안 기울지 않았습니다.");

            // 이제 아무도 요청하지 않는다(Idle 포즈 + 기울임 틱만 돈다).
            for (int i = 0; i < 120; i++)
            {
                pose.ApplyIdlePose(Dt, StaticPoseSettings(), SmoothingRate);
                pose.TickBodyLean(Dt, LeanSmoothingRate);
            }

            Assert.AreEqual(0f, pose.BodyLeanDegrees, 0f,
                "요청이 끊겼는데 상체가 정확히 직립으로 돌아오지 않았습니다(잔재가 남으면 그때부터 계속 기웁니다).");
            Assert.AreEqual(0f, _torso.localEulerAngles.z, 1e-4f, "몸통 회전이 원복되지 않았습니다.");
            Assert.AreEqual(0f, _head.localPosition.x, Tol, "머리가 중립 x로 돌아오지 않았습니다.");
        }

        [Test]
        public void 피격_임펄스는_스스로_사라진다()
        {
            StickmanPoseAnimator pose = BuildRig();
            SettleIdle(pose);

            pose.AddHitLean(-14f, recoverRate: 7f);
            pose.TickBodyLean(Dt, LeanSmoothingRate);
            Assert.Less(pose.BodyLeanDegrees, 0f, "피격 임펄스가 상체를 뒤로 젖히지 않았습니다.");

            float peak = 0f;
            for (int i = 0; i < 120; i++) // 2초.
            {
                pose.ApplyIdlePose(Dt, StaticPoseSettings(), SmoothingRate);
                pose.TickBodyLean(Dt, LeanSmoothingRate);
                peak = Mathf.Min(peak, pose.BodyLeanDegrees);
            }

            Debug.Log($"[LEAN-TEST] 피격 임펄스 최대 {peak:F2}도 -> 2초 뒤 {pose.BodyLeanDegrees:F4}도");
            Assert.Less(peak, -4f, "피격 기울임이 눈에 보일 만큼 나오지 않았습니다.");
            Assert.AreEqual(0f, pose.BodyLeanDegrees, 0f, "피격 기울임이 스스로 사라지지 않았습니다.");
        }

        [Test]
        public void 다리가_없는_리그에서는_기울이지_않는다()
        {
            // 엉덩이를 실측할 수 없으면 발밑을 축으로 도는 그림이 되므로, 아예 기울이지 않는 것이
            // 이 클래스의 계약이다(NullPlatformWindowService류의 "정직한 폴백"과 같은 관례).
            _root = new GameObject("HeadlessRig");
            var torso = new GameObject("Torso");
            torso.transform.SetParent(_root.transform, false);
            torso.transform.localPosition = new Vector3(0f, TorsoCenterY, 0f);

            var pose = new StickmanPoseAnimator(_root.transform);
            pose.SetBodyLean(25f);

            Assert.AreEqual(0f, pose.BodyLeanDegrees, 0f,
                "엉덩이를 실측하지 못한 리그인데 기울임이 적용됐습니다 — 발밑을 축으로 도는 그림이 됩니다.");
            Assert.AreEqual(0f, torso.transform.localEulerAngles.z, 1e-4f, "몸통이 회전했습니다.");
        }

        // ============================================================================
        // 헬퍼
        // ============================================================================

        private void AssertPivotRotated(Vector3 actual, float neutralY, float sin, float cos, string label)
        {
            float rel = neutralY - HipY;
            Assert.AreEqual(rel * sin, actual.x, 1e-4f,
                $"{label}의 x가 엉덩이 피벗 회전 결과와 다릅니다(기대 {rel * sin:F5}, 실측 {actual.x:F5}).");
            Assert.AreEqual(HipY + rel * cos, actual.y, 1e-4f,
                $"{label}의 y가 엉덩이 피벗 회전 결과와 다릅니다.");
        }

        private static void AssertSame(Vector3 expected, Vector3 actual, string label)
        {
            Assert.AreEqual(expected.x, actual.x, Tol, $"{label}의 x가 움직였습니다.");
            Assert.AreEqual(expected.y, actual.y, Tol, $"{label}의 y가 움직였습니다.");
        }

        /// <summary>지정 속도로 충분히 걷고, 그 동안 매 프레임 기울임 틱까지 돌린 뒤의 적용값을 돌려준다
        /// (제품 경로와 같은 순서 — 포즈 틱이 요청하고 TickBodyLean이 확정한다).</summary>
        private float WalkAndMeasureLean(StickmanPoseAnimator pose, float speedRatio, float leanAtFullSpeed,
            out float speed01)
        {
            pose.ResetWalkPhase();
            StickmanPoseAnimator.PoseSettings settings = StaticPoseSettings();
            float actualSpeed = CommandSpeed * speedRatio;
            float x = 0f;

            for (float t = 0f; t < 4f; t += Dt)
            {
                x += actualSpeed * Dt;
                _root.transform.position = new Vector3(x, 0f, 0f);
                pose.TickWalkPose(Dt, CommandSpeed, settings, SmoothingRate, SpeedSmoothingRate,
                    groundingBlend: 1f, amplitudeScale: 1f, strideScale: 0.93f,
                    leanDegreesAtFullSpeed: leanAtFullSpeed);
                pose.TickBodyLean(Dt, LeanSmoothingRate);
            }

            speed01 = pose.WalkSpeed01;
            return pose.BodyLeanDegrees;
        }

        /// <summary>Idle 포즈가 완전히 수렴할 때까지 돌린다(호흡 진폭 0이라 이후로는 각도가 고정된다).</summary>
        private void SettleIdle(StickmanPoseAnimator pose)
        {
            for (int i = 0; i < 240; i++) pose.ApplyIdlePose(Dt, StaticPoseSettings(), SmoothingRate);
        }

        /// <summary>호흡/흔들림이 0인 포즈 설정 — "기울임 말고는 아무 것도 변하지 않는다"를 만들기 위한 것.</summary>
        private static StickmanPoseAnimator.PoseSettings StaticPoseSettings()
            => new StickmanPoseAnimator.PoseSettings(
                legSpread: 12f, armSpread: 40f, idleKnee: 4f, idleElbow: 10f,
                breathAmplitude: 0f, breathFrequencyHz: 0f, breathArmDegrees: 0f);

        private Transform Find(string name)
        {
            Transform t = _root.transform.Find(name);
            if (t == null) t = _root.transform.Find(name.Replace("Lower", "")).Find(name);
            Assert.IsNotNull(t, $"리그에서 {name}을 찾지 못했습니다.");
            return t;
        }

        /// <summary>실제 프리팹 계층과 <b>이름 규약만</b> 같은 최소 리그
        /// (WalkAmplitudeSpeedScalingTests.BuildRig에 몸통/머리를 더한 것).</summary>
        private StickmanPoseAnimator BuildRig()
        {
            _root = new GameObject("FakeStickman");
            _root.transform.position = Vector3.zero;

            AddLimb("LeftLeg", HipY, LegUpperLength, LegLowerLength);
            AddLimb("RightLeg", HipY, LegUpperLength, LegLowerLength);
            AddLimb("LeftArm", ShoulderY, ArmUpperLength, ArmLowerLength);
            AddLimb("RightArm", ShoulderY, ArmUpperLength, ArmLowerLength);

            var torso = new GameObject("Torso");
            torso.transform.SetParent(_root.transform, false);
            torso.transform.localPosition = new Vector3(0f, TorsoCenterY, 0f);
            _torso = torso.transform;

            var head = new GameObject("Head");
            head.transform.SetParent(_root.transform, false);
            head.transform.localPosition = new Vector3(0f, HeadY, 0f);
            _head = head.transform;

            var pose = new StickmanPoseAnimator(_root.transform);
            Assert.IsTrue(pose.HasLimbs, "가짜 리그에서 팔다리를 찾지 못했습니다 — 이름 규약이 바뀌었을 수 있습니다.");
            return pose;
        }

        private void AddLimb(string name, float attachY, float upperLength, float lowerLength)
        {
            var upper = new GameObject(name);
            upper.transform.SetParent(_root.transform, false);
            upper.transform.localPosition = new Vector3(0f, attachY, 0f);
            var upperBox = upper.AddComponent<BoxCollider2D>();
            upperBox.size = new Vector2(LimbWidth, upperLength);
            upperBox.offset = new Vector2(0f, -upperLength * 0.5f);

            var lower = new GameObject(name + "Lower");
            lower.transform.SetParent(upper.transform, false);
            lower.transform.localPosition = new Vector3(0f, -upperLength, 0f);
            var lowerBox = lower.AddComponent<BoxCollider2D>();
            lowerBox.size = new Vector2(LimbWidth, lowerLength);
            lowerBox.offset = new Vector2(0f, -lowerLength * 0.5f);
        }
    }
}
