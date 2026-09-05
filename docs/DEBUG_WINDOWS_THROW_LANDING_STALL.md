# Windows 던지기 착지 후 프리징 — 디버거 조사 (2026-09-05)

담당: `debugger`. 산출 형식: 과학적 토론 로그(가설 → 검증 방법 → 결과 → 결론).
관련 테스트: `Assets/_Project/Scripts/Tests/PlayMode/ThrowLandingRegrabTests.cs` (이 라운드 신설, 2/2 통과).

---

## 0. 현상 (사용자 원문 3건, 시간순)

1. *"윈도우에서 가끔 마우스로 던졌을때 착륙하고 무릎앉아한후에 1~2초정도 멈추는 현상이 있음"*
2. 정정: *"무릎앉아후 일어서고 그상태에서 안움직이고 프리징현상. 마우스로 잡아끌어도 아무 반응 없음 가끔 이런현상이 나옴"*
3. 추가: *"1~2초후 저절로 풀림"*

합치면: **Windows** / 던지기 → 회전 착지 → 무릎앉기 → **일어선 뒤** / 캐릭터가 멈춤 **+ 드래그 무반응** / **1~2초 뒤 자연 복구** / **간헐**.
앱이 죽는 것이 아니고 캐릭터는 보인다(사용자가 "그 상태에서"라고 했다 — 숨김이 아니다).

### 0-1. 조사 전제 — 사용자가 돌리는 빌드가 무엇인가 (먼저 못박는다)

| 항목 | 값 | 근거 |
|---|---|---|
| Windows 산출물 | `Builds/Windows/StickMate_Data/Managed/StickMate.Runtime.dll` **2026-09-03 08:32** (zip `StickMate-Windows-20260903.zip` 08:45) | `ls -la` |
| 그 시점 HEAD | `1eb0e2b` (2026-09-03 02:23) + 빌드 당시 미커밋분(불명) | `git log --before` |
| 오늘(09-05) 추가된 가상데스크톱 COM 프로브(M-8) / Win+D 원점 가드(M-9) | **빌드에 없음** | dll UTF-16 문자열 탐침: `가상데스크톱` False, `오버레이 창이 최소화됨` False (음성 대조), `프레임시간`·`FootholdPoller` True (양성 대조) |

⇒ 가설은 **9/3 빌드 기준**으로 판정한다. 오늘 들어온 Windows 전용 동기 호출(explorer.exe COM 왕복)은 이번 신고의 원인일 수 없다.

### 0-2. 이 머신에서 잴 수 있는 것 / 없는 것 (CLAUDE.md 「플랫폼 단서가 있으면 그 플랫폼 먼저」)

- (1) 코드 경로 정독 — 양 플랫폼 전부 가능.
- (2) macOS 실측 — 플랫폼 중립 로직(상태기계·락·입력 게이트·배회)은 **PlayMode 실행으로** 잴 수 있다(아래 §2). 기존 macOS 사용자 로그(9/1)에 실제 던지기 1건이 있다.
- (3) Windows 전용 경로(Win32/UniWinC/DWM) — **소스로만** 판정. 실기 없음. 그래서 §5에 사용자 Windows `Player.log`로 가르는 판정표를 둔다.

---

## 1. 가설 목록과 최종 순위

리더 초기 H1~H5 → 증상 정정 후 G1~G5 → "저절로 풀림" 후 (A)/(B)/(C) 순으로 재정렬됐다. 아래는 **최종** 표다.

| # | 가설 | 판정 | 근거 요약 |
|---|---|---|---|
| B | 설계된 거부 창의 합(무릎앉기 체류 + 배회 대기) | **부분 반증** — "안 움직임"은 설명하지만 **"드래그 무반응"은 설명 못 한다** | §2 실측: Idle에서 재잡기는 0.000초에 수락. 거부는 ThrowTumble/LandingCrouch 체류 중뿐(0.14~0.88초, 로그 남음) |
| A4 | **Windows 전용: 던지기 1회당 UniWinC 클릭관통 스타일 토글 + 레이어드 해소기 스트립 → DWM 합성 모델 전환 중 화면 정지**(앱은 돌고 화면만 멈춤) | **미확정 — 1순위.** 신고의 다섯 서술자(Windows·던지기 상관·간헐·자연복구·우리 계측에 안 잡힘)를 전부 만족하는 유일한 후보 | §3-A4. 판정은 사용자 로그 + `STICKMATE_KEEP_LAYERED=1` A/B |
| A5 | 스왑체인 재생성(Enforcer 재적용 4회 리사이즈 / 전체화면 재적합) | **반증(던지기 상관 없음)** — 재무장 경로가 드래그와 무관 | §3-A5: `MarkDirty()` 호출자는 `CreateOverlayWindow`/`SetClickThrough`/`SetAlwaysOnTop` 3곳, `SetClickThrough` 호출자는 기동 5초 유예·ESC 개발키 2곳뿐. 드래그는 `LocalClickCaptureGate` 부기만 한다 |
| A2 | 착지 후 발판 재스캔/창 열거 동기 스톨 | **반증** | 착지 트리거 재스캔 경로가 없다(폴러는 0.3초 주기, `PollImmediately`는 Resume/세션게이트만). 사용자 실기 계측 1회 1.72~1.87ms(`FootholdPoller.cs` 헤더 인용). 스파이크면 `[스톨귀인] 창열거경로`에 찍힌다 |
| A6 | 로그 파일 IO / GC / 폰트 아틀라스 재구성 | 미확정 — 후순위 | 전부 `[스톨귀인]`/`[스톨구간]`에 칸이 있다(로그 ms/줄, GC gen 증분, 아틀라스 재구성 횟수). 던지기 상관 근거 약함 |
| C | 전체화면 오발 → `_isSuspended` | **반증** | Suspend는 렌더러를 끈다(캐릭터가 사라진다) — 사용자는 보인다고 했다. 판정도 「전경창 사각형 == 모니터 사각형」+「GameConfigStore 등록 게임」+ 1.0초 디바운스, 자기 창/자기 프로세스 제외 |
| H1 | 설계된 멈춤(무릎앉기+Getup) | **반증** | Getup은 경로에 없다(랙돌을 거치지 않는다). 무릎앉기는 0.14~0.88초 |
| H2 | 프레임 페이싱(Still 15fps) | **반증(원인으로서)** | Calm/Still은 양 플랫폼 모두 `renderFrameInterval`만 바꾼다(`FramePacingPolicy.BuildPlan`) — 게임 루프·입력 폴링 60Hz 유지. ★ `FramePacing.cs:385-387` 주석("Windows에서 Calm은 targetFrameRate를 30으로")은 2026-09-01 이전 서술로 **현행 코드와 다르다**(perf-doc 신고 맞음) |
| H4 | 전이 조건(속도 임계) 미충족 | **반증** | `LandingCrouchState.Tick`은 `progress >= 1` 타이머 하나로 Idle/Walk 분기. 속도 게이트 없음 |
| G1 | 매 프레임 예외 | **반증(영구형)** / 일시형은 근거 없음 | macOS 로그 3종에서 `Exception`/`NullReference` 0건. 예외면 Windows Player.log에 스택이 찍힌다(§5 체크리스트) |
| G2 | 콜라이더/바디 미복구 | **반증** | `TickPose`가 매 프레임 상태ID로 능동 모드 재적용, `DragThrowState.Exit`가 Dynamic 복구, `ThrowTumbleState.Exit`가 직립 복구. §2 테스트에서 착지 직후 잡기가 성립 |
| G3 | = C | | |
| G4 | 상태기계 교착 / 배회 정지 | **반증** | §2: 착지 후 2초간 Idle 밖 상태 0건, `AutoWanderController.Tick`은 상태와 무관하게 매 프레임 돈다 |
| G5 | 입력 경로 플래그 미해제(락·`_pressed`·재잡기 쿨다운) | **반증** | 재잡기 쿨다운 상수 없음(`DragThrowController`/`StickmanClickHitbox`/`RodeoCursorWatcher` 전수). `SpectacleEventLock`은 `From==Dragged` 전이에서 해제 — §2 두 번째 테스트의 양성 대조가 앞 테스트의 락이 풀렸음을 실행으로 증명 |

---

## 2. 실측 — 플랫폼 중립 로직에 "일어선 뒤 잠금"이 있는가 (macOS PlayMode, 필터 2건)

### 2-1. 설계
실제 드래그&던지기 경로(`StickmanClickHitbox.SimulateMouseDownForTests` → `DragThrowController.OnMouseDown` →
`SpectacleEventLock` → `ILocalClickCaptureService` → `Dragged`)로 던지고, **무릎앉기가 끝나 Idle로 돌아온 바로 그 프레임**에
같은 실제 경로로 다시 잡는다. 시간 예산은 전부 벽시계 초(프레임 수 대기 금지).

- 테스트 1 `일어선_직후_실제경로_재잡기가_즉시_받아들여진다`: 양성 대조(평범한 Idle 잡기) → 던짐 → Idle 복귀 프레임에 재잡기 → **0.25초 안에 Dragged**이면서 **요청한 그 프레임에** Dragged여야 한다.
- 테스트 2 `착지후_Idle이_2초간_흔들리지_않고_거부창은_무릎앉기_길이뿐이다`: 최대 세기 던짐 → **무릎앉기 도중** 잡기는 거부(설계) → Idle 복귀 후 2.0초 동안 Idle/Walk 밖 상태 0건 → 2초 뒤 잡기 즉시 수락.

### 2-2. 실행 (러너 규약: xml 실재 + testcasecount)
```
러너: Unity 6000.0.82f1 -batchmode -nographics -runTests -testPlatform PlayMode
      -testFilter StickMate.Tests.PlayMode.ThrowLandingRegrabTests
결과: docs/verify/runs/dbg-throwregrab_play.xml (2026-09-05 21:32)
      testcasecount=2 total=2 passed=2 failed=0 inconclusive=0 skipped=0 (10.02초)
락 : Temp/UnityLockfile 은 남아 있었으나 lsof 보유자 없음 + Unity 프로세스 없음(21:08 빌드의 스테일) — 판정은 산출물로 했다.
```

### 2-3. 결과 (docs/verify/runs/dbg-throwregrab_play.log 발췌)

| 단계 | 테스트 1 | 테스트 2 |
|---|---|---|
| 양성 대조(Idle 잡기) | 직전 Idle → 직후 **Dragged**, 락 DragAndThrow | 동일 |
| 던진 속력 | 5.38유닛/초(3.15 H/초) | 10.81유닛/초(6.34 H/초) |
| 비행 | ThrowTumble 진입 → 회전 시간 부족 → Fall(정상 경로) | 1바퀴, 0.61초 |
| 무릎앉기 | SoftAbsorb 0.62 H, **0.23초** | ShallowCrouch 1.90 H, **0.42초** |
| 무릎앉기 도중 잡기 | — | **거부** `[2/6] 드래그 진입 무시 — 현재 상태가 LandingCrouch라서` (설계) |
| Idle 복귀 후 | 놓은 뒤 0.53초에 Idle | 2.0초 관찰 — Idle 밖 상태 **0건** |
| **일어선 직후 재잡기** | 요청 직전 Idle → **직후 Dragged, 0.000초** | 착지 2초 뒤 잡기 → **직후 Dragged** |

### 2-4. 결론
**우리 상태기계·락·입력 게이트에는 "일어선 뒤 잡기 거부 창"이 없다.** 설계된 거부는 ThrowTumble 체류(비행)와
LandingCrouch 체류(티어별 0.14 → 0.32 → 0.48 → 0.62 → 0.88초, `landingCrouchDuration*` 애셋값)뿐이고 둘 다
`[2/6] 드래그 진입 무시 — 현재 상태가 X라서`를 남긴다. Idle에서는 요청한 그 프레임에 Dragged가 된다.

**정직한 한계**: `SimulateMouseDownForTests`는 이벤트를 직접 쏘므로 **콜라이더 히트 판정(`IsCursorOverHitbox`)은 이 테스트가
보지 않았다.** 다만 `DragThrowState.Enter`의 커서→월드 변환은 실제로 돌았고(`잡은 오프셋=(0.00, -0.85)`), 던지기 경로는
콜라이더를 끄지 않는다(랙돌 미경유). 이 절반은 §5의 진단 로그 제안이 닫는다.

### 2-5. 기존 macOS 사용자 로그(9/1, `~/Library/Logs/Vibelab/StickMate/Player.log`) 대조
실제 던지기 1건: 속력 12.00 → 2바퀴, 비행 1.42초 → 무릎앉기 0.62초(구 램프) → Idle → 1초 뒤 `[유휴동작] 주위 살피기`. 이상 없음.
같은 로그의 "드래그 진입 무시" 3건은 전부 `현재 상태가 Dragged라서` = 이중 입력 경로(Unity OnMouseDown + 전역 폴링)의 중복 클릭이며 이번 증상이 아니다.

---

## 3. Windows 전용 경로 — 소스 판정 (실기 없음)

### 3-A4. ★ 1순위: 던지기 1회당 일어나는 창 스타일 토글과 DWM 합성 모델 전환

**근거 사실(전부 소스/패키지 실물):**
1. Windows 오버레이의 클릭관통은 UniWinC 히트테스트(`hitTestType=Raycast`, `SceneBootstrapper.cs:1630`)가 **매 프레임 끝**에 판정하고
   `UpdateClickThrough()`가 **변화가 있을 때만** 네이티브 `SetClickThrough(bool)`를 부른다
   (`Library/PackageCache/com.kirurobo.uniwinc@304f9ba2aa4a/Runtime/Scripts/UniWindowController.cs:626-648`).
2. 그 네이티브 호출은 `SetWindowLong(GWL_EXSTYLE)`이며 **켤 때 `WS_EX_TRANSPARENT`와 함께 `WS_EX_LAYERED`를 켠다**
   (`Platform/OverlayCompositionSnapshot.cs:409`, `WindowsLayeredHybridResolver.cs:41-43` "라이브러리는 커서가 캐릭터를 벗어날 때마다 레이어드를 다시 켠다").
3. `WindowsLayeredHybridResolver`가 0.25초 주기로 그 `WS_EX_LAYERED`를 **떼어낸다**(`SetWindowLongPtr` + `WindowFromPoint` 검증 2회).
4. 따라서 **던지기 1회당** 최소 두 번의 합성 모델 전환이 있다:
   - 놓는 순간 캐릭터가 커서 밑에서 사라짐 → 관통 ON(+LAYERED) → ≤0.25초 뒤 해소기가 LAYERED 제거.
   - 사용자가 일어선 캐릭터 위로 커서를 다시 가져감 → 관통 OFF(`SetWindowLong`).
   이 세 지점은 **정확히 사용자가 신고한 시각(놓기 / 다시 잡으려는 순간)** 이고, **macOS에는 이 축이 없다**(Swift 쪽은 styleMask 한 줄, 레이어드 개념 없음).
5. 이 앱의 Windows 창은 BitBlt 스왑체인 + DWM 확장 프레임 하이브리드다(`FramePacing.cs` 클래스 문서 Windows 절). DWM이 레이어드 ↔ 비레이어드로
   표면 모델을 바꾸는 동안 **우리 프로세스는 멈추지 않는다** — 화면만 멈춘다. 그래서 `[프레임스파이크]`/`[스톨귀인]`(둘 다 우리 프레임 시간 기준)에는
   **아무것도 안 찍히면서** 사용자 눈에는 "캐릭터가 멈추고 잡아도 반응이 없다(화면이 안 바뀐다)"가 된다. 잡기 자체는 성립해 있으므로 화면이 돌아오는 순간
   드래그가 진행돼 있거나 캐릭터가 걷고 있다 = "저절로 풀림".

**왜 "가끔"인가(가설)**: 전환 비용은 DWM/드라이버 상태에 달렸고(사용자 GPU가 내장 Iris Xe), 해소기의 되돌림·재검증(`RestoreLayered`)이
섞이면 전환 횟수가 달라진다.

**판정 방법(사용자 실기, 재빌드 없음):**
- 로그: 프리징 시각 근처에 `[1/6] 캐릭터 위 마우스다운 감지` → `[2/6] 가드 통과` → `[3/6] 드래그 시작`이 **찍혀 있는데** 같은 구간에
  `[프레임스파이크]`/`[스톨귀인]`이 **없다**면 ⇒ 앱은 반응했고 화면이 멈춘 것 = A4 부류. `[레이어드해소]`/`[합성진단]` 줄의 제거 횟수·지문 변화가 던지기마다 늘면 확정에 가깝다.
- A/B: `STICKMATE_KEEP_LAYERED=1`(해소기 완전 차단, 9/3 빌드에 존재 — dll 탐침 True)로 실행해 같은 던지기를 반복. 사라지면 해소기 스트립이 범인, 남으면 라이브러리 자체 토글이 범인.

**반증 조건**: 위 A/B에서 둘 다 그대로이고 로그에 `[프레임스파이크]`가 프리징 시각에 찍혀 있으면 A4가 아니라 아래 A5/A6이다.

### 3-A5. 스왑체인 재생성(반증 — 던지기 상관 없음)
`WindowsOverlayStateEnforcer`의 재적용 루프(5회 × 0.5초, 비-GlassOnly면 회차당 `SetBorderless` 리사이즈 4회 = 사용자 실기 268~407ms/회)는
`MarkDirty()`로만 재무장되고, 호출자는 `Win32WindowService.cs:1421(CreateOverlayWindow)/1464(SetClickThrough)/1494(SetAlwaysOnTop)`.
`SetClickThrough`의 런타임 호출자는 `StickmanAgent.cs:827(기동 5초 유예)`·`:886(ESC 개발키, 릴리스에서 닫힘)`, `SetAlwaysOnTop`은 `:778(기동)`뿐.
**드래그 시작/종료는 `LocalClickCaptureGate` 부기(`Win32WindowService.cs:1612-1619`)만 한다.** ⇒ 던지기가 이 루프를 깨우지 않는다.
전체화면 재적합(`TickFullScreenBounds`)도 디스플레이 구성 변경/네이티브 창 이동 때만 재무장. 로그 서명이 있으니(`[재적용 N/5 … 리사이즈 4회]`, `[프레임스파이크] … 백버퍼가 바뀌었다`) §5에서 배제한다.

### 3-A2. 창 열거(반증)
`EnumerateFootholds`(`Win32WindowService.cs:903-951`)는 `EnumWindows` + 창당 커널 구조체 읽기(`IsWindowVisible/IsIconic/GetWindowLong/GetWindowThreadProcessId`) +
`InternalGetWindowText`(메시지 안 보냄, 2026-09-01에 `GetWindowTextLength` 199ms 사고를 이걸로 고침) + `DwmGetWindowAttribute` 2종. 사용자 실기 1회 1.72~1.87ms.
착지가 유발하는 재열거는 없다. 스파이크면 `[스톨귀인] … 창열거경로 N ms [열거창 M개 …]`에 찍힌다.

### 3-C. 전체화면 오발(반증)
`EvaluateFullscreen`(`:2042-2116`): 자기 창·자기 프로세스 제외, `GetWindowRect == rcMonitor` **정확 일치**(최대화 창은 테두리만큼 커서 불일치), 게임 레지스트리 확인, 1.0초 디바운스.
그리고 `Suspend()`(`StickmanAgent.cs:1630-1706`)는 `SetRenderersEnabled(false)` — 캐릭터가 **사라진다**. 신고와 모양이 다르다. 로그 서명 `[숨김]`/`[전체화면판정]`.

### 3-기타. 주기적 Windows 전용 동기 호출(던지기 상관 없음, 참고)
`WindowsViewerPresenceService`(0.2~0.5초: `GetLastInputInfo`, `WTSQuerySessionInformation`, `OpenInputDesktop`), `WindowsTopmostWatchdog`(0.1초 `GetWindowLong`),
`WindowsSystemTrayIcon`(기동 시 최대 5회), `WindowsGameProcessProbe`(전경창이 모니터를 덮을 때만). 전부 `PlatformEnforcer`/`Agent` 구간으로 계측된다.

---

## 4. "Windows에서만(주로) 나는 이유" — 결론 문장

플랫폼 중립 로직은 실행으로 배제됐다(§2). 사용자 빌드에서 **던지기와 같은 순간에 Windows에서만 일어나는 일**은
UniWinC 클릭관통 스타일 토글(놓기/재접근)과 레이어드 해소기 스트립뿐이며, 그 비용은 우리 프로세스 밖(DWM)에서 발생해 우리 계측에 잡히지 않는다 —
신고의 서술자 전부와 맞는다(A4). 다만 **실기 확인 전이라 "확정"이 아니다.** 반대로 사용자 로그에 `[2/6] 드래그 진입 무시 — 현재 상태가 LandingCrouch/ThrowTumble라서`가
프리징 시각에 찍혀 있다면 그것은 **플랫폼 무관한 설계된 거부 창(≤0.88초)** 을 Windows에서 관찰한 것이고, 그때 결론은 (B)로 바뀐다.

---

## 5. 사용자 Windows `Player.log`로 가르는 판정표

경로: `%USERPROFILE%\AppData\LocalLow\Vibelab\StickMate\Player.log` (재현 직후 앱을 끄지 말고 복사). 아래 태그는 **전부 9/3 빌드 dll에 실재**함을 UTF-16 탐침으로 확인했다.

프리징이 난 던지기의 `[DragThrowState] [6/6] 놓음` 줄을 찾고, 그 뒤 `[던지기회전] 착지` → `[무릎앉아] 착지 연출 시작`(지속 N초가 적혀 있다) 다음 **±3초**를 본다.

| 프리징 구간에 보이는 것 | 판정 | 다음 조치 |
|---|---|---|
| `[2/6] 드래그 진입 무시 — 현재 상태가 LandingCrouch라서` / `ThrowTumble라서` | (B) 설계된 거부 창(플랫폼 무관). 사용자가 무릎앉기 도중에 잡은 것 | 리더/design-motion 판단(거부 시 시각 신호 또는 무릎앉기 중 잡기 허용). 버그 아님 |
| `[1/6] 캐릭터 위 마우스다운 감지` + `[3/6] 드래그 시작`이 있는데 **`[프레임스파이크]` 없음** | (A4) 앱은 반응, 화면만 정지 = DWM 합성 전환 | `STICKMATE_KEEP_LAYERED=1` A/B → `dev-platform`. ★ 다음 빌드부터는 `[레이어드해소] 타임라인 #N 라이브러리 관통 ON/OFF … 프레임# t=…초`와 `… WS_EX_LAYERED 제거 …` 줄이 던지기마다 찍히므로(§6-b) 프리징 시각과 나란히 놓으면 바로 갈린다 |
| `[프레임스파이크] NNNms … 백버퍼가 바뀌었다` | (A5) 스왑체인 재생성 | 같은 시각의 `[재적용 N/5 … 투명 재적용: …]`/`[전체화면 확장 시도]` 줄 확인 → `dev-platform` |
| `[스톨귀인] … 로직 X%: 창열거경로 N ms` 가 지배적 | (A2) 열거 스파이크(열거창 개수·DWM 회수가 같은 줄에 있다) | `dev-platform` |
| `[스톨귀인] … 로그 N ms/M줄` 지배적 | (A6) 파일 IO(백신 실시간 검사 의심) | Defender 제외 A/B |
| `[스톨귀인] … GC gen1/gen2 +1` / `[스톨구간] 폰트아틀라스 재구성` 증가 | (A6) 관리 힙/폰트 | `perf-doc` |
| `[스톨귀인] … 로직밖 N ms(≥50%)` + 백버퍼 변화 없음 | 렌더/프레젠트/OS 합성 — A4와 같은 부류 | A4 A/B |
| `[히트판정] 근접 클릭이 히트박스를 벗어남(전역폴링) — 커서 월드=…, 몸 중심=…, 콜라이더 외접=…`(★ 다음 빌드부터, §6-a) | 클릭은 우리에게 왔는데 콜라이더를 못 잡았다 — 같은 줄의 커서 월드·몸 중심·콜라이더 외접 좌표로 「좌표 변환 어긋남」과 「콜라이더 위치 어긋남」을 가른다 | `dev-platform`(커서→월드 변환) / `coder`(콜라이더·포즈) |
| `[히트판정] 클릭이 왔지만 커서 좌표를 읽지 못함` | 커서 조회 실패 — 드래그는 원리적으로 시작 불가 | `dev-platform`(ICursorPositionService) |
| **아무 줄도 없음**(클릭 로그·`[히트판정]`·스파이크 전부 없음) | 클릭이 우리 프로세스에 도달하지 않았거나(예: 전경 앱이 권한 상승 상태면 `GetAsyncKeyState`가 0을 돌려준다) Update 자체가 멎음 | 전경 앱 권한 상승 여부 확인 → `dev-platform` |
| `Exception`/`NullReference` 스택 | (G1) 예외 | 스택 그대로 `coder` |
| `[숨김] 캐릭터를 숨기고` / `[전체화면판정]` | (C) — 단, 캐릭터가 보였다는 신고와 모순이라 가능성 낮음 | — |
| `[발판유예] … 공중 유예` / `[발판상실]` | 착지한 창이 열거에서 튐(GroundLossHang 0.45초 + 하드상한 1.35초) — 다만 이 상태는 종종걸음 연출이라 "정지"로 안 읽힌다 | `dev-platform`(Windows 발판 필터) |

참고 주기: `[프레임시간]` 30초마다 p50/p95/p99/최대, `[스톨구간]` 60초마다 구간 상위 3개, `[Z-ORDER]` 60초 요약.

---

## 6. 수정 여부와 진단 로그 추가안

**수정: 하지 않았다.** 원인이 증거로 확정되지 않았고(§4), 확정 후보(A4)는 Windows 전용 경로라 이 머신에서 검증할 수 없다. 사용자가 닫은 문(던짐 텀블 폐지·랙돌 트리거)은 건드리지 않았다.

**이 라운드가 남긴 것**: 대조 테스트 1개(§2, 플랫폼 중립, 벽시계 예산). 상태기계에 거부 창이 생기면 이 테스트가 빨개진다.

### 진단 로그 3건 — 사용자 승인으로 **구현됨**(2026-09-05, 진단 전용 · 동작 수정 0)
- **(a) 근접 미스 클릭 로그 `[히트판정]`** — `Interaction/ClickHitboxNearMissPolicy.cs`(신규, 순수 규칙: 근접 반경 = 몸 중심에서 신장 **1.5배**, 같은 계열 최소 **2초**(벽시계), 문자열) +
  `Interaction/StickmanClickHitbox.cs`(예전 `Update()` 본문을 `ProcessGlobalButtonSample(bool)`로 분리 — 제어 흐름 동일 — 하고 **상승 엣지인데 콜라이더가 안 잡힌 분기**에만
  `NoteMissedPress`를 붙였다). 한 줄에 커서 월드 · 몸 중심 · 발끝 · 거리(유닛/신장) · 활성 콜라이더 외접 사각형 · 상태 · 프레임#. 커서를 못 읽은 경우는 별도 문장.
  진단 카운터 4개(`NearMissLogCount/NearMissSuppressedCount/FarMissCount/CursorUnavailablePressCount`).
- **(b) LAYERED 타임라인 `[레이어드해소] 타임라인 #N …`** — `Platform/LayeredHybridTimeline.cs`(신규, 플랫폼 중립·순수: 엣지에서만 1줄, 상한 = 제거 상한 240 x 2 = 480줄 뒤 1줄 알림 후 세기만) +
  `Platform/Windows/WindowsLayeredHybridResolver.cs`(`Tick`에 `isClickThrough` 캐시 인자 추가 → **매 프레임 엣지**로 "라이브러리 관통 ON = 네이티브가 LAYERED를 켠 프레임"을 잡고,
  0.25초 표본의 OS 실측 LAYERED 켜짐/꺼짐, 제거(누적 횟수), 복구(사유)를 `프레임# t=초 (직전 사건 +Δ초)`로 남긴다) + `WindowsOverlayStateEnforcer.cs`(인자 1개 전달).
- **(c) `FramePacing.cs` 주석 정정** — UI 홀드 인과 서술의 3·4번과 "플랫폼 비대칭" 문단을 "(당시)"로 표시하고 현행(`BuildPlan`은 Calm/Still에서 플랫폼 무관하게
  `renderFrameInterval`만, Windows에서도 루프·커서 폴링 60Hz)을 명시. 비주석 줄 변경 0(diff로 확인).

**테스트(전부 벽시계 초 기준)**: EditMode `ThrowLandingDiag_NearMissPolicyTests`(규칙·포맷 7건) · `ThrowLandingDiag_LayeredTimelineTests`(엣지·포맷·상한 6건 + 배선 니들 감사 2건, 존재/부재 대조 포함) ·
`ThrowLandingDiag_FramePacingCommentTests`(행동의 진실 + 주석의 진실 2건, 부재 단언의 양성 대조 = 행동 단언). PlayMode `ThrowLandingDiag_ClickHitboxNearMissTests`(실제 히트박스 경로:
먼 클릭 침묵 / 근접 미스 1줄 / 연타 억제 / 2.1초 뒤 재발생 / 몸 중심 히트는 Dragged, 1건).

---

## 7. 검증 기록 (verify-change용)

| 항목 | 방법 | 결과 |
|---|---|---|
| 새 테스트 컴파일 | `Tools/VerifyChange/xcheck_isolated.sh osx` | 5/5 유닛 `errors=0`, PlayMode sources=122 |
| Windows 크로스컴파일 | `xcheck_isolated.sh win` | 5/5 유닛 `errors=0` |
| 탐지 경로 생존 | `xcheck_isolated.sh win --selftest` | "양성 대조 통과 — 주입 오류가 runtime 2개에서 검출됐고 그것이 유일한 에러" |
| PlayMode 필터 실행 | 결과 xml `docs/verify/runs/dbg-throwregrab_play.xml` | testcasecount=2, passed=2 |
| 사용자 빌드 내용 | dll UTF-16 문자열 탐침 | 양성 대조 2건 True / 음성 대조 2건 False / 판정표 태그 전부 True |
| Unity 락 | `ls Temp/UnityLockfile`(있음) + `lsof`(보유자 없음) + `ps -axo comm=`(Unity 없음) | 스테일로 판단, 산출물로 판정 |
| 실기 인스턴스 | `pgrep -f "StickMate.app/Contents/MacOS/StickMate" \| wc -l` = 0 (양성 대조 존재하지 않는 이름 0) | 앱을 띄우지 않았다(드래그는 드라이버로 재현 불가) |
| **진단 3건 컴파일** | `xcheck_isolated.sh osx` / `win` | 각 5/5 유닛 `errors=0`(runtime 236 · EditMode 178 · PlayMode 123 소스) |
| 진단 3건 탐지 경로 | `xcheck_isolated.sh win --selftest` | "양성 대조 통과 — 주입 오류가 유일한 에러" |
| 진단 EditMode | `docs/verify/runs/dbg-diag_edit.xml` (22:04, 필터 `StickMate.Tests.EditMode.ThrowLandingDiag_`) | testcasecount=17, passed=17 |
| 진단 PlayMode | `docs/verify/runs/dbg-diag_play.xml` (22:05) | testcasecount=1, passed=1 (5.2초 — 레이트리밋 2.1초 벽시계 대기 포함). 로그에 실제 `[히트판정]` 2줄(프레임#26106 / #49802, 콜라이더 외접 (-0.41,-11.92)~(0.30,-9.96), 거리 1.00신장) + 양성 대조 히트 `[1/6]→[2/6]→[3/6]` |
| 프로덕션 동작 불변 | `git diff -U0` 비주석 줄만 추출 | `StickmanClickHitbox.cs`: Update 본체 → `ProcessGlobalButtonSample` 분리(엣지/히트/놓기 분기 동일) + 미스 분기에 카운터/로그만 · `WindowsLayeredHybridResolver.cs`: 타임라인 관측 호출 4곳 + `Tick` 인자 1개 · `WindowsOverlayStateEnforcer.cs`: 인자 전달 1줄 · `FramePacing.cs`: **비주석 줄 변경 0**, 낡은 니들 0건 |

**Windows 영향: 별도 배정 필요(사유: 1순위 후보 A4가 Windows 전용 경로(UniWinC 스타일 토글·레이어드 해소기·DWM)라 실기 로그/A/B 없이는 확정 불가. 이 라운드의 코드 변경은 플랫폼 중립 테스트 1개뿐이며 `xcheck win` 0에러).**
