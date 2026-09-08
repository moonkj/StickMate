using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 회귀 잠금 — <b>[오늘 할일] 500 × 512 좌표표(docs/UX_WIDGETS.md R6-5)가 코드에 그대로 있다.</b>
    ///
    /// ============================================================================
    /// 왜 «상수를 그대로 읽어» 재는가
    /// ============================================================================
    /// 이 저장소는 <b>폭 상수를 바꿀 때 파생값이 조각마다 흩어져</b> 깨진 적이 있다(폭 1042가
    /// 헤더에는 갔는데 카드줄에는 안 가 캐러셀 4건이 깨졌다). 그래서 여기서 재는 것은
    /// «구현이 스스로와 일관된가»가 아니라 <b>설계 문서의 표와 같은가</b>다 — 그래서 오른쪽 값은
    /// 「프로덕션에서 베낀 숫자」가 아니라 <b>R6-5 표의 전사</b>이고, 그 표가 바뀌면 이 파일도
    /// 함께 바뀌어야 한다(그게 이 파일의 존재 이유다).
    ///
    /// <para>읽는 방법은 소스 파싱이 아니라 <b>리플렉션</b>이다 — <c>const</c>는 컴파일된 값이므로
    /// 「식이 파생인데 값이 틀린」 경우를 파싱보다 정확히 잡는다.</para>
    ///
    /// ============================================================================
    /// 여기서 재지 <b>않는</b> 것
    /// ============================================================================
    /// 실제로 그 자리에 그려지는가(<c>RectTransform</c> 사각형)는 PlayMode의
    /// <c>TodoBoardDateNavigationTests</c>가 씬에서 잰다. 두 파일은 서로를 대체하지 않는다.
    /// </summary>
    public sealed class TodoBoardDateAxisLayoutTests
    {
        private const string LogPrefix = "[할일500x512]";

        private static float C(string name)
        {
            FieldInfo f = typeof(TodoBoardPopover).GetField(name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(f, $"{LogPrefix} 상수 {name}이 사라졌습니다 — 이름을 바꿨다면 이 감사도 함께 고치세요.");
            Assert.IsTrue(f.IsLiteral, $"{LogPrefix} {name}이 더 이상 const가 아닙니다.");
            return Convert.ToSingle(f.GetRawConstantValue());
        }

        // ====================================================================
        // ① 바깥 치수 — R6-5-1
        // ====================================================================

        [Test]
        public void 창은_500x512이고_Content는_468x454다()
        {
            Assert.AreEqual(500f, C("Width"), 0.001f, $"{LogPrefix} 폭이 R6-5 확정치(500)와 다릅니다.");
            Assert.AreEqual(512f, C("Height"), 0.001f, $"{LogPrefix} 높이가 R6-5 확정치(512)와 다릅니다.");
            Assert.AreEqual(468f, C("ContentWidth"), 0.001f,
                $"{LogPrefix} Content 폭이 468이 아닙니다(= 500 − 16×2).");
            Assert.AreEqual(454f, TodoBoardPopover.ContentHeightPoints, 0.001f,
                $"{LogPrefix} Content 높이가 454가 아닙니다(= 512 − 58).");

            // 세로 스택 검산 — R6-5-1의 «452 ≤ 454(여유 2)» 그대로.
            float stack = C("NavRowHeight") + UiChrome.Space2 + C("InputRowHeight") + UiChrome.Space2
                          + C("ListHeight") + UiChrome.Space1 + C("FooterHeight");
            Assert.AreEqual(452f, stack, 0.001f, $"{LogPrefix} 세로 스택이 452가 아닙니다: {stack}");
            Assert.LessOrEqual(stack, TodoBoardPopover.ContentHeightPoints,
                $"{LogPrefix} 세로 스택 {stack}이 Content {TodoBoardPopover.ContentHeightPoints}를 넘습니다 — " +
                "마지막 행이나 푸터가 창 밖에 그려집니다.");
        }

        [Test]
        public void 보이는_행은_10이고_11행은_들어가지_않는다()
        {
            Assert.AreEqual(10, TodoBoardPopover.DesignVisibleRows,
                $"{LogPrefix} 보이는 행이 10이 아닙니다 — 사용자 요구 ①(«창이 작다»)에 대한 답이 그 숫자입니다.");
            Assert.AreEqual(366f, C("ListHeight"), 0.001f,
                $"{LogPrefix} 목록 높이가 366이 아닙니다(= 10×33 + 9×4).");

            // R6-5-1 검산 «11행이 되는가: 489 > 454 ✘».
            float elevenRows = 11f * C("RowHeight") + 10f * C("RowGap");
            float elevenStack = C("NavRowHeight") + UiChrome.Space2 + C("InputRowHeight") + UiChrome.Space2
                                + elevenRows + UiChrome.Space1 + C("FooterHeight");
            Assert.Greater(elevenStack, TodoBoardPopover.ContentHeightPoints,
                $"{LogPrefix} 11행이 들어갑니다({elevenStack} ≤ {TodoBoardPopover.ContentHeightPoints}) — " +
                "상한 10의 근거가 사라졌으니 R6-5를 다시 계산하세요.");
        }

        // ====================================================================
        // ② 일별 페이지 좌표표 — R6-5-2
        // ====================================================================

        [Test]
        public void 일별_페이지_좌표가_R6_5_2_표와_같다()
        {
            float content = C("ContentWidth");

            // y=0 · h=24 : [‹]24@0 · 날짜라벨 196@28 · [›]24@228 · [오늘]44@376 · [내일]44@424
            Assert.AreEqual(24f, C("NavRowHeight"), 0.001f, $"{LogPrefix} 날짜 네비 높이");
            Assert.AreEqual(24f, C("NavArrowSize"), 0.001f, $"{LogPrefix} 화살표 크기");
            Assert.AreEqual(28f, C("DateToggleLeft"), 0.001f, $"{LogPrefix} 날짜 라벨 x");
            Assert.AreEqual(196f, C("DateToggleWidth"), 0.001f, $"{LogPrefix} 날짜 라벨 폭");
            Assert.AreEqual(228f, C("NextArrowLeft"), 0.001f, $"{LogPrefix} [›] x");

            float chipW = C("DayChipWidth");
            float chipGap = C("DayChipGap");
            Assert.AreEqual(44f, chipW, 0.001f, $"{LogPrefix} 날짜 칩 폭(최소 타깃 24 이상이어야 합니다)");
            Assert.GreaterOrEqual(C("DayChipHeight"), UiChrome.MinTargetSizePoints,
                $"{LogPrefix} 날짜 칩 높이가 최소 타깃 미만입니다.");
            Assert.AreEqual(376f, content - (chipW + chipGap) - chipW, 0.001f, $"{LogPrefix} [오늘] x");
            Assert.AreEqual(424f, content - chipW, 0.001f, $"{LogPrefix} [내일] x");

            // y=32 · h=30 : 입력 394@0 · [추가] 66@402
            Assert.AreEqual(-32f, C("InputRowTop"), 0.001f, $"{LogPrefix} 입력 줄 y");
            Assert.AreEqual(30f, C("InputRowHeight"), 0.001f, $"{LogPrefix} 입력 줄 높이");
            Assert.AreEqual(66f, C("AddButtonWidth"), 0.001f, $"{LogPrefix} [추가] 폭");
            Assert.AreEqual(394f, content - C("AddButtonWidth") - C("InputToAddGap"), 0.001f,
                $"{LogPrefix} 입력칸 폭");
            Assert.AreEqual(402f, content - C("AddButtonWidth"), 0.001f, $"{LogPrefix} [추가] x");

            // y=70 · h=366 : 목록 10행(행폭 442) + 레일 22@446
            Assert.AreEqual(-70f, C("ListTop"), 0.001f, $"{LogPrefix} 목록 y");
            Assert.AreEqual(442f, C("RowWidth"), 0.001f, $"{LogPrefix} 행 폭");
            Assert.AreEqual(22f, C("RailWidth"), 0.001f, $"{LogPrefix} 레일 폭");
            Assert.AreEqual(446f, content - C("RailWidth"), 0.001f, $"{LogPrefix} 레일 x");

            // y=440 · h=12 : 푸터
            Assert.AreEqual(440f,
                TodoBoardPopover.ContentHeightPoints - C("DailyFooterBottom") - C("FooterHeight"), 0.001f,
                $"{LogPrefix} 일별 푸터 y");
        }

        [Test]
        public void 행_내부_좌표가_R6_5_2_표와_같다()
        {
            // 행 내부(442): 체크 20@10 · 라벨 368@38 · [✕] 22@410
            Assert.AreEqual(10f, C("RowBoxLeft"), 0.001f, $"{LogPrefix} 체크박스 x");
            Assert.AreEqual(20f, C("RowBoxSize"), 0.001f, $"{LogPrefix} 체크박스 크기");
            Assert.AreEqual(38f, C("RowLabelLeft"), 0.001f, $"{LogPrefix} 라벨 x");
            Assert.AreEqual(368f, TodoBoardPopover.RowLabelWidth, 0.001f, $"{LogPrefix} 라벨 폭");
            Assert.AreEqual(22f, C("RowDeleteSize"), 0.001f, $"{LogPrefix} [✕] 크기");
            Assert.AreEqual(410f, C("RowDeleteLeft"), 0.001f, $"{LogPrefix} [✕] x");

            // ★ 겹침 0 — 라벨 오른끝이 [✕] 왼끝을 넘지 않는다(17자 항목이 삭제 버튼을 덮던 결함).
            float labelRight = C("RowLabelLeft") + TodoBoardPopover.RowLabelWidth;
            Assert.LessOrEqual(labelRight, C("RowDeleteLeft"),
                $"{LogPrefix} 라벨 상자 오른끝 {labelRight}이 [✕] 왼끝 {C("RowDeleteLeft")}을 넘습니다.");
            Assert.LessOrEqual(C("RowDeleteLeft") + C("RowDeleteSize"), C("RowWidth"),
                $"{LogPrefix} [✕]가 행 밖으로 나갑니다.");
            Assert.GreaterOrEqual(C("RowDeleteSize"), UiChrome.MinTargetSizePoints - 2f,
                $"{LogPrefix} [✕] 타깃이 지나치게 작습니다.");
        }

        // ====================================================================
        // ③ 달력 페이지 좌표표 — R6-5-3
        // ====================================================================

        [Test]
        public void 달력_페이지_좌표가_R6_5_3_표와_같다()
        {
            Assert.AreEqual(42, TodoBoardPopover.CalendarCellCountForTests,
                $"{LogPrefix} 달력 칸이 6행×7열이 아닙니다.");

            // 셀 폭은 <b>정수로 떨어져야</b> 한다: (468 − 6×8)/7 = 60.0
            Assert.AreEqual(60f, C("CalendarCellWidth"), 0.001f,
                $"{LogPrefix} 셀 폭이 60이 아닙니다 — 폭이 바뀌면 7로 나누어떨어지지 않아 " +
                "마지막 열이 반 픽셀씩 어긋납니다.");
            Assert.AreEqual(58f, C("CalendarCellHeight"), 0.001f, $"{LogPrefix} 셀 높이");
            Assert.AreEqual(8f, C("CalendarColumnGap"), 0.001f, $"{LogPrefix} 열 간격");
            Assert.AreEqual(6f, C("CalendarRowGap"), 0.001f, $"{LogPrefix} 행 간격");

            // 가로 합 = Content 폭 정확히
            float gridWidth = 7f * C("CalendarCellWidth") + 6f * C("CalendarColumnGap");
            Assert.AreEqual(C("ContentWidth"), gridWidth, 0.001f,
                $"{LogPrefix} 달력 가로 합 {gridWidth}이 Content 폭과 다릅니다.");

            Assert.AreEqual(-32f, C("WeekdayRowTop"), 0.001f, $"{LogPrefix} 요일 줄 y");
            Assert.AreEqual(16f, C("WeekdayRowHeight"), 0.001f, $"{LogPrefix} 요일 줄 높이");
            Assert.AreEqual(-52f, C("CalendarGridTop"), 0.001f, $"{LogPrefix} 그리드 y");

            // 세로 검산 — R6-5-1의 «24+8+16+4+378+12+12 = 454 정확히 일치».
            float gridHeight = 6f * C("CalendarCellHeight") + 5f * C("CalendarRowGap");
            Assert.AreEqual(378f, gridHeight, 0.001f, $"{LogPrefix} 그리드 높이");
            float bottom = -C("CalendarGridTop") + gridHeight + UiChrome.Space3 + C("FooterHeight");
            Assert.AreEqual(TodoBoardPopover.ContentHeightPoints, bottom, 0.001f,
                $"{LogPrefix} 달력 세로 합 {bottom}이 Content 454와 다릅니다.");
            Assert.AreEqual(442f,
                TodoBoardPopover.ContentHeightPoints - C("CalendarFooterBottom") - C("FooterHeight"), 0.001f,
                $"{LogPrefix} 달력 푸터 y");
        }

        // ====================================================================
        // ④ 화면 클램프 — <b>설계 상수가 아니라 지금 크기</b>가 진실이다
        // ====================================================================

        [Test]
        public void 창이_줄면_보이는_행도_함께_준다()
        {
            Assert.AreEqual(TodoBoardPopover.DesignVisibleRows, TodoBoardPopover.ResolveVisibleRows(512f),
                $"{LogPrefix} 설계 크기에서 10행이 안 나옵니다.");

            float rowPitch = C("RowHeight") + C("RowGap");
            Assert.AreEqual(TodoBoardPopover.DesignVisibleRows - 1,
                TodoBoardPopover.ResolveVisibleRows(512f - rowPitch),
                $"{LogPrefix} 한 행 높이만큼 줄였는데 행 수가 그대로입니다 — " +
                "작은 화면에서 마지막 행이 창 밖에 그려집니다(비침해 원칙 2).");

            Assert.AreEqual(1, TodoBoardPopover.ResolveVisibleRows(0f),
                $"{LogPrefix} 병적으로 작은 높이에서 0행 또는 음수가 나왔습니다.");
            Assert.AreEqual(TodoBoardPopover.DesignVisibleRows, TodoBoardPopover.ResolveVisibleRows(10000f),
                $"{LogPrefix} 큰 화면에서 설계 상한을 넘었습니다 — 행 배열보다 많은 행을 그리려 합니다.");

            // 단조성 — 화면이 커지는데 행이 줄면 그건 계산이 뒤집힌 것이다.
            int prev = 0;
            for (float h = 60f; h <= 600f; h += 7f)
            {
                int rows = TodoBoardPopover.ResolveVisibleRows(h);
                Assert.GreaterOrEqual(rows, prev, $"{LogPrefix} 높이 {h}에서 행 수가 줄었습니다.");
                prev = rows;
            }
        }

        [Test]
        public void 달력_셀은_창이_줄면_함께_낮아진다()
        {
            Assert.AreEqual(C("CalendarCellHeight"), TodoBoardPopover.ResolveCalendarCellHeight(512f), 0.001f,
                $"{LogPrefix} 설계 크기에서 셀 높이가 58이 아닙니다.");

            float small = TodoBoardPopover.ResolveCalendarCellHeight(400f);
            Assert.Less(small, C("CalendarCellHeight"),
                $"{LogPrefix} 창이 112pt 줄었는데 셀 높이가 그대로입니다 — 6주가 창 밖으로 흘러나갑니다.");
            Assert.Greater(small, 0f, $"{LogPrefix} 셀 높이가 0 이하가 됐습니다.");

            // 어떤 높이에서도 6주 + 푸터가 Content 안에 들어간다(한 주도 숨기지 않는다).
            for (float h = 120f; h <= 700f; h += 11f)
            {
                float cell = TodoBoardPopover.ResolveCalendarCellHeight(h);
                float used = -C("CalendarGridTop") + 6f * cell + 5f * C("CalendarRowGap") + C("FooterHeight");
                float content = h - 58f;
                if (content <= 200f) continue;   // 하한(8pt)에 걸리는 병적 구간은 제외.
                Assert.LessOrEqual(used, content + 0.001f,
                    $"{LogPrefix} 패널 {h}pt에서 달력이 {used}pt를 써 Content {content}pt를 넘습니다.");
            }
        }

        // ====================================================================
        // ⑤ 날짜 — 시계를 만들지 않는다(UW-6-2)
        // ====================================================================

        [Test]
        public void 일자_번호_환산이_재화_규칙과_같은_에폭을_쓴다()
        {
            // ★ CurrencyRules.LocalDayIndex의 기산점은 private이라 참조할 수 없다. 그래서 값을
            //   베끼는 대신 <b>그 함수로 왕복</b>해 같은 정수가 나오는지 본다 — 두 에폭이 갈라지면
            //   여기서 즉시 걸린다.
            var samples = new[]
            {
                new DateTime(1970, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2000, 2, 29, 3, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 8, 23, 59, 0, DateTimeKind.Utc),
                new DateTime(2031, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            };

            foreach (DateTime utc in samples)
            {
                int index = CurrencyRules.LocalDayIndex(utc, 0);
                DateTime back = TodoBoardPopover.DateOfDayIndex(index);
                Assert.AreEqual(utc.Date, back.Date,
                    $"{LogPrefix} 일자 #{index}를 되돌리니 {back:yyyy-MM-dd}로, 원래 {utc:yyyy-MM-dd}와 다릅니다 — " +
                    "두 파일의 기산점이 갈라졌습니다.");
                Assert.AreEqual(index, TodoBoardPopover.DayIndexOfDate(back),
                    $"{LogPrefix} 역함수가 왕복하지 않습니다.");
            }
        }

        [Test]
        public void 요일_이름이_실제_요일과_맞물린다()
        {
            Assert.AreEqual(7, TodoBoardPopover.WeekdayNames.Length, $"{LogPrefix} 요일이 7개가 아닙니다.");

            // 1970-01-01(일자 0)은 <b>목요일</b>이다. 인덱스가 한 칸 밀리면 여기서 걸린다.
            Assert.AreEqual(DayOfWeek.Thursday, TodoBoardPopover.DateOfDayIndex(0).DayOfWeek,
                $"{LogPrefix} 일자 0의 요일이 목요일이 아닙니다 — 기산점이 어긋났습니다.");
            Assert.AreEqual("목", TodoBoardPopover.WeekdayNames[(int)DayOfWeek.Thursday],
                $"{LogPrefix} 요일 이름 배열이 System.DayOfWeek 순서(0=일요일)가 아닙니다.");

            for (int i = 0; i < 7; i++)
            {
                DateTime d = TodoBoardPopover.DateOfDayIndex(i);
                StringAssert.Contains($"({TodoBoardPopover.WeekdayNames[(int)d.DayOfWeek]})",
                    TodoBoardPopover.FormatDayLabel(i),
                    $"{LogPrefix} 일자 #{i}의 라벨에 요일이 안 들어갔습니다.");
            }
        }

        [Test]
        public void 날짜_라벨과_달_라벨이_R6_5_문구_형태다()
        {
            int index = TodoBoardPopover.DayIndexOfDate(new DateTime(2026, 9, 8));
            Assert.AreEqual("9월 8일 (화) ▾", TodoBoardPopover.FormatDayLabel(index),
                $"{LogPrefix} 날짜 라벨 형태가 R6-5-2의 «9월 8일 (화) ▾»와 다릅니다.");
            Assert.AreEqual("2026년 9월", TodoBoardPopover.FormatMonthLabel(index),
                $"{LogPrefix} 달 라벨 형태가 R6-5-3의 «2026년 9월»과 다릅니다.");

            // ▾는 «여기를 누르면 달력이 열린다»는 유일한 신호다(탭을 늘리지 않기로 했으므로).
            StringAssert.Contains("▾", TodoBoardPopover.FormatDayLabel(index),
                $"{LogPrefix} 날짜 라벨에서 토글 표식이 사라졌습니다 — 달력으로 가는 길이 보이지 않습니다.");
        }

        // ====================================================================
        // ⑥ 입력 정의역 · 빈 상태 문구 — R6-3 / R6-8
        // ====================================================================

        [Test]
        public void 적을_수_있는_날은_오늘과_내일뿐이다()
        {
            const int today = 20000;
            Assert.IsTrue(TodoBoardPopover.IsWritableDay(today, today), $"{LogPrefix} 오늘에 못 적습니다.");
            Assert.IsTrue(TodoBoardPopover.IsWritableDay(today + 1, today), $"{LogPrefix} 내일에 못 적습니다.");
            Assert.IsFalse(TodoBoardPopover.IsWritableDay(today - 1, today),
                $"{LogPrefix} 어제에 적을 수 있습니다 — 지난 날짜는 열람 전용입니다(R6-3).");
            Assert.IsFalse(TodoBoardPopover.IsWritableDay(today + 2, today),
                $"{LogPrefix} 모레에 적을 수 있습니다 — 임의 미래 예약은 이번 범위 밖입니다.");
            Assert.IsFalse(TodoBoardPopover.IsWritableDay(TodoItem.UnknownPlannedDay, TodoItem.UnknownPlannedDay),
                $"{LogPrefix} 날짜 축이 없는데 «쓸 수 있다»가 나왔습니다.");
        }

        [Test]
        public void 입력_안내가_대상_날짜를_말한다()
        {
            const int today = 20000;
            Assert.AreEqual("오늘 할일을 적어보세요", TodoBoardPopover.ResolveInputPlaceholder(today, today));
            Assert.AreEqual("내일 할일을 적어보세요", TodoBoardPopover.ResolveInputPlaceholder(today + 1, today));
            Assert.AreEqual("이 날짜엔 새로 적을 수 없어요", TodoBoardPopover.ResolveInputPlaceholder(today - 3, today));
            Assert.AreEqual("할일을 적어보세요",
                TodoBoardPopover.ResolveInputPlaceholder(TodoItem.UnknownPlannedDay, TodoItem.UnknownPlannedDay),
                $"{LogPrefix} 날짜 축이 없을 때의 문구가 바뀌었습니다(현행 문구를 유지해야 합니다).");
        }

        [Test]
        public void 빈_상태_문구가_R6_8_표_그대로이고_추궁하지_않는다()
        {
            const int today = 20000;

            Assert.AreEqual("오늘은 아직 비어 있어요", TodoBoardPopover.ResolveEmptyTitle(today, today, false));
            Assert.AreEqual("위에 적어두면 제가 가끔 챙겨줄게요.", TodoBoardPopover.ResolveEmptyBody(today, today, false));
            Assert.AreEqual("이 날은 적어둔 게 없어요", TodoBoardPopover.ResolveEmptyTitle(today - 5, today, false));
            Assert.AreEqual("내일 할 일을 미리 적어둘 수 있어요", TodoBoardPopover.ResolveEmptyTitle(today + 1, today, false));
            Assert.AreEqual("완료한 일이 아직 없어요", TodoBoardPopover.ResolveEmptyTitle(today, today, true));

            // ★ 17절 «추궁 금지» — 지난 날짜 빈 화면이 사용자를 나무라면 안 된다.
            foreach (int day in new[] { today - 30, today - 1, today, today + 1 })
            {
                string title = TodoBoardPopover.ResolveEmptyTitle(day, today, false);
                StringAssert.DoesNotContain("아무것도", title,
                    $"{LogPrefix} 빈 상태 문구가 사용자를 추궁합니다: \"{title}\"");
                Assert.IsNotEmpty(title, $"{LogPrefix} 일자 {day}의 빈 상태 제목이 비었습니다 — " +
                    "민지가 «미완성»으로 읽는 자리입니다.");
            }
        }

        // ====================================================================
        // ⑦ 포스트잇 오늘 필터 — persona-stress R-7
        // ====================================================================

        [Test]
        public void 상시_카드는_내일_항목을_그리지_않는다()
        {
            const int today = 20000;
            var source = new List<TodoItem>
            {
                new TodoItem(1, "오늘 것", today),
                new TodoItem(2, "내일 것", today + 1),
                new TodoItem(3, "밀린 것", today - 2),
                new TodoItem(4, "미상", TodoItem.UnknownPlannedDay),
            };

            var buffer = new List<TodoItem>();
            TodoPostItWidget.FilterTodaySurface(source, today, buffer);

            CollectionAssert.AreEquivalent(new[] { 1, 3, 4 }, Ids(buffer),
                $"{LogPrefix} 상시 카드가 그릴 항목이 «오늘 + 밀린 것 + 미상»이 아닙니다 — " +
                "사용자가 아무것도 열지 않았는데 내일 것이 뜨면 그건 침해입니다(R-7).");

            Assert.IsFalse(TodoPostItWidget.IsTodaySurfaceItem(source[1], today),
                $"{LogPrefix} 내일 항목이 상시 표면 판정을 통과했습니다.");

            // ---- 완료 임박(유예 중) 항목도 같은 날짜 조건을 지난다 ----
            source[0].Completed = true;
            source[1].Completed = true;
            TodoPostItWidget.FilterTodaySurface(source, today, buffer);
            CollectionAssert.Contains(Ids(buffer), 1,
                $"{LogPrefix} 방금 체크한 <b>오늘</b> 항목이 사라졌습니다 — 취소선 유예 연출이 죽습니다.");
            CollectionAssert.DoesNotContain(Ids(buffer), 2,
                $"{LogPrefix} 완료된 <b>내일</b> 항목이 상시 카드에 떴습니다.");
        }

        [Test]
        public void 날짜_축이_없으면_거르지_않는다()
        {
            // 저장 파일을 읽고 롤오버 시계가 돌기 전의 몇 프레임 — 0을 1970-01-01로 읽어
            // 카드를 통째로 비우면 «할 일이 사라졌다»가 된다(UW-6-4가 못박은 그 경로).
            const int today = 20000;
            var source = new List<TodoItem>
            {
                new TodoItem(1, "오늘 것", today),
                new TodoItem(2, "내일 것", today + 1),
            };

            var buffer = new List<TodoItem>();
            TodoPostItWidget.FilterTodaySurface(source, TodoItem.UnknownPlannedDay, buffer);
            Assert.AreEqual(source.Count, buffer.Count,
                $"{LogPrefix} 날짜 축이 확정되기 전에 항목이 걸러졌습니다 — 카드가 잠깐 비어 보입니다.");

            // 양성 대조 — 축이 있으면 실제로 거른다(위 단언이 «필터가 죽어서» 통과한 것이 아니다).
            TodoPostItWidget.FilterTodaySurface(source, today, buffer);
            Assert.AreEqual(1, buffer.Count,
                $"{LogPrefix} 축이 있는데도 안 걸렀습니다 — 위 단언이 무의미합니다.");
        }

        // ====================================================================
        // ⑧ UW-6-5 인계 — «오늘 페이지에 실제로 뜨는 개수»를 셀 수 있다
        // ====================================================================

        [Test]
        public void 오늘_페이지에_뜨는_미완료_개수를_셀_수_있다()
        {
            const int today = 20000;
            TodoListModel.ResetForTesting();
            try
            {
                TodoListModel.Add("오늘", softCap: 99, plannedDayIndex: today);
                TodoListModel.Add("밀린 것", softCap: 99, plannedDayIndex: today - 3);
                TodoListModel.Add("미상", softCap: 99, plannedDayIndex: TodoItem.UnknownPlannedDay);
                TodoListModel.Add("내일", softCap: 99, plannedDayIndex: today + 1);

                Assert.AreEqual(4, TodoListModel.UncompletedCount,
                    $"{LogPrefix} 전제 — 전체 미완료가 4건이어야 합니다(현행 소프트캡 기준).");
                Assert.AreEqual(3, TodoBoardPopover.CountTodaySurfaceUncompleted(today),
                    $"{LogPrefix} «오늘 페이지에 뜨는 것»이 3건(오늘+밀린+미상)이 아닙니다 — " +
                    "이 숫자가 UW-6-5의 새 소프트캡 기준이 됩니다(값은 design-systems 소관).");

                // ★ 두 기준이 실제로 <b>갈라진다</b>는 것을 못박는다. 같은 값이면 정의를 옮길 이유가
                //   없고, 이 함수는 있으나 마나다.
                Assert.AreNotEqual(TodoListModel.UncompletedCount,
                    TodoBoardPopover.CountTodaySurfaceUncompleted(today),
                    $"{LogPrefix} 두 기준이 같은 값을 냅니다 — «내일»이 안 걸러졌습니다.");

                // 완료한 것은 세지 않는다(경고는 «치울 것이 많다»는 뜻이다).
                TodoListModel.ToggleComplete(TodoListModel.ActiveItems[0].Id);
                Assert.AreEqual(2, TodoBoardPopover.CountTodaySurfaceUncompleted(today),
                    $"{LogPrefix} 완료한 항목이 아직 세어집니다.");

                // 날짜 축이 없으면 «전부»가 곧 «오늘 페이지»다.
                Assert.AreEqual(TodoListModel.UncompletedCount,
                    TodoBoardPopover.CountTodaySurfaceUncompleted(TodoItem.UnknownPlannedDay),
                    $"{LogPrefix} 날짜 축이 서기 전 기준이 «전체 미완료»와 다릅니다.");

                // ★ 세기만 하고 <b>저장을 건드리지 않는다</b>(R-10).
                TodoListModel.MarkSaved();
                TodoBoardPopover.CountTodaySurfaceUncompleted(today);
                Assert.IsFalse(TodoListModel.IsDirty,
                    $"{LogPrefix} 세기만 했는데 «저장할 것이 생겼다»가 됐습니다.");
            }
            finally
            {
                TodoListModel.ResetForTesting();
            }
        }

        private static List<int> Ids(List<TodoItem> items)
        {
            var ids = new List<int>(items.Count);
            for (int i = 0; i < items.Count; i++) ids.Add(items[i].Id);
            return ids;
        }
    }
}
