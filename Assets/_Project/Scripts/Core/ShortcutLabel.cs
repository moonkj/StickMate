namespace StickMate.Core
{
    /// <summary>
    /// ★ 전역 단축키 <b>표기</b>의 단일 정의처 — 2026-09-01 (Windows 패리티 감사 C3).
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// 보관함 카드의 상태 슬롯(<c>Core/ItemCatalog</c>)과 정보창/설정창 안내가 단축키를
    /// <c>"⌃⌥⌘A"</c>처럼 <b>macOS 글리프로 하드코딩</b>하고 있었다. 그런데 이 앱의 조합키는
    /// 플랫폼마다 물리적으로 다른 키에 매핑된다:
    /// <list type="bullet">
    ///   <item>macOS — Control(⌃) / Option(⌥) / Command(⌘)</item>
    ///   <item>Windows — Ctrl / Alt / <b>Windows 키</b>
    ///         (<c>Platform/Windows/Win32WindowService.TryGetKeyPressed</c>가
    ///         <c>GlobalKey.Command</c>를 <c>VK_LWIN</c>/<c>VK_RWIN</c>으로 읽는다)</item>
    /// </list>
    /// 즉 Windows 사용자에게는 <b>존재하지 않는 조합</b>이 안내되고 있었다. 화면에 나가는 문구가
    /// 실제 동작과 다른 것은 기능 결함과 같은 급이다 — 사용자는 누르는 법을 알 수 없다.
    ///
    /// ============================================================================
    /// 왜 "표기를 만드는 곳"을 따로 두는가
    /// ============================================================================
    /// 소비자가 여럿(카탈로그 11곳 · 정보창 · 설정창 · 성장 알림)이라, 각자 자기 파일에서
    /// <c>#if UNITY_STANDALONE_WIN</c>을 치면 <b>한 곳만 고쳐지는</b> 이 저장소의 단골 실패가 된다.
    /// 그래서 조합키 표기는 여기서만 만들고, 아무도 글리프를 직접 적지 않는다.
    /// <c>Tests/EditMode/PlatformParityAuditTests</c>가 그 규칙(런타임 소스에 글리프 리터럴 금지)을
    /// 실제로 스캔해서 잠근다.
    ///
    /// <para><b>테스트가 프로덕션 문자열을 베끼지 않게</b> 두 표기를 각각 <see cref="MacChord"/> /
    /// <see cref="WindowsChord"/>로 열어 둔다. macOS 머신에서도 Windows 표기를 <b>실제로 계산해</b>
    /// 검사할 수 있어야 한다 — 이 프로젝트는 Windows 빌드를 실행할 수 없기 때문이다.</para>
    /// </summary>
    public static class ShortcutLabel
    {
        /// <summary>macOS 조합키 글리프(Control·Option·Command). 순서는 Apple HIG의 표기 순서다.</summary>
        public const string MacModifiers = "⌃⌥⌘";

        /// <summary>Windows 조합키 표기. macOS의 <b>같은 물리 위치</b> 키에 대응한다
        /// (Control→Ctrl · Option→Alt · Command→Windows 키).</summary>
        public const string WindowsModifiers = "Ctrl+Alt+Win+";

        /// <summary>이 빌드가 Windows 표기를 쓰는가. 테스트가 "호스트에 맞는 쪽이 나왔는지"를
        /// 확인할 때 읽는다 — 값을 확인하는 것이 아니라 <b>어느 표를 골랐는지</b>를 본다.</summary>
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        public const bool HostUsesWindowsNotation = true;
#else
        public const bool HostUsesWindowsNotation = false;
#endif

        // ====================================================================
        // ★ OS가 이미 가져간 조합 — 동작키로 쓰면 안 되는 글자 (2026-09-01 신설)
        // ====================================================================

        /// <summary>
        /// <b>macOS가 <c>⌃⌥⌘</c> 마스크로 이미 예약한 동작키.</b> 여기 있는 글자를 이 앱의
        /// 단축키로 쓰면, 사용자가 한 번 누를 때 <b>두 가지 일이 동시에</b> 일어나고 그중 하나는
        /// 우리가 통제하지 못하는 <b>OS 설정 변경</b>이다 — 불변 원칙 2(비침해)·3(유저 자산 불변) 위반.
        ///
        /// <para><b>실제 사고</b>: 설정창 단축키가 <c>⌃⌥⌘,</c>였다(<c>⌘,</c> = 환경설정이라는 Apple
        /// 관례를 따른 것). 그런데 그 조합은 macOS 접근성 <b>"대비 줄이기"</b>다. 설정창을 열고 닫는
        /// 왕복 2회 누름마다 <c>com.apple.universalaccess</c>의 <c>contrast</c>가 0.10씩 내려갔다.
        /// 기본값(0) 사용자는 클램프되어 무해했지만, <b>대비를 실제로 조절해 쓰는 접근성 사용자만
        /// 골라서</b> 화면이 조금씩 흐려졌다. 2026-09-01 <c>,</c> → <c>P</c>(Preferences)로 옮겼다.</para>
        ///
        /// <para><b>근거(이 머신 실측, <c>defaults read com.apple.symbolichotkeys</c>)</b> —
        /// 세 항목 모두 <c>parameters</c>의 수식자가 <c>1835008</c>(<c>0x1C0000</c> = ⌃+⌥+⌘)다:
        /// <list type="bullet">
        ///   <item>ID <b>21</b> — <c>8</c> : 색 반전</item>
        ///   <item>ID <b>25</b> — <c>.</c> : 대비 늘리기</item>
        ///   <item>ID <b>26</b> — <c>,</c> : 대비 줄이기</item>
        /// </list>
        /// 이 셋뿐이다. <c>⌃⌥⌘</c> + 나머지 글자는 예약되어 있지 않다.</para>
        ///
        /// <para><b>재현/검증 절차</b>(고침이 유효한지 다시 확인할 때 그대로 쓴다):
        /// <code>
        /// defaults read com.apple.universalaccess contrast   # 기준값 기록
        /// # ⌃⌥⌘. 주입 -> contrast 가 올라간다(= OS 훅이 살아 있다는 증명)
        /// # ⌃⌥⌘P 주입 -> contrast 가 그대로여야 한다(= 이 고침의 성패)
        /// </code></para>
        ///
        /// <para><b>왜 <c>⌘,</c> 하나로 못 가는가</b>: 이 앱의 전역 키 조회는 폴링이라, 조합키가
        /// 하나뿐이면 사용자가 <b>다른 앱에서 타이핑하는 중에</b> 반응한다. 조합키 3개 강제가 곧
        /// 비침해 보장이므로(<c>Platform/IGlobalKeyStateService</c>), 마스크를 줄이는 선택지는 없다.</para>
        /// </summary>
        public static readonly string[] MacReservedActionKeys = { "8", ",", "." };

        /// <summary>
        /// Windows 쪽 대응물. <b>지금은 비어 있고, 그것이 조사 결과다</b>(추정이 아니다).
        ///
        /// <para>Windows 셸 단축키(<c>Win+D</c>/<c>Win+R</c>/<c>Win+I</c>/<c>Win+S</c>/<c>Win+X</c>/
        /// <c>Win+P</c> …)는 <c>RegisterHotKey</c> 계열의 <b>정확 일치</b> 매칭이라, Ctrl+Alt가 함께
        /// 눌린 우리 조합에서는 발동하지 않는다. 그래서 <c>Ctrl+Alt+Win+글자</c>로 예약된 것이 없다
        /// (<c>Platform/Windows/Win32WindowService.TryGetKeyPressed</c> 문서 참고). macOS 접근성
        /// 단축키가 마스크를 통째로 가져가는 것과 <b>성질이 다르다</b>.</para>
        ///
        /// <para>비어 있어도 이 배열을 두는 이유는 감사가 두 플랫폼을 <b>같은 코드 경로로</b> 돌게
        /// 하기 위해서다 — 한쪽만 검사하는 감사는 다음에 Windows 예약이 발견됐을 때 또 조용히
        /// 비게 된다. Windows 예약이 확인되면 <b>여기에</b> 추가하면 검사가 곧바로 적용된다.</para>
        /// </summary>
        public static readonly string[] WindowsReservedActionKeys = { };

        /// <summary>이 빌드가 도는 OS가 예약한 동작키. 호스트 판정은 <see cref="HostUsesWindowsNotation"/>과
        /// 같은 분기를 쓴다 — 표기와 금지 목록이 서로 다른 플랫폼을 가리키는 일이 없게.</summary>
        public static string[] HostReservedActionKeys
            => HostUsesWindowsNotation ? WindowsReservedActionKeys : MacReservedActionKeys;

        /// <summary>macOS 표기. <paramref name="key"/>는 동작키 한 글자.
        /// <see cref="MacReservedActionKeys"/>에 있는 글자는 넘기면 안 된다.</summary>
        public static string MacChord(string key) => MacModifiers + key;

        /// <summary>Windows 표기.</summary>
        public static string WindowsChord(string key) => WindowsModifiers + key;

        /// <summary>
        /// <b>지금 이 빌드</b>에서 사용자가 실제로 눌러야 하는 조합의 표기.
        /// <para>컴파일 타임 분기인 이유: 이 앱은 한 빌드가 한 플랫폼에서만 돌고, 표기는 부팅 배너와
        /// 정적 카탈로그 초기화에서 만들어진다. 런타임 질의로 두면 두 경로 모두 매번 분기를 타면서
        /// 얻는 것이 없다.</para>
        /// </summary>
        public static string Chord(string key)
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            => WindowsChord(key);
#else
            => MacChord(key);
#endif
    }
}
