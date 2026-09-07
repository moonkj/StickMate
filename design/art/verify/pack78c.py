# -*- coding: utf-8 -*-
"""§4 — 색상각 후보 정밀 측정. 「간격 몇 도」가 아니라 **ΔE 몇**인지를 본다."""
import sys
import colorlab as C
import pack78 as P

C.calibrate()
print()
packs = P.parse_frozen_packs()
ramp = P.parse_rarity_ramp()
reserved = P.parse_reserved()
catalog = P.parse_catalog_colors()
floors = P.parse_contrast_floors()
if not P.selftest_parsers(packs, ramp, reserved, P.parse_worn_box(), floors, catalog):
    sys.exit("파서 교정 실패")
print()

existing = []
for name, hue, p, s in packs:
    existing.append((f"{name}주", p))
    existing.append((f"{name}보", s))
hues6 = sorted(h for _, h, _, _ in packs)

# ---------------------------------------------------------------- 기준선
print("=" * 100)
print("§4-0. 기준선 — 현행 6팩끼리는 얼마나 떨어져 있나 (같은 팩 안 주↔보는 제외)")
print("=" * 100)
worst = (999, None)
pairs = 0
percross = {}
for i in range(len(packs)):
    for j in range(i + 1, len(packs)):
        ni, hi_, pi, si = packs[i]
        nj, hj, pj, sj = packs[j]
        d = min(C.dE(a, b) for a in (pi, si) for b in (pj, sj))
        gap = abs(((hi_ - hj + 180) % 360) - 180)
        percross[(ni, nj)] = (gap, d)
        pairs += 1
        if d < worst[0]:
            worst = (d, f"{ni} ↔ {nj}")
print(f"  {'팩 쌍':40s} {'색상각차':>7s} {'교차 최소ΔE':>10s}")
for (a, b), (g, d) in sorted(percross.items(), key=lambda kv: kv[1][1]):
    print(f"  {a + ' ↔ ' + b:40s} {g:7.1f} {d:10.2f}")
print(f"\n  ★ 팩간 교차 최소 ΔE = {worst[0]:.2f} ({worst[1]}) · 쌍 {pairs}개")
print(f"  ★ 색상각 44° 짝(컬러 잉크 268 ↔ 오피스 222? 아니다 — 아래 표에서 44°는 컬러잉크↔네온낙서)")

# 색상각차 -> ΔE 회귀 관측
print()
print("  색상각차와 교차 최소 ΔE의 관계(현행 15쌍, 오름차순):")
for (a, b), (g, d) in sorted(percross.items(), key=lambda kv: kv[1][0]):
    print(f"    {g:5.1f}° -> ΔE {d:6.2f}   {a} ↔ {b}")

# ---------------------------------------------------------------- 후보표
print()
print("=" * 100)
print("§4-1. 후보 색상각 정밀표 — 통과 구간의 대표값")
print("=" * 100)
CAND = [18, 20, 29, 33, 36, 56, 58, 63, 68,
        99, 110, 112, 126, 130, 141, 150, 157,
        186, 195, 202, 234, 240, 245, 249, 289, 295, 300, 325, 340, 354]
print(f"  {'H':>4s} {'주색':>9s} {'보조색':>9s} {'배경최악':>7s} {'카탈로그ΔE':>10s} "
      f"{'팩ΔE':>8s} {'등급ΔE':>7s} {'8각최소간격':>10s}")
rowdata = {}
for hd in CAND:
    pr = P.pick(float(hd), True)
    se = P.pick(float(hd), False)
    if pr is None or se is None:
        print(f"  {hd:4d} 해 없음")
        continue
    ok, fails, met = P.full_gate("후보", float(hd), pr, se, existing, catalog, ramp, reserved, floors)
    wbg = min(met["주색_배경최악"], met["보조색_배경최악"])
    dcat = min(met["주색_카탈로그ΔE"][0], met["보조색_카탈로그ΔE"][0])
    dpack = min(met["주색_팩ΔE"][0], met["보조색_팩ΔE"][0])
    dram = min(met["주색_등급ΔE"], met["보조색_등급ΔE"])
    rowdata[hd] = (pr, se, wbg, dcat, dpack, dram)
    print(f"  {hd:4d} {C.rgb2hex(pr):>9s} {C.rgb2hex(se):>9s} {wbg:7.2f} {dcat:10.2f} "
          f"{dpack:8.2f} {dram:7.2f} {'':>10s}  {'통과' if ok else 'X ' + fails[0]}")

# ---------------------------------------------------------------- 쌍 평가
print()
print("=" * 100)
print("§4-2. 쌍 평가 — 두 신규 팩을 함께 놓았을 때")
print("=" * 100)
PAIRS = [
    ("A 기하최적 (둘 다 초록 공백)", 112, 141),
    ("B 광부=번트오커 / 대마법사=에메랄드", 33, 126),
    ("C 광부=머스터드 / 대마법사=에메랄드", 58, 126),
    ("D 광부=번트오커 / 대마법사=관습보라", 33, 245),
    ("E 광부=머스터드 / 대마법사=관습보라", 58, 245),
    ("F 광부=번트오커 / 대마법사=제비꽃(289)", 33, 289),
    ("G 광부=에메랄드 / 대마법사=관습보라", 126, 245),
    ("H 광부=머스터드(63) / 대마법사=에메랄드(126)", 63, 126),
]
print(f"  {'안':42s} {'8각최소간격':>10s} {'신규↔기존 최소ΔE':>16s} {'신규끼리 최소ΔE':>15s} {'게이트':>6s}")
for label, h1, h2 in PAIRS:
    p1, s1 = P.pick(float(h1), True), P.pick(float(h1), False)
    p2, s2 = P.pick(float(h2), True), P.pick(float(h2), False)
    g = P.min_gap(hues6 + [float(h1), float(h2)])
    dcross = min(C.dE(a, b) for a in (p1, s1) for _, b in existing)
    dcross = min(dcross, min(C.dE(a, b) for a in (p2, s2) for _, b in existing))
    dnew = min(C.dE(a, b) for a in (p1, s1) for b in (p2, s2))
    ok1 = P.full_gate("1", float(h1), p1, s1, existing, catalog, ramp, reserved, floors)[0]
    ok2 = P.full_gate("2", float(h2), p2, s2, existing, catalog, ramp, reserved, floors)[0]
    print(f"  {label:42s} {g:10.1f} {dcross:16.2f} {dnew:15.2f} {('OK' if ok1 and ok2 else 'X'):>6s}")
