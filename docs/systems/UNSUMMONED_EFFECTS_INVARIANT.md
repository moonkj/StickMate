# 사용자가 부르지 않은 연출 — N-20 불변식을 어디까지 넓히는가

작성: `game-architect` · 2026-09-15 · 문서만 썼다(코드·테스트 무수정, Unity·앱·러너 미실행)
기준: HEAD `ad49497`(묶음 ⑦). 줄 번호는 HEAD 기준이다. 코드 사실은 전부 소스 판독이고 실기는 0이다.
갱신: 2026-09-15 · verify-change 판정(B 조건부 6항)과 리더 판정·사실 전달을 반영했다. 코드는 HEAD 사본으로 다시 읽었고, 옛 문장은 취소선으로 남겼다(항목별 위치는 문서 끝 「정정 기록」). ★ 작업 트리 `Interaction/WindowCrashDirector.cs`에는 A1 가드가 **미커밋**으로 들어와 있다(HEAD 대비 +25줄). 이 문서의 크랙 줄 번호는 여전히 HEAD 기준이다.
발단: `persona-stress` F3(리더 전달) · 명령 크랙 판정의 부수 발견(형제 명령 3종 등급 가드 0) · 리더 배정 묶음 ⑧.
경로 표기: 코드는 `Assets/_Project/Scripts/` 아래 상대 경로다.
관련: `docs/systems/AUTO_SURFACE_LEASE_AXIS.md`(N-20, §9-1 명령 크랙 정정).

---

## 0. 결론 먼저

| 질문 | 답 |
|---|---|
| ① 원칙 2 기준으로 이 연출들이 등급 1(발표 위)에서 도는 것이 허용되는가 | **자율 발동은 허용하지 않는다 — 종류와 무관하게.** 선례가 이미 코드에 있다: 자율 춤은 외부 전체화면의 모든 등급에서 꺼진다(`Platform/FullscreenSuspendPolicy.cs:302-303` `SuppressesAutoDance(tier) => tier != ForeignFullscreenTier.None`). ~~**지금 출하 빌드에서는 자율 경로가 돌지 않는다** — 확률 기본값이 전부 0이고(§1-2), 사용자가 확률을 올릴 설정이 없다.~~ 〔★ B-2 정정 — 전칭에 예외가 있었다〕 **출하 기본값에서 확률 추첨 자율 경로 5곳은 돌지 않는다** — 확률 기본값이 전부 0이고, 사용자가 확률을 올릴 설정이 없다(§1-2). **로데오 커서는 이 문장에서 제외한다**(제외 근거: 발동 입력이 확률 추첨이 아니라 사용자 커서 정지다 — 층 1 분류 밖, 리더 판정 유지). 단 **⌃⌥⌘R은 개발 게이트 밖에서 그 자동 발동 게이트를 켜고**, 감시자의 **억제 판독은 0**이다(§1-2 「예외 — 로데오 커서」). 위험은 누가 확률을 올리는 미래 변경이다 |
| ② N-20 불변식을 「사용자가 부르지 않은 연출 전체」로 넓힐지, 연출별로 나눌지 | **넓힌다. 규칙은 하나, 층은 둘이다.** 층 1 「사용자가 부르지 않은 연출은 억제 창구가 참인 동안 **시작하지 않는다**」(모든 자율 연출). 층 2 「남의 창에 붙는 오버레이는 억제 창구가 참이 되면 **걷는다**」(창에 붙는 연출). 창구는 N-20의 `SuppressesUnsummonedSurfaces(P, g)` + 캐릭터 축 `IsSuspended`를 그대로 쓴다 |
| ③ 명령 3종은 크랙 A1과 같은 가능 판정 가드가 필요한가 | **창에 붙는 명령 둘(창 도둑 · 그라피티)은 필요하다. 활쏘기는 필요 없다.** 활쏘기 과녁은 캐릭터가 선 발판 위에 놓이는 캐릭터 소품이고(`ArcheryDirector.cs:481-523`), 등급 1은 캐릭터를 남기는 등급이다 |
| ④ T-B 스캐너가 이 연출들을 못 보는 문제 | **구조적으로 못 본다** — 출발점이 `ArePanelsSuppressed` 독자다(`Tests/EditMode/UnsummonedSurfaceAxisTests.cs:170`). 오늘 가드가 없는 연출 5곳은 그 값을 읽지 않으므로 목록에 오르지 않는다. 출발점을 **연출 락 획득 · 오버레이 발행 메서드**로 바꾼 새 감사를 요구한다(§4) |
| ⑤ N-8 · 1.0 필수 | **N-8**: `WindowTheftDirector.cs`가 목록 B다 — 창 도둑 가드는 E-3 증거 결속이 풀린 뒤다. 나머지 가드 대상은 목록 밖이다. **1.0**: ~~창 도둑·그라피티 **명령** 가드는 1.0 필수 권고(단 노출 실측 1회를 먼저 한다 — §5-2).~~ 〔★ B-5 리더 판정 반영〕 창 도둑·그라피티 **명령** 가드는 **1.0 필수 — ROADMAP N-20 해제 조건 7(A1)의 확장 7-b**다(새 게이트가 아니다 · ROADMAP N-20 해제 조건 7-b로 등재(묶음 ⑧)). 그라피티는 N-8 목록 밖이라 A1 커밋 직후 coder-ui 러너 줄에서, 창 도둑은 N-8 B등급이라 E-3 결속 해제 뒤에 한다(§6-2). **자율** 가드와 새 감사는 1.0 필수가 아니다. 대신 「**자율 확률을 0보다 크게 출하하는 라운드는 새 감사 GREEN이 선행 조건**」을 출하 규칙으로 둔다 |

---

## 1. 사실 — 연출 락을 잡는 파일 12개 전수

### 1-1. 프로브

- 대상: 프로덕션 `.cs`(테스트 제외)에서 `SpectacleEventLock.TryAcquire(` 호출 파일 → **12개**.
- 판독 토큰(주석 제거 뒤): `.IsSuspended` · `.ArePanelsSuppressed` · `SuppressesUnsummonedSurfaces(` · `.HidesScreenSurfaces` · `IsForeignFullscreenAppPresent` · 보존 동결 · `HiddenCharacterCommandGate.BlocksNow`.
- 대조: 없는 토큰 음성 대조 0. 양성 대조 — N-8 목록 파서가 `StickmanAgent.cs`를 B로 읽는다 ~~(목록 행 46)~~. 〔★ B-6 정정〕 그 줄은 목록 문서 `docs/verify/DISPLAY_CHANGE_PATH_FILES.md`의 **55행**이다(앵커 `Scripts/Core/StickmanAgent.cs`). 46은 행 번호가 아니라 **목록 항목 수**다 — 같은 문서 검사기의 행 수 하한(앵커 `len(rows) >= 46`)이고, 실측은 A·B·C·D·E 등급 41행 + X 5행 = 46이다.
- 확률 값: `Assets/_Project/Data/DefaultStickConfig.asset`(에셋이 코드 기본값을 덮는다 — `docs/TEAM.md` 거짓 통과 #9).

### 1-2. 표

| 파일 | 자율 경로 | 명령 경로(가능 판정) | 억제·숨김 판독(주석 제외) | 출하 확률 | N-8 |
|---|---|---|---|---|---|
| `Interaction/WindowCrashDirector.cs` | `TickAutoTrigger :196` — **가드 없음** | `GetAvailability :79` — 숨김 게이트만 | 오버레이 틱 `:170-171`에만 새 창구 · `IsSuspended` · 숨김 게이트(`:88`, 명령 칸의 그 줄)〔B-6 보강〕 | `windowCrashChance: 0` | 밖 |
| `Interaction/WindowTheftDirector.cs` | `TickAutoTrigger :216` — **가드 없음**(보존 동결만) | `GetAvailability :86` — 숨김 게이트만 | 보존 동결 · 숨김 게이트 | `windowTheftChance: 0` | **B** |
| `Interaction/GraffitiDirector.cs` | `TickAutoTrigger :154` — **가드 없음** | `GetAvailability :64` — 숨김 게이트만 | 숨김 게이트 | `graffitiChance: 0` | 밖 |
| `Interaction/ArcheryDirector.cs` | `TickAutoTrigger :307` — **가드 없음** | `GetAvailability :154` — 숨김 게이트만 | 숨김 게이트 | `archeryChance: 0` | 밖 |
| `Interaction/DesktopIconMirrorDirector.cs`(블랙홀 · 청소) | `TickAutoTrigger :116` — **가드 없음** | 없음 | 없음 | `blackholeChance: 0` · `desktopTidyChance: 0` | 밖 |
| `Interaction/StressGaugeDirector.cs`(삐짐) | `TickSulkyAutoTrigger` | 없음 | `IsSuspended` | `stressSulkyChance: 0` | 밖 |
| `Interaction/TodoReminderDirector.cs` | (폴링) | 있음 | 새 창구 · `IsSuspended`(N-20) · 숨김 게이트(`:97`)〔B-6 보강〕 | `todoReminderChance: 0` | 밖 |
| `Interaction/RunawayDirector.cs` | (스트레스 결과) | — | `IsSuspended` · 보존 동결 · 숨김 게이트(`:153`)〔B-6 보강〕 | — | **B** |
| `Interaction/DanceEpisodeDirector.cs` | 에피소드 | — | 보존 동결(자율 억제는 ~~AudioReactiveDanceGate.cs:163~~ `Core/AudioReactiveDanceGate.cs:163` 춤 축 〔B-6 경로 보강〕) | — | **B** |
| `Interaction/DragThrowController.cs` · `RodeoCursorWatcher.cs` · `FocusWatchDirector.cs` | 사용자 입력 · 커서 · 데모 | — | 보존 동결 / 없음(아래 「예외 — 로데오 커서」 4) / `IsSuspended` · 숨김 게이트(`FocusWatchDirector.cs:180`)〔B-6 보강〕 | `rodeoCursorEnabled: 0` | B / 밖 / 밖 |

- **사용자가 확률을 올릴 수 있는가 — 없다.** 확률 필드는 `Core/StickConfig.cs` 선언과 그것을 읽는 연출 파일, 그리고 「기본값 0」을 설명하는 주석(`Interaction/AppControlDirector.cs:510` · `Platform/IGlobalKeyStateService.cs:112` · `Core/ItemCatalog.cs:1095`)에만 나온다. 「빈도」류 설정 후보 검색 0건.
- **명령 입구는 출하 빌드에서 산다.** ⌃⌥⌘G(그라피티) · T(창 도둑) · X(크랙) · A(활쏘기)는 개발 게이트 밖이다(`AppControlDirector.cs:295-298` — D·H·S·J·F만 `dev &&`, `:315-319`). 행동 명령창 타일 네 개도 사용자 UI다(`ActionCommandPopover.cs:218`, 「개발 전용 항목 0」 `:59`).
  - 〔2026-09-15 재확인 · 리더 전달 사실을 직접 확인〕 발동 분기: G `:362` `else if (gRise) Invoke(ControlAction.Graffiti` · T `:363` `else if (tRise) Invoke(ControlAction.WindowTheft` · A `:370` `else if (aRise) Invoke(ControlAction.Archery`. 행동 명령창도 같은 Director의 판정과 실행을 부른다(`ActionCommandPopover.cs:513` 그라피티 · `:515` 창 도둑 `GetAvailability()`, `:528` · `:529` `ForceTriggerNow`). 두 Director 파일(HEAD)에서 `SuppressesUnsummonedSurfaces(`는 0건이다(양성 대조: 같은 검색이 HEAD `WindowCrashDirector.cs`에서 1).
- ★ **예외 — 로데오 커서** (2026-09-15 B-2 · 리더 판정: 층 1 분류는 유지하고, 전칭 문장에 아래 넷을 코드 사실로 적는다)
  1. **제외**: 로데오는 §0 ①의 「출하 기본값에서 자율 경로가 돌지 않는다」와 층 1 대상(자율 5곳)에서 뺀다.
  2. **제외 근거**: 발동 입력이 확률 추첨이 아니라 **사용자 커서가 캐릭터 곁에 멈춘 시간**이다(`Interaction/RodeoCursorWatcher.cs` `Update`의 정지 타이머 → `TryTrigger`, 확률 필드를 읽지 않는다). 분류는 「사용자 커서 입력」이다.
  3. **⌃⌥⌘R은 개발 게이트 밖에서 자동 발동 게이트를 켠다**: `Interaction/AppControlDirector.cs:290` `bool r = chord && IsKeyDown(GlobalKey.R);`(`dev` 조건 없음 — `dev &&`는 `:315-319`의 D·H·S·J·F뿐) → `:360` `else if (rRise) Invoke(ControlAction.Rodeo` → `:446` `_config.rodeoCursorEnabled = !_config.rodeoCursorEnabled;`. 실행 중에 스위치를 뒤집고, 로그가 스스로 「자동 발동 게이트」라고 부른다(`:447-448`). 출하 에셋 값은 `rodeoCursorEnabled: 0`(`Assets/_Project/Data/DefaultStickConfig.asset:147`)이다. 프로덕션 `.cs`에서 이 필드를 참조하는 곳은 `Core/StickConfig.cs` 선언 · 이 토글 · 감시자, 세 파일뿐이다.
  4. **억제 판독 0**: 감시자는 연출 락을 잡는다(`RodeoCursorWatcher.cs:146` `SpectacleEventLock.TryAcquire(SpectacleEventKind.RodeoCursor, this)`). 그런데 §1-1 토큰 7종이 전부 0이고, 넓힌 정규식(suspend · hidden · fullscreen · suppress · frozen · BlocksNow · Tier, 대소문자 무시)도 0이다. 대조: 같은 정규식이 `FocusWatchDirector.cs`에서 5 · 없는 토큰은 0.
  - **미확인(후속 라운드)**: 등급 1 · 2에서 로데오가 실제로 어떻게 도는가. 코드로 확인한 것은 하나뿐이다 — 등급 2 진입 순간 `Core/StickmanAgent.cs` `Suspend`가 `RodeoCursor` 상태를 Idle로 강제 전이한다(`:1842`, 앵커 `current == StickmanStateId.Dragged || current == StickmanStateId.RodeoCursor ||`). 그 뒤 숨김 중 재발동 여부와 등급 1 체류 중 발동은 판정하지 않았다.
  - **백로그(code-inspection)**: `:448` 로그 문구 「거처는 설정창 [이벤트], 36-1」은 낡았다. `Interaction/SettingsWindow.cs`에 로데오 참조가 0이다(`rodeo` 대소문자 무시 0 · `로데오` 0. 대조: 같은 니들 `rodeo`가 `AppControlDirector.cs`에서 5 · 같은 파일의 `_config.` 7).

### 1-3. 대상 선정이 등급 1에서 «우연히» 막는가 — 구조적 안전이 아니다

| 연출 | 대상 | 등급 1(전체화면 앱이 화면을 덮음)에서 |
|---|---|---|
| 크랙 | 최상위 실제 창(`WindowCrashDirector.cs:227-242`) | ~~**막지 않는다** — 대상이 전체화면 앱 자체다.~~ 〔★ B-3 — 그라피티와 같은 전제로 맞춤〕 **전제(전체화면 앱 창이 발판 목록에 오른다)가 참이면 막지 않는다** — 대상이 전체화면 앱 자체다. 전제는 Windows에서 코드로 대부분 참이고 macOS는 미확인이다(§1-3-1). 자율이면 다음 프레임 오버레이 틱이 취소하지만, 캐릭터 스윙과 **0.12초 취소 페이드**(`WindowCrashRenderer.cs:62`, 페이드 첫 프레임 알파 1 `:354`)가 발표 위에 남는다(코드 판독, 실기 미확인) |
| 창 도둑 | 폭 상한 이하의 작은 창(`WindowTheftDirector.cs:250-262` · `:314-316`) | 전체화면 앱은 너무 넓어 후보가 아니다. ~~**그 위에 뜬 작은 창**(발표 도구 막대 등)이 발판 목록에 있으면 후보가 된다 — **미확인**~~ 〔★ B-1 정정 — 코드 확정〕 **자격 있는 창이 하나라도 있으면 등급 1에서 막히지 않는다.** 후보는 발판 목록이 아니라 가려짐 필터 **전** 원본 창 목록에서 뽑는다(`:197-204` `ResolveCandidateSource` — `poller.CachedRawWindows`, 그 목록이 비었을 때만 발판 목록으로 폴백). 자격은 실제 창(핸들 0 이상)과 폭 상한뿐이고 가려짐은 보지 않는다(`WindowTheftTargetRules.cs:42-47`). 그래서 전체화면 앱 **뒤에 가려진** 작은 창도 대상이 되고, 강제 발동 로그가 그 경우를 「가려짐=예(다른 창 뒤 — 발판 목록에는 없음)」로 찍는다(`:149`). 가능 판정(`:86-113`)과 자율 경로(`:216-244`)에 등급 조건이 없고, 등급 1에서도 발판 폴링은 돈다(`Core/StickmanAgent.cs:981`은 `_isSuspended`일 때만 되돌아가고, `:987` `_footholdPoller.Tick(dt);`). 원본 채널은 양 플랫폼 모두 있다: Windows `Platform/Windows/Win32WindowService.cs:561` `_readOnlyRawWindows = _rawBuffer.AsReadOnly();`(스타일 · 기하 필터 통과분, 가려짐 전) · macOS `Platform/MacOS/MacWindowService.cs:1051` |
| 그라피티 | 창과 겹치지 않는 빈 자리(`GraffitiDirector.cs:207-208` · `:216-226`) | 전체화면 앱 창이 발판 목록에 있으면 빈 자리가 없어 못 그린다. ~~**그 전제(전체화면 창이 발판인가)는 미확인**이다~~ 〔★ B-3〕 전제는 크랙과 같다 — Windows는 코드로 대부분 참이라 **막힌다**. macOS는 미확인이다(§1-3-1). 틈 하나: 전체화면 앱의 상단선을 그보다 앞의 창이 가리면, 그 폭만큼 발판 사각형이 세로로 통째 빠진다. 그 폭이 낙서 영역보다 넓으면 그 아래에서 빈 자리 판정이 통과할 수 있다(`Platform/VisibleTopEdgeSolver.cs:147-155` · `Win32WindowService.cs:974-976` 판독 추론, 미확인) |
| 블랙홀 · 청소 | 바탕화면 아이콘 영역, 창에 덮이면 건너뜀(`DesktopIconMirrorDirector.cs:136`) | **양 플랫폼 모두 지금 돌지 않는다** — 아이콘 서비스가 Windows는 정직한 미구현 스텁(`Platform/Windows/Win32WindowService.cs:2120-2132`), macOS는 미구현이다(`Platform/MacOS/MacWindowService.cs:59`) |
| 활쏘기 | 캐릭터가 선 발판의 좌우 범위(`ArcheryDirector.cs:481-523`) | 막지 않는다 — 캐릭터 소품이다 |

⇒ 그라피티·아이콘 미러를 막는 것은 **대상 선정의 부수 효과**이고, 크랙·창 도둑·활쏘기는 그것조차 없다. 규칙이 없으면 대상 선정 코드를 고치는 날 조용히 풀린다.

#### 1-3-1. 전체화면 앱 창이 발판 목록에 오르는가 (2026-09-15 B-3 — Windows는 코드로 대부분 판정된다)

Windows 전용 파일은 `#if UNITY_STANDALONE_WIN` 안이라 이 머신에서 컴파일되지 않는다. 전부 소스 텍스트로 읽었다(HEAD 사본).

- **스타일 필터**(`Platform/Windows/Win32WindowService.cs:615-642`, `ClassifyWindowStyle` 본문): 보이지 않음 · 최소화 · 도구 창 · 우리 프로세스 · 제목 없음(`:632` `if (!ProbeHasTitle(hWnd)) return WindowsFootholdRejection.NoTitle;`) · DWM 가림만 뺀다. **전체화면이나 모니터 크기를 빼는 규칙은 없다.**
- **기하 필터**(`Platform/Windows/WindowsFootholdFilter.cs:203-213`, `ClassifyGeometry`): 폭·높이 0 이하 · 알파 하한 미만 · 너무 작음 · 비교 화면과 안 겹침(`:212`)만 뺀다. 비교 화면은 오버레이가 덮는 화면이다(`Win32WindowService.cs:931-932`, 조회 전이면 가상 화면 `:933-937`). **여기에도 전체화면 규칙은 없다.**
- **원본 목록 항목은 창 전체 높이의 발판이 된다**: 가려짐 솔버는 상단선의 좌우만 자른다(`Platform/VisibleTopEdgeSolver.cs:134-137`). Windows는 그 화면 자르기마저 끈다(`Win32WindowService.cs:964`). 발판 사각형은 원본 창의 `y`와 `height`를 그대로 쓴다(`:974-976`). 최상위 표시(`IsTopmost`)는 전경 창 여부다(`:854` `bool isTopmost = hWnd == _foregroundHwndThisPass;`).
- ⇒ **제목이 있고 알파 하한을 넘는 전체화면 앱이 오버레이 화면 위에 있으면, 화면을 덮는 발판이 된다.** 그러면 그라피티는 빈 자리 판정(`GraffitiDirector.cs:208` → `:216-226`)에서 막히고, 크랙은 그 창을 대상으로 잡는다(`WindowCrashDirector.cs:233-240`, 앵커 `if (!f.IsTopmost) continue;`). 둘은 같은 전제 위에 있다.
- **남는 미확인**
  1. 발표 창(슬라이드 쇼 등)에 제목이 있는가 — `:632` 조건. DWM 가림이나 알파 조건에 걸리는 전체화면 앱이 있는가.
  2. **macOS**: 열거(`Platform/MacOS/MacWindowService.cs:1005` 레이어 0 · `:1026-1029` 알파 · 크기 · 화면 표시 · 디스플레이 겹침)에도 전체화면 규칙은 없다. 그러나 네이티브 전체화면(별도 Space)의 창이 이 목록에 어떤 레이어 · 사각형으로 오는지는 실기 없이 판정하지 않는다. macOS 발판의 최상위 표시는 전경이 아니라 「처음 채택된 조각」이다(`:1086-1089`).
  3. 위 그라피티 칸의 틈(상단선이 가려진 폭).

---

## 2. 판정 ① — 원칙 2 기준

1. **자율 발동은 등급 1·2에서 시작하지 않아야 한다(종류 무관).**
   - 선례: 자율 춤은 캐릭터 전용 연출인데도 외부 전체화면 모든 등급에서 꺼진다(`FullscreenSuspendPolicy.cs:302-303`). 창에 그리는 연출이 더 약하게 다뤄질 이유가 없다.
   - 사용자 신호: 확률 기본값 0의 이유가 「사용자가 요청하지 않은 연출이 뜨는 것에 반복적으로 불만」이라고 코드가 적는다(`AppControlDirector.cs:510` · `IGlobalKeyStateService.cs:112`).
   - 크랙 판단: 「발표·화상회의 위에 금 간 유리를 그리는 것은 그 자체가 침해」(`WindowCrashDirector.cs:160-163`, 2026-09-02).
2. **캐릭터 전용 자율 상태(삐짐 · 가출)는 이 판정 밖이다.** 등급 1은 「캐릭터를 남기는」 등급이고(2026-08-31 신고 회귀 방지), 둘은 남의 창 위에 표면을 만들지 않는다. 단 **자율 춤은 끄는데 자율 삐짐은 켜 둔 비대칭**은 원칙이 아니라 연출 판단이다 → `design-motion` 백로그.
3. **사용자 명령**은 ③에서 따로 본다 — 임대 계약(`Core/StickmanAgent.cs:380-381`, 연출에는 허가를 내지 않는다)과 명령 크랙 판정(`AUTO_SURFACE_LEASE_AXIS.md` §9-1)을 따른다.

## 3. 판정 ② — 불변식을 넓힌다, 규칙 하나 · 층 둘

**새 불변식(한 문장)**: **사용자가 부르지 않은 연출은 억제 창구가 참인 동안 시작하지 않고, 남의 창에 붙은 오버레이는 억제 창구가 참이 되면 걷는다.**
- 억제 창구 = `UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(agent.ArePanelsSuppressed, agent.IsUserSummonGrantActive) || agent.IsSuspended` — N-20 · 명령 크랙 A1과 **같은 호출형**이다. 춤은 이미 더 넓은 자기 축(`tier != None`)을 쓰므로 그대로 둔다.

| 층 | 대상 | 오늘 | 필요한 것 |
|---|---|---|---|
| **층 1 — 시작** | 자율 경로 5곳: 크랙 · 창 도둑 · 그라피티 · 활쏘기 · 블랙홀/청소 | 5곳 전부 가드 없음 | 각 `TickAutoTrigger` 맨 앞에 억제 창구(크랙 A2와 같은 형태) |
| **층 2 — 수명** | 창에 붙는 오버레이: 크랙 · 창 도둑 · 그라피티 · 블랙홀/청소 | 크랙만 있음(`:170-171`) 〔B-6: **등급 1 기준**이다. 등급 2에서는 `Core/StickmanAgent.cs` `Suspend`가 `WindowTheft` · `Graffiti` 상태를 Idle로 강제 전이한다(`:1842-1848`). 그 전이에서 두 연출이 오버레이까지 걷는지는 **미확인**〕 | 오버레이 틱에 같은 창구 → 취소. 명령으로 시작한 뒤 등급 1이 켜지는 경우를 덮는다 |

**연출별로 나누지 않는 이유**
1. 같은 뿌리(억제 값을 안 읽음)를 연출마다 따로 고치면, N-20에서 기각한 「포스트잇만 고치기」(`AUTO_SURFACE_LEASE_AXIS.md` §3-5 (바))와 같은 병이 된다.
2. **원칙 4(플러그인 구조)**: 새 연출·DLC 연출은 매니페스트로 기본 로직 무수정 추가돼야 한다. 규칙이 연출마다 흩어져 있으면 새 연출은 **규칙 없이** 태어난다. 규칙은 한 줄로 두고 ~~감사(§4)가 모든 연출에 강제해야 무수정 추가가 안전하다.~~ 〔★ B-4 정정 — 과장이었다〕 감사(§5-2)로 강제한다. **단 그 감사가 덮는 것은 코드로 쓴 연출 파일뿐이다.**
   - §5-2는 프로덕션 소스 텍스트에서 락 획득 · 오버레이 발행 **메서드**를 뽑는다. 그래서 **데이터(매니페스트 · 에셋)만으로 추가하는 연출은 원리상 못 본다.**
   - 지금 DLC 매니페스트 둘(`Core/StickPackManifestSO.cs` · `Core/CostumeManifestSO.cs`)에는 **연출 종류 · 확률 · 발동 조건 필드가 없다.** 팩 매니페스트가 싣는 것은 정체 · 팔레트 · 아이템 수 · 사운드 키 · 대사 세트 키 · 권리 정보다. 코스튬 매니페스트는 프롭 도형 · 전용 키포즈 · 단계별 도형을 싣지만, 그것을 읽는 곳은 코드(`Core/CostumeCatalog.cs` · `States/StickmanBlackboard.cs`)다. 그 파일 문서가 말하는 「연출」은 코스튬이다. ⇒ 데이터만으로 «자율 연출»을 추가하는 형태는 **아직 없다.**
   - 그 형태가 생기면 덮는 것은 백로그의 **락 흡수안**뿐이다(아래 3 — 모든 연출이 지나는 `Core/SpectacleEventLock.cs`에 종류 분류를 넣어 창구를 한 곳에서 읽게 하는 안). 그 전까지 「무수정 추가가 안전」은 **코드로 쓴 연출에 한해** 참이다.
   - 옛 문장의 「§4」는 절 번호가 틀렸다 — 감사는 §5다.
3. 구조적으로 가장 좋은 자리는 모든 연출이 지나는 락(`Core/SpectacleEventLock.cs`)이다. 그러나 그 파일이 **N-8 목록 B**라 지금은 열 수 없다 → 당장은 연출별 호출 + 감사로 강제하고, E-3 결속이 풀린 뒤 「락에 종류 분류를 넣어 흡수」를 백로그로 둔다(되돌릴 수 없는 결정 아님).

## 4. 판정 ③ — 명령 3종

| 명령 | 붙는 곳 | 가능 판정 가드 | 근거 |
|---|---|---|---|
| 창 도둑(⌃⌥⌘T · 명령창) | 남의 작은 창(시각 복사본) | **필요** — 크랙 A1과 같은 호출형 | 남의 창 위 표면. 임대는 연출에 허가를 내지 않는다(`StickmanAgent.cs:380-381`) |
| 그라피티(⌃⌥⌘G · 명령창) | 빈 화면 자리(창 밖) | **필요** | 대상 선정이 막을 수는 있다 ~~(§1-3, 미확인)~~ (§1-3-1 — Windows는 전제가 코드로 대부분 참, macOS 미확인). 그래도 「빈 자리 없음」(`GraffitiDirector.cs:83`)과 「전체화면 앱 위라서 안 됨」은 다른 사유다 — 사유가 상태에서 파생돼야 원칙 1이 선다 |
| 활쏘기(⌃⌥⌘A · 명령창) | 캐릭터가 선 발판 | **불필요** | 캐릭터 소품이다. 등급 1이 캐릭터를 남기는 결정과 같은 편이다 |

- 사유 문구는 등급 1과 FFT(등급 1이 끝났는데 사용자 창이 열린 채) **양쪽에서 참**이어야 한다 → `design-narrative`. 크랙 A1과 한 문구를 공유하는 것을 권한다.
- 층 2 수명 가드는 명령으로 시작한 오버레이에도 걸린다 — 명령으로 시작한 뒤 전체화면 발표가 켜지면 걷는다.

## 5. 판정 ④ — 감사 요구 (`test-engineer` 문안)

### 5-1. 왜 T-B가 못 보는가
T-B의 출발점은 `PanelsRead = "." + nameof(StickmanAgent.ArePanelsSuppressed)`(`UnsummonedSurfaceAxisTests.cs:170`)다. **억제 값을 아예 안 읽는 연출은 독자 목록에 오르지 않는다.** 오늘 가드가 없는 5곳이 정확히 그 형태다 — 스캐너가 「빠진 가드」를 「대상 아님」과 구별할 수 없다.

### 5-2. 새 감사(가칭 `UnsummonedEffectsGuardAuditTests`, EditMode · 소스 텍스트 · 활성 빌드 타깃 무관)
1. **출발점을 바꾼다**: 프로덕션 소스에서 `SpectacleEventLock.TryAcquire(`를 부르는 **메서드**와 `StickmanEventBus.Raise…OverlayChanged(`를 부르는 **메서드**의 합집합. 식별자는 `nameof(SpectacleEventLock.TryAcquire)` 등으로 참조한다(CLAUDE.md 식별자 규칙).
2. **분류 표를 양방향으로 대조한다**: {파일·메서드 → 자율 시작 / 명령 가능 판정 / 오버레이 수명 틱 / 캐릭터 전용 면제(사유)}. 뽑혔는데 표에 없으면 빨강, 표에 있는데 뽑히지 않으면 빨강, 파일 실재는 존재 단언.
3. **자율 시작 메서드**: 락 획득 또는 `Started` 발행 **앞**에 억제 창구 호출(인자 목록에 `.ArePanelsSuppressed`와 `.IsUserSummonGrantActive`)과 `.IsSuspended`가 있어야 한다.
4. **창에 붙는 명령**: `GetAvailability` 본문에 같은 호출. **창에 붙는 오버레이 수명 틱**: 같은 호출 + 취소.
5. **아직 못 고친 항목은 `Assert.Ignore`(사유 포함)로 러너에 계속 보이게 한다**(CLAUDE.md 패리티 규칙과 같은 관례). 면제 목록은 기대값을 명시한다 — 빈 목록이 조용히 통과하지 않게(`docs/TEAM.md` 거짓 통과 #5).
6. **교정**: 합성 표본 — 가드 있는 자율 / 없는 자율 / 락 없이 이벤트만 발행 / 주석·문자열 속 호출(잡지 말 것). T-C의 어휘기(`UnsummonedSurfaceAxisTests` 자기교정)를 재사용한다.
7. **변이**: 크랙 `TickAutoTrigger` 가드 삭제 → 빨강 · 표에 없는 새 연출 파일 추가 → 빨강 · 가드를 `ArePanelsSuppressed` 단독으로 되돌림 → 빨강.
8. **PlayMode 보완**(연출별 1케이스): 테스트에서 자율 확률을 1로 올리고 등급 1을 주입 → 락 획득 0 · 오버레이 이벤트 0. 음성 대조로 등급 0에서 ≥1. 확률 원복은 TearDown에서 하고, 시작 값은 상수로 가정하지 않고 기록한다.

## 6. 판정 ⑤ — N-8 겹침 · 1.0 필수 여부

### 6-1. N-8
- **목록 B**: `Interaction/WindowTheftDirector.cs`(자율·명령·오버레이가 모두 이 파일), `Core/SpectacleEventLock.cs`, `Core/StickmanAgent.cs`, `Interaction/DanceEpisodeDirector.cs`, `Interaction/RunawayDirector.cs`, `Interaction/DragThrowController.cs`.
- **목록 밖**: `WindowCrashDirector.cs` · `GraffitiDirector.cs` · `ArcheryDirector.cs` · `DesktopIconMirrorDirector.cs` · `StressGaugeDirector.cs` · `TodoReminderDirector.cs` · `FocusWatchDirector.cs` · `RodeoCursorWatcher.cs`.
- ⇒ **창 도둑 가드(층 1·2·명령)는 E-3 증거 결속 해제 뒤다.** 락 흡수안도 같다. 나머지 가드와 새 감사(테스트 파일)는 지금 넣을 수 있다.

### 6-2. 1.0

| 항목 | 1.0 | 근거 |
|---|---|---|
| 크랙 명령 A1 | **필수** ~~(N-20 해제 조건 추가 권고 — `AUTO_SURFACE_LEASE_AXIS.md` §9-1)~~ 〔ROADMAP N-20 해제 조건 7로 반영됨(리더 채택) — 작업 트리, 앵커 `행동 명령창 [창 부수기]와 ⌃⌥⌘X의 가능 여부 판정에 같은 창구 가드`〕 | N-20이 새로 만든 원칙 1 회귀 |
| 창 도둑 · 그라피티 명령 가드 | ~~**필수 권고 — 단 노출 실측 1회 선행**~~ **1.0 필수 — ROADMAP N-20 해제 조건 7(A1)의 확장 7-b**(새 게이트 아님 · ROADMAP N-20 해제 조건 7-b로 등재(묶음 ⑧), 앵커 `창 도둑 · 그라피티 명령의 가능 여부 판정에 같은 창구 가드`) | 출하 빌드에서 단축키·명령창으로 즉시 도는 경로이고, 등급 1은 발표·회의 전용 등급이다. ~~실측: 전체화면 발표 위에 작은 창을 띄운 뒤 ⌃⌥⌘T · ⌃⌥⌘G를 누른다(양 플랫폼). **노출이 0이면 1.0 비필수로 내린다.**~~ 창 도둑은 N-8 B라 E-3 결속 해제가 선행한다. 〔**리더 판정 2026-09-15: 노출 입구 코드 확정으로 단서 폐기 — ROADMAP N-20 조건 7-b 1.0 필수**〕 입구: G `AppControlDirector.cs:362` · T `:363`(키 조회 `:295` · `:296`에 `dev` 없음) · 행동 명령창 `ActionCommandPopover.cs:513` · `:515`(같은 Director의 `GetAvailability`). 두 Director의 `GetAvailability`에는 `SuppressesUnsummonedSurfaces` 호출이 0건이다(§1-2). 그래서 실측 없이도 노출은 0보다 크다 — 창 도둑은 가려진 작은 창 하나로 충분하고(§1-3 B-1), 그라피티는 Windows에서 대상 선정이 막더라도 사유가 상태에서 파생되지 않는다(§4). **순서**: 그라피티는 N-8 목록 밖이라 **A1 커밋 직후 coder-ui 러너 줄**에서, 창 도둑은 N-8 B등급이라 **E-3 결속 해제 뒤**에 한다 |
| 자율 층 1 · 층 2 가드 | 비필수 | 출하 확률 전부 0, 사용자 노출 설정 0 〔★ B-2: 로데오 커서는 이 행 밖이다 — ⌃⌥⌘R이 개발 게이트 밖에서 자동 발동 게이트를 켜고 감시자의 억제 판독이 0이다(§1-2 「예외 — 로데오 커서」). 등급 1 · 2 동작 판정은 후속 라운드〕 |
| 새 감사(§5-2) | 비필수 — **대신 출하 규칙**: 자율 확률을 0보다 크게 출하하는 라운드(에셋 · DLC 매니페스트 포함)는 이 감사 GREEN을 선행 조건으로 한다 〔★ B-4: 지금 DLC 매니페스트에는 확률 필드가 없다. 매니페스트가 확률이나 발동을 싣게 되는 라운드는 이 감사가 그 형태를 못 보므로(§3 「연출별로 나누지 않는 이유」 2) **락 흡수안이 선행 조건**이다〕 | 확률이 0인 동안에는 사용자 노출이 없고, 0이 아니게 되는 순간 이 규칙이 필요해진다 |

---

## 7. 담당 제안 (배정은 리더)

| 항목 | 제안 담당 |
|---|---|
| 층 1 · 층 2 가드(목록 밖 4파일) · 창 도둑 · 그라피티 명령 가드 | `coder` (창 도둑은 E-3 결속 해제 뒤) |
| 사유 문구(크랙 A1과 공유) | `design-narrative` |
| 새 감사 `UnsummonedEffectsGuardAuditTests` · PlayMode 보완 | `test-engineer` |
| 노출 실측 1회(~~창 도둑 · 그라피티 ·~~ 크랙 자율 취소 페이드) 〔★ B-5: 창 도둑 · 그라피티 명령 가드의 게이트가 아니다. 남는 실측 대상은 크랙 자율 취소 페이드와 §1-3-1 미확인 셋(발표 창 제목 · macOS 네이티브 전체화면 · 상단선 틈)이다〕 | 체크표 소유 `qa-regression` + 사용자 낮 세션(리더 판단) |
| 자율 춤 끔 ↔ 자율 삐짐 켬 비대칭 | `design-motion` |
| 락 흡수 구조안 | E-3 결속 해제 뒤 `game-architect` 재검토 |
| 로데오 커서 등급 1 · 2 동작 판정(§1-2 예외의 미확인) | 후속 라운드 — 배정은 리더 |
| `AppControlDirector.cs:448` 로그 「거처는 설정창 [이벤트]」 낡음(`SettingsWindow.cs` 로데오 참조 0) | code-inspection 백로그 |
| `WindowTheftDirector.cs` `ResolveCandidateSource` 주석의 「Windows 폴백」 낡음(앵커 `채널이 없는 플랫폼(Windows/모바일/에디터 폴백)`, HEAD `:194`). Win32도 원본 창 목록 채널을 구현한다(`Win32WindowService.cs:1158` `RawWindows`). N-8 B등급 파일이라 `.cs`는 건드리지 않는다 | code-inspection 백로그(리더 전달 사실 · 직접 확인) |

## 8. 무엇을 안 봤는가 · 미확인
- ~~전체화면 앱 창과 그 위의 작은 창이 발판 목록에 오르는지(가려짐 제거 규칙 `Win32WindowService.cs:894` · `MacWindowService.cs:365` 부근을 읽지 않았다) — §1-3의 그라피티·창 도둑 판정이 여기에 달렸다.~~ 〔★ B-1 · B-3 — 읽었다〕 창 도둑 후보는 발판 목록이 아니라 원본 창 목록이라 이 질문과 무관하다(§1-3, 코드 확정). 전체화면 앱 창이 발판에 오르는지는 Windows가 코드로 대부분 판정됐다(§1-3-1). 남는 것은 발표 창 제목 · macOS 네이티브 전체화면 · 상단선 틈이다.
- 크랙 자율 취소 페이드가 화면에서 보이는지(0.12초, 성장 첫 프레임의 조각 수) — 실기 0.
- 락을 잡지 않고 화면에 그리는 연출이 더 있는지 — 출발점을 락 호출로 잡았으므로 락 밖 연출은 이 표에 없다. §5-2의 합집합(오버레이 발행)이 그 구멍을 좁히지만 이벤트 버스 밖에서 그리는 코드는 여전히 못 본다.
- `persona-stress` F3 원문은 읽지 않았다(리더 요약만).
- 로데오 커서의 등급 1 · 2 동작(§1-2 예외) — 후속 라운드.
- 등급 2 강제 Idle(`Core/StickmanAgent.cs:1842-1848`)에서 창 도둑 · 그라피티 오버레이가 걷히는 이벤트까지 나는지(§3 층 2).
- 코스튬 매니페스트의 프롭 · 키포즈를 어느 흐름이 언제 그리는지는 읽는 파일 이름(`CostumeCatalog.cs` · `StickmanBlackboard.cs`)까지만 봤다(§3 B-4).

## 플랫폼 영향
- **Windows 영향**: 규칙·감사는 플랫폼 중립 코드(`Interaction/` · `Platform/` 정책)에 걸린다. 블랙홀·청소는 Windows 아이콘 서비스가 스텁이라 지금 돌지 않는다. 트레이는 연출과 무관하다. 〔2026-09-15 B-1 · B-2 · B-3〕 창 도둑 원본 채널이 `_rawBuffer`(스타일 · 기하 필터 통과, 가려짐 전)라 등급 1에서 가려진 작은 창으로도 발동한다. 제목 · 알파 조건을 넘는 전체화면 앱은 화면을 덮는 발판이 되어 그라피티는 막히고 크랙은 그 창을 잡는다(§1-3-1). ⌃⌥⌘R 토글은 플랫폼 중립 파일(`AppControlDirector.cs`)에 있고, Windows도 전역 키 조회 서비스를 구현한다(`Win32WindowService.cs:90` `IGlobalKeyStateService`).
- **macOS 영향**: 같다. 블랙홀·청소는 macOS도 미구현이라 돌지 않는다. 〔2026-09-15 B-1 · B-2 · B-3〕 창 도둑 원본 채널(`_rawWindowBuffer`)도 가려짐 전이라 결론이 같다. 전체화면 앱 창이 발판에 오르는지는 네이티브 전체화면 Space 때문에 미확인이다. ⌃⌥⌘R 경로는 같은 파일이다.

## Tasklist 등재 문장 (리더가 옮긴다)
> ~~**[game-architect] 사용자가 부르지 않은 연출 — 불변식 범위 판정**(`docs/systems/UNSUMMONED_EFFECTS_INVARIANT.md`, 문서만, HEAD `ad49497`): 연출 락을 잡는 12파일 전수. 자율 경로 5곳(크랙·창 도둑·그라피티·활쏘기·블랙홀/청소)은 억제·숨김 값을 읽지 않는다(출하 확률 전부 0, 사용자 노출 설정 0). 판정 — ① 자율 발동은 종류 무관 등급 1·2에서 시작하지 않아야 한다(선례: 자율 춤 `tier != None`) ② N-20 불변식을 넓힌다: 층 1 시작 가드(자율 5곳) · 층 2 오버레이 수명 가드(창에 붙는 4종), 창구는 N-20 호출형 ③ 명령 가드는 창 도둑·그라피티 필요, 활쏘기 불필요(캐릭터 소품) ④ T-B는 출발점(`ArePanelsSuppressed` 독자) 때문에 구조적으로 못 본다 → 락·오버레이 발행 메서드 출발 새 감사 요구 ⑤ 창 도둑은 N-8 B(E-3 결속 해제 뒤), 창 도둑·그라피티 명령 가드는 노출 실측 1회 뒤 1.0 필수 권고, 자율 가드·감사는 비필수 + 「확률 > 0 출하 라운드는 감사 GREEN 선행」 출하 규칙.~~

〔★ 2026-09-15 갱신본 — 위 문장을 대체한다〕
> **[game-architect] 사용자가 부르지 않은 연출 — 불변식 범위 판정(verify-change B 조건부 반영판)**(`docs/systems/UNSUMMONED_EFFECTS_INVARIANT.md`, 문서만, HEAD `ad49497`): 판정 다섯 개(자율 발동 금지 · 규칙 하나 층 둘 · 창에 붙는 명령 가드 · 감사 출발점 · N-8과 1.0)는 유지하고, 사실 6건을 코드로 다시 읽어 고쳤다. (1) 창 도둑 후보는 가려짐 필터 전 원본 창 목록이라 **자격 있는 창이 하나라도 있으면 등급 1에서 막히지 않는다**(코드 확정 · 옛 「미확인」 폐기). (2) 「출하 기본값에서 자율 경로가 돌지 않는다」 전칭에서 **로데오를 제외**하고 근거를 적었다 — ⌃⌥⌘R은 개발 게이트 밖에서 자동 발동 게이트를 켜고 감시자 억제 판독은 0이다(등급 1·2 동작은 후속 라운드). (3) Windows 스타일·기하 필터에 전체화면 규칙이 없어, 제목·알파 조건을 넘는 전체화면 앱이 화면을 덮는 발판이 된다 → 그라피티는 막히고 크랙은 그 창을 잡는다(같은 전제로 통일 · 남는 미확인: 발표 창 제목·macOS 네이티브 전체화면·상단선 틈). (4) §5-2 감사는 코드로 쓴 연출만 덮는다 — 지금 DLC 매니페스트에는 연출·확률·발동 필드가 없고, 데이터만으로 추가하는 연출은 락 흡수안만 덮는다. (5) 창 도둑·그라피티 명령 가드는 **ROADMAP N-20 해제 조건 7-b로 등재(묶음 ⑧) · 1.0 필수** — 리더 판정으로 「노출 실측 선행 · 0이면 비필수」 단서를 폐기했다(입구 G `:362` · T `:363` · 명령창 `:513`·`:515`, 두 `GetAvailability`에 창구 0). 그라피티는 A1 커밋 직후 coder-ui 러너 줄, 창 도둑은 E-3 결속 해제 뒤. (6) 경미 5건(경로 · 목록 행 수 · 숨김 게이트 표기 4칸 · 층 2 등급 2 미확인 · 표 앞 빈 줄). 백로그(code-inspection): `AppControlDirector.cs:448` 로그의 설정창 [이벤트] 문구 낡음 · `WindowTheftDirector.cs` 후보 소스 주석의 「Windows 폴백」 낡음(N-8 B라 `.cs` 무수정).

## 정정 기록 (2026-09-15 · verify-change 판정 B 조건부 · 결합 sha `ae6cc71c5e30a45f` 기준)

이 파일은 아직 커밋된 적이 없다(초판 미커밋). 리더 지시로 옛 문장은 본문에 취소선으로 남겼고, 위치를 여기 모은다.

| 지적 | 초판 요지 | 정정 | 위치 |
|---|---|---|---|
| B-1 | 창 도둑: 작은 창이 발판 목록에 있으면 후보 — 미확인 | 원본 창 목록(가려짐 전)에서 뽑는다. 자격 있는 창이 하나라도 있으면 등급 1에서 막히지 않는다(코드 확정) | §1-3 창 도둑 칸 · §8 |
| B-2 | 출하 빌드에서 자율 경로가 돌지 않는다(전칭) | 로데오 제외 · 제외 근거 · ⌃⌥⌘R 게이트 밖 토글 · 억제 판독 0을 명시. 등급 1·2 동작은 후속 | §0 ① · §1-2 「예외 — 로데오 커서」 · §6-2 · §7 · §8 |
| B-3 | 그라피티는 전제 미확인, 크랙은 단정 | Windows 코드 판정(§1-3-1). 크랙과 그라피티를 같은 전제로 통일 | §1-3 · §1-3-1 · §4 · §8 |
| B-4 | 감사가 모든 연출에 강제하면 무수정 추가가 안전 | 감사 범위는 코드 연출뿐. 데이터 연출은 락 흡수안만 덮는다 | §3 2 · §6-2 |
| B-5 | 명령 가드 1.0 필수 권고(노출 실측 선행, 0이면 비필수) | ROADMAP N-20 해제 조건 7-b로 등재(묶음 ⑧) · 1.0 필수. 실측 단서는 리더 판정으로 폐기 | §0 ⑤ · §6-2 · §7 |
| B-6 | 경로 누락 · 「목록 행 46」 · 숨김 게이트 누락 · 층 2 등급 기준 · 표 앞 빈 줄 | 각각 보강. 숨김 게이트는 지목된 가출·집중 외에 같은 칸 기준으로 크랙·리마인더도 채웠다(같은 경로 전수) | §1-1 · §1-2 표 · §3 층 표 · §6-2 |
| 자기 발견 | 초판 §0 ⑤의 「§5-2」(노출 실측 참조) · §3 2의 「감사(§4)」 | 절 번호가 틀렸다 — 1.0 판정은 §6-2, 감사는 §5다 | §0 ⑤ · §3 2 |
