#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
design-art — UI 알파 비침(alpha bleed) 실측기.

이 앱은 전체화면 투명 오버레이이고, uGUI 기본 셰이더가
    Blend SrcAlpha OneMinusSrcAlpha        (RGB와 알파에 같이 적용)
    => dstA' = srcA*srcA + dstA*(1-srcA)
로 섞으므로, dstA=0(빈 프레임버퍼) 위에 α짜리 판 하나를 그리면 화면 알파는 α²다.
즉 비침(바탕화면이 보이는 비율) = 1 - α².

교정(calibration)부터 한다 — 알려진 값으로 계산기가 맞는지 먼저 확인하고,
깨지면 그 뒤 숫자는 전부 폐기한다(CLAUDE.md / design-art 규칙).
"""

import math
import os
import re
import sys

# ----------------------------------------------------------------------------
# WCAG 2.2 — relative luminance / contrast ratio
# https://www.w3.org/TR/WCAG22/#dfn-relative-luminance
# ----------------------------------------------------------------------------

def lin(c):
    c = min(1.0, max(0.0, c))
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def L(rgb):
    r, g, b = rgb
    return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)

def CR(a, b):
    la, lb = L(a), L(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)

def flatten(over_rgb, alpha, onto_rgb):
    """UiChrome.Flatten — 반투명 over를 불투명 onto 위에 얹은 α=1 단색."""
    return tuple(onto_rgb[i] + (over_rgb[i] - onto_rgb[i]) * alpha for i in range(3))

def show_through(alpha):
    """이 앱의 블렌드에서 α짜리 판 하나를 빈(알파 0) 프레임버퍼에 그렸을 때의 비침 비율."""
    return 1.0 - alpha * alpha

def hexc(rgb):
    return "#%02X%02X%02X" % tuple(int(round(min(1.0, max(0.0, c)) * 255)) for c in rgb)

def from_hex(h):
    h = h.lstrip("#")
    return (int(h[0:2], 16) / 255.0, int(h[2:4], 16) / 255.0, int(h[4:6], 16) / 255.0)

def de76(a, b):
    """CIE76 ΔE (sRGB D65). 이 저장소의 다른 실측기와 같은 식."""
    def to_xyz(rgb):
        r, g, b_ = (lin(rgb[0]), lin(rgb[1]), lin(rgb[2]))
        x = r * 0.4124 + g * 0.3576 + b_ * 0.1805
        y = r * 0.2126 + g * 0.7152 + b_ * 0.0722
        z = r * 0.0193 + g * 0.1192 + b_ * 0.9505
        return x, y, z
    def f(t):
        return t ** (1.0 / 3.0) if t > 216.0 / 24389.0 else (841.0 / 108.0) * t + 4.0 / 29.0
    def to_lab(rgb):
        x, y, z = to_xyz(rgb)
        xn, yn, zn = 0.95047, 1.00000, 1.08883
        fx, fy, fz = f(x / xn), f(y / yn), f(z / zn)
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz))
    la, lb = to_lab(a), to_lab(b)
    return math.sqrt(sum((la[i] - lb[i]) ** 2 for i in range(3)))


# ----------------------------------------------------------------------------
# 0. 교정 — 알려진 값이 안 나오면 즉시 중단
# ----------------------------------------------------------------------------
def calibrate():
    ok = True
    checks = [
        ("흰 vs 검 = 21.00", CR((1, 1, 1), (0, 0, 0)), 21.0, 0.005),
        ("동일색 = 1.00", CR((0.3, 0.4, 0.5), (0.3, 0.4, 0.5)), 1.0, 1e-9),
        ("중간회색 #767676 vs 흰 = 4.54", CR(from_hex("767676"), (1, 1, 1)), 4.54, 0.01),
        ("ΔE(동일) = 0", de76((0.2, 0.3, 0.4), (0.2, 0.3, 0.4)), 0.0, 1e-9),
        ("ΔE(흰,검) = 100.0", de76((1, 1, 1), (0, 0, 0)), 100.0, 0.05),
        ("비침(α=1) = 0%", show_through(1.0), 0.0, 1e-12),
        ("비침(α=0) = 100%", show_through(0.0), 1.0, 1e-12),
        ("비침(α=0.55) = 69.75%", show_through(0.55), 0.6975, 1e-9),
        ("Flatten(흰 α1 onto 검) = 흰", L(flatten((1, 1, 1), 1.0, (0, 0, 0))), 1.0, 1e-9),
        ("Flatten(흰 α0 onto 검) = 검", L(flatten((1, 1, 1), 0.0, (0, 0, 0))), 0.0, 1e-9),
    ]
    print("=" * 78)
    print("0. 교정 — 알려진 값")
    print("=" * 78)
    for name, got, want, tol in checks:
        good = abs(got - want) <= tol
        ok = ok and good
        print("  %-38s got %-12.6f want %-10.4f %s" % (name, got, want, "OK" if good else "FAIL"))
    if not ok:
        print("\n교정 실패 — 이후 숫자는 전부 폐기한다.")
        sys.exit(1)
    print("  → 교정 통과. 아래 숫자를 신뢰한다.\n")


# ----------------------------------------------------------------------------
# 1. UiChrome 토큰 (프로덕션 소스에서 직접 파싱 — 값을 손으로 베끼지 않는다)
# ----------------------------------------------------------------------------
ROOT = "/Users/kjmoon/App/StickMate"
UICHROME = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/UiChrome.cs")

TOKEN_RE = re.compile(
    r"public\s+static\s+readonly\s+Color\s+(\w+)\s*=\s*new\s+Color\(\s*"
    r"([0-9.]+)f\s*,\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)")

def load_tokens():
    src = open(UICHROME, encoding="utf-8").read()
    out = {}
    for m in TOKEN_RE.finditer(src):
        name = m.group(1)
        r, g, b, a = (float(m.group(i)) for i in (2, 3, 4, 5))
        out[name] = ((r, g, b), a)
    return out


def main():
    calibrate()
    T = load_tokens()

    print("=" * 78)
    print("1. UiChrome의 α<1 토큰 전수 (프로덕션 소스 파싱)")
    print("=" * 78)
    print("  %-22s %-10s %-8s %-9s" % ("토큰", "rgb", "α", "비침"))
    translucent = []
    for name, (rgb, a) in sorted(T.items(), key=lambda kv: kv[1][1]):
        if a >= 1.0:
            continue
        translucent.append((name, rgb, a))
        print("  %-22s %-10s %-8.2f %6.2f%%" % (name, hexc(rgb), a, show_through(a) * 100))
    print("  → α<1 토큰 %d종\n" % len(translucent))

    PANEL = T["PanelSurface"][0]
    CARD = T["CardSurface"][0]
    MUTED = T["CardSurfaceMuted"][0]
    SUBTLE = T["SubtleSurface"][0]
    LOCKED = T["ThumbSurfaceLocked"][0]

    backdrops = [("PanelSurface", PANEL), ("CardSurface", CARD),
                 ("CardSurfaceMuted", MUTED), ("SubtleSurface", SUBTLE),
                 ("ThumbSurfaceLocked", LOCKED)]

    print("=" * 78)
    print("2. Flatten 등가 불투명색 + 그 바탕 대비 (비텍스트 하한 3.00 / 목표 3.60)")
    print("=" * 78)
    for name, rgb, a in translucent:
        print("  %s (α%.2f)" % (name, a))
        for bname, brgb in backdrops:
            f = flatten(rgb, a, brgb)
            cr = CR(f, brgb)
            mark = "OK " if cr >= 3.0 else ("... " if cr >= 1.5 else "XX ")
            print("      on %-19s -> %s  대비 %5.2f  %s" % (bname, hexc(f), cr, mark))
        print("")

    # ------------------------------------------------------------------
    # 3. 설정창 컨트롤 면 — ButtonSurfaceOnCard / OutlineOnCard
    # ------------------------------------------------------------------
    print("=" * 78)
    print("3. 설정창 컨트롤 면 — 현재값 실측")
    print("=" * 78)
    cb_rgb, cb_a = T["CardBorder"]
    for bname, brgb in (("CardSurface", CARD), ("PanelSurface", PANEL)):
        face = flatten(cb_rgb, cb_a, brgb)
        print("  ButtonSurfaceOn%-6s = Flatten(CardBorder α%.2f, %s) = %s  면 대비 %.2f"
              % (bname.replace("Surface", ""), cb_a, bname, hexc(face), CR(face, brgb)))
    pb_rgb, pb_a = T["PanelBorder"]
    for bname, brgb in (("CardSurface", CARD), ("PanelSurface", PANEL)):
        edge = flatten(pb_rgb, pb_a, brgb)
        print("  OutlineOn%-6s        = Flatten(PanelBorder α%.2f, %s) = %s  테두리 대비 %.2f"
              % (bname.replace("Surface", ""), pb_a, bname, hexc(edge), CR(edge, brgb)))
    print("")

    # 목표 대비를 내는 흰색 α 해 찾기(격자 1/1024 — UiChrome.ControlFaceOnSurface와 같은 폭)
    print("  목표 대비별 필요한 흰색 α (Flatten 후 등가색) :")
    print("  %-18s %-8s %-8s %-9s %-8s %-8s" % ("바탕", "목표", "필요α", "등가색", "실대비", "T1잉크"))
    T1 = T["TextPrimary"][0]
    T2 = T["TextSecondary"][0]
    T3 = T["TextTertiary"][0]
    ONACC = T["OnAccentSolid"][0]
    for bname, brgb in (("CardSurface", CARD), ("PanelSurface", PANEL)):
        for target in (3.00, 3.30, 3.60, 4.50, 5.00):
            found = None
            for i in range(1025):
                a = i / 1024.0
                f = flatten((1, 1, 1), a, brgb)
                if CR(f, brgb) >= target:
                    found = (a, f, CR(f, brgb))
                    break
            if found:
                a, f, cr = found
                inks = [("T1", T1), ("T2", T2), ("T3", T3), ("OnAccent", ONACC)]
                best = max(inks, key=lambda kv: CR(kv[1], f))
                print("  %-18s %-8.2f %-8.4f %-9s %-8.2f %s %.2f"
                      % (bname, target, a, hexc(f), cr, best[0], CR(best[1], f)))
    print("")

    # 잉크 붕괴점: 면이 밝아질수록 T1/T2/T3이 언제 4.5를 놓치는가
    print("  면을 밝힐 때 사다리 잉크가 4.50을 놓치는 지점 :")
    for bname, brgb in (("CardSurface", CARD), ("PanelSurface", PANEL)):
        for iname, ink in (("T1 TextPrimary", T1), ("T2 TextSecondary", T2), ("T3 TextTertiary", T3)):
            lost = None
            for i in range(1025):
                a = i / 1024.0
                f = flatten((1, 1, 1), a, brgb)
                if CR(ink, f) < 4.5:
                    lost = (a, f, CR(f, brgb))
                    break
            if lost:
                a, f, cr = lost
                print("      %-18s on %-13s : α %.4f 부터 미달 (그때 면 대비 %.2f, 면 %s)"
                      % (iname, bname, a, cr, hexc(f)))
            else:
                print("      %-18s on %-13s : 끝까지 유지" % (iname, bname))
    print("")

    # ------------------------------------------------------------------
    # 4. 리터럴 new Color(1,1,1,0.18) — 액자 테두리
    # ------------------------------------------------------------------
    print("=" * 78)
    print("4. 리터럴 new Color(1f,1f,1f,0.18f) — 흰 잉크 시 액자 테두리")
    print("=" * 78)
    CHARCOAL = T["InkContrastCharcoal"][0]
    PAPER = T["PortraitSurface"][0]
    for bname, brgb in (("InkContrastCharcoal(흰잉크)", CHARCOAL), ("PortraitSurface(검잉크)", PAPER)):
        f = flatten((1, 1, 1), 0.18, brgb)
        print("  Flatten(흰 α0.18, %-26s) = %s  대비 %.2f" % (bname, hexc(f), CR(f, brgb)))
    print("  raw α0.18 비침 = %.2f%%" % (show_through(0.18) * 100))
    # 검은 잉크 쪽(종이 바탕)에서 지금은 무슨 색을 쓰는지도 계산
    print("")

    # ------------------------------------------------------------------
    # 5. 코너 안티에일리어싱 램프 — Flatten vs raw 비교(대수적 모형)
    # ------------------------------------------------------------------
    print("=" * 78)
    print("5. 둥근 코너 AA 램프 — raw vs Flatten (바탕화면 D 위)")
    print("=" * 78)
    print("  본체(α=1, 스프라이트 커버리지 a) 위에 테두리(같은 커버리지 a)를 얹는다.")
    print("  raw    : lerp(lerp(D,P,a), W, k*a)        (k = 토큰 α)")
    print("  flat   : lerp(lerp(D,P,a), F, a)          (F = Flatten(W k, P))")
    for k in (0.16, 0.10):
        print("\n  k = %.2f  (PanelBorder / CardBorder)" % k)
        print("  %-6s %-10s %-10s %-10s %-10s %-10s" %
              ("a", "D계수raw", "D계수flat", "dstA raw", "dstA flat", "비침차"))
        for a in (0.1, 0.25, 0.5, 0.75, 0.9, 1.0):
            d_raw = (1 - a) * (1 - k * a)
            d_flat = (1 - a) ** 2
            # 알파 채널: 본체 후 dstA = a^2 (dstA0 = 0)
            base = a * a
            sa_raw = k * a
            sa_flat = a
            dA_raw = sa_raw ** 2 + base * (1 - sa_raw)
            dA_flat = sa_flat ** 2 + base * (1 - sa_flat)
            print("  %-6.2f %-10.4f %-10.4f %-10.4f %-10.4f %+.2f%%"
                  % (a, d_raw, d_flat, dA_raw, dA_flat, (dA_flat - dA_raw) * 100))
    print("\n  ※ D계수 = 최종 화소에 섞이는 바탕화면 비율. 낮을수록 덜 비친다.")
    print("  ※ dstA = OS 합성기가 보는 마스크. 높을수록 덜 비친다.")

    # 색 차이(코너 램프에서 raw와 flat이 얼마나 다르게 보이는가) — 흰/검/중간 바탕화면 3종
    print("\n  코너 램프에서 raw와 flat의 겉보기 색차 ΔE (바탕화면 3종):")
    W = (1.0, 1.0, 1.0)
    for k, tok in ((0.16, "PanelBorder"), (0.10, "CardBorder")):
        F = flatten(W, k, PANEL)
        for dname, D in (("흰 바탕화면", (1, 1, 1)), ("중간회색", (0.5, 0.5, 0.5)), ("검 바탕화면", (0, 0, 0))):
            worst = 0.0
            worst_a = 0.0
            for i in range(1, 100):
                a = i / 100.0
                body = tuple(D[j] + (PANEL[j] - D[j]) * a for j in range(3))
                raw = tuple(body[j] + (W[j] - body[j]) * (k * a) for j in range(3))
                flat = tuple(body[j] + (F[j] - body[j]) * a for j in range(3))
                d = de76(raw, flat)
                if d > worst:
                    worst, worst_a = d, a
            print("      %-12s %-12s 최대 ΔE %6.2f (a=%.2f)" % (tok, dname, worst, worst_a))
    print("  ※ 변별 하한 ΔE 7.8과 비교할 것. 램프는 1px 폭이라 면적 가중은 더 작다.")

    # ------------------------------------------------------------------
    # 6. 부채꼴 메뉴 — AccentBorder/AccentSurface가 무엇 위에 얹히는가
    # ------------------------------------------------------------------
    print("\n" + "=" * 78)
    print("6. 부채꼴 버튼 — 등가색 후보")
    print("=" * 78)
    ACC = T["Accent"][0]
    ab_rgb, ab_a = T["AccentBorder"]
    as_rgb, as_a = T["AccentSurface"]
    for bname, brgb in (("PanelSurface", PANEL), ("CardSurface", CARD)):
        fa = flatten(ab_rgb, ab_a, brgb)
        fs = flatten(as_rgb, as_a, brgb)
        print("  on %-14s AccentBorder -> %s 대비 %.2f / AccentSurface -> %s 대비 %.2f"
              % (bname, hexc(fa), CR(fa, brgb), hexc(fs), CR(fs, brgb)))
    print("")

    hover_rgb, hover_a = T["CardBorderHover"]
    for bname, brgb in (("CardSurface", CARD),):
        fh = flatten(hover_rgb, hover_a, brgb)
        print("  on %-14s CardBorderHover -> %s 대비 %.2f" % (bname, hexc(fh), CR(fh, brgb)))

    # 등급 테두리와의 ΔE — Flatten해도 관계가 유지되는가
    print("\n  등급 테두리(α0.55, RarityBorder)를 Flatten했을 때 인접 ΔE (변별 하한 7.8):")
    ramp = [from_hex("9C978C"), from_hex("BCAC8B"), from_hex("DEC081"), from_hex("FFD375")]
    names = ["일반", "희귀", "영웅", "전설"]
    flat_ramp = [flatten(c, 0.55, CARD) for c in ramp]
    for i in range(3):
        print("      %s↔%s  ΔE %.2f  (대비 %.2f / %.2f)"
              % (names[i], names[i + 1], de76(flat_ramp[i], flat_ramp[i + 1]),
                 CR(flat_ramp[i], CARD), CR(flat_ramp[i + 1], CARD)))
    wornb = T["CardBorderWorn"][0]
    print("      착용(α1.00) 대비 %.2f — 전설 Flatten %.2f 과의 서열: %s"
          % (CR(wornb, CARD), CR(flat_ramp[3], CARD),
             "착용이 위" if CR(wornb, CARD) > CR(flat_ramp[3], CARD) else "역전"))


if __name__ == "__main__":
    main()
