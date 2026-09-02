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
    /// FootholdPoller의 읽기 전용 목록을 조회/비교할 뿐이다.
    ///
    /// ★ 2026-08-29 — 후보 소스를 <b>FootholdPoller.CachedRawWindows</b>(가려짐 필터 이전 원본 창)로
    /// 바꿨다. 종전에는 CachedFootholds(= 상단 테두리가 실제로 보이는 창만)를 썼는데, 작은 창은 대개 큰 창
    /// 뒤에 가려져 있어 폭 판정에 도달하기도 전에 후보에서 빠졌다(실측: 계산기를 띄워둬도 전체화면에 가까운
    /// 에디터 창 뒤에 있으면 "완전히 가려짐"으로 탈락 -> 후보 0개 -> 강제 발동조차 조용히 실패).
    /// 창 도둑은 <b>창을 딛는 연출이 아니라 미는 연출</b>이라 가려짐 여부와 무관해도 된다. 접지/걷기 쪽은
    /// 예전 그대로 CachedFootholds만 쓴다(그쪽에서 가려진 창을 받으면 허공을 걷는 버그가 재발한다).
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
        /// 다른 스펙터클과 같은 관례로 "확률/쿨다운만 건너뛰는" 데모 경로를 둔다.
        ///
        /// <b>27-1의 규칙은 강제 경로에서도 하나도 완화하지 않는다</b> — 상호배제 락, Idle/Walk 진입
        /// 조건, 그리고 "폭이 상한(= max(캐릭터 신장 x windowTheftMaxTargetWidthMultiplier,
        /// windowTheftMinTargetWidthPoints)) 이하인 실제 창"이라는 대상 선정 조건을 그대로 통과해야 한다. 후보 창이 없으면 아무 일도 일어나지 않는다(억지로 큰 창을
        /// 대상으로 삼지 않는다 — 그러면 "밀어도 안 움직임"이 당연해 보여 개그가 죽는다).
        /// </summary>
        /// <summary>조건에 맞는 작은 창이 없을 때 사용자에게 보여줄 한 줄(36-7 표와 1:1).</summary>
        public const string NoTargetReason = "지금 붙잡을 만한 작은 창이 없어요";

        /// <summary>창 목록 채널 자체가 없을 때(권한/미지원). 36-7의 "거부(권한)" 행 — 그룹 전체가
        /// 비활성이 되는 유일한 경우다.</summary>
        public const string NoWindowListReason = "다른 창 목록을 읽을 수 없어요";

        /// <summary>
        /// ★ 지금 창 도둑을 시킬 수 있는가 — 회색 처리와 실제 실행이 함께 쓰는 단 하나의 판정
        /// (docs/UX_FLOW.md 36-7). 27-1의 대상 선정 조건(폭 상한 이하인 실제 창)까지 여기서 본다.
        /// </summary>
        public CommandAvailability GetAvailability()
        {
            if (_player == null || _config == null || _player.Blackboard == null || _player.Blackboard.Machine == null)
                return CommandAvailability.Missing;

            if (SpectacleEventLock.IsActive)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(SpectacleEventLock.ActiveKind));

            StickmanStateId current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(current));

            // 후보 소스 자체가 없다 = 창 열거 채널 부재(권한/미지원). "작은 창이 없다"와는 다른 사실이라
            // 다른 문구를 준다 — 사용자가 창을 하나 더 열어도 해결되지 않는 상황이기 때문이다.
            if (ResolveCandidateSource() == null)
                return CommandAvailability.Blocked(NoWindowListReason);

            if (!TryFindTargetWindow(out _))
                return CommandAvailability.Blocked(NoTargetReason);

            return CommandAvailability.Ready;
        }

        /// <returns>실제로 시작했는가. 기존 단축키 호출부는 반환값을 무시하면 되므로 하위 호환이다.</returns>
        public bool ForceTriggerNow(string reason)
        {
            CommandAvailability availability = GetAvailability();
            if (!availability.IsReady)
            {
                // 2026-08-29: 종전에는 "찾지 못했습니다"만 찍어서, 강제 발동이 실패해도 **폭 조건에
                // 걸린 건지 후보 목록 자체가 비어 있는 건지** 구분할 수 없었다(직전 라운드에서 창 도둑을
                // 실물로 한 번도 못 본 채로 남은 직접적인 원인이다). 실제 후보 목록과 각 창의 폭을
                // 그대로 찍어 다음 실행 한 번으로 원인이 특정되게 한다.
                Debug.Log($"[창도둑] 강제 발동 건너뜀({reason}) — {availability.Reason}. {BuildTargetSearchDiagnostic()}");
                return false;
            }

            if (!TryFindTargetWindow(out PlatformFoothold target))
            {
                Debug.Log($"[창도둑] 강제 발동 건너뜀({reason}) — {NoTargetReason}(대상 재선정 단계). " +
                    BuildTargetSearchDiagnostic());
                return false;
            }

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.WindowTheft, this)) return false;

            _targetHandle = target.Handle;
            _targetRectSnapshot = target.ScreenRect;
            _hasTarget = true;

            RaiseOverlay(SpectacleOverlayPhase.Started);
            _player.Blackboard.Machine.ChangeState(StickmanStateId.WindowTheft);

            // 폭/상한/가려짐 여부를 함께 남긴다 — 이 한 줄이 "왜 이 창이 뽑혔는지"와 "가려진 창도
            // 대상이 되는지"(2026-08-29 수정의 직접 증거)를 다음 실행에서 재확인할 수 있게 한다.
            Debug.Log($"[창도둑] 강제 발동({reason}) — 대상 창 handle={_targetHandle}, OS영역 {_targetRectSnapshot}, " +
                $"폭={_targetRectSnapshot.width:F0}pt(상한 {WindowTheftTargetRules.ComputeMaxTargetWidthOsPx(ComputeCharacterHeightOsPx(), _config):F0}pt), " +
                $"가려짐={(IsHandleInFootholdList(_targetHandle) ? "아니오(발판 목록에도 보임)" : "예(다른 창 뒤 — 발판 목록에는 없음)")}. " +
                "실제 창은 1픽셀도 움직이지 않으며(원칙 3), 화면에 보이는 것은 복사본(고스트) 사각형뿐입니다.");
            return true;
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
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

            // 감시도 반드시 **선정과 같은 목록**을 봐야 한다. 예전처럼 여기만 CachedFootholds를 보면,
            // 대상 창이 다른 창 뒤로 들어간 순간(또는 애초에 가려진 창을 대상으로 삼은 순간) "목록에서
            // 사라짐 = 창이 닫힘"으로 오판해 시도가 시작되자마자 취소된다.
            var windows = ResolveCandidateSource();
            if (windows == null) { CancelAttempt(); return; }

            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i].Handle != _targetHandle) continue;

                // 유저가 실제로 그 창을 옮기면(드래그) 캐릭터가 놀라며 즉시 취소 — 27-1 예외 상태.
                if (windows[i].ScreenRect != _targetRectSnapshot) CancelAttempt();
                return;
            }

            // 목록에서 사라짐 = 대상 창이 도중에 닫힘 — 27-1 예외 상태.
            CancelAttempt();
        }

        /// <summary>
        /// 대상 선정/감시가 공유하는 단일 후보 소스. 원본 창 목록(가려짐 필터 이전)을 우선 쓰고, 그
        /// 채널이 없는 플랫폼(Windows/모바일/에디터 폴백)에서는 예전대로 발판 목록으로 되돌아간다 —
        /// 새 채널이 없다고 기능이 통째로 죽지 않게 하는 폴백이다(NullPlatformWindowService 관례).
        /// </summary>
        private IReadOnlyList<PlatformFoothold> ResolveCandidateSource()
        {
            var poller = _player.Blackboard.FootholdPoller;
            if (poller == null) return null;
            IReadOnlyList<PlatformFoothold> raw = poller.CachedRawWindows;
            if (raw != null && raw.Count > 0) return raw;
            return poller.CachedFootholds;
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

        /// <summary>27-1 대상 선정: 폭이 상한(WindowTheftTargetRules.ComputeMaxTargetWidthOsPx —
        /// 캐릭터 신장 x 배수와 절대 하한 중 큰 값) 이하인 실제(핸들 음수 아님 — 안전망 합성 발판 제외)
        /// 창 중 무작위 하나. 후보가 없으면 false. 판정식은 EditMode 테스트가 씬 없이 잠글 수 있도록
        /// WindowTheftTargetRules에 단일 소스로 두고 여기서는 호출만 한다.</summary>
        private bool TryFindTargetWindow(out PlatformFoothold target)
        {
            target = default;
            var windows = ResolveCandidateSource();
            if (windows == null || windows.Count == 0) return false;

            float characterHeightOsPx = ComputeCharacterHeightOsPx();
            if (characterHeightOsPx <= 0f) return false;
            float maxWidth = WindowTheftTargetRules.ComputeMaxTargetWidthOsPx(characterHeightOsPx, _config);

            if (WindowTheftTargetRules.CollectCandidates(windows, maxWidth, _candidateBuffer) == 0) return false;
            target = _candidateBuffer[Random.Range(0, _candidateBuffer.Count)];
            return true;
        }

        // 매 판정마다 새 List를 만들지 않기 위한 재사용 버퍼(24시간 상주 앱 컨벤션).
        private readonly List<PlatformFoothold> _candidateBuffer = new List<PlatformFoothold>(16);

        /// <summary>
        /// 대상 창 탐색이 실패한 이유를 사람이 읽을 수 있는 한 줄로 만든다. <b>강제 발동 실패 시에만</b>
        /// 호출한다(자동 발동 경로는 60초마다 돌므로 여기서 문자열을 만들면 로그가 오염된다).
        ///
        /// 이 진단이 처음 필요했던 이유는 후보 소스가 <b>FootholdPoller의 발판 목록</b>이라는 구조적
        /// 결합이었다 — 발판 목록은 "상단 테두리가 앞에서 실제로 보이는 창"만 담아서, 작은 창이 큰 창
        /// <b>뒤에</b> 있으면 폭 조건을 따지기도 전에 사라졌다. 그 결합은 2026-08-29에 원본 창 목록
        /// (Platform/IRawWindowRectSource.cs)으로 갈아끼워 해소했고, 이 진단은 이제 "무슨 소스를 몇 개
        /// 봤고 폭 상한이 얼마였는지"를 남겨 <b>다음 회귀를 한 줄로 특정</b>하는 역할로 남는다.
        /// </summary>
        private string BuildTargetSearchDiagnostic()
        {
            var poller = _player.Blackboard.FootholdPoller;
            IReadOnlyList<PlatformFoothold> raw = poller != null ? poller.CachedRawWindows : null;
            bool usingRaw = raw != null && raw.Count > 0;
            IReadOnlyList<PlatformFoothold> windows = ResolveCandidateSource();
            string sourceLabel = usingRaw
                ? "원본 창 목록(가려짐 무관)"
                : "발판 목록(가려짐 필터 통과분 — 원본 채널 미지원 플랫폼 폴백)";

            if (windows == null || windows.Count == 0)
            {
                return $"{sourceLabel}이 비어 있습니다(아직 첫 폴링 전이거나 열거된 창이 하나도 없음).";
            }

            float characterHeightOsPx = ComputeCharacterHeightOsPx();
            if (characterHeightOsPx <= 0f)
            {
                return "캐릭터 신장을 OS 픽셀로 환산하지 못했습니다(콜라이더/카메라 배선 확인 필요).";
            }
            float maxWidth = WindowTheftTargetRules.ComputeMaxTargetWidthOsPx(characterHeightOsPx, _config);

            int realCount = 0;
            int syntheticCount = 0;
            var widths = new System.Text.StringBuilder();
            for (int i = 0; i < windows.Count; i++)
            {
                PlatformFoothold f = windows[i];
                if (f.Handle < 0) { syntheticCount++; continue; }
                realCount++;
                if (widths.Length > 0) widths.Append(", ");
                widths.Append("handle=").Append(f.Handle)
                      .Append(" 폭=").Append(f.ScreenRect.width.ToString("F0")).Append("pt")
                      .Append(WindowTheftTargetRules.IsEligibleTarget(f, maxWidth) ? "(통과)" : "(너무 넓음)");
            }

            float scaled = characterHeightOsPx * Mathf.Max(0.01f, _config.windowTheftMaxTargetWidthMultiplier);
            string limit = $"기준: max(신장 {characterHeightOsPx:F0}pt x {_config.windowTheftMaxTargetWidthMultiplier:F1}배 " +
                           $"= {scaled:F0}pt, 절대하한 {_config.windowTheftMinTargetWidthPoints:F0}pt) = 폭 {maxWidth:F0}pt 이하";
            if (realCount == 0)
            {
                return $"소스={sourceLabel}인데 실제 창이 0개입니다(합성 발판 {syntheticCount}개는 Dock/안전망이라 제외). " +
                       $"열려 있는 일반 앱 창이 정말 하나도 없는지 확인하세요. {limit}.";
            }
            return $"소스={sourceLabel}, 실제 창 후보 {realCount}개가 전부 폭 조건을 넘었습니다 [{widths}]. {limit} " +
                   "(27-1: 큰 창을 억지로 대상으로 삼지 않는다).";
        }

        /// <summary>진단 전용 — 이 핸들이 "실제로 보이는 발판" 목록에도 들어 있는지(= 가려지지 않았는지).</summary>
        private bool IsHandleInFootholdList(long handle)
        {
            var poller = _player.Blackboard.FootholdPoller;
            if (poller == null) return false;
            IReadOnlyList<PlatformFoothold> footholds = poller.CachedFootholds;
            if (footholds == null) return false;
            for (int i = 0; i < footholds.Count; i++)
            {
                if (footholds[i].Handle == handle) return true;
            }
            return false;
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
            // (이 상태 자신의 "포기" self-transition 가드와 같은 이유).
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
