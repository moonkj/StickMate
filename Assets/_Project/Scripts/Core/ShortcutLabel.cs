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
        /// Windows 쪽 대응물 — <b>1차 출처로 확인된 「확정 충돌」만 담는다.</b> 지금은 0건이다.
        ///
        /// <para>★★ <b>2026-09-05 조사(M-6). 예전 이 문단은 근거보다 자신만만했다.</b>
        /// 옛 문장은 <i>"비어 있고 그것이 조사 결과다(추정이 아니다)"</i>였는데, 그 근거로 든
        /// <i>"RegisterHotKey 계열의 <b>정확 일치</b> 매칭이라 Ctrl+Alt가 함께 눌리면 발동하지 않는다"</i>는
        /// <b>1차 출처에 없다</b>. Microsoft <c>RegisterHotKey</c> 문서의 Remarks는
        /// <i>"When a key is pressed, the system looks for a match against all hot keys"</i>까지만 적고
        /// <b>여분의 수식자가 매칭을 막는지에 대해 한 글자도 쓰지 않는다.</b> 즉 그것은 널리 관찰되는
        /// 동작이지 <b>문서화된 보장이 아니다</b> — <b>미확인</b>으로 되돌린다.</para>
        ///
        /// <para>★ 그리고 같은 문서에 <b>정반대 방향의 명시적 경고</b>가 있다(<c>fsModifiers</c> 표,
        /// <c>MOD_WIN</c> 행): <i>"Keyboard shortcuts that involve the WINDOWS key are <b>reserved for
        /// use by the operating system</b>."</i> 우리 조합은 <b>전부</b> Windows 키를 포함한다.
        /// ⇒ 이 배열이 비어 있는 것은 <b>"안전이 증명됐다"</b>가 아니라 <b>"확정 충돌을 아직 못 찾았다"</b>다.</para>
        ///
        /// <para><b>실제로 조사한 것(1차 출처 2건, 2026-09-05)</b>:
        /// <list type="number">
        ///   <item>Microsoft Support <i>"Keyboard shortcuts in Windows"</i> 전수 —
        ///     <b><c>Win+Ctrl+Alt+&lt;글자&gt;</c> 조합은 목록에 <u>0건</u>이다.</b> 수식자 3개짜리로
        ///     문서화된 것은 <c>Win+Ctrl+Shift+B</c>와 <c>Win+Ctrl+Shift+숫자</c>뿐이고 둘 다
        ///     <b>Alt가 아니라 Shift</b>다. 우리 11개 글자 중 <b>정확히 일치하는 예약은 하나도 없다.</b></item>
        ///   <item>Microsoft <c>RegisterHotKey</c>(winuser.h) — 위 <c>MOD_WIN</c> 경고와
        ///     "여분 수식자" 무언급.</item>
        /// </list></para>
        ///
        /// <para><b>그래서 여기는 계속 비워 두고</b>, 문서화된 조합에서 <b>우리 수식자 하나만 빼면
        /// 닿는</b> 글자들은 <see cref="WindowsSuspectActionKeys"/>로 따로 뺐다. 그쪽은
        /// <b>강제하지 않는다</b> — 강제하면 지금 출하 중인 단축키 6개가 근거 없이 죽는다.
        /// 가르는 것은 <b>실기 1회</b>이고 그 절차도 그쪽 문서에 있다(낮 세션 A-7).</para>
        ///
        /// <para>비어 있어도 이 배열을 두는 이유는 감사가 두 플랫폼을 <b>같은 코드 경로로</b> 돌게
        /// 하기 위해서다 — 한쪽만 검사하는 감사는 다음에 Windows 예약이 발견됐을 때 또 조용히
        /// 비게 된다. Windows 예약이 <b>실기로</b> 확인되면 <b>여기에</b> 추가하면 검사가 곧바로 적용된다.</para>
        /// </summary>
        public static readonly string[] WindowsReservedActionKeys = { };

        /// <summary>
        /// ★ 2026-09-05 (M-6) — <b>「확정」과 「의심」을 가른 뒤쪽.</b> 여기 있는 글자는
        /// <b>금지 목록이 아니다</b>(<see cref="WindowsReservedActionKeys"/>가 그것이다).
        /// <b>실기 세션에서 우선순위로 눌러 볼 목록</b>이며, 아무 동작도 강제하지 않는다.
        ///
        /// <para><b>선정 규칙(자의적이지 않다)</b>: 우리 조합은 <c>Ctrl+Alt+Win+글자</c>다.
        /// Microsoft 문서에 존재하는 조합 중 <b>우리 수식자 3개에서 정확히 하나만 뺀 것</b>
        /// (= 편집 거리 1)이 있는 글자만 담는다. 「정확 일치 매칭」 가정이 <b>틀렸을 때 가장 먼저
        /// 터지는 자리</b>가 정확히 여기이기 때문이다. 두 개를 빼야 닿는 것(= 맨 <c>Win+글자</c>)은
        /// 담지 않는다 — 그 거리는 <b>우리 11개 글자 전부</b>가 해당해서 목록이 아무것도 안 가른다.</para>
        ///
        /// <para><b>글자별 근거</b> (전부 Microsoft 1차 출처):
        /// <list type="bullet">
        ///   <item><b>B</b> — <c>Win+Alt+B</c>(HDR 켜기/끄기). Ctrl 하나만 빼면 닿는다. <b>출하 중</b>(혼잣말)</item>
        ///   <item><b>C</b> — <c>Win+Ctrl+C</c>(색 필터). Alt 하나만 빼면 닿는다. <b>출하 중</b>(잉크색)</item>
        ///   <item><b>G</b> — <c>Win+Alt+G</c>(Game Bar: 최근 30초 녹화). <b>출하 중</b>(그라피티).
        ///     ★ Game Bar는 사용자가 <b>조합을 재설정할 수 있어</b> 기계마다 다르다 — 실기 1대의
        ///     결과를 전체로 일반화하면 안 된다</item>
        ///   <item><b>K</b> — <c>Win+Alt+K</c>(마이크 음소거). <b>출하 중</b>(사용자 명시 숨김).
        ///     ★ 이 글자는 <b>탈출구</b>라 대체가 가장 비싸다</item>
        ///   <item><b>Q</b> — <c>Win+Ctrl+Q</c>(빠른 지원). <b>출하 중</b>(종료). ★ K와 같은 급의 상시 계약</item>
        ///   <item><b>R</b> — <c>Win+Alt+R</c>(Game Bar: 녹화 시작/중지). <b>출하 중</b>(로데오 커서).
        ///     ★ 오발동이 <b>사용자 디스크에 영상 파일을 만든다</b> — 이 목록에서 결과가 가장 나쁘다</item>
        ///   <item><b>D</b> — <c>Win+Ctrl+D</c>(가상 데스크톱 추가) · <c>Win+Alt+D</c>(날짜/시간 표시).
        ///     <b>두 축 모두</b> 걸린다. 지금은 개발 게이트 뒤(진단 로그)</item>
        ///   <item><b>F</b> — <c>Win+Ctrl+F</c>(PC 찾기). 지금은 개발 게이트 뒤(집중 모드)</item>
        ///   <item><b>H</b> — <c>Win+Alt+H</c>. 지금은 개발 게이트 뒤(하드웨어 반응)</item>
        /// </list>
        /// 나머지 <b>A · I · N · P · T · X</b>(및 개발용 <b>J · S</b>)는 <b>편집 거리 1에서 아무것도
        /// 안 걸린다</b> — 위 두 1차 출처 기준으로 <b>상대적으로 안전</b>하다.</para>
        ///
        /// <para><b>★ 이 목록으로 무엇을 사는가</b>: 실기 세션에서 11개를 다 눌러 보는 대신
        /// <b>이 6개(출하분)를 먼저</b> 누르고 <b>OS 쪽 부수 효과</b>(녹화 시작 / 색 필터 반전 /
        /// HDR 토글 / 마이크 음소거 / 빠른 지원 창)를 본다. <b>우리 동작이 도는지는 관측 대상이
        /// 아니다</b> — 폴링 방식(<c>Platform/Windows/Win32WindowService.TryGetKeyPressed</c>는
        /// 키 상태를 직접 읽는다)이라 OS가 키를 가져가도 <b>우리 쪽은 어차피 돈다</b>.
        /// <b>실패 모드는 「우리가 안 돈다」가 아니라 「둘 다 돈다」</b>이고, 그게 불변 원칙 2 위반이다.</para>
        ///
        /// <para><b>미확인으로 남기는 것</b>: (1) 여분 수식자가 매칭을 막는가 — 문서에 없다.
        /// (2) Game Bar·접근성 기능이 <c>RegisterHotKey</c>를 쓰는가 저수준 훅을 쓰는가 — 문서에 없다.
        /// (3) 이 지원 문서가 전수인가 — Microsoft는 그렇게 선언하지 않는다.
        /// <b>셋 다 추측으로 메우지 않는다.</b></para>
        /// </summary>
        public static readonly string[] WindowsSuspectActionKeys =
            { "B", "C", "D", "F", "G", "H", "K", "Q", "R" };

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
