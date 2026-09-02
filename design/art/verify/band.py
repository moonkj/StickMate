# -*- coding: utf-8 -*-
"""★ 자립 대역 재유도 — "흑·백 두 극단"만 본 것이 **내 실수였다** (design-art, 2026-09-02)

첫 유도(box.py §3)는 바탕화면의 두 극단(흰/검)만 봤고 L ∈ [0.100, 0.300]을 얻었다.
그런데 우리가 **실제로 색을 고른 배경**이 하나 더 있다 — 초상화 무대다. 그리고 그것은
극단이 아니라 **중간 밝기**다. 중간 밝기 배경이 극단보다 **더 어렵다**(자기 휘도에 가까울수록
대비가 0으로 간다). 첫 대역으로 고른 보조색이 종이 무대에서 2.48:1로 미달했다.

    python3 band.py
"""
import colorlab as C

W, K = (255, 255, 255), (0, 0, 0)
PAPER = C.hex2rgb("#E9EAE6")      # UiChrome.PortraitSurface  (검은 잉크용)
CHARCOAL = C.hex2rgb("#25282E")   # CharacterPortraitStage.ResolveBackdropColor (흰 잉크용)
BACKDROPS = [("흰 바탕화면", W), ("검은 바탕화면", K),
             ("종이 무대 #E9EAE6", PAPER), ("목탄 무대 #25282E", CHARCOAL)]
FLOOR = 3.0


def limits():
    """대비 하한 3.0을 네 배경 전부에서 만족하는 상대휘도 구간."""
    lo, hi = 0.0, 1.0
    rows = []
    for name, b in BACKDROPS:
        lb = C.L(b)
        # 색이 배경보다 어두울 때: (lb+0.05)/(L+0.05) >= 3  =>  L <= (lb+0.05)/3 - 0.05
        # 색이 배경보다 밝을 때:   (L+0.05)/(lb+0.05) >= 3  =>  L >= 3(lb+0.05) - 0.05
        dark_side = (lb + 0.05) / FLOOR - 0.05
        light_side = FLOOR * (lb + 0.05) - 0.05
        rows.append((name, lb, dark_side, light_side))
    # 배경보다 어두워야 하는 것(밝은 배경) / 밝아야 하는 것(어두운 배경)을 나눈다
    hi = min(r[2] for r in rows if r[2] > 0)
    lo = max(r[3] for r in rows if r[3] < 1.0)
    return lo, hi, rows


if __name__ == "__main__":
    C.calibrate()
    lo, hi, rows = limits()
    print("=" * 78)
    print("자립 대역 — 네 배경 전부에서 CR >= 3.0")
    print("=" * 78)
    print(f"  {'배경':22s} {'L':>8s} {'이보다 어두우면 통과':>20s} {'이보다 밝으면 통과':>18s}")
    for name, lb, d, l in rows:
        print(f"  {name:22s} {lb:8.4f} {('L <= %.4f' % d) if d > 0 else '-':>20s} "
              f"{('L >= %.4f' % l) if l < 1 else '-':>18s}")
    print(f"\n  ★ 교집합: L ∈ [{lo:.4f}, {hi:.4f}]   (첫 유도 [0.1000, 0.3000] 보다 좁다)")
    print(f"     구속하는 것은 **극단이 아니라 두 무대**다 — 종이가 위를, 목탄이 아래를 막는다.")
    mid = ((C.L(PAPER) + 0.05) * (C.L(CHARCOAL) + 0.05)) ** 0.5 - 0.05
    print(f"     두 무대 대비가 같아지는 점 L = {mid:.4f}  ->  양쪽 "
          f"{(mid + 0.05) / (C.L(CHARCOAL) + 0.05):.2f}:1")
    print(f"     (참고: 흰/검만 볼 때의 균형점은 L = {(1.05*0.05)**0.5 - 0.05:.4f} -> 4.58:1)")
    print(f"\n  → 이 대역 안이면 **바탕화면 양 극단 + 무대 양쪽 + 잉크 양쪽**이 한꺼번에 3.0을 넘는다.")
    print(f"     (잉크는 흰/검이므로 바탕화면 극단 조건이 그대로 잉크 조건이다.)")
