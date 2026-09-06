#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
design-art — R14 판정용 실측기.
  과제1: α<1 토큰 9종 + 리터럴의 Flatten 등가색 확정
  과제2: 설정창 컨트롤 면의 목표 대비 확정

교정부터 한다(alphableed.py와 같은 검사 10종). 깨지면 즉시 중단.
"""

import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from alphableed import (lin, L, CR, flatten, show_through, hexc, from_hex, de76,
                        calibrate, load_tokens)


def dstA_over_opaque(a):
    """불투명(dstA=1) 위에 α짜리 판 하나 -> dstA' = a^2 + 1*(1-a) ; 비침 = 1-dstA'."""
    return a * a + 1.0 * (1.0 - a)


def main():
    calibrate()
    T = load_tokens()
    P = T["PanelSurface"][0]
    C = T["CardSurface"][0]
    M = T["CardSurfaceMuted"][0]
    S = T["SubtleSurface"][0]
    LK = T["ThumbSurfaceLocked"][0]
    ACC = T["Accent"][0]
    T1 = T["TextPrimary"][0]
    T2 = T["TextSecondary"][0]
    T3 = T["TextTertiary"][0]
    ONACC = T["OnAccentSolid"][0]
    CHARCOAL = T["InkContrastCharcoal"][0]
    PAPER = T["PortraitSurface"][0]
    DISABLED = T["DisabledControlInk"][0]

    MIN_NONTEXT = 3.0
    MIN_TEXT = 4.5
    FACE_TARGET = MIN_NONTEXT * 1.20      # 3.60  — UiChrome.ControlFaceContrastTarget
    INK_TARGET = MIN_TEXT * 1.15          # 5.175 — UiChrome.ControlInkContrastTarget

    # --- UiChrome.SelectInkOnSurface / ControlFaceOnSurface 재현 -------------
    def select_ink(backdrop, ladder):
        if CR(ladder, backdrop) >= MIN_TEXT:
            return ladder, True
        if CR(ONACC, backdrop) >= MIN_TEXT:
            return ONACC, True
        if CR(T2, backdrop) >= MIN_TEXT:
            return T2, True
        if CR(T1, backdrop) >= MIN_TEXT:
            return T1, True
        return (T1 if CR(T1, backdrop) >= CR(ONACC, backdrop) else ONACC), False

    def control_face(backdrop, tint=None):
        if tint is None:
            up = FACE_TARGET * (L(backdrop) + 0.05) - 0.05 <= 1.0
            tint = (1, 1, 1) if up else (0, 0, 0)
        for i in range(1025):
            face = flatten(tint, i / 1024.0, backdrop)
            if CR(face, backdrop) < FACE_TARGET:
                continue
            ink, readable = select_ink(face, T1)
            if not readable:
                continue
            if CR(ink, face) >= INK_TARGET:
                return face, i / 1024.0, ink
        return flatten(tint, 1.0, backdrop), 1.0, None

    print("=" * 84)
    print("A. 과제1 — α<1 토큰의 Flatten 등가색 확정표 (호출부가 실제로 올라앉은 바탕별)")
    print("=" * 84)
    plan = [
        ("Divider",         [("PanelSurface", P), ("CardSurface", C)]),
        ("TrackBackground", [("PanelSurface", P), ("CardSurface", C), ("CardSurfaceMuted", M)]),
        ("PanelSheen",      [("PanelSurface", P)]),
        ("CardBorder",      [("PanelSurface", P), ("CardSurface", C), ("CardSurfaceMuted", M),
                             ("SubtleSurface", S), ("ThumbSurfaceLocked", LK), ("PortraitSurface", PAPER)]),
        ("AccentSurface",   [("PanelSurface", P), ("CardSurface", C), ("CardSurfaceMuted", M)]),
        ("PanelBorder",     [("PanelSurface", P), ("CardSurface", C)]),
        ("PanelHighlight",  [("PanelSurface", P)]),
        ("AccentBorder",    [("PanelSurface", P), ("CardSurface", C), ("SubtleSurface", S)]),
        ("CardBorderHover", [("CardSurface", C)]),
    ]
    for tok, backs in plan:
        rgb, a = T[tok]
        print("\n  %-16s α%.2f   불투명 위 비침 %.2f%%   (α=1이면 0.00%%)"
              % (tok, a, (1 - dstA_over_opaque(a)) * 100))
        for bn, br in backs:
            f = flatten(rgb, a, br)
            print("      on %-19s -> %s   대비 %5.2f   %s"
                  % (bn, hexc(f), CR(f, br), "3.0↑" if CR(f, br) >= 3.0 else "3.0 미달(원본 그대로)"))

    print("\n" + "=" * 84)
    print("B. 리터럴 α — 초상화 액자 테두리 (CharacterInfoWindow.cs:1167)")
    print("=" * 84)
    for label, back, other in (("흰 잉크(목탄 무대)", CHARCOAL, P), ("검 잉크(종이 무대)", PAPER, P)):
        f018 = flatten((1, 1, 1), 0.18, back)
        fcb = flatten((1, 1, 1), 0.10, back)
        print("  %s  무대 %s" % (label, hexc(back)))
        print("      Flatten(흰 α0.18, 무대) = %s   무대 대비 %.2f / 창 바탕 대비 %.2f"
              % (hexc(f018), CR(f018, back), CR(f018, other)))
        print("      Flatten(CardBorder α0.10, 무대) = %s   무대 대비 %.2f / 창 바탕 대비 %.2f"
              % (hexc(fcb), CR(fcb, back), CR(fcb, other)))
        print("      무대 자체 vs 창 바탕 = %.2f" % CR(back, other))

    print("\n" + "=" * 84)
    print("C. 과제2 — 설정창 컨트롤 면. 현재값 vs 규칙(ControlFaceOnSurface)이 내는 값")
    print("=" * 84)
    face_card, a_card, ink_card = control_face(C)
    face_panel, a_panel, ink_panel = control_face(P)
    print("  ControlFaceOnSurface(CardSurface)  = %s (흰 α %.4f)  면 %.2f / 잉크 %s %.2f"
          % (hexc(face_card), a_card, CR(face_card, C), hexc(ink_card), CR(ink_card, face_card)))
    print("  ControlFaceOnSurface(PanelSurface) = %s (흰 α %.4f)  면 %.2f / 잉크 %s %.2f"
          % (hexc(face_panel), a_panel, CR(face_panel, P), hexc(ink_panel), CR(ink_panel, face_panel)))
    print("  (참고) UiChrome.CardActionSurface  = 같은 함수 = %s  ← 정보창 카드 [착용] 칩이 쓰는 값"
          % hexc(face_card))
    print("  (참고) UiChrome.ChromeButtonSurface = Flatten(흰 α0.50, Panel) = %s  대비 %.2f"
          % (hexc(flatten((1, 1, 1), 0.50, P)), CR(flatten((1, 1, 1), 0.50, P), P)))

    print("\n  현재 설정창 실측 :")
    cur = [
        ("ButtonSurfaceOnCard  (스텝/버튼/입력칸 면)", flatten((1, 1, 1), 0.10, C), C),
        ("ButtonSurfaceOnPanel (푸터 [지금 종료] 면)", flatten((1, 1, 1), 0.10, P), P),
        ("OutlineOnCard        (버튼/세그/견본 테두리)", flatten((1, 1, 1), 0.16, C), C),
        ("OutlineOnPanel       (푸터 버튼 테두리)", flatten((1, 1, 1), 0.16, P), P),
        ("TrackOnCard          (스위치/슬라이더 트랙)", flatten((1, 1, 1), 0.09, C), C),
        ("CardSurface          (안 고른 세그먼트 면)", C, C),
        ("CardSurfaceMuted     (페이저 버튼 면 on Panel)", M, P),
    ]
    for n, f, b in cur:
        print("      %-44s %s  대비 %5.2f  %s" % (n, hexc(f), CR(f, b), "OK" if CR(f, b) >= 3.0 else "미달"))

    print("\n  목표 대비별 필요한 흰색 α (카드 위) :")
    for target in (3.00, 3.30, 3.60):
        for i in range(1025):
            f = flatten((1, 1, 1), i / 1024.0, C)
            if CR(f, C) >= target:
                ink, ok = select_ink(f, T1)
                print("      %.2f : α %.4f -> %s  대비 %.2f  잉크 %s %.2f %s"
                      % (target, i / 1024.0, hexc(f), CR(f, C), hexc(ink), CR(ink, f),
                         "" if ok else "(사다리 붕괴)"))
                break

    print("\n  제안 면과 이웃의 서열 / 거리 :")
    rows = [
        ("고른 세그먼트 (Accent)", ACC, C),
        ("제안 컨트롤 면", face_card, C),
        ("안 고른 세그먼트 (CardSurface)", C, C),
        ("스위치 트랙 OFF (TrackOnCard)", flatten((1, 1, 1), 0.09, C), C),
        ("스위치 손잡이 (TextPrimary)", T1, C),
        ("슬라이더 채움 ON (Accent)", ACC, C),
    ]
    for n, c, b in rows:
        print("      %-32s %s  카드 대비 %5.2f" % (n, hexc(c), CR(c, b)))
    print("      ΔE(제안 면, Accent)              = %.2f  (변별 하한 7.8)" % de76(face_card, ACC))
    print("      ΔE(제안 면, CardSurface)         = %.2f" % de76(face_card, C))
    print("      ΔE(제안 면, TextPrimary 손잡이)   = %.2f" % de76(face_card, T1))
    print("      대비비(Accent / 제안 면)          = %.2f / %.2f = %.2fx"
          % (CR(ACC, C), CR(face_card, C), CR(ACC, C) / CR(face_card, C)))
    print("      제안 면 위 Accent 대비            = %.2f  (선택 칩이 제안 면 옆에 설 때)"
          % CR(ACC, face_card))

    print("\n  테두리만 올리는 대안(면은 그대로) — 카드 위에서 3.0을 내는 흰색 α :")
    for i in range(1025):
        f = flatten((1, 1, 1), i / 1024.0, C)
        if CR(f, C) >= 3.0:
            print("      α %.4f -> %s 대비 %.2f   ← 1px 선 하나가 이 밝기여야 한다"
                  % (i / 1024.0, hexc(f), CR(f, C)))
            break
    print("      ※ 배율 1에서 1px 선은 물리 1픽셀 — UiChrome.cs:1368-1374가 이미 기각한 경로다.")

    print("\n  비활성 상태 검산 (WCAG 1.4.11은 비활성 컨트롤을 면제한다) :")
    dis = [
        ("꺼진 스위치 손잡이 on DividerOnCard", DISABLED, flatten((1, 1, 1), 0.07, C)),
        ("비활성 세그먼트 활성칩 면 on Card", flatten((1, 1, 1), 0.10, C), C),
        ("제안 면을 쓴 비활성 세그먼트 활성칩", face_card, C),
    ]
    for n, a_, b_ in dis:
        print("      %-42s 대비 %5.2f" % (n, CR(a_, b_)))
    print("      제안 면 위 InkTitle(false)=TextSecondary 대비 %.2f (본문 하한 4.50)"
          % CR(T2, face_card))
    print("      제안 면 위 InkOnSurface가 고르는 잉크 = %s (%.2f)"
          % (hexc(select_ink(face_card, T2)[0]), CR(select_ink(face_card, T2)[0], face_card)))

    print("\n" + "=" * 84)
    print("D. 코너 AA 램프 — Flatten이 실제로 무엇을 바꾸는가 (바깥 실루엣 4곳만 해당)")
    print("=" * 84)
    print("  대상: 부채꼴 버튼 링 / 부채꼴 이름표 보더 / 온보딩 알약 보더 (나머지는 전부 불투명 부모 위)")
    W = (1, 1, 1)
    for tok, k, body in (("CardBorder(링)", 0.10, C), ("PanelBorder(이름표)", 0.16, P),
                         ("AccentBorder(알약)", 0.55, P)):
        Ftok = T[tok.split("(")[0]][0]
        F = flatten(Ftok, k, body)
        print("\n  %s  k=%.2f  Flatten -> %s" % (tok, k, hexc(F)))
        print("      %-6s %-11s %-11s %-11s %-11s" % ("a", "D계수 raw", "D계수 flat", "dstA raw", "dstA flat"))
        for a in (0.25, 0.5, 0.75, 1.0):
            d_raw = (1 - a) * (1 - k * a)
            d_flat = (1 - a) ** 2
            base = a * a
            dA_raw = (k * a) ** 2 + base * (1 - k * a)
            dA_flat = a * a + base * (1 - a)
            print("      %-6.2f %-11.4f %-11.4f %-11.4f %-11.4f" % (a, d_raw, d_flat, dA_raw, dA_flat))
        worst = (0, 0, None)
        for dn, D in (("흰", (1, 1, 1)), ("중간회색", (0.5,) * 3), ("검", (0, 0, 0))):
            wd, wa = 0, 0
            for i in range(1, 100):
                a = i / 100.0
                bodyc = tuple(D[j] + (body[j] - D[j]) * a for j in range(3))
                raw = tuple(bodyc[j] + (Ftok[j] - bodyc[j]) * (k * a) for j in range(3))
                fl = tuple(bodyc[j] + (F[j] - bodyc[j]) * a for j in range(3))
                d = de76(raw, fl)
                if d > wd:
                    wd, wa = d, a
            print("      바탕화면 %-8s 최대 ΔE %6.2f (a=%.2f) — 램프 폭 1px 이내" % (dn, wd, wa))


if __name__ == "__main__":
    main()
