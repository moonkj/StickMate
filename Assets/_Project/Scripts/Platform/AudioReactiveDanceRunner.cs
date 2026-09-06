namespace StickMate.Platform
{
    /// <summary>
    /// ★★★ <b>프로브(사실 조회) ↔ 정책(순수 판정) 사이의 배선</b> — 2026-09-06 신설.
    /// Unity API를 <b>한 개도</b> 쓰지 않는다(<c>using UnityEngine</c> 없음). 그래서 20분짜리 T₄
    /// 시나리오도 EditMode에서 밀리초 안에 접어서 돌릴 수 있다.
    ///
    /// ============================================================================
    /// 이 클래스가 존재하는 이유 — <b><see cref="AudioReactiveDancePolicy"/> 3절이 아직 코드가 아니었다</b>
    /// ============================================================================
    /// 그 정책의 3절은 <i>"조회에 실패한 틱에는 이 함수를 부르지 마라 / 그 다음 <c>deltaSeconds</c>는
    /// 마지막으로 성공한 조회 이후의 경과를 넣어라 / 실패가 길게 이어지면 상태를 <c>default</c>로
    /// 되돌려라"</i>라고 <b>세 가지를 요구</b>한다. 이것은 순수 함수 밖의 규칙이라 정책 파일이 스스로
    /// 지킬 수 없고, 2026-09-03에는 <b>지키는 코드가 아무 데도 없었다</b>(호출부 0건).
    /// 그 규칙을 <c>Update()</c> 안에 흩어 놓으면 다음 사람이 못 보고, 테스트도 씬 없이는 못 짠다.
    /// ⇒ <b>여기 한 곳에 모으고, Unity에서 떼어낸다.</b>
    ///
    /// ============================================================================
    /// ★★ 왜 실패를 <c>false</c>(=무음)로 접으면 안 되는가 — 조용한 초록의 교과서
    /// ============================================================================
    /// <code>
    ///   접었을 때:  조회 실패가 이어짐 → offRun 이 가짜로 계속 증가
    ///               → T₂(3초) 충족 → "음악이 끝났다"고 Stop
    ///               → Lockout 해제(T₃)까지 가짜 침묵 위에서 성립
    ///   화면에 보이는 것: **아무 일도 안 일어난다**. 로그도 «정상 종료»라고 말한다.
    /// </code>
    /// 즉 <b>«프로브가 고장났다»가 «음악이 안 나온다»와 똑같이 생긴다.</b> 그래서 실패한 틱은
    /// 정책을 <b>부르지 않고</b>, 대신 <see cref="LastReadOk"/>로 그 사실을 밖에 드러낸다.
    ///
    /// ============================================================================
    /// ★ 폴링 주기 — <b>이 파일이 숫자를 고르지 않는다</b>
    /// ============================================================================
    /// <see cref="PollIntervalSeconds"/>는 <see cref="AudioReactiveDancePolicy.ReferencePollIntervalSeconds"/>
    /// 를 <b>그대로 참조</b>한다(베끼지 않는다 — CLAUDE.md). 그 상수는 design-systems가 §19-2에서
    /// 임계값을 유도할 때 <b>전제로 깔았던 값</b>이라, 여기서 다른 값을 쓰면 T₁~T₄의 유도 근거가
    /// 조용히 무너진다. 근거는 그 상수의 문서에 있고 요지는 둘이다:
    /// <list type="bullet">
    ///   <item><b>더 느리면 안 되는 이유</b> — 거를 대상이 아니라 <b>세야 할 대상</b>이 곡 사이 공백
    ///     (<see cref="AudioReactiveDancePolicy.MeasuredTrackGapSeconds"/> = 1.09초 실측)이다.
    ///     그 공백 안에 표본이 최소 2개는 떨어져야 <c>onRun</c>이 리셋된다 ⇒ 주기 ≤ 1.09/2 ≈ 0.545초.
    ///     못 맞추면 공백을 통째로 놓쳐 <c>onRun</c>이 안 끊기고, T₄(20분)가 늦게 걸려
    ///     <b>더 오래 춤춘다</b>.</item>
    ///   <item><b>더 빠를 이유가 없는 이유</b> — macOS M-C 실측이 호출당 30.4µs다. 2Hz면 CPU 0.006%.
    ///     24시간 상주 앱에서 이보다 잦게 도는 것은 <b>사는 것 없이 배터리만 쓰는 일</b>이고,
    ///     T₁이 3.0초라 반응 지연은 어차피 주기가 아니라 T₁이 지배한다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 이 클래스가 <b>안 하는 것</b>
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>억제를 판단하지 않는다.</b> <c>suppressed</c>는 <c>Core/AudioReactiveDanceGate</c>가
    ///     매 틱 계산해 넣는다. 판정을 두 벌로 만들면 반드시 갈라진다.</item>
    ///   <item><b>상태 전이를 실행하지 않는다.</b> 「어떤 춤을 어떤 박자로」는 design-motion 사양이고,
    ///     그것을 <c>StickmanStateId</c>로 옮기는 것은 coder의 다음 라운드다.
    ///     여기는 <b>열렸다/닫혔다</b>까지만 말한다(<see cref="IsGateOpen"/>).</item>
    /// </list>
    /// </summary>
    public sealed class AudioReactiveDanceRunner
    {
        /// <summary>
        /// 프로브를 얼마나 자주 부를 것인가. ★ <b>정책이 전제한 값을 그대로 쓴다</b>(위 문단).
        /// 숫자를 여기 적으면 두 벌이 되고, 한쪽만 바뀌는 날이 온다.
        /// </summary>
        public static float PollIntervalSeconds => AudioReactiveDancePolicy.ReferencePollIntervalSeconds;

        /// <summary>
        /// ★ 조회가 이만큼 <b>연속으로 실패</b>하면 누산을 통째로 버리고 <see cref="AudioReactiveDanceState"/>를
        /// <c>default</c>로 되돌린다(= 춤도 닫는다).
        ///
        /// <para><b>새 노브를 만들지 않았다</b> — <see cref="AudioReactiveDancePolicy.ReleaseDelaySeconds"/>
        /// (T₂)를 그대로 쓴다. T₂는 이 저장소가 이미 확정한 <i>"긍정 신호 없이 이만큼 지나면 춤을
        /// 끝낸다"</i>의 길이다. 「모른다」는 「무음」과 다르지만 <b>자동 발동 경로에서 둘의 안전한
        /// 방향은 같다</b> — 「모르면 안 한다」(<c>Core/AudioReactiveDanceGate</c> 클래스 문서).
        /// 별도 값으로 가르고 싶어지면 그때 근거를 여기 적어라.</para>
        /// </summary>
        public static float BlindResetSeconds => AudioReactiveDancePolicy.ReleaseDelaySeconds;

        // ==================== 사유 문자열 (전부 const — 매 틱 할당 0) ====================
        // 정책 파일과 같은 관례다: 24시간 상주 앱에서 문자열 조립은 곧 GC다.

        internal const string ReasonNoProbe =
            "이 플랫폼/빌드에 시스템 오디오 감지 배선이 없다 — 게이트는 영원히 닫힌 채가 정상이다";
        internal const string ReasonBlindHold =
            "조회에 실패했다(«모른다») — 정책을 부르지 않고 직전 판정을 그대로 유지한다";
        internal const string ReasonBlindReset =
            "조회 실패가 T₂만큼 이어졌다 — 낡은 누산을 버리고 닫는다(가짜 침묵 위에 판정을 세우지 않는다)";
        internal const string ReasonBlindSuppressed =
            "조회에 실패한 채로 억제 입력이 참이 됐다 — 신호를 모르는 상태에서는 억제가 무조건 이긴다";
        internal const string ReasonStaleDelta =
            "마지막 성공 조회로부터 T₂를 넘겨 경과했다 — 낡은 누산 위에 큰 델타를 얹지 않고 처음부터 센다";

        private readonly ISystemAudioActivityProbe _probe;

        private AudioReactiveDanceState _state;

        /// <summary>마지막으로 <b>성공한</b> 조회 이후 흐른 시간. 정책의 <c>deltaSeconds</c>가 이 값이다
        /// (틱 간격이 아니다 — 3절).</summary>
        private float _secondsSinceLastGoodRead;

        /// <summary>지금 춤 게이트가 열려 있는가. ★ <b>coder가 이어받을 신호</b>이지만 보통은
        /// <see cref="AudioReactiveDanceDirector.IsDanceGateOpen"/>(정적)을 읽는다.</summary>
        public bool IsGateOpen { get; private set; }

        /// <summary>이번 틱의 결론. 진단·테스트용이고 <c>None</c>이 "아무 일도 없음"이다.</summary>
        public AudioReactiveDanceAction LastAction { get; private set; } = AudioReactiveDanceAction.None;

        /// <summary>사람이 읽는 사유. 정책이 낸 것이거나 이 클래스의 <c>Reason*</c> 중 하나다.</summary>
        public string LastReason { get; private set; } = ReasonNoProbe;

        /// <summary>★ 직전 틱의 조회가 <b>성공</b>했는가. <c>false</c>가 이어지는데 게이트가 조용히
        /// 닫혀 있으면 그것은 «음악이 없다»가 아니라 <b>«프로브가 못 읽고 있다»</b>이다.</summary>
        public bool LastReadOk { get; private set; }

        /// <summary>어느 다리로 서 있는가(진단 전용, 판정에 쓰지 않는다).</summary>
        public string PlatformTag =>
            _probe != null ? _probe.PlatformTag : SystemAudioActivityProbeFactory.NoProbeTag;

        /// <summary>OS가 전이 푸시를 제공하는 플랫폼인가 — <b>지금 푸시를 쓰고 있다는 뜻이 아니다</b>
        /// (<see cref="ISystemAudioActivityProbe.SupportsPushNotification"/> 문서). 이번 라운드는
        /// 양 플랫폼 모두 폴링만 한다.</summary>
        public bool ProbeSupportsPush => _probe != null && _probe.SupportsPushNotification;

        /// <summary>배선이 실제로 있는가. <c>false</c>면 이 러너는 매 틱 아무것도 안 하고 닫힌 채다.</summary>
        public bool HasProbe => _probe != null;

        /// <param name="probe"><c>null</c> 허용 — 「이 플랫폼에는 없는 기능」이라는 정직한 상태다
        /// (<see cref="SystemAudioActivityProbeFactory.Create"/>).</param>
        public AudioReactiveDanceRunner(ISystemAudioActivityProbe probe)
        {
            _probe = probe;
        }

        /// <summary>
        /// 한 폴링 주기를 처리한다. <b><see cref="PollIntervalSeconds"/>마다</b> 부른다.
        /// </summary>
        /// <param name="deltaSeconds">직전 <b>틱</b>으로부터의 실제 경과(벽시계). 프레임 수가 아니다.</param>
        /// <param name="suppressed">지금 춤을 막아야 하는가 — <c>Core/AudioReactiveDanceGate.BlocksNow</c>의
        /// 결론을 <b>매 틱 새로</b> 계산해 넣는다. 발동 시점 1회만 보면 «집중 모드를 켰는데 계속
        /// 춤춘다»가 된다(그 게이트 문서).</param>
        /// <returns>이번 틱의 결론.</returns>
        public AudioReactiveDanceAction Tick(float deltaSeconds, bool suppressed)
        {
            // NaN은 어떤 비교에도 false를 내므로 이 형태가 NaN까지 함께 막는다(정책과 같은 관례).
            float dt = deltaSeconds > 0f ? deltaSeconds : 0f;
            _secondsSinceLastGoodRead += dt;

            // ---- (0) 배선 자체가 없다. 정책을 부르지 않는다 ----
            //      더미 false를 넣으면 offRun이 가짜로 쌓여 "정상적으로 춤이 끝났다"처럼 보인다.
            if (_probe == null)
            {
                LastReadOk = false;
                return Close(ReasonNoProbe);
            }

            bool ok = _probe.TryReadIsAudioPlaying(out bool isAudioPlaying);
            LastReadOk = ok;

            // ---- (1) 조회 실패 — 정책을 부르지 않는다(3절) ----
            if (!ok)
            {
                // 억제는 신호를 몰라도 이긴다. 「모르는데 계속 춤추는」 상태를 남기지 않는다.
                if (suppressed) return Close(ReasonBlindSuppressed);

                // 실패가 T₂만큼 이어졌다 — 누산을 버린다. 여기서 버티면 나중에 성공했을 때
                // «큰 델타 + 낡은 누산»이 한꺼번에 들어가 T₁/T₄가 한 틱에 넘어간다.
                if (_secondsSinceLastGoodRead >= BlindResetSeconds) return Close(ReasonBlindReset);

                // 아직 짧다 — 직전 판정을 그대로 들고 간다. 누산도 건드리지 않는다.
                LastReason = ReasonBlindHold;
                LastAction = IsGateOpen ? AudioReactiveDanceAction.Continue : AudioReactiveDanceAction.None;
                return LastAction;
            }

            // ---- (2) 조회 성공. 델타는 «마지막 성공 이후»다(틱 간격이 아니다) ----
            float elapsed = _secondsSinceLastGoodRead;
            _secondsSinceLastGoodRead = 0f;

            // ★ 틱 자체가 오래 끊겼던 경우(OS 절전 복귀·긴 프레임 정지). 위 (1)은 «조회 실패»만
            //   덮으므로 이 경로는 그것과 별개로 살아 있다. 낡은 누산 위에 큰 델타를 얹지 않는다(3절).
            if (elapsed >= BlindResetSeconds)
            {
                // ★ 이번 표본은 **버린다**. 정책에 흘려보내려면 델타를 지어내야 하는데
                //   (버린 시간을 그대로 넣으면 T₁/T₄가 한 틱에 넘어간다), 지어낸 델타는 이 저장소가
                //   반복해 당한 «값을 만들어 낸 측정»이다. 다음 틱부터 진짜 델타로 깨끗하게 센다 —
                //   잃는 것은 폴링 한 주기(0.5초)뿐이고 T₁이 3.0초라 사람이 느낄 차이가 없다.
                return Close(ReasonStaleDelta);
            }

            AudioReactiveDanceVerdict verdict =
                AudioReactiveDancePolicy.Evaluate(_state, isAudioPlaying, elapsed, suppressed);

            _state = verdict.Next;
            LastAction = verdict.Action;
            LastReason = verdict.Reason;
            IsGateOpen = verdict.Action == AudioReactiveDanceAction.Start
                         || verdict.Action == AudioReactiveDanceAction.Continue;
            return verdict.Action;
        }

        /// <summary>누산과 위상을 통째로 버리고 게이트를 닫는다. 열려 있었으면 <c>Stop</c>을 낸다 —
        /// 그래야 위쪽(다음 라운드의 coder)이 «춤을 끝내라»를 놓치지 않는다.</summary>
        private AudioReactiveDanceAction Close(string reason)
        {
            bool wasOpen = IsGateOpen;
            _state = default;
            _secondsSinceLastGoodRead = 0f;
            IsGateOpen = false;
            LastReason = reason;
            LastAction = wasOpen ? AudioReactiveDanceAction.Stop : AudioReactiveDanceAction.None;
            return LastAction;
        }

        /// <summary>진단용 — 지금 정책이 들고 있는 위상. 판정에 쓰지 마라(그건 정책의 몫이다).</summary>
        public AudioReactiveDancePhase Phase => _state.Phase;
    }
}
