using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 12절 드래그&던지기의 진입/해제 트리거 배선. 실제 물리/속도 계산은
    /// States/DragThrowState.cs가 전담하고, 이 컨트롤러는 "언제 그 상태로 들어가고 나가는지" — 클릭
    /// 히트박스 이벤트, 부분적 클릭관통 해제(15절), 스펙터클 상호배제 락(16-15) — 만 담당한다. States
    /// 계층은 이 컨트롤러의 존재를 전혀 모른다(Enter() 호출 자체가 "확정" 신호라는 원칙 그대로 유지).
    /// </summary>
    public sealed class DragThrowController : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickmanClickHitbox _hitbox;
        [SerializeField] private Collider2D _hitboxCollider;

        private ILocalClickCaptureService _clickCapture;

        private void Awake()
        {
            // 같은 GameObject에 StickmanClickHitbox/Collider2D가 붙어 있는 통상 배치라면 인스펙터
            // 수동 배선 없이도 동작하게 하는 편의 폴백(RagdollLimbImpactRelay.Reset() 컨벤션과 동일 정신).
            // _player는 다른 GameObject(캐릭터 루트)를 가리켜야 하므로 자동 추론하지 않는다.
            if (_hitbox == null) _hitbox = GetComponent<StickmanClickHitbox>();
            if (_hitboxCollider == null) _hitboxCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            if (_hitbox != null)
            {
                _hitbox.MouseDown += OnMouseDown;
                _hitbox.MouseUp += OnMouseUp;
            }
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            if (_hitbox != null)
            {
                _hitbox.MouseDown -= OnMouseDown;
                _hitbox.MouseUp -= OnMouseUp;
            }
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;

            // BUG-P3-M1(Major, docs/BUG_REPORT_PHASE3.md) 대응 — BattleMinigameDirector와 동일한
            // 근거: OnStateTransitioned 구독을 이미 위에서 해제했으므로 더 이상 자동으로 락이
            // 풀리지 않는다. 여기서 직접 반환한다(멱등 — Release()/ReleaseLocalClickCapture()가
            // 소유자 확인 후 no-op하므로 중복 호출해도 안전).
            ReleaseOwnedLocks();
        }

        /// <summary>지금 이 컴포넌트가 소유 중인 락(SpectacleEventLock/ILocalClickCaptureService)을 반환한다.</summary>
        private void ReleaseOwnedLocks()
        {
            if (_player != null && _player.Blackboard != null && _player.Blackboard.Machine != null &&
                _player.Blackboard.Machine.CurrentStateId == StickmanStateId.Dragged)
            {
                // Exit()가 Kinematic->Dynamic 방어적 복구를 담당하므로 강제 Idle 전이로 안전하게 놓아준다.
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }

            _clickCapture?.ReleaseLocalClickCapture(this);
            _clickCapture = null;
            SpectacleEventLock.Release(this);
        }

        private void Update()
        {
            // 15절 제약 1(동적 히트박스 추적): 드래그 중엔 매 프레임 히트박스 영역을 최신 좌표로 갱신한다.
            if (_clickCapture == null || _player == null) return;
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.Dragged) return;
            _clickCapture.UpdateLocalClickCaptureRegion(ComputeHitboxOsRect(), this);
        }

        private void OnMouseDown()
        {
            if (_player == null) return;

            // UX 12절 예외: RAGDOLL/GETUP/ParkourClimb 등 물리·이동 우선 상태 도중엔 새 드래그 시도를
            // 무시한다. 다른 스펙터클 이벤트가 이미 활성 중이어도 무시(16-15 상호배제).
            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) return;
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.DragAndThrow, this)) return;

            _clickCapture = _player.PlatformService as ILocalClickCaptureService;
            Rect hitboxOs = ComputeHitboxOsRect();
            if (_clickCapture != null && !_clickCapture.RequestLocalClickCapture(hitboxOs, this))
            {
                // 이론상 SpectacleEventLock을 이미 확보했으므로 여기서 실패할 일은 없지만(같은 owner
                // 토큰), 방어적으로 락을 되돌린다.
                SpectacleEventLock.Release(this);
                return;
            }

            _player.Blackboard.Machine.ChangeState(StickmanStateId.Dragged);
        }

        private void OnMouseUp()
        {
            if (_player == null) return;
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.Dragged) return;
            _player.Blackboard.DragReleaseSignaled = true;
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            // Dragged를 벗어나는 모든 경로(정상 놓기/타임아웃/전체화면 강제취소)에서 공통으로 락을 해제.
            if (evt.From != StickmanStateId.Dragged) return;
            _clickCapture?.ReleaseLocalClickCapture(this);
            _clickCapture = null;
            SpectacleEventLock.Release(this);
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player == null) return;
            // 트레이 긴급정지 — DragThrowState.Tick()이 다음 프레임에 이 신호를 소비해 즉시 놓임 처리한다.
            _player.Blackboard.DragReleaseSignaled = true;
        }

        private Rect ComputeHitboxOsRect()
        {
            if (_player == null) return default;
            return ClickHitboxRectUtility.ComputeOsRect(_hitboxCollider, _player.Blackboard.MainCamera, _player.Blackboard.Config);
        }
    }
}
