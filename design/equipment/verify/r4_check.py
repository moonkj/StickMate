# -*- coding: utf-8 -*-
"""설계 중인 R4 좌표를 r4_gate 의 자로 바로 잰다(YAML 방출 전 반복용)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r4_geom as G, r4_gate as GT, r4_ink, r4_survive as S

def to_bank(PACKS):
    bank = {}
    for pn, key, motif, P in PACKS:
        for slot in ("HEAD", "EYES", "NECK", "BACK"):
            disp, iid, fn = P[slot]
            pcs = fn()
            pieces = []
            for p in pcs:
                pr = p.params(slot)
                pieces.append(dict(name=p.name, pts=p.pts, loop=p.loop, filled=p.filled, tone=p.tone,
                                   surfaces=1, strokeMult=pr["strokeMult"], noStroke=bool(pr["noStroke"]),
                                   alpha=pr["alpha"], lineAlpha=pr["lineAlpha"], layer=p.layer, bodyFixed=False))
            bank[f"{key}_{slot.lower()}_{disp.replace(' ','_').lower()}"] = dict(ko=disp, slot=slot, pieces=pieces)
    return bank

if __name__ == "__main__":
    mod = sys.argv[1] if len(sys.argv) > 1 else "pack_detail_r4"
    M = __import__(mod)
    bank = to_bank(M.PACKS)
    print(f"# 문턱: 티끌 {GT.TINY_PT2}pt² · 채움<=4 · 얇은판 {GT.THIN_W*G.R_PT_075:.2f}pt · 면오차 {GT.FACET_R}R\n")
    tot = 0
    for k, it in bank.items():
        f, solid, vis = GT.check(it)
        fills = [p for p in G.body_pieces(it) if p["filled"]]
        if len(fills) > 4: f.append(("R4-2", f"채움 조각 {len(fills)}개 > 4"))
        f = [x for x in f if x[0] != "R4-2" or "채움 조각" in x[1]]
        tot += len(f)
        print(f"{k:<34} 채움{len(fills)} 색면 {['%.0f'%v for v in vis]}   {'통과' if not f else '★위반 %d' % len(f)}")
        for a, m in f: print(f"      x {a}  {m}")
    print(f"\n합계 위반 {tot}건")


# ══════════════════════════════════════════════════════════════════════════════
#  ★ R4 축 전용 양성 대조 — 「나쁜 값을 넣으면 실제로 빨개지는가」
#    R3 하니스의 --control 은 R3 좌표를 겨눈 주입이라 R4 기하에서는 T-1/T-4 가 물지 않는다
#    (실측: R4 로 R3 대조를 돌리면 T-1 0건 · T-4 0건). 그래서 R4 축은 자기 대조를 따로 갖는다.
# ══════════════════════════════════════════════════════════════════════════════
def control():
    import copy, math
    import pack_detail_r4 as M
    import r4_gate as GT, r4_geom as G
    bank = to_bank(M.PACKS)
    hits = {}
    def probe(name, key, mutate, axis):
        it = copy.deepcopy(bank[key]); mutate(it["pieces"])
        f, _, _ = GT.check(it)
        fills = [p for p in G.body_pieces(it) if p["filled"]]
        if len(fills) > GT.FILL_MAX: f.append(("R4-2", "채움 %d개" % len(fills)))
        got = sorted(set(a for a, _ in f))
        ok = axis in got
        hits[axis] = hits.get(axis, False) or ok
        print(f"  {'OK ' if ok else '★  '}{name:<52} -> {got if got else '검출 없음'}")
    # ① 티끌 복원 — R3 의 초승달(긴 변 8.6pt · 색면 0.0pt²)을 위저드햇에 되돌린다.
    #    R3 좌표 그대로: crescent(-1.30, 1.10, 0.74, 0.57, 0.30, 200°) · 닫힌 윤곽.
    def add_moon(ps):
        import pack78_shapes as P78
        pts = P78.crescent(-1.30, 1.10, 0.74, 0.57, 0.30, 200.0)
        ps.insert(0, dict(name="R3HatMoon", pts=[tuple(q) for q in pts], loop=True, filled=True, tone=1,
                          surfaces=1, strokeMult=1.0, noStroke=False, alpha=1.0, lineAlpha=1.0,
                          layer=0, bodyFixed=False))
    probe("① 위저드햇에 R3 초승달(긴 변 8.6pt · 색면 0.0pt²)을 되돌린다", "arcane_head_wizard_hat", add_moon, "R4-1")
    # ② 채움 5개 — 로브에 채움 하나 추가
    def add_fill(ps):
        ps.append(dict(name="RobeExtra", pts=[(-0.6, -2.0), (0.6, -2.0), (0.6, -2.8), (-0.6, -2.8)],
                       loop=True, filled=True, tone=0, surfaces=1, strokeMult=1.0, noStroke=True,
                       alpha=1.0, lineAlpha=0.0, layer=0, bodyFixed=False))
    probe("② 로브에 다섯 번째 색면을 더한다", "arcane_back_moon_robe", add_fill, "R4-2")
    # ③ 얇은 판에 닫힌 윤곽 — 위저드햇 띠(짧은 변 3.14pt)의 noStroke 를 끈다
    def restroke(ps):
        for p in ps:
            if p["name"] == "HatBand": p["noStroke"] = False; p["lineAlpha"] = 1.0
    probe("③ 위저드햇 띠(짧은 변 3.14pt)의 noStroke 를 끈다", "arcane_head_wizard_hat", restroke, "R4-3")
    # ④ 곡선을 거칠게 — 램프 렌즈를 22각 -> 8각
    def coarse(ps):
        for p in ps:
            if p["name"] == "LampLens":
                p["pts"] = [(0.58*math.cos(math.radians(360*i/12)), -2.12 + 0.58*math.sin(math.radians(360*i/12))) for i in range(12)]
    probe("④ 램프 렌즈를 22각 -> 12각으로 깎는다", "mine_neck_mine_lamp", coarse, "R4-4")
    print("\n  ★ 양성 대조 판정: " + ("OK — 4축이 전부 실제로 문다" if all(hits.get(a) for a in ("R4-1","R4-2","R4-3","R4-4"))
          else "FAIL — 안 무는 축: " + str([a for a in ("R4-1","R4-2","R4-3","R4-4") if not hits.get(a)])))
    return all(hits.get(a) for a in ("R4-1","R4-2","R4-3","R4-4"))
