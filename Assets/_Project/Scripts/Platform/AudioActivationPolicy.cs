namespace StickMate.Platform
{
    /// <summary>언제 장치를 만질 것인가. 한 틱에 하나만 나온다.</summary>
    public enum AudioActivationAction
    {
        /// <summary>아무 것도 하지 않는다.</summary>
        None = 0,
        /// <summary>지금 연다. ★ <b>소리가 실제로 필요한 그 순간에만</b> — 미리 열지 않는다(클래스 문서 3절).</summary>
        Open = 1,
        /// <summary>지금 완전히 닫는다(0.3초 안에 절전 어서션이 풀린다).</summary>
        Close = 2,
    }

    /// <summary>
    /// 온디맨드 오디오 개폐 판정 — <b>순수 함수</b>. Unity API도, <c>DllImport</c>도, 플랫폼 분기도 없다.
    ///
    /// ============================================================================
    /// 왜 여기(플랫폼 중립)에 있는가
    /// ============================================================================
    /// <c>macOS</c>는 AudioToolbox, <c>Windows</c>는 <c>winmm</c>으로 <b>다른 API를 부르지만
    /// 규칙은 완전히 같다</b>. 이 파일이 <c>Platform/MacOS/</c> 안에 있으면 Windows는 같은 규칙을
    /// 물리적으로 호출할 수 없고, 그쪽 라운드가 규칙을 <b>처음부터 다시 쓴다</b> —
    /// 이 저장소가 <c>FullscreenSuspendPolicy.cs</c>에서 정확히 그렇게 당했다.
    /// 그리고 이 개발 머신에는 Windows가 없으므로, 규칙이 중립이라야 <b>EditMode가 양 플랫폼의
    /// 판정을 실제로 검증할 수 있다</b>(플랫폼 폴더 안의 규칙은 여기서 한 번도 컴파일되지 않는다).
    ///
    /// ============================================================================
    /// ★ 1절 — 개방 트리거에 <b>입력 이벤트를 걸면 안 된다</b> (design-sound Q1, 2026-09-02)
    /// ============================================================================
    /// 처음 설계는 "사용자가 버튼 위에 커서를 올리면 미리 연다" 같은 <b>입력 신호</b>를 개방 힌트로
    /// 쓰려 했다. <c>design-sound</c>의 실측이 그 전제를 깼다:
    /// <list type="table">
    ///  <item><term><c>focus.start</c></term><description>소리 시각에 입력이 붙어 있다</description></item>
    ///  <item><term>설정창 미리듣기</term><description>붙어 있다</description></item>
    ///  <item><term><b><c>focus.complete</c></b></term><description>
    ///   <b>없다 — 최대 25분 전이 마지막 입력이다</b></description></item>
    /// </list>
    /// 즉 <b>입력 게이트를 붙이면 이 기능이 존재하는 이유인 그 소리가 100% 죽는다.</b>
    /// 그리고 그것은 사고가 아니라 설계다 — <c>SILENCE_POLICY</c> §3-8의 M3(자리 비움 ≥180초 묵음)가
    /// <c>focus.*</c> 3키를 <b>면제</b>하는 이유가 정확히 <i>"화면을 안 보는 사용자에게만 값이 있는
    /// 소리"</i>이기 때문이다.
    ///
    /// <para>그래서 이 정책의 개방 조건은 <b>타이머(=재생 요청) 하나뿐</b>이다.
    /// <see cref="AudioActivationInputs.ShellForbidsAudio"/>에 <b>자리 비움/디스플레이 슬립을 넣지
    /// 않는 것</b>도 같은 이유다 — 넣는 순간 <c>focus.complete</c>가 구조적으로 사라진다.
    /// 거기에는 소리가 <b>물리적으로 무의미하거나 침해가 되는</b> 두 가지만 들어간다(세션 잠금 /
    /// 남의 전체화면). 묵음 판정 자체는 <c>design-sound</c>의 것이라
    /// <see cref="AudioActivationInputs.SuppressedBySilencePolicy"/>로 <b>결과만 받는다</b>.</para>
    ///
    /// ============================================================================
    /// ★ 2절 — 닫기 기준시각은 재생 <b>「완료」</b>다 (design-sound Q2)
    /// ============================================================================
    /// <see cref="AudioActivationInputs.SecondsSinceLastPlaybackEnded"/>의 기준은 재생 <b>시작이
    /// 아니라 종료</b>다. 시작 기준으로 재면 성취음 800ms가 잔류 안에서 끝나는 순간
    /// <b>꼬리가 잘린다</b>. 확정 잔류는 <b>3.0초</b>.
    /// <code>
    ///   1회 최대 개방 = 0.800(클립) + 3.0(잔류) = 3.800초
    ///   어서션 최대 보유 = 3.800 + 0.3(해제 지연) = 4.100초 = 최단 디스플레이 슬립 120초의 3.4%
    ///   출하 1차(하루 3건) 듀티 = 3 × 3.8 / 86400 = 0.0132%  → 상시 개방 대비 1/7,579
    /// </code>
    /// <b>정직한 한계</b>: <c>focus.start</c>와 <c>focus.complete</c>는 25분 떨어져 있어
    /// <b>어떤 잔류로도 못 잇는다 — 둘 다 항상 콜드 개방이다.</b>
    ///
    /// ============================================================================
    /// ★ 3절 — <b>미리 열면 오히려 나쁘다</b> (design-sound Q3, 처음 설계를 뒤집은 항목)
    /// ============================================================================
    /// 콜드 48ms는 허용됐다. <b>그런데 이유가 내가 가정한 것과 반대다.</b> 두 트랜지언트가 하나로
    /// 융합되는 창이 약 <b>50ms</b>이고 48ms는 그 <b>안쪽</b>이라, <b>장치 개방 클릭이 우리 소리의
    /// 어택에 흡수된다.</b> 완료 1.5초 전에 미리 여는 안은 <b>기각됐다</b> — 클릭을 융합창 밖으로
    /// 떼어 놓아 <b>설명 없는 클릭 1개</b>를 만들기 때문이다.
    ///
    /// <para>그래서 이 정책에는 "임박하면 미리 연다"는 손잡이가 <b>없다</b>(있었고, 지웠다).
    /// 대신 하드 제약이 생겼다: <b>개방부터 첫 샘플까지가
    /// <see cref="FusionWindowSeconds"/> 안쪽이어야 한다.</b> 실기에서 이 값을 넘기면 Q3 판정이
    /// 다시 열린다 — 그래서 측정 가능해야 하고,
    /// <see cref="IAudioOutputDevice.LastOpenLatencySeconds"/>가 그 창구다.</para>
    ///
    /// <para><b>★ Windows는 이 전제가 아직 확인되지 않았다.</b> 48ms는 macOS AUHAL 실측이고,
    /// Windows는 <c>audiodg.exe</c> + WASAPI 공유 모드라 <b>별도 실측이 필요하다 — 그대로 옮겨
    /// 쓰면 안 된다.</b> 그 숫자가 <see cref="FusionWindowSeconds"/>를 넘으면 Q3 판정을 다시 연다.</para>
    ///
    /// ============================================================================
    /// ★ 백오프는 <b>영구 비활성이 아니다</b> — 스스로 만든 함정을 피한다
    /// ============================================================================
    /// 이 저장소에는 검증 실패 후 <b>영구 비활성</b>되는 해소기가 이미 있다
    /// (<c>WindowsLayeredHybridResolver</c>). 거기서는 그것이 옳았다 — 재시도가 해롭기 때문이다.
    /// <b>여기서는 정반대다.</b> 오디오 열기 실패의 가장 흔한 원인은 <b>일시적</b>이다:
    /// 블루투스 헤드폰이 아직 안 붙었다 / 기본 출력 장치가 방금 바뀌었다 / 다른 앱이 독점 모드다.
    /// 24시간 상주 앱에서 영구 비활성은 <b>남은 하루 종일 소리가 안 나는 것</b>을 뜻하고,
    /// 사용자는 원인을 알 방법이 없다. 그래서 사다리는 <b>상한에서 멈출 뿐 끝나지 않는다</b>.
    /// 같은 판단을 <c>SecureDesktopTrustSeconds</c>에서 이미 한 번 했다 —
    /// <b>시한 없는 정지는 이 정책이 고치려는 문제를 스스로 다시 만든다.</b>
    /// </summary>
    public static class AudioActivationPolicy
    {
        /// <summary>
        /// ★ <b>플랫폼이 소유하는 상한</b>. <c>design-sound</c>가 이 위로 올릴 수 없다.
        ///
        /// <para>30초인 근거: 이 값이 지키는 것은 "소리가 끝난 뒤 장치가 얼마나 오래 붙들려 있는가"다.
        /// 실제 절전 노출은 규칙 (1)(2)가 닫아 주지만, 그 입력이 어떤 이유로 갱신되지 않는
        /// 상황에서도 <b>최악의 노출을 30초로 못박기 위한 다중 방어</b>다. 최단 디스플레이 슬립
        /// 120초의 4분의 1이라, 이 상한만으로도 슬립 판정 전에 어서션이 풀린다.</para>
        /// </summary>
        public const float MaxLingerSeconds = 30f;

        /// <summary>
        /// ★ <b><c>design-sound</c> 확정값(2026-09-02 Q2)</b> — 자리표시자가 아니다.
        /// 기준시각은 재생 <b>완료</b>다(클래스 문서 2절).
        /// </summary>
        public const float DefaultLingerSeconds = 3.0f;

        /// <summary>
        /// ★ <b>트랜지언트 융합창</b>(design-sound Q3). 개방 클릭이 우리 소리의 어택에 흡수되는
        /// 한계. <b>개방 → 첫 샘플</b>이 이 안쪽이어야 하고, 넘으면 <b>설명 없는 클릭</b>이 된다.
        /// macOS 실측 콜드 48ms는 안쪽이다. <b>Windows는 미측정</b>.
        /// </summary>
        public const float FusionWindowSeconds = 0.050f;

        /// <summary>
        /// ★ 장치를 닫은 뒤 절전 어서션이 실제로 풀릴 때까지의 지연(실측 상한).
        /// <c>kAudioDevicePropertyDeviceIsRunning</c>이 0으로 돌아오는 데 걸린 시간이며,
        /// 어서션 보유 예산 계산에 반드시 더한다.
        /// </summary>
        public const float AssertionReleaseLagSeconds = 0.3f;

        /// <summary>
        /// ★ <b>첫 샘플 DC 스텝 방지</b>(design-sound 자백 항목). 상시 개방에서는 안 걸리지만
        /// 온디맨드에서는 첫 샘플이 <b>갓 열린 스트림</b>에 얹혀 DC 스텝 = 클릭이 된다.
        /// 모든 클립은 <b>첫 샘플 = 0</b>에서 시작해 이 시간 동안 페이드인해야 한다.
        /// 강제 지점은 <see cref="IAudioOutputDevice.TrySubmit"/>를 채우는 쪽이다.
        /// </summary>
        public const float MinimumFadeInSeconds = 0.0015f;

        /// <summary>
        /// 연속 열기 실패 <c>n</c>회 뒤 다음 시도까지 기다리는 초. 마지막 칸에서 <b>멈추고 반복</b>한다
        /// (영구 비활성 아님 — 클래스 문서 참고).
        /// </summary>
        public static readonly float[] OpenRetryBackoffSeconds = { 0f, 1f, 5f, 30f, 300f };

        /// <summary>
        /// 한 틱의 판정. <b>같은 입력에 같은 출력</b>이고 상태를 갖지 않는다 — 상태는 전부 호출자가 들고
        /// <see cref="AudioActivationInputs"/>로 넣는다. 그래야 EditMode가 24시간짜리 시나리오를
        /// 초 단위로 접어서 검증할 수 있다.
        /// </summary>
        public static AudioActivationVerdict Evaluate(AudioActivationInputs inputs)
        {
            // ---- (1) 비침해 우선 — 소리가 침해가 되는 구간에서는 무조건 닫는다 --------------------
            //     ★ 여기에 "자리 비움"과 "디스플레이 슬립"은 **일부러 없다**(클래스 문서 1절).
            //        넣으면 focus.complete가 100% 죽는다 — 이 기능이 존재하는 이유가 그 소리다.
            if (inputs.ShellForbidsAudio)
            {
                return inputs.DeviceOpen
                    ? new AudioActivationVerdict(AudioActivationAction.Close,
                        "셸이 소리를 금지하는 구간(세션 잠금 / 남의 전체화면) — 어서션을 즉시 푼다.")
                    : new AudioActivationVerdict(AudioActivationAction.None,
                        "셸이 소리를 금지하는 구간 — 열지 않는다.");
            }

            // ---- (2) design-sound의 묵음 판정을 그대로 따른다 --------------------------------------
            //     면제(focus.* 3키의 M3 면제 등)는 그쪽 규칙 안에서 이미 반영돼 넘어온다.
            //     여기서 다시 판정하면 두 곳이 갈라지고, 한쪽만 바뀌는 사고가 난다.
            if (inputs.SuppressedBySilencePolicy)
            {
                return inputs.DeviceOpen
                    ? new AudioActivationVerdict(AudioActivationAction.Close,
                        "묵음 정책이 지금 소리를 막는다(사용자 OFF 포함) — 기본 OFF와 비용 0이 맞물린다.")
                    : new AudioActivationVerdict(AudioActivationAction.None,
                        "묵음 정책이 지금 소리를 막는다 — 장치를 열 이유가 없다.");
            }

            // ---- (3) 열려 있다: 재생이 **끝나고** 잔류가 지나면 닫는다 -----------------------------
            if (inputs.DeviceOpen)
            {
                if (inputs.PlaybackActive)
                {
                    return new AudioActivationVerdict(AudioActivationAction.None,
                        "재생 중 — 열어 둔다(여기서 닫으면 소리 꼬리가 잘린다).");
                }

                float linger = ClampLinger(inputs.LingerSeconds);
                if (inputs.SecondsSinceLastPlaybackEnded >= linger)
                {
                    return new AudioActivationVerdict(AudioActivationAction.Close,
                        "재생 완료로부터 " + inputs.SecondsSinceLastPlaybackEnded.ToString("F1") +
                        "초 경과(잔류 " + linger.ToString("F1") + "초) — 닫는다.");
                }

                return new AudioActivationVerdict(AudioActivationAction.None,
                    "잔류 구간 — 곧 다시 울릴 수 있어 열어 둔다.");
            }

            // ---- (4) 닫혀 있다: 지금 울려야 하면 연다. 단 백오프를 지킨다 --------------------------
            //     ★ "곧 울릴 것 같다"로는 열지 않는다 — 미리 열면 개방 클릭이 융합창 밖으로
            //        떨어져 나가 오히려 나쁘다(클래스 문서 3절).
            if (!inputs.PlaybackActive)
            {
                return new AudioActivationVerdict(AudioActivationAction.None,
                    "조용하다 — 닫힌 채로 둔다(상주 비용 0).");
            }

            float wait = BackoffSecondsFor(inputs.ConsecutiveOpenFailures);
            if (wait > 0f && inputs.SecondsSinceLastOpenAttempt < wait)
            {
                return new AudioActivationVerdict(AudioActivationAction.None,
                    "열기 실패 " + inputs.ConsecutiveOpenFailures + "회 — " +
                    wait.ToString("F0") + "초 백오프 중(영구 비활성 아님, 반드시 다시 시도한다).");
            }

            return new AudioActivationVerdict(AudioActivationAction.Open,
                "재생할 소리가 있다 — 지금 연다(개방 클릭이 어택에 흡수되도록 미리 열지 않는다).");
        }

        /// <summary><c>design-sound</c>가 고른 잔류 시간을 <b>플랫폼 상한으로 자른다</b>.
        /// 음수는 0(= 소리가 끝나는 즉시 닫기)으로 본다.</summary>
        public static float ClampLinger(float requestedSeconds)
        {
            if (float.IsNaN(requestedSeconds)) return DefaultLingerSeconds;
            if (requestedSeconds < 0f) return 0f;
            return requestedSeconds > MaxLingerSeconds ? MaxLingerSeconds : requestedSeconds;
        }

        /// <summary>연속 실패 횟수에 대응하는 대기 초. 사다리 끝에서 <b>멈추고 반복</b>한다.</summary>
        public static float BackoffSecondsFor(int consecutiveFailures)
        {
            if (consecutiveFailures <= 0) return 0f;
            int index = consecutiveFailures < OpenRetryBackoffSeconds.Length
                ? consecutiveFailures
                : OpenRetryBackoffSeconds.Length - 1;
            return OpenRetryBackoffSeconds[index];
        }

        /// <summary>
        /// ★ Q3 판정의 <b>유일한 전제</b>를 실기에서 되물을 수 있게 하는 판정기.
        /// 개방부터 첫 샘플까지가 융합창 안쪽인가 — 넘으면 개방 클릭이 <b>독립된 클릭</b>으로 들린다.
        ///
        /// <para>Windows는 이 값이 <b>미측정</b>이다. 실기에서 이것이 false를 내면
        /// <c>design-sound</c>의 "48ms 허용" 판정을 다시 열어야 한다 — 조용히 넘기면 안 된다.</para>
        /// </summary>
        public static bool IsOpenLatencyWithinFusionWindow(float openToFirstSampleSeconds)
            => openToFirstSampleSeconds >= 0f && openToFirstSampleSeconds <= FusionWindowSeconds;

        /// <summary>
        /// 한 번의 소리가 절전 어서션을 붙드는 <b>최악의 시간</b>(초).
        /// <b>검산</b>: 클립 0.800 + 잔류 3.0 + 해제 지연 0.3 = <b>4.100초</b>
        /// = 최단 디스플레이 슬립 120초의 3.4%.
        /// </summary>
        public static float EstimateMaxAssertionHoldSeconds(float clipSeconds, float lingerSeconds)
            => (clipSeconds < 0f ? 0f : clipSeconds) + ClampLinger(lingerSeconds) + AssertionReleaseLagSeconds;

        /// <summary>
        /// ★ <c>design-sound</c>에게 넘기는 <b>비용 함수</b>. "잔류를 몇 초로 할까"를 취향이 아니라
        /// 숫자로 고르게 한다. 하루 <paramref name="eventsPerDay"/>건이 각각
        /// <paramref name="clipSeconds"/>만큼 울릴 때의 장치 개방 점유율(0~1).
        ///
        /// <para><b>검산</b>: 하루 3건 / 클립 0.8초 / 잔류 3.0초
        /// → 3 × 3.8 / 86400 = <b>0.000132</b>(0.0132%) = 상시 개방 대비 <b>1/7,579</b>.</para>
        /// </summary>
        public static float EstimateDailyOpenDutyCycle(int eventsPerDay, float clipSeconds, float lingerSeconds)
        {
            if (eventsPerDay <= 0) return 0f;
            float perEvent = (clipSeconds < 0f ? 0f : clipSeconds) + ClampLinger(lingerSeconds);
            float duty = eventsPerDay * perEvent / 86400f;
            return duty > 1f ? 1f : duty;
        }
    }

    /// <summary>
    /// 한 틱의 입력. <b>정책이 상태를 들지 않게</b> 하려고 전부 여기로 모은다 — 그래야 EditMode가
    /// 임의의 시나리오를 초 단위로 합성할 수 있고, 실기 없이도 24시간 경로를 검증할 수 있다.
    /// </summary>
    public readonly struct AudioActivationInputs
    {
        /// <summary>
        /// ★ 지금 울려야 할(또는 울리고 있는) 소리가 있다. <b>이 정책의 유일한 개방 트리거</b>다.
        ///
        /// <para><b>여기에 입력 이벤트를 섞지 마라</b> — <c>focus.complete</c>는 마지막 입력이
        /// 최대 25분 전이라, 입력 게이트를 붙이면 이 기능이 존재하는 이유인 그 소리가 100% 죽는다
        /// (<c>AudioActivationPolicy</c> 클래스 문서 1절).</para>
        ///
        /// <para>재생이 <b>끝날 때까지</b> true여야 한다. 시작 시점에만 세우면 잔류 계산이
        /// 시작 기준이 되어 성취음 800ms의 <b>꼬리가 잘린다</b>.</para>
        /// </summary>
        public readonly bool PlaybackActive;

        /// <summary>
        /// 마지막 클립이 <b>끝난</b> 뒤 지난 초. ★ <b>시작이 아니라 종료 기준</b>이다(Q2).
        /// <see cref="PlaybackActive"/>가 true인 동안에는 읽히지 않는다.
        /// </summary>
        public readonly float SecondsSinceLastPlaybackEnded;

        /// <summary>
        /// ★ <b>소리가 침해가 되거나 물리적으로 무의미한 구간</b>인가 — 세션 잠금 / 남의 전체화면.
        /// 어떤 소리도 이것을 뒤집을 수 없다(원칙 2).
        ///
        /// <para><b>자리 비움과 디스플레이 슬립은 여기에 넣지 않는다.</b> 넣는 순간
        /// <c>focus.complete</c>가 구조적으로 사라진다 — <c>SILENCE_POLICY</c> §3-8 M3가
        /// <c>focus.*</c> 3키를 면제하는 이유가 정확히 그것이다. 이 결정은 절전 예산을
        /// 한 번의 소리당 최대 4.1초 노출로 사는 것이며, 그 값은
        /// <see cref="AudioActivationPolicy.EstimateMaxAssertionHoldSeconds"/>로 계산된다.</para>
        /// </summary>
        public readonly bool ShellForbidsAudio;

        /// <summary>
        /// <c>design-sound</c>의 묵음 정책이 <b>지금 이 소리를</b> 막는가(사용자 OFF, 시간대 규칙,
        /// 자리 비움 M3와 그 면제까지 전부 반영된 <b>결과</b>). 여기서 다시 판정하지 않는다 —
        /// 두 곳에서 판정하면 한쪽만 바뀌는 사고가 난다.
        /// </summary>
        public readonly bool SuppressedBySilencePolicy;

        /// <summary>장치가 지금 열려 있는가. <see cref="IAudioOutputDevice.TryReadIsOpen"/>의 결과이며,
        /// <b>조회에 실패했으면 true(열려 있다고 가정)</b>를 넣는다 — 모르면 닫는 쪽이 보수적이다.</summary>
        public readonly bool DeviceOpen;

        /// <summary>연속 열기 실패 횟수(성공하면 0으로 되돌린다).</summary>
        public readonly int ConsecutiveOpenFailures;

        /// <summary>마지막 열기 <b>시도</b>로부터 지난 초(성공/실패 무관).</summary>
        public readonly float SecondsSinceLastOpenAttempt;

        /// <summary><c>design-sound</c>가 확정한 잔류 초(3.0).
        /// <see cref="AudioActivationPolicy.MaxLingerSeconds"/>로 잘린다.</summary>
        public readonly float LingerSeconds;

        public AudioActivationInputs(
            bool playbackActive,
            float secondsSinceLastPlaybackEnded,
            bool shellForbidsAudio,
            bool suppressedBySilencePolicy,
            bool deviceOpen,
            int consecutiveOpenFailures,
            float secondsSinceLastOpenAttempt,
            float lingerSeconds)
        {
            PlaybackActive = playbackActive;
            SecondsSinceLastPlaybackEnded = secondsSinceLastPlaybackEnded;
            ShellForbidsAudio = shellForbidsAudio;
            SuppressedBySilencePolicy = suppressedBySilencePolicy;
            DeviceOpen = deviceOpen;
            ConsecutiveOpenFailures = consecutiveOpenFailures;
            SecondsSinceLastOpenAttempt = secondsSinceLastOpenAttempt;
            LingerSeconds = lingerSeconds;
        }
    }

    /// <summary>판정 결과 + <b>사람이 읽는 사유</b>. 사유를 함께 돌려주는 이유는 로그에서
    /// "왜 지금 열렸나/닫혔나"를 되짚을 수 있어야 실기에서 이 정책을 반증할 수 있기 때문이다.</summary>
    public readonly struct AudioActivationVerdict
    {
        public readonly AudioActivationAction Action;
        public readonly string Reason;

        public AudioActivationVerdict(AudioActivationAction action, string reason)
        {
            Action = action;
            Reason = reason ?? string.Empty;
        }

        public override string ToString() => Action + " — " + Reason;
    }
}
