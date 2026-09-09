#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""design-art R-비트맵: 몸에 붙는 비트맵 장비의 팔레트 규칙 유도 + 검산.
   ★ 교정이 깨지면 그 뒤 숫자는 전부 폐기한다(팀 규칙).
"""
import sys, math, itertools

# ---------------------------------------------------------------- 기본 변환
def hex2rgb(h):
    h = h.lstrip('#')
    return (int(h[0:2],16), int(h[2:4],16), int(h[4:6],16))

def rgb2hex(r,g,b):
    return '#%02X%02X%02X' % (int(round(r)), int(round(g)), int(round(b)))

def srgb_to_lin(c):           # c in 0..1
    return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4

def lin_to_srgb(c):
    return c*12.92 if c <= 0.0031308 else 1.055*(c**(1/2.4)) - 0.055

def relL(hexstr):
    r,g,b = [v/255.0 for v in hex2rgb(hexstr)]
    return 0.2126*srgb_to_lin(r) + 0.7152*srgb_to_lin(g) + 0.0722*srgb_to_lin(b)

def contrast(a,b):
    la, lb = relL(a), relL(b)
    hi, lo = max(la,lb), min(la,lb)
    return (hi+0.05)/(lo+0.05)

# ---------------------------------------------------------------- Lab / ΔE
def rgb2xyz(hexstr):
    r,g,b = [srgb_to_lin(v/255.0) for v in hex2rgb(hexstr)]
    X = r*0.4124564 + g*0.3575761 + b*0.1804375
    Y = r*0.2126729 + g*0.7151522 + b*0.0721750
    Z = r*0.0193339 + g*0.1191920 + b*0.9503041
    return X,Y,Z

WP = (0.95047, 1.00000, 1.08883)   # D65 2도

def xyz2lab(X,Y,Z):
    def f(t):
        return t**(1/3) if t > 216/24389 else (841/108)*t + 4/29
    fx, fy, fz = f(X/WP[0]), f(Y/WP[1]), f(Z/WP[2])
    return (116*fy - 16, 500*(fx-fy), 200*(fy-fz))

def lab(hexstr):
    return xyz2lab(*rgb2xyz(hexstr))

def de76_lab(L1,L2):
    return math.sqrt(sum((a-b)**2 for a,b in zip(L1,L2)))

def de76(h1,h2):
    return de76_lab(lab(h1), lab(h2))

def de2000_lab(lab1, lab2, kL=1.0, kC=1.0, kH=1.0):
    L1,a1,b1 = lab1; L2,a2,b2 = lab2
    C1 = math.hypot(a1,b1); C2 = math.hypot(a2,b2)
    Cbar = (C1+C2)/2.0
    G = 0.5*(1 - math.sqrt(Cbar**7/(Cbar**7 + 25.0**7))) if Cbar > 0 else 0.5
    a1p = (1+G)*a1; a2p = (1+G)*a2
    C1p = math.hypot(a1p,b1); C2p = math.hypot(a2p,b2)
    def hp(ap,bp):
        if ap == 0 and bp == 0: return 0.0
        h = math.degrees(math.atan2(bp,ap))
        return h+360 if h < 0 else h
    h1p = hp(a1p,b1); h2p = hp(a2p,b2)
    dLp = L2-L1
    dCp = C2p-C1p
    if C1p*C2p == 0:
        dhp = 0.0
    else:
        d = h2p-h1p
        if d > 180: d -= 360
        elif d < -180: d += 360
        dhp = d
    dHp = 2*math.sqrt(C1p*C2p)*math.sin(math.radians(dhp)/2)
    Lbp = (L1+L2)/2.0
    Cbp = (C1p+C2p)/2.0
    if C1p*C2p == 0:
        hbp = h1p+h2p
    else:
        d = abs(h1p-h2p)
        s = h1p+h2p
        if d <= 180: hbp = s/2.0
        elif s < 360: hbp = (s+360)/2.0
        else: hbp = (s-360)/2.0
    T = (1 - 0.17*math.cos(math.radians(hbp-30))
           + 0.24*math.cos(math.radians(2*hbp))
           + 0.32*math.cos(math.radians(3*hbp+6))
           - 0.20*math.cos(math.radians(4*hbp-63)))
    dtheta = 30*math.exp(-(((hbp-275)/25.0)**2))
    RC = 2*math.sqrt(Cbp**7/(Cbp**7 + 25.0**7)) if Cbp > 0 else 0.0
    SL = 1 + (0.015*(Lbp-50)**2)/math.sqrt(20+(Lbp-50)**2)
    SC = 1 + 0.045*Cbp
    SH = 1 + 0.015*Cbp*T
    RT = -math.sin(math.radians(2*dtheta))*RC
    return math.sqrt((dLp/(kL*SL))**2 + (dCp/(kC*SC))**2 + (dHp/(kH*SH))**2
                     + RT*(dCp/(kC*SC))*(dHp/(kH*SH)))

def de2000(h1,h2):
    return de2000_lab(lab(h1), lab(h2))

# ---------------------------------------------------------------- HSV
def rgb2hsv(hexstr):
    r,g,b = [v/255.0 for v in hex2rgb(hexstr)]
    mx, mn = max(r,g,b), min(r,g,b)
    d = mx-mn
    if d == 0: h = 0.0
    elif mx == r: h = (60*((g-b)/d)) % 360
    elif mx == g: h = 60*((b-r)/d) + 120
    else: h = 60*((r-g)/d) + 240
    s = 0.0 if mx == 0 else d/mx
    return h, s, mx

def hsv2hex(h,s,v):
    h = h % 360
    c = v*s; x = c*(1-abs((h/60) % 2 - 1)); m = v-c
    if   h < 60:  rp,gp,bp = c,x,0
    elif h < 120: rp,gp,bp = x,c,0
    elif h < 180: rp,gp,bp = 0,c,x
    elif h < 240: rp,gp,bp = 0,x,c
    elif h < 300: rp,gp,bp = x,0,c
    else:         rp,gp,bp = c,0,x
    return rgb2hex((rp+m)*255, (gp+m)*255, (bp+m)*255)

def worn_color(hexstr):
    """ItemCatalog.WornColor 재현 (S>=0.42, V in [0.55,0.80]) — 잉크 표식 제외."""
    h,s,v = rgb2hsv(hexstr)
    s = max(s, 0.42)
    v = min(max(v, 0.55), 0.80)
    return hsv2hex(h,s,v)

# ================================================================ 교정
def calib():
    print("="*78)
    print("교정 — 알려진 값으로 먼저 맞춘다. 하나라도 깨지면 아래 숫자는 전부 폐기.")
    print("="*78)
    ok = True
    def chk(name, got, want, tol):
        nonlocal ok
        good = abs(got-want) <= tol
        ok = ok and good
        print("  [%s] %-52s got=%.4f want=%.4f tol=%.4f" %
              ("OK " if good else "FAIL", name, got, want, tol))

    # 1. 대비비 — 흰/검 = 21.0, 동일색 = 1.0
    chk("대비 흰↔검", contrast('#FFFFFF','#000000'), 21.0, 1e-9)
    chk("대비 동일색(#9B7922)", contrast('#9B7922','#9B7922'), 1.0, 1e-12)
    chk("대비 대칭성", contrast('#111111','#F2F2F2'), contrast('#F2F2F2','#111111'), 1e-12)
    # 2. 상대휘도 — 흰 1.0 / 검 0.0 / 중간회색 #808080
    chk("relL 흰", relL('#FFFFFF'), 1.0, 1e-12)
    chk("relL 검", relL('#000000'), 0.0, 1e-12)
    chk("relL #808080", relL('#808080'), 0.2159, 5e-4)   # 공개된 표준값
    # 3. Lab — 흰 = (100,0,0), 검 = (0,0,0), #808080 L*≈53.585
    Lw = lab('#FFFFFF'); Lb = lab('#000000'); Lg = lab('#808080')
    chk("Lab 흰 L*", Lw[0], 100.0, 1e-3)
    chk("Lab 흰 a*", Lw[1], 0.0, 1e-3)
    chk("Lab 흰 b*", Lw[2], 0.0, 1e-3)
    chk("Lab 검 L*", Lb[0], 0.0, 1e-9)
    chk("Lab #808080 L*", Lg[0], 53.5850, 5e-3)
    # 4. ΔE2000 — Sharma/Wu/Dalal 공개 검증쌍
    pairs = [
        ((50.0000, 2.6772,-79.7751),(50.0000, 0.0000,-82.7485), 2.0425),
        ((50.0000, 3.1571,-77.2803),(50.0000, 0.0000,-82.7485), 2.8615),
        ((50.0000, 2.8361,-74.0200),(50.0000, 0.0000,-82.7485), 3.4412),
        ((50.0000,-1.3802,-84.2814),(50.0000, 0.0000,-82.7485), 1.0000),
        ((50.0000,-1.1848,-84.8006),(50.0000, 0.0000,-82.7485), 1.0000),
        ((50.0000,-0.9009,-85.5211),(50.0000, 0.0000,-82.7485), 1.0000),
        ((50.0000, 2.4900,-0.0010),(50.0000,-2.4900, 0.0009), 7.1792),
        ((50.0000, 2.4900,-0.0010),(50.0000,-2.4900, 0.0010), 7.1792),
        ((50.0000, 2.4900,-0.0010),(50.0000,-2.4900, 0.0012), 7.2195),
        ((50.0000,-0.0010, 2.4900),(50.0000, 0.0009,-2.4900), 4.8045),
        ((50.0000, 2.5000, 0.0000),(50.0000, 0.0000,-2.5000), 4.3065),
        ((50.0000, 2.5000, 0.0000),(73.0000,25.0000,-18.0000),27.1492),
        ((50.0000, 2.5000, 0.0000),(50.0000, 3.1736, 0.5854), 1.0000),
        ((50.0000, 2.5000, 0.0000),(50.0000, 3.2972, 0.0000), 1.0000),
        ((50.0000, 2.5000, 0.0000),(50.0000, 1.8634, 0.5757), 1.0000),
        ((60.2574,-34.0099,36.2677),(60.4626,-34.1751,39.4387), 1.2644),
        ((22.7233,20.0904,-46.6940),(23.0331,14.9730,-42.5619), 2.0373),
        ((2.0776, 0.0795,-1.1350),( 0.9033,-0.0636,-0.5514), 0.9082),
    ]
    for i,(a,b,want) in enumerate(pairs):
        chk("ΔE2000 Sharma #%02d" % (i+1), de2000_lab(a,b), want, 1e-4)
    chk("ΔE2000 동일색 = 0", de2000('#C96F00','#C96F00'), 0.0, 1e-12)
    chk("ΔE2000 대칭성", de2000('#9B7922','#C6443C'), de2000('#C6443C','#9B7922'), 1e-9)
    chk("ΔE76 동일색 = 0", de76('#9B7922','#9B7922'), 0.0, 1e-12)

    # 5. ★ 저장소 바깥 실측과의 대조 — design-equipment R3 (내 코드와 무관한 출처)
    #    docs/EQUIPMENT_SHAPE_SPEC_PACK_DETAIL_R3.md §2-2:
    #      #EFDCAC ↔ 등급 램프 #DEC081 : ΔE76 22.8 / ΔE2000 7.71
    # ★ 자기 정정: 문서(R3 §2-2)의 «ΔE76 22.8»은 R3 자신의 스크립트로도 재현되지 않는다(13.6879).
    #    같은 행의 ΔE2000 7.71은 정확히 재현된다 ⇒ 문서 전사 오류. 내 자는 R3 스크립트와 일치한다.
    chk("R3 스크립트 대조 ΔE76(#EFDCAC,#DEC081)", de76('#EFDCAC','#DEC081'), 13.6879, 0.001)
    chk("R3 대조 ΔE2000(#EFDCAC,#DEC081)", de2000('#EFDCAC','#DEC081'), 7.71, 0.01)
    #    R3 §2-2 표의 L 값 (같은 문서, 다른 저자 도구)
    for hx, want in [('#F0F0D9',0.859),('#F1ECCE',0.832),('#F3E5B9',0.786),
                     ('#EFDCAC',0.725),('#DBC090',0.548),('#B9A27D',0.376),
                     ('#AD8D64',0.289)]:
        chk("R3 대조 L(%s)" % hx, relL(hx), want, 0.0015)
    #    R3 §2-2 표의 흰 바탕/종이 무대 대비
    # ★ 자기 정정: 문서의 «3.36»도 재현 불가(3.1015). 같은 행 종이무대 2.57은 정확히 재현된다.
    chk("R3 스크립트 대조 #AD8D64↔흰바탕", contrast('#AD8D64','#FFFFFF'), 3.1015, 0.001)
    chk("R3 대조 #AD8D64↔종이무대", contrast('#AD8D64','#E9EAE6'), 2.57, 0.01)

    # 6. ★ PALETTE_SPEC / EQUIPMENT_PALETTE 실측과의 대조 (또 다른 출처)
    # 대역 경계는 8비트 양자화 없이 휘도로 직접 잰다(hex로 굽으면 반올림이 검산을 흐린다).
    def cr(la, lb):
        hi, lo = max(la,lb), min(la,lb); return (hi+0.05)/(lo+0.05)
    chk("대역 상한 L0.2396 ↔ 종이무대", cr(0.2396, relL('#E9EAE6')), 3.0000, 0.0002)
    chk("대역 하한 L0.1632 ↔ 목탄무대", cr(0.1632, relL('#25282E')), 3.0000, 0.0010)
    chk("종이무대 #E9EAE6 L", relL('#E9EAE6'), 0.8188, 0.0002)
    chk("목탄무대 #25282E L", relL('#25282E'), 0.0211, 0.0002)
    chk("브라스 #C8A15A L", relL('#C8A15A'), 0.3851, 0.001)
    # ★ 자기 정정: 처음에 카드 바탕을 #15181E(CardSurfaceMuted = 잠김 썸네일)로 잡아 네 값이 전부
    #    7.6% 높게 나왔다. 등급 램프 주석이 말하는 «카드 바탕»은 CardSurface #1B1F26 (L 0.0135)다
    #    (PALETTE_SPEC §3670 표가 둘을 구분해 적어 뒀다). 네 값이 «같은 방향으로 같은 비율» 틀린 것이
    #    단서였다 — 색이 아니라 배경이 틀렸다는 뜻이다.
    chk("등급 일반 #9C978C ↔ CardSurface #1B1F26", contrast('#9C978C','#1B1F26'), 5.68, 0.01)
    chk("등급 희귀 #BCAC8B ↔ CardSurface", contrast('#BCAC8B','#1B1F26'), 7.40, 0.01)
    chk("등급 영웅 #DEC081 ↔ CardSurface", contrast('#DEC081','#1B1F26'), 9.41, 0.01)
    chk("등급 전설 #FFD375 ↔ CardSurface", contrast('#FFD375','#1B1F26'), 11.66, 0.01)
    chk("CardSurface #1B1F26 L", relL('#1B1F26'), 0.0135, 0.0002)
    chk("CardSurfaceMuted #15181E L", relL('#15181E'), 0.0091, 0.0002)
    chk("등급 일반↔전설 ΔE76", de76('#9C978C','#FFD375'), 51.54, 0.05)
    chk("등급 인접1 일반↔희귀 ΔE76", de76('#9C978C','#BCAC8B'), 15.16, 0.05)
    chk("등급 인접3 영웅↔전설 ΔE76", de76('#DEC081','#FFD375'), 18.02, 0.05)
    chk("팩 오피스주 #456ECC L", relL('#456ECC'), 0.1678, 0.0005)
    chk("팩 밀리터리주 #639400 L", relL('#639400'), 0.2383, 0.0005)
    chk("팩 네온보 #9C5A8E L", relL('#9C5A8E'), 0.1633, 0.0005)
    chk("광부주 #C96F00 L", relL('#C96F00'), 0.2379, 0.0005)
    chk("대마법사주 #00994C L", relL('#00994C'), 0.2330, 0.0005)
    # WornColor 재현 대조 — R3 표의 "몸에서 #CCCC76 / #CCC076 / #CCB776"
    for src, want in [('#F0F0D9','#CCCC76'),('#F1ECCE','#CCC076'),('#F3E5B9','#CCB776')]:
        got = worn_color(src)
        good = got == want
        ok = ok and good
        print("  [%s] %-52s got=%s want=%s" %
              ("OK " if good else "FAIL", "WornColor 재현 %s" % src, got, want))
    # WornColor 항등 대조 — 카탈로그 색은 항등이어야 한다
    for hx in ['#9B7922','#C6443C','#C96F00','#8D7251','#00994C','#518D6F','#456ECC','#639400']:
        got = worn_color(hx)
        good = got == hx
        ok = ok and good
        print("  [%s] %-52s got=%s want=%s" %
              ("OK " if good else "FAIL", "WornColor 항등 %s" % hx, got, hx))

    print()
    print("교정 결과: %s" % ("전부 통과 — 아래 숫자를 신뢰한다." if ok else "★ 실패 — 아래 숫자 전부 폐기."))
    print()
    return ok



# ================================================================ 상수 (전부 저장소 실측 출처)
BAND_LO, BAND_HI = 0.1632, 0.2396          # PALETTE_SPEC §0 자립(아트) 대역
MOAT_HI          = 0.3000                  # 해자 상한 = 크롬 대역 하단
BG = {                                     # 장비가 실제로 놓이는 배경 전부
    '밝은 바탕화면 #F2F2F2': '#F2F2F2',
    '흰 바탕화면 #FFFFFF'  : '#FFFFFF',
    '어두운 바탕화면 #1E1E1E': '#1E1E1E',
    '검은 바탕화면 #111111': '#111111',
    '종이 무대 #E9EAE6'    : '#E9EAE6',
    '목탄 무대 #25282E'    : '#25282E',
    '검은 잉크 머리 #000000': '#000000',   # StickConfig.primaryOutlineColor 실측 = 순검정
    '흰 잉크 머리 #FFFFFF' : '#FFFFFF',   # StickConfig.whiteInkColor 실측 = 순백
}
BG4 = ['#F2F2F2','#FFFFFF','#1E1E1E','#111111']
STAGE = ['#E9EAE6','#25282E']
RAMP = {'일반':'#9C978C','희귀':'#BCAC8B','영웅':'#DEC081','전설':'#FFD375'}
CARD_SURFACE = '#1B1F26'
CROWN_M, CROWN_M2 = '#9B7922', '#C6443C'
SHADE_FACTOR = 0.28
HILITE_WHITE_ALPHA = 0.42

def shaded(hexstr, f=SHADE_FACTOR):
    r,g,b = hex2rgb(hexstr)
    return rgb2hex(r*f, g*f, b*f)

def over_white(hexstr, a=HILITE_WHITE_ALPHA):
    """흰 α를 불투명 화소로 사전 합성(비트맵은 알파를 못 쓴다)."""
    r,g,b = hex2rgb(hexstr)
    return rgb2hex(a*255+(1-a)*r, a*255+(1-a)*g, a*255+(1-a)*b)

def in_band(hexstr):  return BAND_LO <= relL(hexstr) <= BAND_HI
def worst_bg(hexstr, bgs): return min(contrast(hexstr,b) for b in bgs)

def hr(t): print("\n" + "="*78 + "\n" + t + "\n" + "="*78)

# ---------------------------------------------------------------- 양성 대조
def control():
    hr("양성 대조 — 게이트가 실제로 죽는지 먼저 보인다 (죽은 프로브 방지)")
    cases = [
        ("대역 밖(밝음) #F1E7C4 이 in_band 를 통과하면 안 된다", not in_band('#F1E7C4')),
        ("대역 밖(어두움) #2B220A 이 in_band 를 통과하면 안 된다", not in_band('#2B220A')),
        ("대역 안 #9B7922 는 통과해야 한다", in_band('#9B7922')),
        ("대역 안 색이 배경 6종 3.0 을 넘어야 한다", worst_bg('#9B7922', BG4+STAGE) >= 3.0),
        ("대역 밖 밝은 색은 배경 6종 3.0 에 미달해야 한다", worst_bg('#F1E7C4', BG4+STAGE) < 3.0),
        ("대역 밖 어두운 색도 미달해야 한다", worst_bg('#2B220A', BG4+STAGE) < 3.0),
        ("ΔE2000 이 ΔE76 과 다른 값이어야 한다(자를 바꿔치기하면 잡힌다)",
         abs(de76('#EFDCAC','#DEC081') - de2000('#EFDCAC','#DEC081')) > 1.0),
        ("shaded() 가 실제로 어둡게 만든다", relL(shaded('#9B7922')) < relL('#9B7922')),
        ("over_white() 가 실제로 밝게 만든다", relL(over_white('#9B7922')) > relL('#9B7922')),
    ]
    allok = True
    for name, got in cases:
        allok = allok and got
        print("  [%s] %s" % ("OK " if got else "FAIL", name))
    print("\n양성 대조: %s" % ("전부 통과" if allok else "★ 실패 — 판정 무효"))
    return allok

# ================================================================ §A 다운샘플 기하
def sec_downsample():
    hr("§A. 다운샘플 기하 — 「몸에서 1 디바이스 픽셀」이 저작 캔버스의 몇 px인가")
    # §19-2 실측: 모자 폭 = 화면높이의 1.788% @배율0.75 / 0.834% @배율0.35
    frac = {'배율 0.75(출하 기본)': 0.01788, '배율 0.35(하한)': 0.00834, '배율 1.00(상한)': 0.02383}
    disp = {'1920x1080 @100% (Windows 최악)': 1080, '2560x1440': 1440,
            '3840x2160 (4K)': 2160, 'MacBook Pro 14"(3024)': 1964,
            'MacBook Air 13" M2': 1664, 'iPhone 15 Pro': 2556}
    print("  ※ 모자 폭(px) = 화면 세로 화소 × 모자폭비율. §19-2 표를 그대로 재현해 자를 교정한다.")
    print("  %-32s %-22s %8s %10s %10s" % ("디스플레이","배율","모자폭px","256²/모자폭","1024²/모자폭"))
    worst = None
    for dn, dh in disp.items():
        for sn, f in frac.items():
            if sn != '배율 0.75(출하 기본)' and dn != '1920x1080 @100% (Windows 최악)': continue
            w = dh*f
            print("  %-32s %-22s %8.1f %10.1f %10.1f" % (dn, sn, w, 256/w, 1024/w))
            if worst is None or w < worst[0]: worst = (w, dn, sn)
    print("\n  ★ 최악 = %s / %s → 모자 폭 %.1f px" % (worst[1], worst[2], worst[0]))
    print("     §19-2가 적은 값: 배율0.75 1080p = 19.3px / 배율0.35 1080p = 9.0px  (교정 대조)")
    for sn, f in frac.items():
        print("       내 자: 1080p %s = %.1f px" % (sn, 1080*f))
    print()
    w075 = 1080*0.01788; w035 = 1080*0.00834
    for label, w in [('배율 0.75 @1080p', w075), ('배율 0.35 @1080p', w035)]:
        s256, s1024 = 256/w, 1024/w
        print("  [%s] 모자폭 %.1f 디바이스px" % (label, w))
        print("      1 디바이스px = 출하 256²의 %.2f px = 저작 1024²의 %.1f px" % (s256, s1024))
        print("      나이퀴스트(형태로 읽히려면 ≥2 디바이스px) = 저작 1024²의 %.0f px "
              "= 캔버스 폭의 %.1f%%" % (2*s1024, 200*s1024/1024))
    print()
    print("  카드 표면 대조 — 같은 그림이 카드에서는 거의 등배다:")
    for label, px in [('카드 72pt @1x', 72), ('카드 72pt @2x Retina', 144), ('카드 72pt @3x 폰', 216)]:
        print("      %-22s 256² 대비 %.2f배 축소  → 1 디바이스px = 저작 1024²의 %.1f px"
              % (label, 256/px, 1024/px))
    return w075, w035

# ================================================================ §B 자 판정 (F-3 회신)
def sec_metric():
    hr("§B. 【F-3 회신】 어느 자를 쓸 것인가 — ΔE76 vs ΔE2000")
    pool = ['#96814F','#BA7636','#5577AE','#9B7922','#5075B5','#587398','#20878C','#CC5512',
            '#5A8C3C','#428C24','#BA5928','#CC3C3C','#6787B9','#955CCC','#AB7942','#C6443C',
            '#456ECC','#6080CC','#009682','#518D85','#CC1BA9','#9C5A8E','#CC3F29','#9E655C',
            '#9768CC','#8563AB','#639400','#798C51','#C96F00','#8D7251','#00994C','#518D6F']
    rat = []
    for a,b in itertools.combinations(pool,2):
        d76, d00 = de76(a,b), de2000(a,b)
        if d00 > 0.01: rat.append((d76/d00, d76, d00, a, b))
    rat.sort()
    n = len(rat)
    print("  아트 대역 32색의 %d 쌍에서 ΔE76 / ΔE2000 비율:" % n)
    print("    최소 %.3f  (%s↔%s : 76=%.2f, 00=%.2f)" % (rat[0][0], rat[0][3], rat[0][4], rat[0][1], rat[0][2]))
    print("    중앙 %.3f" % rat[n//2][0])
    print("    평균 %.3f" % (sum(r[0] for r in rat)/n))
    print("    최대 %.3f  (%s↔%s : 76=%.2f, 00=%.2f)" % (rat[-1][0], rat[-1][3], rat[-1][4], rat[-1][1], rat[-1][2]))
    # 하한 7.8(ΔE76)이 ΔE2000 으로는 얼마인가 — 실측 비율로 환산
    med = rat[n//2][0]
    print("\n  게이트 하한 ΔE76 7.8 을 이 비율로 환산하면 ΔE2000 %.2f (중앙값 기준) / "
          "%.2f (최대비 기준)" % (7.8/med, 7.8/rat[-1][0]))
    # 7.8(76)을 통과하는데 00으로는 붙는 쌍
    bad = [r for r in rat if r[1] >= 7.8 and r[2] < 7.8]
    print("  ΔE76 ≥ 7.8 을 통과하면서 ΔE2000 < 7.8 인 쌍: %d / %d (%.1f%%)" % (len(bad), n, 100*len(bad)/n))
    if bad:
        bad.sort(key=lambda r: r[2])
        for r in bad[:6]:
            print("      %s ↔ %s : ΔE76 %.2f  ΔE2000 %.2f" % (r[3], r[4], r[1], r[2]))
    # R3 F-3 이 지목한 후보 3쌍 재현
    print("\n  R3 §2-3 이 F-3 으로 올린 후보 3쌍 (게이트는 통과, 눈으로는 같은 색):")
    for name, m, m2 in [('사이버','#009682','#3D9487'),('광부','#C96F00','#C07513'),('대마법사','#00994C','#269A47')]:
        print("      %-6s %s ↔ %s : ΔE76 %6.2f  ΔE2000 %5.2f  비율 %.2f"
              % (name, m, m2, de76(m,m2), de2000(m,m2), de76(m,m2)/de2000(m,m2)))
    return med

# ================================================================ §C 현행 팔레트의 ΔE2000 실적 (하한 유도용)
def sec_floor():
    hr("§C. 하한을 「이미 참인 것」에서 유도한다 — 현행 팔레트의 ΔE2000 실적")
    packs = {'오피스':('#456ECC','#6080CC'),'사이버':('#009682','#518D85'),'네온':('#CC1BA9','#9C5A8E'),
             '스포츠':('#CC3F29','#9E655C'),'컬러잉크':('#9768CC','#8563AB'),'밀리터리':('#639400','#798C51'),
             '광부':('#C96F00','#8D7251'),'대마법사':('#00994C','#518D6F')}
    prim = {k:v[0] for k,v in packs.items()}
    pairs = [(de2000(prim[a],prim[b]), de76(prim[a],prim[b]), a, b) for a,b in itertools.combinations(prim,2)]
    pairs.sort()
    print("  8팩 주색 28쌍 — 가족 하한:")
    for d00,d76,a,b in pairs[:5]:
        print("      %-10s ↔ %-10s ΔE2000 %6.2f   (ΔE76 %6.2f)" % (a,b,d00,d76))
    print("      ...  최대 %s↔%s ΔE2000 %.2f" % (pairs[-1][2],pairs[-1][3],pairs[-1][0]))
    print("  ⇒ 8팩 주색 ΔE2000 하한 = %.2f  (문서가 적은 ΔE76 하한 17.57 과 같은 쌍인가: %s)"
          % (pairs[0][0], "예" if {pairs[0][2],pairs[0][3]}=={'네온','컬러잉크'} else "아니오"))
    print()
    print("  팩 안쪽 M↔M2 (아이템 하나가 두 색을 쓴다):")
    inner = sorted((de2000(m,m2), de76(m,m2), k) for k,(m,m2) in packs.items())
    for d00,d76,k in inner:
        print("      %-10s ΔE2000 %6.2f  (ΔE76 %6.2f)  휘도대비 %.2f" % (k,d00,d76,contrast(packs[k][0],packs[k][1])))
    print("  ⇒ 아이템 안쪽 두 색의 ΔE2000 하한(현행 실적) = %.2f (%s)" % (inner[0][0], inner[0][2]))
    return pairs[0][0], inner[0][0]

# ================================================================ §D 왕관 — 등급 확인 + 색
def sec_crown():
    hr("§D. 왕관 파일럿 — 등급과 색")
    head = [('천모자 cap',0,1),('털모자 fur',1,5),('중절모 fedora',2,9),
            ('왕관 crown',3,20),('베레모 beret',4,23),('밀짚모자 straw',5,26)]
    ladder = ['일반','일반','희귀','희귀','영웅','전설']
    print("  HEAD 슬롯 등급 파생(ItemCatalog.RarityOfMember 재현 — requiredLevel 순위):")
    order = sorted(head, key=lambda t:(t[2], t[1]))
    for rank,(nm,idx,lv) in enumerate(order):
        star = "  ←★" if nm.startswith('왕관') else ""
        print("      rank %d  %-16s idx=%d requiredLevel=%2d  →  %s%s" % (rank,nm,idx,lv,ladder[rank],star))
    print("\n  ★★ 왕관은 **희귀**다. 전설은 밀짚모자다(requiredLevel 26).")
    print("     ⇒ 「왕관이니까 금색 = 전설색」은 이 게임에서 **거짓말**이 된다.")
    print()

    hl_naive = over_white(CROWN_M)      # 벡터 하이라이트(흰 α0.42)를 불투명으로 구운 값
    sh = shaded(CROWN_M)
    print("  왕관 현행 벡터 5색을 «불투명 비트맵 화소»로 구웠을 때:")
    print("  %-28s %-9s %8s %7s %9s %9s" % ("역할","hex","L","대역","배경4최악","무대2최악"))
    rows = [('M 금 (채움)', CROWN_M), ('M2 보석 (채움)', CROWN_M2),
            ('SH 그늘 M×0.28', sh), ('W 하이라이트 흰α0.42 사전합성', hl_naive)]
    for nm,hx in rows:
        print("  %-28s %-9s %8.4f %7s %9.2f %9.2f"
              % (nm,hx,relL(hx), "안" if in_band(hx) else "★밖", worst_bg(hx,BG4), worst_bg(hx,STAGE)))
    print()
    print("  ★ 하이라이트 %s 는 대역을 %.2f배 넘고(L %.4f vs 상한 %.4f), **크롬 대역(L>0.30)에 들어간다**."
          % (hl_naive, relL(hl_naive)/BAND_HI, relL(hl_naive), BAND_HI))
    print("     그 자리는 등급 램프의 자리다 — 실측:")
    for k,v in RAMP.items():
        print("        %s ↔ 등급 %-3s %s : ΔE2000 %5.2f  ΔE76 %6.2f" % (hl_naive,k,v,de2000(hl_naive,v),de76(hl_naive,v)))
    print("     ⇒ 벡터의 하이라이트 문법을 그대로 구우면 **희귀 왕관 안에 희귀·영웅 리본과 같은 색이 들어간다.**")
    return hl_naive, sh

# ================================================================ §E 창 평균 예산 (Tier 2 유도)
def sec_window():
    hr("§E. 「1 디바이스 픽셀 창의 평균」 예산 — 하이라이트/그늘이 몇 %까지 허용되는가")
    base = CROWN_M
    Lb = relL(base)
    print("  전제: 창 평균이 대역 안이어야 한다(그래야 몸에서 그 창이 배경과 3.0을 유지한다).")
    print("  기준 채움 %s L=%.4f · 대역 [%.4f, %.4f]" % (base, Lb, BAND_LO, BAND_HI))
    print()
    print("  ★ 다운샘플 평균은 선형광에서 나는가 sRGB 바이트에서 나는가 — 프로젝트 색공간에 달렸다.")
    print("    Linear 색공간 + sRGB 텍스처: 밉/필터가 **선형광**에서 평균 → 밝은 쪽이 더 세게 끌어올린다(불리).")
    print("    Gamma 색공간: **sRGB 바이트**에서 평균 → 덜 끌어올린다(유리).")
    print("    ⇒ **불리한 쪽(선형광)으로 예산을 잡는다.** 반대로 잡으면 Linear 프로젝트에서 조용히 새어나간다.")
    print()
    print("  하이라이트 휘도 Lh 를 넣었을 때 창의 최대 점유율 f(평균 ≤ %.4f):" % BAND_HI)
    print("  %-12s %-9s %10s %12s %14s" % ("Lh","hex(H43°)","f 상한","1024²에서 폭","256²에서 폭"))
    px1024 = 53.0   # 1 디바이스px(배율 0.75 @1080p)
    for Lh in [0.2396, 0.2600, 0.3000, 0.3500, 0.4485, 0.7000]:
        if Lh <= Lb: continue
        f = (BAND_HI - Lb) / (Lh - Lb)
        f = min(f, 1.0)
        # 같은 색상각(금 43°)의 대표 hex
        hx = None
        for v in range(1, 256):
            cand = hsv2hex(43.1, 0.78, v/255.0)
            if relL(cand) >= Lh: hx = cand; break
        print("  %-12.4f %-9s %9.1f%% %11.1f px %13.1f px"
              % (Lh, hx or '-', 100*f, f*px1024, f*px1024*256/1024))
    print()
    print("  ★ 읽는 법: 「Lh 0.30 · f 34.6%」 = 저작 1024² 캔버스에서 폭 18px 짜리 광택 띠까지는")
    print("     몸에서 창 평균이 대역 안에 남는다. 그 띠는 카드(1024²의 7.1px = 1 디바이스px)에서는 **3px**로 보인다.")
    print("     ⇒ **광택은 카드에서 보이고 몸에서는 평균으로 녹는다.** 이게 이 규격이 노리는 상태다.")
    print()
    # 그늘의 반대 방향
    Lsh = relL(shaded(CROWN_M))
    fdown = (Lb - BAND_LO) / (Lb - Lsh)
    print("  그늘 쪽(아래): SH %s L=%.4f 이면 창 평균 하한 %.4f 을 지키는 f 상한 = %.1f%%"
          % (shaded(CROWN_M), Lsh, BAND_LO, 100*fdown))
    print("     ⇒ 그늘은 창의 %.0f%% 까지 쓸 수 있다. **조형은 위(광택)가 아니라 아래(그늘)로 판다.**" % (100*fdown))
    # 지각 이동량
    def Lstar(Y): return 116*(Y**(1/3))-16 if Y > 216/24389 else (24389/27)*Y
    print("     L* 로: 채움 %.1f / 광택상한(L 0.30) %.1f (+%.1f) / 그늘 %.1f (−%.1f)"
          % (Lstar(Lb), Lstar(0.30), Lstar(0.30)-Lstar(Lb), Lstar(Lsh), Lstar(Lb)-Lstar(Lsh)))

# ================================================================ §F 크림/금 트림 — 비트맵에서는 자가 달라진다
def sec_cream():
    hr("§F. 「크림-금 트림」 재판정 — 비트맵은 WornColor 상자에서 풀려난다")
    target = '#F1E7C4'
    print("  R3 §2-2 의 결론: 색상각 30~65° 전탐색에서 5개 자를 전부 통과하는 색 54개,")
    print("  그중 %s 최근접이 #868C27 (ΔE2000 29.7) — 「금 트림은 이 팔레트 안에 없다」." % target)
    print("  실측 재현: ΔE2000(%s, #868C27) = %.2f  /  ΔE76 = %.2f" % (target, de2000(target,'#868C27'), de76(target,'#868C27')))
    print()
    print("  ★ 그런데 그 5개 자 중 둘은 **비트맵에 걸리지 않는다**:")
    print("     · WornColor 항등 — 스프라이트는 ItemCatalog.WornColor 를 **통과하지 않는다**")
    print("       (AccessoryDefSO.cs:「비트맵은 잉크색 전환·재질색(M/M2) 파생을 태울 수 없다」).")
    print("     · 채도 하한 S ≥ 0.42 — 그 하한은 WornColor 가 **강제하는 것**이지 대역의 성질이 아니다.")
    print()
    def search(target, sfloor, need_worn_identity, lo=BAND_LO, hi=BAND_HI, bg=BG4+STAGE, step=1):
        best = None
        tl = lab(target)
        for r in range(0,256,step):
            for g in range(0,256,step):
                for b in range(0,256,step):
                    hx = rgb2hex(r,g,b)
                    L = relL(hx)
                    if not (lo <= L <= hi): continue
                    h,s,v = rgb2hsv(hx)
                    if s < sfloor: continue
                    if need_worn_identity and worn_color(hx) != hx: continue
                    if min(contrast(hx,x) for x in bg) < 3.0: continue
                    d = de2000_lab(tl, lab(hx))
                    if best is None or d < best[0]: best = (d, hx)
        return best
    print("  전탐색(8비트 격자 전수, step=2):")
    for name, sf, wi in [("R3 조건(S≥0.42 + WornColor 항등)", 0.42, True),
                         ("비트맵 조건(채도 하한 없음, 항등 불필요)", 0.0, False)]:
        d, hx = search(target, sf, wi, step=2)
        h,s,v = rgb2hsv(hx)
        print("     %-38s 최근접 %s  ΔE2000 %5.2f  (L %.4f  H %.1f°  S %.2f  V %.2f)"
              % (name, hx, d, relL(hx), h, s, v))
        print("        %s 배경6종 최악 대비 %.2f" % (" "*38, worst_bg(hx,BG4+STAGE)))
    print()
    print("  ⇒ 결론: 「금 트림」은 비트맵에서도 **가장자리에는 못 온다**(대역이 값이 아니라 배경에서 나온다).")
    print("     그러나 R3 의 «ΔE2000 29.7» 은 비트맵에 **그대로 옮기면 안 된다** — 그 숫자는")
    print("     WornColor 상자 안에서 잰 것이고, 비트맵은 그 상자 밖에 있다.")

# ================================================================ §G 바깥 윤곽선 — 굽어도 되는가
def sec_outline():
    hr("§G. 실루엣 바깥 윤곽선을 구워도 되는가 — 부등식으로 답한다")
    print("  (1) 「대역 안에서 자기 윤곽선」은 **존재할 수 없다**:")
    lo_hi = (BAND_HI+0.05)/(BAND_LO+0.05)
    print("      대역 폭이 휘도로 %.4f~%.4f ⇒ 대역 안 임의 두 색의 최대 대비 = **%.3f : 1**"
          % (BAND_LO, BAND_HI, lo_hi))
    print("      선으로 읽히려면 최소 3.0 이 필요하다(MinNonTextContrast). %.3f < 3.0 ⇒ **공집합**." % lo_hi)
    print("      ⇒ 채움과 구분되는 윤곽선은 반드시 대역 **밖**이다. 그러면 그 윤곽선은")
    print("         어두운 바탕화면(또는 밝은 바탕화면) 한쪽에서 반드시 죽는다.")
    print()
    print("  (2) 그럼 바깥에 어두운 윤곽선을 구우면 얼마나 나쁜가 — 가장자리 창 평균으로 잰다:")
    Lfill = relL(CROWN_M); Lline = relL('#111111')
    fmax = (Lfill - BAND_LO)/(Lfill - Lline)
    px1024 = 53.0
    print("      채움 %s L=%.4f · 윤곽 #111111 L=%.4f" % (CROWN_M, Lfill, Lline))
    print("      가장자리 1 디바이스px 창의 평균이 대역 하한 %.4f 밑으로 내려가지 않을 조건:" % BAND_LO)
    print("        f < %.4f  ⇒ 저작 1024²에서 윤곽 두께 < **%.1f px** (캔버스 폭의 %.2f%%)"
          % (fmax, fmax*px1024, 100*fmax*px1024/1024))
    for w in [8, 12, 16, 24, 32, 53]:
        f = min(w/px1024, 1.0)
        Lm = f*Lline + (1-f)*Lfill
        c_dark = (max(Lm, relL('#1E1E1E'))+0.05)/(min(Lm, relL('#1E1E1E'))+0.05)
        print("        윤곽 %2dpx(1024²) → 가장자리 창 평균 L %.4f  · 어두운 바탕화면 대비 %.2f %s"
              % (w, Lm, c_dark, "" if c_dark>=3.0 else "★ 미달"))
    print()
    print("  ⇒ 판정: **바깥 윤곽선을 굽지 않는다.** 채움 색이 곧 윤곽이다(대역이 그것을 보장한다).")
    print("     윤곽선이 필요한 곳은 **안쪽**뿐이다 — 배경이 「자기 아이템의 채움」인 자리(R-5 와 같은 논리).")

# ================================================================ §H 왕관 확정 팔레트
def sec_crown_palette():
    hr("§H. 왕관 파일럿 확정 팔레트 — 5색")
    H, S = rgb2hsv(CROWN_M)[0], rgb2hsv(CROWN_M)[1]
    print("  금 %s 의 HSV = H %.2f° S %.3f V %.3f" % (CROWN_M, *rgb2hsv(CROWN_M)))
    # 광택: 같은 색상각, L 을 해자 상한 0.30 바로 아래로. 등급 램프와 ΔE2000 ≥ 6.5 요구.
    best = None
    for v in range(120, 256):
        for s100 in range(30, 101):
            hx = hsv2hex(H, s100/100.0, v/255.0)
            L = relL(hx)
            if not (BAND_HI < L <= MOAT_HI): continue
            dmin = min(de2000(hx, r) for r in RAMP.values())
            if dmin < 6.5: continue
            score = (L, dmin)
            if best is None or score > best[0]: best = (score, hx, L, dmin)
    _, HL, HLl, HLd = best
    MID = rgb2hex(*[c*0.62 for c in hex2rgb(CROWN_M)])     # PALETTE_SPEC 그늘선 계수 0.62
    SH  = shaded(CROWN_M)
    table = [
        ('W  광택 (안쪽 전용)', HL,        '광택 띠 · 관테 윗면'),
        ('M  금 채움 = 실루엣', CROWN_M,   '몸통 · 뾰족단 · 가장자리 전부'),
        ('MD 중간 그늘 (M×0.62)', MID,     '곡률 · 관테 아랫면'),
        ('SH 깊은 그늘 (M×0.28)', SH,      '안쪽 뒷벽 · 보석 밑그림자'),
        ('M2 보석 (안쪽 전용)', CROWN_M2,  '보석 3개'),
    ]
    print()
    print("  %-24s %-9s %8s %6s %8s %8s %8s %8s" %
          ("역할","hex","L","대역","배경4","무대2","잉크2","등급ΔE00"))
    for nm, hx, _ in table:
        d = min(de2000(hx, r) for r in RAMP.values())
        print("  %-24s %-9s %8.4f %6s %8.2f %8.2f %8.2f %8.2f"
              % (nm, hx, relL(hx), "안" if in_band(hx) else "밖",
                 worst_bg(hx,BG4), worst_bg(hx,STAGE), worst_bg(hx,['#111111','#FFFFFF']), d))
    print()
    print("  ★ 실루엣을 지는 것은 M %s 하나다 — 8개 배경 전부에서:" % CROWN_M)
    for nm, bg in BG.items():
        print("      %-24s 대비 %5.2f %s" % (nm, contrast(CROWN_M,bg), "" if contrast(CROWN_M,bg)>=3.0 else "★"))
    print()
    print("  아이템 안쪽 변별(ΔE2000) — 인접해서 칠해지는 쌍만:")
    for a,b in [('W','M'),('M','MD'),('MD','SH'),('M','M2'),('M2','SH')]:
        hx = dict(W=HL, M=CROWN_M, MD=MID, SH=SH, M2=CROWN_M2)
        print("      %-3s ↔ %-3s  %s ↔ %s : ΔE2000 %6.2f  휘도대비 %.2f"
              % (a,b,hx[a],hx[b],de2000(hx[a],hx[b]),contrast(hx[a],hx[b])))
    print()
    # 전체 평균(T2) 모의 — 면적 배분 두 가지
    for name, mix in [("보수적 배분", [(HL,0.06),(CROWN_M,0.62),(MID,0.16),(SH,0.09),(CROWN_M2,0.07)]),
                      ("광택 많은 배분", [(HL,0.14),(CROWN_M,0.52),(MID,0.18),(SH,0.09),(CROWN_M2,0.07)]),
                      ("그늘 많은 배분", [(HL,0.05),(CROWN_M,0.50),(MID,0.22),(SH,0.16),(CROWN_M2,0.07)])]:
        Lm = sum(relL(h)*f for h,f in mix)            # 선형광 평균(불리한 쪽)
        rgbm = [sum(hex2rgb(h)[i]*f for h,f in mix) for i in range(3)]
        hm = rgb2hex(*rgbm)
        print("  T2 전체 평균 [%s] : 선형광 L %.4f (%s) · sRGB평균색 %s (L %.4f, %s) · 카탈로그 M 과 ΔE2000 %.2f"
              % (name, Lm, "대역 안" if BAND_LO<=Lm<=BAND_HI else "★대역 밖",
                 hm, relL(hm), "대역 안" if in_band(hm) else "★대역 밖", de2000(hm, CROWN_M)))
    return HL, MID, SH

# ================================================================ §I 평균 모델 두 가지 — 어느 쪽이 무는가
def mean_linear(mix):
    """선형광에서 평균 (Linear 색공간 프로젝트의 밉/필터)."""
    r = sum(srgb_to_lin(hex2rgb(h)[0]/255)*f for h,f in mix)
    g = sum(srgb_to_lin(hex2rgb(h)[1]/255)*f for h,f in mix)
    b = sum(srgb_to_lin(hex2rgb(h)[2]/255)*f for h,f in mix)
    return 0.2126*r+0.7152*g+0.0722*b, rgb2hex(lin_to_srgb(r)*255, lin_to_srgb(g)*255, lin_to_srgb(b)*255)

def mean_srgb(mix):
    """sRGB 바이트에서 평균 (Gamma 색공간 프로젝트의 밉/필터)."""
    hx = rgb2hex(*[sum(hex2rgb(h)[i]*f for h,f in mix) for i in range(3)])
    return relL(hx), hx

def sec_colorspace():
    hr("§I. 평균이 어디서 나는가 — 프로젝트 색공간 실측 + 두 모델 동시 요구")
    print("  실측: ProjectSettings/ProjectSettings.asset:50  m_ActiveColorSpace: 0  ⇒ **Gamma**")
    print("        (UnityEngine.ColorSpace: Gamma=0 / Linear=1) ⇒ 지금은 밉·필터가 **sRGB 바이트**에서 평균한다.")
    print()
    mixes = {'보수적': [('#BB8E1C',0.06),(CROWN_M,0.62),('#604B15',0.16),('#2B220A',0.09),(CROWN_M2,0.07)],
             '광택많음': [('#BB8E1C',0.14),(CROWN_M,0.52),('#604B15',0.18),('#2B220A',0.09),(CROWN_M2,0.07)],
             '그늘많음': [('#BB8E1C',0.05),(CROWN_M,0.50),('#604B15',0.22),('#2B220A',0.16),(CROWN_M2,0.07)]}
    print("  %-10s %-24s %-24s" % ("배분","선형광 평균(Linear 프로젝트)","sRGB 평균(Gamma 프로젝트 ← 현행)"))
    for k, mix in mixes.items():
        Ll, hl = mean_linear(mix); Ls, hs = mean_srgb(mix)
        print("  %-10s %s L %.4f %-6s   %s L %.4f %-6s"
              % (k, hl, Ll, "안" if BAND_LO<=Ll<=BAND_HI else "★밖",
                 hs, Ls, "안" if BAND_LO<=Ls<=BAND_HI else "★밖"))
    print()
    print("  ★ 두 모델은 **서로 반대 방향으로 틀린다**: 선형광 평균이 더 밝고 sRGB 평균이 더 어둡다.")
    print("     ⇒ 밝은 쪽 상한은 Linear 가, 어두운 쪽 하한은 Gamma 가 **먼저 문다.**")
    print("     ⇒ 규격은 **두 모델 모두**에서 통과할 것을 요구한다(색공간을 바꾸면 조용히 새는 것을 막는다).")

# ================================================================ §J 그늘/광택 면적 예산 표 (저작자용)
def sec_budget():
    hr("§J. 저작 예산 표 — 「그늘을 몇 % 칠할 수 있는가」")
    base = CROWN_M
    print("  질문 형태: 채움 %s 위에 그늘색 X 를 면적 f 로 칠했을 때, 전체 평균이 대역에 남는가." % base)
    print("  판정은 두 모델 모두에서. (나머지 면적 = 채움)")
    print()
    print("  %-22s %8s %10s %10s %10s" % ("그늘색","L","Gamma 한계f","Linear 한계f","채택 한계f"))
    cands = [('SH  M×0.28 #2B220A', shaded(CROWN_M)),
             ('MD  M×0.62 #604B15', rgb2hex(*[c*0.62 for c in hex2rgb(CROWN_M)])),
             ('MD2 M×0.78 #79601A', rgb2hex(*[c*0.78 for c in hex2rgb(CROWN_M)])),
             ('대역하한 금 #8B6C1F', None)]
    # 대역 하한에 정확히 앉는 같은 색상각 금
    H,S,_ = rgb2hsv(CROWN_M)
    band_floor_gold = None
    for v in range(1,256):
        hx = hsv2hex(H,S,v/255.0)
        if relL(hx) >= BAND_LO: band_floor_gold = hx; break
    cands[-1] = ('대역하한 금 %s' % band_floor_gold, band_floor_gold)
    for nm, hx in cands:
        fg = fl = 1.0
        for f in [i/1000 for i in range(1001)]:
            mix = [(hx,f),(base,1-f)]
            if mean_srgb(mix)[0] < BAND_LO: fg = min(fg, f); break
        for f in [i/1000 for i in range(1001)]:
            mix = [(hx,f),(base,1-f)]
            if mean_linear(mix)[0] < BAND_LO: fl = min(fl, f); break
        print("  %-22s %8.4f %9.1f%% %9.1f%% %9.1f%%" % (nm, relL(hx), 100*fg, 100*fl, 100*min(fg,fl)))
    print()
    print("  %-22s %8s %10s %10s %10s" % ("광택색","L","Gamma 한계f","Linear 한계f","채택 한계f"))
    hi_c = []
    for v in range(1,256):
        hx = hsv2hex(H,S,v/255.0)
        L = relL(hx)
        if L > BAND_HI: hi_c.append((L,hx))
    for want in [0.2600, 0.2999, 0.3500, 0.4485]:
        hx = min((abs(L-want), h) for L,h in hi_c)[1] if hi_c else None
        if want > 0.31:      # 같은 색상각으로 못 만들면 흰 혼합으로
            hx = over_white(CROWN_M, 0.0)
            for a in [i/100 for i in range(101)]:
                if relL(over_white(CROWN_M,a)) >= want: hx = over_white(CROWN_M,a); break
        fg = fl = 1.0
        for f in [i/1000 for i in range(1001)]:
            mix = [(hx,f),(base,1-f)]
            if mean_srgb(mix)[0] > BAND_HI: fg = min(fg,f); break
        for f in [i/1000 for i in range(1001)]:
            mix = [(hx,f),(base,1-f)]
            if mean_linear(mix)[0] > BAND_HI: fl = min(fl,f); break
        print("  %-22s %8.4f %9.1f%% %9.1f%% %9.1f%%" % (hx, relL(hx), 100*fg, 100*fl, 100*min(fg,fl)))
    print()
    print("  ⇒ 그늘은 밝은 쪽보다 **훨씬 빨리 문다**. 「깊은 그늘 SH」는 면적 예산이 한 자릿수%다.")
    print("     조형이 요구하는 그늘 면적이 그보다 크면 **그늘색을 밝게** 하는 것이 답이다(면적을 줄이는 것이 아니라).")

# ================================================================ §K 왕관 면적 배분 확정
def sec_alloc():
    hr("§K. 왕관 면적 배분 — 두 모델 모두 통과하는 배분을 실제로 찾는다")
    W, M, MD, SH, M2 = '#BB8E1C', CROWN_M, '#604B15', '#2B220A', CROWN_M2
    print("  탐색: W/MD/SH/M2 면적을 1% 격자로 훑어 두 평균 모델이 **모두** 대역 안인 배분을 찾는다.")
    ok = []
    for fw in range(0, 21):
        for fmd in range(0, 31):
            for fsh in range(0, 21):
                for fm2 in [5, 7, 9]:
                    fm = 100 - fw - fmd - fsh - fm2
                    if fm < 35: continue
                    mix = [(W,fw/100),(M,fm/100),(MD,fmd/100),(SH,fsh/100),(M2,fm2/100)]
                    Ll,_ = mean_linear(mix); Ls,_ = mean_srgb(mix)
                    if BAND_LO<=Ll<=BAND_HI and BAND_LO<=Ls<=BAND_HI:
                        ok.append((fw+fmd+fsh, fw, fm, fmd, fsh, fm2, Ll, Ls))
    ok.sort(reverse=True)
    print("  통과 배분 %d개. 「조형이 가장 진한」 것(비-채움 면적 최대) 상위 5:" % len(ok))
    print("  %5s %5s %5s %5s %5s %5s %10s %10s" % ("비채움","W","M","MD","SH","M2","Linear L","Gamma L"))
    for r in ok[:5]:
        print("  %4d%% %4d%% %4d%% %4d%% %4d%% %4d%% %10.4f %10.4f" % (r[0],r[1],r[2],r[3],r[4],r[5],r[6],r[7]))
    print()
    rec = (10, 55, 20, 8, 7)
    mix = [(W,rec[0]/100),(M,rec[1]/100),(MD,rec[2]/100),(SH,rec[3]/100),(M2,rec[4]/100)]
    Ll,hl = mean_linear(mix); Ls,hs = mean_srgb(mix)
    print("  ★ 권고 배분 W %d%% / M %d%% / MD %d%% / SH %d%% / M2 %d%%" % rec)
    print("     Linear 평균 %s L %.4f (%s) · Gamma 평균 %s L %.4f (%s)"
          % (hl,Ll,"안" if BAND_LO<=Ll<=BAND_HI else "★밖", hs,Ls,"안" if BAND_LO<=Ls<=BAND_HI else "★밖"))
    print("     Gamma 평균색 %s ↔ 카탈로그 M %s : ΔE2000 %.2f (이펙트·상세패널이 읽는 색과의 거리)"
          % (hs, CROWN_M, de2000(hs, CROWN_M)))
    print("     Gamma 평균색 배경 8종 최악 대비 %.2f" % worst_bg(hs, list(BG.values())))
    for k,v in RAMP.items():
        print("     Gamma 평균색 ↔ 등급 %-3s %s : ΔE2000 %5.2f" % (k,v,de2000(hs,v)))
    return rec, hs

# ================================================================ §L 팩 정체성 — 평균이 팩 색으로 남는가
def sec_pack():
    hr("§L. 6팩(+2) 테마 통일 — 비트맵 전환이 팩 식별을 흔드는가")
    packs = {'오피스':('#456ECC','#6080CC'),'사이버':('#009682','#518D85'),'네온':('#CC1BA9','#9C5A8E'),
             '스포츠':('#CC3F29','#9E655C'),'컬러잉크':('#9768CC','#8563AB'),'밀리터리':('#639400','#798C51'),
             '광부':('#C96F00','#8D7251'),'대마법사':('#00994C','#518D6F')}
    print("  모형: 팩 아이템 한 장이 W(광택) 10% / M 55% / MD(M×0.62) 20% / SH(M×0.28) 8% / M2 7% 로 칠해진다")
    print("        (§K 권고 배분과 같은 배분 — 화법이 팩마다 다르면 「한 게임」이 아니다).")
    print()
    print("  %-8s %-9s %-9s %8s %8s %10s %10s" % ("팩","주색M","Gamma평균","평균L","대역","ΔE00→M","평균 색상각차"))
    means = {}
    for k,(M,M2) in packs.items():
        H,S,V = rgb2hsv(M)
        W = None
        for v in range(1,256):
            for s100 in range(30,101):
                hx = hsv2hex(H, s100/100.0, v/255.0)
                if BAND_HI < relL(hx) <= MOAT_HI and min(de2000(hx,r) for r in RAMP.values()) >= 6.5:
                    if W is None or relL(hx) > relL(W): W = hx
        MD = rgb2hex(*[c*0.62 for c in hex2rgb(M)])
        SH = rgb2hex(*[c*0.28 for c in hex2rgb(M)])
        mix = [(W,0.10),(M,0.55),(MD,0.20),(SH,0.08),(M2,0.07)]
        Ls,hs = mean_srgb(mix); Ll,_ = mean_linear(mix)
        means[k] = hs
        dh = abs(rgb2hsv(hs)[0]-H); dh = min(dh, 360-dh)
        print("  %-8s %-9s %-9s %8.4f %8s %10.2f %9.1f°"
              % (k, M, hs, Ls, ("안" if BAND_LO<=Ls<=BAND_HI and BAND_LO<=Ll<=BAND_HI else "★밖"),
                 de2000(hs,M), dh))
    print()
    pairs = sorted((de2000(means[a],means[b]), a, b) for a,b in itertools.combinations(means,2))
    prim = sorted((de2000(packs[a][0],packs[b][0]), a, b) for a,b in itertools.combinations(packs,2))
    print("  8팩 «비트맵 평균» 28쌍 가족 하한 : ΔE2000 %.2f  (%s ↔ %s)" % (pairs[0][0], pairs[0][1], pairs[0][2]))
    print("  8팩 «카탈로그 주색»  28쌍 가족 하한 : ΔE2000 %.2f  (%s ↔ %s)" % (prim[0][0], prim[0][1], prim[0][2]))
    print("  ⇒ 변화 %+.2f" % (pairs[0][0]-prim[0][0]))
    print()
    print("  이펙트 색과의 이음매(§13-7): 착지 먼지·오라·파쿠르 파티클은 **카탈로그 M/M2 를 그대로** 쓴다.")
    print("  장비가 비트맵 평균으로 보이므로 둘 사이 거리가 곧 「같은 팩으로 읽히는가」다:")
    for k,(M,M2) in packs.items():
        print("      %-8s 평균 %s ↔ 이펙트 M %s : ΔE2000 %5.2f   ↔ 이펙트 M2 %s : ΔE2000 %5.2f"
              % (k, means[k], M, de2000(means[k],M), M2, de2000(means[k],M2)))

# ================================================================ §M 회색조·색각
def sec_gray():
    hr("§M. 회색조 · 색각 — 비트맵이 새로 만드는 축이 있는가")
    pal = {'W 광택':'#BB8E1C','M 금':CROWN_M,'MD':'#604B15','SH':'#2B220A','M2 보석':CROWN_M2}
    print("  왕관 5색의 회색조 휘도 순서(회색조에서 형태가 남는가):")
    for k,v in sorted(pal.items(), key=lambda kv:-relL(kv[1])):
        print("      %-8s %s  L %.4f" % (k,v,relL(v)))
    print()
    print("  M(금) ↔ M2(보석) 은 색상으로만 갈린다 — 휘도 대비 %.2f (회색조에서 붙는다)."
          % contrast(CROWN_M, CROWN_M2))
    print("  ⇒ EQUIPMENT_PALETTE §13-5-(5) 의 처방(«M↔M2 경계에는 INK 윤곽»)이 비트맵에서는 못 쓰인다")
    print("     (잉크 윤곽은 유저 색을 따라가야 하는데 비트맵은 못 따라간다).")
    print("     **대체 처방: M↔M2 경계에 SH(그늘) 1 디바이스px 를 넣는다** — 실측:")
    print("      M ↔ SH  휘도대비 %.2f   M2 ↔ SH 휘도대비 %.2f  ⇒ 회색조에서도 경계가 선다."
          % (contrast(CROWN_M,'#2B220A'), contrast(CROWN_M2,'#2B220A')))

# ================================================================ §N T2' — 「평균 = 기준색」으로 규칙을 세운다
def derive_W(M):
    """같은 색상각에서 해자 상한(L<=0.30) 안, 등급 램프와 ΔE2000>=6.5 인 가장 밝은 광택색."""
    H,S,_ = rgb2hsv(M); best = None
    for v in range(1,256):
        for s100 in range(20,101):
            hx = hsv2hex(H, s100/100.0, v/255.0)
            L = relL(hx)
            if not (BAND_HI < L <= MOAT_HI): continue
            if min(de2000(hx,r) for r in RAMP.values()) < 6.5: continue
            if best is None or L > relL(best): best = hx
    return best

def sec_t2():
    hr("§N. 【핵심 규칙】 T2′ — 「평균이 곧 기준색」. 그늘 예산은 상수가 아니라 기준색의 함수다")
    packs = {'오피스':'#456ECC','사이버':'#009682','네온':'#CC1BA9','스포츠':'#CC3F29',
             '컬러잉크':'#9768CC','밀리터리':'#639400','광부':'#C96F00','대마법사':'#00994C',
             '(왕관)금':CROWN_M, '(왕관)보석':CROWN_M2}
    print("  왜 「대역 안」만으로는 부족한가 — 기준색이 대역 어디에 앉아 있느냐로 예산이 6배 갈린다:")
    print("  %-12s %-9s %8s %14s %14s" % ("아이템/팩","기준색 M","L","대역 바닥까지","대역 천장까지"))
    for k,M in packs.items():
        L = relL(M)
        print("  %-12s %-9s %8.4f %13.4f %13.4f" % (k,M,L,L-BAND_LO,BAND_HI-L))
    print()
    print("  ⇒ 네온(#CC1BA9)·스포츠(#CC3F29)·오피스(#456ECC)·왕관 보석(#C6443C)은 **바닥에 앉아 있다**.")
    print("     그 아이템에 그늘을 칠하면 평균이 즉시 대역 밖으로 떨어진다. 「그늘 N%」 같은 상수 규칙은")
    print("     이 팔레트에서 **성립하지 않는다.**")
    print()
    print("  T2′ : 스프라이트의 α가중 평균색이 **기준색 M 자신**으로 돌아와야 한다")
    print("        (두 평균 모델 모두에서 대역 안 + ΔE2000(평균, M) ≤ 6.5).")
    print("        ⇒ 그늘을 칠한 만큼 광택을 칠한다. **조형은 기준색 둘레에서 대칭으로 판다.**")
    print()
    print("  각 기준색에 대해: 그늘 SH=M×0.28 을 f_sh 칠했을 때 필요한 광택 f_w (Gamma 모델 기준)")
    print("  %-12s %-9s %-9s %8s %8s %8s %8s" % ("아이템/팩","M","W(광택)","f_sh 8%","f_sh 15%","f_sh 22%","f_sh 30%"))
    for k,M in packs.items():
        W = derive_W(M); SH = rgb2hex(*[c*0.28 for c in hex2rgb(M)])
        row = []
        for fsh in [0.08,0.15,0.22,0.30]:
            sol = None
            for fw in [i/1000 for i in range(0,601)]:
                fm = 1-fsh-fw
                if fm < 0.2: break
                mix = [(W,fw),(M,fm),(SH,fsh)]
                Ls,hs = mean_srgb(mix); Ll,_ = mean_linear(mix)
                if (BAND_LO<=Ls<=BAND_HI and BAND_LO<=Ll<=BAND_HI
                        and de2000(hs,M) <= 6.5 and de2000(mean_linear(mix)[1],M) <= 6.5):
                    sol = fw; break
            row.append("%.0f%%" % (100*sol) if sol is not None else "불가")
        print("  %-12s %-9s %-9s %8s %8s %8s %8s" % (k,M,W,*row))
    print()
    print("  ★ 「불가」는 그 그늘 면적에서는 **어떤 광택량으로도 평균을 되돌릴 수 없다**는 뜻이다")
    print("     (광택의 천장이 해자 상한 L 0.30 이기 때문 — 그 위는 등급 크롬의 자리다).")

def sec_alloc2():
    hr("§O. 왕관 확정 배분 (T2′ 통과) + 전 항목 검산")
    W, M, MD, SH, M2 = derive_W(CROWN_M), CROWN_M, rgb2hex(*[c*0.62 for c in hex2rgb(CROWN_M)]), shaded(CROWN_M), CROWN_M2
    print("  왕관 5색: W %s / M %s / MD %s / SH %s / M2 %s" % (W,M,MD,SH,M2))
    best = None
    for fw in range(4, 26):
        for fmd in range(4, 31):
            for fsh in range(2, 16):
                for fm2 in [7]:
                    fm = 100-fw-fmd-fsh-fm2
                    if fm < 30: continue
                    mix = [(W,fw/100),(M,fm/100),(MD,fmd/100),(SH,fsh/100),(M2,fm2/100)]
                    Ls,hs = mean_srgb(mix); Ll,hlin = mean_linear(mix)
                    if not (BAND_LO<=Ls<=BAND_HI and BAND_LO<=Ll<=BAND_HI): continue
                    d = max(de2000(hs,M), de2000(hlin,M))
                    if d > 6.5: continue
                    form = fw+fmd+fsh          # 조형 면적
                    if best is None or (form, -d) > (best[0], -best[1]):
                        best = (form, d, fw, fm, fmd, fsh, fm2, hs, hlin, Ls, Ll)
    form,d,fw,fm,fmd,fsh,fm2,hs,hlin,Ls,Ll = best
    print("  ★ 조형 면적을 최대로 하면서 T2′ 를 지키는 배분:")
    print("      W %d%% / M %d%% / MD %d%% / SH %d%% / M2 %d%%   (조형 면적 %d%%)" % (fw,fm,fmd,fsh,fm2,form))
    print("      Gamma 평균 %s L %.4f · Linear 평균 %s L %.4f · max ΔE2000(평균,M) %.2f" % (hs,Ls,hlin,Ll,d))
    print("      Gamma 평균 배경 8종 최악 대비 %.2f" % worst_bg(hs, list(BG.values())))
    print("      Gamma 평균 ↔ 등급 램프 최소 ΔE2000 %.2f" % min(de2000(hs,r) for r in RAMP.values()))
    return (W,M,MD,SH,M2),(fw,fm,fmd,fsh,fm2)

# ================================================================ §P 처방 — 「면적을 줄이지 말고 그늘을 밝게」
def sec_shade_k():
    hr("§P. 처방 — 그늘 면적을 줄이는 대신 **그늘 계수 k 를 올린다**")
    packs = {'오피스':'#456ECC','사이버':'#009682','네온':'#CC1BA9','스포츠':'#CC3F29',
             '컬러잉크':'#9768CC','밀리터리':'#639400','광부':'#C96F00','대마법사':'#00994C',
             '(왕관)금':CROWN_M,'(왕관)보석':CROWN_M2}
    print("  조건: 그늘 30% + 광택 10% + 채움 60% 라는 **넉넉한 조형 배분**을 고정하고,")
    print("        그늘색 SH = M × k 의 k 를 올려 T2′(평균 = 기준색)를 만족시키는 최소 k 를 구한다.")
    print("        (현행 벡터의 AccessoryTone.ShadeFactor = 0.28)")
    print()
    print("  %-12s %-9s %-9s %7s %-9s %10s %8s" % ("아이템/팩","M","W(광택)","최소 k","SH = M×k","Gamma평균","ΔE00→M"))
    for nm,M in packs.items():
        W = derive_W(M)
        found = None
        for k100 in range(28, 101):
            k = k100/100.0
            SH = rgb2hex(*[c*k for c in hex2rgb(M)])
            mix = [(W,0.10),(M,0.60),(SH,0.30)]
            Ls,hs = mean_srgb(mix); Ll,hlin = mean_linear(mix)
            if (BAND_LO<=Ls<=BAND_HI and BAND_LO<=Ll<=BAND_HI
                    and max(de2000(hs,M), de2000(hlin,M)) <= 6.5):
                found = (k, SH, hs, max(de2000(hs,M), de2000(hlin,M))); break
        if found:
            k,SH,hs,d = found
            print("  %-12s %-9s %-9s %7.2f %-9s %10s %8.2f" % (nm,M,W,k,SH,hs,d))
        else:
            print("  %-12s %-9s %-9s %7s" % (nm,M,W,"불가"))
    print()
    print("  ⇒ **k = 0.28(현행 벡터 계수)로 30% 그늘을 칠할 수 있는 기준색은 하나도 없다.**")
    print("     비트맵의 그늘 계수는 아이템마다 다르고, 기준색이 대역 바닥에 앉을수록 1.0 에 가까워진다")
    print("     (= 그늘을 거의 못 판다. 그런 아이템은 **형태와 광택**으로 입체를 만든다).")

# ================================================================ §Q 왕관 권고 배분(여유 포함)
def sec_alloc3():
    W, M, MD, SH, M2 = derive_W(CROWN_M), CROWN_M, rgb2hex(*[c*0.62 for c in hex2rgb(CROWN_M)]), shaded(CROWN_M), CROWN_M2
    hr("§Q. 왕관 권고 배분 — 여유를 남긴다(가장자리 배분은 실기에서 무너진다)")
    cands = []
    for fw in range(4, 22):
        for fmd in range(6, 29):
            for fsh in range(2, 13):
                fm2 = 7
                fm = 100-fw-fmd-fsh-fm2
                if fm < 35: continue
                mix = [(W,fw/100),(M,fm/100),(MD,fmd/100),(SH,fsh/100),(M2,fm2/100)]
                Ls,hs = mean_srgb(mix); Ll,hlin = mean_linear(mix)
                if not (BAND_LO<=Ls<=BAND_HI and BAND_LO<=Ll<=BAND_HI): continue
                d = max(de2000(hs,M), de2000(hlin,M))
                if d > 6.5: continue
                margin = min(Ls-BAND_LO, BAND_HI-Ls, Ll-BAND_LO, BAND_HI-Ll)
                cands.append((margin, fw,fm,fmd,fsh,fm2,d,hs,Ls,hlin,Ll))
    cands.sort(reverse=True)
    print("  대역 여유(두 모델 중 최악)를 최대로 하는 배분 상위 3:")
    print("  %8s %5s %5s %5s %5s %5s %8s %10s %10s" % ("여유L","W","M","MD","SH","M2","ΔE00","Gamma L","Linear L"))
    for c in cands[:3]:
        print("  %8.4f %4d%% %4d%% %4d%% %4d%% %4d%% %8.2f %10.4f %10.4f" % (c[0],c[1],c[2],c[3],c[4],c[5],c[6],c[8],c[10]))
    print()
    # 조형면적 >= 35% 중 여유 최대
    sub = [c for c in cands if c[1]+c[3]+c[4] >= 35]
    sub.sort(reverse=True)
    c = sub[0]
    print("  ★ 권고 — 「조형 면적 ≥ 35%」 안에서 여유가 가장 큰 배분:")
    print("      W %d%% / M %d%% / MD %d%% / SH %d%% / M2 %d%%  (조형 %d%%)" % (c[1],c[2],c[3],c[4],c[5],c[1]+c[3]+c[4]))
    print("      Gamma 평균 %s L %.4f · Linear 평균 %s L %.4f" % (c[7],c[8],c[9],c[10]))
    print("      대역 여유(최악) %.4f · ΔE2000(평균, M) %.2f" % (c[0], c[6]))
    print("      Gamma 평균 배경 8종 최악 대비 %.2f · 등급 램프 최소 ΔE2000 %.2f"
          % (worst_bg(c[7], list(BG.values())), min(de2000(c[7],r) for r in RAMP.values())))
    return c

# ================================================================ §S 실무 배분 후보 + 왕관 기하
def sec_practical():
    hr("§S. 왕관 실무 배분 후보 5개 + 「몸에서 무엇이 형태로 남는가」")
    W,M,M2 = derive_W(CROWN_M), CROWN_M, CROWN_M2
    MD = rgb2hex(*[c*0.62 for c in hex2rgb(M)]); SH = shaded(M)
    opts = [("A 광택 위주",  [(W,0.20),(M,0.60),(MD,0.10),(SH,0.03),(M2,0.07)]),
            ("B 균형(권고)", [(W,0.21),(M,0.58),(MD,0.12),(SH,0.02),(M2,0.07)]),
            ("C 그늘 위주",  [(W,0.10),(M,0.60),(MD,0.23),(SH,0.00),(M2,0.07)]),
            ("D 그늘 최대",  [(W,0.20),(M,0.43),(MD,0.28),(SH,0.02),(M2,0.07)]),
            ("E 광택 없음",  [(W,0.00),(M,0.75),(MD,0.15),(SH,0.03),(M2,0.07)]),
            ("F ★벡터 그대로", [(W,0.06),(M,0.62),(MD,0.16),(SH,0.09),(M2,0.07)])]
    print("  %-14s %10s %10s %8s %9s %s" % ("배분","Gamma L","Linear L","ΔE00→M","배경최악","판정"))
    for nm, mix in opts:
        Ls,hs = mean_srgb(mix); Ll,hl = mean_linear(mix)
        d = max(de2000(hs,M), de2000(hl,M)); wb = worst_bg(hs, list(BG.values()))
        good = BAND_LO<=Ls<=BAND_HI and BAND_LO<=Ll<=BAND_HI and d<=6.5 and wb>=3.0
        print("  %-14s %10.4f %10.4f %8.2f %9.2f  %s" % (nm,Ls,Ll,d,wb,"통과" if good else "★탈락"))
    print()
    print("  ★★ F 는 **현행 벡터의 색 배분을 그대로 불투명 화소로 구운 것**이고 **탈락한다**.")
    print("     (Gamma 평균 L 0.1537 < 대역 하한 0.1632 · 배경 최악 2.87 < 3.0)")
    print("     ⇒ 「벡터 규칙을 그대로 옮기면 된다」는 **거짓이다.** 그늘 예산이 비트맵에서 새로 걸린다.")
    print()
    for surf, px, src in [("몸 @0.75 1080p",19.3,1024),("몸 @0.35 1080p",9.0,1024),
                          ("초상 무대 ≈50pt @2x",100,1024),("카드 72pt @2x",144,1024)]:
        print("  %-20s 1 디바이스px = 저작 %6.1f px · 형태로 읽히는 최소 조각(2px) = %6.1f px (캔버스 %.1f%%)"
              % (surf, src/px, 2*src/px, 200/px))
    print()
    print("  왕관 실측 기하(카드 아이콘 40 viewBox: 폭 34.4 · 봉우리 3 · 붉은 밑띠 두께 2.11):")
    for nm, frac in [("봉우리 하나 11.5/34.4",11.5/34.4),("골 폭 7.37/34.4",7.37/34.4),
                     ("붉은 밑띠 2.11/34.4",2.11/34.4)]:
        print("      %-22s 캔버스 %5.1f%% → 몸@0.75 %4.1fpx · 몸@0.35 %4.1fpx · 카드@2x %5.1fpx"
              % (nm, 100*frac, frac*19.3, frac*9.0, frac*144))
    print("  ⇒ 봉우리는 살고(6.5px), 골도 산다(4.1px). **붉은 밑띠는 1.2px — 형태가 아니라 색조로만 남는다.**")
    print("     ⇒ 보석/밑띠 같은 M2 조각은 「몸에서 보이게」가 아니라 「평균을 붉게 미는 양」으로 설계한다.")

# ================================================================ 진입점
def main():
    if not calib():
        print("★ 교정 실패 — 이후 판정 전부 폐기"); sys.exit(2)
    if not control():
        print("★ 양성 대조 실패 — 이후 판정 전부 폐기"); sys.exit(3)
    sec_downsample(); sec_metric(); sec_floor(); sec_crown()
    sec_window(); sec_cream(); sec_outline(); sec_crown_palette()
    sec_colorspace(); sec_budget(); sec_alloc(); sec_pack(); sec_gray()
    sec_t2(); sec_alloc2(); sec_shade_k(); sec_alloc3(); sec_practical()
    hr("§R. 잉크 실측 — 문서가 쓰던 #111111 대신 실제 값으로 재확인")
    print("  StickConfig.primaryOutlineColor = (0,0,0) = #000000 · whiteInkColor = (1,1,1) = #FFFFFF")
    print("  DefaultStickConfig.asset:259-260 실측. 문서들이 쓰던 #111111 은 보수적 대용값이었다(실제는 더 어둡다 = 더 안전).")
    for nm, hx in [('W 광택','#BB8E1C'),('M 금',CROWN_M),('MD','#604B15'),('SH','#2B220A'),('M2 보석',CROWN_M2)]:
        print("      %-8s %s : 검은 잉크 %5.2f · 흰 잉크 %5.2f" % (nm,hx,contrast(hx,'#000000'),contrast(hx,'#FFFFFF')))
    print()
    print("  왕관 뒷층(SH #2B220A)이 바탕화면에 직접 노출될 때 — §11-(5) 리더 판정 L-6 을 재측정:")
    for nm,bg in BG.items():
        print("      %-24s %5.2f" % (nm, contrast('#2B220A',bg)))

if __name__ == '__main__':
    main()
