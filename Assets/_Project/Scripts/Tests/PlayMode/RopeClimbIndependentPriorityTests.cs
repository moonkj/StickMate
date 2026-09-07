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
    /// ★★★ 근본재설계(2026-09-07 3차, §10 "독립 우선순위") 회귀 —
    /// docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md §9→§10.
    ///
    /// 배경: §9-4-A(ropeClimbChance 0→0.20)+§9-4-B(hop-down 좁은 보강)를 적용한 뒤에도, 리더가
    /// 실기(macOS)에서 장시간 관찰한 결과 자연발동이 <b>전혀</b> 없었다 — 캐릭터가 Dock↔안전망 사이를
    /// 무한 왕복(뛰어내리기↔되올라가기)했을 뿐, 로프 등반은 단 한 번도 평가되지 않았다. 재조사로 밝혀진
    /// 진짜 원인 둘을 이 스위트가 각각 직접 잠근다(RopeClimbHopDownOrderingTests가 이미 잠근 §9-4-B
    /// 시나리오와는 다른 두 축이다):
    ///
    /// <list type="number">
    ///   <item><b>탐색 폭</b> — 로프 대역 벽이 "지금 선 경계에서 도보 한 걸음"(기존 좁은 탐색폭, 손
    ///     등반과 동일)보다 먼 곳에 있으면 절대 못 찾았다. <see cref="넓은_탐색_전용_로프벽도_안전망이_상시존재해도_발동한다"/>
    ///     는 (a) 좁은 탐색으로는 이 벽이 실제로 안 보인다는 것, (b) 뛰어내릴 낮은 발판(안전망 격)이
    ///     동시에 상시 존재한다는 것, (c) 그런데도 새 넓은 탐색(TryFindRopeClimbWallWide)이 이 벽을
    ///     찾아 로프 등반을 실제로 발동시킨다는 것을 전부 값으로 확인한다.</item>
    ///   <item><b>확률 결합</b> — 예전 코드는 로프 추첨이 되올라가기 블록의 else 안에 있어 <c>stepUpChance</c>
    ///     의 성공을 먼저 통과해야만 로프 추첨 기회 자체가 열렸다(실효 확률 = stepUpChance × ropeClimbChance,
    ///     §9-3). <see cref="stepUpChance가_0이어도_로프등반은_독립적으로_평가된다"/>는 stepUpChance=0으로
    ///     되올라가기 갈래 자체를 완전히 죽여도 로프 등반이 여전히 발동하는지 확인한다 — 이 테스트는
    ///     재설계 이전 코드에서는 100% 실패했을 케이스다(외곽 게이트가 항상 막았으므로).</item>
    /// </list>
    /// </summary>
    public sealed class RopeClimbIndependentPriorityTests
    {
        private const string LogPrefix = "[밧줄-독립우선순위-테스트]";

        private const long GroundHandle = 7601L;
        private const long LowDropLeftHandle = 7602L;
        private const long LowDropRightHandle = 7603L;
        private const long TallWallLeftHandle = 7604L;
        private const long TallWallRightHandle = 7605L;

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
            RopeClimbQaOverride.SetChanceTestOverride(null);
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        /// <summary>공통 준비 — 씬 로드, 블랙보드 확보, 카메라/화면 크기 확보까지. 발판은 각 테스트가 직접 짠다.</summary>
        private IEnumerator SetUpAgent()
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
        }

        /// <summary>표준 배회 튜닝 — 결정론에 가깝게 조인다(RopeClimbHopDownOrderingTests와 동일 관례).</summary>
        private void TightenWanderForDeterminism()
        {
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
        }

        // ============================================================================
        // (1) 넓은 탐색 전용 로프 벽 — 안전망(낮은 발판)이 상시 존재해도 로프 등반이 발동한다
        // ============================================================================

        [UnityTest]
        public IEnumerator 넓은_탐색_전용_로프벽도_안전망이_상시존재해도_발동한다()
        {
            yield return SetUpAgent();
            StickmanBlackboard bb = _agent.Blackboard;
            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            float groundTopOs = h * 0.5f;

            Vector3 groundLeftWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.4f, groundTopOs), 10f, _clonedConfig);
            Vector3 groundRightWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.6f, groundTopOs), 10f, _clonedConfig);

            // 수평 변환계수 — 세로축의 _unitsPerPixel(기존 RopeClimbTests 관례)를 가로축에 그대로 옮긴다.
            Vector3 xProbe0 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(0f, groundTopOs), 10f, _clonedConfig);
            Vector3 xProbe1 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(100f, groundTopOs), 10f, _clonedConfig);
            float worldUnitsPerOsPixelX = (xProbe1.x - xProbe0.x) / 100f;
            Assert.Greater(Mathf.Abs(worldUnitsPerOsPixelX), 0.0001f, $"{LogPrefix} 가로 좌표 변환이 성립하지 않습니다.");

            float stepUpMax = AutoWanderController.ResolveStepUpMaxHeightStatic(bb);
            float riseUnits = stepUpMax + 2f;
            float dropUnits = DockGeometry.ReferenceDockDropWorldUnits; // 뛰어내리기 밴드 낙차 — 안전망 격.

            // ★ 핵심 배치 — 로프 대역 벽의 근접 모서리를 "detectionRadius의 10배"만큼 경계에서 떨어뜨린다.
            // parkourDetectionRadius(0.5)의 10배(=5.0유닛)는 이 프로젝트 기존 좁은 탐색폭
            // (GroundSensor.AdjacentFootholdSearchRadiusMultiplier=4배=2.0유닛, 손 등반/매달리기/
            // 뛰어내리기 전부가 쓰는 그 폭)보다 확실히 멀고, 새 로프 전용 배수(기본 16배=8.0유닛)
            // 안에는 넉넉히 든다. 다만 "확실히 멀다"를 **가정하지 않고 아래에서 직접 좁은 탐색이
            // 이 벽을 못 찾는다는 것을 값으로 검증**한다 — 사설 상수를 베끼지 않고 실제 판정 함수의
            // 결과로 확인하는 것이 이 프로젝트 관례다.
            float detectionRadius = _clonedConfig.parkourDetectionRadius;
            float farDistanceWorld = detectionRadius * 10f;
            float wideSlackWorld = detectionRadius * Mathf.Max(1f, _clonedConfig.ropeClimbAdjacentSearchRadiusMultiplier);
            Assert.Less(farDistanceWorld, wideSlackWorld,
                $"{LogPrefix} 전제 실패 — 시험 거리({farDistanceWorld:F2})가 넓은 탐색폭({wideSlackWorld:F2}) " +
                "안에 들지 않습니다. StickConfig.ropeClimbAdjacentSearchRadiusMultiplier 기본값을 확인하십시오.");

            float farDistanceOsPixels = Mathf.Abs(farDistanceWorld / worldUnitsPerOsPixelX);
            float dropOsPixels = Mathf.Abs((groundLeftWorld.y - (groundLeftWorld.y - dropUnits)));
            // Y 변환은 기존 세로 unitsPerPixel로.
            Vector3 y0 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig);
            Vector3 y1 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs - 100f), 10f, _clonedConfig);
            float unitsPerPixelY = Mathf.Abs(y1.y - y0.y) / 100f;
            float dropOsOffset = dropUnits / unitsPerPixelY;
            float riseOsOffset = riseUnits / unitsPerPixelY;

            float lowDropTopOs = groundTopOs + dropOsOffset; // 아래(OS y 증가)
            float tallWallTopOs = groundTopOs - riseOsOffset; // 위(OS y 감소)

            Assert.Greater(tallWallTopOs, 0f, $"{LogPrefix} 준비 실패 — 로프벽이 화면 위로 완전히 벗어났습니다.");

            // 왼쪽: 안전망 격 낮은 발판은 Ground 바로 옆(narrow 탐색 안, 상시 존재).
            float lowDropLeftOsX = w * 0.4f - 80f;
            // 왼쪽: 로프 대역 벽은 Ground 왼쪽 경계에서 farDistanceOsPixels만큼 더 떨어진 자리.
            float tallWallLeftRightEdgeOs = (w * 0.4f) - farDistanceOsPixels;
            float tallWallLeftLeftEdgeOs = tallWallLeftRightEdgeOs - 120f;

            float lowDropRightOsX = w * 0.6f;
            float tallWallRightLeftEdgeOs = (w * 0.6f) + farDistanceOsPixels;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(GroundHandle,
                new Rect(w * 0.4f, groundTopOs, w * 0.2f, h - groundTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(LowDropLeftHandle,
                new Rect(lowDropLeftOsX, lowDropTopOs, 80f, h - lowDropTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(LowDropRightHandle,
                new Rect(lowDropRightOsX, lowDropTopOs, 80f, h - lowDropTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallLeftHandle,
                new Rect(tallWallLeftLeftEdgeOs, tallWallTopOs, tallWallLeftRightEdgeOs - tallWallLeftLeftEdgeOs, h * 0.5f), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallRightHandle,
                new Rect(tallWallRightLeftEdgeOs, tallWallTopOs, 120f, h * 0.5f), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            float groundCenterWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig).x;
            float groundTopWorldY = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig).y;
            bb.Body.position = new Vector2(groundCenterWorldX, groundTopWorldY);
            bb.Body.transform.position = new Vector3(groundCenterWorldX, groundTopWorldY, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = GroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            GroundSensor.GroundInfo infoNow = bb.SenseGround();
            float ropeMaxNow = AutoWanderController.ResolveRopeClimbMaxHeight(bb, infoNow.GroundWorldY);
            Assert.LessOrEqual(riseUnits, ropeMaxNow,
                $"{LogPrefix} 전제 실패 — 시험벽 상승폭({riseUnits:F3})이 로프등반 상한({ropeMaxNow:F3})을 넘습니다.");

            // ★★ 핵심 사전조건 — 좁은(기존) 탐색으로는 이 벽이 실제로 안 보인다는 것을 직접 확인한다.
            // (이 값이 사설 상수와 어긋나면 이 assert가 먼저 시끄럽게 실패한다 — "베끼지 않고 재확인".)
            bool narrowLeftFound = bb.TryFindClimbableWall(infoNow, -1, out _, out _);
            bool narrowRightFound = bb.TryFindClimbableWall(infoNow, 1, out _, out _);
            Debug.Log($"{LogPrefix} 좁은 탐색 결과 — 왼쪽={narrowLeftFound}, 오른쪽={narrowRightFound} " +
                "(둘 다 false여야 '넓은 탐색 전용' 시나리오가 성립합니다).");
            Assert.IsFalse(narrowLeftFound, $"{LogPrefix} 전제 실패 — 좁은 탐색이 이미 왼쪽 벽을 찾았습니다(거리 재조정 필요).");
            Assert.IsFalse(narrowRightFound, $"{LogPrefix} 전제 실패 — 좁은 탐색이 이미 오른쪽 벽을 찾았습니다(거리 재조정 필요).");

            // ★★ 그리고 안전망 격 낮은 발판(내려갈 곳)은 실제로 상시 존재한다는 것도 확인한다.
            bool hopLeftFound = bb.TryFindHopDownTarget(infoNow, -1, out _, out _);
            bool hopRightFound = bb.TryFindHopDownTarget(infoNow, 1, out _, out _);
            Debug.Log($"{LogPrefix} 뛰어내리기 후보 — 왼쪽={hopLeftFound}, 오른쪽={hopRightFound} " +
                "(적어도 한쪽은 true여야 '내려갈 곳이 상시 존재' 전제가 성립합니다).");
            Assert.IsTrue(hopLeftFound || hopRightFound,
                $"{LogPrefix} 전제 실패 — 뛰어내릴 낮은 발판이 감지되지 않았습니다.");

            Debug.Log($"{LogPrefix} 준비 완료 — 화면 {w:F0}x{h:F0}, 시험거리={farDistanceWorld:F3}유닛" +
                $"(좁은탐색 실패 확인됨, 넓은탐색폭={wideSlackWorld:F3}유닛), 벽 상승={riseUnits:F3}유닛, " +
                $"낙차={dropUnits:F3}유닛, 시작 위치={bb.Body.position}.");

            TightenWanderForDeterminism();
            _clonedConfig.hopDownChance = 1f;
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.stepUpChance = 1f;
            _clonedConfig.ropeClimbChance = 1f;

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260907));
            bb.IntentSource = wander;

            bool sawRopeClimbRequested = false;
            bool sawRopeClimbState = false;
            bool sawFall = false;
            long lastHandle = bb.CurrentFootholdHandle;
            float elapsed = 0f;

            while (elapsed < MaxObserveSeconds && !sawRopeClimbRequested && !sawRopeClimbState)
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
                $"Fall경유={sawFall}, 총 {elapsed:F2}초, 최종 상태={bb.Machine.CurrentStateId}.");

            Assert.IsTrue(sawRopeClimbRequested || sawRopeClimbState,
                $"{LogPrefix} {MaxObserveSeconds:F0}초 안에 밧줄등반이 요청/진입되지 않았습니다 — 넓은 탐색 폴백이나 " +
                "독립 우선순위가 동작하지 않았거나(§10), '내려갈 곳'이 로프 평가를 여전히 원천봉쇄하고 있다는 뜻입니다.");

            // ★★★ 핵심 회귀 잠금 — 2026-09-07 3차 라이브 실기에서 실제로 겪은 사고: 트리거는 성공
            // (RopeClimbRequested/RopeClimb 상태 진입까지는 갔다)했지만, RopeClimbState.Enter()가
            // **좁은** TryFindClimbableWall로 벽을 다시 찾다 실패해(넓은 탐색으로 찾은 벽은 좁은
            // 탐색에 안 보인다) 그 즉시 "목표 벽 소실"로 Idle/Walk로 되돌아갔다 — 상태 이름만 보면
            // "RopeClimb에 진입했다"로 보여 위 단언은 통과하지만 실제로는 아무 일도 안 일어난 것과
            // 같다. Throw의 최소 지속시간(WindUp+Swing+HookConfirm ≈ 0.73초, StickConfig 8-7표)보다
            // 훨씬 짧은 여유(0.3초)만 더 기다려, 그 사이에 이미 Idle/Walk로 튕겨 나가지 않았는지
            // 확인한다 — 진짜 벽을 찾았다면 이 짧은 창 안에 취소될 이유가 없다.
            if (sawRopeClimbState)
            {
                float followUpElapsed = 0f;
                const float FollowUpWindowSeconds = 0.3f;
                while (followUpElapsed < FollowUpWindowSeconds)
                {
                    yield return null;
                    followUpElapsed += Time.deltaTime;
                    wander.Tick(Time.deltaTime);
                }
                Debug.Log($"{LogPrefix} 진입 후 {FollowUpWindowSeconds:F1}초 추가 관찰 — 최종 상태={bb.Machine.CurrentStateId}.");
                Assert.AreEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                    $"{LogPrefix} RopeClimb에 진입한 지 {FollowUpWindowSeconds:F1}초 만에 {bb.Machine.CurrentStateId}로 " +
                    "돌아갔습니다 — Throw 최소 지속시간(≈0.73초)보다 훨씬 이르므로, Enter()가 넓은 탐색으로 찾은 " +
                    "벽을 좁은 탐색으로 다시 찾다 실패해 '목표 벽 소실'로 즉시 취소했을 가능성이 높습니다 " +
                    "(RopeClimbState.Enter()가 TryFindRopeClimbWallWide를 쓰는지 확인하십시오).");
            }
        }

        // ============================================================================
        // (2) stepUpChance가 0이어도 로프등반은 독립적으로 평가된다
        // ============================================================================

        [UnityTest]
        public IEnumerator stepUpChance가_0이어도_로프등반은_독립적으로_평가된다()
        {
            yield return SetUpAgent();
            StickmanBlackboard bb = _agent.Blackboard;
            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            float groundTopOs = h * 0.5f;

            Vector3 y0 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig);
            Vector3 y1 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs - 100f), 10f, _clonedConfig);
            float unitsPerPixelY = Mathf.Abs(y1.y - y0.y) / 100f;

            float stepUpMax = AutoWanderController.ResolveStepUpMaxHeightStatic(bb);
            float riseUnits = stepUpMax + 2f; // 로프 대역(파쿠르 상한 초과) — 이번엔 기존 좁은 탐색 안에 둔다.
            float riseOsOffset = riseUnits / unitsPerPixelY;
            float tallWallTopOs = groundTopOs - riseOsOffset;
            Assert.Greater(tallWallTopOs, 0f, $"{LogPrefix} 준비 실패 — 로프벽이 화면 위로 완전히 벗어났습니다.");

            // 좌우 대칭 — 벽은 Ground 경계에 바로 붙여(기존 좁은 탐색 안) 이번 테스트가 순수하게
            // "stepUpChance 독립성"만 검증하게 한다(탐색 폭 문제는 위 테스트가 이미 잠갔다).
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(GroundHandle,
                new Rect(w * 0.4f, groundTopOs, w * 0.2f, h - groundTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(TallWallLeftHandle,
                new Rect(w * 0.2f, tallWallTopOs, w * 0.2f, h * 0.5f), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallRightHandle,
                new Rect(w * 0.6f, tallWallTopOs, w * 0.2f, h * 0.5f), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            float groundCenterWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig).x;
            float groundTopWorldY = y0.y;
            bb.Body.position = new Vector2(groundCenterWorldX, groundTopWorldY);
            bb.Body.transform.position = new Vector3(groundCenterWorldX, groundTopWorldY, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = GroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            GroundSensor.GroundInfo infoNow = bb.SenseGround();
            float ropeMaxNow = AutoWanderController.ResolveRopeClimbMaxHeight(bb, infoNow.GroundWorldY);
            Assert.LessOrEqual(riseUnits, ropeMaxNow,
                $"{LogPrefix} 전제 실패 — 시험벽 상승폭({riseUnits:F3})이 로프등반 상한({ropeMaxNow:F3})을 넘습니다.");
            Assert.IsTrue(bb.TryFindClimbableWall(infoNow, -1, out _, out _) || bb.TryFindClimbableWall(infoNow, 1, out _, out _),
                $"{LogPrefix} 전제 실패 — 좁은 탐색으로도 벽이 안 잡힙니다(배치 오류).");

            Debug.Log($"{LogPrefix} 준비 완료 — 벽 상승={riseUnits:F3}유닛(로프상한={ropeMaxNow:F3}), stepUpChance=0으로 " +
                "되올라가기 갈래를 완전히 죽인 채 로프등반만 평가합니다.");

            TightenWanderForDeterminism();
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.ledgeHangChance = 0f;
            // ★★ 핵심 — 되올라가기 갈래를 완전히 죽인다. 재설계 이전 코드라면 로프 추첨 자체가
            // "stepUpChance 통과"라는 외곽 게이트에 막혀 이 값이 0인 한 로프등반도 함께 죽었다.
            _clonedConfig.stepUpChance = 0f;
            _clonedConfig.ropeClimbChance = 1f;

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260907));
            bb.IntentSource = wander;

            bool sawRopeClimbRequested = false;
            bool sawRopeClimbState = false;
            float elapsed = 0f;

            while (elapsed < MaxObserveSeconds && !sawRopeClimbRequested && !sawRopeClimbState)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                wander.Tick(dt);

                if (wander.RopeClimbRequested) sawRopeClimbRequested = true;
                if (bb.Machine.CurrentStateId == StickmanStateId.RopeClimb) sawRopeClimbState = true;
            }

            Debug.Log($"{LogPrefix} 실측 결과(stepUpChance=0) — RopeClimbRequested={sawRopeClimbRequested}, " +
                $"RopeClimb상태진입={sawRopeClimbState}, 총 {elapsed:F2}초, 최종 상태={bb.Machine.CurrentStateId}.");

            Assert.IsTrue(sawRopeClimbRequested || sawRopeClimbState,
                $"{LogPrefix} stepUpChance=0인데도 {MaxObserveSeconds:F0}초 안에 밧줄등반이 발동하지 않았습니다 — " +
                "로프 추첨이 여전히 stepUpChance 게이트에 묶여 있다는 뜻입니다(근본재설계 §10이 되돌려졌을 수 있음).");

            // ★★★ 위 테스트와 같은 핵심 회귀 잠금 — "진입했다"만으로 끝내지 않고 즉시 되튕기지
            // 않는지 짧게 더 지켜본다(RopeClimbState.Enter()가 좁은 탐색으로 회귀하는 사고 방지).
            if (sawRopeClimbState)
            {
                float followUpElapsed = 0f;
                const float FollowUpWindowSeconds = 0.3f;
                while (followUpElapsed < FollowUpWindowSeconds)
                {
                    yield return null;
                    followUpElapsed += Time.deltaTime;
                    wander.Tick(Time.deltaTime);
                }
                Debug.Log($"{LogPrefix} 진입 후 {FollowUpWindowSeconds:F1}초 추가 관찰 — 최종 상태={bb.Machine.CurrentStateId}.");
                Assert.AreEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                    $"{LogPrefix} RopeClimb에 진입한 지 {FollowUpWindowSeconds:F1}초 만에 {bb.Machine.CurrentStateId}로 " +
                    "돌아갔습니다 — Enter()가 즉시 '목표 벽 소실'로 취소했을 가능성이 높습니다.");
            }
        }
    }
}
