# -*- coding: utf-8 -*-
"""30종 폴백 아이콘을 **몸 도형에서 유도**한다(손으로 적지 않는다).

투영은 AccessoryCardIcon.TryBuild가 카드에서 실제로 쓰는 것과 **같은 식**이다:
   잉크 사각형을 0.86·40 = 34.4에 담고 40×40 한가운데 정렬.
그래서 폴백은 '카드가 지금 그리는 그림'과 **같은 좌표**가 되고, 둘이 갈라질 자리가 사라진다.

★ 2026-09-02 정정: 그 한계가 사라졌다. `ItemIconPartKind.Polygon`(4)이 생겨 폴백도 **채운 면**을
   표현한다(CharacterInfoWindow.BuildIcon이 카드 본경로와 **같은** AccessoryFillGraphic으로 채운다).
   그래서 아래 dump는 몸의 `filled` 여부를 그대로 kind로 옮긴다 — filled면 4(Polygon), 아니면 0(Polyline).
   (옛 한계: Polyline/Ring/DashedRing/Dot뿐이라 유도된 폴백이 같은 좌표의 **속 빈 윤곽선**이었다.)
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair
import cards42 as C

WC = C.STROKE_V; LO, HI = WC/2, C.VIEW-WC/2
TONE = {0:"주색", 1:"보조색", 2:"그늘"}

def true_min_edge(s):
    n=len(s.pts); best=None
    for i in range(n if s.loop else n-1):
        L=math.dist(s.pts[i], s.pts[(i+1)%n])
        if L<1e-9: continue
        best=L if best is None else min(best,L)
    return best

CATS=[("HEAD",items.HEAD),("EYES",items.EYES),("NECK",items.NECK),("BACK",items.BACK),("HAIR",hair.SET)]
print("╔══ 30종 유도 폴백 · 카드 규칙 검산 (viewBox 40 · 획 %.1f) ══╗" % WC)
bad=0; DERIVED={}
for cat, table in CATS:
    for nm, sh in table.items():
        # 그늘(Shade) 조각은 아이콘에 넣지 않는다 — 폴백에 음영이라는 개념이 없다(리더 판정: 정당).
        keep=[s for s in sh if s.tone!=2]
        P=C.to_viewbox(keep); DERIVED[(cat,nm)]=P
        msgs=[]; info=[]
        acc=sum(1 for s in P if s.tone==1)
        if not (2<=len(P)<=4): msgs.append("정원 %d개"%len(P))
        if acc!=1: msgs.append("보조색 %d개"%acc)
        for s in P:
            tm=true_min_edge(s); info.append("%s %.2f획"%(s.name,tm/WC))
            if tm<WC: msgs.append("%s 최단 실제 변 %.2f획 < 1.0"%(s.name,tm/WC))
            x0,y0,x1,y1=rig.bounds(s.pts)
            if max(x1-x0,y1-y0)<1.5*WC: msgs.append("%s 잉크 사각형 %.2f획 < 1.5"%(s.name,max(x1-x0,y1-y0)/WC))
            if s.loop and rig.self_intersects(s.pts): msgs.append("%s 자기교차"%s.name)
            if x0<LO-1e-6 or y0<LO-1e-6 or x1>HI+1e-6 or y1>HI+1e-6: msgs.append("%s 상자 밖"%s.name)
        print("  %s %-5s %-8s 조각%d 보조색%d | %s"%("✗" if msgs else "✓",cat,nm,len(P),acc," · ".join(info)))
        for m in msgs: print("      - "+m); bad+=1
print("╚══ 위반 %d건 ══╝"%bad)

if "--dump" in sys.argv:
    print(); print("── 유도된 폴백 좌표 전문 (icon: kind 0 = Polyline / 4 = Polygon(채움) / values / tone) ──")
    for (cat,nm),P in DERIVED.items():
        print("  %s %s"%(cat,nm))
        for s in P:
            v=[]
            for x,y in s.pts: v+=[x,y]
            if s.loop: v+=[s.pts[0][0], s.pts[0][1]]
            print("    %-18s kind %d(%s)  tone %d(%s)  %d점  values [%s]"
                  %(s.name, 4 if s.filled else 0, "Polygon" if s.filled else "Polyline",
                    s.tone, TONE[s.tone], len(v)//2, ", ".join("%.2f"%t for t in v)))
