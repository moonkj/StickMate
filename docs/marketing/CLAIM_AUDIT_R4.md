# 홍보 문구 전수 감사 — R4 (2026-09-02 밤)

작성: `marketing` · 방법: **`docs/marketing/` 전 문서의 주장을 코드/에셋에 1:1 대조**
기준 트리: 커밋 `7ed996d`(09-02 19:24) · 기준 빌드: macOS `19:07` / Windows `12:42`

> **규칙 하나**: 이 문서에 있는 모든 주장에는 **「어느 파일 어느 줄이 이걸 참으로 만드는가」**가 붙는다.
> 못 붙인 문장은 아래 4절(**폐기**)로 내려간다.
>
> ★ **이 라운드에 앱을 실행하지 않았다.** 전부 소스·에셋·빌드 산출물 정적 실측이다.

---

## 0. 결론 세 줄

1. **금지 목록 3건이 오늘 저녁 커밋으로 뒤집혔다.** `STORE_PAGE.md` 6절이 *"거짓이니 쓰지 마라"*고
   막아 둔 문장 중 **셋이 지금은 참**이다. 막아 두는 것도 갈라짐이다 — 아래 1절.
2. **홍보 문구 자체에서 새로 발견된 거짓은 0건이다.** 숫자 주장 21건을 다시 쟀고 **전부 일치**했다.
3. **다만 인용 좌표 5건이 죽어 있다**(줄 번호 표류 4 / 삭제된 코드 1). 주장은 참인데 **근거가 없는 상태**다.

---

## 1. ★★ 오늘 저녁 커밋으로 **참이 된** 문장 3건 (가장 중요)

`7ed996d`(19:24)가 두 가지를 배선했다. 우리 문서는 아직 그 전 세계에 있다.

### 1-1. 「전체화면 앱 위에서 창·패널이 물러난다」 — **참이 됐다**

| 무엇 | 근거 (파일:행) |
|---|---|
| 등급이 **둘**이다 | `Platform/FullscreenSuspendPolicy.cs:208-225` `enum ForeignFullscreenTier { None, PanelsOnly, Full }` |
| 등급 1 = **덮음 O · 게임 X** | 같은 파일 `:251-255` `Resolve(coversDisplay, isGame)` |
| 등급 1은 **표면만** 걷는다 | 같은 파일 `:272-273` `RetreatsPanels(tier) => tier != None` |
| **캐릭터는 등급 2에서만** 사라진다 | 같은 파일 `:264-265` `SuspendsCharacter(tier) => tier == Full` |
| 실제로 두 축이 갈려 배선됨 | `Core/StickmanAgent.cs:1185`(등급 1회 조회) `:1198`(캐릭터 축) `:1204`(표면 축) |
| 양 플랫폼이 등급을 올려보낸다 | `Platform/MacOS/MacWindowService.cs:65` / `Platform/Windows/Win32WindowService.cs:95` 둘 다 `IForeignFullscreenTierSource` 구현 |
| 기본값이 켜져 있다 | `Core/AppSettingsModel.cs:80` `AutoHideOnFullscreen { get; private set; } = true` |

> ### ★ 쓸 수 있는 문장 (이 두 줄을 붙여서만 쓴다)
> **"발표·전체화면 앱 위에서는 창과 패널이 스스로 물러납니다(캐릭터는 남습니다).**
> **전체화면 게임에서는 캐릭터까지 사라집니다."**

**★ 함께 지켜야 하는 4가지 — 하나라도 빼면 그 순간 거짓이 된다**

| # | 조건 | 근거 |
|---|---|---|
| C1 | **창모드는 해당 없다.** 등급 1의 조건은 "발표 중"이 아니라 **"디스플레이를 덮는다"**다. 창모드 Zoom·Teams는 안 걸린다 | `FullscreenSuspendPolicy.cs:253` `if (!coversDisplay) return None` |
| C2 | **macOS와 Windows의 관용도가 다르다.** macOS는 상단 시스템 스트립(≤5%)을 허용하고, **Windows는 정확일치만** 본다 | 같은 파일 `:117-124`(클래스 문서, 의도적 분기) / `:131` `MatchesExactly` |
| C3 | **사용자가 끌 수 있다.** 설정창 [일반]에서 끄면 두 등급 모두 죽는다 | `StickmanAgent.cs:1198,1204` 둘 다 `AppSettingsModel.AutoHideOnFullscreen &&` 로 시작 |
| C4 | ★ **걷힌 창은 저절로 안 돌아온다.** 상시 HUD(톱니·포스트잇)만 스스로 복귀한다 | `StickmanAgent.cs:1284-1287` 해제 로그 원문 |

★ **요약 금지**: `FullscreenSuspendPolicy.cs:19-23`이 못박아 둔 그대로,
이 문단을 **"게임이 아니면 아무것도 안 한다"**로 줄이면 거짓이다. 등급이 둘이다.

### 1-2. 「단축키로 즉시 숨길 수 있다」 — **참이 됐다** (금지 해제)

`STORE_PAGE.md` 6절과 `personas/README.md`가 근거로 든 `SettingsWindow.cs:1042 = enabled:false`는
**그 코드가 지금 없다.**

| 무엇 | 근거 (파일:행) |
|---|---|
| 단축키 글자 | `Core/StickmanAgent.cs:220` `public const string UserHideHotkeyLetter = "K"` |
| 토글이 **실제 단축키를 달고** 만들어진다 | `Interaction/SettingsWindow.cs:1118-1125` (`hotkey: ShortcutLabel.Chord(...)`, `enabled:` 인자 **없음** = 활성) |
| 전역 키 폴링이 실제로 K를 읽는다 | `Interaction/AppControlDirector.cs:246` `bool kKey = chord && IsKeyDown(GlobalKey.K)` |
| **양 플랫폼 키코드 매핑 존재** | `Platform/MacOS/MacWindowService.cs:2033` `case GlobalKey.K: code = kVK_ANSI_K` / `Platform/Windows/Win32WindowService.cs:1530` `case GlobalKey.K: letter = 'K'` |
| 옛 "배선 대기" 안내를 지운 기록 | `SettingsWindow.cs:480-481` 주석 — *"실제로 동작한다(GlobalKey.K). 잠긴 행 목록에 남겨두면 이 로그 자체가 거짓이 된다"* |

**★ 쓸 수 있는 조건**: 실기 검증이 아직 없다(2절). **"됩니다"가 아니라 기능 목록 한 줄**로만.

### 1-3. 「보일지 말지는 당신이 정합니다」 — **게이트가 풀렸다**

`STORE_PAGE.md` 2-5절이 *"`SettingsWindow.cs:1042`가 `enabled:false`라 쓰면 거짓"*이라며 잠가 둔 줄이다.
그 게이트의 **두 조건이 모두 참이 됐다**:

| 게이트 조건 | 지금 |
|---|---|
| 숨김 단축키 배선 | ✅ 1-2절 |
| 숨김이 **지속**될 것 | ✅ `StickmanAgent.cs:1250` `shouldSuspend = _fullscreenAutoHide \|\| _userHidden` — **사용자 축이 독립**이라 전체화면 왕복으로 안 풀린다 |
| 숨기면 **표면까지** 걷힐 것 | ✅ `SettingsWindow.cs:253-255` `HideEscapeCaption` 원문: *"캐릭터도 열린 창도 함께 사라져요. 다시 부르려면 ⌃⌥⌘K — 이 방법뿐입니다."* |

> ★ **이 한 줄이 `personas/README.md`가 말한 「여섯 페르소나가 동시에 걸린 유일한 지점」이다.**
> R1·R3·R4·R5 + M2 + M5가 같은 줄에 걸려 있었고, **그 줄이 오늘 풀렸다.**
> 마케팅 판단으로는 **이번 라운드 최대 사건**이고, 그래서 3절 캡처 요청의 1순위다.

### 1-4. ★ 내가 오해했다가 실측으로 반증한 것 1건 (자진 기록)

빌드 DLL에서 **옛 캡션 문자열 "전체화면 앱을 오갔다"가 여전히 나와서** 화면에 낡은 문구가
남은 줄 알았다. **틀렸다.** 소스 전수 결과 남은 2곳은 **정정된 문장의 일부**였고 둘 다 `Debug.Log`다:

- `StickmanAgent.cs:207-209` — *"…전체화면 앱을 오갔다 **와도 되살아나지 않습니다**"*
- `AppControlDirector.cs:507-508` — 같은 취지

**사용자에게 보이는 캡션은 `HideEscapeCaption` 하나뿐이고 새 문장이다.** 문자열 조각으로
판정하면 이렇게 뒤집힌다 — 부분 문자열 검색은 **뜻을 못 본다**.

---

## 2. ★ 빌드 실측 — **두 빌드가 서로 다른 제품이다** (캡처 계획의 전제)

.NET 문자열은 UTF-16이라 `strings`로는 안 잡힌다(거짓 통과 4번 형태).
**바이트 단위로 UTF-16LE / UTF-8 양쪽을 세고, 양성·음성 대조를 붙였다.**

```
python3: dll_bytes.count("표면회수".encode("utf-16-le"))
```

| 니들 | macOS 19:07 | Windows 12:42 |
|---|---:|---:|
| `표면회수` (등급 1 로그) | **2** | **0** |
| `이 방법뿐입니다` (새 숨김 캡션) | **1** | **0** |
| `걸치는 것` — **양성 대조** | 1 | 1 |
| `ZZZ없는문자열` — **음성 대조** | 0 | 0 |

> ### ★★ 결론 — 이걸 모르면 거짓 소재가 나온다
> **현행 Windows 배포본(`windows-preview-20260902b`)에는 등급 1도 숨김 단축키도 없다.**
> 커밋 `7ed996d`는 **19:24**, Windows 빌드는 **12:42**다.
>
> → **1절의 세 문장을 Windows 화면으로 찍으면 그 순간 거짓 소재다.**
> → Windows 소재는 **재빌드 후**에만. 이건 문안 문제가 아니라 **촬영 순서 문제**다.

---

## 3. 다시 잰 숫자 21건 — **전부 일치** (표류 없음)

| 주장 | 문서 | 실측 근거 (파일:행) | 판정 |
|---|---|---|---|
| 아이템 **42종** | STORE 3-11 | `ls Assets/_Project/Resources/Items/*.asset \| wc -l` = **42** | ✅ |
| 신규 해금 **7개** | TRUTH R3-3 | `grep -l "requiredLevel: 1$"` = **7개**(파일명 전수 확인) | ✅ |
| 그중 1개가 「없음」 → **실질 6종** | 같은 곳 | `Items/look_fx_none.asset` `displayName: "없음"`(=없음) | ✅ |
| 보관함 헤더 `걸치는 것 (7 / 42)` | 공통실측 | `Interaction/CharacterInfoWindow.Inventory.cs:43` | ✅ |
| 행동 명령 타일 **5개** | STORE 3-8 | `Interaction/ActionCommandPopover.cs:170-177` enum 5항목 | ✅ |
| 타일 수가 **enum에서 파생** | — | 같은 파일 `:185` `CommandCount = Enum.GetValues(...).Length` | ✅ |
| 행동 명령창 **480 × 508** | SHOT D-3 | 같은 파일 `:75` `Width = 480f` / `:97` `Height = 508f` | ✅ |
| 장난 확률 **전부 0** | TRUTH 2절 | `DefaultStickConfig.asset` `:138 windowTheft 0` `:145 desktopTidy 0` `:149 blackhole 0` `:153 graffiti 0` `:162 windowCrash 0` `:177 todoReminder 0` `:208 stressSulky 0` `:312 archery 0` `:110/:114 wander*Jump 0` | ✅ 10/10 |
| `ledgeHangChance 0.35` | SHOT A-4 | 같은 에셋 `:71` | ✅ |
| `hopDownChance 0.5` / `stepUpChance 0.85` | TRUTH 1절 | `:84` / `:92` | ✅ |
| `parkourClimbDuration 1.20` | TRUTH 1절 | `:41` = `1.2` | ✅ |
| `idleChatterChance 0.28` / `walkChatterChance 0.14` | TRUTH 1절 | `:231` / `:232` | ✅ |
| `wanderRestExtendSitChance 0.15` | TRUTH 1절 | `:120` | ✅ |
| `idleAmbientMotionEnabled: 1` | TRUTH 1절 | `:361` | ✅ |
| `clickThroughDefaultEnabled: 1` | STORE 3-7 | 같은 에셋 `:97` | ✅ |
| `m_DisableAudio = 1` | STORE 3-9 | `ProjectSettings/AudioManager.asset:18` | ✅ |
| `submitAnalytics: 0` | STORE 5절 | `ProjectSettings/ProjectSettings.asset:93` | ✅ (★ 4-1절 경고) |
| FramePacing **4단계** | TRUTH 1절 | `Platform/FramePacing.cs` `Active/Calm/Still/Suspended` (`:526,553,566`) | ✅ |
| 던지기 회전이 **파라미터화** | SHOT A-1 | `States/ThrowTumbleState.cs` `ResolveSpinSpeedDegreesPerSecond` | ✅ |
| 던지기에 **대사 없음**(설계) | MOMENTS 99 | `ThrowTumbleState.cs:215-217` *"대사는 만들지 않는다"* | ✅ |
| `[화면클램프]`는 진단 스위치와 무관 | CAPTURE 113 | `Core/StickConfig.cs:1534` | ✅ |

---

## 4. ★ 지켜 주는 장치가 없는 주장 / 죽은 인용

### 4-1. 「네트워크 0」 — **지금은 참, 그러나 무방비다**

| 잰 것 | 결과 |
|---|---|
| `ProjectSettings/ProjectSettings.asset:93` | `submitAnalytics: 0` ✅ |
| ★ **이 값을 검사하는 테스트/도구** | `grep -rn "submitAnalytics" Assets/ Tools/` = **0건** |
| 왜 | `Tests/EditMode/OfflineFirstNetworkAuditTests.cs:459`가 `Directory.GetFiles(scriptsRoot, "*.cs", ...)` — **`.cs`만 본다.** `ProjectSettings/`는 스캔 범위 밖 |
| 양성 대조 | `ProjectSettings/`를 읽는 테스트는 실재한다(`DockGeometryInvariantTests` 등 2건) → **불가능해서 없는 게 아니라 안 걸어둔 것** |
| 남은 사실 | `Packages/manifest.json:28`에 `com.unity.modules.unityanalytics` 존재(빌트인이라 정상이지만 **존재 ≠ 무통신**) |

> **규율(유지)**: **"인터넷에 접속하지 않습니다"는 출하 빌드 아웃바운드 실측 후에만 페이지에 올린다.**
> **추가 요청(리더 경유)**: `test-engineer` 또는 `security`에게
> **`ProjectSettings/ProjectSettings.asset`의 `submitAnalytics`를 읽는 감사 1건** 신설.
> 지금은 **누가 1로 되돌려도 러너가 초록이다.** 그리고 그 순간 우리 스토어 문장이 거짓이 된다.

### 4-2. 「권한 0」 — **참이고, 지켜야 할 자산이다**

| 잰 것 | 결과 |
|---|---|
| 합성 입력 API (`SendInput` / `CGEventPost` / `SetCursorPos` / `mouse_event` / `keybd_event` / `CGWarpMouseCursorPosition` / `CGEventCreateMouseEvent`) | `Assets/` 전체 **0건** |
| 접근성·화면기록 권한 API (`AXIsProcessTrusted` / `CGRequestScreenCaptureAccess` / `CGPreflightScreenCaptureAccess`) | **0건** |
| macOS 창 **제목** 키 `kCGWindowName` | **0건** |
| ★ **양성 대조 1** | 같은 alternation 문법이 `CGWindowListCopyWindowInfo`로 **26건** 매치 → 프로브는 살아 있다 |
| ★ **양성 대조 2** | `kCGWindowOwnerName` **3건**(`MacWindowService.cs:559,811,866`) → 앱 **이름만** 읽는다는 문장의 실물 |
| 조회 전용 API는 실재 | `GetCursorPos` 계열이 **프로덕션 15파일**(테스트 0) → "안 읽는 게 아니라 **쓰지 않는다**" |

> ★★ **이건 지켜야 할 자산이다.** 온보딩 자동 클릭·데모 자동 시연 같은 것을
> **합성 입력으로 만들면 macOS 접근성 권한이 필요해지고, 위 표 전체가 한 줄로 무너진다.**
> 마케팅이 이 사실을 문서에 남기는 이유는, **가장 좋은 소재를 만들려다 가장 강한 문장을
> 잃는 경로가 실재하기 때문**이다. → 리더 경유로 전원에게 공유 요청.

### 4-3. 죽은 인용 5건 — **주장은 참, 좌표가 틀렸다**

| 문서 | 인용 | 실제 | 조치 |
|---|---|---|---|
| TRUTH 0절 | `MacWindowService.cs:465,608` (제목 안 읽음) | 465=`RejectOffDisplay` 상수 / 608=무관 주석. **표류** | → **`kCGWindowName` 0건 + `kCGWindowOwnerName:559,811,866`**으로 교체 |
| TRUTH 0절 | `MacWindowService.cs:102,110,213,248` (권한 불필요) | 213/248은 `CFStringGetTypeID` DllImport. **표류** | → **권한 API 0건 + 양성 대조**로 교체(4-2절) |
| TRUTH 0절 | `Win32WindowService.cs:153` (권한 불필요) | `GetThreadTimes` 줄. **표류** | → 같은 교체 |
| ROADMAP 238 | `StickmanBlackboard.cs:1663` (화면클램프) | 실제 `:1724` | 줄 번호 갱신 |
| personas/README 118 | `SettingsWindow.cs:438` "⌃⌥⌘V 배선 대기" | **그 코드는 삭제됐다**(`SettingsWindow.cs:480-481`이 삭제 사실을 기록) | **삭제** — 1-2절로 대체 |

★ **살아 있는 인용**: `Win32WindowService.cs:238,252`(DllImport 2종) / `:487`(`TitleProbeBufferChars = 2`) —
**세 좌표 모두 정확하다.** 다만 호출 지점은 `:673,674,698`이므로 함께 적는 편이 낫다.

> ### ★ 규율 하나 추가 — 줄 번호를 단독 근거로 쓰지 않는다
> 이 저장소는 하루에 수백 줄이 움직인다. **줄 번호는 5건 중 4건이 하루 만에 썩었다.**
> 앞으로 인용은 **`파일 + 심볼/문자열` 을 1차로, 줄 번호는 보조**로 적는다.
> (`TitleProbeBufferChars`는 안 썩었다 — **이름으로 걸었기 때문이다.**)

---

## 5. 여전히 **금지**인 것 (변동 없음 — 다시 확인함)

| 금지 | 왜 (재확인) |
|---|---|
| **"화면 공유를 자동으로 감지"** | 공개 API 부재. `docs/SCREEN_SHARE_DETECTION.md`. ★ **1절과 혼동 금지** — 우리가 감지하는 것은 **화면 공유가 아니라 전체화면 기하**다 |
| **성능 수치 일체** | ★ `tft-competitive` 경고 추가: 우리 GPU 11.1%는 **`dwm.exe` 귀속**, Desktop Mate 50%는 **앱 자체 귀속**. **다른 자로 잰 값이다** — 동일 기준 재측정 전 **비교 소재 금지**. 절대값 인용도 여전히 금지 |
| **"한국어 / English"를 지금 페이지에** | 영어화 **미착지**. `docs/localization/PLAN_1.0.md`상 **L0 게이트만** 구현. 문자열 테이블·로케일 전환 **0** |
| **"세계 최초" / "유일한"** | 조사가 보장하는 것은 "찾지 못했다"까지 |
| **"창을 발판으로 딛는다"를 차별점으로** | 카테고리 입장료 (→ `DIFFERENTIATION.md`) |
| **DLC 로드맵 약속 / 모바일 / 이벤트 탭 / 격파 놀이** | 변동 없음 |
| **장비·커스터마이즈 컷** | 27색 중 21색 대비 미달(D등급). 수정 라운드 뒤 |

---

## 6. 플랫폼 영향

- **Windows 영향: 함께 검토함(문서만).** ★ 이 감사의 최대 산출이 Windows 쪽이다 —
  **현행 Windows 배포본(12:42)에 등급 1·숨김 단축키가 없다**(2절, 바이트 실측).
  따라서 1절 세 문장은 **Windows 재빌드 전까지 Windows 소재로 쓸 수 없다.**
  Windows 전용 사실 3건은 그대로 참이다: 제목 버퍼 1글자(`Win32WindowService.cs:487`),
  전체화면 정확일치 분기(`FullscreenSuspendPolicy.cs:131`), 작업표시줄 예외 고지(STORE 4-1).
- **macOS 영향: 없음(문서만).** macOS 빌드 19:07은 1절 코드를 **포함**한다(바이트 실측 `표면회수` 2건).
  프로덕션 `.cs` 수정 0건, `docs/marketing/` 외 쓰기 0건.
