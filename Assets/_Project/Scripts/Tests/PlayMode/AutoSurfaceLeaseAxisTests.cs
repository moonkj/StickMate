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
    /// </list>
    ///
    /// <para>★ <b>수정 전 박제</b>: 이 파일은 새 정책 함수를 참조하지 않는다 — 수정 전 트리(HEAD)에서도 컴파일되어
    /// R1·R3·R4가 빨강임을 먼저 박제하기 위해서다(ROADMAP N-20 「R1은 수정 전 빨강 박제를 같이 낸다」).</para>
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
    }
}
