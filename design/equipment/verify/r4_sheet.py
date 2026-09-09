# -*- coding: utf-8 -*-
"""R4 시안 시트 — **오프라인 렌더러다. 최종 판정용이 아니다**(이 저장소는 오프라인 렌더가 코너 붕괴를 가린 사고가 있었다).
착용 배율 0.75 · 2x 장치 해상도로 그리고, 눈으로 보라고 4배 확대해 붙인다."""
import sys, os, numpy as np
sys.path.insert(0, '.')
from PIL import Image, ImageDraw
import r4_geom as G, r4_stack as ST, r4_check, r4_paint as P
import pack_detail_r4 as R4

PPR = 2.0 * G.R_PT_075          # 11.633 px/R (착용 장치 해상도)
ZOOM = 4
PAL = {  # 몸 표면 색 (HandoffFillBase/LineBase 를 그대로 옮김) — 팩 매니페스트의 M/M2
    "cyber":  ((0, 150, 130), (81, 141, 133)),
    "mine":   ((201, 111, 0), (141, 114, 81)),
    "arcane": ((0, 153, 76), (81, 141, 111)),
    "ship":   ((85, 119, 174), (204, 85, 18)),        # 중절모 Felt / TintHead
}
INK = (24, 24, 28)

def draw(pieces, pack, w, h, ox, oy, img=None):
    if img is None: img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    M, M2 = PAL[pack]
    SH = tuple(int(c * 0.28) for c in M)
    d = ImageDraw.Draw(img)
    for i in P._order(pieces):
        p = pieces[i]
        pts = [(ox + x * PPR, oy - y * PPR) for x, y in p["pts"]]
        if len(pts) < 2: continue
        col = {0: M, 1: M2, 2: SH}.get(p["tone"], M)
        if p["filled"] and len(pts) >= 3:
            d.polygon(pts, fill=col + (255,))
        if not p["noStroke"] and p.get("lineAlpha", 1.0) > 0:
            a = int(255 * min(1.0, p.get("lineAlpha", 1.0)))
            lc = (255, 255, 255, a) if p["tone"] == 3 else (INK + (a,))
            wpx = max(1, int(round(G.W * p["strokeMult"] * PPR)))
            seq = pts + [pts[0]] if p["loop"] else pts
            d.line(seq, fill=lc, width=wpx, joint="curve")
    return img

def cell(items, pack, label):
    w = int(9.0 * PPR); h = int(9.0 * PPR); ox, oy = w / 2, h * 0.34
    img = Image.new("RGBA", (w, h), (233, 234, 230, 255))
    d = ImageDraw.Draw(img)
    d.ellipse([ox - PPR, oy - PPR, ox + PPR, oy + PPR], outline=(30, 30, 30, 255), width=2)   # 머리 원반
    d.line([(ox, oy + PPR), (ox, oy + 4.4 * PPR)], fill=(30, 30, 30, 255), width=max(1, int(G.W * PPR)))
    for pieces in items: draw(pieces, pack, w, h, ox, oy, img)
    return img.resize((w * ZOOM, h * ZOOM), Image.NEAREST), label

if __name__ == "__main__":
    p3 = G.load_pack("pack_detail_r3"); b4 = r4_check.to_bank(R4.PACKS); h = G.load_handoff()
    rows = []
    rows.append(("출하 중절모(기준)", [cell([G.body_pieces(h["fedora"])], "ship", "fedora")]))
    for pk in ("cyber", "mine", "arcane"):
        k3 = [k for k in p3 if k.startswith("pack_" + pk + "_")]
        k4 = [k for k in b4 if k.startswith(pk + "_")]
        rows.append((f"pack.{pk}  R3 / R4  (단품 4 + 4종 동시)",
                     [cell([G.body_pieces(p3[k])], pk, k) for k in sorted(k3)]
                     + [cell([G.body_pieces(p3[k]) for k in k3], pk, "R3 4종")]
                     + [cell([G.body_pieces(b4[k])], pk, k) for k in sorted(k4)]
                     + [cell([G.body_pieces(b4[k]) for k in k4], pk, "R4 4종")]))
    cw = rows[1][1][0][0].width; ch = rows[1][1][0][0].height
    W = cw * 10 + 20; H = sum(ch + 26 for _ in rows) + 20
    sheet = Image.new("RGB", (W, H), (20, 22, 26))
    dd = ImageDraw.Draw(sheet)
    y = 10
    for label, cells in rows:
        dd.text((10, y), label, fill=(220, 220, 220))
        y += 18
        x = 10
        for img, cap in cells:
            sheet.paste(img.convert("RGB"), (x, y)); x += cw + 2
        y += ch + 8
    out = os.path.join("..", "pack_detail_r4_sheet.png")
    sheet.save(out); print("wrote", os.path.abspath(out), sheet.size)
