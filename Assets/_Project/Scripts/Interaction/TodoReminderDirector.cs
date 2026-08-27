using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 17절 투두 말풍선 "들고 다니는 모드"의 유휴 트리거를 전담한다. 실제 종이 꺼내기
    /// 연출/대사 홀드는 States/TimedSpectacleState.cs(StickmanStateId.TodoReminder)가 담당한다 —
    /// 이 컨트롤러는 WindowTheftDirector/GraffitiDirector와 동일한 "언제 트리거하는지"만의 책임을 진다.
    ///
    /// "포스트잇 모드"(다건 노출)는 이 컨트롤러와 무관하다 — Interaction/TodoPostItWidget.cs가 완전히
    /// 독립적인 Canvas UI 경로로 담당한다(같은 UX 17절이지만 서로 다른 두 표현 방식).
    /// </summary>
    public sealed class TodoReminderDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        private float _checkTimer;

        private void OnEnable()
        {
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;
            ReleaseOwnedLock();
        }

        private void ReleaseOwnedLock()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player != null && _player.Blackboard != null && _player.Blackboard.Machine != null &&
                _player.Blackboard.Machine.CurrentStateId == StickmanStateId.TodoReminder)
            {
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
            SpectacleEventLock.Release(this);
        }

        private void Update()
        {
            if (_player == null || _config == null) return;
            if (_player.IsSuspended) return;

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) { _checkTimer = 0f; return; }

            _checkTimer += Time.deltaTime;
            float interval = Mathf.Max(1f, _config.todoReminderCheckInterval);
            if (_checkTimer < interval) return;
            _checkTimer = 0f;

            // 17절: "할일 0개 -> 캐릭터도 종이를 꺼내는 동작을 하지 않음"(빈 상태 예외).
            if (TodoListModel.UncompletedCount <= 0) return;
            if (SpectacleEventLock.IsActive) return; // 16-15/28절-29 상호배제
            if (Random.value >= _config.todoReminderChance) return;

            if (!TryPickFeaturedTodo(out string text)) return;
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.TodoReminder, this)) return;

            TodoListModel.SetPendingReminderText(text);
            _player.Blackboard.Machine.ChangeState(StickmanStateId.TodoReminder);
        }

        /// <summary>"1개 강조" — 가장 오래된 미완료 항목을 고른다(TodoListModel.ActiveItems는 FIFO 순서).</summary>
        private bool TryPickFeaturedTodo(out string text)
        {
            var items = TodoListModel.ActiveItems;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Completed) continue;
                text = items[i].Text;
                return true;
            }
            text = null;
            return false;
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (evt.From != StickmanStateId.TodoReminder) return;
            if (evt.To == StickmanStateId.TodoReminder) return; // self-transition 방어(다른 Director들과 동일 관례)
            SpectacleEventLock.Release(this);
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            ReleaseOwnedLock();
        }
    }
}
