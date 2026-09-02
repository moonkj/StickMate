# -*- coding: utf-8 -*-
"""대역의 정원 — 25색이 들어가는가. 안 들어가면 무엇을 줄이는가. (design-art, 2026-09-02)
전수/결정적. 난수 없음."""
import os, sys, math, itertools, statistics
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
ok = lambda rgb: (BAND[0] <= CL.L(rgb) <= BAND[1] and CL.is_worn_fixed(rgb)
                  and min(CL.CR(rgb, b) for b in BG) >= 3.0)

def bads(pal, keys):
    out = []
    for a, b in itertools.combinations(keys, 2):
        d = CL.dE(CL.hex2rgb(pal[a]), CL.hex2rgb(pal[b]))
        if d < DISCERN: out.append((d, a, b))
    for c in keys:
        for p in PACK12:
            d = CL.dE(CL.hex2rgb(pal[c]), CL.hex2rgb(p))
            if d < DISCERN: out.append((d, c, "팩" + p))
    return sorted(out)

def greedy(pal, keys, cap=15.0, rounds=40, verbose=True):
    pal = dict(pal); orig = dict(pal); moved = {}
    for it in range(rounds):
        bd = bads(pal, keys)
        if not bd: break
        d0, A, B = bd[0]
        cands = []
        for c in ([A, B] if not str(B).startswith("팩") else [A]):
            h, s, v = CL.rgb_to_hsv(CL.hex2rgb(pal[c]))
            for step in range(1, 1801):
                hit = False
                for sign in (+1, -1):
                    dh = sign * step / 10.0
                    q = CL.hsv_to_rgb(h + dh / 360.0, s, v)
                    if not ok(q): continue
                    tot = ((CL.hue_deg(q) - CL.hue_deg(CL.hex2rgb(orig[c]))) + 540) % 360 - 180
                    if abs(tot) > cap: continue
                    t = dict(pal); t[c] = CL.rgb2hex(q)
                    nb = bads(t, keys)
                    if len(nb) < len(bd) and (not nb or nb[0][0] > d0):
                        cands.append((abs(dh), len(nb), c, CL.rgb2hex(q), tot)); hit = True
                if hit: break
        if not cands:
            if verbose: print("    막힘: dE %.2f (%s ↔ %s) — ±%.0f° 안에서 못 고친다"
                              % (d0, nm(A), nm(B) if not str(B).startswith("팩") else B, cap))
            break
        cands.sort(key=lambda x: (x[1], x[0], x[2]))
        _, _, c, hx, tot = cands[0]
        pal[c] = hx; moved[c] = tot
    return pal, moved, bads(pal, keys)

def report(tag, pal, keys):
    ds = sorted(CL.dE(CL.hex2rgb(pal[a]), CL.hex2rgb(pal[b])) for a, b in itertools.combinations(keys, 2))
    bd = bads(pal, keys)
    print("  %-34s n=%2d  최소 dE %5.2f · 5%%tile %5.2f · <7.8 %d쌍(팩 포함)"
          % (tag, len(keys), ds[0], ds[max(0, int(round(0.05*(len(ds)-1))))], len(bd)))
    return bd

print("=" * 92)
print("정원 시험 — 색 수를 줄이면 색상각만으로 하한을 넘길 수 있는가")
print("=" * 92)
HAIRSET = ["#936C3F", "#A0622A", "#A07830", "#A16A28", "#A86E1F", "#AF651C", "#BD501F"]
opts = [
    ("(0) 지금 25색 그대로", cols),
    ("(1) 머리카락 7 -> 3 (두피+덩어리+결)", [c for c in cols if c not in
        ("#A16A28", "#A86E1F", "#BD501F", "#AF651C")]),
    ("(2) 머리카락 7 -> 2 (덩어리+결)", [c for c in cols if c not in
        ("#936C3F", "#A16A28", "#A86E1F", "#BD501F", "#AF651C")]),
]
for tag, keys in opts:
    print("\n%s" % tag)
    print("  [이동 없음]", end=" "); report("", W, keys)
    pal, moved, rem = greedy(W, keys, cap=15.0, verbose=True)
    print("  [±15° 색상각 재배치 후]", end=" ")
    report("", pal, keys)
    print("    움직인 색 %d개: %s" % (len(moved), ", ".join(
        "%s %s->%s(%+.1f°)" % (nm(c), W[c], pal[c], moved[c]) for c in sorted(moved, key=lambda x: nm(x)))))
    for d, a, b in rem:
        print("    남음: dE %5.2f  %s ↔ %s" % (d, nm(a), nm(b) if not str(b).startswith("팩") else b))

print("\n" + "=" * 92)
print("★ 채택안 (1) 전문 — 21색 · 색상각만 움직임 · S/V는 규칙값 그대로")
print("=" * 92)
keys1 = [c for c in cols if c not in ("#A16A28", "#A86E1F", "#BD501F", "#AF651C")]
pal1, moved1, rem1 = greedy(W, keys1, cap=15.0, verbose=False)
print("  %-13s %-9s %-9s %-9s %7s %8s %8s %6s %5s" %
      ("이름", "출하", "W=0.10", "제안", "Δh", "색상각", "L", "최악CR", "항등"))
for c in sorted(keys1, key=lambda x: CL.hue_deg(CL.hex2rgb(pal1[x]))):
    rgb = CL.hex2rgb(pal1[c])
    dh = ((CL.hue_deg(rgb) - CL.hue_deg(CL.hex2rgb(W[c]))) + 540) % 360 - 180
    print("  %-13s %-9s %-9s %-9s %+7.1f %8.2f %8.4f %6.2f %5s"
          % (nm(c), n2o[c], W[c], pal1[c], dh, CL.hue_deg(rgb), CL.L(rgb),
             min(CL.CR(rgb, b) for b in BG), "예" if CL.is_worn_fixed(rgb) else "★아니오"))
print("\n  폐기하는 4색 (머리카락이 색이 아니라 실루엣으로 갈린다):")
for c in ("#A16A28", "#A86E1F", "#BD501F", "#AF651C"):
    users = sorted({r["asset"] for r in new if r["hex"] == c})
    print("    %-13s %s (옛 %s)  쓰던 곳 %s -> HairDark/HairDarkLit로 흡수"
          % (nm(c), c, n2o[c], ", ".join(users)))
ds = sorted(CL.dE(CL.hex2rgb(pal1[a]), CL.hex2rgb(pal1[b])) for a, b in itertools.combinations(keys1, 2))
print("\n  검산: 21색 %d쌍 — 최소 dE %.2f · 5%%tile %.2f · 중앙 %.2f · <7.8 %d쌍"
      % (len(ds), ds[0], ds[max(0,int(round(0.05*(len(ds)-1))))], statistics.median(ds),
         sum(1 for d in ds if d < DISCERN)))
print("  검산: 대역 안 %d/21 · WornColor 항등 %d/21 · 배경4종 최악 %.2f:1 · 팩12색 최근접 dE %.2f"
      % (sum(1 for c in keys1 if BAND[0] <= CL.L(CL.hex2rgb(pal1[c])) <= BAND[1]),
         sum(1 for c in keys1 if CL.is_worn_fixed(CL.hex2rgb(pal1[c]))),
         min(min(CL.CR(CL.hex2rgb(pal1[c]), b) for b in BG) for c in keys1),
         min(CL.dE(CL.hex2rgb(pal1[c]), CL.hex2rgb(p)) for c in keys1 for p in PACK12)))
# 머리카락 규칙 유지 확인
print("\n  머리카락 '보조=결이 더 밝다' 규칙 유지 확인:")
for a2, a, b in (("bald", "#936C3F", "#BE5B23"), ("나머지 5종", "#A0622A", "#A07830")):
    pa, pb = pal1.get(a, W.get(a)), pal1.get(b, W.get(b))
    print("    %-10s 덩어리 %s L %.4f | 결 %s L %.4f  dE %.2f  %s"
          % (a2, pa, CL.L(CL.hex2rgb(pa)), pb, CL.L(CL.hex2rgb(pb)),
             CL.dE(CL.hex2rgb(pa), CL.hex2rgb(pb)),
             "결>덩어리 ✔" if CL.L(CL.hex2rgb(pb)) > CL.L(CL.hex2rgb(pa)) else "★뒤집힘"))
