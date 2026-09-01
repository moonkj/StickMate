using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 발판 상실 공중 유예 상태(<see cref="StickmanStateId.GroundLossHang"/>) 회귀 잠금
    /// (2026-09-01, 소은 실측 + 리더 결정 "(C) 시간은 두고 연출을 붙인다").
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 — 두 종류
    /// ============================================================================
    /// <b>(1) 갇힘 방지가 최우선이다.</b> 이 상태에 갇히면 캐릭터가 <b>영원히 공중에 뜬다</b> — 원래
    /// 버그("창에서 갑자기 떨어짐")보다 나쁜 결과다. 그래서 나가는 길을 하나도 빠짐없이 각각
    /// 잠그고(H2~H7), 마지막에 <b>어떤 조합으로도 오래 머물 수 없다</b>를 반복 시나리오로 잠근다(H12).
    /// 최후 안전망(유예의 3배 상한)은 정상 경로가 전부 죽은 상황을 인위적으로 만들어 직접 잰다(H11).
    ///
    /// <b>(2) 연출이 "실제로 눈에 보이는 크기"인지 수치로 증명한다.</b> "움직이게 만들었다"는 증명이
    /// 아니다 — 이번 라운드의 핵심 발견이 정확히 그것이다: 상체를 10도 기울여도 끝점이 6.1pt라
    /// <b>육안으로 무의미</b>했다(소은 실측: 1% 미만 화소 변화는 안 보인다 / 프리즈 구간 화소차 0.00%).
    /// 그래서 여기서는 <b>배포 화면 기준 pt</b>로 환산해 그 6.1pt 기준선과 비교한다(H8/H9), 그리고
    /// <b>같은 자를 들고</b> 스위치를 끈 상태를 재서 그 초록이 "재는 방법이 둔해서 나온 것"이 아님을
    /// 보인다(H10 네거티브 컨트롤 — 소은의 IDLE 프리즈 관측을 코드로 재현한다).
    ///
    /// ============================================================================
    /// 왜 pt 환산에 <b>배포 상수</b>를 쓰는가
    /// ============================================================================
    /// batchmode 화면은 640x480이라 약 20pt/유닛이고 배포 화면(982pt / orthographicSize 12)은
    /// 40.9pt/유닛이다. "화면에서 몇 pt로 보이는가"는 <b>배포에서의</b> 값이어야 소은의 실측
    /// (모자 상단 픽셀 추적)과 같은 자가 된다. 그래서 위치는 이 화면에서 재고 환산만
    /// <see cref="DockGeometry.ReferencePointsPerWorldUnit"/>으로 한다.
    ///
    /// ============================================================================
    /// 시간 예산은 전부 벽시계다
    /// ============================================================================
    /// 이 저장소 배치모드 PlayMode는 8,000~13,000fps라 "N프레임" 예산은 밀리초밖에 안 된다
    /// (CLAUDE.md 규칙 / Tests/PlayMode/TestClock.cs). 이 파일의 모든 대기·표본은 초 단위다.
    /// </summary>
    public sealed class GroundLossHangStateTests
    {
        private const string LogPrefix = "[공중유예-TEST]";
        private const float SettleWaitSeconds = 0.7f;

        /// <summary>발판을 물리 바닥에서 이만큼 위에 둔다(월드 유닛) — 유예가 끝나 실제로 떨어져도
        /// 관측 구간 안에 바닥에 닿지 않아야 "떨어졌다"를 순수하게 볼 수 있다.</summary>
        private const float FootholdAboveFloorUnits = 6f;

        private const long TestFootholdHandle = 992001L;

        /// <summary>
        /// ★ <b>육안 무의미 기준선</b>(pt). 소은 실측이 남긴 유일한 숫자 기준이다 — 상체 길이 약 35pt를
        /// 10도 기울이면 끝점이 35·sin(10°) = 6.1pt 움직이는데, 그것이 "물리적으로 안 보이는 변화"로
        /// 판정된 값이다. 이 파일의 연출 단언은 전부 이 값의 배수로 적는다(임의의 숫자를 새로 만들지
        /// 않는다 — 그러면 "보인다"의 정의가 파일마다 갈린다).
        /// </summary>
        private const float InvisibleDatumPoints = 6.1f;

        /// <summary>연속 표본 사이의 간격(초). 소은의 영상 계측이 53fps였으므로 그보다 성긴 30fps
        /// 그리드로 잰다(보수적 — 실제 화면에서는 이보다 더 촘촘히 보인다).</summary>
        private const float SampleGridSeconds = 1f / 30f;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class FixedIntentSource : IMovementIntentSource
        {
            public float Move;
            public float MoveInputX => Move;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        /// <summary>한 스칼라의 "얼마나 크게 / 얼마나 자주 움직였는가"를 동시에 재는 표본기.
        /// 진폭(peak-to-peak)만 재면 아주 느린 표류도 통과하고, 프레임 간 변화만 재면 미세 떨림이
        /// 통과한다 — 소은의 계측이 둘을 함께 본 것과 같은 이유로 둘 다 남긴다.</summary>
        private sealed class Excursion
        {
            private float _min = float.PositiveInfinity;
            private float _max = float.NegativeInfinity;
            private float _gridAnchor;
            private float _gridAccum;
            private bool _hasAnchor;
            private float _maxGridStep;

            public void Sample(float value, float dt)
            {
                if (value < _min) _min = value;
                if (value > _max) _max = value;

                if (!_hasAnchor)
                {
                    _gridAnchor = value;
                    _hasAnchor = true;
                    _gridAccum = 0f;
                    return;
                }
                _gridAccum += dt;
                if (_gridAccum < SampleGridSeconds) return;

                float step = Mathf.Abs(value - _gridAnchor);
                if (step > _maxGridStep) _maxGridStep = step;
                _gridAnchor = value;
                _gridAccum = 0f;
            }

            public float PeakToPeak => _max >= _min ? _max - _min : 0f;
            public float MaxGridStep => _maxGridStep;
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private FixedIntentSource _intent;
        private Vector2 _savedOrigin;
        private TestFootholdService _service;

        private float _footholdTopWorldY;
        private float _footholdTopOsY;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                StickmanBlackboard bb = _agent.Blackboard;
                bb.ReleaseGroundedGravitySuppression();
                if (_originalConfig != null) bb.Config = _originalConfig;
                if (_originalIntent != null) bb.IntentSource = _originalIntent;
                if (_originalPoller != null) bb.FootholdPoller = _originalPoller;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        // ====================================================================
        // 공통 준비 (GroundedGravitySuppressionTests와 같은 재현 환경 보정을 그대로 쓴다)
        // ====================================================================

        private IEnumerator SetUpSingleFoothold()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");
            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;
            // 물리 Dock 계단이 켜져 있으면 논리 발판만의 거동을 관측할 수 없다(DockSinkholeRegressionTests와
            // 같은 이유 — 계단이 있으면 자유낙하 자체가 물리적으로 일어나지 않는다).
            _clonedConfig.dockPhysicsStepEnabled = false;

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 전제 실패 — 씬에 PhysicsGround가 없습니다.");
            float floorTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y;
            _footholdTopWorldY = floorTopWorldY + FootholdAboveFloorUnits;

            Camera cam = bb.MainCamera;

            // 접지 밴드를 **배포 형상과 같은 월드 크기**로 맞춘다(GroundedGravitySuppressionTests의
            // SetUp과 완전히 같은 보정 — 그 파일에 유도 과정이 있다). 이 보정이 없으면 batchmode
            // 화면에서 밴드가 배포의 2배가 되어 재현 자체가 성립하지 않는다.
            float livePointsPerUnit = GroundSensor.ComputeOsPointsPerWorldUnit(cam, _clonedConfig);
            float deployedToleranceWorld = _clonedConfig.groundSnapTolerance * DockGeometry.ReferenceWorldUnitsPerPoint;
            _clonedConfig.groundSnapTolerance = deployedToleranceWorld * livePointsPerUnit;

            _footholdTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(0f, _footholdTopWorldY), _clonedConfig, out _).y;

            _service = new TestFootholdService();
            _service.Footholds.Add(FullWidthFoothold());
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            _intent = new FixedIntentSource();
            bb.IntentSource = _intent;

            Debug.Log($"{LogPrefix} 준비 — 발판 상단 월드Y {_footholdTopWorldY:F3}(OS y={_footholdTopOsY:F1}), " +
                $"유예={_clonedConfig.ResolveGroundLossGraceDuration():F3}초" +
                $"(폴링 {_clonedConfig.footholdPollInterval:F2}초 x {_clonedConfig.groundLossGracePollIntervalMultiplier:F2}), " +
                $"갇힘상한={_clonedConfig.ResolveGroundLossHangHardTimeout():F3}초, " +
                $"무반응 비율={_clonedConfig.groundLossHangReactionDelayRatio:F2}, " +
                $"전조 비율={_clonedConfig.groundLossHangFallTellRatio:F2}, " +
                $"다리 배속={_clonedConfig.groundLossHangLegCycleSpeedMultiplier:F2}, " +
                $"배포 환산 {DockGeometry.ReferencePointsPerWorldUnit:F2}pt/유닛.");
        }

        private PlatformFoothold FullWidthFoothold()
            => new PlatformFoothold(TestFootholdHandle,
                new Rect(0f, _footholdTopOsY, Screen.width, Mathf.Max(1f, Screen.height - _footholdTopOsY)), true);

        private IEnumerator PlaceGroundedAndSettle()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            bb.MoveBodyToWorld(new Vector2(0f, _footholdTopWorldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = TestFootholdHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(StickmanStateId.Idle, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — 발판 위에서 Idle을 유지하지 못했습니다.");
            Assert.AreEqual(TestFootholdHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 전제 실패 — 테스트 발판을 딛고 있지 않습니다.");
        }

        /// <summary>창이 사라진다(발판 목록에서 제거 + 즉시 재열거).</summary>
        private void RemoveFoothold()
        {
            _service.Footholds.Clear();
            _agent.Blackboard.FootholdPoller.PollImmediately();
        }

        /// <summary>창이 돌아온다.</summary>
        private void RestoreFoothold()
        {
            _service.Footholds.Clear();
            _service.Footholds.Add(FullWidthFoothold());
            _agent.Blackboard.FootholdPoller.PollImmediately();
        }

        private float ToleranceWorld()
        {
            float pointsPerUnit = GroundSensor.ComputeOsPointsPerWorldUnit(_agent.Blackboard.MainCamera, _clonedConfig);
            Assert.Greater(pointsPerUnit, 0f, $"{LogPrefix} OS 포인트/월드유닛 환산에 실패했습니다.");
            return _clonedConfig.groundSnapTolerance / pointsPerUnit;
        }

        /// <summary>월드 유닛 -> <b>배포 화면 기준</b> pt(클래스 문서의 "왜 배포 상수인가" 참고).</summary>
        private static float ToDeployedPoints(float worldUnits) => worldUnits * DockGeometry.ReferencePointsPerWorldUnit;

        private Transform FindHead()
        {
            Transform root = _agent.Blackboard.Body.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c != null && c.name == "Head") return c;
            }
            return null;
        }

        // ====================================================================
        // H1 — 승격 자체 + 붙잡음이 그대로 살아 있다
        // ====================================================================

        [UnityTest]
        public IEnumerator H1_발판이_사라지면_Idle이_공중유예_상태로_승격되고_몸은_붙잡혀_있다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            float startY = bb.Body.position.y;
            RemoveFoothold();

            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang,
                0.5f, "발판이 사라졌는데 GroundLossHang으로 승격되지 않았습니다");

            // 유예의 절반 지점에서 본다 — 아직 낙하하면 안 되고, 중력은 눌려 있어야 한다.
            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            yield return new WaitForSeconds(grace * 0.5f);

            float drift = Mathf.Abs(bb.Body.position.y - startY);
            Debug.Log($"{LogPrefix} H1 — 상태={bb.Machine.CurrentStateId}, 세로 이동={drift:F4}유닛" +
                $"({ToDeployedPoints(drift):F2}pt, 허용오차 {ToleranceWorld():F4}유닛), " +
                $"중력억제={bb.IsGroundedGravitySuppressed}, gravityScale={bb.Body.gravityScale:F3}.");

            Assert.AreEqual(StickmanStateId.GroundLossHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 유예 중간에 이미 상태를 벗어났습니다.");
            Assert.IsTrue(bb.IsGroundedGravitySuppressed,
                $"{LogPrefix} 유예 중인데 중력 억제가 풀렸습니다 — 붙잡음이 사라지면 이번 라운드가 " +
                "고치려던 '창에서 갑자기 떨어짐'이 그대로 돌아옵니다(승격이 붙잡음을 깨뜨리면 안 됩니다).");
            Assert.Less(drift, ToleranceWorld(),
                $"{LogPrefix} 유예 동안 몸이 {drift:F4}유닛 움직였습니다 — 붙잡음이 동작하지 않습니다.");
        }

        // ====================================================================
        // H2 — ★갇힘 방지 1: 유예가 끝나면 반드시 나간다
        // ====================================================================

        [UnityTest]
        public IEnumerator H2_유예가_끝나면_반드시_Fall로_나간다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            RemoveFoothold();

            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang,
                0.5f, "GroundLossHang으로 승격되지 않았습니다");
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId != StickmanStateId.GroundLossHang,
                grace * 2f + 0.5f, "유예가 끝났는데도 공중유예 상태를 벗어나지 않았습니다(★갇힘)");

            // 나갔으면 실제로 떨어져야 한다 — "상태만 바뀌고 공중에 붙박이는" 반대쪽 사고 방지.
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"{LogPrefix} H2 — 유예 {grace:F2}초 뒤 상태={bb.Machine.CurrentStateId}, " +
                $"몸Y={bb.Body.position.y:F3}(발판 상단 {_footholdTopWorldY:F3}), " +
                $"중력억제={bb.IsGroundedGravitySuppressed}.");

            Assert.IsFalse(bb.IsGroundedGravitySuppressed,
                $"{LogPrefix} 유예가 끝났는데 중력 억제가 남아 있습니다 — 공중에 붙박입니다.");
            Assert.Less(bb.Body.position.y, _footholdTopWorldY - ToleranceWorld(),
                $"{LogPrefix} 유예가 끝났는데 캐릭터가 그 자리에 그대로 있습니다.");
        }

        // ====================================================================
        // H3 — 탈출: 발판이 돌아오면 지상 상태로 복귀한다(유예의 설계 목적)
        // ====================================================================

        [UnityTest]
        public IEnumerator H3_발판이_돌아오면_Idle로_복귀한다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            float startY = bb.Body.position.y;
            RemoveFoothold();
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang,
                0.5f, "GroundLossHang으로 승격되지 않았습니다");

            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            yield return new WaitForSeconds(grace * 0.5f);
            RestoreFoothold();

            yield return TestClock.WaitForState(bb, StickmanStateId.Idle, 1.0f, holdSeconds: 0.2f);

            float drift = Mathf.Abs(bb.Body.position.y - startY);
            Debug.Log($"{LogPrefix} H3 — 발판 복귀 후 상태={bb.Machine.CurrentStateId}, " +
                $"세로 이동={drift:F4}유닛({ToDeployedPoints(drift):F2}pt), 발판핸들={bb.CurrentFootholdHandle}.");

            Assert.Less(drift, ToleranceWorld(),
                $"{LogPrefix} 유예가 흡수에 성공했는데 몸이 {drift:F4}유닛 움직였습니다 — " +
                "붙잡음이 없으면 튐이 지나가도 발판으로 돌아올 수 없습니다.");
            Assert.AreEqual(TestFootholdHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 복귀 후 원래 발판을 딛고 있지 않습니다.");
        }

        [UnityTest]
        public IEnumerator H3b_이동_의도가_있으면_Walk로_복귀한다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            RemoveFoothold();
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang,
                0.5f, "GroundLossHang으로 승격되지 않았습니다");

            _intent.Move = 1f;   // 걷고 있었다(소은이 "귀엽다"고 판정한 WALK 케이스).
            RestoreFoothold();

            yield return TestClock.WaitForState(bb, StickmanStateId.Walk, 1.0f, holdSeconds: 0.15f);
            Debug.Log($"{LogPrefix} H3b — 이동 의도 {_intent.Move:F2}에서 복귀 상태={bb.Machine.CurrentStateId}.");
            _intent.Move = 0f;
        }

        // ====================================================================
        // H4/H5 — 반대편 절대 조건: 붙잡을 근거가 없으면 이 상태에 들어가지도, 머물지도 않는다
        // ====================================================================

        /// <summary>발판을 화면 왼쪽 40%로 좁히고 오른쪽 아래에 하나 더 둔다(G7과 같은 배치) —
        /// 그래야 "화면 좌우 이탈" 판정이 먼저 발동하지 않아 WalkedOffPreferredFoothold를 순수하게 잰다.</summary>
        private void SplitFootholdsAndStepOutside()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            float lowerOsY = Mathf.Min(Screen.height - 2f, _footholdTopOsY + Screen.height * 0.25f);
            _service.Footholds.Clear();
            _service.Footholds.Add(new PlatformFoothold(TestFootholdHandle,
                new Rect(0f, _footholdTopOsY, Screen.width * 0.4f, Mathf.Max(1f, Screen.height - _footholdTopOsY)), true));
            _service.Footholds.Add(new PlatformFoothold(TestFootholdHandle + 1L,
                new Rect(Screen.width * 0.6f, lowerOsY, Screen.width * 0.4f, Mathf.Max(1f, Screen.height - lowerOsY)), true));
            bb.FootholdPoller.PollImmediately();

            float outsideX = ScreenCoordinateConverter.OsScreenToWorld(bb.MainCamera,
                new Vector2(Screen.width * 0.5f, _footholdTopOsY), 10f, _clonedConfig).x;
            bb.MoveBodyToWorld(new Vector2(outsideX, _footholdTopWorldY));
        }

        [UnityTest]
        public IEnumerator H4_걸어서_모서리를_넘어가면_공중유예에_들어가지_않는다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            SplitFootholdsAndStepOutside();

            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.WalkedOffPreferredFoothold,
                $"{LogPrefix} 전제 실패 — 발판 X 범위 밖인데 WalkedOffPreferredFoothold가 false입니다.");

            bool everHung = false;
            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            yield return TestClock.SampleForSeconds(grace * 1.2f, _ =>
            {
                if (bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang) everHung = true;
            });

            float drop = _footholdTopWorldY - bb.Body.position.y;
            Debug.Log($"{LogPrefix} H4 — 모서리 밖에서 {grace * 1.2f:F2}초: 상태={bb.Machine.CurrentStateId}, " +
                $"처짐={drop:F4}유닛, 공중유예 진입={everHung}.");

            Assert.IsFalse(everHung,
                $"{LogPrefix} 걸어서 모서리를 넘어갔는데 공중유예에 들어갔습니다 — 그건 코요테 개그가 " +
                "아니라 **공중부양**입니다(붙잡을 근거가 애초에 없습니다).");
            Assert.Greater(drop, 0.05f,
                $"{LogPrefix} 모서리 밖으로 나갔는데 내려가지 않았습니다.");
        }

        [UnityTest]
        public IEnumerator H5_유예_중_발밑이_정말_비면_즉시_낙하한다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            RemoveFoothold();
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang,
                0.5f, "GroundLossHang으로 승격되지 않았습니다");

            // 유예 도중에 상황이 "걸어서 모서리를 넘어간 것"으로 바뀐다 — 더 기다릴 이유가 없다.
            SplitFootholdsAndStepOutside();

            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.Fall,
                grace * 0.9f, "발밑이 비었는데 유예가 만료될 때까지 붙잡고 있었습니다");

            Debug.Log($"{LogPrefix} H5 — 발밑이 빈 것을 감지하고 유예 만료({grace:F2}초)를 기다리지 않고 " +
                $"Fall로 나갔습니다(상태={bb.Machine.CurrentStateId}).");
        }

        // ====================================================================
        // H6/H7 — 탈출: 외부에서 상태를 빼앗아도 잔재가 남지 않는다
        // ====================================================================

        /// <summary>
        /// 외부(랙돌 인터럽트/드래그/스펙터클 취소)가 유예 도중 상태를 빼앗아도 <b>갇히지 않는다</b>를
        /// 잰다. 두 가지를 따로 본다:
        /// <list type="number">
        ///   <item><b>전이가 즉시 걸린다</b> — 유예 상태가 ChangeState를 되돌리거나 무시하지 않는다.</item>
        ///   <item><b>결국 반드시 정착한다</b> — 이 상태로 되돌아오는 경로가 있더라도(아래 실측 참고)
        ///     유예 타이머가 리셋되지 않으므로 만료가 반드시 온다.</item>
        /// </list>
        ///
        /// <para>★ 실측으로 확인한 것(H7): 커서가 없는 배치모드에서 <c>Dragged</c>로 전이시키면
        /// <c>DragThrowState.Tick</c>이 첫 틱에 "커서를 한 번도 못 얻었다"며 Idle로 되돌리고, 몸은 아직
        /// 사라진 발판 위 공중이라 같은 프레임의 접지 안전망이 <b>유예로 다시 승격</b>한다. 이건 결함이
        /// 아니라 옳은 거동이다(공중이면 유예가 맞다). 중요한 것은 그 왕복이 <c>_groundLossTimer</c>를
        /// 리셋하지 않는다는 사실이고 — 리셋한다면 잡았다 놓기를 반복해 <b>영원히 공중에 뜰 수 있다</b> —
        /// 아래 두 번째 단언이 정확히 그것을 잠근다.</para>
        /// </summary>
        private IEnumerator AssertForcedTransitionLeavesCleanly(StickmanStateId target)
        {
            yield return PlaceGroundedAndSettle();
            StickmanBlackboard bb = _agent.Blackboard;

            RemoveFoothold();
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang,
                0.5f, "GroundLossHang으로 승격되지 않았습니다");
            Assert.IsTrue(bb.IsGroundedGravitySuppressed, $"{LogPrefix} 전제 실패 — 유예 중인데 억제가 없습니다.");

            bb.Machine.ChangeState(target, isForcedInterrupt: true);
            Assert.AreEqual(target, bb.Machine.CurrentStateId,
                $"{LogPrefix} {target}로 강제 전이를 걸었는데 상태가 바뀌지 않았습니다 — 유예 상태가 " +
                "외부 인터럽트를 막고 있다는 뜻입니다(★갇힘의 가장 직접적인 형태).");

            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            yield return null;
            StickmanStateId afterOneFrame = bb.Machine.CurrentStateId;

            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId != StickmanStateId.GroundLossHang,
                grace * 2f + 0.5f,
                $"{target}로 인터럽트된 뒤에도 공중유예를 벗어나지 못했습니다(★갇힘 — 유예 타이머가 " +
                "인터럽트 왕복에 리셋되고 있을 가능성)");
            yield return new WaitForSeconds(0.2f);

            Debug.Log($"{LogPrefix} 강제 전이 {target} — 1프레임 뒤 상태={afterOneFrame}, " +
                $"정착 상태={bb.Machine.CurrentStateId}, 중력억제={bb.IsGroundedGravitySuppressed}, " +
                $"gravityScale={bb.Body.gravityScale:F3}, 몸Y={bb.Body.position.y:F3}" +
                $"(발판 상단 {_footholdTopWorldY:F3}).");

            Assert.AreNotEqual(StickmanStateId.GroundLossHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} {target}로 강제 전이했는데 결국 공중유예로 돌아와 머물고 있습니다(★갇힘).");
            Assert.IsFalse(bb.IsGroundedGravitySuppressed,
                $"{LogPrefix} 정착 상태 {bb.Machine.CurrentStateId}인데 중력 억제가 남아 있습니다 — " +
                "중력이 꺼진 채 갇혔습니다(이 수정이 막으려는 버그보다 심각합니다).");
        }

        [UnityTest]
        public IEnumerator H6_유예_중_랙돌_강제_인터럽트로_빠져나온다()
        {
            yield return SetUpSingleFoothold();
            yield return AssertForcedTransitionLeavesCleanly(StickmanStateId.Ragdoll);
        }

        [UnityTest]
        public IEnumerator H7_유예_중_드래그로_붙잡히면_빠져나온다()
        {
            yield return SetUpSingleFoothold();
            yield return AssertForcedTransitionLeavesCleanly(StickmanStateId.Dragged);
        }

        // ====================================================================
        // H8 ★핵심★ — 연출이 "화면에서 실제로 보이는" 크기인가 (수치 증명)
        // ====================================================================

        [UnityTest]
        public IEnumerator H8_무반응_박자_뒤에_다리와_팔이_눈에_보이는_크기로_움직인다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            StickmanPoseAnimator pose = bb.GetPoseAnimator();
            Assert.IsNotNull(pose, $"{LogPrefix} 포즈 애니메이터를 찾지 못했습니다.");

            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            float reactionDelay = grace * _clonedConfig.groundLossHangReactionDelayRatio;

            RemoveFoothold();
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang,
                0.5f, "GroundLossHang으로 승격되지 않았습니다");

            var freezeFoot = new Excursion();     // [0, 무반응) 구간의 발 — 여기는 **정지가 정답**이다.
            var liveFoot = new Excursion();       // [무반응, 유예) 구간의 발
            var liveHand = new Excursion();       // 같은 구간의 손
            float legCycles = 0f;
            float prevPhase = pose.WalkPhase01;
            float lifeSeconds = 0f;

            yield return TestClock.SampleForSeconds(grace * 0.98f, t =>
            {
                if (bb.Machine.CurrentStateId != StickmanStateId.GroundLossHang) return false;
                float dt = Time.unscaledDeltaTime;

                pose.GetFootWorldPositions(out Vector2 lFoot, out Vector2 rFoot);
                pose.GetHandWorldPositions(out Vector2 lHand, out _);

                if (t < reactionDelay)
                {
                    freezeFoot.Sample(lFoot.x, dt);
                }
                else
                {
                    lifeSeconds += dt;
                    // 두 값 모두 **루트 위치를 뺀** 상대량이다 — 몸이 통째로 움직여도 오염되지 않게
                    // 해야 "팔다리가 실제로 돌았는가"만 재는 자가 된다(유예 중 몸은 붙잡혀 있지만,
                    // 이 자가 그 전제에 의존하면 전제가 깨질 때 조용히 거짓 초록이 된다).
                    liveFoot.Sample(lFoot.x - rFoot.x, dt);              // 두 발 사이 간격 = 보폭 신호 그 자체
                    liveHand.Sample(lHand.y - bb.Body.position.y, dt);   // 팔 허우적은 세로 진폭이 가장 크다

                    float phase = pose.WalkPhase01;
                    float d = phase - prevPhase;
                    if (d < -0.5f) d += 1f;                   // 위상 랩어라운드
                    if (d > 0f) legCycles += d;
                    prevPhase = phase;
                }
                return true;
            });

            float freezePt = ToDeployedPoints(freezeFoot.PeakToPeak);
            float footPt = ToDeployedPoints(liveFoot.PeakToPeak);
            float footStepPt = ToDeployedPoints(liveFoot.MaxGridStep);
            float handPt = ToDeployedPoints(liveHand.PeakToPeak);
            float handStepPt = ToDeployedPoints(liveHand.MaxGridStep);

            Debug.Log($"{LogPrefix} H8 ★실측(배포 화면 pt 환산, 육안 무의미 기준선 {InvisibleDatumPoints:F1}pt) —\n" +
                $"  · 무반응 구간({reactionDelay * 1000f:F0}ms): 발 이동 진폭 {freezePt:F3}pt " +
                "(여기는 **정지가 정답**이다 — 늦게 알아차리는 한 박자)\n" +
                $"  · 생명신호 구간({lifeSeconds * 1000f:F0}ms): 양발 간격 진폭 {footPt:F2}pt " +
                $"(= 기준선의 {footPt / InvisibleDatumPoints:F1}배), 30fps 한 프레임 최대 이동 {footStepPt:F2}pt\n" +
                $"  · 같은 구간 손 세로 진폭 {handPt:F2}pt(= 기준선의 {handPt / InvisibleDatumPoints:F1}배), " +
                $"30fps 한 프레임 최대 이동 {handStepPt:F2}pt\n" +
                $"  · 다리 사이클 {legCycles:F2}회(= 약 {legCycles * 2f:F1}걸음), " +
                $"평균 {(lifeSeconds > 0f ? legCycles / lifeSeconds : 0f):F2}Hz " +
                $"(걷기 케이던스의 {_clonedConfig.groundLossHangLegCycleSpeedMultiplier:F1}배 설정)");

            Assert.Less(freezePt, 0.5f,
                $"{LogPrefix} 무반응 구간에서 이미 발이 {freezePt:F3}pt 움직였습니다 — 연출의 첫 박자" +
                "(늦게 알아차림)가 사라졌고, 발판을 잃는 순간 즉시 반응하는 그림이 됩니다.");

            Assert.Greater(footPt, InvisibleDatumPoints * 2f,
                $"{LogPrefix} 종종걸음의 양발 간격 진폭이 {footPt:F2}pt뿐입니다 — 육안 무의미 기준선" +
                $"({InvisibleDatumPoints:F1}pt)의 2배를 넘지 못하면 '움직이게 만들었다'일 뿐 " +
                "'실제로 보인다'가 아닙니다(이번 라운드의 핵심 발견).");
            Assert.Greater(footStepPt, 1f,
                $"{LogPrefix} 30fps 한 프레임 사이 발 이동이 {footStepPt:F2}pt뿐입니다 — " +
                "소은이 잰 프리즈 구간(연속 프레임 화소차 0.00%)과 구분되지 않습니다.");

            Assert.Greater(handPt, InvisibleDatumPoints * 2f,
                $"{LogPrefix} 팔 허우적의 세로 진폭이 {handPt:F2}pt뿐입니다(기준선 {InvisibleDatumPoints:F1}pt).");

            Assert.Greater(legCycles, 1f,
                $"{LogPrefix} 생명신호 구간에 다리 사이클이 {legCycles:F2}회뿐입니다 — 한 사이클(=두 걸음)도 " +
                "못 보여주면 '제자리 달리기'로 읽히지 않습니다. 배속(groundLossHangLegCycleSpeedMultiplier) 또는 " +
                "무반응 비율(groundLossHangReactionDelayRatio)을 확인하세요.");
        }

        // ====================================================================
        // H9 — 낙하 전조(상체 기울임)도 "보이는" 크기인가
        // ====================================================================

        [UnityTest]
        public IEnumerator H9_낙하_전조_상체기울임이_기준선을_넘는다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            StickmanPoseAnimator pose = bb.GetPoseAnimator();
            Transform head = FindHead();
            Assert.IsNotNull(head, $"{LogPrefix} 캐릭터 루트 직속에서 Head를 찾지 못했습니다.");

            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            float tellStart = grace * _clonedConfig.groundLossHangFallTellRatio;

            RemoveFoothold();
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang,
                0.5f, "GroundLossHang으로 승격되지 않았습니다");

            float headOffsetAtTell = float.NaN;
            float maxLean = 0f;
            float maxHeadShift = 0f;

            yield return TestClock.SampleForSeconds(grace * 0.98f, t =>
            {
                if (bb.Machine.CurrentStateId != StickmanStateId.GroundLossHang) return false;
                if (t < tellStart) return true;

                float offset = head.position.x - bb.Body.position.x;
                if (float.IsNaN(headOffsetAtTell)) headOffsetAtTell = offset;

                float lean = Mathf.Abs(pose.BodyLeanDegrees);
                if (lean > maxLean) maxLean = lean;
                float shift = Mathf.Abs(offset - headOffsetAtTell);
                if (shift > maxHeadShift) maxHeadShift = shift;
                return true;
            });

            float headShiftPt = ToDeployedPoints(maxHeadShift);
            Debug.Log($"{LogPrefix} H9 ★실측 — 전조 구간({(grace - tellStart) * 1000f:F0}ms) " +
                $"최대 상체 기울임 {maxLean:F2}도(목표 {_clonedConfig.groundLossHangFallTellLeanDegrees:F1}도, " +
                $"지수 감쇠 {_clonedConfig.bodyLeanSmoothingRate:F0}/초로 접근), " +
                $"머리 가로 이동 {headShiftPt:F2}pt(육안 무의미 기준선 {InvisibleDatumPoints:F1}pt의 " +
                $"{headShiftPt / InvisibleDatumPoints:F1}배).");

            Assert.Greater(maxLean, 10f,
                $"{LogPrefix} 전조 기울임이 {maxLean:F2}도뿐입니다 — 소은이 '단독으론 물리적으로 안 보이는 " +
                "변화'로 지목한 10도 구간을 벗어나지 못했습니다.");
            Assert.Greater(headShiftPt, InvisibleDatumPoints,
                $"{LogPrefix} 전조로 머리가 {headShiftPt:F2}pt밖에 움직이지 않았습니다 — " +
                $"기준선({InvisibleDatumPoints:F1}pt) 이하는 '기울였다'고만 말할 수 있고 '보인다'고는 못 합니다. " +
                "groundLossHangFallTellRatio(전조 시작 지점)나 groundLossHangFallTellLeanDegrees를 확인하세요.");
        }

        // ====================================================================
        // H10 ★네거티브 컨트롤★ — 스위치를 끄면 실제로 얼어붙는다
        //     (= H8의 초록이 "재는 방법이 둔해서" 나온 것이 아님을 같은 자로 증명)
        // ====================================================================

        [UnityTest]
        public IEnumerator H10_네거티브컨트롤_스위치를_끄면_같은_구간이_통째로_정지_화면이다()
        {
            yield return SetUpSingleFoothold();
            _clonedConfig.groundLossHangStateEnabled = false;   // 2026-09-01 오전 거동으로 되돌린다
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            StickmanPoseAnimator pose = bb.GetPoseAnimator();
            float startY = bb.Body.position.y;

            RemoveFoothold();

            var foot = new Excursion();
            bool everHung = false;
            float grace = _clonedConfig.ResolveGroundLossGraceDuration();

            yield return TestClock.SampleForSeconds(grace * 0.9f, _ =>
            {
                if (bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang) everHung = true;
                pose.GetFootWorldPositions(out Vector2 l, out Vector2 r);
                foot.Sample(l.x - r.x, Time.unscaledDeltaTime);
            });

            float footPt = ToDeployedPoints(foot.PeakToPeak);
            float drift = Mathf.Abs(bb.Body.position.y - startY);
            Debug.Log($"{LogPrefix} H10 네거티브 컨트롤(스위치 OFF) — 상태={bb.Machine.CurrentStateId}, " +
                $"승격 발생={everHung}, 양발 간격 진폭={footPt:F3}pt, 30fps 한 프레임 최대 이동=" +
                $"{ToDeployedPoints(foot.MaxGridStep):F3}pt, 세로 이동={drift:F4}유닛. " +
                "→ 소은이 실측한 IDLE 프리즈(화소차 0.00%)를 코드로 재현한 값이다.");

            Assert.IsFalse(everHung,
                $"{LogPrefix} 스위치를 껐는데 승격이 일어났습니다 — 네거티브 컨트롤이 성립하지 않습니다.");
            Assert.Less(footPt, InvisibleDatumPoints,
                $"{LogPrefix} 스위치를 껐는데도 발이 {footPt:F3}pt 움직였습니다 — 그렇다면 H8의 초록은 " +
                "이 연출이 만든 움직임이 아니라 다른 무언가를 잰 것입니다(관측 전제 붕괴).");
            Assert.Less(drift, ToleranceWorld(),
                $"{LogPrefix} 스위치를 껐더니 붙잡음까지 사라졌습니다 — 스위치는 **연출만** 꺼야 합니다.");
        }

        // ====================================================================
        // H11 ★갇힘 방지 2★ — 유예 타이머가 죽어도 자기 시계로 나간다(최후 안전망)
        // ====================================================================

        [UnityTest]
        public IEnumerator H11_유예_타이머가_외부에서_계속_리셋돼도_상한에서_강제로_나간다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            RemoveFoothold();
            yield return TestClock.WaitUntil(() => bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang,
                0.5f, "GroundLossHang으로 승격되지 않았습니다");

            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            float hardTimeout = _clonedConfig.ResolveGroundLossHangHardTimeout();

            // ★ 정상 경로를 인위적으로 죽인다 — 매 프레임 유예 타이머를 0으로 되돌리면
            //   GroundedTick은 영원히 "아직 유예 중"이라고 판단한다. 이것이 상태 문서가 말하는
            //   "갇힐 수 있는 유일한 남은 경로"이며, 최후 안전망은 정확히 이 경우를 위해 있다.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("갇힘 방지 안전망 발동"));

            bool leftInTime = false;
            float dwell = 0f;
            yield return TestClock.SampleForSeconds(hardTimeout + 0.6f, _ =>
            {
                if (bb.Machine.CurrentStateId != StickmanStateId.GroundLossHang)
                {
                    leftInTime = true;
                    return false;
                }
                dwell += Time.unscaledDeltaTime;
                bb.ResetGroundLossTimer();
                return true;
            });

            Debug.Log($"{LogPrefix} H11 — 유예 타이머를 매 프레임 리셋하며 관찰: 체류 {dwell:F2}초, " +
                $"상한 {hardTimeout:F2}초(= 유예 {grace:F2}초 x " +
                $"{_clonedConfig.groundLossHangHardTimeoutGraceMultiplier:F1}), 탈출={leftInTime}, " +
                $"최종 상태={bb.Machine.CurrentStateId}.");

            Assert.IsTrue(leftInTime,
                $"{LogPrefix} ★ 갇혔습니다 — 유예 타이머가 리셋되는 동안 공중유예 상태를 영원히 벗어나지 " +
                "못했습니다. 캐릭터가 영원히 공중에 뜬다는 뜻이고, 이것은 원래 버그보다 나쁩니다.");
            Assert.Less(dwell, hardTimeout + 0.3f,
                $"{LogPrefix} 최후 안전망이 상한({hardTimeout:F2}초)보다 한참 늦게 동작했습니다({dwell:F2}초).");
        }

        // ====================================================================
        // H12 ★갇힘 방지 3★ — 창이 반복해서 깜빡여도 절대 오래 머물지 않는다
        // ====================================================================

        [UnityTest]
        public IEnumerator H12_창이_반복해서_깜빡여도_한_번도_오래_머물지_않는다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            float hardTimeout = _clonedConfig.ResolveGroundLossHangHardTimeout();

            // 한 주기 = 사라짐(유예보다 짧게) + 돌아옴. 유예보다 짧게 두는 이유는 이 테스트가 재는 것이
            // "만료 경로"(H2가 이미 잠갔다)가 아니라 **복귀 경로를 반복해도 고착되지 않는가**이기 때문이다.
            float offSeconds = grace * 0.55f;
            float onSeconds = grace * 0.55f;
            float period = offSeconds + onSeconds;
            const int Cycles = 8;

            bool footholdPresent = true;
            float maxDwell = 0f;
            float dwell = 0f;
            int hangEntries = 0;
            bool wasHanging = false;

            yield return TestClock.SampleForSeconds(period * Cycles, t =>
            {
                bool shouldBePresent = Mathf.Repeat(t, period) >= offSeconds;
                if (shouldBePresent != footholdPresent)
                {
                    footholdPresent = shouldBePresent;
                    if (footholdPresent) RestoreFoothold(); else RemoveFoothold();
                }

                bool hanging = bb.Machine.CurrentStateId == StickmanStateId.GroundLossHang;
                if (hanging)
                {
                    if (!wasHanging) hangEntries++;
                    dwell += Time.unscaledDeltaTime;
                    if (dwell > maxDwell) maxDwell = dwell;
                }
                else
                {
                    dwell = 0f;
                }
                wasHanging = hanging;
                return true;
            });

            // 마지막에는 발판이 있는 상태로 되돌려 정착을 확인한다.
            RestoreFoothold();
            yield return new WaitForSeconds(0.5f);

            Debug.Log($"{LogPrefix} H12 — {Cycles}주기(창 사라짐 {offSeconds:F2}초 / 돌아옴 {onSeconds:F2}초) 반복: " +
                $"공중유예 진입 {hangEntries}회, **최장 연속 체류 {maxDwell:F3}초** " +
                $"(유예 {grace:F2}초 / 갇힘 상한 {hardTimeout:F2}초), 최종 상태={bb.Machine.CurrentStateId}, " +
                $"몸Y={bb.Body.position.y:F3}(발판 상단 {_footholdTopWorldY:F3}).");

            Assert.Greater(hangEntries, 0,
                $"{LogPrefix} 창이 {Cycles}번 사라졌는데 공중유예에 한 번도 들어가지 않았습니다 — " +
                "이 테스트가 재려는 상황 자체가 성립하지 않았습니다(관측 전제 붕괴).");
            Assert.Less(maxDwell, grace * 1.5f,
                $"{LogPrefix} 공중유예에 연속 {maxDwell:F3}초 머물렀습니다 — 유예({grace:F2}초)보다 " +
                "한참 길다는 것은 정상 탈출 경로가 어떤 조합에서 동작하지 않는다는 뜻입니다.");
            Assert.Less(maxDwell, hardTimeout,
                $"{LogPrefix} ★ 갇힘 상한({hardTimeout:F2}초)까지 갔습니다.");
            Assert.AreNotEqual(StickmanStateId.GroundLossHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 반복이 끝나고 발판이 돌아왔는데도 공중유예에 남아 있습니다(★갇힘).");
        }
    }
}
