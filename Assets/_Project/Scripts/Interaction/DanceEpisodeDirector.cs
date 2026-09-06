using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★★★ 음악 반응 춤의 <b>2층 — 에피소드 감독</b>(2026-09-06 신설).
    ///
    /// <para>사용자 확정 2026-09-03: <i>"소리시스템을 전체빼줘, 다만 시스템에서 노래가 나오면 상호
    /// 반응해서 춤추는 동작을 넣어줘"</i> / <i>"춤모션이 3개가 있다면 중복 장착이 가능하고 … 랜덤으로
    /// 돌아가면서 나오는거지"</i>.</para>
    ///
    /// ============================================================================
    /// ★★★ 이 파일이 존재하는 이유 = <b>창(window)과 에피소드는 다른 층이다</b>
    /// ============================================================================
    /// <code>
    ///   1층 창       Platform/AudioReactiveDanceDirector.IsDanceGateOpen   "춤춰도 된다"(몇 시간 지속)
    ///   2층 에피소드  ★ 이 파일                                            ChangeState의 유일한 소유자
    ///   3층 자세      States/DanceState + StickmanPoseAnimator.ApplyDancePose
    /// </code>
    /// 1층 신호를 <c>ChangeState</c>에 직결하면 <b>음악이 나오는 몇 시간 내내</b> Dance가 단일 상태
    /// 슬롯과 <see cref="SpectacleEventKind.Dance"/> 락을 붙들어 활쏘기·그라피티·창 도둑·청소부·
    /// 블랙홀·창 크래시·투두·SULKY·가출·집중 포즈가 <b>전부 발동 불가</b>가 된다 —
    /// 2026-09-06에 고친 집중모드 결함과 <b>같은 형태</b>다. 그래서 여기서 [에피소드 → 휴지]를 돌리고,
    /// <b>휴지 중에는 상태도 락도 잡지 않는다</b>.
    ///
    /// <para>★ <see cref="AudioReactiveDanceDirector"/>는 <b>고치지 않는다</b>. 그 파일은 «춤춰도
    /// 되는가»까지가 전부이고 스스로 <i>"여기에 ChangeState를 쓰지 마라"</i>고 못박아 두었다.
    /// 이 감독은 <b>구독만</b> 한다.</para>
    ///
    /// ============================================================================
    /// 억제는 <b>매 프레임</b> 다시 묻는다 — 1층은 2Hz라 최대 0.5초 늦다
    /// ============================================================================
    /// 집중 세션은 <b>춤 도중에</b> 시작될 수 있다(음악 → 춤 → 사용자가 [집중 모드] 클릭).
    /// 1층의 폴링만 믿으면 최대 0.5초 동안 계속 춘다. 그래서 에피소드가 도는 동안에는
    /// <see cref="AudioReactiveDanceGate.BlocksNow"/>를 <b>여기서 매 프레임</b> 다시 부른다 —
    /// 판정을 두 벌 만드는 것이 아니라 <b>같은 술어를 다시 부르는 것</b>이다.
    ///
    /// <para>★ 사유 <b>문자열</b>로 갈래를 나누지 않는다. 사유는 진단용이고, 그것으로 분기하면
    /// 문구가 바뀌는 날 조용히 갈래가 사라진다(CLAUDE.md의 «니들 하드코딩» 사고와 같은 형태).</para>
    ///
    /// ============================================================================
    /// 피로 램프 — 원칙 2(비침해)의 수치 (리더 승인 2026-09-06)
    /// ============================================================================
    /// <c>F(n) = min(1 + danceRestFatigueStep·(n−1), danceRestFatigueCap)</c>,
    /// <c>휴지 = U(danceRestMinSeconds, danceRestMaxSeconds) × F(n)</c>.
    /// <b>n은 이번 «창» 안에서의 에피소드 번호</b>이고 창이 닫히면 1로 되돌아간다.
    /// 평탄한 듀티 27%를 그대로 두면 <b>3시간 플레이리스트에서 3시간 내내 27%로 춘다</b> —
    /// 사람도 안 그러고, 상주 앱에서는 그게 곧 시선 강탈이다. 1~2곡 감상은 사실상 그대로이고
    /// (누적 듀티 27.0% → 24.7%) 긴 감상만 <b>11.0%</b>로 수렴한다. 부수 효과로 스펙터클 락 점유가
    /// 함께 줄어 다른 연출의 발동 기회가 늘어난다.
    ///
    /// ============================================================================
    /// 절대 원칙 3(유저 자산 불변) — 이 클래스가 하지 않는 일
    /// ============================================================================
    /// 창/파일/아이콘을 조작하는 API가 하나도 없다. 읽는 것은 <b>캐릭터 자신의 좌표와 딛고 있는
    /// 발판의 실측 경계</b>뿐이고(<see cref="GroundSensor"/>), 오버레이도 콜라이더도 만들지 않는다 —
    /// 춤추는 동안에도 그 자리의 다른 앱은 평소처럼 클릭된다.
    /// </summary>
    public sealed class DanceEpisodeDirector : MonoBehaviour
    {
        /// <summary>사용자가 Player.log에서 찾을 태그. 1층(감지)은 <c>[음악춤]</c>이다 —
        /// 신고가 오면 <b>둘 다</b> grep해야 «감지가 안 된 것»과 «에피소드가 안 뜬 것»이 갈린다.</summary>
        public const string LogTag = "[춤에피소드]";

        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        // ============================================================================
        // 발판 여유 요구 (docs/UX_MOTION_DANCE.md 7-4 / MOTION_SPEC 26-7-2)
        // ============================================================================
        // ★ 임의값이 아니다. 제자리 동작의 최대 좌우 반폭 = max(스타점프 손·발 0.28554,
        //   프리샤트카 찬 발 0.40088, 팔 수평 0.32972) = 0.40088 H에 여유 0.05를 더해 0.45 H.
        //   문워크 = 4루프 활강 1.20 + 여유 0.40 = 1.60 H(4루프마다 뒤집으므로 왕복 폭이 곧 요구다).
        //   스타점프 = 도움닫기 1.60 + 공중 잔여 0.3045 + 착지 감속 0.0285 = 1.933 H에 여유를 얹은 2.70 H
        //   (재검산에서 여유 0.767 H로 유효함을 확인했고, 최악 배율 0.35에서도 2.223 H다).

        /// <summary>제자리 5종이 좌우로 요구하는 최소 여유(신장 H 배수).</summary>
        internal const float InPlaceClearanceHeights = 0.45f;

        /// <summary>문워크가 <b>한쪽</b>으로 요구하는 여유(신장 H 배수).</summary>
        internal const float MoonwalkClearanceHeights = 1.60f;

        /// <summary>스타점프가 <b>한쪽</b>으로 요구하는 여유(신장 H 배수).</summary>
        internal const float StarJumpClearanceHeights = 2.70f;

        /// <summary>막혔을 때 다시 시도하는 간격(초). 상한은 <c>danceEpisodeStartRetrySeconds</c>다.</summary>
        private const float RetryIntervalSeconds = 1f;

        /// <summary>참조 재탐색 간격(초) — <c>FindAnyObjectByType</c>은 씬 전체 스캔이라 매 틱 돌리면 안 된다
        /// (<see cref="AudioReactiveDanceDirector"/>와 같은 관례·같은 값).</summary>
        private const float ReferenceLookupRetrySeconds = 1f;

        private FocusWatchDirector _focus;
        private float _nextLookupTime;

        private bool _windowOpen;
        private bool _active;              // 에피소드가 도는 중(= 이 감독이 락을 쥐고 있다).
        private int _episodeIndex = 1;     // 이번 «창» 안에서 몇 번째인가(1부터). 피로 램프와 대사의 입력.
        private float _restRemaining;
        private float _retryDeadline;      // 벽시계 기준 재시도 창의 끝(Time.unscaledTime).
        private bool _retryLogged;

        private readonly List<string> _bag = new List<string>(8);
        private string _lastDrawn;

        // ==================== 진단/테스트용 관찰 창구 ====================

        /// <summary>지금 «창»이 열려 있는가(= 1층이 음악을 감지하고 있는가). 이 값이 참이어도
        /// 대부분의 시간은 <b>휴지</b>다 — 듀티는 11~27%다.</summary>
        public bool IsDanceWindowOpen => _windowOpen;

        /// <summary>지금 에피소드가 도는 중인가(= 상태 슬롯과 스펙터클 락을 쥐고 있는가).</summary>
        public bool IsEpisodeActive => _active;

        /// <summary>이번 «창» 안에서 몇 번째 에피소드인가(1부터). 창이 닫히면 1로 돌아간다.</summary>
        public int EpisodeIndex => _episodeIndex;

        /// <summary>다음 에피소드까지 남은 휴지(초).</summary>
        public float RestRemainingSeconds => _restRemaining;

        // ============================================================================
        // 생애주기
        // ============================================================================

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

        private void ReleaseOwnedLock()
        {
            _active = false;
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null,
                StickmanStateId.Dance);
        }

        private void Update()
        {
            using var __stall = StallAttribution.Section(StallSection.Directors);   // [스톨구간] 계측
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null) return;

            float dt = Time.deltaTime;

            // ★ 이벤트가 아니라 **프로퍼티 폴링**을 쓴다. 1층 문서가 권하는 쪽이고("구독 시점을 놓쳐도
            //   값은 언제나 맞다"), 도메인 리로드를 끈 에디터에서 정적 이벤트 구독이 세션을 넘어 남는
            //   함정도 피한다.
            bool gateOpen = AudioReactiveDanceDirector.IsDanceGateOpen;
            if (gateOpen && !_windowOpen) OpenDanceWindow();
            else if (!gateOpen && _windowOpen) CloseDanceWindow();

            if (_active)
            {
                TickActiveEpisode();
                return;
            }

            if (!_windowOpen) return;

            if (_restRemaining > 0f)
            {
                _restRemaining -= dt;
                return;
            }

            TickStartAttempt(dt);
        }

        // ============================================================================
        // ★ 이름 규약 — «DanceWindow»에서 «Dance»를 빼지 마라 (2026-09-06)
        // ============================================================================
        // 여기의 «창»은 OS 창이 아니라 <b>시간 창</b>이다(_windowOpen = 1층이 음악을 감지하고 있는
        // 구간). 그런데 이 두 메서드가 처음에 Open/Close + Window 라는 이름으로 태어났고, 그 형태는
        // Tests/EditMode/UserAssetImmutabilityAuditTests의 원칙 3 금지 니들과 <b>문자 그대로 같다</b>
        // (그 니들이 겨누는 것은 남의 창을 닫는 Win32 API다). 첫 실전 러너에서 실제로 빨갛게 났다.
        //
        // 처방으로 «화이트리스트에 등재»가 아니라 «개명»을 택한 이유: 화이트리스트는 그 자리에
        // 나중에 들어올 <b>진짜</b> 위반까지 함께 숨긴다(그 감사 파일 스스로가 그 위험을 설계 의도로
        // 적어 두었다). 이름만 바꾸면 감사의 이빨은 하나도 무뎌지지 않는다.
        //
        // ⇒ 이 두 메서드를 «Dance»를 뺀 이름으로 되돌리면 감사가 다시 빨개진다. 되돌리지 마라.

        /// <summary>
        /// 창이 열렸다 — <b>곧바로 첫 에피소드를 시도한다</b>(휴지를 먼저 두지 않는다. 정책의
        /// T₁ 3.0초와 폴링 0.5초가 이미 지연이라, 여기서 또 20~45초를 기다리면 «음악을 틀었는데
        /// 아무 일도 안 일어난다»가 된다).
        /// </summary>
        /// <remarks><b>internal인 이유</b>: 이 전이를 «실제 오디오 폴링»으로만 만들 수 있으면
        /// 창/휴지/피로 카운터의 계약을 검증하는 테스트가 <b>T₁ 3.0초 + 폴링 0.5초 + 휴지 20~45초</b>를
        /// 벽시계로 기다려야 한다. 그러면 아무도 안 돌리는 테스트가 되고, 그건 없는 것과 같다
        /// (<see cref="AudioReactiveDanceDirector.RunStartup"/>이 가짜 프로브를 받으려고 public인 것과
        /// 같은 판단이다). 프로덕션 호출부는 여전히 <see cref="Update"/> 한 곳뿐이다.</remarks>
        internal void OpenDanceWindow()
        {
            _windowOpen = true;
            _episodeIndex = 1;
            _restRemaining = 0f;
            _retryDeadline = 0f;
            _retryLogged = false;
            _bag.Clear();
            _lastDrawn = null;
            Debug.Log($"{LogTag} 창이 열렸습니다({AudioReactiveDanceDirector.PlatformTag}) — " +
                "곧바로 첫 에피소드를 시도합니다. 피로 카운터를 1로 되돌립니다.");
        }

        /// <summary>
        /// 창이 닫혔다(음악이 T₂만큼 끊겼거나 T₄ 고착 상한에 걸렸다) — <b>정상 퇴장</b>을 요청한다.
        /// 급한 일이 아니므로 박자를 끝내고 나간다. 휴지 중이었으면 아무 일도 일어나지 않는다.
        /// </summary>
        /// <inheritdoc cref="OpenDanceWindow"/>
        internal void CloseDanceWindow()
        {
            _windowOpen = false;
            _restRemaining = 0f;
            _retryDeadline = 0f;
            _retryLogged = false;

            DanceState dance = ResolveDanceState();
            if (_active && dance != null) dance.RequestGracefulExit("창이 닫혔다(음악 종료 또는 T₄ 고착 상한)");

            // 피로 카운터는 여기서 되돌린다 — 새 감상 세션은 늘 활기차게 시작한다.
            _episodeIndex = 1;
            Debug.Log($"{LogTag} 창이 닫혔습니다 — {(_active ? "박자를 끝내고 나갑니다" : "휴지 중이라 화면에는 아무 변화가 없습니다")}. " +
                "피로 카운터를 1로 되돌립니다.");
        }

        /// <summary>에피소드가 도는 동안 <b>매 프레임</b> 억제를 다시 묻는다(1층은 2Hz라 최대 0.5초 늦다).</summary>
        private void TickActiveEpisode()
        {
            if (!AudioReactiveDanceGate.BlocksNow(_player, ResolveFocus())) return;

            DanceState dance = ResolveDanceState();
            if (dance != null)
            {
                // 사유 «문자열»로 분기하지 않는다 — 같은 술어를 다시 부른 결과만 쓴다.
                dance.RequestForcedExit("억제(집중 세션 / 사용자 숨김 / 전체화면 감지)");
                return;
            }
            // 상태를 못 찾는 경우(배선 사고) — 락을 든 채 남지 않도록 여기서 끊는다.
            _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
        }

        /// <summary>
        /// 시작 시도. 막혀 있으면 <b>1.0초 간격으로 <c>danceEpisodeStartRetrySeconds</c>까지</b>
        /// 재시도하고, 그래도 안 되면 이번 에피소드를 건너뛰고 휴지로 간다.
        ///
        /// <para>★ 이 재시도는 원칙 1을 위반하지 않는다 — 파생 근거(«음악이 나오고 있다»)가
        /// 재시도 내내 <b>매 폴링 다시 참으로 확인</b>되기 때문이다. <see cref="FocusWatchDirector"/>의
        /// 시작 포즈 재시도와 다른 점이 여기다: 그쪽은 «시작»이라는 <b>일회성 사실</b>이라 늦으면
        /// 거짓이 되고, 이쪽은 <b>지속되는 사실</b>이다.</para>
        /// </summary>
        private void TickStartAttempt(float deltaTime)
        {
            if (TryStartEpisode(out string blockedReason)) return;

            float limit = Mathf.Max(0f, _config != null ? _config.danceEpisodeStartRetrySeconds : 8f);
            if (!_retryLogged)
            {
                // ★ 재시도 창은 **벽시계**로 잰다. 예전 판은 «시도할 때마다 deltaTime을 더하는» 식이라
                //   1초 간격 × 프레임 1회분(약 16ms)만 쌓여, 8초짜리 창이 실제로는 **약 480초**가 됐다
                //   (그 사이 억제가 풀리면 «음악이 나온 지 몇 분 뒤에 갑자기 춘다»가 된다).
                //   시도 사이의 «쉬는 시간»은 정의상 deltaTime 누산으로 셀 수 없다.
                _retryLogged = true;
                _retryDeadline = Time.unscaledTime + limit;
                Debug.Log($"{LogTag} 시작을 미룹니다 — {blockedReason}. " +
                    $"{RetryIntervalSeconds:F1}초 간격으로 최대 {limit:F0}초까지 다시 시도합니다.");
            }

            if (Time.unscaledTime < _retryDeadline)
            {
                // 다음 시도까지 1초 쉰다(매 프레임 발판을 재계산하지 않는다 — 상주 앱이다).
                _restRemaining = RetryIntervalSeconds;
                return;
            }

            Debug.Log($"{LogTag} 이번 에피소드를 건너뜁니다 — {limit:F0}초 동안 계속 막혔습니다" +
                $"(마지막 사유: {blockedReason}). 휴지로 갑니다.");
            _retryLogged = false;
            ScheduleRest();
        }

        private bool TryStartEpisode(out string blockedReason)
        {
            blockedReason = null;
            StickmanBlackboard blackboard = _player.Blackboard;

            if (SpectacleEventLock.IsActive)
            {
                blockedReason = StickMateDisplayNames.BusyText(SpectacleEventLock.ActiveKind);
                return false;
            }

            // ★ 진입 허용 상태는 Idle/Walk 둘뿐이고, 그 밖은 **버린다. 큐잉하지 않는다** —
            //   큐잉하면 «그 시점의 사실»이 아니라 «과거 신호»에서 행동이 파생된다(원칙 1 위반).
            //   음악이 여전히 나오고 있다면 다음 시도에서 자연스럽게 다시 잡힌다.
            StickmanStateId current = blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
            {
                blockedReason = StickMateDisplayNames.BusyText(current);
                return false;
            }

            if (AudioReactiveDanceGate.BlocksNow(_player, ResolveFocus()))
            {
                blockedReason = "억제 중입니다(집중 세션 / 사용자 숨김 / 전체화면 감지 / 배선 미확보)";
                return false;
            }

            if (!TryDrawDance(out string danceId, out string drawReason))
            {
                blockedReason = drawReason;
                return false;
            }

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.Dance, this))
            {
                blockedReason = "스펙터클 락을 잡지 못했습니다";
                return false;
            }

            _active = true;
            _retryDeadline = 0f;
            _retryLogged = false;

            // ★ ChangeState **직전에** 써 넣는다 — DanceState.Enter가 이 둘을 읽어 동작을 해석하고
            //   대사 발화 여부를 정한다(ArcheryDirector가 과녁 좌표를 미리 써 넣는 것과 같은 관례).
            blackboard.DanceId = danceId;
            blackboard.DanceEpisodeIndex = _episodeIndex;
            blackboard.Machine.ChangeState(StickmanStateId.Dance);
            return true;
        }

        // ============================================================================
        // 추첨 — 셔플 백(비복원 추출). 순수 랜덤이 아니다
        // ============================================================================

        /// <summary>
        /// 이번 에피소드에 출 동작을 뽑는다.
        ///
        /// <para><b>순수 랜덤이면 장착 2개일 때 P(같은 동작 2연속) = 50%</b>이고, 그게 정확히 사용자가
        /// 싫어한 그림이다(<i>"매번 같은 동작만 나오면 안 된다"</i>). 셔플 백이면 N개 장착 시 같은
        /// 동작이 다시 나오기까지 <b>최소 N−1개가 먼저 나온다</b> — 구조적 보장이다.
        /// 새 셔플의 첫 장이 직전 셔플의 마지막 장과 같으면 다시 섞어 경계 중복도 없앤다.</para>
        ///
        /// <para>★ 뽑기 대상은 «장착»이 아니라 <b>«장착 ∩ 지금 발판에서 가능»</b>이다. 그 교집합이
        /// 비면 <b>춤을 시작하지 않는다</b> — 못 하는 것을 하는 척하지 않는다(원칙 1). 활쏘기가
        /// «과녁 놓을 자리가 없어요»로 포기하는 것과 완전히 같은 선례다.</para>
        /// </summary>
        private bool TryDrawDance(out string danceId, out string reason)
        {
            danceId = null;
            reason = null;

            IReadOnlyList<string> equipped = CurrencyModel.EquippedDanceIds;
            if (equipped == null || equipped.Count == 0)
            {
                // DanceIds.Normalize가 빈 집합을 무료 2종으로 되메우므로 정상 경로에서는 올 수 없다.
                reason = "장착된 춤이 없습니다";
                return false;
            }

            ResolveFootingClearance(out float leftHeights, out float rightHeights);

            // 이번에 «가능»한 것만 남긴 뒤 셔플 백에서 뽑는다. 백에 남은 장이 전부 불가능하면
            // 다시 채워서 한 바퀴만 더 본다(무한 루프를 만들지 않는다).
            for (int attempt = 0; attempt < 2; attempt++)
            {
                for (int i = 0; i < _bag.Count; i++)
                {
                    string candidate = _bag[i];
                    if (!IsPossibleHere(candidate, leftHeights, rightHeights)) continue;
                    _bag.RemoveAt(i);
                    _lastDrawn = candidate;
                    danceId = candidate;
                    return true;
                }
                RefillBag(equipped);
            }

            reason = $"지금 발판에서 가능한 춤이 없습니다(좌 {leftHeights:F2} H / 우 {rightHeights:F2} H — " +
                $"제자리 동작은 좌우 {InPlaceClearanceHeights:F2} H가 필요합니다)";
            return false;
        }

        private void RefillBag(IReadOnlyList<string> equipped)
        {
            _bag.Clear();
            for (int i = 0; i < equipped.Count; i++) _bag.Add(equipped[i]);

            // Fisher-Yates. 새 셔플의 첫 장이 직전 셔플의 마지막 장과 같으면 한 번 더 섞는다
            // (2장 이하일 때는 무한히 실패할 수 있으므로 시도 횟수를 막는다).
            for (int reshuffle = 0; reshuffle < 4; reshuffle++)
            {
                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
                }
                if (_bag.Count <= 1) break;
                if (_lastDrawn == null || !string.Equals(_bag[0], _lastDrawn, System.StringComparison.Ordinal)) break;
            }
        }

        /// <summary>이 동작을 지금 발판에서 할 수 있는가. 발판 폭만 보고 높이는 보지 않는다 —
        /// Dock 위에서도 제자리 5종은 사실상 항상 가능하다(최대 배율에서 41.9pt).</summary>
        internal static bool IsPossibleHere(string danceId, float leftHeights, float rightHeights)
        {
            if (leftHeights < InPlaceClearanceHeights || rightHeights < InPlaceClearanceHeights) return false;

            float widest = Mathf.Max(leftHeights, rightHeights);
            if (string.Equals(danceId, DanceIds.Moonwalk, System.StringComparison.Ordinal))
                return widest >= MoonwalkClearanceHeights;
            if (string.Equals(danceId, DanceIds.StarJump, System.StringComparison.Ordinal))
                return widest >= StarJumpClearanceHeights;
            return true;   // 제자리 5종.
        }

        /// <summary>
        /// 지금 딛고 있는 발판에서 좌우로 남은 여유(신장 H 배수). 구간은 활쏘기와 <b>같은 방식</b>으로
        /// 잡는다 — <b>딛고 있는 발판의 실측 좌우 경계</b>(추정하지 않는다)와 <b>걸어다닐 수 있는
        /// 화면 범위</b>의 교집합이다(같은 값의 두 번째 계산원을 만들지 않는다).
        /// </summary>
        private void ResolveFootingClearance(out float leftHeights, out float rightHeights)
        {
            leftHeights = 0f;
            rightHeights = 0f;

            StickmanBlackboard blackboard = _player.Blackboard;
            if (blackboard == null || blackboard.Body == null) return;

            GroundSensor.GroundInfo ground = blackboard.SenseGround();
            float lo = ground.CurrentFootholdLeftWorldX;
            float hi = ground.CurrentFootholdRightWorldX;
            if (blackboard.TryGetWalkableScreenBoundsWorld(out float screenLeft, out float screenRight))
            {
                lo = Mathf.Max(lo, screenLeft);
                hi = Mathf.Min(hi, screenRight);
            }
            if (!(hi > lo)) return;

            float footX = blackboard.Body.position.x;
            float height = Mathf.Max(0.0001f, blackboard.CharacterHeightWorld);
            leftHeights = Mathf.Max(0f, footX - lo) / height;
            rightHeights = Mathf.Max(0f, hi - footX) / height;
        }

        // ============================================================================
        // 종료 · 휴지
        // ============================================================================

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (evt.From != StickmanStateId.Dance) return;
            if (!_active) return;
            _active = false;

            // ★ 상태에서 빠지는 **모든** 경로가 여기를 지난다(정상 종료 / 강제 인터럽트 / 발판 상실 /
            //   외력 랙돌 / 하드캡 감시견). 락 해제를 상태 쪽에 두지 않고 여기 한 곳에 모으는 이유가
            //   그것이다 — 락을 든 채 상태만 빠지는 것이 이 저장소가 가장 자주 낸 사고 형태다.
            SpectacleEventLock.Release(this);
            OnEpisodeFinished();

            Debug.Log($"{LogTag} 에피소드 종료 — {evt.To}(으)로 전이(강제인터럽트={evt.IsForcedInterrupt}, " +
                $"비정상이탈={evt.IsAbnormalExit}). 락을 놓았고 {_restRemaining:F1}초 휴지에 들어갑니다" +
                $"(다음은 창 안 {_episodeIndex}번째, 피로 계수 {ResolveFatigue(_episodeIndex):F2}). " +
                "휴지 중에는 상태도 락도 잡지 않으므로 다른 연출이 정상적으로 발동합니다.");
        }

        /// <summary>에피소드가 끝났다 — 피로 카운터를 올리고 휴지를 잡는다.
        /// <inheritdoc cref="OpenDanceWindow"/></summary>
        internal void OnEpisodeFinished()
        {
            _episodeIndex++;
            ScheduleRest();
        }

        /// <summary>휴지 = <c>U(min, max) × F(n)</c>. 창이 닫히면 n이 1로 돌아가 다시 짧아진다.</summary>
        private void ScheduleRest()
        {
            float min = Mathf.Max(0f, _config != null ? _config.danceRestMinSeconds : 20f);
            float max = Mathf.Max(min, _config != null ? _config.danceRestMaxSeconds : 45f);
            _restRemaining = Random.Range(min, max) * ResolveFatigue(_episodeIndex);
        }

        /// <summary>설정에서 계수를 읽어 <see cref="ResolveFatigue(int,float,float)"/>에 넘기는 얇은 껍데기.</summary>
        internal float ResolveFatigue(int episodeIndex)
            => ResolveFatigue(episodeIndex,
                _config != null ? _config.danceRestFatigueStep : 0.25f,
                _config != null ? _config.danceRestFatigueCap : 3f);

        /// <summary>
        /// ★ 피로 계수 <c>F(n) = min(1 + step·(n−1), cap)</c> — <b>순수 함수</b>.
        ///
        /// <para>씬 없이 n을 훑을 수 있어야 «창 안에서 자란다 / 상한에서 멈춘다 / 창이 닫히면
        /// 되돌아온다»를 <b>모든 n에 대해</b> 확인할 수 있다. 인스턴스 메서드만 두면 테스트가
        /// 몇 개의 n만 찔러 보게 되고, 그건 «성공한 측정과 똑같이 생긴 실패한 측정»이 된다.</para>
        ///
        /// <para><paramref name="step"/>을 0으로 두면 피로 램프가 없는 예전 설계(평탄한 듀티 27%)로
        /// 정확히 되돌아간다 — 회귀 테스트의 네거티브 컨트롤이 이 성질을 쓴다.</para>
        /// </summary>
        internal static float ResolveFatigue(int episodeIndex, float step, float cap)
            => Mathf.Min(1f + Mathf.Max(0f, step) * Mathf.Max(0, episodeIndex - 1), Mathf.Max(1f, cap));

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null) return;
            if (_player.Blackboard.Machine.CurrentStateId == StickmanStateId.Dance)
            {
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
        }

        // ============================================================================
        // 참조 확보
        // ============================================================================

        private DanceState ResolveDanceState()
        {
            StickmanStateMachine machine = _player != null ? _player.Blackboard?.Machine : null;
            if (machine == null) return null;
            if (machine.CurrentStateId != StickmanStateId.Dance) return null;
            return machine.GetState(StickmanStateId.Dance) as DanceState;
        }

        /// <summary>집중 세션 감시자 — 못 찾아도 매 틱 다시 찾지 않는다.
        /// <b>못 찾으면 <see cref="AudioReactiveDanceGate.BlocksNow"/>가 «막는다»로 떨어진다</b>,
        /// 그리고 그것이 자동 발동 경로의 안전한 방향이다(«모르면 안 한다»).</summary>
        private FocusWatchDirector ResolveFocus()
        {
            if (_focus != null) return _focus;
            if (Time.unscaledTime < _nextLookupTime) return null;
            _nextLookupTime = Time.unscaledTime + ReferenceLookupRetrySeconds;
            _focus = FindAnyObjectByType<FocusWatchDirector>();
            return _focus;
        }
    }
}
