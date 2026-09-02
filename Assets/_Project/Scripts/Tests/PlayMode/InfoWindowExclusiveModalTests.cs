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
    /// (★ 2026-09-02 후기: 그 탈출구는 2026-08-30에 구현됐다가 <b>사용자 지시로 다시 걷혔다</b> —
    ///  "사용자가 닫기전에는 안꺼져야함". 그래서 지금 탈출구는 다시 [✕] 하나이고, 이번에는 그것이 의도다.)
    ///
    /// ============================================================================
    /// 절대 조건으로 잠그는 것
    /// ============================================================================
    ///  ① <b>배타</b>: 어느 진입점으로 열든(부채꼴 / 단축키 경로) 창이 뜬 뒤에는 부채꼴도 팝오버도 없다.
    ///  ② <b>역방향</b>: 창이 열린 채 톱니를 누르면 창이 닫히고 부채꼴은 펼쳐지지 않는다.
    ///  ③ ★ <b>2026-09-02에 뒤집혔다</b> — 창 밖 클릭은 <b>닫지 않는다</b>(사용자 지시). 창 안/톱니 위도
    ///     마찬가지고, 닫는 마우스 경로는 <b>[✕] 하나</b>다. 그 하나가 살아 있음을 같은 테스트가 잰다.
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

        /// <summary>
        /// ★★ 2026-09-02 <c>test-engineer</c> — 여기 있던 <b>백업/복원</b>은 <b>오염 보존기</b>였다.
        /// 걷어냈다. 되살리지 마라. (<c>FullscreenPanelRetreatTests</c>가 같은 날 먼저 걷어낸 것과
        /// <b>같은 코드</b>가 8개 픽스처에 남아 있었다.)
        ///
        /// <para><b>원래 근거가 사라졌다.</b> 옛 코드는 <c>OneTimeSetUp</c>에서 저장 파일을 통째로 읽어
        /// 두고 <c>OneTimeTearDown</c>에서 <b>그대로 다시 썼다</b>. 정당화는 <i>"저장 파일이 실제 앱의
        /// 것과 같은 경로"</i>였는데, 그 전제는 2026-08-31에 <c>GlobalPlayModeTestIsolation</c>이
        /// 경로를 임시 폴더로 옮기면서 <b>거짓이 됐다</b>.</para>
        ///
        /// <para><b>그리고 뜻이 정반대로 뒤집혔다.</b> 격리된 폴더에서 <c>_hadFile == true</c>는
        /// "개발자 파일이 있다"가 아니라 <b>"앞선 픽스처가 남긴 오염이 있다"</b>는 뜻이다. 옛 TearDown은
        /// 그 오염을 <b>다시 써서 되살렸고</b>, 같은 코드가 여러 픽스처에 있었으므로 오염이 스위트
        /// 전체를 타고 <b>세탁</b>됐다 — 어떤 정리도 그 다음 픽스처의 복원 한 줄에 무효화됐다.
        /// 2026-09-02 실측이 그 결과다: <c>c1-play</c>가 씬 로드 430회 중 "없음 161 → 불러옴 278"로
        /// 도중에 뒤집혔고 <c>스틱메이트 Lv.127</c>이 로그에 505회 찍혔다.</para>
        ///
        /// <para><b>대신 가드를 남긴다.</b> 격리가 꺼진 채로 이 픽스처가 돌면 씬 로드가 개발자의 실제
        /// 저장 파일을 읽고 쓴다. 그때는 조용히 진행하지 않고 <b>즉시 실패</b>한다.</para>
        /// </summary>
        [OneTimeSetUp]
        public void RequireIsolatedSaveFileAndStartClean()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다. " +
                "이대로 진행하면 개발자의 실제 저장 파일을 읽고 씁니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        /// <summary>격리 폴더를 다음 픽스처에 <b>넘기지 않는다</b> — 이 픽스처가 만든 저장 파일을 지운다.
        /// 옛 <c>RestoreRealSaveFile</c>이 하던 "다시 쓰기"의 정확한 반대다(위 문단 참고).</summary>
        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
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

        // ==================== ③ 창 밖 클릭은 <b>닫지 않는다</b>(2026-09-02 사용자 지시) ====================

        /// <summary>
        /// ★★ <b>2026-09-02에 뒤집힌 단언</b>. 이 자리에는 원래 33-7-9 ③의 "창 밖 클릭 탈출구"를
        /// 잠그는 테스트가 있었다. 사용자 신고가 그 설계를 뒤집었다 — <i>"캐릭터창이나 다른 메뉴창들이
        /// 떠있을때 바탕화면을 클릭하면 꺼지는데 안꺼지고 사용자가 닫기전에는 안꺼져야함"</i>.
        ///
        /// <para>★ 그래서 <b>어디를 눌러도 닫히지 않는다</b>를 잠근다. 창 안이든, 톱니 위든, 화면
        /// 구석이든 마찬가지다. 그리고 마지막에 <b>[✕]로는 반드시 닫힌다</b>를 확인한다 —
        /// 이 네거티브 컨트롤이 없으면 "닫기 자체가 통째로 고장난" 상태에서도 이 테스트가 초록이다
        /// (그 상태의 사용자는 창을 영영 못 닫는다).</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OutsideClickDoesNotCloseWindowButTheCloseButtonStillDoes()
        {
            yield return LoadSceneAndResolve();

            _window.Open("테스트 — 창 밖 클릭");
            yield return null;
            yield return null;
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");

            // (a) 창 안의 빈 자리 클릭 — 예나 지금이나 닫지 않는다.
            _window.FeedClickForTests(_window.PanelScreenRect.center);
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창 안을 눌렀는데 창이 닫혔습니다.");
            yield return new WaitForSecondsRealtime(0.5f);   // 클릭 중복 억제(0.35초)를 넘긴다.

            // (b) 톱니 위 클릭도 이 창을 닫지 않는다(그 클릭은 톱니가 뗀 순간 자기가 처리한다).
            // 좁은 배치 화면(640×480)에서는 880 창이 우상단 톱니까지 덮어 "창 밖"과 "톱니 위"를 구분할 수
            // 없다. 그래서 이 한 프레임만 창을 최소 크기로 줄여 둘을 갈라 놓는다(주입 관례는 클래스 문서 참고).
            Assert.IsNotNull(ClampMethod, $"{LogPrefix} ClampPanelToScreen을 찾지 못했습니다 — 이름이 바뀌었습니다.");
            ClampMethod.Invoke(_window, new object[] { ScaleFactorForSmallPanel() });
            Assert.IsFalse(_window.PanelScreenRect.Contains(_gear.IconScreenCenter),
                $"{LogPrefix} 관측 전제 실패 — 창을 줄였는데도 톱니가 창 안에 있습니다.");
            _window.FeedClickForTests(_gear.IconScreenCenter);
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 톱니 위를 눌렀는데 창이 닫혔습니다.");
            yield return new WaitForSecondsRealtime(0.5f);

            // (c) ★ 핵심 — 창 밖(바탕화면 자리)을 눌러도 닫히지 않는다.
            Vector2 outside = new Vector2(4f, 4f);   // 화면 좌하단 구석 — 창(중앙 모달)과도 톱니(우상단)와도 겹치지 않는다.
            Assert.IsFalse(_window.PanelScreenRect.Contains(outside),
                $"{LogPrefix} 관측 전제 실패 — 창이 화면 좌하단 구석까지 덮고 있습니다.");
            _window.FeedClickForTests(outside);
            Assert.IsTrue(_window.IsOpen,
                $"{LogPrefix} 창 밖을 눌렀더니 창이 꺼졌습니다 — 2026-09-02 사용자 지시는 " +
                "\"사용자가 닫기전에는 안꺼져야함\"입니다.");
            yield return new WaitForSecondsRealtime(0.5f);

            // (d) ★ 네거티브 컨트롤 — 닫기 경로 자체는 살아 있다. 이게 죽어 있으면 위 (a)~(c)의
            //     "열려 있다"는 전부 공짜 초록이고, 사용자는 창을 영영 못 닫는다.
            Rect close = _window.CloseButtonScreenRect;
            Assert.Greater(close.width, 1f, $"{LogPrefix} [✕] 사각형이 비었습니다.");
            _window.FeedClickForTests(close.center);
            Assert.IsFalse(_window.IsOpen,
                $"{LogPrefix} [✕]를 눌렀는데 닫히지 않았습니다 — 바깥 클릭을 없앤 지금 이것이 " +
                "유일한 마우스 탈출구입니다.");

            Debug.Log($"{LogPrefix} ③ 통과 — 창 밖/톱니 위/창 안 어디를 눌러도 안 닫히고, [✕]만 닫습니다.");
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
