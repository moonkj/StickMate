# 장비 도형 오프라인 검산 자(尺)

`docs/EQUIPMENT_SHAPE_SPEC.md`의 모든 숫자가 여기서 나왔다. 프로덕션 규칙을 그대로 옮겼다:

- `rig.py` — W(획 예산) 유도, 규칙 1 린트(`AccessoryStrokeBudgetTests.DescribeRuleOneViolation`과 같은 식),
  실루엣 프로파일(`AccessorySilhouetteMetrics.ProfileOf`와 같은 72구간 × 5도 + 변 조밀 표본),
  점 포함 판정, 자기교차, 같은 아이템 안 채움 겹침.
- `hair.py` / `items.py` — 30종의 좌표(머리 중심 원점 · R 배수 · +x 진행 방향).
- `verify.py` — **전수 검산**. 위반 0건이어야 한다.
- `ascii.py` — 획 두께를 실제로 얹어 ASCII로 래스터(눈으로 확인용).
- `mkhtml.py` — `design/equipment/equipment-shapes-v2.html`을 **같은 좌표에서** 굽는다.
- `dump.py` / `coords.txt` — 스펙 부록 A의 좌표표.

```
cd design/equipment/verify && python3 verify.py
```

**좌표를 고쳤으면 반드시 `verify.py`를 다시 돌려라.** 문서·그림·검산이 갈라지지 않는 유일한 방법이다.

---

## FX 6 + PET 6 (2026-09-01 추가, design-equipment)

30종 하니스와 **같은 자(尺)**를 쓰되 대상만 다르다(`Interaction/AppearanceShapeBuilder.cs` 소관).

- `appearance.py` — FX/PET 12종의 **현행 좌표**(`*_NOW`)와 **제안 A**(`*_A`). 프로덕션 상수를
  값이 아니라 **식**으로 옮겼다(`footprint_diameter_in_R()` 등).
- `verify_appearance.py` — 전수 검산. 현행 **27건** → 제안 A **0건**.
  30종 하니스에 없는 검사 두 개를 추가로 돈다:
  · `true_min_edge()` — **꺾임 문턱과 무관한 최단 실제 변**. 규칙 1 린트는 "양끝이 모두 45° 이상
    꺾인 변"만 보므로 정12각형(30°)·정14각형(25.7°)·5분할 초승달(40°)을 **통째로 건너뛴다**.
    FX/PET 위반 6건이 그 사각지대에 있었다.
  · `ngon_max(r)` — 반지름 r이 살 수 있는 최대 각수 `π / asin(W/2r)`.
- `cards12.py` — `Resources/Items/*.asset`의 **카드 아이콘 12장** 정원/보조색 감사.
  ★ 2026-09-02: 손으로 베낀 좌표표를 버리고 **.asset을 직접 파싱**하도록 바꿨다(사본은 에셋을
  고치는 순간 거짓 초록을 낸다). 같은 날 12장을 `cardspec12.py` 좌표로 다시 구워 **위반 6장 → 0장**.
- `dump12.py` / `coords12.txt` — 제안 A 좌표 전문(스펙 부록 A).

```
cd design/equipment/verify
python3 verify_appearance.py
python3 cards12.py
```

스펙 본문: `docs/EQUIPMENT_SHAPE_SPEC_FXPET.md`

---

## 카드 폴백 아이콘 42종 (2026-09-01 밤, design-equipment)

`Resources/Items/*.asset`의 폴백 아이콘 ↔ 몸 도형 전수 대조. 스펙 본문: `docs/EQUIPMENT_SHAPE_SPEC.md` **10~11절**.

- `cards42.py` — **.asset을 직접 파싱**해(손으로 베끼지 않는다) 42종을 대조한다.
  카드에 실제로 그려지는 것이 폴백인지 몸인지도 여기서 판정한다
  (`AccessoryCardIcon.TryBuild` 분기를 그대로 옮겼다 — **30종은 폴백을 안 쓴다**).
- `cardrules42.py` — 폴백 42장을 카드 규칙(정원/보조색/최단 실제 변/잉크 사각형/상자 밖)으로 검사.
- `accent30.py` / `accent30b.py` — 리더 확정 판정 축 2개(보조색 조각 수 / 보조색 꼭짓점 수).
  `accent30b.py`는 **닫는 점 1개 셈법 편향**을 걷어낸다(오검출 3건·미검출 1건을 잡는다).
- `derive30.py` — 30종 폴백을 **몸 좌표에서 유도**한다. `--dump`로 좌표 전문.
- `cardspec12.py` — FX/PET 12장 **카드 제안**과 검산(위반 0 / 조각 간격 최악 구간 0). `--dump`로 좌표 전문.
- `handocclusion.py` — 머리 액세서리가 "주위 살피기" 손을 가리는가(11절). β 방향 잉크 반경 표.
- `sheet42.py` / `sheet12.py` — 대조 시안 PNG(`design/equipment/cards42-compare.png`,
  `design/equipment/cards12-proposal.png`). ★ 오프라인 래스터는 **둥근 캡**을 찍는다 —
  코너 붕괴를 가린다. **최종 판정은 실제 빌드 캡처로만 한다.**

```
cd design/equipment/verify
python3 cards42.py        # 42종 전수 대조 + 눈금 교정
python3 cardrules42.py    # 폴백 카드 규칙 검사
python3 accent30b.py      # 축2 셈법 정규화
python3 derive30.py       # 30종 유도 폴백 검산 (--dump 로 좌표)
python3 cardspec12.py     # FX/PET 12장 제안 검산 (--dump 로 좌표)
python3 handocclusion.py  # 손 가림 실측
```

좌표 전문 한 파일: `design/equipment/fallback-icons-derived.txt`

### 배율 축 (2026-09-01 밤 추가 — 리더 최우선 배정)

- **`verify.py`가 이제 배율 0.35 / 0.50 / 0.60 / 0.75 / 1.00 / 1.50을 상시로 훑고,
  "규칙 1 위반 0이 되는 최소 배율"을 이분법으로 찍는다.** 0.35/0.75/1.0/1.5만 재던 관행이
  **0.60 구멍**을 만들었다(사용자 저장 배율이 0.60인데 그 배율 위반 11건을 아무도 못 봤다).
- ~~`scale060.py` / `sheet060.py`~~ — **2026-09-02에 지웠다.** 둘 다 "현행 vs 수정안"을 나란히
  재는 도구였는데, 수정안이 프로덕션에 들어가면서 두 쪽이 같은 값이 됐다(비교할 대상이 없다).
  `items.py`(프로덕션 거울)와 `AccessoryShapeBuilder.cs`에 새 좌표가 들어갔고,
  **위반 0 최소 배율이 0.7120 → 0.7070으로 내려갔다**(`verify.py`가 상시로 찍는다).
  그때 구운 시안 `design/equipment/scale060-hats.png`은 기록으로 남겨 둔다.


---

## 남는 머리 / 색면 예산 (2026-09-02, design-equipment)

사용자 신고 4건("털모자가 머리 전체를 가림" / "모자도 과하게 덮는다" / "아직도 조잡하다" /
"별로 변경된 것 같지 않다")의 계량자. 스펙 본문: `docs/EQUIPMENT_SHAPE_SPEC.md` **13절**.

- `headroom.py` — **남는 머리** 3축(면적비 / 외곽호 / x=0 두께)과 하한 상수 4개.
  머리 잉크는 **채움 + 획 W/2 팽창**으로 잡는다(중심선만 재면 −0.64R짜리 도형의 실제 밑단
  −0.812R를 놓친다 — 리더 손실측이 82%, 실제가 90.6%였던 이유).
  스캔라인 구간 대수라 표본 잡음이 없다. 자기검증 포함.
- `inkbudget.py` — **색면 생존율**(채운 도형이 자기 윤곽선 W/2에 먹히고 남는 면적)과
  **커버선 정직성**(선언한 `HatCoverLocalY`와 실제 잉크 밑단의 차).
  ★ 배율 0.60에서 생존율 중앙값 **14.4%**, 커버선 거짓말 **1.5~2.2획**.
- `beanie.py` — 털모자 후보 9안 비교(남는 머리 x 옆벽 x 실루엣 x 머리카락).
- `verify.py` — **남는 머리 하한이 하드 게이트로 들어갔다**(모자 6종 x 배율 0.75/0.60).
  하한은 `headroom.py`에만 적혀 있다 — verify.py는 그 이름을 참조한다.

```bash
cd design/equipment/verify
python3 verify.py      # 남는 머리 하한 포함
python3 inkbudget.py   # "왜 조잡한가"
python3 beanie.py      # 털모자 후보 비교
```

★ **오프라인 래스터는 최종 판정이 아니다**(CLAUDE.md). 13절의 판정은 실제 빌드 캡처
(PID 66787, 빌드 05:03:02)로 했고, 여기 도구들은 그 캡처에서 본 것을 **숫자로 재현**한다.


---

## R10 — 그늘 낱선 조형 결함 2건 · B군 코다리 (2026-09-03, design-equipment)

그늘 배수가 `×0.62 → ×0.28`로 착지해 축 S 대비가 1.955 → 3.392가 됐다.
**안 보이던 그늘 낱선이 보이게 됐고, 그와 함께 그 낱선의 조형 결함도 보이게 됐다.**
스펙 본문: `docs/EQUIPMENT_SHAPE_SPEC.md` **15절**.

- `r10_fit.py` — 진단. **프로덕션 좌표를 직접 읽는다**(`Tools/ShapeDump/build.sh`) — 거울을 재면
  "거울이 맞나"와 "도형이 맞나"가 섞인다. 축 다섯: 그늘 낱선 7건 / 긴망토 `CapeFold2` 掃引 /
  털모자 `BeanieCuff` 대안 / 코다리 창 / 카드 혼동.
  ★ 교정 블록이 **알려진 ρ값 3개 + 양성 대조 2건 + 음성 대조 1건**을 매번 먼저 통과시킨다.
- `r10_rx.py` — 처방과 판정. 형제 기준선(짧은망토·판초), 44px 카드, **도형별 배율 문턱 랭킹**.
- `r10_install.py` — 처방을 거울 위에 얹고 `verify.py` 전량. `--baseline`으로 대조.
  (거울을 직접 안 고치는 규약은 `r9_bandfix.py`와 같다 — 거울은 프로덕션을 비추는 물건이다.)
- `r10_*.out.txt` — 출력 고정본.

```bash
cd design/equipment/verify
python3 mirrordrift.py                 # 어긋남 0건이어야 한다
python3 r10_fit.py                     # 진단
python3 r10_rx.py                      # 처방과 판정
python3 r10_install.py --baseline      # 기준선
python3 r10_install.py                 # 처방 적용 후 전량
```

### ★ 같은 라운드에 고친 도구 결함 1건
`mirrordrift.py`가 **컴파일 단계에서 죽어 있었다** — §14-1의 베레모 띠 처방이 프로덕션에
`Mathf.InverseLerp`를 들여왔는데 `Tools/ShapeDump/Shim.cs`의 `Mathf` 대역에 그 함수가 없었다.
조용히 죽지는 않았다(`rc=1` + `!! build.sh 실패`). 3줄 추가로 복구했고, 되살린 검사기가
**어긋남 5건**(§14-1 A군 5종)을 잡아내 **자기 양성 대조**를 스스로 보였다. 거울을 맞춰 0건 복귀.

---

## 인계본 아이템 아트 16종 이식 (2026-09-03, design-equipment)

사용자 확정 *"장비는 내가준 디자인 기준으로 비슷하게 다른 장비들도 구현해야지"* +
제약 *"장비는 기존 컨셉처럼 투명해서 머리속이나 그런게 보이면 안됨"* 대응.
기준 문서 = `docs/handoff/design_handoff_equipment_window/reference/ItemIcon.dc.html`(16종).
스펙 본문: **`docs/EQUIPMENT_HANDOFF_PORT_SPEC.md`**

- `handoff.py` — ★ **HTML을 직접 파싱한다**(좌표를 손으로 베끼지 않는다 — `cards42.py`가
  `.asset` 직접 파싱으로 바뀐 것과 같은 이유). SVG path 평탄화 + **슬롯별 좌표 변환식**.
  변환식은 인계본이 스스로 선언한 값에서만 유도했다(무대 머리 `r=28` / 렌더 폭 158px /
  오버레이 박스 70·48·54·88px).
- `handoff_cal.py` — ★ **교정. 이게 깨지면 아래 숫자를 전부 폐기한다.**
  앵커 A(무대→R): 인계본 자기 캐릭터 눈 ↔ 우리 `rig` 상수(눈 x 4.8% 차).
  앵커 B(아이콘박스→R): 안경다리·고글끈·왕관테 끝이 **머리 가장자리 ±1.0 R**에 닿는가.
  ★ **배율이 1.458배 다른 두 박스(48px·70px)가 둘 다 1.0 R을 낸다**(최악 3.4%).
  **음성 대조 내장** — 박스를 절반/두 배로 틀면 실제로 빨개진다.
- `handoff_gate.py` — 16종을 우리 자로. 카드(44px·58px) + 착용(0.75·0.60).
- `handoff_piece.py` — ★ **조각별 해상도 예산.** 91조각 중 **존재 47 / 소멸 44**.
- `handoff_rx.py` — 새벽 처방 3건 ↔ 인계본 정면 대조. **양성 대조 내장**
  (현행 `CapeFold2`가 실제로 −0.0443 R로 밖인지 먼저 재현한다).
- `handoff_color.py` — 색 게이트(자립 대역 · `WornColor` 항등 · **알파 합성**).
  `design/art/verify/colorlab.py` 교정 16건을 그대로 통과시킨 뒤에만 숫자를 낸다.
- `handoff_port.py` — 이식 단위(액자 1.75R · 감쌈 · **몸통 배수 재표현** · 망토 착용 도형).
- `handoff_fix.py` — ★ **처방 재검산.** 처방을 낸 뒤 그 처방으로 다시 잰다.
  **이걸 돌려서 내 처방 2건이 틀렸다는 것을 잡았다**(중절모 −0.30 → −0.35 / 그라디언트 여유 과대평가).
- `handoff_dump.py` — 1차 기계 이식 좌표 → `handoff_ported_coords.txt`. **미조정본이다.**
  ★ **2026-09-03(R12) 결함 수정.** 구판 솎기(각도 8도 문턱)가 저곡률 구간을 통째로 지워
  **47조각 중 23조각**이 형태를 잃었고(선글라스 렌즈 세로 0.5425 R → **0.0093 R**),
  존폐 판정이 `max(가로,세로)` 하나라 **조용히 초록**이었다.
  → Douglas–Peucker(수직편차 0.010 R) + **경계 극점 4개 강제 보존** + **조각마다 자가검산**
    (어기면 `SystemExit`) + **규칙 1-C(ρ_max) 추가**로 바꿨다.
  ★ **좌표표를 인용하기 전에 반드시 `handoff_dumpfix.py` 를 먼저 돌려라.**
- ★ `handoff_dumpfix.py` — 위 결함의 **양성·음성 대조 4종**. A 음성(구판이 감사에 걸리는가,
  23/47) · B 양성(신판 47/47) · C 합성(해석해를 아는 도형 3종) · D 변이(훼손을 잡는가, 3/3).
- ★ `r12_view.py` — **시점(view) 축.** 인계본은 정면 도형, 우리는 3/4 도형이다.
  `c`(덩어리 오프셋) / `A`(거울 잔차 지수) / `k`(부위 단축) 세 성분으로 나눠 재고,
  야구모자 「감쌈은 평행이동으로 못 산다」를 검산한다. `--control` 로 강제 대칭화하면 14 → 0 종.
  ※ `F/B` 비는 봉투 양 끝만 보므로 이 축을 부분적으로만 본다(반례: 인계본 `scarf` F/B 1.000 · A 1.134).
- ★★ `r12_head.py` — **이식 1단계(HEAD) 관문.** 처방 9건을 **한 벌로** 얹고 8축을 한 번에 잰다
  (액자 · 감쌈 · 꼭대기 · 잉크밑단 · 몽당변 · 1-A · 1-C · 자기교차 · 쌍별 실루엣).
  ★ **적용 순서가 결과를 바꾼다**: 형태 → 두께(다시 풀기) → `c`. 액자 축소가 ρ 를 같이 줄인다.
  ★ **액자는 반경 도달이 아니라 꼭대기 y 다** — 반경으로 재면 우리 출하 모자가 먼저 죽는다
    (야구모자 +0.200 · 중절모 +0.329 · 밀짚모자 +0.451인데 `verify.py` 는 위반 0건).
  ★ **`c` 재부여도 조각 단위다** — 아이템 c 를 관에 걸면 챙(A 성분)과 이중계상된다.
  실측: 4종 전량 통과 · **DROP 후보 0건** · 감쌈 **0/4 → 4/4**.
- ★ `r12_thick.py` — **1-C 미달 조각을 두께 축(단축)으로만 올리고 이웃을 재검산한다.**
  ★ **전/후 대조**로 「이식본이 원래 그런 것」과 「처방이 깬 것」을 가른다 — 그 구분이 없으면
  처방을 냈는지 부쉈는지 알 수 없다. 실측: 13건 처방 · **만든 위반 0건** · 없앤 위반 4건 ·
  1-C 미달 13→0 · 못 올린 조각 0. 재검산 축: 1-A · 자기교차 · 액자 1.75 R · 슬롯 경계 ·
  눈 간격 · 쌍별 실루엣(이식 4 + 우리 2 = 슬롯당 6종).
  ★ **`GATE = 0.21818 R` 은 상수다. 초록을 만들려고 무르게 하지 마라.**
  ★ ⑦-4가 **`c` 재부여 실험**이다 — 정면화본 HEAD 0.75획 → `c` 재부여 1.76획(하한 1.0).
- ★ `r12_band.py` — 강조띠 두께 판정. **띠는 채움이라 1-A(≥1.0 W)가 아니라 1-C가 지배한다.**
  곧은 띠는 `ρ_max = 두께/2` 이므로 필요 두께 **0.43636 R = 1.269 W**.
  `AccentBandThicknessRatio = 0.46` 이 5.4% 여유로 통과. `--control`(0.43)에서 실패 검출.

```
cd design/equipment/verify
python3 handoff_cal.py      # ★ 먼저. 실패하면 아래를 돌리지 마라
python3 handoff_gate.py ; python3 handoff_piece.py ; python3 handoff_rx.py
python3 handoff_color.py ; python3 handoff_port.py ; python3 handoff_fix.py
python3 handoff_dumpfix.py                                   # ★ 좌표표를 믿기 전에 이것부터
python3 handoff_dump.py --full > handoff_ported_coords.txt
python3 r12_view.py ; python3 r12_view.py --control
python3 r12_band.py ; python3 r12_band.py --control
python3 r12_thick.py ; python3 r12_thick.py --control
python3 r12_head.py  ; python3 r12_head.py  --control   # ★ 이식 1단계 관문
```

★ **이 하니스는 「인계본이 옳은가」를 재지 않는다.** 인계본을 **우리 제약 위에 놓았을 때
무엇이 남고 무엇이 사라지는가**를 잰다. 채택 판정은 리더 소관이다.
