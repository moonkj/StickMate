# -*- coding: utf-8 -*-
"""★ 「모자 밑에서 살아남는 EYES 대역」.
eyesunderhat.py 결과: 현행 안경 6종은 모자와 함께 쓰면 평균 6.4%(@0.75)만 남는다.
유일하게 사정이 나은 것이 고글(22.3%)이고, 이유는 하나다 — **끈이 |x|=1.52R까지 나간다.**
그럼 모자 잉크는 각 y에서 |x| 얼마까지 오는가. 그 바깥이 EYES의 생존 대역이다."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, headroom as H
from rig import Shape

HATS = {"야구모자": items.cap, "털모자": items.beanie, "중절모": items.fedora,
        "왕관": items.crown_hat, "베레모": items.beret, "밀짚모자": items.straw}


def hat_xmax(sh, y, w):
    sp = H._merge(H.ink_spans(sh, y, w))
    if not sp: return None
    return max(max(abs(a), abs(b)) for a, b in sp), min(a for a, b in sp), max(b for a, b in sp)


def run(scale):
    w = H.stroke_in_R(scale)
    print("\n╔══ 배율 %.2f  W=%.4f R ══╗" % (scale, w))
    # 교정: 반경 1R 원판의 y=0 에서 x범위는 [-1,1] (획 팽창 포함 1+W/2)
    disc = [Shape("D", [(rig.polar(d, 1.0)) for d in range(0, 360, 5)], True, True)]
    m = hat_xmax(disc, 0.0, w)
    exp = 1.0 + w / 2
    print("  [교정] 반경1R 원판 y=0 -> |x|max %.4f (기대 %.4f)  %s"
          % (m[0], exp, "OK" if abs(m[0] - exp) < 0.02 else "FAIL"))
    if abs(m[0] - exp) > 0.02: sys.exit(1)

    ys = [0.60, 0.40, 0.20, 0.09, 0.00, -0.20, -0.40]
    print("\n  각 y에서 모자 잉크가 닿는 최대 |x| (이 값보다 **바깥**의 EYES 잉크는 안 가려진다)")
    print("   y      " + "".join("%9s" % h for h in HATS) + "   ★최대(최악)")
    env = {}
    for y in ys:
        row = []
        for h, f in HATS.items():
            m = hat_xmax(f(), y, w)
            row.append(m[0] if m else None)
        vals = [v for v in row if v is not None]
        env[y] = max(vals) if vals else 0.0
        print("  %+.2f  " % y + "".join(("%9.3f" % v) if v is not None else "%9s" % "—" for v in row)
              + "   %8.3f" % env[y])
    print("\n  안경선(GlassesLocalY) y=+0.09 에서 최악 모자 도달 |x| = %.3f R" % env[0.09])
    print("  → 팩 EYES의 **식별 잉크**는 |x| ≥ %.3f R 에 두어야 6/6 상태에서 살아남는다." % env[0.09])
    print("  참고 상한: EYES 게이트 |x| ≤ 1.60R (packcap r_cap(EYES,0°))  ·  머리 반경 1.00R")
    print("  → 쓸 수 있는 대역 폭 = %.3f R = %.2f 획" % (1.60 - env[0.09], (1.60 - env[0.09]) / w))
    return env


for s in (0.75, 0.60):
    run(s)
