using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 20절 가출의 트리거/발견/복귀 신호 배선을 전담한다. 실제 페이즈 진행(뛰어감 →
    /// 은신 → 발견 → 화해/자진복귀)은 States/RunawayState.cs가 담당한다 — 이 컨트롤러는 "언제
    /// 발동하는지" + "유저의 탐색/간식/수동소환/긴급정지 신호를 블랙보드 펄스로 전달하는지"만 책임진다
    /// (WindowTheftDirector와 WindowTheftState의 관계와 동일 컨벤션).
    ///
    /// "찾기" 상호작용(20절)은 신규 입력 경로를 만들지 않는다 — 은신 중에도 캐릭터의 Rigidbody2D/
    /// Collider2D는 살아있고(States/RunawayState.cs 문서 참고, Kinematic이지 simulated=false가 아님)
    /// 렌더러만 꺼지므로, 기존 Interaction/StickmanClickHitbox.cs의 OnMouseDown이 "안 보이는 캐릭터를
    /// 클릭"했을 때 그대로 발동한다 — BattleMinigameDirector/DragThrowController와 같은 히트박스를
    /// 공유하는 세 번째 구독자일 뿐이다.
    ///
    /// SpectacleEventLock: 20절/24절/25절-20이 명시적으로 요구하는 상호배제 세트 편입 — 가출 중에는
    /// 다른 어떤 방해성/스펙터클 이벤트도 동시 발동하지 않는다(수 시간 지속될 수 있어 이 락을 가장
    /// 오래 붙들 수 있는 항목).
    /// </summary>
    public sealed class RunawayDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickmanClickHitbox _hitbox;
        [SerializeField] private StickConfig _config;

        private void Awake()
        {
            if (_hitbox == null) _hitbox = GetComponent<StickmanClickHitbox>();
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
            ReleaseOwnedLock();
        }

        // 개선 R2(docs/CODE_REVIEW_FINAL.md): 3단계 보일러플레이트를 SpectacleEventLock.ReleaseIfOwned로 추출.
        // States/RunawayState.cs의 Exit()가 렌더러/Kinematic을 방어적으로 복구하므로 강제 Idle 전이가 안전하다.
        private void ReleaseOwnedLock()
        {
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null, StickmanStateId.Runaway);
        }

        private void Update()
        {
            if (_player == null || _config == null) return;
            if (_player.IsSuspended) return;

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current == StickmanStateId.Runaway) return; // 이미 진행 중.
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) return;
            if (SpectacleEventLock.IsActive) return;

            // 24절: 가출은 확률이 아니라 임계값 도달 시 확정 발동.
            if (StressGauge.CurrentLevel < _config.stressRunawayThreshold) return;

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.Runaway, this)) return;

            _player.Blackboard.PendingRunawayHideWorldPos = ComputeHideSpotWorldPos(Random.Range(0, 4));
            _player.Blackboard.Machine.ChangeState(StickmanStateId.Runaway);
        }

        /// <summary>은신처 4곳(화면 네 모서리, 20절)을 OS 화면 좌표로 계산한 뒤 캐릭터 현재 위치 기준
        /// 카메라 depth로 월드 좌표로 되돌린다(StickmanBlackboard.TryGetCursorWorldPosition과 동일한
        /// 왕복 변환 관례 — ScreenCoordinateConverter.cs 문서의 "같은 호출 세트 안에서 cameraDepth 재사용" 규칙).</summary>
        private Vector2 ComputeHideSpotWorldPos(int cornerIndex)
        {
            var blackboard = _player.Blackboard;
            if (blackboard.MainCamera == null || blackboard.Body == null) return blackboard.Body != null ? blackboard.Body.position : Vector2.zero;

            float dpi = _config.desktopDpiScale > 0f ? _config.desktopDpiScale : 1f;
            float screenW = (Screen.width > 0 ? Screen.width : 1920f) * dpi;
            float screenH = (Screen.height > 0 ? Screen.height : 1080f) * dpi;
            float margin = Mathf.Max(0f, _config.runawayHideSpotMarginPx);

            Vector2 osCorner;
            switch (cornerIndex)
            {
                case 0: osCorner = new Vector2(margin, margin); break; // 좌상단
                case 1: osCorner = new Vector2(screenW - margin, margin); break; // 우상단
                case 2: osCorner = new Vector2(margin, screenH - margin); break; // 좌하단
                default: osCorner = new Vector2(screenW - margin, screenH - margin); break; // 우하단
            }

            _ = ScreenCoordinateConverter.WorldToOsScreen(blackboard.MainCamera, blackboard.Body.position, _config, out float cameraDepth);
            return ScreenCoordinateConverter.OsScreenToWorld(blackboard.MainCamera, osCorner, cameraDepth, _config);
        }

        private void OnHitboxMouseDown()
        {
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null) return;
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.Runaway) return;
            _player.Blackboard.RunawayFoundSignaled = true;
        }

        /// <summary>찾은 자리의 "[간식 주기]" 버튼(20절, 14절 사과 먹이기와 동일 톤의 앱 소유 UI) —
        /// 실제 버튼 UI는 Phase2+ 렌더링/설정창 담당(WindowTheftOverlayChanged류 기존 이벤트-only
        /// 패턴과 동일한 이유 — 14절 인질극 자체가 아직 구현되지 않아 문자 그대로 재사용할 기존 코드가
        /// 없다, Tasklist.md 참고), 여기서는 그 버튼이 호출할 공개 진입점만 마련한다.</summary>
        public void OfferSnack()
        {
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null) return;
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.Runaway) return;
            _player.Blackboard.RunawaySnackOfferedSignaled = true;
        }

        /// <summary>트레이 메뉴 "[돌아오라고 부르기]"(20절 수동 소환) — 찾기 미니게임을 강제하지 않는다.</summary>
        public void RecallManually()
        {
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null) return;
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.Runaway) return;
            _player.Blackboard.RunawayManualRecallSignaled = true;
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (evt.From != StickmanStateId.Runaway) return;
            if (evt.To == StickmanStateId.Runaway) return; // self-transition 방어(다른 Director들과 동일 관례)
            SpectacleEventLock.Release(this);
        }

        /// <summary>24절: 가출 중 트레이 긴급정지는 "종료"가 아니라 "즉시 강제 소환"으로 동작이 달라야
        /// 한다 — 같은 전역 이벤트를 재사용하되(신규 이벤트 불필요) 현재 상태가 Runaway일 때만 다르게
        /// 해석한다. 다른 상태(인질극/로데오 등)의 OnEmergencyStop 핸들러는 각자의 "종료" 의미를 그대로
        /// 유지하므로, 라벨/동작 분기는 이벤트 발행 쪽이 아니라 구독자(이 Director) 쪽 책임이다.</summary>
        private void OnEmergencyStop()
        {
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null) return;
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.Runaway) return;
            _player.Blackboard.RunawayForceSummonSignaled = true;
        }
    }
}
