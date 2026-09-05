# 장비 데이터 변경의 시스템·스키마 영향 — 재질색 / 조각 계약 v2 / R19

`coder-systems`, 2026-09-05. **분석·설계 문서. 이 라운드에 프로덕션 `.cs`·`.asset` 0줄 수정.**
근거: `docs/EQUIPMENT_PALETTE.md` · `docs/EQUIPMENT_HANDOFF_PORT_SPEC.md` §14-10-8 ·
`design/equipment/verify/r19_coords.txt` · 실측은 전부 작업 트리(미커밋 포함)의 소스와 `.asset`.

> **읽는 순서**: §0 한 줄씩 → §2(버전 판정, 리더가 가장 먼저 볼 것) → §7 표 → §10 리더 판단.

---

## 0. 한 줄씩

| # | 결론 | 근거 |
|---|---|---|
| 1 | **재질색은 세이브에 0바이트다.** 세이브에는 아이템 **아이디 7개**만 있고 색·좌표·알파·층은 한 칸도 없다 | §1 |
| 2 | ★ **브리프의 전제 1건이 실물과 다르다 — `material`/`material2` 라는 「필드 2개」는 존재하지 않는다.** 착지한 구현은 칸을 **안 만들고** `icon[].color`(tone 0/1)를 재질색으로 재해석했다. 이건 실수가 아니라 이음매를 하나로 유지한 **옳은 선택**이다 | §1-3 |
| 3 | **`CharacterSaveStore.CurrentVersion` 상승 요구 없음 — v10 유지.** 이번 변경이 만든 세이브 필드가 **0개**다. v11 하위 호환 테스트를 새로 쓸 의무가 발생하지 않는다 | §2 |
| 4 | 대신 **`StickPackManifestSO.SchemaVersion`이 1 → 2로 이미 올라갔고**(작업 트리) 그 의무는 `WornShapeContractCompatTests`가 **이미 이행했다**(v1 데이터 → v2 앱, 8건) | §2-3 · §5 |
| 5 | **L-1(등급색을 조각에서 뺌)의 죽은 코드는 0건이다.** 칸 자체를 안 만드는 방식이라 「남은 참조」가 원리적으로 생기지 않는다. 등급을 읽는 프로덕션 경로는 **9곳, 전부 카드 크롬**이고 전부 살아 있다 | §3 |
| 6 | **세트 시너지는 프로덕션에 0줄이다.** `setId`는 코드에 **없다**(전수 grep 0건, 양성대조 통과). 재질색이 세트 규칙에 주는 영향도 **원리상 0** — 세트 판정 입력에 색이 없다 | §4 |
| 7 | **팩은 늘어난 필드를 「안 채워도」 안전하다**(원칙 4 유지). 새 필드는 전부 **0/false = v1과 같은 뜻**이고, 팩 아이템은 `IsHandoffCode`가 기본 0~3번만 참이라 **구조적으로** 에셋 경로로 떨어진다 | §5 |
| 8 | ★ **팩에는 재질색 원천이 둘 있다 — 그리고 둘 중 하나는 아무도 안 읽는다.** `StickPackManifestSO.primaryColor/secondaryColor`는 `PackRegistry`가 담기만 하고 **프로덕션 소비자 0건**이다. 팩 아이템의 실제 색은 그 팩 `.asset`의 `icon[].color`에서 온다 | §5-3 · **L-A** |
| 9 | **세이브 크기 영향 0바이트 / 로딩 영향은 「1회 로드 + 재구성 시」로 한정.** `.asset` 총량 68,610 → **123,543 B(×1.80)**, 착용 조각 15 → **27개**. 남은 이행분 **70조각**을 다 내리면 **약 340 KB** 추정 | §6 |
| 10 | 리더 요청 추가분 — 「우클릭 폐지」 문장 **파일 6곳 / 줄 12곳**의 행선지표, 신규 정책 클래스 **후보 경로 2개** | §8 · §9 |

---

## 1. 무엇이 세이브에 들어가고 무엇이 카탈로그에만 있는가

### 1-1. 경계선 — 실측

`Core/CharacterSaveStore.cs`의 `SaveData`(:178~380)에 있는 **장비 관련 필드는 이것이 전부**다.

| 필드 | 뜻 | 버전 |
|---|---|---|
| `equippedHead` / `equippedEyes` / `equippedNeck` / `equippedShoulders` | v1~v4 시절 bool 4개. **계속 정확한 값으로 기록**(파일 안에 어긋나는 두 문장을 안 남긴다) | v1 |
| `wornHead` / `wornEyes` / `wornNeck` / `wornShoulders` / `wornHair` / `wornFx` / `wornPet` | **지금 걸친 아이템 아이디 문자열 7개.** 빈 문자열 = 미착용 | v5 |
| `purchasedItemIds` | 상점에서 산 아이템 아이디 목록 | v10 |

**세이브에 색·좌표·알파·층·표면비트·획배수는 한 칸도 없다.**

부재 단언이므로 **양성 대조를 붙인다**(CLAUDE.md 거짓통과 #4):

```
grep "Color|color" Core/CharacterSaveStore.cs    → inkColorSaved / inkColorName 두 필드뿐
grep -c "public string" 같은 파일                → 14   ← 프로브가 살아 있다(양성 대조 통과)
```

`inkColorName`은 **사용자가 고른 캐릭터 잉크색**이고 값은 hex가 아니라 **이름 문자열**(`"Black"`/`"White"`)이다.
아이템 재질색과 무관하다. ⇒ **「재질색은 카탈로그 전용이어야 한다」는 요구는 실제로 지켜지고 있다.**

### 1-2. 왜 이 경계가 옳은가 — 되짚어 둔다

세이브에 색을 넣으면 **팩 업데이트로 색을 못 바꾼다**. 더 나쁜 것은 그다음이다:
색이 세이브에 앉는 순간 **같은 아이템의 색이 두 곳(파일/카탈로그)에 살고**, 둘이 갈라지는 날 증상은
「내 왕관만 옛날 색」이다. 파일을 열어봐도 안 이상하다 — 파일에는 **정상적인 옛 색**이 적혀 있으니까.
이건 이 저장소가 이미 겪은 형태다(월드 ↔ 초상화 FX 0번).

### 1-3. ★ 브리프 정정 — 「필드 2개(material/material2)」는 착지하지 않았고, 그게 맞다

브리프는 *"아이템별 재질색 필드 2개(material/material2)"*가 오늘 추가된다고 적었다.
**실물은 다르다.** `Core/AccessoryDefSO.cs:558-560`가 그 자리에 이렇게 적혀 있다:

> 재질색은 **별도 칸이 아니다** — `M = icon[].color(tone 0) = entry.PrimaryColor`,
> `M2 = tone 1 = entry.SecondaryColor`. 카드·몸이 그 한 원천을 읽는다.
> 칸을 하나 더 두면 같은 아이템의 색 원천이 둘이 된다(2026-08-30 「카드엔 색이 있는데 착용하면 없다」의 뿌리).

전수 확인(양성 대조 포함):

| 프로브 | 결과 |
|---|---|
| `grep "public Color material\|material2\|materialColor"` 프로덕션 `.cs` | **0건** (히트 2건은 `HandoffPalette` **생성자 인자 이름** — 런타임 `readonly struct`, 직렬화 안 됨) |
| `grep -rl "material" Resources/Items/` | **0파일** |
| 양성 대조: `grep -rl "color:" Resources/Items/` | **42파일** ← 프로브 살아 있음 |
| 양성 대조: `grep -c "public Color " Scripts/Core/` | **10** ← 프로브 살아 있음 |

`Material`/`Material2`는 **`Interaction/AccessoryShapeBuilder.HandoffPalette`의 런타임 필드**이고
(`:1241`, `:1243`) 값은 `AccessoryHandoffPalette.Body/Card`가 `ItemCatalogEntry`에서 매번 만든다.
**에셋에도 세이브에도 없다.** ⇒ 「필드 2개가 늘었다」로 리더에게 보고되면 안 된다.

### 1-4. 카탈로그에만 있는 것 (오늘 늘어난 것 전부)

| 어디 | 무엇 |
|---|---|
| `AccessoryDefSO.icon[]` | `kind` · `values[]` · **`color`** · `tone`(0~6으로 도메인 확대) |
| `AccessoryDefSO.wornShapes[]` | `name` `loop` `filled` `tone` `swayStart` `swayCount` `swingDegrees` `terms[]` (v1 8칸) **+ v2 9칸**: `surfaces` `strokeMult` `strokeInR` `noStroke` `alpha` `lineAlpha` `underBack` `layer` `bodyFixed` |
| `AccessoryDefSO` 아이템 단위 | `wornGroupAlpha` `wornScale` `wornScaleY` `wornOffsetYInR` `wornMirrorX` |
| `AccessoryDefSO` 규칙 | `requiredLevel` `hidesHair` `cohortId` `declaredRarity` |
| 코드(생성물) | `Interaction/AccessoryShapeBuilder.Handoff.cs` — HEAD 4 · EYES 4 · BACK 4의 좌표 + `WornTransformCode` |

---

## 2. 저장 스키마 버전 — **상승 요구 없음(v10 유지)**

### 2-1. 판정

**이번 장비 변경은 `CharacterSaveStore.CurrentVersion`을 올릴 이유가 하나도 없다.**

이 저장소의 실제 규칙은 두 개이고(`CharacterSaveStore.cs:117-144`의 리더 판정 문단),
**둘 다 해당하지 않는다**:

| 규칙 | 이번 변경에 적용되는가 |
|---|---|
| (가) **필드의 「없음」이 그 필드의 0값과 다른 뜻일 때** 버전을 강제한다 | **해당 없음 — 새 세이브 필드가 0개다.** 「없음」을 따질 대상 자체가 없다 |
| (나) **다운그레이드 방어** — 새 값이 옛 번호로 앉으면 구버전이 그것을 덮어 지운다(v10을 올린 진짜 이유) | **해당 없음.** 이번 변경은 디스크에 **한 바이트도 새로 쓰지 않는다.** 구버전 빌드가 이 트리가 만든 세이브를 읽어도 잃을 것이 없다 |

`docs/EQUIPMENT_PALETTE.md` §2-3의 *"세이브·스키마 0바이트"*는 **독립 확인 결과 참이다**
(§1-1의 grep + 양성 대조). 설계자의 주장을 그대로 옮긴 것이 아니라 다시 쟀다.

### 2-2. 그래서 「vN-1 하위 호환 테스트 1건」 의무도 발생하지 않는다

CLAUDE.md 의무는 **`CurrentVersion`을 올리는 라운드**에 붙는다. 안 올리므로 안 붙는다.
현재 v10의 의무는 이미 이행됐다 — `Tests/EditMode/EquipmentMigrationTests.cs`:

- `:715` `v9_파일을_읽어도_게임화_14필드가_안전한_기본값이_되고_가진_것을_빼앗지_않는다()`
- `:807` `v10_왕복은_동전과_구매이력과_등급해금과_장착한_춤을_보존한다()`
- `:844` `게임화_필드는_v10_번호로_기록된다()` — 숫자를 안 베끼고 `CharacterSaveStore.CurrentVersion` ·
  `FirstVersionWithGameplayCurrency` **상수를 참조**한다(:850, :854). 규칙 준수 확인함.

### 2-3. 그럼 **의무가 붙는 쪽은 어디인가** — 팩 스키마다

작업 트리에서 `StickPackManifestSO.SchemaVersion`이 **1 → 2**로 올라갔다(`:136`).
같은 종류의 의무(옛 데이터가 새 앱에서 여전히 옳은가)가 여기 붙고, **이미 이행돼 있다**:

`Tests/EditMode/WornShapeContractCompatTests.cs`(신규, 미커밋) —
`v1_스트림에_새_키가_없으면_기본값이_들어오고_그_뜻은_v1_그대로다()` 외 8건.

★ 리더 확인 요청: **이 테스트가 지금 실제로 빨간불을 볼 수 있는가**는 내가 못 쟀다(배치모드 미실행).
`coder-systems` 원칙 *"고치기 전에 빨간불을 먼저 봐라"*의 미충족분으로 남긴다.

### 2-4. ★ 그래도 「v11 예약」은 지금 정해 두는 것이 싸다 (설계 권고)

`.claude/agents/coder-systems.md`의 경고 — *"필드를 나눠 넣으면 v11·v12·v13이 된다. 한 번에 모아 넣어라"* —
가 걸릴 자리가 **이미 둘 보인다.** 둘 다 지금은 필드가 0개지만, 착수하는 날 각각 v11·v12가 된다.

| 후보 | 무엇이 세이브를 요구하는가 | 지금 상태 |
|---|---|---|
| **세트 시너지** | 「이 세트를 완성한 적 있다」의 영구 기록(연출·대사 전환이 한 번만 나야 한다면). 세트 **자체**는 파생이라 저장 불필요 | 런타임 0줄(§4) |
| **팩 엔타이틀먼트 캐시** | 오프라인에서 산 팩을 잠깐 못 쓰는 것을 막는 캐시 | `ENTITLEMENT_CONTRACT` §E-4-a가 **저장 금지**로 닫아 뒀고 `EntitlementNotInSaveAuditTests`가 잠근다. **열지 마라** |

⇒ **권고**: 다음에 세이브 필드가 필요해지는 라운드가 오면, 그 라운드가 **세트·유예·스탯을 한 번에**
받도록 리더가 목록을 먼저 확정한다. `itemGraceBaselines`가 v10에 **자리만** 미리 들어간 것과 같은 처방이다
(`CharacterSaveStore.cs:373-379` — *"지금 자리를 안 잡으면 U-2가 「존치」로 나오는 순간 v11이다"*).

### 2-5. ★ 그럼에도 필요할 때를 위한 **v10 → v11 하위 호환 테스트 설계**(문장, 구현은 `coder`)

지금은 **쓰지 마라**(§2-1). 아래는 **v11이 실제로 필요해졌을 때** 그대로 쓰라고 남기는 설계다.
「테스트 이름 하나 + 어떤 함정을 잡는가」까지 문장으로 못박는다.

> **테스트 1건**: `Tests/EditMode/EquipmentMigrationTests.cs`에
> `v10_파일을_읽어도_<새묶음>이_안전한_기본값이_되고_가진_것을_빼앗지_않는다()`

**반드시 지킬 것 5가지** (앞선 v9·v8 테스트가 실제로 지킨 형태 그대로):

1. **픽스처를 손으로 쓴다.** v10 JSON을 **문자열 리터럴로 직접 적고**, 그 안에 v11 키를 **한 개도 안 넣는다.**
   ★ 픽스처를 `Save()`로 만들면 **그 순간 v11 파일이 되어 아무것도 못 잰다** — 생성기와 검사기가
   같이 틀리는 형태(TEAM.md §「생성기와 검사기가 같이 틀린다」)다.
2. **버전 숫자를 베끼지 않는다.** 픽스처의 `"version"`은 `CharacterSaveStore.CurrentVersion - 1`이 아니라
   **그 버전을 뜻하는 상수**(`FirstVersionWith…`)로 적는다. 리터럴 `10`을 적으면 v12 라운드에 조용히 낡는다.
3. **「기본값이 들어왔다」로 끝내지 않는다 — 「가진 것을 안 빼앗겼다」를 같은 테스트에서 함께 단언한다.**
   v9 테스트가 이 형태다: 신규 필드가 0/false/빈이 되는 것 **그리고** `coinBalance`가 아닌
   **기존 착용·레벨·할일이 그대로**임을 같이 본다. 앞의 절반만 있으면 「전부 날리고 기본값」도 통과한다.
4. **「없음 ≠ 0」인 필드가 새 묶음에 하나라도 있으면 동반 불리언을 만들고, 그 불리언을 테스트가 지목한다.**
   `dayBoundaryOffsetSaved`가 그 선례다(0이 UTC+0이라는 실재 시간대다).
5. **왕복 테스트를 짝으로 붙인다** — `v11_왕복은_<새묶음>을_보존한다()`.
   하위 호환만 있으면 「읽기는 되는데 쓰면 사라진다」를 못 잡는다.

**양성 대조**(이 테스트가 정말 무언가를 잡는지): 픽스처에 v11 키를 **일부러 하나 넣은** 변형을 만들고
그때는 기본값이 **아닌** 값이 들어오는지 같은 클래스에서 확인한다. 그게 없으면
「필드를 읽는 코드가 통째로 빠져도 초록」이 된다.

---

## 3. 등급색이 조각에서 빠진 여파 (리더 판정 L-1)

### 3-1. 등급을 읽는 프로덕션 경로 — 전수

**유일한 출처**는 `ItemCatalog.Rarity(slot, itemIndex)`(`ItemCatalog.cs:874`)이고
그 유일한 구현은 `RarityOfMember`(`:895`)다. 색·칸 수의 유일한 출처는 `UiChrome`이다.

| # | 자리 | 무엇을 읽는가 | 살아 있는가 |
|---|---|---|---|
| 1 | `Interaction/CharacterInfoWindow.Cards.cs:308-311` | 착용 요약 행 — `RarityName` · `RarityColor` · `RarityBorder` | 살아 있다 |
| 2 | `…Cards.cs:358` → `:373-374` | 카드 낱말 + 낱말 색 | 살아 있다 |
| 3 | `…Cards.cs:424` | **카드 테두리 「기본 자리 승계」**(안 D) — `RarityBorder` | 살아 있다 |
| 4 | `…Cards.cs:517` → `:525-529` | **리본 칸 수**(`RarityFilledCells`) + 칸 색(`RarityColor`) — **주 채널** | 살아 있다 |
| 5 | `…Cards.cs:623` → `:625` | 상세 썸네일 테두리 | 살아 있다 |
| 6 | `…Cards.cs:650-652` | 상세 낱말 | 살아 있다 |
| 7 | `Interaction/CharacterInfoWindow.Inventory.cs:170` | 보관함 행 테두리 | 살아 있다 |
| 8 | `…Inventory.cs:232` | 보관함 행 부제 낱말 | 살아 있다 |
| 9 | `Core/ItemCatalog.cs:1060` `RarityName` | 낱말의 유일한 출처 | 살아 있다 |
| — | `Core/ItemRarity.cs` `DeclaredRarityRules.TryResolve` + `ItemCatalog.MaxDeclaredRarityForPack` | 팩 선언 → 등급, 그리고 팩 상한(희귀) | 살아 있다(팩 0개라 아직 안 탄다) |

**9곳이 전부 카드 크롬(창 안)이다. 조각(몸·카드 아이콘)에는 0곳.**

### 3-2. 색 참조가 남아 죽은 코드가 되는 곳 — **0건**

L-1을 「칸을 안 만드는 것」으로 구현했기 때문이다. `AccessoryShapeBuilder.HandoffPalette`(`:1231`)에는
**`Accent`(등급색) 필드가 애초에 없다**. 그래서

- `HandoffFillBase`(`:1261`) / `HandoffLineBase`(`:1275`)가 등급색을 **참조할 대상이 없다**(컴파일 불가).
- `AccessoryHandoffPalette.Body/Card`(`:33`, `:42`)의 인자에도 등급이 없다 —
  **부르는 쪽이 등급을 계산조차 하지 않는다.**

★ **이것이 「참조하지 않는다」의 구조적 형태**이고, 산문 규칙보다 강하다.
`Tests/EditMode/HandoffRarityAbsenceTests.cs`가 부재 단언(`:34`)과 **양성 대조**(`:70`
「컨트롤 — 재질색 자리에 등급색을 넣으면 비교기가 잡는다」)를 **같은 클래스에서** 대조한다.
CLAUDE.md가 요구한 *"부재 단언은 실재했다가 사라진 것인지를 같은 테스트 안에서 대조로 못박는다"*를
충족한다. 그리고 `:63`이 `Assert.Greater(handoffPieces, 100, "…아래 부재 단언이 공허합니다")`로
**모집단이 비어서 초록이 되는 형태**(거짓통과 #5)까지 막는다. **이 설계는 그대로 두면 된다.**

### 3-3. 여파가 실제로 남는 곳은 **문서 3건**이지 코드가 아니다

| 위치 | 낡은 문장 | 정정 방향 |
|---|---|---|
| `Core/AccessoryShapeContract.cs:17` | *"강조색 A = 그 아이템의 등급색(런타임)"* — R16 서술 | L-1로 폐기됨. `:76`이 이미 정정 문단을 갖고 있으나 **`:17`은 그대로 남아 같은 파일 안에서 서로 모순**이다 |
| `docs/EQUIPMENT_HANDOFF_PORT_SPEC.md` §14-6 / §14-10-4 「데이터 계약 9′」 | *"강조 = 등급색 한 색"* | §14-10-8 C-1 개정이 대체했다고 적혀 있으나 원문 절이 살아 있다 |
| `docs/EQUIPMENT_PALETTE.md` §1 표(인계본 호출부) | 인계본이 `accent = it.rc`(등급색)를 쓴다는 **사실 기술** | 정정 불필요 — **인계본의 사실**이지 우리 규칙이 아니다. 오해 방지 문구만 |

★ 이 저장소는 *"같은 서술이 6개 파일에 복사돼 있다가 전부 낡았다"*(랙돌 절)를 이미 겪었다.
**`AccessoryShapeContract.cs:17`은 그 형태의 초기 단계다** — 파일 하나 안에서 이미 갈라져 있다.

---

## 4. 세트 시너지

### 4-1. 지금 상태 — **프로덕션에 0줄**

| 프로브 | 결과 |
|---|---|
| `grep -rn "setId\|SetId" Assets/_Project/Scripts --include="*.cs"` | 장비 세트 관련 **0건**(히트 전부 `SetIdleAutoCloseSecondsForTests` — 무관한 함수명) |
| 양성 대조: 같은 방식 `grep -rn "CohortId"` | **78건** ← 프로브 살아 있음 |
| `Interaction/CharacterInfoWindow.cs:1669` (자백) | *"세트 판정 런타임도 아직 없다(ItemRarity.cs의 주석 한 줄이 전부다). 카드가 아니라 **비활성 문구**를 둔다 — 빈 카드를 두면 「0/4 달성」이라는 거짓 진행도로 읽힌다"* |

즉 화면의 [테마 세트 / SET]는 **자리만 잡힌 비활성 패널**이다. 옳은 상태다.

### 4-2. 세트 판정이 **무엇을 볼 것인가** (확정된 설계, 미구현)

`design/systems/ECONOMY_SPEC.md` **요건 DS-4**:

> 세트 판정은 **`setId`**로 한다. 기본 6세트는 `setId = "base.rank{N}"`로 rank에서 **파생**하고
> (에셋 무변경), 팩은 `setId = 팩 아이디`다. 세트는 총 12개지만 **동시 완성은 항상 최대 1개**라
> +2 보너스는 스택되지 않는다.

⇒ **판정 입력은 「슬롯 4종(HEAD·EYES·NECK·BACK)의 `setId`가 모두 같은가」 하나**다.

### 4-3. 재질색 도입이 세트 규칙에 주는 영향 — **원리상 0**

세트 판정의 입력 = `setId`(rank 파생 또는 팩 아이디). **색은 입력이 아니다.**
그리고 rank의 입력 = `requiredLevel` + `cohortId`(`RarityOfMember`) — 여기에도 색이 없다.
오늘 바뀐 것은 `icon[].color` 1건(왕관 M2)과 조각 표현 파라미터뿐이고, **둘 다 rank에 안 닿는다.**

★ **다만 「원리상 0」이 「검사가 있다」는 뜻은 아니다.** 지금은 세트 코드가 없어 회귀할 대상 자체가
없지만, 세트를 구현하는 라운드는 **`setId` 파생을 `Rarity`와 같은 함수에서 뽑아야 한다** —
두 벌로 적으면 등급과 세트가 갈라지고, 그 증상은 「전설 4종을 다 입었는데 세트가 안 뜬다」다.

### 4-4. ★ 세트 구현 라운드에 미리 넘기는 함정 2개

1. **`setId`를 `.asset` 필드로 만들 때 기본값이 `""`(빈 문자열)여야 한다.**
   `"base.rank0"` 같은 값을 기본값으로 두면, Unity가 키 없는 42종을 **전부 rank0 세트**로 싣는다 —
   `declaredRarity`가 `ItemRarity`가 아니라 `DeclaredRarity`인 것과 **정확히 같은 함정**이다
   (`ItemRarity.cs:36-65`의 실측: 42/42가 Common으로 실렸다).
2. **세트 보너스 +2는 `design-systems` 소관이고 되돌린 이력이 있다**
   (`ECONOMY_SPEC` §0-2-5 — +3으로 올리려다 표를 다시 읽고 되돌렸다). **구현자가 조정하지 마라.**

---

## 5. DLC 팩 매니페스트 — 늘어난 필드를 팩이 어떻게 채우는가

### 5-1. 팩이 채워야 하는 것 / 안 채워도 되는 것

| 늘어난 필드 | 팩이 안 채우면 | 안전한가 |
|---|---|---|
| `surfaces` | 0 = `Body\|Card`(`AccessorySurfaces.Effective`) — v1과 같은 뜻 | ✅ |
| `strokeMult` | 0 = ×1.0 (`AccessoryStroke.Multiplier`) | ✅ |
| `strokeInR` | 0 = **v1 규칙**(비례 획 + 옛 하한). `AccessoryStroke.IsHandoff(0) == false`라 **색·알파·하한 전부 v1 경로**로 떨어진다 | ✅ |
| `noStroke` / `bodyFixed` | false = 예전 동작 | ✅ |
| `alpha` | 0 = `AccessoryCardWash.GradientMeanAlpha`(0.21) | ⚠️ **주의** — §5-2 |
| `lineAlpha` | 0 → 1 | ✅ |
| `underBack` | 0 = 없음(바탕) | ✅ |
| `layer` | 0 = `AccessoryPieceLayer.Slot`(슬롯 기본 층) | ✅ |
| `wornGroupAlpha` / `wornScale` / `wornScaleY` / `wornOffsetYInR` / `wornMirrorX` | 전부 0/false = 변형 없음(`AccessoryWornTransform.IsSet == false`) | ✅ |

**전부 「0/false = v1과 같은 뜻」이라 원칙 4(기본 로직 무수정)가 유지된다.**
그리고 이것이 우연이 아니라 **설계 요구사항**이라는 것이 `AccessoryShapeContract.cs:23-25`,
`:38-39`, `:63`, `:141`에 각각 명시돼 있다.

### 5-2. ⚠️ `alpha`만 「0 = 안전」이 자명하지 않다

`alpha`의 0은 **1.0이 아니라 0.21**(그라디언트 평균)로 읽힌다.
즉 **팩이 채움 조각의 `alpha`를 안 적으면 그 조각이 21% 불투명으로 그려진다.**

- 기본 16종에서는 문제가 아니다 — 오늘 라운드가 전부 `alpha: 1`로 명시했다(실측: `equip_neck_striped.asset:79` 등).
- **팩 작성자에게는 함정이다.** 「안 적으면 v1처럼」이라는 다른 8칸의 규칙과 **이 칸만 다르다.**

⇒ **리더 판단 L-B**(§10). 세 가지 중 하나를 골라야 한다:
(가) 그대로 두고 팩 저작 문서에 명시 · (나) 0을 1.0으로 읽고 워시 3조각만 명시값을 적게 한다 ·
(다) `alpha`에도 「미설정」을 나르는 별도 표현을 준다.
**(나)가 가장 싸 보이지만 카드 워시 예외 3조각(L-4)의 뜻이 바뀌므로 `design-art` 확인이 필요하다.**

### 5-3. ★ 팩의 재질색 원천이 둘이다 — 그리고 하나는 소비자가 없다

| 원천 | 무엇 | 누가 읽는가 |
|---|---|---|
| 팩 **아이템** `.asset`의 `icon[].color`(tone 0/1) | 실제로 화면에 칠해지는 M/M2 | `ItemCatalog.EntryFrom` → `entry.PrimaryColor/SecondaryColor` → 카드·몸 **전부** |
| 팩 **매니페스트** `primaryColor` / `secondaryColor`(`StickPackManifestSO.cs:167,170`) | 「이 팩의 동결 팔레트」 선언 | `PackRegistry.cs:44-45`가 `PackInfo`에 담는다 → **그 뒤 프로덕션 소비자 0건** |

전수 확인(양성 대조 포함):
```
grep -rn "\.PrimaryColor|\.SecondaryColor" --include=*.cs   → 전부 ItemCatalogEntry 쪽. PackInfo 쪽 0건
grep -rn "primaryColor|secondaryColor"                      → 정의 2 + PackRegistry 2 + 테스트 4.  프로덕션 소비자 0
```

**이것이 이 라운드에서 내가 찾은 가장 큰 이음매 위험이다.**
지금은 팩이 0개라 아무 증상이 없다. 그러나 팩이 하나 붙는 순간 **같은 사실(팩의 색)이 두 곳에 앉고,
둘이 갈라져도 아무도 모른다** — 화면은 아이템 쪽을 따르고, 감사·문서·마케팅 소재는 매니페스트 쪽을 볼 것이다.
`.claude/agents/coder-systems.md`가 금지한 *"같은 사실이 두 곳에서 계산되면 그게 다음 버그다"* 그 형태다.

⇒ **리더 판단 L-A**(§10). 권고는 **(가) 조인 감사를 붙인다** —
`PackRegistry`가 팩을 실을 때 *"이 코호트 아이템들의 `PrimaryColor`가 매니페스트 `primaryColor`와 같은가"*를
검사해 다르면 **크게 신고**한다(조용히 넘기지 않는다 — `IsPlaceable`·`AcceptWornShapes`와 같은 관례).
매니페스트 쪽을 **지우는 것**(나)은 반대한다: 그 값은 `PackPaletteGateTests`(색상각·대역·ΔE 게이트)가
서는 자리이고, 지우면 **팔레트 감사가 물을 대상을 잃는다**.

### 5-4. 팩은 **구조적으로** 에셋 경로로 떨어진다 (좋은 소식)

`AccessoryShapeBuilder.Handoff.cs:20-33`의 `IsHandoffCode`는 슬롯별로 **고정 상수 0~3번**만 참이다
(`HeadCap`/`HeadBeanie`/`HeadFedora`/`HeadCrown` 등). 팩은 `itemIndexBase ≥ 6`이므로
(`StickPackManifestSO.cs:177` 툴팁) **언제나 false**가 되어 `ItemCatalog.WornShapes`/`WornTransform`을 탄다.

⇒ **팩을 하나 더 붙이는 데 `AccessoryShapeBuilder.Handoff.cs`를 고칠 필요가 없다.** 원칙 4 유지.
`Tests/EditMode/PackManifestCorridorTests.cs`가 합성 7번째 팩으로 이 통로를 직접 잰다.

### 5-5. `SchemaVersion = 2`의 필드 목록 — **20:2x 라운드 중 `coder`가 정정했다(해소)**

내가 §5를 쓴 시점(16:28)의 `StickPackManifestSO.cs:127-130`은 새 필드를
`strokeGrade / surfaces / alpha / underBack` 4개라 적고 있었고, 그중 **`strokeGrade`는 존재한 적이 없는 이름**이었다
(R16이 이산 등급 → 연속 배수로 바꾸면서 `strokeMult` + `strokeInR`가 됐다).

**16:35에 `coder`가 그 문단을 고쳤다.** 현재(`:127-134`)는 조각 9칸 + 아이템 5칸 = 14칸을 정확히 열거하고
*"이름 `strokeGrade`는 존재한 적이 없다"*까지 명시한다. ⇒ **L-C 해소. 리더 조치 불필요.**

---

## 6. 조각 수 증가가 세이브 크기·로딩에 주는 영향

### 6-1. 세이브 크기 — **0바이트**

§1-1대로 세이브에 조각이 없다. 조각이 100개가 되든 1,000개가 되든 세이브는 **아이디 7개** 그대로다.

### 6-2. `.asset` 크기 — 실측

| | HEAD 커밋 | 작업 트리 | 변화 |
|---|---:|---:|---:|
| `Resources/Items/*.asset` 총합 | 68,610 B | **123,543 B** | **+54,933 B (×1.80)** |
| 착용 조각(`wornShapes[]`) 총 개수 | 15 | **27** | +12 |
| 착용 스트림 float 총 개수 | — | **6,941** | 평균 257/조각 |

파일별(변화가 있는 것만):

| 파일 | HEAD | 작업 트리 | 조각 |
|---|---:|---:|---:|
| `equip_neck_bell.asset` | 4,952 | **31,433** | 2 → 8 |
| `equip_neck_bowtie.asset` | 3,271 | **17,024** | 3 → 4 |
| `equip_neck_scarf.asset` | 3,735 | **16,522** | 3 → 6 |
| `equip_neck_striped.asset` | 5,282 | **6,827** | 3 → 5 |
| `equip_neck_bandana` / `pendant` | 3,099 / 3,020 | 3,100 / 3,020 | 2 / 2 (v1 그대로) |

### 6-3. 남은 이행분 — 추정(추정임을 명시한다)

`r19_coords.txt` 전수 집계(직접 파싱):

| | 값 |
|---|---:|
| 인계본 16종 **몸** 조각 | **93** |
| 인계본 16종 **카드** 조각 | 91 |
| **뒤층**(`back`) 조각 | **15** |
| 조건부 조각(`cond` ≠ `-`) | **1** (외알안경 반대쪽 눈) |
| 그중 이미 `.asset`에 내려온 것(NECK 4종) | 23 |
| **아직 생성 `.cs`에 있는 것**(HEAD 4 · EYES 4 · BACK 4) | **70** |

NECK 4종 실측 단가 ≈ **3,122 B/조각**(71,806 B ÷ 23). 이 단가를 그대로 적용하면
70조각 ≈ **+219 KB** → `Resources/Items` 총합 **약 340 KB**.
★ **추정이다.** 단가는 조각의 점 개수에 크게 좌우되고(방울목걸이 3,929 B/조각 vs 줄무늬타이 1,365 B/조각),
망토·날개처럼 점이 많은 조각이 위로 튈 수 있다.

**340 KB는 이 앱의 실행 성능에 유의미한 크기가 아니다.** 비교 기준을 하나 둔다:
이 값은 `Resources` 폴더 하나의 크기이고, 24시간 상주 앱의 상시 비용은 파일 크기가 아니라
**프레임 경로**다(§6-4).

### 6-4. 로딩·런타임 경로 — 어디가 비싸지는가

| 경로 | 언제 도는가 | 조각 수에 비례하는가 |
|---|---|---|
| `ItemCatalog.EnsureLoaded()` → `Resources.LoadAll<AccessoryDefSO>` (`ItemCatalog.cs:440-448`) | **프로세스당 1회**(`_bySlot != null`이면 즉시 반환) | 예 — **1회뿐이라 무해** |
| `AcceptWornShapes` → `AccessoryWornShapeReader.Validate` (`:592`) | **로드 1회**. `Validate`는 스트림을 `stateOn` 양쪽으로 **2번 실행** | 예 — 27조각 × 2. 지금 규모에서 무시 가능 |
| `AccessoryShapeBuilder.AppendWorn` (`:1490`) → 조각마다 `new Vector3[count]` | **재구성 시에만** — `CharacterAccessoryRenderer.EnsureBuilt`(`:649`)가 서명으로 막는다 | 예 — **여기가 유일한 반복 비용** |
| `ComputeSignature()` (`:672`) | **매 프레임** | **아니오** — 착용 조합·잉크·배율만 본다 |

★ 즉 **매 프레임 비용은 조각 수와 무관하다.** 재구성이 도는 순간(착용 변경 / 방향 전환 / 잉크색 변경 /
크기 다이얼)만 조각 수에 비례해 `Vector3[]`가 할당된다.

**그런데 「방향 전환」이 서명에 들어 있다**(`:679` `_facingSign`). 캐릭터가 좌우를 바꿀 때마다
전 조각이 다시 구워진다. 지금 착용 최대 조합은 슬롯 7개 × 조각 최대 10개 ≈ **수십 개** 수준이라
문제가 아니지만, **인계본 16종이 전부 내려오고 팩이 붙으면** 이 자리가 첫 번째로 티가 난다.

⇒ **`perf-doc`에 넘길 관찰 1건**(지시 아님, 리더 경유):
*"방향 전환마다 액세서리 전 조각 재빌드 — 조각 수가 93+로 늘어난 뒤 왕복 걸음에서 GC 스파이크가 나는지"*.
지금은 **미측정**이고, 미측정을 「괜찮다」로 적지 않는다.

### 6-5. 정직하게 못 잰 것

- 실기 실행 0회(리더 승인 대기 규칙). 위 로딩 판정은 **소스 경로 추적**이지 프로파일러 실측이 아니다.
- Unity 임포트 후 메모리 상주량(직렬화 `float[]` → 힙)은 안 쟀다. `.asset` 바이트와 다를 수 있다.

---

## 7. 필드 표 — [필드 / 위치 / 기본값 / 하위 호환]

**위치 범례**: `S` = 세이브(`CharacterSaveStore.SaveData`) · `C` = 카탈로그(`AccessoryDefSO` / `ItemCatalog`) ·
`P` = 팩 매니페스트(`StickPackManifestSO`) · `X` = 생성 코드(`AccessoryShapeBuilder.Handoff.cs`)

### 7-1. 이번 라운드가 건드리는 필드

| 필드 | S | C | P | X | 기본값(미설정) | 하위 호환 |
|---|:-:|:-:|:-:|:-:|---|---|
| `icon[].color` (= 재질색 M/M2) | — | ✅ | — | — | `(0,0,0,0)` | 42종 전부 명시. **팩도 명시 필수** — 안 적으면 투명 |
| `icon[].tone` (0~6) | — | ✅ | — | — | `0` = 주색 M | v1 에셋의 0/1이 새 표에서 **같은 뜻**(`AccessoryTone.Resolve`) |
| `wornShapes[].surfaces` | — | ✅ | — | ✅ | `0` = `Body\|Card` | ✅ v1과 동일 |
| `wornShapes[].strokeMult` | — | ✅ | — | ✅ | `0` → ×1.0 | ✅ |
| `wornShapes[].strokeInR` | — | ✅ | — | ✅ | `0` = **v1 획 규칙** | ✅ **v2 진입 스위치** — 0이면 색·알파·하한 전부 v1 |
| `wornShapes[].noStroke` | — | ✅ | — | ✅ | `false` | ✅ |
| `wornShapes[].alpha` | — | ✅ | — | ✅ | `0` → **0.21** | ⚠️ **L-B** — 이 칸만 「0 ≠ v1」 |
| `wornShapes[].lineAlpha` | — | ✅ | — | ✅ | `0` → 1 | ✅ |
| `wornShapes[].underBack` | — | ✅ | — | ✅ | `0` = 없음 | ✅ |
| `wornShapes[].layer` | — | ✅ | — | ✅ | `0` = 슬롯 기본 층 | ✅ 뒤층/앞층은 명시한 조각만 |
| `wornShapes[].bodyFixed` | — | ✅ | — | ✅ | `false` | ✅ |
| `wornGroupAlpha` | — | ✅ | — | ✅ | `0` → 1.0 | ✅ **날개·배낭 0.40 → 0(=1.0) 착지 확인**(L-3) |
| `wornScale` / `wornScaleY` / `wornOffsetYInR` / `wornMirrorX` | — | ✅ | — | ✅ | `0`/`false` | ✅ `AccessoryWornTransform.IsSet == false` |
| `StickPackManifestSO.SchemaVersion` | — | — | ✅ | — | 앱 = **2**, 팩 `requiresSchemaVersion` 기본 **1** | ✅ v1 팩은 v2 앱에서 그대로 옳다(`WornShapeContractCompatTests`) |

### 7-2. 건드리지 않는데 **자주 오해되는** 필드 (오해 방지용)

| 필드 | S | C | P | 기본값 | 왜 여기 적는가 |
|---|:-:|:-:|:-:|---|---|
| `itemId` | ✅ | ✅ | — | — | **세이브 키다.** 바꾸면 사용자 차림이 사라진다(`AccessoryDefSO.cs:443-446`) |
| `cohortId` | — | ✅ | ✅ | `0` = `BaseCohortId` | 팩이 0을 쓰면 **기본 42종 등급이 통째로 미끄러진다**. 증상이 **안 산 사람에게** 난다 |
| `declaredRarity` | — | ✅ | — | `Derived`(**반드시 0**) | 타입을 `ItemRarity`로 바꾸면 42/42가 「일반 선언」이 되고 **28/42 등급이 내려앉는다** |
| `requiredLevel` | — | ✅ | — | `1` | 등급 파생의 입력. 팩은 전부 1(`PackRequiredLevel`) |
| `primaryColor` / `secondaryColor` (팩) | — | — | ✅ | `Color.white` | ⚠️ **소비자 0건 — L-A** |
| `setId` | — | ❌ | ❌ | — | **아직 존재하지 않는다.** 만들 때 기본값은 반드시 `""`(§4-4) |
| `inkColorName` | ✅ | — | — | `null` | 세이브에 있는 **유일한** 색 관련 값이고, **아이템 색이 아니라 사용자 잉크**다 |

---

## 8. 리더 추가 지시 ①② — 「우클릭 폐지」 문장의 행선지

> **면책 한 줄**: 리더가 언급한 *"네 문서의 「부채꼴이 톱니에 의존하는 지점」 절"*은
> **이 문서에 존재하지 않는다**(내 담당은 `Core/` 데이터·규칙이다). 그 절이 다른 라운드 문서를
> 가리키는 것이라면 이 표를 그쪽으로 옮겨 붙이면 된다. 여기서는 **독립 절**로 둔다.

**전제(dev-platform 실측, 내가 재현하지 않음)**: 캐릭터 위 우클릭은 **지금도 우리 창이 삼키고
아무 반응도 없다.** ⇒ 「우클릭은 밑의 앱으로 관통한다」는 **거짓**이고, 「폐지의 근거가 비침해 개선」이라는
**논거 자체가 틀렸었다.** 반면 「우클릭 **메뉴 UI**가 폐지됐다」는 **참**이다 — 두 문장을 섞지 말 것.

**개수 세는 법을 먼저 밝힌다**(리더가 「6곳」이라 했다): 내 grep 기준
**`AppControlDirector.cs` 외 파일 6개 / 줄로는 12곳**이다. 파일 기준으로 6과 일치한다.

프로브(양성 대조 포함):
```
grep -rn "우클릭" Scripts --include=*.cs | grep -v /Tests/ | grep -E "폐지|삭제|없어|없앤|제거|존재하지 않"
양성 대조: grep -rn "톱니" 같은 범위 → 285건 (프로브 살아 있음)
```

| # | 파일:줄 | 정정 필요 문장(요지) | 등급 | 정정 방향 |
|---|---|---|---|---|
| 1 | `Interaction/AppControlDirector.cs:174-175` | 기동 로그: *"…폐지됐습니다 — **우클릭은 이제 밑에 있는 앱으로 그대로 관통합니다**(비침해 개선)"* | **거짓 — 사용자에게 출력된다** | 뒷문장 삭제. 「메뉴는 없다. 우클릭은 **현재 아무 동작도 하지 않는다**(관통은 미구현)」로. ★ **가장 급하다 — 유일하게 화면·로그에 나가는 문장이다** |
| 2 | `Interaction/AppControlDirector.cs:40-42` | *"비침해가 실제로 개선된다 … 지금까지 우리는 사용자의 우클릭을 가로채고 있었다. **제공할 메뉴가 사라지면 그 비용만 남는다.** 원칙 2에 비추어 제거가 정답이다"* | **거짓 — 폐지 근거 자체** | 「메뉴 폐지는 **사용자 지시 이행**이다. 비침해 개선은 **일어나지 않았다** — 창이 여전히 삼킨다」로. **지시 이행과 비침해 논거를 분리** |
| 3 | `Interaction/AppControlDirector.cs:33` | *"3안(캐릭터 우클릭 메뉴) — 2026-08-31 폐지"* | 참(메뉴), 오도(맥락) | 유지 + **「폐지 ≠ 관통」 한 줄** 병기 |
| 4 | `Interaction/AppControlDirector.cs:36` | *"UI와 **폴링까지** 제거했다"* | 참 | 유지. 다만 *"폴링을 지웠으니 비용이 사라졌다"*로 읽히지 않게 — **삼키는 것은 폴링이 아니라 창 자체**임을 명시 |
| 5 | `Interaction/AppControlDirector.cs:59` | *"우클릭까지 없앤 뒤 그 환경에 남는 종료 수단이 0이 되면…"* | 참 | 유지 |
| 6 | `Interaction/AppControlDirector.cs:118` | *"우클릭 메뉴가 폐지되면서 「메뉴 행 번호」라는 의미는 사라졌고"* | 참 | 유지 |
| 7 | `Interaction/CharacterInfoWindow.cs:749-750` | *"이 안내는 **없어진 문**을 광고하고 있었다 … `AppControlDirector.LogStartupBanner()`가 **같은 부팅 로그에서** 「우클릭 메뉴는 …」"* | **연쇄 오염** | #1을 고치면 이 인용이 **다시 낡는다**. 인용문을 새 문장으로 갱신하거나 **인용 자체를 참조로 바꾼다** |
| 8 | `Interaction/CharacterInfoWindow.cs:1378` | *"`docs/UX_FLOW.md` 36-11이 우클릭 메뉴 폐지에…"* | 참(참조) | 36-11 본문이 관통을 주장하면 **문서 쪽도 함께**(문서는 내 소관 아님 — 리더 배분) |
| 9 | `Interaction/CharacterInfoWindow.Input.cs:423` | *"`AppControlDirector.HitTestMenuRow`는 **존재하지 않는다.** 우클릭 제어 메뉴 UI가 통째로 삭제되면서 같이 없어졌다"* | 참 | 유지. **이 문장은 옳게 쓰인 본보기다**(죽은 이름을 찾아가는 사람을 막는다) |
| 10 | `Interaction/StressGaugeRenderer.cs:258` | *"그 **우클릭 메뉴는 삭제됐다**"* | 참 | 유지 |
| 11 | `Interaction/GearRadialMenuWidget.cs:374` | *"(2026-08-31: 32760에 있던 우클릭 제어 메뉴는 폐지됐다 — 36-9.)"* | 참 | 유지 |
| 12 | `Platform/IGlobalPointerButtonService.cs:45` | *"**오른쪽 버튼은 이 앱에서 아무도 쓰지 않으므로**, 「캐릭터를 우클릭하면 제어 메뉴가 뜬다」는 관습적 조작을 충돌 없이 얹을 수 있다"* | **거짓 — 사용자 관점** | 폴링은 지웠지만 **창이 삼키므로 사용자에게는 「쓰이고 있다」**. 「우버튼은 **지금 아무 기능에도 배선돼 있지 않다**. 단, 캐릭터 위 우클릭은 **창이 삼켜** 밑 앱에 도달하지 않는다(미해결)」로 |
| 13 | `Platform/ScreenCoordinateConverter.cs:611` | *"(`AppControlDirector.cs`의 **우클릭 메뉴** — 클릭관통 오버레이에서는…)"* | **죽은 참조** | 그 메뉴는 없다. 살아 있는 예(톱니 부채꼴/정보창)로 교체 |

★ **연쇄 주의**: #1을 고치면 #7이 인용째로 낡는다. **두 곳을 같은 라운드에서 함께 고쳐야 한다** —
이 저장소의 *"서술이 여러 파일에 복사돼 전부 낡았다"* 형태를 그대로 밟는 자리다.

★ **테스트 영향 확인**: 위 문장을 **니들로 하드코딩한 테스트**가 있으면 문장을 고치는 순간 빨개지거나
(존재 단언) 조용히 초록이 된다(부재 단언). `Tests/`는 이 라운드 범위 밖이라 **안 세었다 — 미확인**.
고치는 라운드가 `grep -rn "폐지\|관통" Assets/_Project/Scripts/Tests/`를 **먼저** 돌릴 것.

---

## 9. 리더 추가 지시 ③ — 신규 정책 클래스의 후보 경로

**생성은 다음 라운드. 여기서는 자리만 제안한다.**

### 9-1. 결론

```
Assets/_Project/Scripts/Platform/IPointerSwallowStateSource.cs
Assets/_Project/Scripts/Platform/RightClickFanGatePolicy.cs
```

플랫폼 전용 「사실 조회」가 생기면:
```
Assets/_Project/Scripts/Platform/Windows/WindowsPointerSwallowProbe.cs
Assets/_Project/Scripts/Platform/MacOS/MacPointerSwallowProbe.cs
```

### 9-2. 근거 — 취향이 아니라 실측된 관례다

| 프로브 | 결과 |
|---|---|
| `find Scripts -name "*Policy.cs" -not -path "*/Tests/*"` | **20건, 전부 `Platform/` 바로 아래. 예외 0건** |
| `Platform/` 루트의 `I*Source` / `I*Service` | 12건(`IForeignFullscreenTierSource` · `IVirtualDesktopMembershipSource` · `IRawWindowRectSource` …) |

그리고 CLAUDE.md가 이유를 명시한다:
> 정책 판정 로직은 **플랫폼 중립 위치(`Platform/`)**에 두고, 플랫폼 전용 코드는 **「사실 조회」만** 담당한다.
> 정책이 `Platform/MacOS/` 안에 있으면 Windows가 **물리적으로 호출할 수 없다**(실제 사고: `FullscreenSuspendPolicy.cs`).

**바로 어제 같은 형태가 한 벌 착지했고, 그것을 그대로 베끼면 된다**(작업 트리, 미커밋):
```
Platform/IVirtualDesktopMembershipSource.cs   ← 인터페이스 + 「모른다」를 값으로 갖는 enum
Platform/VirtualDesktopSuspendPolicy.cs       ← 순수 판정. UnityEngine·P/Invoke 0줄
Platform/Windows/WindowsVirtualDesktopProbe.cs ← 사실 조회만
```
그 파일이 자기 자리를 이렇게 변호한다(`VirtualDesktopSuspendPolicy.cs`):
> 이 규칙이 `Platform/Windows/` 안에 있으면 **이 개발 머신에서 한 줄도 실행되지 않는다** —
> 즉 **검증이 구조적으로 불가능한 자리**다.

### 9-3. 「부채꼴 정책인데 왜 `Interaction/`이 아닌가」에 대한 답

부채꼴 위젯(`Interaction/GearRadialMenuWidget.cs`)은 **표면**이다. 그런데
`RightClickFanGatePolicy`가 판정할 입력은 **「지금 우리 창이 포인터를 삼키고 있는가」**이고,
그건 오버레이 창의 클릭관통 상태 — **`Platform/`의 사실**이다
(`IPlatformWindowService.SetClickThrough`, 소유자는 `Core/StickmanAgent.ApplyClickThrough:831`).

정책을 `Interaction/`에 두면 **UI 어셈블리가 창 상태를 직접 캐게 되고**, 그 순간
「부채꼴이 열릴 조건」이 위젯 안에 숨어 **테스트가 위젯을 띄워야만 잴 수 있게 된다.**
`Platform/`에 두면 `UnityEngine` 없이 EditMode에서 순수 함수로 잰다 — 위 세 형제가 전부 그 형태다.

### 9-4. 인터페이스 설계에 미리 못박을 것 하나

`IPointerSwallowStateSource`의 반환은 **`bool`이 아니라 3값**이어야 한다.
`IVirtualDesktopMembershipSource`가 세운 규약 그대로:
> 「모른다」를 **값으로** 갖는 것이 이 열거형의 존재 이유다. `bool` 하나로 만들면
> 「삼키고 있다」와 「조회가 실패했다」가 같은 `false`로 뭉개진다.

같은 규약이 이 저장소에 이미 셋 있다 — `IWindowEnumerationCostSource`(미지원을 0이 아니라 음수),
`ReservedEdgeInsets.MeasuredEdges`(측정된 0 ≠ 못 잰 0), `VirtualDesktopMembership.Unknown`.
**보수 방향도 정해 두라**: 「모른다」면 부채꼴을 **여는 쪽인가 안 여는 쪽인가**.
오판의 대가가 비대칭이므로(안 열리면 사용자가 못 찾고, 잘못 열리면 남의 창 위에 우리 UI가 뜬다)
**리더가 정할 값**이다 — §10 L-D.

---

## 10. 리더 판단이 필요한 것

| # | 항목 | 내 권고 | 왜 리더인가 |
|---|---|---|---|
| **L-A** | **팩 재질색 원천이 둘** — `StickPackManifestSO.primaryColor/secondaryColor`는 프로덕션 소비자 0건이고, 실제 색은 팩 아이템 `.asset`에서 온다(§5-3) | **조인 감사를 붙인다**(`PackRegistry`가 팩 로드 시 불일치를 크게 신고). 매니페스트 쪽을 **지우지는 마라** — `PackPaletteGateTests`가 서는 자리다 | 팩을 출하한 **뒤에** 고치면 완료된 결제 아래에서 상품 정의가 바뀐다. **지금은 팩 0개라 위험 0이다** |
| **L-B** | **`alpha`만 「0 ≠ v1」** — 미설정이 1.0이 아니라 0.21로 읽힌다. 팩 작성자에게 유일한 함정(§5-2) | 셋 중 택일. (나)「0을 1.0으로」가 가장 싸 보이나 **카드 워시 예외 3조각(L-4)의 뜻이 바뀐다** | `design-art`의 L-4 판정과 맞물린다. 나 혼자 못 정한다 |
| ~~**L-C**~~ | ~~`StickPackManifestSO.cs`의 신규 필드 목록이 실물과 다르다~~ → **해소됨**. 16:35에 `coder`가 정정했다(§5-5·§13) | — | **조치 불필요** |
| **L-D** | `IPointerSwallowStateSource`의 **「모른다」일 때 부채꼴을 여는가**(§9-4) | 미정 — 비대칭의 방향을 안 쟀다 | 사용자에게 보이는 동작 결정 |
| **L-E** | 「우클릭 폐지」 문장 **#1과 #7을 같은 라운드에서 함께** 고칠 것(§8). #1만 고치면 #7의 인용이 낡는다 | 함께 배정 | 파일 배분은 리더 소유 |
| **L-F** | `Core/AccessoryShapeContract.cs:17`이 **같은 파일 `:76`과 모순**(등급색 = 강조 A) — 랙돌 절과 같은 형태의 초기 단계(§3-3) | `:17`을 R16 이력으로 표시하고 정본을 `:76` 하나로 | 정본 위치 결정 |
| **L-G** | **v11 예약** — 다음에 세이브 필드가 필요해지는 라운드가 오면 세트·유예·스탯을 **한 번에** 받게 목록을 먼저 확정(§2-4) | 목록을 리더가 준다 | `.claude/agents/coder-systems.md` 명시 의무 |

### 미확인 (추측으로 메우지 않는다)

- **실기 실행 0회 · Unity 배치모드 0회.** 로딩·재구성 판정은 **소스 경로 추적**이지 프로파일러 실측이 아니다.
- **새 테스트가 지금 빨간불을 보는지 못 쟀다** — `WornShapeContractCompatTests`·`HandoffRarityAbsenceTests`·
  `CardShapeContractTests`는 전부 **미커밋 신규**이고, 배치모드를 안 돌렸다(§2-3).
- **`Tests/` 안에 「우클릭 폐지」 문장을 니들로 쓴 곳이 있는지 안 세었다**(§8 말미).
- `.asset` 340 KB 추정은 **NECK 단가 외삽**이다. 점이 많은 망토·날개가 위로 튈 수 있다.
- 우클릭 삼킴은 **dev-platform 실측을 인용**한 것이고 내가 재현하지 않았다.

---

## 11. 플랫폼 영향

- **Windows 영향: 없음.** 이 라운드는 문서 1건뿐이고 `.cs`·`.asset` **0줄**이다. 장비 데이터 경로
  (`Core/AccessoryDefSO` · `ItemCatalog` · `CharacterSaveStore` · `StickPackManifestSO`)에는
  `#if UNITY_STANDALONE_*` 분기가 **하나도 없다** — 플랫폼 중립이라 갈릴 축 자체가 없다.
  §9가 제안한 신규 클래스도 **`Platform/` 중립 위치**라 같은 성질을 유지한다
  (Windows 전용 프로브가 생기는 날 `xcheck.sh win`이 필요하다 — **그 라운드의 의무**).
- **macOS 영향: 함께 검토함 — 없음.** 빌드·앱 실행·Unity 락 사용 0회. `Library/` 미접촉.
- ★ 다만 **§6-4의 재구성 비용은 두 플랫폼에서 같은 코드**이므로, 조각이 93+로 늘어난 뒤의 실측은
  **한쪽만 재면 안 된다**(프레임 예산이 다르다).

## 12. 재현

```bash
cd /Users/kjmoon/App/StickMate

# §1 경계 — 세이브에 색이 없다 (부재 단언 + 양성 대조)
grep -n "Color\|color" Assets/_Project/Scripts/Core/CharacterSaveStore.cs
grep -c "public string" Assets/_Project/Scripts/Core/CharacterSaveStore.cs      # 14 = 프로브 생존

# §1-3 material 필드 부재 (양성 대조 필수 — 0건만으로는 판정 불가)
grep -rn "public Color material\|material2\|materialColor" Assets/_Project/Scripts --include="*.cs"
grep -rl "material" Assets/_Project/Resources/Items/ ; grep -rl "color:" Assets/_Project/Resources/Items/ | wc -l

# §4 setId 부재 + 양성 대조
grep -rn "setId\|SetId" Assets/_Project/Scripts --include="*.cs"
grep -rn "CohortId" Assets/_Project/Scripts --include="*.cs" | wc -l                # 78 = 프로브 생존

# §6 크기 실측 (HEAD 대조본을 tar로 꺼내 비교)
git archive HEAD Assets/_Project/Resources/Items | tar -x -C <임시경로>
find Assets/_Project/Resources/Items -name "*.asset" -exec stat -f%z {} \; | awk '{s+=$1} END{print s}'

# §6-3 r19 조각 집계
python3 - <<'PY'
import re
L=open('design/equipment/verify/r19_coords.txt').read().split('\n')
b=c=0; mode=None
for i,l in enumerate(L):
    if l.startswith('-- BODY'): mode='b'
    elif l.startswith('-- CARD'): mode='c'
    elif re.match(r'^  \S+\s+(front|back|card)\s', l):
        b += mode=='b'; c += mode=='c'
print('body',b,'card',c)
PY

# §8 우클릭 문장 + 양성 대조
grep -rn "우클릭" Assets/_Project/Scripts --include="*.cs" | grep -v /Tests/ | grep -E "폐지|삭제|없어|없앤|제거|존재하지 않"
grep -rn "톱니" Assets/_Project/Scripts --include="*.cs" | grep -v /Tests/ | wc -l   # 285 = 프로브 생존

# §9 정책 파일 관례 (예외 0건 확인)
find Assets/_Project/Scripts -name "*Policy.cs" -not -path "*/Tests/*"
```

---

## 13. ★ 라운드 중 파일이 움직였다 — 재확인 결과 (2026-09-05 20:2x, API 한도 리셋 후)

리더 지시로 §1·§2·§3·§5를 **다른 방법으로 다시 쟀다**(같은 명령 재실행은 검증이 아니다 — TEAM.md §3).
그 과정에서 **두 가지가 나왔다. 둘 다 이 절에 남긴다.**

### 13-1. `coder`가 16:35에 계약 v2 필드 **3개를 제거**했다 — 내 §1·§5·§7이 그만큼 낡았었다

내 문서 mtime은 **16:28:22**이고, 아래 파일들의 mtime은 **16:35:42 / 16:36:27**이다.

| 제거된 필드 | 제거 사유(코드에 적힌 것) |
|---|---|
| `wornShapes[].bodyAlpha` | `AccessoryDefSO.cs:187` — *"139조각 실사용 0(game-architect I-22/I-23)"* |
| `wornShapes[].fixedFill` | 〃 |
| `wornShapes[].fixedLine` | 〃 |

⇒ **계약 v2 조각 필드는 12칸이 아니라 9칸**이다(v1 8칸 + v2 9칸 = 17칸).
`.asset` 실물도 이미 그 키가 없다(`equip_neck_striped.asset` `bodyAlpha` 히트 **0건**).
**§1-4 · §5-1 · §7-1 표와 §3-2·§1-3의 줄 번호를 실물에 맞춰 고쳤다.**

파생 결과 3건:

1. **L-C는 해소됐다**(§5-5) — 같은 편집에서 `coder`가 매니페스트 문단까지 정정했다.
2. **`AccessoryTone.Fixed`(4)는 이제 「팩 작성자 전용이지만 값을 실을 칸이 없는」 역할이다.**
   `HandoffFillBase`가 `case AccessoryTone.Fixed: return s.FixedFill.a > 0f ? … : p.Material` 형태였는데
   `FixedFill` 칸이 사라졌다. **팩이 고정색을 쓰려면 칸을 되살려야 한다** — 지금 쓰는 팩이 0개라 무해하지만,
   `PACK_THEME_SPEC`이 고정색을 요구하면 그때 v3가 된다. **리더 판단 L-H**(아래).
3. **`HandoffFillAlpha(in Shape s, bool body)`의 `body` 인자가 지금은 쓰이지 않는다.**
   `:1287`이 *"호출부의 뜻을 남기려고 둔다"*고 자백해 뒀다 — **의도된 잔존이고 죽은 코드가 아니다.**
   다만 §5-2의 `alpha` 함정(0 → 0.21)은 **그대로 유효**하고, 덮어쓸 수단(`bodyAlpha`)이 사라져서
   **오히려 더 중요해졌다**(L-B 유지).

★ **바뀌지 않은 판정**: §2(v10 유지) · §3(죽은 코드 0건) · §4(세트 0줄) · §5-3(L-A) · §6(세이브 0바이트).
아래 §13-3이 그 넷을 **다른 자로** 다시 잰 결과다.

### 13-2. ★ 자기 신고 — 내 재측정 프로브 하나가 **거짓 통과**를 냈다

`SaveData`에 `Color` 필드가 없음을 확인하려고 쓴 첫 파서가 이랬다:

```python
body = re.sub(r'///.*|//.*|/\*.*?\*/', '', body, flags=re.S)   # ← flags=re.S 가 범인
```

`re.S`(DOTALL)가 **`///.*` 에도 걸려** 주석 한 줄이 아니라 **클래스 본문 전체**를 먹었다.
결과: 필드가 58개가 아니라 **5개**로 보였고, 출력은

```
★ Color 타입 필드: 0개        ← 참이 아니라 "아무것도 안 센 결과"
```

**이 저장소의 그 형태 그대로다 — 죽은 프로브의 출력이 성공한 프로브와 똑같이 생겼다.**
(같은 가족: .NET `\b`가 한글을 낱말로 셈 / zsh 글롭이 `--include`를 죽임 / 빈 문자열 인자가 형태 검사 통과.)

**무엇이 이걸 구했는가**: 바로 다음에 돌린 **다른 프로브가 58을 냈고 두 숫자가 안 맞았다.**
숫자를 하나만 봤으면 「0개 = 깨끗」으로 읽고 넘어갔다.
⇒ **처방(내 것)**: 부재 단언 파서에는 **모집단 크기를 함께 찍고, 그 크기가 기대와 맞는지 먼저 본다.**
`HandoffRarityAbsenceTests:63`이 `Assert.Greater(handoffPieces, 100, "…부재 단언이 공허합니다")`로
이미 그렇게 하고 있다 — 내가 그 본을 안 따랐다.

### 13-3. 네 항목 재측정 — **다른 자로 쟀고, 판정은 그대로다**

| 항목 | 1차 측정 방법 | **2차(다른) 방법** | 결과 |
|---|---|---|---|
| ① 재질색 = 카탈로그 전용 | `grep "color"` 문자열 | `SaveData` **필드 전수 구조 파싱** → 타입별 집계 | **58칸 중 `Color` 타입 0개.** 이름에 color 포함 = `inkColorSaved`(bool)·`inkColorName`(string) 둘뿐. 장비 필드 13개 전부 **아이디 문자열/불리언**. ⇒ **일치** |
| ② 스키마 버전 | 상수 `CurrentVersion` 읽기 | **HEAD ↔ 작업트리 필드 집합 diff** | HEAD 58칸 → 작업트리 58칸, **추가 0 · 삭제 0.** `CurrentVersion` 양쪽 다 10. ⇒ **일치(상승 불필요)** |
| ③ L-1 죽은 코드 | `Accent` 문자열 grep | **타입 흐름** — `ItemRarity`가 렌더 경로 시그니처에 들어가는가 | `HandoffPalette` 생성자 인자 **6개 전부 `Color`/`float`, 등급 타입 0개.** 호출부 2곳(`AccessoryHandoffPalette.cs:36,43`)뿐. 렌더 5파일(`AccessoryShapeBuilder`·`HandoffPalette`·`CardIcon`·`AccessoryRenderer`·`PortraitStage`)의 `ItemRarity` 히트 = **주석 1건**(`:1228`, L-1 설명), **코드 0건**. 양성 대조 `Cards.cs` = 9건. ⇒ **일치(죽은 코드 0건)** |
| ④ 팩 공란 안전성 | XML 주석의 「0 = …」 서술 | **접근자 함수의 실제 식** (주석은 거짓말해도 식은 못 한다) | `Effective(0) → Body\|Card` · `Multiplier(0) → 1f` · `IsHandoff(0) → false` · `GroupAlpha = groupAlpha > 0f ? … : 1f` · `HandoffLineAlpha(0) → 1f` · `IsSet`(0/false → false). **8칸 전부 「0 = v1」 확인.** ⇒ **일치.** ★ 예외는 여전히 `HandoffFillAlpha`: `s.Alpha > 0f ? s.Alpha : GradientMeanAlpha(0.21)` — **`alpha`만 0 ≠ 1.0**(L-B) |
| §5-3 L-A | `PackInfo` 소비자 grep | `coder` 편집 **후** 재확인 | `PackRegistry.cs:22,23,44,45`가 담기만 하고 프로덕션 소비자 **여전히 0건**. ⇒ **L-A 유효** |

### 13-4. 이 절이 남기는 운영 교훈 (리더용)

**내가 `Core/` 읽기만 하는 조사 라운드였는데도 문서가 7분 만에 낡았다.**
`coder`가 같은 시각 `AccessoryDefSO.cs`·`AccessoryShapeContract.cs`·`StickPackManifestSO.cs`를 만지고 있었고,
그 셋은 **내 분석 대상 그 자체**다(CLAUDE.md 동시 진행 규칙의 「파일을 갈라 준다」가 **읽기 라운드에는 안 걸려 있었다**).

⇒ **권고**: 분석 라운드에도 **스냅샷 기준점을 문서에 적게 한다.**
이 문서는 이제 §13에 mtime을 박아 뒀지만, 그건 사후 대응이다.
다음부터는 조사 라운드 착수 시 `git rev-parse HEAD` + 대상 파일 mtime을 **문서 머리에** 적는 것이 싸다.

### 13-5. 추가 리더 판단

| # | 항목 | 내 권고 | 왜 리더인가 |
|---|---|---|---|
| **L-H** | `AccessoryTone.Fixed`(역할 4)는 남았는데 **값을 실을 칸(`fixedFill`/`fixedLine`)이 사라졌다.** 기본 16종 실사용 0이라 지금은 무해 | **지금은 그대로 둔다.** 되살리는 것은 팩이 고정색을 실제로 요구할 때, 그리고 그때는 **팩 스키마 v3**다 | `PACK_THEME_SPEC`의 팩 12색이 「고정색」을 요구하는지는 `design-art` 소관이고, 요구하면 **다음 팩 라운드가 v3를 짊어진다** — 순서 결정이다 |
