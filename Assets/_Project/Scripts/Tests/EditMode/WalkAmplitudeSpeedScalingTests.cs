using NUnit.Framework;
using StickMate.States;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ P9-c 회귀 잠금 — <b>"걷는 속도가 빠를수록 보폭/스윙 진폭이 커진다."</b>
    /// (docs/UX_FLOW.md 38-14-2, 사용자 요청 "움직임을 더 부드럽고 역동적으로")
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// <c>TickWalkPose</c>는 처음부터 <c>amplitudeScale</c> 인자를 받았지만, 거기 들어오는 값은
    /// <c>StickConfig.walkPoseAmplitudeScale</c>이라는 <b>고정 상수 1.0</b>이었다. 즉 배관은 있는데
    /// 속도와 연결돼 있지 않아, 아무리 빨리 걸어도 다리 스윙 폭이 한 치도 달라지지 않았다.
    /// 참고자료의 "달리기"는 새 키표가 아니라 <b>같은 키표의 더 큰 진폭</b>이다.
    ///
    /// ============================================================================
    /// 왜 이 테스트가 물리도 씬도 없이 성립하는가
    /// ============================================================================
    /// <see cref="StickmanPoseAnimator"/>는 MonoBehaviour가 아닌 순수 C# 클래스이고, 보행 사이클의
    /// 유일한 속도 입력은 <b>루트 Transform의 X 이동량 실측</b>이다(그 클래스의 _measuredSpeed 문서).
    /// 그래서 Rigidbody2D도 물리 스텝도 없이, 가짜 계층의 루트를 우리가 직접 원하는 속도로 밀어주면
    /// 실제 제품 코드가 그 속도를 그대로 측정한다 — <b>제품 코드를 흉내 내지 않고 그대로 돌린다</b>.
    /// 마디 길이는 BoxCollider2D.size.y에서 읽히므로(BuildSegment) 보폭 검증도 함께 가능하다.
    ///
    /// <para><b>주의(단위)</b>: 이 테스트가 재는 것은 전부 <b>무차원 비율</b>이거나 같은 리그 안에서의
    /// 상대 비교다. 캐릭터 배율/화면 해상도에 의존하는 절대값 단언은 두지 않는다.</para>
    /// </summary>
    public sealed class WalkAmplitudeSpeedScalingTests
    {
        // 가짜 리그 치수 — 실제 프리팹 값을 흉내 낼 필요가 없다(비율만 본다). 다만 대퇴 > 정강이 같은
        // 상식적 비례는 지켜 보폭 계산이 현실적인 수를 내게 한다.
        private const float HipY = 0.70f;
        private const float ShoulderY = 1.32f;
        private const float LegUpperLength = 0.375f;
        private const float LegLowerLength = 0.375f;
        private const float ArmUpperLength = 0.285f;
        private const float ArmLowerLength = 0.285f;
        private const float LimbWidth = 0.05f;

        private const float Dt = 1f / 60f;
        // 실제 배포 설정값 그대로(StickConfig.poseSmoothingRate / walkSpeedSmoothingRate) —
        // 스무딩 감쇠까지 포함한 "화면에 실제로 나오는" 진폭을 재기 위해서다.
        private const float SmoothingRate = 35f;
        private const float SpeedSmoothingRate = 6f;
        private const float CommandSpeed = 2.5f;   // WalkState가 넘기는 ResolveWalkSpeed() 자리.

        /// <summary>유도식이 실제로 쓰는 두 끝점(States/StickmanPoseAnimator.cs의 같은 이름 상수).
        /// 여기 다시 적는 이유는 "값이 바뀌면 이 테스트가 먼저 빨간불이 되게" 하기 위해서다 —
        /// 곡선 모양을 바꾸는 것은 자유지만, 바꿨다는 사실이 조용히 지나가면 안 된다.</summary>
        private const float ExpectedAtRest = 0.85f;
        private const float ExpectedAtFullSpeed = 1.35f;

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        // ========================================================================
        // (1) 핵심 — 진폭 배율이 속도에 대해 단조 증가한다
        // ========================================================================

        [Test]
        public void 진폭_배율이_속도에_대해_단조증가한다()
        {
            float[] ratios = { 0f, 0.25f, 0.5f, 0.75f, 1f };
            var amplitudes = new float[ratios.Length];

            for (int i = 0; i < ratios.Length; i++)
            {
                StickmanPoseAnimator pose = BuildRig();
                amplitudes[i] = RunAtSpeedRatio(pose, ratios[i], out float speed01, out _);
                Debug.Log($"[P9C-TEST] 속도비 {ratios[i]:F2} -> 측정 속도정규화 {speed01:F3}, 진폭 배율 {amplitudes[i]:F4}");
                TearDown();
            }

            for (int i = 1; i < amplitudes.Length; i++)
            {
                Assert.Greater(amplitudes[i], amplitudes[i - 1],
                    $"속도비 {ratios[i]:F2}의 진폭({amplitudes[i]:F4})이 더 느린 {ratios[i - 1]:F2}의 " +
                    $"진폭({amplitudes[i - 1]:F4})보다 크지 않습니다 — 속도 연동이 끊겼습니다.");
            }

            // 양 끝점이 설계 곡선과 일치하는가. 실측(2026-09-01): 0.8500 / 0.9750 / 1.1000 / 1.2250 / 1.3500
            // — 정확히 lerp(0.85, 1.35, 속도비)다.
            Assert.AreEqual(ExpectedAtRest, amplitudes[0], 0.02f,
                "정지에 가까운 속도인데 진폭 배율이 설계 최소치(종종걸음)가 아닙니다.");
            Assert.AreEqual(ExpectedAtFullSpeed, amplitudes[amplitudes.Length - 1], 0.02f,
                "명령 속도에 도달했는데 진폭 배율이 설계 최대치가 아닙니다.");
            Assert.Less(amplitudes[0], 1f,
                "정지에 가까운 속도인데 진폭 배율이 1 이상입니다 — 종종걸음이 나오지 않습니다.");
        }

        // ========================================================================
        // (2) 실제로 적용된 관절 각도가 커지는가 (배율 숫자가 아니라 **그림**을 잰다)
        // ========================================================================

        [Test]
        public void 빠를수록_엉덩이_스윙_각도_자체가_커진다()
        {
            StickmanPoseAnimator slowPose = BuildRig();
            RunAtSpeedRatio(slowPose, 0.15f, out _, out float slowSwing);
            TearDown();

            StickmanPoseAnimator fastPose = BuildRig();
            RunAtSpeedRatio(fastPose, 1f, out _, out float fastSwing);

            // 실측(2026-09-01): 느림 46.12도 vs 빠름 63.26도 -> 비 1.372.
            Debug.Log($"[P9C-TEST] 실제 적용 엉덩이 스윙 진폭 — 느림 {slowSwing:F2}도 vs 빠름 {fastSwing:F2}도 " +
                $"(비 {(slowSwing > 0.01f ? fastSwing / slowSwing : 0f):F3})");

            // 설계상 진폭비는 1.35/0.925 = 1.46이고, 빠른 쪽은 사이클 주파수가 높아 지수 스무딩에
            // 조금 더 깎이므로 화면에 실제로 나오는 비는 그보다 낮다(실측 약 1.4). 임계 1.15는
            // "변화가 있다"와 "설계대로다" 사이에서 스무딩 오차에 둔감한 선이다.
            Assert.Greater(fastSwing, slowSwing * 1.15f,
                $"빠르게 걸을 때의 엉덩이 스윙({fastSwing:F2}도)이 느릴 때({slowSwing:F2}도)보다 " +
                "15%도 크지 않습니다 — 배율만 바뀌고 실제 포즈에는 반영되지 않았을 수 있습니다.");
        }

        // ========================================================================
        // (3) 보폭도 함께 커진다 — "성큼성큼"의 정의
        // ========================================================================

        [Test]
        public void 빠를수록_한_사이클_이동거리_보폭도_커진다()
        {
            StickmanPoseAnimator slowPose = BuildRig();
            RunAtSpeedRatio(slowPose, 0.15f, out _, out _);
            float slowStride = slowPose.DistancePerCycle;
            TearDown();

            StickmanPoseAnimator fastPose = BuildRig();
            RunAtSpeedRatio(fastPose, 1f, out _, out _);
            float fastStride = fastPose.DistancePerCycle;

            // 실측(2026-09-01): 1.1427 -> 1.6039유닛 (비 1.404).
            Debug.Log($"[P9C-TEST] 한 사이클 이동거리 — 느림 {slowStride:F4}유닛 vs 빠름 {fastStride:F4}유닛");

            Assert.Greater(slowStride, 0f, "보폭이 0입니다 — 가짜 리그의 다리 길이 배선이 깨졌습니다.");
            Assert.Greater(fastStride, slowStride * 1.2f,
                $"빠를 때의 사이클 이동거리({fastStride:F4})가 느릴 때({slowStride:F4})보다 20%도 크지 않습니다 " +
                "— 보폭이 진폭을 따라오지 않으면 발이 미끄러집니다(_distancePerCycle 문서).");
        }

        // ========================================================================
        // (4) 네거티브 컨트롤 — 설정 배율(walkPoseAmplitudeScale)은 여전히 곱셈으로 살아 있다
        // ========================================================================

        [Test]
        public void 설정_진폭배율은_유도값에_그대로_곱해진다()
        {
            StickmanPoseAnimator a = BuildRig();
            float baseline = RunAtSpeedRatio(a, 1f, out _, out _, configAmplitude: 1f);
            TearDown();

            StickmanPoseAnimator b = BuildRig();
            float halved = RunAtSpeedRatio(b, 1f, out _, out _, configAmplitude: 0.5f);

            Assert.AreEqual(baseline * 0.5f, halved, 1e-4f,
                $"설정 진폭배율 0.5가 유도값에 곱해지지 않았습니다(기준 {baseline:F4}, 실측 {halved:F4}) — " +
                "속도 유도가 설정값을 덮어써 사용자가 진폭을 조절할 수 없게 됐을 수 있습니다.");
        }

        /// <summary>진폭배율 0(또는 음수)이 들어오면 1로 폴백하던 기존 가드가 살아 있는가.</summary>
        [Test]
        public void 진폭배율_0_폴백_가드가_유지된다()
        {
            StickmanPoseAnimator pose = BuildRig();
            float applied = RunAtSpeedRatio(pose, 1f, out _, out _, configAmplitude: 0f);

            Assert.AreEqual(ExpectedAtFullSpeed, applied, 0.02f,
                "진폭배율 0이 들어왔을 때 1로 폴백하는 기존 가드가 사라졌습니다 — 다리가 통째로 멈춥니다.");
        }

        // ========================================================================
        // (5) 결정성 — 같은 입력이면 같은 결과 (플레이키 진단의 기준선)
        // ========================================================================

        [Test]
        public void 같은_속도_입력에_대해_결정적이다()
        {
            StickmanPoseAnimator a = BuildRig();
            float first = RunAtSpeedRatio(a, 0.6f, out float s1, out _);
            TearDown();

            StickmanPoseAnimator b = BuildRig();
            float second = RunAtSpeedRatio(b, 0.6f, out float s2, out _);

            Assert.AreEqual(first, second, 1e-6f, "같은 입력인데 진폭 배율이 달라졌습니다.");
            Assert.AreEqual(s1, s2, 1e-6f, "같은 입력인데 속도 정규화 값이 달라졌습니다.");
        }

        // ========================================================================
        // 헬퍼
        // ========================================================================

        /// <summary>
        /// 루트를 <paramref name="speedRatio"/> × 명령속도로 등속 이동시키며 보행 틱을 충분히 돌린다.
        /// 워밍업(속도 스무딩이 자리잡는 구간)을 지난 뒤 한 사이클 이상을 관찰해,
        /// (a) 최종 진폭 배율을 반환하고 (b) 실제 적용된 왼다리 엉덩이 각도의 진폭을 함께 돌려준다.
        /// </summary>
        private float RunAtSpeedRatio(StickmanPoseAnimator pose, float speedRatio,
            out float speed01, out float hipSwingDegrees, float configAmplitude = 1f)
        {
            pose.ResetWalkPhase();
            var settings = new PoseSettingsBuilder().Build();

            float actualSpeed = CommandSpeed * speedRatio;
            float x = 0f;

            // 워밍업 3초 — 속도 스무딩(_smoothedSpeed)이 첫 틱의 명령 속도에서 실제 속도로 수렴할 시간.
            const float WarmupSeconds = 3f;
            for (float t = 0f; t < WarmupSeconds; t += Dt)
            {
                x += actualSpeed * Dt;
                _root.transform.position = new Vector3(x, 0f, 0f);
                pose.TickWalkPose(Dt, CommandSpeed, settings, SmoothingRate, SpeedSmoothingRate,
                    groundingBlend: 1f, amplitudeScale: configAmplitude, strideScale: 0.93f);
            }

            // 관찰 3초 — 실제 적용된 엉덩이 각도의 최대/최소를 모은다(좌우 반전 부호는 최대-최소 폭에
            // 영향을 주지 않으므로 그대로 쓴다).
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            const float ObserveSeconds = 6f;  // 가장 느린 구간(약 0.33Hz)에서도 두 사이클 이상 담기게.
            for (float t = 0f; t < ObserveSeconds; t += Dt)
            {
                x += actualSpeed * Dt;
                _root.transform.position = new Vector3(x, 0f, 0f);
                pose.TickWalkPose(Dt, CommandSpeed, settings, SmoothingRate, SpeedSmoothingRate,
                    groundingBlend: 1f, amplitudeScale: configAmplitude, strideScale: 0.93f);

                pose.GetUpperAngles(out float hipL, out _, out _, out _);
                if (hipL < min) min = hipL;
                if (hipL > max) max = hipL;
            }

            speed01 = pose.WalkSpeed01;
            hipSwingDegrees = max - min;
            return pose.WalkAmplitudeScale;
        }

        /// <summary>실제 프리팹 계층과 <b>이름 규약만</b> 같은 최소 리그(Rigidbody2D/HingeJoint2D 없음).
        /// StickmanPoseAnimator는 이름으로 마디를 찾고 길이는 BoxCollider2D에서 읽으므로 이것으로 충분하다.</summary>
        private StickmanPoseAnimator BuildRig()
        {
            _root = new GameObject("FakeStickman");
            _root.transform.position = Vector3.zero;

            AddLimb("LeftLeg", HipY, LegUpperLength, LegLowerLength);
            AddLimb("RightLeg", HipY, LegUpperLength, LegLowerLength);
            AddLimb("LeftArm", ShoulderY, ArmUpperLength, ArmLowerLength);
            AddLimb("RightArm", ShoulderY, ArmUpperLength, ArmLowerLength);

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

        /// <summary>PoseSettings는 인자 7개짜리 생성자뿐이라 테스트마다 나열하면 읽기 어렵다.
        /// 값 자체는 이 테스트의 관심사가 아니므로(보행 표는 PoseSettings를 쓰지 않는다) 한 곳에 묶는다.</summary>
        private sealed class PoseSettingsBuilder
        {
            public StickmanPoseAnimator.PoseSettings Build()
                => new StickmanPoseAnimator.PoseSettings(
                    legSpread: 12f, armSpread: 40f, idleKnee: 4f, idleElbow: 10f,
                    breathAmplitude: 0f, breathFrequencyHz: 0f, breathArmDegrees: 0f);
        }
    }
}
