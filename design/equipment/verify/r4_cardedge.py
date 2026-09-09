# -*- coding: utf-8 -*-
"""★ R2/R3 게이트 축 1개의 교정 — 「카드 두 크기 최단변 >= 획」.

R2/R3 는 «최단변»을 `true_min_edge`(꺾임과 무관한 모든 변의 최솟값)로 정의했다.
그 자를 **출하 인계본 12종**(= 지금 화면에 그려지는 물건)에 그대로 대면 어떻게 되는가를 잰다.
음성 대조로 «양끝이 45° 이상 꺾인 변만 보는 자»(= rig.rule_one = 프로덕션 린트의 정의)를 함께 돌린다.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r4_geom as G, rig
import pack78_shapes as P78

SLOT = {'HeadCap': 'HEAD', 'HeadBeanie': 'HEAD', 'HeadFedora': 'HEAD', 'HeadCrown': 'HEAD',
        'EyesSunglasses': 'EYES', 'EyesRound': 'EYES', 'EyesGoggles': 'EYES', 'EyesMonocle': 'EYES',
        'BackCape': 'BACK', 'BackLongCape': 'BACK', 'BackWings': 'BACK', 'BackBackpack': 'BACK'}

def min_edge(pcs, corner_deg=None):
    m, who = 9e9, None
    for p in pcs:
        pts = p["pts"]; n = len(pts)
        if n < 2: continue
        if corner_deg is None:
            rngs = range(n) if p["loop"] else range(n - 1)
            for i in rngs:
                L = math.dist(pts[i], pts[(i + 1) % n])
                if 1e-9 < L < m: m, who = L, p["name"]
        else:
            if n < 3: continue
            corner = [False] * n
            rng = range(n) if p["loop"] else range(1, n - 1)
            for i in rng:
                corner[i] = rig.turn_deg(pts[(i - 1) % n], pts[i], pts[(i + 1) % n]) >= corner_deg
            segs = n if p["loop"] else n - 1
            for i in range(segs):
                j = (i + 1) % n
                if not (corner[i] and corner[j]): continue
                L = math.dist(pts[i], pts[j])
                if L < m: m, who = L, p["name"]
    return m, who

if __name__ == "__main__":
    h = G.load_handoff()
    print("출하 인계본 12종 — R2/R3 게이트의 「카드 두 크기 최단변 >= 획」 축을 그대로 적용")
    print(f"{'아이템':<16}{'슬롯':<6}{'전체최단 58pt':>14}{'획':>7}{'판정':>7}   {'꺾임-꺾임 58pt':>15}{'판정':>7}   {'24pt(꺾임)':>11}{'판정':>7}")
    b_all = b_cor = 0
    for k, it in h.items():
        slot = SLOT[it["slot"]]
        pcs = [p for p in it["pieces"] if p["surfaces"] == 0 or (p["surfaces"] & 2)]
        allp = [q for p in pcs for q in p["pts"]]
        x0, y0, x1, y1 = rig.bounds(allp); span = max(x1 - x0, y1 - y0)
        upr = P78.units_per_R(slot); shrink = min(1.0, P78.ICON_VIEWBOX / (span * upr))
        ea, _ = min_edge(pcs); ec, _ = min_edge(pcs, 45.0)
        out = []
        for e in (ea, ec, ec):
            pass
        r = []
        for size, e in ((58.0, ea), (58.0, ec), (24.0, ec)):
            ptr = upr * shrink * (size / 64.0); st = P78.card_stroke_pt(size)
            r.append((float('inf') if e > 9e8 else e * ptr, st, (e > 9e8) or e * ptr >= st))
        b_all += 0 if r[0][2] else 1
        b_cor += 0 if (r[1][2] and r[2][2]) else 1
        f = lambda t: ("없음" if t[0] == float('inf') else "%.2fpt" % t[0])
        print(f"{k:<16}{slot:<6}{f(r[0]):>14}{r[0][1]:>7.3f}{'OK' if r[0][2] else '★미달':>7}   "
              f"{f(r[1]):>15}{'OK' if r[1][2] else '★미달':>7}   {f(r[2]):>11}{'OK' if r[2][2] else '★미달':>7}")
    print()
    print(f"⇒ 옛 자(모든 변)      : 12종 중 **{b_all}종 미달**  — 즉 이 축은 출하된 물건을 전부 불합격시킨다.")
    print(f"⇒ 새 자(꺾임-꺾임 변) : 12종 중 {b_cor}종 미달  — 프로덕션 린트(rig.rule_one)와 같은 정의다.")
    print()
    print("해석: 매끄러운 곡선은 **정의상** 짧은 변이 많다. 옛 자는 「곡선을 촘촘히 그리는 것」을 금지한다.")
    print("      옛 자가 잡으려던 것(FX/PET 의 «작은 다각형»)은 규칙 1-C(rho_max >= 펜)와")
    print("      「채움 잉크 사각형 >= 1.5획」이 이미 잡으므로, 그 둘은 그대로 두고 이 축만 교체한다.")
