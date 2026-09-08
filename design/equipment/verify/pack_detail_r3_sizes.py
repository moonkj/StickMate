# -*- coding: utf-8 -*-
"""R3 — 세 크기(카드 58pt · 슬롯행 24pt · 착용 머리 지름 11.63/15.51pt)에서의 실측표.
   착용 크기는 배율 0.75/1.00 의 머리 반경 R = 11.6250/2 · 15.5100/2 pt 를 그대로 곱한다
   (ReferencePointsPerWorldUnitApprox = 846/24 = 35.25 pt/unit · HeadRadius = 0.22 unit)."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, pack78_shapes as P78
import pack_detail_r2 as R2
import pack_detail_r3 as R3
from pack_detail_r2 import NOMINAL, W075

PT_PER_UNIT = 35.25
HEAD_R_UNIT = 0.22
MIN_ACC_STROKE_PT = 1.0          # StickConfig.MinAccessoryStrokeScreenPoints

def worn_R_pt(scale): return HEAD_R_UNIT * scale * PT_PER_UNIT

def true_min_edge(pcs):
    m, w = 9e9, None
    for pc in pcs:
        if pc.noStroke: continue
        p = pc.pts; n = len(p)
        segs = [(p[i], p[i+1]) for i in range(n-1)] + ([(p[-1], p[0])] if pc.loop and n > 2 else [])
        for a, b in segs:
            d = math.hypot(b[0]-a[0], b[1]-a[1])
            if d < m: m, w = d, pc.name
    return m, w

print("자: 카드 58pt 획 %.5fpt · 슬롯행 24pt 획 %.5fpt (2.2/64 x 상자) · 착용 획 = max(strokeInR x R, 1pt)"
      % (P78.card_stroke_pt(58.0), P78.card_stroke_pt(24.0)))
print("   착용 R = %.4fpt @0.75 (머리 지름 %.3fpt) · %.4fpt @1.00 (지름 %.3fpt)"
      % (worn_R_pt(0.75), 2*worn_R_pt(0.75), worn_R_pt(1.00), 2*worn_R_pt(1.00)))
print()
hdr = ("%-16s %-5s %7s %7s | %8s %8s | %8s %8s %8s | %8s %8s %8s"
       % ("아이템", "슬롯", "조각", "점", "58최단", "24최단", "0.75획", "0.75최단", "0.75보조", "1.0획", "1.0최단", "1.0보조")) + " | 꺾임최단"
print(hdr); print("-"*len(hdr))
rows = []
for packname, key, motif, PACK in R3.PACKS:
    for slot in ("HEAD", "EYES", "NECK", "BACK"):
        disp, iid, fn = PACK[slot]
        pcs = fn()
        allp = [q for p in pcs for q in p.pts]
        e, _ = true_min_edge(pcs)
        x0, y0, x1, y1 = rig.bounds(allp); span = max(x1-x0, y1-y0)
        upr = P78.units_per_R(slot); shrink = min(1.0, P78.ICON_VIEWBOX/(span*upr))
        c58 = e*upr*shrink*(58.0/64.0); c24 = e*upr*shrink*(24.0/64.0)
        r_acc = max(P78.rho_max(p.pts) for p in pcs if p.tone == 1)
        ce, _ = P78.corner_min_edge([p.to_shape(slot) for p in pcs], W075)   # 규칙 1 이 재는 «양끝 꺾임» 변
        out = [disp, slot, len(pcs), len(allp), c58, c24]
        cepp = (None if ce > 9e8 else ce)
        for sc in (0.75, 1.00):
            Rp = worn_R_pt(sc)
            stroke = max(NOMINAL[slot]*Rp, MIN_ACC_STROKE_PT)
            out += [stroke, e*Rp, 2*r_acc*Rp]
        out.append(cepp)
        rows.append(out)
        print("%-16s %-5s %7d %7d | %8.2f %8.2f | %8.3f %8.2f %8.2f | %8.3f %8.2f %8.2f | %s" % tuple(out[:12] + [
            "매끈" if cepp is None else "%.2f획" % (cepp/W075)]))
print()
print("판정 —  58pt 최단변 >= %.3fpt : %s   |   24pt 최단변 >= %.3fpt : %s"
      % (P78.card_stroke_pt(58.0), "12/12" if all(r[4] >= P78.card_stroke_pt(58.0) for r in rows) else "미달 있음",
         P78.card_stroke_pt(24.0), "12/12" if all(r[5] >= P78.card_stroke_pt(24.0) for r in rows) else "미달 있음"))
print()
print("★ «0.75최단» 은 «그려지는 모든 변» 중 가장 짧은 것이라 **곡선 표본**을 포함한다 — 규칙 1 이 재는 것은")
print("  마지막 칸의 «양끝이 모두 꺾임(>=45도)인 변»이고, 그쪽 하한이 1.00획이다. 곡선 표본이 획보다 짧은 것은")
print("  둥근 캡이 이어 그리므로 결함이 아니다(R2 §6-2 «호 표본 수는 자유 파라미터가 아니다»의 반대쪽 면).")
for i, sc in ((6, 0.75), (9, 1.00)):
    okn = sum(1 for r in rows if r[i+1] >= r[i])
    print("        착용 %.2f 최단변 >= 획 : %d/12  (최소 %.2f획, %s)"
          % (sc, okn, min(r[i+1]/r[i] for r in rows), min(rows, key=lambda r: r[i+1]/r[i])[0]))
    okb = sum(1 for r in rows if r[i+2] >= 1.0)
    print("        착용 %.2f 보조색 두께 >= 1pt : %d/12  (최소 %.2fpt)" % (sc, okb, min(r[i+2] for r in rows)))
