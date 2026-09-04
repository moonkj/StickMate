#!/usr/bin/env python3
"""R10 판정 — TextOnAccent 를 브라스 가족으로 옮긴다.
계산기는 알려진 값으로 먼저 교정한다. 교정이 깨지면 아래 숫자는 전부 폐기."""
import math, itertools, sys

# ---------- 색 기본 ----------
def lin(c):  # sRGB -> 선형
    c/=255.0
    return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def Y(rgb):  # 상대휘도 (WCAG 2.x)
    r,g,b = (lin(v) for v in rgb)
    return 0.2126*r + 0.7152*g + 0.0722*b
def CR(a,b):
    la,lb = Y(a), Y(b)
    if la < lb: la,lb = lb,la
    return (la+0.05)/(lb+0.05)

_WP=(0.95047,1.0,1.08883)
def rgb2xyz(rgb):
    r,g,b=(lin(v) for v in rgb)
    return (0.4124564*r+0.3575761*g+0.1804375*b,
            0.2126729*r+0.7151522*g+0.0721750*b,
            0.0193339*r+0.1191920*g+0.9503041*b)
def _f(t): return t**(1/3) if t > 216/24389 else (841/108)*t + 4/29
def lab(rgb):
    x,y,z=rgb2xyz(rgb); fx,fy,fz=_f(x/_WP[0]),_f(y/_WP[1]),_f(z/_WP[2])
    return (116*fy-16, 500*(fx-fy), 200*(fy-fz))
def dE(a,b):  # CIE76 — 이 저장소의 정본 자 (pairdE.py / lumorder.py 와 같은 식)
    la,lb=lab(a),lab(b)
    return math.sqrt(sum((la[i]-lb[i])**2 for i in range(3)))
def hue(rgb):  # LCh 색상각
    L,a,b=lab(rgb); return math.degrees(math.atan2(b,a))%360
def arc(h1,h2):
    d=abs(h1-h2)%360; return min(d,360-d)

def over(fg, a, bg):  # 8비트 sRGB 알파 합성 (Unity UI 감마 공간), 반올림 half-up
    return tuple(int(math.floor(a*f + (1-a)*b + 0.5)) for f,b in zip(fg,bg))
def hx(c): return "#%02X%02X%02X" % c
def u(*f): return tuple(int(round(v*255)) for v in f)   # UiChrome float -> 8bit

# ---------- 0. 교정 ----------
def calibrate():
    ok=True
    W,K,G=(255,255,255),(0,0,0),(128,128,128)
    tests=[
        ("CR(흰,검)",      CR(W,K),            21.0,   1e-9),
        ("CR(검,흰) 대칭", CR(K,W),            21.0,   1e-9),
        ("CR(동일색)",     CR((200,161,90),(200,161,90)), 1.0, 1e-12),
        ("CR(흰,흰)",      CR(W,W),            1.0,    1e-12),
        ("Y(흰)",          Y(W),               1.0,    1e-9),
        ("Y(검)",          Y(K),               0.0,    1e-12),
        ("L*(흰)",         lab(W)[0],          100.0,  1e-4),
        ("L*(검)",         lab(K)[0],          0.0,    1e-9),
        ("a*b*(중성회)",   abs(lab(G)[1])+abs(lab(G)[2]), 0.0, 1e-4),
        ("ΔE(흰,검)",      dE(W,K),            100.0,  1e-4),
        ("ΔE(동일)",       dE((200,161,90),(200,161,90)), 0.0, 1e-12),
        # 합성 항등: α=1 이면 전경 그대로, α=0 이면 배경 그대로
        ("합성 α=1",       sum(abs(x-y) for x,y in zip(over((200,161,90),1.0,(27,31,38)),(200,161,90))), 0, 0),
        ("합성 α=0",       sum(abs(x-y) for x,y in zip(over((200,161,90),0.0,(27,31,38)),(27,31,38))),   0, 0),
        # 알려진 출하값 재현: AccentBorder(α0.55) on CardSurface = #7A6743 (PALETTE_SPEC §28-8-1 다)
        ("합성 재현 #7A6743", 0 if hx(over(u(.784,.631,.353),0.55,u(.106,.122,.149)))=="#7A6743" else 1, 0, 0),
        # 알려진 ΔE 재현: 브라스 ↔ CardBorderWorn = 90.52 (UiChrome.cs:167)
        ("ΔE 재현 90.52",  dE(u(.784,.631,.353), u(.365,.631,.961)), 90.52, 0.01),
        # 알려진 ΔE 재현: 브라스 ↔ 영웅 #DEC081 = 12.86 (UiChrome.cs:187 / §12-5)
        ("ΔE 재현 12.86",  dE(u(.784,.631,.353), (0xDE,0xC0,0x81)), 12.86, 0.01),
        # 알려진 CR 재현: OnAccentSolid on Accent = 7.91 (UiChrome.cs:350)
        ("CR 재현 7.91",   CR(u(.043,.063,.086), u(.784,.631,.353)), 7.91, 0.01),
        # 알려진 L 재현: 브라스 L = 0.3851 (§0)
        ("L 재현 0.3851",  Y(u(.784,.631,.353)), 0.3851, 0.0001),
    ]
    for n,got,want,tol in tests:
        good = abs(got-want) <= tol; ok &= good
        print(f"  [{'OK ' if good else 'FAIL'}] {n:<20} = {got:.6f}  (기대 {want})")
    return ok

print("=== 0. 계산기 교정 (알려진 값 18건) ===")
if not calibrate():
    print("\n교정 실패 — 이 뒤의 숫자는 전부 폐기한다."); sys.exit(2)
print("  → 교정 통과. 이 자를 아래에 쓴다.\n")

# ---------- 토큰 ----------
ACCENT   = u(.784,.631,.353)          # #C8A15A
PANEL    = u(.078,.090,.110)          # 패널면
CARD     = u(.106,.122,.149)          # 카드면
SUBTLE   = u(.098,.114,.141)          # 보조면
CUR      = u(.561,.765,1.000)         # 현행 TextOnAccent #8FC3FF
CAND     = (0xE3,0xB9,0x71)           # 리더 후보 #E3B971

# TextOnAccent 가 실제로 앉는 바탕 = AccentSurface(α0.14) 를 세 기반면에 합성한 것
BASES = [("패널면 위 칩", PANEL), ("카드면 위 칩", CARD), ("보조면 위 칩", SUBTLE)]
CHIPS = [(n, over(ACCENT, 0.14, b), b) for n,b in BASES]

# 예약색
RESERVED = [
    ("TextTertiary(잠김)",   (0x8B,0x93,0x9F)),
    ("CardBorderWorn(착용)", u(.365,.631,.961)),
    ("등급 일반",            (0x9C,0x97,0x8C)),
    ("등급 희귀",            (0xBC,0xAC,0x8B)),
    ("등급 영웅",            (0xDE,0xC0,0x81)),
    ("등급 전설",            (0xFF,0xD3,0x75)),
]

print("=== 1. TextOnAccent 가 실제로 앉는 바탕 3종 ===")
for n,chip,b in CHIPS:
    print(f"  {n:<12} 기반 {hx(b)} + AccentSurface α0.14 -> {hx(chip)}  (L={Y(chip):.4f}, 색상각 {hue(chip):.1f}°)")

print("\n=== 2. 현행 #8FC3FF 재측정 (coder-ui 주장 7.03 / 7.75 / 7.21 대조) ===")
cur_cr=[]
for n,chip,_ in CHIPS:
    c=CR(CUR,chip); cur_cr.append(c)
    print(f"  {n:<12} CR {c:.4f}")
print(f"  색상각: 글자 {hue(CUR):.1f}° vs 칩 {hue(CHIPS[1][1]):.1f}° -> 호 {arc(hue(CUR),hue(CHIPS[1][1])):.1f}°")

import colorsys
def hsvh(rgb): return colorsys.rgb_to_hsv(*[v/255 for v in rgb])[0]*360
# 교정: 이 저장소 산문의 「색상각」은 HSV 색상각이다 (LCh 아님) — 알려진 3값으로 확인
assert abs(hsvh(ACCENT)-38.7)<0.05 and abs(hsvh(CUR)-212.1)<0.05 and abs(hsvh(CAND)-37.9)<0.05
assert abs(hsvh(CHIPS[1][1])-40.0)<0.05
print("\n  [OK ] 색상각 규약 교정: HSV 색상각으로 38.7 / 212.1 / 37.9 / 40.0 재현")

BASE_CR = cur_cr[:]          # 내 자로 잰 현행값 (열화 금지선)
CODER   = [7.75, 7.03, 7.21] # coder-ui 주장 (같은 순서로 정렬)
print("\n=== 3. coder-ui 주장과 내 자의 차이 ===")
for (n,_,_),mine,theirs in zip(CHIPS,BASE_CR,CODER):
    print(f"  {n:<12} 내 자 {mine:.4f} / coder-ui {theirs:.2f}  (차 {abs(mine-theirs):.4f})")

# ---------- 4. 전수 탐색 ----------
import numpy as np
print("\n=== 4. 전수 탐색 (8비트 RGB 16,777,216색) ===")
g = np.arange(256, dtype=np.float64)/255.0
gl = np.where(g <= 0.04045, g/12.92, ((g+0.055)/1.055)**2.4)
R,G,B = np.meshgrid(np.arange(256),np.arange(256),np.arange(256), indexing='ij')
R=R.ravel().astype(np.uint8); G=G.ravel().astype(np.uint8); B=B.ravel().astype(np.uint8)
Yv = 0.2126*gl[R] + 0.7152*gl[G] + 0.0722*gl[B]

def crv(Yc, bgc):     # 후보 배열 vs 단일 배경
    yb = Y(bgc)
    hi = np.maximum(Yc, yb); lo = np.minimum(Yc, yb)
    return (hi+0.05)/(lo+0.05)

m = Yv > 0.30                                            # C1 크롬 대역
print(f"  C1 크롬 대역 L>0.30            -> {m.sum():,}")
for (n,chip,_),base in zip(CHIPS,BASE_CR):
    m &= crv(Yv, chip) >= base                           # C2 열화 금지
    print(f"  C2 {n} CR>={base:.4f}  -> {m.sum():,}")

idx = np.flatnonzero(m)
cand = np.stack([R[idx],G[idx],B[idx]],axis=1).astype(np.int16)
print(f"  남은 후보: {len(cand):,}")

# C3 브라스 가족 — HSV 색상각 호
mx = cand.max(axis=1).astype(np.float64); mn = cand.min(axis=1).astype(np.float64)
dl = np.maximum(mx-mn, 1e-9)
r,gg,b = cand[:,0],cand[:,1],cand[:,2]
h = np.where(mx==r, ((gg-b)/dl)%6, np.where(mx==gg, (b-r)/dl+2, (r-gg)/dl+4))*60.0
h = h % 360
ha = np.minimum(np.abs(h-hsvh(ACCENT)), 360-np.abs(h-hsvh(ACCENT)))
HUE_TOL = 3.0
m2 = ha <= HUE_TOL
print(f"  C3 브라스 색상각 호 <= {HUE_TOL}°     -> {m2.sum():,}")
cand = cand[m2]; ha = ha[m2]

# ΔE (벡터화 CIE76)
def labv(a):
    lr,lg,lb = gl[a[:,0]], gl[a[:,1]], gl[a[:,2]]
    x=0.4124564*lr+0.3575761*lg+0.1804375*lb
    y=0.2126729*lr+0.7151522*lg+0.0721750*lb
    z=0.0193339*lr+0.1191920*lg+0.9503041*lb
    def f(t): return np.where(t>216/24389, np.cbrt(t), (841/108)*t+4/29)
    fx,fy,fz=f(x/_WP[0]),f(y/_WP[1]),f(z/_WP[2])
    return np.stack([116*fy-16, 500*(fx-fy), 200*(fy-fz)],axis=1)
LC = labv(cand)
def dEv(target):
    t=np.array(lab(target)); return np.sqrt(((LC-t)**2).sum(axis=1))

# C4 예약색 침범 금지
DISCERN=7.8; HERO_FLOOR=dE(ACCENT,(0xDE,0xC0,0x81))   # 12.864 — 브라스 자신의 거리
m3 = np.ones(len(cand),bool)
for n,c in RESERVED:
    d=dEv(c)
    floor = HERO_FLOOR if n=="등급 영웅" else DISCERN
    m3 &= d >= floor
    print(f"  C4 {n:<20} ΔE>={floor:.3f} -> {m3.sum():,}")
cand=cand[m3]; ha=ha[m3]; LC=LC[m3]
print(f"  최종 실현가능 후보: {len(cand):,}")

# 목적함수: 브라스에 최근접 (= 브라스 가족의 밝은 값)
dbrass = dEv(ACCENT)
order = np.argsort(dbrass)
print(f"\n  --- 브라스 최근접 상위 8 ---")
for i in order[:8]:
    c=tuple(int(v) for v in cand[i])
    print(f"   {hx(c)}  ΔE_brass {dbrass[i]:6.3f}  호 {ha[i]:.2f}°  L {Y(c):.4f}  "
          f"CR {CR(c,CHIPS[0][1]):.3f}/{CR(c,CHIPS[1][1]):.3f}/{CR(c,CHIPS[2][1]):.3f}  "
          f"ΔE영웅 {dE(c,(0xDE,0xC0,0x81)):.2f}")
WIN = tuple(int(v) for v in cand[order[0]])

print(f"\n=== 5. 리더 후보 #E3B971 검증 ===")
inpool = any(tuple(int(v) for v in c)==CAND for c in cand)
print(f"  실현가능 집합에 있는가: {'예' if inpool else '아니오'}")
for n,chip,_ in CHIPS:
    print(f"  {n:<12} CR {CR(CAND,chip):.4f}")
print(f"  L {Y(CAND):.4f} (크롬 대역 L>0.30: {'통과' if Y(CAND)>0.30 else '실패'})  호 {arc(hsvh(CAND),hsvh(ACCENT)):.2f}°  ΔE_brass {dE(CAND,ACCENT):.3f}")
for n,c in RESERVED:
    print(f"  ΔE {n:<20} {dE(CAND,c):7.3f}")
print(f"  카드면 직접 CR {CR(CAND,CARD):.3f} / 패널면 {CR(CAND,PANEL):.3f}")
