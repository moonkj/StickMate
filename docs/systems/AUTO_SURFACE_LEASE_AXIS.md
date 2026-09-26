# 자동 표면은 사용자 임대로 풀리지 않는다 — 표면 억제 축 분리 판정

작성: `game-architect` · 2026-09-15 · 문서만 썼다(코드·테스트 무수정, 앱·Unity·러너 미실행)
기준: HEAD `eb4670d`. 작업 트리에는 E-4(`coder-ui`)가 진행 중이다. 그 라운드의 테스트 파일은 **작업 트리 판**으로 읽었고, 그렇게 표시했다.
발단: `persona-stress` ①과 `persona-newcomer` ②(메모 카드 깜빡임)가 따로 적발했다. 리더가 코드로 다시 확인한 뒤 배정했다.
경로 표기: `Assets/_Project/Scripts/` 아래 상대 경로.
정정(2026-09-15, 같은 날 2차 — `coder-ui` N-20 구현 보고 반영, HEAD `761f5cb` · 작업 트리): §6-2 거짓 1건 정정과 자백 · §6-5 인계 결과 신설 · §4-5 로그 문구 판정 · §4-6 리더 판정 기록. 옛 **판정** 문장(§6-2)은 지우지 않고 취소선으로 두었다. 단 **지시 문구 1줄**(§6-4 「게터로 읽는다」 → 「기존 게터 `TodoPostItWidget.IsClickBlockerEnabled`로 읽는다」)은 뜻이 같아 취소선 없이 제자리에서 갱신했다. 3차(`verify-change` 2차 조건부 반영): §6-2 「0건」의 범위를 코드로 한정 · §4-5 요구 1의 자연 만료 무로그 모순 수정.

---

## 0. 결론 먼저

| 질문 | 답 |
|---|---|
| 결함이 실재하는가 | **실재한다(코드 판독, 실기 0).** `ArePanelsSuppressed`(아래 P)는 s ∨ (r ∧ ¬g)이다. 등급 1에서 사용자 창이 임대(g)를 갱신하는 동안에는 P가 **거짓**이다. 그래서 P를 읽는 자동 표면 4곳이 억제에서 풀린다 |
| 무한정인가 | **정보창·설정창은 무한정이다**(무입력 자동 닫기 0건). 부채꼴은 무입력 6초 뒤 접히고, 팝오버는 무입력 180초 뒤 닫힌 다음 부채꼴이 6초 뒤 접혀 끝난다 |
| 고칠 곳 | **소비자 4곳**: `TodoPostItWidget.cs:414` · `TodoReminderDirector.cs:53` · `WindowCrashDirector.cs:167` · `SettingsWindow.cs:169`(자동 재오픈 예약). 사용자가 여는 표면 5곳은 **무변경** |
| 새 창구 | `Platform/UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(panelsSuppressed, userSummonGranted) => panelsSuppressed \|\| userSummonGranted`. (s,r,g)로 풀면 **s ∨ r ∨ g**다 |
| N-8 결속 | **깨지지 않는다.** 구현 파일은 전부 표시 변경 경로 목록 밖이고, `StickmanAgent.cs`(목록 B)는 건드리지 않는다 |
| 출시 관문 | **1.0 필수**(출시 후보 동결 조건). 공개 프리뷰 해제 조건은 아니다. 단 **E-1을 싣는 첫 공개 프리뷰에 함께 싣기를 권고**한다(§5) 〔★ 리더 채택 2026-09-15: product-strategy 제안으로 **E-1이 든 공개 프리뷰에는 동결 해제 필수**로 강화 — `docs/strategy/ROADMAP.md` §60-9 N-20〕 |
| 적발 ③(설정창 자동 재오픈 편승) | **같은 축 문제다.** 같은 수정(`SettingsWindow.cs:169`)으로 닫히고, 별도 설계는 필요 없다(§7) |

**새 불변식(한 문장)**: **사용자 허가(임대)는 사용자가 부르지 않은 표면을 절대 드러내지 않는다.** 즉 `SuppressesUnsummonedSurfaces`는 모든 입력에서 `SuppressesPanels`를 포함하고, 허가 비트에 대해 단조 증가다.

---

## 1. 기전 — 무엇이 어떻게 새는가

기호는 `docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md` §16-2b를 따른다.
- s = `HidesScreenSurfaces`(`Core/StickmanAgent.cs:356`)
- r = `_fullscreenPanelRetreat`(private, `:105`). 등급 1 이상에서 참이고 **등급 2에서도 참**이다(`Platform/FullscreenSuspendPolicy.cs:272`)
- g = `IsUserSummonGrantActive`(`:370`)
- P = `ArePanelsSuppressed`(`:241-242`) = `SuppressesPanels(s, r, g)` = s ∨ (r ∧ ¬g)(`Platform/UserSurfaceSummonPolicy.cs:116-118`)

| 행 | s r g | 상태 | P(현행) | 자동 표면(현행) |
|---|---|---|:-:|---|
| FTF | F T F | 등급 1 · 무허가 | T | 억제 |
| **FTT** | F **T T** | **등급 1 · 사용자 창이 열려 임대 갱신 중** | **F** | ★ **풀림** — 포스트잇 카드와 차단막이 되살아나고, 리마인더가 발동하고, 크랙 오버레이가 남고, 설정창 자동 재오픈이 실행된다 |

**임대 갱신자**(모두 매 프레임 갱신한다. 누가 열었는지는 묻지 않는다):
- 부채꼴: `GearRadialMenuWidget.cs:1290`(`LateUpdate`)
- 부채꼴 또는 정보창: `InfoGearIconWidget.cs:855`
- 설정창: `SettingsWindow.cs:760`

**수명 상한**:
- 부채꼴: `AutoCollapseIdleSeconds = 6`(`:324`). 팝오버가 떠 있으면 접힘이 멈춘다(`:1366`)
- 팝오버: `DefaultIdleAutoCloseSeconds = 180`(`PopoverPanel.cs:188`)
- 정보창·설정창: 없음. `Idle…Close|AutoClose` 코드 검색 결과 0건이었다. 같은 검색이 `PopoverPanel.cs`에서는 2건을 잡아 프로브가 살아 있음을 확인했다

**E-1이 바꾼 것**: E-1은 이 구조를 만들지 않았다(09-03부터 있었다). **도달 빈도**를 바꿨다.
- E-1 전 도달 경로: ⌃⌥⌘P, Windows 트레이 [설정 열기], 사용자 숨김 중 대기 톱니
- E-1 후 도달 경로: 캐릭터 우클릭 → 부채꼴(`AppControlDirector.cs:894`(HEAD 줄 — 작업 트리는 E-4 편집으로 밀려 있다) 허가) — 주 입구다. 전체화면 앱 위에서 부채꼴을 여는 순간마다 **최소 수 초** 포스트잇이 뜬다(민지 ②와 같은 사건)

**리더 인용 줄 대조**: 전부 참이다. 하나만 보정한다 — `SettingsWindow.cs:131`은 유예 상수 문서의 끝 줄이고, 상수 자체는 `:132`다.

---

## 2. 소비자 전수 분류

### 2-1. 프로브 기록 (양성·음성 대조 포함)

- 명령: `git grep -n "ArePanelsSuppressed" -- Assets/_Project/Scripts`에서 `/Tests/`를 빼고, 주석 줄(`^…:[0-9]+:\s*//`)을 뺐다 → **13줄**.
  - 음성 대조: 존재하지 않는 이름 `…XYZ_NOPE` → 0.
  - 주석 필터의 양성 대조: `TodoReminderDirector.cs`의 코드 줄 `_player.IsSuspended` → 1.
  - HEAD(`git grep … HEAD`)와 작업 트리 모두 13줄로 같다.
  - `Assets/_Project/Scripts` 밖 독자: 0(같은 명령의 Scripts 쪽 대조는 28파일).
- 13줄의 내역: 정의 1(`StickmanAgent.cs:241`) + **문자열 리터럴 안** 2(`StickmanAgent.cs:1885`, `SettingsWindow.cs:1424`) + **코드 독자 10**.
- 「매 프레임 7개 표면」 확정: 아래 #1·#2·#3·#4·#7·#8·#9. 여기에 #6(무장 중일 때만 매 프레임), #5(사건), #10(폴링)이 더해져 **독자는 10**이다.
- ★ **자백 — 죽은 프로브 1건**: 팝오버 하위 클래스를 `git grep -E "class\s+PopoverPanel\b"`로 셌는데 0이 나왔다. git grep의 ERE는 `\s`·`\b`를 지원하지 않는다. 같은 형태의 양성 대조도 0이어서 죽은 프로브임을 알았고, `-F ": PopoverPanel"`로 다시 셌다 → 3개(`ActionCommandPopover`·`FocusSessionPopover`·`TodoBoardPopover`).

### 2-2. 표

| # | 파일:줄 (`eb4670d`) | 무엇을 판정하나 | 누가 여는가·발동하는가 | 분류 | 판정 후 창구 |
|---|---|---|---|---|---|
| 1 | `Interaction/CharacterInfoWindow.cs:1034` | 정보창과 차단막을 닫는다 | 사용자(부채꼴 [캐릭터] · ⌃⌥⌘I · `Open` 발급 `:945`). 시트 복귀 `ReopenFromSheet`(`:937`)는 사용자 흐름의 연속이다 | **(가)** | 불변(P) |
| 2 | `Interaction/GearRadialMenuWidget.cs:1263` | 부채꼴과 팝오버를 걷는다 | 사용자만: 캐릭터 우클릭 `AppControlDirector.cs:900`(HEAD), 톱니 `InfoGearIconWidget.cs:1501`·`:1775`. 재앵커 `:1312`는 앞 호출의 연장이다 | **(가)** | 불변(P) |
| 3 | `Interaction/PopoverPanel.cs:449` | 팝오버 3종을 걷는다 | 부채꼴 버튼만: `GearRadialMenuWidget.cs:1113`·`:1125`·`:1140`. 다른 호출부는 0이다 | **(가)** | 불변(P) |
| 4 | `Interaction/SettingsWindow.cs:767` | 설정창과 차단막을 닫는다 | 사용자(단축키 · 칩 · 트레이, 발급 `:648`). 자동 재오픈으로 열린 경우는 #6에서 막는다 | **(가)** | 불변(P) |
| 5 | `Interaction/SettingsWindow.cs:708` | 시트 복귀(정보창 되돌림) 가드 | **사용자가 설정창을 닫은 결과**다. 회수 경로 `:771`에서는 복귀 플래그를 먼저 끈다 | **(가)** 사용자 흐름의 연속 | 불변(P) — 아래 경계 사례 |
| 6 | `Interaction/SettingsWindow.cs:169` | 자동 재오픈 예약을 실행할지 | **자동**(§16-2b B5에 이름으로 올라 있다) | **(나)** | **새 창구** |
| 7 | `Interaction/TodoPostItWidget.cs:414` | 카드와 클릭 차단막 | **상시 HUD다.** 이 파일에서 허가를 내는 경로는 0이다 | **(나)** | **새 창구** |
| 8 | `Interaction/TodoReminderDirector.cs:53` | 리마인더 자동 발동 | **자동**(밀어내기) | **(나)** | **새 창구**(`\|\| IsSuspended` 유지) |
| 9 | `Interaction/WindowCrashDirector.cs:167` | 크랙 오버레이 취소 | **자동** | **(나)** | **새 창구**(`\|\| IsSuspended` 유지) |
| 10 | `Core/StickmanAgent.cs:1698` | `[표면회수]` 등급 1 전이 로그 | 진단(폴링 `:1608`, 숨김 토글 `:493`) | (라) 진단 | 불변 — **N-8 목록 파일**. 계수 위험은 §4-5 |

### 2-3. (다) 「E-1이 불변으로 둔 닫기 소비자」는 독립 분류가 아니다

(다)는 #1~#9 전체다. 성격에 따라 (가) #1~#5와 (나) #6~#9로 갈린다.

그 계약(§16-2b 경계 #3, HEAD F18e, 작업 트리 F18e·F18f)이 지키는 것은 두 가지다.
- (ㄱ) 등급 1 **진입 순간 회수**(B2)
- (ㄴ) **열기 판정**(`IsUserSummonBlocked`)을 읽지 않는 것

새 창구는 모든 행에서 P를 포함한다(§3-2). 그래서 (나)로 옮겨도 (ㄱ)은 **구조적으로** 유지되고, 열기 판정도 읽지 않으므로 (ㄴ)도 유지된다.
⇒ 문구만 고치면 된다. §16-2b 경계 #3의 **「하나도 바꾸지 않는다」**를 다음으로 바꿀 것을 제안한다: **「열기 판정으로 바꾸지 않는다. (나) 자동 표면은 억제를 넓히는 방향으로만 `SuppressesUnsummonedSurfaces`로 옮긴다.」** 문서 소유자는 `ux-designer`다.

### 2-4. 경계 사례 판정

**포스트잇 카드 → (나).** 상시 HUD이고 톱니 옆 패널이지만 사용자가 연 표면이 아니다. 근거 넷:
1. 등급 1에서 포스트잇을 **불러낼 사용자 경로가 구조적으로 없다.** 허가 발급 4곳(`AppControlDirector.cs:894`(HEAD 줄 — 작업 트리는 E-4 편집으로 밀려 있다), `CharacterInfoWindow.cs:945`, `InfoGearIconWidget.cs:1791`, `SettingsWindow.cs:648`)에 포스트잇은 없다. 따라서 등급 1에서 카드가 보이는 경우는 **남의 임대에 편승한 경우뿐**이다.
2. §16-2b B5가 이미 포스트잇을 자동 경로로 분류했다.
3. 등급 1에서 할 일을 보려는 사용자는 부채꼴 [오늘 할일] 팝오버(#3, 사용자 호출, 임대 대상)로 닿는다. **도달성 손실은 0이다.**
4. 「톱니 옆」은 **배치**(`SyncPanelInsetToGear`)일 뿐 호출 주체가 아니다. 톱니가 등급 2 창구에 남은 이유는 「안전판이 자기 자신을 지운다」(`InfoGearIconWidget.cs:806-814`)이고, 포스트잇은 안전판이 아니다.

**팝오버 → (가).** 여는 곳이 부채꼴 버튼 3곳뿐이다. 떠 있는 동안 부채꼴이 접힘을 멈추고 임대를 갱신한다. 180초 + 6초로 수명이 유한하다.

**시트 복귀 #5 → (가).** 설정창 [✕]로 닫혔을 때, 그 직전에 사용자가 보던 정보창을 되돌린다. E-2가 이 경우를 **의도해** 남겼다(`SettingsWindow.cs:717-720` *「이미 살아 있는 임대는 톱니 위젯의 갱신으로 이어진다」*). 새 창구(등급 1에서 항상 참)로 옮기면 **등급 1에서 M8 시트 복귀가 사라진다** — E-2 회귀다.

**설정창 한 파일에 두 분류**: `:767`(#4)는 (가), `:169`(#6)는 (나)다. 그래서 테스트는 파일 단위가 아니라 **메서드 단위**로 재야 한다(§6-2).

---

## 3. 축 설계안

### 3-1. 정의 (규칙 본문은 `Platform/` 중립 위치, 플랫폼 분기 0)

```csharp
// Platform/UserSurfaceSummonPolicy.cs — SuppressesPanels · BlocksUserSummon 옆
public static bool SuppressesUnsummonedSurfaces(bool panelsSuppressed, bool userSummonGranted)
    => panelsSuppressed || userSummonGranted;
```

- 인자는 **같은 프레임의** `StickmanAgent.ArePanelsSuppressed`와 `StickmanAgent.IsUserSummonGrantActive`다. 둘 다 `Time.unscaledTime`을 읽고 한 프레임 안에서 값이 같으므로, P 안의 g와 두 번째 인자 g는 같은 비트다.
- 소비자 호출형은 다음과 같다. **이 형태만 허용한다** — 이유는 §4-2다.

```csharp
if (_agent != null && UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(
        _agent.ArePanelsSuppressed, _agent.IsUserSummonGrantActive))
// 리마인더 · 크랙: 여기에 `|| _player.IsSuspended`를 그대로 붙인다(캐릭터 축, 원칙 1).
```

### 3-2. 진리표 — 기대값 상수 (인덱스 = s·4 + r·2 + g)

| (s,r,g) | FFF | FFT | FTF | **FTT** | TFF | TFT | TTF | TTT |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `SuppressesPanels` (P) | F | F | T | **F** | T | T | T | T |
| `BlocksUserSummon` (= s) | F | F | F | F | T | T | T | T |
| **`SuppressesUnsummonedSurfaces`** | F | **T** | T | **T** | T | T | T | T |

- **포함**: P가 참인 행 5개 모두에서 새 값도 참이다(8/8 행 확인). ⇒ B2 진입 회수와 「등급 2 포함」 불변식(`UserSurfaceSummonPolicy.cs:110-114`)이 그대로 유지된다.
- **단조**: 같은 (s,r)에서 g가 F→T로 가도 T→F로 떨어지는 쌍이 0이다(4쌍 확인).
- **P와 갈리는 칸은 정확히 2개**: FTT(결함 칸)와 FFT(이력 칸).

### 3-3. 왜 「순수 임대 없는 창구」(s ∨ r)가 아니라 s ∨ r ∨ g인가

리더 브리프의 원형은 `SuppressesPanels(s, r, false)` = s ∨ r이다. **두 가지 이유로 채택하지 않는다.**

**(1) N-8 목록 파일을 건드리지 않고는 s ∨ r을 만들 수 없다.**
- r은 private이다(`StickmanAgent.cs:105`).
- 공개 게터로 얻을 수 있는 것은 s · g · P · `IsUserSummonBlocked`(= s, `BlocksUserSummon` 전개 결과) · `IsForeignFullscreenAppPresent`(춤 축)뿐이다.
- g = T일 때 P = s이므로, {s, g, P}에서 r을 복원할 수 없다.
- 춤 축은 오늘 값이 r과 같지만 §3-5 (나)의 이유로 쓸 수 없다.

**(2) FFT 칸(이력 칸)은 결함이 아니라 이득이다.**
- FFT는 「등급 1에서 사용자가 연 창이 아직 떠 있는데 전체화면 앱은 사라진」 상태다. 이때 자동 표면은 **사용자 창이 닫힐 때까지(+ 0.5초) 기다린다.**
  - (가) 적발 ③의 자동 재오픈이 등급 1→0 순간 `CloseOverlappingSurfaces`로 **사용자의 정보창을 빼앗지 않는다**(§7). 순수 s ∨ r이면 빼앗는다.
  - (나) Windows 가설 H1(우리 창이 전경이 되면 `Win32WindowService.cs:2054`·`:2063`이 등급을 None으로 판정해 r이 떨어진다)이 참이어도, 사용자가 우리 창을 만지는 동안에는 자동 표면이 새지 않는다. 순수 s ∨ r이면 샌다(**조건부 이득, H1 판독은 `dev-platform` 대기**).
- 대가: 그 사이 포스트잇 복귀·리마인더·크랙이 늦어진다. 억제를 **더하는** 방향이라 원칙 2 쪽으로 안전하다.

### 3-4. 춤 축 선례와의 일관성

| | 춤 축(09-06) | 이번 축 |
|---|---|---|
| 지키는 불변식 | 허가는 자동 발동을 드러내지 않는다 | **같다** |
| 임대를 대하는 방식 | 읽지 않는다 | **억제 쪽으로만** 읽는다 |
| 규칙 위치 | `ForeignFullscreenTierPolicy.SuppressesAutoDance`(`Platform/`) | `UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces`(`Platform/`) |
| 배선 위치 | 에이전트 필드(`StickmanAgent.cs:1594`) | **소비자 호출부** — 에이전트가 N-8 목록 B라서 |

선택(백로그, 권하지 않음): 나중에 E-3 증거 결속이 풀린 뒤 `StickmanAgent.cs`를 다른 이유로 여는 라운드가 있으면, `AreUnsummonedSurfacesSuppressed => UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(ArePanelsSuppressed, IsUserSummonGrantActive)`로 이름을 붙여도 된다. **같은 함수를 부르므로 규칙 본문은 두 벌이 되지 않는다.** 그 라운드만을 위해 파일을 열 가치는 없다.

### 3-5. 기각한 안

| 안 | 기각 이유 |
|---|---|
| (가) 자동 표면이 `IsUserSummonBlocked`를 읽는다 | 값이 s라서 **등급 1 억제가 통째로 사라진다**(B2·B5 회귀). 작업 트리 F18e가 이미 금지한다 |
| (나) `IsForeignFullscreenAppPresent`(춤 축) 재사용 | ① 계약 위반: *「이 프로퍼티를 표면 회수나 캐릭터 숨김에 쓰지 마라」*(`StickmanAgent.cs:282`). ② **테스트 주입이 닿지 않는다**: PlayMode는 등급 1을 `_fullscreenPanelRetreat` 리플렉션으로만 흉내 낸다(`PanelsOnlyTierMouseEntryTests` `SetPanelRetreat`). 춤 필드는 그 주입을 따라가지 않아 스위트가 **조용히 다른 세계를 잰다** — `StickmanAgent.cs:216-222`·`:345-352`가 경고한 병과 같다. ③ 두 축이 갈라지는 날 표면이 춤 규칙에 끌려간다 |
| (다) 에이전트에 s ∨ r 프로퍼티 신설 | 정확하지만 N-8 목록 B 파일이라 `eb4670d` E-3 증거 결속이 끊긴다. 그리고 §3-3 (2)의 이득을 잃는다 |
| (라) 정보창·설정창에 무입력 자동 닫기 추가 | 무한정을 유한정으로 줄일 뿐 **부활 자체는 남는다.** 발표 중 설정을 읽는 사용자의 창을 우리가 닫게 된다 |
| (마) 임대를 표면별 토큰으로 바꾸기 | 구조적으로는 옳은 방향이다. 하지만 에이전트(N-8)와 갱신자 3곳 계약을 바꿔야 하는데, 자동 표면에 대한 결과는 이 안과 같다. 뿌리(「갱신자가 자기가 사용자에게 불렸는지 묻지 않는다」)는 §7-3에서 이 안으로 **자동 경로 편승 0**을 보인다 |
| (바) 포스트잇만 고치기 | 리마인더·크랙·자동 재오픈이 남는다. 같은 뿌리를 네 번 나눠 고치게 된다 |

---

## 4. 되돌릴 수 없는 것 · 위험

### 4-1. 되돌릴 수 없는 결정: **없다**
세이브 스키마, 좌표계, 플러그인 계약, 설정 에셋 모두 무변경이다. 순수 판정 함수 1개와 호출부 4곳이 전부다. 유일하게 무거운 축은 N-8 결속인데, 이 안은 그것을 건드리지 않는다(§4-3).

### 4-2. E-1·E-2 진리표와 E-4(작업 트리) 테스트와의 충돌 — **없다 (호출형을 지킬 때)**

| 테스트 | 영향 | 근거 |
|---|---|---|
| `RightClickFanGateTests` 정책 진리표 · F18 · F18b · F18c · F18d | 없음 | `SuppressesPanels`·`BlocksUserSummon`·게이트 호출부가 무변경 |
| HEAD F18e(파일 5개 명부) | 없음 | 그 5개 중 옮기는 파일은 `TodoPostItWidget.cs`·`SettingsWindow.cs`. 둘 다 새 호출 인자로 `.ArePanelsSuppressed`를 계속 포함하고, `.IsUserSummonBlocked`는 읽지 않는다 |
| **작업 트리 F18e**(전수 스캔: 열기 판정 독자 = 게이트 파일) | 없음 | 새 호출형은 열기 판정을 읽지 않는다 |
| **작업 트리 F18f**(`CloserGuards`: 조건에 `.HidesScreenSurfaces`/`.IsUserSummonBlocked`, 본문에 `Close`·`Hide`·`CancelOverlay`·`EnterFullscreenHiding` → 위반) | 없음 | 포스트잇·크랙 가드 조건은 두 토큰 모두 없고 `.ArePanelsSuppressed`는 포함한다 → 「닫기 가드 수」 존재 대조에 계속 잡힌다. ★ **구현자가 s ∨ r을 `HidesScreenSurfaces`로 조립하면 F18f가 빨개진다** — 그것은 **옳은 빨강**이다(§3-5 (나)·(다)) |
| `PanelsOnlyTierMouseEntryTests` B3_B4 · B2 · B5 · B7 | 없음 | 포스트잇·리마인더를 단언하지 않는다. B7은 s = T라 새 창구도 T다 |
| **작업 트리 V1**(`:810-852`, 무허가 등급 1 미발동 → 해제하면 발동) | 없음 | 무허가 상태라 g = F. 해제 뒤 새 창구 = F → 발동한다 |
| 작업 트리 V2 · V3 | 없음 | 설정창·정보창 경로 무변경 |
| `SuspendClickBlockerAuditTests` `PollsPanelChannel` | 초록 유지(**약해진 이유로**) | 부분 문자열 `"ArePanelsSuppressed"`(기존 문자열 니들)가 새 호출 인자에 남는다. 보강은 선택이다: `nameof(UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces)`를 인정 채널에 추가 |
| `SuspendedOverlayLeakAuditTests` · `UiInteractionFramePacingHoldTests` · `ManualHideAxisSeparationAuditTests` · `AudioReactiveDanceGateAuditTests` | 없음 | 면제 사유 문구뿐이거나 대상 파일(부채꼴·톱니·에이전트·춤 게이트)이 무변경 |

★ **순서 위험**: E-4가 이 테스트 파일 두 개를 **지금 작업 트리에서 편집 중**이다. 이 수정을 E-4 커밋 전에 트리에 얹으면, 「컴파일 불가 트리에서 검증」이나 「남의 작업 중 파일을 커밋」 사고(2026-09-02에 2건)를 그대로 밟는다. ⇒ §6-1 순서.

### 4-3. N-8 표시 변경 경로 목록과의 결속 — **깨지지 않는다**

- 목록 블록(`<!-- DCP-LIST BEGIN/END -->`)을 awk로 뽑아 대조했다.
  - 양성 대조: `Core/StickmanAgent.cs` → **B**로 잡혔다.
  - 구현 파일 `Platform/UserSurfaceSummonPolicy.cs` · `Interaction/TodoPostItWidget.cs` · `Interaction/TodoReminderDirector.cs` · `Interaction/WindowCrashDirector.cs` · `Interaction/SettingsWindow.cs` → **목록에도 X 행에도 없다.**
- 썩음 검사 (다)의 소비자 식별자(`DisplayChangeHold*` · `IsPreservationFrozen` 등)도 새로 들어가지 않는다. `NEW-CALLEE`는 두 Enforcer 함수 본문만 보므로 무관하다.
- ⇒ `eb4670d` E-3 증거 빌드는 이 수정이 착지한 뒤에도 N-8 기준으로 유효하다. E-4와 같은 입장이다(ROADMAP N-17 「N-8 영향」 칸).
- ★ **주의**: `StickmanAgent.cs:1698` 로그(#10)나 에이전트 쪽 이름 붙이기(§3-4)를 같은 라운드에 끼우면 결속이 끊긴다. **이 라운드에서 `StickmanAgent.cs` 수정은 0줄이어야 한다.**

### 4-4. 구조적 잔여 — 갱신자는 여전히 「누가 열었나」를 묻지 않는다
- `SettingsWindow.cs:760`은 자동 재오픈으로 열린 창에서도 갱신한다.
- `InfoGearIconWidget.cs:855`는 `ReopenFromSheet`로 열린 정보창에서도 갱신한다.
- 이 안 뒤에 편승이 0이 되는 논증과, 새 비사용자 진입점이 생기면 다시 뚫린다는 조건은 §7-3에 있다.

### 4-5. ★ 로그 계수 위험 — `[표면회수]` 줄을 등급 1 재진입 빈도로 세지 마라

`StickmanAgent.cs:1696-1709`는 **P의 변화**에 로그를 단다(등급의 변화가 아니다).
- 등급 1에서 사용자가 창을 열면 다음 폴링에 **`[표면회수] 등급 1 해제 — …`**가 찍힌다. 등급 1은 계속 켜져 있는데도 그렇다.
- 창을 닫아 임대가 **자연 만료되면** 다음 폴링에 **`[표면회수] 등급 1 — 전체화면 앱이 떴지만 …`**이 찍힌다. 자연 만료 경로는 `ExpireUserSummonGrant`를 거치지 않아서 만료 줄이 없다.
- ⇒ 사용자 창 세션 하나가 **가짜 해제/진입 한 쌍**을 남긴다.

리더 계획(소은 ② 빈도 = 사용자 E-3 로그의 `[표면회수]` 줄 셈)은 이대로 세면 **과대 계수**가 된다.
- 권고: 빈도의 원천은 `dev-platform`이 확정할 **플랫폼 판정 로그 문자열**로 한다.
- 또는 인접한 `[표면회수] 등급 1 중 사용자가 표면을 직접 불렀습니다` 줄로 한 쌍씩 걸러 낸다.

수정 뒤에도 이 로그는 P를 키로 쓴다(N-8 파일이라 손대지 않음). 그래서 해제 문구 *「톱니와 포스트잇처럼 상시 HUD인 표면만 스스로 돌아옵니다」*는 임대 칸에서 **거짓**이 된다. E-3 결과를 받은 뒤 `design-narrative` 로그 문안 묶음에 합류시킨다.

**★ 판정 추가 (2026-09-15, N-20 착지 뒤 — 코드 판독, 실기 0)** — N-20은 `StickmanAgent.cs`를 건드리지 않는다(작업 트리 diff 0). 바뀌는 것은 자동 표면의 동작이다. 그래서 같은 로그 문장의 참·거짓이 달라진다.

| 줄 | N-20 전 | N-20 뒤 | 판정 |
|---|---|---|---|
| `StickmanAgent.cs:1707-1709` 「등급 1 해제 — … 톱니와 포스트잇처럼 상시 HUD인 표면만 스스로 돌아옵니다」 | 임대 칸(r∧g)에서 「해제」가 거짓이었다(등급 1은 계속 — 위 가짜 쌍). 「포스트잇이 돌아온다」는 **결함 그대로의 사실**이었다 | 「해제」 거짓은 그대로다. ★ **「포스트잇이 스스로 돌아온다」가 새로 거짓이 된다** — 임대 칸에서는 U = 참이라 카드·차단막이 숨은 채다. FFT 칸(등급 1이 끝났는데 사용자 창이 열림)에서는 P가 이미 거짓이라 **실제 해제 순간에 줄이 없다.** 카드는 창을 모두 닫고 허가가 끝난 뒤에야 돌아온다(§6-5 R3) | **부정확해진다** — 신규 1(포스트잇 복귀) · 기존 2(가짜 해제 · FFT 무로그) |
| `:1704-1706` 「등급 1 — … 창·패널·팝오버·부채꼴과 그 클릭 차단막만 걷습니다」 | 임대 자연 만료 때 가짜 재진입 줄 | 같다. 「걷습니다」는 만료로 걷히는 사용자 창에 대해 참이다 | 기존 부정확 유지(N-20 무관) |
| `:1885` 「[숨김] … 각 표면이 ArePanelsSuppressed를 폴링해 같은 프레임에 스스로 걷고」 | 참 | 이 가지는 `HidesScreenSurfaces`(s) = 참일 때만 찍힌다. 그러면 P = 참이고 U = P ∨ g = 참이라 **결과는 그대로 참이다.** 자동 표면 4곳이 P를 `SuppressesUnsummonedSurfaces(P, g)`의 인자로 읽게 된 **기전 서술만 낡는다** | **결과상 참 · 기전 문구만 낡음(낮음)** |

- 조치: 두 줄 모두 N-8 목록 B 파일이라 **지금 고치지 않는다.** E-3 증거 결속이 해제된 뒤 **「21 로그 지점」 라운드에 합류**시킨다(리더 판정).
- 그 라운드에 넘길 요구(문안은 `design-narrative` 몫):
  1. 이 줄의 키를 P가 아니라 **등급(r) 전이**로 바꾼다. 허가 **부여**는 기존 `:401` 줄에 맡긴다. 허가 **만료**는 기존 `:430`(명시 만료 `ExpireUserSummonGrant`)만으로는 **부족하다** — 위 둘째 항목대로 자연 만료는 그 함수를 거치지 않는다. 허가는 시각 비교로만 끝나기 때문에(`IsUserSummonGrantActive => Time.unscaledTime < _userSummonLeaseUntil`, `StickmanAgent.cs:370`) 자연 만료에는 줄이 없다. ⇒ **허가가 참 → 거짓으로 바뀌는 전이를 폴링 한 곳에서 감지해, 사유(명시 만료 / 자연 만료)와 함께 1줄**을 남기도록 요구한다(명시 만료가 이미 `:430`을 찍었으면 중복하지 않는다 — 자리와 방법은 구현 라운드 몫). → 가짜 해제/진입 쌍 · FFT 칸 무로그 · **자연 만료 무로그**가 함께 없어진다. 〔3차 정정 — 2차 문안 「허가 부여·만료는 기존 `:401`·`:430` 줄에 맡긴다」는 같은 절의 사실과 모순이었다(`verify-change` 2차)〕
  2. 포스트잇 복귀 조건을 「사용자가 연 창이 모두 닫히고 허가가 끝난 뒤」로 적는다.
  3. `:1885`는 「자동 표면은 사용자 허가와 무관하게 걷힌다」 한 구절이면 된다.
- 위의 계수 권고(빈도를 `[표면회수]` 줄로 세지 말 것)는 N-20 뒤에도 그대로다.

### 4-6. 별건 (한 줄, 판정 범위 밖)
`WindowCrashDirector.TickAutoTrigger`(`:192-218`)에는 등급 가드가 없다. 등급 1에서도 휘두르기 상태로 전이하고, 오버레이만 다음 틱에 취소된다(현행, 이 수정과 무관한 기존 동작). 「결과 없는 휘두르기」로 읽히는지는 `design-motion`/`persona-immersion`이 판단할 일이다. 발동 가드에 같은 창구를 쓰는 것은 같은 파일이라 비용이 적다 — 넣을지는 리더가 판단한다.
〔★ **리더 판정 2026-09-15: 크랙 발동 가드는 N-20 묶음에 넣지 않는다.** 캐릭터 행동 변경(원칙 1 「결과 없는 휘두르기」)이라 테스트가 끝난 묶음에 섞지 않는다. 커밋 뒤 페르소나 3인 질문으로 올리고, `design-motion` 판단 백로그로 둔다.〕

---

## 5. 출시 관문 성격 — **1.0 필수** (출시 후보 동결 조건, 공개 프리뷰 해제 조건 아님)

| 축 | 판단 |
|---|---|
| 원칙 2 위반의 실체 | 사용자가 부르지 않은 표면이 남의 전체화면 앱(발표·회의) 위에 뜬다. 그중 포스트잇 **차단막이 그 사각형의 클릭을 먹는다**(`TodoPostItWidget.cs:403`이 스스로 적었다). 클릭을 먹는 것은 비침해의 핵심이라 **강도 중** |
| 조건 | 등급 1 체류 **그리고** 사용자가 연 창이 살아 있는 동안. 부채꼴·팝오버는 유한하고(6초 / 180초 + 6초), **정보창·설정창을 열어 둔 채 잊으면 무한정**이다 |
| 빈도 | E-1 뒤 주 입구가 이 경로를 밟는다. 부채꼴을 열 때마다 짧은 깜빡임이 나는 것은 **흔하고**, 창을 열어 둔 채 잊는 것은 **드물지만 가능하다**(추정, 실기 0) |
| 파괴성 | 없다. 창을 닫으면 0.5초 안에 회수되고, 데이터·시스템 영향 0 → **차단은 과하다**(N-17과 같은 급) |
| N-15와의 관계 | N-15 해제 조건(등급 1 입구 도달성)과는 **독립**이라 N-15 해제를 막지 않는다. 하지만 **N-15 수정(E-1)이 노출을 키웠다** → N-15의 꼬리로 같은 표에 새 행을 둔다 |
| N-17과의 관계 | 같은 분류(1.0 필수 · 프리뷰 해제 조건 아님) · 같은 N-8 입장(목록 파일 무수정) |
| 프리뷰 권고 | E-1이 든 첫 공개 프리뷰에 **함께 싣기를 권고**한다. 싣지 않으면 그 프리뷰 사용자는 입구 복원과 **새로 흔해진 표면 누수**를 같이 받는다. 수정 규모는 함수 1개와 호출부 4곳이다. 차단으로 올릴지는 리더와 `product-strategy`가 판단한다 〔★ 판단 완료 2026-09-15: 「E-1이 든 빌드 한정 동결 해제 필수」 리더 채택〕 |

**ROADMAP §60-9 제안 행** (현재 마지막 번호 N-19 → **N-20**, 소유는 리더/`product-strategy`):
> **N-20** | ★ Major — 등급 1에서 사용자가 창을 연 동안 자동 표면(포스트잇 카드·차단막 / 리마인더 / 크랙 / 설정창 자동 재오픈)이 억제에서 풀린다. `ArePanelsSuppressed`가 사용자 임대를 인자로 받기 때문이다. E-1 이후 주 입구가 이 경로를 밟는다(N-15의 꼬리) | 1.0 필수 · 프리뷰 해제 조건 아님(E-1이 든 첫 프리뷰 동반 권고 → 〔리더 채택 2026-09-15: E-1 든 프리뷰 필수로 강화〕) | `docs/systems/AUTO_SURFACE_LEASE_AXIS.md` | 출시 후보 동결. **N-8 영향: 없음** — `StickmanAgent.cs` 무수정, 소비자 4파일과 `UserSurfaceSummonPolicy.cs` 모두 목록 밖

**해제 조건 제안**:
1. §6-1 착지.
2. EditMode T-A·T-B GREEN.
3. PlayMode R0~R4 GREEN — R1은 **수정 전 빨강 박제**를 같이 낸다.
4. 변이 M-a~M-d가 각각 어딘가에서 빨갛다.
5. QG-1 기준(출시 후보 커밋)으로 센다. 실기는 조건에 넣지 않는다 — 판정이 입력 게이트이고 코드가 공통이다.

---

## 6. 구현 · 시험 인계

### 6-1. 순서와 근거
1. **E-4(`coder-ui`) 커밋을 먼저 기다린다.** E-4가 `RightClickFanGateTests.cs`(F18e/F18f)와 `PanelsOnlyTierMouseEntryTests.cs`(V1)를 편집 중이고, 이 수정의 무충돌 논증(§4-2)은 **그 두 스캐너와 합친 트리에서 한 번 실측**돼야 참이 된다. 먼저 넣으면 두 라운드가 서로 다른 트리에서 초록을 내고, 조합은 아무도 재지 않는다.
2. 구현(`coder-ui`) → EditMode·PlayMode 신규(`test-engineer` 설계, 러너 줄) → `verify-change` → 리더 커밋.
3. 문서(`ux-designer` §16-2b 문구)는 구현과 병렬로 한다(파일이 겹치지 않는다).

### 6-2. `coder-ui` 작업 파일 (겹침 방지 명부)

**수정한다**:
- `Platform/UserSurfaceSummonPolicy.cs` — 함수 1개와 문서(§0 불변식, §3-2 표, 「FFT 칸은 의도」)
- `Interaction/TodoPostItWidget.cs` — `:414` 가드. 그리고 ~~**테스트 게터 `IsClickBlockerEnabledForTests` 추가**(차단막 상태를 읽을 공개 창구가 지금 없다.~~ FPRT의 `PostItBlockerName` 문자열 상수 방식은 따르지 않는다)
  - 〔★ **정정 2026-09-15 — 취소선 문장은 거짓이었다.**
    - `TodoPostItWidget.IsClickBlockerEnabled`(`:308`, `_clickThroughBlocker != null && _clickThroughBlocker.enabled`)가 **`2051739`(2026-09-02)부터** 있다.
    - 근거: 파일 경로로 한정한 `git log -S "IsClickBlockerEnabled" -- Interaction/TodoPostItWidget.cs`의 첫 커밋이 `2051739`이다. 그 커밋 diff에 `+ public bool IsClickBlockerEnabled …` 한 줄이 있고, 부모 커밋의 같은 파일에는 0건이다(음성 대조).
    - HEAD 테스트가 이미 읽는다 — **3파일 7줄**, 변수의 형 선언으로 확인: `FullscreenSuspendUiHidingTests.cs:286·317·350·377` · `TodoPostItExpansionTests.cs:170·283` · `TodoBoardDateNavigationTests.cs:384`. 리더가 전달한 「4곳」과는 세는 단위가 다르다.
    - N-20 구현은 새 게터를 만들지 않고 이 게터를 썼다(코드(`Assets/`)에서 `IsClickBlockerEnabledForTests` 0건 — 이름 자체는 `Tasklist.md`와 이 문서에 남아 있다 · `AutoSurfaceLeaseAxisTests.cs`의 `_postIt.IsClickBlockerEnabled` 적중).
    - **뒷문장(FPRT 문자열 방식을 따르지 않는다)은 유효하다.**〕
  - 〔★ **자백** — 이 부재 주장에는 §2-1 같은 프로브 기록도, 양성 대조도 **0건**이었다(TEAM 거짓 통과 규칙 4 「모든 없음 판정에 양성 대조」 위반).
    - 추정 경위: FPRT가 차단막을 이름 문자열 `PostItBlockerName = "TodoPostItClickBlocker"`(`FullscreenPanelRetreatTests.cs:513`)로 찾는 것을 보고 「게터가 없어서 그렇게 했다」고 **확인 없이 추론했다.**
    - 필드명 `_clickThroughBlocker` 계열로만 찾았다면 이름이 다른 게터(`IsClickBlockerEnabled` — `Through`가 없다)를 놓친다.
    - 작성 라운드의 실제 검색 문자열은 기록이 없어 **재구성할 수 없다.**〕
- `Interaction/TodoReminderDirector.cs` — `:53`
- `Interaction/WindowCrashDirector.cs` — `:167`(+ §4-6 발동 가드는 리더 판단 〔→ 판정: N-20에 넣지 않음, §4-6〕)
- `Interaction/SettingsWindow.cs` — **`:169`만.** `:708`·`:767`은 무변경
- 신규 `Tests/EditMode/UnsummonedSurfaceAxisTests.cs`, `Tests/PlayMode/AutoSurfaceLeaseAxisTests.cs`(+ `.meta`)

**건드리지 않는다**:
- `Core/StickmanAgent.cs`(N-8 목록 B)
- `docs/verify/DISPLAY_CHANGE_PATH_FILES.md`
- E-4 소유 파일: `Interaction/AppControlDirector.cs` · `Interaction/StickmanClickHitbox.cs` · `Tests/EditMode/RightClickFanGateTests.cs` · `Tests/PlayMode/PanelsOnlyTierMouseEntryTests.cs` · `Tests/PlayMode/HiddenCharacterLeftClickTests.cs`
- 사용자 표면 3파일: `CharacterInfoWindow.cs` · `GearRadialMenuWidget.cs` · `PopoverPanel.cs`

**크로스 컴파일**: `xcheck` osx·win 모두 0 에러와 셀프테스트(공통 코드지만 CLAUDE.md 규칙).

### 6-3. `test-engineer` — EditMode `UnsummonedSurfaceAxisTests`

- **T-A 진리표**
  - 기대 상수 `{F,T,T,T,T,T,T,T}`(인덱스 s·4 + r·2 + g). 입력 = `SuppressesUnsummonedSurfaces(SuppressesPanels(s,r,g), g)`.
  - 같은 루프에서 세 가지를 함께 단언한다: ① P ⊆ 새 값(8행) ② g에 대해 단조(4쌍) ③ **P와 갈리는 칸이 정확히 2개**(음성 대조 — 0이면 편승으로 되돌아간 것, 3개 이상이면 억제 규칙이 흔들린 것).
  - 기대값은 **상수에서** 온다. 정책 함수로 만들지 않는다(TEAM 「생성기와 검사기가 같이 틀린다」).
- **T-B 소비자 분류 스캔**
  - 프로덕션 소스 전수에서 `"." + nameof(StickmanAgent.ArePanelsSuppressed)` 독자를 뽑는다(E-4 F18e의 「명부를 손으로 적지 않는다」를 계승).
  - 뽑은 독자를 **분류 표 {파일 → (가)/(나)/(라)}**와 **양방향**으로 대조한다: 미분류 독자가 있으면 빨강, 표에 있는데 읽지 않으면 빨강. 파일 실재는 `File.Exists`로 존재 단언한다.
  - (나) 파일 조건: `nameof(UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces)` 호출의 **인자 목록**에 `.ArePanelsSuppressed`와 `.IsUserSummonGrantActive`가 둘 다 있어야 한다(존재). 그 호출 **밖**에 `.ArePanelsSuppressed`를 조건으로 쓰는 `if`가 없어야 한다(부재 — 같은 테스트 안의 존재 단언과 짝).
  - `SettingsWindow.cs`는 **메서드 단위**로 잰다: `TickReopenAfterSuspend` 본문은 새 호출, `Update` 본문은 기존 P 가드. 두 메서드는 private이라 `nameof`가 안 된다 → 문자열 니들을 쓰되 **같은 테스트에서 본문 절단 성공(존재)을 먼저 단언**한다(CLAUDE.md 식별자 규칙).
- **T-C 스캐너 교정**: 알려진 표본(잡을 것 1 / 잡지 말 것 1)으로 먼저 자기검증한다(작업 트리 F18g 방식).

### 6-4. `test-engineer` — PlayMode `AutoSurfaceLeaseAxisTests` (재현안)

**세계 선언**: 대기 톱니 우회를 끈 프로덕션 기본(`PanelsOnlyTierMouseEntryTests`와 같은 SetUp/TearDown 자기검증)이다. 테스트 이름과 전제 단언에 적는다.

**준비**:
- `TodoListModel.ResetForTesting()` 뒤 `Add(text, softCap)` 1건.
- 설정 `todoReminderChance = 1`, `todoReminderCheckInterval = 1`(프로덕션 하한 `Mathf.Max(1f, …)`와 같은 값). TearDown에서 원복한다.
- 등급 1은 `_fullscreenPanelRetreat` 리플렉션으로 주입한다 — **`GetField != null` 존재 단언을 먼저** 한다.

**대기와 관측**:
- 대기는 전부 **벽시계**(`Time.realtimeSinceStartup`)다. 예산은 프로덕션 상수식으로 잡는다: `UserSurfaceSummonPolicy.LeaseSeconds`, 리마인더 간격, `SettleSeconds`.
- 관측은 **매 프레임 표본**(「한 번이라도 보였는가」)으로 한다(B7 방식).

| 케이스 | 절차 | 단언 |
|---|---|---|
| **R0 음성 대조(등급 0)** | 주입 없음 | `IsCardVisible` 참 · 차단막 켜짐 · 예산 안에 상태가 `StickmanStateId.TodoReminder`로 전이. ★ **빨가면 아래 전부 무효**(리마인더가 원래 못 뜨는 세계였다는 뜻) |
| **R1 결함 재현** — 수정 전 빨강 박제 | 등급 1 주입 → 정착 → 전제: 카드 거짓 · 허가 거짓 → `CharacterInfoWindow.Open("…")` → 전제: 허가 참 · 창 열림 → 벽시계 max(리마인더 간격 + 슬랙, `LeaseSeconds`×4) | 매 프레임: 카드 한 번도 안 보임 · 차단막 한 번도 안 켜짐 · `TodoReminder` 전이 0. **끝에서 허가가 여전히 참** — 이 단언이 없으면 「임대가 만료돼서 숨었다」와 구별되지 않는다 |
| R2 무허가 등급 1 유지 | 등급 1 주입만 | 카드 숨김 · 리마인더 0 (U ⊇ P 보존) |
| R3 이력 칸(FFT) | R1에 이어 등급 1 해제(r = 거짓) → 창 열린 채 정착 → `Close` | 창이 열린 동안 카드 숨김 유지 → 닫은 뒤 `LeaseSeconds` + 정착 안에 카드 복귀. (§3-3의 선택을 고정한다. 순수 s ∨ r로 바꾸면 빨강이 되는데, 그것은 **설계 변경**이라 의도된 빨강이다) |
| R4 적발 ③ 편승 | 등급 0에서 사용자가 설정창을 연다 → 축 1 + r 주입(등급 2) → 창 닫힘 · `IsReopenAfterSuspendArmed` 참 → 축 1만 해제(2→1, r 유지) → `CharacterInfoWindow.Open` → 1초 이상 | 설정창 **안 열림** · 예약 유지 · 정보창 유지. 대조: 정보창을 닫고 r 해제 → 기본 유예(20초) 안에 설정창 재오픈 |

**변이(각각 어딘가에서 빨강이어야 한다 — V1~V3 「잠갔다」 과장 재발 방지)**:
- M-a: (나) 한 곳을 P 단독으로 되돌림 → R1
- M-b: 정책 함수가 `panelsSuppressed`만 반환 → T-A · R1
- M-c: `|| userSummonGranted`를 `&& !userSummonGranted`로 → T-A
- M-d: `SettingsWindow.cs:169` 되돌림 → R4

**식별자 규칙 준수**:
- 프로퍼티·타입·열거값은 전부 `nameof`/형 참조로 쓴다.
- private 필드명(`_fullscreenPanelRetreat`)과 메서드명은 문자열이 불가피하다 → **같은 테스트에서 리플렉션 결과 null 아님을 존재 단언**한다.
- 포스트잇 차단막 이름 문자열은 쓰지 않는다 → 기존 게터 `TodoPostItWidget.IsClickBlockerEnabled`로 읽는다(§6-2 정정).

### 6-5. 인계 결과 — N-20 구현 반영 (작업 트리 · 커밋 전)

출처는 `coder-ui` 구현 보고(리더 전달)다. 러너 색은 **보고 인용**이다 — 이 문서 작성자는 러너를 돌리지 않았다. 「확인」 칸은 소스 텍스트로 대조한 것이다.

| 설계(§5·§6) | 구현 결과 | 확인 |
|---|---|---|
| R4 한 케이스 | **R4a(캐릭터 우클릭 허가) · R4b(사용자 정보창 `Open` 허가)** 두 케이스로 나뉘었다. §5 해제 조건 3의 「R0~R4」는 R0~R3 · R4a · R4b로 읽는다 | `AutoSurfaceLeaseAxisTests.cs:677`·`:704` 이름 |
| 「창을 모두 닫으면 카드 복귀」 | **R3(FFT 칸)**에서 잰다. 등급 1이 이어지는 동안(R1 임대 칸)에는 복귀하지 **않아야** 하므로, 복귀 관측은 등급 1이 끝난 칸에서만 성립한다 | `:545` R3 이름 |
| 변이 M-a~M-d | 여기에 **M-e · M-e2**(자동 표면을 옛 창구 `return;` 가드로 되돌림)가 더해졌다. 결과: **T-B만 빨강, E-4의 F18e/F18f는 초록** → **E-4 스캐너 사각지대가 실재했다.** §5 해제 조건 4의 변이 목록에 M-e · M-e2가 더해진다 | T-C 교정 표본이 그 형태를 명시한다(`UnsummonedSurfaceAxisTests.cs:619-620` 「F18f 사각지대 · 변이 M-e」). `M-e2`라는 이름은 테스트 소스에 없다(보고서 명칭) |
| (기존 감사) `SuspendClickBlockerAuditTests.PollsPanelChannel` | **「약해진 이유로」 초록이다.** 파일 텍스트에 `ArePanelsSuppressed`가 있기만 하면 참인데, N-20 뒤 (나) 소비자는 그 이름을 새 창구의 **인자로만** 가진다 — 옛 창구로 걷는지가 아니라 이름이 남았는지만 본다. **보강하지 않았다.** 소비자 분류는 T-B가 메서드·호출형 단위로 잰다 | `:194-195` 본문(`IndexOf("ArePanelsSuppressed") >= 0`) |
| §6-2 게터 추가 | 새 게터는 만들지 않았다 — 기존 `IsClickBlockerEnabled` | §6-2 정정 |
| §4-6 크랙 발동 가드 | 넣지 않았다 | §4-6 리더 판정 |

---

## 7. 적발 ③ — 등급 2→1 복귀 후 20초 안 우클릭이 설정창 자동 재오픈에 편승

### 7-1. 판정: **같은 축 문제다.** #6(`SettingsWindow.cs:169`)이 (나) 자동 경로인데 임대를 포함한 P를 읽는다.

### 7-2. 기전 (코드 판독)
1. 등급 2에서 설정창이 `:767`로 닫히고, `:782`에서 무장한다(`HidesScreenSurfaces` 참).
2. 2→1은 **에지가 없다**(r이 등급 2에서도 참이라 `TickPanelRetreatEntry`가 발화하지 않는다). 무장이 유지된다.
3. 사용자가 캐릭터를 우클릭하면 허가가 나고(g) → P가 거짓이 된다. 그러면 `:169`를 통과해 `:173` 20초 이내 조건이 맞으면 `Open(userInitiated: false)`가 실행된다.
4. `:659` `CloseOverlappingSurfaces`가 **사용자의 부채꼴·정보창을 걷는다.**
5. 자동으로 열린 설정창이 `:760`에서 **살아 있는 임대를 연장**한다(부활은 아니다). ⇒ 자동 표면이 사용자 임대에 편승해 **스스로 수명을 늘린다.**

### 7-3. 수정 뒤 — 편승이 구조적으로 0인 이유
- #6이 새 창구를 읽으면 실행 조건 = s ∨ r ∨ g가 거짓 ⇒ **g가 거짓**이다. 갱신은 만료된 임대를 되살리지 않으므로(`RenewedLeaseUntil`) 자동으로 열린 창은 임대를 **연장할 것이 없다.**
- 현재 비사용자 열기 진입점은 **2개**다.
  - `SettingsWindow.cs:181` — #6이 막는다.
  - `CharacterInfoWindow.ReopenFromSheet`(`SettingsWindow.cs:721`) — 사용자 흐름의 연속(§2-4)이다.
- ⇒ 이 두 진입점 목록이 유지되는 한 자동 경로 편승은 0이다.
- ★ **새 비사용자 진입점이 생기면 다시 뚫린다.** 백로그 감사 제안: `Open(…, userInitiated: false)` 호출부의 가드는 새 창구를 읽는다.

### 7-4. 부수 효과
- 등급 1 → 0 순간 사용자 창이 떠 있으면 재오픈은 그 창이 닫힐 때까지 기다린다(FFT 칸). **사용자 창을 빼앗지 않는다.** 20초 판정은 무변경이다.
- `ux-designer`에게 배정 대기 중인 §16-2b B5 「편승」 열은 **설계 결정 없이 문서화만** 남는다.

---

## 8. 플랫폼 영향

- **공통**: 수정 대상 5파일 모두 `#if` 0이다. 규칙은 `Platform/` 중립 위치에 둔다.
- **Windows**:
  - 등급 판정이 전경 창 기준이고, 우리 창·우리 프로세스가 전경이면 None이다(`Win32WindowService.cs:2054`·`:2063`).
  - H1이 참이면 순수 s ∨ r은 사용자가 우리 창을 만지는 동안 자동 표면을 흘린다. 이 안(g 항)은 흘리지 않는다(조건부 이득, H1 판독은 `dev-platform` 대기).
  - 사용자 창이 닫힌 뒤에도 우리 창이 계속 전경이면 r이 거짓이라 자동 표면이 돌아온다 — **H1 자체의 잔여**이며 이 안의 범위 밖이다.
  - 트레이 [설정 열기]가 같은 허가를 내므로, 이미 나간 프리뷰에도 드문 경로로는 노출돼 있었다.
- **macOS**:
  - 등급 판정이 layer 0 창 기준이라 H1에 해당하지 않는다.
  - 트레이가 없어 E-1 전 노출 경로는 ⌃⌥⌘P와 대기 톱니뿐이었다 → **E-1 뒤 노출 증가 폭이 더 크다.**
- **모바일(스크린샷 백드롭)**: 등급 원천이 없는 서비스는 `IsFullscreenAppActive` 강등 경로를 탄다(`StickmanAgent.cs:1629-1631`). 모바일 서비스가 거짓을 돌려주면 r = F라 영향 0이다 — **모바일 서비스 구현은 미확인.**

---

## 9. 무엇을 안 봤는가 · 미확인

- **실기 0회, 러너 0회, 변이 0회.** 제안한 R1이 수정 전에 실제로 빨간지는 **미확인**이다 — 박제로 확인할 것.
- 포스트잇 깜빡임은 코드 판독이다. 화면에서 체감되는 길이(부채꼴 펼침 0.3초 + 무입력 6초 이내)는 미측정이다.
- Windows H1 실재 여부(`dev-platform` 배정 중).
- ~~개발 전용 강제 발동(`ForceTriggerNow`, ⌃⌥⌘J 등): 등급 1에서 크랙 오버레이가 즉시 취소되는 쪽으로 바뀐다. 개발 전용이라 판정에서 제외했다.~~ 〔★ 2026-09-15 정정 — **전제가 거짓이었다**(작성자 오류). 정정 본문은 §9-1〕
- `TodoPostItWidget` 로그 두 줄(`[투두] 전체화면 감지`/`종료`)이 우클릭마다 찍히던 스팸은 수정 뒤 사라질 것으로 **추론**한다. 미측정이다.
- 에디터 스크립트(`Assets/Editor` 등)는 스캔 범위에 넣었고 독자 0이었다. 패키지 내부는 보지 않았다.

### 9-1. ★ 정정 — 명령 크랙(행동 명령창 [창 부수기] · ⌃⌥⌘X)은 개발 전용이 아니다 (2026-09-15, `ad49497` 커밋 뒤)

발단: `persona-immersion`(소은) 코드 판독 지적 → 판정(리더 배정). 코드 줄은 `ad49497` 기준이다. 〔2026-09-15 재확인 · verify-change A 조건부 반영: 작업 트리 `Interaction/WindowCrashDirector.cs`에는 A1 가드가 **미커밋**으로 들어와 있다(HEAD 대비 +25줄). 이 절의 크랙 줄 번호는 여전히 HEAD 기준이다. 〔묶음 ⑨ 보강: 예를 들어 `ForceTriggerNow` 첫 줄(앵커 `CommandAvailability availability = GetAvailability();`)은 `ad49497` blob과 `a8c3723`(HEAD) blob에서 `:109`이고, A1이 든 작업 트리에서는 `:134`다. **A1이 커밋되면 HEAD의 이 파일 줄 번호가 밀린다** — 그 뒤 이 절의 `WindowCrashDirector.cs` 줄 인용은 `ad49497` 기준으로만 참이므로 앵커로 찾는다〕 A 1 · 2 · 3 정정은 이 절 안에 취소선과 함께 있다〕

**무엇이 틀렸나 — 자백**
- 위 §9 줄은 `Interaction/WindowCrashDirector.cs:60-63`의 `ForceTriggerNow` 문서 주석(「강제 발동 … 데모 경로」)만 믿었고 **호출자를 세지 않았다.**
- 실제 호출자는 둘이고 둘 다 사용자 입구다: 행동 명령창 타일(`Interaction/ActionCommandPopover.cs:530`, 그 창 스스로 「개발 전용 항목 0」 `:59`) · 전역 단축키 ⌃⌥⌘X(`Interaction/AppControlDirector.cs:652`). ⌃⌥⌘X는 **개발 게이트 밖**이다(`:297` `bool x = chord && …` — 같은 파일 D·H·S·J·F만 `dev &&`, `:315-319`).
- 적힌 키 ⌃⌥⌘J도 틀렸다 — ~~J는 크랙이 아니라 개발 게이트 키다(`:318`).~~ 〔★ 2026-09-15 정정(verify-change A-2) — 「게이트 키」는 게이트를 여닫는 키로 읽혀 틀렸다〕 J는 크랙 키가 아니다. J는 **개발 게이트 뒤에 있는** 키다 — 게이트가 닫혀 있으면 키를 조회하지도 않는다(`:318` `bool j = dev && chord && IsKeyDown(GlobalKey.J);`). 하는 일은 **할일 리마인더 데모**다: `:368` `else if (jRise) Invoke(ControlAction.TodoReminder` → `:481-482` → `:730` `private void ForceTodoReminder(string source)` → `TodoReminderDirector.ForceTriggerNow`(그 메서드 문서가 스스로 「개발 전용 ⌃⌥⌘J」라고 적는다).

**N-20이 만든 것**
- 등급 1에서 사용자가 부채꼴 → [행동] 명령창을 연 동안 부채꼴이 매 프레임 임대를 갱신한다(`Interaction/GearRadialMenuWidget.cs:1290`, 팝오버가 열린 동안 `_phase` Open 유지 `:1277-1278`). 그 상태에서 [창 부수기]는 **「준비됨」으로 보이고**(가능 판정 `WindowCrashDirector.cs:79-104`에 등급 조건 없음), 누르면 캐릭터가 휘두르지만 새 가드(`:170-171`)가 **다음 프레임에 오버레이를 취소**한다 → 「결과 없는 휘두르기」, 원칙 1 싱크 위반.
- N-20 전(가드가 `ArePanelsSuppressed || IsSuspended`)에는 임대 칸에서 P가 거짓이라 금이 `windowCrashOverlayDurationSeconds = 3`(에셋) 동안 보였다 — 조건: 그 3초 동안 명령창·부채꼴이 떠 있을 것.
- ⌃⌥⌘X를 등급 1 무허가에서 누르는 경로는 **N-20 전부터** 같은 결함이다(P 참 → 즉시 취소).

**분류 — (나)**: 사용자 명령 크랙은 (가) 사용자 표면이 아니다. ① 임대의 목적은 설정 스위치 도달뿐이고 연출에는 허가를 내지 않는다(`Core/StickmanAgent.cs:380-381` · `:403-404`) ② 대상이 최상위 실제 창 = 등급 1에서는 발표 화면 자체다(`WindowCrashDirector.cs:227-242`, 2026-09-02 판단 「그 자체가 침해」 `:160-163`) ③ 닫는 조작이 없는 3초 자체 수명 오버레이다. (§2-2 #9 「자동」은 자율 경로 기준이고, 같은 파일에 명령 경로가 있다.)

**대응안**

| 안 | 내용 | 판정 |
|---|---|---|
| **A1** | 가능 판정 `GetAvailability`에 N-20 창구 가드(`SuppressesUnsummonedSurfaces(P, g)`) → 타일 회색 + 사유. ⌃⌥⌘X도 `ForceTriggerNow`가 같은 판정으로 건너뛴다(`:109-115`) | **채택 권고** — 원칙 1·2 모두 선다. 한 파일, N-8 목록 밖. 사유 문구는 등급 1과 FFT 양쪽에서 참이어야 한다(`design-narrative`) |
| A2 | 자율 `TickAutoTrigger` 맨 앞 호출형 가드 | **백로그** — 출하 확률 0(`windowCrashChance: 0`)이라 사용자 체감 0. §4-6 리더 판정(N-20 밖) 유지. 범위 확장 판정은 `docs/systems/UNSUMMONED_EFFECTS_INVARIANT.md` 층 1 |
| B | 명령 크랙은 취소 조건에서 제외 | **기각** — 발표 위 금 3초(원칙 2) · 임대 계약 확장 · 무허가 ⌃⌥⌘X로 등급 1 억제 우회 · T-B 파일 단위 (나) 분류가 거짓이 된다 |

**테스트 요구**
- PlayMode R5: 등급 1 + 명령창 열림(임대 갱신 중) → 크랙 타일 `IsReady` 거짓 · 사유 상수 일치 · `ForceTriggerNow` false · `WindowCrash` 전이 0 · 오버레이 이벤트 0. 음성 대조: 등급 0 · 명령창 열림 → `IsReady` 참 · 오버레이 3초 관측. FFT 1건(등급 1 해제 뒤 명령창 열린 채 → 회색, 사유가 거짓말하지 않는지).
- EditMode T-B: `GetAvailability` 메서드 단위 항목(본문에 새 창구 호출 존재 단언).
- 변이 M-f(A1 가드 삭제) → R5 · T-B 빨강. M-f2(가드를 P 단독으로) → R5 빨강.

~~**N-20 해제 조건 추가 제안(리더 판정)**: 「6. 크랙 가능 판정이 `SuppressesUnsummonedSurfaces`를 따른다 — PlayMode R5 GREEN + 변이 M-f 빨강」.~~ 〔★ 2026-09-15 정정(verify-change A-1) — 번호가 틀렸다〕

**ROADMAP N-20 해제 조건 7로 반영됨(리더 채택).**
- 위치: `docs/strategy/ROADMAP.md` 작업 트리의 「N-20 해제 조건」 목록. 조건 7 앵커는 `행동 명령창 [창 부수기]와 ⌃⌥⌘X의 가능 여부 판정에 같은 창구 가드`다.
- 조건 6은 A1이 아니라 QG-1이다(앵커 `6. **증거는 QG-1 기준으로 센다**`).
- **번호 체계가 다르다**: 이 문서 §5 「해제 조건 제안」은 5항목(1 착지 · 2 EditMode · 3 PlayMode · 4 변이 · 5 QG-1)이다. ROADMAP은 리더 수용 때 「verify-change 독립 재측정 뒤 리더 커밋」을 5로 따로 세웠고, 그래서 QG-1이 6이 되고 A1이 7로 붙었다. §5의 번호로 ROADMAP 조건을 부르지 않는다.
- 확장 **7-b**(창 도둑 · 그라피티 명령 가드)는 **ROADMAP N-20 해제 조건 7-b로 등재(묶음 ⑧)** 됐다(앵커 `창 도둑 · 그라피티 명령의 가능 여부 판정에 같은 창구 가드`). 판정 근거는 `docs/systems/UNSUMMONED_EFFECTS_INVARIANT.md` 「6-2. 1.0」이다.

**백로그**
- 취소 로그 사유 누락: `Interaction/WindowCrashRenderer.cs:169` 「취소(대상 창 닫힘/최소화, 전체화면 게임 감지, 긴급정지)」에 **등급 1**(2026-09-02부터의 원인)과 **임대 칸**(N-20부터)이 없다 → `coder`(원인을 이벤트에 실어 보내거나 문구를 원인 전체로).
- 부수 발견(담당 제안만): 크랙 알파 0.45 → 1 튐(`WindowCrashRenderer.cs:346` · `:354`가 페이드 첫 프레임에 알파 1로 덮음) → `coder` + `design-art`. 해머 스윙 포즈 부재 — `TimedSpectacleState`를 쓰는 연출 4종 공통, 포즈 파일 참조 0 → `design-motion`.

---

## 10. Tasklist 등재 문장 (리더가 옮긴다)

> **[game-architect] 자동 표면 임대 축 분리 판정**(`docs/systems/AUTO_SURFACE_LEASE_AXIS.md`, 문서만, HEAD `eb4670d`): `ArePanelsSuppressed`(= s ∨ (r ∧ ¬g))를 읽는 코드 독자 10곳을 전수 분류했다(양성·음성 대조, 자백: ERE 죽은 프로브 1건 → `-F`로 재측). 사용자 표면 (가) 5곳(정보창 `:1034` · 부채꼴 `:1263` · 팝오버 `:449` · 설정창 닫기 `:767` · 시트 복귀 `:708`)은 불변이다. **자동 표면 (나) 4곳(포스트잇 `:414` · 리마인더 `:53` · 크랙 `:167` · 설정창 자동 재오픈 `:169`)을 새 정책 `UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces(ArePanelsSuppressed, IsUserSummonGrantActive)`(= s ∨ r ∨ g)로 옮긴다.** 에이전트 로그 `:1698`은 진단이며 N-8 파일이라 불변이다. 불변식: 「사용자 허가는 사용자가 부르지 않은 표면을 절대 드러내지 않는다 — 새 값은 `SuppressesPanels`를 포함하고 허가에 대해 단조 증가다」. 순수 s ∨ r은 r이 private(N-8 목록 B)이라 목록 파일을 건드리지 않고는 만들 수 없다. 대신 이력 칸(FFT)이 적발 ③에서 사용자 창 탈취를 막고 Windows H1 누수도 막는다(조건부) → 채택했다. **N-8 결속 무영향**(구현 5파일 목록 밖 확인, `StickmanAgent.cs` 0줄). E-1·E-2 진리표와 E-4 작업 트리 F18e·F18f·V1 모두 무충돌이다(호출형 준수 조건 — `HidesScreenSurfaces`로 조립하면 F18f가 옳게 빨개진다). **적발 ③은 같은 축이며 같은 수정으로 닫힌다.** 출시 관문: **1.0 필수 · 프리뷰 해제 조건 아님 · E-1이 든 첫 프리뷰 동반 권고**(〔리더 채택 2026-09-15: E-1 든 프리뷰 필수로 강화〕) → ROADMAP §60-9 **N-20** 행 제안. ★ 로그 계수 위험: `[표면회수] 등급 1 해제/진입` 줄은 임대 발급·자연 만료에서도 찍혀, E-3 로그로 등급 1 재진입 빈도를 세면 과대 계수된다. 순서: **E-4 커밋 → coder-ui(정책 1 + 소비자 4 + 포스트잇 테스트 게터) → test-engineer(EditMode T-A·T-B·T-C, PlayMode R0~R4 · 변이 M-a~M-d, 벽시계) → verify-change**. 문서 동반: `ux-designer` §16-2b 경계 #3 문구. Windows 영향: 공통 코드, H1이 참이면 이 안이 사용자 조작 중 누수를 막는다(H1 잔여는 `dev-platform`). macOS 영향: 공통 코드, 트레이가 없어 E-1 뒤 노출 증가가 더 크고 수정 효과도 같다.
