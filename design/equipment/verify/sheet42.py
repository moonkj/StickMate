# -*- coding: utf-8 -*-
"""42종 카드: 폴백(왼) vs 몸 도형 투영(오른) 대조 시트.
★ 오프라인 래스터는 **둥근 캡**을 찍는다 — 코너 붕괴를 가린다(2026-09-01 발 사고).
   여기서는 '같은 물건으로 읽히는가'만 본다. 최종 판정은 실제 빌드 캡처로만 한다."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import cards42 as C

Z = 6            # 확대
CELL = int(C.VIEW * Z)
GAP = 8
cols = 6
rows_ = (len(C.rows) + cols - 1) // cols
W_ = cols * (CELL*2 + GAP*2) + GAP
H_ = rows_ * (CELL + 26) + GAP
img = Image.new("RGB", (W_, H_), (24, 26, 30))
d = ImageDraw.Draw(img)

def draw(shapes, ox, oy, ink=(226,228,232), acc=(232,131,74)):
    for s in shapes:
        col = acc if s.tone == 1 else ink
        pts = [(ox + x*Z, oy + y*Z) for x, y in s.pts]
        if s.filled and len(pts) >= 3:
            d.polygon(pts, fill=tuple(int(c*0.55) for c in col))
        seq = pts + [pts[0]] if s.loop else pts
        d.line(seq, fill=col, width=max(1, int(round(1.7*Z*0.5))), joint="curve")

for i, r in enumerate(C.rows):
    cx = GAP + (i % cols) * (CELL*2 + GAP*2)
    cy = GAP + (i // cols) * (CELL + 26)
    d.rectangle([cx, cy, cx+CELL, cy+CELL], outline=(60,64,70))
    d.rectangle([cx+CELL+GAP, cy, cx+CELL*2+GAP, cy+CELL], outline=(60,64,70))
    draw(r["fb"], cx, cy)
    if r["body"]:
        bv = C.to_viewbox(r["body"])
        draw(bv, cx+CELL+GAP, cy)
    tag = "%s %s" % ("★" if r["live"] else " ", r["name"].replace("equip_","").replace("look_",""))
    d.text((cx, cy+CELL+4), tag[:34], fill=(180,184,190))
    d.text((cx, cy+CELL+14), "폴백 | 몸" if r["body"] else "폴백 | (몸 없음)", fill=(120,124,130))
img.save(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "cards42-compare.png"))
print("wrote design/equipment/cards42-compare.png  %dx%d" % img.size)
