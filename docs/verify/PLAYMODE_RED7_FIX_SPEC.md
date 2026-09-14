# PlayMode 빨강 7건 수정 명세 (test-engineer, 2026-09-14 · 2026-09-15 갱신: 간헐 #8 추가 · #4 간헐 확정 · 분류 규칙)

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

### 알려진 목록 — `Logs/vc-e4/P-full.xml` 기준
이름은 xml `fullname`에서 파이썬으로 대조했다(`StickMate.Tests.PlayMode.` 뒤). 아래 8개 이름은 전부 적중했다. 음성 대조로 넣은 없는 이름은 `ABSENT`로 나왔다.

| 분류 | # | 이름 | 절 |
|---|---|---|---|
| 알려진 빨강 | 1 | `PortraitEyeVisibilityTests.EyesAreAbsentUnderEveryGlassesItem` | §1 |
| 알려진 빨강 | 3 | `CostumeFocusPropLifecycleTests.몰입기_도중_취소해도_프롭이_화면에_남지_않는다` | §3 |
| 알려진 빨강 | 5 | `SettingsWindowReturnPathTests.ClosingSettingsReopensTheInfoWindowItReplaced` | §5·6 |
| 알려진 빨강 | 7 | `AccessoryFillRenderingTests.왕관은_채워지되_얹는_물건으로_남는다` | §7 |
| **간헐** | 4 | `PetBalloonClearsBodyTests.실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다` | §4, §4-7 |
| **간헐** | 8 | `CostumeFocusStillTierTests.몰입기_동안_절감등급_Still에_도달한다` | §11 |
| 판정 불가 | 2 | `TodoBoardDateNavigationTests.ClickingACalendarCellPicksTheDayInsteadOfDraggingTheWindow` | §2 |
| 판정 불가 | 6 | `SettingsWindowReturnPathTests.ClickingOutsideSettingsNeitherClosesItNorReturnsTheInfoWindow` | §5·6 |

- 합계: 실패 6(알려진 빨강 4 + 간헐 2)이 xml `failed="6"`과 같고, 판정 불가 2가 `inconclusive="2"`와 같다.
- 간헐 2건은 **이번 전량에서 빨강**이었다. 다음 전량에서 초록이 나와도 「고쳐졌다」로 읽지 않는다.

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
