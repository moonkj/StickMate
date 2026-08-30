using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 캐릭터 창 = <b>배타적 모달</b> + 창 밖 클릭 탈출구 + 타이틀바 드래그 — 2026-08-30 회귀.
    ///
    /// ============================================================================
    /// 무엇이 문제였나 (디버거 실측 재현)
    /// ============================================================================
    /// 정보창(31000)이 부채꼴(31500)/팝오버(31700)보다 <b>아래</b>에 깔렸는데, 세 진입점 중
    /// 부채꼴 경유만 나머지를 정리했다. 그래서
    ///  · 시나리오 B: 팝오버가 뜬 채 단축키(⌃⌥⌘I)로 창을 열면 캔버스 3개가 동시에 떴고,
    ///  · 시나리오 C: 창이 열린 채 톱니를 다시 누르면(사용자가 창을 닫으려고 하는 가장 자연스러운 동작)
    ///    부채꼴이 창 <b>위로</b> 펼쳐졌다.
    /// 게다가 33-7-9가 규정한 "창 밖 클릭" 탈출구가 구현되어 있지 않아 실제 탈출구는 [✕] 하나뿐이었다.
    ///
    /// ============================================================================
    /// 절대 조건으로 잠그는 것
    /// ============================================================================
    ///  ① <b>배타</b>: 어느 진입점으로 열든(부채꼴 / 단축키 경로) 창이 뜬 뒤에는 부채꼴도 팝오버도 없다.
    ///  ② <b>역방향</b>: 창이 열린 채 톱니를 누르면 창이 닫히고 부채꼴은 펼쳐지지 않는다.
    ///  ③ <b>창 밖 클릭</b>으로 닫힌다. 단 창 안 클릭과 <b>톱니 위 클릭</b>은 닫지 않는다
    ///     (톱니는 뗀 순간 자기가 닫는다 — 여기서도 닫으면 한 클릭이 두 번 처리된다).
    ///  ④ <b>정렬</b>: 창이 부채꼴/팝오버보다 위, 말풍선과는 값이 <b>다르다</b>(동률은 Unity가 순서를
    ///     보장하지 않는다).
    ///  ⑤ <b>드래그</b>: 타이틀바를 끌면 창이 그 방향으로 움직이고, 아무리 끌어도 화면을 벗어나지 않으며,
    ///     닫았다 다시 열면 화면 중앙에서 시작한다(33-7-7의 "열면 중앙"은 유지).
    ///  ⑥ <b>가로 클램프</b>: 창이 화면 폭 안에 들어온다(예전에는 폭이 항상 880 고정이라 좁은 화면에서
    ///     좌우로 흘러나갔다).
    ///
    /// 입력 주입 관례는 InfoGearDragTests / InfoGearRadialMenuTests와 같다 — 테스트 전용 분기를 만들지
    /// 않고 실제 입력이 지나가는 같은 함수에 버튼 상태와 커서를 먹인다.
    /// </summary>
    public sealed class InfoWindowExclusiveModalTests
    {
        private const string LogPrefix = "[배타모달-TEST]";

        /// <summary>화면 크기를 배치 실행에서 바꿀 수 없어, 실제 클램프 함수에 배율을 주입해 같은 계산
        /// 경로로 창을 줄인다(InfoWindowClippedHitTestTests와 완전히 같은 관례).</summary>
        private static readonly MethodInfo ClampMethod = typeof(CharacterInfoWindow).GetMethod(
            "ClampPanelToScreen", BindingFlags.Instance | BindingFlags.NonPublic);

        private InfoGearIconWidget _gear;
        private CharacterInfoWindow _window;
        private GearRadialMenuWidget _menu;
        private string _backup;
        private bool _hadFile;

        [OneTimeSetUp]
        public void BackupRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            _hadFile = File.Exists(path);
            _backup = _hadFile ? File.ReadAllText(path) : null;
        }

        [OneTimeTearDown]
        public void RestoreRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            if (_hadFile) File.WriteAllText(path, _backup);
            else if (File.Exists(path)) File.Delete(path);
            UiLayoutModel.ResetForTesting();
        }

        [SetUp]
        public void ResetLayout()
        {
            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Save();
        }

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            _gear = null;
            _menu = null;
            yield return null;
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _gear = Object.FindFirstObjectByType<InfoGearIconWidget>();
            Assert.IsNotNull(_gear, $"{LogPrefix} 씬에 InfoGearIconWidget이 없습니다.");
            _window = _gear.GetComponent<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            _menu = _gear.GetComponent<GearRadialMenuWidget>();
            Assert.IsNotNull(_menu, $"{LogPrefix} 씬에 GearRadialMenuWidget이 없습니다.");
            yield return null;
        }

        /// <summary>실제 사용자와 같은 순서 — 짧게 눌렀다 떼고 부채꼴이 다 펼쳐질 때까지 기다린다.</summary>
        private IEnumerator ClickGear()
        {
            Vector2 center = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(false, center);
            _gear.FeedPointerForTests(true, center);
            _gear.FeedPointerForTests(false, center);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.MenuReadySeconds + 0.35f);
        }

        private IEnumerator ClickMenuButton(GearMenuButton button)
        {
            Vector2 p = _menu.ButtonScreenCenter((int)button);
            _gear.FeedPointerForTests(false, p);
            _gear.FeedPointerForTests(true, p);
            _gear.FeedPointerForTests(false, p);
            yield return new WaitForSecondsRealtime(0.6f);
        }

        private static Canvas FindCanvas(string namePrefix)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.name.StartsWith(namePrefix)) return c;
            return null;
        }

        private static bool IsCanvasOn(string namePrefix)
        {
            Canvas c = FindCanvas(namePrefix);
            return c != null && c.gameObject.activeInHierarchy;
        }

        // ==================== ① 배타 — 단축키 경로(시나리오 B) ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OpeningWhilePopoverIsUpLeavesOnlyTheWindow()
        {
            yield return LoadSceneAndResolve();

            yield return ClickGear();
            yield return ClickMenuButton(GearMenuButton.Todo);

            var todo = _gear.GetComponent<TodoBoardPopover>();
            Assert.IsNotNull(todo, $"{LogPrefix} TodoBoardPopover가 없습니다.");
            Assert.IsTrue(todo.IsOpen, $"{LogPrefix} 관측 전제 실패 — [오늘 할일] 팝오버가 열리지 않았습니다.");

            // 단축키(⌃⌥⌘I) / 우클릭 메뉴와 <b>같은</b> 진입점.
            _window.Toggle("테스트 — 단축키 경로");
            yield return new WaitForSecondsRealtime(0.6f);

            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
            Assert.IsFalse(todo.IsOpen, $"{LogPrefix} 창을 열었는데 할일 팝오버가 그대로 떠 있습니다(시나리오 B 재발).");
            Assert.IsFalse(_gear.IsMenuExpanded, $"{LogPrefix} 창을 열었는데 부채꼴이 펼쳐진 채 남아 있습니다.");
            Assert.IsFalse(IsCanvasOn("TodoBoardPopoverCanvas"),
                $"{LogPrefix} 할일 팝오버 캔버스가 아직 켜져 있습니다 — 화면에 창이 겹쳐 보입니다.");
            Assert.IsFalse(IsCanvasOn("GearRadialMenuCanvas"),
                $"{LogPrefix} 부채꼴 캔버스가 아직 켜져 있습니다 — 화면에 창이 겹쳐 보입니다.");
            Assert.IsTrue(IsCanvasOn("CharacterInfoCanvas"), $"{LogPrefix} 정작 창 캔버스가 꺼져 있습니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 단축키 경로로 열어도 창 하나만 남습니다.");
        }

        // ==================== ② 역방향 — 창이 열린 채 톱니 재클릭(시나리오 C) ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator GearClickWhileWindowIsOpenClosesItWithoutExpandingTheFan()
        {
            yield return LoadSceneAndResolve();

            yield return ClickGear();
            yield return ClickMenuButton(GearMenuButton.Character);
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 관측 전제 실패 — [캐릭터] 버튼으로 창이 열리지 않았습니다.");

            // 창을 닫으려고 톱니를 다시 누른다(사용자의 주 진입점이 톱니다).
            yield return ClickGear();

            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 톱니를 다시 눌렀는데 창이 닫히지 않았습니다.");
            Assert.IsFalse(_gear.IsMenuExpanded,
                $"{LogPrefix} 톱니를 다시 눌렀더니 부채꼴이 펼쳐졌습니다 — 창 위에 메뉴가 얹히는 시나리오 C 재발.");

            // 창이 없으면 톱니는 평소처럼 부채꼴을 편다(기능을 죽인 것이 아니다 — 네거티브 컨트롤).
            yield return ClickGear();
            Assert.IsTrue(_gear.IsMenuExpanded,
                $"{LogPrefix} 창이 닫힌 상태에서도 부채꼴이 펼쳐지지 않습니다 — 톱니 기능을 죽였습니다.");

            _menu.Collapse(GearMenuCollapseMode.User, "테스트 정리");
            Debug.Log($"{LogPrefix} ② 통과 — 톱니 재클릭은 창을 닫기만 하고, 창이 없을 때만 부채꼴을 폅니다.");
        }

        // ==================== ③ 창 밖 클릭 탈출구(33-7-9) ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OutsideClickClosesWindowButInsideAndGearDoNot()
        {
            yield return LoadSceneAndResolve();

            _window.Open("테스트 — 창 밖 클릭");
            yield return null;
            yield return null;
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");

            // (a) 창 안 클릭은 닫지 않는다.
            _window.FeedClickForTests(_window.PanelScreenRect.center);
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창 안을 눌렀는데 창이 닫혔습니다.");
            yield return new WaitForSecondsRealtime(0.5f);   // 클릭 중복 억제(0.35초)를 넘긴다.

            // (b) 톱니 위 클릭도 닫지 않는다 — 그 클릭은 톱니가 뗀 순간 자기가 처리한다.
            // 좁은 배치 화면(640×480)에서는 880 창이 우상단 톱니까지 덮어 "창 밖"과 "톱니 위"를 구분할 수
            // 없다. 그래서 이 한 프레임만 창을 최소 크기로 줄여 둘을 갈라 놓는다(주입 관례는 클래스 문서 참고).
            Assert.IsNotNull(ClampMethod, $"{LogPrefix} ClampPanelToScreen을 찾지 못했습니다 — 이름이 바뀌었습니다.");
            ClampMethod.Invoke(_window, new object[] { ScaleFactorForSmallPanel() });
            Assert.IsFalse(_window.PanelScreenRect.Contains(_gear.IconScreenCenter),
                $"{LogPrefix} 관측 전제 실패 — 창을 줄였는데도 톱니가 창 안에 있습니다.");
            _window.FeedClickForTests(_gear.IconScreenCenter);
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 톱니 위를 눌렀는데 창이 먼저 닫혔습니다(한 클릭이 두 번 처리됩니다).");
            yield return new WaitForSecondsRealtime(0.5f);

            // (c) 그 밖의 창 밖 클릭은 닫는다.
            Vector2 outside = new Vector2(4f, 4f);   // 화면 좌하단 구석 — 창(중앙 모달)과도 톱니(우상단)와도 겹치지 않는다.
            Assert.IsFalse(_window.PanelScreenRect.Contains(outside),
                $"{LogPrefix} 관측 전제 실패 — 창이 화면 좌하단 구석까지 덮고 있습니다.");
            _window.FeedClickForTests(outside);
            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 창 밖을 눌렀는데 닫히지 않았습니다 — 33-7-9의 탈출구가 없습니다.");

            Debug.Log($"{LogPrefix} ③ 통과 — 창 밖 클릭으로 닫히고, 창 안/톱니 위는 예외입니다.");
        }

        // ==================== ④ 정렬 순서 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ModalSortsAboveFanAndPopoversAndDiffersFromBubble()
        {
            yield return LoadSceneAndResolve();

            // 팝오버 캔버스는 한 번 열어야 만들어진다(지연 생성).
            yield return ClickGear();
            yield return ClickMenuButton(GearMenuButton.Todo);
            yield return ClickGear();   // 접는다(팝오버 캔버스는 남는다).
            yield return new WaitForSecondsRealtime(0.4f);

            _window.Open("테스트 — 정렬 확인");
            yield return null;

            Canvas info = FindCanvas("CharacterInfoCanvas");
            Canvas fan = FindCanvas("GearRadialMenuCanvas");
            Canvas todo = FindCanvas("TodoBoardPopoverCanvas");
            Canvas bubble = FindCanvas("DialogueBubbleCanvas");

            Assert.IsNotNull(info, $"{LogPrefix} CharacterInfoCanvas를 찾지 못했습니다.");
            Assert.IsNotNull(fan, $"{LogPrefix} GearRadialMenuCanvas를 찾지 못했습니다.");
            Assert.IsNotNull(todo, $"{LogPrefix} TodoBoardPopoverCanvas를 찾지 못했습니다.");

            Assert.Greater(info.sortingOrder, fan.sortingOrder,
                $"{LogPrefix} 모달인 창({info.sortingOrder})이 부채꼴({fan.sortingOrder})보다 아래입니다.");
            Assert.Greater(info.sortingOrder, todo.sortingOrder,
                $"{LogPrefix} 모달인 창({info.sortingOrder})이 팝오버({todo.sortingOrder})보다 아래입니다.");
            if (bubble != null)
            {
                Assert.AreNotEqual(bubble.sortingOrder, info.sortingOrder,
                    $"{LogPrefix} 말풍선과 창의 sortingOrder가 동률입니다 — Unity는 동률 캔버스의 그리기 순서를 보장하지 않습니다.");
            }

            Debug.Log($"{LogPrefix} ④ 통과 — 창 {info.sortingOrder} > 팝오버 {todo.sortingOrder} > 부채꼴 {fan.sortingOrder}" +
                (bubble != null ? $", 말풍선 {bubble.sortingOrder}(값 분리됨)" : ""));
        }

        // ==================== ⑤ 타이틀바 드래그 + ⑥ 화면 안 클램프 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TitleBarDragMovesWindowStaysOnScreenAndReopenRecenters()
        {
            yield return LoadSceneAndResolve();
            Assert.IsNotNull(ClampMethod, $"{LogPrefix} ClampPanelToScreen을 찾지 못했습니다 — 이름이 바뀌었습니다.");

            _window.Open("테스트 — 드래그");
            yield return null;
            yield return null;

            // ⑥ 열자마자 창은 화면 안에 들어와 있어야 한다(가로 클램프가 없으면 여기서 좌우로 샌다).
            AssertPanelInsideScreen("열린 직후");
            Assert.AreEqual(Vector2.zero, _window.PanelOffsetPoints, $"{LogPrefix} 창이 화면 중앙에서 시작하지 않았습니다.");

            // ★ 이하 한 프레임 안에서 측정한다 — Update가 매 프레임 실제 화면 크기로 다시 클램프하므로
            //   주입한 크기는 그 프레임 안에서만 유효하다(레이아웃 그룹이 없어 코너는 즉시 갱신된다).
            //   좁은 배치 화면(640×480)에서는 창이 화면을 거의 다 채워 움직일 여백이 0이라, 창을 최소
            //   크기로 줄여 "옮길 여백이 있는 화면"을 흉내낸다.
            ClampMethod.Invoke(_window, new object[] { ScaleFactorForSmallPanel() });

            Rect bar = _window.TitleBarScreenRect;
            Assert.Greater(bar.width * bar.height, 0f, $"{LogPrefix} 타이틀바 사각형이 비어 있습니다.");

            Vector2 grab = bar.center;
            _window.FeedPointerForTests(false, grab);   // 첫 표본(Open이 버리는 것) 소모.
            _window.FeedPointerForTests(true, grab);
            Assert.IsTrue(_window.IsDraggingPanel, $"{LogPrefix} 타이틀바를 눌렀는데 드래그가 시작되지 않았습니다.");

            _window.FeedPointerForTests(true, grab + new Vector2(60f, -40f));
            Vector2 moved = _window.PanelOffsetPoints;
            Assert.Greater(moved.x, 0.5f, $"{LogPrefix} 오른쪽으로 끌었는데 창이 오른쪽으로 가지 않았습니다({moved}).");
            Assert.Less(moved.y, -0.5f, $"{LogPrefix} 아래로 끌었는데 창이 내려가지 않았습니다({moved}).");
            AssertPanelInsideScreen("끄는 중");

            // 화면 밖으로 끌어내도 나가지 않는다.
            _window.FeedPointerForTests(true, grab + new Vector2(100000f, 100000f));
            AssertPanelInsideScreen("화면 밖으로 끌었을 때");

            _window.FeedPointerForTests(false, grab + new Vector2(100000f, 100000f));
            Assert.IsFalse(_window.IsDraggingPanel, $"{LogPrefix} 버튼을 뗐는데 드래그가 계속됩니다.");

            // 다음 프레임: 실제 화면 크기로 다시 클램프되어도 창은 여전히 화면 안이다.
            yield return null;
            AssertPanelInsideScreen("실제 화면 크기로 복귀한 뒤");

            // ⑤ 닫았다 다시 열면 화면 중앙에서 시작한다(33-7-7의 "열면 중앙"은 유지).
            _window.Close("테스트 — 재개 확인");
            yield return null;
            _window.Open("테스트 — 재개 확인");
            yield return null;
            Assert.AreEqual(Vector2.zero, _window.PanelOffsetPoints,
                $"{LogPrefix} 다시 열었는데 화면 중앙이 아닙니다 — '열면 중앙' 규칙이 깨졌습니다.");

            Debug.Log($"{LogPrefix} ⑤⑥ 통과 — 타이틀바로 옮겨지고, 화면을 벗어나지 않고, 다시 열면 중앙입니다.");
        }

        /// <summary>창을 클램프 하한(320×320)까지 줄여 "옮길 여백이 있는 화면"을 만드는 배율.</summary>
        private static float ScaleFactorForSmallPanel()
            => Mathf.Max(0.01f, Mathf.Max(Screen.width, Screen.height) / 300f);

        private void AssertPanelInsideScreen(string phase)
        {
            Rect r = _window.PanelScreenRect;
            Assert.GreaterOrEqual(r.xMin, -0.5f, $"{LogPrefix} {phase}: 창이 화면 왼쪽으로 흘러나갔습니다({r}).");
            Assert.GreaterOrEqual(r.yMin, -0.5f, $"{LogPrefix} {phase}: 창이 화면 아래로 흘러나갔습니다({r}).");
            Assert.LessOrEqual(r.xMax, Screen.width + 0.5f, $"{LogPrefix} {phase}: 창이 화면 오른쪽으로 흘러나갔습니다({r}, 화면 폭 {Screen.width}).");
            Assert.LessOrEqual(r.yMax, Screen.height + 0.5f, $"{LogPrefix} {phase}: 창이 화면 위로 흘러나갔습니다({r}, 화면 높이 {Screen.height}).");
        }
    }
}
