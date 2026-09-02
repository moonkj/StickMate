#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using StickMate.Platform;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// ★★ 2026-09-02 — 작업표시줄 자동 숨김 <b>사실 조회 + 최소 쓰기 한 건</b>.
    /// 이 앱에서 <b>시스템 전역 설정을 바꾸는 유일한 파일</b>이고, 그래서 다른 어떤 것도 여기 없다.
    ///
    /// ============================================================================
    /// ★ 원칙 3의 승인된 예외 — 경위를 여기에도 적는다(코드만 보는 사람을 위해)
    /// ============================================================================
    /// CLAUDE.md 절대 불변 원칙 3은 "실제 파일/아이콘/<b>타 윈도우</b>는 절대 이동·삭제·수정하지
    /// 않는다"이다. 작업표시줄도 남의 창이고 자동 숨김 해제는 전역 설정 변경이라, 이 파일은
    /// 그 원칙이 금지하는 방향으로 <b>한 칸</b> 열려 있다.
    ///
    /// <para>이것은 실수가 아니다. 2026-09-02에 리더가 사용자에게 충돌을 명시하고 선택지 3개를
    /// 제시했으며, 사용자가 <b>"실행 중에만 바꾸고 종료 시 원복"</b>을 택했다. 사용자 지시 원문:
    /// <i>"일단 우리 프로그램을 실행하면 작업표시줄 숨김처리가 되어 있어도 강제로 보이게 해야함"</i>.
    /// CLAUDE.md 원칙 3에 예외 조항이 명시되어 있고 상세 근거는 <c>docs/TASKBAR_REVEAL.md</c>에 있다.
    /// <b>이 파일을 "원칙 위반"으로 판단해 되돌리기 전에 반드시 그 문서를 먼저 읽을 것.</b></para>
    ///
    /// ============================================================================
    /// 왜 <c>Win32WindowService</c>에 넣지 않았는가
    /// ============================================================================
    /// 그 클래스는 <c>IReservedBottomBarService</c>를 구현하고, 그 인터페이스 문서가
    /// <b>"이 인터페이스의 구현체는 작업표시줄을 바꾸는 API를 절대 부르지 않는다"</b>고 못박고 있다.
    /// 같은 타입이 읽기 계약과 쓰기 능력을 겸하면 그 문장이 즉시 거짓이 되고, 다음 사람이 조회
    /// 경로 옆에 쓰기를 하나 더 붙이는 것이 자연스러워진다. 능력을 <b>다른 타입, 다른 파일</b>에
    /// 두는 것이 그 문장을 참으로 유지하는 유일한 방법이다.
    ///
    /// ============================================================================
    /// 여기에 <b>없는</b> 것 — 감사가 기계적으로 지킨다
    /// ============================================================================
    /// <c>ABM_SETPOS</c>(막대 이동/크기 변경) · <c>ABM_NEW</c>/<c>ABM_SETAUTOHIDEBAR</c>(우리를
    /// 도킹 앱바로 등록해 화면을 예약하는 것) · <c>ABM_REMOVE</c> · 셸 프로세스 재시작.
    /// 전부 <c>Tests/EditMode/UserAssetImmutabilityAuditTests</c>의 금지 목록에 있고 예외가 없다.
    /// 이 파일이 예외를 받은 것은 <c>ABM_SETSTATE</c> 한 건뿐이며, 그 예외조차 라인 단위로 재검증된다.
    ///
    /// ============================================================================
    /// 창 핸들이 필요 없다 — 그래서 <c>BeforeSceneLoad</c>에서 돌 수 있다
    /// ============================================================================
    /// 상태 조회/설정 메시지는 <c>APPBARDATA.cbSize</c>(와 설정 시 <c>lParam</c>)만 읽는다. 오버레이
    /// 창(<c>Win32WindowService._overlayHwnd</c>)이 잡히기 전에 끝낼 수 있고, 그래야 작업표시줄
    /// 실측이 처음부터 "막대 있음"을 보게 된다(타이밍 근거는
    /// <see cref="ReservedBarRevealDirector"/> 클래스 문서).
    /// </summary>
    public sealed class WindowsReservedBarAutoHideControl : IReservedBarAutoHideControl
    {
        #region Win32 선언 (이 리전 밖으로 유출 금지)

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        /// <summary>자동 숨김/항상위 <b>상태 조회</b>. 읽기 전용이다.</summary>
        private const uint ABM_GETSTATE = 0x00000004;

        /// <summary>★ 이 앱의 유일한 시스템 전역 쓰기(원칙 3의 승인된 예외, 위 클래스 문서 참고).</summary>
        private const uint ABM_SETSTATE = 0x0000000A;

        private const int ABS_AUTOHIDE = 0x00000001;
        private const int ABS_ALWAYSONTOP = 0x00000002;

        #endregion

        public string PlatformTag => "Windows";

        public bool TryReadAutoHide(out bool autoHideEnabled)
        {
            autoHideEnabled = false;
            if (!TryGetState(out int state)) return false;

            autoHideEnabled = (state & ABS_AUTOHIDE) != 0;
            return true;
        }

        /// <summary>
        /// 자동 숨김 비트만 바꾼다.
        ///
        /// <para><b>다른 비트는 읽은 그대로 보존한다</b>(<c>ABS_ALWAYSONTOP</c> 등). 리더 지시는
        /// "<c>ABS_ALWAYSONTOP</c>을 적용"이었고, 자동 숨김이 켜져 있는 실제 상황에서 두 방식의
        /// 결과는 <b>같다</b>(조회값 <c>ALWAYSONTOP|AUTOHIDE</c>에서 <c>AUTOHIDE</c>만 내리면
        /// 남는 것이 정확히 <c>ALWAYSONTOP</c>이다). 보존하는 쪽을 고른 이유는 <b>모르는 비트를
        /// 지우지 않기 위해서</b>다 — 이 상태 워드는 OS 버전마다 보고 내용이 달랐던 이력이 있고,
        /// 우리가 건드리기로 승인받은 것은 자동 숨김 하나뿐이다. 조회에 실패했을 때만
        /// <c>ALWAYSONTOP</c> 기준값으로 조립한다(그때는 보존할 값을 모른다).</para>
        ///
        /// <para><b>반드시 되읽어 확인한다.</b> 이 설정 메시지는 성공/실패를 반환값으로 알려주지
        /// 않는다(항상 같은 값을 돌려준다). 반환값만 믿으면 실패를 성공으로 로그하게 되고, 그러면
        /// 원복 흔적이 "바꾸지도 않은 것"을 되돌리려 든다. 진실은 되읽기뿐이다 —
        /// 이 프로젝트는 캐시를 믿었다가 이미 한 번 크게 당했다
        /// (<c>Platform/TopmostRestorePolicy.cs</c>의 <c>IsTopmost</c> 캐시 절).</para>
        /// </summary>
        public bool TrySetAutoHide(bool autoHideEnabled)
        {
            int desired;
            if (TryGetState(out int current))
            {
                desired = autoHideEnabled ? (current | ABS_AUTOHIDE) : (current & ~ABS_AUTOHIDE);
            }
            else
            {
                desired = autoHideEnabled ? (ABS_ALWAYSONTOP | ABS_AUTOHIDE) : ABS_ALWAYSONTOP;
            }

            try
            {
                var data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
                data.lParam = new IntPtr(desired);
                SHAppBarMessage(ABM_SETSTATE, ref data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ReservedBarRevealDirector.LogTag} 작업표시줄 상태 설정 호출이 " +
                    $"실패했습니다: {ex.Message}. 시스템 설정은 바뀌지 않았습니다.");
                return false;
            }

            if (!TryReadAutoHide(out bool after))
            {
                Debug.LogWarning($"{ReservedBarRevealDirector.LogTag} 상태를 쓴 뒤 되읽지 못했습니다 — " +
                    "반영 여부를 확인할 수 없어 '실패'로 처리합니다(확인 못 한 변경을 성공으로 " +
                    "기록하면 원복 흔적이 거짓말을 하게 됩니다).");
                return false;
            }

            if (after != autoHideEnabled)
            {
                Debug.LogWarning($"{ReservedBarRevealDirector.LogTag} 상태를 " +
                    $"{ReservedBarRevealPolicy.DescribeAutoHide(autoHideEnabled)}(으)로 요청했지만 " +
                    $"OS는 여전히 {ReservedBarRevealPolicy.DescribeAutoHide(after)}입니다. " +
                    "셸이 요청을 무시했을 수 있습니다(정책/그룹 정책/셸 대체 등).");
                return false;
            }

            return true;
        }

        /// <summary>상태 워드를 읽는다. 예외는 밖으로 내보내지 않는다 — 기동 경로에서 도는 코드다.</summary>
        private static bool TryGetState(out int state)
        {
            state = 0;
            try
            {
                var data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
                state = (int)SHAppBarMessage(ABM_GETSTATE, ref data).ToInt64();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ReservedBarRevealDirector.LogTag} 작업표시줄 상태 조회에 " +
                    $"실패했습니다: {ex.Message}.");
                return false;
            }
        }
    }
}
#endif
