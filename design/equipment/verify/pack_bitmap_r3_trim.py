#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 — 비트맵 «트림 띠 두께»를 R 단위로 환산한다.
   두께 = 가로 주사선의 연속 트림 런 길이 중앙값(양끝이 비트림인 런만).
   환산 = (런 / 비트맵 아이템 폭 px) x (R3 벡터의 같은 아이템 폭 R).
   비교 대상 = 착용 0.75 의 액세서리 획 실폭 1.00pt (MinAccessoryStrokeScreenPoints 화면 하한)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import rig
from pack_bitmap_r3_shape import load, roles
from pack_bitmap_r3_colors import NAMES
import pack_detail_r3 as R3

R_PT = 0.22 * 0.75 * 35.25          # 5.8163 pt — 착용 0.75 의 머리 반경
STROKE_PT = 1.0

vecw = {}
for pn, k, m, P in R3.PACKS:
    for slot in ("HEAD", "EYES", "NECK", "BACK"):
        d, iid, fn = P[slot]
        a = [q for p in fn() for q in p.pts]
        x0, y0, x1, y1 = rig.bounds(a)
        vecw[R3.FILES[(k, slot)]] = x1 - x0

print("아이템                           런중앙값  비트맵폭  R3폭(R)   두께(R)  착용0.75(pt)  획 대비")
print("-"*92)
out = []
for n in NAMES:
    arr = load(n); role, m, bg = roles(arr)
    T = (role == 1)
    ys, xs = np.nonzero(role >= 0); W = xs.max() - xs.min() + 1
    runs = []
    for r in range(role.shape[0]):
        row = T[r]; i = 0
        while i < len(row):
            if row[i]:
                j = i
                while j < len(row) and row[j]: j += 1
                if i > 0 and j < len(row) and not row[i-1] and not row[j]: runs.append(j - i)
                i = j
            else: i += 1
    med = float(np.median(runs)) if runs else 0.0
    inR = med / W * vecw[n]
    pt = inR * R_PT
    out.append((n, med, W, vecw[n], inR, pt))
    print("%-30s %7.1f %8d %8.2f %8.3f %12.2f %8.2fx" % (n, med, W, vecw[n], inR, pt, pt / STROKE_PT))
a = np.array([o[4] for o in out]); b = np.array([o[5] for o in out])
print("-"*92)
print("최소 %.3f R (%.2f pt) · 중앙값 %.3f R (%.2f pt) · 최대 %.3f R (%.2f pt)"
      % (a.min(), b.min(), np.median(a), np.median(b), a.max(), b.max()))
print("→ 엔진 최소 획 1.00pt 대비 R3 트림은 %.1f ~ %.1f 배 굵다." % (1/b.max(), 1/b.min()))
