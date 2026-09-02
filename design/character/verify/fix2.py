# -*- coding: utf-8 -*-
import math
R0=0.22; H=2.2746944; HEAD_C=H-R0
SH=(1.7646944-HEAD_C)/R0; HIPR=(0.9346944-HEAD_C)/R0; TL=SH-HIPR
W_TORSO=0.11*1.045/R0; W_ARM=0.10*1.045/R0
RING=0.0756501/R0; HEAD_INK_R=1.0+RING/2; W_DES=0.048/R0
COLLAR=SH+0.10; ARM_UP=0.38/R0; ARM_LO=0.37/R0; SPREAD=40.0; ELBOW=10.0
assert abs(W_TORSO-0.52250)<1e-4 and abs(HEAD_INK_R-1.171932)<1e-5 and abs(W_DES-0.218182)<1e-5
def seg_dist(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]; L=dx*dx+dy*dy
    t=0.0 if L<1e-12 else max(0.0,min(1.0,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/L))
    return math.hypot(p[0]-(a[0]+dx*t), p[1]-(a[1]+dy*t))
def arm(sg):
    a=math.radians(SPREAD*sg); b=math.radians((SPREAD+ELBOW)*sg)
    ex,ey=ARM_UP*math.sin(a), SH-ARM_UP*math.cos(a)
    return [(0,SH),(ex,ey),(ex+ARM_LO*math.sin(b), ey-ARM_LO*math.cos(b))]
ARMS=[arm(1),arm(-1)]
def hid(x,y):
    if (abs(x)<=W_TORSO/2 and HIPR<=y<=-1.0) or math.hypot(x,y+1.0)<=W_TORSO/2 or math.hypot(x,y-HIPR)<=W_TORSO/2: return 't'
    for a in ARMS:
        for i in range(len(a)-1):
            if seg_dist((x,y),a[i],a[i+1])<=W_ARM/2: return 'a'
    return None
def inside(poly,q):
    ins=False; n=len(poly)
    for i in range(n):
        a=poly[i]; b=poly[(i+1)%n]
        if (a[1]>q[1])!=(b[1]>q[1]):
            xx=a[0]+(q[1]-a[1])*(b[0]-a[0])/(b[1]-a[1])
            if q[0]<xx: ins=not ins
    return ins
def vis(name, poly, step=0.006):
    xs=[p[0] for p in poly]; ys=[p[1] for p in poly]
    tot=ht=ha=0; x=min(xs)
    while x<=max(xs):
        y=min(ys)
        while y<=max(ys):
            if inside(poly,(x,y)):
                tot+=1; h=hid(x,y)
                if h=='t': ht+=1
                elif h=='a': ha+=1
            y+=step
        x+=step
    v=100-(ht+ha)/tot*100
    print(f"{name:26s} 가시 {v:5.1f}%  (몸통 {ht/tot*100:4.1f}% 팔 {ha/tot*100:4.1f}%)  {'통과' if v>=65 else '★미달'}")
    return v
print("== 교정(알려진 값) ==")
vis("짧은망토(신고없음)", [(0.40,COLLAR),(0.85,COLLAR-TL*1.35),(-2.45,COLLAR-TL*1.35),(-0.62,COLLAR)],0.008)
vis("출하 옷깃띠(신고됨)", [(0.40,COLLAR+0.10),(0.40,COLLAR-0.34),(-0.66,COLLAR-0.34),(-0.66,COLLAR+0.10)],0.004)
print("\n== 확정 배치 ==")
vis("오피스 노트북가방", [(-0.35,-2.60),(-1.55,-2.60),(-1.55,-3.90),(-0.35,-3.90)])
vis("사이버 급수통", [(-0.55,-2.30),(-1.65,-2.30),(-1.65,-4.10),(-0.55,-4.10)])
vis("스포츠 번호망토", [(0.40,COLLAR),(0.85,COLLAR-TL*1.35),(-2.45,COLLAR-TL*1.35),(-0.62,COLLAR)],0.008)
vis("스포츠 엠블럼 2.0R", [(1.00,-2.60),(1.00,-3.60),(-1.00,-3.60),(-1.00,-2.60)],0.005)
print("\n== 후드 정수리 재설정 ==")
for top in (1.62,1.68,1.691):
    print(f"정수리 {top:+.3f} R -> 단차 {(top-HEAD_INK_R)/W_DES:+.2f}획 {'통과' if (top-HEAD_INK_R)/W_DES>=2.12 else '★미달'} (액자 1.80R 여유 {1.80-top:.3f} R)")
print("\n== 실루엣 봉투 C-3 (|x| <= 2.45 R, 손끝 잉크 2.6361) ==")
for nm,mx in {"오피스 노트북가방":1.55,"사이버 급수통":1.65,"사이버 후드 뒤자락":1.85,
              "스포츠 번호망토":2.45,"오피스 귀컵":1.24+0.40,"스포츠 매듭":1.25+0.42}.items():
    print(f"{nm:22s} 최대 |x| {mx:.2f} R  {'통과' if mx<=2.45 else '★초과'}")
