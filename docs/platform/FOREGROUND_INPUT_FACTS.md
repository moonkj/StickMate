# 전경 획득 뒤 키보드 입력의 행방 · 부채꼴 「바깥 클릭 접힘」의 구현 위치 — 코드 사실 조회

담당 `dev-platform` · 2026-09-15 · 기준 HEAD `ad49497` · **코드 읽기만 했다. 실기 0회, 빌드·Unity 실행 0회, 코드 변경 0줄.**
경로는 따로 적지 않으면 `Assets/_Project/Scripts/` 기준이다. **예외**: 씬은 `Assets/_Project/Scenes/Main.unity`, 입력 설정은 저장소 루트 `ProjectSettings/InputManager.asset`, 에디터 코드는 `Assets/Editor/`, 패키지 파일은 인용마다 패키지 이름을 붙인다(초판은 이 예외를 적지 않았다 — 끝 절 정정 기록 13). 코드 줄 번호는 HEAD 작업 트리 기준이다 — 판독 시점에 `Platform`·`Interaction`·`Core`·`Scenes`·`ProjectSettings`·`Packages`는
`git --no-optional-locks status --short` 빈 출력(같은 명령이 `docs`에서는 `M` 줄을 내 경로가 살아 있음을 확인).
작성 뒤 재확인에서 `Tests/PlayMode/AutoSurfaceLeaseAxisTests.cs`(1차) · `Interaction/WindowCrashDirector.cs` · `Tests/EditMode/UnsummonedSurfaceAxisTests.cs`(2차)가 `M`으로 나타났다 — 이 라운드는 `.cs`를 고치지 않았으므로 **다른 라운드의 변경**이고, 셋 다 이 문서가 인용한 파일이 아니다(이 문장을 쓰기 전 문서 안 이름 적중 0 — 지금 적중은 이 문장 1줄뿐).

입력: `E1_FOREGROUND_TIER_FACTS.md` A절(H1)에서 남긴 미확인 두 건. 이 문서는 그 문서를 수정하지 않는다(연결은 5절 목록).
**이 문서에서 코드·문서 인용이 없는 주장은 전부 「실기 미확인」이다. 이 개발 머신에는 Windows가 없다.**

판정 표기: **(가) 코드상 발생 / (나) 발생 안 함 / (다) 실기 필요.**

---

## 0. 측정 방법과 대조

| 측정 | 결과 | 양성 대조 | 음성 대조 |
|---|---|---|---|
| 키 가로채기 API 이름(비테스트 `.cs`): `SetWindowsHookEx`·`RegisterHotKey`·`WM_KEYDOWN`·`WM_CHAR`·`WM_HOTKEY`·`RegisterRawInputDevices`·`CGEventTap`(호출)·`addGlobalMonitor`·`NSEvent` | **호출 0**. 걸린 것은 전부 주석(「후킹이 필요 없다」 류)과 테스트의 금지 니들 | 같은 명령이 `GetAsyncKeyState`(`Win32WindowService.cs:313`)·`CGEventSourceKeyState`(`MacWindowService.cs:115`) 선언을 잡는다 | — |
| Unity 키 조회(`Input.GetKey*`·`anyKey`·`GetButton*`·`GetAxis*`, 비테스트) | 4곳(1-2 표) | 같은 명령이 테스트 파일의 니들 문자열도 잡는다 | — |
| uGUI 내비게이션 설정(`.navigation`·`Navigation.Mode`, 비테스트) | **0건** = 전부 기본값 | 같은 디렉터리·같은 도구로 `.transition = Selectable.Transition` **10건** | — |
| UniWinC Windows DLL 임포트 표(`strings -a` + 정확 일치) | `SetWindowsHookExW`·`RegisterHotKey`·`RegisterRawInputDevices`·`SetForegroundWindow`·`SetFocus`·`SetActiveWindow` **0** · `GetAsyncKeyState` **1** | `SetWindowLongPtrW` 1 | 없는 이름 0 |
| UniWinC macOS 번들 심볼(`nm -m`) | `_keepKeyWindowObserver` 2 · `setClickThrough` 2 · `setBorderless` 3 · `_makeKeyWindow` **0**(※ 한계 참고) | (좌동) | 없는 이름 0 |
| 부채꼴 접힘 호출자 — 세는 규칙: 주석이 아닌 코드 줄에서 `_menu.` 바로 뒤에 `Collapse(`·`ForceCloseAll(`·`UnionScreenRect`·`ContainsCursor(` 중 하나가 오는 줄 | `InfoGearIconWidget.cs` **6줄**(`:639`·`:1011`·`:1049`·`:1160`·`:1161`·`:1507`) · `AppControlDirector.cs` **1줄**(`:974`) | 위젯 파일 자신의 고정 문자열 `Collapse(` 줄 **10개**(2-1) | — |
| N-8 목록 등급 조회(`DISPLAY_CHANGE_PATH_FILES.md` 목록 블록, 등급 열 필터) | 46행(목록 41 + X 5) | `FramePacing.cs` → A · `FootholdPoller.cs` → X | 없는 경로 → 빈 값 |

**한계**: `_makeKeyWindow`는 Swift `private static`이라 심볼로 남지 않았을 수 있다(인라인·이름 변형). 0은 부재 증거가 아니다. 판정은 소스(1-4)로 했다.
**소스·이진 동일성 미확인**: 잠금 해시 `304f9ba2…`(upm 브랜치 「release 0.9.8」)에는 `Xcode/` 소스가 없어 받기에 실패했고, **태그 `v0.9.8`의** `Xcode/LibUniWinC/LibUniWinC.swift`를 읽었다. 이진에 `_keepKeyWindowObserver`가 있어 **어긋나지 않는다**는 정도만 말할 수 있다.

**자백 3건**
1. 첫 검색이 zsh 글롭에 `--include=*.cs`를 먹혀 **전 항목 0**이었다(`TEAM.md` 12번째 형태 같은 가족). 게다가 그때 고른 양성 대조 `Input.GetMouseButton`은 **살아 있는 프로브에서도 0**(이 저장소는 그 API를 안 쓴다)이라 대조 자체가 죽어 있었다. 따옴표로 다시 돌렸고, 프로브 생존은 다른 적중(`Input.GetKeyDown` 등)으로 보였다.
2. N-8 등급 조회 첫 판이 **168행**을 뽑았다 — 문서의 ②(가) 코드 블록 안에도 `DCP-LIST` 표지 문자열이 있어 추출 구간이 코드까지 이어졌고, 나는 등급 열 필터를 빠뜨렸다. 기대 46과 달라 폐기하고 필터를 넣어 46행 + 양성·음성 대조로 다시 쟀다(3절 표는 둘째 판).
3. macOS 번들 문자열에 `canBecomeKeyWindow`가 있고 `method_exchangeImplementations`를 임포트하는 것을 보고 「플러그인이 `canBecomeKeyWindow`를 바꿔치기한다」고 **가설을 세웠다 — 소스로 반증됐다.** 바꿔치기 대상은 `constrainFrameRect`다(`LibUniWinC.swift` v0.9.8 `:25-35`). 키 창 유지는 별도 관찰자가 한다(1-4).

**자백 2차 (verify-change 조건부 통과 뒤)**
4. 계수 두 곳이 틀렸다. 0절 「`InfoGearIconWidget.cs` 5곳」은 세는 규칙 없이 적은 숫자였다(규칙을 정해 다시 세면 6줄). 「위젯 안 `Collapse(` 15건」은 실제로는 **5갈래 정규식이 걸린 줄 수**를 `Collapse(` 호출 수처럼 적은 것이었다(고정 문자열 `Collapse(` 줄은 10개).
5. 굵게·기울임의 닫는 기호가 문장부호와 한글 사이에 끼어 **렌더링에서 닫히지 않는 줄 4곳**을 냈다(verify-change가 2곳, 이번 검사기가 첫 판에서 2곳을 더 찾았다 — 모두 닫는 기호가 문장부호·백틱과 한글 사이에 있었다). 이번 판은 CommonMark 좌우 flanking 규칙을 흉내 낸 검사기를 합성 사례로 교정한 뒤 파일 전체에 돌렸다.
6. STORE_PAGE 앵커가 걸린 줄이 속한 **§7 금지 목록 절**을 확인하지 않고 「약속 문구」로 적었다 — `TEAM.md`의 「1회 걸렸다 ≠ 제자리」를 어겼다.
7. 5절에서 주어가 다른 전칭 문장(키 행방 / 판·오버레이)을 한 판정으로 묶었고, 같은 경로의 전칭 문장 4곳을 빠뜨렸다(`TEAM.md` 「같은 경로 전수」).
8. 「우리 앱에도 닿지 않고 사라진다」와 「포커스가 숨은 호스트 창으로 감」은 엔진·OS 동작을 코드 사실처럼 단정한 문장이었다.
9. [지금 종료]의 더 나쁜 경우(클릭 자체가 1차 무장)를 놓쳤다. 무장 함수와 리스너를 읽고도 「그 클릭이 무장이자 선택」이라는 결합을 보지 못했다.

**자백 3차 (2차 재검증 뒤)**
10. 정정 기록 절에 옛 판정 4건(1-3 복귀 문장 축소 · 4절 사유 축소 · `WM_ACTIVATE` 무조건형 · 4절 R-3 가드)을 빠뜨렸다. 본문을 고치면서 기록을 같이 옮기지 않았다.
11. 1-3에서 W-1을 「요청의 OS 성공 여부 미확인」으로 좁혀 놓고, 5절 부류 K·O 근거에는 옛 단정(「키가 호스트 창으로 간다」)을 그대로 두었다. 같은 문서 안에서 같은 경로 전수를 어겼다.
12. 가드 경계를 「0.35초 초과」로 적었다. 코드는 `<`로 거부하므로 경계값은 통과한다(`:1087`).
13. 1차 편집에서 `:312` 뒤 띄어쓰기를 지웠다(편집 문자열 끝 공백 누락).
14. 한 번 길게 누르기 경로를 놓쳤다. 두 입력 경로가 같은 키로 `TryClaimAction`한다고 1-2에 직접 적어 두고도 **누름과 뗌이라는 발동 시점 차이**를 보지 않았다.
15. 드래그 처리기 검사의 첫 식이 `Slider` 뒤에만 낱말 경계를 둬 `SettingsSlider`를 uGUI `Slider`로 셌고(16·3줄), 양성 대조 수 「6줄」도 그 식에서 나온 값을 문서에 먼저 적었다. 양쪽 경계 식으로 다시 세어 대상 0 · 양성 4로 고쳤다.

---

## 1. (1) 키보드 입력의 행방

### 1-0. OS 규칙 — 발표 앱은 포커스가 있어야 키를 받는다

**Windows** (Microsoft Learn, Keyboard Input Overview, 「Keyboard Focus and Activation」 절):
- *"The system posts keyboard messages to the message queue of the foreground thread that created the window with the keyboard focus."*
- *"The window with the keyboard focus is either the active window, or a child window of the active window."*
- *"The user can activate a top-level window by clicking it"* · *"When the default window procedure receives the WM_ACTIVATE message, it sets the keyboard focus to the active window."*
- 「System and Nonsystem Keystrokes」 절: *"Nonsystem keystroke messages are for use by application windows; the DefWindowProc function does nothing with them."*

⇒ 발표 앱(PowerPoint 등)이 넘기기 키를 받으려면 **그 앱의 창이 전경이고 키보드 포커스를 가져야 한다.** 우리 창이나 우리 프로세스의 다른 창이 전경이면 키는 **그 창의 스레드 큐**로 가고 발표 앱에는 가지 않는다. Unity가 키를 「먹어서」가 아니다. **OS가 애초에 포커스 창으로만 보내기 때문이다.**
- 넘기기 키 (Microsoft Support, 「Use keyboard shortcuts to deliver PowerPoint presentations」): 다음 = *"N, Enter, Page down, Right arrow key, Down arrow key, Spacebar"* · 이전 = *"P, Page up, Left arrow key, Up arrow key, Backspace"* · 검은 화면 = *"B, Period (.)"* · 시작 = *"F5"* · 끝 = *"Esc"*.
- 발표 리모컨: 브리프 전제대로 **키보드 HID로 위 키를 보내는 제품**이면 똑같이 포커스 창으로 간다(같은 문서 「Scan Codes」 절: HID Usage → 스캔 코드 → 키 메시지). 제조사 전용 소프트웨어가 특정 앱에 직접 넣어 주는 제품은 **이 문서가 확인하지 않았다.**

**macOS** (Apple Developer Documentation):
- `NSApplication.keyWindow`: *"The window that currently receives keyboard events."* · *"The property might be nil … when the receiver is not active."*
- `NSApplication.ActivationPolicy.accessory`: *"… it may be activated programmatically or by clicking on one of its windows."*
- `NSWindow.canBecomeKey`: *"The value of this property is true if the window has a title bar or a resize bar, or false otherwise."*
- `NSWindow.makeKey()`: *"Makes the window the key window."* — 문서 요약은 **앱 활성화를 말하지 않는다.**

⇒ 키 이벤트는 **활성 앱의 키 창**으로 간다. 우리 앱이 활성화되면 발표 앱(Keynote 등)은 키를 받지 못한다.

### 1-1. 우리 코드가 키를 가로채는 곳 — 없다 · 전역 단축키는 조회만 한다 → **(나)**

- 후킹·핫키 등록·원시 입력 등록·창 프로시저의 키 메시지 처리: **0**(0절 표). 트레이 호스트 창 프로시저도 키 메시지를 처리하지 않고 `DefWindowProc`로 넘긴다(`Platform/Windows/WindowsSystemTrayIcon.cs:560`).
- 전역 단축키(⌃⌥⌘ + 글자)는 **상태 조회**다. Windows `GetAsyncKeyState`(`Win32WindowService.cs:1519` `IsDown`, `:1558` `TryGetKeyPressed`) · macOS `CGEventSourceKeyState`(`MacWindowService.cs:2052`). 조회는 키 메시지를 큐에서 빼지 않는다. 그래서 **발표 앱이 포커스를 가진 동안에는 우리가 단축키를 읽어도 그 키는 발표 앱에 그대로 간다.**
- 캐릭터 우클릭·톱니 좌클릭 감지도 같은 조회다(`Win32WindowService.cs:1521-1529` `GetAsyncKeyState(VK_LBUTTON/VK_RBUTTON)` · `MacWindowService.cs:1999-2008` `CGEventSourceButtonState`). **조회 자체는 전경을 만들지 않는다**(E1 A-2 (마)와 같다).
- 패키지 DLL도 가로채기 API는 0이다. 임포트된 `GetAsyncKeyState` 1건은 조회 전용 API다(용도는 추정 `GetModifierKeys` — `Runtime/Scripts/LowLevel/UniWinCore.cs:206`, 미확인).

### 1-2. 우리 창·프로세스가 포커스를 가진 동안 Unity 쪽에서 키가 쓰이는 곳

**전제**: 아래는 전부 **우리 앱에 포커스가 있을 때만** 돈다 — Unity `Input`은 포커스를 가진 동안만 참이다(`Platform/IGlobalKeyStateService.cs:148-150` 주석, `Interaction/PopoverPanel.cs:487-488` 주석). uGUI 모듈도 플레이어 빌드에서 포커스가 없으면 아무것도 처리하지 않는다(패키지 `com.unity.ugui@8bb446d869cd` `StandaloneInputModule.cs:157-164` `ShouldIgnoreEventsOnNoFocus()` → 플레이어 `return true`, `:293-296` `Process` 조기 반환).

| 키 | 우리 쪽 결과 | 근거 |
|---|---|---|
| **Space · Enter(Return/키패드)** | uGUI **Submit** → **마지막으로 uGUI 경로에서 눌린 버튼을 다시 누른다** | 씬 `Assets/_Project/Scenes/Main.unity:493-496`(Horizontal/Vertical/Submit/Cancel) · `:513` `m_sendNavigationEvents: 1` / `ProjectSettings/InputManager.asset` Submit 2항목 = `return`·`joystick button 0` / `enter`·`space` / `StandaloneInputModule.cs:307-313`·`:469-476` / `Button.cs:148-150` `OnSubmit → Press()` |
| **← → ↑ ↓ · A D W S** | uGUI **Move** → 선택이 이웃 버튼으로 옮겨 간다(자동 내비게이션) | InputManager Horizontal = `left`/`right`·`a`/`d`, Vertical = `down`/`up`·`s`/`w` / `StandaloneInputModule.cs:310` |
| Esc | uGUI Cancel(처리기 0 — `ICancelHandler` 비테스트 0건) · 긴급 관통 해제는 **개발 게이트 뒤** | `Core/StickmanAgent.cs:135` `EmergencyDisableKey = KeyCode.Escape` · `:968` `StickMateDevTools.Enabled &&` 선행 |
| **PageDown · PageUp · F5 · B · 마침표** | **아무 동작 없음**(바인딩 0). 단 발표 앱에도 가지 않는다(1-0) | InputManager에서 `page down`·`page up`·`f5`·`b`·`period` 각 0건(양성 대조 `space` 2 · `escape` 1) |
| 아무 키 | 팝오버 무입력 시계만 되감는다 | `Interaction/PopoverPanel.cs:489` `Input.anyKey` |
| 글자 입력 칸이 선택된 동안 | 입력 칸이 키를 먹는다(이름 편집·설정 숫자 칸·할일 입력) | `Interaction/CharacterInfoWindow.cs:1292-1293` · `Interaction/SettingsControls.cs:708-712` · `Interaction/TodoBoardPopover.cs:947` |

**「다시 누르기」가 성립하는 사슬** (코드상):
1. uGUI 경로의 클릭은 `Selectable.OnPointerDown`에서 그 버튼을 **선택 객체로 만든다** — 내비게이션이 `None`이 아니면(`Selectable.cs:1200-1207`). 우리 코드에는 내비게이션 설정이 0건이라 기본값 `Navigation.defaultNavigation`(`Selectable.cs:127`)이 그대로다.
2. 버튼 처리기의 가드는 **같은 키 0.35초 중복 방지뿐**이다(`Interaction/SettingsWindow.cs:91` `ActionDedupSeconds = 0.35f` · `:1085-1091` / `CharacterInfoWindow.cs:473` / `PopoverPanel.cs:166`). 0.35초보다 늦은 Space는 새 동작으로 통과한다.
3. **우리 코드 기준** 선택 해제 코드는 0건이다(`SetSelectedGameObject(null` 비테스트 0 — 같은 도구로 `SetSelectedGameObject(` 2건이 잡힌다). uGUI 자체에는 해제가 있다: `PointerInputModule.cs:427` `DeselectIfSelectionChanged`(호출 `StandaloneInputModule.cs:366`)가 **우리 uGUI가 처리하는 포인터 누름**이 다른 대상을 가리킬 때 선택을 비운다. 그 처리도 포커스를 전제하므로(위 전제), 남의 앱을 누르는 클릭으로는 선택이 비워지지 않는다. 그리고 창이 `SetActive(false)`면 Submit이 닿지 않는다(`ExecuteEvents.cs` — `Execute`가 거치는 `GetEventList`가 대상의 `activeInHierarchy`를 먼저 보고(`:325`), 컴포넌트는 `isActiveAndEnabled`로 거른다(`:312`)). 창을 다시 열었을 때 옛 선택으로 Submit이 다시 닿는지는 **미확인**(uGUI 내부 상태).
- ★ 가장 나쁜 조합 — 설정창 **[지금 종료]는** 2단 확인이고 확인 창이 **3초**다(~~`SettingsWindow.cs:95` `QuitConfirmSeconds = 3f`~~ → ★ **정정 2026-09-28 (code-inspection)**: 그 상수는 이제 `SettingsWindow.cs`에 **없다** — 중복 정의를 없애고 정본 `ActionCommandPopover.QuitConfirmSeconds`(3f) 한 자리를 참조한다. 값 **3초는 그대로**이므로 이 문단의 결론은 바뀌지 않는다. 지금 이 파일에서 그 상수를 읽는 자리는 `SettingsWindow.cs:822`(무장 만료)·`:1668`(1차 클릭 로그)다. ★ **아울러 `:95`는 통합 전에도 이미 틀렸다**(그때도 `:100`이었다) — 줄 번호 인용은 편집마다 썩는다. ★ **이 라운드가 재측정한 것은 이 괄호의 상수 관련 인용뿐이다**: 같은 괄호의 나머지 줄 번호는 통합으로 **+6줄씩 밀렸다**(첫 누름 무장 `:1652-1658` → `:1658-1664` · 두 번째 누름 `:1661-1664` → `:1667-1670` · uGUI 리스너 `:1939` → `:1945` · 전역 폴링 `:1060-1062` → `:1066-1068`). 이 밀림은 산술이고 앵커 하나씩 직접 읽어 확인하지는 않았다 — 쓰려면 다시 재라 · 첫 누름 무장 `:1652-1658` · 두 번째 누름 `:1661-1664` `Application.Quit()` · uGUI 리스너 `:1939` · 전역 폴링 경로 `:1060-1062` — 두 경로 모두 같은 키 `"quit"`로 `TryClaimAction`).
  - **① 클릭 1회 + Space 1회** (verify-change 지적으로 추가 · 코드 직접 확인): 그 버튼을 uGUI 경로로 누른 **클릭 자체가 1차 무장**이고, 같은 클릭이 그 버튼을 선택 객체로 만든다(`Selectable.cs:1200-1207`). 그 뒤 **0.35초 이상, 3초 미만** 사이에 Space를 한 번 누르면 Submit → 같은 리스너 → 두 번째 누름 → 종료 경로다.
    가드는 이것뿐이다: 같은 키 0.35초 중복 방지 `:1085-1091`(통과할 때만 시각을 갱신) · 3초 만료 `TickQuitConfirm` `:808-813` · 열기 `:662` · 닫기 `:682` · 탭 전환 `:2204`의 `DisarmQuit()`.
  - **② Space 2회**: 선택만 남고 무장이 풀린 뒤(3초 경과)에는, 서로 0.35초 이상 떨어진 Space 두 번이 3초 안에 들어오면 첫 번째가 무장, 두 번째가 종료다.
  - 조건: 그 클릭·Space 시점에 **포커스가 우리에게 있다**(uGUI는 포커스를 전제한다 — 이 절 전제. 클릭이 곧 포커스를 만드는지는 W-2 (다)) · 설정창이 떠 있다 · 그 버튼이 선택 객체다. 빈도는 코드로 못 잰다. **실기 미확인.**
- ★ **검증 발견(2026-09-15) — 설정창 [지금 종료]는 한 번 길게 누르면 종료 경로에 닿는다** (verify-change 발견 · dev-platform이 HEAD blob `ad49497`로 재현 · 코드상, 실기 미확인).
  - **누를 때 — 전역 폴링이 무장한다.** `SettingsWindow.cs:805` `TickGlobalPointer()`는 창이 열려 있고 표면 억제가 아니면(`:755` · `:771-787`) **포커스 조건 없이** 돈다. 입력은 전역 버튼 조회이고 평소 0.05초 간격이다(`:85`). `:884` 누름 상승 에지 → `:889` 헤더 손잡이가 아니면(푸터는 해당 없음, `:936-945`) → `:890` `FeedClick` → `:1060-1062` `TryClaimAction("quit")` → `OnQuitClicked` 무장.
  - **뗄 때 — uGUI Button이 발동한다.** `StandaloneInputModule.cs:607` `ProcessMousePress` → `:677` `ReleaseMouse` → `:206-215`(누를 때 기록한 클릭 처리기 위에서 떼고 `eligibleForClick`이면 실행) → `Button.cs:109` `OnPointerClick` → `:64` `Press` → 리스너 `:1939` `TryClaimAction("quit")`.
  - **가드가 못 막는 이유**: `:1087`은 같은 키가 0.35초 **미만**일 때만 거부하고, 시각은 통과할 때만 갱신한다(`:1088-1089`). 누른 채 **0.35초 이상 3초 미만** 뒤에 떼면 뗌이 두 번째 누름이 되어 `:1661-1664` `Application.Quit()`에 닿는다. 0.35초 미만의 빠른 클릭은 뗌이 가드에 막혀 무장만 남는다(의도된 2단 확인 그대로).
  - **같은 물리 클릭에서 두 경로가 둘 다 도는가 — 포커스가 있으면 둘 다, 없으면 전역 폴링만.** uGUI는 플레이어 빌드에서 포커스가 없으면 `Process`가 바로 반환한다(`StandaloneInputModule.cs:157-164` · `:293-296`). 게다가 **누름 프레임을 uGUI가 처리하지 못했으면 뗌에서도 발동하지 않는다** — 클릭 처리기는 누름을 처리할 때만 기록되고(`:661`) 뗌 처리 끝에 비워진다(`:225`). 그래서 「그 누름이 포커스를 만드는」 경우는 포커스 전달과 그 프레임 입력 처리의 선후에 달린다 → W-2 (다).
  - **그 누름이 우리 창에 도착하는 조건(Windows)**: 설정창 클릭 차단막(`SyncClickBlocker` `:2280-2294`, 창 사각형 전체 `BoxCollider2D`)이 커서 아래에 있어 UniWinC 히트테스트가 관통을 끈다(E1 A-2 (가)). macOS도 같은 코드다(플랫폼 중립).
  - **재현 결과: 재현 · 조건 차이 2건**
    - (a) 검증 보고의 `StandaloneInputModule.cs:436-438`은 **터치** 경로(`ProcessTouchPress` `:352`)다. 마우스 경로의 클릭 발동은 `ReleaseMouse` `:206-215`다. 결론은 같다.
    - (b) 드래그 임계 10px(`Assets/_Project/Scenes/Main.unity:514`)는 **조건이 아니다.** 임계를 넘은 드래그가 클릭을 취소하는 것은 누른 대상 위쪽에 드래그 처리기가 있을 때뿐이다(`PointerInputModule.cs:369-378` — `pointerDrag`가 있어야 `eligibleForClick = false`). [지금 종료]의 조상 계층(Label[`CrispText`→`Text`] → Quit[`Image`·`Button`] → Footer → SettingsPanel → SettingsCanvas[`Canvas`·`CanvasScaler`·`GraphicRaycaster`], 루트)에 드래그 처리기가 0이라 누를 때 `pointerDrag`가 null이다(`PointerInputModule.cs:356`; 계층 근거 `SettingsWindow.cs:1181`·`:1195`·`:1877-1878`·`:1911-1937`, `UiChrome.cs:1323`·`:1071`·`:1083`·`:1876` — verify-change 3차 계층 판정). 설정창 파일 전체로 보면 드래그 처리기가 0은 아니다 — `SettingsControls.cs:1444`가 uGUI `InputField`(드래그 처리기 구현)를 붙인다. 그 입력칸은 종료 버튼의 조상이 아니라 결론은 같다. ⇒ **끌고 나갔다 돌아와서 떼도, 뗀 자리가 같은 버튼이면 발동한다**(`:213` 조건은 「뗀 자리의 클릭 처리기 = 누른 자리의 것」과 `eligibleForClick`뿐).
  - **R-3(내비게이션 끄기)으로는 해소되지 않는다** — 키보드가 아니라 마우스 두 입력 경로의 중복이다 → 3절 R-7.
  - 조건: 포커스가 우리에게 있다(W-2 (다)) · 설정창이 떠 있다 · 누름과 뗌이 같은 버튼 위다. **실기 미확인.**
- 창 캔버스는 전부 `GraphicRaycaster`를 단다(`SettingsWindow.cs:1181` · `CharacterInfoWindow.cs:1357` · `PopoverPanel.cs:1004` · `TodoPostItWidget.cs:1116` · `GearRadialMenuWidget.cs:2348`). 그래서 포커스가 있는 동안의 클릭은 uGUI 경로도 탄다.

### 1-3. Windows — 경로별 판정

#### W-1 트레이 메뉴 → **(가) 코드상 발생** — 전경 요청과 복귀 코드 부재까지. 요청의 OS 성공 여부는 실기
- 사슬: 트레이 좌·우클릭 뗌(`Platform/SystemTrayPresencePolicy.cs:218-219` `IsMenuTriggerMessage`) → `WindowsSystemTrayIcon.cs:550` `ShowMenu()` → **`:590` `SetForegroundWindow(_hostWindow)`** → `:594-596` `TrackPopupMenu`(중첩 모달 루프) → `:600` `PostMessage(WM_NULL)` → 고른 명령은 다음 `Tick`에 배달(`:602-607`).
- 이 순서는 MS 문서 요구 그대로다 (TrackPopupMenu Remarks): *"To display a context menu for a notification icon, the current window must be the foreground window before the application calls TrackPopupMenu … Otherwise, the menu will not disappear when the user clicks outside of the menu."*
- 결과: 메뉴가 닫힌 뒤 코드상 전경 **요청** 대상은 우리 숨은 호스트 창이다. 요청이 OS에서 받아들여지는지는 **실기 미확인**이다 — SetForegroundWindow Remarks: *"It is possible for a process to be denied the right to set the foreground window even if it meets these conditions."*
  받아들여지면 `WM_ACTIVATE`의 기본 처리가 포커스를 그 창에 주고(1-0 인용), 그 창 프로시저는 키 메시지를 `DefWindowProc`로 넘기며 기본 처리는 아무것도 하지 않는다(*"does nothing with them"*). ⇒ **넘기기 키는 발표 앱에 가지 않는다.** 우리 앱(Unity 창)에 닿는지는 **미확인**이다(아래 ★ 항목).
- 메뉴 명령(`Platform/SystemTrayPresencePolicy.cs:149-154` `MenuOrder`: 캐릭터 숨기기/다시 보이기 · 설정 열기 · 종료)은 **어느 것도 발표 앱에 포커스를 돌려주지 않는다**(우리 코드에 남의 창 활성화 호출 0).
- ★ **미확인 — 트레이 명령 뒤 Unity 창이 활성화되는가.** 우리 `Platform/` 코드에는 `SetWindowPos`·`ShowWindow`가 0건이다(주석 제외, 양성 대조 `GetForegroundWindow(` 5건). 패키지 DLL은 `ShowWindow`·`SetWindowPos`를 각 1회 임포트한다(`strings -a` 정확 일치, 없는 이름 0). 어느 경로에서 어떤 플래그로 부르는지는 판독하지 않았다. [설정 열기]·[다시 보이기] 처리 중 Unity 창이 활성화되면 키는 호스트 창이 아니라 Unity 창으로 가고, **1-2의 Space·Enter 재발동이 W-1에서도 성립한다.** → 1-6 K-① 관찰 항목.
- ★ 정직한 완화 사실(OS 동작, **이 문서 미확인**): 트레이 아이콘을 누르는 행위 자체가 작업표시줄(셸 창) 클릭이라, 그 순간 이미 발표 앱의 포커스가 떠난다고 본다. **우리 코드가 더하는 것은 포커스가 머무는 곳이 셸 대신 우리 숨은 창이 된다는 것**이다. 발표 앱 입장의 결과(키를 못 받음)는 같다.
  그리고 단일 모니터 전체화면 슬라이드쇼에서는 작업표시줄이 안 보이므로, 이 경로는 주로 **발표자 보기(보조 모니터)·창 모드 읽기 보기**에서 난다.
- **사용자 체감**: 「발표 중 트레이 메뉴로 캐릭터를 숨긴 뒤, 리모컨 [다음]이 **한 번 이상** 안 먹는다.」 한 번이 아니라 **다시 풀릴 때까지 계속**이다.
- **풀리는 조건**: 발표 창을 한 번 클릭하거나 Alt+Tab으로 발표 앱을 고른다(1-0 인용 「clicking it」·Alt+Tab). 메뉴를 **바깥 클릭으로 닫았고 그 클릭이 발표 창에 떨어졌다면** 그 클릭이 곧바로 풀어 줄 가능성이 있다 — 메뉴 루프가 그 클릭을 발표 창에 넘기는지는 **(다) 실기**.
- ★ 이 경로는 E1 A-2 (라)가 적은 **등급 흔들림(H1)과 같은 입구**다. 등급 판정 사유 S2(`전경 창이 우리 프로세스(pid`)가 이 창에서 나온다.

#### W-2 우리 창 클릭(캐릭터·톱니·부채꼴 경계상자·정보창·설정창·팝오버) → **(다) 실기 필요**
- 클릭이 우리 창에 도착하는 조건과 막는 장치 0건은 E1 A-2 (가)(나)와 같다. 최종 결과는 **Unity 플레이어 원래 창 프로시저의 `WM_MOUSEACTIVATE` 반환값**이 정한다. 문서상 `MA_ACTIVATE`/`MA_ACTIVATEANDEAT`면 활성화, `MA_NOACTIVATE`/`MA_NOACTIVATEANDEAT`면 비활성화다(Microsoft Learn, WM_MOUSEACTIVATE 반환값 표). 엔진 소스가 없어 판정할 수 없다.
- 패키지는 활성화를 **정상 사건으로 다룬다** — `UniWindowController.cs:904-915` `OnApplicationFocus(true)`에서 클릭 관통을 끈다. 포커스를 주는 API는 비워 뒀다(`:1205-1211` `Focus()` 안 `//uniWin.SetFocus();`).
- **활성화가 일어난다면**(가정):
  - 발표 앱은 다음 클릭 전까지 넘기기 키를 **하나도** 못 받는다(1-0).
  - 동시에 우리 쪽에서 Space·Enter·화살표가 **마지막 누른 버튼을 다시 누르거나 선택을 옮긴다**(1-2).
  - 등급 1 흔들림(H1)과 겹친다.
- **사용자 체감(활성화 시)**: 「캐릭터를 우클릭해 부채꼴을 쓰고(또는 설정창에서 값을 바꾸고) 발표로 돌아왔는데, 리모컨 [다음]이 안 넘어가고 **대신 설정창 탭이 바뀌거나 버튼이 눌린다**. 발표 화면을 한 번 누르면 그 뒤로는 정상.」
- **풀리는 조건**: 발표 창 클릭 / Alt+Tab. 부채꼴을 **경계상자 밖 클릭**으로 접었다면 그 클릭이 발표 창에 떨어지므로 그 순간 풀린다(2-2 (iii)).

#### W-3 전역 단축키·전역 버튼 조회 → **(나)** (1-1)

### 1-4. macOS — 판정 **(다) 실기 필요** (코드 사실과 실측 1건이 서로 다른 쪽을 가리킨다)

**코드 사실**
- 우리 코드의 앱 활성화 호출: **0**. `objc_msgSend`로 보내는 셀렉터는 `sharedApplication`·`setActivationPolicy:`·`activationPolicy`뿐이고(`Platform/MacOS/MacSpaceBehaviorNative.cs:184-185`·`:205-228`), 정책은 **보조 앱**(`:94` `NSApplicationActivationPolicyAccessory = 1`, 호출 `MacOverlayStateEnforcer.cs:279`)이다. Apple 문서상 보조 앱도 **창 클릭으로 활성화될 수 있다**(1-0).
- 패키지 네이티브(v0.9.8 소스): `NSApp.activate` 계열 호출 **0**. 대신 **우리 창이 키 창 자리를 잃으면 되찾는 관찰자**가 있다.
  - `:386` `didResignKeyNotification` 구독 → `:439-445` `_keepKeyWindowObserver`: 부착 때 키 창이었으면(`:140` `isKeyWindow` 저장) → `_makeKeyWindow()`.
  - `:473-488` `_makeKeyWindow`: 테두리 없는 창은 `canBecomeKey`가 거짓이라, 잠깐 테두리를 켜서 `makeKey()` 한 뒤 되돌린다(주석 `:479` 원문).
  - 우리 씬은 `_isTransparent: 1`(`Assets/_Project/Scenes/Main.unity:446-447`)이다. 패키지가 투명과 동시에 테두리 없음을 켜므로(`UniWinCore.cs:535-540` `EnableTransparent` → `SetBorderless(isTransparent)`) 이 분기를 탄다.
  - ⇒ **우리 앱 안에서는 우리 창이 계속 키 창으로 유지된다.** 이것은 앱 활성화가 아니다(`makeKey` 문서 요약 · `keyWindow`는 앱이 비활성이면 nil — 1-0). **우리 앱이 활성화되는 순간 키가 곧바로 우리 창으로 간다**는 뜻이다.
- 클릭 관통 해제는 `ignoresMouseEvents = false`다(v0.9.8 `:760-763`). 커서가 캐릭터·표면 위일 때 클릭이 우리 창에 도착하는 것은 Windows와 같은 구조다(`MacWindowService.cs:1297-1302`에서 관통 켤 때 히트테스트도 켠다 — 씬 기본값 `isHitTestEnabled: 0`(`Assets/_Project/Scenes/Main.unity:450-451`)을 실행 중 덮는다).
- 우리 코드 주석은 활성화를 **전제**한다: `Interaction/InfoGearIconWidget.cs:55-56` 「macOS에서 비활성 앱의 첫 클릭이 "앱 활성화"에만 소비되는 경우에도 확실히 잡는다」. 주석이지 측정이 아니다.

**실측 1건(반대 방향)**
- `Interaction/UiChrome.cs:666-668` — 「2026-09-02 밤 — 실측: 정보창을 연 상태에서 `Cmd+W`를 누르면 뒤에 있던 Finder 창이 …」. `Tasklist.md` 고정 문자열 앵커 `Cmd+W가 뒤에 있던 Finder 창을 닫았다`(실측 확정)도 같다.
- 뜻: 그 세션에서는 우리 표면을 클릭해 정보창을 연 뒤에도 **키가 뒤의 활성 앱(Finder)으로 갔다** — 우리 앱이 활성화되지 않았거나, 사용자가 그 사이 Finder를 눌렀다.
- **기록에 없는 조건**: 정보창을 연 경로(톱니/단축키), `Cmd+W` 직전 마지막 클릭 위치, 빌드 차수. 게다가 09-02 이후 입력 경로가 여러 번 바뀌었다(대기 톱니·캐릭터 우클릭 부채꼴 등). **현행 빌드의 증거로 쓸 수 없다.**

**판정 문장**: 「macOS는 우리 코드와 패키지 어디에도 앱을 활성화하는 호출이 없다. 다만 OS 규칙상 창 클릭이 보조 앱을 활성화할 수 있고, 활성화되면 패키지가 유지하는 키 창으로 키가 간다. 09-02 실측 1건은 키가 뒤 앱으로 갔다고 기록하지만 조건이 없다 → 현행 빌드 실기 1회로 가른다.」
- 활성화 시 사용자 체감·풀림 조건은 W-2와 같다(Keynote 슬라이드를 한 번 클릭).
- 활성화 시 1-2 표(Space·Enter 다시 누르기)도 macOS에 똑같이 적용된다. uGUI 코드는 플랫폼 중립이다.
- ★ macOS는 **전체화면 등급이 전경과 무관**하다(E1 A-3). 그래서 H1 형태는 없고 **키 행방만** 남는다.

### 1-5. 판정 요약

| 경로 | Windows | macOS |
|---|---|---|
| 전역 단축키·전역 버튼 조회 | **(나)** | **(나)** |
| 트레이 메뉴 | **(가)** — 전경 요청 대상 = 숨은 호스트 창(OS 성공 여부 실기 미확인), 명령 뒤 발표 앱 복귀 코드 없음 | 해당 없음(트레이 없음) |
| 우리 창 클릭 | **(다)** — 엔진 `WM_MOUSEACTIVATE` | **(다)** — 창 클릭의 앱 활성화 여부(09-02 실측은 반대 방향, 조건 미기록) |
| 포커스를 받은 뒤 우리 쪽 오작동 | **(가)** 조건부 — Space·Enter가 선택 버튼 재발동, 화살표가 선택 이동, [지금 종료]는 클릭 1회 + Space 1회, 또는 한 번 길게 누르기로 종료 경로 | 같음 |

### 1-6. 실기 확인 문안 (체크표 반영은 리더 판단 — 이 문서는 `WINDOWS_CHECK_SESSION.md`를 고치지 않았다)

- **포커스 로그는 없다** — 우리 코드에 `OnApplicationFocus`가 0건이다(비테스트). 판독은 **눈으로** 한다. 등급 판정 줄 S1/S2(E1 A-5)는 「그 폴링 순간 전경이 우리였다」는 **보조 증거**다.
- 대조용 키 수신 창은 **메모장**을 쓴다(글자가 찍히는지로 판정 — 발표 앱보다 모호하지 않다).
- ★ **안전**: 이 절차 동안 설정창 [지금 종료]를 **누르지도, 길게 누르지도 않는다** — 누른 뒤 3초 안의 Space 한 번, 또는 누른 채 0.35초 이상 있다가 떼는 것이 종료 경로다(1-2).
- **K-①(W-1)**: 메모장을 누르고 `a`를 한 번 친다(양성 대조 — 글자가 찍혀야 한다) → 트레이 아이콘 우클릭 → `Esc`로 메뉴 닫기 → `a` 한 번 → **메모장에 찍혔는가.** 이어서 트레이 메뉴 → [설정 열기] → `a` 한 번 → 찍혔는가 → (안 찍혔다면) 설정창이 떠 있는 채로 `Space` 한 번 → **설정창에 무엇이 바뀌었는가**(트레이 명령 뒤 Unity 창이 활성화됐는지의 간접 관찰 — 1-3 W-1 ★ 미확인 항목).
  **적는다**: `K-① Esc 뒤 찍힘/안 찍힘 · 명령 뒤 찍힘/안 찍힘 · Space 반응(없음/무엇)`.
- **K-②(W-2)**: 메모장에 `a`(양성 대조) → 캐릭터를 좌클릭 한 번(부채꼴·창을 열지 않는 짧은 클릭) → `a` → 찍혔는가.
  → 캐릭터 우클릭으로 부채꼴 → **버튼 사이 빈 곳이 아니라 버튼 하나**를 눌러 팝오버 → `a` → 찍혔는가.
  → (찍히지 않았다면) `Space` 한 번 → 팝오버·설정창에 **무엇이 바뀌었는가**.
  **적는다**: `K-② 캐릭터 클릭 뒤 찍힘/안 찍힘 · 부채꼴 버튼 뒤 찍힘/안 찍힘 · Space 반응(없음/무엇)`.
- **무효**: 양성 대조 `a`가 안 찍힘 · 메모장이 아닌 곳에 커서가 있었음.
- macOS는 같은 절차를 텍스트 편집기로 한다(K-①은 해당 없음).

---

## 2. (2) 부채꼴 「바깥 클릭 접힘」 — 어디서 일어나는가

### 2-1. 위치 — 위젯이 아니라 톱니 위젯의 **전역 폴링**이다

`GearRadialMenuWidget.cs:75`의 주석(「기어 재클릭 / 바깥 클릭 / 버튼 선택」)은 **모드 이름**만 정의한다. 위젯 파일에서 고정 문자열 `Collapse(`가 든 줄은 **10개**다 = 정의 1(`:929`) · `Collapse(` 호출 7(`:908`·`:989`·`:1102`·`:1149`·`:1190`·`:1375`·`:1386`) · `TickAutoCollapse(` 2(`:1323` 호출 · `:1364` 정의). 호출 7에 「바깥 클릭」 문자열 인자는 없다. 문자열 `부채꼴 바깥 클릭`이 주석이 아닌 코드에 나오는 곳은 `InfoGearIconWidget.cs:1614` 한 곳이다(같은 파일 주석 `:144`에 1회 더).

실행 사슬 (코드상):
1. `Interaction/InfoGearIconWidget.cs:789` `LateUpdate`
   - `:831-835` 전체화면 표면 숨김이면 반환.
   - 그 뒤 **대기 톱니가 보이든 말든** `:869` `TickPointer()`를 부른다(이 사이에 톱니 가시성 게이트 없음).
2. `:1548-1571` `TickPointer`: 평소 **0.05초 간격**(`:333` `ClickPollInterval`)으로 `TryGetPrimaryButtonPressed` 조회 — Windows `GetAsyncKeyState(VK_LBUTTON)`(`Win32WindowService.cs:1521-1523`) · macOS `CGEventSourceButtonState`(`MacWindowService.cs:1999-2001`).
3. `:1575-1584` `ProcessPointer`가 눌림 상승 에지에서 `:1586` `BeginPress`.
4. `:1602-1616` 부채꼴이 펼쳐져 있으면:
   - 버튼 원 위(`GearRadialMenuWidget.HitTest` `:1202-1215`, 원 판정)면 그 버튼.
   - 아니고 톱니 사각형 밖이면, 팝오버가 붙어 있지 않을 때(`AnchoredButton < 0`) **`:1614` `CollapseMenu(User, "부채꼴 바깥 클릭")`** → `:1504-1509` → `GearRadialMenuWidget.Collapse` `:929-943`.
   - 주석 `:1591-1592`: 「접기는 그 클릭을 **소비하지 않는다** — 밑에서 하려던 일은 그대로 일어난다」.
- 로그: `[부채꼴] 접힘(사용자 동작) — 부채꼴 바깥 클릭.`(`GearRadialMenuWidget.cs:942` 조립).
- 캐릭터 우클릭으로 연 부채꼴(`AppControlDirector.cs:991`)도 같은 폴링으로 접힌다 — 톱니 위젯이 부채꼴의 앵커 출처를 가리지 않는다.
- 부산물(코드 산술): 누름과 뗌이 **한 폴링 간격(0.05초) 안에** 끝나면 상승 에지를 못 봐 접히지 않는다. 안전한 쪽(아무 일 없음)의 실패다.

### 2-2. 차단막 — 있다. 다만 **화면 전체가 아니라 부채꼴 경계상자**다

- `_clickTarget`은 isTrigger `BoxCollider2D` 하나다(`InfoGearIconWidget.cs:1398-1399`). 크기는 `:1157-1173`에서 **톱니 사각형(보일 때만) ∪ `_menu.UnionScreenRect`로** 잡는다.
- 켜짐 조건은 `:1166` `_standbyVisible || fanVisible`이다.
- `UnionScreenRect`는 **보이는 버튼 상자들의 경계상자**다(`GearRadialMenuWidget.cs:1491-1505`). 이름표·첫 안내는 빠진다(`:1500-1501` 주석, `:2395-2433` `raycastTarget = false`).
- 패키지 히트테스트(씬 `hitTestType: 2` = Raycast, `Assets/_Project/Scenes/Main.unity:434-435`)가 **커서 아래 `Collider2D`/uGUI 그래픽**을 보고 클릭 관통을 끈다(E1 A-2 (가)). 부채꼴 캔버스에도 `GraphicRaycaster`가 있다(`GearRadialMenuWidget.cs:2348`).

| 클릭 위치 | 그 클릭의 행방 | 부채꼴 | 전경 |
|---|---|---|---|
| (i) 버튼 원 위 | 우리 창 | 그 버튼 동작 | W-2와 같다(Windows 엔진 / macOS 활성화 — **(다)**) |
| **(ii) 경계상자 안, 버튼 사이 빈 곳** | **우리 창**(콜라이더가 덮음 → 관통 꺼짐) — **아래 앱은 이 클릭을 못 받는다** | **접힘**(HitTest −1 · 톱니 밖) | W-2와 같다 — **(다)** |
| (iii) 경계상자 밖 | **아래 앱**(콜라이더 없음 → 관통 유지) | **접힘**(전역 폴링이 따로 봄) | 우리 창에 마우스 메시지가 가지 않는다 → **우리 쪽 활성화 없음.** 눌린 앱이 OS 기본대로 활성화된다 → 키보드가 **발표 앱으로 돌아가는 방향** |
| (iv) 팝오버가 붙어 있는 동안 | 팝오버 쪽 규칙 | **접지 않음**(`:1614` 조건) | — |
| (v) 톱니 위 | 우리 창 | 톱니 재클릭 토글(`:1780`) | W-2와 같다 |

### 2-3. 판정

- **구현 위치**: 전역 입력 조회(`InfoGearIconWidget.TickPointer`)다. 화면을 덮는 차단막 방식이 **아니다.** — **확정(코드)**.
- **아래 앱 클릭을 먹는가**: **(가) 코드상 발생 — (ii) 경계상자 안 빈 곳 한정.** 호 위에 늘어선 버튼 5개의 **경계상자 사각형**이라 호 안쪽·모서리에 빈 면적이 생긴다. 넓이는 배치에 달려 있어 이번에 **재지 않았다**(미측정).
  (iii) 경계상자 밖 클릭은 **먹지 않는다 — (나).**
- **전경을 가져오는가**: 접힘 코드 자체는 전경 API를 부르지 않는다 **(나)**. (i)(ii)(v)의 클릭은 H1과 같은 입구라 **(다)**. (iii)은 오히려 전경을 **아래 앱에 돌려준다**(OS 기본).
- 부수 후보(**미판독**): 대기 톱니가 **보이는 동안**(`StandbyGearPolicy.ShouldShow` `:933-934` = 사용자 숨김 ∥ 가출) 캐릭터 앵커 부채꼴이 열리면, `:1160` `Union(IconScreenRect, fan)`이 **우상단 톱니와 부채꼴을 잇는 경계상자**가 된다 — 2026-09-05 P0(`:1143-1149` 주석)와 같은 모양이다.
  우클릭 게이트는 `CharacterOnScreenForInput(IsSuspended, IsCharacterHiddenByRunaway)`(`AppControlDirector.cs:946-948`)라 대부분 막히는 것으로 보인다. 그러나 「사용자 숨김 ⇒ `IsSuspended`」와 「가출 활성 ⇒ 캐릭터 숨김」의 시간 관계를 **읽지 않았다.** 이 문서의 판정 범위 밖으로 남긴다.

---

## 3. 대응안 후보 (구현 금지 · 리더 판단용)

| # | 대상 | 후보 | 효과 | 위험·충돌 | 파일 · **N-8 겹침** |
|---|---|---|---|---|---|
| R-1 | W-1 트레이 | (a) 메뉴 전 전경 창을 기억했다가 메뉴 뒤 되돌림 | 발표 앱으로 키 복귀 | **남의 창에 `SetForegroundWindow`** — 원칙 3 승인 예외 범위(호스트 창 고정, `WindowsSystemTrayIcon.cs:55-57` 조건 1)를 넘는다. `UserAssetImmutabilityAuditTests`가 문다 → **기각 방향**(사용자 판단 사안) | `WindowsSystemTrayIcon.cs` = **X**(알려진 제외 — ②(가) 추출에 안 들어가 N-8 기준 불변) |
| R-1 | 〃 | (b) 코드 무변경 + 매뉴얼에 「트레이 메뉴 뒤에는 발표 화면을 한 번 누르세요」 고지 | 사용자 인지 | 행동 부담이 사용자에게 간다 | 문서만 · 목록 밖 |
| R-2 | W-2 클릭 활성화(Windows) | (a) `WS_EX_NOACTIVATE` | 활성화 차단 | E1 A-6 (b) **기각 방향 유지**(입력 칸 포커스 전제 · 「자기 창 스타일 쓰기는 해소기 한 파일」 규칙) | `Win32WindowService.cs` = **A** · 스타일 쓰기 파일 추가 시 규칙 6(E) 편입 검토 → **N-8 다른 경로(E-3 재시험)** |
| R-2 | 〃 | (b) Unity 창 `WM_MOUSEACTIVATE`를 서브클래스해 **입력 칸 편집 중이 아니면** `MA_NOACTIVATE` | 활성화 차단 + 이름 편집 유지 | Unity 창 서브클래싱은 트레이 파일이 스타일 경합을 이유로 **피한 자리**(`WindowsSystemTrayIcon.cs:72-79`) · 패키지가 이미 서브클래스(체인 순서) · 판정(「지금 키 입력이 필요한 표면이 있는가」)은 `Platform/` 중립 정책에 두고 Windows는 사실만 · macOS 대응(보조 앱 활성화 억제)은 **대응 API 미조사** | `Win32WindowService.cs` = **A**, 새 Windows 파일 = 규칙 6(E) 후보 → **N-8 다른 경로** |
| R-3 | 1-2 포커스 뒤 오작동(양 플랫폼) · ★ **리더 채택: 1.0 필수, 대상 확대** | EventSystem `sendNavigationEvents = false`(또는 버튼 공장에서 `navigation.mode = None`). 기본값이 참(`EventSystem.cs:55` `m_sendNavigationEvents = true`)이라 **씬만 고치면 폴백 생성분이 남는다** — 생성 지점 전부를 함께 고친다 | Space·Enter·화살표가 버튼을 누르거나 선택을 옮기지 않는다([지금 종료] 클릭+Space 경로 소멸). **키를 발표 앱에 돌려주지는 못한다** | 입력 칸은 자체 이벤트 처리(`InputField.cs:2018-2049` `ProcessEvent`)라 영향 없을 것으로 판단 — **실기 미확인**. 할일 Enter 추가(`TodoBoardPopover.cs:947`)는 입력 칸 제출이라 무관 | 씬 `Assets/_Project/Scenes/Main.unity:513` · 런타임 폴백 4파일 `Interaction/CharacterInfoWindow.cs:2046`·`Interaction/SettingsWindow.cs:2335`·`Interaction/PopoverPanel.cs:1156`·`Interaction/TodoPostItWidget.cs:1295` · 에디터 `Assets/Editor/SceneBootstrapper.cs:1847`(생성)·`:1859`(모듈 추가) → 6개 전부 **목록 밖**(46행 등급 조회 빈 값 · 양성 `FramePacing.cs` A · 음성 없는 경로 빈 값 · 에디터 코드는 목록 규칙상 제외) → **N-8 불변** |
| R-4 | 2-3 (ii) 빈 곳 클릭 흡수 | 경계상자 `BoxCollider2D` 대신 **버튼별 원형 콜라이더**(또는 부채꼴 구간은 버튼 그래픽 레이캐스트만) | 빈 곳 클릭이 아래 앱으로 간다(접힘은 전역 폴링이라 유지) | 버튼 그래픽이 원 판정 반지름(`HitPaddingPoints` 포함)을 덮는지 확인 필요 — 덮지 못하면 「보이는데 새는」 가장자리가 생긴다(`GearRadialMenuWidget.cs:2035` 주석과 같은 병) | `InfoGearIconWidget.cs`·`GearRadialMenuWidget.cs` 목록 밖 → **N-8 불변** |
| R-5 | 관측 수단 | 포커스 변화 한 줄 로그(`OnApplicationFocus` → `[포커스] 얻음/잃음`), 새 파일 | 1-6 실기가 눈 판독에서 로그 판독으로 | 상시 로그 양 — 전이에서만 1줄 | 새 파일이면 목록 규칙 1–6 해당 없음 → **N-8 불변**. `StickmanAgent.cs`(B)에 넣으면 **다른 경로** |
| R-6 | 문서 전칭 문장 | 5절 목록의 문장을 실기 결과 뒤 한정형으로 | 거짓 약속 제거 | 실기 전 정정은 반대 방향 거짓 위험 | 문서만 · 목록 밖 |
| R-7 | 1-2 검증 발견 — 설정창 [지금 종료] 한 번 길게 누르기 · ★ **리더 채택 1.0 필수 — 구현 설계는 coder-ui**(K2·K3′와 같은 라운드) | 두 입력 경로(전역 폴링 누름 · uGUI 뗌)의 중복 처리를 없앤다 — 권고안은 표 아래 | 물리 클릭 하나가 누름 하나로만 센다 | R-3으로는 해소되지 않는다(마우스 경로) · 같은 두 경로 구조가 설정창의 다른 버튼과 다른 창에도 있는지 전수 필요(이 문서 미실시) | `Interaction/SettingsWindow.cs` 목록 밖 → **N-8 불변** |

**R-7 권고안 (구현 설계는 coder-ui — 리더 채택)**
- **안 A — 경로 단일화(전역 폴링을 정본으로)**: 설정창 버튼의 uGUI `onClick` 리스너를 떼고, 누름 상승 에지(`ProcessPointer` `:884`) 한 곳에서만 발동한다.
  장점: 포커스 유무와 무관하게 같은 동작이다(`FeedClick`의 「비활성 앱의 첫 클릭 경로」 주석 `:1058-1059`와 같은 이유) · 누름/뗌 시점 차이가 원천에서 사라진다 · R-3과 합치면 키보드 Submit 경로도 함께 사라진다.
  단점: 기존 테스트가 uGUI 클릭 경로를 전제하는지 전수가 필요하다 · uGUI 누름 시각 상태를 쓰는 곳이 있으면 다시 그려야 한다(미확인) · 다른 창(정보창·팝오버·포스트잇)의 같은 구조와 기준이 갈라지지 않게 함께 봐야 한다.
- **안 B — 물리 누름 단위 가드**: 무장 뒤 두 번째 누름은 **전역 폴링이 뗌을 한 번 관측한 뒤의 새 누름**에서만 인정한다(무장한 누름이 끝나기 전 입력은 경로와 무관하게 거부).
  장점: 두 경로를 유지하는 최소 변경이다 · 시간 상수(0.35초) 경계에 기대지 않는다.
  단점: 전역 폴링이 0.05초 간격(`:85`)이라 아주 짧은 뗌과 재누름을 놓칠 수 있다(안전한 쪽 실패 — 종료가 안 됨) · uGUI 뗌이 전역 폴링의 뗌 관측보다 먼저 오면 순서 의존이 생긴다 · 버튼마다 같은 가드를 반복해야 한다.
- 어느 안이든 「누름 → 0.35초 이상 대기 → 뗌」 합성 입력에서 종료 경로 도달 0을 단언하는 테스트로 잠글 수 있다(종료 대리 훅은 설계 범위).

**착수 순서 제안(판단은 리더)**:
1. 1-6 실기 1회. R-5가 있으면 판독이 확실해진다.
2. R-3·R-7(리더 채택 1.0 필수 · R-7 구현 설계는 coder-ui) · R-4 — 목록 밖이고 양 플랫폼 공통, 위험이 작다.
3. R-1·R-2 — 원칙·N-8 비용이 크다. 실기 결과 뒤에 판단한다.

---

## 4. `PlatformParityAuditTests` 등재안 (문안만 · 구현 금지)

- **위치**: `미해결_전체화면_등급의_기준_모니터와_적용_범위가_갈라져_있다` 끝 바로 뒤.
- **이름**: `미해결_우리_창이_전경을_얻은_뒤_키보드가_남의_앱으로_가지_않는_경로`
- **Ignore 전 가드**(썩음 방지 · 니들은 존재 대조를 같은 테스트에 둔다):
  - `WindowsSystemTrayIcon.cs` 본문에 `SetForegroundWindow(_hostWindow)`가 **없으면** `Assert.Fail`. 이 호출이 없는 판에서는 W-1 전제가 달라졌으므로 사람이 다시 본다.
  - R-3 착지 판정은 **생성 지점 6곳 전부**(3절 R-3 행: 씬 1 · 런타임 폴백 4 · 에디터 1)를 본다. 씬 EventSystem `m_sendNavigationEvents: 1`이 **없어지고**, 폴백 4파일과 에디터 1파일의 생성 줄마다 `sendNavigationEvents = false`(또는 버튼 공장의 `Navigation.Mode.None`)가 **생기면** 해당 줄을 떼고 `Assert.Pass`. 한 곳만 보면 기본값 참(`EventSystem.cs:55`) 때문에 폴백 생성분이 남은 채 초록이 된다.
    두 신호(씬 문자열·코드 문자열)를 **각각 존재 대조**한다 — 부재 단언만 두면 조용히 초록이 된다.
- **사유 문장**:
```text
【미해결 · 입력 포커스 · 코드 판독 / 실기 0회】 신설 2026-09-15 (dev-platform)
· Windows 트레이 메뉴: KB135788 관용구가 우리 숨은 호스트 창을 전경으로 요청하고, 메뉴 명령 뒤 발표 앱에 포커스를 돌려주는 코드가 없다(코드상 발생 · 요청의 OS 성공 여부와 트레이 명령 뒤 Unity 창 활성화 여부는 실기 필요).
· Windows 우리 창 클릭: 활성화 여부는 Unity 창 프로시저의 WM_MOUSEACTIVATE가 정한다(실기 필요).
· macOS: 활성화 호출 0, 패키지가 우리 창을 키 창으로 유지 — 창 클릭이 보조 앱을 활성화하면 키가 우리 창으로 간다(실기 필요, 2026-09-02 실측 1건은 반대 방향·조건 미기록).
· 두 플랫폼 공통: 포커스를 받은 동안 uGUI 내비게이션(Space·Enter·화살표)이 마지막 누른 버튼을 다시 누른다 — 설정창 [지금 종료]는 클릭 1회 + Space 1회로 종료 경로에 닿는다. 마우스 두 입력 경로(전역 폴링 누름 · uGUI 뗌)의 중복으로 한 번 길게 누르기도 종료 경로에 닿는다(R-7).
해소 조건: 실기로 경로별 발생 여부를 확정하고, 판정(키 입력이 필요한 표면이 열려 있는가)은 Platform/ 중립 정책에, 활성화 억제는 플랫폼 사실 조회 파일에 둔다.
근거: docs/platform/FOREGROUND_INPUT_FACTS.md 1절.
```

---

## 5. 연결 목록 (이 문서는 아래 파일을 **수정하지 않았다** · 고정 문자열 앵커)

**전칭 문장은 주어로 두 부류를 나눈다** (verify-change 수정 4).
- **부류 K(키 행방)** — 「키가 아래 앱으로 간다」. W-1에서 **거짓**이다: 요청이 성공하면 호스트 창, 거부되면 셸 — 어느 갈래든 발표 앱은 아니다(발표 앱 복귀 코드 0은 코드 사실, 두 갈래의 OS 동작은 실기 미확인). W-2 활성화 때도 거짓이다.
- **부류 O(우리 창·판)** — 「판·오버레이·이 앱의 창이 포커스를 안 받는다」. W-1에서 포커스는 판(Unity 창)이 아닌 곳으로 간다 — 요청이 성공하면 숨은 호스트 창, 거부되면 셸(두 갈래의 OS 동작은 실기 미확인) — 그래서 거짓이 아니다. **W-2 활성화가 일어날 때만** 거짓이다(현재 (다)). 트레이 명령 뒤 Unity 창 활성화(1-3 ★ 미확인)가 확인되면 W-1에서도 거짓이 된다.
- macOS 전용 문장은 1-4 (다)에 달린다.

| 파일 | 앵커 | 관계 |
|---|---|---|
| `docs/platform/E1_FOREGROUND_TIER_FACTS.md` | `명시적 전경 전환이 1곳 더 있다` | W-1과 같은 입구(등급 흔들림 쪽). 키 행방은 이 문서 1-3 |
| 〃 | `코드로 판정할 수 없는 한 곳` | W-2의 미확인과 같은 지점 |
| `docs/manual/01-where-to-click.md` | `키보드로 누른 키는 전부 아래에 있는 앱으로 갑니다` | **부류 K** — W-1에서 거짓(요청 성공 시 호스트 창, 거부 시 셸 — 어느 갈래든 발표 앱 아님. 복귀 코드 0은 코드 사실, 갈래별 OS 동작은 실기 미확인). 정정 여부는 리더·매뉴얼 담당 |
| 〃 | `이 판은 키보드 입력을 받지 않습니다` | **부류 O** — W-2 활성화 때만 거짓(현재 (다)) |
| 〃 | `뒤에 있던 다른 앱의 창이 닫힙니다` | macOS ⌘W 행, 09-02 실측 기반(1-4). **macOS 앱 활성화가 일어나면** ⌘W는 우리 앱으로 간다 → 1-4 (다)에 달린다. Windows 경로(W-1·W-2)와 무관 |
| `docs/UX_FLOW.md` | `오버레이는 포커스를 가져가지 않음` | **부류 O** — W-2 활성화 때만 거짓(현재 (다)) |
| 〃 | `온보딩이 포커스를 뺏지 않는다` | **부류 O**(주어 = 온보딩). 온보딩 코드에 활성화 호출 0 — 표의 상황(사용자가 **다른 창**을 클릭)에서는 참. 사용자가 온보딩 표면을 누르면 W-2 (다) |
| 〃 | `OS 창이 아니므로` | 「그림이라 포커스를 못 받는다」는 **창 단위**로는 참이다. 그러나 그림을 담은 **판 전체(우리 OS 창)가 활성화되는 경로**는 따로 있다(W-2·macOS) |
| `Assets/_Project/Scripts/Interaction/UiChrome.cs` | `뒤에 있던 Finder 창이` | macOS 실측 1건의 원천(1-4). 조건 미기록 |
| 〃 | `키는 밑에 있는 남의 앱으로 간다` | **부류 K** — W-1에서 거짓(Windows · 갈래 조건은 위 부류 K 머리와 같다). 원 맥락은 macOS 09-02 실측이라 macOS에서는 1-4 (다) |
| `Assets/_Project/Scripts/Platform/IGlobalKeyStateService.cs` | `클릭으로 포커스를 줄 수 없다` | **부류 O**(주어 = 이 앱의 창). 커서가 콜라이더 위면 관통이 꺼져 클릭이 도착한다(E1 A-2 (가)) → W-2 활성화 때 거짓(현재 (다)) |
| `docs/marketing/STORE_PAGE.md` | `발표 중에도 방해하지 않습니다` | 이 앵커가 걸리는 줄은 §7 「쓰면 안 되는 문장(확정 금지 목록)」 표의 행이다 — **이미 금지된 문구이지 게시 중인 약속이 아니다.** 게시 문안이 있는 §2-7(절 제목 앵커 「전체화면 위에서 물러나되, 갇히지 않는다」)에는 이 앵커가 0회다. W-1을 금지 사유에 보탤지는 marketing 판단 |
| `docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md` | `라 키보드 포커스를 받을 수 없고` | 앞 낱말이 NOACTIVATE 스타일 이름이다. **현행 코드와 불일치**(E1 0절: 그 스타일 0건). 옛 설계 문서 |
| `docs/BUG_REPORT_PHASE0.md` | `가설 H2` | 옛 가설(포커스 탈취). 이력 문서라 참고만 |
| `docs/BUG_REPORT_PHASE1.md` | `특성상 OS로부터 키보드 포커스를 받` | 옛 가설 H6(NOACTIVATE 전제). 이력 문서라 참고만 — 「가설 H6」 낱말은 이 파일에 2회라 앵커로 쓰지 않았다 |

---

**Windows 영향**: 코드 변경 없음. W-1 트레이 경로는 **코드상 발생(가)** — 전경 요청과 복귀 코드 부재까지이고, 요청의 OS 성공 여부와 트레이 명령 뒤 Unity 창 활성화는 실기다. W-2 클릭 경로는 **실기 필요(다)**, 포커스 뒤 uGUI 재발동은 **조건부 코드상 발생**([지금 종료]는 클릭 1회 + Space 1회, 또는 한 번 길게 누르기 — R-7 리더 채택 1.0 필수). 대응 후보 중 N-8 다른 경로는 R-2뿐이다(`Win32WindowService.cs` A). 실기 미확인.
**macOS 영향**: 코드 변경 없음. 활성화 호출 0 · 패키지 키 창 유지 관찰자 실재 · 창 클릭 활성화 여부 **실기 필요(다)** · 포커스 뒤 uGUI 재발동과 [지금 종료] 길게 누르기는 Windows와 같다(설정창 코드는 플랫폼 중립). R-3·R-4·R-7은 양 플랫폼 공통이다(R-3·R-7은 리더 채택 1.0 필수). 실기 미확인.

## 출처

- 이 저장소: 본문 각 `파일:줄`(HEAD `ad49497`).
- 패키지 캐시: `com.kirurobo.uniwinc@304f9ba2aa4a` — `Runtime/Scripts/UniWindowController.cs`(`:904-915`·`:1205-1211`) · `Runtime/Scripts/LowLevel/UniWinCore.cs`(`:206`·`:535-540`) · `Runtime/Plugins/Windows/x64/LibUniWinC.dll` 임포트 표 · `Runtime/Plugins/MacOS/LibUniWinC.bundle` 심볼 / `com.unity.ugui@8bb446d869cd` — `StandaloneInputModule.cs`·`Selectable.cs`·`Button.cs`·`InputField.cs`.
- 업스트림 소스: `kirurobo/UniWindowController` 태그 `v0.9.8` `Xcode/LibUniWinC/LibUniWinC.swift`(이진과의 동일성 미확인 — 0절).
- Microsoft Learn: Keyboard Input Overview · WM_MOUSEACTIVATE · TrackPopupMenu Remarks / Microsoft Support: Use keyboard shortcuts to deliver PowerPoint presentations.
- Apple Developer Documentation: `NSApplication.keyWindow` · `NSApplication.ActivationPolicy.accessory` · `NSWindow.canBecomeKey` · `NSWindow.makeKey()`.

---

## 초판(미커밋) 대비 정정 기록

이 파일은 커밋된 적이 없다. `TEAM.md` §5(고정 문자열 앵커 `한 번도 커밋되지 않은 초판의 문장을 본문에 취소선으로 되살리지 않는다` — 작업 트리 전용 앵커: HEAD `ad49497`에서 0회 · 작업 트리 1회, TEAM.md 동반 커밋)에 따라 옛 문장은 본문이 아니라 이 절에 **취소선으로** 남긴다 — 리더 지시(「옛 문장은 취소선으로 남긴다」)와 정본 규칙을 함께 지키는 형태로 판단했다 — 리더 채택(2026-09-15): 정정 기록 절 방식.
초판 = sha256 앞 16자 `009182e1b3726153` · 1차 정정본 = `ff3107f3e1d5168b`(둘 다 verify-change 조건부 통과 대상).

| # | 초판 원문(요지) | 정정 | 근거 |
|---|---|---|---|
| 1 | 1-2 설정창 [지금 종료] 문장의 굵게 표시(닫는 기호가 `]`와 `는` 사이) | 닫는 기호를 `는` 뒤로 | verify-change 수정 1 |
| 2 | 1-3 W-1 영어 인용 기울임(닫는 기호가 `"`와 `이` 사이) | 괄호 안 인용으로 | 수정 2 |
| 3 | ~~약속 문구. W-1 사실과의 관계를 marketing이 재판정할 대상~~ | §7 금지 목록 표의 행 — 게시 약속 아님, §2-7 게시 문안에는 앵커 0회 | 수정 3 · STORE_PAGE 절 머리 대조 |
| 4 | ~~전칭 문장. W-1(Windows 트레이)에서 코드상 거짓~~(매뉴얼 두 문장 묶음) · ~~같은 전칭. W-1에서 코드상 거짓~~(UX_FLOW) | 부류 K(키 행방, W-1 거짓) / 부류 O(판·오버레이, W-2 활성화 때만 거짓)로 분리 | 수정 4 |
| 5 | ~~위젯 밖 비테스트: InfoGearIconWidget.cs 5곳~~ | 세는 규칙 명시 후 6줄 | 경미 · 재계수 |
| 6 | ~~위젯 파일 안의 Collapse( 호출 15건~~ · ~~위젯 파일 자신에서 15건~~ | 고정 문자열 `Collapse(` 줄 10개 = 정의 1 · 호출 7 · `TickAutoCollapse(` 2 | 경미 · 재계수 |
| 7 | ~~포커스가 숨은 호스트 창으로 감, 명령 뒤 복귀 없음~~ | 전경 요청 대상 — OS 성공 여부 실기 미확인 | 경미 · SetForegroundWindow Remarks |
| 8 | ~~넘기기 키는 발표 앱에도 우리 앱에도 닿지 않고 사라진다~~ | 발표 앱에 가지 않음만 유지, Unity 창 도달은 미확인(패키지 `ShowWindow`·`SetWindowPos` 임포트) + K-① 관찰 추가 | 경미 |
| 9 | ~~그 버튼이 선택된 채로 Space를 두 번 누르면 종료 경로에 닿는다~~(유일한 경우로 적음) | 더 나쁜 경우 「클릭 1회 + Space 1회」 추가(코드 직접 확인) | 경미 ★ |
| 10 | ~~선택 해제 코드는 0건~~ | 「우리 코드 기준」 한정 + uGUI 해제·비활성 창 사실 추가 | 경미 |
| 11 | ~~Scenes/Main.unity(목록 밖) · SettingsWindow.cs/CharacterInfoWindow.cs EnsureEventSystem(목록 밖)~~ | 생성 지점 6곳(씬 1 · 런타임 폴백 4 · 에디터 1), 기본값 참, N-8 목록 밖 재확인 | 경미 · 리더 판정 R-3 1.0 필수 |
| 12 | 5절 전칭 문장 목록에서 4곳 누락 | `IGlobalKeyStateService.cs` · `UiChrome.cs` 키 행방 · 매뉴얼 ⌘W 행 · UX_FLOW 온보딩 행 추가 | 경미 · `TEAM.md` 「같은 경로 전수」 |
| 13 | ~~경로는 따로 적지 않으면 Assets/_Project/Scripts/ 기준~~(예외 없음) | 씬·입력 설정·에디터·패키지 예외 명시, 해당 인용을 전체 경로로 | 경미 |
| 14 | 단일 물결표 2곳(0.35와 3초 사이 · 규칙 1과 6 사이) | 「초과·미만」 · EN DASH | 경미 |
| 15 | 0절 `v0.9.8` 굵게 · 2-2 `UnionScreenRect` 굵게(닫는 기호가 백틱과 한글 사이) | 닫는 기호를 한글 뒤로 | 형식 검사기 적발(verify-change 목록 밖) |
| 16 | ~~어느 것도 포커스를 돌려주지 않는다(돌려주는 API 호출 0)~~ (초판 1-3 메뉴 명령) | 「발표 앱에」 한정 · 「남의 창 활성화 호출 0」으로 축소 — Unity 창 활성화 가능성(패키지 임포트)을 분리 | 2차 재검증 수정 1 |
| 17 | ~~발표 앱은 다음 클릭 전까지 넘기기 키를 받지 못한다(코드상 발생)~~ (초판 4절 사유) | 코드상 발생 범위를 전경 요청·복귀 코드 부재로 축소, OS 성공 여부·트레이 명령 뒤 Unity 창 활성화는 실기 | 2차 재검증 수정 1 |
| 18 | ~~WM_ACTIVATE의 기본 처리가 포커스를 그 창에 준다~~ (초판 1-3, 무조건형) | 「받아들여지면」 조건형 | 2차 재검증 수정 1 |
| 19 | ~~씬 EventSystem m_sendNavigationEvents: 1이 없으면, 그리고 비테스트 코드에 sendNavigationEvents = false나 Navigation.Mode.None이 생기면 R-3 착지로 보고~~ (초판 4절 가드) | 생성 지점 6곳 전부를 보는 가드 | 2차 재검증 수정 1 경미 |
| 20 | ~~W-1에서는 키가 숨은 트레이 호스트 창(전경 요청 대상)으로 가므로 코드상 거짓~~ (1차 정정본 5절 부류 K와 그 행 2개) · ~~W-1에서 포커스는 다른 창(숨은 호스트)으로 가므로 거짓이 아니다~~ (부류 O) | 갈래 명시 — 요청 성공 시 호스트 창, 거부 시 셸, 어느 갈래든 발표 앱·판이 아니다(복귀 코드 0은 코드 사실, 갈래별 OS 동작은 실기 미확인). 결론 유지 | 2차 재검증 수정 2 · 부류 O는 같은 경로 전수로 함께 |
| 21 | ~~0.35초 초과~~ · ~~0.35초 넘게~~ (1차 정정본 1-2) | 「이상」 — `:1087`이 `<`로 거부하므로 경계값 0.35초는 통과 | 2차 재검증 경미 · 코드 직접 확인 |
| 22 | ~~리더 판정 대기~~ (이 절 머리) | 리더 채택(2026-09-15) · TEAM.md 앵커에 작업 트리 전용 표시 | 2차 재검증 경미 |
| 23 | 1-2 [지금 종료] 경로 목록에 「한 번 길게 누르기」 없음 | 검증 발견 반영(재현 · 조건 차이 2건) + R-7 + 1-6 안전 문구 | 2차 재검증 신규 발견 |
| 24 | ~~설정창 쪽 파일(SettingsWindow·SettingsControls·UiChrome)에 IDragHandler·ScrollRect·Scrollbar·uGUI Slider는 0이다~~ (1-2 (b) 근거) | [지금 종료] 조상 계층 기준 드래그 처리기 0 · `SettingsControls.cs:1444` InputField 명시 · R-3 행 「판정」→「채택」 표기 통일 | 3차 재검증 경미 · 리더 커밋 전 정확 일치 치환 |


---

## 6. 작업 표시줄 · Alt+Tab / Dock · ⌘Tab에 우리 이름이 뜨는가 — 코드 사실 조회 (2026-09-15 추가)

담당 `dev-platform` · 기준 `a8c3723` blob(비테스트 `.cs` 297개를 `git show`로 떠서 계수) · 코드 읽기만, 실기 0회. **이 절은 삽입만 했고 위 본문은 한 글자도 바꾸지 않았다.**
입력: `docs/strategy/CHANNEL_PRICING_DECISIONS.md` 59절의 한정형 문구 — 작업 트리 전용 앵커 `발표 중에도 작업 표시줄과 Alt+Tab에 우리 이름이 뜨지 않습니다`(HEAD `76504c2` 0회 · 작업 트리 1회, 그 파일은 작업 트리 `M`). 이 절을 처음 쓸 때 그 문구는 「사용 보류」였다. 지금 그 파일은 이 절의 판정을 근거로 리더 판정(2026-09-15) **「1.0 게시에서 쓰지 않는다」 판정**으로 닫았고(고정 문자열 `이 한정형은 1.0 게시에서 쓰지 않는다`, 작업 트리 1회 · HEAD 0회), 「Alt+Tab 목록에 뜨지 않습니다」 단독형은 실기 확인 전까지 보류다.

### 6-0. 세계별 판정

| 세계 | 작업 표시줄 버튼(창 목록) | 작업 표시줄 알림 영역 | Alt+Tab | 문구 판정 |
|---|---|---|---|---|
| Windows · 트레이 켬(기본) | **조건부** — `ITaskbarList::DeleteTab` 성공 전제(6-1 ②). 기동 직후 성공 전까지는 있다 | **아이콘이 뜬다.** 이름은 커서를 올릴 때 툴팁으로 뜬다(6-2) | **코드상 참** — 도구 창 비트(6-1 ①). 틈: 부착 감지 전 기동 직후 · 외부가 비트를 지운 뒤 최대 2초 | **조건부** — 「작업 표시줄」에 알림 영역을 포함하면 문구 그대로는 거짓(아이콘이 뜨고 툴팁에 이름이 있다). 버튼만 뜻하면 DeleteTab 성공 전제 |
| Windows · 트레이 끔(`STICKMATE_NO_TRAY_ICON`) | 같음(조건부) | 아이콘 없음. 세션 종료 때 할 일이 있을 때만 숨은 수신 창이 선다 — 이번 실행이 작업표시줄 설정을 바꿨거나 종료 표지가 시작됐을 때(`Platform/AppShutdownSequence.cs:464-465`, 1회만 평가 `:507-510`). 도구 창 비트, 한 번도 보이지 않음 | 코드상 참(같은 틈) | **조건부** — 버튼 제거 성공 전제 |
| macOS | 해당 없음(대응 표면은 Dock) | 해당 없음(accessory는 메뉴 막대가 없다 — Apple) | 해당 없음(대응 표면은 ⌘Tab) | Dock **코드상 참**(부착 뒤 accessory 전환, 기동 직후 틈) · ⌘Tab **미확인**(1차 문서 문장 없음, 코드 주석만 주장) |

- Windows 행은 전부 **실기 미확인**이다(이 머신에 Windows 없음).
- 「발표 중에도」: 전체화면 등급·사용자 숨김은 창 스타일·표시 상태·activation policy를 바꾸지 않는다. 우리 비테스트 코드의 `ShowWindow`는 전부 주석이고(6-1 ④), activation policy 쓰기는 부착 시 1회뿐이다(6-4). **발표 중이라서 달라지는 경로는 코드에 없다.**

### 6-1. Windows — 오버레이(Unity 메인) 창

**① Alt+Tab — `WS_EX_TOOLWINDOW`**
- 창 부착을 처음 감지한 프레임에 1회 `_toolWindowStyle.ApplyOnce()`(`Platform/Windows/WindowsOverlayStateEnforcer.cs:291`), 그 뒤 매 프레임 `Tick`(`:331`, 내부 2초 주기 `Platform/Windows/WindowsToolWindowStyleControl.cs:80`).
- 읽기 → 판정 → 대행 쓰기: `WindowsToolWindowStyleControl.cs:131-173` → `Platform/Windows/WindowsLayeredHybridResolver.cs:404-425` `TryAddExStyleBits`(비트를 켜기만 한다 — `:416` `ex | bitsToAdd`, `:419` 쓰기, `:420` 되읽기). 검증 `Platform/AppSwitcherPresencePolicy.cs:119-120` `VerifyApplied`. 비트 값의 단일 출처 `:74` `0x00000080`.
- 해소기 영구 비활성과 무관하다 — `TryAddExStyleBits`는 정적 함수이고 본문(`:404-425`)에 비활성 게이트가 없다.
- Microsoft(Extended Window Styles, `WS_EX_TOOLWINDOW`): *"A tool window does not appear in the taskbar or in the dialog that appears when the user presses ALT+TAB."*
- 이미 보인 창에 비트를 얹었을 때 Alt+Tab에서 **즉시** 빠지는지는 1차 문서 문장이 없다. 저장소 정책 `AppSwitcherPresencePolicy.cs:126-127` `TakesEffectOnAlreadyShownWindow`(Alt+Tab 참)는 판단이지 인용이 아니다 → **미확인(실기)**.
- 틈 두 개(코드상): (a) 기동 직후 부착 감지 전에는 비트가 없다. (b) 누가 비트를 지우면 다음 2초 틱까지 없다. 지우는 후보는 패키지 `detachWindow`의 **절대값 복원**이다(업스트림 태그 `v0.9.8` `VisualStudio/LibUniWinC/libuniwinc.cpp:82` `SetWindowLong(hTargetWnd_, GWL_EXSTYLE, originalWindowInfo_.dwExStyle)`. 부착 대상이 바뀔 때 `:98-101`, 명시 해제 `:549-550`). ★ **플레이어 빌드에서는 C#이 이 복원을 부르지 않는다** — 패키지 C#의 `DetachWindow()` 호출자는 `Runtime/Scripts/UniWindowController.cs:1195` 하나이고 `OnApplicationQuit`의 `#if UNITY_EDITOR` 안(`:1191-1196`)에 있으며, `Dispose`의 호출은 주석 처리돼 있다(`Runtime/Scripts/LowLevel/UniWinCore.cs:322-323`). 남는 후보는 네이티브 내부 재부착 경로(`attachWindow` `:98-101`)뿐이고, 그것이 같은 창에서 도는지는 **미확인**이다. 소스와 이진이 같은 판인지 미확인.

**② 작업 표시줄 버튼 — 스타일로는 안 빠지고 COM으로 지운다**
- Microsoft(The Taskbar, 「Managing Taskbar Buttons」): *"The Shell will remove a window's button from the taskbar only if the window's style supports visible taskbar buttons. If you want to dynamically change a window's style to one that does not support visible taskbar buttons, you must hide the window first (by calling ShowWindow with SW_HIDE), change the window style, and then show the window."* — 비트는 창이 이미 보인 뒤에 얹히므로 **스타일만으로는 버튼이 남는다.**
- 그래서 `Platform/Windows/WindowsTaskbarButtonRemover.cs`가 `ITaskbarList::DeleteTab(우리 창)`을 부른다(`:144`, 호출부 `WindowsOverlayStateEnforcer.cs:338`). 같은 문서 「Modifying the Contents of the Taskbar」: *"From an application, you can now add, remove, and activate taskbar buttons."*
- **조건(코드상)**: 환경변수 `STICKMATE_KEEP_TASKBAR_BUTTON`이 없다(`:78`·`:117-126`) · COM 생성과 `HrInit`이 성공한다(`:187-230` — 실패하면 `_unavailable`이 되어 버튼은 그대로이고 다시 시도하지 않는다) · `DeleteTab` 호출이 예외를 던지지 않는다(`:146-153` — 던지면 역시 `_unavailable`, 재시도 없음) · 시도 횟수는 **호출이 성공한 뒤에만** 오른다(`:155`) · 최대 **3회**, 2초 간격(`:83`·`:86`, 상한 판정 `AppSwitcherPresencePolicy.cs:144-151`).
  셸이 3회째 시도 뒤에 버튼을 만들면 남는다(주석 `:69-72` 「부착 직후 시점에 셸이 이미 버튼을 만들어 두었는가」) · Mono COM 상호운용이 플레이어에서 도는지(같은 주석) → 둘 다 **실기 미확인**.
- 3회가 끝난 뒤 버튼이 다시 생기는 경로(셸 재시작 `TaskbarCreated`, 위 ① (b) 틈 동안의 표시 전환)를 다시 지우는 코드는 없다. 셸이 도구 창 비트가 선 창에 버튼을 다시 만드는지는 **미확인**.
- `WS_EX_APPWINDOW`: 우리 비테스트 코드 **0파일** · 패키지 소스 **0회**(같은 파일 양성 대조 `WS_EX_LAYERED` 7회 · 음성 대조 0).

**③ 소유자 창 · `WS_EX_NOACTIVATE` · 초기 스타일**
- 소유자: Win32 `SetParent`(점 접두사가 없는 호출 · `extern` 선언 · `EntryPoint` 문자열, 주석 제외)와 `GWLP_HWNDPARENT`는 `a8c3723` 비테스트 `.cs`에 **0**이다. 참고로 고정 문자열 `SetParent` 전체는 147줄 / 36파일이고, 그중 `.SetParent`(Unity `transform.SetParent`)가 142줄 / 34파일, 나머지 5줄은 Unity `SetParent`를 설명하는 주석이다.
- 패키지 `SetParent`는 `SetBackground`(`libuniwinc.cpp:884-906`, 창을 바탕화면 뒤로 붙이는 기능)에만 있다. 그 기능을 켜는 `isBottommost` 쓰기는 우리 코드에 0파일이고, 씬 `Assets/_Project/Scenes/Main.unity`(`a8c3723`)와 패키지 프리팹 `Runtime/Prefabs/UniWindowController.prefab`의 bottommost 계수는 0이다(양성 대조: 씬 `_isTransparent` 1 · 프리팹 topmost 1). 필드 기본값은 `false`다(`UniWindowController.cs:175`).
- `WS_EX_NOACTIVATE`: 우리 비테스트 코드 2파일(`States/IMovementIntentSource.cs`·`States/StickmanBlackboard.cs`)이 모두 주석이고 패키지 소스 0회다(E1 0절과 같은 결과).
- Unity 플레이어가 만드는 창의 **초기** 확장 스타일과 소유자는 코드로 알 수 없다 → **미확인(실기)**.
- 패키지 `SetBorderless`(`libuniwinc.cpp:694-783`)는 우리 씬의 투명 설정(`Assets/_Project/Scenes/Main.unity` `_isTransparent: 1`)이 켜는 경로이고 안에서 `ShowWindow`를 부른다(`:713`·`:783`). 기동 시(비트 전)라면 버튼이 정상 생성되고 ②가 지운다. 비트가 선 뒤 투명 재적용으로 이 `ShowWindow`가 다시 돌 때의 셸 동작은 **미확인**.

**④ 우리 코드의 `ShowWindow`** — 비테스트 `.cs` 5개 파일 6줄이 **전부 주석**이다(`a8c3723`, 줄 머리 `//` 판정). 기각된 「ShowWindow 왕복」이 들어오지 않았음은 `AppSwitcherPresenceTests` `B3_기각된_ShowWindow_왕복이_들어오지_않았다`가 소스로 잠근다.

### 6-2. Windows — 트레이 호스트 창 · 알림 영역 아이콘

- **호스트 창**: `Platform/Windows/WindowsSystemTrayIcon.cs:471` `EnsureHostWindow` → `:491-497` `CreateWindowEx`.
  - 확장 스타일은 도구 창 비트(`AppSwitcherPresencePolicy` 상수)다.
  - 스타일은 `WS_POPUP`이고 `WS_VISIBLE`이 없다.
  - 크기는 0이고 부모는 `IntPtr.Zero`다.
  - `HWND_MESSAGE` 0회 — 메시지 전용 창이 아니라 **숨은 최상위 창**이다. 표시 호출 0(위 ④).
  - ⇒ 버튼·Alt+Tab 없음(도구 창이고 한 번도 보이지 않음 — 위 Microsoft 규칙, 실기 미확인). 창 제목 문자열 `StickMate`는 보이지 않는 창의 제목이다.
- **트레이 끔**: `EnsureSessionEndReceiverWithoutTray`(`:302`) → `CreateSessionEndReceiverWindow` → **같은** `EnsureHostWindow` — 아이콘 없이 같은 숨은 창만 선다.
- **알림 영역 아이콘**: `Shell_NotifyIcon(NIM_ADD)`(`:349`), 툴팁 `Platform/SystemTrayPresencePolicy.cs:75` `StickMate — 우클릭: 메뉴 (종료 · 숨기기 · 설정)`.
  - Microsoft(The Taskbar, 「About the Taskbar」): 작업 표시줄 구성 목록에 *"Taskbar buttons"* · *"Notification area"* 가 함께 있다 — **알림 영역 아이콘은 작업 표시줄의 일부다.**
  - 이름은 아이콘에 커서를 둘 때 툴팁으로 뜬다. 아이콘이 기본으로 숨은 아이콘 영역(오버플로)에 들어가는지는 **미확인**이다.
- 트레이 메뉴 경로(1-3 W-1)에서 호스트 창이 전경이 돼도 도구 창이라 Alt+Tab에는 뜨지 않을 것으로 판단한다 — 미확인.
- **구분**: 「작업 표시줄에 이름이 뜬다」를 **버튼**(창 목록)으로 읽으면 트레이 아이콘은 해당하지 않는다. **작업 표시줄 전체**로 읽으면 알림 영역 아이콘과 그 툴팁이 해당한다. 문구가 어느 쪽인지는 marketing·product-strategy 판단이다.

### 6-3. Windows — 우리 UI는 OS 창인가

- 설정창·정보창·포스트잇·부채꼴·팝오버·말풍선은 **Unity 창 안의 캔버스**다: `SettingsCanvas` `Interaction/SettingsWindow.cs:1181` · `CharacterInfoCanvas` `Interaction/CharacterInfoWindow.cs:1357` · `TodoPostItCanvas` `Interaction/TodoPostItWidget.cs:1116` · `GearRadialMenuCanvas` `Interaction/GearRadialMenuWidget.cs:2348` · 팝오버 `Interaction/PopoverPanel.cs:1004` · 말풍선 `Dialogue/DialogueBubbleRenderer.cs:1888`(고정 문자열 `typeof(Canvas)` — `a8c3723` 비테스트 `.cs` **6줄 / 6파일**).
- 우리 코드의 `CreateWindowEx` **호출**은 트레이 호스트 1곳뿐이다. 나머지 두 파일은 주석이다(`Platform/ILocalClickCaptureService.cs:35` · `Platform/Windows/Win32WindowService.cs:44`). ⇒ 우리 UI가 새 작업 표시줄 버튼·Alt+Tab 항목을 만들 경로는 코드에 없다. macOS 실측 창 1장(`docs/UX_FLOW.md` 앵커 `OS 창이 아니므로`)과 같은 구조다.

### 6-4. macOS — Dock · ⌘Tab

- **활성화 정책**: 창 부착을 처음 감지한 프레임에 1회 `MacSpaceBehaviorNative.ApplyAccessoryActivationPolicyOnce()`(`Platform/MacOS/MacOverlayStateEnforcer.cs:279`) → `setActivationPolicy:` accessory(1)(`Platform/MacOS/MacSpaceBehaviorNative.cs:94` · `:205-228`). 되읽어 확인한다. 되읽은 값이 accessory가 아니면 `Debug.Log`로 「전환 실패」 한 줄(`:230`)을 남기고, 예외나 앱 객체 획득 실패는 `Debug.LogWarning` 1회(`LogFailureOnce` `:304-308`)를 남긴다. 어느 쪽이든 일반 앱으로 남는다.
- **`LSUIElement`**: `Assets/Editor/` 빌드 코드와 `ProjectSettings/ProjectSettings.asset`에 **0회**다. 비테스트 `.cs`에서 걸린 3곳은 전부 주석이다(`MacSpaceBehaviorNative.cs` 1 · `MacWindowService.cs` 2). Info.plist를 만지는 후처리 `Assets/Editor/MacHybridGpuInfoPlistPostprocessor.cs`에도 이 키가 없다.
  ⇒ **기동 시에는 일반 앱으로 떴다가 부착 뒤 accessory로 내려갈 것으로 판단한다**— 빌드 산출물 실측: `Builds/macOS/StickMate.app/Contents/Info.plist`(2026-09-09 13:32, 같은 앱의 `StickMate.Runtime.dll` 09-09 13:19 — **`a8c3723`(09-15)보다 이전 빌드**)에 `LSUIElement` 키 **0**이다(`plutil`로 XML 변환 뒤 `<key>` 고정 문자열 계수. 양성 대조 `CFBundleIdentifier` 1 · `NSSupportsAutomaticGraphicsSwitching` 1 · 음성 대조 없는 키 0). `a8c3723` 빌드 산출물은 재지 않았다.
- Apple(`NSApplication.ActivationPolicy.accessory`): *"The application doesn’t appear in the Dock and doesn’t have a menu bar, but it may be activated programmatically or by clicking on one of its windows."*
- Apple(`LSUIElement`): *"A Boolean value indicating whether the app is an agent app that runs in the background and doesn’t appear in the Dock."*
- **⌘Tab**: 위 두 1차 문서 문장에 없다. 코드 주석(`MacSpaceBehaviorNative.cs:28` 「Dock 아이콘과 Cmd-Tab 목록에서 사라진다」)만 주장한다 → **미확인(1차 문서·실기)**.
- **틈**: 기동 직후 부착 감지 전 Dock 아이콘(위 판단) — 길이는 미확인이다.

### 6-5. 이 사실을 잠그는 테스트 (`a8c3723`, 선언 기준)

- `AppSwitcherPresenceTests` 14개(A1–C1): 비트 값, 다른 비트 보존, 읽기 실패 시 쓰기 0, 「버튼은 스타일만으로 안 사라진다」, DeleteTab 상한, 부착 시점 대칭, ShowWindow 왕복 부재, COM은 DeleteTab 하나. **전부 소스·순수 규칙 검사다.**
- `PlatformParityAuditTests` `앱전환기_제외가_양_플랫폼에_대칭으로_배선되어_있다` · `작업표시줄_버튼_제거는_창_상태를_건드리지_않는_경로로만_한다`.
- 실제로 버튼·Alt+Tab·Dock·⌘Tab에서 빠지는지 재는 테스트는 없다(실기 영역).

### 6-6. 연결과 실기 문안 (이 절은 아래 파일을 수정하지 않았다)

- 같은 문장이 걸리는 파일(고정 문자열 계수, `a8c3723` / 작업 트리): `docs/strategy/CHANNEL_PRICING_DECISIONS.md` **0 / 1**(작업 트리에만) · `docs/strategy/ROADMAP.md` 1 / 1 · `docs/marketing/ROADMAP.md` 1 / 1 · `docs/marketing/TRUTH_INVENTORY.md` 1 / 1 · `docs/marketing/CAPTURE_REQUESTS_R6.md` 1 / 1.
- 실기 확인 문안(체크표 반영은 리더 판단): 기동 30초 뒤에 본다.
  - (a) 작업 표시줄 버튼 유무
  - (b) Alt+Tab 목록에 StickMate 유무
  - (c) 알림 영역·오버플로에서 아이콘 위치
  - (d) 로그 `[전환기제외]`의 `부여` 또는 `이미 서 있음` 줄, `[작업표시줄버튼] ITaskbarList.DeleteTab 호출 성공` 줄
  - macOS: Dock·⌘Tab에 StickMate 유무와 로그 `activation policy 전환 성공` 줄

**Windows 영향**: 코드 변경 없음. Alt+Tab 제외는 코드상 참(부착 전·외부 삭제 뒤 최대 2초 틈), 작업 표시줄 버튼 제거는 DeleteTab 성공 전제의 조건부, 트레이 켬이면 알림 영역 아이콘과 이름 툴팁이 뜬다. 실기 미확인.
**macOS 영향**: 코드 변경 없음. Dock 제외는 부착 뒤 코드상 참(기동 직후 틈), ⌘Tab 제외는 1차 문서가 없어 미확인. 실기 미확인.

### 6-7. 6절 정정 기록 (미커밋 삽입분 제자리 교체 · 2026-09-15)

6절은 `a8c3723` 뒤에 삽입한 미커밋 부분이라 제자리에서 고쳤다(1–5절 커밋판 글자 삭제 0). `TEAM.md` §5 형식(요지 → 정정 → 근거)으로 적는다. 첫 삽입본 sha 앞 16자 `9208a2c835cd45f3`.
근거 번호는 `Tasklist.md` 「[verify-change] `FOREGROUND_INPUT_FACTS.md` 6절 삽입 — 조건부 통과」 기록의 순서다 — 틀린 계수 ①②, 경미 1–7. 이 표의 형식(항목별 근거 번호 · 교체 표기)은 델타 재검증 C1(리더 판정)으로 맞췄다.

| # | 첫 삽입본 요지 | 정정 | 근거 |
|---|---|---|---|
| 1 | 6-1 ③ 「우리 비테스트 코드에 `SetParent`·`GWLP_HWNDPARENT` 0파일」(계수 기준 없음) | 기준 명시: Win32 형태 0 · 문자열 147줄 / 36파일 · `.SetParent` 142줄 / 34파일 | 틀린 계수 ① |
| 2 | 6-3 「`typeof(Canvas)` 비테스트 5곳」 | 6줄 / 6파일(말풍선 캔버스 누락) | 틀린 계수 ② |
| 3 | 6-1 ② `DeleteTab` 재시도 조건에 COM 생성 실패만 적음 | 「호출 예외 → 재시도 없음」·「시도 계수는 성공 뒤에만」 추가 | 경미 1 |
| 4 | 6-1 ① 「같은 창을 다시 붙일 때 이 복원이 도는지는 판독하지 않았다」 | **교체**: 플레이어 빌드에서는 C#이 부르지 않는다(`#if UNITY_EDITOR` 안 호출 1곳 · `Dispose`의 호출은 주석), 남는 후보는 네이티브 재부착 경로뿐(미확인). 6-1 ③에 씬·프리팹 bottommost 0 추가 | 경미 2 |
| 5 | 6-4 「Unity 기본 Info.plist에 이 키가 없다는 것은 미확인」 | `a8c3723` 이전 빌드(09-09) Info.plist 실측 0으로 교체 | 경미 3 |
| 6 | 6-6 표제 「(작업 트리)」와 `docs/marketing/ROADMAP.md`에만 붙은 「(`a8c3723`에도 1회)」 | **교체**: 「`a8c3723` / 작업 트리」 계수로 통일 — 작업 트리에만 있는 것은 CHANNEL(0 / 1) 하나 | 경미 4 |
| 7 | 6-0 트레이 끔 행 「세션 종료 수신용 숨은 창만 선다」 | **교체**: 세션 종료 때 할 일이 있을 때만(이번 실행이 작업표시줄 설정을 바꿨거나 종료 표지 시작 · 1회 평가) 숨은 수신 창이 선다 | 경미 5 |
| 8 | 6-4 Apple 인용의 곧은 아포스트로피 3곳 | 원문 둥근 따옴표 | 경미 6 |
| 9 | 6-4 「되읽어 확인하고, 실패하면 경고 한 줄을 남긴 뒤 일반 앱으로 남는다」 | **교체**: 되읽기 실패는 `Debug.Log`(`:230`), 예외·앱 객체 획득 실패는 `Debug.LogWarning` 1회(`:304-308`) | 경미 7 |
| 10 | 절 머리 입력 줄 「CHANNEL의 사용 보류 문구」(`a8c3723` 기준 계수) | **교체**: CHANNEL 59절의 현재 상태(리더 판정 「1.0 게시에서 쓰지 않는다」, Alt+Tab 단독형은 실기 전 보류)와 HEAD `76504c2` 기준 계수 | strategy 검증 경미 ③ |
