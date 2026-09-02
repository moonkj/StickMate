# -*- coding: utf-8 -*-
"""★ 「모자를 쓰면 안경이 얼마나 남는가」 — 팩은 HEAD와 EYES를 **둘 다** 반드시 포함한다(DS-2).
   즉 팩이 파는 대표 상태(6/6)에서 두 물건이 같은 세로 구간을 두고 다툰다. 그 다툼을 잰다.
   레이어: SortEyes=8 < SortHead=10 이므로 모자가 안경 위다. 안경은 **잘리지 않고 가려지기만** 한다."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, headroom as H
from rig import Shape
INF = float("inf")

HATS = {"야구모자": items.cap, "털모자": items.beanie, "중절모": items.fedora,
        "왕관": items.crown_hat, "베레모": items.beret, "밀짚모자": items.straw}
EYES = {"선글라스": items.sunglasses if hasattr(items,'sunglasses') else None}
# items 모듈의 EYES 딕셔너리를 직접 쓴다
EYESET = items.EYES


def visible_ratio(shapes, occ, w):
    """가려지지 않고 남는 채움 잉크 면적 비율. 자르기는 없다(커버선 +∞)."""
    base = H.hair_visible_area(shapes, [], INF, w)
    if base <= 1e-9: return None
    return H.hair_visible_area(shapes, occ, INF, w) / base


def run(scale):
    w = H.stroke_in_R(scale)
    print("\n╔══ 배율 %.2f  W=%.4f R ══╗" % (scale, w))
    e0 = list(EYESET)[0]
    s0 = EYESET[e0]() if callable(EYESET[e0]) else EYESET[e0]
    far = [Shape(s.name, [(x, y + 40) for x, y in s.pts], s.loop, s.filled) for s in items.cap()]
    a = visible_ratio(s0, far, w); b = visible_ratio(s0, [Shape("Big", [(-40,-40),(40,-40),(40,40),(-40,40)], True, True)], w)
    print("  [교정] 멀리 치운 모자 -> %.6f (기대 1.0) / 전부 덮음 -> %.6f (기대 0.0)  %s"
          % (a, b, "OK" if abs(a-1) < 1e-6 and b < 1e-9 else "FAIL"))
    if abs(a-1) > 1e-6 or b > 1e-9: sys.exit(1)

    names = list(HATS)
    print("\n  %-11s" % "" + "".join("%9s" % h for h in names) + "   ★6종평균")
    for en in EYESET:
        sh = EYESET[en]() if callable(EYESET[en]) else EYESET[en]
        cells = []
        for h in names:
            r = visible_ratio(sh, HATS[h](), w)
            cells.append(r)
        if cells[0] is None:
            print("  %-11s (채운 도형 없음)" % en); continue
        print("  %-11s" % en + "".join("%8.1f%%" % (c*100) for c in cells)
              + "   %7.1f%%" % (sum(cells)/len(cells)*100))

for s in (0.75, 0.60):
    run(s)
