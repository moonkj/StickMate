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
            _summaryTimer = 0f;
            for (int i = 0; i < TierSeconds.Length; i++) TierSeconds[i] = 0f;
            FrameTimeStats.ResetForTests();
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
        /// 호출부(각 플랫폼 Enforcer)가 상태머신에서 읽어 넘긴다. 모르면 false를 넘기면 되고,
        /// 그러면 적응형 등급이 <see cref="FramePacingTier.Calm"/>으로 내려가지 않을 뿐 나머지
        /// (자리 비움 / 디스플레이 꺼짐)는 그대로 동작한다.</param>
        internal static void Tick(bool characterIdle = false)
        {
            FrameTimeStats.Tick();
            TickAdaptiveGovernor(characterIdle);
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

        // 평소(Active) 값 — 두 플랫폼이 서로 다른 기구를 쓰지만 판단 함수는 둘 다 받는다.
        private static int _baseVSyncCount;
        private static int _baseTargetFrameRate = -1;

        /// <summary>OS 관측 주기(초). 활성 등급에서는 느긋하게, 이미 절감 중일 때는 촘촘하게 —
        /// 절감 중에는 프레임 자체가 싸므로 폴링을 늘려도 되고, 오히려 <b>깨어나는 지연</b>을 줄이는
        /// 것이 중요하다(사용자가 마우스를 움직인 순간 늦게 깨면 그게 곧 렉으로 보인다).</summary>
        private const float PresencePollActiveSeconds = 0.5f;
        private const float PresencePollDormantSeconds = 0.2f;

        /// <summary>캐릭터가 이만큼(초) 계속 IDLE이어야 Calm으로 내려간다. 서 있다가 곧바로 다시
        /// 걷는 짧은 정지에서 등급이 깜빡이지 않게 하는 히스테리시스.</summary>
        private const float CalmDwellSeconds = 0.75f;

        /// <summary>지금 적용 중인 등급(진단/테스트 창구 — 제품 로직은 읽지 않는다).</summary>
        internal static FramePacingTier CurrentTier => _currentTier;

        /// <summary>마지막 OS 관측값(진단용).</summary>
        internal static ViewerPresenceSnapshot LastPresence => _presence;

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
                $"등급: 활성(그대로) / 정적(1/2) / 자리비움(1/4) / 화면꺼짐({FramePacingPolicy.DisplayOffTargetFps}fps 고정). " +
                (_forcedTier.HasValue ? $"★ STICKMATE_FORCE_TIER={_forcedTier.Value} 강제 지정됨(계측용). " : "") +
                "근거/실측은 FramePacing·FramePacingPolicy 클래스 문서 참고.");
        }

        private static void TickAdaptiveGovernor(bool characterIdle)
        {
            if (!_applied || !_adaptiveEnabled) return;

            float dt = Time.unscaledDeltaTime;
            _idleDwellSeconds = characterIdle ? _idleDwellSeconds + dt : 0f;
            AccumulateTierResidency(dt);

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

            FramePacingTier tier = _forcedTier
                ?? FramePacingPolicy.DecideTier(_presence, _suspendedNow, characterIdle);
            FramePacingPlan plan = FramePacingPolicy.BuildPlan(
                tier, _baseVSyncCount, _baseTargetFrameRate, _presence.Valid && _presence.LowPowerMode);
            ApplyPlan(plan);
        }

        /// <summary>
        /// 손잡이를 실제로 돌리는 <b>유일한</b> 지점. 값이 그대로면 아무것도 하지 않는다(24시간 상주
        /// 앱에서 같은 값을 매 폴링마다 대입하면 그 자체가 낭비이고, 로그도 더러워진다).
        /// </summary>
        private static void ApplyPlan(in FramePacingPlan plan)
        {
            if (_planValid && plan.SameAs(_currentPlan) && plan.Tier == _currentTier) return;

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
                    $"renderFrameInterval={plan.RenderFrameInterval}. 관측: {_presence}." +
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
        private static int _transitionCount;
        private static float _summaryTimer;
        private static readonly float[] TierSeconds = new float[5];

        private static void AccumulateTierResidency(float dt)
        {
            int i = (int)_currentTier;
            if (i >= 0 && i < TierSeconds.Length) TierSeconds[i] += dt;

            _summaryTimer += dt;
            if (_summaryTimer < TierSummaryIntervalSeconds) return;
            _summaryTimer = 0f;

            float total = 0f;
            for (int k = 0; k < TierSeconds.Length; k++) total += TierSeconds[k];
            if (total <= 0f) return;

            Debug.Log($"[FramePacing/적응형] 최근 {total:F0}초 등급 체류 — " +
                $"활성 {TierSeconds[0] / total * 100f:F0}% / 정적 {TierSeconds[1] / total * 100f:F0}% / " +
                $"자리비움 {TierSeconds[2] / total * 100f:F0}% / 전체화면숨김 {TierSeconds[3] / total * 100f:F0}% / " +
                $"화면꺼짐 {TierSeconds[4] / total * 100f:F0}%, 전이 {_transitionCount}회 (누적). " +
                "(활성 비율이 100%에 가까우면 절감이 전혀 안 되고 있다는 뜻이다.)");

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
        private static bool _enabled;

        internal static void ResetForTests()
        {
            _sampleCount = 0;
            _sampleHead = 0;
            _reportTimer = 0f;
            _enabled = false;
        }

        internal static void Configure(Core.StickConfig config)
        {
            _enabled = config != null && config.logFrameTimeStats;
        }

        internal static void Tick()
        {
            if (!_enabled) return;

            Samples[_sampleHead] = Time.unscaledDeltaTime * 1000f;
            _sampleHead = (_sampleHead + 1) % SampleCapacity;
            if (_sampleCount < SampleCapacity) _sampleCount++;

            _reportTimer += Time.unscaledDeltaTime;
            if (_reportTimer < ReportIntervalSeconds || _sampleCount < 16) return;
            _reportTimer = 0f;

            int n = _sampleCount;
            System.Array.Copy(Samples, SortScratch, n);
            System.Array.Sort(SortScratch, 0, n);

            float sum = 0f;
            for (int i = 0; i < n; i++) sum += SortScratch[i];
            float mean = sum / n;

            float p50 = SortScratch[n / 2];
            float p95 = SortScratch[Mathf.Min(n - 1, Mathf.RoundToInt(n * 0.95f))];
            float p99 = SortScratch[Mathf.Min(n - 1, Mathf.RoundToInt(n * 0.99f))];
            float max = SortScratch[n - 1];

            // ★ 이 값은 **게임 루프 주기**이지 화면에 실제로 나가는 주기가 아니다.
            //   renderFrameInterval이 2 이상이면 루프는 60Hz인데 프레젠트는 30fps일 수 있다 —
            //   그 차이를 모르면 "60fps인데 왜 반만 부드럽지?"라고 잘못 진단하게 되므로 함께 찍는다.
            int interval = Mathf.Max(1, OnDemandRendering.renderFrameInterval);
            float loopFps = mean > 0f ? 1000f / mean : 0f;

            Debug.Log($"[프레임시간] 표본 {n}개 — 루프 평균 {mean:F2}ms({loopFps:F1}fps) " +
                $"p50 {p50:F2}ms / p95 {p95:F2}ms / p99 {p99:F2}ms / 최대 {max:F2}ms. " +
                $"vSyncCount={QualitySettings.vSyncCount}, targetFrameRate={Application.targetFrameRate}, " +
                $"renderFrameInterval={interval} -> 실제 프레젠트 약 {loopFps / interval:F1}fps" +
                (interval > 1 ? " (적응형 절감 중)" : "") + ". " +
                "(최대가 p50의 2배를 크게 넘으면 그 순간이 사용자가 '렉'으로 느끼는 지점이다.)");
        }
    }
}
