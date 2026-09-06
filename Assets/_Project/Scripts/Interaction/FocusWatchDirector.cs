using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>UX_FLOW.md 18절 "감시 민감도(관대/보통/예민) 3단계 슬라이더" — 연속 관찰 주기 임계값에
    /// 곱해지는 배율만 다르다(관대=더 오래 지속돼야 반응, 예민=더 빨리 반응).
    /// <para>★ 2026-09-03 정정 — 원래 "설정창(7절, <b>미구현</b>)이 나중에 이 프로퍼티를 바꿔 끼우면 된다"고
    /// 적혀 있었으나 <c>Interaction/SettingsWindow.cs</c>는 <b>이미 있고 씬에도 배선돼 있다</b>
    /// (Assets/Editor/SceneBootstrapper.cs). 참인 것은 그쪽이 아니라 이것이다:
    /// <b>이 값을 실제로 바꾸는 곳은 <c>Interaction/FocusSessionPopover</c>의 관대/보통/예민 칩 3개뿐이고,
    /// 설정창에는 이 항목이 없다.</b> "설정창이 없어서 못 바꾼다"가 아니라 "설정창에 안 넣었다"이다.</para></summary>
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

        // ============================================================================
        // ★★★ 2026-09-06 — 사용자 신고 «집중모드 시작시 캐릭터다리쪽에 원이 생김.
        //      집중모드 행동을 해야하는데 안함»
        // ============================================================================
        // 원인은 **두 조건이 갈라져 있었던 것**이다. 발밑 타이머 링은
        // <c>Interaction/FocusWatchRenderer.LateUpdate</c>가 <c>IsSessionActive</c> <b>하나만</b> 보고
        // 그렸는데, 정작 캐릭터의 행동(안경+팔짱 포즈)은 <see cref="TryTriggerPoseState"/>의 훨씬 까다로운
        // 관문(Idle/Walk일 것 + SpectacleEventLock이 비어 있을 것)을 통과해야 한다. 관문이 막히면
        // 포즈는 <b>조용히</b> 생략되는데 링은 이미 떠 있다 — 화면에는 "원만 있고 아무 일도 없는" 그림이
        // 남는다. 그게 신고 문장 그대로다.
        //
        // 절대 불변 원칙 1의 정신("대사는 상태 전이가 확정된 뒤 그 상태로부터만 파생")을 링에도 그대로
        // 적용한다: <b>링은 세션 활성화가 아니라 「시작 포즈 전이가 실제로 확정됐다」는 사실에서 파생된다.</b>
        //
        // 그러면서 docs/UX_WIDGETS.md 369행의 계약("위상 전이를 놓쳐도 타이머는 영향받지 않아야 한다")은
        // <b>그대로 지킨다</b> — 아래 어느 필드도 <see cref="RemainingSeconds"/>의 진행에 관여하지 않는다.
        // 관문이 막히면 잠깐 <b>순수 타이머</b>가 될 뿐이고, 관문이 열리는 순간(캐릭터가 Idle/Walk로
        // 돌아오거나 다른 스펙터클이 끝나는 순간) 포즈와 링이 <b>함께</b> 나타난다.

        /// <summary>이번 세션의 시작 포즈(FocusStart)가 <b>실제로 확정</b>됐는가(= 상태머신이 그 상태를
        /// 실제로 들고 있는 것을 확인했는가). 조용한 스킵과 성공을 구분하는 유일한 값이다.</summary>
        public bool IsStartPoseConfirmed { get; private set; }

        /// <summary>
        /// ★ 발밑 타이머 링을 그려도 되는가 — <b>링 가시성 판정의 단일 창구</b>.
        /// 렌더러가 자기 조건을 따로 갖지 않는다(두 벌이 되면 갈라진다. 그게 이번 신고였다).
        ///
        /// <para>세 가지가 동시에 참이어야 한다:</para>
        /// <list type="number">
        ///   <item>세션이 진행 중이다.</item>
        ///   <item>시작 포즈가 확정됐다(= 캐릭터가 실제로 "감시 시작" 행동을 했다).</item>
        ///   <item>그 캐릭터가 화면에 있다. 숨어 있으면(전체화면 자동 숨김/사용자 명시 숨김) 링은 주인
        ///     없는 유령이 된다 — <see cref="HiddenCharacterCommandGate"/>가 "지켜보는 캐릭터가 화면에
        ///     없으면 이 기능은 순수 타이머가 됩니다"라고 이미 못박은 그 판정을, 시작 시점뿐 아니라
        ///     <b>세션 도중에도</b> 같은 축(<see cref="StickmanAgent.IsSuspended"/>)으로 유지한다.
        ///     ★ 이건 원칙 2(비침해)이기도 하다: 예전에는 전체화면 게임이 감지돼 캐릭터가 사라진 뒤에도
        ///     링만 화면에 남아 있었다(몸 렌더러는 꺼지지만 링은 캐릭터의 자식이 아니다).</item>
        /// </list>
        /// </summary>
        public bool IsTimerRingWarranted =>
            IsSessionActive && IsStartPoseConfirmed && !(_player != null && _player.IsSuspended);

        /// <summary>시작 포즈가 관문에 막혔을 때 <b>다시 시도할</b> 남은 시간(초). 0이면 더 시도하지 않는다.</summary>
        private float _startPoseRetryRemaining;

        /// <summary>
        /// 시작 포즈 재시도 창(초). 관문을 막는 사유(낙하/등반/다른 스펙터클 진행 중)는 전부
        /// <b>몇 초 안에 스스로 풀리는</b> 일시적 상태라, 그동안 기다렸다가 열리는 순간 포즈를 낸다.
        ///
        /// <para>무한히 기다리지 않는 이유: 25분 세션의 10분째에 "좋아, 감시 시작"이 튀어나오면 그것이야말로
        /// 원칙 1 위반이다(시작이 아닌 시점에 시작 대사가 나온다). 창이 지나면 이 세션은 링 없는
        /// <b>순수 타이머</b>로 조용히 계속된다 — 팝오버가 남은 시간을 계속 보여주므로 사용자가 정보를
        /// 잃지는 않는다.</para>
        /// </summary>
        private const float StartPoseRetryWindowSeconds = 10f;

        /// <summary>18절 "감시 자체를 끄고 순수 타이머로만 쓰는 옵션".</summary>
        public bool DistractionDetectionEnabled { get; set; } = true;

        public PomodoroSensitivity Sensitivity { get; set; } = PomodoroSensitivity.Normal;

        /// <summary>이번 세션의 총 길이(초). Interaction/FocusWatchRenderer.cs가 타이머 링의 남은 시간
        /// 비율을 계산할 때 <see cref="RemainingSeconds"/>와 짝으로 읽는다 — 렌더러가 분 단위를 다시
        /// 곱하는 식으로 자체 계산하면 15/25/50분 선택값이 어긋나므로 값의 생산자를 한 곳으로 둔다.</summary>
        public float SessionDurationSeconds { get; private set; }

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

        /// <summary>세션 길이의 하한(초) — 원래 코드의 값을 그대로 유지한다(1분 미만을 넘기는 호출자는
        /// 존재하지 않으므로 이 라운드에서 건드릴 이유가 없다). 이름만 상수로 뽑았다.</summary>
        private const float MinimumSessionSeconds = 60f;

        /// <summary>
        /// 집중 모드 데모 토글(Ctrl+Opt+Cmd+F / 우클릭 메뉴). 다른 Director의 ForceTriggerNow가
        /// "확률/쿨다운만 건너뛴다"는 성격인 것과 달리, 포모도로는 애초에 확률이 아니라 <b>유저가
        /// 직접 켜는 기능</b>이라 이 경로가 곧 정식 진입점이다 — 트레이 메뉴가 없는 지금 아키텍처에서
        /// 18절의 "[시작] 트레이 메뉴 '집중 모드'"와 "[종료-중도취소] 트레이에서 '집중 모드 끄기'"를
        /// 하나의 토글로 제공한다.
        ///
        /// 진행 중이면 즉시 정상 종료(패널티 없는 톤 — 18절), 아니면 새 세션을 시작한다. 세션 길이는
        /// 링이 실제로 줄어드는 것을 눈으로 확인할 수 있게 짧게 잡는다(실사용 15/25/50분은
        /// <see cref="StartFocusSession"/>에 그대로 남아 있고 설정창이 생기면 그쪽을 부르면 된다).
        /// 감시 판정 로직/유예 시간/에스컬레이션 임계값은 하나도 건드리지 않는다.
        /// </summary>
        public void ForceTriggerNow(string reason)
        {
            if (_player == null || _config == null)
            {
                Debug.LogWarning($"[포모도로] 집중 모드 토글 실패({reason}) — 플레이어/설정 배선이 없습니다.");
                return;
            }

            if (IsSessionActive)
            {
                Debug.Log($"[포모도로] 집중 모드 끄기({reason}) — 남은 시간 {RemainingSeconds:F0}초에서 중도 취소합니다. " +
                    "패널티 없는 톤으로 종료하고(18절) 타이머 링을 걷습니다.");
                StopFocusSession();
                return;
            }

            // ★★★ 2026-09-03 — <b>시작만</b> 막는다. 바로 위 «이미 세션 중이면 끈다» 분기는
            //   이 줄보다 앞에 있으므로 <b>끄는 길은 숨은 동안에도 열려 있다</b> — 켜 둔 채 숨긴
            //   사용자에게서 정지 수단을 빼앗지 않는다(HiddenCharacterCommandGate "무엇을 막지
            //   않는가"와 같은 판단). 시작 쪽은 캐릭터가 안경+팔짱 포즈로 전이하고 발밑에 링을
            //   그리는 연출이라 보이지 않으면 성립하지 않는다(원칙 1).
            if (HiddenCharacterCommandGate.BlocksNow(_player))
            {
                Debug.Log($"[포모도로] 집중 모드 시작 건너뜀({reason}) — {HiddenCharacterCommandGate.HiddenReason}. " +
                    "지켜보는 캐릭터가 화면에 없으면 이 기능은 순수 타이머가 됩니다(19절/18절 연출 전제).");
                return;
            }

            float demoMinutes = DemoSessionSeconds / 60f;
            StartFocusSession(demoMinutes);
            // 데모 전용 유예 단축. StickConfig.pomodoroGraceSeconds(기본 120초)는 실사용 15~50분
            // 세션을 전제한 값이라 90초짜리 데모에서는 세션 전체를 덮어버려 에스컬레이션 경로를
            // **구조적으로 도달 불가능**하게 만든다(= 3단계 연출을 한 번도 눈으로 볼 수 없다).
            // 설정값 자체는 건드리지 않고 이 데모 세션의 남은 유예만 줄인다.
            _graceRemaining = Mathf.Min(_graceRemaining, DemoGraceSeconds);
            Debug.Log($"[포모도로] 집중 모드 시작({reason}) — 데모 길이 {DemoSessionSeconds:F0}초, " +
                $"유예 {_graceRemaining:F0}초(그동안은 관찰만 하고 경고하지 않는다 — 18절), " +
                $"민감도 {Sensitivity}, 딴짓 감지 {(DistractionDetectionEnabled ? "켬" : "끔")}. " +
                (IsStartPoseConfirmed
                    ? "안경+팔짱 포즈(FocusStart)로 **전이 확정** — 대사가 그 상태에서 파생되고 발밑 타이머 " +
                      "링도 같은 사실에서 파생돼 함께 나타납니다."
                    : "★ 시작 포즈는 아직 확정되지 않았습니다 — 링도 아직 그리지 않습니다(원칙 1). " +
                      "관문이 열리면 둘이 함께 나타납니다."));
        }

        /// <summary>데모 토글이 쓰는 세션 길이(초) — 링이 눈에 띄게 줄어드는 것을 한 자리에서 확인할 수
        /// 있을 만큼 짧게.</summary>
        private const float DemoSessionSeconds = 90f;

        /// <summary>데모 세션에만 적용하는 유예 시간(초). 실사용 값(StickConfig.pomodoroGraceSeconds,
        /// 기본 120초)은 90초 데모를 통째로 덮어버려 경고가 구조적으로 절대 발동하지 않는다.</summary>
        private const float DemoGraceSeconds = 8f;

        /// <summary>트레이 메뉴 "집중 모드" 시작(18절). minutes는 15/25/50 등 유저 선택값.</summary>
        public void StartFocusSession(float minutes)
        {
            if (_player == null || _config == null) return;

            IsSessionActive = true;
            SessionDurationSeconds = Mathf.Max(MinimumSessionSeconds, minutes * 60f);
            RemainingSeconds = SessionDurationSeconds;
            _graceRemaining = Mathf.Max(0f, _config.pomodoroGraceSeconds);

            _hasForegroundHandle = false;
            _switchesInWindow = 0;
            _windowTimer = 0f;
            _hasLastMousePos = false;
            _mouseIdleTimer = 0f;
            _mouseErraticThisWindow = false;
            _consecutiveFlaggedWindows = 0;
            SetTier(FocusWatchTier.None);

            // ★ 링은 이 전이가 **확정된 뒤에만** 뜬다(IsTimerRingWarranted). 못 잡으면 아래 재시도 창이
            //   열리고, 그동안 타이머는 그대로 흐른다(UX_WIDGETS 369행 계약).
            IsStartPoseConfirmed = false;
            _startPoseRetryRemaining = StartPoseRetryWindowSeconds;
            if (TryTriggerPoseState(StickmanStateId.FocusStart))
            {
                IsStartPoseConfirmed = true;
                _startPoseRetryRemaining = 0f;
            }
            else
            {
                Debug.Log($"[포모도로] 시작 포즈를 지금 잡지 못했습니다 — {DescribePoseGate()}. " +
                    $"{StartPoseRetryWindowSeconds:F0}초 동안 다시 시도합니다. 그동안 타이머는 정상적으로 " +
                    "흐르고(위상 전이를 놓쳐도 타이머는 영향받지 않는다 — UX_WIDGETS 18절), " +
                    "발밑 링은 포즈가 확정되는 순간 함께 나타납니다(원칙 1).");
            }
        }

        /// <summary>타이머 링 클릭 또는 트레이 "집중 모드 끄기"(18절 중도 취소, 패널티 없는 톤).</summary>
        public void StopFocusSession()
        {
            if (!IsSessionActive) return;
            PayCancelCoins("중도 취소");
            IsSessionActive = false;
            TryTriggerPoseState(StickmanStateId.FocusCancelled);
        }

        private void CompleteSession()
        {
            PayCompletionCoins();
            IsSessionActive = false;
            TryTriggerPoseState(StickmanStateId.FocusComplete);
        }

        // ====================================================================
        // ★★ 재화 지급 — 이 파일에서 동전이 생기는 자리는 아래 둘뿐이다
        // ====================================================================
        //
        // ★ <b>세션이 끝나는 길은 셋인데 지급은 둘이다.</b> 완주(<see cref="CompleteSession"/>) ·
        //   중도 취소(<see cref="StopFocusSession"/>) · 긴급정지(<see cref="OnEmergencyStop"/>)가 있고,
        //   <b>긴급정지도 사용자 입장에서는 중도 취소</b>다(18절이 그것을 "탈출구"로 정의한다).
        //   그래서 셋 다 아래 두 함수 중 하나를 지난다. ⚠ <b>네 번째 종료 경로를 만들지 마라</b> —
        //   만들면 그 길로 끝낸 사용자만 그날 번 동전을 통째로 잃고, 그 실패는 화면에 아무 흔적도
        //   남기지 않는다(GAME_ARCHITECTURE_REVIEW §3421이 적은 «중도 취소 경로에서만 어긋난다»가
        //   정확히 이 형태다). <c>IsSessionActive = false</c>를 쓰는 자리를 늘리기 전에 여기를 봐라.
        //
        // ★ <b>반드시 <c>IsSessionActive = false</c> 「앞」에서 부른다</b>(DS-5′ 인계 조건). 두 함수 모두
        //   <c>IsSessionActive</c>를 재진입 방지 관문으로 쓰기 때문에, 뒤에서 부르면 <b>조용히 0원</b>이 된다.
        //
        // ★ 산식은 여기 없다 — <c>Core/CurrencyRules.FocusCompletionCoins/FocusCancelCoins</c> 한 곳뿐이고
        //   이 파일은 <b>어느 초를 넘길지</b>만 정한다. 요율(24/20)을 이 파일에 적지 마라.

        /// <summary>
        /// 완주 지급. ★ <b>명목 세션 길이</b>(<see cref="SessionDurationSeconds"/>)를 넘긴다 —
        /// 계측 누적값이 아니다. <see cref="RemainingSeconds"/>는 완주 시점에 <b>0 이하로 넘어간</b>
        /// 값(마지막 프레임의 <c>dt</c>만큼 음수)이라, 그걸로 경과를 재면 세션 길이보다 <b>길게</b>
        /// 나와 프레임률에 따라 지급액이 흔들린다. 명목값은 프레임률과 무관하게 항상 같다.
        /// </summary>
        private void PayCompletionCoins()
        {
            if (!IsSessionActive) return;

            double durationSeconds = SessionDurationSeconds;
            int coins = CurrencyModel.PayFocusCompletionCoins(durationSeconds);
            Debug.Log($"[포모도로][재화] 지급 {coins}동전, 사유=완주, 경과={durationSeconds:F1}초" +
                $"(명목 세션 길이). 잔액 {CurrencyModel.CoinBalance}동전. " +
                "집중 지급은 일일 상한 밖이라(§22-13) 오늘 유휴 상한에 걸려 있어도 전액 지급됩니다.");
        }

        /// <summary>
        /// 중도 취소 지급. 경과는 <c>명목 − 잔여</c>다.
        /// <para>★ <b>0동전일 때도 로그를 남긴다.</b> 1분 미만 취소가 0인 것은
        /// <c>floor(경과/60) = 0</c>에서 저절로 나오는 <b>의도된 결과</b>인데(§22-12 — 그래서 최소 보상
        /// 하한을 넣지 않았다), 아무 기록도 없으면 "지급이 고장났다"는 오진이 올라온다.
        /// 실제로 이 저장소는 같은 형태의 오진을 반복해서 받았다.</para>
        /// </summary>
        private void PayCancelCoins(string reason)
        {
            if (!IsSessionActive) return;

            double elapsedSeconds = SessionDurationSeconds - RemainingSeconds;
            int coins = CurrencyModel.PayFocusCancelCoins(elapsedSeconds);
            Debug.Log($"[포모도로][재화] 지급 {coins}동전, 사유={reason}, 경과={elapsedSeconds:F1}초" +
                $"(완주 {SessionDurationSeconds:F0}초 중 {RemainingSeconds:F0}초 남김). " +
                $"잔액 {CurrencyModel.CoinBalance}동전. " +
                (coins > 0
                    ? "취소는 「분을 먼저 내림」이라 채운 분까지만 지급됩니다(§22-12)."
                    : "★ 1분을 채우지 못해 0동전입니다 — 고장이 아니라 의도된 계단입니다(§22-12). " +
                      "패널티가 아니라 「아직 안 쌓였다」이고, 다음 1분을 채우면 그때부터 붙습니다."));
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
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

            // ★ 시작 포즈 재시도 — **타이머 진행 뒤, 유예 분기 앞**에 둔다. 유예 분기는 return하므로
            //   그 아래에 두면 처음 2분(pomodoroGraceSeconds) 동안 한 번도 돌지 않는다.
            TickStartPoseRetry(dt);

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

        /// <summary>
        /// 시작 포즈가 관문에 막혔을 때, 관문이 열리는 순간 포즈를 낸다(그리고 그때 링이 함께 뜬다).
        ///
        /// <para><b>타이머에는 손대지 않는다</b> — 이 메서드는 <see cref="RemainingSeconds"/>를 읽지도
        /// 쓰지도 않는다. 성공하든 실패하든 세션 시간은 똑같이 흐른다(UX_WIDGETS 18절 계약).</para>
        /// </summary>
        private void TickStartPoseRetry(float dt)
        {
            if (IsStartPoseConfirmed || _startPoseRetryRemaining <= 0f) return;

            _startPoseRetryRemaining -= dt;
            if (TryTriggerPoseState(StickmanStateId.FocusStart))
            {
                IsStartPoseConfirmed = true;
                _startPoseRetryRemaining = 0f;
                Debug.Log($"[포모도로] 시작 포즈를 이제 잡았습니다(남은 시간 {RemainingSeconds:F0}초). " +
                    "안경+팔짱 자세로 전이했고 같은 프레임부터 발밑 타이머 링이 보입니다 — " +
                    "행동과 링이 같은 사실에서 파생됩니다(원칙 1).");
                return;
            }

            if (_startPoseRetryRemaining <= 0f)
            {
                _startPoseRetryRemaining = 0f;
                Debug.Log($"[포모도로] {StartPoseRetryWindowSeconds:F0}초 동안 시작 포즈를 잡지 못했습니다 — " +
                    $"{DescribePoseGate()}. 이번 세션은 **순수 타이머**로 계속합니다(남은 시간은 집중 모드 " +
                    "팝오버가 그대로 보여줍니다). 시작하지도 않은 행동의 링을 발밑에 남기지 않는 것이 " +
                    "원칙 1입니다 — 지금 화면에 원이 없는 것은 정상입니다.");
            }
        }

        /// <summary>지금 포즈 관문을 막고 있는 사유(로그 전용). 조용한 스킵을 **말하게** 만드는 장치다 —
        /// 이 문장이 없어서 "원은 있는데 행동이 없다"의 원인을 로그에서 찾을 수 없었다.</summary>
        private string DescribePoseGate()
        {
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null)
                return "캐릭터 배선이 없습니다";
            StickmanStateId current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
                return $"캐릭터가 Idle/Walk가 아닙니다(지금 {current})";
            if (SpectacleEventLock.IsActive && SpectacleEventLock.CurrentOwner != (object)this)
                return $"다른 연출이 상태 슬롯을 쥐고 있습니다({SpectacleEventLock.ActiveKind})";
            return "관문은 지금 열려 있습니다(직전 프레임에 막혔던 것으로 보입니다)";
        }

        /// <summary>
        /// 포즈 상태로 전이를 시도한다. <b>반환값이 곧 "행동이 실제로 일어났는가"</b>이고, 링/로그는
        /// 이 값에서만 파생된다(2026-09-06). 예전에는 void라 조용한 스킵과 성공이 호출부에서
        /// 구분되지 않았고, 그 구분이 없다는 사실이 이번 신고의 절반이었다.
        ///
        /// <para>성공 판정을 <c>ChangeState</c>를 불렀다는 사실이 아니라 <b>상태머신이 실제로 그 상태를
        /// 들고 있는지</b>로 확인한다 — <c>ChangeState</c>는 미등록 ID면 에러 로그만 남기고 현재 상태를
        /// 유지하고(BUG-M2 방어), <c>Enter()</c>/전이 이벤트 구독자가 같은 프레임에 상태를 다시 바꿀 수도
        /// 있다. "불렀다"가 아니라 "됐다"를 재는 것이 원칙 1의 '확정'이다.</para>
        /// </summary>
        private bool TryTriggerPoseState(StickmanStateId stateId)
        {
            if (_player == null || _player.Blackboard == null || _player.Blackboard.Machine == null) return false;

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) return false; // 조용히 스킵(포즈만 생략, 타이머 로직에는 영향 없음)
            if (SpectacleEventLock.IsActive) return false;
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.FocusPose, this)) return false;

            _player.Blackboard.Machine.ChangeState(stateId);
            if (_player.Blackboard.Machine.CurrentStateId == stateId) return true;

            // 전이가 착지하지 못했다 — 방금 잡은 락을 **그 자리에서** 돌려준다. 여기서 안 놓으면
            // 주인 없는 락이 세션 내내 남아 다른 모든 스펙터클이 조용히 막힌다(재시도 경로가 생기면서
            // 매 프레임 도달 가능해진 자리라 방어가 장식이 아니다).
            SpectacleEventLock.Release(this);
            Debug.LogWarning($"[포모도로] {stateId} 전이가 확정되지 않았습니다(지금 " +
                $"{_player.Blackboard.Machine.CurrentStateId}) — 락을 즉시 반납합니다.");
            return false;
        }

        // 개선 R2(docs/CODE_REVIEW_FINAL.md) 판단: SpectacleEventLock.ReleaseIfOwned 헬퍼로 흡수하지
        // 않는 예외로 남긴다(리뷰어가 직접 지목한 소수 예외 중 하나) — 다른 11곳은 단일 StickmanStateId
        // 하나와 CurrentStateId를 비교하지만, 이 컨트롤러는 4개 상태(FocusStart/FocusComplete/
        // FocusCancelled/FocusNudge) 중 하나인지를 IsFocusPoseState()로 확인해야 해서 단일
        // StickmanStateId 파라미터로 표현할 수 없다(다중값 predicate로 일반화하면 이 한 곳을 위해
        // 헬퍼 시그니처에 delegate 파라미터를 추가하는 셈이라 추상화 비용이 절감분보다 크다는 판단).
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

            // ★ 긴급정지도 사용자 입장에서는 「중도 취소」다 — 18절이 이것을 포모도로의 탈출구로
            //   명시한다. 여기서 지급을 빼면 «탈출구로 나간 사용자만 그때까지 번 동전을 통째로 잃는»
            //   경로가 되고, 그건 패널티를 안 주기로 한 18절 톤과도 정면으로 어긋난다.
            //   ⚠ 포즈/락 처리는 아래 그대로 둔다(FocusCancelled 포즈를 띄우지 않고 즉시 유휴로
            //   보내는 것이 긴급정지의 정의다) — 이 줄은 <b>재화만</b> 얹는다.
            PayCancelCoins("긴급정지");
            IsSessionActive = false;
            ReleaseOwnedLock(forceIdle: true);
        }
    }
}
