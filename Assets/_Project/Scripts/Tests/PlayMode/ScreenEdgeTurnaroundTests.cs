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
    /// ★ 2026-08-29 리더 지시 — "화면 물리적 끝에서 제자리 걷기(러닝머신)" 회귀 방지.
    ///
    /// 증상(수정 전): 캐릭터가 화면 좌/우 끝까지 걸어가면 배회 AI의 경계 판정에 걸리지 않은 채
    /// 화면 하드 클램프를 계속 밀었다. 걷기 애니메이션은 도는데 위치는 변하지 않는 러닝머신이 되고,
    /// Walk 지속시간(1.5~4초)이 만료돼야 겨우 풀렸다.
    ///
    /// 원인: 클램프(StickmanBlackboard.EnforceScreenBoundsAndRescue)는 캐릭터를 화면 끝에서
    /// "기본 여유 8pt + 시각 반폭"(실측 약 58pt) 안쪽에 가두는데, 경계 판정
    /// (AutoWanderController.IsNearFootholdEdge)은 wanderEdgeStopDistance(0.3유닛 ≈ 24pt)를
    /// **발판의 원시 경계**(= 화면 끝)에서 쟀다. 58 > 24라 "경계 근처"가 영영 성립하지 않았다.
    ///
    /// 이 파일이 잠그는 두 가지:
    ///   (1) <see cref="WalkableBoundsAreExactlyWhereTheHardClampStopsTheCharacter"/> —
    ///       "갈 수 있는 한계" 조회와 하드 클램프가 **같은 계산식 하나**에서 나온다는 계약.
    ///       이 프로젝트는 같은 값을 두 곳에서 따로 계산하다 두 번 사고를 냈다
    ///       (BUG-P1-R4-B1, BUG-P1-R5-B2) — 그래서 값 자체를 대조해 못 박는다.
    ///   (2) <see cref="WalksIntoScreenEdgeAndTurnsAroundWithinAbsoluteDeadline"/> —
    ///       화면 끝을 향해 걷게 두면 **절대 시한 안에** 스스로 방향을 바꾼다.
    ///
    /// 왜 "절대 조건"인가(리더 지시): 이 프로젝트는 상대 마진 방식 테스트가 버그를 2라운드 연속
    /// 놓친 전례가 있다. 그래서 (2)는 Walk 지속시간을 관찰 창보다 **훨씬 길게**(30초 vs 5초) 잡아
    /// "타이머 만료로 우연히 풀리는" 탈출구를 원천 차단하고, 즉흥 방향전환 확률도 0으로 막는다 —
    /// 이 조건에서 방향이 바뀔 수 있는 경로는 경계 판정 하나뿐이다.
    /// </summary>
    public sealed class ScreenEdgeTurnaroundTests
    {
        private const string LogPrefix = "[EDGE-TURN-TEST]";

        /// <summary>화면 가로 전체를 덮는 발판(= 그 좌우 끝이 곧 "화면의 물리적 끝"인 배치).</summary>
        private const long FullWidthFloorHandle = 9101L;

        /// <summary>★ 2026-09-02 — <b>두 번째 모니터의 창</b>을 흉내내는 발판. 우리 오버레이 화면
        /// 바깥(OS x &gt; Screen.width)에 놓여 <c>GroundInfo.ScreenRightWorldX</c>(전체 발판 통합
        /// 경계)만 바깥으로 밀어낸다 — 캐릭터가 딛는 발판은 여전히 화면 전폭 바닥 하나다.</summary>
        private const long OffscreenNeighborHandle = 9102L;

        private const float SettleWaitSeconds = 2.5f;

        /// <summary>★ 절대 시한 — 화면 끝을 향해 걷기 시작한 뒤 이 시간 안에 스스로 돌아서야 한다.</summary>
        private const float MaxTurnaroundSeconds = 5f;

        /// <summary>관찰을 끝내기 전 "돌아선 뒤 실제로 되돌아 걸었는가"를 확인하는 시간.</summary>
        private const float PostTurnObserveSeconds = 3f;

        /// <summary>돌아선 뒤 안쪽으로 최소한 이만큼(월드 유닛)은 실제로 이동해야 한다(러닝머신 아님의 증거).</summary>
        private const float MinInwardTravelUnits = 1f;

        /// <summary>캐릭터가 "갈 수 있는 한계"를 넘어가도 되는 최대치(월드 유닛). 하드 클램프가 붙잡으므로
        /// 원리적으로 0에 가까워야 한다 — 프레임 경계의 미세 오차만 허용한다.</summary>
        private const float MaxWalkableOverrunUnits = 0.05f;

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
        private float _floorTopWorldY;

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

        /// <summary>
        /// 공통 준비 — 화면 가로 **전체**를 덮는 발판 한 장만 두어, 그 좌우 경계가 곧 화면의 물리적
        /// 끝이 되게 한다(GroundInfo.CurrentFootholdLeft/RightWorldX == ScreenLeft/RightWorldX →
        /// isTrueScreenEdge == true). 실제 배포에서 안전망 발판이 뷰포트 폭과 일치하는 상황과 같다.
        /// StickConfig는 복제본을 꽂아 원본 자산을 절대 건드리지 않는다(CLAUDE.md 불변 원칙 3).
        /// </summary>
        private IEnumerator SetUpFullWidthFloor()
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
            Assert.IsNotNull(cam, $"{LogPrefix} 블랙보드에 카메라가 없습니다.");

            float w = Screen.width;
            float h = Screen.height;
            float floorTopOs = h * 0.70f;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(FullWidthFloorHandle, new Rect(0f, floorTopOs, w, h - floorTopOs), false));
            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            _floorTopWorldY = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, floorTopOs), 10f, _clonedConfig).y;

            float centerX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, floorTopOs), 10f, _clonedConfig).x;
            PlaceOnFloor(bb, centerX);

            // 전제 확인 — 이 발판의 좌우 경계가 실제로 "화면 자체의 끝"으로 판정되어야 이 테스트가
            // 의미를 갖는다(그렇지 않으면 그냥 평범한 발판 경계를 보고 있는 셈이다).
            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded, $"{LogPrefix} 준비 실패 — 화면 전체 발판 위에 접지하지 못했습니다.");
            Assert.AreEqual(info.ScreenRightWorldX, info.CurrentFootholdRightWorldX, 0.01f,
                $"{LogPrefix} 준비 실패 — 발판 오른쪽 경계가 화면 끝과 일치하지 않습니다.");
            Assert.AreEqual(info.ScreenLeftWorldX, info.CurrentFootholdLeftWorldX, 0.01f,
                $"{LogPrefix} 준비 실패 — 발판 왼쪽 경계가 화면 끝과 일치하지 않습니다.");

            Assert.IsTrue(bb.TryGetWalkableScreenBoundsWorld(out float wl, out float wr),
                $"{LogPrefix} 준비 실패 — 걸을 수 있는 한계를 조회하지 못했습니다.");

            Debug.Log($"{LogPrefix} 준비 완료 — 화면 {w:F0}x{h:F0}, 발판 상단 월드Y={_floorTopWorldY:F3}, " +
                $"발판 X범위={info.CurrentFootholdLeftWorldX:F3}~{info.CurrentFootholdRightWorldX:F3}, " +
                $"걸을 수 있는 한계={wl:F3}~{wr:F3}(발판 경계보다 좌우 각각 " +
                $"{(wl - info.CurrentFootholdLeftWorldX):F3}/{(info.CurrentFootholdRightWorldX - wr):F3}유닛 안쪽), " +
                $"시각 반폭={bb.CharacterVisualHalfWidthWorld:F3}유닛, wanderEdgeStopDistance={_clonedConfig.wanderEdgeStopDistance:F2}유닛");
        }

        private void PlaceOnFloor(StickmanBlackboard bb, float worldX)
        {
            bb.Body.position = new Vector2(worldX, _floorTopWorldY);
            bb.Body.transform.position = new Vector3(worldX, _floorTopWorldY, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = FullWidthFloorHandle;
            bb.ResetGroundLossTimer();
        }

        // ============================================================================
        // (1) 단일 소스 계약 — "갈 수 있는 한계" 조회 == 하드 클램프가 실제로 세우는 자리
        // ============================================================================

        [UnityTest]
        public IEnumerator WalkableBoundsAreExactlyWhereTheHardClampStopsTheCharacter()
        {
            yield return SetUpFullWidthFloor();
            StickmanBlackboard bb = _agent.Blackboard;

            // 오른쪽 — 한계보다 한참 바깥에 강제로 놓고 클램프를 직접 돌린 뒤 위치를 대조한다.
            // (같은 프레임 안에서 처리하므로 에이전트 Update가 끼어들 여지가 없다.)
            Assert.IsTrue(bb.TryGetWalkableScreenBoundsWorld(out float leftBefore, out float rightBefore),
                $"{LogPrefix} 걸을 수 있는 한계 조회 실패.");

            bb.Body.position = new Vector2(rightBefore + 3f, _floorTopWorldY);
            bb.Body.transform.position = new Vector3(rightBefore + 3f, _floorTopWorldY, bb.Body.transform.position.z);
            bb.EnforceScreenBoundsAndRescue(0.02f);
            float clampedRight = bb.Body.position.x;

            bb.Body.position = new Vector2(leftBefore - 3f, _floorTopWorldY);
            bb.Body.transform.position = new Vector3(leftBefore - 3f, _floorTopWorldY, bb.Body.transform.position.z);
            bb.EnforceScreenBoundsAndRescue(0.02f);
            float clampedLeft = bb.Body.position.x;

            Debug.Log($"{LogPrefix} 단일 소스 대조 — 조회한 한계=({leftBefore:F4}, {rightBefore:F4}), " +
                $"클램프가 실제로 세운 자리=({clampedLeft:F4}, {clampedRight:F4}), " +
                $"오차=({Mathf.Abs(clampedLeft - leftBefore):F4}, {Mathf.Abs(clampedRight - rightBefore):F4})유닛");

            Assert.AreEqual(rightBefore, clampedRight, 0.01f,
                $"{LogPrefix} 오른쪽: 조회한 '갈 수 있는 한계'({rightBefore:F4})와 하드 클램프가 실제로 세운 " +
                $"자리({clampedRight:F4})가 다릅니다 — 두 값이 서로 다른 계산식에서 나오고 있다는 뜻이고, " +
                "그러면 배회 AI의 경계 판정이 다시 어긋나 러닝머신 증상이 재발합니다.");
            Assert.AreEqual(leftBefore, clampedLeft, 0.01f,
                $"{LogPrefix} 왼쪽: 조회한 '갈 수 있는 한계'({leftBefore:F4})와 하드 클램프가 실제로 세운 " +
                $"자리({clampedLeft:F4})가 다릅니다(위와 동일한 회귀).");

            // 한계가 발판 원시 경계보다 확실히 안쪽이어야 이 테스트가 의미를 갖는다 — 두 값이 같다면
            // 애초에 러닝머신이 생기지 않으므로, 조건 자체가 성립하는지도 함께 못 박는다.
            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.Less(rightBefore, info.ScreenRightWorldX,
                $"{LogPrefix} 전제 실패 — 걸을 수 있는 한계가 화면 끝보다 안쪽이 아닙니다(클램프 여유가 0?).");
            Assert.Greater(leftBefore, info.ScreenLeftWorldX,
                $"{LogPrefix} 전제 실패 — 왼쪽도 마찬가지입니다.");
        }

        // ============================================================================
        // (2) ★ 본체 — 화면 끝을 향해 걷게 두면 절대 시한 안에 스스로 돌아선다
        // ============================================================================

        [UnityTest]
        public IEnumerator WalksIntoScreenEdgeAndTurnsAroundWithinAbsoluteDeadline()
        {
            yield return SetUpFullWidthFloor();
            StickmanBlackboard bb = _agent.Blackboard;

            // 배회를 결정론에 가깝게 조인다. 핵심은 아래 두 줄이다:
            //   · Walk 지속시간 30초 >> 관찰 창 5초  → "타이머가 만료돼 우연히 풀리는" 탈출구 제거
            //   · 즉흥 방향전환 확률 0               → 방향이 바뀔 수 있는 경로가 경계 판정 하나만 남음
            _clonedConfig.wanderIdleDurationMin = 0.05f;
            _clonedConfig.wanderIdleDurationMax = 0.05f;
            _clonedConfig.wanderWalkDurationMin = 30f;
            _clonedConfig.wanderWalkDurationMax = 30f;
            _clonedConfig.wanderDurationJitterRatio = 0f;
            _clonedConfig.wanderSpontaneousTurnChance = 0f;
            _clonedConfig.wanderPostIdleWalkChance = 1f;
            _clonedConfig.wanderPostIdleJumpChance = 0f;
            _clonedConfig.wanderEdgeJumpAttemptChance = 0f;
            _clonedConfig.wanderEdgeTurnPauseMin = 0.15f;
            _clonedConfig.wanderEdgeTurnPauseMax = 0.15f;
            // 화면 끝에서는 어차피 금지된 행동들이지만(isTrueScreenEdge 가드), 그 가드가 회귀했을 때
            // 이 테스트가 엉뚱한 이유로 초록불이 되지 않도록 확률 자체를 0으로 못 박는다.
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.stepUpChance = 0f;

            // ★ 에이전트가 들고 있는 AutoWanderController는 **원본** StickConfig로 생성돼 있어 복제본
            // 설정이 반영되지 않는다(생성자 주입). 원본 자산을 런타임에 고쳐 쓰는 것은 금지이므로
            // (CLAUDE.md 불변 원칙 3), 복제본으로 만든 컨트롤러를 IntentSource에 꽂고 이 코루틴이
            // 직접 Tick한다(Tests/PlayMode/EdgeHopDownTests.cs와 동일한 관례).
            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260829));
            bb.IntentSource = wander;

            // ── 1단계: 배회가 Walk 페이즈에 들어가 방향을 뽑을 때까지 기다린다.
            float warmup = 0f;
            while (Mathf.Approximately(wander.MoveInputX, 0f) && warmup < 3f)
            {
                yield return null;
                float dt = Time.deltaTime;
                warmup += dt;
                wander.Tick(dt);
            }
            Assert.AreNotEqual(0f, wander.MoveInputX,
                $"{LogPrefix} 준비 실패 — {warmup:F2}초 동안 배회가 Walk 페이즈에 진입하지 않았습니다.");

            int dir = wander.MoveInputX > 0f ? 1 : -1;

            // ── 2단계: 그 방향의 화면 끝 바로 앞(1.5유닛 안쪽)에 세우고 Walk로 만든다. AI가 뽑은 방향을
            // 그대로 존중하므로(좌/우 어느 쪽이든) 방향을 강제로 조작하지 않는다.
            Assert.IsTrue(bb.TryGetWalkableScreenBoundsWorld(out float walkableLeft, out float walkableRight),
                $"{LogPrefix} 걸을 수 있는 한계 조회 실패.");
            const float StartInsetUnits = 1.5f;
            float startX = dir > 0 ? walkableRight - StartInsetUnits : walkableLeft + StartInsetUnits;
            PlaceOnFloor(bb, startX);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            Debug.Log($"{LogPrefix} 관찰 시작 — 방향={(dir > 0 ? "오른쪽" : "왼쪽")}, 시작 X={startX:F3}, " +
                $"그 방향의 한계={(dir > 0 ? walkableRight : walkableLeft):F3}(남은 거리 {StartInsetUnits:F2}유닛), " +
                $"Walk 지속시간={_clonedConfig.wanderWalkDurationMin:F1}초, 절대 시한={MaxTurnaroundSeconds:F1}초");

            // ── 3단계: 관찰. 방향이 뒤집히는 순간까지, 그리고 그 뒤 실제로 되돌아 걷는지까지.
            float elapsed = 0f;
            float turnedAtSeconds = -1f;
            float xAtTurn = 0f;
            float extremeX = startX;              // 진행 방향으로 가장 멀리 간 지점
            float maxOverrun = float.NegativeInfinity; // (진행 방향 기준) 한계를 넘어선 최대치
            float inwardTravel = 0f;
            bool sawFall = false;

            while (elapsed < MaxTurnaroundSeconds + PostTurnObserveSeconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                wander.Tick(dt);

                float x = bb.Body.position.x;
                if (bb.Machine.CurrentStateId == StickmanStateId.Fall) sawFall = true;

                // 한계는 매 프레임 다시 물어본다 — 시각 반폭이 포즈에 따라 변하므로 캐싱하면 안 된다.
                if (bb.TryGetWalkableScreenBoundsWorld(out float l, out float r))
                {
                    float overrun = dir > 0 ? x - r : l - x;
                    if (overrun > maxOverrun) maxOverrun = overrun;
                }

                if (dir > 0) extremeX = Mathf.Max(extremeX, x);
                else extremeX = Mathf.Min(extremeX, x);

                if (turnedAtSeconds < 0f)
                {
                    if (wander.MoveInputX * dir < 0f)
                    {
                        turnedAtSeconds = elapsed;
                        xAtTurn = x;
                        Debug.Log($"{LogPrefix} 방향 전환 관측 — t={turnedAtSeconds:F2}초, X={xAtTurn:F3}, " +
                            $"진행 방향 최원점 X={extremeX:F3}, 상태={bb.Machine.CurrentStateId}");
                    }
                }
                else
                {
                    inwardTravel = dir > 0 ? xAtTurn - bb.Body.position.x : bb.Body.position.x - xAtTurn;
                    if (elapsed - turnedAtSeconds >= PostTurnObserveSeconds) break;
                }
            }

            float boundOnThatSide = dir > 0 ? walkableRight : walkableLeft;
            Debug.Log($"{LogPrefix} 실측 결과 — 방향전환 시각={turnedAtSeconds:F2}초(시한 {MaxTurnaroundSeconds:F1}초), " +
                $"전환 지점 X={xAtTurn:F3}, 진행 방향 최원점 X={extremeX:F3}, 그 방향 한계={boundOnThatSide:F3}, " +
                $"한계 초과 최대치={maxOverrun:F4}유닛, 전환 후 안쪽 이동={inwardTravel:F3}유닛, " +
                $"Fall 관측={sawFall}, 총 관찰={elapsed:F2}초");

            Assert.GreaterOrEqual(turnedAtSeconds, 0f,
                $"{LogPrefix} 화면 끝을 향해 걷다가 {MaxTurnaroundSeconds + PostTurnObserveSeconds:F1}초 안에 " +
                "방향을 바꾸지 못했습니다 — 제자리 걷기(러닝머신)에 갇혔다는 뜻입니다. " +
                $"(Walk 지속시간을 {_clonedConfig.wanderWalkDurationMin:F0}초로 잡아 타이머 만료로 풀리는 경로를 " +
                "막아뒀고 즉흥 방향전환 확률도 0이므로, 방향이 바뀔 수 있는 경로는 경계 판정 하나뿐입니다.)");
            Assert.Less(turnedAtSeconds, MaxTurnaroundSeconds,
                $"{LogPrefix} 방향은 바꿨지만 절대 시한 {MaxTurnaroundSeconds:F1}초를 넘겼습니다({turnedAtSeconds:F2}초).");
            Assert.LessOrEqual(maxOverrun, MaxWalkableOverrunUnits,
                $"{LogPrefix} 캐릭터가 '갈 수 있는 한계'를 {maxOverrun:F4}유닛 넘어갔습니다 — " +
                "화면 밖으로 나가지 않는다는 불변식이 깨졌습니다.");
            Assert.GreaterOrEqual(inwardTravel, MinInwardTravelUnits,
                $"{LogPrefix} 돌아선 뒤 {PostTurnObserveSeconds:F1}초 동안 안쪽으로 {inwardTravel:F3}유닛밖에 " +
                $"이동하지 못했습니다(최소 {MinInwardTravelUnits:F1}유닛) — 방향만 바뀌고 몸은 여전히 " +
                "제자리라면 러닝머신이 그대로인 것입니다.");
            Assert.IsFalse(sawFall,
                $"{LogPrefix} 화면 끝에서 낙하했습니다 — 화면 끝에서는 뛰어내리기/매달리기가 금지여야 합니다" +
                "(isTrueScreenEdge 가드 회귀).");
        }

        // ============================================================================
        // (3) ★★ 2026-09-02 — 멀티모니터에서 되살아난 러닝머신
        //
        //     사용자 신고: "지금 멀티모니터 쓰는데 창에서 다른 모니터로 못넘어가는데도
        //                  끝 벽쪽에서 계속 걷고 있음. 제자리걸음인거지"
        //
        //     위 (2)와 **딱 한 가지만** 다르다: 화면 바깥에 발판이 하나 더 있다. 그것만으로
        //     GroundInfo.ScreenRightWorldX(전체 발판 통합 경계)가 화면 밖으로 밀려나고,
        //     2026-08-29 러닝머신 수정이 걸어 둔 게이트(isTrueScreenEdge)가 거짓이 되어
        //     보정이 통째로 꺼진다. 캐릭터가 딛는 발판도, 클램프도, 화면도 (2)와 완전히 같다.
        //
        //     ★ 이 테스트는 수정 전 빨간불이어야 한다. 초록이면 재현 조건을 못 만든 것이다.
        // ============================================================================

        [UnityTest]
        public IEnumerator 화면밖_이웃발판이_있어도_화면_끝에서_돌아선다()
        {
            yield return SetUpFullWidthFloor();
            StickmanBlackboard bb = _agent.Blackboard;

            // ── 재현 조건 주입: 화면 오른쪽 바깥에 발판 하나(2번 모니터의 창).
            //    캐릭터가 닿을 수 없는 높이/위치라 접지 후보로는 절대 뽑히지 않는다 —
            //    바뀌는 것은 오직 "전체 발판 통합 경계"뿐이다.
            float w = Screen.width;
            float h = Screen.height;
            _service.Footholds.Add(new PlatformFoothold(OffscreenNeighborHandle,
                new Rect(w + 200f, h * 0.20f, 800f, 40f), false));
            _poller.PollImmediately();
            yield return null;

            GroundSensor.GroundInfo info0 = bb.SenseGround();
            Assert.IsTrue(info0.Grounded, $"{LogPrefix} 이웃 발판 추가 후 접지를 잃었습니다 — 재현 조건이 잘못됐습니다.");
            Assert.AreEqual(FullWidthFloorHandle, info0.GroundedFootholdHandle,
                $"{LogPrefix} 캐릭터가 화면 밖 이웃 발판을 딛고 있습니다 — 재현 조건이 잘못됐습니다.");
            Assert.Greater(info0.ScreenRightWorldX, info0.CurrentFootholdRightWorldX + 0.05f,
                $"{LogPrefix} 재현 조건 실패 — 통합 경계({info0.ScreenRightWorldX:F3})가 딛고 있는 발판의 " +
                $"오른쪽 끝({info0.CurrentFootholdRightWorldX:F3})보다 바깥이어야 합니다. " +
                "이 부등호가 성립하지 않으면 옛 게이트가 그대로 참이라 버그가 재현되지 않습니다.");

            // ── (2)와 같은 결정론 조임.
            _clonedConfig.wanderIdleDurationMin = 0.05f;
            _clonedConfig.wanderIdleDurationMax = 0.05f;
            _clonedConfig.wanderWalkDurationMin = 30f;
            _clonedConfig.wanderWalkDurationMax = 30f;
            _clonedConfig.wanderDurationJitterRatio = 0f;
            _clonedConfig.wanderSpontaneousTurnChance = 0f;
            _clonedConfig.wanderPostIdleWalkChance = 1f;
            _clonedConfig.wanderPostIdleJumpChance = 0f;
            _clonedConfig.wanderEdgeJumpAttemptChance = 0f;
            _clonedConfig.wanderEdgeTurnPauseMin = 0.15f;
            _clonedConfig.wanderEdgeTurnPauseMax = 0.15f;
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.stepUpChance = 0f;

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260902));
            bb.IntentSource = wander;

            // ── 오른쪽(= 화면 밖 이웃이 있는 쪽)으로 강제로 걷게 만든다. (2)는 AI가 뽑은 방향을
            //    존중하지만 여기서는 **재현 조건이 오른쪽에만 있으므로** 방향을 고정해야 한다.
            float warmup = 0f;
            while (wander.MoveInputX <= 0f && warmup < 6f)
            {
                yield return null;
                float dt = Time.deltaTime;
                warmup += dt;
                wander.Tick(dt);
            }
            Assert.Greater(wander.MoveInputX, 0f,
                $"{LogPrefix} 준비 실패 — {warmup:F2}초 동안 배회가 오른쪽 걷기를 뽑지 않았습니다.");

            Assert.IsTrue(bb.TryGetWalkableScreenBoundsWorld(out _, out float walkableRight),
                $"{LogPrefix} 걸을 수 있는 한계 조회 실패.");
            const float StartInsetUnits = 1.5f;
            float startX = walkableRight - StartInsetUnits;
            PlaceOnFloor(bb, startX);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            Debug.Log($"{LogPrefix} (멀티모니터) 관찰 시작 — 시작 X={startX:F3}, 갈 수 있는 한계={walkableRight:F3}, " +
                $"딛은 발판 오른쪽 끝={info0.CurrentFootholdRightWorldX:F3}, 통합 경계={info0.ScreenRightWorldX:F3}, " +
                $"클램프가 발판 끝보다 {(info0.CurrentFootholdRightWorldX - walkableRight) * 1f:F3}유닛 앞에서 막습니다. " +
                $"Walk 지속시간={_clonedConfig.wanderWalkDurationMin:F0}초, 절대 시한={MaxTurnaroundSeconds:F1}초");

            // ── 관찰: 벽시계(초) 기준. 프레임 수 예산 금지(배치모드는 2,000fps 이상으로 돈다).
            float elapsed = 0f;
            float turnedAtSeconds = -1f;
            float xAtTurn = 0f;
            float inwardTravel = 0f;
            float maxOverrun = float.NegativeInfinity;
            bool sawFall = false;

            while (elapsed < MaxTurnaroundSeconds + PostTurnObserveSeconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                wander.Tick(dt);

                float x = bb.Body.position.x;
                if (bb.Machine.CurrentStateId == StickmanStateId.Fall) sawFall = true;
                if (bb.TryGetWalkableScreenBoundsWorld(out _, out float r))
                {
                    float overrun = x - r;
                    if (overrun > maxOverrun) maxOverrun = overrun;
                }

                if (turnedAtSeconds < 0f)
                {
                    if (wander.MoveInputX < 0f)
                    {
                        turnedAtSeconds = elapsed;
                        xAtTurn = x;
                    }
                }
                else
                {
                    inwardTravel = xAtTurn - x;
                    if (elapsed - turnedAtSeconds >= PostTurnObserveSeconds) break;
                }
            }

            Debug.Log($"{LogPrefix} (멀티모니터) 결과 — 방향전환={turnedAtSeconds:F2}초(시한 {MaxTurnaroundSeconds:F1}초), " +
                $"전환 지점 X={xAtTurn:F3}, 한계 초과 최대치={maxOverrun:F4}유닛, " +
                $"전환 후 안쪽 이동={inwardTravel:F3}유닛, Fall={sawFall}, 총 관찰={elapsed:F2}초");

            Assert.GreaterOrEqual(turnedAtSeconds, 0f,
                $"{LogPrefix} 화면 밖에 발판이 하나 더 있다는 이유만으로 캐릭터가 " +
                $"{MaxTurnaroundSeconds + PostTurnObserveSeconds:F1}초 동안 돌아서지 못했습니다 — " +
                "사용자가 신고한 멀티모니터 제자리걸음입니다. (Walk 지속시간 30초 + 즉흥 전환 0%이므로 " +
                "방향이 바뀔 수 있는 경로는 경계 판정 하나뿐입니다.)");
            Assert.Less(turnedAtSeconds, MaxTurnaroundSeconds,
                $"{LogPrefix} 방향은 바꿨지만 절대 시한 {MaxTurnaroundSeconds:F1}초를 넘겼습니다({turnedAtSeconds:F2}초).");
            Assert.LessOrEqual(maxOverrun, MaxWalkableOverrunUnits,
                $"{LogPrefix} 캐릭터가 '갈 수 있는 한계'를 {maxOverrun:F4}유닛 넘어갔습니다.");
            Assert.GreaterOrEqual(inwardTravel, MinInwardTravelUnits,
                $"{LogPrefix} 돌아선 뒤 안쪽으로 {inwardTravel:F3}유닛밖에 이동하지 못했습니다(최소 {MinInwardTravelUnits:F1}유닛).");
            Assert.IsFalse(sawFall,
                $"{LogPrefix} 화면 끝에서 낙하했습니다 — 클램프가 막는 지점에서는 뛰어내리기/매달리기가 금지여야 합니다.");
        }
    }
}
