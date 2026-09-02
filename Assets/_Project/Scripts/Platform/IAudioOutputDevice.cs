namespace StickMate.Platform
{
    /// <summary>
    /// 온디맨드로 열고 닫는 <b>출력 전용</b> 오디오 장치. 16비트 정수 인터리브 PCM 한 가지만 받는다.
    ///
    /// ============================================================================
    /// 왜 이 계약이 필요한가 — Unity 데스크톱에 온디맨드 API가 없다
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><c>m_EnableOutputSuspension</c>은 Unity 공식 문서상 <b>에디터 전용</b>이다(이 프로젝트는
    ///   이미 1이지만 빌드에서 안 먹는다).</item>
    ///  <item><c>AudioSettings.Mobile.StartAudioOutput()/StopAudioOutput()</c>은 <b>iOS/Android 전용</b>.
    ///   데스크톱 등가물이 없다.</item>
    ///  <item><c>AudioSettings.Reset()</c>은 <b>재구성</b>이지 해제가 아니다.</item>
    /// </list>
    /// 그래서 <c>m_DisableAudio=false</c>로 되돌리는 순간, <b>소리를 끈 사용자에게도</b> 출력 장치가
    /// 24시간 열린다. <c>docs/PERFORMANCE_NOTES.md</c> 부록 C 실측:
    /// <c>coreaudiod</c>가 <b>0.00% → 4.92~5.06%</b>(우리 프로세스는 0.19%. 26배가 옆 프로세스에 있다),
    /// 버퍼를 8배(512→4096)로 키워도 <b>−17%뿐</b>이라 <b>스트림이 열려 있다는 사실 자체</b>가 비용이다.
    /// 더 큰 것은 CPU가 아니라 <c>PreventUserIdleSystemSleep</c> 어서션 — <b>오디오를 켜면 맥이 안 잔다</b>
    /// (원칙 2 재위반). Windows도 같은 함정 근거가 있다: 열린 스트림이 <c>powercfg /requests</c>에
    /// <i>"An audio stream is currently in use"</i>로 뜨고, <c>audiodg.exe</c>가 <c>coreaudiod</c> 자리다.
    ///
    /// <para>실측한 대가: <b>콜드 48ms / 이후 ~38ms</b>, 닫으면 <b>0.3초 안에</b>
    /// <c>kAudioDevicePropertyDeviceIsRunning</c>이 0으로 돌아온다(= 어서션도 함께 풀린다).</para>
    ///
    /// ============================================================================
    /// 계약 — 사실 조회 + 열기/쓰기/닫기. <b>정책은 여기 없다</b>
    /// ============================================================================
    /// "언제 열 것인가 / 얼마나 열어 둘 것인가 / 실패하면 언제 다시 시도할 것인가"는 <b>한 줄도</b>
    /// 이 인터페이스나 그 구현체가 정하지 않는다. 전부 플랫폼 중립
    /// <see cref="AudioActivationPolicy"/>가 정하고, 구현체는 OS에 묻고 OS에 쓰기만 한다.
    /// 이 분리가 없으면 Windows가 macOS의 규칙을 <b>물리적으로 재사용할 수 없다</b>
    /// (실제 사고: <c>FullscreenSuspendPolicy.cs</c>가 <c>Platform/MacOS/</c> 안에 있었다).
    ///
    /// <para><b>선택적 캐퍼빌리티</b>다 — <see cref="IReservedBarAutoHideControl"/>와 같은 관례.
    /// 구현이 없는 플랫폼에서는 <b>소리가 안 날 뿐</b> 예외가 나지 않는다. 실패의 방향이 언제나
    /// "조용해진다"인 것은 의도된 설계다(24시간 상주 앱에서 소리 경로의 예외는 로그를 더럽히고
    /// 프레임을 먹는다).</para>
    ///
    /// ============================================================================
    /// ★★ 이 계약이 <b>영원히 갖지 않는 것</b> — 캡처(입력)
    /// ============================================================================
    /// 여기에는 <b>녹음/캡처/루프백 동사가 하나도 없고, 앞으로도 추가하지 않는다.</b>
    /// 문서가 아니라 <c>Tests/EditMode/PlatformParityAuditTests</c>가 기계적으로 지킨다.
    /// 이유가 두 개이고 둘 다 실측 근거가 있다:
    /// <list type="number">
    ///  <item><b>백신 휴리스틱.</b> <c>docs/security/ENTITLEMENT_CONTRACT.md</c> S-3의 전수 실측대로
    ///   우리 Win32 표면은 이미 <i>"종일 상주하며 남의 창 제목을 훑고 키 상태를 폴링하는 프로세스"</i>
    ///   모양이다. 사용자 실기 환경은 <b>AhnLab V3</b>다. 여기에 <b>재생</b>을 더하는 것은 중립이지만
    ///   <b>캡처</b>(<c>waveIn*</c> / <c>IAudioCaptureClient</c> /
    ///   <c>AUDCLNT_STREAMFLAGS_LOOPBACK</c> / <c>AudioQueueNewInput</c>)를 더하는 순간
    ///   프로필이 <b>도청</b>으로 바뀐다. Shimeji를 죽인 것은 기능이 아니라 백신 경고였다.</item>
    ///  <item><b>macOS TCC.</b> 출력은 권한이 필요 없지만 <b>입력은 마이크 동의 창</b>을 띄운다
    ///   (<c>NSMicrophoneUsageDescription</c> + <c>com.apple.security.device.audio-input</c>).
    ///   바탕화면 캐릭터가 첫 실행에 마이크를 요구하는 것은 되돌릴 수 없는 신뢰 사고다.</item>
    /// </list>
    /// 즉 <b>비문서 API 누적은 이 계약으로 0건 늘어난다</b>(현재 누적 <c>InternalGetWindowText</c> 1건).
    /// macOS는 AudioToolbox/CoreAudio 공개 C API, Windows는 <c>winmm</c>의
    /// <c>waveOut*</c>(1991년부터 공개 헤더 <c>mmeapi.h</c>) — 양쪽 다 문서화된 것만 쓴다.
    /// </summary>
    public interface IAudioOutputDevice
    {
        /// <summary>로그·흔적에 찍을 플랫폼 꼬리표("macOS"/"Windows"). 다른 플랫폼이 만든 진단을
        /// 실수로 자기 것으로 읽는 것을 막는다(<see cref="IReservedBarAutoHideControl.PlatformTag"/>와 같은 관례).</summary>
        string PlatformTag { get; }

        /// <summary>
        /// 기본 출력 장치를 연다. <b>이 앱이 오디오 하드웨어를 붙잡는 유일한 지점</b>이다.
        ///
        /// <para>구현체는 성공을 <b>되읽어</b> 확인해야 한다 — 이 계열 API는 "요청을 접수했다"만
        /// 알려주고 실제 반영 여부를 알려주지 않는 경우가 있어, 반환값만 믿으면 실패를 성공으로
        /// 로그한다(<c>UniWinCore.IsTopmost</c> 캐시 사고와 같은 형태 —
        /// <see cref="TopmostRestorePolicy"/> 문서 참고).</para>
        /// </summary>
        /// <param name="format">요청 포맷. 장치가 그대로 못 받으면 구현체가 리샘플 없이 <b>실패</b>를
        /// 돌려준다 — 조용히 다른 포맷으로 여는 것은 "무엇을 재고 있는지"를 흐린다.</param>
        /// <param name="failureReason">실패 사유(사람이 읽는 한 줄). 성공이면 null.</param>
        /// <returns>되읽기까지 마쳐 실제로 열렸는가.</returns>
        bool TryOpen(AudioOutputFormat format, out string failureReason);

        /// <summary>
        /// 16비트 정수 인터리브 PCM을 그대로 밀어 넣는다. <b>부동소수·리샘플·믹싱을 계약에 두지 않는
        /// 이유</b>: 양 플랫폼의 공개 API(<c>AudioQueueEnqueueBuffer</c> / <c>waveOutWrite</c>)가 이
        /// 형식을 그대로 받으므로, 여기서 변환을 요구하면 <b>플랫폼 코드가 정책을 갖게 된다</b>.
        ///
        /// <para>★ <b>호출자의 의무 — 첫 샘플은 0이어야 한다.</b> 온디맨드에서는 첫 샘플이
        /// <b>갓 열린 스트림</b>에 얹혀 DC 스텝 = 클릭이 된다(상시 개방에서는 안 나타나는 종류라
        /// <c>design-sound</c>의 초기 스펙에 페이드인 칸이 비어 있었다 — 그쪽 자백 항목).
        /// 모든 클립은 0에서 시작해 최소
        /// <see cref="AudioActivationPolicy.MinimumFadeInSeconds"/> 동안 페이드인한다.
        /// 이 계약은 그것을 <b>강제하지 않는다</b> — 파형을 검사하려면 여기서 전수 스캔을 해야 하고,
        /// 그것은 재생 경로에 없어야 할 비용이다. 강제 지점은 클립을 굽는 쪽이다.</para>
        /// </summary>
        /// <param name="interleaved">샘플 배열. <paramref name="sampleCount"/>보다 길어도 된다.</param>
        /// <param name="sampleCount">실제로 밀 샘플 수(채널 합산, 프레임 수 × 채널 수).</param>
        /// <returns>장치가 받아들였는가. 열려 있지 않으면 반드시 false(예외 금지).</returns>
        bool TrySubmit(short[] interleaved, int sampleCount);

        /// <summary>
        /// 장치를 <b>완전히</b> 닫는다 — 정지가 아니라 해제다. 여기서 절전 어서션이 풀린다.
        /// 이미 닫혀 있으면 아무 일도 하지 않고 true.
        /// </summary>
        /// <returns>되읽기까지 마쳐 실제로 닫혔는가.</returns>
        bool TryClose();

        /// <summary>
        /// OS가 지금 보고하는 개폐 상태. <b>내부 플래그를 되돌려주지 마라</b> — 그러면 이 조회는
        /// 아무것도 검증하지 못한다. 못 읽으면 false를 돌려주고, 그때 정책은 보수적으로
        /// "열려 있다고 가정하고 닫기"로 간다(모르면 남의 전기를 쓰지 않는다).
        /// </summary>
        /// <param name="isOpen">지금 장치가 열려 있는가.</param>
        /// <returns>조회에 성공했는가.</returns>
        bool TryReadIsOpen(out bool isOpen);

        /// <summary>
        /// ★ 마지막 열기에서 <b>호출부터 첫 샘플이 나갈 때까지</b> 실제로 걸린 초.
        /// 아직 한 번도 열지 않았으면 음수.
        ///
        /// <para><b>왜 계약에 넣는가</b>: <c>design-sound</c>의 "콜드 48ms 허용" 판정은
        /// <b>단 하나의 전제</b> 위에 서 있다 — 그 지연이 트랜지언트 융합창
        /// (<see cref="AudioActivationPolicy.FusionWindowSeconds"/>) 안쪽이라 개방 클릭이
        /// 우리 소리의 어택에 흡수된다는 것. 이 전제가 깨지면 사용자에게는 <b>설명 없는 클릭</b>이
        /// 들린다. 그런데 그 전제는 <b>플랫폼과 장치마다 다르고 미측정 구간이 두 개</b>다:
        /// <list type="bullet">
        ///  <item><b>Windows 전체</b> — 48ms는 macOS AUHAL 실측이다. Windows는 <c>audiodg.exe</c> +
        ///   WASAPI 공유 모드라 <b>그대로 옮겨 쓰면 안 된다</b>.</item>
        ///  <item><b>블루투스 / USB DAC</b> — 재개방 지연이 자릿수부터 다르고
        ///   <b>스트림 머리를 삼킬 수 있다</b>(양 플랫폼 공통, 미측정).</item>
        /// </list>
        /// 그래서 이 값을 <b>추정하지 않고 실제로 재서</b> 돌려준다. 실기에서
        /// <see cref="AudioActivationPolicy.IsOpenLatencyWithinFusionWindow"/>가 false를 내면
        /// 그것은 성능 문제가 아니라 <b>Q3 판정을 다시 열어야 한다는 신호</b>다.</para>
        /// </summary>
        float LastOpenLatencySeconds { get; }
    }

    /// <summary>
    /// 출력 포맷. <b>비트 깊이는 계약상 16비트 정수 고정</b>이라 필드가 없다 —
    /// 협상 축을 줄일수록 "실패한 열기와 성공한 열기가 똑같이 생기는" 자리가 줄어든다.
    /// </summary>
    public readonly struct AudioOutputFormat
    {
        /// <summary>표본화 주파수(Hz). 48000이 양 플랫폼 기본 장치의 사실상 표준이고,
        /// 44100을 요청하면 OS가 리샘플러를 하나 더 끼운다(비용이 우리 눈에 안 보이는 곳에서 는다).</summary>
        public readonly int SampleRateHz;

        /// <summary>채널 수. 1(모노) 또는 2(스테레오).</summary>
        public readonly int ChannelCount;

        public AudioOutputFormat(int sampleRateHz, int channelCount)
        {
            SampleRateHz = sampleRateHz;
            ChannelCount = channelCount;
        }

        /// <summary>이 앱의 기본값 — 48kHz 스테레오. 근거는 위 <see cref="SampleRateHz"/> 문서.</summary>
        public static AudioOutputFormat Default => new AudioOutputFormat(48000, 2);

        /// <summary>구현체가 열기 전에 부르는 방어선. 말이 안 되는 값으로 OS를 두드리지 않는다.</summary>
        public bool IsValid => SampleRateHz >= 8000 && SampleRateHz <= 192000
            && (ChannelCount == 1 || ChannelCount == 2);

        public override string ToString() => SampleRateHz + "Hz/" + ChannelCount + "ch/16bit";
    }
}
