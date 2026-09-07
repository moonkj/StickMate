# -*- coding: utf-8 -*-
"""§9 — 확정안 전량 검산 + 팔레트 수용량(팩 몇 개까지 버티는가)."""
import sys
import colorlab as C
import cvd
import band
import pack78 as P

C.calibrate(); cvd.calibrate(); print()
packs = P.parse_frozen_packs(); ramp = P.parse_rarity_ramp()
reserved = P.parse_reserved(); catalog = P.parse_catalog_colors(); floors = P.parse_contrast_floors()
if not P.selftest_parsers(packs, ramp, reserved, P.parse_worn_box(), floors, catalog):
    sys.exit("파서 교정 실패")
print()
LO, HI, _ = band.limits()
MINER_H, MAGE_H = 33, 138
MINER = (P.pick(33.0, True), P.pick(33.0, False))
MAGE = (P.pick(138.0, True), P.pick(138.0, False))
ALL = [(n, p, s) for n, h, p, s in packs] + [("광부", *MINER), ("대마법사", *MAGE)]
HUES8 = sorted([h for _, h, _, _ in packs] + [33.0, 138.0])

SHADE = 0.28  # Core/AccessoryShapeContract.AccessoryTone.ShadeFactor
def shaded(c):
    return tuple(int(round(v * SHADE)) for v in c)

CARD_MUTED = C.hex2rgb("#15181E")   # UiChrome.CardSurfaceMuted (카드 썸네일 바탕)
CARD = C.hex2rgb("#1B1F26")         # UiChrome.CardSurface (카드 면)
CARD_INK = C.hex2rgb("#E8E2D6")     # UiChrome.CardIconInk
BLACK_HEAD = C.hex2rgb("#111111")
WHITE_HEAD = C.hex2rgb("#FFFFFF")

print("=" * 100)
print("§9-1. 확정안 값")
print("=" * 100)
print(f"  자립 대역 L ∈ [{LO:.4f}, {HI:.4f}]  ·  상자 S≥0.42 · V 0.55~0.80  ·  WornColor 항등 요구")
for nm, hd, (p, s) in (("광부", MINER_H, MINER), ("대마법사", MAGE_H, MAGE)):
    for role, c in (("주색 M", p), ("보조색 M2", s)):
        h, sa, v = C.rgb_to_hsv(c)
        print(f"  {nm:6s} {role:9s} {C.rgb2hex(c)}  실측H {C.hue_deg(c):7.2f}°  L {C.L(c):.4f}  "
              f"S {sa:.3f}  V {v:.3f}  WornColor {'항등' if C.worn(c)==c else '비항등 '+C.rgb2hex(C.worn(c))}")
    print(f"  {nm:6s} {'그늘 SH':9s} {C.rgb2hex(shaded(p))}  (= M × ShadeFactor 0.28, 파생 · 새 hex 아님)")
    print(f"  {nm:6s} 주↔보 ΔE {C.dE(p, s):.2f}")

print()
print("=" * 100)
print("§9-2. 색상각 8각 · 간격 검산")
print("=" * 100)
gs = [(HUES8[i], HUES8[(i+1) % 8], (HUES8[(i+1) % 8] - HUES8[i]) % 360) for i in range(8)]
tot = 0
for a, b, g in gs:
    print(f"  {a:5.0f}° → {b:5.0f}° = {g:5.1f}°")
    tot += g
print(f"  검산 합계 {tot:.1f}° (기대 360.0)  {'OK' if abs(tot-360)<1e-9 else 'FAIL'}")
print(f"  최소 간격 {min(g for _,_,g in gs):.1f}°  최대 {max(g for _,_,g in gs):.1f}°")

print()
print("=" * 100)
print("§9-3. 게이트 5종 — 신규 4색 전량")
print("=" * 100)
newc = [("광부 주", MINER[0]), ("광부 보", MINER[1]), ("대마법사 주", MAGE[0]), ("대마법사 보", MAGE[1])]
BD = band.BACKDROPS
print(f"  {'색':12s} {'대역':>6s} {'항등':>5s} " + " ".join(f"{n[:6]:>7s}" for n, _ in BD)
      + f" {'검머리':>7s} {'흰머리':>7s} {'썸네일':>7s} {'카드면':>7s} {'카드잉크':>8s}")
for nm, c in newc:
    row = [f"{C.CR(c, b):7.2f}" for _, b in BD]
    print(f"  {nm:12s} {'OK' if LO<=C.L(c)<=HI else 'X':>6s} {'OK' if C.worn(c)==c else 'X':>5s} "
          + " ".join(row)
          + f" {C.CR(c, BLACK_HEAD):7.2f} {C.CR(c, WHITE_HEAD):7.2f} "
            f"{C.CR(c, CARD_MUTED):7.2f} {C.CR(c, CARD):7.2f} {C.CR(c, CARD_INK):8.2f}")
print(f"  하한: 비텍스트 {floors[1]:.1f} (카드 잉크 윤곽은 보조 대비라 하한 없음 — EQUIPMENT_PALETTE §3)")

print()
print("=" * 100)
print("§9-4. ΔE 전량 — 카탈로그 26 · 등급 램프 4 · 예약 2 · 기존 팩 12 · 신규끼리")
print("=" * 100)
for nm, c in newc:
    dc, wc = min((C.dE(c, x), n) for x, n in catalog)
    dr = min((C.dE(c, r), i) for i, r in enumerate(ramp))
    dt = C.dE(c, reserved["TextTertiary"]); db = C.dE(c, reserved["CardBorderWorn"])
    dp, wp = min((C.dE(c, x), n) for n, h, pp, ss in packs for n, x in ((f"{n}주", pp), (f"{n}보", ss)))
    print(f"  {nm:12s} 카탈로그 {dc:6.2f}({wc})  등급 {dr[0]:6.2f}(#{dr[1]})  "
          f"TextTertiary {dt:6.2f}  CardBorderWorn {db:6.2f}  기존팩 {dp:6.2f}({wp})")
print(f"  하한: 카탈로그 8.0 · 나머지 7.8")
print(f"  신규끼리 최소 ΔE {min(C.dE(a,b) for a in MINER for b in MAGE):.2f}")

print()
print("=" * 100)
print("§9-5. '한 대역' 유지 — 16색 휘도 폭 (팩끼리 밝기로 구분되면 안 된다)")
print("=" * 100)
lums = [(C.L(c), f"{n} {r}") for n, p, s in ALL for r, c in (("주", p), ("보", s))]
lums.sort()
spread = (lums[-1][0] + 0.05) / (lums[0][0] + 0.05)
print(f"  최저 {lums[0][1]} L={lums[0][0]:.4f}  ·  최고 {lums[-1][1]} L={lums[-1][0]:.4f}")
print(f"  폭 대비 {spread:.4f}:1  (하한 {floors[1]:.1f} 미만이어야 '밝기로는 구분 안 됨' — "
      f"{'OK' if spread < floors[1] else 'FAIL'})")

print()
print("=" * 100)
print("§9-6. 8팩 전체 쌍 (28쌍) — 교차 최소 ΔE")
print("=" * 100)
rows = []
for i in range(len(ALL)):
    for j in range(i+1, len(ALL)):
        d = min(C.dE(a, b) for a in ALL[i][1:] for b in ALL[j][1:])
        rows.append((d, ALL[i][0], ALL[j][0]))
rows.sort()
for d, a, b in rows:
    mark = " ← 가족 하한" if d == rows[0][0] else ("  (신규 관여)" if "광부" in (a,b) or "대마법사" in (a,b) else "")
    print(f"  ΔE {d:6.2f}  {a} ↔ {b}{mark}")
print(f"  ★ 8팩 가족 하한 {rows[0][0]:.2f} · 현행 6팩 하한 17.57 · 차이 {rows[0][0]-17.57:+.2f}")
print(f"  ★ 식별 하한 48.6을 넘는 쌍 {sum(1 for d,_,_ in rows if d>=48.6)}/28 — "
      f"색만으로 팩 이름을 대는 것은 8팩에서 구조적으로 불가(모티프가 주 채널)")
