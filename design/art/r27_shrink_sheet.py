#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
R27 시안 — Ø44 / Ø36 / 펼침 전이 / 옛 확성기(양성 대조)를 <같은 자>로 그린 A/B 시트.

렌더러는 `r27_shrink_fg3.py` 가 §0에서 교정한 것과 <같은 알파 모형>이다
(alpha = clamp01(0.5 + sdf/EdgeFeather), 합성 = 1 − Π(1−aᵢ), 화소 중심 점 표본).
그래서 이 그림과 그 숫자는 같은 물건을 본다.

두 장을 낸다:
  r27_shrink_sheet.png       — 화소 구조를 보는 확대판(칸마다 «몇 배 확대»를 적었다)
  r27_shrink_actualsize.png  — 실크기 띠. «작아진다»는 사실 자체는 이쪽으로 본다.

★ 오프라인 렌더다. 최종 판정은 실기 빌드 캡처로만 한다(CLAUDE.md 디자인 7인 공통).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r27_fanmirror as M                                   # noqa: E402
from r27_fanmirror import Capsule, Vec                      # noqa: E402
from r27_shrink_fg3 import (composite, CARD_SURFACE, TEXT_PRIMARY,   # noqa: E402
                            CARD_BORDER, flatten)

FONT = "/System/Library/Fonts/AppleSDGothicNeo.ttc"      # 한글 라벨
BG = (0.055, 0.063, 0.075)
TARGET = 470          # 확대 칸의 목표 변 길이(화소)


def render_button(pieces, k, S, consts, phase=(0.0, 0.0)):
    d = consts["button_d"]
    n = int(round((d + 4) * k * S))
    pitch = 1.0 / (k * S)
    ax = (np.arange(n) - (n - 1) / 2.0) * pitch
    X, Y = np.meshgrid(ax + phase[0] * pitch, -(ax + phase[1] * pitch))
    f = consts["feather"]
    r = np.hypot(X, Y)
    layers = [(np.clip(0.5 + (d / 2 - r) / f, 0, 1), CARD_SURFACE),
              (np.clip(0.5 + np.minimum(d / 2 - r, r - (d / 2 - consts["border_t"])) / f, 0, 1),
               flatten(CARD_BORDER, CARD_SURFACE)),
              (composite(pieces, X, Y, f), TEXT_PRIMARY)]
    img = np.zeros(X.shape + (3,))
    img[:] = BG
    for a, col in layers:
        img = img * (1 - a[..., None]) + np.array(col) * a[..., None]
    return img


def to_img(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).round().astype(np.uint8))


def main():
    glyphs, consts, fan, chrome = M.load_glyphs()
    k36 = consts["shrunk_d"] / consts["button_d"]
    kexp = k36 * fan.need("StartScale")
    old_megaphone = [
        Capsule("HornUpper", 13.0, 2.0, 13.0, Vec(-1.6, 5.0)),
        Capsule("HornLower", 13.0, 2.0, -13.0, Vec(-1.6, -5.0)),
        Capsule("HornNeck", 5.6, 2.0, 90.0, Vec(-8.4, 0.0)),
        Capsule("HornMouth", 11.6, 2.0, 90.0, Vec(5.0, 0.0)),
        Capsule("WaveUpper", 4.6, 1.6, 30.0, Vec(9.6, 3.4)),
        Capsule("WaveLower", 4.6, 1.6, -30.0, Vec(9.6, -3.4)),
    ]
    cols = [("Ø44 @1x\n(기준)", 1.0, 1.0, (0.0, 0.0)),
            ("Ø36 @1x\n(축소 폴백)", k36, 1.0, (0.0, 0.0)),
            ("Ø36 @1x\n위상 +0.5px", k36, 1.0, (0.5, 0.5)),
            (f"Ø36 x 펼침전이\nk={kexp:.3f}", kexp, 1.0, (0.0, 0.0)),
            ("Ø36 @2x\n(Retina)", k36, 2.0, (0.0, 0.0))]
    rows = list(glyphs.items()) + [("★대조 옛 확성기\n(R26 이전 0.43pt)", old_megaphone)]

    f_lab = ImageFont.truetype(FONT, 20)
    f_sub = ImageFont.truetype(FONT, 16)
    pad, head, side = 10, 64, 250
    W = side + len(cols) * (TARGET + pad) + pad
    H = head + len(rows) * (TARGET + pad) + pad
    sheet = Image.new("RGB", (W, H), (14, 16, 19))
    dr = ImageDraw.Draw(sheet)
    for ci, (label, k, S, ph) in enumerate(cols):
        x = side + ci * (TARGET + pad)
        dr.multiline_text((x + 6, 8), label, font=f_lab, fill=(210, 214, 222))
    for ri, (name, pieces) in enumerate(rows):
        y = head + ri * (TARGET + pad)
        dr.multiline_text((10, y + 10), name, font=f_lab, fill=(210, 214, 222))
        for ci, (label, k, S, ph) in enumerate(cols):
            img = to_img(render_button(pieces, k, S, consts, ph))
            zoom = max(1, TARGET // img.width)
            im = img.resize((img.width * zoom, img.height * zoom), Image.NEAREST)
            x = side + ci * (TARGET + pad) + (TARGET - im.width) // 2
            sheet.paste(im, (x, y + (TARGET - im.height) // 2))
            dr.text((x, y + TARGET - 18), f"{img.width}x{img.height}px  x{zoom}",
                    font=f_sub, fill=(120, 128, 140))
    p1 = os.path.join(os.path.dirname(os.path.abspath(__file__)), "r27_shrink_sheet.png")
    sheet.save(p1)
    print("wrote", p1, sheet.size)

    # 실크기 띠 — 확대 없음.
    order = [(1.0, 1.0, "Ø44@1x"), (k36, 1.0, "Ø36@1x"), (1.0, 2.0, "Ø44@2x"), (k36, 2.0, "Ø36@2x")]
    tiles = [[to_img(render_button(p, k, S, consts)) for k, S, _ in order]
             for _, p in glyphs.items()]
    cw = max(t.width for row in tiles for t in row) + 6
    ch = max(t.height for row in tiles for t in row) + 6
    strip = Image.new("RGB", (len(order) * cw * 5 + 20, ch + 26), (14, 16, 19))
    d2 = ImageDraw.Draw(strip)
    x = 10
    for row in tiles:
        for t in row:
            strip.paste(t, (x + (cw - t.width) // 2, 20 + (ch - t.height) // 2))
            x += cw
    for i, (_, _, lab) in enumerate(order * 5):
        d2.text((10 + i * cw + 2, 3), lab, font=f_sub, fill=(120, 128, 140))
    p2 = os.path.join(os.path.dirname(os.path.abspath(__file__)), "r27_shrink_actualsize.png")
    strip.save(p2)
    print("wrote", p2, strip.size)


if __name__ == "__main__":
    main()
