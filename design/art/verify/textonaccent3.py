#!/usr/bin/env python3
"""R10 3차 — 경계에 붙지 않는 색을 고른다. 목적함수를 '브라스 최근접'에서 '여유 최대'로 바꾼다."""
import sys,os,math
sys.path.insert(0,os.path.dirname(__file__))
import numpy as np, colorsys
exec(open(os.path.join(os.path.dirname(__file__),"textonaccent.py")).read().split("# ---------- 4. 전수 탐색")[0])
HERO=(0xDE,0xC0,0x81); LEG=(0xFF,0xD3,0x75); RARE=(0xBC,0xAC,0x8B); COMMON=(0x9C,0x97,0x8C)
TERT=(0x8B,0x93,0x9F); WORN=u(.365,.631,.961)
RAMP=[("일반",COMMON),("희귀",RARE),("영웅",HERO),("전설",LEG)]

print("=== 11. 하한을 다시 세운다 — 브라스 자신의 램프 최근접 거리 ===")
for n,c in RAMP: print(f"  ΔE(브라스, 등급 {n}) = {dE(ACCENT,c):.3f}")
FLOOR=min(dE(ACCENT,c) for _,c in RAMP)
print(f"  -> 브라스의 램프 최근접 = {FLOOR:.3f} (영웅). 후보는 '어느 등급색에도' 이보다 가까우면 안 된다.")

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
mx=C.max(axis=1).astype(float);mn=C.min(axis=1).astype(float);dl=np.maximum(mx-mn,1e-9)
r,gg,b=C[:,0],C[:,1],C[:,2]
h=(np.where(mx==r,((gg-b)/dl)%6,np.where(mx==gg,(b-r)/dl+2,(r-gg)/dl+4))*60.0)%360
ha=np.minimum(np.abs(h-hsvh(ACCENT)),360-np.abs(h-hsvh(ACCENT)))
def labv(a):
    lr,lg,lb=gl[a[:,0]],gl[a[:,1]],gl[a[:,2]]
    x=0.4124564*lr+0.3575761*lg+0.1804375*lb; y=0.2126729*lr+0.7151522*lg+0.0721750*lb
    z=0.0193339*lr+0.1191920*lg+0.9503041*lb
    f=lambda t: np.where(t>216/24389,np.cbrt(t),(841/108)*t+4/29)
    fx,fy,fz=f(x/_WP[0]),f(y/_WP[1]),f(z/_WP[2])
    return np.stack([116*fy-16,500*(fx-fy),200*(fy-fz)],axis=1)
LC=labv(C); dv=lambda t:np.sqrt(((LC-np.array(lab(t)))**2).sum(axis=1))
dramp=np.minimum.reduce([dv(c) for _,c in RAMP]); dbr=dv(ACCENT)
dother=np.minimum.reduce([dv(TERT),dv(WORN),dv(u(.839,.859,.890)),dv(u(.682,.706,.749))])

# 여유(슬랙) 정의 — 각 제약을 '하한 대비 얼마나 남았나'로 정규화
sl_cr  = np.minimum.reduce([crv(Yc,CHIPS[k][1])-cur_cr[k] for k in range(3)])   # 열화 금지선 대비
sl_ramp= dramp-FLOOR                                                            # 등급 램프 여유
sl_hue = 3.0-ha                                                                 # 브라스 가족 여유
feas=(sl_cr>=0)&(sl_ramp>=0)&(sl_hue>=0)&(dother>=7.8)
print(f"  실현가능 {feas.sum():,}색")

print("\n=== 12. 여유 최대화 — 경계에서 가장 먼 색 ===")
# 정규화: CR 0.10 / 램프 ΔE 3.0 / 호 1.0° 를 '한 단위 여유'로 본다
score=np.where(feas, np.minimum.reduce([sl_cr/0.10, sl_ramp/3.0, sl_hue/1.0]), -1e9)
best=np.argsort(-score)[:10]
print(f"  {'색':<9} {'점수':>6} {'CR여유':>7} {'램프ΔE':>7} {'호°':>6} {'브라스ΔE':>8}  칩CR(패널/카드/보조)")
for k in best:
    c=tuple(int(v) for v in C[k])
    print(f"  {hx(c):<9} {score[k]:>6.2f} {sl_cr[k]:>7.3f} {dramp[k]:>7.2f} {ha[k]:>6.2f} {dbr[k]:>8.2f}  "
          f"{CR(c,CHIPS[0][1]):.3f}/{CR(c,CHIPS[1][1]):.3f}/{CR(c,CHIPS[2][1]):.3f}")

def robust(c):
    n=0
    for dr in(-1,0,1):
      for dg in(-1,0,1):
        for db in(-1,0,1):
            q=(c[0]+dr,c[1]+dg,c[2]+db)
            if (Y(q)>0.30 and all(CR(q,CHIPS[k][1])>=cur_cr[k] for k in range(3))
                and min(dE(q,t) for _,t in RAMP)>=FLOOR and arc(hsvh(q),hsvh(ACCENT))<=3.0): n+=1
    return n

print("\n=== 13. 최종 3색 비교 (강건성 포함) ===")
SHORT=[("#E3B971 리더후보",CAND),("#E9B768 브라스최근접",(0xE9,0xB7,0x68))]
SHORT+= [(f"{hx(tuple(int(v) for v in C[k]))} 여유최대",tuple(int(v) for v in C[k])) for k in best[:2]]
for n,c in SHORT:
    rp=min((dE(c,t),nm) for nm,t in RAMP)
    print(f"\n  {n} {hx(c)}")
    print(f"    L {Y(c):.4f} 크롬대역 {'통과' if Y(c)>0.30 else 'FAIL'} | 호 {arc(hsvh(c),hsvh(ACCENT)):.2f}° | 브라스ΔE {dE(c,ACCENT):.2f}")
    print(f"    칩CR {CR(c,CHIPS[0][1]):.4f}/{CR(c,CHIPS[1][1]):.4f}/{CR(c,CHIPS[2][1]):.4f}  (현행 {cur_cr[0]:.4f}/{cur_cr[1]:.4f}/{cur_cr[2]:.4f})")
    print(f"    램프 최근접 ΔE {rp[0]:.2f} ({rp[1]}) vs 하한 {FLOOR:.2f} -> {'통과' if rp[0]>=FLOOR else 'FAIL'}")
    print(f"    ±1 LSB 강건성 {robust(c)}/27")
