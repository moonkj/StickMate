#!/usr/bin/env python3
"""R10 2차 — #E3B971 기각 확인 · 절충 전선 · 최종색 강건성."""
import sys, os, math
sys.path.insert(0,os.path.dirname(__file__))
import numpy as np, colorsys
exec(open(os.path.join(os.path.dirname(__file__),"textonaccent.py")).read().split("# ---------- 4. 전수 탐색")[0])

HERO=(0xDE,0xC0,0x81); LEG=(0xFF,0xD3,0x75); RARE=(0xBC,0xAC,0x8B); COMMON=(0x9C,0x97,0x8C)
print("\n=== 6. #E3B971 기각의 검산 — 세 방향으로 다시 잰다 ===")
print(f"  (가) ΔE(#E3B971, 영웅 #DEC081) = {dE(CAND,HERO):.4f}  < 변별 하한 7.8   -> 미달")
print(f"  (나) 브라스 자신의 거리        = {dE(ACCENT,HERO):.4f}  -> 후보가 브라스보다 {dE(ACCENT,HERO)-dE(CAND,HERO):.2f} 더 가깝다")
print(f"  (다) 채널차: ", tuple(abs(a-b) for a,b in zip(CAND,HERO)), " (R,G,B 각 8비트)")
lc,lh=lab(CAND),lab(HERO)
print(f"      Lab 성분차 ΔL* {lc[0]-lh[0]:+.2f} / Δa* {lc[1]-lh[1]:+.2f} / Δb* {lc[2]-lh[2]:+.2f}")
print(f"  (라) 회색조(휘도)만 남기면: 후보 L {Y(CAND):.4f} vs 영웅 L {Y(HERO):.4f} -> CR {CR(CAND,HERO):.3f} (서로 1.08 이하면 같은 밝기)")

print("\n=== 7. 절충 전선 — 영웅 여유를 올리면 브라스에서 얼마나 멀어지는가 ===")
g=np.arange(256,dtype=np.float64)/255.0
gl=np.where(g<=0.04045,g/12.92,((g+0.055)/1.055)**2.4)
R,G,B=np.meshgrid(np.arange(256),np.arange(256),np.arange(256),indexing='ij')
R=R.ravel();G=G.ravel();B=B.ravel()
Yv=0.2126*gl[R]+0.7152*gl[G]+0.0722*gl[B]
def crv(Yc,bg):
    yb=Y(bg); return (np.maximum(Yc,yb)+0.05)/(np.minimum(Yc,yb)+0.05)
m=Yv>0.30
for (n,chip,_),base in zip(CHIPS,cur_cr): m&=crv(Yv,chip)>=base
i=np.flatnonzero(m); C=np.stack([R[i],G[i],B[i]],axis=1).astype(np.int16)
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
LC=labv(C)
dv=lambda t:np.sqrt(((LC-np.array(lab(t)))**2).sum(axis=1))
dh,dbr=dv(HERO),dv(ACCENT)
ok=(ha<=3.0)&(dv(LEG)>=7.8)&(dv(RARE)>=7.8)&(dv(COMMON)>=7.8)&(dv((0x8B,0x93,0x9F))>=7.8)
print(f"  {'영웅 여유 하한':>14} | {'후보수':>8} | {'최근접 브라스 ΔE':>16} | 색")
for floor in [7.8,10.0,12.864,14.0,15.0,16.0,18.0,20.0]:
    s=ok&(dh>=floor)
    if s.sum()==0: print(f"  {floor:>14.3f} | {0:>8} | {'-':>16} |"); continue
    j=np.flatnonzero(s); k=j[np.argmin(dbr[j])]; c=tuple(int(v) for v in C[k])
    print(f"  {floor:>14.3f} | {s.sum():>8,} | {dbr[k]:>16.3f} | {hx(c)} (영웅 ΔE {dh[k]:.2f}, 호 {ha[k]:.2f}°)")

print("\n=== 8. 최종색 후보 정밀 검증 ===")
FINAL=(0xE9,0xB7,0x68)
OTHER=[("#E9B768(채택안)",FINAL),("#E3B971(리더 후보)",CAND),("#8FC3FF(현행)",CUR)]
TOK=[("Accent 브라스",ACCENT),("등급 일반",COMMON),("등급 희귀",RARE),("등급 영웅",HERO),("등급 전설",LEG),
     ("TextTertiary 잠김",(0x8B,0x93,0x9F)),("CardBorderWorn 착용",u(.365,.631,.961)),
     ("TextPrimary",u(.839,.859,.890)),("TextSecondary",u(.682,.706,.749)),
     ("틴트 주황 HEAD",u(.910,.514,.290)),("틴트 청록",u(.310,.753,.776)),
     ("틴트 초록",u(.549,.753,.431)),("틴트 라벤더",u(.690,.561,.816))]
for n,c in OTHER:
    print(f"\n  --- {n} {hx(c)} ---")
    print(f"    L {Y(c):.4f} | 크롬대역 L>0.30 {'통과' if Y(c)>0.30 else 'FAIL'} | HSV 호(브라스) {arc(hsvh(c),hsvh(ACCENT)):.2f}°")
    print(f"    칩 CR  패널 {CR(c,CHIPS[0][1]):.4f} / 카드 {CR(c,CHIPS[1][1]):.4f} / 보조 {CR(c,CHIPS[2][1]):.4f}")
    print(f"    기반면 직접 CR  패널 {CR(c,PANEL):.3f} / 카드 {CR(c,CARD):.3f} / 보조 {CR(c,SUBTLE):.3f}")
    worst=min(dE(c,t) for _,t in TOK[1:])
    for tn,t in TOK:
        flag="  <-- 하한 7.8 미달" if dE(c,t)<7.8 and tn!="Accent 브라스" else ""
        print(f"    ΔE {tn:<22} {dE(c,t):7.3f}{flag}")

print("\n=== 9. 강건성 — ±1 LSB 반올림에서도 판정이 유지되는가 (#E9B768) ===")
bad=0
for dr in(-1,0,1):
  for dg in(-1,0,1):
    for db in(-1,0,1):
        c=(FINAL[0]+dr,FINAL[1]+dg,FINAL[2]+db)
        cond=(Y(c)>0.30 and CR(c,CHIPS[0][1])>=cur_cr[0] and CR(c,CHIPS[1][1])>=cur_cr[1]
              and CR(c,CHIPS[2][1])>=cur_cr[2] and dE(c,HERO)>=12.864)
        if not cond: bad+=1
print(f"  27개 이웃 중 판정 유지 {27-bad}/27  (실패 {bad})")
print("\n=== 10. Windows 색역 — 회색조 서열 (채도 제거해도 순서가 남는가) ===")
for n,c in [("칩(카드)",CHIPS[1][1]),("Accent 브라스",ACCENT),("등급 영웅",HERO),("등급 전설",LEG),
            ("#E9B768 채택안",FINAL),("#E3B971 리더후보",CAND)]:
    print(f"  {n:<18} {hx(c)}  L={Y(c):.4f}")
