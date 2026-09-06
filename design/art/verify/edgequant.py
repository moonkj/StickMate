#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""design-art R14 추가 판정 — EdgeOnSurface의 8비트 양자화 손실을 재고 마진을 정한다.

coder 보고: 부동소수 해는 3.0005~3.0096인데 8비트로 반올림하면 17종 중 7종이 2.9895~2.9999.
여기서 (1) 그 보고를 독립 재현하고 (2) 「전 바탕에서 양자화 후에도 3.0 이상」을 만드는
<b>최소 배수</b>를 이분이 아니라 격자로 찾는다.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from alphableed import L, CR, flatten, hexc, calibrate, load_tokens

MIN_NONTEXT = 3.0


def q8(c):
    """화면에 실제로 나가는 8비트 색."""
    return tuple(round(min(1.0, max(0.0, x)) * 255) / 255.0 for x in c)


def edge(backdrop, target):
    """UiChrome.EdgeOnSurface 재현 — 방향 판정식은 ControlFaceOnSurface(Color)와 같다."""
    up = target * (L(backdrop) + 0.05) - 0.05 <= 1.0
    tint = (1, 1, 1) if up else (0, 0, 0)
    for i in range(1025):
        f = flatten(tint, i / 1024.0, backdrop)
        if CR(f, backdrop) >= target:
            return f, i / 1024.0, "흰" if up else "검"
    f = flatten(tint, 1.0, backdrop)
    return f, 1.0, "흰" if up else "검"


def main():
    calibrate()
    T = load_tokens()

    # 정책 §4-3의 10행 + coder가 「선언된 바탕 17종」이라 부른 것에 대응하는 실 표면 전부.
    backdrops = [
        ("PanelSurface", T["PanelSurface"][0]),
        ("CardSurface", T["CardSurface"][0]),
        ("CardSurfaceMuted", T["CardSurfaceMuted"][0]),
        ("SubtleSurface", T["SubtleSurface"][0]),
        ("ThumbSurfaceLocked", T["ThumbSurfaceLocked"][0]),
        ("InkContrastCharcoal 목탄무대", T["InkContrastCharcoal"][0]),
        ("PortraitSurface 종이무대", T["PortraitSurface"][0]),
        ("검정 잉크 견본", (0.0, 0.0, 0.0)),
        ("흰 잉크 견본", (1.0, 1.0, 1.0)),
        ("Accent", T["Accent"][0]),
        ("ChromeButtonSurface", flatten((1, 1, 1), 0.50, T["PanelSurface"][0])),
        ("ChromeButtonSurfaceHover", flatten((1, 1, 1), 0.58, T["PanelSurface"][0])),
        ("ChromeButtonSurfacePressed", flatten((1, 1, 1), 0.66, T["PanelSurface"][0])),
        ("ControlFace on Card #838589", flatten((1, 1, 1), 0.4570, T["CardSurface"][0])),
        ("ControlFace on Panel #848588", flatten((1, 1, 1), 0.4756, T["PanelSurface"][0])),
        ("ButtonSurfaceOnCard #32353C", flatten((1, 1, 1), 0.10, T["CardSurface"][0])),
        ("TextPrimary 흰채움", T["TextPrimary"][0]),
    ]

    print("=" * 96)
    print("J-1. coder 보고 재현 — target = 3.0 (마진 없음)")
    print("=" * 96)
    print("  %-30s %-8s %-9s %-9s %-9s %-9s" % ("바탕", "방향", "부동소수", "화면색", "양자화후", "판정"))
    bad = []
    for n, c in backdrops:
        f, a, d = edge(c, MIN_NONTEXT)
        cr_float = CR(f, c)
        cr_q = CR(q8(f), q8(c))
        ok = cr_q >= MIN_NONTEXT
        if not ok:
            bad.append((n, cr_q))
        print("  %-30s %-8s %-9.4f %-9s %-9.4f %s"
              % (n, d, cr_float, hexc(f), cr_q, "OK" if ok else "★ 미달"))
    print("\n  → 양자화 후 미달 %d / %d 종. 최악 %s %.4f (목표 대비 %+.2f%%)"
          % (len(bad), len(backdrops),
             min(bad, key=lambda r: r[1])[0] if bad else "-",
             min(bad, key=lambda r: r[1])[1] if bad else 0,
             (min(bad, key=lambda r: r[1])[1] / MIN_NONTEXT - 1) * 100 if bad else 0))

    print("\n" + "=" * 96)
    print("J-2. 배수를 올리면 몇 종이 살아나는가 — 격자 스윕")
    print("=" * 96)
    print("  %-9s %-9s %-9s %-38s" % ("배수", "target", "미달 수", "최악(양자화 후)"))
    first_clean = None
    for k in range(1000, 1211, 5):
        mult = k / 1000.0
        target = MIN_NONTEXT * mult
        worst = (None, 99.0)
        cnt = 0
        for n, c in backdrops:
            f, a, d = edge(c, target)
            cr_q = CR(q8(f), q8(c))
            if cr_q < MIN_NONTEXT:
                cnt += 1
                if cr_q < worst[1]:
                    worst = (n, cr_q)
        if cnt == 0 and first_clean is None:
            first_clean = mult
        if mult in (1.000, 1.005, 1.010, 1.015, 1.020, 1.025, 1.030, 1.050,
                    1.100, 1.150, 1.200) or (first_clean == mult):
            print("  %-9.3f %-9.4f %-9d %s"
                  % (mult, target, cnt, "-" if cnt == 0 else "%s %.4f" % worst))
    print("\n  → 양자화 후 전 바탕이 3.0을 넘는 <b>최소 배수</b> = %.3f" % first_clean)

    print("\n" + "=" * 96)
    print("J-3. 후보 배수별 실제 테두리색 — 「눈에 띄게 굵어/밝아지는가」")
    print("=" * 96)
    for mult in (1.000, first_clean, 1.05, 1.10, 1.15, 1.20):
        target = MIN_NONTEXT * mult
        print("\n  ── 배수 %.3f (target %.4f) ──" % (mult, target))
        print("     %-30s %-9s %-9s %-8s %-8s" % ("바탕", "테두리", "양자화후", "ΔE(3.0안)", "α"))
        base = {}
        for n, c in backdrops:
            f0, a0, _ = edge(c, MIN_NONTEXT)
            base[n] = (f0, a0)
        for n, c in backdrops:
            f, a, d = edge(c, target)
            f0, a0 = base[n]
            # 3.0안 대비 8비트 계단 몇 칸 움직였나
            steps = max(abs(round(f[i] * 255) - round(f0[i] * 255)) for i in range(3))
            print("     %-30s %-9s %-9.4f %-8d %-8.4f"
                  % (n, hexc(f), CR(q8(f), q8(c)), steps, a))

    print("\n" + "=" * 96)
    print("J-4. 부수 발견 검산 — 밝기 애매 구간에서 흰쪽 우선이 만드는 것")
    print("=" * 96)
    print("  %-30s %-8s %-9s %-8s %-9s %-9s"
          % ("바탕", "L", "흰쪽해", "대비", "검은쪽해", "대비"))
    for n, c in backdrops:
        lum = L(c)
        # 흰쪽
        w = None
        for i in range(1025):
            f = flatten((1, 1, 1), i / 1024.0, c)
            if CR(f, c) >= MIN_NONTEXT:
                w = f
                break
        b = None
        for i in range(1025):
            f = flatten((0, 0, 0), i / 1024.0, c)
            if CR(f, c) >= MIN_NONTEXT:
                b = f
                break
        print("  %-30s %-8.4f %-9s %-8s %-9s %-9s"
              % (n, lum,
                 hexc(w) if w else "해없음", ("%.2f" % CR(w, c)) if w else "-",
                 hexc(b) if b else "해없음", ("%.2f" % CR(b, c)) if b else "-"))
    print("\n  ※ 「해없음」이 한쪽에만 있으면 방향은 선택이 아니라 <b>강제</b>다.")
    print("  ※ 양쪽 다 해가 있는 구간에서만 「흰쪽 우선」이 실제 선택으로 작동한다.")


if __name__ == "__main__":
    main()
