#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// ★ 2026-09-03 — 우리 오버레이 창을 <b>앱 전환 표면에서 빼는</b> 실행부.
    /// 판정은 한 줄도 하지 않는다 — 규칙 전부와 "이 조작이 무엇을 못 하는가"까지
    /// 플랫폼 중립 <see cref="StickMate.Platform.AppSwitcherPresencePolicy"/>에 있다
    /// (CLAUDE.md: 정책은 중립 위치, 플랫폼 전용 코드는 사실 조회와 실행만).
    ///
    /// ============================================================================
    /// ★ 이 파일은 Win32 <b>쓰기</b>를 직접 하지 않는다 (2026-09-03 정정)
    /// ============================================================================
    /// 하는 일은 <b>읽고(<c>GetWindowLongPtrW</c>) · 판정하고(중립 정책) · 대행을 시키는</b> 것뿐이다.
    /// 실제 <c>SetWindowLongPtr</c>은 <see cref="WindowsLayeredHybridResolver.TryAddExStyleBits"/>가
    /// 한다 — 이 저장소는 <b>자기 창 스타일 쓰기 API를 그 한 파일에 가둬 두고</b>
    /// (<c>LayeredHybridPolicyTests.자기창_스타일_쓰기_API는_해소기_한_파일에만_있다</c>),
    /// 그 규약이 생긴 이유가 <b>바로 그 해소기가 검증 실패로 영구 비활성된 사고</b>다.
    /// 자기 창 스타일을 여러 곳에서 쓰면 누가 무엇을 되돌렸는지 아무도 모른다.
    ///
    /// <para>★ 초안은 여기에 <c>SetWindowLongPtrW</c>/<c>SetWindowLongW</c>를 직접 두었고
    /// <b>그 감사가 러너에서 잡아냈다</b>(2026-09-03, 연속 빨강 2회). 예외를 등재하는 대신
    /// 감사의 실패 메시지가 지시한 대로 <b>쓰기를 그 파일로 옮겼다</b>. 대행 창구는
    /// <b>비트를 켜기만</b> 하므로, 여기서 실수로 <c>WS_EX_TRANSPARENT</c>를 지워 클릭 관통
    /// (절대 불변 원칙 2)을 깨뜨리는 일이 <b>구조적으로 불가능하다</b> — 초안보다 안전해졌다.</para>
    ///
    /// <para>대상은 언제나 <b>우리 자신의 창</b>이고 핸들 출처는 하나
    /// (<see cref="UniWinCNativeHandle.Resolve"/>)다. 타 프로세스 창은 읽지도 않고,
    /// 위치/크기/Z-order/수명에는 손대지 않는다(절대 불변 원칙 3).
    ///
    /// <para><b>이것은 앱바 조작이 아니다.</b> 셸 앱바 메시지(<c>SHAppBarMessage</c> 계열)를
    /// 부르는 줄이 이 파일에 <b>0줄</b>이다 — 지금 이 문장 말고는 그 이름이 코드에 등장하지도
    /// 않으며, 그 사실을 <c>AppSwitcherPresenceTests</c>가 <b>주석을 걷어낸 뒤</b> 다시 센다
    /// (주석에 이름을 인용했다고 감사가 빨개지면 다음 사람이 사실을 지운다 —
    /// <c>UserAssetImmutabilityAuditTests</c>가 같은 이유로 주석 줄을 비우고 본다).
    /// 작업표시줄 자동 숨김 해제(원칙 3의 <b>승인된 예외 1건</b>,
    /// <c>WindowsReservedBarAutoHideControl</c>)는 <b>셸의 막대</b>를 건드리는 일이고, 여기는
    /// <b>우리 창</b>의 스타일 비트다. 두 일을 한 낱말로 부르면 승인 범위가 조용히 넓어진다 —
    /// 그 구분을 <c>AppSwitcherPresenceTests</c>가 기계적으로 못박는다.</para>
    ///
    /// ============================================================================
    /// ★ 정직하게 — 이 조작이 <b>못 하는 것</b>
    /// ============================================================================
    /// 이미 보여진 창에 스타일을 얹는 것은 <b>Alt+Tab 목록에서는 즉시 듣고, 작업표시줄 버튼은
    /// 지우지 못한다</b>(Microsoft 문서: 스타일을 동적으로 바꾸려면 <c>SW_HIDE</c> → 스타일 →
    /// 다시 보이기). 근거 전문은 중립 정책의 클래스 문서에 있다.
    ///
    /// <para><b>그 나머지 절반은 형제 파일이 닫는다</b> — <see cref="WindowsTaskbarButtonRemover"/>가
    /// <c>ITaskbarList::DeleteTab</c>으로 <b>창 상태를 한 비트도 건드리지 않고</b> 버튼만 지운다
    /// (2026-09-03 리더 판정 (b); <c>ShowWindow</c> 왕복 (a)는 재적용 감시자들과의 충돌로 기각).
    /// 두 기전을 <b>다른 파일</b>에 둔 이유는 각 파일이 "쓰기 한 종류"라는 보증을 지키기 위해서다.</para>
    ///
    /// <para><b>이 파일의 로그는 자기 몫만 말한다</b> — 실기 로그를 읽는 사람이 어느 기전이 무엇을
    /// 했는지 가릴 수 있어야 한다. 버튼 쪽 결과는 <c>[작업표시줄버튼]</c> 줄에 따로 찍힌다.</para>
    ///
    /// ============================================================================
    /// 24시간 상주 비용
    /// ============================================================================
    /// 안정 상태의 틱 하나는 <c>IsWindow</c> + <c>GetWindowLongPtrW</c> <b>각 1회</b>이고 주기는
    /// <see cref="ReassertIntervalSeconds"/>(2초)다 = 초당 0.5회. 같은 <c>Update</c>에서 이미
    /// <see cref="WindowsLayeredHybridResolver"/>가 초당 4회, <see cref="WindowsTopmostWatchdog"/>이
    /// 초당 10회 같은 API를 부르고 있으므로 증분은 그 3.4%다. 쓰기는 <b>비트가 실제로 빠져
    /// 있을 때만</b> 일어나므로 정상 경로에서는 프로세스 수명 동안 1회다. 매 틱 할당은 0이다.
    ///
    /// <para><b>왜 "한 번 걸고 끝"이 아닌가</b>: 라이브러리는 커서가 캐릭터를 드나들 때마다
    /// <c>SetClickThrough</c>로 같은 <c>GWL_EXSTYLE</c>을 되쓴다. 그 구현은 읽고-고쳐-쓰기라
    /// (<c>exstyle |= WS_EX_TRANSPARENT; exstyle |= WS_EX_LAYERED;</c>) <b>우리 비트를 보존할
    /// 것으로 판단</b>하지만, 이 머신에는 Windows가 없어 <b>실행으로 확인할 수 없다.</b>
    /// 그래서 확인 대신 <b>감시</b>를 둔다 — 비트가 사라지면 다시 켜고, 그 사실을 <b>한 번</b>
    /// 경고로 남긴다. 추측을 코드에 박는 대신 현장에서 스스로 보고하게 만드는 쪽이다.</para>
    /// </summary>
    internal sealed class WindowsToolWindowStyleControl
    {
        internal const string LogPrefix = "[전환기제외]";

        /// <summary>비트가 아직 서 있는지 다시 묻는 주기(초).</summary>
        private const float ReassertIntervalSeconds = 2f;

        private const int GwlExStyle = -20;

        private IntPtr _hwnd = IntPtr.Zero;
        private float _timer;

        /// <summary>비트가 서 있는 것을 <b>한 번이라도</b> 확인했는가.</summary>
        private bool _standing;

        private bool _appliedLogged;
        private bool _failureLogged;
        private bool _clearedWarned;

        /// <summary>비트가 사라져 다시 켠 횟수(프로세스 누적). 0에서 멈춰 있어야 정상이다.</summary>
        private int _reassertCount;

        /// <summary>진단용 — 지금 비트가 서 있다고 관측됐는가.</summary>
        internal bool IsStanding => _standing;

        /// <summary>
        /// 창 부착이 확인된 <b>바로 그 프레임</b>에 1회 호출한다. macOS판이 같은 자리에서
        /// <c>MacSpaceBehaviorNative.ApplyAccessoryActivationPolicyOnce()</c>를 부르는 것과
        /// <b>같은 지점·같은 목적</b>이다(그 대칭을 <c>PlatformParityAuditTests</c>가 잠근다).
        /// </summary>
        internal void ApplyOnce()
        {
            EnsureBitStanding();
        }

        /// <summary>
        /// 매 프레임 호출. 내부에서 주기를 지킨다. 재적용 상한(<c>ReapplyAttempts</c>)과 무관하게
        /// 앱 수명 내내 돈다 — 위 클래스 문서의 "왜 한 번 걸고 끝이 아닌가" 참고.
        /// </summary>
        internal void Tick(float unscaledDeltaTime)
        {
            _timer += unscaledDeltaTime;
            if (_timer < ReassertIntervalSeconds) return;
            _timer = 0f;
            EnsureBitStanding();
        }

        private void EnsureBitStanding()
        {
            // 핸들은 인자로 받지 않고 스스로 얻는다 — 대상은 "라이브러리가 실제로 붙잡은 그 창"이고,
            // 호출부가 들고 있는 Process.MainWindowHandle이 그 창이라는 보장이 없다
            // (UniWinCNativeHandle 클래스 문서. 두 값이 다르면 그쪽이 한 번 경고를 남긴다).
            if (_hwnd == IntPtr.Zero || !IsWindowSafe(_hwnd)) _hwnd = UniWinCNativeHandle.Resolve();
            IntPtr hwnd = _hwnd;
            if (hwnd == IntPtr.Zero) return;

            bool readOk = TryReadExStyle(hwnd, out long before);
            AppSwitcherStyleVerdict verdict = AppSwitcherPresencePolicy.DecideToolWindowApply(readOk, before);

            if (verdict == AppSwitcherStyleVerdict.Unknown)
            {
                LogFailureOnce($"확장 스타일 실측 실패(hwnd=0x{hwnd.ToInt64():X}) — " +
                    "모를 때는 아무것도 쓰지 않습니다(되쓰면 클릭 관통 비트를 날릴 수 있습니다).");
                return;
            }

            if (verdict == AppSwitcherStyleVerdict.AlreadyOut)
            {
                if (!_standing)
                {
                    _standing = true;
                    LogAppliedOnce(before, before, wroteThisTime: false);
                }
                return;
            }

            // 여기부터가 "켜야 한다". 이미 한 번 서 있었는데 다시 여기 왔다면 누군가 지운 것이다.
            if (_standing && !_clearedWarned)
            {
                _clearedWarned = true;
                Debug.LogWarning($"{LogPrefix} ★ 한 번 섰던 {AppSwitcherPresencePolicy.WindowsMechanismName}가 " +
                    $"사라졌습니다(현재 exStyle=0x{before:X}). 이 저장소는 그런 일이 <없을 것으로 판단>하고 " +
                    "있었습니다 — 라이브러리의 SetClickThrough/알파 경로는 GWL_EXSTYLE을 읽고-고쳐-쓰기 하고 " +
                    "SetBorderless는 GWL_STYLE(다른 인덱스)을 쓰므로 우리 비트를 보존해야 합니다. " +
                    "이 줄이 찍혔다면 그 전제가 반증된 것이니, 다음 라운드는 누가 절대값으로 되쓰는지부터 " +
                    $"찾으세요(후보: 라이브러리 갱신, 타 오버레이 도구, OS 정책). 지금은 다시 켜 둡니다 " +
                    $"(재적용 누적 {_reassertCount + 1}회).");
            }

            long next = AppSwitcherPresencePolicy.ComposeToolWindowExStyle(before);

            // ★ 쓰기는 <b>우리가 직접 하지 않는다</b> — 자기 창 스타일 쓰기 API는 저장소 규약상
            //   WindowsLayeredHybridResolver 한 파일에만 있고, 그 규약은
            //   LayeredHybridPolicyTests.자기창_스타일_쓰기_API는_해소기_한_파일에만_있다가 지킨다.
            //   그 창구는 <비트를 켜기만> 하므로 여기서 실수로 WS_EX_TRANSPARENT를 지워
            //   클릭 관통(원칙 2)을 깨뜨리는 일이 <구조적으로> 불가능하다.
            if (!WindowsLayeredHybridResolver.TryAddExStyleBits(
                    hwnd, AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit,
                    out long _, out long after))
            {
                LogFailureOnce($"확장 스타일 쓰기가 실패했습니다(hwnd=0x{hwnd.ToInt64():X}, " +
                    $"목표 exStyle=0x{next:X}).");
                return;
            }

            bool verified = AppSwitcherPresencePolicy.VerifyApplied(after != 0L, after);

            if (!verified)
            {
                LogFailureOnce($"되읽기 검증 실패 — 썼는데 비트가 서지 않았습니다 " +
                    $"(전=0x{before:X}, 목표=0x{next:X}, 후=0x{after:X}). " +
                    "쓰기 반환값을 믿지 않고 되읽는 이유가 이것입니다.");
                return;
            }

            if (_standing) _reassertCount++;
            _standing = true;
            LogAppliedOnce(before, after, wroteThisTime: true);
        }

        /// <summary>
        /// 성공 로그는 <b>한 번만</b>이다(24시간 상주 앱). 그리고 <b>이 조작이 못 하는 것</b>을
        /// 같은 줄에 적는다 — 실기 로그를 읽는 사람이 "버튼이 왜 남아 있지"에서 막히면
        /// 다음 신고가 그것이 된다.
        /// </summary>
        private void LogAppliedOnce(long before, long after, bool wroteThisTime)
        {
            if (_appliedLogged) return;
            _appliedLogged = true;

            bool altTabNow = AppSwitcherPresencePolicy.TakesEffectOnAlreadyShownWindow(
                AppSwitcherSurface.AltTabList);
            bool taskbarNow = AppSwitcherPresencePolicy.TakesEffectOnAlreadyShownWindow(
                AppSwitcherSurface.TaskbarButton);

            Debug.Log($"{LogPrefix} {AppSwitcherPresencePolicy.WindowsMechanismName} " +
                $"{(wroteThisTime ? "부여" : "이미 서 있음")} — exStyle 0x{before:X} -> 0x{after:X} " +
                $"(비트 0x{AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit:X} 하나만 켜고 나머지는 보존). " +
                $"macOS의 {AppSwitcherPresencePolicy.MacOsMechanismName}와 같은 목적입니다 — " +
                "이 앱은 원래 앱 전환기에 나타나지 않는 오버레이이고, Windows만 예외였습니다.\n" +
                $"  · Alt+Tab 목록: {(altTabNow ? "지금부터 제외됩니다" : "변화 없음")}\n" +
                $"  · 작업표시줄 버튼: {(taskbarNow ? "이 비트로 제외됩니다" : "★ 이 비트로는 안 빠집니다")} " +
                "— 셸은 창이 보여질 때 버튼을 만들고 그 뒤의 스타일 변경을 통보받지 않습니다(Microsoft 문서). " +
                $"그쪽은 {nameof(WindowsTaskbarButtonRemover)}가 ITaskbarList로 따로 지웁니다 — " +
                "결과는 [작업표시줄버튼] 줄을 보세요.\n" +
                "  · 앱이 안 보이면 <b>다시 실행</b>하세요 — 사용자 직접 숨김은 저장되지 않으므로 재실행이 확실한 탈출구입니다.");
        }

        private void LogFailureOnce(string why)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            Debug.LogWarning($"{LogPrefix} {why} 이 실패는 <앱 전환기에 계속 보인다>는 뜻일 뿐이고 " +
                "투명/항상위/클릭 관통에는 영향이 없습니다(정직한 실패 보고용 로그).");
        }

        // ==================== Win32 (자기 창 전용) ====================

        private static bool IsWindowSafe(IntPtr hwnd)
        {
            try { return IsWindow(hwnd); }
            catch (EntryPointNotFoundException) { return false; }
            catch (DllNotFoundException) { return false; }
        }

        /// <summary>우리 창의 확장 스타일 실측. 실패의 뜻은 "지금은 모른다"다.</summary>
        private static bool TryReadExStyle(IntPtr hwnd, out long exStyle)
        {
            exStyle = 0L;
            try
            {
                if (!IsWindow(hwnd)) return false;
                exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
                // GetWindowLongPtr은 실패를 0으로 알린다. 우리 오버레이는 항상 최소
                // WS_EX_TOPMOST/WS_EX_TRANSPARENT 중 하나를 갖고 있으므로 0은 사실상 실패다
                // (WindowsLayeredHybridResolver.TryReadExStyle과 같은 판정).
                return exStyle != 0L;
            }
            catch (EntryPointNotFoundException) { return false; }
            catch (DllNotFoundException) { return false; }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        /// <summary>32비트 플레이어에는 <c>GetWindowLongPtrW</c>가 아예 없다(별칭이 아니다).
        /// <c>WindowsWindowStyleProbe</c>/<c>WindowsLayeredHybridResolver</c>와 동일한 분기.
        /// <b>읽기만 있다</b> — 쓰기 짝은 이 파일에 두지 않는다(위 규약).</summary>
        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
            => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }
}
#endif
