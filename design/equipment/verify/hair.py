# -*- coding: utf-8 -*-
"""HAIR 6종 재설계.

좌표계: 머리 중심이 원점, +x = 진행 방향, 단위 = 머리 반경 R.
경계를 도는 순서(전부 같다 — 이 순서가 어긋나면 폴리곤이 자기교차한다):
   돔(앞->뒤 바깥)  ->  뒤 커튼(내려갔다 안쪽으로)  ->  두피 안쪽 호(뒤->앞)  ->  앞 커튼(이마선->앞 끝)
앞 커튼의 마지막 점이 돔의 첫 점으로 닫힌다.
"""
import math, rig
from rig import Shape, polar, W

INNER_R = 0.58   # 두피를 파고드는 깊이. 규칙 4 부착 판정선 1-W = 0.656R보다 작아야 한다.

def inner_arc(d_back, d_front, seg=7, r=INNER_R):
    return [polar(d_back + (d_front - d_back) * i / seg, r) for i in range(seg + 1)]

def dome(cap, d0, d1, seg=12):
    return [polar(d0 + (d1 - d0) * i / seg, cap) for i in range(seg + 1)]

def mass(dome_pts, back_curtain, inner_pts, front_curtain):
    return list(dome_pts) + list(back_curtain) + list(inner_pts) + list(front_curtain)

# ── 0 삐친머리 — 바깥 윤곽 자체가 다섯 번 뾰족하다(붙인 삼각형이 아니라 실루엣이 정체) ──
def cowlick():
    spikes = [(  6,1.28),( 24,1.70),( 44,1.30),( 66,1.76),( 90,1.32),
              (114,1.78),(138,1.34),(160,1.72),(184,1.30),(204,1.56)]
    pts = mass([polar(d,r) for d,r in spikes],
               [(-1.30,-1.10), (-0.78,-0.76)],
               inner_arc(196, 76),
               [(0.86, 0.04), (1.12,-0.72)])
    return [Shape("HairMass", pts, filled=True),
            Shape("HairCrest", [polar(126,1.34), polar(142,2.45), polar(154,1.26)],
                  filled=True, tone=1)]

# ── 1 단정한머리 — 길고 곧게 늘어진 생머리(뒤 2.12R). 끝이 점으로 수렴한다 ──
def straight():
    pts = mass(dome(1.58, 10, 206),
               [(-1.46,-0.92), (-1.10,-2.12), (-0.62,-1.26)],
               inner_arc(198, 78),
               [(0.80, 0.10), (1.06,-0.62), (1.28,-1.30)])
    return [Shape("HairMass", pts, filled=True),
            Shape("HairPart", [(-0.14,1.56), (0.26,1.54), (0.44,0.60), (0.04,0.64)],
                  filled=True, tone=1)]

# ── 2 곱슬머리 — 커튼 가장자리가 물결친다(정수리는 액자 1.75R에 막힌다 — 아래 검산) ──
def curly():
    pts = mass(dome(1.62, 8, 208),
               [(-1.90,-0.50), (-1.36,-0.94), (-1.82,-1.42), (-1.18,-1.74), (-0.86,-0.98)],
               inner_arc(200, 74),
               [(0.84, 0.16), (1.44,-0.30), (1.06,-0.86), (1.54,-1.34)])
    return [Shape("HairMass", pts, filled=True),
            Shape("HairCoil", [(1.54,-1.34), (1.94,-1.68), (1.50,-2.10), (1.00,-1.80), (1.20,-1.54)],
                  filled=True, tone=1)]

# ── 3 민머리 — 덩어리가 '없는' 것이 정체. 관자놀이/뒤통수에 남은 테 2조각 ──
def bald():
    def band(d0, d1, seg):
        return [polar(d0+(d1-d0)*i/seg, 1.20) for i in range(seg+1)] + \
               [polar(d1+(d0-d1)*i/seg, 0.58) for i in range(seg+1)]
    return [Shape("HairRimBack",  band(120, 208, 7), filled=True),
            Shape("HairRimFront", band(-28,  26, 4), filled=True, tone=1)]

# ── 4 바가지머리 — 턱선에서 수평으로 자른 단발. '자른 밑선'이 정체 ──
def bowl():
    CAP, CUT, SIDE, FR = 1.62, -0.95, 0.80, 0.46
    s = math.degrees(math.asin(CUT/CAP)); e = 180 - s
    pts  = [polar(s+(e-s)*i/14, CAP) for i in range(15)]
    pts += [(-SIDE, CUT)]
    pts += [(-SIDE+2*SIDE*i/4, FR) for i in range(5)]
    pts += [(SIDE, CUT)]
    return [Shape("HairMass", pts, filled=True),
            Shape("HairFringe", [(-SIDE+2*SIDE*i/4, FR) for i in range(5)], loop=False, tone=1)]

# ── 5 포니테일 — 짧은 덩어리 + 뒤통수에서 묶여 떨어지는 긴 묶음 ──
def ponytail():
    pts = mass(dome(1.56, 12, 200),
               [(-1.34,-0.84), (-0.82,-0.62)],
               inner_arc(194, 76),
               [(0.82, 0.06), (1.10,-0.72)])
    tail = [polar(158,1.22), (-1.86,0.62), (-2.42,-0.10),
            (-1.84,-0.34), (-1.30,-0.46), polar(196,1.06)]
    return [Shape("HairMass", pts, filled=True),
            Shape("HairTail", tail, filled=True, tone=1)]

SET = {"삐친머리":cowlick(), "단정한머리":straight(), "곱슬머리":curly(),
       "민머리":bald(), "바가지머리":bowl(), "포니테일":ponytail()}
