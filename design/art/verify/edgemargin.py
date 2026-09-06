#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""design-art R14 추가 판정 2 — 마진을 <b>17개 표본이 아니라 전 색공간</b>에 맞춘다.

17종에 딱 맞는 배수를 고르면 18번째 바탕이 생기는 날 같은 결함이 돌아온다.
그래서 (1) 회색 램프 256단 전수 + (2) 유채색 격자로 양자화 최악 손실을 재고,
(3) 방향 규칙의 절벽(direction flip)이 어디 있는지 찾아 배수가 거기 붙지 않게 한다.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from alphableed import L, CR, flatten, hexc, calibrate, load_tokens

MIN = 3.0


def q8(c):
    return tuple(round(min(1.0, max(0.0, x)) * 255) / 255.0 for x in c)


def edge(backdrop, target, rule="white-first"):
    lb = L(backdrop)
    white_ok = target * (lb + 0.05) - 0.05 <= 1.0
    black_ok = (lb + 0.05) / 0.05 >= target
    if rule == "white-first":
        tint = (1, 1, 1) if white_ok else (0, 0, 0)
    else:  # max-headroom: 더 멀리 갈 수 있는 쪽
        head_w = (1.0 + 0.05) / (lb + 0.05)
        head_b = (lb + 0.05) / 0.05
        tint = (1, 1, 1) if head_w >= head_b else (0, 0, 0)
        if tint == (1, 1, 1) and not white_ok:
            tint = (0, 0, 0)
        if tint == (0, 0, 0) and not black_ok:
            tint = (1, 1, 1)
    for i in range(1025):
        f = flatten(tint, i / 1024.0, backdrop)
        if CR(f, backdrop) >= target:
            return f, tint
    return flatten(tint, 1.0, backdrop), tint


def main():
    calibrate()
    T = load_tokens()

    # ---------------------------------------------------------------
    # K-1. 양자화 최악 손실 — 회색 램프 256단 전수
    # ---------------------------------------------------------------
    print("=" * 90)
    print("K-1. 양자화 손실의 <b>이론 최악</b> — 회색 256단 전수 (target = 3.0)")
    print("=" * 90)
    worst = (None, 99.0)
    fails = 0
    total = 0
    for v in range(256):
        bg = (v / 255.0,) * 3
        f, _ = edge(bg, MIN)
        crq = CR(q8(f), q8(bg))
        total += 1
        if crq < MIN:
            fails += 1
            if crq < worst[1]:
                worst = (v, crq)
    print("  미달 %d / %d 단   최악 회색 #%02X%02X%02X -> %.4f (%.3f%% 부족)"
          % (fails, total, worst[0], worst[0], worst[0], worst[1], (1 - worst[1] / MIN) * 100))

    # ---------------------------------------------------------------
    # K-2. 유채색 격자
    # ---------------------------------------------------------------
    print("\n" + "=" * 90)
    print("K-2. 유채색 격자 (17³ = 4913 색, target = 3.0)")
    print("=" * 90)
    worstc = (None, 99.0)
    failsc = 0
    totalc = 0
    step = 16
    for r in range(0, 256, step):
        for g in range(0, 256, step):
            for b in range(0, 256, step):
                bg = (r / 255.0, g / 255.0, b / 255.0)
                f, _ = edge(bg, MIN)
                crq = CR(q8(f), q8(bg))
                totalc += 1
                if crq < MIN:
                    failsc += 1
                    if crq < worstc[1]:
                        worstc = ((r, g, b), crq)
    print("  미달 %d / %d (%.1f%%)   최악 #%02X%02X%02X -> %.4f (%.3f%% 부족)"
          % (failsc, totalc, failsc / totalc * 100, worstc[0][0], worstc[0][1], worstc[0][2],
             worstc[1], (1 - worstc[1] / MIN) * 100))

    # ---------------------------------------------------------------
    # K-3. 배수를 올렸을 때 전 색공간이 언제 깨끗해지나
    # ---------------------------------------------------------------
    print("\n" + "=" * 90)
    print("K-3. 배수 스윕 — 회색 256단 + 유채색 4913색 <b>전부</b>가 3.0을 넘는 최소 배수")
    print("=" * 90)
    print("  %-8s %-9s %-12s %-12s %-9s" % ("배수", "target", "회색 미달", "유채 미달", "전체 최악"))
    clean = None
    for k in range(1000, 1101, 2):
        mult = k / 1000.0
        target = MIN * mult
        gf = 0
        gw = 99.0
        for v in range(256):
            bg = (v / 255.0,) * 3
            f, _ = edge(bg, target)
            crq = CR(q8(f), q8(bg))
            if crq < MIN:
                gf += 1
            gw = min(gw, crq)
        cf = 0
        cw = 99.0
        for r in range(0, 256, step):
            for g in range(0, 256, step):
                for b in range(0, 256, step):
                    bg = (r / 255.0, g / 255.0, b / 255.0)
                    f, _ = edge(bg, target)
                    crq = CR(q8(f), q8(bg))
                    if crq < MIN:
                        cf += 1
                    cw = min(cw, crq)
        if clean is None and gf == 0 and cf == 0:
            clean = mult
        if mult in (1.000, 1.004, 1.006, 1.008, 1.010, 1.020, 1.030, 1.050, 1.100) or clean == mult:
            print("  %-8.3f %-9.4f %-12d %-12d %-9.4f" % (mult, target, gf, cf, min(gw, cw)))
    print("\n  → 전 색공간이 양자화 후에도 3.0을 넘는 <b>최소 배수</b> = %.3f" % clean)

    # ---------------------------------------------------------------
    # K-4. 방향 절벽 — 배수가 어디에 붙으면 안 되나
    # ---------------------------------------------------------------
    print("\n" + "=" * 90)
    print("K-4. 「흰쪽 우선」 규칙의 절벽 — 흰쪽 해가 사라지는 배수")
    print("=" * 90)
    print("  흰쪽 해 존재 조건: target·(L+0.05) − 0.05 ≤ 1.0  →  target ≤ 1.05/(L+0.05)")
    cands = [
        ("ChromeButtonSurface", flatten((1, 1, 1), 0.50, T["PanelSurface"][0])),
        ("ChromeButtonSurfaceHover", flatten((1, 1, 1), 0.58, T["PanelSurface"][0])),
        ("ControlFace on Card", flatten((1, 1, 1), 0.4570, T["CardSurface"][0])),
        ("Accent", T["Accent"][0]),
    ]
    print("  %-28s %-8s %-12s %-12s" % ("바탕", "L", "절벽 target", "절벽 배수"))
    cliffs = []
    for n, c in cands:
        lb = L(c)
        t = 1.05 / (lb + 0.05)
        cliffs.append(t / MIN)
        print("  %-28s %-8.4f %-12.4f %-12.4f" % (n, lb, t, t / MIN))
    print("\n  → 가장 낮은 절벽 배수 = %.4f. 배수를 이 근처에 두면 <b>바탕이 1/255만 밝아져도</b>"
          % min(cliffs))
    print("     테두리가 거의 흰색 ↔ 거의 검정으로 <b>뒤집힌다</b>.")

    # ---------------------------------------------------------------
    # K-5. 방향 규칙을 「여유 큰 쪽」으로 바꾸면
    # ---------------------------------------------------------------
    print("\n" + "=" * 90)
    print("K-5. 방향 규칙 두 벌 비교 — 흰쪽 우선 vs 여유 큰 쪽")
    print("=" * 90)
    surf = [
        ("PanelSurface", T["PanelSurface"][0]),
        ("CardSurface", T["CardSurface"][0]),
        ("InkContrastCharcoal 목탄", T["InkContrastCharcoal"][0]),
        ("PortraitSurface 종이", T["PortraitSurface"][0]),
        ("검정 견본", (0.0, 0.0, 0.0)),
        ("흰 견본", (1.0, 1.0, 1.0)),
        ("Accent", T["Accent"][0]),
        ("ChromeButtonSurface", flatten((1, 1, 1), 0.50, T["PanelSurface"][0])),
        ("ControlFace #838589", flatten((1, 1, 1), 0.4570, T["CardSurface"][0])),
    ]
    tgt = MIN * 1.05
    print("  (target = %.4f)" % tgt)
    print("  %-28s %-8s %-11s %-11s %-8s" % ("바탕", "L", "흰쪽우선", "여유큰쪽", "같나"))
    for n, c in surf:
        fw, tw = edge(c, tgt, "white-first")
        fh, th = edge(c, tgt, "max-headroom")
        print("  %-28s %-8.4f %-11s %-11s %-8s"
              % (n, L(c), hexc(fw), hexc(fh), "=" if hexc(fw) == hexc(fh) else "★ 다름"))

    print("\n  「여유 큰 쪽」 규칙의 절벽 위치 — 흰/검 여유가 같아지는 L:")
    # (1.05)/(L+0.05) == (L+0.05)/0.05  ->  (L+0.05)^2 = 0.0525 -> L = sqrt(0.0525)-0.05
    import math
    lstar = math.sqrt(0.0525) - 0.05
    print("     L* = sqrt(0.0525) − 0.05 = %.4f  (그 지점의 양쪽 최대 대비 = %.4f로 동일)"
          % (lstar, 1.05 / (lstar + 0.05)))
    print("     ★ 이 절벽은 <b>target과 무관</b>하다 — 배수를 바꿔도 안 움직인다.")
    print("     즉 「여유 큰 쪽」 규칙에서는 <b>마진 선택이 방향을 흔들 수 없다</b>.")


if __name__ == "__main__":
    main()
