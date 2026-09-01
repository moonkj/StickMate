# -*- coding: utf-8 -*-
"""FX/PET 카드 12장: 현행(왼) vs 제안(오른). ★ 오프라인 래스터는 둥근 캡을 찍는다 — 최종은 빌드 캡처로."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import cards42 as C, cardspec12 as S

Z = 9; CELL = int(C.VIEW*Z); GAP = 10; cols = 4
NOW = {r["name"]: r for r in C.rows if r["cat"] in ("FX","PET")}
KEY = {"FX 없음":"look_fx_none","FX 발자국":"look_fx_footprint","FX 반짝임":"look_fx_sparkle",
       "FX 먼지":"look_fx_dust","FX 물방울":"look_fx_bubble","FX 나뭇잎":"look_fx_leaf",
       "PET 작은공":"look_pet_ball","PET 종이비행기":"look_pet_plane",
       "PET 리틀스틱메이트":"look_pet_mini","PET 커서친구":"look_pet_cursor",
       "PET 풍선":"look_pet_balloon","PET 달팽이":"look_pet_snail"}
rows_ = (len(KEY)+cols-1)//cols
img = Image.new("RGB",(cols*(CELL*2+GAP*2)+GAP, rows_*(CELL+26)+GAP),(24,26,30))
d = ImageDraw.Draw(img)
INK,ACC=(226,228,232),(232,131,74)
def dp(pts, loop, col, w):
    q=[(a,b) for a,b in pts]
    d.line(q+[q[0]] if loop else q, fill=col, width=w, joint="curve")
def draw_now(r, ox, oy):
    for p,s in zip(r["parts"], r["fb"]):
        col = ACC if p["tone"]==1 else INK
        if p["kind"] in (1,2):
            cx,cy,rr=p["values"][:3]
            d.ellipse([ox+(cx-rr)*Z,oy+(cy-rr)*Z,ox+(cx+rr)*Z,oy+(cy+rr)*Z],outline=col,width=max(1,int(1.7*Z*0.5)))
        elif p["kind"]==3:
            cx,cy,rr=p["values"][:3]
            d.ellipse([ox+(cx-rr)*Z,oy+(cy-rr)*Z,ox+(cx+rr)*Z,oy+(cy+rr)*Z],fill=col)
        else:
            dp([(ox+x*Z,oy+y*Z) for x,y in s.pts], s.loop, col, max(1,int(1.7*Z*0.5)))
def draw_new(P, ox, oy):
    for s in P:
        col = ACC if s.tone==1 else INK
        if hasattr(s,"circle"):
            cx,cy,rr=s.circle
            if s.kind=="dot": d.ellipse([ox+(cx-rr)*Z,oy+(cy-rr)*Z,ox+(cx+rr)*Z,oy+(cy+rr)*Z],fill=col)
            else: d.ellipse([ox+(cx-rr)*Z,oy+(cy-rr)*Z,ox+(cx+rr)*Z,oy+(cy+rr)*Z],outline=col,width=max(1,int(1.7*Z*0.5)))
        else:
            dp([(ox+x*Z,oy+y*Z) for x,y in s.pts], s.loop, col, max(1,int(1.7*Z*0.5)))
for i,(nm,key) in enumerate(KEY.items()):
    cx=GAP+(i%cols)*(CELL*2+GAP*2); cy=GAP+(i//cols)*(CELL+26)
    d.rectangle([cx,cy,cx+CELL,cy+CELL],outline=(60,64,70))
    d.rectangle([cx+CELL+GAP,cy,cx+CELL*2+GAP,cy+CELL],outline=(60,64,70))
    draw_now(NOW[key],cx,cy); draw_new(S.OUT[nm],cx+CELL+GAP,cy)
    d.text((cx,cy+CELL+5),"%s   현행 | 제안"%nm,fill=(190,194,200))
img.save(os.path.join(os.path.dirname(os.path.abspath(__file__)),"..","cards12-proposal.png"))
print("wrote design/equipment/cards12-proposal.png %dx%d"%img.size)
