# -*- coding: utf-8 -*-
"""R4 §2 — «동시 착용 시 뭉치는가»를 눈이 아니라 숫자로.

R3 가 놓친 지점(리더 지적): 대마법사 4종을 한꺼번에 입으면 초록 덩어리가 된다.
여기서는 4종을 **같은 좌표계(머리 중심 원점 · R 배수)** 에 올려 놓고
  (a) 실제 착용 장치 해상도로 래스터해 **연결 성분(덩어리) 개수**를 센다,
  (b) 가장 큰 덩어리가 전체 잉크의 몇 %인가를 잰다,
  (c) 머리 원반(r=1R)과 눈 자리가 얼마나 덮이는가를 잰다.
장치 해상도: 배율 0.75 · R = 5.8163 pt · 2x 레티나 ⇒ 11.633 px/R. 이 값 아래로는 화면에 없다.
"""
import os, sys, math, json
import numpy as np
from PIL import Image, ImageDraw
import r4_geom as G

PX_PER_R_DEVICE = 2.0 * G.R_PT_075 / 1.0        # 11.633 px/R  (배율0.75, 2x)
SS = 6                                          # 슈퍼샘플

def render_item(pieces, px_per_r, ox, oy, w, h, img=None, fill_only=False):
    if img is None:
        img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    for p in sorted(pieces, key=lambda q: -q["layer"]):
        pts = [(ox + x * px_per_r, oy - y * px_per_r) for x, y in p["pts"]]
        if len(pts) < 2: continue
        if p["filled"] and len(pts) >= 3:
            d.polygon(pts, fill=255)
        if not fill_only and not p["noStroke"] and p.get("lineAlpha", 1.0) > 0.3:
            wpx = max(1.0, G.W * p["strokeMult"] * px_per_r)
            seq = pts + [pts[0]] if p["loop"] else pts
            d.line(seq, fill=255, width=int(round(wpx)), joint="curve")
    return img

def components(mask):
    """4-이웃 연결 성분 — 라벨링 직접(scipy 없음)."""
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32); cur = 0; sizes = []
    for j in range(h):
        for i in range(w):
            if mask[j, i] and lab[j, i] == 0:
                cur += 1; stack = [(j, i)]; lab[j, i] = cur; n = 0
                while stack:
                    a, b = stack.pop(); n += 1
                    for da, db in ((1,0),(-1,0),(0,1),(0,-1)):
                        y, x = a+da, b+db
                        if 0 <= y < h and 0 <= x < w and mask[y, x] and lab[y, x] == 0:
                            lab[y, x] = cur; stack.append((y, x))
                sizes.append(n)
    return sizes

def analyse(bank, keys, label, span=(-5.0, 5.0, -6.5, 4.2)):
    x0, x1, y0, y1 = span
    ppr = PX_PER_R_DEVICE
    w = int(round((x1 - x0) * ppr)); h = int(round((y1 - y0) * ppr))
    ox, oy = -x0 * ppr, y1 * ppr
    per = {}
    union = Image.new("L", (w, h), 0)
    for k in keys:
        im = render_item(G.body_pieces(bank[k]), ppr, ox, oy, w, h)
        per[k] = np.array(im) > 127
        union = Image.composite(Image.new("L", (w, h), 255), union, im.point(lambda v: 255 if v > 127 else 0))
    U = np.array(union) > 127
    sizes = sorted(components(U), reverse=True)
    ink = U.sum()
    # 머리 원반 · 눈 자리
    yy, xx = np.mgrid[0:h, 0:w]
    X = (xx - ox) / ppr; Y = (oy - yy) / ppr
    head = (X**2 + Y**2) <= 1.0
    eye = ((X - G.RIG.EYE_X)**2 + (Y - G.RIG.EYE_Y)**2) <= (G.RIG.PUPIL_R * 1.6)**2
    eye2 = ((X + G.RIG.EYE_X)**2 + (Y - G.RIG.EYE_Y)**2) <= (G.RIG.PUPIL_R * 1.6)**2
    print(f"── {label}")
    print(f"   잉크 화소 {ink}  ·  덩어리(4-이웃 연결성분) {len(sizes)}개  ·  최대 덩어리 점유 "
          f"{(sizes[0]/ink*100 if ink else 0):.1f}%  ·  덩어리 크기 상위 {sizes[:6]}")
    print(f"   머리 원반 가림 {(U & head).sum()/head.sum()*100:.1f}%   ·  눈(앞) 가림 {(U & eye).sum()/max(1,eye.sum())*100:.0f}%"
          f"  ·  눈(뒤) 가림 {(U & eye2).sum()/max(1,eye2.sum())*100:.0f}%")
    ks = list(keys)
    for a in range(len(ks)):
        for b in range(a + 1, len(ks)):
            inter = (per[ks[a]] & per[ks[b]]).sum()
            if inter:
                base = min(per[ks[a]].sum(), per[ks[b]].sum())
                print(f"   겹침 {ks[a].split('_')[-2:]}×{ks[b].split('_')[-2:]}: {inter}px = 작은쪽의 {inter/base*100:.0f}%")
    return dict(ink=int(ink), blobs=len(sizes), top=float(sizes[0]/ink) if ink else 0)

if __name__ == "__main__":
    print(f"# 장치 해상도 {PX_PER_R_DEVICE:.3f} px/R (배율 0.75 · 2x). 이 아래 해상도는 화면에 없다.\n")
    p3 = G.load_pack("pack_detail_r3")
    print("★ 팩 R3 — 팩별 4종 동시 착용")
    for pk in ("arcane", "cyber", "mine"):
        keys = [k for k in p3 if f"_{pk}_" in k]
        keys.sort(key=lambda k: ["back", "neck", "eyes", "head"].index(k.split("_")[2]))
        analyse(p3, keys, f"pack.{pk} 4종")
    print()
    print("◇ 대조군 — 출하 4종 동시 착용(중절모·선글라스·나비넥타이·짧은망토)")
    h = G.load_handoff(); n = G.load_neck6()
    bank = dict(h); bank.update({("neck_" + k): v for k, v in n.items()})
    analyse(bank, ["shortcape", "neck_bowtie", "sunglasses", "fedora"], "출하 4종")
    print()
    print("◇ 대조군 — 출하 단품(중절모 하나)")
    analyse(bank, ["fedora"], "중절모 단품")
    print("◇ 팩 단품(대마법사 모자 하나)")
    analyse(p3, ["pack_arcane_head_wizard_hat"], "위저드햇 단품")
