# -*- coding: utf-8 -*-
"""R23 시트 — 왕관 착용 위치 수정 전/후 × 안경 6종.  python3 r23_crown_sheet.py

수정 후 = 지금 프로덕션 좌표. 수정 전 = 그 좌표에 R19 재맞춤의 역변환을 건 것
(x' = x/k · y' = (y−d)/k, k = 0.0570/0.0630, d = 3.1200 − k·2.6000).
★ 오프라인 래스터다. 최종 판정은 실제 빌드 캡처로만(팀 규약).
"""
import os

from PIL import Image, ImageDraw

import r23_crown as M

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "r23-crown-ab.png"))

K = 0.0570 / 0.0630
D = 3.1200 - K * 2.6000

R_PX, CW, CH = 52, 260, 290
BG, INK = (34, 34, 38), (232, 226, 214)
# 재질 톤 → 대략색(팔레트 정본이 아니라 시트용 근사 — 형태 판정용이다)
TONE = {0: (198, 164, 88), 1: (120, 92, 40), 2: (43, 34, 10), 3: (255, 255, 255),
        4: (232, 226, 214), 5: (20, 20, 20), 6: (150, 190, 210)}


def cell(crown, eyes_key, title):
    im = Image.new("RGB", (CW, CH), BG)
    d = ImageDraw.Draw(im)
    cx, cy = CW / 2.0, 150.0
    P = lambda p: (cx + p[0] * R_PX, cy - p[1] * R_PX)
    d.ellipse([cx - R_PX, cy - R_PX, cx + R_PX, cy + R_PX], outline=INK, width=max(2, int(0.1719 * R_PX)))
    d.line([P((0, -1.32)), P((0, -2.7))], fill=INK, width=max(2, int(0.26 * R_PX)))
    for pieces in (M.EYES[eyes_key](), crown):          # EYES(8) 먼저, HEAD(10) 나중 = 모자가 위
        for p in sorted(pieces, key=lambda q: q["sort"]):
            pts = [P(q) for q in p["pts"]]
            c = TONE.get(p["tone"], INK)
            if p["filled"]:
                d.polygon(pts, fill=c, outline=INK)
            else:
                d.line(pts + ([pts[0]] if p["loop"] else []), fill=c, width=3)
    d.text((6, 4), title, fill=(205, 205, 205))
    return im


def main():
    now = M.HATS["왕관"]()
    before = [dict(p, pts=[(x / K, (y - D) / K) for x, y in p["pts"]]) for p in now]
    keys = list(M.EYES)
    sheet = Image.new("RGB", (CW * len(keys) + 20, CH * 2 + 76), (248, 248, 246))
    sd = ImageDraw.Draw(sheet)
    sd.text((10, 8), "R23 왕관 부착 위치 — 위: 수정 전(앞층 밑단 -0.5028 R) / 아래: 수정 후(+0.3127 R). "
                     "안경 6종 동시착용. 정렬 EYES 8 < HEAD 10 이라 모자가 언제나 위다.", fill=(20, 20, 20))
    sd.text((10, 26), "수정 전 가려짐 99.8 / 99.9 / 98.7 / 80.1 / 91.0 / 84.1 %  ->  "
                      "수정 후 12.1 / 16.0 / 12.8 / 20.3 / 30.2 / 22.4 %  (r23_crown.out.txt ②)", fill=(120, 40, 40))
    for i, k in enumerate(keys):
        sheet.paste(cell(before, k, "수정 전 · " + k), (10 + i * CW, 46))
        sheet.paste(cell(now, k, "수정 후 · " + k), (10 + i * CW, 46 + CH + 14))
    sheet.save(OUT)
    print("wrote %s %dx%d" % (OUT, sheet.size[0], sheet.size[1]))


if __name__ == "__main__":
    main()
