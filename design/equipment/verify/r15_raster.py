# -*- coding: utf-8 -*-
"""R15 — 우리 데이터 모델(r15_model.Piece) 오프라인 래스터. 4배 슈퍼샘플 → LANCZOS 축소.
★ README 경고 그대로: 오프라인 래스터는 둥근 캡을 찍어 코너 붕괴를 가린다. 최종 판정은 실제 빌드 캡처로만.
   이 그림의 목적은 「인계본 렌더와 나란히 놓고 같은 퀄리티로 보이는가」를 눈으로 보는 것이다(§13-0)."""
import math, os
from PIL import Image, ImageDraw, ImageFont

SS = 4

class Canvas:
    def __init__(self, w, h, bg):
        self.w, self.h = int(w), int(h)
        self.im = Image.new("RGB", (self.w * SS, self.h * SS), bg)
        self.d = ImageDraw.Draw(self.im)

    def _p(self, pts):
        return [(x * SS, y * SS) for x, y in pts]

    def fill(self, pts, color):
        if len(pts) >= 3: self.d.polygon(self._p(pts), fill=color)

    def stroke(self, pts, width, color, loop):
        if len(pts) < 2 or width <= 0: return
        w = max(1, int(round(width * SS)))
        p = self._p(pts)
        if loop: p = p + [p[0]]
        self.d.line(p, fill=color, width=w, joint="curve")
        r = w / 2.0
        # 둥근 캡은 **끝점에만**(고리는 이음점 하나). 첫 판은 모든 꼭짓점에 원을 찍어 윤곽이 울퉁불퉁해졌다 —
        # 그건 설계가 아니라 도구 결함이었다(joint="curve" 가 이미 이음을 둥글게 한다).
        for q in ((p[0],) if loop else (p[0], p[-1])):
            self.d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=color)

    def ellipse(self, cx, cy, r, fill=None, outline=None, width=1.0):
        b = [(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS]
        self.d.ellipse(b, fill=fill, outline=outline, width=max(1, int(round(width * SS))))

    def line(self, a, b, width, color):
        self.stroke([a, b], width, color, False)

    def done(self):
        return self.im.resize((self.w, self.h), Image.LANCZOS)


def draw_pieces(cv, pieces, colors, to_px, width_px, surf_bit=None):
    """pieces 를 순서대로 그린다. colors: {piece: (fill, outline)}. width_px(piece) → 획 px."""
    for p in pieces:
        if surf_bit is not None and not (p.surfaces & surf_bit): continue
        fill, line = colors[p]
        pts = [to_px(q) for q in p.pts]
        if p.filled and fill is not None: cv.fill(pts, fill)
        if line is not None: cv.stroke(pts, width_px(p), line, p.loop)


_FONT = "/System/Library/Fonts/AppleSDGothicNeo.ttc"
def font(size, bold=False):
    try:
        return ImageFont.truetype(_FONT, size, index=5 if bold else 2)
    except Exception:
        try: return ImageFont.truetype(_FONT, size)
        except Exception: return ImageFont.load_default()


def text_block(d, xy, lines, size=12, color=(60, 60, 60), lh=None, bold_first=False):
    x, y = xy; lh = lh or int(size * 1.45)
    for i, ln in enumerate(lines):
        d.text((x, y), ln, fill=color, font=font(size, bold=(bold_first and i == 0)))
        y += lh
    return y
