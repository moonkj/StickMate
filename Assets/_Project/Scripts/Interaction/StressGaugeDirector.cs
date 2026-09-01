using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 19절 스트레스 게이지 — 트리거 판정(격파훈련 과다/장시간 방치/긴급정지 반복 사용/
    /// 시간당 자연 감소)을 전담하고 값 자체는 Core.StressGauge(정적)에 보관한다. 이 라운드 지시사항의
    /// 명시적 스코프 축소에 따라 3단 노출(표정 암시/트레이 점/설정창 상세) UI는 구현하지 않는다 —
    /// Core.StressGauge가 발행하는 StickmanEventBus.StressLevelChanged 이벤트 훅까지만 확정한다.
    ///
    /// 유일한 예외로 "SULKY 상태 전이"(19절 "반항 임계값 도달 전 예고 신호")는 여기서 함께 구현한다 —
    /// 이는 게이지 수치 표시가 아니라 캐릭터의 확정된 상태 전이(원칙 1 그대로 적용)이고, 20절 가출이
    /// 참조하는 "지금 이미 기분이 안 좋다"는 현재형 신호이기도 해 스트레스 판정 로직과 분리하면 오히려
    /// 트리거 조건이 이중 관리될 위험이 있다.
    ///
    /// SpectacleEventLock 적용 판단(Coder 판단, Tasklist.md 교차 레이어 로그에 근거 기록): SULKY는
    /// ChangeState(Sulky)를 호출해 단일 상태 슬롯을 다투므로 참여시킨다 — HardwareReactionDirector가
    /// 이 락을 쓰지 않는 이유(ChangeState를 전혀 호출하지 않는 순수 오버레이 신호)와 정확히 대칭되는
    /// 논리로, SULKY는 그 조건을 충족하지 않는다(실제 상태 전이이므로).
    /// </summary>
    public sealed class StressGaugeDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickmanClickHitbox _hitbox;
        [SerializeField] private StickConfig _config;

        private readonly List<float> _overuseTimestamps = new List<float>(16);
        private readonly List<float> _emergencyStopTimestamps = new List<float>(8);

        private float _secondsSinceInteraction;
        private float _sulkyCheckTimer;
        private float _sulkyCooldownRemaining;

        private void Awake()
        {
            if (_hitbox == null) _hitbox = GetComponent<StickmanClickHitbox>();
        }

        private void OnEnable()
        {
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
            StickmanEventBus.TodoListChanged += OnUserInteractionObserved;
            if (_hitbox != null) _hitbox.MouseDown += OnUserInteractionObserved;
        }

        private void OnDisable()
        {
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;
            StickmanEventBus.TodoListChanged -= OnUserInteractionObserved;
            if (_hitbox != null) _hitbox.MouseDown -= OnUserInteractionObserved;
            ReleaseOwnedSulkyLock();
        }

        // 개선 R2(docs/CODE_REVIEW_FINAL.md): 3단계 보일러플레이트를 SpectacleEventLock.ReleaseIfOwned로 추출.
        private void ReleaseOwnedSulkyLock()
        {
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null, StickmanStateId.Sulky);
        }

        /// <summary>
        /// 스트레스 게이지 데모 순환(Ctrl+Opt+Cmd+S / 우클릭 메뉴). 다른 Director의 ForceTriggerNow가
        /// "확률/쿨다운만 건너뛴다"는 성격인 것과 달리, 스트레스는 확률이 아니라 <b>실제로 쌓이는 데
        /// 수 시간~반나절이 걸리는 값</b>이라(19절: 5분 내 8회 격파훈련 / 반나절 방치 / 자연 감소
        /// 0.05per시간) 같은 의미의 강제 경로가 존재할 수 없다. 그래서 이것만은
        /// HardwareReactionDirector.ForceTriggerNow와 같은 성격 — <b>실제로는 아직 쌓이지 않은 값의
        /// 연출만</b> 미리 보여주는 경로다.
        ///
        /// 누를 때마다 안정 -> 주의 -> 경고 -> 안정으로 순환한다. 경고 단계를 지나 다시 안정으로
        /// 돌아오는 세 번째 단계를 반드시 두는 이유: 자연 감소가 시간당 0.05라 한 번 0.8까지 올려두면
        /// 스스로 내려오는 데 16시간이 걸린다 — 데모가 앱을 반나절 동안 부루퉁한 상태로 고정시키면
        /// 안 된다(SULKY 자동 발동이 계속 걸린다).
        ///
        /// 게이지 값 자체는 Core.StressGauge를 통해서만 바꾼다(값의 단일 소유자를 우회하지 않는다).
        /// SULKY 전이는 여기서 직접 호출하지 않고 기존 판정(TickSulkyAutoTrigger)에 그대로 맡긴다 —
        /// 상호배제 락/쿨다운/진입 상태 조건을 데모가 건너뛰면 실물 검증의 의미가 없어진다.
        /// </summary>
        public void ForceTriggerNow(string reason)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[스트레스] 데모 순환 실패({reason}) — 설정 배선이 없습니다.");
                return;
            }

            float alarm = Mathf.Clamp01(_config.stressSulkyThreshold);
            float caution = Mathf.Clamp(_config.stressTierCautionLevel, 0f, alarm);
            float current = StressGauge.CurrentLevel;

            float next;
            if (current < caution) next = caution;
            else if (current < alarm) next = alarm;
            else next = 0f;

            StressGauge.SetLevel(next);
            Debug.Log($"[스트레스] 데모 순환({reason}) — 게이지 {current:F2} -> {next:F2}. " +
                "★ 화면에는 숫자도 막대도 그리지 않는다(19절) — 어깨 처짐/한숨의 단계 변화로만 보인다. " +
                $"경고 단계({alarm:F2}) 이상이면 SULKY 자동 발동 추첨이 " +
                $"{_config.stressSulkyCheckInterval:F0}초 주기 {_config.stressSulkyChance:P0} 확률로 시작되고, " +
                $"가출 임계값({_config.stressRunawayThreshold:F2}) 도달 시에는 확률 없이 확정 발동한다(24절).");
        }

        /// <summary>"장시간 방치"(19절) 판정에 쓰는 최소 정의의 '상호작용' 신호 — 캐릭터 클릭, 투두
        /// 목록 변경(추가/체크), 긴급정지 사용. 유휴 자동 발동 스펙터클(그라피티/창도둑 등)은 유저
        /// 상호작용이 아니라 앱 스스로 트리거한 것이므로 의도적으로 제외한다(포함시키면 "방치" 신호가
        /// 사실상 영원히 리셋되어 이 트리거 자체가 무력화된다) — Coder 판단, Tasklist.md 기록.</summary>
        private void OnUserInteractionObserved()
        {
            _secondsSinceInteraction = 0f;
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
            if (_player == null || _config == null) return;
            if (_player.IsSuspended) return;

            float dt = Time.deltaTime;
            _secondsSinceInteraction += dt;
            if (_sulkyCooldownRemaining > 0f) _sulkyCooldownRemaining -= dt;

            TickNeglectAndDecay(dt);
            TickSulkyAutoTrigger(dt);
        }

        private void TickNeglectAndDecay(float dt)
        {
            float neglectThreshold = Mathf.Max(1f, _config.stressNeglectThresholdSeconds);
            if (_secondsSinceInteraction >= neglectThreshold)
            {
                StressGauge.Add(_config.stressNeglectIncrementPerHourOver / 3600f * dt);
            }
            else
            {
                // 최근 상호작용이 있어 '방치' 상태가 아닐 때만 자연 감소(Coder 판단 — 단조증가 방지, 위 클래스 주석 참고).
                StressGauge.Add(-_config.stressPassiveDecayPerHour / 3600f * dt);
            }
        }

        private void TickSulkyAutoTrigger(float dt)
        {
            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) { _sulkyCheckTimer = 0f; return; }

            _sulkyCheckTimer += dt;
            float interval = Mathf.Max(1f, _config.stressSulkyCheckInterval);
            if (_sulkyCheckTimer < interval) return;
            _sulkyCheckTimer = 0f;

            if (_sulkyCooldownRemaining > 0f) return;
            if (StressGauge.CurrentLevel < _config.stressSulkyThreshold) return;
            if (SpectacleEventLock.IsActive) return;
            if (Random.value >= _config.stressSulkyChance) return;
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.Sulky, this)) return;

            _player.Blackboard.Machine.ChangeState(StickmanStateId.Sulky);
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            // 격파훈련 과다(19절): 격파 미니게임/드래그&던지기 "진입"만 센다 — BattleMinigameState의
            // 재도전 self-transition(From==To==BattleMinigame)은 같은 시도의 연장이라 새 진입이 아니므로 제외.
            bool qualifyingEntry = (evt.To == StickmanStateId.BattleMinigame && evt.From != StickmanStateId.BattleMinigame)
                || evt.To == StickmanStateId.Dragged;
            if (qualifyingEntry) RecordOveruseEntry();

            if (evt.From == StickmanStateId.Sulky && evt.To != StickmanStateId.Sulky)
            {
                _sulkyCooldownRemaining = _config != null ? _config.stressSulkyCooldownSeconds : 90f;
                SpectacleEventLock.Release(this);
            }
        }

        private void RecordOveruseEntry()
        {
            float now = Time.time;
            _overuseTimestamps.Add(now);

            float window = _config != null ? Mathf.Max(1f, _config.stressOveruseWindowSeconds) : 300f;
            _overuseTimestamps.RemoveAll(t => now - t > window);

            int triggerCount = _config != null ? Mathf.Max(1, _config.stressOveruseTriggerCount) : 8;
            if (_overuseTimestamps.Count > triggerCount)
            {
                StressGauge.Add(_config != null ? _config.stressOveruseIncrement : 0.12f);
            }
        }

        private void OnEmergencyStop()
        {
            OnUserInteractionObserved(); // 긴급정지 사용 자체는 앱을 쓰고 있다는 신호이므로 방치 타이머 리셋.

            float now = Time.time;
            _emergencyStopTimestamps.Add(now);
            float window = _config != null ? Mathf.Max(1f, _config.stressEmergencyStopWindowSeconds) : 600f;
            _emergencyStopTimestamps.RemoveAll(t => now - t > window);

            int triggerCount = _config != null ? Mathf.Max(1, _config.stressEmergencyStopTriggerCount) : 3;
            if (_emergencyStopTimestamps.Count > triggerCount)
            {
                // 19절: 긴급정지는 유저의 정당한 권리이므로 아주 약한 가중치만(사용을 주저하게 만들면 안 됨).
                StressGauge.Add(_config != null ? _config.stressEmergencyStopIncrement : 0.03f);
            }

            ReleaseOwnedSulkyLock();
        }
    }
}
