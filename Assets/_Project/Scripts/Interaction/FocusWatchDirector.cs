using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 18절 집중 세션(포모도로) 디렉터 — <b>타이머 + 시작/완주/취소 포즈</b>를 전담한다.
    ///
    /// ============================================================================
    /// ★★★ 2026-09-06 사용자 지시 — «집중모드에서 지켜보기 기능 삭제해줘»
    /// ============================================================================
    /// 이 파일에 있던 <b>「딴짓 감지」 + 3단계 에스컬레이션</b>(곁눈질 / 한마디 / 타이머 두드림)이
    /// 통째로 삭제됐다. 함께 사라진 것: 팝오버의 <c>지켜보기</c> 토글과 그것이 쓰던
    /// <c>DistractionDetectionEnabled</c>, 그 임계값을 배율로 조절하던 <c>민감도(관대/보통/예민)</c>,
    /// 전경 창 전환 카운터, 마우스 극단값 신호, 관찰 창/유예 시간, <c>FocusWatchTier</c> 이벤트.
    ///
    /// <para>★ <b>같은 날 두 번째 지시로 「발밑 타이머 링」도 삭제됐다</b> — 사용자가 실기를 보고
    /// 「집중모드시 캐릭터 아래에 녹색링이 생김」을 지적했고, 리더가 확인해 <b>완전 제거</b>로 확정했다.
    /// <c>Interaction/FocusWatchRenderer.cs</c> <b>파일 자체가 사라졌고</b>(트랙 원 + 진행 호가
    /// 그 파일의 전부였다) 이 클래스의 <c>IsTimerRingWarranted</c>도 함께 없앴다.
    /// <b>남은 시간 표시는 팝오버의 60분 절대 다이얼 하나뿐이다</b>(그쪽은 유지 지시).</para>
    ///
    /// <para>★ <b>되살리기 전에 읽어라</b>: 이건 결함 수정이 아니라 <b>사용자가 닫은 문</b>이다.
    /// 「감시가 안 돈다」는 신고가 올라와도 그것은 회귀가 아니다.</para>
    ///
    /// <para>★ <b>같이 지우면 안 되는 것</b> — 세션 중 캐릭터의 «관망 자세»(팔짱 P1 ↔ 뒷짐 P2 + 미세
    /// 생명감 + 제스처 4종)는 <b>이 기능과 무관한 별개 시스템</b>이다. 그쪽 스위치는
    /// <c>StickmanBlackboard.IsFocusSessionAmbientActive</c>(= <c>StickConfig.focusSessionAmbientEnabled</c>
    /// × <see cref="IsSessionActive"/>)이고, 삭제된 토글을 <b>한 번도 읽은 적이 없다</b>.</para>
    ///
    /// <para>★ <c>StickmanStateId.FocusNudge</c>(19)는 <b>enum에 그대로 남는다</b> — 그 정수는 DLC
    /// 매니페스트에 나가는 값이라 지우면 출하된 팩의 배선이 밀린다
    /// (<c>Tests/EditMode/StickmanStateIdWireFormatTests</c>). <c>GlobalKey.K</c>가 격파 놀이 삭제
    /// 뒤에 예약으로만 남은 것과 같은 처리다: <b>진입 경로만 사라졌고 번호는 예약으로 남았다.</b></para>
    ///
    /// SpectacleEventLock: FocusStart/FocusComplete/FocusCancelled 상태가 ChangeState()로 단일 상태
    /// 슬롯을 다투므로 SpectacleEventKind.FocusPose로 참여시킨다(Tasklist.md 교차 레이어 로그에 판단
    /// 근거 기록).
    /// </summary>
    public sealed class FocusWatchDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        /// <summary>같은 GameObject의 성장 디렉터 — 집중 모드 XP(2026-09-07, design-systems §15)를
        /// 지급할 때 부른다. 코인(<c>CurrencyModel.PayFocus*Coins</c>)은 이 파일이 직접 부르지만,
        /// XP는 레벨업 로그·즉시 저장·장비 해금 알림을 <see cref="CharacterProgressionDirector.Grant"/>가
        /// 이미 갖고 있어 그걸 재사용한다 — <c>Assets/Editor/SceneBootstrapper.cs</c>가 둘 다 같은
        /// 루트에 붙이므로(<c>CharacterProgressionDirector</c>가 이미 <c>GetComponent&lt;FocusWatchDirector&gt;()</c>로
        /// 그 반대 방향 참조를 쓰고 있는 것과 같은 관례) 값이 존재한다. null이면 조립 사고이고,
        /// 코인 지급 로그와 똑같이 <b>말하게</b> 만든다(조용한 스킵을 만들지 않는다 — 아래
        /// <see cref="PayCompletionXp"/>/<see cref="PayCancelXp"/>의 경고 로그).</summary>
        private CharacterProgressionDirector _progression;

        public bool IsSessionActive { get; private set; }
        public float RemainingSeconds { get; private set; }

        private void Awake()
        {
            _progression = GetComponent<CharacterProgressionDirector>();
        }

        // ============================================================================
        // ★ 2026-09-06 — 사용자 신고 «집중모드 시작시 캐릭터다리쪽에 원이 생김.
        //   집중모드 행동을 해야하는데 안함» 의 <b>후반부</b>가 여기 남아 있다.
        // ============================================================================
        // 신고는 두 갈래였다. 앞의 「원」은 그날 두 번째 지시로 <b>기능 자체가 삭제</b>됐고
        // (발밑 링 = FocusWatchRenderer.cs, 파일째 제거), 뒤의 「행동을 안 함」이 이 필드다.
        //
        // 캐릭터의 시작 행동(안경+팔짱 포즈)은 <see cref="TryTriggerPoseState"/>의 관문
        // (Idle/Walk일 것 + SpectacleEventLock이 비어 있을 것)을 통과해야 하는데, 막히면 포즈가
        // <b>조용히</b> 생략됐다. 그 조용한 스킵을 <b>말하게</b> 만든 것이 아래 두 멤버다 —
        // 성공/실패를 구분하고, 실패했으면 관문이 열릴 때까지 다시 시도한다.
        //
        // ★ 타이머는 이 값과 무관하다(docs/UX_WIDGETS.md 369행 계약): 포즈를 놓쳐도
        //   <see cref="RemainingSeconds"/>는 정상적으로 흐르고 팝오버가 그 숫자를 계속 보여준다.

        /// <summary>이번 세션의 시작 포즈(FocusStart)가 <b>실제로 확정</b>됐는가(= 상태머신이 그 상태를
        /// 실제로 들고 있는 것을 확인했는가). 조용한 스킵과 성공을 구분하는 유일한 값이다.</summary>
        public bool IsStartPoseConfirmed { get; private set; }

        /// <summary>시작 포즈가 관문에 막혔을 때 <b>다시 시도할</b> 남은 시간(초). 0이면 더 시도하지 않는다.</summary>
        private float _startPoseRetryRemaining;

        /// <summary>
        /// 시작 포즈 재시도 창(초). 관문을 막는 사유(낙하/등반/다른 스펙터클 진행 중)는 전부
        /// <b>몇 초 안에 스스로 풀리는</b> 일시적 상태라, 그동안 기다렸다가 열리는 순간 포즈를 낸다.
        ///
        /// <para>무한히 기다리지 않는 이유: 25분 세션의 10분째에 시작 대사가 튀어나오면 그것이야말로
        /// 원칙 1 위반이다(시작이 아닌 시점에 시작 대사가 나온다). 창이 지나면 이 세션은 시작 연출 없이
        /// 조용히 계속된다 — 팝오버가 남은 시간을 계속 보여주므로 사용자가 정보를 잃지는 않는다.</para>
        /// </summary>
        private const float StartPoseRetryWindowSeconds = 10f;

        /// <summary>이번 세션의 총 길이(초). <c>Interaction/FocusSessionPopover</c>가 다이얼의 남은 시간
        /// 비율을 계산할 때 <see cref="RemainingSeconds"/>와 짝으로 읽는다 — 소비자가 분 단위를 다시
        /// 곱하는 식으로 자체 계산하면 15/25/50분 선택값이 어긋나므로 값의 생산자를 한 곳으로 둔다.</summary>
        public float SessionDurationSeconds { get; private set; }

        // ====================================================================
        // ★ 3구간 (docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md 8절) — 새 타이머는 하나도 안 생긴다
        // ====================================================================
        //
        // 경과 = SessionDurationSeconds − RemainingSeconds. 이 파일이 세션 시간의 **유일한
        // 생산자**라, 구간을 다른 곳에서 세면 두 계산이 반드시 어긋난다. 그래서 구간도 여기서만
        // 판정하고, 경계식 자체는 순수 함수(Core/FocusSessionPhases)에 둬서 EditMode가 잰다.

        /// <summary>
        /// 지금 세션의 구간. <b>세션이 없으면 항상 <see cref="FocusSessionPhase.None"/></b>이다.
        ///
        /// <para>★ <b>이 값은 저장된 상태가 아니라 파생값이다.</b> 그래서 세션이 끝나는 세 경로
        /// (<see cref="CompleteSession"/> / <see cref="StopFocusSession"/> / <see cref="OnEmergencyStop"/>)가
        /// 전부 이미 쓰고 있는 <see cref="IsSessionActive"/> 하나로 <b>자동으로 닫힌다</b> —
        /// 경로마다 따로 초기화할 필드가 없으니 «세 번째 경로를 빠뜨렸다»가 구조적으로 불가능하다
        /// (이 저장소가 재화 지급에서 실제로 겪은 형태라 같은 함정을 두 번 파지 않는다).</para>
        ///
        /// <para>매 프레임 호출돼도 할당이 없다(float 몇 개) — 24시간 상주 앱 규약.</para>
        /// </summary>
        public FocusSessionPhase CurrentPhase => IsSessionActive
            ? FocusSessionPhases.Of(SessionDurationSeconds, SessionDurationSeconds - RemainingSeconds)
            : FocusSessionPhase.None;

        /// <summary>
        /// <see cref="StickmanEventBus.FocusSessionPhaseChanged"/>로 <b>마지막으로 내보낸</b> 구간.
        /// <see cref="StartFocusSession"/>이 매번 <see cref="FocusSessionPhase.Adapt"/>로 다시 잠그므로
        /// 지난 세션의 값이 다음 세션으로 새지 않는다 — <b>이것이 「한 세션에 정확히 2회」의 근거다</b>
        /// (적응기→몰입기, 몰입기→한계. 세션 시작·종료는 「구간 전이」가 아니라 「세션 경계」라 안 쏜다).
        /// </summary>
        private FocusSessionPhase _publishedPhase = FocusSessionPhase.None;

        private void OnEnable()
        {
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
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
        /// 다이얼이 실제로 줄어드는 것을 한 자리에서 확인할 수 있게 짧게 잡는다(실사용 15/25/50분은
        /// <see cref="StartFocusSession"/>에 그대로 남아 있고 설정창이 생기면 그쪽을 부르면 된다).
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
                    "패널티 없는 톤으로 종료합니다(18절).");
                StopFocusSession();
                return;
            }

            // ★★★ 2026-09-03 — <b>시작만</b> 막는다. 바로 위 «이미 세션 중이면 끈다» 분기는
            //   이 줄보다 앞에 있으므로 <b>끄는 길은 숨은 동안에도 열려 있다</b> — 켜 둔 채 숨긴
            //   사용자에게서 정지 수단을 빼앗지 않는다(HiddenCharacterCommandGate "무엇을 막지
            //   않는가"와 같은 판단). 시작 쪽은 캐릭터가 안경+팔짱 포즈로 전이하는 연출이라
            //   보이지 않으면 성립하지 않는다(원칙 1).
            if (HiddenCharacterCommandGate.BlocksNow(_player))
            {
                Debug.Log($"[포모도로] 집중 모드 시작 건너뜀({reason}) — {HiddenCharacterCommandGate.HiddenReason}. " +
                    "지켜보는 캐릭터가 화면에 없으면 이 기능은 순수 타이머가 됩니다(19절/18절 연출 전제).");
                return;
            }

            float demoMinutes = DemoSessionSeconds / 60f;
            StartFocusSession(demoMinutes);
            Debug.Log($"[포모도로] 집중 모드 시작({reason}) — 데모 길이 {DemoSessionSeconds:F0}초. " +
                (IsStartPoseConfirmed
                    ? "안경+팔짱 포즈(FocusStart)로 **전이 확정** — 대사가 그 상태에서 파생됩니다."
                    : "★ 시작 포즈는 아직 확정되지 않았습니다(관문이 막혔습니다) — 열리면 그때 냅니다."));
        }

        /// <summary>데모 토글이 쓰는 세션 길이(초) — 팝오버 다이얼이 눈에 띄게 줄어드는 것을 한 자리에서
        /// 확인할 수 있을 만큼 짧게.</summary>
        private const float DemoSessionSeconds = 90f;

        /// <summary>트레이 메뉴 "집중 모드" 시작(18절). minutes는 15/25/50 등 유저 선택값.</summary>
        public void StartFocusSession(float minutes)
        {
            if (_player == null || _config == null) return;

            IsSessionActive = true;
            SessionDurationSeconds = Mathf.Max(MinimumSessionSeconds, minutes * 60f);
            RemainingSeconds = SessionDurationSeconds;

            // ★ 구간 래치를 여기서 되잠근다. 경과 0초는 언제나 적응기이므로(가장자리 폭은 항상 > 0)
            //   시작에는 이벤트를 쏘지 않고 값만 맞춰 둔다 — 그래야 한 세션의 발행이 정확히 2회다.
            _publishedPhase = FocusSessionPhase.Adapt;

            // ★ 못 잡으면 아래 재시도 창이 열리고, 그동안 타이머는 그대로 흐른다(UX_WIDGETS 369행 계약).
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
                    "흐릅니다(위상 전이를 놓쳐도 타이머는 영향받지 않는다 — UX_WIDGETS 18절).");
            }
        }

        /// <summary>팝오버 [그만두기] 또는 단축키 토글(18절 중도 취소, 패널티 없는 톤).</summary>
        public void StopFocusSession()
        {
            if (!IsSessionActive) return;
            PayCancelCoins("중도 취소");
            PayCancelXp();
            IsSessionActive = false;
            TryTriggerPoseState(StickmanStateId.FocusCancelled);
        }

        private void CompleteSession()
        {
            PayCompletionCoins();
            PayCompletionXp();
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
        // ★★ 2026-09-07 — <b>3구간도 이 세 경로에서 함께 닫힌다</b>. 다만 닫을 필드가 따로 없다:
        //   <see cref="CurrentPhase"/>가 <c>IsSessionActive</c>에서 <b>파생</b>되므로 세 경로 전부가
        //   이미 쓰고 있는 그 한 줄로 자동으로 None이 된다. 위 재화 지급이 «세 번째 경로를 빠뜨렸다»에
        //   당한 자리라, 구간은 <b>애초에 빠뜨릴 필드를 만들지 않는</b> 형태로 넣었다.
        //
        // ★ <b>반드시 <c>IsSessionActive = false</c> 「앞」에서 부른다</b>(DS-5′ 인계 조건). 두 함수 모두
        //   <c>IsSessionActive</c>를 재진입 방지 관문으로 쓰기 때문에, 뒤에서 부르면 <b>조용히 0원</b>이 된다.
        //
        // ★ 산식은 여기 없다 — <c>Core/CurrencyRules.FocusCompletionCoins/FocusCancelCoins</c> 한 곳뿐이고
        //   이 파일은 <b>어느 초를 넘길지</b>만 정한다. 요율(24/20)을 이 파일에 적지 마라.
        //
        // ★★ 2026-09-07 — <b>XP도 같은 세 자리에서 나란히 나간다</b>(design-systems §15,
        //   PayCompletionXp/PayCancelXp, 바로 아래). <b>코인과 완전히 독립적</b>이다 — 코인은
        //   §22-13대로 일일 상한 밖이고, XP는 새 일일 상한(CurrencyRules.FocusXpDailyCap)이 있다.
        //   그래서 두 지급은 서로 다른 카운터(CurrencyModel.TodayGrantedCoins/ArcheryCoinsToday와
        //   FocusXpToday)를 갉고, 한쪽이 상한에 걸려도 다른 쪽은 영향받지 않는다. XP 산식도 여기
        //   없다 — <c>CurrencyRules.FocusCompletionXp/FocusCancelXp</c> 한 곳뿐이고, 상한 클램프는
        //   <c>CurrencyModel</c>이, 레벨 적용(+로그·즉시 저장)은 <c>CharacterProgressionDirector.Grant</c>가
        //   맡는다 — 이 파일은 여전히 <b>어느 초를 넘길지</b>만 정한다.

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

        /// <summary>
        /// 완주 XP — 코인의 <see cref="PayCompletionCoins"/>와 <b>같은 초</b>(명목 세션 길이)를
        /// 넘기지만 <b>완전히 독립적으로</b> 상한 체크된다(design-systems §15-4). 실제 지급/레벨업
        /// 처리는 <see cref="CharacterProgressionDirector.GrantFocusCompletionXp"/>가 맡는다 —
        /// 그 안에서 <c>CurrencyModel.FocusXpToday</c>가 상한에 얼마나 가까운지 판정하고,
        /// 필요한 로그도 그쪽에서 남긴다(같은 사실을 두 파일이 각자 말하지 않는다).
        /// </summary>
        private void PayCompletionXp()
        {
            if (!IsSessionActive) return;
            if (_progression == null)
            {
                Debug.LogWarning("[포모도로] 같은 GameObject에서 CharacterProgressionDirector를 찾지 못해 " +
                    "집중 모드 완주 XP를 지급하지 못했습니다 — 코인은 정상 지급됐습니다.");
                return;
            }
            _progression.GrantFocusCompletionXp(SessionDurationSeconds);
        }

        /// <summary>중도 취소 XP — 코인의 <see cref="PayCancelCoins"/>와 같은 경과(명목 − 잔여)를
        /// 넘긴다. 나머지는 <see cref="PayCompletionXp"/>와 같은 이유로 <see cref="CharacterProgressionDirector"/>에
        /// 위임한다.</summary>
        private void PayCancelXp()
        {
            if (!IsSessionActive) return;
            if (_progression == null)
            {
                // ★ 완주 경로와 <b>독립적으로</b> 경고한다 — 취소로 끝난 세션은 완주 경로를 한 번도
                //   안 지나므로, 여기서 안 찍으면 이 조립 사고가 화면·로그 어디에도 안 남는다.
                Debug.LogWarning("[포모도로] 같은 GameObject에서 CharacterProgressionDirector를 찾지 못해 " +
                    "집중 모드 중도 취소 XP를 지급하지 못했습니다 — 코인은 정상 지급됐습니다.");
                return;
            }

            double elapsedSeconds = SessionDurationSeconds - RemainingSeconds;
            _progression.GrantFocusCancelXp(elapsedSeconds);
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
            if (_player == null || _config == null) return;
            // 18절 예외 상태: 전체화면 게임/영상 감지 중에는 타이머까지 멈춘다(팝오버 상태줄이
            // "일시정지 · 전체화면 앱 사용 중"으로 그 사실을 말한다 — 원칙 1).
            if (_player.IsSuspended) return;
            if (!IsSessionActive) return;

            float dt = Time.deltaTime;
            RemainingSeconds -= dt;
            if (RemainingSeconds <= 0f)
            {
                CompleteSession();
                return;
            }

            // ★ 구간 전이 발행 — 시간을 감산한 **바로 그 자리**다(계약서 8-4절). 감산과 판정 사이에
            //   다른 코드가 끼면 그 프레임의 경과가 두 값으로 갈린다.
            PublishPhaseTransition();

            // ★ 시작 포즈 재시도. 이 아래에는 아무것도 없다 — 「딴짓 감지」가 삭제된 뒤 이 Update가
            //   하는 일은 <b>시간 줄이기 + 구간 전이 발행 + 시작 포즈 재시도</b> 셋뿐이다.
            TickStartPoseRetry(dt);
        }

        /// <summary>
        /// 직전에 발행한 구간과 비교해 <b>바뀔 때만</b> 쏜다. 세션이 도는 동안에만 호출되므로
        /// (<see cref="Update"/>가 <see cref="IsSessionActive"/>에서 이미 걸러낸다)
        /// <see cref="FocusSessionPhase.None"/>이 여기서 나올 수 없다.
        ///
        /// <para><b>구간을 건너뛸 수 없다</b>: 가장 좁은 몰입기는 최단 세션(60초)에서도 12초인데
        /// <c>Time.deltaTime</c>은 <c>Maximum Allowed Timestep</c>(이 프로젝트 설정값 0.33333334초,
        /// <c>ProjectSettings/TimeManager.asset</c>)으로 클램프된다. 즉 한 프레임이 구간 하나를
        /// 통째로 넘길 수 없어 발행 횟수가 프레임률에 흔들리지 않는다.</para>
        ///
        /// <para>할당 0 — 이벤트 인자가 enum 둘이라 박싱이 없다(<c>Action&lt;T1,T2&gt;</c>).</para>
        /// </summary>
        private void PublishPhaseTransition()
        {
            FocusSessionPhase now = CurrentPhase;
            if (now == _publishedPhase) return;

            FocusSessionPhase from = _publishedPhase;
            _publishedPhase = now;
            StickmanEventBus.RaiseFocusSessionPhaseChanged(from, now);

            // 세션당 2줄뿐이라 상주 비용이 없고, 「구간이 안 넘어갔다」는 신고가 오면 이 줄의 유무가
            // 곧 답이다(이 파일이 시작 포즈에서 이미 세운 «조용한 스킵을 말하게 만든다» 관례).
            Debug.Log($"[포모도로][구간] {from} → {now} (경과 {SessionDurationSeconds - RemainingSeconds:F0}초 / " +
                $"세션 {SessionDurationSeconds:F0}초, 가장자리 {FocusSessionPhases.EdgeSeconds(SessionDurationSeconds):F0}초).");
        }

        /// <summary>
        /// 시작 포즈가 관문에 막혔을 때, 관문이 열리는 순간 포즈를 낸다.
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
                Debug.Log($"[포모도로] 시작 포즈를 이제 잡았습니다(남은 시간 {RemainingSeconds:F0}초) — " +
                    "안경+팔짱 자세로 전이했습니다.");
                return;
            }

            if (_startPoseRetryRemaining <= 0f)
            {
                _startPoseRetryRemaining = 0f;
                Debug.Log($"[포모도로] {StartPoseRetryWindowSeconds:F0}초 동안 시작 포즈를 잡지 못했습니다 — " +
                    $"{DescribePoseGate()}. 이번 세션은 시작 연출 없이 계속합니다(남은 시간은 집중 모드 " +
                    "팝오버가 그대로 보여줍니다). 일어나지 않은 행동의 대사를 억지로 띄우지 않는 것이 원칙 1입니다.");
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
        /// 포즈 상태로 전이를 시도한다. <b>반환값이 곧 "행동이 실제로 일어났는가"</b>이고, 로그는
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
        // 하나와 CurrentStateId를 비교하지만, 이 컨트롤러는 여러 상태 중 하나인지를 IsFocusPoseState()로
        // 확인해야 해서 단일 StickmanStateId 파라미터로 표현할 수 없다(다중값 predicate로 일반화하면
        // 이 한 곳을 위해 헬퍼 시그니처에 delegate 파라미터를 추가하는 셈이라 추상화 비용이 절감분보다
        // 크다는 판단).
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

        /// <summary>이 디렉터가 락을 쥔 채 들어갈 수 있는 상태들.
        /// <para>★ <c>FocusNudge</c>는 <b>일부러 남겨 둔다</b> — 2026-09-06에 그 상태로 <b>들어가는
        /// 경로</b>(3단계 에스컬레이션)가 삭제됐지만, 상태 자체는 wire format 예약으로 살아 있고
        /// (클래스 문서 참고) 락 반납은 «혹시 들어와 있다면»에 대한 방어라서 좁힐 이유가 없다.</para></summary>
        private static bool IsFocusPoseState(StickmanStateId id)
            => id == StickmanStateId.FocusStart || id == StickmanStateId.FocusComplete ||
               id == StickmanStateId.FocusCancelled || id == StickmanStateId.FocusNudge;

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (!IsFocusPoseState(evt.From)) return;
            if (evt.To == evt.From) return; // 방어적 — 이 상태들은 self-transition을 쓰지 않지만 다른 Director들과 동일한 관례 유지.
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
            //   보내는 것이 긴급정지의 정의다) — 이 두 줄은 <b>재화·XP만</b> 얹는다.
            PayCancelCoins("긴급정지");
            PayCancelXp();
            IsSessionActive = false;
            ReleaseOwnedLock(forceIdle: true);
        }
    }
}
