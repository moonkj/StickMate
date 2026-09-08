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
    /// <para>배경: §9-4-A(ropeClimbChance 0→0.20)+§9-4-B(hop-down 좁은 보강)를 적용한 뒤에도 리더가
    /// 실기(macOS)에서 장시간 관찰한 결과 자연발동이 <b>전혀</b> 없었다 — 캐릭터가 Dock↔안전망 사이를
    /// 무한 왕복했을 뿐 로프 등반은 단 한 번도 평가되지 않았다. 재조사로 밝혀진 원인 셋을 이 스위트가
    /// 각각 직접 잠근다(RopeClimbHopDownOrderingTests가 이미 잠근 §9-4-B 시나리오와는 다른 축이다).</para>
    ///
    /// <list type="number">
    ///   <item><b>탐색 폭</b> — 로프 대역 벽이 좁은 탐색폭 바깥에 있으면 절대 못 찾았다.
    ///     <see cref="넓은_탐색_전용_로프벽도_안전망이_상시존재해도_발동한다"/>.</item>
    ///   <item><b>겹침 형상</b> — 실기의 진짜 형상은 "멀리 있는 벽"이 아니라 <b>경계를 가로질러 걸친 창</b>
    ///     이었다(Dock 254~1259 / Finder 976~1512, 경계 1259). 이 형상은 <b>탐색 폭을 아무리 넓혀도</b>
    ///     원리상 안 잡힌다 — 폭만 넓힌 1차 시도가 정확히 그 함정에 빠졌다.
    ///     <see cref="경계를_가로질러_걸친_창은_폭을_아무리_넓혀도_좁은탐색이_못찾고_겹침탐색만_찾는다"/>.</item>
    ///   <item><b>확률 결합</b> — 예전 코드는 로프 추첨이 되올라가기 블록의 else 안에 있어 실효 확률이
    ///     stepUpChance × ropeClimbChance였다(§9-3).
    ///     <see cref="stepUpChance가_0이어도_로프등반은_독립적으로_평가된다"/>.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★★ 2026-09-07 자기 부검 — 이 파일의 <b>1차판이 전제 단계에서 죽었다</b>(거짓 통과와 한 몸)
    /// ============================================================================
    /// <para>1차판은 발판을 배선한 <b>직후, 캐릭터가 지면 한복판에 선 채로</b> 네 개의 판정 함수를
    /// 직접 불러 "전제"를 확인했다. 그런데 이 프로젝트의 경계 판정 3종
    /// (<c>GroundSensor.TryFindClimbableWall</c> / <c>TryFindClimbableWallOverlapping</c> /
    /// <c>TryFindDescendTarget</c>)은 전부 첫 줄이 <c>if (distanceToEdge &gt; probeReach) return false;</c>다.
    /// 배치모드 화면(640×480, 카메라 orthographic size 12 → 0.05유닛/px)에서 지면 한복판은 경계에서
    /// <b>3.200유닛</b>인데 그 게이트(<c>StickmanBlackboard.EdgeProbeReachWorld</c>)는 <b>0.500유닛</b>이다 —
    /// <b>6.4배 밖</b>이라 네 판정 전부가 벽/발판의 실제 배치와 무관하게 false였다.</para>
    ///
    /// <para>★ 그리고 더 나쁜 절반: 그 중 <b>둘은 IsFalse 단언이라 "통과"했다</b>. 좁은 탐색이 벽을 못
    /// 찾은 이유가 "거리가 멀어서"가 아니라 "게이트가 막아서"였는데도 초록이었다 — 이 저장소가 반복해서
    /// 당한 <b>「부재 단언은 썩으면 조용히 초록이 된다」</b>의 교과서적 재현이다(CLAUDE.md 협업 프로토콜).</para>
    ///
    /// <para>그래서 2차판은 세 가지를 바꿨다:
    /// <list type="bullet">
    ///   <item>전제 판정을 <b>프로덕션이 실제로 판정하는 자리</b>(경계에서 <c>EdgeProbeReachWorld</c> 안)에서
    ///     잰다. 그 위치도 상수를 베끼지 않고 <c>bb.EdgeProbeReachWorld</c>에서 유도하고, 게이트가
    ///     성립한다는 것 자체를 <b>숫자로 먼저 단언</b>한다(그래야 다음 실패가 "배치 오류"로 뭉개지지 않는다).</item>
    ///   <item>모든 <b>부재 단언에 같은 순간·같은 위치의 존재 단언을 짝으로</b> 붙인다(좁은 탐색은 못 찾고
    ///     넓은 탐색은 찾는다). 한쪽만 있으면 게이트가 다시 막았을 때 조용히 초록이 된다.</item>
    ///   <item><b>네거티브 컨트롤</b>을 넣는다 — 프로덕션 탐색/추첨을 그 자신의 노브로 일부러 죽였을 때
    ///     이 테스트가 실제로 빨개지는지 같은 실행 안에서 보인다.</item>
    /// </list></para>
    ///
    /// <para>★ 1차판의 "진입 후 되튕김 감시" 블록(<c>if (sawRopeClimbState)</c>)은 <b>한 번도 실행되지
    /// 않는 죽은 단언</b>이었다 — 관측 루프가 <c>RopeClimbRequested</c> 펄스에서 즉시 빠져나오는데, 그
    /// 펄스를 <c>WalkState</c>가 소비해 상태가 바뀌는 것은 <b>그 다음 프레임의 Update</b>이기 때문이다
    /// (형제 스위트 RopeClimbHopDownOrderingTests의 실측 로그도 <c>RopeClimb상태진입=False</c>였다).
    /// 2차판은 펄스를 본 뒤 <b>상태 전이까지 벽시계로 기다렸다가</b> 유지 여부를 확인한다 — 그래야
    /// "Enter()가 좁은 탐색으로 회귀해 즉시 취소"라는 실기 사고가 실제로 잠긴다.</para>
    /// </summary>
    public sealed class RopeClimbIndependentPriorityTests
    {
        private const string LogPrefix = "[밧줄-독립우선순위-테스트]";

        private const long GroundHandle = 7601L;
        private const long LowDropLeftHandle = 7602L;
        private const long LowDropRightHandle = 7603L;
        private const long TallWallLeftHandle = 7604L;
        private const long TallWallRightHandle = 7605L;

        // ★ 방향 분별/동률 파훼 전용(아래 마지막 테스트). 핸들 크기 관계 자체가 파훼 규칙 ③의
        // 기대값이므로 **오름차순으로 읽히게** 이름을 붙였다 — 값을 바꾸면 그 단언의 뜻이 바뀐다.
        private const long ForwardWallHandle = 7606L;
        private const long RearWallHandle = 7607L;
        // ★ 여기서 **먼 쪽에 더 작은 핸들**을 주는 것이 의도다(③ 단계). 그래야 「진행방향 근접」 키가
        // 실제로 이겼는지가 증명된다 — 근접 키가 사라져 핸들 키로 흘러내리면 답이 먼 쪽으로 뒤집힌다.
        // 가까운 쪽에 작은 핸들을 주면 두 키가 같은 답을 내서 무엇이 이겼는지 영영 못 가른다.
        private const long FarWallHandle = 7608L;
        private const long NearWallHandle = 7609L;
        private const long TwinLowerHandle = 7610L;
        private const long TwinHigherHandle = 7611L;

        private const float SettleWaitSeconds = 2.5f;
        private const float MaxObserveSeconds = 20f;

        /// <summary>펄스를 본 뒤 <c>WalkState</c>가 그것을 소비해 상태가 바뀔 때까지 기다리는 벽시계 예산(초).
        /// 소비는 다음 프레임 Update 한 번이면 끝나므로 이 값은 넉넉한 상한이다(프레임 수로 세지 않는다 —
        /// CLAUDE.md "시간 기반 검증은 벽시계 기준").</summary>
        private const float EntryWindowSeconds = 2f;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        /// <summary>관측 결과 묶음 — C# 반복자는 out 인자를 못 받으므로 가변 객체로 돌려받는다.</summary>
        private sealed class Observation
        {
            public bool SawRequest;
            public bool SawState;
            public bool SawFall;
            public float RequestSeconds = -1f;
            public float StateSeconds = -1f;
            public float ElapsedSeconds;
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
            // 정적이라 다음 테스트로 샌다 — 반드시 되돌린다(RopeClimbQaOverrideTests와 같은 관례).
            RopeClimbQaOverride.SetChanceTestOverride(null);
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        // ============================================================================
        // 공통 준비
        // ============================================================================

        /// <summary>씬 로드, 블랙보드 확보, 설정 복제까지. 발판은 각 테스트가 직접 짠다.</summary>
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

        private static void PlaceBody(StickmanBlackboard bb, float worldX, float worldY, long footholdHandle)
        {
            bb.Body.position = new Vector2(worldX, worldY);
            bb.Body.transform.position = new Vector3(worldX, worldY, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = footholdHandle;
            bb.ResetGroundLossTimer();
        }

        /// <summary>
        /// ★ 이 헬퍼가 1차판 사고의 처방 본체다.
        ///
        /// <para>경계 판정 3종은 전부 <c>distanceToEdge &gt; probeReach</c>면 즉시 false다. 그래서 전제
        /// 확인은 <b>프로덕션이 실제로 판정하는 자리</b>, 즉 진행방향 경계에서 <c>EdgeProbeReachWorld</c>
        /// 안쪽으로 들어간 지점에서만 의미가 있다. 위치는 그 값에서 <b>유도</b>하고(상수를 베끼지 않는다),
        /// 게이트가 실제로 성립했는지를 프로덕션과 <b>같은 식</b>으로 다시 계산해 숫자로 단언한다 —
        /// 다음에 이 스위트가 빨개지면 "배치 오류"라는 뭉뚱그린 문구 대신 어느 항이 깨졌는지가 남는다.</para>
        /// </summary>
        private GroundSensor.GroundInfo SenseAtEdge(StickmanBlackboard bb, int direction, float groundTopWorldY, string label)
        {
            GroundSensor.GroundInfo before = bb.SenseGround();
            Assert.IsTrue(before.Grounded,
                $"{LogPrefix} [{label}] 전제 실패(접지) — 지면 한복판에서조차 접지가 아닙니다. " +
                $"발판 캐시 {bb.FootholdPoller.CachedFootholds.Count}개, 고착핸들={bb.CurrentFootholdHandle}, " +
                $"몸 위치={bb.Body.position}. 발판 배선/스냅 허용오차부터 의심하십시오.");

            float reach = bb.EdgeProbeReachWorld;
            float edgeBefore = direction > 0 ? before.CurrentFootholdRightWorldX : before.CurrentFootholdLeftWorldX;
            // 게이트 안쪽 절반 지점 — "경계에 닿기 직전"이라는 프로덕션의 판정 자리를 그 값에서 유도한다.
            float probeX = edgeBefore - direction * (reach * 0.5f);
            PlaceBody(bb, probeX, groundTopWorldY, GroundHandle);

            GroundSensor.GroundInfo at = bb.SenseGround();
            Assert.IsTrue(at.Grounded,
                $"{LogPrefix} [{label}] 전제 실패(접지) — 경계 근처({probeX:F3})로 옮긴 뒤 접지를 잃었습니다.");
            Assert.AreEqual(GroundHandle, at.GroundedFootholdHandle,
                $"{LogPrefix} [{label}] 전제 실패(발판) — 딛고 있는 발판이 시험 지면이 아닙니다.");

            float edgeAt = direction > 0 ? at.CurrentFootholdRightWorldX : at.CurrentFootholdLeftWorldX;
            float distanceToEdge = direction > 0 ? edgeAt - bb.Body.position.x : bb.Body.position.x - edgeAt;
            Assert.LessOrEqual(distanceToEdge, reach,
                $"{LogPrefix} [{label}] 전제 실패(경계 게이트) — 경계까지 {distanceToEdge:F3}유닛인데 " +
                $"프로덕션 판정 도달거리는 {reach:F3}유닛입니다. 이 상태에서는 벽/발판 탐색 3종이 " +
                "배치와 무관하게 전부 false를 돌려줍니다(1차판이 정확히 여기서 죽었습니다).");
            Assert.Greater(distanceToEdge, 0f,
                $"{LogPrefix} [{label}] 전제 실패(경계 게이트) — 이미 경계를 넘어섰습니다({distanceToEdge:F3}유닛).");

            Debug.Log($"{LogPrefix} [{label}] 판정 자리 확보 — 방향={(direction > 0 ? "오른쪽" : "왼쪽")}, " +
                $"몸 x={bb.Body.position.x:F3}, 경계 x={edgeAt:F3}, 경계까지={distanceToEdge:F3}유닛 " +
                $"(프로덕션 도달거리 {reach:F3}유닛 이내), 지면 월드Y={at.GroundWorldY:F3}, 발판핸들={at.GroundedFootholdHandle}.");
            return at;
        }

        /// <summary>
        /// 자율 배회를 그대로 돌리며 로프 등반 요청/진입을 관측한다(벽시계 예산).
        /// <para>★ 상태 확인을 <c>wander.Tick()</c>보다 <b>먼저</b> 한다 — Tick의 첫 줄이 1프레임 펄스를
        /// 지우고, 상태 전이는 직전 프레임 Update에서 이미 일어나 있기 때문이다.</para>
        /// </summary>
        private IEnumerator ObserveRope(AutoWanderController wander, StickmanBlackboard bb, float budgetSeconds, Observation obs)
        {
            long lastHandle = bb.CurrentFootholdHandle;
            while (obs.ElapsedSeconds < budgetSeconds && !obs.SawRequest && !obs.SawState)
            {
                yield return null;
                float dt = Time.deltaTime;
                obs.ElapsedSeconds += dt;

                StickmanStateId state = bb.Machine.CurrentStateId;
                if (state == StickmanStateId.RopeClimb && !obs.SawState)
                {
                    obs.SawState = true;
                    obs.StateSeconds = obs.ElapsedSeconds;
                }
                if (state == StickmanStateId.Fall) obs.SawFall = true;

                wander.Tick(dt);
                if (wander.RopeClimbRequested && !obs.SawRequest)
                {
                    obs.SawRequest = true;
                    obs.RequestSeconds = obs.ElapsedSeconds;
                }

                long handle = bb.CurrentFootholdHandle;
                if (handle != lastHandle)
                {
                    Debug.Log($"{LogPrefix} 발판 이동 — {lastHandle} -> {handle} (t={obs.ElapsedSeconds:F2}s, 상태={state})");
                    lastHandle = handle;
                }
            }
        }

        /// <summary>
        /// ★★★ 1차판에서 <b>한 번도 실행되지 않았던</b> 회귀 잠금을 실제로 돌린다.
        ///
        /// <para>펄스를 본 뒤 상태 전이까지 기다렸다가(다음 프레임 Update가 <c>WalkState</c>에서 소비한다),
        /// Throw 최소 지속시간보다 <b>확실히 짧은</b> 창 동안 그 상태가 유지되는지 본다. 창 길이는
        /// 숫자를 베끼지 않고 <c>StickConfig.ropeThrowWindUpSeconds</c>의 80%로 잡는다 — WindUp 하나만으로도
        /// 이 창보다 길기 때문에, 이 창 안에 상태가 바뀌었다면 그것은 <b>취소</b>다(실기 사고: Enter()가
        /// 좁은 탐색으로 벽을 다시 찾다 실패해 "목표 벽 소실"로 즉시 되튕겼다).</para>
        /// </summary>
        private IEnumerator AssertRopeClimbEntersAndHolds(StickmanBlackboard bb, Observation obs)
        {
            // ★ 여기서는 wander.Tick()을 부르지 않는다 — 의도적이다.
            // RopeClimbRequested는 1프레임 펄스이고 AutoWanderController.Tick()의 첫 줄이 그것을 지운다.
            // 제품에서는 같은 Update 안에서 «Tick → 상태머신»이 순서대로 돌아 소비가 보장되지만
            // (StickmanAgent.Update가 _autoWander를 먼저 틱한다), 이 리그의 wander는 코루틴이 틱하므로
            // 소비는 **다음 프레임 Update**다. 여기서 한 번 더 틱하면 소비되기 전에 펄스를 우리 손으로
            // 지워 버려, 「전이가 안 됐다」는 결론이 리그 때문인지 제품 때문인지 영영 못 가른다.
            float entryElapsed = 0f;
            while (!obs.SawState && entryElapsed < EntryWindowSeconds)
            {
                yield return null;
                entryElapsed += Time.deltaTime;
                if (bb.Machine.CurrentStateId == StickmanStateId.RopeClimb)
                {
                    obs.SawState = true;
                    obs.StateSeconds = obs.ElapsedSeconds + entryElapsed;
                    break;
                }
            }

            Assert.IsTrue(obs.SawState,
                $"{LogPrefix} 요청 펄스(t={obs.RequestSeconds:F2}s)는 났는데 {EntryWindowSeconds:F1}초 안에 " +
                $"RopeClimb 상태로 전이하지 않았습니다(최종 상태={bb.Machine.CurrentStateId}). " +
                "WalkState의 RopeClimbPressed 재확인이 넓은 탐색으로 벽을 다시 못 찾았거나, 소비 프레임에서 " +
                "접지를 잃었을 가능성이 높습니다 — 펄스는 났는데 아무 일도 안 일어나는 이 형태가 §10 이전의 사고입니다.");

            float holdSeconds = _clonedConfig.ropeThrowWindUpSeconds * 0.8f;
            float holdElapsed = 0f;
            while (holdElapsed < holdSeconds)
            {
                yield return null;
                holdElapsed += Time.deltaTime;
                Assert.AreEqual(StickmanStateId.RopeClimb, bb.Machine.CurrentStateId,
                    $"{LogPrefix} RopeClimb 진입 {holdElapsed:F2}초 만에 {bb.Machine.CurrentStateId}로 돌아갔습니다 — " +
                    $"Throw의 WindUp({_clonedConfig.ropeThrowWindUpSeconds:F2}초)조차 못 채운 시간이므로 정상 종료가 아니라 " +
                    "취소입니다. RopeClimbState.Enter()가 넓은 탐색(TryFindRopeClimbWallWide)이 찾은 벽을 " +
                    "좁은 탐색으로 다시 찾다 실패해 '목표 벽 소실'로 즉시 취소했을 가능성이 높습니다.");
            }

            Debug.Log($"{LogPrefix} 진입 확인 — 요청 t={obs.RequestSeconds:F2}s, 진입 t={obs.StateSeconds:F2}s, " +
                $"이후 {holdSeconds:F2}초(WindUp {_clonedConfig.ropeThrowWindUpSeconds:F2}초의 80%) 동안 RopeClimb 유지됨.");
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

            // 가로/세로 변환계수를 실제 변환 함수로 재서 쓴다(식을 새로 만들지 않는다).
            Vector3 xProbe0 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(0f, groundTopOs), 10f, _clonedConfig);
            Vector3 xProbe1 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(100f, groundTopOs), 10f, _clonedConfig);
            float unitsPerPixelX = Mathf.Abs(xProbe1.x - xProbe0.x) / 100f;
            Vector3 y0 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig);
            Vector3 y1 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs - 100f), 10f, _clonedConfig);
            float unitsPerPixelY = Mathf.Abs(y1.y - y0.y) / 100f;
            Assert.Greater(unitsPerPixelX, 0.0001f, $"{LogPrefix} 가로 좌표 변환이 성립하지 않습니다.");
            Assert.Greater(unitsPerPixelY, 0.0001f, $"{LogPrefix} 세로 좌표 변환이 성립하지 않습니다.");

            float stepUpMax = AutoWanderController.ResolveStepUpMaxHeightStatic(bb);
            float riseUnits = stepUpMax + 2f;                          // 손 등반 상한을 확실히 넘는 로프 대역 벽
            float dropUnits = DockGeometry.ReferenceDockDropWorldUnits; // 뛰어내리기 밴드 낙차 — 안전망 격

            // ★ 시험 거리 — 프로덕션의 로프 전용 밴드 폭에서 유도한다(그 60% 지점).
            // 이 유도가 프로덕션과 어긋나도 조용히 통과할 수 없다: 너무 가까우면 아래 "좁은 탐색이
            // 못 찾는다"가, 너무 멀면 "넓은 탐색이 찾는다"가 각각 시끄럽게 실패한다(양방향 가드).
            float bandToleranceWorld = _clonedConfig.parkourDetectionRadius
                * Mathf.Max(1f, _clonedConfig.ropeClimbAdjacentSearchRadiusMultiplier);
            float farDistanceWorld = bandToleranceWorld * 0.6f;
            float farDistanceOsPixels = farDistanceWorld / unitsPerPixelX;

            float dropOsOffset = dropUnits / unitsPerPixelY;
            float riseOsOffset = riseUnits / unitsPerPixelY;
            float lowDropTopOs = groundTopOs + dropOsOffset;   // 아래(OS y 증가)
            float tallWallTopOs = groundTopOs - riseOsOffset;  // 위(OS y 감소)
            Assert.Greater(tallWallTopOs, 0f, $"{LogPrefix} 준비 실패 — 로프벽이 화면 위로 완전히 벗어났습니다.");

            float groundLeftOs = w * 0.4f;
            float groundRightOs = w * 0.6f;

            // 좌우 대칭 — 배회가 어느 쪽을 먼저 고르든 같은 시나리오를 만난다.
            float tallWallLeftRightEdgeOs = groundLeftOs - farDistanceOsPixels;
            float tallWallLeftLeftEdgeOs = tallWallLeftRightEdgeOs - 120f;
            float tallWallRightLeftEdgeOs = groundRightOs + farDistanceOsPixels;
            Assert.Greater(tallWallLeftLeftEdgeOs, 0f,
                $"{LogPrefix} 준비 실패 — 왼쪽 로프벽이 화면 왼쪽 밖으로 나갔습니다(화면 {w:F0}px가 이 밴드 폭을 담기엔 좁습니다).");
            Assert.Less(tallWallRightLeftEdgeOs + 120f, w,
                $"{LogPrefix} 준비 실패 — 오른쪽 로프벽이 화면 오른쪽 밖으로 나갔습니다.");

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(GroundHandle,
                new Rect(groundLeftOs, groundTopOs, groundRightOs - groundLeftOs, h - groundTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(LowDropLeftHandle,
                new Rect(groundLeftOs - 80f, lowDropTopOs, 80f, h - lowDropTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(LowDropRightHandle,
                new Rect(groundRightOs, lowDropTopOs, 80f, h - lowDropTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallLeftHandle,
                new Rect(tallWallLeftLeftEdgeOs, tallWallTopOs, tallWallLeftRightEdgeOs - tallWallLeftLeftEdgeOs, h * 0.5f), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallRightHandle,
                new Rect(tallWallRightLeftEdgeOs, tallWallTopOs, 120f, h * 0.5f), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            float groundCenterWorldX = y0.x;
            float groundTopWorldY = y0.y;
            PlaceBody(bb, groundCenterWorldX, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            // ---- 전제 확인: 프로덕션이 판정하는 그 자리에서, 부재와 존재를 짝으로 ----
            for (int i = 0; i < 2; i++)
            {
                int direction = i == 0 ? -1 : 1;
                string label = direction > 0 ? "오른쪽" : "왼쪽";
                GroundSensor.GroundInfo info = SenseAtEdge(bb, direction, groundTopWorldY, label);
                float ropeMax = AutoWanderController.ResolveRopeClimbMaxHeight(bb, info.GroundWorldY);

                bool narrowFound = bb.TryFindClimbableWall(info, direction, out _, out float narrowTopY);
                bool wideFound = bb.TryFindRopeClimbWallWide(info, direction, out long wideHandle, out float wideTopY, stepUpMax, ropeMax);
                float wideHeight = wideTopY - info.GroundWorldY;
                Debug.Log($"{LogPrefix} [{label}] 탐색 대조 — 좁은={narrowFound}(상단 {narrowTopY:F3}), " +
                    $"넓은={wideFound}(핸들 {wideHandle}, 높이 {wideHeight:F3}유닛), 대역=({stepUpMax:F3}, {ropeMax:F3}].");

                // ★ 존재 단언을 먼저 세운다 — 이것이 아래 부재 단언의 유일한 보증인이다.
                //   (게이트가 다시 막히면 둘 다 false가 되는데, 그때 이 단언이 시끄럽게 먼저 죽는다.)
                Assert.IsTrue(wideFound,
                    $"{LogPrefix} [{label}] 넓은 탐색이 로프 대역 벽을 못 찾았습니다 — 시험 거리 {farDistanceWorld:F3}유닛, " +
                    $"밴드 폭 {bandToleranceWorld:F3}유닛. TryFindRopeClimbWallWide(§10)가 되돌려졌거나 " +
                    "ropeClimbAdjacentSearchRadiusMultiplier가 줄었습니다.");
                Assert.IsFalse(narrowFound,
                    $"{LogPrefix} [{label}] 좁은 탐색이 이미 벽을 찾았습니다 — 이 테스트가 '넓은 탐색 전용'을 " +
                    $"검증하지 못합니다(시험 거리 {farDistanceWorld:F3}유닛이 좁은 탐색폭 안으로 들어왔습니다).");
                Assert.Greater(wideHeight, stepUpMax,
                    $"{LogPrefix} [{label}] 시험벽이 손 등반 대역입니다({wideHeight:F3} ≤ {stepUpMax:F3}) — 로프 대역이 아닙니다.");
                Assert.LessOrEqual(wideHeight, ropeMax,
                    $"{LogPrefix} [{label}] 시험벽이 로프 상한을 넘습니다({wideHeight:F3} > {ropeMax:F3}).");

                // ★★ 네거티브 컨트롤 — 프로덕션의 밴드 폭 노브를 그 자신의 최솟값으로 죽이면
                //    같은 배치에서 넓은 탐색이 실제로 실패해야 한다. 실패하지 않으면 위 IsTrue는
                //    "밴드 덕분"이 아니라 다른 이유로 통과한 것이고, 그 순간 이 스위트는 아무것도 못 재고 있다.
                float shippedMultiplier = _clonedConfig.ropeClimbAdjacentSearchRadiusMultiplier;
                _clonedConfig.ropeClimbAdjacentSearchRadiusMultiplier = 1f;
                bool wideFoundWithCrushedBand = bb.TryFindRopeClimbWallWide(info, direction, out _, out _, stepUpMax, ropeMax);
                _clonedConfig.ropeClimbAdjacentSearchRadiusMultiplier = shippedMultiplier;
                bool wideFoundAfterRestore = bb.TryFindRopeClimbWallWide(info, direction, out _, out _, stepUpMax, ropeMax);
                Debug.Log($"{LogPrefix} [{label}] 네거티브 컨트롤 — 밴드 배수 {shippedMultiplier:F1}→1 로 죽였을 때 " +
                    $"넓은탐색={wideFoundWithCrushedBand}, 원복 뒤={wideFoundAfterRestore}.");
                Assert.IsFalse(wideFoundWithCrushedBand,
                    $"{LogPrefix} [{label}] 네거티브 컨트롤 실패 — 밴드 폭을 최소로 죽였는데도 넓은 탐색이 벽을 찾았습니다. " +
                    "이 배치는 밴드 폭을 재고 있지 않으므로 위 통과는 아무것도 증명하지 못합니다.");
                Assert.IsTrue(wideFoundAfterRestore,
                    $"{LogPrefix} [{label}] 네거티브 컨트롤 원복 실패 — 배수를 되돌렸는데 넓은 탐색이 되살아나지 않았습니다.");

                // ★★ '내려갈 곳이 상시 존재'라는 이 시나리오의 다른 절반.
                bool hopFound = bb.TryFindHopDownTarget(info, direction, out long hopHandle, out float hopTopY);
                Debug.Log($"{LogPrefix} [{label}] 뛰어내리기 후보 — {hopFound}(핸들 {hopHandle}, 낙차 {(info.GroundWorldY - hopTopY):F3}유닛).");
                Assert.IsTrue(hopFound,
                    $"{LogPrefix} [{label}] 전제 실패 — 뛰어내릴 낮은 발판이 감지되지 않았습니다(낙차 {dropUnits:F3}유닛 배치). " +
                    "이 전제가 없으면 '내려갈 곳이 있어도 로프가 먼저 평가된다'를 검증할 수 없습니다.");
            }

            // ---- 자율 배회 관측 ----
            PlaceBody(bb, groundCenterWorldX, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            TightenWanderForDeterminism();
            _clonedConfig.hopDownChance = 1f;
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.stepUpChance = 1f;
            _clonedConfig.ropeClimbChance = 1f;
            // 환경변수(STICKMATE_QA_ROPE_CLIMB_CHANCE)가 켜져 있으면 config 값이 무시된다 —
            // 러너 환경에 좌우되지 않도록 프로덕션의 오버라이드 창구로 명시적으로 못박는다.
            RopeClimbQaOverride.SetChanceTestOverride(1f);

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260907));
            bb.IntentSource = wander;

            var obs = new Observation();
            yield return ObserveRope(wander, bb, MaxObserveSeconds, obs);

            Debug.Log($"{LogPrefix} 실측 결과 — 요청={obs.SawRequest}(t={obs.RequestSeconds:F2}s), " +
                $"진입={obs.SawState}(t={obs.StateSeconds:F2}s), Fall경유={obs.SawFall}, " +
                $"총 {obs.ElapsedSeconds:F2}초, 최종 상태={bb.Machine.CurrentStateId}.");

            Assert.IsTrue(obs.SawRequest || obs.SawState,
                $"{LogPrefix} {MaxObserveSeconds:F0}초 안에 밧줄등반이 요청/진입되지 않았습니다 — 넓은 탐색 폴백이나 " +
                "독립 우선순위가 동작하지 않았거나(§10), '내려갈 곳'이 로프 평가를 여전히 원천봉쇄하고 있다는 뜻입니다.");

            yield return AssertRopeClimbEntersAndHolds(bb, obs);
        }

        // ============================================================================
        // (2) 경계를 가로질러 걸친 창 — 폭을 아무리 넓혀도 좁은 탐색은 원리상 못 찾는다
        // ============================================================================

        /// <summary>
        /// ★ 실기(2026-09-07 3차, 리더 macOS 라이브 세션)에서 실제로 관측된 형상을 그대로 재현한다 —
        /// Finder 창(OS x 976~1512)이 Dock(254~1259)의 오른쪽 경계(1259)를 <b>가로질러 걸쳐</b> 있었다.
        /// 비율로 옮기면 안쪽 침범 283/1005 = 28.2%, 바깥 돌출 253/1005 = 25.2%다.
        ///
        /// <para>이 형상이 중요한 이유: 좁은 탐색(<c>GroundSensor.TryFindClimbableWall</c>)은 후보의
        /// <b>선행 모서리 하나만</b> 보므로, 탐색 폭(<c>searchSlackOverrideWorld</c>)을 아무리 크게 줘도
        /// 이 형상을 <b>원리상</b> 못 잡는다 — 그 인자는 검사 범위를 바깥쪽으로만 넓힌다. 폭만 넓혔던
        /// 1차 시도가 정확히 여기서 실패했다. 이 테스트는 그 <b>기각된 대안</b>과 채택된 겹침 탐색을
        /// 실제로 갈라 세운다(구별력 없는 회귀 테스트는 회귀를 못 막는다).</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 경계를_가로질러_걸친_창은_폭을_아무리_넓혀도_좁은탐색이_못찾고_겹침탐색만_찾는다()
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
            Assert.Greater(unitsPerPixelY, 0.0001f, $"{LogPrefix} 세로 좌표 변환이 성립하지 않습니다.");

            float stepUpMax = AutoWanderController.ResolveStepUpMaxHeightStatic(bb);
            float riseUnits = stepUpMax + 2f;
            float tallWallTopOs = groundTopOs - riseUnits / unitsPerPixelY;
            Assert.Greater(tallWallTopOs, 0f, $"{LogPrefix} 준비 실패 — 로프벽이 화면 위로 완전히 벗어났습니다.");

            // 지면을 넓게(화면 절반) 잡아 실기 Dock의 비율을 그대로 옮긴다.
            float groundLeftOs = w * 0.25f;
            float groundRightOs = w * 0.75f;
            float groundWidthOs = groundRightOs - groundLeftOs;
            const float InsideRatio = 0.282f;  // 실기: (1259-976)/1005
            const float OutsideRatio = 0.252f; // 실기: (1512-1259)/1005

            float rightWallLeftOs = groundRightOs - InsideRatio * groundWidthOs;
            float rightWallRightOs = groundRightOs + OutsideRatio * groundWidthOs;
            float leftWallRightOs = groundLeftOs + InsideRatio * groundWidthOs;
            float leftWallLeftOs = groundLeftOs - OutsideRatio * groundWidthOs;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(GroundHandle,
                new Rect(groundLeftOs, groundTopOs, groundWidthOs, h - groundTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(TallWallLeftHandle,
                new Rect(leftWallLeftOs, tallWallTopOs, leftWallRightOs - leftWallLeftOs, h * 0.5f), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallRightHandle,
                new Rect(rightWallLeftOs, tallWallTopOs, rightWallRightOs - rightWallLeftOs, h * 0.5f), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            float groundCenterWorldX = y0.x;
            float groundTopWorldY = y0.y;
            PlaceBody(bb, groundCenterWorldX, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            for (int i = 0; i < 2; i++)
            {
                int direction = i == 0 ? -1 : 1;
                string label = direction > 0 ? "오른쪽" : "왼쪽";
                GroundSensor.GroundInfo info = SenseAtEdge(bb, direction, groundTopWorldY, label);
                float ropeMax = AutoWanderController.ResolveRopeClimbMaxHeight(bb, info.GroundWorldY);
                IReadOnlyList<PlatformFoothold> footholds = bb.FootholdPoller.CachedFootholds;
                Vector2 foot = bb.Body.position;
                float reach = bb.EdgeProbeReachWorld;

                // (a) 좁은 탐색 — 폭을 "지면 전체 폭"만큼(터무니없이 크게) 줘도 못 찾아야 한다.
                //     이것이 기각된 1차 대안("폭만 넓히기")과 채택안을 가르는 구별 단언이다.
                float absurdSlack = Mathf.Abs(info.CurrentFootholdRightWorldX - info.CurrentFootholdLeftWorldX);
                bool narrowWithAbsurdSlack = GroundSensor.TryFindClimbableWall(cam, foot, info, direction,
                    footholds, _clonedConfig, out _, out _, reach, absurdSlack);

                // (b) 겹침 탐색 — 같은 순간, 같은 자리, 같은 도달거리에서 찾아야 한다(존재 단언 짝).
                float bandTolerance = _clonedConfig.parkourDetectionRadius
                    * Mathf.Max(1f, _clonedConfig.ropeClimbAdjacentSearchRadiusMultiplier);
                bool overlapFound = GroundSensor.TryFindClimbableWallOverlapping(cam, foot, info, direction,
                    footholds, _clonedConfig, out PlatformFoothold overlapWall, out float overlapTopY, reach, bandTolerance);

                // (c) 프로덕션 소비 경로(트리거/재확인이 실제로 부르는 그 함수).
                bool wideFound = bb.TryFindRopeClimbWallWide(info, direction, out long wideHandle, out float wideTopY, stepUpMax, ropeMax);

                Debug.Log($"{LogPrefix} [{label}] 걸친창 대조 — 좁은(폭 {absurdSlack:F2}유닛 강제)={narrowWithAbsurdSlack}, " +
                    $"겹침={overlapFound}(핸들 {overlapWall.Handle}, 상단 {overlapTopY:F3}), " +
                    $"소비경로={wideFound}(핸들 {wideHandle}, 높이 {(wideTopY - info.GroundWorldY):F3}유닛).");

                Assert.IsTrue(overlapFound,
                    $"{LogPrefix} [{label}] 겹침 탐색이 경계에 걸친 창을 못 찾았습니다 — " +
                    "TryFindClimbableWallOverlapping(§10)이 되돌려졌거나 밴드 폭이 줄었습니다.");
                Assert.IsFalse(narrowWithAbsurdSlack,
                    $"{LogPrefix} [{label}] 좁은 탐색이 폭 {absurdSlack:F2}유닛에서 이 창을 찾았습니다 — " +
                    "그렇다면 이 배치는 '걸친 형상'이 아니고, 이 테스트는 기각된 1차 대안(폭만 넓히기)과 " +
                    "채택안을 구별하지 못합니다(배치를 다시 보십시오).");
                Assert.IsTrue(wideFound,
                    $"{LogPrefix} [{label}] 소비 경로(TryFindRopeClimbWallWide)가 이 창을 로프 대역으로 채택하지 않았습니다.");
                Assert.AreEqual(direction > 0 ? TallWallRightHandle : TallWallLeftHandle, wideHandle,
                    $"{LogPrefix} [{label}] 진행 방향과 반대편 벽이 채택됐습니다(핸들 {wideHandle}) — " +
                    "겹침 밴드가 방향을 무시하고 뒤쪽 창을 잡으면 밧줄을 등지고 던지는 그림이 됩니다.");
            }

            // ---- 자율 배회 관측 — 이 형상에서 로프가 발동하면 그 경로는 겹침 탐색뿐이다 ----
            PlaceBody(bb, groundCenterWorldX, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            TightenWanderForDeterminism();
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.stepUpChance = 0f;
            _clonedConfig.ropeClimbChance = 1f;
            RopeClimbQaOverride.SetChanceTestOverride(1f);

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260907));
            bb.IntentSource = wander;

            var obs = new Observation();
            yield return ObserveRope(wander, bb, MaxObserveSeconds, obs);

            Debug.Log($"{LogPrefix} 걸친창 실측 결과 — 요청={obs.SawRequest}(t={obs.RequestSeconds:F2}s), " +
                $"진입={obs.SawState}, 총 {obs.ElapsedSeconds:F2}초, 최종 상태={bb.Machine.CurrentStateId}.");

            Assert.IsTrue(obs.SawRequest || obs.SawState,
                $"{LogPrefix} 실기에서 관측된 '경계에 걸친 창' 형상에서 {MaxObserveSeconds:F0}초 안에 밧줄등반이 " +
                "발동하지 않았습니다 — 이 형상을 찾을 수 있는 경로는 겹침 탐색뿐이므로(위 대조가 좁은 탐색의 " +
                "구조적 불가를 이미 보였습니다), §10의 겹침 탐색이 소비 경로에서 끊겼다는 뜻입니다.");

            yield return AssertRopeClimbEntersAndHolds(bb, obs);
        }

        // ============================================================================
        // (3) stepUpChance가 0이어도 로프등반은 독립적으로 평가된다
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
            float riseUnits = stepUpMax + 2f; // 로프 대역(손 등반 상한 초과) — 이번엔 기존 좁은 탐색 안에 둔다.
            float tallWallTopOs = groundTopOs - riseUnits / unitsPerPixelY;
            Assert.Greater(tallWallTopOs, 0f, $"{LogPrefix} 준비 실패 — 로프벽이 화면 위로 완전히 벗어났습니다.");

            // 좌우 대칭 — 벽을 지면 경계에 바로 붙여(좁은 탐색 안) 이 테스트가 순수하게
            // "stepUpChance 독립성"만 보게 한다(탐색 폭/형상 문제는 위 두 테스트가 이미 잠갔다).
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(GroundHandle,
                new Rect(w * 0.4f, groundTopOs, w * 0.2f, h - groundTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(TallWallLeftHandle,
                new Rect(w * 0.2f, tallWallTopOs, w * 0.2f, h * 0.5f), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallRightHandle,
                new Rect(w * 0.6f, tallWallTopOs, w * 0.2f, h * 0.5f), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            float groundCenterWorldX = y0.x;
            float groundTopWorldY = y0.y;
            PlaceBody(bb, groundCenterWorldX, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            for (int i = 0; i < 2; i++)
            {
                int direction = i == 0 ? -1 : 1;
                string label = direction > 0 ? "오른쪽" : "왼쪽";
                GroundSensor.GroundInfo info = SenseAtEdge(bb, direction, groundTopWorldY, label);
                float ropeMax = AutoWanderController.ResolveRopeClimbMaxHeight(bb, info.GroundWorldY);

                bool narrowFound = bb.TryFindClimbableWall(info, direction, out long narrowHandle, out float narrowTopY);
                float narrowHeight = narrowTopY - info.GroundWorldY;
                Debug.Log($"{LogPrefix} [{label}] 좁은 탐색 — {narrowFound}(핸들 {narrowHandle}, 높이 {narrowHeight:F3}유닛), " +
                    $"대역=({stepUpMax:F3}, {ropeMax:F3}].");
                Assert.IsTrue(narrowFound,
                    $"{LogPrefix} [{label}] 전제 실패 — 경계에 붙여 둔 벽을 좁은 탐색으로도 못 찾습니다(배치 오류).");
                Assert.Greater(narrowHeight, stepUpMax,
                    $"{LogPrefix} [{label}] 전제 실패 — 시험벽이 손 등반 대역입니다({narrowHeight:F3} ≤ {stepUpMax:F3}). " +
                    "그러면 stepUpChance=0이 로프가 아니라 되올라가기를 막는 것이 되어 이 테스트가 겨냥을 잃습니다.");
                Assert.LessOrEqual(narrowHeight, ropeMax,
                    $"{LogPrefix} [{label}] 전제 실패 — 시험벽이 로프 상한을 넘습니다({narrowHeight:F3} > {ropeMax:F3}).");
            }

            // ---- 자율 배회 관측(양성) ----
            PlaceBody(bb, groundCenterWorldX, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            TightenWanderForDeterminism();
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.ledgeHangChance = 0f;
            // ★★ 핵심 — 되올라가기 갈래를 완전히 죽인다. 재설계 이전 코드라면 로프 추첨 자체가
            // "stepUpChance 통과"라는 외곽 게이트에 막혀 이 값이 0인 한 로프등반도 함께 죽었다.
            _clonedConfig.stepUpChance = 0f;
            _clonedConfig.ropeClimbChance = 1f;
            RopeClimbQaOverride.SetChanceTestOverride(1f);

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260907));
            bb.IntentSource = wander;

            var obs = new Observation();
            yield return ObserveRope(wander, bb, MaxObserveSeconds, obs);

            Debug.Log($"{LogPrefix} 실측 결과(stepUpChance=0) — 요청={obs.SawRequest}(t={obs.RequestSeconds:F2}s), " +
                $"진입={obs.SawState}, 총 {obs.ElapsedSeconds:F2}초, 최종 상태={bb.Machine.CurrentStateId}.");

            Assert.IsTrue(obs.SawRequest || obs.SawState,
                $"{LogPrefix} stepUpChance=0인데도 {MaxObserveSeconds:F0}초 안에 밧줄등반이 발동하지 않았습니다 — " +
                "로프 추첨이 여전히 stepUpChance 게이트에 묶여 있다는 뜻입니다(근본재설계 §10이 되돌려졌을 수 있음).");

            float positiveSeconds = obs.RequestSeconds > 0f ? obs.RequestSeconds : obs.ElapsedSeconds;
            yield return AssertRopeClimbEntersAndHolds(bb, obs);

            // ============================================================================
            // ★★ 네거티브 컨트롤 — 위 초록이 "로프 평가 때문"임을 같은 실행 안에서 증명한다
            // ============================================================================
            // 프로덕션의 로프 확률 창구를 0으로 죽인다. 다른 조건은 전부 그대로다. 그런데도 요청/진입이
            // 나온다면 위 단언은 로프 평가가 아니라 다른 경로가 만든 것이고, 이 스위트는 아무것도 재고
            // 있지 않다. 예산은 양성이 실제로 걸린 시간에서 유도한다(고정 프레임 수 금지 — 벽시계).
            float negativeBudget = Mathf.Max(3f, positiveSeconds * 3f);
            RopeClimbQaOverride.SetChanceTestOverride(0f);
            PlaceBody(bb, groundCenterWorldX, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            var negativeWander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260908));
            bb.IntentSource = negativeWander;

            var negativeObs = new Observation();
            yield return ObserveRope(negativeWander, bb, negativeBudget, negativeObs);

            Debug.Log($"{LogPrefix} 네거티브 컨트롤(로프 확률 0) — 요청={negativeObs.SawRequest}, 진입={negativeObs.SawState}, " +
                $"예산 {negativeBudget:F2}초(양성 {positiveSeconds:F2}초의 3배), 실제 {negativeObs.ElapsedSeconds:F2}초, " +
                $"최종 상태={bb.Machine.CurrentStateId}.");
            Assert.IsFalse(negativeObs.SawRequest || negativeObs.SawState,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 로프 확률을 0으로 죽였는데도 " +
                $"{negativeObs.ElapsedSeconds:F2}초 만에 밧줄등반이 요청/진입됐습니다. " +
                "그렇다면 위 양성 관측은 로프 추첨이 만든 것이 아니며 이 테스트는 대상을 겨누고 있지 않습니다.");
        }

        // ============================================================================
        // (4) 방향 분별 + 동률 파훼 — 「양쪽 창이 모두 밴드에 드는」 배치 (2026-09-07 debugger 수정 잠금)
        // ============================================================================

        /// <summary>겹침 탐색이 쓰는 밴드 폭. <c>StickmanBlackboard.TryFindRopeClimbWallWide</c>가
        /// 넘기는 것과 같은 유도식이다(숫자를 베끼지 않는다 — 두 노브를 그대로 읽는다).</summary>
        private float BandToleranceWorld() =>
            _clonedConfig.parkourDetectionRadius * Mathf.Max(1f, _clonedConfig.ropeClimbAdjacentSearchRadiusMultiplier);

        /// <summary>발판 목록을 통째로 갈아 끼운다. 새 폴러를 만드는 이유는 생성자가 즉시 1회 폴링하기
        /// 때문이다 — 폴링 주기를 기다리지 않아도 되고, 목록 <b>순서</b>만 바꾼 재배선도 확실히 반영된다.</summary>
        private void RebuildFootholds(StickmanBlackboard bb, params PlatformFoothold[] items)
        {
            _service = new TestFootholdService();
            _service.Footholds.AddRange(items);
            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;
        }

        /// <summary>월드 x 구간으로 벽 발판 하나를 만든다(가로 배치를 월드 단위로 논증하고, OS 변환은
        /// 실제 변환 함수에 맡긴다 — 테스트가 좌표 변환식을 새로 만들지 않는 이 저장소 관례).</summary>
        private PlatformFoothold MakeWallByWorldSpan(Camera cam, long handle, float worldXa, float worldXb,
            float topOsY, float screenW, float screenH)
        {
            Vector2 a = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(worldXa, 0f), _clonedConfig, out _);
            Vector2 b = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(worldXb, 0f), _clonedConfig, out _);
            float x0 = Mathf.Min(a.x, b.x);
            float x1 = Mathf.Max(a.x, b.x);
            Assert.GreaterOrEqual(x0, 0f, $"{LogPrefix} 준비 실패 — 벽(핸들 {handle})이 화면 왼쪽 밖입니다(OS x={x0:F1}).");
            Assert.LessOrEqual(x1, screenW, $"{LogPrefix} 준비 실패 — 벽(핸들 {handle})이 화면 오른쪽 밖입니다(OS x={x1:F1}).");
            return new PlatformFoothold(handle, new Rect(x0, topOsY, x1 - x0, screenH * 0.5f), false);
        }

        /// <summary>지면 한복판에 세운 뒤 진행방향 경계 앞(프로덕션 판정 자리)으로 옮겨 접지 정보를 얻는다.</summary>
        private GroundSensor.GroundInfo PrepareEdgeQuery(StickmanBlackboard bb, int direction,
            float centerWorldX, float groundTopWorldY, string label)
        {
            PlaceBody(bb, centerWorldX, groundTopWorldY, GroundHandle);
            return SenseAtEdge(bb, direction, groundTopWorldY, label);
        }

        private bool QueryOverlap(StickmanBlackboard bb, GroundSensor.GroundInfo info, int direction,
            out long handle, out float topWorldY)
        {
            bool found = GroundSensor.TryFindClimbableWallOverlapping(bb.MainCamera, bb.Body.position, info, direction,
                bb.FootholdPoller.CachedFootholds, _clonedConfig, out PlatformFoothold wall, out topWorldY,
                bb.EdgeProbeReachWorld, BandToleranceWorld());
            handle = found ? wall.Handle : 0L;
            return found;
        }

        /// <summary>
        /// ★★ <b>되돌린 형태의 복제</b> — 2026-09-07 debugger 수정 <b>이전</b>의 규칙 두 줄
        /// (① 밴드가 <c>[edgeX − tol, edgeX + tol]</c> <b>대칭</b>, ② 동률 파훼가 <c>&gt;</c> 강부등호
        /// 하나뿐이라 <b>목록 순서</b>가 이김)을 그대로 옮겨 놓은 것이다.
        ///
        /// <para><b>기대값을 만들지 않는다</b> — 아래 단언의 기대값은 이 함수가 아니라 <b>테스트가 직접
        /// 놓은 앞쪽 벽의 핸들</b>이다(CLAUDE.md "기대값을 프로덕션 함수로 만들지 마라"의 취지 그대로).
        /// 이 복제는 오직 <b>"이 배치가 그 회귀를 실제로 가르는가"</b>를 같은 실행 안에서 보이기 위한
        /// 것이며, 언젠가 이 복제가 프로덕션과 <b>같은 답</b>을 내기 시작하면 그때는 배치가 구별력을
        /// 잃은 것이므로 아래 <c>AreNotEqual</c>이 시끄럽게 죽는다 — 조용히 초록이 되는 경로가 없다.</para>
        /// </summary>
        private long PickWallByRevertedSymmetricRule(StickmanBlackboard bb, GroundSensor.GroundInfo info,
            int direction, float bandTolerance)
        {
            Camera cam = bb.MainCamera;
            IReadOnlyList<PlatformFoothold> footholds = bb.FootholdPoller.CachedFootholds;
            Vector2 foot = bb.Body.position;
            _ = ScreenCoordinateConverter.WorldToOsScreen(cam, foot, _clonedConfig, out float depth);

            float detectionRadius = _clonedConfig.parkourDetectionRadius;
            float edgeX = direction > 0 ? info.CurrentFootholdRightWorldX : info.CurrentFootholdLeftWorldX;
            float bandMin = edgeX - bandTolerance; // ← 되돌린 형태: 뒤쪽으로도 tol만큼 열려 있었다
            float bandMax = edgeX + bandTolerance;

            float bestTopY = float.NegativeInfinity;
            long picked = 0L;
            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold fh = footholds[i];
                Rect r = fh.ScreenRect;
                Vector3 topLeft = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(r.x, r.y), depth, _clonedConfig);
                Vector3 topRight = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(r.x + r.width, r.y), depth, _clonedConfig);
                float candMin = Mathf.Min(topLeft.x, topRight.x);
                float candMax = Mathf.Max(topLeft.x, topRight.x);
                if (!(candMax >= bandMin && candMin <= bandMax)) continue;
                if (topLeft.y - info.GroundWorldY < detectionRadius) continue;
                if (topLeft.y > bestTopY) // ← 되돌린 형태: 동률이면 목록 앞쪽(= z-order)이 이겼다
                {
                    bestTopY = topLeft.y;
                    picked = fh.Handle;
                }
            }
            return picked;
        }

        /// <summary>
        /// ★★★ 2026-09-07 debugger 수정(소견 1 방향 무시 · 소견 2 z-order 의존) 회귀 잠금.
        ///
        /// <para><b>왜 새로 필요한가</b>: 바로 위 테스트의 방향 단언은 <b>좌우에 후보가 각각 하나뿐</b>이라
        /// 자명하게 통과하고 있었다 — 반대편 창이 애초에 밴드에 들지 않아, "방향을 본다"가 아니라
        /// "후보가 하나다"를 재고 있었다. 소견 1을 실제로 겨누려면 <b>앞뒤 두 창이 모두 밴드에 드는</b>
        /// 배치라야 한다. 이 테스트가 그 배치를 세운다.</para>
        ///
        /// <para>이 스위트의 다른 셋과 달리 <b>자율 배회를 돌리지 않는다</b> — 여기서 잠그는 것은
        /// "언제 발동하는가"가 아니라 <b>"같은 순간에 어느 벽을 고르는가"</b>라는 순수 기하 판정이고,
        /// 그건 결정론적으로 재는 편이 훨씬 강하다(20초 예산 3개가 붙지 않는다).</para>
        ///
        /// <para>단계: ① 앞뒤 동시 배치에서 앞쪽 채택(+ 되돌린 규칙이면 뒤쪽이 이긴다는 대조),
        /// ② 양성 대조 — 앞쪽만 두면 잡히고 뒤쪽만 두면 못 잡는다(밴드 자체는 살아 있다),
        /// ③ 동률 파훼 «상단 → 진행방향 근접» — 목록 순서를 뒤집어도 같은 답,
        /// ④ 동률 파훼 «→ 핸들» — 사각형까지 완전히 같으면 더 작은 핸들, 역시 순서 무관.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 앞뒤_두_창이_모두_밴드에_들어도_진행방향_창만_채택되고_목록순서와_무관하다()
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
            Assert.Greater(unitsPerPixelY, 0.0001f, $"{LogPrefix} 세로 좌표 변환이 성립하지 않습니다.");

            float stepUpMax = AutoWanderController.ResolveStepUpMaxHeightStatic(bb);
            float riseUnits = stepUpMax + 2f;
            float tallWallTopOs = groundTopOs - riseUnits / unitsPerPixelY;
            Assert.Greater(tallWallTopOs, 0f, $"{LogPrefix} 준비 실패 — 시험벽이 화면 위로 완전히 벗어났습니다.");

            float groundLeftOs = w * 0.4f;
            float groundRightOs = w * 0.6f;
            var groundFoothold = new PlatformFoothold(GroundHandle,
                new Rect(groundLeftOs, groundTopOs, groundRightOs - groundLeftOs, h - groundTopOs), true);
            float groundLeftWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(groundLeftOs, groundTopOs), 10f, _clonedConfig).x;
            float groundRightWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(groundRightOs, groundTopOs), 10f, _clonedConfig).x;
            float centerWorldX = y0.x;
            float groundTopWorldY = y0.y;

            float tol = BandToleranceWorld();
            Debug.Log($"{LogPrefix} 밴드 폭={tol:F3}유닛(감지반경 {_clonedConfig.parkourDetectionRadius:F2} × " +
                $"배수 {_clonedConfig.ropeClimbAdjacentSearchRadiusMultiplier:F1}), 지면 월드 x=[{groundLeftWorldX:F2}, {groundRightWorldX:F2}], " +
                $"시험벽 상승={riseUnits:F3}유닛.");

            for (int i = 0; i < 2; i++)
            {
                int d = i == 0 ? -1 : 1;
                string label = d > 0 ? "오른쪽" : "왼쪽";
                float edgeX = d > 0 ? groundRightWorldX : groundLeftWorldX;

                // 앞쪽 창 — 경계 바깥, 좁은 탐색폭 밖, 밴드 안.
                float forwardA = edgeX + d * (tol * 0.375f);
                float forwardB = edgeX + d * (tol * 0.750f);
                // 뒤쪽 창 — 새 규칙(내 앞에 걸친 부분이 하나도 없다)에서는 탈락해야 하고,
                // 되돌린 대칭 밴드에서는 후보였어야 한다.
                float rearA = edgeX - d * (tol * 0.525f);
                float rearB = edgeX - d * (tol * 0.775f);

                PlatformFoothold forwardWall = MakeWallByWorldSpan(cam, ForwardWallHandle, forwardA, forwardB, tallWallTopOs, w, h);
                PlatformFoothold rearWall = MakeWallByWorldSpan(cam, RearWallHandle, rearA, rearB, tallWallTopOs, w, h);

                // ---------- ① 앞뒤 동시 — 뒤쪽 창을 목록 **앞**에 둔다(되돌린 규칙이라면 그쪽이 이긴다) ----------
                RebuildFootholds(bb, groundFoothold, rearWall, forwardWall);
                GroundSensor.GroundInfo info = PrepareEdgeQuery(bb, d, centerWorldX, groundTopWorldY, label);
                float ropeMax = AutoWanderController.ResolveRopeClimbMaxHeight(bb, info.GroundWorldY);
                float footX = bb.Body.position.x;

                // 배치 전제를 값으로 못박는다 — 이 셋이 깨지면 아래 단언들은 아무것도 재지 못한다.
                float rearNearEdge = d > 0 ? Mathf.Max(rearA, rearB) : Mathf.Min(rearA, rearB);
                Assert.Less((rearNearEdge - footX) * d, 0f,
                    $"{LogPrefix} [{label}] 전제 실패 — 뒤쪽 창이 몸({footX:F3})보다 앞에 있습니다(끝 {rearNearEdge:F3}). " +
                    "그러면 새 규칙에서도 정당한 후보라 방향 분별을 검증하지 못합니다.");
                Assert.GreaterOrEqual((rearNearEdge - (edgeX - d * tol)) * d, 0f,
                    $"{LogPrefix} [{label}] 전제 실패 — 뒤쪽 창이 되돌린 대칭 밴드 밖입니다. " +
                    "그러면 옛 규칙에서도 후보가 아니었다는 뜻이라 이 테스트는 그 회귀를 가르지 못합니다.");
                Assert.IsFalse(bb.TryFindClimbableWall(info, d, out _, out _),
                    $"{LogPrefix} [{label}] 전제 실패 — 좁은 탐색이 앞쪽 창을 이미 찾았습니다. " +
                    "그러면 소비 경로가 겹침 탐색에 도달하지 않아 이 잠금이 무의미해집니다.");

                bool found = QueryOverlap(bb, info, d, out long pickedHandle, out float pickedTopY);
                long revertedPick = PickWallByRevertedSymmetricRule(bb, info, d, tol);
                Debug.Log($"{LogPrefix} [{label}] ① 앞뒤 동시 — 채택={found}(핸들 {pickedHandle}), " +
                    $"되돌린 규칙이었다면 핸들 {revertedPick}. 몸 x={footX:F3}, 경계 x={edgeX:F3}, " +
                    $"앞쪽=[{Mathf.Min(forwardA, forwardB):F2},{Mathf.Max(forwardA, forwardB):F2}], " +
                    $"뒤쪽=[{Mathf.Min(rearA, rearB):F2},{Mathf.Max(rearA, rearB):F2}].");

                Assert.IsTrue(found, $"{LogPrefix} [{label}] ① 겹침 탐색이 앞쪽 창조차 못 찾았습니다.");
                Assert.AreEqual(ForwardWallHandle, pickedHandle,
                    $"{LogPrefix} [{label}] ① 진행 방향 반대편(등 뒤) 창이 채택됐습니다(핸들 {pickedHandle}). " +
                    "밴드가 다시 대칭으로 돌아갔다는 뜻입니다 — 그러면 앞으로 밧줄을 던지는데 목표는 등 뒤가 됩니다.");

                // ★ 네거티브 컨트롤 — 되돌린 규칙은 이 배치에서 **다른 답**을 내야 한다.
                //   같은 답이 나오면 이 배치는 구별력이 없고, 위 단언은 회귀를 못 잡는다.
                Assert.AreEqual(RearWallHandle, revertedPick,
                    $"{LogPrefix} [{label}] 네거티브 컨트롤 실패 — 되돌린 대칭 밴드 규칙조차 뒤쪽 창을 고르지 않았습니다" +
                    $"(핸들 {revertedPick}). 이 배치는 debugger 수정 전/후를 가르지 못하므로 위 통과는 아무것도 증명하지 못합니다.");
                Assert.AreNotEqual(revertedPick, pickedHandle,
                    $"{LogPrefix} [{label}] 네거티브 컨트롤 실패 — 지금 규칙과 되돌린 규칙이 같은 답({pickedHandle})을 냈습니다.");

                // 소비 경로(트리거/재확인이 실제로 부르는 함수)도 같은 벽을 골라야 한다.
                Assert.IsTrue(bb.TryFindRopeClimbWallWide(info, d, out long consumedHandle, out _, stepUpMax, ropeMax),
                    $"{LogPrefix} [{label}] ① 소비 경로가 로프 대역 벽을 못 찾았습니다.");
                Assert.AreEqual(ForwardWallHandle, consumedHandle,
                    $"{LogPrefix} [{label}] ① 소비 경로가 등 뒤 창을 채택했습니다(핸들 {consumedHandle}).");

                // ---------- ② 양성 대조 — 앞쪽만: 잡힌다 / 뒤쪽만: 못 잡는다 ----------
                RebuildFootholds(bb, groundFoothold, forwardWall);
                GroundSensor.GroundInfo infoFwdOnly = PrepareEdgeQuery(bb, d, centerWorldX, groundTopWorldY, label);
                bool foundForwardOnly = QueryOverlap(bb, infoFwdOnly, d, out long fwdOnlyHandle, out _);

                RebuildFootholds(bb, groundFoothold, rearWall);
                GroundSensor.GroundInfo infoRearOnly = PrepareEdgeQuery(bb, d, centerWorldX, groundTopWorldY, label);
                bool foundRearOnly = QueryOverlap(bb, infoRearOnly, d, out long rearOnlyHandle, out _);

                Debug.Log($"{LogPrefix} [{label}] ② 양성/음성 대조 — 앞쪽만={foundForwardOnly}(핸들 {fwdOnlyHandle}), " +
                    $"뒤쪽만={foundRearOnly}(핸들 {rearOnlyHandle}).");
                Assert.IsTrue(foundForwardOnly,
                    $"{LogPrefix} [{label}] ② 양성 대조 실패 — 앞쪽 창 하나만 둬도 못 찾습니다. 밴드 자체가 죽었다는 뜻이라 " +
                    "①의 '뒤쪽이 안 뽑혔다'는 결과도 방향 분별이 아니라 전면 불능의 결과일 수 있습니다.");
                Assert.AreEqual(ForwardWallHandle, fwdOnlyHandle, $"{LogPrefix} [{label}] ② 앞쪽 단독 조회가 다른 핸들을 냈습니다.");
                Assert.IsFalse(foundRearOnly,
                    $"{LogPrefix} [{label}] ② 뒤쪽 창 하나만 뒀는데 채택됐습니다(핸들 {rearOnlyHandle}) — " +
                    "①의 통과는 '뒤쪽이 배제된 것'이 아니라 '앞쪽에 밀린 것'뿐이었다는 뜻입니다.");

                // ---------- ③ 동률 파훼 «상단 동률 → 진행방향 근접» + 목록 순서 무관 ----------
                float nearA = edgeX + d * (tol * 0.375f);
                float nearB = edgeX + d * (tol * 0.500f);
                float farA = edgeX + d * (tol * 0.750f);
                float farB = edgeX + d * (tol * 0.875f);
                PlatformFoothold nearWall = MakeWallByWorldSpan(cam, NearWallHandle, nearA, nearB, tallWallTopOs, w, h);
                // ★ 먼 쪽이 **더 작은 핸들**(FarWallHandle < NearWallHandle)이다 — 의도적이다.
                //   파훼 3키 중 ②「진행방향 근접」과 ③「작은 핸들」이 **서로 반대 답**을 가리키게 만들어,
                //   ③으로 흘러내리면(=②가 사라지면) 답이 먼 쪽으로 뒤집히게 한다. 가까운 쪽에 작은
                //   핸들을 주면 두 키가 같은 답을 내서 무엇이 이겼는지 구별할 수 없다.
                PlatformFoothold farWall = MakeWallByWorldSpan(cam, FarWallHandle, farA, farB, tallWallTopOs, w, h);
                Assert.Less(FarWallHandle, NearWallHandle,
                    $"{LogPrefix} [{label}] ③ 전제 실패 — 먼 쪽 핸들이 가까운 쪽보다 작아야 파훼 키 ②와 ③이 " +
                    "서로 반대를 가리켜 구별력이 생깁니다(핸들 상수를 바꿨다면 이 배치의 뜻이 바뀝니다).");

                RebuildFootholds(bb, groundFoothold, farWall, nearWall);
                GroundSensor.GroundInfo infoOrderA = PrepareEdgeQuery(bb, d, centerWorldX, groundTopWorldY, label);
                bool foundOrderA = QueryOverlap(bb, infoOrderA, d, out long orderAHandle, out float orderATopY);

                RebuildFootholds(bb, groundFoothold, nearWall, farWall);
                GroundSensor.GroundInfo infoOrderB = PrepareEdgeQuery(bb, d, centerWorldX, groundTopWorldY, label);
                bool foundOrderB = QueryOverlap(bb, infoOrderB, d, out long orderBHandle, out float orderBTopY);

                Debug.Log($"{LogPrefix} [{label}] ③ 동률(상단 {orderATopY:F4} vs {orderBTopY:F4}) 파훼 — " +
                    $"목록순서A(먼 쪽 먼저)={orderAHandle}, 목록순서B(가까운 쪽 먼저)={orderBHandle}.");
                Assert.IsTrue(foundOrderA && foundOrderB, $"{LogPrefix} [{label}] ③ 동률 배치에서 아무것도 못 찾았습니다.");
                Assert.AreEqual(orderATopY, orderBTopY,
                    $"{LogPrefix} [{label}] ③ 전제 실패 — 두 창의 상단이 동률이 아닙니다({orderATopY:F6} vs {orderBTopY:F6}). " +
                    "같은 OS y에서 만들었으므로 이 값은 비트 단위로 같아야 합니다.");
                Assert.AreEqual(NearWallHandle, orderAHandle,
                    $"{LogPrefix} [{label}] ③ 상단이 같을 때 진행 방향으로 더 가까운 창이 이겨야 합니다 — " +
                    $"채택 핸들이 {orderAHandle}입니다. 먼 쪽 핸들({FarWallHandle})이 더 작게 배치돼 있으므로, " +
                    "이 값이 나왔다면 파훼가 「근접」 키를 건너뛰고 「핸들」 키로 흘러내렸다는 뜻입니다.");
                Assert.AreEqual(orderAHandle, orderBHandle,
                    $"{LogPrefix} [{label}] ③ 목록 순서만 바꿨는데 답이 달라졌습니다({orderAHandle} → {orderBHandle}) — " +
                    "발판 목록 순서는 OS 창 열거 순서(z-order)라, 사용자가 창을 클릭하는 것만으로 목표가 바뀐다는 뜻입니다.");

                // ---------- ④ 동률 파훼 «→ 핸들» — 사각형까지 완전히 같은 두 창 ----------
                PlatformFoothold twinLower = MakeWallByWorldSpan(cam, TwinLowerHandle, nearA, nearB, tallWallTopOs, w, h);
                PlatformFoothold twinHigher = MakeWallByWorldSpan(cam, TwinHigherHandle, nearA, nearB, tallWallTopOs, w, h);

                RebuildFootholds(bb, groundFoothold, twinHigher, twinLower);
                GroundSensor.GroundInfo infoTwinA = PrepareEdgeQuery(bb, d, centerWorldX, groundTopWorldY, label);
                bool foundTwinA = QueryOverlap(bb, infoTwinA, d, out long twinAHandle, out _);

                RebuildFootholds(bb, groundFoothold, twinLower, twinHigher);
                GroundSensor.GroundInfo infoTwinB = PrepareEdgeQuery(bb, d, centerWorldX, groundTopWorldY, label);
                bool foundTwinB = QueryOverlap(bb, infoTwinB, d, out long twinBHandle, out _);

                Debug.Log($"{LogPrefix} [{label}] ④ 완전 동률(같은 사각형) 파훼 — 순서A(큰 핸들 먼저)={twinAHandle}, " +
                    $"순서B(작은 핸들 먼저)={twinBHandle}.");
                Assert.IsTrue(foundTwinA && foundTwinB, $"{LogPrefix} [{label}] ④ 완전 동률 배치에서 아무것도 못 찾았습니다.");
                Assert.AreEqual(TwinLowerHandle, twinAHandle,
                    $"{LogPrefix} [{label}] ④ 사각형까지 같을 때는 더 작은 핸들이 이겨야 합니다(핸들 {twinAHandle}). " +
                    "핸들은 OS가 준 식별자라 열거 순서와 독립이고, 그것이 이 판정을 결정론으로 만드는 마지막 키입니다.");
                Assert.AreEqual(twinAHandle, twinBHandle,
                    $"{LogPrefix} [{label}] ④ 목록 순서만 바꿨는데 답이 달라졌습니다({twinAHandle} → {twinBHandle}).");
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ★★★ 2026-09-08 — 화면 전폭 발판(= Windows 작업표시줄)에서도 밧줄만 통과한다
        // ════════════════════════════════════════════════════════════════════════════
        //
        // 무엇이 터졌었나 — 사용자가 "윈도우에서는 밧줄 동작안함"을 <b>두 번</b> 신고했고, 리더는
        // 두 번 다 확률(0.20→0.85)과 높이 상한(6.6H→화면비례)만 고쳐 내보냈다. 둘 다 효과가 없었다.
        // 진짜 원인은 수치가 아니라 <b>구조</b>였다:
        //
        //   AutoWanderController: if (!_edgeActionRolledThisLeg && !isTrueScreenEdge) → 추첨
        //
        // macOS Dock은 화면 <b>가운데</b> 띠라 좌우에 안전망 조각이 남고 내부 경계가 2개 생긴다.
        // Windows 작업표시줄은 <b>화면 전폭</b>이라 그 조각이 폭 0으로 죽고, 발판의 좌우 끝이
        // 곧 화면 끝이다 → isTrueScreenEdge가 <b>항상 true</b> → 추첨이 한 번도 돌지 않았다.
        //
        // 이 테스트가 잠그는 계약 두 개(같은 지형에서 동시에 잰다):
        //   (1) <b>양성</b> — 전폭 발판의 화면 끝에서도 <b>밧줄은</b> 평가되고 발동한다.
        //   (2) <b>음성</b> — 같은 자리에서 <b>뛰어내리기는 여전히 막힌다</b>. 화면 끝에서 내려가면
        //       몸이 화면 밖으로 나가기 때문이고, 그 근거는 밧줄에는 성립하지 않는다(위로 오른다).
        //
        // ★ (2)가 없으면 이 수정은 "게이트를 통째로 열어 버린 것"과 초록이 구분되지 않는다 —
        //   이 저장소가 반복해서 당한 «성공한 측정과 똑같이 생긴 실패한 측정»이다.
        [UnityTest]
        public IEnumerator 화면_전폭_발판에서도_밧줄은_평가되고_뛰어내리기는_여전히_막힌다()
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
            float riseUnits = stepUpMax + 2f;
            float tallWallTopOs = groundTopOs - riseUnits / unitsPerPixelY;
            Assert.Greater(tallWallTopOs, 0f, $"{LogPrefix} 준비 실패 — 로프벽이 화면 위로 벗어났습니다.");

            // ★ 지면은 <b>화면 전폭</b>이다 — 이것이 이 테스트의 전부다(Windows 작업표시줄 재현).
            //   벽은 그 위에 겹쳐 세운다(전폭 지면에는 «옆칸»이 없으므로 겹침 탐색이 유일한 경로다).
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(GroundHandle,
                new Rect(0f, groundTopOs, w, h - groundTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(TallWallLeftHandle,
                new Rect(0f, tallWallTopOs, w * 0.25f, h * 0.5f), false));
            _service.Footholds.Add(new PlatformFoothold(TallWallRightHandle,
                new Rect(w * 0.75f, tallWallTopOs, w * 0.25f, h * 0.5f), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            float groundCenterWorldX = y0.x;
            float groundTopWorldY = y0.y;

            // ---- 전제 검증 — 이 지형이 정말 «화면 끝»으로 판정되는가 ----
            //   이것이 거짓이면 아래 초록은 이 결함과 아무 상관이 없다(다른 것을 재고 있다는 뜻).
            //   ★ SenseAtEdge는 «접지 중»을 전제로 하므로 몸을 <b>먼저</b> 지면에 놓는다
            //     (이 파일의 다른 테스트들과 같은 순서 — 빠뜨리면 접지 전제에서 걸린다).
            PlaceBody(bb, groundCenterWorldX, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            for (int i = 0; i < 2; i++)
            {
                int direction = i == 0 ? -1 : 1;
                string label = direction > 0 ? "오른쪽" : "왼쪽";
                GroundSensor.GroundInfo info = SenseAtEdge(bb, direction, groundTopWorldY, label);
                bool hasWalkable = bb.TryGetWalkableScreenBoundsWorld(out float wl, out float wr);
                AutoWanderController.ResolveEffectiveEdgeBoundary(
                    direction > 0 ? info.CurrentFootholdRightWorldX : info.CurrentFootholdLeftWorldX,
                    direction > 0 ? info.ScreenRightWorldX : info.ScreenLeftWorldX,
                    hasWalkable, direction > 0 ? wr : wl, direction, out bool isTrueScreenEdge);
                float ropeMaxHere = AutoWanderController.ResolveRopeClimbMaxHeight(bb, info.GroundWorldY);
                bool wallHere = bb.TryFindRopeClimbWallWide(info, direction, out long wallHandleHere,
                    out float wallTopHere, stepUpMax, ropeMaxHere);
                float wallHeightHere = wallTopHere - info.GroundWorldY;
                Debug.Log($"{LogPrefix} [{label}] 전폭 지면 경계 판정 — isTrueScreenEdge={isTrueScreenEdge}, " +
                    $"딛은발판={info.GroundedFootholdHandle}, 지면Y={info.GroundWorldY:F3}, " +
                    $"경계X(좌{info.CurrentFootholdLeftWorldX:F3}/우{info.CurrentFootholdRightWorldX:F3}), " +
                    $"화면X(좌{info.ScreenLeftWorldX:F3}/우{info.ScreenRightWorldX:F3}) | " +
                    $"로프벽 탐색={wallHere}(핸들 {wallHandleHere}, 높이 {wallHeightHere:F3}), " +
                    $"대역=({stepUpMax:F3}, {ropeMaxHere:F3}].");
                Assert.IsTrue(isTrueScreenEdge,
                    $"{LogPrefix} [{label}] 전제 실패 — 전폭 지면인데 «화면 끝»으로 판정되지 않았습니다. " +
                    "이 테스트는 그 판정 위에서만 뜻이 있으므로, 여기서 멈추는 것이 초록으로 넘어가는 것보다 낫습니다.");
                Assert.IsTrue(wallHere,
                    $"{LogPrefix} [{label}] 전제 실패 — 화면 끝에서 로프 대역 벽을 찾지 못했습니다. " +
                    "게이트를 풀어도 찾을 벽이 없으면 발동할 수 없으므로, 이 배치부터 틀린 것입니다.");
            }

            // ---- (1) 양성 — 화면 끝에서도 밧줄은 발동한다 ----
            // ★ 이 테스트만 예산이 다르다 — 지면이 <b>화면 전폭</b>(32유닛)이라 다른 테스트의 좁은
            //   지면(0.2w)과 달리 경계까지 걸어가는 데만 오래 걸린다. 보행 1.5유닛/초 기준 중앙에서
            //   끝까지 약 10.7초이고, 방향은 50:50이라 반대로 출발하면 그 두 배다.
            //   그래서 ① 몸을 오른쪽 끝에서 4유닛 안쪽에 놓아 «가까운 쪽으로 출발하면 2.7초»가 되게 하고,
            //   ② 예산도 벽시계 기준으로 넉넉히 잡는다(프레임 수 기반 대기는 금지 — CLAUDE.md).
            const float WideGroundObserveSeconds = 45f;
            float startNearRightEdge = groundCenterWorldX + (bb.TryGetWalkableScreenBoundsWorld(out _, out float wrx)
                ? Mathf.Max(0f, wrx - groundCenterWorldX - 4f) : 0f);
            PlaceBody(bb, startNearRightEdge, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            TightenWanderForDeterminism();
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.stepUpChance = 0f;
            _clonedConfig.ropeClimbChance = 1f;
            // ★ 목표 지향 주기를 조인다 — 배포 기본 100초는 테스트 예산 안에 한 번도 안 돈다.
            //   (값 자체를 재는 테스트가 아니다. 여기서 재는 것은 «전폭 지면에서도 도는가»뿐이다.)
            _clonedConfig.climbSeekIntervalSeconds = 1f;
            RopeClimbQaOverride.SetChanceTestOverride(1f);

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260908));
            bb.IntentSource = wander;

            var obs = new Observation();
            yield return ObserveRope(wander, bb, WideGroundObserveSeconds, obs);

            Debug.Log($"{LogPrefix} 전폭 지면 실측 — 요청={obs.SawRequest}(t={obs.RequestSeconds:F2}s), " +
                $"진입={obs.SawState}, 총 {obs.ElapsedSeconds:F2}초.");
            Assert.IsTrue(obs.SawRequest || obs.SawState,
                $"{LogPrefix} 화면 전폭 발판(= Windows 작업표시줄)에서 {WideGroundObserveSeconds:F0}초 안에 밧줄등반이 " +
                "발동하지 않았습니다 — isTrueScreenEdge 게이트가 밧줄까지 다시 막고 있다는 뜻이고, 그것이 " +
                "사용자가 두 번 신고한 «윈도우에서는 밧줄 동작안함» 그 상태입니다.");

            // ★★★ 2026-09-08 — <b>요청만으로는 부족하다.</b> 사용자 신고 5회차의 실기 로그가 정확히
            //   «요청은 갔는데 상태 안에서 벽을 잃는» 그림이었다:
            //     [등반목표] 도착 — 벽핸들=5246684, 높이=13.233유닛 ... 지금 던집니다
            //     [밧줄등반] Throw 진입 — 벽핸들=0, 오를 높이=0.000유닛
            //     [밧줄등반] Throw 취소 — 목표 벽 소실
            //   소비자가 셋(AutoWander / WalkState / RopeClimbState.Enter)인데 앞의 둘만 고쳤고,
            //   세 번째가 여전히 «발판 경계» 기준으로 다시 찾다가 방금 그 벽을 놓쳤다.
            //   ⇒ 이 단언이 없으면 그 실패가 «요청 관측됨»이라는 <b>초록</b>에 그대로 숨는다.
            //     실제로 이 테스트는 그 상태에서도 통과했었다 — 그것이 이 라운드의 교훈이다.
            //   ★ 관측 루프(ObserveRope)는 «요청을 본 순간» 종료하므로 그것만으로는 진입을 잴 수 없다.
            //     진입·유지는 이 파일이 이미 가진 헬퍼가 전담한다(그 안에서 wander를 다시 틱하지 않는
            //     이유까지 그 함수 문단에 적혀 있다 — 펄스를 우리 손으로 지우면 판정이 리그 탓인지
            //     제품 탓인지 영영 못 가른다).
            yield return AssertRopeClimbEntersAndHolds(bb, obs);

            // ---- (2) 음성 — 같은 자리에서 뛰어내리기는 여전히 막힌다 ----
            //   밧줄만 죽이고 하강 갈래를 최대로 열어 둔다. 그런데도 뛰어내리기가 나오면
            //   이 라운드가 게이트를 «통째로» 연 것이고, 캐릭터가 화면 밖으로 걸어 나갈 수 있다.
            RopeClimbQaOverride.SetChanceTestOverride(0f);
            _clonedConfig.ropeClimbChance = 0f;
            _clonedConfig.hopDownChance = 1f;
            _clonedConfig.ledgeHangChance = 1f;
            _clonedConfig.stepUpChance = 1f;

            // 화면 끝 <b>아래</b>에 내려앉을 발판을 실제로 놓아 준다 — 없으면 «갈 곳이 없어서»
            // 안 나온 것과 «화면 끝이라 막혀서» 안 나온 것이 구분되지 않는다.
            float lowerTopOs = groundTopOs + 40f;
            _service.Footholds.Add(new PlatformFoothold(LowDropLeftHandle,
                new Rect(0f, lowerTopOs, w * 0.1f, h - lowerTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(LowDropRightHandle,
                new Rect(w * 0.9f, lowerTopOs, w * 0.1f, h - lowerTopOs), false));
            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            PlaceBody(bb, startNearRightEdge, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            float negativeBudget = Mathf.Max(3f, (obs.RequestSeconds > 0f ? obs.RequestSeconds : obs.ElapsedSeconds) * 3f);
            var descendWander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260909));
            bb.IntentSource = descendWander;

            bool sawDescend = false;
            float t = 0f;
            while (t < negativeBudget)
            {
                descendWander.Tick(Time.deltaTime);
                if (descendWander.HopDownRequested || descendWander.LedgeHangRequested) { sawDescend = true; break; }
                t += Time.deltaTime;
                yield return null;
            }

            Debug.Log($"{LogPrefix} 네거티브(하강 확률 전부 1) — 하강 요청 관측={sawDescend}, 예산 {negativeBudget:F2}초.");
            Assert.IsFalse(sawDescend,
                $"{LogPrefix} 화면 끝에서 하강(뛰어내리기/매달리기)이 발동했습니다 — 이 라운드가 게이트를 " +
                "밧줄만이 아니라 «통째로» 열었다는 뜻이고, 그러면 캐릭터가 화면 밖으로 나갈 수 있습니다.");
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ★★★ 2026-09-08 — 가로로 떨어진 창으로 갈 때 <b>몸이 밧줄을 따라</b> 오르는가
        // ════════════════════════════════════════════════════════════════════════════
        //
        // 사용자 신고: "높이가 다르고 좀 떨어져있는 창에서 창으로 이동할때 포즈가 이상함"
        //            → "창에서 창으로 이동하는것도 체크해야함"(테스트로 잠그라는 지시)
        //
        // 무엇이 이상했나: RopeClimbState의 Ascend가 반복 구간에서 <c>pos.x = _startWorldX</c>로
        // <b>제자리에서 수직으로만</b> 올랐다. 목표가 바로 위면 맞다(밧줄도 수직이다). 그런데
        // 가로로 떨어진 창이 목표면 앵커가 옆으로 멀어져 <b>밧줄은 대각선인데 몸은 수직</b>이 되어
        // 둘이 따로 놀고, 마지막 구간에서 가로 거리를 한꺼번에 메우느라 옆으로 미끄러졌다.
        //
        // 이 테스트가 잠그는 것 — <b>한 실행에서 셋 다</b>:
        //   (1) 전제 : 이 배치가 정말로 «가로로 떨어진» 목표를 만든다(가로 이동량이 유의미하다).
        //              이것이 없으면 아래 단언은 offset 0인 수직 등반에서도 조용히 통과한다.
        //   (2) 양성 : 오르는 동안 몸이 «시작점→앵커» 직선 위에 머문다(가로 진행 ≈ 세로 진행).
        //   (3) 음성 : 옛 거동(x를 시작점에 고정)이라면 (2)가 실제로 실패한다 — 같은 표본으로
        //              «고정했을 때의 이탈»을 함께 계산해 그것이 허용치를 넘는지 확인한다.
        //              (3)이 없으면 «허용치가 헐거워서» 통과한 것과 구분되지 않는다.
        [UnityTest]
        public IEnumerator 가로로_떨어진_창으로_오를_때_몸이_밧줄을_따라_간다()
        {
            yield return SetUpAgent();
            StickmanBlackboard bb = _agent.Blackboard;
            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            float groundTopOs = h * 0.5f;

            Vector3 xProbe0 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(0f, groundTopOs), 10f, _clonedConfig);
            Vector3 xProbe1 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(100f, groundTopOs), 10f, _clonedConfig);
            float unitsPerPixelX = Mathf.Abs(xProbe1.x - xProbe0.x) / 100f;
            Vector3 y0 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig);
            Vector3 y1 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs - 100f), 10f, _clonedConfig);
            float unitsPerPixelY = Mathf.Abs(y1.y - y0.y) / 100f;

            float stepUpMax = AutoWanderController.ResolveStepUpMaxHeightStatic(bb);
            float riseUnits = stepUpMax + 2f;
            float bandToleranceWorld = _clonedConfig.parkourDetectionRadius
                * Mathf.Max(1f, _clonedConfig.ropeClimbAdjacentSearchRadiusMultiplier);
            float farDistanceWorld = bandToleranceWorld * 0.6f;      // «좀 떨어져있는» 창
            float farDistanceOsPixels = farDistanceWorld / unitsPerPixelX;
            float riseOsOffset = riseUnits / unitsPerPixelY;
            float tallWallTopOs = groundTopOs - riseOsOffset;        // «높이가 다른» 창
            Assert.Greater(tallWallTopOs, 0f, $"{LogPrefix} 준비 실패 — 로프벽이 화면 위로 벗어났습니다.");

            float groundLeftOs = w * 0.35f;
            float groundRightOs = w * 0.5f;
            float tallWallRightLeftEdgeOs = groundRightOs + farDistanceOsPixels;
            Assert.Less(tallWallRightLeftEdgeOs + 120f, w,
                $"{LogPrefix} 준비 실패 — 오른쪽 로프벽이 화면 밖으로 나갔습니다(화면 {w:F0}px가 밴드 폭을 담기엔 좁습니다).");

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(GroundHandle,
                new Rect(groundLeftOs, groundTopOs, groundRightOs - groundLeftOs, h - groundTopOs), true));
            _service.Footholds.Add(new PlatformFoothold(TallWallRightHandle,
                new Rect(tallWallRightLeftEdgeOs, tallWallTopOs, 120f, h * 0.5f), false));
            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            float groundCenterWorldX = ScreenCoordinateConverter.OsScreenToWorld(
                cam, new Vector2((groundLeftOs + groundRightOs) * 0.5f, groundTopOs), 10f, _clonedConfig).x;
            float groundTopWorldY = y0.y;

            PlaceBody(bb, groundCenterWorldX, groundTopWorldY, GroundHandle);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            TightenWanderForDeterminism();
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.stepUpChance = 0f;
            _clonedConfig.ropeClimbChance = 1f;
            _clonedConfig.climbSeekIntervalSeconds = 1f;
            RopeClimbQaOverride.SetChanceTestOverride(1f);

            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260908));
            bb.IntentSource = wander;

            var obs = new Observation();
            yield return ObserveRope(wander, bb, MaxObserveSeconds, obs);
            Assert.IsTrue(obs.SawRequest || obs.SawState,
                $"{LogPrefix} 가로로 떨어진 벽에 대해 {MaxObserveSeconds:F0}초 안에 밧줄등반이 발동하지 않았습니다 — " +
                "이 배치부터 성립하지 않으면 아래 궤적 검사는 아무것도 재지 못합니다.");
            yield return AssertRopeClimbEntersAndHolds(bb, obs);

            // ---- 상승 궤적 표본 수집 ----
            float startX = bb.Body.position.x;
            float startY = bb.Body.position.y;
            float maxX = startX, minX = startX, topY = startY;
            var samples = new System.Collections.Generic.List<Vector2>();
            float elapsed = 0f;
            while (elapsed < AscendSampleBudgetSeconds
                   && bb.Machine.CurrentStateId == StickmanStateId.RopeClimb)
            {
                yield return null;
                elapsed += Time.deltaTime;
                Vector2 p = bb.Body.position;
                samples.Add(p);
                maxX = Mathf.Max(maxX, p.x); minX = Mathf.Min(minX, p.x);
                topY = Mathf.Max(topY, p.y);
            }

            Assert.Greater(samples.Count, 8,
                $"{LogPrefix} 상승 표본이 {samples.Count}개뿐입니다 — 궤적을 판정할 수 없습니다.");

            float totalRise = topY - startY;
            float totalRun = Mathf.Max(maxX - startX, startX - minX);
            Assert.Greater(totalRise, 0.05f, $"{LogPrefix} 상승이 관측되지 않았습니다({totalRise:F3}유닛).");

            // ---- (1) 전제 — 정말 «가로로 떨어진» 목표였는가 ----
            //   가로 이동이 세로 상승의 10%도 안 되면 사실상 수직 등반이고, 그러면 아래 (2)(3)은
            //   무엇도 증명하지 못한다(offset 0에서는 옛 코드도 통과한다).
            Debug.Log($"{LogPrefix} 대각선 등반 표본 — 상승 {totalRise:F3}유닛 / 가로 {totalRun:F3}유닛, 표본 {samples.Count}개.");
            Assert.Greater(totalRun, totalRise * 0.10f,
                $"{LogPrefix} 전제 실패 — 가로 이동({totalRun:F3})이 상승({totalRise:F3})의 10%에 못 미칩니다. " +
                "이 배치는 «가로로 떨어진 창»을 만들지 못했으므로 이 테스트는 겨냥을 잃었습니다.");

            // ---- (2)(3) 궤적 이탈 — 지금 코드 vs 옛 코드(x 고정) ----
            float worstNow = 0f, worstPinned = 0f;
            foreach (Vector2 p in samples)
            {
                float t = totalRise > 0.0001f ? Mathf.Clamp01((p.y - startY) / totalRise) : 1f;
                float expectedX = Mathf.Lerp(startX, startX + (maxX - startX != 0f ? (maxX - startX) : (minX - startX)), t);
                worstNow = Mathf.Max(worstNow, Mathf.Abs(p.x - expectedX));
                worstPinned = Mathf.Max(worstPinned, Mathf.Abs(startX - expectedX));   // 옛 거동이었다면
            }
            float tolerance = Mathf.Max(0.15f, totalRun * 0.35f);
            Debug.Log($"{LogPrefix} 궤적 이탈 — 지금 {worstNow:F3}유닛 / 옛 거동(x고정) {worstPinned:F3}유닛, 허용 {tolerance:F3}유닛.");

            Assert.Less(worstNow, tolerance,
                $"{LogPrefix} 오르는 동안 몸이 밧줄 선에서 최대 {worstNow:F3}유닛 벗어났습니다(허용 {tolerance:F3}). " +
                "밧줄은 대각선인데 몸이 수직으로만 오르고 있다는 뜻입니다 — 사용자 신고 «높이가 다르고 좀 " +
                "떨어져있는 창으로 이동할때 포즈가 이상함»이 정확히 이 상태입니다.");

            // ★ 음성 대조 — 허용치가 헐거워서 통과한 것이 아님을 같은 표본으로 증명한다.
            Assert.Greater(worstPinned, tolerance,
                $"{LogPrefix} 음성 대조 실패 — 옛 거동(x를 시작점에 고정)이었어도 이탈 {worstPinned:F3}가 " +
                $"허용치 {tolerance:F3} 안에 듭니다. 그러면 위 통과는 «고쳐서»가 아니라 «허용치가 헐거워서»일 " +
                "수 있고, 이 테스트는 회귀를 잡지 못합니다.");
        }

        /// <summary>대각선 등반 궤적을 표집할 벽시계 예산(초) — 프레임 수 기반 대기는 금지다
        /// (CLAUDE.md: 배치모드 PlayMode는 2,000fps 이상으로 돌아 프레임 예산이 무의미하다).</summary>
        private const float AscendSampleBudgetSeconds = 8f;
    }
}
