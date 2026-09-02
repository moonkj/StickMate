# -*- coding: utf-8 -*-
"""★ "착용했을 때 물건으로 읽히는가" — 실측으로 교정한 자(尺).  design-equipment / 2026-09-02

이 파일이 생긴 이유
-------------------
소은/민지가 같은 라운드에 같은 것을 봤다: **털모자가 "모자"가 아니라 "머리 윗부분 색칠"로 읽힌다.**
기존 게이트는 전부 초록이었다(verify.py 위반 0건, 남는 머리 6종 통과). 즉 **목록에 없는 것이
또 나갔다.** 없던 자는 셋이다.

  ① 부조(relief)   — 모자 잉크가 **머리 원 밖으로** 얼마나 나가는가. 각도별.
  ② 단차(step)     — 밑단 바깥 꼭짓점이 머리 원 밖인가. "얹은 것"의 결정적 단서.
  ③ 고리화(ring)   — 채운 작은 도형이 **테두리에 먹혀** 반지로 보이는가(폼폼).
  ④ 몸가림 잔존    — 카드는 **몸 없이** 그린다. 착용하면 목/몸통 획이 아이템을 자른다.
                     카드에서 막대 하나였던 것이 착용하면 얼룩 둘이 되는 자리(짧은망토 클래스프).

★ 교정 (2026-09-02, 실기 캡처 z_head.png 픽셀 실측)
---------------------------------------------------
머리는 반지름 1.000 R 짜리 채운 원반이다 — 캡처에서 200/220/320/340도 네 방향 실측이
1.002 / 0.992 / 0.992 / 1.002 R 로 나왔다(원 맞춤: 중심 (223.5, 300.5), R = 131.5 px).
같은 캡처에서 잰 **현행 털모자의 각도별 부조**와 이 파일의 계산이 일치해야 한다:

    각도    캡처 실측     이 파일(폴리곤, 획 팽창 0)
     0도    -0.028 R      -0.029 R
    20도    +0.132 R      +0.133 R
    30도    +0.158 R      +0.158 R
    50도    +0.182 R      +0.180 R
    60도    +0.272 R      +0.272 R

`python3 wornread.py` 가 이 교정을 **먼저** 돌린다. 깨지면 그 뒤 숫자는 전부 폐기다.

획 두께가 둘이라는 사실 (2026-09-02 M6, StickConfig)
----------------------------------------------------
  · 낱선           MinStrokeScreenPoints      = 2.00pt  -> W_line
  · 채움 경계선    MinFillOutlineScreenPoints = 1.00pt  -> W_fill
배율 0.75에서 W_line = 0.34386 R, W_fill = 0.21818 R. **W_fill은 배율 0.591 이상에서 상수**다
(비례항 0.048*s 가 1pt 바닥을 넘기 때문). 이 파일은 **둘 다** 보고한다 — 어느 쪽을 골랐는지
숨기면 그게 다음 거짓 통과다.
"""
import math

BASELINE_STROKE_W = 0.048
BASELINE_HEAD_R   = 0.22
PT_PER_UNIT       = 846.0 / 24.0          # 35.25
MIN_LINE_PT       = 2.0
MIN_FILL_PT       = 1.0


def w_line(scale=0.75):
    return max(BASELINE_STROKE_W * scale, MIN_LINE_PT / PT_PER_UNIT) / (BASELINE_HEAD_R * scale)


def w_fill(scale=0.75):
    return max(BASELINE_STROKE_W * scale, MIN_FILL_PT / PT_PER_UNIT) / (BASELINE_HEAD_R * scale)


def w_card(span_R, icon=44.0, fit=0.86, stroke=1.7 * 44 / 40):
    """카드 아이콘의 획을 **R 단위**로 환산한다.
    AccessoryCardIcon.TryBuild: scale = size*FitFraction/span (span은 월드 = R 단위와 비례).
    따라서 획_R = stroke * span_R / (size*fit).  ★ 이 값이 W_fill보다 훨씬 작다는 것이
    '카드에선 공, 착용하면 반지'의 정체다."""
    return stroke * span_R / (icon * fit)


# ---------------------------------------------------------------------------
# 기하 — 폴리곤/캡슐의 방사 최대 반경
# ---------------------------------------------------------------------------
def _seg_ray_hit(p, q, ux, uy):
    """원점에서 방향 (ux,uy)로 쏜 반직선이 선분 p-q와 만나는 t(>=0). 없으면 None.
    t*u = p + s*(q-p) 를 크래머로 푼다:
        den = ux*dy - uy*dx,  t = (px*dy - py*dx)/den,  s = (px*uy - py*ux)/den"""
    dx, dy = q[0] - p[0], q[1] - p[1]
    den = ux * dy - uy * dx
    if abs(den) < 1e-14:
        return None
    t = (p[0] * dy - p[1] * dx) / den
    s = (p[0] * uy - p[1] * ux) / den
    if s < -1e-12 or s > 1.0 + 1e-12 or t < 0.0:
        return None
    return t


def ray_radius(shapes, deg, rho=0.0):
    """머리 중심에서 deg 방향으로 쏜 반직선이 **잉크**(다각형 ⊕ rho, 선 캡슐 반지름 rho)와
    만나는 최대 반경. 잉크가 없으면 0."""
    a = math.radians(deg)
    ux, uy = math.cos(a), math.sin(a)
    best = 0.0
    for s in shapes:
        pts = s.pts
        n = len(pts)
        segs = n if s.loop else n - 1
        for i in range(segs):
            p, q = pts[i], pts[(i + 1) % n]
            t = _seg_ray_hit(p, q, ux, uy)
            if t is not None:
                best = max(best, t)
            if rho > 0.0:
                # 선분을 rho로 부풀린 캡슐의 반직선 교점(가장 먼 것)
                best = max(best, _capsule_ray(p, q, rho, ux, uy))
        if rho > 0.0:
            for p in pts:
                best = max(best, _circle_ray(p, rho, ux, uy))
    return best


def _circle_ray(c, r, ux, uy):
    b = c[0] * ux + c[1] * uy
    cc = c[0] * c[0] + c[1] * c[1] - r * r
    disc = b * b - cc
    if disc < 0:
        return 0.0
    t = b + math.sqrt(disc)
    return max(0.0, t)


def _capsule_ray(p, q, r, ux, uy):
    """직사각형 부분(선분을 법선으로 ±r 민 것)의 네 변과의 교점."""
    dx, dy = q[0] - p[0], q[1] - p[1]
    L = math.hypot(dx, dy)
    if L < 1e-12:
        return _circle_ray(p, r, ux, uy)
    nx, ny = -dy / L * r, dx / L * r
    corners = [(p[0] + nx, p[1] + ny), (q[0] + nx, q[1] + ny),
               (q[0] - nx, q[1] - ny), (p[0] - nx, p[1] - ny)]
    best = 0.0
    for i in range(4):
        t = _seg_ray_hit(corners[i], corners[(i + 1) % 4], ux, uy)
        if t is not None:
            best = max(best, t)
    return best


# ---------------------------------------------------------------------------
# ① 부조 — 각도별 (머리 원 = 1.0 R)
# ---------------------------------------------------------------------------
def relief(shapes, deg, rho=0.0):
    return ray_radius(shapes, deg, rho) - 1.0


def relief_profile(shapes, rho=0.0, lo=0, hi=180, step=5):
    return [(d, relief(shapes, d, rho)) for d in range(lo, hi + 1, step)]


#: ★ 옆 부조를 재는 각도 띠. 왜 20~60도인가 —
#   0~15도는 머리 원의 **가장 두꺼운 자리**라 어떤 모자든 여기서 나가면 얼굴을 덮는다(감쌈 규칙과 충돌).
#   65도 위는 정수리라 **어떤 모자든 통과한다**(현행 털모자도 90도에선 +0.43R이다).
#   즉 "모자냐 색칠이냐"가 실제로 갈리는 구간이 20~60도다. 소은이 지목한 구간과 같다.
LATERAL_LO, LATERAL_HI = 20, 60


def lateral_relief(shapes, rho=0.0):
    """옆 띠(20~60도, 좌우 모두)에서의 **최소** 부조. 이 값이 곧 '실루엣이 갈라지는가'."""
    vals = []
    for d in range(LATERAL_LO, LATERAL_HI + 1, 5):
        vals.append(relief(shapes, d, rho))
        vals.append(relief(shapes, 180 - d, rho))
    return min(vals)


# ---------------------------------------------------------------------------
# ② 단차 — 밑단 바깥 꼭짓점이 머리 원 밖인가
# ---------------------------------------------------------------------------
def hem_step(shapes, cover_y=None):
    """모자 잉크의 **가장 낮은 바깥 꼭짓점**이 머리 원 밖으로 나간 양(R).
    음수면 밑단이 머리 안쪽에 있어 머리 윤곽이 밑단을 **감싸고 지나간다**(= 색칠로 읽힌다)."""
    best = None
    for s in shapes:
        for x, y in s.pts:
            if y > 0.10:                    # 정수리 쪽 점은 밑단이 아니다
                continue
            d = math.hypot(x, y) - 1.0
            if best is None or d > best:
                best = d
    return best if best is not None else -9.9


# ---------------------------------------------------------------------------
# ③ 고리화 — 채운 도형이 테두리에 먹혀 반지가 되는가
# ---------------------------------------------------------------------------
def inradius(pts):
    """볼록/오목 모두: 다각형 내부 점 중 변까지의 최소거리가 최대인 값(격자+국소 정련)."""
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)

    def dist_to_edges(px, py):
        best = 1e9
        n = len(pts)
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            dx, dy = b[0] - a[0], b[1] - a[1]
            L2 = dx * dx + dy * dy
            t = 0.0 if L2 < 1e-18 else max(0.0, min(1.0, ((px - a[0]) * dx + (py - a[1]) * dy) / L2))
            best = min(best, math.hypot(px - (a[0] + t * dx), py - (a[1] + t * dy)))
        return best

    def inside(px, py):
        c = False
        n = len(pts)
        for i in range(n):
            a, b = pts[i], pts[(i - 1) % n]
            if (a[1] > py) != (b[1] > py) and \
               px < (b[0] - a[0]) * (py - a[1]) / (b[1] - a[1]) + a[0]:
                c = not c
        return c

    best, bx, by = 0.0, 0.0, 0.0
    N = 60
    for i in range(N + 1):
        for j in range(N + 1):
            px = x0 + (x1 - x0) * i / N
            py = y0 + (y1 - y0) * j / N
            if not inside(px, py):
                continue
            d = dist_to_edges(px, py)
            if d > best:
                best, bx, by = d, px, py
    step = max(x1 - x0, y1 - y0) / N
    for _ in range(40):
        improved = False
        for ddx, ddy in ((step, 0), (-step, 0), (0, step), (0, -step),
                         (step, step), (step, -step), (-step, step), (-step, -step)):
            px, py = bx + ddx, by + ddy
            if not inside(px, py):
                continue
            d = dist_to_edges(px, py)
            if d > best:
                best, bx, by = d, px, py
                improved = True
        if not improved:
            step *= 0.5
    return best


def rim_fraction(pts, w):
    """테두리가 반지름에서 먹는 비율. 0.35를 넘으면 눈에 '반지'로 읽힌다.
    (교정: 카드 폼폼 0.20 -> 캡처에서 '흰 공' / 착용 폼폼 0.41 -> 캡처에서 '올리브 링')"""
    rho = inradius(pts)
    return (w * 0.5) / rho if rho > 1e-9 else 9.9


RIM_FRACTION_CEIL = 0.35


# ---------------------------------------------------------------------------
# ④ 몸가림 잔존 — 목/몸통 검은 획이 아이템을 자른 뒤 남는 조각
# ---------------------------------------------------------------------------
def occlusion_pieces(pts, band_half):
    """세로 몸 획(|x| <= band_half)이 도형을 자른 뒤 남는 좌/우 조각의 (폭, 높이).
    반환: [(폭, 높이), ...]  — 몸 획에 걸치지 않으면 원본 하나."""
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    x0, x1 = min(xs), max(xs)
    h = max(ys) - min(ys)
    if x1 <= -band_half or x0 >= band_half:
        return [(x1 - x0, h)]
    out = []
    if x0 < -band_half:
        out.append((-band_half - x0, h))
    if x1 > band_half:
        out.append((x1 - band_half, h))
    return out if out else [(0.0, h)]


def poly_area(pts):
    """다각형 면적(부호 없음)."""
    a = 0.0
    n = len(pts)
    for i in range(n):
        x1, y1 = pts[i]
        x2, y2 = pts[(i + 1) % n]
        a += x1 * y2 - x2 * y1
    return abs(a) * 0.5
