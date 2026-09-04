#!/usr/bin/env python3
"""R10 4차 — '브라스 가족'을 ΔE 총량이 아니라 색상/채도/명도로 분해해 판정한다."""
import sys,os,math
sys.path.insert(0,os.path.dirname(__file__))
import numpy as np, colorsys
exec(open(os.path.join(os.path.dirname(__file__),"textonaccent.py")).read().split("# ---------- 4. 전수 탐색")[0])
HERO=(0xDE,0xC0,0x81); LEG=(0xFF,0xD3,0x75); RARE=(0xBC,0xAC,0x8B); COMMON=(0x9C,0x97,0x8C)
RAMP=[("일반",COMMON),("희귀",RARE),("영웅",HERO),("전설",LEG)]
FLOOR=min(dE(ACCENT,c) for _,c in RAMP)

def LCh(c):
    L,a,b=lab(c); return L, math.hypot(a,b), math.degrees(math.atan2(b,a))%360
def split(c,ref):
    """ΔE 를 ΔL*(명도) / ΔC*(채도) / ΔH*(색상) 로 분해 — 가족 판정은 ΔH* 로 한다."""
    L1,C1,h1=LCh(c); L2,C2,h2=LCh(ref)
    dL,dC=L1-L2,C1-C2
    dH=math.sqrt(max(dE(c,ref)**2-dL*dL-dC*dC,0.0))
    return dL,dC,dH

print("=== 14. 왜 이 문제가 어려운가 — 등급 램프가 '밝은 브라스'를 이미 점유하고 있다 ===")
print(f"  {'색':<22} {'L*':>6} {'C*':>6} {'h°':>7}")
for n,c in [("Accent 브라스",ACCENT)]+[(f"등급 {n}",c) for n,c in RAMP]:
    L,Cc,hh=LCh(c); print(f"  {n:<22} {L:>6.1f} {Cc:>6.1f} {hh:>7.1f}")
print("  -> 영웅·전설은 브라스와 같은 색상각대(h 70~80°)의 '더 밝은 값'이다.")
print("     대비 하한이 글자 L*을 78 근처로 못박으므로, 순진한 '밝은 브라스'는 반드시 그 둘과 겹친다.")
print(f"     (현행 #8FC3FF 의 L* = {LCh(CUR)[0]:.1f}, 영웅 L* = {LCh(HERO)[0]:.1f}, 전설 L* = {LCh(LEG)[0]:.1f})")

g=np.arange(256,dtype=np.float64)/255.0
gl=np.where(g<=0.04045,g/12.92,((g+0.055)/1.055)**2.4)
R,G,B=np.meshgrid(np.arange(256),np.arange(256),np.arange(256),indexing='ij')
R=R.ravel();G=G.ravel();B=B.ravel()
Yv=0.2126*gl[R]+0.7152*gl[G]+0.0722*gl[B]
def crv(Yc,bg):
    yb=Y(bg); return (np.maximum(Yc,yb)+0.05)/(np.minimum(Yc,yb)+0.05)
m=Yv>0.30
for (n,chip,_),base in zip(CHIPS,cur_cr): m&=crv(Yv,chip)>=base
i=np.flatnonzero(m); C=np.stack([R[i],G[i],B[i]],axis=1).astype(np.int16); Yc=Yv[i]
def labv(a):
    lr,lg,lb=gl[a[:,0]],gl[a[:,1]],gl[a[:,2]]
    x=0.4124564*lr+0.3575761*lg+0.1804375*lb; y=0.2126729*lr+0.7151522*lg+0.0721750*lb
    z=0.0193339*lr+0.1191920*lg+0.9503041*lb
    f=lambda t: np.where(t>216/24389,np.cbrt(t),(841/108)*t+4/29)
    fx,fy,fz=f(x/_WP[0]),f(y/_WP[1]),f(z/_WP[2])
    return np.stack([116*fy-16,500*(fx-fy),200*(fy-fz)],axis=1)
LC=labv(C); dv=lambda t:np.sqrt(((LC-np.array(lab(t)))**2).sum(axis=1))
Lc=LC[:,0]; Cc_=np.hypot(LC[:,1],LC[:,2]); hc=np.degrees(np.arctan2(LC[:,2],LC[:,1]))%360
Lb,Cb,hb=LCh(ACCENT)
dHlab=np.minimum(np.abs(hc-hb),360-np.abs(hc-hb))          # Lab 색상각 호 (진짜 가족 판정자)
dramp=np.minimum.reduce([dv(c) for _,c in RAMP])
dother=np.minimum.reduce([dv((0x8B,0x93,0x9F)),dv(u(.365,.631,.961)),dv(u(.839,.859,.890)),dv(u(.682,.706,.749))])
sl_cr=np.minimum.reduce([crv(Yc,CHIPS[k][1])-cur_cr[k] for k in range(3)])

print("\n=== 15. 가족 판정자를 Lab 색상각으로 바꾼다 (HSV 각은 채도를 못 본다) ===")
print(f"  브라스 Lab 색상각 h = {hb:.2f}°, 채도 C* = {Cb:.1f}")
for tol in [2.0,4.0,6.0,8.0]:
    f=(dHlab<=tol)&(dramp>=FLOOR)&(dother>=7.8)&(sl_cr>=0)
    print(f"  Lab 호<={tol:.0f}° ∩ 램프여유 ∩ 열화금지 -> {f.sum():,}색")

FEAS=(dHlab<=4.0)&(dramp>=FLOOR)&(dother>=7.8)&(sl_cr>=0)
print(f"\n=== 16. 실현가능 {FEAS.sum():,}색 안에서 — 채도 사다리로 줄 세운다 ===")
idx=np.flatnonzero(FEAS)
print(f"  {'색':<9} {'L*':>5} {'C*':>5} {'Lab호':>6} {'램프ΔE':>7} {'브라스ΔE':>8} {'ΔL*':>6} {'ΔC*':>6} {'ΔH*':>5} 칩CR(카드)")
# 채도를 브라스(C*=41.6)에서 낮은 쪽으로 훑으며 각 구간의 대표(램프 여유 최대)를 뽑는다
for lo,hi in [(45,60),(35,45),(28,35),(22,28),(16,22),(10,16),(5,10)]:
    s=idx[(Cc_[idx]>=lo)&(Cc_[idx]<hi)]
    if len(s)==0: continue
    k=s[np.argmax(dramp[s])]; c=tuple(int(v) for v in C[k])
    dL,dC,dH=split(c,ACCENT)
    print(f"  {hx(c):<9} {Lc[k]:>5.1f} {Cc_[k]:>5.1f} {dHlab[k]:>6.2f} {dramp[k]:>7.2f} {dv(ACCENT)[k]:>8.2f} "
          f"{dL:>6.1f} {dC:>6.1f} {dH:>5.1f}  {CR(c,CHIPS[1][1]):.3f}")

print("\n=== 17. 판정 후보 정밀 대조 ===")
def robust(c):
    n=0
    for dr in(-1,0,1):
      for dg in(-1,0,1):
        for db in(-1,0,1):
            q=(c[0]+dr,c[1]+dg,c[2]+db)
            L,Cq,hq=LCh(q)
            if (Y(q)>0.30 and all(CR(q,CHIPS[k][1])>=cur_cr[k] for k in range(3))
                and min(dE(q,t) for _,t in RAMP)>=FLOOR
                and min(abs(hq-hb),360-abs(hq-hb))<=4.0): n+=1
    return n
for nm,c in [("현행 #8FC3FF",CUR),("리더후보 #E3B971",CAND),("#F0E5D1",(0xF0,0xE5,0xD1)),
             ("#EFE3CC",(0xEF,0xE3,0xCC)),("#F3E8D4",(0xF3,0xE8,0xD4)),("#EDE0C7",(0xED,0xE0,0xC7))]:
    L,Cq,hq=LCh(c); dL,dC,dH=split(c,ACCENT)
    rp=min((dE(c,t),n) for n,t in RAMP)
    print(f"\n  {nm} {hx(c)}")
    print(f"    L*{L:.1f} C*{Cq:.1f} h{hq:.1f}° | Lab호(브라스) {min(abs(hq-hb),360-abs(hq-hb)):.2f}° | ΔL*{dL:+.1f} ΔC*{dC:+.1f} ΔH*{dH:.2f}")
    print(f"    L(휘도) {Y(c):.4f} 크롬대역 {'통과' if Y(c)>0.30 else 'FAIL'}")
    print(f"    칩CR {CR(c,CHIPS[0][1]):.4f}/{CR(c,CHIPS[1][1]):.4f}/{CR(c,CHIPS[2][1]):.4f} (현행 {cur_cr[0]:.4f}/{cur_cr[1]:.4f}/{cur_cr[2]:.4f})")
    print(f"    램프 최근접 {rp[0]:.2f}({rp[1]}) vs 하한 {FLOOR:.2f} {'통과' if rp[0]>=FLOOR else 'FAIL'} | ±1LSB {robust(c)}/27")
