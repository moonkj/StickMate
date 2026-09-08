using System;
using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ [오늘 할일] <b>날짜 축</b> 모델 계약 — 2026-09-08, docs/UX_WIDGETS.md R6-6(UW-6-4) ·
    /// 리더 판정 R6-13 ③ / persona-stress R-10.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 두 계약
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>「0 = 날짜 미상」</b>. 0은 1970-01-01이 <b>아니다</b>. 미상 항목은 어느 날짜 조회에도
    ///    잡히지 않고 달력 어느 칸에도 세지 않는다. UW-6-4의 경고 원문:
    ///    <i>"0을 1970-01-01로 읽는 순간 이 설계는 전부 무효다."</i> v12 세이브의 <b>모든</b> 항목이
    ///    0으로 올라오므로, 이 계약이 깨지면 기존 사용자의 할일 전부가 그 칸에 쌓이고
    ///    <b>테스트는 초록</b>이다(조용히 썩는 자리).</item>
    ///  <item><b>「탐색은 저장하지 않는다」</b>(persona-stress R-10). 날짜를 넘기는 것만으로
    ///    <see cref="TodoListModel.IsDirty"/>가 서거나 <c>TodoListChanged</c>가 나가면, 같은 세이브
    ///    파일을 공유하는 여러 인스턴스의 충돌 창이 넓어진다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    ///  · <c>TallyForDay</c>/<c>AppendItemsForDay</c>의 «미상이면 빈 값» 가드를 지우면
    ///    <c>미상_항목은_어느_날짜에도…</c>가 실패한다.
    ///  · 조회 API 중 하나라도 <c>IsDirty = true</c>를 세우면 <c>탐색만으로는…</c>이 실패한다.
    ///    그 테스트는 <b>같은 자리에서</b> «내용 변경은 실제로 IsDirty를 세운다»를 함께 재서,
    ///    이 부재 단언이 «측정기가 죽어서» 통과한 것이 아님을 못박는다(CLAUDE.md 부재 단언 규칙).
    /// </summary>
    public sealed class TodoPlannedDayTests
    {
        // 「어제/오늘/내일」 — CurrencyRules.LocalDayIndex가 내는 정수와 같은 계다(에폭 이후 일수).
        // 실제 벽시계를 읽지 않는 이유: 이 모델은 «오늘»을 계산하지 않는다(UW-6-2 시계 단일성).
        private const int Yesterday = 20_700;
        private const int Today = 20_701;
        private const int Tomorrow = 20_702;

        private readonly List<TodoItem> _buffer = new List<TodoItem>(8);

        [SetUp]
        public void Reset()
        {
            TodoListModel.ResetForTesting();
            // ToggleComplete가 [오늘 할일] 하루 1회 동전을 지급한다(모델이 재화를 건드리는 유일한 자리) —
            // 그 정적 상태를 이 픽스처 밖으로 내보내지 않는다.
            CurrencyModel.ResetForTesting();
            _buffer.Clear();
        }

        [TearDown]
        public void ResetAfter()
        {
            TodoListModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
        }

        private static TodoItem Last => TodoListModel.ActiveItems[TodoListModel.ActiveItems.Count - 1];

        [Test]
        public void 날짜_없이_추가한_항목은_미상이다()
        {
            TodoListModel.Add("우유 사기", softCap: 15);

            Assert.AreEqual(TodoItem.UnknownPlannedDay, Last.PlannedDayIndex,
                "날짜를 안 준 항목에 날짜가 생겼습니다.");
            Assert.IsFalse(Last.IsPlannedDayKnown,
                "미상 항목이 «날짜를 안다»고 말합니다 — 화면이 날짜 라벨을 그리게 됩니다.");
        }

        [Test]
        public void 날짜를_주면_그_날짜로_들어간다()
        {
            TodoListModel.Add("내일 회의 준비", softCap: 15, plannedDayIndex: Tomorrow);

            Assert.AreEqual(Tomorrow, Last.PlannedDayIndex, "준 날짜가 저장되지 않았습니다.");
            Assert.IsTrue(Last.IsPlannedDayKnown, "날짜를 줬는데 «모른다»고 말합니다.");
        }

        /// <summary>음수는 «미상»으로 접는다 — 에폭 이전 날짜는 이 앱에 존재할 수 없고,
        /// 손상된 값으로 달력을 그리느니 «모른다»가 정확하다.</summary>
        [Test]
        public void 음수_날짜는_미상으로_접힌다()
        {
            TodoListModel.Add("손상된 값", softCap: 15, plannedDayIndex: -5);

            Assert.AreEqual(TodoItem.UnknownPlannedDay, Last.PlannedDayIndex,
                "음수 날짜가 그대로 들어왔습니다 — 달력이 음수 칸을 그리게 됩니다.");
        }

        /// <summary>
        /// ★★ UW-6-4의 본 단언 — <b>미상 항목은 어느 날짜 조회에도 잡히지 않는다</b>.
        /// <para>양성 대조를 <b>같은 테스트 안에</b> 둔다: 같은 날짜에 실제로 있는 항목은 잡혀야 한다.
        /// 그게 없으면 아래 부재 단언은 "조회가 통째로 죽어서" 통과한 것과 구별되지 않는다.</para>
        /// </summary>
        [Test]
        public void 미상_항목은_어느_날짜_조회에도_안_잡힌다()
        {
            TodoListModel.Add("옛 세이브에서 올라온 것", softCap: 15);                       // 미상
            TodoListModel.Add("오늘 할 것", softCap: 15, plannedDayIndex: Today);            // 오늘

            // ---- 양성 대조: 오늘 조회는 실제로 «오늘 것»을 찾는다 ----
            TodoListModel.DayTally today = TodoListModel.TallyForDay(Today);
            Assert.AreEqual(1, today.Total,
                "오늘 조회가 오늘 항목을 못 찾습니다 — 아래 부재 단언이 무효가 됩니다.");
            Assert.AreEqual(1, TodoListModel.AppendItemsForDay(Today, _buffer), "오늘 목록이 비었습니다.");
            Assert.AreEqual("오늘 할 것", _buffer[0].Text, "오늘 목록에 엉뚱한 항목이 들어왔습니다.");

            // ---- 부재 단언: 미상은 «0일»로도, 다른 어느 날로도 잡히지 않는다 ----
            Assert.AreEqual(0, TodoListModel.TallyForDay(TodoItem.UnknownPlannedDay).Total,
                "0을 날짜처럼 물었더니 미상 항목이 잡혔습니다 — 0을 1970-01-01로 읽는 경로입니다(UW-6-4 무효).");
            _buffer.Clear();
            Assert.AreEqual(0, TodoListModel.AppendItemsForDay(TodoItem.UnknownPlannedDay, _buffer),
                "0일 목록에 미상 항목이 실렸습니다 — 그 화면은 1970년 1월 1일입니다.");
            foreach (int day in new[] { Yesterday, Today, Tomorrow })
            {
                _buffer.Clear();
                TodoListModel.AppendItemsForDay(day, _buffer);
                foreach (TodoItem item in _buffer)
                {
                    Assert.IsTrue(item.IsPlannedDayKnown,
                        $"{day}일 목록에 «날짜 미상» 항목이 섞여 들어왔습니다.");
                }
            }

            // ---- 미상은 «미상 창구»로만 나온다 ----
            _buffer.Clear();
            Assert.AreEqual(1, TodoListModel.AppendUnknownPlannedDayItems(_buffer),
                "미상 항목을 볼 창구가 없습니다 — 옛 사용자의 할일이 화면에서 통째로 사라집니다.");
            Assert.AreEqual("옛 세이브에서 올라온 것", _buffer[0].Text, "미상 창구에 엉뚱한 항목이 나왔습니다.");
        }

        /// <summary>달력 셀 하나가 필요로 하는 값(완료/전체) — 활성과 완료함을 <b>합쳐</b> 센다.
        /// 데이터는 옮기지 않고 뷰가 합친다(R6-4).</summary>
        [Test]
        public void 하루_집계는_활성과_완료함을_합친다()
        {
            TodoListModel.Add("A", softCap: 15, plannedDayIndex: Today);
            TodoListModel.Add("B", softCap: 15, plannedDayIndex: Today);
            TodoListModel.Add("C", softCap: 15, plannedDayIndex: Tomorrow);

            int bId = TodoListModel.ActiveItems[1].Id;
            TodoListModel.ToggleComplete(bId);
            // 유예가 지나면 완료함으로 내려간다 — 그래도 그날의 집계에서는 사라지면 안 된다.
            TodoListModel.SweepCompleted(lingerSeconds: 0f);

            Assert.AreEqual(1, TodoListModel.CompletedArchive.Count, "전제 — 완료 항목이 완료함으로 내려가야 한다.");

            TodoListModel.DayTally today = TodoListModel.TallyForDay(Today);
            Assert.AreEqual(2, today.Total,
                "완료함으로 내려간 항목이 그날 집계에서 사라졌습니다 — 진행 막대의 분모가 줄어듭니다.");
            Assert.AreEqual(1, today.Completed, "완료 수가 틀립니다.");
            Assert.AreEqual(1, today.Remaining, "남은 수가 틀립니다.");
            Assert.IsTrue(today.HasAny, "항목이 있는 날인데 «빈 칸»이라고 말합니다.");

            Assert.AreEqual(1, TodoListModel.TallyForDay(Tomorrow).Total, "내일 집계가 틀립니다.");
            Assert.IsFalse(TodoListModel.TallyForDay(Yesterday).HasAny, "적어둔 게 없는 날이 «있다»고 말합니다.");
        }

        /// <summary>그날 목록은 <b>미완료 먼저, 완료 나중</b>이다(R6-7 «한 목록, 두 그룹»).</summary>
        [Test]
        public void 하루_목록은_미완료가_먼저다()
        {
            TodoListModel.Add("먼저 끝낸 것", softCap: 15, plannedDayIndex: Today);
            TodoListModel.Add("아직 남은 것", softCap: 15, plannedDayIndex: Today);
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[0].Id);

            Assert.AreEqual(2, TodoListModel.AppendItemsForDay(Today, _buffer), "그날 항목 수가 틀립니다.");
            Assert.IsFalse(_buffer[0].Completed, "완료 항목이 미완료보다 앞에 왔습니다.");
            Assert.IsTrue(_buffer[1].Completed, "완료 항목이 뒤에 오지 않았습니다.");
        }

        /// <summary>밀린 것 = <b>날짜가 있고 · 오늘보다 이르고 · 아직 안 끝난 것</b>(R6-7).
        /// 미상은 밀린 것이 아니다 — 언제 하기로 했는지 모르는 것을 «늦었다»고 말할 수 없다.</summary>
        [Test]
        public void 밀린_것은_오늘보다_이른_미완료뿐이다()
        {
            TodoListModel.Add("어제 못 한 것", softCap: 15, plannedDayIndex: Yesterday);
            TodoListModel.Add("어제 끝낸 것", softCap: 15, plannedDayIndex: Yesterday);
            TodoListModel.Add("오늘 것", softCap: 15, plannedDayIndex: Today);
            TodoListModel.Add("내일 것", softCap: 15, plannedDayIndex: Tomorrow);
            TodoListModel.Add("날짜 모르는 것", softCap: 15);
            TodoListModel.ToggleComplete(TodoListModel.ActiveItems[1].Id);

            Assert.AreEqual(1, TodoListModel.AppendOverdueItems(Today, _buffer),
                "밀린 것의 개수가 1이 아닙니다.");
            Assert.AreEqual("어제 못 한 것", _buffer[0].Text, "밀린 것으로 엉뚱한 항목이 잡혔습니다.");
        }

        /// <summary>탐색 하한(R6-8 #5) — 가장 이른 날짜. <b>미상은 후보가 아니다</b>(0을 하한으로
        /// 삼으면 탐색이 1970년까지 열린다).</summary>
        [Test]
        public void 가장_이른_날짜는_미상을_세지_않는다()
        {
            Assert.IsFalse(TodoListModel.TryGetEarliestPlannedDay(out _),
                "아무것도 없는데 가장 이른 날짜가 있다고 말합니다.");

            TodoListModel.Add("날짜 모르는 것", softCap: 15);
            Assert.IsFalse(TodoListModel.TryGetEarliestPlannedDay(out _),
                "미상 항목 하나로 탐색 하한이 «0일»(=1970-01-01)이 됐습니다.");

            TodoListModel.Add("오늘 것", softCap: 15, plannedDayIndex: Today);
            TodoListModel.Add("어제 것", softCap: 15, plannedDayIndex: Yesterday);
            Assert.IsTrue(TodoListModel.TryGetEarliestPlannedDay(out int earliest), "가장 이른 날짜를 못 찾습니다.");
            Assert.AreEqual(Yesterday, earliest, "가장 이른 날짜가 틀립니다.");
        }

        /// <summary>
        /// ★★ persona-stress R-10 — <b>탐색만으로는 저장이 걸리지 않는다</b>.
        ///
        /// <para>세 인스턴스가 같은 세이브 파일을 공유하고, <c>CharacterSaveStore</c>는 같은 버전
        /// 인스턴스끼리의 갱신 손실을 <b>범위 밖</b>이라고 명시한다. 날짜를 넘길 때마다 저장이
        /// 걸리면 그 충돌 창이 넓어진다.</para>
        ///
        /// <para><b>측정기가 살아 있음을 같은 테스트 안에서 증명한다</b>: 마지막에 «내용 변경»을
        /// 한 번 해서 <see cref="TodoListModel.IsDirty"/>가 실제로 서는지 본다. 그게 없으면 이 부재
        /// 단언은 «IsDirty가 영영 안 서는 코드»에서도 통과한다(CLAUDE.md 확대 규칙).</para>
        /// </summary>
        [Test]
        public void 탐색만으로는_저장도_통지도_걸리지_않는다()
        {
            TodoListModel.Add("A", softCap: 15, plannedDayIndex: Today);
            TodoListModel.Add("B", softCap: 15, plannedDayIndex: Tomorrow);
            TodoListModel.MarkSaved();
            Assert.IsFalse(TodoListModel.IsDirty, "전제 — 저장 직후에는 깨끗해야 한다.");

            int changed = 0;
            Action handler = () => changed++;
            StickmanEventBus.TodoListChanged += handler;
            try
            {
                // «이전/다음 날짜»와 «달력 페이지»가 부를 수 있는 조회를 전수로 돈다.
                for (int day = Yesterday - 40; day <= Tomorrow + 40; day++)
                {
                    TodoListModel.TallyForDay(day);
                    _buffer.Clear();
                    TodoListModel.AppendItemsForDay(day, _buffer);
                    TodoListModel.AppendOverdueItems(day, _buffer);
                }
                _buffer.Clear();
                TodoListModel.AppendUnknownPlannedDayItems(_buffer);
                TodoListModel.TryGetEarliestPlannedDay(out _);

                Assert.IsFalse(TodoListModel.IsDirty,
                    "날짜를 넘겨 보기만 했는데 «저장할 것이 생겼다»가 됐습니다 — 주기 저장이 파일을 " +
                    "쓰게 되고, 같은 파일을 쓰는 다른 인스턴스와의 충돌 창이 넓어집니다(R-10).");
                Assert.AreEqual(0, changed,
                    "조회가 TodoListChanged를 쐈습니다 — 듣는 화면들이 전부 다시 그려집니다(상주 앱).");

                // ---- 측정기가 살아 있는가(양성 대조) ----
                TodoListModel.Add("내용 변경", softCap: 15, plannedDayIndex: Today);
                Assert.IsTrue(TodoListModel.IsDirty,
                    "내용을 바꿨는데도 IsDirty가 서지 않습니다 — 위 부재 단언이 무효입니다.");
                Assert.AreEqual(1, changed, "내용 변경이 TodoListChanged를 쏘지 않았습니다(측정기 무효).");
            }
            finally
            {
                StickmanEventBus.TodoListChanged -= handler;
            }
        }

        /// <summary>★ 「지금 보고 있는 날짜」는 이 모델이 들고 있지 않다 — 그래서 탐색이 저장에
        /// 닿을 경로가 <b>구조적으로 존재하지 않는다</b>. 누군가 «선택된 날짜»를 모델에 올리면
        /// 그 순간 세이브 스키마에 칸이 필요해지고 R-10이 되살아난다.</summary>
        [Test]
        public void 선택된_날짜를_모델이_들고_있지_않다()
        {
            foreach (System.Reflection.MemberInfo member in typeof(TodoListModel).GetMembers(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                         | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance))
            {
                if (!(member is System.Reflection.FieldInfo) && !(member is System.Reflection.PropertyInfo)) continue;
                string name = member.Name.ToLowerInvariant();
                // ★ 니들을 좁게 잡는다 — «selected/viewing/browsing»만 본다. «current…»까지 넣으면
                //   정당한 이름(예: 오늘 페이지 집계)까지 걸려 거짓 빨강이 된다.
                bool looksLikeSelection = (name.Contains("selected") || name.Contains("viewing")
                                           || name.Contains("browsing"))
                                          && (name.Contains("day") || name.Contains("date"));
                Assert.IsFalse(looksLikeSelection,
                    $"모델이 «지금 보고 있는 날짜»를 들고 있습니다({member.Name}) — 탐색 상태는 화면의 " +
                    "것이고 세이브에 칸이 없어야 합니다(persona-stress R-10).");
            }
        }
    }
}
