# -*- coding: utf-8 -*-
"""R25 시트 — 모자 6종 H-2 목표 +0.45 재맞춤 전/후 × 안경 6종.  python3 r25_sheet.py

행 12개 = 모자 6종 × (전 / 후). 「전」은 프로덕션 현행(베레모·밀짚모자는 v1 현행).
「후」는 r25_hats 의 재맞춤 결과 그대로(같은 모듈을 import 해서 같은 좌표를 그린다 —
시트가 딴 좌표를 그리면 판정과 그림이 갈라진다).

★ 오프라인 래스터다. **최종 판정은 실제 빌드 캡처로만**(팀 규약 — Unity 미설치라 실기 캡처 0회).
★ 재질색은 형태 판정용 근사다(팔레트 정본 아님). 색 판정에 쓰지 마라.
"""
import os

from PIL import Image, ImageDraw

import r24_hats as M
import r25_hats as R25

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "r25-hats-ab.png"))

R_PX, CW, CH = 46, 232, 258
BG, INK = (34, 34, 38), (232, 226, 214)
TONE = {0: (198, 164, 88), 1: (120, 92, 40), 2: (43, 34, 10), 3: (255, 255, 255),
        4: (232, 226, 214), 5: (20, 20, 20), 6: (150, 190, 210)}


def cell(hat, eyes_key, title, warn=False):
    im = Image.new("RGB", (CW, CH), BG)
    d = ImageDraw.Draw(im)
    cx, cy = CW / 2.0, 142.0
    P = lambda p: (cx + p[0] * R_PX, cy - p[1] * R_PX)
    d.ellipse([cx - R_PX, cy - R_PX, cx + R_PX, cy + R_PX], outline=INK, width=max(2, int(0.1719 * R_PX)))
    d.line([P((0, -1.32)), P((0, -2.6))], fill=INK, width=max(2, int(0.26 * R_PX)))
    for pieces in (M.EYES[eyes_key](), hat):            # EYES(8) 먼저, HEAD(10) 나중 = 모자가 위
        for p in sorted(pieces, key=lambda q: q["sort"]):
            pts = [P(q) for q in p["pts"]]
            c = TONE.get(p["tone"], INK)
            if p["filled"]:
                d.polygon(pts, fill=c, outline=INK)
            else:
                d.line(pts + ([pts[0]] if p["loop"] else []), fill=c, width=3)
    d.text((6, 4), title, fill=(200, 120, 120) if warn else (205, 205, 205))
    return im


def main():
    hats, hats21, eyes = R25.load()
    ei = {k: M.ink(v) for k, v in eyes.items()}
    plan = {}
    KXR = {"천모자": [1.0], "중절모": [1.0], "왕관": [1.0], "털모자": [1.0]}
    for k in ("천모자", "중절모", "왕관", "털모자"):
        plan[k] = R25.search(hats[k], kx_ratios=KXR[k])
    plan["밀짚모자"] = R25.search(hats21["밀짚모자"])
    plan["베레모"] = R25.search(R25.beret_variant("sag", 5.0, 0.070), s_list=[1.0])

    rows = []
    for k in ("천모자", "중절모", "털모자", "왕관", "베레모", "밀짚모자"):
        o0 = R25.occl(hats[k], ei)
        o1 = R25.occl(plan[k]["pieces"], ei)
        rows.append(("%s · 전 (가려짐 %.1f~%.1f%%)" % (k, min(o0.values()), max(o0.values())), hats[k], True))
        rows.append(("%s · 후 (착용선 %+.3f · 밑단 %+.3f · %.1f~%.1f%%)"
                     % (k, plan[k]["wear"], plan[k]["bottom"], min(o1.values()), max(o1.values())),
                     plan[k]["pieces"], False))

    keys = list(eyes)
    sheet = Image.new("RGB", (CW * len(keys) + 20, CH * len(rows) + 60), (248, 248, 246))
    sd = ImageDraw.Draw(sheet)
    sd.text((10, 8), "R25 모자 6종 — H-2 목표 +0.30 → +0.45 전면 상향 + 앞층 밑단 목표 +0.315(왕관 실측). "
                     "정렬 EYES 8 < HEAD 10 이라 모자가 언제나 위다. 오프라인 래스터(최종 판정은 실제 빌드 캡처로만).", fill=(20, 20, 20))
    sd.text((10, 26), "천모자·중절모·왕관 = HAT_FIT(dy·ky) · 털모자 = AccessoryWornTransform · "
                      "베레모 = R21 안의 밑변을 수평으로(처짐은 밴드 밖으로) · 밀짚모자 = R21 안 그대로 + dy. 카드 좌표 0변경.",
            fill=(120, 40, 40))
    for r, (title, pieces, warn) in enumerate(rows):
        for i, k in enumerate(keys):
            sheet.paste(cell(pieces, k, title + " · " + k, warn), (10 + i * CW, 44 + r * CH))
    sheet.save(OUT)
    print("wrote %s %dx%d" % (OUT, sheet.size[0], sheet.size[1]))


if __name__ == "__main__":
    main()
