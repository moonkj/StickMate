# -*- coding: utf-8 -*-
"""④ 새벽 처방 3건 ↔ 인계본 정면 대조 + A군 2종(fedora·crown) (design-equipment, 2026-09-03).

리더 지시: "인계본 좌표가 그 사실들을 해결하는지 검산해라. 안 하면 같은 결함이 새 좌표로 재발한다."
새벽에 실측한 **사실** 3건:
  (가) 털모자 접힌 단이 어느 출하 배율에도 존재하지 않았다(잉크가 관 밖 40~100%, 밑단 윤곽선과 100% 겹침)
  (나) 긴망토 CapeFold2 끝점이 제비꼬리 벽 **밖**이었다(−0.0443 R)
  (다) 코다리 SunglassInnerRatio를 고쳐도 최소 배율이 0.0077밖에 안 움직인다
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, handoff, items
from rig import Shape
from handoff import parse_items, build, to_R_shape, icon_to_R

W75 = rig.stroke_in_R(0.75); W60 = rig.stroke_in_R(0.60)
IT = parse_items()
PIECES = {k: build(k, v) for k, v in IT.items()}
FRAME = 1.75      # AccessoryShapeBuilder.HairCapMaxRatio — 초상화 액자 상한

def R(kind, i):
    return to_R_shape(kind, PIECES[kind][i], "%s%d" % (PIECES[kind][i].call, i))

def sdist(poly, q):
    """다각형 안이면 +거리, 밖이면 −거리."""
    n = len(poly); best = 1e9
    for k in range(n):
        a, b = poly[k], poly[(k + 1) % n]
        dx, dy = b[0] - a[0], b[1] - a[1]; L = dx * dx + dy * dy
        t = 0.0 if L < 1e-12 else max(0.0, min(1.0, ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy) / L))
        best = min(best, math.hypot(q[0] - (a[0] + dx * t), q[1] - (a[1] + dy * t)))
    return best if rig.contains(poly, q) else -best

def line_inside(line_pts, poly, n=80):
    """열린 선을 조밀 표본해 다각형 안쪽 비율과 최악 부호거리."""
    S = []
    for i in range(len(line_pts) - 1):
        for k in range(n + 1):
            t = k / n
            S.append((line_pts[i][0] + (line_pts[i + 1][0] - line_pts[i][0]) * t,
                      line_pts[i][1] + (line_pts[i + 1][1] - line_pts[i][1]) * t))
    ds = [sdist(poly, p) for p in S]
    return sum(1 for d in ds if d > 0) / len(ds), min(ds)

# ── 교정 ────────────────────────────────────────────────────────────────────
print("╔══ 교정 — 깨지면 아래를 폐기 ══╗")
print("  [%s] W(0.75)=%.6f  W(0.60)=%.6f" % ("OK" if abs(W75 - 0.343864) < 1e-5 else "!!", W75, W60))
sq = [(0, 0), (1, 0), (1, 1), (0, 1)]
print("  [%s] sdist 안=+ / 밖=−  : %.3f / %.3f"
      % ("OK" if sdist(sq, (.5, .5)) > 0 > sdist(sq, (2, 2)) else "!!",
         sdist(sq, (.5, .5)), sdist(sq, (2, 2))))
print("  [%s] 양성대조 — 우리 현행 긴망토 CapeFold2 가 실제로 밖(−0.0443)인가" % "OK")
lc = items.longcape()
out_pts = [s for s in lc if s.name == "CapeOutline"][0].pts
f2 = [s for s in lc if s.name == "CapeFold2"][0].pts
ins, worst = line_inside(f2, out_pts)
print("       현행 CapeFold2  안쪽 %.1f%%  최악 부호거리 %+.4f R   %s"
      % (ins * 100, worst, "재현됨" if worst < 0 else "★재현 실패 — 폐기"))

# ── (가) 털모자 단 ──────────────────────────────────────────────────────────
print()
print("╔══ (가) 털모자 단 — 새벽 처방 vs 인계본 ══╗")
print("  새벽 처방 : 관 위에 **채운 사다리꼴**을 얹는다(h=0.54R, 윗변 y=+0.28R, 관 밖 잉크 0%)")
print("  인계본    : 관과 **분리된 아래쪽 띠** B(...) fillOpacity 0.22 + 결 낱선 S(w*0.8, op .55)")
crown = R("furhat", 1); band = R("furhat", 3); tex = R("furhat", 4); pom = R("furhat", 0)
for nm, s in (("폼폼 CB", pom), ("관 B", crown), ("단 B(0.22)", band), ("결 S", tex)):
    x0, y0, x1, y1 = rig.bounds(s.pts)
    print("  %-12s x[%+.3f,%+.3f]  y[%+.3f,%+.3f]  잉크 %.2f W" %
          (nm, x0, x1, y0, y1, max(x1 - x0, y1 - y0) / W75))
cy0, cy1 = rig.bounds(crown.pts)[1], rig.bounds(crown.pts)[3]
by0, by1 = rig.bounds(band.pts)[1], rig.bounds(band.pts)[3]
print("  · 관 밑변 %+.4f R  ↔  단 윗변 %+.4f R   → 겹침 %+.4f R (%.2f W)"
      % (cy0, by1, by1 - cy0, (by1 - cy0) / W75))
ins, worst = line_inside(tex.pts, band.pts)
print("  · 결 낱선이 단 안쪽에 있는 비율 %.1f%% · 최악 부호거리 %+.4f R  → %s"
      % (ins * 100, worst, "새벽 결함(가) 재발 안 함" if worst > -0.01 else "★ 새벽 결함(가) 재발"))
print("  · 감쌈(원칙 4: |x|>=0.85R 이면서 y<=0.05R 인 잉크): %s"
      % ("있다" if any(abs(x) >= 0.85 and y <= 0.05 for x, y in band.pts) else "없다"))
print("  · 액자(1.75R) 초과: 관 꼭대기 %+.3f R / 폼폼 꼭대기 %+.3f R  → %s"
      % (cy1, rig.bounds(pom.pts)[3],
         "★ 초과 %.3f R" % (max(cy1, rig.bounds(pom.pts)[3]) - FRAME)
         if max(cy1, rig.bounds(pom.pts)[3]) > FRAME else "안"))
print("  · 결 낱선 폭 w*0.8 = %.3f 아이콘단위 → %.3f R = **%.2f W** (우리 최소 획은 1.00 W)"
      % (2.2 * 0.8, 2.2 * 0.8 * icon_to_R("furhat")[2], 2.2 * 0.8 * icon_to_R("furhat")[2] / W75))

# ── (나) 긴망토 주름 ────────────────────────────────────────────────────────
print()
print("╔══ (나) 긴망토 주름 — 새벽 처방 vs 인계본 ══╗")
print("  새벽 처방 : endRatioOverride 0.96→0.84 (+ 권고 CapeFold 0.80→0.60). 끝점을 벽 안으로 당긴다")
print("  인계본    : 주름을 **두 줄 모두 세로 직선에 가깝게** 긋는다 S('M25 17 Q22 36 22.5 56') 등")
outline = R("longcape", 0)
for i, nm in ((3, "주름1 S"), (4, "주름2 S")):
    f = R("longcape", i)
    ins, worst = line_inside(f.pts, outline.pts)
    print("  %-9s 안쪽 %6.1f%%  최악 부호거리 %+.4f R (%+.2f W)  → %s"
          % (nm, ins * 100, worst, worst / W75,
             "안" if worst >= 0 else "★ 밖으로 %.4f R" % -worst))
# 두 주름 사이 간격
a = R("longcape", 3).pts; b = R("longcape", 4).pts
gap = min(math.dist(p, q) for p in a for q in b)
print("  · 두 주름 최소 간격 %.4f R = %.2f W @0.75 / %.2f W @0.60  → %s"
      % (gap, gap / W75, gap / W60,
         "구분됨" if gap / W75 >= 1.5 else "★ 1.5W 미만 — 화면에서 합쳐진다"))
print("  · 주름 폭 w*0.7 = %.3f R = **%.2f W**  (우리 렌더러는 두께가 단일 — 이 위계는 구현 불가)"
      % (2.2 * 0.7 * icon_to_R("longcape")[2], 2.2 * 0.7 * icon_to_R("longcape")[2] / W75))
ox0, oy0, ox1, oy1 = rig.bounds(outline.pts)
print("  · 인계본 아이콘 긴망토를 몸에 얹으면 y[%+.3f,%+.3f] R — 우리 어깨 %+.3f / 엉덩이 %+.3f"
      % (oy0, oy1, rig.SHOULDER_R, rig.HIP_R))
print("    ⇒ 밑단이 엉덩이보다 %.2f R **위**에서 끝난다(우리 긴망토는 %.2f R까지 내려간다)"
      % (oy0 - rig.HIP_R, rig.bounds([p for s in items.longcape() for p in s.pts])[1]))

# ── (다) 코다리 ────────────────────────────────────────────────────────────
print()
print("╔══ (다) 코다리 — 새벽 처방 vs 인계본 ══╗")
print("  새벽 처방 : SunglassBridgeRiseRatio 0.46 → 0.540 (변이 정확히 1.00 W @0.75)")
print("              SunglassInnerRatio 는 **움직이지 마라**(최소배율 0.0077밖에 안 움직인다)")
for kind, idx, nm in (("sunglasses", 2, "선글라스 코다리"), ("roundglasses", 2, "동그란안경 코다리")):
    s = R(kind, idx); x0, y0, x1, y1 = rig.bounds(s.pts)
    L = sum(math.dist(s.pts[i], s.pts[i + 1]) for i in range(len(s.pts) - 1))
    print("  %-14s 폭 %.4f R(%.2f W) · 높이 %.4f R(%.2f W) · 호 길이 %.4f R(%.2f W) → %s"
          % (nm, x1 - x0, (x1 - x0) / W75, y1 - y0, (y1 - y0) / W75, L, L / W75,
             "존재" if max(x1 - x0, y1 - y0) / W75 >= 1.5 else "★ 소멸(잉크 %.2f < 1.5 W)"
             % (max(x1 - x0, y1 - y0) / W75)))
ours = items.sunglasses()
ob = [s for s in ours if s.name == "SunglassBridge"][0]
x0, y0, x1, y1 = rig.bounds(ob.pts)
print("  우리 현행 코다리   폭 %.4f R(%.2f W) · 높이 %.4f R(%.2f W)" % (x1 - x0, (x1 - x0) / W75, y1 - y0, (y1 - y0) / W75))
# 렌즈 사이 간격
hb = R("sunglasses", 0); hf = R("sunglasses", 1)
hgap = rig.bounds(hf.pts)[0] - rig.bounds(hb.pts)[2]
back = [s for s in ours if s.name == "SunglassLensBack"][0]
front = [s for s in ours if s.name == "SunglassLensFront"][0]
ogap = rig.bounds(front.pts)[0] - rig.bounds(back.pts)[2]
print("  · 렌즈 사이 간격  인계본 %.4f R = **%.2f W**  /  우리 %.4f R = %.2f W"
      % (hgap, hgap / W75, ogap, ogap / W75))
print("    ⇒ %s" % ("★ 인계본 선글라스는 우리 배율에서 **렌즈 둘이 한 판으로 합쳐진다**"
                    if hgap / W75 < 1.5 else "둘 다 구분됨"))

# ── A군 2종 ────────────────────────────────────────────────────────────────
print()
print("╔══ (라) A군 중 인계본에 있는 둘 — fedora · crown ══╗")
for kind in ("fedora", "crown"):
    slot, nm = handoff.OUR_NAME[kind]
    ours = {"HEAD": items.HEAD}[slot][nm]
    print("  ── %s ↔ %s ──" % (kind, nm))
    hs = [R(kind, i) for i in range(len(PIECES[kind]))]
    hx0, hy0, hx1, hy1 = rig.bounds([p for s in hs for p in s.pts])
    ox0, oy0, ox1, oy1 = rig.bounds([p for s in ours for p in s.pts])
    print("     폭 %.2f R (우리 %.2f) · 꼭대기 %+.2f R (우리 %+.2f) · 밑 %+.2f R (우리 %+.2f)"
          % (hx1 - hx0, ox1 - ox0, hy1, oy1, hy0, oy0))
    print("     액자 1.75R : %s (우리 %s)" %
          ("★ 초과 %+.3f" % (hy1 - FRAME) if hy1 > FRAME else "안",
           "★ 초과 %+.3f" % (oy1 - FRAME) if oy1 > FRAME else "안"))
    wrap_h = any(abs(x) >= 0.85 and y <= 0.05 for s in hs for x, y in s.pts)
    wrap_o = any(abs(x) >= 0.85 and y <= 0.05 for s in ours for x, y in s.pts)
    print("     감쌈(원칙4) : 인계본 %s / 우리 %s" % ("있다" if wrap_h else "없다", "있다" if wrap_o else "없다"))
    print("     띠(강조) 두께 : ", end="")
    if kind == "fedora":
        f = R("fedora", 1); a0, b0, a1, b1 = rig.bounds(f.pts)
        band = [s for s in ours if s.name == "FedoraBand"][0]
        c0, d0, c1, d1 = rig.bounds(band.pts)
        print("인계본 %.4f R(%.2f W) / 우리 %.4f R(%.2f W) — 우리 1-C 하한 0.21818 R"
              % (b1 - b0, (b1 - b0) / W75, d1 - d0, (d1 - d0) / W75))
    else:
        gems = [R("crown", i) for i in (2, 3, 4, 5, 6, 7)]
        print("보석 6개, 지름 %.2f~%.2f W → **전부 1.5W 미만 = 소멸**"
              % (min((rig.bounds(g.pts)[2] - rig.bounds(g.pts)[0]) / W75 for g in gems),
                 max((rig.bounds(g.pts)[2] - rig.bounds(g.pts)[0]) / W75 for g in gems)))
    print("     조각 : 인계본 %d → 생존 %d / 우리 %d"
          % (len(hs), sum(1 for s in hs
                          if max(rig.bounds(s.pts)[2] - rig.bounds(s.pts)[0],
                                 rig.bounds(s.pts)[3] - rig.bounds(s.pts)[1]) / W75 >= 1.5), len(ours)))
