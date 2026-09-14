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
    /// ★★ <b>등급 1(게임이 아닌 전체화면 앱) 체류 중 마우스 입구가 실제로 열리는가</b> — 2026-09-14 P1 회귀 잠금.
    /// 설계 정본: <c>docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md</c> §16-2(E-1·E-2) · §16-2b(원칙 2 경계 표 B1~B11) · §16-4 T-2.
    ///
    /// ============================================================================
    /// 이 테스트가 도는 세계 — <b>대기 톱니 우회를 끈 프로덕션 기본</b> (TEAM.md 「스위트 전역 격리가 프로덕션 기본을 바꾼다」 규칙 2·3)
    /// ============================================================================
    /// <c>GlobalPlayModeTestIsolation</c>이 어셈블리 전체에서 대기 톱니 게이트를 우회한다(그 파일의 「대기 톱니 게이트 우회」 절). 그 세계에서는
    /// 캐릭터가 보이는 등급 1에도 톱니가 서 있어, <b>프로덕션에서는 입구가 0인 상태</b>를 톱니 경로로 초록 통과시켰다
    /// (<c>FullscreenPanelRetreatTests</c>의 옛 두 건). 이 픽스처는 <b>매 테스트 SetUp에서 우회를 끄고</b>,
    /// TearDown에서 픽스처 시작 값으로 되돌린다. 되돌림 누락은 다음 테스트의 SetUp과 <c>[OneTimeTearDown]</c>이 드러낸다.
    /// 각 테스트는 전제 단언으로 <c>IsStandbyGateBypassedForTests == false</c>를 먼저 확인한다.
    ///
    /// ============================================================================
    /// 무엇이 결함이었나 (E-1 이전)
    /// ============================================================================
    /// 우클릭 게이트 넷째 항이 <c>ArePanelsSuppressed</c>(= s ∨ (r ∧ ¬g))였다. 허가(g)가 없는 등급 1에서 참이라
    /// <b>허가 발급(<c>AppControlDirector</c> 「게이트 6」)에 닿기 전에</b> 조용히 <c>return</c>했다. 톱니는 평상시 숨김이다.
    /// ⇒ macOS 마우스 경로 0, Windows는 트레이로 설정창만. E-1은 넷째 항을 <c>IsUserSummonBlocked</c>
    /// (= 「허가를 받아도 억제되는가」, 결과적으로 s)로 바꿨다.
    ///
    /// ============================================================================
    /// 케이스 ↔ §16-2b
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>B3·B4</b> — 무허가 등급 1에서 캐릭터 우클릭 → 부채꼴 → [캐릭터] → 정보창 → [설정] → 설정창. <b>E-1 전에는 첫 홉에서 빨강</b>.</item>
    ///   <item><b>B2</b> — 평상시에 연 부채꼴은 등급 1 진입 순간 회수된다. E-1 전후 <b>똑같이 초록</b>이어야 한다(닫기 소비자 불변).</item>
    ///   <item><b>B5</b> — 설정창 닫힘 자동 복귀 진입점은 허가를 내지 않는다 / 사용자 열기는 낸다(E-2).</item>
    ///   <item><b>B7</b> — 등급 2(전체화면 게임)에서는 첫 홉이 막힌다. E-1 전후 <b>똑같이 초록</b>이어야 한다(원칙 2 경계).</item>
    /// </list>
    ///
    /// <para>★ <b>넷째 홉을 칩 클릭이 아니라 칩 핸들러와 같은 호출로 재는 이유</b>: 배치 PlayMode 게임 뷰(640×480)는
    /// 정보창 헤더의 [설정] 칩이 <b>접히는 폭</b>이다(같은 문서 제1부 §5-3, P2 — E-3 소관). 칩 클릭으로 재면 이 테스트가
    /// E-1과 무관한 이유로 빨개진다. 칩 핸들러(<c>CharacterInfoWindow.OpenSettings</c>)가 하는 일은
    /// <c>SettingsWindow.Open(source)</c> 한 줄이므로 그 호출을 그대로 부른다. 칩 사각형 폭은 로그로만 남긴다.</para>
    ///
    /// <para><b>입력은 프로덕션 본체를 태운다</b> — <c>RightClickFanRuntimeTests</c>와 같은 하네스(가짜 전역 버튼 서비스를
    /// <c>_buttonService</c>에 꽂고 <c>TickRightClickFan()</c>을 부른다). 커서는 매 조회마다 몸 중심에서 유도한다.
    /// <b>시간은 전부 벽시계(초)</b>이고 예산은 프로덕션 상수식이다(CLAUDE.md).</para>
    /// </summary>
    public sealed class PanelsOnlyTierMouseEntryTests
    {
        private const string LogPrefix = "[등급1입구-TEST]";

        /// <summary>씬·폴링 소비자가 한 바퀴 도는 여유(초).</summary>
        private const float SettleSeconds = 0.35f;

        /// <summary>연출 예산 위에 얹는 관측 슬랙(초).</summary>
        private const float ObserveSlackSeconds = 0.25f;

        /// <summary>에이전트 자체 폴링이 주입한 축을 덮어쓰지 못하게 하는 값(초) — 에디터의 널 서비스는 등급 None이다.
        /// (<c>FullscreenPanelRetreatTests.ObservePollInterval</c>과 같은 사정.)</summary>
        private const float ObservePollInterval = 9999f;

        /// <summary>가짜 전역 포인터 — 세운 값을 그대로 돌려준다(조회는 항상 성공).</summary>
        private sealed class ScriptedButtons : IGlobalPointerButtonService
        {
            public bool Primary;
            public bool Secondary;
            public bool TryGetPrimaryButtonPressed(out bool pressed) { pressed = Primary; return true; }
            public bool TryGetSecondaryButtonPressed(out bool pressed) { pressed = Secondary; return true; }
        }

        private StickmanAgent _agent;
        private AppControlDirector _control;
        private GearRadialMenuWidget _menu;
        private InfoGearIconWidget _gear;
        private CharacterInfoWindow _window;
        private SettingsWindow _settings;
        private StickConfig _config;

        private readonly ScriptedButtons _buttons = new ScriptedButtons();

        private float _savedPollInterval;
        private bool _configHijacked;
        private object _savedButtonService;
        private CursorPositionQuery _savedCursor;
        private Vector2 _savedOverlayOrigin;
        private bool _hijacked;

        private bool _bypassAtFixtureStart;
        private bool _fixtureStartCaptured;

        private static readonly FieldInfo ButtonServiceField =
            typeof(AppControlDirector).GetField("_buttonService", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo TickRightClickFanMethod =
            typeof(AppControlDirector).GetMethod("TickRightClickFan", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>축 3 — 등급 1의 원시 사실(주입 지점이 없어 리플렉션 — 이 어셈블리의 기존 관례).</summary>
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

            // ★ 되돌릴 값을 <b>상수 true로 가정하지 않는다</b> — 전역 우회는 뒤 라운드(전역 격리 정합)에서 꺼질 예정이다.
            _bypassAtFixtureStart = InfoGearIconWidget.IsStandbyGateBypassedForTests;
            _fixtureStartCaptured = true;
            Debug.Log($"{LogPrefix} 픽스처 시작 시 대기 톱니 우회={_bypassAtFixtureStart}. 각 테스트는 우회를 끄고 시작해 " +
                "끝에서 이 값으로 되돌립니다(TEAM.md 전역 격리 규칙 2·3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        /// <summary>
        /// ★ 복원 누락 자기 검증(마지막 테스트 몫). 앞 테스트의 누락은 다음 테스트의 <see cref="VerifyPreviousRestoreThenTurnBypassOff"/>가 잡는다.
        /// <para>관측을 <b>되돌리기 전에</b> 한다 — 되돌린 뒤에 재면 누락이 있어도 항상 같다. 단언이 실패해도 결과 xml의
        /// <c>failed=</c>에는 안 들어가고 스위트 <c>site="TearDown"</c>으로만 남는다(TEAM.md 「픽스처 끝에서 난 실패」) —
        /// 그래서 단언 뒤에 성공 로그 한 줄을 남긴다(같은 절 규칙 4).</para>
        /// </summary>
        [OneTimeTearDown]
        public void RestoreBypassForTheRestOfTheAssembly()
        {
            bool leftAtEnd = InfoGearIconWidget.IsStandbyGateBypassedForTests;
            InfoGearIconWidget.SetStandbyGateBypassedForTests(_bypassAtFixtureStart);
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();

            Assert.AreEqual(_bypassAtFixtureStart, leftAtEnd,
                $"{LogPrefix} ★ 마지막 테스트가 대기 톱니 우회를 되돌리지 않았습니다(남은 값={leftAtEnd}, 시작 값={_bypassAtFixtureStart}). " +
                "지금은 여기서 되돌렸지만, UnityTearDown의 복원 줄이 사라졌다는 뜻입니다.");
            Debug.Log($"{LogPrefix} 픽스처 정리 단언 통과 — 대기 톱니 우회가 시작 값({_bypassAtFixtureStart})으로 남아 있었습니다.");
        }

        [SetUp]
        public void VerifyPreviousRestoreThenTurnBypassOff()
        {
            Assert.IsTrue(_fixtureStartCaptured, $"{LogPrefix} 픽스처 시작 값이 기록되지 않았습니다 — OneTimeSetUp이 돌지 않았습니다.");
            Assert.AreEqual(_bypassAtFixtureStart, InfoGearIconWidget.IsStandbyGateBypassedForTests,
                $"{LogPrefix} ★ 앞 테스트가 대기 톱니 우회를 되돌리지 않았습니다(복원 누락). 이대로면 뒤 픽스처가 " +
                "우회가 꺼진 세계를 물려받아 다른 이유로 빨개집니다.");

            // ★ 여기서부터 이 테스트는 프로덕션 기본 세계다 — 캐릭터가 보이는 동안 톱니는 없다.
            InfoGearIconWidget.SetStandbyGateBypassedForTests(false);
        }

        [UnityTearDown]
        public IEnumerator RestoreSceneAndBypass()
        {
            // 표면 → 축 → 하네스 → 설정 에셋 → 우회 순. 설정창을 먼저 닫아야 자동 복귀가 정보창을 되살려도 아래에서 함께 닫힌다.
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
            }

            // ★ 가로챈 적이 없으면 되돌리지도 않는다 — 도중 실패 시 «저장된 값»이 기본값이라 그것이 오염이 된다.
            if (_hijacked)
            {
                if (_control != null && ButtonServiceField != null) ButtonServiceField.SetValue(_control, _savedButtonService);
                if (_agent != null && _agent.Blackboard != null) _agent.Blackboard.CursorProvider = _savedCursor;
                ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOverlayOrigin;
                _hijacked = false;
            }
            // StickConfig는 <b>배포 에셋</b>이라 반드시 원복한다.
            if (_configHijacked && _config != null) _config.fullscreenPollInterval = _savedPollInterval;
            _configHijacked = false;

            InfoGearIconWidget.SetStandbyGateBypassedForTests(_bypassAtFixtureStart);

            _agent = null;
            _control = null;
            _menu = null;
            _gear = null;
            _window = null;
            _settings = null;
            _config = null;
            _savedButtonService = null;
            yield return null;
        }

        private IEnumerator LoadScene()
        {
            Assert.IsNotNull(ButtonServiceField, $"{LogPrefix} AppControlDirector._buttonService를 찾지 못했습니다 — 주입 경로가 죽었습니다.");
            Assert.IsNotNull(TickRightClickFanMethod, $"{LogPrefix} AppControlDirector.TickRightClickFan()을 찾지 못했습니다.");
            Assert.IsNotNull(PanelRetreatField, $"{LogPrefix} StickmanAgent._fullscreenPanelRetreat를 찾지 못했습니다 — 등급 1 주입이 아무 일도 안 합니다.");
            Assert.IsNotNull(FullscreenAxisField, $"{LogPrefix} StickmanAgent._fullscreenAutoHide를 찾지 못했습니다 — 등급 2 주입이 아무 일도 안 합니다.");
            Assert.IsNotNull(ApplyDecisionMethod, $"{LogPrefix} StickmanAgent.ApplySuspendDecision()을 찾지 못했습니다.");

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            _control = Object.FindFirstObjectByType<AppControlDirector>(FindObjectsInactive.Include);
            Assert.IsNotNull(_control, $"{LogPrefix} 씬에 AppControlDirector가 없습니다.");
            _menu = _agent.GetComponent<GearRadialMenuWidget>();
            Assert.IsNotNull(_menu, $"{LogPrefix} 캐릭터에 GearRadialMenuWidget이 없습니다.");
            _gear = Object.FindFirstObjectByType<InfoGearIconWidget>(FindObjectsInactive.Include);
            Assert.IsNotNull(_gear, $"{LogPrefix} 씬에 InfoGearIconWidget이 없습니다.");
            _window = _agent.GetComponent<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 캐릭터에 CharacterInfoWindow가 없습니다.");
            _settings = _agent.GetComponent<SettingsWindow>();
            Assert.IsNotNull(_settings, $"{LogPrefix} 캐릭터에 SettingsWindow가 없습니다 — 등급 1을 끄는 유일한 스위치가 그 창 안에 있습니다.");

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
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} 새 씬인데 부채꼴이 이미 떠 있습니다.");

            // 첫 표본은 «기록만» 하는 프레임이다(앱 시작 순간 눌려 있던 버튼을 명령으로 오인하지 않는 설계).
            Tick();
            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
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

        // ==================== 커서 — 몸 중심에서 매번 유도(좌표 하드코딩 금지) ====================

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

        private void AssertCursorInside()
        {
            Assert.IsTrue(AnyColliderContains(CursorWorldNow()),
                $"{LogPrefix} 전제 불성립 — 몸 중심 좌표가 어떤 캐릭터 콜라이더에도 안 걸립니다. 아래 «열린다/안 열린다»가 다른 것을 재게 됩니다.");
        }

        // ==================== B3 · B4 — 무허가 등급 1에서 네 홉 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator B3_B4_등급1_무허가에서_캐릭터_우클릭으로_부채꼴_정보창_설정창까지_네_홉이_열린다()
        {
            yield return LoadScene();

            // ---------- ① 아무것도 열려 있지 않은 상태에서 등급 1로 진입 ----------
            SetPanelRetreat(true);
            yield return Wait(SettleSeconds);

            // ---------- ② 전제 — 프로덕션 기본의 등급 1인가(톱니 없음 · 허가 없음) ----------
            Assert.IsFalse(InfoGearIconWidget.IsStandbyGateBypassedForTests,
                $"{LogPrefix} 전제 불성립 — 대기 톱니 우회가 켜져 있습니다.");
            Assert.IsFalse(_gear.IsIconVisible,
                $"{LogPrefix} 전제 불성립 — 캐릭터가 보이는 등급 1인데 톱니가 보입니다. 이 테스트는 «톱니가 없는» 프로덕션 " +
                $"기본에서 입구를 재야 합니다(사유: {_gear.StandbyGearReason}).");
            Assert.IsTrue(_agent.ArePanelsSuppressed, $"{LogPrefix} 등급 1을 세웠는데 표면 억제가 켜지지 않았습니다 — 전제가 없습니다.");
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 등급 1인데 캐릭터가 숨었습니다(2026-08-31 신고 회귀).");
            Assert.IsFalse(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} 등급 1 진입 직후인데 허가가 살아 있습니다 — 아래 단언이 «원래 허가가 있어서 통과»가 됩니다.");
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} 준비 단계에서 부채꼴이 이미 떠 있습니다.");
            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 준비 단계에서 정보창이 이미 열려 있습니다.");
            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} 준비 단계에서 설정창이 이미 열려 있습니다.");

            // ---------- ③ 첫 홉 — 캐릭터 우클릭(프로덕션 폴링 본체) ----------
            AssertCursorInside();
            RightDown();

            // 동기 관측 — 게이트를 통과했다면 이 호출 안에서 허가와 펼침이 이미 일어났다.
            Assert.IsTrue(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} ★ 캐릭터를 우클릭했는데 허가가 나지 않았습니다 — 게이트가 허가 발급보다 먼저 막았습니다. " +
                "이것이 P1 결함 그 자체입니다(게임이 아닌 전체화면 앱에서 마우스 입구 0, §15-1).");
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} ★ 허가는 났는데 부채꼴이 펴지지 않았습니다.");
            Assert.AreEqual(GearMenuAnchorSource.Character, _menu.AnchorSource,
                $"{LogPrefix} 부채꼴이 열리긴 했는데 앵커가 «캐릭터»가 아닙니다 — 다른 경로가 열었을 수 있습니다.");

            // ---------- ④ 머문다 — 펼침 예산 + 임대 수명의 3배 ----------
            yield return Wait(GearRadialMenuWidget.ExpandTotalSeconds + UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            RightUp();
            Assert.IsTrue(_menu.IsVisible,
                $"{LogPrefix} 부채꼴이 임대 수명({UserSurfaceSummonPolicy.LeaseSeconds:F2}초)의 3배 안에 사라졌습니다 — 자기 갱신이 끊겼습니다.");
            Assert.IsTrue(_agent.IsUserSummonGrantActive, $"{LogPrefix} 부채꼴이 떠 있는데 허가가 만료됐습니다.");
            Assert.IsFalse(_agent.ArePanelsSuppressed, $"{LogPrefix} 허가가 살아 있는데 표면 억제가 켜져 있습니다.");

            // ---------- ⑤ 둘째 홉 — 부채꼴 [캐릭터] → 정보창 ----------
            _menu.Activate((int)GearMenuButton.Character);
            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsTrue(_window.IsOpen,
                $"{LogPrefix} ★ [{GearRadialMenuWidget.NameOf((int)GearMenuButton.Character)}]를 눌렀는데 정보창이 머물지 못했습니다 — 둘째 홉에서 끊깁니다.");
            Assert.IsTrue(_window.IsClickBlockerEnabled, $"{LogPrefix} 정보창은 떴는데 차단막이 꺼졌습니다 — 그리면서 클릭은 안 받는 창입니다.");

            // ---------- ⑥ 넷째 홉 — 정보창 [설정] 칩 핸들러와 같은 호출 ----------
            Debug.Log($"{LogPrefix} 참고 — 이 게임 뷰({Screen.width}×{Screen.height})에서 [설정] 칩 사각형 폭=" +
                $"{_window.SettingsChipScreenRect.width:F1}px. 좁은 폭에서는 칩이 접힌다(P2, E-3 소관) — 그래서 칩 핸들러와 같은 호출로 잰다.");
            _settings.Open("등급 1 우클릭 도달성 — 정보창 [설정] 칩 핸들러와 같은 호출");
            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 4f);
            Assert.IsTrue(_settings.IsOpen,
                $"{LogPrefix} ★ 설정창이 {UserSurfaceSummonPolicy.LeaseSeconds * 4f:F2}초를 버티지 못했습니다 — 등급 1을 끄는 유일한 스위치에 닿지 못합니다.");
            Assert.IsTrue(_settings.IsClickBlockerEnabled, $"{LogPrefix} 설정창은 떴는데 차단막이 꺼졌습니다.");
            Assert.IsTrue((bool)PanelRetreatField.GetValue(_agent),
                $"{LogPrefix} 축 3이 저절로 꺼졌습니다 — 위 단언들은 등급 1을 한 번도 마주치지 않은 채 통과한 것입니다.");

            Debug.Log($"{LogPrefix} B3·B4 도달 확인 — 톱니 없는 등급 1에서 우클릭 → 부채꼴 → 정보창 → 설정창 네 홉이 열렸고 " +
                $"설정창이 {UserSurfaceSummonPolicy.LeaseSeconds * 4f:F2}초를 버텼습니다.");

            // ---------- ⑦ 양성 대조 — 허가는 백지수표가 아니다 ----------
            _settings.Close("양성 대조 — 사용자가 [✕]를 눌렀다");
            yield return null;
            if (_window.IsOpen) _window.Close("양성 대조 — 사용자가 정보창도 닫았다");
            yield return Wait(SettleSeconds);
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} 양성 대조 준비 — 부채꼴이 아직 떠 있어 임대를 계속 갱신합니다.");
            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 양성 대조 준비 — 정보창이 아직 떠 있습니다.");

            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsFalse(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} ★ 양성 대조 실패 — 표면을 전부 닫았는데 허가가 {UserSurfaceSummonPolicy.LeaseSeconds * 3f:F2}초 뒤까지 살아 있습니다.");
            Assert.IsTrue(_agent.ArePanelsSuppressed,
                $"{LogPrefix} ★ 양성 대조 실패 — 허가가 만료됐는데 회수가 돌아오지 않았습니다. 위 도달 판정도 함께 폐기해야 합니다.");
            Debug.Log($"{LogPrefix} 양성 대조 통과 — 사용자가 닫자 임대가 만료되고 등급 1 회수가 돌아왔습니다.");
        }

        // ==================== B2 — 등급 1 진입 순간 회수는 E-1 전후 불변 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator B2_평상시에_우클릭으로_연_부채꼴은_등급1_진입_순간_회수되고_허가도_남지_않는다()
        {
            yield return LoadScene();

            // 양성 대조 — 등급 None에서 같은 하네스가 실제로 연다(아래 «회수됐다»가 «원래 안 열렸다»가 아니게).
            AssertCursorInside();
            RightDown();
            yield return Wait(GearRadialMenuWidget.ExpandTotalSeconds + ObserveSlackSeconds);
            RightUp();
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} 양성 대조 실패 — 평상시 캐릭터 우클릭이 부채꼴을 열지 못했습니다.");
            Assert.IsFalse(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} 평상시(등급 None)에 허가가 났습니다 — 허가는 등급 1이 이미 켜져 있을 때만 난다(§16-2b B1).");

            SetPanelRetreat(true);
            yield return Wait(SettleSeconds);

            Assert.IsTrue(_agent.ArePanelsSuppressed, $"{LogPrefix} 등급 1을 세웠는데 표면 억제가 켜지지 않았습니다.");
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 등급 1인데 캐릭터가 숨었습니다(2026-08-31 신고 회귀).");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} ★ 등급 1 진입 순간에 허가가 남아 있습니다 — 「진입 시 전부 회수」가 깨졌습니다.");
            Assert.IsFalse(_menu.IsVisible,
                $"{LogPrefix} ★ 등급 1 진입 순간 부채꼴이 회수되지 않았습니다 — 닫기 소비자가 새 열기 판정(IsUserSummonBlocked)을 " +
                "읽고 있지 않은지 보십시오(§16-2b B2 · 변이 M5). 발표 화면 위에 우리가 부르지 않은 메뉴가 남습니다(원칙 2).");

            Debug.Log($"{LogPrefix} B2 확인 — 평상시에 연 부채꼴이 등급 1 진입과 함께 걷혔고 허가는 없습니다.");
        }

        // ==================== B5 — 자동 복귀 진입점은 허가를 내지 않는다(E-2) ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator B5_자동_복귀_진입점은_등급1에서_허가를_내지_않고_사용자_열기는_낸다()
        {
            yield return LoadScene();

            SetPanelRetreat(true);
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.ArePanelsSuppressed, $"{LogPrefix} 전제 불성립 — 등급 1 억제가 켜지지 않았습니다.");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} 전제 불성립 — 무허가 등급 1이 아닙니다.");
            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 전제 불성립 — 정보창이 이미 열려 있습니다.");

            // ---------- 자동 복귀(설정창 닫힘 → 보던 정보창으로) 진입점 ----------
            _window.ReopenFromSheet("B5 — 설정창 닫힘 자동 복귀와 같은 호출");
            Assert.IsFalse(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} ★ 자동 복귀 진입점이 허가를 냈습니다 — 「우리가 스스로에게 발급하는 면제」는 원칙 2의 구멍입니다(§16-2b B5 · 변이 M4).");
            yield return Wait(SettleSeconds);
            Assert.IsFalse(_window.IsOpen,
                $"{LogPrefix} ★ 허가 없이 자동 복귀로 연 정보창이 등급 1에서 살아남았습니다 — 발표 화면 위에 사용자가 부르지 않은 창이 남습니다.");
            Assert.IsFalse(_window.IsClickBlockerEnabled, $"{LogPrefix} 정보창은 닫혔는데 차단막이 남았습니다(안 보이는데 클릭만 먹는다).");

            // ---------- 대조 — 같은 조건에서 사용자 열기(⌃⌥⌘I · 부채꼴 [캐릭터]가 부르는 진입점) ----------
            _window.Open("B4 대조 — 사용자가 정보창을 연다");
            Assert.IsTrue(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} ★ 사용자 열기가 허가를 내지 않았습니다 — E-2 누락. 단축키 경로가 등급 1에서 열자마자 닫힙니다(변이 M3).");
            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 4f);
            Assert.IsTrue(_window.IsOpen,
                $"{LogPrefix} ★ 사용자가 연 정보창이 {UserSurfaceSummonPolicy.LeaseSeconds * 4f:F2}초를 버티지 못했습니다.");

            // 양성 대조 — 닫으면 만료되어 회수가 돌아온다.
            _window.Close("B5 양성 대조 — 사용자가 닫았다");
            yield return Wait(UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} 양성 대조 실패 — 닫았는데 허가가 남았습니다.");
            Assert.IsTrue(_agent.ArePanelsSuppressed, $"{LogPrefix} 양성 대조 실패 — 허가가 만료됐는데 회수가 돌아오지 않았습니다.");

            Debug.Log($"{LogPrefix} B5 확인 — 자동 복귀는 허가 없이 걷히고, 사용자 열기는 허가를 받아 머뭅니다.");
        }

        // ==================== B7 — 등급 2(전체화면 게임)는 계속 닫힌다 — 원칙 2 경계 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator B7_등급2_전체화면_게임에서는_캐릭터_우클릭이_첫_홉에서_막힌다()
        {
            yield return LoadScene();

            // ---------- 양성 대조 먼저 — 같은 씬·같은 하네스가 등급 None에서는 연다 ----------
            AssertCursorInside();
            RightDown();
            yield return Wait(GearRadialMenuWidget.ExpandTotalSeconds + ObserveSlackSeconds);
            RightUp();
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} 양성 대조 실패 — 등급 None에서 우클릭이 부채꼴을 열지 못했습니다. 아래 «막혔다»는 무의미합니다.");
            _menu.Collapse(GearMenuCollapseMode.User, "B7 준비 — 등급 2로 넘어가기");
            yield return Wait(GearRadialMenuWidget.CollapseUserSeconds + ObserveSlackSeconds);
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} 준비 단계에서 부채꼴이 접히지 않았습니다.");

            // ---------- 등급 2 주입 — 프로덕션 등급 2는 축 3도 참이다(§16-2b B7: s=T, r=T) ----------
            SetPanelRetreat(true);
            SetGameAxis(true);
            yield return Wait(SettleSeconds);

            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 축 1 주입이 Suspend로 이어지지 않았습니다 — 아래 단언은 아무것도 재지 않습니다.");
            Assert.IsTrue(_agent.HidesScreenSurfaces, $"{LogPrefix} 등급 2인데 표면 채널이 거짓입니다.");
            Assert.IsTrue(_agent.ArePanelsSuppressed, $"{LogPrefix} 등급 2인데 표면 억제가 거짓입니다.");
            Assert.IsFalse(_gear.IsIconVisible, $"{LogPrefix} 전체화면 게임 위에 톱니가 남았습니다(원칙 2).");
            Assert.IsFalse(_control.IsReactionHoldActive,
                $"{LogPrefix} 전제 불성립 — 양성 대조의 호명 반응 붙잡기가 아직 남아 있어 아래 «반응이 안 걸렸다»를 잴 수 없습니다.");

            bool cursorOverBody = AnyColliderContains(CursorWorldNow());
            Debug.Log($"{LogPrefix} B7 진단 — 등급 2에서 몸 중심 좌표가 캐릭터 콜라이더에 걸리는가={cursorOverBody}. " +
                "false면 0항(커서∈캐릭터)이 먼저 닫고, true면 넷째 항(IsUserSummonBlocked)이 닫는다. " +
                "넷째 항 자체의 원칙 2 경계는 EditMode(정책 진리표 · F18 · F18c)가 잠근다.");

            // ---------- 첫 홉 시도 ----------
            RightDown();
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} ★ 등급 2(전체화면 게임)에서 캐릭터 우클릭이 부채꼴을 폈습니다 — 원칙 2 정면 위반(§16-2b B7).");
            Assert.IsFalse(_control.IsReactionHoldActive,
                $"{LogPrefix} ★ 등급 2에서 우클릭이 게이트를 통과했습니다(호명 반응 붙잡기가 걸렸다) — 숨은 캐릭터의 이동이 멈춥니다.");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} ★ 등급 2에서 허가가 났습니다 — 원칙 2의 구멍입니다.");

            float deadline = Time.realtimeSinceStartup + GearRadialMenuWidget.ExpandTotalSeconds + ObserveSlackSeconds;
            bool everVisible = false;
            int frames = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (_menu.IsVisible) everVisible = true;
                frames++;
                yield return null;
            }
            RightUp();

            Assert.Greater(frames, 1, $"{LogPrefix} 관측 프레임이 {frames}개뿐입니다 — «한 프레임도 안 펴졌다»를 잴 수 없습니다.");
            Assert.IsFalse(everVisible, $"{LogPrefix} ★ 등급 2에서 관측 {frames}프레임 중 부채꼴이 한 번이라도 보였습니다(원칙 2).");
            Assert.IsFalse(_agent.IsUserSummonGrantActive, $"{LogPrefix} ★ 등급 2 관측 뒤 허가가 살아 있습니다.");

            Debug.Log($"{LogPrefix} B7 확인 — 등급 2에서 우클릭이 {frames}프레임 동안 한 번도 부채꼴을 펴지 않았고 허가·반응도 없었습니다" +
                $"(같은 씬 등급 None에서는 열렸습니다, 커서∈몸={cursorOverBody}).");
        }
    }
}
