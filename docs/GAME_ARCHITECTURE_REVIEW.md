# 총괄 게임 아키텍트 — 1차 전체 정합성 검토 (2026-09-02)

작성: `game-architect` (오늘 신설). 배정: 리더(Architect).
**프로덕션 `.cs` 수정 0건 / 앱 실행 0회 / 빌드 0회.** 이 문서는 판단과 설계만 담는다.

> 이 문서가 답하는 것
> ① 신규 기획 전체 ↔ 기존 구조 정합성 (충돌 전수)
> ② 되돌릴 수 없는 결정과 그 시점
> ③ 착수 순서 — "A를 안 하면 B가 어떻게 막히는가"
> ④ 모듈화(파일 분할 / 어셈블리 분리) 판단 — 실측 의존 그래프 포함

---

## 0. 이 문서를 신뢰해도 되는 근거 — 무엇을 재고 무엇을 안 잤는가

### 0-1. 읽은 것의 mtime (낡은 파일을 현재 상태로 읽는 사고 방지)

| 파일 | mtime | 비고 |
|---|---|---|
| `docs/UX_HANDOFF_REVIEW_BRASS_ARCHIVE.md` | 09-02 11:20 | 가장 새롭다 |
| `docs/UX_FLOW.md` (§45/§46/§47) | 09-02 11:18 | |
| `docs/UX_SHOP_AND_CURRENCY.md` | 09-02 11:04 | |
| `scratchpad/.../design_handoff_equipment_window/README.md` | 09-02 11:03 | 사용자 핸드오프 |
| `design/systems/ECONOMY_SPEC.md` | 09-02 10:51(디렉터리) | |
| `design/motion/2026-09-02_신규행동_모션사양.md` | 09-02 10:54(디렉터리) | |
| `design/equipment/HAT_HEADROOM_PRESCRIPTION.md` | 09-02 09:16 | |
| `Tasklist.md` §캐릭터 조형 잠금 | 09-02 (커밋 e64b61b/03e1b48) | |

### 0-2. 코드는 문서를 믿지 않고 직접 읽었다

`CharacterSaveStore.cs` / `ItemCatalog.cs` / `EquipmentModel.cs` / `AccessoryDefSO.cs` /
`AccessoryShapeBuilder.cs` / `CharacterInfoWindow.cs` / `SettingsWindow.cs` /
`StickmanEventBus.cs` / `Plugins/*.cs` / `Platform/Mobile/*.cs` / `Core/StickmanAgent.cs`
+ `Resources/Items/*.asset` 42개 전수 파싱.

> ★ **측정 시점 고지**: 이 문서의 파일 수·줄 수·의존 그래프는 **2026-09-02 11:40 작업 트리 스냅샷**이다.
> 그 시각 동시 라운드 4개가 돌고 있어 `.cs` 16개가 수정 중이었고 미추적 신규 파일도 있었다
> (`Core/SpectacleExitClassification.cs`, `Tools/ShapeDumpPC/` 등). **±수 파일 오차가 있을 수 있다** —
> 다만 이 문서의 어떤 판정도 그 오차에 뒤집히지 않는다(전부 자릿수 차이로 갈린다).

### 0-3. ★ 하지 않은 것 (= 이 문서가 보장하지 않는 것)

| 미확인 | 왜 |
|---|---|
| `items.py` ↔ 프로덕션 **거울 어긋남 13건** | 리더 보고를 그대로 인용한다. **내가 재확인하지 않았다** — `mirrordrift.py`가 `Tools/ShapeDump/build.sh`(Roslyn 컴파일 + 실행)를 호출하므로 "빌드 전 리더에게 알린다" 규칙에 걸린다 |
| 어셈블리 분리 시 깨지는 `internal` **정확한 건수** | 이름 기반 스캔은 상한만 준다(§4-3). 정확한 셈은 Roslyn 분할 컴파일 1회가 필요하고, 그건 별도 라운드다 |
| 핸드오프 HTML 프로토타입의 **렌더 픽셀** | 브라우저 없음. `ux-designer`와 같은 제약 |
| 신규 9종 모션의 **실기 검증** | 아직 구현 자체가 없다 |

---

# 1. 【임무 ①】 기획서 전체 ↔ 기존 구조 — 충돌 전수

정렬 기준: **위로 갈수록 "다른 결정을 막는 것"**. 아래로 갈수록 국소적이다.

## 1-A. ★★ 가장 큰 것 — **경제는 두 개의 서로 다른 축이고, 섞으면 둘 다 죽는다**

`ux-designer`가 "수치 충돌 6건"으로 올린 것을 **한 건 더 찾아 7건**으로 만들고,
동시에 **"6~7건의 협상"이 아니라 "축 1개의 선택"**임을 보고한다.

| # | 항목 | `ECONOMY_SPEC` (축 A) | 핸드오프 (축 B) | 비고 |
|---|---|---|---|---|
| 1 | 임계 3단계 | 6 / 12 / 18, 캡 **20** | 10 / 20 / 32, 게이지 상한 **40** | 2배 |
| 2 | 등급 주스탯 | 3 / 5 / 7 / 10 | 3 / 6 / 10 / 15 | 전설 1.5배 |
| 3 | 등급 부스탯 | **0** / 1 / 2 / 3 | 1 / 2 / 4 / 6 | |
| 4 | 가격 | 30 / 70 / 150 / 330 | 600 / 1,400 / 3,200 (전설 없음) | 약 20배 |
| 5 | 세트 완성 | 4스탯 각 **+2** | 총합 **+8** | 같은 뜻일 수도 있다(총합 8 = 4×2) — **이 한 줄은 충돌이 아닐 가능성이 높다** |
| 6 | 유예 자동 해금 | **있다** (5/15/35/80시간) | **없다** | 구조가 다르다 |
| **7** | **레벨 기본치** | `min(floor((Lv−1)/4), 5)` — **4스탯 동일**, Lv.7 = **1** | Lv.7 예시 **집중력 8 / 관찰력 6 / 매력 5 / 민첩 7** — **스탯마다 다름** | ★ 아무도 안 적은 7번째 |

### 왜 "섞을 수 없다"인가 — 산술로

**축 A는 자기 안에서 닫혀 있다.** 검산했다(세트 F 완성 @Lv.28):
```
레벨기본치 5 + 주스탯(전설) 10 + 부스탯(순환으로 들어온 전설 부) 3 + 세트 2 = 20 = 상단 캡
```
4스탯 전부 정확히 20에 착지한다 → *"기본 42종만으로 캡 도달 = DLC 재구매 압박 0"*이라는
페이투윈 차단 검산이 **성립한다.**

**축 B는 도달 가능성을 검산할 수 없다.** 핸드오프에는 **레벨 기본치 곡선이 없다**(Lv.7 값 하나뿐).
그 하나로 최대치를 짜면:
```
Lv.7 집중력 8 + 전설 주 15 + 전설 부 6 + 세트 2 = 31  <  고급 임계 32
```
**핸드오프의 예시 수치만으로는 3단계(고급)에 원리적으로 못 닿는다.** 곡선이 오면 닿겠지만
그 곡선이 없으므로 **축 B는 아직 시스템이 아니라 화면 예시다.**

> **판정 권고: 축 A를 채택하고, 핸드오프의 수치는 "프로토타입 표시값"으로 강등한다.**
> 근거 3건 — (a) 축 A만 도달 가능성·페이투윈 차단·완주일수를 숫자로 증명한다,
> (b) 축 B의 가격 600/1,400/3,200은 축 A의 획득량(기준 유저 64동전/일)에서 **영웅 하나에 50일**이 된다,
> (c) 축 B에는 전설 가격이 아예 없다.
> **채택해도 화면은 거의 안 바뀐다** — 게이지 상한 40→20, 눈금 10/20/32→6/12/18, 가격 자릿수 4→3.
> 카드 레이아웃은 `UX_SHOP_AND_CURRENCY` §14-5가 이미 축 A로 폭 검산을 마쳤다.

### 그리고 6번(유예 자동 해금)만은 **화면 구조를 바꾼다**

`ux-designer`가 지적한 대로 카드 메타 칸의 내용이 갈린다. 여기에 **코드 근거를 하나 더 붙인다** —
`ItemCatalog.IsOwned`의 주석이 이미 같은 원칙을 명문화했다:

> *"더 아래(착용 시점)에서 우회하면 'Lv.20에 열림'이라 적힌 카드가 눌리는 거짓말이 된다."*

핸드오프의 `동전 3,200으로 해금`은 **상점 층에서 그 우회를 다시 만드는 것**이다.
`ItemCatalog.cs:233`이 지키던 불변식과 정면으로 부딪힌다.
→ **유예 자동 해금 채택을 권고한다.** (사용자 답변 대기 항목과 연결 — §5-1)

---

## 1-B. ★★ 아무도 보지 않은 구멍 — **핸드오프를 그대로 넣으면 「외형」 탭이 사라진다**

`ux-designer`의 §5 대조표 11행에 **`외형` 탭 행이 없다.** 핸드오프 README에도 비교가 없다.
그런데 두 문서가 말하는 「외형」은 **완전히 다른 화면**이다.

| | 현행 코드 (실측) | `UX_SHOP_AND_CURRENCY` §46 | **핸드오프** |
|---|---|---|---|
| 외형 탭 내용 | **카드 그리드 3섹션** — 머리 6 / 이펙트 6 / 펫 6 | 동일 유지 | **설정 페이지** — 크기 슬라이더 · 윤곽선 강조 · 저전력 30/45/60fps · 소품 토글 2 · 파티클 색 |
| 근거 | `SectionSlot(Tab.Appearance, …)` → `EquipmentModel.IsAppearanceSlot`, `FirstAppearanceSlot = Hair` | §46-1 | README "Screen: 외형 탭" |

**이 교체가 조용히 부수는 것 3건 (전부 실측):**

1. **아이템 18종이 갈 곳을 잃는다.** `Resources/Items/*.asset` 42개 중
   `look.hair.*` 6 + `look.fx.*` 6 + `look.pet.*` 6 = **18개가 이미 출하돼 있고 요구 레벨이 붙어 있다**
   (`look.pet.snail` Lv.30 = 카탈로그의 마지막 아이템). 핸드오프 4탭 어디에도 이 18종의 자리가 없다.
   보관함 탭은 "보유 아이템"이라 표시는 되겠지만 **착용 UI가 없어진다.**
2. **설정이 두 집을 갖게 된다.** `SettingsWindow.cs`가 이미:
   - `[캐릭터]` 탭에 **캐릭터 크기 슬라이더**를 갖고 있고, 코드 주석이 *"크기 조정은 아래 [캐릭터] 탭의
     '캐릭터 크기' 슬라이더 하나로 **일원화**된다"*라고 못박았다(`SettingsWindow.cs:1051`, 구석 패널 삭제 라운드).
   - `[접근성 · 성능]` 탭이 **"윤곽선 강조 · 애니메이션 줄이기 · 저전력 렌더링"**을 플레이스홀더로
     선언하고 *"여기 적힌 항목들은 다음 업데이트에 들어옵니다"*라고 **사용자에게 이미 약속했다**.
   핸드오프의 외형 탭은 이 셋을 **정보창으로 가져간다.** 그러면 설정창의 그 약속이 거짓이 되거나,
   같은 값에 컨트롤이 둘 생긴다. 이 저장소는 그 실패를 이미 한 번 겪고(구석 크기 패널) 삭제로 끝냈다.
3. **아이템 아트가 16/42밖에 없다.** 핸드오프 `ItemIcon.dc.html` 16종은 4슬롯 × 4개다.
   실제 카탈로그는 4슬롯 × **6개**(장비 24) + 외형 18 = 42. **26종의 아트가 정의되지 않았다**
   (베레모·밀짚모자 / 뿔테·안대 / 펜던트·반다나 / 판초·요정날개 + 외형 18).

> **판정 권고**: 핸드오프의 「외형」 페이지는 **정보창이 아니라 설정창의 미완성 탭 2개(캐릭터/접근성)의
> 시안으로 라우팅**하고, 정보창 「외형」 탭은 현행(머리/이펙트/펫 카드)을 유지한다.
> 그러면 4탭은 **장비 / 외형(현행) / 보관함 / 상점**이 되어 `ux-designer` §46과 정확히 일치하고,
> 핸드오프에서 살릴 것(3컬럼·스탯 카드·세트 패널·상점 2컬럼)은 그대로 산다.
> 결정은 리더 — **다만 이 항목이 결정되기 전에 `[외형]` 탭 코드를 건드리면 안 된다.**

---

## 1-C. 이미 폐기됐거나 사실과 다른 전제 — 4건

| # | 어디서 | 무엇 | 실측 사실 |
|---|---|---|---|
| C-1 | 핸드오프 캐릭터 사양 | `눈 circle (90,43)(110,43) r=3.4 흰색` | **눈은 없다(사용자 확정 잠금).** `CharacterPortraitStage.DrawEyes = false`(129행) / `SceneBootstrapper.BakeEyes = false`(244행), 두 게이트를 `EyeRestorePathContractTests`가 잠근다. **채택 불가** |
| C-2 | `UX_HANDOFF_REVIEW` §3-2 사실 3 | *"우리 머리는 채운 원이 아니라 **링(획)**이다"* | **틀렸다.** 머리는 **채운 원반**이다 — `SceneBootstrapper.cs:1245 CreateFilledDisc("HeadFill", …)` + `CharacterPortraitStage.cs:893 AddFilledDisc("HeadFill", …)`. 링(`HeadOutline`)은 **채움과 같은 색**으로 위에 겹치고, `StickmanMetrics`가 반지름을 **재는 자**로만 쓴다(`StickmanMetrics.cs:30`은 렌더가 아니라 계측 문서다). → **H-6(`design-character` 라우팅)은 불필요하다.** 핸드오프의 `fill`은 **우리와 이미 같다.** 남는 충돌은 **눈뿐**이고 그건 C-1로 이미 닫혔다 |
| C-3 | 기획 5-3 세트 연출 예시 | "격파 성공 시 마법진" | 격파 놀이 **삭제 완료**(2026-09-02). 잔존물은 `battleWins` 저장 필드 하나뿐(`CharacterSaveStore.cs:138` 주석이 남긴 이유를 적어 뒀다). `design-systems`가 이미 무효 처리함 |
| C-4 | 기획 5-6 "스탯 재분배" | 배분할 스탯 포인트 | **존재하지 않는다.** 스탯은 100% 파생값(레벨 + 착용 4종)이고 착용은 이미 무료로 언제든 바꾼다. `design-systems` 6-2가 유일한 해석(부스탯 방향 변경)을 냈다. 핸드오프 보관함 탭의 `무료 재분배 1회 사용` 버튼은 **그 해석을 채택해야만 의미가 생긴다** |

---

## 1-D. ★★ 구조적으로 가장 무거운 것 — **불변 원칙 4가 지금 어디에도 구현돼 있지 않다**

리더의 브리핑은 *"장비만 원칙 4를 비껴가 `AccessoryShapeBuilder`가 2,741줄 하드코딩"*이라고 적었다.
**실측 결과 더 나쁘다 — 모션·이펙트는 비껴간 정도가 아니라 소비자가 0개다.**

```
Assets/_Project/Scripts/Plugins/MotionPluginSO.cs   23줄
Assets/_Project/Scripts/Plugins/EffectPluginSO.cs   26줄
  · 프로덕션 참조 : 0곳   (Plugins/ 폴더 밖에서 이 두 타입을 쓰는 .cs 파일 없음)
  · .asset 인스턴스 : 0개
  · 클래스 주석이 스스로 적어 둔 것: "Phase 0에서는 필드만 정의하고,
    실제 소비(적용) 로직은 이후 Phase의 MotionPluginRegistry 등이 담당한다"  ← 그 Registry는 없다
```

그런데 **세 문서가 이것을 이미 존재하는 인프라로 전제한다:**

| 문서 | 전제한 문장 |
|---|---|
| 핸드오프 README | *"StickMate는 Unity 기반(`MotionPluginSO` / `EffectPluginSO` / `DialogueIntent` 플러그인 구조)이므로"* |
| `ECONOMY_SPEC` 3-4 | 세트 완성 = *"연출 스킨(`MotionPluginSO`/`EffectPluginSO` **스위칭**)"* |
| DLC 6팩 기획 | 팩 = 조형 + 대사 + **연출** |

**→ DLC 6팩과 세트 완성 연출은 "구현"이 아니라 "인프라 신설"이다.** 규모 산정이 통째로 빠져 있다.

### 그리고 장비 쪽: **카드는 데이터, 몸은 코드** — 같은 아이템에 진실이 둘

```
카드 아이콘 :  Resources/Items/*.asset (AccessoryDefSO.icon = AccessoryIconPartData[])  ← 데이터
몸 도형     :  AccessoryShapeBuilder.Append(slot, itemIndex) -> switch(itemIndex)        ← 코드
설계 거울   :  design/equipment/verify/items.py                                          ← 손으로 옮긴 사본
```

- `AccessoryShapeBuilder.cs:229~242`가 아이템 자리를 **`const int`로** 박아 둔다
  (`HeadCap = 0 … HeadStraw = 5`). 5개 `Append*` 스위치에 **`default:`가 하나도 없다**
  (파일 전체 `switch` 7개 중 `default:`는 `HatCoverLocalY` 1곳뿐).
- **즉 7번째 모자(`itemIndex = 6`)를 `.asset`으로 추가하면**: 카드에는 아이콘이 뜨고,
  착용은 되고, **몸에는 아무것도 안 그려진다.** 예외도 로그도 없다.
  덤으로 `HatCoverLocalY`의 `default: return float.PositiveInfinity`가 그 모자를
  **조용히 왕관("얹는 물건")으로 취급**해 머리카락 클리핑까지 틀어진다.
- **원칙 4 검증**: `CardsInSection`은 카탈로그에서 **센다**(주석이 자랑스럽게 적어 뒀다). 맞다 —
  **카드만** 원칙 4를 지킨다. 몸은 안 지킨다. 이 비대칭이 오늘 13건 어긋남이 난 자리다.

### 검산 하니스의 비용도 그 비대칭에서 나온다

| | 카드 아이콘 | 몸 도형 |
|---|---|---|
| 설계가 프로덕션을 재는 법 | `.asset` YAML **직접 파싱** (1단) | `Shim.cs`+`CoreShim.cs`로 UnityEngine 흉내 → Roslyn 컴파일 → `dump.dll` 실행 → 탭 텍스트 파싱 (**4단**) |
| 그 하니스를 지키는 하니스 | 불필요 | `Tools/ShapeDump/shimdrift.py`(shim이 표류하지 않는지) + `design/equipment/verify/mirrordrift.py`(거울이 표류하지 않는지) |

**감시자를 지키는 감시자가 2개 필요하다는 것 자체가 구조 신호다.**
`design-equipment`의 결론(*"좌표는 데이터, 자(尺)는 코드"*)에 동의한다. §2-3에 전환 설계를 적는다.

---

## 1-E. 모션 ↔ 경제 — 스탯 어휘가 어긋난다 (3건)

| # | 충돌 | 실측 |
|---|---|---|
| E-1 | **`근력`은 이 게임의 스탯이 아니다** | 모션 사양 §14가 `근력 → 산출`(곡괭이질 광물 / 책상업무 서류) 행을 둔다. 확정 4스탯은 집중력·관찰력·매력·민첩이고, **집중모드 성과물 배율은 `집중력`의 임계 효과**다(`ECONOMY_SPEC` 2-3). 같은 효과에 주인이 둘. (모션 사양 279행은 *"근력(있다면)"*으로 hedge했으나 §14 표는 안 했다) |
| E-2 | **관찰력·매력은 신규 9종에 착지점이 0개** | 모션 사양 전문에서 `관찰력` 0회 / `매력` 0회 / `집중력` 3회 / `민첩` 5회. 관찰력은 **기존 활쏘기**에만, 매력은 **아직 아무도 설계하지 않은 오라 이펙트**에만 걸려 있다 → 4스탯 중 2개가 신규 콘텐츠와 무관하다 |
| E-3 | **집중력 임계 3단계의 연출이 1개만 설계됐다** | `ECONOMY_SPEC` 2-3: 초급 **명상 자세** / 중급 **졸음** / 고급 **깊은 잠**. 모션 사양의 유휴 7종에 `WindowShadeNap`(낮잠) **하나뿐**이고 `명상`·`깊은 잠`은 없다 |

> **권고**: E-1은 **경제 쪽(집중력)으로 통일**한다 — 슬롯↔주스탯 매핑(모자=집중력)이 이미 세 문서에서
> 일치하고, 스탯을 5개로 늘리면 슬롯이 4개라 매핑이 깨진다.
> E-2/E-3은 **정보이지 결함이 아니다** — 다만 "9종을 다 넣어도 관찰력·매력은 여전히 비어 있다"는
> 사실을 순서 판단에 넣어야 한다(§3).

---

## 1-F. 국소 충돌 — 6건 (전부 리더 판정 대기, 규모 작음)

| # | 항목 | 충돌 |
|---|---|---|
| F-1 | 환급 | 핸드오프 `구매가 × 0.4` **상시** vs `ECONOMY_SPEC` 6-2 *"상시 환급 없음, 구매 후 5분 이내 100% 1회"* |
| F-2 | 구매 확인 | 핸드오프 **1클릭 확정** vs `UX_SHOP` §3-5 **2단 버튼 3초** — 이 창은 **바깥 클릭으로 안 닫힌다**(사용자 확정)이라 오조작 탈출구가 `[✕]` 하나뿐 |
| F-3 | 진열 범위 | `ECONOMY_SPEC` 4-1 *"요구 레벨 도달 → 진열"* vs `UX_SHOP` §14-4 *"미달도 진열(S4 비활성)"* — `ux-designer`의 반박 3건에 동의한다 |
| F-4 | 임계 취소 표현 | 핸드오프 *"해금 취소 표현 안 함"* vs `UX_SHOP` §5-3 **색(영구)×높이(현재) 2채널** — 절충안(넘긴 눈금만 황동) 지지 |
| F-5 | 세트 표시 이름 | `ECONOMY_SPEC` 세트 A~F는 **내부 식별자**, 표시 이름 없음. 핸드오프는 팩 이름(오피스 워커 등)을 테마명으로 쓴다 → **팩 6종 이름을 세트 6종 이름으로 그대로 쓰면 해결된다**(양쪽 다 6개, 신규 명명 0건). 리더 확인 요망 |
| F-6 | 신규 행동 수 | 리더 목록은 "유휴 **9종**", 모션 사양 제목은 "집중모드 2종 + 유휴 **7종**" = 합 9 | 목록 표기만 정정하면 된다 |

---

# 2. 【임무 ②】 되돌릴 수 없는 결정 — 8건

판정 기준: **한 번 출하되면 사용자 데이터/자산이 그 형태에 묶여, 되돌리려면 마이그레이션이 필요한 것.**
리더가 준 3건에 **5건을 더 찾았다.**

| # | 결정 | 언제 정해야 하나 | 안 정하면 무슨 일이 나는가 |
|---|---|---|---|
| **I-1** | **저장 원자성**(`File.Replace` IOException 폴백) | **모든 것보다 먼저** | 아래 2-1 |
| **I-2** | **세이브 스키마 v9 → v10의 모양** | 재화/스탯 **코드 첫 줄 전에** | 아래 2-2 |
| **I-3** | **`StickmanStateId`에 명시 값을 박을 것인가** | **첫 DLC/플러그인 `.asset`이 나가기 전** | 아래 2-4 ★ 아무도 안 짚은 것 |
| **I-4** | **장비 좌표를 데이터로 뺄 것인가** | **DLC 6팩 조형 착수 전** | 아래 2-3 |
| **I-5** | **창 크기 / 리플로우 규칙** | **4탭 리디자인 코드 착수 전** | 아래 2-5 |
| **I-6** | **재화 명칭 "동전"과 그 아이콘** | 상점 출하 전 | 저장 필드명(`coinBalance`)과 문구 수십 곳에 퍼진다. `ECONOMY_SPEC` 6-1이 이미 상수 1곳 원칙을 냈다 — **그 원칙만 지키면 되돌릴 수 있다.** 우선순위 낮음 |
| **I-7** | **`ownedItemIds`가 레벨 파생을 대체하는가 보완하는가** | I-2와 동시 | 아래 2-2-b ★ |
| **I-8** | **`AccessoryDefSO.itemIndex`를 계속 도형 키로 쓸 것인가** | I-4와 동시 | 42개 `.asset`이 이미 `itemIndex`를 직렬화한다. 데이터 전환 시 이 필드의 의미가 바뀐다 |

## 2-1. I-1 — 저장 원자성이 **선결 조건인 이유는 빈도가 아니라 회복 불가능성이다**

세 문서(핸드오프 README "선결 조건" / `ECONOMY_SPEC` 7-2 / `UX_SHOP` §9-3)가 **독립적으로 같은 결론**에
도달했다. 이건 이 라운드에서 **세 담당자가 합의한 유일한 항목**이다. 코드로 확인했다:

```
Core/CharacterSaveStore.cs:739   File.Replace(temp, path, null);
                          :745   LastSaveWasAtomic = false;
                          :746   File.WriteAllText(path, json);        ← 비원자적 폴백
   경고 문구가 스스로 인정한다: "이번 쓰기 도중 강제 종료되면 파일이 손상될 수 있습니다."
```

- 실기 로그에 **이미 관측된 사건**이다(Windows).
- 지금 손상되면 잃는 것: **XP/레벨** → 켜두면 저절로 복구된다.
- 재화 이후 잃는 것: **동전 잔액 + 보유 42종 + 임계 영구 해금** → **다시 못 번다**(75일치 집중 세션).
- 노출량: 패시브 XP가 10초마다 `IsDirty`를 세워 60초 자동 저장이 **항상** 돈다 = 1,440회/일.
  완주(75일)까지 **108,000회**. `Save()` 호출처는 지금도 **6파일 16곳**이고 구매/지급이 더 붙는다.

> **판정: I-1은 협상 대상이 아니다.** 순서 1번(§3).

## 2-2. I-2 — v10 스키마: **지금 정하지 않으면 v11·v12를 연달아 올리게 된다**

신규 필드를 **한 라운드에 모아서** 넣어야 한다. 두 문서가 낸 목록을 합치고 **중복 1건을 제거**했다:

```
v10 신규 필드 (제안)
  coinBalance              int         재화 잔액
  ownedItemIds             string[]    구매/유예로 얻은 소유  ← I-7 참조
  itemReachedAtSeconds     long[]      요구 레벨을 넘긴 시각(유예 기산점, 함께한 시간 축)
  statTierReached          int[4]      스탯별 최고 도달 단계(임계 영구 해금) — 평생 12회만 쓰인다
  subStatOverride          string[4]   부스탯 재분배 결과
  lastArcheryCoinUnix      long        활쏘기 동전 지급 쿨다운 기산점
  freeRedistributeUsedAtLevel int      레벨업당 1회 무료 재분배 소진 표시
```

**조건부 필드는 조건이 먼저 정해져야 한다:**
- `itemReachedAtSeconds`는 **유예 자동 해금을 채택할 때만** 필요하다(§1-A 6번).
  채택하지 않으면 이 배열은 영원히 죽은 필드가 된다 → **§1-A를 먼저 닫아야 v10을 확정할 수 있다.**
- `subStatOverride`는 **C-4(재분배의 정의)**가 먼저 정해져야 한다.

**CLAUDE.md 규약 이행**: `CurrentVersion`을 올리는 라운드는 **v9 하위 호환 테스트 1건 동반 필수**.
`CharacterSaveStore.cs:100`의 `internal const int CurrentVersion`을 테스트가 **숫자로 베끼지 말고 참조**해야
한다(이미 `internal`로 열려 있고 그 이유가 주석에 있다).

### 2-2-b. ★ I-7 — 이 마이그레이션의 **유일한 진짜 위험**

`EquipmentModel.cs:64`가 명시적으로 경고한다:

> *"보유 여부는 상태가 아니라 레벨에서 매번 파생된다(**저장하지 않는다 — 저장하면 레벨과 어긋난
> 두 번째 진실이 생긴다**)."*

재화가 들어오면 그 불변식을 **깨야 한다.** 깨는 방법이 두 가지이고 **결과가 완전히 다르다:**

| | 의미 | 결과 |
|---|---|---|
| **(a) 대체** — `IsOwned = ownedItemIds.Contains(id)` | 소유는 오직 저장 배열 | **v9 사용자가 레벨로 갖고 있던 것을 전부 잃는다.** 그리고 이후 레벨업이 **아무것도 주지 않는다** — `Lv.9에 열립니다` 문구가 그 순간 거짓말이 된다 |
| **(b) 합집합** — `IsOwned = 레벨파생 ∪ ownedItemIds` | 소유는 두 경로의 합 | v9 파일이 `ownedItemIds` 없이 읽혀도 **아무것도 안 잃는다.** 레벨업은 계속 준다. 동전/유예는 **앞당기기**로만 작동 |

> **판정: (b) 합집합만이 성립한다.** 그리고 (b)는 `ECONOMY_SPEC` 4-1의 *"동전은 해금이 아니라
> 앞당기기"*와 **같은 문장의 코드 형태**다. 하위 호환 테스트가 잠글 것은 정확히 이것이다 —
> *"v9 파일(= `ownedItemIds` 없음)을 읽으면 레벨 파생 보유가 그대로 살아 있다."*

## 2-3. I-4 — 장비 좌표를 데이터로: **규모와 순서**

리더가 규모 판단을 요청했다. **전면 전환은 반대하고, 3단계 분할을 권고한다.**

### 대상 실측
```
AccessoryShapeBuilder.cs   2,742줄 (주석 46%, 코드 1,493줄)
   internal const   190개      ← 대부분이 "비율 상수"
   internal static   43개
   Append* 스위치     5개 / 분기 30개 (5슬롯 × 6아이템)
   default:           0개  ← 신규 아이템이 조용히 사라지는 자리
```

### 그런데 **190개 상수를 전부 데이터로 빼면 안 된다**

`design-equipment`의 *"좌표는 데이터, 자(尺)는 코드"*가 정확하다. 실제로 이 파일에는
**성질이 다른 두 종류**가 섞여 있다:

| 종류 | 예 | 데이터로 빼는가 |
|---|---|---|
| **자(尺) — 규칙·예산·검사** | `StrokeWidthRatio`, `StrokeBudgetInHeadRadii(scale)`, `FillOutlineBudgetInHeadRadii`, 규칙 1-C 색면 조건, 감쌈 조건 `\|x\| ≥ 0.85R ∧ y ≤ 0.05R` | **아니오.** 이건 "모든 장비가 지켜야 하는 법"이다. 데이터로 빼면 DLC가 법을 어길 수 있게 된다 |
| **좌표 — 이 물건의 생김새** | `HatBrimReachRatio`, `HatBrimRootDropRatio`, `BeanieCuff` 점들, `FedoraBrim` 8점 | **예.** 아이템 하나에만 속한다 |

### 3단계 전환 (되돌릴 수 있는 순서로)

| 단계 | 무엇 | 규모 | 되돌릴 수 있나 |
|---|---|---|---|
| **S1. 안전망 먼저** | 5개 `Append*` 스위치에 `default:` 추가 → 알 수 없는 `itemIndex`면 `Debug.LogError` + **아무것도 안 그리는 대신 그 슬롯을 미착용 취급**. `HatCoverLocalY`의 `+∞` 폴백도 "왕관"과 "알 수 없음"을 가른다 | **매우 작다**(6곳) | 예 |
| **S2. `AccessoryDefSO`에 도형 필드 추가** | `icon`과 **같은 형태**(`AccessoryShapeData[] shape`)를 옆에 둔다. **비어 있으면 기존 스위치로 폴백.** 42개 에셋은 당분간 비워 둔다 | 중간 | **예** — 필드 추가는 하위 호환이고, 비면 오늘과 100% 동일 |
| **S3. 슬롯 단위로 옮긴다** | `Head` 6종부터 `.asset`에 좌표를 채우고 스위치의 그 6분기를 지운다. 다음 라운드에 `Eyes` … 슬롯 1개 = 1라운드 | 5라운드 | 슬롯 단위로 예 |

**S2가 되는 순간 얻는 것 (이게 이 전환의 진짜 값어치다):**
- `items.py`(설계 거울)가 **사라진다.** 설계도 프로덕션도 같은 `.asset`을 읽는다 →
  **거울 어긋남이라는 결함 범주 자체가 소멸한다**(오늘 13건이 난 자리).
- 검산 하니스가 **4단 → 1단**이 된다. `Shim.cs`/`CoreShim.cs`/`shimdrift.py`/`mirrordrift.py`가 불필요해진다.
- **DLC 6팩(36종)이 코드 수정 0줄로 추가된다** = 원칙 4가 장비에서 처음으로 성립한다.

> **순서 권고: S1은 지금 당장(DLC 전에 반드시), S2는 DLC 6팩 조형 착수 직전, S3는 그 뒤 천천히.**
> **S1 없이 DLC를 시작하면 안 된다** — 안 그리는 장비를 디버깅하는 라운드가 확정적으로 생긴다.

## 2-4. ★ I-3 — **아무도 짚지 않은 것: `StickmanStateId`에 값이 안 박혀 있다**

```
Core/StickmanEventBus.cs:10   public enum StickmanStateId { Idle, Walk, Jump, ... }   ← 명시 값 0개
```

- 비교 대상: `EquipmentSlot`은 **명시 값이 박혀 있다**(`Head = 0 … Pet = 6`).
  그리고 그 주석이 왜 박았는지 적어 뒀다 — 표정(FACE) 삭제로 뒤 값이 한 칸 당겨진 사고 이후다.
- `StickmanStateId`는 **같은 사고를 이미 겪었다**: 격파 미니게임 상태를 **enum 중간에서** 지웠다
  (`StickmanEventBus.cs:22~24` 주석). 그때는 아무 피해가 없었다 — **아무도 이 값을 저장하지 않았으니까.**
- **그런데 저장하려는 것이 이미 있다**: `MotionPluginSO.applicableStates` / `EffectPluginSO.applicableStates`가
  `StickmanStateId[]`이고 **ScriptableObject 필드다.** Unity는 enum을 **정수로** 직렬화한다.
- 그리고 모션 사양이 **신규 상태 9개**를 추가하려 한다(`StickmanStateId` 현행 **27개** → **36개**, +33%).

> **판정: 첫 DLC/플러그인 `.asset`이 나가기 전에 `StickmanStateId` 전체에 명시 값을 박아라.**
> 지금 비용 = 상수 한 줄씩 + 값이 안 바뀌었음을 잠그는 EditMode 테스트 1건.
> 나중 비용 = 출하된 모든 DLC 팩 매니페스트의 조용한 오배선(에러 없음, 잘못된 상태에 연출이 붙는다).
> **신규 9종을 추가하는 라운드가 이 작업의 자연스러운 자리다** — 그 라운드가 어차피 enum을 건드린다.

## 2-5. I-5 — 창 크기: **가로 1242는 리플로우 규칙 없이는 결정이 아니다**

`ux-designer`의 실측에 동의한다(세로 802 채택 / 가로 1242는 Win@200%에서 **97.3% 점유**, Win 1920@150%에서
**108.1%** = 화면보다 크다). 여기에 **아키텍트 관점 2건**을 더한다:

1. **점유율 97%는 원칙 2를 시험하는 게 아니라 뒤집는다.** 이 앱의 정체성은 *"업무를 방해하지 않는다"*이고,
   이 창은 **바깥 클릭으로 안 닫힌다**(사용자 확정). 화면의 97%를 덮으면서 탈출구가 `[✕]` 하나인 창은
   **"오버레이 위젯"이 아니라 "전체화면 앱"**이다.
2. **리플로우 규칙이 없으면 코드가 임의로 결정한다.** 현행 `ClampPanelToScreen`은
   `min(설계값, max(320, 화면논리 − 32))`로 **줄이기만 한다.** 핸드오프의 컬럼은 `306px / 292px / 1fr` 고정이라
   **`1fr`만 0을 향해 줄어든다** — 즉 아이템 그리드가 먼저 사라지고 프리뷰/스탯이 남는다.
   이건 "설계된 축소"가 아니라 **CSS 기본 동작의 부작용**이다.

> **권고: 가로 1,042(카드 2열)를 설계 폭으로 채택한다.** Win@200% 점유율 97.3% → **81.6%**,
> 세로 802의 이득(잘림 −93 → −34)은 그대로 살고, `ux-designer`가 계산한 임계점 표를 그대로 쓴다.
> 1242를 유지하려면 **디자이너에게 `< 1042` 구간의 규칙을 받아야 구현 가능**하다 — 그 전에는 착수 불가.
> **이건 제품 결정이므로 사용자에게 점유율 숫자를 전달하고 고르게 할 것을 권고한다.**

---

# 3. 【임무 ③】 착수 순서 — "A를 안 하면 B가 어떻게 막히는가"

## 3-1. 막힘 그래프 (화살표 = "왼쪽이 없으면 오른쪽이 막힌다")

```
[0] 사용자 결정 2건 (§5)
      └─▶ [1] 저장 원자성 (I-1)
              └─▶ [2] v10 스키마 확정 (I-2 · I-7 합집합)
                      ├─▶ [3] 재화·상점
                      │       └─▶ [4] 스탯/임계/세트 표면
                      └─▶ [7] 유예 자동 해금

[A] 창 크기·리플로우 확정 (I-5) ─┐
[B] 「외형」 탭 귀속 확정 (§1-B) ─┴─▶ [5] 캐릭터창 4탭 리디자인
                                        (└ [3][4]가 이 창 안에 들어간다)

[6] AccessoryShapeBuilder default: 안전망 (S1)
      └─▶ [8] 장비 데이터화 S2
              └─▶ [9] DLC 6팩 조형

[10] StickmanStateId 값 고정 (I-3)  ─┐
[11] MotionPlugin/EffectPlugin 레지스트리 신설 ─┴─▶ [9] DLC 6팩 (연출·대사 스킨)
                                                  └─▶ [4]의 "세트 완성 = 매니페스트 스위칭"

[12] 집중모드 2행동 + 유휴 7종      ← 위 어느 것에도 안 막힌다 (독립)
[13] 모바일 스크린샷 백드롭 모드    ← 위 어느 것에도 안 막힌다 (독립, 그러나 §4-5 참조)
```

## 3-2. 순서와 근거 (막힘 관계로만 씀)

| 순 | 작업 | **이걸 안 하면 무엇이 막히는가** |
|---|---|---|
| 1 | **저장 원자성** (I-1) | 재화·보유·임계 해금이 들어온 뒤 손상되면 **다시 못 번다.** 108,000회 노출. 세 담당자가 독립적으로 같은 결론. **뒤로 미룰 수 있는 유일한 조건은 "재화를 영원히 안 넣는다"뿐이다** |
| 2 | **`Append*`에 `default:`** (I-4 S1) | 이걸 안 하면 신규 장비가 **에러 없이 안 그려진다.** 6곳 수정. **DLC 조형 라운드가 시작되기 전이면 언제든 되지만, 시작된 뒤면 이미 늦다**(첫 증상이 "안 보인다"라서 원인 추적이 가장 비싸다) |
| 3 | **사용자 결정 2건 회수** (§5) + **§1-A 축 결정** + **§1-B 외형 귀속** + **I-5 창 폭** | 이 넷이 **전부 화면 내용을 바꾼다.** 카드 메타 칸(유예 유무) / 게이지 상한(20 vs 40) / 탭 3개인가 4개인가 / 컬럼 3열인가 2열인가. **하나라도 미정인 채 4탭 코드를 쓰면 그 코드는 버려진다** |
| 4 | **v10 스키마** (I-2 · I-7) | 재화·상점·임계 저장이 전부 여기 매달린다. **필드를 나눠 넣으면 v10·v11·v12가 되고 하위 호환 테스트가 3배**가 된다 |
| 5 | **재화·상점** → **스탯/세트 표면** | 상점이 없으면 스탯 화면에 "무엇으로 바꾸는가"가 없다. 반대는 성립한다(상점만 먼저 가능) |
| 6 | **`CharacterInfoWindow` 구조 선정리** (§4-4 P0 3건) | `TabCount`/`TabNames`/`enum Tab` 3중 정의 + `wantAppearance` 2분기 불리언 **3곳**(892 / 909 / 1424행). **4번째 탭을 이 상태로 얹으면 `[상점]`이 조용히 `[장비]`로 폴백한다.** 비용이 작고 5번 작업의 전제라 5번과 **같은 라운드**에 넣는다 |
| 7 | **`StickmanStateId` 값 고정** (I-3) | 첫 플러그인 에셋 전. **신규 9종 라운드가 자연스러운 자리** |
| 8 | **플러그인 레지스트리 신설** | 없으면 **DLC 6팩의 "연출·대사"와 세트 완성의 "스킨 전환"이 전부 착수 불가.** 지금은 인프라가 0이다(§1-D) |
| 9 | **장비 데이터화 S2 → DLC 6팩 조형** | S2 없이 36종을 넣으면 `AccessoryShapeBuilder`가 2,742 → 약 6,000줄이 된다 |
| — | **집중모드 2 + 유휴 7종** | **아무것도 안 막고 아무것에도 안 막힌다.** 위 대기열과 **병렬로 돌릴 수 있는 유일한 큰 덩어리**다. 단 §1-E(스탯 어휘)를 먼저 정정할 것 |
| — | **모바일** | 독립. 그러나 §4-5 — 어셈블리 경계를 나눌 거라면 **모바일 착수 전이 가장 싸다** |

## 3-3. ★ 지금 병렬로 돌릴 수 있는 것 / 없는 것

- **병렬 가능**: [1] 저장 원자성 · [2] `default:` 안전망 · [12] 신규 9종 모션 · [7] enum 값 고정.
  서로 파일이 안 겹친다(`Core/CharacterSaveStore.cs` / `Interaction/AccessoryShapeBuilder.cs` /
  `States/*` / `Core/StickmanEventBus.cs`).
- **병렬 불가**: [3][4][5][6]은 **전부 `CharacterInfoWindow.cs` 한 파일**이다(3,549줄).
  CLAUDE.md의 *"동시 진행 시 파일이 겹치지 않게 리더가 갈라 준다"*가 여기서 물리적으로 불가능해진다.
  → **§4가 이 문제의 답이다.**

---

# 4. 【임무 ④】 모듈화 — 실측 판단

## 4-1. 실측 — 규모와 어셈블리 현황

```
프로덕션 .cs   190파일 / 87,332줄        (Assets 전체, Tests 제외)
테스트 .cs     221파일
런타임 어셈블리 StickMate.Runtime.asmdef  1개 — 전부 여기 들어 있다
  references: ["Kirurobo.UniWindowController"]
테스트 어셈블리 StickMate.Tests.EditMode / StickMate.Tests.PlayMode (둘 다 Runtime만 참조)
InternalsVisibleTo  1줄 — "StickMate.Tests.EditMode"만.  PlayMode에는 internal 접근이 없다
```

| 버킷 | 파일 | 비고 |
|---|---|---|
| Interaction | 50 | 가장 크다 |
| Platform | 48 | (+ Windows 11 / MacOS 5 / Mobile 1) |
| Core | 31 | |
| States | 29 | |
| Editor | 7 | |
| Dialogue | 5 | |
| Plugins | 2 | **소비자 0(§1-D)** |

## 4-2. 【답 1】 경계를 어디에 그을 것인가 — **의존 방향은 이미 순환한다** (실측)

`using` 선언 전수 + 정규화 참조 스캔 결과. **순환 쌍이 실제로 존재한다:**

| 정방향 (많음) | 역방향 (적음) | 역방향을 만드는 **파일 전부** |
|---|---|---|
| Interaction → Core (46파일) | — | 없음 ✔ |
| States → Core (24) | **Core → States (3)** | `Core/StickmanAgent.cs`, `Core/CharacterScaleController.cs`, `Core/SpectacleEventLock.cs` |
| Interaction → Platform (23) | — | 없음 ✔ |
| States → Dialogue (9) | **Dialogue → States (3)** | `Dialogue/DialogueBubbleRenderer.cs`, `AmbientChatter.cs`, `DialogueIntent.cs` |
| Interaction → States (9) | — | 없음 ✔ |
| Platform → Core (5) | **Core → Platform (3)** | `Core/StickmanAgent.cs`, `Core/DockGeometry.cs`, `Core/SpectacleEventLock.cs` |
| — | **Core → Dialogue (2)** | `Core/StickmanEventBus.cs`, `Core/AppSettingsModel.cs` |
| — | **Core → Platform/{Windows,MacOS,Mobile}** (각 1) | `Core/StickmanAgent.cs` **한 파일** |
| Platform/{Win,Mac} → Platform (5) | **Platform → Platform/{Win,Mac}** | 팩토리 패턴 — `#if`로 구체 구현을 고른다 |
| — | **Platform/Windows → Interaction** (1) | `Platform/Windows/WindowsCompositionProbe.cs`의 **2줄**: `Interaction.UiChrome.FontTitle`(62행) / `Interaction.UiChrome.RoundedFill`(278행) |
| Interaction → Dialogue (1) | **Dialogue → Interaction (1)** | ★ **2노드 순환 1건**: `Dialogue/DialogueBubbleRenderer.cs` ↔ `Interaction/HardwareReactionRenderer.cs`. 서로 상대 타입 **하나씩만** 쓴다 — 내가 제안하는 `Gameplay`(=States+Dialogue) ↔ `UI`(=Interaction) 경계를 **정확히 가로지르는 유일한 순환**이다 |

### ★ 이 표에서 읽을 것

1. **`Interaction`은 이미 깨끗한 최상층이다.** `Interaction → Core/Platform/States`만 있고 역방향이
   (WindowsCompositionProbe 2줄을 빼면) 없다. → **가장 먼저 떼어낼 수 있는 것은 Interaction이다.**
2. **`Core`는 최하층이 **아니다**.** Core가 States·Platform·Dialogue·플랫폼3종을 전부 부른다.
   원인은 **`Core/StickmanAgent.cs` 한 파일**(1,283줄, MonoBehaviour)이 **컴포지션 루트**이기 때문이다:
   ```
   Core/StickmanAgent.cs:1217  #if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                        :1232      return new FallbackPlatformWindowService(new Win32WindowService(), _config);
                        :1233  #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR ...
                        :1265  #elif UNITY_IOS || UNITY_ANDROID
                        :1273      return new ScreenshotBackdropPlatformService();
   ```
   **이 파일 하나를 옮기면 Core → Platform/* 역방향 4개가 전부 사라진다.**
3. **`Gameplay` ↔ `UI` 사이에는 순환이 딱 하나 있고, 그것도 2파일짜리다.**
   `DialogueBubbleRenderer`(Dialogue)가 `HardwareReactionRenderer`(Interaction) 하나를,
   그쪽이 `StickMate.Dialogue`를 쓴다. **이 한 쌍만 풀면 UI → Gameplay 단방향이 된다** —
   가장 값싼 해법은 둘 사이의 계약을 `Core`(또는 `Dialogue`)의 인터페이스 하나로 뒤집는 것이다.
4. **`Platform`의 순환은 팩토리 패턴이라 구조적이다.** `Platform`이 `Platform/Windows`의 구체 타입을
   `new` 한다. 어셈블리를 나누면 이건 **컴파일 불가**가 된다 → 팩토리를 **위층(Bootstrap)**으로 올려야 한다.

### 권고 경계 (5 어셈블리)

```
StickMate.Core        ← 최하층. 모델/상수/이벤트버스. 아무도 안 부른다
StickMate.Platform    ← Core만 참조. 인터페이스 + 플랫폼 중립 정책
   StickMate.Platform.Windows / .MacOS / .Mobile   ← Platform + Core 참조
StickMate.Gameplay    ← States + Dialogue (둘의 순환은 안쪽에 가둔다 — §4-3)
StickMate.UI          ← Interaction
StickMate.Bootstrap   ← ★ 신설. StickmanAgent + 플랫폼 팩토리. 전부 참조. 아무도 안 참조
```

> **핵심 값어치**: CLAUDE.md의 *"정책 판정은 플랫폼 중립 위치에, 플랫폼 전용 코드는 사실 조회만"*이
> **관습에서 컴파일 오류로 승격된다.** `FullscreenSuspendPolicy`가 `Platform/MacOS/`에 있어
> Windows가 못 부른 그 사고는, 이 경계에서는 **애초에 컴파일되지 않는다.**
> `PlatformParityAuditTests`가 사후에 잡던 것을 컴파일러가 사전에 잡는다. **동의한다 — 이게 이 제안의
> 가장 큰 값어치라는 리더의 판단이 맞다.**

## 4-3. 【답 3】 대가 — `internal` 가시성이 갈라진다

| 실측 | 값 |
|---|---|
| 프로덕션 `internal` 선언 총계 | **448건** (Interaction 314 / Platform 57 / Windows 32 / Core 22 / MacOS 13 / States 9 / Dialogue 1) |
| `AccessoryShapeBuilder`의 `internal const` | **190개** (+ `internal static` 43) |
| **버킷을 넘는 `internal` 참조 (상한)** | **119건** — ★ 이 숫자는 **과대계상이다** |

**과대계상인 이유를 재현했다**: 이름 기반 스캔이라 `Tick` / `Begin` / `Append` / `Resolve` / `From` / `To`
같은 흔한 멤버명이 여러 클래스에 중복 선언돼 있으면 전부 교차로 센다. 실제로 검증해 보니
`Platform/Windows → Platform/MacOS (7건)`은 **전부 오검출**이었다 — `OverlayRectReporter`는
`MacOverlayStateEnforcer`와 `WindowsOverlayStateEnforcer`가 **각자 따로** 선언한 동명 멤버다.

**육안으로 확인한 진짜 교차 `internal` (표본):**

| 경로 | 심볼 | 성격 |
|---|---|---|
| Dialogue → States | `StickmanStateMachine.CurrentTransitionGeneration`, `StateTransitionContext.TryConsumeToken` | **불변 원칙 1(행동-텍스트 싱크)의 계약 그 자체.** `AssemblyInfo.cs` 주석이 이 계약을 명시한다 |
| Interaction → Platform | `FramePacing.HoldActiveForInteraction`, `StallAttribution.CurrentTier` | 성능 계측/페이싱 |
| Platform/{Win,Mac} → Platform | `FramePacing.ResolveCharacterIdle`, `StallAttribution.Begin/Tick` | |

> **정확한 건수는 미확인.** 확정하려면 Roslyn으로 **분할 컴파일 1회**를 돌려야 하고
> (`Tools/CrossCompile/xcheck.sh`가 이미 Unity 동봉 Roslyn을 모는 방법을 갖고 있다),
> 그건 트리를 복제해 asmdef 없이 버킷별 `-target:library`로 컴파일하는 별도 라운드다.
> **그 라운드의 컴파일 에러 목록이 곧 "옮겨야 할 것의 전수"다.** 추정으로 대신하지 않기를 권고한다.

**함께 갈라지는 것 2건 (확정적):**
- `InternalsVisibleTo("StickMate.Tests.EditMode")`는 **어셈블리마다 1줄씩** 필요해진다(1 → 5~6줄).
  비용은 작다. **다만 PlayMode 테스트에는 지금도 internal 접근이 없으므로 그쪽은 무변화다.**
- `AccessoryShapeBuilder`의 `internal const` 190개는 **소비자가 전부 `Interaction` 안에 있으므로 무사하다**
  (`CharacterAccessoryRenderer` / `CharacterPortraitStage` 둘 다 Interaction).
  **단 `Tools/ShapeDump`가 이 파일을 소스째 컴파일한다** — asmdef와 무관하니 영향 없다.
  **그리고 §2-3 S2를 하면 이 190개 중 좌표 몫이 `.asset`으로 나가면서 문제가 더 작아진다.**

## 4-4. 【답 4】 ★ **파일 쪼개기와 어셈블리 나누기는 다른 일이고, 순서가 있다**

**둘 다 해야 한다. 그러나 파일 쪼개기가 먼저다.** 근거 3건:

1. **지금 막고 있는 것은 파일이지 어셈블리가 아니다.** §3-3에서 확인했듯
   [상점]·[스탯]·[4탭]·[구조정리]가 **전부 `CharacterInfoWindow.cs` 한 파일**이라 병렬이 안 된다.
   어셈블리를 나눠도 그 파일은 여전히 한 개다. **어셈블리 분리는 이 병목을 못 푼다.**
2. **어셈블리 분리는 되돌리기 비싸고 지금 4개 라운드가 동시에 돈다.** asmdef 추가는 트리 전체를
   재컴파일시키고 `.meta`/GUID를 만들며, `#if` 분기의 컴파일 대상이 바뀐다. **동시 라운드 중 착수 반대.**
3. **파일 쪼개기는 대부분 `partial`로 공짜다.** 검산:

| 파일 | 줄 | 쪼개는 법 | 위험 |
|---|---|---|---|
| `Interaction/CharacterInfoWindow.cs` | 3,549 | `partial class` — Tabs / Cards / Inventory / Portrait / Stats·Shop(신규) | **0.** MonoBehaviour의 직렬화는 필드 이름 기준이고 partial은 이름을 안 바꾼다 |
| `Core/StickConfig.cs` | 3,349, **필드 390개** | `partial class` — Motion / Dialogue / Progression / Platform / Economy(신규) | **0**, 단 ★ 아래 |
| `Interaction/AccessoryShapeBuilder.cs` | 2,742 | 슬롯별 partial, 또는 **§2-3 S2로 좌표를 아예 빼기** | 0 |
| `States/StickmanBlackboard.cs` | 2,749 | partial (감지 / 발판 / 상태 캐시) | 0 |

> ★ **`StickConfig` 경고 — 이건 되돌릴 수 없는 결정과 붙어 있다.**
> `StickConfig`는 `[CreateAssetMenu]` **ScriptableObject**이고 실제 에셋이 하나 있다:
> `Assets/_Project/Data/DefaultStickConfig.asset` (필드 390개가 그 YAML에 이름으로 적혀 있다).
> **`partial class`로 파일만 쪼개는 것은 안전하다**(직렬화 이름·타입 불변).
> **그러나 "SO를 여러 개로 쪼갠다"거나 "필드를 중첩 `[Serializable]` 클래스로 묶는다"는 순간
> 에셋 마이그레이션이 된다** — 세이브 스키마와 같은 무게의 결정이다.
> 재화·스탯 필드가 들어오기 전에 **"파일만 쪼갠다 / 에셋은 안 건드린다"를 명시적으로 못박기를 권고한다.**

## 4-5. 【답 5】 모바일과의 관계 — **어셈블리 경계가 돕는다. 그리고 지금이 가장 싸다**

**실측 — 모바일은 아직 거의 없다:**
```
Platform/Mobile/ScreenshotBackdropPlatformService.cs   110줄, 파일 1개
  · IPlatformWindowService 구현 1개 + 유저 지정 발판 리스트뿐
  · 온보딩 UI / 터치 입력 / 스크린샷 로드 경로 : 없음
  · 클래스 주석이 스스로 적었다: "실제 플랫폼별 자동 선택 로직은 …
    추후 별도 팩토리/부트스트랩 코드의 책임이다"  ← 그 팩토리는 Core/StickmanAgent.cs:1265에 있다
```

| | 판단 |
|---|---|
| **돕는가** | **돕는다.** 모바일은 데스크톱의 `Interaction`(정보창/설정창/부채꼴/톱니 — 50파일, 전부 마우스·창 오버레이 전제)을 **거의 쓸 수 없다.** 어셈블리가 하나면 iOS 빌드에 그 50파일이 전부 들어간다. `StickMate.UI`가 분리돼 있으면 **모바일 전용 UI 어셈블리로 갈아끼울 수 있다** |
| **방해하는가** | 한 곳. `Platform/Mobile`이 `Platform`을 참조하고 `Core/StickmanAgent`가 `Platform.Mobile`을 참조한다 → **§4-2의 `Bootstrap` 어셈블리 신설로 함께 풀린다** |
| **시점** | ★ **모바일 코드가 110줄인 지금이 경계를 긋기 가장 싼 시점이다.** 온보딩·터치·백드롭 렌더가 들어와 모바일이 20파일이 된 뒤에 나누면, 그때는 그 20파일이 어느 쪽에 속하는지를 **먼저 정해야** 나눌 수 있다 |

## 4-6. 【답 2】 한 번에 하지 않는 순서

| 단계 | 무엇 | 지금 해도 되나 | 되돌리기 |
|---|---|---|---|
| **M1** | **`CharacterInfoWindow.cs`를 `partial`로 5분할** | ★ **지금.** [상점]/[스탯]/[4탭]의 **전제**다(§3-3) | 쉽다 |
| **M2** | `StickConfig.cs`를 `partial`로 분할 (**에셋 무변경 명시**) | 재화 필드 들어오기 **직전** | 쉽다 |
| **M3** | `Core/StickmanAgent.cs`의 **플랫폼 팩토리를 `Bootstrap/`로 분리** (폴더만, 어셈블리 아직 없음) | 동시 라운드가 끝난 뒤 | 중간 |
| **M4** | `WindowsCompositionProbe.cs`의 `Interaction.UiChrome` 참조 **2줄** 제거 (값을 Platform 쪽 상수로 넘겨받게) | M3와 함께 | 쉽다 |
| **M4b** | `DialogueBubbleRenderer` ↔ `HardwareReactionRenderer` **2파일 순환** 제거 (인터페이스 1개로 방향 뒤집기) | M3와 함께 | 쉽다 |
| **M5** | `Core → States/Dialogue` 역방향 **5파일** 정리 | M3 뒤 | 중간 |
| **M6** | ★ **Roslyn 분할 컴파일 1회** — 남은 `internal` 교차의 **전수**를 컴파일러에게 묻는다 | M5 뒤 | 되돌릴 것 없음(측정) |
| **M7** | asmdef 6개 도입 | **M6 결과를 보고 리더가 판단** | **비싸다** |

> **M1~M5는 어셈블리를 나누지 않고도 전부 값어치가 있다** — 병렬 작업이 풀리고, 순환이 줄고,
> 플랫폼 팩토리가 제자리를 찾는다. **M7까지 안 가도 손해가 없다.**
> 그래서 **"둘 다 해야 하지만, M1~M6을 먼저 하고 M7은 그 결과를 보고 정한다"**를 권고한다.

---

# 5. ★ 사용자 답변 대기 — 추측으로 메우지 않은 것

## 5-1. 【미확인】 미니 보스 레이드를 되살릴 것인가

- 기획 5-6이 **전설 등급 대체 획득처**로 썼다.
- **2026-08-31 사용자가 AskUserQuestion으로 완전 제외를 확정했다**(`Tasklist.md:9512`).
- 코드 확인: `Rival*` 계열 클래스는 **프로덕션에 0개** — 실제로 지워져 있다.

| 사용자 답 | 결과 |
|---|---|
| **되살리지 않는다**(현행 유지) | **아무것도 바뀌지 않는다.** `ECONOMY_SPEC` 4-1의 유예 자동 해금이 그 요구를 이미 흡수했다 — 원형 A(집중모드 0회)도 87일차에 42종 전부 보유한다. 전설의 "대체 획득처"가 필요했던 이유(저참여 유저가 영영 못 가짐)가 구조적으로 사라진다. **신규 인프라 0개** |
| **되살린다** | 라이벌 인프라(`RivalStickmanAgent` 등) **전체 재구축**이 선행된다 = 큰 신규 라운드. 그리고 유예 자동 해금과 **획득 경로가 중복**되므로 둘 중 하나의 수치를 다시 짜야 한다. `docs/ARCHITECTURE.md`가 이 항목의 난이도를 "큼", 원칙을 "확인 필요"로 표시해 둔 것과 일치한다 |

> **아키텍트 권고: 되살리지 않는다.** 유예 모델이 같은 문제를 신규 인프라 0개로 푼다.

## 5-2. 【미확인】 관찰력의 "활쏘기 쿨다운 단축"을 **동전 지급 쿨다운**에도 적용할지

사실 확인(코드):
```
StickConfig.cs:2494  public float archeryChance = 0f;        ← 자율 발동 확률 0
StickConfig.cs:2500  public float archeryCooldownSeconds = 600f;
StickConfig.cs:2496  주석: "archeryChance가 0이면 추첨 자체가 무의미하다."
States/ArcheryState  마지막 발은 항상 정중앙 = 명중률 33.3% 구조 고정
```
→ **기본 설정에서 자율 발동이 없으므로, 쿨다운 단축은 지금 아무 화면도 안 바꾼다.**

| 사용자 답 | 결과 |
|---|---|
| **적용한다** | 관찰력이 실제 효과를 갖는다(고급 = −50% → 활쏘기 동전 6/시 → 12/시). `ECONOMY_SPEC` 4-4의 완주일수 표를 **재검산해야 한다**(동전 획득량이 최대 2배가 되는 구간이 생긴다). 신규 튜닝 노브 0개 — 기존 `archeryCooldownSeconds`를 재사용 |
| **적용 안 한다** | 관찰력의 확정 효과 3개 중 **"쿨다운 단축" 1개가 기본 설정에서 무효**가 되고, 남는 것은 잔상 이펙트 2개뿐이다. 그런데 잔상 계열은 `ECONOMY_SPEC` 2-3 권고대로 **기본 OFF**이므로, **관찰력은 기본 설정에서 아무 일도 하지 않는 스탯이 된다.** 카드에 `관찰력 +6`이라 적힌 것이 거짓말에 가까워진다(원칙 1) |

> **아키텍트 의견: 적용하는 쪽을 권고하되, 이건 수치 문제가 아니라 "스탯 하나가 기본 설정에서
> 비어 있어도 되는가"라는 제품 결정이라 사용자 답이 필요하다.**
> **적용하지 않기로 한다면, 관찰력에 다른 착지점을 하나 줘야 한다**(§1-E E-2 — 지금 관찰력은
> 신규 9종 어디에도 없다).

## 5-3. 리더가 사용자에게 함께 물어 주기를 권고하는 것 2건 (내가 새로 낸 것)

| # | 질문 | 왜 사용자여야 하나 |
|---|---|---|
| **U-3** | **창이 화면의 97%를 덮어도 되는가** (핸드오프 가로 1242 @ Windows 2560×1600 200%) | 창 크기는 제품 결정이고, 이 앱의 정체성(원칙 2 비침해)과 직접 부딪힌다. 대안 1,042는 점유율 81.6%이고 카드 열 수 외 모든 사양이 산다 |
| **U-4** | **「외형」 탭이 아이템 착용 화면인가 설정 화면인가** (§1-B) | 핸드오프를 그대로 쓰면 출하된 아이템 **18종의 착용 UI가 사라진다.** 사용자가 핸드오프를 올렸으므로 의도를 확인해야 한다 |

---

# 6. 플랫폼 영향

| | 내용 |
|---|---|
| **Windows 영향** | **함께 검토함 — 별도 배정 불필요(이 라운드는 설계 문서 1개뿐, 프로덕션 `.cs` 0줄 수정).** 다만 이 검토가 **Windows 선행 작업 1건을 확정한다**: §2-1 저장 원자성(`File.Replace` IOException)은 **Windows 실기 로그에서만 관측된 결함**이고(백신/OneDrive/Steam이 대상 핸들을 쥔다), 재화 도입이 그 피해를 "되돌릴 수 있는 불편"에서 "복구 불가능한 손실"로 바꾼다. **CLAUDE.md 규약의 역방향 사례** — macOS를 고치기 전에 Windows를 먼저 고쳐야 한다. 그리고 §2-5 창 크기 문제는 **Windows DPI 200%/150%에서만** 발생한다(macOS는 ×1/×2뿐이라 1512×982에서 안전) |
| **macOS 영향** | **없음(이 라운드).** 구현 단계에서 §1~§4의 판정은 전부 플랫폼 중립이다 — 검토 대상 파일(`CharacterInfoWindow.cs` / `UiChrome.cs` / `CharacterSaveStore.cs` / `AccessoryShapeBuilder.cs` / `ItemCatalog.cs`)에 `#if UNITY_STANDALONE_*` 분기 **0건**(grep 확인). 유일한 예외는 §4-2가 지목한 `Core/StickmanAgent.cs:1217~1276`의 플랫폼 팩토리이고, 그건 **양 플랫폼을 동시에 옮기는** 작업이다 |
| **모바일(iPad/iPhone)** | §4-5. 지금 `Platform/Mobile`은 **파일 1개 / 110줄**이고 온보딩·터치·백드롭 렌더가 전부 미구현이다. 어셈블리 경계를 그을 거라면 **모바일이 커지기 전인 지금이 가장 싸다.** 그리고 정보창 880×861(또는 핸드오프 1,242)은 **iPhone 논리 폭 ≈390을 초과**하므로 4탭 리디자인은 모바일에 그대로 못 간다 — **미확인 / 별도 라운드** |
