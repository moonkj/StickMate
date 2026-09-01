using UnityEngine;
using UnityEngine.Rendering;

namespace StickMate.Platform
{
    /// <summary>
    /// **프레임 페이싱 단일 진입점** — "이 앱이 초당 몇 장을, 어떤 방식으로 화면에 내보낼 것인가"를
    /// 결정하는 곳은 이 파일 하나다(2026-08-31 리더 결정으로 통합).
    ///
    /// 통합 전에는 <c>Platform/Windows/WindowsFramePacing.cs</c>와
    /// <c>Platform/MacOS/MacFramePacing.cs</c>가 따로 있었다. 두 파일은 서로 다른 라운드에서 서로 다른
    /// 증상을 쫓다가 각각 태어났지만(아래 참고), 결국 같은 손잡이(<c>vSyncCount</c> /
    /// <c>targetFrameRate</c>)를 돌리고 있었다. 다음 사람이 "프레임 상한이 몇이지?"를 알아보려고 두
    /// 파일을 각각 찾아다니지 않도록 여기 하나로 모았다.
    ///
    /// ============================================================================
    /// ★ 두 플랫폼이 <b>정반대 처방</b>을 쓴다 — 통합하면서 이 비대칭을 뭉개지 마라
    /// ============================================================================
    ///                     macOS                         Windows
    ///   합성 경로        CAMetalLayer -> WindowServer   레거시 BitBlt -> DWM 레이어드 창
    ///   vsync            <b>켠다</b> (vSyncCount=N)     <b>끈다</b> (vSyncCount=0)
    ///   상한 기구        vSyncCount(위상 고정)          Application.targetFrameRate(sleep)
    ///   왜               CVDisplayLink가 실제 동기화를  레이어드 창은 스캔아웃이 아니라 DWM
    ///                    담당한다(실측 근거 아래)        합성을 거쳐 앱 vsync가 지연만 더한다
    ///
    /// "둘 다 그냥 targetFrameRate 쓰면 되지 않나?"에 대한 답이 이 표의 존재 이유다. 아래 각 절에 각각의
    /// 실측/근거가 있다.
    ///
    /// ============================================================================
    /// macOS — 24시간 상주 앱의 유휴 CPU/배터리 + "부드럽지 않다"는 체감 (2026-08-31 성능 라운드)
    /// ============================================================================
    /// 이 프로젝트에는 프레임 상한이 **전혀 없었다**(런타임 설정 전수 검색 0건). 실행 중인 .app 계측:
    ///
    ///   · 프레임: Player.log의 frame= 카운터로 840초에 89,672프레임 = <b>106.8fps</b> (유휴 상태)
    ///   · CPU  : `top -pid` 실측 평균 약 <b>28%</b> — 캐릭터가 걷기만 하는 유휴 상태에서
    ///   · 메인 스레드 10초(`sample`): 대부분이 렌더 대기 -> 줄일 수 있는 것은 "그리는 횟수"뿐
    ///
    /// <b>왜 targetFrameRate가 아니라 vSyncCount인가(핵심 판단):</b> 사용자 신고는 "수치는 낮은데
    /// 캐릭터가 부드럽지 않고 렉처럼 보인다"였다. 이건 평균 부하가 아니라 <b>프레임 간격의 균일성</b>
    /// 문제다. targetFrameRate는 vsync를 끄고 sleep으로 속도를 맞추므로 앱의 위상이 디스플레이 주기와
    /// 무관하게 떠다닌다 — 120Hz에서 60fps 평균이어도 어떤 프레임은 1회, 어떤 프레임은 2회 표시되는
    /// 맥놀이가 생겨 <b>오히려 더 끊겨 보인다</b>. vSyncCount=N은 위상을 디스플레이에 고정해 간격이
    /// 정확히 균일해진다.
    ///
    /// macOS에서 vsync가 실제로 먹는다는 근거(추측 아님): `sample`에 <c>CVDisplayLink</c> 스레드가
    /// 살아 있고 <c>-[CAMetalLayer nextDrawable]</c>이 <c>semaphore_timedwait</c>에서 실제
    /// back-pressure를 받고 있었다(645샘플 중 461).
    ///
    /// <b>120Hz 패널에서 균일한 값은 약수뿐이다</b>: 120/60/40/30 (= vSyncCount 1/2/3/4).
    /// 45fps 같은 비약수는 120/45=2.67이라 2,3,2,3회 표시가 번갈아 나와 60fps보다 더 끊겨 보인다 —
    /// "45도 시험해 보라"는 접근은 이 하드웨어에서 애초에 나쁜 선택지다.
    ///
    /// <b>실측 A/B (같은 기기, 같은 유휴 상태, 90초 CPU 시간 적산):</b>
    /// <code>
    ///   상한 없음(~107fps)  CPU 약 28%    RSS 541MB
    ///   vSyncCount=2 (60fps) CPU  20.4%    RSS 409MB   p50 16.66ms / p95 17.26 / p99 33.21 / 최대 33.33
    ///   vSyncCount=4 (30fps) CPU   7.8%    RSS 165MB   p50 33.33ms / p95 33.88 / p99 34.22 / 최대 91.79
    /// </code>
    /// 관계가 선형이 아니다 — 60 -> 30으로 절반만 줄였는데 CPU는 <b>2.6배</b> 싸진다. 30fps가 배터리
    /// 관점에서는 압도적으로 유리하다는 뜻이다. 그럼에도 <b>기본값을 60(=vSyncCount 2)으로 둔 이유</b>는
    /// 이번 사용자 신고가 정확히 "부드럽지 않다"였기 때문이다. 30fps는 걷기 애니메이션이 눈에 띄게
    /// 성겨지므로, 그 신고를 받은 라운드에서 기본값으로 내리는 것은 방향이 반대다.
    /// 배터리를 더 원하면 <c>StickConfig.macVSyncInterval</c>을 3(40fps)이나 4(30fps)로 올려라 —
    /// 위 표가 그 대가와 이득을 이미 숫자로 보여준다.
    ///
    /// <b>이것이 "시스템 전체가 느려진다"와 이어지는 지점:</b> 이 앱은 전체화면 투명 오버레이다.
    /// vmmap 실측에서 3024x2020 BGRA 'CAMetalLayer Display Drawable' <b>3장</b>(71.1MB)이 WindowServer와
    /// 공유되고 있다. 이 앱이 한 프레임을 낼 때마다 <b>WindowServer가 화면 전체를 다시 합성</b>한다.
    /// 116fps로 그린다는 것은 <b>OS 컴포지터를 116Hz로 돌린다</b>는 뜻이고, 그 비용은 이 프로세스의
    /// CPU%에 잡히지 않은 채 다른 앱의 반응성을 갉아먹는다 — 사용자가 말한 "앱 수치는 낮은데 시스템이
    /// 느려진다"의 구조적 설명이다.
    ///
    /// ============================================================================
    /// Windows — 레거시 BitBlt 스왑체인 + 레이어드 창의 합성 경합(잔상) (2026-08-31 잔상 라운드)
    /// ============================================================================
    /// 1. 이 앱의 Windows 투명 창은 <b>useFlipModelSwapchain = false</b>를 전제로만 성립한다. 네이티브
    ///    LibUniWinC.dll이 투명화에 <c>DwmExtendFrameIntoClientArea</c>를 쓰는데 DXGI flip-model
    ///    스왑체인이 그 함수와 함께 동작하지 않기 때문이다.
    /// 2. 즉 이 창은 <b>레거시 BitBlt(리디렉션 표면) 경로</b>로만 화면에 올라간다. Present()는 화면을
    ///    직접 넘기지 않고 DWM의 리디렉션 표면에 복사되며, DWM은 자기 합성 주기로 그 표면을 따로 읽어
    ///    레이어드 창으로 합성한다. <b>두 주기 사이에 동기화 계약이 없다.</b>
    /// 3. 앱이 빠르게 그릴수록 DWM이 "아직 다 갱신되지 않은 표면"을 읽을 확률이 올라가고, 그 결과는
    ///    화면 전체가 두 프레임이 섞인 상으로 보이는 것이다 — 사용자 신고("글자 획이 유령처럼 살짝
    ///    어긋나 겹침", "정보창도 왼쪽 아래 호버 패널도 똑같음")와 <b>증상이 표면 종류를 가리지
    ///    않는다</b>는 점이 정확히 이 층위를 가리킨다.
    /// 4. 여기서 vSyncCount를 <b>0으로 내려야</b> 하는 이유: (a) 1 이상이면 targetFrameRate가 통째로
    ///    무시되고(Unity 공식 문서), (b) 레이어드 창은 스캔아웃이 아니라 DWM 합성을 거치므로 앱 쪽
    ///    vsync가 찢어짐을 막아주지 못하고 지연만 한 프레임 더한다.
    ///
    /// <b>★ 이 환경(macOS)에서 검증되지 않은 것 — 정직한 한계:</b> 위 3번의 인과(프레임 상한 -> 잔상
    /// 감소)는 <b>추론이며 실측되지 않았다</b>. 개발 머신이 macOS라 실기 재현이 불가능하다. 다만
    /// (a) 이 변경은 실패해도 macOS/에디터/모바일에 영향이 0이고, (b) 같은 원인을 공유한다고 판단한
    /// 신고 "렉"을 함께 겨냥하며, (c) 아래 로그 한 줄로 실기에서 적용 여부와 실제 주사율이 즉시 확인된다.
    ///
    /// ============================================================================
    /// 프레임레이트를 바꿔도 게임 로직 타이밍은 안 변한다 (2026-08-31 확인 완료)
    /// ============================================================================
    /// 리더 지적("N프레임 동안" 식으로 시간을 재는 곳이 있으면 상한 변경에 타이밍이 딸려 변한다")에
    /// 따라 <c>Time.frameCount</c> 전수 조사를 했다. 결과: 쓰이는 곳이 <b>전부 "같은 프레임인가?"
    /// 동일성 비교</b>이거나 로그/디버그 스냅샷이고, <b>프레임 수로 지속시간을 재는 코드는 0건</b>이다.
    ///   · DialogueBubbleRenderer: <c>_forcedInterruptFrame == Time.frameCount</c> (같은 프레임 판정)
    ///   · StickmanBlackboard   : <c>_groundedTickFrame == Time.frameCount</c> (중복 틱 방지)
    ///   · DialogueIntent.CreatedFrame / StateTransitionContext.ConfirmedFrame: 저장만 하고 산술에
    ///     쓰지 않는 디버그 메타데이터(읽는 곳 0건)
    /// 지속시간은 전부 <c>Time.deltaTime</c>/초 단위다. 즉 상한을 60이든 30이든 바꿔도 대사 노출 시간,
    /// 상태 지속시간, 폴링 주기는 실제 초 단위로 동일하다.
    /// </summary>
    internal static class FramePacing
    {
        private static bool _applied;

        /// <summary>이미 적용됐는지. 호출부가 이 값을 먼저 봐서 <b>인자 계산 자체를</b> 건너뛴다 —
        /// 매 프레임 Update에서 불리는 자리라 24시간 상주 컨벤션상 낭비를 0으로 둔다.</summary>
        internal static bool IsApplied => _applied;

        /// <summary>테스트/재시작 시나리오용 리셋(런타임 경로에서는 호출하지 않는다).</summary>
        internal static void ResetForTests()
        {
            _applied = false;
            _suspendedNow = false;
            _presenceService = null;
            _presence = default;
            _idleDwellSeconds = 0f;
            _presencePollTimer = 0f;
            _currentTier = FramePacingTier.Active;
            _planValid = false;
            _adaptiveEnabled = false;
            _forcedTier = null;
            _transitionCount = 0;
            _transitionCountAtLastSummary = 0;
            _tierChangedAtTime = float.NegativeInfinity;
            _summaryTimer = 0f;
            _firstSummaryDone = false;
            _stillDivisor = FramePacingPolicy.DefaultStillDivisor;
            _presentBaselineValid = false;
            _presentBaselineLoopFrame = 0;
            _presentBaselineRenderedFrame = 0;
            _interactionHoldUntil = float.NegativeInfinity;
            for (int i = 0; i < TierSeconds.Length; i++) TierSeconds[i] = 0f;
            FrameTimeStats.ResetForTests();
            RenderDiagnostics.ResetForTests();
        }

        /// <summary>
        /// 프레임 페이싱을 한 번만 적용한다. 설정이 아직 씬에 없으면(초기 몇 프레임) 아무것도 하지 않고
        /// 다음 프레임에 다시 시도한다 — 하드코딩된 숫자로 대신 지르지 않는다.
        /// </summary>
        internal static void ApplyOnce(Core.StickConfig config)
        {
            if (_applied) return;
            if (config == null) return;
            _applied = true;

            FrameTimeStats.Configure(config);
            RenderDiagnostics.Begin();
            ApplyDisplaySleepPolicy();

#if UNITY_STANDALONE_OSX
            ApplyMacOS(config);
            _presenceService = new MacOS.MacViewerPresenceService();
#elif UNITY_STANDALONE_WIN
            ApplyWindows(config);
            _presenceService = new Windows.WindowsViewerPresenceService();
#endif
            InitializeAdaptiveGovernor();
        }

        /// <summary>
        /// 매 프레임 호출해도 안전 — 통계가 꺼져 있으면 첫 줄에서 돌아간다.
        /// </summary>
        /// <param name="characterIdle">지금 캐릭터가 IDLE 상태인가(= 제자리에 서 있는가).
        /// 호출부(각 플랫폼 Enforcer)가 상태머신에서 읽어 넘긴다. 모르면 false를 넘기면 된다.
        ///
        /// <para><b>★ 2026-09-01부터 이 인자는 <see cref="FramePacingTier.Away"/> 판정에도 들어간다</b>
        /// (그 전에는 <see cref="FramePacingTier.Calm"/>에만 쓰였다 — 무입력만으로 Away를 주면 구경
        /// 중인 사용자 앞에서 걷기가 15fps로 끊겼다. 근거: <see cref="FramePacingPolicy.AwaySeconds"/>
        /// 문서). 그래서 false를 계속 넘기면 <b>Calm뿐 아니라 Away도 성립하지 않는다</b> — 즉 화면이
        /// 꺼지지 않는 한 계속 60fps다. 24시간 상주 앱에서 이 신호가 끊기면 절감이 아니라
        /// <b>비용</b> 쪽으로 실패한다는 뜻이므로, 배선을 지울 때 이 문단을 먼저 읽어라.
        /// (<see cref="FramePacingTier.DisplayOff"/>는 이 인자와 무관하게 그대로 동작한다.)</para></param>
        internal static void Tick(bool characterIdle = false)
        {
            FrameTimeStats.Tick();
            TickAdaptiveGovernor(characterIdle);

            // 등급을 여기서 넘기는 이유: 진단 로그의 A/B 요약이 "이 60초를 어느 등급에서 쟀는가"를
            // 함께 적어야 한다. 등급이 다른 두 실행을 비교하면 MSAA 차이가 아니라 프레임 수 차이를
            // 보게 되므로, 그 무효 조건을 사람이 눈으로 걸러낼 수 있어야 한다.
            RenderDiagnostics.Tick(_currentTier);
        }

        /// <summary>
        /// 전체화면 게임 감지로 <b>캐릭터가 숨겨져 있는 동안</b> 프레임을 더 깊게 조인다
        /// (2026-08-31 성능 라운드). <see cref="Core.StickmanAgent"/>의 Suspend()/Resume()가 부른다.
        ///
        /// <para><b>왜 이게 필요한가</b>: Suspend()는 물리와 렌더러를 끄지만, 앱은 여전히 초당 60번
        /// <b>빈 화면을 그려 OS 컴포지터에 제출</b>한다. 이 앱은 전체화면 투명 오버레이라 프레임을 낼
        /// 때마다 컴포지터가 화면 전체를 다시 합성하므로(vmmap 실측: 3024x2020 드로어블 3장이
        /// WindowServer와 공유됨), 그 비용은 <b>사용자가 전체화면 게임을 하는 바로 그 순간</b>에
        /// 부과된다. 보이지도 않는 것을 위해 게임의 프레임을 갉아먹는 셈이라, 비침해 원칙(CLAUDE.md 2)의
        /// 관점에서도 고쳐야 할 구멍이다.</para>
        ///
        /// <para><b>왜 0fps(완전 정지)가 아닌가</b>: 전체화면 해제를 알아채는 폴링
        /// (<c>StickConfig.fullscreenPollInterval</c>, 기본 1.5초)이 <c>Update()</c>에서 돌기 때문에
        /// 프레임이 완전히 멈추면 영영 깨어나지 못한다. 숨김 등급의 vSyncCount=4
        /// (120Hz에서 30fps, 60Hz에서 15fps)면 폴링 주기 1.5초에 비해 충분히 촘촘해 복귀 지연이 0이면서
        /// 프레임 수는 절반이 된다. 숨겨져 있는 동안에는 부드러움이 아무 의미가 없으므로 여기서는
        /// 위상 균일성보다 절감이 우선이다.</para>
        ///
        /// <para><b>에디터/테스트에서는 아무 일도 하지 않는다</b>: <see cref="_applied"/>가 true일 때만
        /// 동작하는데, 그 값은 <c>ApplyOnce</c>가 실제 플레이어에서 불렸을 때만 켜진다(호출부인
        /// Enforcer들이 <c>UNITY_STANDALONE_* &amp;&amp; !UNITY_EDITOR</c> 경로에서만 생성된다).
        /// 즉 PlayMode 테스트가 Suspend/Resume을 왕복시켜도 <c>QualitySettings</c>를 건드리지 않는다 —
        /// 테스트 타이밍에 영향을 주지 않기 위한 의도적 설계다.</para>
        /// </summary>
        internal static void SetSuspended(bool suspended)
        {
            // 페이싱을 아직(혹은 전혀) 적용하지 않았으면 손대지 않는다 — 에디터/테스트 무영향 보장.
            if (!_applied) return;
            if (_suspendedNow == suspended) return;
            _suspendedNow = suspended;

            // ★ 2026-08-31 2차 성능 라운드부터: 여기서 직접 손잡이를 돌리지 않고 적응형 등급 판단에
            // 사실 하나("지금 숨겨져 있다")를 넘긴 뒤 다시 계산하게 한다. 이전 구현과 결과값은
            // 동일하다(Suspended 등급 -> 나누기 2 -> macOS base vSync 2 x 2 = 4, Windows 60/2 = 30).
            // 손잡이를 돌리는 곳이 두 군데면 서로 마지막 값을 덮어써서 "왜 30fps에서 안 올라오지?" 같은
            // 재현 어려운 버그가 난다 — 그래서 적용 지점을 ApplyPlan() 한 곳으로 못 박는다.
            EvaluateAdaptiveTier(characterIdle: false, force: true);
        }

        // ============================================================================
        // 적응형 프레임 등급(2026-08-31 2차 성능 라운드) — "아무도 안 볼 때는 그리지 않는다"
        // ============================================================================
        //
        // ★ 이 라운드에서 실측으로 확정된 비용 모델(추정 아님):
        //
        //     StickMate 실행 중 : WindowServer 20.2%  +  StickMate 22.4%  = 42.6% (코어 1개 기준)
        //     StickMate 종료 후 : WindowServer  2.2%
        //     ------------------------------------------------------------------------
        //     -> 이 앱이 OS 컴포지터에 부과하는 비용 18.0%p. **앱 자신의 CPU%에는 잡히지 않는다.**
        //        즉 사용자가 보고 있던 "17%"는 실제 시스템 비용의 절반도 안 되는 숫자였다.
        //
        // 같은 표본의 `sample` 프로파일에서 관리 코드(C# 스크립트)는 5,306 표본 중 13개(0.25%)뿐이었고
        // 메인 스레드의 바쁜 구간은 특정 함수에 몰리지 않고 평평하게 흩어져 있었다. **줄일 핫스팟이
        // 없다 — 프레임 한 장의 존재 자체가 비용이다.** 그래서 유일한 절감 수단은 "프레임 수"이고,
        // 부드러움을 해치지 않고 그렇게 할 수 있는 시간대는 **아무도 보고 있지 않은 시간**이다.
        //
        // 등급/기구 선택의 근거는 전부 FramePacingPolicy 클래스 문서에 있다(순수 함수라 테스트도 그쪽을
        // 겨냥한다). 여기 있는 것은 "관측 -> 판단 -> 적용"의 배선과 히스테리시스뿐이다.

        private static bool _suspendedNow;
        private static IViewerPresenceService _presenceService;
        private static ViewerPresenceSnapshot _presence;
        private static float _presencePollTimer;
        private static float _idleDwellSeconds;
        private static FramePacingTier _currentTier = FramePacingTier.Active;
        private static FramePacingPlan _currentPlan;
        private static bool _planValid;
        private static bool _adaptiveEnabled;
        private static FramePacingTier? _forcedTier;

        /// <summary>진단 로그용 — 이번 실행에서 등급이 환경변수로 고정됐는지 사람이 읽을 문자열.
        /// A/B 비교의 유효성 판정에 쓰인다(고정 없이 잰 두 실행은 등급이 달라 비교가 무의미해질 수
        /// 있다). 문자열 보간이 아니라 상수 반환이라 호출 비용이 0이다.</summary>
        internal static string ForcedTierLabel => _forcedTier.HasValue ? _forcedTier.Value.ToString() : "없음";

        // 평소(Active) 값 — 두 플랫폼이 서로 다른 기구를 쓰지만 판단 함수는 둘 다 받는다.
        private static int _baseVSyncCount;
        private static int _baseTargetFrameRate = -1;

        /// <summary>OS 관측 주기(초). 활성 등급에서는 느긋하게, 이미 절감 중일 때는 촘촘하게 —
        /// 절감 중에는 프레임 자체가 싸므로 폴링을 늘려도 되고, 오히려 <b>깨어나는 지연</b>을 줄이는
        /// 것이 중요하다(사용자가 마우스를 움직인 순간 늦게 깨면 그게 곧 렉으로 보인다).</summary>
        private const float PresencePollActiveSeconds = 0.5f;
        private const float PresencePollDormantSeconds = 0.2f;

        /// <summary>캐릭터가 이만큼(초) 계속 IDLE이어야 Calm으로 내려간다. 서 있다가 곧바로 다시
        /// 걷는 짧은 정지에서 등급이 깜빡이지 않게 하는 히스테리시스.
        ///
        /// <para><b>★ 2026-09-01 0.75 -> 0.4로 낮췄다.</b> 이 값은 <b>순수한 낭비</b>다 — 캐릭터가
        /// 이미 서 있는데 아직 60fps로 그리는 시간이다. 자율 배회 실측(Idle 2~6초, 이어붙임 확률
        /// 0.75 -> 평균 연속 정지 약 5.3초 / 걷기 평균 2.75초, 대략 8초 주기)에서 0.75초는 주기의
        /// 9%를 차지했다.</para>
        ///
        /// <para><b>왜 낮춰도 안전해졌는가</b>: 같은 라운드에서 <b>이탈을 즉시로</b> 만들었다
        /// (<see cref="TickAdaptiveGovernor"/>의 "정지 -> 이동 엣지" 처리). 히스테리시스가 막으려던
        /// 것은 "깜빡임"인데, 이제 내려가는 것만 지연되고 올라오는 것은 지연이 없으므로 깜빡여도
        /// <b>보이는 쪽</b>은 항상 60fps다. 남는 비용은 손잡이 대입(int 2개)뿐이라 0으로 취급해도 된다.
        /// 0으로 두지 않은 이유는 1~2프레임짜리 상태 경유(예: Landing -> Idle -> Walk)까지 등급을
        /// 흔들 필요는 없기 때문이다.</para></summary>
        private const float CalmDwellSeconds = 0.4f;

        /// <summary>
        /// 캐릭터가 이만큼(초) 계속 IDLE이면 <see cref="FramePacingTier.Still"/>로 내려간다 —
        /// <b>사용자 입력 여부를 보지 않는</b> 유일한 절감 등급의 유일한 문턱값.
        ///
        /// <para><b>왜 1.6초인가(위/아래 양쪽에서 눌린 값이다)</b>:
        /// <list type="bullet">
        /// <item><b>위</b>: 자율 배회의 Idle 에피소드가 2.0~6.0초다(<c>StickConfig.wanderIdleDurationMin/Max</c>).
        ///   문턱이 2.0초를 넘으면 <b>가장 짧은 에피소드에서는 절대 성립하지 않고</b>, 5초쯤 되면
        ///   Still이 거의 발생하지 않아 이 라운드가 통째로 무의미해진다.</item>
        /// <item><b>아래</b>: 너무 짧으면 걷기 사이의 순간 정지마다 제출률이 1/4로 튀었다가 돌아온다.
        ///   Calm(0.4초)과의 간격이 최소 한 자릿수 프레임은 돼야 등급이 계단으로 읽힌다.</item>
        /// </list>
        /// 실측 주기로 계산한 기대 절감(기본 분주 4): 8.08초 주기당 제출
        /// 60x3.15 + 30x1.2 + 15x3.73 = 281장 -> <b>34.8장/초(-42%)</b>.
        /// 분주 8이면 31.3장/초(-48%). 이 숫자가 실기 로그의 "실효 제출"과 맞아야 한다.</para>
        ///
        /// <para><b>이 값보다 큰 지렛대가 하나 남아 있다(리더 보고 항목)</b>: 위 계산에서 Active가
        /// 차지하는 3.15초 중 2.75초가 <b>걷기</b>다. 즉 남은 제출의 대부분은 "캐릭터가 하루의 약
        /// 34%를 걸어다니기 때문"이며, 그것은 페이싱이 아니라 배회 AI의 듀티 사이클
        /// (<c>wanderPostIdleWalkChance</c> 등) 문제다. 여기서는 손대지 않는다 — UX 결정이다.</para>
        /// </summary>
        internal const float StillDwellSeconds = 1.6f;

        /// <summary>Still 등급의 분주(제출을 몇 분의 1로 줄일지). 기본
        /// <see cref="FramePacingPolicy.DefaultStillDivisor"/>이며 환경변수
        /// <c>STICKMATE_STILL_DIVISOR</c>로 실기에서 재빌드 없이 A/B할 수 있다(4 vs 8).</summary>
        private static int _stillDivisor = FramePacingPolicy.DefaultStillDivisor;

        /// <summary>등급이 <b>더 깊어지는</b> 전이 사이의 최소 간격(초). 얕아지는 방향에는 걸지
        /// 않는다(그쪽을 늦추면 걷기 시작이 끊긴다). 1초인 이유: 실측 유휴 에피소드가 평균 5.3초라
        /// Calm(0.4초)→Still(1.6초) 계단은 그대로 통과하면서, 상태머신이 한 프레임씩 튀는 병적인
        /// 왕복만 흡수한다.</summary>
        // internal인 이유: 테스트가 이 값을 숫자로 베끼면 상수를 바꾼 날 테스트만 조용히 틀린다
        // (CLAUDE.md "테스트에 프로덕션 상수를 숫자로 베끼지 않는다").
        internal const float TierDescendCooldownSeconds = 1f;

        private static float _tierChangedAtTime = float.NegativeInfinity;

        /// <summary>지금 적용 중인 등급(진단/테스트 창구 — 제품 로직은 읽지 않는다).</summary>
        internal static FramePacingTier CurrentTier => _currentTier;

        /// <summary>마지막 OS 관측값(진단용).</summary>
        internal static ViewerPresenceSnapshot LastPresence => _presence;

        /// <summary>누적 등급 전이 횟수(진단용 — <see cref="FrameTimeStats"/>의 스파이크 포렌식이
        /// "이 프레임에 등급이 바뀌었는가"를 알아내는 데 쓴다).</summary>
        internal static int TransitionCount => _transitionCount;

        // ============================================================================
        // UI 상호작용 홀드 (2026-08-31 — 사용자 신고 "기어 설정창조차 클릭하면 약간 렉걸린듯이 움직임")
        // ============================================================================
        //
        // ★ 확정된 인과(코드 검증):
        //   1. 사용자가 정보창을 열고 읽는다 -> 마우스 무입력 2초 경과.
        //   2. 그 사이 캐릭터가 자율 배회의 Idle 구간(실측 2~6초)에 들어간다 -> **Calm 등급**.
        //   3. Windows에서 Calm은 `Application.targetFrameRate`를 60 -> 30으로 나눈다
        //      (baseVSyncCount=0이라 renderFrameInterval이 아니라 targetFrameRate 쪽이 나뉜다).
        //      즉 **게임 루프 자체가 30Hz**가 된다.
        //   4. CharacterInfoWindow의 타이틀바 드래그는 `Update()`마다 OS 커서를 한 번 폴링해
        //      패널 위치를 갱신한다 -> 커서 표본 주기도 30Hz -> 창이 커서를 **계단식으로** 따라온다.
        //   5. 사용자가 다시 마우스를 움직여도 등급 복귀는 다음 관측 폴링(최대 0.2초)에 가서야
        //      일어난다 -> **모든 상호작용의 첫 0.2초가 절반 프레임레이트로 시작**한다.
        //
        // macOS에서는 같은 등급이 renderFrameInterval만 건드리므로 게임 루프는 60Hz 그대로다
        // (= 커서 추적 정확도는 유지되고 표시만 30fps). 사용자가 "윈도우에서는"이라고 말한 것과
        // 플랫폼 비대칭이 정확히 일치한다.
        //
        // 처방은 두 겹이다. **둘 다 필요하다** — 하나만으로는 위 5번(복귀 지연)이 남는다:
        //   (A) 홀드가 걸린 동안 등급 판정에서 Calm을 금지한다(FramePacingPolicy.DecideTier).
        //   (B) 홀드가 **걸리는 그 순간** 폴링 주기를 기다리지 않고 즉시 재평가한다(아래 참고).
        //
        // <b>왜 "창이 열려 있다"가 아니라 "만료 시각"인가</b>: 홀드를 켜고 끄는 두 개의 호출로 만들면
        // 예외/강제 종료 경로 하나만 새도 60fps가 영원히 붙잡힌다(24시간 상주 앱에서 가장 비싼
        // 종류의 누수다). 만료 시각 방식은 호출부가 죽어도 HoldSeconds 뒤에 **저절로** 풀린다 —
        // 해제 책임이 아예 존재하지 않는다.

        /// <summary>한 번의 <see cref="HoldActiveForInteraction"/>이 유지되는 시간(초).
        /// 호출부는 열려 있는 동안 매 프레임 다시 부르면 되고, 호출이 끊기면 이 시간 뒤 자동 만료된다.
        /// 0.5초인 이유: 관측 폴링 최대 간격(0.5초)보다 짧으면 홀드가 폴링 사이에서 깜빡일 수 있다.</summary>
        internal const float InteractionHoldSeconds = 0.5f;

        private static float _interactionHoldUntil = float.NegativeInfinity;

        /// <summary>지금 UI 상호작용 홀드가 유효한가(진단/테스트 창구).</summary>
        internal static bool IsInteractionHeld => Time.unscaledTime < _interactionHoldUntil;

        /// <summary>
        /// "지금 사용자가 이 앱의 UI 표면을 붙잡고 있다"를 알린다 — <b>열려 있는 동안 매 프레임</b>
        /// 부르면 된다(비용: float 비교 1회 + 대입 1회, 할당 0).
        ///
        /// <para>등급이 이미 Active면 아무 일도 하지 않는다. Active가 아니었다면 <b>폴링 주기를
        /// 기다리지 않고 즉시</b> 재평가한다 — 이것이 없으면 창을 여는 첫 0.2초가 여전히 절반
        /// 프레임레이트로 시작한다(위 5번). <c>ApplyPlan</c>의 동등성 가드 덕분에 즉시 재평가가
        /// 손잡이를 실제로 돌리는 것은 등급이 진짜 바뀌는 그 한 프레임뿐이다.</para>
        ///
        /// <para>에디터/테스트에서는 <c>_applied</c>가 false라 손잡이를 건드리지 않는다(이 파일의
        /// 일관된 규약). 홀드 <b>상태 자체</b>는 그래도 기록하므로 EditMode에서 검증할 수 있다.</para>
        /// </summary>
        internal static void HoldActiveForInteraction(float seconds = InteractionHoldSeconds)
        {
            float until = Time.unscaledTime + Mathf.Max(0f, seconds);
            if (until <= _interactionHoldUntil) return;   // 이미 더 긴 홀드가 걸려 있다.
            bool wasHeld = IsInteractionHeld;
            _interactionHoldUntil = until;

            // 홀드가 "새로" 걸렸고 지금 절감 중이라면 폴링을 기다리지 않는다.
            if (wasHeld || _currentTier == FramePacingTier.Active) return;

            // 관측값도 함께 새로 읽는다. 홀드만 갱신하고 낡은 관측을 그대로 쓰면 Away(3분 무입력)에서
            // 돌아오는 경로가 낡은 "181초 무입력"을 근거로 여전히 Away로 판정되어, 정작 이 즉시
            // 재평가가 아무것도 못 고친다. 이 조회는 홀드가 **걸리는 순간에만** 일어나므로(연속
            // 호출은 위에서 걸러진다) 주기 폴링 예산에 영향이 없다.
            if (_presenceService != null && !_presenceService.TryGetPresence(out _presence)) _presence = default;
            _presencePollTimer = 0f;
            EvaluateAdaptiveTier(_idleDwellSeconds >= CalmDwellSeconds, force: false);
        }

        /// <summary>
        /// "지금 캐릭터가 제자리에 서 있는가"를 <b>양 플랫폼이 똑같이</b> 판정하는 한 곳.
        /// 각 Enforcer가 자기 파일에서 따로 판정하면 한쪽만 조건이 바뀌는 사고가 난다
        /// (2026-08-31 오전 가려짐 필터 사고와 같은 부류라 처음부터 공용으로 둔다).
        ///
        /// <para>왜 상태 ID 하나만 보는가: <c>Idle</c>은 이 프로젝트에서 정확히 "제자리에 서 있는"
        /// 상태다(걷기/점프/낙하/랙돌/드래그/미니게임/스펙터클이 전부 별도 상태 ID를 가진다). 속도나
        /// 포즈를 추가로 재면 판정이 더 정확해지는 대신 <b>프레임마다 흔들려</b> 등급이 깜빡인다.
        /// 게다가 이 판정이 틀렸을 때의 최악은 30fps로 그려지는 것뿐이라(정지가 아니다) 정밀도를
        /// 높일 실익이 없다 — FramePacingPolicy 클래스 문서의 "안전 설계" 절 참고.</para>
        /// </summary>
        internal static bool ResolveCharacterIdle(Core.StickmanAgent agent)
        {
            if (agent == null) return false;
            var blackboard = agent.Blackboard;
            if (blackboard == null || blackboard.Machine == null) return false;
            return blackboard.Machine.CurrentStateId == Core.StickmanStateId.Idle;
        }

        private static void InitializeAdaptiveGovernor()
        {
            _adaptiveEnabled = _presenceService != null && ReadEnvFlag("STICKMATE_ADAPTIVE_PACING", true);
            _forcedTier = ReadEnvTier("STICKMATE_FORCE_TIER");
            _stillDivisor = Mathf.Clamp(
                ReadEnvInt("STICKMATE_STILL_DIVISOR", FramePacingPolicy.DefaultStillDivisor),
                FramePacingPolicy.MinStillDivisor, FramePacingPolicy.MaxStillDivisor);
            _baseVSyncCount = QualitySettings.vSyncCount;
            _baseTargetFrameRate = Application.targetFrameRate;
            _currentTier = FramePacingTier.Active;
            _currentPlan = new FramePacingPlan(FramePacingTier.Active, _baseVSyncCount, _baseTargetFrameRate, 1);
            _planValid = true;
            _presencePollTimer = float.MaxValue; // 첫 Tick에서 곧바로 한 번 관측한다.

            if (!_adaptiveEnabled)
            {
                Debug.Log("[FramePacing/적응형] 비활성 — 항상 활성 등급(60fps)으로 동작합니다. " +
                    "(관측 서비스가 없거나 STICKMATE_ADAPTIVE_PACING=0)");
                return;
            }

            Debug.Log($"[FramePacing/적응형] 활성 — 기준 vSyncCount={_baseVSyncCount}, " +
                $"targetFrameRate={_baseTargetFrameRate}. " +
                $"등급: 활성(그대로) / 정적(1/2) / 정지(1/{_stillDivisor}) / 자리비움(1/4) / " +
                $"화면꺼짐({FramePacingPolicy.DisplayOffTargetFps}fps 고정). " +
                $"정지 등급 문턱={StillDwellSeconds:F1}초(캐릭터 정지 지속), 정적 문턱={CalmDwellSeconds:F1}초. " +
                (_forcedTier.HasValue ? $"★ STICKMATE_FORCE_TIER={_forcedTier.Value} 강제 지정됨(계측용). " : "") +
                "근거/실측은 FramePacing·FramePacingPolicy 클래스 문서 참고.");
        }

        private static void TickAdaptiveGovernor(bool characterIdle)
        {
            if (!_applied || !_adaptiveEnabled) return;

            float dt = Time.unscaledDeltaTime;
            bool wasIdle = _idleDwellSeconds > 0f;
            _idleDwellSeconds = characterIdle ? _idleDwellSeconds + dt : 0f;
            AccumulateTierResidency(dt);
            SeedPresentCountersIfNeeded();

            // ============================================================================
            // ★ 정지 -> 이동 엣지: 폴링을 기다리지 않고 **그 프레임에** 올라온다
            // ============================================================================
            // 절감 등급에서 벗어나는 방향은 언제나 안전하므로(더 많이 그릴 뿐이다) OS 관측을 다시
            // 읽지 않고 캐시된 _presence로 즉시 재평가한다. 관측 호출이 없으니 예산에 영향이 0이다.
            //
            // 왜 필요해졌는가: 이 라운드에서 Still(기본 15fps)이 생겼다. 기존처럼 다음 폴링
            // (최대 0.2초)까지 기다리면 **걷기 시작의 첫 0.2초 = 3프레임**이 15fps로 그려진다.
            // 보행 주기 1.35Hz에서 그 3프레임은 한 걸음의 4분의 1이라 "출발할 때 뚝 끊긴다"로
            // 보인다 — 이 프로젝트가 이미 한 번 겪은 신고(AwaySeconds 문서)와 같은 부류다.
            //
            // 반대 방향(Active -> Calm/Still)은 일부러 즉시가 아니다. 그쪽은 늦어도 보이는 것이
            // 없고(더 그릴 뿐), 급하게 내려가면 dwell 히스테리시스의 의미가 사라진다.
            // ★ 조건을 Calm/Still로 **좁힌** 이유(리뷰에서 잡은 함정): "Active가 아니면"으로 두면
            //   DisplayOff(4fps)에서도 캐릭터가 걷기 시작할 때마다 이 분기가 돌고, 여기서 관측
            //   폴링 타이머를 만지면 **화면이 다시 켜진 것을 알아채는 폴링이 계속 뒤로 밀린다**.
            //   이 엣지가 고치려는 것은 "보는 사람 앞에서 걷기가 끊기는 것" 하나뿐이므로, 보는
            //   사람이 있는 절감 등급에서만 돈다. (Away에서 걷기 시작하는 경우도 여기 없다 —
            //   그건 3분 무입력 = 아무도 안 보는 시간이라 0.2초 지연이 보이지 않는다.)
            if (wasIdle && !characterIdle
                && (_currentTier == FramePacingTier.Calm || _currentTier == FramePacingTier.Still))
            {
                // 폴링 타이머는 건드리지 않는다 — 예정된 관측이 이 즉시 재평가 때문에 밀리면
                // 안 된다(위 함정과 같은 부류).
                EvaluateAdaptiveTier(characterIdle: false, force: false);
                return;
            }

            _presencePollTimer += dt;
            float interval = _currentTier == FramePacingTier.Active
                ? PresencePollActiveSeconds
                : PresencePollDormantSeconds;
            if (_presencePollTimer < interval) return;
            _presencePollTimer = 0f;

            if (!_presenceService.TryGetPresence(out _presence)) _presence = default;
            EvaluateAdaptiveTier(_idleDwellSeconds >= CalmDwellSeconds, force: false);
        }

        private static void EvaluateAdaptiveTier(bool characterIdle, bool force)
        {
            if (!_applied) return;
            if (!_adaptiveEnabled)
            {
                // 적응형이 꺼져 있어도 전체화면 숨김 절감은 유지해야 한다(기존 동작 보존).
                if (!force) return;
                ApplyPlan(FramePacingPolicy.BuildPlan(
                    _suspendedNow ? FramePacingTier.Suspended : FramePacingTier.Active,
                    _baseVSyncCount, _baseTargetFrameRate, lowPowerMode: false));
                return;
            }

            // UI 상호작용 홀드는 등급 판정과 저전력 감쇄 **양쪽 모두**에 들어가야 한다.
            // 한쪽만 넣으면 배터리 세이버가 켜진 노트북에서 등급이 Active인데도 손잡이는 여전히
            // 반값인 상태가 남는다(위 InteractionHoldSeconds 근처 주석 + FramePacingPolicy
            // .ShouldApplyLowPowerDownshift 문서 참고).
            bool held = IsInteractionHeld;

            // characterIdle(지금 서 있다)과 characterStill(오래 서 있다)은 같은 사실의 두 임계값이다.
            // 지속 시간을 세는 곳은 이 파일 하나이고, 판단은 순수 함수 한 곳이다.
            bool characterStill = characterIdle && _idleDwellSeconds >= StillDwellSeconds;

            FramePacingTier tier = _forcedTier
                ?? FramePacingPolicy.DecideTier(_presence, _suspendedNow, characterIdle, held, characterStill);
            FramePacingPlan plan = FramePacingPolicy.BuildPlan(tier, _baseVSyncCount, _baseTargetFrameRate,
                FramePacingPolicy.ShouldApplyLowPowerDownshift(_presence, held), _stillDivisor);
            ApplyPlan(plan);
        }

        /// <summary>
        /// 손잡이를 실제로 돌리는 <b>유일한</b> 지점. 값이 그대로면 아무것도 하지 않는다(24시간 상주
        /// 앱에서 같은 값을 매 폴링마다 대입하면 그 자체가 낭비이고, 로그도 더러워진다).
        /// </summary>
        private static void ApplyPlan(in FramePacingPlan plan)
        {
            if (_planValid && plan.SameAs(_currentPlan) && plan.Tier == _currentTier) return;

            // ============================================================================
            // ★ 등급 진동 제동 (2026-09-01 — 사용자 실기 로그에서 Active<->Calm이 4~7초마다 왕복)
            // ============================================================================
            // 캐릭터가 걷다 서다를 반복하는 것은 이 앱의 **정상 동작**(자율 배회)이므로, 그걸 등급에
            // 직접 물리면 왕복은 필연이다. 그래서 처방은 두 겹이고, **순서가 중요하다**:
            //
            //   (A) 왕복을 **싸게** 만든다 — 이게 진짜 수정이다. 보는 사람이 있는 등급이 이제 양
            //       플랫폼 모두 renderFrameInterval만 바꾸므로 **게임 루프 페이스가 변하지 않는다**.
            //       실기 로그의 "루프 평균 25.31ms(39.5fps), p50 16.75ms"는 상당 부분이 Windows
            //       Calm이 targetFrameRate를 30으로 내려 만든 **의도된** 33ms 프레임이었다
            //       (히치가 아니라 설계였고, 그 설계가 틀렸다). 그 진동원이 사라진다.
            //   (B) 그 위에 병적인 고속 왕복만 막는 최소 제동을 건다 — 아래.
            //
            // 제동은 **깊어지는 방향에만** 건다. 얕아지는 방향(더 그리는 쪽)을 늦추면 그게 곧
            // "걷기 시작이 끊긴다"이므로 절대 늦추지 않는다. Suspended/DisplayOff는 관측된 사실이자
            // 비침해 원칙이라 제동 대상이 아니다.
            bool deeper = (int)plan.Tier > (int)_currentTier;
            bool exempt = plan.Tier == FramePacingTier.Suspended
                || plan.Tier == FramePacingTier.DisplayOff
                || _forcedTier.HasValue;
            if (deeper && !exempt && Time.unscaledTime - _tierChangedAtTime < TierDescendCooldownSeconds)
            {
                return;
            }
            _tierChangedAtTime = Time.unscaledTime;

            FramePacingTier before = _currentTier;
            _currentPlan = plan;
            _currentTier = plan.Tier;
            _planValid = true;

            if (QualitySettings.vSyncCount != plan.VSyncCount) QualitySettings.vSyncCount = plan.VSyncCount;
            if (Application.targetFrameRate != plan.TargetFrameRate) Application.targetFrameRate = plan.TargetFrameRate;
            if (OnDemandRendering.renderFrameInterval != plan.RenderFrameInterval)
            {
                OnDemandRendering.renderFrameInterval = plan.RenderFrameInterval;
            }

            _transitionCount++;
            if (_transitionCount <= VerboseTransitionLogLimit)
            {
                Debug.Log($"[FramePacing/적응형] 등급 {before} -> {plan.Tier} " +
                    $"({FramePacingPolicy.DescribeTier(plan.Tier)}) — " +
                    $"vSyncCount={plan.VSyncCount}, targetFrameRate={plan.TargetFrameRate}, " +
                    $"renderFrameInterval={plan.RenderFrameInterval}. 관측: {_presence}, " +
                    $"UI홀드={(IsInteractionHeld ? "걸림" : "없음")}." +
                    (_transitionCount == VerboseTransitionLogLimit
                        ? " (이후 전이는 개별로 남기지 않고 5분마다 요약합니다 — 아래 주석 참고.)"
                        : string.Empty));
            }
        }

        // ============================================================================
        // 등급 전이 로그를 요약으로 바꾸는 이유 (24시간 상주 앱의 로그도 자원이다)
        // ============================================================================
        // 실측: 자율 배회가 Idle 2~6초 / Walk 1.5~4초를 반복하므로 Active <-> Calm 전이가
        // **분당 약 10회** 일어난다. 전이마다 한 줄씩 남기면 하루 약 14,000줄이고, 그때마다 문자열
        // 보간 할당이 생긴다. 이 프로젝트가 Update() 할당을 41B까지 따지는 마당에 로그로 그것을
        // 되돌릴 수는 없다. 그래서 **처음 몇 번만 자세히 남기고**(동작 확인용) 이후에는 5분마다
        // 등급별 체류 시간 비율을 한 줄로 요약한다. 요약은 사용자 기기에서 "실제로 어느 등급에
        // 얼마나 머무는가"를 알려주는 유일한 원격 계측 수단이기도 하다.
        private const int VerboseTransitionLogLimit = 6;
        private const float TierSummaryIntervalSeconds = 300f;

        /// <summary>★ <b>첫</b> 요약만 60초에 낸다. 사용자가 "고쳤다"를 확인하는 데 5분을 기다리게
        /// 하지 않기 위해서다(2026-09-01 리더 요청: "사용자가 몇 분 안에 검증할 수 있는 절차").
        /// 이후에는 <see cref="TierSummaryIntervalSeconds"/> 주기로 돌아간다 — 24시간 상주 앱에서
        /// 주기 로그를 늘리지 않는다는 원칙은 그대로다.</summary>
        private const float FirstTierSummarySeconds = 60f;

        private static int _transitionCount;
        private static int _transitionCountAtLastSummary;
        private static float _summaryTimer;
        private static bool _firstSummaryDone;
        private static readonly float[] TierSeconds = new float[6];

        // ============================================================================
        // ★ 실효 제출 계측 (2026-09-01 컴포지터 라운드) — "정말 덜 내보내고 있는가"
        // ============================================================================
        // 이 라운드의 절감은 전부 renderFrameInterval 하나에 걸려 있다. 그런데 그 API가 어떤
        // 플랫폼/파이프라인에서 **조용히 무시될** 가능성이 있고(이 프로젝트는 Screen.msaaSamples가
        // 거짓말을 하는 것을 이미 두 번 겪었다 — RenderDiagnostics 클래스 문서), 그러면 사용자
        // 기기에서 "고쳤는데 그대로"가 된다. 그래서 설정값이 아니라 **결과**를 센다:
        //
        //   루프fps  = Time.frameCount 증가분 / 경과   -> 입력/로직이 도는 속도
        //   렌더fps  = Time.renderedFrameCount 증가분 / 경과 -> 실제로 그려 **제출한** 장수
        //
        // 두 값이 같으면 renderFrameInterval이 걸리지 않은 것이다(= 이 라운드의 절감이 0). 다르면
        // 그 비율이 곧 컴포지터에 부과하는 비용의 비율이다(macOS 실측에서 제출 횟수 비례가
        // 확정됐다: ACTIVE-OFF=+12.09%p, AWAY-OFF=+3.06%p, 비율 0.25 = 코드상 제출비와 일치).
        //
        // 비용: 프레임당 int 비교 1회, 5분에 한 번 뺄셈 두 번. 할당 0.
        //
        // ★ 2026-09-01 정정 — 위 두 계기만으로는 못 가른다. Time.renderedFrameCount가 스킵을 반영하지
        //   않을 가능성(가설 H3)이 남기 때문이다. 그래서 **실측 렌더 콜백**(Camera.onPostRender로 센
        //   프레임 수)을 세 번째 계기로 함께 찍는다 — 근거와 판정표는 RenderDiagnostics의
        //   "계기 정직성 라운드" 문단에 있다.
        private static bool _presentBaselineValid;
        private static int _presentBaselineLoopFrame;
        private static int _presentBaselineRenderedFrame;
        private static int _presentBaselineActualRenderFrame;

        private static void SeedPresentCountersIfNeeded()
        {
            if (_presentBaselineValid) return;
            _presentBaselineValid = true;
            _presentBaselineLoopFrame = Time.frameCount;
            _presentBaselineRenderedFrame = Time.renderedFrameCount;
            _presentBaselineActualRenderFrame = RenderDiagnostics.ActualRenderedFrameCount;
        }

        private static void AccumulateTierResidency(float dt)
        {
            int i = (int)_currentTier;
            if (i >= 0 && i < TierSeconds.Length) TierSeconds[i] += dt;

            _summaryTimer += dt;
            float due = _firstSummaryDone ? TierSummaryIntervalSeconds : FirstTierSummarySeconds;
            if (_summaryTimer < due) return;
            _summaryTimer = 0f;
            _firstSummaryDone = true;

            float total = 0f;
            for (int k = 0; k < TierSeconds.Length; k++) total += TierSeconds[k];
            if (total <= 0f) return;

            float loopFps = (Time.frameCount - _presentBaselineLoopFrame) / total;
            float renderFps = (Time.renderedFrameCount - _presentBaselineRenderedFrame) / total;
            float actualFps = (RenderDiagnostics.ActualRenderedFrameCount - _presentBaselineActualRenderFrame) / total;
            // 실측 콜백이 0이면 "0장 제출"이 아니라 **측정 불가**다(위 A/B 요약과 같은 규칙).
            bool actualAvailable = actualFps > 0.01f || loopFps <= 1f;
            float submittedFps = actualAvailable ? actualFps : renderFps;
            _presentBaselineLoopFrame = Time.frameCount;
            _presentBaselineRenderedFrame = Time.renderedFrameCount;
            _presentBaselineActualRenderFrame = RenderDiagnostics.ActualRenderedFrameCount;

            // ★ 이 라운드가 줄이는 것은 **ms/프레임이 아니라 초당 장수**다. 그래서 둘의 곱
            //   (= 초당 GPU 점유)을 같이 찍는다 — 작업 관리자의 GPU %와 대응하는 유일한 숫자이며,
            //   ms만 보고 "안 줄었다"고 결론내는 함정을 로그 차원에서 막는다.
            string gpu;
            if (RenderDiagnostics.TryDrainGpuLoad(out float gpuMeanMs, out float gpuWorstMs, out int gpuN))
            {
                gpu = $"GPU {gpuMeanMs:F2}ms/프레임(최악 {gpuWorstMs:F2}, 표본 {gpuN}) " +
                      $"x {submittedFps:F1}장/초 = ★GPU 점유 추정 {gpuMeanMs * submittedFps / 10f:F1}%";
            }
            else
            {
                gpu = RenderDiagnostics.GpuTimingAvailable
                    ? "GPU: 드라이버가 타이머 질의를 돌려주지 않음(표본 0)"
                    : "GPU: 측정 불가(enableFrameTimingStats 꺼짐)";
            }

            Debug.Log($"[FramePacing/적응형] 최근 {total:F0}초 — " +
                $"★ 실효 제출 {submittedFps:F1}장/초({(actualAvailable ? "실측 렌더 콜백" : "renderedFrameCount — 실측 콜백 측정 불가")}) " +
                $"[계기 대조: 루프 {loopFps:F1}Hz / renderedFrameCount {renderFps:F1} / " +
                $"실측 콜백 {(actualAvailable ? $"{actualFps:F1}" : "측정 불가")}], " +
                $"정지등급 분주 {_stillDivisor}. " +
                $"{gpu}. " +
                $"등급 체류: 활성 {TierSeconds[0] / total * 100f:F0}% / 정적 {TierSeconds[1] / total * 100f:F0}% / " +
                $"정지 {TierSeconds[2] / total * 100f:F0}% / " +
                $"자리비움 {TierSeconds[3] / total * 100f:F0}% / 전체화면숨김 {TierSeconds[4] / total * 100f:F0}% / " +
                $"화면꺼짐 {TierSeconds[5] / total * 100f:F0}%, " +
                $"전이 {_transitionCount - _transitionCountAtLastSummary}회(이 구간) / {_transitionCount}회(누적). " +
                "(활성 비율이 100%에 가까우면 절감이 전혀 안 되고 있다는 뜻이다. " +
                "실효 제출이 루프와 같으면 renderFrameInterval이 먹지 않은 것이다 — 그 경우 이 라운드의 " +
                "절감은 0이므로 리더에게 보고할 것. " +
                "★ '실측 콜백'과 'renderedFrameCount'가 다르면 후자가 거짓말하는 것이다 — " +
                "그 값으로 낸 과거 판정은 다시 재야 한다.)");

            _transitionCountAtLastSummary = _transitionCount;
            for (int k = 0; k < TierSeconds.Length; k++) TierSeconds[k] = 0f;
        }

        /// <summary>
        /// ★ 이 라운드 최대 발견 — <b>이 앱이 사용자의 디스플레이를 24시간 잠들지 못하게 막고 있었다.</b>
        ///
        /// <para>실측(<c>pmset -g assertions</c>, 수정 전 실행 중인 빌드):
        /// <code>
        ///   pid 36382(StickMate): [0x...] 00:16:02 PreventUserIdleDisplaySleep named: "disable screen saver"
        /// </code>
        /// 이 문자열은 <c>UnityPlayer.dylib</c> 안에 있다(<c>strings</c>로 확인) — <b>프로젝트 코드가
        /// 만든 것이 아니라 Unity 플레이어의 기본 동작</b>이다. Unity는 <c>Screen.sleepTimeout</c>의
        /// 기본값이 <c>NeverSleep</c>이라 시작할 때 IOPM 어서션을 걸고 앱이 살아 있는 동안 유지한다.
        /// 게임에서는 합리적인 기본값이지만(컨트롤러만 잡고 컷신을 보는 동안 화면이 꺼지면 안 된다),
        /// <b>24시간 상주하는 바탕화면 장식 앱</b>에서는 정반대다 — 이 앱을 켜 두면 사용자가 자리를
        /// 비워도 화면이 영영 꺼지지 않고 화면보호기도 뜨지 않는다.</para>
        ///
        /// <para><b>비용 규모가 지금까지 쫓던 것과 자릿수가 다르다</b>: 노트북 패널이 밤새 8시간 더
        /// 켜져 있는 것은 수십 Wh 단위이고, 이번 라운드에서 다투던 CPU 절감분은 W 단위다. 게다가 이건
        /// "성능"이 아니라 <b>사용자 환경을 앱이 마음대로 바꾸는 것</b>이라 CLAUDE.md 원칙 2(비침해)에
        /// 정면으로 걸린다.</para>
        ///
        /// <para><b>고친 뒤에도 화면이 꺼지면 캐릭터가 멈추지 않는가?</b> — 오히려 그게 목적이다.
        /// 위 적응형 등급의 <see cref="FramePacingTier.DisplayOff"/>가 짝을 이룬다: 화면이 꺼지면
        /// 4fps로 내려가고, 켜지면 0.25초 안에 복귀한다. "화면을 꺼도 되게 만들고, 꺼져 있는 동안은
        /// 그리지 않는다" 두 개가 한 쌍이다.</para>
        /// </summary>
        /// <remarks><b>private가 아니라 internal인 이유</b>: 이 한 줄(원칙 2 위반 수정)을 누가 지워도
        /// 전체 스위트가 초록불이었다(R5 Major 2). <c>ApplyOnce</c>는 에디터/테스트에서 절대 실행되지
        /// 않도록 의도적으로 설계돼 있어(호출부 Enforcer가 <c>UNITY_STANDALONE_* &amp;&amp; !UNITY_EDITOR</c>
        /// 에서만 생성됨) 이 메서드만 따로 부를 수 있어야 회귀가 잠긴다.
        /// <c>DisplaySleepPolicyTests</c>가 직접 호출해 검증한다.</remarks>
        internal static void ApplyDisplaySleepPolicy()
        {
            int before = Screen.sleepTimeout;
            if (before == SleepTimeout.SystemSetting)
            {
                Debug.Log("[FramePacing] 디스플레이 슬립 정책 — 이미 시스템 설정을 따르고 있습니다.");
                return;
            }

            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Debug.Log($"[FramePacing] 디스플레이 슬립 정책 — sleepTimeout {before} -> " +
                $"{Screen.sleepTimeout} (SystemSetting). " +
                "Unity 기본값(NeverSleep)은 플레이어가 'disable screen saver' IOPM 어서션을 걸어 " +
                "사용자의 화면이 영영 꺼지지 않게 만든다. 24시간 상주 앱에서는 비침해 원칙 위반이라 해제한다. " +
                "확인 방법: macOS에서 `pmset -g assertions | grep StickMate` 가 비어 있어야 한다.");
        }

        private static bool ReadEnvFlag(string name, bool fallback)
        {
            try
            {
                string v = System.Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrEmpty(v)) return fallback;
                return v != "0" && !v.Equals("false", System.StringComparison.OrdinalIgnoreCase);
            }
            catch { return fallback; }
        }

        /// <summary>정수 환경변수 읽기(실기 A/B용). 값이 없거나 숫자가 아니면 기본값 그대로.</summary>
        private static int ReadEnvInt(string name, int fallback)
        {
            try
            {
                string v = System.Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrEmpty(v)) return fallback;
                return int.TryParse(v, out int parsed) ? parsed : fallback;
            }
            catch { return fallback; }
        }

        /// <summary>계측 전용 — 특정 등급을 강제해 그 등급의 실제 CPU 비용을 재는 데 쓴다.
        /// 환경변수를 지정하지 않으면 아무 효과가 없다(제품 동작에 영향 0).</summary>
        private static FramePacingTier? ReadEnvTier(string name)
        {
            try
            {
                string v = System.Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrEmpty(v)) return null;
                if (System.Enum.TryParse(v, ignoreCase: true, out FramePacingTier tier)) return tier;
            }
            catch { }
            return null;
        }

#if UNITY_STANDALONE_OSX
        private static void ApplyMacOS(Core.StickConfig config)
        {
            int vSyncBefore = QualitySettings.vSyncCount;
            int targetBefore = Application.targetFrameRate;
            int interval = Mathf.Clamp(config.macVSyncInterval, 0, 4);

            if (interval > 0)
            {
                // 주 경로: 디스플레이 주기에 위상을 고정한다. targetFrameRate는 vSyncCount가 1 이상이면
                // 어차피 무시되므로, 두 기구가 싸우는 것처럼 보이지 않게 명시적으로 해제해 둔다.
                QualitySettings.vSyncCount = interval;
                Application.targetFrameRate = -1;
            }
            else if (config.macTargetFrameRate > 0)
            {
                // 보조 경로: vsync가 예상대로 동작하지 않는 환경용(외장 모니터 등). 이때는 vSyncCount를
                // 반드시 0으로 내려야 한다 — 1 이상이면 Unity가 targetFrameRate를 통째로 무시한다.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = config.macTargetFrameRate;
            }
            else
            {
                Debug.Log("[FramePacing/macOS] 프레임 페이싱 비활성" +
                    "(macVSyncInterval=0, macTargetFrameRate<=0) — 주사율 그대로 유지합니다.");
                return;
            }

            double hz = Screen.currentResolution.refreshRateRatio.value;
            double expectedFps = interval > 0 && hz > 0.0 ? hz / interval : Application.targetFrameRate;

            Debug.Log($"[FramePacing/macOS] 적용 — vSyncCount {vSyncBefore} -> {QualitySettings.vSyncCount}, " +
                $"targetFrameRate {targetBefore} -> {Application.targetFrameRate}. " +
                $"디스플레이 {hz:F1}Hz -> 기대 {expectedFps:F1}fps. " +
                $"Screen=({Screen.width}x{Screen.height}), 그래픽API={SystemInfo.graphicsDeviceType}, " +
                $"MSAA 실측={Screen.msaaSamples}x. " +
                "(상주 앱의 유휴 CPU/배터리 + OS 컴포지터 부하 절감. 실측 A/B는 FramePacing 클래스 문서 참고.)");
        }
#endif

#if UNITY_STANDALONE_WIN
        private static void ApplyWindows(Core.StickConfig config)
        {
            int cap = config.windowsTargetFrameRate;
            if (cap <= 0)
            {
                Debug.Log("[FramePacing/Windows] 프레임 상한 비활성(windowsTargetFrameRate <= 0) — " +
                    "주사율 그대로 유지합니다.");
                return;
            }

            int vSyncBefore = QualitySettings.vSyncCount;
            int targetBefore = Application.targetFrameRate;

            if (config.windowsDisableVSyncForFrameCap && QualitySettings.vSyncCount != 0)
            {
                QualitySettings.vSyncCount = 0;
            }
            Application.targetFrameRate = cap;

            Debug.Log($"[FramePacing/Windows] 적용 — targetFrameRate {targetBefore} -> " +
                $"{Application.targetFrameRate}, vSyncCount {vSyncBefore} -> {QualitySettings.vSyncCount}. " +
                $"디스플레이 주사율={Screen.currentResolution.refreshRateRatio.value:F1}Hz, " +
                $"해상도={Screen.currentResolution.width}x{Screen.currentResolution.height}, " +
                $"Screen=({Screen.width}x{Screen.height}), " +
                $"그래픽API={SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}), " +
                $"MSAA 실측={Screen.msaaSamples}x. " +
                "(레거시 BitBlt 스왑체인 + 레이어드 창의 합성 경합을 줄이기 위한 조치 — " +
                "잔상이 남는다면 이 값을 더 낮춰 재확인해 주세요.)");
        }
#endif
    }

    /// <summary>
    /// 프레임 시간 분포(p50/p95/p99/최댓값)를 주기적으로 로그에 남긴다. <b>할당 0</b>(고정 크기 링 버퍼).
    ///
    /// <para>왜 평균이 아니라 분위수인가: "평균 CPU는 낮은데 렉이 느껴진다"는 신고는 평균으로는 절대
    /// 재현되지 않는다. 사람이 렉으로 인지하는 것은 <b>가끔 튀는 긴 프레임</b>이다. p99와 최댓값이
    /// 기대 프레임 시간의 2배를 넘는지가 판별 기준이다(예: 60fps 목표에서 기대 16.7ms인데 최댓값이
    /// 33ms면 그 순간 한 프레임을 통째로 흘린 것이다).</para>
    ///
    /// <para>플랫폼 분기가 없다 — 프레임 시간은 어느 플랫폼에서든 같은 방식으로 재는 것이 맞고,
    /// 특히 실기 검증이 불가능한 Windows 잔상/렉 조사에서 이 로그가 유일한 원격 계측 수단이 된다.</para>
    /// </summary>
    internal static class FrameTimeStats
    {
        private const int SampleCapacity = 512;
        private const float ReportIntervalSeconds = 30f;

        private static readonly float[] Samples = new float[SampleCapacity];
        private static readonly float[] SortScratch = new float[SampleCapacity];
        private static int _sampleCount;
        private static int _sampleHead;
        private static float _reportTimer;
        private static bool _logEnabled;
        private static bool _sampling;

        internal static void ResetForTests()
        {
            _sampleCount = 0;
            _sampleHead = 0;
            _reportTimer = 0f;
            _logEnabled = false;
            _sampling = false;
            _spikeBackoff.Reset();
            _spikeCount = 0;
            _spikeTiers.Reset();
            _spikeRate.Reset();
            _lastGc0 = 0;
            _lastGc1 = 0;
            _spikeLastWidth = 0;
            _spikeLastHeight = 0;
            _lastTransitionCountSeen = 0;
            _lastTierSeen = FramePacingTier.Active;
        }

        internal static void Configure(Core.StickConfig config)
        {
            // ★ 2026-09-01: 로그를 꺼도 **표본 수집은 계속한다.** <see cref="RenderDiagnostics"/>의
            // A/B 요약 한 줄이 이 링 버퍼를 그대로 재사용하기 때문이다(같은 버퍼를 두 벌 만들지
            // 않는다). 수집 비용은 프레임당 float 대입 1회 + 덧셈 2회, 할당 0이라 24시간 상주
            // 컨벤션에 걸리지 않는다. 바뀐 것은 "언제 Debug.Log를 부르는가"뿐이다.
            _logEnabled = config != null && config.logFrameTimeStats;
            _sampling = true;
        }

        internal static void Tick()
        {
            if (!_sampling) return;

            float dtMs = Time.unscaledDeltaTime * 1000f;
            Samples[_sampleHead] = dtMs;
            _sampleHead = (_sampleHead + 1) % SampleCapacity;
            if (_sampleCount < SampleCapacity) _sampleCount++;

            WatchSpike(dtMs);

            _reportTimer += Time.unscaledDeltaTime;
            if (!_logEnabled) return;
            if (_reportTimer < ReportIntervalSeconds || _sampleCount < MinimumSamples) return;
            _reportTimer = 0f;

            if (!TrySummarize(out FrameTimeSummary s)) return;

            // ★ 이 값은 **게임 루프 주기**이지 화면에 실제로 나가는 주기가 아니다.
            //   renderFrameInterval이 2 이상이면 루프는 60Hz인데 프레젠트는 30fps일 수 있다 —
            //   그 차이를 모르면 "60fps인데 왜 반만 부드럽지?"라고 잘못 진단하게 되므로 함께 찍는다.
            int interval = Mathf.Max(1, OnDemandRendering.renderFrameInterval);
            float loopFps = s.MeanMs > 0f ? 1000f / s.MeanMs : 0f;

            Debug.Log($"[프레임시간] 표본 {s.SampleCount}개 — 루프 평균 {s.MeanMs:F2}ms({loopFps:F1}fps) " +
                $"p50 {s.P50Ms:F2}ms / p95 {s.P95Ms:F2}ms / p99 {s.P99Ms:F2}ms / 최대 {s.MaxMs:F2}ms. " +
                $"vSyncCount={QualitySettings.vSyncCount}, targetFrameRate={Application.targetFrameRate}, " +
                $"renderFrameInterval={interval} -> 실제 프레젠트 약 {loopFps / interval:F1}fps" +
                (interval > 1 ? " (적응형 절감 중)" : "") + ". " +
                "(최대가 p50의 2배를 크게 넘으면 그 순간이 사용자가 '렉'으로 느끼는 지점이다.)");
        }

        private const int MinimumSamples = 16;

        // ============================================================================
        // ★ 스파이크 포렌식 (2026-09-01 — 사용자 실기 로그: p99 150ms / 최대 407ms)
        // ============================================================================
        // 분위수 요약은 "튄다"는 사실만 말하고 **왜** 튀는지는 말하지 않는다. 407ms짜리 멈춤은
        // 분포 문제가 아니라 **단일 사건**이므로, 그 순간의 정황을 그때 찍지 않으면 영영 못 잡는다.
        //
        // 여기서 확인하는 용의자 3종(전부 프레임당 비용 0으로 잴 수 있는 것들만 골랐다):
        //
        //   (1) ★ 백버퍼 재생성 — 최우선 용의자. 같은 로그에서 창 폭이 재적용마다 1px씩 줄고
        //       있었다(3840 -> 3839 -> ... -> 3831). 그 줄어듦은 곧 Screen.SetResolution 재호출이고,
        //       D3D11에서 3840x2160 스왑체인 + 리디렉션 표면을 해제/재생성하는 것은 수백 ms짜리
        //       스톨이다. 이 프로젝트는 이미 그 인과를 알고 있었다 —
        //       Platform/DisplayTopologyWatcher.cs 클래스 문서: "그 중간 상태마다
        //       Screen.SetResolution을 부르면 백버퍼 재할당이 연달아 일어나 사용자가 (히치를 본다)".
        //       스파이크 순간에 Screen.width/height를 읽어 직전 값과 비교하면 **인과가 바로 갈린다.**
        //   (2) GC — GC.CollectionCount(0/1) 증가분. 24시간 상주 앱에서 힙이 커지면 실제로 후보다.
        //   (3) 페이싱 손잡이 전환 — 등급 전이가 그 프레임에 있었는가(전이 카운터 증가분).
        //
        // <b>비용</b>: 평상시 프레임당 float 비교 1회 + int 2회(GC 카운터는 런타임 카운터 읽기).
        // Screen 조회는 **스파이크가 났을 때만** 한다. 로그는 쿨다운으로 폭주를 막는다.

        /// <summary>이보다 긴 프레임을 "스파이크"로 본다(절대 하한). 사용자 실기 p99가 150ms이므로
        /// 100ms면 그 꼬리를 잡으면서 평상시에는 절대 걸리지 않는다.</summary>
        private const float SpikeAbsoluteMs = 100f;

        /// <summary>그리고 <b>동시에</b> 기대 프레임 시간의 이 배수를 넘어야 한다. 절감 등급
        /// (Away 15fps / DisplayOff 4fps)에서는 250ms 프레임이 <b>정상</b>이므로, 절대값만 보면
        /// 밤새 "스파이크" 로그가 쌓인다.</summary>
        private const float SpikeRelativeFactor = 2.5f;

        // (예전의 고정 SpikeLogCooldownSeconds = 5f는 2026-09-02 R2-1에서 적응형 백오프로 바뀌었다.
        //  최소/최대 간격은 SpikeLogBackoff.MinSeconds / MaxSeconds가 갖는다.)

        /// <summary>
        /// **발생률 창**(초). 누적 단조증가 숫자는 24시간 상주 앱에서 언제나 "망가졌다"로 읽힌다 —
        /// "누적 4,222회"는 그게 3분에 쌓였는지 20시간에 쌓였는지 말해 주지 않는다.
        ///
        /// <para>★ 2026-09-02 R2-5 — 이 창은 <b>슬라이딩</b>이다. 예전에는 텀블링(통째로 굴리기)
        /// 이었는데, 그러면 실제 발생률이 일정한데도 로그의 숫자가 <b>창이 자라는 동안 단조 상승</b>
        /// 하다가 굴리는 순간 <b>2.4배 점프</b>했다(실측 53.3 -> 129.4). 두 줄을 나란히 읽는 사람은
        /// 그걸 "갑자기 나빠졌다"로 읽는다 — 계기가 사건이 아니라 자기 구조를 보고한 셈이다.
        /// 버킷을 하나씩 은퇴시키면 한 번에 바뀌는 몫이 최대 1/<see cref="RateBucketCount"/>이라
        /// 값이 실제 발생률을 <b>따라간다</b>.</para>
        /// </summary>
        /// (수치와 규칙은 <see cref="SpikeRateWindow"/>가 갖는다 — 두 플랫폼 공용.)

        // ★ 2026-09-02 R2-1 — 고정 5초 쿨다운을 적응형 백오프로 바꿨다. 가려진 452초 동안
        //   이 줄과 [스톨귀인]이 전체 로그 바이트의 70.7%를 먹었다(230 B/s). 규칙은
        //   Platform/SpikeLogBackoff.cs에 있고 StallAttribution도 같은 것을 쓴다.
        private static SpikeLogBackoff _spikeBackoff;

        private static int _spikeCount;

        // ★ 등급 축 분해 — 이것 하나로 "조인 구간의 정상적인 긴 프레임"과 "진짜 히치"가 갈린다.
        //   규칙과 소급 재분류는 Platform/SpikeTierLedger.cs에 있다(두 플랫폼 공용).
        private static SpikeTierLedger _spikeTiers;

        // ★ 2026-09-02 R2-5 — 발생률 창을 텀블링에서 **슬라이딩**으로 바꿨다. 텀블링은
        //   (a) 창이 자라는 동안 값이 단조 상승만 하고 (b) 창이 굴리는 순간 2.4배 점프해서,
        //   두 줄을 나란히 읽는 사람이 "갑자기 나빠졌다"로 오독했다(실측 53.3 -> 129.4).
        private static SpikeRateWindow _spikeRate;

        private static int _lastGc0;
        private static int _lastGc1;
        private static int _spikeLastWidth;
        private static int _spikeLastHeight;
        private static int _lastTransitionCountSeen;
        private static FramePacingTier _lastTierSeen = FramePacingTier.Active;

        /// <summary>
        /// 최근 프레임 주기의 **중앙값**(관측). 스파이크 문턱의 기준선으로 쓴다.
        /// 정렬 비용이 있으므로 <b>스파이크 후보일 때만</b>, 그것도 이 주기로만 다시 잰다.
        /// </summary>
        private const float BaselineRefreshSeconds = 2f;

        private static float _baselineMs;
        private static float _baselineAgeSeconds;

        internal static float RecentMedianMs()
        {
            if (_baselineMs > 0f && _baselineAgeSeconds < BaselineRefreshSeconds) return _baselineMs;
            if (!TrySummarize(out FrameTimeSummary s)) return 0f;
            _baselineMs = s.P50Ms;
            _baselineAgeSeconds = 0f;
            return _baselineMs;
        }

        /// <summary>
        /// ============================================================================
        /// ★ [프레임스파이크] 읽는 법 (이 설명은 <b>로그 줄에 싣지 않는다</b> — 2026-09-02)
        /// ============================================================================
        /// <list type="number">
        /// <item><b>먼저 볼 것은 누적이 아니다.</b> <c>실사용등급</c> 수와 <c>분당 N회</c>를 본다.
        ///   절감등급(Away/Suspended/DisplayOff)의 긴 프레임은 <b>설계된 절감</b>이지 히치가 아니다 —
        ///   그 둘을 한 칸에 합쳐 놓았기 때문에 예전 로그가 언제나 "4,222회 망가짐"으로 읽혔다.</item>
        /// <item>백버퍼가 바뀌었으면 원인은 <c>Screen.SetResolution</c> 재호출이다(창 크기 재적용 루프).</item>
        /// <item>GC 증가분이 0이고 백버퍼도 그대로면 <c>[스톨귀인]</c>/<c>[스톨구간]</c> 줄을 본다 —
        ///   같은 프레임#으로 짝이 맞춰져 있고, 그쪽이 "어디서 시간이 갔는가"를 계측으로 답한다.</item>
        /// </list>
        /// 로그는 <see cref="SpikeLogBackoff"/>가 억제하지만(같은 등급이 이어지면 5초에서 최대 60초까지
        /// 간격을 벌린다) <b>카운트는 전부</b> 센다 — 억제 앞에서 증가시키는 것이 의도다.
        /// </summary>
        private static void WatchSpike(float dtMs)
        {
            float dt = Time.unscaledDeltaTime;
            _spikeBackoff.Tick(dt);
            _baselineAgeSeconds += dt;

            // 발생률 슬라이딩 창 — 벽시계 조회 없이 이미 읽은 dt만 누산한다(규칙은 SpikeRateWindow).
            _spikeRate.Tick(dt);

            int gc0 = System.GC.CollectionCount(0);
            int gc1 = System.GC.CollectionCount(1);
            int gc0Delta = gc0 - _lastGc0;
            int gc1Delta = gc1 - _lastGc1;
            _lastGc0 = gc0;
            _lastGc1 = gc1;

            int transitions = FramePacing.TransitionCount;
            int transitionDelta = transitions - _lastTransitionCountSeen;
            _lastTransitionCountSeen = transitions;

            // ★ R2-2 — 전환 시각 부기는 **스파이크가 아닌 프레임에서도** 굴러야 소급 재분류가 된다.
            //   ★★ 2026-09-02 실측 정정: "모든 등급 전환"이 아니라 **절감 경계를 넘는 전환**만
            //   유예 사유다. Active↔Calm↔Still 미세 전환은 캐릭터가 서고 걷기만 해도 수 초마다
            //   일어나서, 그것까지 유예로 치면 3초 창이 타임라인을 덮어 **진짜 히치를 전환 칸으로
            //   삼킨다**(실측: 유도한 192ms/434ms 히치가 둘 다 전환 칸으로 갔다).
            FramePacingTier tierNow = FramePacing.CurrentTier;
            bool crossed = SpikeTierLedger.CrossesThrottleBoundary(_lastTierSeen, tierNow);
            _lastTierSeen = tierNow;
            _spikeTiers.Tick(dt, crossed);

            if (dtMs < SpikeAbsoluteMs) return;

            // 기대 프레임 시간 — 절감 등급의 "긴 프레임"은 스파이크가 아니다.
            // ★ 계산은 StallAttribution 한 곳에만 있다. 두 로그가 같은 프레임#으로 1:1 짝을 이루려면
            //   분모가 같아야 하는데, 값을 복사해 두면 한쪽만 고쳤을 때 조용히 어긋난다.
            float expectedMs = StallAttribution.ExpectedFrameMs();

            // ★★ R2-1/R2-6 — 계획값만으로는 "가려진 앱을 OS가 조인" 상황을 절대 못 맞춘다
            //    (실측: 계획 16.7ms인데 실제 p50 105ms). 관측된 중앙값을 기준선에 함께 넣는다.
            float medianMs = RecentMedianMs();
            float thresholdMs = StallAttribution.SpikeThresholdMs(expectedMs, medianMs);
            if (dtMs < thresholdMs) return;

            // ★ 카운트는 억제 **앞**에 있는 것이 옳다 — 전부 세고 일부만 찍는 의도된 설계다.
            //   뒤로 옮기면 누적값이 실제 발생의 일부만 세게 된다.
            FramePacingTier tier = tierNow;
            SpikeTierLedger.SpikeClass spikeClass = _spikeTiers.Classify(tier);
            _spikeCount++;
            _spikeTiers.Count(tier);
            _spikeRate.Count1();

            // 적응형 백오프 — 억제를 뚫는 것은 **실사용 히치**뿐이다(등급 변화만으로 뚫으면
            // 전체화면 왕복에서 억제가 무력화된다 — SpikeLogBackoff 문서의 실측 근거 참고).
            if (!_spikeBackoff.ShouldLog((int)spikeClass,
                    spikeClass == SpikeTierLedger.SpikeClass.Actionable)) return;

            // ★ 여기서만 Screen을 읽는다(네이티브 조회라 평상시에는 아깝다).
            int w = Screen.width;
            int h = Screen.height;
            bool backbufferChanged = _spikeLastWidth != 0 && (w != _spikeLastWidth || h != _spikeLastHeight);
            string sizeNote = _spikeLastWidth == 0
                ? $"{w}x{h}(첫 관측)"
                : backbufferChanged
                    ? $"★{_spikeLastWidth}x{_spikeLastHeight} -> {w}x{h} — 백버퍼가 바뀌었다(스왑체인 재생성 유력)"
                    : $"{w}x{h}(변화 없음)";
            _spikeLastWidth = w;
            _spikeLastHeight = h;

            // 발생률 — 슬라이딩 창. 창이 짧으면 그 사실을 숫자 옆에 적는다.
            string rateNote = _spikeRate.SpanTooShort ? "(관측 짧음)" : string.Empty;

            Debug.LogWarning($"[프레임스파이크] {dtMs:F0}ms 멈춤 " +
                // ★ 문턱은 계획값과 **관측 중앙값** 중 큰 쪽에서 나온다. 둘을 함께 찍지 않으면
                //   "왜 이건 잡히고 저건 안 잡히나"를 로그만 보고는 영영 알 수 없다.
                $"(계획 {expectedMs:F1}ms / 관측 p50 {medianMs:F1}ms -> 문턱 {thresholdMs:F0}ms) — " +
                $"누적 {_spikeCount}회 = {_spikeTiers}, " +
                $"최근 {_spikeRate.SpanSeconds:F0}초에 {_spikeRate.Count}회 = " +
                $"분당 {_spikeRate.PerMinute:F1}회{rateNote}. " +
                $"백버퍼: {sizeNote}. " +
                $"GC: gen0 +{gc0Delta} / gen1 +{gc1Delta}. " +
                $"페이싱: 등급={tier}, 이번 프레임 전이 {transitionDelta}회, " +
                $"vSyncCount={QualitySettings.vSyncCount}, targetFrameRate={Application.targetFrameRate}, " +
                $"renderFrameInterval={OnDemandRendering.renderFrameInterval}. " +
                $"프레임#{Time.frameCount}. 다음 억제 {_spikeBackoff.CurrentIntervalSeconds:F0}초.");
        }

        /// <summary>
        /// 링 버퍼의 <b>현재 내용</b>(최근 최대 <see cref="SampleCapacity"/>프레임 = 60fps에서 약 8.5초)을
        /// 분위수로 요약한다. <b>할당 0</b>(미리 잡아둔 <see cref="SortScratch"/>에 복사해 정렬).
        ///
        /// <para>표본이 <see cref="MinimumSamples"/>개 미만이면 false를 돌려주고 아무것도 채우지 않는다 —
        /// 시작 직후 몇 프레임의 스파이크(셰이더 컴파일/창 부착)가 "최댓값"으로 굳어 A/B 비교를
        /// 오염시키는 것을 막는다.</para>
        ///
        /// <para><b>이 요약의 창(window)은 "최근 8.5초"다.</b> 실행 전체의 최악 프레임을 알고 싶으면
        /// 이 값이 아니라 <see cref="RenderDiagnostics"/>가 워밍업 구간 전체에 걸쳐 따로 누적하는
        /// 최댓값을 봐야 한다 — 두 숫자는 의도적으로 다른 것을 재고 있고, 로그에도 그렇게 적힌다.</para>
        /// </summary>
        internal static bool TrySummarize(out FrameTimeSummary summary)
        {
            summary = default;
            int n = _sampleCount;
            if (n < MinimumSamples) return false;

            System.Array.Copy(Samples, SortScratch, n);
            System.Array.Sort(SortScratch, 0, n);

            float sum = 0f;
            for (int i = 0; i < n; i++) sum += SortScratch[i];

            summary.SampleCount = n;
            summary.MeanMs = sum / n;
            summary.P50Ms = SortScratch[n / 2];
            summary.P95Ms = SortScratch[Mathf.Min(n - 1, Mathf.RoundToInt(n * 0.95f))];
            summary.P99Ms = SortScratch[Mathf.Min(n - 1, Mathf.RoundToInt(n * 0.99f))];
            summary.MaxMs = SortScratch[n - 1];
            return true;
        }
    }

    /// <summary>프레임 시간 분위수 요약 한 벌(구조체 — 힙 할당 0).</summary>
    internal struct FrameTimeSummary
    {
        internal int SampleCount;
        internal float MeanMs;
        internal float P50Ms;
        internal float P95Ms;
        internal float P99Ms;
        internal float MaxMs;
    }

    /// <summary>
    /// **렌더 비용 진단 로그** — "이 실행에서 실제로 무엇을, 얼마나 비싸게 그리고 있는가"를
    /// 사용자가 Player.log 몇 줄만 보고 알 수 있게 한다(2026-09-01 "윈도우만 렉" 라운드).
    ///
    /// <para><b>왜 필요한가.</b> 이 앱의 Windows 렌더 비용은 <b>우리 프로세스의 CPU%에 잡히지 않는다</b>
    /// (레거시 BitBlt 경로의 복사는 dwm.exe와 GPU 복사 엔진에 계상된다 — <see cref="FramePacing"/> 클래스
    /// 문서의 Windows 절). 그래서 작업 관리자로는 원인이 보이지 않고, 개발 머신이 macOS라 실기 프로파일러도
    /// 붙일 수 없다. 남은 유일한 원격 계측 수단이 <b>앱이 스스로 찍는 로그</b>다.</para>
    ///
    /// <para><b>세 줄만 남긴다(24시간 상주 앱이라 주기 로그를 늘리지 않는다).</b>
    /// <list type="number">
    ///   <item>콜드스타트 스냅샷 — 창 부착이 끝난 뒤 <b>1회</b>. 구성(그래픽 API/백버퍼/MSAA/페이싱).</item>
    ///   <item>A/B 요약 — 워밍업 <see cref="WarmupSeconds"/>초 뒤 <b>1회</b>. 이 한 줄이 A/B 비교 단위다.</item>
    ///   <item>백버퍼 변경 — 모니터 전환/해상도 변경 등 <b>실제로 바뀔 때만</b>. 안 바뀌면 영원히 안 찍는다.</item>
    /// </list>
    /// 요약을 낸 뒤에는 <see cref="Tick"/>이 <b>2초에 한 번 int 두 개를 비교</b>하는 것 외에 아무 일도 하지
    /// 않는다(GPU 타이밍 수집도 그때 멈춘다). 매 프레임 로그·매 프레임 할당은 0이다.</para>
    ///
    /// ============================================================================
    /// ★ MSAA를 A/B 할 때의 함정 — 이 클래스가 존재하는 진짜 이유
    /// ============================================================================
    /// <para><c>Screen.msaaSamples</c>는 <b>백버퍼의 진실을 말하지 않는다.</b> 이 프로젝트는 같은 함정에
    /// 두 번 빠졌다: (a) Apple GPU가 8x 요청을 조용히 4x로 낮췄는데도 8을 그대로 보고했고(커밋 39ab690),
    /// (b) 런타임에 4 -> 0으로 바꾸면 즉시 0을 보고하지만 그래픽 메모리는 1바이트도 움직이지 않았다
    /// (<see cref="RenderQualityTuner"/>의 "닫힌 길" 주석, 2026-08-31 6쌍 실측).</para>
    ///
    /// <para>그래서 이 로그는 <c>Screen.msaaSamples</c>를 <b>참고값으로만</b> 찍고, 신뢰할 수 있는 지표
    /// <b>두 개</b>를 대신 내놓는다:</para>
    /// <list type="number">
    ///   <item><b>적용 시점의 사실</b> — <see cref="RenderQualityTuner.MutatedAfterStartup"/>.
    ///     MSAA가 <c>BeforeSceneLoad</c>에서만 정해졌는가(= 백버퍼에 반영될 수 있는 유일한 시점)를
    ///     주석이 아니라 <b>런타임 플래그</b>로 확인한다. true면 그 실행의 MSAA 비교는 무효다.</item>
    ///   <item><b>행동으로 드러난 비용</b> — <c>FrameTimingManager</c>의 <b>GPU 프레임 시간</b>.
    ///     MSAA가 실제로 걸렸고 실제로 비싸면 여기가 갈린다. 안 갈리면 안 걸렸거나 공짜인 것이다.
    ///     API가 뭐라고 보고하든 이 숫자가 최종 심판이다.</item>
    /// </list>
    ///
    /// <para><b>왜 GPU 시간이 프레임 시간보다 결정적인가.</b> 이 앱은 60fps 상한이 걸려 있다. GPU가
    /// 4x에서 9ms, 0x에서 3ms를 쓰더라도 <b>둘 다 60fps를 채우므로 CPU 프레임 시간은 똑같이 16.7ms다.</b>
    /// 즉 프레임 시간만 보면 "차이 없음"이라는 잘못된 결론이 나온다. 그런데 그 6ms는 사라진 게 아니라
    /// <b>사용자의 다른 앱이 쓸 수 있었던 GPU 시간</b>이고, 그게 정확히 신고 "앱 수치는 낮은데 시스템이
    /// 느려짐"의 정체다. GPU 프레임 시간은 상한에 가려지지 않고 그 비용을 그대로 보여준다.</para>
    ///
    /// <para><b>전제</b>: <c>PlayerSettings.enableFrameTimingStats</c>가 켜져 있어야 GPU 시간이 나온다
    /// (<c>Assets/Editor/BuildStandalone.cs</c>의 <c>ConfigureRenderDiagnostics()</c>가 빌드 때 켠다).
    /// 꺼져 있거나 드라이버가 타이머 질의를 지원하지 않으면 <c>FrameTimingManager.IsFeatureEnabled()</c>가
    /// false를 주고, 그때는 <b>"측정 불가"라고 적는다</b> — 0을 진짜 값인 척 찍지 않는다.</para>
    /// </summary>
    internal static class RenderDiagnostics
    {
        /// <summary>창 부착(전체 데스크톱 확장)이 끝나기를 기다리는 시간. 이보다 일찍 찍으면 백버퍼가
        /// 아직 기본 창 크기여서 "렌더 타깃 해상도" 줄이 통째로 거짓이 된다. 값이 틀려도 백버퍼가
        /// 나중에 바뀌면 3번 로그가 자동으로 정정하므로 여기서 크게 잡을 이유는 없다.</summary>
        private const float SnapshotDelaySeconds = 5f;

        /// <summary>A/B 요약을 내기까지의 워밍업. 짧을수록 테스트 루프가 짧아지고(콜드스타트 2회 =
        /// 약 2분 30초) 길수록 표본이 안정된다. 60초는 셰이더 워밍업·창 부착·첫 GC가 모두 지난 뒤이면서
        /// 사용자가 한 번의 A/B를 앉은자리에서 끝낼 수 있는 지점이다.</summary>
        private const float WarmupSeconds = 60f;

        /// <summary>요약 후 백버퍼 변화를 확인하는 주기. 매 프레임 <c>Screen.width</c>를 읽는 것은
        /// 네이티브 호출이라 24시간 상주 앱에서는 그것조차 아깝다.</summary>
        private const float BackbufferWatchSeconds = 2f;

        // GetLatestTimings는 배열을 받아 채운다 — 미리 한 번만 잡아 재사용한다(프레임당 할당 0).
        private static readonly FrameTiming[] TimingScratch = new FrameTiming[1];

        private static bool _snapshotLogged;
        private static bool _summaryLogged;
        private static float _elapsed;
        private static float _watchTimer;
        private static int _lastWidth;
        private static int _lastHeight;

        // 워밍업 구간 전체(= 60초)에 걸친 누적. FrameTimeStats의 링 버퍼(최근 8.5초)와 달리
        // "이번 실행에서 가장 나빴던 프레임"을 놓치지 않는다.
        private static double _cpuSumMs;
        private static double _cpuWorstMs;
        private static int _cpuCount;
        private static double _gpuSumMs;
        private static double _gpuWorstMs;
        private static int _gpuCount;
        private static bool _timingFeatureAvailable;

        // ★ 실효 제출 계측 — FramePacing의 5분 요약과 **같은 지표를 같은 방식으로** 잰다
        //   (지표를 두 벌 만들면 두 로그가 서로 다른 숫자를 말할 때 누가 맞는지 알 수 없다).
        //   차이는 창의 길이뿐이다: 여기는 A/B 한 판(약 55초), 저기는 상시 5분.
        private static int _measureBaselineLoopFrame;
        private static int _measureBaselineRenderedFrame;
        private static int _measureBaselineActualRenderFrame;
        private static float _measureStartElapsed;

        // ============================================================================
        // ★ 상시 GPU 표본 (2026-09-01) — "절감이 ms를 실제로 줄였는가"를 계속 볼 수 있어야 한다
        // ============================================================================
        // A/B 요약은 시작 후 60초에 **한 번** 찍고 끝난다. 그런데 이 라운드의 절감은 캐릭터가 서
        // 있는 구간에서만 일어나므로, 한 번의 60초 창에 절감 구간이 얼마나 들어갔는지가 매번 다르다.
        // 그래서 표본을 계속 모아 FramePacing의 주기 요약이 함께 찍게 한다.
        //
        // ★ 핵심 — **GPU ms/프레임은 이 라운드로 줄어들지 않는다.** 한 장을 그리는 비용은 그대로이고
        //   줄어드는 것은 **장수**다. 작업 관리자의 GPU %에 대응하는 값은
        //       GPU 점유 = (ms/프레임) x (제출/초) / 10   [%]
        //   이고, 이 곱만이 이번 절감을 반영한다. ms만 보고 "안 줄었다"고 결론내는 함정을 막으려고
        //   로그가 세 숫자를 **한 줄에 같이** 찍는다(ms, 제출/초, 그 곱).
        //
        // 비용: 렌더되는 프레임 8장마다 네이티브 호출 1회(= 제출 15장/초에서 초당 약 2회). 할당 0.
        // ============================================================================
        // ★★ 2026-09-01 계기 정직성 라운드 — "실효 제출"을 **세 번째 계기**로 교차 검증한다
        // ============================================================================
        // 문제: 지금까지 절전 효과의 판정 근거였던 두 숫자가 서로 다른 말을 했다.
        //   (A) Time.renderedFrameCount - Time.frameCount 차이  -> "renderFrameInterval이 안 먹었다"
        //   (B) GPU 표본 수가 활성 등급 비율에만 비례            -> "프레임을 건너뛰고 있다"
        //
        // ★ (B)는 **증거가 될 수 없다**(2026-09-01 확인, UnityCsReference
        //   Runtime/Export/Graphics/OnDemandRendering.bindings.cs):
        //       public static bool willCurrentFrameRender => Time.frameCount % renderFrameInterval == 0;
        //   네이티브 바인딩이 없는 **순수 관리 코드 산술**이다. 즉 이 값은 "실제로 그렸는가"가 아니라
        //   "그리기로 되어 있는가"이며, 엔진이 그 계획을 지켰는지와 무관하게 항상 1/interval의 비율로
        //   true가 된다. 그것으로 걸러 센 표본 수가 interval에 비례하는 것은 **동어반복**이다.
        //
        // 그래서 계획이 아니라 **사건**을 센다: Camera.onPostRender는 카메라가 실제로 렌더를 마쳤을 때만
        // 불린다(빌트인 렌더 파이프라인 — 이 프로젝트는 URP/HDRP를 쓰지 않는다: Packages/manifest.json에
        // render-pipelines 패키지가 없고 GraphicsSettings.m_CustomRenderPipeline=0). 프레임이 정말로
        // 건너뛰어졌다면 이 콜백은 그 프레임에 **오지 않는다**.
        //
        // 판정표(요약 로그가 세 숫자를 한 줄에 찍는다):
        //   루프 == renderedFrameCount == 실측콜백  -> 절감 0. renderFrameInterval이 정말 안 먹었다.
        //   루프 == renderedFrameCount >  실측콜백  -> 절감은 되고 있는데 renderedFrameCount가 거짓말.
        //                                             (= 가설 H3 성립. 이 지표로 낸 과거 판정은 무효)
        //   루프 >  renderedFrameCount == 실측콜백  -> 지표가 정확하다. H3 반증.
        //
        // 비용: 프레임당 int 비교 1회 + 대입 1회. 할당 0. 델리게이트 등록 1회.
        private static int _actualRenderedFrames;
        private static int _lastCountedRenderFrame = -1;
        private static bool _renderCallbackHooked;

        private static void EnsureRenderCallbackHooked()
        {
            if (_renderCallbackHooked) return;
            _renderCallbackHooked = true;
            Camera.onPostRender += CountActualRender;
        }

        private static void CountActualRender(Camera cam)
        {
            // 오프스크린 카메라(초상화 스테이지 등)는 화면에 제출되는 프레임이 아니다 — 세면
            // "건너뛴 프레임"이 렌더된 것으로 잘못 잡힌다.
            if (cam != null && cam.targetTexture != null) return;
            // 한 프레임에 카메라가 여러 대면 콜백도 여러 번 온다. 세는 단위는 **프레임**이다.
            if (Time.frameCount == _lastCountedRenderFrame) return;
            _lastCountedRenderFrame = Time.frameCount;
            _actualRenderedFrames++;
        }

        /// <summary>실제 렌더 콜백이 온 **프레임 수**(누적). 요약 로그가 구간 차분으로 쓴다.</summary>
        internal static int ActualRenderedFrameCount => _actualRenderedFrames;

        private const int OngoingGpuSampleStride = 8;
        private static int _ongoingStride;
        private static double _ongoingGpuSumMs;
        private static double _ongoingGpuWorstMs;
        private static int _ongoingGpuCount;

        private static void SampleOngoingGpu()
        {
            if (!_timingFeatureAvailable) return;
            // 건너뛴 프레임에서는 새 타이밍이 나오지 않는다 — 같은 표본을 다시 세지 않기 위해 거른다.
            if (!OnDemandRendering.willCurrentFrameRender) return;
            if (++_ongoingStride < OngoingGpuSampleStride) return;
            _ongoingStride = 0;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, TimingScratch) <= 0) return;
            double gpu = TimingScratch[0].gpuFrameTime;
            if (gpu <= 0.0) return;   // 0 = 아직 질의가 안 돌아왔다는 뜻이지 "0ms"가 아니다.
            _ongoingGpuSumMs += gpu;
            _ongoingGpuCount++;
            if (gpu > _ongoingGpuWorstMs) _ongoingGpuWorstMs = gpu;
        }

        /// <summary>주기 요약이 읽고 비운다. 표본이 없으면 false(0을 진짜 값인 척 찍지 않는다).</summary>
        internal static bool TryDrainGpuLoad(out float meanMs, out float worstMs, out int samples)
        {
            samples = _ongoingGpuCount;
            meanMs = samples > 0 ? (float)(_ongoingGpuSumMs / samples) : 0f;
            worstMs = (float)_ongoingGpuWorstMs;
            _ongoingGpuSumMs = 0.0;
            _ongoingGpuWorstMs = 0.0;
            _ongoingGpuCount = 0;
            return samples > 0;
        }

        /// <summary>GPU 타이밍 질의를 이 실행에서 쓸 수 있는가(로그 문구 분기용).</summary>
        internal static bool GpuTimingAvailable => _timingFeatureAvailable;

        internal static void ResetForTests()
        {
            _snapshotLogged = false;
            _summaryLogged = false;
            _elapsed = 0f;
            _watchTimer = 0f;
            _lastWidth = 0;
            _lastHeight = 0;
            _cpuSumMs = 0.0;
            _cpuWorstMs = 0.0;
            _cpuCount = 0;
            _gpuSumMs = 0.0;
            _gpuWorstMs = 0.0;
            _gpuCount = 0;
            _timingFeatureAvailable = false;
            _measureBaselineLoopFrame = 0;
            _measureBaselineRenderedFrame = 0;
            _measureBaselineActualRenderFrame = 0;
            _measureStartElapsed = 0f;
            // 델리게이트는 static이라 테스트 간에 새어 나간다 — 반드시 풀어 준다.
            if (_renderCallbackHooked)
            {
                Camera.onPostRender -= CountActualRender;
                _renderCallbackHooked = false;
            }
            _actualRenderedFrames = 0;
            _lastCountedRenderFrame = -1;
            _ongoingStride = 0;
            _ongoingGpuSumMs = 0.0;
            _ongoingGpuWorstMs = 0.0;
            _ongoingGpuCount = 0;
        }

        /// <summary><see cref="FramePacing.ApplyOnce"/>가 부른다. 여기서는 로그를 남기지 않는다 —
        /// 이 시점의 백버퍼는 아직 창 부착 전이라 믿을 수 없기 때문이다.</summary>
        internal static void Begin()
        {
            _timingFeatureAvailable = FrameTimingManager.IsFeatureEnabled();
        }

        /// <summary>
        /// 매 프레임 호출된다. 요약이 끝난 뒤의 비용은 <b>float 덧셈 1회 + 비교 1회</b>이고,
        /// 2초에 한 번만 <c>Screen</c>을 읽는다.
        /// </summary>
        internal static void Tick(FramePacingTier tier)
        {
            float dt = Time.unscaledDeltaTime;
            EnsureRenderCallbackHooked();
            SampleOngoingGpu();

            if (_summaryLogged)
            {
                WatchBackbuffer(dt);
                return;
            }

            _elapsed += dt;

            if (!_snapshotLogged)
            {
                // --- 정착 구간: 아직 아무것도 재지 않는다 ---
                // ★ 여기서 표본을 모으면 A/B가 망가진다. 시작 직후에는 셰이더 컴파일, 창 부착
                //   (전체 데스크톱 확장), 첫 GC가 몰려 있어 **수십 ms짜리 스파이크**가 뜬다. 그것들은
                //   MSAA와 아무 상관이 없는데 "최악 프레임" 자리를 차지해 버려서, 두 회차를 비교할 때
                //   MSAA 차이가 아니라 그날의 시작 운을 비교하게 된다. 그래서 정착이 끝난 뒤부터만 잰다.
                if (_elapsed < SnapshotDelaySeconds) return;

                _snapshotLogged = true;
                _lastWidth = Screen.width;
                _lastHeight = Screen.height;
                _measureBaselineLoopFrame = Time.frameCount;
                _measureBaselineRenderedFrame = Time.renderedFrameCount;
                _measureBaselineActualRenderFrame = _actualRenderedFrames;
                _measureStartElapsed = _elapsed;
                LogSnapshot("콜드스타트");
                return;
            }

            // --- 측정 구간(정착 이후 ~ 워밍업 종료). 끝나면 이 블록 자체가 실행되지 않는다 ---
            float cpuMs = dt * 1000f;
            _cpuSumMs += cpuMs;
            if (cpuMs > _cpuWorstMs) _cpuWorstMs = cpuMs;
            _cpuCount++;

            if (_timingFeatureAvailable)
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, TimingScratch) > 0)
                {
                    double gpu = TimingScratch[0].gpuFrameTime;
                    // 0은 "그 프레임의 타이머 질의가 아직 안 돌아왔다"는 뜻이지 "GPU가 0ms 썼다"가
                    // 아니다. 평균에 섞으면 값이 아래로 끌려가므로 버린다.
                    if (gpu > 0.0)
                    {
                        _gpuSumMs += gpu;
                        if (gpu > _gpuWorstMs) _gpuWorstMs = gpu;
                        _gpuCount++;
                    }
                }
            }

            if (_elapsed >= WarmupSeconds)
            {
                _summaryLogged = true;
                LogAbSummary(tier);
            }
        }

        private static void WatchBackbuffer(float dt)
        {
            _watchTimer += dt;
            if (_watchTimer < BackbufferWatchSeconds) return;
            _watchTimer = 0f;

            int w = Screen.width;
            int h = Screen.height;
            if (w == _lastWidth && h == _lastHeight) return;

            _lastWidth = w;
            _lastHeight = h;
            LogSnapshot("백버퍼 변경 감지");
        }

        // ====================================================================
        // 로그 본문
        // ====================================================================

        private static void LogSnapshot(string reason)
        {
            int w = Screen.width;
            int h = Screen.height;
            int samples = Mathf.Max(1, RenderQualityTuner.RequestedSamples);
            long px = (long)w * h;

            // 산술 추정이다(드라이버의 프레임버퍼 압축/타일 최적화는 반영되지 않는다).
            // 그래서 이 숫자는 "규모"를 보라고 찍는 것이지 벤치마크 값이 아니다 — 실제 비용은
            // 아래 A/B 요약의 GPU 프레임 시간이 말한다.
            double colorMb = px * 4.0 * samples / (1024.0 * 1024.0);
            double resolveMb = samples > 1 ? px * 4.0 * (samples + 1) / (1024.0 * 1024.0) : 0.0;
            double bltMb = px * 4.0 * 2 / (1024.0 * 1024.0);

            int interval = Mathf.Max(1, OnDemandRendering.renderFrameInterval);
            int cap = Application.targetFrameRate;
            double presentFps = (cap > 0 ? cap : Screen.currentResolution.refreshRateRatio.value) / interval;

            Camera cam = Camera.main;
            string camInfo = cam == null
                ? "메인 카메라 없음"
                : $"allowMSAA={cam.allowMSAA}, allowHDR={cam.allowHDR}, clear={cam.clearFlags}";

            Debug.Log($"[렌더진단] {reason} — " +
                $"그래픽API={SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}, " +
                $"{SystemInfo.graphicsDeviceVersion}), VRAM={SystemInfo.graphicsMemorySize}MB. " +
                $"렌더타깃(백버퍼)={w}x{h}, 디스플레이모드={Screen.currentResolution.width}x" +
                $"{Screen.currentResolution.height}@{Screen.currentResolution.refreshRateRatio.value:F1}Hz, " +
                $"시스템주화면={Display.main.systemWidth}x{Display.main.systemHeight}, " +
                $"dpi={Screen.dpi:F0}, 창모드={Screen.fullScreenMode}. " +
                $"MSAA {RenderQualityTuner.DescribeState()} " +
                $"(참고: Screen.msaaSamples={Screen.msaaSamples} — 이 값은 백버퍼의 진실이 아니다). " +
                $"카메라: {camInfo}. " +
                $"추정 컬러버퍼 {colorMb:F1}MB, 프레임당 MSAA resolve 트래픽 {resolveMb:F1}MB, " +
                $"프레임당 표면 복사 {bltMb:F1}MB -> 현재 프레젠트 {presentFps:F1}fps(설정값 기준 추정) 기준 " +
                $"{(resolveMb + bltMb) * presentFps / 1024.0:F2}GB/s(산술 추정). " +
                $"페이싱: vSyncCount={QualitySettings.vSyncCount}, targetFrameRate={cap}, " +
                $"renderFrameInterval={interval}, runInBackground={Application.runInBackground}. " +
                $"GPU 타이밍={(_timingFeatureAvailable ? "사용 가능" : "사용 불가(enableFrameTimingStats 꺼짐 또는 드라이버 미지원)")}.");
        }

        /// <summary>
        /// 세 계기(루프 / <c>Time.renderedFrameCount</c> / 실측 렌더 콜백)가 서로 무슨 말을 하는지
        /// 로그가 **스스로** 판정한다. 사람이 세 숫자를 눈으로 비교하는 단계를 없애기 위한 것이다 —
        /// 이 프로젝트는 계기 불일치를 사람이 놓쳐 여러 라운드를 날린 이력이 있다.
        /// </summary>
        private static string DescribeInstrumentAgreement(float loopFps, float renderFps, float actualFps,
            bool actualAvailable)
        {
            if (OnDemandRendering.renderFrameInterval <= 1) return string.Empty;
            if (!actualAvailable)
            {
                return " ※ 실측 렌더 콜백이 한 번도 오지 않았다 — Camera.onPostRender는 빌트인 렌더" +
                       " 파이프라인 전용이다. 렌더 파이프라인을 바꿨다면 이 계기를 그 파이프라인의" +
                       " endContextRendering으로 옮겨야 한다(옮기기 전까지 절전 판정은 근거가 없다)";
            }

            bool reportedSkips = renderFps < loopFps * 0.9f;
            bool actuallySkipped = actualFps < loopFps * 0.9f;

            if (actuallySkipped && !reportedSkips)
            {
                return " ★계기 불일치: 실제로는 건너뛰는데 Time.renderedFrameCount가 루프와 같다" +
                       " — 이 값으로 낸 과거의 절전 판정은 근거가 없다(가설 H3 성립)";
            }
            if (!actuallySkipped && reportedSkips)
            {
                return " ★계기 불일치: renderedFrameCount만 줄었고 실제 렌더는 루프와 같다" +
                       " — renderedFrameCount를 신뢰하지 말 것";
            }
            return actuallySkipped
                ? " (계기 3종 일치: 실제로 건너뛰고 있다)"
                : " (계기 3종 일치: 건너뛰지 않고 있다 = 절감 0)";
        }

        private static void LogAbSummary(FramePacingTier tier)
        {
            float cpuMean = _cpuCount > 0 ? (float)(_cpuSumMs / _cpuCount) : 0f;
            string tail = FrameTimeStats.TrySummarize(out FrameTimeSummary recent)
                ? $"최근 {recent.SampleCount}프레임 p95 {recent.P95Ms:F2}ms / p99 {recent.P99Ms:F2}ms"
                : "최근 분위수: 표본 부족";

            string gpu = _gpuCount > 0
                ? $"GPU 프레임시간 평균 {_gpuSumMs / _gpuCount:F2}ms / 최악 {_gpuWorstMs:F2}ms (표본 {_gpuCount})"
                : (_timingFeatureAvailable
                    ? "GPU 프레임시간: 드라이버가 타이머 질의를 돌려주지 않음(표본 0)"
                    : "GPU 프레임시간: 측정 불가(enableFrameTimingStats 꺼짐 — 이 빌드로는 MSAA 비용을 잴 수 없다)");

            float window = Mathf.Max(0.001f, _elapsed - _measureStartElapsed);
            float loopFps = (Time.frameCount - _measureBaselineLoopFrame) / window;
            float renderFps = (Time.renderedFrameCount - _measureBaselineRenderedFrame) / window;
            float actualFps = (_actualRenderedFrames - _measureBaselineActualRenderFrame) / window;
            // ★ 계기 자신의 정직성 — 콜백이 한 번도 안 왔는데 "0장/초 제출"이라고 찍으면 이 계기가
            //   바로 그 거짓말을 하는 쪽이 된다. Camera.onPostRender는 **빌트인 렌더 파이프라인**
            //   전용이라, 이 프로젝트가 URP/HDRP로 옮겨가면 조용히 0이 된다. 그때는 "측정 불가"라고
            //   말하고 예전 지표(renderedFrameCount)로 되돌아간다.
            bool actualAvailable = actualFps > 0.01f || loopFps <= 1f;
            float submittedFps = actualAvailable ? actualFps : renderFps;
            // ★ 판정은 **실측 콜백**으로 한다. renderedFrameCount는 이 라운드의 조사 대상이다
            //   (위 "계기 정직성" 문단 — 두 계기가 다르면 세 번째가 심판이다).
            bool intervalTookEffect = OnDemandRendering.renderFrameInterval <= 1
                || submittedFps < loopFps * 0.9f;

            Debug.Log($"[렌더진단] ★A/B 요약(정착 {SnapshotDelaySeconds:F0}초 제외, " +
                $"측정 {window:F0}초) — " +
                $"MSAA {RenderQualityTuner.DescribeState()}, " +
                $"백버퍼={Screen.width}x{Screen.height}, 그래픽API={SystemInfo.graphicsDeviceType}, " +
                $"등급={tier}(강제={FramePacing.ForcedTierLabel}). " +
                $"CPU 프레임시간 평균 {cpuMean:F2}ms / 최악 {_cpuWorstMs:F2}ms (표본 {_cpuCount}), {tail}. " +
                $"{gpu}. " +
                $"★ 실효 제출 {submittedFps:F1}장/초({(actualAvailable ? "실측 렌더 콜백" : "renderedFrameCount — 실측 콜백 측정 불가")}) " +
                $"[계기 대조: 루프 {loopFps:F1}Hz / renderedFrameCount {renderFps:F1} / " +
                $"실측 콜백 {(actualAvailable ? $"{actualFps:F1}" : "측정 불가")}], " +
                $"renderFrameInterval={OnDemandRendering.renderFrameInterval}" +
                $"{(intervalTookEffect ? string.Empty : " — ※ 걸리지 않았다: 렌더가 루프와 같은 속도다")}" +
                $"{DescribeInstrumentAgreement(loopFps, renderFps, actualFps, actualAvailable)}. " +
                (_gpuCount > 0
                    ? $"★GPU 점유 추정 {_gpuSumMs / _gpuCount * submittedFps / 10.0:F1}% " +
                      "(= ms/프레임 x 제출/초 / 10 — 작업 관리자 GPU %와 대응하는 값). "
                    : string.Empty) +
                "※ 이 한 줄을 MSAA만 바꾼 다른 콜드스타트의 같은 줄과 비교하세요. " +
                "CPU 프레임시간은 60fps 상한에 가려 잘 안 갈립니다 — **GPU 프레임시간**을 보세요. " +
                "컴포지터(dwm/WindowServer) 부하는 **실효 제출**에 비례합니다. " +
                "등급이 서로 다르면 그 비교는 무효입니다(STICKMATE_FORCE_TIER=Active로 고정하세요).");
        }
    }

}
