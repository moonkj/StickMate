using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상호작용 상태: 격파 미니게임(docs/UX_FLOW.md 10절) — 기 모으기 게이지(1.5~2초) →
    /// 스위트스팟(70~85%) → 클릭 판정 → 성공/실패(재도전 최대 3회) → 5초 무입력 타임아웃.
    ///
    /// 진입: Interaction/BattleMinigameDirector가 (유휴 저확률 추첨 또는 트레이 메뉴 수동 트리거로)
    /// 부분적 클릭관통 해제 + SpectacleEventLock을 확보했을 때만 Machine.ChangeState(BattleMinigame)를
    /// 호출한다. 클릭 판정 대상은 캐릭터 자신의 히트박스 + 소환된 오브젝트/게이지의 콜라이더 둘 다이며
    /// (UX 10절 "캐릭터/오브젝트의 화면 히트박스 영역"), 후자는 시각 레이어인
    /// Interaction/BattleMinigameRenderer가 런타임에 만들어 StickmanClickHitbox에 등록한다 —
    /// 이 상태 클래스는 그 존재를 전혀 모른다(둘 다 결국 같은 MouseDown 이벤트 하나로 합류한다).
    ///
    /// 클릭 입력 경로: Interaction/BattleMinigameDirector가 StickmanClickHitbox.MouseDown을 구독해
    /// blackboard.BattleClickSignaled를 세팅하고, 이 상태의 Tick()이 매 프레임 그 신호를 소비한다
    /// (DragThrowState의 DragReleaseSignaled와 동일한 컨벤션).
    ///
    /// [self-transition, Architect 지시 2026-08-27 — Tasklist.md 교차 레이어 로그] "릴리즈 순간"
    /// (클릭으로 성공/실패가 갈리는 그 프레임)의 대사(UX_FLOW.md 31-2 표 #5)는 DialogueIntent가 오직
    /// Enter() 안에서만 만들어질 수 있다는 원칙(31-1/9절-1)에 예외를 두지 않는다. 대신 RagdollState가
    /// 반복 피격 때 쓰는 것과 동일한 패턴을 재사용한다: 판정에 필요한 파라미터(chargeRatio)를 재전이
    /// 직전에 필드에 기록해두고, 같은 상태로 자기 자신을 다시 ChangeState()해 Exit()→Enter()를
    /// 재실행시킨다 — "판정 순간"과 "전이 확정 순간"이 코드 구조상 같은 프레임의 같은 사건이 된다.
    /// TickCharging()이 판정을 직접 내리지 않고 TriggerResolution()으로 자기-전이만 시키면,
    /// 실제 판정(성공/실패/재도전/소진)과 대사 파생은 전부 Enter()의 ResolveOutcome()이 담당한다.
    ///
    /// ★ 2026-09-01 — <b>재충전도 self-transition이다</b>(자기-전이 2종: 릴리즈 / 재충전). 재도전
    ///   게이지를 다시 채우기 시작하는 것 역시 "대사가 파생될 자격이 있는 확정된 사건"이고,
    ///   전이가 아니면 (a) 직전 판정 대사가 만료되지 않아 <b>기를 다시 모으는 그림 위에 기가 빠졌다는
    ///   문장</b>이 남으며 (b) 개시 대사가 재도전에서 구조적으로 도달 불가가 된다. 자세한 실측은
    ///   TickResolving() 주석 참고 — 그 비대칭이 절대 불변 원칙 1 위반 두 건의 공통 원인이었다.
    ///
    /// ============================================================================
    /// ★★ 2026-09-01 전면 개정 — 결과 축(BattleRelease) 도입 (절대 불변 원칙 1 위반 수정)
    /// ============================================================================
    /// <b>정직 기록: 이 코드는 사양서를 정확히 구현했고, 틀린 것은 사양서였다.</b> 구판
    /// docs/UX_FLOW.md 31-2 표 #5는 판정 대사를 <c>chargeRatio &gt;= 0.9</c>로 규정했는데
    ///     성공      ⟺ 0.70 ≤ r ≤ 0.85          (battleSweetSpot*)
    ///     "필살기다!" ⟺ r ≥ 0.9
    ///     ∴ 성공 ∩ "필살기다!" = ∅              ← 두 구간이 서로소
    /// 이므로 이 대사는 <b>정의상 실패 시에만</b> 나왔다. 여기에 무클릭 자동 릴리즈(r = 1.0)가 겹치면
    /// 무인 관측에서는 모든 판정이 r = 1.0이라 <b>실패 100% → "필살기다!" 100%</b>가 된다.
    /// 실기 로그의 "판정=실패 50건 → 직후 대사 50건 전부 필살기다!"는 표본이 아니라 항등식이었다.
    ///
    /// 진짜 결함은 임계값 하나가 아니라 <b>결과 축이 파라미터에 없다</b>는 것이었다 — ResolveOutcome()이
    /// 성공/실패·재도전 잔여·종료 여부를 그 자리에서 전부 계산한 뒤 <b>버려서</b>, 대사 매핑 함수는 그
    /// 상태에서 가장 중요한 확정 사실을 볼 수가 없었다. 구조가 원칙 1을 지킬 수 없게 되어 있었다.
    /// 그래서 숫자 교체가 아니라 <see cref="BattleRelease"/> 도입이 개정의 본체다.
    /// 부수 효과: 고유 대사 <b>2종 → 10종</b>, 그리고 구조적으로 구분 불가능하던
    /// "늦게 눌렀다"(LateMiss)와 "안 눌렀다"(NoInput)가 갈린다.
    ///
    /// ★ 개시 대사는 UX_FLOW.md 10절의 "좋아, 간다"에서 31-2 #5 개정판(게이지 길이에서 파생되는
    ///   "천천히... 모은다" / "빠르다, 집중")으로 교체됐다. 10절 1)행이 아직 구 문자열을 인용하고
    ///   있으므로 문서 동기화가 필요하다(UX_FLOW는 ux-designer 소유 — 리더 라우팅 대상).
    ///
    /// ★ 타임아웃(5초 무입력) 경로는 <b>의도적으로 침묵한다</b>. 그 판정의 의미가 "유저가 이미 다른
    ///   작업으로 이탈했다"이기 때문이다 — 이탈한 유저에게 말을 거는 것은 절대 불변 원칙 2(업무 방해
    ///   제로) 위반이다. 그래서 그 경로는 Release 값을 만들지 않고 조용히 Idle로 보낸다.
    /// </summary>
    public sealed class BattleMinigameState : IStickmanState, IHasDialogueParams
    {
        private enum Phase { Charging, Resolving }

        /// <summary>
        /// 이번 <c>Enter()</c>가 <b>무엇 때문에</b> 불렸는가. self-transition이 두 종류가 되면서
        /// bool 두 개로는 "둘 다 true"라는 있을 수 없는 상태가 표현 가능해지므로 열거로 고정한다.
        /// </summary>
        private enum EntryCause
        {
            /// <summary>Director가 밖에서 트리거한 새 대결(유일하게 self-transition이 아닌 경로).</summary>
            FreshStart,
            /// <summary>릴리즈 순간 — <see cref="TriggerResolution"/>이 건 자기-전이.</summary>
            Resolution,
            /// <summary>재도전 게이지 재충전 — <see cref="TickResolving"/>이 건 자기-전이.</summary>
            Recharge,
        }

        private readonly StickmanBlackboard _blackboard;

        private Phase _phase;
        private float _chargeElapsed;
        private float _chargeDuration;
        private int _retryCount;
        private float _noInputTimer; // 이벤트 시작 후 무클릭 누적 시간(10절 "5초 이상 클릭 입력이 전혀 없으면 자동 취소")
        private float _resolveTimer;
        private bool _terminal; // 이번 Resolving이 끝나면 종료(Idle 복귀)인지, 재도전인지.

        // self-transition 패턴용 보류 파라미터 — 자기-전이를 거는 쪽이 기록하고 다음 Enter()가 소비한다.
        // 기본값 FreshStart는 "밖에서 들어온 새 대결"이며, 소비 즉시 여기로 되돌아간다.
        private EntryCause _pendingEntry = EntryCause.FreshStart;
        private float _pendingChargeRatio;
        /// <summary>★ 이 한 필드가 "늦게 눌렀다"(LateMiss)와 "안 눌렀다"(NoInput)를 가른다.
        /// 두 경로는 예전에 똑같이 TriggerResolution(1f)로 합류해 스냅샷이 구분 불가능했다.</summary>
        private bool _pendingClicked;

        /// <summary>
        /// 이번 판정이 **무엇이었는가** — UX_FLOW.md 31-2 표 #5의 결과 축(2026-09-01 신규).
        ///
        /// 왜 필요했나(정직 기록): 구판 파라미터에는 <c>ChargeRatio</c> 하나뿐이었고, 성공/실패는
        /// <c>ResolveOutcome()</c>이 그 자리에서 계산한 뒤 <b>버렸다</b>. 그래서 대사 매핑 함수는
        /// 그 상태에서 가장 중요한 확정 사실을 볼 수가 없었고, 표가 규정한 임계값
        /// (<c>chargeRatio &gt;= 0.9</c>)은 성공 구간(0.70~0.85)과 <b>서로소</b>라
        /// "필살기다!"가 <b>정의상 실패 시에만</b> 나왔다(실기 로그 50/50, 100%).
        /// 구조가 절대 불변 원칙 1을 지킬 수 없게 되어 있었던 것이다.
        /// </summary>
        public enum BattleRelease
        {
            /// <summary>최초 진입(게이지 시작). 아직 판정 전.</summary>
            Opening,
            /// <summary>스위트스팟 안에서 클릭.</summary>
            Success,
            /// <summary>클릭했으나 r &lt; sweetStart.</summary>
            EarlyMiss,
            /// <summary>클릭했으나 r &gt; sweetEnd.</summary>
            LateMiss,
            /// <summary>클릭 없이 게이지가 만충되어 자동 릴리즈(r = 1.0).</summary>
            NoInput,
        }

        /// <summary>
        /// UX_FLOW.md 31-2 표 #5 대응 파라미터(2026-09-01 전면 개정).
        ///
        /// 스위트스팟 경계까지 **스냅샷으로 함께 싣는** 이유: 매핑 함수가 StickConfig를 직접 읽으면
        /// "판정에 쓰인 값"과 "대사에 쓰인 값"이 서로 다른 시점의 설정일 수 있다(설정은 런타임에
        /// 바뀐다). 31-1은 하나의 Enter, 하나의 스냅샷을 요구한다.
        /// </summary>
        public sealed class BattleDialogueParams
        {
            /// <summary>★ 결과 축. 이 값이 없으면 대사가 판정을 볼 수 없다.</summary>
            public BattleRelease Release;
            public float ChargeRatio;
            /// <summary>확정된 남은 재도전 횟수(성공/소진이면 0).</summary>
            public int RetriesLeft;
            /// <summary>이번 판정으로 대결이 끝나는가.</summary>
            public bool Terminal;
            /// <summary>이번 게이지의 확정 길이(초) — 개시 대사용.</summary>
            public float ChargeDurationSeconds;
            /// <summary>
            /// 개시 대사가 "천천히"와 "빠르다"로 갈리는 기준(초) = 게이지 길이 추첨 밴드
            /// [battleChargeDurationMin, battleChargeDurationMax]의 중점. 기본값에서 (1.5+2.0)/2 =
            /// <b>1.75</b>로 UX_FLOW.md 31-2 표 #5의 문자값과 정확히 일치한다.
            ///
            /// ★ 표의 1.75를 상수로 박지 않은 이유가 이번 사고의 교훈 그 자체다(31-4 C2): 밴드를
            /// 0.8~1.2로 조이는 순간 고정 임계 1.75는 밴드와 <b>서로소</b>가 되어 "천천히... 모은다"가
            /// 구조적으로 도달 불가가 된다. 분기 기준은 그 분기를 만드는 값과 같은 축 위에 둔다.
            /// </summary>
            public float ChargeDurationMid;
            public float SweetStart;
            public float SweetEnd;
        }

        private readonly BattleDialogueParams _dialogueParams = new BattleDialogueParams();

        public object DialogueParams => _dialogueParams;

        public StickmanStateId StateId => StickmanStateId.BattleMinigame;

        public BattleMinigameState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Enter(StateTransitionContext context)
        {
            EntryCause cause = _pendingEntry;
            _pendingEntry = EntryCause.FreshStart; // 소비 즉시 리셋(이 프로젝트의 1회성 펄스 관례).

            if (cause == EntryCause.Resolution)
            {
                ResolveOutcome(_pendingChargeRatio, _pendingClicked, context);
                return;
            }

            // 새 대결이면 사이클 카운터를 리셋한다. 재충전(Recharge)은 **같은 대결의 계속**이므로
            // _retryCount도 _noInputTimer도 건드리지 않는다 — 무입력 타임아웃은 "이벤트 시작 후"
            // 누적이지 "이번 게이지 시작 후"가 아니다.
            if (cause == EntryCause.FreshStart)
            {
                _retryCount = 0;
                _noInputTimer = 0f;
            }

            BeginCharge();

            // ★ 개시 대사(31-2 표 #5의 Opening 두 행). 파라미터는 BeginCharge()가 _chargeDuration을
            //   확정한 **직후**에 채운다 — 대사가 "천천히 모은다 / 빠르다, 집중" 중 무엇이 될지는
            //   이번 게이지 길이라는 **이미 확정된 사실**에서만 나온다.
            //   Narrative(진행 서술)이므로 규칙 8 발화 자격 게이트를 통과해야 한다.
            SnapshotDialogueParams(BattleRelease.Opening, chargeRatio: 0f, retriesLeft: AttemptsNotYetStarted(),
                terminal: false);
            _ = DialogueIntent.TryCreate(context, ResolveDialogueLine, PlannedChargeDwellSeconds());
        }

        /// <summary>설정의 최대 재도전 횟수(스냅샷 창구를 한 곳으로 모은다).</summary>
        private int ResolveMaxRetries() => _blackboard.Config != null ? _blackboard.Config.battleMaxRetries : 3;

        /// <summary>무입력 자동 취소까지의 설정값(초). Tick()과 게이트가 같은 창구를 쓴다.</summary>
        private float ResolveInputTimeoutSeconds()
            => _blackboard.Config != null ? _blackboard.Config.battleInputTimeoutSeconds : 5f;

        /// <summary>
        /// 아직 <b>시작되지 않은</b> 시도 횟수 — <see cref="BattleDialogueParams.RetriesLeft"/>의 정의.
        ///
        /// 개시(_retryCount == 0)에서는 maxRetries 그대로이고, 재충전에서는 지금 시작하는 시도가
        /// 빠지므로 하나씩 줄어든다. 판정 시점의 <c>maxRetries - _retryCount + 1</c>과 **같은 축의
        /// 같은 양**이다(판정은 "다음 시도가 아직 시작 전"인 시점이라 +1이 붙는다). 두 곳이 서로 다른
        /// 의미의 숫자를 같은 필드에 넣으면 대사가 그 필드로 분기하는 순간 조용히 틀린다.
        /// </summary>
        private int AttemptsNotYetStarted() => Mathf.Max(0, ResolveMaxRetries() - _retryCount);

        /// <summary>
        /// ★ 개시 대사의 **계획 잔여 체류 시간**(초, 규칙 8) — 게이지 길이 <b>와</b> 무입력 타임아웃
        /// 잔여 중 <b>짧은 쪽</b>이다.
        ///
        /// 왜 게이지 길이만으로는 틀리는가: 무입력 타이머는 이벤트 시작부터 누적되고 재충전에서
        /// 리셋되지 않는다. 그래서 마지막 재충전은 게이지를 다 채우기 전에 타임아웃으로 끊기는 일이
        /// 실제로 생기고, 그때 "천천히... 모은다"는 게이지가 아니라 <b>0.1초</b>만 살아 있다가 즉시
        /// 컷된다 — 규칙 8이 없애려던 "번쩍이는 글자" 그 자체다. 상태가 이미 아는 종료 사유를
        /// 게이트에 안 알려주면 게이트는 그것을 알 방법이 없다.
        /// </summary>
        private float PlannedChargeDwellSeconds()
            => Mathf.Min(_chargeDuration, Mathf.Max(0f, ResolveInputTimeoutSeconds() - _noInputTimer));

        /// <summary>
        /// 대사 파라미터를 **한 곳에서만** 확정한다. 개시/판정 두 경로가 각자 필드를 채우면 어느 한쪽이
        /// 새 필드를 빠뜨렸을 때 대사가 "직전 판정의 잔재"에서 파생된다 — 31-1이 막으려는 바로 그 형태다.
        /// </summary>
        private void SnapshotDialogueParams(BattleRelease release, float chargeRatio, int retriesLeft, bool terminal)
        {
            _dialogueParams.Release = release;
            _dialogueParams.ChargeRatio = chargeRatio;
            _dialogueParams.RetriesLeft = retriesLeft;
            _dialogueParams.Terminal = terminal;
            _dialogueParams.ChargeDurationSeconds = _chargeDuration;
            float durMin = _blackboard.Config != null ? _blackboard.Config.battleChargeDurationMin : 1.5f;
            float durMax = _blackboard.Config != null ? _blackboard.Config.battleChargeDurationMax : 2.0f;
            _dialogueParams.ChargeDurationMid = 0.5f * (durMin + Mathf.Max(durMin, durMax));
            _dialogueParams.SweetStart = _blackboard.Config != null ? _blackboard.Config.battleSweetSpotStart : 0.70f;
            _dialogueParams.SweetEnd = _blackboard.Config != null ? _blackboard.Config.battleSweetSpotEnd : 0.85f;
        }

        private void BeginCharge()
        {
            _phase = Phase.Charging;
            _chargeElapsed = 0f;

            float min = _blackboard.Config != null ? _blackboard.Config.battleChargeDurationMin : 1.5f;
            float max = _blackboard.Config != null ? _blackboard.Config.battleChargeDurationMax : 2.0f;
            _chargeDuration = max > min ? Random.Range(min, max) : min;

            _blackboard.BattleClickSignaled = false;

            // 렌더 힌트 초기화 — 이 시점부터 게이지가 화면에 보이기 시작한다(StickmanBlackboard의
            // BattleChargeRatio/BattleChargeGaugeVisible 문서 참고: 판정에는 전혀 쓰이지 않는 단방향 통보).
            _blackboard.BattleChargeRatio = 0f;
            _blackboard.BattleChargeGaugeVisible = true;
        }

        public void Tick(float deltaTime)
        {
            _noInputTimer += deltaTime;
            if (_noInputTimer >= ResolveInputTimeoutSeconds())
            {
                // "유저가 다른 작업으로 이탈"로 간주 — 부분적 클릭관통 해제는 Interaction/
                // BattleMinigameDirector가 이 상태의 Exit(=StateTransitioned, To!=BattleMinigame)을
                // 구독해 원복한다.
                //
                // ★ 2026-09-02 — 예전에는 여기도 아래 재도전 소진과 **같은 값**(Exhausted)을 쐈다.
                //   두 경로는 화면 결과가 다른데(소진은 "오늘은 여기까지"를 말하고 끝나지만 여기는
                //   대사가 아예 없다) 구독자가 구분할 수 없었다. 값을 쪼개 1:1로 맞춘다.
                //
                // ★ 그리고 이 경로에는 **로그가 한 줄도 없었다.** 대사도 없고 사유 구분도 없어
                //   계기판이 0이었다 — 창을 볼 수 없는 환경에서 "왜 갑자기 끝났는지"를 재구성할
                //   방법이 없다는 뜻이다. 그래서 그 순간의 실제 숫자를 함께 남긴다(이산 경로라
                //   문자열 할당이 상주 비용에 잡히지 않는다 — GroundSensor.DescribeGroundLoss와
                //   같은 관례).
                Debug.Log($"[격파] 중단=무입력 타임아웃 — {ResolveInputTimeoutSeconds():F1}초 동안 클릭이 " +
                    $"없었습니다(경과 {_noInputTimer:F2}초). 이번 사이클에 끝난 시도 {_retryCount}회 / " +
                    $"설정 최대 재도전 {ResolveMaxRetries()}회, 중단 시점 페이즈=" +
                    $"{(_phase == Phase.Charging ? "충전 중" : "판정 후 대기")}. " +
                    "**판정이 아니라 중단이므로 대사는 없습니다**(재도전 소진과는 다른 사건입니다 — " +
                    "그쪽은 \"오늘은 여기까지\"를 말하고 끝납니다).");

                StickmanEventBus.RaiseBattleMinigamePhaseChanged(BattleMinigamePhase.InputTimeout);
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            if (_phase == Phase.Charging) TickCharging(deltaTime);
            else TickResolving(deltaTime);
        }

        private void TickCharging(float deltaTime)
        {
            _chargeElapsed += deltaTime;
            float ratio = _chargeDuration > 0f ? Mathf.Clamp01(_chargeElapsed / _chargeDuration) : 1f;
            _blackboard.BattleChargeRatio = ratio; // 렌더 힌트(게이지 바) — 판정은 아래에서 ratio로 직접 한다.

            if (_blackboard.BattleClickSignaled)
            {
                _blackboard.BattleClickSignaled = false;
                _noInputTimer = 0f; // 맞았든 틀렸든 "클릭 입력이 있었다"는 사실 자체로 무입력 타이머 리셋.
                TriggerResolution(ratio, clicked: true);
                return;
            }

            if (ratio >= 1f)
            {
                // 끝까지 클릭이 전혀 없었음 -> 미스(실패)로 취급(ratio=1.0 스냅샷, 무한정 같은 게이지에 머무르지 않게 함).
                TriggerResolution(1f, clicked: false);
            }
        }

        /// <summary>
        /// "릴리즈 순간"의 실제 판정(성공/실패/재도전/소진)과 대사 파생은 여기서 직접 하지 않는다 —
        /// 스냅샷만 기록해두고 같은 상태로 자기 자신을 재전이시켜, Enter()의 ResolveOutcome()이 그
        /// 값을 읽어 처리하게 한다(위 클래스 주석의 self-transition 패턴).
        /// </summary>
        /// <param name="clicked">
        /// ★ 이 인자 하나가 <see cref="BattleRelease.LateMiss"/>("늦게 눌렀다")와
        /// <see cref="BattleRelease.NoInput"/>("아예 안 눌렀다")를 가른다. 두 경로는 예전에 똑같이
        /// <c>TriggerResolution(1f)</c>로 합류해 스냅샷이 구조적으로 구분 불가능했고, 그래서 유저 경험이
        /// 완전히 다른 두 사건이 같은 대사를 받았다(교차 레이어 로그 L5).
        /// </param>
        private void TriggerResolution(float chargeRatio, bool clicked)
        {
            _pendingChargeRatio = chargeRatio;
            _pendingClicked = clicked;
            _pendingEntry = EntryCause.Resolution;
            _blackboard.Machine.ChangeState(StickmanStateId.BattleMinigame, isForcedInterrupt: false);
        }

        /// <summary>
        /// 릴리즈 self-transition으로 재실행된 Enter() 안에서만 호출된다. 성공/실패/재도전/소진 판정과
        /// StickmanEventBus 통지, "릴리즈 순간" DialogueIntent 생성을 모두 이 시점(=전이 확정 시점)에서
        /// 함께 처리해 판정과 대사 파생이 항상 같은 프레임의 같은 사건이 되게 한다.
        /// </summary>
        private void ResolveOutcome(float chargeRatio, bool clicked, StateTransitionContext context)
        {
            float sweetStart = _blackboard.Config != null ? _blackboard.Config.battleSweetSpotStart : 0.70f;
            float sweetEnd = _blackboard.Config != null ? _blackboard.Config.battleSweetSpotEnd : 0.85f;
            bool success = chargeRatio >= sweetStart && chargeRatio <= sweetEnd;

            // ★ 결과 축 확정(31-2 표 #5). clicked가 없으면 아래 두 줄이 구분되지 않는다.
            BattleRelease release =
                success ? BattleRelease.Success
                : !clicked ? BattleRelease.NoInput
                : chargeRatio < sweetStart ? BattleRelease.EarlyMiss
                : BattleRelease.LateMiss;

            _phase = Phase.Resolving;
            _resolveTimer = 0f;

            // 판정이 끝난 구간에서는 게이지를 감춘다 — "지금 클릭해도 판정에 안 먹힌다"는 사실을
            // 게이지 유무만으로 알 수 있게 해 헛클릭을 줄인다(재도전이면 BeginCharge가 다시 켠다).
            _blackboard.BattleChargeRatio = chargeRatio;
            _blackboard.BattleChargeGaugeVisible = false;

            int maxRetries = ResolveMaxRetries();
            int retriesLeft;

            if (success)
            {
                _terminal = true;
                retriesLeft = 0;
                StickmanEventBus.RaiseBattleMinigamePhaseChanged(BattleMinigamePhase.Success);
            }
            else
            {
                _retryCount++;
                if (_retryCount > maxRetries)
                {
                    _terminal = true;
                    retriesLeft = 0;
                    // ★ 2026-09-02 — 무입력 타임아웃(Tick의 위쪽 분기)과 **다른 값**이다. 이쪽만
                    //   "오늘은 여기까지" 대사를 동반한다.
                    StickmanEventBus.RaiseBattleMinigamePhaseChanged(BattleMinigamePhase.RetriesExhausted);
                }
                else
                {
                    _terminal = false;
                    // "남은 재도전 횟수"는 지금 확정된 사실이다 — TickResolving()이 재충전 자기-전이
                    // 외의 경로를 갖지 않으므로(_terminal == false), 이 값이 1이면 "한 번 더 한다"가
                    // 이미 확정돼 있다. 그래서 "마지막 한 번!"은 미래형 약속이 아니다(31-2 #5 비고).
                    //
                    // +1은 시점 차이 하나뿐이다: Enter()는 "지금 시작하는 시도"를 이미 뺀 뒤의 값을
                    // 쓰고, 여기는 다음 시도가 **아직 시작 전**이라 그 하나가 남아 있다. 두 자리가
                    // 각자 산식을 손으로 쓰면 밴드가 바뀌는 날 조용히 갈라지므로 한 함수를 공유한다.
                    retriesLeft = AttemptsNotYetStarted() + 1;
                    StickmanEventBus.RaiseBattleMinigamePhaseChanged(BattleMinigamePhase.Fail);
                }
            }

            SnapshotDialogueParams(release, chargeRatio, retriesLeft, _terminal);

            Debug.Log($"[격파] 판정 — 결과={release}, 게이지={chargeRatio:F3}" +
                $"(스위트 {sweetStart:F2}~{sweetEnd:F2}), 클릭={(clicked ? "있음" : "없음")}, " +
                $"남은재도전={retriesLeft}, 종료={_terminal}.");

            // UX_FLOW.md 31-2 표 #5 — **결과 축을 실은** 파라미터 하나에서 파생되는 순수 함수 분기.
            // 판정 대사는 전부 Reaction(점 사건 서술)이라 발화 자격 게이트를 타지 않는다.
            _ = new DialogueIntent(context, ResolveDialogueLine);
        }

        /// <summary>
        /// ★ 텍스트 매핑 함수(순수). UX_FLOW.md 31-2 표 #5를 **위에서 아래로 최초 일치(first-match)**
        /// 순서 그대로 옮긴 것이며, <b>그 순서 자체가 계약이다</b>.
        ///
        /// 순서가 왜 계약인가: <see cref="BattleRelease.Opening"/> 분기가 <c>RetriesLeft</c> 분기보다
        /// 위에 있어야 한다. 개시 시점의 스냅샷은 재도전 잔여를 최대치로 들고 있는데, 최대 재도전이
        /// 1로 설정된 환경에서는 <c>Release == Opening &amp;&amp; RetriesLeft == 1</c>이 동시에 참이 되어
        /// 개시 대사 자리에 "마지막 한 번!"이 나온다 — 순서를 바꾸는 순간 생기는 새 모호성이다.
        ///
        /// 이 함수는 난수/시간/설정을 읽지 않는다. 모든 입력이 <see cref="BattleDialogueParams"/>
        /// 스냅샷 하나에서 온다(31-1).
        /// </summary>
        /// <remarks>★ 2026-09-02 — <c>private</c>에서 <c>internal</c>로 열었다(공개 API는 늘지 않는다 —
        /// Scripts/AssemblyInfo.cs의 InternalsVisibleTo가 EditMode 테스트에만 보인다).
        /// 이유: 이 함수는 <b>순수 함수</b>라 파라미터만 바꿔 분기를 직접 확인할 수 있는데, 그러려면
        /// 상태 머신을 실제로 돌려야만 검증 가능한 조건들이 있다 — 특히 <c>"마지막 한 번!"</c>의
        /// <c>Release != NoInput</c> 조건은 상태를 돌려서는 <b>무클릭 경로로 재현할 수 없다</b>
        /// (그 경로에서는 애초에 재도전 잔여 1에 도달하지 못한다). 순수 함수를 순수 함수로
        /// 검사하게 두는 것이 31-1의 이점을 실제로 쓰는 방법이다.
        /// Tests/EditMode/BattleUnattendedReachabilityTests가 유일한 소비자다.</remarks>
        internal static DialogueLine ResolveDialogueLine(StickmanStateId id, object dialogueParams)
        {
            var p = dialogueParams as BattleDialogueParams;
            if (p == null) return DialogueLine.React("어... 어라?"); // 방어적 폴백(정상 경로에서는 도달하지 않음).

            // ── 개시 (반드시 첫 분기 — 위 요약 참고). 진행 서술이므로 Narrative.
            if (p.Release == BattleRelease.Opening)
            {
                return p.ChargeDurationSeconds >= p.ChargeDurationMid
                    ? DialogueLine.Say("천천히... 모은다")
                    : DialogueLine.Say("빠르다, 집중");
            }

            // ── 성공. ★ "필살기다!"가 나오는 유일한 자리이며, 그 조건이 성공 판정과 **같은 축**
            //    (스위트스팟) 위에 있다. 구판은 이 둘이 서로소여서 성공 시 0%, 실패 시 100%였다.
            if (p.Release == BattleRelease.Success)
            {
                float mid = 0.5f * (p.SweetStart + p.SweetEnd);
                return p.ChargeRatio >= mid ? DialogueLine.React("필살기다!") : DialogueLine.React("딱 맞았다!");
            }

            // ── 실패이면서 이번이 마지막 판정(재도전 소진).
            if (p.Terminal) return DialogueLine.React("오늘은 여기까지");

            // ── 실패, 재도전이 정확히 한 번 남음(Attack "한 발 더!"와 같은 모양).
            if (p.RetriesLeft == 1 && p.Release != BattleRelease.NoInput) return DialogueLine.React("마지막 한 번!");

            // ── 실패 사유별.
            switch (p.Release)
            {
                case BattleRelease.NoInput:
                    return DialogueLine.React("어... 힘이 다 샜다"); // 무인 관측의 100%가 오는 자리.
                case BattleRelease.EarlyMiss:
                    return p.ChargeRatio < p.SweetStart * 0.5f
                        ? DialogueLine.React("너무 빨랐다")
                        : DialogueLine.React("아, 조금 일렀나");
                default:
                    return DialogueLine.React("아... 늦었다");
            }
        }

        private void TickResolving(float deltaTime)
        {
            _resolveTimer += deltaTime;
            float delay = _terminal
                ? (_blackboard.Config != null ? _blackboard.Config.battleSuccessResolveDelaySeconds : 1.0f)
                : (_blackboard.Config != null ? _blackboard.Config.battleFailRetryDelaySeconds : 1.5f);
            if (_resolveTimer < delay) return;

            if (_terminal)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            // ★ 2026-09-01 — 예전에는 여기서 BeginCharge()를 **직접** 불렀다. 그것이 전이가 아니라서
            //   TransitionGeneration이 오르지 않았고, 결과가 절대 불변 원칙 1 위반 두 개였다:
            //     (a) 직전 판정 대사("어... 힘이 다 샜다")가 만료되지 않아, 캐릭터가 기를 **다시 모으는**
            //         1.88초 동안 화면에는 기가 빠졌다는 문장이 그대로 떠 있었다(실측 노출 3.38초 =
            //         battleFailRetryDelaySeconds 1.5 + 다음 게이지 1.88).
            //     (b) 개시 대사가 Enter()에서만 만들어지는데 재충전이 Enter()를 거치지 않아, 재도전에는
            //         개시 대사가 **구조적으로** 나올 수 없었다(실측 3사이클 동안 개시 대사 1회).
            //   판정 순간을 self-transition으로 만든 것과 정확히 같은 이유로 재충전도 self-transition이다:
            //   "게이지를 새로 채우기 시작했다"는 것도 대사가 파생될 자격이 있는 **확정된 사건**이다.
            //   그 대칭이 반쪽만 있었던 것이 이 결함의 전부였다.
            _pendingEntry = EntryCause.Recharge;
            _blackboard.Machine.ChangeState(StickmanStateId.BattleMinigame, isForcedInterrupt: false);
        }

        /// <summary>
        /// 어떤 경로로 빠져나가든(성공 종료/소진/타임아웃/긴급정지 강제 인터럽트) 게이지 렌더 힌트를
        /// 반드시 끈다 — Director의 OnDisable() 락 반환과 같은 취지의 "중간 상태를 화면에 남기지
        /// 않는다" 관례다. self-transition(재판정 / 재충전) 두 종류 모두에서 Exit()이 호출되지만,
        /// 곧이어 실행되는 Enter()의 ResolveOutcome()/BeginCharge()가 값을 다시 확정하므로 문제되지 않는다.
        /// </summary>
        public void Exit()
        {
            _blackboard.BattleChargeGaugeVisible = false;
        }
    }
}
