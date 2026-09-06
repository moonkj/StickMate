# -*- coding: utf-8 -*-
"""FX 6종 + PET 6종의 **2026-09-06 현행 프로덕션 좌표** 거울.

★ 왜 `appearance.py`를 안 고치고 파일을 새로 뒀는가
   `appearance.py`의 FX_NOW/PET_NOW는 **2026-09-01 시점의 스냅샷**이다(제안 A 이전).
   그 파일을 덮어쓰면 "제안 A가 무엇을 바꿨는가"의 증거가 사라진다. 그래서 옛 스냅샷은
   그대로 두고, 오늘 코드를 다시 읽어 옮긴 거울을 여기 새로 만든다.

출처(2026-09-06 HEAD에서 직접 읽음):
  Interaction/AppearanceShapeBuilder.cs
  Interaction/CharacterFxRenderer.cs    (호출부 인자 — 반지름/획/각수 상한)
  Interaction/CharacterPetRenderer.cs   (호출부 인자 + 색 배정 tone)
단위 = 머리 반경 R · 원점 = 각 아이템의 로컬 원점(코드와 같다).
"""
import math
from rig import Shape, W, BASELINE_TOTAL_H, BASELINE_HEAD_R, MIN_STROKE_PT, PT_PER_UNIT, SHIP_SCALE


def poly(cx, cy, r, n, start_deg=0.0, ry=None):
    ry = r if ry is None else ry
    return [(cx + math.cos(math.radians(start_deg) + 2 * math.pi * i / n) * r,
             cy + math.sin(math.radians(start_deg) + 2 * math.pi * i / n) * ry) for i in range(n)]


# ── 현행 상수 (AppearanceShapeBuilder.cs, 2026-09-06) ────────────────────────────
FX_STROKE_RATIO   = 0.022       # CharacterFxRenderer/CharacterPetRenderer.StrokeRatio
SPARKLE_ARM       = 1.00        # SparkleArmInR              (09-01: 0.85)
SPARKLE_H_RATIO   = 0.68        # SparkleHorizontalArmRatio  (09-01: 없음 — 가로=세로)
DUST_RADIUS       = 0.50        # DustRadiusInR / 호출부 리터럴 r*0.5f
BUBBLE_MIN        = 0.62        # BubbleMinRadiusInR         (09-01: 0.58)
BUBBLE_MAX        = 0.80        # BubbleMaxRadiusInR
BUBBLE_START      = 0.90        # CharacterFxRenderer.BubbleStartScale
LEAF_LEN          = 1.15        # LeafLengthInR
BALL_R_IN_HEIGHT  = 0.055       # BallRadiusInHeight
PLANE_HALFSPAN    = 1.00        # PlaneWingSpanInR           (09-01: 0.75)
CURSOR_SIZE       = 1.40        # CursorSizeInR              (09-01: 0.90)
BALLOON_R         = 0.80        # BalloonRadiusInR
BALLOON_STRING    = 1.70        # BalloonStringInR
SNAIL_SIZE        = 1.00        # SnailSizeInR
SNAIL_SHELL_R     = 0.78        # SnailShellRadiusRatio      (09-01: 0.68)
SNAIL_CORE_R      = 0.26        # SnailShellCoreRatio        (09-01: 0.15)
SNAIL_CX, SNAIL_CY = -0.30, 0.76  # SnailShellCenterX/YRatio (09-01: -0.15, 0.66)

# 각수는 빌더가 Mathf.Min(호출부 상한, 자기 상수)로 자른다 — **실효값**을 적는다.
#   CharacterPetRenderer 지역 상수 12/14/8은 빌더 상수 9/12/4에 잘려 사문(死文)이다.
BUBBLE_SEG      = 9             # BubbleSegments             (09-01: 12)
BALL_SEG        = 9             # BallSegments               (09-01: 12)
SNAIL_SHELL_SEG = 12            # SnailShellSegments         (09-01: 14)
SNAIL_CORE_SEG  = 4             # SnailCoreSegments          (09-01: 8)
BALLOON_SEG     = 12            # BalloonBody 내부 리터럴

BALL_SEAM_BULGE = 0.4924        # BallSeamBulgeRatio
BALL_SEAM_PTS   = 4             # BallSeamPoints


def footprint_diameter_in_R(scale=SHIP_SCALE):
    """BuildDot: diameter = max(2 * (Height*0.022) * 0.9, MinStrokeWorld). 09-01과 **같다**."""
    stroke = BASELINE_TOTAL_H * scale * FX_STROKE_RATIO
    d = max(2.0 * 0.9 * stroke, MIN_STROKE_PT / PT_PER_UNIT)
    return d / (BASELINE_HEAD_R * scale)


def dust_crescent(radius, index):
    """DustCrescent — Segments=3, 작은 쪽 배수 0.88 / 올림 0.80 (09-01: 5 / 0.65 / 0.55)."""
    rr = radius * (1.0 if index == 0 else 0.88)
    off = 0.0 if index == 0 else radius * 0.80
    return [(math.cos(math.radians(a)) * rr, math.sin(math.radians(a)) * rr * 0.7 + off)
            for a in [(-10 + 200 * k / 3.0) for k in range(4)]]


def leaf_blade(l):
    return [(-0.50 * l, 0.0), (-0.20 * l, 0.26 * l), (0.14 * l, 0.30 * l),
            (0.50 * l, 0.0), (0.14 * l, -0.30 * l), (-0.20 * l, -0.26 * l)]


def leaf_stem(l):
    """LeafStem — 끝점 (−0.98, −0.24) (09-01: −0.86, −0.16)."""
    return [(-0.50 * l, 0.0), (-0.98 * l, -0.24 * l)]


def plane_body(w):
    return [(w, 0.0), (-0.75 * w, 0.62 * w), (-0.42 * w, 0.0), (-0.75 * w, -0.62 * w)]


def plane_fold(w):
    """PlaneFold — 2점 (09-01: 3점, 세 번째 변이 몸 변과 완전 중복)."""
    return [(w, 0.0), (-0.42 * w, 0.0)]


def ball_seam(r):
    """BallSeam — 중심에서 뻗던 바퀴살을 솔기 호로 (09-01: [(0,0),(r,0)])."""
    bulge = r * BALL_SEAM_BULGE
    rc = (r * r + bulge * bulge) / (2 * bulge)
    cx = bulge - rc
    half = math.asin(min(1.0, r / rc))
    return [(cx + math.cos(t) * rc, math.sin(t) * rc)
            for t in [(-half + 2 * half * k / (BALL_SEAM_PTS - 1)) for k in range(BALL_SEAM_PTS)]]


def cursor_arrow(s):
    """CursorArrow — 여전히 **한 획**이다(스펙 4-2의 머리/꼬리 분할은 미이식).
    마지막 점이 첫 점과 같아 열린 선으로도 닫혀 보인다 — 중복점은 검산에서 뺀다."""
    return [(0, 0), (0, -s), (0.26 * s, -0.74 * s), (0.42 * s, -1.06 * s),
            (0.66 * s, -0.96 * s), (0.50 * s, -0.64 * s), (0.78 * s, -0.62 * s)]


def balloon_string(r):
    s = r * BALLOON_STRING
    return [(0, 0), (0.10 * r, 0.247 * s), (-0.08 * r, 0.500 * s), (0.09 * r, 0.753 * s), (0.0, s)]


def balloon_body(r):
    rad = r * BALLOON_R
    cy = r * BALLOON_STRING + rad
    return [(math.cos(math.radians(-90 + i * 360.0 / BALLOON_SEG)) * rad * 0.92,
             cy + math.sin(math.radians(-90 + i * 360.0 / BALLOON_SEG)) * rad)
            for i in range(BALLOON_SEG)]


def snail_foot(s):
    return [(-0.95 * s, 0.10 * s), (-0.50 * s, 0.0), (0.50 * s, 0.0),
            (0.92 * s, 0.30 * s), (1.02 * s, 0.70 * s)]


BALL_R = (BASELINE_TOTAL_H * BALL_R_IN_HEIGHT) / BASELINE_HEAD_R   # 0.56867 R
FOOT_D = footprint_diameter_in_R()                                 # 0.40952 R = 1.19 W

# ── 현행 세트 (2026-09-06) ──────────────────────────────────────────────────────
FX_NOW = {
    "없음": [],
    # ★ 미해소 — 여전히 둥근 점 하나. 스펙 4-2의 열린 3점 밑창은 미이식.
    "발자국": [Shape("Dot", poly(0, 0, FOOT_D * 0.5, 16), True, filled=True)],
    "반짝임": [Shape("CrossV", [(0, -SPARKLE_ARM), (0, SPARKLE_ARM)], False),
               Shape("CrossH", [(-SPARKLE_ARM * SPARKLE_H_RATIO, 0),
                                (SPARKLE_ARM * SPARKLE_H_RATIO, 0)], False)],
    "먼지": [Shape("Crescent0", dust_crescent(DUST_RADIUS, 0), False),
             Shape("Crescent1", dust_crescent(DUST_RADIUS, 1), False)],
    "물방울": [Shape("Ring", poly(0, 0, BUBBLE_MIN * BUBBLE_START, BUBBLE_SEG), True)],
    "나뭇잎": [Shape("Blade", leaf_blade(LEAF_LEN), True),
               Shape("Stem", leaf_stem(LEAF_LEN), False)],
}

PET_NOW = {
    "작은공": [Shape("Ring", poly(0, 0, BALL_R, BALL_SEG), True),
               Shape("Seam", ball_seam(BALL_R), False, tone=1)],
    "종이비행기": [Shape("Body", plane_body(PLANE_HALFSPAN), True),
                   Shape("Fold", plane_fold(PLANE_HALFSPAN), False, tone=1)],
    # ★ 미해소 — 한 획 · 보조색 0개(정원 1개).
    "커서친구": [Shape("Arrow", cursor_arrow(CURSOR_SIZE), False)],
    "풍선": [Shape("String", balloon_string(1.0), False, tone=1),
             Shape("Body", balloon_body(1.0), True)],
    "달팽이": [Shape("Foot", snail_foot(SNAIL_SIZE), False),
               Shape("Shell", poly(SNAIL_CX * SNAIL_SIZE, SNAIL_CY * SNAIL_SIZE,
                                   SNAIL_SHELL_R * SNAIL_SIZE, SNAIL_SHELL_SEG), True),
               Shape("Core", poly(SNAIL_CX * SNAIL_SIZE, SNAIL_CY * SNAIL_SIZE,
                                  SNAIL_CORE_R * SNAIL_SIZE, SNAIL_CORE_SEG, 0.0), True, tone=1)],
}
