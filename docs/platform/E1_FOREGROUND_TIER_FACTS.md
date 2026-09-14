# E-1 등급 1 · 전경 창 판정 — 코드 사실 조회 (A·B·C)

> ★ 2026-09-15 리더: 이 문서의 `AppControlDirector.cs` 줄 번호는 작성 당시 작업 트리 = **커밋 `e6b14c2` 기준**이다(`eb4670d` 기준으로는 `:920` → `:844`, `:985` → `:894`).

담당 `dev-platform` · 2026-09-15 · 기준 HEAD `eb4670d` · **코드 읽기만 했다. 실기 0회, 빌드·Unity 실행 0회, 코드 변경 0줄.**
경로는 따로 적지 않으면 `Assets/_Project/Scripts/` 기준. 줄 번호는 HEAD 작업 트리 기준이다(`Platform/`·`Core/StickmanAgent.cs`는 HEAD 대비 미수정 — `git status --short` 빈 출력, 같은 명령이 `Interaction/`에서는 `M` 2줄을 내 경로가 살아 있음을 확인).

입력: `persona-stress`(재현)가 `eb4670d` 코드 판독에서 올린 플랫폼 주장 3건. 판정은 확정 / 기각 / 판정 불가로 가른다.
**이 문서에서 코드 인용이 없는 주장은 전부 「실기 미확인」이다. 이 개발 머신에는 Windows가 없다.**

---

## 0. 측정 방법과 대조 — 죽은 프로브 기록

| 측정 | 결과 | 양성 대조 | 음성 대조 |
|---|---|---|---|
| `WS_EX_NOACTIVATE` 이름, 비테스트 `.cs` 전체 | `Platform/` **0건**. 전체에서는 `States/IMovementIntentSource.cs`·`States/StickmanBlackboard.cs` **주석 2건뿐** | 같은 명령이 그 주석 2건을 잡는다 | — |
| 수치형 `0x08000000`(접미 `L`/`U`/`UL` 허용) | **0건** | 같은 형태 `0x00080000L`(`WsExLayered`)이 `Platform/Windows`에서 3건 | — |
| `WM_MOUSEACTIVATE`·`MA_NOACTIVATE`·수치 `0x21` | `Platform/` **0건** | (위 두 줄과 같은 명령 형태) | — |
| 패키지 네이티브 DLL 임포트 표(`strings -a` + 정확 일치) | `SetForegroundWindow`·`SetActiveWindow`·`SetFocus`·`BringWindowToTop` **0건** | `DwmExtendFrameIntoClientArea`·`SetWindowLongPtrW`·`SetWindowLongW`·`CallWindowProcW`·`DefWindowProcW`·`SetLayeredWindowAttributes`·`SetWindowPos` 각 1건 | 존재하지 않는 이름 0건 |
| `[전체화면판정]` 리터럴 위치 | 비테스트 10곳(아래 A-5) | — | 가짜 태그 0건 |
| DCP 소비자 식별자 정규식(`DISPLAY_CHANGE_PATH_FILES.md` ②(다) 2)과 같은 식·같은 주석 제거) | C절 후보 14파일 **전부 0** | `Core/StickmanAgent.cs`(B) 4종 · `Interaction/RunawayDirector.cs`(B) 1종 | `Platform/FootholdPoller.cs`(X) 0 |

**자백 2건**
1. 수치형 검색 첫 판은 `0x0?8000000\b`였다. `0x08000000L`에서 `0`과 `L`이 둘 다 낱말 문자라 `\b`가 성립하지 않는다 — **접미 `L`이 붙은 상수를 구조적으로 못 보는 죽은 프로브**였고, 양성 대조도 다른 형태로 잡아 그 결함을 가리지 못했다. 접미를 허용한 식과 **같은 형태의 양성 대조**로 다시 쟀다(위 표).
2. `ls -d Library/PackageCache/*uniwindow*`가 zsh에서 `no matches found`를 냈다 — 실제 폴더명은 `com.kirurobo.uniwinc@…`라 **글롭 문자열이 틀린 것**이지 패키지가 없는 게 아니었다. `ls | grep`으로 다시 찾았다.

---

## A. H1 가설 — 「우리 창 클릭 → 전경 = 우리 → 등급 1 풀림 → 브라우저 재클릭 → 상승 에지 → 열어 둔 창이 늦게 걷힘」 (Windows 전용)

### A-1. 인용 줄 대조

| 재현의 인용 | 실재 | 코드 사실 |
|---|---|---|
| `Platform/Windows/Win32WindowService.cs:2048` 전경 기준 | **참** | `IntPtr fg = GetForegroundWindow();` — 창 목록을 훑지 않는 **단일 조회**다 |
| `:2054` 전경이 우리 오버레이 → 전체화면 아님 | **참** | `if (fg == _overlayHwnd)` → `coversDisplay=false`로 `return`. `_overlayHwnd`는 `:1388` `Process.MainWindowHandle` |
| `:2063` 같은 프로세스 | **참** | `if (fgPid == _currentProcessId)` → 같은 결과. `_currentProcessId`는 생성자 `:564`의 자기 pid |
| `:1927` 디바운스 1.0초 | **참** | `FullscreenVerdictHoldSeconds = 1.0`. 단 **실제 지연은 이 값만으로 정해지지 않는다**(A-4) |
| `Platform/Windows`에 `WS_EX_NOACTIVATE` 0건 | **참** | 0절 표. 수치형 0건 |
| UniWindowController `OnApplicationFocus` 전제 | **참** | 패키지 `Runtime/Scripts/UniWindowController.cs:904-915` — 포커스를 받는 순간 클릭 관통을 강제로 끈다. **창이 포커스를 받는 것을 정상 사건으로 다룬다** |
| `Core/StickmanAgent.cs:442-448` 임대 만료 | **참** | `TickPanelRetreatEntry` — `_fullscreenPanelRetreat`가 false→true인 폴링에서만 `ExpireUserSummonGrant` |
| `Platform/MacOS/MacWindowService.cs:1476-1477` layer 0만 봄 | **줄 어긋남, 결론은 참** | layer 0 필터는 **`:1458`**(`layer != 0` → `continue`). `:1477-1478`은 알파 거부권이다 |
| 「1~2.5초 늦게」 | **수치 교정** | 코드 산술로는 **약 1.5~3.0초**(A-4) |

### A-2. 우리 레이어드 창이 클릭으로 활성화될 수 있는가

**(가) 우리 창이 마우스 메시지를 직접 받는 조건 — 코드상 있다.**
- `Win32WindowService.cs:1449-1453` `SetClickThrough(true)` → `isClickThrough=true` **그리고 `isHitTestEnabled=true`**.
- 패키지 `UniWindowController.cs:626-649` `UpdateClickThrough`가 매 프레임 커서 아래를 판정해, 걸리면 `SetClickThrough(false)`로 관통을 **끈다**.
- 판정 방식은 씬 값 `Assets/_Project/Scenes/Main.unity:434-436` `hitTestType: 2` = `Raycast`(열거 `UniWindowController.cs:61-66`) →
  `HitTestByRaycast`(`:797-`)가 uGUI `RaycastAll` + `Physics.Raycast` + **`Physics2D.GetRayIntersection`** 순으로 본다.
- 네이티브 `SetClickThrough(FALSE)`는 `WS_EX_TRANSPARENT`만 지운다(업스트림 소스, `Platform/LayeredHybridPolicy.cs:17-21`의 인용과 같다).
- ⇒ **커서가 캐릭터 콜라이더나 열린 창(정보창·설정창·부채꼴의 uGUI) 위에 있으면 그 물리 클릭은 우리 창에 도착한다.**

**(나) 활성화를 막는 장치 — 우리 코드에도 패키지에도 없다.**
- 우리 코드: `WS_EX_NOACTIVATE` 0 · `WM_MOUSEACTIVATE` 처리 0. 우리가 우리 창에 쓰는 확장 스타일은 `WS_EX_TOOLWINDOW` **추가**(`Platform/Windows/WindowsToolWindowStyleControl.cs:171`)와 `WS_EX_LAYERED` 떼기·복원(`WindowsLayeredHybridResolver.cs:367`·`:419`)뿐이다. 둘 다 활성화와 무관하다.
- 패키지 네이티브(업스트림 `main` 브랜치 `libuniwinc.cpp`): 서브클래스 창 프로시저가 처리하는 메시지는 `WM_DROPFILES`·`WM_DISPLAYCHANGE`·`WM_WINDOWPOSCHANGING`·`WM_STYLECHANGED`·`WM_SIZE` 다섯뿐이고 나머지는 원래 프로시저로 넘긴다. `WM_MOUSEACTIVATE` 미처리, `WS_EX_NOACTIVATE` 미설정, 활성화 API 호출 0.
  ★ 그 소스가 잠금 해시 `304f9ba2…`의 DLL과 같은 판인지는 **미확인**이다. 다만 **DLL 실물의 임포트 표**에도 활성화 API가 0이다(0절) — 이것은 판과 무관한 이진 사실이다.
- OS 문서(Microsoft Learn, Extended Window Styles): `WS_EX_NOACTIVATE` — *"A top-level window created with this style does not become the foreground window when the user clicks it."* ⇒ 이 스타일이 **없는** 최상위 창은 클릭하면 전경이 되는 것이 기본이다.

**(다) 코드로 판정할 수 없는 한 곳** — **Unity 플레이어의 원래 창 프로시저**(엔진 내부, 소스 없음)가 `WM_MOUSEACTIVATE`에 `MA_NOACTIVATE`를 돌려주는지. 패키지 서브클래스가 이 메시지를 원래 프로시저로 넘기므로 최종 결과는 엔진이 정한다.

**(라) 명시적 전경 전환이 1곳 더 있다** — `Platform/Windows/WindowsSystemTrayIcon.cs:590` `SetForegroundWindow(_hostWindow)`(KB135788 관용구, 승인된 예외). 트레이 메뉴를 띄우는 동안 **우리 프로세스의 숨은 호스트 창이 전경**이 되어 `:2063` 분기를 탄다. 메뉴를 약 1.5초 넘게 열어 두면 같은 형태가 난다.

**(마) 입구 자체는 활성화를 만들지 않는다** — 우클릭 감지는 전역 폴링이다(`Interaction/AppControlDirector.cs:920` `TryGetSecondaryButtonPressed` → `Win32WindowService.cs:1519` `GetAsyncKeyState`). `⌃⌥⌘I`도 같은 `GetAsyncKeyState`다. 활성화를 만드는 것은 **커서가 캐릭터·창 위에 있을 때의 물리 클릭**이다. E-1 흐름(우클릭 → 부채꼴 → 정보창 → [설정] → 설정창 조작)은 전부 그런 클릭이다.

**판정 (2): 코드상 활성화 경로가 있다** — 막는 장치가 0이고 OS 기본 동작이 활성화다. 엔진 창 프로시저의 `WM_MOUSEACTIVATE` 처리만 **판정 불가**이며 실기로만 확정된다.

### A-3. macOS 대조 — 코드상 같은 형태가 없다

- `MacWindowService.cs:1440-1550` `EvaluateFullscreen`은 **활성 앱이 아니라** `CGWindowListCopyWindowInfo` 앞→뒤 순서에서 **첫 layer 0 창**을 본다(`:1458`).
- 우리 오버레이는 `SetAlwaysOnTop(true)` → `NSFloatingWindowLevel`(`:1320-1334`)이라 layer 0이 아니다. 비테스트 코드의 `SetAlwaysOnTop(false)` 호출은 **0건**이다(양성 대조: `SetAlwaysOnTop(true)` 4건).
- ⇒ 우리 앱이 클릭으로 활성화돼도 첫 layer 0 창은 여전히 전체화면 앱이다. 등급이 흔들릴 경로가 코드에 없다.
- 미확인: 플로팅 레벨의 실제 `kCGWindowLayer` 값, 네이티브 전체화면 Space에서 목록 순서.

### A-4. H1이 참일 때의 시간 산술 (코드 산술, 실기 미확인)

- 폴링 주기 P = **1.5초**(`Core/StickConfig.cs:1039` 기본값, `Assets/_Project/Data/DefaultStickConfig.asset:116` `fullscreenPollInterval: 1.5`). 타이머는 넘으면 0으로 리셋한다(`StickmanAgent.cs:1547-1550`).
- 디바운서(`Platform/FullscreenSuspendPolicy.cs:470-502`): 후보를 처음 본 시각에서 1.0초 이상 지난 **다음 폴링**에 확정한다. P ≥ 1.0이므로 **두 번째 관측에서 확정**된다. 기하 축은 별도 디바운서다(`Win32WindowService.cs:1956`).
- ⇒ 전경 변화 → 등급 확정 지연 ∈ **(P, 2P] = 약 1.5~3.0초**(+프레임 1개).
- ⇒ H1이 **발현하려면** 우리 창이 **폴링 두 번에 걸쳐** 전경이어야 한다. 약 1.5초 미만 체류는 흡수될 수 있고, 3.0초 이상이면 반드시 발현한다(활성화가 일어난다는 전제). 설정창 조작은 보통 이보다 길다.
- 걷힘 사슬: 상승 에지 폴링 `:1606` → `:447` 임대 0 → `Interaction/SettingsWindow.cs:760` 갱신은 **만료된 임대를 되살리지 않는다**(`Platform/UserSurfaceSummonPolicy.cs:104-105`) → `:767-772` `Close` / `Interaction/CharacterInfoWindow.cs:1034-1036` `Close`.
  세 클래스에 `[DefaultExecutionOrder]`가 없어서, 닫힘이 같은 프레임에 나는지 다음 프레임에 나는지는 정해져 있지 않다.
- ★ **관측 가능한 차이**: H1이 없으면 브라우저를 다시 눌러도 등급이 계속 1이라 상승 에지가 없고, 설정창은 임대 갱신으로 **남는다**. 설정창은 창 밖 클릭으로 닫히지 않는다(`SettingsWindow.cs:663-664` 로그 문구). H1이 있으면 **브라우저를 누르고 약 1.5~3초 뒤에 닫힌다**. 로그 없이 눈으로도 갈린다.
  이 동작이 결함인지 허용인지는 **이 문서가 판정하지 않는다**(UX·리더 판단).

### A-5. ★ 실기에서 H1을 판독할 실재 로그 문자열

**있다.** 등급이 바뀌는 폴링에서만 한 줄 찍힌다. 형식은 `Win32WindowService.cs:1975`:
`$"[전체화면판정] {ForeignFullscreenTierPolicy.Describe(tier)} — {reason}"`

**H1 서명 — 둘 중 하나가 등급 1 줄 뒤에 나오면 H1 발현 확정:**

```text
S1  [전체화면판정] 등급 0 — 남의 전체화면 앱 없음 — 전경 창이 우리 오버레이 자신이라 '다른 전체화면 앱'이 아님.
S2  [전체화면판정] 등급 0 — 남의 전체화면 앱 없음 — 전경 창이 우리 프로세스(pid <숫자>)의 다른 창이라 '다른 전체화면 앱'이 아님.
```
- 조립 근거: 머리 `Win32WindowService.cs:1975` · 등급 0 설명 `Platform/FullscreenSuspendPolicy.cs:316` `"등급 0 — 남의 전체화면 앱 없음"` · 사유 S1 `Win32WindowService.cs:2056` · 사유 S2 `:2065`(`$"전경 창이 우리 프로세스(pid {fgPid})의 다른 창이라 '다른 전체화면 앱'이 아님."`).
- 문자 실측: `—`는 **U+2014 EM DASH**(앞뒤 공백 1칸), `'`는 **U+0027**.
- S2가 나는 경우: `_overlayHwnd`(`MainWindowHandle`)가 실제 전경 HWND와 다를 때, 또는 트레이 메뉴 호스트 창(A-2 (라)).

**판독 규칙 (qa-regression이 F-4에 넣을 때 그대로 쓸 것)**
1. **부분 문자열로 센다** — `전경 창이 우리 오버레이 자신이라` / `전경 창이 우리 프로세스(pid`. EM DASH·따옴표를 검색식에 넣지 마라.
2. PowerShell은 **`Get-Content -Encoding UTF8`**(이 문서 `WINDOWS_CHECK_SESSION.md:308` 방식)로 읽고, **`Select-String -SimpleMatch`**를 쓴다. 기본 정규식에서는 `(`가 메타 문자다. 인코딩을 빼면 한글이 깨져 **0건 = 죽은 프로브**다.
3. **양성 대조**: 같은 로그에서 `[전체화면판정] 등급 1(패널 회수)`가 **1건 이상**이어야 한다. 0이면 F11 전체화면이 아니었거나 읽기가 죽은 것이라 **판독 무효**다.
4. **흔들림 로그는 H1에서 찍히지 않는다(음성 기대)** — `:1964` `원시 판정이 …로 흔들렸지만`은 `raw = 기하 && 게임` 축 전용이다. 게임이 아닌 앱에서는 raw가 항상 false라 나오지 않는다. 이 줄이 없다고 H1을 기각하지 마라.
5. **한계**: 사유는 **확정한 폴링의** 전경 창이다. 확정 폴링 전에 사용자가 다른 일반 창으로 옮겼으면 S1/S2 대신 `판정 근거 창 = pid …, … -> 기하 일치=false.`가 찍힌다. **우리 창 안에 3초 이상 머문 뒤** 브라우저로 가야 서명이 확정적으로 남는다.

**H1 발현 시 기대 순서** (근거 줄):
```text
1 [전체화면판정] 등급 1(패널 회수) —                                   ← Win32:1975 + Policy:313  (F11)
2 [표면회수] 등급 1 중 사용자가 표면을 직접 불렀습니다(캐릭터 우클릭)     ← StickmanAgent:401
3 S1 또는 S2                                                          ← ★ H1 확정 증거
4 [전체화면판정] 등급 1(패널 회수) —                                   ← 브라우저 재클릭
5 [표면회수] 사용자 표면 허가를 만료시켰습니다 — 등급 1 진입 — 이미 떠 있던 표면은 예외 없이 걷는다.   ← StickmanAgent:430 + :447
6 [설정창] 닫힘(전체화면 감지 — 자동 숨김(비침해 원칙 2)).  또는  [정보창] 닫힘(전체화면 감지 — 자동 숨김(비침해 원칙 2)).
                                                                     ← SettingsWindow:682+:772 / CharacterInfoWindow:993+:1036
```
- **H1 기각 증거**: 규칙 3이 유효하고 설정창 안에서 5초 이상 조작했는데 3이 **0건**이며, 2 뒤에 `[전체화면판정]` 줄이 더 없다.
- 2 → 3 사이에 `[표면회수] 등급 1 해제` 줄은 **나오지 않는 것이 정상**이다(임대가 살아 있어 `ArePanelsSuppressed`가 false→false, `StickmanAgent.cs:1698-1700`).

**F-4에 넣을 확인 단계 문안** (qa-regression 적용분, 이 문서는 `WINDOWS_CHECK_SESSION.md`를 고치지 않았다):
> ③ (①에서 부채꼴이 열렸으면) 부채꼴에서 정보창 → [설정]을 연다 → 설정창 **안**을 5초 동안 몇 번 누른다(값은 바꾸지 않아도 된다) → 브라우저 화면(설정창 밖)을 **한 번** 누른다 → 5초 기다린다.
> **본다**: 설정창이 몇 초 뒤 **저절로 닫히는가**. **적는다**: `F-4 ③ 닫힘(약 N초) / 남음 / 무효` + 로그 `전경 창이 우리 오버레이 자신이라` 개수 · `전경 창이 우리 프로세스(pid` 개수 · `허가를 만료시켰습니다` 개수 · (양성 대조) `등급 1(패널 회수)` 개수.
> **무효**: 양성 대조 0 · ①이 안 열림.

**최소 추가안(구현 금지)**: **필수 추가 없음** — 판독용 문자열이 이미 실재한다.
선택안 1건만 적는다. 기하 축(등급 1)에는 「흔들렸지만 흡수」 로그가 없어서 **흡수된 짧은 체류**(1.5초 미만)가 로그에 흔적을 남기지 않는다. 필요하면 `Win32WindowService.cs:1959-1967` 옆에 `rawCovers` 전용 같은 형태의 한 줄을 두는 안이 있고, macOS `:1391` 쪽도 같이 넣어야 한다. 이번 판독에는 불필요하다.

### A-6. 판정과 다음 조치 문안

- **(1) 인용: 확정.** 7건 중 6건은 그대로 참이고, macOS 인용 1건은 줄만 어긋났다(`:1458`, 결론 불변). 지연 수치는 1~2.5초가 아니라 **약 1.5~3.0초**로 교정한다.
- **(2) 활성화 경로: 코드상 있음 · 발현은 판정 불가(실기).** 막는 장치 0, OS 기본 동작은 활성화, 엔진 창 프로시저만 미확인이다.
- **(3) 판독 문자열: 실재(S1/S2).**
- **다음 조치 문안**: 「H1은 코드상 성립 조건이 전부 참이고 발현은 실기 1회로 갈린다. `WINDOWS_CHECK_SESSION.md` F-4에 ③ 단계를 추가해 S1/S2 개수를 받는다. **수정 착수는 실기 결과 뒤**다.」
- 수정이 필요해질 때의 후보(구현 금지, 리더 판단용):
  - (a) 전경이 우리 창/우리 프로세스일 때 **직전 확정 등급을 유지**(판정 보류). 규칙은 `Platform/FullscreenSuspendPolicy.cs` 중립 위치에 두고, macOS `IsOwnAppWindow` 분기(`:1482`)에도 같은 규칙을 대는지 함께 검토하며 `PlatformParityAuditTests`에 항목을 둔다. 위험: 보류 중 전체화면 앱이 바뀌면 그만큼 늦게 반영된다. 게임이 뜨면 그 게임이 전경이 되어 보류가 풀린다.
  - (b) `WS_EX_NOACTIVATE`는 **기각 방향**이다. 정보창 이름 편집(`CharacterInfoWindow.cs:1297` `EndNameEdit`의 입력 필드)과 키보드 입력이 포커스를 전제하고, 네이티브 스타일을 새로 쓰면 `LayeredHybridPolicyTests`의 「자기 창 스타일 쓰기는 해소기 한 파일」 규칙과도 부딪친다.

**Windows 영향**: H1은 Windows 전용 형태다(코드상). 실기 미확인. 코드 변경 없음.
**macOS 영향**: 코드상 같은 형태 없음(A-3). 실기 미확인. 코드 변경 없음.

---

## B. 멀티모니터 판정 기준 비대칭 (낮음, 기존)

### B-1. 코드 사실

| | macOS | Windows |
|---|---|---|
| 판정 창 | CGWindowList 첫 layer 0 창(`MacWindowService.cs:1458`) | 전경 창(`Win32WindowService.cs:2048`) |
| 비교 사각형 | **메인 디스플레이** `CGDisplayBounds(CGMainDisplayID())`(`:1507`) | **그 창이 놓인 모니터** `MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST)`(`:2080`) → `rcMonitor`(`:2090-2093`) |
| 불일치 시 | 그 창에서 **즉시 `return`**(`:1516-1523`) — 다음 창을 보지 않는다 | `return`(`:2101-2104`) |

- **적용은 전역이다**: `StickmanAgent.cs:1579-1586`이 등급을 그대로 `_fullscreenAutoHide`/`_fullscreenPanelRetreat`에 싣는다. 오버레이가 놓인 모니터는 `WindowsOverlayStateEnforcer.cs:878`/`MacOverlayStateEnforcer.cs:783` `TryGetTargetMonitorRect`가 **따로** 고르고, 판정 경로는 그것을 참조하지 않는다.
- 결과:

| 상황 | macOS | Windows |
|---|---|---|
| 전체화면 앱 = 오버레이 = 메인/주 모니터 | 감지 | 감지 |
| 전체화면 앱 = 오버레이 = **보조** 모니터 | **등급 0**(창·차단막이 슬라이드쇼 위에 남음 — 원칙 2 방향) | 감지 |
| 전체화면 앱 ≠ 오버레이 모니터 | 앱이 메인이면 감지(다른 화면의 표면까지 걷음) / 보조면 미감지 | 감지(**다른 화면의 표면까지 걷음**, 등급 2면 캐릭터도 숨김) |

- 주장 「macOS에서 보조 디스플레이 전체화면(프로젝터 슬라이드쇼)은 등급 0」 → **확정.** 조건: 그 앱의 창이 CGWindowList의 첫 layer 0 창일 때. 첫 창이 메인 디스플레이의 일반 창이어도 역시 등급 0이다.
- 실기 미확인: macOS 프로젝터 슬라이드쇼(Keynote/PowerPoint)가 보조 디스플레이에 만드는 창의 layer·bounds.

### B-2. 기존 패리티 항목

`Tests/EditMode/PlatformParityAuditTests.cs:3245-3296` **`역방향_보조모니터_전체화면_감지는_Windows가_낫다`** — 감지 비대칭 자체는 **이미 등재돼 있다.**
- 단 이 항목은 **Ignore가 아니라 초록으로 끝난다**(`:3292` `Assert.IsTrue`). 사유는 문서에 적힌 **의도**다(`:3254-3258` 「나은 쪽을 얼리고 못한 쪽은 고쳐지면 `Assert.Pass`」). 그래서 러너에서 「건너뜀」으로 보이지 않는다.
- 그리고 **등급 1 이전에 쓰였다**. 메시지가 「전체화면 **게임**을 놓친다」(`:3271`, `:3251-3252`)뿐이고, (i) 등급 1(E-1 이후 입구가 된 축), (ii) 「판정 모니터 ≠ 오버레이 모니터인데 전역 적용」 축은 다루지 않는다.

### B-3. 판정

**확정(기존 갭).** 감지 비대칭은 기존 항목이 얼려 두었다. **새로 보이게 남길 부분은 위 (i)·(ii)**다.

### B-4. `Assert.Ignore` 등재안 (문안만, 구현 금지)

> ★ **2026-09-15 정정(리더) — 이 절은 초안이고, 정본은 구현된 테스트다**: `Assets/_Project/Scripts/Tests/EditMode/PlatformParityAuditTests.cs` `미해결_전체화면_등급의_기준_모니터와_적용_범위가_갈라져_있다`(dev-platform 5-d 러너 줄, 커밋 전). 초안과 다른 점 2가지: ① **한 축만 닫히면 `Assert.Pass`가 아니라 `Assert.Fail`** — 축마다 Pass로 두면 남은 축이 러너에서 「통과」로 사라진다(변이 B1·B3로 확인; 두 축이 모두 닫혀야 Pass). ② 아래 사유 문장에서 「(원칙 2 방향)」과 **「2026-09-14 E-1로 … 이 경로의 노출이 늘었다」를 삭제**했다 — macOS 보조 디스플레이 전체화면은 등급 0이라 E-1(등급 1 입구)과 무관하다(dev-platform 자기 초안 오류). 결함/의도 문장은 「미해결 — game-architect 판정 대기」로 바꾸고, (나) 적용 범위 축은 **두 플랫폼 공통**으로 적었다. 양성 앵커 2개(`nameof`)와 부재 니들 존재 대조가 추가됐다. 아래 원문은 이력으로 남긴다.

- **위치**: `:3296`(기존 `역방향_보조모니터_전체화면_감지는_Windows가_낫다` 끝) **바로 뒤**, `:3298` 「Windows 하단 막대 낙차」 절 머리 **앞**.
- **테스트 이름**: `미해결_전체화면_등급의_기준_모니터와_적용_범위가_갈라져_있다`
- **Ignore 전 가드**(썩음 방지 — 기존 항목과 같은 방식):
  - `MethodBody(mac, "private void EvaluateFullscreen(")`에 `CGMainDisplayID()`가 **없으면** `Assert.Pass`(「macOS가 메인 디스플레이만 보지 않게 됐다 — 이 항목을 정식 검사로」).
  - `MethodBody(win, …)`에 `MonitorFromWindow(fg`가 없으면 `Assert.Fail`(기존 항목과 같은 뜻의 회귀).
  - `StickmanAgent.cs` `TickFullscreenSuspend` 본문이 오버레이 모니터(`TryGetTargetMonitorRect` 등)를 참조하기 시작하면 `Assert.Pass`(「적용 범위 축이 닫혔다」).
- **사유 문장**:
```text
【미해결 · 판정 기준 비대칭 · 코드 판독 / 실기 0회】 신설 2026-09-15 (dev-platform)
항목: 전체화면 등급의 '기준 모니터'와 '적용 범위'가 갈라져 있다.
· macOS: 첫 layer 0 창을 메인 디스플레이(CGMainDisplayID) 사각형과만 비교한다 — 보조 디스플레이의 전체화면 앱
  (프로젝터 슬라이드쇼 등)은 등급 0이고, 오버레이가 그 화면에 있으면 정보창·설정창과 그 클릭 차단막이 그 위에 남는다(원칙 2 방향).
  2026-09-14 E-1로 등급 1에서도 사용자가 창을 열 수 있게 되어 이 경로의 노출이 늘었다.
· Windows: 전경 창이 놓인 모니터로 판정해 감지는 되지만, 결과(등급 1 표면 회수 · 등급 2 캐릭터 숨김)가
  오버레이가 놓인 모니터와 무관하게 전역 적용된다 — 다른 모니터의 전체화면 앱이 우리 화면의 창을 걷는다.
  이것이 결함인지 의도(원칙 2 문구는 전역)인지는 미판정이다(리더·ux-designer).
· 감지 비대칭 자체는 역방향_보조모니터_전체화면_감지는_Windows가_낫다가 얼려 두었다. 이 항목은 그 옆의 두 축을 러너에 보이게 남긴다.
해소 조건: '오버레이가 놓인 모니터와 겹치는 전체화면인가'를 플랫폼 중립 정책(Platform/FullscreenSuspendPolicy.cs)에서 묻고,
두 플랫폼은 창이 놓인 디스플레이 사각형과 오버레이 모니터 사각형을 사실로만 넘긴다.
근거: docs/platform/E1_FOREGROUND_TIER_FACTS.md B절.
```

**Windows 영향**: 코드 변경 없음. 적용 범위 축은 Windows에도 있다(판단 대기).
**macOS 영향**: 코드 변경 없음. 감지 누락은 macOS 쪽 갭이다(기존).

---

## C. 표시 변경 경로 목록 검토 — `SettingsWindow.cs`·`CharacterInfoWindow.cs` (낮음, 실기 필요)

### C-1. 주장 분해와 코드 사실

| 주장 | 판정 | 코드 사실 |
|---|---|---|
| (c1) E-1로 「등급 1 + 창 연 채」가 흔해진다 | **코드상 참**(빈도는 코드로 못 잰다) | 등급 1 허가 발급 입구가 우클릭 `AppControlDirector.cs:985` · 정보창 사용자 열기 `CharacterInfoWindow.cs:945` · 설정창 `SettingsWindow.cs:648` · 톱니로 넓어졌다(`StickmanAgent.cs:376-380`) |
| (c2) 분리 유예 중에도 등급이 흔들릴 수 있다 | **코드상 가능 · 발현은 실기** | `StickmanAgent.cs:978` `TickPreservationFreeze()` → **`:980` `TickFullscreenSuspend(dt)`** → `:985` 동결 조기 반환. **전체화면 폴링은 보존 동결 중에도 돈다.** 입력은 Windows `GetWindowRect(fg)`·`rcMonitor`(`Win32WindowService.cs:2074-2093`), macOS `CGDisplayBounds(CGMainDisplayID())`(`:1507`)이고, 모니터 분리가 둘 다 바꾼다. 1→0→1로 **확정**되려면 각 상태가 A-4의 확정 창을 넘어야 한다(창 재배치 소요 시간 — 실기) |
| (c3) 임대 만료 → `Close` → `FlushPendingSave` 디스크 쓰기 | **부분 참 — 조건부** | `SettingsWindow.cs:679` → `:1164-1169` `if (!_saveRequested) return;` — **보류 저장이 있을 때만**(슬라이더 드래그 중 등) `CharacterSaveStore.Save()`. 정보창 `Close`(`:974-994`)는 `:989` `EndNameEdit(commit: true)` → `:1300` **이름 편집 중이고 값이 바뀌었을 때만** 저장. 그 밖에 캔버스 `SetActive(false)`·차단막 `enabled=false`(두 창), 초상화 무대 카메라 끄기 `:992` → `CharacterPortraitStage.cs:328-331` |
| (c4) 재적합과 같은 틱 | **가능** | 실행 순서 속성이 없고, 재적합은 `WindowsOverlayStateEnforcer.Update`에서 돈다. 같은 프레임 여부는 스크립트 순서가 정한다(미정) |

### C-2. 표시 변경 이벤트 → 두 파일 호출 사슬

**호출 사슬은 없다. 연결은 데이터 결합뿐이다.**
- DCP 진입 신호(`OnMonitorChanged` 구독 · `DisplayTopologyWatcher.Observe`) → `DisplayChangeHoldDriver` → `DisplayChangeRenderHold` → 소비자, 어디에서도 `GetForeignFullscreenTier`·`TickFullscreenSuspend`·`ArePanelsSuppressed`를 부르지 않는다.
  실측: `DisplayChangeHoldDriver.cs`·`DisplayChangeRenderHold.cs`·두 Enforcer에서 그 식별자 **0건**. 같은 식별자의 비테스트·비주석 호출은 `StickmanAgent.cs`·두 `*WindowService.cs`·`FallbackPlatformWindowService.cs`에만 있다(양성).
- 사슬 모양: 표시 변경 → **OS가 창·모니터 사각형을 바꿈** → 1.5초 타이머 폴링(`StickmanAgent.cs:1545`)이 **읽음** → 등급 상승 에지 → 임대 0(`:447`) → 각 창 `Update`가 `ArePanelsSuppressed`를 폴링해 `Close`.
- ★ 설계 의도와의 대조: `Core/CharacterPreservationFreeze.cs:13-16`이 「보존 동결은 **UI 표면을 숨기지 않는다** — `IsSuspended`/`HidesScreenSurfaces`/`ArePanelsSuppressed` 계열에 섞으면 모니터를 뺄 때마다 UI 숨김이 켜진다」고 적어 **호출 결합은 의도적으로 끊었다.** 재현이 가리킨 것은 그 결합이 아니라 **등급 판정 입력을 통한 우회 결합**이고, 그것은 코드상 존재한다.
- 간접 효과 1건: 차단막이 사라지면 다음 프레임에 패키지 히트테스트(`UniWindowController.cs:643-647`)가 관통을 다시 켜고, 네이티브는 `WS_EX_TRANSPARENT|WS_EX_LAYERED`를 쓴다. 이것은 **커서가 캐릭터를 벗어날 때마다 일어나는 평상 쓰기와 같은 경로**이고, 쓰는 주체(패키지)는 이미 A행 `Packages/packages-lock.json`이 대리한다.

### C-3. 목록 포함 규칙(①「경계 규칙」)에 비춘 판정

| 규칙 | `SettingsWindow.cs` | `CharacterInfoWindow.cs` |
|---|---|---|
| 1 진입 신호 수신 | 아님(`OnMonitorChanged` 0 — 0절 정규식) | 아님 |
| 2 유예·재적합·해제 중 동기 호출 | 아님(C-2: 호출 사슬 없음) | 아님 |
| 3 유예 상태 소비자 | 아님(소비자 식별자 0) | 아님 |
| 4 경로 위 디스크·스레드 계측 | 아님(`FlushPendingSave`는 계측이 아니고 경로 위도 아니다) | 아님 |
| 5 입력 | 아님 | 아님 |
| 6 같은 틱에 같은 오버레이 창의 투명·크기·Z·Space 쓰기 | 아님(캔버스 활성·uGUI 차단막은 창 상태가 아니다. 관통 재점등은 C-2 간접 효과 — 패키지가 쓴다) | 아님(카메라 끄기도 창 상태 아님) |

**판정안: 두 파일 모두 목록 편입 불요.** 현행 규칙 1~6 해당 0이다. 썩음 검사(②(다))도 두 파일을 `NEW-*`로 내지 않는다(소비자 식별자 0).

**그리고 넣어도 증거가 늘지 않는다.** 목록의 목적은 N-8 「같은 반응 경로를 시험했는가」다. 그런데 §W E-3 절차는 등급 1·열린 창을 만들지 않는다. `WINDOWS_CHECK_SESSION.md` §W에서 `전체화면|설정창|정보창|부채꼴` 적중은 로그 태그 목록(`:84`·`:367`)과 오버레이 「첫 전체화면 적합」(`:145`, 남의 전체화면 앱이 아니다)뿐이다. 두 빌드 모두 이 사슬을 **시험하지 않으므로**, 파일 차이가 E-3 증거 동일성을 바꾸지 않는다.
⇒ 이 조합을 CW-7 증거로 덮고 싶다면 수단은 목록이 아니라 **E-3 변형 절차**(등급 1 + 설정창 연 채 분리)다. 강제 리부팅 위험이 같으므로 **리더 판단**이다.

**선택안(리더 판단)** — 기록만 남기려면 `X` 행에 사유와 함께 적는다.
- `X both    Assets/_Project/Scripts/Interaction/SettingsWindow.cs` / `X both    Assets/_Project/Scripts/Interaction/CharacterInfoWindow.cs`
- 사유: 「화면 변경과 호출 결합 없음. 등급 판정 입력을 통한 2차 결합만 있고 E-3 절차가 그 조건(등급 1 + 열린 창)을 만들지 않는다」.
- `X`는 ②(가)의 `awk`(`^[ABCDE]$`만 추출)에 들어가지 않아 **목록 41·N-8 기준 불변**이다. 행 수는 46→48로 늘어 하한 46에 걸리지 않는다. 이 문서는 목록 파일을 **편집하지 않았다.**

### C-4. 이 사슬에서 찾은 추가 후보와 백로그 겹침

사슬의 **판정 절반은 이미 목록 안**이다 — `Win32WindowService.cs`(A) · `MacWindowService.cs`(A) · `StickmanAgent.cs`(B). 목록 밖은 아래뿐이고, **전부 현행 규칙상 불요**다(C-3과 같은 이유). 소비자 식별자는 14파일 모두 0이다(0절).

| 후보 | 사슬에서의 자리 | 백로그 「DCP 누락 후보」(`Tasklist.md:27560`) 겹침 |
|---|---|---|
| `Platform/FullscreenSuspendPolicy.cs` | 디바운서·등급 합성 — **분리 중 흔들림이 확정되느냐를 정하는 유일한 시간 규칙** | 겹치지 않음 |
| `Platform/UserSurfaceSummonPolicy.cs` | 임대 0.5초·부활 금지·억제 판정 | 겹치지 않음 |
| `Core/AppSettingsModel.cs` | `AutoHideOnFullscreen` 게이트(`StickmanAgent.cs:1579`·`:1585`) | **겹침** — 백로그의 `AppSettingsModel.cs`와 같은 파일 |
| `Platform/Windows/WindowsGameProcessProbe.cs` | 등급 판정 중 프로세스 경로·레지스트리 읽기(`Win32WindowService.cs:2114`) | 겹치지 않음 |
| `Platform/FallbackPlatformWindowService.cs` | 등급 조회 위임(`:779-783`) | 겹치지 않음 |
| 표면 소비자: `Interaction/GearRadialMenuWidget.cs` · `InfoGearIconWidget.cs` · `PopoverPanel.cs` · `FocusSessionPopover.cs` · `TodoPostItWidget.cs` · `TodoReminderDirector.cs` · `WindowCrashDirector.cs` · `AppControlDirector.cs` · `Core/AudioReactiveDanceGate.cs` | `ArePanelsSuppressed`/`IsUserSummonBlocked`를 읽고 스스로 걷음(비테스트 파일 grep) | 겹치지 않음 |

백로그의 나머지(`ViewerPresence.cs` · Windows 창 스타일 3파일 · `WindowsLayeredHybridResolver.cs`)는 **이 사슬과 무관**하다. 「창 스타일 3파일」은 Tasklist 원문에 파일 이름이 없어 **이름 대조를 하지 못했다**. `WindowsLayeredHybridResolver.cs`만 C-2 간접 효과(레이어드 재점등)와 스치지만, 그것은 사슬이 아니라 평상 경로다.

**이 문서가 규칙 신설을 제안하지는 않는다.** 만약 리더가 「화면 변경이 입력을 흔드는 판정의 소비자」를 규칙 7로 넣는다면, 위 표 전체(약 16파일)가 한꺼번에 들어가야 일관된다. 두 파일만 넣으면 기준이 갈라진다.

### C-5. 판정과 다음 조치 문안

- **C 판정**: 재현의 사슬은 **데이터 결합으로 코드상 존재하고, 호출 사슬로는 기각**한다. 디스크 쓰기는 **조건부**다. 목록 편입은 **불요**(규칙 1~6 해당 0, E-3가 조건을 만들지 않음). 분리 중 등급 1→0→1 확정 여부는 **판정 불가(실기)**.
- **다음 조치 문안**: 「`DISPLAY_CHANGE_PATH_FILES.md` 목록 무변경. 원하면 X 행 2줄(C-3 선택안) 추가 — N-8 기준 불변. 등급 1 + 창 열림 + 분리 조합의 실기는 E-3 변형으로만 덮이며 리더 판단. `AppSettingsModel.cs`는 기존 백로그 후보와 같은 파일이므로 5-d 뒤 백로그 판정에서 함께 본다.」

**Windows 영향**: 코드 변경 없음. 목록 무변경 권고.
**macOS 영향**: 코드 변경 없음. 입력(`CGMainDisplayID`)이 메인 디스플레이 분리에 흔들리는 것은 같다 — 목록 무변경 권고.

---

## 출처 (전부 이 저장소·패키지 캐시 실측, 외부 2건)

- 이 저장소: 본문 각 `파일:줄`.
- 패키지 캐시 `Library/PackageCache/com.kirurobo.uniwinc@304f9ba2aa4a/`: `Runtime/Scripts/UniWindowController.cs`(`:61-66`·`:626-649`·`:797-`·`:904-915`) · `Runtime/Plugins/Windows/x64/LibUniWinC.dll` 임포트 표.
- 외부: 업스트림 `kirurobo/UniWindowController` `main` 브랜치 `VisualStudio/LibUniWinC/libuniwinc.cpp`(창 프로시저·`SetClickThrough`, 판 일치 미확인) · Microsoft Learn 「Extended Window Styles」(`WS_EX_NOACTIVATE` 설명).
