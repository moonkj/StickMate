# 코스튬 DLC × 집중 모드 — 구현 계약서 (PART2)

작성 `game-architect` / 2026-09-07 / **프로덕션 `.cs` 0줄 변경**

> ## 이 문서의 지위
> **이것은 「무엇을 결정했다」가 아니라 「어느 파일의 어느 함수에 무엇을 넣어라」다.**
> `coder-systems` · `coder` · `coder-ui`가 이 문서만 읽고 착수할 수 있어야 한다.
> **수치(임계값·가격·획득량)는 이 문서의 소관이 아니다** — 전부 `design-systems` 산출물
> (`docs/DESIGN_SYSTEMS_COSTUME_EVOLUTION.md`, 같은 라운드 병렬 작성)을 참조하는 **구멍**으로 남겼다.
> 구멍에는 `[DS-구멍 N]` 표를 달았다. 그 표가 안 채워지면 그 항목은 착수 불가다.
>
> ### 표기
> | 표기 | 뜻 |
> |---|---|
> | **[실측]** | 이 라운드에 코드/에셋을 직접 읽고 잰 것. 파일:행 동반 |
> | **[계산]** | 실측값에서 유도. 식과 검산 동반 |
> | **[추정]** | 재지 못했다. 무엇을 재야 하는지 함께 적었다 |
> | **[미확인]** | 모른다. 메우지 않았다 |

---

## 0. 실측 원장 — 이 문서의 모든 판정이 서 있는 바닥

이 라운드에 직접 읽은 값만 적는다. **문서가 아니라 코드를 읽었다.**

| # | 사실 | 값 | 출처(실측) |
|---|---|---|---|
| M-1 | 팩 아이템은 **테마를 가질 수 없다** | 구조적 | `Core/ItemCatalog.cs:1426` — `if (cohortId != BaseCohortId ...) return ThemeUnassigned;` |
| M-2 | 테마 표의 모집단 | **기본 코호트 24종뿐** | `Core/ItemCatalog.cs` `ThemeTable.Map` (24행 직접 셈) |
| M-3 | 실재 테마 | **6개** `ink/sport/office/cyber/mil/neon` | `Core/ItemCatalog.cs:1317` `AllThemes()` |
| M-4 | `IsSetComplete`는 **문자열 동등성만** 본다 | — | `Core/EquipmentStatRules.cs:343` |
| M-5 | 실린 팩 수 | **0개** | `Resources/Items/*.asset` 42개 전부 기본 42종. `StickPackManifestSO` 에셋 **0건** |
| M-6 | 엔타이틀먼트 기본 출처 | **전부 `Unknown`** | `Core/PackEntitlement.cs` `NullPackEntitlementSource.Query` |
| M-7 | `PackEntitlements.StateOf` **프로덕션 소비자 수** | ★ **0건** | 전 트리 grep. `Store/SteamPackEntitlementSource.cs`는 *생산자*다 |
| M-8 | 무료의 표현 | *"무료는 채널 항목 자체가 없는 것"* | `Core/StickPackManifestSO.cs` `PackEntitlementRef.entitlementId` 문서 |
| M-9 | `Unknown`의 계약 | 유지는 하되 **새로 시작은 거부** | `docs/security/ENTITLEMENT_CONTRACT.md` E-2 표 |
| M-10 | 세이브 현재 판 | **`CurrentVersion = 11`** | `Core/CharacterSaveStore.cs:158` |
| M-11 | 가변 길이 키-값 저장 선례 | `ItemGraceBaseline[]` | `Core/CurrencyModel.cs:16` + `CharacterSaveStore.cs:433` |
| M-12 | 절감 등급 판정 입력 | **`CurrentStateId == Idle` 하나뿐** | `Platform/FramePacing.cs:499` `ResolveCharacterIdle` |
| M-13 | 제출률 | Active/Calm **30/초**, Still/Away **15/초** | `Platform/ViewerPresence.cs` `BuildPlan` + `DefaultActiveDivisor=2`(:582) `DefaultStillDivisor=4`(:512) |
| M-14 | 집중 관망 자세는 **이미 Idle 안에** 산다 | — | `States/StickmanBlackboard.cs:2306` 분기 + `:2481` `TickFocusWatchStance`가 `id != Idle`이면 false |
| M-15 | 팔다리 다시 굽기 문턱 | **0.05°** | `States/LimbCurveRenderer.cs:174` `RebuildEpsilonDegrees` |
| M-16 | 포즈 감쇠 계수 | **35/초** | `Core/StickConfig.cs:80` `poseSmoothingRate` |
| M-17 | 상체 기울임은 **같은 값이면 무비용** | 조기 반환 | `States/StickmanPoseAnimator.cs:3643` `if (clamped == _bodyLeanDegrees) return;` |
| M-18 | 「그린 뒤 안 건드리는」 프롭 선례 | `Mode.Holding`에서 **쓰기 0** | `Interaction/GraffitiRenderer.cs:249` |
| M-19 | 세션 시간의 단일 창구 | `SessionDurationSeconds` / `RemainingSeconds` | `Interaction/FocusWatchDirector.cs:55,98` |
| M-20 | 세션 최소 길이 | **60초** | `Interaction/FocusWatchDirector.cs:115` `MinimumSessionSeconds` |
| M-21 | 세션 종료 경로 | **정확히 3개** | `FocusWatchDirector` `CompleteSession` / `StopFocusSession` / `OnEmergencyStop` |
| M-22 | 배회 확률 사다리 | 부채꼴 0 > 자리비움 0.15 > 집중 0.40 > 평소 0.75 | `States/AutoWanderController.cs:377-380` |
| M-23 | ★ 장비 조형의 에셋화 진도 | **24종 중 6종(NECK만)** | `Resources/Items/*.asset` 중 `wornShapes` 보유 = 6개(전부 `equip_neck_*`). `AccessoryShapeBuilder.cs:1431`이 NECK만 `AppendWorn` |
| M-24 | `MotionPluginSO` 소비자 수 | ★ **0건** | 전 트리 grep. 필드만 있는 Phase 0 골조 |
| M-25 | 지면 Y 스냅샷 선례 | `ArcheryGroundWorldY` | `States/StickmanBlackboard.cs:213` |
| M-26 | 팩 1개의 구성 | **6슬롯 × 1종**(HEAD/EYES/NECK/BACK/FX/PET) | `design/art/PACK_THEME_SPEC.md §2-1` |

---

## 1. 소유권 게이트 — **최종 규칙 1개**

### 1-1. 나는 내 지난 제안을 **실측으로 철회한다**

지난 라운드에 내가 낸 것: *"코스튬 = 기존 테마 세트 완성(`IsSetComplete`)의 두 번째 보상"*.
`product-strategy` P-11이 그것을 정면으로 반박했다. **P-11이 맞다. 근거는 M-1 · M-2다.**

```
ItemCatalog.cs:1426   if (cohortId != BaseCohortId || string.IsNullOrEmpty(itemId)) return ThemeUnassigned;
```

⇒ **팩 아이템은 테마 문자열을 절대 못 받는다.** 그러므로 `IsSetComplete`가 true가 되는 유일한 길은
**기본 코호트 24종**이고, 그 24종은 전부 무료·동전이다(M-2 · M-3).
**`cyber` 세트 완성 = 왕관 + 외알안경 + 펜던트 + 긴망토 = 전부 기본 42종.**
⇒ 내 안을 채택하면 **`pack.cyber`의 간판 연출이 100% 무료로 샌다.** 추론이 아니라 표 4행 실측이다.

> ### ★ 그런데 P-11을 **글자 그대로** 구현해도 죽는다 — 내가 새로 찾은 것
> P-11은 *"게이트는 엔타이틀먼트"*다. 그대로 짜면 오늘 트리에서 이렇게 된다:
>
> | 실측 | 결과 |
> |---|---|
> | M-5 실린 팩 0개 | `PackRegistry.FindByCohort()`가 **언제나 null** |
> | M-6 출처가 `NullPackEntitlementSource` | `StateOf()`가 **언제나 `Unknown`** |
> | M-9 `Unknown`은 새로 시작 거부 | **아무 코스튬도 안 뜬다** |
>
> ⇒ **무료 오피스까지 포함해 전원이 아무것도 못 본다.** 그리고 그 증상은
> *"기능이 아직 안 붙었다"*와 **화면상 완전히 같다** — 이 저장소가 아홉 번 당한 그 형태다.
> **P-11은 「어느 층에 게이트를 두는가」로는 옳고, 「무엇을 물어보는가」로는 미완이다.**

### 1-2. 최종 규칙 — **소속(cohort) × 개방(entitlement)**

> ### ★★ 규칙 C-1 (최종)
> **코스튬은 「착용 4부위가 합의하는 소속」이 「열려 있을 때」만 산다.**
> - **소속의 단위는 테마가 아니라 코호트다.** 기본 코호트(0)에서만 테마가 소속을 대신한다.
> - **개방은 엔타이틀먼트다.** 단 **무료는 「물어보지 않는 것」**이지 「Owned를 받는 것」이 아니다(M-8).
> - **저장 파일은 개방 판정에 절대 입력되지 않는다**(ENTITLEMENT_CONTRACT E-4-a).

```
CostumeResolver.Resolve() -> CostumeKey?    (null = 코스튬 없음)

 1. 스탯 4슬롯(Head/Eyes/Neck/Shoulders)이 전부 착용 && 해금인가
      → EquipmentStatRules.SlotLoadout(slot).Worn 로 판정한다. 새 술어를 짓지 마라
        (EquipmentStatRules.cs:578이 "잠금 검사를 여기 말고 또 적으면 이음매가 둘이 된다"고 못박았다)
 2. 네 자리의 ItemCatalog.Item(slot, WornIndex(slot)).CohortId 가 전부 같은가   → 아니면 null
 3. cohort == ItemCatalog.BaseCohortId(0) 이면
      3a. 네 자리의 ItemCatalog.ThemeOfItem(...) 이 전부 같은가                → 아니면 null
      3b. CostumeCatalog.FindByBaseTheme(theme)                              → 없으면 null
      3c. 기본 코호트 코스튬에는 엔타이틀먼트가 없다 → 그대로 통과
 4. cohort != 0 이면
      4a. PackRegistry.FindByCohort(cohort)                                   → null이면 null(고아 코호트)
      4b. CostumeCatalog.FindByPack(descriptor.PackId)                        → 없으면 null
      4c. CostumeEntitlement.IsOpen(descriptor):
            descriptor.Entitlements.Count == 0  →  열림   ★ 무료(M-8). StateOf를 부르지 않는다
            그 외 → PackEntitlements.StateOf(descriptor.PackId) 가 Owned 일 때만 열림
                    NotOwned / Unknown → 닫힘 (E-2)
```

### 1-3. 왜 이 규칙이 세 요구를 **동시에** 만족하는가

| 요구 | 만족되는 이유 |
|---|---|
| P-11(연출이 새면 안 된다) | 기본 코호트 `cyber` 4종은 `CohortId == 0`이라 **3번 갈래**로 간다. `CostumeCatalog.FindByBaseTheme("cyber")`가 **없으므로** null. 유료 cyber 코스튬은 `pack.cyber` 코호트에서만 나온다 |
| 사용자 요구(*"세트별로 완전히 다름"*) | 여전히 **입은 것이 정한다.** 바뀐 것은 등가류가 「테마」에서 「코호트」로 옮겨간 것뿐이다 |
| 무료 오피스(X-1) | `pack.office`가 **없어도** 기본 코호트 `office` 테마(중절모·동그란안경·줄무늬타이·배낭 — **4종 전부 에셋 실재**)로 성립한다. 엔타이틀먼트를 **안 묻는다** |

### 1-4. 리더 판정용 — 죽은 안까지 포함한 전체 표

| 안 | 게이트 | 판정 | 왜 |
|---|---|---|---|
| **★ C-1 (1순위 권고)** | 코호트 합의 × 개방 | **채택 권고** | 위 3행 전부 만족. 신규 개념 0개(코호트·엔타이틀먼트 둘 다 실재) |
| A. 테마 세트 완성 | `IsSetComplete` | **기각** | M-1·M-2 — cyber가 무료로 샌다. **내 지난 제안이고 내가 철회한다** |
| B. 엔타이틀먼트 단독 | `StateOf == Owned` | **기각** | M-5·M-6 — 오늘 트리에서 **전원 아무것도 못 본다**. 무료 오피스도 죽는다 |
| C. 엔타이틀먼트 OR 테마 | 둘 중 하나 | **기각** | OR는 약한 쪽으로 붕괴한다 = A와 같아진다 |
| D. 별도 「코스튬 슬롯」 신설 | 사용자가 직접 고름 | **기각** | 세이브 필드 +1, *"입은 것과 다른 코스튬"* 어긋남 상태가 생긴다. 그 어긋남은 화면만 봐서는 못 찾는다 |

### 1-5. `Unknown` 처리 — E-2와 E-3-a를 **한 규칙으로** 만족시킨다

> ### ★★ 규칙 C-2
> **개방 판정은 「세션 안에서 단조(monotonic)」다.**
> - 세션 시작에 1회 · 착용 변경마다 1회 · 세션 중 **60초마다 1회** 재조회한다(E-3-a 권고 간격 그대로).
> - **닫힘 → 열림 전이는 즉시 적용한다**(09:00에 스팀이 안 떠 있어 `Unknown`이었어도 09:01에 살아난다).
> - **열림 → 닫힘 전이는 세션 중에 적용하지 않는다**(E-3-b). 다음 세션에서 정리된다.
> - 비용: 60초에 조회 1회 = 로컬 IPC 1회. `PackEntitlements`가 **부정 캐시를 안 만들므로** 재시도 장치를 따로 짤 필요가 없다(그 클래스 문서가 그렇게 설계됐다고 명시).

### 1-6. ★ 이것은 **C층의 첫 소비자**다 — 되돌릴 수 없다

**M-7: `PackEntitlements.StateOf`의 프로덕션 소비자는 오늘 0건이다.**
코스튬 게이트가 **첫 번째**이고, 첫 소비자가 세우는 어법이 뒤따르는 모든 소비자의 본이 된다.
그래서 위 3·4번 갈래를 **`Core/CostumeEntitlement.cs` 한 파일에 가둔다**.
`Interaction/`이나 `States/`에서 `PackEntitlements.StateOf`를 직접 부르는 코드를 **한 줄도 쓰지 마라** —
그 순간 판정이 두 곳이 되고, 「반만 열린」 상태가 만들어진다.

**감사 1건 신설 요구**: `Tests/EditMode/CostumeEntitlementSingleGateTests` —
프로덕션 소스에서 `PackEntitlements.StateOf` 참조가 **`Core/CostumeEntitlement.cs` 1파일에만** 있는지.
(★ 이 감사는 **부재 단언**이다 — CLAUDE.md가 경고한 «썩으면 조용히 초록이 되는» 종류다.
**같은 테스트 안에서 존재 대조를 붙여라**: 그 1파일에 실제로 참조가 **1건 이상** 있는지 먼저 단언하고,
그 대조가 깨지면 부재 단언 결과를 통째로 폐기하라.)

---

## 2. 코스튬 키 공간과 4종 정의

### 2-1. 키 공간 — `costume.*` 역DNS

```
costume.office     현대 직장인 / 독서실
costume.cyber      사이버펑크 연구원
costume.mine       광부 / 노가다
costume.arcane     판타지 대마법사
```
- **`packId`와 같은 문자열을 쓰지 마라.** `pack.office`는 상품이고 `costume.office`는 연출이며,
  **1:1이 아니다** — `costume.office`는 오늘 팩 없이 기본 코호트 테마에서 나온다(1-3절).
- 모양 검사는 **이미 있는 것을 쓴다**: `PackManifestKeys.IsWellFormed`(ASCII 소문자·숫자·점·밑줄).
  새 검사기를 짓지 마라 — 두 벌이 되면 갈라진다.

### 2-2. 4종의 파생 근거 — **전부 실측**

| 코스튬 | 소속 | 4부위 | 오늘 에셋 실재? | 개방 | 근거 |
|---|---|---|---|---|---|
| `costume.office` | **기본 코호트 테마 `office`** | `equip.head.fedora` · `equip.eyes.round` · `equip.neck.striped` · `equip.shoulders.backpack` | ★ **4/4 실재** | 무료(안 묻는다) | `ItemCatalog.cs` ThemeTable [실측] |
| `costume.cyber` | **팩 `pack.cyber`** (코호트 미배정) | 그 팩의 HEAD/EYES/NECK/BACK 1종씩 | **0/4** (팩 에셋 0건, M-5) | `pack.cyber` 엔타이틀먼트 | `PACK_THEME_SPEC §2-1` + 40-2절 (B)안 |
| `costume.mine` | **팩 `pack.mine`** (7번째 코호트) | 같음 | **0/4** | 엔타이틀먼트 | 9회차 §40-3 |
| `costume.arcane` | **팩 `pack.arcane`** (8번째 코호트) | 같음 | **0/4** | 엔타이틀먼트 | 9회차 §40-3 |

> ### ★★ 이 표가 MVP를 바꾼다 — 나는 지난 라운드의 MVP 권고를 철회한다
> 지난 라운드에 나는 *"MVP는 사이버펑크 연구원 — cyber가 기본 코호트에 4/4 있어 **신규 에셋 0개**"*라고 적었다.
> **C-1을 채택하면 그 문장은 거짓이 된다** — 기본 코호트 cyber는 이제 `costume.cyber`로 안 간다(그게 P-11의 요점이다).
> **`pack.cyber`의 아이템 에셋은 0개고 매니페스트도 0개다**(M-5).
>
> **⇒ 새 MVP: `costume.office`.** 신규 에셋 **0개** · 신규 매니페스트 **0개** · 엔타이틀먼트 배선 **0줄**로
> 파이프라인 전체(해석 → 세이브 → 포즈 → 프롭 → 구간)를 관통한다.
> 4부위가 전부 실재하는 유일한 코스튬이기 때문이다.
>
> ### ★ 그리고 그 MVP에는 **출하 금지 조건**이 붙는다 (P-12)
> `product-strategy` P-12: *"무료 팩이 어떤 표면을 갖는 순간 모든 유료 팩은 그 표면을 최소 한 벌 갖는다."*
> `costume.office`만 든 빌드는 **「유료가 무료보다 빈약하다」를 실제로 만든다.**
> ⇒ **릴리즈 게이트 R-1: `costume.office`는 유료 코스튬이 최소 1개 함께 출하되기 전에는 공개 빌드에 들어가지 않는다.**
> 개발 빌드에서는 켠다. 스위치는 `StickConfig.costumeFocusEnabled`(기본 `false`, 1-2절 규칙과 무관한 마스터 스위치).

### 2-3. `CostumeManifestSO` — 신규 ScriptableObject 1종 (불변 원칙 4)

**왜 `StickPackManifestSO`에 필드를 더하지 않는가:**
1. `costume.office`는 **팩이 없다**(1-3절). 팩 매니페스트에 넣으면 무료 오피스가 팩 에셋을 요구하게 되고, MVP가 죽는다.
2. 매니페스트 스키마를 v2→v3로 올려야 하고, 그러면 **v2 앱이 v3 팩을 반쯤 읽는** 창이 열린다
   (`StickPackManifestSO.SchemaVersion` 문서가 막으려는 바로 그것).
3. 연출은 상품과 수명이 다르다 — 같은 팩에 코스튬을 나중에 얹는 경우가 정상이다.

**`Assets/_Project/Scripts/Core/CostumeManifestSO.cs` (신규)**

```csharp
public enum CostumeSourceKind { BaseTheme = 0, Pack = 1 }

[CreateAssetMenu(fileName="CostumeManifest", menuName="StickMate/Costume Manifest", order=3)]
public sealed class CostumeManifestSO : ScriptableObject
{
    public const int SchemaVersion = 1;

    public string costumeKey;              // "costume.office" — PackManifestKeys.IsWellFormed로 검사
    public int    requiresSchemaVersion = 1;

    public CostumeSourceKind sourceKind;   // BaseTheme | Pack
    public string sourceId;                // BaseTheme면 "office"(ItemCatalog 테마 키) / Pack이면 "pack.cyber"

    public string displayNameKey;          // 키만. 원문 금지(ARCHITECTURE §528)

    // ── (가) 소환 오브젝트 = 전 팩 필수 (P-13) ──────────────────────────────
    public CostumePropShapeData[] propShapes;   // 좌표 스트림. 비면 프롭 없음
    public float propAnchorOffsetXInH;          // 캐릭터 발밑 기준 수평 오프셋(신장 H 배수)

    // ── (나) 전용 캐릭터 모션 = 상위 티어만 (P-13) ─────────────────────────
    // ★ 불리언을 두지 마라. "있으면 상위 티어"가 곧 데이터다 — 플래그를 두면
    //   플래그와 실물이 갈라진다(StickPackManifestSO가 아이템 목록을 안 드는 것과 같은 이유).
    public CostumeKeyposeTableSO keyposes;      // null = 프롭만(하위 티어)

    // ── 3단계 진화 ────────────────────────────────────────────────────────
    // ★ 임계 «값»은 여기 없다 — design-systems 단일 출처(CostumeEvolutionRules)를 본다.
    //   여기 적으면 팩마다 임계가 갈려 "이 팩만 빨리 큰다"가 되고 그건 P2W 축이다.
    public CostumeStageOverride[] stageShapes;  // 단계별 조각 차이(0=기본). 비면 단계별 외형 변화 없음
}
```

**`CostumeCatalog`(신규 static, `Core/`)** — `Resources.LoadAll<CostumeManifestSO>(ItemCatalog.ItemResourceFolder)` 1회 로드 +
`FindByBaseTheme(string)` / `FindByPack(string)` 두 창구. **`PackRegistry`와 완전히 같은 어법으로 쓴다**
(결함은 고치지 않고 신고 · 순수 `Build()` 분리 · `ResetForTesting()`).

**합격 기준(불변 원칙 4)**: **9번째 코스튬을 프로덕션 `.cs` 0줄로 추가할 수 있는가.**
`Tests/EditMode/CostumeManifestCorridorTests`가 합성 9번째 코스튬을 직접 먹여 잰다
(`PackManifestCorridorTests`가 합성 7번째 팩으로 하는 것과 같은 형태).

### 2-4. ★ P5의 진짜 선결 조건 — **장비 조형이 아직 에셋화되지 않았다**

> **M-23 [실측]: 스탯 4슬롯 24종 중 에셋이 형상을 가진 것은 6종(NECK)뿐이다.**
> `AccessoryShapeBuilder.cs:1431`이 `AppendWorn`을 **NECK에만** 건다.
> HEAD/EYES/BACK 18종은 여전히 `AppendHead`/`AppendEyes`/`AppendBack`의 **코드 switch**다.

⇒ **`pack.mine` · `pack.arcane`의 신규 12종 중 HEAD/EYES/BACK 6종은 오늘 「프로덕션 `.cs` 0줄」로 추가할 수 없다.**
`StickPackManifestSO` 클래스 문서가 약속한 *"새 팩 = 새 에셋 파일들, 그게 전부"*는 **NECK에서만 참이다.**

**이건 PART2가 만든 문제가 아니라 PART2가 처음 밟는 문제다.** 순서에 반영한다(9절 P5).
`Tests/EditMode/PackManifestCorridorTests`가 이 갭을 못 잡고 있다면 **`Assert.Ignore`(사유 포함)로 남겨라** —
CLAUDE.md 규칙대로 러너에 「건너뜀」으로 계속 보이게.

---

## 3. 세이브 스키마 — v11 → **v12**

### 3-1. 필드 형태 (그대로 적는다)

> ★ **이 절은 `design-systems` R26(`docs/DESIGN_SYSTEMS_COSTUME_EVOLUTION.md` §6-1)과 같은 라운드에
> 병렬로 쓰였고 두 문서가 갈라졌다.** 갈라진 지점과 판정은 **13절**에 모았다.
> 여기 적힌 것이 **그 판정을 반영한 최종형**이다(스키마 «형태»는 내 축, 스키마 «값·단위»는 그쪽 축).

`Core/CharacterSaveStore.cs`의 `SaveData` 맨 끝에 **필드 2개**:

```csharp
// ================================================================
// ---- v12: 코스튬별 누적 집중 시간 (2026-09-07, PART2) ----
// ================================================================
// 정본: docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md 3절(형태) +
//       docs/DESIGN_SYSTEMS_COSTUME_EVOLUTION.md §4·§5·§6(단위·격자·소프트캡).

/// <summary>코스튬을 «입은 채» 집중 세션을 마친 누적 «분». 없으면 기록 없음이고
/// 그것이 정확한 사실이다. 0분인 코스튬은 배열에 넣지 않는다.</summary>
public CostumeFocusRecord[] costumeFocus;

/// <summary>오늘 코스튬 누적에 실린 분(전 코스튬 공유 1개, 일일 소프트캡용).
/// 일자 롤오버에 0으로 돌아간다 — archeryCoinsToday·focusXpToday와 같은 종류다.</summary>
public int costumeFocusMinutesToday;
```

`Core/CostumeProgressModel.cs`(신규)에:

```csharp
[Serializable]
public sealed class CostumeFocusRecord
{
    public string costumeKey;    // CostumeManifestSO.costumeKey 그대로 ("costume.office")
    public int    focusMinutes;  // ★ 정수 「분」. 초도 float도 아니다 — 3-2절
}
```

**정규화(로드 시. `CurrencyModel.RestoreFromSave`의 관례를 그대로 따른다)**:
`null` → 빈 배열 / `CostumeCatalog`가 모르는 `costumeKey` 버림 / 중복 키는 첫 항목만 /
음수 분은 0 / `costumeFocusMinutesToday`는 `[0, 일일소프트캡]` 클램프.

> ### ★ I-2 수정 — 「가변 길이 배열 **2개**」를 **레코드 배열 1개**로 고친다
> 나는 지난 라운드에 *"가변 길이 배열 2개"*라고 적었고, **`design-systems`가 그것을 그대로 승계했다**
> (§6-1: *"game-architect가 이미 고른 형태를 그대로 승계한다"*). **내가 그 형태를 철회한다.**
>
> | | 병렬 배열 2개 | ★ **레코드 배열 1개(채택)** |
> |---|---|---|
> | 키-값 어긋남 | 가능 → **「길이 불일치면 둘 다 버린다」 분기와 그 테스트가 필요** | ★ **문법적으로 불가능** |
> | 가변 길이(팩 늘어도 `v++` 없음) | ✅ | ✅ **같다** — 그 이점은 «병렬»이 아니라 «가변»에서 온다 |
> | 저장소 선례 | 없음 | ★ `ItemGraceBaseline[]`(M-11)이 **키와 값을 한 레코드에** 담고 이미 출하돼 있다 |
>
> **`design-systems` §6-2가 「길이 불일치 → 둘 다 버린다」라는 위생 규칙을 세운 것 자체가
> 이 형태가 만드는 실패 모드의 증거다.** 레코드 배열은 그 규칙과 그 테스트를 **통째로 없앤다.**
> 그쪽 §6-2의 나머지 네 줄(모르는 키 버림 · 중복 첫 항목 · 값 범위 · 길이 상한)은 **전부 그대로 산다.**

> ### ★ `stageReached` 필드는 **두지 않는다** — `design-systems` §3-3에 양보한다
> 나는 1차안에서 high-water 필드를 제안했다. **철회한다.**
> 그쪽 근거: *"누적은 줄어들지 않으므로 **강등이 문법적으로 존재하지 않는다.** `statTierReached[]`가
> 필요했던 이유(스탯은 로드아웃에 따라 내려간다)가 여기엔 없다."* **맞다.** 단계는 `focusMinutes`에서
> **파생**한다(`CostumeEvolutionRules.StageOf`).
> ★ **다만 이 양보에는 되살릴 조건이 하나 있고 그것을 여기 남긴다**: **임계값을 패치로 «올리면»**
> 이미 3단계였던 사용자가 2단계로 내려앉는다. 오늘 그 위험이 낮은 이유는 10 h·50 h·100 h가
> **사용자가 직접 쓴 숫자**이고 `design-systems`가 *"그 넷을 하나도 옮기지 않는다"*고 못박았기
> 때문이다. **임계를 올리는 라운드가 생기면 그 라운드가 `stageReached`를 함께 도입해야 한다** —
> 그때는 v13이고 하위 호환 1벌이 따라온다.

### 3-2. ★★ 단위가 **정수 「분」**인 이유 — 두 라운드가 독립으로 같은 실측에 도달했다

`float32`에 `dt = 1/60`을 계속 더할 때, **1분(3,600회 가산)의 실제 증가량** [실측, numpy `float32`]:

| 시작 누적 | 1분 뒤 실제 증가 | 오차 |
|---:|---:|---:|
| 36,000 s (10 h) | 56.250 s | **−6.2 %** |
| 180,000 s (50 h) | 56.250 s | **−6.2 %** |
| 262,144 s (72.8 h) | 112.500 s | ★ **+87.5 %** |
| 360,000 s (**100 h — 마스터 임계**) | 112.500 s | ★ **+87.5 %** |
| 524,288 s (145.6 h) | **0.000 s** | ★ **−100 % (완전 정지)** |

원인: 누적값의 ULP가 `dt`를 추월한다. 72.8 h 이후 ULP = 0.03125 s > 2×dt라 매 가산이 **1 ULP를 통째로**
먹어 시계가 87.5 % 빨라지고, 145.6 h 이후엔 ULP/2 > dt라 **아예 안 들어간다**.

> **⇒ 100시간 마스터 임계가 「87.5 % 빨리 도달」 구간 한복판에 있다.**
> 증상은 *"어떤 사람은 54시간 만에 마스터가 되고 어떤 사람은 안 된다"*이고
> **화면에도 로그에도 아무 흔적이 안 남는다.**
>
> ★ **`design-systems` R26 §4-3이 같은 라운드에 독립적으로 같은 값을 냈다**(−6.25 % / +87.5 %).
> **조율 없이 두 라운드가 같은 실측에 수렴했다 — 이 판정은 교차 검증됐다.**

**처방 — 여기서 나는 내 1차 처방을 `design-systems` 것으로 바꾼다:**

| | 내 1차안 | ★ **채택(§4-2·§4-3)** |
|---|---|---|
| 단위 | 정수 초 + 메모리 `double` 반송값 | **정수 분** |
| 더하는 시점 | 매 프레임(반송) | ★ **세션 종료 1회** |
| 프레임 누적 축 | 남는다(반송값 버그 여지) | ★ **통째로 사라진다** |
| 코인·XP와의 격자 | 다르다 | ★ **같다**(`floor(경과초/60)`) |

그쪽 검산을 그대로 옮긴다 — **한 세션에서 세 값이 같은 길이에서 나온다**:
```
25분 완주     → 누적 +25분,  600동전, 150XP
3분40초 취소  → floor(220/60) = 3분 → 누적 +3분,  60동전,  15XP
40초 취소     → floor(40/60)  = 0분 → 누적 +0분,   0동전,   0XP
```
`6,000`(100 h)은 `int`에서 정확하고, **부동소수가 이 축에서 완전히 사라진다.**

**남는 테스트 1건**: `Tests/EditMode/CostumeAccrualGridTests` — 세 종료 경로(M-21)에서
누적 분과 지급 동전의 **격자가 같은가**. 기대값은 위 3행을 **상수로** 적는다
(프로덕션 함수로 기대값을 만들면 그 함수가 틀어질 때 기대값도 함께 틀어져 아무것도 못 잰다).

### 3-3. 버전을 올리는 근거 — **v10 · v11과 글자 하나까지 같다**

`costumeFocus`의 「없음」은 `null`이고 그 `null`은 *"아직 아무 코스튬도 안 입었다"*라는 **정확한 사실**이다.
⇒ 「없음 ≠ 0」 규칙만으로는 v12가 **강제되지 않는다.** 그런데도 올리는 이유는 **다운그레이드 방어** 하나다:
이 필드를 v11 번호로 디스크에 앉히면 v11 시절 빌드가 그 파일을 **자기 버전으로** 읽어
`data.version > CurrentVersion` 검사를 못 타고, `SaveSuspended`가 안 걸린 채
60초 뒤 자동 저장이 **누적 100시간을 통째로 지운다.** 동전과 같은 등급의 「못 되버는 값」이다.

```csharp
internal const int CurrentVersion = 12;

/// <summary>코스튬 누적 집중 시간이 처음 들어간 버전 — FirstVersionWithGameplayCurrency와
/// 같은 이유로 존재한다(로드 분기용이 아니라, 테스트가 숫자를 베끼지 않게 하려고).</summary>
internal const int FirstVersionWithCostumeFocus = 12;
```

### 3-4. 하위 호환 테스트 — **의무 1건** (CLAUDE.md)

`Tests/EditMode/EquipmentMigrationTests`에 v10/v9가 쓴 것과 **같은 형태**로 추가:

```
[Test] public void v11_파일을_읽어도_코스튬_누적이_비어_있고_v11_값이_그대로_남는다()
```
반드시 **두 가지를 같은 테스트에서** 확인한다(v9 테스트가 세운 본):
1. `costumeFocus`와 `costumeFocusMinutesToday`가 없는 v11 파일에서 **빈 배열 · 0**으로 떨어지는가
   (= *"아직 아무 코스튬도 안 입고 집중한 적이 없다"* — v11 사용자에게 **사실이다**, 기능이 없었다)
2. **같은 파일의 다른 v11 값들이 그대로 살아남는가** — `focusXpToday` · `coinBalance` · `level`.
   (2)가 없으면 (1)은 *"파일을 통째로 버렸다"*와 구별되지 않는다.

그리고 왕복 1건: `v12_왕복은_코스튬_누적_분과_오늘분을_보존한다`.
정규화 3건: 모르는 키 버림 · 중복 키 첫 항목만 · 음수 분 0으로.

### 3-5. ★ 이 필드는 **엔타이틀먼트가 아니다** — 감사와 충돌하지 않음을 미리 못박는다

`EntitlementNotInSaveAuditTests`가 세이브 필드 **이름**에 `dlc/pack/entitle/owned`가 들어오는 것을 막는다.
`costumeFocus` / `costumeFocusMinutesToday` / `costumeKey` / `focusMinutes` — **네 이름 전부 걸리지 않는다** [실측].

> **그러나 규칙을 하나 명시적으로 건다 (규칙 C-3):**
> **`CostumeResolver`·`CostumeEntitlement`는 세이브를 절대 읽지 않는다.**
> 저장된 것은 *"입은 채 몇 초를 보냈는가"*이지 *"가졌는가"*가 아니다.
> 그 둘을 섞는 순간 **세이브 파일이 결제 우회 표적이 된다** — C층을 세이브에서 뺀 목적이 **표적 제거**였다.
> 감사 1건: `CostumeSaveIsNotEntitlementTests` — `Core/CostumeEntitlement.cs`·`Core/CostumeResolver.cs`의
> 소스에 `CharacterSaveStore`/`CostumeProgressModel` 참조가 **0건**인가(존재 대조 동반).

---

## 4. 상태 ID 신설 금지 — 준수 배선

### 4-1. 함정의 실물 (M-12 · M-13)

```
Platform/FramePacing.cs:499
    return blackboard.Machine.CurrentStateId == Core.StickmanStateId.Idle;
```
절감 등급의 **유일한** 캐릭터측 입력이다. `characterIdle` → `characterStill`(1.6초 체류) → `Still` 등급.

| 코스튬을 새 상태 ID로 만들면 | 제출률 | 근거 |
|---|---|---|
| 세션 25분 내내 `CurrentStateId != Idle` | **30/초 고정**(Active) | `BuildPlan` Active 분주 2 |
| 지금(코스튬 없음, Idle 유지) | **15/초**(Still) | `BuildPlan` Still 분주 4 |

**⇒ 제출이 정확히 2배.** (지난 라운드에 나는 *"GPU 4배"*라고 적었다 — 그건 `DefaultActiveDivisor`가 1이던 시절
기준이었고, **2026-09-07에 기본값이 2로 바뀌었다**(`ViewerPresence.cs:582`). **2배로 정정한다.**
`perf-doc` 4점 선형 `GPU% ≈ 2.3 + 0.354 × 제출fps`를 그대로 쓰면 15/초 7.6 % → 30/초 12.9 %, **+5.3 %p** [계산].)

> ### ★★ 규칙 C-4
> **코스튬은 새 `StickmanStateId`를 만들지 않는다.** `Idle`의 **포즈 층**에서만 산다.
> `WanderAmbientMotion`에도 값을 더하지 않는다 — 그건 «짧은 변주» 어휘이고 코스튬은 «지속 자세»다.

### 4-2. 선례가 이미 그 문제를 풀어 놓았다 [실측]

`States/StickmanBlackboard.cs:2306`:
```csharp
if (_focusStanceActiveThisFrame)
{
    bool gesturing = TickIdleAmbientMotion(deltaTime);
    pose.ApplyFocusWatchStancePose(deltaTime, BuildPoseSettings(), PoseSmoothingRate,
        BuildFocusWatchStanceInput(gesturing));
    return;
}
```
이 분기에 **도달했다는 것 자체가 `CurrentStateId == Idle`을 뜻한다** — 앞의 조기 반환 열 몇 개가
Walk/Fall/등반/춤/활쏘기/Focus* 를 전부 걸러냈고, `TickFocusWatchStance`(`:2481`)가
`id != StickmanStateId.Idle`이면 `false`를 돌려준다. **집중 관망 자세는 Still 등급을 한 번도 안 깬다.**
⇒ **패턴 재사용 가능. 그대로 쓴다.**

### 4-3. 배선 — 정확한 위치

**`States/StickmanBlackboard.cs` `TickPoseRouting`, 위 분기를 다음으로 교체:**

```csharp
if (_focusStanceActiveThisFrame)
{
    bool gesturing = TickIdleAmbientMotion(deltaTime);

    // ★★ 코스튬 LFVS — «몰입기»에만, 그리고 제스처가 안 도는 동안에만 포즈를 가져간다.
    //    새 상태 ID를 만들지 않는 것이 이 배선의 전부다(규칙 C-4).
    //    이 분기가 없어도 아래 한 줄이 그대로 돌아 거동이 100% 예전과 같다.
    if (!gesturing && TickCostumeKeypose(deltaTime, pose)) return;

    pose.ApplyFocusWatchStancePose(deltaTime, BuildPoseSettings(), PoseSmoothingRate,
        BuildFocusWatchStanceInput(gesturing));
    return;
}
```

**왜 `!gesturing`인가**: 앰비언트 제스처 4종(`FocusAmbientGestures`)은 **부드러운 보간**이 어휘의 본체다
(G2 끄덕임 2박은 1.20초에 걸쳐 감쇠로 그려진다). LFVS는 **보간 없음**이 정의라 둘이 같은 프레임을 다투면
제스처가 스텝에 잘려 죽는다. 제스처가 도는 동안은 기존 경로에 돌려주고, LFVS는 스텝 위상을 리셋한다.

**왜 «몰입기에만»인가** — 8절이 근거다. 요약: 적응기·한계에는 기존 관망 자세 + 제스처가 그대로 돌고,
그 구간이 곧 **프롭이 없는 구간**이라 배회도 평소대로 돈다.

**`States/StickmanBlackboard.cs`에 추가할 조회 창구 3개** — 어법은 `IsFocusSessionAmbientActive`와 한 글자도 다르지 않게:

```csharp
public FocusSessionPhase FocusPhase { get; }              // FocusWatchDirector 읽기 전용 조회 + 캐싱
public bool IsCostumeImmersionActive { get; }             // 마스터 스위치 × 세션 × 몰입기 × 코스튬 해석 성공
public string ActiveCostumeKey { get; }                   // 해석 결과(캐시). 없으면 null
```

**`States/AutoWanderController.cs` `ResolvePostIdleBranch`(:377) 사다리에 한 칸 추가:**

```csharp
bool hold  = IsRadialMenuHolding;
bool immer = !hold && IsCostumeImmersionHolding;          // ★ 신규
bool away  = !hold && !immer && IsViewerAwayForWander;
float walkChance = hold  ? 0f
                 : immer ? 0f                              // ★ 프롭 곁을 안 떠난다
                 : away  ? Cfg(c => c.awayWanderWalkChance, 0.15f)
                 : IsFocusAmbientActive ? Cfg(c => c.focusSessionWalkChance, 0.4f)
                 :                        Cfg(c => c.wanderPostIdleWalkChance, 0.75f);
```
사다리는 여전히 **단조 내림차순**(0 = 0 > 0.15 > 0.40 > 0.75)이라 우선순위 판정 코드가 필요 없다 — 그 파일이
`:371`에 적어 둔 성질을 그대로 상속한다.
**`wanderIdleDurationMin/Max`는 절대 무수정**(design-motion이 2026-09-07에 못박은 것 — 베끼면 p99가 깨진다).

**부수 효과(의도된 것)**: 몰입기 동안 걷기가 0이 되어 `Still` 체류가 늘고 **제출이 준다.**
사용자 요구 4번(*"기존 FramePacing과 충돌 없이 절감 등급 정상 동작"*)을 **깨지 않을 뿐 아니라 돕는다.**
[계산] 25분 세션에서 몰입기 900초 동안 걷기 지분(현행 0.40 사다리 기준 실측 걷기 2.75초/주기 8.08초 = 34 %)이
0이 되면 그 구간 제출이 30/초 → 15/초로 내려간다: **900 × 0.34 × 15 = 4,590 제출 절감/세션**.
★ [추정 아님, 그러나 실기 미확인] — 8.08초 주기·2.75초 걷기는 design-motion의 6h×3시드 시뮬레이션 값이다.

---

## 5. LFVS(저프레임 벡터 스텝) 규격

### 5-1. 왜 「3~5프레임 비트맵」이 아닌가 (지난 라운드 반증의 요약)

GPU 비용은 **면적 × 제출 고정분**이라 콘텐츠와 무관하다(`perf-doc` 실측: 아무것도 안 그리는 대조 프로그램이
같은 창 크기·제출률에서 StickMate의 85 %를 재현). 비트맵으로 바꿔도 **GPU는 1원도 안 줄고**, 대신
배율 0.35~1.5 대응과 **전 장비 불투명 계약**(사용자 확정)이 깨진다. **줄일 수 있는 것은 CPU다.**

### 5-2. 스텝레이트 — 약수 제약 [계산, 검산 동반]

제출률(M-13): Active/Calm **30/초**, Still/Away **15/초**.
스텝 하나가 **모든 등급에서 정수 개의 제출**을 받아야 박자가 안 흔들린다 ⇒ 스텝레이트는 **15의 약수**여야 한다.

```
15 / 1 = 15  OK        15 / 2 = 7.5  ✗        15 / 3 = 5   OK
15 / 4 = 3.75 ✗        15 / 5 = 3    OK       15 / 6 = 2.5 ✗       15 / 15 = 1 OK
(30에서는 1·2·3·5·6·15가 전부 정수이므로 제약은 15가 지배한다)
```

> ### ★★ 규칙 C-5
> **`stepsPerSecond ∈ {3, 5}`.** 1은 슬라이드쇼, 15는 Still과 같아 절감이 0이다.
> - **3/초** — 스텝 333.33 ms. 제출 5장(Still) / 10장(Calm·Active)
> - **5/초** — 스텝 200.00 ms. 제출 3장(Still) / 6장(Calm·Active)
> **4/초를 쓰지 마라** — 15 ÷ 4 = 3.75라 스텝마다 3장·4장이 번갈아 나온다. 그 불균일은
> 「평균 fps」로는 안 보이고 **눈에는 보인다**(FramePacing 클래스 문서의 45fps 판정과 같은 논리).

**런타임 가드 1건**: `STICKMATE_STILL_DIVISOR`(계측용 환경변수, 1~8)가 4가 아니면 제출률이 15가 아니게 된다
(예: 8 → 7.5/초). 그때는 **한 번만** 경고 로그를 남기고 계속 돈다 — 벽시계 기준이라 박자 자체는 유지되고
정렬만 흐트러진다. **출하 경로가 아니다**(그 변수는 A/B 계측 도구다).

### 5-3. 포즈 스무딩 우회 — **왜 필요하고 어디를 지나가는가**

감쇠 계수 35/초, `dt = 1/60` ⇒ 프레임당 잔존 `exp(−35/60) = 0.558035` [계산].
`RebuildEpsilonDegrees = 0.05°`(M-15)까지 가라앉는 데 걸리는 프레임:

| 스텝 각도 변화 | 프레임 | 시간 |
|---:|---:|---:|
| 5° | 8 | 133.3 ms |
| 10° | 10 | 166.7 ms |
| 20° | 11 | **183.3 ms** |
| 40° | 12 | 200.0 ms |

**3스텝/초(333 ms)에서 20° 스텝은 183 ms(55 %)를 이징에 쓴다.**
⇒ (a) *"보간 없이 즉시 전환"*이라는 LFVS의 정의가 깨지고, (b) 그 183 ms 동안 팔다리가 매 프레임 다시 구워져
**절감이 절반으로 준다.** 그래서 **스냅이 필수다.**

**`States/StickmanPoseAnimator.cs`에 공개 메서드 1개 추가** (`ApplyIdlePoseImmediate`(:3145)가 세운 본):

```csharp
/// <summary>보간 없이 즉시 키포즈로 스냅한다(LFVS 전용).
/// ★ 상체 기울임을 «즉시 + 요청» 둘 다 건다 — 요청만 하면 TickBodyLean이 감쇠로 따라와
/// 스텝 사이에 계속 움직이고, 즉시만 하면 다음 프레임 TickBodyLean이 0으로 되돌린다.</summary>
public void ApplyCostumeKeyposeImmediate(in PoseSettings settings, in CostumeKeypose key)
{
    SetBodyOffset(key.BodyOffsetY);
    for (int i = 0; i < _limbs.Length; i++) { ... SetSegmentImmediate(...); }
    SetBodyLean(key.LeanDegrees);        // 즉시 (M-17: 같은 값이면 조기 반환 = 무비용)
    RequestBodyLean(key.LeanDegrees);    // 요청 — TickBodyLean의 Damp(x, x) = x → SetBodyLean 조기 반환
}
```

> ★ **`SetBodyLean`은 원래 즉시 함수다**(`:3638`, 감쇠 없음) — 그 사실을 실측으로 확인했다.
> 그리고 `:3643`의 `if (clamped == _bodyLeanDegrees) return;` 덕분에 **스텝 사이 프레임은 완전 무비용**이다.
> `RequestBodyLean`은 `private`이라 이 메서드가 **반드시 `StickmanPoseAnimator` 안에** 있어야 한다.

**⇒ 스텝 사이 프레임의 비용: Transform 쓰기 0 · `LineRenderer.SetPositions` 0 · 할당 0바이트.**
각도가 **비트 단위로 같아** `LimbCurveRenderer.Rebuild`의 조기 `continue`(`:441`)에 전부 걸린다.

### 5-4. 키포즈 표의 자료구조

**`Core/CostumeKeyposeTableSO.cs` (신규 ScriptableObject)**

```csharp
[Serializable]
public struct CostumeKeypose
{
    public float leftUpperArm,  leftLowerArm,  rightUpperArm,  rightLowerArm;   // 도(deg)
    public float leftUpperLeg,  leftLowerLeg,  rightUpperLeg,  rightLowerLeg;   // 도
    public float leanDegrees;      // 상체 기울임(+ = 앞). MaxBodyLeanDegrees로 클램프된다
    public float bodyOffsetY;      // 신장 H 배수. 「내려앉기」용
    public byte  propFrame;        // 프롭의 몇 번째 변형인가(0 = 변형 없음) — 6-4절
}

[CreateAssetMenu(...)]
public sealed class CostumeKeyposeTableSO : ScriptableObject
{
    public int stepsPerSecond = 3;      // ★ {3,5}만. 로드 시 검사하고 아니면 이 표를 싣지 않는다
    public CostumeKeypose[] keyposes;   // 길이 2~8. 순환 재생
    public int[] stageKeyposeStart;     // 단계별 시작 인덱스(길이 = 단계 수). 없으면 전 단계 공통
}
```

- **배열이 아니라 이름 붙은 8각도인 이유**: 배열은 마디 열거 순서에 의존한다. 그 순서가 바뀌는 날
  **모든 코스튬의 팔다리가 한 칸씩 밀리고**, 저장 파일을 열어봐도 안 보인다
  (`CharacterSaveStore`가 창 위치를 `Vector2[3]`이 아니라 이름 붙은 9필드로 둔 것과 **같은 판단**).
- **`float`로 충분하다** — 각도는 누적되지 않는다(3-2절의 병은 «누적»에서 온다).
- **재생 위치는 벽시계다**: `stepIndex = (int)(elapsedSeconds * stepsPerSecond) % keyposes.Length`.
  프레임 수로 세지 마라 — CLAUDE.md가 금지한 형태이고, 등급이 바뀌면 프레임 수의 뜻이 바뀐다.

### 5-5. LFVS 합격 기준 — **CPU · 호출수 · 할당** (GPU가 아니다)

| # | 재는 것 | 어떻게 | 합격선 |
|---|---|---|---|
| L-1 | 팔다리 다시 굽기 | `LimbCurveRenderer.LastRebuiltSegmentCount` 초당 합 | 3스텝/초에서 **≤ 3 × 세그먼트수**. 스텝 사이 프레임은 **정확히 0** |
| L-2 | 할당 | `Profiler.GetMonoUsedSizeLong()` 델타(몰입기 60초) | **0 바이트** |
| L-3 | 등급 | `FramePacing.CurrentTier` | 몰입기 동안 **`Still` 도달이 관측된다**(★ 이게 핵심 증거다) |
| L-4 | CPU 구간 | `StallAttribution` `Agent` + `Renderers` 섹션 ms | 코스튬 OFF 대비 **증가 없음** |
| L-5 | 대조(양성) | 스텝레이트를 15로 강제 | L-1이 **실제로 15배로 뛰는가** — 안 뛰면 L-1 측정이 죽은 것이다 |

> ★ **L-5를 빼지 마라.** 「0이 나왔다」는 「절감이 됐다」와 「계기가 안 붙었다」를 **구별하지 못한다**.
> 이 저장소가 아홉 번 당한 형태다.
> ★ **GPU ms를 합격 기준에 넣지 마라** — 이 라운드에서 0이 나오는 것이 **정상**이고,
> 그건 실패가 아니라 **측정 한계**다(GPU는 면적 × 제출률의 함수다).

---

## 6. 소환 오브젝트(프롭) 렌더 아키텍처

### 6-1. 사용자 요구를 코드 사실로 번역

> 원문: *"대형 오브젝트는 별도 Static 레이어로 1회만 그리고 고정, 매 프레임 재드로우 금지"*

**Unity에는 「Static 레이어」가 없다.** 요구의 실체는 **「한 번 만든 뒤 아무도 안 만진다」**이고,
**이 저장소에는 그 패턴이 이미 있다** [실측]:

```
Interaction/GraffitiRenderer.cs:249   case Mode.Holding:  break;    // ← 유지 구간에 쓰기 0줄
```
`RopeClimbRenderer`도 같은 계열이다: `EnsureBuilt()` 1회 → 이벤트가 올 때만 `SetPosition`.
**밧줄은 손이 움직이므로 매 이벤트 갱신하지만, 램프·곡괭이·마법서·단말기는 안 움직인다** ⇒ 갱신 0.

### 6-2. `CostumePropRenderer` — 세 선례를 그대로 상속한다

`Interaction/CostumePropRenderer.cs` (신규). **다음 6가지는 세 렌더러의 공통 규약이라 그대로 베낀다:**

| # | 규약 | 근거 |
|---|---|---|
| 1 | `LineRenderer`만 쓴다. 스프라이트 에셋 0개 | 배율 0.35~1.5 · 불투명 계약 |
| 2 | 캐릭터 `LineRenderer`의 `sharedMaterial`을 빌린다. `Shader.Find` 금지 | 빌드 스트리핑 위험 |
| 3 | **같은 `GameObject`의 `StickmanAgent`만** 참조. 씬 전역 탐색 폴백 없음 | 프리팹 복제 시 프롭이 두 벌 그려지는 사고 |
| 4 | **콜라이더 0개.** `ActiveColliderCount` 진단 창구 노출 | 원칙 3 · 비침해 원칙 2(클릭 관통 유지) |
| 5 | `LateUpdate`에서 `SuspendedOverlayGate.FreezeAndHide(_agent, _container, ref _hidden, "[코스튬]", "소품")` | 컨테이너가 `SetParent(null)` 독립 루트라 `Suspend()`가 구조적으로 못 닿는다. `SuspendedOverlayLeakAuditTests`가 소스 스캔으로 잡는다 |
| 6 | 종료/취소에 **즉시** 철거 | *"창이 닫혔는데 그림만 허공에 남으면 «우리 앱이 그 창에 뭘 걸어뒀다»는 인상"* |

### 6-3. 수명 — **정확히 이 4개 시점에만 움직인다**

```
① 몰입기 진입          EnsureBuilt() 1회 + 앵커 확정 1회        (구축·좌표 쓰기)
② 몰입기 사이 전 프레임  아무 일도 하지 않는다                    ← 요구의 실체
③ 한계 구간 진입        Teardown()                              (철거)
④ 세션 종료 3경로       Teardown()                              (M-21 세 경로 전부)
```

- **단계(진화) 변경으로 다시 굽는 일은 없다** — 단계는 세션 경계에서만 바뀐다(7-3절).
- **앵커**: `ArcheryGroundWorldY`(M-25)와 **같은 어법**. 몰입기 진입 프레임의
  캐릭터 발바닥 Y와 `Body.position.x + facing × propAnchorOffsetXInH × CharacterHeightWorld`를 **스냅샷**한다.
  ★ **새 「GroundLine」 개념을 만들지 마라** — 지난 라운드의 내 I-6을 여기서 **철회한다**. 선례가 이미 있다.
- **몰입기 동안 캐릭터가 안 걷는다**(4-3절)는 것이 이 «고정»을 성립시키는 조건이다.
  걷기를 0으로 안 내리면 프롭만 덩그러니 남는 그림이 나온다 — **두 결정은 한 묶음이다.**

### 6-4. `propFrame` — 프롭이 「움직여야」 할 때만의 탈출구

곡괭이질처럼 소품이 **캐릭터 손에 붙는** 경우가 있다. 그때만 `CostumeKeypose.propFrame`이 0이 아니고,
렌더러는 **미리 구운 N개 변형 중 하나를 `enabled`로 토글**한다(좌표 재계산 0, `SetPositions` 0).
N ≤ 4로 제한한다. **`propFrame`이 전부 0인 코스튬은 6-3 ②가 문자 그대로 참이다.**

### 6-5. 프롭 합격 기준 — **CPU 호출수/할당** (지난 라운드 판정의 수치화)

| # | 재는 것 | 합격선 |
|---|---|---|
| P-1 | 몰입기 60초 동안 `LineRenderer.SetPosition(s)` 호출 | `propFrame` 전부 0인 코스튬에서 **정확히 0**(구축 프레임 제외) |
| P-2 | `GameObject`/`Component` 생성 | 몰입기 진입 1회 이후 **0** |
| P-3 | 할당 | 몰입기 60초 델타 **0 바이트** |
| P-4 | `StallAttribution.Renderers` | 코스튬 OFF 대비 **+0.05 ms 이내**(`LateUpdate`의 `FreezeAndHide` 1회분) |
| P-5 | 콜라이더 | `ActiveColliderCount == 0` — **항상** |
| P-6 | 대조(양성) | 고의로 매 프레임 `SetPosition`을 부르는 판을 만들어 **P-1이 실제로 3,600을 세는지** |

> ★ **P-6이 없으면 P-1의 「0」은 계기가 안 붙은 것과 구별되지 않는다.**

---

## 7. 진화 단계(S4: 상태 4 · 진화 3회) — 구조 확정

### 7-1. 임계 숫자는 `design-systems`가 이미 냈다 — **구멍이 채워졌다**

같은 라운드에 `docs/DESIGN_SYSTEMS_COSTUME_EVOLUTION.md`(R26)가 착지했다.
**내 `[DS-구멍 1]`은 그 문서로 닫힌다.** 그쪽 §3-3 확정값을 그대로 인용한다:

| 단계 | 구간 | 분 | 원형 B 도달일 |
|---:|---|---:|---:|
| 0 | `[0, 10h)` | 0 ~ 599 | 0일 |
| 1 | `[10h, 50h)` | 600 ~ 2,999 | 12.0일 |
| 2 | `[50h, 100h)` | 3,000 ~ 5,999 | 60.0일 |
| 3 (마스터) | `[100h, ∞)` | 6,000 ~ | 120.0일 |

> ### ★ 「3단계」의 뜻이 **상태 4개 / 진화 사건 3회**로 확정됐다(S4)
> 사용자가 쓴 네 숫자(0 · 10 h · 50 h · 100 h)를 하나도 안 버리는 읽기다.
> **내 1차 계약이 `StageCount = 3`이라고 적었던 것을 `StageCount = 4`(상태 수)로 정정한다.**
> `CostumeManifestSO.stageShapes`의 길이도 **4**다(0번 = 기본).
> ⇒ **조형 상태가 `8팩 × 4 = 32`개다**(3상태 대비 +8). 그 공수는
> `design-equipment`/`design-motion` 축이고 **나는 지시하지 않는다 — 리더 경유.**

**단일 출처**: `Core/CostumeEvolutionRules.cs`(신규).
```csharp
public const int StageCount = 4;                      // 상태 0..3
public static int StageOf(int focusMinutes);          // 반열린 구간 [시작, 끝)
public static int NextBoundaryMinutes(int stage);     // 표시용. 마스터면 -1
public const int DailySoftCapMinutes = 240;           // design-systems §5-3
```
**어디서도 숫자를 베끼지 마라.** 임계 상수를 참조하되 `StageOf`의 결과는 **손계산 상수와 대조**한다.

| `[DS-구멍]` | 상태 |
|---|---|
| ~~임계 초~~ | ★ **닫힘** — §3-3 표 |
| ~~코스튬 최소 세션 길이~~ | ★ **철회** — 8-2절 참고(몰입기는 어떤 길이에서도 존재하므로 하한이 필요 없다) |
| 각 단계의 조형 차이(상태 32개) | 열림 — `design-equipment` / `design-art` / `design-motion` |
| 8-2절 세 상수(0.20 / 60 s / 300 s / 0.40) | 열림 — `design-systems` 확인 대상 |

### 7-2. U-1 — **내가 틀렸고 `design-systems`가 반증했다**

내가 지난 라운드에 올린 U-1: *"진화가 「마지막 목표」가 되려면 임계가 재화 곡선보다 뒤여야 하는데
현재 배치는 앞섬."* **숫자로는 정확히 반대였다** (R26 §2, 원형 B 실측):

```
동전 완주 35.1일   <   레벨 천장 90일   <   100h 마스터 120일
```
⇒ **임계가 재화 곡선보다 «뒤»에 있다.** U-1은 **반증됐고, 임계도 재화 곡선도 옮기지 않는다.**

**그래도 살아남는 구조 규칙 하나는 남긴다(내 소관)**:

> ### ★★ 규칙 C-7 — **진화는 재화 축과 연결되지 않는다**
> `Core/CostumeEvolutionRules.cs`는 `CurrencyModel`을 **참조하지 않는다.**
> 코스튬 단계는 **동전으로 앞당길 수 없다.** 그렇지 않으면 *"돈으로 살 수 있는 것 중에 그냥
> 플레이해서 얻는 것보다 센 것은 없다"*(8회차 §36-2, `product-strategy`가 X-2로 지킨 문장)가
> **연출 축에서 뚫린다.**
> 감사 1건: `CostumeEvolutionRules.cs` 소스에 `CurrencyModel`/`coinBalance` 참조 **0건**(존재 대조 동반).

### 7-3. 승급 이벤트 — **세션 종료에서만**

> ### ★★ 규칙 C-6
> **단계는 세션 시작에 래치되고, 승급은 세션 종료에 판정된다. 세션 중에 안 바뀐다.**

이유 셋:
1. **6-3 ②를 지키기 위해서다.** 중간에 단계가 바뀌면 프롭을 다시 구워야 하고 «1회만 그린다»가 깨진다.
2. 몰입 중에 화면이 갑자기 변하면 그건 «집중 보조»가 아니라 **방해**다.
3. 판정 지점이 하나면 이벤트도 하나다.

```
세션 시작:  stageLatched = CostumeEvolutionRules.StageOf(model.MinutesOf(key));
            startKey     = CostumeResolver.Resolve();          // 4-4절 「시작 ∧ 종료」의 앞쪽

세션 종료(M-21 세 경로 전부, PayXxx와 같은 자리 = IsSessionActive=false 「앞」):
    if (CostumeResolver.Resolve() != startKey) return;          // 갈아입었으면 안 쌓는다
    int add = min(그 경로가 쓰는 분, DailySoftCapMinutes - MinutesToday);
    if (add <= 0) return;
    model.Add(startKey, add);  MinutesToday += add;
    int now = CostumeEvolutionRules.StageOf(model.MinutesOf(startKey));
    if (now > stageLatched) StickmanEventBus.RaiseCostumeStageAdvanced(startKey, now);
```

- **「그 경로가 쓰는 분」은 새로 세지 않는다** — 완주는 명목 `SessionDurationSeconds`, 취소/긴급정지는
  `SessionDurationSeconds − RemainingSeconds`. **동전 지급이 이미 쓰는 그 값**이다(R26 §4-1 실측).
- **네 번째 종료 경로를 만들지 마라** — `FocusWatchDirector`가 이미 못박은 계약이고,
  누적도 **같은 두 함수 안**에서 일어난다.
- **승급 순간의 연출**: 완주 포즈(`FocusComplete`) 위에 얹는 **한 줄 대사 + 다음 세션부터 새 외형**.
  ★ 문안·박자는 `design-narrative`·`design-motion` 소관. 나는 **자리만** 만든다
  (원칙 1 — 대사는 이 이벤트가 확정된 **뒤** 그 사실로부터 파생한다).

### 7-4. 「입은 채」의 판정 — **시작 ∧ 종료. 세션 중 샘플링 없음**

**내 1차안(«몰입기 초만 센다» + 착용 변경 시 그 자리에서 대상 전환)을 철회한다.**
`design-systems` §4-4가 네 안을 표로 비교해 **시작 ∧ 종료**를 골랐고, 근거가 더 강하다:

| 안 | 어뷰징 | 정상 비용 |
|---|---|---|
| 종료 시점만 | 마지막 1초에 갈아입어 몰아주기 | 0 |
| 시작 시점만 | 시작만 하고 벗어도 쌓임 | 0 |
| ★ **시작 ∧ 종료** | **이득 0** | **0 — 인스턴스 필드 1개(문자열)** |
| 세션 중 샘플링(내 1차안) | 이득 0 | ★ **런타임 누적기 필요** + 세션이 프로세스를 넘으면 사라진다 |

⇒ **런타임 누적기를 만들지 마라.** 시작 시점 키는 `FocusWatchDirector`의 **인스턴스 필드 1개**다.
세이브에 안 넣는다 — 세션은 프로세스를 넘어 살지 않는다.

**그리고 이 결정이 8-3절의 「몰입기 하한」을 없앤다**: 누적이 «세션 전체»를 세므로, 코스튬이 화면에
한 번도 안 뜨는 구간이 있으면 «일어나지 않은 일을 센다»가 된다(R26 §4-6이 D-3에서 쓴 그 논증).
⇒ **몰입기는 어떤 세션 길이에서도 존재해야 한다.** 8-2절 표가 그것을 이미 만족한다
(최소 세션 60초에서도 몰입기 12초). **최소 길이 하한을 두지 않는 이유가 이것이다.**

### 7-5. 정보창 표시 — `design-systems`가 **그릴 수 있음을 먼저 증명했다**

R26 §7-1 실측: 그릴 수 있는 폭 234pt에서 **통짜 0→100h 막대는 25분 완주 1회에 0.97pt 움직인다** =
**1pt 미만이라 안 보인다**(`UX_WIDGETS` R3-2-3의 링이 당한 것과 같은 형태).
⇒ **막대를 쓸 거면 반드시 «다음 경계까지» 구간별.** 최악 구간(50h→100h)도 1.95pt로 살아난다.

`Interaction/CharacterInfoWindow.Stats.cs`의 세트 패널(`:649` `RefreshSetPanel`) **옆**에 붙인다.
★ **레이아웃·문안은 `ux-designer` 소관.** 내가 넘기는 것은 **읽을 값 3개**뿐:
```
CostumeProgressModel.MinutesOf(key)
CostumeEvolutionRules.StageOf(minutes)
CostumeEvolutionRules.NextBoundaryMinutes(stage)     // 마스터면 -1
```
표시 형식(“12시간 30분” + “37.4%” 내림)은 **R26 §7-2가 확정**했고 그쪽이 정본이다.

## 8. 25분 세션 내 3구간

### 8-1. 단일 창구 — `FocusWatchDirector`가 이미 다 들고 있다 [실측]

```
Interaction/FocusWatchDirector.cs:55   public bool  IsSessionActive        { get; private set; }
Interaction/FocusWatchDirector.cs:56   public float RemainingSeconds       { get; private set; }
Interaction/FocusWatchDirector.cs:98   public float SessionDurationSeconds { get; private set; }
```
경과 = `SessionDurationSeconds − RemainingSeconds`. **새 타이머를 만들지 마라** —
그 파일이 이미 *"두 곳에서 같은 시간을 세면 반드시 어긋난다"*(`GraffitiRenderer.cs:250` 주석)를 못박았고,
`FocusWatchDirector`는 세션 시간의 **유일한 생산자**다.

**추가할 것: 프로퍼티 1개 + 순수 함수 파일 1개.**

```csharp
// Interaction/FocusWatchDirector.cs
public FocusSessionPhase CurrentPhase => IsSessionActive
    ? FocusSessionPhases.Of(SessionDurationSeconds, SessionDurationSeconds - RemainingSeconds)
    : FocusSessionPhase.None;
```

```csharp
// Core/FocusSessionPhase.cs (신규, 순수 static — MonoBehaviour 없이 EditMode에서 잰다)
public enum FocusSessionPhase { None = 0, Adapt = 1, Immersion = 2, Limit = 3 }

public static class FocusSessionPhases
{
    public const float NominalFraction  = 0.20f;  // 25분에서 5분 (사용자 요구)
    public const float MinEdgeSeconds   = 60f;    // 짧은 세션에서 구간이 사라지지 않게
    public const float MaxEdgeSeconds   = 300f;   // 긴 세션에서 적응기가 늘어지지 않게
    public const float MaxEdgeFraction  = 0.40f;  // 두 가장자리가 세션을 다 먹지 않게(합 ≤ 0.8D)

    /// <summary>가장자리(적응기 = 한계 구간) 길이(초).</summary>
    public static float EdgeSeconds(float durationSeconds)
        => Mathf.Min(
             Mathf.Clamp(NominalFraction * durationSeconds, MinEdgeSeconds, MaxEdgeSeconds),
             MaxEdgeFraction * durationSeconds);

    public static FocusSessionPhase Of(float durationSeconds, float elapsedSeconds) { ... }
}
```

### 8-2. 자유시간(1~60분)에서 어떻게 스케일되는가 — **검산표**

**25분 고정이 아니다.** 이 저장소는 이미 **뽀모도로 다이얼 1~60분**을 출하했고
(`MinimumSessionSeconds = 60`, M-20), 25분만 맞추면 나머지 59개 값에서 구간이 깨진다.

| 세션 D | 적응기 | 몰입기 | 한계 | 검산 합 | 비고 |
|---:|---:|---:|---:|---:|---|
| 60 s (최소) | 24.0 | **12.0** | 24.0 | 60.0 | ★ 몰입기가 **0이 아니다** — 7-4절이 요구하는 성질 |
| 120 s | 48.0 | 24.0 | 48.0 | 120.0 | ″ |
| 150 s | 60.0 | 30.0 | 60.0 | 150.0 | 두 갈래가 만나는 지점(연속) |
| 180 s | 60.0 | 60.0 | 60.0 | 180.0 | — |
| 300 s (5분) | 60.0 | 180.0 | 60.0 | 300.0 | `MinEdgeSeconds`가 지배 |
| 900 s (15분) | 180.0 | 540.0 | 180.0 | 900.0 | 20 % / 60 % / 20 % |
| **1500 s (25분)** | **300.0** | **900.0** | **300.0** | 1500.0 | ★ **사용자 요구와 정확히 일치**(0~5 / 5~20 / 20~25분) |
| 3000 s (50분) | 300.0 | 2400.0 | 300.0 | 3000.0 | `MaxEdgeSeconds`가 지배 |
| 3600 s (60분) | 300.0 | 3000.0 | 300.0 | 3600.0 | ″ |

**성질 검증 [계산]**: `EdgeSeconds(D)`는 구간별로 `0.4D → 60 → 0.2D → 300`이고 **경계에서 연속·단조 비감소**다
(D=150에서 0.4×150 = 60, D=300에서 0.2×300 = 60, D=1500에서 0.2×1500 = 300). **꺾이지만 안 튄다.**

> ### ★ 왜 순수 비율(20/60/20)만 쓰지 않는가
> 60분 세션에서 적응기가 **12분**이 된다. 「자리 잡는 데 12분」은 연출로도 사실로도 거짓이다.
> 반대로 절대값(5분 고정)만 쓰면 **1분 세션에서 적응기가 세션보다 길다**. 그래서 셋을 겹쳐 물린다.
> **세 상수 전부 `[DS-구멍 2]`로 `design-systems` 확인 대상**이지만, **구조(세 상수를 겹쳐 문다)는 내가 확정한다.**

### 8-3. 구간 → 층 매핑 (기본안, `design-motion`이 뒤집을 수 있다)

| 구간 | 포즈 층 | 프롭 | 배회 | 근거 |
|---|---|---|---|---|
| **적응기** | 기존 관망 자세 + 제스처 4종 | 없음 | 평소(0.40) | 아직 자리를 잡는 중. 코스튬이 아직 안 나와야 «들어간다»가 읽힌다 |
| **몰입기** | ★ **코스튬 LFVS** | ★ **있음(고정)** | ★ **0** | 프롭과 캐릭터가 한 장면. 4-3절 |
| **한계** | 기존 관망 자세 + 제스처(느려진 변주) | 없음(철거) | 평소(0.40) | «지친다»의 표현. `design-motion` 소관 |

**이 매핑이 구조적으로 하는 일**: LFVS와 앰비언트 제스처가 **시간축에서 겹치지 않는다.**
그래서 4-3절의 `!gesturing` 가드는 «경계 프레임 보험»일 뿐이고, 정상 동작에서는 다툴 일이 없다.

### 8-4. 구간 전이 이벤트

```csharp
StickmanEventBus.FocusSessionPhaseChanged;   // Action<FocusSessionPhase, FocusSessionPhase>
```
**발행자는 `FocusWatchDirector.Update` 한 곳뿐이다.** `RemainingSeconds`를 이미 감산하는 그 자리
(`:325`)에서 직전 구간과 비교해 바뀔 때만 쏜다. 구독자: `CostumePropRenderer`(구축/철거) ·
`StickmanBlackboard`(캐시 무효화) · `design-narrative`의 대사 훅.

---

## 9. 착수 순서와 파일 분할표

### 9-1. Phase 순서 — **왜 이 순서인가(막히는 이유)**

| Phase | 무엇 | **앞을 안 하면 무엇이 막히는가** |
|---|---|---|
| **P0** | 리더 판정 3건(10절) + `[DS-구멍 1·2]` | 게이트 규칙이 안 정해지면 P1의 `CostumeResolver`가 **다시 짜여야** 한다. 세이브 v12를 먼저 굽고 규칙이 뒤집히면 **v13이 필요**하고 하위 호환 테스트가 2배가 된다 |
| **P1** | 키 공간 · `CostumeManifestSO`/`CostumeCatalog` · `CostumeResolver` · `CostumeEntitlement` · **세이브 v12** | 해석기가 없으면 P2의 프롭이 «어느 프롭인가»를 못 묻는다. **v12는 한 라운드에 한 번에** 넣는다 — 나눌 때마다 다운그레이드 창이 한 번씩 열린다(v10이 세운 판단) |
| **P2** | `FocusSessionPhase` + `FocusWatchDirector.CurrentPhase` + 구간 이벤트 | 구간이 없으면 P3의 프롭이 **언제 뜰지**를 모른다. 그리고 P4의 적립 대상(몰입기 초)이 정의되지 않는다 |
| **P3** | `CostumePropRenderer` + 배회 사다리 1칸 | 프롭이 없으면 P4의 LFVS가 «허공에서 곡괭이질»이 된다(원칙 1) |
| **P4** | `CostumeKeyposeTableSO` + `ApplyCostumeKeyposeImmediate` + `TickCostumeKeypose` 배선 | ← 여기까지가 **MVP `costume.office`**. L-1~L-5 합격 필수 |
| **P5** | 진화 3단계 배선 + 정보창 표시 | v12 필드는 P1에 이미 있다. 여기서 **읽고 쓰기만** 붙인다 |
| **P6** | ★ **HEAD/EYES/BACK 조형 에셋화**(M-23) → `pack.cyber`/`mine`/`arcane` 6종씩 | **이걸 안 하면 유료 코스튬을 프로덕션 `.cs` 0줄로 못 만든다.** 그리고 R-1(2-2절)이 유료 코스튬 1개를 요구하므로 **출하 자체가 막힌다** |
| **P7** | 판매 배선(스팀 DLC · 스토어 문안 S-6) | `product-strategy`·`marketing` 소관 |

> ### ★ 순서에서 가장 흔한 오해 두 개를 미리 막는다
> 1. **«P6을 먼저 하자»** — 안 된다. 게이트 규칙(P0)이 확정되기 전에 팩 아이템을 굽고 나면
>    `costume.cyber`가 기본 코호트로 갈지 팩 코호트로 갈지에 따라 **코호트 번호를 다시 배정**해야 하고,
>    코호트는 「출시 후 절대 못 바꾸는 값」이다(`StickPackManifestSO` 툴팁).
> 2. **«세이브 v12를 P5로 미루자»** — 안 된다. P4까지 돌려 보려면 누적을 어딘가 적어야 하고,
>    임시로 적으면 그 임시 형태가 디스크에 앉는다. 그리고 **문을 닫는 것은 커밋이 아니라 빌드다**
>    (`CharacterSaveStore` v10 주석의 그 문장).

### 9-2. 파일 분할표 — **이 표의 한 행은 한 사람이 통째로 갖는다**

동시 진행 시 리더는 **행 단위로** 배정한다. 행이 겹치면 동시 진행 불가다.

| Phase | 담당 | **이 사람만 만지는 파일** | 읽기만 |
|---|---|---|---|
| **P1-a** | `coder-systems` | `Core/CostumeManifestSO.cs`(신) · `Core/CostumeCatalog.cs`(신) · `Core/CostumeResolver.cs`(신) · `Core/CostumeEntitlement.cs`(신) | `ItemCatalog` · `PackRegistry` · `PackEntitlement` · `EquipmentStatRules` |
| **P1-b** | `coder-systems` (P1-a와 **직렬**) | `Core/CostumeProgressModel.cs`(신) · `Core/CharacterSaveStore.cs` · `Tests/EditMode/EquipmentMigrationTests.cs` | `CurrencyModel` |
| **P2** | `coder` | `Core/FocusSessionPhase.cs`(신) · `Interaction/FocusWatchDirector.cs` · `Core/StickmanEventBus.cs`(이벤트 1개) | — |
| **P3** | `coder` (P2 이후) | `Interaction/CostumePropRenderer.cs`(신) · `States/AutoWanderController.cs` · `Core/StickConfig.cs` + `Data/DefaultStickConfig.asset` | `SuspendedOverlayGate` · `GraffitiRenderer` |
| **P4** | `coder` (P3 이후) | `Core/CostumeKeyposeTableSO.cs`(신) · `States/StickmanPoseAnimator.cs` · `States/StickmanBlackboard.cs` | `LimbCurveRenderer` · `FramePacing` |
| **P5-a** | `coder-systems` | `Core/CostumeEvolutionRules.cs`(신) | `CostumeProgressModel` |
| **P5-b** | `coder-ui` | `Interaction/CharacterInfoWindow.Stats.cs` | `CostumeEvolutionRules` |
| **P6** | `coder` + `design-equipment` | `Interaction/AccessoryShapeBuilder*.cs` · `Resources/Items/*.asset` | — |
| **테스트** | `test-engineer` | `Tests/EditMode/Costume*.cs`(신규 전부) | 전부 |

> ### ★ 충돌 경보 — 리더가 반드시 볼 것
> - **`Core/StickConfig.cs` + `Data/DefaultStickConfig.asset`은 항상 한 묶음이다.**
>   애셋을 안 고치면 스위치가 꺼진 채 출하된다(이 저장소 거짓 통과 #9).
> - **`States/StickmanBlackboard.cs`(3,511줄)와 `Core/CharacterSaveStore.cs`(1,522줄)는
>   다른 라운드가 자주 만지는 파일이다.** P1-b와 P4를 **동시에 돌리지 마라.**
> - **`Core/StickmanEventBus.cs`는 P2와 P5-a가 둘 다 이벤트를 더하고 싶어 한다.**
>   `RaiseCostumeStageAdvanced`를 **P2 라운드가 함께 넣어 둬라**(쓰는 곳은 나중이어도).

### 9-3. 각 Phase의 «끝났다» 판정

| Phase | 통과 조건 (전부 관측 가능해야 한다) |
|---|---|
| P1 | 합성 9번째 코스튬 매니페스트로 `CostumeCatalog`가 코스튬을 **싣는다**(코드 0줄) · v11 하위 호환 테스트 초록 · v12 왕복 초록 · 게이트 단일 창구 감사 초록(존재 대조 포함) |
| P2 | 표 8-2의 **9행 전부**가 EditMode에서 재현 · 구간 이벤트가 한 세션에 **정확히 2회** |
| P3 | P-1~P-6 전부. 특히 **P-6 양성 대조** |
| P4 | L-1~L-5 전부. 특히 **L-3(`Still` 도달 관측)** — 이것이 요구 4번의 유일한 직접 증거 |
| P5 | 100 h 지점 정밀도 테스트 초록 · 임계를 내려 잡은 픽스처에서 단계가 **안 내려가는가**(래칫) |
| P6 | 신규 HEAD 아이템 1종을 **에셋만으로** 추가해 몸에 그려지는가 |

---

## 10. ★ 되돌릴 수 없는 결정 — **리더 판정 대기**

| # | 결정 | 왜 되돌릴 수 없나 | 내 권고 |
|---|---|---|---|
| **I-1** | 게이트 = **코호트 합의 × 개방**(규칙 C-1) | 1차 출시가 **Windows 단독**이라 **출시분에서 굳는다.** 나중에 느슨하게 바꾸면 산 사람의 연출이 무료 유저에게 열리고, 조이면 **이미 보던 것이 사라진다** | **채택.** 1-4절 표 |
| **I-2** | 코스튬 키 `costume.*` 역DNS + **`packId`와 별개** | 세이브에 문자열로 앉는다. 바꾸면 누적 100시간이 **다른 코스튬 것이 된다** | 채택 |
| **I-3** | 세이브 **v12 · `CostumeFocusRecord[]`(레코드 배열 1개) + `int costumeFocusMinutesToday` · 단위 「분」** | 디스크 형태. 한 번 빌드가 돌면 되돌리려면 v13 + 하위 호환 2벌 | 채택. **`design-systems` §6-1의 병렬 배열 2개와 갈린다 — 13절 D-1** |
| **I-4** | **새 `StickmanStateId` 없음 · 새 `WanderAmbientMotion` 없음** | `StickmanStateId` 정수는 **DLC 매니페스트에 나가는 값**이다(`:92`). 나중에 끼워 넣으면 출하된 팩의 배선이 밀린다 | 채택 |
| **I-5** | **C층 첫 소비자를 `Core/CostumeEntitlement.cs` 1파일에 가둔다** | 첫 소비자가 어법을 정한다. 두 번째부터는 첫 번째를 베낀다 | 채택 |
| **I-6** | 프롭 앵커는 **`ArcheryGroundWorldY` 어법 재사용**(신규 「GroundLine」 개념 만들지 않음) | 좌표계 결정 | 채택. **지난 라운드 내 I-6을 철회한 것이다** |
| **I-7** | `CostumeManifestSO` **신규 SO** (팩 매니페스트 v3 승격 안 함) | 팩 스키마 판은 **옛 앱이 새 팩을 반쯤 읽는** 문제를 지킨다. 한 번 v3로 올리면 v2 앱이 신규 팩을 거부한다 | 채택. 2-3절 근거 3건 |
| **I-8** | **릴리즈 게이트 R-1** — 무료 오피스 코스튬은 유료 코스튬 1개와 **함께** 출하 | 한 번 «무료가 더 풍성한» 빌드가 나가면 P7(산 다음에 화내는 사람)이 터진다 | 채택 |
| **I-9** | **`AccessorySurface.Stage`(비트 8) 신설 여부** | 조각 계약 v2→v3. 팩 스키마와 같은 등급 | ★ **보류 권고.** 7-1절대로 단계 조형이 `CostumeManifestSO.stageShapes`로 들어가면 **비트가 필요 없다.** `design-equipment`가 「단계별로 몸 조각 자체가 갈린다」를 요구할 때만 연다. **지난 라운드 내 I-5를 여기서 보류로 내린다** |

### 10-2. 사용자 확인이 필요한 것 — **1건**

| # | 질문 | 왜 지금 물어야 하나 |
|---|---|---|
| **X-4** | **MVP를 `costume.office`(무료·에셋 0개)로 바꿔도 되는가.** 대신 그 빌드는 **유료 코스튬 1개가 붙기 전까지 공개 출하하지 않는다**(R-1) | 지난 라운드에 사용자가 본 계획은 «MVP = 사이버펑크»였다. P-11 채택이 그 전제를 뒤집었고(2-2절), **바뀐 것을 말하지 않으면 사용자는 옛 계획으로 안다** |

---

## 11. 미확인 — 메우지 않았다

| # | 무엇 | 무엇을 재야 하는가 |
|---|---|---|
| U-A | 몰입기 동안 걷기 0이 «붙박이»로 읽히는가 | `persona-immersion` + 실기 25분 세션 1회. **오프라인 렌더러로 판정하지 마라** |
| U-B | 3스텝/초가 «저프레임 감성»으로 읽히는가, 「끊긴다」로 읽히는가 | `design-motion` 눈판정. 실제 빌드 캡처만 |
| U-C | LFVS의 실제 CPU 절감폭 | L-1의 **기준선**(코스튬 OFF 몰입기의 초당 재굽기 수)을 아직 안 쟀다. 관망 자세에 「미세 생명감」이 있어 **0이 아닐 것**이다 |
| U-D | Windows에서 `Still` 등급 진입률 | macOS 실측만 있다. Windows는 `baseVSyncCount = 0` 경로라 `BuildPlan` 분기가 다르다(보는 사람 있는 등급은 같지만 **확인 안 했다**) |
| U-E | `pack.cyber`/`mine`/`arcane` 아이템 12종의 실제 제작 공수 | `design-equipment`. P6 규모 산정이 여기 달렸다. **나는 못 센다** |
| U-F | 승급 순간을 세션 «중»에 보여 달라는 요구가 나올지 | 규칙 C-6은 그 요구가 오면 **다시 열어야 한다**(프롭 재굽기 1회가 필요하다). 열 때의 비용은 6-3 ②의 한 번 위반 |

---

## 12. 플랫폼 영향

- **Windows 영향: 없음(직접) / 별도 배정 필요(간접 1건).**
  이 라운드는 프로덕션 `.cs` **0줄** · `.asset` **0줄** · `ProjectSettings` **0줄**이다.
  설계 자체도 **전부 플랫폼 중립 위치**에 놓았다 — `Core/`(해석·게이트·구간·진화) ·
  `States/`(포즈) · `Interaction/`(프롭). `Platform/` 아래에 **한 줄도 넣지 않는다.**
  (CLAUDE.md: *"정책 판정 로직은 플랫폼 중립 위치에 두고, 플랫폼 전용 코드는 사실 조회만"* —
  `FullscreenSuspendPolicy.cs`가 `Platform/MacOS/`에 있어 Windows가 물리적으로 못 부른 그 사고를 반복하지 않는다.)
  - ★ **별도 배정 1건 — U-D**: `Still` 등급 진입 관측(L-3)은 **1차 출시 플랫폼인 Windows에서도** 재야 한다.
    `BuildPlan`의 「보는 사람 있는 등급」 경로는 2026-09-01부터 플랫폼 분기가 없지만
    [실측: `ViewerPresence.cs` `viewerPresent` 분기], **그 사실을 Windows 실기로 확인한 기록이 없다.**
  - ★ **I-1이 Windows 1.0 출시분에서 굳는다**(`product-strategy` 9회차 §45 신고와 같은 사실).

- **macOS 영향: 없음(직접).** 위와 같다. 이 개발 머신이 macOS라 P1~P5의 EditMode 검증은 여기서 돈다.
  단 **활성 빌드 타깃이 지금 `StandaloneOSX`**이므로(2026-09-07 `dev-platform` 라운드 부수효과, `Tasklist.md` 기록),
  Windows 전용 파일을 건드리는 Phase가 생기면 `Tools/CrossCompile/xcheck.sh win`을 **반드시** 함께 돌린다.
  이 문서의 어떤 Phase도 Windows 전용 파일을 지목하지 않는다.

- **iPad/iPhone 영향: 없음(설계상 호환).** 프롭 렌더러는 `LineRenderer`뿐이고 스크린샷 백드롭 모드에서도
  같은 카메라 위에 그려진다. **입력·창 열거에 의존하는 부분이 하나도 없다** — 유일한 외부 의존은
  `FramePacing.LastPresence`인데 그건 이미 「관측 실패 = 평소대로」로 안전하게 떨어진다.

---

## 13. ★★ 인계 계약 — `design-systems` R26과의 맞물림 (같은 라운드 병렬 산출)

> 인계 계약(`docs/TEAM.md` §3): *"리더에게 올릴 때 「상대 팀 산출물의 어느 지점과 어떻게 맞물리는가」를 반드시 적는다."*
> 두 문서는 **서로를 안 보고 같은 라운드에 쓰였다.** 아래가 대조 결과다.

### 13-1. 수렴한 것 — **교차 검증으로 취급한다**

| 항목 | 내 근거 | R26 근거 | 판정 |
|---|---|---|---|
| `float` 초 누적은 100 h에서 **+87.5 %** 틀어진다 | numpy `float32` 시뮬레이션(3-2절) | 같은 값 독립 도출(§4-3) | ★ **조율 없이 수렴. 확정** |
| 종료 경로는 **정확히 3개**, 지급은 2함수 | M-21 | §4-1 | ★ 확정 |
| 세션 시간의 단일 창구 = `FocusWatchDirector` | M-19 | §1 앵커표 | ★ 확정 |
| 세트 4/4 완성이 진입 조건 | C-1 1~2단계 | §4-5 | ★ 확정 |
| 미소유 팩에는 **안 쌓는다** | C-1(`Resolve()`가 null이면 적립 자체가 없다) | §4-6 D-3 | ★ **같은 규칙. C-1이 그것을 구조로 만든다** |

### 13-2. ★ 갈라진 것 — **리더 판정 2건**

| # | 나 | `design-systems` R26 | 내 권고와 근거 |
|---|---|---|---|
| **D-1** | 세이브 = **레코드 배열 1개** `CostumeFocusRecord[]` | **병렬 배열 2개** `costumeFocusThemes[]` + `costumeFocusMinutes[]` (§6-1) | ★ **레코드 배열.** 스키마 «형태»는 내 축이다(되돌릴 수 없는 결정). 근거: 병렬 배열은 §6-2가 스스로 **「길이 불일치 → 둘 다 버린다」 위생 규칙**을 세워야 했고 그 규칙의 존재가 곧 실패 모드의 증거다. 레코드 배열은 그 분기와 그 테스트를 **삭제**한다. 저장소 선례(`ItemGraceBaseline[]`)도 레코드 형태다. **그쪽이 승계한 것은 내 «옛» 제안이므로, 철회의 책임도 내게 있다** |
| **D-2** | 키 = **`costume.*`**(코스튬 매니페스트가 소유) | 키 = **`ItemCatalog.Theme*` 문자열**(`"mil"`·`"office"`) (§6-1) | ★★ **`costume.*`. 이건 취향이 아니라 실현 가능성이다.** M-1 실측: **팩 아이템은 테마 문자열을 구조적으로 못 받는다**(`ItemCatalog.cs:1426`). 그러므로 R26 §4-6이 쓴 누적 게이트 *"`IsSetComplete(theme)` ∧ 엔타이틀먼트(theme)"*는 **팩 코스튬에 대해 영원히 false다** — `pack.cyber`·`pack.mine`·`pack.arcane`이 **한 분도 못 쌓는다.** 이건 1-1절의 B-1 함정과 같은 형태(화면상 「기능 미구현」과 구별 안 됨)다. **키를 `costumeKey`로 바꾸면 R26의 다른 모든 판정(단위 분 · S4 · 240분 캡 · 표시 형식 · §6-2 위생 4줄)은 한 글자도 안 바뀐다.** 기계적 치환이다 |

> ### ★ D-2에 대한 정직한 부기 — **R26이 내 P-11 관련 판정을 강화해 줬다**
> R26 §4-5: *"P-11의 최악 사례는 cyber가 아니라 `mil`이다 — 1일차 무상 4종이 그대로 mil 4/4라
> **동전 0원 · 0일**이다."* 【실】 맞다. 1-1절의 논거가 그만큼 더 강해진다.
> ⇒ **1-2절 규칙 C-1의 3a 갈래(기본 코호트 테마)에 `mil`을 넣지 마라.**
> `CostumeCatalog`에 `costume.mil` 매니페스트를 만들지 않는 것으로 자동 충족된다
> (오늘 기본 코호트 코스튬은 `costume.office` 하나뿐 — 2-2절 표).

### 13-3. R26이 새로 얹은 것 — **내 계약에 흡수 완료**

| R26 판정 | 내 문서 어디에 들어갔나 |
|---|---|
| 일일 소프트캡 **240분/일**(§5-3) | 3-1절 `costumeFocusMinutesToday` 필드 · 7-3절 승급 의사코드의 `min(...)` |
| 「1분 세션 반복」에 방어 **불필요**(§5-1) | 별도 코드 0줄 — 7-3절이 `floor(경과/60)`을 쓰므로 저절로 성립 |
| 일시정지(전체화면) 중 **안 쌓인다**(§4-2) | 추가 코드 0줄 — `FocusWatchDirector.Update`가 `IsSuspended`에서 조기 반환해 `RemainingSeconds`가 안 준다 |
| 통짜 100 h 막대는 **0.97pt**라 안 움직인다(§7-1) | 7-5절 — 막대는 **구간별**로만 |
| S4(상태 4 · 진화 3회) | 7-1절 `StageCount = 4`, `stageShapes` 길이 4 |
| `stageReached` high-water **불필요**(§3-3) | 3-1절 — 필드 삭제. **되살릴 조건 1개**를 같은 자리에 명시 |

### 13-4. 다른 팀으로 나가는 인계 (리더 경유. **나는 지시하지 않는다**)

| 받는 팀 | 무엇 | 어디 |
|---|---|---|
| `design-equipment` · `design-art` | ★ **조형 상태 32개**(8팩 × 4단계) 공수 산정. 그리고 **HEAD/EYES/BACK 조형 에셋화**(M-23)가 P6의 선결 | 2-4절 · 7-1절 |
| `design-motion` | LFVS 3 또는 5 스텝/초 눈판정 · 8-3절 구간→층 매핑 확정 · 몰입기 걷기 0의 「붙박이」 위험 | 5-2절 · 8-3절 · U-A |
| `design-narrative` | 승급 한 줄 대사(원칙 1 — **이벤트가 확정된 뒤** 파생) · 구간 전이 어조 | 7-3절 · 8-4절 |
| `ux-designer` | 정보창 코스튬 진행 줄. **막대는 구간별**(통짜는 1pt 미만) | 7-5절 |
| `product-strategy` | ★ **R-1 릴리즈 게이트**(무료 오피스 코스튬 단독 출하 금지) · **X-4**(MVP를 오피스로 바꿔도 되는가) | 2-2절 · 10-2절 |
| `test-engineer` | 감사 4건(단일 게이트 · 세이브≠엔타이틀먼트 · 재화 비참조 · 매니페스트 통로) + 양성 대조 L-5/P-6 | 1-6 · 3-5 · 7-2 · 2-3 |

## 부록 A. 이 문서가 **철회한** 내 지난 판정 4건 (정직성 규칙)

| # | 지난 라운드에 내가 적은 것 | 지금 |
|---|---|---|
| 1 | *"코스튬 = 테마 세트 완성의 두 번째 보상"* | ★ **철회.** M-1·M-2로 반증. `product-strategy` P-11이 맞았다 |
| 2 | *"MVP는 사이버펑크 — 신규 에셋 0개"* | ★ **철회.** P-11 채택 시 `pack.cyber` 에셋 0개(M-5)라 성립 안 함. **MVP는 `costume.office`** |
| 3 | *"세이브 v11 · 가변 길이 배열 2개"* | ★ **수정.** 버전은 **v12**(그 사이 XP 라운드가 11을 썼다, M-10). 배열은 **레코드 1개**(3-1절). 단위는 **분**(3-2절) |
| 4 | *"GPU가 4배"* / *"신규 기저 GroundLine"* / *"AccessorySurface.Stage = 8"* | ★ **정정 3건.** 제출 **2배**(`DefaultActiveDivisor`가 2로 바뀜) / GroundLine 대신 **`ArcheryGroundWorldY` 어법 재사용** / Stage 비트는 **보류**(I-9) |
| 5 | **U-1** *"진화 임계가 재화 곡선보다 앞선다"* | ★ **반증됨.** `design-systems` R26 §2 실측 — 120일 > 90일 > 35.1일로 **뒤에 있다**. 임계도 재화 곡선도 안 옮긴다 |
| 6 | *"몰입기 초만 적립 / 세션 중 착용 변경 시 대상 전환"* | ★ **철회.** R26 §4-4가 더 강하다 — **시작 ∧ 종료**, 런타임 누적기 0개(7-4절) |
| 7 | *"3단계 = 상태 3개"* | ★ **정정.** S4 = **상태 4개 · 진화 사건 3회**(7-1절). 조형 상태 `8팩 × 4 = 32` |

## 부록 B. 이 문서가 **새로 찾은** 것 3건

| # | 발견 | 왜 중요한가 |
|---|---|---|
| B-1 | **P-11을 글자 그대로 짜면 오늘 트리에서 전원이 아무것도 못 본다**(M-5·M-6·M-9) | 그 증상이 「기능 미구현」과 화면상 동일하다 — 이 저장소가 아홉 번 당한 형태 |
| B-2 | **`PackEntitlements.StateOf`의 프로덕션 소비자가 0건**(M-7) | 코스튬 게이트가 **C층 첫 소비자**다. 첫 소비자가 어법을 굳힌다 |
| B-3 | **장비 조형 에셋화가 24종 중 6종(NECK)뿐**(M-23) | `pack.mine`/`pack.arcane`의 HEAD/EYES/BACK 6종을 **「`.cs` 0줄」로 못 만든다.** 원칙 4의 구멍이 장비에 아직 남아 있고, **PART2가 그 구멍을 처음 밟는다** |
