#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Ø36 축소 폴백 — [오늘 할일] 배지 숫자가 «읽히는가»를 대비(contrast)로 판정한다.

왜 «번짐 %»가 아니라 대비인가
------------------------------
1차 계측에서 양성 대조가 실패했고, 그 실패가 물리를 알려 줬다:
GPU 4-tap 바이리니어로 0.818배 축소하면 재구성 필터(2텍셀 폭)가 출력 화소 간격(1.222텍셀)보다
<좁아서> 앨리어싱이 난다 — 균일한 «블러»가 아니라 <획마다 농도가 들쭉날쭉해진다>.
즉 «번짐 몇 %»는 이 현상을 재는 자가 아니다. 실제로 나타나는 손해는
<획의 최대 알파가 255에 못 미치는 것>이고, 그것은 곧 <글자색이 배경 쪽으로 끌려가는 것>이다.

그리고 그 손해는 design-art 관할의 확정 기준으로 바로 잴 수 있다:
  UiChrome.MinTextContrast = 4.5   (본 리포지터리 확정 하한)
  배지 = Accent 면 위 OnAccentSolid 글자, 설계 대비 7.91 (ART_FAN_MENU_LANGUAGE 표)

알파 a 로 그려진 글자의 실효색 = lerp(면색, 글자색, a). 그 대비를 잰다.
"""
import sys
from PIL import Image, ImageDraw, ImageFont

FONT_BOLD = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"

# --- 프로덕션 실측 상수 (베끼지 않고 출처를 적는다) --------------------------
ACCENT = (0.784, 0.631, 0.353)          # UiChrome.cs:227  Accent
ON_ACCENT = (0.043, 0.063, 0.086)       # UiChrome.cs:386  OnAccentSolid
MIN_TEXT_CONTRAST = 4.5                 # UiChrome.cs:1355 MinTextContrast
BUTTON_D, SHRUNK_D = 44.0, 36.0         # GearRadialMenuWidget.cs:212-213
K = SHRUNK_D / BUTTON_D
BADGE_PT = 10                           # GearRadialMenuWidget.cs:2524 AddText(..., 10, ...)
GLYPHS = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "9+"]


def lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def luminance(rgb):
    r, g, b = (lin(c) for c in rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a, b):
    la, lb = luminance(a), luminance(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def blend(bg, fg, alpha):
    return tuple(bg[i] + (fg[i] - bg[i]) * alpha for i in range(3))


def gpu_bilinear(src, out_w, out_h):
    sw, sh = src.size
    sp = src.load()
    dst = Image.new("L", (out_w, out_h)); dp = dst.load()
    sxs, sys_ = sw / out_w, sh / out_h
    for y in range(out_h):
        v = (y + 0.5) * sys_ - 0.5
        y0 = int(v // 1); fy = v - y0
        y0c = min(max(y0, 0), sh - 1); y1c = min(max(y0 + 1, 0), sh - 1)
        for x in range(out_w):
            u = (x + 0.5) * sxs - 0.5
            x0 = int(u // 1); fx = u - x0
            x0c = min(max(x0, 0), sw - 1); x1c = min(max(x0 + 1, 0), sw - 1)
            a = sp[x0c, y0c] * (1 - fx) + sp[x1c, y0c] * fx
            b = sp[x0c, y1c] * (1 - fx) + sp[x1c, y1c] * fx
            dp[x, y] = int(round(a * (1 - fy) + b * fy))
    return dst


# ===========================================================================
# 교정 — 알려진 값. 하나라도 깨지면 아래 숫자 전부 폐기.
# ===========================================================================
def calibrate():
    ok = True
    c_wb = contrast((1, 1, 1), (0, 0, 0))
    print(f"  교정1 흰/검 대비 = {c_wb:.4f}  (기대 21.0000)")
    ok &= abs(c_wb - 21.0) < 1e-4

    c_same = contrast(ACCENT, ACCENT)
    print(f"  교정2 동일색 대비 = {c_same:.4f}  (기대 1.0000)")
    ok &= abs(c_same - 1.0) < 1e-6

    c_badge = contrast(ACCENT, ON_ACCENT)
    print(f"  교정3 배지 설계 대비(Accent vs OnAccentSolid) = {c_badge:.2f}  "
          f"(문서 ART_FAN_MENU_LANGUAGE 표 = 7.91)")
    ok &= abs(c_badge - 7.91) < 0.05

    # 리샘플러 항등
    src = Image.new("L", (17, 17)); ImageDraw.Draw(src).rectangle([4, 0, 6, 16], fill=255)
    md = max(abs(src.load()[x, y] - gpu_bilinear(src, 17, 17).load()[x, y])
             for y in range(17) for x in range(17))
    print(f"  교정4 리샘플러 항등(k=1) 최대 화소차 = {md}  (기대 0)")
    ok &= md == 0

    # ★ 양성 대조 — 알파가 떨어지면 대비도 떨어져야 한다(지표가 살아 있는가).
    c_full = contrast(ACCENT, blend(ACCENT, ON_ACCENT, 1.0))
    c_half = contrast(ACCENT, blend(ACCENT, ON_ACCENT, 0.5))
    print(f"  교정5 양성대조: 알파1.0 -> {c_full:.2f} · 알파0.5 -> {c_half:.2f}  "
          f"(기대 감소) = {c_half < c_full}")
    ok &= c_half < c_full
    return ok


def txt(px, ch, pad=8):
    n = max(1, int(round(px)))
    f = ImageFont.truetype(FONT_BOLD, n)
    im = Image.new("L", (n * 3 + pad * 2, n * 3 + pad * 2))
    ImageDraw.Draw(im).text((pad, pad), ch, font=f, fill=255)
    return im


def peak_alpha(im):
    return max(im.getdata()) / 255.0


def main():
    print("=" * 100)
    print("교정 (알려진 값)")
    print("=" * 100)
    if not calibrate():
        print("\n★ 교정 실패 — 아래 숫자 전부 폐기."); sys.exit(2)
    print("  => 교정 통과.\n")

    # 대비 4.5 를 지키기 위해 필요한 최소 알파를 먼저 푼다.
    lo, hi = 0.0, 1.0
    for _ in range(60):
        mid = (lo + hi) / 2
        if contrast(ACCENT, blend(ACCENT, ON_ACCENT, mid)) < MIN_TEXT_CONTRAST:
            lo = mid
        else:
            hi = mid
    alpha_floor = hi
    print("=" * 100)
    print(f"배지 글자가 MinTextContrast {MIN_TEXT_CONTRAST} 를 지키려면 알파 >= {alpha_floor:.4f} "
          f"({alpha_floor*255:.0f}/255) 여야 한다.")
    print("=" * 100)
    print()

    cases = [("Win 100% / 비Retina", 1.0), ("Win 125%", 1.25),
             ("Win 150% (신고 실기)", 1.5), ("Win 175%", 1.75), ("macOS Retina 2x", 2.0)]

    print(f"{'환경':<21}{'S':>5}{'굽기':>7}{'화면em':>9} | "
          f"{'(a) Ø36 축소폴백':^30} | {'(c) 현행 Ø44':^24}")
    print(f"{'':<21}{'':>5}{'':>7}{'':>9} | {'최저peak':>10}{'대비':>9}{'판정':>9} | "
          f"{'최저peak':>10}{'대비':>9}{'판정':>5}")
    print("-" * 100)

    summary = []
    for name, S in cases:
        baked = int(round(BADGE_PT * S))
        shown = BADGE_PT * S * K
        pa, pc = [], []
        for ch in GLYPHS:
            src = txt(baked, ch)
            w, h = src.size
            pa.append(peak_alpha(gpu_bilinear(src, max(1, round(w * K)), max(1, round(h * K)))))
            pc.append(peak_alpha(src))
        amin, cmin = min(pa), min(pc)
        ca = contrast(ACCENT, blend(ACCENT, ON_ACCENT, amin))
        cc = contrast(ACCENT, blend(ACCENT, ON_ACCENT, cmin))
        va = "통과" if ca >= MIN_TEXT_CONTRAST else "★미달"
        vc = "통과" if cc >= MIN_TEXT_CONTRAST else "★미달"
        print(f"{name:<19}{S:>5.2f}{baked:>6}px{shown:>8.2f}px | "
              f"{amin:>10.3f}{ca:>9.2f}{va:>9} | {cmin:>10.3f}{cc:>9.2f}{vc:>7}")
        summary.append((name, S, shown, amin, ca, cmin, cc))

    print()
    print("=" * 100)
    print("글자별 최저 알파 상세 (축소 폴백)")
    print("=" * 100)
    print(f"{'S':>5} | " + " ".join(f"{g:>5}" for g in GLYPHS))
    for name, S in cases:
        baked = int(round(BADGE_PT * S))
        row = []
        for ch in GLYPHS:
            src = txt(baked, ch)
            w, h = src.size
            row.append(peak_alpha(gpu_bilinear(src, max(1, round(w * K)), max(1, round(h * K)))))
        print(f"{S:>5.2f} | " + " ".join(f"{v:>5.2f}" for v in row))


if __name__ == "__main__":
    main()
