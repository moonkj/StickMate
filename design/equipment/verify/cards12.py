# -*- coding: utf-8 -*-
"""FX/PET 12종의 **현행 카드 아이콘**(Resources/Items/*.asset, 40x40 캔버스) 검산.
카드 획 = CharacterInfoWindow.IconStroke = 1.7 캔버스 유닛."""
import sys, math; sys.path.insert(0,'.')
from rig import Shape, rule_one, bounds
WC = 1.7
def circ(cx,cy,r,n=None):
    n = n or max(4, int(math.floor(math.pi/math.asin(min(1.0, WC/(2*r))))) )
    return [(cx+math.cos(2*math.pi*i/n)*r, cy+math.sin(2*math.pi*i/n)*r) for i in range(n)]
def P(v): return [(v[i],v[i+1]) for i in range(0,len(v),2)]

CARDS = {
 "FX 없음":   [("dot",[(20,20)],0),("dotted-ring",circ(20,20,9),0)],
 "FX 발자국": [("dot1",[(10,27)],0),("dot2",[(17,23)],0),("dot3",[(24,19)],0),("dot4",[(31,15)],0)],
 "FX 반짝임": [("armN",P([20,8,20,18]),0),("armS",P([20,22,20,32]),0),("armW",P([8,20,18,20]),0),
               ("armE",P([22,20,32,20]),0),("tick1",P([12,12,16,16]),1),("tick2",P([28,12,24,16]),1),
               ("tick3",P([12,28,16,24]),1),("tick4",P([28,28,24,24]),1)],
 "FX 먼지":   [("cloud",P([10,26,7.96,22.78,8.79,19.06,12,17,14.32,13.58,18.16,12.06,22.19,12.98,25,16,29.12,16.05,32,19,31.95,23.12,29,26,10,26]),0),
               ("gnd1",P([8,30,17,30]),1),("gnd2",P([21,30,31,30]),1)],
 "FX 물방울": [("b1",circ(14,22,4),0),("b2",circ(24,17,3),0),("b3",circ(29,26,2.2),1)],
 "FX 나뭇잎": [("blade",P([12,26,16,16,26,12,24,23,12,26]),0),("stem",P([12,26,8,31]),1)],
 "PET 작은공": [("ball",circ(20,18,8),0),
               ("hilite",P([14,13,15.75,14.62,17,16.5,17.75,18.62,18,21]),1),
               ("shadow",P([11,31,15.5,32.12,20,32.5,24.5,32.12,29,31]),1)],
 "PET 종이비행기":[("body",P([6,20,34,8,24,32,19,23,6,20]),0),("fold",P([6,20,19,23,34,8]),1)],
 "PET 리틀스틱메이트":[("head",circ(20,13,5),0),("torso",P([20,18,20,27]),0),("armB",P([20,21,14,25]),0),
               ("armF",P([20,21,26,25]),0),("legB",P([20,27,15,34]),0),("legF",P([20,27,25,34]),0)],
 "PET 커서친구": [("arrow",P([13,7,13,30,19,24,23,33,27,31,23,22,31,22,13,7]),0)],
 "PET 풍선":  [("body",circ(20,15,7),0),("string",P([20,22,21,29,19,34]),1)],
 "PET 달팽이": [("shell",circ(18,20,7),0),("foot",P([11,27,30,27,33,22]),0),("core",circ(18,20,3),1)],
}
def true_min(pts, loop):
    n=len(pts); best=None
    for i in range(n if loop else n-1):
        L=math.dist(pts[i],pts[(i+1)%n])
        if L<1e-9: continue
        best=L if best is None else min(best,L)
    return best
print("╔══ FX/PET 12종 **현행 카드 아이콘** 검산 (40x40 캔버스, 카드 획 = 1.7 유닛) ══╗")
bad=0
for name, parts in CARDS.items():
    msgs=[]
    if not (2<=len(parts)<=4): msgs.append("정원 %d개(2~4 밖)"%len(parts))
    acc=sum(1 for _,_,t in parts if t==1)
    if acc!=1: msgs.append("보조색 %d개"%acc)
    mins=[]
    for nm,pts,t in parts:
        if len(pts)<2: mins.append("%s 점"%nm); continue
        loop = nm in ("ball","body","shell","core","head","b1","b2","b3","dotted-ring") or (len(pts)>3 and pts[0]==pts[-1])
        tm=true_min(pts, loop)
        if tm is not None:
            mins.append("%s %.2f획"%(nm,tm/WC))
            if tm < WC: msgs.append("%s 최단 실제 변 %.2f획 < 1.0"%(nm,tm/WC))
        x0,y0,x1,y1=bounds(pts)
        if max(x1-x0,y1-y0) < 1.5*WC: msgs.append("%s 잉크 사각형 %.2f획 < 1.5"%(nm,max(x1-x0,y1-y0)/WC))
    print("  %s %-18s 도형%d 보조색%d | %s"%("✗" if msgs else "✓",name,len(parts),acc," · ".join(mins)))
    for m in msgs: print("      - "+m); bad+=1
print("╚══ 위반 %d건 ══╝"%bad)
