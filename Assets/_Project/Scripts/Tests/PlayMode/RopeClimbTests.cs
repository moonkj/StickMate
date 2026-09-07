using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 밧줄 등반(RopeClimb, 2026-09-07) 회귀 테스트 —
    /// docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md 구조설계 + design-motion §8을 그대로 구현한 라운드의
    /// 검증. 리더 지시 검증 항목을 그대로 잠근다:
    /// <list type="number">
    ///   <item>트리거 높이대역 경계 — 파쿠르↔밧줄↔포기 3구간이 정확히 갈리는가
    ///     (<see cref="ParkourBandRoutesToParkourClimb"/> / <see cref="RopeBandRoutesToRopeClimb"/> /
    ///     <see cref="AboveRopeBandGivesUp"/>).</item>
    ///   <item>등반속도/사이클 실측(벽시계) — <see cref="AscendCoversHeightAtConfiguredSpeed_WallClock"/>.</item>
    ///   <item>화면상단쿼리 멀티모니터 안전성 — <see cref="ScreenTopIsSingleSourceWithHardClamp"/> /
    ///     <see cref="OffscreenAboveNeighborDoesNotMoveScreenTop"/>(기존 수평 판정이 겪은 버그와 같은
    ///     패턴 — 발판 통합 경계를 화면 경계로 오인하는 함정).</item>
    ///   <item>취소 전환 — <see cref="ThrowCancelWhenWallDisappears_GoesToIdleOrWalk"/> /
    ///     <see cref="AscendCancelWhenWallDisappears_GoesToFall"/>.</item>
    ///   <item>대사트리거 — <see cref="DialogueFires_WithCombinedThrowPlusAscendDwell"/>(계획 잔여
    ///     체류가 Throw만이 아니라 Throw+Ascend 전체 추정치인지, ParkourClimbState가 2026-09-02에
    ///     겪은 "낡은 폴백이 대사를 전부 침묵시키는" 사고의 재발 여부를 값으로 대조한다).</item>
    /// </list>
    ///
    /// 시간 예산은 전부 <b>벽시계(초)</b>다(CLAUDE.md — 배치모드 PlayMode가 2,000fps 이상으로 돈다).
    /// </summary>
    public sealed class RopeClimbTests
    {
        private const string LogPrefix = "[밧줄등반-테스트]";

        private const long WallHandle = 7401L;
        private const long GroundHandle = 7402L;
        private const long OffscreenAboveNeighborHandle = 7403L;

        private const float SettleWaitSeconds = 2.5f;
        private const float StateTransitionTimeoutSeconds = 8f;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        /// <summary>펄스를 프레임 단위로 직접 조작할 수 있는 스텁 — RopeClimbRequested를 명시적으로
        /// 구현한다(기본 구현은 false이므로, 이 트리거를 실제로 검증하려면 반드시 재정의해야 한다).</summary>
        private sealed class ScriptedIntentSource : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested { get; set; }
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested { get; set; }
            public bool RopeClimbRequested { get; set; }
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private FootholdPoller _poller;
        private ScriptedIntentSource _intent;

        private float _groundTopWorldY;
        private float _wallLeftWorldX;
        private float _unitsPerPixel;
        private float _wallLeftOs;
        private float _groundTopOs;

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
            if (_clonedConfig != null) UnityEngine.Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        /// <summary>
        /// 공통 리그 — 화면 왼쪽에 바닥, 오른쪽 55%부터 벽(창) 발판. 벽 높이는
        /// <paramref name="chooseRiseWorld"/>가 "이 화면에서 실제로 쓰이는 파쿠르/로프 상한"을 받아
        /// 그때그때 정한다(고정 리터럴을 베끼면 화면 크기가 다른 배치 환경에서 밴드를 벗어난다).
        /// </summary>
        private IEnumerator SetUpRig(Func<float, float, float> chooseRiseWorld)
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = UnityEngine.Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");

            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = UnityEngine.Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;
            // 배포 기본값 0 — 이 라운드가 잠재워 둔 확률(§7절)이라 테스트에서는 직접 트리거하므로
            // 굳이 올릴 필요는 없지만, AutoWanderController 경로 자체를 함께 확인하는 테스트를 위해
            // 켜 둔다(추첨은 각 테스트가 결정론으로 조인다).
            _clonedConfig.ropeClimbChance = 1f;
            _clonedConfig.ropeClimbChatterChance = 1f;

            Camera cam = bb.MainCamera;
            Assert.IsNotNull(cam, $"{LogPrefix} 블랙보드에 카메라가 없습니다.");

            float w = Screen.width;
            float h = Screen.height;
            _groundTopOs = h * 0.82f;
            _wallLeftOs = w * 0.55f;

            float y0 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, _groundTopOs), 10f, _clonedConfig).y;
            float y1 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, _groundTopOs - 100f), 10f, _clonedConfig).y;
            _unitsPerPixel = Mathf.Abs(y1 - y0) / 100f;
            Assert.Greater(_unitsPerPixel, 0.0001f, $"{LogPrefix} 좌표 변환이 성립하지 않습니다.");

            Vector3 wallEdgeAtGround = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(_wallLeftOs, _groundTopOs), 10f, _clonedConfig);
            _groundTopWorldY = wallEdgeAtGround.y;
            _wallLeftWorldX = wallEdgeAtGround.x;

            float parkourMax = AutoWanderController.ResolveStepUpMaxHeightStatic(bb);
            float ropeMax = AutoWanderController.ResolveRopeClimbMaxHeight(bb, _groundTopWorldY);
            Assert.Greater(ropeMax, parkourMax,
                $"{LogPrefix} 전제 실패 — 밧줄 상한({ropeMax:F3})이 파쿠르 상한({parkourMax:F3})보다 크지 않습니다. " +
                "이 화면 크기에서는 로프 대역 테스트가 성립하지 않습니다.");

            float riseWorld = chooseRiseWorld(parkourMax, ropeMax);
            BuildWall(riseWorld);

            _intent = new ScriptedIntentSource { MoveInputX = 1f };
            bb.IntentSource = _intent;

            float standBack = bb.EdgeProbeReachWorld * 0.5f;
            Vector3 stand = new Vector3(_wallLeftWorldX - standBack, _groundTopWorldY, wallEdgeAtGround.z);
            bb.Body.position = new Vector2(stand.x, stand.y);
            bb.Body.transform.position = stand;
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = GroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
            yield return null;

            Debug.Log($"{LogPrefix} 리그 준비 — 화면 {w:F0}x{h:F0}, 파쿠르상한={parkourMax:F3}, 밧줄상한={ropeMax:F3}, " +
                $"벽 상승={riseWorld:F3}유닛, 선 자리=({stand.x:F3},{stand.y:F3}).");
        }

        /// <summary>벽 발판을 (재)생성한다 — 접지 바닥은 벽이 시작되는 자리에서 끝나야
        /// TryFindClimbableWall의 "경계 근접" 게이트를 통과한다(ParkourClimbPoseTests와 같은 이유).</summary>
        private void BuildWall(float riseWorld)
        {
            float w = Screen.width;
            float h = Screen.height;
            float wallTopOs = _groundTopOs - riseWorld / _unitsPerPixel;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(WallHandle,
                new Rect(_wallLeftOs, wallTopOs, w * 0.40f, (_groundTopOs - wallTopOs) + h * 0.10f), true));
            _service.Footholds.Add(new PlatformFoothold(GroundHandle,
                new Rect(0f, _groundTopOs, _wallLeftOs, h * 0.16f), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            _agent.Blackboard.FootholdPoller = _poller;
        }

        /// <summary>지금 있는 벽 발판을 제거한다 — Throw/Ascend 중 "목표(벽) 소실" 취소 경로 재현.
        /// 바닥은 그대로 둔다(Throw 취소가 낙하가 아니라 Idle/Walk여야 함을 검증하려면 발밑에 여전히
        /// 발판이 있어야 한다).</summary>
        private void RemoveWall()
        {
            _service.Footholds.RemoveAll(f => f.Handle == WallHandle);
            _poller.PollImmediately();
        }

        // ============================================================================
        // (1) 트리거 높이대역 경계 — 파쿠르 ↔ 밧줄 ↔ 포기
        // ============================================================================

        [UnityTest]
        public IEnumerator ParkourBandRoutesToParkourClimb()
        {
            yield return SetUpRig((parkourMax, ropeMax) => parkourMax * 0.9f);
            StickmanBlackboard bb = _agent.Blackboard;

            _intent.StepUpRequested = true;
            yield return null;
            _intent.StepUpRequested = false;

            Assert.AreEqual(StickmanStateId.ParkourClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 파쿠르 상한 이내(0.9x) 벽인데 ParkourClimb로 가지 않았습니다 — " +
                $"실제 상태={bb.Machine.CurrentStateId}.");
        }

        [UnityTest]
        public IEnumerator RopeBandRoutesToRopeClimb()
        {
            yield return SetUpRig((parkourMax, ropeMax) => Mathf.Lerp(parkourMax, ropeMax, 0.5f));
            StickmanBlackboard bb = _agent.Blackboard;

            _intent.RopeClimbRequested = true;
            yield return null;
            _intent.RopeClimbRequested = false;

            Assert.AreEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 파쿠르 상한 초과 + 밧줄 상한 이내(중간값) 벽인데 RopeClimb로 가지 않았습니다 — " +
                $"실제 상태={bb.Machine.CurrentStateId}. WalkState.ResolveClimbBandTarget이 " +
                "AutoWanderController와 같은 계산원을 쓰는지 확인하십시오.");
        }

        [UnityTest]
        public IEnumerator AboveRopeBandGivesUp()
        {
            // 밧줄 상한보다 확실히 높은 벽 — "포기"(아무 것도 안 함, 기존 배회 거동 유지)가 성립해야 한다.
            yield return SetUpRig((parkourMax, ropeMax) => ropeMax + (ropeMax - parkourMax) * 0.5f + 0.5f);
            StickmanBlackboard bb = _agent.Blackboard;

            _intent.RopeClimbRequested = true;
            _intent.StepUpRequested = true;
            yield return null;
            yield return null;
            _intent.RopeClimbRequested = false;
            _intent.StepUpRequested = false;

            Assert.AreNotEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 밧줄 상한보다 높은 벽인데 RopeClimb에 진입했습니다 — 상한이 새지 않아야 합니다.");
            Assert.AreNotEqual(StickmanStateId.ParkourClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 밧줄 상한보다 높은 벽인데 ParkourClimb에 진입했습니다.");
        }

        // ============================================================================
        // (2) 화면상단쿼리 — 단일 소스 계약 + 멀티모니터 안전성
        // ============================================================================

        [UnityTest]
        public IEnumerator ScreenTopIsSingleSourceWithHardClamp()
        {
            yield return SetUpRig((parkourMax, ropeMax) => Mathf.Lerp(parkourMax, ropeMax, 0.5f));
            StickmanBlackboard bb = _agent.Blackboard;

            Assert.IsTrue(bb.TryGetWalkableScreenTopWorldY(out float topBefore),
                $"{LogPrefix} 화면 상단 조회 실패.");

            // ★ 화면 "위쪽 밖"으로 나가야 클램프가 작동한다 — 이 좌표계는 세계 Y가 위로 갈수록
            // 커지므로(화면 상단 = 도달 가능한 가장 큰 세계 Y), 한참 위(+3유닛)에 놓아야 상한을
            // 넘는다. 안쪽(topBefore - 3f)에 놓으면 애초에 클램프할 것이 없어 이 테스트가 무의미해진다
            // (가로 축 테스트가 rightBefore + 3f로 "바깥"을 만드는 것과 같은 부호 규칙).
            float x = bb.Body.position.x;
            bb.Body.position = new Vector2(x, topBefore + 3f);
            bb.Body.transform.position = new Vector3(x, topBefore + 3f, bb.Body.transform.position.z);
            bb.EnforceScreenBoundsAndRescue(0.02f);
            float clampedTop = bb.Body.position.y;

            Debug.Log($"{LogPrefix} 화면 상단 단일소스 대조 — 조회={topBefore:F4}, 클램프 실제 자리={clampedTop:F4}, " +
                $"오차={Mathf.Abs(clampedTop - topBefore):F4}유닛");

            Assert.AreEqual(topBefore, clampedTop, 0.01f,
                $"{LogPrefix} 조회한 화면 상단({topBefore:F4})과 하드 클램프가 실제로 세우는 자리({clampedTop:F4})가 " +
                "다릅니다 — 좌우 경계 조회(TryGetWalkableScreenBoundsWorld)와 같은 단일 소스 " +
                "(ComputeScreenClampOsBounds) 계약이 깨졌다는 뜻입니다.");
        }

        [UnityTest]
        public IEnumerator OffscreenAboveNeighborDoesNotMoveScreenTop()
        {
            yield return SetUpRig((parkourMax, ropeMax) => Mathf.Lerp(parkourMax, ropeMax, 0.5f));
            StickmanBlackboard bb = _agent.Blackboard;

            Assert.IsTrue(bb.TryGetWalkableScreenTopWorldY(out float topBefore),
                $"{LogPrefix} 화면 상단 조회 실패(준비).");

            // ★ 두 번째 모니터의 창을 흉내낸다 — 이 오버레이 화면 훨씬 위(OS y가 화면 밖으로 음수)에
            // 놓여 "발판 목록 전체의 최고 Y"를 화면 밖으로 밀어낸다. 캐릭터가 딛는 발판/화면/카메라는
            // 전혀 바뀌지 않는다 — ScreenEdgeTurnaroundTests의 가로축 재현과 완전히 같은 패턴을
            // 세로축에 옮긴 것이다.
            float w = Screen.width;
            _service.Footholds.Add(new PlatformFoothold(OffscreenAboveNeighborHandle,
                new Rect(w * 0.5f, -3000f, 400f, 40f), false));
            _poller.PollImmediately();
            yield return null;

            Assert.IsTrue(bb.TryGetWalkableScreenTopWorldY(out float topAfter),
                $"{LogPrefix} 화면 상단 조회 실패(이웃 추가 후).");

            Debug.Log($"{LogPrefix} 멀티모니터 안전성 — 이웃 추가 전={topBefore:F4}, 후={topAfter:F4}");

            Assert.AreEqual(topBefore, topAfter, 0.01f,
                $"{LogPrefix} 화면 밖 위쪽 이웃 발판 하나만으로 화면 상단 조회값이 " +
                $"{topBefore:F4} -> {topAfter:F4}로 바뀌었습니다 — '발판 목록의 최고 Y'를 화면 경계로 " +
                "오인하고 있다는 뜻이고, 이 프로젝트가 가로축에서 이미 두 번 겪은 러닝머신 버그와 " +
                "같은 함정입니다(TryGetWalkableScreenTopWorldY는 반드시 ComputeScreenClampOsBounds만 읽어야 합니다).");
        }

        // ============================================================================
        // (3) 등반속도/사이클 실측 — 벽시계
        // ============================================================================

        [UnityTest]
        public IEnumerator AscendCoversHeightAtConfiguredSpeed_WallClock()
        {
            // 사이클이 최소 1회 이상 돌 만큼 충분히 높은(그러나 상한 이내) 벽을 고른다.
            yield return SetUpRig((parkourMax, ropeMax) => Mathf.Min(ropeMax * 0.95f, parkourMax * 2.5f));
            StickmanBlackboard bb = _agent.Blackboard;

            bb.Machine.ChangeState(StickmanStateId.RopeClimb, isForcedInterrupt: true);
            yield return null;
            Assert.AreEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — RopeClimb에 진입하지 못했습니다.");

            float speed = _clonedConfig.ropeClimbSpeedHeightsPerSecond * bb.CharacterHeightWorld;
            Assert.IsTrue(bb.TryGetFootholdTopWorldY(WallHandle, out float wallTopY),
                $"{LogPrefix} 벽 상단 조회 실패.");
            float startY = _groundTopWorldY;
            float totalRise = wallTopY - startY;
            float expectedAscendSeconds = totalRise / speed;

            // Throw가 끝나 Ascend로 넘어갈 때까지 대기(벽시계) — 진입 순간의 월드 Y를 Ascend 시작
            // 기준으로 다시 잰다(Throw 중에는 y가 바뀌지 않지만, 기준점을 상태가 실제로 쓰는 시점에서
            // 잡아야 한다).
            float waited = 0f;
            const float ThrowTimeoutSeconds = 3f;
            while (bb.Machine.CurrentStateId == StickmanStateId.RopeClimb && waited < ThrowTimeoutSeconds)
            {
                // Ascend 진입 여부는 Y가 실제로 움직이기 시작했는지로 판별한다(페이즈 자체는 private).
                if (Mathf.Abs(bb.Body.position.y - startY) > 0.01f) break;
                yield return null;
                waited += Time.deltaTime;
            }
            Assert.Less(waited, ThrowTimeoutSeconds,
                $"{LogPrefix} Throw가 {ThrowTimeoutSeconds:F0}초 안에 끝나지 않았습니다(Ascend로 전이하지 못함).");

            float ascendStartWallClock = Time.time;
            float ascendStartY = bb.Body.position.y;

            float elapsed = 0f;
            const float AscendTimeoutSeconds = 20f;
            while (bb.Machine.CurrentStateId == StickmanStateId.RopeClimb && elapsed < AscendTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            float actualAscendSeconds = Time.time - ascendStartWallClock;
            Assert.AreNotEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 등반이 {AscendTimeoutSeconds:F0}초 안에 끝나지 않았습니다.");
            Assert.IsTrue(bb.Machine.CurrentStateId == StickmanStateId.Walk,
                $"{LogPrefix} 등반 완료 후 상태가 Walk가 아니라 {bb.Machine.CurrentStateId}입니다.");

            Debug.Log($"{LogPrefix} Ascend 실측 — 총 상승={totalRise:F3}유닛, 설정 속도={speed:F3}유닛/초, " +
                $"기대 소요={expectedAscendSeconds:F2}초, 실측 소요={actualAscendSeconds:F2}초, " +
                $"시작Y={ascendStartY:F3}, 도착Y={bb.Body.position.y:F3}(목표 {wallTopY:F3}).");

            // 배치모드 프레임 지터를 감안한 여유(±15%) — 벽시계 기준이므로 프레임레이트와 무관하게 성립해야 한다.
            Assert.AreEqual(expectedAscendSeconds, actualAscendSeconds, expectedAscendSeconds * 0.15f + 0.1f,
                $"{LogPrefix} 실측 Ascend 소요({actualAscendSeconds:F2}초)가 설정 속도로 계산한 기대치" +
                $"({expectedAscendSeconds:F2}초)와 크게 다릅니다 — ropeClimbSpeedHeightsPerSecond가 실제로 " +
                "반영되지 않고 있을 수 있습니다.");
        }

        // ============================================================================
        // (4) 취소 전환 — Throw 중 / Ascend 중
        // ============================================================================

        [UnityTest]
        public IEnumerator ThrowCancelWhenWallDisappears_GoesToIdleOrWalk()
        {
            yield return SetUpRig((parkourMax, ropeMax) => Mathf.Lerp(parkourMax, ropeMax, 0.5f));
            StickmanBlackboard bb = _agent.Blackboard;

            bb.Machine.ChangeState(StickmanStateId.RopeClimb, isForcedInterrupt: true);
            yield return null;
            Assert.AreEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — RopeClimb에 진입하지 못했습니다.");

            // 아직 Throw 중(몸이 접지 자리에서 움직이지 않은 시점)임을 확인한 뒤 벽을 지운다.
            float startY = bb.Body.position.y;
            Assert.Less(Mathf.Abs(bb.Body.position.y - startY), 0.001f,
                $"{LogPrefix} 준비 실패 — 이미 Ascend로 넘어가 y가 움직였습니다.");

            RemoveWall();

            float elapsed = 0f;
            while (bb.Machine.CurrentStateId == StickmanStateId.RopeClimb && elapsed < StateTransitionTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Debug.Log($"{LogPrefix} Throw 취소 결과 — {elapsed:F2}초 후 상태={bb.Machine.CurrentStateId}.");

            Assert.IsTrue(
                bb.Machine.CurrentStateId == StickmanStateId.Idle || bb.Machine.CurrentStateId == StickmanStateId.Walk,
                $"{LogPrefix} Throw 도중 목표 소실 시 Idle/Walk로 취소돼야 하는데 {bb.Machine.CurrentStateId}로 " +
                "갔습니다 — 아직 발판 위(접지 중)인데 낙하 이유가 없습니다(§3-B).");
        }

        [UnityTest]
        public IEnumerator AscendCancelWhenWallDisappears_GoesToFall()
        {
            yield return SetUpRig((parkourMax, ropeMax) => Mathf.Lerp(parkourMax, ropeMax, 0.5f));
            StickmanBlackboard bb = _agent.Blackboard;

            bb.Machine.ChangeState(StickmanStateId.RopeClimb, isForcedInterrupt: true);
            yield return null;
            Assert.AreEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — RopeClimb에 진입하지 못했습니다.");

            float startY = bb.Body.position.y;
            float waited = 0f;
            const float ThrowTimeoutSeconds = 3f;
            while (bb.Machine.CurrentStateId == StickmanStateId.RopeClimb && waited < ThrowTimeoutSeconds)
            {
                if (Mathf.Abs(bb.Body.position.y - startY) > 0.01f) break;
                yield return null;
                waited += Time.deltaTime;
            }
            Assert.Less(waited, ThrowTimeoutSeconds,
                $"{LogPrefix} Throw가 {ThrowTimeoutSeconds:F0}초 안에 끝나지 않아 Ascend 취소를 재현할 수 없습니다.");
            Assert.AreEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 준비 실패 — Ascend 도달 전에 이미 다른 상태로 전이했습니다.");

            RemoveWall();

            float elapsed = 0f;
            while (bb.Machine.CurrentStateId == StickmanStateId.RopeClimb && elapsed < StateTransitionTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Debug.Log($"{LogPrefix} Ascend 취소 결과 — {elapsed:F2}초 후 상태={bb.Machine.CurrentStateId}.");

            Assert.AreEqual(StickmanStateId.Fall, bb.Machine.CurrentStateId,
                $"{LogPrefix} Ascend 도중 목표 소실 시 즉시 Fall로 가야 하는데 {bb.Machine.CurrentStateId}입니다 — " +
                "공중에서 손을 잡고 있던 것과 물리적으로 같은 처지입니다(ParkourClimbState와 100% 동일 규칙).");
        }

        // ============================================================================
        // (5) 대사 트리거 — 계획 잔여 체류가 Throw+Ascend 전체인지
        // ============================================================================

        [UnityTest]
        public IEnumerator DialogueFires_WithCombinedThrowPlusAscendDwell()
        {
            // Throw 단독으로는 규칙 8을 겨우 넘거나 못 넘는 낮은 벽을 고른다 — Ascend 추정이 실제로
            // 더해지지 않으면 이 표본에서 발화가 막힐 가능성이 가장 높은 경계 지점이다.
            yield return SetUpRig((parkourMax, ropeMax) => parkourMax * 1.05f);
            StickmanBlackboard bb = _agent.Blackboard;

            _clonedConfig.ambientChatterCooldownSeconds = 0f;
            bb.NextChatterAllowedUnscaledTime = 0f;

            DialogueIntentCapture capture = new DialogueIntentCapture();
            StickmanEventBus.DialogueRequested += capture.OnRequested;
            try
            {
                bb.Machine.ChangeState(StickmanStateId.RopeClimb, isForcedInterrupt: true);
                yield return null;
                Assert.AreEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                    $"{LogPrefix} 전제 실패 — RopeClimb에 진입하지 못했습니다.");

                var ropeState = bb.Machine.GetState(StickmanStateId.RopeClimb) as RopeClimbState;
                Assert.IsNotNull(ropeState, $"{LogPrefix} RopeClimbState 인스턴스를 찾지 못했습니다.");

                Debug.Log($"{LogPrefix} 대사 예산 진단 — Throw만={ropeState.LastThrowOnlySecondsDiagnostic:F2}초, " +
                    $"전체(Throw+Ascend)={ropeState.LastPlannedDwellSecondsDiagnostic:F2}초, " +
                    $"발화 여부={capture.Fired}.");

                // ★★ 핵심 단언 — §8-6-B가 경고하는 사고("Throw 페이즈 하나만 게이트에 넘긴다")가
                // 재발하지 않았는지를 로그가 아니라 값으로 잠근다.
                Assert.Greater(ropeState.LastPlannedDwellSecondsDiagnostic, ropeState.LastThrowOnlySecondsDiagnostic,
                    $"{LogPrefix} 계획 잔여 체류({ropeState.LastPlannedDwellSecondsDiagnostic:F2}초)가 Throw만의 " +
                    $"추정치({ropeState.LastThrowOnlySecondsDiagnostic:F2}초)보다 크지 않습니다 — Ascend 추정이 " +
                    "더해지지 않았다는 뜻이고, 이는 ParkourClimbState가 2026-09-02에 실제로 겪은 사고와 " +
                    "같은 함정입니다.");

                Assert.IsTrue(capture.Fired,
                    $"{LogPrefix} 대사가 발화하지 않았습니다(계획 잔여 체류 " +
                    $"{ropeState.LastPlannedDwellSecondsDiagnostic:F2}초, 확률 1.0, 쿨다운 0). " +
                    "트리거 배선(Enter() 단일 지점)이 실제로 대사를 만들지 못하고 있을 수 있습니다.");
            }
            finally
            {
                StickmanEventBus.DialogueRequested -= capture.OnRequested;
            }
        }

        private sealed class DialogueIntentCapture
        {
            public bool Fired;
            public void OnRequested(DialogueIntent intent) => Fired = true;
        }
    }
}
