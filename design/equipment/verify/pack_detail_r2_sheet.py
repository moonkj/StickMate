# -*- coding: utf-8 -*-
"""나란히 비교 시트 — 출하(왼쪽) vs R2 제안(오른쪽), 12행. 오프라인 래스터(둥근 캡)라 **최종 판정이 아니다** — 실기 캡처로 판정한다.
   python3 pack_detail_r2_sheet.py  ->  design/equipment/pack_detail_r2_sheet.png"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import pack_detail_r2 as D, pack_assets as PA

PX = 44.0            # px per R  (카드 58pt HEAD 가 18.3pt/R 이니 이 시트는 카드의 ~2.4배 확대)
CELL = (300, 300); SS = 3
COLORS = {"cyber": ((0, 150, 130), (81, 141, 133)), "mine": ((201, 111, 0), (141, 114, 81)), "arcane": ((0, 153, 46), (81, 141, 99))}
HEAD_C = (30, 30, 34)

def tone_color(tone, key):
    M, M2 = COLORS[key]
    if tone == 1: return M2
    if tone == 2: return tuple(int(c*0.28) for c in M)
    return M

def draw_item(dr, ox, oy, pieces, key, slot, anchor_y):
    """pieces: dict(pts, loop, filled, tone, noStroke, layer, kind)"""
    def P(x, y): return (ox + x*PX*SS, oy - (y - anchor_y)*PX*SS)
    R = PX*SS
    back = [p for p in pieces if p.get("layer", 0) == 1]; front = [p for p in pieces if p.get("layer", 0) != 1]
    def paint(p):
        pts = [P(x, y) for x, y in p["pts"]]
        M, M2 = COLORS[key]
        if p["filled"]:
            dr.polygon(pts, fill=tone_color(p["tone"], key))
            if not p.get("noStroke"): dr.line(pts + [pts[0]], fill=(20, 20, 20), width=int(0.14*R))
        else:
            k = p.get("kind", "line"); w = int(0.14*R)
            col = (20, 20, 20)
            if k == "hi": col = (255, 255, 255); w = int(0.10*R)
            elif k == "seam": col = (60, 60, 60); w = int(0.11*R)
            elif p["tone"] == 1: col = M2
            elif p["tone"] == 0: col = M
            dr.line(pts + ([pts[0]] if p["loop"] else []), fill=col, width=w, joint="curve")
    for p in back: paint(p)
    if slot in ("HEAD", "EYES"):
        dr.ellipse([P(-1.0, 1.0), P(1.0, -1.0)], fill=HEAD_C)
    else:
        # 목·어깨·몸통 (참고선)
        dr.ellipse([P(-1.0, 1.0), P(1.0, -1.0)], fill=HEAD_C)
        dr.line([P(0, -1.0), P(0, -5.09)], fill=HEAD_C, width=int(0.34*R))
        dr.line([P(-1.4, -2.3), P(0, -1.32), P(1.4, -2.3)], fill=HEAD_C, width=int(0.34*R))
    for p in front: paint(p)

def main():
    shipped = PA.pack_items()
    rows = []
    for packname, key, motif, PACK in D.PACKS:
        for slot in ("HEAD", "EYES", "NECK", "BACK"):
            disp, iid, fn = PACK[slot]
            old = [dict(pts=s["pts"], loop=s["loop"], filled=s["filled"], tone=s["tone"], noStroke=s["noStroke"], layer=s["layer"], kind="line") for s in shipped[(slot, key)][1]]
            new = [p.to_dict() for p in fn()]
            rows.append((key, slot, disp, old, new))
    W, H = CELL[0]*2 + 40, CELL[1]*len(rows) + 40
    img = Image.new("RGB", (W*SS, H*SS), (236, 232, 224)); dr = ImageDraw.Draw(img)
    for r, (key, slot, disp, old, new) in enumerate(rows):
        oy = (40 + CELL[1]*r + CELL[1]*0.62) * SS
        if slot in ("NECK", "BACK"): oy = (40 + CELL[1]*r + CELL[1]*0.30) * SS
        anchor = 0.0
        draw_item(dr, (20 + CELL[0]*0.5)*SS, oy, old, key, slot, anchor)
        draw_item(dr, (20 + CELL[0]*1.5)*SS, oy, new, key, slot, anchor)
        dr.text(((20)*SS, (40 + CELL[1]*r + 6)*SS), "%s / %s  — shipped" % (key, disp), fill=(40, 40, 40))
        dr.text(((20 + CELL[0])*SS, (40 + CELL[1]*r + 6)*SS), "R2 proposal", fill=(40, 40, 40))
        dr.line([(20*SS, (40 + CELL[1]*(r+1))*SS), ((W-20)*SS, (40 + CELL[1]*(r+1))*SS)], fill=(180, 180, 180), width=SS)
    img = img.resize((W, H), Image.LANCZOS)
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pack_detail_r2_sheet.png")
    img.save(out); print("wrote", os.path.relpath(out), img.size)

if __name__ == "__main__":
    main()
