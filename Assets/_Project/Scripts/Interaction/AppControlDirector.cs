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
    /// 3안(**캐릭터 우클릭 메뉴**) — <b>2026-08-31 폐지</b>. 아래 참조.
    ///
    /// ============================================================================
    /// ★★ 2026-08-31 — 캐릭터 우클릭 메뉴를 <b>UI와 폴링까지</b> 제거했다 (docs/UX_FLOW.md 36-9)
    /// ============================================================================
    /// 사용자 지시: "캐릭터 마우스 우클릭으로 행동이나 설정 변경하는 메뉴 없애고".
    ///
    /// 지시 이행 외에 <b>비침해가 실제로 개선된다</b>: 우클릭을 잡으려면 그 순간 클릭관통을 부분
    /// 해제해야 하고, 캐릭터는 남의 창 위에 서 있다 — 지금까지 우리는 <b>사용자가 그 앱에 내리려던
    /// 우클릭(문맥 메뉴)을 가로채고 있었다</b>. 제공할 메뉴가 사라지면 그 비용만 남는다. 원칙 2에
    /// 비추어 제거가 정답이다. 그래서 메뉴 UI뿐 아니라 <c>IGlobalPointerButtonService</c>의 우버튼
    /// 조회 <b>폴링 자체</b>를 지웠다 — 조용한 no-op으로 남겨두면 그 비용은 그대로 남는다.
    /// 좌클릭 드래그&던지기(12절)는 그대로 살아 있다.
    ///
    /// 18행 메뉴의 각 항목이 어디로 갔는지는 36-1의 전수 분류표에 있다:
    /// <b>(가) 사용자 행동 명령 7개</b> → <see cref="ActionCommandPopover"/>(부채꼴 ④) /
    /// <b>(나) 설정 3개</b> → 정보창·설정창 / <b>(다) 개발 전용 5개 + 가출 발동</b> →
    /// <see cref="StickMateDevTools"/> 게이트 뒤 단축키만 / <b>(라) 종료</b> → 아래.
    ///
    /// ============================================================================
    /// 종료 경로는 <b>2중</b>이다 (36-10) — 단축키 하나로는 부족하다
    /// ============================================================================
    /// 이 앱에는 Dock 아이콘도, 메뉴바 아이콘도, 트레이도 없다. 단축키는 <b>발견 불가능</b>하고
    /// (앱 UI 어디에도 적혀 있지 않다), <c>_keyService</c>가 null인 환경에서는 <b>아예 동작하지
    /// 않는다</b>. 우클릭까지 없앤 뒤 그 환경에 남는 종료 수단이 0이 되면 그건 강제 종료(활성 상태
    /// 보기/작업 관리자) 외에는 끌 수 없는 상주 오버레이이며, 원칙 2·4의 명백한 위반이다.
    ///   ① <b>⌃⌥⌘Q</b> — 여기. <b>개발 게이트 대상이 아니다</b>(릴리스에서 반드시 산다).
    ///   ② <b>행동 명령창 푸터 [✕ 앱 종료]</b>(2단 확인 3초) — 마우스만으로 도달하는 유일한 경로.
    /// 저장은 <c>CharacterProgressionDirector.OnApplicationQuit()</c>이 담당하므로 어느 쪽으로 끄든
    /// 데이터 손실이 없다.
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
        private bool _prevQ, _prevC, _prevD, _prevR, _prevB, _prevK, _prevG;
        private bool _prevT, _prevX, _prevH;
        private bool _prevS, _prevN, _prevJ, _prevF;
        private bool _prevA;
        private bool _prevI;
        private bool _prevComma;

        // 지연 탐색 후 캐시.
        private BattleMinigameDirector _battleDirector;
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
            BattleMinigame,
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
            Settings,           // ★ 2026-09-01 신설 — 설정창(⌃⌥⌘,)
            CornerPanel,        // (다) 임시 — 거처는 설정창 [일반](36-11의 부채)
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
            LogStartupBanner();
        }

        /// <summary>
        /// 시작 배너는 <b>두 벌로 갈라진다</b>(36-2 규칙 3) — 릴리스 로그가 <b>존재하지 않는 기능을
        /// 광고하지 않게</b> 한다. 릴리스 빌드에서 "S(스트레스 게이지 순환)"이 로그에 적혀 있는데 눌러도
        /// 아무 일이 없으면, 그건 사용자에게도 팀에게도 거짓말이다.
        /// </summary>
        private void LogStartupBanner()
        {
            string quitLine = "[앱제어] 준비 완료 — 종료 방법 2가지: " +
                "(1) 전역 단축키 **Control+Option+Command+Q**, " +
                "(2) **기어 아이콘 → 부채꼴 ④[행동] → 창 푸터 [✕ 앱 종료]**(2단 확인 3초). " +
                "★ 캐릭터 우클릭 메뉴는 2026-08-31에 폐지됐습니다 — 우클릭은 이제 밑에 있는 앱으로 " +
                "그대로 관통합니다(비침해 개선, UX_FLOW 36-9). ";

            string userKeys = "사용자 단축키: Ctrl+Opt+Cmd+C(잉크색 전환) / R(로데오 커서 on-off) / " +
                "**B(말 걸기)** / **K(격파 놀이)** / **G(그라피티)** / **T(창 도둑)** / " +
                "**X(창 부수기)** / **A(활쏘기)** / **N(가출 중이면 돌아오라고 부르기)** / " +
                "**I(캐릭터 정보/장비 창)** / **,(설정창)**. 이 명령들의 주 경로는 부채꼴 ④ [행동] 창이고, \n" +
                "설정창의 주 경로는 캐릭터 정보창 헤더의 [설정]입니다. ";

            string devKeys = StickMateDevTools.Enabled
                ? "★ 개발 전용 단축키(게이트 열림 — " + StickMateDevTools.SourceLabel + "): " +
                  "D(진단 로그 on-off) / H(하드웨어 반응 미리보기) / S(스트레스 게이지 순환) / " +
                  "J(할일 알림 강제) / F(집중 모드 90초 데모) / N(가출 강제 발동) / " +
                  "구석 크기 패널 토글. 이 6개는 사용자 UI에 노출되지 않습니다(원칙 1 — 표시된 것과 " +
                  "실제가 달라지는 경로라서). "
                : $"개발 전용 단축키는 잠겨 있습니다({StickMateDevTools.SourceLabel}) — " +
                  $"환경변수 {StickMateDevTools.EnvironmentVariableName}=1 로 실행하면 열립니다. ";

            // ★ 전역 키 조회가 미지원인 환경은 종료 경로가 ②(행동 명령창 푸터) 하나뿐이 된다 —
            //   그 사실을 로그가 분명히 말해야 팀이 그 환경을 재현했을 때 원인을 즉시 안다(36-10).
            string keyLine = _keyService != null
                ? "전역 키 조회=사용 가능."
                : "전역 키 조회=미지원 — 단축키 전체가 동작하지 않습니다. 이 환경에서 앱을 끄는 유일한 " +
                  "경로는 부채꼴 ④[행동] 창의 [✕ 앱 종료]입니다.";

            Debug.Log(quitLine + userKeys + devKeys + keyLine);
        }

        private void Update()
        {
            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer < PollInterval) return;
            _pollTimer = 0f;

            TickHotkeys();
        }

        // ==================== 전역 단축키 ====================

        private void TickHotkeys()
        {
            if (_keyService == null) return;

            // 조합키 3개가 모두 눌려 있을 때만 동작키를 본다 — 이 순서가 곧 비침해 보장이다
            // (Platform/IGlobalKeyStateService.cs "비침해 원칙 유지" 절 참고).
            bool chord = IsKeyDown(GlobalKey.Control) && IsKeyDown(GlobalKey.Option) && IsKeyDown(GlobalKey.Command);

            // ★ 36-2 규칙 3 — 게이트가 닫혀 있으면 (다) 키는 <b>읽지도 않는다</b>. 조용한 no-op이
            //   아니라 아예 조회하지 않는 것이 요구사항이다: 남겨두면 20Hz × 6키의 네이티브 조회가
            //   릴리스 빌드에서 영원히 도는데, 그 비용을 낼 이유가 없다(하루 종일 켜져 있는 앱이다).
            bool dev = StickMateDevTools.Enabled;

            bool q = chord && IsKeyDown(GlobalKey.Q);
            bool c = chord && IsKeyDown(GlobalKey.C);
            bool r = chord && IsKeyDown(GlobalKey.R);
            bool b = chord && IsKeyDown(GlobalKey.B);
            bool k = chord && IsKeyDown(GlobalKey.K);
            bool g = chord && IsKeyDown(GlobalKey.G);
            bool t = chord && IsKeyDown(GlobalKey.T);
            bool x = chord && IsKeyDown(GlobalKey.X);
            bool aKey = chord && IsKeyDown(GlobalKey.A);
            bool iKey = chord && IsKeyDown(GlobalKey.I);
            bool comma = chord && IsKeyDown(GlobalKey.Comma);

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
                _prevQ = q; _prevC = c; _prevD = d; _prevR = r; _prevB = b; _prevK = k; _prevG = g;
                _prevT = t; _prevX = x; _prevH = h;
                _prevS = sKey; _prevN = n; _prevJ = j; _prevF = f;
                _prevA = aKey;
                _prevI = iKey;
                _prevComma = comma;
                return;
            }

            bool qRise = q && !_prevQ;
            bool cRise = c && !_prevC;
            bool dRise = d && !_prevD;
            bool rRise = r && !_prevR;
            bool bRise = b && !_prevB;
            bool kRise = k && !_prevK;
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
            bool commaRise = comma && !_prevComma;
            _prevQ = q; _prevC = c; _prevD = d; _prevR = r; _prevB = b; _prevK = k; _prevG = g;
            _prevT = t; _prevX = x; _prevH = h;
            _prevS = sKey; _prevN = n; _prevJ = j; _prevF = f;
            _prevA = aKey;
            _prevI = iKey;
            _prevComma = comma;

            if (qRise) Invoke(ControlAction.Quit, "전역 단축키 Ctrl+Opt+Cmd+Q");
            else if (cRise) Invoke(ControlAction.InkColor, "전역 단축키 Ctrl+Opt+Cmd+C");
            else if (rRise) Invoke(ControlAction.Rodeo, "전역 단축키 Ctrl+Opt+Cmd+R");
            else if (dRise) Invoke(ControlAction.Diagnostics, "전역 단축키 Ctrl+Opt+Cmd+D");
            else if (bRise) Invoke(ControlAction.SayNow, "전역 단축키 Ctrl+Opt+Cmd+B");
            else if (kRise) Invoke(ControlAction.BattleMinigame, "전역 단축키 Ctrl+Opt+Cmd+K");
            else if (gRise) Invoke(ControlAction.Graffiti, "전역 단축키 Ctrl+Opt+Cmd+G");
            else if (tRise) Invoke(ControlAction.WindowTheft, "전역 단축키 Ctrl+Opt+Cmd+T");
            else if (xRise) Invoke(ControlAction.WindowCrash, "전역 단축키 Ctrl+Opt+Cmd+X");
            else if (hRise) Invoke(ControlAction.HardwareReaction, "전역 단축키 Ctrl+Opt+Cmd+H");
            else if (sRise) Invoke(ControlAction.StressGauge, "전역 단축키 Ctrl+Opt+Cmd+S");
            else if (nRise) Invoke(ControlAction.Runaway, "전역 단축키 Ctrl+Opt+Cmd+N");
            else if (jRise) Invoke(ControlAction.TodoReminder, "전역 단축키 Ctrl+Opt+Cmd+J");
            else if (fRise) Invoke(ControlAction.FocusWatch, "전역 단축키 Ctrl+Opt+Cmd+F");
            else if (aRise) Invoke(ControlAction.Archery, "전역 단축키 Ctrl+Opt+Cmd+A");
            else if (iRise) Invoke(ControlAction.CharacterInfo, "전역 단축키 Ctrl+Opt+Cmd+I");
            else if (commaRise) Invoke(ControlAction.Settings, "전역 단축키 Ctrl+Opt+Cmd+,");
        }

        private bool IsKeyDown(GlobalKey key)
            => _keyService != null && _keyService.TryGetKeyPressed(key, out bool pressed) && pressed;

        // ==================== 동작 ====================

        private void Invoke(ControlAction action, string source)
        {
            switch (action)
            {
                case ControlAction.Quit:
                    // ★ 개발 게이트 대상이 아니다(36-10) — 릴리스에서 반드시 살아야 한다.
                    Debug.Log($"[앱제어] 종료 요청({source}) — Application.Quit()을 호출합니다. " +
                        "저장은 CharacterProgressionDirector.OnApplicationQuit()이 담당하므로 데이터 손실이 없습니다. " +
                        "안녕히 계세요!");
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
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

                case ControlAction.CornerPanel:
                    UiLayoutModel.SetCornerPanelEnabled(!UiLayoutModel.CornerPanelEnabled);
                    Debug.Log($"[앱제어] 구석 호버 패널 {(UiLayoutModel.CornerPanelEnabled ? "켬" : "끔")}({source}) — " +
                        "끄면 화면 좌하단 감지 영역이 통째로 비활성화됩니다(저장됨). " +
                        "★ 이 토글은 갈 곳(설정창 [일반])이 아직 없어 개발 게이트 뒤에 임시로 있습니다(36-11 부채).");
                    break;

                case ControlAction.Diagnostics:
                    if (_config == null) break;
                    _config.verboseDiagnosticsLogging = !_config.verboseDiagnosticsLogging;
                    Debug.Log($"[앱제어] 진단 로그 {(_config.verboseDiagnosticsLogging ? "켬(촘촘)" : "끔(60초 심장박동만)")}({source}).");
                    break;

                case ControlAction.SayNow:
                    ForceSayNow(source);
                    break;

                case ControlAction.BattleMinigame:
                    ForceBattleMinigame(source);
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
        /// 설정창 토글(전역 단축키 ⌃⌥⌘,). 주 진입점은 <b>정보창 헤더의 [설정]</b>이고 이쪽은 보조
        /// 경로다(docs/UX_FLOW.md 36-11). 배타 모달 정리는 <see cref="SettingsWindow.Open"/> 한 곳이
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

        private void ForceBattleMinigame(string source)
        {
            if (_battleDirector == null) _battleDirector = Object.FindFirstObjectByType<BattleMinigameDirector>();
            if (_battleDirector == null)
            {
                Debug.LogWarning($"[앱제어] 격파 놀이({source}) — 씬에 BattleMinigameDirector가 없어 건너뜁니다.");
                return;
            }
            _battleDirector.ForceTriggerNow($"앱제어 {source}");
        }

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
    }
}
