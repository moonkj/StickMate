using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 10절 격파 미니게임의 진입/해제 트리거 배선 — 유휴 저확률 자동 발동 + 트레이
    /// 메뉴 수동 발동(<see cref="TriggerManually"/>) 둘 다 지원한다. 실제 게이지/스위트스팟/재도전
    /// 로직은 States/BattleMinigameState.cs가 전담하고, 이 컨트롤러는 "언제 시작하고 언제 락을
    /// 해제하는지" + "캐릭터 히트박스 클릭을 상태에 전달하는지"만 담당한다.
    /// </summary>
    public sealed class BattleMinigameDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickmanClickHitbox _hitbox;
        [SerializeField] private Collider2D _hitboxCollider;
        [SerializeField] private StickConfig _config;

        private ILocalClickCaptureService _clickCapture;
        private float _idleCheckTimer;

        private void Awake()
        {
            // DragThrowController.Awake()와 동일한 편의 폴백 — 같은 GameObject에 StickmanClickHitbox/
            // Collider2D가 붙어 있는 통상 배치라면 인스펙터 수동 배선 없이도 동작한다.
            if (_hitbox == null) _hitbox = GetComponent<StickmanClickHitbox>();
            if (_hitboxCollider == null) _hitboxCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            if (_hitbox != null) _hitbox.MouseDown += OnHitboxMouseDown;
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            if (_hitbox != null) _hitbox.MouseDown -= OnHitboxMouseDown;
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;
        }

        /// <summary>트레이 메뉴 "격파 놀이"(10절 수동 트리거)에서 호출할 공개 진입점. 이미 다른
        /// 스펙터클 이벤트가 활성 중이면 조용히 실패(false)한다.</summary>
        public bool TriggerManually() => TryBegin();

        private void Update()
        {
            if (_clickCapture != null && _player != null &&
                _player.Blackboard.Machine.CurrentStateId == StickmanStateId.BattleMinigame)
            {
                _clickCapture.UpdateLocalClickCaptureRegion(ComputeHitboxOsRect(), this);
            }

            TickAutoTrigger();
        }

        private void TickAutoTrigger()
        {
            if (_player == null || _config == null) return;

            // "코어 루프 유휴 상태가 일정 시간 누적되면" — 유휴(Idle) 상태가 아니면 누적 타이머를 리셋.
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.Idle)
            {
                _idleCheckTimer = 0f;
                return;
            }

            _idleCheckTimer += Time.deltaTime;
            float interval = Mathf.Max(1f, _config.battleAutoTriggerCheckInterval);
            if (_idleCheckTimer < interval) return;
            _idleCheckTimer = 0f;

            if (Random.value < _config.battleAutoTriggerChance) TryBegin();
        }

        private bool TryBegin()
        {
            if (_player == null) return false;

            // 11절과 상호 배제(동시 발동 금지) — SpectacleEventLock 하나로 모든 스펙터클/개입 이벤트를 통제.
            if (SpectacleEventLock.IsActive) return false;

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) return false;

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.BattleMinigame, this)) return false;

            _clickCapture = _player.PlatformService as ILocalClickCaptureService;
            if (_clickCapture != null && !_clickCapture.RequestLocalClickCapture(ComputeHitboxOsRect(), this))
            {
                SpectacleEventLock.Release(this);
                return false;
            }

            _player.Blackboard.Machine.ChangeState(StickmanStateId.BattleMinigame);
            return true;
        }

        private void OnHitboxMouseDown()
        {
            if (_player == null) return;
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.BattleMinigame) return;
            _player.Blackboard.BattleClickSignaled = true;
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (evt.From != StickmanStateId.BattleMinigame) return;
            _clickCapture?.ReleaseLocalClickCapture(this);
            _clickCapture = null;
            SpectacleEventLock.Release(this);
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player == null) return;
            if (_player.Blackboard.Machine.CurrentStateId == StickmanStateId.BattleMinigame)
            {
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
        }

        private Rect ComputeHitboxOsRect()
        {
            if (_player == null) return default;
            return ClickHitboxRectUtility.ComputeOsRect(_hitboxCollider, _player.Blackboard.MainCamera, _player.Blackboard.Config);
        }
    }
}
