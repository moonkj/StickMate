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

        // 개선 R2(docs/CODE_REVIEW_FINAL.md): 3단계 보일러플레이트를 SpectacleEventLock.ReleaseIfOwned로 추출.
        private void ReleaseOwnedLock()
        {
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null, StickmanStateId.TodoReminder);
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

        /// <summary>강조할 할일이 하나도 없을 때의 문구(36-7 표와 1:1).</summary>
        public const string NoTodoReason = "아직 적어둔 할일이 없어요";

        /// <summary>
        /// ★ 지금 리마인더를 강제 발동할 수 있는가 — 회색 처리와 실제 실행이 함께 쓰는 단 하나의 판정
        /// (docs/UX_FLOW.md 36-7). 이 경로는 (다) 개발 전용이지만 판정 구조는 나머지 6개와 같게 둔다:
        /// 두 벌의 판정이 생기는 것을 막는 규칙에 예외를 두면 그 예외가 다음 함정이 된다.
        /// </summary>
        public CommandAvailability GetAvailability()
        {
            if (_player == null || _config == null || _player.Blackboard == null || _player.Blackboard.Machine == null)
                return CommandAvailability.Missing;

            if (TodoListModel.UncompletedCount <= 0)
                return CommandAvailability.Blocked(NoTodoReason);

            StickmanStateId current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(current));

            if (SpectacleEventLock.IsActive)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(SpectacleEventLock.ActiveKind));

            return CommandAvailability.Ready;
        }

        /// <summary>
        /// 리마인더 강제 발동(개발 전용 ⌃⌥⌘J). 자동 트리거가 45초 주기 20% 추첨이라 확률만으로는
        /// 실물 검증이 사실상 불가능해 확률/주기만 건너뛴다 — 상호배제 락과 진입 상태 조건(Idle/Walk)은
        /// 그대로 지킨다.
        ///
        /// ============================================================================
        /// ★★ 2026-08-31 버그 수정 — 이 경로는 더 이상 <b>사용자의 진짜 목록에 쓰지 않는다</b>
        /// ============================================================================
        /// 종전에는 목록이 비어 있으면 데모 할일 3건("보고서 초안 쓰기"/"장보기"/"세탁물 찾기")을
        /// <c>TodoListModel.Add</c>로 <b>실제 목록에 넣고 저장까지 남겼다</b>. 그 시절의 논거는
        /// "Add 호출자가 프로젝트 전체에 0건이라 투두 기능이 도달 불가능하다"였는데, 부채꼴 ④
        /// <see cref="TodoBoardPopover"/>에 입력칸이 생기면서 <b>그 전제가 더 이상 사실이 아니게 됐다</b>.
        /// 남은 것은 사용자가 적지 않은 항목이 자기 목록에 나타나 저장 파일에까지 남는 것뿐이며,
        /// 그것은 게이트로 숨기고 말고와 무관한 <b>데이터 오염</b>이다(CLAUDE.md 원칙 1·3).
        ///
        /// 그래서 데모 시딩을 <b>지웠다</b>(끄기 위해 남겨두지 않는다 — 죽은 채 남은 쓰기 경로는 반드시
        /// 되살아난다). 목록이 비어 있으면 이 명령은 <see cref="NoTodoReason"/>으로 <b>거절</b>되고,
        /// 검증하려는 사람은 부채꼴 ④의 입력칸이라는 <b>정식 경로</b>로 할일을 하나 적으면 된다.
        /// </summary>
        /// <returns>실제로 리마인더를 시작했는가.</returns>
        public bool ForceTriggerNow(string reason)
        {
            CommandAvailability availability = GetAvailability();
            if (!availability.IsReady)
            {
                Debug.Log($"[투두] 리마인더 강제 발동({reason}) 건너뜀 — {availability.Reason}. " +
                    "★ 이 경로는 데모 할일을 만들지 않는다(2026-08-31 수정) — 할일은 부채꼴 [오늘 할일] " +
                    "입력칸으로만 들어온다. 포스트잇 카드는 상태와 무관하게 그대로 떠 있다.");
                return false;
            }

            if (!TryPickFeaturedTodo(out string text)) return false;
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.TodoReminder, this)) return false;

            TodoListModel.SetPendingReminderText(text);
            _player.Blackboard.Machine.ChangeState(StickmanStateId.TodoReminder);
            Debug.Log($"[투두] 들고 다니는 모드 강제 발동({reason}) — 강조 할일 \"{text}\", " +
                $"{_config.todoReminderHoldSeconds:F1}초 홀드. 종이는 TodoReminderRenderer가, " +
                "텍스트는 말풍선이 그린다(원칙 1 — 전이가 확정된 뒤 그 상태에서만 파생).");
            return true;
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
