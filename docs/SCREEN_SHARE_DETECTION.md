# 화면 공유·발표 중 자동 회피 — 감지 수단 조사 및 설계

작성 `dev-platform` / 2026-09-02 / **조사·설계 전용 라운드. `Assets/` 아래 `.cs` 수정 0건.**
대상 신고: 페르소나 `재현` 실기 재현(macOS) — 카테고리 미선언 앱의 네이티브 전체화면에서
`전체화면숨김 0%`, 톱니·정보창이 그 위에 렌더링, 패널 안 클릭을 전체화면 앱이 못 받음.

---

## 0. 한 줄 결론

> **"화면 공유 중인가"는 macOS·Windows 어디에도 공개 API가 없다(양 벤더 공식 확인).
> 그런데 재현이 실제로 잡은 것은 화면 공유가 아니라 "남의 전체화면 위에 우리 UI가 남는 것"이고,
> 그건 감지 API가 이미 우리 손에 있다 — `CoversDisplay()`가 그 창에서 이미 `true`를 반환하고 있다.
> 막고 있는 것은 그 뒤의 `IsGameCategory()` 게이트 하나뿐이다.**

값싼 길은 **감지를 늘리는 것이 아니라 판정을 두 등급으로 가르는 것**이다(4절 S1).
새 네이티브 코드 0줄, 새 권한 0개, 새 P/Invoke 0개.

---

## 1. ★ 먼저 정정 — 지시서의 전제 1건이 이 저장소에 없다

> 지시서: *"이 저장소에 이미 기록된 사실: macOS에 공개 화면캡처 감지 API는 없고,
> `CGDisplayIsInMirrorSet`이 유일하게 살아 있는 부분집합이다."*

**그 기록은 이 저장소에 없다.** 전수 검색 결과:

```
$ grep -rn "MirrorSet|SetWindowDisplayAffinity|DisplayAffinity|NOT_ENOUGH_MEMORY|WDA_EXCLUDEFROMCAPTURE" \
        docs/ Assets/_Project/Scripts/ Tasklist.md process.md
(0건)

$ grep -rni "mirror" docs/ Tasklist.md process.md
→ 전부 DesktopIconMirrorDirector / mirrordrift.py (설계 거울) — 디스플레이 미러링과 무관

# ★ 양성 대조 (0건 판정이 grep 고장이 아님을 증명)
$ grep -rn "CGDisplayIsAsleep" docs/ Assets/_Project/Scripts/
Assets/.../MacViewerPresenceService.cs:38   ← 히트 5건. grep은 정상 동작한다.
```

이 저장소가 화면 공유에 대해 실제로 기록해 둔 것은 **"조사한 적이 없다"**는 사실뿐이다:

| 기록 위치 | 내용 |
|---|---|
| `docs/strategy/MARKET_PERSONAS_PURCHASE.md:33` | F2 — "화면 공유·녹화 감지 기능이 없다. `IViewerPresenceService`가 아는 것은 '디스플레이가 잠들었는가'뿐" |
| `docs/strategy/ROADMAP.md:135` | R-1 — "**미확인** — 플랫폼별 감지 API 조사 필요" |
| `docs/strategy/ROADMAP.md:432` | B7 — `dev-platform` 배정 예정 항목 |
| `docs/marketing/personas/R5_화면공유.md:32` | "자동 숨김은 전체화면 **게임** 판정이다 — 회의·발표 앱은 그 판정에 안 걸린다" |

→ **아래 2·3절은 "다시 조사"가 아니라 이 저장소의 첫 조사다.** 결론적으로 지시서의 기억은
**맞는 방향이었고**(공개 API 없음 — 2절 A1에서 Apple DTS 인용으로 확인), 다만 그것이
**이 저장소에 적혀 있던 적은 없다.**

한편 지시서의 다른 전제 — *"`SetWindowDisplayAffinity`가 레이어드 창에서
`ERROR_NOT_ENOUGH_MEMORY(8)`로 실패한 이력"* — 은 **이 저장소에는 기록이 없지만
Microsoft 자신의 문서에서 사실로 확인됐다**(3절 W1). 우리 Windows 출하 형상이 정확히 그
실패 조건에 해당한다는 것도 코드로 확인했다.

---

## 2. ★ 문제를 셋으로 가른다 (지시서 3번 항목)

재현의 신고는 한 덩어리로 왔지만 **원인도 처방도 서로 다른 세 축**이 섞여 있다.
이걸 안 가르면 "권한을 요구하는 감지"로 "권한이 필요 없는 문제"를 푸는 사고가 난다.

| 축 | 현상 | 진짜 조건 | 감지 가능성 |
|---|---|---|---|
| **A. 전경 점유** | 남의 전체화면 창 **위에** 우리 톱니·정보창이 남고, 그 사각형이 **클릭을 먹는다** | "다른 앱이 화면을 통째로 덮고 있다" | ✅ **이미 우리 손에 있다.** `FullscreenGeometry.CoversDisplay()`가 그 창에서 `true`를 반환 중 |
| **B. 화면 공유·녹화** | 우리 캐릭터·UI가 **남의 화면에 송출**된다 | "누군가 이 디스플레이를 캡처 중" | ❌ **양 OS 모두 공개 API 없음**(2절 A1 / 3절 W0) |
| **C. 미러링·발표** | 프로젝터/AirPlay로 **복제 출력** 중 | "디스플레이가 미러 세트에 속함" | ⚠️ macOS만 부분 가능(`CGDisplayIsInMirrorSet`). Windows는 사실상 불가 |

### ★ 재현이 잡은 것은 A축이다 — B축이 아니다

증거 3건(전부 신고서 본문에서 직접 나온다):

1. **로그가 A축을 말한다.** `[FramePacing/적응형] … 전체화면숨김 0%` 는 "캡처를 못 봤다"가 아니라
   "**전체화면 판정이 false였다**"다. 화면 공유 여부는 이 로그에 애초에 들어오지 않는다.
2. **대조 실험이 A축만 뒤집었다.** `LSApplicationCategoryType=...action-games`를 붙였더니 정상 작동 —
   **화면 공유 상태는 아무것도 안 바뀌었는데** 증상이 사라졌다. 즉 증상의 원인 변수는
   공유 여부가 아니라 **카테고리 게이트**다.
3. **클릭 방해는 공유와 무관하다.** 우리 클릭 차단막이 발표자 자신의 로컬 클릭을 먹는 것이고,
   원격 참가자와는 아무 관계가 없다. 이 피해는 **공유를 끄고 혼자 발표해도 똑같이 난다.**

### 그래서 "게임 판정 확장"은 몇 %인가 — **0%다. 하지만 판정 *분할*은 대부분이다.**

지시서의 질문에 두 가지 읽기가 있어 각각 답한다.

**읽기 (i) — 게임 카테고리 목록을 늘린다: 커버리지 0%.**
재현의 창은 카테고리가 **미선언**이었다. `IsGameCategory(null) == false`는 목록 크기와 무관하다.
`entertainment`를 추가하든 20종을 추가하든 **미선언은 영원히 안 잡힌다.**
게다가 Zoom·Teams·Keynote·PowerPoint는 게임이 될 일이 없다 — 방향 자체가 틀렸다.

**읽기 (ii) — 게임 조건을 없애고 기하 판정만 쓴다: 커버리지 100%, 그러나 금지.**
이건 2026-08-31 이전 상태로의 완전한 회귀다. 사용자가 직접 신고한
*"엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가 없어져버림"* 이 그대로 되살아난다.
`FullscreenSuspendPolicy.cs`의 클래스 문서와 `Win32WindowService.cs:1755-1762`가 둘 다
이 처방을 명시적으로 반증해 뒀다. **음성 대조가 이미 존재하는 처방은 다시 시도하지 않는다.**

**읽기 (iii) ★ 채택 권고 — 판정을 두 등급으로 *분할*한다: 클릭 방해 100%, 면적 피해 50.4%p 즉시 제거.**

| 등급 | 조건 | 걷는 것 | 남기는 것 | 오탐 비용 |
|---|---|---|---|---|
| **1 — 패널 회수** | 기하 일치 **만**(게임 여부 무관) | 톱니 · 부채꼴 · 정보창 · 설정창 · 팝오버 · 포스트잇 · **클릭 차단막** | **캐릭터 본체** | 열어둔 창이 닫힌다. 복구 = 톱니 1클릭 |
| **2 — 전면 숨김** | 기하 일치 **AND** 게임 | 위 전부 **+ 캐릭터 렌더러** | — | 2026-08-31 신고 버그. **지금 그대로 유지** |

검산 — 신고된 4개 증상 중 등급 1이 지우는 것:

| 신고 항목 | 등급 1로 해결되는가 | 근거 |
|---|---|---|
| 톱니·정보창이 전체화면 위에 렌더링 | ✅ | 두 표면 모두 이미 `IsSuspended`를 폴링해 스스로 내려간다(`InfoGearIconWidget.cs:478`, `CharacterInfoWindow.cs:732`) |
| 패널 안 클릭을 전체화면 앱이 못 받음 | ✅ **100%** | 차단막은 창에 종속이고 *"창이 닫히면 차단막은 반드시 꺼진다"*(`CharacterInfoWindow.cs:74`) |
| 정보창 877×853 = 면적 50.4% | ✅ **50.38%p** | 877×853 = 748,081 / 1512×982 = 1,484,784 → **0.5038**. 세로 853/982 = **0.8686**. 재현의 실측치와 일치 |
| `전체화면숨김 0%`(캐릭터 몸) | ❌ 의도적으로 남긴다 | 이게 등급 2이고, 여기를 건드리면 위의 반증된 처방 (ii)가 된다 |

→ **신고된 피해 중 "클릭 방해"는 전부, "화면 점유"는 면적 기준 50.4%p가 등급 1로 사라진다.**
남는 것은 캐릭터 본체 실루엣 하나뿐이고, **그건 사용자가 이 앱을 산 이유**다.
(캐릭터 본체의 실제 점유 면적은 **이번 라운드 미측정** — 재현이 정보창을 잰 것과 같은 방법으로
한 번 재면 이 표가 완성된다.)

### 등급 1이 실제로 무엇을 잡는가 — 플랫폼별

| 발표 상황 | macOS | Windows |
|---|---|---|
| Keynote 발표 모드 | ✅ `CoversDisplay` true (상단 스트립 허용 경로) | — |
| PowerPoint 슬라이드쇼 | ✅ | ✅ `MatchesExactly` — 슬라이드쇼는 모니터 사각형과 정확 일치 |
| 브라우저 F11 / 동영상 전체화면 | ✅ | ✅ |
| Zoom·Teams 네이티브 전체화면 | ✅ (= 재현이 잡은 그 창) | ✅ |
| **창 단위 공유 중 우리 창 위에 아무것도 없음** | ❌ 안 걸림 = **정답**(재현 확인: 창 공유엔 우리가 안 찍힌다) | 동일 |

Windows의 `MatchesExactly`가 상단 도킹 작업표시줄 환경에서 최대화 창을 오판할 위험은 **없다** —
최대화는 `rcWork`를 따르고 `rcMonitor`와 다르다. 등급 1을 켜도 그 방벽은 그대로다.

---

## 3. macOS 후보 전수 — (a)무엇을 잡나 (b)놓치는 것 (c)권한 (d)오탐

### A1. ★ 공개 API 자체가 없다 — Apple 공식 확인 2건

> **Quinn "The Eskimo!" (Apple DTS)**: *"The short answer is that, no, there's no good way to
> identify which GUI login sessions have screen sharing attached to them."*
> — [Apple Developer Forums #46546](https://developer.apple.com/forums/thread/46546)

> **Apple DTS Engineer (2025, macOS 15.4 문의에 대한 답)**: *"At this time there are no public APIs
> for preventing screen capture."*
> — [Apple Developer Forums #792152](https://developer.apple.com/forums/thread/792152)

두 번째 인용은 **차단**에 대한 답이지만, 같은 스레드가 `NSWindow.sharingType = .none`이
**macOS 15.4+에서 ScreenCaptureKit에 무시된다**는 것을 확인해 준다 — 즉 "감지 못 하면 차라리
캡처에서 빠지자"는 우회로도 macOS에서는 **닫혔다**(A6).

### 후보표

| # | 수단 | (a) 실제로 잡는 것 | (b) 놓치는 것 | (c) 권한 | (d) 오탐 | 판정 |
|---|---|---|---|---|---|---|
| **A2** | `CGDisplayIsInMirrorSet(displayID)` | 디스플레이가 **소프트웨어/하드웨어 미러 세트**에 속함 = 프로젝터 복제, AirPlay 미러링, 시스템 설정의 "디스플레이 미러링" | **Zoom·Teams·OBS의 화면 공유를 전혀 못 잡는다**(ScreenCaptureKit은 미러 세트를 만들지 않는다). 확장 데스크톱 모드 발표도 못 잡는다 | ✅ **불필요** (Quartz Display Services, 순수 디스플레이 구성 조회) | 낮음. 다만 "미러링 중 = 발표 중"은 아니다(집에서 TV에 미러링해 영화 보는 사람이 캐릭터를 잃는다) | ⚠️ **C축 전용, 무료.** B축 해결 아님 |
| **A3** | `NSScreen.screens` / `CGGetActiveDisplayList` 개수 변화 | 디스플레이가 붙고 떨어짐 | 미러링인지 확장인지 자체로는 모름 | ✅ 불필요 | 높음(외장 모니터 상시 사용자에게 상시 참) | ❌ 단독 무용 |
| **A4** | `ScreenCaptureKit` / `SCShareableContent` | **우리가 캡처할 수 있는 대상 목록.** "누가 캡처 중인가"는 알려주지 않는다 | 애초에 질문에 답하지 않는 API | 🔴 **Screen Recording TCC 권한 필요** | — | ❌ **권한만 태우고 답을 못 준다** |
| **A5** | `CGDisplayStream` | 위와 동일. macOS 13.0에서 deprecated | 동일 | 🔴 동일 | — | ❌ |
| **A6** | `NSWindow.sharingType = .none` (감지 회피) | 과거: 우리 창을 캡처 결과에서 제외 | **macOS 15.4+에서 ScreenCaptureKit에 무시됨**(Apple DTS 확인). macOS 15 미만에서만 유효 | ✅ 불필요 | — | ❌ **막다른 길.** 다만 macOS 14 이하에서 무료로 얹을 수는 있음(가치 낮음) |
| **A7** | `CGSIsScreenWatcherPresent` (CGSInternal / SkyLight) | 화면 감시자 존재 여부(주장) | **비공개 SPI.** 헤더 없음, 계약 없음, 버전마다 사라질 수 있음 | ✅ 불필요 | 미확인 | 🔴 **금지.** Mac App Store 심사 리스크 + 이 저장소의 "확인 못 한 것은 미확인" 규칙 정면 위반 |
| **A8** | `screencapture`/`screencap` 프로세스 + `~/Library/ScreenRecordings/` 열린 파일 관측 | **macOS 내장 화면 기록만** | Zoom·Teams·OBS·QuickTime 전부 못 잡는다 = 우리가 노리는 케이스 100% 미탐지 | ✅ 불필요(단 프로세스 열거) | 낮음 | ❌ 커버리지 ≈ 0 |
| **A9** | 시스템 화면공유 표시기(보라 메뉴바 아이콘) 관측 | 이론상 "누군가 캡처 중" | 이 아이콘은 `ControlCenter`가 소유한 메뉴바 창이다. 어느 창인지 가르려면 **`kCGWindowName`(창 제목)**이 필요한데, **그건 Screen Recording 권한이 있어야 딕셔너리에 들어온다** | 🔴 **권한 필요**(제목 없이는 위치·개수 휴리스틱뿐 — 메뉴바 항목이 하나만 바뀌어도 깨짐) | 매우 높음 | ❌ **권한을 태우고도 깨지기 쉽다** |
| **A10** | `CGWindowListCopyWindowInfo`로 회의 앱 창 지문 탐지 (예: Zoom의 "공유 중" 초록 툴바) | Zoom이 공유를 시작하면 뜨는 **작고 항상 위인 창**의 존재 | Zoom 버전·언어·플랫폼마다 다름. Teams·Meet·Webex·Discord 각각 별도 지문 필요. **공유 툴바를 숨긴 사용자**는 미탐지 | ✅ 불필요 (우리가 이미 하는 조회. **창 제목은 안 읽는다** — 아래 확인) | 중간(회의 앱 UI가 바뀌는 날 조용히 죽거나 오탐) | ⚠️ **유지보수 부채가 커버리지를 넘는다.** S3 이하 |
| **A11** | `NSWorkspace.runningApplications`로 회의 앱 실행 여부 | "Zoom이 켜져 있다" | **"켜져 있다 ≠ 공유 중이다."** 하루 종일 Slack 옆에 Zoom을 띄워두는 사람이 다수 | ✅ 불필요 | 🔴 **매우 높음** — 이 안은 오탐으로 캐릭터를 상시 숨긴다 = 제품 자체를 끄는 것 | ❌ **금지** |

### 우리가 지금 읽는 창 필드 — 권한 0 확인 (양성 대조 포함)

```
$ grep -n "_key.* = CFStringCreateWithCString" MacWindowService.cs
kCGWindowLayer / kCGWindowBounds / kCGWindowOwnerPID / kCGWindowOwnerName /
kCGWindowNumber / kCGWindowAlpha / kCGWindowIsOnscreen / LSApplicationCategoryType   ← 8건(양성 대조 성립)

$ grep -n "kCGWindowName" MacWindowService.cs
(0건)
```

`kCGWindowName`(= 창 **제목**)은 Screen Recording 권한이 없으면 딕셔너리에서 **빠진다**.
우리는 그 키를 읽지 않는다 → **현재 형상은 권한 0이 맞고, A9/A10 중 제목을 읽는 안은
그 자산을 태운다.** 이 사실이 A9을 기각하는 결정적 근거다.

---

## 4. Windows 후보 전수

### W0. 결론 — Windows도 "내 창이 캡처되는가"를 알 방법이 없다

Windows.Graphics.Capture / Desktop Duplication은 **캡처당하는 쪽에 아무 통지도 하지 않는다.**
Microsoft가 제공하는 것은 감지가 아니라 **제외**(`SetWindowDisplayAffinity`)뿐이고,
Raymond Chen 자신이 그것을 *"just an obstacle, not a security measure"* 로 못박았다.

| # | 수단 | (a) 실제로 잡는 것 | (b) 놓치는 것 | (c) 권한 | (d) 오탐 | 판정 |
|---|---|---|---|---|---|---|
| **W1** | `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` (감지 회피) | 우리 창을 캡처 결과에서 제외. Zoom·Teams·Meet·Webex·OBS가 전부 같은 OS 표면을 읽으므로 원리상 전부에 적용 | 🔴 **우리 창에는 못 건다.** 아래 참조 | ✅ 불필요 (Win10 2004+) | — | 🔴 **구조적으로 막혀 있다** |
| **W2** | `GetWindowDisplayAffinity` | 현재 affinity 값 조회 | 감지 기능 아님 | ✅ | — | 진단용만 |
| **W3** | `GetSystemMetrics(SM_REMOTECONTROL)` (0x2001) | 문서 원문: *"determine if the current Terminal Server session is being **remotely controlled**"* — 즉 **RDP 섀도잉**(tsadmin/shadow.exe) | Zoom·Teams·OBS **전혀 못 잡음**. 기업 IT 원격지원 시나리오 한정 | ✅ 불필요 | 매우 낮음 | ⚠️ **무료. 커버리지는 좁지만 정확하다** |
| **W4** | `GetSystemMetrics(SM_REMOTESESSION)` (0x1000) | *"the calling process is associated with a Terminal Services client session"* = **우리가 RDP 안에서 돌고 있다** | 로컬에서 돌면서 화면만 공유되는 경우 전부 미탐지 | ✅ 불필요 | 낮음 | ⚠️ 무료. 다른 축(원격 데스크톱)의 사실 |
| **W5** | `EnumDisplayMonitors` 개수 > `GetSystemMetrics(SM_CMONITORS)` | MS 문서 원문: `SM_CMONITORS`는 보이는 모니터만 세고, `EnumDisplayMonitors`는 *"invisible pseudo-monitors that are associated with **mirroring drivers**"* 도 센다 → **미러 드라이버 존재** = macOS `CGDisplayIsInMirrorSet`의 Windows 대응물 | 🔴 미러 드라이버는 **레거시**다. Windows 8 이후 화면 캡처는 Desktop Duplication / Graphics Capture로 옮겨갔고 **가상 모니터를 만들지 않는다** → 현대 Zoom·Teams 커버리지 ≈ **0** | ✅ 불필요 | 낮음 | ⚠️ **무료지만 사실상 죽은 신호.** 넣어도 손해는 없음 |
| **W6** | `WTSRegisterSessionNotification` | 세션 연결/해제(`WM_WTSSESSION_CHANGE`) | 화면 공유와 무관. 그리고 🔴 **HWND에 등록해 창 프로시저로 받는 API다** — 우리 HWND는 UniWindowController 네이티브 플러그인 소유라 창 프로시저를 가로챌 수 없다. `WindowsViewerPresenceService.cs:18-26`이 `WM_POWERBROADCAST`에서 **이미 같은 벽에 막힌 기록**을 남겨 뒀다 | ✅ | — | ❌ **같은 구조적 벽. 재발 조사 불필요** |
| **W7** | 회의 앱 프로세스/창 지문(`EnumWindows` + 클래스명) | A10과 동일 성격 | 동일 | ✅ | 중간~높음 | ⚠️ S3 이하 |

### ★ W1이 왜 우리에게 막혀 있는가 — 지시서의 "이력"은 사실이다 (외부 1차 출처로 확인)

Microsoft 포럼의 **채택 답변**(MSDN Community Support, 2019-05-30):

> *"Layered windows come in two flavors: `SetLayeredWindowAttributes` windows and
> `UpdateLayeredWindow` windows. … **Windows currently does not support monitor affinity for
> this type of layered window.** The error code is unfortunately wrong and confusing in this case.
> There isn't any problem with not having enough memory, but, rather, **the properties of the
> window don't look like what the monitor affinity code expects.**"*

그리고 Microsoft Q&A(Windows 11)에는 Microsoft 엔지니어 Junjie Zhu가 **미해결**로 확인한
후속 보고가 있다(`ChangeWindowTreeProtection()` in `win32kfull.sys`, 수정 일정 미정).

**우리 출하 형상이 정확히 그 실패 조건이다.** `LayeredHybridPolicy.cs:15-25`에 실측 기록이 있다:

```cpp
// UniWinC libuniwinc.cpp — SetClickThrough()
if (bTransparent) { exstyle |= WS_EX_TRANSPARENT; exstyle |= WS_EX_LAYERED; ... }
else              { exstyle &= ~WS_EX_TRANSPARENT;  /* 레이어드는 일부러 안 지운다 */ }
```

→ 원칙 2로 **클릭 관통이 기본 ON**이므로 Windows 출하 형상은 **항상 `WS_EX_LAYERED`가 붙어 있다**.
게다가 그 레이어드는 `CreateWindowEx`가 아니라 `SetWindowLong`으로 나중에 붙고
`SetLayeredWindowAttributes`가 뒤따르지 않는데, 같은 포럼 스레드에서 재현자가
**바로 그 조합이 실패를 재현한다**고 확인했다.

그리고 이 저장소는 그 레이어드를 **뗄 수 없다** — `.claude/agents/dev-platform.md:30-31`:
*"해소기는 검증 실패로 영구 비활성됐다(`WS_EX_TRANSPARENT` 단독으로는 관통이 성립하지 않는 환경)."*

**결론: W1은 "언젠가 시도해 볼 것"이 아니라 구조적으로 닫힌 경로다.**
열려면 클릭 관통 방식 자체를 갈아엎어야 하고, 그건 원칙 2를 건 도박이다.
**실기 미확인**(이 머신에 Windows 없음)이지만, 우리 창 스타일이 실패 조건에 해당한다는 것은
**소스로 확인**했고 실패 원인은 Microsoft 자신이 설명해 뒀다.

---

## 5. ★ 피해 크기가 감지 수단의 비용 상한을 정한다 (지시서 5번 항목)

재현이 좁혀 준 사실 2건이 이 판단의 전제다:

- 우리는 **남의 창 제목·파일명을 한 글자도 그리지 않는다**(`WindowTheftDirector`/`DesktopIconMirrorDirector` 렌더 0건).
  macOS 쪽 코드에서도 `kCGWindowName` 참조 0건을 이번 라운드에 재확인했다(3절 하단).
- **창 단위 공유에는 우리가 안 찍힌다. 화면 단위 공유에서만** 문제다.

→ **노출되는 것은 캐릭터 · 톱니 · 정보창(이름/레벨/장비)뿐이다. 프라이버시 유출이 아니다.**
피해는 **① 창피함 ② 클릭 방해** 두 가지고, 둘 다 **복구 가능하고 되돌릴 수 없는 손실이 없다.**

### 비용 상한 판정

> **이 피해 크기에는 "새 권한"도 "잦은 오탐"도 값이 맞지 않는다.**

| 지불하려는 비용 | 얻는 것 | 판정 |
|---|---|---|
| **Screen Recording TCC 권한** (A4/A9) | 여전히 "누가 캡처 중인가"는 못 얻는다(A4는 답 자체를 안 준다, A9는 깨지기 쉽다) | 🔴 **불가.** 상품 전략이 확인한 **"권한 0 · 네트워크 0"** 차별점을 태우는데 **얻는 것이 0이다.** 게다가 첫 실행에 시스템 설정 재시작 유도 다이얼로그가 뜬다 — `persona-newcomer`의 첫인상 지표를 정면으로 깬다 |
| **비공개 SPI** (A7) | 불확실한 감지 | 🔴 **불가.** Mac App Store 심사 + macOS 업데이트마다 조용히 죽는 코드. 이 저장소는 "조용한 실패"에 이미 여러 번 당했다 |
| **회의 앱 지문**(A10/W7) | 부분 감지 | ⚠️ **조건부.** 유지보수 부채가 커버리지를 넘고, 지문이 죽으면 **아무도 모른다**(조용한 실패). 넣는다면 반드시 "지문 미탐지"를 로그로 자백하게 |
| **오탐으로 캐릭터를 자주 숨김** (A11) | — | 🔴 **불가.** 24시간 상주 앱에서 "가끔 없어지는 캐릭터"는 기능 실패가 아니라 **제품 실패**다. 2026-08-31 사용자 신고가 정확히 그 형태였다 |
| **판정 분할**(2절 iii) | 클릭 방해 100% + 면적 50.4%p | ✅ **권한 0, 네이티브 코드 0줄, 오탐 비용 = "창이 닫힌다"** |
| **`CGDisplayIsInMirrorSet` 1콜** (A2) | 프로젝터 미러 발표 | ✅ **권한 0, P/Invoke 1개.** C축만. 켜고 끌 수 있게 |

**즉 이 신고는 "감지를 사는" 문제가 아니라 "이미 가진 감지를 다르게 쓰는" 문제다.**

---

## 6. 비용 대비 커버리지 — 단계 권고

| 단계 | 무엇 | 새 네이티브 | 새 권한 | 커버리지 | 오탐 위험 | 권고 |
|---|---|---:|---:|---|---|---|
| **S0** | 사용자 명시 숨김(⌃⌥⌘K) — **이미 있다**(`StickmanAgent.SetUserHidden`, 축 2) | 0 | 0 | 100%(사용자가 누르면) / 0%(안 누르면) | 0 | ✅ **완료.** 다만 `persona-newcomer` 관점의 **발견가능성**은 별건 — `ux-designer` 배정 대상 |
| **S1** ★ | **판정 2등급 분할**(2절 iii). 기하 일치만으로 **패널·차단막 회수**, 캐릭터는 게임일 때만 | **0줄** | 0 | **A축**: 클릭 방해 **100%**, 화면 점유 면적 **50.4%p**. Keynote·PPT 슬라이드쇼·F11·Zoom 전체화면 전부 | 창이 닫힘(1클릭 복구) | ✅ **1순위. 재빌드만으로 된다** |
| **S2** | `CGDisplayIsInMirrorSet` → **C축**(프로젝터/AirPlay 미러 발표) 시 S1과 같은 등급 1 적용 | macOS P/Invoke 1개 | 0 | C축 전용. B축 0% | 집에서 TV 미러링하는 사용자 | ⚠️ **2순위, 설정에서 끌 수 있게.** Windows 대응은 W5뿐이고 사실상 죽은 신호 — **패리티 갭을 `Assert.Ignore`(사유 포함)로 명시** |
| **S3** | 회의 앱 "공유 중" 창 지문(A10/W7) | 양 플랫폼 | 0 | Zoom·Teams 부분. 버전마다 재조정 | 중간, **조용히 죽는다** | 🟡 **보류.** 하려면 지문 실패를 반드시 자백하는 로그와 함께 |
| **S4** | Screen Recording 권한을 쓰는 모든 안(A4/A9) | 양 플랫폼 | 🔴 1개 | **그래도 B축 미해결** | — | 🔴 **기각.** 5절 |
| **S5** | 비공개 SPI(A7) / `WDA_EXCLUDEFROMCAPTURE`(W1) | — | 0 | — | — | 🔴 **기각.** 각각 심사 리스크 / 구조적 차단(4절 W1) |

### B축(진짜 화면 공유 감지)에 대한 정직한 결론

> **S1~S3을 다 해도 "Zoom으로 화면 공유 중"은 알 수 없다. 양 OS가 그 API를 제공하지 않는다.**
> 그 축에 남는 유일한 정직한 답은 **S0(사용자가 직접 누르는 단축키)** 이고,
> 그래서 이 문제의 다음 병목은 **감지가 아니라 S0의 발견가능성**이다 — `ux-designer` 소관.
>
> 마케팅·스토어 문구에 **"화면 공유 자동 감지"는 절대 쓰면 안 된다**(즉시 거짓 소재).
> 쓸 수 있는 정직한 문구는 **"발표·전체화면 앱 위에서는 스스로 물러납니다 + 원할 때 단축키 하나로 즉시 숨김"** 이다.

---

## 7. S1 구현 설계 — 다음 라운드 인계 (코드 미작성)

**리더가 배정할 때 이 절만 읽으면 되도록 적는다. 이번 라운드는 `.cs` 0건 수정.**

### 원칙 준수

- 정책은 **`Platform/` 중립**에 둔다. `Platform/MacOS/` 안에 두면 Windows가 못 부른다
  (`FullscreenSuspendPolicy.cs` 클래스 문서의 실제 사고 사례).
- 플랫폼 코드는 **사실 조회만** — 기하 일치 여부와 게임 여부를 **각각** 올려 보낸다.

### 변경 지점 (4곳, 전부 기존 파일)

| # | 파일 | 변경 |
|---|---|---|
| 1 | `Platform/FullscreenSuspendPolicy.cs` | `enum ForeignFullscreenTier { None, PanelsOnly, Full }` + 순수 합성 함수 `Resolve(bool coversDisplay, bool isGame)` 신설. **EditMode가 네이티브 없이 전 분기 검증** |
| 2 | `Platform/IPlatformWindowService.cs` | `IsFullscreenAppActive()`를 유지하되(호환) `ForeignFullscreenTier GetForeignFullscreenTier()` 추가. 기존 bool은 `== Full`로 정의 |
| 3 | `Platform/MacOS/MacWindowService.cs` `EvaluateFullscreen` | 지금 `return isGame;` 한 줄이 두 사실을 하나로 뭉갠다 → **두 사실을 따로 반환.** `FullscreenVerdictDebouncer`를 **축마다 하나씩(2개)** 둔다 |
| 4 | `Platform/Windows/Win32WindowService.cs` `EvaluateFullscreen` | 위와 **같은 라운드에** 대칭 적용. `MatchesExactly`는 그대로(관용 켜지 않는다 — 이건 결정이지 갭이 아니다) |

### 소비자 측 (`Core`/`Interaction` — `coder` 소관, `dev-platform` 아님)

- `StickmanAgent`: 축 3 `_foreignFullscreenPanelRetreat` 신설. **`IsSuspended`의 의미는 바꾸지 않는다**
  (캐릭터 렌더러가 그걸 본다). 새 프로퍼티 `ArePanelsSuppressed => IsSuspended || _foreignFullscreenPanelRetreat`.
- 패널 6종(`InfoGearIconWidget` / `GearRadialMenuWidget` / `CharacterInfoWindow` / `SettingsWindow` /
  `FocusSessionPopover` / `TodoPostItWidget`)의 폴링 대상을 `IsSuspended` → `ArePanelsSuppressed`로.
  **캐릭터 렌더러·`RunawayDirector`·`FocusWatchDirector` 등은 손대지 않는다.**
- ★ **`AppSettingsModel.AutoHideOnFullscreen` 토글이 축 3까지 함께 끄지 않도록 주의.**
  `StickmanAgent.cs:57-68`이 "축을 한 조건식에 얹으면 토글 하나가 둘을 동시에 끈다"는 사고를
  이미 기록해 뒀다. **같은 사고를 축 3으로 반복하지 말 것.**

### ★ 인계 시 주의 — 동시 진행 라운드가 같은 파일을 쪼개는 중이다

이 라운드 도중 다른 라운드가 `Interaction/CharacterInfoWindow.cs`를 **7개 partial로 분할**했다
(`*.Cards.cs` / `.Input.cs` / `.Inventory.cs` / `.Layout.cs` / `.Shop.cs` / `.Tabs.cs` / `.TestApi.cs`, 전부 미추적).
`Core/StickmanAgent.cs`·`Interaction/SettingsWindow.cs`·`Interaction/FocusSessionPopover.cs`도 수정 중이다.

→ **위에 적은 줄 번호는 이 라운드 시점의 좌표다. S1 착수 라운드는 줄 번호를 믿지 말고
`grep -rn "IsSuspended" Assets/_Project/Scripts/Interaction/`로 폴링 지점을 다시 찾아라.**
(이 라운드는 `.cs`를 한 줄도 건드리지 않았다 — 위 변경은 전부 다른 라운드 소유다.)

### 테스트 (신설 3건 제안)

| 테스트 | 검증 |
|---|---|
| EditMode `ForeignFullscreenTierTests` | `Resolve()` 4분기 진리표. **프로덕션 상수 하드코딩 금지**(참조로 검증) |
| EditMode `PlatformParityAuditTests` 추가 항목 | 양 플랫폼 소스 파일이 **둘 다** 2축을 올려 보내는가(**타입이 아니라 소스 파일을 읽도록** — 활성 빌드 타깃 사각지대) |
| PlayMode `PanelRetreatOnForeignFullscreenTests` | 등급 1에서 **차단막 콜라이더가 꺼지고 캐릭터 렌더러는 켜져 있다**. ★ **벽시계(초) 예산**으로 대기(프레임 수 금지) |

---

## 8. 모바일(iPad/iPhone) 각주 — 여기서만 공개 API가 있다

데스크톱과 정반대로, **iOS에는 공식 감지 API가 있다**:

- `UIScreen.isCaptured` — *"screen is being recorded, mirrored, or using AirPlay"* (iOS/iPadOS 11.0+)
- `UIScreen.capturedDidChangeNotification` — 상태 변화 통지. **폴링이 아니라 이벤트**
- **권한 불필요**

Mac Catalyst에서도 쓸 수 있지만(13.1+), 이 앱은 AppKit 네이티브라 **해당 없음**이고,
Catalyst로 갈아타는 것은 오버레이·창 열거를 전부 버리는 일이라 논외다.
"스크린샷 백드롭 모드"(`docs/ARCHITECTURE.md` 0-1절) 착수 시 **이 API를 처음부터 넣을 것.**
(단 최신 SDK에서 deprecated 표기가 보인다 — **착수 시점에 재확인 필요, 이번 라운드 미확인.**)

---

## 9. 미확인 목록 (추측으로 메우지 않는다)

| # | 미확인 항목 | 왜 확인 못 했나 |
|---|---|---|
| 1 | `CGDisplayIsInMirrorSet`의 **deprecation 여부** | Apple 문서 페이지가 JS 렌더라 본문 취득 실패. 함수 존재·의미는 다중 출처로 확인, **버전 표기는 미확인** |
| 2 | Zoom/Teams 화면 공유가 미러 세트를 만드는가 | **실기 미측정.** ScreenCaptureKit 구조상 안 만든다고 판단하지만 검증 안 함. **S2 착수 전 실측 필수** |
| 3 | macOS Screen Sharing.app(VNC)이 미러 세트를 만드는가 | 동일. 미측정 |
| 4 | `SetWindowDisplayAffinity`가 **우리 실제 창**에서 실패하는가 | **이 머신에 Windows가 없다.** 창 스타일이 실패 조건에 해당함은 소스로 확인, **실기 미확인** |
| 5 | 캐릭터 본체가 화면에서 차지하는 실제 면적(pt²) | 이번 라운드 미측정. 재현이 정보창(877×853)을 잰 것과 같은 방법으로 1회 측정하면 2절 표가 완성된다 |
| 6 | Windows에 시스템 차원 화면공유 표시기가 있는가 | 찾지 못했다. **"없다"가 아니라 "못 찾았다"** |
| 7 | `UIScreen.isCaptured`의 현행 deprecation 상태 | 검색 결과에 deprecated 표기가 보였으나 대체 API 미확인 |

---

## 10. 1차 출처

- Apple DTS (Quinn "The Eskimo!") — 화면 공유 세션 식별 불가: <https://developer.apple.com/forums/thread/46546>
- Apple DTS Engineer — *"no public APIs for preventing screen capture"* (macOS 15.4): <https://developer.apple.com/forums/thread/792152>
- Microsoft — `SetWindowDisplayAffinity` 공식 문서(WDA_* 값, DWM 요구, Win10 2004 도입): <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity>
- Microsoft 포럼 채택 답변 — 레이어드 창 + `ERROR_NOT_ENOUGH_MEMORY(8)`의 진짜 원인: <https://learn.microsoft.com/en-us/archive/msdn-technet-forums/7ce400f0-ebda-4b95-869c-85b5b93f972d>
- Microsoft Q&A — Windows 11에서 여전히 미해결(엔지니어 Junjie Zhu): <https://learn.microsoft.com/en-us/answers/questions/700122/setwindowdisplayaffinity-on-windows-11>
- Microsoft — `GetSystemMetrics` 공식 문서(`SM_REMOTESESSION` / `SM_REMOTECONTROL` / 미러 드라이버 의사 모니터): <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getsystemmetrics>
- Microsoft — `WTSRegisterSessionNotification`(HWND 필수, `WM_WTSSESSION_CHANGE`): <https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification>
- Raymond Chen — 캡처 차단은 *"an obstacle, not a security measure"*: <https://devblogs.microsoft.com/oldnewthing/20130603-00/?p=4193>
- Apple — `UIScreen.isCaptured`: <https://developer.apple.com/documentation/uikit/uiscreen/iscaptured>
- Apple — `CGDisplayIsInMirrorSet`: <https://developer.apple.com/documentation/coregraphics/1455558-cgdisplayisinmirrorset>
- 참고(2차, 대조용) — MacPaw 리서치 / ghostty-org 논의: <https://macpaw.tech/research/is-mac-screen-captured/> · <https://github.com/ghostty-org/ghostty/discussions/9056>

---

## 11. 플랫폼 영향

- **macOS 영향**: 없음(이번 라운드 코드 변경 0건). S1 착수 시 `MacWindowService.EvaluateFullscreen`의
  반환값을 2축으로 넓히는 변경이 발생한다 — 기하 판정·알파 필터·디바운스 로직은 손대지 않는다.
- **Windows 영향**: 없음(이번 라운드 코드 변경 0건). **S1은 반드시 같은 라운드에 양 플랫폼 동시 적용**한다.
  Windows 쪽은 `MatchesExactly` 유지가 오히려 등급 1의 안전판이다(최대화 창은 `rcWork`라 안 걸린다).
  W1(`WDA_EXCLUDEFROMCAPTURE`)은 **구조적으로 닫힘 — 별도 배정도 불필요**하고, 그 사유를
  `PlatformParityAuditTests`에 `Assert.Ignore`(사유 포함)로 남길 것을 권고한다.
