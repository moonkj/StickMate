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
    /// ★★ 사용자 신고 회귀 잠금(2026-08-30, 디버거):
    /// **"갑자기 독 아래로 떨어지면서 관절이 이상하게 꺾임"**
    ///
    /// ============================================================================
    /// 실측으로 확정한 인과 (추측 아님 — 아래 두 증거가 서로를 확인한다)
    /// ============================================================================
    /// [증거 1 — 실제 앱 Player.log]
    ///   `[착지충격] 충돌 충격량=10.01(랙돌 임계 8.0), 상태=BattleMinigame, 접촉 1개(최저 y=-11.881),
    ///    발 y=-11.886, 차단스위치=True -> 외력으로 판정, 임계값 초과 -> RAGDOLL 전이.`
    ///   그 뒤로 `[RagdollRig] RAGDOLL 관절 제한 적용` -> `[발판변경] -2 -> 0 (Fall 진입 — 공중)`
    ///   -> `[캐릭터구조] 6초 이상 착지하지 못해 강제 복귀` 가 **6회 반복**됐다(복귀 지점 전부
    ///   (0.000,-10.167) = Dock 상단, 랙돌 시작점 전부 Dock 위).
    ///
    /// [증거 2 — 이 파일의 PlayMode 재현(수정 전 실측 전이 추적)]
    ///   Dock 위 Idle -> Attack 진입:
    ///     `Idle->Attack 몸=(0.000,-10.167)` -> `Attack->Ragdoll(강제) 몸=(0.000,-11.886)`
    ///     -> `Ragdoll->Getup` -> `Getup->Idle` -> `Idle->Fall` -> (41,000프레임 = 6초 뒤) 강제 복귀.
    ///   Dock 확장으로 캐릭터가 Dock 구멍에 들어간 경우:
    ///     `Idle->Fall 몸=(12.800,-11.801)` -> (6초 고착) -> `Fall->Idle(강제) 몸=(0.000,-10.167)`
    ///     = 화면 가로 중앙으로 순간이동.
    ///
    /// 근본 원인 두 가지:
    ///  (1) **접지 유지(GroundedTick) 호출이 상태마다 흩어져 있었고 Attack/Getup/BattleMinigame에
    ///      빠져 있었다.** Dock/창 상단은 논리 발판일 뿐 물리 콜라이더가 없으므로, 그런 상태에
    ///      들어가는 순간 자유낙하해 화면 최하단 물리 바닥에 전속력으로 부딪힌다.
    ///  (2) **그 충돌을 "내 착지"로 걸러내는 차단막이 상태 허용목록(Fall/Jump/LandingCrouch/
    ///      ThrowTumble)이었다.** 그래서 (1)의 상태들은 차단막 밖이라 그대로 RAGDOLL이 됐다.
    ///
    /// 그리고 그 결과 캐릭터가 도착하는 곳(Dock 가로 구간의 화면 최하단)은 "물리적으로는 떠받쳐지지만
    /// 논리적으로는 접지하지 않는" 사각지대라, 착지가 영원히 확정되지 않고 6초 강제 복귀까지 Dock
    /// 아래에 널브러져 있었다 — 사용자가 본 그림 그대로다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  T1 Dock 위에서 GroundedTick을 부르지 않는 상태(Attack)에 들어가도 **RAGDOLL이 되지 않는다**.
    ///  T1n (네거티브) 접지 안전망 스위치를 끄면 T1이 **실제로 깨진다**(= 이 테스트가 버그를 잡는다).
    ///  T2 차단막이 상태와 무관하게 동작한다 — Getup 중 물리 바닥 충돌도 RAGDOLL이 아니다.
    ///  T3 Dock 확장으로 사각지대에 갇혀도 **6초가 아니라 1초 안에** 회복하고, 가로 순간이동이 없다.
    ///  T3n (네거티브) 사각지대 회수 스위치를 끄면 T3이 **실제로 깨진다**.
    ///  T4 Dock 위에서 진짜 RAGDOLL(외력)이 나면 여전히 랙돌이 된다 — 차단막이 과잉 차단하지 않는다.
    ///
    /// 배치는 실제 데스크톱과 동일하게 만든다: **씬의 물리 바닥(PhysicsGround) 상단 Y를 실측해**
    /// 그 높이에 논리 안전망 두 조각을 놓고, 그보다 1.6375유닛 위에 Dock 발판을 놓되 Dock 가로
    /// 구간에는 논리 발판 구멍을 남긴다(= 실제 앱의 Dock 단차/구멍 그대로).
    /// StickConfig는 복제본을 꽂아 원본 자산을 절대 건드리지 않는다(CLAUDE.md 불변 원칙 3).
    /// </summary>
    public sealed class DockSinkholeRegressionTests
    {
        private const string LogPrefix = "[SINKHOLE-TEST]";

        /// <summary>실제 앱의 합성 발판 핸들과 같은 값을 쓴다(FallbackPlatformWindowService 참고).</summary>
        private const long DockHandle = -2L;
        private const long NetLeftHandle = -1L;
        private const long NetRightHandle = -3L;

        /// <summary>★ Dock 상단 → 바닥 안전망 상단 낙차(월드 유닛). **하드코딩하지 않는다** —
        /// Core/DockGeometry.cs가 (tilesize + dockThicknessTilePaddingPoints − BottomSafetyNetInsetPoints)를
        /// 월드로 환산해 주는 단일 소스다(이 개발 머신 tilesize=49 → 67pt → 1.63747유닛).
        /// 2026-08-30 횡단 리뷰 M1: 이 값이 파일마다 0.855(안전망이 40pt 위였던 시절의 화석) / 1.6375로
        /// 갈라져 있었고, 그 탓에 배율 불변식 테스트가 실제 시스템이 아니라 자기 상수를 지키고 있었다.</summary>
        private static readonly float DockDropUnits = DockGeometry.ReferenceDockDropWorldUnits;

        private const float SettleWaitSeconds = 2.0f;

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
        private float _dockTopWorldY;
        private float _floorTopWorldY;
        private float _dockTopOsY;
        private float _floorTopOsY;

        private readonly List<string> _trace = new List<string>();
        private int _ragdollEntries;

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
            if (e.To == StickmanStateId.Ragdoll) _ragdollEntries++;
        }

        private string Trace() => _trace.Count == 0 ? "(전이 없음)" : string.Join("\n    ", _trace);

        /// <summary>
        /// 실제 배치 재현 — 물리 바닥 상단 == 논리 안전망 상단, Dock은 그보다 DockDropUnits 위,
        /// Dock 가로 구간에는 논리 발판 구멍.
        /// </summary>
        private IEnumerator SetUpDockLayout(float dockLeftFrac, float dockRightFrac)
        {
            _trace.Clear();
            _ragdollEntries = 0;

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

            // ★ 2026-08-30 (리더 지시 1항 이후) — 이 파일은 **Dock 물리 계단을 끈 상태**에서
            // 상태머신 쪽 안전망(접지 유지 / 사각지대 회수)만 단독으로 검증한다.
            // 근거는 T1c를 따로 둔 것과 같다: 물리 계단이 켜져 있으면 Dock 구간의 자유낙하 자체가
            // 물리적으로 일어나지 않아, 여기 있는 안전망들이 **한 번도 실행되지 않은 채 통과**한다
            // (= "돌아갈 것 같다"짜리 테스트가 된다). 이 방어선들은 계단이 어떤 이유로든 없을 때
            // (Dock 자동 숨김 / 세로 Dock / 비-macOS / 스위치 off) 여전히 유일한 방어선이므로
            // 독립적으로 잠가 둬야 한다.
            // 물리 계단 자체의 효과(= 낙하가 애초에 안 생긴다)는 DockPhysicsStepTests가 잠근다.
            _clonedConfig.dockPhysicsStepEnabled = false;

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 전제 실패 — 씬에 PhysicsGround가 없습니다. " +
                "이 테스트는 '물리 바닥은 있는데 논리 발판이 없는 구간'을 재현하므로 그 바닥이 필수다.");
            _floorTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y;
            _dockTopWorldY = _floorTopWorldY + DockDropUnits;

            Camera cam = bb.MainCamera;
            _floorTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _floorTopWorldY), _clonedConfig, out _).y;
            _dockTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _dockTopWorldY), _clonedConfig, out _).y;

            _service = new TestFootholdService();
            ApplyDockSpan(dockLeftFrac, dockRightFrac);
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.IntentSource = new StillIntentSource();

            StickmanEventBus.StateTransitioned += OnTransition;

            Debug.Log($"{LogPrefix} 준비 — 물리바닥 상단 월드Y={_floorTopWorldY:F4}(OS {_floorTopOsY:F1}), " +
                $"Dock 상단 월드Y={_dockTopWorldY:F4}(OS {_dockTopOsY:F1}), 낙차={DockDropUnits:F4}유닛, " +
                $"신장={bb.CharacterHeightWorld:F3}, 랙돌임계={_clonedConfig.ragdollForceThreshold:F1}, " +
                $"차단막={_clonedConfig.landingImpactRagdollShield}, 접지안전망={_clonedConfig.groundKeepingSafetyNetEnabled}, " +
                $"사각지대회수={_clonedConfig.sinkholeLiftRecoveryEnabled}, " +
                $"물리계단={_clonedConfig.dockPhysicsStepEnabled}(이 파일은 일부러 끈다).");
        }

        private void ApplyDockSpan(float leftFrac, float rightFrac)
        {
            float w = Screen.width;
            float h = Screen.height;
            float leftOs = w * leftFrac;
            float rightOs = w * rightFrac;
            _service.Footholds.Clear();
            _service.Footholds.Add(new PlatformFoothold(DockHandle,
                new Rect(leftOs, _dockTopOsY, rightOs - leftOs, h - _dockTopOsY), true));
            _service.Footholds.Add(new PlatformFoothold(NetLeftHandle,
                new Rect(0f, _floorTopOsY, leftOs, h - _floorTopOsY), false));
            _service.Footholds.Add(new PlatformFoothold(NetRightHandle,
                new Rect(rightOs, _floorTopOsY, w - rightOs, h - _floorTopOsY), false));
        }

        private float WorldXAtScreenFraction(float frac, float osY)
        {
            return ScreenCoordinateConverter.OsScreenToWorld(_agent.Blackboard.MainCamera,
                new Vector2(Screen.width * frac, osY), 10f, _clonedConfig).x;
        }

        private void Place(float worldX, float worldY, long handle, StickmanStateId state)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            bb.MoveBodyToWorld(new Vector2(worldX, worldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = handle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(state, isForcedInterrupt: true);
        }

        /// <summary>seconds초 동안 관찰하며 최저 Y와 RAGDOLL 프레임 수를 센다.</summary>
        private IEnumerator Observe(float seconds, System.Action<float, int> onDone)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            float t = 0f;
            float lowestY = float.MaxValue;
            int ragdollFrames = 0;
            while (t < seconds)
            {
                t += Time.deltaTime;
                lowestY = Mathf.Min(lowestY, bb.Body.position.y);
                if (bb.Machine.CurrentStateId == StickmanStateId.Ragdoll) ragdollFrames++;
                yield return null;
            }
            onDone(lowestY, ragdollFrames);
        }

        // ============================================================================
        // T1 — Dock 위에서 접지 유지를 안 부르는 상태(Attack)에 들어가도 랙돌이 되지 않는다
        // ============================================================================

        [UnityTest]
        public IEnumerator T1_AttackOnDockDoesNotRagdollOrSinkBelowDock()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            yield return RunNoGroundedTickStateScenario(StickmanStateId.Attack, expectRagdoll: false);
        }

        // ============================================================================
        // T1n — 네거티브 컨트롤: 안전망을 끄면 위 조건이 실제로 깨진다
        //       (= 이 테스트가 "돌아갈 것 같다"가 아니라 진짜 버그를 잡는다는 증거)
        // ============================================================================

        [UnityTest]
        public IEnumerator T1n_NegativeControl_WithoutSafetyNetAttackFallsBelowDock()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            _clonedConfig.groundKeepingSafetyNetEnabled = false;   // 수정을 되돌린다
            _clonedConfig.landingImpactRagdollShield = false;      // 2026-08-29 차단막도 함께 되돌린다
            yield return RunNoGroundedTickStateScenario(StickmanStateId.Attack, expectRagdoll: true);
        }

        private IEnumerator RunNoGroundedTickStateScenario(StickmanStateId state, bool expectRagdoll)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = WorldXAtScreenFraction(0.5f, _dockTopOsY);
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(StickmanStateId.Idle, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — Dock 위에서 Idle을 유지하지 못했습니다.");
            Assert.AreEqual(DockHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 전제 실패 — Dock 발판을 딛고 있지 않습니다.");

            _trace.Clear();
            _ragdollEntries = 0;
            bb.Machine.ChangeState(state);

            float lowestY = 0f;
            int ragdollFrames = 0;
            yield return Observe(3.0f, (low, frames) => { lowestY = low; ragdollFrames = frames; });

            Debug.Log($"{LogPrefix} [{state} / 랙돌기대={expectRagdoll}] 결과 — 최저Y={lowestY:F3}(Dock 상단 {_dockTopWorldY:F3}, " +
                $"물리바닥 {_floorTopWorldY:F3}), RAGDOLL진입={_ragdollEntries}회/{ragdollFrames}프레임, " +
                $"최종상태={bb.Machine.CurrentStateId}\n    전이추적:\n    {Trace()}");

            // Dock 상단보다 이만큼 아래로 내려가면 "Dock 아래로 떨어졌다"로 본다.
            // 낙차의 절반이면 접지 스냅의 미세 진동(수 cm)과 명백히 구분된다.
            float belowDockThreshold = _dockTopWorldY - DockDropUnits * 0.5f;

            if (expectRagdoll)
            {
                Assert.Greater(_ragdollEntries, 0,
                    $"{LogPrefix} 네거티브 컨트롤 실패 — 안전망/차단막을 껐는데도 RAGDOLL이 발생하지 않았습니다. " +
                    "이 테스트가 실제로 버그를 잡는다는 증거가 성립하지 않으므로 시나리오를 다시 설계해야 합니다.\n" +
                    $"    전이추적:\n    {Trace()}");
                Assert.Less(lowestY, belowDockThreshold,
                    $"{LogPrefix} 네거티브 컨트롤 실패 — Dock 아래로 떨어지지도 않았습니다(최저Y={lowestY:F3}).");
                yield break;
            }

            Assert.AreEqual(0, _ragdollEntries,
                $"{LogPrefix} 회귀 — Dock 위에서 {state} 상태에 들어갔을 뿐인데 RAGDOLL이 됐습니다" +
                $"({_ragdollEntries}회). 사용자 신고 '갑자기 독 아래로 떨어지면서 관절이 이상하게 꺾임'의 " +
                "직접 재현입니다. 원인 후보: (a) StickmanBlackboard.TickGroundKeepingSafetyNet이 이 상태를 " +
                "IsGroundKeepingSelfManaged로 잘못 제외했거나 StickmanAgent.Update()에서 호출이 빠졌다, " +
                "(b) RagdollImpactResolver.IsOwnLandingContact가 다시 상태 허용목록으로 되돌아갔다.\n" +
                $"    전이추적:\n    {Trace()}");
            Assert.GreaterOrEqual(lowestY, belowDockThreshold,
                $"{LogPrefix} 회귀 — {state} 상태에서 캐릭터가 Dock 아래로 떨어졌습니다" +
                $"(최저Y={lowestY:F3} < 기준 {belowDockThreshold:F3}). 논리 발판(Dock/창 상단)에는 물리 " +
                "콜라이더가 없으므로, 접지 유지를 부르지 않는 상태는 그 자리에서 자유낙하합니다.\n" +
                $"    전이추적:\n    {Trace()}");
        }

        // ============================================================================
        // T1b — 실제 앱 로그에 남은 바로 그 상태(BattleMinigame)로도 같은 조건을 잠근다.
        //       `[착지충격] ... 상태=BattleMinigame ... -> RAGDOLL 전이`가 사용자 환경에서 실제로
        //       찍힌 유일한 "원인이 로그로 확인된" 사례라, 그 상태를 이름으로 못박아 둔다.
        // ============================================================================

        [UnityTest]
        public IEnumerator T1b_BattleMinigameOnDockDoesNotRagdoll()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            yield return RunNoGroundedTickStateScenario(StickmanStateId.BattleMinigame, expectRagdoll: false);
        }

        // ============================================================================
        // T1c — 두 수정을 **분리해서** 잠근다: 접지 안전망만 끄고 차단막은 켜 둔다.
        //   이렇게 하면 캐릭터가 실제로 Dock 아래로 자유낙하해 **물리 바닥과 진짜로 충돌**하므로,
        //   RagdollImpactResolver.IsOwnLandingContact의 새 판정("부딪힌 상대가 Dynamic 바디가 아니면
        //   내 착지")이 그 충돌 경로에서 실제로 동작하는지 검사할 수 있다.
        //   (T1/T1b/T2는 안전망이 낙하 자체를 막아버려 이 경로를 지나가지 않는다 — 그래서 별도 항목이
        //    필요하다. 안 그러면 "두 번째 수정은 한 번도 실행되지 않은 채 통과"가 된다.)
        // ============================================================================

        [UnityTest]
        public IEnumerator T1c_ShieldAloneStopsRagdollOnRealFloorImpact()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            _clonedConfig.groundKeepingSafetyNetEnabled = false; // 낙하는 일부러 허용한다
            _clonedConfig.landingImpactRagdollShield = true;     // 차단막만으로 막아야 한다

            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = WorldXAtScreenFraction(0.5f, _dockTopOsY);
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);

            _trace.Clear();
            _ragdollEntries = 0;
            bb.Machine.ChangeState(StickmanStateId.Attack);

            float lowestY = 0f;
            int ragdollFrames = 0;
            yield return Observe(3.0f, (low, frames) => { lowestY = low; ragdollFrames = frames; });

            Debug.Log($"{LogPrefix} [차단막 단독] 결과 — 최저Y={lowestY:F3}(물리바닥 {_floorTopWorldY:F3}), " +
                $"RAGDOLL진입={_ragdollEntries}회/{ragdollFrames}프레임, 최종상태={bb.Machine.CurrentStateId}\n" +
                $"    전이추적:\n    {Trace()}");

            // 전제: 이 시나리오는 실제로 물리 바닥까지 떨어져야 의미가 있다.
            Assert.Less(lowestY, _floorTopWorldY + 0.2f,
                $"{LogPrefix} 전제 실패 — 안전망을 껐는데도 물리 바닥까지 떨어지지 않았습니다" +
                $"(최저Y={lowestY:F3}). 차단막 경로를 지나가지 않았으므로 이 테스트는 무의미합니다.\n" +
                $"    전이추적:\n    {Trace()}");

            Assert.AreEqual(0, _ragdollEntries,
                $"{LogPrefix} 회귀 — 자유낙하로 물리 바닥에 부딪혔는데 RAGDOLL이 됐습니다. " +
                "RagdollImpactResolver.IsOwnLandingContact가 다시 상태 허용목록으로 되돌아갔거나, " +
                "Collision2D.rigidbody(= 부딪힌 **상대**의 바디)를 자기 바디로 잘못 읽고 있습니다.\n" +
                $"    전이추적:\n    {Trace()}");
        }

        // ============================================================================
        // T2 — 차단막이 상태 허용목록이 아니라 "부딪힌 대상"으로 판정한다 (Getup 경로)
        // ============================================================================

        [UnityTest]
        public IEnumerator T2_GetupOnDockDoesNotRagdoll()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            yield return RunNoGroundedTickStateScenario(StickmanStateId.Getup, expectRagdoll: false);
        }

        // ============================================================================
        // T3 — Dock 가로 구간이 넓어져 사각지대에 갇혀도 6초가 아니라 즉시 회복하고,
        //      **가로 순간이동이 없다**(예전 RescueToSafeGround는 화면 가로 중앙으로 옮겼다)
        // ============================================================================

        [UnityTest]
        public IEnumerator T3_SinkholeRecoversFastAndWithoutHorizontalTeleport()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            yield return RunSinkholeScenario(expectSlowCenterRescue: false);
        }

        // ============================================================================
        // T3n — 네거티브 컨트롤: 사각지대 회수를 끄면 예전 거동(6초 + 가로 순간이동)이 돌아온다
        // ============================================================================

        [UnityTest]
        public IEnumerator T3n_NegativeControl_WithoutLiftRecoveryStaysStuckAndTeleports()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            _clonedConfig.sinkholeLiftRecoveryEnabled = false;
            yield return RunSinkholeScenario(expectSlowCenterRescue: true);
        }

        private IEnumerator RunSinkholeScenario(bool expectSlowCenterRescue)
        {
            StickmanBlackboard bb = _agent.Blackboard;

            // 안전망 오른쪽 조각 위(= 물리 바닥과 같은 높이)에 세운다.
            float standX = WorldXAtScreenFraction(0.92f, _floorTopOsY);
            Place(standX, _floorTopWorldY, NetRightHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(StickmanStateId.Idle, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — 안전망 위에서 Idle을 유지하지 못했습니다.");

            // Dock 가로 구간이 넓어져 캐릭터를 삼킨다(실제로 일어난다 — 앱을 켜고 끄면 Dock 타일 수가
            // 변해 실측 폭이 x201~1312 <-> x174~1338로 바뀌는 것을 로그로 확인했다).
            _trace.Clear();
            ApplyDockSpan(0.13f, 0.97f);
            bb.FootholdPoller.PollImmediately();
            Debug.Log($"{LogPrefix} Dock 가로 구간 확장 — 캐릭터(x={standX:F3})가 Dock 구멍 안으로 들어갔습니다.");

            // ① 먼저 "사각지대에 빠졌다"(Fall 진입)를 확인한다. 발판 상실은 fallGraceDuration(0.1초)
            //    유예 뒤에 확정되므로, 이 대기 없이 곧바로 시간을 재면 아직 Idle이라 0초가 나온다
            //    (첫 실행에서 실제로 그렇게 잘못 쟀다 — 그 오류를 여기 남겨 다시 밟지 않게 한다).
            float toFall = 0f;
            while (toFall < 2f && bb.Machine.CurrentStateId != StickmanStateId.Fall)
            {
                toFall += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(StickmanStateId.Fall, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — Dock 확장으로 발판을 잃었는데도 {toFall:F2}초 안에 Fall로 " +
                "전이하지 않았습니다(사각지대 재현 자체가 성립하지 않음).\n" +
                $"    전이추적:\n    {Trace()}");

            // ② 사각지대에서 회복(Idle/Walk 복귀)하기까지 걸린 시간을 잰다. 예전 강제 복귀는 6초다.
            float elapsed = 0f;
            const float MaxWait = 8f;
            while (elapsed < MaxWait && bb.Machine.CurrentStateId != StickmanStateId.Idle
                   && bb.Machine.CurrentStateId != StickmanStateId.Walk)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            // 회복 직후 잠깐 안정화.
            yield return new WaitForSeconds(0.3f);

            float finalX = bb.Body.position.x;
            float finalY = bb.Body.position.y;
            float horizontalJump = Mathf.Abs(finalX - standX);
            Debug.Log($"{LogPrefix} [사각지대 / 느린복귀기대={expectSlowCenterRescue}] 결과 — 회복까지 {elapsed:F2}초, " +
                $"최종 몸=({finalX:F3},{finalY:F3}), 가로 이동={horizontalJump:F3}유닛, " +
                $"최종상태={bb.Machine.CurrentStateId}\n    전이추적:\n    {Trace()}");

            if (expectSlowCenterRescue)
            {
                Assert.Greater(elapsed, 3f,
                    $"{LogPrefix} 네거티브 컨트롤 실패 — 회수를 껐는데도 {elapsed:F2}초 만에 회복했습니다. " +
                    "스위치가 실제로 거동을 가르지 못하므로 이 테스트는 아무 것도 증명하지 못합니다.\n" +
                    $"    전이추적:\n    {Trace()}");
                Assert.Greater(horizontalJump, 1f,
                    $"{LogPrefix} 네거티브 컨트롤 실패 — 예전 경로(RescueToSafeGround)는 화면 가로 중앙으로 " +
                    $"옮기므로 가로 이동이 커야 하는데 {horizontalJump:F3}유닛이었습니다.");
                yield break;
            }

            Assert.Less(elapsed, 2f,
                $"{LogPrefix} 회귀 — Dock 사각지대에서 회복하는 데 {elapsed:F2}초가 걸렸습니다. " +
                "그 동안 캐릭터는 Dock 아래에 박혀 있습니다(사용자 신고 '갑자기 독 아래로 떨어짐'). " +
                "StickConfig.sinkholeLiftRecoveryEnabled / StickmanBlackboard.TryLiftOutOfSinkhole 회귀 의심.\n" +
                $"    전이추적:\n    {Trace()}");
            Assert.Less(horizontalJump, 0.5f,
                $"{LogPrefix} 회귀 — 사각지대 회수가 캐릭터를 가로로 {horizontalJump:F3}유닛 순간이동시켰습니다. " +
                "이 프로젝트 사용자는 순간이동성 아티팩트에 반복적으로 민감했고, 회수는 '그 자리에서 " +
                "바로 위 발판으로'가 계약입니다.");
            Assert.AreEqual(_dockTopWorldY, finalY, 0.05f,
                $"{LogPrefix} 회귀 — 회수 후 캐릭터가 Dock 상단({_dockTopWorldY:F3})이 아니라 " +
                $"{finalY:F3}에 있습니다.");
        }

        // ============================================================================
        // T5 — ★ 라이벌도 같은 보호를 받는가 (리더 지적: 사용자가 **"한 명이"** 독 아래에서
        //      계속 쓰러진다고 했다 = 두 캐릭터 중 하나만 고쳐졌을 가능성)
        //
        //      Interaction/RivalStickmanAgent는 플레이어(Core/StickmanAgent)와 **완전히 별개의**
        //      Update 루프를 갖고 있고, 거기에는 플레이어가 매 프레임 하는 세 가지
        //      (TickGroundKeepingSafetyNet / TickPose / EnforceScreenBoundsAndRescue)가 **하나도**
        //      없었다. 그래서 대결 중 1.2초마다 들어가는 AttackState(0.4초, 접지 스냅 없음)에서
        //      라이벌만 Dock 아래로 가라앉고, 6초 강제 복귀조차 없어 영영 못 나왔다.
        //      라이벌에게는 OnCollisionEnter2D 자체가 없어(RagdollLimbImpactRelay는 부모에
        //      StickmanAgent가 없어 무동작) 랙돌은 플레이어의 반격으로만 생긴다 — 그래서 증상이
        //      "가라앉은 채 계속 쓰러진다"가 된다.
        // ============================================================================

        [UnityTest]
        public IEnumerator T5_RivalGetsSameProtectionOnDock()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            yield return RunRivalAttackScenario(disableFixes: false);
        }

        // ============================================================================
        // T5n — 네거티브 컨트롤: 라이벌 쪽 스위치를 끄면 실제로 Dock 아래에 가라앉아 못 나온다
        // ============================================================================

        [UnityTest]
        public IEnumerator T5n_NegativeControl_RivalSinksBelowDockWithoutFixes()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            yield return RunRivalAttackScenario(disableFixes: true);
        }

        private IEnumerator RunRivalAttackScenario(bool disableFixes)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = WorldXAtScreenFraction(0.5f, _dockTopOsY);
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.4f);

            var rival = Object.FindFirstObjectByType<Interaction.RivalStickmanAgent>(FindObjectsInactive.Include);
            Assert.IsNotNull(rival, $"{LogPrefix} 전제 실패 — 씬에서 RivalStickmanAgent를 찾지 못했습니다 " +
                "(SceneBootstrapper.CreateRivalStickman 배선 확인).");

            // 플레이어에게서 충분히 떨어진 Dock 위에 소환한다 — 근접 전투(rivalAttackRange 1.0)가
            // 끼어들면 "공격 중 자유낙하"와 "얻어맞아서 랙돌"을 구분할 수 없다.
            float rivalX = dockCenterX + 8f;
            rival.BeginDuel(_agent, new Vector2(rivalX, _dockTopWorldY));
            yield return null;
            Assert.IsNotNull(rival.Blackboard, $"{LogPrefix} 전제 실패 — 라이벌 블랙보드가 만들어지지 않았습니다.");

            // 라이벌은 자기 StickConfig(원본 자산)를 들고 있으므로, 테스트에서는 복제본으로 갈아끼운다
            // (원본 자산 불변 — CLAUDE.md 불변 원칙 3).
            StickConfig rivalCfg = Object.Instantiate(_clonedConfig);
            if (disableFixes)
            {
                rivalCfg.groundKeepingSafetyNetEnabled = false;
                rivalCfg.sinkholeLiftRecoveryEnabled = false;
            }
            rival.Blackboard.Config = rivalCfg;

            Rigidbody2D rivalBody = rival.Blackboard.Body;
            Assert.IsNotNull(rivalBody, $"{LogPrefix} 전제 실패 — 라이벌 Rigidbody2D가 없습니다.");
            rival.Blackboard.MoveBodyToWorld(new Vector2(rivalX, _dockTopWorldY));
            rivalBody.linearVelocity = Vector2.zero;
            rival.Blackboard.CurrentFootholdHandle = DockHandle;
            rival.Blackboard.ResetGroundLossTimer();
            rival.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(_dockTopWorldY, rivalBody.position.y, 0.1f,
                $"{LogPrefix} 전제 실패 — 라이벌이 Dock 위에 서지 못했습니다(y={rivalBody.position.y:F3}).");

            // 대결 중 실제로 반복되는 그 상태(AttackState — 접지 스냅을 부르지 않는다)에 넣는다.
            rival.Blackboard.Machine.ChangeState(StickmanStateId.Attack);

            float t = 0f;
            float lowestY = float.MaxValue;
            while (t < 3f)
            {
                t += Time.deltaTime;
                lowestY = Mathf.Min(lowestY, rivalBody.position.y);
                yield return null;
            }

            float finalY = rivalBody.position.y;
            StickmanStateId finalState = rival.Blackboard.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} [라이벌 / 수정무력화={disableFixes}] 결과 — 최저Y={lowestY:F3}, 최종Y={finalY:F3}, " +
                $"최종상태={finalState}(Dock 상단 {_dockTopWorldY:F3}, 물리바닥 {_floorTopWorldY:F3}).");

            float belowDockThreshold = _dockTopWorldY - DockDropUnits * 0.5f;
            Object.DestroyImmediate(rivalCfg);

            if (disableFixes)
            {
                Assert.Less(lowestY, belowDockThreshold,
                    $"{LogPrefix} 네거티브 컨트롤 실패 — 라이벌 쪽 수정을 껐는데도 Dock 아래로 " +
                    $"가라앉지 않았습니다(최저Y={lowestY:F3}). 이 테스트가 실제로 버그를 잡는다는 증거가 " +
                    "성립하지 않습니다.");
                Assert.Less(finalY, belowDockThreshold,
                    $"{LogPrefix} 네거티브 컨트롤 실패 — 가라앉았다가 스스로 회복했습니다" +
                    $"(최종Y={finalY:F3}). 예전 라이벌에는 회복 경로 자체가 없어야 합니다.");
                yield break;
            }

            Assert.GreaterOrEqual(lowestY, belowDockThreshold,
                $"{LogPrefix} 회귀 — 라이벌이 Attack 중에 Dock 아래로 가라앉았습니다(최저Y={lowestY:F3}). " +
                "RivalStickmanAgent.Update()에서 _blackboard.TickGroundKeepingSafetyNet(dt) 호출이 " +
                "빠졌는지 확인하세요. 플레이어만 고치고 라이벌을 빠뜨리면 사용자에게는 " +
                "'한 명이 독 아래에서 계속 쓰러짐'으로 보입니다.");
            Assert.AreEqual(_dockTopWorldY, finalY, 0.1f,
                $"{LogPrefix} 회귀 — 라이벌이 Dock 위로 돌아오지 못했습니다(최종Y={finalY:F3}, " +
                $"상태={finalState}). RivalStickmanAgent.Update()의 " +
                "_blackboard.EnforceScreenBoundsAndRescue(dt) 호출 확인.");
        }

        // ============================================================================
        // T4 — 차단막이 과잉 차단하지 않는다: 진짜 외력은 여전히 RAGDOLL이 된다
        //      (수정이 "랙돌을 통째로 없애버린 것"이 아님을 증명 — 반대 방향 잠금)
        // ============================================================================

        [UnityTest]
        public IEnumerator T4_RealExternalImpactStillRagdolls()
        {
            yield return SetUpDockLayout(0.13f, 0.885f);
            StickmanBlackboard bb = _agent.Blackboard;
            float dockCenterX = WorldXAtScreenFraction(0.5f, _dockTopOsY);
            Place(dockCenterX, _dockTopWorldY, DockHandle, StickmanStateId.Idle);
            yield return new WaitForSeconds(0.5f);

            _trace.Clear();
            _ragdollEntries = 0;
            // 라이벌 타격/던지기와 같은 **직접 호출 경로**(차단막을 거치지 않는다).
            _agent.ReportExternalImpact(_clonedConfig.ragdollForceThreshold * 2f);
            yield return null;

            Assert.AreEqual(StickmanStateId.Ragdoll, bb.Machine.CurrentStateId,
                $"{LogPrefix} 회귀 — 임계값의 2배 외력을 넣었는데도 RAGDOLL이 되지 않았습니다. " +
                "이번 수정이 랙돌 경로 자체를 죽였다는 뜻입니다(아키텍처 0절: 피격/던짐은 RAGDOLL).\n" +
                $"    전이추적:\n    {Trace()}");
            Assert.AreEqual(1, _ragdollEntries, $"{LogPrefix} RAGDOLL 진입이 정확히 1회여야 합니다.");
        }
    }
}
