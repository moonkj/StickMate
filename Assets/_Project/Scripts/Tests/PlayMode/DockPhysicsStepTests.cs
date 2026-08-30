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
    /// ★★ Dock **물리 계단** 회귀 잠금 (2026-08-30, 리더 지시 1항 "Dock 사각지대 근본 제거").
    ///
    /// ============================================================================
    /// 무엇이 달라졌는가 — 대증요법에서 원인 제거로
    /// ============================================================================
    /// 직전 라운드(DockSinkholeRegressionTests)는 Dock 사각지대에 **빠진 뒤** 0.35초 만에 회수하는
    /// 안전망을 넣었다. 사각지대 자체는 그대로였다: 물리 바닥(PhysicsGround)은 화면 최하단의 전체 폭
    /// 한 장인데 그 위의 논리 발판(Dock 상단)은 1.6375유닛 더 높아서, Dock 가로 구간 아래에 큰 빈
    /// 공간이 있었다.
    ///
    /// 이 파일이 잠그는 것은 **그 빈 공간이 없어졌다**는 사실이다. Platform/DockPhysicsStep이 런타임에
    /// Dock 가로 구간 아래에 Dock 상단 높이의 물리 콜라이더를 놓으므로, 접지 스냅이 끊겨도 캐릭터는
    /// **Dock 상단 높이에서 물리적으로 바로 멈춘다**(자유낙하가 애초에 일어나지 않는다).
    ///
    /// ============================================================================
    /// 잠그는 항목
    /// ============================================================================
    ///  T1  Dock 위에서 접지 스냅이 끊겨도(접지 안전망 off, 사각지대 회수 off) **낙하 깊이가 거의 0**.
    ///  T1n (네거티브) 계단을 끄면 같은 시나리오에서 **1.6유닛 깊은 낙하가 실제로 재현**된다.
    ///  T2  계단의 기하가 Dock 발판과 **같은 단일 소스**에서 나온다 — 윗면 Y/좌우 끝이 Dock 발판과 일치,
    ///      아랫면은 PhysicsGround 아랫면 이하(둘 사이에 새 틈이 생기지 않는다).
    ///  T3  계단이 있으면 사각지대 회수(`[사각지대회수]`)가 **한 번도 발동하지 않는다**.
    ///  T3n (네거티브) 계단을 끄면 같은 시나리오에서 회수가 **실제로 발동한다**.
    ///  T4  Dock 발판이 없어지면(자동 숨김/세로 Dock/비-macOS) 계단도 **즉시 사라진다** —
    ///      실제 Dock이 없는 자리에 보이지 않는 벽을 남기지 않는다.
    ///
    /// 배치는 DockSinkholeRegressionTests와 **동일한 실측 재현**이다(같은 사고를 두 각도에서 본다):
    /// 씬의 PhysicsGround 상단 Y를 실측해 그 높이에 논리 안전망 두 조각을 놓고, 그보다 1.6375유닛 위에
    /// Dock 발판을 놓되 Dock 가로 구간에는 논리 발판 구멍을 남긴다. StickConfig는 복제본을 꽂아 원본
    /// 자산을 절대 건드리지 않는다(CLAUDE.md 불변 원칙 3).
    /// </summary>
    public sealed class DockPhysicsStepTests
    {
        private const string LogPrefix = "[STEP-TEST]";

        private const long DockHandle = -2L;   // FallbackPlatformWindowService.DockFootholdHandle
        private const long NetLeftHandle = -1L;
        private const long NetRightHandle = -3L;

        /// <summary>실제 macOS 실측 — Dock 상단에서 화면 최하단 안전망까지의 낙차.</summary>
        private const float DockDropUnits = 1.6375f;

        private const float SettleWaitSeconds = 2.0f;

        /// <summary>Dock 가로 구간(화면 폭 비율). 화면 정중앙(world x≈0)이 반드시 포함되어야 한다.</summary>
        private const float DockLeftFraction = 0.25f;
        private const float DockRightFraction = 0.75f;

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
        private DockPhysicsStep _step;

        private float _dockTopWorldY;
        private float _floorTopWorldY;
        private float _dockTopOsY;
        private float _floorTopOsY;

        private int _sinkholeLifts;

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
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
            _step = null;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!string.IsNullOrEmpty(condition) && condition.Contains("[사각지대회수]")) _sinkholeLifts++;
        }

        /// <summary>
        /// 실제 배치 재현 + Dock 물리 계단이 실제로 켜질 때까지 대기.
        /// </summary>
        /// <param name="stepEnabled">물리 계단 스위치(네거티브 컨트롤용).</param>
        /// <param name="groundKeepingSafetyNet">
        /// 접지 유지 안전망. **기본 false** — 켜두면 상태머신이 매 프레임 캐릭터를 발판에 붙여 놓아
        /// 물리 계단이 한 번도 실행되지 않은 채 통과한다(DockSinkholeRegressionTests의 T1c와 같은 이유로,
        /// 이 파일은 물리 계단 **단독**의 효과를 본다).
        /// </param>
        private IEnumerator SetUp(bool stepEnabled, bool groundKeepingSafetyNet = false, bool sinkholeLift = false)
        {
            _sinkholeLifts = 0;

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");

            var steps = Object.FindObjectsByType<DockPhysicsStep>(FindObjectsSortMode.None);
            Assert.AreEqual(1, steps.Length,
                $"{LogPrefix} 씬의 DockPhysicsStep이 {steps.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락(--force 재생성 필요), 2개 이상이면 중복 배치입니다. " +
                "★ 물리 바닥은 플레이어/라이벌이 **공유**하는 오브젝트이므로 캐릭터마다 하나씩 두면 안 됩니다.");
            _step = steps[0];

            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;
            _clonedConfig.dockPhysicsStepEnabled = stepEnabled;
            _clonedConfig.groundKeepingSafetyNetEnabled = groundKeepingSafetyNet;
            _clonedConfig.sinkholeLiftRecoveryEnabled = sinkholeLift;

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 전제 실패 — 씬에 PhysicsGround가 없습니다.");
            _floorTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y;
            _dockTopWorldY = _floorTopWorldY + DockDropUnits;

            Camera cam = bb.MainCamera;
            _floorTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _floorTopWorldY), _clonedConfig, out _).y;
            _dockTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _dockTopWorldY), _clonedConfig, out _).y;

            _service = new TestFootholdService();
            ApplyDockSpan();
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.IntentSource = new StillIntentSource();

            Application.logMessageReceived += OnLog;

            // 계단 갱신은 DockPhysicsStep.Update()에서 일어난다 — 몇 프레임 준다.
            yield return null;
            yield return null;
            yield return null;

            Debug.Log($"{LogPrefix} 준비 — 물리바닥 상단 월드Y={_floorTopWorldY:F4}, Dock 상단 월드Y={_dockTopWorldY:F4}, " +
                $"낙차={DockDropUnits:F4}유닛, 계단스위치={stepEnabled}, 계단활성={_step.IsActive}, " +
                $"접지안전망={groundKeepingSafetyNet}, 사각지대회수={sinkholeLift}.");
        }

        private void ApplyDockSpan()
        {
            float w = Screen.width;
            float h = Screen.height;
            float leftOs = w * DockLeftFraction;
            float rightOs = w * DockRightFraction;
            _service.Footholds.Clear();
            _service.Footholds.Add(new PlatformFoothold(DockHandle,
                new Rect(leftOs, _dockTopOsY, rightOs - leftOs, h - _dockTopOsY), true));
            _service.Footholds.Add(new PlatformFoothold(NetLeftHandle,
                new Rect(0f, _floorTopOsY, leftOs, h - _floorTopOsY), false));
            _service.Footholds.Add(new PlatformFoothold(NetRightHandle,
                new Rect(rightOs, _floorTopOsY, w - rightOs, h - _floorTopOsY), false));
        }

        private float WorldXAtScreenFraction(float frac)
        {
            return ScreenCoordinateConverter.OsScreenToWorld(_agent.Blackboard.MainCamera,
                new Vector2(Screen.width * frac, _dockTopOsY), 10f, _clonedConfig).x;
        }

        /// <summary>Dock 정중앙에 세워 두고, 접지 스냅을 부르지 않는 상태(Attack)로 강제 전이시킨다.</summary>
        private void DropOnDock()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            bb.MoveBodyToWorld(new Vector2(0f, _dockTopWorldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = DockHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Attack, isForcedInterrupt: true);
        }

        private IEnumerator ObserveLowestY(float seconds, System.Action<float> report)
        {
            float lowest = float.MaxValue;
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                float y = _agent.Blackboard.Body.position.y;
                if (y < lowest) lowest = y;
                yield return null;
            }
            report(lowest);
        }

        // ====================================================================
        // T1 / T1n — 낙하 깊이
        // ====================================================================

        [UnityTest]
        public IEnumerator T1_계단이_있으면_Dock에서_접지가_끊겨도_거의_안_떨어진다()
        {
            yield return SetUp(stepEnabled: true);

            Assert.IsTrue(_step.IsActive,
                $"{LogPrefix} Dock 발판이 있는데 물리 계단이 켜지지 않았습니다 — DockPhysicsStep 배선/설정 확인.");

            DropOnDock();

            float lowest = 0f;
            yield return ObserveLowestY(2f, v => lowest = v);

            float depth = _dockTopWorldY - lowest;
            Debug.Log($"{LogPrefix} T1 낙하 깊이={depth:F4}유닛 (최저Y={lowest:F4}, Dock 상단={_dockTopWorldY:F4}, " +
                $"물리바닥 상단={_floorTopWorldY:F4}). 사각지대회수 발동={_sinkholeLifts}회.");

            // 물리 접촉 오프셋/한 스텝 관통을 감안한 여유. 예전 거동(1.6375유닛)의 1/8 미만이면
            // "사실상 안 떨어진다"고 말할 수 있다.
            Assert.Less(depth, 0.2f,
                $"{LogPrefix} ★회귀★ Dock 구간에서 여전히 {depth:F3}유닛 떨어졌습니다 — 물리 계단이 " +
                "Dock 상단 높이를 떠받치지 못하고 있습니다.");
        }

        [UnityTest]
        public IEnumerator T1n_네거티브_계단을_끄면_깊은_낙하가_재현된다()
        {
            yield return SetUp(stepEnabled: false);

            Assert.IsFalse(_step.IsActive,
                $"{LogPrefix} 스위치를 껐는데 물리 계단이 켜져 있습니다 — 네거티브 컨트롤이 성립하지 않습니다.");

            DropOnDock();

            float lowest = 0f;
            yield return ObserveLowestY(2f, v => lowest = v);

            float depth = _dockTopWorldY - lowest;
            Debug.Log($"{LogPrefix} T1n(네거티브) 낙하 깊이={depth:F4}유닛 (최저Y={lowest:F4}).");

            Assert.Greater(depth, 1.0f,
                $"{LogPrefix} 네거티브 컨트롤이 성립하지 않습니다 — 계단을 껐는데도 깊은 낙하가 " +
                $"재현되지 않았습니다({depth:F3}유닛). T1의 통과가 물리 계단 덕분이라고 말할 수 없습니다.");
        }

        // ====================================================================
        // T2 — 기하가 Dock 발판과 같은 단일 소스에서 나온다
        // ====================================================================

        [UnityTest]
        public IEnumerator T2_계단_기하가_Dock_발판과_정확히_일치한다()
        {
            yield return SetUp(stepEnabled: true);

            Assert.IsTrue(_step.IsActive, $"{LogPrefix} 물리 계단이 켜지지 않았습니다.");
            Bounds b = _step.StepBounds;

            float expectedLeft = WorldXAtScreenFraction(DockLeftFraction);
            float expectedRight = WorldXAtScreenFraction(DockRightFraction);

            Debug.Log($"{LogPrefix} T2 계단 bounds x={b.min.x:F4}~{b.max.x:F4}, 윗면 y={b.max.y:F4}, 아랫면 y={b.min.y:F4} / " +
                $"기대 Dock 구간 x={expectedLeft:F4}~{expectedRight:F4}, Dock 상단 y={_dockTopWorldY:F4}.");

            Assert.AreEqual(_dockTopWorldY, b.max.y, 0.02f,
                $"{LogPrefix} 계단 윗면이 Dock 발판 상단과 어긋납니다 — 그 차이가 곧 새로운 사각지대입니다.");
            Assert.AreEqual(expectedLeft, b.min.x, 0.05f,
                $"{LogPrefix} 계단 왼쪽 끝이 Dock 가로 구간과 어긋납니다(단일 소스 위반).");
            Assert.AreEqual(expectedRight, b.max.x, 0.05f,
                $"{LogPrefix} 계단 오른쪽 끝이 Dock 가로 구간과 어긋납니다(단일 소스 위반).");

            var ground = GameObject.Find("PhysicsGround").GetComponent<BoxCollider2D>();
            Assert.LessOrEqual(b.min.y, ground.bounds.min.y + 0.001f,
                $"{LogPrefix} 계단 아랫면이 물리 바닥 아랫면보다 위에 있습니다 — 그 사이에 새로운 틈이 " +
                "생겨 이번에 없애려던 것과 같은 종류의 사각지대를 다시 만듭니다.");

            // 전체 폭 물리 바닥은 그대로 남아 있어야 한다(계단은 구멍이 아니라 그 위에 얹힌 별개 오브젝트).
            Assert.Less(ground.bounds.min.x, b.min.x, $"{LogPrefix} 물리 바닥이 계단보다 좁습니다.");
            Assert.Greater(ground.bounds.max.x, b.max.x, $"{LogPrefix} 물리 바닥이 계단보다 좁습니다.");
        }

        // ====================================================================
        // T3 / T3n — 임시방편(사각지대 회수)이 더 이상 발동하지 않는다
        // ====================================================================

        [UnityTest]
        public IEnumerator T3_계단이_있으면_사각지대회수가_한_번도_발동하지_않는다()
        {
            yield return SetUp(stepEnabled: true, groundKeepingSafetyNet: false, sinkholeLift: true);

            DropOnDock();
            yield return new WaitForSeconds(3f);

            Debug.Log($"{LogPrefix} T3 사각지대회수 발동={_sinkholeLifts}회 (0이어야 합니다).");
            Assert.AreEqual(0, _sinkholeLifts,
                $"{LogPrefix} ★회귀★ 물리 계단이 있는데도 사각지대 회수가 {_sinkholeLifts}회 발동했습니다 — " +
                "사각지대가 아직 남아 있다는 뜻입니다(회수는 안전망일 뿐 정상 경로가 아니어야 합니다).");
        }

        [UnityTest]
        public IEnumerator T3n_네거티브_계단을_끄면_사각지대회수가_실제로_발동한다()
        {
            yield return SetUp(stepEnabled: false, groundKeepingSafetyNet: false, sinkholeLift: true);

            DropOnDock();
            yield return new WaitForSeconds(3f);

            Debug.Log($"{LogPrefix} T3n(네거티브) 사각지대회수 발동={_sinkholeLifts}회 (1회 이상이어야 합니다).");
            Assert.GreaterOrEqual(_sinkholeLifts, 1,
                $"{LogPrefix} 네거티브 컨트롤이 성립하지 않습니다 — 계단을 껐는데도 사각지대 회수가 " +
                "발동하지 않았습니다. T3의 '0회'가 계단 덕분이라고 말할 수 없습니다.");
        }

        // ====================================================================
        // T4 — Dock이 사라지면 계단도 사라진다
        // ====================================================================

        [UnityTest]
        public IEnumerator T4_Dock_발판이_사라지면_계단도_즉시_사라진다()
        {
            yield return SetUp(stepEnabled: true);
            Assert.IsTrue(_step.IsActive, $"{LogPrefix} 전제 실패 — 계단이 켜져 있어야 합니다.");

            // Dock 자동 숨김 / 세로 Dock / 비-macOS = Dock 발판이 목록에서 사라지는 경우.
            _service.Footholds.RemoveAll(f => f.Handle == DockHandle);
            _agent.Blackboard.FootholdPoller.PollImmediately();
            yield return null;
            yield return null;

            Assert.IsFalse(_step.IsActive,
                $"{LogPrefix} Dock 발판이 사라졌는데 물리 계단이 남아 있습니다 — 실제 Dock이 없는 자리에 " +
                "보이지 않는 벽이 서 있게 됩니다(비침해 원칙/UX 양쪽에 나쁩니다).");
            Debug.Log($"{LogPrefix} T4 통과 — Dock 발판 제거와 함께 계단도 꺼졌습니다.");
        }
    }
}
