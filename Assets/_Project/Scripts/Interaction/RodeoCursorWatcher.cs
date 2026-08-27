using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 13절 로데오 커서 트리거 감시자 — 클릭 불필요, 커서 정지 감지만으로 발동한다
    /// (15절 부분적 클릭관통 해제 대상 아님, 13절에 명시). 커서 좌표는 9절-3에서 이미 마련된 전역 폴링
    /// 채널(StickmanAgent.TryGetCursorPosition, ICursorPositionService)을 그대로 재사용한다 — 신규
    /// 폴링 채널을 만들지 않는다.
    /// </summary>
    public sealed class RodeoCursorWatcher : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        private Vector2 _lastCursorOs;
        private bool _hasLastCursor;
        private float _stillTimer;

        private void OnEnable()
        {
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;
        }

        private void Update()
        {
            if (_player == null || _config == null) return;

            if (!_player.TryGetCursorPosition(out Vector2 cursorOs))
            {
                _hasLastCursor = false;
                _stillTimer = 0f;
                return;
            }

            if (_hasLastCursor && Vector2.Distance(cursorOs, _lastCursorOs) <= _config.rodeoStillRadiusPx)
            {
                _stillTimer += Time.deltaTime;
            }
            else
            {
                _stillTimer = 0f;
            }
            _lastCursorOs = cursorOs;
            _hasLastCursor = true;

            if (_stillTimer < _config.rodeoStillTriggerSeconds) return;

            TryTrigger(cursorOs);
        }

        private void TryTrigger(Vector2 cursorOs)
        {
            if (SpectacleEventLock.IsActive) return;

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) return;

            var blackboard = _player.Blackboard;
            if (blackboard.MainCamera == null || blackboard.Body == null) return;

            // "캐릭터가 도달 가능 거리에 있을 때" 판정 — 캐릭터/커서를 같은 OS 화면 좌표계로 비교.
            Vector2 charOs = ScreenCoordinateConverter.WorldToOsScreen(blackboard.MainCamera, blackboard.Body.position, blackboard.Config, out _);
            if (Vector2.Distance(charOs, cursorOs) > _config.rodeoReachDistancePx) return;

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.RodeoCursor, this)) return;

            _stillTimer = 0f; // 즉시 재트리거 방지(다음 5초를 다시 채워야 함).
            blackboard.Machine.ChangeState(StickmanStateId.RodeoCursor);
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (evt.From != StickmanStateId.RodeoCursor) return;
            SpectacleEventLock.Release(this);
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player == null) return;
            if (_player.Blackboard.Machine.CurrentStateId == StickmanStateId.RodeoCursor)
            {
                // 3차 안전망(13절) — 트레이 긴급정지는 항상 유효한 전역 안전판.
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
        }
    }
}
