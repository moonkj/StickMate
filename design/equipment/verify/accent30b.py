# -*- coding: utf-8 -*-
"""축2(보조색 꼭짓점)를 **두 가지 세는 법**으로 동시에 낸다.
 · 몸(Shape.Points)은 닫힘 표시가 Loop 플래그라 **닫는 점을 안 적는다**.
 · 폴백(ItemIconPart)은 "닫힌 도형이면 마지막 점이 첫 점과 같다"라고 형식이 규정한다 = **닫는 점을 적는다**.
그래서 같은 도형이라도 폴백이 **항상 1 크게** 세어진다. 이 차이를 지우고 봐야 진짜 결함이 남는다."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cards42 as C
print("╔══ 축2 · 두 셈법 ══╗")
print("%-24s %-16s %-16s %s"%("아이템","원값(닫는점 포함)","정규화(닫는점 제거)","판정"))
print("-"*100)
real=0
for r in C.rows:
    if r["live"] or not r["body"]: continue
    bodyacc=[s for s in r["body"] if s.tone==1]
    pairs=[(p,s) for p,s in zip(r["parts"], r["fb"]) if p["tone"]==1]
    # ★ 2026-09-02: Polygon(4)도 꼭짓점을 가진 종류다(빠뜨리면 채운 보조색이 통째로 침묵한다).
    if len(pairs)!=1 or pairs[0][0]["kind"] not in (0, 4): continue
    p,s=pairs[0]
    raw=len(p["values"])//2                    # 에셋에 적힌 그대로
    norm=len(s.pts)                            # 닫는 중복점 제거
    vb=sum(len(x.pts) for x in bodyacc)
    closed = s.loop
    tag=[]
    if vb!=norm: tag.append("★ 실제 차이 %d ↔ %d"%(vb,norm)); real+=1
    elif vb!=raw: tag.append("셈법 차이뿐(닫는 점 1개)")
    print("%-24s %-16s %-16s %s"%(r["name"],"%d ↔ %d"%(vb,raw),"%d ↔ %d"%(vb,norm)," · ".join(tag) if tag else "일치"))
print("╚══ 셈법을 맞춘 뒤 남는 축2 결함 %d건 ══╝"%real)
