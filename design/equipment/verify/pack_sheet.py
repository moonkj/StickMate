# -*- coding: utf-8 -*-
"""팩 「야간 정비반」 시안 굽기 — ★ **판정 아님.**
   오프라인 래스터는 모든 점에 둥근 캡을 찍어 **코너 붕괴를 가린다**(이 저장소의 실제 사고).
   최종 판정은 실기 빌드 캡처로만. 여기 그림은 후보를 좁히는 도구다.

   획은 프로덕션과 같은 **두 역할**을 쓴다:
     선  MinStrokeScreenPoints      = 2.00pt  (wornread.w_line)
     면 외곽선 MinFillOutlineScreenPoints = 1.00pt  (wornread.w_fill)
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import rig, items, hair, wornread as WR
import pack_nightshift as P

PT_PER_UNIT = 846.0 / 24.0
DEV = 2.0
PRIM, ACC, SHD = (196, 152, 96), (232, 196, 108), (110, 86, 56)
BG, INK = (233, 234, 230), (24, 24, 24)


def draw(d, shapes, P_, wl, wf):
    for s in shapes:
        col = ACC if s.tone == 1 else (SHD if s.tone == 2 else PRIM)
        pts = [P_(p) for p in s.pts]
        if s.filled:
            d.polygon(pts, fill=col)
            d.line(pts + [pts[0]], fill=tuple(int(c*0.55) for c in col),
                   width=max(1, int(round(wf))), joint="curve")
        else:
            d.line(pts, fill=col, width=max(1, int(round(wl))), joint="curve")


def worn(shapes, scale, zoom=1, body=True):
    S = 0.22 * scale * PT_PER_UNIT * DEV * zoom
    wl, wf = WR.w_line(scale)*S, WR.w_fill(scale)*S
    Wp, Hp = int(2*(S*3.4)+16), int(S*10.6+16)
    im = Image.new("RGB", (Wp, Hp), BG); d = ImageDraw.Draw(im)
    cx, cy = Wp/2, S*2.1+8
    Pf = lambda p: (cx + p[0]*S, cy - p[1]*S)
    if body:
        d.ellipse([cx-S, cy-S, cx+S, cy+S], fill=INK)
        d.line([Pf((0,-1.0)), Pf((0,-5.09))], fill=INK, width=max(1,int(round(wl))))
        d.line([Pf((0,-1.32)), Pf((0.55,-2.9))], fill=INK, width=max(1,int(round(wl))))
        d.line([Pf((0,-1.32)), Pf((-0.30,-3.0))], fill=INK, width=max(1,int(round(wl))))
        d.line([Pf((0,-5.09)), Pf((0.42,-7.6))], fill=INK, width=max(1,int(round(wl))))
        d.line([Pf((0,-5.09)), Pf((-0.42,-7.6))], fill=INK, width=max(1,int(round(wl))))
    # 레이어 순서대로: BACK(-1) → HAIR(6) → NECK(7) → EYES(8) → HEAD(10)
    for sh in shapes: draw(d, sh, Pf, wl, wf)
    return im


def card(shapes, zoom=1):
    ICON, FIT, IST = 44.0, 0.86, 1.7*44/40
    pts = [q for s in shapes for q in s.pts]
    x0, y0, x1, y1 = rig.bounds(pts)
    span = max(x1-x0, y1-y0); k = ICON*FIT/span*zoom
    Wp = int(ICON*zoom)
    im = Image.new("RGB", (Wp, Wp), (247,247,245)); d = ImageDraw.Draw(im)
    cx0, cy0 = (x0+x1)/2, (y0+y1)/2
    Pf = lambda p: (Wp/2 + (p[0]-cx0)*k, Wp/2 - (p[1]-cy0)*k)
    draw(d, shapes, Pf, IST*zoom, IST*zoom)
    return im


def hstack(ims, gap=10, bg=(18,18,18)):
    w = sum(i.width for i in ims) + gap*(len(ims)+1)
    h = max(i.height for i in ims) + gap*2
    im = Image.new("RGB", (w,h), bg); x = gap
    for i in ims: im.paste(i, (x, gap + (h-2*gap-i.height)//2)); x += i.width+gap
    return im

def vstack(ims, gap=10, bg=(18,18,18)):
    w = max(i.width for i in ims) + gap*2
    h = sum(i.height for i in ims) + gap*(len(ims)+1)
    im = Image.new("RGB", (w,h), bg); y = gap
    for i in ims: im.paste(i, (gap, y)); y += i.height+gap
    return im


root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
ORDER = [P.back_toolbag(), P.hair_napetie(), P.neck_apronbib(), P.eyes_respirator(), P.head_havelock()]
BASEO = [items.BACK["요정날개"], hair.SET["단정한머리"], items.NECK["나비넥타이"],
         items.EYES["동그란안경"], items.fedora()]

# ① 카드 6장 — 실제 44px 과 6배
cards = [P.head_havelock(), P.eyes_respirator(), P.neck_apronbib(),
         P.back_toolbag(), P.hair_napetie(), P.pet_worklamp()]
row1 = hstack([card(c, 1) for c in cards])
row2 = hstack([card(c, 6) for c in cards])
Image.fromarray(__import__("numpy").array(vstack([row1, row2]))).save(
    os.path.join(root, "pack-nightshift-cards.png")) if False else vstack([row1,row2]).save(
    os.path.join(root, "pack-nightshift-cards.png"))
print("  -> pack-nightshift-cards.png")

# ② 6/6 착용 — 배율 0.75 / 0.60, 실제 픽셀 + 5배, 기본 한 벌과 나란히
for sc in (0.75, 0.60):
    a1, a5 = worn(ORDER, sc, 1), worn(ORDER, sc, 4)
    b1, b5 = worn(BASEO, sc, 1), worn(BASEO, sc, 4)
    vstack([hstack([a1, a5]), hstack([b1, b5])]).save(
        os.path.join(root, "pack-nightshift-worn-%.2f.png" % sc))
    print("  -> pack-nightshift-worn-%.2f.png  (위=팩 6/6, 아래=기본 한 벌)" % sc)

# ③ 한 개씩 껴 나가는 순서 (한계 가림률의 그림판)
seq = [worn(ORDER[:k+1], 0.75, 3) for k in range(5)]
hstack(seq).save(os.path.join(root, "pack-nightshift-buildup.png"))
print("  -> pack-nightshift-buildup.png  (1→5개, 배율 0.75 x4)")
print("\n★ 이 그림들은 시안이다. **최종 판정은 실기 빌드 캡처로만** (CLAUDE.md).")
