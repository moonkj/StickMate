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
    /// ★ 사용자 신고 "캐릭터가 창에서 가끔 갑자기 떨어짐"의 <b>근본 원인 1</b> 회귀 잠금
    /// (디버거 가설 H4 — 미반증/유력).
    ///
    /// ============================================================================
    /// 무엇이 버그였나
    /// ============================================================================
    /// 창/Dock 상단은 <b>논리 발판일 뿐 물리 콜라이더가 없다.</b> 그래서 "서 있기"는 매 프레임
    /// <c>SnapToGround()</c> 한 번으로만 유지되는데, <b>그 사이에도 중력은 계속 적분된다.</b>
    /// 한 프레임의 자유낙하가 접지 허용오차(<c>groundSnapTolerance</c>)를 넘으면 그 프레임이 끝나는
    /// 순간 접지 판정이 실패하고, 그 한 프레임이 유예까지 통째로 소진하므로 <b>단 한 프레임으로 낙하가
    /// 확정된다</b> — 창은 1픽셀도 움직이지 않았는데.
    ///
    /// 임계 프레임시간은 <see cref="GroundSensor.ComputeGroundLossFrameTimeThreshold"/>가 계산한다
    /// (배포 형상에서 약 182ms). 그런데 이 앱의 절전 프레임페이싱 티어
    /// <c>FramePacingTier.DisplayOff</c>는 <see cref="FramePacingPolicy.DisplayOffTargetFps"/>fps다 —
    /// 즉 <b>임계를 상시 넘는 동작 등급이 실제로 존재한다.</b> G4가 그 대소 관계를 수치로 잠근다.
    ///
    /// ============================================================================
    /// 이 테스트가 "긴 프레임"을 실제로 만드는 방법
    /// ============================================================================
    /// batchmode의 실제 프레임은 1ms 미만이라 그냥 기다려서는 이 상황이 재현되지 않는다(이 저장소가
    /// PetFallSyncTests에서 한 번 데인 harness 결함이다). 그래서 <c>Physics2D.simulationMode</c>를
    /// <c>Script</c>로 바꾸고 <c>Physics2D.Simulate(1/DisplayOffTargetFps)</c>를 직접 호출한다 —
    /// <b>물리 적분 구간의 길이 자체를 250ms로 만든다.</b> 프레임 순서(Update -> yield null 재개)에
    /// 따라 에이전트가 중력 억제를 얹은 직후에 그 긴 스텝이 돌므로, 실제 저프레임 등급과 같은 조건이다.
    /// </summary>
    public sealed class GroundedGravitySuppressionTests
    {
        private const string LogPrefix = "[중력억제-TEST]";
        private const float SettleWaitSeconds = 0.7f;

        /// <summary>발판을 물리 바닥에서 이만큼 위에 둔다 — 한 번의 250ms 자유낙하(약 1.84유닛)로도
        /// 바닥에 닿지 않아야 "떨어졌다"를 순수하게 관측할 수 있다.</summary>
        private const float FootholdAboveFloorUnits = 6f;

        private const long TestFootholdHandle = 991001L;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;
        private SimulationMode2D _savedSimulationMode;
        private TestFootholdService _service;

        private float _footholdTopWorldY;
        private float _footholdTopOsY;
        private float _baselineGravityScale;

        [TearDown]
        public void TearDown()
        {
            Physics2D.simulationMode = _savedSimulationMode;
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
        // 공통 준비
        // ====================================================================

        private IEnumerator SetUpSingleFoothold()
        {
            _savedSimulationMode = Physics2D.simulationMode;

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
            // 같은 이유 — 계단이 있으면 자유낙하 자체가 물리적으로 일어나지 않아 "돌아갈 것 같다"짜리가 된다).
            _clonedConfig.dockPhysicsStepEnabled = false;

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 전제 실패 — 씬에 PhysicsGround가 없습니다.");
            float floorTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y;
            _footholdTopWorldY = floorTopWorldY + FootholdAboveFloorUnits;

            Camera cam = bb.MainCamera;

            // ★★ 재현 환경 보정 — **월드 공간 접지 밴드를 배포 형상과 같게 맞춘다.**
            // groundSnapTolerance는 OS 포인트 단위인데, 그 포인트가 월드에서 얼마나 큰지는 화면
            // 기하에 달려 있다: 배포 화면(982pt / orthographicSize 12)은 40.9pt/유닛이고 batchmode
            // 기본 화면(640x480)은 20pt/유닛이다. 즉 아무 보정 없이 20pt를 쓰면 이 테스트의 접지
            // 밴드가 **배포의 2배(1.0유닛 vs 0.489유닛)** 로 관대해져, 배포에서는 떨어지는 상황이
            // 여기서는 안 떨어진다 = 재현이 성립하지 않는다.
            // 그래서 "배포에서의 월드 밴드"를 이 화면의 포인트로 환산해 되돌린다(숫자를 새로 적지
            // 않는다 — DockGeometry의 배포 환산 상수에서 유도한다).
            float livePointsPerUnit = GroundSensor.ComputeOsPointsPerWorldUnit(cam, _clonedConfig);
            float deployedToleranceWorld = _clonedConfig.groundSnapTolerance * DockGeometry.ReferenceWorldUnitsPerPoint;
            _clonedConfig.groundSnapTolerance = deployedToleranceWorld * livePointsPerUnit;

            _footholdTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(0f, _footholdTopWorldY), _clonedConfig, out _).y;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(TestFootholdHandle,
                new Rect(0f, _footholdTopOsY, Screen.width, Mathf.Max(1f, Screen.height - _footholdTopOsY)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.IntentSource = new StillIntentSource();

            // 중력 배율의 기준선은 "억제가 확실히 풀린 상태"에서 실측한다 — 씬 에셋의 값을 추측하지 않는다.
            bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);
            yield return null;
            _baselineGravityScale = bb.Body.gravityScale;
            Assert.Greater(_baselineGravityScale, 0f,
                $"{LogPrefix} 전제 실패 — 공중(Fall) 상태인데 gravityScale이 0입니다(중력이 꺼진 채 갇혔다는 뜻).");

            Debug.Log($"{LogPrefix} 준비 — 물리바닥 상단 {floorTopWorldY:F3}, 발판 상단 월드Y {_footholdTopWorldY:F3}" +
                $"(OS y={_footholdTopOsY:F1}), 기준 gravityScale={_baselineGravityScale:F2}, " +
                $"허용오차={_clonedConfig.groundSnapTolerance:F2}pt(={deployedToleranceWorld:F4}유닛, " +
                $"배포와 동일하게 보정 / 이 화면 {livePointsPerUnit:F2}pt-유닛), " +
                $"유예={_clonedConfig.ResolveGroundLossGraceDuration():F3}초(폴링 {_clonedConfig.footholdPollInterval:F2}초).");
        }

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

        /// <summary>월드 유닛으로 환산한 접지 허용오차(= 이만큼 어긋나면 접지가 풀린다).</summary>
        private float ToleranceWorld()
        {
            float pointsPerUnit = GroundSensor.ComputeOsPointsPerWorldUnit(_agent.Blackboard.MainCamera, _clonedConfig);
            Assert.Greater(pointsPerUnit, 0f, $"{LogPrefix} OS 포인트/월드유닛 환산에 실패했습니다.");
            return _clonedConfig.groundSnapTolerance / pointsPerUnit;
        }

        /// <summary>
        /// "프레임이 <see cref="FramePacingPolicy.DisplayOffTargetFps"/>fps로 튄" 상황을 steps번 만든다.
        /// 반환값은 그동안 관측한 발판 상단 대비 <b>최대 처짐</b>(월드 유닛, 아래로가 양수).
        /// </summary>
        private IEnumerator RunLongFrames(int steps, System.Action<float> onDone)
        {
            float longFrameSeconds = 1f / FramePacingPolicy.DisplayOffTargetFps;
            // ★ 한 번의 거대한 스텝이 아니라 **엔진과 똑같이 fixedDeltaTime 단위로 쪼갠 substep**으로
            //   250ms를 채운다. 실제 Unity도 긴 프레임 하나에 FixedUpdate를 여러 번 몰아서 돌리므로
            //   (Time.maximumDeltaTime 한도 내), 한 방에 250ms를 적분하면 자유낙하가 2배로 나와
            //   "실제보다 가혹한 조건"에서 통과하는 테스트가 된다.
            int subSteps = Mathf.Max(1, Mathf.CeilToInt(longFrameSeconds / Time.fixedDeltaTime));
            float subStepSeconds = longFrameSeconds / subSteps;
            StickmanBlackboard bb = _agent.Blackboard;

            Physics2D.simulationMode = SimulationMode2D.Script;
            float worstDrop = 0f;
            for (int i = 0; i < steps; i++)
            {
                // 프레임 순서: (물리 없음) -> StickmanAgent.Update(억제 적용) -> 여기(코루틴 재개).
                yield return null;
                for (int k = 0; k < subSteps; k++) Physics2D.Simulate(subStepSeconds);
                float drop = _footholdTopWorldY - bb.Body.position.y;
                if (drop > worstDrop) worstDrop = drop;
            }
            Physics2D.simulationMode = _savedSimulationMode;
            onDone(worstDrop);
            Debug.Log($"{LogPrefix} 긴 프레임 재현 — {longFrameSeconds * 1000f:F0}ms를 " +
                $"{subSteps}개 substep({subStepSeconds * 1000f:F1}ms)으로 x {steps}회, 최대 처짐={worstDrop:F4}유닛.");
        }

        // ====================================================================
        // G1 — 접지 중에는 중력이 실제로 꺼져 있다
        // ====================================================================

        [UnityTest]
        public IEnumerator G1_접지중에는_중력이_꺼져있다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            Assert.IsTrue(bb.IsGroundedGravitySuppressed,
                $"{LogPrefix} 접지 중인데 중력 억제가 걸려 있지 않습니다 — 위치 스냅에만 의존하면 " +
                "긴 프레임 하나로 떨어집니다(근본 원인 1).");
            Assert.AreEqual(0f, bb.Body.gravityScale, 1e-6f,
                $"{LogPrefix} 접지 중 gravityScale이 0이 아닙니다({bb.Body.gravityScale:F3}).");

            Debug.Log($"{LogPrefix} G1 — 접지 중 gravityScale={bb.Body.gravityScale:F3}(기준 {_baselineGravityScale:F2}).");
        }

        // ====================================================================
        // G2 ★핵심★ — 프레임이 250ms로 튀어도 접지 상태면 떨어지지 않는다
        // ====================================================================

        [UnityTest]
        public IEnumerator G2_프레임이_절전티어_길이로_튀어도_접지면_떨어지지_않는다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            float toleranceWorld = ToleranceWorld();
            float worstDrop = 0f;
            yield return RunLongFrames(8, d => worstDrop = d);

            StickmanBlackboard bb = _agent.Blackboard;
            float longFrameSeconds = 1f / FramePacingPolicy.DisplayOffTargetFps;
            Debug.Log($"{LogPrefix} G2 — 긴 프레임 {longFrameSeconds * 1000f:F0}ms x 8회 후 " +
                $"최대 처짐={worstDrop:F4}유닛(허용오차 {toleranceWorld:F4}유닛), 상태={bb.Machine.CurrentStateId}, " +
                $"발판핸들={bb.CurrentFootholdHandle}.");

            Assert.Less(worstDrop, toleranceWorld,
                $"{LogPrefix} 프레임이 {longFrameSeconds * 1000f:F0}ms로 튀는 동안 캐릭터가 접지 허용오차를 " +
                "넘어 내려갔습니다 — 접지 중 중력 억제가 동작하지 않습니다(신고 '창에서 갑자기 떨어짐' 재발).");
            Assert.AreEqual(StickmanStateId.Idle, bb.Machine.CurrentStateId,
                $"{LogPrefix} 긴 프레임을 겪은 뒤 상태가 {bb.Machine.CurrentStateId}로 바뀌었습니다 — 낙하가 시작됐다는 뜻입니다.");
            Assert.AreEqual(TestFootholdHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 딛고 있던 발판을 놓았습니다.");
        }

        // ====================================================================
        // G2n — 네거티브 컨트롤: 억제를 끄면 같은 시나리오에서 실제로 떨어진다
        //       (= 이 테스트가 "돌아갈 것 같다"가 아니라 진짜 버그를 잡는다는 증거)
        // ====================================================================

        [UnityTest]
        public IEnumerator G2n_네거티브컨트롤_억제를_끄면_같은조건에서_밴드를_벗어난다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            _clonedConfig.groundedGravitySuppressionEnabled = false;   // 수정을 되돌린다
            yield return null;                                          // 다음 Update에서 억제가 벗겨진다

            float toleranceWorld = ToleranceWorld();
            float worstDrop = 0f;
            yield return RunLongFrames(3, d => worstDrop = d);

            Debug.Log($"{LogPrefix} G2n — 억제 OFF에서 최대 처짐={worstDrop:F4}유닛(허용오차 {toleranceWorld:F4}유닛).");
            Assert.Greater(worstDrop, toleranceWorld,
                $"{LogPrefix} 억제를 껐는데도 밴드를 벗어나지 않았습니다 — 이 테스트가 재현하는 상황이 " +
                "애초에 성립하지 않는다는 뜻이라 G2의 초록은 아무것도 증명하지 못합니다(관측 전제 붕괴).");
        }

        // ====================================================================
        // G3 — 중력이 꺼진 채 갇히지 않는다(억제보다 위험한 반대쪽 버그)
        // ====================================================================

        private IEnumerator AssertGravityRestoredAfterTransition(StickmanStateId target)
        {
            yield return PlaceGroundedAndSettle();
            StickmanBlackboard bb = _agent.Blackboard;
            Assert.IsTrue(bb.IsGroundedGravitySuppressed, $"{LogPrefix} 전제 실패 — 억제가 걸려 있지 않습니다.");

            bb.Machine.ChangeState(target, isForcedInterrupt: true);
            yield return null;   // Update 한 번 = 맨 앞에서 벗기고, 맨 끝에서 다시 얹을지 판정

            Assert.IsFalse(bb.IsGroundedGravitySuppressed,
                $"{LogPrefix} {target}로 전이했는데 중력 억제가 남아 있습니다 — 중력이 꺼진 채 갇혔습니다.");
            Assert.AreEqual(_baselineGravityScale, bb.Body.gravityScale, 1e-4f,
                $"{LogPrefix} {target}로 전이한 뒤 gravityScale이 원래 값으로 복구되지 않았습니다 " +
                $"(현재 {bb.Body.gravityScale:F3}, 기준 {_baselineGravityScale:F2}).");
        }

        [UnityTest]
        public IEnumerator G3a_Fall로_전이하면_중력이_되살아난다()
        {
            yield return SetUpSingleFoothold();
            yield return AssertGravityRestoredAfterTransition(StickmanStateId.Fall);
        }

        [UnityTest]
        public IEnumerator G3b_Jump으로_전이하면_중력이_되살아난다()
        {
            yield return SetUpSingleFoothold();
            yield return AssertGravityRestoredAfterTransition(StickmanStateId.Jump);
        }

        [UnityTest]
        public IEnumerator G3c_Ragdoll로_전이하면_중력이_되살아난다()
        {
            yield return SetUpSingleFoothold();
            yield return AssertGravityRestoredAfterTransition(StickmanStateId.Ragdoll);
        }

        [UnityTest]
        public IEnumerator G3d_발판이_사라지면_중력이_되살아나고_실제로_떨어진다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            Assert.IsTrue(bb.IsGroundedGravitySuppressed, $"{LogPrefix} 전제 실패 — 억제가 걸려 있지 않습니다.");

            // 창이 실제로 닫힌 상황(발판 목록에서 사라짐). 유예가 끝나면 Fall로 가야 하고,
            // 그 순간부터는 중력이 반드시 살아 있어야 한다 — 억제가 "떨어져야 할 때 못 떨어지게"
            // 만들면 그것도 버그다.
            _service.Footholds.Clear();
            bb.FootholdPoller.PollImmediately();

            float grace = _clonedConfig.ResolveGroundLossGraceDuration();
            yield return new WaitForSeconds(grace + 0.5f);

            Debug.Log($"{LogPrefix} G3d — 발판 제거 후 {grace + 0.5f:F2}초: 상태={bb.Machine.CurrentStateId}, " +
                $"gravityScale={bb.Body.gravityScale:F3}, 억제={bb.IsGroundedGravitySuppressed}, " +
                $"몸Y={bb.Body.position.y:F3}(발판 상단 {_footholdTopWorldY:F3}).");

            Assert.IsFalse(bb.IsGroundedGravitySuppressed,
                $"{LogPrefix} 발판이 사라졌는데 중력 억제가 남아 있습니다 — 공중에 붙박입니다.");
            Assert.Less(bb.Body.position.y, _footholdTopWorldY - ToleranceWorld(),
                $"{LogPrefix} 발판이 사라졌는데 캐릭터가 그 자리에 그대로 있습니다.");
        }

        // ====================================================================
        // G6 — 유예의 설계 목적: 창 열거가 한 번 튀어도 제자리를 지킨다 (근본 원인 2)
        // ====================================================================

        [UnityTest]
        public IEnumerator G6_창열거가_한_폴링주기_튀어도_제자리를_지키고_상태가_유지된다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            float startY = bb.Body.position.y;

            // "열거가 한 번 실패했다"를 재현한다 — 목록이 통째로 비었다가 한 폴링 주기 뒤 그대로 돌아온다.
            var saved = new List<PlatformFoothold>(_service.Footholds);
            _service.Footholds.Clear();
            bb.FootholdPoller.PollImmediately();

            yield return new WaitForSeconds(_clonedConfig.footholdPollInterval);

            _service.Footholds.Clear();
            _service.Footholds.AddRange(saved);
            bb.FootholdPoller.PollImmediately();
            yield return new WaitForSeconds(0.2f);

            float drift = Mathf.Abs(bb.Body.position.y - startY);
            Debug.Log($"{LogPrefix} G6 — 열거 튐 {_clonedConfig.footholdPollInterval:F2}초 뒤 복구: " +
                $"세로 이동={drift:F4}유닛(허용오차 {ToleranceWorld():F4}), 상태={bb.Machine.CurrentStateId}, " +
                $"발판핸들={bb.CurrentFootholdHandle}, 유예={_clonedConfig.ResolveGroundLossGraceDuration():F3}초.");

            Assert.AreEqual(StickmanStateId.Idle, bb.Machine.CurrentStateId,
                $"{LogPrefix} 창 열거가 한 번 튀었을 뿐인데 낙하했습니다 — 유예가 폴링 주기를 덮지 못하거나 " +
                "유예 동안 몸이 붙잡히지 않았습니다(근본 원인 2 재발).");
            Assert.Less(drift, ToleranceWorld(),
                $"{LogPrefix} 유예 동안 몸이 {drift:F4}유닛 움직였습니다 — 유예만 늘리고 몸을 놔두면 " +
                "튐이 지나간 뒤 이미 밴드 밖이라 되돌아올 수 없습니다.");
        }

        // ====================================================================
        // G7 — 반대편 절대 조건: 정말 모서리를 넘어갔으면 붙잡지 않는다(공중부양 금지)
        // ====================================================================

        [UnityTest]
        public IEnumerator G7_걸어서_모서리를_넘어가면_붙잡지_않고_바로_떨어진다()
        {
            yield return SetUpSingleFoothold();
            yield return PlaceGroundedAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            Camera cam = bb.MainCamera;

            // 발판을 화면 왼쪽 40%로 좁히고, 오른쪽 40%에 **훨씬 아래쪽** 발판을 하나 더 둔다.
            // 오른쪽 발판은 "화면(발판 좌우 범위) 이탈" 판정이 먼저 발동하지 않게 하려는 장치다 —
            // 그래야 이 테스트가 재는 것이 CheckScreenBoundsOrFall이 아니라 WalkedOffPreferredFoothold가 된다.
            float lowerOsY = Mathf.Min(Screen.height - 2f, _footholdTopOsY + Screen.height * 0.25f);
            _service.Footholds.Clear();
            _service.Footholds.Add(new PlatformFoothold(TestFootholdHandle,
                new Rect(0f, _footholdTopOsY, Screen.width * 0.4f, Mathf.Max(1f, Screen.height - _footholdTopOsY)), true));
            _service.Footholds.Add(new PlatformFoothold(TestFootholdHandle + 1L,
                new Rect(Screen.width * 0.6f, lowerOsY, Screen.width * 0.4f, Mathf.Max(1f, Screen.height - lowerOsY)), true));
            bb.FootholdPoller.PollImmediately();

            // 두 발판 사이(화면 50%) — 전체 발판 좌우 범위 안이지만, 딛고 있던 발판의 밖이다.
            float outsideX = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(Screen.width * 0.5f, _footholdTopOsY), 10f, _clonedConfig).x;
            bb.MoveBodyToWorld(new Vector2(outsideX, _footholdTopWorldY));

            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsFalse(info.Grounded, $"{LogPrefix} 전제 실패 — 발판 밖인데 접지로 판정됩니다.");
            Assert.IsTrue(info.WalkedOffPreferredFoothold,
                $"{LogPrefix} 발판이 목록에 그대로 있고 X만 벗어났는데 WalkedOffPreferredFoothold가 false입니다 — " +
                "이 판정이 없으면 유예 동안 캐릭터가 허공에 붙박입니다.");

            // 유예 길이의 절반만 기다려도 이미 내려가고 있어야 한다(붙잡지 않는다는 뜻).
            float half = _clonedConfig.ResolveGroundLossGraceDuration() * 0.5f;
            yield return new WaitForSeconds(half);

            float drop = _footholdTopWorldY - bb.Body.position.y;
            Debug.Log($"{LogPrefix} G7 — 모서리 밖에서 {half:F3}초 뒤 처짐={drop:F4}유닛, " +
                $"억제={bb.IsGroundedGravitySuppressed}, 상태={bb.Machine.CurrentStateId}.");
            Assert.IsFalse(bb.IsGroundedGravitySuppressed,
                $"{LogPrefix} 발판 바깥인데 중력이 억제돼 있습니다 — 공중부양입니다.");
            Assert.Greater(drop, 0.05f,
                $"{LogPrefix} 발판 바깥으로 나갔는데 {half:F3}초 동안 {drop:F4}유닛밖에 안 내려갔습니다.");
        }

        // ====================================================================
        // G4 — 왜 이 버그가 상시 발생할 수 있었는가(수치로 잠금)
        // ====================================================================

        [UnityTest]
        public IEnumerator G4_배포형상에서_임계프레임시간이_절전티어_프레임간격보다_짧다()
        {
            yield return SetUpSingleFoothold();

            // ★ 반드시 **배포 형상**(화면 982pt / orthographicSize 12 -> 40.9pt/유닛)에서 재야 한다.
            //   batchmode 테스트 화면은 기본 640x480이라 20pt/유닛이고, 그 화면의 임계(약 261ms)를
            //   배포 임계(약 182ms)로 착각하면 이 단언이 실제 출하 조건과 무관해진다.
            //   (이 환산 상수는 DockGeometry가 "테스트 배치 재현용"으로 이미 갖고 있는 그 값이다.)
            // 원본(배포) 설정 + 배포 환산으로 잰다. _clonedConfig는 위 SetUp에서 이 화면에 맞게
            // groundSnapTolerance를 포인트 단위로 재환산해 뒀으므로 여기 쓰면 이중 보정이 된다.
            float deployedThreshold = GroundSensor.ComputeGroundLossFrameTimeThreshold(
                DockGeometry.ReferencePointsPerWorldUnit, _originalConfig);
            float liveThreshold = GroundSensor.ComputeGroundLossFrameTimeThreshold(
                _agent.Blackboard.MainCamera, _clonedConfig);
            float displayOffFrame = 1f / FramePacingPolicy.DisplayOffTargetFps;

            Debug.Log($"{LogPrefix} G4 — 배포 형상 임계={deployedThreshold * 1000f:F1}ms " +
                $"({DockGeometry.ReferencePointsPerWorldUnit:F2}pt/유닛), " +
                $"이 테스트 화면 임계={liveThreshold * 1000f:F1}ms " +
                $"({GroundSensor.ComputeOsPointsPerWorldUnit(_agent.Blackboard.MainCamera, _clonedConfig):F2}pt/유닛, " +
                $"Screen={Screen.width}x{Screen.height}), " +
                $"DisplayOff 티어 프레임 간격={displayOffFrame * 1000f:F0}ms" +
                $"({FramePacingPolicy.DisplayOffTargetFps}fps), 엔진 최대 timestep={Time.maximumDeltaTime * 1000f:F0}ms.");

            Assert.Less(deployedThreshold, displayOffFrame,
                $"{LogPrefix} 배포 형상의 임계 프레임시간이 절전 티어의 프레임 간격보다 깁니다 — 이 대소 " +
                "관계가 뒤집히면 근본 원인 1의 전제가 사라진 것이므로(예: gravityScale/허용오차 변경) " +
                "중력 억제의 필요성 자체를 다시 판단해야 합니다.");
            Assert.Less(deployedThreshold, Time.maximumDeltaTime,
                $"{LogPrefix} 배포 형상의 임계 프레임시간이 엔진 최대 timestep보다 깁니다 — 위와 같은 취지입니다.");

            // 밴드 보정이 실제로 배포와 같은 월드 밴드를 만들었는지 함께 잠근다 — 이게 어긋나면
            // G2/G2n이 배포와 다른 조건에서 도는 것이므로 그쪽 초록도 의미를 잃는다.
            Assert.AreEqual(deployedThreshold, liveThreshold, deployedThreshold * 0.02f,
                $"{LogPrefix} 밴드 보정 후에도 이 화면의 임계({liveThreshold * 1000f:F1}ms)가 배포 " +
                $"임계({deployedThreshold * 1000f:F1}ms)와 다릅니다 — SetUp의 재현 환경 보정이 깨졌습니다.");
        }

        // ====================================================================
        // G5 — 진단 함수(DescribeGroundLoss)가 수정 후에도 정확한 사유를 보고한다
        // ====================================================================

        [UnityTest]
        public IEnumerator G5_진단함수가_억제중에는_프레임끊김을_원인으로_지목하지_않는다()
        {
            yield return SetUpSingleFoothold();

            Camera cam = _agent.Blackboard.MainCamera;
            var footholds = new List<PlatformFoothold>(_service.Footholds);
            // 발이 발판 상단보다 한참 아래(= 세로 이탈)인 상황 + 아주 긴 프레임.
            var footWorld = new Vector2(0f, _footholdTopWorldY - 3f);
            // ★ "임계를 확실히 넘는 프레임"은 **이 화면의** 임계에서 유도한다. 고정 250ms를 쓰면
            //   batchmode 화면(임계 약 261ms)에서는 임계 미만이라 검사하려던 분기에 도달조차 못 한다
            //   (첫 실행에서 실제로 그렇게 헛돌았다 — 그 함정을 여기 남겨 다시 밟지 않게 한다).
            float liveThreshold = GroundSensor.ComputeGroundLossFrameTimeThreshold(cam, _clonedConfig);
            float longFrame = liveThreshold * 1.5f;
            Assert.Greater(longFrame, liveThreshold,
                $"{LogPrefix} 전제 실패 — 시험용 프레임 시간이 임계를 넘지 않습니다.");

            string withSuppression = GroundSensor.DescribeGroundLoss(cam, footWorld, footholds,
                _clonedConfig, TestFootholdHandle, longFrame, gravitySuppressedWhileGrounded: true);
            string withoutSuppression = GroundSensor.DescribeGroundLoss(cam, footWorld, footholds,
                _clonedConfig, TestFootholdHandle, longFrame, gravitySuppressedWhileGrounded: false);

            Debug.Log($"{LogPrefix} G5 억제 ON  — {withSuppression}");
            Debug.Log($"{LogPrefix} G5 억제 OFF — {withoutSuppression}");

            StringAssert.Contains("원인이 아닙니다", withSuppression,
                $"{LogPrefix} 억제 중인데도 진단이 프레임 끊김(사유 d)을 원인으로 남깁니다 — " +
                "다음 조사자가 이미 반증된 방향을 다시 파게 됩니다.");
            StringAssert.Contains("사유 d: 프레임 끊김", withoutSuppression,
                $"{LogPrefix} 억제가 꺼진 상태에서는 사유 (d)를 정확히 지목해야 합니다 — " +
                "진단 함수를 남겨 둔 이유가 이것입니다.");
        }

        [UnityTest]
        public IEnumerator G5b_진단함수가_발판소멸을_사유_a로_지목한다()
        {
            yield return SetUpSingleFoothold();

            Camera cam = _agent.Blackboard.MainCamera;
            string why = GroundSensor.DescribeGroundLoss(cam, new Vector2(0f, _footholdTopWorldY),
                new List<PlatformFoothold>(), _clonedConfig, TestFootholdHandle, 0f);
            Debug.Log($"{LogPrefix} G5b — {why}");
            StringAssert.Contains("사유 (a)", why,
                $"{LogPrefix} 발판이 목록에서 사라진 경우를 사유 (a)로 지목하지 못했습니다.");
        }
    }
}
