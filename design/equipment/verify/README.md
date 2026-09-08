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

---

## R13-P4 — 안대 끈 각도 (2026-09-05, design-equipment)

R13-P1(드러난 눈 아몬드 → 원반 r=0.33 R)이 눈을 위아래로 0.09 R씩 키우면서 끈-눈 간격이
1.57 → **1.31획**(문턱 1.5획)으로 떨어졌다. 스펙 본문: `docs/EQUIPMENT_SHAPE_SPEC.md` **16절**.

- `r13_patchstrap.py` — 처방(`PatchStrapDegrees` 122° → **111°**)을 **거울 위에 얹고**
  `verify.py` 전량. 거울을 직접 안 고치는 규약은 `r10_install.py`와 같다.
  · `--sweep` 각도 축 전체 + **천장/문턱 이분법**(천장 1.6032획은 **천 뒤변**이 만든다 —
    끈 각도로는 못 넘는다. 이 사실이 «각도를 더 낮출 이유가 없다»의 근거다)
  · `--baseline` 현행 122° → 위반 1건  · `--control` 116°(일부러 미달) → 위반 1건(음성 대조)
  · 기본 실행 111° → **위반 0건**
  ★ `rig.stroke_gap`은 선을 21점으로 표본한다. 문턱 근처에서 값을 고르는 중이라 이 도구는
    **정확 선분-선분 거리**(`exact_gap`)를 따로 쓴다. 두 값의 차가 이 도형에서 <1e-3 R인 것이
    표본이 충분했다는 **양성 대조**다.
- `r13_patchstrap.out.txt` — 위 넷의 출력 고정본.

```bash
cd design/equipment/verify
python3 r13_patchstrap.py --sweep
python3 r13_patchstrap.py --baseline   # 위반 1건이어야 한다
python3 r13_patchstrap.py --control    # 위반 1건이어야 한다
python3 r13_patchstrap.py              # 위반 0건
```

★ **활(3점/변) 끈은 검산을 통과하지만 기각했다** — 문턱만 넘기는 최소 활 깊이가 0.35~0.58 pt로
**획 하나(2.00 pt)보다 얇다.** 게이트만 초록으로 만드는 유령이다. 근거는 스펙 16-5절.

---

## R15 — 인계본 16종 「보이는 그대로」 두 표면 설계 + 나란히 비교 시트 (2026-09-05, design-equipment)

사용자가 세 번 말했다(*"내가 준 디자인 컨셉 정도의 장비 퀄리티가 나와야함"*). 앞 라운드들은 **우리 자**(획 두께·정원)로
우리 도형을 재고 「충족」이라 적었다 — 잘못된 질문에 정확히 답한 것이다. R15의 자는 **눈**이다: 인계본 렌더와
우리 렌더를 **같은 크기로 나란히** 놓는다. 스펙 본문: `docs/EQUIPMENT_HANDOFF_PORT_SPEC.md` **§13**.

- `r15_model.py` — ★ 안 D 데이터 모델(`strokeGrade` 0~3 · `Tone 3` 하이라이트 · `surfaces` Body/Card · 카드 알파)에
  16종을 담는다. `CARD_SET`(인계본 91조각 전량, 정면 기하) · `BODY_SET`(프로덕션 3/4 기하 + 인계본 레이어를
  조각 단위 상자 아핀으로 얹고 **획 등급 실폭**으로 1-A/1-C 생존 판정) · `SHARED`(「좌표 한 벌」 변형).
  **교정**: 조각 91 / H 17 / §4-2-7 잉크값 7건이 안 맞으면 `SystemExit`. 사전합성 색(카드: 브라스 over `#15181E`,
  몸: 흰 42% over 재질색)도 여기서 나온다. 출력 고정본 `r15_model.out.txt`.
- `r15_raster.py` — 4× 슈퍼샘플 래스터. ★ 첫 판은 **모든 꼭짓점에 둥근 캡**을 찍어 윤곽이 울퉁불퉁했다 — 도구 결함이었고
  고쳤다(캡은 끝점에만). 이 경고가 README 맨 위에 있는 이유가 그대로 재현된 셈이다.
- `r15_compare_sheet.py` — ★★ **비교 시트 3장.** 인계본 쪽은 `docs/handoff/.../render/icons.js`(ux-designer 재현기)를
  headless Chrome 으로 찍는다(원본 HTML 은 `support.js` 부재로 안 열린다).
  · `r15_compare_sheet.png` — 16행 × [A 인계본 카드 | B 우리 카드 설계 | C 인계본 착용 프리뷰 | D 우리 몸 부착 설계 | E 아직 다른 점].
    A·B 는 같은 64→232px 프레이밍·같은 팔레트, C·D 는 같은 머리 반경(32px).
  · `r15_compare_truesize.png` — 실제 크기(카드 58pt 1×/2× · 착용 인계본 1× / 우리 @0.75 1×·2×).
  · `r15_compare_shared.png` — 「좌표 한 벌」이면 카드가 어떻게 보이는가(HEAD 4 + EYES 4). **눈으로 기각됐다.**
- `r15_dump.py` — 두 표면 좌표 전문 `r15_coords.txt` + 데이터 계약 통계(정원·보조색 수·최대 점 수·알파 스펙트럼).

```bash
cd design/equipment/verify
python3 r15_model.py > r15_model.out.txt      # 교정 + 생존 판정 + 색 + 쌍별 실루엣
python3 r15_compare_sheet.py                  # ★ Chrome 필요. 시트 3장
python3 r15_dump.py                           # 좌표 전문 + 계약 통계
```

★ **눈으로 기각한 조각을 숫자 자가 못 잡는다** — 털모자 결(`BeanieCuffWave`)은 1-A 를 통과하지만 진폭이 획 폭과 같아
「결」이 아니라 「굵은 물결선」으로 읽힌다. `r15_model.EYE_REJECT` 에 그 판정을 적어 두었다. 자는 「존재」만 잰다.

---

## R16 — 착용 표면 재설계: 획 1pt 하한 · 브라스 단색 · 인계본 착용 기하 그대로 (2026-09-05, design-equipment)

사용자 판정(R15 시트): *"왼쪽 모자들은 인계본과 비슷한데 착용 모습이 아예 다름"* → 카드(B) 합격 · 착용(D) 불합격,
*"특히 안경은 심함"*, 합격선은 *"동일하게 읽힌다"*. 리더 전제: 착용 획 하한 **2pt → 1pt** · 착용 색 **브라스 단색** ·
D열이 C열처럼. 스펙 본문: `docs/EQUIPMENT_HANDOFF_PORT_SPEC.md` **§14**.

- `r16_model.py` — ★ 착용 기하 = **인계본 착용 프리뷰 그 자체**(README 「장비 오버레이 위치」: 정면 아이콘을 슬롯 박스
  70/48/54/88 에 · 획 2.8/3.4/3.4/2 · 등 아이콘 40 % · 망토는 무대 도형 뒤판+칼라+걸쇠). R12~R15 의 3/4 재저작 기하는 이 표면에서
  폐기. 획은 슬롯 명목(0.115~0.138 R) × 조각 배수에 **1pt 하한**을 건 실폭. 배율 0.35/0.60/0.75/1.00 × 하한 1pt/2pt 로
  1-A/1-C/자기교차 생존을 전 조각(87)에 찍고, R15 「구조적 소멸 29」를 재판정한다. **교정**: 조각 91 / H 17 / 잉크값 7건 /
  걸쇠 중심 / ρ_max 벡터화 ↔ 순수 파이썬 3건. 출력 고정본 `r16_model.out.txt`.
- `r16_dupattr.py` — ★★ **재현기 결함의 증명.** `render/icons.js`(ux-designer 재현기)는 같은 이름의 SVG 속성을 두 번 내고
  HTML 파서는 **첫 것만** 남긴다 → 원문(React)의 `fill: A` 채움 23건 · 획 배수 11건(조각 33/91)이 R15 A/C 열과 `icons_16.png`
  에서 **조용히 빠져 있었다.** 두 픽셀(rect 색 · line 두께) + 양성 대조로 찍는다. 출력 `r16_dupattr.out.txt`.
- `r16_raster.py` — 알파 합성 래스터(반투명 채움 · objectBoundingBox 그라디언트 · 낱선 알파 · 파선 → 점열 · 그룹 알파).
- `r16_worn_sheet.py` — ★★ **비교 시트.** `r16_worn_sheet.png` = 16행 × [C 인계본 착용(재현기 그대로) | C* 인계본 착용(원문 의미로
  고친 재현기) | D′ 우리 착용(인계본 무대+캐릭터 위에 우리 장비만 — 1pt 하한·브라스) | D″ 출하 화면(검은 잉크·밝은 바탕·
  잉크로 꽉 찬 머리·눈 없음) | E]. **EYES 4행이 맨 위.** `r16_worn_truesize.png` = 실제 크기(@0.75 1×/2× · @1.00 1×/2× ·
  어두운 바탕). `r16_raster_control.png` = 도구 교정(Chrome C/C* vs 내 래스터, 장비 픽셀만 평균 |Δ| — 아이콘 11~21/255 ·
  망토 2~3/255).
- `r16_dump.py` — 좌표 전문 `r16_coords.txt`(조각별 명목/실폭 획 · 채움/선/파선 · 배율별 생존).

```bash
cd design/equipment/verify
python3 r16_dupattr.py > r16_dupattr.out.txt   # ★ Chrome 필요. 첫 속성 승 + 양성 대조
python3 r16_model.py   > r16_model.out.txt     # 교정 + 획 표 + 색/WornColor + 생존 + 29 재판정 + 안경 실측
python3 r16_worn_sheet.py                      # ★ Chrome 필요. 시트 3장
python3 r16_dump.py                            # r16_coords.txt
```

★ **R15 시트의 도구 오류 자백**: R15 D열은 본체를 **속 빈 머리 + 눈동자**로 그렸다. 프로덕션은 2026-09-01 P1 이후
**잉크로 꽉 찬 머리 원반, 눈 없음**(`SceneBootstrapper.BakeEyes = false`)이다. 사용자가 판정한 D열의 본체는 존재하지 않는
본체였다. R16 D″ 는 프로덕션 본체로 그린다.

---

## R17 — 착용 표면 개정: 모자 불투명·머리 맞춤 · 외알안경 오른쪽+반대쪽 눈 · 강조=등급색 · 줄무늬타이 목 하단 (2026-09-05, design-equipment)

R16 시트에 대한 사용자 지적 5건(모자가 비친다 · 머리보다 작다 · 외알안경 왼쪽 · 반대쪽 눈 없음 · 줄무늬타이가 머리 쪽) + 리더 정정
(강조 = 등급색). **디자인 소유자 지시가 인계본 파일보다 우선** — 인계본 착용 프리뷰와 일부러 다르다. 스펙 본문: `docs/EQUIPMENT_HANDOFF_PORT_SPEC.md` **§14-10**.

- `r17_model.py` — 몸 표면 변환 표. **모자 맞춤 전수 탐색**(`fit`: u 0.001 · dy 0.02 격자, 규칙 H-2 = 불투명 합집합 중앙 반폭 ≥ 머리 현(1.184 R) +
  0.06, 착용선 ≤ 목표, 꼭대기 < 2.551, 밑 > −1.0; 균일 배율 불가면 ky 0.95→0.70 대안) · **불투명 바탕 조각**(`base`, 머리 채움색) · 외알안경 `x→64−x` ·
  조건부 `ExposedEye`(흰 r 1pt @(−0.46,+0.107)) · 줄무늬타이 dy −0.2871 · NECK 착용선 규칙 N-1 · 등급색 팔레트. 출력 `r17_model.out.txt`.
- `r17_sheet.py` — `r17_priority_sheet.png`(외알안경 → 모자 4 → 줄무늬타이 + ★ **자홍 머리 불투명 증명 띠**: 합집합 안 자홍 픽셀 792/1325/776/577 → 0,
  양성 대조 통과) · `r17_worn_sheet.png`(16행) · `r17_worn_truesize.png`. 열은 R16 과 같다(C | C* | D′ | D″ | E).
- `r17_dump.py` — `r17_coords.txt`: 머리에 **변환 표**(HEAD u/ky/dy · MIRROR · SHIFT · 조건부) + 조각별 좌표·획·색·생존.

```bash
cd design/equipment/verify
python3 r17_model.py > r17_model.out.txt
python3 r17_sheet.py          # ★ Chrome
python3 r17_dump.py
```

★ 털모자는 균일 배율로 액자(2.551 R)를 못 넘는다(관 23 u ≪ 단 40 u, 단 위 높이 28.6 u → 꼭대기 3.13 R) — 세로 압축 ky 0.80 으로 냈다.
★ 중절모 챙은 균일 배율에서 4.06 R(팔 폭)이 된다 — 인계본 비 52/24 의 정직한 결과, 대안은 리더 판단.

### R17b/c/d 추가 (2026-09-05, design-equipment)
- **R17b** 외알안경 사슬·구슬은 반전하지 않고 알 상대 오프셋 유지(바깥쪽) — `r17b_monocle.py` → `r17b_monocle.png`.
- **R17c** 알 중심 = 반대쪽 눈의 거울 위치 (+0.46, +0.185) R, 구슬 y −0.03 R(원반 가장자리 회피) — `r17c_monocle.py` → `r17c_monocle.png`.
  검산 4건(머리 현·중심선·몸 간섭·슬롯 64 초과 5.5 u/몸 표면 클리핑 없음)은 `r17_model.out.txt` §2.
- **R17d** 망토 길이 2단(규칙 B-1/B-2/B-3): 짧은 := 현행 긴 뒤판(밑단 −5.089 R), 긴 = 발목(−9.055 R, 세로 ×2.005 · 폭 ×1.536 플레어 유지).
  `r17d_capes.py` → `r17d_capes.png`(망토 2행 + **웅크리기 최대 칸**: 긴 밑단이 발목선 아래 2.18 R 관통 → HemSway 루프 클램프 처방). 숫자는 `r17_model.out.txt` §7.
- 시트 글자의 U+2212 「−」가 폰트에 없어 □로 찍히던 결함 → `r16_raster.sanitize` 로 치환(R17 시트 전부 재생성).

### R18+R19 (2026-09-05, design-equipment) — 재질 팔레트 적용 + 사용자 지적 6건
- `r19_model.py` — 색 = `palette_model.ROLE`(design-art 정본) + `ROLE_FIX` 4건(선글라스 렌즈 SH/테 M2 · 고글 렌즈 윤곽 없음 · 구슬 M2 윤곽 없음 r 1pt · 배낭 끈 M2).
  기하: 모자 2층(챙 장축 분할 · 왕관 뒷벽 · 왕관 재맞춤 골 ≤ 머리 꼭대기 −0.40) · 나비넥타이 dy +0.30 · 배낭 = 뒷판+손잡이(뒤층)+어깨끈 2(앞층) · 투명 렌즈 몸 채움 없음 · 그룹 α 1.0. 출력 `r19_model.out.txt`.
- `r19_sheet.py` — `r19_priority_sheet.png`(9행 + 자홍 대조 재정의판: (a) 앞층 안 0 · (b) 뒤층∩머리 획 밖/기하) · `r19_worn_sheet.png`(16행 × 4 잉크/바탕 변형 + 웅크리기 망토 칸) · `r19_card_sheet.png`(44px + 2×).
- `r19_dump.py` — `r19_coords.txt`(좌표 + 층 + 색 역할 + 검은/흰 잉크 해석 hex; 카드 절 포함). 스펙 §14-10-8.

### R20 (2026-09-05, design-equipment) — 나머지 26종 전수 대조 · 격차 등급 · (가)군 5종 좌표
- `r20_model.py` → `r20_model.out.txt` — 26종을 인계본 규칙(P/S/H/C/G/L)에 대조한 표, 등급 (가)4 / (나)4 / (다)18, (가) 4종(안대·펜던트·반다나·요정날개) 좌표+층+색 역할 + 밀짚모자 임시 좌표(H-2 맞춤 u 1.190 dy +0.580, H-1 챙 분할은 3/4 챙이 찢어져 철회 → 나), 펜던트/반다나 N-1 dy.
- `r20_sheet.py` → `r20_sheet.png` — (가) 4종 + 밀짚모자 임시, 카드 44/88 + 착용 4배경. `r20_dump.py` → `r20_coords.txt`. 스펙 §14-11.
- `r20_eyescover.py` → `r20_eyescover.out.txt` — `EyesVisorOpacityTests` 의 격자·간격·눈 자리 계측을 복제(변형 후보 사전 검사; EditMode 실측은 coder). 결과: 동그란안경 GLASS 판 27칸 · 외알안경 알·눈 ±0.56 에서 간격 1.52 W · 6종 쌍별 최소 0.24. 스펙 §14-10-9(규칙 E-2).
- 2026-09-05 상수 정정(design-character 감사): `r16_model.HEAD_OUTER_R` 1.1432 → **1.171932 R**(실효 링 계수 0.0756501), 목 0.14625 R. 나비넥타이 dy +0.30 → **+0.269**(계산식), 펜던트 −0.084 · 반다나 −0.204. r17_model.out/r19/r20 재생성, r17 PNG 는 역사(미재생성). 스펙 §14-10-10.

### R21 (2026-09-05, design-equipment) — (나)군 4종 재저작 · (다)군 18종 창작 · 테마 배정
- `r21_model.py` → `r21_model.out.txt` — (나) 밀짚모자(렌즈꼴 챙 2층, 관 16u, HAT_FIT u 0.075 dy +3.62) · 베레모(처진 원반 + 띠 5u + 꼭지, u 0.070 dy +2.88) · 뿔테안경(바 11u + GLASS 렌즈 2 + 다리 2 + H 2, E-2 쌍별 최소 0.27) · 판초(뒤판 −4.33/3.20 R + 술 4 + V 앞자락) / (다) 머리카락 6(정면 기하: 이마선·구레나룻·돔, 앞층 + 뒤층 2) · FX/PET 12 카드 64u. ★ 모자 H-2 를 **앞층만의 합집합**으로 다시 걸었다(먼 쪽 챙은 머리 뒤). 몸 51/51 · 카드 97/97.
- `r21_sheet.py` → `r21_sheet.png`(10행 + H-1 자홍 띠: (a) 0/3071·0/3929 (c) 착용선 위 머리 노출 0 px) · `r21_card_sheet.png`(FX/PET 12 + 현행 몸 대조). 커버선 절단(Sutherland–Hodgman)을 시트에 복제해 「머리카락 + 야구모자」 칸을 만들었다.
- `r21_dump.py` → `r20_coords.txt` **재생성**(R20 절 그대로 + R21 절: 몸 R / 카드 64u + `theme=`). R20 임시 밀짚모자 절은 「전사 금지」 표시.
- `r21_theme.py` → `r21_theme.out.txt` — 인계본 16종 theme 을 HTML 에서 파싱, 6×4 표, 안 0(16 무변경: 위반 4) / 안 A(3건 변경) / 안 B(4건 변경, 권고) E1~E3 검산. ★ 16종 무변경으로는 E1·E2·E3 동시 만족 해가 없다. 스펙 §14-12.

---

## R2 — 팩 장비 12종 디테일 고도화 (2026-09-08, design-equipment)

사용자 지시 *"각 장비들의 디자인 고도화가 필요함"* — 사이버(2)·광부(7)·대마법사(8) HEAD/EYES/NECK/BACK 12종이
기본 42종(특히 인계본 계열)보다 단순해 보인다는 신고. 스펙 본문: **`docs/EQUIPMENT_SHAPE_SPEC_PACK_DETAIL_R2.md`**.

- `pack_assets.py` — ★ **프로덕션을 있는 그대로 읽는다.** `Resources/Items/pack_*.asset` 의 `wornShapes` 항 스트림을
  `AccessoryWornShapeReader.TryBuild` 와 같은 문법으로 풀고(기저 4 = HeadCenterLine, 0 = HeadRadius), 기본 24종은
  `Tools/ShapeDump/build.sh` 가 뽑은 프로덕션 좌표를 쓴다(캐시 `pack_detail_r2.prod.tsv`, `--refresh-prod` 로 재생성).
  손으로 베낀 좌표는 0개다.
- `legibility.py` — `AccessoryNameLegibilityTests.NormalizedCells/Difference` 의 파이썬 거울(64×64 정규화 격자 · 획 반폭 0.0494).
  **교정: 출하 Patched Hood ↔ 베레모 = 0.100(테스트 CardDebt 실측과 일치).** 깨지면 하니스가 멈춘다.
- `pack_detail_r2.py` — 12종 R2 좌표 + 게이트 12축(규칙 1-A/1-C · 자기교차 · **규칙 4 금지대(획 조각 쌍은 닿거나 1.5획 이상)** ·
  슬롯 경계 · H-2/H-2b(뒤층 제외) · E-대역 · 같은 팩 모자×안경 가림 · 카드 판별성(기본 6 + 다른 팩 2, 문턱 0.15) ·
  쌍별 실루엣 래칫 · 카드 58pt/24pt 최단변·보조색 두께 · 모티프 결정나무(보조색 조각 전부)).
  `--control` 나쁜 값 5종 → 위반 25건 검출 · `--dump` 좌표 전문 → `pack_detail_r2_coords.txt` ·
  `--emit` → `design/equipment/pack_detail_r2/*.wornShapes.yaml`(coder 가 .asset 의 `wornShapes:` 블록을 통째로 바꿔 끼운다) ·
  `--verify-emit` 방출본을 에셋 파서로 되읽어 설계 좌표와 대조(양성 대조 내장).
- `pack_detail_r2_sheet.py` → `design/equipment/pack_detail_r2_sheet.png` — 출하 vs R2 나란히 12행. **오프라인 래스터 — 최종 판정 아님.**

```bash
cd design/equipment/verify
python3 pack_detail_r2.py                # 위반 0건 (pack_detail_r2.out.txt)
python3 pack_detail_r2.py --control      # 위반 25건이어야 한다 (pack_detail_r2.control.out.txt)
python3 pack_detail_r2.py --emit && python3 pack_detail_r2.py --verify-emit
python3 pack_detail_r2_sheet.py
```

★ 이 라운드가 새로 못박은 자 3개: (가) **채움 조각의 짧은 변도 ≥ 1.5획(0.516 R)** — 규칙 1-C(ρ≥0.218)만으로는 0.44 R 띠가 통과하는데
그 띠는 카드에서 선으로 뭉갠다. (나) **둥근 모서리 표본은 모서리당 3점(n=2)** — 4점 이상이면 변이 카드 24pt 획(0.825pt) 아래로 내려간다.
(다) **접합은 「닿음」이 아니라 「가로지름」으로 설계한다** — 곡선 표본점이 윤곽 위에 정확히 놓이지 않아 0.001 R 틈이 금지대로 잡힌다.
