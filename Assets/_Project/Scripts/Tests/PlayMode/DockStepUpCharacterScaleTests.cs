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
    /// ★ 캐릭터 **배율 × Dock 되올라오기** — 2026-08-31 사용자 신고
    /// "맥에서 캐릭터 크기를 키우면 독 아래에서 독 위로 안 올라옴".
    ///
    /// ============================================================================
    /// 기존 커버리지가 왜 이걸 못 잡았나
    /// ============================================================================
    /// Tests/PlayMode/DockTileSizeStepUpTests는 **tilesize**를 16~128로 흔들며 되올라오기를 잠갔지만
    /// 배율은 배포값(0.75) 하나뿐이었다. 반대로 CharacterScaleInvarianceTests는 배율을 흔들지만
    /// **되올라오기 왕복 자체는 돌리지 않는다**(밴드/임계값 산술만 본다). 그래서 "배율이 커질 때만
    /// 되올라오기가 죽는" 조합이 두 파일 사이의 빈틈으로 빠져나갔다.
    ///
    /// ============================================================================
    /// 무엇을 잠그는가 (두 층)
    /// ============================================================================
    /// (A) **판정층(결정론)** — 배회 AI가 되올라가기를 평가하는 그 거리에서 벽 탐지가 성립하는가.
    ///     배회 AI는 "발판 경계까지 남은 거리 &lt;= 경계 판정 거리(StickmanBlackboard.EdgeStopDistanceWorld)"가
    ///     되는 **첫 프레임에 딱 한 번** 경계 행동을 추첨하고(AutoWanderController._edgeActionRolledThisLeg),
    ///     실패하면 그 자리에서 멈춰 돌아선다. 즉 벽 탐지는 **최대 그 거리에서** 성립해야 한다.
    ///     경계 판정 거리는 2026-08-30부터 배율에서 유도되지만(0.4 x 배율 + 0.1), 벽 탐지의
    ///     "경계 근처인가" 게이트는 StickConfig.parkourDetectionRadius(0.5) **절대값**이었다 —
    ///     배율 1.0을 넘는 순간 판정 거리가 게이트를 추월해 되올라가기가 **구조적으로** 죽는다
    ///     (교차점은 정확히 배율 1.000 — Tests/EditMode/DockGeometryInvariantTests가 산술로 잠근다).
    /// (B) **왕복층(실물)** — 실제로 안전망 위에서 Dock 위까지 올라오는가(사용자가 본 그 증상).
    ///
    /// tilesize는 이 개발 머신 실측값(49) 하나로 고정한다 — 이 파일이 흔드는 축은 **배율**이며,
    /// tilesize 축은 이미 DockTileSizeStepUpTests가 덮는다.
    /// </summary>
    public sealed class DockStepUpCharacterScaleTests
    {
        private const string LogPrefix = "[DOCK-SCALE]";

        // 실제 앱의 합성 발판 핸들(FallbackPlatformWindowService)과 같은 값이어야 실측 낙차 조회가 Dock을 찾는다.
        private const long DockHandle = -2L;
        private const long NetLeftHandle = -1L;
        private const long NetRightHandle = -3L;

        private const float SettleWaitSeconds = 2.0f;
        private const float RoundTripObserveSeconds = 25f;
        private const int FixedWanderSeed = 20260831;
        private const float StartInsetFromScreenEdgeUnits = 0.15f;

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
        private FootholdPoller _originalPoller;
        private IMovementIntentSource _originalIntent;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private float _dockTopWorldY;
        private float _floorTopWorldY;
        private float _dockLeftWorldX;
        private float _dockRightWorldX;

        private readonly List<string> _trace = new List<string>();
        private bool _sawParkourClimb;

        [TearDown]
        public void TearDown()
        {
            StickmanEventBus.StateTransitioned -= OnTransition;
            if (_agent != null && _agent.Blackboard != null)
            {
                if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
            }
            // ★ ApplyCharacterScale은 **에이전트가 든 원본 에셋**에 런타임 배율을 쓴다([NonSerialized]라
            //   디스크에는 못 닿지만 같은 세션의 다음 테스트에는 샌다). 반드시 지운다.
            if (_originalConfig != null) _originalConfig.ClearRuntimeCharacterScale();
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _originalIntent = null;
            _agent = null;
        }

        private void OnTransition(StateTransitionEvent e)
        {
            if (_trace.Count < 200) _trace.Add($"{e.From}->{e.To}");
            if (e.To == StickmanStateId.ParkourClimb) _sawParkourClimb = true;
        }

        // ============================================================================
        // (A) 판정층 — 배회 AI가 실제로 추첨하는 그 거리에서 벽 탐지가 성립하는가 (결정론)
        // ============================================================================

        [UnityTest] public IEnumerator ClimbProbeReachesDecisionDistance_Scale075() { yield return AssertClimbProbeReachesDecisionDistance(0.75f); }
        [UnityTest] public IEnumerator ClimbProbeReachesDecisionDistance_Scale100() { yield return AssertClimbProbeReachesDecisionDistance(1.00f); }
        [UnityTest] public IEnumerator ClimbProbeReachesDecisionDistance_Scale125() { yield return AssertClimbProbeReachesDecisionDistance(1.25f); }
        /// <summary>다이얼 상한(StickConfig.MaxCharacterScale). 상한이 바뀌면 이 테스트가 자동으로 따라간다 —
        /// 숫자를 박아 두면 상한을 올린 사람이 이 경로를 검증하지 않고 지나간다.</summary>
        [UnityTest] public IEnumerator ClimbProbeReachesDecisionDistance_ScaleMax() { yield return AssertClimbProbeReachesDecisionDistance(StickConfig.MaxCharacterScale); }

        private IEnumerator AssertClimbProbeReachesDecisionDistance(float scale)
        {
            yield return SetUpDockLayout(DockGeometry.ReferenceDockDropWorldUnits);
            ApplyScale(scale);

            StickmanBlackboard bb = _agent.Blackboard;

            // 배회 AI가 "경계에 도달했다"고 보는 바로 그 거리 = 되올라가기를 평가하는 **최대** 거리.
            float decisionDistance = bb.EdgeStopDistanceWorld;
            Assert.Greater(decisionDistance, 0f, $"{LogPrefix} 경계 판정 거리가 0입니다(유도가 죽었습니다).");

            // 안전망 오른쪽 조각 위, Dock(왼쪽) 경계에서 정확히 그 거리만큼 떨어진 자리에 세운다.
            bb.MoveBodyToWorld(new Vector2(_dockRightWorldX + decisionDistance, _floorTopWorldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = NetRightHandle;
            bb.ResetGroundLossTimer();

            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded,
                $"{LogPrefix} 준비 실패 — 안전망 위 접지가 성립하지 않습니다(배율 {scale:F2}).");

            float remaining = bb.Body.position.x - info.CurrentFootholdLeftWorldX;
            bool found = bb.TryFindClimbableWall(info, -1, out long wallHandle, out float wallTopY);

            // ★ 네거티브 컨트롤 — 유도를 **거치지 않는** 옛 경로(게이트 = parkourDetectionRadius 절대값)를
            //   같은 자리에서 그대로 호출한다. 수정 전 코드가 이 호출과 정확히 같았으므로, 이 값이
            //   큰 배율에서 false라는 사실이 곧 "고치기 전에는 실패했다"의 직접 증거다
            //   (수정 후에도 재현 가능한 증거라 회귀 잠금이 된다).
            bool legacyFound = GroundSensor.TryFindClimbableWall(bb.MainCamera, bb.Body.position, info, -1,
                bb.FootholdPoller.CachedFootholds, _clonedConfig, out _, out _);

            Debug.Log($"{LogPrefix} (A) 배율 {scale:F2}× — 몸 물리 반폭 {bb.CharacterPhysicalHalfWidthWorld:F3}, " +
                $"경계 판정 거리(=추첨 거리) {decisionDistance:F3}, 실제 잔여 {remaining:F3}, " +
                $"parkourDetectionRadius 설정 {_clonedConfig.parkourDetectionRadius:F3} → 유도 도달거리 " +
                $"{bb.EdgeProbeReachWorld:F3}, 벽 탐지={found}(핸들 {wallHandle}, 상단Y {wallTopY:F3}), " +
                $"**유도 없는 옛 경로**={legacyFound}, " +
                $"Dock 오른쪽 모서리 {_dockRightWorldX:F3}, 위치 x={bb.Body.position.x:F3}");

            // 옛 경로는 배율 1.0을 넘으면 반드시 실패해야 한다 — 실패하지 않는다면 이 테스트는
            // 아무 것도 잠그지 않는다(재현 조건이 바뀐 것이므로 근거부터 다시 세울 것).
            if (scale > 1.05f)
            {
                Assert.IsFalse(legacyFound,
                    $"{LogPrefix} 네거티브 컨트롤 실패 — 배율 {scale:F2}×에서 유도 없는 옛 경로도 벽을 " +
                    $"찾았습니다(추첨 거리 {decisionDistance:F3} vs 옛 게이트 " +
                    $"{_clonedConfig.parkourDetectionRadius:F3}). 재현 조건이 바뀌었습니다.");
            }

            Assert.AreEqual(decisionDistance, remaining, 0.02f,
                $"{LogPrefix} 배치 오차 — 의도한 추첨 거리와 실제 잔여 거리가 다릅니다.");

            // ★ 절대 조건 — 이게 false면 AutoWanderController.TryRollEdgeAction의 되올라가기 갈래가
            //   **평가되자마자 기각**되고, 그 걷기 구간은 그대로 돌아서기로 끝난다(영구 실패).
            Assert.IsTrue(found,
                $"{LogPrefix} 배율 {scale:F2}×에서 배회 AI의 추첨 거리({decisionDistance:F3}유닛)에 " +
                $"Dock 턱이 잡히지 않습니다 — 벽 탐지 게이트(parkourDetectionRadius " +
                $"{_clonedConfig.parkourDetectionRadius:F3})가 그 거리보다 짧습니다. " +
                "캐릭터는 Dock 아래에서 영영 못 올라옵니다(사용자 신고 증상).");
            Assert.AreEqual(DockHandle, wallHandle,
                $"{LogPrefix} 탐지된 턱이 Dock({DockHandle})이 아닙니다(핸들 {wallHandle}).");
        }

        // ============================================================================
        // (B) 왕복층 — 실제로 Dock 위로 되올라오는가
        // ============================================================================

        [UnityTest] public IEnumerator ClimbsBackOntoDock_Scale075() { yield return AssertClimbsBackOntoDock(0.75f); }
        [UnityTest] public IEnumerator ClimbsBackOntoDock_Scale125() { yield return AssertClimbsBackOntoDock(1.25f); }
        [UnityTest] public IEnumerator ClimbsBackOntoDock_ScaleMax() { yield return AssertClimbsBackOntoDock(StickConfig.MaxCharacterScale); }

        private IEnumerator AssertClimbsBackOntoDock(float scale)
        {
            float drop = DockGeometry.ReferenceDockDropWorldUnits;
            yield return SetUpDockLayout(drop);
            ApplyScale(scale);

            StickmanBlackboard bb = _agent.Blackboard;

            // 확률/시드 제거 — 확률이 아니라 "확률이 성립했을 때 경로가 끝까지 이어지는가"를 본다
            // (DockTileSizeStepUpTests와 같은 관례).
            _clonedConfig.wanderIdleDurationMin = 0.05f;
            _clonedConfig.wanderIdleDurationMax = 0.05f;
            _clonedConfig.wanderWalkDurationMin = RoundTripObserveSeconds * 4f;
            _clonedConfig.wanderWalkDurationMax = RoundTripObserveSeconds * 4f;
            _clonedConfig.wanderDurationJitterRatio = 0f;
            _clonedConfig.wanderSpontaneousTurnChance = 0f;
            _clonedConfig.wanderPostIdleWalkChance = 1f;
            _clonedConfig.wanderPostIdleJumpChance = 0f;
            _clonedConfig.wanderEdgeJumpAttemptChance = 0f;
            _clonedConfig.wanderEdgeTurnPauseMin = 0.15f;
            _clonedConfig.wanderEdgeTurnPauseMax = 0.15f;
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.stepUpChance = 1f;

            bb.MoveBodyToWorld(new Vector2(_dockRightWorldX + 0.6f, _floorTopWorldY));
            Assert.IsTrue(bb.TryGetWalkableScreenBoundsWorld(out _, out float walkableRightX),
                $"{LogPrefix} 걷기 가능 X 범위를 조회하지 못했습니다.");

            float startX = walkableRightX - StartInsetFromScreenEdgeUnits;
            Assert.Greater(startX - _dockRightWorldX, 1f,
                $"{LogPrefix} 준비 실패 — 안전망 오른쪽 조각이 너무 좁습니다(시작 x={startX:F3}).");

            bb.MoveBodyToWorld(new Vector2(startX, _floorTopWorldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = NetRightHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            _trace.Clear();
            _sawParkourClimb = false;
            StickmanEventBus.StateTransitioned += OnTransition;

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(FixedWanderSeed));
            bb.IntentSource = wander;

            Debug.Log($"{LogPrefix} (B) 되올라오기 관찰 시작 — 배율 {scale:F2}×, 낙차 {drop:F3}유닛, " +
                $"몸 물리 반폭 {bb.CharacterPhysicalHalfWidthWorld:F3}, 경계 판정 거리 {bb.EdgeStopDistanceWorld:F3}, " +
                $"맨틀 인셋 {bb.ParkourMantleInsetWorld:F3}, 보행 속도 {_clonedConfig.ResolveWalkSpeed():F3}, " +
                $"시작 x={startX:F3}, Dock 오른쪽 모서리 {_dockRightWorldX:F3}");

            bool backOnDock = false;
            float elapsed = 0f;
            while (elapsed < RoundTripObserveSeconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                wander.Tick(dt);
                if (bb.CurrentFootholdHandle == DockHandle && bb.Body.position.y > _floorTopWorldY + drop * 0.5f)
                {
                    backOnDock = true;
                    break;
                }
            }

            Debug.Log($"{LogPrefix} (B) 결과 — 배율 {scale:F2}×, 되올라옴={backOnDock}, 등반관측={_sawParkourClimb}, " +
                $"{elapsed:F1}초, 최종 발판핸들={bb.CurrentFootholdHandle}, " +
                $"위치=({bb.Body.position.x:F3},{bb.Body.position.y:F3}), Dock 상단 Y={_dockTopWorldY:F3}\n" +
                $"    전이: {(_trace.Count == 0 ? "(없음)" : string.Join(" ", _trace))}");

            Assert.IsTrue(_sawParkourClimb,
                $"{LogPrefix} 배율 {scale:F2}×에서 {RoundTripObserveSeconds:F0}초 동안 ParkourClimb에 " +
                "한 번도 진입하지 못했습니다 — 캐릭터를 키우면 Dock 위로 못 올라온다는 사용자 신고 그대로입니다.");
            Assert.IsTrue(backOnDock,
                $"{LogPrefix} 배율 {scale:F2}× — 등반은 시도했으나 Dock 발판({DockHandle}) 위로 복귀하지 " +
                $"못했습니다(최종 핸들 {bb.CurrentFootholdHandle}).");
        }

        // ============================================================================
        // 공통 준비
        // ============================================================================

        /// <summary>다이얼과 **같은 경로**로 배율을 적용한다(Core/StickmanAgent.ApplyCharacterScale).
        /// 복제 config에도 같은 배율을 심는다 — 에이전트는 원본을, 블랙보드는 복제본을 들고 있어서
        /// 한쪽만 적용하면 지오메트리와 보행 속도가 어긋난다.</summary>
        private void ApplyScale(float scale)
        {
            _clonedConfig.SetRuntimeCharacterScale(scale);
            _agent.ApplyCharacterScale(scale, "테스트(배율 × Dock 되올라오기)");
            Assert.AreEqual(scale, _agent.CurrentCharacterScale, 0.01f,
                $"{LogPrefix} 배율 적용 실패 — 요청 {scale:F2}, 실제 {_agent.CurrentCharacterScale:F2}.");
        }

        private IEnumerator SetUpDockLayout(float dropUnits)
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalPoller = bb.FootholdPoller;
            _originalIntent = bb.IntentSource;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 전제 실패 — 씬에 PhysicsGround가 없습니다.");
            _floorTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y;
            _dockTopWorldY = _floorTopWorldY + dropUnits;

            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            float floorTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _floorTopWorldY), _clonedConfig, out _).y;
            float dockTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _dockTopWorldY), _clonedConfig, out _).y;

            Assert.Greater(dockTopOsY, 0f, $"{LogPrefix} 준비 실패 — 낙차가 화면 위로 벗어납니다.");
            Assert.Less(floorTopOsY, h, $"{LogPrefix} 준비 실패 — 안전망이 화면 아래로 벗어납니다.");

            float dockLeftOs = w * 0.30f;
            float dockRightOs = w * 0.70f;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(DockHandle,
                new Rect(dockLeftOs, dockTopOsY, dockRightOs - dockLeftOs, h - dockTopOsY), true));
            _service.Footholds.Add(new PlatformFoothold(NetLeftHandle,
                new Rect(0f, floorTopOsY, dockLeftOs, h - floorTopOsY), false));
            _service.Footholds.Add(new PlatformFoothold(NetRightHandle,
                new Rect(dockRightOs, floorTopOsY, w - dockRightOs, h - floorTopOsY), false));

            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);

            _dockLeftWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(dockLeftOs, dockTopOsY), 10f, _clonedConfig).x;
            _dockRightWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(dockRightOs, dockTopOsY), 10f, _clonedConfig).x;

            Debug.Log($"{LogPrefix} 준비 — 안전망 상단 월드Y={_floorTopWorldY:F4}, Dock 상단 월드Y={_dockTopWorldY:F4}, " +
                $"낙차={dropUnits:F4}유닛(tilesize {DockGeometry.DeveloperMachineTileSizePoints:F0} 실측), " +
                $"Dock 월드 X {_dockLeftWorldX:F3}~{_dockRightWorldX:F3}");

            yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }
}
