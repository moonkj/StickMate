# -*- coding: utf-8 -*-
"""R16 — 알파 합성 래스터(4× 슈퍼샘플 → LANCZOS). r15_raster 와 달리 **반투명 채움·그라디언트·낱선 알파·
파선·그룹 알파**를 그린다 — 인계본 착용 프리뷰의 문법이 그것이라서다.

★ README 경고 그대로: 오프라인 래스터는 둥근 캡을 찍어 코너 붕괴를 가린다. 최종 판정은 실제 빌드 캡처로만.
★ 이 도구는 r16_worn_sheet.py 가 **Chrome 이 찍은 C열을 이 래스터로 재현해 픽셀 차를 잰다**(도구 교정).
   교정이 깨지면 D′ 열의 인상은 설계가 아니라 도구 탓일 수 있으므로 그 숫자를 시트에 적는다.
"""
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageChops

SS = 4

def _rgb(h):
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))

class Canvas:
    def __init__(self, w, h, bg=None):
        self.w, self.h = int(w), int(h)
        self.im = Image.new("RGBA", (self.w * SS, self.h * SS), (0, 0, 0, 0) if bg is None else tuple(bg) + (255,))

    def _p(self, pts):
        return [(x * SS, y * SS) for x, y in pts]

    def paste_background(self, arr):
        """arr: (h, w, 3) uint8 배경(스테이지 그라디언트 등)."""
        big = Image.fromarray(arr, "RGB").resize((self.w * SS, self.h * SS), Image.BILINEAR).convert("RGBA")
        self.im = Image.alpha_composite(big, self.im)

    # ---- 채움 -------------------------------------------------------------------
    def fill(self, pts, color, alpha=1.0, grad=None):
        """grad = (a0, a1): SVG objectBoundingBox 그라디언트 (0,0)→(0.35,1) 방향으로 알파 a0→a1."""
        if len(pts) < 3: return
        p = self._p(pts)
        xs = [q[0] for q in p]; ys = [q[1] for q in p]
        x0, y0 = int(math.floor(min(xs))) - 1, int(math.floor(min(ys))) - 1
        x1, y1 = int(math.ceil(max(xs))) + 1, int(math.ceil(max(ys))) + 1
        x0, y0 = max(0, x0), max(0, y0); x1, y1 = min(self.im.width, x1), min(self.im.height, y1)
        if x1 <= x0 or y1 <= y0: return
        mask = Image.new("L", (x1 - x0, y1 - y0), 0)
        ImageDraw.Draw(mask).polygon([(x - x0, y - y0) for x, y in p], fill=255)
        m = np.asarray(mask, dtype=np.float32) / 255.0
        if grad is not None:
            a0, a1 = grad
            bw = max(1e-6, max(xs) - min(xs)); bh = max(1e-6, max(ys) - min(ys))
            yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
            u = (xx - min(xs)) / bw; v = (yy - min(ys)) / bh
            t = np.clip((0.35 * u + 1.0 * v) / (0.35 * 0.35 + 1.0), 0.0, 1.0)
            a = a0 + (a1 - a0) * t
            m = m * a
        else:
            m = m * alpha
        layer = np.zeros((y1 - y0, x1 - x0, 4), dtype=np.uint8)
        layer[..., 0], layer[..., 1], layer[..., 2] = color
        layer[..., 3] = np.clip(m * 255.0 + 0.5, 0, 255).astype(np.uint8)
        self.im.alpha_composite(Image.fromarray(layer, "RGBA"), dest=(x0, y0))

    # ---- 획 ---------------------------------------------------------------------
    def stroke(self, pts, width, color, alpha=1.0, loop=False, dash=None):
        if len(pts) < 2 or width <= 0: return
        wpx = width * SS
        p = self._p(pts)
        if dash is not None:
            self._dots(p, wpx, dash, color, alpha, loop); return
        wi = max(1, int(round(wpx)))
        xs = [q[0] for q in p]; ys = [q[1] for q in p]
        pad = wi + 2
        x0, y0 = max(0, int(min(xs)) - pad), max(0, int(min(ys)) - pad)
        x1, y1 = min(self.im.width, int(max(xs)) + pad), min(self.im.height, int(max(ys)) + pad)
        if x1 <= x0 or y1 <= y0: return
        layer = Image.new("RGBA", (x1 - x0, y1 - y0), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        q = [(x - x0, y - y0) for x, y in p]
        if loop: q = q + [q[0]]
        d.line(q, fill=tuple(color) + (255,), width=wi, joint="curve")
        r = wi / 2.0
        # 둥근 캡은 끝점에만(고리는 이음점 하나) — r15_raster 의 교정 그대로
        for c in ((q[0],) if loop else (q[0], q[-1])):
            d.ellipse([c[0] - r, c[1] - r, c[0] + r, c[1] + r], fill=tuple(color) + (255,))
        if alpha < 1.0:
            layer.putalpha(ImageChops.multiply(layer.getchannel("A"), Image.new("L", layer.size, int(round(alpha * 255)))))
        self.im.alpha_composite(layer, dest=(x0, y0))

    def _dots(self, p, wpx, dash, color, alpha, loop):
        """파선(dash on < 폭)은 둥근 캡 때문에 점열이 된다 — 주기마다 지름 = 폭인 점."""
        on, off = dash[0] * SS, dash[1] * SS
        period = on + off
        segs = list(zip(p, p[1:] + ([p[0]] if loop else [])))
        total = sum(math.dist(a, b) for a, b in segs)
        t = 0.0; pts = []
        while t <= total + 1e-6:
            acc = 0.0
            for a, b in segs:
                L = math.dist(a, b)
                if acc + L >= t:
                    u = 0 if L == 0 else (t - acc) / L
                    pts.append((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u)); break
                acc += L
            t += period
        r = (wpx + on) / 2.0
        xs = [q[0] for q in pts]; ys = [q[1] for q in pts]
        pad = int(r) + 2
        x0, y0 = max(0, int(min(xs)) - pad), max(0, int(min(ys)) - pad)
        x1, y1 = min(self.im.width, int(max(xs)) + pad), min(self.im.height, int(max(ys)) + pad)
        layer = Image.new("RGBA", (x1 - x0, y1 - y0), (0, 0, 0, 0)); d = ImageDraw.Draw(layer)
        for c in pts:
            cx, cy = c[0] - x0, c[1] - y0
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=tuple(color) + (255,))
        if alpha < 1.0:
            layer.putalpha(ImageChops.multiply(layer.getchannel("A"), Image.new("L", layer.size, int(round(alpha * 255)))))
        self.im.alpha_composite(layer, dest=(x0, y0))

    def disc(self, cx, cy, r, color, alpha=1.0):
        n = 48
        self.fill([(cx + math.cos(2 * math.pi * i / n) * r, cy + math.sin(2 * math.pi * i / n) * r) for i in range(n)], color, alpha)

    def line(self, a, b, width, color, alpha=1.0):
        self.stroke([a, b], width, color, alpha, False)

    # ---- 그룹 알파 -----------------------------------------------------------------
    def group(self):
        return Canvas(self.w, self.h, None)

    def composite(self, other, alpha=1.0):
        im = other.im
        if alpha < 1.0:
            im = im.copy(); im.putalpha(ImageChops.multiply(im.getchannel("A"), Image.new("L", im.size, int(round(alpha * 255)))))
        self.im = Image.alpha_composite(self.im, im)

    def done(self):
        return self.im.convert("RGB").resize((self.w, self.h), Image.LANCZOS)


_FONT = "/System/Library/Fonts/AppleSDGothicNeo.ttc"
def font(size, bold=False):
    try:
        return ImageFont.truetype(_FONT, size, index=5 if bold else 2)
    except Exception:
        try: return ImageFont.truetype(_FONT, size)
        except Exception: return ImageFont.load_default()

def sanitize(s):
    """AppleSDGothicNeo 에 없는 기호를 있는 것으로 — U+2212 「−」가 □ 로 찍히던 것(R17b 시트에서 발견)."""
    return s.replace("−", "-").replace("′", "'").replace("″", '"')

def text_block(d, xy, lines, size=12, color=(60, 60, 60), lh=None, bold_first=False):
    x, y = xy; lh = lh or int(size * 1.45)
    for i, ln in enumerate(lines):
        d.text((x, y), sanitize(ln), fill=color, font=font(size, bold=(bold_first and i == 0)))
        y += lh
    return y

def wrap(text, width=26, hard=32):
    buf = ""; out = []
    for ch in text:
        buf += ch
        if (len(buf) >= width and ch in " ·,.)") or len(buf) >= hard:
            out.append(buf.strip()); buf = ""
    if buf.strip(): out.append(buf.strip())
    return out
