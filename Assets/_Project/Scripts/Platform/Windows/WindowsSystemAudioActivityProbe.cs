#if UNITY_STANDALONE_WIN
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Debug = UnityEngine.Debug;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// Windows용 <see cref="ISystemAudioActivityProbe"/> — <b>피크 미터 조회만</b> 한다.
    /// <b>오디오 스트림을 열지 않고, 샘플을 한 개도 읽지 않는다.</b>
    ///
    /// ============================================================================
    /// ★★ 무엇을 <b>안</b> 하는가 — 이것이 이 파일에서 가장 중요한 문단이다
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>루프백 캡처를 하지 않는다.</b> <c>AUDCLNT_STREAMFLAGS_LOOPBACK</c>도
    ///     <c>IAudioCaptureClient</c>도 등장하지 않는다.</item>
    ///   <item><b><c>IAudioClient</c>조차 열지 않는다.</b> <c>IMMDevice::Activate</c> 한 번으로
    ///     <c>IAudioMeterInformation</c>만 받는다. 스트림 세션이 생기지 않는다.</item>
    /// </list>
    /// 이유는 취향이 아니라 실측이다(<c>docs/security/ENTITLEMENT_CONTRACT.md</c> S-3):
    /// 우리 Win32 표면은 이미 <c>EnumWindows · GetWindowTextW · GetAsyncKeyState · RegEnumKeyExW ·
    /// OpenProcess</c> 조합이라 백신 휴리스틱상 <b>애드웨어/키로거 모양</b>이고, 사용자 실기는
    /// <b>AhnLab V3</b>다. 여기에 루프백 캡처가 더해지면 프로필이 <b>「도청」</b>이 된다 —
    /// <i>"Shimeji를 죽인 건 기능이 아니라 백신 경고였다."</i>
    /// <b>이 제약은 <c>PlatformParityAuditTests</c>의 캡처 금지 니들 7종이 잠근다. 그 감사를
    /// «내가 하려는 일이 걸리니까» 지우지 마라</b> — 이 저장소는 그 형태로 이미 당했다.
    ///
    /// ============================================================================
    /// 1차 출처 (Microsoft Learn "Peak Meters") — <b>이 머신에서 실행 검증은 불가능하다</b>
    /// ============================================================================
    /// <code>
    ///   IMMDeviceEnumerator::GetDefaultAudioEndpoint(eRender, eMultimedia, &amp;device)
    ///     → IMMDevice::Activate(IID_IAudioMeterInformation, CLSCTX_ALL, NULL, &amp;meter)
    ///       → IAudioMeterInformation::GetPeakValue(&amp;peak)      // 0.0 ~ 1.0
    /// </code>
    /// 원문: <i>"For a rendering device, the value retrieved from the peak meter represents the maximum
    /// sample value encountered in the <b>output stream</b> to the device during the preceding metering
    /// period."</i> / <i>"...floating-point numbers in the normalized range from 0.0 to 1.0."</i>
    /// <b>Windows Vista 이상</b>(문서 명시).
    ///
    /// <para>★ <b>이 파일은 이 개발 머신에서 한 번도 실행되지 않는다.</b> 확인한 것은
    /// <c>Tools/CrossCompile/xcheck.sh win</c>의 <b>컴파일 0에러</b>까지이고, 실제 거동은
    /// <b>미확인</b>이다. "고쳤다"가 아니라 "이렇게 동작할 것으로 판단한다, 실기 미확인"이 정직하다.</para>
    ///
    /// ============================================================================
    /// ★ 알려진 <b>거짓 음성</b> — 배타 모드에서는 언제나 0.0이다 (문서 명시)
    /// ============================================================================
    /// <i>"In exclusive mode, the application and the audio hardware exchange audio data directly,
    /// bypassing the software peak meter (which always reports a peak value of 0.0)."</i>
    /// ⇒ <b>배타 모드로 소리를 내는 게임/DAW는 아무리 크게 울려도 감지되지 않는다.</b>
    /// 숨기지 않는다 — 이건 우리가 고칠 수 있는 것이 아니고, 고치려면 캡처가 필요하다.
    /// 실害가 작은 이유는 하나뿐이다: <b>전체화면 게임에서는 어차피 캐릭터가 숨는다</b>(원칙 2).
    /// 그 두 구간이 겹친다. 겹치지 않는 경우(창 모드 배타 DAW)는 <b>그냥 안 춤춘다.</b>
    ///
    /// ============================================================================
    /// ★★ 레벨 → 불리언 변환은 <b>여기가 소유한다</b> (I-13)
    /// ============================================================================
    /// macOS는 불리언을, Windows는 연속 레벨을 준다. 중립 정책
    /// (<see cref="AudioReactiveDancePolicy"/>)에 레벨을 넣으면 macOS 구현체가 값을 <b>지어내야</b>
    /// 하므로, 문턱은 <b>이 파일 안에서</b> 끝낸다.
    /// <list type="bullet">
    ///   <item><b><see cref="PeakThreshold"/> = 0.0005</b>(≈ −66 dBFS). 16비트 LSB가 1/32768 ≈ 0.0000305
    ///     이므로 <b>약 16 LSB</b> 위다 — 디더/노이즈 플로어보다는 확실히 높고, 사람이 들을 수 있는
    ///     어떤 내용보다는 확실히 낮다. <b>무음 임계는 낮게 잡는다</b>: 놓치는 쪽(안 춤춘다)보다
    ///     조금 더 잡는 쪽이 이 기능에서는 덜 나쁘다.</item>
    ///   <item><b><see cref="PeakHoldSeconds"/> = 1.0</b>. 미터가 답하는 것은 <b>직전 계측 구간</b>
    ///     (장치 주기, 대략 10ms)의 최대값이라, 0.5초마다 한 번 보는 폴링은 <b>구간 사이를 통째로
    ///     놓칠 수 있다</b>. 그대로 두면 조용한 악절에서 <c>onRun</c>이 리셋되어
    ///     <see cref="AudioReactiveDancePolicy.StartDelaySeconds"/>(T₁, 연속 ON 3.0초)가 영원히
    ///     안 차는 <b>Windows 전용 거짓 음성</b>이 된다. 그래서 임계를 넘은 시각을 기억해 두고
    ///     1.0초 동안은 계속 «재생 중»으로 보고한다(폴링 주기 0.5초보다 크고, T₂ 3.0초보다 작다).
    ///     대가: 음악이 진짜 끝난 뒤 최대 1.0초 늦게 OFF로 떨어진다 — <b>T₂ 안에 흡수된다.</b></item>
    /// </list>
    ///
    /// ============================================================================
    /// 장치 전환 — <b>캐시하지 않는다</b>(macOS 구현과 같은 판단)
    /// ============================================================================
    /// 기본 출력 엔드포인트는 블루투스 연결/해제로 바뀐다. 캐시한 미터는 그때 <b>낡은 장치를 계속
    /// 가리키고 조용히 0.0을 돌려준다</b>(status는 성공이다 = 실패한 측정이 성공한 측정과 똑같이 생긴다).
    /// 그래서 매 조회마다 엔드포인트와 미터를 새로 받고 <b>즉시 <c>ReleaseComObject</c></b> 한다.
    /// 열거자(<c>IMMDeviceEnumerator</c>)만 한 번 만들어 재사용하고 앱 종료 훅에서 놓는다.
    /// </summary>
    internal sealed class WindowsSystemAudioActivityProbe : ISystemAudioActivityProbe
    {
        private const string LogPrefix = "[오디오감지]";

        /// <summary>이 값을 <b>넘어야</b> "소리가 난다"로 본다. 위 클래스 문서의 유도 참고.</summary>
        internal const float PeakThreshold = 0.0005f;

        /// <summary>임계를 넘은 뒤 이 시간 동안은 계속 «재생 중»으로 보고한다(폴링 사이 구멍 메우기).</summary>
        internal const float PeakHoldSeconds = 1.0f;

        // ---- COM 식별자. 1차 출처는 Microsoft Learn / mmdeviceapi.h · endpointvolume.h 다.
        //      한 글자만 틀려도 개체 생성이나 QueryInterface가 조용히 실패한다. 이 머신에서는 실행으로
        //      확인할 수 없으므로 SystemAudioActivityProbeContractTests가 소스에서 뽑아
        //      «서로 다르고 문서 값과 같은지»를 대신 잠근다.

        /// <summary><c>CLSID_MMDeviceEnumerator</c>.</summary>
        internal const string DeviceEnumeratorClsid = "BCDE0395-E52F-467C-8E3D-C4579291692E";

        /// <summary><c>IID_IMMDeviceEnumerator</c>.</summary>
        internal const string DeviceEnumeratorIid = "A95664D2-9614-4F35-A746-DE8DB63617E6";

        /// <summary><c>IID_IMMDevice</c>.</summary>
        internal const string DeviceIid = "D666063F-1587-4E43-81F1-B948E807363F";

        /// <summary><c>IID_IAudioMeterInformation</c>. ★ 미터 <b>정보</b>다 — 스트림이 아니다.</summary>
        internal const string AudioMeterInformationIid = "C02216F6-8C67-4B5B-9D00-D008E73E0064";

        /// <summary><c>eRender</c>(EDataFlow) — <b>출력</b> 엔드포인트. 우리는 이 값만 쓴다.</summary>
        private const uint DataFlowRender = 0;

        /// <summary><c>eMultimedia</c>(ERole) — 음악/영상이 쓰는 기본 장치.</summary>
        private const uint RoleMultimedia = 1;

        /// <summary><c>CLSCTX_ALL</c>.</summary>
        private const uint ClsCtxAll = 0x17;

        private IMMDeviceEnumerator _enumerator;
        private object _enumeratorComObject;
        private bool _unavailable;
        private bool _failureLogged;
        private bool _limitationLogged;
        private bool _quitHookInstalled;

        /// <summary>임계를 마지막으로 넘은 시각. <c>Stopwatch</c>라 사용자가 시계를 돌려도 안 흔들리고,
        /// Unity 시간축(일시정지·타임스케일)과도 무관하다.</summary>
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _lastAboveThresholdSeconds = double.NegativeInfinity;

        public string PlatformTag => "Windows/WASAPI(IAudioMeterInformation)";

        /// <summary>
        /// 재생 <b>상태</b>에 대한 푸시는 확인되지 않았다 — <b>false</b>.
        /// <para><c>IMMNotificationClient</c>는 <b>장치 추가/제거/기본 장치 변경</b> 알림이지
        /// "지금 소리가 나는가"가 아니다. 그것을 푸시라고 적으면 폴링 주기를 늘려도 된다는 판단이
        /// 조용히 틀린다(U-2).</para>
        /// </summary>
        public bool SupportsPushNotification => false;

        public bool TryReadIsAudioPlaying(out bool isPlaying)
        {
            isPlaying = false;
            if (_unavailable) return false;

            IMMDevice device = null;
            object meterComObject = null;
            try
            {
                if (!EnsureEnumerator()) return false;

                _enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleMultimedia, out device);
                if (device == null)
                {
                    // 출력 장치가 하나도 없다. "모른다"로 돌려준다 — false(=무음)로 접으면
                    // 게이트가 «영원히 안 춤춘다»를 정상으로 오인한다.
                    LogFailureOnce("기본 출력 엔드포인트를 받지 못했습니다");
                    return false;
                }

                var iid = new Guid(AudioMeterInformationIid);
                device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out meterComObject);
                if (!(meterComObject is IAudioMeterInformation meter))
                {
                    LogFailureOnce("IAudioMeterInformation으로 QueryInterface하지 못했습니다");
                    return false;
                }

                meter.GetPeakValue(out float peak);
                LogKnownLimitationOnce();

                double now = _clock.Elapsed.TotalSeconds;
                if (peak > PeakThreshold) _lastAboveThresholdSeconds = now;

                // 홀드 — 폴링 사이의 계측 구멍을 메운다(클래스 문서의 PeakHoldSeconds 문단).
                isPlaying = now - _lastAboveThresholdSeconds <= PeakHoldSeconds;
                return true;
            }
            catch (Exception e)
            {
                // COM HRESULT 실패는 예외로 온다(PreserveSig 기본값). 조회 실패는 "모른다"이지
                // "무음"이 아니다. Mono/IL2CPP의 COM 상호운용이 안 도는 경우도 여기로 온다.
                LogFailureOnce($"피크 미터 조회 중 예외({e.GetType().Name}: {e.Message})");
                return false;
            }
            finally
            {
                ReleaseSafe(meterComObject);
                ReleaseSafe(device);
            }
        }

        private bool EnsureEnumerator()
        {
            if (_enumerator != null) return true;

            try
            {
                Type type = Type.GetTypeFromCLSID(new Guid(DeviceEnumeratorClsid));
                if (type == null)
                {
                    _unavailable = true;
                    LogFailureOnce("CLSID_MMDeviceEnumerator에 대한 타입을 얻지 못했습니다(COM 미지원 런타임일 수 있습니다)");
                    return false;
                }

                object instance = Activator.CreateInstance(type);
                if (!(instance is IMMDeviceEnumerator enumerator))
                {
                    _unavailable = true;
                    ReleaseSafe(instance);
                    LogFailureOnce("IMMDeviceEnumerator로 QueryInterface하지 못했습니다");
                    return false;
                }

                _enumeratorComObject = instance;
                _enumerator = enumerator;
                InstallQuitHook();
                return true;
            }
            catch (Exception e)
            {
                _unavailable = true;
                LogFailureOnce($"COM 초기화 중 예외({e.GetType().Name}: {e.Message})");
                return false;
            }
        }

        private void InstallQuitHook()
        {
            if (_quitHookInstalled) return;
            _quitHookInstalled = true;
            // 씬 오브젝트를 만들지 않는다 — 씬 배선에 기대면 씬이 바뀔 때 조용히 죽는다
            // (WindowsTaskbarButtonRemover.InstallQuitHook과 같은 이유).
            UnityEngine.Application.quitting += ReleaseEnumerator;
        }

        private void ReleaseEnumerator()
        {
            ReleaseSafe(_enumeratorComObject);
            _enumeratorComObject = null;
            _enumerator = null;
        }

        private static void ReleaseSafe(object comObject)
        {
            if (comObject == null) return;
            try { Marshal.ReleaseComObject(comObject); }
            catch (Exception) { /* 해제 실패로 종료나 다음 폴링을 막지 않는다. */ }
        }

        /// <summary>
        /// ★ <b>첫 성공 조회에 딱 한 번</b> 남기는 한계 고지. 로그 폭주가 아니라 <b>다음 신고를
        /// 가리기 위한 것</b>이다 — <i>"음악을 틀었는데 춤을 안 춰요"</i>가 올라왔을 때, 이 줄이
        /// Player.log에 있으면 배타 모드 여부를 <b>먼저</b> 물어볼 수 있다. 이 줄이 없으면
        /// 그 신고는 «감지가 고장났다»로 오진된다.
        /// </summary>
        private void LogKnownLimitationOnce()
        {
            if (_limitationLogged) return;
            _limitationLogged = true;
            Debug.Log($"{LogPrefix} {PlatformTag} — 시스템 오디오 감지를 시작합니다. " +
                "샘플은 읽지 않고 피크 미터 값만 조회합니다(캡처 아님). " +
                "★ 알려진 한계: <b>배타 모드</b>(WASAPI exclusive)로 소리를 내는 앱은 하드웨어를 직접 " +
                "쓰면서 소프트웨어 피크 미터를 우회하므로 <b>언제나 0.0</b>으로 보고됩니다 — " +
                "그런 앱(일부 게임·DAW)에서는 춤 반응이 나오지 않습니다. 고장이 아니라 " +
                "Microsoft 문서에 명시된 동작이고, 캡처 없이는 우회할 수 없습니다.");
        }

        private void LogFailureOnce(string why)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            Debug.LogWarning($"{LogPrefix} {why}. 시스템 오디오 감지를 «모름»으로 보고합니다 — " +
                "춤 반응이 발동하지 않을 뿐이고 다른 기능은 영향을 받지 않습니다. " +
                "오디오 스트림은 열지 않았고 샘플도 읽지 않았습니다(피크 미터 조회입니다).");
        }

        // ==================== COM 선언 ====================
        // ★ 슬롯 순서가 곧 ABI다. 줄 순서를 바꾸면 엉뚱한 함수가 불린다.
        //   부르지 않는 슬롯도 반드시 자리를 채워야 한다(WindowsTaskbarButtonRemover와 같은 관례).

        [ComImport]
        [Guid(DeviceEnumeratorIid)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            /// <summary>슬롯 0 — 원본 <c>EnumAudioEndpoints</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot0(uint dataFlow, uint stateMask, out IntPtr collection);

            /// <summary>슬롯 1 — <c>GetDefaultAudioEndpoint</c>. <b>이 파일이 부르는 유일한 것.</b></summary>
            void GetDefaultAudioEndpoint(uint dataFlow, uint role, out IMMDevice device);

            /// <summary>슬롯 2 — 원본 <c>GetDevice</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot2([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

            /// <summary>슬롯 3 — 원본 <c>RegisterEndpointNotificationCallback</c>.
            /// <b>부르지 않는다</b> — 콜백을 달면 역방향 P/Invoke가 생기고, 그것이 이번 라운드에
            /// macOS 푸시를 미룬 이유와 같은 벽이다(자리표시자).</summary>
            void ReservedSlot3(IntPtr client);

            /// <summary>슬롯 4 — 원본 <c>UnregisterEndpointNotificationCallback</c>. <b>부르지 않는다</b>.</summary>
            void ReservedSlot4(IntPtr client);
        }

        [ComImport]
        [Guid(DeviceIid)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            /// <summary>슬롯 0 — <c>Activate</c>. ★ 우리가 넘기는 IID는 <b>언제나</b>
            /// <see cref="AudioMeterInformationIid"/> 하나다. <c>IID_IAudioClient</c>를 여기 넣지 마라 —
            /// 그 순간 스트림 세션이 생기고 이 파일의 전제가 무너진다.</summary>
            void Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object iface);

            /// <summary>슬롯 1 — 원본 <c>OpenPropertyStore</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot1(uint stgmAccess, out IntPtr store);

            /// <summary>슬롯 2 — 원본 <c>GetId</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot2(out IntPtr id);

            /// <summary>슬롯 3 — 원본 <c>GetState</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot3(out uint state);
        }

        [ComImport]
        [Guid(AudioMeterInformationIid)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            /// <summary>슬롯 0 — <c>GetPeakValue</c>. <b>이 파일이 부르는 유일한 것.</b>
            /// 0.0~1.0 정규화 값이고, 배타 모드에서는 언제나 0.0이다(클래스 문서).</summary>
            void GetPeakValue(out float peak);

            /// <summary>슬롯 1 — 원본 <c>GetMeteringChannelCount</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot1(out uint channelCount);

            /// <summary>슬롯 2 — 원본 <c>GetChannelsPeakValues</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot2(uint channelCount, IntPtr peakValues);

            /// <summary>슬롯 3 — 원본 <c>QueryHardwareSupport</c>. <b>부르지 않는다</b>(자리표시자).</summary>
            void ReservedSlot3(out uint response);
        }
    }
}
#endif
