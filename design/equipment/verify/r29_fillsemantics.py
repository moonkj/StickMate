# -*- coding: utf-8 -*-
"""r29b — ★ **프롭의 `filled`는 면(mesh)이 아니다.** 같은 폴리라인을 **2.2배 굵기로 한 번 더** 긋는 것이다.

    CostumePropRenderer.cs:278   if (meta.filled) CreateShapeLine(pts, meta, ink, stroke * 2.2f, SortingPropFill, ...)
    CostumePropRenderer.cs:524   CreateShapeLine(...) -> LineRenderer.startWidth = width   (메시 없음)

반면 **장비**는 진짜 메시다: CharacterAccessoryRenderer.cs:1065  mr.sortingOrder = shape.FillSortingOrder (MeshRenderer).

⇒ R28 문서의 「채움」 판단(내접원 vs 채움 윤곽 펜 = 색면 조건)은 **장비 파이프라인의 자**다.
   프롭에서 그 자를 그대로 쓰면 **틀린다.** 이 스크립트가 그 차이를 숫자로 낸다.

    python3 r29_fillsemantics.py
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, r28_props as P

W = P.WP
FILL_MULT = 2.2                      # CostumePropRenderer.cs:278
HALF = FILL_MULT * W / 2.0           # 잉크가 윤곽 밖으로 번지는 폭 = 1.10 W_P
print("=" * 104)
print("  r29b — 프롭 `filled` = 2.2배 굵기 재묘사. 잉크 번짐 반폭 = %.4f H = %.2f W_P = %.2f pt @0.75"
      % (HALF, HALF / W, HALF * P.PT_PER_H * 0.75))
print("=" * 104)

def poly_dist(pts, q):
    """폴리곤 경계까지의 최단거리."""
    n = len(pts)
    return min(P._pt_seg(q, pts[i], pts[(i + 1) % n]) for i in range(n))

def inside(pts, q):
    return rig.contains(pts, q)

def hull(pts):
    ps = sorted(set(pts))
    if len(ps) < 3: return ps
    def half(ps):
        out = []
        for p in ps:
            while len(out) >= 2 and ((out[-1][0]-out[-2][0])*(p[1]-out[-2][1])
                                     - (out[-1][1]-out[-2][1])*(p[0]-out[-2][0])) <= 0:
                out.pop()
            out.append(p)
        return out
    return half(ps)[:-1] + half(ps[::-1])[:-1]

def probe(pts, step=0.0015):
    """(a) 내부 최대 내접원 rho_in  (b) 오목 주머니(볼록껍질\\폴리곤) 최대 여유 rho_out."""
    x0, y0, x1, y1 = rig.bounds(pts)
    hl = hull(pts)
    rin, rout = 0.0, 0.0
    y = y0
    while y <= y1:
        x = x0
        while x <= x1:
            q = (x, y)
            d = poly_dist(pts, q)
            if inside(pts, q):
                rin = max(rin, d)
            elif inside(hl, q):
                rout = max(rout, d)
            x += step
        y += step
    return rin, rout

rows = []
for key, name, fn, counts, wpts, aims, leaf in P.PROPS:
    for s in fn():
        if not s.filled: continue
        rin, rout = probe(s.pts)
        rows.append((key, s.name, rin, rout))

print()
print("  %-9s %-11s %10s %10s   %s" % ("프롭", "채움 조각", "rho_in", "rho_pocket", "2.2배 재묘사가 만드는 것"))
print("  " + "-" * 100)
for key, nm, rin, rout in rows:
    solid  = rin <= HALF                       # 내부 구멍이 닫히는가
    pocket = (rout > 0) and (rout <= HALF)     # 오목 주머니가 메워지는가
    if solid and rout <= 1e-9:
        verd = "**꽉 찬 면** — 의도대로 색면"
    elif solid and pocket:
        verd = "**꽉 찬 면 + 오목부까지 메워짐** -> 실루엣이 바뀐다"
    elif solid:
        verd = "꽉 찬 면(오목부는 남음 %.4f H)" % rout
    else:
        verd = "**가운데가 뚫린 굵은 테두리** — 색면이 아니다(구멍 반경 %.4f H)" % (rin - HALF)
    print("  %-9s %-11s %10.4f %10.4f   %s" % (key, nm, rin, rout, verd))

print()
print("  기준: rho_in <= %.4f H 면 내부가 닫힌다 / rho_pocket <= %.4f H 면 오목부가 메워진다" % (HALF, HALF))
print()
print("  ★ 그리고 **잉크 봉투가 사방으로 %.4f H 커진다.** R28 의 금지대(중심거리 2.00 W_P)는" % HALF)
print("     1배 획 두 개를 전제한 값이다. **채움 조각이 낀 쌍의 하한은 다르다**:")
print("       1배 x 1배   : d >= (1.0+1.0)/2 W + 1.0 W = 2.00 W_P = %.4f H" % (2.00 * W))
print("       2.2배 x 1배 : d >= (2.2+1.0)/2 W + 1.0 W = **2.60 W_P** = %.4f H" % (2.60 * W))
print("       2.2배 x 2.2배: d >= (2.2+2.2)/2 W + 1.0 W = 3.20 W_P = %.4f H" % (3.20 * W))
print()

# R28 4프롭에서 **채움 조각이 낀 쌍**만 2.60 W_P 로 다시 잰다.
print("=" * 104)
print("  재검산 — R28 4프롭의 「채움 x 선」 쌍을 하한 2.60 W_P 로 다시 본다")
print("=" * 104)
viol = 0
for key, name, fn, counts, wpts, aims, leaf in P.PROPS:
    shapes = fn()
    worst = (9e9, None)
    for i in range(len(shapes)):
        for j in range(len(shapes)):
            if i >= j: continue
            a, b = shapes[i], shapes[j]
            if not (a.filled or b.filled): continue
            need = 3.20 * W if (a.filled and b.filled) else 2.60 * W
            d, _ = P.shape_dist(a, b)
            if d <= 1e-9: continue                      # 닿은 쌍은 금지대를 안 만든다
            if d < worst[0]: worst = (d, (a.name, b.name, need))
    if worst[1] is None:
        print("  %-9s 채움이 낀 «떨어진» 쌍 없음" % key); continue
    d, (an, bn, need) = worst[0], worst[1]
    tag = "OK" if d >= need else "x "
    if d < need: viol += 1
    print("  %s %-9s 최악 '%s' x '%s'  d=%.4f H = %.2f W_P  (새 하한 %.2f W_P = %.4f H)"
          % (tag, key, an, bn, d, d / W, need / W, need))
print()
print("결과: 채움 하한 재적용 위반 %d건" % viol)
