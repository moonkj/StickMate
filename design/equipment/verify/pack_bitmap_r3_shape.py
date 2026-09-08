#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 — 비트맵 12장의 «구조»를 숫자로: 전경 마스크 · 종횡비 · 4역할(몸/트림/암부/발광) 점유·중심·범위 ·
가로 프로파일(실루엣 폭) · 밑단 꼭짓점 수.

  python3 pack_bitmap_r3_shape.py             # 표
  python3 pack_bitmap_r3_shape.py --profile   # 실루엣 폭 프로파일 20단
"""
import os, sys, math
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pack_bitmap_r3_colors import (ICONS, NAMES, rgb_to_lab_array, rgb_to_lab, hexs, bg_mask)

def load(name, sub=1):
    im = Image.open(os.path.join(ICONS, name + '.png')).convert('RGB')
    return np.asarray(im)[::sub, ::sub]

def roles(arr):
    """4역할 분류 — 배경 제외 뒤 L*/C* 로 가른다.
       암부  L* < 30
       트림  L* >= 78 이고 C* < 45        (크림/금 테두리 — 밝고 저채도)
       발광  L* >= 62 이고 C* >= 45       (보석·램프·빛)
       몸    나머지"""
    m, bg = bg_mask(arr)
    h, w, _ = arr.shape
    flat = arr.reshape(-1, 3)/255.0
    lab = rgb_to_lab_array(flat)
    L = lab[:, 0]; C = np.hypot(lab[:, 1], lab[:, 2])
    fgm = ~m.reshape(-1)
    role = np.full(len(L), -1, np.int8)
    role[fgm & (L < 30)] = 0
    role[fgm & (L >= 78) & (C < 45)] = 1
    role[fgm & (L >= 62) & (C >= 45)] = 2
    role[fgm & (role < 0)] = 3
    return role.reshape(h, w), m, bg

RN = ['암부', '트림', '발광', '몸']

def main():
    prof = '--profile' in sys.argv
    print('역할 분류: 암부 L*<30 / 트림 L*>=78·C*<45 / 발광 L*>=62·C*>=45 / 몸 나머지.  (512x512 전 픽셀)')
    print()
    hdr = f"{'아이템':<30}{'전경%':>6}{'종횡(W/H)':>10}   " + ''.join(f"{r+'%':>8}" for r in RN) + f"{'트림중심y':>10}{'암부중심y':>10}"
    print(hdr); print('-'*len(hdr))
    store = {}
    for n in NAMES:
        arr = load(n)
        role, m, bg = roles(arr)
        h, w = role.shape
        fg = role >= 0
        ys, xs = np.nonzero(fg)
        bw, bh = xs.max()-xs.min()+1, ys.max()-ys.min()+1
        tot = fg.sum()
        shares = [(role == i).sum()/tot for i in range(4)]
        def cy(i):
            yy, _ = np.nonzero(role == i)
            return (1 - (yy.mean()-ys.min())/bh) if len(yy) else float('nan')   # 0=밑단 1=꼭대기
        store[n] = (role, (xs.min(), xs.max(), ys.min(), ys.max()))
        print(f"{n:<30}{tot/(w*h)*100:>5.1f}%{bw/bh:>10.3f}   " + ''.join(f"{s*100:>7.1f}%" for s in shares)
              + f"{cy(1):>10.3f}{cy(0):>10.3f}")
    if not prof: return
    print()
    print('=== 실루엣 폭 프로파일 (위 → 아래 20단, 폭 = 그 단의 전경 가로폭 / 전체 폭) ===')
    for n in NAMES:
        role, (x0, x1, y0, y1) = store[n]
        fg = role >= 0
        bw = x1-x0+1; bh = y1-y0+1
        cells = []
        for k in range(20):
            a = y0 + int(bh*k/20); b = y0 + max(a+1, int(bh*(k+1)/20))
            band = fg[a:b, :]
            cols = band.any(axis=0)
            cells.append(0.0 if not cols.any() else (np.nonzero(cols)[0].max()-np.nonzero(cols)[0].min()+1)/bw)
        print(f"  {n:<30}" + ' '.join(f"{c:.2f}" for c in cells))

if __name__ == '__main__':
    main()
