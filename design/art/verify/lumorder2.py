# -*- coding: utf-8 -*-
"""휘도 순서 판정 2부 — (1) 아이템 내부 변별 (2) 새 팔레트가 이미 뒤집은 것 (3) 색상각 중복 전수
   (4) 산문 정정 2건의 출처 추적                                       (design-art, 2026-09-02)"""
import os, sys, itertools, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
from lumorder import load, INK_MARKERS, W010, NAMES_NEW, NAMES_OLD, hue_gap, DISCERN, worst_bg, BACKGROUNDS

CL.calibrate(verbose=False)
print("교정 통과(16건) — 아래 숫자 유효\n")

new, old = load("new"), load("old")
k = lambda r: (r["asset"], r["part"])
mo = {k(r): r["hex"] for r in old}
o2n = {}
for r in new: o2n[mo[k(r)]] = r["hex"]
n2o = {v: kk for kk, v in o2n.items()}
inv_n = {v: a for a, v in NAMES_NEW.items()}
inv_o = {v: a for a, v in NAMES_OLD.items()}
nm = lambda h: inv_n.get(h) or inv_o.get(n2o.get(h, ""), "") or "(무명)"

# 아이템별 (주,보조)
items = []
for asset in sorted({r["asset"] for r in new}):
    rs = [r for r in new if r["asset"] == asset]
    t0 = next((r["hex"] for r in rs if r["tone"] == 0 and r["hex"] not in INK_MARKERS), None)
    t1 = next((r["hex"] for r in rs if r["tone"] == 1 and r["hex"] not in INK_MARKERS), None)
    if t0 and t1 and t0 != t1:
        items.append((asset, t0, t1))

print("=" * 78)
print("A. 아이템 내부 변별 — 주색↔보조색이 한 아이콘 안에서 붙어 있다 (하한 dE %.1f)" % DISCERN)
print("=" * 78)
print("  %-28s %8s %8s %8s" % ("아이템", "옛 dE", "W=0.00", "W=0.10"))
bad00 = bad10 = badold = 0
worst = []
for asset, a, b in items:
    d_old = CL.dE(CL.worn(CL.hex2rgb(n2o[a])), CL.worn(CL.hex2rgb(n2o[b])))
    d_00 = CL.dE(CL.hex2rgb(a), CL.hex2rgb(b))
    d_10 = CL.dE(CL.hex2rgb(W010.get(a, a)), CL.hex2rgb(W010.get(b, b)))
    badold += d_old < DISCERN; bad00 += d_00 < DISCERN; bad10 += d_10 < DISCERN
    worst.append((min(d_00, d_10), asset, d_old, d_00, d_10))
    flag = ""
    if d_00 < DISCERN: flag += " [W00 미달]"
    if d_10 < DISCERN: flag += " [W10 미달]"
    print("  %-28s %8.2f %8.2f %8.2f%s" % (asset, d_old, d_00, d_10, flag))
print("  --- 하한 미달 아이템 수: 옛 %d / W=0.00 %d / W=0.10 %d (전 %d종)"
      % (badold, bad00, bad10, len(items)))

print("\n" + "=" * 78)
print("B. 새 팔레트(W=0.00)가 **이미** 뒤집은 휘도 순서 — 출하본 대비")
print("=" * 78)
cols_new = sorted({r["hex"] for r in new} - INK_MARKERS)
flips_all = []
for a, b in itertools.combinations(cols_new, 2):
    oa, ob = n2o[a], n2o[b]
    if hue_gap(a, b) > 15: continue
    lo_a, lo_b = CL.L(CL.worn(CL.hex2rgb(oa))), CL.L(CL.worn(CL.hex2rgb(ob)))
    ln_a, ln_b = CL.L(CL.hex2rgb(a)), CL.L(CL.hex2rgb(b))
    if (lo_a - lo_b) * (ln_a - ln_b) < 0:
        flips_all.append((a, b, lo_a, lo_b, ln_a, ln_b))
print("  색상대 폭 ±15° 안에서 출하본 -> W=0.00 이 뒤집은 쌍: %d" % len(flips_all))
for a, b, la, lb, na, nb in flips_all:
    print("    %-12s %s L %.4f -> %.4f | %-12s %s L %.4f -> %.4f"
          % (nm(a), a, la, na, nm(b), b, lb, nb))
# 아이템 내부 순서가 뒤집힌 것
intra_flip = []
for asset, a, b in items:
    lo = CL.L(CL.worn(CL.hex2rgb(n2o[a]))) - CL.L(CL.worn(CL.hex2rgb(n2o[b])))
    ln = CL.L(CL.hex2rgb(a)) - CL.L(CL.hex2rgb(b))
    if lo * ln < 0: intra_flip.append(asset)
print("  아이템 내부(주 vs 보조) 순서를 W=0.00이 이미 뒤집은 아이템: %d/%d  %s"
      % (len(intra_flip), len(items), intra_flip))

print("\n" + "=" * 78)
print("C. 색상각 중복 전수 — 휘도로는 못 가르는 쌍 (새 팔레트, 색상각차 <= 3.5°)")
print("=" * 78)
dups = []
for a, b in itertools.combinations(cols_new, 2):
    g = hue_gap(a, b)
    if g <= 3.5:
        dups.append((g, CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b))
for g, d, a, b in sorted(dups):
    oa, ob = n2o[a], n2o[b]
    print("  색상각차 %5.2f°  dE %5.2f   %-12s %s (옛 %s)  ↔  %-12s %s (옛 %s)"
          % (g, d, nm(a), a, oa, nm(b), b, ob))
print("  총 %d쌍" % len(dups))

print("\n  같은 쌍의 **출하본** 색상각차 — 원죄인가 이번 라운드 탓인가")
for g, d, a, b in sorted(dups):
    oa, ob = n2o[a], n2o[b]
    print("    %-12s ↔ %-12s   출하 %5.2f°  새 %5.2f°   (이동 %+.2f°)"
          % (nm(a), nm(b), hue_gap(oa, ob), g, g - hue_gap(oa, ob)))

print("\n" + "=" * 78)
print("D. 산문 정정 2건 — 2.48:1은 어디서 왔는가")
print("=" * 78)
paper = CL.hex2rgb("#E9EAE6")
print("  종이 무대 #E9EAE6 L = %.4f" % CL.L(paper))
print("  #7690CC  L = %.4f  ->  종이 무대 대비 %.4f:1  (산문 2.48, 도구 2.62)"
      % (CL.L(CL.hex2rgb("#7690CC")), CL.CR(CL.hex2rgb("#7690CC"), paper)))
# 첫 유도 대역 상한 L=0.30 의 종이 무대 대비
for Lc in (0.3000, 0.2816, 0.2396):
    cr = (CL.L(paper) + 0.05) / (Lc + 0.05)
    print("    가상 색 L=%.4f 의 종이 무대 대비 = %.4f:1" % (Lc, cr))
print("  -> 2.48은 **첫 유도 대역의 상한 L=0.3000**이 종이 무대에서 내는 값이다.")
print("     산문이 '대역 상한이 허용하는 최악'과 '내가 실제로 고른 색'을 뒤섞었다.")

print("\n  §1-4 산문 수치 재측정 (지금 트리 기준)")
cols_old = sorted({r["hex"] for r in old} - INK_MARKERS)
chg = sum(1 for c in cols_old if not CL.is_worn_fixed(CL.hex2rgb(c)))
des = sorted(CL.dE(CL.hex2rgb(c), CL.worn(CL.hex2rgb(c))) for c in cols_old)
import statistics
print("    [출하본] WornColor가 바꾸는 색 %d/25 · 카드↔몸 dE 중앙 %.1f 최악 %.1f"
      % (chg, statistics.median(des), des[-1]))
pin = sum(1 for c in cols_old if CL.worn(CL.hex2rgb(c))[0] == 204)
print("    [출하본] 몸 위 R=204 못박힘 %d/25" % pin)
chg_n = sum(1 for c in cols_new if not CL.is_worn_fixed(CL.hex2rgb(c)))
pin_n = sum(1 for c in cols_new if CL.worn(CL.hex2rgb(c))[0] == 204)
print("    [새 팔레트] WornColor가 바꾸는 색 %d/25 · R=204 못박힘 %d/25" % (chg_n, pin_n))
w10 = [W010.get(c, c) for c in cols_new]
print("    [W=0.10] WornColor 항등 %d/25 · 대역 안 %d/25 · 배경4종 최악 %.2f:1"
      % (sum(1 for c in w10 if CL.is_worn_fixed(CL.hex2rgb(c))),
         sum(1 for c in w10 if 0.1632 <= CL.L(CL.hex2rgb(c)) <= 0.2396),
         min(worst_bg(c) for c in w10)))
for c in w10:
    l = CL.L(CL.hex2rgb(c))
    if not (0.1632 <= l <= 0.2396) or not CL.is_worn_fixed(CL.hex2rgb(c)) or worst_bg(c) < 3.0:
        print("      ★ %s L=%.4f 항등=%s 최악=%.2f" % (c, l, CL.is_worn_fixed(CL.hex2rgb(c)), worst_bg(c)))
