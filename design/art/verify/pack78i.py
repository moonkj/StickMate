# -*- coding: utf-8 -*-
"""§10 — 팔레트 수용량. '출시 이후 계속 추가팩'이 사용자 확정이므로 N을 재 둔다.
    + §11 감사 처방의 임계값 근거(색상각 자로는 못 가르는 쌍의 실증)."""
import sys
import colorlab as C
import band
import pack78 as P

C.calibrate(); print()
packs = P.parse_frozen_packs(); ramp = P.parse_rarity_ramp()
reserved = P.parse_reserved(); catalog = P.parse_catalog_colors(); floors = P.parse_contrast_floors()
if not P.selftest_parsers(packs, ramp, reserved, P.parse_worn_box(), floors, catalog):
    sys.exit("파서 교정 실패")
print()

# 색상각별 유도색 + 카탈로그/등급/예약 게이트(팩끼리는 뺀다 — 그건 아래에서 집합으로 본다)
LO, HI, _ = band.limits()
BD = band.BACKDROPS
derived = {}
for hd in range(360):
    p, s = P.pick(float(hd), True), P.pick(float(hd), False)
    if p is None or s is None:
        continue
    okc = all(min(C.dE(c, x) for x, _ in catalog) >= 8.0 for c in (p, s))
    okr = all(min(C.dE(c, r) for r in ramp) >= 7.8 for c in (p, s))
    okv = all(min(C.dE(c, rc) for rc in reserved.values()) >= 7.8 for c in (p, s))
    okb = all(min(C.CR(c, b) for _, b in BD) >= floors[1] for c in (p, s))
    if okc and okr and okv and okb:
        derived[hd] = (p, s)

FIXED = [(8, None), (80, None), (172, None), (222, None), (268, None), (312, None)]
fixedcols = {}
for n, h, p, s in packs:
    fixedcols[int(round(h))] = (p, s)
fixedcols[33] = derived[33]
fixedcols[138] = derived[138]

def crossmin(h1, h2, cols):
    a = cols[h1]; b = cols[h2]
    return min(C.dE(x, y) for x in a for y in b)

def floor_of(hs, cols):
    return min(crossmin(hs[i], hs[j], cols)
               for i in range(len(hs)) for j in range(i+1, len(hs)))

cols = dict(fixedcols)
for hd, ps in derived.items():
    cols.setdefault(hd, ps)

print("=" * 100)
print("§10-1. 팩을 계속 늘리면 가족 하한(교차 최소 ΔE)은 어디까지 내려가는가")
print("=" * 100)
cur = [8, 33, 80, 138, 172, 222, 268, 312]
print(f"  6팩(출하 예정)         하한 {floor_of([8,80,172,222,268,312], cols):6.2f}")
print(f"  8팩(이 라운드 확정안)   하한 {floor_of(cur, cols):6.2f}")
avail = sorted(h for h in derived if h not in cur)
for k in range(9, 15):
    best = None
    for h in avail:
        f = floor_of(cur + [h], cols)
        if best is None or f > best[0]:
            best = (f, h)
    if best is None:
        break
    cur = sorted(cur + [best[1]])
    avail = [h for h in avail if h != best[1]]
    print(f"  {k:2d}팩 (탐욕 최선 {best[1]:3d}° 추가)  하한 {best[0]:6.2f}   각 {cur}")

print()
print("  ★ 읽는 법: 하한이 7.8 아래로 내려가는 순간 '두 팩이 같은 색으로 읽힌다'.")
print("     15.0 아래는 '나란히 놓으면 갈리지만 기억으로는 안 갈린다' 구간이다.")

print()
print("=" * 100)
print("§11. ★ 감사 처방의 근거 — 색상각 자로는 이 두 쌍을 가를 수 없다")
print("=" * 100)
base6 = [8, 80, 172, 222, 268, 312]
for label, h2 in (("199° 사파이어", 199), ("245° 보라", 245)):
    hs = sorted(base6 + [33, h2])
    gaps = [(hs[(i+1) % 8] - hs[i]) % 360 for i in range(8)]
    f = floor_of(hs, cols)
    print(f"  광부 33° + 대마법사 {label:14s} -> 최소 색상각 간격 {min(gaps):5.1f}°   가족 하한 ΔE {f:6.2f}")
print("  ⇒ **색상각 간격은 둘 다 23.0°로 같은데 ΔE는 1.9배 차이가 난다.**")
print("     따라서 '최소 색상각 간격 N도' 형태의 임계값은 이 두 경우를 **원리적으로** 못 가른다.")
print("     감사는 각이 아니라 **동결된 primaryColor/secondaryColor 자체**를 재야 한다.")
print()
print("  양성/음성 대조 쌍(감사 테스트가 그대로 쓸 수 있다):")
print(f"    통과해야 함: 대마법사 {C.rgb2hex(cols[199][0])}/{C.rgb2hex(cols[199][1])} (199°)")
print(f"    잡아야 함  : 대마법사 {C.rgb2hex(cols[245][0])}/{C.rgb2hex(cols[245][1])} (245°) — "
      f"컬러 잉크 #8563AB 와 ΔE {min(C.dE(c, C.hex2rgb('#8563AB')) for c in cols[245]):.2f}")
print(f"    확정안     : 광부 {C.rgb2hex(cols[33][0])}/{C.rgb2hex(cols[33][1])} · "
      f"대마법사 {C.rgb2hex(cols[138][0])}/{C.rgb2hex(cols[138][1])}")
