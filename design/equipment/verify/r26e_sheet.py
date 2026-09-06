# -*- coding: utf-8 -*-
"""R26e — 뒷짐 × 망토 겹침 시트(눈으로 대조하기 위한 보조 그림).

★ 이 그림은 **판정 근거가 아니다.** 이 저장소의 규칙대로 최종 판정은 실제 빌드 캡처로만 한다
   (오프라인 렌더러가 코너 붕괴를 가린 사고가 있었다). 숫자는 r26/r26b/r26c 의 .out.txt 가 정본이고,
   이 시트는 «그 숫자가 어떤 그림인지»를 사람이 확인하기 위한 것이다.

그리기 순서는 프로덕션 정렬 번호 그대로:
   망토 뒤판 채움(−2) → 뒤판 선(−1) → 다리(0) → 몸통(1) → 팔(2) → 칼라·걸쇠(3) → 머리(3/4)
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r26_capestance as B
from PIL import Image, ImageDraw

SCALE_PX = 46.0            # 1 R = 46 px (실물 크기가 아니라 «구조 확인용» 확대)
W, H = 300, 700
PAD_Y = 60

CAPE_FILL = (198, 74, 74)
CAPE_LINE = (140, 44, 44)
BODY      = (24, 24, 24)
ARM_HI    = (0, 132, 255)

def to_px(p, ox, oy):
    return (ox + p[0]*SCALE_PX, oy - p[1]*SCALE_PX)

def draw_cell(img, ox, oy, cape, arms, w, title, sub):
    d = ImageDraw.Draw(img)
    back, collar, clasp = cape["back"], cape["collar"], cape["clasp"]
    d.polygon([to_px(p, ox, oy) for p in back], fill=CAPE_FILL, outline=CAPE_LINE)
    # 다리(중립) — sortingOrder 0
    hip = (0.0, B.HIP_R)
    for sign in (+1.0, -1.0):
        pts = B.limb_polyline(2.2727, 2.0455, -4.0, 0.57)
        a = math.radians(sign*12.0); c, s = math.cos(a), math.sin(a)
        L = [(hip[0]+p[0]*c-p[1]*s, hip[1]+p[0]*s+p[1]*c) for p in pts]
        d.line([to_px(p, ox, oy) for p in L], fill=BODY, width=int(0.570*SCALE_PX), joint="curve")
    # 몸통(1)
    d.line([to_px((0, B.SHOULDER_R+0.10), ox, oy), to_px((0, B.HIP_R), ox, oy)],
           fill=BODY, width=int(0.523*SCALE_PX))
    # 팔(2)
    for i, (u, l) in enumerate(arms):
        L = B.arm_points(u, l, w)
        d.line([to_px(p, ox, oy) for p in L], fill=BODY if i == 0 else ARM_HI,
               width=int(w*SCALE_PX), joint="curve")
    # 칼라·걸쇠(3)
    d.polygon([to_px(p, ox, oy) for p in collar], fill=CAPE_FILL, outline=CAPE_LINE)
    d.polygon([to_px(p, ox, oy) for p in clasp], fill=CAPE_LINE)
    # 머리(3/4)
    r = 1.0*SCALE_PX
    d.ellipse([ox-r, oy-r, ox+r, oy+r], fill=(255, 255, 255), outline=BODY, width=max(2, int(0.35*SCALE_PX)))
    d.text((ox-120, 8), title, fill=(0, 0, 0))
    d.text((ox-120, 22), sub, fill=(90, 90, 90))

def main():
    cells = []
    for nm, cape in B.CAPES.items():
        cells.append((nm, cape, "P2 뒷짐", B.watch_stance_arms(True, 0.0)))
        cells.append((nm, cape, "P1 팔짱", B.watch_stance_arms(False, 0.0)))
        cells.append((nm, cape, "대조 Idle", B.idle_arms()))
        cells.append((nm, cape, "대조 보행(어깨 0°)", [(0.0, 20.0), (0.0, 20.0)]))
    img = Image.new("RGB", (W*len(cells), H), (250, 250, 250))
    w = B.arm_width_R(0.75)
    for i, (nm, cape, label, arms) in enumerate(cells):
        draw_cell(img, W*i + W//2, PAD_Y + 1.0*SCALE_PX, cape, arms, w,
                  "%s / %s" % (nm, label), "1R=%dpx (구조 확인용 확대)" % int(SCALE_PX))
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "r26_capestance_sheet.png")
    img.save(out)
    print("저장:", out, img.size)

if __name__ == "__main__":
    main()
