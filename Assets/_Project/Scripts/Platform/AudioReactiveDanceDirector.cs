using System;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★★★ <b>「노래가 나오면 춤춘다」의 실행부</b> — 2026-09-06 신설(dev-platform).
    ///
    /// <para>사용자 요청: <i>"소리시스템을 전체빼줘, 다만 시스템에서 노래가 나오면 상호 반응해서
    /// 춤추는 동작을 넣어줘"</i> / <i>"노래소리만감지 제목알필요없음"</i>.</para>
    ///
    /// ============================================================================
    /// ★★ 이 파일이 왜 이제야 생겼는가 — <b>완성된 부품 5개가 서로를 못 부르고 있었다</b>
    /// ============================================================================
    /// 2026-09-03에 계약·정책·게이트·양 플랫폼 구현이 <b>전부</b> 착지했는데,
    /// 사운드 감사(2026-09-06)가 <b>프로덕션 <c>new</c> 0건 / 호출부 0건</b>을 실측했다.
    /// 즉 <b>기능 전체가 한 번도 실행된 적이 없다.</b> 부품은 다 있는데 <b>돌리는 것이 없었다</b>:
    /// <code>
    ///   MacSystemAudioActivityProbe   ─┐
    ///   WindowsSystemAudioActivityProbe┤→ SystemAudioActivityProbeFactory (플랫폼 분기, 신설)
    ///                                  │        ↓
    ///                                  │   AudioReactiveDanceRunner (3절 실패 규칙, 신설)
    ///                                  │        ↓  ← AudioReactiveDancePolicy (T₁~T₄, 기존)
    ///                                  └→ ★ 이 파일 (2Hz 폴링 + 억제 + 훅, 신설)
    ///                                           ↑  Core/AudioReactiveDanceGate (억제 술어, 기존)
    /// </code>
    ///
    /// ============================================================================
    /// ★★★ <b>여기는 춤 «자세»를 모른다 — 그건 design-motion 소관이다</b>
    /// ============================================================================
    /// 이 감독이 하는 말은 <b>딱 한 마디</b>다: <i>"지금 춤춰도 된다 / 안 된다."</i>
    /// <b>어떤 춤을 어떤 박자로 출지, 어떤 상태로 전이할지는 여기서 정하지 않는다.</b>
    /// design-motion이 7종 춤의 자세·박자 사양을 작성 중이고, 그것을
    /// <c>StickmanStateId</c>/<c>StickmanStateMachine</c>으로 옮기는 것은 <b>coder의 다음 라운드</b>다.
    ///
    /// <para><b>다음 라운드가 이어받을 지점(훅)은 정확히 둘이다</b> — 이 파일을 고칠 필요 없이
    /// <b>구독만</b> 하면 된다:
    /// <list type="number">
    ///   <item><see cref="IsDanceGateOpen"/> — 폴링용 프로퍼티. <b>이쪽이 더 튼튼하다</b>
    ///     (이벤트 구독 시점을 놓쳐도 값은 언제나 맞다).</item>
    ///   <item><see cref="DanceGateChanged"/> — 전이 순간만 필요할 때. 인자는 «열렸는가»다.</item>
    /// </list>
    /// ★ <b>여기에 <c>ChangeState(...)</c>를 쓰지 마라.</b> 그 순간 이 파일이 상태 전이 소유권을
    /// 갖게 되고, design-motion이 규정한 <i>"상태의 수평 이동 소유권"</i>(CLAUDE.md 인계 계약)과
    /// 충돌한다. 이 파일은 <b>플랫폼 사실 → 게이트</b>까지가 전부다.</para>
    ///
    /// ============================================================================
    /// ★ 폴링 주기 — 2Hz. <b>숫자는 이 파일에 없다</b>
    /// ============================================================================
    /// <see cref="AudioReactiveDanceRunner.PollIntervalSeconds"/>를 그대로 쓰고, 그 값은 다시
    /// <see cref="AudioReactiveDancePolicy.ReferencePollIntervalSeconds"/>를 참조한다 —
    /// design-systems가 T₁~T₄를 유도할 때 <b>전제로 깔았던 주기</b>다. 유도 근거는 그 두 문서에 있고
    /// 요지는 <b>«곡 사이 공백 1.09초(실측) 안에 표본이 2개는 떨어져야 한다»</b>(⇒ ≤ 0.545초)와
    /// <b>«macOS 실측 30.4µs/회 ⇒ 2Hz면 CPU 0.006%»</b>다. 24시간 상주 앱이라 후자가 중요하다.
    ///
    /// <para><c>Time.unscaledDeltaTime</c>을 쓴다 — 이 판정은 <b>벽시계</b>가 축이고
    /// <c>timeScale</c>과 무관해야 한다(CLAUDE.md의 시간 기반 연출 규칙과 같은 이유).</para>
    ///
    /// ============================================================================
    /// 억제는 <b>매 틱</b> 다시 묻는다 — 발동 시점 1회가 아니다
    /// ============================================================================
    /// 세션은 <b>춤 도중에</b> 시작될 수 있다(음악 → 춤 → 사용자가 [집중 모드] 클릭).
    /// 발동 시점에만 보면 그 춤은 세션 내내 계속되고, <i>"집중 모드 켰는데 계속 춤춘다"</i>가
    /// 재현 조건이 까다로운 신고로 올라온다(<c>Core/AudioReactiveDanceGate</c> 문서).
    /// ⇒ <see cref="ResolveSuppressed"/>를 매 폴링마다 부른다.
    ///
    /// <para>★ <b>배선을 못 찾으면 «막는다»가 정답이다.</b> <c>BlocksNow(null, null)</c>는 <c>true</c>이고
    /// 그것은 그 게이트가 <b>의도적으로</b> 고른 기본값이다 — 자동 발동 경로에는 보여줄 사유도,
    /// 그것을 읽을 사람도 없기 때문이다. 그래서 여기서 <c>null</c>을 특별 취급하지 않는다.</para>
    ///
    /// ============================================================================
    /// 로그 태그
    /// ============================================================================
    /// 이 감독의 판정은 <see cref="LogTag"/>(<c>[음악춤]</c>), <b>프로브의 조회 실패</b>는
    /// <c>[오디오감지]</c>로 찍힌다. 사용자 신고가 오면 <b>둘 다</b> grep해야 «감지가 안 된 것»과
    /// «게이트가 막은 것»이 갈린다.
    /// </summary>
    public sealed class AudioReactiveDanceDirector : MonoBehaviour
    {
        /// <summary>사용자가 Player.log에서 찾을 태그. 한 곳에서만 정의한다.</summary>
        public const string LogTag = "[음악춤]";

        /// <summary>
        /// ★★ <b>관측 사실</b> 전용 태그 — 2026-09-06 신설(페르소나 «재현» 실기 신고 대응).
        ///
        /// <para><b>왜 태그를 하나 더 만드는가.</b> 이 기능이 실기에서 통째로 죽어 있었는데
        /// <b>3분 넘게 로그에 한 줄도 안 찍혔다.</b> 원인은 <see cref="SetGateOpen"/>이 <b>무로그</b>였다는
        /// 것이다 — 게이트가 열렸는지조차 알 수 없으니 아래 셋이 <b>겉보기가 전부 같았다</b>:</para>
        /// <list type="number">
        ///   <item><b>감지 실패</b> — 프로브가 못 읽는다(<see cref="LastReadOk"/>가 계속 거짓).</item>
        ///   <item><b>소비자 부재</b> — 게이트는 열렸는데 그 신호를 읽는 컴포넌트가 씬에 없다
        ///     (2026-09-06 실제 사고: <c>Stickman.prefab</c>에
        ///     <see cref="DanceEpisodeDirector"/>가 없었다).</item>
        ///   <item><b>억제 중</b> — <see cref="AudioReactiveDanceGate"/>가 막고 있다.</item>
        /// </list>
        /// <para>이 태그의 줄만 <c>grep</c>하면 셋이 갈린다. <see cref="LogTag"/>는 «배선/기동»이고
        /// 이쪽은 «지금 무엇이 관측되는가»다.</para>
        /// </summary>
        public const string DiagnosticLogTag = "[오디오감지]";

        /// <summary>주기 요약 간격(초). 24시간 상주 앱이라 <b>5분에 한 줄</b>이 상한이다 —
        /// 그보다 잦으면 사용자 로그가 이 기능 하나로 뒤덮이고, 그보다 뜸하면
        /// «3분 틀어 봤는데 아무 줄도 없다»는 이번 신고가 그대로 재발한다.</summary>
        private const float SummaryIntervalSeconds = 300f;

        /// <summary>상주 호스트 오브젝트 이름. <c>~</c> 접두는 이 저장소의 내부 오브젝트 관례다.</summary>
        private const string HostObjectName = "~StickMateAudioReactiveDance";

        /// <summary>에이전트/집중감시자를 못 찾았을 때 다시 찾기까지의 간격.
        /// <c>FindAnyObjectByType</c>은 씬 전체 스캔이라 매 틱 돌리면 안 된다
        /// (<c>WindowsOverlayStateEnforcer.EnsureAgentResolved</c>와 같은 관례·같은 값).</summary>
        private const float ReferenceLookupRetrySeconds = 1f;

        /// <summary>이만큼 배선을 못 찾은 채로 흐르면 <b>한 번만</b> 경고한다. 씬에
        /// <c>FocusWatchDirector</c>가 없으면 게이트는 영원히 닫혀 있는데, 그 상태는
        /// «음악이 안 나온다»와 겉보기가 같다 — 그 오진을 막는 줄이다.</summary>
        private const float UnresolvedWarnAfterSeconds = 30f;

        // ====================================================================
        // ★★ 훅 — 다음 라운드(coder)가 이어받는 지점. 여기서부터 아래가 공개 표면이다.
        // ====================================================================

        /// <summary>
        /// ★ <b>지금 춤 게이트가 열려 있는가.</b> design-motion 사양이 나온 뒤 coder가 읽을 값이다.
        ///
        /// <para><b>«춤추고 있다»가 아니라 «춤춰도 된다»이다.</b> 실제 상태 전이는 아직 아무도 하지
        /// 않는다(이번 라운드 범위 밖). 배선이 없거나 조회가 실패하면 <c>false</c>이고, 그것이
        /// 자동 발동 경로의 안전한 방향이다.</para>
        /// </summary>
        public static bool IsDanceGateOpen { get; private set; }

        /// <summary>
        /// ★ 게이트가 <b>열리거나 닫히는 순간</b>에만 불린다(인자 = 열렸는가).
        ///
        /// <para>★ 도메인 리로드를 끈 에디터에서는 구독이 <b>플레이 세션을 넘어 남을 수 있다</b>.
        /// 테스트는 <see cref="ResetForTesting"/>로 비우고 시작해라 —
        /// <see cref="ReservedBarRevealDirector.ResetForTesting"/>와 같은 관례다.</para>
        ///
        /// <para><b>구독 시점을 놓쳐도 되는 쪽을 권한다</b>: <see cref="IsDanceGateOpen"/>는 언제 읽어도
        /// 맞고, 이벤트는 <c>RunStartup</c>보다 늦게 구독하면 첫 전이를 놓칠 수 있다.</para>
        /// </summary>
        public static event Action<bool> DanceGateChanged;

        /// <summary>마지막 폴링의 결론. 진단·테스트용이다.</summary>
        public static AudioReactiveDanceAction LastAction { get; private set; } = AudioReactiveDanceAction.None;

        /// <summary>마지막 폴링의 사유(사람이 읽는 문장). 정책 또는 러너가 낸 <c>const</c>다.</summary>
        public static string LastReason { get; private set; } = AudioReactiveDanceRunner.ReasonNoProbe;

        /// <summary>어느 다리로 서 있는가. 사용자 신고에서 플랫폼을 가리기 위한 것이다.</summary>
        public static string PlatformTag { get; private set; } = SystemAudioActivityProbeFactory.NoProbeTag;

        /// <summary>이 빌드에 시스템 오디오 감지 배선이 있는가. <c>false</c>면 게이트는 영원히 닫힌 채이고
        /// <b>그것이 설계된 동작</b>이다(에디터·모바일).</summary>
        public static bool HasProbe { get; private set; }

        /// <summary>★ 직전 폴링의 <b>조회</b>가 성공했는가. <c>HasProbe == true</c>인데 이 값이 계속
        /// <c>false</c>면 «음악이 없다»가 아니라 <b>«프로브가 못 읽고 있다»</b>이다.</summary>
        public static bool LastReadOk { get; private set; }

        // ====================================================================
        // 내부
        // ====================================================================

        private static AudioReactiveDanceDirector _instance;

        private AudioReactiveDanceRunner _runner;
        private float _pollTimer;
        private float _nextLookupTime;
        private float _unresolvedSeconds;
        private bool _unresolvedWarned;

        private StickmanAgent _agent;
        private FocusWatchDirector _focus;

        // ==================== 진단 요약 누산기(2026-09-06) ====================
        // 정적인 이유: 전이 로그는 static인 SetGateOpen에서, 누산은 인스턴스 Update에서 일어나는데
        // 호스트는 언제나 한 개뿐이다(EnsureExists). ResetForTesting이 전부 비운다.

        /// <summary>이번 요약 창에서 흐른 벽시계 시간(초).</summary>
        private static float _summaryWindowSeconds;

        /// <summary>이번 요약 창에서 게이트가 <b>열려 있던</b> 시간(초).</summary>
        private static float _summaryGateOpenSeconds;

        /// <summary>이번 요약 창에서 게이트가 «열림»으로 전이한 횟수.</summary>
        private static int _summaryGateOpenCount;

        /// <summary>이번 요약 창에서 프로브 <b>조회가 성공</b>한 폴링 횟수 / 전체 폴링 횟수.</summary>
        private static int _summaryReadOkPolls;
        private static int _summaryPolls;

        /// <summary>이번 요약 창에서 <b>억제</b>로 판정된 폴링 횟수.</summary>
        private static int _summarySuppressedPolls;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // ★ AfterSceneLoad인 이유: 억제 판정이 씬의 StickmanAgent·FocusWatchDirector를 읽는다.
            //   (ReservedBarRevealDirector가 BeforeSceneLoad인 것은 «첫 작업표시줄 실측보다 먼저»라는
            //    그쪽만의 타이밍 계약 때문이고, 여기에는 그런 제약이 없다.)
            RunStartup(SystemAudioActivityProbeFactory.Create());
        }

        /// <summary>
        /// 기동 경로 — <b>테스트가 가짜 프로브를 주입할 수 있도록 공개한다</b>
        /// (<see cref="ReservedBarRevealDirector.RunStartup"/>와 같은 관례).
        ///
        /// <para><paramref name="probe"/>가 <c>null</c>이면 <b>호스트 오브젝트를 만들지 않는다</b> —
        /// 에디터·모바일에서 아무것도 안 하는 <c>Update</c>를 24시간 돌릴 이유가 없다.</para>
        /// </summary>
        public static void RunStartup(ISystemAudioActivityProbe probe)
        {
            HasProbe = probe != null;
            PlatformTag = probe != null ? probe.PlatformTag : SystemAudioActivityProbeFactory.NoProbeTag;
            LastAction = AudioReactiveDanceAction.None;
            LastReason = AudioReactiveDanceRunner.ReasonNoProbe;
            LastReadOk = false;
            SetGateOpen(false);

            if (probe == null)
            {
                // 정직한 실패 보고다 — 고장이 아니다. 이 줄이 없으면 «왜 춤을 안 추지»가
                // «감지가 깨졌다»로 오진된다.
                Debug.Log($"{LogTag} 이 빌드에는 시스템 오디오 감지 배선이 없습니다({PlatformTag}). " +
                          "음악 반응 춤은 발동하지 않습니다 — 다른 기능은 영향을 받지 않습니다.");
                DestroyHost();
                return;
            }

            AudioReactiveDanceDirector host = EnsureExists();
            host._runner = new AudioReactiveDanceRunner(probe);
            host._pollTimer = AudioReactiveDanceRunner.PollIntervalSeconds; // 첫 관측을 다음 Update에서 곧바로.
            host._unresolvedSeconds = 0f;
            host._unresolvedWarned = false;

            Debug.Log($"{LogTag} 시스템 오디오 감지를 배선했습니다 — {PlatformTag}, " +
                      $"폴링 {AudioReactiveDanceRunner.PollIntervalSeconds:F2}초 간격" +
                      $"(OS 푸시 제공={probe.SupportsPushNotification}, 이번 라운드는 폴링만 합니다). " +
                      "샘플은 한 개도 읽지 않습니다(상태/미터 조회입니다).");
        }

        private static AudioReactiveDanceDirector EnsureExists()
        {
            if (_instance != null) return _instance;

            var existing = UnityEngine.Object.FindAnyObjectByType<AudioReactiveDanceDirector>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return existing;
            }

            var go = new GameObject(HostObjectName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioReactiveDanceDirector>();
            return _instance;
        }

        private static void DestroyHost()
        {
            if (_instance == null) return;
            GameObject go = _instance.gameObject;
            _instance = null;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        private void Update()
        {
            using var __stall = StallAttribution.Section(StallSection.SystemAudio);   // [스톨구간] 계측

            if (_runner == null) return;

            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer < AudioReactiveDanceRunner.PollIntervalSeconds) return;

            // ★ 나눈 나머지를 버리지 않고 «실제로 흐른 시간»을 그대로 넘긴다.
            //   정책의 T₁~T₄는 전부 누산이라, 명목값 0.5를 넣으면 누산이 조금씩 느려진다.
            float elapsed = _pollTimer;
            _pollTimer = 0f;

            bool suppressed = ResolveSuppressed(elapsed);
            AudioReactiveDanceAction action = _runner.Tick(elapsed, suppressed);

            LastAction = action;
            LastReason = _runner.LastReason;
            LastReadOk = _runner.LastReadOk;
            SetGateOpen(_runner.IsGateOpen);

            AccumulateDiagnostics(elapsed, suppressed);
        }

        // ============================================================================
        // 진단 로그 (2026-09-06) — «감지 실패» / «소비자 부재» / «억제 중»을 로그만 보고 가른다
        // ============================================================================

        /// <summary>
        /// 폴링 1회분을 요약 창에 누산하고, 창이 차면 <b>한 줄</b>을 찍는다.
        ///
        /// <para><b>프레임이 아니라 폴링에서 누산하는 이유</b>: 폴링은 2Hz라 5분 창의 오차가 최대
        /// 0.5초다(0.17%). 반대로 매 프레임 누산하면 상주 앱의 <c>Update</c>에 상시 비용이 붙는다.</para>
        /// </summary>
        private void AccumulateDiagnostics(float elapsed, bool suppressed)
        {
            _summaryWindowSeconds += elapsed;
            if (IsDanceGateOpen) _summaryGateOpenSeconds += elapsed;
            _summaryPolls++;
            if (LastReadOk) _summaryReadOkPolls++;
            if (suppressed) _summarySuppressedPolls++;

            if (_summaryWindowSeconds < SummaryIntervalSeconds) return;

            int consumers = CountGateConsumers();
            float openPercent = _summaryWindowSeconds > 0f
                ? _summaryGateOpenSeconds / _summaryWindowSeconds * 100f : 0f;

            Debug.Log($"{DiagnosticLogTag} {_summaryWindowSeconds / 60f:F1}분 요약 — " +
                $"플랫폼={PlatformTag}, 폴링 {_summaryPolls}회 중 조회성공 {_summaryReadOkPolls}회, " +
                $"게이트 열림 {_summaryGateOpenCount}회 / 누적 {_summaryGateOpenSeconds:F0}초({openPercent:F1}%), " +
                $"억제 {_summarySuppressedPolls}회, 소비자 {consumers}개, " +
                $"마지막 판정={LastAction}, 마지막 사유=\"{LastReason}\". " +
                ResolveSummaryVerdict(consumers));

            _summaryWindowSeconds = 0f;
            _summaryGateOpenSeconds = 0f;
            _summaryGateOpenCount = 0;
            _summaryPolls = 0;
            _summaryReadOkPolls = 0;
            _summarySuppressedPolls = 0;
        }

        /// <summary>요약 한 줄의 <b>마지막 문장</b> — 사용자·팀이 읽을 결론이다.
        /// 갈래는 사유 «문자열»이 아니라 <b>관측된 수</b>로만 나눈다.</summary>
        private string ResolveSummaryVerdict(int consumers)
        {
            if (!HasProbe)
                return "★ 이 빌드에는 시스템 오디오 감지 배선이 없습니다 — 설계된 동작입니다(에디터·모바일).";
            if (_summaryReadOkPolls == 0 && _summaryPolls > 0)
                return "★ 조회가 한 번도 성공하지 못했습니다 — «음악이 없다»가 아니라 «프로브가 못 읽고 있다»입니다.";
            if (_summaryGateOpenSeconds > 0f && consumers == 0)
                return "★ 게이트는 열렸는데 그 신호를 읽는 컴포넌트가 씬에 하나도 없습니다 — " +
                       "화면에서는 아무 일도 일어나지 않습니다(프리팹 배선 결함).";
            if (_summarySuppressedPolls >= _summaryPolls && _summaryPolls > 0)
                return "★ 창 내내 억제 중이었습니다 — 집중 세션 / 캐릭터 숨김 / 전체화면 앱 / " +
                       "«이번 세션만 끄기» 중 하나입니다.";
            if (_summaryGateOpenSeconds <= 0f)
                return "게이트가 한 번도 열리지 않았습니다 — 이 구간에는 소리가 없었다는 뜻입니다.";
            return "정상입니다.";
        }

        /// <summary>
        /// 이 게이트 신호를 <b>실제로 읽는</b> 컴포넌트가 씬에 몇 개 있는가.
        ///
        /// <para><b>전이 순간과 5분 요약에서만</b> 부른다 — <c>FindObjectsByType</c>은 씬 전체 스캔이다.
        /// 이 수가 0이면 «게이트는 열렸는데 화면은 그대로»이고, 그 상태는 «음악이 안 나온다»와
        /// 겉보기가 완전히 같다. 2026-09-06에 실제로 그 오진이 났다.</para>
        /// </summary>
        private static int CountGateConsumers()
        {
            return UnityEngine.Object.FindObjectsByType<DanceEpisodeDirector>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// 지금 춤을 막아야 하는가. <b>판정은 여기서 하지 않는다</b> —
        /// <c>Core/AudioReactiveDanceGate</c>가 유일한 술어다(판정을 두 벌로 만들면 반드시 갈라진다).
        /// </summary>
        private bool ResolveSuppressed(float elapsed)
        {
            EnsureReferencesResolved();

            if (_agent == null || _focus == null)
            {
                _unresolvedSeconds += elapsed;
                if (!_unresolvedWarned && _unresolvedSeconds >= UnresolvedWarnAfterSeconds)
                {
                    _unresolvedWarned = true;
                    Debug.LogWarning($"{LogTag} 억제 판정에 필요한 배선을 {_unresolvedSeconds:F0}초 동안 " +
                        $"찾지 못했습니다(캐릭터={( _agent != null ? "있음" : "없음")}, " +
                        $"집중감시자={(_focus != null ? "있음" : "없음")}). " +
                        "그동안 춤은 «모르면 안 한다»로 막힙니다 — 이 상태는 겉보기가 «음악이 안 나온다»와 " +
                        "같으므로, 춤이 안 나온다는 신고가 오면 이 줄을 먼저 확인하십시오.");
                }
            }
            else
            {
                _unresolvedSeconds = 0f;
            }

            return AudioReactiveDanceGate.BlocksNow(_agent, _focus);
        }

        /// <summary>참조 확보 — <b>못 찾아도 매 틱 다시 찾지 않는다</b>.
        /// <c>FindAnyObjectByType</c>은 씬 전체 스캔이다(그 비용 근거는
        /// <c>WindowsOverlayStateEnforcer.EnsureAgentResolved</c> 문서에 있다).</summary>
        private void EnsureReferencesResolved()
        {
            if (_agent != null && _focus != null) return;
            if (Time.unscaledTime < _nextLookupTime) return;
            _nextLookupTime = Time.unscaledTime + ReferenceLookupRetrySeconds;

            if (_agent == null) _agent = UnityEngine.Object.FindAnyObjectByType<StickmanAgent>();
            if (_focus == null) _focus = UnityEngine.Object.FindAnyObjectByType<FocusWatchDirector>();
        }

        /// <summary>
        /// 게이트 전이. <b>2026-09-06 이전에는 여기가 무로그였다</b> — 그래서 이 기능이 통째로 죽어
        /// 있는데도 Player 로그에 3분 넘게 한 줄도 안 남았다(<see cref="DiagnosticLogTag"/> 문서).
        ///
        /// <para>로그는 <b>전이에서만</b> 찍는다. 이 함수의 첫 줄(같은 값이면 즉시 반환)이 그것을
        /// 구조적으로 보장하므로, 폴링 2Hz에도 로그는 «열림/닫힘»마다 한 줄뿐이다.</para>
        /// </summary>
        private static void SetGateOpen(bool open)
        {
            if (IsDanceGateOpen == open) return;
            IsDanceGateOpen = open;
            if (open) _summaryGateOpenCount++;

            int consumers = CountGateConsumers();
            if (open)
            {
                Debug.Log($"{DiagnosticLogTag} 춤 게이트 «열림» — 플랫폼={PlatformTag}, " +
                    $"직전 조회={(LastReadOk ? "성공" : "실패")}, 판정={LastAction}, 사유=\"{LastReason}\", " +
                    $"소비자 {consumers}개." +
                    (consumers == 0
                        ? " ★ 이 신호를 읽는 컴포넌트가 씬에 하나도 없습니다 — 게이트는 열렸지만 화면에서는 " +
                          "아무 일도 일어나지 않습니다. Stickman 프리팹에 " + nameof(DanceEpisodeDirector) +
                          "가 붙어 있는지 확인하십시오."
                        : " 이제 에피소드 감독이 시작을 시도합니다(그쪽 로그 태그는 " +
                          DanceEpisodeDirector.LogTag + "입니다)."));
            }
            else
            {
                Debug.Log($"{DiagnosticLogTag} 춤 게이트 «닫힘» — 플랫폼={PlatformTag}, " +
                    $"직전 조회={(LastReadOk ? "성공" : "실패")}, 판정={LastAction}, 사유=\"{LastReason}\", " +
                    $"소비자 {consumers}개. 닫힘은 «소리가 끊겼다» 또는 «억제»가 원인이며, 사유 문자열이 그것을 " +
                    "구분합니다.");
            }

            DanceGateChanged?.Invoke(open);
        }

        /// <summary>테스트 격리용 — 정적 상태와 <b>구독자</b>를 전부 비운다.
        /// 정적 이벤트는 도메인 리로드를 끈 에디터에서 세션을 넘어 남는다.</summary>
        public static void ResetForTesting()
        {
            DanceGateChanged = null;
            IsDanceGateOpen = false;
            LastAction = AudioReactiveDanceAction.None;
            LastReason = AudioReactiveDanceRunner.ReasonNoProbe;
            PlatformTag = SystemAudioActivityProbeFactory.NoProbeTag;
            HasProbe = false;
            LastReadOk = false;
            _summaryWindowSeconds = 0f;
            _summaryGateOpenSeconds = 0f;
            _summaryGateOpenCount = 0;
            _summaryPolls = 0;
            _summaryReadOkPolls = 0;
            _summarySuppressedPolls = 0;
            DestroyHost();
        }
    }
}
