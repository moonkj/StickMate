# -*- coding: utf-8 -*-
"""R27 — 천모자 착용 챙 띠 A/B 시트.  python3 r27_sheet.py [옛_Handoff.cs]

design/equipment/r27-capband-ab.png 을 굽는다. 왼쪽 = 옛 판(관 닫힘변 획 있음) · 오른쪽 = 현행.
★ 이것은 **오프라인 렌더러**다. 최종 판정은 실제 빌드 캡처로만 한다(CLAUDE.md 디자인 7인 공통 규칙).
   여기서 보이는 것은 «두 좌표 집합이 같은 화가 규약 아래 어떻게 다른가»뿐이다.
"""
import math
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r27_capband as CB                                    # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "design", "equipment", "r27-capband-ab.png")

INK = (0x11, 0x11, 0x11)
M = (0x96, 0x81, 0x4F)          # palette_model BY_KIND["clothhat"] material
M2 = (0xCC, 0x55, 0x12)         # secondaryColor #CC5512 (equip_head_cap.asset)
SH = tuple(int(round(c * 0.28)) for c in M)                 # palette_model.shaded(M)
WHITE = (0xFF, 0xFF, 0xFF)
BG = (0xF2, 0xF2, 0xF2)
TONE_RGB = {0: M, 1: M2, 2: SH, 3: WHITE, 4: INK}

SS = 8                          # 물리픽셀당 초과표본(다운스케일로 AA 를 만든다)
X0, X1, Y0, Y1 = -2.35, 2.35, -1.30, 2.60


def render(cases, kind, scale, px_per_pt, label):
    Rpt = CB.head_r_pt(scale)
    ppr = Rpt * px_per_pt                                   # 물리픽셀 / R
    w = int(round((X1 - X0) * ppr)); h = int(round((Y1 - Y0) * ppr))
    img = Image.new("RGB", (w * SS, h * SS), BG)
    d = ImageDraw.Draw(img, "RGBA")

    def P(x, y):
        return ((x - X0) * ppr * SS, (Y1 - y) * ppr * SS)

    elems, _ = CB.worn_elems(kind, scale, cases=cases)
    for e in sorted(elems, key=lambda q: (q.z, q.sub)):
        if e.kind == "disc":
            cx, cy = e.pts[0]
            r = e.r * ppr * SS
            x, y = P(cx, cy)
            d.ellipse([x - r, y - r, x + r, y + r], fill=INK)
            continue
        if e.kind == "fill":
            d.polygon([P(x, y) for x, y in e.pts], fill=TONE_RGB[e.tag[2]])
            continue
        col = TONE_RGB[e.tag[2]]
        alpha = 107 if e.tag[2] == 3 else 255                # 하이라이트 α 0.42
        lw = max(1, int(round(2 * e.r * ppr * SS)))
        pts = [P(x, y) for x, y in e.pts] + ([P(*e.pts[0])] if e.loop else [])
        d.line(pts, fill=col + (alpha,), width=lw, joint="curve")
        for q in (pts[0], pts[-1]):                          # 둥근 캡
            d.ellipse([q[0] - lw / 2, q[1] - lw / 2, q[0] + lw / 2, q[1] + lw / 2], fill=col + (alpha,))

    img = img.resize((w, h), Image.LANCZOS)
    return img, label


def main():
    old_path = sys.argv[1] if len(sys.argv) > 1 else None
    old = CB.parse(open(old_path, encoding="utf-8").read()) if old_path else None
    rows = []
    for scale, ppt, tag in ((0.75, 2, "배율 0.75 · macOS Retina 2px/pt"),
                            (0.75, 1, "배율 0.75 · Windows 100% 1px/pt"),
                            (1.00, 2, "배율 1.00 · macOS Retina 2px/pt")):
        pair = []
        for cases, who in ((old, "옛 판"), (None, "현행 R27")):
            if cases is None and old is None and who == "옛 판":
                continue
            img, _ = render(cases, "clothhat", scale, ppt, who)
            pair.append((img, "%s — %s" % (tag, who)))
        rows.append(pair)

    zoom = 4
    pad, gap, lab = 14, 26, 16
    cw = max(im.width for r in rows for im, _ in r) * zoom
    ch = max(im.height for r in rows for im, _ in r) * zoom
    cols = max(len(r) for r in rows)
    W = pad * 2 + cols * cw + (cols - 1) * gap
    H = pad * 2 + len(rows) * (ch + lab + gap)
    sheet = Image.new("RGB", (W, H), (0xFF, 0xFF, 0xFF))
    dr = ImageDraw.Draw(sheet)
    y = pad
    for r in rows:
        for i, (im, cap) in enumerate(r):
            x = pad + i * (cw + gap)
            sheet.paste(im.resize((im.width * zoom, im.height * zoom), Image.NEAREST), (x, y))
            dr.text((x, y + ch + 3), cap + "  (물리픽셀 ×%d 확대)" % zoom, fill=(0x22, 0x22, 0x22))
        y += ch + lab + gap
    sheet.save(OUT)
    print("wrote", OUT, sheet.size)


if __name__ == "__main__":
    main()
