using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 앱 제어 수단(2026-08-28 리더 지시: "지금 터미널 없이는 끌 수도 없다").
    ///
    /// ============================================================================
    /// 문제
    /// ============================================================================
    /// 이 앱의 창은 클릭 관통 상태라 클릭으로 포커스를 줄 수 없고, Unity의 Input은 창이 키보드 포커스를
    /// 가진 동안만 동작한다(Core/StickmanAgent의 Escape 긴급 해제가 가진 바로 그 한계 — 사용자가 다른
    /// 앱을 한 번 클릭하는 순간 무력화된다). 그래서 지금까지 앱을 끄는 유일한 방법이 터미널
    /// <c>kill PID</c>였다. 실사용 앱으로는 치명적이다.
    ///
    /// ============================================================================
    /// 채택한 수단과 그 이유 (리더가 제시한 3안 중)
    /// ============================================================================
    /// 1안(macOS 메뉴바 NSStatusItem)은 **채택하지 않았다**. NSStatusItem은 AppKit Objective-C API라
    ///    네이티브 플러그인이 반드시 필요한데, 이 프로젝트는 직전 라운드들에서 자체 Objective-C
    ///    플러그인이 반복적으로 실패해 전부 제거하고 검증된 오픈소스(UniWindowController)로 교체한
    ///    이력이 있다(Platform/MacOS/MacWindowService.cs 클래스 문서). 그걸 되돌리는 비용/위험이
    ///    이 라운드 예산을 확실히 넘어선다.
    ///
    /// 2안(**전역 단축키**) — 채택. 핵심 미지수였던 "접근성 권한 없이 키 상태를 읽을 수 있는가"를
    ///    먼저 실측으로 확인했다: <c>CGEventSourceKeyState</c>는 우리가 이미 마우스에 쓰고 있는
    ///    <c>CGEventSourceButtonState</c>와 같은 계열의 조회 전용 API로, 권한 없이 동작한다
    ///    (Platform/IGlobalKeyStateService.cs의 "권한에 대하여" 절에 실측 절차 기록). 창 포커스와
    ///    무관하므로 클릭 관통 상태에서도 항상 살아 있다.
    ///
    /// 3안(**캐릭터 자체를 조작 UI로**) — 함께 채택(2안의 이중화). 2안 하나에만 의존하면, 만약 향후
    ///    macOS가 이 API에 TCC 권한을 요구하도록 바꾸는 순간 사용자가 다시 앱을 끌 수 없게 된다.
    ///    "터미널 없이 종료할 수 있어야 한다"는 최소 요구사항을 단일 실패점 위에 올려두지 않기 위해,
    ///    이미 검증된 경로(캐릭터 위 클릭 감지 — Interaction/StickmanClickHitbox.cs가 실제 드래그로
    ///    동작 확인됨)를 그대로 재사용하는 **우클릭 메뉴**를 함께 둔다.
    ///
    /// ============================================================================
    /// 사용법 (사용자에게 안내해야 하는 내용)
    /// ============================================================================
    ///   • 전역 단축키: <b>Control + Option + Command + Q</b> = 종료
    ///                  Control + Option + Command + C = 잉크색(검정/흰색) 전환
    ///                  Control + Option + Command + R = 로데오 커서 켜기/끄기
    ///                  Control + Option + Command + D = 진단 로그 켜기/끄기
    ///                  <b>Control + Option + Command + B</b> = 지금 즉시 말풍선 띄우기(데모)
    ///                  <b>Control + Option + Command + K</b> = 격파 미니게임 강제 발동(데모, "breaK")
    ///                  <b>Control + Option + Command + G</b> = 그라피티 강제 발동(데모, "Graffiti")
    ///                  <b>Control + Option + Command + T</b> = 창 도둑 강제 발동(데모, "Theft")
    ///                  <b>Control + Option + Command + X</b> = 윈도우 크래시 강제 발동(데모, 부서짐)
    ///                  <b>Control + Option + Command + H</b> = 하드웨어 반응 데모 미리보기(4종 순환, "Hardware")
    ///                  <b>Control + Option + Command + S</b> = 스트레스 게이지 단계 순환(미리보기, "Stress")
    ///                  <b>Control + Option + Command + N</b> = 가출 발동 / 가출 중이면 돌아오라고 부르기("Nope")
    ///                  <b>Control + Option + Command + J</b> = 할일 추가(데모) + 들고 다니는 모드 알림("Job")
    ///                  <b>Control + Option + Command + F</b> = 집중 모드 켜기/끄기("Focus")
    ///                  <b>Control + Option + Command + A</b> = 활쏘기 발동("Archery")
    ///                  <b>Control + Option + Command + I</b> = 캐릭터 정보/장비 창 열기·닫기("Info").
    ///                    주 진입점은 화면 우상단 톱니 아이콘(Interaction/InfoGearIconWidget.cs)이고
    ///                    이 단축키와 우클릭 메뉴 [캐릭터 정보]는 보조 경로다.
    ///     3개 조합키를 모두 쓰는 이유: Cmd+Shift+Q는 macOS의 "로그아웃"이고 Cmd+Q는 활성 앱 종료라
    ///     둘 다 이미 의미가 있다. Ctrl+Option+Cmd 조합은 시스템/일반 앱이 거의 쓰지 않아, 사용자가
    ///     다른 앱에서 작업하다 실수로 데스크톱 펫을 종료시킬 위험이 사실상 없다.
    ///   • 캐릭터 <b>우클릭</b> -> 캐릭터 옆에 작은 메뉴가 뜬다. 그 메뉴의 [앱 종료]를 클릭하면 종료.
    ///     좌클릭은 이미 드래그&던지기(12절)가 쓰고 있으므로 우클릭을 쓴다(충돌 없음).
    ///
    /// ============================================================================
    /// 기존 안전장치와의 관계 (절대 깨뜨리지 않는다)
    /// ============================================================================
    /// 이 컴포넌트는 <c>SetClickThrough</c>를 **한 번도 호출하지 않는다**. 시작 5초 클릭관통 지연과
    /// Escape 긴급 해제(Core/StickmanAgent)는 그대로 살아 있고, 이 클래스는 그 위에 종료/설정 수단만
    /// 얹는다. 반대 방향도 마찬가지다 — 메뉴가 열려 있든 말든 클릭관통 상태는 변하지 않으므로,
    /// 메뉴를 띄운 채로도 비침해 원칙(CLAUDE.md 2)이 유지된다.
    /// </summary>
    public sealed class AppControlDirector : MonoBehaviour
    {
        // 전역 단축키 조합 — 클래스 문서 "사용법" 참고.
        private const float PollInterval = 0.05f;          // 20Hz. 단축키 감지에 충분하고 비용은 무시 가능.
        private const float MenuAutoCloseSeconds = 12f;    // 열어두고 잊어버려도 알아서 사라진다.
        private const float MenuPanelWidth = 188f;         // Unity 스크린 픽셀(= 화면 포인트 / dpi 배율).
        private const float MenuRowHeight = 26f;
        private const float MenuHeaderHeight = 22f;
        private const float MenuPadding = 6f;
        private const float MenuOffsetFromCharacterX = 34f; // 캐릭터와 겹치지 않도록 옆으로 밀어 놓는 거리.

        private StickmanAgent _agent;
        private StickConfig _config;
        private IGlobalKeyStateService _keyService;
        private IGlobalPointerButtonService _buttonService;

        private float _pollTimer;

        // 전역 단축키 엣지 판정 — 첫 폴링은 기록만 하고 넘어가, 앱 시작 순간 이미 눌려 있던 키를
        // 명령으로 오인하지 않는다(StickmanClickHitbox의 _globalPressedInitialized와 동일한 관례).
        private bool _hotkeyInitialized;
        private bool _prevQ, _prevC, _prevD, _prevR, _prevB, _prevK, _prevG;
        private bool _prevT, _prevX, _prevH;
        // Phase 5(2026-08-29): 스트레스(S) / 가출(N) / 할일(J) / 집중 모드(F).
        private bool _prevS, _prevN, _prevJ, _prevF;
        // 활쏘기(2026-08-29) — A.
        private bool _prevA;
        // 캐릭터 정보/장비 창(2026-08-29) — I.
        private bool _prevI;

        // 우클릭/메뉴 클릭 엣지 판정.
        private bool _rightPrev;
        private bool _rightInitialized;
        private bool _leftPrev;
        private bool _leftInitialized;

        private bool _menuOpen;
        private float _menuTimer;

        // 메뉴 UI(런타임 생성 — 씬/프리팹 수동 배선 없이도 동작. TodoPostItWidget과 동일한 관례).
        private Canvas _canvas;
        private CanvasScaler _scaler;   // Retina 대응 — 캔버스 1유닛 == OS 포인트 1로 맞춘다(ApplyCanvasScaleFactor).
        private RectTransform _panel;
        private Text[] _rowLabels;
        private RectTransform[] _rowRects;
        private BoxCollider2D _menuBlocker; // 메뉴 위 클릭이 밑의 다른 앱까지 새지 않게 막는 히트테스트용.
        private BattleMinigameDirector _battleDirector; // 격파 미니게임 강제 발동용(지연 탐색 후 캐시).
        private GraffitiDirector _graffitiDirector;     // 그라피티 강제 발동용(지연 탐색 후 캐시).
        private WindowTheftDirector _windowTheftDirector;   // 창 도둑 강제 발동용(지연 탐색 후 캐시).
        private WindowCrashDirector _windowCrashDirector;   // 윈도우 크래시 강제 발동용(지연 탐색 후 캐시).
        private HardwareReactionDirector _hardwareDirector;  // 하드웨어 반응 데모 미리보기용(지연 탐색 후 캐시).
        private StressGaugeDirector _stressDirector;        // 스트레스 게이지 단계 순환용(지연 탐색 후 캐시).
        private RunawayDirector _runawayDirector;           // 가출 발동/돌아오라고 부르기용(지연 탐색 후 캐시).
        private TodoReminderDirector _todoDirector;         // 할일 추가 + 리마인더 강제 발동용(지연 탐색 후 캐시).
        private FocusWatchDirector _focusDirector;          // 집중 모드 토글용(지연 탐색 후 캐시).
        private ArcheryDirector _archeryDirector;           // 활쏘기 발동용(지연 탐색 후 캐시).
        private CharacterInfoWindow _infoWindow;            // 캐릭터 정보/장비 창 토글용(지연 탐색 후 캐시).

        // 메뉴 행 정의 — 순서가 곧 화면 표시 순서이자 히트테스트 인덱스다.
        // 순서 = 화면 표시 순서 = 히트테스트 인덱스. 새 항목은 항상 [닫기] **앞에** 넣는다
        // (닫기가 마지막 줄이라는 관습을 지키기 위해 — 인덱스는 MenuRowCount가 자동으로 따라간다).
        private enum MenuAction
        {
            // ※ 5번은 [라이벌 소환]이었다 — 라이벌 기능 전체 삭제(2026-08-30)로 행을 지우고
            //   뒤 항목을 한 칸씩 당겼다. 값이 저장 파일이나 씬에 박히지 않으므로 재번호가 안전하다
            //   (히트테스트 인덱스로만 쓰이고 매 프레임 다시 만들어진다).
            Quit = 0, InkColor = 1, Rodeo = 2, Diagnostics = 3, SayNow = 4,
            BattleMinigame = 5, Graffiti = 6, WindowTheft = 7, WindowCrash = 8, HardwareReaction = 9,
            // Phase 5(2026-08-29 신설) — 항상 [닫기] **앞에** 넣는다(위 주석의 관습).
            StressGauge = 10, Runaway = 11, TodoReminder = 12, FocusWatch = 13,
            // 활쏘기(2026-08-29 사용자 요청) — 자율 발동 확률이 기본 0이라 이 행과 단축키 A가
            // **유일한 발동 경로**다(StickConfig.archeryChance 문서 참고). 항상 [닫기] 앞에 넣는다.
            Archery = 14,
            // 캐릭터 정보/장비 창(2026-08-29 성장/장비 라운드) — 주 진입점은 화면 우상단 톱니
            // 아이콘(Interaction/InfoGearIconWidget.cs)이고 이 행과 단축키 I는 보조 경로다.
            // 항상 [닫기] **앞에** 넣는다(위 주석의 관습).
            CharacterInfo = 15,
            // 구석 호버 패널 on/off(2026-08-31, docs/UX_FLOW.md 34-8 탈출구 ③) — 저장되는 **영구 설정**이다.
            // 설계는 기어 부채꼴에 두라고 했지만 그쪽은 원버튼 3개로 확정된 구조라(32절), 다른 영구
            // 토글들(잉크색/로데오/진단로그)이 이미 모여 있는 이 메뉴가 같은 성격의 자리다.
            // 항상 [닫기] **앞에** 넣는다(위 주석의 관습).
            CornerPanel = 16,
            Close = 17,
        }
        private const int MenuRowCount = 18;

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            if (_agent == null) _agent = Object.FindFirstObjectByType<StickmanAgent>();
        }

        private void Start()
        {
            _config = _agent != null ? _agent.Config : null;
            _keyService = _agent != null ? _agent.PlatformService as IGlobalKeyStateService : null;
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;

            // 시작 배너 — 사용자가 "어떻게 끄는지"를 로그에서도 확인할 수 있게 한다. 이 앱은 창을
            // 클릭할 수 없으므로, 조작법을 알려주는 통로 자체가 귀하다.
            Debug.Log("[앱제어] 준비 완료 — 종료 방법 2가지: " +
                "(1) 전역 단축키 **Control+Option+Command+Q**, " +
                "(2) **캐릭터 우클릭 -> [앱 종료] 클릭**. " +
                "그 밖의 단축키: Ctrl+Opt+Cmd+C(잉크색 전환) / R(로데오 커서 on-off) / D(진단 로그 on-off) / " +
                "**B(말풍선 즉시 띄우기)** / **K(격파 미니게임 강제 발동)** / " +
                "**G(그라피티 강제 발동)** / **T(창 도둑 강제 발동)** / **X(윈도우 크래시 강제 발동)** / " +
                "**H(하드웨어 반응 데모 미리보기 — 4종 순환)** / **S(스트레스 게이지 단계 순환)** / " +
                "**N(가출 발동, 가출 중이면 돌아오라고 부르기)** / **J(할일 추가 + 알림)** / " +
                "**F(집중 모드 켜기/끄기)** / **A(활쏘기 — 과녁을 세우고 3발)** / " +
                "**I(캐릭터 정보/장비 창 — 주 진입점은 화면 우상단 톱니 아이콘)**. " +
                $"전역 키 조회={(_keyService != null ? "사용 가능" : "미지원 — 우클릭 메뉴만 동작")}, " +
                $"전역 버튼 조회={(_buttonService != null ? "사용 가능" : "미지원 — 단축키만 동작")}.");
        }

        private void Update()
        {
            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer < PollInterval) return;
            _pollTimer = 0f;

            TickHotkeys();
            TickRightClickMenu();
        }

        // ==================== 2안: 전역 단축키 ====================

        private void TickHotkeys()
        {
            if (_keyService == null) return;

            // 조합키 3개가 모두 눌려 있을 때만 동작키를 본다 — 이 순서가 곧 비침해 보장이다
            // (Platform/IGlobalKeyStateService.cs "비침해 원칙 유지" 절 참고).
            bool chord = IsKeyDown(GlobalKey.Control) && IsKeyDown(GlobalKey.Option) && IsKeyDown(GlobalKey.Command);

            bool q = chord && IsKeyDown(GlobalKey.Q);
            bool c = chord && IsKeyDown(GlobalKey.C);
            bool d = chord && IsKeyDown(GlobalKey.D);
            bool r = chord && IsKeyDown(GlobalKey.R);
            bool b = chord && IsKeyDown(GlobalKey.B);
            bool k = chord && IsKeyDown(GlobalKey.K);
            bool g = chord && IsKeyDown(GlobalKey.G);
            bool t = chord && IsKeyDown(GlobalKey.T);
            bool x = chord && IsKeyDown(GlobalKey.X);
            bool h = chord && IsKeyDown(GlobalKey.H);
            bool sKey = chord && IsKeyDown(GlobalKey.S);
            bool n = chord && IsKeyDown(GlobalKey.N);
            bool j = chord && IsKeyDown(GlobalKey.J);
            bool f = chord && IsKeyDown(GlobalKey.F);
            bool aKey = chord && IsKeyDown(GlobalKey.A);
            bool iKey = chord && IsKeyDown(GlobalKey.I);

            if (!_hotkeyInitialized)
            {
                _hotkeyInitialized = true;
                _prevQ = q; _prevC = c; _prevD = d; _prevR = r; _prevB = b; _prevK = k; _prevG = g;
                _prevT = t; _prevX = x; _prevH = h;
                _prevS = sKey; _prevN = n; _prevJ = j; _prevF = f;
                _prevA = aKey;
                _prevI = iKey;
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
            _prevQ = q; _prevC = c; _prevD = d; _prevR = r; _prevB = b; _prevK = k; _prevG = g;
            _prevT = t; _prevX = x; _prevH = h;
            _prevS = sKey; _prevN = n; _prevJ = j; _prevF = f;
            _prevA = aKey;
            _prevI = iKey;

            if (qRise) Invoke(MenuAction.Quit, "전역 단축키 Ctrl+Opt+Cmd+Q");
            else if (cRise) Invoke(MenuAction.InkColor, "전역 단축키 Ctrl+Opt+Cmd+C");
            else if (rRise) Invoke(MenuAction.Rodeo, "전역 단축키 Ctrl+Opt+Cmd+R");
            else if (dRise) Invoke(MenuAction.Diagnostics, "전역 단축키 Ctrl+Opt+Cmd+D");
            else if (bRise) Invoke(MenuAction.SayNow, "전역 단축키 Ctrl+Opt+Cmd+B");
            else if (kRise) Invoke(MenuAction.BattleMinigame, "전역 단축키 Ctrl+Opt+Cmd+K");
            else if (gRise) Invoke(MenuAction.Graffiti, "전역 단축키 Ctrl+Opt+Cmd+G");
            else if (tRise) Invoke(MenuAction.WindowTheft, "전역 단축키 Ctrl+Opt+Cmd+T");
            else if (xRise) Invoke(MenuAction.WindowCrash, "전역 단축키 Ctrl+Opt+Cmd+X");
            else if (hRise) Invoke(MenuAction.HardwareReaction, "전역 단축키 Ctrl+Opt+Cmd+H");
            else if (sRise) Invoke(MenuAction.StressGauge, "전역 단축키 Ctrl+Opt+Cmd+S");
            else if (nRise) Invoke(MenuAction.Runaway, "전역 단축키 Ctrl+Opt+Cmd+N");
            else if (jRise) Invoke(MenuAction.TodoReminder, "전역 단축키 Ctrl+Opt+Cmd+J");
            else if (fRise) Invoke(MenuAction.FocusWatch, "전역 단축키 Ctrl+Opt+Cmd+F");
            else if (aRise) Invoke(MenuAction.Archery, "전역 단축키 Ctrl+Opt+Cmd+A");
            else if (iRise) Invoke(MenuAction.CharacterInfo, "전역 단축키 Ctrl+Opt+Cmd+I");
        }

        private bool IsKeyDown(GlobalKey key)
            => _keyService != null && _keyService.TryGetKeyPressed(key, out bool pressed) && pressed;

        // ==================== 3안: 캐릭터 우클릭 메뉴 ====================

        private void TickRightClickMenu()
        {
            if (_buttonService == null) return;

            // (a) 우클릭 상승 엣지 -> 커서가 캐릭터 위면 메뉴 토글.
            if (_buttonService.TryGetSecondaryButtonPressed(out bool right))
            {
                if (!_rightInitialized) { _rightInitialized = true; _rightPrev = right; }
                else
                {
                    bool rise = right && !_rightPrev;
                    _rightPrev = right;
                    if (rise && IsCursorOverCharacter())
                    {
                        if (_menuOpen) CloseMenu("캐릭터 우클릭(토글 닫기)");
                        else OpenMenu();
                    }
                }
            }

            if (!_menuOpen) return;

            // (b) 자동 닫힘 — 열어두고 잊어버려도 화면에 영구히 남지 않는다.
            _menuTimer += PollInterval;
            if (_menuTimer >= MenuAutoCloseSeconds)
            {
                CloseMenu($"{MenuAutoCloseSeconds:F0}초 무동작 자동 닫힘");
                return;
            }

            UpdateMenuPlacement();

            // (c) 좌클릭 상승 엣지 -> 메뉴 행 히트테스트.
            //     **창 포커스와 무관한 전역 폴링으로 직접 판정한다** — uGUI의 EventSystem 경로는 우리
            //     창이 마우스 이벤트를 실제로 수신해야 동작하는데, 클릭관통 오버레이에서는 그 보장이
            //     없기 때문이다(StickmanClickHitbox가 같은 이유로 이미 이 방식을 쓴다).
            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            if (!_leftInitialized) { _leftInitialized = true; _leftPrev = left; return; }
            bool leftRise = left && !_leftPrev;
            _leftPrev = left;
            if (!leftRise) return;

            if (!TryGetCursorUnityScreen(out Vector2 cursorScreen)) return;

            int hit = HitTestMenuRow(cursorScreen);
            if (hit < 0)
            {
                // 메뉴 밖 클릭 = 취소(데스크톱 앱의 관습적 동작).
                CloseMenu("메뉴 밖 클릭");
                return;
            }
            _menuTimer = 0f; // 조작이 있었으니 자동 닫힘 타이머를 다시 채운다.
            Invoke((MenuAction)hit, "우클릭 메뉴");
        }

        private bool IsCursorOverCharacter()
        {
            if (_agent == null || _agent.Blackboard == null) return false;
            if (!_agent.Blackboard.TryGetCursorWorldPosition(out Vector2 cursorWorld)) return false;

            // 판정 영역을 StickmanClickHitbox와 정확히 같게 맞춘다(캐릭터의 모든 Collider2D).
            Collider2D[] colliders = _agent.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null || !col.enabled) continue;
                if (col.OverlapPoint(cursorWorld)) return true;
            }
            return false;
        }

        private bool TryGetCursorUnityScreen(out Vector2 unityScreen)
        {
            unityScreen = default;
            if (_agent == null || !_agent.TryGetCursorPosition(out Vector2 osScreen)) return false;
            unityScreen = ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _config);
            return true;
        }

        /// <summary>커서(Unity 스크린 좌표)가 어느 메뉴 행 위인지. 메뉴 밖이면 -1.</summary>
        private int HitTestMenuRow(Vector2 cursorScreen)
        {
            if (_panel == null) return -1;
            for (int i = 0; i < MenuRowCount; i++)
            {
                RectTransform rt = _rowRects[i];
                if (rt == null) continue;
                // ScreenSpaceOverlay 캔버스에서는 RectTransform의 월드 좌표가 곧 스크린 픽셀 좌표다.
                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                if (cursorScreen.x >= corners[0].x && cursorScreen.x <= corners[2].x &&
                    cursorScreen.y >= corners[0].y && cursorScreen.y <= corners[2].y)
                {
                    return i;
                }
            }
            return -1;
        }

        // ==================== 동작 ====================

        private void Invoke(MenuAction action, string source)
        {
            switch (action)
            {
                case MenuAction.Quit:
                    Debug.Log($"[앱제어] 종료 요청({source}) — Application.Quit()을 호출합니다. " +
                        "안녕히 계세요!");
                    CloseMenu("종료");
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                    break;

                case MenuAction.InkColor:
                    if (_config == null) break;
                    _config.inkColor = _config.inkColor == StickmanInkColor.White
                        ? StickmanInkColor.Black
                        : StickmanInkColor.White;
                    _agent?.ApplyInkColorFromConfig();
                    Debug.Log($"[앱제어] 잉크색 전환({source}) -> {_config.inkColor}.");
                    RefreshMenuLabels();
                    break;

                case MenuAction.Rodeo:
                    if (_config == null) break;
                    _config.rodeoCursorEnabled = !_config.rodeoCursorEnabled;
                    Debug.Log($"[앱제어] 로데오 커서 {(_config.rodeoCursorEnabled ? "켬" : "끔")}({source}).");
                    RefreshMenuLabels();
                    break;

                case MenuAction.CornerPanel:
                    UiLayoutModel.SetCornerPanelEnabled(!UiLayoutModel.CornerPanelEnabled);
                    Debug.Log($"[앱제어] 구석 호버 패널 {(UiLayoutModel.CornerPanelEnabled ? "켬" : "끔")}({source}) — " +
                        "끄면 화면 좌하단 감지 영역이 통째로 비활성화됩니다(저장됨).");
                    RefreshMenuLabels();
                    break;

                case MenuAction.Diagnostics:
                    if (_config == null) break;
                    _config.verboseDiagnosticsLogging = !_config.verboseDiagnosticsLogging;
                    Debug.Log($"[앱제어] 진단 로그 {(_config.verboseDiagnosticsLogging ? "켬(촘촘)" : "끔(60초 심장박동만)")}({source}).");
                    RefreshMenuLabels();
                    break;

                case MenuAction.SayNow:
                    ForceSayNow(source);
                    break;

                case MenuAction.BattleMinigame:
                    ForceBattleMinigame(source);
                    break;

                case MenuAction.Graffiti:
                    ForceGraffiti(source);
                    break;

                case MenuAction.WindowTheft:
                    ForceWindowTheft(source);
                    break;

                case MenuAction.WindowCrash:
                    ForceWindowCrash(source);
                    break;

                case MenuAction.HardwareReaction:
                    ForceHardwareReaction(source);
                    break;

                case MenuAction.StressGauge:
                    ForceStressGauge(source);
                    RefreshMenuLabels(); // 트레이 점 자리를 대신하는 라벨(19절 "필요시(트레이)" 채널)을 즉시 갱신.
                    break;

                case MenuAction.Runaway:
                    ForceRunaway(source);
                    RefreshMenuLabels(); // 24절: 가출 중에는 같은 행이 "종료"가 아니라 "소환"으로 보여야 한다.
                    break;

                case MenuAction.TodoReminder:
                    ForceTodoReminder(source);
                    RefreshMenuLabels();
                    break;

                case MenuAction.FocusWatch:
                    ForceFocusWatch(source);
                    RefreshMenuLabels();
                    break;

                case MenuAction.Archery:
                    ForceArchery(source);
                    break;

                case MenuAction.CharacterInfo:
                    ToggleCharacterInfo(source);
                    break;

                case MenuAction.Close:
                    CloseMenu("메뉴 [닫기]");
                    break;
            }
        }

        /// <summary>
        /// 활쏘기 발동(전역 단축키 Ctrl+Opt+Cmd+A / 우클릭 메뉴 [활쏘기]). 다른 데모 항목과 성격이
        /// 다르다 — <b>확률을 건너뛰는 지름길이 아니라 정식이자 유일한 진입점</b>이다
        /// (StickConfig.archeryChance 기본값 0: 사용자가 요청하지 않은 연출이 뜨는 것에 반복적으로
        /// 불만을 표했기 때문. 집중 모드(F)와 같은 성격이다).
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
        /// 캐릭터 정보/장비 창 토글(전역 단축키 Ctrl+Opt+Cmd+I / 우클릭 메뉴 [캐릭터 정보]).
        /// 활쏘기(A)/집중 모드(F)와 같은 성격의 <b>정식 진입점</b>이다 — 확률을 건너뛰는 데모가 아니다.
        /// 주 진입점은 화면 우상단 톱니 아이콘(Interaction/InfoGearIconWidget.cs)이고 이쪽은 보조 경로다.
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
            CloseMenu("캐릭터 정보창 열기"); // 이 메뉴는 이 클래스 소유라 여기서 닫는다.
        }

        // ==================== 데모 진입점 (말풍선) ====================

        /// <summary>
        /// "지금 즉시 말풍선을 보여달라"(Ctrl+Opt+Cmd+B). 화면을 볼 수 없는 개발 환경에서도, 그리고
        /// 사용자가 확률(StickConfig.idleChatterChance)을 기다리지 않고도 원칙 1의 산출물을 눈으로
        /// 확인할 수 있게 하는 통로다.
        ///
        /// **원칙 1을 우회하지 않는다**: 대사 문자열을 직접 만들어 이벤트로 쏘는 게 아니라,
        /// 블랙보드에 강제 발화 펄스를 세운 뒤 <b>실제 상태 전이</b>(지금 상태로의 재진입)를 일으킨다.
        /// 대사는 여전히 그 전이가 확정된 뒤 Idle/WalkState.Enter() 안에서만 파생된다 —
        /// "혼잣말을 한다"는 행동 자체가 이 전이로 확정된 사실이 된다.
        ///
        /// Idle/Walk가 아닐 때(낙하/랙돌/스펙터클 진행 중)는 아무것도 하지 않는다. 진행 중인 행동을
        /// 대사를 보여주자고 중단시키는 것이야말로 5절이 막으려는 "텍스트가 행동을 끌고 가는" 구조다.
        /// </summary>
        private void ForceSayNow(string source)
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Machine == null)
            {
                Debug.LogWarning($"[앱제어] 말풍선 요청({source}) — 상태머신을 찾지 못해 건너뜁니다.");
                return;
            }

            StickmanStateId current = blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
            {
                Debug.Log($"[앱제어] 말풍선 요청({source}) — 지금은 {current} 중이라 건너뜁니다(진행 중인 행동을 " +
                          "대사 때문에 중단시키지 않는다 — UX_FLOW.md 5절).");
                return;
            }

            blackboard.ForcedChatterSignaled = true;      // 확률/쿨다운을 건너뛰는 1프레임 펄스.
            blackboard.Machine.ChangeState(current);      // 같은 상태로 재진입 = Enter()가 다시 확정 실행된다.
            Debug.Log($"[앱제어] 말풍선 강제 발화({source}) — {current} 재진입으로 대사를 파생시켰습니다.");
        }

        /// <summary>
        /// 격파 미니게임 강제 발동(Ctrl+Opt+Cmd+K). 기본 트리거가 "유휴 60초 주기마다 5% 추첨"이라
        /// 확률만으로는 실물 검증이 사실상 불가능해, 확률만 건너뛰는 데모 경로를 둔다 —
        /// 확률만 건너뛰고 상호배제 락(Core.SpectacleEventLock)과 진입 상태 조건은 그대로 지킨다.
        /// </summary>
        private void ForceBattleMinigame(string source)
        {
            if (_battleDirector == null) _battleDirector = Object.FindFirstObjectByType<BattleMinigameDirector>();
            if (_battleDirector == null)
            {
                Debug.LogWarning($"[앱제어] 격파 미니게임({source}) — 씬에 BattleMinigameDirector가 없어 건너뜁니다.");
                return;
            }
            _battleDirector.ForceTriggerNow($"앱제어 {source}");
        }

        /// <summary>
        /// 그라피티 강제 발동(Ctrl+Opt+Cmd+G). 기본 트리거는 60초 주기 4% 추첨 + 10분 쿨다운이라
        /// K와 완전히 같은 이유로 강제 경로가 필요하다. 쿨다운/확률만 건너뛸 뿐, "발판(창)과 겹치지 않는
        /// 빈 영역을 못 찾으면 그리지 않는다"는 27-3의 침해 방지 규칙은 강제 경로에서도 그대로 지킨다.
        /// </summary>
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

        /// <summary>
        /// 창 도둑 강제 발동(Ctrl+Opt+Cmd+T). 자동 발동은 60초 주기 3% 추첨 + 15분 쿨다운이라 K/G와
        /// 정확히 같은 이유로 데모 경로가 필요하다. 확률/쿨다운만 건너뛸 뿐, "캐릭터 신장의 3배 이하 폭을
        /// 가진 실제 창"이라는 27-1 대상 선정 조건과 상호배제 락은 그대로 지킨다.
        /// </summary>
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

        /// <summary>
        /// 윈도우 크래시 강제 발동(Ctrl+Opt+Cmd+X). 자동 발동이 60초 주기 2% 추첨 + 25분 쿨다운으로 이
        /// 프로젝트에서 가장 희소한 스펙터클이라(27-4: 파괴 연출은 더 드물어야 한다) 확률만으로는 실물
        /// 검증이 사실상 불가능하다. 크랙은 100% 클릭관통 시각 레이어이므로 강제로 띄워도 대상 창의
        /// 조작을 방해하지 않는다.
        /// </summary>
        private void ForceWindowCrash(string source)
        {
            if (_windowCrashDirector == null) _windowCrashDirector = Object.FindFirstObjectByType<WindowCrashDirector>();
            if (_windowCrashDirector == null)
            {
                Debug.LogWarning($"[앱제어] 윈도우 크래시({source}) — 씬에 WindowCrashDirector가 없어 건너뜁니다.");
                return;
            }
            _windowCrashDirector.ForceTriggerNow($"앱제어 {source}");
        }

        /// <summary>
        /// 하드웨어 반응 데모 미리보기(Ctrl+Opt+Cmd+H). 위 3개와 성격이 다르다 — 확률을 건너뛰는 게
        /// 아니라 <b>실제로는 일어나지 않은 신호의 연출만</b> 잠깐 보여주는 경로다(배터리를 20%로
        /// 만드는 것은 원칙 3/27-7이 금지하는 OS 제어이므로 애초에 불가능하다). 누를 때마다
        /// 배터리 -> CPU -> 네트워크 -> 충전 순으로 하나씩 순환하며, 스스로 짧게 걷힌다.
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

        // ==================== Phase 5 데모 진입점 (스트레스 / 가출 / 할일 / 집중 모드) ====================

        /// <summary>
        /// 스트레스 게이지 단계 순환(Ctrl+Opt+Cmd+S). 하드웨어 반응(H)과 같은 성격의 "미리보기" 경로다 —
        /// 확률을 건너뛰는 것이 아니라 <b>실사용에서는 수 시간~반나절이 걸려야 쌓이는 값</b>을 미리
        /// 세워 보는 것이다(19절: 반나절 방치 / 5분 내 8회 격파훈련 / 시간당 0.05 자연 감소).
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
        /// 가출 발동 / 돌아오라고 부르기 토글(Ctrl+Opt+Cmd+N). 24절이 "같은 버튼이 상황에 따라 다른
        /// 동작을 하면서 라벨이 안 바뀌면 혼란"이라고 못박았으므로, 이 행의 라벨은
        /// <see cref="RefreshMenuLabels"/>에서 현재 상태에 따라 다르게 표기한다.
        /// </summary>
        private void ForceRunaway(string source)
        {
            if (_runawayDirector == null) _runawayDirector = Object.FindFirstObjectByType<RunawayDirector>();
            if (_runawayDirector == null)
            {
                Debug.LogWarning($"[앱제어] 가출({source}) — 씬에 RunawayDirector가 없어 건너뜁니다.");
                return;
            }
            _runawayDirector.ForceTriggerNow($"앱제어 {source}");
        }

        /// <summary>
        /// 할일 추가(데모) + 들고 다니는 모드 알림 강제 발동(Ctrl+Opt+Cmd+J). 17절의 정식 진입점인
        /// "설정창/트레이 메뉴의 [+ 할일 추가]"가 아직 없어, <b>목록에 항목을 넣는 유일한 경로</b>이기도
        /// 하다(이 경로가 생기기 전에는 Core.TodoListModel.Add 호출자가 0건이라 투두 기능 전체가 도달
        /// 불가능이었다). 실제 캘린더/할일 앱은 읽지 않는다(원칙 3).
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
        /// 집중 모드 켜기/끄기(Ctrl+Opt+Cmd+F). 18절의 "[시작] 트레이 메뉴 '집중 모드'"와
        /// "[종료-중도취소] 트레이에서 '집중 모드 끄기'"를 하나의 토글로 제공한다 — 트레이가 없는 지금
        /// 아키텍처에서는 이 단축키/메뉴 행이 곧 그 트레이 메뉴다(데모가 아니라 정식 진입점).
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

        // ==================== 메뉴 UI ====================

        private void OpenMenu()
        {
            EnsureMenuBuilt();
            _menuOpen = true;
            _menuTimer = 0f;
            _leftInitialized = false; // 메뉴를 여는 그 클릭이 곧바로 행 클릭으로 오인되지 않게 엣지 재초기화.
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_menuBlocker != null) _menuBlocker.enabled = true;
            RefreshMenuLabels();
            UpdateMenuPlacement();
            Debug.Log("[앱제어] 캐릭터 우클릭 — 제어 메뉴를 열었습니다([앱 종료]/[잉크색]/[로데오]/[진단로그]/" +
                "[말풍선]/[격파 놀이]/[그라피티]/[창 도둑]/[창 부수기]/[하드웨어 반응]/" +
                "[스트레스]/[가출]/[할일 알림]/[집중 모드]/[활쏘기]/[캐릭터 정보]/[닫기]).");
        }

        private void CloseMenu(string reason)
        {
            if (!_menuOpen) return;
            _menuOpen = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_menuBlocker != null) _menuBlocker.enabled = false;
            Debug.Log($"[앱제어] 제어 메뉴를 닫았습니다 — {reason}.");
        }

        private void EnsureMenuBuilt()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("AppControlCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32760; // 다른 모든 UI(TodoPostItWidget 포함)보다 위 — 종료 수단이 가려지면 안 된다.
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();

            float panelHeight = MenuHeaderHeight + MenuRowHeight * MenuRowCount + MenuPadding * 2f;
            var panelGo = new GameObject("ControlPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0f, 0.5f);
            _panel.sizeDelta = new Vector2(MenuPanelWidth, panelHeight);
            // 불투명에 가까운 밝은 패널 — 어떤 바탕화면 위에서도 글자가 읽혀야 한다(종료 수단이므로
            // "안 보여서 못 끄는" 상황이 절대 없어야 한다).
            panelGo.GetComponent<Image>().color = new Color(0.97f, 0.97f, 0.97f, 0.97f);

            var header = CreateLabel(panelGo.transform, "Header", MenuHeaderHeight,
                new Vector2(0f, 1f), new Vector2(MenuPadding, -MenuPadding));
            header.text = "StickMate";
            header.fontStyle = FontStyle.Bold;
            header.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            header.fontSize = 12;

            _rowLabels = new Text[MenuRowCount];
            _rowRects = new RectTransform[MenuRowCount];
            for (int i = 0; i < MenuRowCount; i++)
            {
                var rowGo = new GameObject($"Row{i}", typeof(RectTransform), typeof(Image));
                rowGo.transform.SetParent(panelGo.transform, false);
                var rt = rowGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(MenuPanelWidth - MenuPadding * 2f, MenuRowHeight);
                rt.anchoredPosition = new Vector2(MenuPadding, -(MenuPadding + MenuHeaderHeight + MenuRowHeight * i));
                rowGo.GetComponent<Image>().color = i == (int)MenuAction.Quit
                    ? new Color(0.92f, 0.30f, 0.26f, 0.16f)  // 종료 행만 옅은 붉은 배경으로 구분.
                    : new Color(0f, 0f, 0f, 0.05f);
                _rowRects[i] = rt;

                var label = CreateLabel(rowGo.transform, "Label", MenuRowHeight, Vector2.zero, Vector2.zero);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(8f, 0f);
                label.rectTransform.offsetMax = Vector2.zero;
                _rowLabels[i] = label;
            }

            var hint = CreateLabel(panelGo.transform, "Hint", MenuHeaderHeight,
                new Vector2(0f, 0f), new Vector2(MenuPadding, 2f));
            hint.rectTransform.pivot = new Vector2(0f, 0f);
            hint.text = "단축키: ⌃⌥⌘Q 종료";
            hint.fontSize = 10;
            hint.color = new Color(0.45f, 0.45f, 0.45f, 1f);

            // 메뉴 영역 히트테스트 차단막 — UniWindowController의 Raycast 히트테스트가 이 콜라이더를
            // 발견하면 그 자리에서 클릭관통이 풀려, 메뉴를 클릭할 때 그 클릭이 **밑에 있는 다른 앱까지
            // 함께 새는** 것을 막는다. isTrigger=true라 캐릭터 물리(충돌/랙돌 인터럽트)에는 전혀
            // 관여하지 않는다(OnCollisionEnter2D는 트리거에서 발생하지 않는다). 메뉴가 닫혀 있는
            // 동안에는 enabled=false라 존재 자체가 없는 것과 같다.
            var blockerGo = new GameObject("AppControlMenuBlocker");
            _menuBlocker = blockerGo.AddComponent<BoxCollider2D>();
            _menuBlocker.isTrigger = true;
            _menuBlocker.enabled = false;

            canvasGo.SetActive(false);
        }

        private static Text CreateLabel(Transform parent, string name, float height, Vector2 anchor, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(MenuPanelWidth - MenuPadding * 2f, height);

            var text = go.GetComponent<Text>();
            // TodoPostItWidget과 동일 — 이 프로젝트에는 TextMeshPro가 없으므로 Unity 내장 legacy 폰트를 쓴다.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.black;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void RefreshMenuLabels()
        {
            if (_rowLabels == null) return;
            SetRowText(MenuAction.Quit, "✕  앱 종료");
            SetRowText(MenuAction.InkColor,
                $"잉크색: {(_config != null && _config.inkColor == StickmanInkColor.White ? "흰색" : "검정")}");
            SetRowText(MenuAction.Rodeo,
                $"로데오 커서: {(_config != null && _config.rodeoCursorEnabled ? "켬" : "끔")}");
            SetRowText(MenuAction.Diagnostics,
                $"진단 로그: {(_config != null && _config.verboseDiagnosticsLogging ? "켬" : "끔")}");
            SetRowText(MenuAction.SayNow, "말풍선 띄우기");
            SetRowText(MenuAction.BattleMinigame, "격파 놀이 시작");
            SetRowText(MenuAction.Graffiti, "그라피티 그리기");
            SetRowText(MenuAction.WindowTheft, "창 도둑 놀이");
            SetRowText(MenuAction.WindowCrash, "창 부수기(가짜)");
            SetRowText(MenuAction.HardwareReaction, "하드웨어 반응 미리보기");

            // 19절 "필요시(트레이)" 채널 — 트레이가 없는 이 앱에서 이 행이 트레이 색점을 대신한다.
            // **수치가 아니라 상태만** 보여준다(19절이 명시적으로 요구한 형태). 단계 경계 계산은
            // StressGaugeRenderer.TierForLevel 하나에서만 나온다(경계가 두 곳에서 계산되지 않게).
            StressMoodTier tier = StressGaugeRenderer.TierForLevel(StressGauge.CurrentLevel, _config);
            string dot = tier == StressMoodTier.Alarm ? "●" : tier == StressMoodTier.Caution ? "◐" : "○";
            SetRowText(MenuAction.StressGauge, $"스트레스: {dot} {StressGaugeRenderer.TierLabel(tier)}");

            // 24절 필수 요구: 가출 중에는 같은 버튼이 "종료"가 아니라 "소환"으로 동작하므로 라벨도 달라야 한다.
            bool isRunaway = _agent != null && _agent.Blackboard != null && _agent.Blackboard.Machine != null
                && _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Runaway;
            SetRowText(MenuAction.Runaway, isRunaway ? "돌아오라고 부르기" : "가출 시키기");

            SetRowText(MenuAction.TodoReminder, $"할일 알림 ({TodoListModel.UncompletedCount}건)");

            if (_focusDirector == null) _focusDirector = Object.FindFirstObjectByType<FocusWatchDirector>();
            bool focusOn = _focusDirector != null && _focusDirector.IsSessionActive;
            SetRowText(MenuAction.FocusWatch, focusOn
                ? $"집중 모드 끄기 ({_focusDirector.RemainingSeconds:F0}초 남음)"
                : "집중 모드 시작");

            SetRowText(MenuAction.Archery, "활쏘기");

            SetRowText(MenuAction.CornerPanel,
                $"구석 크기 패널: {(UiLayoutModel.CornerPanelEnabled ? "켬" : "끔")}");

            if (_infoWindow == null) _infoWindow = Object.FindFirstObjectByType<CharacterInfoWindow>();
            SetRowText(MenuAction.CharacterInfo,
                _infoWindow != null && _infoWindow.IsOpen ? "캐릭터 정보 닫기" : "캐릭터 정보");

            SetRowText(MenuAction.Close, "닫기");
        }

        private void SetRowText(MenuAction action, string text)
        {
            int i = (int)action;
            if (_rowLabels != null && i < _rowLabels.Length && _rowLabels[i] != null) _rowLabels[i].text = text;
        }

        /// <summary>
        /// ScreenSpaceOverlay 캔버스의 스케일을 현재 화면 배율에 맞춘다 — **캔버스 1유닛 == OS 포인트 1**.
        /// 근거는 ScreenCoordinateConverter.ResolveCanvasScaleFactor() 문서 참고(2026-08-29 Retina 대응,
        /// 리더 지시 5항). 이게 없으면 Retina에서 종료 메뉴가 물리적으로 절반 크기가 되어 읽기 어려워진다 —
        /// 이 메뉴는 사용자가 앱을 끄는 **유일한 수단**이라 특히 작아지면 안 된다.
        /// </summary>
        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
        }

        /// <summary>메뉴를 캐릭터 옆(화면 안)으로 옮긴다 — 캐릭터가 걸어가도 메뉴가 따라온다.</summary>
        private void UpdateMenuPlacement()
        {
            if (_panel == null || _agent == null || _agent.Blackboard == null) return;
            Camera cam = _agent.Blackboard.MainCamera;
            Rigidbody2D body = _agent.Blackboard.Body;
            if (cam == null || body == null) return;

            ApplyCanvasScaleFactor(); // 배율은 실행 중에 바뀔 수 있다(모니터 이동/시작 직후 창 확장).

            // ★ 단위 변환(2026-08-29 Retina 대응): WorldToScreenPoint/Screen.width는 **Unity 픽셀**,
            // anchoredPosition/sizeDelta는 **캔버스 유닛**이다. scaleFactor가 2인 Retina에서 이 변환을
            // 빼먹으면 메뉴가 캐릭터에서 화면 절반만큼 떨어진 곳에 그려진다.
            Vector3 charScreenPx = cam.WorldToScreenPoint(body.position);
            float charX = ScreenCoordinateConverter.UnityScreenToCanvas(charScreenPx.x, _config);
            float charY = ScreenCoordinateConverter.UnityScreenToCanvas(charScreenPx.y, _config);
            float screenW = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.width, _config);
            float screenH = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.height, _config);

            float panelW = _panel.sizeDelta.x;
            float panelH = _panel.sizeDelta.y;

            // 기본은 캐릭터 오른쪽. 오른쪽이 화면 밖이면 왼쪽으로 뒤집는다.
            float x = charX + MenuOffsetFromCharacterX;
            if (x + panelW > screenW - 4f) x = charX - MenuOffsetFromCharacterX - panelW;
            x = Mathf.Clamp(x, 4f, Mathf.Max(4f, screenW - panelW - 4f));

            // 세로는 캐릭터 몸통 중앙 언저리. 화면 위/아래로 넘치면 안쪽으로 끌어당긴다.
            float y = Mathf.Clamp(charY + panelH * 0.5f, panelH * 0.5f + 4f,
                Mathf.Max(panelH * 0.5f + 4f, screenH - panelH * 0.5f - 4f));

            _panel.anchoredPosition = new Vector2(x, y);

            // 히트테스트 차단막을 같은 화면 영역의 월드 사각형으로 맞춘다.
            // Camera.ScreenToWorldPoint는 **Unity 픽셀**을 받으므로 캔버스 유닛을 되돌려 넘긴다.
            if (_menuBlocker != null)
            {
                float pxLeft = ScreenCoordinateConverter.CanvasToUnityScreen(x, _config);
                float pxRight = ScreenCoordinateConverter.CanvasToUnityScreen(x + panelW, _config);
                float pxBottom = ScreenCoordinateConverter.CanvasToUnityScreen(y - panelH * 0.5f, _config);
                float pxTop = ScreenCoordinateConverter.CanvasToUnityScreen(y + panelH * 0.5f, _config);
                Vector3 bl = cam.ScreenToWorldPoint(new Vector3(pxLeft, pxBottom, charScreenPx.z));
                Vector3 tr = cam.ScreenToWorldPoint(new Vector3(pxRight, pxTop, charScreenPx.z));
                _menuBlocker.transform.position = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, 0f);
                _menuBlocker.size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
            }
        }
    }
}
