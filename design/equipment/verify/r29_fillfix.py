# -*- coding: utf-8 -*-
"""r29c — §6-4 처방(나)의 좌표를 **실제로 재서** 얻는다. R28 문서/하니스는 건드리지 않는다.
    python3 r29_fillfix.py
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r28_props as P, rig

W = P.WP
LINE_LINE = 2.00 * W
FILL_LINE = 2.60 * W          # (2.2+1.0)/2 + 1.0  — CostumePropRenderer.cs:278 의 2.2배 재묘사
d = lambda a, b: P.shape_dist(a, b)[0]
need = lambda a, b: FILL_LINE if (a.filled or b.filled) else LINE_LINE

print("=" * 96)
print("  r29c — 채움 인지 하한(2.60 W_P)에서 깨지는 R28 좌표 2건의 수정안, 실측")
print("=" * 96)

# ── C3 광부 : Crack1 을 옮긴다 (최대 여유 탐색) ──────────────────────────────
m = {s.name: s for s in P.miner()}
print("\n[C3 광부] 현재 Vein1 x Crack1 = %.4f H = %.2f W_P  (하한 2.60) -> 위반"
      % (d(m['Vein1'], m['Crack1']), d(m['Vein1'], m['Crack1']) / W))
best = (-9, None)
for k in range(45):
    y = 0.8300 + 0.0025 * k
    for j in range(26):
        x0 = 0.3600 + 0.0050 * j
        for L in (0.0600, 0.0700, 0.0800):
            c = P.S('Crack1', [(x0, y), (x0 + L, y)])
            sc, okall = 9e9, True
            for n in m:
                if n == 'Crack1': continue
                dd = d(m[n], c)
                if dd <= 1e-9: okall = False; break
                sc = min(sc, dd / need(m[n], c))
            if okall and sc > best[0]: best = (sc, (x0, y, L))
x0, y, L = best[1]
c = P.S('Crack1', [(x0, y), (x0 + L, y)])
print("  최적해 score=%.3f  Crack1 (%+.4f,%+.4f) (%+.4f,%+.4f)  길이 %.4f H = %.2f W_P"
      % (best[0], x0, y, x0 + L, y, L, L / W))
for n in ('Vein1', 'RockFace', 'Vein2'):
    print("     x %-9s %.4f H = %.2f W_P (하한 %.2f)" % (n, d(m[n], c), d(m[n], c) / W, need(m[n], c) / W))
print("  ★ 여유가 0.7%%뿐이다 — 벽 안쪽에 가로획 하나가 겨우 들어가는 것이 상한이다.")

# ── C4 대마법사 : Ring 3개를 등간격으로 내린다 ───────────────────────────────
a = {s.name: s for s in P.archmage()}
x0_, y0_, x1_, y1_ = rig.bounds(a['StaffMoon'].pts)
print("\n[C4 대마법사] StaffMoon 실측 bounds x[%.4f,%.4f] y[%.4f,%.4f]  ← 밑끝은 %.4f 다(초안에서 0.8920 으로 잘못 짚었다)"
      % (x0_, x1_, y0_, y1_, y0_))
print("  현재 StaffMoon x Ring1 = %.4f H = %.2f W_P (하한 2.60) -> 위반"
      % (d(a['StaffMoon'], a['Ring1']), d(a['StaffMoon'], a['Ring1']) / W))
ring = lambda cy, nm: P.S(nm, rig.poly(1.0400, cy, 0.0500, 12), loop=True)
G, y = 0.1700, 0.7400
while y > 0.55:
    R1, R2, R3 = ring(y, 'Ring1'), ring(y - G, 'Ring2'), ring(y - 2 * G, 'Ring3')
    if (d(a['StaffMoon'], R1) >= FILL_LINE and d(R1, R2) >= LINE_LINE and d(R2, R3) >= LINE_LINE):
        break
    y -= 0.0025
R1, R2, R3 = ring(y, 'Ring1'), ring(y - G, 'Ring2'), ring(y - 2 * G, 'Ring3')
print("  수정  Ring 중심 y = %.4f / %.4f / %.4f  (등간격 %.3f)" % (y, y - G, y - 2 * G, G))
print("     StaffMoon x Ring1 %.4f H = %.2f W_P (하한 2.60)" % (d(a['StaffMoon'], R1), d(a['StaffMoon'], R1) / W))
print("     Ring1 x Ring2     %.4f H = %.2f W_P (하한 2.00)" % (d(R1, R2), d(R1, R2) / W))
print("     Ring2 x Ring3     %.4f H = %.2f W_P (하한 2.00)" % (d(R2, R3), d(R2, R3) / W))
print("     Ring3 x CradleA   %.4f H = %.2f W_P" % (d(R3, a['CradleA']), d(R3, a['CradleA']) / W))
print("     Ring3 x Staff     %.4f H  (0 = 지팡이에 꿰여 있다)" % d(R3, a['Staff']))
print("     Ring3 최하점 y    %.4f  (접지선 위: %s)" % (min(p[1] for p in R3.pts),
                                                  "예" if min(p[1] for p in R3.pts) > 0 else "아니오"))
