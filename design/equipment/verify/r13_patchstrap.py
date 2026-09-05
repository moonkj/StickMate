# -*- coding: utf-8 -*-
"""R13-P4 — 안대 끈(`PatchStrap`) 각도 처방을 **거울 위에 얹고** verify.py 전량을 다시 돌린다.

배경: R13-P1이 드러난 눈을 아몬드(반높이 0.24R) -> 원반(r=0.33R)으로 바꿨다. 눈이 위아래로
0.09R씩 자랐고, 그 0.09R이 그대로 끈의 여유를 먹었다 — 끈-눈 간격 1.57획 -> **1.31획**(문턱 1.5획).
숫자가 맞는다: 0.09R / 0.34386R = 0.26획 = 1.57 - 1.31.

거울(`items.py`)을 직접 고치지 않는 이유는 `r10_install.py`와 같다 — 거울은 프로덕션을 비추는
물건이고 `mirrordrift.py`가 어긋남 0건을 지킨다. 프로덕션 `.cs`가 바뀌면 거울이 따라가고
그때 이 파일은 역할이 끝난다.

    python3 r13_patchstrap.py             # 처방 적용 후 verify.py 전량
    python3 r13_patchstrap.py --baseline  # 처방 없이(기준선) — 위반 1건이 나와야 한다
    python3 r13_patchstrap.py --sweep     # 각도 축 전체(왜 111도인가)
    python3 r13_patchstrap.py --control   # 음성 대조: 116도를 넣으면 실제로 빨개지는가
"""
import math, os, sys, subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

# ── 처방 ────────────────────────────────────────────────────────────────
PATCH_STRAP_DEGREES = 111.0        # AccessoryShapeBuilder.PatchStrapDegrees : 122 -> 111
PATCH_STRAP_REACH   = 1.02         # PatchStrapReachRatio — **건드리지 않는다**


def _tip(deg, reach):
    a = math.radians(deg)
    return (round(math.cos(a) * reach, 5), round(math.sin(a) * reach, 5))


def install(deg=PATCH_STRAP_DEGREES, reach=PATCH_STRAP_REACH):
    import items
    sh = items.EYES["안대"]
    cover = [s for s in sh if s.name == "PatchCover"][0].pts
    tb, bb = cover[0], cover[3]
    up = _tip(deg, reach)
    for s in sh:
        if s.name == "PatchStrap":
            s.pts = [up, tb, bb, (up[0], -up[1])]
    return up


# ── 축 계산(처방의 근거) ────────────────────────────────────────────────
def _seg_pt(a, b, p):
    dx, dy = b[0] - a[0], b[1] - a[1]
    L = dx * dx + dy * dy
    t = 0.0 if L < 1e-15 else max(0.0, min(1.0, ((p[0]-a[0])*dx + (p[1]-a[1])*dy) / L))
    return math.hypot(p[0] - (a[0] + dx*t), p[1] - (a[1] + dy*t))


def _seg_seg(a, b, c, d):
    return min(_seg_pt(a, b, c), _seg_pt(a, b, d), _seg_pt(c, d, a), _seg_pt(c, d, b))


def exact_gap(line, poly):
    """열린 선 <-> 폴리곤 **정확** 최소거리. `rig.stroke_gap`은 선을 21점으로 표본하므로
    참값을 위로 놓칠 수 있다. 처방을 문턱 근처에서 고르는 중이라 여기서는 정확값을 쓴다.
    (실측: 이 도형에서 두 값의 차 < 1e-3 R — 표본이 충분했다는 **양성 대조**다.)"""
    return min(_seg_seg(line[i], line[i+1], poly[k], poly[(k+1) % len(poly)])
               for i in range(len(line) - 1) for k in range(len(poly)))


def sweep():
    import rig, items
    W = rig.W
    sh = items.EYES["안대"]
    eye = [s for s in sh if s.name == "PatchEye"][0].pts
    cover = [s for s in sh if s.name == "PatchCover"][0].pts
    tb, bb = cover[0], cover[3]

    def gap(deg, reach=PATCH_STRAP_REACH):
        up = _tip(deg, reach)
        return exact_gap([up, tb, bb, (up[0], -up[1])], eye)

    ceil = exact_gap([tb, bb], eye)
    print("╔══ 안대 끈 각도 축 (배율 0.75, W = %.5f R, 문턱 1.5W = %.5f R) ══╗" % (W, 1.5 * W))
    print("  ★ 천장 %.4f R = %.4f 획 — **천 뒤변(PatchCover)** 이 만든다. 끈 각도로는 못 넘는다." % (ceil, ceil / W))
    print("  각도   끝점             끈-눈 간격      판정")
    for deg in list(range(104, 127, 2)):
        g = gap(deg)
        x, y = _tip(deg, PATCH_STRAP_REACH)
        print("  %3d   (%+.3f,%+.3f)   %.4f R = %.3f 획  %s%s"
              % (deg, x, y, g, g / W,
                 "OK" if g >= 1.5 * W else "✗",
                 "  ← 천장" if g >= ceil - 1e-9 else ""))
    lo, hi = 104.0, 121.0
    for _ in range(60):
        m = (lo + hi) / 2
        lo, hi = (m, hi) if gap(m) >= ceil - 1e-12 else (lo, m)
    corner = lo
    lo, hi = 104.0, 130.0
    for _ in range(60):
        m = (lo + hi) / 2
        lo, hi = (m, hi) if gap(m) >= 1.5 * W else (lo, m)
    print("  ── 천장을 유지하는 최대 각도 = %.3f°   /   문턱을 지키는 최대 각도 = %.3f°" % (corner, lo))
    print("  ── 처방 %.0f°: %.4f 획 (문턱 대비 여유 %+.1f%%)"
          % (PATCH_STRAP_DEGREES, gap(PATCH_STRAP_DEGREES) / W,
             (gap(PATCH_STRAP_DEGREES) / (1.5 * W) - 1) * 100))
    print("  ── 반경 축은 레버가 아니다: reach 1.02/1.06/1.10 에서 천장 유지 최대각 %.2f / %.2f / %.2f°"
          % tuple(_corner(gap_r(tb, bb, eye, r), ceil) for r in (1.02, 1.06, 1.10)))
    print("     (0.04 반경당 1°. 끝점을 머리 밖으로 더 내보내는 대가가 각도 1°다 — 남는 장사가 아니다.)")
    # 착용/카드 두 크기에서 「얼마나 움직이는가」
    R075 = 15.5 * 0.75 / 2.0                      # 머리 반지름 pt (지름 11.6pt)
    a, b = _tip(122, PATCH_STRAP_REACH), _tip(PATCH_STRAP_DEGREES, PATCH_STRAP_REACH)
    d = math.dist(a, b)
    pts = [p for s in sh for p in s.pts]
    span = max(max(p[0] for p in pts) - min(p[0] for p in pts),
               max(p[1] for p in pts) - min(p[1] for p in pts))
    print("  ── 끝점 이동량: %.3f R = **%.2f pt**(착용 0.75, 획 폭 2.00pt) = %.1f px(카드 44px)"
          % (d, d * R075, d * 44.0 * 0.86 / span))
    print("     ⇒ 착용 화면에서 **획 하나보다 작다**. 카드에서만 보이고, 보이는 것은 끈의 기울기다.")
    print("╚" + "═" * 62 + "╝\n")


def gap_r(tb, bb, eye, reach):
    def g(deg):
        up = _tip(deg, reach)
        return exact_gap([up, tb, bb, (up[0], -up[1])], eye)
    return g


def _corner(gfn, ceil):
    lo, hi = 104.0, 121.0
    for _ in range(60):
        m = (lo + hi) / 2
        lo, hi = (m, hi) if gfn(m) >= ceil - 1e-12 else (lo, m)
    return lo


if __name__ == "__main__":
    if "--sweep" in sys.argv:
        sweep()
        sys.exit(0)
    deg = None
    if "--control" in sys.argv:
        deg = 116.0                      # 음성 대조: 문턱을 아슬하게 못 넘는 값
    elif "--baseline" not in sys.argv:
        deg = PATCH_STRAP_DEGREES
    if deg is not None:
        up = install(deg)
        print("★ 처방 적용: PatchStrapDegrees %.0f° (reach %.2f) -> 끝점 (%+.5f, %+.5f)%s\n"
              % (deg, PATCH_STRAP_REACH, up[0], up[1],
                 "   ※ --control (일부러 틀린 값)" if "--control" in sys.argv else ""))
    else:
        print("★ 기준선(처방 없음) — 현행 122°\n")
    src = open(os.path.join(HERE, "verify.py"), encoding="utf-8").read()
    g = {"__name__": "__main__", "__file__": os.path.join(HERE, "verify.py")}
    os.chdir(HERE)
    exec(compile(src, "verify.py", "exec"), g)
