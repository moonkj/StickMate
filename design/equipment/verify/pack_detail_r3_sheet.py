# -*- coding: utf-8 -*-
"""R3 나란히 시트 — [카드 비트맵 | 출하(R2) 벡터 | R3 벡터] × 12.
★ 오프라인 래스터다. 최종 판정이 아니다(CLAUDE.md: 실기 캡처로만) — «어디가 달라졌는지»를 보는 용도.
   둥근 캡·안티에일리어싱·정렬 순서는 Unity 와 다르다.
  python3 pack_detail_r3_sheet.py
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import rig, pack_assets as PA
import pack_detail_r2 as R2
import pack_detail_r3 as R3
from pack_detail_r2 import NOMINAL

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
ICONS = os.path.join(ROOT, 'Assets/_Project/Resources/Items/Icons')
OUT = os.path.join(ROOT, 'design/equipment/pack_detail_r3_sheet.png')

# 카탈로그 색(에셋 icon tone0/1) — ItemCatalog.PrimaryColor / SecondaryColor 와 같은 원천
PAL = {"cyber": ((0x00, 0x96, 0x82), (0x51, 0x8D, 0x85)),
       "mine":  ((0xC9, 0x6F, 0x00), (0x8D, 0x72, 0x51)),
       "arcane": ((0x00, 0x99, 0x2E), (0x51, 0x8D, 0x63))}
INK = (24, 26, 30)
BG = (0x15, 0x18, 0x1E)
CELL = 300

def tone_fill(tone, pal):
    if tone == 1: return pal[1]
    if tone == 2: return tuple(int(c*0.28) for c in pal[0])
    return pal[0]

def draw_pieces(im, pcs, slot, pal, box):
    x0, y0, x1, y1 = box
    allp = [q for p in pcs for q in p.pts]
    bx0, by0, bx1, by1 = rig.bounds(allp)
    span = max(bx1-bx0, by1-by0) * 1.10
    cx, cy = (bx0+bx1)/2, (by0+by1)/2
    S = (x1-x0)/span
    T = lambda p: (x0 + (x1-x0)/2 + (p[0]-cx)*S, y0 + (y1-y0)/2 - (p[1]-cy)*S)
    w = max(1.0, NOMINAL[slot]*S*0.9)
    lay = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(lay, "RGBA")
    for p in sorted(pcs, key=lambda q: (0 if q.layer == 1 else 1)):
        pts = [T(q) for q in p.pts]
        pr = p.params(slot)
        if p.filled:
            d.polygon(pts, fill=tone_fill(p.tone, pal) + (255,))
        if not p.noStroke:
            if p.tone == 3: col = (255, 255, 255, int(255*pr["lineAlpha"]))
            elif p.tone == 4: col = INK + (int(255*pr["lineAlpha"]),)
            elif not p.filled and p.tone == 1: col = pal[1] + (255,)
            elif not p.filled and p.tone == 0: col = pal[0] + (255,)
            else: col = INK + (255,)
            lw = max(1, int(round(w*pr["strokeMult"])))
            seq = pts + ([pts[0]] if p.loop and len(pts) > 2 else [])
            d.line(seq, fill=col, width=lw, joint="curve")
            for q in seq:
                d.ellipse([q[0]-lw/2, q[1]-lw/2, q[0]+lw/2, q[1]+lw/2], fill=col)
    im.alpha_composite(lay)

def main():
    rows = 12
    W, H = CELL*3, CELL*rows
    im = Image.new("RGBA", (W, H), BG + (255,))
    dr = ImageDraw.Draw(im)
    shipped = PA.pack_items()
    r = 0
    for packname, key, motif, PACK in R3.PACKS:
        for slot in ("HEAD", "EYES", "NECK", "BACK"):
            disp, iid, fn = PACK[slot]
            y = r*CELL
            fname = R3.FILES[(key, slot)]
            bm = Image.open(os.path.join(ICONS, fname + ".png")).convert("RGBA").resize((CELL, CELL), Image.LANCZOS)
            im.paste(bm, (0, y))
            # 출하(R2) — 에셋에서 직접 읽은 좌표
            old = shipped[(slot, key)][1]
            class P:  # 최소 어댑터
                def __init__(s, d_):
                    s.pts = d_["pts"]; s.loop = d_["loop"]; s.filled = d_["filled"]; s.tone = d_["tone"]
                    s.layer = d_["layer"]; s.noStroke = d_["noStroke"]
                    s.kind = "fill"
                def params(s, slot): return dict(strokeMult=1, strokeInR=0, noStroke=int(s.noStroke), alpha=1, lineAlpha=1)
            draw_pieces(im, [P(d_) for d_ in old], slot, PAL[key], (CELL, y, CELL*2, y+CELL))
            draw_pieces(im, fn(), slot, PAL[key], (CELL*2, y, CELL*3, y+CELL))
            dr.text((CELL+6, y+6), "%s  SHIPPED(R2)" % disp, fill=(200, 205, 215, 255))
            dr.text((CELL*2+6, y+6), "%s  R3" % disp, fill=(200, 205, 215, 255))
            dr.text((6, y+6), "%s  CARD BITMAP" % disp, fill=(200, 205, 215, 255))
            r += 1
    im.convert("RGB").save(OUT)
    print("wrote", OUT, im.size)

main()
