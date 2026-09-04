namespace StickMate.Platform
{
    /// <summary>한 틱에 하나만 나오는 결론. 「지금 상태를 어떻게 바꾸라」는 명령이지 상태 그 자체가 아니다.</summary>
    public enum AudioReactiveDanceAction
    {
        /// <summary>아무 것도 하지 않는다(춤을 안 추고 있고, 시작할 이유도 없다).</summary>
        None = 0,
        /// <summary>지금 춤을 시작하라.</summary>
        Start = 1,
        /// <summary>이미 추고 있고 계속 춰라.</summary>
        Continue = 2,
        /// <summary>지금 멈춰라.</summary>
        Stop = 3,
    }

    /// <summary>게이트 내부 위상. <c>default</c>(=0)가 <b>정확히 올바른 초기 상태</b>가 되도록 배치했다.</summary>
    public enum AudioReactiveDancePhase
    {
        /// <summary>춤을 안 추는 중. 원 신호가 T₁만큼 연속 ON이면 시작한다.</summary>
        Armed = 0,
        /// <summary>추는 중.</summary>
        Dancing = 1,
        /// <summary>★ T₄(상한 이탈)로 잠긴 상태. macOS M-B 거짓 양성의 <b>탈출구</b>다.
        /// 원 신호가 T₃만큼 연속 OFF가 되어야 풀린다.</summary>
        Lockout = 2,
    }

    /// <summary>
    /// 게이트가 틱 사이에 들고 다니는 <b>전부</b>. 순수 데이터이고 <see cref="AudioReactiveDancePolicy"/>는
    /// 이것을 바꾸지 않고 <b>새 값을 돌려준다</b>(정적 가변 상태 0개 = 테스트가 서로를 오염시키지 않는다).
    ///
    /// <para><b><c>default(AudioReactiveDanceState)</c>가 올바른 시작값이다</b> —
    /// <see cref="AudioReactiveDancePhase.Armed"/> + 모든 누산 0. 별도의 초기화 함수를 두지 않는 이유는
    /// 두 벌이 생기면 반드시 갈라지기 때문이다.</para>
    /// </summary>
    public readonly struct AudioReactiveDanceState
    {
        /// <summary>지금 위상.</summary>
        public readonly AudioReactiveDancePhase Phase;

        /// <summary>★ <b>원(raw) 신호</b>가 연속 ON인 시간. T₁·T₄가 <b>이 값만</b> 본다(§19-1).</summary>
        public readonly float OnRunSeconds;

        /// <summary>★ <b>원(raw) 신호</b>가 연속 OFF인 시간. T₂(해제 판정)와 Lockout 해제가 본다.</summary>
        public readonly float OffRunSeconds;

        /// <summary>이번 춤이 시작된 뒤 흐른 시간. T₃(최소 유지)가 본다.</summary>
        public readonly float DanceElapsedSeconds;

        public AudioReactiveDanceState(AudioReactiveDancePhase phase,
            float onRunSeconds, float offRunSeconds, float danceElapsedSeconds)
        {
            Phase = phase;
            OnRunSeconds = onRunSeconds;
            OffRunSeconds = offRunSeconds;
            DanceElapsedSeconds = danceElapsedSeconds;
        }
    }

    /// <summary>한 틱의 결론 + 다음 틱에 그대로 넘길 상태 + 사람이 읽을 사유.</summary>
    public readonly struct AudioReactiveDanceVerdict
    {
        public readonly AudioReactiveDanceAction Action;

        /// <summary>진단 로그·테스트 실패 메시지용. <b>전부 <c>const</c> 리터럴</b>이라 매 틱 할당이 없다
        /// (24시간 상주 앱에서 문자열 조립은 곧 GC다).</summary>
        public readonly string Reason;

        /// <summary>다음 틱에 그대로 넣을 상태.</summary>
        public readonly AudioReactiveDanceState Next;

        public AudioReactiveDanceVerdict(AudioReactiveDanceAction action, string reason,
            AudioReactiveDanceState next)
        {
            Action = action;
            Reason = reason;
            Next = next;
        }
    }

    /// <summary>
    /// ★★★ <b>시스템 오디오 불리언 시계열 → 춤 개시/유지/종료</b> 판정 — <b>순수 함수</b>.
    /// Unity API도, <c>DllImport</c>도, 플랫폼 분기도, 정적 가변 상태도 없다.
    ///
    /// ============================================================================
    /// 왜 여기(플랫폼 중립)에 있는가
    /// ============================================================================
    /// 입력이 <b>불리언 시계열</b>뿐이라 이 규칙은 양 플랫폼에서 <b>글자 그대로 같다</b>
    /// (레벨 → 불리언 변환은 Windows 구현체가 이미 끝내고 넘긴다 —
    /// <see cref="ISystemAudioActivityProbe"/> 문서 I-13). 이 파일이 <c>Platform/MacOS/</c> 안에 있으면
    /// Windows가 물리적으로 못 부르고 그쪽 라운드가 규칙을 처음부터 다시 쓴다 —
    /// 이 저장소가 <see cref="FullscreenSuspendPolicy"/>에서 정확히 그렇게 당했다.
    /// 그리고 이 개발 머신에 Windows가 없으므로, 규칙이 중립이라야 <b>EditMode가 양 플랫폼의 판정을
    /// 실제로 검증</b>한다(20분짜리 T₄ 시나리오를 밀리초 안에 접어서 돌린다).
    ///
    /// ============================================================================
    /// ★★ 1절 — <b>T₁·T₄는 원(raw) 신호에, T₂는 「해제 판정」에만.</b> 절대 섞지 마라
    /// ============================================================================
    /// <c>design-systems</c>(<c>docs/DESIGN_SYSTEMS_STATS.md</c> §19-1)가 찾은 함정이다:
    /// <code>
    ///   알림 2회가 1.09초 간격으로 오면 (game-architect M-D 실측 그림 그대로)
    ///     T₂가 그 공백을 이어 붙여 → 병합 ON = 24.68~30.54 = 5.86초
    ///     그 5.86초가 T₁(3.0초)을 통과한다  ⇒ **알림 2회에 춤이 발동한다**
    /// </code>
    /// 그래서 <see cref="AudioReactiveDanceState.OnRunSeconds"/>는 <b>병합 없는 원 신호</b>이고,
    /// T₂는 <see cref="AudioReactiveDancePhase.Dancing"/>일 때의 종료 판정에만 쓰인다.
    /// 각 알림은 2.38초로 <b>따로</b> 세어져 둘 다 T₁ 미달 → 발동 없음.
    /// <b>두 값을 하나로 합치는 "정리"를 하지 마라 — 그 순간 알림음마다 춤춘다.</b>
    ///
    /// ============================================================================
    /// 2절 — 상태기계 (§19-1 그대로)
    /// ============================================================================
    /// <code>
    ///   Armed    : onRun  ≥ T₁                        → Dancing (danceElapsed = 0)   [Start]
    ///   Dancing  : offRun ≥ T₂  ∧  danceElapsed ≥ T₃  → Armed                        [Stop]
    ///              onRun  ≥ T₄                        → Lockout                      [Stop]
    ///   Lockout  : offRun ≥ T₃                        → Armed
    ///   (억제 입력이 참이면 위 전부보다 먼저 이긴다 — 춤 중이면 Stop, 아니면 None)
    /// </code>
    ///
    /// ============================================================================
    /// 3절 — <b>「모른다」를 불리언으로 지어내지 마라</b>
    /// ============================================================================
    /// <see cref="ISystemAudioActivityProbe.TryReadIsAudioPlaying"/>가 <c>false</c>를 돌려준 틱에는
    /// <b>이 함수를 부르지 마라.</b> 실패를 «무음»으로 접으면 <c>offRun</c>이 가짜로 늘어
    /// T₂·Lockout 해제가 <b>실제로 일어나지 않은 침묵</b> 위에서 성립한다. 그 다음 <c>deltaSeconds</c>는
    /// <b>마지막으로 성공한 조회 이후의 경과</b>를 넣는다. 실패가 길게 이어지면 상태를
    /// <c>default</c>로 되돌려라 — 낡은 누산 위에 큰 델타를 얹는 것이 더 나쁘다.
    ///
    /// ============================================================================
    /// 4절 — 이 정책이 <b>안 하는 것</b>
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>억제 조건을 스스로 판단하지 않는다.</b> 숨김·집중 모드 판정은
    ///     <c>Core/AudioReactiveDanceGate</c>가 하고, 그 결과가 <c>suppressed</c>로 들어온다.
    ///     판정을 두 벌로 만들면 반드시 갈라진다.</item>
    ///   <item><b>박자를 모른다.</b> 우리는 샘플을 안 읽으므로 BPM이 없다 — 춤은 <b>음악과 동기되지
    ///     않는 고정 박자</b>다(§11-5 (나)). 이건 결함이 아니라 캡처를 안 하기로 한 대가다.</item>
    ///   <item><b>상태 전이를 실행하지 않는다.</b> <c>Start</c>/<c>Stop</c>을 실제 상태로 옮기는 것은
    ///     <c>design-motion</c>이 정한 경로의 몫이다(이번 라운드 범위 밖).</item>
    /// </list>
    /// </summary>
    public static class AudioReactiveDancePolicy
    {
        // ====================================================================
        // 확정 임계값 — docs/DESIGN_SYSTEMS_STATS.md §19-2 (design-systems, 2026-09-03)
        // ★ 숫자를 다른 파일에 베끼지 마라. 테스트도 이 상수를 참조한다(CLAUDE.md).
        // ====================================================================

        /// <summary>
        /// <b>T₁ 개시 지연</b> — 원 신호가 이만큼 <b>연속</b> ON이어야 춤이 시작된다.
        /// <para>거를 대상은 <b>알림음 1회</b>다. 실측 ON 지속 <see cref="MeasuredNotificationOnSeconds"/>를
        /// 넘어야 하고(여유 26%), 스트림 오버행 0.38초를 빼면 <b>3.1초까지의 소리를 거른다</b>.
        /// macOS 기본 알림음은 전부 2초 미만이라 덮인다.</para>
        /// </summary>
        public const float StartDelaySeconds = 3.0f;

        /// <summary>
        /// <b>T₂ 해제 지연</b> — <b>이미 춤추는 중일 때만</b> 쓰인다. 원 신호가 이만큼 연속 OFF여야
        /// 종료를 검토한다.
        /// <para>실측 곡 사이 공백 <see cref="MeasuredTrackGapSeconds"/>의 2.75배이고 트랙 전환
        /// 버퍼링(2~3초)까지 덮는다. 대가: 음악이 진짜 끝난 뒤 <b>최대 3.0초 더 춤춘다</b>.</para>
        /// <para>★ <b>이 값을 개시 판정에 쓰지 마라</b> — 위 1절이 그 함정 전체다.</para>
        /// </summary>
        public const float ReleaseDelaySeconds = 3.0f;

        /// <summary>
        /// <b>T₃ 최소 유지</b> — 춤을 시작했으면 최소 이만큼은 유지한다(억제 입력은 예외로 이긴다).
        /// <para>★ 지어낸 값이 아니다 — <see cref="MotionEpisodeMinSeconds"/>(design-motion의 최소
        /// 에피소드 길이) <b>그 자체</b>다. 더 작으면 게이트가 에피소드를 중간에 자르고,
        /// <see cref="MotionEpisodeMaxSeconds"/>로 잡으면 짧은 에피소드에 억지 연장이 생긴다.</para>
        /// </summary>
        public const float MinimumHoldSeconds = 8.0f;

        /// <summary>
        /// ★ <b>T₄ 상한 이탈</b> — 원 신호가 <b>한 번도 끊기지 않고</b> 이만큼 ON이면 춤을 <b>멈춘다</b>.
        ///
        /// <para><b>이 값이 없으면 macOS에서 영원히 춤춘다.</b> M-B 실측이 확정한 대로 macOS 신호는
        /// "소리"가 아니라 <b>"스트림 개방"</b>이라, 무음 스트림을 여는 앱 <b>하나</b>만 상주해도
        /// 신호가 고착된다. 그건 버그가 아니라 <b>설계된 무한 춤</b>이 된다.</para>
        ///
        /// <para><b>축은 「세션 길이」가 아니라 「무공백 구간 길이」다</b>(§19-2). 진짜 음악 재생은
        /// 트랙이 바뀔 때마다 OFF가 뜨고(실측 1.09초) 그때 <c>onRun</c>이 리셋되므로,
        /// <b>앨범·플레이리스트는 몇 시간이든 계속 춤춘다.</b> 여기 걸리는 것은 «공백 없는 20분»뿐이다
        /// (고착 스트림·팟캐스트·통화·영화). 20분은 어떤 단일 트랙도 끊지 않는다.</para>
        ///
        /// <para><b>정직하게</b>: 「가장 긴 단일 트랙 20분」은 <b>음악 장르 통계이지 이 저장소의
        /// 실측이 아니다</b>(§19-5).</para>
        /// </summary>
        public const float StuckSignalCeilingSeconds = 1200f;

        /// <summary>
        /// Lockout(T₄로 잠김)이 풀리는 데 필요한 <b>연속 OFF</b> 시간.
        /// <para>★ <b>일부러 <see cref="MinimumHoldSeconds"/>를 재사용한다</b> — 새 노브를 만들지 않기
        /// 위해서다(§19-1). 별도 값으로 가르고 싶어지면 그때 근거를 여기 적어라.</para>
        /// </summary>
        public const float LockoutReleaseSeconds = MinimumHoldSeconds;

        // ====================================================================
        // 입력 앵커 — 전부 남이 실측한 값이다. 위 임계값의 유도 근거이자,
        // 테스트가 "T₁ > 알림 1회 길이" 같은 관계를 숫자 없이 검증하기 위한 참조점이다.
        // ====================================================================

        /// <summary>곡 사이 공백 실측(game-architect M-D: <c>t=27.06 OFF → 28.15 ON</c>).
        /// <see cref="ReleaseDelaySeconds"/>는 반드시 이보다 커야 한다.</summary>
        public const float MeasuredTrackGapSeconds = 1.09f;

        /// <summary>~2초짜리 오디오 1회의 <b>ON 지속</b> 실측(M-D: <c>t=24.68 → 27.06</c>).
        /// 오디오 길이보다 0.38초 긴 것이 <b>스트림 오버행</b>이다.
        /// <see cref="StartDelaySeconds"/>는 반드시 이보다 커야 한다 — 그래야 알림음 1회를 거른다.</summary>
        public const float MeasuredNotificationOnSeconds = 2.38f;

        /// <summary>M-C가 전제한 폴링 주기(2Hz). macOS는 푸시가 가능해 이보다 좋다.
        /// <para>★ 한계(정직하게): 0.5초 간격에서 <b>1.0초 미만 공백</b>은 통째로 놓칠 수 있고,
        /// 그러면 <c>onRun</c>이 리셋되지 않아 T₄가 늦게 걸린다 = <b>더 오래 춤춘다</b>.
        /// 안전한 방향의 실패다(춤이 안 나오는 쪽이 아니다).</para></summary>
        public const float ReferencePollIntervalSeconds = 0.5f;

        /// <summary>design-motion 에피소드 최소 길이(MOTION_SPEC §26). <see cref="MinimumHoldSeconds"/>의 출처.</summary>
        public const float MotionEpisodeMinSeconds = 8.0f;

        /// <summary>design-motion 에피소드 최대 길이. T₃를 이 값으로 잡으면 짧은 에피소드가 억지로 늘어난다.</summary>
        public const float MotionEpisodeMaxSeconds = 16.0f;

        // ==================== 사유 문자열 (전부 const — 매 틱 할당 0) ====================

        internal const string ReasonSuppressed = "억제 입력이 참이다(숨김/집중 모드 등) — 춤은 가장 낮은 우선순위다";
        internal const string ReasonSuppressedStop = "억제 입력이 춤 도중에 참이 됐다 — T₃(최소 유지)보다 억제가 이긴다";
        internal const string ReasonStart = "원 신호가 T₁만큼 연속 ON이다 — 알림음 1회로는 여기 못 온다";
        internal const string ReasonWaitingStart = "아직 T₁ 미달이다 — 원 신호 연속 ON이 더 필요하다";
        internal const string ReasonContinue = "계속 춘다 — 종료 조건(T₂ 연속 OFF + T₃ 최소 유지)이 아직 다 차지 않았다";
        internal const string ReasonStopReleased = "원 신호가 T₂만큼 연속 OFF이고 T₃(최소 유지)도 찼다 — 음악이 끝났다";
        internal const string ReasonStopCeiling = "원 신호가 T₄만큼 «한 번도 끊기지 않고» ON이다 — 고착 신호로 보고 멈춘다(M-B 탈출구)";
        internal const string ReasonLockoutHeld = "T₄ 고착 잠금 중이다 — 연속 OFF가 T₃만큼 쌓여야 풀린다";
        internal const string ReasonLockoutReleased = "고착 잠금이 풀렸다 — 다시 T₁부터 센다";

        /// <summary>
        /// 한 틱을 평가한다. <b>같은 입력이면 언제나 같은 출력</b>이고, 어떤 정적 상태도 읽거나 쓰지 않는다.
        /// </summary>
        /// <param name="state">직전 틱이 돌려준 상태. 처음에는 <c>default</c>를 넣는다.</param>
        /// <param name="isAudioPlaying">★ <b>원 신호</b>. 어떤 병합도 하지 않은 값을 그대로 넣는다.
        /// 조회에 실패한 틱은 이 함수를 <b>부르지 않는다</b>(위 3절).</param>
        /// <param name="deltaSeconds">직전 성공 조회로부터의 경과(초). 0 이하·NaN은 0으로 본다.</param>
        /// <param name="suppressed">지금 춤을 막아야 하는가(<c>Core/AudioReactiveDanceGate</c>의 결론).
        /// <b>매 틱 다시 계산한 값</b>이어야 한다 — 발동 시점 1회만 보면 «집중 모드를 켰는데 계속
        /// 춤춘다»가 된다(§11-11-3).</param>
        public static AudioReactiveDanceVerdict Evaluate(
            AudioReactiveDanceState state, bool isAudioPlaying, float deltaSeconds, bool suppressed)
        {
            // NaN은 어떤 비교에도 false를 내므로 이 형태가 NaN까지 함께 막는다.
            float dt = deltaSeconds > 0f ? deltaSeconds : 0f;

            // ---- (1) 원 신호 누산. T₁·T₄가 보는 유일한 값이다(1절) ----
            float onRun = isAudioPlaying ? state.OnRunSeconds + dt : 0f;
            float offRun = isAudioPlaying ? 0f : state.OffRunSeconds + dt;

            AudioReactiveDancePhase phase = state.Phase;
            float danceElapsed = phase == AudioReactiveDancePhase.Dancing
                ? state.DanceElapsedSeconds + dt
                : state.DanceElapsedSeconds;

            // ---- (2) 고착 잠금 해제 — 억제 여부와 무관한 순수 장부다 ----
            if (phase == AudioReactiveDancePhase.Lockout && offRun >= LockoutReleaseSeconds)
            {
                phase = AudioReactiveDancePhase.Armed;
                danceElapsed = 0f;
                // 여기서 바로 Start를 낼 수는 없다 — offRun이 찼다는 것은 onRun이 0이라는 뜻이다.
                return Verdict(AudioReactiveDanceAction.None, ReasonLockoutReleased,
                    phase, onRun, offRun, danceElapsed);
            }

            // ---- (3) T₄ 상한 이탈. 춤 중이든 아니든 원 신호가 고착이면 잠근다 ----
            if (phase != AudioReactiveDancePhase.Lockout && onRun >= StuckSignalCeilingSeconds)
            {
                bool wasDancing = phase == AudioReactiveDancePhase.Dancing;
                phase = AudioReactiveDancePhase.Lockout;
                danceElapsed = 0f;
                return Verdict(
                    wasDancing ? AudioReactiveDanceAction.Stop : AudioReactiveDanceAction.None,
                    ReasonStopCeiling, phase, onRun, offRun, danceElapsed);
            }

            // ---- (4) 억제 — 위 장부보다는 뒤, 아래 발동보다는 앞. T₃보다 이긴다 ----
            if (suppressed)
            {
                if (phase == AudioReactiveDancePhase.Dancing)
                {
                    phase = AudioReactiveDancePhase.Armed;
                    danceElapsed = 0f;
                    return Verdict(AudioReactiveDanceAction.Stop, ReasonSuppressedStop,
                        phase, onRun, offRun, danceElapsed);
                }

                return Verdict(AudioReactiveDanceAction.None, ReasonSuppressed,
                    phase, onRun, offRun, danceElapsed);
            }

            // ---- (5) 위상별 판정 ----
            switch (phase)
            {
                case AudioReactiveDancePhase.Armed:
                    if (onRun >= StartDelaySeconds)
                    {
                        return Verdict(AudioReactiveDanceAction.Start, ReasonStart,
                            AudioReactiveDancePhase.Dancing, onRun, offRun, 0f);
                    }
                    return Verdict(AudioReactiveDanceAction.None, ReasonWaitingStart,
                        phase, onRun, offRun, 0f);

                case AudioReactiveDancePhase.Dancing:
                    if (offRun >= ReleaseDelaySeconds && danceElapsed >= MinimumHoldSeconds)
                    {
                        return Verdict(AudioReactiveDanceAction.Stop, ReasonStopReleased,
                            AudioReactiveDancePhase.Armed, onRun, offRun, 0f);
                    }
                    return Verdict(AudioReactiveDanceAction.Continue, ReasonContinue,
                        phase, onRun, offRun, danceElapsed);

                default:   // Lockout — (2)에서 안 풀렸으면 계속 잠겨 있다.
                    return Verdict(AudioReactiveDanceAction.None, ReasonLockoutHeld,
                        phase, onRun, offRun, 0f);
            }
        }

        private static AudioReactiveDanceVerdict Verdict(AudioReactiveDanceAction action, string reason,
            AudioReactiveDancePhase phase, float onRun, float offRun, float danceElapsed)
            => new AudioReactiveDanceVerdict(action, reason,
                new AudioReactiveDanceState(phase, onRun, offRun, danceElapsed));
    }
}
