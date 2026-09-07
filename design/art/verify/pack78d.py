# -*- coding: utf-8 -*-
"""§5 — 색상각 간격 대신 **ΔE 하한**을 목적함수로 놓고 두 각을 고른다.

왜 바꾸는가: §4-0 실측이 「색상각 간격 = 구분 정도」라는 대리 지표를 깬다.
  50° 떨어진 오피스↔사이버는 ΔE 51.90인데 44° 떨어진 네온↔컬러잉크는 17.57이다.
  (색상환은 지각적으로 균질하지 않다 — 노랑 근처는 도당 변화가 빠르고 파랑 근처는 느리다.)
따라서 목적함수 = **모든 팩 쌍(기존15 + 신규×기존12 + 신규끼리1)의 교차 최소 ΔE 최대화.**
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
    sys.exit("파서 교정 실패")
print()

existing = [(f"{n}주", p) for n, h, p, s in packs] + [(f"{n}보", s) for n, h, p, s in packs]
hues6 = sorted(h for _, h, _, _ in packs)

# 기존 6팩 교차 최소 ΔE (기준선)
base = min(min(C.dE(a, b) for a in (packs[i][2], packs[i][3]) for b in (packs[j][2], packs[j][3]))
           for i in range(6) for j in range(i + 1, 6))

derived = {}
for hd in range(360):
    pr = P.pick(float(hd), True)
    se = P.pick(float(hd), False)
    ok = False
    if pr is not None and se is not None:
        ok = P.full_gate("c", float(hd), pr, se, existing, catalog, ramp, reserved, floors)[0]
    derived[hd] = (ok, pr, se)

feas = [h for h in range(360) if derived[h][0]]

def cross_min(h1, h2):
    _, p1, s1 = derived[h1]
    _, p2, s2 = derived[h2]
    d = min(C.dE(a, b) for a in (p1, s1) for _, b in existing)
    d = min(d, min(C.dE(a, b) for a in (p2, s2) for _, b in existing))
    d = min(d, min(C.dE(a, b) for a in (p1, s1) for b in (p2, s2)))
    return d

print("=" * 100)
print("§5-1. 목적함수 = 8팩 전체 교차 최소 ΔE. 기준선(현행 6팩) = %.2f" % base)
print("=" * 100)
best = []
for i, h1 in enumerate(feas):
    for h2 in feas:
        if h2 <= h1:
            continue
        best.append((cross_min(h1, h2), h1, h2))
best.sort(reverse=True)
print(f"  후보 쌍 {len(best)}개(게이트 통과 각 {len(feas)}개에서). 상위 15:")
print(f"  {'H1':>4s} {'H2':>4s} {'8팩 교차최소ΔE':>13s} {'기준선 대비':>10s} {'8각 최소간격':>11s}")
for d, h1, h2 in best[:15]:
    g = P.min_gap(hues6 + [float(h1), float(h2)])
    print(f"  {h1:4d} {h2:4d} {d:13.2f} {d - base:+10.2f} {g:11.1f}")

print()
print("  ★ 도달 가능한 최대 = %.2f (기준선 %.2f 대비 %+.2f)" % (best[0][0], base, best[0][0] - base))

# 온도 제약을 걸었을 때 — 광부는 따뜻(0~70°), 대마법사는 차가움(120~300°)
print()
print("=" * 100)
print("§5-2. 테마 제약을 걸었을 때 — 광부=따뜻(0~70°) / 대마법사=차가움(120~300°)")
print("=" * 100)
warm = [h for h in feas if h <= 70]
cool = [h for h in feas if 120 <= h <= 300]
sub = sorted(((cross_min(a, b), a, b) for a in warm for b in cool), reverse=True)
print(f"  따뜻 후보 {len(warm)}개 · 차가움 후보 {len(cool)}개 · 조합 {len(sub)}개. 상위 15:")
print(f"  {'광부':>5s} {'대마':>5s} {'8팩 교차최소ΔE':>13s} {'기준선 대비':>10s} {'8각 최소간격':>11s}  주색")
for d, h1, h2 in sub[:15]:
    g = P.min_gap(hues6 + [float(h1), float(h2)])
    print(f"  {h1:5d} {h2:5d} {d:13.2f} {d - base:+10.2f} {g:11.1f}  "
          f"{C.rgb2hex(derived[h1][1])} / {C.rgb2hex(derived[h2][1])}")

# 관습 보라를 고집했을 때의 대가
print()
print("=" * 100)
print("§5-3. 「대마법사는 보라여야 한다」를 고집했을 때의 대가 (보라 = 250~290° 관습 대역)")
print("=" * 100)
purple = [h for h in range(250, 291)]
pf = [h for h in purple if derived[h][0]]
print(f"  250~290° 중 게이트 통과 = {pf if pf else '없음'}")
allpurple = [h for h in range(234, 300) if derived[h][0]]
print(f"  넓혀서 234~299° 중 통과 = {allpurple}")
best_p = sorted(((cross_min(a, b), a, b) for a in warm for b in allpurple), reverse=True)
if best_p:
    d, h1, h2 = best_p[0]
    print(f"  보라 대역 최선: 광부 {h1}° + 대마법사 {h2}° -> 8팩 교차 최소 ΔE {d:.2f} "
          f"(기준선 {base:.2f} 대비 {d-base:+.2f}, 변별 하한 7.8 대비 +{d-7.8:.2f})")
    print(f"     주색 {C.rgb2hex(derived[h1][1])} / {C.rgb2hex(derived[h2][1])}")
    # 어느 쌍이 최소를 만드는가
    _, p2, s2 = derived[h2]
    worst = min(((C.dE(a, b), nm, C.rgb2hex(a)) for a in (p2, s2) for nm, b in existing))
    print(f"     그 최소를 만드는 쌍: 대마법사 {worst[2]} ↔ {worst[1]} = ΔE {worst[0]:.2f}")
