using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>UX_FLOW.md 18절 "감시 민감도(관대/보통/예민) 3단계 슬라이더" — 연속 관찰 주기 임계값에
    /// 곱해지는 배율만 다르다(관대=더 오래 지속돼야 반응, 예민=더 빨리 반응). 설정창(7절, 미구현)이
    /// 나중에 이 프로퍼티를 바꿔 끼우면 된다.</summary>
    public enum PomodoroSensitivity
    {
        Lenient,
        Normal,
        Strict,
    }

    /// <summary>
    /// docs/UX_FLOW.md 18절 포모도로 감시자 — 타이머 시작/종료 연출 + "딴짓 감지" 에스컬레이션을 전담한다.
    ///
    /// "딴짓 감지" 신호 설계(25절-16 요구사항 그대로): 신규 상시 폴링을 만들지 않는다. 1차 신호(전경 창
    /// 포커스 전환 빈도)는 이미 StickmanBlackboard.FootholdPoller가 StickConfig.footholdPollInterval
    /// 주기로 열거하는 캐시(FootholdPoller.CachedFootholds)에서, PlatformFoothold.IsTopmost가 true인
    /// 항목의 Handle을 관찰해 얻는다 — 이 IsTopmost 값 자체가 Win32WindowService.OnEnumWindow() 안에서
    /// GetForegroundWindow()로 이미 계산되고 있던 것이라 새 OS 호출이 전혀 없다. StickmanEventBus.
    /// FootholdsChanged(발판 캐시가 바뀔 때만 발행되는 기존 이벤트)를 구독해 그 순간의 최상단 핸들이
    /// 직전과 달라졌으면 전환 1회로 카운트한다(FallbackPlatformWindowService의 안전망 합성 발판은
    /// Handle&lt;0이라 WindowTheftDirector/GraffitiDirector와 동일한 관례로 제외). 2차 신호(마우스
    /// 활동 극단값)는 9절-3 기존 전역 커서 폴링 채널(StickmanAgent.TryGetCursorPosition)을 이 컴포넌트의
    /// Update()에서 세션이 활성일 때만 읽는다 — 이 역시 신규 폴링 "채널"이 아니라 기존에 이미 노출된
    /// 조회 API를 소비하는 것뿐이다.
    ///
    /// SpectacleEventLock: FocusStart/FocusComplete/FocusCancelled/FocusNudge 4개 상태 모두
    /// ChangeState()로 단일 상태 슬롯을 다투므로 SpectacleEventKind.FocusPose로 참여시킨다(Tasklist.md
    /// 교차 레이어 로그에 판단 근거 기록). Glance/WindowTap(1/3단계)은 상태 전이가 없는 순수 앰비언트
    /// 이벤트(FocusWatchTierChanged)라 이 락과 무관하다.
    /// </summary>
    public sealed class FocusWatchDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        public bool IsSessionActive { get; private set; }
        public float RemainingSeconds { get; private set; }

        /// <summary>18절 "감시 자체를 끄고 순수 타이머로만 쓰는 옵션".</summary>
        public bool DistractionDetectionEnabled { get; set; } = true;

        public PomodoroSensitivity Sensitivity { get; set; } = PomodoroSensitivity.Normal;

        private float _sessionDurationSeconds;
        private float _graceRemaining;

        private bool _hasForegroundHandle;
        private long _lastForegroundHandle;
        private int _switchesInWindow;
        private float _windowTimer;

        private bool _hasLastMousePos;
        private Vector2 _lastMouseOsPos;
        private float _mouseIdleTimer;
        private bool _mouseErraticThisWindow;

        private int _consecutiveFlaggedWindows;
        private FocusWatchTier _currentTier = FocusWatchTier.None;

        private void OnEnable()
        {
            StickmanEventBus.FootholdsChanged += OnFootholdsChanged;
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            StickmanEventBus.FootholdsChanged -= OnFootholdsChanged;
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;
            ReleaseOwnedLock(forceIdle: true);
        }

        /// <summary>트레이 메뉴 "집중 모드" 시작(18절). minutes는 15/25/50 등 유저 선택값.</summary>
        public void StartFocusSession(float minutes)
        {
            if (_player == null || _config == null) return;

            IsSessionActive = true;
            _sessionDurationSeconds = Mathf.Max(60f, minutes * 60f);
            RemainingSeconds = _sessionDurationSeconds;
            _graceRemaining = Mathf.Max(0f, _config.pomodoroGraceSeconds);

            _hasForegroundHandle = false;
            _switchesInWindow = 0;
            _windowTimer = 0f;
            _hasLastMousePos = false;
            _mouseIdleTimer = 0f;
            _mouseErraticThisWindow = false;
            _consecutiveFlaggedWindows = 0;
            SetTier(FocusWatchTier.None);

            TryTriggerPoseState(StickmanStateId.FocusStart);
        }

        /// <summary>타이머 링 클릭 또는 트레이 "집중 모드 끄기"(18절 중도 취소, 패널티 없는 톤).</summary>
        public void StopFocusSession()
        {
            if (!IsSessionActive) return;
            IsSessionActive = false;
            TryTriggerPoseState(StickmanStateId.FocusCancelled);
        }

        private void CompleteSession()
        {
            IsSessionActive = false;
            TryTriggerPoseState(StickmanStateId.FocusComplete);
        }

        private void Update()
        {
            if (_player == null || _config == null) return;
            // 18절 예외 상태: 전체화면 게임/영상 감지 중에는 감시 로직 자체를 일시정지.
            if (_player.IsSuspended) return;
            if (!IsSessionActive) return;

            float dt = Time.deltaTime;
            RemainingSeconds -= dt;
            if (RemainingSeconds <= 0f)
            {
                CompleteSession();
                return;
            }

            if (_graceRemaining > 0f)
            {
                _graceRemaining -= dt;
                ResetWindowCounters(); // 유예 시간 동안은 관찰만 하고 누적하지 않는다(18절 명시).
                return;
            }

            TickMouseSignal(dt);

            _windowTimer += dt;
            float windowLength = Mathf.Max(1f, _config.pomodoroObservationWindowSeconds);
            if (_windowTimer < windowLength) return;
            _windowTimer = 0f;

            EvaluateWindow();
        }

        private void TickMouseSignal(float dt)
        {
            if (!_player.TryGetCursorPosition(out Vector2 pos))
            {
                _hasLastMousePos = false;
                return;
            }

            if (_hasLastMousePos)
            {
                float dist = Vector2.Distance(pos, _lastMouseOsPos);
                if (dist < 1f) _mouseIdleTimer += dt;
                else _mouseIdleTimer = 0f;

                // "매우 짧은 간격의 광범위 이동" 근사 — 프레임 간(가장 짧은 관찰 단위) 순간 속도가
                // 임계값을 넘으면 이번 관찰 창 동안 erratic 플래그를 켠다(18절, 절대치 단독 판정 아님 —
                // 아래 EvaluateWindow가 focus-switch/idle과 함께 조합해야만 실제 경고로 이어진다).
                float speed = dist / Mathf.Max(0.0001f, dt);
                if (speed >= Mathf.Max(1f, _config.pomodoroMouseErraticSpeedThreshold)) _mouseErraticThisWindow = true;
            }

            _lastMouseOsPos = pos;
            _hasLastMousePos = true;
        }

        private void OnFootholdsChanged()
        {
            if (_player == null || !IsSessionActive || _graceRemaining > 0f) return;
            if (!TryGetRealForegroundHandle(out long handle)) return;

            if (_hasForegroundHandle && handle != _lastForegroundHandle) _switchesInWindow++;
            _lastForegroundHandle = handle;
            _hasForegroundHandle = true;
        }

        private bool TryGetRealForegroundHandle(out long handle)
        {
            handle = 0L;
            var footholds = _player.Blackboard != null && _player.Blackboard.FootholdPoller != null
                ? _player.Blackboard.FootholdPoller.CachedFootholds
                : null;
            if (footholds == null) return false;

            for (int i = 0; i < footholds.Count; i++)
            {
                if (footholds[i].Handle < 0) continue; // FallbackPlatformWindowService 안전망 합성 발판 제외
                if (!footholds[i].IsTopmost) continue;
                handle = footholds[i].Handle;
                return true;
            }
            return false;
        }

        private void EvaluateWindow()
        {
            bool switchFlag = _switchesInWindow >= Mathf.Max(1, _config.pomodoroFocusSwitchThreshold);
            bool idleFlag = _mouseIdleTimer >= Mathf.Max(1f, _config.pomodoroMouseIdleSeconds);
            bool erraticFlag = _mouseErraticThisWindow;
            bool windowFlagged = DistractionDetectionEnabled && (switchFlag || idleFlag || erraticFlag);

            ResetWindowCounters();

            if (windowFlagged)
            {
                _consecutiveFlaggedWindows++;
            }
            else if (_consecutiveFlaggedWindows > 0)
            {
                // "즉시 리셋 규칙"(18절) — 신호가 정상 범위로 돌아오면 다음 관찰 주기부터 바로 리셋.
                _consecutiveFlaggedWindows = 0;
            }

            UpdateTier();
        }

        private void ResetWindowCounters()
        {
            _switchesInWindow = 0;
            _mouseErraticThisWindow = false;
            // 마우스 무입력 누적(_mouseIdleTimer)은 창 경계와 무관하게 연속 누적되어야 의미가 있으므로
            // 여기서 리셋하지 않는다(실제 움직임이 감지될 때만 TickMouseSignal이 0으로 되돌린다).
        }

        private void UpdateTier()
        {
            int tier1 = EffectiveThreshold(_config.pomodoroTier1ConsecutiveWindows);
            int tier2 = tier1 + EffectiveThreshold(_config.pomodoroTier2AdditionalWindows);
            int tier3 = tier2 + EffectiveThreshold(_config.pomodoroTier3AdditionalWindows);

            FocusWatchTier target;
            if (_consecutiveFlaggedWindows >= tier3) target = FocusWatchTier.WindowTap;
            else if (_consecutiveFlaggedWindows >= tier2) target = FocusWatchTier.Nudge;
            else if (_consecutiveFlaggedWindows >= tier1) target = FocusWatchTier.Glance;
            else target = FocusWatchTier.None;

            if (target == _currentTier)
            {
                // 1단계는 "가장 자주 발동해도 거슬리지 않아야" 하는 앰비언트라 계속 재알림해도 무방(18절).
                if (target == FocusWatchTier.Glance) StickmanEventBus.RaiseFocusWatchTierChanged(FocusWatchTier.Glance);
                return;
            }

            SetTier(target);
            if (target == FocusWatchTier.Nudge) TryTriggerPoseState(StickmanStateId.FocusNudge);
            // WindowTap(3단계)은 상태 전이 없이 순수 앰비언트(창 두드림+화면 흔들림) 이벤트로만 표현—
            // 캐릭터 로컬 이펙트일 뿐 상태 슬롯을 다툴 필요가 없다는 판단(Tasklist.md 참고).
        }

        private int EffectiveThreshold(int baseValue)
        {
            float multiplier = Sensitivity == PomodoroSensitivity.Lenient ? 1.5f
                : Sensitivity == PomodoroSensitivity.Strict ? 0.7f
                : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, baseValue) * multiplier));
        }

        private void SetTier(FocusWatchTier tier)
        {
            _currentTier = tier;
            StickmanEventBus.RaiseFocusWatchTierChanged(tier);
        }

        private void TryTriggerPoseState(StickmanStateId stateId)
        {
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null) return;

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) return; // 조용히 스킵(포즈만 생략, 타이머 로직에는 영향 없음)
            if (SpectacleEventLock.IsActive) return;
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.FocusPose, this)) return;

            _player.Blackboard.Machine.ChangeState(stateId);
        }

        private void ReleaseOwnedLock(bool forceIdle)
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (forceIdle && _player != null && _player.Blackboard != null && _player.Blackboard.Machine != null &&
                IsFocusPoseState(_player.Blackboard.Machine.CurrentStateId))
            {
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
            SpectacleEventLock.Release(this);
        }

        private static bool IsFocusPoseState(StickmanStateId id)
            => id == StickmanStateId.FocusStart || id == StickmanStateId.FocusComplete ||
               id == StickmanStateId.FocusCancelled || id == StickmanStateId.FocusNudge;

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (!IsFocusPoseState(evt.From)) return;
            if (evt.To == evt.From) return; // 방어적 — 이 4개 상태는 self-transition을 쓰지 않지만 다른 Director들과 동일한 관례 유지.
            SpectacleEventLock.Release(this);
        }

        /// <summary>Minor 2 대응(docs/BUG_REPORT_PHASE5.md): 다른 8개 Director와 달리 이 메서드만
        /// SpectacleEventLock 소유권 확인 없이 항상 세션을 취소했다 — 로데오/인질극류처럼 "지금 화면을
        /// 방해 중인 스펙터클"을 끄려고 트레이 긴급정지를 눌러도, 그 순간 별개로 진행 중이던 포모도로
        /// 세션까지 함께 날아가는 부작용이 있었다.
        /// 판단 근거: 6-5절은 긴급정지 버튼을 "이러한 이벤트"(인질극/로데오/창 점령 등 악동·반항 계열
        /// 방해성 이벤트)를 끄는 안전판으로 정의한다 — 포모도로는 유저가 자발적으로 켠 생산성 기능이라
        /// 이 "이러한 이벤트" 부류에 속하지 않는다. 반면 18절은 "탈출구: ... 트레이 긴급정지도 항상
        /// 유효"라고 명시해, 포모도로 자체를 끄는 경로로도 긴급정지가 유효해야 한다고 요구한다 — 이
        /// 요구를 지우면 18절 문서 계약을 깨게 되므로 구독 자체를 제거하는 안은 채택하지 않았다.
        /// 두 요구를 동시에 만족하는 지점: 다른 방해성 이벤트가 현재 SpectacleEventLock을 쥐고 있다면
        /// (즉 이 컴포넌트가 소유자가 아니라면) 그 긴급정지는 그 이벤트를 겨냥한 것이 거의 확실하므로
        /// 무관한 포모도로에 반응하지 않는다. 락이 비어있거나(다른 이벤트가 활성 중이 아님) 포모도로
        /// 자신의 포즈 상태(FocusStart/Complete/Cancelled/Nudge)가 이미 락을 쥐고 있는 상태라면, 그
        /// 긴급정지가 겨냥할 다른 대상이 없으므로 18절의 "항상 유효한 탈출구"를 그대로 적용한다(가장
        /// 흔한 케이스 — 포모도로만 실행 중이고 다른 이벤트가 없을 때도 여전히 즉시 종료 가능).</summary>
        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.IsActive && SpectacleEventLock.CurrentOwner != (object)this) return;

            IsSessionActive = false;
            ReleaseOwnedLock(forceIdle: true);
        }
    }
}
