# -*- coding: utf-8 -*-
"""42종 폴백 아이콘을 **카드 규칙**으로 전수 검사한다(파싱은 .asset 원본에서 — 손으로 베끼지 않는다).
카드 획 = CharacterInfoWindow.IconStroke = 1.7 * 44/40 캔버스 유닛 = viewBox 단위로 1.7."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cards42 as C
import rig

WC = C.STROKE_V   # 1.7 (viewBox 단위)

def true_min_edge(s):
    n=len(s.pts); best=None
    for i in range(n if s.loop else n-1):
        L=math.dist(s.pts[i], s.pts[(i+1)%n])
        if L<1e-9: continue
        best=L if best is None else min(best,L)
    return best

print()
print("╔══ 42종 폴백 아이콘 · 카드 규칙 전수 검사 (viewBox 40, 획 %.1f) ══╗" % WC)
print("%-30s %-5s %-6s %-6s %-8s %s" % ("아이템","자리","카드","정원","보조색","위반"))
print("-"*104)
tot=0; live_bad=0
for r in C.rows:
    msgs=[]
    parts=r["parts"]; fb=r["fb"]
    n=len(parts); acc=sum(1 for p in parts if p["tone"]==1)
    if not (2<=n<=4): msgs.append("정원 %d개(2~4 밖)"%n)
    if acc!=1: msgs.append("보조색 %d개(정확히 1)"%acc)
    for i,(p,s) in enumerate(zip(parts,fb)):
        if p["kind"] in (1,2,3):
            rr=p["values"][2]
            # 링/점선링은 획 하나짜리 테두리. 지름 < 1.5획이면 잉크 사각형 위반
            if 2*rr < 1.5*WC: msgs.append("p%d(%s) 지름 %.2f획 < 1.5"%(i,C.KIND[p["kind"]],2*rr/WC))
            continue
        if len(s.pts)<2: msgs.append("p%d 점 1개"%i); continue
        x0,y0,x1,y1=rig.bounds(s.pts)
        if max(x1-x0,y1-y0) < 1.5*WC: msgs.append("p%d 잉크 사각형 %.2f획 < 1.5"%(i,max(x1-x0,y1-y0)/WC))
        tm=true_min_edge(s)
        if tm is not None and tm < WC: msgs.append("p%d 최단 실제 변 %.2f획 < 1.0"%(i,tm/WC))
        if s.loop and rig.self_intersects(s.pts): msgs.append("p%d 자기교차"%i)
        # viewBox 밖으로 나가는가(획 반폭 포함)
        if x0 < WC/2 or y0 < WC/2 or x1 > C.VIEW-WC/2 or y1 > C.VIEW-WC/2:
            msgs.append("p%d 상자 밖(획 반폭 포함)"%i)
    print("%-30s %-5s %-6s %-6s %-8s %s" %
          (r["name"], r["cat"], "폴백" if r["live"] else "몸(사문)", "%d개"%n, "%d개"%acc,
           " · ".join(msgs) if msgs else "—"))
    tot += len(msgs)
    if r["live"]: live_bad += len(msgs)
print("╚══ 폴백 위반 합계 %d건 · 그중 **카드에 실제로 보이는** 12종의 위반 %d건 ══╝" % (tot, live_bad))
