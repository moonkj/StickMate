#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""빌드 캡처 §2 — 등급 리본 자동 탐지 + 실측.

앞선 shot_l4l6.py의 리본 상자 4개는 카드 테두리를 집었다(밝은 흰 선이 상자를 통과).
여기서는 리본 띠 y를 **찾아서** 잰다. 상자를 손으로 대지 않는다.
"""
import math
from PIL import Image

SHOT = "/tmp/stickmate-run/screenshots/final-full.png"

def srgb_lin(c):
    c /= 255.0
    return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def lum(c):
    r,g,b = (srgb_lin(float(x)) for x in c[:3])
    return 0.2126*r+0.7152*g+0.0722*b
def cr(a,b):
    la,lb = lum(a),lum(b); hi,lo = max(la,lb),min(la,lb)
    return (hi+0.05)/(lo+0.05)
def to_lab(c):
    r,g,b = (srgb_lin(float(x)) for x in c[:3])
    x,y,z = 0.4124*r+0.3576*g+0.1805*b, 0.2126*r+0.7152*g+0.0722*b, 0.0193*r+0.1192*g+0.9505*b
    xn,yn,zn = 0.95047,1.0,1.08883
    f = lambda t: t**(1/3) if t > 216/24389 else (841/108)*t + 4/29
    fx,fy,fz = f(x/xn),f(y/yn),f(z/zn)
    return (116*fy-16, 500*(fx-fy), 200*(fy-fz))
def de(a,b):
    la,lb = to_lab(a),to_lab(b)
    return math.sqrt(sum((la[i]-lb[i])**2 for i in range(3)))
def hx(h): return ((h>>16)&255,(h>>8)&255,h&255)
def sh(c): return "#%02X%02X%02X" % tuple(int(v) for v in c[:3])

# 교정
assert abs(cr((255,255,255),(0,0,0)) - 21.0) < 1e-6
assert abs(cr((200,161,90),(200,161,90)) - 1.0) < 1e-9
assert abs(de((200,161,90),(200,161,90))) < 1e-9
assert abs(cr(hx(0x9C978C), hx(0x1B1F26)) - 5.68) < 0.01
print("교정: 흰/검 21.0 · 동일색 1.0 / ΔE 0 · 일반/카드면 5.68 — 유효\n")

RAMP = [("일반",0x9C978C),("희귀",0xBCAC8B),("영웅",0xDEC081),("전설",0xFFD375)]
CARD, TRACK, BRASS = 0x1B1F26, 0x3A4049, 0xC8A15A

im = Image.open(SHOT).convert("RGB")
px = im.load()
S = im.size[0]/2000.0
X = lambda v: int(round(v*S))
Y = lambda v: int(round(v*S))

# 카드 격자 (표시 좌표) — 2열 x 3행
COLS = [(1140, 1378), (1402, 1640)]
ROWS = [266, 556, 846]           # 카드 상단 테두리 근처
CARDS = [("1 천모자·일반·착용중", 0, 0, "일반"),
         ("2 ???·일반·잠김",      1, 0, "일반"),
         ("3 ???·희귀·잠김",      0, 1, "희귀"),
         ("4 ???·희귀·잠김",      1, 1, "희귀"),
         ("5 ???·영웅·잠김",      0, 2, "영웅"),
         ("6 ???·전설·잠김",      1, 2, "전설")]
want = dict(RAMP)

def find_ribbon(x0, x1, ytop):
    """ytop 아래 40px(표시) 안에서 「가로로 가장 길게 이어지는 밝은 띠」의 y와 색."""
    best = None
    for yy in range(Y(ytop)-4, Y(ytop+40)):
        row = [px[x, yy][:3] for x in range(X(x0), X(x1))]
        bright = [c for c in row if lum(c) > 0.12]
        if len(bright) < 20: continue
        bright.sort(key=lum)
        med = bright[len(bright)//2]
        score = len(bright) * lum(med)
        if best is None or score > best[0]:
            best = (score, yy, med, len(bright), len(row))
    return best

print("=== 리본 띠 자동 탐지 ===")
found = {}
for label, ci, ri, w in CARDS:
    x0, x1 = COLS[ci]
    b = find_ribbon(x0, x1, ROWS[ri])
    score, yy, med, nb, nr = b
    # 채움 칸 개수 = 밝은 픽셀 런 개수
    row = [px[x, yy][:3] for x in range(X(x0), X(x1))]
    runs, cur = [], 0
    for c in row:
        if lum(c) > 0.12: cur += 1
        elif cur: runs.append(cur); cur = 0
    if cur: runs.append(cur)
    runs = [r for r in runs if r > 8]
    found[label] = med
    near = min(RAMP, key=lambda kv: de(med, hx(kv[1])))
    print("  %-22s y=%4d  채움 %s  칸 %d개  ΔE(%s)=%5.2f  최근접=%s  CR(카드면)=%5.2f"
          % (label, yy, sh(med), len(runs), w, de(med, hx(want[w])), near[0], cr(med, hx(CARD))))

print("\n=== 리본 빈 칸(트랙) ===")
for label, ci, ri, w in CARDS:
    x0, x1 = COLS[ci]
    b = find_ribbon(x0, x1, ROWS[ri]); yy = b[1]
    row = [(x, px[x, yy][:3]) for x in range(X(x0), X(x1))]
    dark = [c for x, c in row if 0.008 < lum(c) < 0.12]
    if not dark:
        print("  %-22s 빈 칸 없음(전부 채움)" % label); continue
    dark.sort(key=lum); md = dark[len(dark)//2]
    print("  %-22s 트랙 %s  ΔE(#3A4049)=%5.2f  채움/트랙 CR=%.2f"
          % (label, sh(md), de(md, hx(TRACK)), cr(found[label], md)))

print("\n=== 「일반」끼리 · 「일반 vs 전설」 ===")
a = found["1 천모자·일반·착용중"]; b2 = found["2 ???·일반·잠김"]
print("  일반(착용) %s vs 일반(잠김) %s  ΔE=%.2f" % (sh(a), sh(b2), de(a, b2)))
print("  일반 vs 전설  ΔE=%.2f (핸드오프 제약판은 채널차 37로 사실상 동색이었다)"
      % de(b2, found["6 ???·전설·잠김"]))

print("\n=== 카드 테두리 (좌변 세로선) ===")
for label, ci, ri, w in CARDS:
    x0, _ = COLS[ci]
    ys = range(Y(ROWS[ri]+40), Y(ROWS[ri]+180))
    best, bl = None, -1
    for xx in range(X(x0)-6, X(x0)+8):
        col = [px[xx, y][:3] for y in ys]
        col.sort(key=lum); m = col[len(col)//2]
        if lum(m) > bl: bl, best = lum(m), m
    print("  %-22s %s  ΔE(브라스)=%5.2f  ΔE(파랑#5DA1F5)=%5.2f  ΔE(%s램프)=%5.2f"
          % (label, sh(best), de(best, hx(BRASS)), de(best, hx(0x5DA1F5)), w, de(best, hx(want[w]))))
