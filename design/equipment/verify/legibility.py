# -*- coding: utf-8 -*-
"""AccessoryNameLegibilityTests.NormalizedCells / Difference 의 파이썬 거울 — 카드 정규화 실루엣 차.
교정(양성 대조): 출하 Patched Hood ↔ 베레모 = 0.100 (테스트 실측 CardDebt, 허용 ±0.02). 깨지면 숫자를 쓰지 마라.
"""
import numpy as np

GRID = 64
STROKE_TO_SPAN = (1.7 / 40.0) / 0.86      # 테스트의 자 눈금(프로덕션 상수가 아니다 — 그 파일 주석 그대로)
MIN_CARD_DIFFERENCE = 0.15


def _bounds(shapes):
    xs = [p[0] for s in shapes for p in s["pts"]]; ys = [p[1] for s in shapes for p in s["pts"]]
    return min(xs), min(ys), max(xs), max(ys)


def _contains(poly, X, Y):
    n = len(poly); inside = np.zeros(X.shape, bool)
    for i in range(n):
        ax, ay = poly[i]; bx, by = poly[(i + 1) % n]
        cond = (ay > Y) != (by > Y)
        with np.errstate(divide="ignore", invalid="ignore"):
            xint = ax + (Y - ay) * (bx - ax) / (by - ay)
        inside ^= cond & (X < xint)
    return inside


def _near_edge(shapes, X, Y, r):
    hit = np.zeros(X.shape, bool)
    for s in shapes:
        p = s["pts"]; n = len(p)
        if n < 2: continue
        edges = n if s["loop"] else n - 1
        for e in range(edges):
            ax, ay = p[e]; bx, by = p[(e + 1) % n]
            dx, dy = bx - ax, by - ay; L2 = dx * dx + dy * dy
            if L2 < 1e-12:
                d = np.hypot(X - ax, Y - ay)
            else:
                t = np.clip(((X - ax) * dx + (Y - ay) * dy) / L2, 0, 1)
                d = np.hypot(X - (ax + dx * t), Y - (ay + dy * t))
            hit |= d <= r
    return hit


def cells(shapes):
    """긴 변이 1 이 되게 정규화한 64×64 격자에서 잉크(채움 안 또는 획 반폭 이내)에 덮인 칸."""
    x0, y0, x1, y1 = _bounds(shapes)
    span = max(x1 - x0, y1 - y0)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    step = 1.0 / GRID; half = span * STROKE_TO_SPAN * 0.5
    I, J = np.meshgrid(np.arange(GRID), np.arange(GRID), indexing="ij")
    X = cx + (-0.5 + (I + 0.5) * step) * span
    Y = cy + (-0.5 + (J + 0.5) * step) * span
    filled = np.zeros(X.shape, bool)
    for s in shapes:
        if s["filled"]: filled |= _contains(s["pts"], X, Y)
    return filled | _near_edge(shapes, X, Y, half)


def difference(a, b):
    union = (a | b).sum()
    return 0.0 if union == 0 else float((a ^ b).sum()) / float(union)


def calibrate(hood, beret, expect=0.100, tol=0.02):
    d = difference(cells(hood), cells(beret))
    return d, abs(d - expect) <= tol
