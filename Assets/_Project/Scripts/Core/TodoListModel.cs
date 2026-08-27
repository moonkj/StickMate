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
        public readonly int Id;
        public readonly string Text;
        public bool Completed;

        /// <summary>완료 체크 시각(Time.unscaledTime 스냅샷) — todoUndoWindowSeconds/
        /// todoCompletedLingerSeconds 판정에 사용. 미완료 상태면 의미 없음(0).</summary>
        public float CompletedAtUnscaledTime;

        public TodoItem(int id, string text)
        {
            Id = id;
            Text = text;
        }
    }

    /// <summary>
    /// docs/UX_FLOW.md 17절 투두 말풍선의 데이터 소스 — 정적 클래스(SpectacleEventLock/StressGauge와
    /// 동일한 이유: 24시간 상주 앱, 씬 생명주기와 무관한 단일 프로세스 전역 상태). 활성(미완료) 목록과
    /// 완료함(누적 보존, 리스트에서는 사라져도 데이터는 보존 — 17절 명시)을 분리해 보관한다.
    ///
    /// 소비자: Interaction/TodoReminderDirector.cs(들고 다니는 모드 트리거), Interaction/
    /// TodoPostItWidget.cs(포스트잇 카드 UI — 이 위젯의 체크박스 클릭은 이 모델을 직접 호출할 뿐,
    /// SpectacleEventLock/Platform.ILocalClickCaptureService와는 완전히 무관하다).
    /// </summary>
    public static class TodoListModel
    {
        private static readonly List<TodoItem> _active = new List<TodoItem>(16);
        private static readonly List<TodoItem> _completedArchive = new List<TodoItem>(16);
        private static readonly ReadOnlyCollection<TodoItem> _activeReadOnly = _active.AsReadOnly();
        private static readonly ReadOnlyCollection<TodoItem> _archiveReadOnly = _completedArchive.AsReadOnly();
        private static int _nextId = 1;

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

        /// <summary>새 할일을 추가한다. 항상 성공(강제 차단 없음 — 17절 "추가 자체는 막지 않음").
        /// 반환값은 활성 개수가 소프트캡을 넘겼는지(호출자가 "먼저 정리해볼까?" 안내를 띄울지 판단하는 용도).</summary>
        public static bool Add(string text, int softCap)
        {
            if (string.IsNullOrWhiteSpace(text)) return UncompletedCount > softCap;
            _active.Add(new TodoItem(_nextId++, text.Trim()));
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
            StickmanEventBus.RaiseTodoListChanged();
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
            if (changed) StickmanEventBus.RaiseTodoListChanged();
        }

        /// <summary>항목 삭제(우클릭/스와이프, 17절). 완료함 항목은 삭제 대상이 아니다(데이터 보존 원칙).</summary>
        public static void Remove(int id)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id != id) continue;
                _active.RemoveAt(i);
                StickmanEventBus.RaiseTodoListChanged();
                return;
            }
        }

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
