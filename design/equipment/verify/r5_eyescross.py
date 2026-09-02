# -*- coding: utf-8 -*-
"""★ 교차 대조 — 「모자 밑 안경 생존율」이 두 팀에서 다르게 나왔다.

  design-equipment(나)  36조합 최악 **0.0%**  (선글라스 6종평균 6.4%@0.75)
  design-character      36조합 대역 **5 ~ 47%** (최악 중절모+동그란안경 5%)

같은 대상을 두 자로 쟀다. **어느 쪽이 틀린 게 아니라 무엇을 다르게 쟀는지**를 가른다.
가설 4개를 하나씩 켜고 끄면서 상대 숫자에 도달하는지 본다.

  H1 좌표가 다르다            -> mirrordrift.py 0건으로 이미 기각(설계거울 == 프로덕션)
  H2 분자·분모에 **획 잉크**를 넣는가 (나: 채움 내부만 / 그쪽: 획 팽창 포함 마스크)
  H3 **창으로 자르는가**       (그쪽: x[-1.80,1.80] y[-1.45,1.15])
  H4 채움 없는 도형(체인·끈)을 세는가

  python3 r5_eyescross.py
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, headroom as H
from rig import Shape
INF = float("inf")

HATS = {"야구모자": items.cap, "털모자": items.beanie, "중절모": items.fedora,
        "왕관": items.crown_hat, "베레모": items.beret, "밀짚모자": items.straw}
EYE = items.EYES
X0, X1, Y0, Y1 = -1.80, 1.80, -1.45, 1.15      # design-character r3_combo.py 의 창


# ── 자 A : 내 것 — 채움 다각형 내부 면적만 (획 halo 없음, 창 없음) ───────────────
def ruler_fill(sh, occ, w):
    b = H.hair_visible_area(sh, [], INF, w)
    return None if b <= 1e-9 else H.hair_visible_area(sh, occ, INF, w) / b


# ── 자 B : 그쪽 것 — **획 팽창 포함 잉크 마스크**를 창 안에서 픽셀로 센다 ────────
def mask_ratio(sh, occ, w, h=0.004, clip=True, use_open=True):
    """sh 의 잉크(채움+획 W/2 팽창) 중 occ 잉크에 안 덮인 비율. 격자 적분."""
    xs0, xs1, ys0, ys1 = (X0, X1, Y0, Y1) if clip else (-4.0, 4.0, -4.0, 4.0)
    sh2 = sh if use_open else [s for s in sh if s.filled]
    tot = vis = 0
    ny = int((ys1 - ys0) / h)
    for j in range(ny):
        y = ys0 + (j + 0.5) * h
        spans = H.ink_spans(sh2, y, w)
        if not spans: continue
        cov = H.ink_spans(occ, y, w) if occ else []
        for a, b in spans:
            a = max(a, xs0); b = min(b, xs1)
            if b <= a: continue
            tot += (b - a)
            cur = [(a, b)]
            for ca, cb in cov:
                nxt = []
                for p, q in cur:
                    if cb <= p or ca >= q: nxt.append((p, q)); continue
                    if ca > p: nxt.append((p, ca))
                    if cb < q: nxt.append((cb, q))
                cur = nxt
            vis += sum(q - p for p, q in cur)
    return None if tot <= 0 else vis / tot


def main():
    w = H.stroke_in_R(0.75)
    print("╔══ 교정 ══╗")
    big = [Shape("Big", [(-40, -40), (40, -40), (40, 40), (-40, 40)], True, True)]
    far = [Shape(s.name, [(x, y + 40) for x, y in s.pts], s.loop, s.filled) for s in items.cap()]
    sg = EYE["선글라스"]() if callable(EYE["선글라스"]) else EYE["선글라스"]
    for nm, fn in (("자A 채움면적", ruler_fill), ("자B 잉크마스크", lambda a, b, c: mask_ratio(a, b, c))):
        v1 = fn(sg, far, w); v0 = fn(sg, big, w)
        print("  %-12s 멀리치운모자 %.4f(기대 1.0) · 전부덮음 %.4f(기대 0.0)  %s"
              % (nm, v1, v0, "OK" if abs(v1 - 1) < 2e-3 and v0 < 2e-3 else "FAIL"))
    print("╚══════════╝\n")

    names = list(HATS)
    rows = [("A 채움면적(내 pack_fit·eyesunderhat)", lambda s, o: ruler_fill(s, o, w)),
            ("B 잉크마스크+창 (design-character 식)", lambda s, o: mask_ratio(s, o, w, clip=True)),
            ("B' 잉크마스크·창 없음",                 lambda s, o: mask_ratio(s, o, w, clip=False)),
            ("B'' 잉크마스크·채움도형만",             lambda s, o: mask_ratio(s, o, w, clip=True, use_open=False))]
    allmin = {}
    for label, f in rows:
        print("── %s ──" % label)
        print("  %-11s" % "" + "".join("%9s" % h for h in names) + "   평균")
        cells_all = []
        for en in EYE:
            sh = EYE[en]() if callable(EYE[en]) else EYE[en]
            cells = [f(sh, HATS[h]()) for h in names]
            if any(c is None for c in cells):
                print("  %-11s (측정 불가)" % en); continue
            cells_all += cells
            print("  %-11s" % en + "".join("%8.1f%%" % (c * 100) for c in cells)
                  + " %7.1f%%" % (sum(cells) / len(cells) * 100))
        allmin[label] = (min(cells_all), max(cells_all), sum(cells_all) / len(cells_all))
        print("  ★ 36조합 대역 %.1f%% ~ %.1f%%   평균 %.1f%%\n"
              % (allmin[label][0] * 100, allmin[label][1] * 100, allmin[label][2] * 100))

    print("╔══ 판정 ══╗")
    print("  design-character 보고: 대역 5%% ~ 47%%")
    for k, (lo, hi, av) in allmin.items():
        print("  %-38s 대역 %5.1f%% ~ %5.1f%%  평균 %5.1f%%" % (k, lo * 100, hi * 100, av * 100))


if __name__ == "__main__":
    main()
