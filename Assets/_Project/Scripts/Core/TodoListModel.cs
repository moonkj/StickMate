using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 투두 항목 1건(docs/UX_FLOW.md 17절). 참조 타입(class)으로 두어 체크/완료시각을 그 자리에서
    /// 갱신할 수 있게 한다 — TodoListModel이 반환하는 읽기 전용 목록에서도 항목 식별(Id)로 갱신 가능.
    /// </summary>
    public sealed class TodoItem
    {
        /// <summary>
        /// ★ <b>「날짜 미상」</b>. <see cref="PlannedDayIndex"/>가 이 값이면 그 항목에는 날짜가 <b>없다</b> —
        /// 「1970-01-01」이 아니다.
        ///
        /// <para><b>0을 실재하는 날짜로 읽는 순간 이 설계는 전부 무효다</b>(docs/UX_WIDGETS.md UW-6-4의
        /// 경고를 그대로 옮긴다). v12 이하 세이브의 모든 항목이 여기로 떨어지므로, 0을 유효 날짜처럼
        /// 다루면 <b>기존 사용자의 할일 전부가 1970년 1월 1일 칸에 쌓이고 테스트는 초록</b>이 된다.
        /// 그래서 이 파일의 날짜 조회 API는 <b>전부</b> 0을 «어느 날에도 속하지 않음»으로 처리하고,
        /// <see cref="IsPlannedDayKnown"/>이 그 판정의 단일 창구다.</para>
        /// </summary>
        public const int UnknownPlannedDay = 0;

        public readonly int Id;
        public readonly string Text;
        public bool Completed;

        /// <summary>완료 체크 시각(Time.unscaledTime 스냅샷) — todoUndoWindowSeconds/
        /// todoCompletedLingerSeconds 판정에 사용. 미완료 상태면 의미 없음(0).</summary>
        public float CompletedAtUnscaledTime;

        /// <summary>
        /// ★ 이 할일이 <b>어느 날의 것</b>인가 — 에폭 이후 일수(정수). 저장 스키마 v13에서 추가됐다.
        ///
        /// <para><b>값의 정의는 새로 만들지 않았다</b>: <see cref="CurrencyRules.LocalDayIndex"/>가 내는
        /// <b>바로 그 정수</b>이고, 화면의 "오늘"은 <see cref="CurrencyModel.DayIndex"/> 하나에서만 온다
        /// (docs/UX_WIDGETS.md UW-6-2 "시계 단일성"). 이 파일은 <c>DateTime.Now</c>를 읽지 않는다 —
        /// 벽시계를 두 벌 만들면 하루 1회 동전(<see cref="CurrencyModel.TryPayTodoDailyCoins"/>)과
        /// 화면의 «오늘»이 서로 다른 순간에 넘어간다(persona-stress R-8).</para>
        ///
        /// <para><b>기본값은 <see cref="UnknownPlannedDay"/>(0) = 「날짜 미상」</b>이다. 그 문단을 반드시
        /// 읽어라 — 0은 날짜가 아니다.</para>
        ///
        /// <para>자정에 <b>아무것도 옮기지 않는다</b>(UW-6-3-1): 저장된 것은 정수 하나이고 "오늘"은
        /// 계산된 정수라, 날짜가 넘어가면 어제의 D+1이 저절로 오늘이 된다. 그래서 «롤오버를 놓쳤다»는
        /// 버그 클래스가 구조적으로 존재하지 않는다(앱이 꺼져 있던 밤에도 같다).</para>
        /// </summary>
        public int PlannedDayIndex;

        /// <summary>이 항목에 <b>날짜가 있는가</b>. false면 날짜 라벨을 그리지 않고 달력의 어느 칸에도
        /// 세지 않는다 — 그것이 <see cref="UnknownPlannedDay"/>가 요구하는 계약이다.
        /// <para>화면을 그리는 쪽은 <c>PlannedDayIndex != 0</c>을 손으로 적지 말고 이 프로퍼티를 봐라
        /// (같은 판정이 두 곳에 살면 한쪽이 반드시 낡는다).</para></summary>
        public bool IsPlannedDayKnown => PlannedDayIndex != UnknownPlannedDay;

        public TodoItem(int id, string text)
        {
            Id = id;
            Text = text;
        }

        /// <summary>날짜를 함께 주는 판. <paramref name="plannedDayIndex"/>가 음수면 «미상»으로 접는다 —
        /// 에폭 이전 날짜는 이 앱에 존재할 수 없고, 손상된 값으로 달력을 그리느니 «모른다»가 정확하다.</summary>
        public TodoItem(int id, string text, int plannedDayIndex) : this(id, text)
        {
            PlannedDayIndex = NormalizePlannedDay(plannedDayIndex);
        }

        /// <summary>음수/손상 값을 «미상»으로 접는다 — 정규화 창구는 여기 하나다.</summary>
        public static int NormalizePlannedDay(int plannedDayIndex)
            => plannedDayIndex > UnknownPlannedDay ? plannedDayIndex : UnknownPlannedDay;
    }

    /// <summary>
    /// docs/UX_FLOW.md 17절 투두 말풍선의 데이터 소스 — 정적 클래스(SpectacleEventLock/StressGauge와
    /// 동일한 이유: 24시간 상주 앱, 씬 생명주기와 무관한 단일 프로세스 전역 상태). 활성(미완료) 목록과
    /// 완료함(누적 보존, 리스트에서는 사라져도 데이터는 보존 — 17절 명시)을 분리해 보관한다.
    ///
    /// 소비자: Interaction/TodoReminderDirector.cs(들고 다니는 모드 트리거), Interaction/
    /// TodoPostItWidget.cs(포스트잇 카드 UI — 이 위젯의 체크박스 클릭은 이 모델을 직접 호출할 뿐,
    /// SpectacleEventLock/Platform.ILocalClickCaptureService와는 완전히 무관하다).
    ///
    /// <para>★ <b>2026-09-06 — 이 모델이 재화를 건드리는 자리가 하나 생겼다</b>:
    /// <see cref="ToggleComplete"/>가 «미완료 → 완료» 전이에서 [오늘 할일] 하루 1회 보상을 지급한다
    /// (<see cref="CurrencyModel.TryPayTodoDailyCoins"/>). 모델에 지급을 둔 것은 <b>완료 전이가
    /// 일어나는 자리가 여기 하나뿐</b>이기 때문이다 — 부르는 UI는 둘이고 앞으로 더 늘 수 있다.
    /// 금액·하루 1회·리셋은 전부 재화 쪽에 있고 이 파일에는 숫자가 없다.</para>
    /// </summary>
    public static class TodoListModel
    {
        private static readonly List<TodoItem> _active = new List<TodoItem>(16);
        private static readonly List<TodoItem> _completedArchive = new List<TodoItem>(16);
        private static readonly ReadOnlyCollection<TodoItem> _activeReadOnly = _active.AsReadOnly();
        private static readonly ReadOnlyCollection<TodoItem> _archiveReadOnly = _completedArchive.AsReadOnly();
        private static int _nextId = 1;

        /// <summary>마지막 저장 이후 목록이 바뀌었는가(Core/UiLayoutModel.IsDirty와 같은 관례).
        /// 주기 저장이 "바뀐 게 있을 때만" 파일을 쓰게 하는 값이다 — 24시간 상주 앱.</summary>
        public static bool IsDirty { get; private set; }

        /// <summary>포스트잇 카드에 표시할 미완료 항목(완료되어 유예 중인 항목은 여전히 여기 남아있다 —
        /// UI가 Completed 플래그로 취소선/반투명을 그린다). 순서는 추가된 순서(FIFO) — "1개 강조" 모드의
        /// 우선순위는 가장 오래된 항목을 우선시하는 단순 규칙이며, 유저 지정 강조는 후속 과제.</summary>
        public static IReadOnlyList<TodoItem> ActiveItems => _activeReadOnly;

        /// <summary>완료함(데이터 보존용, 17절 "리스트에서 사라져도 데이터는 보존").</summary>
        public static IReadOnlyList<TodoItem> CompletedArchive => _archiveReadOnly;

        /// <summary>완료 처리되지 않은 순수 미완료 개수(todoActiveCountSoftCap 판정용 — 유예 중인
        /// 완료 항목은 이미 완료로 간주해 제외한다).</summary>
        public static int UncompletedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _active.Count; i++)
                {
                    if (!_active[i].Completed) count++;
                }
                return count;
            }
        }

        // 들고 다니는 모드(17절) self-transition 텍스트 전달용 — StickmanBlackboard.AttackShotsRemaining과
        // 동일한 "펄스로 세팅 → 소비 즉시 리셋" 관례. TodoReminderDirector가 ChangeState(TodoReminder)
        // 직전에 세팅하고, States/TimedSpectacleState.cs의 dialogueTextSelector가 Enter() 안에서 1회 소비한다.
        private static string _pendingReminderText;

        public static void SetPendingReminderText(string text) => _pendingReminderText = text;

        public static string ConsumePendingReminderText(StickConfig unusedConfig)
        {
            string text = _pendingReminderText;
            _pendingReminderText = null;
            return text;
        }

        /// <summary>새 할일을 <b>날짜 없이</b> 추가한다(옛 호출부 그대로 — 포스트잇/말풍선 경로).
        /// 항상 성공(강제 차단 없음 — 17절 "추가 자체는 막지 않음"). 반환값은 활성 개수가 소프트캡을
        /// 넘겼는지(호출자가 "먼저 정리해볼까?" 안내를 띄울지 판단하는 용도).</summary>
        public static bool Add(string text, int softCap)
            => Add(text, softCap, TodoItem.UnknownPlannedDay);

        /// <summary>
        /// 새 할일을 <b>그 날짜에</b> 추가한다(날짜 축 — docs/UX_WIDGETS.md R6-3).
        /// <paramref name="plannedDayIndex"/>는 <see cref="CurrencyRules.LocalDayIndex"/>가 내는 정수이고,
        /// <see cref="TodoItem.UnknownPlannedDay"/>(0)를 주면 «날짜 미상»으로 들어간다.
        /// <para>「오늘」은 <see cref="CurrencyModel.DayIndex"/>에서만 온다 — 이 파일은 벽시계를 읽지 않는다.
        /// 호출부가 그 값을 넘긴다(UW-6-2 시계 단일성).</para>
        /// </summary>
        public static bool Add(string text, int softCap, int plannedDayIndex)
        {
            if (string.IsNullOrWhiteSpace(text)) return UncompletedCount > softCap;
            _active.Add(new TodoItem(_nextId++, text.Trim(), plannedDayIndex));
            IsDirty = true;
            StickmanEventBus.RaiseTodoListChanged();
            return UncompletedCount > softCap;
        }

        /// <summary>체크박스 토글(포스트잇 위젯의 uGUI Raycast가 직접 호출) — 완료<->미완료 왕복 모두 이
        /// 메서드 하나로 처리한다(17절 "체크 취소(다시 클릭) 3초간 허용"은 UI 레이어가 CompletedAtUnscaledTime
        /// 기준으로 버튼을 계속 노출할지만 판단하면 되고, 데이터 모델 자체는 왕복을 막지 않는다).</summary>
        public static void ToggleComplete(int id)
        {
            TodoItem item = FindActive(id);
            if (item == null) return;

            item.Completed = !item.Completed;
            item.CompletedAtUnscaledTime = item.Completed ? Time.unscaledTime : 0f;
            IsDirty = true;

            // ★★ 2026-09-06 재화 배선 — [오늘 할일] 하루 1회 보상은 <b>여기</b>에서 나간다.
            //    ★ <b>왜 UI가 아니라 모델인가.</b> 완료 판정을 부르는 UI는 둘이다
            //      (Interaction/TodoPostItWidget.cs · Interaction/TodoBoardPopover.cs).
            //      거기에 지급을 얹으면 «완료했다»의 정의가 두 벌이 되고, 셋째 진입점이 생기는 날
            //      그 길로 체크한 사용자만 조용히 보상을 못 받는다. 전이가 일어나는 자리는 여기 하나뿐이다.
            //    ★ 되돌리기(체크 해제)에는 지급하지 않는다 — 그래서 토글 결과가 «완료»일 때만 본다.
            //      반복 체크로 파밍할 수 없는 이유는 이 조건이 아니라 <c>todoCoinPaidToday</c>다
            //      (하루 1회, 롤오버가 되돌린다). 두 겹인 것이 의도다.
            //    ★ RaiseTodoListChanged <b>앞</b>에서 지급한다 — 그 이벤트로 갱신되는 화면이
            //      잔액을 그린다면 «이미 들어온 잔액»을 봐야 한다(원칙 1: 화면은 확정된 사실에서 파생된다).
            if (item.Completed) PayTodoDailyCoins();

            StickmanEventBus.RaiseTodoListChanged();
        }

        /// <summary>
        /// [오늘 할일] 하루 1회 정액 보상. 지급 여부·금액·리셋은 전부
        /// <see cref="CurrencyModel.TryPayTodoDailyCoins"/>와 <see cref="CurrencyRules.TodoDailyCoins"/>
        /// 안에 있고, 이 파일은 <b>언제 물어볼지</b>만 안다(금액을 여기 적으면 같은 사실이 두 곳에 산다).
        ///
        /// <para>★ <b>0동전일 때는 조용히 지나간다.</b> 오늘 두 번째 완료가 0인 것은 고장이 아니라
        /// 설계(하루 1회)이고, 할일을 여러 개 체크하는 것은 <b>흔한 정상 경로</b>라 매번 로그를 남기면
        /// 소음이 된다. 반대로 «오늘 첫 완료»는 하루 한 번뿐이라 한 줄이 값을 한다.</para>
        ///
        /// <para>★ <b>저장을 강제하지 않는다.</b> 지급이 <c>CurrencyModel.IsDirty</c>를 세우고
        /// <c>Interaction/CharacterProgressionDirector</c>의 주기/종료 저장이 싣는다 —
        /// 이 모델도 같은 파일에 실리므로 별도 경로를 만들면 두 컴포넌트가 같은 파일을 번갈아 쓴다.</para>
        /// </summary>
        private static void PayTodoDailyCoins()
        {
            int coins = CurrencyModel.TryPayTodoDailyCoins();
            if (coins <= 0) return;

            Debug.Log($"[재화] [오늘 할일] 오늘 첫 완료 +{coins}동전 — " +
                $"잔액 {CurrencyModel.CoinBalance}동전. 하루 1회이고 날짜가 바뀌면 다시 열립니다. " +
                "저장은 다음 주기/종료 저장에 실립니다.");
        }

        /// <summary>완료 유예 시간(todoCompletedLingerSeconds)이 지난 항목을 활성 목록에서 걷어내
        /// 완료함으로 옮긴다. Interaction/TodoPostItWidget.cs가 주기적으로 호출한다(정적 클래스라 자체
        /// Update()가 없음 — 폴링 주체는 MonoBehaviour 쪽).</summary>
        public static void SweepCompleted(float lingerSeconds)
        {
            bool changed = false;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                TodoItem item = _active[i];
                if (!item.Completed) continue;
                if (Time.unscaledTime - item.CompletedAtUnscaledTime < lingerSeconds) continue;

                _active.RemoveAt(i);
                _completedArchive.Add(item);
                changed = true;
            }
            if (!changed) return;
            IsDirty = true;
            StickmanEventBus.RaiseTodoListChanged();
        }

        /// <summary>항목 삭제(우클릭/스와이프, 17절). 완료함 항목은 삭제 대상이 아니다(데이터 보존 원칙).</summary>
        public static void Remove(int id)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id != id) continue;
                _active.RemoveAt(i);
                IsDirty = true;
                StickmanEventBus.RaiseTodoListChanged();
                return;
            }
        }

        /// <summary>테스트/디버그 전용 완전 초기화 — 정적 클래스라 씬을 다시 로드해도 목록이 그대로
        /// 살아남기 때문에, PlayMode 테스트가 서로의 목록을 물려받아 "빈 상태 예외"(17절) 검증이
        /// 실행 순서에 따라 통과/실패하는 것을 막는다(Core/StressGauge.ResetForTesting과 같은 이유,
        /// 같은 관례). 정상 게임플레이 경로에서는 호출되지 않는다.</summary>
        public static void ResetForTesting()
        {
            _active.Clear();
            _completedArchive.Clear();
            _pendingReminderText = null;
            _nextId = 1;
            IsDirty = false;
        }

        // ====================================================================================
        // ★★ 날짜 축 조회 — <b>탐색은 저장하지 않는다</b> (2026-09-08, persona-stress R-10)
        // ====================================================================================
        //
        // 신고 원문(R-10): *"여러 인스턴스: 창 위치도 할일도 같은 세이브 파일이고,
        // <see cref="CharacterSaveStore"/>의 «동시 인스턴스» 절이 «같은 버전 인스턴스끼리의 갱신
        // 손실은 범위 밖»이라고 명시한다. 날짜 이동마다 저장을 걸면 충돌 창이 넓어진다 —
        // 저장은 「사용자가 내용을 바꿨을 때」로 한정하고 탐색은 저장하지 마라."*
        //
        // ★ 그래서 이 절의 규약은 두 줄이다:
        //   ① 아래 조회 API는 <b>단 하나도</b> <see cref="IsDirty"/>를 세우지 않고
        //      <see cref="StickmanEventBus.RaiseTodoListChanged"/>를 쏘지 않는다. 읽기만 한다.
        //   ② <b>「지금 보고 있는 날짜」를 이 모델이 들고 있지 않는다.</b> 선택 상태는 화면의 것이고
        //      세이브 스키마에 칸이 없다 — 그래서 «이전/다음 날짜»를 아무리 눌러도 디스크에 닿을
        //      경로가 <b>구조적으로 존재하지 않는다</b>. (플래그로 «이건 저장 안 함»을 관리하는 대신
        //      저장할 것 자체를 만들지 않는 쪽을 골랐다. 플래그는 언젠가 한쪽이 잊는다.)
        //
        // ★ 「0 = 날짜 미상」의 계약도 여기서 지켜진다(UW-6-4): 미상 항목은 <b>어느 날짜 조회에도</b>
        //   잡히지 않는다. <see cref="TallyForDay"/>는 0을 물어도 빈 값을 돌려주고, 미상 항목만
        //   따로 보려면 <see cref="AppendUnknownPlannedDayItems"/>를 쓴다. 0을 1970-01-01로 읽는
        //   경로를 만들지 않기 위해 «날짜로 묻는 길»과 «미상을 묻는 길»을 아예 분리했다.

        /// <summary>어느 하루의 집계 — 달력 셀 하나가 필요로 하는 전부다(R6-5-3: 완료/전체 진행 막대 +
        /// 남은 것 점). <b>개수만</b> 있고 항목은 없다 — 달력은 지도지 보고서가 아니다.</summary>
        public readonly struct DayTally
        {
            public readonly int Total;
            public readonly int Completed;

            public DayTally(int total, int completed)
            {
                Total = total;
                Completed = completed;
            }

            /// <summary>그날 아직 안 끝난 것.</summary>
            public int Remaining => Total - Completed;

            /// <summary>그날 적어 둔 것이 하나라도 있는가(빈 칸과 «전부 완료»를 가르는 값).</summary>
            public bool HasAny => Total > 0;
        }

        /// <summary>
        /// 그날의 집계(활성 + 완료함 <b>합집합</b> — 데이터는 옮기지 않고 뷰가 합친다, R6-4).
        /// <para><paramref name="dayIndex"/>가 <see cref="TodoItem.UnknownPlannedDay"/>면 <b>항상 빈 값</b>이다 —
        /// 「날짜 미상」은 어느 날에도 세지 않는다는 계약(UW-6-4)이 여기서 강제된다.</para>
        /// <para>저장을 건드리지 않는다(위 규약 ①).</para>
        /// </summary>
        public static DayTally TallyForDay(int dayIndex)
        {
            if (dayIndex == TodoItem.UnknownPlannedDay) return default;

            int total = 0;
            int completed = 0;
            CountInto(_active, dayIndex, ref total, ref completed);
            CountInto(_completedArchive, dayIndex, ref total, ref completed);
            return new DayTally(total, completed);
        }

        private static void CountInto(List<TodoItem> source, int dayIndex, ref int total, ref int completed)
        {
            for (int i = 0; i < source.Count; i++)
            {
                TodoItem item = source[i];
                if (item.PlannedDayIndex != dayIndex) continue;
                total++;
                if (item.Completed) completed++;
            }
        }

        /// <summary>
        /// 그날의 항목을 <paramref name="buffer"/>에 <b>덧붙인다</b>(미완료 먼저, 그 다음 완료 —
        /// R6-7의 «한 목록, 두 그룹» 순서 그대로). 반환값은 이번에 넣은 개수.
        /// <para>새 리스트를 만들어 돌려주지 않는 이유: 이 앱은 하루 종일 켜져 있고, 날짜를 넘길 때마다
        /// 배열을 새로 굽는 것은 상주 앱에서 금지다(Core/DockGeometry가 명문화한 무할당 관례).
        /// 호출부가 자기 버퍼를 재사용한다.</para>
        /// <para><paramref name="dayIndex"/>가 «미상»이면 아무것도 넣지 않는다 — 위 계약과 같다.</para>
        /// <para>저장을 건드리지 않는다(위 규약 ①).</para>
        /// </summary>
        public static int AppendItemsForDay(int dayIndex, List<TodoItem> buffer)
        {
            if (buffer == null || dayIndex == TodoItem.UnknownPlannedDay) return 0;

            int before = buffer.Count;
            AppendMatching(_active, buffer, dayIndex, wantCompleted: false);
            AppendMatching(_active, buffer, dayIndex, wantCompleted: true);
            AppendMatching(_completedArchive, buffer, dayIndex, wantCompleted: true);
            return buffer.Count - before;
        }

        private static void AppendMatching(List<TodoItem> source, List<TodoItem> buffer,
            int dayIndex, bool wantCompleted)
        {
            for (int i = 0; i < source.Count; i++)
            {
                TodoItem item = source[i];
                if (item.PlannedDayIndex != dayIndex || item.Completed != wantCompleted) continue;
                buffer.Add(item);
            }
        }

        /// <summary>
        /// <b>날짜 미상</b> 항목만 덧붙인다 — v12 이하 세이브에서 올라온 것들이다(R6-6 마이그레이션 표:
        /// «오늘 페이지 최상단 그룹, 날짜 라벨을 그리지 않는다»). 반환값은 이번에 넣은 개수.
        /// <para><b>완료된 미상 항목은 넣지 않는다</b> — 그쪽은 [완료함] 탭의 몫이고, 오늘 페이지에
        /// 끌어올리면 «시간이 지나면 자연 소멸한다»는 이 그룹의 성질이 깨진다.</para>
        /// <para>저장을 건드리지 않는다(위 규약 ①).</para>
        /// </summary>
        public static int AppendUnknownPlannedDayItems(List<TodoItem> buffer)
        {
            if (buffer == null) return 0;

            int before = buffer.Count;
            for (int i = 0; i < _active.Count; i++)
            {
                TodoItem item = _active[i];
                if (item.IsPlannedDayKnown || item.Completed) continue;
                buffer.Add(item);
            }
            return buffer.Count - before;
        }

        /// <summary>
        /// <b>밀린 것</b>(날짜가 있고, 그 날짜가 오늘보다 이르고, 아직 안 끝난 것)을 덧붙인다 — R6-7.
        /// <para><b>데이터는 옮기지 않는다.</b> 이 항목들은 과거 날짜 페이지에도 그대로 남는다
        /// (달력이 원장으로 남아야 한다). 오늘 페이지가 «합쳐서 보여줄» 뿐이다.</para>
        /// <para>저장을 건드리지 않는다(위 규약 ①).</para>
        /// </summary>
        public static int AppendOverdueItems(int todayIndex, List<TodoItem> buffer)
        {
            if (buffer == null || todayIndex == TodoItem.UnknownPlannedDay) return 0;

            int before = buffer.Count;
            for (int i = 0; i < _active.Count; i++)
            {
                TodoItem item = _active[i];
                if (!item.IsPlannedDayKnown || item.Completed) continue;
                if (item.PlannedDayIndex >= todayIndex) continue;
                buffer.Add(item);
            }
            return buffer.Count - before;
        }

        /// <summary>
        /// 적어 둔 것 중 <b>가장 이른 날짜</b>. 없으면 false — 날짜 탐색의 하한을 정하는 데 쓴다
        /// (R6-8 #5: 빈 달을 무한히 넘길 수 있으면 사용자는 그걸 고장으로 읽는다).
        /// <para>«미상»은 후보가 아니다 — 0을 하한으로 삼는 순간 탐색이 1970년까지 열린다.</para>
        /// <para>저장을 건드리지 않는다(위 규약 ①).</para>
        /// </summary>
        public static bool TryGetEarliestPlannedDay(out int dayIndex)
        {
            dayIndex = TodoItem.UnknownPlannedDay;
            bool found = false;
            found |= ScanEarliest(_active, ref dayIndex);
            found |= ScanEarliest(_completedArchive, ref dayIndex);
            return found;
        }

        private static bool ScanEarliest(List<TodoItem> source, ref int earliest)
        {
            bool found = false;
            for (int i = 0; i < source.Count; i++)
            {
                TodoItem item = source[i];
                if (!item.IsPlannedDayKnown) continue;
                if (!found && earliest == TodoItem.UnknownPlannedDay) earliest = item.PlannedDayIndex;
                else if (item.PlannedDayIndex < earliest) earliest = item.PlannedDayIndex;
                found = true;
            }
            return found;
        }

        // ==================== 영속화 (저장 스키마 v4 · 날짜는 v13) ====================

        /// <summary>
        /// 저장 파일에서 목록을 되살린다. <b>이벤트를 쏘지 않는다</b> — 복원 도중의 중간 상태를 UI가
        /// 그리지 않게 하는 관례(CharacterSaveStore.Load가 전부 끝난 뒤 한 번만 통지한다).
        /// Id는 파일에 있던 값을 그대로 쓰고 다음 Id를 그보다 크게 올려, 재시작 후 추가한 항목이
        /// 옛 항목과 같은 Id를 갖는 사고(엉뚱한 줄이 체크되는)를 막는다.
        /// </summary>
        internal static void RestoreFromSave(TodoItem[] active, TodoItem[] archive)
        {
            _active.Clear();
            _completedArchive.Clear();
            _nextId = 1;

            AppendRestored(_active, active);
            AppendRestored(_completedArchive, archive);
            IsDirty = false;
        }

        private static void AppendRestored(List<TodoItem> target, TodoItem[] source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
            {
                TodoItem item = source[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Text)) continue;

                // 완료 시각은 지난 세션의 Time.unscaledTime이라 이번 실행에서는 의미가 없다(이번 시계는
                // 0에서 다시 시작한다). 0으로 두면 다음 Sweep에서 정상적으로 완료함으로 넘어간다 —
                // 그대로 두면 "지난 세션 시각 - 지금"이 음수라 영원히 유예 상태로 남는다.
                item.CompletedAtUnscaledTime = 0f;
                target.Add(item);
                if (item.Id >= _nextId) _nextId = item.Id + 1;
            }
        }

        internal static void MarkSaved() => IsDirty = false;

        private static TodoItem FindActive(int id)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id == id) return _active[i];
            }
            return null;
        }
    }
}
