# -*- coding: utf-8 -*-
"""BACK(sort -1)은 몸 뒤에 그려진다. 몸통(sort1)/팔(sort0,2)이 등판을 얼마나 지우는가.
좌표: 머리 중심 원점, R 단위, +x = 진행 방향.
"""
import math
R0=0.22; H=2.2746944; HEAD_C=H-R0
SH=(1.7646944-HEAD_C)/R0; HIPR=(0.9346944-HEAD_C)/R0; TL=SH-HIPR
W_TORSO=0.11*1.045/R0; W_ARM=0.10*1.045/R0
ARM_UP=0.38/R0; ARM_LO=0.37/R0; SPREAD=40.0; ELBOW=10.0
TORSO_TOP=-1.0                     # 몸통 선은 턱까지 올라온다
# ---- 교정: 알려진 값 --------------------------------------------------
assert abs(SH+1.31818)<1e-4 and abs(HIPR+5.09091)<1e-4 and abs(TL-3.77273)<1e-4
assert abs(W_TORSO-0.52250)<1e-4 and abs(W_ARM-0.47500)<1e-4
print("교정 통과\n")

def seg_dist(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]; L=dx*dx+dy*dy
    t=0.0 if L<1e-12 else max(0.0,min(1.0,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/L))
    return math.hypot(p[0]-(a[0]+dx*t), p[1]-(a[1]+dy*t))

def arm_pts(sign):
    sx,sy=0.0,SH
    a=math.radians(SPREAD*sign); b=math.radians(SPREAD*sign+ELBOW*sign)
    ex,ey=sx+ARM_UP*math.sin(a), sy-ARM_UP*math.cos(a)
    hx,hy=ex+ARM_LO*math.sin(b), ey-ARM_LO*math.cos(b)
    return [(sx,sy),(ex,ey),(hx,hy)]
ARMS=[arm_pts(+1),arm_pts(-1)]

def body_covers(p):
    if abs(p[0])<=W_TORSO/2 and HIPR<=p[1]<=TORSO_TOP: return True     # 몸통(캡 제외)
    if math.hypot(p[0],p[1]-TORSO_TOP)<=W_TORSO/2: return True         # 위 캡
    if math.hypot(p[0],p[1]-HIPR)<=W_TORSO/2: return True              # 아래 캡
    for a in ARMS:
        for i in range(len(a)-1):
            if seg_dist(p,a[i],a[i+1])<=W_ARM/2: return True
    return False

def inside(poly,q):
    ins=False; n=len(poly)
    for i in range(n):
        a=poly[i]; b=poly[(i+1)%n]
        if (a[1]>q[1])!=(b[1]>q[1]):
            x=a[0]+(q[1]-a[1])*(b[0]-a[0])/(b[1]-a[1])
            if q[0]<x: ins=not ins
    return ins

def measure(name, poly, step=0.01):
    xs=[p[0] for p in poly]; ys=[p[1] for p in poly]
    tot=0; hid=0; hid_t=0; hid_a=0
    x=min(xs)
    while x<=max(xs):
        y=min(ys)
        while y<=max(ys):
            if inside(poly,(x,y)):
                tot+=1
                t = (abs(x)<=W_TORSO/2 and HIPR<=y<=TORSO_TOP) or math.hypot(x,y-TORSO_TOP)<=W_TORSO/2 or math.hypot(x,y-HIPR)<=W_TORSO/2
                a = any(seg_dist((x,y),ar[i],ar[i+1])<=W_ARM/2 for ar in ARMS for i in range(len(ar)-1))
                if t or a: hid+=1
                if t: hid_t+=1
                if a and not t: hid_a+=1
            y+=step
        x+=step
    A=tot*step*step
    print(f"{name:14s} 면적 {A:6.3f} R^2   몸통이 지움 {hid_t/tot*100:5.1f}%  팔이 추가로 {hid_a/tot*100:5.1f}%"
          f"   -> 가시 {100-hid/tot*100:5.1f}%")
    return A, 100-hid/tot*100

# ---- 출하 BACK 도형(AccessoryShapeBuilder 상수에서 유도) ----------------
COLLAR_Y = SH+0.10
def cape(spread_back, spread_front, length_ratio, collar_f=0.40, collar_b=-0.62):
    hem = COLLAR_Y - TL*length_ratio
    return [(collar_f,COLLAR_Y),(spread_front,hem),(-spread_back,hem),(collar_b,COLLAR_Y)]
print("== 출하 BACK 등판 가시율 (중립 자세) ==")
measure("짧은망토",  cape(2.45,0.85,1.35))
measure("긴망토",    cape(2.45,0.85,1.75))
measure("판초",      cape(1.95,1.55,1.05))
# 옷깃 띠(클래스프) — CapeCollarBand front 0.40 ~ back -0.66, top +0.10 ~ bottom -0.34 (옷깃선 기준)
band=[(0.40,COLLAR_Y+0.10),(0.40,COLLAR_Y-0.34),(-0.66,COLLAR_Y-0.34),(-0.66,COLLAR_Y+0.10)]
measure("옷깃 띠",   band, step=0.004)
print(f"\n몸통 폭 {W_TORSO:.4f} R / 띠 폭 {0.40+0.66:.4f} R = {W_TORSO/1.06*100:.1f}% (배정문 '40%'는 장비펜 0.4298로 잰 값)")
