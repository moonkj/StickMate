# -*- coding: utf-8 -*-
"""4부 — 휘도 순서가 뜻을 나르는 **유일한 후보**: 아이템 내부 '주=덩어리 / 보=하이라이트'
   슬롯별로 방향이 일관하는가. 그리고 W=0.10이 그 일관성을 깨는가.   (design-art, 2026-09-02)"""
import os, sys, itertools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
from lumorder import load, INK_MARKERS, W010, NAMES_NEW, NAMES_OLD, hue_gap, DISCERN

CL.calibrate(verbose=False); print("교정 통과(16건)\n")
new, old = load("new"), load("old")
k = lambda r: (r["asset"], r["part"])
mo = {k(r): r["hex"] for r in old}
o2n = {}
for r in new: o2n[mo[k(r)]] = r["hex"]
n2o = {v: a for a, v in o2n.items()}
SLOT = {0: "머리", 1: "눈", 2: "목", 3: "어깨", 4: "머리카락", 5: "이펙트", 6: "펫"}

rows = []
for asset in sorted({r["asset"] for r in new}):
    rs = [r for r in new if r["asset"] == asset]
    t0 = next((r["hex"] for r in rs if r["tone"] == 0 and r["hex"] not in INK_MARKERS), None)
    t1 = next((r["hex"] for r in rs if r["tone"] == 1 and r["hex"] not in INK_MARKERS), None)
    if not (t0 and t1 and t0 != t1): continue
    rows.append((SLOT.get(rs[0]["slot"], "?"), asset, t0, t1))

print("=" * 92)
print("아이템 내부 방향 — 보조색(하이라이트/장식)이 주색보다 밝은가")
print("=" * 92)
Lh = lambda h: CL.L(CL.hex2rgb(h))
for slot in ["머리카락", "머리", "눈", "목", "어깨", "이펙트", "펫"]:
    g = [r for r in rows if r[0] == slot]
    if not g: continue
    def dirs(sub):
        return ["보밝" if Lh(sub(b)) > Lh(sub(a)) else "주밝" for _, _, a, b in g]
    d_old = ["보밝" if CL.L(CL.worn(CL.hex2rgb(n2o[b]))) > CL.L(CL.worn(CL.hex2rgb(n2o[a]))) else "주밝"
             for _, _, a, b in g]
    d00 = dirs(lambda x: x)
    d10 = dirs(lambda x: W010.get(x, x))
    cons = lambda d: "일관(%s)" % d[0] if len(set(d)) == 1 else "불일관 %d/%d" % (d.count("보밝"), len(d))
    print("  [%s] n=%d   출하 %-14s W=0.00 %-14s W=0.10 %s"
          % (slot, len(g), cons(d_old), cons(d00), cons(d10)))
    for (s, a2, a, b), o, x, y in zip(g, d_old, d00, d10):
        mark = "" if x == y else "  ★W=0.10이 뒤집음"
        chg = "" if o == x else "  (이번 라운드가 이미 뒤집음)"
        print("      %-28s 주 %s(L%.4f) 보 %s(L%.4f)  출하 %s / W00 %s / W10 %s%s%s"
              % (a2, a, Lh(a), b, Lh(b), o, x, y, chg, mark))

print("\n" + "=" * 92)
print("★ 머리카락 4종만 따로 — 여기가 유일하게 휘도가 뜻(덩어리 vs 결)을 나르는 자리다")
print("=" * 92)
hair = [r for r in rows if r[0] == "머리카락"]
for _, a2, a, b in hair:
    for tag, f in (("출하", lambda h: CL.worn(CL.hex2rgb(n2o[h]))),
                   ("W=0.00", lambda h: CL.hex2rgb(h)),
                   ("W=0.10", lambda h: CL.hex2rgb(W010.get(h, h)))):
        la, lb = CL.L(f(a)), CL.L(f(b))
        print("  %-20s %-7s 주 %s L%.4f | 보 %s L%.4f  차 %+.4f  dE %5.2f  %s"
              % (a2, tag, CL.rgb2hex(f(a)), la, CL.rgb2hex(f(b)), lb, lb - la,
                 CL.dE(f(a), f(b)), "보>주 ✔" if lb > la else "★뒤집힘"))
    print()

print("=" * 92)
print("검산 — W=0.10 후보가 기존 게이트를 깨지 않는가 (25색 전수)")
print("=" * 92)
cols = sorted({r["hex"] for r in new} - INK_MARKERS)
BG = [("흰", (255,255,255)), ("검", (0,0,0)), ("종이", CL.hex2rgb("#E9EAE6")), ("목탄", CL.hex2rgb("#25282E"))]
fails = 0
print("  %-9s %-9s %8s %8s %7s %7s %7s" % ("W=0.00", "W=0.10", "L", "색상각", "S", "항등", "최악CR"))
for c in cols:
    v = W010.get(c, c)
    rgb = CL.hex2rgb(v)
    l = CL.L(rgb); h, s, _ = CL.rgb_to_hsv(rgb)
    ident = CL.is_worn_fixed(rgb)
    worst = min(CL.CR(rgb, bg) for _, bg in BG)
    bad = (not (0.1632 <= l <= 0.2396)) or (not ident) or worst < 3.0 or s < 0.42
    fails += bad
    if c != v or bad:
        print("  %-9s %-9s %8.4f %8.2f %7.3f %7s %7.2f %s"
              % (c, v, l, h*360, s, "예" if ident else "★아니오", worst, "★위반" if bad else ""))
print("  위반 %d건 / 25색" % fails)
print("  색상각 이동 (W=0.00 -> W=0.10):")
for c, v in W010.items():
    print("    %s -> %s   색상각 %.2f° -> %.2f°  (이동 %+.2f°)  S %.3f -> %.3f"
          % (c, v, CL.hue_deg(CL.hex2rgb(c)), CL.hue_deg(CL.hex2rgb(v)),
             CL.hue_deg(CL.hex2rgb(v)) - CL.hue_deg(CL.hex2rgb(c)),
             CL.rgb_to_hsv(CL.hex2rgb(c))[1], CL.rgb_to_hsv(CL.hex2rgb(v))[1]))
print("\n  리더가 지목한 '휘도로 못 가르는 두 쌍'이 W=0.10에서 어떻게 되는가:")
for a, b, nmA, nmB in (("#6183B4", "#6787B9", "Silver", "Paper"), ("#A66930", "#A26B2F", "Wool", "Canvas")):
    d0 = CL.dE(CL.hex2rgb(a), CL.hex2rgb(b))
    d1 = CL.dE(CL.hex2rgb(W010.get(a, a)), CL.hex2rgb(W010.get(b, b)))
    print("    %-7s ↔ %-7s  W=0.00 dE %5.2f -> W=0.10 dE %5.2f  %s"
          % (nmA, nmB, d0, d1, "하한 %s" % ("통과" if d1 >= DISCERN else "여전히 미달")))
