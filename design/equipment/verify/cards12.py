# -*- coding: utf-8 -*-
"""FX/PET 12종의 **현행 카드 아이콘**(Resources/Items/*.asset, 40x40 캔버스) 검산.
카드 획 = CharacterInfoWindow.IconStroke = 1.7 캔버스 유닛.

★ 2026-09-02 — 좌표를 손으로 베낀 표를 **.asset 직접 파싱**으로 바꿨다. 사본을 손으로 들고 있으면
   에셋을 고치는 순간 하니스가 거짓 초록을 낸다. 파서는 cards42.py 것을 그대로 쓴다(눈금이 하나다).
   원(Ring/DashedRing/Dot)은 변이 아니라 **지름**으로 재는 것도 cardspec12.py와 같은 규약이다.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rig import bounds
import cards42 as C

WC = C.STROKE_V   # 1.7
LO, HI = WC / 2.0, C.VIEW - WC / 2.0

def true_min(pts, loop):
    n = len(pts); best = None
    for i in range(n if loop else n - 1):
        L = math.dist(pts[i], pts[(i + 1) % n])
        if L < 1e-9: continue
        best = L if best is None else min(best, L)
    return best

print("╔══ FX/PET 12종 **현행 카드 아이콘** 검산 (40x40 캔버스, 카드 획 = %.1f 유닛) ══╗" % WC)
bad = 0
for f in sorted(os.listdir(C.ASSETS)):
    if not f.endswith(".asset"): continue
    name, slot, idx, parts = C.parse_asset(os.path.join(C.ASSETS, f))
    if slot not in (5, 6): continue          # FX / PET만 폴백이 실제로 그려진다
    msgs, info = [], []
    if not (2 <= len(parts) <= 4): msgs.append("정원 %d개(2~4 밖)" % len(parts))
    acc = sum(1 for p in parts if p["tone"] == 1)
    if acc != 1: msgs.append("보조색 %d개" % acc)
    for i, p in enumerate(parts):
        nm, v = "p%d" % i, p["values"]
        if p["kind"] in (1, 2, 3):           # Ring / DashedRing / Dot
            cx, cy, r = v[0], v[1], v[2]
            info.append("%s ⌀%.2f획" % (nm, 2 * r / WC))
            if 2 * r < 1.5 * WC: msgs.append("%s 지름 %.2f획 < 1.5" % (nm, 2 * r / WC))
            if cx - r < LO or cy - r < LO or cx + r > HI or cy + r > HI:
                msgs.append("%s 상자 밖" % nm)
            continue
        pts = [(v[j], v[j + 1]) for j in range(0, len(v) - 1, 2)]
        loop = (len(pts) > 3 and abs(pts[0][0] - pts[-1][0]) < 1e-6
                and abs(pts[0][1] - pts[-1][1]) < 1e-6)
        if loop: pts = pts[:-1]
        tm = true_min(pts, loop)
        if tm is not None:
            info.append("%s %.2f획" % (nm, tm / WC))
            if tm < WC: msgs.append("%s 최단 실제 변 %.2f획 < 1.0" % (nm, tm / WC))
        x0, y0, x1, y1 = bounds(pts)
        if max(x1 - x0, y1 - y0) < 1.5 * WC:
            msgs.append("%s 잉크 사각형 %.2f획 < 1.5" % (nm, max(x1 - x0, y1 - y0) / WC))
        if x0 < LO or y0 < LO or x1 > HI or y1 > HI: msgs.append("%s 상자 밖" % nm)
    print("  %s %-22s 도형%d 보조색%d | %s"
          % ("✗" if msgs else "✓", name, len(parts), acc, " · ".join(info)))
    for m in msgs: print("      - " + m); bad += 1
print("╚══ 위반 %d건 ══╝" % bad)
