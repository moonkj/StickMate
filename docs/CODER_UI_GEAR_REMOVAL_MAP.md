# 톱니 제거 → 캐릭터 우클릭 부채꼴 : **변경 지도** (구현 전 전수 조사)

작성 2026-09-05 · `coder-ui` · **조사 전용 라운드 — 프로덕션 `.cs` 0줄 수정**
대상 지시: 사용자 2026-09-05 — *"지금은 메뉴 스크류모양이 따로 있는데 그냥 캐릭터에서 마우스 오른쪽
버튼 누르면 촤르륵 펼쳐지게 변경"* / 리더 지시: *"구현 전에 무엇이 어디에 묶여 있는지 전수 조사"*

## 이 문서의 자격 — 확인한 것 / 확인 못 한 것

| | 내용 |
|---|---|
| **소스 실측으로 확인** | 톱니·부채꼴 전 소스, 소비자 39개 프로덕션 파일, 테스트 38개 파일, 프리팹 YAML, `.asmdef` 구성, 감사 6종의 니들 |
| **다른 라운드 산출물을 인용(내가 재지 않음)** | `docs/PLATFORM_RIGHTCLICK_FAN.md`(dev-platform 2026-09-05)의 클릭관통·삼킴·경합 분석. **인용에는 출처를 달았다** |
| **확인 불가** | 실기 동작 전부(앱 미실행), Unity 컴파일·러너(배치모드 미사용 — 리더 승인 대기), Windows 전부 |

**"고쳤다"고 쓸 수 있는 것이 이 문서에는 하나도 없다.** 전부 "이렇게 묶여 있다"이다.

★ **`ux-widgets`의 `docs/UX_RIGHTCLICK_FAN_MENU.md`는 작성 시점에 아직 존재하지 않았다**(`ls` 확인).
이 문서는 **설계를 정하지 않는다** — 문구·배치·제스처는 전부 그쪽 소관이고, 여기 나오는 「권고」는
**구조·비용·위험**에 대한 것뿐이다.

---

## 0. 다섯 줄 요약

1. **부채꼴은 이미 앵커 무관이다.** `GearRadialMenuWidget.Expand(Vector2 anchorUnityScreen)` — 톱니 좌표를
   **인자로 받을 뿐** 톱니를 참조하지 않는다. 캐릭터로 옮기는 것은 **인자 한 개를 바꾸는 일**이다.
2. **그런데 부채꼴은 입력을 하나도 갖고 있지 않다.** 커서 폴링·호버·버튼 누름/뗌·바깥 클릭 접힘·
   **클릭 차단막**이 전부 톱니 소유다(`GearRadialMenuWidget`에 `IGlobalPointerButtonService` 참조 **0건**).
   ⇒ **이번 라운드의 본체는 「부채꼴 이사」가 아니라 「입력 소유권 이전」이다. 신규 컴포넌트 350~420줄.**
3. ★★ **톱니를 완전히 없애면 2026-09-03 사용자 확정이 뒤집힌다.** 사용자 명시 숨김(⌃⌥⌘K)은
   **캐릭터만** 가리고 톱니는 일부러 남긴다 — 신고 *"전부 다 없어져버려서 다시 나오게 할 방법이 없어"*
   → *"메뉴버튼은 보여야지"* → *"캐릭만 가리고"*. **캐릭터가 숨은 상태에서는 우클릭할 대상이 없다.**
   근거: `Core/StickmanAgent.cs:291 HidesScreenSurfaces => _isSuspended && !IsUserHiddenOnly` +
   `Interaction/InfoGearIconWidget.cs:822`. 잠금 테스트 2건이 이것을 명시적으로 지키고 있다
   (`ManualHideUserAxisTests.축분리_톱니는_사용자숨김에서_남고_전체화면_게임에서는_사라진다` 등).
4. **타입을 지우면 EditMode·PlayMode 두 어셈블리가 모두 컴파일 실패한다** — 테스트 **2,530건이 0건**이 된다
   (EditMode 1,890 / PlayMode 640, `[Test]`+`[UnityTest]`+`[TestCase]` 실측). 타입 참조가 있는 테스트 파일이
   **15개**(문자열 니들만 4개 · 주석만 2개). ⇒ **단계 분리가 필수다**(§4-3).
5. **권고: 이번 라운드에 파일을 지우지 마라.** ⑥에 근거를 적었다 — 지우는 것과 「기본값을 끄는 것」의
   사용자 체감은 같은데 **되돌리는 비용이 1줄 vs 1,681줄**이고, ③의 탈출구 문제는 **어느 쪽이든 남는다.**

---

## 1. 톱니가 소유한 것 — 전수

### 1-1. 파일 자체

```
Assets/_Project/Scripts/Interaction/InfoGearIconWidget.cs   1,681줄 / 118,202바이트
  클래스 1개(sealed MonoBehaviour), partial 아님, 형제 파일 0개
```

**구역별 줄 수**(실측, 경계는 파일의 `// ====` 구분선 기준):

| 줄 | 줄수 | 구역 | **행선지** |
|---:|---:|---|---|
| 1–155 | 155 | 클래스 문서 | **삭제** (일부 문장은 신규 컴포넌트 문서로 이관 — §1-6) |
| 156–352 | 197 | 치수·형태 상수(잇수·팁/뿌리 반지름·헤일로·알파) | **삭제**. 단 `TipRadius`/`HaloWidth`/`StrokeWidth`/`HitPaddingPoints`는 **테스트 3파일이 계산에 쓴다**(§5) |
| 353–408 | 56 | 필드 | 일부 **이동**(`_menu`/`_buttonService`/`_agent`/`_config`/`_camera`), 나머지 삭제. ★ `_focusDirector`·`_todoDirector`는 **선언만 있고 참조 0건 — 죽은 필드다** |
| 409–609 | 201 | 공개 API(진단·치수·상태) | §1-2 표에 개별 행선지 |
| 610–636 | 27 | 테스트 API | **이동**(`FeedPointerForTests`) / **삭제**(`StartSpinForTests`·`ResetPositionForTests`) |
| 637–733 | 97 | 위치 되돌리기 + 온보딩 대여 | **삭제 후보**. ★ **온보딩 3종의 프로덕션 호출자가 0명이다**(§1-5) |
| 735–777 | 43 | Awake / Start(기동 배너) | **이동**(형태를 바꿔서) |
| 778–783 | 6 | OnDestroy | **이동** |
| 784–861 | 78 | LateUpdate(비침해 게이트 + 등급1 임대갱신) | ★★ **이동 — 이 78줄이 이 파일에서 가장 위험한 부분이다**(§1-4) |
| 862–898 | 37 | ApplySuspendHide / ReleaseSuspendHide | **이동**(톱니 그림 부분만 제거) |
| 899–912 | 14 | TickMenuHover | **이동(그대로)** |
| 913–923 | 11 | RestoreSavedPositionOnce | **삭제** |
| 925–1000 | 76 | PlaceOnScreen | 대부분 **삭제**. ★ **차단막 갱신 20줄만 이동**(983–999) |
| 1001–1010 | 10 | `Union(Rect,Rect)` | **이동** |
| 1011–1169 | 159 | 좌표·경계(예약 띠 인셋 / 기본 위치 / 클램프) | **삭제**. ★ 단 §5의 감사 3건이 이 구역을 겨눈다 |
| 1170–1183 | 14 | EnsureBuilt | **삭제** |
| 1184–1288 | 105 | Build(기어 도형) + 도형 헬퍼 | **삭제** |
| 1289–1315 | 27 | TickSpin(회전 연출) | **삭제** — ★ 사용자 원문 *"클릭하면 기어가 회전하면서"*가 여기서 죽는다(§1-6) |
| 1316–1332 | 17 | ExpandMenu / CollapseMenu | **이동(그대로)** |
| 1333–1367 | 35 | TickHoverAlpha / ApplyAlphaToAll | **삭제** |
| 1368–1396 | 29 | TickPointer(좌버튼 폴링) | **이동 + 우버튼 추가** |
| 1397–1407 | 11 | ProcessPointer | **이동(그대로)** |
| 1408–1457 | 50 | BeginPress | **이동(드래그 부분 제거 → 약 30줄)** |
| 1458–1495 | 38 | UpdatePress(드래그 판정) | **삭제** |
| 1496–1523 | 28 | EndPress | **이동(드래그 분기 제거 → 약 18줄)** |
| 1524–1540 | 17 | AbortPress | **이동(드래그 분기 제거)** |
| 1541–1567 | 27 | CommitDragPosition / PersistGearCenter | **삭제** |
| 1568–1610 | 43 | ActivateClick | ★ **재작성** — 토글 의미가 「톱니 클릭」에서 「우클릭」으로 바뀐다 |
| 1611–1622 | 12 | ActivateMenuButton | **이동(그대로)** |
| 1623–1635 | 13 | TickDragVisual | **삭제** |
| 1636–1681 | 46 | 테스트 커서 주입 + `TryGetCursorUnityScreen` + `IsCursorOverIcon` + 머티리얼 | **이동**(`IsCursorOverIcon` → `IsCursorOverCharacter`로 **교체**) |

⇒ **이동 ≈ 300줄 / 삭제 ≈ 1,140줄 / 재작성 ≈ 240줄**(문서 포함).

### 1-2. 공개 표면(메서드·프로퍼티) 전수 — 41개

| # | 멤버 | 밖에서 쓰는 곳 | **행선지** |
|---:|---|---|---|
| 1 | `IsSpinning` | 없음(내부+테스트) | 삭제 |
| 2 | `IsIconVisible` | `FullscreenSuspendUiHidingTests`·`ManualHideUserAxisTests` | 삭제(테스트 동반 수정) |
| 3 | `IsClickBlockerEnabled` | `InfoGearRadialMenuTests`·`FullscreenPanelRetreatTests` | **부채꼴로 이동**(신규 컴포넌트가 같은 이름으로 노출) |
| 4 | `IconScreenCenter` | 테스트 다수 | **부채꼴로 이동** → 「앵커 좌표」 |
| 5 | `IconScreenRect` | ★ **`TodoPostItWidget.ResolveRightInsetPoints`(프로덕션)** + 테스트 | ★ **판단 필요** — §1-3 |
| 6 | `InteractiveScreenRect` | 테스트(`InteractiveRectCoversButtonsOnlyWhileExpanded`) | **부채꼴로 이동** |
| 7 | `IsMenuExpanded` / 8 `IsMenuVisible` | 테스트 | **이동** |
| 9 | `MenuButtonScreenCenter(GearMenuButton)` | `GearMenuHoverLabelTests` | **이동**(사실상 `_menu.ButtonScreenCenter` 위임) |
| 10 | `MenuButtonProgress(GearMenuButton)` | 테스트 | **이동** |
| 11 | `MenuExpandTotalSeconds` (static) | 테스트 | **삭제** — `GearRadialMenuWidget.ExpandTotalSeconds`로 직접 가라 |
| 12 | `MenuReadySeconds` (static) | 테스트 | ★ `max(부채꼴 펼침 0.30, 회전 1.4)`인데 **회전이 죽으면 0.30이 된다.** 부채꼴로 이동하며 **값이 바뀐다** |
| 13 | `GearAngleDegrees` | `InfoGearMeshingTests` | 삭제 |
| 14–21 | `Teeth`·`TipRadius`·`RootRadius`·`HubRadius`·`StrokeWidth`·`HaloWidth`·`ToothTipHalfFraction_ForTests`·`ToothRootHalfFraction_ForTests` | `InfoGearHaloContrastTests`·`SurfaceSafeAreaPolicyTests`·`ReservedScreenEdgeContractTests` | **삭제** — ★ 테스트 3파일이 **이 값들로 계산을 한다**(§5-2) |
| 22 | `ResolveHaloColor(Color)` (static) | `InfoGearHaloContrastTests` · `UiChrome.cs:1331` 주석 | 삭제(색 계약은 `UiChrome`이 이미 별도로 잠근다) |
| 23 | `InkColorForTests` | 테스트 | 삭제 |
| 24 | `IdleOpacity` / 25 `UserHiddenOpacity` | `InfoGearHaloContrastTests` | 삭제 |
| 26 | `IsDimmedForUserHide` | `ManualHideUserAxisTests` | ★ **판단 필요** — §3-1의 「숨김 중 진입점」 결정에 종속 |
| 27 | `IsDraggingIcon` | 테스트 | 삭제 |
| 28 | `HasCustomPosition` | `InfoGearPositionOwnershipTests` | 삭제 |
| 29 | `IconCenterPoints` / 30 `HomeCenterPoints` | 테스트 | 삭제 |
| 31 | `HitRadiusPoints` (static) | ★ `ReservedScreenEdgeContractTests` 6곳 | 삭제(테스트 동반) |
| 32 | `DefaultRightMarginPoints` / 33 `SideBandMarginPoints` | ★ `ReservedScreenEdgeContractTests` 5곳 | 삭제(테스트 동반) |
| 34 | `DragLongPressSeconds` / 35 `DragMoveThreshold` / 36 `ShouldBeginDrag(f,f)` | `InfoGearDragTests` | **삭제** — 드래그가 통째로 사라진다 |
| 37 | `StartSpinForTests()` | `InfoGearMeshingTests` | 삭제 |
| 38 | `FeedPointerForTests(bool,Vector2)` | ★ **PlayMode 8파일 40여 곳** | **이동(이름 유지 권고)** |
| 39 | `ResetPositionForTests()` | 없음(전수 0건) | **삭제 — 죽은 API** |
| 40 | `ReturnToDefaultPosition(string)` | ★ **`SettingsWindow.OnGearHomeClicked`(프로덕션)** | **삭제** — 설정 행과 함께 |
| 41 | `IsOnboardingPositionOwned` / `BeginOnboardingPlacement` / `EndOnboardingPlacement` | ★ **프로덕션 호출자 0명**(테스트 전용) | §1-5 |
| 42 | `FeedHoverCursorForTests` / `ClearHoverCursorForTests` | `GearMenuHoverLabelTests`·`GearMenuOnboardingHintTests` | **이동(이름 유지 권고)** |

> ★ **이벤트는 0개다.** 이 클래스는 `event`를 하나도 노출하지 않는다(`grep` 실측). 구독 해제 누수 걱정은 없다.

### 1-3. 다른 프로덕션 파일에서의 참조 — **타입 참조 6곳 / 주석·문자열 참조 33곳**

| 파일 | 줄 | 무엇 | **행선지** |
|---|---:|---|---|
| `Interaction/TodoPostItWidget.cs` | 222 · 289 · 578~607 | ★ **실제 코드 의존** — 포스트잇이 `_gear.IconScreenRect`를 매 프레임 읽어 **톱니를 비켜 앉는다**(51-9-3) | ★ **판단 필요.** 톱니가 사라지면 이 회피가 통째로 무의미해진다. 코드는 `_gear == null`에서 **기준선으로 안전 복귀**하도록 이미 짜여 있어(`:578`) **깨지지는 않는다** — 그래서 **삭제가 안전한 유일한 소비자**다. 테스트 3건은 함께 죽는다 |
| `Interaction/SettingsWindow.cs` | 321 · 895 · 925 · 1249~1284 · 1347~1373 | ★ **설정 행 2개 + 경고 캡션 + 게이트 2개 + `ResolveGearWidget`** | **삭제**(§1-7). 세로 예산이 **줄어드는** 방향이라 위험 낮음 |
| `Editor/SceneBootstrapper.cs` | 1105 | `root.AddComponent<InfoGearIconWidget>()` | **교체**(신규 컴포넌트로) |
| `Interaction/GearRadialMenuWidget.cs` | 102 · 133 · 1016 · 1027 · 1966 | 주석 5곳(입력 소유자가 톱니라는 서술) | **문구 갱신 필수** — 안 고치면 다음 사람이 없는 클래스를 찾는다 |
| `Core/UiLayoutModel.cs` | 9 · 24 · 29~35 · 92~139 · 162~166 | 톱니 중심 저장의 **모델 전체** | §1-8 |
| `Core/StickmanAgent.cs` | 312 · 424~425 · 1594 · 1691~1692 (+ 주석 21곳) | ★ **`:424`·`:1692`는 사용자에게 찍히는 문장**이다 — *"톱니·열려 있던 창·부채꼴은 그대로 남습니다"* | **문구 갱신 필수**(거짓이 된다) |
| `Core/ItemCatalog.cs` | 281 | ★ **사용자 노출 문구** `MenuOnlyStatus = "톱니 메뉴에서"` (정보창 행동 목록의 상태 슬롯) | ★ **`ux-designer`/`design-narrative` 배정** — §1-9에 폭 계산 |
| `Interaction/AppControlDirector.cs` | 172 · 174 · 186 | 기동 배너 3줄. `:174`는 **지금 사용자에게 거짓말**을 하고 있다(dev-platform §2-2 인용) | **문구 갱신 필수** |
| `Interaction/CharacterInfoWindow.cs` | 751~755 · 860 · 1379 | *"(1) 화면 우상단 톱니 아이콘 → 부채꼴 [캐릭터]"* — **사용자에게 찍히는 안내** | **문구 갱신 필수** |
| `Interaction/ActionCommandPopover.cs` | 31~35 · 134 | 앵커·드래그 불가 사유 서술 | 문구 갱신 |
| `Interaction/PopoverPanel.cs` · `UiChrome.cs` | 363 · 508 · 593 / 693 · 1331 | 관례 인용 | 문구 갱신(경미) |
| `Platform/*` 8파일 | `ReservedTopBarProbe:13,17` · `IReservedScreenEdgeService:148` · `MacReservedTopBarService:60` · `FallbackPlatformWindowService:87` · `IGlobalKeyStateService:41,106` · `SystemTrayCommandRouter:106` · `WindowsSystemTrayIcon:16,185,631` · `SystemTrayPresencePolicy:13` | ★ **「다섯 파일」/「3중 경로」 목록이 톱니를 센다** | ★ **`dev-platform` 배정** — 그중 `WindowsSystemTrayIcon:185,631`은 **사용자 노출 문자열**이다 |

**전수 규모**: 프로덕션 `.cs`에서 `톱니|기어` **383건 / 39파일**, 테스트에서 **437건**.
`InfoGearIconWidget.cs`라는 **파일명 형태**의 참조는 **14건**(§5-1이 이걸 감사한다).

### 1-4. LateUpdate 78줄 — **이 파일에서 가장 위험한 구역**

세 가지가 여기 겹쳐 있고, 셋 다 **다른 축**을 읽는다. 하나라도 잘못 옮기면 **릴리즈 블로커**다.

```csharp
// InfoGearIconWidget.cs:822  — 비침해 1차 관문
if (_agent.HidesScreenSurfaces || !AppSettingsModel.GearIconVisible) { ApplySuspendHide(...); return; }
// :840  — 등급 1 탈출구의 임대 갱신
if (IsMenuExpanded || (_window != null && _window.IsOpen)) _agent.RenewUserSummonGrant();
```

| 축 | 톱니가 읽는 값 | 부채꼴이 읽는 값 | 신규 컴포넌트는? |
|---|---|---|---|
| 자동 숨김 게이트 | `HidesScreenSurfaces`(등급 2 + 게임) | `ArePanelsSuppressed`(등급 1 포함) | ★ **`ArePanelsSuppressed`여야 한다.** 신규 컴포넌트는 「상시 표면」이 아니라 **부채꼴의 입력 어댑터**다 |
| 임대 **갱신** | `RenewUserSummonGrant()` | — | **이동(그대로)** |
| 임대 **발급** | `ActivateClick():1601 TryGrantUserSummon("톱니 클릭")` | — | ★★ **반드시 `Expand()` 앞**에 둔다. 뒤에 두면 **펼쳐지는 그 프레임에 회수되어 「우클릭이 안 먹는다」가 된다** — 파일 주석 `:1596`이 그것이 2026-09-03 이전의 실제 증상이었다고 적어 두었다 |

★ 소스 감사가 이 축을 **문자열로 잠근다**: `ManualHideAxisSeparationAuditTests.톱니는_사용자_명시_숨김에서_살아남는다`가
`InfoGearIconWidget.cs`에서 `.IsSuspended`와 `.ArePanelsSuppressed`의 **부재**를 단언한다(:199~217).
파일이 사라지면 `ReadSource`가 **예외로 죽는다**(조용한 초록이 아니라 빨강 — 그건 다행이다).

### 1-5. 온보딩 3종 — **프로덕션 호출자 0명**

```
BeginOnboardingPlacement / EndOnboardingPlacement / IsOnboardingPositionOwned
  프로덕션 호출: 0건   (전수 grep, 양성 대조: 테스트에서는 8건이 잡힌다)
  「온보딩」이라는 낱말이 있는 프로덕션 파일: InfoGearIconWidget.cs / GearRadialMenuWidget.cs 뿐
```
`docs/UX_FLOW.md` §35-2/§51-6은 **톱니를 주인공으로 하는 첫 실행 연출**을 이미 설계해 두었고
(§51-6-1이 *"온보딩 도중 사용자가 톱니를 직접 누른다"*를 다룬다), **그 연출은 아직 구현되지 않았다.**
톱니가 사라지면 **그 설계 전체가 앵커를 잃는다.**
⇒ **행선지: 코드는 삭제 / 설계는 `ux-widgets`·`ux-designer`에 재배정.** 리더 판단 항목 **L-U4**.

### 1-6. 사용자 원문 3건이 여기서 죽는다 — **정직하게 적는다**

| 원문 | 날짜 | 무엇이 죽는가 |
|---|---|---|
| *"바탕화면 오른쪽 상단에 기어 표시같은걸 띄워놓고 클릭하면 기어가 회전하면서 캐릭터 창이 나오게끔"* | 2026-08-29 | **상시 기어 + 회전 연출**(`SpinSeconds 1.4` · `SpinTurns 4`) 통째로 |
| *"기어의 디자인도 좀 멋있게 바꿔줘"* / *"큰기어와 작은기어가 맞물려"* | 2026-08-30 | 조형 197줄. (맞물림은 이미 2026-09-01에 폐지됨 — 새로 죽는 것은 단일 기어 조형) |
| *"캐릭터 설정 기어들도 길게 클릭해서 위치 옮길 수 있게 해줘"* | 2026-08-30 | **길게 눌러 옮기기 + 저장 + [처음 자리로]** 전부 |

★ **이 셋은 2026-09-05 신규 지시가 덮어쓴 것이지 「잊힌 것」이 아니다.** 그러나
**「기어가 회전하면서」는 새 지시와 충돌하지 않는다** — 우클릭 순간에 회전할 기어가 없을 뿐이다.
대체 연출(예: 캐릭터가 반응하는 0.1초)이 필요한지는 `design-motion`·`ux-widgets` 판단이다.

### 1-7. 설정 항목 — `Core/StickConfig.cs`에는 **톱니 필드가 0개다**

전수 확인(양성 대조 포함: 같은 grep이 `throwTumbleEnabled:2400`을 정상 검출):
```
StickConfig.cs (3,493줄)  —  gear|톱니|radial|부채꼴|fan  →  히트 1건, 그것도 무관한 문장
```
톱니 설정은 **전부 `AppSettingsModel` + `UiLayoutModel`에 있다.**

| 저장 항목 | 선언 | 세이브 필드 | 소비자 | **행선지** |
|---|---|---|---|---|
| `AppSettingsModel.GearIconVisible` (기본 `true`) | `AppSettingsModel.cs:84,93,317,325,375` | `CharacterSaveStore.cs:268 gearIconVisible` | `InfoGearIconWidget:822` · `SettingsWindow:895,925,1250,1253` | ★ **판단 필요** — ⑥의 권고안에서는 **살린다**(기본값만 뒤집음) |
| `UiLayoutModel.HasGearCenter` / `GearCenterPoints` | `UiLayoutModel.cs:31,35,92,124,138,165` | `CharacterSaveStore.cs:217,220,221` (`gearPositionSaved`/`gearCenterXPoints`/`gearCenterYPoints`) | `InfoGearIconWidget` 3곳 · `SettingsWindow.OnGearHomeClicked` | **소비자 삭제 / 모델·세이브 필드는 남긴다**(§1-8) |

### 1-8. 세이브 스키마 — **필드를 지우지 마라**

`CharacterSaveStore.cs`의 4개 필드(`gearPositionSaved`·`gearCenterXPoints`·`gearCenterYPoints`·`gearIconVisible`)를
지우면 **기존 세이브 파일이 있는 사용자에게 `JsonUtility`가 값을 버린다.** 지금 형태는
「없으면 false/true 기본값」으로 **하위 호환이 이미 성립**해 있고(`:45`, `:75`, `:147` 주석),
필드를 **남긴 채 소비만 끊는 것**은 스키마 버전을 올릴 필요조차 없다.

⇒ **권고: `CurrentVersion`을 올리지 않는다. 필드 4개를 그대로 둔다.**
`UiLayoutModel`은 클래스 문서가 이미 *"지금 담는 값은 … 하나뿐이지만"*이라며 **확장을 전제**하고 있으므로,
껍데기를 남겨도 죽은 코드가 아니다(부채꼴 관련 값이 새로 생기면 그 자리로 들어온다).
잠금 테스트: `UiLayoutPersistenceTests`(9곳) · `AppSettingsModelContractTests`(5곳) — **건드리지 않으면 그대로 초록.**

### 1-9. 사용자 노출 문구 — **7곳** (여기 있는 것을 `coder-ui`가 지어내지 않는다)

| # | 위치 | 현재 문구 | 폭 제약 |
|---:|---|---|---|
| 1 | `SettingsWindow.cs:1249` | 토글 라벨 **"톱니 아이콘"** | 카드 라벨 |
| 2 | `SettingsWindow.cs:1272` | 캡션 **"끄면 캐릭터 정보창은 ⌃⌥⌘I 로만 열 수 있어요."** | — |
| 3 | `SettingsWindow.cs:1280` | 행 라벨 **"톱니 위치"** / 버튼 **"처음 자리로"** | ★ 버튼 폭 `26 + 글자수×9` — **글자 수가 바뀌면 옆 버튼이 미끄러진다**(`:1230` 경고) |
| 4 | `SettingsWindow.cs:1282` | 캡션 **"드래그해서 옮긴 자리를 화면 오른쪽 위로 되돌립니다."** | — |
| 5 | `SettingsWindow.cs:1279` | 게이트 문구 **"아직 옮긴 적이 없어요."** | — |
| 6 | ★ `Core/ItemCatalog.cs:281` | **`MenuOnlyStatus = "톱니 메뉴에서"`** (정보창 행동 목록 상태 슬롯) | ★ 파일 주석의 실측: **슬롯 96pt · 캡션 10pt에서 6글자 ≈ 63pt**. ⇒ 10.5pt/글자. **"우클릭 메뉴에서"(8글자) ≈ 84pt < 96pt — 들어간다(여유 12pt)**. "캐릭터 우클릭으로"(9글자) ≈ 94.5pt — **여유 1.5pt, 위험** |
| 7 | `CharacterInfoWindow.cs:755` · `StickmanAgent.cs:424` · `AppControlDirector.cs:172,174,186` · `WindowsSystemTrayIcon.cs:185,631` | 「진입 경로」 안내 문장들 | — |

★ **1~5는 항목 자체가 사라지므로 문구가 필요 없다. 6과 7은 새 문구가 필요하다** →
`ux-designer`·`design-narrative` 배정 항목이고, **`coder-ui`가 정하지 않는다.**

---

## 2. 부채꼴이 톱니에 의존하는 지점 — **무엇이 죽는가**

### 2-1. 결론부터 — 부채꼴 본체는 **거의 안 죽는다**

`GearRadialMenuWidget.cs`(2,319줄)에서:
- `IGlobalPointerButtonService` 참조 **0건**, `TryGetCursorPosition` **0건**, `Update()` **없음**.
- `Awake()`는 `GetComponent<StickmanAgent>()` + `BuildUi()`뿐.
- 앵커는 **인자**다: `public void Expand(Vector2 gearCenterUnityScreen)` → `_gearCenterPoints = ScreenToPoints(...)`.
- 좌표 변환은 **자기 안에 닫혀 있다**(`ScreenToPoints`/`PointsToScreen`이 `ScreenCoordinateConverter.UnityScreenToCanvas/CanvasToUnityScreen`을 쓴다).
  ⇒ **DPI 함정(dev-platform §4-3)은 부채꼴 쪽에서 이미 닫혀 있다.** 앵커로 `Camera.WorldToScreenPoint`(Unity 픽셀)를 넘기면 단위가 맞는다.
- 네 방향 예약 띠도 이미 배선돼 있다(`EffectiveTop/Bottom/Left/RightMarginPoints`, `ReservedEdgeProbe` 4건).

⇒ **`Expand()`에 다른 좌표를 넘기는 것만으로 부채꼴은 캐릭터에서 열린다.**
**바꿔야 할 것은 인자명·문서·`GearMenuCollapseMode.Drag`뿐이다.**

### 2-2. 죽는 코드 — 정확히 3개

| # | 무엇 | 어디 | 왜 죽나 | 처방 |
|---:|---|---|---|---|
| 1 | **`GearMenuCollapseMode.Drag`** (0.08초 접힘) | 선언 `GearRadialMenuWidget.cs:49` · 유일 사용처 `InfoGearIconWidget.cs:1486` · 스위치 2곳(`ModeLabel`·`CollapseSecondsFor`) | **톱니 드래그가 사라지면 발생원이 0이 된다.** 테스트에서도 `User`만 쓴다(전수 확인) | ★ **`default:`로 흘리지 마라.** enum 멤버를 지우면 `switch` 2곳의 `_ =>` 폴백이 **정상값을 삼킨다.** ⇒ **(가) 멤버를 남기고 「예약됨」 주석** 또는 **(나) 멤버·스위치 분기·주석 3곳을 동시에 제거.** (나)를 택하면 `ActionCommandPopover.cs:32`의 서술도 함께 고쳐야 한다 |
| 2 | **접힘 사유 「톱니를 옮기기 시작」** | `InfoGearIconWidget.cs:1486` | 위와 같음 | 삭제 |
| 3 | **「톱니 재클릭 = 토글 닫기」** | `InfoGearIconWidget.cs:1583,1590` | 톱니가 없으면 재클릭할 것이 없다 | ★ **재작성** — 「우클릭 재입력 = 닫기」로 옮길지, 「좌클릭 바깥 = 닫기」만 남길지는 **설계 판단**(`ux-widgets`) |

### 2-3. 죽지 않지만 **의미가 흔들리는** 것 — 3개 (설계 입력)

| # | 무엇 | 지금 | 캐릭터 앵커에서 |
|---:|---|---|---|
| 1 | `ComputeLayout():1644` `_baseAngleDegrees = Snap45(화면중심 − 앵커)` | 톱니는 **우상단 고정**이라 언제나 225°(좌하향) 근처. 이 값이 §36-3의 **「스팬 90°, 평행이동 평균 0.7pt」 실측의 전제**다 | ★ 캐릭터는 **바닥을 걷는다.** 화면 하단 중앙에서는 90°(위), 좌하단에서는 45°. **모서리 4곳만 검증된 사다리를 앵커 전 영역이 훑게 된다.** ★ 그리고 `Snap45`는 앵커가 **정확히 화면 중심**이면 `sqrMagnitude < 1e-6` → **225° 고정**(`:1628`)이라 아래로 펼친다. 근처에서는 **1픽셀 이동에 스냅 각이 45° 튄다**(열 때마다만 — 프레임 지터는 아니다) |
| 2 | **위성 [앱 종료] 궤도 168pt** (`SatelliteOrbitRadiusPoints:235`) | 우상단에서 좌하향이라 화면 안쪽으로만 뻗음 | 캐릭터가 **바닥에 서 있으면** 위성이 위로 168pt 나간다. `EffectiveTopMarginPoints`(≥40pt)와 상단 예약 띠에 걸리는 구간이 새로 생긴다 → **세로 일렬 폴백 빈도가 오른다**(폴백 자체는 이미 구현돼 있어 **깨지지는 않는다**) |
| 3 | **팝오버 앵커**(`ButtonScreenRect(...)`, `:879/891/906`) | 우상단 근처에서 자란다 | ★ **팝오버 3종(집중/할일/행동)이 캐릭터를 따라다닌다.** 행동창은 큰 창이라 캐릭터가 화면 구석에 있을 때 배치가 새 경로를 탄다. **`PopoverPanel`의 클램프가 이것을 이미 하는지 확인이 필요하다**(이번 라운드 미확인) |

### 2-4. 톱니가 소유한 **차단막**(비침해의 실체) — 그대로 이사해야 한다

```csharp
// InfoGearIconWidget.cs:983-999  (+ 1218-1223에서 BoxCollider2D 생성, isTrigger)
InteractiveScreenRect = _menu != null && _menu.IsVisible
    ? Union(IconScreenRect, _menu.UnionScreenRect)   // 펼침: 톱니 + 버튼 합집합
    : IconScreenRect;                                 // 접힘: 톱니만
```
★ **캐릭터 앵커에서는 첫 항이 필요 없다** — 캐릭터에는 **이미 자기 콜라이더(GrabArea 포함)가 있어**
그 위의 클릭은 원래 우리가 먹는다(dev-platform §2-2). ⇒ **신규 컴포넌트의 차단막은
`_menu.UnionScreenRect` 하나면 충분하고, 접히면 `enabled=false`.**
★★ **접힘 시 0 복귀를 빠뜨리면 화면 한복판에 영구 클릭 흡수 구역이 생긴다 — 원칙 2 정면 위반.**
그리고 톱니와 달리 **그 자리가 화면 구석이 아니라 캐릭터 주변**이라 사고의 체감 피해가 훨씬 크다.

---

## 3. 우클릭 배선의 현재 실체

### 3-1. `TryGetSecondaryButtonPressed` — **구현 3개, 소비자 0명**

전수 grep(양성 대조: 같은 grep이 `TryGetPrimaryButtonPressed` 소비자 6곳을 정상 검출):

| 층 | 파일:줄 | 상태 |
|---|---|---|
| 계약 | `Platform/IGlobalPointerButtonService.cs:48` | ★ 문서가 `Interaction/AppControlDirector.cs`를 가리키는데 **그 기능이 없다 — 계약 문서가 낡았다** |
| macOS | `Platform/MacOS/MacWindowService.cs:2004` | `CGEventSourceButtonState(…, kCGMouseButtonRight)` · 항상 `true` 반환 |
| Windows | `Platform/Windows/Win32WindowService.cs:1527` | `GetAsyncKeyState(VK_RBUTTON)` · 항상 `true` 반환 |
| 데코레이터 | `Platform/FallbackPlatformWindowService.cs:698` | 순수 위임 |
| **소비자** | — | ★ **0명** |

⇒ **채널은 살아 있다. 붙이기만 하면 된다.** (dev-platform `PLATFORM_RIGHTCLICK_FAN.md` §1과 같은 결론)

### 3-2. 좌클릭 경로의 구조 — **우클릭을 얹을 자리는 「옆」이지 「안」이 아니다**

```
StickmanClickHitbox.cs   237줄   [RequireComponent(typeof(Collider2D))]
  event MouseDown / MouseUp                       :46,50
  SimulateMouseDownForTests()                     :62
  Update()  매 프레임 TryGetPrimaryButtonPressed   :132-152
      rising && !_pressed && IsCursorOverHitbox() → BeginPress
      !down && _pressed                           → EndPress   ← 놓기는 「엣지」가 아니라 「현재 상태」
  IsCursorOverHitbox()  private                   :155-180
      _colliders (Awake의 GetComponentsInChildren, 캐시) + _extraColliders (동적 등록)
      판정: blackboard.TryGetCursorWorldPosition → OverlapPoint
  OnMouseDown/OnMouseUp                           :181-213  (전역 폴링이 살아 있으면 Up을 무시)
```
구독자 5개: `DragThrowController`(:45-46) · `RunawayDirector` · `RunawayRenderer` · `ArcheryDirector` · `StressGaugeDirector`.

★ **여기에 두 번째 버튼을 얹지 마라.** `_pressed` **하나**로 두 입력 경로를 엣지 트리거하는 구조라,
우클릭을 같은 플래그에 태우면 **좌클릭 드래그가 조용히 죽는다.** (dev-platform §3-3과 같은 판단.)

**⇒ 신규 컴포넌트가 같은 콜라이더 집합을 「읽기만」 한다.** 두 가지 선택지:

| 안 | 방법 | 비용 | 주의 |
|---|---|---|---|
| **(A) 자체 캐시** | 신규 컴포넌트가 `Awake`에서 `GetComponentsInChildren<Collider2D>(true)` 캐시 + `OverlapPoint` | 20줄 · `StickmanClickHitbox.cs` **0줄 수정** | ★ `_extraColliders`(가출 연출의 과자)를 **못 본다.** 그건 오히려 옳다 — 과자 우클릭에 부채꼴이 뜰 이유가 없다. **의식적 선택임을 주석에 남길 것** |
| (B) 접근자 추가 | `StickmanClickHitbox`에 `public bool IsCursorOverHitbox()` 노출 | 1줄이지만 **그 파일을 건드린다** | 읽기 전용이라 무해하지만, 그 파일은 「한 글자도 바꾸지 않는다」가 이번 라운드의 안전선이다 |

**권고: (A).** 구(舊) 구현이 정확히 그랬다(`git 767c985^:AppControlDirector.cs:339-353 IsCursorOverCharacter()`).
★ 다만 **구 구현은 폴링마다 `GetComponentsInChildren`을 새로 호출했다**(할당 발생) — 24시간 상주 앱이므로
**반드시 `Awake` 캐시로 고쳐서 옮긴다.**

### 3-3. 폐지된 `AppControlDirector` — **재사용할 것이 있다 (git에)**

현재 파일(747줄)에는 우클릭 코드가 **한 줄도 없다**. 그러나 `git 767c985^`(삭제 직전 커밋)에 원문이 그대로 있다:

| 되살릴 것 | 원문 위치 | 상태 |
|---|---|---|
| 우클릭 상승 엣지 폴링(`_rightPrev`/`_rightInitialized`) | `767c985^:AppControlDirector.cs:283-301` | **그대로 쓸 수 있다.** 상승 엣지만 보고 하강 엣지에는 아무 일도 안 한다 |
| `IsCursorOverCharacter()` | `:339-353` | **할당 제거 후** 쓸 수 있다(§3-2) |
| `TryGetCursorUnityScreen()` | `:355-361` | 지금 `InfoGearIconWidget.cs:1660`에 **같은 코드가 살아 있다** — 그쪽에서 옮기는 게 낫다 |
| `PollInterval = 0.05f`(20Hz) | 구 상수 | 톱니의 `ClickPollInterval = 0.05f`(`:333`)와 **같은 값**. 그대로 |
| 메뉴 행 히트테스트 / `MenuAutoCloseSeconds` / `UpdateMenuPlacement` | `:317-336` | ★ **쓰지 마라.** 부채꼴이 이미 `HitTest`·`AutoCollapseIdleSeconds(6초)`·`ComputeLayout`을 갖고 있다 |

**현재 파일에서 재사용할 것**: `Invoke(ControlAction, source)` 디스패치 표와 `ToggleCharacterInfo`/`ToggleSettings`
— 단 이것들은 **부채꼴 버튼이 이미 각자 부르고 있어** 새로 필요하지 않다.
⇒ **`AppControlDirector.cs`에서 실제로 옮길 코드는 0줄이다. 고칠 것은 기동 배너 3줄(문구)뿐이다.**

---

## 4. 변경 규모 추정

### 4-1. 파일별 표

| # | 파일 | 현재 | 예상 변경 | 위험 | 담당 |
|---:|---|---:|---|:---:|---|
| 1 | **`Interaction/(신규) 우클릭 부채꼴 입력 컴포넌트`** | 0 | **+350 ~ +420** (문서 120 포함) | **상** | `coder-ui` |
| 2 | `Interaction/InfoGearIconWidget.cs` | 1,681 | **−1,681**(삭제안) / **+15 −5**(⑥ 권고안) | **상** / 하 | `coder-ui` |
| 3 | `Interaction/GearRadialMenuWidget.cs` | 2,319 | **±40** (인자명 `gearCenter…`→`anchor…`, 주석 5곳, `Drag` 모드 처리) | **중** | `coder-ui` |
| 4 | `Interaction/SettingsWindow.cs` | — | **−55 ~ −60** (행 2 + 캡션 + 게이트 2 + `ResolveGearWidget` + `RefreshAll` 3줄) | **하** | `coder-ui` |
| 5 | `Interaction/TodoPostItWidget.cs` | — | **−45 ~ −50**(회피 로직 삭제 시) / **0**(그대로 두면 `_gear == null` 경로로 안전 동작) | **중** | `coder-ui` |
| 6 | `Editor/SceneBootstrapper.cs` | — | **±10** (`AddComponent` 1줄 교체 + `EnsurePrefabComponents`에 1줄 추가 + 주석) | **중** | `coder-ui` |
| 7 | **`_Project/Prefabs/Stickman.prefab`** | — | ★ **−13줄 / +13줄** — `m_Component` 항목 `:1368` + `MonoBehaviour` 블록 `:1879-1889`. 신규 컴포넌트 블록 추가 | **상** | `coder-ui` |
| 8 | `Core/UiLayoutModel.cs` · `AppSettingsModel.cs` · `CharacterSaveStore.cs` | — | **0줄(권고) ~ ±30(모델 정리 시)** | **상**(세이브) | `coder-systems` |
| 9 | `Core/ItemCatalog.cs:281` | — | **±1** (문구 — `ux-designer` 확정 후) | **하** | `coder-ui` |
| 10 | `Core/StickmanAgent.cs` · `Interaction/AppControlDirector.cs` · `CharacterInfoWindow.cs` · `ActionCommandPopover.cs` · `PopoverPanel.cs` · `UiChrome.cs` | — | **±35** (전부 문구·주석) | **하** | `coder-ui` |
| 11 | `Platform/` 8파일 | — | **±25** (문구·목록 갱신) + dev-platform 신규 인터페이스 | **중** | `dev-platform` |
| 12 | **테스트 15파일(타입 참조)** | — | **−700 ~ −900 / +250** | **상** | `test-engineer`·`qa-regression` |
| 13 | 테스트 4파일(문자열 니들) | — | **±20** | **중** | 위와 같음 |
| 14 | `docs/UX_FLOW.md` §36-9 · §32 · §41-8 · §51-6 | — | **±150** | **중** | `ux-designer`·`ux-widgets` |

**합계 대략**: 프로덕션 **−1,800 / +500** · 테스트 **−800 / +250** · 문서 **±150**.

### 4-2. 위험 순위 — 상위 5개

| 순위 | 위험 | 왜 |
|---:|---|---|
| **1** | ★★ **사용자 명시 숨김 중 마우스 진입점이 0이 된다** | 2026-09-03 사용자 확정의 정면 반전. §3-1·§5의 테스트 2건이 이것을 지킨다 |
| **2** | **차단막의 접힘 복귀 누락** | 화면 **한복판**에 영구 클릭 흡수 구역. 톱니 시절보다 피해가 크다(구석이 아니다) |
| **3** | **`TryGrantUserSummon`을 `Expand()` 뒤에 두는 것** | 등급 1에서 「우클릭이 안 먹는다」. **파일 주석이 이미 그것이 과거 실제 증상이었다고 적어 두었다**(`:1596`) |
| **4** | **테스트 어셈블리 컴파일 실패** | 타입 하나 지우면 **2,530건이 0건.** 러너가 초록도 빨강도 못 낸다 |
| **5** | **`GearMenuCollapseMode.Drag`를 `default:`로 흘리기** | 이 저장소가 반복해 당한 형태. `switch` 2곳이 `_ =>`(=User) 폴백을 갖고 있다 |

### 4-3. 되돌리기 쉬운 순서 — **5단계, 각 단계가 그 자체로 초록이어야 한다**

> ★ **한 커밋에 몰지 마라.** 4-2의 4번 때문에 「중간 상태에서 러너를 못 돌리는」 구간이 생긴다.

| 단계 | 하는 일 | 되돌리기 | 이 단계가 끝나면 |
|---:|---|---|---|
| **S1** | **신규 입력 컴포넌트를 추가한다. 톱니는 그대로 둔다.** 우클릭 → `Expand(캐릭터 앵커)`. 차단막·게이트·임대·호버 전부 신규 쪽에 배선 | 파일 1개 삭제 | **두 진입점이 공존한다.** 실기로 우클릭만 따로 검증 가능. 기존 테스트 **전부 그대로 초록** |
| **S2** | **톱니의 부채꼴 소유권을 끊는다** — `_menu` 필드 제거, `ActivateClick`은 정보창 토글만. 톱니는 「아이콘 + 드래그」만 남는다 | S1로 되돌림 | 부채꼴 입력 경로가 **한 벌**이 된다. 여기서 `InfoGearRadialMenuTests` 14건 · `GearMenuHoverLabelTests` 8건이 **신규 컴포넌트를 겨누도록 이전**된다 |
| **S3** | ★ **톱니를 평상시 화면에서 걷는다.** ⑥의 (B′) = `InfoGearIconWidget.cs:822` 조건식 1줄. **세이브·모델은 손대지 않는다** | 조건식 1줄 | ★ **사용자가 보는 결과는 「없앴다」와 동일하다.** 여기서 실기 판정을 받는다. 깨지는 테스트는 §6에 열거(3건) |
| **S4** | **설정 행 2개 · 포스트잇 회피 · 문구 7곳**을 정리 | 커밋 되돌리기 | 화면에서 톱니의 흔적이 사라진다 |
| **S5** | **파일·테스트 15개·프리팹 블록을 한 커밋에 삭제** | 커밋 되돌리기 | ★ **이 단계만이 컴파일 원자성을 요구한다.** 리더 판정 후 별도 라운드 권고 |

★ **S3에서 멈추는 선택지가 실재한다.** ⑥이 그것을 권고한다.

---

## 5. 깨질 테스트 — 예측

### 5-1. 즉시 **컴파일이 깨지는** 테스트 (타입 참조) — 15파일

> ★ **어셈블리 단위로 죽는다.** 파일 하나만 깨져도 그 어셈블리의 모든 테스트가 실행되지 않는다.

| 어셈블리 | 파일 | 그 파일의 테스트 수 | 무엇을 재고 있었나 |
|---|---|---:|---|
| **EditMode**(1,890건 전체가 위험) | `InfoGearHaloContrastTests.cs` | 6 | 톱니 헤일로의 배경 무관 대비 3:1 보장 |
| | `SurfaceSafeAreaPolicyTests.cs` | 21 | ★ `교정_톱니_히트_반지름은_프로덕션_유도와_정확히_같다`(핀 19.82pt) — **이 파일의 다른 검사 전부가 이 교정에 종속** |
| | `ReservedScreenEdgeContractTests.cs` | 28 | 우측 도킹 작업표시줄에서 톱니가 밀려나는가(6곳이 `HitRadiusPoints` 사용) |
| **PlayMode**(640건 전체가 위험) | `InfoGearDragTests.cs` | 11 | 길게 눌러 옮기기 · 저장 · 화면 밖 클램프 |
| | `InfoGearRadialMenuTests.cs` | 14 | ★ 부채꼴 전 기능(펼침·바깥 클릭·모서리 배치·[앱 종료] 2단 확인·차단막) |
| | `GearMenuHoverLabelTests.cs` | 8 | 호버 이름표 8종 |
| | `FullscreenPanelRetreatTests.cs` | 6 | 등급 1 탈출구(`등급1에서_톱니를_누르면_설정창까지_도달한다`) |
| | `ManualHideUserAxisTests.cs` | 6 | ★★ `축분리_톱니는_사용자숨김에서_남고_전체화면_게임에서는_사라진다` · `같은_토글을_다시_누르면_캐릭터가_돌아오고_톱니는_내내_남는다` |
| | `TodoPostItReservedTopBarTests.cs` | 8 | 발산 구간에서 카드가 톱니를 덮지 않는가 |
| | `TodoPostItGearAvoidanceTests.cs` | 3 | 카드의 톱니 회피 |
| | `InfoWindowExclusiveModalTests.cs` | 5 | `GearClickWhileWindowIsOpenClosesItWithoutExpandingTheFan` |
| | `InfoGearPositionOwnershipTests.cs` | 4 | 온보딩 대여 ↔ 사용자 위치의 소유권 |
| | `InfoGearMeshingTests.cs` | 4 | ★ 이미 3건이 `Assert.Ignore`(맞물린 두 기어 폐지) |
| | `GearMenuOnboardingHintTests.cs` | 3 | 최초 1회 안내 |
| | `FullscreenSuspendUiHidingTests.cs` | 2 | 전체화면 감지에서 표면+차단막 전수 회수 |

**직접 관련 테스트 합계 ≈ 129건** / **실행 불가가 되는 총계 2,530건**.

### 5-2. 파일명·문자열 니들을 쓰는 **감사** — 이쪽이 더 조용하고 더 위험하다

| # | 감사 | 니들 | 톱니를 지우면 |
|---:|---|---|---|
| **A1** | `CommentReferenceAuditTests.주석이_지목한_소스_파일이_새로_사라지지_않는다` | 프로덕션·테스트 주석의 **모든 `*.cs` 참조**가 실재하는지 | ★ **`InfoGearIconWidget.cs` 참조 14건이 전부 위반이 된다.** `KnownBroken`에 넣거나 **14곳을 다 고쳐야** 한다 |
| **A2** | 같은 파일 `줄번호_참조는_썩지_않았는지_앵커로_확인한다` | ★ `("InfoGearIconWidget.cs:51", …, 51, "hitTestType=Raycast")` | ★★ **톱니 파일의 51행 위에 한 줄만 끼어들어도 빨개진다.** ⑥의 「파일을 남긴다」 안을 택해도 **51행 위를 건드리면 안 된다** |
| **A3** | `PlatformParityAuditTests` (하단 인셋 소비 배선) | `mustConsume = { PopoverPanel.cs, CharacterInfoWindow.Layout.cs, InfoGearIconWidget.cs }` → 각각 `EnforcedBottomInsetPoints(` 포함 요구 | `ReadSource`가 **파일 없음 예외**로 죽는다 |
| **A4** | `ReservedScreenEdgeContractTests` (상단 프로브 소비자 수) | ★ `Assert.AreEqual(5, 상단프로브_소비자.Count)` | ★ **5 → 4가 되어 실패.** 그리고 `ReservedTopBarProbe.cs:13` · `IReservedScreenEdgeService.cs:148` · `MacReservedTopBarService.cs:60`의 **「다섯 파일」 목록도 함께 고쳐야 한다**(감사 메시지가 그렇게 지시한다) |
| **A5** | 같은 파일 (네 방향 소비자) | `nameof(InfoGearIconWidget) + ".cs"` **필수 목록** | ★ `nameof`라 **타입을 지우면 컴파일이 깨진다**(조용한 통과는 아니다) |
| **A6** | `ManualHideAxisSeparationAuditTests.톱니는_사용자_명시_숨김에서_살아남는다` | `ReadSource("Interaction","InfoGearIconWidget.cs")` + `.HidesScreenSurfaces` 존재 / `.IsSuspended`·`.ArePanelsSuppressed` **부재** | 파일 없음 예외. ★ **이 검사가 지키던 사용자 확정 자체를 재배치해야 한다** |
| **A7** | ★★ `SuspendClickBlockerAuditTests` | `Interaction/` 전수 스캔 → 차단막 소유자 **`Assert.GreaterOrEqual(owners.Count, 5)`** | ★★ **현재 소유자가 정확히 5개다**(`CharacterInfoWindow`·`InfoGearIconWidget`·`SettingsWindow`·`TodoPostItWidget`·`PopoverPanel`). **톱니를 지우고 대체 차단막을 안 만들면 4가 되어 실패한다.** 신규 컴포넌트가 차단막을 가지면 5가 유지되고, **동시에 그 컴포넌트가 전체화면 축을 읽도록 강제된다 — 이 감사는 우리 편이다** |
| **A8** | `TestClaimExpiryAuditTests` R2-8 | `ProductionFilesWithStem("InfoGearIconWidget")` + `BuildGearOutline` 개수 | 목록 0 → *"이름이 바뀌었다면 이 항목도 함께 고쳐라"* 메시지로 실패 |
| **A9** | `UiInteractionFramePacingHoldTests.상호작용_표면_명부가_빠짐없이_배선돼_있다` | 명부 4개 + `Assert.AreEqual(4, surfaces.Length)` | ★ **톱니는 명부에 없다.** 영향 **없음** |

> ★★ **정정 — `dev-platform` 문서 §4-2의 인용 하나가 틀렸다.**
> 그 문서는 *"`FramePacing.HoldActiveForInteraction()`을 … 톱니가 그렇게 한다(`InfoGearIconWidget.cs:1045`)"*라고 적었다.
> **실측: `InfoGearIconWidget.cs`에 `HoldActiveForInteraction` 호출은 0건이다.** 그 호출은
> **`GearRadialMenuWidget.cs:1045`**에 있다(파일명이 뒤바뀐 인용).
> ⇒ **좋은 소식이다**: 프레임 페이싱 홀드는 **부채꼴이 자기 `LateUpdate`에서** 스스로 하므로
> **톱니 제거로 잃지 않고, 신규 컴포넌트에 홀드를 새로 배선할 필요도 없다.**
> `UiInteractionFramePacingHoldTests`의 4개 명부도 **손댈 필요가 없다**(위 A9).
> `StillTierCompositorBudgetTests.cs:423`의 니들은 **커서추종 펫**용이고 부채꼴과 무관하다.

### 5-3. 세이브·모델 테스트 — **건드리지 않으면 안 깨진다**

`UiLayoutPersistenceTests`(9곳) · `InkColorPersistenceTests` · `EquipmentMigrationTests`(9곳, 문맥상 무관)
— **§1-8의 「필드를 남긴다」를 지키면 그대로 초록.**

★ **단 하나 예외가 있다 — `AppSettingsModelContractTests`.**
이 파일은 **`GearIconVisible`의 기본값이 `true`임을 3곳에서 단언**한다:
```
:59   Assert.IsTrue(AppSettingsModel.GearIconVisible, "톱니 아이콘의 기본은 보임이어야 한다.");
:147  Assert.IsTrue(AppSettingsModel.GearIconVisible);            // 구버전 세이브 복원 경로
:194  AppSettingsModel.SetGearIconVisible(true);   // 이미 true — 변화 없음.
```
⇒ **`GearIconVisible`의 기본값을 뒤집는 안(⑥의 (B))을 택하면 이 3곳이 깨진다.**
⑥이 권고하는 (B′)는 이 값을 건드리지 않으므로 **이 파일은 그대로 초록이다.**

---

## 6. 톱니 파일을 지울 것인가 — **권고: 이번 라운드에는 지우지 않는다**

사용자 문장은 **"없애고"**다. 그 말이 요구하는 것은 **화면에서 사라지는 것**이고, 그것을 만드는 방법이 둘이다.

### ★ 먼저, 내 초안이 틀렸던 것을 정정한다 (보고 전 재확인에서 잡았다)

초안에 *"(B)는 `AppSettingsModel.cs:84`의 `= true` → `= false` **한 곳**"*이라고 적었다. **거짓이다.**

| 실측 | 결과 |
|---|---|
| 기본값 선언 자리 | **3곳** — `AppSettingsModel.cs:84`(필드) · `:375`(`ResetForTesting`) · `CharacterSaveStore.cs:647`(구버전 세이브 폴백 `hasAppSettings ? data.gearIconVisible : true`) |
| ★★ **기존 사용자** | `FirstVersionWithAppSettings = 8` 이상 세이브에는 **`gearIconVisible: true`가 이미 기록돼 있다.** ⇒ **코드 기본값을 뒤집어도 이미 한 번이라도 앱을 켠 사용자에게는 톱니가 그대로 뜬다.** 없애려면 **스키마 v10 → v11 + 마이그레이션 + 하위 호환 테스트 1건**(CLAUDE.md 의무)이 필요하다 |
| 테스트 | `AppSettingsModelContractTests`가 **`GearIconVisible == true`를 3곳에서 단언**한다(`:59`, `:147`, `:194` 주석 *"이미 true — 변화 없음"*). 초안의 *"전부 그대로 초록"*은 **거짓이었다** |

⇒ **「기본값 뒤집기」는 값싸지 않다.** 그래서 형태를 바꾼다.

### 세 안 비교

| | (A) 파일 삭제 | (B) 기본값 OFF | ★ **(B′) 게이트 조건 교체** |
|---|---|---|---|
| 무엇을 바꾸나 | 전부 | `AppSettingsModel`/`CharacterSaveStore` 기본값 3곳 + 스키마 v11 | **`InfoGearIconWidget.cs:822`의 조건식 1줄** |
| 평상시 톱니 | 없음 | 없음(**신규 설치만** — 기존 사용자는 그대로 뜬다) | **없음 (전원)** |
| 숨김 중 진입점 | **0** | **0** | ★ **톱니가 그때만 나타난다 — 유지** |
| 세이브 스키마 | 손댐 | ★ **v10 → v11 + 마이그레이션 + 호환 테스트** | ★ **무변경** |
| `AppSettingsModelContractTests` | 컴파일 불가 | ★ **3건 실패** | ★ **그대로 초록** |
| 되돌리기 | 커밋(대형) | 커밋 + 스키마 되돌리기(어렵다) | ★ **조건식 1줄** |
| 죽은 코드 | 없음 | 1,681줄 | 1,681줄(단, **조건부로 살아 있다**) |

```csharp
// InfoGearIconWidget.cs:822 — 지금
if (_agent.HidesScreenSurfaces || !AppSettingsModel.GearIconVisible)
// (B′) 권고
if (_agent.HidesScreenSurfaces || !(AppSettingsModel.GearIconVisible && _agent.IsUserHiddenOnly))
```
= **평소에는 없고, 사용자가 캐릭터를 숨긴 동안에만 나타난다.**
2026-09-05 지시(*"스크류모양이 따로 있는데 … 없애고"*)와
2026-09-03 확정(*"메뉴버튼은 보여야지 / 캐릭만 가리고"*)을 **둘 다 글자 그대로** 지킨다.
신규 표면 0개 · 신규 세이브 필드 0개 · 스키마 무변경.

**(B′)가 건드리는 테스트**(초안보다 정직하게):
- `ManualHideUserAxisTests.축분리_톱니는_사용자숨김에서_남고_전체화면_게임에서는_사라진다` → **여전히 통과한다**(숨김 중 톱니는 남는다).
- `…같은_토글을_다시_누르면_캐릭터가_돌아오고_톱니는_내내_남는다` → ★ **실패한다**("내내"가 거짓이 된다). 문장과 단언을 함께 고쳐야 한다.
- `FullscreenPanelRetreatTests.등급1은…캐릭터와_톱니는_남긴다` / `등급1에서_톱니를_누르면_설정창까지_도달한다` → ★ **실패한다.** 등급 1에서는 캐릭터가 남으므로 **우클릭으로 대체되고, 이 2건은 신규 컴포넌트를 겨누도록 이전**한다.
- `SettingsWindow`의 토글 라벨·캡션은 뜻이 바뀌므로 **`ux-designer` 문구 필요**(§1-9, L-U6에 합침).

**⇒ (B′)를 권고한다. 근거 4개:**

1. ★ **사용자 체감(「평상시 톱니 없음」)은 (A)와 같은데 되돌리는 비용이 1,681줄 vs 1줄이다.** 이 저장소는
   *"창 바깥 클릭으로 닫기"*를 **없앴다가 부채꼴에는 남긴** 전례가 있다(`InfoGearIconWidget.cs:1416-1424`).
   **새 조작이 실기에서 어떻게 읽히는지는 아직 아무도 모른다.**
2. ★★ **§3-1의 탈출구 문제가 삭제로는 해결되지 않는다.** 사용자 명시 숨김 중에는 캐릭터가 없어
   우클릭할 대상이 없고, macOS에는 **되돌릴 OS 표면이 0개다**(dev-platform §6-1: Dock·⌘Tab·메뉴바 전부 없음).
   ⇒ 이 문제를 푸는 가장 싼 방법이 **「숨김 중에만 톱니가 뜬다」**인데, 그러려면 **파일이 살아 있어야 한다.**
3. **삭제는 되돌릴 수 없는 결정에 가깝다** — 감사 6종·테스트 15파일·프리팹 YAML을 동시에 고치고 나면
   되돌리는 커밋이 그만큼 커진다. `game-architect`의 「되돌릴 수 없는 결정 식별」 대상이다.
4. **S5는 언제든 나중에 할 수 있다.** 반대는 성립하지 않는다.
   그리고 (B′)는 **세이브 스키마를 한 비트도 건드리지 않는다** — (A)와 (B)는 둘 다 건드린다.

### ★ 어느 안을 고르든 「상시 톱니」는 이번 라운드에 확실히 죽는다

(B′)에서도 **평상시 화면에 톱니는 없다.** 남는 것은 **코드**이고, 그 코드가 §3-1의 탈출구를 위한
**조건부 표면**으로 재활용된다는 것이 (B′)의 값이다.
★ **조건부 톱니의 형태·문구·발견가능성은 `ux-widgets`/`ux-designer`가 정한다** —
`coder-ui`가 제시하는 것은 **「그 자리가 코드로 이미 있다」는 사실**뿐이다.

---

## 7. 리더 판단 필요 항목

> dev-platform의 `PLATFORM_RIGHTCLICK_FAN.md` §8이 L-1~L-7을 이미 올렸다. **중복을 피해 UI 표면 쪽만 적는다.**

| # | 항목 | 정해야 하는 것 | 정하지 않으면 |
|---|---|---|---|
| **L-U1** ★★ | **사용자 명시 숨김(⌃⌥⌘K) 중의 마우스 진입점** | (가) ⑥의 조건부 톱니 / (나) macOS `NSStatusItem` 선행(비용 상) / (다) 단축키만 남기고 2026-09-03 확정을 되돌린다(사용자 재확인 필요) | **구현이 시작될 수 없다.** 이것이 톱니 코드를 지울지 남길지를 결정한다 |
| **L-U2** | **톱니 파일: (A) 삭제 / (B) 기본값 OFF / (B′) 게이트 조건 교체** | ⑥. `coder-ui` 의견은 **(B′)** — 세이브 스키마를 안 건드리는 유일한 안이다 | S5 유무가 정해지지 않아 테스트 15파일의 운명이 미정 |
| **L-U3** | **`GearMenuCollapseMode.Drag` 처리** | (가) 「예약됨」으로 남긴다 / (나) enum·`switch` 2곳·주석을 동시에 제거 | ★ 어중간하게 두면 **`default:`가 정상값을 삼킨다** |
| **L-U4** | **UX_FLOW §35-2/§51-6 온보딩 설계의 앵커** | 톱니를 주인공으로 하는 미구현 설계 100여 줄이 앵커를 잃는다. 폐기인가 캐릭터 기준 재설계인가 | 다음 사람이 없는 톱니를 향해 구현한다 |
| **L-U5** | **포스트잇 톱니 회피(51-9-3) 코드 처리** | 톱니가 안 뜨면 무의미. (가) 삭제 / (나) `_gear == null` 안전 경로에 맡기고 방치 | 죽은 45줄이 「왜 있는지 모를 코드」로 남는다 |
| **L-U6** | **`ItemCatalog.MenuOnlyStatus` 새 문구** | `ux-designer`·`design-narrative` 배정. 폭 예산은 §1-9에 계산해 두었다(96pt 슬롯, 10.5pt/글자) | `coder-ui`가 문구를 지어내게 된다 — **정의서가 금지한다** |
| **L-U7** | **`docs/UX_FLOW.md` §36-9 재작성 주체** | 근거 1(지시 위반)은 신규 지시가 덮었고, **근거 2는 사실이 아니며**(dev-platform §2-2), 「감수하는 비용」의 상쇄 근거가 *"기어가 상시 보이는 유일한 진입점"*이라 **통째로 무너진다** | 설계 정본이 거짓인 채로 남는다 |
| **L-U8** | **파일 소유 배분** | `Interaction/`(신규 컴포넌트·부채꼴·설정창·포스트잇) = `coder-ui` / `Core/`(세이브·모델·`ItemCatalog`) = `coder-systems` / `Platform/`(신규 인터페이스·문구) = `dev-platform` / 프리팹 YAML = **한 사람만**(동시 편집 시 병합 불가) | 2026-09-02의 「겹친 채로 돌다 남의 작업 파일을 커밋」 재발 |
| **L-U9** | **부채꼴 앵커의 기하 재검증을 요구할 것인가** | §2-3. 지금의 「스팬 90° / 평행이동 평균 0.7pt」 실측은 **모서리 4곳 전제**다. 캐릭터 앵커 전 영역 격자 재계산을 `ux-widgets`에 요구할 것인가 | Windows·저해상도에서 세로 일렬 폴백이 기본 화면이 될 수 있다 |

---

## 부록. 이 라운드가 실행하지 않은 것 (정직하게)

- **프로덕션 `.cs`를 0줄 고쳤다.** 따라서 크로스컴파일(`xcheck.sh win`/`osx`)을 **돌리지 않았다** —
  잴 변경이 없고, 지금 트리는 `coder`가 `AccessoryShapeBuilder*.cs`·`CharacterInfoWindow.Cards.cs`·
  `Tools/CardShapeGen/`·`Items/*.asset`을 **작업 중**이라 컴파일을 재면 **남의 진행 상태를 재게 된다.**
  ★ **구현 라운드는 반드시 격리 미러 + `xcheck_isolated.sh osx|win` 양쪽 0에러 + 타깃 교차 대조를 붙일 것.**
- **Unity 배치모드를 돌리지 않았다**(리더 승인 없이 `Library/` 락을 잡지 않는다). §5의 「깨질 테스트」는
  **소스 분석에 의한 예측**이지 실행 결과가 아니다.
- **앱을 띄우지 않았다.** 실기 확인 0건.
- **`Tasklist.md`를 수정하지 않았다.** 등재 문장은 완료 보고에 있다.
- **금지된 파일을 열지 않았다** — `AccessoryShapeBuilder*.cs` · `CharacterInfoWindow.Cards.cs` ·
  `Tools/CardShapeGen/` · `Items/*.asset`. (`ItemCatalog.cs`는 금지 목록에 없어 **읽기만** 했다.)

### ★ 이 문서가 스스로 잡은 거짓 주장 1건 (보고 전 재확인에서)

초안 §6은 *"(B) 기본값 OFF는 `AppSettingsModel.cs:84` **한 곳**이고 **테스트는 전부 그대로 초록**"*이라고 적었다.
**둘 다 거짓이었다.** 재확인에서 잡은 실측:

| 초안 | 실측 |
|---|---|
| 변경 「한 곳」 | ★ **3곳** — `AppSettingsModel.cs:84`·`:375` · `CharacterSaveStore.cs:647` |
| *"테스트 전부 초록"* | ★ **`AppSettingsModelContractTests` 3곳이 `GearIconVisible == true`를 단언한다**(`:59`·`:147`·`:194`) |
| (초안에 없던 것) | ★★ **기존 사용자 세이브에 `gearIconVisible: true`가 이미 앉아 있어, 코드 기본값을 뒤집어도 톱니가 그대로 뜬다.** 없애려면 **스키마 v10→v11 + 마이그레이션 + 하위 호환 테스트**가 필요하다 |

⇒ 그래서 권고가 **(B) → (B′)**로 바뀌었다. **틀린 주장을 지우지 않고 여기 남긴다** —
「기본값 하나만 뒤집으면 된다」는 직관은 다음 사람도 똑같이 할 것이고, 그때 이 표가 그것을 막는다.

### 이 문서가 쓴 프로브의 양성 대조

| 주장 | 프로브 | 양성 대조 |
|---|---|---|
| `StickConfig.cs`에 톱니 필드 0개 | `grep -i -E 'gear\|톱니\|radial\|부채꼴\|fan'` | **같은 파일에서 `throwTumbleEnabled`를 `:2400`으로 정상 검출** |
| `TryGetSecondaryButtonPressed` 소비자 0명 | 전수 `grep` | **같은 grep이 `IGlobalPointerButtonService` 소비자 6곳을 정상 검출** |
| 온보딩 프로덕션 호출자 0명 | `grep BeginOnboardingPlacement` | **같은 grep이 테스트 8건을 정상 검출** |
| 프리팹에 톱니 컴포넌트 1개 | guid `847e0e73…` | **`GearRadialMenuWidget` guid `79552553…`도 1건으로 정상 검출** |
| 차단막 소유자 5개 | `Interaction/` 전수 | **감사 자신의 하한(5)과 일치**(독립 확인) |
| `zsh`가 `--include`를 죽이는 함정 | 첫 시도가 `no matches found`로 **소리 내며 실패** → 따옴표로 재실행 | CLAUDE.md 기록된 형태 |
