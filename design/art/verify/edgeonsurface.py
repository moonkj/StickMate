#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""design-art — 「테두리」를 바탕에서 파생시키는 규칙(EdgeOnSurface)의 해를 전 바탕에서 푼다.

규칙(제안):
    EdgeOnSurface(backdrop, target)
      = 흰색으로 위로 갈 수 있으면 위로, 아니면 검정으로 아래로 —
        target(기본 MinNonTextContrast 3.0)을 만족하는 <b>최소</b> 혼합의 Flatten 결과.
    방향 판정은 UiChrome.ControlFaceOnSurface(Color)가 이미 쓰는 식과 <b>같은 식</b>이다.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from alphableed import L, CR, flatten, hexc, from_hex, de76, calibrate, load_tokens

MIN_NONTEXT = 3.0


def edge_on_surface(backdrop, target=MIN_NONTEXT):
    up = target * (L(backdrop) + 0.05) - 0.05 <= 1.0
    tint = (1, 1, 1) if up else (0, 0, 0)
    for i in range(1025):
        a = i / 1024.0
        f = flatten(tint, a, backdrop)
        if CR(f, backdrop) >= target:
            return f, a, CR(f, backdrop), "흰" if up else "검"
    return flatten(tint, 1.0, backdrop), 1.0, CR(flatten(tint, 1.0, backdrop), backdrop), "흰" if up else "검"


def main():
    calibrate()
    T = load_tokens()
    surfaces = [
        ("PanelSurface   창 바탕", T["PanelSurface"][0]),
        ("CardSurface    카드/칩", T["CardSurface"][0]),
        ("CardSurfaceMuted 잠긴칸", T["CardSurfaceMuted"][0]),
        ("SubtleSurface  상세패널", T["SubtleSurface"][0]),
        ("ThumbSurfaceLocked 썸네일", T["ThumbSurfaceLocked"][0]),
        ("InkContrastCharcoal 흰잉크 무대", T["InkContrastCharcoal"][0]),
        ("PortraitSurface 검잉크 무대", T["PortraitSurface"][0]),
        ("검정 잉크 견본 #000000", (0.0, 0.0, 0.0)),
        ("흰 잉크 견본 #FFFFFF", (1.0, 1.0, 1.0)),
        ("Accent 강조 채움", T["Accent"][0]),
        ("ControlFace #838589", flatten((1, 1, 1), 0.4570, T["CardSurface"][0])),
    ]
    print("=" * 92)
    print("G. EdgeOnSurface(바탕) — 비텍스트 하한 3.0을 <b>어떤 바탕에서도</b> 내는 불투명 테두리색")
    print("=" * 92)
    print("  %-30s %-9s %-6s %-7s %-8s %-9s" % ("바탕", "바탕색", "방향", "혼합α", "테두리", "대비"))
    for n, c in surfaces:
        f, a, cr, d = edge_on_surface(c)
        print("  %-30s %-9s %-6s %-7.4f %-8s %6.2f" % (n, hexc(c), d, a, hexc(f), cr))

    print("\n  ※ 여유(3.30 목표 = 하한 × 1.10)로 잡으면:")
    print("  %-30s %-9s %-7s %-8s %-9s" % ("바탕", "바탕색", "혼합α", "테두리", "대비"))
    for n, c in surfaces:
        f, a, cr, d = edge_on_surface(c, 3.30)
        print("  %-30s %-9s %-7.4f %-8s %6.2f" % (n, hexc(c), a, hexc(f), cr))

    print("\n" + "=" * 92)
    print("H. 지금 잉크색 견본의 링이 실제로 무엇인가 (CharacterInfoWindow.cs:1122 / :1776)")
    print("=" * 92)
    T1 = T["TextPrimary"][0]
    PB, PBa = T["PanelBorder"]
    CHAR = T["InkContrastCharcoal"][0]
    for sn, sc in (("검정 잉크 견본 #000000", (0, 0, 0)), ("흰 잉크 견본 #FFFFFF", (1, 1, 1))):
        raw_unsel = flatten(PB, PBa, sc)
        print("  %s" % sn)
        print("      비선택 링 = PanelBorder α0.16 (raw)  -> 합성색 %s  견본 대비 %5.2f  %s"
              % (hexc(raw_unsel), CR(raw_unsel, sc), "OK" if CR(raw_unsel, sc) >= 3.0 else "★ 미달"))
        print("      선택 링   = TextPrimary %s          견본 대비 %5.2f  %s"
              % (hexc(T1), CR(T1, sc), "OK" if CR(T1, sc) >= 3.0 else "★ 미달"))
        f, a, cr, d = edge_on_surface(sc)
        print("      제안 비선택 = EdgeOnSurface(견본) = %s  대비 %5.2f (%s쪽 α%.4f)" % (hexc(f), cr, d, a))
        sel = T1 if L(sc) < 0.18 else CHAR
        print("      제안 선택   = %s  대비 %5.2f   (밝은 견본 위에서는 목탄으로 뒤집는다)"
              % (hexc(sel), CR(sel, sc)))
        print("      ΔE(제안 선택, 제안 비선택) = %.2f  — 두 상태가 갈리는가(하한 7.8)" % de76(sel, f))

    print("\n" + "=" * 92)
    print("I. 초상화 액자 테두리 — 리터럴 α0.18을 EdgeOnSurface로 대체했을 때")
    print("=" * 92)
    P = T["PanelSurface"][0]
    for sn, sc in (("흰 잉크 무대(목탄)", CHAR), ("검 잉크 무대(종이)", T["PortraitSurface"][0])):
        cur = flatten((1, 1, 1), 0.18, sc) if sc == CHAR else flatten((1, 1, 1), 0.10, sc)
        f, a, cr, d = edge_on_surface(sc)
        print("  %s %s" % (sn, hexc(sc)))
        print("      현재 = %s  무대 대비 %5.2f  /  창 바탕 대비 %5.2f" % (hexc(cur), CR(cur, sc), CR(cur, P)))
        print("      제안 = %s  무대 대비 %5.2f  /  창 바탕 대비 %5.2f  (%s쪽 α%.4f)"
              % (hexc(f), cr, CR(f, P), d, a))
    print("\n  ※ 액자 테두리는 <b>두 가지 일</b>을 한다 — (1) 무대와 창 바탕을 가르고 (2) 액자임을 말한다.")
    print("     (1)은 무대 자체가 이미 한다: 종이 무대 vs 창 바탕 = %.2f. 목탄 무대 vs 창 바탕 = %.2f."
          % (CR(T["PortraitSurface"][0], P), CR(CHAR, P)))
    print("     즉 <b>목탄 무대에서만</b> 테두리가 분리막이고, 종이 무대에서는 장식이다.")


if __name__ == "__main__":
    main()
