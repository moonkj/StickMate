"""R26 §0 교정 + §1 현행 실측."""
import math
import numpy as np
from fanglyph import *

print("=" * 78)
print("§0 교정 — 거울이 알려진 값을 재현하는가")
print("=" * 78)

cal = []
# 1) 카드 아이콘 획 (프로덕션 상수에서)
cal.append(("카드 아이콘 획 2.2/64 × 58", "1.994pt (EQUIPMENT_HANDOFF_PORT_SPEC §13-2-1)",
            f"{CARD_STROKE:.3f}pt"))
# 2) design-art R14 §5 가 잰 스톱워치 분침 여유 1.50pt
m = measure(cur_stopwatch())
d = dict(((a, b), c) for a, b, c in m["clears"])
cal.append(("스톱워치 분침↔링 여유", "1.50pt (ART_FAN_MENU_LANGUAGE §5)",
            f"{d[('Ring','MinuteHand')]:.3f}pt"))
# 3) 스틱맨 다리 끝 사이 2.06pt
m2 = measure(cur_stickman())
d2 = dict(((a, b), c) for a, b, c in m2["clears"])
cal.append(("스틱맨 다리끝 사이", "2.06pt (같은 표)", f"{d2[('IconLegL','IconLegR')]:.3f}pt"))
# 4) 확성기 입↔소리선 0.81pt
m4 = measure(cur_megaphone())
d4 = dict(((a, b), c) for a, b, c in m4["clears"])
cal.append(("확성기 입↔소리선", "0.81pt (같은 표, 전체 최악)",
            f"{min(d4[('HornMouth','WaveUpper')], d4[('HornMouth','WaveLower')]):.3f}pt"))
# 5) 체크리스트 빈 박스 구멍 2.50pt
cal.append(("체크리스트 박스 구멍(4.5 − 2×1.0)", "2.50pt (같은 표)", f"{4.5 - 2*1.0:.3f}pt"))
# 6) 스틱맨 머리 구멍 (Ø7, 획 1.8)
cal.append(("스틱맨 머리 구멍(Ø7 − 2×1.8)", "3.40pt", f"{7.0 - 2*1.8:.3f}pt"))

for k, expect, got in cal:
    print(f"  {k:34s} 기대 {expect:46s} 거울 {got}")

print()
print("=" * 78)
print("§1 현행 5종 실측")
print("=" * 78)
print(f"  자: 상자 {SYMBOL_BOX:.0f}pt · W = {SYMBOL_STROKE:.1f}pt · 램프 {EDGE_FEATHER}pt/변")
print(f"      상자를 W 로 재면 {SYMBOL_BOX/SYMBOL_STROKE:.1f} W")
print(f"      비교) 장비 카드 {CARD_BOX:.0f}pt · 획 {CARD_STROKE:.3f}pt = "
      f"{CARD_BOX/CARD_STROKE:.1f} W · 상자의 {100*CARD_STROKE_FRAC:.4f}%")
print(f"      비교) 장비 슬롯행 {SLOT_BOX:.0f}pt · 획 {SLOT_STROKE:.3f}pt "
      f"(부채꼴과 같은 24pt 상자인데 획은 {SYMBOL_STROKE/SLOT_STROKE:.2f}배 얇다)")
print()

rows = []
for name, fn in CURRENT.items():
    mm = measure(fn())
    worst = mm["clears"][0] if mm["clears"] else ("-", "-", float("nan"))
    rows.append((name, mm, worst))
    print(f"── {name}")
    print(f"   조각 {mm['n']}개 · 획 폭 {mm['thicknesses']} "
          f"({len(mm['thicknesses'])}종) · 잉크 면적 {100*mm['ink_frac']:.1f}%")
    bx0, bx1, by0, by1 = mm["bbox"]
    print(f"   잉크 상자 x[{bx0:+.2f},{bx1:+.2f}] y[{by0:+.2f},{by1:+.2f}] "
          f"→ 24pt 상자 넘침 {mm['overflow_box']:+.2f}pt")
    if mm["hidden"]:
        print(f"   ★ 완전히 가려진 조각: {mm['hidden']}")
    print("   가장 좁은 3쌍 (코어↔코어 간극 / 그 사이 알파 0 골):")
    for a, b, c in mm["clears"][:3]:
        v = valley(c)
        flag = "✘" if c < 1.5 * SYMBOL_STROKE else "✓"
        print(f"     {flag} {a:12s}↔{b:12s} {c:5.2f}pt = {c/SYMBOL_STROKE:4.2f}W  "
              f"골 {v:+5.2f}pt  (1× {v*1:+.2f}px / 2× {v*2:+.2f}px)")
    print()

print("=" * 78)
print("§2 램프가 먹는 폭 — 획이 얇을수록 실체가 아니라 그라데이션이 된다")
print("=" * 78)
print("   ★ 2026-09-06 정정 — 램프는 코어 <바깥>이 아니라 코어 가장자리를 <가운데 두고>")
print("     ±EdgeFeather/2 로 걸친다(alpha = clamp01((core − d)/feather + 0.5),")
print("     UiChrome.cs:948 캡슐 · :892 원). 옛 «그려짐 = 획+1.0 · 심 = 획»은 폐기한다.")
print(f"   그려지는 폭 = 획 + {EDGE_FEATHER}.  심(알파 1) = 획 − {EDGE_FEATHER}.")
for t in [4.0, 2.0, 1.994, 1.8, 1.6, 1.4, 0.825]:
    drawn, spine = t + EDGE_FEATHER, max(0.0, t - EDGE_FEATHER)
    print(f"   획 {t:5.3f}pt → 그려짐 {drawn:5.3f}pt · 심 {spine:5.3f}pt "
          f"· 심 비율 {100*spine/drawn:4.1f}% · 램프 {100*(drawn-spine)/drawn:4.1f}%")
print()
print(f"   ⇒ 두 획 사이 코어 간극 g 일 때 알파 0 인 골 = g − {EDGE_FEATHER}pt.")
print(f"     1× (Windows 100%) 에서 골이 1px 이상 남으려면 g ≥ {1.0 + EDGE_FEATHER}pt.")
print(f"     1.5W = {1.5*SYMBOL_STROKE:.1f}pt 규칙은 골 {1.5*SYMBOL_STROKE - EDGE_FEATHER:.1f}pt "
      f"= 1×에서 {1.5*SYMBOL_STROKE - EDGE_FEATHER:.1f}px 로, 이 하한을 여유 있게 넘는다.")
print(f"     ★ 축소 폴백 Ø36(k = 36/44 = {36/44:.4f})에서도 골 "
      f"{(1.5*SYMBOL_STROKE - EDGE_FEATHER)*36/44:.2f}px 로 하한 위다 — R27 재검증 결함 0건.")
