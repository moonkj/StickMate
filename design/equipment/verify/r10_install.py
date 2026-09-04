# -*- coding: utf-8 -*-
"""R10 처방을 **설계 거울 위에 얹고** verify.py 전량을 다시 돌린다.

거울(`items.py`)을 직접 고치지 않는 이유: 거울은 **프로덕션을 비추는 물건**이고
`mirrordrift.py`가 0건을 지킨다. 프로덕션 `.cs`가 바뀌는 순간 거울이 따라가고,
그때 이 파일은 역할이 끝난다(스펙 14-1의 `r9_bandfix.py`와 같은 규약).

    python3 r10_install.py              # 처방 적용 후 verify.py 전량
    python3 r10_install.py --baseline   # 처방 없이 (기준선)
"""
import os, sys, subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

# ── 처방 (r10_rx.py 판정) ──────────────────────────────────────────────
LONGCAPE_FOLD2_END = 0.84          # endRatioOverride 0.96 -> 0.84
LONGCAPE_FOLD1_END = 0.60          # (완전 처방) 0.80 -> 0.60. None 이면 안 건드린다
BEANIE_CUFF_HEIGHT = 0.54          # 낱선 -> 채운 사다리꼴, 밑단 위 높이 R


def install():
    import rig, items
    trail = 3.10                                  # LongCapeSpreadRatio
    sh = items.BACK["긴망토"]
    for s in sh:
        if s.name == "CapeFold2":
            s.pts = [s.pts[0], (-trail * LONGCAPE_FOLD2_END, s.pts[1][1])]
        if s.name == "CapeFold" and LONGCAPE_FOLD1_END:
            s.pts = [s.pts[0], (-trail * LONGCAPE_FOLD1_END, s.pts[1][1])]

    bn = items.HEAD["털모자"]
    crown = [s for s in bn if s.name == "BeanieCrown"][0].pts
    hem, half = -0.26, 0.56
    ytop = round(hem + BEANIE_CUFF_HEIGHT, 6)
    xa = _x_at(crown, ytop, -1); xb = _x_at(crown, ytop, +1)
    for s in bn:
        if s.name == "BeanieCuff":
            s.pts = [(-half, hem), (xa, ytop), (xb, ytop), (half, hem)]
            s.loop = True
            s.filled = True                       # tone 은 2(Shade) 그대로


def _x_at(poly, y, sign):
    best = None
    n = len(poly)
    for i in range(n):
        p, q = poly[i], poly[(i + 1) % n]
        if (p[1] > y) != (q[1] > y):
            x = p[0] + (y - p[1]) * (q[0] - p[0]) / (q[1] - p[1])
            if sign * x > 0 and (best is None or sign * x > sign * best): best = x
    return best


if __name__ == "__main__":
    if "--baseline" not in sys.argv:
        install()
        print("★ R10 처방 적용: 긴망토 CapeFold2 e=%.2f%s · 털모자 BeanieCuff 채운 사다리꼴 h=%.2f\n"
              % (LONGCAPE_FOLD2_END,
                 (" / CapeFold e=%.2f" % LONGCAPE_FOLD1_END) if LONGCAPE_FOLD1_END else "",
                 BEANIE_CUFF_HEIGHT))
    else:
        print("★ 기준선 (처방 없음)\n")
    exec(compile(open(os.path.join(HERE, "verify.py"), encoding="utf-8").read(),
                 "verify.py", "exec"), {"__name__": "__main__"})
