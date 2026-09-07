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
    /// ★ §9-4-B 좁은 보강 회귀 — docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md §9-2/§9-4-B.
    ///
    /// game-architect 재조사(§9-2)가 찾은 실재 결함: <c>AutoWanderController.TryRollEdgeAction()</c>의
    /// 1번 블록(뛰어내리기)은 성공 시 <b>추첨 성공이든 실패든 무조건 return false</b>한다. 그래서 같은
    /// 탐색 폭(≈2.5유닛) 안에 "뛰어내릴 낮은 발판"과 "로프 대역 벽"이 <b>동시에</b> 있으면(예: 작은 틈
    /// 바로 너머에 큰 창), 3번 블록의 로프 벽 평가 자체가 그 프레임에 실행되지 못한다. 이 파일은 그
    /// 정확한 배치를 재현해, §9-4-B의 좁은 순서 교정(존재만 확인 — 추첨 없음)이 실제로 이 원천봉쇄를
    /// 푸는지 <b>자율 배회 그대로</b>(스크립트 펄스 없이, EdgeHopDownTests의 "정책 실측" 관례와 동일
    /// 방식)로 확인한다.
    ///
    /// 방향 편향을 없애기 위해 좌/우 양쪽에 완전히 같은 "낮은 발판 + 로프벽" 쌍을 대칭 배치한다 —
    /// AutoWanderController가 첫 Walk 페이즈에서 좌/우 어느 쪽을 무작위로 고르든 같은 시나리오를
    /// 만나게 해, 방향 선택 자체의 난수 결과에 테스트가 좌우되지 않게 한다.
    /// </summary>
    public sealed class RopeClimbHopDownOrderingTests
    {
        private const string LogPrefix = "[밧줄-hopdown순서-테스트]";

        private const long GroundHandle = 7501L;
        private const long LowDropLeftHandle = 7502L;
        private const long TallWallLeftHandle = 7503L;
        private const long LowDropRightHandle = 7504L;
        private const long TallWallRightHandle = 7505L;

        private const float SettleWaitSeconds = 2.5f;
        private const float MaxObserveSeconds = 20f;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private FootholdPoller _poller;

        [TearDown]
        public void TearDown()
        {
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

        [UnityTest]
        public IEnumerator 로프대역벽이_있으면_hopdown이_원천봉쇄하지_않고_밧줄등반으로_넘어간다()
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

            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            float groundTopOs = h * 0.5f;

            Vector3 groundTopWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig);

            // 파쿠르 상한 — 실측 Dock 낙차 유도(DockGeometry)에 기대지 않고 config 기반 값을 직접
            // 재서 쓴다(AutoWanderController.ResolveStepUpMaxHeightStatic이 폴백할 때 쓰는 것과 같은
            // 식). 우리 시험 발판 목록에는 Dock 신호가 없으므로 실제로 소비될 상한도 이 값과 같다 —
            // 아래에서 발판 목록을 배선한 뒤 ResolveRopeClimbMaxHeight로 다시 한 번 교차 확인한다.
            float stepUpMax = _clonedConfig.ResolveStepUpMaxHeightWorld(bb.CharacterHeightWorld);
            float riseUnits = stepUpMax + 2f;

            // "뛰어내릴 낮은 발판"의 낙차 — EdgeHopDownTests가 이미 검증해 둔 값(뛰어내리기 밴드 안에
            // 확실히 들어가는 실측 낙차)을 그대로 재사용한다. 새 낙차 상수를 만들지 않는다.
            float dropUnits = DockGeometry.ReferenceDockDropWorldUnits;

            Vector2 lowDropWorld = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(groundTopWorld.x, groundTopWorld.y - dropUnits), _clonedConfig, out _);
            Vector2 tallWallWorld = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(groundTopWorld.x, groundTopWorld.y + riseUnits), _clonedConfig, out _);
            float lowDropTopOs = lowDropWorld.y;
            float tallWallTopOs = tallWallWorld.y;

            Assert.Greater(lowDropTopOs, groundTopOs, $"{LogPrefix} 준비 실패 — 낮은 발판이 지면보다 위에 놓였습니다.");
            Assert.Less(tallWallTopOs, groundTopOs, $"{LogPrefix} 준비 실패 — 로프벽이 지면보다 아래에 놓였습니다.");
            Assert.Greater(tallWallTopOs, 0f, $"{LogPrefix} 준비 실패 — 로프벽이 화면 위로 완전히 벗어났습니다(화면이 " +
                "이 낙차/상승폭 조합을 담기엔 너무 작습니다).");

            // Ground(중앙) + 좌/우 각각 "낮은 발판 + 로프벽" 대칭 쌍. 낮은 발판과 로프벽은 **같은
            // 가로 구간**에 겹쳐 둔다 — TryFindHopDownTarget은 "더 낮은" 후보만, TryFindClimbableWall은
            // "충분히 높은" 후보만 보므로 서로 간섭하지 않는다(GroundSensor 실측 확인).
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(GroundHandle, new Rect(w * 0.4f, groundTopOs, w * 0.2f, h - groundTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(LowDropLeftHandle, new Rect(w * 0.2f, lowDropTopOs, w * 0.2f, h - lowDropTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallLeftHandle, new Rect(w * 0.2f, tallWallTopOs, w * 0.2f, h - tallWallTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(LowDropRightHandle, new Rect(w * 0.6f, lowDropTopOs, w * 0.2f, h - lowDropTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallRightHandle, new Rect(w * 0.6f, tallWallTopOs, w * 0.2f, h - tallWallTopOs), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            float groundCenterWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig).x;
            bb.Body.position = new Vector2(groundCenterWorldX, groundTopWorld.y);
            bb.Body.transform.position = new Vector3(groundCenterWorldX, groundTopWorld.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = GroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            // 우리 발판 목록이 배선된 뒤 다시 한 번 상/하한을 확인해 둔다(로그 목적 — 실패해도 위에서
            // 이미 잡힌다).
            GroundSensor.GroundInfo infoNow = bb.SenseGround();
            float ropeMaxNow = AutoWanderController.ResolveRopeClimbMaxHeight(bb, infoNow.GroundWorldY);
            Assert.LessOrEqual(riseUnits, ropeMaxNow,
                $"{LogPrefix} 전제 실패 — 시험벽 상승폭({riseUnits:F3})이 로프등반 상한({ropeMaxNow:F3})을 넘습니다. " +
                "화면이 이 조합을 담기엔 너무 작거나 화면 클램프가 예상보다 좁습니다.");

            Debug.Log($"{LogPrefix} 준비 완료 — 화면 {w:F0}x{h:F0}, 지면 월드Y={groundTopWorld.y:F3}, " +
                $"낮은발판 낙차={dropUnits:F3}유닛, 로프벽 상승폭={riseUnits:F3}유닛" +
                $"(파쿠르상한={stepUpMax:F3}, 로프상한={ropeMaxNow:F3}), 시작 위치={bb.Body.position}, " +
                $"상태={bb.Machine.CurrentStateId}");

            // 배회를 결정론에 가깝게 조인다 — 확률 자체가 아니라 "로프 벽 평가 기회가 실제로 열리는가"를 본다.
            _clonedConfig.wanderIdleDurationMin = 0.05f;
            _clonedConfig.wanderIdleDurationMax = 0.05f;
            _clonedConfig.wanderWalkDurationMin = 12f;
            _clonedConfig.wanderWalkDurationMax = 12f;
            _clonedConfig.wanderDurationJitterRatio = 0f;
            _clonedConfig.wanderSpontaneousTurnChance = 0f;
            _clonedConfig.wanderPostIdleWalkChance = 1f;
            _clonedConfig.wanderPostIdleJumpChance = 0f;
            _clonedConfig.wanderEdgeJumpAttemptChance = 0f;
            _clonedConfig.wanderEdgeTurnPauseMin = 0.15f;
            _clonedConfig.wanderEdgeTurnPauseMax = 0.15f;
            _clonedConfig.ledgeHangChance = 0f; // 이 배치의 낙차는 매달리기 대상이 아니다(DockDropUnits < LedgeHangMinDropDepth).
            _clonedConfig.hopDownChance = 1f;
            _clonedConfig.stepUpChance = 1f;
            _clonedConfig.ropeClimbChance = 1f;

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260907));
            bb.IntentSource = wander;

            bool sawRopeClimbRequested = false;
            bool sawRopeClimbState = false;
            bool sawFall = false;
            long lastHandle = bb.CurrentFootholdHandle;
            float elapsed = 0f;

            while (elapsed < MaxObserveSeconds && !sawRopeClimbRequested && !sawRopeClimbState && !sawFall)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                wander.Tick(dt);

                if (wander.RopeClimbRequested) sawRopeClimbRequested = true;
                if (bb.Machine.CurrentStateId == StickmanStateId.RopeClimb) sawRopeClimbState = true;
                if (bb.Machine.CurrentStateId == StickmanStateId.Fall) sawFall = true;

                long handle = bb.CurrentFootholdHandle;
                if (handle != lastHandle)
                {
                    Debug.Log($"{LogPrefix} 발판 이동 — {lastHandle} -> {handle} (t={elapsed:F2}s, 상태={bb.Machine.CurrentStateId})");
                    lastHandle = handle;
                }
            }

            Debug.Log($"{LogPrefix} 실측 결과 — RopeClimbRequested={sawRopeClimbRequested}, RopeClimb상태진입={sawRopeClimbState}, " +
                $"Fall진입={sawFall}, 총 {elapsed:F2}초, 최종 상태={bb.Machine.CurrentStateId}, 최종 발판핸들={bb.CurrentFootholdHandle}");

            Assert.IsFalse(sawFall,
                $"{LogPrefix} 회귀 — 같은 경계에 로프 대역 벽이 있는데도 뛰어내리기가 그 평가를 원천봉쇄하고 " +
                "Fall로 전이했습니다(§9-4-B 좁은 보강이 동작하지 않습니다).");
            Assert.IsTrue(sawRopeClimbRequested || sawRopeClimbState,
                $"{LogPrefix} {MaxObserveSeconds:F0}초 안에 밧줄등반이 요청/진입되지 않았습니다 — " +
                "hopDownChance=stepUpChance=ropeClimbChance=1인데도 발동하지 않았다면 §9-4-B 보강이 " +
                "적용되지 않았거나 다른 회귀가 있다는 뜻입니다.");
        }
    }
}
