#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""geom.py + feat.py 교차표 — 44개 소멸의 원인 분해 (가)픽셀 / (나)렌더러 / (다)데이터모델"""
import json, subprocess, sys, importlib.util, os
here = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("geom", os.path.join(here, "r15_handoff_geom.py"))
import io, contextlib
geom = importlib.util.module_from_spec(spec)
with contextlib.redirect_stdout(io.StringIO()):
    spec.loader.exec_module(geom)

rows  = geom.rows
items = geom.items
split = geom.split_calls
W075  = geom.W075
WOUT  = geom.WOUT075

feat = json.load(open(os.path.join(here, "pieces.json")))
fmap = {(f["item"], f["i"]): set(f["feat"]) for f in feat}

def blocked(f):
    a = any(x.startswith("alpha:") and not x.startswith("alpha:fill=grad") for x in f)
    g = "fill:gradient" in f
    s = any(x.startswith("strokeW:x") and x != "strokeW:x1.0" for x in f)
    wte = "strokeColor:WHITE" in f
    d = "dash" in f
    return a or g or s or wte or d

STROKE_KINDS = ("S", "H")          # 선 — 규칙 1(1.5 W)이 지배
AREA_KINDS   = ("B", "F", "CB", "CF", "RB")   # 면 — 규칙 1-C(W_out)가 지배

n_dead = n_alive = 0
tab = {}
for r in rows:
    f = fmap[(r["item"], r["i"])]
    dead = r["ink_R"] < 1.5 * W075
    blk  = blocked(f)
    key = ("픽셀부족" if dead else "픽셀OK", "표현불가" if blk else "표현가능")
    tab[key] = tab.get(key, 0) + 1

print("== 교차표: 91조각 × (픽셀 축 @0.75) × (표현 기능 축) ==")
print("                     표현가능   표현불가   계")
for a in ("픽셀OK", "픽셀부족"):
    r1 = tab.get((a, "표현가능"), 0); r2 = tab.get((a, "표현불가"), 0)
    print("   %-16s %5d %9d %6d" % (a, r1, r2, r1+r2))
tot1 = tab.get(("픽셀OK","표현가능"),0)+tab.get(("픽셀부족","표현가능"),0)
tot2 = tab.get(("픽셀OK","표현불가"),0)+tab.get(("픽셀부족","표현불가"),0)
print("   %-16s %5d %9d %6d" % ("계", tot1, tot2, tot1+tot2))

print("\n★ 픽셀은 충분한데 표현 기능이 없어서 못 그리는 조각: %d/91 (%.0f%%)"
      % (tab.get(("픽셀OK","표현불가"),0), 100*tab.get(("픽셀OK","표현불가"),0)/91))
print("★ 픽셀 자체가 모자란 조각(어떤 렌더러를 써도 못 살림)  : %d/91 (%.0f%%)"
      % (tab.get(("픽셀부족","표현가능"),0)+tab.get(("픽셀부족","표현불가"),0),
         100*(tab.get(("픽셀부족","표현가능"),0)+tab.get(("픽셀부족","표현불가"),0))/91))

# ---- 면 조각은 1.5W 가 아니라 1-C(W_out=0.2182) 가 맞는 자 ----
print("\n== 자를 바꾸면: 면 조각은 규칙 1-C(짧은변 >= 2*W_out = %.4f R), 선 조각은 1.5W ==" % (2*WOUT))
def short_side(p):
    xs=[q[0] for q in p]; ys=[q[1] for q in p]
    return min(max(xs)-min(xs), max(ys)-min(ys))
dead2 = 0; dead2_list=[]
for r in rows:
    s = geom.S[r["slot"]]
    if r["kind"] in STROKE_KINDS:
        ok = r["ink_R"] >= 1.5*W075
    else:
        ok = short_side(r["pts"])*s >= 2*WOUT
    if not ok:
        dead2 += 1; dead2_list.append((r["item"], r["i"], r["kind"]))
print("   올바른 자로 재판정한 소멸 조각: %d/91 (%.0f%%)" % (dead2, 100*dead2/91))

# ---- 하이라이트만 따로 ----
hl = [r for r in rows if r["kind"] == "H"]
alive_hl = sum(1 for r in hl if r["ink_R"] >= 1.5*W075)
print("\n== 하이라이트 17개 — 픽셀 축만 보면 ==")
for r in sorted(hl, key=lambda x: -x["ink_R"]):
    print("   %-13s 잉크 %.4f R = %.2f W  %s" % (r["item"], r["ink_R"], r["ink_R"]/W075,
          "OK" if r["ink_R"] >= 1.5*W075 else "미달"))
print("   -> 1.5W 통과 %d / 17" % alive_hl)
