using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 앱 제어 수단 — <b>전역 단축키</b>(2026-08-28 리더 지시: "지금 터미널 없이는 끌 수도 없다").
    ///
    /// ============================================================================
    /// 문제
    /// ============================================================================
    /// 이 앱의 창은 클릭 관통 상태라 클릭으로 포커스를 줄 수 없고, Unity의 Input은 창이 키보드 포커스를
    /// 가진 동안만 동작한다(Core/StickmanAgent의 Escape 긴급 해제가 가진 바로 그 한계 — 사용자가 다른
    /// 앱을 한 번 클릭하는 순간 무력화된다). 그래서 한때 앱을 끄는 유일한 방법이 터미널
    /// <c>kill PID</c>였다. 실사용 앱으로는 치명적이다.
    ///
    /// ============================================================================
    /// 채택한 수단
    /// ============================================================================
    /// 1안(macOS 메뉴바 NSStatusItem)은 **채택하지 않았다**. NSStatusItem은 AppKit Objective-C API라
    ///    네이티브 플러그인이 반드시 필요한데, 이 프로젝트는 직전 라운드들에서 자체 Objective-C
    ///    플러그인이 반복적으로 실패해 전부 제거하고 검증된 오픈소스(UniWindowController)로 교체한
    ///    이력이 있다(Platform/MacOS/MacWindowService.cs 클래스 문서).
    ///
    /// 2안(**전역 단축키**) — 채택. 핵심 미지수였던 "접근성 권한 없이 키 상태를 읽을 수 있는가"를
    ///    먼저 실측으로 확인했다: <c>CGEventSourceKeyState</c>는 우리가 이미 마우스에 쓰고 있는
    ///    <c>CGEventSourceButtonState</c>와 같은 계열의 조회 전용 API로, 권한 없이 동작한다
    ///    (Platform/IGlobalKeyStateService.cs의 "권한에 대하여" 절에 실측 절차 기록). 창 포커스와
    ///    무관하므로 클릭 관통 상태에서도 항상 살아 있다.
    ///
    /// 3안(**캐릭터 우클릭**) — 2026-08-31 폐지 → ★ <b>2026-09-05 사용자 지시로 재개방</b>. 아래 참조.
    ///
    /// ============================================================================
    /// ★★ 2026-08-31 폐지 → 2026-09-05 재개방 (docs/UX_FLOW.md 36-9 / UX_RIGHTCLICK_FAN_MENU §5-0)
    /// ============================================================================
    /// 2026-08-31 사용자 지시: "캐릭터 마우스 우클릭으로 행동이나 설정 변경하는 메뉴 없애고".
    /// 그때의 판단은 지시에 충실했고 옳았다 — 18행 텍스트 메뉴는 36-1의 분류표대로 흩어졌고,
    /// <b>그 18행은 한 칸도 되돌아오지 않는다</b>. 지금 우클릭이 여는 것은 <b>부채꼴 5칸</b>이다.
    ///
    /// ★★ <b>그때 함께 적었던 근거 하나는 사실이 아니었다 — 지우지 않고 정정으로 남긴다.</b>
    /// 원문: <i>"우클릭을 잡으려면 그 순간 클릭관통을 부분 해제해야 하고 … 지금까지 우리는 사용자가
    /// 그 앱에 내리려던 우클릭을 가로채고 있었다"</i>. 그 문장은 <b>클릭관통이 버튼별로 걸린다</b>는
    /// 전제 위에 서 있는데, 2026-09-05 <c>dev-platform</c> 소스 실측이 그 전제를 반증했다:
    /// <c>UniWindowController</c>의 히트테스트는 <b>커서 아래에 콜라이더가 있는가</b> 하나만 보고
    /// <b>버튼 종류를 어디에도 넣지 않는다</b>(<c>EnableClickThrough</c>는 창 전체 속성이다).
    /// ⇒ 커서가 캐릭터 잡기영역 안인 동안 우클릭은 <b>폐지 이후에도 계속 우리 창이 삼키고 있었고</b>,
    /// 폐지가 없앤 것은 그 가로채기가 아니라 <b>그 가로채기로 하던 일</b>뿐이었다.
    /// ⇒ 그래서 재개방의 <b>비침해 순증가는 0</b>이다 — 이미 치르던 비용에 값을 붙이는 것이다.
    /// 좌클릭 드래그&던지기(12절)는 그때도 지금도 그대로 살아 있다.
    ///
    /// 우클릭 배선의 실체는 이 파일 맨 아래 「캐릭터 우클릭 → 부채꼴」 절에 있다.
    ///
    /// 18행 메뉴의 각 항목이 어디로 갔는지는 36-1의 전수 분류표에 있다:
    /// <b>(가) 사용자 행동 명령 7개</b> → <see cref="ActionCommandPopover"/>(부채꼴 ④) /
    /// <b>(나) 설정 3개</b> → 정보창·설정창 / <b>(다) 개발 전용 5개 + 가출 발동</b> →
    /// <see cref="StickMateDevTools"/> 게이트 뒤 단축키만 / <b>(라) 종료</b> → 아래.
    ///
    /// ============================================================================
    /// 종료 경로는 <b>3중</b>이다 (36-10 / 53-6) — 단축키 하나로는 부족하다
    /// ============================================================================
    /// 이 앱에는 Dock 아이콘도, 메뉴바 아이콘도, 트레이도 없다. 단축키 표기가 <b>화면에</b> 나오는
    /// 자리는 설정창 안 네 곳뿐이고(종료 버튼 라벨 Q · [일반] 숨기기 행의 칩과 캡션 K ·
    /// 톱니 경고 캡션 I · 푸터 "여는 방법" P), 그 창에 닿으려면 톱니(마우스) 아니면 또 다른 단축키가
    /// 필요하다 — 즉 창을 한 번도 열지 않은 사용자에게 단축키는 여전히 <b>발견 불가능</b>하다.
    /// 게다가 <c>_keyService</c>가 null인 환경에서는 <b>아예 동작하지 않는다</b>. 우클릭까지 없앤 뒤
    /// 그 환경에 남는 종료 수단이 0이 되면 그건 강제 종료(활성 상태
    /// 보기/작업 관리자) 외에는 끌 수 없는 상주 오버레이이며, 원칙 2·4의 명백한 위반이다.
    ///   ① <b>⌃⌥⌘Q</b> — 여기. <b>개발 게이트 대상이 아니다</b>(릴리스에서 반드시 산다).
    ///   ② <b>부채꼴 [앱 종료]</b>(2단 확인 3초) — 마우스 경로 1. <b>최상위 1클릭 도달</b>.
    ///      (2026-09-06: 축 위 위성 → 다섯 번째 호 슬롯. <b>2단 확인은 한 줄도 안 바뀌었다</b> —
    ///       위치로 가르던 «다름»이 사라진 만큼 이 확인이 오폭 방어의 전부다.)
    ///      ★ 2026-09-05 — 그 부채꼴을 여는 문이 <b>캐릭터 우클릭</b>으로 바뀌었다(톱니는 캐릭터가
    ///      화면에서 사라진 동안에만 나타나는 대기 진입점이 됐다). 경로 수는 3중 그대로다.
    ///      ★ 2026-09-03 사용자 지시로 신설됐고(*"나사 메뉴 지금 4개중에 버튼 하나 추가해서 종료버튼으로
    ///      만들어줘"*), <b>같은 라운드에 행동창 푸터의 종료 칩은 삭제됐다</b> — 36-1이 (라)로 분류한
    ///      것을 (가) 행동 명령창에 얹어 두었던 오분류였다(앱 종료는 캐릭터에게 시키는 일이 아니다).
    ///      경로 수는 3중 그대로다: <b>하나가 늘고 하나가 빠진 것이 아니라, 있던 하나가 제자리로 갔다.</b>
    ///   ③ <b>설정창 [지금 종료]</b>(2단 확인 3초) — 마우스 경로 2. ★ 2026-09-02 정정: 여기 원래
    ///      "②가 마우스만으로 도달하는 유일한 경로"라고 적혀 있었으나 <b>거짓</b>이었다(ux-designer 발견).
    /// 저장은 <c>CharacterProgressionDirector.OnApplicationQuit()</c>이 담당하므로 어느 쪽으로 끄든
    /// 데이터 손실이 없다.
    /// ★ 2026-09-02 정정 — 이 문단은 원래 단축키가 <i>"앱 UI 어디에도 적혀 있지 않다"</i>고 적고
    /// 있었고, 설정창이 생기면서 거짓이 됐다(위 네 자리). 결론(마우스 경로가 따로 있어야 한다)은
    /// 그대로지만 근거가 틀린 채로 남으면, 다음 사람이 그 근거를 지우면서 결론까지 함께 지운다.
    ///
    /// ============================================================================
    /// 기존 안전장치와의 관계 (절대 깨뜨리지 않는다)
    /// ============================================================================
    /// 이 컴포넌트는 <c>SetClickThrough</c>를 **한 번도 호출하지 않는다**. 시작 5초 클릭관통 지연과
    /// Escape 긴급 해제(Core/StickmanAgent)는 그대로 살아 있고, 이 클래스는 그 위에 종료/제어 수단만
    /// 얹는다.
    /// </summary>
    public sealed class AppControlDirector : MonoBehaviour
    {
        private const float PollInterval = 0.05f;          // 20Hz. 단축키 감지에 충분하고 비용은 무시 가능.

        private StickmanAgent _agent;
        private StickConfig _config;
        private IGlobalKeyStateService _keyService;

        private float _pollTimer;

        // 전역 단축키 엣지 판정 — 첫 폴링은 기록만 하고 넘어가, 앱 시작 순간 이미 눌려 있던 키를
        // 명령으로 오인하지 않는다(StickmanClickHitbox의 _globalPressedInitialized와 동일한 관례).
        private bool _hotkeyInitialized;
        private bool _prevQ, _prevC, _prevD, _prevR, _prevB, _prevG;
        private bool _prevT, _prevX, _prevH;
        private bool _prevS, _prevN, _prevJ, _prevF;
        private bool _prevA;
        private bool _prevI;
        private bool _prevP;
        private bool _prevK;

        // 지연 탐색 후 캐시.
        private GraffitiDirector _graffitiDirector;
        private WindowTheftDirector _windowTheftDirector;
        private WindowCrashDirector _windowCrashDirector;
        private HardwareReactionDirector _hardwareDirector;   // (다) 개발 전용.
        private StressGaugeDirector _stressDirector;          // (다) 개발 전용.
        private RunawayDirector _runawayDirector;             // 소환=(가) / 발동=(다).
        private TodoReminderDirector _todoDirector;           // (다) 개발 전용.
        private FocusWatchDirector _focusDirector;            // (다) 90초 데모 — 정식 경로는 부채꼴 ①.
        private ArcheryDirector _archeryDirector;
        private CharacterInfoWindow _infoWindow;
        private SettingsWindow _settingsWindow;

        /// <summary>
        /// 단축키가 부르는 동작. <b>우클릭 메뉴가 폐지되면서 "메뉴 행 번호"라는 의미는 사라졌고</b>,
        /// 이제 순수한 동작 식별자다(값에 의존하는 코드가 없으므로 순서/번호는 자유롭다).
        ///
        /// <b>(다)</b> 표시는 docs/UX_FLOW.md 36-1의 분류다 — 그 항목들은
        /// <see cref="StickMateDevTools.Enabled"/>가 꺼져 있으면 <b>폴링조차 하지 않는다</b>.
        /// </summary>
        private enum ControlAction
        {
            Quit,
            InkColor,
            Rodeo,
            Diagnostics,        // (다)
            SayNow,
            Graffiti,
            WindowTheft,
            WindowCrash,
            HardwareReaction,   // (다)
            StressGauge,        // (다)
            Runaway,            // 소환은 (가), 발동은 (다) — RunawayDirector가 상태로 갈라 준다.
            TodoReminder,       // (다)
            FocusWatch,         // (다) 90초 데모
            Archery,
            CharacterInfo,
            Settings,           // ★ 2026-09-01 신설 — 설정창(⌃⌥⌘P)
            UserHide,           // ★ 2026-09-02 신설 — 사용자 명시 숨김 토글(⌃⌥⌘K)
        }

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            if (_agent == null) _agent = Object.FindFirstObjectByType<StickmanAgent>();
        }

        private void Start()
        {
            _config = _agent != null ? _agent.Config : null;
            _keyService = _agent != null ? _agent.PlatformService as IGlobalKeyStateService : null;

            // ★ 우클릭 채널은 <b>선택적 캐퍼빌리티</b>다 — 없으면 null이 되고 우클릭 경로 전체가 조용히
            //   꺼진다(다른 기능은 한 톨도 영향받지 않는다). NullPlatformWindowService가 그 경우다.
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;
            _menu = _agent != null ? _agent.GetComponent<GearRadialMenuWidget>() : null;
            // 캐릭터 콜라이더는 Awake에서 <b>한 번만</b> 캐시한다 — 구(舊) 우클릭 구현은 폴링마다
            // GetComponentsInChildren을 새로 불러 24시간 상주 앱에 매초 20회의 할당을 냈다.
            _characterColliders = _agent != null
                ? _agent.GetComponentsInChildren<Collider2D>(true)
                : System.Array.Empty<Collider2D>();

            LogStartupBanner();
        }

        /// <summary>
        /// 시작 배너는 <b>두 벌로 갈라진다</b>(36-2 규칙 3) — 릴리스 로그가 <b>존재하지 않는 기능을
        /// 광고하지 않게</b> 한다. 릴리스 빌드에서 "S(스트레스 게이지 순환)"이 로그에 적혀 있는데 눌러도
        /// 아무 일이 없으면, 그건 사용자에게도 팀에게도 거짓말이다.
        /// </summary>
        private void LogStartupBanner()
        {
            // ★ 2026-09-01 — 여기가 "Windows 실기 로그에 macOS 단축키가 찍힌다"의 진원지였다.
            //   표기를 손으로 적으면 Windows 사용자에게 **존재하지 않는 조합**이 안내된다
            //   (Win32WindowService는 GlobalKey.Command를 VK_LWIN으로 읽는다 = Windows 키).
            //   글리프가 아니라 "Control+Option+Command"처럼 **낱말**로 적혀 있었던 탓에
            //   Tests/EditMode/PlatformParityAuditTests의 글리프 스캐너도 잡지 못했다.
            string quitLine = "[앱제어] 준비 완료 — 종료 방법 3가지: " +
                "(1) 전역 단축키 **" + ShortcutLabel.Chord("Q") + "**, " +
                "(2) **캐릭터 우클릭 → 부채꼴 [앱 종료]**(2단 확인 3초), " +
                "(3) **설정창 [지금 종료]**(2단 확인 3초). " +
                // ★ 2026-09-05 — 이 문장은 두 번 고쳐졌다. 원문은 「우클릭이 밑의 앱으로 그대로
                //   관통한다」였고 그것은 <b>거짓</b>이었다(dev-platform §2-2 실측: 히트테스트는 버튼을
                //   구분하지 않으므로 캐릭터 위 우클릭은 그때도 이미 우리 창이 삼켰다 — 반응만 없었다).
                //   같은 날 사용자 지시로 그 삼킴에 값이 붙었다: 우클릭 = 부채꼴.
                "★ 캐릭터 위에서 **마우스 오른쪽 버튼**을 누르면 부채꼴 메뉴가 촤르륵 펼쳐집니다" +
                "(2026-09-05 사용자 지시). 캐릭터 밖의 영역은 예전 그대로 밑에 있는 앱으로 관통합니다(비침해). " +
                "톱니 아이콘은 **평소에는 뜨지 않고**, 캐릭터가 화면에서 사라진 동안(숨기기 / 가출)에만 " +
                "되돌아올 문으로 나타납니다. ";

            string userKeys = "사용자 단축키: " + ShortcutLabel.Chord("C") + "(잉크색 전환) / R(로데오 커서 on-off) / " +
                "**B(말 걸기)** / **G(그라피티)** / **T(창 도둑)** / " +
                "**X(창 부수기)** / **A(활쏘기)** / **N(가출 중이면 돌아오라고 부르기)** / " +
                "**I(캐릭터 정보/장비 창)** / **P(설정창)** / " +
                // ★ 2026-09-02 — K를 다시 목록에 올린다. 격파 놀이 삭제 라운드에 바인딩만 지워지면서
                //   이 자리가 약 9시간 비어 있었고, 이제 사용자 명시 숨김이 쓴다.
                //   ★ 이 줄은 <b>탈출구 고지</b>를 겸한다.
                //   ★★ 2026-09-05 — 이 문장이 <b>정확히 반대</b>가 됐다. 예전에는 «숨는 동안에는 톱니도
                //     함께 사라집니다»였는데, 이제 톱니는 <b>숨는 동안에만</b> 나타난다(대기 톱니).
                "**" + StickmanAgent.UserHideHotkeyLetter + "(지금 숨기기 / 다시 보이기 — <b>같은 키를 다시 누르면 돌아옵니다</b>. " +
                "숨는 동안에는 화면 우상단에 <b>대기 톱니</b>가 나타나 마우스로도 되돌릴 수 있습니다)**. " +
                "이 명령들의 주 경로는 부채꼴 ④ [행동] 창이고, \n" +
                "설정창의 주 경로는 캐릭터 정보창 헤더의 [설정]입니다. ";

            string devKeys = StickMateDevTools.Enabled
                ? "★ 개발 전용 단축키(게이트 열림 — " + StickMateDevTools.SourceLabel + "): " +
                  "D(진단 로그 on-off) / H(하드웨어 반응 미리보기) / S(스트레스 게이지 순환) / " +
                  "J(할일 알림 강제) / F(집중 모드 90초 데모). " +
                  "이 5개는 사용자 UI에 노출되지 않습니다(원칙 1 — 표시된 것과 " +
                  "실제가 달라지는 경로라서). " +
                  // ★ 2026-09-02 정정 — 여기 원래 N이 6번째로 실려 있었고 "이 6개"라고 적었으나
                  //   거짓이었다(verify-change 실측). dev 게이트가 걸린 것은 위 다섯 개뿐이다
                  //   (:244~248의 `dev && chord && ...` 다섯 줄이 전부다). N은 :242에서
                  //   `chord && IsKeyDown(GlobalKey.N)` — 게이트가 없고, 바로 위 userKeys에도
                  //   실려 있어 같은 로그 한 줄이 스스로 모순됐다.
                  //   N의 "가출 강제 발동" 절반만 ForceRunaway 내부에서 게이트를 보고,
                  //   "돌아오라고 부르기" 절반은 릴리스에서 항상 산다 — 그래서 사용자 키다.
                  "(N은 개발 전용이 아닙니다 — 위 사용자 단축키 목록을 보세요.) "
                : $"개발 전용 단축키는 잠겨 있습니다({StickMateDevTools.SourceLabel}) — " +
                  $"환경변수 {StickMateDevTools.EnvironmentVariableName}=1 로 실행하면 열립니다. ";

            // ★ 전역 키 조회가 미지원인 환경은 종료 경로가 마우스 둘(부채꼴 [앱 종료] · 설정창)뿐이 된다 —
            //   그 사실을 로그가 분명히 말해야 팀이 그 환경을 재현했을 때 원인을 즉시 안다(36-10).
            string keyLine = _keyService != null
                ? "전역 키 조회=사용 가능."
                : "전역 키 조회=미지원 — 단축키 전체가 동작하지 않습니다. 이 환경에서 앱을 끄는 유일한 " +
                  "경로는 **캐릭터 우클릭 → 부채꼴 [앱 종료]**입니다(2단 확인 3초).";

            Debug.Log(quitLine + userKeys + devKeys + keyLine);
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
            // ★ 붙잡기(호명 반응 비트)는 벽시계로 만료된다 — 폴링 간격(20Hz)에 얹으면 최악 50ms
            //   늦게 풀려 «대사가 끝났는데 아직 서 있다»가 된다. 그래서 폴링 게이트 <b>앞</b>이다.
            TickReactionHold();

            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer < PollInterval) return;
            _pollTimer = 0f;

            TickHotkeys();
            TickRightClickFan();
        }

        // ==================== 전역 단축키 ====================

        private void TickHotkeys()
        {
            if (_keyService == null) return;

            // 조합키 3개가 모두 눌려 있을 때만 동작키를 본다 — 이 순서가 곧 비침해 보장이다
            // (Platform/IGlobalKeyStateService.cs "비침해 원칙 유지" 절 참고).
            bool chord = IsKeyDown(GlobalKey.Control) && IsKeyDown(GlobalKey.Option) && IsKeyDown(GlobalKey.Command);

            // ★ 36-2 규칙 3 — 게이트가 닫혀 있으면 (다) 키는 <b>읽지도 않는다</b>. 조용한 no-op이
            //   아니라 아예 조회하지 않는 것이 요구사항이다: 남겨두면 20Hz × 5키(D/H/S/J/F)의 네이티브
            //   조회가 릴리스 빌드에서 영원히 도는데, 그 비용을 낼 이유가 없다(하루 종일 켜져 있는 앱).
            //   ★ 2026-09-02 정정 — "6키"였다. 배너의 "이 6개"는 같은 날 고쳤는데 이 줄만 남아,
            //   한 파일이 서로 다른 개수를 말하고 있었다. N은 게이트 밖이다(바로 아래 참조).
            bool dev = StickMateDevTools.Enabled;

            bool q = chord && IsKeyDown(GlobalKey.Q);
            bool c = chord && IsKeyDown(GlobalKey.C);
            bool r = chord && IsKeyDown(GlobalKey.R);
            bool b = chord && IsKeyDown(GlobalKey.B);
            bool g = chord && IsKeyDown(GlobalKey.G);
            bool t = chord && IsKeyDown(GlobalKey.T);
            bool x = chord && IsKeyDown(GlobalKey.X);
            bool aKey = chord && IsKeyDown(GlobalKey.A);
            bool iKey = chord && IsKeyDown(GlobalKey.I);
            bool pKey = chord && IsKeyDown(GlobalKey.P);

            // ★ 2026-09-02 — 사용자 명시 숨김(K). <b>개발 게이트 대상이 아니다</b>: Q(종료)와 같은 이유로
            //   릴리스에서 반드시 살아야 한다. 이 키가 죽으면 숨긴 사용자에게 남는 탈출구가 0이 된다
            //   ★ 2026-09-03 정정 — 옛 근거는 «숨는 동안 톱니·부채꼴·창이 전부 스스로 내려가 마우스
            //   경로가 없다»였다. <b>더 이상 참이 아니다</b>: 사용자 명시 숨김은 캐릭터만 가리고 톱니와
            //   열린 창을 남긴다(StickmanAgent.HidesScreenSurfaces). 그래도 이 키는 게이트 밖에 둔다 —
            //   톱니를 껐거나(설정창 [일반] "톱니 아이콘") 창을 다 닫아 둔 사용자에게는 이것이
            //   여전히 가장 짧은 길이고, Q(종료)와 같은 급의 상시 계약이다.
            bool kKey = chord && IsKeyDown(GlobalKey.K);

            // N은 반쪽만 사용자용이다: 가출 중이면 [돌아와!](상시 탈출구, 원칙 4)이고 그 밖에는 강제
            // 발동(개발 전용)이다. 키 조회는 항상 하고, 갈래는 RunawayDirector가 상태로 나눈다.
            bool n = chord && IsKeyDown(GlobalKey.N);

            bool d = dev && chord && IsKeyDown(GlobalKey.D);
            bool h = dev && chord && IsKeyDown(GlobalKey.H);
            bool sKey = dev && chord && IsKeyDown(GlobalKey.S);
            bool j = dev && chord && IsKeyDown(GlobalKey.J);
            bool f = dev && chord && IsKeyDown(GlobalKey.F);

            if (!_hotkeyInitialized)
            {
                _hotkeyInitialized = true;
                _prevQ = q; _prevC = c; _prevD = d; _prevR = r; _prevB = b; _prevG = g;
                _prevT = t; _prevX = x; _prevH = h;
                _prevS = sKey; _prevN = n; _prevJ = j; _prevF = f;
                _prevA = aKey;
                _prevI = iKey;
                _prevP = pKey;
                _prevK = kKey;
                return;
            }

            bool qRise = q && !_prevQ;
            bool cRise = c && !_prevC;
            bool dRise = d && !_prevD;
            bool rRise = r && !_prevR;
            bool bRise = b && !_prevB;
            bool gRise = g && !_prevG;
            bool tRise = t && !_prevT;
            bool xRise = x && !_prevX;
            bool hRise = h && !_prevH;
            bool sRise = sKey && !_prevS;
            bool nRise = n && !_prevN;
            bool jRise = j && !_prevJ;
            bool fRise = f && !_prevF;
            bool aRise = aKey && !_prevA;
            bool iRise = iKey && !_prevI;
            bool pRise = pKey && !_prevP;
            bool kRise = kKey && !_prevK;
            _prevQ = q; _prevC = c; _prevD = d; _prevR = r; _prevB = b; _prevG = g;
            _prevT = t; _prevX = x; _prevH = h;
            _prevS = sKey; _prevN = n; _prevJ = j; _prevF = f;
            _prevA = aKey;
            _prevI = iKey;
            _prevP = pKey;
            _prevK = kKey;

            if (qRise) Invoke(ControlAction.Quit, HotkeySource("Q"));
            else if (cRise) Invoke(ControlAction.InkColor, HotkeySource("C"));
            else if (rRise) Invoke(ControlAction.Rodeo, HotkeySource("R"));
            else if (dRise) Invoke(ControlAction.Diagnostics, HotkeySource("D"));
            else if (bRise) Invoke(ControlAction.SayNow, HotkeySource("B"));
            else if (gRise) Invoke(ControlAction.Graffiti, HotkeySource("G"));
            else if (tRise) Invoke(ControlAction.WindowTheft, HotkeySource("T"));
            else if (xRise) Invoke(ControlAction.WindowCrash, HotkeySource("X"));
            else if (hRise) Invoke(ControlAction.HardwareReaction, HotkeySource("H"));
            else if (sRise) Invoke(ControlAction.StressGauge, HotkeySource("S"));
            else if (nRise) Invoke(ControlAction.Runaway, HotkeySource("N"));
            else if (jRise) Invoke(ControlAction.TodoReminder, HotkeySource("J"));
            else if (fRise) Invoke(ControlAction.FocusWatch, HotkeySource("F"));
            else if (aRise) Invoke(ControlAction.Archery, HotkeySource("A"));
            else if (iRise) Invoke(ControlAction.CharacterInfo, HotkeySource("I"));
            else if (pRise) Invoke(ControlAction.Settings, HotkeySource("P"));
            else if (kRise) Invoke(ControlAction.UserHide, HotkeySource(StickmanAgent.UserHideHotkeyLetter));
        }

        /// <summary>
        /// 단축키 발동 출처 문구. 조합키 <b>표기</b>는 <see cref="ShortcutLabel"/>에서만 나온다 —
        /// 이 17줄이 <c>"Ctrl+Opt+Cmd+X"</c>를 손으로 들고 있었고, 그래서 Windows 실기 로그에도
        /// macOS 표기가 그대로 찍혔다(2026-09-01 사용자 로그로 확인).
        ///
        /// <para>매 프레임 할당이 아니다: 이 함수는 <b>상승 에지</b>(사용자가 실제로 조합을 누른 순간)
        /// 에서만 불린다. 20Hz 폴링 자체는 문자열을 만들지 않는다.</para>
        /// </summary>
        private static string HotkeySource(string key) => "전역 단축키 " + ShortcutLabel.Chord(key);

        private bool IsKeyDown(GlobalKey key)
            => _keyService != null && _keyService.TryGetKeyPressed(key, out bool pressed) && pressed;

        // ==================== 동작 ====================

        /// <summary>
        /// ★ 앱을 실제로 끄는 <b>단 하나의 자리</b>(2026-09-03 신설).
        ///
        /// <para><b>왜 함수로 뺐나</b>: 이 다섯 줄(로그 + <c>Application.Quit()</c> + 에디터 분기)이
        /// 원래 <b>세 곳</b>에 복사돼 있었다 — 여기, 행동창 푸터 칩, 설정창. 같은 라운드에 부채꼴
        /// [앱 종료]가 네 번째가 될 뻔했고, 행동창 칩이 삭제되면서 그 사본 하나가 사라졌다.
        /// <b>되돌릴 수 없는 행동의 실행부가 여러 벌인 것 자체가 비용이다</b> — 에디터 분기를 한 곳에서
        /// 빠뜨리면 그 경로만 배치모드 테스트를 얼려 버린다.</para>
        ///
        /// <para><c>Interaction/SettingsWindow.cs</c>의 사본은 <b>이번에 안 건드렸다</b>(다른 담당의
        /// 작업 중 파일이다). 그쪽도 이 창구로 모으는 것이 남은 정리다 — 리더에게 보고했다.</para>
        ///
        /// <para>저장은 <c>CharacterProgressionDirector.OnApplicationQuit()</c>이 담당하므로 어느
        /// 경로로 끄든 데이터 손실이 없다.</para>
        /// </summary>
        public static void QuitApplication(string source)
        {
            Debug.Log($"[앱제어] 종료 요청({source}) — Application.Quit()을 호출합니다. " +
                "저장은 CharacterProgressionDirector.OnApplicationQuit()이 담당하므로 데이터 손실이 없습니다. " +
                "안녕히 계세요!");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Invoke(ControlAction action, string source)
        {
            switch (action)
            {
                case ControlAction.Quit:
                    // ★ 개발 게이트 대상이 아니다(36-10) — 릴리스에서 반드시 살아야 한다.
                    QuitApplication(source);
                    break;

                case ControlAction.InkColor:
                    if (_config == null) break;
                    // ★ 2026-08-31 R5 — 배포 에셋의 직렬화 필드(_config.inkColor)에 쓰지 않는다.
                    //   이번 실행의 값은 런타임 오버라이드에, 사용자의 선택은 저장 파일에 남는다
                    //   (Interaction/CharacterInfoWindow.OnInkSwatchClicked와 **같은 경로**).
                    StickmanInkColor nextInk = _config.IsWhiteInk()
                        ? StickmanInkColor.Black
                        : StickmanInkColor.White;
                    _config.SetRuntimeInkColor(nextInk);
                    CharacterAppearanceModel.SetInkColor(nextInk);
                    _agent?.ApplyInkColorFromConfig();
                    // 정보창 스와치/장비 토글과 **같이 즉시 저장**한다. 주기 저장(최대 60초)에만 맡기면
                    // 강제 종료 시 사용자가 방금 고른 색이 사라진다.
                    CharacterSaveStore.Save();
                    Debug.Log($"[앱제어] 잉크색 전환({source}) -> {nextInk} (즉시 저장). " +
                        "주 경로는 정보창의 잉크 스와치입니다.");
                    break;

                case ControlAction.Rodeo:
                    if (_config == null) break;
                    _config.rodeoCursorEnabled = !_config.rodeoCursorEnabled;
                    Debug.Log($"[앱제어] 로데오 커서 {(_config.rodeoCursorEnabled ? "켬" : "끔")}({source}) — " +
                        "이건 자동 발동 게이트이지 발동 명령이 아닙니다(거처는 설정창 [이벤트], 36-1).");
                    break;

                case ControlAction.Diagnostics:
                    if (_config == null) break;
                    _config.verboseDiagnosticsLogging = !_config.verboseDiagnosticsLogging;
                    Debug.Log($"[앱제어] 진단 로그 {(_config.verboseDiagnosticsLogging ? "켬(촘촘)" : "끔(60초 심장박동만)")}({source}).");
                    break;

                case ControlAction.SayNow:
                    ForceSayNow(source);
                    break;

                case ControlAction.Graffiti:
                    ForceGraffiti(source);
                    break;

                case ControlAction.WindowTheft:
                    ForceWindowTheft(source);
                    break;

                case ControlAction.WindowCrash:
                    ForceWindowCrash(source);
                    break;

                case ControlAction.HardwareReaction:
                    ForceHardwareReaction(source);
                    break;

                case ControlAction.StressGauge:
                    ForceStressGauge(source);
                    break;

                case ControlAction.Runaway:
                    ForceRunaway(source);
                    break;

                case ControlAction.TodoReminder:
                    ForceTodoReminder(source);
                    break;

                case ControlAction.FocusWatch:
                    ForceFocusWatch(source);
                    break;

                case ControlAction.Archery:
                    ForceArchery(source);
                    break;

                case ControlAction.CharacterInfo:
                    ToggleCharacterInfo(source);
                    break;

                case ControlAction.Settings:
                    ToggleSettings(source);
                    break;

                case ControlAction.UserHide:
                    ToggleUserHide(source);
                    break;
            }
        }

        /// <summary>
        /// 활쏘기 발동(전역 단축키 Ctrl+Opt+Cmd+A / 행동 명령창 [활쏘기]). 다른 데모 항목과 성격이
        /// 다르다 — <b>확률을 건너뛰는 지름길이 아니라 정식 진입점</b>이다
        /// (StickConfig.archeryChance 기본값 0: 사용자가 요청하지 않은 연출이 뜨는 것에 반복적으로
        /// 불만을 표했기 때문).
        /// </summary>
        private void ForceArchery(string source)
        {
            if (_archeryDirector == null) _archeryDirector = Object.FindFirstObjectByType<ArcheryDirector>();
            if (_archeryDirector == null)
            {
                Debug.LogWarning($"[앱제어] 활쏘기 발동 실패({source}) — 씬에 ArcheryDirector가 없습니다.");
                return;
            }
            _archeryDirector.ForceTriggerNow(source);
        }

        /// <summary>
        /// 캐릭터 정보/장비 창 토글(전역 단축키 Ctrl+Opt+Cmd+I). 주 진입점은 부채꼴 ② [캐릭터]이고
        /// 이쪽은 보조 경로다.
        /// </summary>
        private void ToggleCharacterInfo(string source)
        {
            if (_infoWindow == null) _infoWindow = Object.FindFirstObjectByType<CharacterInfoWindow>();
            if (_infoWindow == null)
            {
                Debug.LogWarning($"[앱제어] 캐릭터 정보창 열기 실패({source}) — 씬에 CharacterInfoWindow가 없습니다.");
                return;
            }
            // 부채꼴/팝오버 정리는 여기서 하지 않는다 — 그 책임은 CharacterInfoWindow.Open() 한 곳에 있다
            // (2026-08-30 배타 모달). 진입점마다 정리 코드를 흩뿌리다 이 경로가 실제로 새고 있었다.
            _infoWindow.Toggle(source);
        }

        /// <summary>
        /// 설정창 토글(전역 단축키 ⌃⌥⌘P, Preferences). 주 진입점은 <b>정보창 헤더의 [설정]</b>이고
        /// 이쪽은 보조 경로다(docs/UX_FLOW.md 36-11).
        /// <para>2026-09-01 <c>⌃⌥⌘,</c>에서 옮겼다 — 그 조합은 macOS 접근성 "대비 줄이기"라
        /// 창을 여닫을 때마다 사용자의 OS 대비 설정이 실제로 내려갔다(<c>Core/ShortcutLabel</c>).</para> 배타 모달 정리는 <see cref="SettingsWindow.Open"/> 한 곳이
        /// 책임진다 — 여기서 다른 창을 닫지 않는다(정보창 경로가 실제로 그렇게 새고 있었다).
        /// </summary>
        private void ToggleSettings(string source)
        {
            if (_settingsWindow == null) _settingsWindow = Object.FindFirstObjectByType<SettingsWindow>();
            if (_settingsWindow == null)
            {
                Debug.LogWarning($"[앱제어] 설정창 열기 실패({source}) — 씬에 SettingsWindow가 없습니다. " +
                    "Assets/Editor/SceneBootstrapper.cs의 EnsurePrefabComponents를 실행했는지 확인하세요.");
                return;
            }
            _settingsWindow.Toggle(source);
        }

        /// <summary>
        /// ★ <b>사용자 명시 숨김</b> 토글(⌃⌥⌘K) — 2026-09-02. 화면공유·발표·녹화 중에 "지금은 나오지 마"를
        /// 사용자가 직접 지시하는 유일한 경로다.
        ///
        /// <para><b>왜 렌더러만 끄지 않는가</b>: 예전에 설정창이 갖고 있던 "지금 즉시 숨기기"는
        /// <c>SetCharacterVisibleNow</c>로 <b>렌더러만</b> 껐다. 그러면 열려 있던 설정창/정보창/부채꼴과
        /// 그 <b>클릭 차단막</b>이 그대로 남아, 발표 화면에는 캐릭터 대신 UI가 찍혔다 — 캐릭터만 사라지고
        /// 창이 남는 쪽이 더 이상하다. 그래서 이 경로는 <see cref="StickmanAgent.SetUserHidden"/>을 통해
        /// 전체화면 감지와 <b>같은 Suspend 경로</b>를 탄다(렌더러·물리·상태가 한 곳에서 멈춘다).</para>
        ///
        /// <para>★★★ <b>2026-09-03 — 범위가 「캐릭터만」으로 좁아졌다</b>(사용자 확정 <i>"캐릭만 가리고"</i>).
        /// 위 문단의 <i>"열려 있던 창과 차단막도 함께 걷는다"</i>는 <b>더 이상 참이 아니다</b>:
        /// 같은 Suspend 경로를 타되 <see cref="StickmanAgent.HidesScreenSurfaces"/>가 false라
        /// 톱니·열린 창·부채꼴은 남는다. 사용자가 겪은 실제 사고는 «발표 화면에 UI가 찍힘»이 아니라
        /// <b>«갇힘»</b>이었다 — <i>"전부 다 없어져버려서 다시 나오게 할 방법이 없어"</i>.</para>
        ///
        /// <para><b>왜 개발 게이트 뒤가 아닌가</b>: Q(종료)와 같다. 톱니를 꺼 둔 사용자에게는 이것이
        /// 여전히 가장 짧은 복귀 경로다.</para>
        /// </summary>
        private void ToggleUserHide(string source)
        {
            if (_agent == null)
            {
                Debug.LogWarning($"[앱제어] 숨기기/보이기 실패({source}) — 씬에 StickmanAgent가 없습니다.");
                return;
            }

            bool hidden = _agent.ToggleUserHidden(source);
            Debug.Log($"[앱제어] 캐릭터 {(hidden ? "숨김" : "다시 보이기")}({source}) — " +
                (hidden
                    ? "캐릭터와 거기 붙은 것(말풍선·이펙트·장비·펫)만 가렸습니다 — 톱니·열린 창·부채꼴은 " +
                      "그대로 남습니다. 전체화면 앱을 오갔다 와도 되살아나지 않습니다. 같은 키를 다시 " +
                      "누르거나 설정창 [일반] > [보이기]로 돌아옵니다."
                    : "캐릭터를 다시 보이게 했습니다."));
        }

        // ==================== 말 걸기 (행동 명령창의 7번째 명령) ====================

        /// <summary>
        /// ★ 지금 말을 걸 수 있는가 — 행동 명령창의 회색 처리와 <see cref="ForceSayNow"/>가 함께 쓰는
        /// 단 하나의 판정(docs/UX_FLOW.md 36-7).
        ///
        /// 다른 6개와 달리 <see cref="SpectacleEventLock"/>을 보지 않는다: 이 명령은 락을 잡지 않고
        /// 상태 전이도 <b>같은 상태로의 재진입</b>뿐이기 때문이다. 대신 진입 조건(Idle/Walk)은 같다.
        /// </summary>
        public CommandAvailability GetSayNowAvailability()
        {
            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Machine == null) return CommandAvailability.Missing;

            // ★★★ 2026-09-03 — 캐릭터가 안 보이면 말풍선의 주인이 없다(원칙 1). 다른 6개와 같은 게이트.
            if (HiddenCharacterCommandGate.BlocksNow(_agent)) return HiddenCharacterCommandGate.WhileHidden;

            StickmanStateId current = blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(current));

            return CommandAvailability.Ready;
        }

        /// <summary>
        /// "지금 즉시 한마디 하게 한다"(Ctrl+Opt+Cmd+B / 행동 명령창 [말 걸기]).
        ///
        /// **원칙 1을 우회하지 않는다**: 대사 문자열을 직접 만들어 이벤트로 쏘는 게 아니라,
        /// 블랙보드에 강제 발화 펄스를 세운 뒤 <b>실제 상태 전이</b>(지금 상태로의 재진입)를 일으킨다.
        /// 대사는 여전히 그 전이가 확정된 뒤 Idle/WalkState.Enter() 안에서만 파생된다 —
        /// "혼잣말을 한다"는 행동 자체가 이 전이로 확정된 사실이 된다. 36-1이 이 항목을 (가)로 분류하며
        /// "원칙 1을 우회하지 않는 유일한 방식"이라고 적은 근거가 이것이다.
        ///
        /// Idle/Walk가 아닐 때는 아무것도 하지 않는다. 진행 중인 행동을 대사를 보여주자고 중단시키는
        /// 것이야말로 5절이 막으려는 "텍스트가 행동을 끌고 가는" 구조다.
        /// </summary>
        /// <returns>실제로 발화 전이를 일으켰는가.</returns>
        public bool ForceSayNow(string source)
        {
            CommandAvailability availability = GetSayNowAvailability();
            if (!availability.IsReady)
            {
                Debug.Log($"[앱제어] 말 걸기({source}) 건너뜀 — {availability.Reason}" +
                    "(진행 중인 행동을 대사 때문에 중단시키지 않는다 — UX_FLOW.md 5절).");
                return false;
            }

            StickmanBlackboard blackboard = _agent.Blackboard;
            StickmanStateId current = blackboard.Machine.CurrentStateId;
            blackboard.ForcedChatterSignaled = true;      // 확률/쿨다운을 건너뛰는 1프레임 펄스.
            blackboard.Machine.ChangeState(current);      // 같은 상태로 재진입 = Enter()가 다시 확정 실행된다.
            Debug.Log($"[앱제어] 말 걸기({source}) — {current} 재진입으로 대사를 파생시켰습니다.");
            return true;
        }

        // ==================== (가) 사용자 명령 진입점 ====================

        private void ForceGraffiti(string source)
        {
            if (_graffitiDirector == null) _graffitiDirector = Object.FindFirstObjectByType<GraffitiDirector>();
            if (_graffitiDirector == null)
            {
                Debug.LogWarning($"[앱제어] 그라피티({source}) — 씬에 GraffitiDirector가 없어 건너뜁니다.");
                return;
            }
            _graffitiDirector.ForceTriggerNow($"앱제어 {source}");
        }

        private void ForceWindowTheft(string source)
        {
            if (_windowTheftDirector == null) _windowTheftDirector = Object.FindFirstObjectByType<WindowTheftDirector>();
            if (_windowTheftDirector == null)
            {
                Debug.LogWarning($"[앱제어] 창 도둑({source}) — 씬에 WindowTheftDirector가 없어 건너뜁니다.");
                return;
            }
            _windowTheftDirector.ForceTriggerNow($"앱제어 {source}");
        }

        private void ForceWindowCrash(string source)
        {
            if (_windowCrashDirector == null) _windowCrashDirector = Object.FindFirstObjectByType<WindowCrashDirector>();
            if (_windowCrashDirector == null)
            {
                Debug.LogWarning($"[앱제어] 창 부수기({source}) — 씬에 WindowCrashDirector가 없어 건너뜁니다.");
                return;
            }
            _windowCrashDirector.ForceTriggerNow($"앱제어 {source}");
        }

        /// <summary>
        /// 가출 발동 / 돌아오라고 부르기(Ctrl+Opt+Cmd+N). <b>36-1이 이 키를 반으로 갈랐다</b>:
        /// 가출 중이면 [돌아와!](= 20절이 "찾기 미니게임을 강제하지 않는 상시 탈출구"로 못박은 원칙 4
        /// 장치, <b>(가) 사용자 명령</b>)이고, 그 밖에는 강제 발동(<b>(다) 개발 전용</b> — 가출은
        /// 스트레스의 <b>결과</b>인데 원인 없이 결과를 만드는 것이라 원칙 1 위반이다).
        ///
        /// 그래서 게이트를 <b>발동측에만</b> 건다 — 여기서 키 전체를 잠그면 릴리스 빌드에서 탈출구가
        /// 사라진다. 소환은 행동 명령창 헤더의 [돌아와!] 칩이 주 경로이고 이 키는 보조다.
        /// </summary>
        private void ForceRunaway(string source)
        {
            if (_runawayDirector == null) _runawayDirector = Object.FindFirstObjectByType<RunawayDirector>();
            if (_runawayDirector == null)
            {
                Debug.LogWarning($"[앱제어] 가출({source}) — 씬에 RunawayDirector가 없어 건너뜁니다.");
                return;
            }

            if (_runawayDirector.IsRunawayActive)
            {
                _runawayDirector.TryRecallNow($"앱제어 {source}");
                return;
            }

            if (!StickMateDevTools.Enabled)
            {
                Debug.Log($"[앱제어] 가출 강제 발동({source}) 건너뜀 — 개발 전용 경로입니다(36-1: 가출은 " +
                    "스트레스의 결과이지 명령이 아니다). 소환([돌아와!])은 가출 중일 때 언제나 동작합니다.");
                return;
            }
            _runawayDirector.TryForceRunawayNow($"앱제어 {source}");
        }

        // ==================== (다) 개발 전용 진입점 ====================
        // 아래 4개는 StickMateDevTools 게이트가 열려 있을 때만 키가 조회되므로(TickHotkeys) 여기까지
        // 도달하지 않는다. 그래도 각 메서드에 게이트를 다시 걸지 않는 이유: 게이트를 두 곳에 두면
        // "어느 쪽이 진짜인가"가 생겨 이 라운드가 없애려는 바로 그 문제(진실 두 벌)를 만든다.

        /// <summary>
        /// 하드웨어 반응 데모 미리보기(개발 전용 ⌃⌥⌘H). <b>확률을 건너뛰는 게 아니라 실제로는 일어나지
        /// 않은 신호의 연출만</b> 보여주는 경로다 — 배터리 90%인데 배터리 경고 반응을 시키는 것이라
        /// 원칙 1의 정면 위반이고, 그래서 사용자 UI에 자리가 없다(36-1의 9행).
        /// </summary>
        private void ForceHardwareReaction(string source)
        {
            if (_hardwareDirector == null) _hardwareDirector = Object.FindFirstObjectByType<HardwareReactionDirector>();
            if (_hardwareDirector == null)
            {
                Debug.LogWarning($"[앱제어] 하드웨어 반응({source}) — 씬에 HardwareReactionDirector가 없어 건너뜁니다.");
                return;
            }
            _hardwareDirector.ForceTriggerNow($"앱제어 {source}");
        }

        /// <summary>
        /// 스트레스 게이지 단계 순환(개발 전용 ⌃⌥⌘S). <b>실사용에서는 수 시간~반나절이 걸려야 쌓이는
        /// 값</b>을 미리 세워 보는 것이라 게이지 표시값을 거짓으로 만든다. 값 <b>읽기</b>는 정보창/설정창의
        /// 읽기 전용 표시로 살아 있고, <b>쓰기</b>만 여기 있다(36-1의 10행).
        /// </summary>
        private void ForceStressGauge(string source)
        {
            if (_stressDirector == null) _stressDirector = Object.FindFirstObjectByType<StressGaugeDirector>();
            if (_stressDirector == null)
            {
                Debug.LogWarning($"[앱제어] 스트레스 게이지({source}) — 씬에 StressGaugeDirector가 없어 건너뜁니다.");
                return;
            }
            _stressDirector.ForceTriggerNow($"앱제어 {source}");
        }

        /// <summary>
        /// 할일 리마인더 강제 발동(개발 전용 ⌃⌥⌘J). ★ 2026-08-31 — 이 경로는 더 이상 <b>가짜 할일을
        /// 추가하지 않는다</b>(TodoReminderDirector 클래스 문서의 버그 수정 절 참고). 할일을 넣는 유일한
        /// 경로는 부채꼴 ③ [오늘 할일]의 입력칸이다.
        /// </summary>
        private void ForceTodoReminder(string source)
        {
            if (_todoDirector == null) _todoDirector = Object.FindFirstObjectByType<TodoReminderDirector>();
            if (_todoDirector == null)
            {
                Debug.LogWarning($"[앱제어] 할일 알림({source}) — 씬에 TodoReminderDirector가 없어 건너뜁니다.");
                return;
            }
            _todoDirector.ForceTriggerNow($"앱제어 {source}");
        }

        /// <summary>
        /// 집중 모드 <b>90초 데모</b> 세션(개발 전용 ⌃⌥⌘F). 정식 경로는 부채꼴 ① →
        /// <see cref="FocusSessionPopover"/>에서 사용자가 15/25/50분을 고르는 것이다. "25분"을 고른
        /// 사용자에게 90초짜리 세션을 주면 그 순간 화면의 숫자가 거짓이 된다(36-1의 13행).
        /// </summary>
        private void ForceFocusWatch(string source)
        {
            if (_focusDirector == null) _focusDirector = Object.FindFirstObjectByType<FocusWatchDirector>();
            if (_focusDirector == null)
            {
                Debug.LogWarning($"[앱제어] 집중 모드({source}) — 씬에 FocusWatchDirector가 없어 건너뜁니다.");
                return;
            }
            _focusDirector.ForceTriggerNow($"앱제어 {source}");
        }

        // ============================================================================
        // ★★ 캐릭터 우클릭 → 부채꼴 (2026-09-05 사용자 지시)
        // ============================================================================
        //
        // 사용자 원문: "지금은 메뉴 스크류모양이 따로 있는데 그냥 캐릭터에서 마우스 오른 쪽 버튼 누르면
        // 촤르륵 펼쳐지게 변경". 설계 정본은 docs/UX_RIGHTCLICK_FAN_MENU.md(기하·순서) ·
        // docs/PLATFORM_RIGHTCLICK_FAN.md(플랫폼 실체) · docs/UX_MOTION_FAN_AND_CAPE.md §1(박자)다.
        //
        // ★ 이 파일이 그 자리인 이유: 2026-08-31에 <b>여기서</b> 우클릭 폴링이 지워졌고
        //   (git 767c985^), Platform/IGlobalPointerButtonService.cs의 계약 문서가 지금도
        //   "Interaction/AppControlDirector.cs"를 가리킨다 — 그 문서를 다시 참으로 만든다.
        //   신규 컴포넌트를 만들지 않으므로 씬/프리팹 배선이 0줄이다(누락이 반복 재발한 함정이다).
        //
        // ★ <b>왜 비침해 비용이 0인가</b>(dev-platform §2-2 실측): 히트테스트는 <b>버튼을 구분하지
        //   않는다</b>. 커서가 캐릭터 잡기영역 안이면 클릭관통은 <b>이미</b> 꺼져 있고, 우클릭은
        //   <b>지금도</b> 우리 창이 삼키고 있다 — 그러고 아무 일도 안 했다.
        //   즉 이 변경은 새로 가로채는 것이 아니라 <b>낭비되던 가로채기에 값을 붙이는 것</b>이다.
        //   여기서 SetClickThrough를 <b>한 번도 부르지 않는다</b>(관통 해제는 히트테스트가 상시로 한다).

        /// <summary>
        /// ★ 「지금 부채꼴을 열어도 되는가」 — <b>OS 호출 0줄 순수 판정</b>(테스트 설계 C-1).
        /// EditMode가 전 분기를 씬 없이 실행할 수 있어야 하므로 상태를 하나도 읽지 않는다.
        /// </summary>
        public static class RightClickFanGatePolicy
        {
            /// <summary>
            /// ★★ <b>리더 판정 L-2 — fail-open.</b> 삼킴 상태를 <b>못 읽으면 통과시킨다</b>.
            ///
            /// <para>비대칭이 명백하다: 못 읽을 때 닫아걸면 «우클릭이 아예 안 먹는다»가 되고 그것은
            /// <b>사용자가 신고한 바로 그 증상</b>이며 <b>조용하다</b>(로그 없이는 우리도 못 본다).
            /// 열어 두면 최악이 «아주 가끔 아래 앱 메뉴가 같이 뜬다»이고 그것은 <b>눈에 보이고 즉시
            /// 회복된다</b>. 그리고 macOS에서는 우클릭이 평상시 유일한 마우스 진입점이다.</para>
            ///
            /// <para>이 함수를 fail-closed(<c>queried &amp;&amp; swallowed</c>)로 되돌리면 그 판정을
            /// 뒤집는 것이다.</para>
            /// </summary>
            /// <param name="queried">삼킴 상태를 <b>읽는 데 성공했는가</b>(미지원이면 false).</param>
            /// <param name="swallowed">읽었다면 그 값 — 지금 이 클릭이 우리 창에 삼켜졌는가.</param>
            public static bool SwallowAllowsOpen(bool queried, bool swallowed) => !queried || swallowed;

            /// <summary>
            /// 다섯 항의 곱. 항이 다섯이라 <b>2⁵ = 32행 전수</b>를 EditMode가 루프로 돌 수 있다.
            /// <para>순서는 docs/UX_RIGHTCLICK_FAN_MENU.md §10-1 #3 그대로다.</para>
            /// </summary>
            public static bool ShouldOpenFan(bool cursorOverCharacter, bool secondaryRisingEdge,
                bool swallowAllowsOpen, bool panelsSuppressed, bool primaryButtonHeld)
                => cursorOverCharacter
                && secondaryRisingEdge
                && swallowAllowsOpen
                && !panelsSuppressed
                && !primaryButtonHeld;
        }

        /// <summary>삼킴 상태 조회 델리게이트 — <c>out</c> 매개변수라 <c>System.Func</c>로 표현할 수 없다.</summary>
        public delegate bool PointerSwallowQuery(out bool swallowed);

        /// <summary>
        /// ★ 삼킴 상태 조회 창구. <b>지금은 비어 있다</b>(= 미지원 = fail-open으로 통과).
        ///
        /// <para><c>dev-platform</c>이 <c>IPointerSwallowStateSource</c>(<c>Platform/</c>, 중립)를 내고
        /// 양 플랫폼이 <c>UniWindowController.isClickThrough</c> 되읽기 <b>한 줄</b>씩을 구현하면,
        /// 여기에 그 구현을 꽂는 것으로 히트테스트 1렌더프레임 경합(최악 ~67ms, 정지 등급)이 닫힌다.
        /// <b>그때까지는 fail-open이 그 자리를 대신한다 — 그것이 L-2의 뜻이다.</b></para>
        ///
        /// <para>테스트는 이 자리에 가짜 구현을 꽂아 세 갈래(미지원 / 삼켜짐 / 명시적 미삼킴)를 각각
        /// 재현한다(테스트 설계 C-5).</para>
        /// </summary>
        public static PointerSwallowQuery SwallowStateProvider { get; set; }

        private IGlobalPointerButtonService _buttonService;
        private GearRadialMenuWidget _menu;
        private Collider2D[] _characterColliders = System.Array.Empty<Collider2D>();

        // 우클릭은 <b>상승 엣지 1회</b>만 의미를 갖는다. 하강 엣지에는 아무 일도 하지 않는다 —
        // 우클릭 드래그에 의미를 주면 「데스크톱 러버밴드 선택」 관습과 충돌한다(dev-platform §3-2).
        // 첫 폴링은 기록만 하고 넘어가 앱 시작 순간 눌려 있던 버튼을 명령으로 오인하지 않는다.
        private bool _rightPrev;
        private bool _rightInitialized;

        private void TickRightClickFan()
        {
            if (_buttonService == null || _agent == null || _menu == null) return;
            if (!_buttonService.TryGetSecondaryButtonPressed(out bool right))
            {
                _rightInitialized = false;   // 상태를 잃었으면 다음 폴링이 엣지를 새로 잡게 한다.
                return;
            }
            if (!_rightInitialized) { _rightInitialized = true; _rightPrev = right; return; }

            bool rising = right && !_rightPrev;
            _rightPrev = right;
            if (!rising) return;

            bool primaryHeld = _buttonService.TryGetPrimaryButtonPressed(out bool left) && left;
            bool queried = TryReadPointerSwallowed(out bool swallowed);

            // ── 게이트 0~2 (§10-4) ──────────────────────────────────────────────
            if (!RightClickFanGatePolicy.ShouldOpenFan(
                    IsCursorOverCharacter(), true,
                    RightClickFanGatePolicy.SwallowAllowsOpen(queried, swallowed),
                    _agent.ArePanelsSuppressed, primaryHeld))
            {
                return;
            }

            // ── 게이트 3 — 배타 표면이 떠 있으면 «지금 떠 있는 표면을 닫는다»가 이 앱의 재입력 관례다.
            //    톱니의 ActivateClick 첫 분기와 같은 판단이고, 닫으려던 사용자에게 곧바로 다른 UI를
            //    들이밀지 않는다. 다음 우클릭이 평소처럼 부채꼴을 편다.
            if (_infoWindow == null) _infoWindow = Object.FindFirstObjectByType<CharacterInfoWindow>();
            if (_infoWindow != null && _infoWindow.IsOpen)
            {
                _infoWindow.Close("캐릭터 우클릭(창 닫기)");
                return;
            }

            // ── 게이트 4 — 같은 문이면 토글 닫기, 다른 문이면 재앵커(§5-7).
            if (_menu.IsExpanded && _menu.AnchorSource == GearMenuAnchorSource.Character)
            {
                _menu.Collapse(GearMenuCollapseMode.User, "캐릭터 재우클릭(토글 닫기)");
                return;
            }

            if (!TryResolveCharacterAnchor(out Vector2 anchorUnityScreen)) return;

            // ── 5. 프레임 페이싱 홀드는 Expand() 첫 줄이 부른다(L-10) — 여기서 또 부르지 않는다.
            // ── 6. ★★ 등급 1 탈출구의 허가. <b>반드시 펼침 앞</b>이다: 부채꼴은 자기 LateUpdate에서
            //       ArePanelsSuppressed를 폴링해 스스로 접으므로, 허가가 펼침보다 늦으면 <b>펼쳐지는
            //       그 프레임에 회수되어</b> 사용자 눈에는 "우클릭이 안 먹는다"로 보인다.
            //       그것이 2026-09-03에 톱니에서 실제로 났던 증상이다(InfoGearIconWidget.ActivateClick).
            _agent.TryGrantUserSummon("캐릭터 우클릭");

            // ── 7. 호명 반응 비트 — <b>펼침보다 먼저</b>다(원칙 1, MOTION §1-8-7의 ②·④ < ⑤).
            BeginReactionHold();

            // ── 8. 펼침. 앵커는 이 프레임의 몸 중심으로 <b>동결</b>된다(캐릭터를 따라가지 않는다).
            _menu.ExpandOrReanchor(anchorUnityScreen, GearMenuAnchorSource.Character,
                GearRadialMenuWidget.FanUpBiasPoints, "캐릭터 우클릭(재앵커)");
        }

        /// <summary>삼킴 상태를 읽는 유일한 창구. 꽂힌 구현이 없으면 «미지원»이고, 그 경우
        /// <see cref="RightClickFanGatePolicy.SwallowAllowsOpen"/>이 통과시킨다(L-2 fail-open).</summary>
        private bool TryReadPointerSwallowed(out bool swallowed)
        {
            swallowed = false;
            PointerSwallowQuery provider = SwallowStateProvider;
            return provider != null && provider(out swallowed);
        }

        /// <summary>
        /// ★ <b>앵커 = 캐릭터 몸 중심</b>(머리 중심이 아니다 — docs/UX_RIGHTCLICK_FAN_MENU.md §1-2).
        ///
        /// <para>머리 중심에 걸면 배율 1.00의 아래 세 방향에서 버튼 히트원이 잡기영역을
        /// <b>−5.21pt 관통</b>한다(캐릭터는 앵커 기준으로 <b>아래로만</b> 길기 때문이다).
        /// 몸 중심은 그 비대칭이 사라져 <b>전 배율·전 방향에서 최소 여유 +29.93pt</b>다.</para>
        ///
        /// <para><b>새 측정 코드가 필요 없다</b>: 이 좌표는 이미 있다 —
        /// <c>SceneBootstrapper</c>가 만드는 GrabArea 캡슐의 <c>offset = (0, 전신 × 0.5)</c>가
        /// 정확히 몸 중심이고, 여기서는 같은 값을 <c>Body.position + 신장/2</c>로 얻는다.</para>
        /// </summary>
        private bool TryResolveCharacterAnchor(out Vector2 anchorUnityScreen)
        {
            anchorUnityScreen = default;
            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null || blackboard.MainCamera == null) return false;

            Vector2 foot = blackboard.Body.position;
            Vector2 bodyCenter = foot + new Vector2(0f, blackboard.CharacterHeightWorld * 0.5f);
            Vector3 screen = blackboard.MainCamera.WorldToScreenPoint(bodyCenter);
            anchorUnityScreen = new Vector2(screen.x, screen.y);
            return true;
        }

        /// <summary>
        /// 커서가 캐릭터의 콜라이더 집합 안인가 — <see cref="StickmanClickHitbox"/>가 쓰는 것과
        /// <b>같은 집합</b>을 <b>읽기만</b> 한다.
        ///
        /// <para>★ 그 파일에 두 번째 버튼을 얹지 않는 이유(dev-platform §3-3): 그쪽은 <c>_pressed</c>
        /// 플래그 <b>하나</b>로 이중 입력 경로를 엣지 트리거하는 구조라, 우클릭을 같은 플래그에 태우면
        /// <b>좌클릭 드래그가 조용히 죽는다</b>.</para>
        ///
        /// <para>★ 가출 연출의 과자 같은 <b>임시 콜라이더</b>(<c>RegisterExtraCollider</c>)는
        /// <b>일부러 보지 않는다</b> — 과자를 우클릭했다고 부채꼴이 뜰 이유가 없다.</para>
        /// </summary>
        private bool IsCursorOverCharacter()
        {
            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null) return false;
            if (!blackboard.TryGetCursorWorldPosition(out Vector2 cursorWorld)) return false;

            for (int i = 0; i < _characterColliders.Length; i++)
            {
                Collider2D c = _characterColliders[i];
                if (c == null || !c.enabled) continue;
                if (c.OverlapPoint(cursorWorld)) return true;
            }
            return false;
        }

        // ============================================================================
        // ★ 호명 반응 비트 (design-motion §1-8)
        // ============================================================================
        //
        // 문제: 부채꼴은 <b>연 자리에 고정</b>되는데 말풍선은 <b>머리를 따라간다</b>. 그대로 두면
        // 캐릭터가 자기 메뉴를 두고 걸어가면서 그 메뉴에 대한 대사를 한다 — 원칙 1의 문자는 지키고
        // 정신이 깨지는 형태다(실측 분리 126.2pt > 호 궤도 111pt).
        //
        // 판정: <b>우클릭 확정 → MoveInputX = 0 고정 → 상태 기계가 <i>스스로</i> Walk → Idle 전이를
        // 낸다(moveInputDeadzone 0.15) → 대사는 <i>그 전이</i>에서 파생된다.</b>
        // ⇒ 새 상태 0개 · 새 인터페이스 0개 · 와이어 포맷(StickmanStateId) 무변경.
        // ⇒ 그리고 감속을 설계할 필요도 없다 — IdleState.Enter()가 이미 v.x = 0을 한다.
        //
        // ★ <b>메뉴가 캐릭터를 멈추는 것이 아니다.</b> 붙잡는 주체는 <b>캐릭터 자신의 발화</b>이고,
        //   붙잡는 길이는 그 대사의 노출 상한이다. 대사가 없으면 펼침 예산(0.300초)이고, 그 뒤
        //   캐릭터는 즉시 다시 걷는다(부채꼴은 접지 않는다).
        //
        // ★★ <b>design-narrative 문구 대기, 임시값</b> — 반응 대사 문자열은 아직 확정되지 않았다.
        //   <see cref="ReactionLineProvider"/>가 그 자리이고, <b>비어 있는 동안에는 아무 말도 하지
        //   않는다</b>(붙잡기 0.300초). <see cref="PlaceholderReactionLine"/>은 예산 계산이 실제로
        //   도는지를 재기 위한 <b>임시값</b>이며 프로덕션에서 발화되지 않는다.
        //   ⇒ 문구가 오면 프로바이더 한 줄을 꽂는 것으로 끝난다. 문구를 여기서 지어내지 않는다.

        /// <summary>design-narrative 문구 대기, 임시값. 7자 = 붙잡기 1.910초(MOTION §1-8-5의 권장 상한 근처).</summary>
        public const string PlaceholderReactionLine = "어, 불렀어?";

        /// <summary>반응 대사 공급자. <b>비어 있으면 대사 없음</b>(붙잡기 = 펼침 예산).</summary>
        public static System.Func<string> ReactionLineProvider { get; set; }

        /// <summary>
        /// 붙잡는 시간(초) = <c>대사의 노출 상한</c>, 대사가 없으면 <c>ExpandTotalSeconds</c>.
        /// <b>순수 함수</b>라 EditMode가 문구 길이별 예산을 씬 없이 잰다.
        /// <para>★ 상한 노출을 고르는 이유(MOTION §1-8-4): 그보다 짧은 값은 전부 «대사가 화면에 남아
        /// 있는 동안 캐릭터가 걸어가는» 잔여 거리를 남긴다(최소 노출로 잡아도 73.0pt).
        /// 상한이 <b>분리를 가능하지 않게 만드는 가장 짧은 값</b>이다.</para>
        /// </summary>
        public static float ReactionHoldSecondsFor(string line)
            => string.IsNullOrEmpty(line)
                ? GearRadialMenuWidget.ExpandTotalSeconds
                : Dialogue.DialogueBudget.MaxVisibleSecondsFor(line,
                    Dialogue.DialogueTiming.PopInSeconds, Dialogue.DialogueTiming.FadeOutSeconds);

        /// <summary>붙잡기가 끝나는 벽시계 시각(음수 = 붙잡는 중이 아님).</summary>
        private float _reactionHoldUntil = -1f;
        private ReactionHoldIntent _reactionHold;

        /// <summary>지금 호명 반응으로 캐릭터를 붙잡고 있는가(진단/테스트 창구).</summary>
        public bool IsReactionHoldActive => _reactionHold != null;

        /// <summary>이번 붙잡기의 총 길이(초). 붙잡는 중이 아니면 0.</summary>
        public float ReactionHoldSeconds { get; private set; }

        /// <summary>
        /// ★ 이동 의도를 <b>감싸는</b> 어댑터 — 원본을 그대로 전달하고 <see cref="MoveInputX"/>만 0으로 덮는다.
        ///
        /// <para><b>왜 배회 컨트롤러를 고치지 않는가</b>: 이 붙잡기는 <b>UI가 만든 일시적 사건</b>이고,
        /// 배회 AI의 규칙이 아니다. 감싸면 <b>되돌리기가 참조 하나</b>이고 원본의 타이머·계획
        /// (<c>IPlannedDwellSource</c>)이 한 톨도 훼손되지 않는다.</para>
        ///
        /// <para>★ 펄스 채널 넷(점프·매달리기·뛰어내리기·기어오르기)도 <b>0으로 막는다</b> — 이동만
        /// 막고 점프를 남기면 «말하는 도중에 뛰는» 화면이 나온다.</para>
        /// </summary>
        private sealed class ReactionHoldIntent : IMovementIntentSource, IPlannedDwellSource
        {
            public readonly IMovementIntentSource Inner;

            public ReactionHoldIntent(IMovementIntentSource inner) { Inner = inner; }

            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;

            public float PlannedDwellRemainingSeconds =>
                Inner is IPlannedDwellSource planned ? planned.PlannedDwellRemainingSeconds : float.NaN;
        }

        private void BeginReactionHold()
        {
            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.IntentSource == null) return;
            if (_reactionHold != null) return;   // 이미 붙잡는 중이면 늘리지 않는다.

            string line = ReactionLineProvider != null ? ReactionLineProvider() : null;
            ReactionHoldSeconds = ReactionHoldSecondsFor(line);

            _reactionHold = new ReactionHoldIntent(blackboard.IntentSource);
            blackboard.IntentSource = _reactionHold;
            _reactionHoldUntil = Time.unscaledTime + ReactionHoldSeconds;

            Debug.Log($"[앱제어] 호명 반응 — 이동 입력을 {ReactionHoldSeconds:F3}초 동안 0으로 붙잡습니다. " +
                "상태 기계가 스스로 Walk → Idle 전이를 내고, 대사는 <b>그 전이</b>에서 파생됩니다(원칙 1). " +
                (string.IsNullOrEmpty(line)
                    ? "반응 대사는 아직 없습니다(design-narrative 대기) — 붙잡기는 펼침 예산과 같습니다."
                    : $"대사 \"{line}\"의 노출 상한이 곧 붙잡는 시간입니다."));
        }

        /// <summary>붙잡기 만료 — 소유권을 배회 AI에 <b>돌려준다</b>. 부채꼴은 그대로 열려 있다(접지 않는다).
        /// <para>★ 내가 꽂아 둔 그 참조일 때만 되돌린다. 그 사이 다른 주인이 의도 소스를 갈아 끼웠다면
        /// 남의 것을 덮어쓰지 않고 조용히 손을 뗀다.</para></summary>
        private void TickReactionHold()
        {
            if (_reactionHold == null) return;
            if (Time.unscaledTime < _reactionHoldUntil) return;

            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard != null && ReferenceEquals(blackboard.IntentSource, _reactionHold))
                blackboard.IntentSource = _reactionHold.Inner;

            _reactionHold = null;
            _reactionHoldUntil = -1f;
            ReactionHoldSeconds = 0f;
        }

        /// <summary>씬이 내려갈 때 소유권을 반드시 돌려준다 — 붙잡은 채로 죽으면 캐릭터가 영원히 선다.</summary>
        private void OnDisable()
        {
            if (_reactionHold == null) return;
            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard != null && ReferenceEquals(blackboard.IntentSource, _reactionHold))
                blackboard.IntentSource = _reactionHold.Inner;
            _reactionHold = null;
            _reactionHoldUntil = -1f;
            ReactionHoldSeconds = 0f;
        }
    }
}
