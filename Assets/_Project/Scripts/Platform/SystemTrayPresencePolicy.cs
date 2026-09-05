namespace StickMate.Platform
{
    /// <summary>
    /// 이 오버레이가 OS의 <b>상시 표시 영역</b>(Windows 시스템 트레이 / macOS 메뉴바 상태 아이템)에
    /// 어떻게 나타나고 무엇을 제공하는가를 정하는 <b>순수 규칙</b>. UnityEngine 의존도 P/Invoke도
    /// 한 줄 없다 — Windows 실기가 없는 이 개발 머신에서 규칙 자체를 실행해 검증할 수 있어야 하기
    /// 때문이다(<see cref="AppSwitcherPresencePolicy"/>, <see cref="LayeredHybridPolicy"/>와 같은 설계).
    ///
    /// ============================================================================
    /// 왜 생겼는가 — 2026-09-03 사용자 확정 지시 "실행시 시스템 트레이에 표시되어야함"
    /// ============================================================================
    /// 같은 날 사용자가 <b>두 번</b> 종료 버튼을 찾지 못했다(<i>"종료버튼 어딨어"</i>). 종료 경로는
    /// 3중인데(전역 단축키 · 톱니 부채꼴 · 설정창) <b>셋 다 창을 한 번도 열어 본 적 없는 사용자에게는
    /// 발견 불가능</b>하다는 것을 <c>Interaction/AppControlDirector.cs</c>의 클래스 문서가 이미
    /// 스스로 적고 있었다.
    ///
    /// <para>그리고 같은 날 <see cref="AppSwitcherPresencePolicy"/> 라운드가 Windows에서
    /// <b>작업표시줄 버튼과 Alt+Tab 항목을 없앴다.</b> 그 라운드의 "갚아야 할 대가" 문단이
    /// <i>"Windows에서 앱을 찾는 시각 단서가 사라진다"</i>고 적어 두었는데, 트레이 아이콘이 바로
    /// 그 빚을 갚는 물건이다 — 지금 이 앱에는 <b>OS 레벨 존재 증거가 하나도 없다.</b></para>
    ///
    /// ============================================================================
    /// ★ macOS는 이 라운드에 <b>구현하지 않는다</b> — 사유를 여기 실행 가능한 형태로 남긴다
    /// ============================================================================
    /// macOS 대응물은 <see cref="MacOsMechanismName"/>인데 이것은 AppKit Objective-C API라
    /// <b>자체 네이티브 플러그인이 반드시 필요하다.</b> 이 프로젝트는 직전 라운드들에서 자체
    /// Objective-C 플러그인이 반복적으로 실패해 전부 제거하고 검증된 오픈소스
    /// (UniWindowController)로 교체한 이력이 있다(<c>Platform/MacOS/MacWindowService.cs</c> 클래스
    /// 문서, 그리고 <c>Interaction/AppControlDirector.cs</c>의 "1안 기각" 문단).
    ///
    /// <para><b>이번 라운드에 실측으로 확인한 것</b>: 그 검증된 오픈소스에 대체 수단이 있는지 먼저
    /// 확인했고 — <c>Library/PackageCache/com.kirurobo.uniwinc@…</c> 전체에서
    /// <c>NSStatusItem</c>/<c>NSMenu</c>/<c>statusBar</c>/<c>menuBar</c>가 <b>0건</b>이다.
    /// 즉 기각 사유가 아직 살아 있다. 그래서 macOS는 <b>"별도 배정 필요"</b>로 남기고, 그 사실을
    /// <c>Tests/EditMode/PlatformParityAuditTests</c>가 러너에 <b>계속 건너뜀으로 띄운다</b>(잊히지
    /// 않게). 이 상수들이 그 감사의 근거다 — 문자열을 감사 쪽에 베끼지 않는다.</para>
    ///
    /// ============================================================================
    /// ★ 이 정책이 <b>정하지 않는 것</b>
    /// ============================================================================
    /// 메뉴 항목이 <b>무엇을 하는가</b>는 여기 없다. 트레이 메뉴는 <b>기존 진입점을 한 번 더
    /// 부르는 배선일 뿐</b>이며(새 종료 로직·새 숨김 로직을 만들지 않는다), 실제 호출은
    /// <see cref="SystemTrayCommandRouter"/>에 등록된 핸들러가 한다. 그 분리 덕분에
    /// <c>Platform/Windows/</c>는 <c>StickMate.Interaction</c>을 한 번도 참조하지 않는다.
    /// </summary>
    public static class SystemTrayPresencePolicy
    {
        // ====================================================================
        // 기전 이름 — 감사가 문자열을 베끼지 않고 확인할 수 있도록 여기 한 곳에 둔다
        // ====================================================================

        /// <summary>Windows 쪽 대응 기전의 이름(shell32 트레이 API).</summary>
        public const string WindowsMechanismName = "Shell_NotifyIcon";

        /// <summary>macOS 쪽 대응 기전의 이름. <b>아직 구현이 없다</b> —
        /// <see cref="MacOsGapReason"/> 참고.</summary>
        public const string MacOsMechanismName = "NSStatusItem";

        /// <summary>
        /// macOS가 이번 라운드에 빠진 사유. <b>감사 메시지가 이 상수를 인용한다</b> — 사유를 감사
        /// 파일에 베껴 두면 사유가 바뀐 날 감사만 낡는다(CLAUDE.md: 상수/식별자를 베끼지 않는다).
        /// </summary>
        public const string MacOsGapReason =
            "별도 배정 필요 — NSStatusItem은 AppKit Objective-C API라 자체 네이티브 플러그인이 " +
            "필요한데, 이 프로젝트는 자체 Objective-C 플러그인이 반복 실패해 전부 제거한 이력이 있다. " +
            "2026-09-03 실측: 검증된 대안인 UniWindowController 패키지에 NSStatusItem/NSMenu 관련 " +
            "코드가 0건이라 기각 사유가 아직 살아 있다.";

        /// <summary>재빌드 없이 트레이 아이콘을 끄는 환경변수
        /// (<c>STICKMATE_KEEP_TASKBAR_BUTTON</c>과 같은 관례).</summary>
        public const string OptOutEnvironmentVariable = "STICKMATE_NO_TRAY_ICON";

        /// <summary>트레이 아이콘 툴팁. Win32 <c>szTip</c>은 128자 제한이라
        /// <see cref="MaxTooltipLength"/>로 함께 잠근다.</summary>
        public const string Tooltip = "StickMate — 우클릭: 메뉴 (종료 · 숨기기 · 설정)";

        /// <summary>Win32 <c>NOTIFYICONDATA.szTip</c>의 요소 수(널 종단 포함).</summary>
        public const int MaxTooltipLength = 128;

        // ====================================================================
        // Win32 상수 — 이 저장소의 단일 출처
        // ====================================================================
        //
        // 중립 위치에 두는 이유는 AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit과 같다:
        // (1) CLAUDE.md 규칙 — 판정은 중립에, 플랫폼 코드는 사실 조회만.
        // (2) 검증 가능성 — Platform/Windows/ 는 이 머신의 활성 타깃(macOS)에서 컴파일조차 되지
        //     않으므로, 상수를 그 안에 두면 EditMode가 값을 확인할 방법이 원리적으로 없다.
        //     트레이는 특히 이것이 중요하다: NIM_ADD(0)와 NIM_DELETE(2)를 바꿔 적으면
        //     "종료할 때 아이콘을 하나 더 추가하는" 조용한 좀비 생성기가 된다.

        /// <summary><c>NIM_ADD</c> — 아이콘 추가.</summary>
        public const uint NotifyIconAdd = 0x00000000;

        /// <summary><c>NIM_MODIFY</c> — 이미 있는 아이콘 갱신.</summary>
        public const uint NotifyIconModify = 0x00000001;

        /// <summary><c>NIM_DELETE</c> — 아이콘 제거. <b>종료 경로가 반드시 불러야 한다</b>
        /// (안 부르면 셸이 죽은 아이콘을 한동안 그대로 그린다 = 좀비 아이콘).</summary>
        public const uint NotifyIconDelete = 0x00000002;

        /// <summary><c>NIF_MESSAGE</c> — 콜백 메시지를 쓰겠다.</summary>
        public const uint NotifyIconFlagMessage = 0x00000001;

        /// <summary><c>NIF_ICON</c> — 아이콘 핸들을 쓰겠다.</summary>
        public const uint NotifyIconFlagIcon = 0x00000002;

        /// <summary><c>NIF_TIP</c> — 툴팁을 쓰겠다.</summary>
        public const uint NotifyIconFlagTip = 0x00000004;

        /// <summary>추가 시 켜는 플래그 3종 묶음.</summary>
        public const uint NotifyIconAddFlags =
            NotifyIconFlagMessage | NotifyIconFlagIcon | NotifyIconFlagTip;

        /// <summary>
        /// 셸이 우리에게 보낼 콜백 메시지 ID. <c>WM_APP</c>(0x8000) 위에서 고른다 —
        /// <c>WM_USER</c> 대역은 컨트롤 클래스가 자기 용도로 쓰므로 트레이 콜백에 쓰면 안 된다.
        /// </summary>
        public const uint TrayCallbackMessage = 0x8000 + 1;

        /// <summary>우리 트레이 아이콘의 <c>uID</c>. 한 프로세스에 아이콘이 하나뿐이라 고정값이다.</summary>
        public const uint TrayIconId = 1;

        /// <summary><c>WM_LBUTTONUP</c> — 좌클릭 뗌.</summary>
        public const uint MouseLeftButtonUp = 0x0202;

        /// <summary><c>WM_RBUTTONUP</c> — 우클릭 뗌.</summary>
        public const uint MouseRightButtonUp = 0x0205;

        /// <summary>
        /// 셸이 <b>재시작</b>했을 때(explorer.exe 크래시 후 복구 등) 보내는 브로드캐스트 메시지의
        /// 이름. <c>RegisterWindowMessage</c>로 ID를 받아 구독한다.
        ///
        /// <para><b>24시간 상주 앱에 이것이 없으면 안 된다</b>: 셸이 재시작하면 모든 트레이 아이콘이
        /// 사라지는데, 우리 앱은 작업표시줄 버튼도 Alt+Tab 항목도 없으므로 그 순간
        /// <b>OS 레벨 존재 증거가 0이 된다</b> — 사용자는 앱을 끌 방법을 다시 잃는다.</para>
        /// </summary>
        public const string ShellRestartMessageName = "TaskbarCreated";

        // ====================================================================
        // 메뉴
        // ====================================================================

        /// <summary>
        /// 트레이 메뉴가 <b>표시하는 순서 그대로</b>의 항목 목록.
        /// <para>순서에 뜻이 있다: 사용자가 못 찾은 것은 <b>종료</b>였으므로 종료를 맨 아래
        /// <b>고정 위치</b>에 둔다(구분선 아래, 항상 마지막). 되돌릴 수 없는 명령이 목록 중간에서
        /// 자리를 옮겨 다니면 오조작이 난다.</para>
        /// </summary>
        public static readonly TrayMenuCommand[] MenuOrder =
        {
            TrayMenuCommand.ToggleCharacterHidden,
            TrayMenuCommand.OpenSettings,
            TrayMenuCommand.Quit,
        };

        /// <summary>이 명령 <b>앞에</b> 구분선을 그리는가. 되돌릴 수 없는 <see cref="TrayMenuCommand.Quit"/>를
        /// 나머지에서 떼어 놓는 것이 유일한 목적이다.</summary>
        public static bool NeedsSeparatorBefore(TrayMenuCommand command)
            => command == TrayMenuCommand.Quit;

        /// <summary>
        /// 메뉴에 그릴 글자. <b>숨김/보이기는 현재 상태에 따라 문구가 뒤집힌다</b> —
        /// 토글 항목에 고정 문구를 쓰면 사용자가 지금 어느 쪽인지 알 수 없다(원칙 1의 정신:
        /// 표시된 것과 실제가 갈라지면 안 된다).
        /// </summary>
        /// <param name="characterHidden">지금 <b>사용자 직접 숨김 축(축 2)</b>이 켜져 있는가
        /// (<c>StickmanAgent.IsUserHidden</c>). 전체화면 자동 숨김(축 1)도, 다른 가상 데스크톱(축 4)도
        /// 이 축이 아니다.
        /// <para>★ 2026-09-05 정정 — 예전에는 <c>IsUserHiddenOnly</c>라고 적혀 있었다. 그 값은
        /// 「축 2<b>만</b>으로 숨었는가」라 축 1·축 4가 함께 켜지면 <b>false</b>이고, 그러면 사용자가
        /// 이미 숨겨 둔 상태에서 글자가 「캐릭터 숨기기」로 나온다. <b>토글 글자는 그 토글이 소유한
        /// 축에서만 나와야 한다</b> — 이 항목이 부르는 것은 <c>SetUserHidden(!_userHidden)</c>이다.</para></param>
        public static string LabelFor(TrayMenuCommand command, bool characterHidden)
        {
            switch (command)
            {
                case TrayMenuCommand.ToggleCharacterHidden:
                    return characterHidden ? "캐릭터 다시 보이기" : "캐릭터 숨기기";
                case TrayMenuCommand.OpenSettings:
                    return "설정 열기";
                case TrayMenuCommand.Quit:
                    return "StickMate 종료";
                default:
                    return command.ToString();
            }
        }

        /// <summary>
        /// Win32 메뉴 항목 ID. <b>0이면 안 된다</b> — <c>TrackPopupMenu</c>는 <c>TPM_RETURNCMD</c>일 때
        /// <b>사용자가 취소했음</b>을 0으로 알리므로, ID가 0인 항목은 취소와 구분되지 않는다.
        /// </summary>
        public static int ToCommandId(TrayMenuCommand command) => (int)command;

        /// <summary>
        /// <c>TrackPopupMenu</c>가 돌려준 값을 명령으로 되돌린다. <b>모르는 값은 받지 않는다</b> —
        /// 0(취소)과 목록에 없는 ID는 전부 false다.
        /// </summary>
        public static bool TryResolveCommand(int menuResult, out TrayMenuCommand command)
        {
            command = default;
            if (menuResult == 0) return false;
            foreach (TrayMenuCommand candidate in MenuOrder)
            {
                if (ToCommandId(candidate) != menuResult) continue;
                command = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 이 마우스 메시지가 <b>메뉴를 여는</b> 동작인가. 좌·우 클릭 <b>둘 다</b> 연다.
        ///
        /// <para>좌클릭도 여는 이유: 이 앱에는 "본창을 띄운다"는 좌클릭 기본 동작이 없다
        /// (창이 아니라 바탕화면 오버레이다). 좌클릭에 아무 반응이 없으면 사용자는 아이콘이
        /// 죽었다고 판단한다 — 그것이 애초에 이 라운드가 고치려는 문제(발견 불가능성)와 같은 병이다.</para>
        /// </summary>
        public static bool IsMenuTriggerMessage(uint mouseMessage)
            => mouseMessage == MouseLeftButtonUp || mouseMessage == MouseRightButtonUp;

        // ====================================================================
        // 설치 재시도
        // ====================================================================

        /// <summary>
        /// ★ 트레이 아이콘 <b>설치</b>를 이번 틱에 시도할 것인가.
        ///
        /// <para>이 규칙이 중립 위치에 있는 이유는 <see cref="AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval"/>과
        /// 같다 — <b>상한이 없으면 24시간 상주 앱에서 실패한 셸 호출이 영원히 반복된다.</b></para>
        ///
        /// <para>재시도가 필요한 이유는 <b>경합</b>이다: 로그온 직후 셸의 트레이가 아직 준비되지 않았으면
        /// <c>Shell_NotifyIcon(NIM_ADD)</c>이 실패한다. 몇 번만 더 묻되 <b>반드시 멈춘다</b>.</para>
        ///
        /// <para><b>이 판정은 설치에만 건다.</b> 아이콘이 선 뒤에도 매 프레임 도는 일(선택된 명령
        /// 배달 · 표시 상태 갱신)은 이 상한과 무관하다 — 그 둘을 한 스위치로 묶으면 상한에 도달한
        /// 순간 <b>메뉴가 조용히 먹통</b>이 된다.</para>
        /// </summary>
        /// <param name="optedOut">사용자가 환경변수로 이 경로를 껐는가.</param>
        /// <param name="alreadyInstalled">아이콘이 이미 서 있는가.</param>
        /// <param name="knownUnavailable">이 경로가 이미 못 쓴다고 확인됐는가.</param>
        /// <param name="attemptsSoFar">지금까지 시도한 횟수.</param>
        /// <param name="maxAttempts">상한. 0 이하이면 이 기능을 통째로 끈 것과 같다.</param>
        public static bool ShouldAttemptInstall(
            bool optedOut, bool alreadyInstalled, bool knownUnavailable,
            int attemptsSoFar, int maxAttempts)
        {
            if (optedOut) return false;
            if (alreadyInstalled) return false;
            if (knownUnavailable) return false;
            if (maxAttempts <= 0) return false;
            return attemptsSoFar < maxAttempts;
        }

        /// <summary>
        /// 셸 재시작 통보를 받았을 때 <b>다시 세워야</b> 하는가.
        ///
        /// <para><see cref="ShouldAttemptInstall"/>의 상한과 <b>일부러 분리</b>했다: 셸 재시작은
        /// 실패가 아니라 <b>외부 사건</b>이라 시도 상한으로 눌러야 할 대상이 아니다. 옵트아웃만
        /// 존중한다. 이 둘을 합치면 하루짜리 세션에서 explorer가 두어 번 죽는 순간
        /// 아이콘이 영영 돌아오지 않는다.</para>
        /// </summary>
        public static bool ShouldReinstallAfterShellRestart(bool optedOut, bool knownUnavailable)
            => !optedOut && !knownUnavailable;

        /// <summary>로그용 한 줄. 문자열을 실행부에 흩어 두지 않는다.</summary>
        public static string DescribeMenu(bool characterHidden)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < MenuOrder.Length; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append('[').Append(LabelFor(MenuOrder[i], characterHidden)).Append(']');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 트레이 메뉴가 부를 수 있는 명령. <b>값이 곧 Win32 메뉴 항목 ID</b>이므로
    /// <b>0을 쓰지 않는다</b>(0은 "사용자가 메뉴를 취소했다"는 뜻이다).
    ///
    /// <para>세 항목 전부 <b>이미 있는 진입점</b>을 가리킨다 — 이 열거형은 새 기능이 아니라
    /// <b>이름표</b>다. 실제 호출은 <see cref="SystemTrayCommandRouter"/>에 등록된 핸들러가 한다.</para>
    /// </summary>
    public enum TrayMenuCommand
    {
        /// <summary>캐릭터 숨기기/다시 보이기 — <c>StickmanAgent.ToggleUserHidden</c>(⌃⌥⌘K와 같은 축).</summary>
        ToggleCharacterHidden = 1,

        /// <summary>설정창 열기 — <c>SettingsWindow.Open</c>.</summary>
        OpenSettings = 2,

        /// <summary>앱 종료 — <c>AppControlDirector.QuitApplication</c>(단일 종료 진입점).</summary>
        Quit = 3,
    }
}
