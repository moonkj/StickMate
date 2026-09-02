using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 2026-09-02 P0 — <b>펼친 포스트잇이 화면 6.994%의 클릭관통을 무기한 해제하고 있었다.</b>
    /// 근거: docs/UX_WIDGETS.md §3-2. 절대 불변 원칙 2(비침해) 직결.
    ///
    /// ============================================================================
    /// 이 파일이 재는 것 — <b>계약이 아니라 동작</b>
    /// ============================================================================
    /// EditMode의 <c>TodoPostItExpansionAuditTests</c>는 "상수의 정본이 하나인가 / 로그 사유가
    /// 갈리는가"를 소스로 잰다. 여기서는 <b>실제로 그렇게 되는가</b>를 씬에서 잰다:
    /// 15건을 펼쳐도 8행에서 멈추는가, 시간이 지나면 스스로 접히는가, 그리고 <b>쓰고 있는 동안에는
    /// 접히지 않는가</b>(양성 대조 — 이것이 없으면 "항상 접힌다"도 초록으로 통과한다).
    ///
    /// ============================================================================
    /// 시간 예산은 <b>벽시계(초)</b>다
    /// ============================================================================
    /// CLAUDE.md 확정 규칙: 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로 돌아서 프레임 수
    /// 기반 대기는 실제로 0.01초밖에 안 되는 경우가 있다. 아래 대기는 전부
    /// <c>Time.realtimeSinceStartup</c> 기준이다.
    ///
    /// 그리고 3분을 진짜로 기다리지 않는다 — <see cref="PopoverPanel.SetIdleAutoCloseSecondsForTests"/>로
    /// 임계를 낮춘다(포스트잇은 그 상수를 <b>참조</b>하므로 같은 손잡이 하나로 움직인다).
    /// </summary>
    public sealed class TodoPostItExpansionTests
    {
        private const string LogPrefix = "[포스트잇펼침-TEST]";

        /// <summary>소프트캡 경고를 보지 않으므로 넉넉히 잡는다(TodoPostItChromeTests와 같은 관례).</summary>
        private const int SoftCap = 99;

        /// <summary>테스트용 무입력 임계(초). 벽시계로 기다릴 수 있을 만큼 짧고, 폴링 주기(0.25초)의
        /// 몇 배는 되어야 "폴링이 한 번도 안 돌아 통과"가 생기지 않는다.</summary>
        private const float TestIdleSeconds = 1.0f;

        private TodoPostItWidget _widget;

        /// <summary>★ 무입력 임계는 <b>정적</b>이라 다른 PlayMode 테스트(PopoverIdleAutoCloseTests /
        /// SurfaceOutsideClickTests)가 낮춰 둔 값을 물려받을 수 있다. 실행 순서에 기대지 않도록
        /// 매 테스트를 기본값에서 시작한다 — 낮춘 값이 새면 상한 테스트가 검사 도중 접혀 버린다.</summary>
        [UnitySetUp]
        public IEnumerator Prepare()
        {
            PopoverPanel.ResetIdleAutoCloseSecondsForTests();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_widget != null) _widget.ClearIdleCursorForTests();
            PopoverPanel.ResetIdleAutoCloseSecondsForTests();
            TodoListModel.ResetForTesting();
            _widget = null;
            yield return null;
        }

        /// <summary>할 일 <paramref name="count"/>건을 넣고 카드가 보이는 상태를 만든다.</summary>
        private IEnumerator ShowCardWith(int count)
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _widget = Object.FindFirstObjectByType<TodoPostItWidget>();
            Assert.IsNotNull(_widget, $"{LogPrefix} 씬에 TodoPostItWidget이 없습니다.");

            TodoListModel.ResetForTesting();
            for (int i = 0; i < count; i++) TodoListModel.Add($"펼침 회귀 확인용 {i + 1}", SoftCap);
            Assert.AreEqual(count, TodoListModel.UncompletedCount,
                $"{LogPrefix} 테스트용 할 일 {count}건이 목록에 들어가지 않았습니다.");
            yield return null;
            yield return null;

            Assert.IsTrue(_widget.IsCardVisible,
                $"{LogPrefix} 할 일이 {count}건인데 카드가 보이지 않습니다 — 관측 전제가 성립하지 않습니다.");
        }

        private static RectTransform FindPanel(TodoPostItWidget widget)
        {
            var all = widget.GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform rt in all)
            {
                if (rt.name == "PostItPanel") return rt;
            }
            return null;
        }

        /// <summary>실제 [+N개 더보기] 버튼을 누른다 — 테스트용 우회로를 만들지 않는다.</summary>
        private static Button FindMoreButton(TodoPostItWidget widget)
        {
            var all = widget.GetComponentsInChildren<Button>(true);
            foreach (Button b in all)
            {
                if (b.name == "MoreButton") return b;
            }
            return null;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
        }

        // ==================================================================
        // (1) 행 상한
        // ==================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ExpandedCardStopsAtRowCapEvenAtSoftCapItemCount()
        {
            yield return ShowCardWith(15);   // docs/UX_WIDGETS.md가 잰 최악(소프트캡).

            int collapsedRows = _widget.VisibleRowCount;
            Assert.Greater(collapsedRows, 0, $"{LogPrefix} 접힘 상태에서 행이 하나도 없습니다.");
            Assert.Less(collapsedRows, 15, $"{LogPrefix} 접힘 상태부터 15줄을 그리고 있습니다.");

            RectTransform panel = FindPanel(_widget);
            Assert.IsNotNull(panel, $"{LogPrefix} PostItPanel을 찾지 못했습니다 — 이름이 바뀌었습니다.");
            float collapsedHeight = panel.rect.height;

            Button more = FindMoreButton(_widget);
            Assert.IsNotNull(more, $"{LogPrefix} MoreButton을 찾지 못했습니다 — 이름이 바뀌었습니다.");
            more.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsTrue(_widget.IsExpandedForTests, $"{LogPrefix} [+N개 더보기]를 눌렀는데 펼쳐지지 않았습니다.");
            Assert.AreEqual(TodoPostItWidget.ExpandedMaxRows, _widget.VisibleRowCount,
                $"{LogPrefix} 15건을 펼쳤을 때 행 수가 상한({TodoPostItWidget.ExpandedMaxRows})이 아닙니다. " +
                "상한이 없으면 카드가 220×472 = 103,840pt²(화면 6.994%)까지 자라고 그 전부가 " +
                "클릭관통 해제 면적이 됩니다(원칙 2).");

            // 카드가 실제로 그만큼만 자랐는지 화면 사각형으로 확인한다 — 행 수만 맞고 패널만 큰
            // 경우를 잡아야 한다(차단막은 패널 사각형을 덮지 행을 덮지 않는다).
            //
            // ★ 기대 높이를 <b>수식으로 적지 않는다</b>. 그러면 프로덕션 레이아웃 식을 테스트에
            //   베끼는 '설계 거울'이 되고, 둘이 갈라지는 순간 서로 다른 판정을 낸다.
            //   대신 <b>측정 대 측정</b>으로 잰다: 항목이 딱 상한만큼일 때의 높이와 같아야 한다.
            float expandedHeight = panel.rect.height;
            Assert.Greater(expandedHeight, collapsedHeight,
                $"{LogPrefix} 펼쳤는데 카드가 커지지 않았습니다 — 관측이 성립하지 않습니다.");

            while (TodoListModel.UncompletedCount > TodoPostItWidget.ExpandedMaxRows)
            {
                TodoListModel.Remove(TodoListModel.ActiveItems[TodoListModel.ActiveItems.Count - 1].Id);
            }
            yield return null;
            yield return null;
            Assert.IsTrue(_widget.IsExpandedForTests,
                $"{LogPrefix} 항목을 지우는 사이에 펼침이 풀렸습니다 — 이 비교의 전제가 깨졌습니다.");

            float exactCapHeight = panel.rect.height;
            Assert.AreEqual(exactCapHeight, expandedHeight, 0.5f,
                $"{LogPrefix} 15건을 펼친 높이 {expandedHeight:F1}pt가 " +
                $"{TodoPostItWidget.ExpandedMaxRows}건을 펼친 높이 {exactCapHeight:F1}pt와 다릅니다 — " +
                "행 수만 잘라 놓고 패널은 그대로 자라고 있습니다. 차단막은 행이 아니라 패널 사각형을 덮습니다.");

            Assert.IsTrue(_widget.IsClickBlockerEnabled,
                $"{LogPrefix} 카드가 보이는데 차단막이 꺼져 있습니다 — 클릭이 밑으로 샙니다.");
        }

        // ==================================================================
        // (2) 무입력 자동 접힘
        // ==================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ExpandedCardCollapsesItselfAfterIdleTimeout()
        {
            PopoverPanel.SetIdleAutoCloseSecondsForTests(TestIdleSeconds);
            Assert.AreEqual(TestIdleSeconds, TodoPostItWidget.IdleAutoCollapseSeconds, 0.001f,
                $"{LogPrefix} 임계를 낮췄는데 포스트잇이 그 값을 보지 않습니다 — 상수를 복사한 것입니다.");

            yield return ShowCardWith(15);

            Button more = FindMoreButton(_widget);
            more.onClick.Invoke();
            yield return null;
            Assert.IsTrue(_widget.IsExpandedForTests, $"{LogPrefix} 펼침 전제가 성립하지 않았습니다.");

            // 커서를 카드에서 멀리 떨어뜨린다 = "이 카드를 쓰고 있지 않다".
            // 실제 OS 커서는 붙잡아 둘 수 없으므로 주입한다(PopoverPanel과 같은 관례).
            _widget.FeedIdleCursorForTests(new Vector2(-10000f, -10000f));

            // 벽시계로 임계의 3배를 기다린다 — 폴링(0.25초)이 여러 번 돌 충분한 여유.
            yield return WaitSeconds(TestIdleSeconds * 3f);

            Assert.IsFalse(_widget.IsExpandedForTests,
                $"{LogPrefix} 무입력 {TestIdleSeconds}초 임계의 3배를 기다렸는데 펼침이 남아 있습니다 " +
                $"(누적 무입력 {_widget.IdleSecondsForTests:F2}초). 펼쳐 두고 잊으면 그 면적의 " +
                "클릭관통이 무기한 해제된 채 남습니다(원칙 2).");

            // ★ 접힌 것은 <b>펼침</b>이지 카드가 아니다. 할 일이 남아 있으면 카드는 계속 있어야 한다
            //   — 상시 HUD를 시간으로 지우면 그게 사고다.
            Assert.IsTrue(_widget.IsCardVisible,
                $"{LogPrefix} 자동 접힘이 카드까지 숨겼습니다 — 접혀야 하는 것은 펼침뿐입니다.");
            Assert.Greater(_widget.VisibleRowCount, 0,
                $"{LogPrefix} 접힌 뒤 행이 하나도 남지 않았습니다.");
            Assert.Less(_widget.VisibleRowCount, TodoPostItWidget.ExpandedMaxRows,
                $"{LogPrefix} 접혔다는데 행 수가 펼침 상한 그대로입니다.");
        }

        // ==================================================================
        // (3) ★ 양성 대조 — 쓰고 있는 동안에는 접히지 않는다
        // ==================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ExpandedCardStaysWhileCursorKeepsMovingOnTheCard()
        {
            PopoverPanel.SetIdleAutoCloseSecondsForTests(TestIdleSeconds);

            yield return ShowCardWith(15);

            RectTransform panel = FindPanel(_widget);
            Button more = FindMoreButton(_widget);
            more.onClick.Invoke();
            yield return null;
            Assert.IsTrue(_widget.IsExpandedForTests, $"{LogPrefix} 펼침 전제가 성립하지 않았습니다.");

            // 카드 한가운데 근처에서 커서를 계속 흔든다 = "지금 이걸 보고 있다".
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            Vector2 center = (corners[0] + corners[2]) * 0.5f;

            // ★ 좌우 두 점을 번갈아 찍으면 안 된다 — 무입력 판정은 0.25초마다만 표본을 뜨므로
            //   연속 두 표본이 같은 점에 걸리면 "안 움직였다"로 읽혀 거짓 빨강이 난다.
            //   시간의 함수인 톱니(주기 1초, 폭 40pt)로 흔들어 어느 표본 쌍이든 최소 10pt는
            //   벌어지게 한다. 폭 40pt는 카드 폭 220pt 안이라 커서가 카드를 벗어나지 않는다.
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < TestIdleSeconds * 3f)
            {
                float phase = Mathf.Repeat((Time.realtimeSinceStartup - t0) * 40f, 40f);
                _widget.FeedIdleCursorForTests(center + new Vector2(phase - 20f, 0f));
                yield return null;
            }

            Assert.IsTrue(_widget.IsExpandedForTests,
                $"{LogPrefix} 카드 위에서 커서를 계속 움직였는데도 접혔습니다 " +
                $"(누적 무입력 {_widget.IdleSecondsForTests:F2}초). 읽고 있는 것을 시간으로 접으면 " +
                "그건 편의가 아니라 사고입니다 — 이 단언이 없으면 '항상 접힌다'도 초록으로 통과합니다.");
            Assert.AreEqual(TodoPostItWidget.ExpandedMaxRows, _widget.VisibleRowCount,
                $"{LogPrefix} 펼침이 유지됐다는데 행 수가 상한이 아닙니다.");
        }

        // ==================================================================
        // (4) 17절 "빈 상태 예외"를 이 라운드가 깨지 않았는가
        // ==================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator EmptyListStillHidesTheWholeCard()
        {
            yield return ShowCardWith(1);

            // ★ ResetForTesting()으로 비우면 안 된다 — 그 메서드는 <b>TodoListChanged를 쏘지 않는다</b>
            //   (Core/TodoListModel.cs). 위젯이 갱신될 계기가 없으니 카드가 그대로 남고, 그러면 이
            //   테스트는 제품이 멀쩡한데도 빨개진다. 실제로 음성 대조 1회차에서 그렇게 빨개졌다.
            //   사용자가 마지막 할 일을 지우는 진짜 경로(Remove)로 비운다.
            while (TodoListModel.ActiveItems.Count > 0)
            {
                TodoListModel.Remove(TodoListModel.ActiveItems[0].Id);
            }
            Assert.AreEqual(0, TodoListModel.UncompletedCount,
                $"{LogPrefix} 목록을 비우지 못했습니다 — 관측 전제가 성립하지 않습니다.");
            yield return null;
            yield return null;

            Assert.IsFalse(_widget.IsCardVisible,
                $"{LogPrefix} 미완료 0건인데 카드가 남아 있습니다 — UX_FLOW 17절 '빈 상태 예외'가 깨졌습니다.");
            Assert.IsFalse(_widget.IsClickBlockerEnabled,
                $"{LogPrefix} 카드가 없는데 차단막이 켜져 있습니다 — '안 보이는데 클릭만 먹는' 최악의 형태입니다.");
        }
    }
}
