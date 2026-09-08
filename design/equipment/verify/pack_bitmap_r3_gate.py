#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 팔레트 게이트 재현 — PackPaletteGateTests 의 5개 자를 파이썬으로 다시 짠다.

  python3 pack_bitmap_r3_gate.py                # 현행 12색 + 후보안 판정
  python3 pack_bitmap_r3_gate.py --calib        # 계산기 교정만
  python3 pack_bitmap_r3_gate.py --search       # 「금 트림」 보조색이 존재하는가 전탐색

★ 이 스크립트는 프로덕션 상수를 <b>숫자로 베끼지 않는다</b>는 규칙의 예외다 —
  파이썬에서 C#을 참조할 수 없으므로 아래 SOURCED 표에 <b>출처 파일:줄</b>을 함께 적고,
  --calib 이 프로덕션과 같은 알려진 값(흰/검 21.0 · #767676 4.5422 · 빨강 Lab)을 내는지 먼저 본다.
  출처가 움직이면 --calib 은 여전히 통과하므로, 표의 값은 라운드마다 grep 으로 재확인해야 한다.
"""
import os, re, sys, math, colorsys, itertools
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
ITEMS = os.path.join(ROOT, 'Assets/_Project/Resources/Items')

# ---------------- SOURCED: 프로덕션에서 옮긴 값 (출처 표기) ----------------
MIN_NON_TEXT_CONTRAST = 3.0        # UiChrome.cs:1361  MinNonTextContrast
PORTRAIT_SURFACE = (0.914, 0.918, 0.902)   # UiChrome.cs:162
CHARCOAL_STAGE   = (0.145, 0.157, 0.180)   # CharacterPortraitStage.cs:373
RARITY_RAMP = [0x9C978C, 0xBCAC8B, 0xDEC081, 0xFFD375]  # UiChrome.cs:479-482
WORN_S_FLOOR, WORN_V_FLOOR, WORN_V_CEIL = 0.42, 0.55, 0.80   # ItemCatalog.cs:1961-1963
PACK_CATALOG_DE_FLOOR = 8.0        # PackPaletteGateTests.cs  PackCatalogDeltaEFloor
DISCRIMINATION_FLOOR  = 7.8        # PackPaletteGateTests.cs  DiscriminationFloor

FROZEN = [   # PackPaletteGateTests.FrozenPacks
    ('오피스 워커', 222.0, 0x456ECC, 0x6080CC),
    ('사이버 아포칼립스', 172.0, 0x009682, 0x518D85),
    ('네온 낙서', 312.0, 0xCC1BA9, 0x9C5A8E),
    ('스포츠', 8.0, 0xCC3F29, 0x9E655C),
    ('컬러 잉크', 268.0, 0x9768CC, 0x8563AB),
    ('밀리터리', 80.0, 0x639400, 0x798C51),
    ('광부', 33.0, 0xC96F00, 0x8D7251),
    # ★ 2026-09-09 M-2 착지 — 대마법사 138° -> 150°(리더 채택). 값은 design/art/verify/pack78.pick(150, ...)
    #   가 낸 것에 §13-4 각주의 V/S 정정을 같은 식으로 적용한 것이다. **주색만 옮기면 안 된다** —
    #   PackPaletteGateTests.동결_대장의_색이_선언된_색상각에_있다 가 보조색도 ±1° 를 요구한다.
    #   (아래 §6 후보 탐색은 보조색을 138° 에 고정한 채로 돌기 때문에 이 제약을 못 본다 — 표를 그대로 믿지 마라.)
    ('대마법사', 150.0, 0x00994C, 0x518D6F),
]

# ---------------- 색 계산기 ----------------
def hex2rgb(h): return ((h >> 16 & 255)/255.0, (h >> 8 & 255)/255.0, (h & 255)/255.0)
def rgb2hex(c): return '#%02X%02X%02X' % tuple(int(round(max(0,min(1,x))*255)) for x in c)

def lin(c): return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def rel_lum(c):
    r, g, b = (lin(x) for x in c)
    return 0.2126*r + 0.7152*g + 0.0722*b
def contrast(a, b):
    la, lb = rel_lum(a), rel_lum(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)

def lab(c):
    r, g, b = (lin(x) for x in c)
    x = r*0.4124564 + g*0.3575761 + b*0.1804375
    y = r*0.2126729 + g*0.7151522 + b*0.0721750
    z = r*0.0193339 + g*0.1191920 + b*0.9503041
    x, y, z = x/0.95047, y/1.0, z/1.08883
    f = lambda t: t**(1/3) if t > 216/24389 else (841/108)*t + 4/29
    fx, fy, fz = f(x), f(y), f(z)
    return (116*fy-16, 500*(fx-fy), 200*(fy-fz))

def dE76(c1, c2):
    """★ PackPaletteGateTests.DeltaE 는 **CIE76**(Lab 유클리드 거리)다 — CIEDE2000 이 아니다
    (그 파일 787행: `Vector3.Distance(Lab(a), Lab(b))`). 교정값(흰/검 100 · 빨강 Lab)은 둘 다 통과하므로
    «교정이 통과했다»만 보고 CIEDE2000 을 쓰면 게이트 판정이 통째로 달라진다 — 첫 시안이 그렇게 12건을
    거짓 미달로 냈다. 게이트 재현은 반드시 이 함수를 쓴다."""
    a, b = lab(c1), lab(c2)
    return math.sqrt(sum((a[i]-b[i])**2 for i in range(3)))


def dE2000(c1, c2):
    L1,a1,b1 = lab(c1); L2,a2,b2 = lab(c2)
    C1 = math.hypot(a1,b1); C2 = math.hypot(a2,b2); Cb = (C1+C2)/2
    G = 0.5*(1-math.sqrt(Cb**7/(Cb**7+25**7))) if Cb > 0 else 0.5
    a1p, a2p = (1+G)*a1, (1+G)*a2
    C1p, C2p = math.hypot(a1p,b1), math.hypot(a2p,b2)
    h1p = math.degrees(math.atan2(b1,a1p)) % 360 if (a1p or b1) else 0.0
    h2p = math.degrees(math.atan2(b2,a2p)) % 360 if (a2p or b2) else 0.0
    dLp, dCp = L2-L1, C2p-C1p
    if C1p*C2p == 0: dhp = 0.0
    elif abs(h2p-h1p) <= 180: dhp = h2p-h1p
    elif h2p-h1p > 180: dhp = h2p-h1p-360
    else: dhp = h2p-h1p+360
    dHp = 2*math.sqrt(C1p*C2p)*math.sin(math.radians(dhp)/2)
    Lbp, Cbp = (L1+L2)/2, (C1p+C2p)/2
    if C1p*C2p == 0: hbp = h1p+h2p
    elif abs(h1p-h2p) <= 180: hbp = (h1p+h2p)/2
    elif h1p+h2p < 360: hbp = (h1p+h2p+360)/2
    else: hbp = (h1p+h2p-360)/2
    T = (1-0.17*math.cos(math.radians(hbp-30))+0.24*math.cos(math.radians(2*hbp))
         +0.32*math.cos(math.radians(3*hbp+6))-0.20*math.cos(math.radians(4*hbp-63)))
    dTh = 30*math.exp(-(((hbp-275)/25)**2))
    Rc = 2*math.sqrt(Cbp**7/(Cbp**7+25**7)) if Cbp > 0 else 0
    Sl = 1+(0.015*(Lbp-50)**2)/math.sqrt(20+(Lbp-50)**2)
    Sc = 1+0.045*Cbp; Sh = 1+0.015*Cbp*T
    Rt = -math.sin(math.radians(2*dTh))*Rc
    return math.sqrt((dLp/Sl)**2+(dCp/Sc)**2+(dHp/Sh)**2+Rt*(dCp/Sc)*(dHp/Sh))

def worn(c):
    h, s, v = colorsys.rgb_to_hsv(*c)
    return colorsys.hsv_to_rgb(h, max(s, WORN_S_FLOOR), min(max(v, WORN_V_FLOOR), WORN_V_CEIL))
def worn_identity(c):
    w = worn(c)
    return all(abs(w[i]-c[i]) < 0.004 for i in range(3))

BACKDROPS = [('밝은 바탕화면', (1,1,1)), ('어두운 바탕화면', (0,0,0)),
             ('종이 무대', PORTRAIT_SURFACE), ('목탄 무대', CHARCOAL_STAGE)]

def band_floor(bg):
    """이 배경보다 «충분히 밝은» 최소 휘도."""
    return MIN_NON_TEXT_CONTRAST*(rel_lum(bg)+0.05) - 0.05
def band_ceil(bg):
    return (rel_lum(bg)+0.05)/MIN_NON_TEXT_CONTRAST - 0.05

def self_standing_band():
    s = sorted(BACKDROPS, key=lambda t: rel_lum(t[1]))
    valid = []
    for split in range(len(s)+1):
        lo, hi = 0.0, 1.0; ls, hs = '휘도 하한(0)', '휘도 상한(1)'
        for i in range(split):
            f = band_floor(s[i][1])
            if f > lo: lo, ls = f, s[i][0]
        for i in range(split, len(s)):
            c = band_ceil(s[i][1])
            if c < hi: hi, hs = c, s[i][0]
        if lo < hi: valid.append((lo, hi, ls, hs))
    return valid

# ---------------- 카탈로그 색 (에셋에서 직접) ----------------
PACK_PREFIX = 'pack_'
def catalog_colors():
    out = {}
    for fn in sorted(os.listdir(ITEMS)):
        if not fn.endswith('.asset'): continue
        if fn.startswith(PACK_PREFIX): continue           # 팩은 모집단에서 뺀다(게이트와 동일)
        if not (fn.startswith('equip_') or fn.startswith('look_')): continue
        txt = open(os.path.join(ITEMS, fn), encoding='utf-8').read()
        for m in re.finditer(r'color: \{r: ([-\d.]+), g: ([-\d.]+), b: ([-\d.]+), a: ([-\d.]+)\}', txt):
            c = (float(m.group(1)), float(m.group(2)), float(m.group(3)))
            out.setdefault(rgb2hex(c), c)
    return out

def calib():
    ok = True
    def chk(name, got, want, tol):
        nonlocal ok
        good = abs(got-want) <= tol
        ok = ok and good
        print(f"   {'OK ' if good else 'FAIL'} {name}: {got:.4f} (기대 {want})")
    chk('대비 흰/검', contrast((1,1,1),(0,0,0)), 21.0, 0.0005)
    chk('대비 동일색', contrast((1,1,1),(1,1,1)), 1.0, 0.0005)
    chk('대비 #767676/흰', contrast(hex2rgb(0x767676),(1,1,1)), 4.5422, 0.0005)
    L,a,b = lab((1,1,1)); chk('Lab 흰 L*', L, 100, 0.01); chk('Lab 흰 a*', a, 0, 0.01); chk('Lab 흰 b*', b, 0, 0.01)
    chk('Lab 검 L*', lab((0,0,0))[0], 0, 0.01)
    L,a,b = lab(hex2rgb(0xFF0000)); chk('Lab 빨강 L*', L, 53.24, 0.02); chk('Lab 빨강 a*', a, 80.09, 0.05); chk('Lab 빨강 b*', b, 67.20, 0.05)
    chk('ΔE76 동일색', dE76((1,1,1),(1,1,1)), 0.0, 1e-4)
    chk('ΔE76 흰/검', dE76((1,1,1),(0,0,0)), 100.0, 0.01)
    chk('ΔE2000 동일색', dE2000((1,1,1),(1,1,1)), 0.0, 1e-4)
    chk('ΔE2000 흰/검', dE2000((1,1,1),(0,0,0)), 100.0, 0.01)
    return ok

def judge(name, c, others, cat, verbose=True):
    """한 색이 5개 자를 통과하는가. others = 함께 실릴 팩 12색(자기 제외)."""
    fails = []
    if not worn_identity(c):
        fails.append(f'WornColor 비항등 → 몸에서 {rgb2hex(worn(c))}')
    for bn, bg in BACKDROPS:
        cr = contrast(c, bg)
        if cr < MIN_NON_TEXT_CONTRAST: fails.append(f'배경 {bn} 대비 {cr:.2f}:1 < 3.0')
    lo, hi, _, _ = self_standing_band()[0]
    L = rel_lum(c)
    if not (lo <= L <= hi): fails.append(f'자립 대역 밖 L={L:.4f} ∉ [{lo:.4f},{hi:.4f}]')
    worst_cat = min((dE76(c, v), k) for k, v in cat.items())
    if worst_cat[0] < PACK_CATALOG_DE_FLOOR: fails.append(f'카탈로그 {worst_cat[1]}와 ΔE {worst_cat[0]:.2f} < 8.0')
    if others:
        wo = min((dE76(c, v), n) for n, v in others)
        if wo[0] < DISCRIMINATION_FLOOR: fails.append(f'팩 내부 {wo[1]}와 ΔE {wo[0]:.2f} < 7.8')
    else:
        wo = (float('inf'), '-')
    wr = min((dE76(c, hex2rgb(h)), rgb2hex(hex2rgb(h))) for h in RARITY_RAMP)
    if wr[0] < DISCRIMINATION_FLOOR: fails.append(f'등급 램프 {wr[1]}와 ΔE {wr[0]:.2f} < 7.8')
    if verbose:
        print(f"  {name:<26}{rgb2hex(c)}  L={rel_lum(c):.4f}  카탈로그최소ΔE {worst_cat[0]:5.2f}  "
              f"팩내부최소 {wo[0]:5.2f}  등급최소 {wr[0]:5.2f}  → {'통과' if not fails else 'X ' + ' / '.join(fails)}")
    return fails

def pack_colors():
    out = []
    for n, hue, p, s in FROZEN:
        out.append((n+' 주', hex2rgb(p)))
        out.append((n+' 보조', hex2rgb(s)))
    return out

def main():
    print('=== 0. 계산기 교정 ===')
    if not calib():
        print('교정 실패 — 아래 판정 전부 폐기'); sys.exit(2)
    print()
    bands = self_standing_band()
    print(f'=== 1. 자립 대역 ===  후보 {len(bands)}개 (기대 1)')
    for lo, hi, ls, hs in bands:
        print(f'   L ∈ [{lo:.4f} ({ls}), {hi:.4f} ({hs})]')
    print(f'   → 이 대역 안에서 나올 수 있는 가장 밝은 회색 ≈ {rgb2hex((bands[0][1]**(1/2.2),)*3)} 급'
          f' / 실제 상한 L={bands[0][1]:.4f}')
    if '--calib' in sys.argv: return
    print()
    cat = catalog_colors()
    print(f'=== 2. 카탈로그 고유색 {len(cat)}종 (팩 제외, 에셋 직접 파싱) ===')
    print()
    pcs = pack_colors()
    print('=== 3. 현행 팩 16색 판정 (12색 + 신규 4색 = 8팩 × 2) ===')
    nfail = 0
    for i, (n, c) in enumerate(pcs):
        others = [x for j, x in enumerate(pcs) if j != i]
        f = judge(n, c, others, cat)
        nfail += 1 if f else 0
    print(f'  → 위반 {nfail}건 (기대 0 — 트리가 초록이면 0이어야 한다)')
    print()

    # ---- 비트맵이 요구하는 「크림/금 트림」이 이 게이트 안에 존재하는가 ----
    print('=== 4. 비트맵 트림색 후보 판정 ===')
    print('   비트맵 실측 트림(3팩 공통): #F0F0D9 / #F1ECCE / #F3E5B9 / #EFDCAC / #F4E4B1 / #F2EBD7')
    for h in (0xF0F0D9, 0xF1ECCE, 0xF3E5B9, 0xEFDCAC, 0xF4E4B1, 0xE2DBB9, 0xDBC090, 0xB9A27D, 0xAD8D64, 0x8E795C):
        judge(f'비트맵 트림 {rgb2hex(hex2rgb(h))}', hex2rgb(h), pcs, cat)
    print()
    print('=== 5. 전탐색 — 「금/크림」 계열(색상각 30~65°)에서 5개 자를 전부 통과하는 색이 있는가 ===')
    found = []
    for hue in range(28, 70):
        for s in np.arange(0.42, 1.001, 0.02):
            for v in np.arange(0.55, 0.801, 0.01):
                c = colorsys.hsv_to_rgb(hue/360.0, float(s), float(v))
                c = tuple(round(x*255)/255.0 for x in c)
                if judge('', c, pcs, cat, verbose=False): continue
                found.append((hue, float(s), float(v), c))
    print(f'   해 {len(found)}개.')
    if found:
        # 비트맵 트림 평균과 가장 가까운 해
        tgt = hex2rgb(0xF1E7C4)
        found.sort(key=lambda t: dE2000(t[3], tgt))
        for hue, s, v, c in found[:6]:
            print(f'   {rgb2hex(c)} H{hue}° S{s:.2f} V{v:.2f}  비트맵트림 #F1E7C4와 ΔE2000 {dE2000(c,tgt):5.2f}'
                  f'  등급최소ΔE76 {min(dE76(c,hex2rgb(h)) for h in RARITY_RAMP):5.2f}')
    print()
    print('=== 6. 「대마법사 주색을 비트맵 색상각으로」 후보 ===')
    for hue in (138, 145, 148, 150, 152, 155, 158, 160):
        best = None
        for s in np.arange(0.42, 1.001, 0.01):
            for v in np.arange(0.55, 0.801, 0.005):
                c = colorsys.hsv_to_rgb(hue/360.0, float(s), float(v))
                c = tuple(round(x*255)/255.0 for x in c)
                if judge('', c, [x for x in pcs if not x[0].startswith('대마법사 주')], cat, verbose=False): continue
                # 비트맵 대마법사 초록 대표 #208751 / #279E60 / #1A6941 의 평균에 가장 가까운 것
                score = min(dE2000(c, hex2rgb(x)) for x in (0x208751, 0x279E60, 0x1A6941))
                if best is None or score < best[0]: best = (score, c, s, v)
        if best:
            print(f'   H{hue}°  최적 {rgb2hex(best[1])} S{best[2]:.2f} V{best[3]:.3f}  비트맵초록 최소ΔE {best[0]:5.2f}')
        else:
            print(f'   H{hue}°  해 없음')

def search_accent():
    """비트맵의 «발광/포인트» 색에 가장 가까우면서 5개 자를 전부 통과하는 보조색을 팩별로 찾는다.
    목표색은 pack_bitmap_r3_de.py 가 낸 역할 «발광» 면적가중 평균이다."""
    cat = catalog_colors()
    pcs = pack_colors()
    TARGETS = {
        '사이버 아포칼립스': [0x75CF95, 0x7EC690, 0x42D5A1, 0x3BBA8E],
        '광부': [0xFBB02D, 0xFAA11D, 0xFAC23F, 0xF6A323],
        '대마법사': [0x6BCC6B, 0x7BD182, 0x73D084, 0x87CE7A],
    }
    print('=== 7. 보조색 재지정 후보 — «비트맵 발광색»에 가장 가까운 합법색 ===')
    for name, tg in TARGETS.items():
        others = [(n, c) for n, c in pcs if not n.startswith(name + ' 보조')]
        best = []
        for hue in range(0, 360):
            for s in np.arange(0.42, 1.001, 0.02):
                for v in np.arange(0.55, 0.801, 0.01):
                    c = colorsys.hsv_to_rgb(hue/360.0, float(s), float(v))
                    c = tuple(round(x*255)/255.0 for x in c)
                    if judge('', c, others, cat, verbose=False): continue
                    d = min(dE2000(c, hex2rgb(t)) for t in tg)
                    best.append((d, c, hue, float(s), float(v)))
        best.sort(key=lambda t: t[0])
        cur = [c for n, c in pcs if n == name + ' 보조'][0]
        dcur = min(dE2000(cur, hex2rgb(t)) for t in tg)
        print(f'  {name}: 현행 {rgb2hex(cur)} -> 비트맵 발광과 ΔE2000 {dcur:5.2f}')
        for d, c, hue, s, v in best[:4]:
            print(f'      후보 {rgb2hex(c)} H{hue}° S{s:.2f} V{v:.2f}  ΔE2000 {d:5.2f}  (개선 {dcur-d:+5.2f})')
        if not best: print('      해 없음')


if __name__ == '__main__':
    if '--accent' in sys.argv:
        if not calib(): sys.exit(2)
        search_accent()
    else:
        main()
