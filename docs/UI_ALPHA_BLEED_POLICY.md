# UI 알파 비침 정책 · 설정창 컨트롤 면 판정 (design-art, 2026-09-06 R14)

> **유지 소관(2026-09-26 리더 배정): `ux-designer`** — 이 문서의 최신성·인용 정합을 UX 표면 담당이 진다.
> 아래 「담당」은 **R14 집필자**(수치 사양의 저자)이고, 소관과 별개다.
> 담당: `design-art` — 팔레트 / 등급 색 체계 / 이펙트 색 / 투명도 체계
> 산출물 성격: **문서 + 숫자 사양**. 프로덕션 `.cs` 수정 0건.
> 실측기: `design/art/verify/alphableed.py` · `alphascan.py` · `alphapolicy.py` ·
> `settingsface.py` · `rarityflat.py` · `edgeonsurface.py` ·
> **`edgequant.py` · `edgemargin.py`(R14-b)**
> 로그: `design/art/alphableed.out.txt` · `alphascan.out.txt` · `alphapolicy.out.txt` ·
> `settingsface.out.txt` · `rarityflat.out.txt` · `edgeonsurface.out.txt` ·
> **`edgequant.out.txt` · `edgemargin.out.txt` · `edgefinal.out.txt`**
>
> **★ R14-b 개정(같은 날)** — `coder`가 `EdgeOnSurface`를 구현하며 8비트 양자화 손실을 실측 보고했고,
> 그 보고를 독립 재현한 뒤 **두 곳을 고쳤다**: 기본 목표 `3.0 → 3.15`(×1.05), 방향 규칙
> `흰쪽 우선 → 여유 큰 쪽`. §4-3-a / §4-3-b에 근거와 스윕이 있고, 그에 딸린 값 14곳을
> 문서 전체에 전파했다. **§4-3-c 표가 정본이다.**
>
> ★★ **2026-09-26 문서 ↔ 코드 정합 확인 (`ux-designer`)** — 이 문서의 **판정·수치·규칙은 그대로 살아 있다.**
> 그리고 인계 대상이던 **P0 전량이 코드에 착지했다.** 그래서 §5·§7-3 인계표의 「지금」 칸은
> **현재 상태가 아니라 R14 판(`349048f2`)의 기록**으로 읽어야 한다. 착지 증거(작업 트리 `fa25570` 기준, 고정 문자열 앵커):
> - `UiChrome.cs` 앵커 `public const float EdgeContrastTarget`이 `MinNonTextContrast * 1.05f`(=3.15)이고,
>   `EdgeOnSurface`의 방향 주석이 「여유가 큰 쪽」이다 — §4-3 개정 ①②가 **글자 그대로** 들어갔다.
> - `UiChrome.RarityBorder(` 호출부 **7곳 전부**가 `UiChrome.Flatten(` 안에 있다(§1-E의 「10번째 출처」가 닫혔다).
>   그중 2곳은 R14 때 없던 파일(`CharacterInfoWindow.Dlc.cs` · `CharacterInfoWindow.Shop.cs`)이다.
> - §4-3이 「사라진다」고 예고한 리터럴 `new Color(1f, 1f, 1f, 0.18f)`는 `CharacterInfoWindow*.cs`에서 **0건**이다.
>   그 삼항식은 지금 `UiChrome.cs`의 `EdgeOnSurface` 문서 주석이 **역사로** 인용한다.
> - §7-2 채택값은 `SettingsControls.ControlFaceOnCard` · `ControlFaceOnPanel`로 배선됐고, §7-3 ★ 경고
>   (게이트가 면을 알아야 한다)는 `GatedInk`가 **면을 들고** 잉크를 그 면에서 뽑는 형태로 예방됐다.
> - §9 감사는 `Tests/EditMode/UiAlphaBleedPolicyAuditTests` · `EdgeOnSurfaceTests`로 실재하고,
>   §9-3이 요구한 **8비트 반올림 뒤 회색 램프 256단 전수**와 **절벽 양쪽 1/255 찌르기**가 그 안에 있다.
> - §4-2의 α 9종과 §4-3-c의 바탕 색값은 `UiChrome.cs` 선언에서 **하나도 바뀌지 않았다.**
>   그래서 이 문서의 표는 지금도 계산이 선다.
>
> **틀린 것 · 안 닫힌 것은 §12 정정 기록에 모았다**: §4-4의 월드 렌더러 「4건」은 **3건**이고(파일 1개 삭제),
> §3-4의 「이 60줄」은 **54줄**이며, §5-4 처방은 **여전히 미채택(P2)이다.**

---

## §0 교정 — 이걸 통과 못 하면 아래 숫자는 전부 폐기다

계산기를 새로 만들었으므로 **알려진 값으로 먼저 교정**했다. 10건 전부 통과(로그 상단):

| 검사 | 기대 | 실측 |
|---|---|---|
| 흰 vs 검 대비 | 21.00 | 21.000000 |
| 동일색 대비 | 1.00 | 1.000000 |
| `#767676` vs 흰 (WCAG 예제) | 4.54 | 4.542225 |
| ΔE(동일색) | 0 | 0.000000 |
| ΔE(흰, 검) | 100.0 | 100.000001 |
| 비침(α=1) | 0 % | 0.000000 |
| 비침(α=0) | 100 % | 1.000000 |
| 비침(α=0.55) = 1−0.55² | 69.75 % | 0.697500 |
| Flatten(흰 α1 onto 검) | 흰 | 1.000000 |
| Flatten(흰 α0 onto 검) | 검 | 0.000000 |

**독립 교차검증 3건** — 내 계산기가 이 저장소가 이미 적어 둔 값을 재현하는가:

| 이미 적혀 있던 값 | 출처 | 내 실측 | 차 |
|---|---|---|---|
| 등급 테두리 인접 ΔE 9.06 / 11.13 / 11.01 | `UiChrome.cs` 앵커 `9.06 / 11.13 / 11.01` (§15.6-a 검산 1) | 9.06 / 11.12 / 11.00 | ≤ 0.006 |
| 전설 테두리 카드 대비 4.45 | `UiChrome.cs` 앵커 `그 최대(전설)가 카드면 대비` | 4.45 | 0.00 |
| `CardBorderHover` 실효색 `#A8AAAD` · 7.09 | `UiChrome.cs` 앵커 `α0.62에서 실효색` | `#A8AAAD` · 7.09 | 0.00 |

교정이 섰으므로 아래 숫자를 신뢰한다.

### 0-1 비침 공식 두 개를 구분한다 — 보고서의 표가 섞여 있었다

uGUI 기본 셰이더는 `Blend SrcAlpha OneMinusSrcAlpha`를 **알파 채널에도** 적용한다
(`dstA' = srcA² + dstA(1−srcA)`, `UiChrome.cs` 머리 앵커 `RGB<b>와 알파에 똑같이</b> 적용된다`). 그래서 비침은 **밑에 무엇이 있느냐**로 갈린다:

| | 식 | α0.55 | α0.14 | α0.10 |
|---|---|---|---|---|
| **빈 프레임버퍼 위**(바탕화면 직접) | `1 − α²` | **69.75 %** | 98.04 % | 99.00 % |
| **불투명 겹 위**(`dstA=1`) | `α(1−α)` | **24.75 %** | 12.04 % | 9.00 % |

★ 인계 보고서의 표(24.75 / 23.56 / 13.44 / 12.04 / 9.00)는 **전부 아래 줄**이다 — 즉
"우리 창 안"의 값이다. **창 밖(부채꼴 버튼·이름표·온보딩 알약)은 위 줄이라 훨씬 나쁘다.**
이 구분이 §2와 §3의 판정을 가른다.

---

## §1 판정 요약 (두 과제, 한 페이지)

### 과제 1 — raw 알파 토큰

| 결정 | 내용 |
|---|---|
| **1-A. `AddSurface`/`AddOutline`을 항상 Flatten하게 바꾸지 않는다** | **반려.** 근거 5개, 전부 숫자 있음(§2). 요약: 그 함수는 **밑에 깔린 색을 모른다**. 실제 바탕이 6종이고, 그중 하나(`CharacterInfoWindow.cs` 앵커 `_inkRings[i].color = active ? SelectedRingOn(fill)`, 잉크색 견본 링)는 **런타임에 유저가 바꾸는 색**이다. |
| **1-B. 구조적 수정은 「토큰이 아니라 호출부에서 `Flatten(토큰, 바탕)`」으로 한다** | 이 저장소가 **이미 16곳에서 쓰고 있는 관용구**다. 새 관용구를 만들지 않는다. 강제는 함수 시그니처가 아니라 **소스 감사 테스트**로 한다(§9). |
| **1-C. 테두리 계열은 `Flatten`이 아니라 새 파생 규칙 `EdgeOnSurface(바탕)`으로 닫는다** | 흰색 α를 얹는 방식은 **밝은 바탕에서 원리상 실패**한다(종이 무대 1.02:1, 흰 잉크 견본 1.00:1). `ControlFaceOnSurface`/`InkOnSurface`와 **같은 형태의 세 번째 문**이고, 이걸 만들면 리터럴 `new Color(1f,1f,1f,0.18f)`는 **패치되는 게 아니라 사라진다**(§4-3). |
| **★ 1-C-b (R14-b 개정, `coder` 구현 실측 반영)** | 목표 = **`MinNonTextContrast × 1.05` = 3.15** (3.0 아님). 방향 = **「여유 큰 쪽」**(「흰쪽 우선」 아님). 근거: 3.0으로 두면 8비트 반올림 후 **회색 램프의 40.6 %·유채색의 37.7 %가 미달**로 떨어지고, 「흰쪽 우선」은 절벽이 `target`에 딸려 움직여 **마진 상수 하나가 테두리 색을 흰↔검으로 뒤집는다**. §4-3-a / §4-3-b. |
| **1-D. 코너 AA 램프는 이 라운드를 막지 않는다** | 실측 결과 Flatten은 코너에서 **비침을 줄인다**(D계수 0.46→0.25, dstA 0.236→0.375). 그리고 **이미 출하돼 있다** — `AddOpaquePanel`(`UiChrome.cs` 앵커 `public static RectTransform AddOpaquePanel`)이 2026-08-31부터 보더를 Flatten해 왔고 창 4개에서 6일간 코너 신고 0건. **다만 바깥 실루엣 4곳은 실기 캡처 항목으로 남긴다**(§8). |
| **1-E. 대상은 9종이 아니라 10종이다** | 인계본이 못 본 것: **`UiChrome.RarityBorder()` α0.55**(함수 반환이라 토큰 grep에 안 걸린다, 4줄, **카드 24장 × 탭 4개의 테두리 전부 + 보관함 행 전부** = 최악 비침의 **최대 면적**). 반대로 인계본 목록의 `Cards.cs:509`(R14 판 `349048f2` 기준. 지금 그 파일의 이름은 `CharacterInfoWindow.Cards.cs`다)는 **오탐**이다(여러 줄에 걸친 `Flatten(`의 인자다 — 지금 앵커는 `생 CardBorder/AccentBorder(α<1)를 그대로 얹지 않는다`). |
| **1-F. 「60곳」은 (토큰, 줄) 쌍의 수이고 실제 소스 줄은 58이다** | 한 줄이 토큰 두 개를 쓰는 경우가 있다(`Cards.cs:509`의 `CardBorder`×2 + `AccentBorder`). 고칠 대상을 셀 때는 **줄**로 세라 — 쌍으로 세면 진행률이 부풀어 보인다. 58 = 공장 호출 29 + `.color=` 대입 16 + 삼항/Lerp/Fade/정적필드 13. 리터럴 α0.18은 **이미 그 58 안**에 있다(같은 줄에 `CardBorder`가 함께 있다). |

### 과제 2 — 설정창 컨트롤 면

| 결정 | 값 |
|---|---|
| **2-A. 목표 대비 = 3.60이 아니다. 3.60은 물리적으로 도달 불가능하다** | 실측: 라벨이 면 위에 얹히는 버튼에서 **면 3.30~3.93 구간은 해가 없다**(골짜기). 이 저장소가 선언한 `ControlFaceContrastTarget = 3.60`이 **정확히 그 골짜기 안**이다(§7-1). |
| **2-B. 채택값: `ControlFaceOnSurface(바탕)` — 새 상수 0개** | 카드 위 **`#838589` 면 4.49 : 1** / 잉크 `#0B1016` 5.19 : 1. 창 바탕 위 **`#848588` 면 4.88 : 1** / 잉크 5.18 : 1. |
| **2-C. 「가장 튀는 요소가 된다」는 반증됐다** | 적용 후 카드 위 밝기 서열 9단 중 **5위**다: 라벨 14.99 > 본문 7.93 > 고른 세그먼트 6.83 > 캡션 5.32 > **버튼 면 4.49** > 테두리 3.16 > 트랙 1.31 > 구분선 1.22 > 안 고른 세그먼트 1.00. |
| **2-D. 「전부 3.6으로」가 아니다. 부품 종류가 넷이고 처방이 넷이다** | F1 누를 수 있는 면 → 4.49/4.88 · F2 입력칸 → 면 유지 + 테두리 **3.16** · F3 트랙/홈 → **손대지 않는다** · F4 견본/안 고른 세그먼트 → 테두리 **3.16**(§7-3). |
| **2-E. 면만 바꾸면 화면이 지워진다 — 면과 잉크는 한 쌍이다** | `#838589` 위에서 현 콜사이트의 잉크는 `TextPrimary` **3.34** / `TextSecondary` **1.77** / `TextTertiary`(플레이스홀더) **1.18**로 전부 붕괴한다. 반드시 `InkOnSurface(면, …)`로 함께 갈아탄다. |

---

## §2 과제1-A — 왜 `AddSurface`/`AddOutline`을 고치지 않는가 (근거 5)

### (1) 그 함수는 `onto`를 모른다. 그리고 실제 바탕은 6종이다

`Flatten(over, onto)`는 **두 인자**가 필요하다. 공장 함수는 두 번째를 갖고 있지 않다.
고정 가정(예: 항상 `PanelSurface`)을 박으면 나머지 5종 위에서 **틀린 색**이 나온다:

| 같은 `CardBorder` α0.10을 | 합성 결과 |
|---|---|
| on `PanelSurface` `#14171C` | `#2B2E33` |
| on `CardSurface` `#1B1F26` | `#32353C` |
| on `CardSurfaceMuted` `#15181E` | `#2C2F35` |
| on `SubtleSurface` `#191D24` | `#30343A` |
| on `ThumbSurfaceLocked` `#101318` | `#282B2F` |
| on `PortraitSurface` `#E9EAE6` | `#EBECE9` |

이 저장소는 **이미 이 사실을 알고 두 벌을 두었다**(`SettingsControls.ButtonSurfaceOnCard` /
`ButtonSurfaceOnPanel`, 그 주석: *"밑에 깔린 색이 다르면 같은 토큰이라도 합성 결과가 달라야 한다"*).
공장이 하나를 추측하면 **지금 맞게 그려지는 곳을 틀리게** 만든다.

### (2) 공장을 고쳐도 **58줄 중 29줄(50 %)만** 닫힌다

실측 분류(`alphascan.py`, 오탐 `Cards.cs:509` 제외 후 **소스 줄 기준**).
★ 이 절의 「파일:줄」과 줄 수는 전부 **R14 판(`349048f2`) 기준**이다 — 그때의 결함 분포를 말하는 계량이고,
지금은 §5 인계가 착지해 이 분포 자체가 과거형이다(문서 머리 ★ 블록):

| 형태 | 줄 수 | 공장 수정으로 닫히나 |
|---|---|---|
| `AddSurface / AddOutline / AddCircle(…, 토큰, …)` | **29** | ✔ |
| `image.color = 토큰` (생성 **후** 대입) | **16** | ✘ |
| 삼항식 / `Color.Lerp` / `Fade` / 정적 필드 초기화 | **13** | ✘ |
| **합계** | **58** | **29 (50 %)** |

(`RarityBorder()` 4줄과 리터럴 α0.18 1줄은 위 세 칸에 **이미 분산돼 있다** — 따로 더하지 마라.)

**절반만 닫는 수정은 안 하느니만 못하다** — 나머지 절반이 남았는데 "구조적으로 닫았다"는
기록이 생기기 때문이다. 이 저장소는 그 실패 모드를 이미 겪었다(부재 단언 니들 61건, CLAUDE.md).
그리고 남는 29줄이 하필 **가장 위험한 쪽**이다: `.color=` 대입은 **상태가 바뀔 때마다 매번**
실행되므로, 생성 시점 한 번만 고치는 공장 수정은 **호버/선택/착용 상태에서 곧바로 원상복구된다**
(`Cards.cs:979`가 만든 옳은 색을 `Cards.cs:430/432`가 매 갱신마다 덮어쓰는 것이 정확히 그 형태다).

### (3) α<1 중 **옳은 것**이 섞여 있다. 공장은 그걸 구분할 수 없다

- `AddSurface(display, "NameHit", Color.clear, …)` (`CharacterInfoWindow.cs` 앵커 `AddSurface(display, "NameHit", Color.clear`),
  `AddSurface(stripRect, "Tab"+name, Color.clear, …)` (`CharacterInfoWindow.Tabs.cs` 앵커
  `AddSurface(stripRect, "Tab" + name, Color.clear`), 같은 파일의 탭 면 갱신 앵커 `active ? UiChrome.Accent : Color.clear` —
  **칠하지 않는 판**이다. `Flatten(Color.clear, onto)`는 정확히 `onto`를 돌려주므로 이것들이
  **불투명 판으로 변한다**. 탭 면은 그 아래 탭 스트립의 테두리를 덮는다.
- `SettingsControls.AddHitArea`(α=0) · `TodoPostItWidget.cs`의 α0.001 두 줄(앵커 `new Color(0f, 0f, 0f, 0.001f)`, 그 파일에 2회) —
  α=0은 `dstA' = 0 + dstA·1`로 **프레임버퍼 알파를 건드리지 않는다**. 안전하고, 그대로 둬야 한다.
- 부채꼴의 펼침/수축 `Fade(색, alpha)` — **의도된 페이드**다. 쉬는 상태는 `alpha = 1f`
  (`GearRadialMenuWidget.cs` 앵커 `b.Progress = 1f;`의 그 분기)이므로 Flatten 후에는 정지 상태에서 비침 0 %가 되고,
  애니메이션 중의 비침은 원래 하려던 연출이다.

### (4) ★ 바탕이 **런타임에 유저 손으로 바뀌는** 자리가 있다

`CharacterInfoWindow.cs` 앵커 `_inkRings[i].color = active ? SelectedRingOn(fill)`(생성부는 `UiChrome.EdgeOnSurface(fill.color), 1.5f`) — 잉크색 견본의 링. 그 링이 올라앉은 면은
`_config.primaryOutlineColor`(검정) 또는 `_config.whiteInkColor`(흰색)다.
공장이 어떤 고정 바탕을 가정하든 **둘 중 하나에서는 반드시 틀린다.**

실측으로 이 자리는 지금 **이미 깨져 있다**:

| 상태 | 색 | 검정 견본 대비 | 흰 견본 대비 |
|---|---|---|---|
| 비선택 링 = `PanelBorder` α0.16 (raw) | `#292929` / `#FFFFFF` | **1.44** ✘ | **1.00** ✘ |
| 선택 링 = `TextPrimary` | `#F2F4F7` | 19.06 ✔ | **1.10** ✘ |

즉 **흰 잉크를 고르면 「지금 고른 것」 표시가 사라진다.** Flatten 한 겹으로는 못 고친다 —
§4-3의 `EdgeOnSurface`가 필요한 이유가 이것이다.

### (5) 코너 AA는 반대 방향의 근거다 — §3

---

## §3 과제1-D — 둥근 코너 AA 램프, 실측

### 3-1 모형

본체(α=1, 스프라이트 커버리지 `a`) 위에 같은 사각형·같은 반지름의 테두리를 얹는다.
`AddOutline`은 `Stretch()`로 부모를 꽉 채우므로 **두 램프의 기하가 일치**한다.
바탕화면 색을 `D`, 본체를 `P`, 토큰 α를 `k`라 하면

```
raw  : lerp(lerp(D,P,a), 흰, k·a)          → D 계수 = (1−a)(1−k·a)
flat : lerp(lerp(D,P,a), F, a)             → D 계수 = (1−a)²        (F = Flatten(흰 k, P))
```

### 3-2 실측 (`alphapolicy.py` §D)

| `a` | D계수 raw | D계수 **flat** | dstA raw | dstA **flat** |
|---|---|---|---|---|
| 0.25 | 0.7312 | **0.5625** | 0.0616 | **0.1094** |
| 0.50 | 0.4750 | **0.2500** | 0.2400 | **0.3750** |
| 0.75 | 0.2313 | **0.0625** | 0.5259 | **0.7031** |
| 1.00 | 0.0000 | 0.0000 | 0.9100 | **1.0000** |

(k = 0.10 기준. k = 0.16 / 0.55도 같은 방향, 로그 참조.)

**Flatten은 코너에서 바탕화면을 덜 보여 준다.** D계수가 절반이고 dstA가 최대 +19.4 %p 높다.
지적된 위험("밑에 깔린 것이 프레임 채움이 아니라 패널 바탕인 구간")은 실재하지만,
그 구간이 만드는 것은 **구멍이 아니라 반 픽셀의 과잉 커버리지**다.

### 3-3 그 과잉이 눈에 보이는가

| 토큰 | 바탕화면 흰 | 중간 회색 | 검 |
|---|---|---|---|
| `CardBorder` k0.10 | ΔE **20.08** (a=0.54) | 9.41 | 3.61 |
| `PanelBorder` k0.16 | ΔE **19.38** (a=0.54) | 9.45 | 2.47 |
| `AccentBorder` k0.55 | ΔE **10.20** (a=0.52) | 4.88 | 1.26 |

흰 바탕화면에서 변별 하한 7.8을 넘는다. **다만 그 편차가 사는 곳은 램프 한 겹**이고,
램프 폭은 원 스프라이트에서 `max(0.75, half−outer)` 텍셀 = Ø44 버튼 기준 **0.26 pt**
(Retina에서 약 0.5 물리 픽셀), 둥근 사각형에서는 `clamp01(0.5 − outside)` = **1 텍셀**이다.

### 3-4 그래서 판정

1. **58줄 중 54줄은 코너 노출이 0이다.** 전부 불투명한 부모 표면 위에 있다
   (팝오버 3종은 `PopoverPanel.cs` 앵커 `AddOpaquePanel(canvasGo.transform, "Panel"`,
   정보창은 `CharacterInfoWindow.cs` 앵커 `AddOpaquePanel(canvasGo.transform, "InfoPanel"`,
   설정창은 `SettingsWindow.cs` 앵커 `AddOpaquePanel(canvasGo.transform, "SettingsPanel"`,
   포스트잇은 `TodoPostItWidget.cs` 앵커 `AddOpaquePanel(canvasGo.transform, "PostItPanel"`).
   이 ~~60줄~~ **54줄**(2026-09-26 정정 — §12 ㉯: 같은 항목 첫 줄이 이미 54라고 적고 있어 문서가 자기와 어긋나 있었다)에 대해
   **코너 위험은 논점이 아니다** — 램프 아래에 있는 것은 항상 불투명한 부모다.
2. **나머지 4줄만 바깥 실루엣이다** — R14 판(`349048f2`) 기준 `GearRadialMenuWidget.cs:2204 / 2238 / 2277`과
   그 세 줄을 매 프레임 다시 칠하는 `:1552 / 1661 / 1688 / 1695`(같은 세 개체)다. §8 목록.
   그 형태는 **이미 출하돼 있다** —
   `AddOpaquePanel`이 `Flatten(PanelBorder, PanelSurface)`로 보더를 그린 지 6일이고
   창 4개(정보창·설정창·팝오버 3종·포스트잇)가 전부 그 코드를 탄다. 코너 신고 0건.
3. 따라서 **문서 판정만 내고 전부 보류**는 과하다. **54줄은 지금 고치고, 4줄은 고치되
   실기 캡처로 확인 도장을 받는다**(§8).

---

## §4 과제1 — 토큰별 목표 불투명색 확정표

### 4-1 규칙

> **α<1 색이 `Image.color`에 도달하는 경로는 셋뿐이다.**
> (가) `Flatten(토큰, 실제 바탕)` — 바탕이 컴파일 시점에 알려진 불투명색일 때
> (나) `EdgeOnSurface(바탕)` — **테두리**이고 바탕이 밝을 수도 어두울 수도 있을 때(§4-3)
> (다) `Fade(위 둘 중 하나, 애니메이션α)` — **의도된 페이드**일 때만
>
> **α를 hex로 손으로 적지 마라.** 아래 표의 hex는 *검산용*이지 붙여넣기용이 아니다 —
> 하드코딩하면 `PanelSurface`가 바뀌는 날 두 벌이 갈라진다(`SettingsControls` 색 절의 규칙 그대로).

### 4-2 확정표 (α=1 등가색, 실제 호출부가 올라앉은 바탕만)

| 토큰 | α | 불투명 위 비침 | on Panel `#14171C` | on Card `#1B1F26` | on Muted `#15181E` | on Subtle `#191D24` | on Locked `#101318` |
|---|---|---|---|---|---|---|---|
| `Divider` | 0.07 | 6.51 % | `#24272C` | `#2B2F35` | — | — | — |
| `TrackBackground` | 0.09 | 8.19 % | `#292C30` | `#30333A` | `#2A2D32` | — | — |
| `PanelSheen` | 0.10 | 9.00 % | `#2B2E33` | — | — | — | — |
| `CardBorder` | 0.10 | 9.00 % | `#2B2E33` | `#32353C` | `#2C2F35` | `#30343A` | `#282B2F` |
| `AccentSurface` | 0.14 | **12.04 %** | `#2D2A25` | `#33312D` | `#2E2B26` | — | — |
| `PanelBorder` | 0.16 | 13.44 % | `#3A3C40` | `#404349` | — | — | — |
| `PanelHighlight` | 0.30 | 21.00 % | `#5A5D60` | — | — | — | — |
| `AccentBorder` | 0.55 | **24.75 %** | `#77633E` (3.11) | `#7A6643` (3.01) | — | `#796642` (3.04) | — |
| `CardBorderHover` | 0.62 | 23.56 % | — | `#A8AAAD` (7.09) | `#A6A7AA` (7.40) | — | — |
| **`RarityBorder()`** ★ | **0.55** | **24.75 %** | — | 아래 표 | 아래 표 | — | 아래 표 |

★ **인계본이 못 본 10번째 출처.** `UiChrome.RarityBorder()`는 함수 반환이라 토큰 grep에 안 걸린다.
`RarityBorderAlpha = 0.55f`(`UiChrome.cs` 앵커 `private const float RarityBorderAlpha`)로 **최악값과 동률**이고, 붙는 자리가
**카드 24장 × 탭 4개 + 보관함 행 전부 + 상세 썸네일**이라 **이 앱에서 최악 비침의 최대 면적**이다.

#### 등급 테두리 Flatten 등가색 — 세 바탕 전부에서 보증이 산다

| 바탕 | 일반 | 희귀 | 영웅 | 전설 | 인접 최소 ΔE (하한 7.8) | 4상태 서열 |
|---|---|---|---|---|---|---|
| `CardSurface` | `#62615E` 2.67 | `#746D5E` 3.20 | `#867858` 3.79 | `#988251` **4.45** | **9.06** (+16.1 %) | 유지 |
| `CardSurfaceMuted` | `#5F5E5B` 2.74 | `#71695A` 3.29 | `#847454` 3.91 | `#967F4E` 4.60 | **9.11** (+16.8 %) | 유지 |
| `ThumbSurfaceLocked` | `#5D5C58` 2.77 | `#6F6757` 3.33 | `#817252` 3.96 | `#937D4B` 4.66 | **9.14** (+17.2 %) | 유지 |

**Flatten은 이 자리에서 보이는 색을 한 톤도 바꾸지 않는다** — `UiChrome.cs` 앵커 `9.06 / 11.13 / 11.01`이 적어 둔
9.06 / 11.13 / 11.01과 전설 4.45는 **애초에 「합성 후」 기준으로 계산된 값**이었다
(내 실측 9.06 / 11.12 / 11.00 / 4.45, 차 ≤ 0.006). 즉 문서는 옳았고 코드만 α를 안 접고 있었다.
서열(선택 14.99 > 호버 7.09 > 착용 6.19 > 전설 4.45)도 세 바탕 전부에서 유지된다.

### 4-3 ★ 새 파생 규칙 `EdgeOnSurface(바탕)` — 리터럴 α0.18이 사라지는 자리

**문제**: 흰색 α를 얹는 방식은 **밝은 바탕에서 원리상 실패**한다.
`Flatten(CardBorder, PortraitSurface)` = `#EBECE9`, 종이 무대 대비 **1.02**.
`Flatten(PanelBorder, 흰 견본)` = `#FFFFFF`, 대비 **1.00**. 흰색 위에 흰색을 더 얹을 수는 없다.

**규칙 — ★ 2026-09-06 개정(R14-b). 초판 두 곳이 틀렸고 `coder` 구현 실측이 잡았다.**

```
EdgeContrastTarget = MinNonTextContrast * 1.05      // = 3.15   ← 개정 ①

EdgeOnSurface(backdrop, target = EdgeContrastTarget)
  = <여유가 큰 쪽>으로                                            ← 개정 ②
      (흰쪽 최대 = 1.05/(L+0.05), 검은쪽 최대 = (L+0.05)/0.05)
    target을 만족하는 <최소> 혼합의 Flatten 결과 (항상 α = 1)
    ※ 고른 쪽에 해가 없으면 반대쪽으로 강제 반전
```

**§4-3-a 개정 ① — 기본 목표를 `MinNonTextContrast`(3.0)에서 `×1.05`(3.15)로 올린다**

`coder` 보고를 독립 재현했다(`edgequant.py` J-1): 부동소수 해는 3.0005\~3.0096인데
8비트로 반올림하면 선언 바탕 17종 중 **7종이 2.9895\~2.9999**. 최악 종이 무대 2.9895.

**그런데 17종은 표본이고, 진짜 규모는 그보다 크다**(`edgemargin.py` K-1/K-2):

| 모집단 | 양자화 후 미달 | 최악 |
|---|---|---|
| 회색 램프 256단 전수 | **104 / 256 (40.6 %)** | `#121212` → 2.9790 (**−0.699 %**) |
| 유채색 격자 4096색 | **1545 / 4096 (37.7 %)** | `#B0B0A0` → 2.9810 (−0.632 %) |

★ **그러므로 「3.0 그대로 두고 8비트 반올림 후 2.99대를 허용한다」는 반려한다.**
그건 예외를 허용하는 게 아니라 **가능한 바탕의 약 40 %에서 정책이 성립하지 않는다**는 뜻이다.
`MinNonTextContrast`는 WCAG 2.2 §1.4.11에서 그대로 인용해 온 **하한**이고
(`UiChrome.cs` 앵커 `public const float MinNonTextContrast`), 하한은 부동소수 중간값이 아니라 **화면에 나가는 픽셀**에서 서야 한다.

배수 스윕(전 색공간 기준, K-3):

| 배수 | target | 회색 미달 | 유채 미달 | 전체 최악 |
|---|---|---|---|---|
| 1.000 | 3.0000 | 104 | 1545 | 2.9790 |
| 1.004 | 3.0120 | 23 | 135 | 2.9898 |
| 1.006 | 3.0180 | 5 | 4 | 2.9962 |
| **1.008** | 3.0240 | **0** | **0** | 3.0028 ← 최소해 |
| 1.010 | 3.0300 | 0 | 0 | 3.0114 |
| **1.050** | **3.1500** | **0** | **0** | **3.1269** ← **채택** |

**왜 최소해(1.008/1.010)가 아닌가**: 최소해를 상수로 박으면 여유가 0이다 —
`UiChrome.cs`가 `ControlFaceLift`에서 **이미 같은 말을 적어 놓았다**(앵커는 바로 아래 인용문)
(*"최소 해를 그대로 상수로 박으면 여유가 0이 된다"*).

**왜 `ControlFaceContrastTarget`의 ×1.20을 그대로 안 쓰는가** — 두 근거:

1. **커버할 축의 수가 다르다.** 면은 **잉크와 한 쌍**이라 면을 밝히면 잉크가 죽는
   양방향 싸움을 안고 있고(§7-1의 골짜기), 양자화 + 감마 + 모니터 프로파일 세 축을 덮어야 한다.
   테두리는 **잉크가 안 얹힌다** — 덮을 축이 양자화 하나다. 실측 최악 손실이 **0.699 %이고**
   ×1.05의 여유는 **+4.37 %로** 그 **6.2배**다. 마진을 베끼지 않고 **잰 값에 맞춰 깎았다.**
2. **×1.20은 §7-4의 층을 뭉갠다.** F1 버튼 면이 4.49인데 테두리가 3.60이면
   **1.25배**밖에 안 떨어져 「면이 있는 것」과 「테두리만 있는 것」이 같은 단으로 읽힌다.
   ×1.05에서는 **1.42배**로 3.0안(1.49배)과 사실상 같다. 캡션(5.32) > 테두리(3.16) 층도 유지된다.

**§4-3-b 개정 ② — 방향 규칙을 「흰쪽 우선」에서 「여유 큰 쪽」으로 바꾼다**

`coder`가 「부수 발견」으로 올린 것(`ChromeButtonSurface` 위에서 거의 흰색 `#F0F0F1`)은
**실해 없는 특이점이 아니라 절벽이다.** 흰쪽 해가 존재할 조건은 `target ≤ 1.05/(L+0.05)`이므로
**절벽 위치가 `target`에 딸려 움직인다**(K-4):

| 바탕 | L | 절벽 배수 |
|---|---|---|
| `ChromeButtonSurface` `#898B8E` | 0.2575 | **1.1381** |
| `ControlFace on Card` `#838589` | 0.2355 | **1.2261** |

즉 **배수 1.20을 골랐다면** `#898B8E`는 검은 테두리(`#343536`), `#838589`는 흰 테두리(`#FCFDFD`)를
받는다 — **ΔL\* 2 이내의 두 면이 정반대 색 테두리를 받는다.** 마진 상수 하나가 색을 뒤집는 구조다.

「여유 큰 쪽」으로 바꾸면 절벽이 `L* = √0.0525 − 0.05 = **0.1791**` 한 점에 고정되고
**`target`과 완전히 무관해진다** — 나중에 마진을 조정해도 방향은 안 흔들린다.

그리고 **9종 중 7종은 결과가 비트 단위로 같다**(K-5). 달라지는 둘은 밝은 크롬 칩인데,
거기서는 **어두운 테두리가 정답**이다: 그 면들은 이미 `BrightTextBackdrops`에 등록돼 있어
`InkOnSurface`가 어두운 잉크(`OnAccentSolid`)를 고른다(`UiChrome.cs` 앵커 `public static readonly Color[] BrightTextBackdrops`).
테두리만 흰쪽으로 가면 **같은 칩에서 잉크와 테두리가 반대 방향**이 된다.

**§4-3-c 확정표 (개정 반영, `target = 3.1500`, 방향 = 여유 큰 쪽)**

| 바탕 | 바탕색 | 방향 | 혼합 α | 테두리 | 양자화 후 |
|---|---|---|---|---|---|
| `PanelSurface` | `#14171C` | 흰 | 0.3438 | `#65676A` | 3.1662 |
| `CardSurface` | `#1B1F26` | 흰 | 0.3457 | **`#6A6D71`** | 3.1786 |
| `CardSurfaceMuted` | `#15181E` | 흰 | 0.3438 | `#65676B` | 3.1370 |
| `SubtleSurface` | `#191D24` | 흰 | 0.3447 | `#686B6F` | 3.1558 |
| `ThumbSurfaceLocked` | `#101318` | 흰 | 0.3447 | `#626468` | 3.1391 |
| `InkContrastCharcoal`(흰 잉크 무대) | `#25282E` | 흰 | 0.3535 | **`#727478`** | 3.1547 |
| `PortraitSurface`(검 잉크 무대) | `#E9EAE6` | **검** | 0.4404 | **`#828381`** | 3.1521 |
| 검정 잉크 견본 | `#000000` | 흰 | 0.3623 | `#5C5C5C` | 3.1405 |
| 흰 잉크 견본 | `#FFFFFF` | **검** | 0.4316 | `#919191` | 3.1517 |
| `Accent` | `#C8A15A` | **검** | 0.4980 | `#64512D` | 3.1564 |
| `ChromeButtonSurface` | `#898B8E` | **검** ← 개정 ② | 0.5566 | **`#3D3E3F`** | 3.1376 |
| `ChromeButtonSurfaceHover` | `#9C9EA0` | 검 | 0.5137 | `#4C4D4E` | 3.1514 |
| `ChromeButtonSurfacePressed` | `#AFB0B2` | 검 | 0.4854 | `#5A5B5C` | 3.1357 |
| `ControlFace on Card` | `#838589` | **검** ← 개정 ② | 0.5742 | **`#38393A`** | 3.1311 |
| `ControlFace on Panel` | `#848588` | **검** ← 개정 ② | 0.5742 | `#38393A` | 3.1368 |
| `ButtonSurfaceOnCard` | `#32353C` | 흰 | 0.3779 | `#7F8286` | 3.1809 |
| `TextPrimary` 흰 채움 | `#F2F4F7` | **검** | 0.4365 | `#888A8B` | 3.1476 |

**전 17종 양자화 후 최소 = 3.1311 (하한 +4.37 %, 실측 최악 손실의 6.2배).**
전 색공간(회색 256 + 유채 4096)에서도 미달 0.

★ **테스트는 반드시 `q8()`(8비트 반올림) 후에 재라.** 부동소수로 재면 이 결함을 못 본다 —
그게 이번 회차가 잡은 것 그 자체다. 이 한 줄이 §9-2에 들어가야 한다.

> **이 규칙이 리터럴을 「예외 처리」하지 않고 「삭제」한다.**
> R14 판(`349048f2`)의 `CharacterInfoWindow.cs:1167`에 있던 `whiteInk ? new Color(1f,1f,1f,0.18f) : UiChrome.CardBorder`는
> **손으로 굴린 `EdgeOnSurface`**다 — 누군가 바탕이 뒤집힌다는 걸 눈치채고 분기를 하드코딩한 것이다.
> 규칙이 생기면 그 삼항식 자체가 `UiChrome.EdgeOnSurface(무대색)` 한 호출로 접힌다.
> 이것이 "예외를 만들지 마라"에 대한 내 답이다 — 예외를 규칙 안에 넣는 게 아니라 **예외를 없앤다**.
>
> ★ **2026-09-26: 예고대로 됐다.** 그 리터럴은 `CharacterInfoWindow*.cs`에서 **0건**이고,
> 지금은 `UiChrome.cs`의 `EdgeOnSurface` 문서 주석이 **역사로만** 인용한다.
> 링 갱신부는 `CharacterInfoWindow.cs` 앵커 `_inkRings[i].color = active ? SelectedRingOn(fill)`이다.

**액자 테두리 개선치**:

| | 현재 | 제안 | 무대 대비 | 창 바탕 대비 |
|---|---|---|---|---|
| 흰 잉크(목탄 `#25282E`) | `#4C4F54` | **`#727478`** | 1.79 → **3.15** | 2.18 → **3.83** |
| 검 잉크(종이 `#E9EAE6`) | `#EBECE9` | **`#828381`** | 1.02 → **3.15** | 15.17 → 4.71 |

★ 검 잉크 쪽은 **창 바탕 대비가 내려간다**(15.17 → 4.96). 괜찮다 — 그 자리에서
무대와 창 바탕을 가르는 것은 **무대 자신**(14.88 : 1)이지 테두리가 아니다.
목탄 무대에서는 무대 자신이 1.22 : 1이라 **테두리가 유일한 분리막**이고, 거기서 1.79 → 3.15가 오른다.

**잉크색 견본 링 개선치**(`CharacterInfoWindow.cs` 갱신부 앵커 `_inkRings[i].color = active ? SelectedRingOn(fill)` ·
생성부 앵커 `UiChrome.EdgeOnSurface(fill.color), 1.5f`):

| | 검정 견본 | 흰 견본 |
|---|---|---|
| 비선택 (현재 `PanelBorder` raw) | 1.44 ✘ | 1.00 ✘ |
| 비선택 (제안 `EdgeOnSurface`) | `#5C5C5C` **3.14** ✔ | `#919191` **3.15** ✔ |
| 선택 (현재 `TextPrimary`) | 19.06 ✔ | 1.10 ✘ |
| 선택 (제안: 어두운 견본이면 `TextPrimary`, 밝으면 `InkContrastCharcoal`) | `#F2F4F7` 19.06 ✔ | `#25282E` **14.77** ✔ |
| ΔE(선택, 비선택) | 58.27 | 45.73 |

새 색 0개 — `InkContrastCharcoal`은 이미 "밝은 잉크 위의 대비 잉크"로 존재하는 토큰이다
(`UiChrome.cs` 앵커 `InkContrastCharcoal = new Color(0.145f`).

### 4-4 경계 선언 — 이 정책이 **덮지 않는** 것 (예외가 아니라 다른 그리기 경로)

`alphascan.py`가 프로덕션 전체에서 찾은 리터럴 α 중 ~~4건~~ **3건**(2026-09-26 정정 — §12 ㉮)은 **uGUI `Image`가 아니다**:

| 파일:줄 | α | 무엇 |
|---|---|---|
| `WindowTheftRenderer.cs` 앵커 `GhostFrameColor = new Color(0.24f, 0.52f, 0.92f, 0.95f)` | 0.95 | 고스트 창 프레임 (`LineRenderer`) |
| 같은 파일 앵커 `GhostTitleColor = new Color(0.24f, 0.52f, 0.92f, 0.60f)` | 0.60 | 고스트 창 제목 (`LineRenderer`) |
| 같은 파일 앵커 `DustColor = new Color(0.62f, 0.60f, 0.56f, 0.85f)` | 0.85 | 발밑 먼지 퍼프 (`LineRenderer`) |
| ~~`FocusWatchRenderer.cs:97`~~ | ~~0.55~~ | ~~집중 링 트랙 (`LineRenderer`)~~ **2026-09-26 정정 — 이 파일은 삭제됐다**(발밑 타이머 링 제거, 커밋 `912fd8c`). 그래서 이 표는 **4행이 아니라 3행**이다. 같은 사실을 `Tests/EditMode/UiAlphaBleedPolicyAuditTests`가 제외 목록(`NotUguiRenderers`)에서 이 이름을 빼며 「죽은 니들」이라고 적어 두었다. §12 ㉮ |

이것들은 캐릭터의 `Sprites-Default` 머티리얼을 빌려 쓰므로 블렌드 식이 다르다
(`Blend One OneMinusSrcAlpha` → `dstA' = srcA + dstA(1−srcA)`, `CharacterPortraitStage.cs` 앵커 `감기 방향이 무관하고` 참조).
**같은 규칙을 적용하면 계산이 거짓말을 한다.** 그리고 「고스트 창」은 반투명이 **연출 그 자체**다.

→ **이 정책은 uGUI 오버레이 캔버스의 `Image`만 덮는다고 명시한다.** 월드 레이어 ~~4건~~ 3건은
**별건으로 측정이 필요하다**(§8 실기 캡처 목록 W-1). 조용히 빼지 않고 여기 적어 둔다.

---

## §5 과제1 — `coder-ui` 인계표 (파일:줄)

★ **이 절의 「파일:줄」은 전부 R14 판(`349048f2`) 기준이고, 이 인계는 이미 착지했다**(문서 머리 ★ 블록).
줄 번호를 지금 파일에 그대로 대지 마라 — 파일이 쪼개지고 길어졌다(예: 표의 `Cards.cs` · `Inventory.cs` ·
`Tabs.cs`는 지금 `CharacterInfoWindow.Cards.cs` · `CharacterInfoWindow.Inventory.cs` ·
`CharacterInfoWindow.Tabs.cs`이고, R14에 없던 `CharacterInfoWindow.Dlc.cs` · `CharacterInfoWindow.Shop.cs`가
같은 처방을 이미 쓰고 있다). 지금 상태는 **「`UiChrome.Flatten(` 안에 있는가」를 고정 문자열로** 세서 확인한다
(§9-1 감사가 그 규칙을 소스에서 잠근다).

**공통 규약**: hex를 적지 말고 `UiChrome.Flatten(토큰, 바탕)` / `UiChrome.EdgeOnSurface(바탕)`을
호출부에 쓴다. 아래 hex는 리뷰어가 눈으로 검산할 값이다.

### 5-1 정보창 `CharacterInfoWindow*` (창 바탕 `PanelSurface`)

| 파일:줄 | 지금 | 실제 바탕 | 바꿀 것 | 결과 |
|---|---|---|---|---|
| `CharacterInfoWindow.cs:1122` | `active ? TextPrimary : PanelBorder` | **견본 채움(런타임 가변)** | §4-3 링 규칙 | `#F2F4F7`/`#25282E` · `#5C5C5C`/`#919191` |
| `CharacterInfoWindow.cs:1167` | `whiteInk ? new Color(1,1,1,0.18) : CardBorder` | 무대(목탄/종이) | `EdgeOnSurface(무대)` | `#727478` / `#828381` |
| `CharacterInfoWindow.cs:1545` | `CardBorder` (액자 테두리 **생성**) | 무대 | 같은 규칙 | (1167이 덮어씀 — 생성값도 맞춰라) |
| `CharacterInfoWindow.cs:1613` | `CardBorder` | `CardSurface`(:1608) | `Flatten` | `#32353C` |
| `CharacterInfoWindow.cs:1776` | `PanelBorder` (링 **생성**) | 견본 채움 | §4-3 링 규칙 | (1122가 덮어씀 — 생성값도 맞춰라) |
| `CharacterInfoWindow.cs:1888` | `PanelBorder` | `CardSurface`(:1887) | `Flatten` | `#404349` |
| `CharacterInfoWindow.Stats.cs:324` | `CardBorder` | `CardSurfaceMuted`(:318) | `Flatten` | `#2C2F35` |
| `CharacterInfoWindow.Stats.cs:417` | `CardBorder` | `CardSurfaceMuted`(:414) | `Flatten` | `#2C2F35` |
| `CharacterInfoWindow.Tabs.cs:259` | `CardBorder` | `CardSurfaceMuted`(:256) | `Flatten` | `#2C2F35` |
| `CharacterInfoWindow.Cards.cs:309` | `CardBorder` | `CardSurface` | `Flatten` | `#32353C` |
| **`Cards.cs:319`** | **`RarityBorder()`** | `CardSurface` | `Flatten` | `#62615E`/`#746D5E`/`#867858`/`#988251` |
| **`Cards.cs:430`** | `CardBorderHover` | `card.Surface.color` | `Flatten` | `#A8AAAD` / `#A6A7AA` |
| **`Cards.cs:432`** | **`RarityBorder()`** | `card.Surface.color` | `Flatten` | 위 표(바탕별) |
| **`Cards.cs:633`** | **`RarityBorder()`** | `_detailThumb.color` | `Flatten` | 위 표(바탕별) |
| `Cards.cs:979` | `CardBorder` | `CardSurface`(:975) | `Flatten` | `#32353C` |
| `Cards.cs:1250` | `CardBorder` | `CardSurfaceMuted`(:1245) | `Flatten` | `#2C2F35` |
| `Cards.cs:1264` | `CardBorder` | `ThumbSurfaceLocked`(:1257) | `Flatten` | `#282B2F` |
| **`Inventory.cs:220`** | **`RarityBorder()`** | `view.Surface.color`(:211) | `Flatten` | 위 표(바탕별) |
| `Inventory.cs:222` | `CardBorder` | `view.Surface.color` | `Flatten` | `#32353C` / `#2C2F35` |
| `Inventory.cs:226` | `TrackBackground` | 행 바탕 | `Flatten` | `#30333A` / `#2A2D32` |
| `Inventory.cs:364` | `CardBorder` | `CardSurface`(:361) | `Flatten` | `#32353C` |
| `Inventory.cs:435` | `CardBorder` | `SubtleSurface`(:431) | `Flatten` | `#30343A` |

> ★ **`Cards.cs:509`는 고치지 마라 — 이미 옳다.** 여러 줄에 걸친 `UiChrome.Flatten(`의 **인자**이고
> (`:508`에서 열려 `:510`에서 닫힌다), 그 주석이 이 라운드가 하려는 말을 이미 하고 있다.
> 인계 보고서의 60줄 목록에 이 줄이 들어 있는 것은 오탐이다.
> 그리고 **바로 3줄 위(`:432`)가 같은 결함으로 남아 있다** — 옳은 처방이 3줄 위 형제에게
> 옮겨붙지 않은 전형적 사례다.

### 5-2 팝오버 3종 (`PopoverPanel:624` = `AddOpaquePanel`, `PanelSurface`)

| 파일:줄 | 지금 | 바탕 | 결과 |
|---|---|---|---|
| `ActionCommandPopover.cs:236` | `AcceptFlashPeak = AccentSurface` | 타일 아래 = 그룹 카드 `CardSurface` | `#33312D` (α1) |
| `ActionCommandPopover.cs:238` | `AcceptFlashEnd`(α0) | — | `CardSurface`(α1)로. **`IdleTileSurface`(α0)는 그대로 둔다** — α0은 `dstA`를 안 건드린다 |
| `ActionCommandPopover.cs:384` | `CardBorder` | `CardSurface`(:382) | `#32353C` |
| `ActionCommandPopover.cs:402` | `Divider` | `CardSurface` | `#2B2F35` |
| `FocusSessionPopover.cs:333` | `Divider` | `PanelSurface` | `#24272C` |
| `FocusSessionPopover.cs:360` | `CardBorder` | `CardSurface`(:357) | `#32353C` |
| `FocusSessionPopover.cs:401` | `CardBorder` | `CardSurface`(:398) | `#32353C` |
| `FocusSessionPopover.cs:438` | `AccentBorder` | **`:442`가 정한 면** | `Flatten(AccentBorder, #2D2A25)` = `#826C42` |
| `FocusSessionPopover.cs:442` | `AccentSurface` | `PanelSurface` | `#2D2A25` |
| `FocusSessionPopover.cs:469` | `CardBorder` | `CardSurface`(:467) | `#32353C` |
| `FocusSessionPopover.cs:490` | `TrackBackground` (`AddCircle` 링) | `PanelSurface` | `#292C30` |
| `FocusSessionPopover.cs:529` | `CardBorder` | `CardSurface`(:527) | `#32353C` |
| `FocusSessionPopover.cs:738` | `AccentSurface : CardSurface` | `PanelSurface` | `#2D2A25` : `CardSurface` |
| `FocusSessionPopover.cs:739` | `AccentBorder : CardBorder` | **:738이 정한 면** | `#826C42` : `#32353C` |
| `FocusSessionPopover.cs:750` / `:751` | 동일 | 동일 | 동일 |
| `FocusSessionPopover.cs:756` | `TrackBackground` | `PanelSurface` | `#292C30` |
| `TodoBoardPopover.cs:172` | `CardBorder` | `CardSurface`(:161) | `#32353C` |
| `TodoBoardPopover.cs:189` | `CardBorder` | `CardSurface`(:186) | `#32353C` |
| `TodoBoardPopover.cs:193` | `CardBorder` | `SubtleSurface`(:191) | `#30343A` |
| `TodoBoardPopover.cs:229` | `CardBorder` | `CardSurface`(:227) | `#32353C` |
| `TodoBoardPopover.cs:249` | `CardBorder` | `CardSurface`(:247) | `#32353C` |
| `TodoBoardPopover.cs:261` | `CardBorder` | `CardSurface`(:260) | `#32353C` |
| `TodoBoardPopover.cs:393` | `AccentSurface : CardSurface` | `PanelSurface` | `#2D2A25` : `CardSurface` |
| `TodoPostItWidget.cs:1148` | `CardBorder` | 버튼 면 | `Flatten(CardBorder, 그 면)` |

### 5-3 부채꼴 `GearRadialMenuWidget` — ★ **바탕화면 직접, Class B**

여기는 비침 식이 `1 − α²`다. **`AccentBorder` α0.55는 이 자리에서 69.75 % 비친다**
(창 안 24.75 %가 아니다).

| 파일:줄 | 지금 | 바꿀 것 | 결과 |
|---|---|---|---|
| `GearRadialMenuWidget.cs:1552` | `Fade(AccentBorder, α)` | `Fade(Flatten(AccentBorder, PanelSurface), α)` | `#77633E` (3.11 vs 알약 바탕) |
| `:1661` | `Fade(PanelBorder, α)` | `Fade(Flatten(PanelBorder, PanelSurface), α)` | `#3A3C40` |
| `:1686` | `armedQuit ? AccentSurface` | `Flatten(AccentSurface, CardSurface)` | `#33312D` — **§5-4 참조** |
| `:1688` | `armedQuit ? AccentBorder : CardBorder` | 두 끝점 모두 Flatten(그 버튼 면 위로) | **§5-4 참조** |
| `:1694` | `Lerp(CardSurface, AccentSurface, t)` | 두 끝점을 미리 Flatten한 뒤 Lerp | `#1B1F26` → `#33312D` |
| `:1695` | `Lerp(CardBorder, AccentBorder, t)` | 동일 | `#32353C` → **§5-4** |
| `:1739` | `Fade(TrackBackground, α)` | `Fade(Flatten(TrackBackground, 버튼 면), α)` | — |
| `:2204` | `AddOutline(…, PanelBorder, 9)` | `Flatten(PanelBorder, PanelSurface)` | `#3A3C40` **[실기 캡처 C-2]** |
| `:2238` | `AddOutline(…, AccentBorder, 9)` | `Flatten(AccentBorder, PanelSurface)` | `#77633E` **[실기 캡처 C-3]** |
| `:2250` | `Fade(AccentBorder, 0f)` | 위와 같은 끝점으로(α0이라 무해하나 두 벌을 만들지 않는다) | — |
| `:2277` | `AddCircle(…, CardBorder, 1.2f)` | `Flatten(CardBorder, CardSurface)` | `#32353C` **[실기 캡처 C-1]** |

> `Lerp` 주의: `Lerp(Flatten(A,B), Flatten(C,B), t)`는 **끝점이 정확히 같고 중간만
> 다르다**(raw는 α까지 함께 보간한다). 호버 보간은 0.09초(`HoverSeconds`)라 중간값을
> 사람이 판정할 수 없다. **끝점이 맞으면 충분하다.**

### 5-4 ★ Flatten이 드러내는 기존 결함 2건 (숫자 판정, 리더 배분 사항)

Flatten은 색을 안 바꾸지만, **α로 가려져 있던 대비 미달을 드러낸다.**

**(가) 무장한 [앱 종료] 버튼의 면이 약하다.**
`Flatten(AccentSurface, CardSurface)` = `#33312D`인데, 평상 면 `SubtleSurface` `#191D24` 대비
**1.31 : 1** · ΔE 12.81이다. ~~지금은~~ **R14 판(`349048f2`)까지는** `α0.14`가 바탕화면을 98 % 통과시켜 **구멍**으로 보이던 자리다
(지금은 합성돼 있다 — `GearRadialMenuWidget.cs` 앵커 `Color quitArmedFace = UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurface)`).
불투명하게 만들면 「장전됨」이 **면만으로는 안 읽힌다.**

**(나) 무장 테두리가 자기 면 위에서 3.0을 못 넘는다.**
`Flatten(AccentBorder, #33312D)` = `#856F46`, 면 대비 **2.68** ✘.

**처방(design-art 권고, 새 색 0개 · α 0개)**: 무장 링을 `AccentBorder`가 아니라
**`UiChrome.Accent`(α=1, `#C8A15A`)** 로 올린다.

| | 무장 면 `#33312D` 대비 | 평상 링 `#30343A`과의 ΔE |
|---|---|---|
| `Flatten(AccentBorder, 면)` `#856F46` | 2.68 ✘ | — |
| **`Accent` `#C8A15A`** | **5.35** ✔ | **66.28** |

근거는 그 함수의 자기 주석이다 — *"강조색은 무장 전용이다"*(`GearRadialMenuWidget.cs` 앵커 `강조색은 <b>무장 전용</b>이다`).
강조색을 α로 반쯤 지워 놓고 "무장 전용"이라고 적어 둔 상태였다. 같은 이유로 호버 보간
`:1695`의 끝점도 `Accent`가 맞다(호버 끝 면 `#33312D`에서 2.68 → 5.35).

**P2 · 실기 캡처 항목 C-4** — 이건 알파 결함 수정이 아니라 **연출 강도 판정**이므로
리더 승인 후 별도 배정을 권한다.

★ **2026-09-26 확인: 이 처방(가·나)은 여전히 미채택이다.** 코드가 그 사실을 스스로 적어 두었다 —
`GearRadialMenuWidget.cs`의 위 앵커 바로 위 주석이 「무장 링을 Accent로 올리는 §5-4는 연출 강도
판정이라 P2로 남는다」이고, 실제 배선은 테두리를 `Flatten(AccentBorder, surface)`로, 기호를
`WarmAccent`로 둔다. **즉 α 결함만 닫히고 강도 판정은 열려 있다**(§12 ㉰).

---

## §6 과제1 — 「전부 반영 후」 이 앱에 남는 α<1

| 남는 것 | α | 왜 남기나 |
|---|---|---|
| `AddHitArea` / `NameHit` / 탭 면 (α=0) | 0 | `dstA' = 0 + dstA(1−0) = dstA`. **프레임버퍼 알파를 건드리지 않는다.** |
| `TodoPostItWidget.cs` 앵커 `new Color(0f, 0f, 0f, 0.001f)`(2회) | 0.001 | 불투명 위 비침 **0.0001 %**. 히트테스트용. |
| `ActionCommandPopover` `IdleTileSurface` (α=0) | 0 | 위와 같음 |
| 부채꼴 펼침/수축 `Fade(…, α)` | 0→1 | **의도된 페이드.** 쉬는 상태 `alpha = 1f`(`GearRadialMenuWidget.cs` 앵커 `b.Progress = 1f;`)라 정지 시 비침 0 % |
| 이름표/알약 페이드 인·아웃 | 0→1 | 동일 |
| `UiChrome`의 9개 토큰 **정의 자체** | — | 정의는 남는다. **바뀌는 것은 「생으로 `Image.color`에 넣지 않는다」는 규칙**이다 |
| 월드 `LineRenderer` ~~4건~~ 3건 | 0.55\~0.95 | §4-4 — **다른 블렌드 경로.** 별건 측정 |

---

## §7 과제2 — 설정창 컨트롤 면

### 7-1 ★ 먼저: 목표 3.60은 **도달 불가능하다**

"라벨이 면 위에 얹히는 버튼"에서 두 하한(면 ≥ 3.0, 잉크 ≥ 4.5)을 **동시에** 만족하는 면을
흰색 α 전 구간에서 훑었다(`settingsface.py` §E). 결과는 **연속 구간이 아니라 두 섬**이다:

**카드 바탕 `#1B1F26` 위**

| 흰 α | 면 | 면 대비 | 최선 잉크 | 잉크 대비 | 판정 |
|---|---|---|---|---|---|
| 0.3125 | `#62656A` | 2.83 | `TextPrimary` | 5.30 | ✘ 면 미달 |
| **0.3438** | `#696C71` | **3.14** | `TextPrimary` | **4.78** | ✔ **섬 1** |
| 0.3594 | `#6D7074` | 3.30 | `TextPrimary` | 4.54 | ✔ 섬 1 끝 |
| 0.3750 | `#717377` | 3.47 | `TextPrimary` | 4.31 | ✘ **골짜기** |
| 0.3906 | `#74777B` | **3.65** | `OnAccentSolid` | 4.22 | ✘ **골짜기** |
| 0.4219 | `#7B7E82` | 3.93 | `OnAccentSolid` | 4.66 | ✔ 섬 2 시작 |
| **0.4570** | **`#838589`** | **4.49** | `OnAccentSolid` | **5.19** | ✔ **섬 2 · 채택** |
| 0.5000 | `#8D8F92` | 5.10 | `OnAccentSolid` | 5.89 | ✔ 섬 2 |

**골짜기 = 면 대비 3.30 ~ 3.93.** 이 저장소가 선언한 `ControlFaceContrastTarget = 3.60`
(`UiChrome.cs` 앵커 `public const float ControlFaceContrastTarget`)이 **정확히 그 안**이다. 창 바탕 위에서도 같다(골짜기 3.61 ~ 4.32,
선언값 3.60이 경계에 걸린다).

> **왜 이 골짜기가 생기나**: 면을 밝히면 면 대비는 오르고 그 위의 **밝은 잉크는 죽는다**.
> 어두운 잉크(`OnAccentSolid`)로 뒤집히기 전까지 두 지표가 정면으로 싸운다.
> `UiChrome.cs` 앵커 `두 지표는 <b>반대 방향</b>이다`가 "두 지표는 반대 방향이다"라고 이미 적어 놓았고,
> `ControlFaceOnSurface`가 이분 탐색이 아니라 격자 탐색인 이유도 이것이다
> (같은 파일 앵커 `이분 탐색이 아니라 <b>격자 탐색</b>인 이유`).
> **선언된 3.60이 그 골짜기 안이라는 사실은 아무 데도 안 적혀 있었다.**

★ **그러므로 "설정창 컨트롤 면을 3.60으로 맞춰라"는 지시는 실행 불가능하다.**
답은 3.60이 아니라 **섬 1(3.14~3.30) 또는 섬 2(3.93 이상)** 중 하나를 고르는 것이다.

### 7-2 섬 1 vs 섬 2 — 왜 섬 2인가

| | 섬 1 (`#696C71`, 흰 잉크) | **섬 2 (`#838589`, 어두운 잉크)** |
|---|---|---|
| 면 대비 | 3.14 (하한 +4.7 %) | **4.49 (하한 +49.7 %)** |
| 잉크 대비 | 4.78 (하한 +6.2 %) | **5.19 (하한 +15.3 %)** |
| 양자화·감마 여유 | 거의 없음 | `ControlInkContrastTarget`이 요구하는 마진 그대로 |
| 다크 창의 인상 | 유지 | 밝은 칩이 늘어난다 |
| **이 앱의 선례** | **0건** | **2건 — 이미 출하 중** |

결정 근거는 마지막 줄이다. 이 앱은 같은 문제를 **두 번 풀었고 두 번 다 섬 2로 갔다**:

- `ChromeButtonSurface` `#898B8E` (창 바탕 5.26) — 창 [✕]/[설정] 칩, 2026-09-02
- `CardActionSurface` `#838589` (카드 4.49) — 정보창 카드 [착용]/[해제] 칩, 2026-09-02

**설정창만 섬 1로 가면 이 앱에 「누를 수 있는 것」의 시각 언어가 두 벌이 된다.**
내 자리에서 그건 결함이다 — 여섯 팩이 한 게임으로 안 보이는 것과 같은 종류의 실패다.

그리고 **채택값은 이미 그 함수가 만든다.** 새 상수를 만들지 않는다:

```
ControlFaceOnSurface(CardSurface)   = #838589   면 4.49  잉크 #0B1016  5.19
ControlFaceOnSurface(PanelSurface)  = #848588   면 4.88  잉크 #0B1016  5.18
```

### 7-3 ★ 「전부 3.6으로」가 아니다 — 부품 네 종류, 처방 네 개

★ **F1 · F2 · F4는 착지했다**(2026-09-26 확인). 아래 표의 「지금」 칸은 R14 판(`349048f2`) 기록이고,
현재 배선은 `SettingsControls.cs` 앵커 `public static readonly Color ControlFaceOnCard`와
`ControlFaceOnPanel` 선언, 그리고 같은 파일의 `UiChrome.EdgeOnSurface(` 호출부다.
F3(트랙/홈)은 설계대로 **손대지 않았다.**

`coder-ui`가 넘긴 판정("제 행만 밝히면 위계가 뒤집힌다")의 진짜 답은
**전부 같은 값으로 올리는 것이 아니라, 무엇이 어포던스를 지고 있는지로 나누는 것**이다.

#### F1 — 면이 곧 어포던스 (누를 수 있는 슬래브)

대상: 슬라이더 스텝 `[+]`/`[−]`, `AddButtons`의 행 버튼, 푸터 `[지금 종료]`,
설정창 오른쪽 레일의 페이지 버튼 `[▲]`/`[▼]`.

| 파일:줄 | 지금 | 바꿀 것 |
|---|---|---|
| `SettingsControls.cs:1092` (`AddStepButton` 면) | `ButtonSurfaceOnCard` `#32353C` **1.35** | `ControlFaceOnSurface(CardSurface)` `#838589` **4.49** |
| `SettingsControls.cs:1096` (스텝 글리프) | `InkTitle(enabled)` → `#838589` 위 **3.34** ✘ | `InkOnSurface(면, Title, enabled)` → `#0B1016` **5.19** |
| `SettingsControls.cs:1183` (`AddButtons` 면) | `ButtonSurfaceOnCard` **1.35** | `#838589` **4.49** |
| `SettingsControls.cs:1187` (버튼 라벨) | `InkTitle(enabled)` **3.34** ✘ | `InkOnSurface(면, Title, enabled)` **5.19** |
| `SettingsWindow.cs:1666` (`[지금 종료]` 면) | `ButtonSurfaceOnPanel` `#2B2E33` **1.32** | `ControlFaceOnSurface(PanelSurface)` `#848588` **4.88** |
| `SettingsWindow.cs:1802` (`AddPageButton` 면) | `CardSurfaceMuted` `#15181E` **1.01** ← 창 바탕보다 **어둡다** | `ControlFaceOnSurface(PanelSurface)` `#848588` **4.88** |
| `SettingsWindow.cs:1804-1805` (그 테두리) | `Flatten(CardBorder, CardSurfaceMuted)` — **α는 이미 옳다**, 대비만 1.33 | 면이 4.88이 되면 테두리는 장식이 된다 → 삭제 권고(P2) |

★ **`SettingsRowGate.Apply()`(`SettingsControls.cs` 앵커 `ink.Label.color = UiChrome.InkOnSurface(ink.Face, UiChrome.InkRole.Title, Enabled)`)도 함께 고쳐야 한다.**
그 함수는 등록된 `TitleInk` 전부에 `UiChrome.InkTitle(Enabled)`를 **일괄 대입**하는데,
그 배열에 F1 버튼 라벨이 들어 있다(`AddButtons`의 `titleInk` 조립, 앵커 `titleInk[i + 1] = new SettingsRowGate.GatedInk(`).
게이트가 내려가는 순간 라벨이 `TextSecondary`가 되고 `#838589` 위에서 **1.77 : 1**로 지워진다.
→ 게이트가 **면을 알아야** 한다(또는 `Text`가 아니라 `(Text, 면)` 쌍으로 등록한다).
**이걸 안 고치면 이번 수정이 새 「1.28 : 1 사고」를 만든다** — 그 사고의 재발 형태 그대로다.

★ **2026-09-26 확인: 고쳐졌다.** 게이트는 이제 `Text`가 아니라 `(Text, 면)` 쌍(`GatedInk`)으로
등록받고 잉크를 **그 면에서 다시 뽑는다** — 위 두 앵커가 그 증거다. 이 절이 제시한 두 번째
선택지가 채택된 것이고, 예고된 새 결함은 발생하지 않았다(§12 ㉱).

★ **F1의 테두리는 삭제를 권한다(P2, 필수 아님).** `#838589` 면 위의 `OutlineOnCard` `#404349`는
면 대비 2.70의 **어두운 링**이 되어 베벨처럼 읽힌다. 선례인 `[✕]`·`[착용]` 칩은 테두리가 없다.
알파 결함은 아니므로(이미 Flatten된 값이다) 이번 라운드 필수 항목에서 뺀다.

#### F2 — 입력칸: 면을 올리지 **않는다**

대상: `SettingsControls.cs:1282` 이름 입력 필드.

실측이 반대로 말한다:

| | 현재 면 `#32353C` | 제안 F1 면 `#838589` |
|---|---|---|
| 입력 글자 `TextPrimary` | **11.10** ✔ | 3.34 ✘ |
| 플레이스홀더 `InkMeta` | 3.94 | **1.18** ✘✘ |

**입력칸에서 부족한 것은 글자가 아니라 「상자가 있다는 사실」이다.** 그건 테두리의 일이다.
그리고 `UiChrome.cs`가 면을 고른 이유 셋 중 하나가 "테두리만 있는 것은 버튼이 아니라
**입력칸**으로 읽힌다"였다(앵커 `테두리만 있는 것은 버튼이 아니라 <b>입력칸</b>으로 읽힌다`)
— 여기서는 그게 **결함이 아니라 정답**이다.

| 파일:줄 | 지금 | 바꿀 것 |
|---|---|---|
| `SettingsControls.cs:1282` (면) | `ButtonSurfaceOnCard` | **유지** |
| `SettingsControls.cs:1287-1288` (테두리) | `OutlineOnCard` `#404349` **1.66** · 두께 1 | `EdgeOnSurface(CardSurface)` `#6A6D71` **3.16** · **두께 2** |

두께를 2로 올리는 이유: 배율 1.0에서 1 pt = **물리 1픽셀**이고, 이 저장소는 그걸 한 번 잃어 본 적이
있다(커밋 `39ab690`, `UiChrome.cs` 앵커 `커밋 39ab690`이 인용). 견본 테두리는 이미 두께 2다
(`SettingsControls.cs` 앵커 `UiChrome.EdgeOnSurface(colors[i])`) — 새 규칙이 아니라 **이미 있는 값에 맞추는 것**이다.

#### F3 — 트랙/홈: **손대지 않는다**

대상: 스위치 트랙(`SettingsControls.cs` 앵커 `AddSurface(row, "Track", SettingsControls.TrackOnCard`),
슬라이더 트랙(같은 파일 앵커 `AddSurface(hit.rectTransform, "Track", SettingsControls.TrackOnCard, 3)`),
행 구분선(같은 파일 앵커 `AddSurface(_card, "Divider" + _rowIndex, SettingsControls.DividerOnCard, 2)`).

| 근거 | 숫자 |
|---|---|
| 어포던스는 **손잡이**가 이미 진다 | `TextPrimary` `#F2F4F7` = 카드 대비 **14.99** |
| 값은 **채움**이 이미 진다 | `Accent` `#C8A15A` = **6.83** |
| 트랙을 4.49로 올리면 켜짐/꺼짐의 밝기 비가 무너진다 | 지금 **5.23배** → **1.52배** |

같은 논리를 이 저장소가 이미 등급 리본 트랙에서 확정했다 —
*"트랙은 「보이는 구획선」이 아니라 **「홈」으로** 설계된 것"*(`UiChrome.cs` 앵커 `트랙은 「보이는 구획선」이 아니라`).
그리고 WCAG 2.2 §1.4.11은 컴포넌트를 **식별**하는 데 필요한 시각 정보를 요구하지,
컴포넌트의 모든 부분에 3 : 1을 요구하지 않는다.

#### F4 — 색이 곧 내용 / 「고르지 않음」: **테두리가 진다**

대상: 색 견본 2행(`SettingsControls.cs:1238-1242`), 안 고른 세그먼트 칩(`:1127-1129`).

| 파일:줄 | 지금 | 바꿀 것 | 근거 |
|---|---|---|---|
| `SettingsControls.cs:1241-1242` (견본 테두리) | `OutlineOnCard` **1.66** (카드 기준) | **`EdgeOnSurface(견본 채움)`** — §4-3 | 이 테두리는 **견본 위**에 있다. 검정 견본에서 1.44, 흰 견본에서 **1.00** |
| `SettingsControls.cs:733` (`SettingsSwatchRow.Apply` 비선택) | `OutlineOnCard` | `EdgeOnSurface(BaseColors[i])` | 같음 |
| `SettingsControls.cs:732` (선택) | `TextPrimary` | 밝은 견본이면 `InkContrastCharcoal`로 뒤집기 | 흰 견본에서 **1.10** ✘ |
| `SettingsControls.cs:1128-1129` (세그먼트 테두리) | `OutlineOnCard` **1.66** | `EdgeOnSurface(CardSurface)` `#6A6D71` **3.16** · 두께 2 | 안 고른 칩은 면이 `CardSurface` = **1.00**. 테두리가 유일한 분리막 |
| `SettingsControls.cs:606` (`Apply` 비선택 테두리) | `OutlineOnCard` | `EdgeOnSurface(CardSurface)` `#6A6D71` | 같음 |

★ **안 고른 세그먼트의 「면」은 올리지 않는다.** 고른 칩(`Accent`, 6.83)과 안 고른 칩의 차이가
**면의 유무**로 읽혀야 하기 때문이다. 테두리만 3.16으로 올리면 「누를 수 있는 칸이 여기 있다」는
읽히고 「지금 골라진 건 저것」도 그대로 읽힌다 — ΔE(고른 면, 안 고른 테두리) = **51.26**.

### 7-4 적용 후 서열 검산 — 「가장 튀는 요소가 되는가」에 대한 답

카드 바탕 `#1B1F26` 기준 (`settingsface.py` §F):

| 순위 | 요소 | 색 | 카드 대비 |
|---|---|---|---|
| 1 | 행 라벨 · 스위치 손잡이 (`TextPrimary`) | `#F2F4F7` | 14.99 |
| 2 | 본문 · 값 (`TextSecondary`) | `#AEB4BF` | 7.93 |
| 3 | **고른 세그먼트 면** (`Accent`) | `#C8A15A` | 6.83 |
| 4 | 캡션 (`TextTertiary`) | `#8B939F` | 5.32 |
| **5** | **★ 제안 버튼 면 (F1)** | `#838589` | **4.49** |
| 6 | ★ 제안 테두리 (F2/F4) | `#6A6D71` | 3.16 |
| 7 | 스위치 트랙 OFF — 유지 | `#30333A` | 1.31 |
| 8 | 행 구분선 — 유지 | `#2B2F35` | 1.22 |
| 9 | 안 고른 세그먼트 면 — 유지 | `#1B1F26` | 1.00 |

**5위다.** 라벨의 1/3.34, 고른 세그먼트의 1/1.52다. 위계는 뒤집히지 않는다.
창 바탕 쪽도 같다 — 제안 F1 면 4.88 < `[✕]` 크롬 칩 5.26으로 **창 크롬이 여전히 위**다.

### 7-5 기존 위계와의 충돌 점검

| 이미 있는 위계 | 충돌하나 | 숫자 |
|---|---|---|
| 활성/비활성 **토글** | ✘ | 트랙은 F3라 안 건드린다. 꺼진 손잡이 1.71(WCAG 1.4.11 면제, `SettingsControls.cs` 앵커 `비활성 컴포넌트를 명시적으로 면제`가 이미 근거를 적어 놓았다) ★ 2026-09-26: 그 주석의 현재 숫자는 **1.70 : 1**이다. 어느 쪽이 맞는지는 이 라운드에서 재계산하지 않았다(미확인) |
| 활성/비활성 **세그먼트** | ~~**주의**~~ **해소(2026-09-26)** | `Apply()`가 비활성 활성칩에 ~~`ButtonSurfaceOnCard`를 쓴다~~ → F1을 바꾸면 여기도 4.49가 된다. **의도대로다**(활성 = `Accent` 6.83 > 비활성 = 4.49). 잉크는 이미 `InkOnSurface(face, …)`라 **자동으로 따라온다** — 이 부품만 이미 옳게 배선돼 있다. ★ **착지 확인**: 그 면은 지금 `ControlFaceOnCard`다(앵커 `: (active ? SettingsControls.ControlFaceOnCard : UiChrome.CardSurface)`, 대상 파일 1회) · 잉크 앵커는 `Labels[i].color = UiChrome.InkOnSurface(face, UiChrome.InkRole.Body, enabled: true)`이고, 코드가 그 자리에 「정책 §7-5」를 인용해 두었다 |
| **K1 캡션 접두사 "준비 중"** | ✘ | 문자열 채널이다(`SettingsControls.cs` 앵커 `public const string NotBuiltWord`). 면·잉크와 축이 다르다 |
| **`SettingsRowGate`** | **★ 충돌** | §7-3 F1의 ★ 항목. **이번 라운드에서 함께 안 고치면 새 결함이 난다** |
| `Dimmed()` 견본 감쇠 (2.67 : 1 물러남) | ✘ | 견본 **면**의 규칙이고 F4는 **테두리**만 건드린다 |
| 정보창 `CardActionSurface` `#838589` | ✘ | **같은 값이 되는 것이 목적**이다(§7-2) |

---

## §8 실기 캡처 필요 목록

**이 머신에서는 렌더할 수 없는 항목만 적는다.** 캡처 전에는 「확인됨」이라고 쓰지 마라.

| ID | 무엇 | 왜 계산으로 못 닫나 | 어디서 |
|---|---|---|---|
| **C-1** | 부채꼴 버튼 링(`GearRadialMenuWidget.cs` 앵커 `view.Border = UiChrome.AddCircle(view.Root, "Border", d,`) Flatten 후 **원 실루엣의 코너 램프** | 원 스프라이트의 램프 폭이 0.26 pt(≈Retina 0.5 px). 바이리니어 축소 후 실제 커버리지는 렌더러가 정한다 | Windows **밝은 바탕화면**부터 (ΔE 20.08 vs 검 3.61) |
| **C-2** | 부채꼴 이름표 보더(같은 파일 앵커 `_hoverLabelBorder = UiChrome.AddOutline(_hoverLabel, "Border",`) 반지름 9 코너 | 위와 같음(둥근 사각형) | 동일 |
| **C-3** | 온보딩 알약 보더(같은 파일 앵커 `_onboardingHintBorder = UiChrome.AddOutline(_onboardingHint, "Border",`) 반지름 9 코너, k=0.55 | 위와 같음. k가 커서 raw↔flat 차가 가장 작다(ΔE 10.20) | 동일 |
| **C-4** | 무장 [앱 종료]의 「장전됨」이 `#33312D` 면 + `Accent` 링으로 읽히는가 | 연출 강도 판정 — 대비 5.35는 **읽힌다**를 보장하지만 **긴장감**은 보장 못 한다 | macOS/Windows 둘 다 |
| **C-5** | 설정창 F1 버튼 10여 개가 `#838589`가 됐을 때 **창 전체 인상** | 서열은 계산으로 닫았다(§7-4). 남은 것은 「밝은 칩이 많아 산만한가」 — 면적 효과라 색 계산이 못 본다 | 탭 4개 전부 |
| **C-6** | F2/F4 테두리 **두께 2 pt**가 세그먼트 그룹에서 무겁지 않은가 | 2 pt는 Windows ×1.25/×1.75에서 2.5/3.5 px(반 픽셀). 4의 배수만 잔차 0인데(`UiChrome.cs` 앵커 `<b>4의 배수</b>만 세 배율 전부에서 정수 픽셀이 된다`) 4 pt 테두리는 과하다 → **읽히기는 하되 흐린다**를 눈으로 판정 | Windows 125/150/175 % |
| **C-7** | 액자 테두리 검 잉크 쪽 `#828381`가 **종이 무대를 답답하게** 만들지 않는가 | 창 바탕 대비가 15.17 → 4.71로 내려간다. 계산상 옳지만 초상화 인상은 캡처 판정 | macOS 먼저(기본 잉크 = 검정) |
| **W-1** | 월드 `LineRenderer` ~~4건~~ 3건(§4-4)의 실제 블렌드 결과 | 머티리얼이 `Blend One OneMinusSrcAlpha`이고 색이 프리멀티플라이드가 아니다 → 계산 모형이 다르다. **별건 배정 필요** | 고스트 창 · 먼지 ~~· 집중 링~~(2026-09-26: 링 삭제 — §12 ㉮) |

---

## §9 검증 설계 (제안 — `test-engineer` 소관)

1. **소스 감사(EditMode)** — `Assets/_Project/Scripts` 전체를 읽어, 9개 토큰 + `RarityBorder(` +
   `new Color(…, α<1)`이 `Flatten(` / `EdgeOnSurface(` / `Fade(` **밖에서** `Image.color`·
   `AddSurface`·`AddOutline`·`AddCircle`·`AddStroke`에 도달하면 실패.
   - ★ **여러 줄에 걸친 `Flatten(`을 반드시 처리하라.** 내 첫 스캐너가 `Cards.cs:509`를 오탐했다.
     괄호 균형을 **파일 단위**로 세라(줄 단위로 세면 같은 오탐이 난다).
   - ★ **허용 목록에 α=0을 넣어라**(`Color.clear`, `new Color(0,0,0,0)`, `AddHitArea`). 안 넣으면
     히트 영역이 전부 빨개진다.
   - ★ **월드 렌더러 2개는 제외 경로**로 명시하라(§4-4). 조용히 빼면 다음 사람이 규칙을 못 읽는다.
2. **값 검사(EditMode)** — 상수를 베끼지 말고 **`UiChrome.ContrastRatio`로 다시 재라**:
   - `Flatten(RarityBorder(r), bg).a == 1` (4등급 × 3바탕)
   - 인접 등급 ΔE ≥ 7.8 (3바탕) — **하한을 숫자로 적지 말고** `PALETTE_SPEC`의 상수를 참조
   - 4상태 서열 `선택 > 호버 > 착용 > 전설` (3바탕)
   - F1 면: `ContrastRatio(면, 바탕) ≥ MinNonTextContrast` **and**
     `ContrastRatio(InkOnSurface(면,…), 면) ≥ MinTextContrast` — **반드시 한 쌍으로**
     (`UiChrome.cs` 앵커 `그래서 아래 두 목표는 <b>언제나 한 쌍으로</b> 쓴다`가 요구하는 그대로.
     면만 재는 검사는 잉크 붕괴를 못 본다)
3. **★ 8비트 반올림 후에 재라 (R14-b에서 추가 — 이번 회차가 잡은 결함 그 자체다)**
   `EdgeOnSurface` 검사는 반환색과 바탕을 **`Color32`로 왕복시킨 뒤** 대비를 잰다.
   부동소수로 재면 3.0005가 통과하고 화면의 2.9895는 안 보인다.
   - 표본은 선언 바탕 17종으로 **부족하다** — 실측상 회색 램프 256단 중 **104단**,
     유채색 4096 중 **1545**가 그 함정에 있다(`edgemargin.py` K-1/K-2).
     **회색 램프 256단 전수**를 도는 검사 1건을 반드시 포함하라(0.01초짜리다).
   - 방향 규칙(「여유 큰 쪽」)의 절벽 `L* = √0.0525 − 0.05 = 0.1791`을 **양쪽에서 1/255씩**
     찌르는 검사 1건. 절벽이 `target`에 딸려 움직이면 그 순간 빨개져야 한다 —
     그게 개정 ②가 산 이유다.
4. **네거티브 컨트롤** — 옛 raw 값으로 되돌리면 **실제로 빨개지는가**를 같은 테스트 안에서 증명.
   `PortraitBackdropGlowAlphaTests`가 이미 이 형태를 쓰고 있다
   (`Tests/EditMode/PortraitBackdropGlowAlphaTests.cs` 앵커 `네거티브 컨트롤의 반대편` ·
   같은 파일 앵커 `string rawGlow = "= UiChrome."`) — 그 틀을 재사용.
5. **게이트 회귀** — `SettingsRowGate.SetEnabled(false)` 후 F1 버튼 라벨의 대비를 실제로 재라
   (§7-3 ★). 지금 형태로 두면 **1.77 : 1**이 나온다.

---

## §10 플랫폼 영향

- **Windows 영향: 없음(코드 변경 0건 — 이 라운드는 문서·수치 사양이다).**
  다만 **판정 안에 Windows 고유 제약이 두 개 들어가 있다**:
  (1) 배율 125/150/175 %에서 1 pt 테두리가 반 픽셀에 걸린다 → F2/F4 분리막 테두리를 **2 pt**로 올렸다
      (`UiChrome.cs` 앵커 `<b>4의 배수</b>만 세 배율 전부에서 정수 픽셀이 된다`의 리본 두께 판정과 같은 근거).
  (2) **실기 캡처 C-1~C-3은 Windows 밝은 바탕화면에서 먼저 찍어야 한다** — 코너 램프의 raw↔flat
      편차가 바탕화면 밝기에 비례한다(흰 ΔE 20.08 / 중간 9.41 / 검 3.61). macOS 기본 배경보다
      Windows 기본 테마 쪽이 밝은 경우가 흔하므로 **최악 조건이 Windows에 있다**.
  비침 자체는 프레임버퍼 알파에서 나오므로 **양 OS 동일**하다.
- **macOS 영향: 없음(코드 변경 0건).**
  캡처 항목 중 **C-7(액자 테두리 검 잉크)은 macOS에서 먼저** 봐야 한다 — 출하 기본 잉크가 검정이라
  종이 무대가 기본 화면이다.
- **모바일(iPad/iPhone)**: 스크린샷 백드롭 모드에서도 캔버스 알파는 **똑같이** OS 합성 마스크가 되므로
  같은 규칙이 그대로 적용된다. 배율이 ×2/×3이라 코너 램프 위험은 데스크톱보다 **낮다**.

---

## §11 리더 배분 제안 (이 라운드 이후)

★ **2026-09-26 — 아래 P0 4행은 전부 착지했다**(증거는 문서 머리 ★ 블록). 남은 것은
**P1 실기 캡처 C-1~C-7**과 **P2 두 건**(§4-4 W-1 월드 레이어 · §5-4 무장 링 강도 판정)이다.
이 표를 「미착수 목록」으로 읽지 마라.

| 배정 | 내용 | 우선도 |
|---|---|---|
| `coder-ui` | §5 인계표 58줄 + §7-3 F1/F2/F4 | **P0** — 최악 24.75 %가 카드 24×4 + 보관함 행 전부에 살아 있다 |
| `coder-ui` | §7-3 F1 ★ `SettingsRowGate` 잉크 배선 | **P0** — 안 고치면 이번 수정이 새 결함을 만든다 |
| `coder` | `UiChrome.EdgeOnSurface()` 신설(§4-3) | **P0** — F2/F4/액자/견본이 전부 이걸 기다린다 |
| `test-engineer` | §9 감사 4종 | **P0** — 이 규칙은 테스트가 없으면 다음 라운드에 새는 종류다 |
| 리더 → 실기 | §8 C-1~C-7 | **P1** — 코드 변경과 함께 캡처 |
| `coder` / `dev-platform` | §4-4 W-1 월드 레이어 4건 별건 측정 | **P2** |
| `design-art`(나) | C-4·C-5·C-7 캡처 도착 후 최종 도장 | 캡처 후 |

---

## §12 정정 기록 (2026-09-26, `ux-designer`)

**기준**: 대상 파일 `docs/UI_ALPHA_BLEED_POLICY.md`(시작 blob `ac85024a`, HEAD `fa25570`의 커밋본과 동일) ·
코드 대조는 작업 트리 `fa25570` 기준 · 방법은 **고정 문자열 계수**(`grep -rF -c`, 대상 `Assets/_Project/Scripts` 아래 `*.cs`, 테스트 포함) ·
「적중 n」은 저장소 전체 발생 수이고, 대상 파일 안 횟수가 다르면 따로 적었다.
TEAM.md §5(옛 판정은 글자 그대로 + 취소선) · 「기준과 대상이 같이 낡은 스냅숏」 규칙 3(줄 번호를 키로 쓰지 않는다)을 따랐다.

### ㉮ 월드 `LineRenderer`는 4건이 아니라 3건이다 (뜻이 바뀌는 정정 — 취소선)

- 옛 문장: §4-4 표의 4행(FocusWatchRenderer.cs:97 / 0.55 / 집중 링 트랙)과 §4-4 본문 · §6 표 · §8 W-1의 「4건」.
- 사실: 그 파일은 **삭제됐다**(커밋 `912fd8c`, 발밑 타이머 링 제거). 지금 저장소에 `Interaction/FocusWatchRenderer.cs`는 없다.
- 근거 둘: ① 삭제 커밋 ② `Tests/EditMode/UiAlphaBleedPolicyAuditTests`의 `NotUguiRenderers` 주석이 **그 이름을 제외 목록에서 뺀 이유를
  「죽은 니들」이라고 적어 두었다** — 코드가 같은 정정을 먼저 했고 문서만 뒤처져 있었다.
- 고친 곳 4군데: §4-4 본문 · §4-4 표 4행 · §6 표 · §8 W-1(「4건」과 「집중 링」).
- 남는 3건의 α(0.95 / 0.60 / 0.85)는 **바뀌지 않았다** — 정의 3줄을 앵커로 다시 쟀다.

### ㉯ §3-4 ①의 「이 60줄」은 54줄이다 (문서가 자기와 어긋나 있었다)

- 옛 문장: 「이 60줄에 대해 **코너 위험은 논점이 아니다**」.
- 같은 항목 첫 줄이 이미 「58줄 중 54줄은 코너 노출이 0이다」이고, §1-F가 「60은 (토큰, 줄) 쌍의 수이고 실제 소스 줄은 58」이라고 못박았다.
  즉 60은 이 문장에 올 수 없는 수다. **54 + 4(바깥 실루엣) = 58**로 맞는다.
- 코드 대조가 아니라 **문서 내부 산술**로 판정했다.

### ㉰ §5-4 처방(무장 링을 `Accent`로)은 여전히 미채택(P2)이다

- 「지금은 α0.14가 … 구멍으로 보이던 자리다」의 「지금은」만 R14 판 표기로 정정했다. 그 면은 이제 합성돼 있다
  (`GearRadialMenuWidget.cs` 앵커 `Color quitArmedFace = UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurface)`, 적중 1).
- 그러나 **처방 자체는 들어가지 않았다.** 같은 함수 주석이 「무장 링을 Accent로 올리는 §5-4는 연출 강도 판정이라 P2로 남는다」고 적고 있고,
  테두리는 `Flatten(AccentBorder, surface)` · 기호는 `WarmAccent`다. **α 결함만 닫히고 강도 판정은 열려 있다.**
- 문서의 오류가 아니라 **열린 배정**이라 취소선이 아니라 확인 문장으로 남겼다.

### ㉱ §7-3 ★가 예고한 새 결함은 발생하지 않았다

- 예고: 게이트가 면을 모르면 F1 버튼 **라벨**이 `#838589` 위에서 1.77 : 1로 지워진다.
- 실제: `GatedInk`가 `(Text, 면)` 쌍으로 등록되고 게이트가 잉크를 **그 면에서** 다시 뽑는다
  (`SettingsControls.cs` 앵커 `ink.Label.color = UiChrome.InkOnSurface(ink.Face, UiChrome.InkRole.Title, Enabled)` 적중 1 ·
  같은 파일 앵커 `titleInk[i + 1] = new SettingsRowGate.GatedInk(` 적중 1). 이 절이 제시한 **두 번째 선택지**가 채택됐다.

### ㉲ 렌더 결함 정정 6건 (의미 불변 — TEAM.md §5 「렌더 결함 정정」 예외)

예외 조건 셋을 다 보인다: ① 바뀐 글자는 렌더 기호(`~` · `*`)뿐 ② 기호를 뺀 본문이 글자 단위로 같다 ③ 사유는 아래 칸.

| 위치 | 옛 표기 | 새 표기 | 무엇이 어떻게 깨져 보였나 |
|---|---|---|---|
| §4-3-a | `3.0005~3.0096` · `2.9895~2.9999` | `3.0005\~3.0096` · `2.9895\~2.9999` | **한 문단 안에 단일 물결표가 둘이라 짝이 맞아 취소선으로 렌더됐다.** 「3.0096인데 … 7종이 2.9895」가 그어져 보였다. 백슬래시 2자 **삽입만** |
| §4-3-a | `**0.699 %**이고` | `**0.699 %이고**` | 닫는 `**` 앞이 `%`(구두점)이고 뒤가 한글이라 GFM이 **닫지 못한다** — 별표가 그대로 보였다. 기호 위치만 이동 |
| §4-3-a | `**+4.37 %**로` | `**+4.37 %로**` | 같은 형태 |
| §7-1 인용 | `*"두 지표는 반대 방향이다"*라고` | 이탤릭 기호 2개 삭제 | 닫는 `*` 앞이 따옴표, 뒤가 한글이라 닫히지 않았다. 따옴표가 이미 인용을 표시하므로 기호만 뺐다 |
| §7-3 F2 인용 | `*"…읽힌다"*였다` | 이탤릭 기호 2개 삭제 | 같은 형태 |
| §7-3 F3 인용 | `**「홈」**으로` | `**「홈」으로**` | 닫는 `**` 앞이 `」`(구두점)이라 닫히지 않았다 |

§6 표의 `0.55~0.95`는 그 칸에 물결표가 하나뿐이라 **원래 무해**했지만, 같은 라운드에 이스케이프를 넣어 두었다(삽입만 1자).

검사는 두 방법으로 했다: ① CommonMark 좌우 플랭킹 규칙을 구현한 짝짓기 스캐너 ② 「기호 앞이 구두점이고 뒤가 글자」인 형태만 세는 독립 가족 스캔.
★ 짝짓기 스캐너의 **첫 판은 중첩 강조를 버리면서 §7-3 F3의 결함을 조용히 감췄다**(안쪽 열린 기호를 스택에서 지웠다).
가족 스캔이 그것을 잡아 5건이 6건이 됐다 — **검사기가 자기 판정을 깨뜨리는** 이 저장소의 형태 그대로다.

### ㉳ 줄 번호 인용 → 고정 문자열 앵커 (제자리 전환)

현재 사실을 말하는 인용은 **전부 앵커로 바꿨다**(아래 표). 옛 인용은 이 칸에 백틱 없이 적는다 — 앵커 추출 도구가 산 앵커로 읽지 않게 하는 TEAM 규칙이다.

| 위치 | 옛 인용 | 새 앵커 | 적중 |
|---|---|---|---|
| §0 검산 1·2·3 | UiChrome.cs:531 · :183 · :201 | `9.06 / 11.13 / 11.01` · `그 최대(전설)가 카드면 대비` · `α0.62에서 실효색` | 1 · 1 · 1 |
| §0-1 | UiChrome.cs:70-81 | `RGB<b>와 알파에 똑같이</b> 적용된다` | 1 |
| §1-D | UiChrome.cs:1322 | `public static RectTransform AddOpaquePanel` | 1 |
| §1-E | Cards.cs:509 | `생 CardBorder/AccentBorder(α<1)를 그대로 얹지 않는다` + 파일명 정정 | 1 |
| §2 (3) | CharacterInfoWindow.cs:1726 · Tabs.cs:266 · :276 · TodoPostItWidget.cs:1096/1153 · GearRadialMenuWidget.cs:1376 | `AddSurface(display, "NameHit", Color.clear` · `AddSurface(stripRect, "Tab" + name, Color.clear` · `active ? UiChrome.Accent : Color.clear` · `new Color(0f, 0f, 0f, 0.001f)` · `b.Progress = 1f;` | 1 · 1 · 1 · 2(그 파일) · 1 |
| §3-4 ① | PopoverPanel:624 · CharacterInfoWindow.cs:1299 · SettingsWindow.cs:985 · TodoPostItWidget.cs:1025 | `AddOpaquePanel(canvasGo.transform, "…"` 4종 | 각 1 |
| §4-2 ★ | UiChrome.cs:552 | `private const float RarityBorderAlpha` | 1 |
| §4-3-a | UiChrome.cs:1341-1345 · :1391 | `public const float MinNonTextContrast` · `최소 해를 그대로 상수로 박으면 여유가 0이 된다` | 1 · 1 |
| §4-3-b | UiChrome.cs:1592-1603 | `public static readonly Color[] BrightTextBackdrops` | 1 |
| §4-3 링·액자 | CharacterInfoWindow.cs:1122 · :1776 · UiChrome.cs:152 | `_inkRings[i].color = active ? SelectedRingOn(fill)` · `UiChrome.EdgeOnSurface(fill.color), 1.5f` · `InkContrastCharcoal = new Color(0.145f` | 1 · 1 · 1 |
| §4-4 | WindowTheftRenderer.cs:95 · :96 · :109 · CharacterPortraitStage.cs:828-830 | 정의 3줄(`GhostFrameColor` · `GhostTitleColor` · `DustColor` 전체 식) · `감기 방향이 무관하고` | 각 1 |
| §5-4 | GearRadialMenuWidget.cs:1670 | `강조색은 <b>무장 전용</b>이다` | 1 |
| §6 | TodoPostItWidget.cs:1096/1153 · :1376 | 위와 같음 | 2 · 1 |
| §7-1 | UiChrome.cs:1384 · :1376-1379 · :1500-1503 | `public const float ControlFaceContrastTarget` · `두 지표는 <b>반대 방향</b>이다` · `이분 탐색이 아니라 <b>격자 탐색</b>인 이유` | 1 · 1 · 1 |
| §7-3 ★·F2·F3 | SettingsControls.cs:810-819 · :1207-1209 · UiChrome.cs:1374 · :1368-1369 · SettingsControls.cs:1242 · UiChrome.cs:497 | `ink.Label.color = UiChrome.InkOnSurface(ink.Face, UiChrome.InkRole.Title, Enabled)` · `titleInk[i + 1] = new SettingsRowGate.GatedInk(` · `테두리만 있는 것은 버튼이 아니라 <b>입력칸</b>으로 읽힌다` · `커밋 39ab690` · `UiChrome.EdgeOnSurface(colors[i])` · `트랙은 「보이는 구획선」이 아니라` | 1 · 1 · 1 · UiChrome.cs 1(저장소 3파일) · 1 · 1 |
| §8 C-6 · §10 | UiChrome.cs:608-612 (2곳) | `<b>4의 배수</b>만 세 배율 전부에서 정수 픽셀이 된다` | 1 |
| §9-2 · §9-4 | UiChrome.cs:1379 · PortraitBackdropGlowAlphaTests :101 · :324 | `그래서 아래 두 목표는 <b>언제나 한 쌍으로</b> 쓴다` · `네거티브 컨트롤의 반대편` · `string rawGlow = "= UiChrome."` | 1 · 1 · 1 |

**앵커에 `<b>` 태그가 들어간 것들은 원문이 그렇게 생겼기 때문이다** — 태그를 뺀 문장은 파일에 없으므로
고정 문자열로 찾으려면 태그째 써야 한다(TEAM 규칙: 앵커에 EM DASH·스마트 따옴표는 넣지 않는다. 위 앵커에는 없다).

**앵커로 바꾸지 않고 판 표기(`349048f2`)로 남긴 것**: §5-1 · §5-2 · §5-3 인계표 전체, §5-1의 오탐 경고 블록,
§2 (2)의 예시 줄, §3-4 ②의 부채꼴 줄 목록, §7-3 F1/F2/F4 표의 「파일:줄」 칸, §7-5 표, §8 C-1~C-3.
이유: 이것들은 **현재 상태가 아니라 R14 시점의 인계 대상**을 가리키는 인용이고, TEAM 규칙이 과거 판 인용에 요구하는 것은
앵커가 아니라 **판 표기**다. 각 절 머리의 ★ 블록이 그 표기를 지고 있다.

**계량** — 줄 번호 인용은 **본문 146건에서 91건으로** 줄었다(full 73 · 확장자 없는 형태 1 · 꼬리 `:N` 17).
남은 91건은 전부 위에 적은 판 표기 구역 안에 있다. 이 절(§12) 안의 인용(측정 시점 30건)은 **계수에서 뺀다** —
정정 기록 절은 옛 문장을 원문 그대로 남기는 곳이라 본문 1 + 기록 1의 중복이 **구조적으로** 생긴다
(TEAM.md 「기준과 대상이 같이 낡은 스냅숏」 규칙 3의 리더 판정 2026-09-26).
- 형태 정의: full = `파일명.확장자:숫자`(범위·복수 포함) · 확장자 없는 형태 = `` `PopoverPanel:624` `` 같은 것 ·
  꼬리 = `` `:1776` ``처럼 파일명 없이 줄 번호만. **발생 수**로 셌고 줄 수로 세지 않았다.
- 앵커 생존 검증: 이 라운드에 새로 넣은 앵커를 **(대상 파일, 기대 횟수)** 표로 만들어 전수 확인했고(46/46),
  존재하지 않는 앵커 2개를 **음성 대조**로 함께 돌려 0을 확인했다. 음성 대조가 없으면
  「0건이라 깨끗하다」와 「검사기가 죽었다」를 가를 수 없다.
- 렌더 결함은 정정 뒤 같은 스캐너로 **물결 취소선 0 · 닫히지 않은 `~~` 0 · 닫히지 않는 강조 0**이다
  (정정 전 1 / 0 / 10).

### ㉵ 이 라운드의 자백 — 내 검사기가 세 번 틀렸다

1. **죽은 프로브를 만들었다.** `강조색은 무장 전용이다`를 평문으로 찾아 0건이 나왔고, 하마터면
   「그 인용은 죽었다」고 정정할 참이었다. 원문이 `강조색은 <b>무장 전용</b>이다`라 태그가 끼어 있었을 뿐이고,
   태그째 찾으면 적중 1이다. **「없다」 판정에 양성 대조를 붙인다**는 규칙을 내가 먼저 어겼다.
2. **검사기가 자기 판정을 감췄다.** 강조 짝짓기 스캐너의 첫 판이 중첩 강조를 버리면서(안쪽에 열린 기호를
   스택에서 제거) §7-3 F3의 결함을 조용히 지웠다. 독립 가족 스캔이 그것을 잡아 5건이 6건이 됐다.
3. **꼬리 인용 함정을 내가 새로 만들었다.** 앵커를 문서에서 자동 추출하는 검사기를 만들었더니, 한 줄에
   파일 이름이 여럿인 행(위 ㉳ 표)에서 **이웃의 파일을 대상으로 붙여** 「적중 0」 9건을 냈다. 전부 오탐이었고,
   앵커 위치에서 왼쪽으로 가장 가까운 파일 이름을 대상으로 고치니 사라졌다. 명시 표 검증은 처음부터 46/46이었다.

### ㉴ 미확인 · 넘긴 것

- **§8 C-1~C-7 실기 캡처는 여전히 0장**이다. 이 라운드도 앱을 띄우지 않았다(리더 금지). 「확인됨」이라고 쓰지 않았다.
- §4-4 W-1(월드 렌더러 3건의 실제 블렌드)은 **미측정**이다. 대상이 4건에서 3건으로 줄었을 뿐이다.
- §5-4 강도 판정(C-4)과 §7-3 F1 테두리 삭제 권고는 **리더 배분 대기**다.
