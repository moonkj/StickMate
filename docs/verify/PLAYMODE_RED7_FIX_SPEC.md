# PlayMode 빨강 7건 수정 명세 (test-engineer, 2026-09-14 · 2026-09-15 갱신: 간헐 #8 추가 · #4 간헐 확정 · 분류 규칙 · 간헐 #9 추가 · 간헐 #10 추가 · 관련 위험 W1)

- ★ 2026-09-15 #9 실행(dev-platform 5-d 러너): `scratchpad/dp5d/xml/P1.xml` — test-run `result="Failed(Child)"`, 802 = testcasecount 802 / 통과 770 / 실패 5 / 건너뜀 25 / 판정 불가 2, `site`가 SetUp·TearDown인 스위트 0, 활성 타깃 macOS(러너의 typedef 대조 — 양성·음성 대조 통과). 격리 ×3은 `scratchpad/dp5d/xml/ISO_EdgeHopDown_{1,2,3}.xml`(각 total 7 = testcasecount 7, `Passed`). 네 실행 모두 같은 트리 해시(러너 `run.txt`의 `myfiles`)다. 이 문서가 파이썬으로 다시 대조했다(없는 이름 `ABSENT`, 같은 픽스처의 다른 이름 적중).
- ★ 이 문서의 `scratchpad/dp5d/xml/…` 경로(6곳)는 **세션 임시 경로(보존 안 됨)**다. 세션이 끝나면 사라지므로, 재판독 근거로 인용하기 전에 실재를 먼저 확인한다.
- ★ 2026-09-15 #10 실행(verify-change 5-d 독립 전량): `Logs/vc-5d/P.xml` — test-run `result="Failed(Child)"`, 802 = testcasecount 802 / 통과 770 / 실패 5 / 건너뜀 25 / 판정 불가 2, `site`가 SetUp·TearDown인 스위트 0. 격리 ×3은 `Logs/vc-5d/ISO_DragStruggle_{1,2,3}.xml`(각 total 5 = testcasecount 5, `Passed`). 파이썬으로 다시 대조했다(없는 이름 `ABSENT`). §13·§14의 기준 코드는 HEAD `e899fb8`이고, 인용 파일은 트리 수정 0이다.
- ★ 2026-09-15 갱신 실행: `Logs/vc-e4/P-full.xml` — E-4 커밋 `e6b14c2` 검증 전량(verify-change). test-run `result="Failed(Child)"`, 802 = testcasecount 802 / 통과 769 / 실패 6 / 건너뜀 25 / 판정 불가 2, `site`가 SetUp·TearDown인 스위트 0. 격리 ×3은 `Logs/vc-e4/iso-CostumeFocusStillTierTests-{1,2,3}.xml`, `Logs/vc-e4/iso-PetBalloonClearsBodyTests-{1,2,3}.xml`(각 total 2 = testcasecount 2).
- 대상 실행(첫 판): `Logs/coder-onbstore/play-full.xml` — test-run `result="Failed(Child)"`, 783 / 통과 750 / 실패 5 / 건너뜀 26 / 판정 불가 2, macOS 배치모드, 화면 640×480, 그래픽 API Null.
- 입력: `Tasklist.md` 「[qa-regression] TearDown 거짓 통과 전수 재판독 + PlayMode 실패 원인 조사」, 그 아래 「리더 판정」 ③④. qa의 이력 스캔은 `scratchpad/qa-sfx/hist.out`.
- 기준 코드: HEAD `7900ad0` + 트리. 아래에 인용한 파일은 `git status` 수정 목록에 하나도 없다. 즉 인용한 줄 번호는 HEAD와 같다.
- 이 문서가 한 일: 코드 읽기와 산술만 했다. **Unity 0회, 트리 편집 0회.** 줄 인용은 전부 `awk`로 줄 번호를 붙여 원문에서 옮겼다.
- 판정 등급
  - **사실(코드)**: 인용한 줄로 기전이 닫힌다.
  - **추정**: 수치 또는 순서를 실측하지 않았다.
  - **문서 근거**: 디자인 문서만 주장하고 자동 검사는 없다.
- 2026-09-15 갱신분(§분류 규칙, §4-7, §11)의 기준 코드는 HEAD `e6b14c2`다. 인용 파일 8개(`CostumeFocusStillTierTests.cs`·`PetBalloonClearsBodyTests.cs`·`CostumeFocusRig.cs`·`StickmanBlackboard.cs`·`StickmanPoseAnimator.cs`·`FramePacing.cs`·`CostumeKeyposeTableSO.cs`·`CharacterPetRenderer.cs`)는 `git diff --stat 7900ad0 e6b14c2`에서 변화 0이고 트리 수정도 0이다(양성 대조: 같은 명령이 새 파일 `PanelsOnlyTierMouseEntryTests.cs` +898을 냈다). ⇒ §4의 줄 번호는 `e6b14c2`에서도 유효하다. §1~3·5~7의 인용 파일은 이번에 다시 대조하지 않았다.
- §12(#9)의 기준 코드는 HEAD `5912e33`(브리프 시점 `e4b9931` 뒤에 문서 커밋 1개가 더 들어왔다) + 트리다. 인용 파일 7개(`EdgeHopDownTests.cs`·`AutoWanderController.cs`·`ParkourClimbState.cs`·`StickmanBlackboard.cs`·`StickConfig.cs`·`DefaultStickConfig.asset`·`ProjectSettings/TimeManager.asset`)는 `git diff --stat e6b14c2 HEAD`에서 변화 0이고 `git status`에서도 0이다(양성 대조: 같은 diff 명령이 `docs`에서 42개 파일을 냈고, 같은 status 명령이 5-d의 `Platform/AppShutdownSequence.cs`에서 `M`을 냈다). ⇒ P1 실행 트리와 §12 줄 번호가 같다.

---

## 분류 규칙 · 알려진 목록 (2026-09-15 현행화)

### 분류 규칙
1. **보호 편입 금지.** `PanelsOnlyTierMouseEntryTests`의 아래 3건은 E-4 변이 V4(게이트 앞 옛 억제 `return`)를 PlayMode에서 잡는 유일한 방어다. **알려진 빨강·판정 불가·간헐 어느 목록에도 넣지 않는다.** 빨개지면 그 커밋의 회귀로 다룬다.
   - `B3_B4_등급1_무허가에서_캐릭터_우클릭으로_부채꼴_정보창_설정창까지_네_홉이_열린다`
   - `B10_등급1_더하기_사용자_숨김_중_보이지_않는_몸_자리_우클릭은_부채꼴도_허가도_내지_않는다`
   - `항별_입력값_B1_B7_B9_B10_B11에서_게이트가_어느_항으로_닫혔는지`
   - `P-full.xml`에서 3건 모두 `Passed`다(V4를 잡는다는 판정은 verify-change 변이 실측을 인용한 것이고, 이 문서가 다시 재지 않았다).
2. **새 빨강 분류 관례(리더 결정).** 커밋 게이트에서 「새로 빨개진 이름」은 ① **격리 ×3**으로 간헐과 재현을 가르고 ② 바뀐 코드 경로가 **그 테스트에서 실행됐는지 로그로 확인한 뒤에야** 분류한다.
   - 사례: #8은 E-4 검증 전량에서 처음 빨개졌다. 격리 ×3에서 1/3만 빨강이었고 E-4 코드 경로는 실행되지 않았다(verify-change 로그 확인) ⇒ 「간헐 — E-4 무관」.
   - 이 문서가 다른 자로 보강했다: `git show --name-only e6b14c2`(14개 파일)에 #8·#4 기전 파일이 0건이다(§4-7, §11-4).
   - 사례 2: #9는 5-d 트리 전량(P1)에서 처음 빨개졌다. 격리 ×3은 3/3 초록이었고, dev-platform이 5-d 코드 경로가 실행되지 않았음을 로그로 확인했다 ⇒ 「간헐 — 5-d 무관」. 이 문서가 다른 자로 보강했다: 5-d 트리 변경 6파일(프로덕션은 `Platform/AppShutdownSequence.cs`·`Platform/ReservedBarRestoreLedger.cs` 둘뿐, 나머지 4개는 EditMode 테스트)에 §12 기전 파일이 0건이고, 같은 트리 해시로 격리 3/3이 초록이었다(§12-2).
   - 사례 3: #10은 verify-change 5-d 독립 전량(`Logs/vc-5d/P.xml`)에서 빨개졌다. 격리 ×3은 5/5 초록이었다.
     - 5-d 무관 근거 ① `git show --name-only e899fb8`에 `DragThrowState.cs`·`StickmanAgent.cs`·`DragStruggleTests.cs`가 0건이다(양성 대조: 같은 목록에 `SessionEndShutdownTests.cs` 1건).
     - 근거 ② 테스트 파일의 5-d 타입(`ReservedBar*`·`AppShutdown*`·`SessionExit*`·`SessionEnd*`) 참조가 0이다(양성 대조: `StickmanBlackboard` 4).
     - 근거 ③ 이 테스트 로그 창(`P.log:7915-7922`)에 `[작업표시줄]`·`[종료순서]`가 0이다. ★ 다만 두 태그는 **전체 로그에도 0**이라, 로그 0만으로는 「미실행」과 「이 실행에 태그가 안 나옴」을 가르지 못한다(§9 자백 24). 판정은 ①②로 한다.
     - 같은 모양의 빨강이 과거에도 있었다(09-01·09-08) ⇒ 「간헐 — 기존 재발, 5-d 무관」.

### 알려진 목록 — `Logs/vc-e4/P-full.xml` 기준 (+ #9는 `scratchpad/dp5d/xml/P1.xml`, #10은 `Logs/vc-5d/P.xml`)
이름은 xml `fullname`에서 파이썬으로 대조했다(`StickMate.Tests.PlayMode.` 뒤). 아래 10개 이름은 전부 적중했다. 음성 대조로 넣은 없는 이름은 `ABSENT`로 나왔다.

| 분류 | # | 이름 | 절 |
|---|---|---|---|
| 알려진 빨강 | 1 | `PortraitEyeVisibilityTests.EyesAreAbsentUnderEveryGlassesItem` | §1 |
| 알려진 빨강 | 3 | `CostumeFocusPropLifecycleTests.몰입기_도중_취소해도_프롭이_화면에_남지_않는다` | §3 |
| 알려진 빨강 | 5 | `SettingsWindowReturnPathTests.ClosingSettingsReopensTheInfoWindowItReplaced` | §5·6 |
| 알려진 빨강 | 7 | `AccessoryFillRenderingTests.왕관은_채워지되_얹는_물건으로_남는다` | §7 |
| **간헐** | 4 | `PetBalloonClearsBodyTests.실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다` | §4, §4-7 |
| **간헐** | 8 | `CostumeFocusStillTierTests.몰입기_동안_절감등급_Still에_도달한다` | §11 |
| **간헐** | 9 | `EdgeHopDownTests.NegativeControl_WithoutPostClimbCooldown_LeavesDockAlmostImmediately` | §12 |
| **간헐** | 10 | `DragStruggleTests.StruggleDoesNotBreakCursorStickiness` | §13 |
| 판정 불가 | 2 | `TodoBoardDateNavigationTests.ClickingACalendarCellPicksTheDayInsteadOfDraggingTheWindow` | §2 |
| 판정 불가 | 6 | `SettingsWindowReturnPathTests.ClickingOutsideSettingsNeitherClosesItNorReturnsTheInfoWindow` | §5·6 |

- 합계(`P-full`): 실패 6(알려진 빨강 4 + 간헐 #4·#8)이 xml `failed="6"`과 같고, 판정 불가 2가 `inconclusive="2"`와 같다. #9는 `P-full`에서 `Passed`였다.
- 합계(`P1`, 5-d 트리): 실패 5(알려진 빨강 #1·#3·#5·#7 + 간헐 #9)가 xml `failed="5"`와 같고, 판정 불가 2(#2·#6)가 `inconclusive="2"`와 같다. 간헐 #4·#8은 `Passed`였다. 목록 밖 이름은 0건이다(파이썬으로 `Failed`·`Inconclusive` 전수 추출).
- 합계(`vc-5d/P`, `e899fb8` 전량): 실패 5(알려진 빨강 #1·#3·#5·#7 + 간헐 #10)가 xml `failed="5"`와 같고, 판정 불가 2(#2·#6)가 `inconclusive="2"`와 같다. 간헐 #4·#8·#9는 실패·판정 불가 목록에 없다. 목록 밖 이름은 0건이다(파이썬 전수 추출).
- **간헐 4건은 서로 다른 실행에서 번갈아 빨개진다.** #4·#8은 `P-full`에서 빨강 → `P1`에서 초록, #9는 그 반대다. #10은 `vc-5d/P`에서만 빨갛고 `P-full`·`P1`에서는 실패 목록에 없었다. 어느 전량에서 초록이 나와도 「고쳐졌다」로 읽지 않는다.

---

## 0. 요약 표

| # | 테스트 | 판정 | 틀린 쪽 | 러너 판별 | 담당 |
|---|---|---|---|---|---|
| 1 | `PortraitEyeVisibilityTests.EyesAreAbsentUnderEveryGlassesItem` | EYES 목록을 숫자 6과 이름 표로 하드코딩했다. 새 3종이 눈을 가리는지는 이 테스트의 판정과 **무관하다**. 초상화의 눈 그리기는 상수로 꺼져 있고 EYES 슬롯을 보지 않는다 | 테스트 | 불필요(코드로 확정). 수정 후 픽스처 실행 + 변이 E1~E3 | test-engineer |
| 2 | `TodoBoardDateNavigationTests.ClickingACalendarCell…` (판정 불가) | 테스트 결함 확정. 빈 목록에서 [‹]는 설계상 비활성이다. 이 테스트는 PlayMode에서 처음으로 돈 것이 09-14다 | 테스트 | 불필요. 수정 후 실행 + 변이 T1~T3 | test-engineer |
| 3 | `CostumeFocusPropLifecycleTests.몰입기_도중_취소해도…` | 테스트가 프로덕션 계약보다 강한 주장(동기 해제)을 했다. 원칙 1은 포즈 층의 이중 게이트로 이미 막혀 있다 | 테스트 | **R3** | test-engineer |
| 4 | `PetBalloonClearsBodyTests.실제로_그려지는_풍선이…` | 공식값을 몸통 x에 대고 비교한 것이 설계 결함이다. **qa 가설(착지 기울기)은 수치로 반증된다.** 원인 후보는 Idle **두리번 상체 기울임**. ★ 09-15 **간헐 확정**(격리 ×3 중 2 빨강). 빨강 조건은 `최소 편차 ≤ 0.5575`다(§4-7) | 테스트 | **R2** | test-engineer |
| 5 | `SettingsWindowReturnPathTests.ClosingSettingsReopens…` | 640×480에서 [설정] 칩이 접힌다(ce3b102 [DLC] 탭). 테스트가 해상도 전제를 묵시적으로 가졌다 | 테스트(+UX 질문) | **R1** | test-engineer / ux-designer |
| 6 | `SettingsWindowReturnPathTests.ClickingOutsideSettings…` (판정 불가) | 5와 같은 원인이다. 거기에 `Assume` 때문에 러너에 안 보였다 | 테스트 | **R1** | test-engineer |
| 7 | `AccessoryFillRenderingTests.왕관은_채워지되…` | 게이트에 착용 비트맵 갈래가 없다. 왕관은 존폐 대기라서 조건부 `Assert.Ignore`로 처리한다 | 테스트(인프라) | 불필요. 수정 후 실행 + 변이 K1~K3 | test-engineer |
| 8 | `CostumeFocusStillTierTests.몰입기_동안_절감등급_Still에_도달한다` (09-15 추가) | **간헐**(격리 ×3 중 1 빨강). 가설: 「Still 도달(폴링 위상으로 2.00초 고정) 전에 코스튬 쓰기 ≥ 1」 단언이 **코스튬 듀티 주기의 무쓰기 구간(코드상 최장 3.2초)**과 관측 시작 위상에 기댄다. 프레임 수 의존이 아니라 **벽시계 위상 의존**이다. 결과는 두 모양뿐이었다: 통과 6회는 전부 `1→8 / 3회`, 빨강 2회는 전부 `1→10 / 0회` | 테스트(추정) | **필요**(§11-8, 러너 라운드 배정 대기) | test-engineer |
| 9 | `EdgeHopDownTests.NegativeControl_WithoutPostClimbCooldown_LeavesDockAlmostImmediately` (09-15 추가) | **간헐**(5-d 전량 1 빨강 / 격리 3 초록 / 이력 초록 12). **사실(코드)**: 관측 루프 예산(`while (hold < DockHoldSeconds)` `:666`)과 단언 경계(`Assert.Less(_dockHoldSeconds, DockHoldSeconds)` `:568`)가 **같은 상수**다. 그래서 「떠나지 못한 채 예산이 끝남」이 곧 빨강이고, 이를 가를 수 있는 `_dockLost`(`:674`)는 기록만 하고 단언하지 않는다. **사실(로그)**: 빨강 회차는 올라선 직후 시험 컨트롤러가 `이동의도 0.00 · 페이즈 잔여 0.03초`였고, 그 뒤 안쪽으로 걸어갔다(X 3.64). 가설: 음성 대조가 기대는 **등반 중 경주**(`:571`)가 이 회차에 성립하지 않았다. 걷기 구간 바닥값이 구간을 늘려 등반 창과의 위상을 바꿨다 — 유력 후보 `de3fb32`, 같은 기간 기전 파일 커밋 8개 중 판별은 러너(§12-3 나) | 테스트(추정) | **필요**(§12-8, 러너 라운드 배정 대기) | test-engineer |
| 10 | `DragStruggleTests.StruggleDoesNotBreakCursorStickiness` (09-15 추가) | **간헐 — 기존 재발**(5-d 독립 전량 1 빨강 0.0225 / 격리 3 초록 / 저장소 xml 126개(같은 실행 복사본·격리 포함 — 실행 횟수 아님) 중 밀착 오차 빨강 3 · 다른 사유 1). **사실(코드)**: 허용 `0.02f`는 테스트 리터럴이다(`:339`). 테스트는 커서를 에이전트 Update **뒤에** 옮기고(`:186`) 곧바로 잰다(`:201-202`). 몸통은 같은 프레임 Update에서 **한 프레임 전 커서**에 즉시 붙는다(`DragThrowState.cs:197`→`:490-494`). ⇒ 오차 = 커서 속도 2.6249유닛/초 × 그 프레임 dt(추정: 코루틴 재개 순서는 Unity 규칙, 미실측). 빨강 ⇔ 1.2초 창 안에 dt > 7.62ms인 프레임이 하나라도 있음. 실측 등가 dt: 빨강 8.6·13.9·10.4ms / 격리 0.6·2.9·2.6ms. 제품의 추종 지연이 아니라 **리그의 한 프레임 지연**을 잰다 | 테스트(추정) | **필요**(§13-7, 러너 라운드 배정 대기) | test-engineer |

- R1~R3은 리더 판정 ③에 이름이 붙은 것만 대응시켰다.
- qa 계획 원문(R1~R6의 정의 문장)은 Tasklist와 qa scratch에서 찾지 못했다. **R4·R5가 무엇인지는 미확인**이다(§9 자백).

공통 규칙: 수정본에서 **`Assume`를 전제 판정에 쓰지 않는다.** 판정 불가는 `failed=`에도 `regress.sh compare`에도 안 보인다(TEAM.md 거짓 통과 형태 규칙 5). 2번과 6번이 정확히 그 경로로 가려져 있었다.

---

## 1. `PortraitEyeVisibilityTests.EyesAreAbsentUnderEveryGlassesItem`

### 1-1. 현재 단언
`Tests/PlayMode/PortraitEyeVisibilityTests.cs:182-184`
```csharp
Assert.AreEqual(Glasses.Length, EquipmentModel.ItemCount(EquipmentSlot.Eyes),
    $"{LogPrefix} EYES 카테고리의 아이템 수가 {EquipmentModel.ItemCount(EquipmentSlot.Eyes)}개로 바뀌었습니다 — " +
    "이 테스트의 목록(Glasses)을 함께 갱신해야 합니다.");
```
목록(`:61-69`)은 `(Index, Label, LensShape)` 6행이다. 반복문(`:215`)도 이 목록만 돈다.

### 1-2. 원인 재확인 — qa 가설 **맞음**
EYES 에셋은 9개다(`Resources/Items/`).
- 기본 6종: `equip_eyes_*`
- `pack_cyber_eyes_slit_visor`: `itemIndex: 6`, `cohortId: 2`, `requiredLevel: 1`
- `pack_mine_eyes_dust_goggles`: `itemIndex: 7`, `cohortId: 7`, `requiredLevel: 1`
- `pack_arcane_eyes_astro_lens`: `itemIndex: 8`, `cohortId: 8`, `requiredLevel: 1`

첫 줄의 개수 알람이 울렸다. 반복문이 6종만 도므로 알람을 6→9로 고쳐도 새 3종은 검사되지 않는다.

### 1-3. 「새 3종이 초상화에서 눈을 가리는가」 — 사실 판정
**(가) 초상화에는 가릴 눈이 없다 — 사실(코드).**
- `Interaction/CharacterPortraitStage.cs:129` `private const bool DrawEyes = false;`
- `:1135` `if (!DrawEyes) return;`이 눈 원 두 개(`:1139` `AddCircle("EyeBack", …)`, `:1140` `AddCircle("EyeFront", …)`)보다 앞에 있다.
- 이 게이트는 EYES 슬롯을 **읽지 않는다.** `:1113-1129` 주석은 슬롯 조건문으로 눈을 지우던 이중 정의를 2026-08-31에 제거했다고 적는다.
- ⇒ 어떤 EYES 아이템을 쓰든 초상화에 눈이 없다는 결과는 **가림과 독립**이다. 이 테스트가 잠그는 불변식은 「몸과 초상화가 같은 그림」(파일 문서 `:22-29`)이지 「안경이 눈을 가린다」가 아니다.

**(나) 기하학적으로 눈 자리를 덮는가 — 문서 근거, 자동 검사 없음.**
- **판별 속성이 에셋에 없다.** `Core/AccessoryDefSO.cs`의 가림 계열 필드는 `hidesHair`(`:567`) 하나다. 팩 3종 에셋의 최상위 키에도 눈 가림 필드가 없다(`hidesHair: 0`만 있다).
- 자동 검사의 모집단도 기본 코호트뿐이다.
  - `Tests/EditMode/EyesVisorOpacityTests.cs:66-74` `AllEyes()`는 기본 6종 상수다.
  - `:79-84` `목록이_카탈로그와_같은_수다`는 2026-09-08에 기본 코호트로 좁혀졌다.
  - 팩은 명부 `TestClaimExpiryAuditTests` → `AccessoryAssetShapeReachTests.팩_코호트_조형_게이트_셋이_아직_팩을_안_본다`의 Ignore 갭에 등재돼 있다(「EYES 불투명 바이저」 게이트가 팩을 안 본다).
- 디자인 문서:
  - `docs/EQUIPMENT_SHAPE_SPEC_PACK_DETAIL_R3.md:294` Slit Visor 「두 눈 커버 ✓」
  - `:361` Dust Goggles 「두 눈 커버 ✓」
  - `:431` Astro Lens 「큰 판 하나(r 0.86 > EyeOffsetXInHeadRadii 0.3409)」
  - `docs/EQUIPMENT_SHAPE_SPEC_PACK_DETAIL_R4.md:291` Astro Lens는 r 0.56에서 「두 눈 커버 계약 성립」으로 재확인했다.
  - ★ R4는 Slit Visor 바 높이를 0.98→0.60R(`:273`), Dust Goggles 렌즈 높이를 1.30→0.74R(`:282`)로 줄였다. 그런데 두 아이템의 두 눈 커버를 **R4에서 다시 적지 않았다.** 에셋이 R3판인지 R4판인지는 이 라운드에서 대조하지 않았다 ⇒ **Slit Visor·Dust Goggles의 두 눈 커버는 추정(미확인).**
- ⇒ 결론: 가림 여부로 목록을 거르면 안 된다. 거를 속성도 없다. 새로 만들려면 에셋 스키마 변경이 필요하고(design-equipment + coder), 이 테스트에는 필요 없다.

### 1-4. 수정안 (테스트 쪽)
판정 근거: 프로덕션은 초상화 눈을 꺼 둔 상태 그대로 옳다. 낡은 것은 테스트의 손목록뿐이다.

1. **목록 `Glasses`와 `:182-184` 개수 단언을 삭제한다.** `for (int i = 0; i < EquipmentModel.ItemCount(EquipmentSlot.Eyes); i++)`로 **카탈로그 전종**을 돈다. 표시 이름은 `EquipmentModel.ItemName(EquipmentSlot.Eyes, i)`로 쓴다.
   - 숫자 목록도, 식별자 문자열 목록도 새로 만들지 않는다(CLAUDE.md 하드코딩 금지).
   - 비공허성 단언 `Assert.Greater(ItemCount, 0)`를 둔다.
2. **렌즈 이름 대조(`lens`)를 없앤다.** 착용 대조군은 이미 있는 **부품 수 증가**(`:206-253`)만 쓴다.
   - 이 자는 이름과 코호트 모두에 무관하다. 기본 0~3번은 이미 v1 이름이 없어 이 자로만 재고 있다.
   - v1 이름이 남은 4·5번에서는 「더 엄격한 이름 검사」를 잃는다. 그 이름을 지키는 쪽은 EditMode 조형 테스트다. 초상화 테스트의 목적(눈 부재)과는 무관하므로 잃어도 된다.
3. **전종 보유를 강제한다.**
   - `EquipmentModel.RequiredLevel(Eyes, i)`(`Core/EquipmentModel.cs:244`, public)의 최댓값까지 `CharacterProgressionModel.AddXp`로 올린다. `PetBalloonClearsBodyTests.RaiseLevelTo`와 같은 관례다.
   - `Assert.AreEqual(EquipmentModel.ItemCount(EquipmentSlot.Eyes), tested)`로 **건너뜀 0**을 단언한다. `:219-223`의 미보유 건너뜀 경로는 삭제한다.
   - TearDown에 `CharacterProgressionModel.ResetForTesting()`를 더한다.
4. 메서드 이름은 **유지**한다. `docs/verify/renames.tsv`로 이어지는 기준선 이력이 끊기지 않게 하기 위해서다. 요약 문서(`:12`)의 「EYES 전종」은 이미 맞는 말이다.
5. `EyeBack`/`EyeFront` 니들은 **살아 있다.** 프로덕션 `CharacterPortraitStage.cs:1139-1140`에 실재한다. 다만 부재 단언용이므로 E1로 생존을 확인해야 한다(CLAUDE.md 부재 단언 규칙).

### 1-5. 수정 뒤에도 결함을 잡는가 — 변이
| 변이 | 주입 | 기대 |
|---|---|---|
| E1 | `CharacterPortraitStage.DrawEyes = true` | 미착용 대조군(`:191`)과 전 회차에서 빨강. 부재 니들 생존의 증명이다 |
| E2 | `DrawAccessories`(`:1345-`)에서 `if (slot == EquipmentSlot.Eyes && !기본코호트) continue;` | 수정본은 팩 회차의 부품 수 대조군에서 빨강. **옛 테스트는 초록**(6종만 돎)이다. 커버리지가 늘었다는 증명이다 |
| E3 | 팩 EYES 착용 시에만 눈을 그리게(`if (DrawEyes \|\| wornEyes >= 기본수)`) | 수정본은 팩 회차에서 빨강, 옛 테스트는 초록 |

---

## 2. `TodoBoardDateNavigationTests.ClickingACalendarCellPicksTheDayInsteadOfDraggingTheWindow`

### 2-1. 현재 단언
`Tests/PlayMode/TodoBoardDateNavigationTests.cs:176-179`
```csharp
_popover.FeedClickForTests(_popover.PrevDayScreenRect.center);
yield return null;
Assume.That(_popover.SelectedDayIndexForTests, Is.Not.EqualTo(today),
    $"{LogPrefix} [‹]를 눌렀는데 날짜가 안 옮겨졌습니다 — 아래 판정의 전제가 성립하지 않습니다.");
```
`:56`에서 `TodoListModel.ResetForTesting();`로 목록을 비운다.

### 2-2. 원인 재확인 — qa 가설 **맞음, 테스트 결함 확정**
세 겹으로 막혀 있다. 세 겹 모두 사실(코드)이다.
1. `Interaction/TodoBoardPopover.cs:413-422` `MinSelectableDay`는 `TryGetEarliestPlannedDay(out int earliest) && earliest < today ? earliest : today`다. 목록이 비면 `today`가 된다.
2. `:1308` `SetControlEnabled(_prevDay, axis && _selectedDay > MinSelectableDay);`이므로 [‹]가 비활성이다. `:1599` `if (button != null && !button.interactable) return true;`이 클릭을 먹고 아무 일도 하지 않는다.
3. 설령 눌려도 `:1007` `Mathf.Clamp(_selectedDay + delta, MinSelectableDay, MaxSelectableDay)` → `:1008` `if (next == _selectedDay) return;`에서 멈춘다.

이 하한은 설계다(`:411-412` 「빈 달을 무한히 넘길 수 있으면 사용자는 그걸 고장으로 읽는다(R6-8 #5)」).

이 테스트와 하한은 **같은 커밋 `450482c`**에서 들어왔다. `git log`상 테스트 파일은 그 커밋 하나이고, `MinSelectableDay` 블록의 `-L` 이력도 `450482c`만 나온다. 그 커밋 본문은 「EditMode 전량 2,901건」만 적었다. qa 이력에서도 이 테스트의 결과 기록은 09-14 한 줄뿐이다 ⇒ **한 번도 통과한 적 없는 테스트가 판정 불가로 숨어 있었다.**

### 2-3. 수정안 (테스트 쪽)
1. **「오늘이 아닌 날」을 같은 달 안에서 고른다.** 달이 중요한 이유가 있다.
   - 달력 토글이 `_calendarAnchorDay = _selectedDay`(`:997`)로 달을 정한다.
   - 칸은 `Selectable = inMonth && inDomain`(`:1394-1398`)이다.
   - 옮겨 간 날이 다른 달이면 오늘 칸이 선택 불가가 되고, 월말·월초에 `:204`에서 **거짓 빨강**이 난다.
   ```csharp
   bool tomorrowSameMonth = TodoBoardPopover.DateOfDayIndex(today + 1).Month
                            == TodoBoardPopover.DateOfDayIndex(today).Month;   // public static 순수 함수(:383)
   if (!tomorrowSameMonth)
       TodoListModel.Add("탐색 하한 확장(테스트)", int.MaxValue, today - 1);   // 반환값 미사용 — softCap은 무의미
   // ↓ 기존 :168 재오픈을 이 뒤에 둔다 → RefreshContent가 [‹] 활성을 다시 계산한다
   _popover.Open(...);
   yield return null;
   Rect step = tomorrowSameMonth ? _popover.NextDayScreenRect : _popover.PrevDayScreenRect;
   ```
   - [›]는 `today < MaxSelectableDay(= today+1)`이라 항상 활성이다. 시드가 필요 없다.
   - 한 달은 28일 이상이라 오늘이 1일이면서 말일일 수 없다. 두 갈래 중 하나는 항상 같은 달이다.
2. `Assume.That` → **`Assert.AreNotEqual`**로 바꾼다. 메시지에 방향, `today`, 달 판정을 넣는다.
3. 날짜 의존 갈래가 매 실행 검증되지 않는 문제를 보완한다. 방향 선택식을 테스트 안의 순수 정적 함수로 뺀다. 같은 테스트 첫머리에서 **합성 일자 두 개**(`DayIndexOfDate(new DateTime(2026,9,30))`, `(2026,9,15)`)에 대해 갈래가 갈리는지 단언한다. 프로덕션 상태를 안 건드리는 순수 산술이라 날짜와 무관하게 늘 돈다.
4. 정리는 기존 `Cleanup`(`:97`)과 `OneTimeTearDown`(`:44`)의 `ResetForTesting`으로 충분하다.

### 2-4. 변이
| 변이 | 주입 | 기대 |
|---|---|---|
| T1 | `StepDay`에서 `next = _selectedDay` 고정(탐색 불능) | 수정본은 전제 `Assert` **빨강**. 옛 테스트는 판정 불가(러너에 안 보임) |
| T2 | 달력 셀 버튼을 `_controlRects` 스냅샷 밖으로(지연 생성) | `:211` 드래그 단언 빨강. 테스트 원래 목적이 보존됐다는 확인이다 |
| T3 | `OnCellClicked`에서 `_followToday` 갱신 삭제 | `:218` 빨강 |

---

## 3. `CostumeFocusPropLifecycleTests.몰입기_도중_취소해도_프롭이_화면에_남지_않는다`

### 3-1. 현재 단언
`Tests/PlayMode/CostumeFocusPropLifecycleTests.cs:184-189`
```csharp
director.StopFocusSession();

// 프롭 층은 이 순간 즉시 «없는 것»이 된다(유령 제스처 방지). 그림은 퇴장 연출 뒤 사라진다.
Assert.IsFalse(prop.PropPlaced,
    $"{LogPrefix} 취소 직후에도 PropPlaced가 참입니다 — 코스튬 포즈 층이 " +
    "사라지는 물건을 계속 짚습니다(절대 불변 원칙 1).");
```

### 3-2. 원인 재확인 — qa 가설 **맞음, 한 곳 정밀화**
사실(코드):
- `Interaction/FocusWatchDirector.cs:229-236` `StopFocusSession()`
  - `IsSessionActive = false; TryTriggerPoseState(StickmanStateId.FocusCancelled);`
  - `CurrentPhase`는 파생값(`:119-121`)이라 **즉시** `None`이 된다.
- `PropPlaced`는 `Interaction/CostumePropRenderer.cs`의 `LateUpdate`(`:181-189`)에서만 내려간다.
  - 경로: `SyncToPhase`(`:196-215`) → `if (from == FocusSessionPhase.Immersion) BeginOutro();` → `BeginOutro`(`:442-450`)의 `PropPlaced = false;`
  - 구독 경로는 없다. 설계 문구는 `:212-213` 「넷 이상이지만 여기 한 줄이 전부다(파생값 하나만 보기 때문)」다.
- UnityTest의 `yield return null`은 다음 프레임의 **Update 뒤, LateUpdate 앞**에서 재개된다(Unity 문서의 코루틴 실행 순서 — **이 Unity 버전에서는 미실측**, R3에서 확인).

⇒ 테스트는 프레임 N의 Update와 LateUpdate **사이**에서 `Stop`을 부르고 곧바로 단언한다. `PropPlaced`는 같은 프레임 N의 LateUpdate에서 내려간다.

- **정밀화**: qa는 「1프레임」이라 적었다. 실제 차이는 **같은 프레임 안의 LateUpdate 한 번**이다. `Stop` 뒤에 `PropPlaced == true`인 채로 **렌더된 프레임은 0개**다(추정 — 렌더는 LateUpdate 뒤).
- 이력: 이 테스트가 든 전량 실행 2회(09-07 `part2-final`, 09-14)가 모두 같은 메시지로 실패했다. 기록상 통과 0회다. 생성 커밋은 `git log`에서 경로로 잡히지 않아 **미확인**이다.

### 3-3. 원칙 1(행동-텍스트 싱크)에 걸리는가 — **포즈 층으로는 안 걸린다(사실(코드))**
`PropPlaced`의 소비처는 셋이다.

**① 코스튬 포즈 층.** 원칙 1과 직결된다.
- `States/StickmanBlackboard.cs:2632` `IsCostumeImmersionActive => CostumeProp != null && CostumeProp.PropPlaced`
- `:2690` `TickCostumeKeypose`가 이 값을 본다. 그런데 이 함수는 `:2331-2348` `if (_focusStanceActiveThisFrame) { … if (!gesturing && TickCostumeKeypose(pose)) return; … }` **안에서만** 불린다.
- `_focusStanceActiveThisFrame = TickFocusWatchStance(dt)`(`:2096`)이다. 이 함수는 `:2524` `id != StickmanStateId.Idle || !IsFocusSessionAmbientActive`면 false다.
- `IsFocusSessionAmbientActive`(`:2443-2444`)는 `_focusDirector.IsSessionActive`를 매번 읽는다(`:2422-2431`).
- ⇒ `Stop` 직후에는 세션 비활성이고 상태도 `FocusCancelled`다. 두 게이트가 모두 닫혀 있다. `PropPlaced`가 LateUpdate까지 참이어도 **이후 어떤 `TickPose`도 코스튬 키포즈를 쓸 수 없다.**

**② `AutoWanderController.IsCostumeImmersionHolding`(`States/AutoWanderController.cs:375`).** 배회 사다리와 `ScreenGlanceAllowed`를 정한다. 텍스트와 무관하다.

**③ `CharacterPetRenderer.PlaneOrbitFoldedToBackHalf`(`:408`, `:628`).** 종이비행기 궤도를 접는다. 렌더러끼리 LateUpdate 순서가 정해져 있지 않다(두 `.meta` 모두 `executionOrder` 없음). 그래서 프레임 N에 한 번 더 접힌 궤도가 그려질 수 있다. 시각만 해당하고 텍스트와 무관하다.

범위 밖 관찰(원칙 1, 이 테스트와 무관):
- 포즈는 `StickmanAgent.Update`(`Core/StickmanAgent.cs:1016` `_blackboard.TickPose(dt)`)에서 계산된다.
- 입력(팝오버 [그만두기])이 에이전트 Update **뒤**에 처리되면, 프레임 N은 「취소 전 포즈 + `FocusCancelled` 말풍선」으로 렌더된다.
- 이것은 입력으로 촉발되는 **모든** 상태 대사가 가진 한 프레임 순서 차이이고, 프롭과 무관하다.
- Still 등급에서 한 프레임은 최대 60÷8 = 7.5fps, 즉 133ms다(`Platform/ViewerPresence.cs:517-518` `MaxStillDivisor = 8`, `:642`). 체감 여부는 design-narrative/design-motion의 판단 사안이고 미측정이다.

### 3-4. 수정안 (테스트 쪽) — 「다음 프레임까지는 반드시 해제」
판정 근거:
- 프로덕션 문서(`CostumePropRenderer.cs:447` 「포즈 층은 **이 순간부터** 즉시 꺼진다」)의 「이 순간」은 `BeginOutro`가 도는 LateUpdate다.
- 테스트 주석(`:186`)만 `Stop` 호출 시점으로 읽었다.
- 동기 해제를 위해 세션 종료 구독을 더하는 안은 **기각**한다. 「끄는 자리」가 둘이 되고, 3-3에서 봤듯 이득 보는 소비처가 없다.

```csharp
director.StopFocusSession();
Assert.AreEqual(FocusSessionPhase.None, director.CurrentPhase,
    "…Stop이 구간을 동기로 닫지 않았다 — 아래 1프레임 경계가 공허해진다(양성 대조)");
int frameAtStop = Time.frameCount;
yield return null;                                         // ★ 정확히 한 번
Assert.AreEqual(frameAtStop + 1, Time.frameCount,
    "…경계 측정이 한 프레임이 아니다 — 이 단언의 뜻이 바뀌었다");
Assert.IsFalse(prop.PropPlaced, "…Stop 다음 프레임에도 프롭이 서 있다(해제는 Stop이 일어난 프레임의 LateUpdate가 경계)");
Assert.IsFalse(agent.Blackboard.IsCostumeImmersionActive, "…");
Assert.IsFalse(agent.Blackboard.CostumePoseWroteThisFrame,
    "…Stop 다음 프레임의 TickPose가 코스튬 키포즈를 썼다 — 사라지는 물건을 짚는 유령 제스처(원칙 1)");
```
- `CostumePoseWroteThisFrame`(`StickmanBlackboard.cs:2677`)은 매 프레임 `:2115`에서 내려가고 `:2731`에서만 선다. `yield return null` 직후는 프레임 N+1의 `TickPose` 결과다 ⇒ **원칙 1의 직접 계기**다. `PropPlaced`는 간접 계기다.
- 기존 `TestClock.WaitUntil(ActiveVisualCount == 0, budget)`(`:194`)과 뒤 단언(`:197-203`)은 그대로 둔다. 퇴장 연출은 시간 기반이라 벽시계 예산이 맞다.
- `:186` 주석을 「해제 경계 = Stop이 일어난 프레임의 LateUpdate」로 고친다.

### 3-5. 벽시계 규칙과의 양립 — 근거
- CLAUDE.md의 금지 대상은 **시간 기반 연출**을 프레임 수로 예산 잡는 것이다. 배치모드 2,000fps에서 180프레임이 0.01~0.08초로 쪼그라드는 사고 때문이다.
- 여기서 재는 것은 시간이 아니라 **실행 순서 계약**(「Stop 뒤 첫 LateUpdate」)이다. `Stop`과 단언 사이에 끼는 LateUpdate 수는 fps와 무관하게 **구조적으로 1**이다.
- 반대로 벽시계 예산(예: 50ms)을 쓰면 2,000fps에서 약 100프레임을 기다린다. 그러면 변이 C1(해제를 퇴장 끝으로 미룸, 0.22s÷4 ≈ 55ms)이 **초록으로 통과한다.** 무한 대기(`WaitUntil(!PropPlaced)`)도 `Teardown`(`:503`)이 결국 내려 주므로 C1·C2를 둘 다 통과시킨다.
- ⇒ 이 경계만큼은 프레임 단위가 옳은 자이고 벽시계는 틀린 자다. `Time.frameCount` 가드는 「정확히 한 프레임」 자체를 단언으로 잠근다. 누가 `yield`를 늘리면 빨개진다.
- `WaitForEndOfFrame`(같은 프레임 경계)은 더 조이지만 쓰지 않는다. 배치모드 Null 그래픽에서의 재개 보장을 확인하지 않았다(프로덕션 사용처는 `Platform/LayeredHybridPolicy.cs` 한 곳).

### 3-6. 변이
| 변이 | 주입 | 수정본 | 무한 대기판(대조) |
|---|---|---|---|
| C1 | `BeginOutro`의 `PropPlaced = false;` 삭제(`Teardown`에서만 내려감) | **빨강** | 초록 |
| C2 | `SyncToPhase`의 `BeginOutro`를 보류 플래그로 다음 LateUpdate에 실행 | **빨강** | 초록 |
| C3 | `TickFocusWatchStance`의 `!IsFocusSessionAmbientActive` 조건 삭제 + `TickCostumeKeypose`의 `!IsCostumeImmersionActive` 가드 삭제(이중) | `CostumePoseWroteThisFrame` 단언 **빨강**(계기 생존) | — |
| T1(테스트 쪽) | `yield return null` 2회 | `frameCount` 가드 **빨강** | — |

### 3-7. R3 판별 설계
1. 진단 사본(커밋 안 함)으로 로그를 남긴다.
   - `Stop` 직전과 직후: `Time.frameCount`, `PropPlaced`, `CurrentPhase`, `CostumePoseWroteThisFrame`, 상태 ID
   - `yield return null` 직후: 같은 값
   - **기대**: 직후 `PropPlaced=True`(동기 아님), yield 뒤 `False`, frameCount +1, `CostumePoseWroteThisFrame=False`
2. 1이 기대와 다르면 3-5의 전제(yield 재개 지점)가 틀린 것이다 → debugger와 가설 토론.
3. 수정본 + C1·C2·C3·T1을 **각각 따로** 실행한다. xml 4개가 전부 빨강이고 판정은 TEAM.md 규칙 1로 한다.

---

## 4. `PetBalloonClearsBodyTests.실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다`

### 4-1. 현재 단언
`Tests/PlayMode/PetBalloonClearsBodyTests.cs:165-183`(요지)
```csharp
float ownerX = agent.Blackboard.Body.position.x;           // ★ 한 번만
...  float deviation = Mathf.Abs(pet.PetWorldPosition.x - ownerX); ... // 1초 최소값
Assert.Greater(minAbsDeviation, expectedOffset - SampleTolerance, "…공식값 … 못 미칩니다 …");   // :178
Assert.Less(minAbsDeviation, expectedOffset + SampleTolerance, …);                              // :181
```
`:186-190`은 이름이 말하는 성질이다: `minAbsDeviation - HeadRadius*0.80 > physicalHalfWidth`.

### 4-2. 원인 재확인 — ★ **qa 가설(착지 기울기 타이밍)은 수치로 반증된다**
**설계 결함은 사실(코드)이다.** 프로덕션 앵커는 몸통 x가 아니라 **기울어진 머리**를 따른다.
- `Interaction/CharacterPetRenderer.cs:1019-1021`
  - `Vector2 head = LeanedHeadWorld(bb, r * BalloonTetherAboveInR);`
  - `anchor = new Vector2(head.x - facing * r * BalloonTetherBehindInR, …)`
- `LeanedHeadWorld`(`:1843-1852`)는 몸통 `localRotation`으로 엉덩이를 축 삼아 돈다.
- 테스트는 공식값을 **몸통 x**와 비교한다(`:165`, `:169`). 상체가 기울면 두 값은 **정의상** 다르다.

**착지 기울기로는 크기가 안 맞는다 — 산술.**
- 착지는 별도 상태 `LandingCrouch`다(`States/LandingCrouchState.cs:192`).
- 기울임 요청은 `pitch × CurrentCrouchAmount`이고(`:340`), 곡선은 끝에서 일어서며 0으로 돌아간다(`:398-`).
- 종료 후 목표 0으로 `bodyLeanSmoothingRate` 12/초 감쇠한다(`Data/DefaultStickConfig.asset:414`, `StickmanPoseAnimator.cs:3813-3819`).
- 착지 상체 기울기의 **코드상 최대는 30°**다: `LandingCrouchState.cs:241-243` `_torsoPitchDegrees = Lerp(6, 22, …) + u·max(0, 30 − 22)` → 22 + u·8, u ≤ 1(설정 기본값 기준).
- 테스트는 Idle 연속 0.5초를 기다린다(`:243-253`) ⇒ 상한 30°에서 시작해도 잔여 ≤ 30·e^(−6) ≈ **0.0744°**.
- 머리 x 이동은 팔 길이 0.890(아래 기하) × sin 0.0744° ≈ **0.0012**다. 관측 부족분 0.025~0.076보다 **약 21~66배 작다** ⇒ 반증 결론은 유지.
- ★ 정정(2026-09-14 리더 · verify-change 3단계): 첫 판은 23.4°를 기준으로 「잔여 0.058° · 이동 0.0009 · 30~85배」라 적었다. 23.4°는 코드가 아니라 전량 로그 한 회차의 `[무릎앉아] … 상체=23.4도` 관측값이었고 출처도 적지 않았다. 반증은 코드 상한으로 해야 한다.

**원인 후보 — Idle 두리번(LookAround) 상체 기울임.** 기전은 사실(코드), 크기와 발생 확률은 추정이다.
- 발동
  - `AutoWanderController.TickResting`(`:408-433`)이 휴식마다 1회 `WanderAmbientMotion.LookAround`를 발행한다. 지연은 U(1.0, 2.5)초(`:305`, 에셋 `:136-137`), 쿨다운은 30초(에셋 `:421`)다. 새 씬이므로 쿨다운 0에서 시작한다.
  - `_autoWander.Tick(dt)`은 `IntentSource`와 무관하게 매 Update 호출된다(`StickmanAgent.cs:976`, `AutoWanderController.cs:232-246`) ⇒ **테스트의 `StillIntent`로는 안 막힌다.**
- 자세
  - `StickmanBlackboard.cs:2358-2362` → `ApplyIdleAmbientPose` → `StickmanPoseAnimator.cs:865-866` `RequestBodyLean(sin(2πp)·smoothstep(sin πp)·LookLeanDegrees)`
  - 지속은 0.9초(에셋 `:400`)다.
  - `LookLeanDegrees = bodyLeanLookAroundDegrees = 7`(에셋 `:411`, `StickmanBlackboard.cs:3314-3315`)이고, 봉우리는 p=0.25와 0.75에서 7×0.793 = **5.55°**다.
- 기하(상수에서 유도)
  - r = 0.5775÷3.5 = 0.165 → 배율 0.165÷0.22 = 0.75
  - 머리 중심 (2.2747−0.22)×0.75 = 1.541, 매듭 +0.3r = 0.0495, 엉덩이 0.9347×0.75 = 0.701 → 팔 길이 **0.890**
- 크기
  - 5.55°의 원시 부족분은 0.890·sin 5.55° = **0.086**이다.
  - 풍선 추종 `BalloonFollowRate` 3.4/초(`:239`)와 상체 감쇠 12/초로 걸러지면, 1차 저역통과 근사로 **약 0.03~0.07**이다(한 봉우리 대 앞뒤 왕복의 되돌이).
  - 관측은 7f41eef 이후 기준으로 0.0252(c5166eb), 0.0286(09-14), 0.0762(09-07 part2-final)다. **같은 자릿수**다. `a85914a`의 0.0447은 코드 주석 `:1038-1040`이 규명한 대칭 클램프 결함(4차 수정 전)이다.
- 발생 확률
  - 표본 창은 Idle 진입 약 0.5초 뒤부터 약 1초다. 두리번은 1.0~2.5초에 발동하므로 창과 겹치는 경우는 지연 < 약 1.5초, 대략 1/3이다(추정).
  - 관측은 7회 중 실패 4다. 표본이 작고, 휴식 진입(`EnterResting` `:290-293`)과 Idle 진입의 동기는 미확인이다.
- 배제한 다른 기울임 출처
  - 관망 자세(`:1552`)는 집중 세션 중에만 켜진다(`StickmanBlackboard.cs:2524`). **나는 처음에 이 가설을 세웠다가 이 줄로 기각했다.**
  - 피격 기울임은 `RagdollImpactResolver`에서만 오고 테스트에 충돌이 없다.
  - 기타 요청은 걷기, 춤, 집중 제스처(`:1125`, `:1848`)라 Idle과 무관하다.

### 4-3. 판정
- **테스트 쪽 설계 결함**이다: 공식값 비교의 기준점이 틀렸고(몸통 x 대 기울어진 머리 x), 기울임 구간을 거르지 않았다. `ownerX` 1회 측정은 Idle 고정 몸통이면 무해하지만 기준을 매 프레임으로 바꾼다.
- 프로덕션은 결함이 아니다. 머리 옆에 묶인 끈이 머리를 따르는 것이 문서화된 설계다(`:997-1008`).
- 이름이 말하는 성질은 성립한다. 최악 0.5013에서 0.5013 − 0.132 = 0.369 > 0.300이다.

### 4-4. 수정안 (리더 방침 그대로: 성질은 권위, 공식 일치는 표본을 가려서)
**A. 권위 단언 — 전 표본, 기울임 구간 포함.** 매 프레임 `bodyX_i = agent.Blackboard.Body.position.x`를 새로 읽는다. 모든 i에 대해 `|pet.x − bodyX_i| − HeadRadius·BalloonRadiusInR > physicalHalfWidth`를 요구한다.
- 전 구간 최소값과 그 프레임의 `pose.BodyLeanDegrees`, `IsIdleAmbientMotionActive`를 로그에 남긴다.
- 표본 벽시계 ≥ 1초와 프레임 > 30을 비공허성으로 둔다.

**B. 공식 일치 — 기울임 없는 정착 표본만.**
- 표본 조건(매 프레임):
  - `!agent.Blackboard.IsIdleAmbientMotionActive`(`StickmanBlackboard.cs:2855`, public)
  - `pose.BodyLeanDegrees == 0f`(`StickmanPoseAnimator.cs:3759`, public). `SetBodyLean`이 임계 아래를 정확히 0으로 스냅하므로(`:3746`) 정확 비교가 정당하다.
  - 위 둘이 **벽시계 연속 0.25초 이상** 성립했고, 그 0.25초 동안 `pet.PetWorldPosition.x`의 변화폭이 `SampleTolerance`의 1/10 미만(추종 정착 실측)일 것
  - 추종 계수 3.4는 private라 테스트에 베끼지 않는다. 정착은 **측정**으로 판정한다.
- 대기 예산(벽시계) = `config.wanderLookAroundDelayMax + config.idleAmbientLookAroundSeconds + 2초`. 에셋 기준 5.4초이고 **Config 필드를 읽어 계산**한다.
- 예산 안에 정착 표본이 0.5초분 모이지 않으면 **실패**다(비공허성).
- 그 표본에서만 `:178`과 `:181`의 ±`SampleTolerance`를 적용한다.

**C.** `:160-163` 주석(「Idle이라 좌우 흔들림이 없다」)은 거짓이므로 두리번 기울임 사실로 고친다.

### 4-5. 변이
| 변이 | 주입 | 기대 |
|---|---|---|
| P1 | `BalloonTetherBehindInR` 3.5 → 0.75(신고 당시) | A·B 둘 다 빨강 |
| P2 | `TickBalloon`의 `if (!_hasPosition) { _position = anchor; … }` 삭제(원점에서 추종 시작) | A가 첫 프레임에서 빨강 |
| P3 | `BalloonTetherOffsetWorld` 게터만 `HeadRadius * 3.0f`(공식과 그림 불일치) | B 빨강(0.495 대 0.5775) |
| P4(대조) | 옛 테스트 + 표본 창 시작 시 `StickmanEventBus.RaiseWanderAmbientMotionRequested(WanderAmbientMotion.LookAround)` 강제(`Core/StickmanEventBus.cs:826`, public) | 옛 테스트는 **결정적 빨강**, 수정본은 초록. 원인 확정의 한 축이다 |

한계(기존과 동일): 몸통이 화면 중앙(x=0)이라 화면 가장자리 클램프 결함(4차 수정분)은 이 테스트가 못 잡는다.

### 4-6. R2 판별 설계 (구체화)
1. **진단 사본**(커밋 안 함)을 **10회** 반복한다. 매 프레임 한 줄씩 남긴다.
   - 필드: `t_wall, frameCount, state, IsIdleAmbientMotionActive, CurrentIdleAmbientMotion, BodyLeanDegrees, bodyX, petX, |petX−bodyX|`
   - 두리번 발행 시각은 `AutoWanderController.LookAroundRaisedCount` 증가로 잡는다(`:75` `public int … { get; private set; }`, 증가는 `:428`).
   - 로그 줄 수가 프레임 수와 같은지 확인한다. 아니면 그 회차는 무효다.
2. 가설별 예측
   - **H-두리번**(이 문서): 부족분 > 0.02인 프레임이 전부 [두리번 시작, 종료 + 약 1초] 안에 있다. 두리번이 창과 안 겹친 회차는 전부 통과한다.
   - **H-착지**(qa): 부족분이 두리번과 무관하게, 표본 창 **시작부**에서 `BodyLeanDegrees ≠ 0`으로 감쇠하며 나타난다.
   - **H-기타**: 부족분 프레임에서 `BodyLeanDegrees == 0`이고 두리번이 비활성인 채 1초 넘게 지났다 → 둘 다 아니다 → **디버거로 이동**
3. **개입 A/B**(인과 확정)
   - (a) P4 강제 발행 → 옛 단언 10/10 빨강
   - (b) 매 프레임 `agent.Blackboard.CancelIdleAmbientMotion()`(`:2915`, public) → 옛 단언 10/10 초록
   - (a)와 (b)가 모두 성립하면 H-두리번 확정이다.
   - (b)가 초록이 안 되면 H-두리번 기각 → 디버거와 과학적 토론으로 넘긴다.
4. 수정본 10회 초록, P1~P3 각각 빨강.

범위 밖 관찰(미검증 — design-equipment/design-motion 참고): 피격 기울임 `bodyLeanHitDegrees` 14°에서는 원시 부족분이 0.890·sin 14° = 0.215다. 그러면 0.5775 − 0.215 − 0.132 = 0.230 < 0.300이 되어, **추종 필터 전 기준으로** 주머니가 몸통 반폭 안에 들어올 수 있다. 이 테스트 이름은 Idle 범위라 단언하지 않는다.

### 4-7. 2026-09-15 추가 실측 — 간헐 확정 · 기준값 확정
**기준값 — 사실(코드 + 로그).** 보고마다 「0.5775」와 「> 0.5575」로 표기가 갈렸다. 원인은 **실패 메시지가 문턱이 아니라 공식값을 찍는 것**이다.
- `PetBalloonClearsBodyTests.cs:85` `private const float SampleTolerance = 0.02f;`
- `:155` `float expectedOffset = pet.BalloonTetherOffsetWorld;` → `CharacterPetRenderer.cs:420` `HeadRadius * BalloonTetherBehindInR`, `:275` `= 3.5f`
  - 로그 `:174-175`가 찍은 런타임 값은 4회 모두 `공식값 0.5775`다(= 머리 반경 0.165 × 3.5).
- 판정식 원문과 문턱
  - `:178` `Assert.Greater(minAbsDeviation, expectedOffset - SampleTolerance, …)` → **통과 조건 `minAbsDeviation > 0.5575`**(엄격 부등호, float32 뺄셈, 표시 F4)
  - `:181` `Assert.Less(minAbsDeviation, expectedOffset + SampleTolerance, …)` → `< 0.5975`
  - `:186-187` `minAbsDeviation - metrics.HeadRadius * BalloonRadiusInR > physicalHalfWidth` → `> 0.432`(0.300 + 0.165×0.80)
- 메시지 `:179-180` 「실측 편차 {minAbsDeviation}유닛이 **공식값 {expectedOffset}유닛**에 못 미칩니다」가 찍는 값은 0.5775다. 문턱 0.5575는 로그 어디에도 찍히지 않는다.
- ★ **확정: 하한 단언 빨강 ⇔ 1초 최소 편차 ≤ 0.5575.** (상한 단언 `:181` — 편차 ≥ 0.5975도 빨강 — 은 별도. 2026-09-15 verify-change 정정) 보고서에는 이 값을 쓴다. 「0.5775에 못 미침」은 문턱이 아니다. 수정본(§4-4 B)의 메시지에는 문턱값 `expectedOffset − SampleTolerance`를 함께 싣는다.

**격리 ×3 + 전량 (`Logs/vc-e4/`, 로그 `[PET-풍선-몸통이탈] 1초간 실측한 최소 가로 편차` 줄)**
| 실행 | 최소 편차 | 공식값 대비 부족분(0.5775 − x) | 문턱 대비(x − 0.5575) | 결과 |
|---|---|---|---|---|
| `P-full` | 0.5185 | 0.0590 | −0.0390 | 빨강 |
| `iso-…-1` | 0.5537 | 0.0238 | −0.0038 | 빨강 |
| `iso-…-2` | 0.5465 | 0.0310 | −0.0110 | 빨강 |
| `iso-…-3` | 0.5775 | 0.0000 | +0.0200 | 통과 |
| `scratchpad/dp5d/xml/P1`(5-d 전량, 09-15 추가 표본) | 0.5775 | 0.0000 | +0.0200 | 통과 |

- 09-15 추가 표본: 5-d 전량 `P1`에서 이 테스트는 `Passed`였다(xml). 로그 `[PET-풍선-몸통이탈]` 줄의 최소 편차 0.5775는 iso-3과 같은 「부족분 0」 모양이다. 누적 관측은 5회 중 빨강 3회다(`P-full`·iso-1·iso-2 빨강, iso-3·`P1` 통과).

- ⇒ 격리 3회 중 2회 빨강이고, 값이 회차마다 다르다 → **간헐 확정**이다. 매번 같은 값을 내는 결정적 결함이 아니다.
- §4-2 가설과의 대조 — 추정을 유지하고, 판별은 R2가 한다.
  - 부족분 0.024~0.059는 §4-2 저역통과 근사 0.03~0.07과 같은 자릿수다.
  - iso-3의 부족분이 **정확히 0**이었다. 「두리번이 창과 안 겹친 회차는 공식값 그대로」(§4-6 ② H-두리번 예측)와 맞는다. 다만 H-착지 잔여도 산술상 0.0012(§4-2)라서 F4 표시로는 0에 가깝다. **이 표본 하나로는 두 가설을 가를 힘이 약하다.**
  - ★ 자백: §4-2의 발생 확률 추정(창과 겹칠 확률 약 1/3)은 **관측보다 낮다.** 이번 4회 중 3회가 빨강이고, 앞 집계는 7회 중 4회였다(두 집계의 중복 여부는 미확인). 기전 가설은 그대로 두지만 확률 추정은 틀렸을 가능성이 높다. 휴식 진입과 Idle 진입의 동기(§4-2 「미확인」)가 겹침을 늘릴 수 있다. R2 ①의 10회 반복이 잰다.
- E-4 무관 — 다른 자로 확인했다.
  - `git show --name-only e6b14c2`의 14개 파일에 `PetBalloonClearsBodyTests.cs`·`CharacterPetRenderer.cs`·`AutoWanderController.cs`·`StickmanBlackboard.cs`·`StickmanPoseAnimator.cs`·`DefaultStickConfig.asset`이 0건이다. 양성 대조로 같은 명령의 전체 목록이 출력됐다.

---

## 5·6. `SettingsWindowReturnPathTests` — `ClosingSettingsReopensTheInfoWindowItReplaced` + `ClickingOutsideSettingsNeitherClosesItNorReturnsTheInfoWindow`

### 5-1. 현재 단언
- `Tests/PlayMode/SettingsWindowReturnPathTests.cs:101-102`
  ```csharp
  Rect chip = _info.SettingsChipScreenRect;
  Assert.Greater(chip.width, 1f, $"{LogPrefix} 헤더의 [설정] 칩 사각형이 비었습니다.");
  ```
- `:138-141`(판정 불가)
  ```csharp
  _info.FeedClickForTests(_info.SettingsChipScreenRect.center);
  yield return null;
  Assume.That(_settings.IsOpen, Is.True, $"{LogPrefix} 전제: 설정창이 열려 있어야 합니다.");
  ```

### 5-2. 원인 재확인 — qa 가설 **맞음(기전은 사실, 수치는 추정)**
- 패널 폭: `Interaction/CharacterInfoWindow.Layout.cs:68` `width = Mathf.Min(PanelWidth, Mathf.Max(MinPanelWidth, Screen.width / scaleFactor - ScreenMargin * 2f))` → 640, 배율 1이면 608.
- 칩 접힘: `:194-200` `SyncHeaderChips`
  - `SetChipVisible(_settingsRect, panelWidth - HeaderSettingsChipInset - HeaderSettingsChipWidth >= _tabStripRightEdge + UiChrome.Space4)`
  - 인셋 = 24 + 32 + 12 = 68(`CharacterInfoWindow.cs:151-152`), 폭 48(`:144`), Space4 16
  - ⇒ **보이는 조건은 `panelWidth ≥ 탭 스트립 오른끝 + 132`**다.
- 빈 사각형: `CharacterInfoWindow.Input.cs:349` `if (… !rt.gameObject.activeInHierarchy) return new Rect();` → 폭 0. 6번은 `(0,0)` 클릭이 칩에 안 맞아 설정창이 안 열린다 → `Assume` 판정 불가.
- 탭 스트립 오른끝
  - 시작 x = `levelX + 44 + 24`(`CharacterInfoWindow.cs:1476`), `levelX = 28 + nameWidth + 8`(`:1453`), `nameWidth` = **캐릭터 이름의 실측 폭**(`:1452`)
  - 폭 = Σ(글자 실측 + 28) + 2×(n−1) + 6(`Tabs.cs:286-316`, `:346-347`)
  - ce3b102가 5번째 행 `new TabDef("DLC", TabPage.Dlc)`(`Tabs.cs:108`)를 더했다.
- **추정 수치**(14pt 한글 약 14pt/자, 라틴 3자 약 28pt, 이름 「스틱메이트」 약 70pt)
  - 시작 174
  - 4탭 폭 약 250 → 오른끝 약 424 → 문턱 약 556 ≤ 608 ⇒ **보임**
  - 5탭 약 +58 → 오른끝 약 482 → 문턱 약 614 > 608 ⇒ **접힘**
  - 이력(09-07 통과 → 09-14 실패)과 방향이 맞는다. 정확한 수치는 R1이 잰다.
- ★ **추가 사실**: 문턱이 캐릭터 이름 길이에 따라 움직인다. 이름 칸은 최대 200pt(`:1448`)이므로, 긴 이름이면 논리 폭 약 750pt 화면에서도 접힐 수 있다(추정).

### 5-3. 판정
- M8 경로(복귀) 테스트는 **테스트 쪽 전제 누락**이다. 640×480에서 칩이 보인다는 것을 당연시했다.
- 칩 접힘 자체는 의도된 설계다. 이유는 오배선 방지다: `Layout.cs:188-191` 「탭을 눌렀는데 [설정]이 열리는 오배선」.
- 다만 접힘의 **사용자 결과**는 UX 판단 사안이다(5-5).

### 5-4. 수정안 (테스트 쪽) — 해상도 전제를 명시하고 강제한다
1. **논리 폭을 넓혀 전제를 강제한다.** 두 M8 테스트 첫머리에서 `ScreenCoordinateConverter.ReportUiDensityScale(0.5f)`(`Platform/ScreenCoordinateConverter.cs:499-504`, public)를 부른다.
   - 이 머신의 에셋은 `desktopDpiScale: 0`(`Data/DefaultStickConfig.asset:117`)이다. 그래서 `ResolveCanvasScaleFactor`(`:527-532`)가 0.5를 돌려주고, 논리 화면은 1280×960이 된다.
   - 정보창은 매 프레임 `ApplyCanvasScaleFactor()`(`CharacterInfoWindow.cs:1020`)를 불러 패널 폭을 1042(설계 최대)로 되돌린다.
   - 설정창도 자기 스케일러를 같은 경로로 맞춘다(`SettingsWindow.cs:2247`).
   - 설계 폭 1042는 private 상수라 **참조하지 않는다.** 대신 2번의 칩 존재 단언이 전제를 확인한다.
   - 0.5는 프로덕션 상수의 사본이 아니라 **시험 환경 매개변수**다(2배 논리 폭).
2. 전제 단언은 `Assert`로 하고 측정값을 싣는다.
   - `Assert.Greater(chip.width, 1f, $"… 논리 폭을 넓혔는데도 [설정] 칩이 없다 — Screen {Screen.width}×{Screen.height}, 캔버스 배율 {_info.CanvasScaleForTests}, 패널 {_info.PanelScreenRect}")`
   - `CanvasScaleForTests`(`TestApi.cs:167`)와 `PanelScreenRect`(`:64`)는 public이다.
3. 6번의 `:140-141` `Assume.That` 두 줄을 **`Assert`**로 바꾼다.
   - 넓어진 논리 화면에서는 `:150-165`의 「화면 안 바깥 좌표」 갈래가 실제로 돈다. 지금까지는 화면 밖 좌표 갈래만 돌았다.
4. 복원: `[UnitySetUp]` 또는 테스트 첫머리에서 `_savedDensity = ScreenCoordinateConverter.AutoUiDensityScale`를 저장한다.
   - `CloseEverything`(`:79`)에서 `_savedDensity > 0 ? ReportUiDensityScale(_savedDensity) : ClearReportedUiDensity()`로 되돌린다. 선례는 `UiDensityCanvasScaleTests.cs:50-60`이다.
   - 복원 직후 `Assert.AreEqual(_savedDensity, AutoUiDensityScale)`를 **자기 검증**으로 둔다. 복원 누락은 다음 픽스처를 조용히 오염시킨다.
5. 좁은 화면에서의 거동(칩 접힘)에 대한 단언은 **ux-designer 답이 오기 전에는 넣지 않는다.** 한쪽 답을 미리 잠그면 결정을 테스트가 대신하게 된다.

### 5-5. ux-designer 질문 (명세에 그대로 전달)
> 정보창 헤더의 [설정] 칩은 「패널 폭 < 탭 스트립 오른끝 + 132pt」이면 접힌다(`CharacterInfoWindow.Layout.cs:194-200`). ce3b102에서 [DLC] 탭이 더해져 문턱이 약 58pt 올라갔다. 탭 시작점은 **캐릭터 이름 폭**에 따라 밀린다(`:1452-1453`, 이름 칸 최대 200pt). 추정으로는 논리 폭 약 650pt 미만, 긴 이름이면 약 750pt 미만 화면에서 칩이 사라진다(R1 실측 대기).
>
> 그때 **macOS 사용자에게는 설정창으로 가는 마우스 경로가 하나도 남지 않는다.** 근거는 셋이다. 메뉴바 상태 아이템은 미구현이다(`Platform/SystemTrayPresencePolicy.cs:23-34`). 부채꼴은 [집중 모드]/[캐릭터]/[오늘 할일]/[행동]/[앱 종료] 5개로 설정이 없다. 남는 것은 전역 단축키뿐이다. Windows는 트레이 `OpenSettings`(`:152`, `:179`)가 남는다.
>
> 코드 주석도 [설정]을 「설정창의 유일한 GUI 경로」라 부른다(`CharacterInfoWindow.cs:148`). 좁은 창에서 이 입구가 사라지는 것을 다음 중 어느 쪽으로 할지 판단해 달라.
> - **(가)** 허용한다 — 드문 화면, 단축키로 충분
> - **(나)** [설정]은 [✕]처럼 접지 않는다. 대신 탭과 겹치지 않게 탭 쪽을 줄인다(라벨 축약, 이름 말줄임 등). 겹치면 탭을 눌렀는데 설정이 열리는 오배선이 난다(`Layout.cs:188-191`).
> - **(다)** 접힐 때 대체 입구를 연다(헤더 넘침 메뉴, 또는 부채꼴에 설정 추가)
>
> 이 앱의 4플랫폼 목표 중 iPhone은 논리 폭이 390pt 안팎이다. 모바일 백드롭 모드가 같은 정보창을 쓴다면 이 상황이 상시가 된다(모바일 사용 여부는 미확인).

### 5-6. 변이
| 변이 | 주입 | 기대 |
|---|---|---|
| S1 | 설정창 닫기에서 밀어낸 정보창 복귀 삭제 | `:114`·`:182` 빨강(M8 본래 목적) |
| S2 | `SyncHeaderChips`의 [설정] 조건을 항상 거짓 | 수정본은 전제 `Assert` **빨강 + 측정값 출력**. 옛 6번은 판정 불가(안 보임) |
| S3 | 테스트 복원 줄 삭제 | 자기 검증 단언 빨강 |

### 5-7. R1 진단 설계
1. 진단 사본(커밋 안 함). 두 테스트 앞에서 로그를 남긴다.
   - `Screen.width/height`, `_info.CanvasScaleForTests`, `AutoDpiScale`, `AutoUiDensityScale`, `_info.PanelScreenRect`, [설정] 칩 activeSelf
   - 헤더 `TabStrip`의 월드 코너 오른끝. 진단 전용 이름 조회라 커밋하지 않는다.
   - 그 값 + 132와 패널 폭의 비교
2. **반사실 대조**(격리 미러, 트리 아님): `TabTable`에서 `DLC` 행만 뺀 미러로 같은 로그를 낸다. 칩이 보이고 두 테스트가 초록이면 ce3b102 인과 확정이다.
3. **이름 길이 대조**: `CharacterProgressionModel.SetCharacterName(…)`(`Core/CharacterProgressionModel.cs:193`, 속성 세터는 private `:82`)으로 10자 이름을 넣은 회차로 문턱 이동을 실측한다. 끝나면 원래 이름으로 복원한다(격리 저장 경로라도 같은 실행의 다음 픽스처가 읽는다). 5-5 질문의 수치로 쓴다.
4. 수정본(밀도 0.5): 두 테스트 초록, S1·S2·S3 각각 빨강.

---

## 7. `AccessoryFillRenderingTests.왕관은_채워지되_얹는_물건으로_남는다`

### 7-1. 현재 단언
- `Tests/PlayMode/AccessoryFillRenderingTests.cs:213-216`
  ```csharp
  HandoffPlayModeGate.SkipIfHandoffRendered(container, "…", "CrownBody");
  ```
- 실패 자리 `Tests/PlayMode/HandoffPlayModeGate.cs:103-106`
  ```csharp
  Assert.IsTrue(hasHandoff,
      $"{LogPrefix} '{root.name}' 아래에 v1 도형('{v1ShapeName}')도 인계본 조각('{HandoffPiecePrefix}…')도 " + …
  ```
- 실측 이름: `[HeadAttached, HeadWornSpriteBack, HeadWornSpriteFront]`

### 7-2. 원인 재확인 — qa 가설 **맞음**
- `Resources/Items/equip_head_crown.asset:64-65` `wornSpriteOverride`, `wornSpriteBackOverride`가 채워져 있다(7f41eef).
- `Interaction/CharacterAccessoryRenderer.cs:1055` `if (TryAppendWornSprites(slot, item)) continue;` → 벡터 경로(`CrownBody`·`Piece_*`)를 건너뛴다.
- `:1194` `new GameObject($"{slot}WornSprite{suffix}")`, `:1201` `AddComponent<SpriteRenderer>()`
- 게이트는 v1 / 인계본 / 둘 다 없음의 세 갈래다(`HandoffPlayModeGate.cs:29-37`). 비트맵 갈래가 없어서 셋째(실패)로 떨어진다.
- 이력: 09-09 02:12Z(`coder_wornbitmap_play1`)까지 Skipped:Ignored(인계본 갈래), 09-14에 실패.

### 7-3. 수정안 — 조건부 `Assert.Ignore` (존폐는 열지 않음)
**위치**: `AccessoryFillRenderingTests.cs:197`(`fills` 계산) 다음, `:199`(인계본 주석 블록) **앞**. 게이트 호출보다 먼저 둔다.

```csharp
// ★ 착용 비트맵 갈래 — 7f41eef가 몸 표면에 비트맵을 배선했고 44c42bb에서 사용자 지시로 파일럿 전체 중단·대기.
Sprite wornFront = ItemCatalog.WornSprite(EquipmentSlot.Head, /*기존 착용 인덱스 그대로*/ 3);
if (wornFront != null)
{
    // 존재 단언(양성 대조) — 카탈로그가 「비트맵」이라 말하면 그 스프라이트가 실제로 그려져 있어야 한다.
    SpriteRenderer drawn = null;
    foreach (SpriteRenderer sr in container.GetComponentsInChildren<SpriteRenderer>(true))
        if (sr.sprite == wornFront && sr.enabled && sr.gameObject.activeInHierarchy) { drawn = sr; break; }
    Assert.IsNotNull(drawn,
        $"{LogPrefix} 왕관 착용 비트맵이 카탈로그에 배선됐는데 몸에 그려지지 않았습니다 — 건너뛰기 전에 이 자리가 깨졌습니다.");

    Assert.Ignore(
        "왕관 비트맵 파일럿 중단 44c42bb — 존폐 결정 대기, 7f41eef 배선과 게이트 갈래 불일치. " +
        "잃는 것: 왕관 채움(37-6 규칙 6)·얹는 물건(채움이 턱까지 안 내려옴)·«채움 수 = 표식 수». " +
        "재개 시 첫 확인: 출하 트리의 몸 표면은 레벨 20+ 착용자에게 G1 기각 비트맵을 그리고, 초상·카드는 벡터를 그린다(몸≠초상). " +
        "되살림: equip_head_crown.asset의 wornSprite 두 칸을 비우면 이 갈래를 지나 게이트가 다시 돈다.");
}
```
- 식별자 니들이 없다. 판정은 **공개 카탈로그 값**(`ItemCatalog.WornSprite`, `Core/ItemCatalog.cs:778`, public)과 **스프라이트 참조 동일성**으로만 한다. `"HeadWornSpriteFront"` 같은 조립 이름은 쓰지 않는다.
- `3`은 이 메서드 `:192` `Wear(EquipmentSlot.Head, 3, …)`에 이미 있던 리터럴이다. 부채를 늘리지 않고 같은 값을 쓴다. 기존 부채로 남는다.
- 게이트 파일 `HandoffPlayModeGate.cs`는 **수정하지 않는다.** `IgnoreGateInventory`의 `CallSites = 12`와 `Tokens = 1`(`TestClaimExpiryAuditTests.cs:1365-1368`)이 불변이다.
- **명부 등재 필수**: `TestClaimExpiryAuditTests.IgnoreInventory()`(`:603-`)에 한 항목을 넣는다. 안 넣으면 `Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다`가 빨개진다.
  ```csharp
  new IgnoreEntry {
      File = "AccessoryFillRenderingTests.cs",
      Method = "왕관은_채워지되_얹는_물건으로_남는다",
      Kind = RatchetKind.자동,
      Why = "왕관 비트맵 파일럿 중단(44c42bb, 사용자 지시) — 7f41eef 착용 비트맵 배선과 PlayMode 인계본 게이트 갈래 불일치. " +
            "★ 장치: ItemCatalog.WornSprite(Head, 왕관)가 null이 되면(에셋 두 칸 비움 = 롤백) Ignore 앞 조건이 거짓이 되어 " +
            "게이트가 다시 돈다. 배선됐는데 안 그려지면 Ignore 앞 존재 단언이 빨개진다.",
  },
  ```
  `RatchetKind.없음` 상한(`MaxIgnoresWithoutRatchet = 1`, `:74`)을 소모하지 않는다.

### 7-4. 변이
| 변이 | 주입 | 기대 |
|---|---|---|
| K1 | 에셋 `wornSpriteOverride` 비움(롤백 모사, 격리 미러) | Ignore를 지나 게이트 인계본 갈래로 간다 → 09-06~09-09와 같은 Skipped:Ignored(옛 사유). 되살림 장치 생존 |
| K2 | `TryAppendWornSprites`가 `AddWornSprite`를 부르지 않고 `true` 반환 | 존재 단언 **빨강** |
| K3 | `AddWornSprite`에서 `sr.enabled = false` | 존재 단언 **빨강** |

### 7-5. 재개 시 첫 확인 항목 — 착용 비트맵이 **어떤 조건에서 실제로 그려지는가** (사실(코드))
1. **그려지는 조건**
   - HEAD 슬롯에 왕관(`itemIndex: 3`)을 착용해야 한다.
   - `ShouldDraw` = `IsEquipped && IsUnlocked`(`CharacterAccessoryRenderer.cs:1265-1266`)
   - 보유 조건 = 레벨 ≥ `requiredLevel: 20`(에셋 `:20`) 또는 구매 또는 디버그 전체 해제(`EquipmentModel.cs:269-272`)
   - 이 조건이 서면 몸 표면은 **항상** 비트맵이다. 기능 플래그나 설정 스위치는 없다. `TryAppendWornSprites`(`:1096-1115`)는 앞장 스프라이트가 null이 아니고 슬롯 정렬 해석이 성공하면 곧바로 참이다.
2. **초상화와 카드는 벡터 그대로다**(`ItemCatalog.cs:773-776` 「소비자는 몸 표면 하나뿐이다」, 초상은 `CharacterPortraitStage.cs:1354` `AccessoryShapeBuilder.Append`) ⇒ 착용자는 **바탕화면 캐릭터에서 G1 기각 비트맵 왕관**을 보고, **정보창 초상과 카드에서 옛 벡터 왕관**을 본다. 「몸과 초상화가 같은 그림」 불변식이 장비 쪽에서 깨진 상태다.
3. **사용자 손에 간 빌드에 들어 있는가**: `git merge-base --is-ancestor 7f41eef a6b3101` → **포함**이다. `3cc6753`, `17f6f38`, `7900ad0`도 포함이다.
   - qa가 `WINDOWS_CHECK_SESSION.md` §R에서 사용자 대기 zip을 `a6b3101`(2차)로 판독했다 ⇒ **그 zip을 실행해 레벨 20 이상에서 왕관을 쓰면 기각된 비트맵이 뜬다**(zip 내부 PNG 포함 여부는 미확인, 에셋 참조라 포함으로 추정).
4. 롤백 수단은 에셋 두 칸 비우기다(`CharacterAccessoryRenderer.cs:1044-1046` 「빈칸 하나가 곧 롤백이다」). 코드 변경 0이고, 이 테스트의 Ignore는 자동으로 풀린다(K1).

---

## 8. 추가 — 부분 실행이 전량처럼 보인 경로를 막는 테스트 쪽 장치 (제안, 리더 판정)

`regress.sh`의 G5(`testcasecount != total` → 부분 실행, `:450`)는 **Unity 필터 실행을 못 본다.** 필터로 3건만 돌린 `mut-M5p.xml`도 `testcasecount="3" total="3"`이다. G4는 하한 숫자에 기대므로 하한을 낮게 잡은 라운드에서는 역시 조용하다.

두 개의 독립된 자를 제안한다.
- **(1) `regress.sh report`**
  - `Tests/PlayMode` 소스에서 `[Test]`와 `[UnityTest]` 선언을 센 **리프 기대 수**(TestCase 전개 포함, 전량 xml 783으로 교정)를 xml `total`과 비교한다.
  - 모자라면 첫 줄에 `부분 실행 N/M — 전량 아님(PlayMode 전량 마지막: <커밋·시각>)`을 찍고 전량 판정 rc와 다른 종료코드를 낸다.
  - 마지막 전량 커밋과 시각은 `docs/verify/BASELINE.md`의 PlayMode 전량 행에서 읽는다.
- **(2) `GlobalPlayModeTestIsolation`의 `OneTimeTearDown`**
  - 이번 실행에서 실제로 돈 리프 수(스위트 콜백 계수)와 PlayMode 어셈블리 리플렉션 리프 수를 한 줄에 함께 찍는다: `[테스트격리] 실행 리프 N / 어셈블리 리프 M(부분 실행)`.
  - 판정은 하지 않고 로그만 남긴다. 계수는 C# 콜백이, total은 NUnit이 세므로 서로 다른 자다.

리더의 `Assets/` 커밋 게이트(TEAM.md 「상시 백테스팅 공백」)는 (1)의 배너가 없는 xml, 즉 **그 커밋 묶음 이후 시각**의 전량 xml을 증거로 요구하면 된다.

- 한계: 리플렉션 계수는 `[TestCaseSource]` 전개와 `[Ignore]` 속성 테스트의 계산에서 xml과 어긋날 수 있다 → 전량 xml로 먼저 교정한다.
- 이 장치는 「6일 17시간」 같은 **시간 공백**을 직접 막지는 않는다. 보고서가 부분 실행을 전량이라 말하지 못하게 할 뿐이다.

---

## 9. 자백 · 미확인

1. zsh에서 `grep --include=*.cs`를 따옴표 없이 5번 돌렸고, 전부 `no matches found`로 **아무것도 검색하지 못했다.** 출력이 「0건」처럼 생겨 죽은 프로브였다 → 따옴표를 붙여 재실행했다.
2. 풍선 원인으로 처음 세운 「집중 관망 자세 기울임」 가설은 `StickmanBlackboard.cs:2524`(집중 세션 중에만)로 **기각**했다. 문서에는 최종 가설(두리번)만 남겼다.
3. `EyeBack`/`EyeFront`의 프로덕션 실재를 첫 grep(`head -20` 절단)에서 못 봐서 「죽은 니들」로 오판할 뻔했다 → `CharacterPortraitStage.cs:1139-1140` 원문으로 실재를 확인했다.
4. qa 재실행 계획 R1~R6의 **정의 원문을 찾지 못했다**(Tasklist와 `scratchpad/qa-sfx`에 없음). R1·R2·R3은 리더 판정 ③의 대응만 따랐고 **R4·R5 대응은 미확인**이다.
5. 수치 추정: 탭·이름 폭(14pt 가정), 배치모드 캔버스 배율 1, 풍선 부족분 저역통과 근사, 두리번 겹침 확률 1/3 — **전부 미측정**이다(R1·R2가 잰다).
6. UnityTest `yield return null` 재개 지점(Update 뒤, LateUpdate 앞)은 Unity 문서의 일반 규칙에 기댔다. 이 버전에서는 미실측이다(R3 ①).
7. Slit Visor·Dust Goggles 에셋이 R3판인지 R4판인지 대조하지 않았다. 두 눈 커버는 추정이다.
8. `CostumeFocusPropLifecycleTests` 생성 커밋을 `git log -- <경로>`로 잡지 못했다(경로 이력 비어 있음, 이름 변경 추정). 「처음부터 실패」는 이력 xml 2회에만 근거한다.
9. (2026-09-15) zsh에서 `--include=*.cs`를 따옴표 없이 **또** 썼다. 자백 1과 같은 형태다. `CostumeFocusRhythm` 위치 검색이 `no matches found`와 빈 경로로 끝났고 출력이 「0건」처럼 생겼다 → 따옴표를 붙여 다시 돌렸다(`Core/CostumeKeyposeTableSO.cs:219`).
10. (2026-09-15) §4-2의 두리번 겹침 확률 1/3 추정이 관측(이번 4회 중 3회 빨강)보다 낮다(§4-7).
11. (2026-09-15) #8의 H1(경계 보행)은 **러너로 확인하지 않았다.** 기존 로그로는 가를 수 없었다(§11-4). 코드로 닫힌 것은 쓰기 시각표와 Still 도달 2.00초까지다.
12. (2026-09-15) 브리프의 「관측 창 전체 8~10회」는 **누적 계기의 끝값**이었다. 창 안에서 쓴 횟수는 7회(통과) 또는 9회(빨강)다(§11-2). 전달 과정에서 생긴 표기 차이일 수 있다. verify-change 원문과는 대조하지 않았다.
13. (2026-09-15) 두 가지는 verify-change 실측을 **인용만** 했다: 「V4를 잡는 유일한 방어」, 「E-4 코드 경로 미실행(로그)」. 이 문서는 이름·결과(xml)와 커밋 파일 목록(git)만 다시 쟀다.
14. (2026-09-15 #9) 브리프에는 메시지가 「But was: 5.0」로 적혀 있었다. xml 원문은 `But was:  5.00002861f`다. 의미는 같다(루프가 예산 5를 넘긴 마지막 프레임의 초과분). 이 문서에는 원문을 적었다.
15. (#9) 「5-d 코드 경로 미실행」은 dev-platform 로그 확인을 **인용만** 했다. 이 문서가 다시 잰 것은 트리 변경 파일 목록(git)과 같은 트리 해시에서의 격리 결과(xml)다.
16. (#9) H9-a(구간 만료)와 H9-b(경계 정지 재진입)를 **기존 로그로 가르지 못했다**. 빨강 회차 로그 「배회 페이즈 잔여 0.03초, 이동의도 0.00」은 휴식과 경계 정지를 구분하지 않는다(`AutoWanderController.cs:212-217`). 두 페이즈 모두 이동의도가 0이다.
17. (#9) 구간 길이 약 3.75초는 **상한 추정**이다. 클램프 여백과 런타임 배율을 재지 않았다. 반환점 X도 `[눈추적]` 표본이 드문드문해 확정하지 못했다.
18. (#9) `de3fb32` 이전 0.08초 모양(12-2 표 8회, 09-15 저장소 전체 로그 재셈 37회)이 구간 1.2초에서 왜 경주를 매번 재현했는지는 H9-a 산술(구간 잔여 > 1.05초 필요)로 **설명되지 않는다**. 미확인이다(§12-4).
19. (#9) 처음 로그를 읽을 때 「[되올라가기] 안착 … 8.0초 동안 유예」 줄을 보고 **음성 대조에서도 쿨다운이 켜졌다고 오독할 뻔했다.** 그 줄은 원본 설정을 든 에이전트 자체 컨트롤러가 찍는다(`EdgeHopDownTests.cs:434-437`, `AutoWanderController.cs:272-273`·`:1400-1403` `Cfg`는 자기 `_config`를 읽는다). 시험 컨트롤러는 쿨다운 0이라 `:273`에서 로그 없이 돌아간다.
20. (#9, verify-change 조건부 수정) §12-3 (나)에서 「경로 길이가 바뀐 시점과 맞는다」만 적고 **같은 기간의 교란 커밋 8개를 병기하지 않았다.** 시점 일치로 `de3fb32`를 특정한 것처럼 읽혔다 → 재측정 표를 넣고 「유력 후보」로 좁혔다.
21. (#9) 교란 커밋 재측정 첫 시도에서 zsh의 따옴표 없는 `$F`가 단어로 쪼개지지 않아 git이 경로 하나로 받았고 **0건**이 나왔다. 양성 대조(`de3fb32` 적중 0)가 죽은 프로브를 잡았다 → bash 배열로 다시 쟀다. 자백 1·9와 같은 가족이다.
22. (#9) 「전체 로그에는 109건」을 `[스톨귀인]` 수로 적었다. 109는 `[스톨` 접두사 전체이고, `[스톨귀인]`은 74건이다(구간 35).
23. (#9) 「12/12」와 「모양은 둘뿐(이봉)」에 **범위를 적지 않았다.** 09-06 이후에서만 참이다. 09-01 이전에는 다른 모양(체류 0.48~0.56초, 0.16초)이 있다(verify-change 적발, 이 문서가 전수 재셈으로 확인).
24. (#10) 「5-d 도달 0」의 로그 근거(`[작업표시줄]`·`[종료순서]` 0)는 **전체 `P.log`에도 0**이다. 그 태그가 이 실행에 나올 수 있다는 양성 대조가 없어 죽은 프로브일 수 있다. 판정은 변경 파일 목록과 테스트 파일 참조 0으로 했다.
25. (#10) 브리프의 「과거 실패 08-31 `coder_36_play`」는 xml상 이 테스트가 `Failed`가 맞다. 그러나 **밀착 오차 빨강이 아니다** — 사유는 `[UiChrome]` 그림자 오류 로그의 `Unhandled log message`이고, 그 실행에서 픽스처 5건이 전부 같은 사유로 빨갰다. 밀착 오차 빨강은 3회(09-01·09-08·09-15)다.
26. (#10) §13-3의 「오차 = 속도 × dt」 등식은 코루틴 `yield return null`이 Update 뒤에 재개된다는 전제에 기댄다. §3-2·자백 6과 같은 미실측 전제다. 러너 판별(§13-7 ②)이 매 프레임 일치로 잰다.
27. (W1) Unity Mono Windows 플레이어·에디터에서 `DateTime.UtcNow`의 실제 해상도를 문서로도 실측으로도 확인하지 않았다. 「1~15.6ms」는 브리프 전달값이다.

## 10. 플랫폼 영향

- **Windows 영향**
  - 1·2·3·4·7: 없음(테스트만 수정, 플랫폼 분기 0).
  - 5·6: 수정본이 `ReportUiDensityScale`을 쓴다. Windows에서는 플랫폼이 실제 밀도를 보고하므로 **저장값 복원이 `Report(saved)`여야 한다**(5-4 ④). 「Clear」로 복원하면 Windows 러너에서 이후 픽스처가 오염된다.
  - UX 질문: Windows는 트레이 `OpenSettings`가 남아 마우스 경로가 소실되지 않는다.
  - 7: 비트맵 경로는 플랫폼 중립이라 **Windows 빌드(a6b3101 zip)에도 뜬다.**
- **macOS 영향**
  - 5·6: 좁은 논리 폭에서 설정창 마우스 입구가 소실된다(메뉴바 아이템 미구현).
  - 7: 동일하게 뜬다.
  - 나머지는 없음.
- 프로덕션 코드 수정 제안: **0건.** 7번 에셋 롤백은 사용자 결정 대기다. 테스트 수정은 PlayMode 테스트 어셈블리와 EditMode 명부 파일에 한정되고 `#if` 분기가 없다. 그래도 적용 라운드에서 격리 미러 osx·win 교차 컴파일 0 에러를 확인한다.
- ★ 2026-09-15 추가
  - **Windows 영향**: #8의 기전 파일(`FramePacing` 판정 경로·`CostumeFocusRhythm`·포즈 층·배회)은 전부 플랫폼 중립이고 테스트에 `#if`가 없다. ⇒ Windows 러너에서 돌리면 같은 위상 간헐이 날 것으로 판단한다(미확인, Windows 러너에서 이 테스트를 돈 기록이 없다). 수정안 A는 테스트만 바꾸므로 영향 없음이다. #4 §4-7은 기준값 확정뿐이라 영향 없음이다. 분류 규칙은 러너 판정 절차라 플랫폼과 무관하다.
  - **macOS 영향**: #8은 이 머신(macOS 배치모드)에서 관측된 간헐이다. 사용자 체감 영향은 없다 — 정지 구간에 코스튬 쓰기가 0인 것은 절감 설계 그 자체다(§11-3 나). 프로덕션 수정 제안은 여전히 0건이다.
- ★ 2026-09-15 #9 추가
  - **Windows 영향**: 없음(테스트만 대상). 기전 파일(`AutoWanderController`·`ParkourClimbState`·`StickmanBlackboard`·`StickConfig`)은 플랫폼 중립이다. 테스트에는 `#if`가 0개다(grep 계수 0). 리그는 합성 발판 3장이라 Windows 작업표시줄의 전폭 구조와도 무관하다. ⇒ Windows 러너에서도 같은 위상 간헐이 날 것으로 판단한다. 다만 Windows 러너에서 이 테스트를 돈 기록이 없어 미확인이다. 수정안 A·B는 PlayMode 테스트 파일 하나만 바꾼다.
  - **macOS 영향**: 이 머신(macOS 배치모드) 전량에서 관측된 간헐이다. 사용자 체감 영향은 없다. 음성 대조는 **수정을 끈** 옛 경로를 재현하는 장치다. 배포 기본값은 쿨다운 8초 켬(`DefaultStickConfig.asset:113`)이다. 쿨다운을 켠 짝 테스트 (5) `AutoWanderStaysOnDock…`는 `P1`과 격리 ×3 모두 `Passed`였다. (5)는 맨틀 신호로 방향을 강제(`AutoWanderController.cs:275-277`)하므로 이 위상에 기대지 않는다(사실(코드)). 프로덕션 수정 제안은 0건이다.
- ★ 2026-09-15 #10 · W1 추가
  - **Windows 영향**: #10 — 기전(코루틴의 커서 갱신 순서, 프레임 dt)은 플랫폼 중립이고 테스트에 `#if`가 0개다. 프레임이 긴 러너일수록 빨강이 잦을 것으로 판단한다(미확인). 수정안 A는 테스트 파일 하나만 바꾼다. W1 — **Windows에서 먼저 드러날 위험**이다(§14). QG-1 Win64 EditMode 실행 전에 처리를 권고한다.
  - **macOS 영향**: #10 — 이 머신 전량에서 관측됐다. 사용자 체감은 없다(실사용 커서는 에이전트가 그 호출 시점에 OS에서 읽으므로 이 한 프레임 지연이 없다 — 추정). W1 — macOS의 현재 초록은 이 위험을 가리지 못한다(시각 해상도가 더 세밀할 것으로 추정). 프로덕션 수정 제안은 0건이다.

---

## 11. `CostumeFocusStillTierTests.몰입기_동안_절감등급_Still에_도달한다` — 간헐 #8 (2026-09-15 추가)

- 이 절이 한 일: 테스트·프로덕션 소스 읽기, 로그 인용, 산술. **Unity 0회, 트리 편집 0회. 원인을 러너로 확인하지 않았다.** 코드로 닫히는 부분은 사실(코드), 회차별 위상은 추정으로 나눠 적는다.
- 기준 코드: HEAD `e6b14c2`. 인용 파일은 머리말의 `7900ad0..e6b14c2` 변화 0 확인에 포함된다.

### 11-1. 현재 단언
`Tests/PlayMode/CostumeFocusStillTierTests.cs:359-362`
```csharp
Assert.Greater(poseWritesAtStill, poseWritesAtStart,
    $"{LogPrefix} Still에 도달한 시점까지 코스튬 포즈가 한 번도 쓰지 않았습니다 " +
    $"({poseWritesAtStart} -> {poseWritesAtStill}). 그렇다면 이 Still은 «코스튬이 돌면서도 " +
    "절감이 산다»의 증거가 아니라 «코스튬이 안 돌았다»의 증거입니다.");
```
- `poseWritesAtStart = bb.CostumePoseWriteCount`(`:291`)는 관측 직전 값이다. `poseWritesAtStill`(`:314`)은 `CurrentTier()`가 처음 `Still`인 프레임의 값이다.
- 관측은 `TestClock.SampleForSeconds(3f * dwell, …)`(`:287`, `:296`)다. `TestClock.cs:58-61`이 `elapsed += Time.unscaledDeltaTime`으로 **벽시계** 예산을 잰다.

### 11-2. 실측 — 로그 `[코스튬절감-TEST] 몰입기 관측` 줄 원문
| 실행 | 결과 | 프레임 / 벽시계 | Still 도달 | 누적 계기 시작 → 끝 | Still까지 쓰기 |
|---|---|---|---|---|---|
| `Logs/vc-e4/P-full` | 빨강 | 62,493 / 4.80초 | 2.00초 | 1 → 10 | 0 |
| `Logs/vc-e4/iso-…-1` | 빨강 | 67,011 / 4.80초 | 2.00초 | 1 → 10 | 0 |
| `Logs/vc-e4/iso-…-2` | 통과 | 67,170 / 4.80초 | 2.00초 | 1 → 8 | 3 |
| `Logs/vc-e4/iso-…-3` | 통과 | 65,332 / 4.80초 | 2.00초 | 1 → 8 | 3 |
| 이전 전량 4회: `docs/verify/runs/part2-final_play` · `Logs/coder-onbstore/play-full` · `Logs/coder-onbstore/s4-playfull` · `docs/verify/runs/vc-e12_play` | 통과 | 64,390~66,032 / 4.80초 | 2.00초 | 1 → 8 | 3 |
| `scratchpad/dp5d/xml/P1`(5-d 전량, 09-15 추가 표본) | 통과 | 65,781 / 4.80초 | 2.00초 | 1 → 8 | 3 |

- 09-15 추가 표본: `P1` 로그 `:4857`·`:4858` 원문이 「코스튬 포즈 쓰기 1 -> 8」과 「그 시점까지 코스튬 포즈 쓰기 3회」다. 통과 모양 그대로다. 누적 관측은 9회 중 빨강 2회다.

- ★ 정정: 「관측 창 전체 8~10회」는 누적 계기의 **끝값**이다. 창 안에서 쓴 횟수는 통과 7회, 빨강 9회다.
- **모양은 둘뿐이다.** 통과 6회는 전부 `1→8 / 3회`, 빨강 2회는 전부 `1→10 / 0회`다. 연속 분포가 아니라 이봉이다.
- **빨강 쪽이 창 전체로는 더 많이 썼다.** 메시지의 추론 「코스튬이 안 돌았다」와 정반대다.

### 11-3. 기전 — 코드로 닫히는 부분 (사실(코드))
**(가) Still 도달 시각은 폴링 위상에 고정된다 → 늘 2.00초.**
- `FramePacing.cs:152-180` `ResetForTests`가 `_presencePollTimer = 0`, `_idleDwellSeconds = 0`, `_tierChangedAtTime = −∞`로 되돌린다. 테스트는 관측 직전에 부른다(`:255`).
- `:632-640`: 폴링 타이머가 `PresencePollActiveSeconds = 0.5f`(`:355`)에 닿을 때만 `EvaluateAdaptiveTier`가 돈다.
- `:664` `characterStill = characterIdle && _idleDwellSeconds >= StillDwellSeconds`, `StillDwellSeconds = 1.6f`(`:395`)
- ⇒ 평가 시각 0.5 / 1.0 / 1.5 / 2.0초 중 dwell ≥ 1.6인 첫 평가 = **2.0초**다.
  - 하강 제동 `TierDescendCooldownSeconds = 1f`(`:435`, `:701`)는 걸리지 않는다. 첫 적용(0.5초, 로그 「등급 Active -> Active」)에서 1.5초 뒤이기 때문이다.
- 프레임 수와 무관하다(4.80초에 62k~67k 프레임). **CLAUDE.md의 프레임 예산 금지 규칙 위반은 이 테스트에 없다.** `Assert.Greater(frames, 30)`(`:330`)은 비공허성 하한일 뿐이다.

**(나) 코스튬 계기는 「층이 돌았다」가 아니라 「스텝이 바뀌어 썼다」를 센다.**
- `StickmanBlackboard.cs:2735` `if (CostumePoseWroteThisFrame) CostumePoseWriteCount++;` — 값은 `ApplyCostumeFocusStepPose`의 반환값이다.
- `StickmanPoseAnimator.cs:3224` `if (HoldsCostumeStepAlready(settings, step, offsetY)) return false;` — 리그가 이미 그 키포즈를 들고 있으면 쓰지 않는다(PC-4).
- 프로덕션 문서가 둘을 이미 갈랐다.
  - `StickmanBlackboard.cs:2648-2651` 「돌았다」와 「썼다」는 다르다.
  - `:2668-2671` 「스텝 사이 프레임의 쓰기가 0」이 절감의 증거다.
- ⇒ 테스트 메시지(`:360-362`)의 「쓰기 0 = 코스튬이 안 돌았다」는 **프로덕션 계약과 어긋난 추론**이다.

**(다) 몰입기 첫 소구간에는 설계상 3.2초 무쓰기 구간이 있다.** 전부 프로덕션 상수와 에셋에서 유도했다.
- 세션: `CostumeFocusRig.cs:70` `SessionMinutes = 1f` → 60초
  - `FocusSessionPhase.cs:75-78` 가장자리 = min(clamp(0.20×60, 60, 300), 0.40×60) = **24초**
  - `:83-84` 몰입기 = 60 − 48 = **12초**. 로그 「몰입기 길이 12.00초」, 「Adapt → Immersion (경과 24초…)」와 일치한다.
- 리듬(`Core/CostumeKeyposeTableSO.cs`)
  - 소구간 = 12/3 = 4초(`:252-258`)
  - 주기 = clamp(12/3/3 = 1.33, 4, 8) = **4초**(`:268-273`). 상한 설정은 `costumeFocusDutyCycleMaxSeconds: 8`(`Data/DefaultStickConfig.asset:216`)
- 표: `Resources/Items/CostumeKeyposeTable_office.asset:15` `stepsPerSecond: 3`, 키포즈 3개, `:38` `stageKeyposeStart: []` → 창은 0번부터 3개(`StageWindow` `:182-194`)
- 소구간별 작업 구간 W(`:282-294`, 루프 = 키 3개 ÷ 스텝레이트)

  | 소구간 | 스텝/초 | 루프 | 듀티 | 루프 수 floor(듀티×4÷루프 + 0.5) | W |
  |---|---|---|---|---|---|
  | 0 진입 | 3 | 1.0초 | 1/3 | floor(1.833) = 1 | 1.0초 |
  | 1 절정 | 5 | 0.6초 | 1/2 | floor(3.833) = 3 | 1.8초 |
  | 2 이완 | 3 | 1.0초 | 1/6 | floor(1.167) = 1 | 1.0초 |

- `StickmanBlackboard.cs:2717-2722`: `phase = Repeat(몰입 경과, 주기)`, `working = phase < W`, 정지 구간의 스텝은 0이다.
- ⇒ 몰입 경과 기준 **쓰기 시각**(스텝이 바뀌는 순간만)
  - 0(첫 적용) · 0.333 · 0.667 · 1.0
  - 4.2 · 4.4 · 4.6 · 4.8 · 5.0 · 5.2 · 5.4 · 5.6 · 5.8 (9회). 4.0초는 정지 스텝 0 → 작업 스텝 0이라 쓰지 않는다.
  - 8.333 · 8.667 · 9.0
- 무쓰기 구간: **1.0→4.2 = 3.2초**, 5.8→8.333 = 2.53초, 9.0→12.0 = 3.0초. **최장 3.2초 > Still 도달 2.0초.**

### 11-4. 가설 — 관측 시작 위상 (추정, 러너 미확인)
관측 시작 시점의 몰입 경과를 τ0라 하자. 위 쓰기 시각표에서 관측된 두 모양이 **정확히** 나온다.

| τ0 구간 | Still까지 (τ0, τ0+2.0] 쓰기 | 창 (τ0, τ0+4.8] 쓰기 | 누적 계기 | 해당 관측 |
|---|---|---|---|---|
| [0, 0.2) | 0.333·0.667·1.0 = **3** | 3 + 4.2·4.4·4.6·4.8 = **7** | 1 → 8 | 통과 6회 전부 (★ 2026-09-15 verify-change 정정: [0.2, 0.333)은 5.0초 쓰기가 창에 들어와 `3/8` = `1→9` — 미관측) |
| [1.0, 2.2) | **0** | 4.2~5.8 = **9** | 1 → 10 | 빨강 2회 전부 |

- 두 구간 밖의 τ0는 다른 모양을 낸다. 예를 들어 [0.333, 1.0)이면 Still까지 1~2회다. 8회 중 0회 관측됐다.
- **H1 — 경계 보행: τ0가 1.0초 이상 밀리는 이유.**
  - 적응기 24초에는 배회가 돈다. 몰입기 배회 확률 0(`costumeImmersionWalkChance: 0`, `DefaultStickConfig.asset:217`)은 **다음 추첨부터** 적용된다(`AutoWanderController.cs:471-478`, 테스트 자신의 상수 문서 `:87-89`).
  - 경계 프레임에 걷고 있었으면 `WaitForGroundedIdle`(`:244` → `CostumeFocusRig.cs:352-360`)이 그 에피소드 끝까지 기다린다. 그동안은 Idle이 아니라서 코스튬 층이 돌지 않는다(`StickmanBlackboard.cs:2524` 게이트, §3-3).
  - Idle에 드는 첫 프레임에 스텝 0을 한 번 쓴다 → `poseWritesAtStart = 1`로 **통과 회차와 같은 값**이 된다. 시작값만으로 두 경우를 가를 수 없는 이유다.
  - 보행 에피소드 1.5~4초(`DefaultStickConfig.asset:122-123`) 중 남은 시간이 1.0~2.2초면 빨강이 된다.
- 기존 로그로 가를 수 있는가 — **가를 수 없다(자백).**
  - `[눈추적]` 표본이 경계 프레임을 찍지 않는다.
  - 「마지막 `[눈추적]` 몸통 x → 프롭 앵커 x」 이동은 빨강 2회(iso-1 6.71→3.22, P-full 0.82→8.77)에도, 통과 2회(iso-2 0.00→2.70, vc-e12 0.00→5.25)에도 있었다.
  - 프롭은 진입 프레임의 몸통 x에 선다(iso-3 1.05→1.05, onbstore −6.71→−6.71). 그래서 「경계 전에 걸었다」까지만 말할 수 있고 「경계에서 걷고 있었다」는 말할 수 없다.
- 기각한 가설
  - **H3 프롭 늦은 배치**: `StartAndReachImmersion`은 `PropPlaced`를 최대 2초 기다린다(`CostumeFocusRig.cs:341`). 늦게 서면 그동안 층이 꺼진다. 그러나 빨강 2회 모두 `[코스튬프롭] 소환 — … 폴백 1단`이라 진입 프레임에 섰다 → **기각(로그)**.
  - **H2 제스처 가림**: `StickmanBlackboard.cs:2348` `if (!gesturing && TickCostumeKeypose(pose))`라서 제스처 동안에는 층이 멈춘다. 그런데 그동안 `ApplyFocusWatchStancePose`가 마디를 옮기므로, 제스처가 끝나는 프레임에 `HoldsCostumeStepAlready`가 거짓이 되어 **쓰기가 한 번 더 난다.** 「Still까지 정확히 0」을 만들려면 제스처가 2초 창 전체를 덮어야 한다. 제스처 길이는 약 1초대다(두리번 0.9초 §4-2, 끄덕임 1.20초 `:2343`) → **약한 기각(추정)**.
- **E-4 무관**
  - verify-change가 E-4 코드 경로 미실행을 로그로 확인했다.
  - 다른 자로도 같은 결론이다: `git show --name-only e6b14c2`의 14개 파일에 이 테스트·`CostumeFocusRig`·`TestClock`·`StickmanBlackboard`·`StickmanPoseAnimator`·`FramePacing`·`CostumeKeyposeTableSO`·`FocusSessionPhase`·`AutoWanderController`·office 표·`DefaultStickConfig`가 0건이다.
- 이전 전량 4회가 전부 통과였던 이유(추정): 경계 보행 발생률을 모른다(러너가 잰다). 그리고 이 테스트는 PlayMode 전량에서만 돌았다. 09-08 이후 전량 공백은 TEAM.md 「상시 백테스팅 공백」 참고.

### 11-5. 판정
- **틀린 쪽: 테스트(추정 — 러너 판별 전).** 정지 구간에 쓰기가 0인 것은 절감 설계 그 자체다(`StickmanBlackboard.cs:2668-2671`).
- **기대값의 근거 상수: 없다.** `> poseWritesAtStart`는 숫자 없는 「≥ 1회」 단언이다. 이것이 늘 성립하려면 `Still 도달(4 × 0.5 = 2.0초) ≥ 최장 무쓰기(3.2초)`여야 한다. 코드상 성립하지 않는다.
- **프레임·틱 경계 의존인가: 프레임 수는 아니다(11-3 가). 벽시계 위상 의존이다.**
  - ① 폴링 위상: 관측 시작에 고정, 결정적
  - ② 코스튬 듀티 위상: 몰입 경과에 고정, 결정적
  - ③ 둘 사이 어긋남 τ0: 경계 보행에 따라 흔들린다. **①·②는 결정적이고 ③만 흔들린다.**
- 증거 가치: 통과 회차의 「3회」도 사실은 **τ0 ≈ 0 위상의 산물**이다. 「Still 순간에 층이 돌고 있었다」의 직접 증거가 아니다. 반대로 빨강 회차에서도 층은 돌았다(창 전체 9회).

### 11-6. 수정 방향 후보 (테스트 쪽, 채택은 리더)
**A (권고) — 계기를 「썼다」에서 「돌았다」로 바꾼다.**
- Still 첫 프레임에서 `Assert.GreaterOrEqual(bb.CostumeStepIndex, 0, …)`을 단언한다.
  - `CostumeStepIndex`는 매 프레임 `StickmanBlackboard.cs:2113`에서 −1로 내려가고 `:2727`에서만 선다(public 게터 `:2656`).
  - ⇒ 「Still에 도달한 **그 프레임**에 코스튬 층이 돌았다」. 테스트 주석 `:358` 「Still에 도달한 그 순간에도 LFVS는 돌고 있었다」가 원래 하려던 말이다.
- 쓰기 양성 단언은 **창 전체**로 옮긴다: 관측 끝에서 `Assert.Greater(bb.CostumePoseWriteCount, poseWritesAtStart)`.
  - 창 4.8초(= 3 × dwell)는 최장 무쓰기 3.2초보다 길다.
  - 이 여유를 전제로 검산하려고 최장 무쓰기를 `CostumeFocusRhythm` 공개 함수로 계산하면, TEAM.md 「기대값을 프로덕션 함수로 만들지 마라」에 닿는다. 그래서 그 계산은 **예산 충분성 전제에만** 쓰고 판정 기대값에는 쓰지 않는다. 판정은 실측 계기 두 개(스텝 인덱스 · 누적 쓰기)만으로 한다.
- 메시지에 `τ0 = SessionDurationSeconds − RemainingSeconds − EdgeSeconds`, `CostumeSubPhase`, `CostumeStepIndex`, 관측 시작 프레임 상태를 싣는다. 다음 빨강에서 로그만으로 위상을 읽기 위해서다.
- 메서드 이름은 유지한다(`docs/verify/renames.tsv` 기준선 연속).

**B — 관측 시작 위상을 고정한다.** `WaitForGroundedIdle` 뒤에 τ0가 작업 구간 시작에 올 때까지 기다린 다음 관측한다. 결정적이지만 테스트가 **유리한 위상을 고르는** 형태다. 실사용자의 위상은 임의라서 A보다 약하다. 병행한다면 보조로만 쓴다.

**C (기각) — 프로덕션을 바꾼다.** 정지 구간에도 주기적으로 쓰기, 또는 dwell 단축. 절감 설계를 테스트에 맞춰 깨는 것이다.

**D (기각) — 예산 연장이나 재시도.** 이봉의 원인을 가리고 판정 불가처럼 숨긴다. §0 공통 규칙과 같은 병이다.

### 11-7. 변이
| 변이 | 주입(진단 사본·격리 미러) | 수정본 A | 옛 테스트 |
|---|---|---|---|
| S1 | `TickCostumeKeypose` 첫 줄에 `return false`(층 사망) | 빨강(스텝 인덱스 −1, 창 쓰기 0) | 빨강 |
| S2 | 절감 등급이 Still이면 코스튬 층이 `return false` — 「절감과 충돌」의 실물 | **빨강**(Still 프레임 스텝 인덱스 −1) | τ0 ≈ 0 회차에서 **초록**(Still 전 3회를 이미 셌다) |
| S3 (위상 강제) | Idle 유지 상태에서 관측 시작을 τ0 ∈ [1.05, 2.2)초로 미룬다(τ0 ≥ 2.2이면 옛 테스트도 통과 — 2026-09-15 verify-change 정정) | 초록 | **결정적 빨강**(`n → n`) — H1 인과의 한 축 |
| S4 (대조) | 관측 시작을 τ0 < 0.3초로 강제한다 | 초록 | 초록 |

- S2가 수정본 A의 핵심 이득이다. 옛 단언은 「Still 전에 한 번이라도 썼다」만 보므로, 절감 등급이 층을 끄는 회귀를 τ0 ≈ 0 회차에서 놓친다.

### 11-8. 러너 판별 설계 (러너 라운드 — 리더 배정 필요)
1. **진단 사본**(커밋 안 함)을 **10회** 돌린다. 기록 항목:
   - 관측 시작의 τ0
   - 몰입기 진입 프레임의 `CurrentStateId`(구간 전이를 매 프레임 폴링해 그 프레임 값)
   - `WaitForGroundedIdle` 소요 벽시계
   - 계기가 오른 모든 프레임의 몰입 경과
   - 쓰기 줄 수가 계기 증가량과 같은지 확인한다. 다르면 그 회차는 무효다.
2. 가설별 예측
   - **H1**: 빨강 회차는 전부 진입 프레임 상태 ≠ Idle이고 τ0 ∈ [1.0, 2.2)다. 통과 회차는 τ0 < 0.2이다([0.2, 0.333)은 `1→9` — 미관측, 2026-09-15 정정). 쓰기 시각이 11-3 (다) 표와 ±1프레임 안에서 일치한다.
   - **H-기타**: 빨강 회차가 τ0 < 1.0이거나 진입 프레임이 Idle이다 → H1 기각 → **디버거로 이동**.
3. **개입**: S3에서 옛 테스트 10/10 빨강, S4에서 10/10 초록이 나오면 위상 인과가 확정된다.
4. **수정본 A**: 10회 초록, S1·S2 각각 빨강. xml 판정은 TEAM.md 픽스처 끝 실패 규칙 1(`docs/verify/nunit_verdict.py`)로 한다.

---

## 12. `EdgeHopDownTests.NegativeControl_WithoutPostClimbCooldown_LeavesDockAlmostImmediately` — 간헐 #9 (2026-09-15 추가)

- 이 절이 한 일
  - 소스 읽기, 러너 산출물(xml·로그) 파이썬 대조, 산술
  - **Unity 0회, 트리 편집 0회**(이 문서 제외). 원인을 러너로 확인하지 않았다.
- 기준 코드: 머리말의 §12 줄을 따른다(HEAD `5912e33` + 트리, 인용 파일 변화 0).

### 12-1. 현재 단언과 관측 루프
`Tests/PlayMode/EdgeHopDownTests.cs`
```csharp
private const float DockHoldSeconds = 5f;                                        // :493
Assert.Less(_dockHoldSeconds, DockHoldSeconds, "…수정을 껐는데도 Dock에 …초 이상 머물렀습니다 — " +
    "(5)의 계측기가 증상을 잡아내지 못한다는 뜻이라 그 테스트를 신뢰할 수 없습니다. …");     // :568-571
float hold = 0f;                                                                  // :665
while (hold < DockHoldSeconds)                                                    // :666
{
    yield return null; float dt = Time.deltaTime; wander.Tick(dt);                // :668-670
    if (bb.CurrentFootholdHandle != DockHandle) { _dockLost = true; …; break; }   // :671-677
    hold += dt;                                                                   // :678
}
_dockHoldSeconds = hold;                                                          // :680
```
**사실(코드) — 예산 상수와 단언 경계가 같은 값이다.** 루프가 끝나는 길은 둘뿐이다.
- ① 떠남: `break`가 `hold += dt` 앞에 있다 → `hold < 5` → 초록.
- ② 예산 소진: `hold ≥ 5` → 빨강. float 누적이라 마지막 dt만큼 넘친다(xml `5.00002861f`).

⇒ `Assert.Less(hold, 5)`는 `_dockLost == true`와 같은 판정이다.
- 단언이 보는 것은 이름의 「거의 즉시」가 아니다. **「5초 안에 한 번이라도 떠났다」**뿐이다. 초록 회차의 실측은 0.08초다(12-2).
- `_dockLost`(`:498`, `:674`)는 기록만 하고 **단언하지 않는다**. 그래서 두 경우가 같은 빨강·같은 메시지로 나온다.
  - 「떠나지 못한 채 예산이 끝남」 — 경주가 이 회차에 성립하지 않음
  - 「수정이 되살아나 계측기가 죽음」
- 메시지(`:569-571`)는 후자만 말한다.

**벽시계 규칙(CLAUDE.md): 위반 아님.**
- 예산은 `Time.deltaTime`의 초 누적이다. 프레임 수가 아니다.
- 컨트롤러에 넣는 dt와 같은 시계라 일관된다.
- `Maximum Allowed Timestep 0.33333334`(`ProjectSettings/TimeManager.asset`)라서, 멈춘 프레임 하나가 먹는 시간은 최대 0.33초다.

### 12-2. 실측 — 로그 `[HOPDOWN-TEST] Dock 체류 실측(쿨다운 꺼짐(네거티브 컨트롤)…)` 줄 원문
| 실행 | 결과 | 되올라온 X | 연속 체류 | 떠난 순간 핸들 | 최종 상태 | 왕복까지 |
|---|---|---|---|---|---|---|
| `scratchpad/dp5d/xml/P1`(5-d 전량, 약 36분) | **빨강** | 6.150 | **5.00초** | **(안 떠남)** | Walk | **9.03초** |
| `ISO_EdgeHopDown_1` | 통과 | 6.150 | 0.08초 | 0(공중 — 뛰어내림) | Fall | 9.21초 |
| `ISO_EdgeHopDown_2`·`_3` | 통과 | 6.150 | 0.08초 | 0(공중) | Fall | 9.39초 |
| 09-14~15 전량 4회: `Logs/coder-onbstore/play-full`·`s4-playfull`, `docs/verify/runs/vc-e12_play`, `Logs/vc-e4/P-full` | 통과 | 6.150 | 0.08초 | 0(공중) | Fall | 9.21 / 9.39 / 9.39 / 9.39초 |
| 09-06~08 전량 8회: `docs/verify/runs/`의 `coder-fan-play`·`r28-consolidated`·`dbg-r29`·`te-flaky`·`r30-final`·`r31-final`·`ropeclimb-r3`·`part2-final` | 통과 | 6.150 | 0.08초 | 0(공중) | Fall | 전부 4.35초 |

**xml 대조 — ★ 범위 한정: 이 절의 「이력」은 09-06 이후 표본이다**
- 최근 8일(09-07 22:52 이후) 저장소 xml은 6/6 `Passed`다. 브리프의 「이력 6/6」과 같다.
- 09-06 이후로 넓히면 xml 12/12 `Passed`이고, 짝 로그 12/12가 전부 `0.08초 · 공중`이다. 여기에 격리 3회와 `P1`을 더한 **16표본**이 이 절의 범위다.
  - 이 절을 쓴 뒤 `Logs/vc-5d/P`(09-15 03:14, 5-d 전량 재측정)가 생겼다. #9는 `Passed`, 로그는 `0.08초 · 9.21초`다 → 범위 표본 17(빨강 1).
- **저장소 전체**(`Library`·`Temp` 제외, 파이썬 전수): 이 테스트가 든 xml은 120개, `Passed` 119 · `Failed` 1이다. 실패 1은 `Logs/coder_36_play.xml`(08-31)이다. 사유는 `[UiChrome]` 그림자 오류 로그의 `Unhandled log message`(LogAssert)이고, 그 회차 체류는 0.54초·왕복 3.87초다 ⇒ #9 기전과 무관하다. 리더 전달 수치(119개 중 118)와 1개 차이는 그 뒤 생긴 `Logs/vc-5d/P.xml`로 판단한다(추정).
- scratchpad 미러 안의 `BASELINE-20260902-1201_play.xml`은 같은 파일의 복사본이라 세지 않았다.
- 대조: 없는 이름은 전부 `ABSENT`로 나왔고, 같은 픽스처의 (5)는 전부 적중했다.

**모양 — ★ 범위 한정: 09-06 이후에는 둘뿐이다.**
- 09-06 이후 초록 15회(+ `vc-5d` 1회): 전부 `0.08초 / 공중 / Fall`
- 빨강 1회: `5.00초 / 안 떠남 / Walk`
- 이 범위에서는 연속 분포가 아니라 이봉이다(#8과 같다).
- **저장소 전체 로그 120줄**(파이썬 전수, 줄 `쿨다운 꺼짐(네거티브 컨트롤)`, 파일 mtime으로 시기 구분)에는 다른 모양이 있다.

  | 시기 | 연속 체류 | 왕복까지 | 떠난 순간 | 회수 |
  |---|---|---|---|---|
  | 08-29~09-01 | 0.54~0.56초 | 3.85~3.88초 | 공중 | 58 (★ 2026-09-15 verify-change 재셈 — 처음 59) |
  | 08-29 (`Logs/dbg_dock_edge1.log`, 진단 로그) | 0.54초 | 3.87초 | (안 떠남) · Fall | 1 |
  | 08-30~08-31 | 0.48~0.50초 | 3.92~3.99초 | 공중 | 16 (★ 재셈 — 처음 15; `Logs/coder_r2_pm_final.xml` 0.50초·왕복 3.91초는 두 행 왕복 범위 어디에도 안 맞아 체류 기준으로 이 행에 셈) |
  | 08-31 | 0.16초 | 3.35초 | 공중 | 3 |
  | 08-31~09-08 (`de3fb32` 이전) | 0.08초 | 3.85 · 4.35 · 4.36 · 4.43초 | 공중 | 37 |
  | 09-14~09-15 (`de3fb32` 이후) | 0.08초 | 9.21 · 9.39초 | 공중 | 5 |
  | 합계 | | | | 120 |

  - 격리 3회와 `P1`은 scratchpad에만 있어 이 전수에 들어가지 않는다.
  - ⇒ 구간 1.2초 시절에도 0.08초 모양이 **37회** 재현됐다. §9 자백 18(H9-a가 설명하지 못하는 현상)을 **더 강하게** 뒷받침한다.

**빨강 회차에서 올라선 직후 — 사실(로그 `P1.log`)**
- `:8403` `[벽타기] 완료 — 올라선 월드=(6.150,-1.200) … 올라선 방향=왼쪽(맨틀 신호 #1)`. 초록과 같은 자리·같은 방향이다.
- `:8405` `[말풍선] 발화 보류 (Idle) … (배회 페이즈 잔여 0.03초, 이동의도 0.00)`
  - 이 값은 `IntentSource`, 즉 **시험 컨트롤러**에서 온다(`StickmanBlackboard.cs:117-118`; 테스트가 `:623`에서 꽂는다).
  - ⇒ 올라선 순간 시험 컨트롤러는 **바깥으로 걷고 있지 않았다.** 휴식 또는 경계 정지 중이었고 잔여는 0.03초였다.
- `:8406` `[눈추적] 몸통=(3.64, -1.20) … 상태=Walk` — Dock **안쪽**으로 2.51유닛 걸어 들어갔다. 이후 `:8407`까지 `[뛰어내리기] 결정` 줄은 0건이다.
- 초록 3회는 `[벽타기] 완료` 뒤 1~2줄 안에 `[뛰어내리기] 결정 — 방향=오른쪽`이 나온다(iso-1 `:814`, iso-2 `:816`, iso-3 `:807`).

**판정에 쓰지 않은 줄**
- 양쪽 모두에 있는 「[되올라가기] 안착 … 8.0초 동안 유예」는 **에이전트 자체 컨트롤러**의 줄이다(§9 자백 19).
- 음성 대조 조건이 성립했음은 테스트 자신의 `:8407` 줄 「쿨다운 꺼짐 · 인셋 실제=0.250(유도 끔)」이 찍었다.

**스톨**
- `P1`의 이 테스트 구간에는 `[스톨` 접두사 줄이 0건이다(`[스톨귀인]`·`[스톨구간]` 둘 다). 전체 로그에는 `[스톨귀인]` 74건 · `[스톨구간]` 35건, 합 109건이다(grep 계수, 없는 접두사 대조 0).
- 격리 3회는 씬 로드 직후 1건씩 있었다(640~766ms). 모두 리그 준비(`[HOPDOWN-TEST] 준비 완료`) 전이다.
- ⇒ 「큰 프레임 한 번이 경주를 깼다」는 로그로 뒷받침되지 않는다. 다만 스톨 문턱 100ms 미만의 흔들림은 이 로그가 보지 못한다.

**5-d 무관(다른 자)**
- 5-d 트리 변경 6파일에 12-3 기전 파일이 0건이다.
- `P1`과 격리 ×3의 트리 해시가 같고(`run.txt` `myfiles`), 격리 3/3이 초록이다.
- 「5-d 코드 미실행」은 dev-platform 확인을 인용했다.

### 12-3. 기전 — 코드로 닫히는 부분 (사실(코드))
**(가) 이 음성 대조가 재현하려는 것은 「등반 중 경주」다.**
- 근거 문장: 테스트 메시지 `:571` 「고장 재현 조건(경계 정지가 등반 도중 끝나며 방향이 반전되는 타이밍)」, 프로덕션 문서 `AutoWanderController.cs:252-257`.
- 쿨다운 0이면 맨틀 신호를 받아도 아무것도 하지 않는다: `:272-273` `if (cooldown <= 0f) return;`. 그래서 `:277` `EnterMoving(inward)`(안쪽 방향 강제)가 없다.
- 경주의 순서
  1. 아래 발판 경계에서 되올라가기 추첨(`:701-704` → `:1203-1214`; `stepUpChance = 1`, 테스트 `:603`) → 등반 진입. 이 다리의 추첨권은 이미 썼다(`:703`).
  2. 등반 첫 틱에 컨트롤러는 여전히 경계에 있다고 보고 경계 정지를 건다(`:742`). 정지 길이는 0.15초(테스트 `:599-600`), 지터 0(`:594`)이다.
  3. 정지가 끝나면(`:763-779`) **방향을 뒤집는다**(`:770`, 바깥 = 오른쪽). 추첨권을 리셋하고(`:773`), `if (_moveTimer >= _moveDuration) EnterResting();`(`:778`)를 본다.
  4. 그 뒤 등반 중 공중 틱에서는 추첨권이 다시 리셋되고(`:647-658`), 경계 분기를 못 타므로 `:747-750` `if (_moveTimer >= _moveDuration) EnterResting();`에 닿는다.
  5. 등반은 1.20초다(`ParkourClimbState.cs:365-366`, 에셋 `:41`). 끝나면 발판 고착(`:400`) + 맨틀 신호(`:408`).
- 올라선 자리 X=6.150은 모서리 6.400에서 0.250 안쪽이다(인셋 0.25·유도 끔, 테스트 `:611-612`). 경계 판정 거리 0.400(로그)보다 가깝다.
- ⇒ 컨트롤러가 **그 순간 바깥으로 걷는 중이면** 이렇게 이어진다: 다음 틱 경계 → 추첨(리셋됨) → 뛰어내리기(`:1166-1180`, `hopDownChance = 1` `:602`) → 확약(`:710-722`) → (0.250 − 확약 거리) ÷ 속도만큼 걸어 발을 뗀다. 로그상 0.08초다.
- ⇒ **초록의 필요조건: 올라선 틱에 `_phase == Moving`이고 `_direction`이 바깥이다.** 휴식(`TickResting`)·경계 정지(`:621-625`에서 곧바로 반환)·안쪽 방향이면 그 틱에 뛰어내리기 추첨이 없다.

**(나) `de3fb32`(2026-09-08 14:42)가 걷기 구간 길이 규칙을 바꿨다 — 코드 사실. 이번 모양 변화의 원인 커밋인지는 추정이다(교란 커밋 8개, 아래 표).**
- `ResolveWalkPhaseDurationSeconds`(`:564-583`) = `max(추첨값, min(바닥값, 12))`
  - 바닥값 = 걸을 수 있는 화면 폭 × `wanderWalkTargetScreenFraction` ÷ `ResolveWalkSpeed()`
  - `wanderWalkTargetScreenFraction` = 0.22(에셋 `:124`). **테스트가 이 값을 덮지 않는다.**
  - `ResolveWalkSpeed()` = 2.5 × 0.75 = 1.875(`StickConfig.cs:2546`, 에셋 `:15`·`:272`)
- 리그 화면 폭: Dock 12.8유닛이 화면 폭의 40%다(테스트 `:152`, 로그 `X -6.400~6.400`) → 32.0유닛. 클램프 여백을 빼면 더 좁다(미측정).
- 검산: 바닥값 ≤ 32.0 × 0.22 ÷ 1.875 = **3.755초**. 테스트가 정한 추첨값 1.2초(`:592-593`)보다 크다 ⇒ 구간은 약 3.75초다(상한 추정).
- 낡은 전제 셋
  - 테스트 주석 `:588-589`는 구간을 1.2초로 본다.
  - 폭 전제 단언 `:616`(`walkSpeed × wanderWalkDurationMax × 2`)도 1.2초로 계산한다.
  - 프로덕션 문서 `:561-562` 「640×480에서는 바닥값이 추첨값보다 작아 결과가 비트 단위로 그대로다」도 **이 리그에는 맞지 않는다**(추정: 기본 추첨 하한 1.5초에도 3.755초가 이긴다).
- 관측 정합(추정 근거): 왕복 시간이 `part2-final`(시작 09-08 01:02, xml `start-time`) 이하 8회는 **4.35초**, 09-14 이후는 **9.21·9.39초**다(12-2). 시점은 `de3fb32`와 맞는다. **그러나 시점 일치만으로 `de3fb32`를 특정할 수 없다** — 같은 사이에 기전 파일을 건드린 커밋이 8개다.
  - 재측정 명령: `git log --since=2026-09-08T01:02:28+09:00 --until=2026-09-14T18:10:29+09:00 -- <기전 파일 7개>`. 창 끝은 `Logs/coder-onbstore/play-full.xml`의 `start-time`이다. 경로는 bash 배열로 넘겼다(§9 자백 21).
  - 대조: 양성 — `de3fb32` 1건 적중 / 음성 — 2030년 이후 창 0건, 같은 창의 `TimeManager.asset` 단독 0건.

  | 커밋 | 시각(09-08) | 요지(커밋 제목) | 건드린 기전 파일 |
  |---|---|---|---|
  | `6156c73` | 08:29 | 밧줄등반 발동빈도 재조정(0.20→0.85) + 장신구 1ULP | `StickConfig`·`DefaultStickConfig`·`AutoWanderController`·`StickmanBlackboard` |
  | `d8df6c6` | 09:56 | 밧줄 상한 화면비례 교체 + 창 드래그 5종 확대 | `StickConfig`·`DefaultStickConfig`·`AutoWanderController` |
  | `1b5b1f1` | 10:47 | 목표 지향 등반 신설 | `StickConfig`·`DefaultStickConfig`·`AutoWanderController`·`StickmanBlackboard` |
  | `d45c8b1` | 11:13 | 밧줄 높이 상한을 시간으로 묶음 | `StickConfig`·`DefaultStickConfig`·`AutoWanderController` |
  | `67f0f6a` | 11:33 | RopeClimb/ParkourClimb `Enter()` 재확인 | `ParkourClimbState`·`StickmanBlackboard` |
  | `de3fb32` | 14:42 | 밧줄 목표 랜덤화 + **배회 거리 화면 비례 바닥값** | `StickConfig`·`DefaultStickConfig`·`AutoWanderController`·`StickmanBlackboard` |
  | `4379004` | 15:07 | 종이비행기 궤도 게이트 + 대사 4건 | `ParkourClimbState` |
  | `99df3f3` | 17:48 | 밧줄등반 중 창을 치우면 안 떨어지던 결함 | `StickmanBlackboard` |

  - 8개 모두 09-08 안이다(09-09 ~ 09-14 18:10 사이는 0건). `EdgeHopDownTests.cs`·`TimeManager.asset`은 0건이다.
  - ⇒ **`de3fb32`가 유력 후보다(추정).** 커밋 제목상 구간 길이를 말하는 것은 `de3fb32`뿐이다. 다만 8개의 diff를 전부 대조하지는 않았다. 특히 `1b5b1f1`(목표 지향 등반 신설)은 경계 판정보다 먼저 이동을 가져가는 경로(`AutoWanderController.cs:664` `TickClimbSeek`)를 요지로 한다. 줄 귀속은 미확인이지만 같은 경주 경로에 닿을 수 있다.
  - 8개 중 판별은 §12-8 러너가 한다(커밋 이분 또는 강제 개입).

**(다) 흔들림의 원천 — 시드는 고정이지만 「뽑는 횟수」가 시간에 묶여 있다.**
- 시드는 고정이다: `new System.Random(20260829)`(`EdgeHopDownTests.cs:622`).
- 그러나 난수를 뽑는 횟수가 걸은 시간에 따라 달라진다.
  - 걷는 동안 0.5초마다 즉흥 반전 확률을 뽑는다. 확률이 0이어도 `_rng.NextDouble()`을 부른다(`AutoWanderController.cs:633-637`).
  - 지터 비율이 0이어도 `Jitter`는 뽑는다(`AutoWanderController.cs:1396`).
- ⇒ 누적 걷기 시간이 0.5초 격자를 넘는 순간이 한 프레임만 달라도, 뒤따르는 **방향 뽑기**(`AutoWanderController.cs:1372`, 반반)의 결과가 달라진다.
- 이 기전 자체는 사실(코드)이다. 이번 빨강의 원인인지는 추정이다.

### 12-4. 가설 — 올라선 틱에 왜 바깥으로 걷고 있지 않았나 (추정, 러너 미확인)
**H9-a 구간 만료 (주 가설)**
- 구간을 늘린 원인 커밋: `de3fb32`가 유력 후보다(추정). 같은 기간 기전 파일 커밋 8개(12-3 나 표) 중 판별은 §12-8 러너(커밋 이분 또는 강제 개입)가 한다.
- 조건: 12-3 (가)의 초록 조건을 구간 시계로 옮기면, 되올라가기 추첨 시점의 구간 잔여가 등반 1.20 − 경계 정지 0.15 = **1.05초**보다 커야 한다. 정지 동안에는 `_moveTimer`가 멈춘다(`:776-777`).
  - 구간이 약 3.75초이면: 추첨 시점까지 같은 구간을 **2.70초 넘게** 걸었을 때 빨강 쪽이 된다.
- 빨강 궤적(`P1.log` `:8392`~`:8399` 사이 `[눈추적]` 표본)
  - `(12.40, Idle)`: 아래 발판 오른쪽 끝 12.8 − 경계 판정 0.4 자리, 즉 경계 정지다.
  - → `(9.47, Walk)` → 벽 앞에서 추첨
  - 12.40 → 추첨 지점 6.80(= 6.4 + 0.4)은 5.60유닛 ÷ 1.875 = **2.99초**로 2.70초를 넘는다. 경계 정지는 구간을 끊지 않는다(`:776-778`).
  - ⇒ 등반 중 구간 만료 → 휴식 0.5초(테스트 `:590-591`) → 올라선 틱에 잔여 약 0.03초. 로그와 맞는다.
- 초록 iso-1 궤적: `(10.82)` → `(11.50)` → `(7.75)`
  - 오른쪽 끝 12.40의 정지 표본이 없다. 12.40 전에 구간이 끝나 휴식했고, 새 구간이 왼쪽으로 뽑혀 벽까지 걸었을 수 있다.
  - 반환점이 X ≤ 11.87이면 추첨까지 (11.87 − 6.80) ÷ 1.875 = 2.70초 이하다. 여유는 0.1초대로 얇다.
  - **반환점 X는 표본으로 확정하지 못했다**(§9 자백 17).
- 휴식 뒤 방향은 `PickDirectionAvoidingEdge`(`:1370-1382`)가 반반으로 뽑는다. X 6.15의 오른쪽 경계는 화면 끝이 아니라서 `:1377-1380`의 강제가 걸리지 않는다.
  - 바깥이 뽑히면 약 0.03 + 0.07초 만에 떠난다 → **초록**
  - 안쪽이 뽑히면 → **빨강**
  - ⇒ H9-a에서 빨강 = 구간 만료 × 안쪽 뽑기(반반)
- 약점: `de3fb32` 이전에는 구간이 1.2초였다. 그러면 구간 잔여 > 1.05초가 되려면 새 구간 시작 후 0.15초 안에 추첨해야 한다. 그런데 12-2 표의 8회만이 아니라 저장소 전체 로그에서 `de3fb32` 이전 0.08초 모양이 **37회**(08-31~09-08) 재현됐다. H9-a만으로는 설명되지 않는다(§9 자백 18). 그 앞(08-29~09-01)에는 0.48~0.56초 모양이 따로 있다. 구간 규칙 말고도 이 체류를 움직인 변화가 여럿 있었다는 뜻이다(12-2).

**H9-b 경계 정지 재진입 (대안)**
- 순서
  1. 정지가 끝나며 반전(`:770-773`)으로 추첨권이 리셋된다.
  2. 등반 막바지에 접지·경계로 판정되는 틱이 있다.
  3. 그 틱에 뛰어내리기 대상 탐색(`:1167`)이 실패하면 `:742` 경계 정지가 **한 번 더** 걸린다(0.15초).
  4. 올라선 틱에 정지 잔여 0.03초 → 끝나면 반전 → **안쪽**(`:770`) → 결정적으로 빨강.
- 로그 「잔여 0.03초 · 이동의도 0」은 H9-b에도 맞는다(휴식 0.5초의 끝 0.03초일 수도, 정지 0.15초의 끝 0.03초일 수도 있다).
- 등반 중 `SenseGround`가 접지로 답하는 틱이 있는지는 **코드로 닫지 못했다.**

**배제**
- 5-d: 12-2 끝.
- 큰 프레임 한 번(스톨): 이 구간의 스톨 로그가 0건이다. 100ms 미만의 흔들림은 배제하지 못한다.
- 쿨다운이 켜진 채 돌았다: 「8.0초 유예」 줄은 에이전트 자체 컨트롤러다. 테스트 로그 `:8407`이 음성 대조 조건 성립을 찍었다.

### 12-5. 판정
- **틀린 쪽: 테스트(추정 — 러너 판별 전).**
  - 배포 기본은 쿨다운 8초 켬이다.
  - 쿨다운을 켠 짝 (5)는 맨틀 신호로 방향을 강제한다(`:275-277`). 그래서 이 위상에 기대지 않는다.
- **벽시계 규칙(CLAUDE.md): 위반 아님**(12-1).
- **#8 리더 보강 후보 「단언 창 길이 ≥ 대상 주기의 최장 무작업 구간」과 같은 형태인가: 같다. 그리고 하나가 더 있다.**
- 같은 점: 단언 창 5초가 「안쪽 갈래에서 다음으로 떠날 수 있는 시점」보다 짧다. 산술(추정, 구간 3.75초):

  | 항목 | 초 |
  |---|---|
  | 휴식 잔여 | 0.03 |
  | 안쪽 걷기 한 구간(7.0유닛 → X ≈ −0.9) | 3.75 |
  | 휴식 | 0.5 |
  | 바깥으로 되돌아오기 (6.0 − (−0.9)) ÷ 1.875 | 3.7 |
  | **합계** | **약 8.0 > 5** |

  - 안쪽 갈래에서 **가장 빨리 떠나도** 약 8.0초다.
  - `de3fb32` 이전(구간 1.2초)이면 0.03 + 1.2 + 0.5 + 1.1 = **약 2.9초 < 5초**다. 안쪽 갈래라도 절반은 창 안에서 떠났다.
  - ⇒ 걷기 구간 바닥값(유력 후보 `de3fb32`, 교란 커밋 8개 — 12-3 나)이 최장 무작업 구간을 창 밖으로 밀었다(추정). #8의 「무쓰기 3.2초 > Still 도달 2.0초」와 같은 모양이다.
- 더한 점 (1): #8은 창과 경계가 서로 다른 값이었다(Still 도달 vs 쓰기 계기). #9는 **예산 상수 = 단언 경계**다.
  - 그래서 「못 떠남」과 「예산 소진」이 구조적으로 같은 값(`hold ≥ 5`)이 된다.
  - 둘을 가를 계기 `_dockLost`가 이미 있는데 쓰지 않는다.
- 더한 점 (2): 경주 전제(「올라선 틱에 바깥으로 걷는 중」)를 **관측도 단언도 하지 않는다.**
  - 그래서 빨강 메시지가 「(5)의 계측기를 신뢰할 수 없다」라는 **틀린 결론**을 낸다.
  - 이번 회차에 (5)는 멀쩡했다(`Passed`).

### 12-6. 수정 방향 후보 (테스트 쪽, 채택은 리더)
**A (권고) — 판정을 셋으로 가른다.** 수정은 테스트 파일 한 곳이고 프로덕션은 0이다.
1. **맨틀 순간을 기록한다.** 자리는 `:647` `_climbedBackToDock = true`다. 기록 항목:
   - `wander.MoveInputX`(`AutoWanderController.cs:188`)
   - `wander.PlannedDwellRemainingSeconds`(`:212`)
   - `bb.ClimbMantleDirection`(`StickmanBlackboard.cs:1998`)
   - 셋 다 public이다.
   - 기록 시점은 「핸들이 Dock으로 바뀐 틱의 `wander.Tick` 뒤」로 정의하고, 다음 1틱 값도 함께 싣는다. 맨틀 신호는 에이전트 Update 안에서 서고 시험 컨트롤러 Tick은 코루틴(`:668-670`)에서 돈다. 두 순서는 12-8 ①이 잰다.
2. **전제 단언**(`Assume`이 아니라 `Assert`): 그 틱에 `Mathf.Sign(wander.MoveInputX) == -bb.ClimbMantleDirection`이다. 즉 바깥으로 걷는 중이다.
   - 메시지: 「경주 미재현 — 올라선 틱에 컨트롤러가 바깥으로 걷고 있지 않았다(이동의도 …, 페이즈 잔여 …). (5)의 계측기 결함이 아니라 리그 위상 문제다」
3. **떠남을 직접 단언한다**: `Assert.IsTrue(_dockLost, …)`. `hold < 5`와 같은 판정이지만 뜻이 드러난다.
4. **「거의 즉시」를 숫자로 단언한다**: `Assert.Less(_dockHoldSeconds, _clonedConfig.wanderIdleDurationMin, …)`
   - 뜻: 휴식을 한 번도 거치지 않고 떠났다 = 경주 경로다.
   - 0.5는 리그 자신의 설정값(`:590`)이라 프로덕션 상수의 사본이 아니다.
   - 초록 실측 0.08초(15/15) 대비 여유는 약 6배다.
5. **예산과 경계를 분리한다.** `DockHoldSeconds`는 (5)의 체류 요구로만 남기고, (6)의 판정 경계로는 쓰지 않는다.
6. **폭 전제(`:616-620`)를 구간 길이와 무관한 상한으로 바꾼다**: `_clonedConfig.ResolveWalkSpeed() * DockHoldSeconds < _dockWidthWorldUnits - bb.ParkourMantleInsetWorld`
   - 체류 창 안에서 반대편 끝에 물리적으로 닿을 수 없다는 뜻이다.
   - 지금도 결론은 성립한다(1.875 × 5 = 9.4 < 12.55, 속도는 추정). 계산 근거만 낡았다.
7. 메서드 이름은 유지한다(`docs/verify/renames.tsv` 기준선 연속).
- **A만으로는 간헐이 남는다.** 달라지는 점은 둘이다.
  - 빨강이 「경주 미재현」으로 정확히 이름 붙어 나온다.
  - 「(5)를 믿을 수 없다」는 거짓 결론이 사라진다.

**B (권고, A와 한 묶음 — 어느 쪽인지는 12-8 판별 뒤에 고른다) — 경주 전제를 결정적으로 만든다.**
- H9-a가 확정되면 → **B1**: (6)의 리그에서 구간이 등반 중에 끝나지 않게 한다.
  - 조건식: 추첨 시점 구간 잔여 > `parkourClimbDuration − wanderEdgeTurnPauseMax`
  - 아래 발판의 최장 경로(발판 폭 왕복)보다 긴 구간을 `wanderWalkDurationMin/Max`에 **설정값으로 명시한다.**
  - 바닥값은 `wanderWalkTargetScreenFraction = 0`으로 명시적으로 끈다(`:569` `!(fraction > 0f)`이면 바닥값을 무시한다). 두 설정이 같은 뜻을 말하게 하기 위해서다.
  - 폭 전제는 A-6이 구간 길이와 무관하게 지킨다. 구체 수치는 러너가 정한다.
- H9-b가 확정되면 → **B2**: 원인이 등반 중 경계 정지 재진입이면 구간 길이는 무관하다. `wanderEdgeTurnPauseMin/Max`(`:599-600`)를 등반 길이 대비 어느 위상에 둘지 러너 로그로 정한다.
- 어느 쪽이든 A-2 전제 단언이 리그 드리프트를 **시끄러운 빨강**으로 잡는다. 조용한 간헐로 돌아가지 않는다.

**C (기각) — 예산 소진을 판정 불가(`Assume`/`Inconclusive`)로 분리**
- 판정 불가는 `failed=`에 들어가지 않는다(TEAM.md 거짓 통과 형태 규칙 5, 이 문서 §0 공통 규칙).
- 게다가 (6)은 **음성 대조가 소리 없이 죽는 것을 막으려고 만든 테스트**다(`:521-528`). 판정 불가로 빼면 바로 그 죽음이 조용해진다.
- 가르기는 하되, A-2처럼 `Assert` 전제로 가른다.

**D (기각) — 예산 연장(예: 10초) 또는 재시도**
- 안쪽 갈래도 약 8.0초면 떠나므로 초록이 된다. 그러나 그 초록은 경주가 아니라 **한 사이클 돌아온 뒤의 정당한 하강**이다.
- 「거의 즉시」라는 계측 대상이 사라진다. 수정이 부분적으로 되살아나 몇 초 늦게 내려가는 회귀도 초록으로 통과한다.
- §11-6 D와 같은 병이다.

**E (보조로만) — 떠남 확률·방향 고정**
- 뛰어내리기 확률은 이미 1이다(`:602`). 흔들리는 것은 방향 뽑기(`:1372`)다.
- 시드는 이미 고정(`:622`)이라 시드로는 고쳐지지 않는다(12-3 다).
- 테스트 안에서 `System.Random`을 상속한 상수 소스를 주입하면 결정적으로 만들 수는 있다. 하지만 상수를 고르는 순간 **어느 갈래를 볼지 테스트가 고르게 된다**(§11-6 B와 같은 약점).
  - 예: 바깥 방향 상수는 H9-a의 빨강 갈래를 「휴식 뒤 바깥 뽑기」라는, 경주가 아닌 경로로 초록으로 만든다.
- 러너 진단 전용으로만 쓴다.

**F (기각) — 프로덕션 변경**(예: 휴식 뒤 올라선 턱의 바깥 방향 회피)
- 음성 대조의 목적은 수정을 끈 옛 경로를 재현하는 것이다. 테스트에 맞춰 옛 경로를 바꾸면 대조할 대상이 사라진다.

### 12-7. 변이 (진단 사본·격리 미러)
| 변이 | 주입 | 수정본 A(+B) | 옛 테스트 |
|---|---|---|---|
| N1 (위상 강제 — 빨강 갈래) | (6) 리그에서 `_clonedConfig.parkourClimbDuration`을 구간 길이보다 길게(등반 중 구간 만료 강제) | A-2 전제 단언 **빨강**, 메시지 「경주 미재현」 | 안쪽 뽑기 회차는 빨강, 바깥 회차는 **초록**(H9-a 기준 반반) — 경주 미재현을 못 가린다 |
| N2 (대조) | `parkourClimbDuration`을 경계 정지보다 조금만 길게 줄인다(구간 만료 불가) | 초록 | 초록 |
| N3 (계기 생존) | (6)에서만 `postClimbDescendCooldown`을 기본 8초로 되살림(수정 켬) | A-3·A-4 **빨강**(`_dockLost=false`) | 빨강 |
| N4 (「거의 즉시」 생존) | 프로덕션 확약 발떼기(`AutoWanderController.cs:714`)에 1.0초 지연 추가(늦게 내려가는 회귀) | A-4 **빨강**(체류 약 1.07초 ≥ 0.5초) | **초록**(5초 안에 떠남) — A-4가 새로 잡는 회귀 |
| N5 (예산 분리 확인) | 테스트 쪽 `DockHoldSeconds`를 10초로 | 판정 불변(경계가 예산과 분리됨) | 초록 회차 불변 |

### 12-8. 러너 판별 설계 (러너 라운드 — 리더 배정 필요)
1. **진단 사본**(커밋 안 함)을 격리로 실행한다. 되올라가기 추첨 틱부터 맨틀 뒤 0.5초까지 매 틱 한 줄을 남긴다.
   - 필드: `t`(누적 `Time.deltaTime`), `Time.frameCount`, 상태, 핸들, 몸통 x, `wander.MoveInputX`, `wander.PlannedDwellRemainingSeconds`, `wander.LastEdgeNear`·`LastRemainingToEdge`·`LastEdgeDirection`(`AutoWanderController.cs:180-186`, public), `bb.ClimbMantleSequence`
   - 페이즈 진입은 잔여값이 **어느 값으로 뛰었는지**로 식별한다. private 필드는 읽지 않는다.
     - 0.5 = 휴식(`:590-591`)
     - 0.15 = 경계 정지(`:599-600`)
     - 구간 길이 부근 = 새 걷기 구간
   - 로그 줄 수가 프레임 수와 다르면 그 회차는 무효다.
   - 같은 로그로 12-3 (나)의 **실제 구간 길이**(새 구간 진입 시 잔여값)를 재서 3.755초 상한 추정을 실측으로 바꾼다.
2. **자연 재현에 기대지 않는다.** 격리 3/3·이력 12/12가 초록이고 빨강은 약 36분짜리 전량에서 1회뿐이다. 10회 반복으로 빨강을 기다리는 설계는 공허하다. 대신 개입한다.
   - N1 10회: H9-a라면 전 회차가 등반 중 잔여값이 0.5로 뛰고(휴식 진입), 옛 테스트의 빨강은 안쪽 뽑기 회차와 일치해야 한다.
   - N2 10회: 전 회차가 올라선 틱에 이동의도 = 바깥이고, 0.08초대에 떠나야 한다.
   - **원인 커밋 판별**(12-3 나 표 8개)
     - 강제 개입(트리 이동 없음): 현 트리 진단 사본에서 `_clonedConfig.wanderWalkTargetScreenFraction = 0f` 한 줄만 켠다. 왕복이 4.35초대로 돌아오고 구간 길이(잔여값 점프)가 1.2초로 돌아오면, 구간 바닥값 규칙이 모양 변화의 원인이다 → `de3fb32` 확정 쪽.
     - 돌아오지 않으면 커밋 이분: `de3fb32^`와 `de3fb32` 트리의 격리 미러에서 이 테스트의 `왕복까지`·`연속 체류`를 비교한다. 전이가 거기서 안 나면 나머지 7개를 이분한다. 미러 규약(TEAM.md 격리 미러 3종 대조)을 따른다.
3. **가설별 예측**(자연 회차 + N1)
   - **H9-a**: 빨강(또는 전제 불성립) 회차에는 등반 중 잔여값이 0.5로 뛴 틱(휴식 진입)이 있다. 추첨 시점의 구간 경과는 구간 길이 − 1.05초보다 크다.
   - **H9-b**: 등반 막바지(맨틀 전 0.15초 안)에 잔여값이 0.15로 뛴 틱(경계 정지 재진입)과 `LastEdgeNear=true`가 있다. 정지가 끝날 때 이동의도가 안쪽으로 뒤집힌다.
   - **H-기타**: 올라선 틱에 이동의도 = 바깥인데 0.5초 안에 `[뛰어내리기] 결정`이 없다 → 둘 다 아니다 → **디버거로 이동**.
4. **수정본 A(+B) 합격 조건**
   - 격리 10회 초록
   - N1 빨강(메시지 「경주 미재현」), N3·N4 빨강, N5 판정 불변
   - PlayMode 전량 1회에서 이 테스트 초록
   - xml 판정은 TEAM.md 픽스처 끝 실패 규칙 1(`docs/verify/nunit_verdict.py`)로 한다.

---

## 13. `DragStruggleTests.StruggleDoesNotBreakCursorStickiness` — 간헐 #10 (2026-09-15 추가)

- 이 절이 한 일: 소스 읽기(HEAD `e899fb8`), xml·로그 파이썬 대조, 산술. **Unity 0회, 트리 편집 0회**(이 문서 제외). 원인을 러너로 확인하지 않았다.

### 13-1. 현재 단언과 관측
`Tests/PlayMode/DragStruggleTests.cs`
```csharp
yield return HoldAndObserve(1.2f, new Vector2(2.5f, 0.8f), obs);                        // :335
Assert.Less(obs.WorstStickError, 0.02f, "…발버둥 때문에 몸이 커서에서 …유닛 떨어졌습니다 …"); // :339-342
// HoldAndObserve
_cursorWorld = start;                                                                     // :172
bb.Machine.ChangeState(StickmanStateId.Dragged, isForcedInterrupt: true);                 // :173
while (t < seconds)                                                                       // :181
{
    yield return null; float dt = Time.deltaTime; t += dt;                                // :183-185
    _cursorWorld += cursorVelocity * dt;                                                  // :186
    …
    result.WorstStickError = Mathf.Max(result.WorstStickError,
        Vector2.Distance(bb.Body.position, _cursorWorld));                                // :201-202
}
```
- 허용 `0.02f`는 **테스트 리터럴**이다(`:339`). 프로덕션에는 대응 상수가 없다. 프로덕션 계약은 수치가 아니라 「같은 프레임 즉시 대입」이다(`DragThrowState.cs:490-494`, `dragFollowSmoothTime = 0`).
- 관측 창 1.2초는 `Time.deltaTime` 누적이다. 벽시계 규칙은 형식상 지킨다.
- 커서는 테스트가 **스크립트로** 움직인다(`:132` `bb.CursorProvider = TryGetScriptedCursor`, `:152-158`).

### 13-2. 실측
| 실행 | 결과 | 최대 밀착 오차 | 오차 ÷ 2.6249 = 등가 프레임 dt |
|---|---|---|---|
| `Logs/vc-5d/P`(09-15, `e899fb8` 전량) | **빨강** | 로그 0.0225 / xml `0.0225006416f` | **8.57ms** |
| `Logs/vc-5d/ISO_DragStruggle_1` | 통과 | 로그 0.0015 | 0.57ms |
| `Logs/vc-5d/ISO_DragStruggle_2` | 통과 | 로그 0.0075 | 2.86ms |
| `Logs/vc-5d/ISO_DragStruggle_3` | 통과 | 로그 0.0068 | 2.59ms |
| `Logs/coder_silhouette_play`(09-01) | **빨강** | xml `0.036372032f` | **13.86ms** |
| `docs/verify/runs/part2-final_play`(09-08) | **빨강** | xml `0.027390115f` | **10.43ms** |

- 문턱의 등가 dt = 0.02 ÷ 2.6249 = **7.62ms**다(|(2.5, 0.8)| = √6.89 = 2.6249).
- 저장소 전체 xml(`Library`·`Temp` 제외, 파이썬 전수)에서 이 테스트가 든 xml은 126개다(★ 실행 횟수가 아니다 — 추적 파일 `BASELINE-20260902-1201_play.xml`은 `coder_hdrift_play_all`과 같은 실행의 복사본이고 격리 3개도 포함, 2026-09-15 verify-change 정정): `Passed` 122 · `Failed` 4.
  - 실패 4 = 밀착 오차 3(위 표) + 다른 사유 1(`Logs/coder_36_play.xml` 08-31, `Unhandled log message` — §9 자백 25).
  - 대조: 없는 이름은 `ABSENT`로 나왔다.
- **내부 대조**: 같은 픽스처의 커서 속도 0 관찰(`:236`, `:265`)은 격리 3회·전량 1회 로그 모두 `최대 밀착 오차=0.0000`이다. 「오차 ∝ 커서 속도」 예측과 맞는다.
- 로그 창(`P.log:7915-7922`)에 `[스톨` 줄은 0이다. 8.6ms 프레임은 스톨 문턱 100ms에 한참 못 미쳐 로그로는 보이지 않는다.
- 5-d 무관: §분류 규칙 사례 3.

### 13-3. 기전
**(가) 몸통은 그 프레임 Update에서 읽은 커서에 즉시 붙는다 — 사실(코드).**
- 호출 순서
  1. `StickmanAgent.Update`(`Core/StickmanAgent.cs:954`) → `_machine.Tick(dt)`(`:1008`)
  2. → `DragThrowState.Tick`(`States/DragThrowState.cs:179`) → `TryGetCursorWorldPosition`(`:197`). 테스트가 꽂은 `TryGetScriptedCursor`(테스트 `:152-158`)는 **그 순간의 `_cursorWorld`**를 돌려준다.
  3. → `FollowCursor`(`:208`) → `smoothTime <= 0`이면 `SetBodyPositionImmediate`(`:490-494`) → `MoveBodyToWorld`(`:512`, 물리 바디와 Transform 동시 기록)
- 발버둥은 루트 **위치**를 건드리지 않는다(`:572-576` 설계 문단). `TickStruggle`(`:209`)은 각도와 회전만 바꾼다.
- 잡은 오프셋은 0이다. 진입 순간 커서 = 몸통(테스트 `:167-173`)이고, 오프셋은 그때 캡처된다(`DragThrowState.cs:155-161`). 지면 클램프(`:485-488`)는 지면 +4유닛 위에서 위로 끄는 이 배치에서는 걸리지 않는다(추정).

**(나) 테스트는 그 뒤에 커서를 옮기고 잰다.**
- `yield return null`은 Update 뒤에 재개된다(Unity 코루틴 순서, 이 버전에서는 미실측 — §3-2·§9 자백 26).
- ⇒ 프레임 N에서 몸통 = 커서(N−1)이고, 측정에 쓰는 커서 = 커서(N−1) + v·dt(N)이다. **오차 = |v| · dt(N) = 2.6249 × dt(N).**
- ⇒ `WorstStickError` = 2.6249 × (1.2초 창 안의 **최대 프레임 dt**). **빨강 ⇔ 창 안에 dt > 7.62ms인 프레임이 하나라도 있다.**
- 이 오차는 제품의 추종 지연이 아니라 **리그의 커서 갱신 순서**가 만든 한 프레임이다. 실사용에서는 `CursorProvider`가 OS 커서를 호출 시점에 읽으므로 이 지연이 없다(추정 — 배선은 `StickmanAgent.cs:705-708`, OS 조회 시점은 미실측).

### 13-4. 판정
- **틀린 쪽: 테스트(추정 — 러너 판별 전).**
- **#8·#9와 같은 형태인가: 아니다. 다른 가족이다.**
  - #8·#9는 「단언 창 < 대상의 최장 무작업 구간」, 즉 벽시계 위상 문제였다. #10은 **오차 자체가 프레임 dt에 비례**한다. 창을 늘려도 고쳐지지 않고, 오히려 긴 프레임을 만날 확률이 는다.
  - #9의 「예산 = 경계」 형태도 아니다. 창 1.2초와 문턱 0.02는 서로 다른 값이다.
  - CLAUDE.md 벽시계 규칙(프레임 수 예산 금지)은 **형식상 지킨다**. 그러나 그 규칙이 막으려던 병, 「프레임 속도가 판정을 바꾼다」의 **반대 방향**이다 — 2,000fps가 아니라 **느린 프레임 하나**가 판정을 바꾼다.
- 전량에서만 빨간 이유(추정): 35분짜리 전량에는 로그·GC·에셋 해제로 긴 프레임이 섞인다. 격리 최대 2.9ms, 전량 빨강 8.6~13.9ms다.

### 13-5. 수정 방향 후보 (테스트 쪽, 채택은 리더)
**A (권고) — 「에이전트가 그 프레임에 읽은 커서」와 비교한다.**
- `:186` 앞에 `Vector2 cursorSeenThisFrame = _cursorWorld;`를 두고, `:201-202`를 `Vector2.Distance(bb.Body.position, cursorSeenThisFrame)`로 바꾼다. 이 값은 프레임 N의 에이전트 Tick이 읽은 값과 같다.
- 그러면 즉시 대입 경로의 오차는 dt와 무관하게 부동소수 수준이다. 문턱을 **속도·dt와 무관한 작은 값**으로 조일 수 있다. 수치는 러너가 정한다(기대값을 프로덕션 함수로 만들지 않는다).
- 같은 테스트 안에 대조를 둔다.
  - 공허성: 창 동안 커서 누적 이동 > 1유닛
  - 옛 지표(`_cursorWorld`와의 거리)의 최댓값도 로그에 남긴다. 「옛 지표 = 2.6249 × 최대 dt」가 러너에서 성립하는지 본다.
- A는 「한 프레임 늦게 붙는」 **제품 회귀**를 여전히 잡는다. 회귀 시 몸통 = 커서(N−2)라 새 지표도 v·dt(N−1)만큼 벌어진다(변이 N2).

**B (기각) — 허용을 넓힌다(예: 0.05).** 19ms 프레임 하나(0.05 ÷ 2.6249)면 다시 빨갛다. 게다가 옛 스무딩 회귀(속도 × 0.08초, `DragThrowState.cs:443-445`)보다 작은 지연 회귀를 통과시킨다.

**C (기각) — 측정 dt로 문턱을 만든다(`오차 < 속도 × 최대 dt + ε`).** 「한 프레임 늦게 붙음」 제품 회귀도 정확히 같은 크기라 **구별하지 못한다.** 검사기가 대상과 같이 움직인다(TEAM.md 「생성기와 검사기가 같이 틀린다」).

**D (기각) — 재시도, 또는 긴 프레임이면 `Assume`.** 판정 불가는 `failed=`에 잡히지 않는다(§0 공통 규칙).

**E (기각) — 프로덕션 변경**(예: 커서를 LateUpdate에서 다시 읽기). 제품은 계약대로 동작한다.

**F (보조) — 커서 갱신을 에이전트보다 먼저 도는 컴포넌트로 옮긴다**(스크립트 실행 순서). A와 효과는 같지만 리그가 커지고 실행 순서 설정에 기댄다. A를 먼저 한다.

### 13-6. 변이 (진단 사본·격리 미러)
| 변이 | 주입 | 수정본 A | 옛 테스트 |
|---|---|---|---|
| N1 (긴 프레임 강제) | 관찰 창 중간의 한 프레임에 테스트 쪽 `System.Threading.Thread.Sleep(15)` | **초록**(오차 부동소수 수준) | **결정적 빨강**(약 0.04) — 가설 인과의 한 축 |
| N2 (한 프레임 지연 회귀) | 프로덕션 진단 사본에서 `FollowCursor`가 직전 프레임 커서를 쓰게 한다 | **빨강** | 격리에서는 대개 **초록**(약 2.6249 × 2dt ≈ 0.015) — 회귀를 **못 잡는다** |
| N3 (스무딩 회귀) | `_clonedConfig.dragFollowSmoothTime = 0.08f` | 빨강 | 빨강 |
| N4 (루트 흔들기 회귀) | `TickStruggle`에서 루트 위치에 0.05유닛 사인을 더한다 | 빨강 | 빨강 |

### 13-7. 러너 판별 설계 (러너 라운드 — 리더 배정 필요)
1. **진단 사본**(커밋 안 함): 관찰 루프의 매 프레임 `Time.frameCount`, `Time.deltaTime`, 옛 지표 거리, 새 지표 거리(A), `2.6249 × dt`를 한 줄씩 남긴다. 로그 줄 수가 프레임 수와 다르면 그 회차는 무효다.
2. **가설별 예측**
   - **H10(한 프레임 커서 지연)**: 옛 지표가 매 프레임 `2.6249 × dt`와 ±1e-4 안에서 일치하고, 새 지표는 0에 가깝다.
   - **H-기타**: 옛 지표가 `2.6249 × dt`와 어긋나고 새 지표도 큰 프레임이 있다 → 제품 쪽 추종 문제 → **디버거로 이동**.
3. **개입**: N1을 10회 돌려 옛 테스트 10/10 빨강 · 수정본 10/10 초록이면 인과가 확정된다. 자연 재현(전량)에 기대지 않는다.
4. **수정본 A 합격 조건**: 격리 10회 초록, N2·N3·N4 각각 빨강, PlayMode 전량 1회에서 이 테스트 초록. xml 판정은 `docs/verify/nunit_verdict.py`로 한다.

---

## 14. 관련 위험 — 이 명세의 PlayMode 목록 밖 (러너 배정 참고)

### W1. 흔적 바이트 불변 단언이 기록 시각 칸에 기댄다 — Windows 시각 해상도 위험 (2026-09-15, `e899fb8` 기준)
- **사실(코드)**
  - 같은 값으로 다시 쓸 때 흔적 JSON에서 달라질 수 있는 칸은 기록 시각 하나다: `Platform/ReservedBarRestoreLedger.cs:221` `writtenAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)`. 나머지 `version`·`active`·`originalAutoHide`·`platform`·`pid`(`:215-223`)는 같은 프로세스·같은 인자면 같다.
  - `Tests/EditMode/SessionEndShutdownTests.cs:1011-1019` `N6_대조_흔적을_같은_값으로_다시_쓰면_바이트가_달라진다` — `Close(true, "TestOS")`를 연달아 두 번 부르고(`:1015`, `:1017`) `CollectionAssert.AreNotEqual(first, …)`(`:1018`)로 판정한다.
  - 같은 파일의 재진입 표 — `:1257`에서 끼어든 순서가 반환된 순간의 바이트를 저장하고, `:1356-1357` `CollectionAssert.AreEqual(ledgerBytesAtInnerReturn, …)`로 「바깥이 다시 쓰지 않았다」를 판정한다.
- **위험(추정, 미확인)**: `DateTime.UtcNow`의 해상도가 연속 두 쓰기 간격보다 거칠면 두 기록 시각이 같아진다. Windows에서는 흔히 1~15.6ms로 알려져 있지만, 이 저장소의 Unity Mono Windows에서는 **미측정**이다(§9 자백 27).
  - `N6_대조` → **거짓 빨강**(재기록했는데 바이트가 같다).
  - 재진입 표 `:1356` → **맹점**(바깥이 같은 틱 안에 다시 써도 바이트가 같아 초록). 교정 테스트 N6이 같은 러너에서 초록이어도 쓰기 간격이 달라 이 맹점을 보증하지 못한다.
  - macOS의 현재 초록은 이 위험을 가리지 못한다.
- **권고**: QG-1 Win64 EditMode 실행 **전에** 두 단언을 시각 비의존으로 바꾼다.
  - (가) 흔적 쓰기 횟수 계기 — 프로덕션 테스트 계수, `ReservedBarRestoreLedger` 소유라 dev-platform
  - (나) 시계 주입 — 테스트가 쓰기마다 서로 다른 시각을 공급한다
  - (다) 기각: 두 `Close` 사이에 `Thread.Sleep`을 넣는다 — 해상도 가정을 테스트에 박는다.
- 담당: dev-platform(계기·주입 자리) + test-engineer(단언), 리더 배정. EditMode 사안이라 이 명세의 PlayMode 알려진 목록에는 넣지 않는다.
