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
    /// ★ Dock **타일 크기(tilesize) 변화 × 되올라오기** — 2026-08-30 횡단 리뷰 M3 대응.
    ///
    /// ============================================================================
    /// 이 파일이 왜 새로 필요했나 (커버리지 구멍이 버그를 숨기고 있었다)
    /// ============================================================================
    /// 이 세션은 "한 번 Dock 아래로 내려가면 영영 못 올라온다"를 세 번 신고받고 두 번 고쳤다
    /// (stepUpMaxHeight 1.5 → 2.4, postClimbDescendCooldown 도입). 그런데 관련 PlayMode 테스트는
    /// **전부 이 개발 머신의 tilesize 49 하나에 고정**돼 있었다. tilesize를 바꿔 보는 테스트가 0건이다.
    ///
    /// 실제 의존성:  낙차(pt) = tilesize + dockThicknessTilePaddingPoints(26) − 안전망 인셋(8)
    ///                        = tilesize + 18
    /// macOS 시스템 설정의 tilesize 범위는 16~128이므로 월드 낙차는
    ///     16 → 0.831유닛 / 48 → 1.613(macOS 기본) / 49 → 1.637(이 머신) / 80 → 2.395 / 128 → 3.568
    /// 이고, 고쳤다고 믿었던 절대값 stepUpMaxHeight=2.4는 **tilesize 81부터 낙차를 못 덮는다**
    /// (교차점 80.2 — 2026-08-30 R3 M2 정정. 아래 "네거티브 컨트롤" 절 참고).
    /// 즉 Dock 아이콘을 크게 쓰는 사용자에게는 그 버그가 처음부터 끝까지 그대로 남아 있었고,
    /// 테스트가 0건이라 아무도 몰랐다.
    ///
    /// ============================================================================
    /// 무엇을 어떻게 검증하는가
    /// ============================================================================
    ///  (A) tilesize 16 / 48 / 80 / 128 각각에서 **실측 경로 그대로** 낙차를 재고
    ///      (Dock 발판 상단 − 바닥 안전망 상단, AutoWanderController가 쓰는 바로 그 조회 경로),
    ///      유도된 되올라가기 상한이 그 낙차를 덮는지 단언한다. 배치는 실제 앱과 같은 합성 발판
    ///      핸들(-2 Dock / -1·-3 안전망)을 쓴다 — 임의 핸들을 쓰면 실측 경로가 Dock을 못 찾아
    ///      **테스트가 수정 자체를 우회**한다(그 함정 자체를 아래 CriticalTileSize_...가 잠근다).
    ///  (B) 최악값 tilesize 128에서 **실제 되올라오기**가 닫히는지 자율 배회 AI(AutoWanderController)를
    ///      그대로 돌려 관찰한다. 여기서 ScriptedIntentSource 같은 가짜 의도를 주입하면 고친 판정
    ///      (AutoWanderController.ResolveStepUpMaxHeight)을 통째로 건너뛰게 되므로 **주입하지 않는다.**
    ///      대신 **확률과 시드만 제거**한다(고정 시드 + 확률 1/0 + 지터 0) — 판정 로직은 실제 코드 그대로다.
    ///
    /// ============================================================================
    /// ★ 이 테스트가 확률에 기대지 않는 이유 (2026-08-30 R3 M1 — 이 파일 자신의 결함이었다)
    /// ============================================================================
    /// 최초 작성본의 (B)는 캐릭터를 Dock 모서리 0.6유닛 옆에 세워 두고 **배회 AI가 스스로 왼쪽으로
    /// 걸어와 stepUpChance(0.85) 추첨을 이기기를 25초 기다리는** 구조였다. StickmanAgent.cs가
    /// `new System.Random(System.Guid.NewGuid().GetHashCode())`로 매 실행 다른 시드를 주입하므로
    /// 이 테스트는 매 실행 다른 경로를 걸었고, test-engineer의 3회 반복 실행에서 **1승 2패**를 냈다
    /// (실패 2회의 최종 x = 6.738 / 11.492 — 반대 방향으로 걸어가 25초 동안 돌아오지 않았다).
    /// 빨간불이 무작위로 켜지는 테스트는 곧 "또 그 flaky"로 무시당하므로 회귀 자산이 아니라 부채다.
    ///
    /// 그래서 이 프로젝트의 기존 관례(Tests/PlayMode/EdgeHopDownTests의
    /// AutoWanderHopsDownAndClimbsBackWithoutScriptedPulses)를 그대로 따른다 —
    ///   · 복제한 StickConfig로 **직접 만든** AutoWanderController를 IntentSource에 꽂고 코루틴이 Tick한다
    ///     (에이전트가 들고 있는 컨트롤러는 **원본** config + 무작위 시드로 생성돼 있어 못 쓴다.
    ///      원본 자산을 런타임에 고치는 것은 CLAUDE.md 불변 원칙 3 위반이므로 복제본을 쓴다),
    ///   · 시드를 고정하고(FixedWanderSeed) 흔들림(지터/즉흥 방향전환/제자리 점프/경계 점프)을 끄며
    ///     경계 행동 확률을 1 또는 0으로 못박는다,
    ///   · 시작 위치를 **화면 오른쪽 클램프 한계 바로 안쪽**으로 잡아 진행 방향 추첨까지 제거한다.
    ///     (PickDirectionAvoidingEdge가 화면 끝에 붙어 있으면 안쪽으로 강제하고, 설령 바깥쪽을
    ///      골라도 화면 끝 경계 판정이 0.15초 만에 방향을 되돌린다 = 어느 쪽이든 왼쪽으로 걷는다.)
    /// 확률 자체를 검증하는 테스트가 아니다 — "확률이 성립했을 때 그 경로가 끝까지 이어지는가"만 본다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// AutoWanderController.ResolveStepUpMaxHeight()를 예전 코드
    /// (`Cfg(c =&gt; c.stepUpMaxHeight, 1.5f)`)로 되돌리면 (A)의 tilesize 128과 (B)가 즉시 실패한다.
    /// 되돌리지 않고도 같은 산술을 확인할 수 있게, (A)는 "설정 절대값 단독으로 덮는가"를 **양방향으로**
    /// 단언한다(교차점 아래 tilesize에서는 덮고, 위에서는 못 덮는다). 그 단언이 통과한다는 것은
    /// 이 테스트가 **유도 로직이 없으면 반드시 빨간불**이라는 뜻이다.
    ///
    /// ★ 2026-08-30 R3 M2 정정 — 그 교차점은 tilesize 80이 아니라 **80.2**다.
    ///     stepUpMaxHeight 2.400유닛 ÷ (24/982 유닛/pt) = 98.2pt,  낙차(pt) = tilesize + 18
    ///     ⇒ 79 → 2.3707 ✔덮음 / 80 → 2.3951 ✔덮음 / **81 → 2.4196 ✘못덮음**
    /// 최초 작성본의 `if (tileSizePoints &gt;= 80f)` 게이트는 한 칸 일러서 tilesize 80에서
    /// `configured(2.400) &lt;= measuredDrop(2.39511)`을 요구했고 그대로 실패했다.
    /// 게다가 tilesize 80은 교차점에서 **0.005유닛**밖에 안 떨어져 있어 이 파일이 허용하는 좌표 왕복
    /// 오차(0.02유닛)보다 작다 — 부등호를 어느 쪽으로 적든 실측값이 뒤집힐 수 있는 자리이므로
    /// 그 근방(±CrossoverAmbiguityBandUnits)에서는 **어느 쪽도 단언하지 않는다.**
    /// 왕복 오차가 없는 순수 산술로 정확한 교차점(80 ✔ / 81 ✘)을 잠그는 것은
    /// Tests/EditMode/DockGeometryInvariantTests의 별도 테스트 몫이다.
    /// </summary>
    public sealed class DockTileSizeStepUpTests
    {
        private const string LogPrefix = "[DOCK-TILESIZE]";

        /// <summary>실제 앱의 합성 발판 핸들과 같은 값(FallbackPlatformWindowService 참고).
        /// ★ 이 값이어야만 런타임의 실측 낙차 조회가 Dock을 찾는다 — 임의 핸들을 쓰면 안 된다.</summary>
        private const long DockHandle = -2L;
        private const long NetLeftHandle = -1L;
        private const long NetRightHandle = -3L;

        private const float SettleWaitSeconds = 2.0f;
        private const float RoundTripObserveSeconds = 25f;

        /// <summary>(B)의 배회 컨트롤러 시드. 값 자체에 의미는 없고 **고정돼 있다는 사실**이 전부다
        /// (R3 M1: 에이전트가 만드는 컨트롤러는 Guid 기반이라 매 실행 다른 경로를 걷는다).</summary>
        private const int FixedWanderSeed = 20260830;

        /// <summary>(B) 시작 위치를 화면 오른쪽 걷기 한계에서 얼마나 안쪽에 둘지(월드 유닛).
        /// wanderEdgeStopDistance(0.30)보다 작아야 "화면 끝에 붙어 있다"로 판정돼 진행 방향이
        /// 안쪽(왼쪽)으로 강제된다.</summary>
        private const float StartInsetFromScreenEdgeUnits = 0.15f;

        /// <summary>(A) 네거티브/포지티브 컨트롤을 단언하지 않는 교차점 근방 폭(월드 유닛).
        /// 좌표 왕복 허용오차(0.02)보다 넉넉히 크게 잡아, 교차점에 붙은 tilesize에서 부등호가
        /// 측정 오차로 뒤집히는 두 번째 flaky를 원천 차단한다.</summary>
        private const float CrossoverAmbiguityBandUnits = 0.05f;

        /// <summary>(C) 전용 — 배회 AI를 거치지 않고 "왼쪽으로 계속 걸어라"만 주입한다.
        /// 배회 AI를 쓰면 **수정이 동작하는 순간** 경계 판정에 걸려 벽에 닿기 전에 돌아서므로
        /// 이격 자체를 잴 수 없다(수정 전에만 측정 가능한 테스트가 되어 회귀 잠금이 안 된다).</summary>
        private sealed class ScriptedIntentSource : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

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

        [TearDown]
        public void TearDown()
        {
            StickmanEventBus.StateTransitioned -= OnTransition;
            Application.logMessageReceived -= OnLogMessage;
            if (_agent != null && _agent.Blackboard != null)
            {
                if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
                // ★ (B)가 자기 AutoWanderController를 꽂아 두므로 반드시 되돌린다 — 안 되돌리면
                // 다음 테스트의 캐릭터가 **파괴된 복제 config**를 든 컨트롤러의 의도를 읽는다.
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _originalIntent = null;
            _agent = null;
        }

        private readonly List<string> _trace = new List<string>();
        private bool _sawParkourClimb;

        private void OnTransition(StateTransitionEvent e)
        {
            if (_trace.Count < 200) _trace.Add($"{e.From}->{e.To}");
            if (e.To == StickmanStateId.ParkourClimb) _sawParkourClimb = true;
        }

        /// <summary>M3 유도 경로(AutoWanderController.ResolveStepUpMaxHeight)가 실제로 발동했는지
        /// 로그로 확인한다. R3 리뷰가 지적한 구멍 — 223건 전체 실행 동안 이 경고가 **0회**였다.
        /// 즉 유도 경로를 지나는 테스트가 사실상 없었다.</summary>
        private bool _sawStepUpDerivationWarning;

        private const string DerivationWarningNeedle = "실측 Dock 낙차";

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning && condition != null && condition.Contains(DerivationWarningNeedle))
            {
                _sawStepUpDerivationWarning = true;
            }
        }

        // ============================================================================
        // (A) tilesize 전 구간 — 유도된 상한이 실측 낙차를 덮는가
        // ============================================================================

        [UnityTest] public IEnumerator StepUpCoversDrop_TileSize16() { yield return AssertStepUpCoversDrop(16f); }
        [UnityTest] public IEnumerator StepUpCoversDrop_TileSize48() { yield return AssertStepUpCoversDrop(48f); }
        [UnityTest] public IEnumerator StepUpCoversDrop_TileSize80() { yield return AssertStepUpCoversDrop(80f); }
        [UnityTest] public IEnumerator StepUpCoversDrop_TileSize128() { yield return AssertStepUpCoversDrop(128f); }

        private IEnumerator AssertStepUpCoversDrop(float tileSizePoints)
        {
            float expectedDrop = DockGeometry.DockDropWorldUnits(tileSizePoints,
                DockGeometry.DefaultDockThicknessTilePaddingPoints);
            yield return SetUpDockLayout(expectedDrop);

            StickmanBlackboard bb = _agent.Blackboard;

            // ── 런타임이 실제로 쓰는 조회 경로 그대로 낙차를 잰다(핸들 기반 실측).
            Assert.IsTrue(bb.TryGetFootholdTopWorldY(DockHandle, out float dockTopY),
                $"{LogPrefix} Dock 발판(핸들 {DockHandle})의 상단을 조회하지 못했습니다 — 배치가 잘못됐거나 " +
                "TryGetFootholdTopWorldY 계약이 바뀌었습니다.");
            bool netFound = bb.TryGetFootholdTopWorldY(NetLeftHandle, out float netTopY)
                            || bb.TryGetFootholdTopWorldY(NetRightHandle, out netTopY);
            Assert.IsTrue(netFound, $"{LogPrefix} 바닥 안전망 조각의 상단을 조회하지 못했습니다.");

            float measuredDrop = dockTopY - netTopY;
            float configured = _clonedConfig.stepUpMaxHeight;
            float resolved = DockGeometry.ResolveStepUpMaxHeight(configured, measuredDrop);

            Debug.Log($"{LogPrefix} tilesize={tileSizePoints:F0}pt — 기대 낙차 {expectedDrop:F4}유닛, " +
                $"실측 낙차 {measuredDrop:F4}유닛(Dock 상단 {dockTopY:F4} − 안전망 상단 {netTopY:F4}), " +
                $"stepUpMaxHeight 설정값 {configured:F3} → 유도값 {resolved:F4} (여유 {(resolved - measuredDrop):F4}), " +
                $"매달리기 최소 낙차 {bb.LedgeHangMinDropDepth:F4}, 배율 {_clonedConfig.ResolveCharacterScale():F3}");

            // 전제 — 배치가 의도한 낙차를 실제로 만들어 냈다(좌표 왕복 오차 허용 0.02유닛).
            Assert.AreEqual(expectedDrop, measuredDrop, 0.02f,
                $"{LogPrefix} 재현된 낙차가 유도식과 다릅니다 — OS↔월드 환산이 어긋났습니다.");

            // ★ 절대 조건 — 유도된 되올라가기 상한이 낙차를 덮는다(못 덮으면 영구 갇힘).
            Assert.Greater(resolved, measuredDrop,
                $"{LogPrefix} tilesize {tileSizePoints:F0}pt(낙차 {measuredDrop:F3}유닛)에서 되올라가기 상한" +
                $"({resolved:F3})이 낙차를 덮지 못합니다 — 이 Dock 설정을 쓰는 사용자는 한 번 내려가면 못 올라옵니다.");

            // ★ 네거티브/포지티브 컨트롤 — "설정 절대값 **단독으로** 낙차를 덮는가"를 양방향으로 박제한다.
            //
            // 2026-08-30 R3 M2 정정: 예전에는 `if (tileSizePoints >= 80f)` 게이트 하나로 "80 이상은
            // 못 덮는다"만 단언했는데 그 게이트가 **한 칸 일렀다**. 교차점은 80이 아니라 80.2다
            // (2.400유닛 ÷ 0.0244399유닛/pt = 98.2pt, 낙차pt = tilesize + 18). 그래서 tilesize 80은
            // 2.3951유닛으로 **아직 2.4가 덮는 쪽**이고, 단언은 거짓이 되어 그대로 실패했다.
            //
            // 이제는 tilesize를 하드코딩하지 않고 교차점을 그 자리에서 산술로 유도한 뒤,
            //   · 교차점보다 확실히 아래(여유 > CrossoverAmbiguityBandUnits) → "절대값이 덮는다"를 단언
            //   · 교차점보다 확실히 위                                        → "절대값이 못 덮는다"를 단언
            //   · 교차점 근방(±밴드)                                          → **아무 것도 단언하지 않는다**
            // 마지막 갈래가 필요한 이유: tilesize 80은 교차점에서 0.005유닛 떨어져 있을 뿐인데 이 파일이
            // 허용하는 좌표 왕복 오차는 0.02유닛이다. 그 자리에서 부등호를 쓰는 것은 부호가 측정 노이즈로
            // 뒤집히는 두 번째 flaky를 심는 짓이다. 정확한 경계(80 ✔ / 81 ✘)는 왕복 오차가 전혀 없는
            // 순수 산술로 Tests/EditMode/DockGeometryInvariantTests가 따로 잠근다.
            float crossoverTileSizePoints = configured / DockGeometry.ReferenceWorldUnitsPerPoint
                - DockGeometry.DefaultDockThicknessTilePaddingPoints
                + NullPlatformWindowService.BottomSafetyNetInsetPoints;
            // 여유 > 0 이면 설정 절대값 단독으로 덮는다. 유도식 낙차(expectedDrop)를 쓴다 — 이 판정의
            // 기준은 "이 tilesize가 교차점의 어느 쪽인가"라는 산술이지 측정 노이즈가 아니기 때문이다.
            float soloCoverageMargin = configured - expectedDrop;

            Debug.Log($"{LogPrefix} tilesize={tileSizePoints:F0}pt — 설정 절대값 단독 커버리지 여유 " +
                $"{soloCoverageMargin:F4}유닛 (교차 tilesize {crossoverTileSizePoints:F2}pt, " +
                $"판정 유보 밴드 ±{CrossoverAmbiguityBandUnits:F2}유닛)");

            if (soloCoverageMargin < -CrossoverAmbiguityBandUnits)
            {
                Assert.Less(configured, measuredDrop,
                    $"{LogPrefix} tilesize {tileSizePoints:F0}pt(교차점 {crossoverTileSizePoints:F2}pt보다 위)에서 " +
                    $"설정 절대값({configured:F3})이 낙차({measuredDrop:F3})를 이미 덮고 있습니다 — " +
                    "이 테스트의 전제(M3의 근거)가 바뀌었습니다. stepUpMaxHeight가 크게 올라갔다면 " +
                    "'일반 창까지 순간이동 등반' 쪽을 다시 검토하세요.");
            }
            else if (soloCoverageMargin > CrossoverAmbiguityBandUnits)
            {
                Assert.Greater(configured, measuredDrop,
                    $"{LogPrefix} tilesize {tileSizePoints:F0}pt(교차점 {crossoverTileSizePoints:F2}pt보다 아래)에서 " +
                    $"설정 절대값({configured:F3})이 낙차({measuredDrop:F3})를 못 덮습니다 — " +
                    "낙차 유도식이나 stepUpMaxHeight가 바뀌었습니다(교차점 자체를 재검산할 것).");
            }
            else
            {
                Debug.Log($"{LogPrefix} tilesize {tileSizePoints:F0}pt는 교차점 {crossoverTileSizePoints:F2}pt " +
                    "근방(왕복 오차 밴드 안)이라 절대값 단독 커버리지를 **단언하지 않는다** — " +
                    "이 자리에서 부등호를 쓰면 측정 노이즈로 뒤집힌다(2026-08-30 R3 M2).");
            }

            // 참고 로그 — 내려가는 갈래가 무엇으로 분류되는지도 남긴다(뛰어내리기 vs 매달리기).
            bool hopDownBand = measuredDrop < bb.LedgeHangMinDropDepth;
            Debug.Log($"{LogPrefix} tilesize={tileSizePoints:F0}pt 하강 갈래 = " +
                $"{(hopDownBand ? "뛰어내리기" : "매달려 내려가기")} " +
                $"(낙차 {measuredDrop:F3} vs 매달리기 최소 {bb.LedgeHangMinDropDepth:F3}). " +
                "둘 중 하나만 성립하면 정상이다 — 둘 다 불성립이면 Dock 위에 갇힌다.");
            Assert.IsTrue(hopDownBand || _clonedConfig.ledgeHangChance > 0f,
                $"{LogPrefix} tilesize {tileSizePoints:F0}pt에서 뛰어내리기 밴드를 벗어났는데 " +
                "ledgeHangChance가 0입니다 — 내려갈 길이 하나도 없습니다.");
        }

        // ============================================================================
        // (B) 최악값 tilesize 128 — 실제 되올라오기가 닫히는가
        //     (자율 배회 AI의 판정 로직 그대로 / 확률과 시드만 제거 — 클래스 문서 "확률에 기대지 않는 이유")
        // ============================================================================

        [UnityTest]
        public IEnumerator LargestTileSizeStillClimbsBackOntoDock()
        {
            yield return AssertClimbsBackOntoDock(128f, requireDerivationWarning: true);
        }

        // ★ 2026-08-30 R3-M1 — 되올라오기 왕복을 **tilesize 전 구간**에서 돌린다.
        // 128 하나만 보던 것이 R3-M1을 늦게 발견한 이유 중 하나였다: 벽 이격 충돌은 tilesize가
        // 커야만 켜지지만(머리 원을 덮을 만큼 벽이 높아야 한다), 켜지는 문턱은 33 근처라
        // **macOS 기본 48과 이 개발 머신 49가 이미 그 안**이었다. 아래 128 전용 테스트는
        // "유도 경고까지 관측"이라는 추가 조건이 있어 그대로 남기고, 이쪽은 왕복만 본다.
        [UnityTest] public IEnumerator ClimbsBackOntoDock_TileSize16() { yield return AssertClimbsBackOntoDock(16f, false); }
        [UnityTest] public IEnumerator ClimbsBackOntoDock_TileSize48() { yield return AssertClimbsBackOntoDock(48f, false); }
        [UnityTest] public IEnumerator ClimbsBackOntoDock_TileSize80() { yield return AssertClimbsBackOntoDock(80f, false); }

        private IEnumerator AssertClimbsBackOntoDock(float tileSizePoints, bool requireDerivationWarning)
        {
            float drop = DockGeometry.DockDropWorldUnits(tileSizePoints, DockGeometry.DefaultDockThicknessTilePaddingPoints);
            yield return SetUpDockLayout(drop);

            StickmanBlackboard bb = _agent.Blackboard;

            // ── 전제 1: 이 배치의 낙차가 설정 절대값을 **넘어야** 한다. 넘지 않으면 되올라가기가
            //    유도(ResolveStepUpMaxHeight) 없이도 통과해 버려 이 테스트는 M3를 하나도 잠그지 못한다.
            //    이 전제가 성립할 때에만 아래의 등반 성공이 곧 "유도가 동작했다"의 증거가 된다.
            if (requireDerivationWarning)
            {
                Assert.Greater(drop, _clonedConfig.stepUpMaxHeight,
                    $"{LogPrefix} 전제 실패 — tilesize {tileSizePoints:F0}의 낙차({drop:F3})가 stepUpMaxHeight 설정값" +
                    $"({_clonedConfig.stepUpMaxHeight:F3}) 이하입니다. 이 상태로는 등반이 성공해도 " +
                    "M3의 유도 경로를 지났다는 증거가 되지 못합니다(테스트 무의미).");
            }

            // ── 확률/시드 제거(R3 M1). 확률 자체가 아니라 "확률이 성립했을 때 경로가 끝까지 이어지는가"를
            //    보는 테스트다. EdgeHopDownTests.AutoWanderHopsDownAndClimbsBackWithoutScriptedPulses와
            //    같은 관례이며, 판정 로직(TryRollEdgeAction/ResolveStepUpMaxHeight)은 실제 코드 그대로다.
            _clonedConfig.wanderIdleDurationMin = 0.05f;
            _clonedConfig.wanderIdleDurationMax = 0.05f;
            // 걷기 구간을 관찰창보다 길게 잡아 도중에 Idle로 빠지지 않게 한다(Idle 복귀는 방향 재추첨을 부른다).
            _clonedConfig.wanderWalkDurationMin = RoundTripObserveSeconds * 4f;
            _clonedConfig.wanderWalkDurationMax = RoundTripObserveSeconds * 4f;
            _clonedConfig.wanderDurationJitterRatio = 0f;
            _clonedConfig.wanderSpontaneousTurnChance = 0f;
            _clonedConfig.wanderPostIdleWalkChance = 1f;
            _clonedConfig.wanderPostIdleJumpChance = 0f;
            _clonedConfig.wanderEdgeJumpAttemptChance = 0f;
            _clonedConfig.wanderEdgeTurnPauseMin = 0.15f;
            _clonedConfig.wanderEdgeTurnPauseMax = 0.15f;
            // 캐릭터는 이미 바닥 안전망 위(= 더 내려갈 곳이 없다)에 있으므로 내려가는 두 갈래는 어차피
            // 대상을 못 찾는다. 0으로 못박아 추첨 자체를 없앤다 — 남는 갈래는 되올라가기 하나뿐이다.
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.stepUpChance = 1f;

            // ── 시작 위치: **화면 오른쪽 걷기 한계 바로 안쪽**(안전망 오른쪽 조각 위) = "이미 내려와 있는" 상태.
            //    여기서 되올라오지 못하면 그것이 곧 사용자가 신고한 증상이다.
            //    이 위치를 고른 이유는 진행 방향 추첨을 없애기 위해서다(클래스 문서 참고):
            //      · PickDirectionAvoidingEdge가 "화면 끝에 붙어 있음"을 보고 안쪽(왼쪽)으로 강제하고,
            //      · 설령 바깥쪽을 골라도 화면 끝 경계 판정이 wanderEdgeTurnPause(0.15초) 만에 되돌린다.
            //    걷기 한계는 화면 하드 클램프와 **같은 계산식 하나**에서 나온다(TryGetWalkableScreenBoundsWorld).
            bb.MoveBodyToWorld(new Vector2(_dockRightWorldX + 0.6f, _floorTopWorldY));
            Assert.IsTrue(bb.TryGetWalkableScreenBoundsWorld(out _, out float walkableRightX),
                $"{LogPrefix} 걷기 가능 X 범위를 조회하지 못했습니다 — Body/MainCamera 배선 확인.");

            float startX = walkableRightX - StartInsetFromScreenEdgeUnits;
            Assert.Greater(startX - _dockRightWorldX, 1f,
                $"{LogPrefix} 준비 실패 — 안전망 오른쪽 조각이 너무 좁습니다(시작 x={startX:F3}, " +
                $"Dock 오른쪽 모서리={_dockRightWorldX:F3}). 걸어올 구간이 없으면 경계 판정이 무의미합니다.");

            bb.MoveBodyToWorld(new Vector2(startX, _floorTopWorldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = NetRightHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            _trace.Clear();
            _sawParkourClimb = false;
            _sawStepUpDerivationWarning = false;
            StickmanEventBus.StateTransitioned += OnTransition;
            Application.logMessageReceived += OnLogMessage;

            // ★ 에이전트가 들고 있는 AutoWanderController는 **원본** StickConfig + Guid 시드로 생성돼 있어
            //   위 조정이 하나도 반영되지 않는다. 원본 자산을 런타임에 고치는 것은 금지이므로(불변 원칙 3)
            //   복제본으로 만든 컨트롤러를 IntentSource에 꽂고 이 코루틴이 직접 Tick한다.
            //   에이전트 쪽 컨트롤러도 계속 Tick되지만 그 출력은 아무도 읽지 않는다(EdgeHopDownTests 전례).
            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(FixedWanderSeed));
            bb.IntentSource = wander;

            Debug.Log($"{LogPrefix} 되올라오기 관찰 시작 — tilesize {tileSizePoints:F0} 낙차 {drop:F3}유닛, " +
                $"몸 물리 반폭 {bb.CharacterPhysicalHalfWidthWorld:F3}, 경계 판정 거리 유도값 {bb.EdgeStopDistanceWorld:F3}" +
                $"(설정 {_clonedConfig.wanderEdgeStopDistance:F3}), " +
                $"시작 위치 x={startX:F3}(걷기 한계 {walkableRightX:F3}에서 {StartInsetFromScreenEdgeUnits:F2} 안쪽, " +
                $"Dock 오른쪽 모서리 {_dockRightWorldX:F3}), " +
                $"stepUpMaxHeight 설정값 {_clonedConfig.stepUpMaxHeight:F3}(이 값만으로는 못 덮는다), " +
                $"stepUpChance={_clonedConfig.stepUpChance:F2}, 시드={FixedWanderSeed}");

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

            Debug.Log($"{LogPrefix} 되올라오기 결과 — 되올라옴={backOnDock}, 등반관측={_sawParkourClimb}, " +
                $"유도경고관측={_sawStepUpDerivationWarning}, {elapsed:F1}초, " +
                $"최종 발판핸들={bb.CurrentFootholdHandle}, " +
                $"위치=({bb.Body.position.x:F3},{bb.Body.position.y:F3}), Dock 상단 Y={_dockTopWorldY:F3}\n" +
                $"    전이: {(_trace.Count == 0 ? "(없음)" : string.Join(" ", _trace))}");

            // ★ 절대 조건 — 가장 큰 Dock 아이콘 설정에서도 되올라온다.
            Assert.IsTrue(_sawParkourClimb,
                $"{LogPrefix} tilesize {tileSizePoints:F0}(낙차 {drop:F3}유닛)에서 {RoundTripObserveSeconds:F0}초 동안 " +
                "ParkourClimb에 한 번도 진입하지 못했습니다 — AutoWanderController가 이 턱을 " +
                "'너무 높다'고 계속 기각했다는 뜻입니다(= 사용자가 신고한 '영영 못 올라옴'). " +
                "ResolveStepUpMaxHeight의 실측 낙차 유도를 확인하세요.");
            Assert.IsTrue(backOnDock,
                $"{LogPrefix} 등반은 시도했으나 Dock 발판({DockHandle}) 위로 복귀하지 못했습니다 " +
                $"(최종 핸들 {bb.CurrentFootholdHandle}).");

            // ★ M3 유도 경로가 실제로 발동했다는 직접 증거(R3가 지적한 "223건 실행 중 경고 0회" 구멍).
            //   위 전제 1(낙차 > 설정 절대값) 덕분에 등반 성공만으로도 유도를 지났다는 것이 논리적으로
            //   확정되지만, 로그로도 한 번 더 못박아 다음 사람이 "유도가 도는지" 눈으로 확인할 수 있게 한다.
            if (requireDerivationWarning)
            {
                Assert.IsTrue(_sawStepUpDerivationWarning,
                    $"{LogPrefix} 되올라가기 상한 유도 경고(\"{DerivationWarningNeedle}...\")가 한 번도 " +
                    "찍히지 않았습니다 — 실측 낙차 조회(TryMeasureDockDropWorldUnits)가 Dock 발판을 " +
                    "못 찾았다는 뜻이며, 그렇다면 이 배치가 실제 앱의 합성 발판 핸들과 어긋난 것입니다.");
            }
        }

        // ============================================================================
        // (C) ★ Dock 계단 **옆면** 이격 실측 — 2026-08-30 R3-M1
        // ============================================================================
        //
        // R3가 잡은 실패는 "가끔 못 올라온다"가 아니라 **기하가 이미 어긋나 있었다**는 것이다:
        //   · DockPhysicsStep의 계단 옆면은 바닥 안전망 조각의 논리 경계와 정확히 같은 X에 선다.
        //   · 그 벽에 막혀 선 캐릭터의 루트는 몸의 물리 반폭(배율 0.75에서 0.300) + Box2D 접촉
        //     이격(약 0.005) 아래로 절대 다가가지 못한다 = 0.305.
        //   · 그런데 경계 판정 거리는 0.300이었다 → **밴드가 물리적으로 도달 불가능**.
        // 아래 테스트는 그 이격을 **실제로 걸어가 부딪혀서** 재고, 유도된 판정 거리가 그것을
        // 명확한 여유(≥ 0.05유닛)로 덮는지 단언한다. 산술이 아니라 물리 실측이라는 점이 핵심이다 —
        // EditMode의 DockGeometryInvariantTests가 같은 관계를 순수 산술로 따로 잠근다.
        //
        // 네거티브 컨트롤(2026-08-30 디버거가 실제로 되돌려 확인): AutoWanderController.IsNearFootholdEdge를
        // 예전 코드(`Cfg(c => c.wanderEdgeStopDistance, 0.3f)`)로 되돌리면 **소비자가 쓴 값**이 0.300으로
        // 떨어져 tilesize 48/80/128에서 여유가 −0.005가 되고, 같은 자리의 경계 판정도 false가 되어
        // 두 단언이 동시에 빨간불을 낸다.
        // ★ 1차 시도의 실패 기록: 처음에는 블랙보드의 유도 프로퍼티(EdgeStopDistanceWorld)만 단언했는데,
        //   그것은 소비자가 유도를 그만 읽어도 그대로 유지되는 값이라 **되돌려도 12/12 통과**했다
        //   (Logs/dbg_m1_negctrl_play.xml). 그래서 컨트롤러의 진단 창구(LastEdgeStopDistanceUsed /
        //   LastEdgeNear)를 보도록 바꿨다 — "값이 맞는가"가 아니라 "쓰는 쪽이 그 값을 보는가"가 본질이다.

        // ★ 정지 판정을 "프레임당 이동량"으로 재면 안 된다(2026-08-30 디버거 1차 시도의 실패):
        // 배치 모드는 프레임 간격이 1ms 안팎이라 **출발 직후 가속 구간**의 프레임당 이동량이
        // 0.00004유닛까지 내려가고, 그것이 "벽에 붙어 멈췄다"로 오인돼 0.02초 만에 측정이 끝났다
        // (실측: 이격 2.500 = 출발 지점 그대로). 그래서 **게임 시간 창(window)** 으로 잰다 —
        // 프레임레이트와 무관하고, 가속 구간(walkSpeedSmoothingRate 6 → 약 0.5초)보다 창이 길다.

        /// <summary>(C) 정지 판정 창의 길이(게임 시간 초).</summary>
        private const float StandoffSettleWindowSeconds = 0.5f;

        /// <summary>(C) 그 창 동안의 이동량이 이 값(월드 유닛) 이하면 벽에 붙어 멈춘 것으로 본다.
        /// 정상 보행이면 같은 창에서 walkSpeed x 배율 x 0.5 = 약 0.94유닛을 간다(약 190배 차이).</summary>
        private const float StandoffSettleWindowEpsilonUnits = 0.005f;

        /// <summary>(C) 가속 구간이 끝나기 전에 "멈췄다"고 오판하지 않기 위한 최소 관측 시간(초).</summary>
        private const float StandoffMinObserveSeconds = 1.5f;

        /// <summary>(C) 관측 상한(초). 2.5유닛을 walkSpeed x 배율(1.875)로 걸으면 1.4초면 닿는다.</summary>
        private const float StandoffMaxObserveSeconds = 10f;

        /// <summary>(C) 요구하는 최소 여유(월드 유닛). R3-M1의 실제 여유는 이 값의 1/10인 0.005였다.</summary>
        private const float RequiredEdgeBandClearanceUnits = 0.05f;

        [UnityTest] public IEnumerator WallStandoffFitsInsideEdgeBand_TileSize16() { yield return AssertWallStandoffFitsInsideEdgeBand(16f); }
        [UnityTest] public IEnumerator WallStandoffFitsInsideEdgeBand_TileSize48() { yield return AssertWallStandoffFitsInsideEdgeBand(48f); }
        [UnityTest] public IEnumerator WallStandoffFitsInsideEdgeBand_TileSize80() { yield return AssertWallStandoffFitsInsideEdgeBand(80f); }
        [UnityTest] public IEnumerator WallStandoffFitsInsideEdgeBand_TileSize128() { yield return AssertWallStandoffFitsInsideEdgeBand(128f); }

        private IEnumerator AssertWallStandoffFitsInsideEdgeBand(float tileSizePoints)
        {
            float drop = DockGeometry.DockDropWorldUnits(tileSizePoints, DockGeometry.DefaultDockThicknessTilePaddingPoints);
            yield return SetUpDockLayout(drop);

            StickmanBlackboard bb = _agent.Blackboard;

            // ── 전제: Dock 물리 계단이 실제로 켜져 있고, 그 옆면이 Dock 발판 경계와 같은 X에 있다.
            //    꺼져 있으면 벽 자체가 없어 이 테스트는 아무 것도 재지 못한다(조용한 통과 방지).
            var step = Object.FindFirstObjectByType<DockPhysicsStep>();
            Assert.IsNotNull(step, $"{LogPrefix} 씬에 DockPhysicsStep이 없습니다 — Main.unity 배선 확인.");
            Assert.IsTrue(step.IsActive,
                $"{LogPrefix} Dock 물리 계단이 꺼져 있습니다(dockPhysicsStepEnabled / Dock 발판 조회 확인) — " +
                "벽이 없으면 이 테스트는 이격을 잴 수 없습니다.");
            Assert.AreEqual(_dockRightWorldX, step.StepBounds.max.x, 0.02f,
                $"{LogPrefix} 계단 옆면({step.StepBounds.max.x:F3})이 Dock 발판 경계({_dockRightWorldX:F3})와 " +
                "다릅니다 — 이 테스트의 전제(벽과 논리 경계가 같은 X)가 깨졌습니다.");

            // ── 안전망 오른쪽 조각 위에 세우고, **배회 AI를 거치지 않고** 왼쪽으로 계속 걷게 한다.
            //    배회 AI를 쓰면 수정이 동작하는 순간 경계 판정에 걸려 벽에 닿기 전에 돌아서므로
            //    이격을 잴 수 없다(= 수정 후에는 측정 불가능한 테스트가 된다).
            bb.MoveBodyToWorld(new Vector2(_dockRightWorldX + 2.5f, _floorTopWorldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = NetRightHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            var scripted = new ScriptedIntentSource { MoveInputX = -1f };
            bb.IntentSource = scripted;

            float windowStartX = bb.Body.position.x;
            float windowTimer = 0f;
            float elapsed = 0f;
            bool settled = false;
            while (elapsed < StandoffMaxObserveSeconds && !settled)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                windowTimer += dt;
                if (windowTimer < StandoffSettleWindowSeconds) continue;

                float moved = Mathf.Abs(bb.Body.position.x - windowStartX);
                settled = elapsed >= StandoffMinObserveSeconds && moved <= StandoffSettleWindowEpsilonUnits;
                windowStartX = bb.Body.position.x;
                windowTimer = 0f;
            }

            float standoff = bb.Body.position.x - _dockRightWorldX;

            // ── ★ 여기서부터가 이 테스트의 핵심: **판정을 쓰는 쪽이 실제로 본 숫자**를 잰다.
            //    블랙보드의 유도 프로퍼티만 단언하면, 소비자(AutoWanderController)가 유도를 그만
            //    읽어도 초록불이 난다 — 실제로 1차 시도에서 그 구멍 때문에 네거티브 컨트롤이
            //    통과해 버렸다(2026-08-30 디버거, Logs/dbg_m1_negctrl_play.xml).
            //    AutoWanderController는 EnterMoving에서 PickDirectionAvoidingEdge를 부르고, 그것이
            //    **양쪽 방향으로 IsNearFootholdEdge를 한 번씩** 호출한다(마지막 호출이 direction=-1
            //    = Dock 쪽). 그래서 컨트롤러를 꽂고 Resting(0.05초)만 넘기면 확률과 무관하게
            //    "벽 쪽 경계 판정"의 실측 표본이 반드시 한 번 남는다 — 결정론적이다.
            _clonedConfig.wanderIdleDurationMin = 0.05f;
            _clonedConfig.wanderIdleDurationMax = 0.05f;
            _clonedConfig.wanderDurationJitterRatio = 0f;
            _clonedConfig.wanderPostIdleWalkChance = 1f;
            _clonedConfig.wanderPostIdleJumpChance = 0f;
            _clonedConfig.hopDownChance = 0f;
            _clonedConfig.ledgeHangChance = 0f;
            _clonedConfig.stepUpChance = 0f;      // 이 테스트는 등반이 아니라 **판정**만 본다.
            _clonedConfig.wanderEdgeJumpAttemptChance = 0f;

            //    ★ 컨트롤러를 IntentSource로 **꽂지 않는다**. IsNearFootholdEdge는 블랙보드의 몸 좌표만
            //    읽으므로 판정에는 배선이 필요 없고, 반대로 꽂아 버리면 스크립트 의도의 미는 힘이 사라져
            //    접촉 복원으로 몸이 벽에서 0.03~0.04유닛 밀려난다(1차 시도에서 실제로 그 드리프트가
            //    "컨트롤러가 잰 잔여거리 ≠ 우리가 잰 이격"을 만들어 tilesize 48/80이 실패했다).
            //    스크립트 의도를 계속 눌러 둔 채로 판정만 시키면 몸이 정확히 벽에 붙어 있는 그 순간의
            //    표본이 나온다 — 재현하려는 상황(벽에 눌려 선 채 되올라가기를 평가해야 하는 순간)과 같다.
            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(FixedWanderSeed));
            float pickWait = 0f;
            while (pickWait < 1f && wander.LastEdgeDirection == 0)
            {
                yield return null;
                pickWait += Time.deltaTime;
                wander.Tick(Time.deltaTime);
            }

            Assert.AreEqual(-1, wander.LastEdgeDirection,
                $"{LogPrefix} 배회 컨트롤러가 왼쪽(Dock 쪽) 경계를 재는 표본을 남기지 않았습니다 " +
                $"(마지막 방향={wander.LastEdgeDirection}, {pickWait:F2}초) — PickDirectionAvoidingEdge가 " +
                "양방향으로 IsNearFootholdEdge를 부른다는 이 테스트의 전제가 바뀌었습니다.");

            float resolvedStop = wander.LastEdgeStopDistanceUsed;
            float clearance = resolvedStop - standoff;

            Debug.Log($"{LogPrefix} (C) tilesize={tileSizePoints:F0}pt 벽 이격 실측 — " +
                $"정지 x={bb.Body.position.x:F4}, Dock 오른쪽 모서리 {_dockRightWorldX:F4} → 이격 {standoff:F4}유닛. " +
                $"몸 물리 반폭 실측 {bb.CharacterPhysicalHalfWidthWorld:F4}, " +
                $"경계 판정 거리 설정 {_clonedConfig.wanderEdgeStopDistance:F3} → **소비자가 실제로 쓴 값** " +
                $"{resolvedStop:F4} (여유 {clearance:F4}, 요구 {RequiredEdgeBandClearanceUnits:F3} 이상). " +
                $"컨트롤러가 잰 잔여거리 {wander.LastRemainingToEdge:F4}, 경계판정={wander.LastEdgeNear}. " +
                $"정착까지 {elapsed:F2}초(정지판정={settled}), 상태={bb.Machine.CurrentStateId}");

            Assert.Less(standoff, 1f,
                $"{LogPrefix} 이격 {standoff:F3}유닛은 너무 큽니다 — 캐릭터가 벽까지 못 걸어왔거나 " +
                $"({elapsed:F2}초 관측, 정지판정={settled}) 도중에 다른 상태로 빠졌습니다" +
                $"(상태={bb.Machine.CurrentStateId}). " +
                "측정이 성립하지 않았습니다.");
            Assert.Greater(standoff, 0f,
                $"{LogPrefix} 이격이 {standoff:F3}유닛(음수)입니다 — 몸이 벽을 파고들었습니다.");

            // ★ 절대 조건 — 유도된 경계 판정 밴드가 벽 이격을 **명확한 여유로** 덮는다.
            Assert.GreaterOrEqual(clearance, RequiredEdgeBandClearanceUnits,
                $"{LogPrefix} tilesize {tileSizePoints:F0}pt에서 경계 판정 거리({resolvedStop:F4})가 " +
                $"벽 이격({standoff:F4})을 {RequiredEdgeBandClearanceUnits:F3} 넘는 여유로 덮지 못합니다" +
                $"(여유 {clearance:F4}). 캐릭터가 Dock 계단 옆면에 붙어 서면 경계 밴드에 들어가지 못해 " +
                "되올라가기를 평가조차 못 합니다 — 사용자가 세 번 신고한 \"Dock 근처에서 멈춰 있음\"입니다.");

            // ★★ 소비자 수준의 절대 조건 — 벽에 눌려 선 그 자리에서 배회 AI가 실제로 "경계"라고
            //     판정하는가. R3-M1의 증상은 정확히 이것이 false였던 것이다(밴드가 도달 불가능).
            Assert.IsTrue(wander.LastEdgeNear,
                $"{LogPrefix} 벽에 눌려 선 자리(잔여 {wander.LastRemainingToEdge:F4})에서 배회 AI가 " +
                $"'경계'로 판정하지 않습니다(판정 거리 {resolvedStop:F4}) — 되올라가기 추첨 자체가 " +
                "돌지 않으므로 캐릭터는 이 걷기 구간이 끝날 때까지 벽에 붙어 있습니다(2026-08-30 R3-M1).");

            // 컨트롤러가 잰 잔여 거리가 우리가 잰 이격과 같아야 한다(같은 것을 재고 있다는 확인).
            Assert.AreEqual(standoff, wander.LastRemainingToEdge, 0.02f,
                $"{LogPrefix} 컨트롤러가 잰 잔여 거리({wander.LastRemainingToEdge:F4})가 이 테스트가 잰 " +
                $"이격({standoff:F4})과 다릅니다 — 서로 다른 경계를 재고 있습니다(측정 무효).");

            // ★ 실측 반폭이 실제로 갱신되고 있는가(0이면 폴백으로 조용히 넘어간다 = 유도가 죽은 것).
            Assert.Greater(bb.CharacterPhysicalHalfWidthWorld, 0f,
                $"{LogPrefix} 몸 물리 반폭 실측이 0입니다 — StickmanAgent.TickPhysicalHalfWidth가 " +
                "콜라이더를 못 찾았다는 뜻이며, 그러면 경계 판정 거리가 설정 배율 폴백으로만 유도됩니다.");
        }

        // ============================================================================
        // 공통 준비 — 실제 앱과 같은 합성 발판 핸들로 Dock + 안전망 두 조각을 재현한다.
        // ============================================================================

        private IEnumerator SetUpDockLayout(float dropUnits)
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");
            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalPoller = bb.FootholdPoller;
            _originalIntent = bb.IntentSource;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            // Dock 물리 계단은 논리 낙차를 바꾸지 않지만(발판 단일 소스에서 파생), 이 테스트는 순수하게
            // "논리 낙차 × 되올라가기 판정"만 보므로 켜 둔 채로 둔다 — 실제 배포 구성 그대로다.

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 전제 실패 — 씬에 PhysicsGround가 없습니다.");
            _floorTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y;
            _dockTopWorldY = _floorTopWorldY + dropUnits;

            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            float floorTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _floorTopWorldY), _clonedConfig, out _).y;
            float dockTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _dockTopWorldY), _clonedConfig, out _).y;

            Assert.Greater(dockTopOsY, 0f, $"{LogPrefix} 준비 실패 — 낙차 {dropUnits:F3}유닛이 화면 위로 벗어납니다.");
            Assert.Less(floorTopOsY, h, $"{LogPrefix} 준비 실패 — 안전망이 화면 아래로 벗어납니다.");

            // Dock 가로 구간은 화면 중앙 40%(실제 Dock의 가운데 정렬 배치와 같은 관례).
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

            Debug.Log($"{LogPrefix} 준비 — 안전망 상단 월드Y={_floorTopWorldY:F4}(OS {floorTopOsY:F1}), " +
                $"Dock 상단 월드Y={_dockTopWorldY:F4}(OS {dockTopOsY:F1}), 낙차={dropUnits:F4}유닛, " +
                $"Dock 월드 X {_dockLeftWorldX:F3}~{_dockRightWorldX:F3}, 신장={bb.CharacterHeightWorld:F3}");

            // 발판 폴러가 최소 한 번은 돌아 캐시가 채워지도록 한 프레임 이상 기다린다.
            yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }
}
