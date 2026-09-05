using System.Collections;
using System.Collections.Generic;
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
    /// ★★ 캐릭터 우클릭 부채꼴의 <b>런타임 항목</b> — docs/TEST_PLAN_FAN_AND_EQUIPMENT.md의
    /// <b>F2 · F5 · F5b · F23′</b>(2026-09-05 test-engineer 실행분).
    ///
    /// ============================================================================
    /// 어떻게 «진짜 클릭»을 만드는가 — <b>프로덕션 본체를 그대로 태운다</b>
    /// ============================================================================
    /// 에디터에는 전역 포인터 서비스가 없어 <c>AppControlDirector.TickRightClickFan()</c>이 첫 줄에서
    /// 돌아간다. 그래서 <b>가짜 버튼 서비스</b>를 그 자리(<c>_buttonService</c>)에 꽂고 <b>같은 본체</b>를
    /// 부른다 — 게이트 5항·엣지 판정·재앵커·허가 발급이 전부 프로덕션 코드다. 테스트 전용 우회 경로를
    /// 새로 만들면 «테스트가 통과한다 = 실제 클릭이 동작한다»는 보장이 그 순간 사라진다
    /// (<c>StickmanClickHitbox.ProcessGlobalButtonSample</c>과 같은 관례).
    ///
    /// <para><b>커서는 매 조회마다 몸을 다시 읽는다</b> — 좌표를 한 번 떠 두면 캐릭터가 걷는 동안
    /// «안»이었던 점이 «밖»이 되어, 프로덕션이 멀쩡한데 테스트만 흔들린다. 그래서 «안/밖»을
    /// <b>모드</b>로 두고 그때그때 몸 중심에서 유도한다(좌표 하드코딩 금지 — F2의 요구다).</para>
    ///
    /// <para>★ <b>이 픽스처는 「대기 톱니」 게이트 우회를 스스로 끈다</b>
    /// (<c>GlobalPlayModeTestIsolation</c>이 어셈블리 전체에 켜 두는 그것).
    /// 우회가 켜진 세상에서는 톱니가 상시로 보이고 <b>차단막 합집합이 다른 모양</b>이 되어,
    /// 아래 <c>차단막</c> 케이스가 <b>출하 조건이 아닌 것</b>을 재게 된다.</para>
    ///
    /// <para><b>시간은 전부 벽시계(초)</b>이고 <b>숫자는 전부 프로덕션 상수식</b>이다(CLAUDE.md).</para>
    /// </summary>
    public sealed class RightClickFanRuntimeTests
    {
        private const string LogPrefix = "[우클릭부채꼴-TEST]";

        /// <summary>씬이 자리를 잡는 데 주는 여유(초).</summary>
        private const float SettleSeconds = 0.35f;

        /// <summary>관측 슬랙(초) — 연출 예산 위에 얹는다. 예산 자체는 전부 프로덕션 상수에서 온다.</summary>
        private const float ObserveSlackSeconds = 0.25f;

        /// <summary>커서를 «밖»에 둘 때 몸 중심에서 떨어뜨리는 거리(신장 배수). 콜라이더 집합 어디에도
        /// 걸리지 않는지는 <see cref="AssertCursorOutside"/>가 <b>실제 <c>OverlapPoint</c>로</b> 확인한다.</summary>
        private const float OutsideHeightMultiplier = 4f;

        private enum CursorMode { Inside, Outside }

        /// <summary>가짜 전역 포인터 — 우리가 세운 값을 그대로 돌려준다(조회는 항상 성공).</summary>
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

        private readonly ScriptedButtons _buttons = new ScriptedButtons();
        private CursorMode _cursorMode = CursorMode.Inside;

        private object _savedButtonService;
        private CursorPositionQuery _savedCursor;
        private Vector2 _savedOverlayOrigin;
        private bool _hijacked;   // 전역 상태를 실제로 가로챘는가 — 아니면 되돌리지도 않는다.
        private readonly List<Collider2D> _disabledByTest = new List<Collider2D>();

        private static readonly FieldInfo ButtonServiceField =
            typeof(AppControlDirector).GetField("_buttonService", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo TickRightClickFanMethod =
            typeof(AppControlDirector).GetMethod("TickRightClickFan", BindingFlags.Instance | BindingFlags.NonPublic);

        // ==================== 준비 / 정리 ====================

        [OneTimeSetUp]
        public void RequireIsolation()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 " +
                "않았습니다(절대 불변 원칙 3).");
            Assert.IsTrue(InfoGearIconWidget.IsStandbyGateBypassedForTests,
                $"{LogPrefix} 「대기 톱니」 게이트 우회가 시작 시점에 꺼져 있습니다 — 이 픽스처가 «끄는 " +
                "쪽»이라는 전제가 무너집니다.");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [OneTimeTearDown]
        public void ForceRestoreBypass()
        {
            InfoGearIconWidget.SetStandbyGateBypassedForTests(true);
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [SetUp]
        public void TurnBypassOff()
        {
            // 출하 조건에서 잰다 — 톱니는 평상시 없다.
            InfoGearIconWidget.SetStandbyGateBypassedForTests(false);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            for (int i = 0; i < _disabledByTest.Count; i++)
            {
                if (_disabledByTest[i] != null) _disabledByTest[i].enabled = true;
            }
            _disabledByTest.Clear();

            if (_menu != null) _menu.ForceCloseAll("테스트 정리");

            // ★ 가로챈 적이 없으면 되돌리지도 않는다 — LoadScene이 도중에 실패하면 «저장된 값»이
            //   실제 값이 아니라 기본값(0)이라, 조건 없이 되돌리면 그것이 오히려 오염이 된다.
            if (_hijacked)
            {
                if (_control != null && ButtonServiceField != null)
                    ButtonServiceField.SetValue(_control, _savedButtonService);
                if (_agent != null && _agent.Blackboard != null) _agent.Blackboard.CursorProvider = _savedCursor;
                ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOverlayOrigin;
                _hijacked = false;
            }

            InfoGearIconWidget.SetStandbyGateBypassedForTests(true);

            _agent = null;
            _control = null;
            _menu = null;
            _gear = null;
            _savedButtonService = null;
            yield return null;
        }

        private IEnumerator LoadScene()
        {
            Assert.IsNotNull(ButtonServiceField,
                $"{LogPrefix} AppControlDirector._buttonService 필드를 찾지 못했습니다 — 우클릭 채널의 " +
                "이름이 바뀌었다면 이 테스트의 주입 경로도 함께 고쳐야 합니다(조용히 0건이 되지 않게 " +
                "여기서 멈춥니다).");
            Assert.IsNotNull(TickRightClickFanMethod,
                $"{LogPrefix} AppControlDirector.TickRightClickFan()을 찾지 못했습니다 — 우클릭 폴링의 " +
                "본체가 사라졌거나 이름이 바뀌었습니다.");

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

            Assert.IsFalse(_agent.ArePanelsSuppressed,
                $"{LogPrefix} 새 씬인데 표면 억제(등급 1)가 켜져 있습니다 — 게이트 4항이 이미 닫혀 있어 " +
                "«안 열린다»가 전부 빈 조건이 됩니다.");
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

        /// <summary>프로덕션 폴링 본체를 한 번 돈다.</summary>
        private void Tick() => TickRightClickFanMethod.Invoke(_control, null);

        /// <summary>우클릭 상승 엣지 1회(누름 → 뗌은 부르는 쪽이 필요할 때 따로 한다).</summary>
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

        // ==================== 커서 ====================

        private Vector2 BodyCenterWorld()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            return bb.Body.position + new Vector2(0f, bb.CharacterHeightWorld * 0.5f);
        }

        private Vector2 CursorWorldNow()
        {
            Vector2 center = BodyCenterWorld();
            if (_cursorMode == CursorMode.Inside) return center;
            return center + new Vector2(0f, _agent.Blackboard.CharacterHeightWorld * OutsideHeightMultiplier);
        }

        private bool TryGetScriptedCursor(out Vector2 osScreenPosition)
        {
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : null;
            if (cam == null) { osScreenPosition = default; return false; }
            osScreenPosition = ScreenCoordinateConverter.WorldToOsScreen(cam, CursorWorldNow(), _agent.Config, out _);
            return true;
        }

        /// <summary>지금 커서 좌표가 <b>실제로</b> 캐릭터 콜라이더 집합 안인가 —
        /// 좌표를 하드코딩하지 않았다는 것을 <c>OverlapPoint</c>로 증명한다(F2의 요구).</summary>
        private void AssertCursorInside()
        {
            _cursorMode = CursorMode.Inside;
            Assert.IsTrue(AnyColliderContains(CursorWorldNow()),
                $"{LogPrefix} «안» 좌표가 어떤 콜라이더에도 안 걸립니다 — 이 전제가 깨지면 아래 " +
                "«열린다/안 열린다»가 전부 다른 것을 재게 됩니다.");
        }

        private void AssertCursorOutside()
        {
            _cursorMode = CursorMode.Outside;
            Assert.IsFalse(AnyColliderContains(CursorWorldNow()),
                $"{LogPrefix} «밖» 좌표가 콜라이더 안입니다 — 이 전제가 깨지면 «밖에서는 안 열린다»가 " +
                "빈 조건이 됩니다.");
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

        private static float Area(Rect r) => Mathf.Max(0f, r.width) * Mathf.Max(0f, r.height);

        private IEnumerator WaitExpanded()
        {
            yield return Wait(GearRadialMenuWidget.ExpandTotalSeconds + ObserveSlackSeconds);
        }

        private IEnumerator WaitCollapsedUser()
        {
            yield return Wait(GearRadialMenuWidget.CollapseUserSeconds + ObserveSlackSeconds);
        }

        // ==================== F2 ====================

        /// <summary>
        /// <b>F2</b> — 캐릭터 콜라이더 <b>밖</b> 우클릭은 부채꼴을 열지 않는다.
        /// <b>양성 대조</b>: 콜라이더 <b>안</b>이면 열린다. <b>음성 대조</b>: 콜라이더를 전부 끄면
        /// <b>안쪽 좌표로도</b> 안 열린다(= 판정이 «좌표»가 아니라 «콜라이더»를 본다).
        ///
        /// <para>★ <b>가시성만 보지 않는다</b>(내 F-표의 FP-7): 안 열렸다면 <b>클릭관통 차단막도
        /// 커지지 않았어야</b> 한다. 두 관측을 같은 케이스에서 함께 본다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F2_캐릭터_밖_우클릭은_부채꼴을_열지_않는다()
        {
            yield return LoadScene();

            // ── ① 양성 대조 먼저 — 이 하네스가 «열 수 있다»를 증명한다 ────────
            AssertCursorInside();
            RightDown();
            yield return WaitExpanded();
            Assert.IsTrue(_menu.IsVisible,
                $"{LogPrefix} 양성 대조 실패 — 콜라이더 «안»에서 우클릭했는데 부채꼴이 열리지 않았습니다. " +
                "이 하네스는 아래 «안 열린다»를 잴 자격이 없습니다(주입이 아무 일도 안 했을 수 있습니다).");
            Assert.AreEqual(GearMenuAnchorSource.Character, _menu.AnchorSource,
                $"{LogPrefix} 열리긴 했는데 앵커가 «캐릭터»가 아닙니다 — 다른 경로가 열었을 수 있습니다.");
            Debug.Log($"{LogPrefix} 양성 대조 통과 — 콜라이더 안 우클릭이 부채꼴을 폅니다.");

            RightUp();
            _menu.Collapse(GearMenuCollapseMode.User, "F2 준비 — 음성 대조로 넘어가기");
            yield return WaitCollapsedUser();
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} 준비 단계에서 부채꼴이 접히지 않았습니다.");

            // ── ② 본 검사 — 콜라이더 «밖» ────────────────────────────────────
            AssertCursorOutside();
            float blockerBefore = Area(_gear.InteractiveScreenRect);
            RightDown();
            yield return WaitExpanded();

            Assert.IsFalse(_menu.IsVisible,
                $"{LogPrefix} ★ 캐릭터 «밖»에서 우클릭했는데 부채꼴이 열렸습니다 — 사용자가 밑의 앱에 " +
                "내리려던 우클릭을 우리가 가로챈 것입니다(절대 불변 원칙 2).");
            Assert.LessOrEqual(Area(_gear.InteractiveScreenRect), blockerBefore + 0.5f,
                $"{LogPrefix} ★ 부채꼴은 안 열렸는데 클릭관통 차단막 면적이 커졌습니다 " +
                $"({blockerBefore:F0} → {Area(_gear.InteractiveScreenRect):F0}px²) — «보이지 않는데 클릭만 " +
                "먹는» 형태입니다. 가시성만 보는 테스트는 이 상태를 초록으로 통과시킵니다(FP-7).");
            RightUp();

            // ── ③ 음성 대조 — 콜라이더를 끄면 «안쪽 좌표»로도 안 열린다 ──────
            //    판정이 좌표가 아니라 콜라이더를 본다는 것을 보인다.
            Vector2 insideWorld;
            {
                _cursorMode = CursorMode.Inside;
                insideWorld = CursorWorldNow();
            }
            Collider2D[] all = _agent.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].enabled) continue;
                all[i].enabled = false;
                _disabledByTest.Add(all[i]);
            }
            Assert.Greater(_disabledByTest.Count, 0,
                $"{LogPrefix} 끌 콜라이더가 0개였습니다 — 이 음성 대조는 아무것도 재지 않았습니다.");

            RightDown();
            yield return WaitExpanded();
            Assert.IsFalse(_menu.IsVisible,
                $"{LogPrefix} ★ 콜라이더를 전부 껐는데 «안쪽 좌표»만으로 부채꼴이 열렸습니다 — 판정이 " +
                $"콜라이더가 아니라 좌표를 보고 있습니다(커서 월드 {insideWorld}).");
            RightUp();

            for (int i = 0; i < _disabledByTest.Count; i++) _disabledByTest[i].enabled = true;
            int restored = _disabledByTest.Count;
            _disabledByTest.Clear();

            Debug.Log($"{LogPrefix} F2 확인 — 안=열림 / 밖=안 열림(차단막도 불변) / 콜라이더 {restored}개 " +
                "끔=안 열림.");
        }

        // ==================== ★ 차단막 번짐 (F2의 «면적» 절반) ====================

        /// <summary>
        /// ★★ <b>부채꼴이 열려 있는 동안 클릭관통 차단막이 부채꼴 밖으로 번지지 않는가</b>.
        ///
        /// <para><b>왜 이 케이스가 따로 있는가</b>: <c>InfoGearIconWidget.PlaceOnScreen</c>은 차단막을
        /// <c>Union(톱니 사각형, 부채꼴 사각형)</c>으로 만든다. 2026-09-05에 톱니가 «평상시 숨김»이
        /// 되면서 톱니 사각형은 <b>넓이 0</b>이 됐는데, 그 <b>위치는 화면 우상단 그대로</b>다.
        /// 경계상자 합집합은 «넓이 0»을 무시하지 않는다 — 점 하나라도 합집합에 들어간다.
        /// 그래서 캐릭터가 화면 아래쪽에 있으면 차단막이 <b>부채꼴에서 화면 우상단까지</b> 이어진
        /// 거대한 직사각형이 될 수 있고, 그 영역의 클릭은 전부 우리가 먹는다(절대 불변 원칙 2).</para>
        ///
        /// <para><b>판정</b>: 차단막 면적 ≤ 부채꼴 면적 × <see cref="BlockerAreaTolerance"/>.
        /// 배수로 재는 이유는 «몇 픽셀 여유»가 아니라 «다른 자릿수로 번졌는가»가 문제이기 때문이다.</para>
        /// </summary>
        private const float BlockerAreaTolerance = 1.10f;

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 차단막은_펼쳐진_부채꼴_밖으로_번지지_않는다()
        {
            yield return LoadScene();

            Assert.IsFalse(_gear.IsStandbyGearVisible,
                $"{LogPrefix} 전제 불성립 — 대기 톱니가 보이는 상태입니다. 이 케이스는 «평상시(톱니 없음) " +
                "+ 부채꼴 열림»이라는 <b>출하 조건</b>을 재는 것입니다.");

            AssertCursorInside();
            RightDown();
            yield return WaitExpanded();
            RightUp();

            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} 전제 불성립 — 부채꼴이 열리지 않았습니다.");

            Rect fan = _menu.UnionScreenRect;
            Rect blocker = _gear.InteractiveScreenRect;
            Assert.Greater(Area(fan), 0f,
                $"{LogPrefix} 부채꼴 면적이 0입니다 — 아래 비교가 빈 조건이 됩니다.");
            Assert.IsTrue(_gear.IsClickBlockerEnabled,
                $"{LogPrefix} 부채꼴이 떠 있는데 차단막이 꺼져 있습니다 — 버튼을 눌러도 그 클릭이 밑의 " +
                "앱으로 샙니다.");

            float ratio = Area(blocker) / Mathf.Max(1f, Area(fan));
            Debug.Log($"{LogPrefix} 실측 — 부채꼴 {fan.width:F0}×{fan.height:F0}={Area(fan):F0}px², " +
                $"차단막 {blocker.width:F0}×{blocker.height:F0}={Area(blocker):F0}px², 배수 {ratio:F2}×. " +
                $"톱니 중심(숨김 상태)={_gear.IconScreenCenter}, 화면={Screen.width}×{Screen.height}.");

            Assert.LessOrEqual(Area(blocker), Area(fan) * BlockerAreaTolerance,
                $"{LogPrefix} ★★ 차단막이 부채꼴보다 {ratio:F2}배 넓습니다 — 숨은 톱니의 «넓이 0» 사각형이 " +
                $"화면 우상단({_gear.IconScreenCenter})에 남아 경계상자 합집합을 그쪽까지 끌고 갔습니다. " +
                "그 사이 영역은 보이지도 않는데 클릭을 먹습니다(절대 불변 원칙 2). " +
                "고칠 곳은 InfoGearIconWidget.PlaceOnScreen의 Union 한 줄입니다 — 넓이 0인 쪽은 " +
                "합집합에서 빼야 합니다. [디버거로 이동]");
        }

        // ==================== F5 ====================

        /// <summary>
        /// <b>F5</b> — <b>누름(상승 엣지)에서 시작</b>한다. 뗌은 아무 일도 하지 않는다.
        /// <list type="bullet">
        ///   <item><b>음성</b>: 밖에서 눌러 안에서 떼면 <b>안 열린다</b>(뗌에 의미가 없다).</item>
        ///   <item><b>양성</b>: 안에서 누르면 그 자리에서 열린다.</item>
        ///   <item><b>유지</b>: 누른 채 밖으로 끌고 나가 떼도 <b>열린 상태 그대로</b>다.</item>
        /// </list>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F5_우클릭은_누름에서_시작하고_뗌은_아무_일도_하지_않는다()
        {
            yield return LoadScene();

            // ── ① 음성 — 밖에서 누르고 안에서 뗀다 ──────────────────────────
            AssertCursorOutside();
            RightDown();
            yield return null;
            AssertCursorInside();
            RightUp();
            yield return WaitExpanded();

            Assert.IsFalse(_menu.IsVisible,
                $"{LogPrefix} ★ 밖에서 눌러 «안»에서 뗐는데 부채꼴이 열렸습니다 — 판정이 뗌(하강 엣지)에 " +
                "걸려 있습니다. 그러면 데스크톱의 우클릭 드래그 관습과 충돌하고, 사용자가 밑의 앱에 " +
                "내리려던 우클릭의 «뗌»까지 우리가 먹습니다.");

            // ── ② 양성 — 안에서 누른다 ──────────────────────────────────────
            AssertCursorInside();
            RightDown();
            yield return WaitExpanded();
            Assert.IsTrue(_menu.IsVisible,
                $"{LogPrefix} 안에서 눌렀는데 부채꼴이 열리지 않았습니다 — 위 «안 열렸다»가 «원래 아무것도 " +
                "안 열린다»와 구별되지 않게 됩니다.");

            // ── ③ 유지 — 누른 채 밖으로 끌고 나가 뗀다 ─────────────────────
            AssertCursorOutside();
            RightUp();
            yield return WaitExpanded();
            Assert.IsTrue(_menu.IsVisible,
                $"{LogPrefix} ★ 누른 채 밖으로 나가 떼자 부채꼴이 사라졌습니다 — 뗌에 «닫기» 의미가 " +
                "붙어 있습니다(F5는 뗌이 아무 일도 하지 않기를 요구합니다).");

            Debug.Log($"{LogPrefix} F5 확인 — 밖누름+안뗌=안 열림 / 안누름=열림 / 누른 채 밖에서 뗌=유지.");
        }

        // ==================== F5b ====================

        /// <summary>
        /// <b>F5b</b> — <b>부채꼴 밖 우클릭은 닫지도 않는다</b>. 열린 상태에서 <b>캐릭터 밖</b>을
        /// 우클릭해도 <c>IsVisible</c>은 불변이고 접힘도 시작되지 않는다.
        ///
        /// <para>★ 좌표를 «캐릭터 밖»으로 잡는 이유: 캐릭터 <b>위</b>의 재우클릭은 <b>같은 문</b>이라
        /// 설계상 토글 닫기가 맞다(§5-7). 여기서 재는 것은 «우리와 상관없는 자리의 우클릭»이다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F5b_부채꼴_밖_우클릭은_닫지도_않는다()
        {
            yield return LoadScene();

            AssertCursorInside();
            RightDown();
            yield return WaitExpanded();
            RightUp();
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} 전제 불성립 — 부채꼴이 열리지 않았습니다.");
            Assert.IsTrue(_menu.IsExpanded, $"{LogPrefix} 전제 불성립 — 부채꼴이 펼침 상태가 아닙니다.");

            // ── 밖을 우클릭 ─────────────────────────────────────────────────
            AssertCursorOutside();
            RightDown();
            yield return WaitCollapsedUser();   // 접혔다면 이 예산 안에 사라졌을 것이다.

            Assert.IsTrue(_menu.IsVisible,
                $"{LogPrefix} ★ 부채꼴 밖(캐릭터 밖) 우클릭이 부채꼴을 닫았습니다 — 그 우클릭은 밑의 " +
                "앱의 것입니다. 우리가 그것을 «닫기»로 소비하면 사용자는 두 번 눌러야 합니다(§5-3).");
            Assert.IsTrue(_menu.IsExpanded,
                $"{LogPrefix} ★ 밖 우클릭이 접힘을 시작시켰습니다(펼침 상태가 아닙니다).");
            RightUp();

            Debug.Log($"{LogPrefix} F5b 확인 — 캐릭터 밖 우클릭은 열지도 닫지도 않습니다.");
        }

        // ==================== F23′ ====================

        /// <summary>
        /// <b>F23′</b> — <b>재앵커</b>. 톱니 앵커로 열린 부채꼴에 캐릭터 우클릭이 오면
        /// <b>접고 캐릭터 앵커로 다시 편다</b>. 예산은
        /// <c>CollapseDragSeconds + ExpandTotalSeconds</c>이고, 그 사이 <c>IsVisible</c>이
        /// <b>한 프레임도</b> 거짓이 되면 안 된다(깜빡임 금지).
        ///
        /// <para><b>음성 대조</b>: 같은 문(캐릭터)을 다시 누르면 재앵커가 아니라 <b>토글 닫기</b>다.</para>
        ///
        /// <para>★ 톱니 앵커 쪽 개시는 <c>_menu.Expand(..., Gear, 0f)</c>를 직접 부른다 — 2026-09-05
        /// 이후 톱니는 평상시 화면에 없어 «톱니 클릭»을 실행으로 만들 수 없기 때문이다. 재는 대상은
        /// «톱니를 어떻게 눌렀는가»가 아니라 <b>«다른 문에서 온 요청이 재앵커로 처리되는가»</b>다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator F23_다른_문에서_열린_부채꼴은_깜빡임_없이_캐릭터_앵커로_재앵커한다()
        {
            yield return LoadScene();

            _menu.Expand(_gear.IconScreenCenter, GearMenuAnchorSource.Gear, 0f);
            yield return WaitExpanded();
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} 전제 불성립 — 톱니 앵커로 열리지 않았습니다.");
            Assert.AreEqual(GearMenuAnchorSource.Gear, _menu.AnchorSource,
                $"{LogPrefix} 전제 불성립 — 앵커가 «톱니»가 아닙니다.");
            Vector2 gearOrigin = _menu.FanOriginPoints;

            // ── 캐릭터 우클릭 → 재앵커 ──────────────────────────────────────
            AssertCursorInside();
            RightDown();

            float budget = GearRadialMenuWidget.CollapseDragSeconds
                + GearRadialMenuWidget.ExpandTotalSeconds + ObserveSlackSeconds;
            float deadline = Time.realtimeSinceStartup + budget;
            bool everInvisible = false;
            int frames = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!_menu.IsVisible) everInvisible = true;
                frames++;
                yield return null;
            }
            RightUp();

            Assert.Greater(frames, 1,
                $"{LogPrefix} 관측 프레임이 {frames}개뿐입니다 — «한 프레임도 안 사라졌다»를 잴 수 없습니다.");
            Assert.IsFalse(everInvisible,
                $"{LogPrefix} ★ 재앵커 도중 부채꼴이 한 번이라도 사라졌습니다({frames}프레임 관측) — " +
                "접힘 완료와 재펼침이 같은 프레임에 일어나지 않았다는 뜻이고, 사용자 눈에는 깜빡임입니다.");
            Assert.AreEqual(GearMenuAnchorSource.Character, _menu.AnchorSource,
                $"{LogPrefix} ★ 예산 {budget:F2}초가 지났는데 앵커가 «캐릭터»가 아닙니다 — 재앵커가 " +
                "일어나지 않았습니다.");
            Assert.AreNotEqual(gearOrigin, _menu.FanOriginPoints,
                $"{LogPrefix} 앵커 이름만 바뀌고 원점은 그대로입니다({gearOrigin}) — 재앵커가 좌표까지 " +
                "옮기지 않았습니다.");
            Assert.IsFalse(_menu.IsReanchorPending,
                $"{LogPrefix} 재앵커 예약이 아직 남아 있습니다 — 예산 안에 끝나지 않았습니다.");

            // ── 음성 대조: 같은 문을 다시 누르면 토글 닫기 ──────────────────
            RightDown();
            yield return WaitCollapsedUser();
            Assert.IsFalse(_menu.IsVisible,
                $"{LogPrefix} ★ 같은 문(캐릭터)을 다시 눌렀는데 닫히지 않았습니다 — 재우클릭이 토글이 " +
                "아니면 부채꼴을 마우스로 닫을 방법이 사라집니다(6초 자동 접힘만 남습니다).");
            RightUp();

            Debug.Log($"{LogPrefix} F23′ 확인 — 톱니 앵커 → 캐릭터 앵커 재앵커가 {frames}프레임 동안 " +
                "한 번도 사라지지 않았고, 같은 문 재입력은 토글로 닫혔습니다.");
        }
    }
}
