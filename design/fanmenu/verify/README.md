# design/fanmenu/verify — 부채꼴 메뉴 심볼 검산 하니스 (R26)

`docs/DESIGN_FAN_MENU_ICONS.md`의 모든 숫자를 재현한다. 프로덕션 `.cs`는 읽기만 한다.

## 무엇이 무엇인가

| 파일 | 하는 일 |
|---|---|
| `fanglyph.py` | **거울** — `GearRadialMenuWidget.Build*Symbol()`을 파이썬으로 옮겨 적은 것 + 래스터 측정기 |
| `r26_measure.py` | §0 교정(알려진 값 6건) + §2 현행 5종 실측 + 램프 표 |
| `r26_correct.py` | §1-2 `ART_FAN_MENU_LANGUAGE` §5 표의 **캡 규약 오류** 재현·정정 |
| `r26_new.py` | **제안 좌표 정본** — 이 파일이 사양이고, `r26_coords.txt`는 여기서 구워진다 |
| `r26_gate.py` | 게이트 FG-1 ~ FG-8 · 현행 vs 제안 나란히 |
| `r26_dump.py` | 좌표 전문 + 두 크기/DPI 표 → `r26_coords.txt` |
| `r26_sheet.py` | 원판 + 심볼 렌더러(8배 시트 + 실크기 44/66/88px) |
| `r26_fan.py` | 실제 부채꼴 배치(궤도 111/168 · 간격 30°)에 얹어 1×/2× · 바탕 3종 |
| `r26_slot4.py` | ④ 행동 명령 칸 후보 8안(지휘봉 5 + 확성기 3) |
| `r26_color.py` | §4-2 대비 근거 — 「왜 잉크 윤곽·흰 광택이 이 크기로 안 오는가」 |

## 돌리기

```bash
cd design/fanmenu/verify
python3 r26_measure.py > r26_measure.out.txt
python3 r26_correct.py > r26_correct.out.txt
python3 r26_gate.py    > r26_gate.out.txt
python3 r26_dump.py    > r26_coords.txt
python3 r26_fan.py
```

의존: `numpy` `scipy` `pillow`.

## 규약 (거울을 고칠 때 반드시 지킬 것)

- **길이 = 보이는 총 길이**다. `UiChrome.AddStroke(L, t)`의 둥근 캡 **중심**은 `±(L/2 − t/2)`,
  코어 끝점이 `±L/2`. 이걸 틀리면 `ART_FAN_MENU_LANGUAGE` §5와 같은 오류가 난다(§1-2).
- **꺾은선은 점 목록**이다(`AddPolyline` 규약). 선분마다 `AddStroke`로 그릴 때는
  길이에 `thickness`를 더하고 중심을 중점에 둔다 — 안 그러면 이음매에 `t/2` 틈이 생긴다.
- **간극 판정은 「덩어리」 단위**다. 겹치는 조각끼리 묶은 뒤(`r26_gate.components`),
  **다른 덩어리 사이**에만 `≥ 1.5W` 를 요구한다. 한 물건 안의 용접은 조형 수단이다.
- **골 = 간극 − `UiChrome.EdgeFeather`(0.5pt)**. 이 뺄셈을 빼먹으면 「1.5pt 여유가 있다」가
  화면에서는 「붙어 있다」가 된다.
  > ★ **2026-09-06 정정** — 여기 있던 `− 2 × EdgeFeather`(= 간극 − 1.0pt)는 **틀렸다.**
  > 실제 굽기가 `alpha = clamp01((core − d)/feather + 0.5)`(`UiChrome.cs:948` 캡슐 · `:892` 원)이라
  > **알파 0.5 등고선이 코어 가장자리**이고, 램프는 그 가장자리를 **가운데 두고 ±0.25pt**로 걸친다.
  > 즉 양쪽이 0.25pt씩 먹어 **한 변당 0.25pt · 합계 0.5pt**다.
  > 배율까지 넣은 일반형: **`(간극 − 0.5pt) · k · S ≥ 1.0px`**(`k` 배치 배율, `S` DPI 배율).
  > 근거·유도: `design/art/r27_shrink_fg3.out.txt` §0 교정6 · `docs/DESIGN_FAN_MENU_ICONS.md` §1-3.
- 크기는 **Ø44(평상) · Ø48(호버) · Ø36(축소 폴백)** 셋이다. **32pt는 이 앱에 없는 크기다.**
  > ★ **2026-09-06 정정** — 여기 있던 «Ø36은 히트/배치 계산에만 쓰이고 화면에 그려지지 않는다»는
  > R26 시점에는 사실이었으나 **뒤집혔다.** 축소 폴백 배선 결함이 수정되면서
  > `GearRadialMenuWidget.ApplyLayoutDiameterToViews()`가 `ButtonView.Group.localScale =`
  > `_diameterPoints / ButtonDiameterPoints`(= 36/44 = **0.818182**)의 **균일 배율**을 걸어
  > **Ø36 버튼이 실제로 그려진다.** 새 거울은 두 배율을 **모두** 봐야 한다.
  > 그 배율에서의 FG-3 재판정 결과는 `r26_coords.txt` 「R27 재검증」 절에 있다(결함 0건).
