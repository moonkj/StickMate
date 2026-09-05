#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// ★ 2026-09-03 (사용자 확정 <i>"실행시 시스템 트레이에 표시되어야함"</i>) —
    /// <c>Shell_NotifyIcon</c>으로 시스템 트레이 아이콘과 그 컨텍스트 메뉴를 세운다.
    ///
    /// ============================================================================
    /// 왜 필요한가 — 지금 이 앱에는 <b>OS 레벨 존재 증거가 하나도 없다</b>
    /// ============================================================================
    /// 사용자가 같은 날 <b>두 번</b> 종료 버튼을 찾지 못했다(<i>"종료버튼 어딨어"</i>). 종료 경로는
    /// 3중인데(전역 단축키 · 톱니 부채꼴 · 설정창) 셋 다 <b>창을 한 번도 열어 본 적 없는 사용자에게는
    /// 발견 불가능</b>하다 — 그 사실은 <c>Interaction/AppControlDirector.cs</c>가 자기 클래스 문서에
    /// 이미 적어 두고 있었다.
    ///
    /// <para>게다가 같은 날 <see cref="WindowsToolWindowStyleControl"/>이 Alt+Tab 항목을,
    /// <see cref="WindowsTaskbarButtonRemover"/>가 작업표시줄 버튼을 없앴다. 그 라운드가 남긴
    /// "갚아야 할 대가"(<see cref="StickMate.Platform.AppSwitcherPresencePolicy"/> 클래스 문서)가
    /// 바로 이 빚이다. <b>트레이 아이콘이 그 유일한 OS 레벨 존재 증거가 된다.</b></para>
    ///
    /// ============================================================================
    /// ★★★ 승인된 예외 — <b>전역 금지 API 3종을 이 파일에서만</b> 쓴다 (2026-09-03 리더 승인)
    /// ============================================================================
    /// <b>이 코드를 "원칙 2/3 위반"으로 오해해 되돌리기 전에 이 절을 끝까지 읽어라.</b>
    /// <c>docs/TASKBAR_REVEAL.md</c>가 세운 선례(작업표시줄 자동 숨김 예외)와 <b>같은 형식</b>이며,
    /// 그때가 «파일 1개 · 형태 2개»였다면 이번은 <b>«파일 1개 · 형태 3개»</b>다.
    ///
    /// <para><b>무엇이 금지돼 있었나.</b> <c>Tests/EditMode/UserAssetImmutabilityAuditTests</c>는
    /// 아래 셋을 프로젝트 전역에서 화이트리스트 없이 금지하고 있었고, 하필 그 셋이
    /// "교과서적 트레이 구현"의 부품이다:
    /// <list type="bullet">
    ///   <item><c>SetForegroundWindow(</c> — 금지 사유: <i>"타 윈도우를 강제로 앞으로 끌어와
    ///     포커스를 빼앗는 API"</i>(원칙 2).</item>
    ///   <item><c>PostMessage(</c> — 금지 사유: <i>"WM_CLOSE / WM_SYSCOMMAND 한 줄이면 남의 창을
    ///     닫거나 옮길 수 있다"</i>.</item>
    ///   <item><c>DestroyWindow(</c> — 금지 사유: <i>"타 윈도우를 강제 종료시키는 API"</i>(원칙 3).</item>
    /// </list></para>
    ///
    /// <para><b>왜 이번엔 예외가 성립하는가.</b> 세 금지 사유의 주어가 전부 <b>«타 윈도우»</b>다.
    /// 그런데 이 파일의 세 호출은 <b>전부 우리가 <c>CreateWindowEx</c>로 직접 만든, 한 번도 보이지
    /// 않는 트레이 호스트 창</b> 하나에만 적용된다. 남의 창은 <b>한 번도 건드리지 않는다</b> —
    /// 그것이 승인의 핵심 조건이다. 감사가 이것을 스스로 구분하지 못했던 이유는 단순하다:
    /// <b>니들이 문자열이라 자타를 구분할 수 없다.</b> 규칙의 취지가 아니라 표현이 넓었을 뿐이다.</para>
    ///
    /// <para><b>범위가 얼마나 좁은가.</b> 허용되는 것은 <b>이 파일 안의 6줄</b>뿐이다 —
    /// 각 API마다 <c>extern</c> 선언 1줄 + 호출 1줄. 그 6줄은
    /// <c>UserAssetImmutabilityAuditTests</c>가 <b>문자열 완전 일치</b>로 라인 단위 재검증하며
    /// (접두/접미 일치가 아니다 — 작업표시줄 예외가 그 함정에 한 번 빠진 적이 있다),
    /// 다른 어떤 파일에서도 이 세 이름은 여전히 <b>즉시 실패</b>다.
    /// <list type="number">
    ///   <item><b>인자가 고정돼 있다.</b> 세 호출 모두 인자가 <see cref="_hostWindow"/> 하나다.
    ///     이 필드에는 우리가 만든 창만 들어간다 — 외부에서 임의 핸들을 주입할 경로가 없다.
    ///     <b>«임의의 창»을 받을 수 있는 편의 오버로드를 만들지 마라.</b> 승인 조건이 거기 걸려 있다.</item>
    ///   <item><b><c>DestroyWindow</c>는 <see cref="OnQuitting"/>에서만 불린다</b>
    ///     (<c>Application.quitting</c>). 유일한 호출자가
    ///     <see cref="DestroyHostWindow"/>이고, 그 함수의 유일한 호출자가 종료 훅이다.</item>
    ///   <item><b><c>SetForegroundWindow</c> + <c>PostMessage(WM_NULL)</c>은 KB135788 그대로다.</b>
    ///     Microsoft가 문서화한 관용구이며 <b>임의 변형을 금지</b>한다 — 이 둘이 빠지면 메뉴가
    ///     바깥 클릭으로 닫히지 않는다.</item>
    /// </list></para>
    ///
    /// <para><b>확장 금지.</b> 예외는 <b>이 세 이름 · 이 한 파일</b>에만 내려졌다. "이왕 창을 만지는
    /// 김에"가 가장 자연스러운 다음 걸음이고 그 순간 원칙 2/3이 조용히 무너진다 —
    /// <c>ShowWindow(</c> · <c>SetWindowPos(</c> · <c>MoveWindow(</c> 등 나머지는 <b>여기서도
    /// 여전히 금지</b>이며, 감사가 이 파일에 대해서도 그대로 문다.</para>
    ///
    /// ============================================================================
    /// 왜 <b>별도의 호스트 창</b>인가 — Unity 창을 건드리지 않는다
    /// ============================================================================
    /// 트레이 아이콘은 콜백을 받을 <c>HWND</c>가 필요하다. Unity 창(= <c>LibUniWinC</c>가 붙잡은
    /// 그 창)의 윈도우 프로시저를 <b>서브클래싱하지 않는다</b>: 그 창은 지금
    /// <see cref="WindowsOverlayStateEnforcer"/> · <see cref="WindowsTopmostWatchdog"/> ·
    /// <see cref="WindowsLayeredHybridResolver"/> 셋이 동시에 스타일을 다투는 창이고, 이 저장소에는
    /// <b>같은 종류의 충돌로 영구 비활성된 해소기가 이미 있다.</b> 그래서 우리 전용 창을 새로 만들고
    /// <b>한 번도 보이지 않는다</b>(<c>WS_VISIBLE</c> 없이 만들고, 보이게 하는 API는 어차피 금지다).
    ///
    /// <para>그 창에도 <c>WS_EX_TOOLWINDOW</c>를 얹는다 — 상수는
    /// <see cref="StickMate.Platform.AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit"/>에서
    /// 가져온다(자기 사본을 두지 않는다). 같은 날 작업표시줄/Alt+Tab에서 애써 빠져 놓고
    /// <b>보조 창으로 다시 나타나는</b> 것이 이 라운드의 가장 그럴듯한 자책골이다.</para>
    ///
    /// ============================================================================
    /// ★ Unity API를 <b>윈도우 프로시저 안에서 부르지 않는다</b>
    /// ============================================================================
    /// <c>TrackPopupMenu</c>는 <b>중첩 모달 메시지 루프</b>다 — 그 안에서는 Unity의 플레이어 루프가
    /// 멈춰 있다. 그래서 프로시저는 두 가지만 한다: <b>캐시된 bool로 메뉴를 그리고, 고른 명령을
    /// 적어 둔다.</b> 실제 배달(<see cref="StickMate.Platform.SystemTrayCommandRouter.Dispatch"/> →
    /// 씬 조회 → 기존 진입점 호출)은 다음 <see cref="Tick"/>에서, 즉 정상적인 <c>Update</c> 흐름
    /// 위에서 일어난다. 표시 상태(축 2 <c>StickmanAgent.IsUserHidden</c> — 2026-09-05에
    /// <c>IsUserHiddenOnly</c>에서 바로잡혔다)도 <see cref="Tick"/>이 미리 캐시해 둔다.
    ///
    /// <para><b>남는 대가</b>: 메뉴가 열려 있는 동안 캐릭터가 멈춘다(중첩 루프가 프레임을 막는다).
    /// 메뉴는 순간적인 표면이라 감수한다 — 대신 그 사실을 여기 적어 둔다.</para>
    ///
    /// ============================================================================
    /// 좀비 아이콘에 대하여 — <b>원장(ledger)으로는 못 고친다</b>
    /// ============================================================================
    /// 크래시로 <c>NIM_DELETE</c>를 못 부르면 셸이 죽은 아이콘을 한동안 그대로 그린다.
    /// <c>ReservedBarRestoreLedger</c>처럼 "다음 실행이 디스크 흔적을 보고 먼저 복구"하는 패턴을
    /// 검토했으나 <b>여기에는 성립하지 않는다</b>: <c>Shell_NotifyIcon</c>은 <c>(hWnd, uID)</c>로
    /// 아이콘을 지목하는데 그 <c>hWnd</c>는 <b>이미 죽은 프로세스의 것</b>이고, 셸은 다른
    /// 프로세스의 창을 가리키는 삭제 요청을 받아 주지 않는다. 즉 <b>다음 실행이 지울 수 있는
    /// 대상이 아니다.</b> 실제 정리는 셸이 한다 — 사용자가 트레이 위에 커서를 올리면 셸이 죽은
    /// 창의 아이콘을 그때 걷어낸다. <b>없는 방어를 만들어 두는 것보다 이 사실을 적어 두는 편이
    /// 정직하다.</b>
    /// </summary>
    internal static class WindowsSystemTrayIcon
    {
        internal const string LogPrefix = "[트레이]";

        /// <summary>설치 재시도 상한. 로그온 직후 셸 트레이가 아직 준비되지 않았을 수 있어 몇 번만
        /// 더 물어보되 <b>반드시 멈춘다</b>(24시간 상주 앱이다). 판정 자체는 중립 정책이 내린다.</summary>
        private const int MaxInstallAttempts = 5;

        /// <summary>설치 재시도 간격(초).</summary>
        private const float AttemptIntervalSeconds = 1f;

        /// <summary>호스트 창의 윈도우 클래스 이름. 프로세스 전역에서 유일하면 된다.</summary>
        private const string HostWindowClassName = "StickMateTrayHostWindow";

        private const uint WS_POPUP = 0x80000000;
        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_NONOTIFY = 0x0080;
        private const int IDI_APPLICATION = 32512;

        /// <summary><c>WM_NULL</c> — 받는 쪽이 <b>아무 일도 하지 않는</b> 메시지. KB135788이
        /// 메뉴 모달 루프를 정상적으로 풀기 위해 처방한 바로 그 값이다.</summary>
        private const uint WM_NULL = 0x0000;

        private static bool _optOutResolved;
        private static bool _optOut;
        private static bool _installed;
        private static bool _unavailable;
        private static bool _failureLogged;
        private static bool _installedLogged;
        private static bool _quitHookInstalled;
#if UNITY_EDITOR
        private static bool _editorSkipLogged;
#endif
        private static int _attempts;
        private static float _timer;

        private static IntPtr _hostWindow = IntPtr.Zero;
        private static IntPtr _icon = IntPtr.Zero;
        private static IntPtr _largeIcon = IntPtr.Zero;
        private static bool _iconIsShared;          // LoadIcon으로 얻은 시스템 아이콘은 파괴하지 않는다.
        private static WndProcDelegate _wndProc;    // ★ GC가 걷어가면 셸의 콜백이 죽은 함수로 뛴다.
        private static uint _shellRestartMessage;

        // 윈도우 프로시저 ↔ Tick 사이의 유일한 통로. 프로시저는 적기만, Tick은 읽고 비우기만 한다.
        private static bool _hasPendingCommand;
        private static TrayMenuCommand _pendingCommand;
        private static bool _shellRestartPending;

        /// <summary>프로시저가 읽는 <b>캐시된</b> 표시 상태. Tick이 갱신한다(위 클래스 문서 참고).</summary>
        private static bool _cachedCharacterHidden;

        /// <summary>진단용 — 아이콘이 실제로 서 있는가.</summary>
        internal static bool IsInstalled => _installed;

        /// <summary>진단용 — 이 경로가 <b>이미 못 쓴다고 확인된</b> 상태인가.</summary>
        internal static bool KnownUnavailable => _unavailable;

        /// <summary>
        /// 매 프레임 호출. <b>부착 대기와 무관하게</b> 돌아야 한다 — 오버레이 창이 영영 붙지 않는
        /// 환경에서야말로 트레이가 유일한 탈출구이기 때문이다(호출 지점 주석 참고).
        /// </summary>
        internal static void Tick(float unscaledDeltaTime)
        {
            if (!_optOutResolved)
            {
                _optOutResolved = true;
                _optOut = ResolveOptOut();
                if (_optOut)
                {
                    Debug.Log($"{LogPrefix} {SystemTrayPresencePolicy.OptOutEnvironmentVariable}가 " +
                        "설정되어 있어 트레이 아이콘을 만들지 않습니다 — 종료는 전역 단축키 · " +
                        "톱니 부채꼴 · 설정창 3중 경로를 쓰세요.");
                }
            }
            if (_optOut) return;

#if UNITY_EDITOR
            // ★★ 에디터에서는 <b>세우지 않는다</b> — 안전 때문이지 게으름이 아니다.
            //
            // 우리 호스트 창의 윈도우 프로시저는 <b>관리 델리게이트</b>다(_wndProc). 에디터는
            // 플레이 종료 때마다 <b>도메인을 리로드</b>해 관리 힙을 통째로 버리는데, 그러면 그
            // 델리게이트가 수거되고 <b>창은 그대로 살아남는다</b> — 그 창에 메시지가 하나라도
            // 도착하는 순간 죽은 함수 포인터로 뛴다(에디터 크래시).
            //
            // ★ 2026-09-03 정정 — 이 자리에 원래 <i>"DestroyWindow가 전역 금지라 창을 치울 손이
            //   없다"</i>고 적혀 있었다. 그 근거는 <b>같은 날 리더 승인으로 사라졌다</b>(위 클래스
            //   문서 "승인된 예외"). 이제 종료 시 창을 실제로 없앤다(DestroyHostWindow).
            //   <b>그런데 결론은 바뀌지 않는다</b> — 근거가 하나 더 있고, 그쪽이 더 질기다:
            //
            //   <b>윈도우 «클래스» 등록은 창보다 오래 산다.</b> RegisterClassEx는 프로세스 수명
            //   동안 유지되고 그 클래스의 lpfnWndProc에는 <b>관리 델리게이트의 함수 포인터</b>가
            //   박혀 있다. 도메인 리로드가 그 델리게이트를 수거해도 <b>클래스 등록은 남는다</b>.
            //   다음 플레이에서 같은 이름으로 RegisterClassEx를 부르면 ERROR_CLASS_ALREADY_EXISTS로
            //   실패하고, CreateWindowEx는 <b>죽은 포인터를 가진 옛 클래스</b>로 창을 만든다 —
            //   첫 메시지에 에디터가 죽는다. UnregisterClass까지 얽으면 정리 순서가 하나 더 늘고,
            //   그 복잡성을 24시간 상주 앱의 기동 경로에 넣을 이유가 없다.
            //
            // 잃는 것: Windows 에디터에서 트레이를 눈으로 확인할 수 없다 — <b>빌드로만</b> 확인한다.
            // 잃지 않는 것: 규칙(SystemTrayPresencePolicy)과 배선(SystemTrayCommandBridge)은
            // 플랫폼 중립이라 에디터에서도 그대로 실행·검증된다.
            if (!_editorSkipLogged)
            {
                _editorSkipLogged = true;
                Debug.Log($"{LogPrefix} 에디터에서는 트레이 아이콘을 만들지 않습니다 — 도메인 리로드가 " +
                    "윈도우 프로시저 델리게이트를 수거하는데 <b>윈도우 클래스 등록은 프로세스 수명 " +
                    "동안 남아</b>, 다음 플레이가 죽은 함수 포인터를 가진 옛 클래스로 창을 만들게 " +
                    "됩니다(에디터 크래시). <b>빌드된 플레이어에서 확인하세요.</b>");
            }
            return;
#else

            // (1) 프로시저가 적어 둔 명령을 <b>정상 Update 흐름 위에서</b> 배달한다.
            DrainPendingCommand();

            // (2) 셸이 재시작했으면 다시 세운다. 시도 상한과 일부러 분리돼 있다(중립 정책 참고).
            if (_shellRestartPending)
            {
                _shellRestartPending = false;
                if (SystemTrayPresencePolicy.ShouldReinstallAfterShellRestart(_optOut, _unavailable))
                {
                    _installed = false;
                    _attempts = 0;
                    _timer = AttemptIntervalSeconds;
                    Debug.Log($"{LogPrefix} 셸(explorer) 재시작을 통보받았습니다 — 트레이 아이콘을 다시 세웁니다. " +
                        "이 처리가 없으면 셸이 죽었다 살아난 뒤 이 앱은 OS 어디에도 보이지 않게 됩니다.");
                }
            }

            // (3) 메뉴 글자에 쓸 표시 상태를 미리 캐시한다(프로시저는 Unity를 부를 수 없다).
            if (_installed)
            {
                _cachedCharacterHidden = SystemTrayCommandRouter.IsCharacterHidden();
                return;
            }

            if (!SystemTrayPresencePolicy.ShouldAttemptInstall(
                    _optOut, _installed, _unavailable, _attempts, MaxInstallAttempts)) return;

            if (_attempts > 0)
            {
                _timer += unscaledDeltaTime;
                if (_timer < AttemptIntervalSeconds) return;
            }
            _timer = 0f;
            _attempts++;

            TryInstall();
#endif
        }

        private static void DrainPendingCommand()
        {
            if (!_hasPendingCommand) return;
            _hasPendingCommand = false;
            TrayMenuCommand command = _pendingCommand;
            SystemTrayCommandRouter.Dispatch(command, "트레이 메뉴");
        }

        // ====================================================================
        // 설치 / 해제
        // ====================================================================

        private static void TryInstall()
        {
            if (!EnsureHostWindow()) return;
            EnsureIcon();

            var data = BuildIconData(SystemTrayPresencePolicy.NotifyIconAddFlags);
            bool ok;
            try
            {
                ok = Shell_NotifyIcon(SystemTrayPresencePolicy.NotifyIconAdd, ref data);
            }
            catch (Exception e)
            {
                _unavailable = true;
                LogFailureOnce($"Shell_NotifyIcon 호출 중 예외({e.GetType().Name}: {e.Message}).");
                return;
            }

            if (!ok)
            {
                if (_attempts >= MaxInstallAttempts)
                {
                    _unavailable = true;
                    LogFailureOnce($"Shell_NotifyIcon(NIM_ADD)이 {MaxInstallAttempts}회 모두 실패했습니다 " +
                        "(셸 트레이가 준비되지 않았거나 정책으로 막혀 있을 수 있습니다).");
                }
                return;
            }

            _installed = true;
            InstallQuitHook();

            if (!_installedLogged)
            {
                _installedLogged = true;
                Debug.Log($"{LogPrefix} 시스템 트레이 아이콘을 세웠습니다(창 0x{_hostWindow.ToInt64():X}). " +
                    $"좌클릭 또는 우클릭으로 메뉴가 열립니다: {SystemTrayPresencePolicy.DescribeMenu(false)}. " +
                    "이 앱은 작업표시줄 버튼도 Alt+Tab 항목도 없으므로 <b>트레이가 유일한 OS 레벨 존재 " +
                    "증거</b>입니다. 끄려면 " + SystemTrayPresencePolicy.OptOutEnvironmentVariable + "=1.");
            }
        }

        /// <summary>
        /// 아이콘을 셸에서 뗀다. 여러 번 불려도 안전하다.
        /// <b>호스트 창은 건드리지 않는다</b> — 창 파괴 API는 프로젝트 전역 금지이고, 프로세스가
        /// 끝나면 OS가 알아서 정리한다(클래스 문서 "쓸 수 없는 API 셋" 참고).
        /// </summary>
        internal static void RemoveNow(string why)
        {
            if (!_installed) return;
            _installed = false;

            try
            {
                var data = BuildIconData(0);
                Shell_NotifyIcon(SystemTrayPresencePolicy.NotifyIconDelete, ref data);
            }
            catch (Exception)
            {
                // 종료를 막지 않는다. 남으면 셸이 커서를 올렸을 때 걷어낸다.
            }

            ReleaseIcons();
            if (!string.IsNullOrEmpty(why)) Debug.Log($"{LogPrefix} 트레이 아이콘 제거 — {why}.");
        }

        private static void InstallQuitHook()
        {
            if (_quitHookInstalled) return;
            _quitHookInstalled = true;
            // 씬 오브젝트에 기대지 않는다 — 씬이 바뀌면 조용히 죽는다
            // (WindowsTaskbarButtonRemover.InstallQuitHook과 같은 이유).
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting()
        {
            RemoveNow("앱 종료");
            DestroyHostWindow();
        }

        /// <summary>
        /// ★ 우리 <b>자신의</b> 숨겨진 호스트 창을 없앤다. <b><see cref="OnQuitting"/>이 유일한
        /// 호출자</b>이고, 그것은 <c>Application.quitting</c>에서만 불린다(리더 승인 조건 5).
        ///
        /// <para>인자는 <see cref="_hostWindow"/> 하나로 고정돼 있고, 그 필드에는 이 클래스가
        /// <c>CreateWindowEx</c>로 <b>직접 만든 창</b>만 들어간다. 남의 창 핸들이 이 경로로 흘러들
        /// 방법이 없다 — 그것이 이 예외의 승인 조건이다.</para>
        ///
        /// <para>지운 뒤 핸들을 즉시 비운다. 남겨 두면 그 뒤의 어떤 경로가 <b>이미 죽은 창</b>을
        /// 향해 메시지를 보내게 된다.</para>
        /// </summary>
        private static void DestroyHostWindow()
        {
            if (_hostWindow == IntPtr.Zero) return;
            try
            {
                DestroyWindow(_hostWindow);
            }
            catch (Exception)
            {
                // 정리 실패로 종료를 막지 않는다.
            }
            _hostWindow = IntPtr.Zero;
        }

        private static NOTIFYICONDATA BuildIconData(uint flags)
        {
            string tip = SystemTrayPresencePolicy.Tooltip;
            if (tip != null && tip.Length >= SystemTrayPresencePolicy.MaxTooltipLength)
            {
                // Win32 szTip은 고정 길이 배열이라 넘치면 마샬러가 던진다.
                tip = tip.Substring(0, SystemTrayPresencePolicy.MaxTooltipLength - 1);
            }

            return new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hostWindow,
                uID = SystemTrayPresencePolicy.TrayIconId,
                uFlags = flags,
                uCallbackMessage = SystemTrayPresencePolicy.TrayCallbackMessage,
                hIcon = _icon,
                szTip = tip ?? string.Empty,
            };
        }

        // ====================================================================
        // 호스트 창
        // ====================================================================

        private static bool EnsureHostWindow()
        {
            if (_hostWindow != IntPtr.Zero) return true;

            try
            {
                _wndProc = WindowProcedure;   // 필드에 붙잡아 둔다(GC 방지).

                var wc = new WNDCLASSEX
                {
                    cbSize = Marshal.SizeOf(typeof(WNDCLASSEX)),
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                    hInstance = GetModuleHandle(null),
                    lpszClassName = HostWindowClassName,
                };

                // 반환 0은 "이미 등록됨"일 수도 있다(에디터 도메인 리로드). 그 경우에도 창 생성은
                // 성공하므로 여기서 포기하지 않는다.
                RegisterClassEx(ref wc);

                _hostWindow = CreateWindowEx(
                    (uint)AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit,
                    HostWindowClassName,
                    "StickMate",
                    WS_POPUP,          // WS_VISIBLE 없음 — 이 창은 한 번도 보이지 않는다.
                    0, 0, 0, 0,
                    IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

                if (_hostWindow == IntPtr.Zero)
                {
                    _unavailable = true;
                    LogFailureOnce("트레이 콜백을 받을 호스트 창을 만들지 못했습니다(CreateWindowEx 실패).");
                    return false;
                }

                _shellRestartMessage = RegisterWindowMessage(SystemTrayPresencePolicy.ShellRestartMessageName);
                return true;
            }
            catch (Exception e)
            {
                _unavailable = true;
                LogFailureOnce($"호스트 창 생성 중 예외({e.GetType().Name}: {e.Message}).");
                return false;
            }
        }

        /// <summary>
        /// ★ 여기는 <b>Unity 스레드의 메시지 펌프</b>가 부른다. Unity API를 부르지 않는다
        /// (클래스 문서 참고) — 캐시된 값으로 메뉴를 그리고 결과를 적어 둘 뿐이다.
        ///
        /// <para><b>실기 미확인</b>: Unity 플레이어가 자기 메인 스레드에서 표준 메시지 루프를 돌리므로
        /// 같은 스레드에서 만든 이 창의 메시지도 이 프로시저로 배달될 것으로 판단한다. 이 개발
        /// 머신에 Windows가 없어 확인하지 못했다.</para>
        /// </summary>
        private static IntPtr WindowProcedure(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (_shellRestartMessage != 0 && message == _shellRestartMessage)
                {
                    _shellRestartPending = true;
                    return IntPtr.Zero;
                }

                if (message == SystemTrayPresencePolicy.TrayCallbackMessage)
                {
                    // 클래식(버전 미지정) 트레이 콜백 규약: lParam의 하위 워드가 마우스 메시지다.
                    uint mouseMessage = unchecked((uint)(lParam.ToInt64() & 0xFFFF));
                    if (SystemTrayPresencePolicy.IsMenuTriggerMessage(mouseMessage)) ShowMenu();
                    return IntPtr.Zero;
                }
            }
            catch (Exception)
            {
                // 프로시저 밖으로 예외를 내보내지 않는다 — 네이티브 프레임을 가로질러 던지면
                // 무슨 일이 벌어지는지 이 머신에서 확인할 방법이 없다.
            }

            return DefWindowProc(hWnd, message, wParam, lParam);
        }

        private static void ShowMenu()
        {
            IntPtr menu = CreatePopupMenu();
            if (menu == IntPtr.Zero) return;

            try
            {
                bool hidden = _cachedCharacterHidden;
                foreach (TrayMenuCommand command in SystemTrayPresencePolicy.MenuOrder)
                {
                    if (SystemTrayPresencePolicy.NeedsSeparatorBefore(command))
                    {
                        AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
                    }
                    AppendMenu(menu, MF_STRING,
                        (UIntPtr)(uint)SystemTrayPresencePolicy.ToCommandId(command),
                        SystemTrayPresencePolicy.LabelFor(command, hidden));
                }

                if (!GetCursorPos(out POINT cursor)) return;

                // ★★ KB135788 표준 관용구 — <b>임의 변형 금지</b>(리더 승인 조건 4).
                //   Microsoft가 문서화한 형태 그대로다: 메뉴를 띄우기 <b>전에</b> 소유 창을 전면으로
                //   올리고, TrackPopupMenu <b>뒤에</b> 그 창으로 WM_NULL을 하나 보낸다. 이 둘이
                //   빠지면 메뉴가 <b>바깥을 클릭해도 닫히지 않는다</b>(문서화된 결함).
                //   두 호출의 인자는 우리가 CreateWindowEx로 직접 만든 <b>숨겨진 호스트 창</b>
                //   하나(_hostWindow)로 고정돼 있다 — 외부에서 임의 핸들을 주입할 형태가 아니다.
                SetForegroundWindow(_hostWindow);

                // TPM_RETURNCMD: 선택 결과를 반환값으로 받는다(메뉴 알림 메시지를 쓰지 않는다).
                // TPM_NONOTIFY: 호스트 창에 WM_COMMAND를 보내지 않는다.
                int result = TrackPopupMenu(
                    menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY,
                    cursor.X, cursor.Y, 0, _hostWindow, IntPtr.Zero);

                // KB135788의 나머지 절반. 우리 자신의 창에 <b>아무 일도 하지 않는 메시지</b>를 하나
                // 보내, 메뉴가 남긴 모달 상태가 정상적으로 풀리게 한다.
                PostMessage(_hostWindow, WM_NULL, IntPtr.Zero, IntPtr.Zero);

                if (SystemTrayPresencePolicy.TryResolveCommand(result, out TrayMenuCommand chosen))
                {
                    // 배달은 다음 Tick에서 — 여기는 중첩 모달 루프 안이다.
                    _pendingCommand = chosen;
                    _hasPendingCommand = true;
                }
            }
            finally
            {
                DestroyMenu(menu);
            }
        }

        // ====================================================================
        // 아이콘
        // ====================================================================

        /// <summary>
        /// 트레이에 그릴 아이콘. <b>실행 파일에 이미 박혀 있는 앱 아이콘을 재사용한다</b> —
        /// 전용 트레이 아이콘 에셋이 아직 없다.
        ///
        /// <para>★ <c>design-art</c> 인계 사항: Windows 트레이 규격은 <b>16×16과 32×32</b>가 모두
        /// 필요하다(DPI에 따라 셸이 고른다). 지금은 앱 아이콘에서 <c>ExtractIconEx</c>가 뽑아 주는
        /// 작은 아이콘을 쓰므로, 앱 아이콘이 16px에서 뭉개지면 트레이에서도 뭉개진다.</para>
        /// </summary>
        private static void EnsureIcon()
        {
            if (_icon != IntPtr.Zero) return;

            try
            {
                using (var self = System.Diagnostics.Process.GetCurrentProcess())
                {
                    string exePath = self.MainModule != null ? self.MainModule.FileName : null;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        if (ExtractIconEx(exePath, 0, out IntPtr large, out IntPtr small, 1) > 0)
                        {
                            _largeIcon = large;
                            if (small != IntPtr.Zero)
                            {
                                _icon = small;
                                _iconIsShared = false;
                                return;
                            }
                            if (large != IntPtr.Zero)
                            {
                                _icon = large;
                                _largeIcon = IntPtr.Zero;
                                _iconIsShared = false;
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 아이콘을 못 뽑아도 트레이는 세운다 — 자리표시자가 없는 것보다 낫다.
            }

            _icon = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
            _iconIsShared = true;   // 시스템 공유 아이콘은 파괴 대상이 아니다.
            Debug.LogWarning($"{LogPrefix} 실행 파일에서 앱 아이콘을 뽑지 못해 <b>시스템 기본 아이콘</b>으로 " +
                "자리표시합니다 — 트레이에 일반 응용프로그램 아이콘이 뜹니다. 전용 트레이 아이콘 " +
                "(16×16 / 32×32)은 design-art 인계 사항입니다.");
        }

        private static void ReleaseIcons()
        {
            try
            {
                if (!_iconIsShared && _icon != IntPtr.Zero) DestroyIcon(_icon);
                if (_largeIcon != IntPtr.Zero) DestroyIcon(_largeIcon);
            }
            catch (Exception)
            {
                // 정리 실패로 종료를 막지 않는다.
            }
            _icon = IntPtr.Zero;
            _largeIcon = IntPtr.Zero;
        }

        // ====================================================================
        // 잡동사니
        // ====================================================================

        private static bool ResolveOptOut()
        {
            try
            {
                string v = Environment.GetEnvironmentVariable(
                    SystemTrayPresencePolicy.OptOutEnvironmentVariable);
                return !string.IsNullOrEmpty(v) && v != "0";
            }
            catch (Exception) { return false; }
        }

        private static void LogFailureOnce(string why)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            Debug.LogWarning($"{LogPrefix} {why} 트레이 아이콘 없이 계속 동작합니다 — " +
                "<b>불편이지 고장이 아닙니다</b>. 다만 이 앱에는 작업표시줄 버튼도 Alt+Tab 항목도 " +
                "없으므로 <b>종료는 전역 단축키 · 톱니 부채꼴 · 설정창</b>으로만 가능합니다. " +
                "정직한 실패 보고용 로그입니다.");
        }

        // ==================== P/Invoke ====================

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public int cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        /// <summary>
        /// <c>NOTIFYICONDATAW</c>. <b>필드 순서가 곧 ABI</b>다 — 줄 순서를 바꾸면 셸이 엉뚱한
        /// 바이트를 읽는다. <c>cbSize</c>는 <c>Marshal.SizeOf</c>로 채워 손으로 센 값이 틀릴 여지를 없앤다.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
        private static extern IntPtr CreateWindowEx(
            uint exStyle, string className, string windowName, uint style,
            int x, int y, int width, int height,
            IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        // ====================================================================
        // ★ 승인된 예외 3종 (2026-09-03 리더 승인) — 아래 세 개의 extern과 그 호출부가 전부다.
        //   전역 금지 니들이지만 이 파일에서만, <b>우리 자신의 호스트 창</b>에 한해 허용된다.
        //   근거와 범위는 클래스 문서의 "승인된 예외" 절에 있고, 라인 단위 재검증은
        //   Tests/EditMode/UserAssetImmutabilityAuditTests의 화이트리스트가 한다.
        //   ★ 인자를 IntPtr 하나로만 받게 두는 것이 중요하다 — 어떤 형태로든 "임의의 창"을
        //     가리킬 수 있는 편의 오버로드를 만들지 마라. 승인 조건이 거기 걸려 있다.
        // ====================================================================

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "PostMessageW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DefWindowProcW")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterWindowMessageW")]
        private static extern uint RegisterWindowMessage(string name);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr itemId, string item);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(
            IntPtr menu, uint flags, int x, int y, int reserved, IntPtr owner, IntPtr rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadIconW")]
        private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
        private static extern uint ExtractIconEx(
            string file, int iconIndex, out IntPtr largeIcon, out IntPtr smallIcon, uint icons);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }
}
#endif
