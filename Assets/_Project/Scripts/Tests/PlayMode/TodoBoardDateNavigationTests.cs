using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 실측 회귀 — <b>[오늘 할일] 날짜 축이 실제 씬에서 동작한다</b>
    /// (docs/UX_WIDGETS.md <b>R6</b> · persona-stress <b>R-5</b>/<b>R-7</b> · 클릭 충돌 P0).
    ///
    /// ============================================================================
    /// EditMode 감사로는 못 재는 것만 여기서 잰다
    /// ============================================================================
    /// <c>TodoBoardDateAxisLayoutTests</c>는 <b>상수와 순수 함수</b>를 잰다. 그것으로는
    /// «달력 칸을 실제로 누르면 날짜가 골라지는가»를 알 수 없다 — persona-stress R-5가 지목한
    /// 결함이 정확히 그 자리다: 나중에 만든 버튼은 <c>PopoverPanel._controlRects</c>(Awake 1회
    /// 스냅샷) 밖이라, 누르면 <b>날짜가 안 골라지고 창이 끌린다</b>. 그 판정은 실제
    /// <c>RectTransform</c>과 실제 클릭 경로가 있어야 성립한다.
    /// </summary>
    public sealed class TodoBoardDateNavigationTests
    {
        private const string LogPrefix = "[할일날짜축-TEST]";

        private TodoBoardPopover _popover;

        [OneTimeSetUp]
        public void RequireIsolatedSaveFile()
        {
            // 이 픽스처는 제품 경로(AddForTests → AddFromInput → CharacterSaveStore.Save)를 그대로 탄다.
            // 격리가 꺼진 채 돌면 개발자의 진짜 저장 파일에 테스트 항목이 남는다(절대 불변 원칙 3).
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다.");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
            TodoListModel.ResetForTesting();
        }

        private IEnumerator LoadSceneAndOpen()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _popover = Object.FindFirstObjectByType<TodoBoardPopover>();
            Assert.IsNotNull(_popover, $"{LogPrefix} 씬에 TodoBoardPopover가 없습니다.");

            TodoListModel.ResetForTesting();
            UiClickArbiter.ResetForTests();

            var anchor = new Rect(Screen.width * 0.5f - 22f, Screen.height * 0.5f - 22f, 44f, 44f);
            _popover.Open(anchor, "날짜 축 테스트");
            Assert.IsTrue(_popover.IsOpen, $"{LogPrefix} 팝오버가 열리지 않았습니다.");
            yield return null;
        }

        /// <summary>
        /// 날짜 축을 <b>반드시</b> 세운다 — 건너뛰지 않는다.
        ///
        /// <para>처음에는 <c>DayIndex == 0</c>이면 건너뛰게 짰다가 되돌렸다. 이 저장소의
        /// <c>TestClaimExpiryAuditTests</c>가 그 자리를 정확히 지목한다 — <b>건너뛴 테스트는 잊힌다</b>.
        /// 그리고 여기서 «오늘»이 안 서는 것은 환경 차이가 아니라 <b>배선이 끊긴 사실</b>이므로,
        /// 건너뛸 것이 아니라 빨개져야 한다.</para>
        ///
        /// <para><c>CurrencyModel.SetDayIndexForTesting</c>은 <c>internal</c>이라 PlayMode 어셈블리에서
        /// 부를 수 없다(<c>InternalsVisibleTo</c>는 EditMode 하나뿐). 그래서 <b>제품이 켜질 때 타는
        /// 바로 그 경로</b>(<c>TickDayRollover</c> — <c>CurrencyDayRolloverTicker.CheckNow</c>가 저장
        /// 로드 직후 부르는 것)를 그대로 부른다. 이미 서 있으면 아무 일도 안 한다.</para>
        /// </summary>
        private static int RequireDayAxis()
        {
            if (CurrencyModel.DayIndex == TodoItem.UnknownPlannedDay)
            {
                CurrencyModel.TickDayRollover(Time.realtimeSinceStartupAsDouble);
            }

            Assert.AreNotEqual(TodoItem.UnknownPlannedDay, CurrencyModel.DayIndex,
                $"{LogPrefix} 날짜 축(CurrencyModel.DayIndex)을 세우지 못했습니다 — " +
                "이 앱에서 «오늘»을 아는 창구가 그것 하나이므로, 여기가 0이면 날짜별 화면 전체가 " +
                "동작하지 않습니다(건너뛰지 않고 실패로 남깁니다).");
            return CurrencyModel.DayIndex;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_popover != null && _popover.IsOpen) _popover.Close("테스트 정리");
            _popover = null;
            TodoListModel.ResetForTesting();
            UiClickArbiter.ResetForTests();
            yield return null;
        }

        // ====================================================================
        // ① 크롬 — 줄어든 창에서도 탭 칩과 [✕]가 안에 남는다
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TabChipsAndCloseChipStayInsideThePanelEvenWhenClamped()
        {
            yield return LoadSceneAndOpen();

            Rect panel = _popover.PanelScreenRect;
            Assert.Greater(panel.width, 1f, $"{LogPrefix} 패널 사각형이 비었습니다.");

            for (int i = 0; i < 2; i++)
            {
                Rect chip = _popover.TabChipScreenRect(i);
                Assert.Greater(chip.width, 1f, $"{LogPrefix} 탭 칩 {i} 사각형이 비었습니다.");
                Assert.GreaterOrEqual(chip.xMin, panel.xMin - 0.5f,
                    $"{LogPrefix} 탭 칩 {i}의 왼끝이 패널 밖입니다 — 화면이 좁아 창이 줄면 칩만 " +
                    "창 밖에 남던 결함입니다(설계 폭에서 역산한 좌표에 걸려 있었다).");
                Assert.LessOrEqual(chip.xMax, panel.xMax + 0.5f,
                    $"{LogPrefix} 탭 칩 {i}이 패널 오른쪽으로 흘러나갔습니다.");
                Assert.LessOrEqual(chip.yMax, panel.yMax + 0.5f,
                    $"{LogPrefix} 탭 칩 {i}이 패널 위로 흘러나갔습니다.");
            }

            Rect close = _popover.CloseButtonScreenRectForTests;
            Rect tab1 = _popover.TabChipScreenRect(1);
            Assert.LessOrEqual(tab1.xMax, close.xMin + 0.5f,
                $"{LogPrefix} 탭 칩이 [✕] 위를 덮습니다(칩 오른끝 {tab1.xMax:F1} vs [✕] 왼끝 {close.xMin:F1}) — " +
                "[✕]는 이 창을 닫는 유일한 마우스 경로입니다.");

            // ★ 차단막 = 보이는 면적. 한 픽셀도 넓지 않다(비침해 원칙 2 · persona-stress R-4).
            Assert.IsTrue(_popover.IsClickBlockerEnabled, $"{LogPrefix} 차단막이 꺼져 있습니다.");
            Vector2 applied = _popover.AppliedPanelSizePoints;
            Assert.Greater(applied.x, 0f, $"{LogPrefix} 적용된 패널 크기가 비었습니다.");
            Assert.LessOrEqual(applied.x, 500f + 0.001f,
                $"{LogPrefix} 적용 폭 {applied.x}이 설계 폭 500을 넘었습니다(클램프는 줄이기만 합니다).");
            Assert.LessOrEqual(applied.y, 512f + 0.001f,
                $"{LogPrefix} 적용 높이 {applied.y}이 설계 높이 512를 넘었습니다.");

            // Content 상자 높이가 상수와 일치하는가 — 기반 클래스 크롬이 바뀌면 여기서 걸린다.
            float expectedContent = applied.y - (512f - TodoBoardPopover.ContentHeightPoints);
            Assert.AreEqual(expectedContent, _popover.ContentHeightForTests, 1f,
                $"{LogPrefix} Content 실측 높이 {_popover.ContentHeightForTests:F1}가 상수에서 나온 " +
                $"{expectedContent:F1}과 다릅니다 — PopoverPanel 크롬 치수가 바뀌었는데 " +
                "TodoBoardPopover.ChromeVerticalPoints가 따라가지 않았습니다.");

            Assert.AreEqual(TodoBoardPopover.ResolveVisibleRows(applied.y), _popover.VisibleRowBudgetForTests,
                $"{LogPrefix} 실제 행 예산이 순수 함수의 답과 다릅니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 적용 {applied.x:F0}×{applied.y:F0}pt, " +
                $"행 예산 {_popover.VisibleRowBudgetForTests}, Content {_popover.ContentHeightForTests:F0}pt.");
        }

        // ====================================================================
        // ② 달력 — 칸을 누르면 <b>날짜가 골라진다</b>(창이 끌리지 않는다)
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClickingACalendarCellPicksTheDayInsteadOfDraggingTheWindow()
        {
            yield return LoadSceneAndOpen();

            int today = RequireDayAxis();
            _popover.Open(new Rect(Screen.width * 0.5f - 22f, Screen.height * 0.5f - 22f, 44f, 44f),
                "날짜 축 재확인");   // 축이 방금 섰다면 화면을 그 값으로 다시 그린다.
            yield return null;
            Assert.AreEqual(today, _popover.TodayIndexForTests,
                $"{LogPrefix} 창이 아는 «오늘»이 CurrencyModel.DayIndex와 다릅니다 — 두 번째 시계가 생겼습니다(UW-6-2).");

            // ★ 먼저 «오늘이 아닌 날»로 옮겨 둔다. 이걸 안 하면 아래 «오늘이 선택됐다»는 단언이
            //   «원래 그 값이었다»로도 통과한다(이 저장소의 거짓 통과 형태 그대로).
            _popover.FeedClickForTests(_popover.PrevDayScreenRect.center);
            yield return null;
            Assume.That(_popover.SelectedDayIndexForTests, Is.Not.EqualTo(today),
                $"{LogPrefix} [‹]를 눌렀는데 날짜가 안 옮겨졌습니다 — 아래 판정의 전제가 성립하지 않습니다.");
            Assert.IsFalse(_popover.FollowsTodayForTests,
                $"{LogPrefix} 특정 날짜를 골랐는데도 «오늘 추종»이 켜져 있습니다 — 자정에 " +
                "읽고 있던 화면을 빼앗깁니다(UW-6-2).");

            // ---- 날짜 라벨이 곧 달력 토글이다(탭을 늘리지 않는다 — UW-6-1) ----
            Assert.IsFalse(_popover.IsCalendarPage, $"{LogPrefix} 처음부터 달력이 떠 있습니다.");
            _popover.FeedClickForTests(_popover.DateToggleScreenRect.center);
            yield return null;
            Assert.IsTrue(_popover.IsCalendarPage,
                $"{LogPrefix} 날짜 라벨을 눌렀는데 달력이 열리지 않았습니다.");
            Assert.AreEqual(0, _popover.ActiveTab,
                $"{LogPrefix} 달력이 <b>새 탭</b>이 됐습니다 — 같은 탭 안의 페이지 전환이어야 합니다(UW-6-1).");
            Assert.IsFalse(_popover.IsDraggingWindow,
                $"{LogPrefix} 날짜 라벨 클릭이 창 드래그로 먹혔습니다.");

            // ---- 오늘 칸을 찾아 누른다 ----
            int target = -1;
            for (int i = 0; i < TodoBoardPopover.CalendarCellCountForTests; i++)
            {
                if (!_popover.CalendarCellSelectable(i)) continue;
                if (_popover.CalendarCellDayIndex(i) != today) continue;
                target = i;
                break;
            }
            Assert.GreaterOrEqual(target, 0,
                $"{LogPrefix} 이번 달 달력에서 «오늘»(일자 #{today}) 칸을 찾지 못했습니다 — " +
                "달이 어긋났거나 정의역 계산이 오늘을 빼먹었습니다.");

            _popover.FeedClickForTests(_popover.CalendarCellScreenRect(target).center);
            yield return null;

            Assert.IsFalse(_popover.IsDraggingWindow,
                $"{LogPrefix} 달력 칸 클릭이 <b>창 드래그</b>로 먹혔습니다 — persona-stress R-5가 " +
                "지목한 자리입니다(나중에 만든 버튼이 _controlRects 스냅샷 밖).");
            Assert.IsFalse(_popover.IsCalendarPage,
                $"{LogPrefix} 칸을 눌렀는데 일별 페이지로 돌아오지 않았습니다.");
            Assert.AreEqual(today, _popover.SelectedDayIndexForTests,
                $"{LogPrefix} 누른 칸의 날짜가 선택되지 않았습니다(직전에 다른 날짜에 있었습니다).");
            Assert.IsTrue(_popover.FollowsTodayForTests,
                $"{LogPrefix} 달력에서 «오늘»을 골랐는데 오늘 추종이 다시 켜지지 않았습니다.");

            // ★ 드래그 제외 목록이 <b>비어 있지 않은가</b>(비면 창 전체가 손잡이가 되어 아무 버튼도 안 눌린다).
            Assert.Greater(_popover.DragExcludedControlCountForTests, 50,
                $"{LogPrefix} 드래그 제외 컨트롤이 {_popover.DragExcludedControlCountForTests}개뿐입니다 — " +
                "달력 42칸이 스냅샷에 안 들어갔습니다(지연 생성으로 바뀌었는지 확인하세요).");

            Debug.Log($"{LogPrefix} ② 통과 — 달력 칸 #{target}(일자 {today}) 클릭이 날짜 선택으로 처리됨, " +
                $"드래그 제외 {_popover.DragExcludedControlCountForTests}개.");
        }

        // ====================================================================
        // ③ 날짜 이동 — 탐색만으로는 <b>저장이 걸리지 않는다</b>(persona-stress R-10)
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator BrowsingDaysNeverMarksTheModelDirty()
        {
            yield return LoadSceneAndOpen();

            int today = RequireDayAxis();

            // AddForTests는 제품 경로 그대로 CharacterSaveStore.Save()까지 탄다 — 그 안에서
            // TodoListModel.MarkSaved()가 불려 IsDirty가 내려간다(PlayMode 어셈블리에는
            // InternalsVisibleTo가 없어 MarkSaved를 직접 부를 수 없다. 이 우회는 «제품이 실제로
            // 하는 일»을 그대로 쓰는 것이라 오히려 더 정확한 전제다).
            _popover.AddForTests("탐색 회귀 확인용");
            yield return null;
            Assert.IsFalse(TodoListModel.IsDirty,
                $"{LogPrefix} 전제 — 추가 직후 저장이 돌아 깨끗해야 합니다(추가 경로가 Save를 안 부르나요?).");

            _popover.FeedClickForTests(_popover.PrevDayScreenRect.center);
            yield return null;
            _popover.FeedClickForTests(_popover.NextDayScreenRect.center);
            yield return null;
            _popover.FeedClickForTests(_popover.TomorrowChipScreenRect.center);
            yield return null;
            _popover.FeedClickForTests(_popover.DateToggleScreenRect.center);
            yield return null;

            Assert.IsFalse(TodoListModel.IsDirty,
                $"{LogPrefix} 날짜를 넘겨 보기만 했는데 «저장할 것이 생겼다»가 됐습니다 — " +
                "주기 저장이 파일을 쓰게 되고 같은 파일을 쓰는 다른 인스턴스와의 충돌 창이 넓어집니다(R-10).");

            // ---- 양성 대조: 내용을 바꾸면 실제로 더러워진다(위 단언이 «측정기가 죽어서» 통과한 것이 아니다) ----
            TodoListModel.Add("측정기 확인용", 15, today);
            Assert.IsTrue(TodoListModel.IsDirty,
                $"{LogPrefix} 내용을 바꿨는데도 IsDirty가 서지 않습니다 — 위 부재 단언이 무효입니다.");
        }

        // ====================================================================
        // ④ 완료 행 — <b>알파로 흐리지 않는다</b>
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CompletedRowIsDimmedByColorTokenAndNeverByAlpha()
        {
            yield return LoadSceneAndOpen();

            _popover.AddForTests("완료 색 회귀 확인용");
            yield return null;
            Assert.AreEqual(1, _popover.VisibleRowCount, $"{LogPrefix} 항목이 목록에 들어가지 않았습니다.");

            Color before = _popover.RowLabelColor(0);
            Assert.AreEqual(1f, before.a, 0.001f, $"{LogPrefix} 미완료 글자부터 알파가 1이 아닙니다.");

            _popover.FeedClickForTests(_popover.RowScreenRect(0).center);
            yield return null;

            Assert.IsTrue(_popover.RowStrikeActive(0), $"{LogPrefix} 완료로 바뀌지 않았습니다.");
            Color after = _popover.RowLabelColor(0);

            // ★ 이 오버레이에서 α<1은 «흐린 글자»가 아니라 <b>구멍</b>이다(UiChrome 알파 채널의 법칙 (2)):
            //   α0.5를 α1 위에 그리면 그 화소의 창 알파가 0.5² + 1×0.5 = 0.75로 내려간다 = 데스크톱 25% 비침.
            Assert.AreEqual(1f, after.a, 0.001f,
                $"{LogPrefix} 완료 글자의 알파가 {after.a:F2}입니다 — 그 자리만 유저의 바탕화면이 " +
                $"{(1f - (after.a * after.a + (1f - after.a))) * 100f:F0}% 비칩니다. " +
                "위계는 색 토큰이 져야 합니다(TodoPostItWidget이 같은 자리에서 이미 그렇게 합니다).");

            Assert.AreNotEqual(before, after,
                $"{LogPrefix} 완료 표시가 글자 색을 전혀 안 바꿉니다 — 위계가 사라졌습니다.");
            Assert.AreEqual(UiChrome.TextTertiary, after,
                $"{LogPrefix} 완료 글자 색이 TextTertiary가 아닙니다 — 포스트잇(정본)과 갈라졌습니다.");
        }

        // ====================================================================
        // ⑤ 클릭 소유권 — 팝오버 위 클릭은 상시 카드가 먹지 않는다 (P0 신고)
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PostItNeverEatsAClickThatLandedOnTheOpenPopover()
        {
            yield return LoadSceneAndOpen();
            yield return null;   // Update가 한 번 돌아 소유권이 등록되게 한다.

            Assert.Greater(UiClickArbiter.PublishedSurfaceCountForTests, 0,
                $"{LogPrefix} 열린 팝오버가 클릭 소유권을 등록하지 않았습니다 — " +
                "등록이 0이면 이 기구는 아무것도 막지 않습니다(공허한 통과).");

            Rect add = _popover.AddButtonScreenRect;
            Assert.Greater(add.width, 1f, $"{LogPrefix} [추가] 사각형이 비었습니다.");

            // 신고 시나리오: 그 좌표는 <b>포스트잇 체크박스와도 겹칠 수 있는</b> 자리다.
            Assert.IsFalse(UiClickArbiter.TryClaimClick(TodoPostItWidget.ClickArbiterSurfaceId,
                    TodoPostItWidget.ClickArbiterLayer, add.center),
                $"{LogPrefix} [추가] 자리의 클릭을 상시 포스트잇이 먹을 수 있는 상태입니다 — " +
                "신고된 «항목 자동 완료 + 300동전 소진»이 그대로 재현됩니다.");

            // ---- 팝오버 밖은 여전히 카드의 것이다(과잉 차단이 아니다) ----
            var outside = new Vector2(_popover.PanelScreenRect.xMax + 50f, _popover.PanelScreenRect.yMax + 50f);
            Assert.IsFalse(_popover.PanelScreenRect.Contains(outside), $"{LogPrefix} 대조 표본이 패널 안입니다.");
            Assert.IsTrue(UiClickArbiter.TryClaimClick(TodoPostItWidget.ClickArbiterSurfaceId,
                    TodoPostItWidget.ClickArbiterLayer, outside),
                $"{LogPrefix} 팝오버 밖의 클릭까지 막혔습니다 — 상시 카드가 통째로 죽었습니다.");

            // ---- 층 모형이 실제 z-순서와 같은가(모형이 화면과 반대면 위 판정이 거짓말이다) ----
            var postIt = Object.FindFirstObjectByType<TodoPostItWidget>();
            if (postIt != null)
            {
                Assert.Greater(_popover.CanvasSortingOrderForTests, postIt.CanvasSortingOrderForTests,
                    $"{LogPrefix} 팝오버 캔버스({_popover.CanvasSortingOrderForTests})가 포스트잇" +
                    $"({postIt.CanvasSortingOrderForTests})보다 아래입니다 — UiClickArbiter의 층 모형이 " +
                    "화면에서 실제로 보이는 것과 반대입니다.");
                Assert.Greater(TodoBoardPopover.ClickArbiterLayer, TodoPostItWidget.ClickArbiterLayer,
                    $"{LogPrefix} 층 모형과 실제 z-순서가 반대 방향입니다.");
            }

            // ---- 닫으면 그 자리를 돌려준다 ----
            _popover.Close("소유권 테스트 정리");
            yield return null;
            yield return null;
            UiClickArbiter.ResetForTests();
            Assert.IsTrue(UiClickArbiter.TryClaimClick(TodoPostItWidget.ClickArbiterSurfaceId,
                    TodoPostItWidget.ClickArbiterLayer, add.center),
                $"{LogPrefix} 팝오버를 닫은 뒤에도 그 자리가 클릭을 삼킵니다.");
        }

        // ====================================================================
        // ⑥ 상시 카드 — 「내일」은 새어나가지 않는다 (persona-stress R-7)
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PostItShowsTodayAndOverdueButNeverTomorrow()
        {
            yield return LoadSceneAndOpen();
            _popover.Close("포스트잇 필터 테스트 준비");
            yield return null;

            var postIt = Object.FindFirstObjectByType<TodoPostItWidget>();
            Assert.IsNotNull(postIt, $"{LogPrefix} 씬에 TodoPostItWidget이 없습니다.");

            int today = RequireDayAxis();

            TodoListModel.ResetForTesting();
            TodoListModel.Add("내일 할 일", 15, today + 1);
            yield return null;
            yield return null;

            Assert.IsFalse(postIt.IsCardVisible,
                $"{LogPrefix} 「내일」 항목 하나만 있는데 상시 카드가 떴습니다 — 사용자가 아무것도 " +
                "열지 않았는데 내일 것이 화면에 나오고, 그 사각형만큼 클릭관통이 풀립니다(R-7).");
            Assert.IsFalse(postIt.IsClickBlockerEnabled,
                $"{LogPrefix} 카드는 안 보이는데 차단막이 켜져 있습니다.");

            // ---- 양성 대조: 오늘 것을 넣으면 카드가 뜬다(필터가 «전부 끄기»가 아니다) ----
            TodoListModel.Add("오늘 할 일", 15, today);
            yield return null;
            yield return null;
            Assert.IsTrue(postIt.IsCardVisible,
                $"{LogPrefix} 오늘 항목을 넣었는데도 카드가 안 뜹니다 — 필터가 전부 걸러 버립니다.");
            Assert.AreEqual(1, postIt.VisibleRowCount,
                $"{LogPrefix} 카드에 {postIt.VisibleRowCount}행이 떴습니다 — 오늘 것 1행이어야 하고, " +
                "2행이면 내일 것이 함께 새어 나온 것입니다.");
        }
    }
}
