# -*- coding: utf-8 -*-
"""털모자/베레모 현행 vs 수정안. 획을 **실제 두께로** 얹어 배율 0.60과 0.75를 나란히.
★ 둥근 캡이 코너 붕괴를 가린다 — 최종 판정은 빌드 캡처로."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import rig, items, scale060 as S

PX = 46.0          # 1 R = 46 px
PAD = 24
def render(shapes, w_in_R, title):
    Wpx = int(6.4*PX); Hpx = int(5.0*PX)
    im = Image.new("RGB", (Wpx, Hpx), (22,24,28)); d = ImageDraw.Draw(im)
    ox, oy = Wpx/2, Hpx*0.72
    def T(p): return (ox + p[0]*PX, oy - p[1]*PX)
    d.ellipse([T((-1,1))[0],T((-1,1))[1],T((1,-1))[0],T((1,-1))[1]], outline=(70,74,80), width=2)  # 머리
    lw = max(1, int(round(w_in_R*PX)))
    for s in shapes:
        col = (232,131,74) if s.tone==1 else (226,228,232)
        pts=[T(p) for p in s.pts]
        if s.filled and len(pts)>=3: d.polygon(pts, fill=(120,124,132))
        d.line(pts+[pts[0]] if s.loop else pts, fill=col, width=lw, joint="curve")
    d.text((8,8), title, fill=(190,194,200))
    return im
rows=[]
for nm, new in (("털모자", S.patched()["털모자"]), ("베레모", S.patched()["베레모"])):
    for sc in (0.60, 0.75):
        w=S.W(sc)
        rows.append([render(items.HEAD[nm], w, "%s 현행 · 배율 %.2f (W=%.3fR)"%(nm,sc,w)),
                     render(new,            w, "%s 수정안 · 배율 %.2f"%(nm,sc))])
cw,ch = rows[0][0].size
sheet = Image.new("RGB", (cw*2+12, ch*len(rows)+6*len(rows)), (14,15,18))
for r,pair in enumerate(rows):
    for c,im in enumerate(pair): sheet.paste(im, (c*(cw+12), r*(ch+6)))
sheet.save(os.path.join(os.path.dirname(os.path.abspath(__file__)),"..","scale060-hats.png"))
print("wrote design/equipment/scale060-hats.png %dx%d"%sheet.size)
