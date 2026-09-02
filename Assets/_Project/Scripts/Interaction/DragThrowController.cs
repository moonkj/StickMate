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

        private void Start()
        {
            // 실측 검증용 준비 상태 로그(리더 지시) — "드래그가 실제로 발동할 준비가 됐는가"를 한 줄로
            // 확인할 수 있게 한다. 이 줄이 안 보이면 컴포넌트 자체가 씬/프리팹에 배선되지 않은 것이다
            // (이번 라운드에 실제로 그랬다 — Tasklist.md 참고).
            Debug.Log($"[DragThrowController] [0/6] 준비 완료 — player={(_player != null)}, hitbox={(_hitbox != null)}, " +
                $"hitboxCollider={(_hitboxCollider != null ? _hitboxCollider.GetType().Name : "(없음)")}, " +
                $"부분클릭관통해제 서비스={((_player != null ? _player.PlatformService as ILocalClickCaptureService : null) != null ? "지원" : "미지원(소유권 부기 생략)")}. " +
                "캐릭터를 마우스로 누르면 Dragged 상태로 전이합니다.");
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

            // BUG-P3-M1(Major, docs/BUG_REPORT_PHASE3.md) 대응 — (당시) BattleMinigameDirector와 동일한
            // 근거: OnStateTransitioned 구독을 이미 위에서 해제했으므로 더 이상 자동으로 락이
            // 풀리지 않는다. 여기서 직접 반환한다(멱등 — Release()/ReleaseLocalClickCapture()가
            // 소유자 확인 후 no-op하므로 중복 호출해도 안전).
            ReleaseOwnedLocks();
        }

        /// <summary>
        /// 개선 R2(docs/CODE_REVIEW_FINAL.md): 3단계 보일러플레이트를 SpectacleEventLock.ReleaseIfOwned로
        /// 추출했다(당시 BattleMinigameDirector.ReleaseOwnedLocks()와 동일한 근거로 소유권 선확인을 추가해도
        /// 동작은 동일 — SpectacleEventLock.ReleaseIfOwned 문서 참고). Exit()가 Kinematic->Dynamic
        /// 방어적 복구를 담당하므로 강제 Idle 전이로 안전하게 놓아준다.
        /// </summary>
        private void ReleaseOwnedLocks()
        {
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null,
                StickmanStateId.Dragged, _clickCapture);
            _clickCapture = null;
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
            // 15절 제약 1(동적 히트박스 추적): 드래그 중엔 매 프레임 히트박스 영역을 최신 좌표로 갱신한다.
            if (_clickCapture == null || _player == null) return;
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.Dragged) return;
            _clickCapture.UpdateLocalClickCaptureRegion(ComputeHitboxOsRect(), this);
        }

        /// <summary>
        /// ★ 2026-08-28 — 모든 조기 반환에 **실패 사유 로그**를 붙였다(리더 지시). 사용자 신고
        /// "마우스로 안 잡힘"을 조사할 때 Player.log에 `[StickmanClickHitbox] 마우스다운 감지`만 찍히고
        /// 그 뒤가 완전히 침묵해서, 어느 가드에서 되돌아간 것인지 전혀 알 수 없었던 것이 진단을
        /// 지연시킨 직접 원인이다. 조용한 no-op을 남기지 않는다는 이 프로젝트 컨벤션을 이 메서드에도
        /// 적용한다.
        /// </summary>
        private void OnMouseDown()
        {
            if (_player == null)
            {
                Debug.LogWarning("[DragThrowController] [2/6] 드래그 진입 실패 — _player 참조가 null입니다(프리팹 배선 확인 필요).");
                return;
            }

            // UX 12절 예외: RAGDOLL/GETUP/ParkourClimb 등 물리·이동 우선 상태 도중엔 새 드래그 시도를
            // 무시한다. 다른 스펙터클 이벤트가 이미 활성 중이어도 무시(16-15 상호배제).
            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
            {
                Debug.Log($"[DragThrowController] [2/6] 드래그 진입 무시 — 현재 상태가 {current}라서(Idle/Walk에서만 잡을 수 있음).");
                return;
            }

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.DragAndThrow, this))
            {
                Debug.Log($"[DragThrowController] [2/6] 드래그 진입 실패 — SpectacleEventLock을 " +
                    $"{SpectacleEventLock.ActiveKind}(소유자 {SpectacleEventLock.CurrentOwner?.GetType().Name})가 점유 중입니다.");
                return;
            }

            _clickCapture = _player.PlatformService as ILocalClickCaptureService;
            Rect hitboxOs = ComputeHitboxOsRect();
            if (_clickCapture != null && !_clickCapture.RequestLocalClickCapture(hitboxOs, this))
            {
                // ★ 사용자 신고 "마우스로 안 잡힘"이 정확히 여기서 발생했다(2026-08-28):
                // MacWindowService가 ILocalClickCaptureService를 구현하지 않아
                // FallbackPlatformWindowService의 위임이 항상 false를 반환했고, `_clickCapture`는
                // 데코레이터라 non-null이라 이 분기가 매번 성립했다. macOS 구현체에 부기를 추가해
                // 근본 해결했지만(Platform/MacOS/MacWindowService.cs 참고), 재발 시 즉시 보이도록
                // 로그를 남긴다.
                Debug.LogWarning("[DragThrowController] [2/6] 드래그 진입 실패 — 부분적 클릭관통 해제(15절) 요청이 " +
                    $"거부됐습니다(서비스={_clickCapture.GetType().Name}, 히트박스={hitboxOs}). 락을 되돌립니다.");
                SpectacleEventLock.Release(this);
                return;
            }

            Debug.Log($"[DragThrowController] [2/6] 가드 통과 — {current} -> Dragged 전이를 요청합니다 " +
                $"(히트박스 OS={hitboxOs}, 클릭캡처={(_clickCapture != null ? _clickCapture.GetType().Name : "없음")}).");
            _player.Blackboard.Machine.ChangeState(StickmanStateId.Dragged);
        }

        private void OnMouseUp()
        {
            if (_player == null) return;
            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Dragged)
            {
                Debug.Log($"[DragThrowController] [5/6] 마우스업을 받았지만 현재 상태가 {current}라 무시합니다(드래그 중이 아님).");
                return;
            }
            Debug.Log("[DragThrowController] [5/6] 놓기 신호 전달 — DragThrowState가 다음 Tick에 던지기를 계산합니다.");
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
