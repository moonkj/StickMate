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

        /// <summary>macOS 표기. <paramref name="key"/>는 동작키 한 글자(또는 <c>","</c>).</summary>
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
