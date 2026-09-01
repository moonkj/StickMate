# -*- coding: utf-8 -*-
"""털모자 후보 좌표 비교 — 2026-09-02 사용자 신고 "털모자 착용시 거의 머리 전체를 가림".

세 요구가 서로 당긴다:
  (1) 얕게 써야 한다               → 밑단을 올린다
  (2) 배율 0.60에서 옆벽이 안 뭉갠다 → 옆벽 ≥ 1.0 W(0.60) = 0.4298R
  (3) 커버선(±0.96, −0.06)은 못 움직인다 → HatCoverLocalY · 머리카락 자르기가 걸려 있다

★ 제3의 축 — **단의 윗변은 커버선일 필요가 없다.**
  옆벽 길이 = 밑단 y − **단 윗변 y**이지 밑단 y − 커버선 y가 아니다. 접힌 단(cuff)은 실물에서도
  관 위로 **타고 올라간다**. 단 윗변을 커버선 위(+0.30R)로 올리면 밑단을 −0.40R까지 끌어올려도
  옆벽이 0.6048R(= 배율 0.60에서 1.41획)로 오히려 지금(1.13획)보다 길어진다.
  · 관 밑변(커버선)은 그대로 −0.06R → HatCoverLocalY · 머리카락 자르기 무변경
  · 단 윗변의 x는 **관 옆변 위의 보간점**이라 좌표를 새로 적지 않는다(규칙 4-a)
  · 단과 관은 같은 primary 채움이라 겹쳐도 그림에 이음매가 생기지 않는다
"""
import sys, math
sys.path.insert(0, '.')
import rig, items, hair, headroom as H
from rig import Shape

CROWN = [(-0.96, -0.06), (-1.06, 0.52), (-0.62, 1.16), (0.00, 1.32),
         (0.62, 1.14), (1.06, 0.50), (0.96, -0.06)]
POM = rig.poly(-0.10, 1.44, 0.28, 10, 90.0)


def crown_edge_x(y):
    """관 앞 옆변(0.96,−0.06)→(1.06,0.50) 위에서 높이 y의 x. 단 윗변이 여기 얹힌다."""
    (x0, y0), (x1, y1) = (0.96, -0.06), (1.06, 0.50)
    t = (y - y0) / (y1 - y0)
    return x0 + (x1 - x0) * t


def cuff_flat(top_y, flare_hw, flare_y, hem_hw, hem_y):
    """단 윗변을 커버선에 두는 옛 방식(top_y = −0.06)과 관 위로 올리는 새 방식 둘 다 이 함수로."""
    tx = crown_edge_x(top_y) if top_y > -0.06 else 0.96
    return [(-tx, top_y), (tx, top_y), (flare_hw, flare_y), (hem_hw, hem_y),
            (-hem_hw, hem_y), (-flare_hw, flare_y)]


def build(cuff_pts, crown=CROWN, pom=POM):
    return [Shape("BeanieCrown", crown, filled=True),
            Shape("BeanieCuff", cuff_pts, filled=True),
            Shape("BeaniePom", pom, filled=True, tone=1)]


CAND = {
    "현행 −0.64":  build(cuff_flat(-0.06, 1.04, -0.54, 0.64, -0.64)),
    "회귀전 −0.52": build(cuff_flat(-0.06, 1.00, -0.42, 0.64, -0.52)),
    "안A 단−0.34": build(cuff_flat(0.30, 1.10, -0.24, 0.70, -0.34)),
    "안B 단−0.40": build(cuff_flat(0.30, 1.10, -0.30, 0.70, -0.40)),
    "안C 단−0.46": build(cuff_flat(0.30, 1.10, -0.36, 0.70, -0.46)),
    "안D 단없음":   [Shape("BeanieCrown", CROWN, filled=True),
                   Shape("BeaniePom", POM, filled=True, tone=1)],
    # ★ 풀이 2 — 사다리꼴 단. 커버선 변을 그대로 공유하고(겹침 0) 수평 성분을 **안쪽으로** 써서
    #   옆벽을 늘린다. 플레어(보이지 않는 0.08R 허리 파임)는 지웠다.
    "안E 사다리−0.40": build([(-0.96,-0.06),(0.96,-0.06),(0.58,-0.40),(-0.58,-0.40)]),
    # ★ 확정 권고 (스펙 13-6). 옆벽 0.50478R = 배율 0.60에서 1.174획(현행 1.132획보다 길다).
    "★확정 −0.34":  build([(-0.96,-0.06),(0.96,-0.06),(0.54,-0.34),(-0.54,-0.34)]),
    "안G 사다리−0.30": build([(-0.96,-0.06),(0.96,-0.06),(0.62,-0.30),(-0.62,-0.30)]),
}


def rule1(shapes, scale):
    w = H.stroke_in_R(scale)
    return [(s.name, rig.rule_one(s, w)) for s in shapes if rig.rule_one(s, w)]


def side_wall(cuff):
    return math.dist(cuff[1], cuff[2])


def report():
    w75, w60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)
    print("╔══ 털모자 후보 (배율 0.75 W=%.4fR / 0.60 W=%.4fR) ══╗" % (w75, w60))
    print("  %-12s %7s %7s %7s %7s | %8s %6s | %s" %
          ("후보", "두께획", "면적%", "호°", "잉크밑", "옆벽R", "0.60획", "규칙1위반(0.75/0.60)"))
    for n, sh in CAND.items():
        m = H.measure(sh, w75)
        thick = m['depth'] * 2.0 / w75
        cuff = [s for s in sh if s.name == "BeanieCuff"]
        sw = side_wall(cuff[0].pts) if cuff else float('nan')
        v75, v60 = rule1(sh, 0.75), rule1(sh, 0.60)
        print("  %-12s %6.2f획 %6.1f%% %6.1f %7.3f | %8.4f %6.2f | %d / %d %s" %
              (n, thick, m['area'] * 100, m['arc'], m['ink_bottom'], sw, sw / w60,
               len(v75), len(v60), "  " + "; ".join("%s %s" % t for t in v60) if v60 else ""))
    print()

    # 다른 모자 5종 대조
    print("  [대조] 다른 모자 (배율 0.75)")
    for n, sh in items.HEAD.items():
        if n == "털모자": continue
        m = H.measure(sh, w75)
        print("    %-8s 두께 %.2f획  면적 %.1f%%  호 %.1f°  잉크밑 %.3f" %
              (n, m['depth'] * 2.0 / w75, m['area'] * 100, m['arc'], m['ink_bottom']))
    print()

    # 실루엣 쌍별 차 — 후보를 털모자 자리에 끼워 넣고 6종 15쌍을 다시 잰다
    print("  [실루엣] 후보를 넣었을 때 HEAD 6종 15쌍 최소 차 (하한 1.00획)")
    for n, sh in CAND.items():
        d = dict(items.HEAD); d["털모자"] = sh
        ks = list(d); pr = {k: rig.profile(d[k], 0.0) for k in ks}
        worst = (None, 99)
        for i in range(len(ks)):
            for j in range(i + 1, len(ks)):
                v = rig.max_delta(pr[ks[i]], pr[ks[j]]) / w75
                if v < worst[1]: worst = ((ks[i], ks[j]), v)
        # 털모자가 낀 쌍만의 최악
        bw = (None, 99)
        for k in ks:
            if k == "털모자": continue
            v = rig.max_delta(pr["털모자"], pr[k]) / w75
            if v < bw[1]: bw = (k, v)
        print("    %-12s 전체최소 %.2f획 (%s vs %s) | 털모자 최악 상대 %s %.2f획" %
              (n, worst[1], worst[0][0], worst[0][1], bw[0], bw[1]))
    print()

    # 머리카락 — 모자를 쓴 뒤 남는 면적
    print("  [머리카락] 커버선 −0.06R에서 자른 뒤 모자 잉크에 안 가려지고 남는 면적 (R², 배율 0.75)")
    print("    %-12s %s" % ("후보", "  ".join("%-6s" % k for k in hair.SET)))
    base = {}
    for hn, hs in hair.SET.items():
        base[hn] = H.hair_visible_area(hs, [], float('inf'), w75)
    print("    %-12s %s" % ("모자 안 씀", "  ".join("%6.3f" % base[k] for k in hair.SET)))
    for n, sh in CAND.items():
        vals = [H.hair_visible_area(hair.SET[k], sh, -0.06, w75) for k in hair.SET]
        tot = sum(vals); bt = sum(base.values())
        print("    %-12s %s   합 %.3f (미착용의 %.0f%%)" %
              (n, "  ".join("%6.3f" % v for v in vals), tot, 100 * tot / bt))
    print("╚═════════════════════════════════════════════╝")


if __name__ == "__main__":
    report()
