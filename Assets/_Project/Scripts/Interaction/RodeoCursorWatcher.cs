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
    ///
    /// BUG-P5-M2 대응(Major, docs/BUG_REPORT_PHASE5.md): UX 24절 "1단계(인질극/로데오는 스트레스 게이지가
    /// 중간 수준이면 발동 확률에 가중치)"가 이전 라운드에는 전혀 배선되지 않았었다. 지금은
    /// GetEffectiveStillTriggerSeconds()가 StressGauge.CurrentLevel을 약한 가중치로 반영한다 — 임계값
    /// (StickConfig.stressRodeoWeightThreshold) 이상이면 정지 판정 시간(rodeoStillTriggerSeconds)을
    /// 완만하게 단축(stressRodeoTriggerSecondsMultiplier)해 "로데오가 좀 더 자주 발동"하는 정도로만
    /// 반영한다 — 발동 자체를 확률 판정으로 바꾸거나 조건을 새로 추가하지 않는다(기존 "정지 시간 도달"
    /// 조건 그대로, 그 시간만 스트레스에 따라 짧아짐).
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

            // BUG-P3-M1(Major, docs/BUG_REPORT_PHASE3.md) 대응 — 기존 OnEmergencyStop()과 같은 판정을
            // 재사용하되, 위에서 이미 OnStateTransitioned 구독을 해제했으므로 SpectacleEventLock을
            // 여기서 직접 반환해야 한다(멱등 — 소유자 확인 후 no-op하므로 중복 호출해도 안전).
            ReleaseOwnedLock();
        }

        // 개선 R2(docs/CODE_REVIEW_FINAL.md): 3단계 보일러플레이트를 SpectacleEventLock.ReleaseIfOwned로 추출.
        private void ReleaseOwnedLock()
        {
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null, StickmanStateId.RodeoCursor);
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

            if (_stillTimer < GetEffectiveStillTriggerSeconds()) return;

            TryTrigger(cursorOs);
        }

        /// <summary>BUG-P5-M2 대응 — 스트레스 게이지가 임계값 이상이면 정지 판정 시간을 완만하게
        /// 단축해 로데오가 좀 더 자주 발동하도록 한다(24절 "1단계 발동 확률 가중치"의 약한 반영).</summary>
        private float GetEffectiveStillTriggerSeconds()
        {
            float baseSeconds = _config.rodeoStillTriggerSeconds;
            if (StressGauge.CurrentLevel < _config.stressRodeoWeightThreshold) return baseSeconds;

            float multiplier = Mathf.Max(0.1f, _config.rodeoStressTriggerSecondsMultiplier);
            return baseSeconds * multiplier;
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
