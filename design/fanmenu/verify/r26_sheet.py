"""R26 — 부채꼴 버튼 시트. 실제 크기(44/48px)와 확대(8×)를 같은 시트에 놓는다."""
import sys
import numpy as np
from PIL import Image, ImageDraw
from fanglyph import *

# UiChrome 토큰(거울)
CARD_SURFACE = (27, 31, 38)
TEXT_PRIMARY = (242, 244, 247)
ACCENT = (200, 161, 90)
PANEL_BG = (20, 24, 30)
CARD_INK = None


def render_button(pieces, px_per_pt, accent_names=(), ring_2pt=True,
                  face=CARD_SURFACE, ink=TEXT_PRIMARY, diameter=BUTTON_D, ss=4):
    """원판 + 심볼을 RGBA 로. 알파 램프(EDGE_FEATHER)를 그대로 재현한다."""
    size = int(round(diameter * px_per_pt))
    n = size * ss
    ax = (np.arange(n) + 0.5) / (px_per_pt * ss) - diameter / 2
    gx, gy = np.meshgrid(ax, -ax)

    # 원판
    r = np.hypot(gx, gy)
    disc = np.clip((diameter / 2 - r) / (1.0 / px_per_pt), 0, 1)
    img = np.zeros((n, n, 4), float)
    img[..., :3] = np.array(face) / 255.0
    img[..., 3] = disc
    if ring_2pt:
        ringa = np.clip((1.0 - np.abs(r - (diameter / 2 - 1.0))) / EDGE_FEATHER, 0, 1)
        ringa = np.minimum(ringa, disc)
        for c in range(3):
            img[..., c] = img[..., c] * (1 - ringa) + (np.array(TEXT_PRIMARY)[c] / 255.0) * ringa
        img[..., 3] = np.maximum(img[..., 3], ringa)

    # 심볼 — fanglyph 의 마스크를 이 그리드에 다시 굽는다
    import fanglyph as fg
    old = (fg.GX, fg.GY, fg.SS)
    fg.GX, fg.GY, fg.SS = gx, gy, px_per_pt * ss
    try:
        for p in pieces:
            m = p.mask()
            from scipy import ndimage
            d = ndimage.distance_transform_edt(~m) / (px_per_pt * ss)
            a = np.clip(1.0 - d / EDGE_FEATHER, 0, 1)
            a[m] = 1.0
            col = np.array(ACCENT if p.name in accent_names else ink) / 255.0
            for c in range(3):
                img[..., c] = img[..., c] * (1 - a) + col[c] * a
            img[..., 3] = np.maximum(img[..., 3], a)
    finally:
        fg.GX, fg.GY, fg.SS = old

    img = (img.reshape(size, ss, size, ss, 4).mean(axis=(1, 3)) * 255).astype(np.uint8)
    return Image.fromarray(img, "RGBA")


def sheet(sets, path, title):
    """sets = [(라벨, {이름: (pieces, accent)})] — 행마다 한 세트."""
    zoom = 8
    cell = int(BUTTON_D * zoom)
    pad = 18
    labelh = 22
    cols = max(len(s[1]) for s in sets)
    W = pad + cols * (cell + pad)
    H = pad + 34 + len(sets) * (cell + labelh + 44 + pad)
    out = Image.new("RGB", (W, H), PANEL_BG)
    dr = ImageDraw.Draw(out)
    dr.text((pad, 10), title, fill=(200, 205, 215))
    y = pad + 34
    for label, glyphs in sets:
        dr.text((pad, y - 14), label, fill=(160, 170, 185))
        x = pad
        for name, (pieces, accent) in glyphs.items():
            big = render_button(pieces, zoom, accent).convert("RGB")
            out.paste(big, (x, y))
            # 실제 크기 3종을 그 아래에
            xs = x
            for scale, cap in ((1.0, "1×44"), (1.5, "1.5×"), (2.0, "2×")):
                tiny = render_button(pieces, scale, accent).convert("RGB")
                out.paste(tiny, (xs, y + cell + 6))
                xs += tiny.width + 8
            dr.text((x, y + cell + 6 + 46), name, fill=(150, 160, 175))
            x += cell + pad
        y += cell + labelh + 44 + pad
    out.save(path)
    print("wrote", path, out.size)
