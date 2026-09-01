# -*- coding: utf-8 -*-
"""★ 남는 머리 비율(headroom) — 모자가 머리 원을 얼마나 남기는가.

2026-09-02 사용자 신고 "털모자 착용시 거의 머리 전체를 가림"의 계량자(尺).

왜 이 파일이 생겼나
--------------------
털모자 밑단을 −0.52 → −0.64로 내린 라운드가 **부작용 24항목을 전부 검산하고도** 이 회귀를
내보냈다. 이유는 하나다 — **"남는 머리 비율"이 검사 목록에 없었다.** 그래서 여기 만든다.

무엇을 재는가 (세 축, 전부 R 단위 · 배율 불변식으로 유도)
--------------------------------------------------------
머리는 **속이 꽉 찬 원반**이다(CharacterPortraitStage.DrawBody의 `AddFilledDisc("HeadFill", …, r)` —
2026-09-01 P1에서 링에서 채움으로 바뀌었다). 그리고 **얼굴이 없다**(DrawEyes=false). 즉 머리의
정체는 **잉크 원반 하나**뿐이고, 모자가 그 위에 다른 색으로 덮인다(ItemCatalog.WornColor가
채도 ≥0.42 · 명도 0.55~0.80을 강제하므로 모자는 흰 잉크와 반드시 구분된다).

  ① 면적비  A_vis / πR²   — 원반 중 모자 잉크에 안 덮인 넓이의 비율. **주 지표.**
  ② 외곽호  Θ_vis [deg]   — 반경 1.0R 원둘레 중 안 덮인 각도. 머리의 윤곽이 얼마나 남는가.
  ③ 세로    h_vis / 2R    — x=0 수직선에서 모자 잉크 아래로 남는 머리 높이의 비율.
                            리더가 손으로 잰 것이 이것이다(단, 리더는 획 두께를 빼고 쟀다).

★ 모자 "잉크"는 채움 다각형 **+ 획 두께 W/2 팽창**이다. 중심선만 재면 안 된다 —
  배율 0.75에서 W/2 = 0.1719R이라 밑단 −0.64R짜리 도형의 실제 잉크는 **−0.812R까지** 내려간다.
  리더 실측(82%)이 실제(90.6%)보다 낙관적이었던 이유가 정확히 이것이다.

계산법 — 스캔라인 구간 대수(정확·순수 파이썬)
---------------------------------------------
캡슐(선분 + 반지름 W/2)은 **볼록**이라 어떤 수평선과의 교집합도 구간 하나다. 채움 다각형은
교차점 정렬로 구간 목록이 된다. 두 종류를 합집합한 뒤 원반의 현(chord)에서 빼면 끝이다.
격자 표본(몬테카를로)을 안 쓰는 이유: 하한 근처에서 표본 잡음이 통과/실패를 뒤집을 수 있다.
"""
import math

# ---------------------------------------------------------------------------
# ★ 하한 상수 — **여기 한 곳에만 적는다.** verify.py는 이 이름을 참조한다.
# ---------------------------------------------------------------------------

#: ★ 하한 ① — 남는 머리의 **두께**(x=0 수직선), 획 배수.
#   근거: 이 프로젝트가 이미 확정한 원칙 "그 배율에서 획 하나보다 얇은 요소는 화면에 존재하지 않는다".
#   남는 머리는 모자 잉크 바로 밑에 붙은 **띠**이고, 그 띠가 획보다 얇으면 모자 윤곽선의 일부로 읽힌다.
#   교정(2026-09-02 실측, 배율 0.75): 털모자 0.55획(사용자가 신고) / 야구 1.19 / 중절 1.23 / 밀짚 1.42 /
#   왕관 2.12 / 베레 2.38. 사용자가 신고한 것과 안 한 것 사이가 0.55~1.19이므로 하한 1.00이 그 창 안에 든다.
HEADROOM_THICKNESS_FLOOR_W = 1.00

#: ★ 하한 ② — 남는 머리의 **면적** 비율. ①만으로는 x=0만 보므로 옆을 다 덮는 모자를 놓친다.
#   교정: 털모자 4.8% / 중절 16.5%(최저 통과) / 야구 17.3 / 밀짚 20.4 / 왕관 34.1 / 베레 38.2 (배율 0.75).
HEADROOM_AREA_FLOOR = 0.12

#: 하한을 재는 배율. 0.75는 출하 기본, 0.60은 **사용자의 실제 저장 배율**(2026-09-02 세이브 실측).
#   두 배율 다 하드 게이트다 — 0.60을 보고만 하면 이번 신고와 같은 회귀가 또 나간다.
HEADROOM_GATE_SCALES = (0.75, 0.60)

#: 설계 목표(하한이 아니라 목표). 하한에 딱 붙은 값은 다음 좌표 수정 한 번에 무너진다.
HEADROOM_THICKNESS_TARGET_W = 1.20

_ARC_SAMPLES = 2880          # 0.125도 간격
_SCAN_LINES = 4001           # y = -1 .. +1


def stroke_in_R(scale=0.75):
    """그 배율에서 실제로 그려지는 액세서리 획 두께(R 배수). rig.stroke_in_R과 같은 식."""
    return max(0.048 * scale, 2.0 / 35.25) / (0.22 * scale)


# ---------------------------------------------------------------------------
# 구간 대수
# ---------------------------------------------------------------------------
def _merge(iv):
    if not iv: return []
    iv = sorted(iv)
    out = [list(iv[0])]
    for a, b in iv[1:]:
        if a <= out[-1][1] + 1e-12: out[-1][1] = max(out[-1][1], b)
        else: out.append([a, b])
    return out


def _poly_spans(pts, y):
    """채운 다각형 내부가 수평선 y와 만나는 구간들."""
    xs = []
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        if (a[1] > y) != (b[1] > y):
            xs.append(a[0] + (y - a[1]) * (b[0] - a[0]) / (b[1] - a[1]))
    xs.sort()
    return [(xs[i], xs[i + 1]) for i in range(0, len(xs) - 1, 2)]


def _capsule_span(a, b, rho, y):
    """캡슐(선분 a-b를 반지름 rho로 부풀린 것)이 수평선 y와 만나는 구간. 볼록이라 하나."""
    lo, hi = math.inf, -math.inf
    # 양 끝 원
    for p in (a, b):
        dy = y - p[1]
        if abs(dy) <= rho:
            hw = math.sqrt(max(0.0, rho * rho - dy * dy))
            lo = min(lo, p[0] - hw); hi = max(hi, p[0] + hw)
    # 가운데 직사각형 — 반평면 4개(cx·x + cy·y + c0 >= 0)를 x 구간에 차례로 물린다
    dx, dy = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dy)
    if L > 1e-12:
        ux, uy = dx / L, dy / L
        nx, ny = -uy, ux
        planes = (
            ( ux,  uy, -( ux * a[0] +  uy * a[1])),          # (p-a)·u >= 0
            (-ux, -uy,  ( ux * b[0] +  uy * b[1])),          # (p-b)·u <= 0
            ( nx,  ny, -( nx * a[0] +  ny * a[1]) + rho),    # (p-a)·n >= -rho
            (-nx, -ny,  ( nx * a[0] +  ny * a[1]) + rho),    # (p-a)·n <=  rho
        )
        rl, rh = -math.inf, math.inf
        ok = True
        for cx, cy, c0 in planes:
            c = cy * y + c0
            if abs(cx) < 1e-12:
                if c < 0: ok = False; break
            elif cx > 0: rl = max(rl, -c / cx)
            else:        rh = min(rh, -c / cx)
        if ok and rl <= rh:
            lo = min(lo, rl); hi = max(hi, rh)
    return None if lo > hi else (lo, hi)


def ink_spans(shapes, y, w):
    """모자 잉크(채움 + 획 W/2 팽창)가 수평선 y에서 차지하는 x 구간들."""
    out = []
    rho = w * 0.5
    for s in shapes:
        pts = s.pts
        if s.filled and len(pts) >= 3:
            out.extend(_poly_spans(pts, y))
        n = len(pts)
        segs = n if s.loop else n - 1
        for i in range(segs):
            sp = _capsule_span(pts[i], pts[(i + 1) % n], rho, y)
            if sp: out.append(sp)
    return _merge(out)


def _covered(shapes, q, w):
    """점 q가 모자 잉크 안인가."""
    rho = w * 0.5
    for s in shapes:
        pts = s.pts; n = len(pts)
        if s.filled and n >= 3:
            inside = False
            for i in range(n):
                a, b = pts[i], pts[(i + 1) % n]
                if (a[1] > q[1]) != (b[1] > q[1]):
                    x = a[0] + (q[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])
                    if q[0] < x: inside = not inside
            if inside: return True
        segs = n if s.loop else n - 1
        for i in range(segs):
            a, b = pts[i], pts[(i + 1) % n]
            dx, dy = b[0] - a[0], b[1] - a[1]
            L2 = dx * dx + dy * dy
            t = 0.0 if L2 < 1e-12 else max(0.0, min(1.0, ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy) / L2))
            if math.hypot(q[0] - (a[0] + dx * t), q[1] - (a[1] + dy * t)) <= rho: return True
    return False


# ---------------------------------------------------------------------------
# 세 축
# ---------------------------------------------------------------------------
def head_area_ratio(shapes, w, head_r=1.0):
    """① 모자 잉크에 안 덮인 머리 원반 면적 / 원반 전체."""
    tot = vis = 0.0
    for k in range(_SCAN_LINES):
        y = -head_r + 2.0 * head_r * k / (_SCAN_LINES - 1)
        half = math.sqrt(max(0.0, head_r * head_r - y * y))
        if half <= 0: continue
        tot += 2 * half
        cov = 0.0
        for a, b in ink_spans(shapes, y, w):
            lo, hi = max(a, -half), min(b, half)
            if hi > lo: cov += hi - lo
        vis += max(0.0, 2 * half - cov)
    return vis / tot if tot > 0 else 0.0


def head_arc_deg(shapes, w, head_r=1.0):
    """② 안 덮인 머리 외곽호 (총합도, 최대 연속호도)."""
    free = []
    for i in range(_ARC_SAMPLES):
        d = 360.0 * i / _ARC_SAMPLES
        a = math.radians(d)
        free.append(not _covered(shapes, (math.cos(a) * head_r, math.sin(a) * head_r), w))
    step = 360.0 / _ARC_SAMPLES
    total = sum(free) * step
    best = run = 0
    for i in range(2 * _ARC_SAMPLES):          # 한 바퀴 더 돌아 0도에 걸친 호를 잇는다
        if free[i % _ARC_SAMPLES]: run += 1; best = max(best, run)
        else: run = 0
    return total, min(360.0, best * step)


def head_depth_ratio(shapes, w, head_r=1.0):
    """③ x=0 수직선에서 모자 잉크의 **가장 아래**부터 머리 밑까지 남는 높이 / 지름.
    리더가 손으로 잰 것이 이 축이다(다만 리더는 획 두께를 빼고 쟀다)."""
    bottom = -head_r
    lo = head_r
    hit = False
    for k in range(_SCAN_LINES):
        y = -head_r + 2.0 * head_r * k / (_SCAN_LINES - 1)
        if any(a <= 0.0 <= b for a, b in ink_spans(shapes, y, w)):
            if y < lo: lo = y
            hit = True
    if not hit: return 1.0, bottom
    return (lo - bottom) / (2 * head_r), lo


def measure(shapes, w):
    a = head_area_ratio(shapes, w)
    tot, run = head_arc_deg(shapes, w)
    d, ybot = head_depth_ratio(shapes, w)
    return dict(area=a, arc=tot, arc_run=run, depth=d, ink_bottom=ybot)


# ---------------------------------------------------------------------------
def polygon_area(pts):
    s = 0.0; n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        s += a[0] * b[1] - b[0] * a[1]
    return abs(s) * 0.5


def clip_below(pts, cover_y, loop=True):
    """AppendClippedBelowCover의 ClipLoop(반평면 y<=cover Sutherland-Hodgman)과 같은 식."""
    if cover_y == float('inf'): return list(pts)
    out = []; n = len(pts)
    for i in range(n):
        cur = pts[i]; nxt = pts[(i + 1) % n]
        ci = cur[1] <= cover_y; ni = nxt[1] <= cover_y
        if ci: out.append(cur)
        if ci != ni:
            t = (cover_y - cur[1]) / (nxt[1] - cur[1])
            out.append((cur[0] + (nxt[0] - cur[0]) * t, cover_y))
    return out


def hair_visible_area(hair_shapes, hat_shapes, cover_y, w):
    """모자를 쓴 뒤 화면에 실제로 남는 머리카락 면적(R²). 커버선에서 자르고 → 모자 잉크로 가린다."""
    vis = 0.0
    for hs in hair_shapes:
        if not hs.filled: continue
        pts = clip_below(hs.pts, cover_y)
        if len(pts) < 3: continue
        ys = [p[1] for p in pts]; y0, y1 = min(ys), max(ys)
        if y1 - y0 < 1e-9: continue
        N = 1201
        for k in range(N):
            y = y0 + (y1 - y0) * k / (N - 1)
            sp = _merge(_poly_spans(pts, y))
            if not sp: continue
            cov = _merge(ink_spans(hat_shapes, y, w)) if hat_shapes else []
            free = 0.0
            for a, b in sp:
                cur = [(a, b)]
                for ca, cb in cov:
                    nxt = []
                    for x0, x1 in cur:
                        if cb <= x0 or ca >= x1: nxt.append((x0, x1)); continue
                        if ca > x0: nxt.append((x0, ca))
                        if cb < x1: nxt.append((cb, x1))
                    cur = nxt
                free += sum(x1 - x0 for x0, x1 in cur)
            vis += free * (y1 - y0) / (N - 1)
    return vis
