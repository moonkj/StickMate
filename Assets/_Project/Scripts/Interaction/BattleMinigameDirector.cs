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

            // BUG-P3-M1(Major, docs/BUG_REPORT_PHASE3.md) 대응: 이 컴포넌트가 SpectacleEventLock/
            // ILocalClickCaptureService를 쥔 채 비활성화/파괴되면(예: 향후 "격파 미니게임 자동발생"
            // 설정 토글) 두 락 모두 소유자 본인만 해제 가능·타임아웃 없음이라 앱 재시작 전까지 영구
            // 잠긴다. 위에서 이미 OnStateTransitioned 구독을 해제했으므로 ChangeState()가 만드는
            // StateTransitioned 이벤트로는 더 이상 락이 자동 해제되지 않는다 — 여기서 직접 해제한다.
            ReleaseOwnedLocks();
        }

        /// <summary>
        /// 개선 R2(docs/CODE_REVIEW_FINAL.md): 3단계 보일러플레이트를 SpectacleEventLock.ReleaseIfOwned로
        /// 추출했다 — 캐릭터가 얼어붙은 중간 상태(기 모으는 자세)로 남지 않도록 안전한 Idle로 강제
        /// 복귀시키는 것까지 헬퍼가 담당한다. 원래 이 메서드는 SpectacleEventLock 소유권을 먼저 확인하지
        /// 않고 상태만 비교했지만, TryBegin()이 TryAcquire 성공 직후에만 ChangeState(BattleMinigame)을
        /// 호출하는 불변식이 코드 전체에서 유지되므로(다른 어떤 경로도 이 상태로 전이하지 않는다) 헬퍼의
        /// 소유권 선확인을 추가해도 관찰 가능한 동작은 동일하다(SpectacleEventLock.ReleaseIfOwned 문서
        /// 참고). Release()/ReleaseLocalClickCapture()는 "소유자 본인일 때만" 동작하는 멱등 가드가 이미
        /// 있으므로 중복 호출돼도 예외 없이 안전하다.
        /// </summary>
        private void ReleaseOwnedLocks()
        {
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null,
                StickmanStateId.BattleMinigame, _clickCapture);
            _clickCapture = null;
        }

        /// <summary>트레이 메뉴 "격파 놀이"(10절 수동 트리거)에서 호출할 공개 진입점. 이미 다른
        /// 스펙터클 이벤트가 활성 중이면 조용히 실패(false)한다.</summary>
        public bool TriggerManually() => TryBegin();

        /// <summary>
        /// 격파 미니게임 강제 발동(전역 단축키 Ctrl+Opt+Cmd+K / 캐릭터 우클릭 메뉴). 기본 자동 발동은
        /// 60초마다 5% 추첨이라 실사용/검증 중에 한 번 보기도 어려워, 다른 스펙터클과
        /// 같은 관례로 "확률만 건너뛰는" 데모 경로를 둔다 — 상호배제 락(SpectacleEventLock)과 진입 가능
        /// 상태 조건(Idle/Walk)은 그대로 지킨다. 왜 조용히 실패하는 TriggerManually를 그냥 쓰지 않는가:
        /// 화면을 볼 수 없는 검증 환경에서 "왜 아무 일도 안 일어났는지"를 로그만으로 판별할 수 있어야 하기
        /// 때문에, 실패 사유를 구분해 남긴다.
        /// </summary>
        /// <summary>부분 클릭관통 해제(15절-4 단일 소유자 락)가 거부됐을 때의 문구.
        /// <b>미리 판정할 수 없는</b> 유일한 실패 원인이라 이유 문자열만 준비해 둔다.</summary>
        public const string ClickCaptureDeniedReason = "지금은 클릭을 받을 수 없어요";

        /// <summary>
        /// ★ 지금 격파 놀이를 시킬 수 있는가 — 회색 처리와 실제 실행이 함께 쓰는 단 하나의 판정
        /// (docs/UX_FLOW.md 36-7). <see cref="ForceTriggerNow"/>가 내부에서 이것을 호출한다.
        ///
        /// 여기서 <b>미리 알 수 없는 것</b> 하나: 부분 클릭관통 해제 요청의 거부다. 그 락은 요청해 봐야
        /// 알 수 있고 요청 자체가 부수효과라 미리 떠볼 수 없다 — 그 경우 실행이 false를 돌려주고
        /// 창이 그 자리에서 이유를 말한다(36-7의 "조용한 실패 금지").
        /// </summary>
        public CommandAvailability GetAvailability()
        {
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null)
                return CommandAvailability.Missing;

            if (SpectacleEventLock.IsActive)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(SpectacleEventLock.ActiveKind));

            StickmanStateId current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(current));

            return CommandAvailability.Ready;
        }

        /// <returns>실제로 시작했는가. 기존 단축키 호출부는 반환값을 무시하면 되므로 하위 호환이다.</returns>
        public bool ForceTriggerNow(string reason)
        {
            CommandAvailability availability = GetAvailability();
            if (!availability.IsReady)
            {
                Debug.Log($"[격파] 강제 발동 건너뜀({reason}) — {availability.Reason}(상호배제 락/진입 상태 조건은 " +
                    "강제 경로에서도 완화하지 않는다).");
                return false;
            }

            _idleCheckTimer = 0f;
            if (TryBegin())
            {
                Debug.Log($"[격파] 강제 발동({reason}) — 기 모으기 " +
                    $"{(_config != null ? _config.battleChargeDurationMin : 1.5f):F1}~" +
                    $"{(_config != null ? _config.battleChargeDurationMax : 2.0f):F1}초, 스위트스팟 " +
                    $"{(_config != null ? _config.battleSweetSpotStart : 0.70f):P0}~" +
                    $"{(_config != null ? _config.battleSweetSpotEnd : 0.85f):P0}, " +
                    $"최대 재도전 {(_config != null ? _config.battleMaxRetries : 3)}회, " +
                    $"무입력 타임아웃 {(_config != null ? _config.battleInputTimeoutSeconds : 5f):F0}초. " +
                    "이제 판자/게이지 위나 캐릭터 위를 클릭하세요.");
                return true;
            }

            Debug.LogWarning($"[격파] 강제 발동 실패({reason}) — {ClickCaptureDeniedReason}" +
                "(부분적 클릭관통 해제 요청이 거부되었습니다 — 15절-4 단일 소유자 락).");
            return false;
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
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
            // BattleMinigameState는 클릭 판정("릴리즈 순간") 때마다 자기 자신에게 재전이한다
            // (States/BattleMinigameState.cs, RagdollState의 반복 피격과 동일한 self-transition
            // 패턴 — Architect 지시, Tasklist.md 교차 레이어 로그). From==To==BattleMinigame인 이
            // 경우는 "빠져나가는 것"이 아니라 여전히 진행 중인 대결이므로 락을 풀면 안 된다.
            if (evt.To == StickmanStateId.BattleMinigame) return;
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
