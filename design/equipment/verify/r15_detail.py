#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""처방 시뮬레이션: '디테일 획' 등급(하한 1pt, 굵기 x0.75)을 신설하면 몇 조각이 살아나는가."""
import importlib.util, io, contextlib, os, json
here = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("geom", os.path.join(here, "r15_handoff_geom.py"))
geom = importlib.util.module_from_spec(spec)
with contextlib.redirect_stdout(io.StringIO()): spec.loader.exec_module(geom)
rows, S = geom.rows, geom.S
feat = {(f["item"], f["i"]): set(f["feat"]) for f in json.load(open(os.path.join(here, "pieces.json")))}

BASE, PT, HR = 0.048, 846.0/24.0, 0.22
def w_of(scale, mult, floor_pt):
    return max(BASE*mult*scale, floor_pt/PT) / (HR*scale)

print("== 획 등급별 실제 폭 (R 배수) ==")
grades = [("현행 낱선  (x1.00, 하한 2pt)", 1.00, 2.0),
          ("현행 채움윤곽(x1.00, 하한 1pt)", 1.00, 1.0),
          ("신설 디테일획(x0.75, 하한 1pt)", 0.75, 1.0),
          ("신설 실낱획  (x0.70, 하한 0.75pt)", 0.70, 0.75)]
for lbl, m, f in grades:
    print("   %-32s @0.75 = %.4f R   @0.60 = %.4f R   @1.00 = %.4f R"
          % (lbl, w_of(0.75, m, f), w_of(0.60, m, f), w_of(1.00, m, f)))

print("\n== 처방 효과: 조각이 자기 획의 1.5배 이상 길이를 갖는가 (@0.75) ==")
def kind_grade(kind, f):
    """인계본이 선언한 굵기 배수를 그대로 등급으로 매핑."""
    mult = 1.0
    for x in f:
        if x.startswith("strokeW:x"): mult = float(x[9:])
    return mult
for lbl, m_over, fl in [("현행(모든 선 x1.00 / 하한 2pt)", None, 2.0),
                        ("처방(인계본 배수 그대로 / 하한 1pt)", "asis", 1.0)]:
    alive = 0; hl_alive = 0
    for r in rows:
        f = feat[(r["item"], r["i"])]
        m = 1.0 if m_over is None else min(kind_grade(r["kind"], f), 1.0)
        w = w_of(0.75, m, fl)
        ok = r["ink_R"] >= 1.5*w
        alive += ok
        if r["kind"] == "H" and ok: hl_alive += 1
    print("   %-36s 생존 %2d/91   그중 하이라이트 %2d/17" % (lbl, alive, hl_alive))

print("\n== 하이라이트 17개 — 디테일획(0.1719 R @0.75) 기준 재판정 ==")
wd = w_of(0.75, 0.75, 1.0)
for r in sorted([x for x in rows if x["kind"] == "H"], key=lambda x: -x["ink_R"]):
    print("   %-13s 잉크 %.4f R = %.2f 디테일획   %s"
          % (r["item"], r["ink_R"], r["ink_R"]/wd, "생존" if r["ink_R"] >= 1.5*wd else "소멸"))

print("\n== 카드(44px · 획 1.87px · FitFraction 0.86) 축에서의 하이라이트 ==")
env = {}
for r in rows:
    x = env.setdefault(r["item"], [1e9, 1e9, -1e9, -1e9])
    s = S[r["slot"]]
    xs = [q[0]*s for q in r["pts"]]; ys = [q[1]*s for q in r["pts"]]
    x[0] = min(x[0], min(xs)); x[1] = min(x[1], min(ys))
    x[2] = max(x[2], max(xs)); x[3] = max(x[3], max(ys))
ok = 0
for r in sorted([x for x in rows if x["kind"] == "H"], key=lambda x: -x["ink_R"]):
    e = env[r["item"]]; E = max(e[2]-e[0], e[3]-e[1])
    px = r["ink_R"]/E * 44 * 0.86
    good = px >= 1.5*1.87
    ok += good
    print("   %-13s 카드상 %.2f px  (문턱 2.805 px)  %s" % (r["item"], px, "생존" if good else "소멸"))
print("   -> 카드에서 생존 %d / 17" % ok)
