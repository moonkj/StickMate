#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""빌드 캡처 실측 — final-full.png 대 확정 팔레트.

역할 규칙: 계산기는 알려진 값으로 먼저 교정한다(흰/검 21.0, 동일색 1.0).
교정이 깨지면 그 뒤 숫자는 전부 폐기한다.
"""
import sys, math
from PIL import Image

SHOT = "/tmp/stickmate-run/screenshots/final-full.png"

# ---------- 색 도구 ----------
def srgb_lin(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def lum(rgb):
    r, g, b = (srgb_lin(float(x)) for x in rgb[:3])
    return 0.2126 * r + 0.7152 * g + 0.0722 * b

def cr(a, b):
    la, lb = lum(a), lum(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)

def to_xyz(rgb):
    r, g, b = (srgb_lin(float(x)) for x in rgb[:3])
    return (0.4124*r+0.3576*g+0.1805*b, 0.2126*r+0.7152*g+0.0722*b, 0.0193*r+0.1192*g+0.9505*b)

def to_lab(rgb):
    x, y, z = to_xyz(rgb)
    xn, yn, zn = 0.95047, 1.0, 1.08883
    def f(t):
        return t ** (1/3) if t > 216/24389 else (841/108) * t + 4/29
    fx, fy, fz = f(x/xn), f(y/yn), f(z/zn)
    return (116*fy - 16, 500*(fx-fy), 200*(fy-fz))

def de76(a, b):
    la, lb = to_lab(a), to_lab(b)
    return math.sqrt(sum((la[i]-lb[i])**2 for i in range(3)))

def hexc(h):
    return ((h >> 16) & 255, (h >> 8) & 255, h & 255)

def sh(rgb):
    return "#%02X%02X%02X" % tuple(int(v) for v in rgb[:3])

# ---------- 교정 ----------
def calibrate():
    checks = [
        ("흰/검 = 21.0", round(cr((255,255,255), (0,0,0)), 4), 21.0),
        ("동일색 = 1.0", round(cr((200,161,90), (200,161,90)), 4), 1.0),
        ("흰/흰 = 1.0", round(cr((255,255,255), (255,255,255)), 4), 1.0),
        ("동일색 ΔE = 0", round(de76((200,161,90), (200,161,90)), 4), 0.0),
        ("흰 L* = 100", round(to_lab((255,255,255))[0], 3), 100.0),
        ("검 L* = 0", round(to_lab((0,0,0))[0], 3), 0.0),
    ]
    ok = True
    print("=== 교정 (알려진 값) ===")
    for name, got, want in checks:
        good = abs(got - want) < 0.005
        ok &= good
        print("  %-16s %-9s (기대 %s)  %s" % (name, got, want, "OK" if good else "FAIL"))
    # 알려진 프로덕션 값 재현 검산(PALETTE_SPEC §28)
    know = cr(hexc(0x9C978C), hexc(0x1B1F26))
    good = abs(know - 5.68) < 0.01
    ok &= good
    print("  %-16s %-9s (기대 5.68)  %s" % ("일반/카드면", round(know, 4), "OK" if good else "FAIL"))
    print("  교정 판정: %s\n" % ("유효" if ok else "무효 — 이하 숫자 폐기"))
    return ok

# ---------- 확정 팔레트 ----------
RAMP = [("일반", 0x9C978C), ("희귀", 0xBCAC8B), ("영웅", 0xDEC081), ("전설", 0xFFD375)]
BRASS = 0xC8A15A
CARD = 0x1B1F26
TRACK = 0x3A4049
WORN_BLUE = 0x5DA1F5

def patch(im, cx, cy, r=3):
    """(cx,cy) 중심 (2r+1)^2 중앙값 — 안티에일리어싱/서브픽셀 흔들림 제거."""
    px = im.load()
    vals = []
    for dy in range(-r, r+1):
        for dx in range(-r, r+1):
            vals.append(px[cx+dx, cy+dy][:3])
    vals.sort(key=lambda c: lum(c))
    return vals[len(vals)//2]

def brightest(im, box):
    """box=(x0,y0,x1,y1) 안에서 가장 밝은 픽셀(리본/테두리 심지 찾기)."""
    px = im.load()
    best, bl = None, -1
    for y in range(box[1], box[3]):
        for x in range(box[0], box[2]):
            c = px[x, y][:3]
            l = lum(c)
            if l > bl:
                bl, best = l, c
    return best

def main():
    if not calibrate():
        sys.exit(1)
    im = Image.open(SHOT).convert("RGB")
    W, H = im.size
    print("캡처: %dx%d  (%s)\n" % (W, H, SHOT))

    S = W / 2000.0   # 표시 좌표 → 원본 좌표 배율
    def P(x, y):
        return (int(round(x*S)), int(round(y*S)))
    def B(x0, y0, x1, y1):
        return (int(x0*S), int(y0*S), int(x1*S), int(y1*S))

    print("배율 S = %.4f (표시 2000px 기준)\n" % S)

    # --- 1. 브라스 악센트가 실제로 브라스인가 ---
    print("=== 1. 브라스 악센트 실측 ===")
    spots = [
        ("탭 '장비' 활성 필", B(556, 148, 606, 178)),
        ("'해제' 버튼 면",   B(1200, 498, 1320, 526)),
        ("'모자' 섹션 틱",   B(1134, 236, 1143, 258)),
        ("'착용 중' 글자",   B(1160, 442, 1205, 458)),
    ]
    for name, box in spots:
        c = brightest(im, box)
        print("  %-16s %s  ΔE(브라스)=%6.2f  CR(카드면)=%5.2f"
              % (name, sh(c), de76(c, hexc(BRASS)), cr(c, hexc(CARD))))

    # --- 2. 등급 리본 6장 ---
    print("\n=== 2. 카드 상단 등급 리본 (좌→우, 위→아래) ===")
    # 표시 좌표에서 읽은 카드 리본 띠 대략 위치
    cards = [
        ("1 천모자 · 일반 · 착용중", B(1150, 262, 1370, 276), "일반"),
        ("2 ???  · 일반 · 잠김",     B(1412, 262, 1630, 276), "일반"),
        ("3 ???  · 희귀 · 잠김",     B(1150, 562, 1370, 576), "희귀"),
        ("4 ???  · 희귀 · 잠김",     B(1412, 562, 1630, 576), "희귀"),
        ("5 ???  · 영웅 · 잠김",     B(1150, 852, 1370, 866), "영웅"),
        ("6 ???  · 전설 · 잠김",     B(1412, 852, 1630, 866), "전설"),
    ]
    names = dict(RAMP)
    got = {}
    for label, box, want in cards:
        c = brightest(im, box)
        got[label] = c
        w = hexc(names[want])
        # 램프 4색 중 최근접
        near = min(RAMP, key=lambda kv: de76(c, hexc(kv[1])))
        print("  %-26s %s  ΔE(%s 기대)=%6.2f  최근접=%s(ΔE %.2f)  CR(카드면)=%5.2f"
              % (label, sh(c), want, de76(c, w), near[0], de76(c, hexc(near[1])), cr(c, hexc(CARD))))

    # --- 3. 일반 리본끼리 vs 일반 리본 대 트랙(빈 칸) ---
    print("\n=== 3. 「일반」과 「잠김」이 헷갈리는가 ===")
    c1 = got["1 천모자 · 일반 · 착용중"]
    c2 = got["2 ???  · 일반 · 잠김"]
    print("  카드1(일반·착용) vs 카드2(일반·잠김) 리본 ΔE = %.2f  → 같은 등급이므로 작아야 정상" % de76(c1, c2))
    trk = patch(im, *P(1330, 269))   # 카드2 리본 오른쪽 빈 칸 쪽
    print("  카드2 리본 빈칸 추정 %s  ΔE(트랙 #3A4049)=%.2f  채움/빈칸 CR=%.2f"
          % (sh(trk), de76(trk, hexc(TRACK)), cr(c2, trk)))

    # --- 4. 착용 테두리 색 (파랑 예약이 살아 있는가) ---
    print("\n=== 4. 착용 중 카드 테두리 ===")
    edge = brightest(im, B(1136, 300, 1142, 500))
    print("  카드1 좌측 테두리 %s  ΔE(파랑 #5DA1F5)=%.2f  ΔE(브라스)=%.2f"
          % (sh(edge), de76(edge, hexc(WORN_BLUE)), de76(edge, hexc(BRASS))))

    # --- 5. 등급 라벨 글자 (일반/희귀/영웅/전설) ---
    print("\n=== 5. 등급 라벨 글자색 ===")
    labels = [
        ("일반(카드1)", B(1338, 410, 1370, 426), "일반"),
        ("일반(카드2)", B(1600, 410, 1632, 426), "일반"),
        ("희귀(카드3)", B(1330, 701, 1366, 717), "희귀"),
        ("영웅(카드5)", B(1330, 991, 1366, 1007), "영웅"),
        ("전설(카드6)", B(1592, 991, 1632, 1007), "전설"),
    ]
    for name, box, want in labels:
        c = brightest(im, box)
        near = min(RAMP, key=lambda kv: de76(c, hexc(kv[1])))
        print("  %-12s %s  ΔE(%s)=%6.2f  최근접=%s  CR(카드면)=%5.2f"
              % (name, sh(c), want, de76(c, hexc(names[want])), near[0], cr(c, hexc(CARD))))

    # --- 6. 확정 램프 자체의 인접 변별 (참조) ---
    print("\n=== 6. 참조: 확정 램프 인접 ΔE / 휘도 단조 ===")
    for i in range(3):
        a, b = RAMP[i], RAMP[i+1]
        print("  %s→%s  ΔE=%6.2f  L*: %.1f → %.1f"
              % (a[0], b[0], de76(hexc(a[1]), hexc(b[1])),
                 to_lab(hexc(a[1]))[0], to_lab(hexc(b[1]))[0]))
    print("  일반↔전설 ΔE = %.2f (핸드오프 제약판은 채널차 37이었다)"
          % de76(hexc(RAMP[0][1]), hexc(RAMP[3][1])))

main()
