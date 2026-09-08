# -*- coding: utf-8 -*-
"""r31 — ★ **채움 조각이 「가운데 뚫린 굵은 테두리」로 나오는 결함**을 좌표로 고친다.
       (design-equipment, 2026-09-08. game-architect §18-2 판정 3단계의 3번)

    python3 r31_fillsolid.py             # 전후 대조 + 전수 게이트
    python3 r31_fillsolid.py --search    # 광부 Vein1 좌표 탐색(재현용)
    python3 r31_fillsolid.py --control   # ★ 양성 대조 — 일부러 나쁜 값에 빨간불이 켜지는가

━━ 이 라운드가 잰 것과 그 자 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
프롭의 `filled`는 **면(mesh)이 아니다**. `CostumePropRenderer.cs:278` 이 같은 폴리라인을
`stroke * 2.2f` 로 **한 번 더 긋는다**. 그래서 잉크는 윤곽선을 중심으로 **양쪽으로** 번지고,
번짐 반폭은  1.10 W_P = 1.10 x 0.0339 = **0.037290 H** 다.

    · 내부가 꽉 차려면          rho_in  <= 0.037290 H      ← **상한**이다(하한이 아니다)
    · 오목 주머니가 살아남으려면 rho_pocket > 0.037290 H
    · 잉크 봉투는 사방으로       +0.037290 H 커진다        ← 금지대 하한이 2.60 W_P 인 이유

★ **부호를 뒤집어 읽으면 정반대로 고치게 된다.** 「도형을 넓히면 채워진다」는 틀렸다 —
  넓힐수록 구멍이 커진다. 통과한 두 조각(arcane 0.0337 / office 0.0335)이 **작은 쪽**이다.
"""
import sys, os, math, re

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r31_assetparse as AP
from r31_assetparse import load_manifest, Piece

# ── 자(尺) — 전부 프로덕션에서 뽑는다 ─────────────────────────────────────────
def _read_stroke_ratio():
    """CostumePropRenderer.StrokeWidthRatio 를 **소스에서 읽는다**(숫자를 베끼지 않는다).
    CostumePropForbiddenZoneTests.StrokeWidthInH 와 같은 방식이다."""
    p = os.path.join(AP.REPO, "Assets", "_Project", "Scripts", "Interaction", "CostumePropRenderer.cs")
    src = open(p, encoding="utf-8").read()
    key = "private const float StrokeWidthRatio = "
    i = src.index(key) + len(key)
    return float(src[i:src.index("f", i)])


def _read_fill_mult():
    """`stroke * 2.2f` 의 2.2 — 같은 소스에서 읽는다."""
    p = os.path.join(AP.REPO, "Assets", "_Project", "Scripts", "Interaction", "CostumePropRenderer.cs")
    src = open(p, encoding="utf-8").read()
    key = "if (meta.filled) CreateShapeLine(pts, meta, ink, stroke * "
    i = src.index(key) + len(key)
    return float(src[i:src.index("f", i)])


WP        = _read_stroke_ratio()          # 0.0339 H
FILL_MULT = _read_fill_mult()             # 2.2
HALF      = FILL_MULT * WP / 2.0          # 1.10 W_P = 0.037290 H  ← 채움 상한
PT_PER_H  = 80.18298                      # r28_props.PT_PER_H (BaselineTotalHeight x PT_PER_UNIT)
SHIP      = 0.75

GAP_1x1    = 2.00 * WP                    # PR-4 (프로덕션 CostumePropForbiddenZoneTests 하한)
GAP_FILLx1 = 2.60 * WP                    # R29 6-3 — 2.2배 x 1배
GAP_FILLxF = 3.20 * WP
EDGE_MIN   = 1.00 * WP                    # PR-1
SPAN_MIN   = 1.50 * WP                    # PR-2
JUNC_DEG   = 30.0                         # PR-4b

# 교정 — 남이 쓴 숫자로 맞춘다(깨지면 아래 값 전부 폐기).
assert abs(WP - 0.0339) < 1e-9, "W_P 교정 실패"
assert abs(FILL_MULT - 2.2) < 1e-9, "채움 배수 교정 실패"
assert abs(HALF - 0.037290) < 1e-6, "채움 번짐 반폭 교정 실패"
assert abs(HALF * PT_PER_H * SHIP - 2.243) < 0.001, "pt 환산 교정 실패"


# ── 기하 ─────────────────────────────────────────────────────────────────────
def segs(p):
    pts, n = p.pts, len(p.pts)
    rng = range(n) if p.loop else range(n - 1)
    return [(pts[i], pts[(i + 1) % n]) for i in rng]


def pt_seg(p, a, b):
    vx, vy = b[0] - a[0], b[1] - a[1]
    L2 = vx * vx + vy * vy
    t = 0.0 if L2 < 1e-15 else max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / L2))
    return math.hypot(p[0] - (a[0] + vx * t), p[1] - (a[1] + vy * t))


def _cross(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


def _onspan(p, q, r):
    return (min(p[0], r[0]) - 1e-9 <= q[0] <= max(p[0], r[0]) + 1e-9
            and min(p[1], r[1]) - 1e-9 <= q[1] <= max(p[1], r[1]) + 1e-9)


def seg_int(a, b, c, d):
    d1, d2 = _cross(a, b, c), _cross(a, b, d)
    d3, d4 = _cross(c, d, a), _cross(c, d, b)
    if (d1 > 0) != (d2 > 0) and (d3 > 0) != (d4 > 0): return True
    if abs(d1) < 1e-12 and _onspan(a, c, b): return True
    if abs(d2) < 1e-12 and _onspan(a, d, b): return True
    if abs(d3) < 1e-12 and _onspan(c, a, d): return True
    if abs(d4) < 1e-12 and _onspan(c, b, d): return True
    return False


def seg_seg(a, b, c, d):
    if seg_int(a, b, c, d): return 0.0
    return min(pt_seg(a, c, d), pt_seg(b, c, d), pt_seg(c, a, b), pt_seg(d, a, b))


def shape_dist(s1, s2):
    best = 9e9
    for a, b in segs(s1):
        for c, d in segs(s2):
            best = min(best, seg_seg(a, b, c, d))
    return best


def collinear_overlap(a, b, c, d):
    """두 «거의 공선» 변이 축 위에서 몇이나 겹치는가(H). 끝끼리 만나기만 하면 0."""
    ux, uy = b[0] - a[0], b[1] - a[1]
    L = math.hypot(ux, uy)
    if L < 1e-12: return 0.0
    ux, uy = ux / L, uy / L
    t = sorted(((c[0]-a[0])*ux + (c[1]-a[1])*uy, (d[0]-a[0])*ux + (d[1]-a[1])*uy))
    return max(0.0, min(L, t[1]) - max(0.0, t[0]))


def junction_angle(s1, s2, tol=1e-9):
    """접합(거리 0)한 두 조각이 몇 도로 갈라지는가 — 최소값(도).
    ★ r28_props.junction_angle 과 **같은 예외**를 둔다: 공선인데 «끝끼리 이어지기만» 하는 쌍
      (겹침 <= 0.5 W_P)은 한 물건의 이어진 선이라 제외한다(책상 앞턱 + 상판)."""
    worst = None
    for a, b in segs(s1):
        for c, d in segs(s2):
            if seg_seg(a, b, c, d) > tol: continue
            u = (b[0] - a[0], b[1] - a[1]); v = (d[0] - c[0], d[1] - c[1])
            nu, nv = math.hypot(*u), math.hypot(*v)
            if nu < 1e-12 or nv < 1e-12: continue
            cs = max(-1.0, min(1.0, (u[0] * v[0] + u[1] * v[1]) / (nu * nv)))
            ang = math.degrees(math.acos(cs))
            ang = min(ang, 180.0 - ang)
            if ang < 1.0 and collinear_overlap(a, b, c, d) <= 0.5 * WP:
                continue
            worst = ang if worst is None else min(worst, ang)
    return worst


def bounds(pts):
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    return min(xs), min(ys), max(xs), max(ys)


def contains(pts, q):
    x, y = q; n = len(pts); inside = False
    for i in range(n):
        x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % n]
        if (y1 > y) != (y2 > y):
            xin = x1 + (y - y1) * (x2 - x1) / (y2 - y1)
            if x < xin: inside = not inside
    return inside


def rho_in(pts, coarse=0.0020, refine=3):
    """내부 최대 내접원 반지름 — 격자 스캔 뒤 국소 세분(격자 오차를 남기지 않는다)."""
    def dist(q):
        n = len(pts)
        return min(pt_seg(q, pts[i], pts[(i + 1) % n]) for i in range(n))
    x0, y0, x1, y1 = bounds(pts)
    step = coarse
    best, bq = 0.0, None
    ny = int((y1 - y0) / step) + 1; nx = int((x1 - x0) / step) + 1
    for iy in range(ny + 1):
        y = y0 + iy * step
        for ix in range(nx + 1):
            x = x0 + ix * step
            if not contains(pts, (x, y)): continue
            d = dist((x, y))
            if d > best: best, bq = d, (x, y)
    for _ in range(refine):
        if bq is None: break
        step /= 4.0
        cx, cy = bq
        for iy in range(-5, 6):
            for ix in range(-5, 6):
                q = (cx + ix * step, cy + iy * step)
                if not contains(pts, q): continue
                d = dist(q)
                if d > best: best, bq = d, q
    return best


def rho_pocket(pts, coarse=0.0020):
    """오목 주머니(볼록껍질 \\ 폴리곤) 최대 여유 — 채움이 노치를 메우는가."""
    def dist(q):
        n = len(pts)
        return min(pt_seg(q, pts[i], pts[(i + 1) % n]) for i in range(n))
    ps = sorted(set(pts))
    if len(ps) < 3: return 0.0
    def half(seq):
        out = []
        for p in seq:
            while len(out) >= 2 and ((out[-1][0]-out[-2][0])*(p[1]-out[-2][1])
                                     - (out[-1][1]-out[-2][1])*(p[0]-out[-2][0])) <= 0:
                out.pop()
            out.append(p)
        return out
    hl = half(ps)[:-1] + half(ps[::-1])[:-1]
    x0, y0, x1, y1 = bounds(pts)
    best = 0.0
    ny = int((y1 - y0) / coarse) + 1; nx = int((x1 - x0) / coarse) + 1
    for iy in range(ny + 1):
        y = y0 + iy * coarse
        for ix in range(nx + 1):
            x = x0 + ix * coarse
            q = (x, y)
            if contains(pts, q) or not contains(hl, q): continue
            best = max(best, dist(q))
    return best


def turn_deg(a, b, c):
    u = (b[0] - a[0], b[1] - a[1]); v = (c[0] - b[0], c[1] - b[1])
    nu, nv = math.hypot(*u), math.hypot(*v)
    if nu < 1e-12 or nv < 1e-12: return 0.0
    cs = max(-1.0, min(1.0, (u[0]*v[0] + u[1]*v[1]) / (nu * nv)))
    return math.degrees(math.acos(cs))


CORNER_DEG = 45.0


def corner_min_edge(p):
    pts, n = p.pts, len(p.pts)
    if n < 2: return 9e9
    corner = [False] * n
    rng = range(n) if p.loop else range(1, n - 1)
    for i in rng:
        corner[i] = turn_deg(pts[(i-1) % n], pts[i], pts[(i+1) % n]) >= CORNER_DEG
    worst = 9e9
    for i in (range(n) if p.loop else range(n - 1)):
        j = (i + 1) % n
        if corner[i] and corner[j]:
            worst = min(worst, math.dist(pts[i], pts[j]))
    return worst


def self_intersects(pts):
    n = len(pts)
    for i in range(n):
        for j in range(i + 1, n):
            if j == i or (i == 0 and j == n - 1) or j == i + 1: continue
            if seg_int(pts[i], pts[(i+1) % n], pts[j], pts[(j+1) % n]): return True
    return False


# ── 모티프 결정나무 (pack78_shapes.classify 를 그대로 옮긴다 — 새 검사기를 짓지 않는다) ──
def convex_deficiency(pts):
    def area(P):
        s = 0.0
        for i in range(len(P)):
            x1, y1 = P[i]; x2, y2 = P[(i+1) % len(P)]
            s += x1*y2 - x2*y1
        return abs(s) / 2.0
    ps = sorted(set(pts))
    if len(ps) < 3: return 0.0
    def half(seq):
        out = []
        for p in seq:
            while len(out) >= 2 and ((out[-1][0]-out[-2][0])*(p[1]-out[-2][1])
                                     - (out[-1][1]-out[-2][1])*(p[0]-out[-2][0])) <= 0:
                out.pop()
            out.append(p)
        return out
    hl = half(ps)[:-1] + half(ps[::-1])[:-1]
    ah = area(hl)
    return 0.0 if ah <= 1e-12 else (ah - area(pts)) / ah


def sharp_corners(pts, deg=70.0):
    n = len(pts); c = 0
    for i in range(n):
        a, b, d = pts[(i-1) % n], pts[i], pts[(i+1) % n]
        v1 = (a[0]-b[0], a[1]-b[1]); v2 = (d[0]-b[0], d[1]-b[1])
        n1, n2 = math.hypot(*v1), math.hypot(*v2)
        if n1 < 1e-12 or n2 < 1e-12: continue
        cs = max(-1.0, min(1.0, (v1[0]*v2[0] + v1[1]*v2[1]) / (n1*n2)))
        if math.degrees(math.acos(cs)) < deg: c += 1
    return c


def taper_ratio(pts):
    if len(pts) != 4: return None
    E = [(pts[(i+1) % 4][0]-pts[i][0], pts[(i+1) % 4][1]-pts[i][1]) for i in range(4)]
    L = [math.hypot(*e) for e in E]
    def ang(u, v):
        a = math.degrees(math.atan2(u[1], u[0])) % 180.0
        b = math.degrees(math.atan2(v[1], v[0])) % 180.0
        d = abs(a-b) % 180.0
        return min(d, 180.0-d)
    pairs = [(ang(E[0], E[2]), L[0], L[2]), (ang(E[1], E[3]), L[1], L[3])]
    pairs.sort(key=lambda t: t[0])
    a, l1, l2 = pairs[0]
    return min(l1, l2) / max(1e-9, max(l1, l2))


def classify(pts):
    n = len(pts)
    cdef = convex_deficiency(pts)
    sharp = sharp_corners(pts)
    tau = taper_ratio(pts)
    if cdef >= 0.15 and sharp >= 2 and n >= 12: return "대마법사"
    if n == 4 and tau is not None and tau <= 0.55: return "광부"
    if cdef >= 0.15:  return "밀리터리"
    if n <= 3:        return "네온 낙서"
    if n <= 4:        return "오피스 워커"
    if n <= 8:        return "사이버 아포칼립스"
    if sharp >= 1:    return "컬러 잉크"
    return "스포츠"


# ── 새 좌표 (이 라운드의 산출물) ─────────────────────────────────────────────
def wedge(root, tip, w_root, w_tip):
    """pack78_shapes.wedge 와 같은 식 — 광부 모티프 «쐐기»."""
    ax, ay = root; bx, by = tip
    L = math.hypot(bx-ax, by-ay)
    ux, uy = (bx-ax)/L, (by-ay)/L
    nx, ny = -uy, ux
    return [(ax+nx*w_root/2, ay+ny*w_root/2), (bx+nx*w_tip/2, by+ny*w_tip/2),
            (bx-nx*w_tip/2, by-ny*w_tip/2), (ax-nx*w_root/2, ay-ny*w_root/2)]


def r4(pts):
    return [(round(x, 4), round(y, 4)) for x, y in pts]


#: C1 사이버 `BasePad` — 8각(모서리 잘린 패드) → **6각(끝을 깎은 납작한 커넥터 패드)**.
#  두께 0.068 H 로 낮춰 rho_in 을 0.0600 → 0.0340 으로 내리고,
#  윗변을 y=0.0980 으로 올려 `Mast` 밑끝(0.54, 0.09)이 **판 안으로 박히게** 한다(d=0 유지).
NEW_BASEPAD = r4([(0.4400, 0.0640), (0.4700, 0.0300), (0.5900, 0.0300),
                  (0.6200, 0.0640), (0.5900, 0.0980), (0.4700, 0.0980)])

#: C3 광부 `Vein1` — 쐐기 그대로, **뿌리폭 0.1200 → 0.0760** (rho_in 0.0494 → 0.034).
NEW_VEIN1_ARG = dict(root=(0.4380, 0.7220), tip=(0.5230, 0.5770), w_root=0.0780, w_tip=0.0356)
NEW_VEIN1 = r4(wedge(**NEW_VEIN1_ARG))

REPLACE = {("cyber", "BasePad"): NEW_BASEPAD, ("mine", "Vein1"): NEW_VEIN1}
#: ★ 「수정 전」 좌표 — R28 §3-1 / §3-3 의 생성식을 **다시 계산**한 것이다(베낀 것이 아니다).
#   이것이 실제 출하본이었는지는 `git_before()` 가 `git show` 로 **매 실행 대조**한다.
CONTROL = {("cyber", "BasePad"): r4([(0.4400, 0.0300), (0.4700, 0.0000), (0.5900, 0.0000),
                                     (0.6200, 0.0300), (0.6200, 0.0900), (0.5900, 0.1200),
                                     (0.4700, 0.1200), (0.4400, 0.0900)]),
           ("mine", "Vein1"): r4(wedge((0.4300, 0.7400), (0.5300, 0.5800), 0.1200, 0.0500))}


def git_before(rev="HEAD"):
    """수정 전 좌표를 **git 에서 직접** 꺼낸다 — 「전」을 파일에서 읽으면 고친 뒤 대조가 죽는다.
    반환: {(short, name): pts} · git 이 없거나 파일이 이미 커밋됐으면 None."""
    import subprocess, tempfile
    out = {}
    try:
        with tempfile.TemporaryDirectory() as td:
            items = os.path.join(td, "Items"); os.makedirs(items)
            for short in ("cyber", "mine", "office", "arcane"):
                rel = "Assets/_Project/Resources/Items/CostumeManifest_%s.asset" % short
                blob = subprocess.run(["git", "show", "%s:%s" % (rev, rel)], cwd=AP.REPO,
                                      capture_output=True, text=True, check=True).stdout
                open(os.path.join(items, os.path.basename(rel)), "w", encoding="utf-8").write(blob)
            keep = AP.ITEMS
            try:
                AP.ITEMS = items
                for short, name in (("cyber", "BasePad"), ("mine", "Vein1")):
                    m = load_manifest(short)
                    out[(short, name)] = [q for q in m["propShapes"] if q.name == name][0].pts
            finally:
                AP.ITEMS = keep
        return out
    except Exception as e:
        print("  ** git 대조를 못 했다(%s) — CONTROL 을 그대로 「전」으로 쓴다." % e)
        return None


def patched(short, table):
    m = load_manifest(short)
    for grp in [m["propShapes"]] + [m["stage"][k] for k in sorted(m["stage"])]:
        for p in grp:
            key = (short, p.name)
            if key in table:
                p.pts = list(table[key])
    return m


# ── 게이트 ───────────────────────────────────────────────────────────────────
GIT_BEFORE = None
FAIL = []
def bad(msg):
    FAIL.append(msg); print("  x  " + msg)
def ok(msg):
    print("  OK " + msg)


def audit(short, m, label):
    print("\n" + "=" * 104)
    print("  %s   (%s · %s)" % (label, short, m["key"]))
    print("=" * 104)
    groups = [("base", m["propShapes"])] + [("S%d" % k, m["stage"][k]) for k in sorted(m["stage"])]

    # (1) 채움 — rho_in 상한
    print("\n  -- (1) 채움이 «면»인가: rho_in <= %.6f H (= 1.10 W_P = %.2f pt @0.75) --"
          % (HALF, HALF * PT_PER_H * SHIP))
    for grp, pieces in groups:
        for p in pieces:
            if not p.filled: continue
            ri = rho_in(p.pts); rp = rho_pocket(p.pts)
            hole = ri - HALF
            if ri > HALF:
                bad("%s %s '%s' rho_in %.4f H = %.2f W_P > 상한 -> **구멍 반경 %.4f H = %.2f pt @0.75**"
                    % (short, grp, p.name, ri, ri / WP, hole, hole * PT_PER_H * SHIP))
            else:
                ok("%-6s %-4s '%s' rho_in %.4f H = %.2f W_P (상한의 %.0f%%) · 오목주머니 %.4f · **꽉 찬 면**"
                   % (short, grp, p.name, ri, ri / WP, 100 * ri / HALF, rp))
        break  # 5벌 모두 같은 조각이라 base 한 번만 잰다(아래에서 동일성을 따로 확인한다)

    # (1b) 5벌이 같은 좌표인가 — 한 벌만 고치고 나머지를 잊는 사고를 잡는다
    for name in set(p.name for _, pieces in groups for p in pieces if p.filled):
        variants = set()
        for grp, pieces in groups:
            for p in pieces:
                if p.name == name: variants.add(tuple(p.pts))
        if len(variants) != 1:
            bad("%s '%s' 가 %d가지 좌표로 갈라져 있다(단계마다 다른 채움)" % (short, name, len(variants)))
        else:
            ok("%-6s '%s' 좌표가 %d벌 전부 동일" % (short, name, len(groups)))

    # (2) 조각 규칙 PR-1 / PR-2 / PR-3
    print("\n  -- (2) PR-1 꺾임변 >= 1.00 W_P · PR-2 잉크 사각형 >= 1.50 W_P · PR-3 자기교차 --")
    for grp, pieces in groups:
        for p in pieces:
            ce = corner_min_edge(p)
            if ce < 9e8 and ce < EDGE_MIN - 1e-9:
                bad("%s %s '%s' 꺾임-꺾임 변 %.4f H = %.2f W_P < 1.00" % (short, grp, p.name, ce, ce / WP))
            x0, y0, x1, y1 = bounds(p.pts)
            w_, h_ = x1 - x0, y1 - y0
            if max(w_, h_) < SPAN_MIN:
                bad("%s %s '%s' 잉크 사각형 span %.4f < 1.50 W_P" % (short, grp, p.name, max(w_, h_)))
            if p.filled and min(w_, h_) < SPAN_MIN - 1e-9:
                bad("%s %s '%s' 채움 짧은 변 %.4f H = %.2f W_P < 1.50"
                    % (short, grp, p.name, min(w_, h_), min(w_, h_) / WP))
            if p.loop and self_intersects(p.pts):
                bad("%s %s '%s' 자기교차" % (short, grp, p.name))
    ok("%-6s 조각 규칙 전수 확인(%d집합)" % (short, len(groups)))

    # (3) 금지대 — 프로덕션 하한(2.00 W_P)과 채움 인지 하한(2.60 W_P) 둘 다
    print("\n  -- (3) PR-4 금지대: d=0(닿음) 또는 d >= 하한. 1x1 %.4f H / 채움x1 %.4f H --"
          % (GAP_1x1, GAP_FILLx1))
    for grp, pieces in groups:
        worst_prod = (9e9, None); worst_fill = (9e9, None)
        for i in range(len(pieces)):
            for j in range(i + 1, len(pieces)):
                a, b = pieces[i], pieces[j]
                d = shape_dist(a, b)
                if d <= 1e-9:
                    ja = junction_angle(a, b)
                    if ja is not None and ja < JUNC_DEG - 1e-9:
                        bad("%s %s 접합 '%s'x'%s' 갈라짐 %.1f도 < %.0f (PR-4b)"
                            % (short, grp, a.name, b.name, ja, JUNC_DEG))
                    continue
                need = GAP_FILLxF if (a.filled and b.filled) else (GAP_FILLx1 if (a.filled or b.filled) else GAP_1x1)
                if d < GAP_1x1 - 1e-9:
                    bad("%s %s 프로덕션 금지대 '%s'x'%s' d=%.4f H = %.2f W_P < 2.00 "
                        "(CostumePropForbiddenZoneTests 가 이걸 잡는다)" % (short, grp, a.name, b.name, d, d / WP))
                if d < need - 1e-9:
                    bad("%s %s 채움인지 금지대 '%s'x'%s' d=%.4f H = %.2f W_P < %.2f"
                        % (short, grp, a.name, b.name, d, d / WP, need / WP))
                if d < worst_prod[0]: worst_prod = (d, (a.name, b.name))
                if (a.filled or b.filled) and d < worst_fill[0]: worst_fill = (d, (a.name, b.name))
        msg = "%-6s %-4s 최악쌍 '%s'x'%s' %.4f H = %.2f W_P" % (
            short, grp, worst_prod[1][0], worst_prod[1][1], worst_prod[0], worst_prod[0] / WP)
        if worst_fill[1]:
            msg += "  |  채움 낀 최악 '%s'x'%s' %.4f H = %.2f W_P (하한 2.60)" % (
                worst_fill[1][0], worst_fill[1][1], worst_fill[0], worst_fill[0] / WP)
        ok(msg)

    # (4) 모티프 잎 — 보조색 조각이 자기 팩 잎으로 분류되는가
    print("\n  -- (4) 모티프 잎 (PACK_THEME_SPEC §4-1 결정나무) --")
    want = {"cyber": "사이버 아포칼립스", "mine": "광부", "office": "오피스 워커", "arcane": "대마법사"}[short]
    acc = [p for p in m["propShapes"] if p.tone == 1]
    if len(acc) != 1:
        bad("%s 보조색 조각 %d개(정확히 1이어야 한다)" % (short, len(acc)))
    else:
        p = acc[0]
        got = classify(p.pts)
        x0, y0, x1, y1 = bounds(p.pts)
        asp = max(x1-x0, y1-y0) / max(1e-9, min(x1-x0, y1-y0))
        tau = taper_ratio(p.pts)
        line = ("%-6s '%s' n=%d 종횡비 %.2f 결손 %.4f 첨점 %d 테이퍼 %s -> 「%s」"
                % (short, p.name, len(p.pts), asp, convex_deficiency(p.pts),
                   sharp_corners(p.pts), "-" if tau is None else "%.3f" % tau, got))
        (ok if got == want else bad)(line if got == want else line + "  (기대 「%s」)" % want)

    # (5) 잉크 봉투 — 채움 조각의 **실제로 보이는** 덩어리
    print("\n  -- (5) 채움 조각의 잉크 봉투(윤곽 +- %.4f H) --" % HALF)
    for p in m["propShapes"]:
        if not p.filled: continue
        x0, y0, x1, y1 = bounds(p.pts)
        ok("%-6s '%s' 윤곽 [%.4f,%.4f]x[%.4f,%.4f] -> 잉크 [%.4f,%.4f]x[%.4f,%.4f] "
           "= %.1f x %.1f pt @0.75 · 접지선 아래 %.2f pt"
           % (short, p.name, x0, x1, y0, y1, x0-HALF, x1+HALF, y0-HALF, y1+HALF,
              (x1-x0+2*HALF) * PT_PER_H * SHIP, (y1-y0+2*HALF) * PT_PER_H * SHIP,
              max(0.0, HALF - y0) * PT_PER_H * SHIP))

    # (6) 배치 봉투 — 필요폭이 안 바뀌었는가
    allp = [q for grp, pieces in groups for p in pieces for q in p.pts]
    x0, y0, x1, y1 = bounds(allp)
    ok("%-6s 배치 봉투 근단 %+.4f / 원단 %+.4f / 최저 %+.4f / 최고 %+.4f · 필요폭 %.4f H = %.1f pt @0.75"
       % (short, x0, x1, y0, y1, x1 + 0.10, (x1 + 0.10) * PT_PER_H * SHIP))


def compare(short, name):
    """전/후 rho_in 대조 한 줄."""
    before = load_manifest(short)
    p0 = [p for p in before["propShapes"] if p.name == name][0]
    r0 = rho_in(p0.pts)
    after = patched(short, REPLACE)
    p1 = [p for p in after["propShapes"] if p.name == name][0]
    r1 = rho_in(p1.pts)
    return r0, r1


def search_vein():
    """★ 재현용 — 광부 Vein1 을 탐색으로 다시 구한다(내가 손으로 고른 값이 최적 근처인지)."""
    base = load_manifest("mine")
    others = {grp: [p for p in pieces if p.name != "Vein1"]
              for grp, pieces in [("S%d" % k, base["stage"][k]) for k in sorted(base["stage"])]}
    print("=" * 104)
    print("  광부 Vein1 탐색 — 목적함수 = min(모든 금지대 여유 / 채움 여유)를 최대화")
    print("=" * 104)
    best = None
    for rx in [0.425 + 0.005 * i for i in range(7)]:
        for ry in [0.730 + 0.004 * i for i in range(6)]:
            for wr in [0.070 + 0.002 * i for i in range(6)]:
                for wt in [0.034 + 0.002 * i for i in range(4)]:
                    for dx, dy in ((0.100, -0.160), (0.110, -0.170), (0.090, -0.150)):
                        pts = r4(wedge((rx, ry), (rx + dx, ry + dy), wr, wt))
                        if taper_ratio(pts) > 0.55: continue
                        if corner_min_edge(Piece("v", pts, True, True, 1)) < EDGE_MIN: continue
                        ri = rho_in(pts, coarse=0.004, refine=2)
                        if ri > HALF: continue
                        v = Piece("Vein1", pts, True, True, 1)
                        gaps = []
                        for grp, ps in others.items():
                            for p in ps:
                                d = shape_dist(v, p)
                                if d <= 1e-9: continue
                                gaps.append(d / GAP_FILLx1)
                        score = min(min(gaps), (HALF - ri) / HALF + 1.0)
                        if best is None or score > best[0]:
                            best = (score, (rx, ry, dx, dy, wr, wt), ri, min(gaps))
    s, arg, ri, g = best
    print("  최적 근처: root=(%.4f,%.4f) 축=(%+.3f,%+.3f) 뿌리폭 %.4f 끝폭 %.4f" % (arg[0], arg[1], arg[2], arg[3], arg[4], arg[5]))
    print("            rho_in=%.4f (상한 %.4f) · 최악 금지대 여유 배수 %.3f · score %.3f" % (ri, HALF, g, s))
    print("\n  내가 채택한 값: root=(%.4f,%.4f) 뿌리폭 %.4f 끝폭 %.4f"
          % (NEW_VEIN1_ARG["root"][0], NEW_VEIN1_ARG["root"][1],
             NEW_VEIN1_ARG["w_root"], NEW_VEIN1_ARG["w_tip"]))


# ── 적용 — 두 조각의 `terms` 스트림만 외과적으로 갈아 끼운다 ─────────────────
def _f2s(v):
    """Unity YAML float 표기 — `r30_costume_assets.f2s` 와 같은 규칙(짧은 표기)."""
    if abs(v) < 5e-8: return "0"
    t = ("%.7f" % v).rstrip("0").rstrip(".")
    return t if t not in ("-0", "") else "0"


def _terms_lines(pts, pad):
    out = ["%s- %d" % (pad, len(pts))]
    for x, y in pts:
        for v in (x, y):
            out += ["%s- 1" % pad, "%s- 8" % pad, "%s- 0" % pad,
                    "%s- 0" % pad, "%s- 1" % pad, "%s- %s" % (pad, _f2s(v))]
    return out


def apply_patch(short, name, pts, dry=False):
    """`.asset` 안에서 `- name: <name>` 블록의 `terms:` 목록만 바꾼다.
    다른 조각·다른 필드·들여쓰기·GUID 는 한 글자도 안 건드린다."""
    path = os.path.join(AP.ITEMS, "CostumeManifest_%s.asset" % short)
    lines = open(path, encoding="utf-8").read().split("\n")
    out, i, hits = [], 0, 0
    while i < len(lines):
        ln = lines[i]
        if ln.strip().startswith("- name: ") and ln.strip()[8:].strip() == name:
            out.append(ln); i += 1
            # 이 조각의 필드를 그대로 흘려보내다가 terms: 를 만나면 목록을 갈아 끼운다
            while i < len(lines) and not lines[i].strip().startswith("- name: "):
                cur = lines[i]
                if cur.strip() == "terms:":
                    out.append(cur); i += 1
                    pad = cur[:len(cur) - len(cur.lstrip())]   # 목록은 `terms:` 와 **같은 들여쓰기**다
                    # ★ **순수 숫자 항목만** 버린다. `startswith("- ")` 로 지우면
                    #   조각이 그 단계의 마지막일 때 다음 줄 `- stage: N` 까지 먹는다
                    #   (실제로 한 번 그렇게 깨뜨렸다 — 되읽기 검증은 Vein1 만 봐서 통과했다).
                    while i < len(lines) and re.match(r"^- -?[0-9.]+$", lines[i].strip()):
                        i += 1                       # 옛 목록 버림
                    out += _terms_lines(pts, pad)
                    hits += 1
                    break
                out.append(cur); i += 1
            continue
        out.append(ln); i += 1
    if hits == 0:
        raise SystemExit("★ '%s' 를 %s 에서 못 찾았다 — 아무것도 안 썼다." % (name, path))
    if not dry:
        open(path, "w", encoding="utf-8").write("\n".join(out))
    return hits, path


def do_apply():
    print("=" * 104)
    print("  r31 --apply : 출하 에셋의 `BasePad` / `Vein1` **terms 스트림만** 갈아 끼운다")
    print("=" * 104)
    for (short, name), pts in REPLACE.items():
        hits, path = apply_patch(short, name, pts)
        print("  %-6s '%s' %d벌 교체 -> %s" % (short, name, hits, os.path.relpath(path, AP.REPO)))
    print("\n  ── 되읽기 검증(파일에서 다시 파싱해 새 좌표와 대조) ──")
    bad_ = 0
    for (short, name), pts in REPLACE.items():
        m = load_manifest(short)
        groups = [m["propShapes"]] + [m["stage"][k] for k in sorted(m["stage"])]
        n = 0
        for g in groups:
            for p in g:
                if p.name != name: continue
                n += 1
                got = [(round(a, 4), round(b, 4)) for a, b in p.pts]
                if got != list(pts):
                    bad_ += 1
                    print("  x  %s '%s' 되읽기 불일치: %s" % (short, name, got))
        print("  OK %-6s '%s' %d벌 전부 새 좌표와 일치" % (short, name, n))
    if bad_:
        raise SystemExit("★ 되읽기 검증 실패 %d건" % bad_)
    print("\n  ── 구조 교정(파일이 통째로 살아 있는가) ──")
    AP.selftest()          # 단계 수 · 조각 수 · 골든 5개. 여기서 «먹힌 줄»이 잡힌다.


def generator_agrees():
    """★ **생성기와 출하본이 갈라지지 않았는가.** `r30_costume_assets.py` 는 이 에셋을 만든
    생성기다 — 거기 좌표를 안 고치면 다음 `--write` 가 이 수정을 **조용히 되돌린다.**
    (이 저장소가 반복해서 당한 형태다: 고쳤는데 다음 라운드에 되살아난다.)"""
    print("\n  ── 생성기 대조 (r30_costume_assets.py 가 같은 좌표를 갖는가) ──")
    try:
        import r30_costume_assets as G
    except Exception as e:
        bad("생성기를 import 하지 못했다(%s) — 되돌림 위험을 확인할 수 없다." % e); return
    for short, fn, name in (("cyber", G.cyber, "BasePad"), ("mine", G.miner, "Vein1")):
        gen = [s for s in fn() if s.name == name][0].pts
        gen = [(round(a, 4), round(b, 4)) for a, b in gen]
        m = load_manifest(short)
        got = [(round(a, 4), round(b, 4)) for a, b in
               [q for q in m["propShapes"] if q.name == name][0].pts]
        if gen != got:
            bad("생성기의 %s '%s' 가 출하본과 다르다 — 다음 --write 가 되돌린다.\n"
                "      생성기 %s\n      출하본 %s" % (short, name, gen, got))
        else:
            ok("%-6s '%s' 생성기 == 출하본 (%d점)" % (short, name, len(got)))


def main():
    control = "--control" in sys.argv
    if "--search" in sys.argv:
        search_vein(); return
    if "--apply" in sys.argv:
        do_apply(); return

    print("=" * 104)
    print("  r31 — 코스튬 채움 조각을 «면»으로: 프롭 채움 = 2.2배 재묘사, 번짐 반폭 %.6f H = %.3f pt @0.75"
          % (HALF, HALF * PT_PER_H * SHIP))
    print("=" * 104)
    AP.selftest()

    global GIT_BEFORE
    GIT_BEFORE = git_before()
    if GIT_BEFORE:
        for k, v in GIT_BEFORE.items():
            drift = max(max(abs(a[0]-b[0]), abs(a[1]-b[1])) for a, b in zip(v, CONTROL[k]))
            print("  git HEAD 의 %-6s '%s' 와 CONTROL 의 최대 좌표차 %.5f H (%s)"
                  % (k[0], k[1], drift, "일치" if drift < 5e-4 else "★ 갈라짐 — CONTROL 을 고쳐라"))
    table = CONTROL if control else REPLACE
    if control:
        print("\n  ★★ --control : **옛(결함) 좌표**를 도로 끼워 넣는다. 아래에서 빨간불이 켜져야 한다.")

    print("\n  ── 전/후 대조 (rho_in) ─────────────────────────────────────────────")
    print("     %-8s %-10s %10s %10s %10s   %s" % ("코스튬", "조각", "전", "후", "상한", "판정"))
    for short, name in (("cyber", "BasePad"), ("mine", "Vein1")):
        # ★ 「전」은 파일이 아니라 CONTROL(= 수정 전 출하 좌표)에서 읽는다 —
        #   고친 뒤에 파일을 읽으면 전/후가 같아져 **대조가 조용히 죽는다.**
        #   CONTROL 이 실제 출하본이었는지는 git 으로 확인할 수 있다(부록 참조).
        r0 = rho_in(GIT_BEFORE[(short, name)] if GIT_BEFORE else CONTROL[(short, name)])
        a = patched(short, table); pa = [p for p in a["propShapes"] if p.name == name][0]
        r1 = rho_in(pa.pts)
        print("     %-8s %-10s %10.4f %10.4f %10.4f   %s"
              % (short, name, r0, r1, HALF, "채워짐" if r1 <= HALF else "**구멍 %.2f pt**" % ((r1-HALF)*PT_PER_H*SHIP)))

    for short, label in (("cyber", "C1 사이버 홀로콘솔"), ("mine", "C3 광부 광맥벽")):
        audit(short, patched(short, table), label)

    # 안 건드린 두 종은 «그대로 통과»를 확인한다(음성 대조)
    for short, label in (("office", "C2 오피스(무수정 — 대조)"), ("arcane", "C4 대마법사(무수정 — 대조)")):
        audit(short, load_manifest(short), label)

    if not control:
        generator_agrees()

    print("\n" + "=" * 104)
    if control:
        print("  --control 결과: 검출 %d건. **0이면 이 게이트는 아무것도 안 잡는다.**" % len(FAIL))
        sys.exit(0 if FAIL else 1)
    print("  결과: 위반 %d건" % len(FAIL))
    sys.exit(1 if FAIL else 0)


if __name__ == "__main__":
    main()
