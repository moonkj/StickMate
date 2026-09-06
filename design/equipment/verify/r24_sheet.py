# -*- coding: utf-8 -*-
"""R24 시트 — 모자 4종 H-2 수정 전/후 × 안경 6종.  python3 r24_sheet.py

행 구성
  1행 천모자 전 / 2행 천모자 후 / 3행 중절모 전 / 4행 중절모 후
  5행 베레모 현행(v1) / 6행 베레모 R21 재저작본(이식 대기)
  7행 밀짚모자 현행(v1) / 8행 밀짚모자 R21 재저작본(이식 대기)

「전」은 프로덕션 좌표에 R24 변경의 역변환을 건 것이다:
  · 천모자·중절모 관(B0/F1/H3): x ÷ CROWN_WIDEN
  · 중절모 챙: 분할선을 챙 장축(옛 자리)으로 되돌린 모양

★ 오프라인 래스터다. **최종 판정은 실제 빌드 캡처로만**(팀 규약 — Unity 미설치라 실기 캡처 0회).
★ 재질색은 형태 판정용 근사다(팔레트 정본 아님). 색 판정에 쓰지 마라.
"""
import os

from PIL import Image, ImageDraw

import r24_hats as M
import r21_model as M21
import r19_model as M19

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "r24-hats-ab.png"))

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


def undo_r24(kind_ko, pieces):
    """R24 변경의 역변환 — 관 x 배수를 되돌리고, 중절모 챙 분할선을 옛 자리(장축)로 되돌린다."""
    key = {"천모자": "clothhat", "중절모": "fedora"}[kind_ko]
    kx = M19.CROWN_WIDEN[key]
    out = []
    for p in pieces:
        src = p["src"]
        if src.endswith("_worn"):
            src = src[:-5]
        if src in ("B0", "F1", "H3"):
            out.append(dict(p, pts=[(x / kx, y) for x, y in p["pts"]]))
        elif src in ("B2near", "B2na", "B2far", "B2fa") and key == "fedora":
            continue                                   # 옛 분할본으로 갈아 끼운다(아래)
        else:
            out.append(dict(p))
    if key == "fedora":
        b2 = [q for q in M17_WORN_FEDORA if q.src == "B2"][0]
        ff, fa, nf, na = M19._split_lens(b2, y_cut=None)
        for pts, filled, loop, tone, sort in ((ff, True, True, 0, M.SORT_BACK), (nf, True, True, 0, M.SORT_HEAD)):
            out.append(dict(name="old", src="B2", pts=pts, filled=filled, loop=loop, tone=tone, sort=sort))
    return out


M17_WORN_FEDORA = None


def r21_pieces(kind):
    return [dict(name=p.name, src=p.src, pts=[tuple(q) for q in p.pts], filled=bool(p.filled),
                 loop=bool(p.loop), tone=(1 if p.src in ("F1",) else 0),
                 sort=(M.SORT_BACK if p.layer == "back" else M.SORT_HEAD)) for p in M21.BODY[kind]]


def main():
    global M17_WORN_FEDORA
    import r17_model as M17
    M17_WORN_FEDORA = [p for p in M17.WORN["fedora"] if not p.base]

    hats = {k: f() for k, f in M.HATS.items()}
    rows = [
        ("천모자 · 수정 전 (착용선 +0.680 실패)", undo_r24("천모자", hats["천모자"]), True),
        ("천모자 · 수정 후 (+0.284 통과)", hats["천모자"], False),
        ("중절모 · 수정 전 (착용선 +0.776 실패)", undo_r24("중절모", hats["중절모"]), True),
        ("중절모 · 수정 후 (+0.276 통과)", hats["중절모"], False),
        ("베레모 · 현행 v1 (덮임 실패)", hats["베레모"], True),
        ("베레모 · R21 재저작본 (+0.276, 이식 대기)", r21_pieces("beret"), False),
        ("밀짚모자 · 현행 v1 (머리 꼭대기 미덮임)", hats["밀짚모자"], True),
        ("밀짚모자 · R21 재저작본 (+0.280, 이식 대기)", r21_pieces("straw"), False),
    ]
    keys = list(M.EYES)
    sheet = Image.new("RGB", (CW * len(keys) + 20, CH * len(rows) + 60), (248, 248, 246))
    sd = ImageDraw.Draw(sheet)
    sd.text((10, 8), "R24 모자 4종 H-2 「앞층만」 — 안경 6종 동시착용. 정렬 EYES 8 < HEAD 10 이라 모자가 언제나 위다. "
                     "오프라인 래스터(최종 판정은 실제 빌드 캡처로만) · 재질색은 형태 판정용 근사.", fill=(20, 20, 20))
    sd.text((10, 26), "천모자·중절모: 몸 표면 관 x 배수(×1.12 / ×1.18) + 중절모 챙 분할선을 관 밑변으로. "
                      "u·dy 무변경 → 챙 폭·꼭대기·앞층 밑단 불변. 베레모·밀짚모자는 v1 코드라 이번 라운드 이식 밖.", fill=(120, 40, 40))
    for r, (title, pieces, warn) in enumerate(rows):
        for i, k in enumerate(keys):
            sheet.paste(cell(pieces, k, title + " · " + k, warn), (10 + i * CW, 44 + r * CH))
    sheet.save(OUT)
    print("wrote %s %dx%d" % (OUT, sheet.size[0], sheet.size[1]))


if __name__ == "__main__":
    main()
