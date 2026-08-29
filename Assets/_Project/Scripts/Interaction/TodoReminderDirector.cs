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

        /// <summary>
        /// 17절 "[+ 할일 추가]"가 아직 없는 지금(설정창/트레이 미구현) 데모 목록에 넣는 문구.
        ///
        /// ★ CLAUDE.md 불변 원칙 3 — 이 문자열들은 <b>전부 이 소스 파일에 박힌 리터럴</b>이다.
        /// 유저의 실제 캘린더/미리알림/할일 앱/파일을 읽어오는 코드는 이 프로젝트 어디에도 없고
        /// (Tests/EditMode/UserAssetImmutabilityAuditTests.cs가 정적으로 스캔한다), 이 기능의 데이터는
        /// 처음부터 끝까지 Core/TodoListModel.cs의 앱 내부 상태뿐이다.
        /// </summary>
        private static readonly string[] DemoTodoTexts =
        {
            "보고서 초안 쓰기",
            "장보기",
            "세탁물 찾기",
        };

        /// <summary>
        /// 투두 데모 경로(Ctrl+Opt+Cmd+J / 우클릭 메뉴) — 두 가지를 한 번에 한다.
        ///
        /// (1) <b>할일 추가</b>: 17절의 트리거는 "설정창 또는 트레이 메뉴의 [+ 할일 추가]"인데 둘 다
        ///     아직 없어서, <c>Core.TodoListModel.Add</c>를 호출하는 코드가 프로젝트 전체에 <b>0건</b>
        ///     이었다. 즉 목록이 영원히 비어 있었고, 그래서 포스트잇 카드는 17절의 "빈 상태 예외"에
        ///     따라 항상 숨겨졌고 이 Director의 유휴 추첨도 <c>UncompletedCount &lt;= 0</c>에서 매번
        ///     즉시 return했다 — 기능 전체가 <b>도달 불가능</b>했다. 목록이 비어 있을 때만 데모 3건을
        ///     넣어 그 경로를 열어준다.
        /// (2) <b>리마인더 강제 발동</b>: 자동 트리거가 45초 주기 20% 추첨이라 확률만으로는 실물 검증이
        ///     사실상 불가능하다 — 다른 Director의 ForceTriggerNow와 같은 성격. 확률/주기만 건너뛰고
        ///     상호배제 락과 진입 상태 조건(Idle/Walk)은 그대로 지킨다.
        /// </summary>
        public void ForceTriggerNow(string reason)
        {
            if (_player == null || _config == null || _player.Blackboard == null || _player.Blackboard.Machine == null)
            {
                Debug.LogWarning($"[투두] 강제 발동 실패({reason}) — 플레이어/설정 배선이 없습니다.");
                return;
            }

            if (TodoListModel.UncompletedCount <= 0)
            {
                for (int i = 0; i < DemoTodoTexts.Length; i++)
                {
                    TodoListModel.Add(DemoTodoTexts[i], _config.todoActiveCountSoftCap);
                }
                Debug.Log($"[투두] 데모 할일 {DemoTodoTexts.Length}건 추가({reason}) — " +
                    $"미완료 {TodoListModel.UncompletedCount}건. 화면 우상단 포스트잇 카드가 나타나고 " +
                    $"최대 {_config.todoPostItMaxVisibleRows}줄까지 보인다(17절). " +
                    "★ 실제 캘린더/할일 앱은 읽지 않는다 — 전부 이 소스에 박힌 리터럴이다(원칙 3).");
            }

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
            {
                Debug.Log($"[투두] 리마인더 강제 발동({reason}) — 지금은 {current} 중이라 종이 꺼내기는 건너뜁니다" +
                    "(포스트잇 카드는 상태와 무관하게 그대로 떠 있다).");
                return;
            }

            if (SpectacleEventLock.IsActive)
            {
                Debug.Log($"[투두] 리마인더 강제 발동({reason}) — 다른 스펙터클({SpectacleEventLock.ActiveKind})이 " +
                    "진행 중이라 건너뜁니다(16-15/28절-29 상호배제는 강제 경로에서도 그대로 지킨다).");
                return;
            }

            if (!TryPickFeaturedTodo(out string text)) return;
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.TodoReminder, this)) return;

            TodoListModel.SetPendingReminderText(text);
            _player.Blackboard.Machine.ChangeState(StickmanStateId.TodoReminder);
            Debug.Log($"[투두] 들고 다니는 모드 강제 발동({reason}) — 강조 할일 \"{text}\", " +
                $"{_config.todoReminderHoldSeconds:F1}초 홀드. 종이는 TodoReminderRenderer가, " +
                "텍스트는 말풍선이 그린다(원칙 1 — 전이가 확정된 뒤 그 상태에서만 파생).");
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
