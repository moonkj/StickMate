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
    /// ★★ 사용자 신고 회귀 잠금(2026-09-06, 디버거 규명):
    /// <b>"마우스로 던졌을때 바닥에 못서고 넘어짐"</b>
    ///
    /// ============================================================================
    /// 무엇이 고장나 있었나 (실측 — 추측 아님)
    /// ============================================================================
    /// <c>States/ThrowTumbleState.ConfirmLanding</c>은 착지 순간 <c>v.y</c>만 지우고 <c>v.x</c>는
    /// 그대로 뒀다. 그 잔여 수평 속도를 <c>States/LandingCrouchState</c>가 지수감쇠
    /// (<see cref="StickConfig.landingCrouchHorizontalDamping"/> = k = 12)로 죽이는데,
    /// <b>지수감쇠의 총 이동거리는 정확히 |vx|/k</b>다:
    /// <code>∫₀^∞ |vx| e^(−kt) dt = |vx| / k</code>
    /// 실측 vx = 2.77 → <b>9.5 OS-pt</b>를 미끄러졌고, 그것이 딛은 발판 가로범위를 <b>2.4pt</b>
    /// 벗어나 접지를 잃고 Fall로 전이했다. 사용자에게는 "착지는 했는데 곧바로 넘어짐"으로 보인다.
    ///
    /// <para>수정은 착지 순간 <b>진입 속도를 발판 안쪽으로 묶는 것</b>이고
    /// (<c>ThrowTumbleState.ClampLandingSlideToFootholdEdge</c>), 스위치는
    /// <see cref="StickConfig.throwTumbleLandingSlideClampEnabled"/>다.
    /// <b>감쇠 자체는 건드리지 않는다</b> — 그러면 모든 착지의 미끄러짐이 함께 사라진다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것
    /// ============================================================================
    ///  T1  발판 <b>가장자리 근처</b>에 던져 착지시켰을 때, 미끄러짐이 발판 안에서 멈추고
    ///      Fall로 빠지지 않는다.
    ///  T1n (네거티브) 클램프 스위치를 끄면 <b>같은 궤적</b>이 실제로 발판을 벗어나 Fall이 된다
    ///      (= 이 시나리오가 정말 결함을 재현한다는 증거).
    ///  T2  발판 <b>한가운데</b>에 착지하면 클램프가 <b>걸리지 않는다</b> — 미끄러짐 연출이
    ///      그대로 남는다(과교정 방지).
    ///
    /// ============================================================================
    /// ★ 왜 "Fall로 안 빠졌다" 한 비트만 보면 안 되는가
    /// ============================================================================
    /// 그 비트는 <b>시나리오가 애초에 넘치지 않았을 때도</b> 초록이다. 그래서 T1/T1n은 매번
    /// <c>ThrowTumbleState.LastLandingSlide*</c> 진단 창구로 <b>"이 착지가 정말 넘칠 착지였는가"</b>
    /// (미끄러질 거리 &gt; 가장자리까지 남은 거리)를 먼저 못박고, 그 다음에 결과를 본다.
    /// 못박히지 않으면 <b>실패</b>로 끝난다 — 재현하지 못한 시나리오가 조용히 초록이 되는 것이
    /// 이 저장소가 반복해서 당한 거짓 통과의 형태다.
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(States/Core만 쓴다). Windows 영향: 없음.</para>
    /// </summary>
    public sealed class ThrowLandingEdgeSlideTests
    {
        private const string LogPrefix = "[착지미끄러짐]";

        private const long PlatformHandle = -21L;

        /// <summary>착지 순간의 잔여 수평 속도(월드 유닛/초). 실측 신고값을 그대로 쓴다.</summary>
        private const float LandingVelocityX = 2.77f;

        /// <summary>발판 위 낙하 높이(월드 유닛). 공중 회전 계획(ThrowTumbleState.TryPlanRotation)이
        /// 한 바퀴를 상한 각속도(720도/초) 안에 넣으려면 착지까지 0.6초 이상이 필요하다 —
        /// 8유닛이면 약 0.74초라 여유가 있다. 짧게 잡으면 회전 계획이 실패해 상태가 Fall로 빠지고
        /// <b>ConfirmLanding을 아예 지나가지 않는다</b>(= 이 테스트가 아무 것도 재지 못한다).</summary>
        private const float DropHeight = 8f;

        /// <summary>발판 상단을 물리 바닥보다 이만큼 올린다 — 가장자리를 벗어나면 <b>실제로 떨어지도록</b>.</summary>
        private const float PlatformLiftAboveFloor = 3f;

        private const float SettleWaitSeconds = 1.0f;
        private const float FlightObserveSeconds = 3.0f;
        private const float AfterLandingObserveSeconds = 1.2f;

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

        private TestFootholdService _service;
        private float _platformTopWorldY;
        private float _platformRightEdgeWorldX;

        private readonly List<string> _trace = new List<string>();
        private bool _sawLandingCrouch;
        private bool _fellAfterLanding;

        [TearDown]
        public void TearDown()
        {
            StickmanEventBus.StateTransitioned -= OnTransition;
            if (_agent != null && _agent.Blackboard != null)
            {
                if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        private void OnTransition(StateTransitionEvent e)
        {
            StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
            Vector2 p = bb != null && bb.Body != null ? bb.Body.position : Vector2.zero;
            _trace.Add($"f={Time.frameCount} {e.From}->{e.To}{(e.IsForcedInterrupt ? "(강제)" : "")} 몸={p.ToString("F3")}");
            if (e.To == StickmanStateId.LandingCrouch) _sawLandingCrouch = true;
            if (_sawLandingCrouch && e.To == StickmanStateId.Fall) _fellAfterLanding = true;
        }

        private string Trace() => _trace.Count == 0 ? "(전이 없음)" : string.Join("\n    ", _trace);

        // ============================================================================
        // 준비 — 물리 바닥보다 3유닛 위에 "가장자리가 있는" 논리 발판 한 장
        // ============================================================================

        private IEnumerator SetUpElevatedPlatform()
        {
            _trace.Clear();
            _sawLandingCrouch = false;
            _fellAfterLanding = false;

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

            // Dock 물리 계단과 사각지대 회수는 "가장자리를 넘어가면 떨어진다"를 흐린다 — 이 파일이
            // 재는 것은 **착지 직후 수평 이동 거리** 하나다.
            _clonedConfig.dockPhysicsStepEnabled = false;
            _clonedConfig.sinkholeLiftRecoveryEnabled = false;

            GameObject ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 씬에서 PhysicsGround를 찾지 못했습니다.");
            float floorTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y;
            _platformTopWorldY = floorTopWorldY + PlatformLiftAboveFloor;

            Camera cam = bb.MainCamera;
            float platformTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(0f, _platformTopWorldY), _clonedConfig, out _).y;

            // 발판은 화면 왼쪽 55%만 덮는다 — 오른쪽 45%가 "가장자리 바깥"이다.
            float w = Screen.width;
            float h = Screen.height;
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(PlatformHandle,
                new Rect(0f, platformTopOsY, w * 0.55f, Mathf.Max(1f, h - platformTopOsY)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.IntentSource = new StillIntentSource();

            // ★ 가장자리 X는 **프로덕션이 쓰는 그 함수**에서 받는다. 테스트가 좌표 변환을 다시 적으면
            //   기준과 대상이 갈라져도 아무도 모른다(이 저장소의 반복 사고 유형).
            bool hasEdge = bb.TryGetFootholdEdgeWorld(PlatformHandle, 1, out _, out _platformRightEdgeWorldX);
            Assert.IsTrue(hasEdge, $"{LogPrefix} 발판 오른쪽 경계를 조회하지 못했습니다 — FootholdPoller 배선 확인.");

            StickmanEventBus.StateTransitioned += OnTransition;

            Debug.Log($"{LogPrefix} 준비 — 물리바닥 상단 y={floorTopWorldY:F3}, 발판 상단 y={_platformTopWorldY:F3}, " +
                $"발판 오른쪽 경계 x={_platformRightEdgeWorldX:F3}, 수평감쇠={_clonedConfig.landingCrouchHorizontalDamping:F1}/초, " +
                $"클램프 스위치={_clonedConfig.throwTumbleLandingSlideClampEnabled}, " +
                $"여유비={_clonedConfig.throwTumbleLandingSlideEdgeSafety01:F2}.");
        }

        /// <summary>
        /// 지정한 "가장자리까지 남을 거리"에 착지하도록 <b>포물선을 역산</b>해서 던진다.
        ///
        /// <para>왜 실제 드래그&amp;던지기가 아니라 상태 직접 진입인가: 이 파일이 재는 것은
        /// <b>착지 순간의 수평 속도 처리</b>이고, 그러려면 착지 지점을 발판 가장자리에서 수 cm 단위로
        /// 맞춰야 한다. 커서 이력으로 그 정밀도를 만들 수는 없다. 던지기 진입 자체(IsCleanThrow
        /// 판정, 커서 속도 계산)는 Tests/PlayMode/ThrowTumbleTests가 이미 실제 경로로 잠근다 —
        /// 여기서는 그 뒤의 ConfirmLanding만 겨눈다.</para>
        /// </summary>
        private void ThrowToLandWithRemaining(float targetRemaining)
        {
            StickmanBlackboard bb = _agent.Blackboard;

            // ★ 중력 배율은 **설정에서** 읽는다. Rigidbody2D.gravityScale을 읽으면 안 된다 —
            //   접지 중에는 StickmanBlackboard.ApplyGroundedGravitySuppression이 그 값을 0으로
            //   눌러 두고, 이 함수는 캐릭터가 아직 서 있는 동안 호출된다. 실제로 첫 실행에서
            //   g=0 -> 비행시간 Infinity -> 시작 x가 -Infinity가 되어 세 케이스가 전부 죽었다.
            //   (공중으로 나가는 순간 억제가 풀려 물리는 설정값으로 돈다.)
            float g = Mathf.Abs(Physics2D.gravity.y) * _clonedConfig.gravityScale;
            Assert.Greater(g, 0.0001f,
                $"{LogPrefix} 유효 중력이 0입니다(Physics2D.gravity.y={Physics2D.gravity.y}, " +
                $"gravityScale={_clonedConfig.gravityScale}) — 포물선 역산이 불가능합니다.");
            float flightSeconds = Mathf.Sqrt(2f * DropHeight / g);
            float startX = _platformRightEdgeWorldX - targetRemaining - LandingVelocityX * flightSeconds;
            var start = new Vector2(startX, _platformTopWorldY + DropHeight);
            var velocity = new Vector2(LandingVelocityX, 0f);

            bb.Body.bodyType = RigidbodyType2D.Dynamic;
            bb.MoveBodyToWorld(start);
            bb.Body.linearVelocity = velocity;
            bb.LastThrowVelocity = velocity;
            bb.CurrentFootholdHandle = 0L;
            bb.ResetGroundLossTimer();

            _trace.Clear();
            _sawLandingCrouch = false;
            _fellAfterLanding = false;
            bb.Machine.ChangeState(StickmanStateId.ThrowTumble, isForcedInterrupt: true);

            Debug.Log($"{LogPrefix} 던짐 — 시작=({startX:F3},{start.y:F3}), 속도={velocity.ToString("F2")}, " +
                $"낙하 {DropHeight:F1}유닛(중력 {g:F2} -> 예상 비행 {flightSeconds:F3}초), " +
                $"목표 착지 x={_platformRightEdgeWorldX - targetRemaining:F3}(가장자리 {_platformRightEdgeWorldX:F3}에서 " +
                $"{targetRemaining:F3}유닛 안쪽), 예상 미끄러짐={LandingVelocityX / _clonedConfig.landingCrouchHorizontalDamping:F3}유닛.");
        }

        /// <summary>착지(LandingCrouch 진입)까지 벽시계 예산으로 기다린다.</summary>
        private IEnumerator WaitForLanding()
        {
            float t = 0f;
            while (t < FlightObserveSeconds && !_sawLandingCrouch)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private ThrowTumbleState Tumble()
        {
            return _agent.Blackboard.Machine.GetState(StickmanStateId.ThrowTumble) as ThrowTumbleState;
        }

        /// <summary>"이 착지가 정말 넘칠 착지였는가"를 값으로 못박는다. 못박히지 않으면 실패다.</summary>
        private void AssertOvershootScenarioReproduced(ThrowTumbleState tumble, string label)
        {
            Assert.IsNotNull(tumble, $"{LogPrefix} [{label}] ThrowTumble 상태 인스턴스를 얻지 못했습니다.");

            Assert.IsTrue(_sawLandingCrouch,
                $"{LogPrefix} [{label}] 던진 캐릭터가 무릎앉아 착지로 이어지지 않았습니다 — " +
                "ConfirmLanding을 아예 지나가지 않았다는 뜻이라 이 케이스는 아무 것도 재지 못했습니다" +
                "(회전 계획 실패로 Fall에 빠졌을 가능성: DropHeight를 늘리십시오).\n" +
                $"    전이추적:\n    {Trace()}");

            float remaining = tumble.LastLandingSlideRemainingToEdge;
            float slide = tumble.LastLandingSlideDistance;

            Assert.IsFalse(float.IsNaN(remaining) || float.IsNaN(slide),
                $"{LogPrefix} [{label}] 착지 시 발판 경계를 재지 못했습니다(남은={remaining}, 미끄러질거리={slide}) — " +
                "ThrowTumbleState.ClampLandingSlideToFootholdEdge가 발판 핸들/감쇠 가드에서 빠져나갔습니다.\n" +
                $"    전이추적:\n    {Trace()}");

            Assert.Greater(slide, remaining,
                $"{LogPrefix} [{label}] 이 착지는 애초에 발판을 넘치지 않는 착지였습니다" +
                $"(미끄러질 거리 {slide:F4} <= 남은 거리 {remaining:F4}). 그러면 '넘어지지 않았다'는 결과는 " +
                "수정의 증거가 아니라 시나리오 실패입니다 — 목표 잔여 거리를 줄이거나 착지 속도를 올리십시오.\n" +
                $"    전이추적:\n    {Trace()}");
        }

        // ============================================================================
        // T1 — 가장자리 근처 착지: 발판 안에서 멈춘다
        // ============================================================================

        [UnityTest]
        public IEnumerator T1_LandingNearEdgeStaysOnPlatform()
        {
            yield return SetUpElevatedPlatform();

            float slide = LandingVelocityX / _clonedConfig.landingCrouchHorizontalDamping;
            ThrowToLandWithRemaining(slide * 0.55f);   // 미끄러질 거리의 55%만 남긴다 = 확실히 넘친다
            yield return WaitForLanding();

            ThrowTumbleState tumble = Tumble();
            AssertOvershootScenarioReproduced(tumble, "T1 가장자리");

            Assert.IsTrue(tumble.LastLandingSlideClamped,
                $"{LogPrefix} [T1] 넘칠 착지인데 클램프가 걸리지 않았습니다" +
                $"(남은={tumble.LastLandingSlideRemainingToEdge:F4}, 미끄러질거리={tumble.LastLandingSlideDistance:F4}, " +
                $"스위치={_clonedConfig.throwTumbleLandingSlideClampEnabled}).");

            yield return new WaitForSeconds(AfterLandingObserveSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            float footX = bb.Body.position.x;
            Debug.Log($"{LogPrefix} [T1] 결과 — 최종 발 x={footX:F4}(가장자리 {_platformRightEdgeWorldX:F4}, " +
                $"여유 {(_platformRightEdgeWorldX - footX):F4}유닛), 최종상태={bb.Machine.CurrentStateId}, " +
                $"발판핸들={bb.CurrentFootholdHandle}, 착지후Fall={_fellAfterLanding}\n    전이추적:\n    {Trace()}");

            Assert.IsFalse(_fellAfterLanding,
                $"{LogPrefix} 회귀 — 착지한 뒤 미끄러져 발판을 벗어나 Fall이 됐습니다. " +
                "사용자 신고 '마우스로 던졌을때 바닥에 못서고 넘어짐'의 직접 재현입니다.\n" +
                $"    전이추적:\n    {Trace()}");

            Assert.LessOrEqual(footX, _platformRightEdgeWorldX,
                $"{LogPrefix} 회귀 — 최종 발 x({footX:F4})가 발판 오른쪽 경계({_platformRightEdgeWorldX:F4})를 " +
                "넘었습니다. 지금은 우연히 접지가 유지됐더라도 한 프레임만 어긋나면 떨어집니다.\n" +
                $"    전이추적:\n    {Trace()}");

            Assert.AreEqual(PlatformHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 착지한 발판을 그대로 딛고 있지 않습니다(현재 핸들={bb.CurrentFootholdHandle}).\n" +
                $"    전이추적:\n    {Trace()}");
        }

        // ============================================================================
        // T1n — 네거티브 컨트롤: 클램프를 끄면 같은 궤적이 실제로 떨어진다
        // ============================================================================

        [UnityTest]
        public IEnumerator T1n_NegativeControl_WithoutClampTheSameThrowSlidesOff()
        {
            yield return SetUpElevatedPlatform();
            _clonedConfig.throwTumbleLandingSlideClampEnabled = false;   // 수정을 되돌린다

            float slide = LandingVelocityX / _clonedConfig.landingCrouchHorizontalDamping;
            ThrowToLandWithRemaining(slide * 0.55f);
            yield return WaitForLanding();

            ThrowTumbleState tumble = Tumble();
            AssertOvershootScenarioReproduced(tumble, "T1n 네거티브");

            Assert.IsFalse(tumble.LastLandingSlideClamped,
                $"{LogPrefix} [T1n] 스위치를 껐는데 클램프가 걸렸습니다 — 네거티브 컨트롤이 성립하지 않습니다.");

            yield return new WaitForSeconds(AfterLandingObserveSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            Debug.Log($"{LogPrefix} [T1n] 결과 — 최종 발 x={bb.Body.position.x:F4}(가장자리 {_platformRightEdgeWorldX:F4}), " +
                $"최종상태={bb.Machine.CurrentStateId}, 착지후Fall={_fellAfterLanding}\n    전이추적:\n    {Trace()}");

            Assert.IsTrue(_fellAfterLanding,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 클램프를 껐는데도 발판을 벗어나지 않았습니다. " +
                "그러면 T1의 초록은 '수정이 동작한다'가 아니라 '애초에 안 떨어지는 시나리오였다'일 수 있습니다 — " +
                "관측창을 다시 설계해야 합니다.\n" +
                $"    전이추적:\n    {Trace()}");
        }

        // ============================================================================
        // T2 — 과교정 방지: 발판 한가운데 착지는 클램프가 걸리지 않는다(미끄러짐 연출 보존)
        // ============================================================================

        [UnityTest]
        public IEnumerator T2_LandingWithRoomDoesNotClamp()
        {
            yield return SetUpElevatedPlatform();

            float slide = LandingVelocityX / _clonedConfig.landingCrouchHorizontalDamping;
            ThrowToLandWithRemaining(slide * 6f);   // 여유가 6배 — 넘칠 이유가 없다
            yield return WaitForLanding();

            ThrowTumbleState tumble = Tumble();
            Assert.IsNotNull(tumble, $"{LogPrefix} [T2] ThrowTumble 상태 인스턴스를 얻지 못했습니다.");
            Assert.IsTrue(_sawLandingCrouch,
                $"{LogPrefix} [T2] 무릎앉아 착지로 이어지지 않았습니다.\n    전이추적:\n    {Trace()}");

            float remaining = tumble.LastLandingSlideRemainingToEdge;
            float measuredSlide = tumble.LastLandingSlideDistance;
            Debug.Log($"{LogPrefix} [T2] 여유 착지 — 남은={remaining:F4}유닛, 미끄러질거리={measuredSlide:F4}유닛, " +
                $"클램프={tumble.LastLandingSlideClamped}.");

            Assert.Greater(remaining, measuredSlide,
                $"{LogPrefix} [T2] 여유 착지를 의도했는데 실제로는 넘치는 착지였습니다" +
                $"(남은 {remaining:F4} <= 미끄러질거리 {measuredSlide:F4}) — 이 케이스가 '과교정 없음'을 재지 못합니다.");

            Assert.IsFalse(tumble.LastLandingSlideClamped,
                $"{LogPrefix} [T2] 과교정 — 발판 여유가 충분한 착지인데도 수평 속도를 깎았습니다. " +
                "이 수정은 '가장자리에서만 개입'이며, 평범한 착지의 미끄러짐 연출은 그대로여야 합니다.");

            yield return new WaitForSeconds(AfterLandingObserveSeconds);
            Assert.IsFalse(_fellAfterLanding,
                $"{LogPrefix} [T2] 여유 착지인데 발판을 벗어났습니다.\n    전이추적:\n    {Trace()}");
        }
    }
}
