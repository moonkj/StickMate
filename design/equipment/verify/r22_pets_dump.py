# -*- coding: utf-8 -*-
"""R22 펫 6종 — (1) 좌표 전문 텍스트, (2) 40 viewBox 카드 파츠, (3) 실측 크기 시트 PNG.

★ 좌표를 손으로 옮겨 적지 않는다. 문서의 표는 이 출력을 붙여 넣은 것이다.
"""
import math
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r22_pets as P
from rig import W, bounds
from PIL import Image, ImageDraw

OUT_COORDS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "r22_pets_coords.txt")
OUT_SHEET = os.path.join(os.path.dirname(os.path.abspath(__file__)), "r22_pets_sheet.png")

ORDER = ["미니위성봇", "종이비행기스피릿", "스틱캣", "꼬마유령", "미니도트드래곤", "블랙홀코어"]
GRADE = {"미니위성봇": "일반 Lv.1", "종이비행기스피릿": "일반 Lv.13", "스틱캣": "희귀 Lv.19",
         "꼬마유령": "희귀 Lv.24", "미니도트드래곤": "영웅 Lv.27", "블랙홀코어": "전설 Lv.30"}


def stamp(d, x, y, dia, col):
    r = dia * 0.5
    d.ellipse([x - r, y - r, x + r, y + r], fill=col)


def draw_shape(d, s, ox, oy, scale, stroke_px, col):
    """★ 구현 그대로 그린다 — 점(DOTS)은 속을 채우고, 폭곡선(WIDTHS)이 있으면 그 폭으로 찍는다.
    1차 시트가 이 둘을 무시하고 균일 윤곽선으로만 그려서 **드래곤이 산맥으로, 위성이 나비넥타이로**
    보였다. 판정은 구현과 같은 그림에서만 유효하다."""
    if s.name in P.DOTS:
        cx, cy, dia = P.DOTS[s.name]
        stamp(d, ox + cx * scale, oy - cy * scale, dia * scale, col)
        return
    pts = [(ox + x * scale, oy - y * scale) for x, y in s.pts]
    if s.loop:
        pts = pts + [pts[0]]

    if s.name == "PlaneTrail":
        segs = [(0, 1, 1.63), (1, 2, 0.03), (2, 3, 1.16)]
        for i, j, m in segs:
            stroke_run(d, pts[i], pts[j], stroke_px * m, stroke_px * m, col)
        return
    if s.name == "GhostEyes":
        for p in (pts[0], pts[1]):
            stamp(d, p[0], p[1], stroke_px * 1.51, col)
        return
    if s.name in P.WIDTHS and s.name in ("DragonBody", "DragonBreath"):
        w = P.WIDTHS[s.name]
        for i in range(len(pts) - 1):
            stroke_run(d, pts[i], pts[i + 1], stroke_px * w[i], stroke_px * w[i + 1], col)
        return
    d.line(pts, fill=col, width=max(1, int(round(stroke_px))), joint="curve")


def stroke_run(d, a, b, w0, w1, col):
    n = max(2, int(math.dist(a, b)))
    for k in range(n + 1):
        t = k / n
        stamp(d, a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, w0 + (w1 - w0) * t, col)


def fmt(pts):
    return "  ".join("(%+.4f,%+.4f)" % (x, y) for x, y in pts)


def card_parts(shapes, box=40.0, margin=2.8):
    """월드 R 좌표 -> 40 viewBox(원점 좌상, y 아래). BuildIcon 여백 규약과 같다."""
    pts = [p for s in shapes for p in s.pts]
    x0, y0, x1, y1 = bounds(pts)
    span = max(x1 - x0, y1 - y0)
    k = (box - 2 * margin) / span
    cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
    out = []
    for s in shapes:
        out.append((s.name, s.loop, s.tone,
                    [(box * 0.5 + (x - cx) * k, box * 0.5 - (y - cy) * k) for x, y in s.pts]))
    return out, k


def main():
    lines = []
    A = lines.append
    A("R22 펫 6종 좌표 전문 — 자동 생성 (design/equipment/verify/r22_pets_dump.py)")
    A("단위 = 머리 반경 R · 원점 = 각 펫의 로컬 원점 · +x = 진행 방향 · +y = 위")
    A("W = %.6f R (배율 0.75 = %.2f pt) · 1.5W = %.6f R · 3.0W = %.6f R"
      % (W, W * P.R_PT, 1.5 * W, 3.0 * W))
    A("")
    for name in ORDER:
        sh = P.PET_NEW[name]
        x0, y0, x1, y1 = P.item_bounds(sh)
        A("### %s (%s)" % (name, GRADE[name]))
        A("    크기 %.4f x %.4f R = %.2f x %.2f pt" % (x1 - x0, y1 - y0,
                                                      (x1 - x0) * P.R_PT, (y1 - y0) * P.R_PT))
        for s in sh:
            tone = "보조색" if s.tone == 1 else "주색  "
            A("    %-14s %s %s %s" % (s.name, "닫힘" if s.loop else "열림", tone, fmt(s.pts)))
        A("")
    A("")
    A("=" * 78)
    A("카드 아이콘 파츠 (40 viewBox · 원점 좌상 · y 아래 · 여백 2.8 · 획 1.70)")
    A("=" * 78)
    for name in ORDER:
        cps, k = card_parts(P.PET_NEW[name])
        A("### %s   (R -> viewBox 배율 %.4f)" % (name, k))
        for nm, loop, tone, pts in cps:
            A("    %-14s kind=%s tone=%d  %s"
              % (nm, "Polyline(닫힘)" if loop else "Polyline(열림)", tone,
                 "  ".join("%.2f,%.2f" % (x, y) for x, y in pts)))
        A("")
    open(OUT_COORDS, "w", encoding="utf-8").write("\n".join(lines))
    print("wrote", OUT_COORDS)

    # ── 시트: 왼쪽 = 실제 착용 크기(pt를 4배 확대) · 오른쪽 = 카드 44px 실크기 + 4배
    CELL_W, CELL_H = 560, 300
    sheet = Image.new("RGB", (CELL_W * 3, CELL_H * 2 + 10), (22, 24, 28))
    d = ImageDraw.Draw(sheet)
    ZOOM = 4.0                     # 1 pt -> 4 px
    for idx, name in enumerate(ORDER):
        cxx = (idx % 3) * CELL_W
        cyy = (idx // 3) * CELL_H
        d.rectangle([cxx + 2, cyy + 2, cxx + CELL_W - 2, cyy + CELL_H - 2], outline=(58, 62, 70))
        d.text((cxx + 10, cyy + 8), "%s  %s" % (name, GRADE[name]), fill=(200, 206, 214))
        sh = P.PET_NEW[name]
        x0, y0, x1, y1 = P.item_bounds(sh)
        ox = cxx + CELL_W * 0.22 - (x0 + x1) * 0.5 * P.R_PT * ZOOM
        oy = cyy + CELL_H * 0.58 + (y0 + y1) * 0.5 * P.R_PT * ZOOM
        sw = W * P.R_PT * ZOOM
        for s in sh:
            col = (255, 255, 255) if s.tone == 0 else (150, 190, 255)
            draw_shape(d, s, ox, oy, P.R_PT * ZOOM, sw, col)
        # 주인 머리 지름 기준자
        d.ellipse([cxx + 16, cyy + CELL_H - 70,
                   cxx + 16 + P.HEAD_DIA_PT * ZOOM,
                   cyy + CELL_H - 70 + P.HEAD_DIA_PT * ZOOM], outline=(120, 126, 136))
        d.text((cxx + 16, cyy + CELL_H - 22), "주인 머리 11.63pt", fill=(120, 126, 136))
        # 카드 44px 실크기
        # 카드 44px — 실크기 한 벌 + 4배 확대 한 벌(같은 좌표, 같은 획 비율)
        for (bx, by, mag) in ((cxx + CELL_W - 62, cyy + 26, 1.0),
                              (cxx + CELL_W - 62 - 4 * 44 - 8, cyy + 26, 4.0)):
            d.rectangle([bx, by, bx + 44 * mag, by + 44 * mag], outline=(70, 76, 86))
            _, k = P.card_map(sh, box=44.0)
            x0c, y0c, x1c, y1c = P.item_bounds(sh)
            ccx, ccy = (x0c + x1c) * 0.5, (y0c + y1c) * 0.5
            sc = k * mag
            for s in sh:
                col = (255, 255, 255) if s.tone == 0 else (150, 190, 255)
                draw_shape(d, s, bx + 22 * mag - ccx * sc, by + 22 * mag + ccy * sc,
                           sc, P.CARD_W_PX * mag, col)
    sheet.save(OUT_SHEET)
    print("wrote", OUT_SHEET)


if __name__ == "__main__":
    main()
