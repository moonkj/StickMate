using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 27-2절(바탕화면 청소부)/27-5절(블랙홀 소환)이 공유하는 "복제 스프라이트" 파이프라인
    /// (28절-25 "하나의 공용 컴포넌트(가칭 DesktopIconMirrorOverlay)로 통합 구현" 권고를 그대로 따른 이름).
    /// 인스펙터의 <see cref="_kind"/>로 청소부/블랙홀 중 무엇을 담당할지만 다르고, 트리거/취소 로직은
    /// 완전히 동일하다 — 두 기능을 위한 코드를 중복 작성하지 않는다.
    ///
    /// 파이프라인(27-2/27-5 원문 그대로):
    /// 1) 이벤트 시작 시 IDesktopIconLayoutService로 아이콘 영역/좌표를 읽기 전용 1회 조회(캡처).
    /// 2) StickmanEventBus.DesktopIconMirrorOverlayChanged(Started, 좌표 목록)만 발행 — 실제 오버레이
    ///    스프라이트 스폰/애니메이션은 Phase2+ 렌더링 담당(아이콘 재배치 API는 이 파일 어디에도 없음).
    /// 3) 이벤트 진행 중에는 9절-3 전역 커서 폴링(ICursorPositionService)을 읽기 전용으로 관찰해, 커서가
    ///    캡처했던 아이콘 영역 안에 들어오면 "클릭을 가로채는 게 아니라 스스로 판단해" 즉시 취소한다
    ///    (28절-27 요구사항 — 실제 클릭/더블클릭 이벤트를 직접 감지할 전역 클릭 상태 API가 이 프로젝트에
    ///    없어, 좌표 진입 자체를 "사용자 활동" 근사 신호로 쓴다 — 실제 클릭보다 더 이르게, 더 안전한
    ///    방향으로만 취소하므로 원칙 2/3을 침해하는 방향의 오차가 아니다).
    /// 4) 정상 종료(시간 경과) 또는 취소 시 오버레이 제거 이벤트만 발행 — 실제 아이콘은 처음부터 끝까지
    ///    한 번도 이동하지 않았다.
    /// </summary>
    public sealed class DesktopIconMirrorDirector : MonoBehaviour
    {
        [SerializeField] private DesktopIconMirrorKind _kind;
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        private float _checkTimer;
        private float _cooldownRemaining;
        private bool _hasRegion;
        private Rect _regionSnapshot;
        private readonly System.Collections.Generic.List<Rect> _iconRectsSnapshot = new System.Collections.Generic.List<Rect>(16);

        private StickmanStateId TargetStateId => _kind == DesktopIconMirrorKind.DesktopTidy
            ? StickmanStateId.DesktopTidy : StickmanStateId.BlackholeSummon;

        private SpectacleEventKind LockKind => _kind == DesktopIconMirrorKind.DesktopTidy
            ? SpectacleEventKind.DesktopTidy : SpectacleEventKind.BlackholeSummon;

        private IDesktopIconLayoutService IconService => _player != null ? _player.PlatformService as IDesktopIconLayoutService : null;

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
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null, TargetStateId);
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
            if (_player == null || _config == null) return;

            if (_player.Blackboard.Machine.CurrentStateId == TargetStateId)
            {
                MonitorActive();
                return;
            }

            TickAutoTrigger();
        }

        private void MonitorActive()
        {
            if (!_hasRegion) return;

            // 28절-27: 클릭 가로채기 없이 전역 커서 좌표 폴링을 읽기 전용으로 관찰만 한다.
            if (_player.TryGetCursorPosition(out Vector2 cursorOs) && _regionSnapshot.Contains(cursorOs))
            {
                Cancel();
                return;
            }

            var svc = IconService;
            if (svc != null && svc.TryGetIconRegion(out Rect nowRegion) && nowRegion != _regionSnapshot)
            {
                // 유저가 실제로 아이콘을 드래그해 재배치 — 복제본과 실제 좌표가 어긋나므로 재계산 없이 즉시 취소.
                Cancel();
                return;
            }

            if (RegionCoveredByRealWindow(_regionSnapshot))
            {
                // 새 창이 열려 데스크톱을 덮음 — 화면이 이미 가려졌으므로 자연 종료(27-2 예외 상태).
                Cancel();
            }
        }

        private void Cancel()
        {
            _hasRegion = false;
            RaiseOverlay(SpectacleOverlayPhase.Cancelled);
            if (_player.Blackboard.Machine.CurrentStateId == TargetStateId)
            {
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
        }

        private void TickAutoTrigger()
        {
            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) { _checkTimer = 0f; return; }

            float interval = _kind == DesktopIconMirrorKind.DesktopTidy ? _config.desktopTidyCheckInterval : _config.blackholeCheckInterval;
            float chance = _kind == DesktopIconMirrorKind.DesktopTidy ? _config.desktopTidyChance : _config.blackholeChance;

            _checkTimer += Time.deltaTime;
            if (_checkTimer < Mathf.Max(1f, interval)) return;
            _checkTimer = 0f;

            if (_cooldownRemaining > 0f) return;
            if (SpectacleEventLock.IsActive) return; // 27-2/27-5 상호배제 + 16-15 전체 스펙터클 상호배제, 전역 락 하나로 둘 다 충족.

            var svc = IconService;
            if (svc == null) return; // 플랫폼이 아이콘 조회를 지원하지 않음(Win32 정직한 미구현 스텁 등) — 조용히 스킵.
            if (!svc.TryGetIconRegion(out Rect region)) return; // 아이콘 좌표 조회 실패

            // "데스크톱 아이콘이 실제로 화면에 보이는 상태일 때만" — 다른 실제 창이 그 영역을 덮고 있으면 후보 제외.
            if (RegionCoveredByRealWindow(region)) return;

            if (Random.value >= chance) return;

            if (!SpectacleEventLock.TryAcquire(LockKind, this)) return;

            _regionSnapshot = region;
            _iconRectsSnapshot.Clear();
            var rects = svc.EnumerateIconRects();
            if (rects != null)
            {
                for (int i = 0; i < rects.Count; i++) _iconRectsSnapshot.Add(rects[i]);
            }
            _hasRegion = true;

            RaiseOverlay(SpectacleOverlayPhase.Started);
            _player.Blackboard.Machine.ChangeState(TargetStateId);
        }

        private bool RegionCoveredByRealWindow(Rect region)
        {
            var footholds = _player.Blackboard.FootholdPoller != null ? _player.Blackboard.FootholdPoller.CachedFootholds : null;
            if (footholds == null) return false;
            for (int i = 0; i < footholds.Count; i++)
            {
                if (footholds[i].Handle < 0) continue; // 안전망 합성 발판 제외
                if (footholds[i].ScreenRect.Overlaps(region)) return true;
            }
            return false;
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (evt.From != TargetStateId) return;
            bool wasCancelled = !_hasRegion; // Cancel()이 이미 _hasRegion=false + Cancelled를 발행했다면 완료 처리 생략.
            _hasRegion = false;
            if (!wasCancelled) RaiseOverlay(SpectacleOverlayPhase.Completed);

            float cooldown = _kind == DesktopIconMirrorKind.DesktopTidy
                ? (_config != null ? _config.desktopTidyCooldownSeconds : 900f)
                : (_config != null ? _config.blackholeCooldownSeconds : 900f);
            _cooldownRemaining = cooldown;
            SpectacleEventLock.Release(this);
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player == null) return;
            Cancel();
        }

        private void RaiseOverlay(SpectacleOverlayPhase phase)
            => StickmanEventBus.RaiseDesktopIconMirrorOverlayChanged(_kind, _iconRectsSnapshot, phase);
    }
}
