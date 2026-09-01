using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 27-4절 윈도우 크래시 — 활성 창에 해머를 내리치는 캐릭터 스윙(States.TimedSpectacleState
    /// 기반 StickmanStateId.WindowCrash, windowCrashSwingDuration만큼 짧게 재생 후 자동 Idle 복귀)과,
    /// 크랙 유리 오버레이 자체의 3초 수명(이 컨트롤러가 독립적으로 관리)을 분리해서 다룬다 — 스윙이
    /// 끝나 캐릭터가 Idle로 돌아간 뒤에도 크랙은 화면에 남아 있어야 하므로, SpectacleEventLock의 해제
    /// 시점은 캐릭터 상태 전이가 아니라 이 컨트롤러의 오버레이 타이머가 결정한다(다른 4개 Director와
    /// 다른 유일한 지점 — 아래 각 메서드 주석 참고).
    ///
    /// 절대 원칙 2/27-4 재확인(가장 중요): 이 컨트롤러는 대상 창에 어떤 입력 차단도 걸지 않는다 —
    /// Platform.ILocalClickCaptureService/Interaction.StickmanClickHitbox를 이 파일 어디서도 참조하지
    /// 않는다. 크랙 오버레이는 좌표 스냅샷 이벤트만 발행하는 순수 시각 레이어이므로, Phase2+ 렌더링이
    /// 그 위에 그리는 크랙 텍스처는 클릭 히트테스트 대상이 될 방법 자체가 코드 구조상 없다(100% 클릭관통
    /// 보장 — "시각 전용 오버레이는 항상 클릭관통" 단일 규칙, 28절-26).
    /// </summary>
    public sealed class WindowCrashDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        private float _checkTimer;
        private float _cooldownRemaining;
        private bool _overlayActive;
        private float _overlayTimer;
        private long _targetHandle;
        private Rect _targetRectSnapshot;

        private void OnEnable()
        {
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;
            ReleaseOwned();
        }

        // 개선 R2(docs/CODE_REVIEW_FINAL.md): 오버레이 자체 정리(이 컨트롤러만의 고유 로직)는 그대로
        // 남기고, 공통 3단계(소유권 확인 → 필요시 강제 Idle 전이 → Release)만 SpectacleEventLock.
        // ReleaseIfOwned로 추출했다. 원래 이 메서드는 소유권을 먼저 확인하지 않고 상태만 비교했지만,
        // TickAutoTrigger()가 TryAcquire 성공 직후에만 ChangeState(WindowCrash)를 호출하는 불변식이
        // 유지되므로 소유권 선확인을 추가해도 동작은 동일하다(SpectacleEventLock.ReleaseIfOwned 문서 참고).
        private void ReleaseOwned()
        {
            if (_overlayActive)
            {
                _overlayActive = false;
                RaiseOverlay(SpectacleOverlayPhase.Cancelled);
            }
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null, StickmanStateId.WindowCrash);
        }

        /// <summary>
        /// 윈도우 크래시 강제 발동(전역 단축키 Ctrl+Opt+Cmd+X / 캐릭터 우클릭 메뉴). 기본 트리거는 60초
        /// 주기 2% 추첨 + 25분 쿨다운으로 <b>이 프로젝트의 모든 스펙터클 중 가장 희소하게</b> 설계돼
        /// 있어(27-4: 파괴 연출은 시각적 충격이 커서 다른 것보다 더 드물어야 한다) 확률만으로는 실물
        /// 검증이 사실상 불가능하다. GraffitiDirector.ForceTriggerNow와 같은 관례로 데모 경로를 둔다.
        ///
        /// 완화하지 않는 것: 상호배제 락, Idle/Walk 진입 조건, 그리고 "포그라운드(IsTopmost)인 실제 창이
        /// 있어야 한다"는 대상 선정 조건. 그리고 무엇보다 <b>대상 창에는 여전히 아무 짓도 하지 않는다</b> —
        /// 발행하는 것은 좌표 스냅샷 이벤트 하나뿐이고 크랙은 100% 클릭관통 시각 레이어다(27-4).
        /// </summary>
        /// <summary>이미 크랙이 떠 있을 때의 문구 — 락과 별개의 자체 수명이라 따로 본다.</summary>
        public const string OverlayBusyReason = "아직 금이 안 사라졌어요";

        /// <summary>포그라운드 창을 못 찾았을 때의 문구(36-7 표와 1:1).</summary>
        public const string NoForegroundWindowReason = "지금 앞에 있는 창이 없어요";

        /// <summary>
        /// ★ 지금 창 부수기를 시킬 수 있는가 — 회색 처리와 실제 실행이 함께 쓰는 단 하나의 판정
        /// (docs/UX_FLOW.md 36-7). 27-4의 대상 조건(IsTopmost인 실제 창)까지 여기서 본다.
        /// </summary>
        public CommandAvailability GetAvailability()
        {
            if (_player == null || _config == null || _player.Blackboard == null || _player.Blackboard.Machine == null)
                return CommandAvailability.Missing;

            if (_overlayActive)
                return CommandAvailability.Blocked(OverlayBusyReason);

            if (SpectacleEventLock.IsActive)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(SpectacleEventLock.ActiveKind));

            StickmanStateId current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(current));

            if (!TryFindForegroundWindow(out _))
                return CommandAvailability.Blocked(NoForegroundWindowReason);

            return CommandAvailability.Ready;
        }

        /// <returns>실제로 시작했는가. 기존 단축키 호출부는 반환값을 무시하면 되므로 하위 호환이다.</returns>
        public bool ForceTriggerNow(string reason)
        {
            CommandAvailability availability = GetAvailability();
            if (!availability.IsReady)
            {
                Debug.Log($"[창크래시] 강제 발동 건너뜀({reason}) — {availability.Reason}" +
                    "(상호배제 락/진입 상태/대상 창 조건은 강제 경로에서도 완화하지 않는다).");
                return false;
            }

            if (!TryFindForegroundWindow(out PlatformFoothold target))
            {
                Debug.Log($"[창크래시] 강제 발동 건너뜀({reason}) — {NoForegroundWindowReason}(대상 재선정 단계).");
                return false;
            }

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.WindowCrash, this)) return false;

            _targetHandle = target.Handle;
            _targetRectSnapshot = target.ScreenRect;
            _overlayTimer = 0f;
            _overlayActive = true;

            RaiseOverlay(SpectacleOverlayPhase.Started);
            _player.Blackboard.Machine.ChangeState(StickmanStateId.WindowCrash);

            Debug.Log($"[창크래시] 강제 발동({reason}) — 대상 창 handle={_targetHandle}, OS영역 {_targetRectSnapshot}, " +
                $"크랙 유지 {_config.windowCrashOverlayDurationSeconds:F1}초. " +
                "크랙은 순수 시각 레이어이므로 그동안에도 그 창은 평소처럼 클릭/타이핑됩니다(27-4).");
            return true;
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
            if (_player == null || _config == null) return;

            if (_overlayActive)
            {
                TickOverlay(Time.deltaTime);
                return;
            }

            TickAutoTrigger();
        }

        private void TickOverlay(float dt)
        {
            // 전체화면 게임 감지(6-4절) — 캐릭터 스윙은 StickmanAgent.Suspend()가 이미 강제 처리하지만,
            // 크랙 오버레이 자체는 캐릭터 상태와 독립된 별도 수명이라 여기서 직접 IsSuspended를 폴링해야
            // 한다(다른 Director들의 IsSuspended 폴링과 동일 패턴).
            if (_player.IsSuspended) { CancelOverlay(); return; }

            var footholds = _player.Blackboard.FootholdPoller != null ? _player.Blackboard.FootholdPoller.CachedFootholds : null;
            bool stillOpen = false;
            if (footholds != null)
            {
                for (int i = 0; i < footholds.Count; i++)
                {
                    if (footholds[i].Handle == _targetHandle) { stillOpen = true; break; }
                }
            }
            if (!stillOpen)
            {
                // 대상 창이 최소화되면(IsWindowVisible=false로 열거에서 제외) 또는 닫히면 즉시 오버레이 제거(27-4 예외 상태).
                CancelOverlay();
                return;
            }

            _overlayTimer += dt;
            if (_overlayTimer >= Mathf.Max(0.1f, _config.windowCrashOverlayDurationSeconds))
            {
                CompleteOverlay();
            }
        }

        private void TickAutoTrigger()
        {
            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) { _checkTimer = 0f; return; }

            _checkTimer += Time.deltaTime;
            float interval = Mathf.Max(1f, _config.windowCrashCheckInterval);
            if (_checkTimer < interval) return;
            _checkTimer = 0f;

            if (_cooldownRemaining > 0f) return;
            if (SpectacleEventLock.IsActive) return;
            if (Random.value >= _config.windowCrashChance) return;

            if (!TryFindForegroundWindow(out PlatformFoothold target)) return;

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.WindowCrash, this)) return;

            _targetHandle = target.Handle;
            _targetRectSnapshot = target.ScreenRect;
            _overlayTimer = 0f;
            _overlayActive = true;

            RaiseOverlay(SpectacleOverlayPhase.Started);
            // 캐릭터의 해머 스윙(States.TimedSpectacleState) — 이 전이 자체는 크랙 오버레이 락과
            // 무관하게 독립적으로 짧게 재생되고 스스로 Idle로 복귀한다(클래스 문서 참고).
            _player.Blackboard.Machine.ChangeState(StickmanStateId.WindowCrash);
        }

        /// <summary>27-4 대상 선정: 신규 폴링 없이 기존 발판 캐시에서 IsTopmost==true인 실제(핸들 음수
        /// 아님) 창 하나를 재사용한다(전체화면 감지용으로 이미 조회 중인 정보 재사용 취지와 동일).</summary>
        private bool TryFindForegroundWindow(out PlatformFoothold target)
        {
            target = default;
            var footholds = _player.Blackboard.FootholdPoller != null ? _player.Blackboard.FootholdPoller.CachedFootholds : null;
            if (footholds == null) return false;

            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold f = footholds[i];
                if (f.Handle < 0) continue; // 안전망 합성 발판 제외
                if (!f.IsTopmost) continue;
                target = f;
                return true;
            }
            return false;
        }

        private void CancelOverlay()
        {
            _overlayActive = false;
            RaiseOverlay(SpectacleOverlayPhase.Cancelled);
            FinishAndCooldown();
        }

        private void CompleteOverlay()
        {
            _overlayActive = false;
            RaiseOverlay(SpectacleOverlayPhase.Completed);
            FinishAndCooldown();
        }

        private void FinishAndCooldown()
        {
            _cooldownRemaining = _config != null ? _config.windowCrashCooldownSeconds : 1500f;
            SpectacleEventLock.Release(this);
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player != null && _player.Blackboard != null && _player.Blackboard.Machine != null &&
                _player.Blackboard.Machine.CurrentStateId == StickmanStateId.WindowCrash)
            {
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
            if (_overlayActive) CancelOverlay();
        }

        private void RaiseOverlay(SpectacleOverlayPhase phase)
            => StickmanEventBus.RaiseWindowCrashOverlayChanged(_targetRectSnapshot, phase);
    }
}
