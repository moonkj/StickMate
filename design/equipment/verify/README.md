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
