# -*- coding: utf-8 -*-
"""FX 6종 + PET 6종의 **현행 프로덕션 좌표**를 R 배수로 옮긴 것.
출처: Interaction/AppearanceShapeBuilder.cs · CharacterFxRenderer.cs · CharacterPetRenderer.cs
단위 = 머리 반경 R, 원점 = 각 아이템의 로컬 원점(코드와 같다)."""
import math
from rig import Shape, W, BASELINE_TOTAL_H, BASELINE_HEAD_R, MIN_STROKE_PT, PT_PER_UNIT, SHIP_SCALE

def poly(cx, cy, r, n, start_deg=0.0, ry=None):
    ry = r if ry is None else ry
    return [(cx + math.cos(math.radians(start_deg) + 2*math.pi*i/n)*r,
             cy + math.sin(math.radians(start_deg) + 2*math.pi*i/n)*ry) for i in range(n)]

# ── 현행 상수 (코드에서 그대로) ────────────────────────────────────────────────
FX_STROKE_RATIO   = 0.022       # CharacterFxRenderer/CharacterPetRenderer.StrokeRatio
SPARKLE_ARM       = 0.85        # SparkleArmInR
DUST_RADIUS       = 0.50        # BuildCrescents(p.Lines, r*0.5)
BUBBLE_MIN        = 0.58        # BubbleMinRadiusInR
BUBBLE_START      = 0.90        # BubbleStartScale
LEAF_LEN          = 1.15        # LeafLengthInR
BALL_R_IN_HEIGHT  = 0.055       # BallRadiusInHeight
PLANE_HALFSPAN    = 0.75        # PlaneWingSpanInR
CURSOR_SIZE       = 0.90        # CursorSizeInR
BALLOON_R         = 0.80        # BalloonRadiusInR
BALLOON_STRING    = 1.70        # BalloonStringInR
SNAIL_SIZE        = 1.00
SNAIL_SHELL_R     = 0.68
SNAIL_CORE_R      = 0.15
SNAIL_CX, SNAIL_CY = -0.15, 0.66

BALL_SEG, SNAIL_SHELL_SEG, SNAIL_CORE_SEG, BUBBLE_SEG, BALLOON_SEG = 12, 14, 8, 12, 12

def footprint_diameter_in_R(scale=SHIP_SCALE):
    """BuildDot: diameter = max(2 * 0.9 * Height*0.022, MinStrokeWorld)."""
    stroke = BASELINE_TOTAL_H * scale * FX_STROKE_RATIO
    d = max(2.0 * 0.9 * stroke, MIN_STROKE_PT / PT_PER_UNIT)
    return d / (BASELINE_HEAD_R * scale)

def dust_crescent(radius, index):
    rr = radius * (1.0 if index == 0 else 0.65)
    off = 0.0 if index == 0 else radius * 0.55
    return [(math.cos(math.radians(a))*rr, math.sin(math.radians(a))*rr*0.7 + off)
            for a in [(-10 + 200*k/5.0) for k in range(6)]]

def leaf_blade(l):
    return [(-0.50*l, 0.0), (-0.20*l, 0.26*l), (0.14*l, 0.30*l),
            (0.50*l, 0.0), (0.14*l, -0.30*l), (-0.20*l, -0.26*l)]

def leaf_stem(l):
    return [(-0.50*l, 0.0), (-0.86*l, -0.16*l)]

def plane_body(w):
    return [(w, 0.0), (-0.75*w, 0.62*w), (-0.42*w, 0.0), (-0.75*w, -0.62*w)]

def plane_fold(w):
    return [(w, 0.0), (-0.42*w, 0.0), (-0.75*w, -0.62*w)]

def cursor_arrow(s):
    return [(0,0), (0,-s), (0.24*s,-0.72*s), (0.40*s,-1.02*s),
            (0.56*s,-0.94*s), (0.40*s,-0.64*s), (0.66*s,-0.62*s), (0,0)]

def balloon_string(r):
    s = r * BALLOON_STRING
    return [(0,0), (0.10*r, 0.247*s), (-0.08*r, 0.500*s), (0.09*r, 0.753*s), (0.0, s)]

def balloon_body(r):
    rad = r * BALLOON_R
    cy = r * BALLOON_STRING + rad
    return [(math.cos(math.radians(-90 + i*360.0/BALLOON_SEG))*rad*0.92,
             cy + math.sin(math.radians(-90 + i*360.0/BALLOON_SEG))*rad)
            for i in range(BALLOON_SEG)]

def snail_foot(s):
    return [(-0.95*s, 0.10*s), (-0.50*s, 0.0), (0.50*s, 0.0), (0.92*s, 0.30*s), (1.02*s, 0.70*s)]

BALL_R = (BASELINE_TOTAL_H * BALL_R_IN_HEIGHT) / BASELINE_HEAD_R   # 0.5687 R
FOOT_D = footprint_diameter_in_R()

# ── 현행 세트 ────────────────────────────────────────────────────────────────
FX_NOW = {
 "없음":   [],
 "발자국": [Shape("Dot", poly(0,0, FOOT_D*0.5, 16), True, filled=True)],
 "반짝임": [Shape("CrossV", [(0,-SPARKLE_ARM),(0,SPARKLE_ARM)], False),
            Shape("CrossH", [(-SPARKLE_ARM,0),(SPARKLE_ARM,0)], False)],
 "먼지":   [Shape("Crescent0", dust_crescent(DUST_RADIUS,0), False),
            Shape("Crescent1", dust_crescent(DUST_RADIUS,1), False)],
 "물방울": [Shape("Ring", poly(0,0, BUBBLE_MIN*BUBBLE_START, BUBBLE_SEG), True)],
 "나뭇잎": [Shape("Blade", leaf_blade(LEAF_LEN), True),
            Shape("Stem", leaf_stem(LEAF_LEN), False)],
}
PET_NOW = {
 "작은공":   [Shape("Ring", poly(0,0, BALL_R, BALL_SEG), True),
              Shape("Spoke", [(0,0),(BALL_R,0)], False, tone=1)],
 "종이비행기":[Shape("Body", plane_body(PLANE_HALFSPAN), True),
              Shape("Fold", plane_fold(PLANE_HALFSPAN), False, tone=1)],
 "커서친구": [Shape("Arrow", cursor_arrow(CURSOR_SIZE), False)],
 "풍선":     [Shape("String", balloon_string(1.0), False, tone=1),
              Shape("Body", balloon_body(1.0), True)],
 "달팽이":   [Shape("Foot", snail_foot(SNAIL_SIZE), False),
              Shape("Shell", poly(SNAIL_CX*SNAIL_SIZE, SNAIL_CY*SNAIL_SIZE,
                                  SNAIL_SHELL_R*SNAIL_SIZE, SNAIL_SHELL_SEG), True),
              Shape("Core", poly(SNAIL_CX*SNAIL_SIZE, SNAIL_CY*SNAIL_SIZE,
                                 SNAIL_CORE_R*SNAIL_SIZE, SNAIL_CORE_SEG), True, tone=1)],
}

# ══════════════════════════════════════════════════════════════════════════════
# 제안안 A — **좌표만** 고친다(렌더러 무변경). 규칙 1 / 정원 / 보조색을 전부 통과시킨다.
# ══════════════════════════════════════════════════════════════════════════════
def ngon_max(r, w=W):
    """반지름 r(R 배수)의 정n각형이 '변 >= 1획'을 지킬 수 있는 최대 n."""
    return int(math.floor(math.pi / math.asin(min(1.0, w / (2.0 * r)))))

# ── FX ──
FOOT_SOLE = [(-0.40, 0.10), (-0.02, 0.00), (0.56, 0.04)]           # 옆에서 본 밑창
SPARKLE_V, SPARKLE_H = 1.00, 0.68                                   # 팔 길이를 다르게 → '+'가 아니다

def dust_crescent_v2(radius, index):
    """분할 5 -> 3. 200도를 3등분(66.7도)해 현이 획을 넘긴다. 작은 쪽을 0.65 -> 0.80으로."""
    rr = radius * (1.0 if index == 0 else 0.88)
    off = 0.0 if index == 0 else radius * 0.80
    return [(math.cos(math.radians(a))*rr, math.sin(math.radians(a))*rr*0.7 + off)
            for a in [(-10 + 200*k/3.0) for k in range(4)]]

BUBBLE_MIN_V2, BUBBLE_SEG_V2 = 0.62, 9
LEAF_STEM_V2 = lambda l: [(-0.50*l, 0.0), (-0.98*l, -0.24*l)]

# ── PET ──
BALL_SEG_V2 = 9
# 공의 '바퀴살'(중심->테)을 **솔기 호**로 바꾼다 — 살은 바퀴를 뜻하고, 솔기는 부피를 뜻한다.
def ball_seam(r, bulge=0.28, n=4):
    """테 위 (0,+r)과 (0,-r)을 잇고 +x로 bulge 만큼 부푼 원호. 구(球)의 큰 원으로 읽힌다."""
    rc = (r*r + bulge*bulge) / (2*bulge)      # 세 점을 지나는 원의 반지름
    cx = bulge - rc
    half = math.asin(min(1.0, r/rc))
    return [(cx + math.cos(t)*rc, math.sin(t)*rc)
            for t in [(-half + 2*half*k/(n-1)) for k in range(n)]]
PLANE_HALFSPAN_V2 = 1.00
def plane_fold_v2(w): return [(w, 0.0), (-0.42*w, 0.0)]             # 몸 변과 겹치던 두 번째 변 제거
CURSOR_SIZE_V2 = 1.40
CURSOR_HEAD = [(0,0), (0,-1.00), (0.26,-0.74), (0.50,-0.64), (0.78,-0.62)]
CURSOR_TAIL = [(0.26,-0.74), (0.42,-1.06), (0.66,-0.96), (0.50,-0.64)]
SNAIL_SHELL_R_V2, SNAIL_CORE_R_V2, SNAIL_CY_V2 = 0.78, 0.26, 0.76
SNAIL_CX_V2 = -0.30   # 현행 -0.15에서 뒤로 — 더듬이가 껍데기에 붙어 보이는 것을 푼다
SNAIL_SHELL_SEG_V2, SNAIL_CORE_SEG_V2 = 12, 4

def scale_pts(pts, s): return [(x*s, y*s) for x, y in pts]

FX_A = {
 "없음":   [],
 "발자국": [Shape("Sole", FOOT_SOLE, False)],
 "반짝임": [Shape("CrossV", [(0,-SPARKLE_V),(0,SPARKLE_V)], False),
            Shape("CrossH", [(-SPARKLE_H,0),(SPARKLE_H,0)], False)],
 "먼지":   [Shape("Crescent0", dust_crescent_v2(DUST_RADIUS,0), False),
            Shape("Crescent1", dust_crescent_v2(DUST_RADIUS,1), False)],
 "물방울": [Shape("Ring", poly(0,0, BUBBLE_MIN_V2*BUBBLE_START, BUBBLE_SEG_V2), True)],
 "나뭇잎": [Shape("Blade", leaf_blade(LEAF_LEN), True),
            Shape("Stem", LEAF_STEM_V2(LEAF_LEN), False)],
}
PET_A = {
 "작은공":   [Shape("Ring", poly(0,0, BALL_R, BALL_SEG_V2), True),
              Shape("Seam", ball_seam(BALL_R), False, tone=1)],
 "종이비행기":[Shape("Body", plane_body(PLANE_HALFSPAN_V2), True),
              Shape("Fold", plane_fold_v2(PLANE_HALFSPAN_V2), False, tone=1)],
 "커서친구": [Shape("Head", scale_pts(CURSOR_HEAD, CURSOR_SIZE_V2), True),
              Shape("Tail", scale_pts(CURSOR_TAIL, CURSOR_SIZE_V2), True, tone=1)],
 "풍선":     [Shape("String", balloon_string(1.0), False, tone=1),
              Shape("Body", balloon_body(1.0), True)],
 "달팽이":   [Shape("Foot", snail_foot(SNAIL_SIZE), False),
              Shape("Shell", poly(SNAIL_CX_V2, SNAIL_CY_V2, SNAIL_SHELL_R_V2, SNAIL_SHELL_SEG_V2), True),
              Shape("Core", poly(SNAIL_CX_V2, SNAIL_CY_V2, SNAIL_CORE_R_V2, SNAIL_CORE_SEG_V2, 0.0), True, tone=1)],
}
