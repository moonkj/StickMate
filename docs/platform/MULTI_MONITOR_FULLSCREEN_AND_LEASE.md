# 멀티모니터 전체화면 판정(P-5) · 모니터 분리 뒤 임대 갱신(P-6) — 코드 사실 조회

담당 `dev-platform` · 2026-09-15 · 기준 `76504c2` blob(비테스트 `.cs` 297개와 `PlatformParityAuditTests.cs`·`docs/verify/DISPLAY_CHANGE_PATH_FILES.md`를 `git show`로 떠서 읽음) · **코드 읽기만 했다. 실기 0회, 빌드·Unity 실행 0회, 코드 변경 0줄.**
경로는 따로 적지 않으면 `Assets/_Project/Scripts/` 기준. 패키지 파일은 `com.kirurobo.uniwinc@304f9ba2aa4a`(C#) · 업스트림 태그 `v0.9.8`(네이티브 소스, 이진과 같은 판인지 미확인)이다.
**코드·1차 문서 인용이 없는 OS 동작은 전부 「미확인」이다.**
범위 밖: 모니터 3대 이상 배치 · 미러링 · OS 모니터 목록 조회가 실패한 뒤의 폴백 동작은 이 문서가 판정하지 않았다.

---

## 0. 판정 요약

| 질문 | 판정 | 근거 절 |
|---|---|---|
| P-5 주장 「macOS 전체화면 판정이 메인 디스플레이하고만 비교한다」 | **확정** | 1-1 |
| P-5 주장 「`MacWindowService.cs:1150`·`:1184`도 메인 기준이다」 | **기각** — `:1150`은 메인 디스플레이 사각형 **조회 함수**다. 오버레이 적합에서는 **주 모니터일 때만** 그 값을 목표 크기로 쓴다(`MacOverlayStateEnforcer.cs:461-467`) — 보조 모니터 크기의 원천은 아니다. `:1184`는 활성 디스플레이 **전부**를 열거하며 주 모니터 표시만 붙인다 | 1-1 |
| 오버레이가 외장 모니터까지 덮는가 | **한 모니터만 덮는다** — 선택된 모니터(기본: 가로 배치면 가장 왼쪽, 세로면 가장 위 / 사용자 선택). 외장이 그 자리면 외장을 덮는다 | 1-2 |
| 발판·캐릭터 이동 범위 | **오버레이 모니터 기준**(메인 디스플레이는 발판 조회의 폴백일 뿐) | 1-3 |
| 외장 모니터 발표 위에 그려질 수 있는가 | **오버레이가 그 모니터에 있을 때만 — 코드상 가능, 실기 미확인** | 1-4 |
| P-5 원칙 2 영향 | **조건부로 있다** — macOS에서 오버레이와 전체화면 앱이 **같은 보조 모니터**면 등급 0(게임이면 캐릭터 노출). Windows는 **전경이 자기 모니터를 정확히 덮지 않는 다른 창일 때** 같은 형태(발표자 보기는 두 갈래 모두 가능 — 2절) | 1-5 · 2 |
| P-6 표시 변경 감지 경로 | **있다**(양 플랫폼 · 패키지 신호 + 우리 위상 감시) | 3-1 |
| P-6 열린 창 보정·닫기 | **닫지 않는다.** 위치는 정보창 매 프레임 클램프 · 설정창은 옮긴 경우만 클램프(안 옮겼으면 중앙) · 부채꼴은 보정 없음 | 3-2 |
| P-6 화면 밖 창이 임대를 갱신하는 경로 | **코드상 성립 — 단 임대가 이미 활성(등급 1 중 사용자가 연 경우)일 때만.** 지속되는 「화면 밖」은 부채꼴(최대 6초, 팝오버가 붙으면 무입력 최대 180초)과 오버레이 재적합 전 구간뿐이다 | 3-3 |
| P0(분리 시 하얀 화면·멈춤)와의 관계 | **직접 원인 가능성 낮음 — 미배제** — 임대가 살아 있으면 부채꼴이 매 프레임 프레임 페이싱 홀드를 걸어 등급·렌더 간격 쓰기에 닿는다(등급 0에서 부채꼴을 연 것과 같은 동작). 창 크기·스왑체인·표시 변경 유예를 호출하지는 않는다 | 4 |

---

## 1. P-5 — macOS

### 1-1. 전체화면 판정과 주장 인용 대조

- 판정: `Platform/MacOS/MacWindowService.cs:1440-1550` `EvaluateFullscreen`이 CGWindowList 앞쪽의 **첫 layer 0 창**을 `:1507` `CGDisplayBounds(CGMainDisplayID())`와 비교한다. 그 창이 메인 디스플레이와 기하가 맞지 않으면 그 자리에서 반환한다(E1 B-1 표와 같은 사실). ⇒ **주장 확정.**
- `:1148-1153` `TryGetMainDisplayBounds` — 메인 디스플레이 사각형을 돌려주는 조회다. 비테스트 호출은 `76504c2`에서 주석 제외 **6줄**(용도 5)이다 — `MacWindowService.cs` 967·972·2270 · `MacOverlayStateEnforcer.cs` 461·719 · `MacReservedScreenEdgeService.cs` 100:
  - 조회 실패 폴백(한 함수 안 두 줄): 발판 경계 `MacWindowService.cs:967` · 원점 위생 검사 `:972` — 앞의 `OverlayMonitorDirectory` 조회가 실패할 때만.
  - 오버레이 적합에서 **주 모니터일 때만** 메뉴 막대·Dock 띠까지 덮는 확장 — 목표 크기를 `(0, 0, 디스플레이 폭, 높이)`로 바꾼다: `Platform/MacOS/MacOverlayStateEnforcer.cs:461-467`.
  - 위상 서명의 desktop 크기: `MacOverlayStateEnforcer.cs:719`.
  - 예약 가장자리(메뉴 막대·Dock) 계산: `Platform/MacOS/MacReservedScreenEdgeService.cs:100`.
  - 오버레이 창 좌표 보고: `MacWindowService.cs:2270`.
- `:1173-1194` `TryEnumerateOsMonitors` — `CGGetActiveDisplayList`로 **활성 디스플레이 전부**(최대 16)를 넣고, `:1184` `CGMainDisplayID()`는 각 항목에 「주 모니터인가」 표시만 붙인다(`:1191`). ⇒ **「메인 기준」 주장 기각.**

### 1-2. 오버레이 창 크기·위치의 원천 — 선택된 한 모니터

- 적합: `MacOverlayStateEnforcer.cs:404` `TickFullScreenBounds` → `:783` `TryGetTargetMonitorRect` → `:947` `TryResolveChosenMonitorRect` → `OverlayMonitorDirectory.Resolve()`. 결과로 `:569` `windowSize = monitor.size` · `:580` `windowPosition = monitor.position`. **가상 화면 전체(모든 디스플레이 합집합)가 아니다.**
- 모니터 목록: OS 열거(위 `:1173-1194`) → `Platform/OverlayMonitorDirectory.cs:56` `Publish`.
- 선택 규칙(`Platform/OverlayMonitorChoicePolicy.cs:423-453` `Resolve`):
  - 사용자가 고른 자리가 있으면 그 자리.
  - 없으면 **시작 자리** — 가로 배치면 가장 왼쪽, 세로 배치면 가장 위. 주 모니터 플래그가 아니라 축 최솟값이다(같은 파일 열거 주석, 사용자 확정 2026-09-02 「기본은 왼쪽」).
  - 미러링이면 x 최솟값.
  - ⇒ **외장 모니터가 메인보다 왼쪽에 있거나 사용자가 골랐으면 오버레이는 외장 모니터에 있다.**
- 보조 모니터에서는 작업 영역(visibleFrame)만 덮는다. 주 모니터일 때만 메뉴 막대·Dock 띠까지 늘린다(`:456-466` 주석 「보조 모니터는 … 작업영역 그대로」).

### 1-3. 발판 · 캐릭터 이동 범위

- 발판 조회 경계: `MacWindowService.cs:966-967` `OverlayMonitorDirectory.TryGetOverlayScreenOsRect`(오버레이가 놓인 모니터) → 실패 시에만 메인 디스플레이. 경계 밖 창은 `:1029` `RejectOffDisplay`. `:971-972` `TryGetDesktopUnionOsRect`(모든 모니터의 외접 사각형)는 발판용이 아니라 **원점 위생 검사 전용**이다(`Platform/OverlayMonitorDirectory.cs:154` 주석 · `MacWindowService.cs:959` 주석 · 사용 `:2129`) → 실패 시 메인.
- 캐릭터 이동 클램프: `States/StickmanBlackboard.cs:1702-1707` `ComputeScreenClampOsBounds` — `OverlayOriginOsScreen`(오버레이 원점) + `Screen.width/height`(= 오버레이 창 크기). 구조 `RescueToSafeGround`(`:1950-1956`)도 같은 원점이다.
- ⇒ **발판과 이동은 오버레이 모니터를 따른다.** 메인 디스플레이가 끼는 곳은 조회 실패 폴백뿐이다.

### 1-4. 외장 모니터 발표 위에 실제로 그려질 수 있는가

- 오버레이가 외장 모니터에 있을 때만 그 화면에 그림이 있다(1-2).
- 다른 앱의 전체화면 Space 위에 머무는 창 동작: `Platform/MacOS/MacSpaceBehaviorNative.cs:133-135` `canJoinAllSpaces | stationary | fullScreenAuxiliary`. 코드상 전체화면 Space에 함께 뜨도록 설정돼 있다. 전제: 앱 activation policy가 **accessory**여야 한다 — Regular 앱은 활성화될 때 macOS가 Space를 그 앱 쪽으로 전환해 동거가 유지되지 않는다(`MacSpaceBehaviorNative.cs:14-23` 클래스 주석, 전환 호출 `MacOverlayStateEnforcer.cs:279`).
- 미확인(실기):
  - macOS 「디스플레이마다 별도 Space 사용」 설정의 켬/끔에 따른 보조 모니터 전체화면 Space 동거.
  - 보조 모니터 visibleFrame 적합과 네이티브 전체화면 창의 겹침.

### 1-5. 원칙 2 영향 — 세계별 (macOS)

| 오버레이 모니터 | 전체화면 앱 | 등급 판정 | 그 화면에 그려지는 것 | 원칙 2 |
|---|---|---|---|---|
| 메인 | 메인 | 감지(첫 layer 0 창이 그 앱일 때) | 규칙대로 물러남 | 문제 없음 |
| 메인 | 외장 | 등급 0 | 외장에는 우리 그림이 **없다** | **문제 아님**(그릴 수 없다) |
| **외장** | **외장** | **등급 0** | 캐릭터·창·메모 카드·차단막이 **그대로** 남음(코드상, 1-4 실기 미확인) | **위반 방향** — 게임이면 캐릭터 노출(등급 2여야 함), 비게임이면 창·차단막 잔존(등급 1이어야 함) |
| 외장 | 메인 | 감지되면 **전역 적용** | 외장의 표면까지 물러남 · 게임이면 캐릭터 숨김 | 위반 아님 — 과잉 물러남(Ignore B (나)) |

- 첫 layer 0 창이 메인 디스플레이의 일반 창이면 다른 모니터의 전체화면 앱은 어느 경우든 보지 않는다(`:1516-1523` 반환, E1 B-1).

---

## 2. Windows 대응 — 같은 질문

| 질문 | Windows 코드 사실 |
|---|---|
| 등급 판정 기준 모니터 | **전경 창이 놓인 모니터** — `Platform/Windows/Win32WindowService.cs:2080` `MonitorFromWindow(fg, …)` → `rcMonitor`와 네 변이 **정확히** 같아야 한다(`:2080-2093`). 전경 창 **하나만** 본다(`:2048`) |
| 오버레이 범위 | 선택된 한 모니터 — `Platform/Windows/WindowsOverlayStateEnforcer.cs:878` `TryGetTargetMonitorRect` → `:1040` → 같은 `OverlayMonitorDirectory.Resolve`, `:696` `windowSize` · `:707` `windowPosition`. 목록은 `EnumDisplayMonitors`(`Win32WindowService.cs:1653`) |
| `MonitorFromWindow`의 다른 사용 | `:1713` · `:1800` — **오버레이 창 자신**이 놓인 모니터의 예약 띠 계산(작업표시줄 두께·가장자리 여백). 판정과 무관 |
| 가상 화면 | `:804-812` `TryGetVirtualScreenBounds`(`SM_*VIRTUALSCREEN`) — 오버레이 크기 원천 아님(판정·적합 경로에서 부르지 않음) |
| 적용 범위 | 전역(`Core/StickmanAgent.cs:1579-1586`) — macOS와 같다 |

**Windows 세계별 원칙 2**
- 오버레이와 전체화면 앱이 같은 외장 모니터 + 사용자가 그 앱을 전경으로 둔다 → 감지 → 문제 없음.
- 오버레이 모니터 ≠ 전체화면 모니터 → 감지되면 전역 적용(과잉 물러남, Ignore B (나)).
- ★ **오버레이가 외장 모니터 + 외장에 전체화면 슬라이드쇼 + 전경은 자기 모니터를 정확히 덮지 않는 다른 창** → 전경 창 기하가 맞지 않으므로 **등급 0** → 슬라이드쇼 위에 창·차단막(게임이면 캐릭터)이 남는다. 코드상 발생(`:2048` 전경 1창 · `:2090-2093` 정확 일치).
  - 발표자 보기가 전경인 경우는 두 갈래다. (i) 발표자 보기 창이 **자기 모니터를 정확히 덮으면** 등급 1이 전역 적용된다(과잉 물러남 — 오버레이 모니터의 표면도 걷힌다). (ii) 정확히 덮지 않으면 위와 같은 등급 0이다. 발표자 보기의 창 사각형과 실제 전경 창은 **둘 다 실기 미확인**이다.
  - **이 갈래(전경 1창 판정)는 Ignore B의 사유에 없다**(아래).

### 2-1. 패리티 `Assert.Ignore` B 대조 (`PlatformParityAuditTests.cs:3316` `미해결_전체화면_등급의_기준_모니터와_적용_범위가_갈라져_있다`, `76504c2`)

| B의 축 | 이 문서 사실과 대조 | 판정 |
|---|---|---|
| (가) macOS 기준 모니터가 메인 디스플레이뿐 | 1-1 확정 | **일치** |
| (나) 두 플랫폼 모두 등급을 오버레이 모니터와 무관하게 전역 적용 | 2절 표 · 1-5 넷째 행 | **일치** |
| 사유 문장 「오버레이가 그 화면에 있으면 창·패널과 그 클릭 차단막을 걷지 않는다」 | 1-5 셋째 행 — 게임이면 **캐릭터까지** 남는다(등급 0이라 캐릭터 축도 안 켜짐) | **보강 필요**(사유가 표면만 말함) |
| 없음 | Windows **전경 1창** 판정 — 전경이 자기 모니터를 정확히 덮지 않는 다른 창이면 오버레이 모니터의 전체화면을 못 본다(2절 ★) | **B에 없는 갈래** |
| 없음 | 「그려질 수 있는가」가 오버레이 모니터 선택(기본 가장 왼쪽)에 달림 — 오버레이가 메인이면 macOS (가)는 원칙 2 문제가 아니다 | **B에 없는 조건** |

- 등재 보강 문안(구현 금지): B 사유에 「(가)의 노출은 오버레이가 그 보조 디스플레이에 있을 때만 성립하고(기본 시작 자리 = 가장 왼쪽 · 사용자 선택), 게임이면 캐릭터까지 남는다」와 「(다) Windows는 전경 창 하나만 판정해, 전경이 자기 모니터를 정확히 덮지 않는 다른 창이면 오버레이 모니터의 전체화면을 못 본다」를 추가.
- 해소 조건은 B의 기존 문장(오버레이 모니터와 겹치는 전체화면인가를 중립 정책에서 묻는다)이 두 갈래를 함께 덮는다.

---

## 3. P-6 — 모니터 분리 뒤 임대 갱신

### 3-1. 표시 구성 변경 감지 경로 — 있다

- Windows 패키지: 서브클래스 창 프로시저의 `WM_DISPLAYCHANGE` → 모니터 변경 콜백(`libuniwinc.cpp:1413-1419`) → C# `OnMonitorChanged` → 우리 구독(`WindowsOverlayStateEnforcer` 표시 변경 유예 구동기, `docs/verify/DISPLAY_CHANGE_PATH_FILES.md` ① 호출 그래프 1 (가)).
- macOS 패키지: `NSApplication.didChangeScreenParametersNotification` 구독 → 콜백(업스트림 `Xcode/LibUniWinC/LibUniWinC.swift:272-289`) → 같은 C# 이벤트 → `MacOverlayStateEnforcer` 구독.
- 우리 위상 감시(양 플랫폼): `TickDisplayTopology`(macOS `:652` · Windows `:789`)가 `DisplayTopologyWatcher`로 구성 변화를 보고, **안정되면** 전체화면 적합을 다시 무장한다(macOS `:688-689` · Windows `:826-827`). 선택된 목표 모니터 번호가 바뀌어도 재무장한다(macOS `:996` · Windows `:1095`).
- 우리 코드가 OS 알림을 **직접** 구독하는 곳은 0이다(`WM_DISPLAYCHANGE`·`CGDisplayRegisterReconfigurationCallback`·screen parameters 알림, 비테스트 코드 줄 기준).

### 3-2. 그때 열린 창을 보정하거나 닫는가

- **닫지 않는다** — 표시 변경 경로(유예 구동기·위상 감시·재적합)에서 설정창·정보창·부채꼴의 `Close`/`Collapse`를 부르는 곳이 없다(E1 C-2 「호출 사슬은 없다」와 같은 결과).
- 위치 보정은 각 창의 매 프레임 경로가 한다(표시 변경 신호와 무관):

| 표면 | 화면 크기 변화에 대한 동작 | 근거 |
|---|---|---|
| 정보창 | 열려 있는 동안 **매 프레임** 크기·위치를 오버레이 화면 안으로 클램프 | `Interaction/CharacterInfoWindow.cs:1018` `_open` 게이트 → `:1045` `ApplyCanvasScaleFactor` → `Interaction/CharacterInfoWindow.Layout.cs:64` `ClampPanelToScreen` |
| 설정창 | **사용자가 옮긴 경우만** 매 프레임 클램프(`SettingsWindow.cs:2275`). 안 옮겼으면 캔버스 중앙 고정(`:1003` `anchoredPosition = Vector2.zero`) → 오버레이 창 안 | `Interaction/SettingsWindow.cs:2259-2277` · `:994-1003` |
| 부채꼴 | **보정 없음** — 배치는 펼칠 때만 계산(`Interaction/GearRadialMenuWidget.cs:824` → `:1963` `ComputeLayout`, `:1965` 화면 크기 스냅샷). 화면이 줄면 버튼이 밖에 남을 수 있다 | 같은 파일 |

- 설정창 「옮긴 경우만」은 주장대로다. 다만 안 옮긴 설정창은 **캔버스 중앙**이라 오버레이 창이 새 모니터에 맞춰지면 함께 화면 안으로 온다. 설정창 720×560 고정 크기보다 남은 화면이 작으면 가장자리가 잘린다.

### 3-3. 화면 밖 창이 임대를 갱신하는 경로 — 코드상 성립(조건부)

- **갱신자 3곳은 열림만 본다 — 주장 확정.**
  - `SettingsWindow.cs:755` `_open` 게이트 → `:764`
  - `Interaction/InfoGearIconWidget.cs:855` `IsMenuExpanded || _window.IsOpen`
  - `GearRadialMenuWidget.cs:1257` `Hidden` 게이트 뒤 → `:1290`
  - 화면 안에 보이는지는 묻지 않는다.
- **전제 — 임대가 이미 살아 있어야 한다.**
  - 발급은 `Core/StickmanAgent.cs:387` `TryGrantUserSummon`(허가 조건 `:394`)이고, `UserSurfaceSummonPolicy.CanGrant` = 캐릭터 표면 숨김이 아니고 **등급 1**일 때만(`Platform/UserSurfaceSummonPolicy.cs:99-100`).
  - 갱신은 만료된 임대를 되살리지 않는다(`:109-110`). 등급 1 **진입** 때 만료된다(`StickmanAgent.cs:442-447`).
  - ⇒ **평상시(등급 0에서 연 창)에는 갱신이 아무 일도 하지 않는다.** 이 경로는 「등급 1 중 사용자가 창을 연 뒤 모니터를 뺐다」에서만 산다.
- **「화면 밖」이 되는 경우와 길이**(코드 산술):
  1. **오버레이 창 자체가 재적합 전** — 사라진 모니터 좌표에 남은 동안 모든 캔버스가 안 보인다. 재적합은 표시 변경 유예가 끝난 뒤 돈다(`TickFullScreenBounds`의 유예 보류 macOS `:409` · Windows `:558`, 유예 벽시계 상한 `Platform/DisplayChangeRenderHold.cs:65` `MaxHoldSeconds = 15.0`). 시도 상한은 6회(macOS `:157` · Windows `:60`)이고, 위상이 안정되면 재무장된다(3-1). ⇒ **일시적**이다. 모니터 조회 실패로 「창 크기를 그대로 둡니다」(macOS `:423` · Windows `:572`)가 난 뒤의 창 위치는 **미확인**(OS가 창을 남은 모니터로 옮기는지는 OS 동작).
  2. **부채꼴 버튼이 줄어든 화면 밖** — 6초 무반응 자동 접힘(`GearRadialMenuWidget.cs:324`). 단 팝오버가 붙어 있으면 자동 접힘이 멈추고(`:1366`), 팝오버의 무입력 자동 닫힘은 기본 180초(`Interaction/PopoverPanel.cs:188`) — 그 뒤 부채꼴도 따라 접힌다(`:1382-1387`).
  3. **정보창 · 설정창** — 재적합 뒤에는 위 3-2대로 오버레이 화면 안이다. **지속되는 화면 밖은 코드상 없다.**
- **결과**:
  - 임대가 갱신되는 동안 자동 표면 억제(`UserSurfaceSummonPolicy.cs:165-166` `SuppressesUnsummonedSurfaces`)가 이어진다.
  - N-20 명령 가드의 회색 사유도 이어진다(`76504c2`).
  - 부채꼴이 떠 있으면 프레임 페이싱 홀드도 매 프레임 이어진다(4절).
  - 이것은 N-20 설계의 「사용자가 연 창이 떠 있는 동안」 동작이다. 달라지는 점은 **그 창이 잠시 안 보일 수 있다**는 것 하나다.
- 대응 후보(구현 금지 · 판단은 리더):
  - (a) 갱신자에 「오버레이 화면 안에 보이는가」 조건 — 부작용: 임대가 만료되면 등급 1에서 그 창이 스스로 걷힌다(사용자 창 소실).
  - (b) 부채꼴은 화면 크기가 바뀌면 접는다 — 부작용 작음.
  - 둘 다 **N-8 목록 밖** 파일이다(5절). 표시 변경 **신호**에 반응하게 만들면 목록 규칙 1(진입 신호)에 편입된다.

---

## 4. ★ P0 신고와의 관계 — 직접 원인 가능성 낮음, 미배제 (리더 판정)

- 신고 원문: 「멀티모니터를 사용중 멀티모니터 분리시 하얀화면에 캐릭터만 보이고 완전 멈춤」.
- 이 경로(3-3)가 바꾸는 값은 UI 표면의 불리언 두 개(자동 표면 억제 · 사용자 표면 회수 보류)다. **그리고 등급 1에서 임대가 살아 있으면 프레임 페이싱에도 닿는다**:
  - 임대가 살아 있으면 `ArePanelsSuppressed`가 거짓이다(`Core/StickmanAgent.cs:241-242` → `UserSurfaceSummonPolicy.SuppressesPanels`). 부채꼴은 `Interaction/GearRadialMenuWidget.cs:1263` 숨김 분기를 지나 `:1279`에서 **매 프레임** `FramePacing.HoldActiveForInteraction()`을 부른다.
  - `Platform/FramePacing.cs:660` 홀드 여부 → `:667` 등급 판정 → `:712`·`:713` `vSyncCount`·`targetFrameRate` 쓰기 → `:716` → `:290` `renderFrameInterval` 쓰기. 적응형 페이싱은 기본 켜짐이다(`:546` — 존재 감지 서비스가 있고 환경변수 기본값 참). `:287`에서 화면 변경 유예가 렌더 간격 계산의 입력으로 들어간다.
  - 이것은 **등급 0에서 부채꼴을 열었을 때와 같은 동작**이다. 창 크기·스왑체인·표시 변경 유예를 **호출**하지는 않는다.
  - 부채꼴·임대 쪽 파일은 N-8 목록 밖이지만, 홀드가 닿는 `FramePacing.cs`는 **A**다(5절).
- 판단: 「하얀 화면」과 「완전 멈춤」은 표시 변경 경로(`docs/verify/DISPLAY_CHANGE_PATH_FILES.md` 목록)의 증상 범주다. 임대 경로는 그 경로가 쓰는 **입력 하나**(렌더 간격을 정하는 등급)를 활성 쪽으로 붙잡을 뿐이고, 같은 입력은 평상시 UI 조작에서도 걸린다 ⇒ **직접 원인 가능성 낮음 — 미배제**.
- **E-3 증거 빌드 로그 판독법** (문자열은 `76504c2` 기준 — E-3 빌드의 판이 다르면 같은 문자열이 있는지 먼저 센다. 긴 문구는 소스에서 여러 줄로 이어 붙으므로 아래 표기한 **첫 조각만** 고정 문자열로 찾는다):
  1. **멈춘 세션의 로그인가**
     - 기동마다 한 번씩 찍히는 줄로 세션을 가른다: ` 활성 — 폴더`(`Platform/FreezeWatchdog.cs:263`) · ` 직전 실행 판정=`(`:289` · `:296`). 둘 다 앞에 `[동결기록]` 태그(`Platform/FreezeForensicsPolicy.cs:42`)가 붙고, 에디터에서는 이 장치가 아예 켜지지 않는다(`:100`). 한 파일에 ` 활성 — 폴더`가 2건 이상이면 세션이 섞인 파일이다.
     - 멈춘 뒤 앱을 다시 켰다면 멈춘 세션은 `Player-prev.log` 쪽이다(파일명 상수 `Platform/SessionExitMarker.cs:70`). 다음 세션이 직전 실행을 비정상 종료로 판정하면 그 파일을 원장 폴더(`FreezeForensics` — `Platform/FreezeForensicsPolicy.cs:45`)에 `previous-abnormal-player-` 접두 사본으로 복사하고(`SessionExitMarker.cs:73`, 호출 `Platform/FreezeWatchdog.cs:259-261`, 보고 줄 `:287-292`), 용량을 넘으면 **끝부분만** 남는다(`:291`).
     - 코드로 확인 못 한 것: `Player.log`에서 `Player-prev.log`로의 회전 자체는 Unity 엔진 동작이라 우리 코드에 없다(전제만 `FreezeWatchdog.cs:259-260` 주석에 적혀 있다). 실기 미확인.
  2. **양성 대조 — 위치로 묶는다**
     - 「같은 로그에 `[화면변경유예]` 줄이 1건 이상」은 대조가 되지 못한다. 분리가 아닌 표시 변경도 유예를 시작한다: 패키지 모니터 변경 통지(`Platform/DisplayChangeRenderHold.cs:194-203`)와 우리 위상 감시기의 변화 감지(`:213-220`) 어느 쪽이든 시작하고, 시작 사유 이름 두 개(`:105-112`)도 분리를 가리키지 않는다.
     - 대신 위치로 묶는다: **로그 끝 직전의 마지막 시작 줄**이고 그 뒤에 같은 번호의 해제 줄이 없어야 한다. 시작 조각은 ` 유예 #` 다음의 ` 시작 — 사유=`(조립 `Platform/DisplayChangeHoldDriver.cs:115`, 출력 `:120`), 해제 조각은 ` 해제 — 사유=`(`:146` · `:151`)이며 그 사이에 ` 조용한 구간 끝`(`:125` · `:128`)이 올 수 있다. 태그는 `[화면변경유예]`(`:20`), 그 뒤 플랫폼 표지는 `Windows`(`Platform/Windows/WindowsOverlayStateEnforcer.cs:970`) 또는 `macOS`(`Platform/MacOS/MacOverlayStateEnforcer.cs:879`)다.
     - 유예 상한은 15초(`DisplayChangeRenderHold.cs:65`)라 메인 스레드가 계속 돌았다면 해제 줄이 찍혀야 한다 ⇒ 「시작만 있고 해제 없이 로그가 끝남」이 멈춤과 묶이는 형태다(코드 추론, 실기 미확인). 해제 줄이 있으면 그 에피소드는 멈춤과 묶을 수 없다.
     - 에피소드 번호는 실행마다 1부터 센다(`:275`, 계약 `:303`). 그래서 1번 단계를 먼저 해야 번호가 뜻을 가진다.
  3. **시각이 없다 — 줄 순서로 읽는다**
     - `Player.log`의 우리 줄에는 시각이 없다. 플레이어에서만 끼우는 로그 처리기(`Platform/StallAttributionProbe.cs:38` 설치 · `:210-227`)는 문자열을 원래 처리기에 그대로 넘기고 소요 시간만 잰다. 그래서 아래 판독은 「시각 순서」가 아니라 **줄 순서와 로그 끝**만 쓴다. Unity 엔진이 줄 머리에 무엇을 붙이는지는 코드 밖이라 미확인.
     - 시각이 꼭 필요하면 멈춤 진단 원장을 본다. 줄마다 UTC 시각 · pid · `rt=` · `frame=`이 붙고(`Platform/FreezeForensicsPolicy.cs:219-232`), 유예 시작·해제는 `Player.log`와 **같은 상세 문자열로** 원장에도 즉시 기록된다(`Platform/DisplayChangeHoldDriver.cs:119` · `:150`, 보존 규칙 `FreezeForensicsPolicy.cs:110-134`). 세션은 원장 머리줄의 ` 세션 시작 UTC`(`Platform/FreezeWatchdog.cs:251`)와 줄마다 실리는 pid로 고른다.
     - 원장의 한계: `FreezeForensics.Record`를 부르는 곳은 세 파일뿐이다(유예 구동기 · 양 플랫폼 오버레이 적합기) — 단 이것은 `Record` 한정이다. 원장에 쓰는 다른 길이 있다: 워치독은 `FreezeForensicsLog.Write`를 직접 부르고(`Platform/FreezeWatchdog.cs:179` — 정지 · 재개 · 하트비트), 두 적합기는 `ObserveTopologyTransition`(`Platform/MacOS/MacOverlayStateEnforcer.cs:680` · `Platform/Windows/WindowsOverlayStateEnforcer.cs:818`)과 `RecordTransparencyReassign`(`:340` · `:441`)도 쓴다. 그래도 결론은 같다: 임대 · 부채꼴 · 창 · `[표면회수]` 줄은 어느 경로로도 원장에 없어 **시각을 얻을 수 없다**.
  4. **임대가 살아 있었는가 — 부여 줄**
     - 부여 로그 `[표면회수] 등급 1 중 사용자가 표면을 직접 불렀습니다`(`Core/StickmanAgent.cs:401`)는 **첫 부여 때 1회만** 찍힌다: 이미 활성이면 `:397-399`의 `if (renewal) return true;`가 로그 앞에서 빠져나간다. 갱신 `RenewUserSummonGrant`(`:417-421`)도, 임대가 살아 있는 동안 두 번째 표면을 열어 생기는 갱신(`:397-399`)도 로그가 없다.
     - 그래서 검색 범위는 **세션 전체다**(1번에서 고른 파일의 처음부터 로그 끝까지). 부여 로그가 0이면 이 경로는 비활성이다 — 여기서 끝난다.
  5. **임대가 살아 있었는가 — 세 표면의 마지막 줄**
     - 갱신자는 세 표면이다: 부채꼴(`Interaction/GearRadialMenuWidget.cs:1290` — 숨김이 아니면 매 프레임 `:1257`) · 정보창(`Interaction/InfoGearIconWidget.cs:855` — 정보창이 열렸거나 부채꼴이 펼쳐진 동안) · 설정창(`Interaction/SettingsWindow.cs:764` — 열려 있는 동안 `:755`). 부채꼴이 닫혀도 다른 창이 열려 있으면 임대는 계속 산다.
     - 규칙: **세 표면 각각에서** 로그 끝 전 마지막 줄이 닫힘일 것. 하나라도 마지막 줄이 열림이면 「살아 있었을 수 있다」다.

     | 표면 | 열림 줄(첫 조각) | 닫힘 줄(첫 조각) |
     |---|---|---|
     | 부채꼴 | `[부채꼴] 펼침 — 진입점=`(`GearRadialMenuWidget.cs:858`, `Expand`가 부른다 `:841`) | `[부채꼴] 접힘(`(`:942`) · `[부채꼴] 전체화면 감지 — 부채꼴과 팝오버를 즉시 거둡니다`(`:1267`) |
     | 설정창 | `[설정창] 열림(`(`SettingsWindow.cs:667`) | `[설정창] 닫힘(`(`:686`) |
     | 정보창 | `[정보창] 열림(`(`CharacterInfoWindow.cs:969`) | `[정보창] 닫힘(`(`:993`) |

     - 부채꼴이 숨는 길은 `Hide()` 세 곳뿐이고(`:1266` · `:1311` · `:1316`, 숨김 상태 대입은 `Hide` 안 `:1398` 한 곳) 그중 둘은 접힘 줄을 남기지 않는다:
       - `:1316` 보통 접힘 — `[부채꼴] 접힘(`(`:942`)이 먼저 찍히고, 접힘 연출(최대 0.26초 `:321-323`) 동안에도 갱신이 돌다가 숨는다. 즉 닫힘 줄은 갱신이 끝나기 **직전에** 찍힌다.
       - `:1266` 억제로 즉시 거둠 — 접힘 줄 없이 `[부채꼴] 전체화면 감지 —`(`:1267`)만 남는다. 이 분기는 `ArePanelsSuppressed`가 참일 때만 들어오므로(`:1263`) 그 순간 임대는 이미 없었다(또는 등급 2다).
       - `:1311` 재앵커 — `[부채꼴] 접힘(앵커 이동)`(예약 `:902`, 접힘 호출 `:908`, 줄 `:942`, 라벨 `:947`) 뒤 같은 프레임에서 `Hide()` 다음 `Expand()`(`:1311-1312`)로 다시 펴고 `[부채꼴] 재앵커 —`(`:1313`)를 남긴다. **판독 영향**: `접힘(앵커 이동)`은 닫힘으로 세지 않는다. 뒤에 펼침·재앵커 줄이 오면 열린 상태가 이어진 것이고, 그 줄이 마지막 부채꼴 줄이면 0.08초(`:322`) 접힘 도중 멈춘 것이라 **열림으로** 센다(접힘 중에도 갱신이 돈다 — `:1257` 게이트는 숨김만 막는다).
     - 설정창 · 정보창은 닫힘 대입이 각 한 곳이고(`SettingsWindow.cs:674` · `CharacterInfoWindow.cs:977`) 그 함수가 곧 닫힘 줄을 찍으므로 로그 없는 닫힘이 없다. 전체화면 자동 숨김으로 닫히는 경로도 같은 함수다(`SettingsWindow.cs:676-677` · `CharacterInfoWindow.cs:979-981` 주석).
  6. **임대가 끝났는가 — 만료 줄과 뒤집힘 줄**
     - 만료 로그 `[표면회수] 사용자 표면 허가를 만료시켰습니다`(`StickmanAgent.cs:430`)를 부르는 곳은 두 곳뿐이다: `TickPanelRetreatEntry`(`:447`, 등급 1 진입 순간만 `:442-447`)와 `Suspend`(`:1827`). 뒤쪽은 `HidesScreenSurfaces`가 참일 때만이라 **표면을 걷는 숨김**, 곧 등급 2 또는 가상 데스크톱 이탈(Windows M-8)로 한정된다(`:356`이 `_isSuspended && !IsUserHiddenOnly`이고 `IsUserHiddenOnly`(`:315`)가 축 4를 포함한다 — macOS는 축 4가 항상 false라 등급 2뿐이다, `:1670` 주석). 게다가 그 순간 임대가 살아 있었을 때만 찍힌다(`:427-429`). 마지막 갱신 뒤 0.5초가 지나 **시간으로 끝나는 만료는 이 줄이 없다**.
     - 대신 `LogPanelRetreatTransition`(`:1696-1710`)이 폴링마다(`:1608` → `ApplySuspendDecision` `:1688`, 사용자 숨김 토글에서도 `:493`) `ArePanelsSuppressed` 뒤집힘을 한 줄로 남긴다. 폴링 주기 기본값은 1.5초다(`Core/StickConfig.cs:1039`, 출하 애셋 `Assets/_Project/Data/DefaultStickConfig.asset:116`).
       - 등급 1이 유지된 채 임대가 시간으로 끝나면 참으로 뒤집혀 `[표면회수] 등급 1 — 전체화면 앱이 떴지만`(`:1704`)이 **다시** 찍힌다. 문구와 달리 새 전체화면 앱이 뜬 것이 아니다 ⇒ 이것이 **시간 만료의 간접 증거다**. 진짜 등급 1 진입과는 같은 폴링의 등급 변화 줄(`[전체화면판정]` — `Platform/Windows/Win32WindowService.cs:1975` · `Platform/MacOS/MacWindowService.cs:1405`, 등급이 바뀔 때만 찍힌다 `:1971` · `:1401`)이 있는지로 가른다. 같은 태그의 `원시 판정이` 줄(`Win32WindowService.cs:1964` · `MacWindowService.cs:1391`)은 등급 변화가 아니다.
       - 부여 직후 다음 폴링에는 `[표면회수] 등급 1 해제 —`(`:1707`)가 찍힌다. **이름과 달리 등급 1이 끝났다는 뜻이 아니다** — 부여 줄(`:401`) 뒤 첫 이 줄이고 사이에 등급 변화 줄이 없으면 임대가 시작된 표시로 읽는다. 이 오독이 「등급 1이 이미 끝났으니 임대는 무관」이라는 반대 결론을 만든다.
       - 예외 ①: `_isSuspended`인 동안에는 줄 없이 기억만 갱신된다(`:1701`) — 등급 2 · 사용자 숨김 · 가상 데스크톱 이탈(`:1671`) 중에는 이 판독이 서지 않는다. 예외 ②: 부여와 시간 만료가 한 폴링 간격 안에 모두 끝나면 뒤집힘이 폴링에 보이지 않아 두 줄이 모두 없다. 예외 ③: 설정 「전체화면 자동 숨김」 토글도 등급 변화 없이 이 두 줄을 만든다(축 3이 그 스위치 아래다 — `:1585-1586`).
     - 판정:
       - 세 표면 모두 마지막 줄이 닫힘이고, 그 뒤 로그 끝 전에 만료 줄 또는 등급 변화 줄 없는 `등급 1 —` 재출력이 있으면 ⇒ 멈춤 전에 임대가 끝났다(로그 기준).
       - 세 표면 모두 닫힘인데 그 뒤 두 줄 중 아무것도 없으면 ⇒ 끝났을 가능성이 높으나 **배제 못 한다**(닫힘 뒤 0.5초와 다음 폴링 사이에 멈췄거나, 예외 ①·②·③).
       - 한 표면이라도 마지막 줄이 열림이면 ⇒ **살아 있었을 수 있다.** 7번으로 간다.
  7. **프레임 페이싱 홀드를 로그로 볼 수 있는가 — 판독 불가(활성 · Calm · 유예를 구분 못 한다)**
     - 홀드 자체를 찍는 로그는 **없다**(`Platform/FramePacing.cs:506` `HoldActiveForInteraction` 본문에 로그 0). 홀드는 등급을 `Active`로 올릴 뿐이고(`Platform/ViewerPresence.cs:465` — 화면 꺼짐 · 전체화면 숨김 · 자리비움이 우선 `:454-463`), 마지막 호출 뒤 0.5초 남는다(`FramePacing.cs:487` · `:492`).
     - 등급별 **분주**와 실제 `renderFrameInterval`은 같지 않다. 보는 사람이 있는 등급(활성 · Calm · 정지 — `ViewerPresence.cs:653-655`)만 분주가 그대로 간격이 되고(`:662`), 자리비움 · 전체화면숨김은 분주가 `vSyncCount`·`targetFrameRate` 쪽으로 들어가 간격에는 나머지만 남는다(기준 vSync가 0이면 `:665-670`, 아니면 vSync 4 상한 뒤 나머지 `:672-677`). 기준값은 `ApplyOnce`가 플랫폼 적용을 먼저 하고(`FramePacing.cs:198-204`) 그 뒤 캡처한 값이다(`:205` → `:557-558`).
     - 출하 기본값의 **실제 간격** 중 양 플랫폼이 같은 칸: 활성 2 · Calm 2(`ViewerPresence.cs:640` · `:641` — `activeTierRenderDivisor`가 코드 상수 `:582`, `Core/StickConfig.cs:3672`, 애셋 `Assets/_Project/Data/DefaultStickConfig.asset:415` 셋 다 2) · 정지 4(`:642`, 기본 `:512`) · 화면꺼짐 1(`:630-633`).
     - **플랫폼이 갈리는 두 칸**: 자리비움은 macOS 2 · Windows 1, 전체화면숨김은 macOS 1 · Windows 1이다(분주 자체는 각각 4 · 2 — `:643` · `:644`). macOS는 기준 vSync가 2라(애셋 `macVSyncInterval: 2`(`:418`) → `FramePacing.cs:1129` clamp · 대입 `:1135`) 자리비움이 `wanted 8 → vSync 4 → 간격 2`, 전체화면숨김이 `wanted 4 → vSync 4 → 간격 1`이다. Windows는 `windowsDisableVSyncForFrameCap: 1`(애셋 `:417` → `FramePacing.cs:1189`)로 기준 vSync가 0이라 `targetFrameRate`만 내려가고 간격은 1로 남는다(`ViewerPresence.cs:665-670`). ⇒ **E-3 증거가 Windows 로그이므로** 간격 1 한 칸에 자리비움 · 전체화면숨김 · 화면꺼짐 · 분주가 1인 환경의 활성이 함께 들어온다.
     - ★ **미확인(실기) 전제**: 위 두 칸의 숫자는 「애셋의 `macVSyncInterval`·`windowsDisableVSyncForFrameCap` 대입이 런타임에 그대로 먹어 기준 `QualitySettings.vSyncCount`가 macOS 2 · Windows 0이 된다」는 전제 위의 **산술**이다(대입 지점 `FramePacing.cs:1135` · `:1189`, 기준값 캡처 `:557-558`). 이 전제는 실기로 확인된 적이 없고, 이 문서의 기존 「미확인(실기)」 목록(1-4)은 macOS OS 동작 전용이라 우리 코드의 대입을 덮지 않는다. **실측 방법**: 같은 로그에서 기동 직후의 `[FramePacing/macOS] 적용 — vSyncCount`(`:1155`) 또는 `[FramePacing/Windows] 적용 — targetFrameRate`(`:1193`, `vSyncCount` 조각 `:1194`) 줄을 읽는다. 그 줄이 없으면 `[프레임시간] 표본` 줄이 같은 값을 `vSyncCount=`로 함께 찍는다(`:1285`). 전제가 깨지면 자리비움 · 전체화면숨김 두 칸과 아래 지표 표 1행을 다시 계산해야 한다.
     - 화면 변경 유예 중에는 등급과 무관하게 `Max(등급 값, 30)`이다(`Platform/DisplayChangeRenderHold.cs:84`, 억제값 30 `:43`).
     - ⇒ 초판의 「활성 등급이면 간격 1 · 표기 없음 쪽」은 **거짓이었다.** 활성과 Calm은 둘 다 2이고 둘 다 `(적응형 절감 중)`이 붙는다(`FramePacing.cs:1287`은 간격이 1보다 큰지만 본다). 간접 지표 셋이 가르는 것과 못 가르는 것:

     | 지표 | 가를 수 있다 | 못 가른다 |
     |---|---|---|
     | `[프레임시간] 표본` 줄(`FramePacing.cs:1283`, 간격 조각 `:1286`) | 그 표본 순간의 간격이 30(유예 중)인지 · 4(정지)인지 · 2인지 · 1인지. 실제로 쓰인 값이다(`:1280`, 쓰기 지점은 `:290` 한 곳 — `:287-290`) | 활성과 Calm(둘 다 2)을 못 가르고, **macOS에서는 자리비움도 2**라 세 등급이 한 칸에 겹친다. **Windows에서는 간격 1**이 자리비움 · 전체화면숨김 · 화면꺼짐 · 분주 1 환경의 활성을 함께 담는다. 단 같은 줄의 `vSyncCount=`·`targetFrameRate=`(`:1285`)가 그 셋을 더 좁힌다 — macOS 자리비움은 vSync 4로, Windows 자리비움·전체화면숨김은 기준 fps를 분주로 나눈 값으로 나타난다(`ViewerPresence.cs:668`; 출하 기준 60이면 각각 15 · 30). 이 좁힘도 바로 위 미확인 전제가 참일 때만 선다. 홀드 유무는 어느 쪽도 못 본다. `(적응형 절감 중)`은 2에서도 붙어 등급 표지가 아니다. 주기가 30초라(`:1220` · `:1272`) 표본 사이 상태를 못 본다. 설정에서 이 로그를 끄면 아예 없다(`:1255`, 기본 켬 — `StickConfig.cs:3738` · 애셋 `DefaultStickConfig.asset:420`) |
     | `[FramePacing/적응형] 등급` 전이 줄(`:721-728`) | 그 전이 순간의 등급 이름(활성과 Calm을 이름으로 가른다)과 `UI홀드=` 값 | 세션 첫 6회 전이 뒤로는 없다(`:719` · `:741`). 적힌 `renderFrameInterval=`은 등급 값이라(`:724`) 유예 반영 전이다. 등급이 안 바뀐 채 걸린 홀드는 줄을 만들지 않는다 |
     | `[FramePacing/적응형] 최근` 요약 줄(`:832`) | 그 구간의 등급별 체류 비율(`:838-841`)과 전이 횟수(`:842`) | 구간 안의 어느 시점인지, 활성이 홀드 때문인지. 첫 요약 60초(`:748`) 뒤 300초 간격(`:742`)이라 마지막 요약과 로그 끝 사이가 비어 있다 |

     - 환경변수로 기본 간격이 바뀐 빌드는 이 표를 다시 짠다: `STICKMATE_ACTIVE_DIVISOR`(`Core/StickConfig.cs:3670` 문구 · `Platform/ViewerPresence.cs:578-579`) · `STICKMATE_STILL_DIVISOR`(`FramePacing.cs:549`).
     - 그래서 이 항목으로 할 수 있는 말은 둘뿐이다. (가) 마지막 표본 줄이 30이면 그 표본은 유예 중이었다 — 비유예 경로에서 간격을 올리는 것은 정지 등급의 분주뿐이고(기본 4 · 환경변수 상한 8 — `ViewerPresence.cs:512` · `:517-518`) 자리비움·전체화면숨김은 오히려 1–2로 내려가므로 30은 유예 말고 만들 수 없다. 게다가 프로덕션에서 `renderFrameInterval` 쓰기는 `FramePacing.cs:290` 한 곳뿐이고 그 줄이 유예를 합산한 값을 쓴다(`:287`). (나) 첫 6회 전이 안에 `UI홀드=걸림`이 있으면 그 시점에 홀드가 있었다. 「멈춤 순간 홀드가 걸려 있었는가」는 **판독 불가다.**

---

## 5. N-8 영향 (`76504c2`의 `docs/verify/DISPLAY_CHANGE_PATH_FILES.md` blob을 그 문서 ②(가)의 `awk` 식을 알려진 제외(X)까지 포함하도록 바꾼 식으로 파싱 — 원식 `$1 ~ /^[ABCDE]$/`, 이 문서 `$1 ~ /^[ABCDEX]$/`)

- 행 수 46(목록 41 + 알려진 제외 5). 양성 대조 `Platform/FramePacing.cs` → A · `Platform/FootholdPoller.cs` → X · 음성 대조 없는 경로 → 빈 값.

| 파일 | 등급 | 이 문서에서의 자리 |
|---|---|---|
| `Platform/MacOS/MacWindowService.cs` | **A** | P-5 판정 기준 모니터 |
| `Platform/MacOS/MacOverlayStateEnforcer.cs` | **A** | 오버레이 모니터·재적합 |
| `Platform/Windows/Win32WindowService.cs` | **A** | Windows 판정(전경 1창) |
| `Platform/Windows/WindowsOverlayStateEnforcer.cs` | **A** | 오버레이 모니터·재적합 |
| `Platform/OverlayMonitorChoicePolicy.cs` · `Platform/OverlayMonitorDirectory.cs` | **A** | 모니터 선택 |
| `Core/StickmanAgent.cs` | **B** | 등급 적용·임대 |
| `Platform/FramePacing.cs` | **A** | 부채꼴 홀드가 닿는 등급·렌더 간격(4절) — 호출될 뿐 수정 후보 아님 |
| `Platform/FootholdPoller.cs` | X | 발판(알려진 제외) |
| `Platform/FullscreenSuspendPolicy.cs` · `Platform/UserSurfaceSummonPolicy.cs` | 목록 밖 | 판정·임대 규칙 |
| `Interaction/SettingsWindow.cs` · `Interaction/CharacterInfoWindow.cs` · `Interaction/InfoGearIconWidget.cs` · `Interaction/GearRadialMenuWidget.cs` | 목록 밖 | 임대 갱신자·창 위치 |

- ⇒ **P-5 수정은 결속 대상(A·B) 파일을 건드린다 → 리더 판단: E-3 증거 결속 유지를 위해 결속 파일 수정 보류**(N-8 원문 `docs/verify/DISPLAY_CHANGE_PATH_FILES.md`는 증거 인정 규칙이다 — 수정하면 N-8 다른 경로, E-3 재시험). 중립 정책(`FullscreenSuspendPolicy.cs`)만으로는 닫히지 않는다 — 플랫폼이 창의 디스플레이 사각형과 오버레이 모니터 사각형을 넘겨야 한다.
- ⇒ **P-6 후보는 목록 밖 파일**이다. 단 표시 변경 신호를 구독하게 만들면 목록 규칙 1·3 편입으로 목록이 바뀐다.

---

**Windows 영향**: 코드 변경 없음. 판정은 전경 창 모니터 기준이라 보조 모니터 감지는 되지만, 전경이 자기 모니터를 정확히 덮지 않는 다른 창이면 오버레이 모니터의 전체화면을 못 본다(발표자 보기는 등급 1 전역 · 등급 0 두 갈래, 코드상 · 실기 미확인 — Ignore B에 없는 갈래). 임대 경로는 macOS와 같다. P0와는 직접 원인 가능성 낮음 — 미배제(4절).
**macOS 영향**: 코드 변경 없음. 판정이 메인 디스플레이뿐이라, 오버레이와 전체화면 앱이 같은 보조 모니터에 있으면 등급 0(게임이면 캐릭터 노출 — 원칙 2 위반 방향, 실기 미확인). 오버레이가 메인이면 보조 모니터 발표 위에는 그림이 없어 원칙 2 문제가 아니다. P0와는 직접 원인 가능성 낮음 — 미배제(4절).

## 출처

- 이 저장소 `76504c2` blob: 본문 각 `파일:줄`.
- 패키지 `com.kirurobo.uniwinc@304f9ba2aa4a`(C#) · 업스트림 `kirurobo/UniWindowController` 태그 `v0.9.8` `VisualStudio/LibUniWinC/libuniwinc.cpp` · `Xcode/LibUniWinC/LibUniWinC.swift` — https://github.com/kirurobo/UniWindowController

---

## 초판(미커밋) 대비 정정 기록

이 파일은 커밋된 적이 없다. `TEAM.md` §5 형식(요지 → 정정 → 근거)으로 적는다. 초판 sha 앞 16자 `cffa4eb96f1af596`. 근거 번호는 리더가 전달한 verify-change 조건부 지적의 순서다 — 필수 1–3, 문구 4–8, 사소 a(Space 비트 전제) · b(범위 밖). 같은 문서 2차 재검증의 지적은 N1–N5, 3차는 N6–N8, 4차는 N9-a·N9-b로 적는다.

| # | 초판 요지 | 정정 | 근거 |
|---|---|---|---|
| 1 | §4·§0 「렌더 간격·창 크기·스왑체인·표시 변경 유예에 닿는 호출이 없다」 · P0 「무관으로 판단」 | **교체**: 임대가 살아 있으면 부채꼴이 매 프레임 홀드를 걸어 등급 · `vSyncCount` · `targetFrameRate` · `renderFrameInterval` 쓰기에 닿는다(등급 0 부채꼴과 같은 동작). 판단을 「직접 원인 가능성 낮음 — 미배제」로 낮추고, 홀드 직접 로그 없음과 간접 지표 판독법, 5절 `FramePacing.cs` A 행, 3-3 결과 한 줄 추가 | 필수 1 · 리더 판정 |
| 2 | §4 판독 1 「멈춤 직전 구간에 부여 로그가 없으면 완전 배제」 | **교체**: 부여 로그는 첫 부여 1회뿐(`:399` 앞 반환 · 갱신 무로그) → 세션 전체 검색, 만료 로그 부재는 배제 못 함, 창 닫힘 로그로 좁힘. 양성 대조 유지 | 필수 2 |
| 3 | §0 판정표 「P-5 원칙 2 영향」 행 · §2 표 「등급 판정 기준 모니터」 행 · §2 「Windows 세계별 원칙 2」 셋째 갈래 · 2-1 표 「B에 없는 갈래」 행 · 2-1 등재 보강 문안 · 끝의 「Windows 영향」 줄 — 「전경이 다른 창이면(발표자 보기 등) 등급 0」 | **교체**: 「전경이 자기 모니터를 정확히 덮지 않는 다른 창일 때 등급 0」, 발표자 보기는 등급 1 전역 / 등급 0 두 갈래 모두 실기 미확인. 「B에 없는 갈래」 유지 | 필수 3 |
| 4 | §0 「`:1150`은 오버레이 크기의 원천이 아니다」 | 주 모니터일 때 목표 크기로 쓰인다(`MacOverlayStateEnforcer.cs:461-467`) | 문구 4 |
| 5 | 1-3 「윗변 발판의 합집합은 `:971-972`」 | 원점 위생 검사 전용 | 문구 5 |
| 6 | 1-1 「비테스트 호출자는 다섯 곳」 · 「발판 조회 폴백 `:967` · `:972`」 | 호출 6줄(용도 5), 줄 목록 명시. `:972`는 원점 위생 검사 폴백 | 문구 6 |
| 7 | §5 「②(가)와 같은 `awk` 식」 | X까지 포함하도록 바꾼 식(원식 대비 명시) | 문구 7 |
| 8 | §5 「수정 착수 금지」 · §4 「억제 로그」(`:430`) | 리더 판단: E-3 증거 결속 유지를 위해 결속 파일 수정 보류(N-8은 증거 인정 규칙) · 「만료 로그」 | 문구 8 |
| 9 | 1-4 Space 비트에 전제 없음 | activation policy가 accessory여야 한다는 전제 추가 | 사소 a |
| 10 | 범위 밖 명시 없음 | 모니터 3대 이상 · 미러링 · OS 목록 조회 실패 폴백은 범위 밖 한 줄 | 사소 b |
| 11 | §4 판독 2 「활성 등급이면 간격 1 · 표기 없음 쪽」 | **교체**: 출하 `activeTierRenderDivisor`가 2라 활성과 Calm이 모두 간격 2에 `(적응형 절감 중)`이 붙고 유예 중에는 30이다 ⇒ 「판독 불가 — 활성 · Calm · 유예를 구분 못 한다」. 간접 지표 셋의 가를 수 있음·없음 표와 표본 30초 주기 추가 | N1 |
| 12 | §4 판독 1 「시간 만료는 로그 없음」 · 「숨김(`Suspend`)에서 만료」 | **보강**: `LogPanelRetreatTransition`이 폴링마다 뒤집힘을 남겨 `:1704` 재출력이 시간 만료의 간접 증거다. `:1707` 「등급 1 해제」가 등급 1 종료가 아니라는 오독 위험과 예외 셋 추가. 「숨김」은 「표면을 걷는 숨김(등급 2)」으로 좁힘 | N2 |
| 13 | §4 판독 4 「`[화면변경유예]` 줄이 1건 이상」 · 판독 3 「시각 순서」 | **교체**: 유예는 분리가 아닌 표시 변경에서도 시작되므로 「로그 끝 직전 마지막 시작 줄 + 같은 번호 해제 없음」 위치 조건으로. 로그에 시각이 없어 줄 순서·로그 끝으로 읽고 시각은 원장에서만 얻는다. 멈춘 세션 식별 단계 신설 | N3 |
| 14 | §4 판독 1 「창 닫힘 로그로 좁힌다」(닫힘 3종만) | **교체**: 갱신자는 세 표면이고 두 번째 표면의 갱신은 무로그 ⇒ 세 표면 각각의 마지막 줄이 닫힘일 것. 열림 로그 3종 인용, 부채꼴의 접힘 줄 없는 `Hide` 두 경로(억제 · 재앵커)의 판독 영향 명시 | N4 |
| 15 | 정정 기록 3행 범위 「§0·§2」 | 실제 변경 위치 여섯 곳을 절 이름으로 적음 | N5 |
| 16 | §4 판독 7 「자리비움 4 · 전체화면숨김 2」 · 지표 1행 「4(정지·자리비움)」 | **교체**: 그 숫자는 분주이고 실제 렌더 간격이 아니다(`ViewerPresence.cs:653-655` · `:662` · `:665-677`, 기준값 캡처 `FramePacing.cs:198-205` → `:557-558`). 실제 간격은 자리비움 macOS 2 · Windows 1, 전체화면숨김 양쪽 1. 플랫폼별로 나눠 적고 macOS 자리비움 2가 활성·Calm과 겹치는 칸임을 명시. 결론 (가)에 비유예 간격 상한과 `FramePacing.cs:290` 단일 쓰기 근거 보강 | N6 |
| 17 | §4 판독 6 · 위 12행 「표면을 걷는 숨김(등급 2)」 | **보강**: `HidesScreenSurfaces`(`:356`)가 `IsUserHiddenOnly`(`:315`)의 축 4를 포함하므로 가상 데스크톱 이탈(Windows M-8)에서도 `:430`이 찍힌다 → 「등급 2 또는 가상 데스크톱 이탈(Windows)」로 넓힘. macOS는 축 4가 항상 false(`:1670`). 판독 예외 ①에 이미 가상 데스크톱이 있어 결론 불변 | N7 |
| 18 | §4 판독 3 「원장에 쓰는 곳은 세 파일뿐」 | **보강**: `FreezeForensics.Record` 한정임을 명시하고 워치독 직접 쓰기(`FreezeWatchdog.cs:179`) · 적합기의 `ObserveTopologyTransition`(`:680` · `:818`) · `RecordTransparencyReassign`(`:340` · `:441`)을 추가. 결론(임대·부채꼴·창·`[표면회수]` 줄은 원장에 없다)은 그대로 | N8 |
| 19 | §4 판독 7 「macOS clamp `FramePacing.cs:1128`」 | **정정**: clamp는 `:1129`이고 `:1128`은 `targetBefore`다(`:1127`이 `vSyncBefore`). `:1135` 대입 인용은 그대로 | N9-a |
| 20 | §4 판독 7 기준 vSync 전제에 미확인 표기 없음 | **보강**: 「애셋 대입이 런타임에 먹어 기준 vSync가 macOS 2 · Windows 0이 된다」를 미확인(실기) 전제로 명시하고 두 칸의 값이 그 위의 산술임을 적음. 실측 방법 3종(`:1155` · `:1193`·`:1194` · `:1285`) 추가. 기존 미확인 목록(1-4)이 macOS OS 동작 전용이라 이 전제를 덮지 않는다는 점도 명시. 아울러 지표 표 1행의 「Windows 간격 1은 못 가른다」가 과했던 것을 같은 줄의 `vSyncCount=`·`targetFrameRate=`로 좁혔다(요청 밖 내 추가 정정) | N9-b |
