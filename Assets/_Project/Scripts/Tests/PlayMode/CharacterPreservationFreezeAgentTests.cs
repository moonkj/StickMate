using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 2026-09-14 — 화면 변경 유예 동안의 캐릭터 「보존 동결」 실측 잠금(리더 판정 + design-narrative 조건 1·2·4·6·7).
    ///
    /// <para><b>유예 상태를 세우는 방법</b>: 계약(<see cref="DisplayChangeHoldStatus"/>)의 쓰기는 <c>internal</c>이고
    /// InternalsVisibleTo는 EditMode 어셈블리에만 열려 있다. 그래서 리플렉션으로 부른다 — 이름이 바뀌면
    /// <see cref="테스트_훅이_실재한다"/>가 시끄럽게 빨개진다(부재가 조용히 초록이 되지 않게).</para>
    ///
    /// <para><b>시간 예산은 전부 벽시계다</b>(CLAUDE.md). 동결 창·대조 창은 초 단위이고, 허용 오차는 그 창에서 유도한다.</para>
    ///
    /// <para><b>TearDown이 유예 상태를 가장 먼저 되돌린다</b> — 새면 뒤 PlayMode 테스트의 캐릭터가 전부 얼어붙는다.</para>
    /// </summary>
    public sealed class CharacterPreservationFreezeAgentTests
    {
        private const string LogPrefix = "[보존동결-TEST]";

        private const float SettleSeconds = 0.7f;
        /// <summary>동결을 유지하는 벽시계 창(초).</summary>
        private const float FreezeWindowSeconds = 0.6f;
        /// <summary>동결 밖에서 「흐른다」를 확인하는 창(초).</summary>
        private const float ControlWindowSeconds = 0.2f;
        private const float EdgeTimeoutSeconds = 1.0f;
        /// <summary>반응 대사가 가독예산을 채우고 페이드아웃에 들어가기까지의 넉넉한 상한(테스트 소유 값).</summary>
        private const float FadeOutTimeoutSeconds = 10f;
        /// <summary>연출 상태가 테스트 중 스스로 끝나지 않게 하는 길이(테스트 소유 값).</summary>
        private const float LongPoseHoldSeconds = 60f;
        /// <summary>테스트 말풍선을 잡담이 갈아끼우지 못하게 쿨다운을 멀리 민다.</summary>
        private const float FarFutureChatterSeconds = 3600f;
        /// <summary>재기점 확인용 쿨다운(동결 창보다 충분히 길다).</summary>
        private const float ChatterProbeSeconds = 30f;
        private const float LiftUnits = 4f;
        private const float FallControlSeconds = 0.08f;

        private static readonly MethodInfo PublishStartedMethod =
            typeof(DisplayChangeHoldStatus).GetMethod("PublishStarted", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo PublishReleasedMethod =
            typeof(DisplayChangeHoldStatus).GetMethod("PublishReleased", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ResetStatusMethod =
            typeof(DisplayChangeHoldStatus).GetMethod("ResetForTesting", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo AgentPollerField =
            typeof(StickmanAgent).GetField("_footholdPoller", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DragMouseDownMethod =
            typeof(DragThrowController).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DragMouseUpMethod =
            typeof(DragThrowController).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class CountingService : IPlatformWindowService
        {
            private readonly IPlatformWindowService _inner;
            public int EnumerateCalls;
            public CountingService(IPlatformWindowService inner) { _inner = inner; }
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() { EnumerateCalls++; return _inner.EnumerateFootholds(); }
            public bool CreateOverlayWindow() => _inner.CreateOverlayWindow();
            public void SetClickThrough(bool enabled) => _inner.SetClickThrough(enabled);
            public void SetAlwaysOnTop(bool enabled) => _inner.SetAlwaysOnTop(enabled);
            public bool IsFullscreenAppActive() => _inner.IsFullscreenAppActive();
        }

        private StickmanAgent _agent;
        private StickmanBlackboard _bb;
        private DialogueBubbleRenderer _bubble;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private FootholdPoller _originalAgentPoller;
        private FootholdPoller _originalBbPoller;
        private FootholdPoller _countingPoller;
        private CountingService _counting;
        private int _transitions;

        private void OnStateTransitioned(StateTransitionEvent evt) => _transitions++;

        // ==================== 준비 / 정리 ====================

        private static void InvokeStatic(MethodInfo method, params object[] args)
        {
            Assert.IsNotNull(method, $"{LogPrefix} 유예 계약의 테스트 훅을 찾지 못했습니다 — 이름이 바뀌었으면 이 파일을 고치십시오.");
            method.Invoke(null, args);
        }

        private static void StartHold(int episode) => InvokeStatic(PublishStartedMethod,
            episode, DisplayChangeHoldStartReason.TopologyChangeDetected, Time.frameCount);

        private static void StopHold(int episode) => InvokeStatic(PublishReleasedMethod,
            episode, DisplayChangeHoldReleaseReason.Settled, Time.frameCount);

        private static void ResetHold()
        {
            if (ResetStatusMethod != null) ResetStatusMethod.Invoke(null, null);
        }

        private IEnumerator LoadAgent(bool countingPoller)
        {
            ResetHold();
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            _bubble = _agent.GetComponent<DialogueBubbleRenderer>();
            Assert.IsNotNull(_bubble, $"{LogPrefix} 캐릭터에 DialogueBubbleRenderer가 없습니다.");
            yield return new WaitForSeconds(SettleSeconds);

            _bb = _agent.Blackboard;
            _originalConfig = _bb.Config;
            _clonedConfig = Object.Instantiate(_originalConfig);
            _bb.Config = _clonedConfig;
            _clonedConfig.pomodoroStartPoseHoldSeconds = LongPoseHoldSeconds;
            _bb.NextChatterAllowedUnscaledTime = Time.unscaledTime + FarFutureChatterSeconds;

            if (countingPoller)
            {
                Assert.IsNotNull(AgentPollerField, $"{LogPrefix} 에이전트의 발판 폴러 필드를 찾지 못했습니다.");
                _originalAgentPoller = (FootholdPoller)AgentPollerField.GetValue(_agent);
                _originalBbPoller = _bb.FootholdPoller;
                _counting = new CountingService(_agent.PlatformService);
                _countingPoller = new FootholdPoller(_counting, _clonedConfig);
                AgentPollerField.SetValue(_agent, _countingPoller);
                _bb.FootholdPoller = _countingPoller;
                _countingPoller.PollImmediately();
            }

            _transitions = 0;
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
        }

        [TearDown]
        public void TearDown()
        {
            ResetHold();   // ★ 가장 먼저.
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;

            if (_agent != null && _agent.IsUserHidden) _agent.SetUserHidden(false, LogPrefix);
            if (_bb != null)
            {
                if (_originalConfig != null) _bb.Config = _originalConfig;
                if (_originalBbPoller != null) _bb.FootholdPoller = _originalBbPoller;
            }
            if (_agent != null && _originalAgentPoller != null && AgentPollerField != null)
                AgentPollerField.SetValue(_agent, _originalAgentPoller);
            if (_clonedConfig != null) Object.Destroy(_clonedConfig);

            _agent = null;
            _bb = null;
            _bubble = null;
            _originalConfig = null;
            _clonedConfig = null;
            _originalAgentPoller = null;
            _originalBbPoller = null;
            _countingPoller = null;
            _counting = null;
        }

        [Test]
        public void 테스트_훅이_실재한다()
        {
            Assert.IsNotNull(PublishStartedMethod, "DisplayChangeHoldStatus.PublishStarted");
            Assert.IsNotNull(PublishReleasedMethod, "DisplayChangeHoldStatus.PublishReleased");
            Assert.IsNotNull(ResetStatusMethod, "DisplayChangeHoldStatus.ResetForTesting");
            Assert.IsNotNull(AgentPollerField, "StickmanAgent._footholdPoller");
            Assert.IsNotNull(DragMouseDownMethod, "DragThrowController.OnMouseDown");
            Assert.IsNotNull(DragMouseUpMethod, "DragThrowController.OnMouseUp");
        }

        private IEnumerator EnterFrozen(int episode)
        {
            StartHold(episode);
            yield return TestClock.WaitUntil(() => _agent.IsPreservationFrozen && _bubble.IsLifetimeClockHeld,
                EdgeTimeoutSeconds, "유예 게시 뒤 에이전트 동결과 말풍선 시계 붙잡기가 걸리지 않았습니다");
        }

        private IEnumerator LeaveFrozen(int episode)
        {
            StopHold(episode);
            yield return TestClock.WaitUntil(() => !_agent.IsPreservationFrozen && !_bubble.IsLifetimeClockHeld,
                EdgeTimeoutSeconds, "유예 해제 뒤 동결이 풀리지 않았습니다");
        }

        private LineRenderer FindHeadRing()
        {
            foreach (LineRenderer lr in _agent.GetComponentsInChildren<LineRenderer>(true))
                if (lr != null && lr.gameObject.name == StickmanMetrics.HeadRingObjectName) return lr;
            return null;
        }

        private IEnumerator LiftAndStartFalling()
        {
            Vector2 start = _bb.Body.position;
            _bb.MoveBodyToWorld(start + Vector2.up * LiftUnits);
            _bb.Body.linearVelocity = Vector2.zero;
            _bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);
            float liftedY = _bb.Body.position.y;
            yield return new WaitForSeconds(FallControlSeconds);
            Assert.Less(_bb.Body.position.y, liftedY - 1e-3f,
                $"{LogPrefix} 음성 대조 실패 — 들어 올린 몸이 {FallControlSeconds:F2}초 동안 떨어지지 않았습니다(아래 정지 판정이 무의미).");
        }

        // ==================== (a)(b)(d) + 조건 7 ====================

        [UnityTest]
        public IEnumerator 동결_중에는_상태Tick_말풍선수명_잡담쿨다운이_멈추고_렌더러와_표면은_그대로다()
        {
            yield return LoadAgent(countingPoller: false);
            var focus = _bb.Machine.GetState(StickmanStateId.FocusStart) as TimedSpectacleState;
            Assert.IsNotNull(focus, $"{LogPrefix} 전제 실패 — FocusStart가 시간 연출 상태가 아닙니다.");

            _bb.Machine.ChangeState(StickmanStateId.FocusStart, isForcedInterrupt: true);
            yield return TestClock.WaitUntil(() => _bubble.IsBubbleVisible && _bubble.CurrentAlpha >= 1f,
                EdgeTimeoutSeconds, "FocusStart 말풍선이 완전히 뜨지 않았습니다");

            // 음성 대조 — 동결 전에는 둘 다 흐른다.
            float p0 = focus.Progress01;
            float v0 = _bubble.VisibleSeconds;
            float wall0 = Time.unscaledTime;
            yield return new WaitForSeconds(ControlWindowSeconds);
            float wallControl = Time.unscaledTime - wall0;
            Assert.Greater(focus.Progress01, p0, $"{LogPrefix} 음성 대조 실패 — 동결 전인데 상태 Tick이 흐르지 않습니다.");
            Assert.Greater(_bubble.VisibleSeconds - v0, wallControl * 0.5f,
                $"{LogPrefix} 음성 대조 실패 — 동결 전인데 말풍선 수명 시계가 흐르지 않습니다.");

            _bb.NextChatterAllowedUnscaledTime = Time.unscaledTime + ChatterProbeSeconds;
            LineRenderer headRing = FindHeadRing();
            Assert.IsNotNull(headRing, $"{LogPrefix} 전제 실패 — 머리 링을 찾지 못했습니다.");
            Assert.IsTrue(headRing.enabled, $"{LogPrefix} 전제 실패 — 동결 전에 머리 링이 꺼져 있습니다.");

            yield return EnterFrozen(1);
            float remainingAtFreeze = _bb.NextChatterAllowedUnscaledTime - Time.unscaledTime;
            float pF = focus.Progress01;
            float vF = _bubble.VisibleSeconds;

            float progressDrift = 0f, visibleDrift = 0f;
            bool bubbleStayed = true, stateStayed = true, surfacesStayed = true, rendererStayed = true;
            int frames = 0;
            yield return TestClock.SampleForSeconds(FreezeWindowSeconds, t =>
            {
                frames++;
                progressDrift = Mathf.Max(progressDrift, Mathf.Abs(focus.Progress01 - pF));
                visibleDrift = Mathf.Max(visibleDrift, Mathf.Abs(_bubble.VisibleSeconds - vF));
                if (!_bubble.IsBubbleVisible) bubbleStayed = false;
                if (_bb.Machine.CurrentStateId != StickmanStateId.FocusStart) stateStayed = false;
                if (_agent.IsSuspended || _agent.HidesScreenSurfaces || _agent.ArePanelsSuppressed) surfacesStayed = false;
                if (!headRing.enabled) rendererStayed = false;
            });

            Debug.Log($"{LogPrefix} 동결 {FreezeWindowSeconds:F2}초/{frames}프레임 — 진행도 드리프트 {progressDrift:E2}, " +
                $"말풍선 노출 드리프트 {visibleDrift:E2}초, 쿨다운 잔여(동결 시작) {remainingAtFreeze:F3}초.");

            Assert.LessOrEqual(progressDrift, 1e-6f, $"{LogPrefix} (a) 동결 중 상태 Tick이 흘렀습니다.");
            Assert.LessOrEqual(visibleDrift, 1e-4f, $"{LogPrefix} (a) 동결 중 말풍선 수명 시계가 흘렀습니다.");
            Assert.IsTrue(stateStayed, $"{LogPrefix} (a) 동결 중 상태가 바뀌었습니다.");
            Assert.IsTrue(bubbleStayed, $"{LogPrefix} (b) 동결 중 떠 있던 말풍선이 사라졌습니다 — 숨기지 않는 정지여야 합니다.");
            Assert.IsTrue(rendererStayed, $"{LogPrefix} (b) 동결 중 캐릭터 렌더러가 꺼졌습니다 — 전체화면 숨김과 섞였습니다.");
            Assert.IsTrue(surfacesStayed, $"{LogPrefix} (d) 동결이 IsSuspended/HidesScreenSurfaces/ArePanelsSuppressed를 켰습니다.");

            yield return LeaveFrozen(1);
            float remainingAfterThaw = _bb.NextChatterAllowedUnscaledTime - Time.unscaledTime;
            float vThaw = _bubble.VisibleSeconds;
            // 재기점이 없으면 두 값이 동결 창만큼 갈라진다. 그 1/4을 허용 오차로 쓴다(한두 프레임의 가장자리 오차를 흡수).
            float tolerance = FreezeWindowSeconds * 0.25f;
            Assert.AreEqual(remainingAtFreeze, remainingAfterThaw, tolerance,
                $"{LogPrefix} 조건 7 — 잡담 쿨다운이 동결 길이만큼 밀리지 않았습니다.");
            Assert.AreEqual(vF, vThaw, tolerance, $"{LogPrefix} 조건 3 — 해제 뒤 말풍선 수명이 동결 전 자리에서 이어지지 않았습니다.");
            Assert.IsTrue(_bubble.IsBubbleVisible, $"{LogPrefix} 조건 8 — 해제 순간 말풍선이 조기 종료됐습니다.");

            float p1 = focus.Progress01;
            yield return new WaitForSeconds(ControlWindowSeconds);
            Assert.Greater(focus.Progress01, p1, $"{LogPrefix} 해제 뒤 상태 Tick이 다시 흐르지 않습니다.");
            Assert.Greater(_bubble.VisibleSeconds, vThaw, $"{LogPrefix} 해제 뒤 말풍선 수명 시계가 다시 흐르지 않습니다.");
        }

        // ==================== 물리·발판 폴링·해제 재폴·불연속 ====================

        [UnityTest]
        public IEnumerator 동결_중에는_물리와_발판폴링이_멈추고_해제_때_발판을_한_번_다시_읽고_튀지_않는다()
        {
            yield return LoadAgent(countingPoller: true);

            // 교정 — 즉시 재폴 한 번이 열거를 몇 번 부르는지 추정하지 않고 잰다.
            int c = _counting.EnumerateCalls;
            _countingPoller.PollImmediately();
            int callsPerPoll = _counting.EnumerateCalls - c;
            Assert.Greater(callsPerPoll, 0, $"{LogPrefix} 교정 실패 — 즉시 재폴이 열거를 부르지 않습니다.");

            // 음성 대조 — 동결 전에는 폴러가 스스로 돈다.
            c = _counting.EnumerateCalls;
            yield return new WaitForSeconds(Mathf.Max(_clonedConfig.footholdPollInterval, ControlWindowSeconds) * 2.5f);
            Assert.Greater(_counting.EnumerateCalls, c, $"{LogPrefix} 음성 대조 실패 — 동결 전인데 발판 폴링이 돌지 않습니다.");

            yield return LiftAndStartFalling();

            yield return EnterFrozen(2);
            Vector2 posF = _bb.Body.position;
            Vector2 velF = _bb.Body.linearVelocity;
            StickmanStateId stateF = _bb.Machine.CurrentStateId;
            int callsF = _counting.EnumerateCalls;
            Assert.IsFalse(_bb.Body.simulated, $"{LogPrefix} 동결 중 전신 물리가 꺼져 있어야 합니다.");

            float posDrift = 0f, velDrift = 0f;
            bool stateStayed = true;
            int callsDrift = 0;
            yield return TestClock.SampleForSeconds(FreezeWindowSeconds, t =>
            {
                posDrift = Mathf.Max(posDrift, Vector2.Distance(_bb.Body.position, posF));
                velDrift = Mathf.Max(velDrift, Vector2.Distance(_bb.Body.linearVelocity, velF));
                if (_bb.Machine.CurrentStateId != stateF) stateStayed = false;
                callsDrift = Mathf.Max(callsDrift, _counting.EnumerateCalls - callsF);
            });

            Assert.LessOrEqual(posDrift, 1e-5f, $"{LogPrefix} 동결 중 몸이 움직였습니다({posDrift:E2}유닛).");
            Assert.LessOrEqual(velDrift, 1e-4f, $"{LogPrefix} 동결 중 속도가 바뀌었습니다 — 재개 때 동결 전 속도로 이어지지 않습니다.");
            Assert.IsTrue(stateStayed, $"{LogPrefix} 동결 중 상태가 바뀌었습니다.");
            Assert.AreEqual(0, callsDrift, $"{LogPrefix} 동결 중 발판 폴링이 돌았습니다.");

            int callsBeforeRelease = _counting.EnumerateCalls;
            yield return LeaveFrozen(2);
            Assert.AreEqual(callsPerPoll, _counting.EnumerateCalls - callsBeforeRelease,
                $"{LogPrefix} (c) 해제 프레임에 발판을 정확히 한 번 다시 읽어야 합니다(누락이면 0, 중복이면 배수).");
            Assert.IsTrue(_bb.Body.simulated, $"{LogPrefix} 해제 뒤 전신 물리가 켜지지 않았습니다.");
            Assert.LessOrEqual(Vector2.Distance(_bb.Body.position, posF), 1e-5f,
                $"{LogPrefix} (c) 해제 프레임에 위치가 순간이동했습니다.");

            // 해제 직후 한 프레임의 이동 상한 = 동결 전 속도 + 중력으로 (그 프레임 시간 + 물리 한 스텝) 동안 갈 수 있는 거리.
            Vector2 posThaw = _bb.Body.position;
            float wallA = Time.unscaledTime;
            yield return null;
            float span = (Time.unscaledTime - wallA) + Time.fixedDeltaTime;
            float g = Physics2D.gravity.magnitude * Mathf.Abs(_bb.Body.gravityScale);
            float bound = velF.magnitude * span + 0.5f * g * span * span + 1e-3f;
            float step = Vector2.Distance(_bb.Body.position, posThaw);
            float jumpIfClockRan = velF.magnitude * FreezeWindowSeconds + 0.5f * g * FreezeWindowSeconds * FreezeWindowSeconds;
            Debug.Log($"{LogPrefix} 해제 직후 이동 {step:F4}유닛(상한 {bound:F4}), 동결 없이 흘렀다면 약 {jumpIfClockRan:F3}유닛.");
            Assert.Greater(jumpIfClockRan, bound * 4f,
                $"{LogPrefix} 전제 실패 — 동결 창 동안의 낙하가 한 프레임 상한과 구분되지 않습니다(판정 불가).");
            Assert.LessOrEqual(step, bound, $"{LogPrefix} (c) 해제 직후 한 프레임에 동결 전 속도로 설명되지 않는 튐이 있습니다.");
        }

        // ==================== 조건 1 — 숨김과 겹쳐도 한쪽 해제가 다른 쪽을 풀지 않는다 ====================

        [UnityTest]
        public IEnumerator 동결_중_숨김을_켰다_끄면_Resume이_불려도_물리는_계속_멈춰_있다()
        {
            yield return LoadAgent(countingPoller: false);
            yield return LiftAndStartFalling();

            yield return EnterFrozen(3);
            Vector2 posF = _bb.Body.position;

            _agent.SetUserHidden(true, LogPrefix);
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 전제 실패 — 사용자 숨김이 Suspend를 부르지 않았습니다.");
            _agent.SetUserHidden(false, LogPrefix);
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 전제 실패 — 숨김 해제가 Resume을 부르지 않았습니다.");
            Assert.IsTrue(_agent.IsPreservationFrozen, $"{LogPrefix} 숨김 왕복이 동결을 풀었습니다.");
            Assert.IsFalse(_bb.Body.simulated, $"{LogPrefix} 조건 1 — 유예 중 Resume()이 물리를 켰습니다.");

            float drift = 0f;
            yield return TestClock.SampleForSeconds(ControlWindowSeconds, t =>
                drift = Mathf.Max(drift, Vector2.Distance(_bb.Body.position, posF)));
            Assert.LessOrEqual(drift, 1e-5f, $"{LogPrefix} 조건 1 — 숨김 왕복 뒤 동결 중인데 몸이 움직였습니다.");

            yield return LeaveFrozen(3);
            Assert.IsTrue(_bb.Body.simulated, $"{LogPrefix} 동결 해제 뒤 물리가 켜지지 않았습니다.");

            // 반대 순서 — 숨김 중에 유예가 시작·해제되면 숨김이 풀릴 때까지 물리는 꺼져 있어야 한다.
            _agent.SetUserHidden(true, LogPrefix);
            yield return EnterFrozen(4);
            yield return LeaveFrozen(4);
            Assert.IsFalse(_bb.Body.simulated, $"{LogPrefix} 조건 1 — 숨김이 남아 있는데 동결 해제가 물리를 켰습니다.");
            _agent.SetUserHidden(false, LogPrefix);
            Assert.IsTrue(_bb.Body.simulated, $"{LogPrefix} 둘 다 풀렸는데 물리가 꺼져 있습니다.");
        }

        // ==================== (e) + 조건 6 — 새어 나온 대사 ====================

        [UnityTest]
        public IEnumerator 동결_중_전이에서_새어_나온_대사는_버리고_해제_뒤_재발화하지_않는다()
        {
            yield return LoadAgent(countingPoller: false);
            _bubble.HideImmediate();

            yield return EnterFrozen(5);
            int dropped0 = _bubble.PreservationFreezeDroppedDialogueCount;
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(DialogueBubbleRenderer.PreservationFreezeDropLogTag)));

            // 입구를 거치지 않는 직접 전이 = 새는 경로.
            _bb.Machine.ChangeState(StickmanStateId.FocusStart, isForcedInterrupt: true);
            Assert.AreEqual(dropped0 + 1, _bubble.PreservationFreezeDroppedDialogueCount,
                $"{LogPrefix} (e) 동결 중 전이의 대사가 렌더러 입구에서 버려지지 않았습니다.");
            Assert.IsFalse(_bubble.IsBubbleVisible, $"{LogPrefix} (e) 동결 중 새 말풍선이 떴습니다.");

            yield return new WaitForSeconds(ControlWindowSeconds);
            Assert.IsFalse(_bubble.IsBubbleVisible, $"{LogPrefix} (e) 동결 중 말풍선이 늦게 떴습니다.");

            yield return LeaveFrozen(5);
            yield return new WaitForSeconds(ControlWindowSeconds);
            Assert.IsFalse(_bubble.IsBubbleVisible, $"{LogPrefix} 조건 6 — 해제 뒤 버린 대사가 재발화됐습니다(대기열 금지).");

            // 양성 대조 — 같은 전이를 동결 밖에서 하면 뜬다.
            _bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            _bb.Machine.ChangeState(StickmanStateId.FocusStart, isForcedInterrupt: true);
            yield return TestClock.WaitUntil(() => _bubble.IsBubbleVisible, EdgeTimeoutSeconds,
                "양성 대조 실패 — 동결 밖의 같은 전이가 말풍선을 띄우지 않습니다(위 0건이 무의미)");
        }

        // ==================== 조건 2 — 팝인·페이드인 진행 0 ====================

        [UnityTest]
        public IEnumerator 동결_중에는_팝인과_페이드인이_진행하지_않고_해제_뒤_이어진다()
        {
            yield return LoadAgent(countingPoller: false);
            _bubble.HideImmediate();

            _bb.Machine.ChangeState(StickmanStateId.FocusStart, isForcedInterrupt: true);
            yield return TestClock.WaitUntil(
                () => _bubble.IsBubbleVisible && _bubble.CurrentAlpha > 0f && _bubble.CurrentAlpha < 1f,
                EdgeTimeoutSeconds, "페이드인 한복판을 잡지 못했습니다");

            yield return EnterFrozen(6);
            Assert.IsTrue(_bubble.IsBubbleVisible, $"{LogPrefix} 조건 4 — 그려지기 시작한 말풍선이 동결 진입에 제거됐습니다.");
            float alphaF = _bubble.CurrentAlpha;
            float popF = _bubble.PopElapsedSeconds;
            Assert.Less(alphaF, 1f, $"{LogPrefix} 전제 실패 — 페이드인이 끝난 뒤에 붙잡혔습니다.");

            float alphaDrift = 0f, popDrift = 0f;
            bool fadedOut = false;
            yield return TestClock.SampleForSeconds(FreezeWindowSeconds, t =>
            {
                alphaDrift = Mathf.Max(alphaDrift, Mathf.Abs(_bubble.CurrentAlpha - alphaF));
                popDrift = Mathf.Max(popDrift, Mathf.Abs(_bubble.PopElapsedSeconds - popF));
                if (_bubble.IsFadingOut) fadedOut = true;
            });
            Assert.AreEqual(0f, alphaDrift, 1e-6f, $"{LogPrefix} 조건 2 — 동결 중 페이드인이 진행했습니다.");
            Assert.AreEqual(0f, popDrift, 1e-6f, $"{LogPrefix} 조건 2 — 동결 중 팝인이 진행했습니다.");
            Assert.IsFalse(fadedOut, $"{LogPrefix} 조건 2 — 동결 중 페이드아웃이 시작됐습니다.");

            yield return LeaveFrozen(6);
            yield return new WaitForSeconds(ControlWindowSeconds);
            Assert.Greater(_bubble.CurrentAlpha, alphaF, $"{LogPrefix} 해제 뒤 페이드인이 이어지지 않습니다.");
            Assert.Greater(_bubble.PopElapsedSeconds, popF, $"{LogPrefix} 해제 뒤 팝인이 이어지지 않습니다.");
        }

        // ==================== 조건 4 — 진입 순간 즉시 제거 두 경우 ====================

        [UnityTest]
        public IEnumerator 동결_진입_순간_한_번도_그려지지_않았거나_페이드아웃_중인_말풍선은_즉시_제거된다()
        {
            yield return LoadAgent(countingPoller: false);

            // (가) 한 번도 그려지지 않은 말풍선 — 뜬 프레임에 유예가 시작된다(실행 순서 미정, C6).
            _bubble.HideImmediate();
            int removed0 = _bubble.PreservationFreezeRemovalCount;
            _bb.Machine.ChangeState(StickmanStateId.FocusStart, isForcedInterrupt: true);
            Assert.IsTrue(_bubble.IsBubbleVisible, $"{LogPrefix} 전제 실패 — 전이 직후 말풍선이 등록되지 않았습니다.");
            Assert.AreEqual(0f, _bubble.CurrentAlpha, $"{LogPrefix} 전제 실패 — 이미 그려진 뒤입니다.");
            StartHold(7);
            yield return null;
            Assert.IsFalse(_bubble.IsBubbleVisible, $"{LogPrefix} 조건 4 — 알파 0 말풍선이 붙잡혔습니다.");
            Assert.AreEqual(removed0 + 1, _bubble.PreservationFreezeRemovalCount);
            yield return LeaveFrozen(7);
            yield return new WaitForSeconds(ControlWindowSeconds);
            Assert.IsFalse(_bubble.IsBubbleVisible, $"{LogPrefix} 조건 4 — 제거한 말풍선이 해제 뒤 튀어나왔습니다.");

            // (나) 페이드아웃 중인 말풍선.
            _bb.Machine.ChangeState(StickmanStateId.FocusStart, isForcedInterrupt: true);
            yield return TestClock.WaitUntil(() => _bubble.IsBubbleVisible && _bubble.CurrentAlpha >= 1f,
                EdgeTimeoutSeconds, "FocusStart 말풍선이 완전히 뜨지 않았습니다");
            _bb.Machine.ChangeState(StickmanStateId.Idle);   // 정상 종료 — 반응 대사는 가독예산을 채운 뒤 페이드아웃한다.
            yield return TestClock.WaitUntil(() => _bubble.IsBubbleVisible && _bubble.IsFadingOut,
                FadeOutTimeoutSeconds, "반응 대사가 페이드아웃에 들어가지 않았습니다");
            int removed1 = _bubble.PreservationFreezeRemovalCount;
            StartHold(8);
            yield return null;
            Assert.IsFalse(_bubble.IsBubbleVisible, $"{LogPrefix} 조건 4 — 페이드아웃 중이던 말풍선이 붙잡혔습니다.");
            Assert.AreEqual(removed1 + 1, _bubble.PreservationFreezeRemovalCount);
            yield return LeaveFrozen(8);
        }

        // ==================== 조건 6 — 입구에서 전이를 미룬다(거동) ====================

        [UnityTest]
        public IEnumerator 동결_중에는_가출_강제발동이_전이를_미루고_해제_뒤에는_된다()
        {
            yield return LoadAgent(countingPoller: false);
            var runaway = Object.FindFirstObjectByType<RunawayDirector>();
            Assert.IsNotNull(runaway, $"{LogPrefix} 씬에 RunawayDirector가 없습니다.");
            _bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            yield return EnterFrozen(9);
            _transitions = 0;
            bool started = runaway.TryForceRunawayNow(LogPrefix);
            yield return new WaitForSeconds(ControlWindowSeconds);
            Assert.IsFalse(started, $"{LogPrefix} 조건 6 — 동결 중 가출이 시작됐습니다.");
            Assert.AreEqual(0, _transitions, $"{LogPrefix} 조건 6 — 동결 중 상태 전이가 {_transitions}건 일어났습니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive, $"{LogPrefix} 동결 중 거절이 락을 쥔 채 남았습니다.");

            yield return LeaveFrozen(9);
            Assert.IsTrue(runaway.TryForceRunawayNow(LogPrefix),
                $"{LogPrefix} 양성 대조 실패 — 동결 밖의 같은 요청이 거절됩니다(위 0건이 무의미).");
            Assert.AreEqual(StickmanStateId.Runaway, _bb.Machine.CurrentStateId);
        }

        [UnityTest]
        public IEnumerator 동결_중에는_드래그_진입이_전이를_미루고_해제_뒤에는_된다()
        {
            yield return LoadAgent(countingPoller: false);
            var drag = Object.FindFirstObjectByType<DragThrowController>();
            Assert.IsNotNull(drag, $"{LogPrefix} 씬에 DragThrowController가 없습니다.");
            Assert.IsNotNull(DragMouseDownMethod, $"{LogPrefix} DragThrowController.OnMouseDown을 찾지 못했습니다.");
            Assert.IsNotNull(DragMouseUpMethod, $"{LogPrefix} DragThrowController.OnMouseUp을 찾지 못했습니다.");
            _bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            yield return EnterFrozen(10);
            _transitions = 0;
            DragMouseDownMethod.Invoke(drag, null);
            yield return new WaitForSeconds(ControlWindowSeconds);
            Assert.AreNotEqual(StickmanStateId.Dragged, _bb.Machine.CurrentStateId, $"{LogPrefix} 조건 6 — 동결 중 드래그가 시작됐습니다.");
            Assert.AreEqual(0, _transitions, $"{LogPrefix} 조건 6 — 동결 중 상태 전이가 {_transitions}건 일어났습니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive, $"{LogPrefix} 동결 중 드래그가 락을 쥐었습니다.");

            yield return LeaveFrozen(10);
            StickmanStateId before = _bb.Machine.CurrentStateId;
            Assert.IsTrue(before == StickmanStateId.Idle || before == StickmanStateId.Walk,
                $"{LogPrefix} 전제 실패 — 양성 대조 시점 상태가 {before}라 드래그 입구 조건을 못 맞춥니다.");
            DragMouseDownMethod.Invoke(drag, null);
            Assert.AreEqual(StickmanStateId.Dragged, _bb.Machine.CurrentStateId,
                $"{LogPrefix} 양성 대조 실패 — 동결 밖의 드래그 진입이 안 됩니다(위 0건이 무의미).");
            DragMouseUpMethod.Invoke(drag, null);
            yield return TestClock.WaitUntil(() => _bb.Machine.CurrentStateId != StickmanStateId.Dragged,
                EdgeTimeoutSeconds, "드래그 놓기 정리가 끝나지 않았습니다");
        }
    }
}
