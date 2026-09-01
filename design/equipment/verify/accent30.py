# -*- coding: utf-8 -*-
"""리더가 확정한 판정 축 2개로 30종을 전수 대조한다.
  축1 = 보조색 조각 수 (규칙 3-2 "정확히 1개")
  축2 = 보조색 조각의 꼭짓점 수 (베레모 사고의 축). 원/점 보조색은 축2에서 제외."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cards42 as C, rig
NAMES = {("HEAD",0):"야구모자",("HEAD",1):"털모자",("HEAD",2):"중절모",("HEAD",3):"왕관",("HEAD",4):"베레모",("HEAD",5):"밀짚모자"}
print("╔══ 30종 · 보조색 축 2개 대조 (몸 ↔ 폴백) ══╗")
print("%-24s %-10s %-14s %-14s %s" % ("아이템","자리","보조색 조각 수","보조색 꼭짓점","판정"))
print("-"*104)
A=B=0
for r in C.rows:
    if r["live"] or not r["body"]: continue
    bodyacc=[s for s in r["body"] if s.tone==1]
    fbacc=[(p,s) for p,s in zip(r["parts"], r["fb"]) if p["tone"]==1]
    nb, nf = len(bodyacc), len(fbacc)
    # 꼭짓점: 원/점(kind 1,2,3)은 세지 않는다
    vb = sum(len(s.pts) for s in bodyacc)
    vf = sum(len(s.pts) for p,s in fbacc if p["kind"]==0)
    circ = any(p["kind"] in (1,2,3) for p,s in fbacc)
    v = []
    if nf != 1: v.append("축1 위반(폴백 %d개)"%nf)
    elif not circ and vb != vf: v.append("축2 다름(몸 %d ↔ 폴백 %d)"%(vb,vf))
    if nf!=1: A+=1
    elif not circ and vb!=vf: B+=1
    print("%-24s %-10s %-14s %-14s %s"%(r["name"], r["cat"], "%d ↔ %d"%(nb,nf),
          ("원/점" if circ else "%d ↔ %d"%(vb,vf)), " · ".join(v) if v else "일치"))
print("╚══ 축1 위반 %d건 · 축2 위반 %d건 ══╝"%(A,B))
