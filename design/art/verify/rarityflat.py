#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""design-art — RarityBorder(α0.55)를 Flatten해도 §15.6-a의 보증이 살아남는가."""

import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from alphableed import CR, flatten, hexc, from_hex, de76, calibrate, load_tokens, UICHROME

DISCRIM = 7.8   # 변별 하한 (PALETTE_SPEC)


def load_ramp():
    """UiChrome._rarityRamp의 hex를 소스에서 직접 읽는다 — 값을 손으로 베끼지 않는다."""
    src = open(UICHROME, encoding="utf-8").read()
    m = re.search(r"_rarityRamp\s*=\s*\{(.*?)\};", src, re.S)
    hexes = re.findall(r"RarityHex\(0x([0-9A-Fa-f]{6})\)", m.group(1))
    return [from_hex(h) for h in hexes]


def load_alpha():
    src = open(UICHROME, encoding="utf-8").read()
    return float(re.search(r"RarityBorderAlpha\s*=\s*([0-9.]+)f", src).group(1))


def main():
    calibrate()
    T = load_tokens()
    ramp = load_ramp()
    alpha = load_alpha()
    names = ["일반", "희귀", "영웅", "전설"]
    print("소스에서 읽은 램프: %s   RarityBorderAlpha = %.2f\n"
          % (", ".join(hexc(c) for c in ramp), alpha))

    backdrops = [
        ("CardSurface(보유 카드)", T["CardSurface"][0]),
        ("CardSurfaceMuted(잠긴 카드)", T["CardSurfaceMuted"][0]),
        ("ThumbSurfaceLocked(상세 썸네일 잠김)", T["ThumbSurfaceLocked"][0]),
    ]
    worn = T["CardBorderWorn"][0]
    hover_rgb, hover_a = T["CardBorderHover"]
    t1 = T["TextPrimary"][0]

    for bn, br in backdrops:
        flat = [flatten(c, alpha, br) for c in ramp]
        print("=" * 78)
        print("바탕 %s %s" % (bn, hexc(br)))
        print("=" * 78)
        for i, c in enumerate(flat):
            print("   %-4s Flatten -> %s   바탕 대비 %5.2f" % (names[i], hexc(c), CR(c, br)))
        mins = []
        for i in range(3):
            d = de76(flat[i], flat[i + 1])
            mins.append(d)
            print("   %s↔%s ΔE %5.2f   %s" % (names[i], names[i + 1], d,
                                              "OK(≥7.8)" if d >= DISCRIM else "★ 변별 하한 미달"))
        print("   인접 최소 ΔE = %.2f  (하한 %.1f, 여유 %+.1f%%)"
              % (min(mins), DISCRIM, (min(mins) / DISCRIM - 1) * 100))
        # 4상태 서열: 선택 > 호버 > 착용 > 등급
        hv = flatten(hover_rgb, hover_a, br)
        print("   서열 검산 — 선택 %s %.2f > 호버 %s %.2f > 착용 %s %.2f > 전설 %s %.2f : %s"
              % (hexc(t1), CR(t1, br), hexc(hv), CR(hv, br), hexc(worn), CR(worn, br),
                 hexc(flat[3]), CR(flat[3], br),
                 "유지" if CR(t1, br) > CR(hv, br) > CR(worn, br) > CR(flat[3], br) else "★ 역전"))
        print("   ΔE(착용 파랑, 등급 4색) 최소 = %.2f" % min(de76(worn, c) for c in flat))
        print("")

    print("=" * 78)
    print("대조 — 이 값들이 UiChrome §15.6-a가 적어 둔 값과 같은가")
    print("=" * 78)
    flat_card = [flatten(c, alpha, T["CardSurface"][0]) for c in ramp]
    doc = [9.06, 11.13, 11.01]
    for i in range(3):
        got = de76(flat_card[i], flat_card[i + 1])
        print("   %s↔%s  문서 %.2f  실측 %.2f  차 %+.3f  %s"
              % (names[i], names[i + 1], doc[i], got, got - doc[i],
                 "일치" if abs(got - doc[i]) < 0.05 else "★ 불일치"))
    print("   → 문서값은 <b>이미 합성 후 기준</b>으로 계산돼 있었다. Flatten은 보이는 색을 바꾸지 않는다.")

    print("\n   대조 — 카드 대비 문서값 (UiChrome §15.6-a 검산 1 인접 표):")
    for i, c in enumerate(flat_card):
        print("      %-4s 카드 대비 %.2f" % (names[i], CR(c, T["CardSurface"][0])))
    print("      전설 4.45 / 착용 파랑 α1.00 %.2f — UiChrome 주석과 대조"
          % CR(worn, T["CardSurface"][0]))


if __name__ == "__main__":
    main()
