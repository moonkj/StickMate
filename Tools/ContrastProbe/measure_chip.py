#!/usr/bin/env python3
"""닫기/설정 칩의 면·잉크 대비를 캡처 PNG의 픽셀에서 직접 잰다.

★ 계산기는 쓰기 전에 알려진 값으로 교정한다(흰/검 21.00, 동일색 1.00).
  교정에 실패하면 아무 숫자도 내지 않고 죽는다 — 이 저장소의 거짓 초록은 전부
  "실패한 측정과 성공한 측정이 똑같이 생긴" 형태였다.

사용법:  measure_chip.py <png> <x> <y> <w> <h> [라벨]
  주어진 사각형 안에서 최빈색(면)과 최대·최소 휘도 화소(잉크 후보)를 뽑아 보고한다.
"""
import sys
from collections import Counter
from PIL import Image

def lin(c):
    c = max(0.0, min(1.0, c))
    return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4

def L(rgb):
    return 0.2126*lin(rgb[0]/255)+0.7152*lin(rgb[1]/255)+0.0722*lin(rgb[2]/255)

def CR(a, b):
    la, lb = L(a), L(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi+0.05)/(lo+0.05)

def calibrate():
    checks = [("흰/검", CR((255,255,255),(0,0,0)), 21.0, 0.005),
              ("동일색(흰)", CR((255,255,255),(255,255,255)), 1.0, 0.0005),
              ("동일색(패널)", CR((20,23,28),(20,23,28)), 1.0, 0.0005),
              ("#767676/흰", CR((0x76,)*3,(255,255,255)), 4.54, 0.005),
              ("#000/#808080", CR((0,0,0),(0x80,)*3), 5.32, 0.005)]
    ok = True
    for n, v, e, tol in checks:
        p = abs(v-e) <= tol
        ok &= p
        print(f"  {'PASS' if p else 'FAIL'}  {n:14s} {v:.4f} (정답 {e})")
    if not ok:
        sys.exit("교정 실패 — 이 스크립트가 내는 모든 숫자를 폐기하십시오.")
    print("  교정 판정: 유효\n")

def region_stats(im, x, y, w, h):
    px = im.convert("RGB").load()
    cnt = Counter()
    for j in range(y, y+h):
        for i in range(x, x+w):
            cnt[px[i, j]] += 1
    total = sum(cnt.values())
    face, n = cnt.most_common(1)[0]
    brightest = max(cnt, key=L)
    darkest = min(cnt, key=L)
    return face, n/total, brightest, darkest, total

def main():
    if len(sys.argv) < 6:
        sys.exit(__doc__)
    path, x, y, w, h = sys.argv[1], *map(int, sys.argv[2:6])
    label = sys.argv[6] if len(sys.argv) > 6 else "영역"
    print("=== 대비 계산기 교정 ===")
    calibrate()
    im = Image.open(path)
    face, share, bright, dark, total = region_stats(im, x, y, w, h)
    print(f"=== {label}  {path} [{x},{y} {w}x{h}] 화소 {total} ===")
    print(f"  최빈색(면)   rgb{face}  #{'%02X%02X%02X'%face}  점유율 {share*100:.1f}%")
    print(f"  최대휘도 화소 rgb{bright}  #{'%02X%02X%02X'%bright}  대 면 = {CR(bright,face):.2f}:1")
    print(f"  최소휘도 화소 rgb{dark}  #{'%02X%02X%02X'%dark}  대 면 = {CR(dark,face):.2f}:1")

if __name__ == "__main__":
    main()
