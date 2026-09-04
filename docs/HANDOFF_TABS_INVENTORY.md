# 인계본 탭 4종 구성요소 전수 목록 (리더 직접 판독)

**출처**: `docs/handoff/design_handoff_equipment_window/reference/equipment-screen.dc.html`
— 1,065줄 / 75,434자. **판독 대상은 `<script>` #2 (18,408자) 전량 + DOM.**

**작성 경위**: 사용자 지시(2026-09-03) — *"디자인파일 전체 재 확인하고 장비는 이미 우리가 이야기
끝냈으니까 캐릭터 창이나 보관함 상점등 탭들의 디자인들 그 탭 안에 구성요소들 확실히 파악한후
팀원들에게 전달해"*.

★ **이 파일이 존재하는 이유**: `EQUIPMENT_HANDOFF_PORT_SPEC.md:927`이 *"인계본
`equipment-screen.dc.html`(78 KB) — 4탭 프로토타입. **이 라운드에서 안 읽었다**"*라고 자백했고,
리더가 그 줄을 읽고도 조치하지 않았다. 그 결과 **스탯·재화·세트 시스템이 통째로 없다는 사실을
사용자가 렌더 사진을 보여줄 때까지 아무도 몰랐다.**

## ★ 무엇을 안 봤는가 (이 문서의 한계)

- **DOM 마크업의 레이아웃 수치**(패딩·간격·폰트 크기)는 **안 뽑았다.** 이 문서는 **구성요소와 데이터**
  목록이지 레이아웃 사양이 아니다. 그쪽은 `ux-designer` 배정 건이다.
- **variant B**(청록 `#57E0C8` 계열)는 **채택하지 않았다** — 우리는 A(브라스 `#C8A15A`) 확정.
  B의 수치는 A와 같고 색만 다르다(`RAR_B` 참조). **B를 근거로 색을 정하지 마라.**
- `ItemIcon.dc.html`(조각 아트)은 **별건**이고 `EQUIPMENT_HANDOFF_PORT_SPEC.md`가 이미 다룬다.

---

## 0. 공통 — 탭 바

```
탭 4종: 장비 · 외형 · 보관함 · 상점
활성 탭:  bg #C8A15A / color #160F06 / weight 600
비활성:   bg transparent / color #8A8578 / weight 400
★ 상점 탭에만 알림 점(dot = 1). 나머지 0.
```

---

## 1. 【장비】 탭 — 이미 합의 완료. 수치 출처만 재확인용

```js
SLOT_MAIN = { HEAD:'집중력', EYES:'관찰력', NECK:'매력', BACK:'민첩' }
BASE  = { 집중력:8, 관찰력:6, 매력:5, 민첩:7 }
TIERS = [초급 10, 중급 20, 고급 32]      CAP = 40
MAINV = [3, 6, 10, 15]                   SUBV = [1, 2, 4, 6]
```
상세는 `docs/DESIGN_SYSTEMS_STATS.md`(design-systems 작성 중) 소관.

---

## 2. 【외형】 탭 — `isLook` · 구성요소 8종

| # | 요소 | 데이터 | 우리 구현 |
|---|---|---|---|
| L-1 | **이름** | `cfg.name` 기본 `'스틱'` | 확인 필요 |
| L-2 | **크기 슬라이더** | `cfg.scale`, 표시 `100%`, 막대 `(scale−70)/80` ⇒ **범위 70~150** | 우리 `MaxCharacterScale` 과 대조 필요 |
| L-3 | **외곽선 토글** | `cfg.outline` | 확인 필요 |
| L-4 | **fps 선택** | `[30, 45, 60]` 3택 | ★ **우리 `FramePacing` 적응형과 충돌 가능** |
| L-5 | **아침 인사 토글** | `cfg.morning` 기본 `true` | 확인 필요 |
| L-6 | **밤 인사 토글** | `cfg.night` 기본 `false` | 확인 필요 |
| L-7 | **팩 스와치 6개** | 아래 색표 | 미구현 |
| L-8 | **프리셋 3개** | 아래 표 | **미구현 — 신규 개념** |

**토글 시각 규약** (`tog()`):
```
켜짐  bg #C8A15A / knob right(calc(100%−17px)) / knobBg #160F06 / 라벨 "켜짐" #C8A15A
꺼짐  bg #231F1B / knob left(3px)              / knobBg #6E665C / 라벨 "꺼짐" #5C574E
```

**팩 스와치 색** (L-7):
```
office #C8A15A   cyber #6E9BE8   neon #B07BE0
sport  #8FBF6A   ink   #D2402F   mil  #8A8578
선택된 것 테두리 #EDE7DB / 나머지 #231F1B
```
★ `ink #D2402F`는 `ItemIcon.dc.html`의 `MAT.shortcape/longcape`와 **같은 값**이다. 우연이 아닌지
`design-art` 확인 필요.

**프리셋 3개** (L-8) — **저장된 조합을 한 번에 적용하는 신규 기능**:
| 이름 | 구성 | 태그 | 태그색 | 적용 |
|---|---|---|---|---|
| 오피스 워커 풀세트 | 중절모 · 동그란안경 · 줄무늬타이 · 배낭 | `세트 완성` | `#8FBF6A` | 가능 |
| 야간 잠행 | 털모자 · 고글 · 목도리 · 긴망토 | `이종 조합` | `#8A8578` | 가능 |
| 연회장 | 왕관 · 외알안경 · 나비넥타이 · 짧은망토 | `2종 미보유` | `#6E665C` | **불가(`미보유`)** |

버튼: 가능 `bg #C8A15A / color #160F06` · 불가 `bg #17130E / color #5C574E / border #2A2622`

---

## 3. 【보관함】 탭 — `isBag` · 구성요소 2종

```
헤더: bagCount = "<N>종 보유"      (보유 아이템만 집계)
목록: 보유 아이템 전량, 부위 순서(HEAD→EYES→NECK→BACK) × 정의 순서
```

**행 1건의 필드**:
| 필드 | 값 |
|---|---|
| `name` / `kind` | 아이템 이름 · 도형 종류 |
| `slotKo` | 부위 (모자/안경/넥타이/망토) |
| `rarLabel` / `rc` | 등급 낱말 + 등급색 |
| `themeKo` | 테마(= DLC 팩) 이름 |
| **`refund`** | ★ **`동전 <가격 × 0.4>`** — **환불 40%. 우리에게 없는 개념이다.** |
| `iconBg` | `radial-gradient(70% 70% at 50% 42%, <등급색>1F, transparent 72%), #0F0D0C` |

★ **`refund`는 「판매/분해」 기능의 존재를 함축한다.** 인계본에 그 버튼은 없고 **값만** 있다.
**기능인지 표시인지 사용자 확인 필요.**

---

## 4. 【상점】 탭 — `isShop` · 구성요소 2종

### 4-1. DLC 팩 6종 (`packs`)

| key | 이름 | 가격 | 보유 | 미리보기 4종 | 설명 |
|---|---|---|---|---|---|
| office | 오피스 워커 | **기본 제공** | ✅ | fedora · roundglasses · stripedtie · backpack | 책상업무 성과물 · 서류더미 |
| cyber | 사이버 아포칼립스 | **₩4,900** | ✗ | crown · goggles · monocle · longcape | 글리치 파티클 · 전설 왕관 포함 |
| neon | 네온 낙서 | **₩4,900** | ✗ | sunglasses · bellnecklace · fedora · wings | 낙서 트레일 · 전설 날개 포함 |
| sport | 스포츠 이펙트 | **₩3,900** | ✗ | furhat · goggles · scarf · shortcape | 스포츠 이펙트 · 응원 대사 풀 |
| ink | 컬러 잉크 | **₩3,900** | ✗ | clothhat · monocle · bowtie · longcape | 잉크 번짐 파티클 · 드로잉 무기 색 |
| mil | 밀리터리 | **₩4,900** | ✗ | sunglasses · furhat · stripedtie · shortcape | 거수경례 유휴 · 경례 격파 연출 |

```
배지   보유 "보유" #8FBF6A  /  미보유 "DLC" #C8A15A
버튼   보유 "적용 중" bg #221B14 color #C8A15A border #4A3A26
       미보유 "스팀에서 구매" bg #C8A15A color #160F06 border transparent
카드 테두리  보유 #4A4036 / 미보유 #231F1B
```
★ **가격이 2단(₩3,900 / ₩4,900)이다.** 전설 포함 팩(cyber·neon)이 ₩4,900이고 mil도 ₩4,900인데
mil에는 전설이 없다 — **가격 근거 불일치. `product-strategy` 판정 필요.**
★ **"스팀에서 구매"** — 채널이 스팀으로 명시돼 있다.

### 4-2. 개별 아이템 (`shopItems`) — **미보유만**

| 필드 | 값 |
|---|---|
| `statText` | `<부위 주스탯> +MAINV[등급] · <부스탯> +SUBV[등급]` |
| `priceText` | DLC면 `DLC 전용`, 아니면 `동전 <가격>` |
| `btnLabel` | DLC면 `스토어`, 아니면 `구매` |

동전 가격 사다리: **일반 600 · 희귀 1,400 · 영웅 3,200 · 전설은 동전 판매 안 함(DLC 전용)**

---

## 5. 팀 배정

| 항목 | 담당 |
|---|---|
| L-1~L-7 화면 배치·상태 | `ux-designer` |
| L-8 프리셋 3종 (신규 기능) | `ux-designer` → `coder-ui` |
| L-4 fps 3택 ↔ 적응형 페이싱 충돌 | `perf-doc` + `dev-platform` |
| 보관함 `refund 40%` | `design-systems` (수치) + 사용자 확인(기능 여부) |
| 상점 DLC 가격 2단 근거 · 스팀 채널 | `product-strategy` |
| 팩 스와치 6색 ↔ 우리 팔레트 | `design-art` |
| 등급색 `RAR_A` ↔ 우리 램프 | `design-art` (이미 R9로 갈라져 있다 — 대조 필요) |

## Windows 영향
**별도 배정 필요** — 이 문서는 판독 결과이고 코드 0줄이다. 다만 L-2 크기 범위(70~150)와
L-4 fps 3택은 **플랫폼별 동작이 다를 수 있어** 구현 라운드에서 반드시 양쪽을 함께 본다.
