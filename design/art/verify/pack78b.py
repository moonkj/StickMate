# -*- coding: utf-8 -*-
"""§3 — 색상각 후보 전수 조사(0~359°). 규칙은 derive_packs.py와 같은 것 하나뿐이다.

각 색상각마다 주색(채도 최대)·보조색(채도 최소)을 유도한 뒤 게이트 6개를 전부 건다.
    (1) 자립 대역   (2) WornColor 항등   (3) 배경 4종 >= 3.0
    (4) 카탈로그 26색과 ΔE >= 8.0        (5) 기존 팩 12색과 ΔE >= 7.8
    (6) 등급 램프 4색 · 예약색 2건과 ΔE >= 7.8
"""
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
    sys.exit("파서 교정 실패 — 폐기")
print()

existing = []
for name, hue, p, s in packs:
    existing.append((f"{name} 주", p))
    existing.append((f"{name} 보", s))
hues6 = sorted(h for _, h, _, _ in packs)

rows = {}
for hd in range(0, 360):
    pr = P.pick(float(hd), True)
    se = P.pick(float(hd), False)
    ok, fails, met = P.full_gate("후보", float(hd), pr, se, existing, catalog, ramp, reserved, floors)
    rows[hd] = (ok, fails, met, pr, se)

feasible = [h for h in rows if rows[h][0]]
print("=" * 96)
print("§3-1. 게이트를 통과하는 색상각 (0~359 정수 전수)")
print("=" * 96)
print(f"  통과 {len(feasible)}개 / 360")
# 구간으로 압축
runs = []
for h in sorted(feasible):
    if runs and h == runs[-1][1] + 1:
        runs[-1][1] = h
    else:
        runs.append([h, h])
print("  통과 구간:", ", ".join(f"{a}–{b}" if a != b else f"{a}" for a, b in runs))
blocked = [h for h in range(360) if not rows[h][0]]
runs2 = []
for h in blocked:
    if runs2 and h == runs2[-1][1] + 1:
        runs2[-1][1] = h
    else:
        runs2.append([h, h])
print("  막힌 구간:", ", ".join(f"{a}–{b}" if a != b else f"{a}" for a, b in runs2))
print()
print("  막힌 이유 요약(구간별 첫 각도의 사유):")
for a, b in runs2:
    print(f"    {a:3d}–{b:3d}°: {rows[a][1][0] if rows[a][1] else '?'}")

# 최적 쌍 — 최소 색상각 간격 최대화
print()
print("=" * 96)
print("§3-2. 새 두 각도 배치 — 최소 색상각 간격을 최대화한다")
print("=" * 96)
best = []
for i, h1 in enumerate(sorted(feasible)):
    for h2 in sorted(feasible):
        if h2 <= h1:
            continue
        g = P.min_gap(hues6 + [float(h1), float(h2)])
        best.append((g, h1, h2))
best.sort(reverse=True)
print(f"  이론 상한: 8개를 360°에 고르게 = 45.0° / 고정 6각이 만든 상한 = min(72,92)/2 = 36.0°")
print(f"  최적 {best[0][0]:.1f}° — 상위 12쌍:")
seen = set()
shown = 0
for g, h1, h2 in best:
    if shown >= 12:
        break
    print(f"    {h1:3d}° + {h2:3d}°  -> 최소 간격 {g:5.1f}°")
    shown += 1

print()
print("  ★ 두 개를 각각 어느 빈칸에 넣는가(현행 6각의 두 최대 공백):")
print("     8→80 = 72.0° (중점 44°)  ·  80→172 = 92.0° (중점 126°)")
for probe in (44, 126):
    ok, fails, met, pr, se = rows[probe]
    print(f"     {probe:3d}° 게이트 {'통과' if ok else '불통과'}"
          + ("" if ok else f" — {fails[0]}"))
