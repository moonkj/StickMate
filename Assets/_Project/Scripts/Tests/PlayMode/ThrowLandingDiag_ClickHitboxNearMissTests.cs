using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 2026-09-05 진단 로그 (a) — 근접 미스 클릭 로그의 <b>발생 조건</b>을 실제 히트박스 경로로 잠근다
    /// (docs/DEBUG_WINDOWS_THROW_LANDING_STALL.md §6). 규칙/포맷은 EditMode <c>ThrowLandingDiag_NearMissPolicyTests</c>.
    ///
    /// 에디터에는 전역 버튼 서비스가 없어 <c>StickmanClickHitbox.Update</c>가 첫 줄에서 돌아가므로,
    /// 프로덕션이 쓰는 같은 본체 <see cref="StickmanClickHitbox.ProcessGlobalButtonSample"/>에 버튼 표본을 직접 넣는다.
    /// 판정은 LogAssert(포맷) + 카운터(발생/억제/먼 클릭)로 한다 — "안 남는다"를 LogAssert의 부정형 없이 값으로 단언하기 위해서다.
    /// 레이트리밋은 벽시계 초(<c>WaitForSecondsRealtime</c>)로 잰다.
    /// </summary>
    public sealed class ThrowLandingDiag_ClickHitboxNearMissTests
    {
        private const string LogPrefix = "[히트판정-TEST]";
        private const long FlatGroundHandle = 9501L;
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

        private sealed class ScriptedIntentSource : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private StickmanAgent _agent;
        private StickmanClickHitbox _hitbox;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private CursorPositionQuery _originalCursor;
        private Vector2 _savedOrigin;
        private TestFootholdService _service;
        private float _groundWorldY;
        private float _characterHeight;
        private Vector2 _cursorWorld;
        private int _mouseDowns;

        [TearDown]
        public void TearDown()
        {
            if (_hitbox != null) _hitbox.MouseDown -= CountMouseDown;
            if (_agent != null && _agent.Blackboard != null)
            {
                StickmanBlackboard bb = _agent.Blackboard;
                if (bb.Machine != null && bb.Machine.CurrentStateId == StickmanStateId.Dragged)
                {
                    bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                }
                if (_originalConfig != null) bb.Config = _originalConfig;
                if (_originalIntent != null) bb.IntentSource = _originalIntent;
                if (_originalPoller != null) bb.FootholdPoller = _originalPoller;
                bb.CursorProvider = _originalCursor;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
            _hitbox = null;
        }

        private void CountMouseDown() => _mouseDowns++;

        private IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} StickmanAgent 없음.");
            _hitbox = _agent.GetComponent<StickmanClickHitbox>();
            Assert.IsNotNull(_hitbox, $"{LogPrefix} StickmanClickHitbox 없음.");
            _mouseDowns = 0;
            _hitbox.MouseDown += CountMouseDown;

            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _originalCursor = bb.CursorProvider;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            _clonedConfig.rodeoCursorEnabled = false;
            bb.Config = _clonedConfig;

            GameObject physicsGround = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(physicsGround, $"{LogPrefix} PhysicsGround 없음.");
            _groundWorldY = physicsGround.GetComponent<BoxCollider2D>().bounds.max.y;

            Camera cam = bb.MainCamera;
            Vector2 groundOs = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _groundWorldY), _clonedConfig, out _);
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(FlatGroundHandle,
                new Rect(0f, groundOs.y, Screen.width, Mathf.Max(1f, Screen.height - groundOs.y)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.IntentSource = new ScriptedIntentSource { MoveInputX = 0f };
            bb.CursorProvider = TryGetScriptedCursor;

            Vector2 start = new Vector2(0f, _groundWorldY);
            bb.Body.bodyType = RigidbodyType2D.Dynamic;
            bb.Body.position = start;
            bb.Body.transform.position = new Vector3(start.x, start.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = FlatGroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            yield return new WaitForSeconds(0.5f);
            _characterHeight = bb.CharacterHeightWorld;
            Assert.Greater(_characterHeight, 0.1f, $"{LogPrefix} 신장을 읽지 못했다.");

            // 전역 폴링 경로의 첫 표본은 기록만 하고 넘어간다(앱 시작 순간 이미 눌려 있던 경우를 클릭으로 오인하지 않는 설계).
            _hitbox.ProcessGlobalButtonSample(false);
        }

        private bool TryGetScriptedCursor(out Vector2 osScreenPosition)
        {
            Camera cam = _agent != null ? _agent.Blackboard.MainCamera : null;
            if (cam == null) { osScreenPosition = default; return false; }
            osScreenPosition = ScreenCoordinateConverter.WorldToOsScreen(cam, _cursorWorld, _clonedConfig, out _);
            return true;
        }

        private Vector2 BodyCenter()
        {
            Vector2 foot = _agent.Blackboard.Body.position;
            return foot + new Vector2(0f, _characterHeight * 0.5f);
        }

        /// <summary>커서를 놓고 누름 표본 한 번(상승 엣지) + 놓음 표본 한 번.</summary>
        private void Click(Vector2 cursorWorld)
        {
            _cursorWorld = cursorWorld;
            _hitbox.ProcessGlobalButtonSample(true);
            _hitbox.ProcessGlobalButtonSample(false);
        }

        [UnityTest]
        public IEnumerator 먼_클릭은_침묵하고_근접_미스는_히트판정_한줄을_남기고_연타는_벽시계_2초_레이트리밋에_걸린다()
        {
            yield return SetUp();
            StickmanBlackboard bb = _agent.Blackboard;
            float h = _characterHeight;

            // (1) 먼 클릭 — 몸 중심에서 신장 4배 옆. 로그 없음, 먼 클릭 카운터만.
            int far0 = _hitbox.FarMissCount, log0 = _hitbox.NearMissLogCount;
            Click(BodyCenter() + new Vector2(4f * h, 0f));
            Assert.AreEqual(far0 + 1, _hitbox.FarMissCount, $"{LogPrefix} 먼 클릭이 먼 클릭으로 세이지 않았다.");
            Assert.AreEqual(log0, _hitbox.NearMissLogCount, $"{LogPrefix} 먼 클릭이 로그를 남겼다 — 24시간 상주 앱의 로그가 클릭 수만큼 자란다.");
            Assert.AreEqual(0, _mouseDowns, $"{LogPrefix} 먼 클릭이 MouseDown을 냈다(콜라이더 판정이 틀렸다).");

            // (2) 근접 미스 — 몸 중심에서 신장 1.0배 옆(콜라이더 밖, 근접 반경 1.5배 안). 로그 1줄.
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(ClickHitboxNearMissPolicy.LogTag) + " 근접 클릭이 히트박스를 벗어남"));
            Click(BodyCenter() + new Vector2(1.0f * h, 0f));
            Assert.AreEqual(log0 + 1, _hitbox.NearMissLogCount, $"{LogPrefix} 근접 미스가 로그로 남지 않았다.");
            Assert.AreEqual(0, _mouseDowns, $"{LogPrefix} 근접 미스 위치에서 MouseDown이 났다 — 테스트 전제(콜라이더 밖) 재검토.");

            // (3) 곧바로 다시 — 레이트리밋(억제 카운터만 오른다).
            int sup0 = _hitbox.NearMissSuppressedCount;
            Click(BodyCenter() + new Vector2(-1.0f * h, 0f));
            Assert.AreEqual(sup0 + 1, _hitbox.NearMissSuppressedCount, $"{LogPrefix} 연타가 억제되지 않았다.");
            Assert.AreEqual(log0 + 1, _hitbox.NearMissLogCount);

            // (4) 벽시계로 간격을 넘긴 뒤 — 다시 남는다.
            yield return new WaitForSecondsRealtime(ClickHitboxNearMissPolicy.LogMinIntervalSeconds + 0.1f);
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(ClickHitboxNearMissPolicy.LogTag) + " 근접 클릭이 히트박스를 벗어남"));
            Click(BodyCenter() + new Vector2(1.0f * h, 0f));
            Assert.AreEqual(log0 + 2, _hitbox.NearMissLogCount, $"{LogPrefix} 간격이 지났는데 로그가 다시 남지 않았다.");

            // (5) 양성 대조 — 몸 중심을 누르면 히트(MouseDown → DragThrowController → Dragged)이고 미스 로그는 오르지 않는다.
            int log1 = _hitbox.NearMissLogCount;
            _cursorWorld = BodyCenter();
            _hitbox.ProcessGlobalButtonSample(true);
            Assert.AreEqual(1, _mouseDowns, $"{LogPrefix} 몸 중심 클릭이 MouseDown을 내지 않았다 — 이 테스트는 미스를 잴 자격이 없다.");
            Assert.AreEqual(StickmanStateId.Dragged, bb.Machine.CurrentStateId, $"{LogPrefix} 히트가 Dragged로 이어지지 않았다.");
            Assert.AreEqual(log1, _hitbox.NearMissLogCount, $"{LogPrefix} 히트인데 미스 로그가 올랐다.");
            _hitbox.ProcessGlobalButtonSample(false);
            yield return null;
        }
    }
}
