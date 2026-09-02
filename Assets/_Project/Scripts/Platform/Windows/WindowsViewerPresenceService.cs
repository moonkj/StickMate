#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// Windows용 <see cref="IViewerPresenceService"/> — macOS 구현과 <b>같은 날 같은 라운드에</b> 함께
    /// 만든다. 2026-08-31 오전에 "macOS에서만 고친 가려짐 필터가 Windows로 전파되지 않아 같은 버그가
    /// 계속 살아 있던" 사고가 있었고, 그 재발 방지가 이 프로젝트의 명시적 교훈이다
    /// (<c>VisibleTopEdgeSolver</c> 도입 경위 참고). 판단 로직은 플랫폼 중립
    /// <see cref="FramePacingPolicy"/> / <see cref="SessionVisibilityPolicy"/> 쪽에만 있고,
    /// 이 파일은 <b>OS에 사실을 묻는 일</b>만 한다.
    ///
    /// <para><b>전부 읽기 전용 조회다</b>(CLAUDE.md 원칙 3). 값을 바꾸는 API는 한 개도 없다.
    /// <c>OpenInputDesktop</c>조차 <c>DESKTOP_READOBJECTS</c> 하나만 요구하고, 성공하면 즉시 닫는다.</para>
    ///
    /// <para><b>★ 정직한 플랫폼 차이 — 모니터 꺼짐을 아직 감지하지 못한다</b>:
    /// macOS는 <c>CGDisplayIsAsleep</c> 한 번으로 "화면이 꺼져 있다"를 즉답한다. Windows에는 대응하는
    /// 폴링 API가 없고, <c>RegisterPowerSettingNotification(GUID_MONITOR_POWER_ON)</c>으로
    /// <c>WM_POWERBROADCAST</c>를 받아야 한다 — 즉 <b>창 프로시저를 가로채야</b> 하는데 이 앱의 창은
    /// UniWindowController 네이티브 플러그인이 소유하고 있어 이번 라운드 범위를 넘는다. 그래서 여기서는
    /// <c>DisplayAsleep=false</c>로 <b>보수적으로 보고</b>하고(= 절감을 포기하고 정상 동작을 택한다),
    /// 대신 무입력 시간 기반 Away 등급은 양 플랫폼에서 동일하게 동작한다.</para>
    ///
    /// ============================================================================
    /// ★★ 세션 잠금 — <b>이 플랫폼이 반대쪽의 사각지대를 덮는 자리</b> (2026-09-02)
    /// ============================================================================
    /// <code>
    ///           | DisplayAsleep                     | SessionLocked
    ///   --------+-----------------------------------+------------------------------------------
    ///   macOS   | 채워짐 (CGDisplayIsAsleep)         | 항상 false — 문서화된 수단이 없다
    ///   Windows | 항상 false (바로 위 문단)           | ★ 채워짐 — 이 파일이 그 자리다
    /// </code>
    /// <b>이 파일의 <c>DisplayAsleep=false</c>를 보고 "Windows는 이 기능이 없구나"라고 결론짓지 마라.</b>
    /// <see cref="SessionVisibilityPolicy.ShouldSuspendFootholdScan"/>은 OR 한 줄이고,
    /// Windows에서는 <b>아래 세션 잠금 다리로 선다.</b> 한쪽을 지우면 그 플랫폼에서 기능이 사라진다.
    ///
    /// <para><b>수단 선택</b>(근거 전문: <c>docs/platform/GHOST_FOOTHOLDS.md</c> 2-2절)
    /// <list type="bullet">
    /// <item><b>(A) 주</b> — <c>WTSQuerySessionInformationW(WTSSessionInfoEx)</c>의
    ///   <c>SessionFlags</c>. 문서화된 읽기 전용 조회이고 잠금/해제를 직접 답한다.</item>
    /// <item><b>(B) 보</b> — <c>OpenInputDesktop</c> 실패. <b>(B)만이 UAC 보안 데스크톱을 덮는다</b>
    ///   (그 구간에는 (A)가 "잠기지 않음"이라고 답한다).</item>
    /// <item><b>(C) 채택 안 함</b> — <c>WTSRegisterSessionNotification</c> +
    ///   <c>WM_WTSSESSION_CHANGE</c>. 창 프로시저가 필요해서 <b>위 DisplayAsleep을 포기한 것과 정확히
    ///   같은 벽</b>에 부딪힌다. (A)(B)는 둘 다 폴링 조회라 그 벽이 없다.</item>
    /// </list></para>
    ///
    /// <para><b>★ 구조체 레이아웃이 틀리면 에러 없이 쓰레기 플래그가 나온다</b> — 이 저장소가 반복해서
    /// 당한 <i>"실패한 측정과 성공한 측정이 똑같이 생긴"</i> 형태 그 자체다. 그래서 값을 쓰기 전에
    /// <b>네 겹으로 교차 검증</b>하고, 하나라도 어긋나면 전부 <b>"모름 → 보고 있다"</b>로 떨어뜨린다
    /// (<see cref="TryQueryWtsSessionLocked"/> 참고). 특히 <c>SessionId</c>를
    /// <c>ProcessIdToSessionId</c>가 돌려준 <b>우리 실제 세션 ID와 대조</b>하는 것이 핵심이다 —
    /// 레이아웃이 어긋나면 이 값이 거의 확실히 불일치하므로, <b>잘못 읽은 측정이 성공한 측정과 다르게
    /// 생기게 된다.</b></para>
    ///
    /// <para><b>★ Win7 / Server 2008 R2 플래그 반전(기록만)</b>: 그 세대에는 코드 결함이 있어
    /// <c>WTS_SESSIONSTATE_LOCK</c>/<c>UNLOCK</c>의 의미가 <b>뒤집혀 있다</b>. 이 앱의 타깃은 Win10+라
    /// Win10 의미(0=잠김, 1=풀림)를 쓰고, OS 버전 분기는 넣지 않는다 — 분기를 넣으면 이 머신에서
    /// 검증 불가능한 경로가 하나 더 생길 뿐이다. 대신 <b>{0,1} 밖의 값은 전부 "모름"</b>으로 간다.</para>
    /// </summary>
    internal sealed class WindowsViewerPresenceService : IViewerPresenceService
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;      // 0 = 배터리, 1 = AC, 255 = 알 수 없음
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;  // 1 = 절전 모드(배터리 세이버) 켜짐 (Windows 10+)
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        // ---- (A) 세션 잠금 — wtsapi32 --------------------------------------------------------

        /// <summary><c>WTS_CURRENT_SESSION</c> = <c>(DWORD)-1</c> — 이 프로세스가 속한 세션.</summary>
        private const uint WtsCurrentSession = 0xFFFFFFFFu;

        /// <summary><c>WTS_INFO_CLASS.WTSSessionInfoEx</c> = 25.</summary>
        private const int WtsSessionInfoEx = 25;

        /// <summary><c>WTS_SESSIONSTATE_LOCK</c>. Win10+ 의미로 <b>0 = 잠김</b>(위 클래스 문서의 Win7 주의).</summary>
        private const int WtsSessionStateLock = 0;

        /// <summary><c>WTS_SESSIONSTATE_UNLOCK</c> = 1 = 풀림.</summary>
        private const int WtsSessionStateUnlock = 1;

        /// <summary><c>WTS_CONNECTSTATE_CLASS</c>의 마지막 값(<c>WTSInit</c> = 9). 범위 검사용.</summary>
        private const int WtsConnectStateMax = 9;

        /// <summary>
        /// <c>WTSINFOEXW</c>의 <b>앞머리만</b> 그대로 옮긴 것. 뒤쪽(고정 길이 WCHAR 배열 3개 +
        /// <c>LARGE_INTEGER</c> 5개 + 카운터 6개)은 <b>한 글자도 읽지 않으므로 선언하지 않는다</b> —
        /// 읽지 않는 필드를 옮겨 적을수록 레이아웃을 틀릴 기회만 늘어난다.
        ///
        /// <para><c>WTSINFOEXW</c>는 <c>{ DWORD Level; WTSINFOEX_LEVEL_W Data; }</c>이고, 그 공용체 안의
        /// <c>WTSINFOEX_LEVEL1_W</c>가 <c>LARGE_INTEGER</c>를 담고 있어 <b>8바이트 정렬</b>을 요구한다.
        /// 그래서 <c>Data</c>는 오프셋 8에서 시작하고, <c>Level</c> 뒤에 4바이트 패딩이 들어간다 —
        /// 아래 <c>UnionPadding</c>이 그것이다(x86/x64 모두 동일: MSVC 기본 팩킹 8에서
        /// <c>__int64</c>는 양쪽 다 8정렬이다).</para>
        ///
        /// <para><c>Data</c>의 첫 세 필드는 <c>ULONG SessionId</c>, <c>WTS_CONNECTSTATE_CLASS
        /// SessionState</c>(열거형 = int), <c>LONG SessionFlags</c>다. <b>우리에게 필요한 것은 마지막
        /// 하나뿐</b>이고 앞의 둘은 전부 <b>레이아웃 검산용</b>으로 읽는다.</para>
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct WTSINFOEX_HEAD
        {
            public uint Level;          // 반드시 1
            public uint UnionPadding;   // 공용체 8바이트 정렬 패딩(읽지 않는다)
            public uint SessionId;      // 검산: ProcessIdToSessionId 결과와 같아야 한다
            public int SessionState;    // 검산: WTS_CONNECTSTATE_CLASS 범위(0..9)
            public int SessionFlags;    // ★ 실제로 쓰는 값. {0,1} 밖은 전부 "모름"
        }

        [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true,
            EntryPoint = "WTSQuerySessionInformationW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WTSQuerySessionInformation(IntPtr hServer, uint sessionId,
            int infoClass, out IntPtr ppBuffer, out uint pBytesReturned);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

        // ---- (B) UAC 보안 데스크톱 — user32 --------------------------------------------------

        /// <summary><c>DESKTOP_READOBJECTS</c> — 우리가 요구하는 <b>최소 권한</b>. 쓰기 권한은 요구하지 않는다.</summary>
        private const uint DesktopReadObjects = 0x0001;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(uint flags,
            [MarshalAs(UnmanagedType.Bool)] bool inherit, uint desiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseDesktop(IntPtr desktop);

        // ------------------------------------------------------------------------------------

        private bool _warnedOnce;

        /// <summary>세션 잠금 조회가 이 실행에서 한 번이라도 예외를 던졌으면 다시 시도하지 않는다.
        /// 실패는 "잠기지 않음"으로 안전하게 폴백한다 — macOS 쪽 <c>_lowPowerProbeBroken</c>과 같은 형태.
        /// ★ 이 래치가 <b>이 조회 하나만</b> 끄고, 무입력/전원 관측은 계속 살아 있게 하는 것이 중요하다.
        /// 전체를 <c>TryGetPresence</c>의 catch에 맡기면 WTS 실패 하나가 Away 등급까지 통째로 꺼 버린다.</summary>
        private bool _sessionProbeBroken;

        /// <summary>레이아웃 검산이 처음 깨졌을 때 한 번만 경고한다(그 뒤로는 조용히 "모름").</summary>
        private bool _warnedLayoutMismatch;

        /// <summary>(B)가 고착됐다고 판정해 신뢰를 끊은 사실을 한 번만 알린다.</summary>
        private bool _warnedSecureDesktopStuck;

        /// <summary>우리 세션 ID(검산용). 0xFFFFFFFF = 아직 모름/조회 실패.</summary>
        private uint _ownSessionId = WtsCurrentSession;
        private bool _ownSessionIdResolved;

        /// <summary>(B)가 "보안 데스크톱"이라고 답하기 시작한 시각(<c>GetTickCount</c>). 신뢰 시한 계산용.</summary>
        private uint _secureDesktopSinceTick;
        private bool _secureDesktopActive;

        public bool TryGetPresence(out ViewerPresenceSnapshot snapshot)
        {
            snapshot = default;
            try
            {
                var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
                float idleSeconds = -1f;
                if (GetLastInputInfo(ref info))
                {
                    // dwTime/GetTickCount는 모두 32비트 밀리초 카운터이며 약 49.7일마다 한 바퀴 돈다.
                    // unchecked 뺄셈이면 한 바퀴 도는 순간에도 차이값은 여전히 옳다(부호 없는 랩어라운드).
                    uint delta = unchecked(GetTickCount() - info.dwTime);
                    idleSeconds = delta / 1000f;
                }

                bool onBattery = false;
                bool lowPower = false;
                if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS power))
                {
                    onBattery = power.ACLineStatus == 0;      // 255(알 수 없음)는 배터리로 치지 않는다.
                    lowPower = power.SystemStatusFlag == 1;   // 배터리 세이버 = macOS 저전력 모드에 대응.
                }

                // ★ DisplayAsleep은 여전히 false다(클래스 문서). Windows에서 이 스냅샷의 "아무도 안
                //   보고 있다" 축은 아래 세션 잠금이 담당한다.
                snapshot = new ViewerPresenceSnapshot(false, idleSeconds, lowPower, onBattery,
                    sessionLocked: ProbeSessionLocked());
                return true;
            }
            catch (Exception e)
            {
                if (!_warnedOnce)
                {
                    _warnedOnce = true;
                    Debug.LogWarning($"[프레임페이싱/presence] Windows 관측 실패({e.GetType().Name}) — " +
                        "적응형 프레임 등급을 끄고 항상 활성으로 동작합니다. " + e.Message);
                }
                return false;
            }
        }

        /// <summary>
        /// (A)를 주로, (B)를 보로 합쳐 "지금 사용자가 이 화면을 볼 수 없는 상태인가"를 답한다.
        ///
        /// <para><b>순서가 곧 신뢰도다</b>: (A)가 잠김을 <b>알고</b> 말하면 그대로 채택하고 (B)는 보지
        /// 않는다(조회 한 번 아낀다). (A)가 "안 잠김" 또는 "모름"일 때만 (B)로 UAC 보안 데스크톱을
        /// 확인한다 — 그 구간이 정확히 (A)가 못 보는 구간이다.</para>
        ///
        /// <para><b>(B)에 시한을 거는 이유</b>: <c>OpenInputDesktop</c>이 어떤 환경에서 <b>영구히</b>
        /// 실패하면 발판 스캔이 영원히 멈춘 채 낡은 캐시로 굳는다 — 이 라운드가 고치려는 버그를 스스로
        /// 만드는 것이다. 시한 판정 자체는 플랫폼 중립
        /// <see cref="SessionVisibilityPolicy.ShouldTrustSecureDesktopSignal"/>에 있다(그래야 Windows가
        /// 없는 이 개발 머신에서 <b>규칙을 실제로 실행해</b> 검증할 수 있다).</para>
        /// </summary>
        private bool ProbeSessionLocked()
        {
            if (_sessionProbeBroken) return false;
            try
            {
                if (TryQueryWtsSessionLocked(out bool wtsLocked) && wtsLocked)
                {
                    // 주 신호가 잠김을 말한다. 보조 신호의 시한 계측은 리셋해 둔다 — 잠금 화면 동안
                    // (B)도 당연히 실패하므로, 리셋하지 않으면 잠금 해제 직후에 시한이 이미 만료된
                    // 상태로 시작해 UAC 구간을 한 번 놓친다.
                    _secureDesktopActive = false;
                    return true;
                }

                if (!IsSecureDesktopActive())
                {
                    _secureDesktopActive = false;
                    return false;
                }

                if (!_secureDesktopActive)
                {
                    _secureDesktopActive = true;
                    _secureDesktopSinceTick = GetTickCount();
                }

                // 부호 없는 랩어라운드 뺄셈 — 49.7일 경계에서도 차이값은 옳다(위 무입력 계산과 같다).
                float elapsed = unchecked(GetTickCount() - _secureDesktopSinceTick) / 1000f;
                if (SessionVisibilityPolicy.ShouldTrustSecureDesktopSignal(elapsed)) return true;

                if (!_warnedSecureDesktopStuck)
                {
                    _warnedSecureDesktopStuck = true;
                    Debug.LogWarning("[세션가시성] OpenInputDesktop이 " +
                        $"{SessionVisibilityPolicy.SecureDesktopTrustSeconds:F0}초 넘게 계속 실패합니다 — " +
                        "UAC 프롬프트가 그렇게 오래 떠 있는 경우는 드물므로 이 보조 신호를 더 이상 " +
                        "믿지 않고 발판 스캔을 재개합니다(주 신호 WTS는 계속 씁니다). " +
                        "환경상 입력 데스크톱을 열 권한이 없는 것으로 보입니다.");
                }
                return false;
            }
            catch (Exception e)
            {
                _sessionProbeBroken = true;
                Debug.LogWarning($"[세션가시성] 세션 잠금 조회 실패({e.GetType().Name}) — " +
                    "이 실행에서는 잠금 감지를 끄고 항상 '보고 있음'으로 동작합니다" +
                    "(무입력/전원 관측은 그대로 유지). " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// (A) <c>WTSSessionInfoEx</c>의 <c>SessionFlags</c>를 <b>네 겹으로 검산한 뒤에만</b> 채택한다.
        ///
        /// <list type="number">
        /// <item><b>반환 크기</b>가 앞머리 구조체보다 작으면 즉시 포기(부분 복사 방지).</item>
        /// <item><b><c>Level == 1</c></b>이 아니면 <c>LEVEL1</c>로 해석하면 안 된다.</item>
        /// <item>★ <b><c>SessionId</c>가 우리 실제 세션 ID와 같은가</b> — 레이아웃이 어긋나면 이 값이
        ///   거의 확실히 불일치한다. <b>이 한 줄이 "쓰레기를 성공으로 읽는" 경로를 닫는다.</b></item>
        /// <item><b><c>SessionState</c>가 <c>WTS_CONNECTSTATE_CLASS</c> 범위(0..9)</b>인가.</item>
        /// </list>
        ///
        /// <para>그리고 <c>SessionFlags</c> 자체도 <b>{0,1} 밖이면 "모름"</b>이다. 넷을 다 통과해야
        /// <paramref name="locked"/>가 의미를 가지며, 그 밖에는 전부 false를 돌려주고 호출부가
        /// 보조 신호로 넘어간다.</para>
        /// </summary>
        private bool TryQueryWtsSessionLocked(out bool locked)
        {
            locked = false;

            if (!_ownSessionIdResolved)
            {
                _ownSessionIdResolved = true;
                _ownSessionId = ProcessIdToSessionId(GetCurrentProcessId(), out uint sid)
                    ? sid
                    : WtsCurrentSession;   // 조회 실패 = 검산 불가 표식
            }
            if (_ownSessionId == WtsCurrentSession) return false;   // 검산할 기준이 없으면 쓰지 않는다.

            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (!WTSQuerySessionInformation(IntPtr.Zero, WtsCurrentSession, WtsSessionInfoEx,
                        out buffer, out uint bytes))
                {
                    return false;
                }
                if (buffer == IntPtr.Zero) return false;

                int headSize = Marshal.SizeOf<WTSINFOEX_HEAD>();
                if (bytes < (uint)headSize) return false;   // (1) 부분 복사 방지

                var head = Marshal.PtrToStructure<WTSINFOEX_HEAD>(buffer);

                bool layoutOk = head.Level == 1u                                   // (2)
                    && head.SessionId == _ownSessionId                             // (3) ★ 핵심 검산
                    && head.SessionState >= 0 && head.SessionState <= WtsConnectStateMax;  // (4)

                if (!layoutOk)
                {
                    if (!_warnedLayoutMismatch)
                    {
                        _warnedLayoutMismatch = true;
                        Debug.LogWarning("[세션가시성] WTSINFOEX 검산 실패 — " +
                            $"Level={head.Level}(기대 1), SessionId={head.SessionId}(기대 {_ownSessionId}), " +
                            $"SessionState={head.SessionState}(기대 0~{WtsConnectStateMax}). " +
                            "구조체 레이아웃이 이 OS와 어긋난 것으로 보고 세션 잠금 감지를 '모름'으로 " +
                            "처리합니다(보수적으로 계속 스캔합니다).");
                    }
                    return false;
                }

                // {0,1} 밖은 전부 모름(Win7/2008R2 반전 세대의 다른 값 포함).
                if (head.SessionFlags != WtsSessionStateLock
                    && head.SessionFlags != WtsSessionStateUnlock)
                {
                    return false;
                }

                locked = head.SessionFlags == WtsSessionStateLock;
                return true;
            }
            finally
            {
                if (buffer != IntPtr.Zero) WTSFreeMemory(buffer);
            }
        }

        /// <summary>
        /// (B) 입력 데스크톱을 <b>읽기 권한으로 열어만 본다</b>. 열리면 우리 데스크톱이고,
        /// 열리지 않으면 잠금 화면 또는 UAC 보안 데스크톱(Winlogon)이 입력을 차지하고 있다는 뜻이다.
        /// 성공했으면 <b>반드시 닫는다</b> — 핸들을 흘리면 24시간 상주 앱에서 폴링당 하나씩 샌다.
        /// </summary>
        private static bool IsSecureDesktopActive()
        {
            IntPtr desktop = OpenInputDesktop(0, false, DesktopReadObjects);
            if (desktop == IntPtr.Zero) return true;
            CloseDesktop(desktop);
            return false;
        }
    }
}
#endif
