# -*- coding: utf-8 -*-
"""R2 ④ 팩 「우천」 6종 — 좌표와 전 게이트.  좌표계: 머리 중심 원점, +x = 진행, 단위 R."""
import math, sys
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/character/verify")
import rig, items, headroom, hair, r2_body as B
from rig import Shape
B.print_cal()

R_INK = 1.171932
SH, HIP, TL = rig.SHOULDER_R, rig.HIP_R, rig.TORSO_R
NECKY = SH + 0.04
def Wof(s): return headroom.stroke_in_R(s)
W75, W60, W35, W100 = Wof(0.75), Wof(0.60), Wof(0.35), Wof(1.00)

# ============================ 팩 「우천」 =====================================
def newspaper_hat():
    """HEAD 신문 모자 — 한 채움 + 접힌 자리 낱선. 밴드가 아니라 **배(舟)**다.
    · 잉크 밑단을 +0.208 R 위로 올려 안경이 규칙을 지키며 들어갈 자리를 만든다(⑤ 참조)
    · 부피를 위가 아니라 **옆(±1.72)**으로 낸다 — 트랩 #2(챙은 바깥으로)의 일반형"""
    body = [(-1.72, 0.66), (-1.04, 0.46), (0.00, 0.44), (1.04, 0.46), (1.72, 0.64),
            ( 1.10, 0.96), ( 0.62, 1.30), (0.00, 1.46), (-0.62, 1.28), (-1.10, 0.94)]
    fold = [(-1.34, 0.78), (0.00, 0.72), (1.34, 0.76)]
    return [Shape("PaperHatBody", body, filled=True),
            Shape("PaperHatFold", fold, loop=False, tone=1)]
HAT_COVER = 0.44          # HatCoverLocalY = 채움의 밑단

def fogged_glasses():
    """EYES 김 서린 안경 — 코 아래로 내려 쓴 **반달 렌즈** 2장 + 곧은 윗테 1줄.
    · 위치로 갈린다(기본 6종은 전부 y 중심 ~0, 이건 -0.76)
    · 렌즈 밑이 머리 잉크 밖으로 1.06획 나간다 — 턱 아래 실루엣에 돌기를 만드는 첫 EYES"""
    back  = [(-1.06,-0.34), (-0.24,-0.34), (-0.30,-0.64), (-0.65,-0.94), (-1.00,-0.66)]
    front = [( 1.06,-0.34), ( 1.00,-0.66), ( 0.65,-0.94), ( 0.30,-0.64), ( 0.24,-0.34)]
    rim   = [(-1.06,-0.34), (0.00,-0.32), (1.06,-0.34)]
    return [Shape("FogLensBack",  back,  filled=True),
            Shape("FogLensFront", front, filled=True),
            Shape("FogRim",       rim,   loop=False, tone=1)]

def rain_collar():
    """NECK 우의 깃 — 목을 **감지 않는다**. 어깨 아래에서 바깥으로 벌어지는 판 2장.
    · 머리 원반 잠식 0 (기본 6종은 전부 원반을 파고든다 — ② census)
    · 위 끝이 머리 잉크에서 1획 이상 떨어진다(규칙 4의 금지 구간을 아래로 피한다)"""
    L = [(-0.14,-1.72), (-1.30,-1.88), (-1.14,-2.64), (-0.10,-2.24)]
    R = [( 0.14,-1.72), ( 0.10,-2.24), ( 1.14,-2.68), ( 1.30,-1.90)]
    return [Shape("CollarLeft",  L, filled=True),
            Shape("CollarRight", R, filled=True, tone=1)]

def umbrella():
    """BACK 우산 — ★ 팩의 간판. **어깨 위를 쓰는 첫 BACK 아이템**(기본 6종 천장 -0.186 R).
    sort -1이라 본체 잉크를 한 점도 못 먹는다 = 실루엣만 커지고 머리는 그대로다."""
    canopy = [(-2.44, 0.42), (-2.14, 1.02), (-1.60, 1.44), (-1.00, 1.50), (-0.46, 1.22),
              (-0.66, 0.78), (-1.14, 0.92), (-1.68, 0.70), (-2.06, 0.58)]
    pole   = [(-0.12,-2.56), (-1.06, 0.86)]
    hook   = [(-0.12,-2.56), (0.26,-2.92), (0.06,-3.24)]
    return [Shape("UmbrellaCanopy", canopy, filled=True),
            Shape("UmbrellaPole",   pole, loop=False),
            Shape("UmbrellaHook",   hook, loop=False, tone=1)]

PACK = {"HEAD":("신문 모자", newspaper_hat()), "EYES":("김 서린 안경", fogged_glasses()),
        "NECK":("우의 깃", rain_collar()), "BACK":("우산", umbrella())}

# ============================ 게이트 =========================================
def bbox(sh):
    xs=[];ys=[]
    for s in sh:
        for x,y in s.pts: xs.append(x); ys.append(y)
    return min(xs),min(ys),max(xs),max(ys)

def rho_max(pts, n=260):
    """최대 내접원 반경(색면 폭 / 2). 규칙 1-C가 요구하는 값."""
    x0=min(p[0] for p in pts); x1=max(p[0] for p in pts)
    y0=min(p[1] for p in pts); y1=max(p[1] for p in pts)
    best=0.0
    for i in range(n):
        for j in range(n):
            q=(x0+(x1-x0)*i/(n-1), y0+(y1-y0)*j/(n-1))
            if not rig.contains(pts,q): continue
            d=1e9; m=len(pts)
            for k in range(m):
                a,b=pts[k],pts[(k+1)%m]
                dx,dy=b[0]-a[0],b[1]-a[1]; L=dx*dx+dy*dy
                t=0.0 if L<1e-12 else max(0.0,min(1.0,((q[0]-a[0])*dx+(q[1]-a[1])*dy)/L))
                d=min(d, math.hypot(q[0]-(a[0]+dx*t), q[1]-(a[1]+dy*t)))
            best=max(best,d)
    return best


# ---- 프로덕션이 실제로 쓰는 문턱 (테스트 소스에서 확인, 숫자를 베끼지 않고 유도) ----
#  · 규칙 1  : AccessoryStrokeBudgetTests → **출하 배율 0.75 하나**. W = 0.343864 R.
#              같은 파일 185~187행: "다이얼 최소(0.35)는 획이 0.74R이라 어떤 디테일도 불가능한
#              **실루엣 전용 구간**" — 0.35는 게이트가 아니다(내가 0.35로 죽이면 안 된다).
#  · 규칙 1-C: AccessoryFillAreaRuleTests → 배율 0.60/0.75/1.00, 펜은 **FillOutline**(1pt 하한)이라
#              세 배율에서 **같은 값** 0.21818 R. (같은 파일 188~195행이 그 동일성을 잠근다)
#  · 0.35    : 같은 파일 271~305행 — "색면이 0이 아닐 것"만 본다 = ρ_max ≥ W_out(0.35)/2.
def w_out(s): return max(0.048*s, 1.0/(846.0/24.0))/(0.22*s)
W_OUT   = w_out(0.75)          # 0.218182 — 0.60/0.75/1.00에서 동일
W_OUT35 = w_out(0.35)          # 0.368434
assert abs(W_OUT-0.218182)<1e-5 and abs(w_out(0.60)-W_OUT)<1e-6 and abs(w_out(1.00)-W_OUT)<1e-6
assert abs(W35-0.7369)<1e-3, W35     # 테스트 주석의 "0.74R"과 대조

print("== ④-1 규칙 1 (잉크 사각형 ≥1.5획 · 양끝 꺾임 변 ≥1.0획) @ 출하 0.75 ==")
for slot,(dn,sh) in PACK.items():
    bad = rig.report(dn, sh, W75)
    print(f"  {slot:5s} {dn:9s} {'OK' if not bad else ' / '.join(bad)}")
print("\n  [양성 대조] 기본 42종을 같은 자로 재면:")
for lbl,tab in (("HEAD",items.HEAD),("EYES",items.EYES),("NECK",items.NECK),("BACK",items.BACK)):
    v=[n for n,sh in tab.items() if rig.report(n,sh,W75)]
    print(f"    {lbl}: 위반 {len(v)}/6 {v if v else ''}")
print("  [음성 대조] 일부러 깨뜨린 도형이 잡히는가:")
broke = [Shape("Broken", [(0,0),(0.20,0),(0.20,0.20),(0,0.20)], filled=True)]
print(f"    0.20x0.20 사각형 -> {rig.report('x',broke,W75)}")

print("\n== ④-2 규칙 1-C (색면 폭 ρ_max ≥ W_out) — 문턱 %.5f R (0.60/0.75/1.00 공통) =="%W_OUT)
for slot,(dn,sh) in PACK.items():
    for s_ in sh:
        if not s_.filled: continue
        r = rho_max(s_.pts)
        print(f"  {slot:5s} {s_.name:16s} ρ_max {r:.4f} R = {r/W_OUT:.2f} W_out"
              f"   여유 {(r/W_OUT-1)*100:+5.0f}%   {'★ 미달' if r<W_OUT else 'OK'}"
              f"   | 0.35 색면≠0({W_OUT35/2:.4f}): {'OK' if r>=W_OUT35/2 else '★ 색면 0'}")

print("\n== ④-3 실루엣 봉투 (액자 천장 +1.80 R / 손끝 잉크 2.6361 R) ==")
tot=[9,9,-9,-9]
for slot,(dn,sh) in PACK.items():
    x0,y0,x1,y1 = bbox(sh)
    x0-=W75/2; y0-=W75/2; x1+=W75/2; y1+=W75/2
    tot=[min(tot[0],x0),min(tot[1],y0),max(tot[2],x1),max(tot[3],y1)]
    print(f"  {slot:5s} {dn:9s} x[{x0:+.3f},{x1:+.3f}]  y[{y0:+.3f},{y1:+.3f}]  천장여유 {1.80-y1:+.3f} R")
print(f"  ---- 6종 동시 착용 합집합: x[{tot[0]:+.3f},{tot[2]:+.3f}] y[{tot[1]:+.3f},{tot[3]:+.3f}]"
      f"   천장여유 {1.80-tot[3]:+.3f} R  ({'✓' if tot[3]<=1.80 else '★ 초과'})")
