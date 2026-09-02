# -*- coding: utf-8 -*-
"""처방 시안 굽기 — **판정 아님**(오프라인 렌더러가 코너 붕괴를 가린 전례가 있다).
   실제 픽셀 크기 그대로 굽고 그 옆에 7배 확대를 붙인다. 판정은 실기 빌드 캡처로만.
   python3 sheet_wornfix.py  ->  ../wornfix-proof.png"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import rig, items, wornread as WR, wornfix as F

PT_PER_UNIT = 846.0 / 24.0
DEV = 2.0                       # 레티나 백킹 스토어

def render(shapes, scale, R_px, pad, body=True, zoom=1):
    """머리 원반 + 아이템. 획 두께는 프로덕션과 같은 두 하한을 쓴다."""
    wl = WR.w_line(scale) * R_px * zoom
    wf = WR.w_fill(scale) * R_px * zoom
    S = R_px * zoom
    Wpx = int(2 * (S * 1.6 + pad)); Hpx = int(2 * (S * 2.1 + pad))
    im = Image.new("RGB", (Wpx, Hpx), (233, 234, 230))
    d = ImageDraw.Draw(im)
    cx, cy = Wpx / 2, Hpx * 0.62
    P = lambda p: (cx + p[0] * S, cy - p[1] * S)
    if body:
        d.ellipse([cx - S, cy - S, cx + S, cy + S], fill=(0, 0, 0))
        d.line([P((0, -1.0)), P((0, -3.2))], fill=(0, 0, 0), width=max(1, int(round(wl))))
    PRIM, ACC, SHD = (185, 145, 103), (200, 179, 126), (114, 90, 64)
    for s in shapes:
        col = ACC if s.tone == 1 else (SHD if s.tone == 2 else PRIM)
        pts = [P(p) for p in s.pts]
        if s.filled:
            d.polygon(pts, fill=col)
            out = tuple(int(c * 0.62) for c in col)
            d.line(pts + [pts[0]], fill=out, width=max(1, int(round(wf))), joint="curve")
        else:
            d.line(pts, fill=col, width=max(1, int(round(wl))), joint="curve")
    return im


def strip(title_shapes, scale, out):
    tiles = []
    for name, sh in title_shapes:
        R_px = 0.22 * scale * PT_PER_UNIT * DEV          # 실제 디바이스 픽셀
        small = render(sh, scale, R_px, 6)
        big = render(sh, scale, R_px, 6, zoom=7)
        tiles.append((name, small, big))
    w = sum(t[1].width + t[2].width + 24 for t in tiles)
    h = max(max(t[1].height, t[2].height) for t in tiles) + 8
    im = Image.new("RGB", (w, h), (20, 20, 20))
    x = 0
    for name, sm, bg in tiles:
        im.paste(sm, (x, 4)); x += sm.width + 8
        im.paste(bg, (x, 4)); x += bg.width + 16
    im.save(out)
    print("  ->", out, im.size)


if __name__ == "__main__":
    root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    old = items.HEAD["털모자"]
    new = F.beanie_v3()
    for sc in (0.75, 0.60):
        strip([("현행", old), ("처방", new)], sc,
              os.path.join(root, "wornfix-beanie-%.2f.png" % sc))
    strip([("현행", items.EYES["선글라스"]), ("처방", F.sunglasses_v2()),
           ("현행", items.EYES["동그란안경"]), ("처방", F.round_glasses_v2())], 0.75,
          os.path.join(root, "wornfix-eyes-0.75.png"))
    print("★ 이 그림은 시안이다. 판정은 실기 빌드 캡처로만.")
