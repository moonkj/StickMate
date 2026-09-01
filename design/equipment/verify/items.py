# -*- coding: utf-8 -*-
"""HEAD / EYES / NECK / BACK 재설계. 좌표 = 머리 중심 원점, +x 진행 방향, 단위 R."""
import math, rig
from rig import Shape, polar, W

SH  = rig.SHOULDER_R      # -1.31818
HIP = rig.HIP_R           # -5.09091
TL  = rig.TORSO_R         #  3.77273
NECKY = SH + 0.04         # 목 부착선(현행 NeckCollarRiseRatio 유지)
COLLARY = SH + 0.10       # 망토 옷깃선

# ============================================================================
# HEAD — 얹지 말고 감싼다 / 챙은 두께가 있다 / 끝은 점으로 수렴
# ============================================================================
def cap():
    """0 야구모자. 관이 옆머리를 감싸 내려오고(−0.22R), 챙은 뿌리가 두껍고 끝이 뾰족하다."""
    crown = [(-0.94,-0.22), (-1.02, 0.36), (-0.72, 1.02), (0.00, 1.24),
             ( 0.72, 1.00), ( 1.00, 0.34), (0.94,-0.06), (0.60, 0.04), (-0.40, 0.06)]
    brim  = [(-0.40, 0.06), (0.60, 0.04), (1.36,-0.06), (1.92,-0.34),
             ( 1.20,-0.42), (0.40,-0.44), (-0.42,-0.40)]
    return [Shape("HatCrown", crown, filled=True),
            Shape("HatBrim",  brim,  filled=True, tone=1)]

def beanie():
    """1 털모자. 카테고리에서 가장 깊이 눌러쓴다(−0.64R). 접힌 단(cuff)이 감쌈을 만든다."""
    crown = [(-0.96,-0.06), (-1.06, 0.52), (-0.62, 1.16), (0.00, 1.32),
             ( 0.62, 1.14), ( 1.06, 0.50), (0.96,-0.06)]
    cuff  = [(-0.96,-0.06), (0.96,-0.06), (1.04,-0.54), (0.64,-0.64),
             (-0.64,-0.64), (-1.04,-0.54)]
    # ★ 2026-09-02 거울 어긋남 수정 — 이 줄만 프로덕션과 달랐다(중심 1.62·반지름 0.30).
    #   프로덕션은 폼폼 **꼭대기**를 고정하고 중심을 유도한다(BeaniePomCrestRiseRatio=0.40):
    #   관 꼭대기 1.32 + (0.40 − 0.28) = 중심 1.44, 반지름 0.28, 꼭대기 1.72.
    pom   = rig.poly(-0.10, 1.44, 0.28, 10, 90.0)
    return [Shape("BeanieCrown", crown, filled=True),
            Shape("BeanieCuff",  cuff,  filled=True),
            Shape("BeaniePom",   pom,   filled=True, tone=1)]

def fedora():
    """2 중절모. 챙이 앞뒤로 다 나가되 앞이 길다. 챙 뿌리 두께 0.58R(1.69획)."""
    brim  = [(-1.68, 0.24), (-0.98, 0.10), (0.98, 0.06), (2.06, 0.18),
             ( 1.24,-0.30), (0.20,-0.42), (-1.00,-0.34)]
    crown = [(-0.98, 0.10), (-0.92, 0.86), (-0.42, 1.16), (0.42, 1.14),
             ( 0.92, 0.82), ( 0.98, 0.06)]
    band  = [(-0.98, 0.10), (0.98, 0.06)]
    return [Shape("FedoraBrim",  brim,  filled=True),
            Shape("FedoraCrown", crown, filled=True),
            Shape("FedoraBand",  band,  loop=False, tone=1)]

def crown_hat():
    """3 왕관. 얹는 물건이라 밑이 뚫린다(HatCoverLocalY = +inf). 봉우리는 점으로 수렴."""
    body = [(-0.98, 0.02), (-0.88, 1.28), (-0.46, 0.62), (0.00, 1.52),
            ( 0.46, 0.62), ( 0.88, 1.28), (0.98, 0.02),
            ( 0.60,-0.10), (-0.60,-0.10)]
    rim  = [(-0.98, 0.02), (-0.60,-0.10), (0.60,-0.10), (0.98, 0.02)]
    return [Shape("CrownBody", body, filled=True),
            Shape("CrownRim",  rim,  loop=False, tone=1)]

def beret():
    """4 베레모. 뒤로 처진 비대칭 덩어리. 밑변이 곧 테(보조색 선)."""
    body = [(-1.46,-0.10), (-1.02, 0.62), (-0.20, 1.06), (0.62, 0.90),
            ( 0.98, 0.54), ( 0.92, 0.02), (-0.30,-0.02)]
    rim  = [(0.92, 0.02), (-0.30,-0.02), (-1.46,-0.10)]
    return [Shape("BeretBody", body, filled=True),
            Shape("BeretRim",  rim,  loop=False, tone=1)]

def straw():
    """5 밀짚모자. 챙이 가장 넓고(앞뒤 2.2R/2.0R) 관이 가장 낮다."""
    brim  = [(-2.06, 0.28), (-0.86, 0.10), (0.86, 0.08), (2.18, 0.26),
             ( 1.30,-0.24), (0.20,-0.36), (-1.20,-0.22)]
    crown = [(-0.86, 0.10), (-0.74, 0.92), (0.00, 1.14), (0.74, 0.90), (0.86, 0.08)]
    band  = [(-0.86, 0.10), (0.86, 0.08)]
    return [Shape("StrawBrim",  brim,  filled=True),
            Shape("StrawCrown", crown, filled=True),
            Shape("StrawBand",  band,  loop=False, tone=1)]

HEAD = {"야구모자":cap(), "털모자":beanie(), "중절모":fedora(),
        "왕관":crown_hat(), "베레모":beret(), "밀짚모자":straw()}
# 모자가 선언하는 커버선(HatCoverLocalY) — 머리 중심 기준 R
COVER = {"야구모자":0.06, "털모자":-0.06, "중절모":0.08,
         "왕관":float('inf'), "베레모":0.02, "밀짚모자":0.08}

# ============================================================================
# EYES — 가리개는 불투명하다. 눈은 '렌즈 안'이 아니라 '가리개 옆'에만 그린다.
# ============================================================================
EYE_DX, EYE_HW, EYE_HH = 0.62, 0.34, 0.24   # 그려지는 눈(아래 검산에서 유도)

def drawn_eye(sx):
    """드러난 눈 — 채운 아몬드. 끝 두 개가 점으로 수렴한다."""
    return [(sx*(EYE_DX-EYE_HW), 0.0), (sx*(EYE_DX-0.06),  EYE_HH),
            (sx*(EYE_DX+EYE_HW), 0.02), (sx*(EYE_DX+0.02), -EYE_HH)]

def sunglasses():
    """0 선글라스 — 어두운 렌즈 2장 + 코다리. 눈은 보이지 않는다(이름이 그렇게 말한다)."""
    back  = [(-0.28, 0.34), (-0.96, 0.30), (-1.02,-0.16), (-0.44,-0.44)]
    front = [( 0.28, 0.36), ( 0.32,-0.46), ( 1.06,-0.20), ( 1.00, 0.32)]
    bridge= [(-0.28, 0.34), (0.00, 0.46), (0.28, 0.36)]
    return [Shape("SunglassLensBack",  back,  filled=True),
            Shape("SunglassLensFront", front, filled=True),
            Shape("SunglassBridge",    bridge, loop=False, tone=1)]

def round_glasses():
    """1 동그란안경 — 두꺼운 테. 렌즈는 불투명하다(투명 렌즈는 이 배율에서 그릴 수 없다)."""
    b = rig.poly(-0.62, 0.02, 0.40, 12); f = rig.poly(0.62, 0.02, 0.40, 12)
    bridge = [b[1], (0.0, 0.50), f[5]]
    return [Shape("RoundLensBack",  b, filled=True),
            Shape("RoundLensFront", f, filled=True),
            Shape("RoundBridge",    bridge, loop=False, tone=1)]

def goggles():
    """2 고글 — 카테고리 최대 판 + 좌우로 똑같이 뻗는 스트랩(머리를 감는다)."""
    strap = [(-1.52,-0.24), (-1.52, 0.22), (-1.06, 0.40), (-0.66, 0.62),
             ( 0.66, 0.62), ( 1.06, 0.40), ( 1.52, 0.22), (1.52,-0.24),
             ( 1.04,-0.06), ( 0.66, 0.16), (-0.66, 0.16), (-1.04,-0.06)]
    lens  = [(-0.66, 0.16), (0.66, 0.16), (1.04,-0.06), (0.84,-0.50),
             ( 0.20,-0.62), (-0.20,-0.62), (-0.84,-0.50), (-1.04,-0.06)]
    return [Shape("GoggleStrap", strap, filled=True, tone=1),
            Shape("GoggleLens",  lens,  filled=True)]

def monocle():
    """3 외알안경 — 앞쪽 눈만 알로 가리고, **가려지지 않은 뒤쪽 눈이 드러난다**(보조색)."""
    pod = rig.poly(0.62, 0.02, 0.36, 12)
    chain = [pod[9], (0.44,-0.76), (0.76,-1.16)]
    return [Shape("MonoclePod",  pod,   filled=True),
            Shape("MonocleChain",chain, loop=False),
            Shape("MonocleEye",  drawn_eye(-1), filled=True, tone=1)]

def browline():
    """4 뿔테안경 — 굵은 눈썹테 아래 렌즈 2장. 테가 주색 덩어리, 렌즈가 보조색."""
    bar  = [(-1.06, 0.14), (-0.98, 0.58), (0.98, 0.56), (1.06, 0.12),
            ( 0.24, 0.10), (-0.24, 0.12)]
    lensb= [(-0.24, 0.12), (-1.06, 0.14), (-0.98,-0.32), (-0.44,-0.50)]
    lensf= [( 0.24, 0.10), ( 0.44,-0.52), ( 1.00,-0.34), (1.06, 0.12)]
    return [Shape("BrowlineBar",       bar,   filled=True, tone=1),
            Shape("BrowlineLensBack",  lensb, filled=True),
            Shape("BrowlineLensFront", lensf, filled=True)]

def patch():
    """5 안대 — 앞쪽 눈을 천으로 덮고, **가려지지 않은 뒤쪽 눈이 드러난다**(보조색)."""
    cover = [(0.28, 0.36), (1.00, 0.30), (0.96,-0.36), (0.30,-0.30)]
    strap = [polar(122, 1.02), (0.28, 0.36), (0.30,-0.30), polar(238, 1.02)]
    return [Shape("PatchCover", cover, filled=True),
            Shape("PatchStrap", strap, loop=False),
            Shape("PatchEye",   drawn_eye(-1), filled=True, tone=1)]

EYES = {"선글라스":sunglasses(), "동그란안경":round_glasses(), "고글":goggles(),
        "외알안경":monocle(), "뿔테안경":browline(), "안대":patch()}
EYE_FRONT_ONLY = {"외알안경","안대"}

# ============================================================================
# NECK — 목 밑동(NECKY)에 걸린다. 몸통은 선 하나이므로 폭이 곧 존재감이다.
# ============================================================================
def bowtie():
    """0 나비넥타이 — 매듭이 획에 먹히던 옛 결함을 매듭 확대로 닫는다(0.91획 -> 1.63획)."""
    L = [(-0.86, NECKY+0.34), (-0.28, NECKY+0.02), (-0.86, NECKY-0.34), (-0.98, NECKY)]
    R = [( 0.86, NECKY+0.34), ( 0.98, NECKY),      ( 0.86, NECKY-0.34), ( 0.28, NECKY+0.02)]
    K = [(-0.28, NECKY+0.30), (0.28, NECKY+0.30), (0.28, NECKY-0.30), (-0.28, NECKY-0.30)]
    return [Shape("BowTieLeftWing", L, filled=True),
            Shape("BowTieRightWing",R, filled=True),
            Shape("BowTieKnot",     K, filled=True, tone=1)]

def stripedtie():
    """1 줄무늬타이 — blade 폭을 0.15R(0.87획)에서 0.34R(1.98획)로 넓힌다. 줄무늬는 1개."""
    ky = NECKY
    knot  = [(-0.36, ky+0.30), (0.36, ky+0.30), (0.26, ky-0.28), (-0.26, ky-0.28)]
    b0 = ky-0.28; L = TL*0.55
    blade = [(-0.34, b0), (0.34, b0), (0.40, b0-L*0.72), (0.00, b0-L), (-0.40, b0-L*0.72)]
    stripe= [(-0.36, b0-L*0.30), (0.36, b0-L*0.30-0.20),
             ( 0.38, b0-L*0.52-0.20), (-0.38, b0-L*0.52)]
    return [Shape("TieKnot",  knot,  filled=True),
            Shape("TieBlade", blade, filled=True),
            Shape("TieStripe",stripe,filled=True, tone=1)]

def scarf():
    """2 목도리 — 목에 감긴 고리(보조색) + 앞뒤 길이가 다른 자락 2개."""
    ty = NECKY
    wrap = [(-0.92, ty+0.30), (0.00, ty+0.06), (0.92, ty+0.30),
            ( 0.96, ty-0.20), (0.00, ty-0.62), (-0.96, ty-0.20)]
    fr = TL*0.40; bk = TL*0.62
    tf = [(0.06, ty-0.48), (0.54, ty-0.34), (1.12, ty-fr), (0.58, ty-fr-TL*0.07)]
    tb = [(-0.58, ty-0.32), (-0.06, ty-0.48), (-0.48, ty-bk), (-1.04, ty-bk+TL*0.07)]
    # 자락을 **먼저** 넣는다 — 같은 채움 레이어에서는 나중에 넣은 것이 위로 온다.
    # 목도리는 고리가 자락을 덮어야 "감았다"로 읽힌다(순서가 뒤집히면 자락이 고리 위로 뜬다).
    return [Shape("ScarfTailBack", tb,   filled=True),
            Shape("ScarfTailFront",tf,   filled=True),
            Shape("ScarfWrap",     wrap, filled=True, tone=1)]

def collar_curve(ty, hw=0.78, rise=0.16, dip=0.32, n=5):
    out=[]
    for i in range(n):
        t = i/(n-1)*2-1
        out.append((hw*t, ty + rise - (1-t*t)*dip))
    return out

def bell():
    """3 방울목걸이 — 목줄 + 매달린 방울(보조색). 방울 위 꼭짓점 = 목줄 최저점."""
    ty=NECKY; c=collar_curve(ty); low=ty+0.16-0.32
    return [Shape("Collar", c, loop=False),
            Shape("Bell", rig.poly(0.0, low-0.30, 0.30, 10, 90.0), filled=True, tone=1)]

def pendant():
    """4 펜던트 — 같은 목줄 + 세로로 긴 마름모(원과 종횡비로 갈린다)."""
    ty=NECKY; c=collar_curve(ty); low=ty+0.16-0.32
    hw,hh=0.30,0.64; py=low-hh
    return [Shape("Chain", c, loop=False),
            Shape("Pendant", [(0,low),(hw,py),(0,py-hh),(-hw,py)], filled=True, tone=1)]

def bandana():
    """5 반다나 — 납작한 띠 + 앞으로 늘어진 삼각 자락 하나."""
    ty=NECKY+0.06
    wrap=[(-0.84, ty+0.22), (0.0, ty+0.10), (0.84, ty+0.22),
          ( 0.84, ty-0.22), (0.0, ty-0.42), (-0.84, ty-0.22)]
    tail=[(0.04, ty-0.30), (0.52, ty-0.30), (0.22, ty-0.30-TL*0.30)]
    return [Shape("BandanaWrap", wrap, filled=True),
            Shape("BandanaTail", tail, filled=True, tone=1)]

NECK = {"나비넥타이":bowtie(), "줄무늬타이":stripedtie(), "목도리":scarf(),
        "방울목걸이":bell(), "펜던트":pendant(), "반다나":bandana()}

# ============================================================================
# BACK — 어깨선(COLLARY)에 매달린다. 몸통 선 뒤(sort -1).
# ============================================================================
def cape_outline(length, spread, front_spread, wave, notch=0.0):
    cy=COLLARY; hy=cy-TL*length; drop=cy-hy
    f,b = 0.40, 0.62
    if notch>0:
        a=0.38
        return [(f,cy), (-b,cy+0.04), (-spread, hy+drop*0.14),
                (-spread*(a+0.24), hy-wave*0.30), (-spread*a, hy+drop*notch),
                (-spread*(a-0.34), hy-wave*0.30), (front_spread, hy+drop*0.10)]
    return [(f,cy), (-b,cy+0.04), (-spread, hy+drop*0.14),
            (-spread*0.62, hy-wave*0.35), (-spread*0.14, hy+wave),
            ( front_spread*0.55, hy-wave*0.30), (front_spread, hy+drop*0.10)]

def cape_fold(length, spread, start_back, end_ratio):
    cy=COLLARY; hy=cy-TL*length
    return [(-0.62*start_back, cy-0.10), (-spread*end_ratio, hy+(cy-hy)*0.20)]

def clasp():
    """서명 디테일 하나 — 목을 감아 잠그는 옷깃 띠. (잠금쇠 한 점으로 하면 긴 망토 카드에서
    1.02획까지 쪼그라든다 — 규칙 5의 '예산 못 지키는 디테일은 넣지 않는다'에 걸린다.)"""
    cy=COLLARY
    return [(0.40, cy+0.10), (0.40, cy-0.34), (-0.66, cy-0.38), (-0.66, cy+0.06)]

def cape():      # 0 짧은망토
    return [Shape("CapeOutline", cape_outline(1.35,2.45,0.85,0.22), filled=True),
            Shape("CapeFold",  cape_fold(1.35,2.45,0.35,0.42), loop=False, tone=2),
            Shape("CapeFold2", cape_fold(1.35,2.45,0.72,0.64), loop=False, tone=2),
            Shape("CapeCollar", clasp(), filled=True, tone=1)]

def longcape():  # 1 긴망토(제비꼬리 밑단)
    return [Shape("CapeOutline", cape_outline(1.85,3.10,1.05,0.30,0.42), filled=True),
            Shape("CapeFold",  cape_fold(1.85,3.10,0.35,0.80), loop=False, tone=2),
            Shape("CapeFold2", cape_fold(1.85,3.10,0.72,0.96), loop=False, tone=2),
            Shape("CapeCollar", clasp(), filled=True, tone=1)]

def wing_blade(sign, outer, mid, inner, rise):
    p=[(0.0, SH+0.12), (1.05, SH+0.62), (outer, SH+rise), (outer*0.68, SH+0.30),
       (mid, SH-0.14), (mid*0.60, SH-0.26), (inner, SH-0.74), (0.44, SH-0.46)]
    return [(sign*x, y) for x,y in (p if sign>0 else list(reversed(p)))]

def wings():     # 2 날개
    return [Shape("WingSpine", [(0.0, SH+0.12), (0.0, SH-TL*0.52)], loop=False, tone=1),
            Shape("WingFeatherA", wing_blade(-1,2.30,1.95,1.30,0.96), filled=True),
            Shape("WingFeatherB", wing_blade(+1,2.30,1.95,1.30,0.96), filled=True)]

def backpack():  # 3 배낭 — '대괄호'였던 옛 그림을 상자+뚜껑+버클+끈으로 다시 세운다
    cx=-0.72; cy=SH-TL*0.40; hw=0.78; hh=TL*0.36
    body=[(cx-hw, cy-hh*0.60), (cx-hw, cy+hh*0.46), (cx-hw*0.62, cy+hh*0.86),
          (cx+hw*0.62, cy+hh*0.86), (cx+hw, cy+hh*0.46), (cx+hw, cy-hh*0.60),
          (cx+hw*0.60, cy-hh), (cx-hw*0.60, cy-hh)]
    lid =[(cx-hw*0.96, cy+hh*0.40), (cx-hw*0.66, cy+hh*0.92), (cx+hw*0.66, cy+hh*0.92),
          (cx+hw*0.96, cy+hh*0.40), (cx+hw*0.80, cy+hh*0.06), (cx-hw*0.80, cy+hh*0.06)]
    buck=[(cx-0.30, cy+hh*0.10), (cx+0.30, cy+hh*0.10), (cx+0.30, cy-hh*0.30), (cx-0.30, cy-hh*0.30)]
    strap=[(0.22, SH+0.04), (-0.10, SH-TL*0.14), (cx+hw, cy+hh*0.46)]
    return [Shape("PackBody",  body, filled=True),
            Shape("PackLid",   lid,  filled=True),
            Shape("PackBuckle",buck, filled=True, tone=1),
            Shape("PackStrap", strap, loop=False)]

def poncho():    # 4 판초
    return [Shape("CapeOutline", cape_outline(1.05,1.95,1.55,0.12), filled=True),
            Shape("CapeFold",  cape_fold(1.05,1.95,0.35,0.42), loop=False, tone=2),
            Shape("CapeFold2", cape_fold(1.05,1.95,0.72,0.64), loop=False, tone=2),
            Shape("CapeCollar", clasp(), filled=True, tone=1)]

def fairywings():# 5 요정날개
    def blade(sign):
        p=[(0.0, SH+0.12), (0.84, SH+0.72), (1.62, SH+0.88), (1.39, SH+0.12),
           (1.02, SH-0.52), (0.44, SH-0.40)]
        return [(sign*x,y) for x,y in (p if sign>0 else list(reversed(p)))]
    return [Shape("WingSpine", [(0.0, SH+0.12), (0.0, SH-TL*0.40)], loop=False, tone=1),
            Shape("WingFeatherA", blade(-1), filled=True),
            Shape("WingFeatherB", blade(+1), filled=True)]

BACK = {"짧은망토":cape(), "긴망토":longcape(), "날개":wings(),
        "배낭":backpack(), "판초":poncho(), "요정날개":fairywings()}
