# -*- coding: utf-8 -*-
"""FX 6 + PET 6 전수 검산. verify.py와 같은 자(尺)를 쓰되,
**45도 함정**(꺾임이 문턱 아래여서 린트가 통째로 건너뛰는 변)을 별도로 드러낸다."""
import sys, math; sys.path.insert(0,'.')
import rig
from rig import W, Shape, bounds, rule_one, self_intersects, profile, max_delta, stroke_gap
import appearance as A

ICON, FIT = 44.0, 0.86
IST = 1.7 * 44 / 40          # 카드 획(캔버스 유닛 -> 44px 환산), verify.py와 같다

def true_min_edge(sh):
    """꺾임 여부와 무관한 **가장 짧은 실제 변**(획 배수). 눈은 문턱을 모른다."""
    p = sh.pts; n = len(p); best = None
    for i in range(n if sh.loop else n-1):
        L = math.dist(p[i], p[(i+1) % n]) / W
        if L < 1e-9: continue
        best = L if best is None else min(best, L)
    return best

def audit(title, table, skip=(), particle=False):
    print("\n╔══ %s ══╗" % title)
    fails = 0
    for name, sh in table.items():
        if not sh:
            print("  · %-10s (월드 도형 없음 — '없음' 자리)" % name); continue
        msgs = []
        # 규칙 5 정원
        if not particle and not (2 <= len(sh) <= 4): msgs.append("정원 %d개(2~4 밖)" % len(sh))
        # 규칙 3-2 보조색
        acc = sum(1 for s in sh if s.tone == 1)
        if not particle and acc != 1: msgs.append("보조색 %d개(정확히 1개여야)" % acc)
        # 규칙 1
        for s in sh:
            v = rule_one(s, W)
            if v: msgs.append("%s 규칙1: %s" % (s.name, v))
            if s.loop and self_intersects(s.pts): msgs.append("%s 자기교차" % s.name)
        # 45도 함정 — 린트가 못 보는 짧은 변
        for s in sh:
            t = true_min_edge(s)
            if t is not None and t < 1.0 and rule_one(s, W) is None:
                msgs.append("%s ★린트사각: 최단 실제 변 %.2f획(<1.0) — 꺾임이 45도 미만이라 규칙1이 건너뜀" % (s.name, t))
        # 카드 44px
        pts = [p for s in sh for p in s.pts]
        x0,y0,x1,y1 = bounds(pts); span = max(x1-x0, y1-y0)
        if span > 1e-9:
            k = ICON*FIT/span
            for s in sh:
                cs = Shape(s.name, [(x*k, y*k) for x,y in s.pts], s.loop, s.filled, s.tone)
                v = rule_one(cs, IST)
                if v: msgs.append("카드 %s: %s" % (s.name, v))
                t = true_min_edge(Shape(s.name, cs.pts, s.loop))
                # true_min_edge는 W로 나누므로 카드용으로 다시 계산
        # 채움
        notes = [] if any(s.filled for s in sh) else ["(참고) 채움 0개 — 전부 윤곽선"]
        mins = ["%s %.2f획" % (s.name, true_min_edge(s)) for s in sh if true_min_edge(s)]
        print("  %s %-10s 도형%d 보조색%d | 최단변: %s" %
              ("✗" if msgs else "✓", name, len(sh), acc, " · ".join(mins)))
        for m in msgs: print("      - " + m); fails += 1
        for m in notes: print("      · " + m)
    # 쌍별 실루엣 차
    ks = [k for k in table if table[k] and k not in skip]
    if len(ks) >= 2:
        pr = {k: profile(table[k]) for k in ks}
        worst = (None, 99.0)
        for i in range(len(ks)):
            for j in range(i+1, len(ks)):
                v = max_delta(pr[ks[i]], pr[ks[j]]) / W
                if v < worst[1]: worst = ((ks[i], ks[j]), v)
        print("  쌍별 최소 실루엣 차 %.2f획 (%s vs %s)%s" %
              (worst[1], worst[0][0], worst[0][1], "" if worst[1] >= 1.0 else "  ✗ <1.0"))
    print("╚══ 위반 %d건 ══╝" % fails)
    return fails

n = audit("FX 6종 — 현행", A.FX_NOW, particle=True)
n += audit("PET 5종(리틀스틱메이트 제외 = design-character 소관) — 현행", A.PET_NOW)
print("\n총 위반 %d건" % n)

print("\n" + "="*78)
m  = audit("FX 6종 — 제안 A(좌표만)", A.FX_A, particle=True)
m += audit("PET 5종 — 제안 A(좌표만)", A.PET_A)
print("\n제안 A 위반 %d건" % m)
