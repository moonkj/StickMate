# Handoff: 데스크탑 캐릭터 정보창 / 장비창 (졸라맨)

## Overview
데스크탑 화면을 돌아다니는 졸라맨 캐릭터의 **캐릭터 정보창 + 장비창** UI입니다.
좌측에 캐릭터 프리뷰와 스탯, 우측에 카테고리별 장비 카드 그리드가 놓인 단일 패널 모달입니다.
장비는 카테고리(모자 / 안경 / 넥타이 / 망토)별로 섹션이 분리되고, 미보유 아이템은
자물쇠 배지 + 흐린 실루엣으로 잠금 표시됩니다. 아이템 32종 전부 개별 라인 아이콘이 그려져 있습니다.

## About the Design Files
이 폴더의 파일은 **HTML로 만든 디자인 레퍼런스**입니다. 의도한 외형과 동작을 보여주는
프로토타입이며, 그대로 프로덕션에 복사해 쓸 코드가 아닙니다.

작업 내용은 이 HTML 디자인을 **대상 코드베이스의 기존 환경(React / Vue / Electron 렌더러 /
Tauri / SwiftUI 등)에서 그 코드베이스의 기존 패턴과 라이브러리로 재현**하는 것입니다.
아직 환경이 정해지지 않았다면 프로젝트에 가장 적합한 프레임워크를 선택해 구현하십시오.
(데스크탑 오버레이 캐릭터라면 Electron/Tauri + React 조합이 자연스럽습니다.)

`캐릭터 정보창.dc.html`은 자체 런타임(`support.js`)에 의존하는 파일이므로, 이 런타임을
가져가지 말고 **마크업 구조·인라인 스타일 값·인터랙션 로직만** 읽어 재구현하십시오.
아이콘 SVG path와 아이템 데이터는 아래 `data/` JSON으로 따로 추출해 두었습니다.

## Fidelity
**High-fidelity (hifi)** — 색상, 타이포그래피, 간격, 반경, 상태 전환이 모두 최종값입니다.
아래 명세된 정확한 hex / px 값으로 픽셀 단위 재현이 가능합니다.
단, 캐릭터 아바타와 아이템 아이콘은 SVG 라인 스케치이며 **실제 캐릭터 아트로 교체될 자리표시자**입니다.
형태를 그대로 쓰든 교체하든, 크기(아바타 100×148, 아이콘 40×40)와 스트로크 굵기 규칙은 유지하십시오.

---

## Screens / Views

### 1. 캐릭터 정보창 (단일 패널, 모달)

**Purpose**: 캐릭터 상태 확인 + 장비/외형 아이템 착용·해제.

**Layout**
- 화면 전체: `min-height: 100vh`, flex center, `padding: 40px`, 배경 `#dcdbd7`
- 패널: `width: 880px`, 배경 `#fbfaf8`, `border: 1px solid #cfcdc7`, `border-radius: 12px`,
  `box-shadow: 0 18px 48px -18px rgba(30,29,26,0.35)`, `overflow: hidden`, 텍스트 `#1b1b19`
- 패널 내부 = 타이틀바(고정 높이) + 본문 2컬럼 그리드 `grid-template-columns: 244px 1fr`
- 컬럼 구분선: 좌측 컬럼 `border-right: 1px solid #ecebe7`

**Components**

#### 1.1 타이틀바
- `display: flex; align-items: center; gap: 10px; padding: 13px 16px;`
  `border-bottom: 1px solid #ecebe7`
- 좌측 라벨: "내 책상 동료" — 13px / weight 500 / `letter-spacing: -0.01em`
- 우측 힌트: "ESC" — IBM Plex Mono 11px / `#8b8981` / `letter-spacing: 0.06em`
- spacer(`flex: 1`)로 좌우 분리
- 닫기 동작: ESC 키. (별도 X 버튼 없음 — 필요하면 추가)

#### 1.2 좌측 컬럼 (244px)
`padding: 22px 20px; display: flex; flex-direction: column; gap: 18px;`

1. **이름 블록** (`gap: 3px`)
   - 이름: 19px / weight 600 / `-0.01em` — 예: `zion`
   - 서브: Mono 11px / `#8b8981` / `0.04em` — `Lv.4 · 적응 중인 동료`
2. **아바타 프레임**
   - `height: 196px; border: 1px solid #ecebe7; border-radius: 8px; background: #f4f3ef;`
     flex center
   - 내부: 졸라맨 SVG 100×148 (viewBox `0 0 100 148`), stroke `#1b1b19`, `stroke-width: 3`,
     `stroke-linecap: round`
   - 착용 아이템이 아바타에 반영됨 (아래 "아바타 오버레이" 참조)
3. **상태 문구**: 11px / `#8b8981` — `지금 · 걷는 중`
4. **게이지 2종** (컨테이너 `gap: 12px`, 각 게이지 `gap: 5px`)
   - 라벨행: Mono 10px / `#8b8981` / `letter-spacing: 0.08em`, 좌우 양끝 정렬
     (`STRESS` / `0% · 안정`, `EXP` / `193 / 492`)
   - 트랙: `height: 4px; border-radius: 2px; background: #eae8e3; overflow: hidden`
   - 채움: STRESS `width: 4%` / `#1b1b19`, EXP `width: 39%` / accent(`#c4622d`)
5. **스탯 리스트**
   - 컨테이너 `border-top: 1px solid #f1f0ec; padding-top: 4px`
   - 각 행: `display: flex; justify-content: space-between; padding: 7px 0;`
     `border-bottom: 1px solid #f1f0ec; font-size: 12px`
   - 키: `#8b8981` / 값: Mono / `letter-spacing: 0.02em`
   - 항목(현재 더미): 근속 `1일차`, 함께한 시간 `2시간 33분`, 격파 성공 `아직 없음`,
     대결 승리 `1승`, 활쏘기 명중 `4 / 12`, 넘어진 횟수 `5번`

#### 1.3 우측 컬럼 (1fr)
`padding: 22px 22px 24px; display: flex; flex-direction: column; gap: 18px;`

1. **탭바**
   - `display: flex; gap: 22px; border-bottom: 1px solid #ecebe7`
   - 탭 버튼: 배경/보더 none, `padding: 0 0 10px`, 13px, `letter-spacing: -0.01em`,
     `margin-bottom: -1px`
   - 활성: 색 `#1b1b19`, weight 600, `border-bottom: 2px solid #1b1b19`
   - 비활성: 색 `#9b9990`, weight 400, `border-bottom: 2px solid transparent`
   - 탭: `장비` / `외형` / `보관함`
2. **카테고리 섹션 리스트** (컨테이너 `gap: 20px`, 섹션 내부 `gap: 10px`)
   - **섹션 헤더** (`display: flex; align-items: center; gap: 8px`)
     - 틴트 도트: `7×7px`, `border-radius: 2px`, 카테고리 틴트 색
     - 카테고리명: 12px / weight 600 / `-0.01em`
     - 슬롯 코드: Mono 10px / `#a8a69e` / `0.06em` (HEAD, EYES, NECK, BACK …)
     - 구분선: `flex: 1; height: 1px; background: #f1f0ec`
     - 카운트: Mono 10px / `#a8a69e` — `보유수 / 전체수` (예 `1 / 4`)
   - **아이템 카드 그리드**: `grid-template-columns: repeat(4, 1fr); gap: 9px`
3. **선택 상세 패널**
   - `border: 1px solid #ecebe7; border-radius: 9px; padding: 14px 15px;`
     `background: #f9f8f5; display: flex; flex-direction: column; gap: 8px`
   - 상단행(`align-items: baseline; gap: 9px`): 이름 13px/600 · 메타 Mono 10px `#8b8981` `0.06em`
     (`카테고리 · 착용 중|보유 중|Lv.n에 열림`) · spacer · 액션 버튼
   - 액션 버튼: `border-radius: 6px; padding: 5px 13px; font-size: 11.5px`
     - 착용 가능: `background #1b1b19` / `color #fff` / `border 1px solid #1b1b19` / label `착용`
     - 착용 중: `background #ffffff` / `color #1b1b19` / `border 1px solid #1b1b19` / label `해제`
     - 잠김: `background #f2f1ed` / `color #b3b0a8` / `border 1px solid #e6e4df` /
       label `잠김` / `cursor: not-allowed` / 클릭 무시
   - 설명: 12px / `line-height: 1.6` / `#56544e`
     (잠김일 때 `레벨 n이 되면 열립니다. 지금은 실루엣만 보입니다.`)

#### 1.4 아이템 카드 (핵심 컴포넌트)
`border: 1px solid …; border-radius: 9px; padding: 11px 11px 10px;`
`display: flex; flex-direction: column; gap: 9px; transition: border-color .12s`

- **썸네일 영역**: `height: 62px; border-radius: 6px;` flex center, 내부 아이콘 SVG 40×40
- **이름**: 12px / weight 500 / `-0.01em` / 1줄 ellipsis (`white-space: nowrap; overflow: hidden`)
- **메타**: Mono 9.5px / `letter-spacing: 0.04em`

상태별 값:

| 상태 | border | background | thumb bg | 아이콘 색 | 이름 색 | 메타 텍스트 / 색 | cursor |
|---|---|---|---|---|---|---|---|
| 기본(보유) | `#ecebe7` | `#ffffff` | `#f6f5f2` | `#3a3833` | `#1b1b19` | `보유` / `#a8a69e` | pointer |
| 착용 중 | `#c9c6be` | `#ffffff` | 틴트 + `1a` 알파 (예 `#c4622d1a`) | 카테고리 틴트 | `#1b1b19` | `착용 중` / 틴트 | pointer |
| 선택됨 | `#1b1b19` (다른 상태보다 우선) | 위와 동일 | 위와 동일 | 위와 동일 | 위와 동일 | 위와 동일 | pointer |
| 잠김 | `#ecebe7` | `#f6f5f2` | `#efeeea` | `#8b8981` @ opacity 0.28 | `#b3b0a8` | `LV.{req}` / `#b3b0a8` | not-allowed |
| hover(전부) | `#b9b6ae` | — | — | — | — | — | — |

- 잠긴 카드 이름은 `???`로 표시.
- 잠긴 카드 썸네일 = 해당 아이템 아이콘을 `opacity: 0.28`, 색 `#8b8981`로 깔고,
  우측 하단(`right: -4px; bottom: -3px`)에 자물쇠 배지를 겹침.
  배지: `background: #efeeea; border-radius: 5px; padding: 1px 2px`, 자물쇠 14×15px, 색 `#a8a69e`.
  자물쇠 SVG(viewBox `0 0 20 21`): 몸통 `<rect x=3 y=9.5 w=14 h=10 rx=2.5 fill>` +
  고리 `<path d="M6.5 9.5 V6.8 a3.5 3.5 0 0 1 7 0 V9.5" stroke-width=1.7 linecap=round>`

#### 1.5 아바타 오버레이
착용 상태가 아바타에 즉시 반영됩니다 (현재는 카테고리 단위의 단순 표현):
- 모자 착용 시: `M31 24 h38 M38 24 q12 -14 24 0` (stroke `#1b1b19`, width 3)
- 안경 착용 시: `M38 34 h24` (stroke `#1b1b19`, width 4)
- 망토 착용 시: `M50 60 L78 96 L50 92 Z` (fill accent, `opacity: 0.85`)

실제 구현에서는 **아이템별** 아바타 레이어로 확장하는 것이 맞습니다.
레이어 순서 권장: 망토 → 몸 → 머리 → 머리스타일 → 안경 → 모자 → 이펙트 → 펫.

---

## Interactions & Behavior
- **탭 클릭** → `tab` 변경, `sel`은 유지되지만 다른 탭에서 찾지 못하면 첫 아이템으로 폴백.
  (구현 시 탭 전환하며 해당 탭 첫 아이템으로 선택 초기화하는 편이 안전)
- **카드 클릭** → `sel = "{카테고리}/{아이템명}"`. 잠긴 카드도 선택은 되고 상세에 잠금 안내가 뜸.
  (완전히 클릭 불가로 하려면 `cursor: not-allowed`에 맞춰 핸들러도 무시)
- **착용/해제 버튼** → `worn[카테고리]`를 아이템명 또는 `null`로 토글.
  카테고리당 1개만 착용(단일 슬롯). 착용 즉시 카드 상태와 아바타가 갱신됨.
- **잠금 규칙** → 아이템의 `req` 레벨이 캐릭터 `level`보다 크면 잠김. `req` 없으면 기본 보유.
  잠김 상태에서는 카드 이름을 `???`, 메타를 `LV.{req}`로 표시하고, 해금되면 실제 아이템 이름을
  그대로 노출. 레벨별 해금 아이템 이름은 `data/unlocks.json`의 `unlocksByLevel` 참조.
- **전환** → 카드 `border-color` 0.12s. 그 외 애니메이션 없음(의도적으로 조용한 UI).
- **호버** → 카드 보더만 `#b9b6ae`로. 확대·그림자 변화 없음.
- **반응형** → 고정 폭 880px 패널. 데스크탑 오버레이 전용이므로 반응형 불필요.
  좁은 화면 대응이 필요하면 2컬럼 → 1컬럼, 카드 그리드 4열 → 3열로 축소 권장.
- **로딩/에러 상태** — 이 디자인에는 없음. 로컬 상태로 즉시 반영되는 UI를 전제.

## State Management
```
level: number            // 캐릭터 레벨 → 잠금 계산 (기본 4)
tab: '장비' | '외형' | '보관함'
sel: string              // "카테고리/아이템명"
worn: { [카테고리]: 아이템명 | null }
                         // 기본값: { 모자: '천 모자', 안경: '선글라스', 넥타이: null, 망토: null }
```
- `보관함` 탭은 장비 + 외형 전체에서 `locked === false`인 아이템만 모아 단일 섹션으로 보여줌.
  이 탭에서는 메타 텍스트가 `보유` 대신 **카테고리명**으로 표시됨.
- 데이터 소스: `data/items.json` (카테고리·슬롯·틴트·아이템·요구 레벨·설명).
  실제 구현에서는 착용 상태와 보유 상태를 영속 저장(로컬 파일/DB)해야 함.
- 데이터 페칭 요구사항 없음 — 전부 로컬.

## Design Tokens

**Colors**
| 용도 | 값 |
|---|---|
| 화면 배경 | `#dcdbd7` |
| 패널 배경 | `#fbfaf8` |
| 카드 배경 (보유) | `#ffffff` |
| 카드 배경 (잠김) / 썸네일 기본 | `#f6f5f2` |
| 썸네일 배경 (잠김) | `#efeeea` |
| 아바타 프레임 배경 | `#f4f3ef` |
| 상세 패널 배경 | `#f9f8f5` |
| 패널 보더 | `#cfcdc7` |
| 구획 보더 | `#ecebe7` |
| 얇은 구분선 | `#f1f0ec` |
| 카드 보더 (착용) | `#c9c6be` |
| 카드 보더 (hover) | `#b9b6ae` |
| 텍스트 기본 | `#1b1b19` |
| 텍스트 본문 | `#56544e` |
| 아이콘 기본 | `#3a3833` |
| 텍스트 보조 | `#8b8981` |
| 텍스트 약함 | `#a8a69e` |
| 텍스트 비활성 | `#b3b0a8` |
| 탭 비활성 | `#9b9990` |
| 게이지 트랙 | `#eae8e3` |
| accent (기본) | `#c4622d` |

**카테고리 틴트**
| 카테고리 | 슬롯 코드 | 틴트 |
|---|---|---|
| 모자 | HEAD | `#c4622d` |
| 안경 | EYES | `#2d6a8f` |
| 넥타이 | NECK | `#5a7d4a` |
| 망토 | BACK | `#7a5a8f` |
| 표정 | FACE | `#c4622d` |
| 머리 | HAIR | `#2d6a8f` |
| 이펙트 | FX | `#5a7d4a` |
| 펫 | PET | `#7a5a8f` |

**Typography**
- 본문: `IBM Plex Sans KR` (300/400/500/600)
- 수치·코드·라벨: `IBM Plex Mono` (400/500)
- 스케일: 19px/600 (이름) · 13px/500–600 (타이틀·탭·상세명) · 12px (카드명, 스탯, 설명) ·
  11px (서브라벨) · 10px–9.5px Mono (슬롯·카운트·메타)
- `letter-spacing`: 한글 텍스트 `-0.01em`, Mono 라벨 `0.04em`~`0.08em`
- `line-height`: 설명문 1.6, 그 외 기본

**Spacing** — 3 / 5 / 8 / 9 / 10 / 11 / 12 / 13 / 14 / 16 / 18 / 20 / 22 px (4px 기준에 느슨하게 정렬)

**Radius** — 패널 12, 카드 9 · 상세 패널 9, 아바타 프레임 8, 썸네일 6 · 버튼 6, 자물쇠 배지 5,
틴트 도트 2, 게이지 2

**Shadow** — 패널만: `0 18px 48px -18px rgba(30,29,26,0.35)`

## Assets
외부 이미지 없음. 모든 그래픽은 인라인 SVG입니다.
- 아이템 아이콘 32종: `data/icon-paths.json` — 40×40 viewBox, `stroke-width: 1.7`,
  `stroke-linecap/linejoin: round`, `fill: none` 기준.
  각 아이콘은 파츠 배열이며 파츠 타입은:
  `["p", d]` = stroke path · `["f", d]` = fill path ·
  `["c", cx, cy, r]` = stroke circle (4번째 인자 있으면 `stroke-dasharray: "2 3"`) ·
  `["fc", cx, cy, r]` = fill circle
- 자물쇠 아이콘 / 졸라맨 아바타 SVG: 위 명세 및 `캐릭터 정보창.dc.html` 참조
- 폰트: Google Fonts (IBM Plex Sans KR, IBM Plex Mono)
- **주의**: 아이콘·아바타 모두 라인 스케치 자리표시자입니다. 실제 캐릭터 아트가 있으면
  같은 크기 규격으로 교체하십시오.

## Files
- `캐릭터 정보창.dc.html` — 디자인 원본 (마크업 + 인터랙션 로직). `support.js` 런타임 필요.
- `support.js` — 원본을 브라우저에서 열어 보기 위한 런타임. **구현에 가져가지 마십시오.**
- `data/items.json` — 8개 카테고리 × 4아이템 = 32종 데이터 (요구 레벨, 설명, 틴트, 슬롯 코드)
- `data/unlocks.json` — 시작 보유 아이템 + **레벨별 해금 아이템 이름 표** (잠금 해제 시 표시할 이름)
- `data/icon-paths.json` — 32종 아이콘 SVG path 데이터

원본을 그대로 보려면 `캐릭터 정보창.dc.html`을 브라우저에서 열면 됩니다
(같은 폴더에 `support.js`가 있어야 함).
