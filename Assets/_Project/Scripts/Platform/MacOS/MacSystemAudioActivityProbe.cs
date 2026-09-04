#if UNITY_STANDALONE_OSX
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// macOS용 <see cref="ISystemAudioActivityProbe"/> — <b>공개 CoreAudio 프로퍼티 조회만</b> 한다.
    /// <b>오디오 샘플을 한 개도 읽지 않는다.</b>
    ///
    /// ============================================================================
    /// 무엇을 읽는가 — 그리고 왜 마이크 동의창이 안 뜨는가
    /// ============================================================================
    /// <code>
    ///   kAudioHardwarePropertyDefaultOutputDevice      → 기본 **출력** 장치 ID
    ///   kAudioDevicePropertyDeviceIsRunningSomewhere   → 0 = 무음 / 1 = 누군가 재생 중
    ///   스코프는 언제나 kAudioObjectPropertyScopeGlobal (= 'glob')
    /// </code>
    /// <b>입력 스코프(<c>kAudioObjectPropertyScopeInput</c>)를 열지 않고, 샘플을 요청하지 않는다.</b>
    /// 그래서 TCC 동의창 대상이 아니다 — <c>dev-platform</c> 실측(M-A)에서 실제로 뜨지 않았다.
    /// 여기 쓰이는 두 셀렉터는 <c>PlatformParityAuditTests</c>의 캡처 금지 니들 7종과 <b>토큰이 다르다</b>
    /// (<c>ScopeGlobal</c> ≠ <c>ScopeInput</c>).
    ///
    /// ============================================================================
    /// ★★ 실측 — 이 파일의 근거는 전부 이 머신에서 직접 잰 것이다
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>M-A / 재측정 2026-09-03</b> — 무음 구간 <c>0</c> → <c>afplay</c> 재생 구간 <c>1</c> →
    ///     종료 후 <c>0</c>. <b>양성·음성 대조 모두 통과.</b> 프로브 앞에 장치 이름 읽기(교정)를 박아
    ///     "눈먼 프로브의 0"과 "진짜 0"을 구분했다.</item>
    ///   <item><b>M-B (거짓 양성 — 이건 <u>버그가 아니라 이 API의 성질</u>이다)</b> —
    ///     <b>무음을 출력하는 스트림만 열려 있어도 <c>1</c>이 뜬다.</b> 이 신호가 답하는 질문은
    ///     "소리가 나는가"가 아니라 <b>"출력 스트림이 열려 돌고 있는가"</b>다.
    ///     ★ <b>여기서 «고치려» 하지 마라</b> — 공개 API로는 구분이 불가능하고, 구분하려면 샘플을
    ///     읽어야 하며 그것이 곧 캡처다. 이 거짓 양성은 <b>게이트</b>가 처리한다:
    ///     <see cref="AudioReactiveDancePolicy.StuckSignalCeilingSeconds"/>(T₄)가 그 탈출구이고,
    ///     <b>T₄가 존재하는 유일한 이유가 이것</b>이다.</item>
    ///   <item><b>M-C</b> — 워밍업 200회 후 2,000회 평균 <b>30.4µs / 32.7µs per call</b>.
    ///     2Hz 폴링이면 초당 61~65µs = <b>CPU 0.006%</b>. 창 열거(<c>EnumWindows</c>)보다 자릿수가 싸다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 폴링이다. 푸시는 <b>이번 라운드에 배선하지 않는다</b> — 그 이유를 정직하게 적는다
    /// ============================================================================
    /// <c>AudioObjectAddPropertyListener</c>로 <b>폴링 0회</b> 전이 수신이 가능하다는 것은 실측했다
    /// (M-D: 등록 <c>status=0</c>, 전이 4건 수신). 그런데도 지금 쓰지 않는 이유는 셋이다:
    /// <list type="number">
    ///   <item><b>계약 자체가 풀(pull) API다.</b> <see cref="ISystemAudioActivityProbe"/>는
    ///     <c>TryRead...</c> 하나이고, 푸시를 달아도 결국 캐시를 읽어 돌려주게 된다.</item>
    ///   <item><b>콜백 스레드가 미확인이다(U-5).</b> HAL 알림은 Unity 메인 스레드로 온다는 보장이 없고,
    ///     <c>kAudioHardwarePropertyRunLoop</c>의 기본값에 따라 <b>아예 안 불릴 수도</b> 있다.
    ///     그 경우 «영원히 무음»이 «조용히 초록»으로 보인다 — 이 저장소가 반복해 당한 형태다.</item>
    ///   <item><b>IL2CPP 역방향 콜백</b>은 <c>AOT.MonoPInvokeCallback</c> + 델리게이트 수명 관리가
    ///     필요하고, 그것을 이 머신에서 <b>실행으로 검증할 수 없다</b>(사용자 화면 잠금으로 플레이어를
    ///     띄울 수 없다). 검증 못 하는 경로를 성능 최적화로 먼저 넣지 않는다 — M-C가 이미
    ///     "폴링 비용이 무시 가능"이라고 답했으므로 <b>지금 사는 것이 없다</b>.</item>
    /// </list>
    /// ⇒ <see cref="SupportsPushNotification"/>는 <b>true</b>다. 그 값의 뜻은 «OS가 제공한다»이지
    /// «우리가 구독 중이다»가 아니다(그 인터페이스 문서 참고).
    ///
    /// ============================================================================
    /// ★ 기본 출력 장치 전환(U-4) — <b>매 조회마다 다시 묻는 것으로 구조적으로 닫았다</b>
    /// ============================================================================
    /// 블루투스 연결/해제나 이어폰 착탈로 기본 출력 장치가 바뀌면 <b>캐시한 장치 ID가 낡는다</b>
    /// (낡은 ID에 물어도 status는 0이고 값만 틀릴 수 있다 = 조용한 오답). 그래서 이 구현은
    /// <b>장치 ID를 캐시하지 않는다</b> — 매 호출에 기본 출력 장치를 다시 묻고 그 장치에 상태를 묻는다.
    /// 조회가 2회로 늘지만 M-C 기준 2Hz에서 <b>CPU 0.012%</b>다. 캐시로 얻을 것이 없다.
    /// </summary>
    internal sealed class MacSystemAudioActivityProbe : ISystemAudioActivityProbe
    {
        private const string CoreAudioLib = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";

        private const string LogPrefix = "[오디오감지]";

        /// <summary><c>kAudioObjectSystemObject</c> — 시스템 전역 오디오 객체.</summary>
        private const uint SystemObject = 1;

        /// <summary><c>kAudioObjectUnknown</c>.</summary>
        private const uint UnknownObject = 0;

        // ---- FourCC 셀렉터. 값을 손으로 바꾸지 마라 — 틀려도 컴파일은 되고 status만 조용히 어긋난다.
        //      MacSystemAudioSelectorTests가 각 상수를 문자 4개로부터 다시 계산해 대조한다.

        /// <summary><c>kAudioHardwarePropertyDefaultOutputDevice</c> = <c>'dOut'</c>.</summary>
        internal const uint SelectorDefaultOutputDevice = 0x644F7574;

        /// <summary><c>kAudioDevicePropertyDeviceIsRunningSomewhere</c> = <c>'gone'</c>.
        /// <b>이것이 이 파일의 전부다.</b> 0 = 무음, 1 = 누군가 재생 중.</summary>
        internal const uint SelectorDeviceIsRunningSomewhere = 0x676F6E65;

        /// <summary><c>kAudioObjectPropertyScopeGlobal</c> = <c>'glob'</c>.
        /// ★ <b>Input이 아니다.</b> 이 한 글자 차이가 마이크 동의창의 유무를 가른다.</summary>
        internal const uint ScopeGlobal = 0x676C6F62;

        /// <summary><c>kAudioObjectPropertyElementMain</c>.</summary>
        private const uint ElementMain = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioObjectPropertyAddress
        {
            public uint Selector;
            public uint Scope;
            public uint Element;
        }

        /// <summary><c>OSStatus</c> 반환(0 = noErr). <c>outData</c>는 이 파일에서 언제나 <c>UInt32</c> 하나다.</summary>
        [DllImport(CoreAudioLib)]
        private static extern int AudioObjectGetPropertyData(
            uint inObjectID, ref AudioObjectPropertyAddress inAddress,
            uint inQualifierDataSize, IntPtr inQualifierData,
            ref uint ioDataSize, out uint outData);

        /// <summary>그 객체가 그 프로퍼티를 갖고 있는가. <b>있는지 먼저 묻고 읽는다</b> —
        /// 가상 오디오 장치 중에는 이 프로퍼티가 없는 것이 있을 수 있고, 그때 값을 그냥 읽으면
        /// 초기화되지 않은 0(=무음)을 성공처럼 돌려주게 된다.</summary>
        [DllImport(CoreAudioLib)]
        private static extern byte AudioObjectHasProperty(
            uint inObjectID, ref AudioObjectPropertyAddress inAddress);

        private bool _failureLogged;

        public string PlatformTag => "macOS/CoreAudio(IsRunningSomewhere)";

        /// <summary>OS가 전이 푸시를 제공하는가 — <b>제공한다</b>(M-D 실측).
        /// 우리가 지금 구독 중이라는 뜻은 아니다(클래스 문서의 「폴링이다」 절).</summary>
        public bool SupportsPushNotification => true;

        public bool TryReadIsAudioPlaying(out bool isPlaying)
        {
            isPlaying = false;

            try
            {
                if (!TryReadDefaultOutputDevice(out uint device)) return false;

                var address = new AudioObjectPropertyAddress
                {
                    Selector = SelectorDeviceIsRunningSomewhere,
                    Scope = ScopeGlobal,
                    Element = ElementMain,
                };

                if (AudioObjectHasProperty(device, ref address) == 0)
                {
                    LogFailureOnce($"기본 출력 장치({device})에 IsRunningSomewhere 프로퍼티가 없습니다");
                    return false;
                }

                uint size = sizeof(uint);
                int status = AudioObjectGetPropertyData(device, ref address, 0, IntPtr.Zero,
                    ref size, out uint running);
                if (status != 0)
                {
                    LogFailureOnce($"IsRunningSomewhere 조회가 실패했습니다(OSStatus={status})");
                    return false;
                }

                isPlaying = running != 0;
                return true;
            }
            catch (DllNotFoundException e) { LogFailureOnce($"CoreAudio를 찾지 못했습니다({e.Message})"); return false; }
            catch (EntryPointNotFoundException e) { LogFailureOnce($"CoreAudio 진입점이 없습니다({e.Message})"); return false; }
        }

        private bool TryReadDefaultOutputDevice(out uint device)
        {
            var address = new AudioObjectPropertyAddress
            {
                Selector = SelectorDefaultOutputDevice,
                Scope = ScopeGlobal,
                Element = ElementMain,
            };

            uint size = sizeof(uint);
            int status = AudioObjectGetPropertyData(SystemObject, ref address, 0, IntPtr.Zero,
                ref size, out device);
            if (status != 0)
            {
                LogFailureOnce($"기본 출력 장치 조회가 실패했습니다(OSStatus={status})");
                device = UnknownObject;
                return false;
            }

            if (device == UnknownObject)
            {
                // 출력 장치가 하나도 없는 상태(HDMI만 물린 채 뽑힘 등). "모른다"로 돌려준다 —
                // false(=무음)로 접으면 게이트가 «영원히 안 춤춘다»를 정상으로 오인한다.
                LogFailureOnce("기본 출력 장치가 없습니다(kAudioObjectUnknown)");
                return false;
            }

            return true;
        }

        /// <summary>실패는 <b>한 번만</b> 적는다. 2Hz 폴링에서 매번 적으면 그게 곧 Player.log 폭주다.
        /// <b>정직한 실패 보고용</b>이지 고장이 아니다 — 이 기능이 없어도 앱의 다른 것은 전부 돈다.</summary>
        private void LogFailureOnce(string why)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            Debug.LogWarning($"{LogPrefix} {why}. 시스템 오디오 감지를 «모름»으로 보고합니다 — " +
                "춤 반응이 발동하지 않을 뿐이고 다른 기능은 영향을 받지 않습니다. " +
                "샘플은 한 개도 읽지 않았으므로 권한 문제는 아닙니다(출력 상태 프로퍼티 조회입니다).");
        }
    }
}
#endif
