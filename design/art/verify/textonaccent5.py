#!/usr/bin/env python3
"""R10 최종 — 채택색 확정 및 전량 검산."""
import sys,os,math
sys.path.insert(0,os.path.dirname(__file__))
import numpy as np, colorsys
exec(open(os.path.join(os.path.dirname(__file__),"textonaccent.py")).read().split("# ---------- 4. 전수 탐색")[0])
HERO=(0xDE,0xC0,0x81); LEG=(0xFF,0xD3,0x75); RARE=(0xBC,0xAC,0x8B); COMMON=(0x9C,0x97,0x8C)
RAMP=[("일반",COMMON),("희귀",RARE),("영웅",HERO),("전설",LEG)]
FLOOR=min(dE(ACCENT,c) for _,c in RAMP)
def LCh(c):
    L,a,b=lab(c); return L,math.hypot(a,b),math.degrees(math.atan2(b,a))%360
Lb,Cb,hb=LCh(ACCENT)
def labarc(c):
    _,_,h=LCh(c); return min(abs(h-hb),360-abs(h-hb))
def split(c,ref):
    L1,C1,_=LCh(c); L2,C2,_=LCh(ref); dL,dC=L1-L2,C1-C2
    return dL,dC,math.sqrt(max(dE(c,ref)**2-dL*dL-dC*dC,0.0))
TOK=[("Accent 브라스",ACCENT),("등급 일반",COMMON),("등급 희귀",RARE),("등급 영웅",HERO),("등급 전설",LEG),
     ("TextTertiary 잠김",(0x8B,0x93,0x9F)),("CardBorderWorn 착용",u(.365,.631,.961)),
     ("TextPrimary",u(.839,.859,.890)),("TextSecondary",u(.682,.706,.749)),("OnAccentSolid",u(.043,.063,.086)),
     ("AccentGlowCore",u(.902,.945,1.0)),("틴트 주황",u(.910,.514,.290)),("틴트 청록",u(.310,.753,.776)),
     ("틴트 초록",u(.549,.753,.431)),("틴트 라벤더",u(.690,.561,.816))]
def robust(c):
    n=0
    for dr in(-1,0,1):
      for dg in(-1,0,1):
        for db in(-1,0,1):
            q=tuple(max(0,min(255,c[k]+d)) for k,d in zip(range(3),(dr,dg,db)))
            if (Y(q)>0.30 and all(CR(q,CHIPS[k][1])>=cur_cr[k] for k in range(3))
                and min(dE(q,t) for _,t in RAMP)>=FLOOR and labarc(q)<=4.0): n+=1
    return n
def report(nm,c):
    L,Cq,hq=LCh(c); dL,dC,dH=split(c,ACCENT); rp=min((dE(c,t),n) for n,t in RAMP)
    print(f"\n  ### {nm}  {hx(c)}  rgb({c[0]},{c[1]},{c[2]})")
    print(f"    LCh  L*{L:.1f}  C*{Cq:.1f}  h{hq:.1f}°   | 브라스와: ΔL*{dL:+.1f} ΔC*{dC:+.1f} ΔH*{dH:.2f} (Lab 호 {labarc(c):.2f}°)")
    print(f"    상대휘도 L={Y(c):.4f}  -> 크롬 대역 L>0.30: {'통과' if Y(c)>0.30 else 'FAIL'}")
    print(f"    칩 대비  패널 {CR(c,CHIPS[0][1]):.4f} | 카드 {CR(c,CHIPS[1][1]):.4f} | 보조 {CR(c,CHIPS[2][1]):.4f}")
    print(f"      현행값  패널 {cur_cr[0]:.4f} | 카드 {cur_cr[1]:.4f} | 보조 {cur_cr[2]:.4f}")
    print(f"      증감    패널 {CR(c,CHIPS[0][1])-cur_cr[0]:+.4f} | 카드 {CR(c,CHIPS[1][1])-cur_cr[1]:+.4f} | 보조 {CR(c,CHIPS[2][1])-cur_cr[2]:+.4f}"
          f"  -> {'전부 개선' if all(CR(c,CHIPS[k][1])>=cur_cr[k] for k in range(3)) else 'FAIL 열화'}")
    print(f"    램프 최근접 ΔE {rp[0]:.2f} ({rp[1]}) vs 하한 {FLOOR:.2f} -> {'통과' if rp[0]>=FLOOR else 'FAIL'}")
    print(f"    ±1 LSB 강건성 {robust(c)}/27")
    worst=min((dE(c,t),n) for n,t in TOK[1:])
    print(f"    예약·크롬 토큰 최근접 ΔE {worst[0]:.2f} ({worst[1]})  [변별 하한 7.8]")
    for n,t in TOK:
        f="  <-- 7.8 미달" if dE(c,t)<7.8 and n!="Accent 브라스" else ""
        print(f"      ΔE {n:<22}{dE(c,t):8.2f}{f}")

print("=== 18. 최종 후보 전량 검산 ===")
for nm,c in [("A. 리더 후보 (기각)","#E3B971"),("B. 브라스 최근접 (기각)","#E9B768"),
             ("C. 채택안","#FFE7C0"),("D. 대안 (255 클립 회피)","#FBE4C0"),("E. 대안 (더 진한 쪽)","#FFE3B1")]:
    report(nm,tuple(int(c[i:i+2],16) for i in (1,3,5)))

print("\n\n=== 19. 회색조 서열 (Windows 색역 — 채도가 죽어도 뜻이 남는가) ===")
rows=[("칩(패널)",CHIPS[0][1]),("칩(카드)",CHIPS[1][1]),("칩(보조)",CHIPS[2][1]),
      ("Accent 브라스",ACCENT),("등급 일반",COMMON),("등급 희귀",RARE),("등급 영웅",HERO),("등급 전설",LEG),
      ("현행 #8FC3FF",CUR),("채택안 #FFE7C0",(0xFF,0xE7,0xC0))]
for n,c in sorted(rows,key=lambda x:Y(x[1])):
    print(f"  L={Y(c):.4f}  {hx(c)}  {n}")
print("\n  채택안 vs 등급 4색의 '회색조 전용' 대비:")
for n,c in RAMP:
    print(f"    등급 {n:<4} CR {CR((0xFF,0xE7,0xC0),c):.3f}   (현행 #8FC3FF 는 {CR(CUR,c):.3f})")
