"""R26 — ④ 행동 명령 칸 후보 비교. 눈으로 고른다(합격선은 게이트가 아니라 「무엇으로 읽히는가」)."""
import math
from fanglyph import *
from r26_new import PL, CAP, DISC, W, G0, G1, G2


def arc(name, cx, cy, r, a0, a1, t, seg=8):
    pts = []
    for i in range(seg + 1):
        a = math.radians(a0 + (a1 - a0) * i / seg)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return PL(name, pts, t)


# ── A. 확성기 유지안 — 나팔을 <닫힌 한 조각>으로, 소리선 1개
def megaphone_v2():
    return [
        PL("Horn", [(-8.4, 2.6), (-8.4, -2.6), (5.0, -5.8), (5.0, 5.8), (-8.4, 2.6)], G0),
        arc("Wave", 5.0, 0.0, 5.5, -38, 38, G0),
    ]


# ── B1. 손잡이 구슬 + 곧은 봉 (끝은 점으로)
def baton_b1():
    return [
        DISC("Bulb", 6.0, -7.0, -7.0),
        PL("Shaft", [(-6.4, -6.4), (6.4, 6.4)], G0),
    ]


# ── B2. 3단 테이퍼 (g2 → g0 → g1), 구슬 없음
def baton_b2():
    return [
        CAP("Grip", 6.0, G2, 45.0, -7.2, -7.2),
        PL("Shaft", [(-6.0, -6.0), (2.0, 2.0)], G0),
        PL("Tip", [(1.6, 1.6), (8.4, 8.4)], G1),
    ]


# ── B3. 손잡이(g2) + 봉(g0) + 끝 구슬  ← r26_new 현재값
def baton_b3():
    return [
        CAP("Grip", 7.0, G2, 45.0, -7.4, -7.4),
        PL("Shaft", [(-6.8, -6.8), (2.0, 2.0)], G0),
        DISC("Orb", 6.4, 7.4, 7.4),
    ]


# ── B4. 손잡이(g2) + 봉(g0) + 휘두른 궤적 호(g0, 진짜 곡선)
def baton_b4():
    return [
        CAP("Grip", 6.0, G2, 45.0, -7.6, -7.6),
        PL("Shaft", [(-7.0, -7.0), (4.0, 4.0)], G0),
        arc("Trail", 0.0, 0.0, 11.0, 105, 165, G0),
    ]


# ── B5. 지시봉 + 과녁 점 (「저기로」) — 화살표 대신 목표점
def baton_b5():
    return [
        CAP("Grip", 6.0, G2, 225.0, -7.4, -7.4),
        PL("Shaft", [(-6.8, -6.8), (3.4, 3.4)], G0),
        arc("Ring", 7.8, 7.8, 3.2, -180, 180, G1, seg=16),
    ]


SLOT4 = {
    "A megaphone v2": megaphone_v2,
    "B1 bulb+rod": baton_b1,
    "B2 taper": baton_b2,
    "B3 orb tip": baton_b3,
    "B4 swing arc": baton_b4,
    "B5 target": baton_b5,
}


# ── A2. 나팔(닫힌 한 조각) + 손잡이(용접) + 소리선 1
def megaphone_v3():
    return [
        PL("Horn", [(-8.4, 2.6), (-8.4, -2.6), (5.0, -5.8), (5.0, 5.8), (-8.4, 2.6)], G0),
        PL("Handle", [(-1.0, -4.37), (-1.8, -8.4)], G0),
        arc("Wave", 5.0, 0.0, 6.6, -38, 38, G0),
    ]


# ── A3. 나팔 + 손잡이만(소리선 없음)
def megaphone_v4():
    return [
        PL("Horn", [(-8.4, 3.4), (-8.4, -3.4), (6.2, -7.0), (6.2, 7.0), (-8.4, 3.4)], G0),
        PL("Handle", [(-1.2, -5.35), (-2.0, -9.6)], G0),
    ]
