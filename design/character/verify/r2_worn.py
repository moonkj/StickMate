# -*- coding: utf-8 -*-
"""R2 ⑤ **착용 상태** — 동시 착용 머리 예산 / 잉크 위계에 따른 가림 / 슬롯 내 실루엣 차."""
import math, sys
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/character/verify")
import rig, items, headroom, hair, r2_body as B, r2_pack as P
from rig import Shape
R_INK=1.171932; SH=rig.SHOULDER_R; HIP=rig.HIP_R
W75=P.W75; W60=P.W60

# ---------------- 본체 잉크 (잉크 위계: 다리0 < 몸통1 < 팔2 < 머리3/4 ; BACK = -1) -------
def cap2(a,b): return Shape("seg",[a,b],loop=False)
U,L = 0.38/0.22, 0.37/0.22
KU,KL = 0.50/0.22, 0.45/0.22
def arm(sign):
    e=(sign*U*math.sin(math.radians(40)), SH-U*math.cos(math.radians(40)))
    h=(sign*(U*math.sin(math.radians(40))+L*math.sin(math.radians(50))),
       SH-(U*math.cos(math.radians(40))+L*math.cos(math.radians(50))))
    return [cap2((0,SH),e), cap2(e,h)]
def leg(sign):
    k=(sign*KU*math.sin(math.radians(12)), HIP-KU*math.cos(math.radians(12)))
    f=(sign*(KU*math.sin(math.radians(12))+KL*math.sin(math.radians(8))),
       HIP-(KU*math.cos(math.radians(12))+KL*math.cos(math.radians(8))))
    return [cap2((0,HIP),k), cap2(k,f)]
HEAD_DISC=[Shape("HeadFill", rig.poly(0,0,R_INK,72), filled=True)]
TORSO=[cap2((0,SH),(0,HIP))]
ARMS=arm(1)+arm(-1); LEGS=leg(1)+leg(-1)
W_TORSO=B.W_TORSO0/B.R0; W_ARM=B.W_ARM0/B.R0; W_LEG=B.W_LEG0/B.R0
BODY=[(HEAD_DISC,0.0),(TORSO,W_TORSO),(ARMS,W_ARM),(LEGS,W_LEG)]

def spans_union(groups, y):
    out=[]
    for shapes,w in groups: out.extend(headroom.ink_spans(shapes,y,w))
    return headroom._merge(out)

def visible_ratio(item, w_item, blockers, N=1601):
    """item 잉크 중 blockers(위 레이어)에 안 가려진 면적 비율 + 남는 조각 최소 폭(획)."""
    ys=[p[1] for s in item for p in s.pts]
    y0,y1=min(ys)-w_item, max(ys)+w_item
    tot=vis=0.0; thin=9e9
    for k in range(N):
        y=y0+(y1-y0)*k/(N-1)
        cur=headroom.ink_spans(item,y,w_item)
        if not cur: continue
        cov=spans_union(blockers,y)
        for a,b in cur:
            tot+=b-a; seg=[(a,b)]
            for ca,cb in cov:
                nxt=[]
                for x0,x1 in seg:
                    if cb<=x0 or ca>=x1: nxt.append((x0,x1)); continue
                    if ca>x0: nxt.append((x0,ca))
                    if cb<x1: nxt.append((cb,x1))
                seg=nxt
            for x0,x1 in seg:
                vis+=x1-x0
    return (vis/tot if tot else 0.0), 0.0

print("== ⑤-1 팩 6종 동시 착용 — 머리 예산 (하한 면적 12% / 두께 1.00획, 목표 1.20) ==")
hat=P.PACK["HEAD"][1]; eyes=P.PACK["EYES"][1]
for sc,w in ((0.75,W75),(0.60,W60)):
    solo=headroom.measure(hat,w); comp=headroom.measure(hat+eyes,w)
    print(f"  s={sc:.2f}  모자 단독 {solo['area']*100:5.1f}% {solo['depth']*2/w:4.2f}획"
          f"   |  모자+안경 {comp['area']*100:5.1f}% {comp['depth']*2/w:4.2f}획"
          f"   외곽호 {comp['arc']:.0f}°")
print("  (팩은 HAIR를 안 판다 → 유저의 기본 머리 6종과 겹칠 때가 최악이다)")
HAIRS={"삐친머리":hair.cowlick(),"단정한머리":hair.straight(),"곱슬머리":hair.curly(),
       "민머리":hair.bald(),"바가지머리":hair.bowl(),"포니테일":hair.ponytail()}
worst=(999,999,None)
for an,ash in HAIRS.items():
    cl=[Shape(s.name, headroom.clip_below(s.pts, P.HAT_COVER), loop=s.loop, filled=s.filled)
        for s in ash if len(headroom.clip_below(s.pts,P.HAT_COVER))>=3]
    m=headroom.measure(hat+eyes+cl, W75)
    a,t=m['area']*100, m['depth']*2/W75
    if a<worst[0]: worst=(a,t,an)
    print(f"    + {an:10s} {a:5.1f}% {t:4.2f}획")
print(f"  ★ 팩 최악(0.75) = +{worst[2]} → {worst[0]:.1f}% / {worst[1]:.2f}획")
print(f"  ★ 비교: 출하 조합 최악(야구모자+고글+단정한머리) = 5.9% / 0.61획")

print("\n== ⑤-2 잉크 위계에 따른 가림 ==")
r,thin = visible_ratio(P.PACK["BACK"][1], W75, BODY)
print(f"  BACK 우산(sort -1)  가시 {r*100:5.1f}%   (출하 6종 대역과 비교 ↓)")
r2,t2 = visible_ratio(P.PACK["EYES"][1], W75, [(hat,W75)])
print(f"  EYES 안경(8) <- 모자(10)  가시 {r2*100:5.1f}%")
r3,t3 = visible_ratio(P.PACK["NECK"][1], W75, [(HEAD_DISC,0.0)])
print(f"  NECK 깃(7)  ← 머리(3/4)  가시 {r3*100:5.1f}%   (머리와 안 겹치면 100%)")
print("  [대조] 출하 BACK 6종을 같은 자로:")
for nm,sh in items.BACK.items():
    rr,tt = visible_ratio(sh, W75, BODY)
    print(f"    {nm:8s} 가시 {rr*100:5.1f}%")
print("  [대조] 출하 EYES 6종 x 출하 모자 6종 — 안경이 모자에 얼마나 살아남는가(%)")
print("        " + "".join(f"{h:>9s}" for h in items.HEAD))
for en,esh in items.EYES.items():
    row=f"    {en:8s}"
    for hn,hsh in items.HEAD.items():
        rr,_=visible_ratio(esh,W75,[(hsh,W75)])
        row+=f"{rr*100:8.0f}%"
    print(row)
print("  [모자 잉크 밑단 x=0 (R)] " + "  ".join(
    f"{hn}:{headroom.measure(hsh,W75)['ink_bottom']:+.3f}" for hn,hsh in items.HEAD.items()))
print(f"  [팩 신문 모자 잉크 밑단] {headroom.measure(hat,W75)['ink_bottom']:+.3f} R")

print("\n== ⑤-3 슬롯 내 실루엣 차 (72구간 프로파일, 문턱 > 1.0획 = %.4f R) =="%W75)
def prof(sh, anchor): return rig.profile(sh, anchor)
for slot,(dn,sh),tab,anchor in (("HEAD",P.PACK["HEAD"],items.HEAD,0.0),
                                ("EYES",P.PACK["EYES"],items.EYES,0.0),
                                ("NECK",P.PACK["NECK"],items.NECK,SH),
                                ("BACK",P.PACK["BACK"],items.BACK,SH)):
    p0=prof(sh,anchor); worst=(9e9,None)
    for nm,other in tab.items():
        d=rig.max_delta(p0, prof(other,anchor))
        if d<worst[0]: worst=(d,nm)
    print(f"  {slot:5s} {dn:9s} 기본 6종과의 최소 차 = {worst[0]/W75:5.2f}획  (상대 {worst[1]})"
          f"  {'OK' if worst[0]>W75 else '★ 미달'}")
    # 기본 6종 서로의 최소 차(기준선)
    ns=list(tab); base=9e9; bp=None
    for i in range(6):
        for j in range(i+1,6):
            d=rig.max_delta(prof(tab[ns[i]],anchor), prof(tab[ns[j]],anchor))
            if d<base: base,bp=d,(ns[i],ns[j])
    print(f"        [기준선] 기본 6종 서로의 최소 차 = {base/W75:.2f}획 ({bp[0]}~{bp[1]})")
