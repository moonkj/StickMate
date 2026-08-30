using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 27-3절 화면 낙서 그라피티 — 캐릭터 근처(200~300px 반경)의, 어떤 발판(창) 사각형과도
    /// 겹치지 않는 빈 화면 영역을 찾아 순수 오버레이로 그렸다가 페이드아웃하는 스펙터클의 트리거/영역
    /// 선정/취소 감시를 전담한다. 실제 스프레이 애니메이션/그림 렌더링은
    /// Interaction/GraffitiRenderer.cs가 GraffitiOverlayChanged 이벤트를 구독해 담당한다.
    ///
    /// 절대 원칙 3 재확인: 배경화면 이미지 파일/설정 API는 이 파일 어디에도 호출하지 않는다 — 순수
    /// 화면 위 오버레이 레이어 좌표 계산일 뿐이다.
    /// </summary>
    public sealed class GraffitiDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        private float _checkTimer;
        private float _cooldownRemaining;
        private bool _hasRegion;
        private Rect _regionSnapshot;

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
            _hasRegion = false;
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null, StickmanStateId.Graffiti);
        }

        /// <summary>
        /// 그라피티 강제 발동(전역 단축키 Ctrl+Opt+Cmd+G / 캐릭터 우클릭 메뉴). 기본 트리거는 60초 주기
        /// 4% 추첨 + 10분 쿨다운이라 실사용/검증 중에 한 번 보기도 어려워, 다른 스펙터클과 같은 관례로
        /// ForceSpawnNow와 같은 관례로 "확률/쿨다운만 건너뛰는" 데모 경로를 둔다.
        ///
        /// <b>27-3의 침해 방지 규칙은 강제 경로에서도 하나도 완화하지 않는다</b> — 상호배제 락,
        /// Idle/Walk 진입 조건, 그리고 무엇보다 "발판(다른 창)과 겹치지 않는 빈 영역을 찾지 못하면
        /// 그리지 않고 이연한다"는 규칙을 그대로 통과해야 한다. 사용자가 단축키를 눌렀다는 사실이
        /// 남의 작업 창 위에 낙서해도 된다는 허락은 아니다.
        /// </summary>
        public void ForceTriggerNow(string reason)
        {
            if (_player == null || _config == null)
            {
                Debug.LogWarning($"[그라피티] 강제 발동 실패({reason}) — 플레이어/설정 배선이 없습니다.");
                return;
            }
            if (SpectacleEventLock.IsActive)
            {
                Debug.Log($"[그라피티] 강제 발동 건너뜀({reason}) — 다른 스펙터클({SpectacleEventLock.ActiveKind})이 " +
                          "진행 중입니다(상호배제 락).");
                return;
            }

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
            {
                Debug.Log($"[그라피티] 강제 발동 건너뜀({reason}) — 지금은 {current} 중입니다(Idle/Walk에서만 시작).");
                return;
            }

            if (!TryFindEmptyRegion(out Rect region))
            {
                Debug.Log($"[그라피티] 강제 발동 건너뜀({reason}) — 캐릭터 주변 " +
                    $"{_config.graffitiMinRadiusPx:F0}~{_config.graffitiMaxRadiusPx:F0}px 안에서 " +
                    "다른 창과 겹치지 않는 빈 영역을 찾지 못했습니다(27-3: 억지로 창 위에 그리지 않는다).");
                return;
            }

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, this)) return;

            _checkTimer = 0f;
            _cooldownRemaining = 0f;
            _regionSnapshot = region;
            _hasRegion = true;
            RaiseOverlay(SpectacleOverlayPhase.Started);
            _player.Blackboard.Machine.ChangeState(StickmanStateId.Graffiti);

            Debug.Log($"[그라피티] 강제 발동({reason}) — 빈 영역 OS좌표 {region}, " +
                $"유지 {_config.graffitiHoldDurationMin:F0}~{_config.graffitiHoldDurationMax:F0}초. " +
                "배경화면 파일/설정 API는 호출하지 않는 순수 오버레이입니다.");
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
            if (_player == null || _config == null) return;

            if (_player.Blackboard.Machine.CurrentStateId == StickmanStateId.Graffiti)
            {
                MonitorRegion();
                return;
            }

            TickAutoTrigger();
        }

        private void MonitorRegion()
        {
            if (!_hasRegion) return;
            // 그려지는 도중 그 빈 영역에 새 창이 열려 겹치게 되면 즉시 취소(27-3 예외 상태).
            if (RegionOverlapsRealFoothold(_regionSnapshot)) CancelDrawing();
        }

        private void CancelDrawing()
        {
            _hasRegion = false;
            RaiseOverlay(SpectacleOverlayPhase.Cancelled);
            if (_player.Blackboard.Machine.CurrentStateId == StickmanStateId.Graffiti)
            {
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
        }

        private void TickAutoTrigger()
        {
            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) { _checkTimer = 0f; return; }

            _checkTimer += Time.deltaTime;
            float interval = Mathf.Max(1f, _config.graffitiCheckInterval);
            if (_checkTimer < interval) return;
            _checkTimer = 0f;

            if (_cooldownRemaining > 0f) return;
            if (SpectacleEventLock.IsActive) return;
            if (Random.value >= _config.graffitiChance) return;

            if (!TryFindEmptyRegion(out Rect region)) return; // 빈 영역 못 찾음 — 억지로 창 위에 그리지 않고 이연

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, this)) return;

            _regionSnapshot = region;
            _hasRegion = true;
            RaiseOverlay(SpectacleOverlayPhase.Started);
            _player.Blackboard.Machine.ChangeState(StickmanStateId.Graffiti);
        }

        /// <summary>
        /// 캐릭터 위치 기준 200~300px 반경 안에서 무작위 각도/거리 후보를 여러 번 시도해, 화면 안쪽이고
        /// 실제 발판(창) 사각형과 겹치지 않는 정사각형 영역을 찾는다. 멀티모니터 개별 경계 API가 아직
        /// 없어(9절-5, 기존에 이미 기록된 한계) 가상 데스크톱 전체(Screen.width/height * DPI 배율)
        /// 사각형을 화면 경계 근사치로 사용한다 — 캐릭터가 서 있는 모니터만으로 한정하는 정교화는 모니터
        /// 경계 API가 생긴 뒤의 후속 과제.
        /// </summary>
        private bool TryFindEmptyRegion(out Rect region)
        {
            region = default;
            if (_player.Blackboard.MainCamera == null || _player.Blackboard.Body == null) return false;

            Vector2 characterOs = ScreenCoordinateConverter.WorldToOsScreen(
                _player.Blackboard.MainCamera, _player.Blackboard.Body.position, _player.Blackboard.Config, out _);

            float dpi = Mathf.Max(0.0001f, ScreenCoordinateConverter.ResolveDpiScale(_config));
            float screenW = (Screen.width > 0 ? Screen.width : 1920f) * dpi;
            float screenH = (Screen.height > 0 ? Screen.height : 1080f) * dpi;
            float size = Mathf.Max(1f, _config.graffitiRegionSizePx);
            float half = size * 0.5f;

            int attempts = Mathf.Max(1, _config.graffitiCandidateSearchAttempts);
            for (int i = 0; i < attempts; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Mathf.Lerp(_config.graffitiMinRadiusPx, _config.graffitiMaxRadiusPx, Random.value);
                Vector2 center = characterOs + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var candidate = new Rect(center.x - half, center.y - half, size, size);

                if (candidate.xMin < 0f || candidate.yMin < 0f || candidate.xMax > screenW || candidate.yMax > screenH) continue;
                if (RegionOverlapsRealFoothold(candidate)) continue;

                region = candidate;
                return true;
            }
            return false;
        }

        private bool RegionOverlapsRealFoothold(Rect region)
        {
            var footholds = _player.Blackboard.FootholdPoller != null ? _player.Blackboard.FootholdPoller.CachedFootholds : null;
            if (footholds == null) return false;
            for (int i = 0; i < footholds.Count; i++)
            {
                if (footholds[i].Handle < 0) continue; // 안전망 합성 발판은 "다른 창"이 아니므로 제외
                if (footholds[i].ScreenRect.Overlaps(region)) return true;
            }
            return false;
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (evt.From != StickmanStateId.Graffiti) return;
            bool wasCancelled = !_hasRegion; // CancelDrawing()이 이미 _hasRegion=false + Cancelled 이벤트를 발행했으면 여기서는 완료 처리하지 않는다.
            _hasRegion = false;
            if (!wasCancelled) RaiseOverlay(SpectacleOverlayPhase.Completed); // 정상 완료(시간 경과)
            _cooldownRemaining = _config != null ? _config.graffitiCooldownSeconds : 600f;
            SpectacleEventLock.Release(this);
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player == null) return;
            CancelDrawing();
        }

        private void RaiseOverlay(SpectacleOverlayPhase phase)
            => StickmanEventBus.RaiseGraffitiOverlayChanged(_regionSnapshot, phase);
    }
}
