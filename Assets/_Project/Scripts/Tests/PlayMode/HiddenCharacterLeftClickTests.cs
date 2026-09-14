using System.Collections;
using System.Reflection;
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
    /// ★★ <b>숨은 캐릭터 자리 좌클릭</b>이 잡기를 시작하지 않는가 — 2026-09-14 Major(debugger 판독) 회귀 잠금, E-4 좌클릭 절반.
    ///
    /// ============================================================================
    /// 무엇이 결함이었나
    /// ============================================================================
    /// 사용자 숨김 · 전체화면 게임(등급 2) · 등급 1 + 숨김 · (Windows) 다른 가상 데스크톱에서 <c>Suspend</c>는 몸의
    /// 물리(<c>Rigidbody2D.simulated</c>)를 끈다. 그래서 OS 히트테스트와 같은 레이캐스트(<c>Physics2D.GetRayIntersection</c>)는
    /// 몸을 못 잡아 클릭은 <b>아래 앱으로 간다</b>. 그런데 전역 버튼 폴링의 판정은 콜라이더 <b>기하</b>(<c>OverlapPoint</c>)라
    /// 여전히 참이고, 공통 입구 <c>StickmanClickHitbox.BeginPress</c>가 드래그 전이·연출 잠금·잡힘 대사를 만들었다.
    /// 수정은 그 입구 첫 줄의 캐릭터 축(<c>IsSuspended</c>) 게이트다.
    ///
    /// ============================================================================
    /// 케이스 (수정 전 기대 → 수정 후 기대)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>L0</b> 보이는 몸 좌클릭은 잡힌다 — 양성 대조(초록 → 초록).</item>
    ///   <item><b>L1</b> 사용자 숨김 · <b>L2</b> 등급 2 · <b>L3</b> 등급 1 + 숨김 · <b>L6</b> 다른 가상 데스크톱 —
    ///     보이지 않는 몸 자리 좌클릭이 잡기·잠금·전이를 만들지 않는다(빨강 → 초록).</item>
    ///   <item><b>L4</b> 가출 은신(Suspend 아님)의 「찾기」 좌클릭은 그대로 된다(초록 → 초록). 좌클릭 게이트에 우클릭
    ///     «화면에 있는가» 식을 그대로 쓰면 여기가 빨개진다.</item>
    ///   <item><b>L4b</b>(설계서의 L4′) 숨김 중에는 공통 입구가 <c>MouseDown</c>을 <b>아예 내지 않는다</b> — 받는 곳 다섯이 한꺼번에
    ///     막힌다(빨강 → 초록). 게이트를 드래그 쪽에만 두면 L1~L3은 초록이어도 여기가 빨갛다.</item>
    ///   <item><b>L5</b> 보존 동결 중 <b>공통 입구로</b> 눌러도 잡기는 미뤄진다(초록 → 초록) —
    ///     <c>CharacterPreservationFreezeAgentTests</c>의 드래그 테스트는 <c>OnMouseDown</c>을 리플렉션으로 직접 불러 입구를 우회한다.</item>
    /// </list>
    ///
    /// <para><b>입력은 프로덕션 본체를 태운다</b> — 공개 진입점 <c>ProcessGlobalButtonSample</c>(전역 폴링 경로 그대로)과
    /// 에이전트 커서 공급자를 몸 중심에 꽂는다. 숨김 전제는 <c>OverlapPoint</c> 참 · <c>GetRayIntersection</c> 없음을 먼저 단언한다
    /// (그래야 «아래 앱으로 간 클릭이 우리 입구에 닿는» 결함 경로를 실제로 탄다). 시간은 전부 벽시계다.</para>
    /// <para>이 파일은 E-4가 더한 새 API를 쓰지 않는다 — 수정 전 트리에서도 컴파일되어 결함 실재를 박제할 수 있게.</para>
    /// </summary>
    public sealed class HiddenCharacterLeftClickTests
    {
        private const string LogPrefix = "[숨김좌클릭-TEST]";
        private const float SettleSeconds = 0.35f;
        private const float ObserveSeconds = 0.4f;
        private const float ReleaseTimeoutSeconds = 3f;
        private const float RunawayHideSlackSeconds = 3f;
        private const float EdgeTimeoutSeconds = 2f;
        private const float ObservePollInterval = 9999f;

        private StickmanAgent _agent;
        private StickmanBlackboard _bb;
        private StickmanClickHitbox _hitbox;
        private RunawayDirector _runaway;
        private StickConfig _config;
        private float _savedPollInterval;
        private bool _configHijacked;
        private CursorPositionQuery _savedCursor;
        private Vector2 _savedOverlayOrigin;
        private bool _hijacked;
        private bool _subscribed;
        private int _mouseDownCount;
        private int _transitions;

        private static readonly FieldInfo FullscreenAxisField =
            typeof(StickmanAgent).GetField("_fullscreenAutoHide", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PanelRetreatField =
            typeof(StickmanAgent).GetField("_fullscreenPanelRetreat", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo VirtualDesktopField =
            typeof(StickmanAgent).GetField("_offCurrentVirtualDesktop", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ApplyDecisionMethod =
            typeof(StickmanAgent).GetMethod("ApplySuspendDecision", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo PublishStartedMethod =
            typeof(DisplayChangeHoldStatus).GetMethod("PublishStarted", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo PublishReleasedMethod =
            typeof(DisplayChangeHoldStatus).GetMethod("PublishReleased", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ResetStatusMethod =
            typeof(DisplayChangeHoldStatus).GetMethod("ResetForTesting", BindingFlags.Static | BindingFlags.NonPublic);

        private void OnMouseDownObserved() => _mouseDownCount++;
        private void OnStateTransitioned(StateTransitionEvent evt) => _transitions++;

        // ==================== 준비 / 정리 ====================

        [OneTimeSetUp]
        public void RequireIsolation()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        /// <summary>픽스처 정리 단언 — 연출 잠금·화면 변경 유예가 뒤 픽스처로 새지 않는다. 실패는 <c>site="TearDown"</c>으로만 남으므로
        /// (TEAM.md 「픽스처 끝에서 난 실패」) 단언 뒤에 성공 로그를 남긴다.</summary>
        [OneTimeTearDown]
        public void AssertNothingLeaked()
        {
            bool lockLeaked = SpectacleEventLock.IsActive;
            if (lockLeaked) SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            ResetHold();
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
            Assert.IsFalse(lockLeaked, $"{LogPrefix} ★ 연출 잠금이 픽스처 끝까지 남았습니다 — 뒤 픽스처의 연출이 전부 «다른 연출 중»으로 막힙니다.");
            Debug.Log($"{LogPrefix} 픽스처 정리 단언 통과 — 연출 잠금 누수 없음, 화면 변경 유예 초기화 완료.");
        }

        [UnityTearDown]
        public IEnumerator RestoreEverything()
        {
            ResetHold();   // ★ 가장 먼저 — 새면 뒤 테스트의 캐릭터가 얼어붙는다.

            if (_hitbox != null && _hijacked) _hitbox.ProcessGlobalButtonSample(false);   // 누른 채 남기지 않는다.
            if (_subscribed)
            {
                if (_hitbox != null) _hitbox.MouseDown -= OnMouseDownObserved;
                StickmanEventBus.StateTransitioned -= OnStateTransitioned;
                _subscribed = false;
            }

            if (_agent != null)
            {
                if (_agent.IsUserHidden) _agent.SetUserHidden(false, "숨김좌클릭 테스트 정리");
                if (FullscreenAxisField != null) FullscreenAxisField.SetValue(_agent, false);
                if (PanelRetreatField != null) PanelRetreatField.SetValue(_agent, false);
                if (VirtualDesktopField != null) VirtualDesktopField.SetValue(_agent, false);
                ApplyDecisionMethod?.Invoke(_agent, null);
                if (_bb != null && _bb.Machine != null
                    && (_bb.Machine.CurrentStateId == StickmanStateId.Runaway || _bb.Machine.CurrentStateId == StickmanStateId.Dragged))
                {
                    _bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                }
            }
            if (SpectacleEventLock.IsActive) SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);

            if (_hijacked)
            {
                if (_bb != null) _bb.CursorProvider = _savedCursor;
                ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOverlayOrigin;
                _hijacked = false;
            }
            if (_configHijacked && _config != null) _config.fullscreenPollInterval = _savedPollInterval;
            _configHijacked = false;

            _agent = null;
            _bb = null;
            _hitbox = null;
            _runaway = null;
            _config = null;
            yield return null;
        }

        private static void InvokeStatic(MethodInfo method, params object[] args)
        {
            Assert.IsNotNull(method, $"{LogPrefix} 화면 변경 유예 계약의 테스트 훅을 찾지 못했습니다 — 이름이 바뀌었으면 이 파일을 고치십시오.");
            method.Invoke(null, args);
        }

        private static void ResetHold()
        {
            if (ResetStatusMethod != null) ResetStatusMethod.Invoke(null, null);
        }

        private IEnumerator LoadScene()
        {
            Assert.IsNotNull(FullscreenAxisField, $"{LogPrefix} StickmanAgent._fullscreenAutoHide를 찾지 못했습니다 — 등급 2 주입이 아무 일도 안 합니다.");
            Assert.IsNotNull(PanelRetreatField, $"{LogPrefix} StickmanAgent._fullscreenPanelRetreat를 찾지 못했습니다.");
            Assert.IsNotNull(VirtualDesktopField, $"{LogPrefix} StickmanAgent._offCurrentVirtualDesktop를 찾지 못했습니다 — L6 주입이 아무 일도 안 합니다.");
            Assert.IsNotNull(ApplyDecisionMethod, $"{LogPrefix} StickmanAgent.ApplySuspendDecision()을 찾지 못했습니다.");

            ResetHold();
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            _bb = _agent.Blackboard;
            Assert.IsNotNull(_bb, $"{LogPrefix} 블랙보드가 없습니다.");
            _hitbox = _agent.GetComponentInChildren<StickmanClickHitbox>(true);
            Assert.IsNotNull(_hitbox, $"{LogPrefix} 캐릭터에 StickmanClickHitbox가 없습니다 — 공통 입구가 없습니다.");
            _runaway = Object.FindFirstObjectByType<RunawayDirector>(FindObjectsInactive.Include);
            Assert.IsNotNull(_runaway, $"{LogPrefix} 씬에 RunawayDirector가 없습니다.");

            _config = _agent.Config;
            Assert.IsNotNull(_config, $"{LogPrefix} StickConfig가 없습니다.");
            _savedPollInterval = _config.fullscreenPollInterval;
            _config.fullscreenPollInterval = ObservePollInterval;   // 에디터 널 서비스의 폴링이 주입한 축을 되돌리지 못하게.
            _configHijacked = true;

            yield return Wait(SettleSeconds);

            _savedOverlayOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
            _savedCursor = _bb.CursorProvider;
            _bb.CursorProvider = TryGetScriptedCursor;
            _hijacked = true;

            _hitbox.MouseDown += OnMouseDownObserved;
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            _subscribed = true;

            StickmanStateId state = _bb.Machine.CurrentStateId;
            if (state != StickmanStateId.Idle && state != StickmanStateId.Walk)
            {
                _bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                yield return Wait(SettleSeconds);
            }

            // 첫 표본은 «기록만» 하는 프레임이다(시작 순간 눌려 있던 버튼을 클릭으로 오인하지 않는 설계).
            _hitbox.ProcessGlobalButtonSample(false);
            _mouseDownCount = 0;
            _transitions = 0;
        }

        private static IEnumerator Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        // ==================== 커서 · 판정 전제 ====================

        private Vector2 CursorWorldNow() => _bb.Body.position + new Vector2(0f, _bb.CharacterHeightWorld * 0.5f);

        private bool TryGetScriptedCursor(out Vector2 osScreenPosition)
        {
            Camera cam = _bb != null ? _bb.MainCamera : null;
            if (cam == null) { osScreenPosition = default; return false; }
            osScreenPosition = ScreenCoordinateConverter.WorldToOsScreen(cam, CursorWorldNow(), _agent.Config, out _);
            return true;
        }

        private bool AnyColliderContains(Vector2 world)
        {
            Collider2D[] all = _agent.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].enabled) continue;
                if (all[i].OverlapPoint(world)) return true;
            }
            return false;
        }

        /// <summary>OS 히트테스트와 같은 물리 레이캐스트(UniWindowController hitTestType=Raycast의 기전)를 커서 자리에 쏜다.</summary>
        private RaycastHit2D RaycastAtCursor()
        {
            Camera cam = _bb.MainCamera;
            Assert.IsNotNull(cam, $"{LogPrefix} 메인 카메라가 없습니다.");
            Vector2 w = CursorWorldNow();
            Vector3 screen = cam.WorldToScreenPoint(new Vector3(w.x, w.y, _bb.Body.transform.position.z));
            return Physics2D.GetRayIntersection(cam.ScreenPointToRay(screen));
        }

        /// <summary>보이는 몸: 기하 판정도 레이캐스트도 몸에 맞는다(아래 «레이캐스트 없음» 판정이 실제로 «맞음»도 볼 수 있다는 대조).</summary>
        private void AssertVisibleBodyIsHit(string what)
        {
            Assert.IsTrue(AnyColliderContains(CursorWorldNow()), $"{LogPrefix} {what} — 몸 중심이 콜라이더 기하에 안 걸립니다(하네스 전제 실패).");
            RaycastHit2D hit = RaycastAtCursor();
            Assert.IsNotNull(hit.collider,
                $"{LogPrefix} {what} — 보이는 몸 자리에 레이캐스트가 아무것도 못 맞혔습니다. 이 측정이 «맞음»을 볼 수 없다면 숨김 케이스의 «없음» 전제도 무의미합니다.");
        }

        /// <summary>숨은 몸의 함정: 기하 판정은 참(우리 입구에 닿는다) · 레이캐스트는 없음(클릭은 아래 앱으로 간다).</summary>
        private void AssertInvisibleBodyTrap(string what)
        {
            Assert.IsTrue(AnyColliderContains(CursorWorldNow()),
                $"{LogPrefix} {what} — 숨은 몸 자리가 콜라이더 기하(OverlapPoint)에 안 걸립니다. 그러면 이 케이스는 결함 경로(전역 폴링 → 기하 참 → 입구)를 타지 않습니다.");
            RaycastHit2D hit = RaycastAtCursor();
            Assert.IsNull(hit.collider,
                $"{LogPrefix} {what} — 숨은 몸 자리에 레이캐스트가 맞았습니다({(hit.collider != null ? hit.collider.name : "-")}). " +
                "몸이 물리에서 빠지지 않았다는 뜻이고, 그러면 «보이지 않는 몸 자리의 클릭은 아래 앱으로 간다»는 전제가 아닙니다.");
        }

        /// <summary>좌클릭 한 번(누름 → 관측 → 뗌) 동안 잡기가 시작되지 않았음을 단언한다.</summary>
        private IEnumerator AssertLeftClickStartsNothing(string what)
        {
            int transitionsBefore = _transitions;
            bool everDragged = false;
            _hitbox.ProcessGlobalButtonSample(true);
            if (_bb.Machine.CurrentStateId == StickmanStateId.Dragged) everDragged = true;
            yield return TestClock.SampleForSeconds(ObserveSeconds, (float _) =>
            {
                if (_bb.Machine.CurrentStateId == StickmanStateId.Dragged) everDragged = true;
            });
            bool lockHeld = SpectacleEventLock.IsActive;
            _hitbox.ProcessGlobalButtonSample(false);
            yield return null;

            Assert.IsFalse(everDragged,
                $"{LogPrefix} ★ {what} — 보이지 않는 몸 자리 좌클릭이 드래그를 시작했습니다. 그 클릭은 아래 앱으로 갔는데 우리 상태기계가 반응했습니다(원칙 2).");
            Assert.IsFalse(lockHeld, $"{LogPrefix} ★ {what} — 숨은 채 연출 잠금을 쥐었습니다(뒤 연출이 «다른 연출 중»으로 막힙니다).");
            Assert.AreEqual(transitionsBefore, _transitions,
                $"{LogPrefix} ★ {what} — 숨은 동안 상태 전이가 {_transitions - transitionsBefore}건 일어났습니다.");
        }

        /// <summary>같은 씬에서 보이는 몸 좌클릭이 실제로 잡힌다(양성 대조) — 누름 → 드래그 → 뗌 → 드래그 끝.</summary>
        private IEnumerator AssertLeftClickGrabsVisibleBody(string what)
        {
            StickmanStateId before = _bb.Machine.CurrentStateId;
            if (before != StickmanStateId.Idle && before != StickmanStateId.Walk)
            {
                _bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                yield return Wait(SettleSeconds);
            }
            AssertVisibleBodyIsHit(what);
            _hitbox.ProcessGlobalButtonSample(true);
            Assert.AreEqual(StickmanStateId.Dragged, _bb.Machine.CurrentStateId,
                $"{LogPrefix} {what} — 양성 대조 실패: 보이는 몸 좌클릭이 드래그를 시작하지 않았습니다. 위 «안 잡힘» 판정이 무의미합니다.");
            Assert.IsTrue(SpectacleEventLock.IsActive, $"{LogPrefix} {what} — 양성 대조: 드래그인데 연출 잠금이 없습니다.");
            _hitbox.ProcessGlobalButtonSample(false);
            yield return TestClock.WaitUntil(() => _bb.Machine.CurrentStateId != StickmanStateId.Dragged,
                ReleaseTimeoutSeconds, "양성 대조 드래그의 놓기가 끝나지 않았습니다");
        }

        private void SetSuspendAxes(bool game, bool panelRetreat, bool virtualDesktop)
        {
            FullscreenAxisField.SetValue(_agent, game);
            PanelRetreatField.SetValue(_agent, panelRetreat);
            VirtualDesktopField.SetValue(_agent, virtualDesktop);
            ApplyDecisionMethod.Invoke(_agent, null);
        }

        // ==================== L0 — 양성 대조 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator L0_양성대조_보이는_몸을_좌클릭하면_공통_입구로_잡힌다()
        {
            yield return LoadScene();
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 새 씬인데 캐릭터가 숨어 있습니다.");

            yield return AssertLeftClickGrabsVisibleBody("L0");
            Assert.AreEqual(1, _mouseDownCount, $"{LogPrefix} L0 — 보이는 몸 누름 한 번에 공통 입구의 MouseDown이 {_mouseDownCount}번 났습니다(기대 1).");
            Debug.Log($"{LogPrefix} L0 확인 — 보이는 몸 좌클릭은 공통 입구로 드래그를 시작하고, 레이캐스트도 몸에 맞습니다.");
        }

        // ==================== L1 · L2 · L3 · L6 — 숨은 몸 자리 좌클릭은 아무것도 시작하지 않는다 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator L1_사용자_숨김_중_보이지_않는_몸_자리_좌클릭은_잡기를_시작하지_않는다()
        {
            yield return LoadScene();
            _agent.SetUserHidden(true, "L1 — 사용자 숨김");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended && _agent.IsUserHiddenOnly, $"{LogPrefix} L1 전제 — 사용자 숨김 단독이 아닙니다.");
            AssertInvisibleBodyTrap("L1");

            yield return AssertLeftClickStartsNothing("L1 사용자 숨김");

            _agent.SetUserHidden(false, "L1 양성 대조");
            yield return Wait(SettleSeconds);
            yield return AssertLeftClickGrabsVisibleBody("L1 숨김 해제 뒤");
            Debug.Log($"{LogPrefix} L1 확인 — 사용자 숨김 중 보이지 않는 몸 자리 좌클릭은 아무것도 시작하지 않았고, 풀자 같은 자리가 잡혔습니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator L2_전체화면_게임_등급2_중_보이지_않는_몸_자리_좌클릭은_잡기를_시작하지_않는다()
        {
            yield return LoadScene();
            SetSuspendAxes(game: true, panelRetreat: true, virtualDesktop: false);
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended && _agent.HidesScreenSurfaces, $"{LogPrefix} L2 전제 — 등급 2 숨김이 서지 않았습니다.");
            AssertInvisibleBodyTrap("L2");

            yield return AssertLeftClickStartsNothing("L2 전체화면 게임");

            SetSuspendAxes(game: false, panelRetreat: false, virtualDesktop: false);
            yield return Wait(SettleSeconds);
            yield return AssertLeftClickGrabsVisibleBody("L2 등급 해제 뒤");
            Debug.Log($"{LogPrefix} L2 확인 — 전체화면 게임 중 게임 클릭이 우리 상태기계를 건드리지 않았습니다(원칙 2).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator L3_등급1_더하기_사용자_숨김_중_보이지_않는_몸_자리_좌클릭은_잡기를_시작하지_않는다()
        {
            yield return LoadScene();
            PanelRetreatField.SetValue(_agent, true);
            _agent.SetUserHidden(true, "L3 — 등급 1 + 사용자 숨김");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended && _agent.IsUserHiddenOnly && _agent.ArePanelsSuppressed,
                $"{LogPrefix} L3 전제 — 등급 1 + 사용자 숨김이 아닙니다.");
            AssertInvisibleBodyTrap("L3");

            yield return AssertLeftClickStartsNothing("L3 등급 1 + 사용자 숨김");

            _agent.SetUserHidden(false, "L3 양성 대조");
            yield return Wait(SettleSeconds);
            yield return AssertLeftClickGrabsVisibleBody("L3 숨김 해제 뒤(등급 1 유지)");
            Debug.Log($"{LogPrefix} L3 확인 — 등급 1 + 숨김 중 보이지 않는 몸 자리 좌클릭은 아무것도 시작하지 않았습니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator L6_다른_가상_데스크톱에_있는_동안_보이지_않는_몸_자리_좌클릭은_잡기를_시작하지_않는다()
        {
            yield return LoadScene();
            SetSuspendAxes(game: false, panelRetreat: false, virtualDesktop: true);
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended && _agent.HidesScreenSurfaces, $"{LogPrefix} L6 전제 — 축 4 숨김이 서지 않았습니다.");
            AssertInvisibleBodyTrap("L6");

            yield return AssertLeftClickStartsNothing("L6 다른 가상 데스크톱");

            SetSuspendAxes(game: false, panelRetreat: false, virtualDesktop: false);
            yield return Wait(SettleSeconds);
            yield return AssertLeftClickGrabsVisibleBody("L6 축 4 해제 뒤");
            Debug.Log($"{LogPrefix} L6 확인 — 다른 가상 데스크톱에 있는 동안 좌클릭은 아무것도 시작하지 않았습니다(축 4는 플랫폼 중립 필드로 주입).");
        }

        // ==================== L4 — 가출 은신의 「찾기」 좌클릭은 보존 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator L4_가출_은신_중_보이지_않는_캐릭터를_좌클릭하면_찾기가_그대로_된다()
        {
            yield return LoadScene();
            CommandAvailability availability = _runaway.GetForcedRunawayAvailability();
            Assert.IsTrue(availability.IsReady, $"{LogPrefix} L4 전제 — 가출 강제 발동 불가(\"{availability.Reason}\").");
            Assert.IsTrue(_runaway.TryForceRunawayNow(LogPrefix), $"{LogPrefix} L4 전제 — 가출이 시작되지 않았습니다.");
            float hideTimeout = _config.runawayFleeDurationSeconds + RunawayHideSlackSeconds;
            yield return TestClock.WaitUntil(() => _bb.IsCharacterHiddenByRunaway, hideTimeout, "가출 은신(Hidden)에 들어가지 않았습니다");

            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} L4 전제 — 가출 은신인데 캐릭터 축 숨김(Suspend)이 켜졌습니다. 이 케이스는 Suspend가 아닌 은신이어야 합니다.");
            // 가출 은신은 Kinematic이라 몸이 물리에 남는다 — 레이캐스트가 맞아 그 자리 클릭은 우리 창이 받는다(「찾기」 설계).
            AssertVisibleBodyIsHit("L4 가출 은신");

            int downBefore = _mouseDownCount;
            _hitbox.ProcessGlobalButtonSample(true);
            Assert.AreEqual(downBefore + 1, _mouseDownCount,
                $"{LogPrefix} ★ L4 — 가출 은신 자리 좌클릭이 공통 입구를 통과하지 못했습니다. 「찾기」가 죽었습니다 — 좌클릭 게이트에 가출 은신까지 넣은 형태입니다.");
            yield return TestClock.WaitUntil(() => !_bb.IsCharacterHiddenByRunaway, EdgeTimeoutSeconds,
                "좌클릭 뒤 가출 은신이 «발견됨»으로 넘어가지 않았습니다");
            _hitbox.ProcessGlobalButtonSample(false);
            Debug.Log($"{LogPrefix} L4 확인 — 가출 은신(Suspend 아님)의 보이지 않는 캐릭터를 좌클릭하자 찾기가 됐습니다.");
        }

        // ==================== L4b(L4′) — 숨김 중 공통 입구는 MouseDown을 아예 내지 않는다 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator L4b_숨김_중에는_공통_입구가_받는_곳_다섯_중_어디에도_MouseDown을_보내지_않는다()
        {
            yield return LoadScene();
            _agent.SetUserHidden(true, "L4b — 사용자 숨김");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} L4b 전제 — 숨김이 서지 않았습니다.");
            AssertInvisibleBodyTrap("L4b");

            _mouseDownCount = 0;
            _hitbox.ProcessGlobalButtonSample(true);
            yield return null;
            _hitbox.ProcessGlobalButtonSample(false);
            Assert.AreEqual(0, _mouseDownCount,
                $"{LogPrefix} ★ L4b — 숨김 중 보이지 않는 몸 자리 누름이 공통 입구의 MouseDown을 {_mouseDownCount}번 냈습니다. " +
                "받는 곳(드래그 · 가출 찾기 · 과자 · 활쏘기 · 스트레스 게이지)이 각자 막지 않으면 전부 반응합니다 — 게이트는 입구에 있어야 합니다.");

            // 양성 대조 — 같은 측정이 «났다»를 볼 수 있다.
            _agent.SetUserHidden(false, "L4b 양성 대조");
            yield return Wait(SettleSeconds);
            yield return AssertLeftClickGrabsVisibleBody("L4b 숨김 해제 뒤");
            Assert.AreEqual(1, _mouseDownCount, $"{LogPrefix} L4b 양성 대조 — 보이는 몸 누름에 MouseDown이 {_mouseDownCount}번 났습니다(기대 1).");
            Debug.Log($"{LogPrefix} L4b 확인 — 숨김 중 공통 입구는 MouseDown을 내지 않았고, 풀자 한 번 냈습니다.");
        }

        // ==================== L5 — 보존 동결 중 공통 입구 판 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator L5_보존_동결_중_공통_입구로_눌러도_드래그는_미뤄지고_해제_뒤에는_잡힌다()
        {
            yield return LoadScene();
            InvokeStatic(PublishStartedMethod, 41, DisplayChangeHoldStartReason.TopologyChangeDetected, Time.frameCount);
            yield return TestClock.WaitUntil(() => _agent.IsPreservationFrozen, EdgeTimeoutSeconds, "유예 게시 뒤 보존 동결이 걸리지 않았습니다");
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} L5 전제 — 보존 동결은 숨김이 아닌데 IsSuspended가 참입니다.");

            // 동결은 캐릭터가 보인다 — 공통 입구는 통과하고(MouseDown), 드래그 쪽 보존 동결 가드가 전이를 미룬다.
            yield return AssertLeftClickStartsNothing("L5 보존 동결(공통 입구 경유)");

            InvokeStatic(PublishReleasedMethod, 41, DisplayChangeHoldReleaseReason.Settled, Time.frameCount);
            yield return TestClock.WaitUntil(() => !_agent.IsPreservationFrozen, EdgeTimeoutSeconds, "유예 해제 뒤 동결이 풀리지 않았습니다");
            yield return AssertLeftClickGrabsVisibleBody("L5 동결 해제 뒤");
            Debug.Log($"{LogPrefix} L5 확인 — 보존 동결 중 공통 입구 경유 좌클릭은 드래그를 미뤘고, 해제 뒤 같은 입구로 잡혔습니다.");
        }
    }
}
