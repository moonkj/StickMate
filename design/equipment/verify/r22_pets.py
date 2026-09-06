# -*- coding: utf-8 -*-
"""R22 — 펫 6종 전면 재설계(사용자 지시 2026-09-06) 좌표 정의 + 검산.

정본 자(尺)는 rig.py 하나다(W = 0.343864 R @ 배율 0.75). 이 파일은 **좌표만** 갖고,
판정 로직은 rig.py / verify_appearance.py와 같은 식을 쓴다.

★ 프로덕션 .cs는 한 줄도 건드리지 않는다. 구현은 coder 소관.
"""
import math
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rig import (W, Shape, bounds, rule_one, self_intersects, profile, max_delta,
                 BASELINE_TOTAL_H, BASELINE_HEAD_R, PT_PER_UNIT, SHIP_SCALE)

# ── 단위 환산 ────────────────────────────────────────────────────────────────
R_PT = BASELINE_HEAD_R * SHIP_SCALE * PT_PER_UNIT          # 머리 반경 1R = ? pt
HEAD_DIA_PT = 2.0 * R_PT
STROKE_PT = W * R_PT                                        # 2.00 pt
OWNER_H_R = BASELINE_TOTAL_H / BASELINE_HEAD_R              # 주인 신장(R 배수) = 10.34 R

# ── 카드 자(尺) ──────────────────────────────────────────────────────────────
# CharacterInfoWindow.BuildIcon: 40 viewBox -> renderSize, stroke = 1.7 * (renderSize/40).
# 즉 어떤 렌더 크기에서도 **획 = 상자의 4.25%**. 40단위 공간에서 W_card = 1.7.
CARD_BOX = 40.0
CARD_W = 1.7
CARD_MARGIN = 2.8            # 기존 12종 아이콘이 실제로 쓰는 여백(2.8 ~ 37.2)
CARD_PX = 44.0               # 이 팀의 판정 기준 크기
CARD_W_PX = CARD_W * CARD_PX / CARD_BOX      # 1.87 px


# ══════════════════════════════════════════════════════════════════════════════
# 도형 생성기
# ══════════════════════════════════════════════════════════════════════════════
def circle(cx, cy, r, n, phase_deg=0.0):
    a0 = math.radians(phase_deg)
    return [(cx + math.cos(a0 + 2 * math.pi * i / n) * r,
             cy + math.sin(a0 + 2 * math.pi * i / n) * r) for i in range(n)]


def ellipse_equal_arc(a, b, n, tilt_deg=0.0, cx=0.0, cy=0.0, phase_deg=0.0):
    """등호장(等弧長) 표본 타원. 균등각 표본은 장축 끝에서 현이 급격히 짧아져
    규칙 1(변 >= 1획)을 못 지킨다 — 실측: a=1.00 b=0.26 n=8 균등각 최단현 0.62획,
    등호장 1.57획."""
    M = 4096
    ts = [2 * math.pi * k / M for k in range(M + 1)]
    pts = [(a * math.cos(t), b * math.sin(t)) for t in ts]
    cum = [0.0]
    for i in range(M):
        cum.append(cum[-1] + math.dist(pts[i], pts[i + 1]))
    total = cum[-1]
    out = []
    for i in range(n):
        target = total * i / n + total * (phase_deg / 360.0)
        target = target % total
        lo, hi = 0, M
        while lo < hi:
            mid = (lo + hi) // 2
            if cum[mid] < target:
                lo = mid + 1
            else:
                hi = mid
        x, y = pts[lo]
        th = math.radians(tilt_deg)
        out.append((cx + x * math.cos(th) - y * math.sin(th),
                    cy + x * math.sin(th) + y * math.cos(th)))
    return out


def ellipse_arc_equal(a, b, n, tilt_deg=0.0, sweep_deg=240.0, start_deg=0.0, cx=0.0, cy=0.0):
    """열린 타원 호. 등호장 표본 n점. **열린 호에는 '개구 >= 3W' 요건이 없다** —
    고리가 아니라 선 하나이기 때문이다(이번 라운드의 핵심 반증)."""
    M = 4096
    t0, t1 = math.radians(start_deg), math.radians(start_deg + sweep_deg)
    ts = [t0 + (t1 - t0) * k / M for k in range(M + 1)]
    pts = [(a * math.cos(t), b * math.sin(t)) for t in ts]
    cum = [0.0]
    for i in range(M):
        cum.append(cum[-1] + math.dist(pts[i], pts[i + 1]))
    total = cum[-1]
    out = []
    th = math.radians(tilt_deg)
    for i in range(n):
        target = total * i / (n - 1)
        lo, hi = 0, M
        while lo < hi:
            mid = (lo + hi) // 2
            if cum[mid] < target:
                lo = mid + 1
            else:
                hi = mid
        x, y = pts[min(lo, M)]
        out.append((cx + x * math.cos(th) - y * math.sin(th),
                    cy + x * math.sin(th) + y * math.cos(th)))
    return out


def dot(cx, cy, diameter, n=4, phase_deg=0.0):
    """캡 지름 = diameter인 '찍은 점'. 구현은 퇴화 2점 선 + lineWidth=diameter이고,
    검산 모델은 반지름 diameter/2의 정n각형이다(FX 발자국이 쓰던 것과 같은 모델)."""
    return circle(cx, cy, diameter * 0.5, n, phase_deg)


# ══════════════════════════════════════════════════════════════════════════════
# PET 0 — 미니 위성 봇  (일반 Lv.1)
# ══════════════════════════════════════════════════════════════════════════════
# ★ 실측으로 뒤집힌 1차안 — a=1.00 b=0.26 를 ±34도로 **거울 대칭**시킨 두 닫힌 타원은
#   숫자를 전부 통과했는데 **화면에서 나비넥타이(X)로 읽혔다**(r22_pets_sheet.png 1차판).
#   원인 두 가지:
#     (1) 닫힌 고리가 '고리'로 읽히려면 최소 개구(2b)가 >= 3W = 1.0316 R 여야 한다.
#         2b = 0.52 R = 1.51 W 는 획 두 줄이 겹쳐 **속이 없는 막대**가 된다.
#     (2) 같은 크기·거울 대칭 두 개는 교차점이 정확히 겹쳐 X 를 만든다.
#   ⇒ 채택안: **고리 하나만 진짜 고리**(개구 3.37W)로 두고, 두 번째는 **열린 호**로 눕힌다.
#     호에는 개구 요건이 없다(선 하나다). "교차하는 두 궤도"는 이 조합으로 읽힌다.
SAT_A, SAT_B, SAT_TILT, SAT_N = 1.30, 0.58, 20.0, 14      # 진짜 고리
SAT_ARC_A, SAT_ARC_B, SAT_ARC_TILT, SAT_ARC_N = 1.30, 0.20, -38.0, 7
SAT_ARC_SWEEP = 250.0
SAT_CORE_D = 1.24     # r = 0.62 > b = 0.58 이라 고리가 핵을 **관통**한다(간격 0).
#                       ★ 1.16(= 2b, 정확히 접선)으로 잡았더니 다각형 근사에서 0.08획 틈이
#                       생겨 규칙 4의 최악 구간에 걸렸다 — '정확히 접함'은 근사 오차에 약하다.

PET0 = [
    Shape("SatCore", dot(0, 0, SAT_CORE_D, n=8), True, tone=0),
    Shape("SatRing", ellipse_equal_arc(SAT_A, SAT_B, SAT_N, +SAT_TILT), True, tone=0),
    Shape("SatArc", ellipse_arc_equal(SAT_ARC_A, SAT_ARC_B, SAT_ARC_N,
                                      SAT_ARC_TILT, sweep_deg=SAT_ARC_SWEEP,
                                      start_deg=-125.0), False, tone=1),
]

# ══════════════════════════════════════════════════════════════════════════════
# PET 1 — 종이비행기 스피릿  (일반 Lv.13)
# ══════════════════════════════════════════════════════════════════════════════
PLANE_W = 1.00
PET1 = [
    Shape("PlaneBody", [(PLANE_W, 0.0), (-0.75 * PLANE_W, 0.62 * PLANE_W),
                        (-0.42 * PLANE_W, 0.0), (-0.75 * PLANE_W, -0.62 * PLANE_W)], True, tone=0),
    Shape("PlaneFold", [(PLANE_W, 0.0), (-0.42 * PLANE_W, 0.0)], False, tone=1),
    # 픽셀 점선 꼬리 — 한 선 안의 두 비드(폭곡선으로 사이를 0으로 눌러 점 두 개로 읽힌다).
    Shape("PlaneTrail", [(-1.12, 0.00), (-1.68, 0.00), (-2.05, 0.12), (-2.45, 0.12)], False, tone=0),
]
PET1_TRAIL_BEAD_D = [0.56, 0.40]      # 비드 굵기(R) — 폭곡선 값

# ══════════════════════════════════════════════════════════════════════════════
# PET 2 — 스틱 캣  (희귀 Lv.19)
# ══════════════════════════════════════════════════════════════════════════════
CAT_HEAD_C = (1.28, 2.34)
CAT_HEAD_R = 0.92
CAT_EAR_R = 1.34            # 귀 끝이 닿는 반지름
CAT_EYE_C = (1.38, 2.39)    # 머리 중심에서 (+0.10, +0.05)
CAT_EYE_D = 0.52


def cat_head():
    pts = []
    for i in range(12):
        a = math.radians(30.0 * i)
        r = CAT_EAR_R if i in (2, 4) else CAT_HEAD_R
        pts.append((CAT_HEAD_C[0] + math.cos(a) * r, CAT_HEAD_C[1] + math.sin(a) * r))
    return pts


CAT_BODY = [
    (0.72, 1.62),     # 목(머리 원 안 — 붙어 있다)
    (-0.20, 1.60),    # 등
    (-0.88, 1.48),    # 엉덩이
    # 감긴 꼬리 — 중심 (-1.52, 2.50) 반지름 0.52 R 의 -70도 -> 210도(280도) 원호, 56도 간격 6점.
    # 현 0.4883 R = 1.42획. 끝점과 시작점 간격 0.6685 R = 1.94획(>= 1.5획)이라 고리가 닫히지 않는다.
    (-1.3421, 2.0114),
    (-1.0154, 2.3742),
    (-1.1336, 2.8479),
    (-1.5924, 3.0149),
    (-1.9874, 2.7279),
    (-1.9703, 2.2400),
]
CAT_LEGS = [(1.18, 0.00), (1.02, 0.85), (-0.70, 0.82), (-0.86, 0.00)]

PET2 = [
    Shape("CatHead", cat_head(), True, tone=0),
    Shape("CatEye", dot(CAT_EYE_C[0], CAT_EYE_C[1], CAT_EYE_D), True, tone=1),
    Shape("CatBody", CAT_BODY, False, tone=0),
    Shape("CatLegs", CAT_LEGS, False, tone=0),
]

# ══════════════════════════════════════════════════════════════════════════════
# PET 3 — 꼬마 유령  (희귀 Lv.24)
# ══════════════════════════════════════════════════════════════════════════════
GHOST_BODY = [
    (1.15, 0.00), (1.06, 0.63), (0.58, 1.13), (0.00, 1.30), (-0.58, 1.13),
    (-1.06, 0.63), (-1.15, 0.00), (-1.42, -0.62), (-2.20, -1.55), (-1.16, -1.06),
    (-0.60, -1.34), (-0.10, -0.92), (0.42, -1.26), (0.86, -0.90), (1.10, -0.52),
]
GHOST_EYES = [(-0.50, 0.34), (0.50, 0.34)]
GHOST_EYE_D = 0.52
GHOST_MOUTH = [(-0.30, -0.18), (0.00, -0.40), (0.30, -0.18)]

PET3 = [
    Shape("GhostBody", GHOST_BODY, True, tone=0),
    Shape("GhostEyes", GHOST_EYES, False, tone=0),
    Shape("GhostMouth", GHOST_MOUTH, False, tone=1),
]

# ══════════════════════════════════════════════════════════════════════════════
# PET 4 — 미니 도트 드래곤  (영웅 Lv.27)
# ══════════════════════════════════════════════════════════════════════════════
# ★ 2차안도 반증됐다 — 목이 짧아 **머리가 몸통 덩어리에 파묻히고**, 날개가 몸통 바로 위에
#   겹쳐서 합쳐진 실루엣이 "봉우리 둘 달린 덩어리" = **빙산/산맥**으로 읽혔다(시트 2차판).
#   ⇒ 3차안(채택): (1) 목을 1.93획 x 2마디로 **길게 세워** 머리를 몸통 위로 빼내고,
#                  (2) 날개를 **엉덩이 쪽으로 물려** 머리와 경쟁하지 않게 하고 크기를 줄인다.
#                  (3) 꼬리를 **위로 감아** S자를 만든다 — 곧은 꼬리는 몸통 덩어리의 연장으로 읽힌다.
DRAGON_BODY = [
    (-1.80, 0.52),    # 0 꼬리 끝(위로 감긴다)
    (-1.40, 0.06),    # 1 꼬리
    (-0.95, -0.16),   # 2 엉덩이
    (-0.25, -0.24),   # 3 몸통(가장 굵다)
    (0.40, -0.10),    # 4 가슴
    (0.86, 0.36),     # 5 목 아래
    (1.10, 0.98),     # 6 목 위
    (1.34, 1.42),     # 7 머리 뒤
    (1.30, 1.92),     # 8 뿔 끝
    (1.76, 1.52),     # 9 눈두덩
    (2.16, 1.22),     # 10 주둥이
]
DRAGON_BODY_WIDTH = [0.55, 1.10, 1.60, 1.85, 1.70, 1.35, 1.10, 1.55, 0.65, 1.55, 0.55]
# ★ 1차안 반증 — 톱니를 **위쪽**에 놓아 봉우리 두 개가 생겼고 드래곤이 아니라 산맥으로 읽혔다.
#   박쥐 날개의 톱니는 **뒷전(아래·뒤)**에 있다. 앞전은 길고 곧게, 뒷전만 손가락 둘로 파낸다.
DRAGON_WING = [
    (0.10, -0.16),   # 뿌리 앞 — 몸통 선 위(간격 0)
    (0.52, 0.86),    # 앞전
    (0.12, 1.36),    # 날개 끝(가장 높다)
    (-0.30, 0.90),   # 뒷전 홈
    (-0.66, 1.06),   # 손가락 2 — 끝보다 0.30R(=1.7pt) 낮다. ★ 같은 높이면 봉우리 둘이 되어
                     #             드래곤이 **왕관/산맥**으로 읽힌다(2차 시트 실측).
    (-1.04, 0.24),   # 뒷전
    (-0.70, -0.19),  # 뿌리 뒤 — 몸통 선 위(간격 0)
]
DRAGON_BREATH = [(2.72, 1.10), (3.31, 0.98)]
DRAGON_BREATH_D = [0.52, 0.30]      # 앞으로 갈수록 가늘어지는 폭곡선(근→원)
DRAGON_GLOW_C = (1.60, 1.48)
DRAGON_GLOW_D = 0.52

PET4 = [
    Shape("DragonBody", DRAGON_BODY, False, tone=0),
    Shape("DragonWing", DRAGON_WING, True, tone=0),
    Shape("DragonBreath", DRAGON_BREATH, False, tone=0),
    Shape("DragonGlow", dot(DRAGON_GLOW_C[0], DRAGON_GLOW_C[1], DRAGON_GLOW_D), True, tone=1),
]

# ══════════════════════════════════════════════════════════════════════════════
# PET 5 — 블랙홀 코어  (전설 Lv.30)
# ══════════════════════════════════════════════════════════════════════════════
# ★ 1차안 반증 — b=0.30 짜리 납작한 타원 두 개는 개구 0.60R = 1.75W 로 **속이 없어서**
#   후광 안에 그은 **X 두 줄**로 읽혔다(위성 봇과 정확히 같은 병). 여기서는 호로 눕히지 않고
#   **개구를 3W 위로 열어** 진짜 고리로 만든다(b = 0.60 -> 개구 1.20R = 3.49W).
#   대신 코어를 **속 찬 원반**(지름 1.34R)으로 키워 고리들이 원반을 **관통**하게 한다(간격 0).
#   ⇒ 위성 봇과의 구조 차: 위성 = 고리 1 + 호 1 + 작은 핵 / 블랙홀 = 후광 1 + 고리 2 + 큰 원반.
BH_HALO_R, BH_HALO_N = 1.22, 12
BH_A, BH_B, BH_TILT, BH_N = 1.22, 0.46, 52.0, 12
BH_CORE_D = 1.00            # 속 찬 원반(전설 금색 글로우가 여기 들어간다). r=0.50 > b=0.46 이라 고리가 원반을 관통 = 간격 0

PET5 = [
    Shape("BhHalo", circle(0, 0, BH_HALO_R, BH_HALO_N), True, tone=0),
    Shape("BhOrbitA", ellipse_equal_arc(BH_A, BH_B, BH_N, +BH_TILT), True, tone=0),
    Shape("BhOrbitB", ellipse_equal_arc(BH_A, BH_B, BH_N, -BH_TILT), True, tone=0),
    Shape("BhCore", dot(0, 0, BH_CORE_D, n=8), True, tone=1),
]

# ══════════════════════════════════════════════════════════════════════════════
# 폭 곡선 — 정점별 선폭(× RenderStroke). None 이면 균일 폭(현행 MakeLine 그대로).
# ★ 지금 CharacterPetRenderer.MakeLine 은 startWidth = endWidth = RenderStroke 만 쓴다.
#   아래 세 항목은 LineRenderer.widthCurve 를 써야 성립한다 — coder 배정 대상.
# ══════════════════════════════════════════════════════════════════════════════
# 이름 -> (중심x, 중심y, 캡 지름). 구현은 **퇴화 2점 선 + lineWidth = 지름**(속이 찬 점)이고,
# 검산 모델만 정다각형이다. 시트 렌더러는 이 표를 보고 **속을 채워** 그린다.
DOTS = {
    "SatCore": (0.0, 0.0, SAT_CORE_D),
    "CatEye": (CAT_EYE_C[0], CAT_EYE_C[1], CAT_EYE_D),
    "DragonGlow": (DRAGON_GLOW_C[0], DRAGON_GLOW_C[1], DRAGON_GLOW_D),
    "BhCore": (0.0, 0.0, BH_CORE_D),
}

WIDTHS = {
    # 비드 두 개 = "픽셀 점선 꼬리". 사이 연결선은 0.02로 눌러 보이지 않게 한다.
    "PlaneTrail": [1.63, 1.63, 0.06, 1.16, 1.16],   # ↑ 정점 4개 + 중간 브레이크 1개(아래 주석)
    # 눈 두 개 = 한 선의 양 끝 캡. 가운데는 0.02.
    "GhostEyes": [1.51, 0.06, 0.06, 1.51],
    # 붓처럼 굵어졌다 가늘어지는 몸통(꼬리 끝 -> 주둥이).
    "DragonBody": DRAGON_BODY_WIDTH,
    "DragonBreath": [1.51, 0.87],
}
# PlaneTrail / GhostEyes 의 폭 목록은 **정점 수와 다르다** — LineRenderer.widthCurve 는
# 선 전체 길이의 0~1 위치에 키를 찍으므로, 비드 경계에서 키를 두 개(같은 t에 큰 값/작은 값)
# 겹쳐 계단을 만든다. 아래 표는 그 키의 값 순서이고, t 위치는 문서 표에 적는다.

PET_NEW = {
    "미니위성봇": PET0,
    "종이비행기스피릿": PET1,
    "스틱캣": PET2,
    "꼬마유령": PET3,
    "미니도트드래곤": PET4,
    "블랙홀코어": PET5,
}

# 현행 6종(비교용) — appearance.py의 PET_NOW + 리틀스틱메이트
import appearance as A                                             # noqa: E402
from appearance import poly as apoly                                # noqa: E402


def mini_figure(h=OWNER_H_R * 0.45):
    r = h * 0.14
    head_y = h - r
    sh_y = h * 0.72
    hip_y = h * 0.40
    out = [Shape("MiniHead", apoly(0, head_y, r, 12), True)]
    out.append(Shape("MiniTorso", [(0, head_y - r), (0, hip_y)], False))
    out.append(Shape("MiniArmB", [(0, sh_y), (-h * 0.10, sh_y - h * 0.30)], False))
    out.append(Shape("MiniArmF", [(0, sh_y), (h * 0.14, sh_y - h * 0.30)], False))
    out.append(Shape("MiniLegB", [(0, hip_y), (-h * 0.10, 0)], False))
    out.append(Shape("MiniLegF", [(0, hip_y), (h * 0.10, 0)], False))
    return out


PET_OLD = {
    "작은공": A.PET_A["작은공"],
    "종이비행기": A.PET_A["종이비행기"],
    "리틀스틱메이트": mini_figure(),
    "커서친구": A.PET_A["커서친구"],
    "풍선": A.PET_A["풍선"],
    "달팽이": A.PET_A["달팽이"],
}


# ══════════════════════════════════════════════════════════════════════════════
# 검산
# ══════════════════════════════════════════════════════════════════════════════
def true_min_edge(sh):
    p = sh.pts
    n = len(p)
    best = None
    for i in range(n if sh.loop else n - 1):
        L = math.dist(p[i], p[(i + 1) % n])
        if L < 1e-9:
            continue
        best = L if best is None else min(best, L)
    return best


def seg_dist(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    L = dx * dx + dy * dy
    t = 0.0 if L < 1e-12 else max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / L))
    return math.hypot(p[0] - (a[0] + dx * t), p[1] - (a[1] + dy * t))


def shape_gap(s1, s2, samples=48):
    """두 도형 중심선 사이 최소 거리(R). 0에 가까우면 '붙어 있다'(규칙 4의 0 쪽)."""
    best = 1e9
    for (A_, B_) in ((s1, s2), (s2, s1)):
        pa, pb = A_.pts, B_.pts
        na = len(pa) if A_.loop else len(pa) - 1
        for i in range(na):
            a, b = pa[i], pa[(i + 1) % len(pa)]
            for k in range(samples + 1):
                q = (a[0] + (b[0] - a[0]) * k / samples, a[1] + (b[1] - a[1]) * k / samples)
                nb = len(pb) if B_.loop else len(pb) - 1
                for j in range(nb):
                    best = min(best, seg_dist(q, pb[j], pb[(j + 1) % len(pb)]))
    return best


def item_bounds(shapes):
    pts = [p for s in shapes for p in s.pts]
    return bounds(pts)


def audit(title, table):
    print("\n╔══════ %s ══════╗" % title)
    fails = 0
    for name, sh in table.items():
        msgs = []
        if not (2 <= len(sh) <= 4):
            msgs.append("정원 %d개 (2~4 밖)" % len(sh))
        acc = sum(1 for s in sh if s.tone == 1)
        if acc != 1:
            msgs.append("보조색 %d개 (정확히 1개여야)" % acc)
        for s in sh:
            v = rule_one(s, W)
            if v:
                msgs.append("%s 규칙1: %s" % (s.name, v))
            if s.loop and self_intersects(s.pts):
                msgs.append("%s 자기교차 %s" % (s.name, self_intersects(s.pts)))
            t = true_min_edge(s)
            if t is not None and t / W < 1.0:
                msgs.append("%s ★최단 실제 변 %.2f획 < 1.0 (45도 함정 포함)" % (s.name, t / W))
        x0, y0, x1, y1 = item_bounds(sh)
        span_x, span_y = x1 - x0, y1 - y0
        mins = " · ".join("%s %.2f획" % (s.name, true_min_edge(s) / W) for s in sh
                          if true_min_edge(s))
        print("  %s %-10s 도형%d 보조색%d  크기 %.2f x %.2f R (%.1f x %.1f pt)"
              % ("✗" if msgs else "✓", name, len(sh), acc, span_x, span_y,
                 span_x * R_PT, span_y * R_PT))
        print("      최단변: %s" % mins)
        for m in msgs:
            print("      - " + m)
            fails += 1
    print("╚══════ 위반 %d건 ══════╝" % fails)
    return fails


def gap_report(table):
    """규칙 4 — 따로 읽혀야 하는 두 조각의 중심선 간격은 0 또는 >= 1.5획."""
    print("\n── 규칙 4 (조각 사이 간격: 0 또는 >= 1.5획 = %.4f R) ──" % (1.5 * W))
    bad = 0
    for name, sh in table.items():
        rows = []
        for i in range(len(sh)):
            for j in range(i + 1, len(sh)):
                g = shape_gap(sh[i], sh[j])
                mark = "붙음(0)" if g < 0.02 else ("%.2f획" % (g / W))
                ok = g < 0.02 or g >= 1.5 * W
                if not ok:
                    bad += 1
                rows.append("%s%s↔%s %s" % ("" if ok else "✗", sh[i].name, sh[j].name, mark))
        print("  %-10s %s" % (name, " | ".join(rows)))
    print("  → 최악 구간(0 < 간격 < 1.5획) 건수: %d" % bad)
    return bad


def centered_profile(shapes):
    x0, y0, x1, y1 = item_bounds(shapes)
    cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
    moved = [Shape(s.name, [(x - cx, y - cy) for x, y in s.pts], s.loop, s.filled, s.tone)
             for s in shapes]
    return profile(moved)


def pair_silhouette(table):
    print("\n── 쌍별 실루엣 차 (72구간 x 5도, bbox 중심 정렬, 획 배수) ──")
    ks = list(table.keys())
    pr = {k: centered_profile(table[k]) for k in ks}
    worst = (None, 1e9)
    for i in range(len(ks)):
        for j in range(i + 1, len(ks)):
            v = max_delta(pr[ks[i]], pr[ks[j]]) / W
            if v < worst[1]:
                worst = ((ks[i], ks[j]), v)
    for i in range(len(ks)):
        row = []
        for j in range(len(ks)):
            if i == j:
                row.append("   ·  ")
            else:
                row.append("%6.2f" % (max_delta(pr[ks[i]], pr[ks[j]]) / W))
        print("  %-10s %s" % (ks[i], " ".join(row)))
    print("  최소 %.2f획 (%s vs %s)%s"
          % (worst[1], worst[0][0], worst[0][1], "" if worst[1] >= 1.0 else "   ✗ < 1.0"))
    return worst


# ── 카드 44px 래스터 + IoU ────────────────────────────────────────────────────
def card_map(shapes, box=CARD_PX, margin_frac=CARD_MARGIN / CARD_BOX):
    """아이템을 카드 상자에 맞춘다(기존 BuildIcon과 같은 여백 규약: 2.8/40)."""
    x0, y0, x1, y1 = item_bounds(shapes)
    span = max(x1 - x0, y1 - y0)
    usable = box * (1.0 - 2 * margin_frac)
    k = usable / span
    cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
    out = []
    for s in shapes:
        out.append(Shape(s.name,
                         [(box * 0.5 + (x - cx) * k, box * 0.5 - (y - cy) * k) for x, y in s.pts],
                         s.loop, s.filled, s.tone))
    return out, k


def raster(shapes, box=CARD_PX, stroke_px=CARD_W_PX, ss=4):
    """획 두께를 가진 잉크 마스크. ss배 슈퍼샘플."""
    n = int(box * ss)
    half = stroke_px * 0.5 * ss
    grid = [[False] * n for _ in range(n)]
    segs = []
    for s in shapes:
        p = s.pts
        m = len(p)
        for i in range(m if s.loop else m - 1):
            segs.append((p[i], p[(i + 1) % m]))
    for (a, b) in segs:
        ax, ay = a[0] * ss, a[1] * ss
        bx, by = b[0] * ss, b[1] * ss
        x0 = max(0, int(min(ax, bx) - half - 2))
        x1 = min(n - 1, int(max(ax, bx) + half + 2))
        y0 = max(0, int(min(ay, by) - half - 2))
        y1 = min(n - 1, int(max(ay, by) + half + 2))
        for yy in range(y0, y1 + 1):
            for xx in range(x0, x1 + 1):
                if grid[yy][xx]:
                    continue
                if seg_dist((xx + 0.5, yy + 0.5), (ax, ay), (bx, by)) <= half:
                    grid[yy][xx] = True
    return grid


def iou(g1, g2):
    inter = union = 0
    for r1, r2 in zip(g1, g2):
        for a, b in zip(r1, r2):
            if a or b:
                union += 1
                if a and b:
                    inter += 1
    return 0.0 if union == 0 else inter / union


def ink_frac(g):
    n = sum(1 for row in g for v in row if v)
    return n / (len(g) * len(g[0]))


def card_report(table):
    print("\n── 카드 %d px 검산 (획 %.2f px = 40단위의 %.2f) ──" % (CARD_PX, CARD_W_PX, CARD_W))
    fails = 0
    rasters = {}
    for name, sh in table.items():
        cs, k = card_map(sh)
        worst = min(true_min_edge(s) for s in cs if true_min_edge(s))
        # 조각별 잉크 사각형
        small = []
        for s in cs:
            x0, y0, x1, y1 = bounds(s.pts)
            sp = max(x1 - x0, y1 - y0)
            if sp < 1.5 * CARD_W_PX:
                small.append("%s 잉크사각 %.2f획" % (s.name, sp / CARD_W_PX))
        g = raster(cs)
        rasters[name] = g
        ok = worst >= CARD_W_PX and not small
        if not ok:
            fails += 1
        print("  %s %-10s 최단변 %.2f px (%.2f획) · 잉크 점유 %.1f%%%s"
              % ("✓" if ok else "✗", name, worst, worst / CARD_W_PX, ink_frac(g) * 100,
                 "" if not small else "   ✗ " + " / ".join(small)))
    return fails, rasters


def iou_report(rasters):
    print("\n── 카드 44px 잉크 마스크 IoU (낮을수록 잘 구분된다) ──")
    ks = list(rasters.keys())
    # ★ 교정: 자기 자신과의 IoU는 1.000, 서로 안 겹치는 판은 0.000이어야 한다.
    self_iou = iou(rasters[ks[0]], rasters[ks[0]])
    n = len(rasters[ks[0]])
    empty = [[False] * n for _ in range(n)]
    zero_iou = iou(rasters[ks[0]], empty)
    print("  [교정] 자기자신 IoU = %.3f (기대 1.000) · 빈 판 IoU = %.3f (기대 0.000)"
          % (self_iou, zero_iou))
    if abs(self_iou - 1.0) > 1e-9 or abs(zero_iou) > 1e-9:
        print("  ★ 교정 실패 — 아래 숫자를 전부 폐기하라.")
        return None
    worst = (None, -1.0)
    for i in range(len(ks)):
        row = []
        for j in range(len(ks)):
            if i == j:
                row.append("  ·  ")
            else:
                v = iou(rasters[ks[i]], rasters[ks[j]])
                row.append("%5.3f" % v)
                if j > i and v > worst[1]:
                    worst = ((ks[i], ks[j]), v)
        print("  %-10s %s" % (ks[i], " ".join(row)))
    print("  최악(가장 닮은) 쌍: %s ↔ %s  IoU %.3f" % (worst[0][0], worst[0][1], worst[1]))
    return worst


def ring_pair_report(rasters, table):
    print("\n── ★ 1번(궤도 링) ↔ 6번(교차 링) 전용 구분 검산 ──")
    a, b = "미니위성봇", "블랙홀코어"
    v = iou(rasters[a], rasters[b])
    pa, pb = centered_profile(table[a]), centered_profile(table[b])
    md = max_delta(pa, pb) / W
    mean_d = sum(abs(x - y) for x, y in zip(pa, pb)) / len(pa) / W
    ax0, ay0, ax1, ay1 = item_bounds(table[a])
    bx0, by0, bx1, by1 = item_bounds(table[b])
    a_span = max(ax1 - ax0, ay1 - ay0)
    b_span = max(bx1 - bx0, by1 - by0)
    a_ar = (ax1 - ax0) / (ay1 - ay0)
    b_ar = (bx1 - bx0) / (by1 - by0)
    print("  카드 44px 잉크 IoU          %.3f      (낮을수록 다르다)" % v)
    print("  실루엣 최대 차              %.2f획" % md)
    print("  실루엣 평균 차              %.2f획" % mean_d)
    print("  월드 최대 치수              %.2f R (%.1f pt)  vs  %.2f R (%.1f pt)  = %.2f배"
          % (a_span, a_span * R_PT, b_span, b_span * R_PT, b_span / a_span))
    print("  가로세로비                  %.2f  vs  %.2f" % (a_ar, b_ar))
    print("  닫힌 고리 수                %d  vs  %d" % (
        sum(1 for s in table[a] if s.loop), sum(1 for s in table[b] if s.loop)))
    print("  바깥 실루엣                 납작한 렌즈(원형 후광 없음)  vs  완전한 원형 후광")
    print("  링 편평도 b/a               %.2f  vs  %.2f" % (SAT_B / SAT_A, BH_B / BH_A))
    return v, md, mean_d


def worn_size_table(table):
    print("\n── 착용 시 크기 (배율 0.75, 머리 지름 %.2f pt · 주인 신장 %.2f R) ──"
          % (HEAD_DIA_PT, OWNER_H_R))
    for name, sh in table.items():
        x0, y0, x1, y1 = item_bounds(sh)
        w_, h_ = x1 - x0, y1 - y0
        print("  %-10s %5.2f x %5.2f R = %5.1f x %5.1f pt   (주인 신장의 %4.1f%%)"
              % (name, w_, h_, w_ * R_PT, h_ * R_PT, max(w_, h_) / OWNER_H_R * 100))


if __name__ == "__main__":
    print("R = %.4f pt · W = %.6f R = %.2f pt · 머리 지름 = %.2f pt = %.2f W"
          % (R_PT, W, STROKE_PT, HEAD_DIA_PT, 2.0 / W))
    print("주인 신장 = %.2f R" % OWNER_H_R)

    print("\n" + "=" * 78)
    print("A. 현행 6종 (기준선)")
    print("=" * 78)
    n_old = audit("현행 PET 6종", PET_OLD)
    old_worst = pair_silhouette(PET_OLD)
    _, old_r = card_report(PET_OLD)
    iou_report(old_r)

    print("\n" + "=" * 78)
    print("B. 신규 6종 (R22 제안)")
    print("=" * 78)
    n_new = audit("신규 PET 6종", PET_NEW)
    gap_bad = gap_report(PET_NEW)
    worn_size_table(PET_NEW)
    new_worst = pair_silhouette(PET_NEW)
    cf, new_r = card_report(PET_NEW)
    iou_report(new_r)
    ring_pair_report(new_r, PET_NEW)

    print("\n" + "=" * 78)
    print("요약: 현행 위반 %d건 → 신규 위반 %d건 (규칙4 최악구간 %d건, 카드 %d건)"
          % (n_old, n_new, gap_bad, cf))
    print("      쌍별 실루엣 최소차  현행 %.2f획 → 신규 %.2f획" % (old_worst[1], new_worst[1]))
