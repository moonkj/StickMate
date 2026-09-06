# -*- coding: utf-8 -*-
"""R26b — 뒷짐 × 망토: «무엇이 새로운가»를 가르는 두 측정.

R26(r26_capestance.py)이 「겹친다」를 쟀다면 이쪽은 그 겹침이 **어떤 종류**인지를 가른다.

  (가) 실루엣 교차 횟수 — 팔 중심선이 망토 뒤판의 경계를 몇 번 넘는가.
       1회(안→밖)면 «팔이 망토 뒤에서 나온다» = 평소 Idle과 같은 그림.
       3회(안→밖→안)면 «팔이 망토를 나갔다 다시 들어간다» = 손이 천 위에 얹힌다.
  (나) 잉크 돌출/매몰 — 팔꿈치와 손끝의 잉크가 망토 실루엣 밖으로 얼마나 나가는가(R·pt).
  (다) 두 팔 잉크의 **합집합** — 두 팔은 8~12° 차이라 잉크가 대부분 겹친다. 화면에 실제로
       보이는 «망토 위의 검은 면적»은 합이 아니라 합집합이다.

r26_capestance.py 의 추출·기하 함수를 그대로 재사용한다(사본 금지).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r26_capestance as B

def crossings(line, poly, step=0.002):
    d = B.densify(line, step)
    st = [B.contains(poly, p) for p in d]
    xs = [i for i in range(1, len(st)) if st[i] != st[i-1]]
    seq = [("안" if st[0] else "밖")]
    for i in xs:
        seq.append("안" if st[i] else "밖")
    return len(xs), "→".join(seq)

def ink_out(poly, q, half):
    """점 q 의 잉크 원(반경 half)이 poly 실루엣 밖으로 삐져나온 거리(R). 음수면 완전히 안."""
    db = B.dist_to_boundary(poly, q)
    return (half - db) if B.contains(poly, q) else (half + db)

def union_area(lines, half, poly, minus=(), step=0.015):
    xs = [p[0] for p in poly]; ys = [p[1] for p in poly]
    lx = [p[0] for L in lines for p in L]; ly = [p[1] for L in lines for p in L]
    x0 = max(min(xs), min(lx)-half); x1 = min(max(xs), max(lx)+half)
    y0 = max(min(ys), min(ly)-half); y1 = min(max(ys), max(ly)+half)
    n = 0; x = x0 + step*0.5
    while x < x1:
        y = y0 + step*0.5
        while y < y1:
            q = (x, y)
            if B.contains(poly, q) and not any(B.contains(m, q) for m in minus) \
               and any(B.dist_pt_polyline(q, L) <= half for L in lines):
                n += 1
            y += step
        x += step
    return n*step*step

POSES = [
    ("P2 뒷짐",      lambda: B.watch_stance_arms(True,  0.0)),
    ("P1 팔짱",      lambda: B.watch_stance_arms(False, 0.0)),
    ("대조 평소Idle", lambda: B.idle_arms()),
]

def main():
    print("="*104)
    print("R26b — 실루엣 교차 구조 · 잉크 돌출 · 두 팔 합집합   (design-equipment / 2026-09-06)")
    print("="*104)
    for scale in (0.75, 1.00):
        w = B.arm_width_R(scale); half = 0.5*w; pt = B.R_to_pt(scale)
        print("\n배율 %.2f — 머리 지름 %.2f pt · 팔 획 %.3f R (%.2f pt) · 잉크 반폭 %.4f R"
              % (scale, 2*pt, w, w*pt, half))
        for nm, cape in B.CAPES.items():
            back, collar, clasp = cape["back"], cape["collar"], cape["clasp"]
            print("  ■ %s" % nm)
            for label, fn in POSES:
                arms = fn()
                lines = [B.arm_points(u, l, w) for (u, l) in arms]
                for i, L in enumerate(lines):
                    nx, seq = crossings(L, back)
                    tip, elbow = L[-1], L[B.ARC_SAMPLES]
                    print("    %-11s 팔%s | 경계 교차 %d회 (%s)" % (label, "AB"[i], nx, seq))
                    print("                     손끝 잉크 돌출 %+.4f R (%+.2f pt) | 팔꿈치 잉크 돌출 %+.4f R (%+.2f pt)"
                          % (ink_out(back, tip, half), ink_out(back, tip, half)*pt,
                             ink_out(back, elbow, half), ink_out(back, elbow, half)*pt))
                ua = union_area(lines, half, back, minus=(collar, clasp))
                print("    %-11s 두 팔 합집합 노출 잉크 = %.4f R² (%.2f pt²) — 망토 뒤판 면적 대비 %.1f%%"
                      % (label, ua, ua*pt*pt, 100.0*ua/poly_area(back)))
            print()

def poly_area(poly):
    a = 0.0; n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]; x2, y2 = poly[(i+1) % n]
        a += x1*y2 - x2*y1
    return abs(a)*0.5

if __name__ == "__main__":
    main()
