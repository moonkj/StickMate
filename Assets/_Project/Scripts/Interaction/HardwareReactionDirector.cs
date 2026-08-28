using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 23절/27-6절 PC 하드웨어 반응 4종(배터리 부족/충전 중/CPU 과부하/네트워크 끊김)의
    /// 공용 저빈도 폴링 스케줄러(28절-28 "하나의 공용 스케줄러 아래 각기 다른 간격으로 등록" 요구사항 —
    /// FootholdPoller와 동일한 정신으로, 신호마다 독립된 Update() 루프를 만들지 않고 이 하나의
    /// MonoBehaviour.Update() 안에서 4개의 개별 타이머만 돌린다).
    ///
    /// 절대 원칙 재확인(27-7 체크리스트): 4개 신호 전부 UnityEngine.SystemInfo/Application의 읽기 전용
    /// 조회 API만 사용한다 — 절전모드 강제 전환, 네트워크 재연결 시도, 프로세스 종료 등 어떤 시스템
    /// 제어(쓰기) API도 이 파일에 존재하지 않는다.
    ///
    /// 27-1~27-5(창 도둑/청소부/그라피티/크래시/블랙홀)와 달리 이 기능은 SpectacleEventLock을 쓰지
    /// 않는다 — UX_FLOW.md 28절-29가 상호배제 세트에 편입시킨 목록은 명시적으로 27-1~27-5뿐이고,
    /// 하드웨어 반응은 "능동 개입 스펙터클"이 아니라 23절이 별도로 정의한 자체 규율(지속조건/쿨다운/
    /// 회복게이트/우선순위 1개만 표현)을 따르는 훨씬 가벼운 유휴 idle 자세 변형이기 때문이다(23절
    /// "캐릭터가 동시에 두 가지 다른 표정/자세를 겹쳐 보이면 안 됨"은 아래 우선순위 리졸버가 전담).
    /// </summary>
    public sealed class HardwareReactionDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        /// <summary>신호 하나(배터리/충전/CPU/네트워크)의 지속조건·회복게이트 상태를 담는 순수 데이터.</summary>
        private sealed class SignalState
        {
            public bool Sustained;
            public bool Notified; // 이번 "지속 사이클" 동안 이미 한 번 표현된 적이 있는지(회복 전까지 재알림 금지)
            public float RecoveryCooldownRemaining; // 회복 이후 재알림 가능해지기까지 남은 시간
        }

        private readonly SignalState _battery = new SignalState();
        private readonly SignalState _cpu = new SignalState();
        private readonly SignalState _network = new SignalState();
        private readonly SignalState _charging = new SignalState();

        private HardwareReactionKind? _currentlyShown;

        // ==================== 데모 미리보기(전역 단축키 Ctrl+Opt+Cmd+H / 우클릭 메뉴) ====================
        // 다른 5개 Director의 ForceTriggerNow는 "확률/쿨다운만 건너뛴다"는 성격이지만, 하드웨어 반응은
        // 확률이 아니라 **실제 하드웨어 상태**가 트리거라 같은 의미의 강제 경로가 존재할 수 없다(배터리를
        // 20%로 만들 수는 없다 — 만들려고 해도 원칙 3/27-7이 금지하는 OS 제어다). 그래서 이것만은
        // "실제 신호와 무관한 데모 미리보기"임을 이름/로그/수명에서 분명히 하고, 4종을 한 번에 하나씩
        // 순환 표시한 뒤 스스로 걷힌다. 미리보기 중에는 실제 신호 표현을 잠시 양보한다(23절 "동시에 두
        // 가지 표정을 겹쳐 보이면 안 됨").
        private const float DemoPreviewSeconds = 6f;
        private HardwareReactionKind? _demoKind;
        private float _demoRemaining;
        private int _demoCycleIndex;

        // 배터리
        private float _batteryPollTimer;
        private bool _batteryLowLastPoll;

        // 충전 — Unity에는 크로스플랫폼 "충전 상태 변경" 이벤트 콜백이 없어(각 OS 네이티브 API가 필요하고
        // 이 프로젝트엔 아직 그런 플러그인이 없음) 27-6 표가 명시한 대로 항상 폴백 폴링만 사용한다.
        private float _chargingPollTimer;

        // CPU 근사(프레임타임) — StickConfig.hardwareCpuSampleInterval 문서 참고, 알려진 한계 명시.
        private float _cpuSampleTimer;
        private float _cpuFrameTimeAccum;
        private int _cpuFrameCount;
        private float _cpuHighSustainTimer;

        // 네트워크
        private float _networkPollTimer;
        private bool _networkDownLastPoll;

        private void Update()
        {
            if (_player == null || _config == null) return;
            // 6-4절: 전체화면 게임 감지 중에는 모든 하드웨어 반응 연출도 함께 숨김.
            // 2026-08-29 시각 레이어 라운드 보강: 지금까지는 여기서 그냥 return만 했다. 구독자가 0명일
            // 때는 아무 차이가 없었지만, HardwareReactionRenderer가 생긴 뒤로는 이모트 컨테이너가
            // 캐릭터의 자식이 아니라 독립 GameObject라 StickmanAgent.Suspend()의 SetRenderersEnabled(false)에
            // 걸리지 않는다 — 즉 return만 하면 전체화면 게임 위에 이모트가 그대로 떠 있게 된다.
            // 표현 중이던 것을 명시적으로 걷어야 23절 "전체화면 감지 중에는 함께 숨김"이 실제로 성립한다.
            if (_player.IsSuspended)
            {
                ClearAllVisibleReactions("전체화면 게임 감지(6-4절)");
                return;
            }

            float dt = Time.deltaTime;
            TickBattery(dt);
            TickCharging(dt);
            TickCpu(dt);
            TickNetwork(dt);

            // 데모 미리보기가 떠 있는 동안에는 폴링/지속조건 갱신은 그대로 계속하되(그래야 미리보기가
            // 끝난 직후 실제 상태가 정확히 반영된다) 표현 결정만 잠시 양보한다.
            if (TickDemoPreview(dt)) return;

            ResolveAndNotify();
        }

        /// <summary>
        /// 하드웨어 반응 데모 미리보기 발동(Ctrl+Opt+Cmd+H / 우클릭 메뉴). 누를 때마다
        /// 배터리 -> CPU -> 네트워크 -> 충전 순으로 하나씩 <see cref="DemoPreviewSeconds"/>초간 보여준다.
        ///
        /// <b>이것은 "실제 하드웨어 신호"가 아니다</b>(로그에도 그렇게 남긴다). 다른 Director의
        /// ForceTriggerNow와 달리 확률을 건너뛰는 게 아니라 존재하지 않는 신호를 표현만 해보는 것이므로,
        /// 유저가 이 화면을 보고 "지금 배터리가 부족하구나"로 오해하면 안 되는 성격의 경로다 — 그래서
        /// 스스로 짧게 걷히고, 걷히는 즉시 실제 신호 판정이 그대로 재개된다.
        /// 하드웨어를 조회만 하고 제어(쓰기)하지 않는다는 27-7 규약은 이 경로에서도 당연히 그대로다.
        /// </summary>
        public void ForceTriggerNow(string reason)
        {
            if (_player == null || _config == null)
            {
                Debug.LogWarning($"[하드웨어] 데모 미리보기 실패({reason}) — 플레이어/설정 배선이 없습니다.");
                return;
            }

            // 실제 신호가 표현 중이면 먼저 걷어 두 표현이 겹치지 않게 한다(23절 우선순위 원칙).
            if (_currentlyShown.HasValue)
            {
                StickmanEventBus.RaiseHardwareReactionChanged(_currentlyShown.Value, active: false);
                _currentlyShown = null;
            }
            if (_demoKind.HasValue)
            {
                StickmanEventBus.RaiseHardwareReactionChanged(_demoKind.Value, active: false);
                _demoKind = null;
            }

            HardwareReactionKind kind = DemoCycleOrder[_demoCycleIndex % DemoCycleOrder.Length];
            _demoCycleIndex++;
            _demoKind = kind;
            _demoRemaining = DemoPreviewSeconds;
            StickmanEventBus.RaiseHardwareReactionChanged(kind, active: true);

            Debug.Log($"[하드웨어] 데모 미리보기 시작({reason}) — {kind} 반응을 {DemoPreviewSeconds:F0}초간 표시합니다. " +
                "★ 실제 하드웨어 신호가 아니라 연출 확인용 미리보기이며, 끝나면 실제 신호 판정이 그대로 재개됩니다. " +
                "(조회 전용 — OS 제어(쓰기) API 호출 0건, 27-7)");
        }

        /// <summary>
        /// 지금 화면에 떠 있는 반응(실제 신호 표현 + 데모 미리보기)을 전부 걷는다. 회복 게이트/쿨다운
        /// 상태(SignalState)는 건드리지 않으므로, 숨김 사유가 사라지면 ResolveAndNotify()가 원래 규칙
        /// 그대로 다시 판정한다("계속 20% 이하에 머물러 있는 동안은 재알림하지 않는다"는 27-6 보강
        /// 규칙도 Notified 플래그가 그대로라 유지된다).
        /// </summary>
        private void ClearAllVisibleReactions(string reason)
        {
            if (_currentlyShown.HasValue)
            {
                StickmanEventBus.RaiseHardwareReactionChanged(_currentlyShown.Value, active: false);
                Debug.Log($"[하드웨어] {_currentlyShown.Value} 반응 표현을 걷습니다 — {reason}.");
                _currentlyShown = null;
            }
            if (_demoKind.HasValue)
            {
                StickmanEventBus.RaiseHardwareReactionChanged(_demoKind.Value, active: false);
                _demoKind = null;
                _demoRemaining = 0f;
            }
        }

        private void OnDisable()
        {
            // 이 컨트롤러가 꺼질 때 이모트가 화면에 영구히 남지 않게 한다(다른 Director들이 OnDisable()에서
            // SpectacleEventLock을 반드시 반환하는 것과 같은 취지의 정리 관례 — 이 기능은 락에 참여하지
            // 않으므로 반환할 락은 없고, 대신 표현 중인 이벤트를 닫아준다).
            ClearAllVisibleReactions("컨트롤러 비활성화");
        }

        private static readonly HardwareReactionKind[] DemoCycleOrder =
        {
            HardwareReactionKind.LowBattery,
            HardwareReactionKind.HighCpu,
            HardwareReactionKind.NetworkDown,
            HardwareReactionKind.Charging,
        };

        /// <summary>데모 미리보기가 진행 중이면 true(그동안 실제 신호 표현은 보류된다).</summary>
        private bool TickDemoPreview(float dt)
        {
            if (!_demoKind.HasValue) return false;

            _demoRemaining -= dt;
            if (_demoRemaining > 0f) return true;

            StickmanEventBus.RaiseHardwareReactionChanged(_demoKind.Value, active: false);
            Debug.Log($"[하드웨어] 데모 미리보기 종료 — {_demoKind.Value} 이모트를 걷고 실제 신호 판정을 재개합니다.");
            _demoKind = null;
            return false;
        }

        private void TickBattery(float dt)
        {
            _batteryPollTimer += dt;
            float interval = Mathf.Max(1f, _config.hardwareBatteryPollInterval);
            if (_batteryPollTimer < interval) return;
            // BUG-P4-M1 대응(Major, docs/BUG_REPORT_PHASE4.md): UpdateSignalLifecycle의 회복 쿨다운은
            // "이 호출과 다음 호출 사이에 실제로 흐른 시간"만큼 줄어야 하는데, 여기서 매 프레임의 dt(한
            // 프레임 분량)만 넘기면 이 메서드 자체가 interval(예: 90초)마다 한 번만 호출되므로 쿨다운이
            // 사실상 거의 줄지 않는다(TickCpu의 기존 elapsedThisSample 패턴과 동일하게, 리셋 직전에
            // 누적된 폴 타이머 값을 스냅샷해 넘긴다).
            float elapsedThisPoll = _batteryPollTimer;
            _batteryPollTimer = 0f;

            float level = SystemInfo.batteryLevel; // 미지원 환경(대부분의 데스크톱)에서는 -1을 반환
            bool lowNow = level >= 0f && level <= Mathf.Clamp01(_config.hardwareLowBatteryThreshold);

            // 순간적 판독 오류 방지 — 연속 2회 폴링(hardwareBatteryConfirmPollCount) 모두 낮아야 확정.
            bool sustainedNow = lowNow && _batteryLowLastPoll;
            _batteryLowLastPoll = lowNow;

            UpdateSignalLifecycle(_battery, sustainedNow, elapsedThisPoll, _config.hardwareReactionCooldownSeconds);
        }

        private void TickCharging(float dt)
        {
            _chargingPollTimer += dt;
            float interval = Mathf.Max(1f, _config.hardwareChargingPollInterval);
            if (_chargingPollTimer < interval) return;
            // BUG-P4-M1 대응 — TickBattery와 동일 이유로 dt 대신 실제 경과 폴 간격을 넘긴다.
            float elapsedThisPoll = _chargingPollTimer;
            _chargingPollTimer = 0f;

            bool chargingNow = SystemInfo.batteryStatus == BatteryStatus.Charging;
            // 충전 상태는 순간 스파이크 개념이 아니라 실제 물리적 사건(플러그 삽입)이라 별도 연속-확인
            // 없이 다음 폴링에서 바로 반영한다(23절: "상태 진입 시 1회 전이 연출"이라는 목표와도 부합).
            UpdateSignalLifecycle(_charging, chargingNow, elapsedThisPoll, _config.hardwareReactionCooldownSeconds);
        }

        private void TickCpu(float dt)
        {
            _cpuFrameTimeAccum += dt;
            _cpuFrameCount++;

            _cpuSampleTimer += dt;
            float sampleInterval = Mathf.Max(1f, _config.hardwareCpuSampleInterval);
            if (_cpuSampleTimer < sampleInterval) return;

            float avgFrameTime = _cpuFrameCount > 0 ? _cpuFrameTimeAccum / _cpuFrameCount : 0f;
            float elapsedThisSample = _cpuSampleTimer;
            _cpuSampleTimer = 0f;
            _cpuFrameTimeAccum = 0f;
            _cpuFrameCount = 0;

            bool sampleHigh = avgFrameTime >= Mathf.Max(0.001f, _config.hardwareCpuHighFrameTimeThresholdSeconds);
            if (sampleHigh) _cpuHighSustainTimer += elapsedThisSample;
            else _cpuHighSustainTimer = 0f;

            bool sustainedNow = _cpuHighSustainTimer >= Mathf.Max(1f, _config.hardwareCpuSustainWindowSeconds);
            UpdateSignalLifecycle(_cpu, sustainedNow, elapsedThisSample, _config.hardwareReactionCooldownSeconds);
        }

        private void TickNetwork(float dt)
        {
            _networkPollTimer += dt;
            float interval = Mathf.Max(1f, _config.hardwareNetworkPollInterval);
            if (_networkPollTimer < interval) return;
            // BUG-P4-M1 대응 — TickBattery와 동일 이유로 dt 대신 실제 경과 폴 간격을 넘긴다.
            float elapsedThisPoll = _networkPollTimer;
            _networkPollTimer = 0f;

            bool downNow = Application.internetReachability == NetworkReachability.NotReachable;
            // 순간적 로밍/전환 끊김 오탐 방지 — 연속 2회 폴링 모두 끊김이어야 확정(23절 근거).
            bool sustainedNow = downNow && _networkDownLastPoll;
            _networkDownLastPoll = downNow;

            UpdateSignalLifecycle(_network, sustainedNow, elapsedThisPoll, _config.hardwareReactionCooldownSeconds);
        }

        /// <summary>
        /// 신호 하나의 지속(sustained) 여부를 갱신하고, "회복(Sustained: true -> false) 이후 쿨다운이
        /// 끝나야 다시 Notified=false(재알림 가능)로 열린다"는 23절 공통원칙 1/2 + 27-6 보강 규칙을 적용한다.
        /// "표현 중이던 신호가 끝났다"(Ended)는 이 메서드가 아니라 ResolveAndNotify()가 Sustained 값을
        /// 직접 관찰해 판단한다 — Notified는 오직 "새 알림을 시작해도 되는가"만 결정한다.
        /// </summary>
        private static void UpdateSignalLifecycle(SignalState state, bool sustainedNow, float dt, float cooldownSeconds)
        {
            if (state.Sustained && !sustainedNow)
            {
                // 방금 회복됨 — 재알림 가능해지기까지의 쿨다운을 시작한다.
                state.RecoveryCooldownRemaining = Mathf.Max(0f, cooldownSeconds);
            }
            state.Sustained = sustainedNow;

            if (!sustainedNow)
            {
                if (state.RecoveryCooldownRemaining > 0f)
                {
                    state.RecoveryCooldownRemaining -= dt;
                }
                else
                {
                    state.Notified = false;
                }
            }
        }

        /// <summary>
        /// 23절 "동시 충족 시 배터리>CPU>네트워크>충전 우선순위 1개만 표현" — 이미 표시 중인 반응은
        /// 그 자신의 조건이 회복될 때까지 유지하고(더 높은 우선순위가 나타나도 강제로 끊지 않음 — 표현이
        /// 갑자기 전환되는 산만함 방지), 아무것도 표시 중이지 않을 때만 우선순위 순서로 다음 후보를 고른다.
        /// </summary>
        private void ResolveAndNotify()
        {
            if (_currentlyShown.HasValue && !GetState(_currentlyShown.Value).Sustained)
            {
                StickmanEventBus.RaiseHardwareReactionChanged(_currentlyShown.Value, active: false);
                _currentlyShown = null;
            }

            if (!_currentlyShown.HasValue)
            {
                if (TryStart(HardwareReactionKind.LowBattery, _battery)) return;
                if (TryStart(HardwareReactionKind.HighCpu, _cpu)) return;
                if (TryStart(HardwareReactionKind.NetworkDown, _network)) return;
                TryStart(HardwareReactionKind.Charging, _charging);
            }
        }

        private bool TryStart(HardwareReactionKind kind, SignalState state)
        {
            if (!state.Sustained || state.Notified) return false;
            state.Notified = true;
            _currentlyShown = kind;
            StickmanEventBus.RaiseHardwareReactionChanged(kind, active: true);
            return true;
        }

        private SignalState GetState(HardwareReactionKind kind)
        {
            switch (kind)
            {
                case HardwareReactionKind.LowBattery: return _battery;
                case HardwareReactionKind.HighCpu: return _cpu;
                case HardwareReactionKind.NetworkDown: return _network;
                default: return _charging;
            }
        }
    }
}
