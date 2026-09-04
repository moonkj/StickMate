# -*- coding: utf-8 -*-
"""② 인계본 16종을 **우리 자(尺)**로 잰다 (design-equipment, 2026-09-03).

두 크기에서 잰다 (★ 32pt는 이 앱에 없는 크기다):
  · 카드    — 우리 44px / 인계본 자기 창 58px
  · 착용    — 배율 0.75 머리지름 11.63pt (W = 0.343864 R), 0.60 도 함께

재는 규칙 (우리 것):
  규칙1  모든 변 >= 1.0 W (양끝 꺾임) · 잉크 사각형 >= 1.5 W
  규칙3-2 보조색 아이템당 정확히 1개
  규칙5  구성 정원 2~4
  자기교차 없음
  조각간 간격 — 서로 다른 조각의 윤곽이 1.5W 안에 있으면 화면에서 한 줄로 뭉친다("뚜껑")
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, handoff, items, hair
from rig import Shape
from handoff import parse_items, build, icon_to_R, to_R_shape, OUR_NAME, ITEM_SLOT, STROKE

W75 = rig.stroke_in_R(0.75)     # 0.343864
W60 = rig.stroke_in_R(0.60)     # 0.429830

def seg_pts(pts, loop, n=24):
    out = []
    m = len(pts)
    segs = m if loop else m - 1
    for i in range(segs):
        a, b = pts[i], pts[(i + 1) % m]
        for k in range(n + 1):
            t = k / n
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out

def min_piece_gap(shapes):
    """서로 다른 조각의 윤곽선 사이 최소 거리. 0이면 교차/접촉."""
    S = [seg_pts(s.pts, s.loop, 12) for s in shapes]
    best, pair = 1e9, None
    for i in range(len(S)):
        for j in range(i + 1, len(S)):
            d = min(math.dist(p, q) for p in S[i] for q in S[j])
            if d < best: best, pair = d, (shapes[i].name, shapes[j].name)
    return best, pair

def corner_count(pts, loop):
    n = len(pts)
    rng = range(n) if loop else range(1, n - 1)
    return sum(1 for i in rng if rig.turn_deg(pts[(i - 1) % n], pts[i], pts[(i + 1) % n]) >= rig.CORNER_DEG)

# ============================================================================
IT = parse_items()

print("╔══ 1. 조각 census — 인계본 16종 ══╗")
print("%-13s %-5s %-24s %-6s %-6s %-8s %s" %
      ("아이템", "조각", "구성", "정원", "보조색", "하이라이트", "채움알파(고유)"))
census = {}
viol_quota, viol_accent = [], []
for kind, pieces in IT.items():
    P = build(kind, pieces)
    kinds = " ".join(p.call for p in P)
    n = len(P)
    acc = sum(1 for p in P if p.is_accent)
    hi = sum(1 for p in P if p.is_highlight)
    alphas = sorted({p.fill_opacity for p in P if p.filled and p.fill_opacity is not None})
    grad = sum(1 for p in P if p.filled and p.fill_opacity is None)
    q_ok = 2 <= n <= 4
    a_ok = acc == 1
    if not q_ok: viol_quota.append((kind, n))
    if not a_ok: viol_accent.append((kind, acc))
    census[kind] = P
    print("%-13s %-5d %-24s %-6s %-6s %-8d %s%s" %
          (kind, n, kinds, "OK" if q_ok else "위반 %d" % n, "OK" if a_ok else "위반 %d" % acc,
           hi, ("그라디언트x%d " % grad) if grad else "",
           " ".join("%.2f" % a for a in alphas)))
print("  ⇒ 정원(2~4) 위반 %d종 · 보조색(정확히 1) 위반 %d종" % (len(viol_quota), len(viol_accent)))

# ============================================================================
print()
print("╔══ 2. 카드 규칙 — 인계본 자기 획(2.2/64) 기준 ══╗")
print("  우리 카드: viewBox 40 · 획 1.7 · FitFraction 0.86 · 아이콘 44px → 획 1.87 캔버스px")
print("  인계본  : viewBox 64 · 획 2.2 · 아이콘 58px(자기 창) → 획 %.3f px" % (2.2 * 58 / 64))
print("            같은 아트를 **우리 44px 카드**에 넣으면 획 %.3f px (현행 대비 %.0f%%)"
      % (2.2 * 44 / 64, 2.2 * 44 / 64 / 1.87 * 100))
print()
print("%-13s %10s %10s %8s %8s %s" % ("아이템", "잉크폭(획)", "최단변(획)", "꼭짓점", "자기교차", "규칙1 @카드"))
card_bad = 0
for kind, P in census.items():
    xs = [x for p in P for x, y in p.pts]; ys = [y for p in P for x, y in p.pts]
    span = max(max(xs) - min(xs), max(ys) - min(ys)) / STROKE
    worst, worst_name, xi = None, None, 0
    for p in P:
        s = Shape(p.call, p.pts, loop=p.closed)
        m = rig.min_corner_seg(s, STROKE)
        if m is not None and (worst is None or m < worst): worst, worst_name = m, p.call
        if p.closed and rig.self_intersects(p.pts): xi += 1
    bad = [p.call for p in P if rig.rule_one(Shape(p.call, p.pts, loop=p.closed), STROKE)]
    if bad: card_bad += 1
    print("%-13s %10.2f %10s %8d %8s %s" %
          (kind, span, ("%.2f(%s)" % (worst, worst_name)) if worst else "—",
           sum(corner_count(p.pts, p.closed) for p in P), xi if xi else "0",
           ("위반 " + ",".join(bad)) if bad else "OK"))
print("  ⇒ 인계본 자기 획 기준 규칙1 위반 %d종" % card_bad)

# ============================================================================
print()
print("╔══ 3. 착용 — 인계본 좌표를 우리 몸(R)에 얹었을 때 ══╗")
print("  변환식은 handoff_cal.py 에서 교정됐다(±1.0R 앵커 4건 · 최악 3.4%%).")
print()
print("%-13s %8s %9s %9s %9s %10s %s" %
      ("아이템", "슬롯", "획비", "최단변W", "잉크폭W", "조각간격W", "규칙1 @0.75 / @0.60"))
worn_rows = []
for kind, P in census.items():
    shapes = [to_R_shape(kind, p, p.call + str(i)) for i, p in enumerate(P)]
    hstroke = handoff.stroke_R(kind)          # 인계본 획을 R로 옮긴 값
    xs = [x for s in shapes for x, y in s.pts]; ys = [y for s in shapes for x, y in s.pts]
    span = max(max(xs) - min(xs), max(ys) - min(ys)) / W75
    worst = None
    for s in shapes:
        m = rig.min_corner_seg(s, W75)
        if m is not None and (worst is None or m < worst): worst = m
    gap, gpair = min_piece_gap(shapes)
    b75 = [s.name for s in shapes if rig.rule_one(s, W75)]
    b60 = [s.name for s in shapes if rig.rule_one(s, W60)]
    worn_rows.append((kind, hstroke, worst, span, gap, gpair, b75, b60))
    print("%-13s %8s %9.2f %9s %9.2f %10.2f %s / %s" %
          (kind, ITEM_SLOT[kind], hstroke / W75,
           ("%.2f" % worst) if worst else "—", span, gap / W75,
           "%d건" % len(b75), "%d건" % len(b60)))
print()
print("  ★ '획비' = 인계본이 자기 그림에서 쓴 획을 우리 몸에 옮겼을 때 우리 W의 몇 배인가.")
print("     1.0 미만이면 **인계본은 우리보다 가는 펜을 전제로 그렸다** = 우리 화면에선 그만큼 뭉친다.")
print("  ★ '조각간격' < 1.5 W 면 서로 다른 조각의 윤곽이 화면에서 **한 줄로 합쳐진다**(머리카락 '뚜껑'과 같은 병).")
merged = [(k, g / W75, gp) for k, hs, wo, sp, g, gp, b1, b2 in worn_rows if g / W75 < 1.5]
print("  ⇒ 1.5W 미만으로 뭉치는 아이템 %d/16종:" % len(merged))
for k, g, gp in sorted(merged, key=lambda r: r[1]):
    print("       %-13s %.2f W   %s ↔ %s" % (k, g, gp[0], gp[1]))

# ============================================================================
print()
print("╔══ 4. 우리 현행 30종과의 조각 수 / 폭 대조 ══╗")
OURS = {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK, "BACK": items.BACK}
print("%-13s %-14s %8s %8s %11s %11s %10s" %
      ("인계본", "우리", "조각(인)", "조각(우)", "폭R(인)", "폭R(우)", "높이R(인/우)"))
for kind, P in census.items():
    slot, nm = OUR_NAME[kind]
    ours = OURS[slot][nm]
    sh = [to_R_shape(kind, p, p.call) for p in P]
    hx = [x for s in sh for x, y in s.pts]; hy = [y for s in sh for x, y in s.pts]
    ox = [x for s in ours for x, y in s.pts]; oy = [y for s in ours for x, y in s.pts]
    print("%-13s %-14s %8d %8d %11.2f %11.2f %5.2f/%.2f" %
          (kind, nm, len(P), len(ours),
           max(hx) - min(hx), max(ox) - min(ox),
           max(hy) - min(hy), max(oy) - min(oy)))
