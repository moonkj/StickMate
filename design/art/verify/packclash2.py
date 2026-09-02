# -*- coding: utf-8 -*-
"""§11-3 전체 꾸러미를 지금 트리에 다시 적용해 본다 + A+B가 새 충돌을 만드는지 (design-art)"""
import sys, os, itertools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL, band, derive_packs as DP
from packclash import load_current, pack_colors, min_dE, rotate, INK_MARKERS, DISCERN, BRASS

CL.calibrate()
cur = load_current()
cat = [h for h in cur if h not in INK_MARKERS]
packs = pack_colors([(n, h) for n, h, _ in DP.PACK_HUES])
pk = [c for _, _, p, s in packs for c in (p, s)]

# PALETTE_SPEC §11-3 전체 꾸러미
HAIR_DROP = ["#A16A28", "#A86E1F", "#BD501F", "#AF651C"]
HUE_MOVE = {"#5577AE": "#5586AE", "#955CCC": "#A45CCC", "#587398": "#587698",
            "#C6443C": "#C64A3C", "#5075B5": "#5071B5", "#9B7922": "#9B7C22"}

def apply(cols, drop=(), move=None):
    move = move or {}
    return [move.get(c, c) for c in cols if c not in drop]

def report(label, catset, pkset):
    ci = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a, b in itertools.combinations(catset, 2))
    pr = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a in catset for b in pkset)
    pp = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a, b in itertools.combinations(pkset, 2))
    band_ok = sum(1 for c in catset if band.limits()[0] <= CL.L(CL.hex2rgb(c)) <= band.limits()[1])
    ident = sum(1 for c in catset if CL.worn(CL.hex2rgb(c)) == CL.hex2rgb(c))
    bd = [("흰", (255,255,255)), ("검", (0,0,0)), ("종이", CL.hex2rgb("#E9EAE6")), ("목탄", CL.hex2rgb("#25282E"))]
    worst = min(min(CL.CR(CL.hex2rgb(c), b) for _, b in bd) for c in catset)
    print(f"{label:34s} n={len(catset):2d} 내부최소 {ci[0][0]:6.2f}(미달{sum(1 for x in ci if x[0]<DISCERN):2d}) "
          f"팩최소 {pr[0][0]:6.2f}(미달{sum(1 for x in pr if x[0]<DISCERN):2d}) "
          f"팩내부 {pp[0][0]:6.2f} 대역{band_ok}/{len(catset)} 항등{ident}/{len(catset)} 배경최악 {worst:.2f}")
    return ci, pr

print("=" * 108)
print("§A. §11-3 전체 꾸러미(머리 4색 폐기 + 색상각 6색 이동)를 지금 트리에 적용")
print("=" * 108)
ci0, pr0 = report("현행 25색", cat, pk)
ci1, pr1 = report("머리 병합만 (21색)", apply(cat, HAIR_DROP), pk)
ci2, pr2 = report("색상각 6색 이동만 (25색)", apply(cat, (), HUE_MOVE), pk)
cat21 = apply(cat, HAIR_DROP, HUE_MOVE)
ci3, pr3 = report("★ §11-3 전체 (21색)", cat21, pk)
print("\n  §11-3 전체의 하한 미달 쌍 (내부):", [(round(d,2), a, b) for d, a, b in ci3 if d < DISCERN] or "없음")
print("  §11-3 전체의 하한 미달 쌍 (↔팩):", [(round(d,2), a, b) for d, a, b in pr3 if d < DISCERN] or "없음")
print("  현행 25색 내부 미달 7쌍:", [(round(d,2), a, b) for d, a, b in ci0 if d < DISCERN])

print()
print("=" * 108)
print("§B. A와 B를 동시에 하면 새 충돌이 생기는가")
print("=" * 108)
for hb in (268, 279, 280, 281, 285, 290):
    p = CL.rgb2hex(DP.pick(float(hb), True)); s = CL.rgb2hex(DP.pick(float(hb), False))
    d_old = CL.dE(CL.hex2rgb(p), CL.hex2rgb("#955CCC"))
    d_new = CL.dE(CL.hex2rgb(p), CL.hex2rgb("#A45CCC"))
    print(f"  팩 {hb:3d}° 주 {p} 보조 {s} | TintBack 현행 #955CCC ΔE {d_old:6.2f} "
          f"| TintBack+8° #A45CCC ΔE {d_new:6.2f} {'★ A+B가 새 충돌' if d_new < DISCERN else ''}")

print()
print("=" * 108)
print("§C. 오피스 워커 보조색 #5C709E — 남은 진짜 병목")
print("=" * 108)
off_s = "#5C709E"
for c in sorted(cat, key=lambda x: CL.dE(CL.hex2rgb(x), CL.hex2rgb(off_s)))[:5]:
    print(f"  {c} ({','.join(sorted(set(cur[c]))[:3])}) ΔE {CL.dE(CL.hex2rgb(c), CL.hex2rgb(off_s)):.2f}")
print("  §11-3 색상각 이동 뒤:")
for c in sorted(cat21, key=lambda x: CL.dE(CL.hex2rgb(x), CL.hex2rgb(off_s)))[:5]:
    print(f"  {c} ΔE {CL.dE(CL.hex2rgb(c), CL.hex2rgb(off_s)):.2f}")
print("  [대안] 오피스 팩 색상각 이동 — 군청 계열(200~240°)에서 최선")
fixed = cat + [c for n, _, p2, s2 in packs if n != "오피스 워커" for c in (p2, s2)] + [BRASS]
best = []
for hdeg in range(195, 246):
    p, s = DP.pick(float(hdeg), True), DP.pick(float(hdeg), False)
    if not p or not s: continue
    m = min(min_dE(CL.rgb2hex(p), fixed)[0], min_dE(CL.rgb2hex(s), fixed)[0])
    best.append((m, hdeg, CL.rgb2hex(p), CL.rgb2hex(s)))
best.sort(reverse=True)
for m, h, p, s in best[:6]:
    print(f"    {h:3d}° 주 {p} 보조 {s} 최소 ΔE {m:5.2f}")
print(f"    현행 222°: 최소 ΔE {[x[0] for x in best if x[1]==222][0]:.2f}")

print()
print("=" * 108)
print("§D. 여섯 팩이 '한 세계'인가 — 통일 축 재검산 (지금 값)")
print("=" * 108)
Ls = [CL.L(CL.hex2rgb(c)) for c in pk]
print(f"  12색 L 범위 {min(Ls):.4f} ~ {max(Ls):.4f} · 최대/최소 대비 {(max(Ls)+0.05)/(min(Ls)+0.05):.2f}:1")
bd = [("흰 바탕화면", (255,255,255)), ("검은 바탕화면", (0,0,0)),
      ("종이 무대", CL.hex2rgb("#E9EAE6")), ("목탄 무대", CL.hex2rgb("#25282E"))]
print(f"  배경 4종 최악 {min(min(CL.CR(CL.hex2rgb(c), b) for _, b in bd) for c in pk):.2f}:1")
print(f"  WornColor 항등 {sum(1 for c in pk if CL.worn(CL.hex2rgb(c))==CL.hex2rgb(c))}/12")
pp = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a, b in itertools.combinations(pk, 2))
print(f"  팩 12색 내부 최소 ΔE {pp[0][0]:.2f} ({pp[0][1]}↔{pp[0][2]}) · 식별 하한 48.6 넘는 쌍 "
      f"{sum(1 for x in pp if x[0]>=48.6)}/{len(pp)}")
print(f"  브라스 #C8A15A 최근접 팩색 ΔE {min_dE(BRASS, pk)[0]:.2f} ({min_dE(BRASS, pk)[1]})")
