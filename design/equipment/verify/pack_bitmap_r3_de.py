#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 — «벡터가 실제로 칠하는 4색» vs «비트맵의 같은 역할 4색» ΔE2000.

벡터 4색은 프로덕션 규칙 그대로 만든다:
  주색 M   = entry.PrimaryColor   (에셋 icon tone 0)        ... WornColor 항등이라 카드=몸
  보조 M2  = entry.SecondaryColor (에셋 icon tone 1)
  그늘     = M x AccessoryTone.ShadeFactor(0.28)
  트림     = Lerp(밑색, 흰, lineAlpha)  — tone 3 은 «흰 선 알파»다(AccessoryTone.Highlight)
비트맵 4색은 pack_bitmap_r3_shape.py 와 같은 역할 분류(암부/트림/발광/몸)의 «면적가중 평균색».
"""
import os, sys, math, colorsys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pack_bitmap_r3_colors import (ICONS, NAMES, rgb_to_lab_array, rgb_to_lab, hexs,
                                   delta_e2000, asset_icon_colors, worn_color)
from pack_bitmap_r3_shape import load, roles

TRIM_ALPHA = 0.85      # R3 트림 조각의 lineAlpha
HI_ALPHA = 0.42        # AccessoryTone.HighlightWhiteAlpha
SHADE = 0.28           # AccessoryTone.ShadeFactor

def lerp_white(c, a): return tuple(x + (1.0-x)*a for x in c)

print("벡터 색 = 프로덕션 식(WornColor 항등 · 그늘 x0.28 · 트림 = 흰 α0.85 를 주색 위에 합성).")
print("비트맵 색 = 역할별 면적가중 평균. ΔE 는 CIEDE2000. (참고: 사람이 «같은 색»이라 부르는 상한은 통상 ΔE 5~10)")
print()
hdr = "%-30s %-22s %-22s %-22s %-22s" % ("아이템", "몸 M vs 비트맵몸", "그늘 vs 비트맵암부", "트림 vs 비트맵트림", "보조 M2 vs 비트맵발광")
print(hdr); print("-"*len(hdr))
tot = {0: [], 1: [], 2: [], 3: []}
for n in NAMES:
    tones, _ = asset_icon_colors(n)
    M, M2 = tones[0], tones[1]
    vec = {3: M, 0: tuple(c*SHADE for c in M), 1: lerp_white(M, TRIM_ALPHA), 2: M2}
    arr = load(n)
    role, m, bg = roles(arr)
    flat = arr.reshape(-1, 3)/255.0
    rr = role.reshape(-1)
    cells = []
    for k in (3, 0, 1, 2):
        sel = flat[rr == k]
        if len(sel) == 0:
            cells.append("       —              "); continue
        # 면적가중 평균은 Lab 에서 낸다(sRGB 평균은 어두워진다)
        lab = rgb_to_lab_array(sel).mean(axis=0)
        d = delta_e2000(rgb_to_lab(vec[k]), tuple(lab))
        tot[k].append(d)
        from pack_bitmap_r3_colors import lab_to_rgb
        cells.append("%s vs %s  %5.1f" % (hexs(vec[k]), hexs(lab_to_rgb(tuple(lab))), d))
    print("%-30s %s %s %s %s" % (n, cells[0], cells[1], cells[2], cells[3]))
print()
print("평균 ΔE —  몸 %.1f · 암부 %.1f · 트림 %.1f · 발광/보조 %.1f"
      % (np.mean(tot[3]), np.mean(tot[0]), np.mean(tot[1]), np.mean(tot[2])))
print()
print("★ 참고: «크림 트림»을 카탈로그 보조색으로 옮길 수 있는가 -> pack_bitmap_r3_gate.py 가 답을 낸다(자립 대역 L∈[0.163,0.240]).")
