# -*- coding: utf-8 -*-
"""3부 — (E) 순서 보존의 두 기준틀 (F) 색 사용처 (G) 하한 미달 17/7쌍 (H) §1-4 산문 대조
                                                            (design-art, 2026-09-02)"""
import os, sys, itertools, statistics
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
inv_n = {v: a for a, v in NAMES_NEW.items()}; inv_o = {v: a for a, v in NAMES_OLD.items()}
nm = lambda h: inv_n.get(h) or inv_o.get(n2o.get(h, ""), "") or "무명"
cols = sorted({r["hex"] for r in new} - INK_MARKERS)

print("=" * 78)
print("E. '휘도 순서를 지켰다'는 어느 기준틀에서 참인가")
print("=" * 78)
for tag, oldL in (("카탈로그 값 기준(카드)", lambda h: CL.L(CL.hex2rgb(n2o[h]))),
                  ("몸 위 기준(WornColor 후)", lambda h: CL.L(CL.worn(CL.hex2rgb(n2o[h]))))):
    for band, lab in ((15, "색상대 ±15°"), (360, "전체")):
        f = 0
        for a, b in itertools.combinations(cols, 2):
            if hue_gap(a, b) > band: continue
            if (oldL(a) - oldL(b)) * (CL.L(CL.hex2rgb(a)) - CL.L(CL.hex2rgb(b))) < 0: f += 1
        print("  %-24s %-12s 출하 -> W=0.00 역전 %2d쌍" % (tag, lab, f))
print("  -> 재맵이 지킨 것은 **카탈로그 값의 순서**다. 몸 위 순서는 이미 이번 라운드가 바꿨다.")

print("\n" + "=" * 78)
print("F. 색 사용처 — 최근접 쌍이 한 화면/한 물건에서 만나는가")
print("=" * 78)
use = {}
for r in new:
    if r["hex"] in INK_MARKERS: continue
    use.setdefault(r["hex"], set()).add(r["asset"])
SLOT = {0: "머리", 1: "눈", 2: "목", 3: "어깨", -1: "룩"}
slot_of = {r["asset"]: r["slot"] for r in new}
for h in cols:
    a = sorted(use[h])
    print("  %-10s %s  (옛 %s)  슬롯 %s"
          % (nm(h), h, n2o[h], sorted({SLOT.get(slot_of[x], str(slot_of[x])) for x in a})))
    print("        %s" % ", ".join(a))

print("\n" + "=" * 78)
print("G. 변별 하한 %.1f 미달 쌍 전량" % DISCERN)
print("=" * 78)
for tag, get in (("W=0.00 (적용됨)", lambda c: c), ("W=0.10 (후보)", lambda c: W010.get(c, c))):
    rows = []
    for a, b in itertools.combinations(cols, 2):
        d = CL.dE(CL.hex2rgb(get(a)), CL.hex2rgb(get(b)))
        if d < DISCERN:
            rows.append((d, a, b, hue_gap(get(a), get(b)),
                         bool(use[a] & use[b])))
    rows.sort()
    print("\n  [%s] %d쌍" % (tag, len(rows)))
    for d, a, b, g, same in rows:
        print("    dE %5.2f  색상각차 %5.2f°  %-10s %s ↔ %-10s %s %s"
              % (d, g, nm(a), get(a), nm(b), get(b), "★같은 아이템에서 만난다" if same else ""))

print("\n" + "=" * 78)
print("H. PALETTE_SPEC §1-4 산문 수치 대조 (잉크 표식 2색 포함/제외)")
print("=" * 78)
cold = sorted({r["hex"] for r in old})
cold_m = [c for c in cold if c not in INK_MARKERS]
for tag, cs in (("27색(잉크 표식 포함)", cold), ("25색(제외 — 몸에서 잴 대상이 없다)", cold_m)):
    chg = [c for c in cs if not CL.is_worn_fixed(CL.hex2rgb(c))]
    des = sorted(CL.dE(CL.hex2rgb(c), CL.worn(CL.hex2rgb(c))) for c in cs)
    pin = sum(1 for c in cs if CL.worn(CL.hex2rgb(c))[0] == 204)
    outs = sum(1 for c in cs if min(CL.CR(CL.worn(CL.hex2rgb(c)), bg) for bg in
               [(255,255,255),(0,0,0),CL.hex2rgb("#E9EAE6"),CL.hex2rgb("#25282E")]) < 3.0)
    print("  [%s] n=%d  WornColor가 바꾸는 색 %d · dE 중앙 %.1f 최악 %.1f · R=204 %d · 대역밖 %d"
          % (tag, len(cs), len(chg), statistics.median(des), des[-1], pin, outs))
