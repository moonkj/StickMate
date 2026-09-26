using System;
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
using Object = UnityEngine.Object;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>사용자 허가(임대)는 사용자가 부르지 않은 표면을 드러내지 않는다</b> — 2026-09-15 N-20 잠금.
    /// 설계 정본: <c>docs/systems/AUTO_SURFACE_LEASE_AXIS.md</c> §6-4 · ROADMAP §60-9 N-20 해제 조건 3.
    ///
    /// ============================================================================
    /// 이 테스트가 도는 세계 — <b>대기 톱니 우회를 끈 프로덕션 기본</b> (TEAM.md 「스위트 전역 격리가 프로덕션 기본을 바꾼다」 규칙 2·3)
    /// ============================================================================
    /// <c>GlobalPlayModeTestIsolation</c>이 어셈블리 전체에서 대기 톱니 게이트를 우회한다. 우회된 세계에서는 톱니 위젯의 갱신·차단막
    /// 모양이 달라지므로 이 픽스처는 <c>PanelsOnlyTierMouseEntryTests</c>와 같은 방식으로 <b>매 테스트 SetUp에서 우회를 끄고</b>
    /// TearDown에서 픽스처 시작 값으로 되돌린다. 테스트 이름의 「프로덕션_톱니세계」가 그 선언이고, 각 테스트는 전제 단언으로
    /// <c>IsStandbyGateBypassedForTests == false</c>를 확인한다. 등급은 에이전트 private 필드 리플렉션으로 주입한다(주입 지점이 없다 —
    /// 이 어셈블리의 기존 관례). 폴링이 주입을 덮지 못하게 <c>fullscreenPollInterval</c>을 크게 올린다.
    ///
    /// ============================================================================
    /// 무엇이 결함이었나 (N-20)
    /// ============================================================================
    /// 자동 표면 4곳(할 일 메모 카드 + 클릭 차단막 · 할 일 리마인더 · 창 크랙 · 설정창 자동 재오픈)이 <c>ArePanelsSuppressed</c>
    /// (= s ∨ (r ∧ ¬g))를 읽었다. 등급 1(r)에서 사용자가 창을 열어 임대(g)가 갱신되는 동안 그 값은 거짓이라, 사용자가 부르지 않은
    /// 표면이 <b>남의 임대에 편승해</b> 발표 화면 위로 되살아났다. 수정은 새 창구 <c>SuppressesUnsummonedSurfaces</c>(= s ∨ r ∨ g)다.
    ///
    /// ============================================================================
    /// 케이스
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>R0</b> 음성 대조 — 등급 없음에서 카드·차단막이 보이고 리마인더가 실제로 발동한다. <b>빨가면 R1~R3의 «0회»는 무효</b>.</item>
    ///   <item><b>R1</b> 결함 재현 — 등급 1 + 할 일 1건 + 사용자 정보창. 벽시계 관측 동안 카드·차단막·리마인더 0회, 끝에서 허가 생존.</item>
    ///   <item><b>R2</b> 무허가 등급 1 — 억제 유지(새 창구 ⊇ 옛 창구).</item>
    ///   <item><b>R3</b> 이력 칸 FFT — 등급 1이 끝나도 사용자 창이 떠 있으면 카드는 기다리고, 창을 모두 닫으면 임대 수명 + 1프레임 안에 돌아온다.</item>
    ///   <item><b>R4a · R4b</b> 적발 ③ — 등급 2에서 빼앗긴 설정창의 자동 재오픈 예약이 2→1 복귀 뒤 사용자의 우클릭(R4a)·정보창 열기(R4b)
    ///     허가에 편승하지 않는다. 대조: 사용자 표면을 닫고 전체화면이 끝나면 유예 안에 재오픈된다.</item>
    ///   <item><b>R5a · R5b · R5c · R5d</b> 해제 조건 A1(명령 크랙, 설계 §9-1) — 사용자가 연 행동 명령창의 [창 부수기]와 ⌃⌥⌘X 경로
    ///     (<c>ForceTriggerNow</c>)가 FTT(R5a) · FFT(R5b) 칸에서 회색 + 사유이고 휘두르기 전이 · 금 오버레이가 0이다.
    ///     R5c는 음성 대조(등급 없음 → 가능 · 금이 설정 수명 동안 취소 없이 보인다), R5d는 가드 위치(숨김 게이트 뒤 · 락 검사 앞)를 잠근다.</item>
    /// </list>
    ///
    /// <para>★ <b>수정 전 박제</b>: 이 파일은 새 정책 함수를 참조하지 않는다 — 수정 전 트리(HEAD)에서도 컴파일되어
    /// R1·R3·R4가 빨강임을 먼저 박제하기 위해서다(ROADMAP N-20 「R1은 수정 전 빨강 박제를 같이 낸다」).
    /// 예외 1곳: R5의 <c>ExpectedCrackLeaseReason</c>은 A1 사유 상수를 참조한다 — 그 박제는 이 한 줄을 표지 문자열로 바꾼 판으로 했다(그 속성 문서).</para>
    ///
    /// <para><b>시간은 전부 벽시계(초)</b>이고 예산은 프로덕션 상수식이다(CLAUDE.md) — <see cref="UserSurfaceSummonPolicy.LeaseSeconds"/>,
    /// <see cref="GearRadialMenuWidget.ExpandTotalSeconds"/>, <see cref="SettingsWindow.DefaultReopenAfterSuspendGraceSeconds"/>.
    /// 관측은 «한 번이라도 보였는가»를 매 프레임 표본으로 센다.</para>
    /// </summary>
    public sealed class AutoSurfaceLeaseAxisTests
    {
        private const string LogPrefix = "[자동표면임대-TEST]";

        /// <summary>씬·폴링 소비자가 한 바퀴 도는 여유(초).</summary>
        private const float SettleSeconds = 0.35f;

        /// <summary>연출 예산 위에 얹는 관측 슬랙(초).</summary>
        private const float ObserveSlackSeconds = 0.25f;

        /// <summary>에이전트 자체 폴링이 주입한 축을 덮어쓰지 못하게 하는 값(초) — 에디터의 널 서비스는 등급 None이다.</summary>
        private const float ObservePollInterval = 9999f;

        /// <summary>메모 카드를 띄울 할 일의 소프트캡 인자. 값 자체는 관심사가 아니다(넘치지 않게 크게).</summary>
        private const int PostItSoftCap = 99;

        /// <summary>리마인더 확률 문을 항상 연다 — «발동 안 함»이 운이 아니게.</summary>
        private const float ReminderChanceForTest = 1f;

        /// <summary>리마인더 점검 주기(초) — 프로덕션 하한(<c>Mathf.Max(1f, …)</c>)과 같은 값이라 실제 점검 주기가 이 값이다.</summary>
        private const float ReminderIntervalForTest = 1f;

        /// <summary>가짜 전역 포인터 — 세운 값을 그대로 돌려준다(조회는 항상 성공).</summary>
        private sealed class ScriptedButtons : IGlobalPointerButtonService
        {
            public bool Primary;
            public bool Secondary;
            public bool TryGetPrimaryButtonPressed(out bool pressed) { pressed = Primary; return true; }
            public bool TryGetSecondaryButtonPressed(out bool pressed) { pressed = Secondary; return true; }
        }

        /// <summary>자동 표면 관측 계수(매 프레임 표본).</summary>
        private sealed class AutoSurfaceTally
        {
            public int Frames;
            public int CardFrames;
            public int BlockerFrames;
            public int ReminderFrames;
            public int IdleWalkFrames;

            public override string ToString() =>
                $"관측 {Frames}프레임 — 카드 {CardFrames} · 차단막 {BlockerFrames} · 리마인더 상태 {ReminderFrames} · Idle/Walk {IdleWalkFrames}";
        }

        /// <summary>설정창 자동 재오픈 편승 관측 계수.</summary>
        private sealed class ReopenTally
        {
            public int Frames;
            public int SettingsOpenFrames;
            public int UserSurfaceGoneFrames;

            public override string ToString() =>
                $"관측 {Frames}프레임 — 설정창 열림 {SettingsOpenFrames} · 사용자 표면 사라짐 {UserSurfaceGoneFrames}";
        }

        private StickmanAgent _agent;
        private AppControlDirector _control;
        private GearRadialMenuWidget _menu;
        private CharacterInfoWindow _window;
        private SettingsWindow _settings;
        private TodoPostItWidget _postIt;
        private StickConfig _config;

        private readonly ScriptedButtons _buttons = new ScriptedButtons();

        private float _savedPollInterval;
        private bool _configHijacked;
        private object _savedButtonService;
        private CursorPositionQuery _savedCursor;
        private Vector2 _savedOverlayOrigin;
        private bool _hijacked;
        private float _savedReminderChance;
        private float _savedReminderInterval;
        private bool _reminderHijacked;
        private bool _graceHijacked;
        private float _beforeTierTwoUnscaled;

        private bool _bypassAtFixtureStart;
        private bool _fixtureStartCaptured;

        // ---- R5(명령 크랙) 하네스 ----
        private WindowCrashDirector _crash;
        private ActionCommandPopover _popover;
        private IMovementIntentSource _savedIntent;
        private bool _intentHijacked;
        private bool _crackObserverAttached;
        private bool _testHoldsLock;
        private bool _userHiddenByTest;
        private readonly CrackTally _crack = new CrackTally();

        // ---- R6 · R7(명령 그라피티 — 해제 조건 7-b) 하네스 ----
        private GraffitiDirector _graffiti;
        private bool _graffitiObserverAttached;
        private readonly GraffitiTally _paint = new GraffitiTally();

        private static readonly FieldInfo ButtonServiceField =
            typeof(AppControlDirector).GetField("_buttonService", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo TickRightClickFanMethod =
            typeof(AppControlDirector).GetMethod("TickRightClickFan", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>축 3 — 등급 1의 원시 사실(r).</summary>
        private static readonly FieldInfo PanelRetreatField =
            typeof(StickmanAgent).GetField("_fullscreenPanelRetreat", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>축 1 — 등급 2(전체화면 <b>게임</b>)의 원시 사실.</summary>
        private static readonly FieldInfo FullscreenAxisField =
            typeof(StickmanAgent).GetField("_fullscreenAutoHide", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo ApplyDecisionMethod =
            typeof(StickmanAgent).GetMethod("ApplySuspendDecision", BindingFlags.Instance | BindingFlags.NonPublic);

        // ==================== 준비 / 정리 — 우회는 픽스처가 끄고 픽스처가 되돌린다 ====================

        [OneTimeSetUp]
        public void CaptureBypassWorldAndRequireIsolation()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다(절대 불변 원칙 3).");
            _bypassAtFixtureStart = InfoGearIconWidget.IsStandbyGateBypassedForTests;
            _fixtureStartCaptured = true;
            Debug.Log($"{LogPrefix} 픽스처 시작 시 대기 톱니 우회={_bypassAtFixtureStart}. 각 테스트는 우회를 끄고 시작해 끝에서 이 값으로 되돌립니다.");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        /// <summary>복원 누락 자기 검증(마지막 테스트 몫). 실패는 <c>site="TearDown"</c>으로만 남으므로(TEAM.md 「픽스처 끝에서 난 실패」)
        /// 단언 뒤에 성공 로그를 남긴다.</summary>
        [OneTimeTearDown]
        public void RestoreBypassForTheRestOfTheAssembly()
        {
            bool leftAtEnd = InfoGearIconWidget.IsStandbyGateBypassedForTests;
            InfoGearIconWidget.SetStandbyGateBypassedForTests(_bypassAtFixtureStart);
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
            Assert.AreEqual(_bypassAtFixtureStart, leftAtEnd,
                $"{LogPrefix} ★ 마지막 테스트가 대기 톱니 우회를 되돌리지 않았습니다(남은 값={leftAtEnd}, 시작 값={_bypassAtFixtureStart}).");
            Assert.AreEqual(SettingsWindow.DefaultReopenAfterSuspendGraceSeconds, SettingsWindow.ReopenAfterSuspendGraceSeconds, 1e-4f,
                $"{LogPrefix} ★ 설정창 재오픈 유예가 기본값으로 돌아오지 않았습니다 — 뒤 픽스처가 다른 유예를 물려받습니다.");
            Debug.Log($"{LogPrefix} 픽스처 정리 단언 통과 — 대기 톱니 우회({_bypassAtFixtureStart})와 설정창 재오픈 유예(기본값)가 원래대로입니다.");
        }

        [SetUp]
        public void VerifyPreviousRestoreThenTurnBypassOff()
        {
            Assert.IsTrue(_fixtureStartCaptured, $"{LogPrefix} 픽스처 시작 값이 기록되지 않았습니다 — OneTimeSetUp이 돌지 않았습니다.");
            Assert.AreEqual(_bypassAtFixtureStart, InfoGearIconWidget.IsStandbyGateBypassedForTests,
                $"{LogPrefix} ★ 앞 테스트가 대기 톱니 우회를 되돌리지 않았습니다(복원 누락).");
            InfoGearIconWidget.SetStandbyGateBypassedForTests(false);
        }

        [UnityTearDown]
        public IEnumerator RestoreSceneAndWorld()
        {
            // 표면 → 축 → 연출 → 하네스 → 설정 에셋 → 정적 값 → 우회 순. 설정창을 먼저 닫아야 시트 복귀가 정보창을 되살려도 아래에서 함께 닫힌다.
            if (_menu != null) _menu.ForceCloseAll("테스트 정리");
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");

            // R5 하네스 — 락 · 숨김 · 관측 구독 · 의도 소스를 되돌린다(단언이 중간에 터져도 여기서 풀린다).
            if (_testHoldsLock) SpectacleEventLock.Release(this);
            _testHoldsLock = false;
            if (_userHiddenByTest && _agent != null) _agent.SetUserHidden(false, "테스트 정리");
            _userHiddenByTest = false;
            if (_crackObserverAttached)
            {
                StickmanEventBus.WindowCrashOverlayChanged -= OnCrackOverlayChanged;
                StickmanEventBus.StateTransitioned -= OnCrackStateTransitioned;
            }
            _crackObserverAttached = false;
            if (_graffitiObserverAttached)
            {
                StickmanEventBus.GraffitiOverlayChanged -= OnGraffitiOverlayChanged;
                StickmanEventBus.StateTransitioned -= OnGraffitiStateTransitioned;
            }
            _graffitiObserverAttached = false;
            if (_intentHijacked && _agent != null && _agent.Blackboard != null) _agent.Blackboard.IntentSource = _savedIntent;
            _intentHijacked = false;
            if (_agent != null && _agent.Blackboard != null && _agent.Blackboard.Machine != null
                && (_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.WindowCrash
                    || _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Graffiti))
            {
                // 그라피티는 출하 값 3~5초짜리 상태다. 남겨 두면 뒤따르는 테스트가 «지금 그라피티 중이라 못 해요»를 본다.
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }

            if (_agent != null)
            {
                if (PanelRetreatField != null) PanelRetreatField.SetValue(_agent, false);
                if (FullscreenAxisField != null && ApplyDecisionMethod != null)
                {
                    FullscreenAxisField.SetValue(_agent, false);
                    ApplyDecisionMethod.Invoke(_agent, null);
                }
                if (_agent.Blackboard != null && _agent.Blackboard.Machine != null
                    && _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.TodoReminder)
                {
                    _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                }
            }
            if (SpectacleEventLock.IsActive) SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);

            if (_hijacked)
            {
                if (_control != null && ButtonServiceField != null) ButtonServiceField.SetValue(_control, _savedButtonService);
                if (_agent != null && _agent.Blackboard != null) _agent.Blackboard.CursorProvider = _savedCursor;
                ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOverlayOrigin;
                _hijacked = false;
            }
            if (_reminderHijacked && _config != null)
            {
                _config.todoReminderChance = _savedReminderChance;
                _config.todoReminderCheckInterval = _savedReminderInterval;
            }
            _reminderHijacked = false;
            if (_graceHijacked) SettingsWindow.ResetReopenGraceForTests();
            _graceHijacked = false;
            TodoListModel.ResetForTesting();

            // StickConfig는 <b>배포 에셋</b>이라 반드시 원복한다.
            if (_configHijacked && _config != null) _config.fullscreenPollInterval = _savedPollInterval;
            _configHijacked = false;

            InfoGearIconWidget.SetStandbyGateBypassedForTests(_bypassAtFixtureStart);

            _agent = null;
            _control = null;
            _menu = null;
            _window = null;
            _settings = null;
            _postIt = null;
            _config = null;
            _savedButtonService = null;
            _crash = null;
            _popover = null;
            _graffiti = null;
            _savedIntent = null;
            yield return null;
        }

        private IEnumerator LoadScene()
        {
            Assert.IsNotNull(ButtonServiceField, $"{LogPrefix} AppControlDirector._buttonService를 찾지 못했습니다 — 우클릭 주입 경로가 죽었습니다.");
            Assert.IsNotNull(TickRightClickFanMethod, $"{LogPrefix} AppControlDirector.TickRightClickFan()을 찾지 못했습니다.");
            Assert.IsNotNull(PanelRetreatField, $"{LogPrefix} StickmanAgent._fullscreenPanelRetreat를 찾지 못했습니다 — 등급 1 주입이 아무 일도 안 합니다.");
            Assert.IsNotNull(FullscreenAxisField, $"{LogPrefix} StickmanAgent._fullscreenAutoHide를 찾지 못했습니다 — 등급 2 주입이 아무 일도 안 합니다.");
            Assert.IsNotNull(ApplyDecisionMethod, $"{LogPrefix} StickmanAgent.ApplySuspendDecision()을 찾지 못했습니다.");

            TodoListModel.ResetForTesting();
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            _control = Object.FindFirstObjectByType<AppControlDirector>(FindObjectsInactive.Include);
            Assert.IsNotNull(_control, $"{LogPrefix} 씬에 AppControlDirector가 없습니다.");
            _menu = _agent.GetComponent<GearRadialMenuWidget>();
            Assert.IsNotNull(_menu, $"{LogPrefix} 캐릭터에 GearRadialMenuWidget이 없습니다.");
            _window = _agent.GetComponent<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 캐릭터에 CharacterInfoWindow가 없습니다.");
            _settings = _agent.GetComponent<SettingsWindow>();
            Assert.IsNotNull(_settings, $"{LogPrefix} 캐릭터에 SettingsWindow가 없습니다.");
            _postIt = Object.FindFirstObjectByType<TodoPostItWidget>(FindObjectsInactive.Include);
            Assert.IsNotNull(_postIt, $"{LogPrefix} 씬에 TodoPostItWidget이 없습니다 — 메모 카드를 잴 수 없습니다.");

            _config = _agent.Config;
            Assert.IsNotNull(_config, $"{LogPrefix} StickConfig가 없습니다.");
            _savedPollInterval = _config.fullscreenPollInterval;
            _config.fullscreenPollInterval = ObservePollInterval;
            _configHijacked = true;

            yield return Wait(SettleSeconds);

            _savedOverlayOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
            _savedCursor = _agent.Blackboard.CursorProvider;
            _agent.Blackboard.CursorProvider = TryGetScriptedCursor;
            _savedButtonService = ButtonServiceField.GetValue(_control);
            _buttons.Primary = false;
            _buttons.Secondary = false;
            ButtonServiceField.SetValue(_control, _buttons);
            _hijacked = true;

            Assert.IsFalse(InfoGearIconWidget.IsStandbyGateBypassedForTests,
                $"{LogPrefix} 전제 불성립 — SetUp이 대기 톱니 우회를 껐는데 켜져 있습니다. 이 픽스처는 우회된 세계를 재지 않습니다.");
            Assert.IsFalse(_agent.ArePanelsSuppressed, $"{LogPrefix} 새 씬인데 이미 표면 억제 중입니다 — 준비 단계가 오염됐습니다.");
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 새 씬인데 캐릭터가 숨어 있습니다.");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} 새 씬인데 사용자 허가가 살아 있습니다.");
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} 새 씬인데 부채꼴이 이미 떠 있습니다.");
            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 새 씬인데 정보창이 이미 열려 있습니다.");
            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} 새 씬인데 설정창이 이미 열려 있습니다.");

            // 첫 표본은 «기록만» 하는 프레임이다(앱 시작 순간 눌려 있던 버튼을 명령으로 오인하지 않는 설계).
            Tick();
            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private void Tick() => TickRightClickFanMethod.Invoke(_control, null);

        private void RightDown()
        {
            _buttons.Secondary = true;
            Tick();
        }

        private void RightUp()
        {
            _buttons.Secondary = false;
            Tick();
        }

        private void SetPanelRetreat(bool on) => PanelRetreatField.SetValue(_agent, on);

        private void SetGameAxis(bool on)
        {
            FullscreenAxisField.SetValue(_agent, on);
            ApplyDecisionMethod.Invoke(_agent, null);
        }

        private Vector2 CursorWorldNow()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            return bb.Body.position + new Vector2(0f, bb.CharacterHeightWorld * 0.5f);
        }

        private bool TryGetScriptedCursor(out Vector2 osScreenPosition)
        {
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : null;
            if (cam == null) { osScreenPosition = default; return false; }
            osScreenPosition = ScreenCoordinateConverter.WorldToOsScreen(cam, CursorWorldNow(), _agent.Config, out _);
            return true;
        }

        private void AssertCursorInside()
        {
            Vector2 world = CursorWorldNow();
            Collider2D[] all = _agent.GetComponentsInChildren<Collider2D>(true);
            bool inside = false;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].enabled) continue;
                if (all[i].OverlapPoint(world)) { inside = true; break; }
            }
            Assert.IsTrue(inside,
                $"{LogPrefix} 전제 불성립 — 몸 중심 좌표가 어떤 캐릭터 콜라이더에도 안 걸립니다. 아래 «우클릭»이 다른 것을 재게 됩니다.");
        }

        // ==================== 공통 절차 ====================

        /// <summary>등급 없음에서 할 일 1건을 넣고 카드·차단막이 <b>실제로</b> 켜지는지 먼저 본다(테스트 안 양성 대조).</summary>
        private IEnumerator PlaceTodoAndConfirmCard(string what)
        {
            TodoListModel.Add($"{what} — 자동 표면 임대 축 시험용 할 일", PostItSoftCap);   // 반환값은 «소프트캡 초과 여부»다(성공 여부 아님).
            Assert.GreaterOrEqual(TodoListModel.UncompletedCount, 1, $"{LogPrefix} {what} 전제 — 미완료 할 일이 없습니다.");
            yield return WaitUntilOrTimeout(() => _postIt.IsCardVisible && _postIt.IsClickBlockerEnabled, SettleSeconds);
            Assert.IsTrue(_postIt.IsCardVisible,
                $"{LogPrefix} {what} 양성 대조 실패 — 등급 없음에서 할 일 {TodoListModel.UncompletedCount}건인데 메모 카드가 안 보입니다. " +
                "아래 «한 번도 안 보였다»가 «원래 안 뜨는 세계»와 구별되지 않습니다.");
            Assert.IsTrue(_postIt.IsClickBlockerEnabled, $"{LogPrefix} {what} 양성 대조 실패 — 카드는 보이는데 클릭 차단막이 꺼져 있습니다.");
        }

        /// <summary>등급 1(r)만 주입하고 정착을 기다린 뒤 무허가 등급 1의 전제를 세운다.</summary>
        private IEnumerator EnterTierOne(string what)
        {
            SetPanelRetreat(true);
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.ArePanelsSuppressed, $"{LogPrefix} {what} 전제 — 등급 1을 세웠는데 표면 억제가 켜지지 않았습니다.");
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} {what} 전제 — 등급 1인데 캐릭터가 숨었습니다(2026-08-31 신고 회귀).");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} {what} 전제 — 등급 1 진입 직후인데 허가가 살아 있습니다.");
            Assert.IsFalse(_postIt.IsCardVisible, $"{LogPrefix} {what} 전제 — 무허가 등급 1인데 메모 카드가 보입니다(기존 억제 회귀).");
            Assert.IsFalse(_postIt.IsClickBlockerEnabled, $"{LogPrefix} {what} 전제 — 무허가 등급 1인데 카드 차단막이 켜져 있습니다.");
        }

        /// <summary>리마인더 확률·주기를 연다. 이미 리마인더 연출 중이면 관측이 «전부터 있던 상태»를 세지 않게 Idle로 돌린다.</summary>
        private void ArmReminder(string what)
        {
            _savedReminderChance = _config.todoReminderChance;
            _savedReminderInterval = _config.todoReminderCheckInterval;
            _reminderHijacked = true;
            _config.todoReminderChance = ReminderChanceForTest;
            _config.todoReminderCheckInterval = ReminderIntervalForTest;
            if (_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.TodoReminder)
            {
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                if (SpectacleEventLock.IsActive) SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            }
            Assert.AreNotEqual(StickmanStateId.TodoReminder, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} {what} 전제 — 관측 시작 전에 이미 리마인더 연출 중입니다.");
        }

        /// <summary>리마인더 점검이 최소 세 번 돌 수 있고 임대가 여러 번 만료될 수 있는 벽시계 예산(프로덕션 상수식).</summary>
        private float AutoSurfaceObserveBudget() =>
            Mathf.Max(Mathf.Max(1f, _config.todoReminderCheckInterval) * 3f + ObserveSlackSeconds, UserSurfaceSummonPolicy.LeaseSeconds * 4f);

        private IEnumerator ObserveAutoSurfaces(float seconds, AutoSurfaceTally tally)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                SampleAutoSurfaces(tally);
            }
        }

        private void SampleAutoSurfaces(AutoSurfaceTally tally)
        {
            tally.Frames++;
            if (_postIt.IsCardVisible) tally.CardFrames++;
            if (_postIt.IsClickBlockerEnabled) tally.BlockerFrames++;
            StickmanStateId s = _agent.Blackboard.Machine.CurrentStateId;
            if (s == StickmanStateId.TodoReminder) tally.ReminderFrames++;
            if (s == StickmanStateId.Idle || s == StickmanStateId.Walk) tally.IdleWalkFrames++;
        }

        private void AssertNoAutoSurfaceEverAppeared(string what, AutoSurfaceTally t)
        {
            Assert.Greater(t.Frames, 1, $"{LogPrefix} {what} — 관측 프레임이 {t.Frames}개뿐입니다(«한 번도 안 보였다»를 잴 수 없습니다).");
            Assert.Greater(t.IdleWalkFrames, 1,
                $"{LogPrefix} {what} 전제 — 관측 동안 캐릭터가 Idle/Walk에 거의 없었습니다({t}). 리마인더 점검이 돌 수 없어 «리마인더 0회»가 무의미합니다.");
            Assert.AreEqual(0, t.CardFrames,
                $"{LogPrefix} ★ {what} — 사용자가 부르지 않은 메모 카드가 {t.CardFrames}프레임 보였습니다({t}). 할 일 문구가 발표 화면 위에 뜹니다(원칙 2, N-20).");
            Assert.AreEqual(0, t.BlockerFrames,
                $"{LogPrefix} ★ {what} — 메모 카드 클릭 차단막이 {t.BlockerFrames}프레임 켜졌습니다({t}). 남의 발표 화면 그 사각형의 클릭을 먹습니다(원칙 2, N-20).");
            Assert.AreEqual(0, t.ReminderFrames,
                $"{LogPrefix} ★ {what} — 사용자가 부르지 않은 할 일 리마인더가 발동했습니다({t}) (원칙 2, N-20).");
        }

        // ==================== R0 — 음성 대조(등급 없음) ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R0_음성대조_프로덕션_톱니세계_등급없음에서는_메모카드와_차단막이_보이고_리마인더가_발동한다()
        {
            yield return LoadScene();
            yield return PlaceTodoAndConfirmCard("R0");
            ArmReminder("R0");

            var t = new AutoSurfaceTally();
            float end = Time.realtimeSinceStartup + Mathf.Max(1f, _config.todoReminderCheckInterval) * 6f + ObserveSlackSeconds;
            while (Time.realtimeSinceStartup < end && t.ReminderFrames == 0)
            {
                yield return null;
                SampleAutoSurfaces(t);
            }
            Debug.Log($"{LogPrefix} R0 — {t}");

            Assert.Greater(t.Frames, 1, $"{LogPrefix} R0 — 관측 프레임이 {t.Frames}개뿐입니다.");
            Assert.Greater(t.CardFrames, 0, $"{LogPrefix} ★ R0 음성 대조 실패 — 등급 없음에서 메모 카드가 관측 중 한 번도 안 보였습니다({t}). R1~R3 전부 무효입니다.");
            Assert.Greater(t.BlockerFrames, 0, $"{LogPrefix} ★ R0 음성 대조 실패 — 등급 없음에서 카드 차단막이 한 번도 안 켜졌습니다({t}). R1~R3 전부 무효입니다.");
            Assert.Greater(t.ReminderFrames, 0,
                $"{LogPrefix} ★ R0 음성 대조 실패 — 등급 없음·확률 {ReminderChanceForTest:F0}에서 리마인더가 발동하지 않았습니다({t}). " +
                "이 세계에서는 리마인더가 원래 못 뜨므로 R1~R3의 «리마인더 0회»는 무효입니다.");
            Assert.IsFalse(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive, $"{LogPrefix} R0 — 등급 없음이 아니었습니다.");
            Debug.Log($"{LogPrefix} R0 확인 — 등급 없음에서 카드·차단막이 보였고 리마인더가 발동했습니다(측정기 생존).");
        }

        // ==================== R1 — 결함 재현(수정 전 빨강 박제 대상) ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R1_프로덕션_톱니세계_등급1에서_사용자가_연_정보창이_떠_있는_동안_메모카드_차단막_리마인더가_한_번도_안_나오고_허가는_살아_있다()
        {
            yield return LoadScene();
            yield return PlaceTodoAndConfirmCard("R1");
            yield return EnterTierOne("R1");
            ArmReminder("R1");

            _window.Open("R1 — 사용자가 정보창을 연다(⌃⌥⌘I · 부채꼴 [캐릭터]와 같은 사용자 열기)");
            Assert.IsTrue(_agent.IsUserSummonGrantActive, $"{LogPrefix} R1 전제 — 사용자 열기가 허가를 내지 않았습니다(E-2 회귀). 아래는 FTT 칸을 재지 못합니다.");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} R1 전제 — 정보창이 열리지 않았습니다.");

            var t = new AutoSurfaceTally();
            float budget = AutoSurfaceObserveBudget();
            yield return ObserveAutoSurfaces(budget, t);
            Debug.Log($"{LogPrefix} R1 — 등급 1 + 사용자 정보창, 벽시계 {budget:F2}초: {t} · 끝 허가={_agent.IsUserSummonGrantActive} · 창={_window.IsOpen}");

            AssertNoAutoSurfaceEverAppeared("R1(등급 1 · 사용자 창 열림 · FTT 칸)", t);
            Assert.IsTrue(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} ★ R1 — 관측 끝에서 허가가 죽어 있습니다. 이 단언이 없으면 «임대가 만료돼서 숨었다»와 «허가가 살아 있어도 숨었다»가 구별되지 않습니다.");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} R1 — 사용자 정보창이 관측 중 사라졌습니다(사용자 표면 회귀).");
            Assert.IsTrue((bool)PanelRetreatField.GetValue(_agent), $"{LogPrefix} R1 — 축 3이 저절로 꺼졌습니다. 위 단언은 등급 1을 재지 않았습니다.");

            // ---------- 양성 대조 — 창을 닫아도 등급 1이면 억제가 이어지고, 등급 1이 끝나면 같은 측정으로 카드가 돌아온다 ----------
            _window.Close("R1 양성 대조 — 사용자가 창을 모두 닫았다");
            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} R1 양성 대조 — 창을 닫았는데 허가가 {UserSurfaceSummonPolicy.LeaseSeconds * 3f:F2}초 뒤까지 남았습니다.");
            Assert.IsFalse(_postIt.IsCardVisible, $"{LogPrefix} R1 양성 대조 — 창을 닫았는데 등급 1인데도 카드가 보입니다.");
            SetPanelRetreat(false);
            yield return WaitUntilOrTimeout(() => _postIt.IsCardVisible && _postIt.IsClickBlockerEnabled, SettleSeconds);
            Assert.IsTrue(_postIt.IsCardVisible && _postIt.IsClickBlockerEnabled,
                $"{LogPrefix} R1 양성 대조 실패 — 창을 모두 닫고 등급 1이 끝났는데 {SettleSeconds:F2}초 안에 카드가 돌아오지 않았습니다. 위 «0회»도 함께 폐기해야 합니다.");
            Debug.Log($"{LogPrefix} R1 확인 — 등급 1에서 사용자 창이 떠 있는 동안 자동 표면 0회, 허가 생존. 닫고 등급 1이 끝나자 카드가 돌아왔습니다.");
        }

        // ==================== R2 — 무허가 등급 1 유지 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R2_프로덕션_톱니세계_무허가_등급1에서는_메모카드_차단막_리마인더가_계속_억제되고_등급1이_끝나면_카드가_돌아온다()
        {
            yield return LoadScene();
            yield return PlaceTodoAndConfirmCard("R2");
            yield return EnterTierOne("R2");
            ArmReminder("R2");

            var t = new AutoSurfaceTally();
            float budget = AutoSurfaceObserveBudget();
            yield return ObserveAutoSurfaces(budget, t);
            Debug.Log($"{LogPrefix} R2 — 무허가 등급 1, 벽시계 {budget:F2}초: {t}");

            AssertNoAutoSurfaceEverAppeared("R2(무허가 등급 1 · FTF 칸)", t);
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} R2 — 아무도 열지 않았는데 허가가 났습니다.");
            Assert.IsTrue(_agent.ArePanelsSuppressed, $"{LogPrefix} R2 — 등급 1 억제가 관측 중 풀렸습니다.");

            SetPanelRetreat(false);
            yield return WaitUntilOrTimeout(() => _postIt.IsCardVisible && _postIt.IsClickBlockerEnabled, SettleSeconds);
            Assert.IsTrue(_postIt.IsCardVisible && _postIt.IsClickBlockerEnabled,
                $"{LogPrefix} R2 양성 대조 실패 — 등급 1이 끝났는데 {SettleSeconds:F2}초 안에 카드가 돌아오지 않았습니다(상시 HUD 복귀 회귀).");
            Debug.Log($"{LogPrefix} R2 확인 — 무허가 등급 1에서 자동 표면 0회, 등급 1이 끝나자 카드가 돌아왔습니다.");
        }

        // ==================== R3 — 이력 칸(FFT) · 창을 모두 닫으면 복귀 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R3_프로덕션_톱니세계_이력칸_등급1이_끝나도_사용자_창이_떠_있으면_카드는_기다리고_창을_모두_닫으면_임대수명_더하기_1프레임_안에_돌아온다()
        {
            yield return LoadScene();
            yield return PlaceTodoAndConfirmCard("R3");
            yield return EnterTierOne("R3");

            _window.Open("R3 — 등급 1에서 사용자가 정보창을 연다");
            Assert.IsTrue(_agent.IsUserSummonGrantActive && _window.IsOpen, $"{LogPrefix} R3 전제 — 사용자 정보창이 허가를 받아 열리지 않았습니다.");

            // ---------- 전체화면 앱이 사라졌다(r = 거짓). 사용자 창은 그대로 — FFT 칸 ----------
            SetPanelRetreat(false);
            ArmReminder("R3");
            var t = new AutoSurfaceTally();
            float budget = AutoSurfaceObserveBudget();
            yield return ObserveAutoSurfaces(budget, t);
            Debug.Log($"{LogPrefix} R3 — 등급 1 종료 · 사용자 창 열림, 벽시계 {budget:F2}초: {t} · 끝 허가={_agent.IsUserSummonGrantActive} · 옛 창구={_agent.ArePanelsSuppressed}");

            AssertNoAutoSurfaceEverAppeared("R3(등급 1 종료 · 사용자 창 열림 · FFT 칸)", t);
            Assert.IsTrue(_agent.IsUserSummonGrantActive, $"{LogPrefix} R3 — 관측 끝에서 허가가 죽어 있습니다(FFT 칸을 재지 못했습니다).");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} R3 — 사용자 정보창이 관측 중 사라졌습니다.");
            Assert.IsFalse((bool)PanelRetreatField.GetValue(_agent), $"{LogPrefix} R3 전제 — 축 3이 아직 켜져 있습니다(FFT 칸이 아닙니다).");
            Assert.IsFalse(_agent.ArePanelsSuppressed,
                $"{LogPrefix} R3 전제 — 옛 창구가 참입니다. 이 칸은 옛 창구가 거짓인데도 자동 표면이 기다려야 하는 칸이라, 참이면 새 창구를 재지 않습니다.");

            // ---------- 사용자가 창을 모두 닫는다 → 임대 수명 + 1프레임 안에 카드 복귀 ----------
            _window.Close("R3 — 사용자가 창을 모두 닫았다");
            float closedAt = Time.unscaledTime;
            float expiry = closedAt + UserSurfaceSummonPolicy.LeaseSeconds;
            float giveUp = closedAt + UserSurfaceSummonPolicy.LeaseSeconds * 4f;
            int hiddenFramesAtOrAfterExpiry = 0;
            float visibleAt = -1f;
            while (true)
            {
                yield return null;
                float now = Time.unscaledTime;
                if (_postIt.IsCardVisible && _postIt.IsClickBlockerEnabled) { visibleAt = now; break; }
                if (now >= expiry) hiddenFramesAtOrAfterExpiry++;
                if (now >= giveUp) break;
            }
            Debug.Log($"{LogPrefix} R3 복귀 — 닫은 뒤 {(visibleAt >= 0f ? (visibleAt - closedAt).ToString("F3") + "초" : "복귀 없음")}, " +
                $"임대 수명({UserSurfaceSummonPolicy.LeaseSeconds:F2}초)이 지난 뒤 숨어 있던 프레임 {hiddenFramesAtOrAfterExpiry}개.");
            Assert.GreaterOrEqual(visibleAt, 0f,
                $"{LogPrefix} ★ R3 — 사용자가 창을 모두 닫았는데 {UserSurfaceSummonPolicy.LeaseSeconds * 4f:F2}초 안에 메모 카드가 돌아오지 않았습니다(상시 HUD가 영구히 숨음).");
            Assert.LessOrEqual(hiddenFramesAtOrAfterExpiry, 1,
                $"{LogPrefix} ★ R3 — 임대 수명({UserSurfaceSummonPolicy.LeaseSeconds:F2}초)이 지난 뒤에도 카드가 {hiddenFramesAtOrAfterExpiry}프레임 더 숨어 있었습니다(허용: 1프레임).");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} R3 — 카드가 돌아왔는데 허가가 살아 있습니다(측정 모순).");
            Debug.Log($"{LogPrefix} R3 확인 — 이력 칸에서 자동 표면이 사용자 창을 기다렸고, 창을 모두 닫자 임대 수명 + 1프레임 안에 카드가 돌아왔습니다.");
        }

        // ==================== R4 — 적발 ③: 설정창 자동 재오픈 편승 ====================

        /// <summary>테스트가 쓰는 유예를 <b>프로덕션 기본값</b>으로 못박는다 — 앞 픽스처가 낮춘 값을 물려받으면 «유예를 넘겨 포기»가 섞인다.</summary>
        private void UseProductionReopenGrace(string what)
        {
            SettingsWindow.SetReopenGraceForTests(SettingsWindow.DefaultReopenAfterSuspendGraceSeconds);
            _graceHijacked = true;
            Assert.AreEqual(SettingsWindow.DefaultReopenAfterSuspendGraceSeconds, SettingsWindow.ReopenAfterSuspendGraceSeconds, 1e-4f,
                $"{LogPrefix} {what} 전제 — 재오픈 유예가 프로덕션 기본값이 아닙니다.");
        }

        /// <summary>등급 없음에서 사용자가 연 설정창을 등급 2가 빼앗고(예약 무장), 2→1로 내려온다(축 3 유지 — 프로덕션에서 이 전이는 에지가 없다).</summary>
        private IEnumerator TakeSettingsInTierTwoThenStepDownToTierOne(string what)
        {
            _settings.Open($"{what} — 등급 없음에서 사용자가 설정창을 연다");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_settings.IsOpen && _settings.IsClickBlockerEnabled, $"{LogPrefix} {what} 양성 대조 — 등급 없음에서 설정창이 열리지 않았습니다.");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} {what} 전제 — 등급 없음에서 허가가 났습니다.");

            _beforeTierTwoUnscaled = Time.unscaledTime;   // 실제 닫힌 시각의 하한 → 이 값으로 잰 경과는 실제 경과의 상한이다.
            SetPanelRetreat(true);   // 프로덕션 등급 2는 축 3도 참이다(§16-2b B7: s=T, r=T).
            SetGameAxis(true);
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended && _agent.HidesScreenSurfaces, $"{LogPrefix} {what} 전제 — 축 1 주입이 등급 2로 이어지지 않았습니다.");
            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} {what} 전제 — 등급 2인데 설정창이 걷히지 않았습니다(원칙 2).");
            Assert.IsTrue(_settings.IsReopenAfterSuspendArmed, $"{LogPrefix} {what} 전제 — 등급 2가 설정창을 빼앗고도 재오픈 예약을 걸지 않았습니다.");

            SetGameAxis(false);   // 2 → 1: 축 3은 그대로다.
            yield return Wait(SettleSeconds);
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} {what} 전제 — 등급 1로 내려왔는데 캐릭터가 숨어 있습니다.");
            Assert.IsTrue((bool)PanelRetreatField.GetValue(_agent) && _agent.ArePanelsSuppressed, $"{LogPrefix} {what} 전제 — 등급 1이 아닙니다.");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} {what} 전제 — 무허가 등급 1이 아닙니다.");
            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} {what} — 무허가 등급 1에서 설정창이 재오픈됐습니다(기존 계약 회귀).");
            Assert.IsTrue(_settings.IsReopenAfterSuspendArmed, $"{LogPrefix} {what} 전제 — 2→1에서 예약이 사라졌습니다(편승을 잴 수 없습니다).");
            Assert.IsFalse(_menu.IsVisible || _window.IsOpen, $"{LogPrefix} {what} 전제 — 사용자 표면이 이미 떠 있습니다.");
        }

        private IEnumerator ObserveReopen(float seconds, ReopenTally tally, Func<bool> userSurfaceStillUp)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                tally.Frames++;
                if (_settings.IsOpen) tally.SettingsOpenFrames++;
                if (!userSurfaceStillUp()) tally.UserSurfaceGoneFrames++;
            }
        }

        private void AssertNoPiggyback(string what, ReopenTally t)
        {
            Assert.Greater(t.Frames, 1, $"{LogPrefix} {what} — 관측 프레임이 {t.Frames}개뿐입니다.");
            Assert.AreEqual(0, t.SettingsOpenFrames,
                $"{LogPrefix} ★ {what} — 사용자 허가에 편승해 설정창 자동 재오픈이 실행됐습니다({t}). 부르지 않은 창이 사용자의 표면을 걷고 그 위로 뜹니다(적발 ③, N-20).");
            Assert.AreEqual(0, t.UserSurfaceGoneFrames,
                $"{LogPrefix} ★ {what} — 사용자가 연 표면이 관측 중 사라졌습니다({t}) — 자동 재오픈이 빼앗았을 수 있습니다.");
            Assert.IsTrue(_settings.IsReopenAfterSuspendArmed, $"{LogPrefix} ★ {what} — 재오픈 예약이 관측 중 소비됐습니다(편승 실행 또는 포기).");
            Assert.IsTrue(_agent.IsUserSummonGrantActive, $"{LogPrefix} {what} — 관측 끝에서 허가가 죽어 있습니다(편승 칸을 재지 못했습니다).");
        }

        /// <summary>대조 — 사용자 표면을 닫고(임대 만료) 전체화면이 끝나면 예약이 유예 안에 실행된다(측정기 생존).</summary>
        private IEnumerator ReleaseAndExpectReopen(string what, Action closeUserSurface, float closeSeconds)
        {
            closeUserSurface();
            yield return Wait(closeSeconds + ObserveSlackSeconds + UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} {what} 대조 — 사용자 표면을 닫았는데 허가가 남았습니다.");
            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} {what} 대조 — 허가가 만료된 무허가 등급 1에서 설정창이 재오픈됐습니다.");
            Assert.IsTrue(_settings.IsReopenAfterSuspendArmed, $"{LogPrefix} {what} 대조 — 등급 1이 이어지는 동안 예약이 사라졌습니다.");

            float awayUpperBound = Time.unscaledTime - _beforeTierTwoUnscaled;
            Assert.Less(awayUpperBound + SettleSeconds + UserSurfaceSummonPolicy.LeaseSeconds, SettingsWindow.ReopenAfterSuspendGraceSeconds,
                $"{LogPrefix} {what} 대조 측정 무효 — 대조 전에 이미 유예({SettingsWindow.ReopenAfterSuspendGraceSeconds:F0}초)에 가깝습니다(경과 상한 {awayUpperBound:F2}초).");

            SetPanelRetreat(false);
            yield return WaitUntilOrTimeout(() => _settings.IsOpen, SettleSeconds + UserSurfaceSummonPolicy.LeaseSeconds);
            Assert.IsTrue(_settings.IsOpen,
                $"{LogPrefix} {what} 대조 실패 — 사용자 표면을 닫고 전체화면이 끝났는데 예약이 실행되지 않았습니다(경과 상한 {awayUpperBound:F2}초 < 유예). " +
                "위 «안 열림»이 «원래 안 열리는 세계»와 구별되지 않습니다.");
            Assert.IsFalse(_settings.IsReopenAfterSuspendArmed, $"{LogPrefix} {what} 대조 — 재오픈 뒤에도 예약이 남았습니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R4a_프로덕션_톱니세계_등급2에서_빼앗긴_설정창의_자동_재오픈은_2에서_1로_내려온_뒤_캐릭터_우클릭_허가에_편승하지_않는다()
        {
            yield return LoadScene();
            UseProductionReopenGrace("R4a");
            yield return TakeSettingsInTierTwoThenStepDownToTierOne("R4a");

            // ---------- 사용자가 캐릭터를 우클릭한다(유예 안) ----------
            AssertCursorInside();
            RightDown();
            Assert.IsTrue(_agent.IsUserSummonGrantActive, $"{LogPrefix} R4a 전제 — 등급 1에서 캐릭터 우클릭이 허가를 내지 않았습니다(E-1 회귀).");
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} R4a 전제 — 허가는 났는데 부채꼴이 펴지지 않았습니다.");

            var t = new ReopenTally();
            float budget = Mathf.Max(GearRadialMenuWidget.ExpandTotalSeconds + ObserveSlackSeconds, UserSurfaceSummonPolicy.LeaseSeconds * 4f);
            yield return ObserveReopen(budget, t, () => _menu.IsVisible);
            RightUp();
            Debug.Log($"{LogPrefix} R4a — 2→1 복귀 뒤 우클릭, 벽시계 {budget:F2}초: {t} · 예약={_settings.IsReopenAfterSuspendArmed} · 허가={_agent.IsUserSummonGrantActive}");
            AssertNoPiggyback("R4a(우클릭 허가)", t);

            yield return ReleaseAndExpectReopen("R4a",
                () => _menu.Collapse(GearMenuCollapseMode.User, "R4a 대조 — 사용자가 부채꼴을 접는다"),
                GearRadialMenuWidget.CollapseUserSeconds);
            Debug.Log($"{LogPrefix} R4a 확인 — 우클릭 허가에 재오픈이 편승하지 않았고, 부채꼴을 접고 전체화면이 끝나자 유예 안에 재오픈됐습니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R4b_프로덕션_톱니세계_등급2에서_빼앗긴_설정창의_자동_재오픈은_2에서_1로_내려온_뒤_사용자_정보창_허가에_편승하지_않는다()
        {
            yield return LoadScene();
            UseProductionReopenGrace("R4b");
            yield return TakeSettingsInTierTwoThenStepDownToTierOne("R4b");

            _window.Open("R4b — 2→1 복귀 뒤 사용자가 정보창을 연다");
            Assert.IsTrue(_agent.IsUserSummonGrantActive && _window.IsOpen, $"{LogPrefix} R4b 전제 — 사용자 정보창이 허가를 받아 열리지 않았습니다.");

            var t = new ReopenTally();
            float budget = UserSurfaceSummonPolicy.LeaseSeconds * 4f;
            yield return ObserveReopen(budget, t, () => _window.IsOpen);
            Debug.Log($"{LogPrefix} R4b — 2→1 복귀 뒤 정보창, 벽시계 {budget:F2}초: {t} · 예약={_settings.IsReopenAfterSuspendArmed} · 허가={_agent.IsUserSummonGrantActive}");
            AssertNoPiggyback("R4b(정보창 허가)", t);

            yield return ReleaseAndExpectReopen("R4b",
                () => _window.Close("R4b 대조 — 사용자가 정보창을 닫는다"),
                0f);
            Debug.Log($"{LogPrefix} R4b 확인 — 정보창 허가에 재오픈이 편승하지 않았고, 창을 닫고 전체화면이 끝나자 유예 안에 재오픈됐습니다.");
        }

        // ==================== R5 — 해제 조건 A1: 명령 크랙(행동 명령창 [창 부수기] · ⌃⌥⌘X) ====================
        //
        // 설계: docs/systems/AUTO_SURFACE_LEASE_AXIS.md §9-1 · 판정 game-architect JUDGMENT ③ A1.
        // 결함: 등급 1에서 사용자가 연 명령창이 임대를 갱신하는 동안 [창 부수기]가 «준비됨»으로 보이고, 누르면 캐릭터는 휘두르지만
        //       금 오버레이는 다음 프레임에 N-20 가드(TickOverlay)가 취소한다 — 「결과 없는 휘두르기」(원칙 1).
        // 대상 창: 에디터 널 서비스의 더미 발판(최상위 · 핸들 ≥ 0)이 «앞에 있는 창»이다. 발판을 갈아 끼우지 않는다.

        /// <summary>
        /// ★ A1 사유 — 프로덕션 상수를 <b>참조</b>한다(CLAUDE.md — 문구를 베끼지 않는다).
        /// <para>수정 전 박제(2026-09-15, HEAD <c>ad49497</c> 프로덕션)에서는 그 상수가 없어 이 한 줄만 어떤 프로덕션 문구와도 같을 수 없는
        /// 표지 문자열로 두고 돌렸다(null이면 «가능 = 사유 null»과 우연히 같아진다). 박제 결과: R5a · R5b 빨강(가능 · 클릭과 ForceTriggerNow 모두
        /// 휘두르기 1 · 금 Started 1 → 1프레임 뒤 Cancelled 1), R5c 초록(금 3.000초), R5d 빨강((ii) 락 사유가 먼저).</para>
        /// </summary>
        private static string ExpectedCrackLeaseReason => WindowCrashDirector.UnsummonedSurfacesSuppressedReason;

        /// <summary>캐릭터가 명령을 받을 수 있는 상태(Idle/Walk · 락 비움)에 오기까지 기다리는 벽시계 예산(초).</summary>
        private const float CommandReadyBudgetSeconds = 6f;

        private const ActionCommandPopover.Command CrackCommand = ActionCommandPopover.Command.WindowCrash;

        /// <summary>제자리에 서 있게 하는 의도 소스 — 배회 AI가 점프 · 등반으로 상태 조건을 흔들지 않게 한다(판정 대상은 상태가 아니다).</summary>
        private sealed class StillIntent : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        /// <summary>명령 크랙 관측 계수 — 이벤트 버스에서 직접 센다(렌더러를 거치지 않는다).</summary>
        private sealed class CrackTally
        {
            public int WindowCrashTransitions;
            public int Started;
            public int Cancelled;
            public int Completed;
            public int UnknownPhase;
            public int StartedFrame = -1;
            public int CancelledFrame = -1;
            public float StartedAt = -1f;
            public float CancelledAt = -1f;
            public float CompletedAt = -1f;

            public void Reset()
            {
                WindowCrashTransitions = Started = Cancelled = Completed = UnknownPhase = 0;
                StartedFrame = CancelledFrame = -1;
                StartedAt = CancelledAt = CompletedAt = -1f;
            }

            public CrackTally Snapshot() => (CrackTally)MemberwiseClone();

            public override string ToString() =>
                $"휘두르기 전이 {WindowCrashTransitions} · 금 Started {Started} · Cancelled {Cancelled} · Completed {Completed} · 미상 단계 {UnknownPhase}" +
                (Started > 0 && Cancelled > 0 ? $" · Started→Cancelled {CancelledFrame - StartedFrame}프레임/{CancelledAt - StartedAt:F3}초" : string.Empty);
        }

        /// <summary>한 칸에서 잰 것 전부. 단언보다 먼저 로그로 남긴다 — 수정 전 박제에서 «무엇이 일어났는가»가 첫 실패 단언에 가려지지 않게.</summary>
        private sealed class CrackCommandReport
        {
            public StickmanStateId StateAtMeasure;
            public bool CouldTakeCommand;
            public float StableSeconds;
            public CommandAvailability Director;
            public CommandAvailability TileJudgement;
            public bool TileReady;
            public string TileReason;
            public string Caption;
            public int OtherReadyTiles;
            public CrackTally AfterClick;
            public bool ForceResult;
            public CrackTally AfterForce;
            public bool LeaseAtEnd;
            public bool PopoverOpenAtEnd;

            public override string ToString() =>
                $"잴 때 상태 {StateAtMeasure}(명령 받을 수 있음={CouldTakeCommand}, 안정 {StableSeconds:F2}초) · " +
                $"판정(감독) 가능={Director.IsReady} 사유=«{Director.Reason}» · 타일 가능={TileReady} 사유=«{TileReason}» · " +
                $"헤더=«{Caption}»(다른 가능 타일 {OtherReadyTiles}) · 클릭 뒤 [{AfterClick}] · ForceTriggerNow={ForceResult} 뒤 [{AfterForce}] · " +
                $"끝 허가={LeaseAtEnd} · 끝 명령창={PopoverOpenAtEnd}";
        }

        private void OnCrackOverlayChanged(WindowCrashOverlayEvent e)
        {
            switch (e.Phase)
            {
                case SpectacleOverlayPhase.Started:
                    _crack.Started++;
                    _crack.StartedFrame = Time.frameCount;
                    _crack.StartedAt = Time.realtimeSinceStartup;
                    return;
                case SpectacleOverlayPhase.Cancelled:
                    _crack.Cancelled++;
                    _crack.CancelledFrame = Time.frameCount;
                    _crack.CancelledAt = Time.realtimeSinceStartup;
                    return;
                case SpectacleOverlayPhase.Completed:
                    _crack.Completed++;
                    _crack.CompletedAt = Time.realtimeSinceStartup;
                    return;
                default:
                    // 정상값은 위 셋이다. 여기 오면 단계가 늘었는데 이 관측기가 모른다 — 조용히 버리지 않고 세서 단언이 드러낸다.
                    _crack.UnknownPhase++;
                    Debug.LogWarning($"{LogPrefix} 알 수 없는 크랙 오버레이 단계({(int)e.Phase}) — 관측기를 함께 고치십시오.");
                    return;
            }
        }

        private void OnCrackStateTransitioned(StateTransitionEvent e)
        {
            if (e.To == StickmanStateId.WindowCrash) _crack.WindowCrashTransitions++;
        }

        private bool CharacterCanTakeCommand()
        {
            StickmanStateId s = _agent.Blackboard.Machine.CurrentStateId;
            return (s == StickmanStateId.Idle || s == StickmanStateId.Walk) && !SpectacleEventLock.IsActive;
        }

        /// <summary>감독 · 명령창을 찾고, 의도 소스를 정지로 바꾸고, 관측을 붙이고, 캐릭터가 명령을 받을 수 있는 상태가 될 때까지 기다린다.</summary>
        private IEnumerator PrepareCrackHarness(string what)
        {
            _crash = Object.FindFirstObjectByType<WindowCrashDirector>(FindObjectsInactive.Include);
            Assert.IsNotNull(_crash, $"{LogPrefix} {what} — 씬에 WindowCrashDirector가 없습니다.");
            _popover = _agent.GetComponent<ActionCommandPopover>();
            Assert.IsNotNull(_popover, $"{LogPrefix} {what} — 캐릭터에 ActionCommandPopover가 없습니다(부채꼴 [행동]이 여는 창).");

            _savedIntent = _agent.Blackboard.IntentSource;
            _agent.Blackboard.IntentSource = new StillIntent();
            _intentHijacked = true;

            StickmanEventBus.WindowCrashOverlayChanged += OnCrackOverlayChanged;
            StickmanEventBus.StateTransitioned += OnCrackStateTransitioned;
            _crackObserverAttached = true;
            _crack.Reset();

            yield return WaitUntilOrTimeout(CharacterCanTakeCommand, CommandReadyBudgetSeconds);
            Assert.IsTrue(CharacterCanTakeCommand(),
                $"{LogPrefix} {what} 전제 — {CommandReadyBudgetSeconds:F0}초 안에 캐릭터가 Idle/Walk + 락 비움에 오지 않았습니다" +
                $"(상태 {_agent.Blackboard.Machine.CurrentStateId}, 락 {SpectacleEventLock.IsActive}). 아래 판정이 다른 사유에 가려집니다.");
        }

        /// <summary>사용자 경로 그대로 연다: 캐릭터 우클릭 → 부채꼴 → [행동]. 타일이 한 번 갱신될 만큼 기다린다.</summary>
        private IEnumerator OpenCommandPopoverFromFan(string what, bool expectGrant)
        {
            AssertCursorInside();
            RightDown();
            RightUp();
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} {what} 전제 — 캐릭터 우클릭이 부채꼴을 펴지 않았습니다.");
            Assert.AreEqual(expectGrant, _agent.IsUserSummonGrantActive,
                $"{LogPrefix} {what} 전제 — 우클릭 뒤 허가가 {_agent.IsUserSummonGrantActive}입니다(기대 {expectGrant}).");

            _menu.Activate((int)GearMenuButton.Action);
            Assert.IsTrue(_popover.IsOpen, $"{LogPrefix} {what} 전제 — 부채꼴 [행동]이 명령창을 열지 않았습니다.");

            yield return Wait(SettleSeconds);   // 명령창 안전 폴링(타일 갱신)이 한 번은 돈다.
            Assert.IsTrue(_popover.IsOpen && _menu.IsVisible, $"{LogPrefix} {what} 전제 — 명령창이나 부채꼴이 걷혔습니다(창 {_popover.IsOpen} · 부채꼴 {_menu.IsVisible}).");
            Assert.AreEqual(expectGrant, _agent.IsUserSummonGrantActive,
                $"{LogPrefix} {what} 전제 — 명령창이 열린 뒤 허가가 {_agent.IsUserSummonGrantActive}입니다(기대 {expectGrant}). 부채꼴 임대 갱신이 끊겼거나 새로 났습니다.");
        }

        private float CrackObserveSeconds() =>
            Mathf.Max(_config.windowCrashSwingDuration, UserSurfaceSummonPolicy.LeaseSeconds) + ObserveSlackSeconds;

        private IEnumerator ObserveCrack(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>임대 칸에서 명령 크랙을 잰다 — 판정 · 타일 · 실제 클릭 · ⌃⌥⌘X와 같은 <c>ForceTriggerNow</c>.</summary>
        private IEnumerator MeasureCrackCommand(string what, CrackCommandReport r)
        {
            // ★ 판정이 상태 조건에 가려지지 않게 잰다 — 캐릭터가 Idle/Walk + 락 비움을 SettleSeconds 동안 유지해
            //   명령창 안전 폴링이 그 상태로 타일을 한 번 갱신한 뒤. (수정 전 박제 1회차에서 우클릭 직후 LandingCrouch에
            //   걸려 타일이 «착지 중» 사유로 회색이었고, 그래서 타일 절반이 결함을 재지 못했다.)
            float stableSince = -1f;
            float deadline = Time.realtimeSinceStartup + CommandReadyBudgetSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (CharacterCanTakeCommand()) { if (stableSince < 0f) stableSince = Time.realtimeSinceStartup; }
                else stableSince = -1f;
                if (stableSince >= 0f && Time.realtimeSinceStartup - stableSince >= SettleSeconds) break;
                yield return null;
            }
            r.StateAtMeasure = _agent.Blackboard.Machine.CurrentStateId;
            r.CouldTakeCommand = CharacterCanTakeCommand();
            r.StableSeconds = stableSince >= 0f ? Time.realtimeSinceStartup - stableSince : 0f;
            r.Director = _crash.GetAvailability();
            r.TileJudgement = _popover.GetAvailability(CrackCommand);
            r.TileReady = _popover.IsCommandReady(CrackCommand);
            r.TileReason = _popover.CommandReason(CrackCommand);
            r.Caption = _popover.StatusCaption;
            for (int i = 0; i < ActionCommandPopover.CommandCount; i++)
                if ((ActionCommandPopover.Command)i != CrackCommand && _popover.IsCommandReady((ActionCommandPopover.Command)i)) r.OtherReadyTiles++;

            _crack.Reset();
            _popover.FeedClickForTests(_popover.CommandScreenRect(CrackCommand).center);
            yield return ObserveCrack(CrackObserveSeconds());
            r.AfterClick = _crack.Snapshot();

            // 수정 전 트리에서는 방금 휘두르기가 끝나야 상태 조건에 막히지 않는다. 수정 뒤에는 전이가 없어 기다리지 않고 통과한다.
            yield return WaitUntilOrTimeout(CharacterCanTakeCommand, CommandReadyBudgetSeconds);
            _crack.Reset();
            r.ForceResult = _crash.ForceTriggerNow($"{what} — ⌃⌥⌘X와 같은 경로");
            yield return ObserveCrack(CrackObserveSeconds());
            r.AfterForce = _crack.Snapshot();

            r.LeaseAtEnd = _agent.IsUserSummonGrantActive;
            r.PopoverOpenAtEnd = _popover.IsOpen;
            Debug.Log($"{LogPrefix} {what} — {r}");
        }

        private void AssertCrackCommandSuppressed(string what, CrackCommandReport r)
        {
            Assert.IsTrue(r.CouldTakeCommand && r.StableSeconds >= SettleSeconds,
                $"{LogPrefix} {what} 전제 — 잴 때 캐릭터가 명령을 받을 수 있는 상태로 안정되지 않았습니다({r}). 아래 «회색»이 상태 · 락 때문일 수 있습니다.");
            Assert.IsFalse(r.Director.IsReady,
                $"{LogPrefix} ★ {what} — [창 부수기] 판정이 «가능»입니다({r}). 사용자가 연 창의 임대에 명령 크랙이 편승합니다(A1).");
            Assert.AreEqual(ExpectedCrackLeaseReason, r.Director.Reason,
                $"{LogPrefix} ★ {what} — 불가 사유가 A1 사유가 아닙니다({r}). 다른 사유로 우연히 막힌 것이면 그 사유가 사라지는 순간 다시 뚫립니다.");
            Assert.AreEqual(r.Director.IsReady, r.TileJudgement.IsReady, $"{LogPrefix} {what} — 명령창이 부른 판정과 감독 판정이 다릅니다(진실 두 벌, 36-7).");
            Assert.IsFalse(r.TileReady, $"{LogPrefix} ★ {what} — 타일이 회색이 아닙니다({r}).");
            Assert.AreEqual(ExpectedCrackLeaseReason, r.TileReason, $"{LogPrefix} ★ {what} — 타일 설명 자리의 사유가 A1 사유가 아닙니다({r}).");

            Assert.AreEqual(0, r.AfterClick.UnknownPhase + r.AfterForce.UnknownPhase, $"{LogPrefix} {what} — 관측기가 모르는 오버레이 단계가 나왔습니다({r}).");
            Assert.AreEqual(0, r.AfterClick.WindowCrashTransitions, $"{LogPrefix} ★ {what} — 회색 타일을 눌렀는데 캐릭터가 휘둘렀습니다({r}).");
            Assert.AreEqual(0, r.AfterClick.Started, $"{LogPrefix} ★ {what} — 회색 타일을 눌렀는데 금 오버레이가 시작됐습니다({r}).");
            Assert.AreEqual(0, r.AfterClick.Cancelled, $"{LogPrefix} ★ {what} — 타일 클릭 뒤 금 오버레이 취소가 관측됐습니다 — 시작했다가 걷힌 것입니다({r}).");
            Assert.IsFalse(r.ForceResult, $"{LogPrefix} ★ {what} — ⌃⌥⌘X와 같은 경로(ForceTriggerNow)가 발동했다고 답했습니다({r}).");
            Assert.AreEqual(0, r.AfterForce.WindowCrashTransitions, $"{LogPrefix} ★ {what} — ForceTriggerNow 뒤 휘두르기 전이가 있었습니다({r}).");
            Assert.AreEqual(0, r.AfterForce.Started, $"{LogPrefix} ★ {what} — ForceTriggerNow 뒤 금 오버레이가 시작됐습니다({r}).");
            Assert.AreEqual(0, r.AfterForce.Cancelled, $"{LogPrefix} ★ {what} — ForceTriggerNow 뒤 금 오버레이 취소가 관측됐습니다({r}).");

            Assert.IsTrue(r.LeaseAtEnd, $"{LogPrefix} {what} — 관측 끝에서 허가가 죽어 있습니다(임대 칸을 재지 못했습니다).");
            Assert.IsTrue(r.PopoverOpenAtEnd, $"{LogPrefix} {what} — 명령창이 관측 중 닫혔습니다(사용자 표면 회귀 또는 전제 붕괴).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R5a_프로덕션_톱니세계_등급1에서_사용자가_연_행동_명령창의_창_부수기는_회색과_사유이고_눌러도_단축키_경로로도_휘두르기와_금이_0이다()
        {
            yield return LoadScene();
            yield return PrepareCrackHarness("R5a");
            yield return EnterTierOne("R5a");
            yield return OpenCommandPopoverFromFan("R5a", expectGrant: true);

            Assert.IsTrue((bool)PanelRetreatField.GetValue(_agent), $"{LogPrefix} R5a 전제 — 축 3이 꺼졌습니다(FTT 칸이 아닙니다).");
            Assert.IsFalse(_agent.ArePanelsSuppressed, $"{LogPrefix} R5a 전제 — 옛 창구가 참입니다. 명령창이 떠 있을 수 없는 칸입니다.");

            var r = new CrackCommandReport();
            yield return MeasureCrackCommand("R5a(등급 1 · 명령창 열림 · FTT 칸)", r);
            AssertCrackCommandSuppressed("R5a(등급 1 · 명령창 열림 · FTT 칸)", r);
            Debug.Log($"{LogPrefix} R5a 확인 — FTT 칸에서 [창 부수기]는 회색 + A1 사유, 클릭 · ForceTriggerNow 모두 휘두르기 0 · 금 0.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R5b_프로덕션_톱니세계_이력칸_등급1이_끝나도_그때_연_명령창이_떠_있으면_창_부수기는_회색이고_연_것을_다_닫으면_사유가_사라진다()
        {
            yield return LoadScene();
            yield return PrepareCrackHarness("R5b");
            yield return EnterTierOne("R5b");
            yield return OpenCommandPopoverFromFan("R5b", expectGrant: true);

            SetPanelRetreat(false);   // 전체화면 앱이 끝났다. 부채꼴 · 명령창은 그대로 — FFT 칸.
            yield return Wait(SettleSeconds);
            Assert.IsFalse((bool)PanelRetreatField.GetValue(_agent), $"{LogPrefix} R5b 전제 — 축 3이 아직 켜져 있습니다.");
            Assert.IsFalse(_agent.ArePanelsSuppressed, $"{LogPrefix} R5b 전제 — 옛 창구가 참입니다(FFT 칸이 아닙니다).");
            Assert.IsTrue(_agent.IsUserSummonGrantActive, $"{LogPrefix} R5b 전제 — 허가가 죽었습니다(부채꼴 갱신이 끊김).");
            Assert.IsTrue(_popover.IsOpen && _menu.IsVisible, $"{LogPrefix} R5b 전제 — 명령창이나 부채꼴이 걷혔습니다.");

            var r = new CrackCommandReport();
            yield return MeasureCrackCommand("R5b(등급 1 종료 · 명령창 열림 · FFT 칸)", r);
            AssertCrackCommandSuppressed("R5b(등급 1 종료 · 명령창 열림 · FFT 칸)", r);

            // ---------- 사유가 약속한 조건(StickMate 창과 버튼을 다 닫고 잠시 뒤 다시 열기)을 채우면 이 사유가 사라지는가 ----------
            //   ★ 2026-09-26 — 사유 문구가 S6로 바뀌었다(design-narrative 5판 · 리더 확정). 여기서 채우는 것은 그 문구의
            //     «다 닫고 → 다시 열면» 절반이다. 문구의 「3초쯤 뒤」는 <b>전체화면 판정이 풀리기까지의 지연</b>(폴링 1.5초 ×
            //     디바운서 유지 1.0초)을 사용자에게 옮긴 값인데, 이 케이스는 등급을 리플렉션으로 이미 내려 둔 FFT 칸에서
            //     시작하므로 그 지연이 존재하지 않는다 — 즉 이 칸은 「3초쯤」을 재는 자리가 아니다(사유 문서 §3-3 비용 표의
            //     test-engineer 메모와 같은 판단). 여기서 기다리는 것은 임대 만료뿐이다.
            _menu.ForceCloseAll("R5b — 전체화면이 끝난 뒤 StickMate 창과 버튼을 다 닫는다");
            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsFalse(_popover.IsOpen || _menu.IsVisible, $"{LogPrefix} R5b 약속 확인 전제 — 닫았는데 명령창이나 부채꼴이 남았습니다.");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} R5b 약속 확인 전제 — 다 닫았는데 허가가 {UserSurfaceSummonPolicy.LeaseSeconds * 3f:F2}초 뒤까지 남았습니다.");

            yield return WaitUntilOrTimeout(CharacterCanTakeCommand, CommandReadyBudgetSeconds);
            yield return OpenCommandPopoverFromFan("R5b 약속 확인(등급 없음에서 다시 연 명령창)", expectGrant: false);
            yield return WaitUntilOrTimeout(() => _popover.IsCommandReady(CrackCommand), CommandReadyBudgetSeconds);
            CommandAvailability after = _crash.GetAvailability();
            Debug.Log($"{LogPrefix} R5b 약속 확인 — 다시 연 명령창의 [창 부수기] 가능={after.IsReady} 사유=«{after.Reason}» · 타일 가능={_popover.IsCommandReady(CrackCommand)}");
            Assert.AreNotEqual(ExpectedCrackLeaseReason, after.Reason,
                $"{LogPrefix} ★ R5b — 사유가 약속한 조건을 다 채웠는데 같은 사유가 다시 나옵니다. 문구가 거짓 약속이 됩니다.");
            Assert.IsTrue(after.IsReady && _popover.IsCommandReady(CrackCommand),
                $"{LogPrefix} R5b — 조건을 채우고 다시 연 명령창에서 [창 부수기]가 가능해지지 않았습니다(사유 «{after.Reason}»). 위 «회색»이 «원래 못 누르는 세계»와 구별되지 않습니다.");
            Debug.Log($"{LogPrefix} R5b 확인 — FFT 칸에서 회색 + A1 사유, 휘두르기 0 · 금 0. 연 것을 다 닫고 다시 열자 [창 부수기]가 가능해졌습니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R5c_음성대조_프로덕션_톱니세계_등급없음에서_명령창의_창_부수기는_가능하고_누르면_금이_설정_수명_동안_취소_없이_보인다()
        {
            yield return LoadScene();
            yield return PrepareCrackHarness("R5c");
            yield return OpenCommandPopoverFromFan("R5c", expectGrant: false);
            Assert.IsFalse(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive || (bool)PanelRetreatField.GetValue(_agent),
                $"{LogPrefix} R5c 전제 — 등급 없음 · 무허가가 아닙니다.");

            yield return WaitUntilOrTimeout(() => _popover.IsCommandReady(CrackCommand) && _crash.GetAvailability().IsReady, CommandReadyBudgetSeconds);
            CommandAvailability before = _crash.GetAvailability();
            Assert.IsTrue(before.IsReady && _popover.IsCommandReady(CrackCommand),
                $"{LogPrefix} ★ R5c 음성 대조 실패 — 등급 없음에서 [창 부수기]가 가능하지 않습니다(사유 «{before.Reason}», 타일 {_popover.IsCommandReady(CrackCommand)}). R5a · R5b · R5d의 «회색»은 무효입니다.");

            _crack.Reset();
            float duration = _config.windowCrashOverlayDurationSeconds;
            float budget = duration * 2f + ObserveSlackSeconds;
            float clickedAt = Time.realtimeSinceStartup;
            int popoverClosedFrames = 0;
            _popover.FeedClickForTests(_popover.CommandScreenRect(CrackCommand).center);
            while (Time.realtimeSinceStartup < clickedAt + budget && _crack.Completed == 0 && _crack.Cancelled == 0)
            {
                yield return null;
                if (!_popover.IsOpen) popoverClosedFrames++;
            }
            float shown = _crack.CompletedAt >= 0f && _crack.StartedAt >= 0f ? _crack.CompletedAt - _crack.StartedAt : -1f;
            Debug.Log($"{LogPrefix} R5c — 등급 없음 클릭: [{_crack}] · 금이 보인 벽시계 {shown:F3}초(설정 수명 {duration:F2}초) · 명령창 닫힘 프레임 {popoverClosedFrames}");

            Assert.AreEqual(0, _crack.UnknownPhase, $"{LogPrefix} R5c — 관측기가 모르는 오버레이 단계가 나왔습니다.");
            Assert.AreEqual(1, _crack.Started, $"{LogPrefix} ★ R5c 음성 대조 실패 — 가능한 타일을 눌렀는데 금 오버레이가 시작되지 않았습니다([{_crack}]). 관측기가 죽었을 수 있습니다.");
            Assert.AreEqual(1, _crack.WindowCrashTransitions, $"{LogPrefix} ★ R5c 음성 대조 실패 — 휘두르기 전이가 {_crack.WindowCrashTransitions}회입니다(기대 1).");
            Assert.AreEqual(0, _crack.Cancelled, $"{LogPrefix} ★ R5c — 등급 없음에서 금이 취소됐습니다([{_crack}]). 이 세계에서 금이 원래 안 남으면 R5의 «0»은 무효입니다.");
            Assert.AreEqual(1, _crack.Completed, $"{LogPrefix} ★ R5c — 벽시계 {budget:F2}초 안에 금이 수명을 채우고 끝나지 않았습니다([{_crack}]).");
            Assert.GreaterOrEqual(shown, duration - Time.maximumDeltaTime,
                $"{LogPrefix} ★ R5c — 금이 {shown:F3}초만 보였습니다(설정 수명 {duration:F2}초, 허용 오차 = 한 프레임 상한 {Time.maximumDeltaTime:F3}초).");
            Assert.AreEqual(0, popoverClosedFrames, $"{LogPrefix} R5c — 금이 보이는 동안 명령창이 닫혔습니다(2026-09-02 «메뉴가 유지되어야함» 회귀).");
            Debug.Log($"{LogPrefix} R5c 확인 — 등급 없음에서 [창 부수기]는 가능했고, 누르자 휘두르기 1회 · 금이 {shown:F2}초 동안 취소 없이 보였습니다(측정기 생존).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R5d_가드위치_등급1에서_숨김이면_숨김_사유가_먼저이고_락이_잡혀_있으면_A1_사유가_락_사유보다_먼저다()
        {
            yield return LoadScene();
            yield return PrepareCrackHarness("R5d");
            yield return EnterTierOne("R5d");
            yield return OpenCommandPopoverFromFan("R5d", expectGrant: true);

            // ---------- (i) 숨김 + 등급 1: 숨김 사유가 나와야 한다(숨김은 A1 사유 문구를 채워도 풀리지 않는 지속 사유라서 먼저 보여야 한다) ----------
            _agent.SetUserHidden(true, "R5d — 등급 1에서 명령창을 연 채 캐릭터를 숨긴다");
            _userHiddenByTest = true;
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} R5d 전제 — 숨김이 IsSuspended로 이어지지 않았습니다.");
            Assert.IsTrue(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive, $"{LogPrefix} R5d 전제 — A1 가드 조건(옛 창구 ∨ 허가)이 거짓입니다(가드 순서를 재지 못합니다).");
            CommandAvailability hidden = _crash.GetAvailability();
            bool popoverOpenWhileHidden = _popover.IsOpen;
            string hiddenTileReason = popoverOpenWhileHidden ? _popover.CommandReason(CrackCommand) : null;
            Debug.Log($"{LogPrefix} R5d(i) 숨김 + 등급 1 — 가능={hidden.IsReady} 사유=«{hidden.Reason}» · 명령창 열림={popoverOpenWhileHidden} 타일 사유=«{hiddenTileReason}»");
            Assert.IsFalse(hidden.IsReady, $"{LogPrefix} ★ R5d(i) — 숨김 + 등급 1인데 [창 부수기]가 가능입니다.");
            Assert.AreEqual(HiddenCharacterCommandGate.HiddenReason, hidden.Reason,
                $"{LogPrefix} ★ R5d(i) — 숨김 + 등급 1에서 사유가 숨김 사유가 아닙니다(«{hidden.Reason}»). A1 가드가 숨김 게이트보다 앞에 있습니다 — 숨김은 A1 사유 문구를 채워도 풀리지 않는 지속 사유라서 먼저 보여야 합니다.");
            if (popoverOpenWhileHidden)
                Assert.AreEqual(HiddenCharacterCommandGate.HiddenReason, hiddenTileReason, $"{LogPrefix} ★ R5d(i) — 타일 사유가 숨김 사유가 아닙니다.");

            _agent.SetUserHidden(false, "R5d — 숨김 해제");
            _userHiddenByTest = false;
            yield return Wait(SettleSeconds);
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} R5d 전제 — 숨김이 풀리지 않았습니다.");

            // ---------- (ii) 등급 1 + 락 점유: A1 사유가 락 사유보다 먼저여야 한다(기다리면 될 것처럼 보였다가 아니게 되지 않게) ----------
            Assert.IsTrue((bool)PanelRetreatField.GetValue(_agent), $"{LogPrefix} R5d(ii) 전제 — 축 3이 꺼졌습니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive, $"{LogPrefix} R5d(ii) 전제 — 테스트가 잡기 전에 이미 락이 잡혀 있습니다.");
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Archery, this), $"{LogPrefix} R5d(ii) 전제 — 테스트가 락을 잡지 못했습니다.");
            _testHoldsLock = true;
            CommandAvailability locked = _crash.GetAvailability();
            SpectacleEventLock.Release(this);
            _testHoldsLock = false;
            string busy = StickMateDisplayNames.BusyText(SpectacleEventKind.Archery);
            Debug.Log($"{LogPrefix} R5d(ii) 등급 1 + 락 — 가능={locked.IsReady} 사유=«{locked.Reason}» (락 사유 문형 «{busy}»)");
            Assert.IsFalse(locked.IsReady, $"{LogPrefix} ★ R5d(ii) — 락이 잡혀 있는데 가능입니다.");
            Assert.AreEqual(ExpectedCrackLeaseReason, locked.Reason,
                $"{LogPrefix} ★ R5d(ii) — 등급 1 + 락에서 사유가 A1 사유가 아닙니다(«{locked.Reason}»). A1 가드가 락 검사보다 뒤에 있습니다.");

            // ---------- 대조: 등급 없음 · 무허가에서 같은 락이면 락 사유가 나온다(락 판정이 살아 있고 위 비교가 가를 수 있다) ----------
            _menu.ForceCloseAll("R5d 대조 — 연 것을 다 닫는다");
            SetPanelRetreat(false);
            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsFalse(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive, $"{LogPrefix} R5d 대조 전제 — 등급 없음 · 무허가가 아닙니다.");
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Archery, this), $"{LogPrefix} R5d 대조 전제 — 테스트가 락을 잡지 못했습니다.");
            _testHoldsLock = true;
            CommandAvailability control = _crash.GetAvailability();
            SpectacleEventLock.Release(this);
            _testHoldsLock = false;
            Debug.Log($"{LogPrefix} R5d 대조 등급 없음 + 락 — 가능={control.IsReady} 사유=«{control.Reason}»");
            Assert.AreEqual(busy, control.Reason, $"{LogPrefix} R5d 대조 실패 — 등급 없음 + 락에서 락 사유가 나오지 않습니다(«{control.Reason}»). 위 (ii) 비교가 아무것도 가르지 못합니다.");
            Debug.Log($"{LogPrefix} R5d 확인 — 숨김이면 숨김 사유, 등급 1 + 락이면 A1 사유, 등급 없음 + 락이면 락 사유.");
        }

        // ==================== R6 · R7 — 해제 조건 7-b: 명령 그라피티(행동 명령창 [낙서하기] · ⌃⌥⌘G) ====================
        //
        // 설계: docs/systems/UNSUMMONED_EFFECTS_INVARIANT.md 「3. 판정 ②」(층 1 시작 · 층 2 수명) · 「4. 판정 ③」(명령 3종).
        // 층 1 결함: 등급 1에서 사용자가 연 명령창이 임대를 갱신하는 동안 [낙서하기]가 «준비됨»으로 보이고, 누르면
        //           발표·회의 화면 위에 낙서가 시작된다. 대상 선정(빈 자리 찾기)이 우연히 막을 수는 있지만 그것은
        //           가드가 아니고, 사유도 「낙서할 빈 자리가 없어요」로 상태에서 파생되지 않는다(원칙 1).
        // 층 2 결함: 등급 0에서 사용자가 직접 시킨 낙서(출하 값 3~5초)가 그려지는 동안 발표가 시작되면 그대로 남는다.
        // ★ 발판 목록은 어느 칸에서도 <b>건드리지 않는다</b> — 그래야 MonitorRegion의 «겹침 취소»와 7-b 가드의 취소가 갈린다.

        /// <summary>
        /// ★ 7-b 사유 — 프로덕션 상수를 <b>참조</b>한다(CLAUDE.md — 문구를 베끼지 않는다).
        /// <para>크랙(A1)과 <b>같은 글자 하나</b>를 쓰는 것이 설계다(사유 문서 §7). 그 «하나»(= 프로덕션 소스에
        /// 이 문구 리터럴이 정확히 한 번만 있다)는 EditMode <c>UnsummonedSurfaceAxisTests</c>의
        /// 사유 문구 복제 검사가 따로 못박는다 — 이 속성만 있으면 누군가 문구를 두 벌로 쪼개도
        /// 이 파일은 각자의 상수를 보며 조용히 초록이다.</para>
        /// </summary>
        private static string ExpectedUnsummonedReason => UnsummonedSurfaceCommandReason.Text;

        private const ActionCommandPopover.Command GraffitiCommand = ActionCommandPopover.Command.Graffiti;

        /// <summary>명령 그라피티 관측 계수 — 이벤트 버스에서 직접 센다(렌더러를 거치지 않는다).</summary>
        private sealed class GraffitiTally
        {
            public int GraffitiTransitions;
            public int Started;
            public int Cancelled;
            public int Completed;
            public int UnknownPhase;
            public int Exits;
            public float StartedAt = -1f;
            public float CancelledAt = -1f;
            public float CompletedAt = -1f;
            public StickmanStateId LastExitTo;
            public bool LastExitAbnormal;

            public void Reset()
            {
                GraffitiTransitions = Started = Cancelled = Completed = UnknownPhase = Exits = 0;
                StartedAt = CancelledAt = CompletedAt = -1f;
                LastExitTo = default;
                LastExitAbnormal = false;
            }

            public GraffitiTally Snapshot() => (GraffitiTally)MemberwiseClone();

            public override string ToString() =>
                $"낙서 전이 {GraffitiTransitions} · Started {Started} · Cancelled {Cancelled} · Completed {Completed} · 미상 단계 {UnknownPhase}" +
                $" · 이탈 {Exits}" + (Exits > 0 ? $"(→ {LastExitTo}, 비정상={LastExitAbnormal})" : string.Empty) +
                (StartedAt >= 0f && Cancelled > 0 ? $" · Started→Cancelled {CancelledAt - StartedAt:F3}초" : string.Empty);
        }

        /// <summary>한 칸에서 잰 것 전부. 단언보다 먼저 로그로 남긴다 — 무엇이 일어났는가가 첫 실패 단언에 가려지지 않게.</summary>
        private sealed class GraffitiCommandReport
        {
            public StickmanStateId StateAtMeasure;
            public bool CouldTakeCommand;
            public float StableSeconds;
            public CommandAvailability Director;
            public CommandAvailability TileJudgement;
            public bool TileReady;
            public string TileReason;
            public string Caption;
            public GraffitiTally AfterClick;
            public bool ForceResult;
            public GraffitiTally AfterForce;
            public bool LeaseAtEnd;
            public bool PopoverOpenAtEnd;

            public override string ToString() =>
                $"잴 때 상태 {StateAtMeasure}(명령 받을 수 있음={CouldTakeCommand}, 안정 {StableSeconds:F2}초) · " +
                $"판정(감독) 가능={Director.IsReady} 사유=«{Director.Reason}» · 타일 가능={TileReady} 사유=«{TileReason}» · " +
                $"헤더=«{Caption}» · 클릭 뒤 [{AfterClick}] · ForceTriggerNow={ForceResult} 뒤 [{AfterForce}] · " +
                $"끝 허가={LeaseAtEnd} · 끝 명령창={PopoverOpenAtEnd}";
        }

        private void OnGraffitiOverlayChanged(GraffitiOverlayEvent e)
        {
            switch (e.Phase)
            {
                case SpectacleOverlayPhase.Started:
                    _paint.Started++;
                    _paint.StartedAt = Time.realtimeSinceStartup;
                    return;
                case SpectacleOverlayPhase.Cancelled:
                    _paint.Cancelled++;
                    _paint.CancelledAt = Time.realtimeSinceStartup;
                    return;
                case SpectacleOverlayPhase.Completed:
                    _paint.Completed++;
                    _paint.CompletedAt = Time.realtimeSinceStartup;
                    return;
                default:
                    // 정상값은 위 셋이다. 여기 오면 단계가 늘었는데 이 관측기가 모른다 — 조용히 버리지 않고 세서 단언이 드러낸다.
                    _paint.UnknownPhase++;
                    Debug.LogWarning($"{LogPrefix} 알 수 없는 낙서 오버레이 단계({(int)e.Phase}) — 관측기를 함께 고치십시오.");
                    return;
            }
        }

        private void OnGraffitiStateTransitioned(StateTransitionEvent e)
        {
            if (e.To == StickmanStateId.Graffiti) _paint.GraffitiTransitions++;
            if (e.From == StickmanStateId.Graffiti)
            {
                _paint.Exits++;
                _paint.LastExitTo = e.To;
                _paint.LastExitAbnormal = e.IsAbnormalExit;
            }
        }

        /// <summary>감독 · 명령창을 찾고, 의도 소스를 정지로 바꾸고(배회 AI가 발판을 벗어나 상태를 흔들지 않게), 관측을 붙인다.</summary>
        private IEnumerator PrepareGraffitiHarness(string what)
        {
            _graffiti = Object.FindFirstObjectByType<GraffitiDirector>(FindObjectsInactive.Include);
            Assert.IsNotNull(_graffiti, $"{LogPrefix} {what} — 씬에 GraffitiDirector가 없습니다.");
            _popover = _agent.GetComponent<ActionCommandPopover>();
            Assert.IsNotNull(_popover, $"{LogPrefix} {what} — 캐릭터에 ActionCommandPopover가 없습니다(부채꼴 [행동]이 여는 창).");

            _savedIntent = _agent.Blackboard.IntentSource;
            _agent.Blackboard.IntentSource = new StillIntent();
            _intentHijacked = true;

            StickmanEventBus.GraffitiOverlayChanged += OnGraffitiOverlayChanged;
            StickmanEventBus.StateTransitioned += OnGraffitiStateTransitioned;
            _graffitiObserverAttached = true;
            _paint.Reset();

            yield return WaitUntilOrTimeout(CharacterCanTakeCommand, CommandReadyBudgetSeconds);
            Assert.IsTrue(CharacterCanTakeCommand(),
                $"{LogPrefix} {what} 전제 — {CommandReadyBudgetSeconds:F0}초 안에 캐릭터가 Idle/Walk + 락 비움에 오지 않았습니다" +
                $"(상태 {_agent.Blackboard.Machine.CurrentStateId}, 락 {SpectacleEventLock.IsActive}). 아래 판정이 다른 사유에 가려집니다.");
        }

        /// <summary>층 1 관측 예산(초) — «시작하지 않았다»를 재는 자리라 임대 수명과 정착 중 긴 쪽에 슬랙을 얹는다.</summary>
        private float GraffitiObserveSeconds() =>
            Mathf.Max(UserSurfaceSummonPolicy.LeaseSeconds, SettleSeconds) + ObserveSlackSeconds;

        private IEnumerator ObserveGraffiti(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>임대 칸에서 명령 그라피티를 잰다 — 판정 · 타일 · 실제 클릭 · ⌃⌥⌘G와 같은 <c>ForceTriggerNow</c>.</summary>
        private IEnumerator MeasureGraffitiCommand(string what, GraffitiCommandReport r)
        {
            // ★ 판정이 상태·락 조건에 가려지지 않게, 캐릭터가 Idle/Walk + 락 비움을 SettleSeconds 동안 유지한 뒤 잰다
            //   (R5 박제에서 우클릭 직후 착지 상태에 걸려 타일이 «착지 중»으로 회색이던 사고와 같은 대비).
            float stableSince = -1f;
            float deadline = Time.realtimeSinceStartup + CommandReadyBudgetSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (CharacterCanTakeCommand()) { if (stableSince < 0f) stableSince = Time.realtimeSinceStartup; }
                else stableSince = -1f;
                if (stableSince >= 0f && Time.realtimeSinceStartup - stableSince >= SettleSeconds) break;
                yield return null;
            }
            r.StateAtMeasure = _agent.Blackboard.Machine.CurrentStateId;
            r.CouldTakeCommand = CharacterCanTakeCommand();
            r.StableSeconds = stableSince >= 0f ? Time.realtimeSinceStartup - stableSince : 0f;
            r.Director = _graffiti.GetAvailability();
            r.TileJudgement = _popover.GetAvailability(GraffitiCommand);
            r.TileReady = _popover.IsCommandReady(GraffitiCommand);
            r.TileReason = _popover.CommandReason(GraffitiCommand);
            r.Caption = _popover.StatusCaption;

            _paint.Reset();
            _popover.FeedClickForTests(_popover.CommandScreenRect(GraffitiCommand).center);
            yield return ObserveGraffiti(GraffitiObserveSeconds());
            r.AfterClick = _paint.Snapshot();

            yield return WaitUntilOrTimeout(CharacterCanTakeCommand, CommandReadyBudgetSeconds);
            _paint.Reset();
            r.ForceResult = _graffiti.ForceTriggerNow($"{what} — ⌃⌥⌘G와 같은 경로");
            yield return ObserveGraffiti(GraffitiObserveSeconds());
            r.AfterForce = _paint.Snapshot();

            r.LeaseAtEnd = _agent.IsUserSummonGrantActive;
            r.PopoverOpenAtEnd = _popover.IsOpen;
            Debug.Log($"{LogPrefix} {what} — {r}");
        }

        private void AssertGraffitiCommandSuppressed(string what, GraffitiCommandReport r)
        {
            Assert.IsTrue(r.CouldTakeCommand && r.StableSeconds >= SettleSeconds,
                $"{LogPrefix} {what} 전제 — 잴 때 캐릭터가 명령을 받을 수 있는 상태로 안정되지 않았습니다({r}). 아래 «회색»이 상태·락 때문일 수 있습니다.");
            Assert.IsFalse(r.Director.IsReady,
                $"{LogPrefix} ★ {what} — [낙서하기] 판정이 «가능»입니다({r}). 사용자가 연 창의 임대에 명령 그라피티가 편승합니다(7-b 층 1).");
            Assert.AreEqual(ExpectedUnsummonedReason, r.Director.Reason,
                $"{LogPrefix} ★ {what} — 불가 사유가 7-b 사유가 아닙니다({r}). 특히 «낙서할 빈 자리가 없어요»로 막힌 것이라면 그것은 " +
                "대상 선정의 부수 효과이지 가드가 아니며, 그 사유가 사라지는 순간(빈 자리가 생기는 순간) 다시 뚫립니다.");
            Assert.AreEqual(r.Director.IsReady, r.TileJudgement.IsReady, $"{LogPrefix} {what} — 명령창이 부른 판정과 감독 판정이 다릅니다(진실 두 벌, 36-7).");
            Assert.IsFalse(r.TileReady, $"{LogPrefix} ★ {what} — 타일이 회색이 아닙니다({r}).");
            Assert.AreEqual(ExpectedUnsummonedReason, r.TileReason, $"{LogPrefix} ★ {what} — 타일 설명 자리의 사유가 7-b 사유가 아닙니다({r}).");

            Assert.AreEqual(0, r.AfterClick.UnknownPhase + r.AfterForce.UnknownPhase, $"{LogPrefix} {what} — 관측기가 모르는 오버레이 단계가 나왔습니다({r}).");
            Assert.AreEqual(0, r.AfterClick.GraffitiTransitions, $"{LogPrefix} ★ {what} — 회색 타일을 눌렀는데 캐릭터가 낙서 상태로 전이했습니다({r}).");
            Assert.AreEqual(0, r.AfterClick.Started, $"{LogPrefix} ★ {what} — 회색 타일을 눌렀는데 낙서 오버레이가 시작됐습니다({r}).");
            Assert.AreEqual(0, r.AfterClick.Cancelled, $"{LogPrefix} ★ {what} — 타일 클릭 뒤 낙서 취소가 관측됐습니다 — 시작했다가 걷힌 것입니다({r}).");
            Assert.IsFalse(r.ForceResult, $"{LogPrefix} ★ {what} — ⌃⌥⌘G와 같은 경로(ForceTriggerNow)가 발동했다고 답했습니다({r}).");
            Assert.AreEqual(0, r.AfterForce.GraffitiTransitions, $"{LogPrefix} ★ {what} — ForceTriggerNow 뒤 낙서 전이가 있었습니다({r}).");
            Assert.AreEqual(0, r.AfterForce.Started, $"{LogPrefix} ★ {what} — ForceTriggerNow 뒤 낙서 오버레이가 시작됐습니다({r}).");
            Assert.AreEqual(0, r.AfterForce.Cancelled, $"{LogPrefix} ★ {what} — ForceTriggerNow 뒤 낙서 취소가 관측됐습니다({r}).");

            Assert.IsTrue(r.LeaseAtEnd, $"{LogPrefix} {what} — 관측 끝에서 허가가 죽어 있습니다(임대 칸을 재지 못했습니다).");
            Assert.IsTrue(r.PopoverOpenAtEnd, $"{LogPrefix} {what} — 명령창이 관측 중 닫혔습니다(사용자 표면 회귀 또는 전제 붕괴).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R6a_프로덕션_톱니세계_등급1에서_사용자가_연_행동_명령창의_낙서하기는_회색과_사유이고_눌러도_단축키_경로로도_낙서가_0이다()
        {
            yield return LoadScene();
            yield return PrepareGraffitiHarness("R6a");
            yield return EnterTierOne("R6a");
            yield return OpenCommandPopoverFromFan("R6a", expectGrant: true);

            Assert.IsTrue((bool)PanelRetreatField.GetValue(_agent), $"{LogPrefix} R6a 전제 — 축 3이 꺼졌습니다(FTT 칸이 아닙니다).");
            Assert.IsFalse(_agent.ArePanelsSuppressed, $"{LogPrefix} R6a 전제 — 옛 창구가 참입니다. 명령창이 떠 있을 수 없는 칸입니다.");

            var r = new GraffitiCommandReport();
            yield return MeasureGraffitiCommand("R6a(등급 1 · 명령창 열림 · FTT 칸)", r);
            AssertGraffitiCommandSuppressed("R6a(등급 1 · 명령창 열림 · FTT 칸)", r);
            Debug.Log($"{LogPrefix} R6a 확인 — FTT 칸에서 [낙서하기]는 회색 + 7-b 사유, 클릭 · ForceTriggerNow 모두 낙서 0.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R6b_프로덕션_톱니세계_이력칸_등급1이_끝나도_그때_연_명령창이_떠_있으면_낙서하기는_회색이고_연_것을_다_닫으면_사유가_사라진다()
        {
            yield return LoadScene();
            yield return PrepareGraffitiHarness("R6b");
            yield return EnterTierOne("R6b");
            yield return OpenCommandPopoverFromFan("R6b", expectGrant: true);

            SetPanelRetreat(false);   // 전체화면 앱이 끝났다. 부채꼴 · 명령창은 그대로 — FFT 칸.
            yield return Wait(SettleSeconds);
            Assert.IsFalse((bool)PanelRetreatField.GetValue(_agent), $"{LogPrefix} R6b 전제 — 축 3이 아직 켜져 있습니다.");
            Assert.IsFalse(_agent.ArePanelsSuppressed, $"{LogPrefix} R6b 전제 — 옛 창구가 참입니다(FFT 칸이 아닙니다).");
            Assert.IsTrue(_agent.IsUserSummonGrantActive, $"{LogPrefix} R6b 전제 — 허가가 죽었습니다(부채꼴 갱신이 끊김).");

            var r = new GraffitiCommandReport();
            yield return MeasureGraffitiCommand("R6b(등급 1 종료 · 명령창 열림 · FFT 칸)", r);
            AssertGraffitiCommandSuppressed("R6b(등급 1 종료 · 명령창 열림 · FFT 칸)", r);

            // ---------- 사유가 약속한 조건(StickMate 창과 버튼을 다 닫고 다시 열기)을 채우면 이 사유가 사라지는가 ----------
            _menu.ForceCloseAll("R6b — 전체화면이 끝난 뒤 StickMate 창과 버튼을 다 닫는다");
            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsFalse(_popover.IsOpen || _menu.IsVisible, $"{LogPrefix} R6b 약속 확인 전제 — 닫았는데 명령창이나 부채꼴이 남았습니다.");
            Assert.IsFalse(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} R6b 약속 확인 전제 — 다 닫았는데 허가가 {UserSurfaceSummonPolicy.LeaseSeconds * 3f:F2}초 뒤까지 남았습니다.");

            yield return WaitUntilOrTimeout(CharacterCanTakeCommand, CommandReadyBudgetSeconds);
            yield return OpenCommandPopoverFromFan("R6b 약속 확인(등급 없음에서 다시 연 명령창)", expectGrant: false);
            yield return WaitUntilOrTimeout(() => _popover.IsCommandReady(GraffitiCommand), CommandReadyBudgetSeconds);
            CommandAvailability after = _graffiti.GetAvailability();
            Debug.Log($"{LogPrefix} R6b 약속 확인 — 다시 연 명령창의 [낙서하기] 가능={after.IsReady} 사유=«{after.Reason}» · 타일 가능={_popover.IsCommandReady(GraffitiCommand)}");
            Assert.AreNotEqual(ExpectedUnsummonedReason, after.Reason,
                $"{LogPrefix} ★ R6b — 사유가 약속한 조건을 다 채웠는데 같은 사유가 다시 나옵니다. 문구가 거짓 약속이 됩니다.");
            Assert.IsTrue(after.IsReady && _popover.IsCommandReady(GraffitiCommand),
                $"{LogPrefix} R6b — 조건을 채우고 다시 연 명령창에서 [낙서하기]가 가능해지지 않았습니다(사유 «{after.Reason}»). " +
                "위 «회색»이 «원래 못 누르는 세계»와 구별되지 않습니다.");
            Debug.Log($"{LogPrefix} R6b 확인 — FFT 칸에서 회색 + 7-b 사유, 낙서 0. 연 것을 다 닫고 다시 열자 [낙서하기]가 가능해졌습니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R6c_음성대조_프로덕션_톱니세계_등급없음에서_명령창의_낙서하기는_가능하고_누르면_낙서가_시작된다()
        {
            yield return LoadScene();
            yield return PrepareGraffitiHarness("R6c");
            yield return OpenCommandPopoverFromFan("R6c", expectGrant: false);
            Assert.IsFalse(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive || (bool)PanelRetreatField.GetValue(_agent),
                $"{LogPrefix} R6c 전제 — 등급 없음 · 무허가가 아닙니다.");

            yield return WaitUntilOrTimeout(() => _popover.IsCommandReady(GraffitiCommand) && _graffiti.GetAvailability().IsReady, CommandReadyBudgetSeconds);
            CommandAvailability before = _graffiti.GetAvailability();
            Assert.IsTrue(before.IsReady && _popover.IsCommandReady(GraffitiCommand),
                $"{LogPrefix} ★ R6c 음성 대조 실패 — 등급 없음에서 [낙서하기]가 가능하지 않습니다(사유 «{before.Reason}», 타일 {_popover.IsCommandReady(GraffitiCommand)}). " +
                "이 세계에서 낙서가 원래 불가면 R6a · R6b · R6d · R7a의 «회색 · 0»은 전부 무효입니다. " +
                "사유가 «낙서할 빈 자리가 없어요»면 발판 환경 탓이고, 7-b 사유면 가드가 등급 없음에서도 무는 것입니다.");

            _paint.Reset();
            _popover.FeedClickForTests(_popover.CommandScreenRect(GraffitiCommand).center);
            yield return ObserveGraffiti(GraffitiObserveSeconds());
            GraffitiTally t = _paint.Snapshot();
            StickmanStateId state = _agent.Blackboard.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} R6c — 등급 없음 클릭: [{t}] · 상태 {state} · 명령창 열림={_popover.IsOpen}");

            Assert.AreEqual(0, t.UnknownPhase, $"{LogPrefix} R6c — 관측기가 모르는 오버레이 단계가 나왔습니다.");
            Assert.AreEqual(1, t.Started, $"{LogPrefix} ★ R6c 음성 대조 실패 — 가능한 타일을 눌렀는데 낙서가 시작되지 않았습니다([{t}]). 관측기가 죽었을 수 있습니다.");
            Assert.AreEqual(1, t.GraffitiTransitions, $"{LogPrefix} ★ R6c 음성 대조 실패 — 낙서 상태 전이가 {t.GraffitiTransitions}회입니다(기대 1).");
            Assert.AreEqual(0, t.Cancelled,
                $"{LogPrefix} ★ R6c — 등급 없음에서 낙서가 취소됐습니다([{t}]). 7-b 가드가 등급 없음에서도 물거나 빈 자리에 발판이 겹친 것이고, " +
                "그러면 R7a의 «취소됐다»가 «원래 취소되는 세계»와 구별되지 않습니다.");
            Assert.AreEqual(StickmanStateId.Graffiti, state, $"{LogPrefix} ★ R6c — 클릭 뒤 상태가 {state}입니다([{t}]).");
            Assert.IsTrue(_popover.IsOpen, $"{LogPrefix} R6c — 낙서가 시작됐는데 명령창이 닫혔습니다(2026-09-02 «메뉴가 유지되어야함» 회귀).");

            // 3~5초짜리 상태를 끌고 가지 않는다 — 뒤따르는 단언 없이 여기서 정리한다(TearDown도 같은 일을 한다).
            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
            Debug.Log($"{LogPrefix} R6c 확인 — 등급 없음에서 [낙서하기]는 가능했고, 누르자 낙서가 1회 시작됐습니다(측정기 생존).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R6d_가드위치_등급1에서_숨김이면_숨김_사유가_먼저이고_락이_잡혀_있으면_7b_사유가_락_사유보다_먼저다()
        {
            yield return LoadScene();
            yield return PrepareGraffitiHarness("R6d");
            yield return EnterTierOne("R6d");
            yield return OpenCommandPopoverFromFan("R6d", expectGrant: true);

            // ---------- (i) 숨김 + 등급 1: 숨김 사유가 나와야 한다(숨김은 7-b 사유 문구를 채워도 풀리지 않는 지속 사유다) ----------
            _agent.SetUserHidden(true, "R6d — 등급 1에서 명령창을 연 채 캐릭터를 숨긴다");
            _userHiddenByTest = true;
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} R6d 전제 — 숨김이 IsSuspended로 이어지지 않았습니다.");
            Assert.IsTrue(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive, $"{LogPrefix} R6d 전제 — 7-b 가드 조건(옛 창구 ∨ 허가)이 거짓입니다(가드 순서를 재지 못합니다).");
            CommandAvailability hidden = _graffiti.GetAvailability();
            Debug.Log($"{LogPrefix} R6d(i) 숨김 + 등급 1 — 가능={hidden.IsReady} 사유=«{hidden.Reason}»");
            Assert.IsFalse(hidden.IsReady, $"{LogPrefix} ★ R6d(i) — 숨김 + 등급 1인데 [낙서하기]가 가능입니다.");
            Assert.AreEqual(HiddenCharacterCommandGate.HiddenReason, hidden.Reason,
                $"{LogPrefix} ★ R6d(i) — 숨김 + 등급 1에서 사유가 숨김 사유가 아닙니다(«{hidden.Reason}»). 7-b 가드가 숨김 게이트보다 앞에 있습니다 — " +
                "숨김은 7-b 사유 문구를 다 채워도 풀리지 않는 지속 사유라서 먼저 보여야 합니다.");

            _agent.SetUserHidden(false, "R6d — 숨김 해제");
            _userHiddenByTest = false;
            yield return Wait(SettleSeconds);
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} R6d 전제 — 숨김이 풀리지 않았습니다.");

            // ---------- (ii) 등급 1 + 락 점유: 7-b 사유가 락 사유보다 먼저여야 한다 ----------
            Assert.IsTrue((bool)PanelRetreatField.GetValue(_agent), $"{LogPrefix} R6d(ii) 전제 — 축 3이 꺼졌습니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive, $"{LogPrefix} R6d(ii) 전제 — 테스트가 잡기 전에 이미 락이 잡혀 있습니다.");
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Archery, this), $"{LogPrefix} R6d(ii) 전제 — 테스트가 락을 잡지 못했습니다.");
            _testHoldsLock = true;
            CommandAvailability locked = _graffiti.GetAvailability();
            SpectacleEventLock.Release(this);
            _testHoldsLock = false;
            string busy = StickMateDisplayNames.BusyText(SpectacleEventKind.Archery);
            Debug.Log($"{LogPrefix} R6d(ii) 등급 1 + 락 — 가능={locked.IsReady} 사유=«{locked.Reason}» (락 사유 문형 «{busy}»)");
            Assert.IsFalse(locked.IsReady, $"{LogPrefix} ★ R6d(ii) — 락이 잡혀 있는데 가능입니다.");
            Assert.AreEqual(ExpectedUnsummonedReason, locked.Reason,
                $"{LogPrefix} ★ R6d(ii) — 등급 1 + 락에서 사유가 7-b 사유가 아닙니다(«{locked.Reason}»). 7-b 가드가 락 검사보다 뒤에 있습니다 — " +
                "«지금 활쏘기 중이에요»가 먼저 떠 기다리면 될 것처럼 보였다가 아니게 됩니다.");

            // ---------- 대조: 등급 없음 · 무허가에서 같은 락이면 락 사유가 나온다(락 판정이 살아 있고 위 비교가 가를 수 있다) ----------
            _menu.ForceCloseAll("R6d 대조 — 연 것을 다 닫는다");
            SetPanelRetreat(false);
            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsFalse(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive, $"{LogPrefix} R6d 대조 전제 — 등급 없음 · 무허가가 아닙니다.");
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Archery, this), $"{LogPrefix} R6d 대조 전제 — 테스트가 락을 잡지 못했습니다.");
            _testHoldsLock = true;
            CommandAvailability control = _graffiti.GetAvailability();
            SpectacleEventLock.Release(this);
            _testHoldsLock = false;
            Debug.Log($"{LogPrefix} R6d 대조 등급 없음 + 락 — 가능={control.IsReady} 사유=«{control.Reason}»");
            Assert.AreEqual(busy, control.Reason,
                $"{LogPrefix} R6d 대조 실패 — 등급 없음 + 락에서 락 사유가 나오지 않습니다(«{control.Reason}»). 위 (ii) 비교가 아무것도 가르지 못합니다.");
            Debug.Log($"{LogPrefix} R6d 확인 — 숨김이면 숨김 사유, 등급 1 + 락이면 7-b 사유, 등급 없음 + 락이면 락 사유.");
        }

        /// <summary>낙서를 시작할 자격이 있는 «발판 위 정지» 상태인가 — 명령 조건(Idle/Walk · 락 비움)에
        /// <b>실제 접지</b>를 더한다. 층 2 케이스는 연출이 몇 초 이어져야 성립하는데, 접지를 보지 않고 시작하면
        /// 몸이 정착 중일 때 시작해 0.1초 안에 <c>Fall</c>로 밀려난다(첫 판 R7b 실측: 시작 0.075초 뒤 이탈).</summary>
        private bool StandsStillOnGround() =>
            CharacterCanTakeCommand() && _agent.Blackboard.SenseGround().Grounded;

        /// <summary>«발판 위 정지»가 <see cref="SettleSeconds"/> 동안 <b>끊기지 않고</b> 이어질 때까지 기다린다.</summary>
        private IEnumerator WaitUntilStandingStill(string what)
        {
            float stableSince = -1f;
            float deadline = Time.realtimeSinceStartup + CommandReadyBudgetSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (StandsStillOnGround()) { if (stableSince < 0f) stableSince = Time.realtimeSinceStartup; }
                else stableSince = -1f;
                if (stableSince >= 0f && Time.realtimeSinceStartup - stableSince >= SettleSeconds) yield break;
                yield return null;
            }
            Assert.IsTrue(StandsStillOnGround(),
                $"{LogPrefix} {what} 전제 — {CommandReadyBudgetSeconds:F0}초 안에 «발판 위 정지»가 {SettleSeconds:F2}초 이어지지 않았습니다" +
                $"(접지={_agent.Blackboard.SenseGround().Grounded}, 상태={_agent.Blackboard.Machine.CurrentStateId}, 락={SpectacleEventLock.IsActive}).");
        }

        /// <summary>층 2 케이스의 시작 시도 횟수. 물리 이탈은 <b>가드 판정이 아니라 전제 붕괴</b>라서 재시도한다.</summary>
        private const int GraffitiStartAttempts = 3;

        /// <summary>
        /// 발판 위 정지 상태에서 ⌃⌥⌘G와 같은 경로로 낙서를 시작한다. 시작 직후 몸이 밀려나 비정상 이탈하면
        /// <b>측정 전 전제가 무너진 것</b>이므로(가드와 무관하다) 정리하고 다시 시도한다.
        /// </summary>
        private IEnumerator StartGraffitiOnStableGround(string what)
        {
            for (int attempt = 1; attempt <= GraffitiStartAttempts; attempt++)
            {
                yield return WaitUntilStandingStill($"{what}(시도 {attempt})");
                CommandAvailability before = _graffiti.GetAvailability();
                Assert.IsTrue(before.IsReady,
                    $"{LogPrefix} ★ {what} 전제 — 등급 없음에서 낙서가 불가합니다(사유 «{before.Reason}»). 시작할 수 없으면 «도중에 걷힌다»를 잴 수 없습니다.");
                _paint.Reset();
                Assert.IsTrue(_graffiti.ForceTriggerNow($"{what} — 등급 없음에서 사용자가 직접 시킨 낙서(시도 {attempt})"),
                    $"{LogPrefix} ★ {what} 전제 — 판정이 «가능»인데 ForceTriggerNow가 발동하지 않았습니다.");
                yield return null;
                Assert.AreEqual(1, _paint.Started, $"{LogPrefix} {what} 전제 — 낙서 Started가 {_paint.Started}회입니다(기대 1) [{_paint}].");

                if (_paint.Exits == 0 && _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Graffiti)
                {
                    Debug.Log($"{LogPrefix} {what} — 시도 {attempt}에서 낙서가 발판 위에서 시작됐습니다 [{_paint}].");
                    yield break;
                }

                Debug.LogWarning($"{LogPrefix} {what} — 시도 {attempt}: 시작 직후 이탈(→ {_paint.LastExitTo}, 비정상={_paint.LastExitAbnormal}) [{_paint}]. " +
                    "가드가 아니라 물리 전제가 무너진 것이므로 다시 시도합니다.");
                if (_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Graffiti)
                    _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                if (SpectacleEventLock.IsActive) SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
                yield return Wait(SettleSeconds);
            }
            Assert.Fail($"{LogPrefix} ★ {what} 전제 붕괴 — {GraffitiStartAttempts}회 모두 시작 직후 몸이 발판에서 밀려났습니다. " +
                "이 칸은 층 2 가드를 재지 못했습니다(가드 판정이 아닙니다).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R7a_층2_등급없음에서_시작한_낙서는_등급1이_주입되면_벽시계_예산_안에_취소된다()
        {
            yield return LoadScene();
            yield return PrepareGraffitiHarness("R7a");
            Assert.IsFalse(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive || (bool)PanelRetreatField.GetValue(_agent),
                $"{LogPrefix} R7a 전제 — 등급 없음 · 무허가가 아닙니다.");

            // ---------- 사용자가 직접 시킨다(⌃⌥⌘G와 같은 경로) ----------
            yield return StartGraffitiOnStableGround("R7a");

            // ---------- 발표가 시작된다(등급 1 주입). 발판 목록은 건드리지 않는다 ----------
            float budget = SettleSeconds + ObserveSlackSeconds;
            float minHold = Mathf.Max(0f, _config.graffitiHoldDurationMin);
            Assert.Less(budget, minHold,
                $"{LogPrefix} R7a 측정 무효 — 관측 예산({budget:F2}초)이 낙서 최소 유지 시간({minHold:F2}초)보다 짧지 않습니다. " +
                "그러면 «가드가 취소했다»와 «수명이 다 돼 끝났다»를 가를 수 없습니다.");

            float injectedAt = Time.realtimeSinceStartup;
            SetPanelRetreat(true);
            yield return WaitUntilOrTimeout(() => _paint.Cancelled > 0, budget);
            GraffitiTally t = _paint.Snapshot();
            float elapsed = t.CancelledAt >= 0f ? t.CancelledAt - injectedAt : -1f;
            StickmanStateId state = _agent.Blackboard.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} R7a — 등급 1 주입 뒤 {(elapsed >= 0f ? elapsed.ToString("F3") + "초" : "취소 없음")}(예산 {budget:F2}초 · 최소 유지 {minHold:F2}초): " +
                $"[{t}] · 상태 {state} · 락 {SpectacleEventLock.IsActive}");

            Assert.AreEqual(0, t.UnknownPhase, $"{LogPrefix} R7a — 관측기가 모르는 오버레이 단계가 나왔습니다([{t}]).");
            Assert.AreEqual(1, t.Cancelled,
                $"{LogPrefix} ★ R7a — 등급 1이 켜졌는데 낙서가 벽시계 {budget:F2}초 안에 취소되지 않았습니다([{t}]). " +
                "명령으로 시작한 낙서가 발표·회의 화면 위에 최대 " + $"{_config.graffitiHoldDurationMax:F0}초 남습니다(7-b 층 2 · 원칙 2).");
            Assert.AreEqual(0, t.Completed,
                $"{LogPrefix} ★ R7a — 취소가 아니라 «완료»로 끝났습니다([{t}]). 수명이 다 돼 끝난 것이면 이 칸은 가드를 재지 못했습니다.");
            Assert.IsFalse(t.LastExitAbnormal,
                $"{LogPrefix} R7a 전제 붕괴 — 낙서가 비정상 이탈(→ {t.LastExitTo})로 끝났습니다([{t}]). 몸이 발판에서 밀려난 것이고 가드 판정이 아닙니다.");
            Assert.AreNotEqual(StickmanStateId.Graffiti, state, $"{LogPrefix} ★ R7a — 취소 이벤트는 났는데 상태가 아직 낙서입니다([{t}]).");
            Assert.Less(elapsed, minHold,
                $"{LogPrefix} ★ R7a — 취소가 주입 뒤 {elapsed:F3}초에 났습니다(최소 유지 {minHold:F2}초 이상) — 수명 만료와 구별되지 않습니다.");
            Debug.Log($"{LogPrefix} R7a 확인 — 명령으로 시작한 낙서가 등급 1 주입 뒤 {elapsed:F3}초에 기존 취소 경로로 걷혔습니다(층 2).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator R7b_음성대조_층2_등급을_주입하지_않으면_같은_예산_동안_낙서가_살아_있다()
        {
            yield return LoadScene();
            yield return PrepareGraffitiHarness("R7b");
            Assert.IsFalse(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive || (bool)PanelRetreatField.GetValue(_agent),
                $"{LogPrefix} R7b 전제 — 등급 없음 · 무허가가 아닙니다.");

            // ★ R7a와 같은 예산으로는 «아무 일도 안 했으니 당연히 안 걷힌다»만 보인다. 그래서 여기서는 <b>최소 유지 시간 직전까지</b>
            //   관측한다 — 그 구간 내내 살아 있으면, R7a의 취소는 주입이 만든 것이라는 귀속이 선다.
            float budget = Mathf.Max(0f, _config.graffitiHoldDurationMin) - ObserveSlackSeconds;
            Assert.Greater(budget, SettleSeconds + ObserveSlackSeconds,
                $"{LogPrefix} R7b 측정 무효 — 관측 예산({budget:F2}초)이 R7a 예산보다 크지 않습니다(낙서 최소 유지 {_config.graffitiHoldDurationMin:F2}초).");

            // ★ 물리 이탈(→ Fall)은 가드 판정이 아니라 전제 붕괴다. 첫 판이 그 이탈을 «음성 대조 실패»로 보고했는데,
            //   실측 원인은 몸이 정착 중에 시작된 것이었다(시작 0.075초 뒤 Fall). 그래서 접지 게이트를 두고, 그래도
            //   이탈하면 그 시도는 버리고 다시 잰다 — 버린 사실은 위 경고 로그에 남는다.
            GraffitiTally t = null;
            for (int attempt = 1; attempt <= GraffitiStartAttempts; attempt++)
            {
                yield return StartGraffitiOnStableGround($"R7b(관측 시도 {attempt})");
                yield return ObserveGraffiti(budget);
                t = _paint.Snapshot();
                if (!t.LastExitAbnormal) break;
                Debug.LogWarning($"{LogPrefix} R7b — 관측 시도 {attempt}에서 몸이 비정상 이탈했습니다(→ {t.LastExitTo}) [{t}]. 전제 붕괴이므로 다시 잽니다.");
                if (_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Graffiti)
                    _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                if (SpectacleEventLock.IsActive) SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
                yield return Wait(SettleSeconds);
            }
            Assert.IsNotNull(t, $"{LogPrefix} R7b — 관측을 한 번도 하지 못했습니다.");
            Assert.IsFalse(t.LastExitAbnormal,
                $"{LogPrefix} ★ R7b 전제 붕괴 — {GraffitiStartAttempts}회 모두 관측 중 몸이 발판에서 밀려났습니다([{t}]). " +
                "이 칸은 층 2 음성 대조를 재지 못했습니다(가드 판정이 아닙니다).");
            StickmanStateId state = _agent.Blackboard.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} R7b — 주입 없이 벽시계 {budget:F2}초: [{t}] · 상태 {state} · 옛 창구={_agent.ArePanelsSuppressed} · 허가={_agent.IsUserSummonGrantActive}");

            Assert.AreEqual(0, t.UnknownPhase, $"{LogPrefix} R7b — 관측기가 모르는 오버레이 단계가 나왔습니다([{t}]).");
            Assert.AreEqual(0, t.Cancelled,
                $"{LogPrefix} ★ R7b 음성 대조 실패 — 등급을 주입하지 않았는데 낙서가 취소됐습니다([{t}], 이탈 → {t.LastExitTo} 비정상={t.LastExitAbnormal}). " +
                "발판이 빈 자리에 겹쳤거나 몸이 밀려난 것이고, 그렇다면 R7a의 취소를 등급 주입에 귀속시킬 수 없습니다.");
            Assert.AreEqual(0, t.Completed, $"{LogPrefix} R7b 측정 무효 — 관측 예산 안에 낙서가 수명을 다 채웠습니다([{t}]).");
            Assert.AreEqual(StickmanStateId.Graffiti, state,
                $"{LogPrefix} ★ R7b 음성 대조 실패 — 주입 없이 상태가 {state}로 바뀌었습니다([{t}]).");
            Assert.IsFalse(_agent.ArePanelsSuppressed || _agent.IsUserSummonGrantActive,
                $"{LogPrefix} R7b — 관측 중 억제 창구가 참이 됐습니다(주입 없는 칸이 아니었습니다).");

            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
            Debug.Log($"{LogPrefix} R7b 확인 — 주입이 없으면 같은 연출이 {budget:F2}초 동안 취소 없이 살아 있었습니다(R7a 취소의 귀속이 섭니다).");
        }
    }
}
