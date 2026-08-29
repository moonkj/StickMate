using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 27-1절 윈도우 창 도둑의 트리거/대상 선정/취소 감시를 전담한다. 실제 시도 진행
    /// 페이즈(1/2회차/포기)는 States/WindowTheftState.cs가 담당하고, 이 컨트롤러는 "언제 시작하고
    /// 언제 강제로 취소하는지" + "어느 창을 대상으로 삼는지"만 결정한다.
    ///
    /// 절대 원칙 3 재확인: 아래 어디에도 대상 창의 좌표/크기를 변경하는 API 호출이 없다 — 오직
    /// FootholdPoller.CachedFootholds(읽기 전용 열거)를 조회/비교할 뿐이다.
    /// </summary>
    public sealed class WindowTheftDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private Collider2D _characterCollider;
        [SerializeField] private StickConfig _config;

        private float _checkTimer;
        private float _cooldownRemaining;
        private long _targetHandle;
        private Rect _targetRectSnapshot;
        private bool _hasTarget;

        private void Awake()
        {
            // DragThrowController.Awake()와 동일한 편의 폴백 — 같은 GameObject에 Collider2D가 붙어
            // 있는 통상 배치라면 인스펙터 수동 배선 없이도 동작한다.
            if (_characterCollider == null) _characterCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;

            // BUG-P3-M1(Major, docs/BUG_REPORT_PHASE3.md)과 동일한 근거로 Phase 4 신규 Director에도
            // 그대로 적용 — OnStateTransitioned 구독을 이미 해제했으므로 여기서 직접 락을 반환한다.
            ReleaseOwnedLock();
        }

        // 개선 R2(docs/CODE_REVIEW_FINAL.md): 3단계 보일러플레이트를 SpectacleEventLock.ReleaseIfOwned로 추출.
        private void ReleaseOwnedLock()
        {
            _hasTarget = false;
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null, StickmanStateId.WindowTheft);
        }

        /// <summary>
        /// 창 도둑 강제 발동(전역 단축키 Ctrl+Opt+Cmd+T / 캐릭터 우클릭 메뉴). 기본 트리거는 60초 주기
        /// 3% 추첨 + 15분 쿨다운이라 실사용/검증 중에 한 번 보기도 어려워, GraffitiDirector.ForceTriggerNow /
        /// RivalEncounterDirector.ForceSpawnNow와 같은 관례로 "확률/쿨다운만 건너뛰는" 데모 경로를 둔다.
        ///
        /// <b>27-1의 규칙은 강제 경로에서도 하나도 완화하지 않는다</b> — 상호배제 락, Idle/Walk 진입
        /// 조건, 그리고 "캐릭터 신장의 windowTheftMaxTargetWidthMultiplier배 이하인 실제 창"이라는 대상
        /// 선정 조건을 그대로 통과해야 한다. 후보 창이 없으면 아무 일도 일어나지 않는다(억지로 큰 창을
        /// 대상으로 삼지 않는다 — 그러면 "밀어도 안 움직임"이 당연해 보여 개그가 죽는다).
        /// </summary>
        public void ForceTriggerNow(string reason)
        {
            if (_player == null || _config == null)
            {
                Debug.LogWarning($"[창도둑] 강제 발동 실패({reason}) — 플레이어/설정 배선이 없습니다.");
                return;
            }
            if (SpectacleEventLock.IsActive)
            {
                Debug.Log($"[창도둑] 강제 발동 건너뜀({reason}) — 다른 스펙터클({SpectacleEventLock.ActiveKind})이 " +
                          "진행 중입니다(상호배제 락).");
                return;
            }

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
            {
                Debug.Log($"[창도둑] 강제 발동 건너뜀({reason}) — 지금은 {current} 중입니다(Idle/Walk에서만 시작).");
                return;
            }

            if (!TryFindTargetWindow(out PlatformFoothold target))
            {
                // 2026-08-29: 종전에는 여기서 "찾지 못했습니다"만 찍어서, 강제 발동이 실패해도 **폭 조건에
                // 걸린 건지 후보 목록 자체가 비어 있는 건지** 구분할 수 없었다(직전 라운드에서 창 도둑을
                // 실물로 한 번도 못 본 채로 남은 직접적인 원인이다). 실제 후보 목록과 각 창의 폭을
                // 그대로 찍어 다음 실행 한 번으로 원인이 특정되게 한다.
                Debug.Log($"[창도둑] 강제 발동 건너뜀({reason}) — {BuildTargetSearchDiagnostic()}");
                return;
            }

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.WindowTheft, this)) return;

            _targetHandle = target.Handle;
            _targetRectSnapshot = target.ScreenRect;
            _hasTarget = true;

            RaiseOverlay(SpectacleOverlayPhase.Started);
            _player.Blackboard.Machine.ChangeState(StickmanStateId.WindowTheft);

            Debug.Log($"[창도둑] 강제 발동({reason}) — 대상 창 handle={_targetHandle}, OS영역 {_targetRectSnapshot}. " +
                "실제 창은 1픽셀도 움직이지 않으며(원칙 3), 화면에 보이는 것은 복사본(고스트) 사각형뿐입니다.");
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
            if (_player == null || _config == null) return;

            if (_player.Blackboard.Machine.CurrentStateId == StickmanStateId.WindowTheft)
            {
                MonitorTarget();
                return;
            }

            TickAutoTrigger();
        }

        private void MonitorTarget()
        {
            if (!_hasTarget) return;

            var footholds = _player.Blackboard.FootholdPoller != null ? _player.Blackboard.FootholdPoller.CachedFootholds : null;
            if (footholds == null) { CancelAttempt(); return; }

            for (int i = 0; i < footholds.Count; i++)
            {
                if (footholds[i].Handle != _targetHandle) continue;

                // 유저가 실제로 그 창을 옮기면(드래그) 캐릭터가 놀라며 즉시 취소 — 27-1 예외 상태.
                if (footholds[i].ScreenRect != _targetRectSnapshot) CancelAttempt();
                return;
            }

            // 목록에서 사라짐 = 대상 창이 도중에 닫힘 — 27-1 예외 상태.
            CancelAttempt();
        }

        private void CancelAttempt()
        {
            _hasTarget = false;
            RaiseOverlay(SpectacleOverlayPhase.Cancelled);
            if (_player.Blackboard.Machine.CurrentStateId == StickmanStateId.WindowTheft)
            {
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
        }

        private void TickAutoTrigger()
        {
            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) { _checkTimer = 0f; return; }

            _checkTimer += Time.deltaTime;
            float interval = Mathf.Max(1f, _config.windowTheftCheckInterval);
            if (_checkTimer < interval) return;
            _checkTimer = 0f;

            if (_cooldownRemaining > 0f) return;
            if (SpectacleEventLock.IsActive) return; // 16-15/28절-29: 다른 스펙터클과 상호배제

            if (Random.value >= _config.windowTheftChance) return;

            if (!TryFindTargetWindow(out PlatformFoothold target)) return; // 후보 없음 — 이번 주기는 스킵

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.WindowTheft, this)) return;

            _targetHandle = target.Handle;
            _targetRectSnapshot = target.ScreenRect;
            _hasTarget = true;

            RaiseOverlay(SpectacleOverlayPhase.Started);
            _player.Blackboard.Machine.ChangeState(StickmanStateId.WindowTheft);
        }

        /// <summary>27-1 대상 선정: 폭이 캐릭터 신장(OS px 환산)의 windowTheftMaxTargetWidthMultiplier배
        /// 이하인 실제(핸들 음수 아님 — 안전망 합성 발판 제외) 창 중 무작위 하나. 후보가 없으면 false.</summary>
        private bool TryFindTargetWindow(out PlatformFoothold target)
        {
            target = default;
            var footholds = _player.Blackboard.FootholdPoller != null ? _player.Blackboard.FootholdPoller.CachedFootholds : null;
            if (footholds == null || footholds.Count == 0) return false;

            float characterHeightOsPx = ComputeCharacterHeightOsPx();
            if (characterHeightOsPx <= 0f) return false;
            float maxWidth = characterHeightOsPx * Mathf.Max(0.01f, _config.windowTheftMaxTargetWidthMultiplier);

            _candidateBuffer.Clear();
            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold f = footholds[i];
                if (f.Handle < 0) continue; // FallbackPlatformWindowService 안전망 합성 발판 제외(실제 창 아님)
                if (f.ScreenRect.width <= 0f || f.ScreenRect.width > maxWidth) continue;
                _candidateBuffer.Add(f);
            }

            if (_candidateBuffer.Count == 0) return false;
            target = _candidateBuffer[Random.Range(0, _candidateBuffer.Count)];
            return true;
        }

        // 매 판정마다 새 List를 만들지 않기 위한 재사용 버퍼(24시간 상주 앱 컨벤션).
        private readonly List<PlatformFoothold> _candidateBuffer = new List<PlatformFoothold>(16);

        /// <summary>
        /// 대상 창 탐색이 실패한 이유를 사람이 읽을 수 있는 한 줄로 만든다. <b>강제 발동 실패 시에만</b>
        /// 호출한다(자동 발동 경로는 60초마다 돌므로 여기서 문자열을 만들면 로그가 오염된다).
        ///
        /// 이 진단이 필요한 이유 — 후보 소스가 <b>FootholdPoller의 발판 목록</b>이라는 구조적 결합 때문이다.
        /// 발판 목록은 "상단 테두리가 앞에서 실제로 보이는 창"만 담는다(Platform/MacOS/MacWindowService.cs의
        /// 가려짐 계산). 그래서 작은 창이 큰 창 <b>뒤에</b> 있으면 폭 조건을 따지기도 전에 목록에서
        /// 사라진다 — 실측 로그: 계산기/작은 Finder를 띄워도 전체화면 에디터 창 뒤에 있으면
        /// "사유=다른 창에 완전히 가려짐"으로 탈락하고, 남는 후보는 맨 앞의 큰 창 하나뿐이다.
        /// 아래 출력이 "실제 창 후보 0개"인지 "후보는 있는데 전부 너무 넓다"인지를 갈라준다.
        /// </summary>
        private string BuildTargetSearchDiagnostic()
        {
            var footholds = _player.Blackboard.FootholdPoller != null ? _player.Blackboard.FootholdPoller.CachedFootholds : null;
            if (footholds == null || footholds.Count == 0)
            {
                return "발판 폴러의 창 목록이 비어 있습니다(아직 첫 폴링 전이거나 열거된 창이 하나도 없음).";
            }

            float characterHeightOsPx = ComputeCharacterHeightOsPx();
            if (characterHeightOsPx <= 0f)
            {
                return "캐릭터 신장을 OS 픽셀로 환산하지 못했습니다(콜라이더/카메라 배선 확인 필요).";
            }
            float maxWidth = characterHeightOsPx * Mathf.Max(0.01f, _config.windowTheftMaxTargetWidthMultiplier);

            int realCount = 0;
            int syntheticCount = 0;
            var widths = new System.Text.StringBuilder();
            for (int i = 0; i < footholds.Count; i++)
            {
                PlatformFoothold f = footholds[i];
                if (f.Handle < 0) { syntheticCount++; continue; }
                realCount++;
                if (widths.Length > 0) widths.Append(", ");
                widths.Append("handle=").Append(f.Handle)
                      .Append(" 폭=").Append(f.ScreenRect.width.ToString("F0")).Append("pt")
                      .Append(f.ScreenRect.width <= maxWidth ? "(통과)" : "(너무 넓음)");
            }

            string limit = $"기준: 캐릭터 신장 {characterHeightOsPx:F0}pt x {_config.windowTheftMaxTargetWidthMultiplier:F1}배 = 폭 {maxWidth:F0}pt 이하";
            if (realCount == 0)
            {
                return $"실제 창 후보가 0개입니다(합성 발판 {syntheticCount}개는 Dock/안전망이라 제외). " +
                       "작은 창이 큰 창 뒤에 가려져 있으면 발판 목록에 아예 오르지 않습니다 — " +
                       $"대상 창을 맨 앞으로 꺼낸 뒤 다시 시도하세요. {limit}.";
            }
            return $"실제 창 후보 {realCount}개가 전부 폭 조건을 넘었습니다 [{widths}]. {limit} " +
                   "(27-1: 큰 창을 억지로 대상으로 삼지 않는다).";
        }

        private float ComputeCharacterHeightOsPx()
        {
            if (_characterCollider == null || _player.Blackboard.MainCamera == null) return 0f;
            Rect r = ClickHitboxRectUtility.ComputeOsRect(_characterCollider, _player.Blackboard.MainCamera, _player.Blackboard.Config);
            return r.height;
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (evt.From != StickmanStateId.WindowTheft) return;
            // WindowTheftState는 2회 시도 소진 시 자기 자신에게 재전이(self-transition)해 포기 대사를
            // 만든다 — From==To==WindowTheft인 이 경우는 "빠져나가는 것"이 아니므로 락을 풀면 안 된다
            // (BattleMinigameDirector의 동일한 self-transition 가드와 같은 이유).
            if (evt.To == StickmanStateId.WindowTheft) return;

            // 2026-08-29 시각 레이어 라운드에서 메운 계약 구멍: 지금까지 이 Director는 Started/Cancelled만
            // 발행하고 **정상 종료(Completed)를 한 번도 발행하지 않았다**. 구독자가 0명이던 동안에는 아무도
            // 몰랐지만, WindowTheftRenderer가 생긴 이상 정상 종료 신호가 없으면 고스트 창이 화면에 영구히
            // 남는다. GraffitiDirector.OnStateTransitioned의 wasCancelled 가드를 그대로 이식한다 —
            // CancelAttempt()가 이미 _hasTarget=false + Cancelled를 발행했으면 여기서 Completed를
            // 겹쳐 발행하지 않는다.
            bool alreadyCancelled = !_hasTarget; // CancelAttempt()가 이미 Cancelled를 발행하고 나온 경로.
            _hasTarget = false;
            if (!alreadyCancelled)
            {
                // 강제 인터럽트로 빠져나간 경우(전체화면 게임 감지 시 StickmanAgent.Suspend()가 Idle로
                // 강제 전이시키는 경로 등)는 "정상 종료"가 아니므로 Cancelled로 알린다 — 렌더러가 천천히
                // 페이드아웃하는 대신 즉시 걷어내야 하는 상황이다(27-1 "전체화면 게임 감지 시 즉시 취소").
                RaiseOverlay(evt.IsForcedInterrupt
                    ? SpectacleOverlayPhase.Cancelled
                    : SpectacleOverlayPhase.Completed);
            }
            _cooldownRemaining = _config != null ? _config.windowTheftCooldownSeconds : 900f;
            SpectacleEventLock.Release(this);
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player == null) return;
            CancelAttempt();
        }

        private void RaiseOverlay(SpectacleOverlayPhase phase)
            => StickmanEventBus.RaiseWindowTheftOverlayChanged(_targetRectSnapshot, phase);
    }
}
