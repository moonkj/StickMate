# -*- coding: utf-8 -*-
"""★ 2026-09-01 coder — **편집 후 프로덕션**(Interaction/AppearanceShapeBuilder.cs)을 그대로 옮긴 것.
appearance.py의 FX_A/PET_A는 '설계안'이고, 이 파일은 '실제로 코드에 들어간 것'이다.
둘을 나눠 두는 이유: 편집 금지 파일(CharacterFxRenderer/CharacterPetRenderer) 때문에
설계안 중 두 도형(발자국 밑창 · 커서 머리/꼬리 분리)이 이번 라운드에 못 들어갔다."""
import math, sys
sys.path.insert(0, '.')
from rig import Shape, W, BASELINE_TOTAL_H, BASELINE_HEAD_R
import appearance as A
from appearance import (poly, leaf_blade, plane_body, balloon_string, balloon_body,
                        snail_foot, footprint_diameter_in_R, BUBBLE_START, LEAF_LEN,
                        DUST_RADIUS, SNAIL_SIZE)

# ── 편집 후 상수 (AppearanceShapeBuilder.cs) ────────────────────────────────────
SPARKLE_ARM        = 1.00
SPARKLE_H_RATIO    = 0.68
PLANE_HALFSPAN     = 1.00
CURSOR_SIZE        = 1.40
BUBBLE_MIN         = 0.62
SNAIL_SHELL_R      = 0.78
SNAIL_CORE_R       = 0.26
SNAIL_CX, SNAIL_CY = -0.30, 0.76
BUBBLE_SEG, BALL_SEG, SNAIL_SHELL_SEG, SNAIL_CORE_SEG = 9, 9, 12, 4
BALL_SEAM_BULGE_RATIO, BALL_SEAM_POINTS = 0.4924, 4
BALL_R = (BASELINE_TOTAL_H * A.BALL_R_IN_HEIGHT) / BASELINE_HEAD_R
FOOT_D = footprint_diameter_in_R()

def dust_crescent(radius, index):                      # Segments 5 -> 3
    rr = radius * (1.0 if index == 0 else 0.88)
    off = 0.0 if index == 0 else radius * 0.80
    return [(math.cos(math.radians(a))*rr, math.sin(math.radians(a))*rr*0.7 + off)
            for a in [(-10 + 200*k/3.0) for k in range(4)]]

def leaf_stem(l):  return [(-0.50*l, 0.0), (-0.98*l, -0.24*l)]
def plane_fold(w): return [(w, 0.0), (-0.42*w, 0.0)]

def ball_seam(r):
    b  = r * BALL_SEAM_BULGE_RATIO
    rc = (r*r + b*b) / (2*b)
    cx = b - rc
    half = math.asin(min(1.0, r/rc))
    n = BALL_SEAM_POINTS
    return [(cx + math.cos(t)*rc, math.sin(t)*rc)
            for t in [(-half + 2*half*k/(n-1)) for k in range(n)]]

def cursor_arrow(s):
    return [(0,0), (0,-s), (0.26*s,-0.74*s), (0.42*s,-1.06*s),
            (0.66*s,-0.96*s), (0.50*s,-0.64*s), (0.78*s,-0.62*s), (0,0)]

FX_NOW = {
 "없음":   [],
 "발자국": [Shape("Dot", poly(0,0, FOOT_D*0.5, 16), True, filled=True)],   # ★ 미완(편집 금지 파일)
 "반짝임": [Shape("CrossV", [(0,-SPARKLE_ARM),(0,SPARKLE_ARM)], False),
            Shape("CrossH", [(-SPARKLE_ARM*SPARKLE_H_RATIO,0),(SPARKLE_ARM*SPARKLE_H_RATIO,0)], False)],
 "먼지":   [Shape("Crescent0", dust_crescent(DUST_RADIUS,0), False),
            Shape("Crescent1", dust_crescent(DUST_RADIUS,1), False)],
 "물방울": [Shape("Ring", poly(0,0, BUBBLE_MIN*BUBBLE_START, BUBBLE_SEG), True)],
 "나뭇잎": [Shape("Blade", leaf_blade(LEAF_LEN), True),
            Shape("Stem", leaf_stem(LEAF_LEN), False)],
}
PET_NOW = {
 "작은공":   [Shape("Ring", poly(0,0, BALL_R, BALL_SEG), True),
              Shape("Seam", ball_seam(BALL_R), False, tone=1)],
 "종이비행기":[Shape("Body", plane_body(PLANE_HALFSPAN), True),
              Shape("Fold", plane_fold(PLANE_HALFSPAN), False, tone=1)],
 "커서친구": [Shape("Arrow", cursor_arrow(CURSOR_SIZE), False)],            # ★ 미완(편집 금지 파일)
 "풍선":     [Shape("String", balloon_string(1.0), False, tone=1),
              Shape("Body", balloon_body(1.0), True)],
 "달팽이":   [Shape("Foot", snail_foot(SNAIL_SIZE), False),
              Shape("Shell", poly(SNAIL_CX, SNAIL_CY, SNAIL_SHELL_R, SNAIL_SHELL_SEG), True),
              Shape("Core",  poly(SNAIL_CX, SNAIL_CY, SNAIL_CORE_R,  SNAIL_CORE_SEG, 0.0), True, tone=1)],
}
