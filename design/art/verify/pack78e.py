# -*- coding: utf-8 -*-
"""§6 — 최종 후보 3안 정밀 비교 (정상 시각 + 색각이상 3형 + 회색조)."""
import sys
import colorlab as C
import cvd
import pack78 as P

C.calibrate()
cvd.calibrate()
print()
packs = P.parse_frozen_packs()
ramp = P.parse_rarity_ramp()
reserved = P.parse_reserved()
catalog = P.parse_catalog_colors()
floors = P.parse_contrast_floors()
if not P.selftest_parsers(packs, ramp, reserved, P.parse_worn_box(), floors, catalog):
    sys.exit("파서 교정 실패")
print()

existing = [(f"{n}주", p) for n, h, p, s in packs] + [(f"{n}보", s) for n, h, p, s in packs]
hues6 = sorted(h for _, h, _, _ in packs)

PLANS = {
    "안1  광부 33° 호박 / 대마법사 199° 사파이어": (33, 199),
    "안2  광부 33° 호박 / 대마법사 138° 에메랄드": (33, 138),
    "안3  광부 33° 호박 / 대마법사 240° 남보라(관습)": (33, 240),
    "안4  광부 33° 호박 / 대마법사 245° 보라(경고값)": (33, 245),
}

def pack_set(h1, h2):
    """8팩 전체 (이름, 주, 보) 목록."""
    out = [(n, p, s) for n, h, p, s in packs]
    out.append(("광부", P.pick(float(h1), True), P.pick(float(h1), False)))
    out.append(("대마법사", P.pick(float(h2), True), P.pick(float(h2), False)))
    return out


def cross_table(ps, transform=None):
    """팩 쌍 교차 최소 ΔE. transform 이 있으면 그 시각으로 변환한 뒤 잰다."""
    def T(c):
        return c if transform is None else transform(c)
    worst = (999, None)
    rows = []
    for i in range(len(ps)):
        for j in range(i + 1, len(ps)):
            ni, pi, si = ps[i]
            nj, pj, sj = ps[j]
            d = min(C.dE(T(a), T(b)) for a in (pi, si) for b in (pj, sj))
            rows.append((d, ni, nj))
            if d < worst[0]:
                worst = (d, f"{ni} ↔ {nj}")
    rows.sort()
    return worst, rows


# 기준선 (현행 6팩)
base6 = [(n, p, s) for n, h, p, s in packs]
print("=" * 100)
print("§6-0. 기준선 — 현행 6팩")
print("=" * 100)
w, _ = cross_table(base6)
print(f"  정상 시각 교차 최소 ΔE  {w[0]:6.2f}  ({w[1]})")
for k in cvd.TYPES:
    wc, _ = cross_table(base6, lambda c, k=k: cvd.sim(c, k))
    print(f"  {cvd.KOR[k]:12s} 교차 최소 ΔE  {wc[0]:6.2f}  ({wc[1]})")
lums = [C.L(c) for _, p, s in base6 for c in (p, s)]
print(f"  회색조(휘도) 폭 {max(lums)/min(lums):.2f}배 · 최대/최소 대비 "
      f"{(max(lums)+0.05)/(min(lums)+0.05):.2f}:1  ← 색이 사라지면 팩 구분도 사라진다(기존 사실)")

for label, (h1, h2) in PLANS.items():
    ps = pack_set(h1, h2)
    print()
    print("=" * 100)
    print(f"§6-{list(PLANS).index(label)+1}. {label}")
    print("=" * 100)
    p1, s1 = ps[-2][1], ps[-2][2]
    p2, s2 = ps[-1][1], ps[-1][2]
    print(f"  광부      주 {C.rgb2hex(p1)}  보 {C.rgb2hex(s1)}   "
          f"L {C.L(p1):.4f}/{C.L(s1):.4f}  실측H {C.hue_deg(p1):.2f}/{C.hue_deg(s1):.2f}")
    print(f"  대마법사  주 {C.rgb2hex(p2)}  보 {C.rgb2hex(s2)}   "
          f"L {C.L(p2):.4f}/{C.L(s2):.4f}  실측H {C.hue_deg(p2):.2f}/{C.hue_deg(s2):.2f}")
    g = P.min_gap(hues6 + [float(h1), float(h2)])
    w, rows = cross_table(ps)
    print(f"  8각 최소 색상각 간격 {g:.1f}°  ← 대리 지표(아래 ΔE와 비교하라)")
    print(f"  정상 시각 교차 최소 ΔE  {w[0]:6.2f}  ({w[1]})   기준선 17.57 대비 {w[0]-17.57:+.2f}")
    for k in cvd.TYPES:
        wc, _ = cross_table(ps, lambda c, k=k: cvd.sim(c, k))
        print(f"  {cvd.KOR[k]:12s} 교차 최소 ΔE  {wc[0]:6.2f}  ({wc[1]})")
    print("  가장 가까운 5쌍:")
    for d, a, b in rows[:5]:
        print(f"    ΔE {d:6.2f}  {a} ↔ {b}")
