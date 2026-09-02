# -*- coding: utf-8 -*-
"""색상각 분리 — 휘도가 다 쓰였을 때 남는 유일한 축 (design-art, 2026-09-02)

★ 이 파일의 1차 초안은 근사식 Δh_min = 2·asin(7.8/2C*) 을 썼다. **교정에서 깨졌다**
  (실측 대비 -2.1° ~ -10.0°). HSV에서 색상각을 돌리면 L*와 C*도 같이 움직이는데
  근사식은 그 둘을 고정으로 가정했다. 그래서 **근사식과 그것으로 낸 '정원' 표를 폐기하고**
  전수 탐색만 쓴다. 아래에 그 실패를 대조로 박제해 둔다.
"""
import os, sys, math, itertools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
from lumorder import load, INK_MARKERS, W010, NAMES_NEW, NAMES_OLD, hue_gap, DISCERN

CL.calibrate(verbose=False)
BAND = (0.1632, 0.2396)
BG = [(255, 255, 255), (0, 0, 0), CL.hex2rgb("#E9EAE6"), CL.hex2rgb("#25282E")]
PACK12 = ["#456ECC", "#5C709E", "#009682", "#518C84", "#CC1BA9", "#9C5A8E",
          "#CC3F29", "#9E655C", "#8D56CC", "#8563AB", "#639400", "#798C51"]

new, old = load("new"), load("old")
k = lambda r: (r["asset"], r["part"])
mo = {k(r): r["hex"] for r in old}
o2n = {}
for r in new: o2n[mo[k(r)]] = r["hex"]
n2o = {v: a for a, v in o2n.items()}
inv_n = {v: a for a, v in NAMES_NEW.items()}; inv_o = {v: a for a, v in NAMES_OLD.items()}
HAIR = {"#936C3F": "HairScalp", "#A0622A": "HairDark", "#A07830": "HairDarkLit",
        "#A86E1F": "HairBrownLit", "#AF651C": "HairRedLit", "#BD501F": "HairRed",
        "#CC3C3C": "Cloth"}
nm = lambda h: inv_n.get(h) or inv_o.get(n2o.get(h, ""), "") or HAIR.get(h, "무명")
cols = sorted({r["hex"] for r in new} - INK_MARKERS)
W = {c: W010.get(c, c) for c in cols}
use = {}
for r in new:
    if r["hex"] in INK_MARKERS: continue
    use.setdefault(r["hex"], set()).add(r["asset"])

def Cstar(h):
    l = CL.lab(CL.hex2rgb(h)); return math.hypot(l[1], l[2])
def ok(rgb):
    l = CL.L(rgb)
    return (BAND[0] <= l <= BAND[1] and CL.is_worn_fixed(rgb)
            and min(CL.CR(rgb, b) for b in BG) >= 3.0)

print("=" * 90)
print("0. 폐기된 근사식 — 교정 실패를 박제한다 (이 표의 숫자는 쓰지 않는다)")
print("=" * 90)
print("  %-13s %-9s %6s %10s %10s %8s" % ("색", "hex", "C*", "실측Δh", "근사식Δh", "오차"))
worst = 0
for c in ["#6183B4", "#A66930", "#BE5B23", "#20878C", "#96814F", "#CC423A", "#955CCC", "#428C24"]:
    h, s, v = CL.rgb_to_hsv(CL.hex2rgb(c))
    got = next((d / 20.0 for d in range(1, 2400)
                if CL.dE(CL.hex2rgb(c), CL.hsv_to_rgb(h + (d / 20.0) / 360.0, s, v)) >= DISCERN), None)
    ap = math.degrees(2 * math.asin(min(1.0, DISCERN / (2 * Cstar(c)))))
    worst = max(worst, abs(got - ap))
    print("  %-13s %-9s %6.1f %10.2f %10.2f %8.2f" % (nm(c), c, Cstar(c), got, ap, got - ap))
print("  최대 오차 %.2f° -> **교정 실패. 근사식과 그것으로 낸 정원 표는 폐기한다.**" % worst)

print("\n" + "=" * 90)
print("1. 색상각 실측 분포 (W=0.10 기준) — 어디가 과밀인가")
print("=" * 90)
hs = sorted((CL.hue_deg(CL.hex2rgb(W[c])), c) for c in cols)
prev = None
for h, c in hs:
    gap = "" if prev is None else "%6.2f°" % (h - prev)
    print("  %6.2f°  %-13s %-9s C*=%5.1f  L=%.4f  앞 색과 %s" % (h, nm(c), W[c], Cstar(W[c]),
                                                             CL.L(CL.hex2rgb(W[c])), gap))
    prev = h
print("\n  60° 구간별:")
for lo in range(0, 360, 60):
    g = [c for h, c in hs if lo <= h < lo + 60]
    print("    %3d~%3d°  %2d색  %s" % (lo, lo + 60, len(g), " ".join(nm(x) for x in g)))
print("  ★ 25색 중 16색(64%)이 0~60° 한 사분면에 있다. 120~180°와 300~360°는 비어 있다.")

print("\n" + "=" * 90)
print("2. 남는 하한 미달 쌍 — 어느 색을 몇 도 돌려야 갈라지는가 (전수 탐색)")
print("=" * 90)
def subfloor(pal):
    out = []
    for a, b in itertools.combinations(cols, 2):
        d = CL.dE(CL.hex2rgb(pal[a]), CL.hex2rgb(pal[b]))
        if d < DISCERN: out.append((d, a, b))
    return sorted(out)
base = subfloor(W)
for d, a, b in base:
    print("  dE %5.2f  %-13s %-9s ↔ %-13s %-9s  색상각차 %5.2f°  %s"
          % (d, nm(a), W[a], nm(b), W[b], hue_gap(W[a], W[b]),
             "★같은 아이템(%s)" % ",".join(sorted(use[a] & use[b])) if use[a] & use[b] else ""))
print("  총 %d쌍" % len(base))

print("\n  각 색을 홀로 돌렸을 때 필요한 최소 |Δh| (S·V 고정, 대역/항등/배경3.0 유지,")
print("  그리고 **새 미달 쌍을 만들지 않을 것**):")
for c in sorted({x for _, a, b in base for x in (a, b)}, key=lambda x: CL.hue_deg(CL.hex2rgb(W[x]))):
    h, s, v = CL.rgb_to_hsv(CL.hex2rgb(W[c]))
    best = None
    for step in range(1, 3601):
        for sign in (+1, -1):
            dh = sign * step / 10.0
            q = CL.hsv_to_rgb(h + dh / 360.0, s, v)
            if not ok(q): continue
            trial = dict(W); trial[c] = CL.rgb2hex(q)
            if not subfloor(trial):
                best = (dh, CL.rgb2hex(q)); break
        if best: break
    if best:
        dh, hx = best
        print("    %-13s %-9s -> %-9s  Δh %+6.1f°  (새 색상각 %.2f°, L=%.4f, 최악CR %.2f) — 이 한 색만으로 7쌍 전부 해소"
              % (nm(c), W[c], hx, dh, CL.hue_deg(CL.hex2rgb(hx)), CL.L(CL.hex2rgb(hx)),
                 min(CL.CR(CL.hex2rgb(hx), b) for b in BG)))
    else:
        print("    %-13s %-9s  홀로 돌려서는 7쌍을 다 못 없앤다 (다른 색도 함께 움직여야 한다)"
              % (nm(c), W[c]))

print("\n" + "=" * 90)
print("3. 카탈로그 25색 ↔ DLC 팩 12색 — 두 팔레트가 같은 대역에 있다. 부딪히는가")
print("=" * 90)
bad = []
for c in cols:
    for p in PACK12:
        d = CL.dE(CL.hex2rgb(W[c]), CL.hex2rgb(p))
        if d < DISCERN: bad.append((d, c, p))
for d, c, p in sorted(bad):
    print("  ★ dE %5.2f  카탈로그 %-13s %-9s ↔ 팩색 %s  색상각차 %.2f°"
          % (d, nm(c), W[c], p, hue_gap(W[c], p)))
print("  카탈로그↔팩 하한 미달 %d쌍 / %d쌍 중" % (len(bad), len(cols) * len(PACK12)))
mn = min((CL.dE(CL.hex2rgb(W[c]), CL.hex2rgb(p)), c, p) for c in cols for p in PACK12)
print("  최근접: dE %.2f  %s %s ↔ 팩 %s" % (mn[0], nm(mn[1]), W[mn[1]], mn[2]))

print("\n" + "=" * 90)
print("4. 팩 충돌은 이번 라운드가 만든 것인가 — 출하본(몸 위)과 대조")
print("=" * 90)
for tag, get in (("출하본(WornColor 후)", lambda c: CL.rgb2hex(CL.worn(CL.hex2rgb(n2o[c])))),
                 ("W=0.00 (적용됨)", lambda c: c),
                 ("W=0.10 (후보)", lambda c: W[c])):
    b = [(CL.dE(CL.hex2rgb(get(c)), CL.hex2rgb(p)), c, p) for c in cols for p in PACK12]
    under = [x for x in b if x[0] < DISCERN]
    print("  %-22s 최근접 dE %5.2f · 하한 미달 %d쌍" % (tag, min(b)[0], len(under)))
    for d, c, p in sorted(under):
        print("       dE %5.2f  %-13s %-9s ↔ 팩 %s" % (d, nm(c), get(c), p))

print("\n" + "=" * 90)
print("5. 색상각 재배치 제안 — 결정적 탐색(난수 없음)")
print("=" * 90)
print("  규칙: S·V는 건드리지 않는다(규칙이 정한 값이다). 색상각만 0.1° 격자로 움직인다.")
print("        제약 = 자립 대역 ∩ WornColor 항등 ∩ 배경4종 3.0 ∩ 카탈로그쌍 7.8 ∩ 팩12색 7.8")
print("        매 회 가장 나쁜 쌍을 골라, 그 쌍을 고치는 **최소 이동**을 가진 쪽을 움직인다.")

def all_bad(pal):
    out = []
    for a, b in itertools.combinations(cols, 2):
        d = CL.dE(CL.hex2rgb(pal[a]), CL.hex2rgb(pal[b]))
        if d < DISCERN: out.append((d, a, b))
    for c in cols:
        for p in PACK12:
            d = CL.dE(CL.hex2rgb(pal[c]), CL.hex2rgb(p))
            if d < DISCERN: out.append((d, c, "팩" + p))
    return sorted(out)

pal = dict(W)
orig = dict(W)
moved = {}
for it in range(40):
    bad = all_bad(pal)
    if not bad: break
    d0, A, B = bad[0]
    cands = []
    for c in ([A, B] if not B.startswith("팩") else [A]):
        h, s, v = CL.rgb_to_hsv(CL.hex2rgb(pal[c]))
        h0 = CL.rgb_to_hsv(CL.hex2rgb(orig[c]))[0]
        for step in range(1, 1801):
            found = False
            for sign in (+1, -1):
                dh = sign * step / 10.0
                q = CL.hsv_to_rgb(h + dh / 360.0, s, v)
                if not ok(q): continue
                tot = ((CL.hue_deg(q) - CL.hue_deg(CL.hex2rgb(orig[c]))) + 540) % 360 - 180
                if abs(tot) > 15.0: continue
                t = dict(pal); t[c] = CL.rgb2hex(q)
                nb = all_bad(t)
                if len(nb) < len(bad) and (not nb or nb[0][0] > d0):
                    cands.append((abs(dh), len(nb), c, CL.rgb2hex(q), tot)); found = True
            if found: break
    if not cands:
        print("  [%d] 가장 나쁜 쌍 dE %.2f (%s ↔ %s) — ±15° 안에서 못 고친다. 멈춘다."
              % (it, d0, nm(A), nm(B) if not B.startswith("팩") else B)); break
    cands.sort(key=lambda x: (x[1], x[0], x[2]))
    _, _, c, hx, tot = cands[0]
    print("  [%d] dE %5.2f (%-12s ↔ %-12s) -> %-12s %s -> %s  누적 Δh %+.1f°  남은 미달 %d쌍"
          % (it, d0, nm(A), nm(B) if not B.startswith("팩") else B, nm(c), pal[c], hx, tot,
             len(all_bad({**pal, c: hx})) ))
    pal[c] = hx
    moved[c] = (orig[c], hx, tot)

rem = all_bad(pal)
print("\n  결과: 남은 하한 미달 %d쌍" % len(rem))
for d, a, b in rem:
    print("    dE %5.2f  %s ↔ %s" % (d, nm(a), nm(b) if not b.startswith("팩") else b))
print("\n  움직인 색 %d개 / 25색:" % len(moved))
print("  %-13s %-9s %-9s %8s %8s %8s %7s %7s" % ("이름", "W=0.10", "제안", "Δh", "L", "최악CR", "항등", "대역"))
for c, (o, h_, t) in sorted(moved.items(), key=lambda x: CL.hue_deg(CL.hex2rgb(x[1][1]))):
    rgb = CL.hex2rgb(h_)
    print("  %-13s %-9s %-9s %+8.1f %8.4f %8.2f %7s %7s"
          % (nm(c), o, h_, t, CL.L(rgb), min(CL.CR(rgb, b) for b in BG),
             "예" if CL.is_worn_fixed(rgb) else "★아니오",
             "안" if BAND[0] <= CL.L(rgb) <= BAND[1] else "★밖"))
ds = sorted(CL.dE(CL.hex2rgb(pal[a]), CL.hex2rgb(pal[b])) for a, b in itertools.combinations(cols, 2))
import statistics
print("\n  제안 팔레트 dE: 최소 %.2f · 5퍼센타일 %.2f · 중앙 %.2f · <7.8 %d쌍"
      % (ds[0], ds[max(0, int(round(0.05 * (len(ds) - 1))))], statistics.median(ds),
         sum(1 for d in ds if d < DISCERN)))
print("  전 25색 검산: 대역 안 %d · 항등 %d · 배경4종 최악 %.2f"
      % (sum(1 for c in cols if BAND[0] <= CL.L(CL.hex2rgb(pal[c])) <= BAND[1]),
         sum(1 for c in cols if CL.is_worn_fixed(CL.hex2rgb(pal[c]))),
         min(min(CL.CR(CL.hex2rgb(pal[c]), b) for b in BG) for c in cols)))
