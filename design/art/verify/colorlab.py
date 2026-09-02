# -*- coding: utf-8 -*-
"""색 계산기 — 아트 디렉션 전용 (design-art, 2026-09-02)

★ 이 파일은 **쓰기 전에 알려진 값으로 교정한다.** 교정이 하나라도 깨지면 아무 숫자도
  내지 않고 죽는다. 이 저장소의 거짓 초록은 전부 "실패한 측정과 성공한 측정이 똑같이 생긴"
  형태였다(TEAM.md §4).

담는 것
  · sRGB <-> 선형 / 상대휘도 / WCAG 대비비    (Tools/ContrastProbe/measure_chip.py와 같은 식)
  · Unity Color.RGBToHSV / HSVToRGB 포트
  · ItemCatalog.WornColor 포트               (S >= 0.42, V in [0.55, 0.80])
  · CIELAB(D65) + CIE76 dE*ab                (색이 "다르게 보이는가"의 자)
  · AccessoryShapeBuilder.FillOutlineColor 포트 (x0.62 그늘색)

교정표 (전부 외부에서 검증 가능한 값)
  대비   흰/검 21.0000 · 동일색 1.0000 · #767676/흰 4.5422 · #000/#808080 5.3172
  HSV    Unity 왕복 항등 (256색 무작위 + 경계색)
  WornColor  #D8B27A -> #CCA873 / #F0C25C -> #CCA54E
             ★ 이 두 값은 design-equipment가 **독립적으로** 실측해 리더 브리프에 적은 값이다.
               내 포트가 같은 답을 내지 않으면 포트가 틀린 것이다.
  LAB    흰 (100, 0, 0) · 검 (0, 0, 0) · 순수 빨강 (53.24, 80.09, 67.20)
  dE     동일색 0 · 흰/검 100
"""
import math
import random
import sys

# ============================================================================
# 1. sRGB / 휘도 / 대비
# ============================================================================


def hex2rgb(h):
    """'#RRGGBB' 또는 0xRRGGBB -> (0..255, 0..255, 0..255)"""
    if isinstance(h, str):
        h = int(h.lstrip("#"), 16)
    return ((h >> 16) & 0xFF, (h >> 8) & 0xFF, h & 0xFF)


def rgb2hex(c):
    r, g, b = (int(round(max(0, min(255, v)))) for v in c)
    return "#%02X%02X%02X" % (r, g, b)


def lin(c):
    """0..1 sRGB 채널 -> 선형."""
    c = max(0.0, min(1.0, c))
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def L(rgb):
    """상대휘도 (WCAG 2.x)."""
    return 0.2126 * lin(rgb[0] / 255) + 0.7152 * lin(rgb[1] / 255) + 0.0722 * lin(rgb[2] / 255)


def CR(a, b):
    """대비비. 인자는 (0..255) 3튜플."""
    la, lb = L(a), L(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


# ============================================================================
# 2. Unity HSV 포트  (UnityEngine.Color.RGBToHSV / HSVToRGB)
#    Unity는 h,s,v 를 전부 0..1로 다룬다. 여기서도 같게 둔다.
# ============================================================================


def rgb_to_hsv(rgb):
    """(0..255)^3 -> (h, s, v) 각 0..1. Unity Color.RGBToHSV와 같은 정의."""
    r, g, b = (v / 255.0 for v in rgb)
    mx, mn = max(r, g, b), min(r, g, b)
    v = mx
    d = mx - mn
    s = 0.0 if mx <= 0.0 else d / mx
    if d <= 0.0:
        h = 0.0
    elif mx == r:
        h = ((g - b) / d) % 6.0
    elif mx == g:
        h = (b - r) / d + 2.0
    else:
        h = (r - g) / d + 4.0
    return (h / 6.0) % 1.0, s, v


def hsv_to_rgb(h, s, v):
    """(0..1)^3 -> (0..255)^3 정수. Unity Color.HSVToRGB와 같은 정의 + 8bit 반올림."""
    h = h % 1.0
    c = v * s
    hp = h * 6.0
    x = c * (1.0 - abs((hp % 2.0) - 1.0))
    i = int(math.floor(hp)) % 6
    r1, g1, b1 = [(c, x, 0.0), (x, c, 0.0), (0.0, c, x),
                  (0.0, x, c), (x, 0.0, c), (c, 0.0, x)][i]
    m = v - c
    return tuple(int(round((q + m) * 255)) for q in (r1, g1, b1))


def hue_deg(rgb):
    return rgb_to_hsv(rgb)[0] * 360.0


# ============================================================================
# 3. ItemCatalog.WornColor 포트
#    Assets/_Project/Scripts/Core/ItemCatalog.cs:633-649
# ============================================================================

WORN_S_FLOOR = 0.42
WORN_V_FLOOR = 0.55
WORN_V_CEIL = 0.80


def worn(rgb):
    """카탈로그 색 -> 몸에 칠할 색. (InkTone 표식은 여기서 다루지 않는다 — 색이 아니라 지시다.)"""
    h, s, v = rgb_to_hsv(rgb)
    s = max(s, WORN_S_FLOOR)
    v = min(max(v, WORN_V_FLOOR), WORN_V_CEIL)
    return hsv_to_rgb(h, s, v)


def is_worn_fixed(rgb):
    """WornColor가 이 색을 **바꾸지 않는가**(카드색 == 몸색)."""
    return worn(rgb) == tuple(rgb)


def fill_outline(rgb):
    """AccessoryShapeBuilder.FillOutlineColor 포트 — 채움의 윤곽선(그늘색) x0.62."""
    return tuple(int(round(v * 0.62)) for v in rgb)


# ============================================================================
# 4. CIELAB (D65) + CIE76 dE*ab
# ============================================================================

_WP = (0.95047, 1.00000, 1.08883)   # D65 2도 관측자


def rgb2xyz(rgb):
    r, g, b = (lin(v / 255.0) for v in rgb)
    x = 0.4124564 * r + 0.3575761 * g + 0.1804375 * b
    y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b
    z = 0.0193339 * r + 0.1191920 * g + 0.9503041 * b
    return x, y, z


def _f(t):
    return t ** (1.0 / 3.0) if t > 216.0 / 24389.0 else (841.0 / 108.0) * t + 4.0 / 29.0


def lab(rgb):
    x, y, z = rgb2xyz(rgb)
    fx, fy, fz = _f(x / _WP[0]), _f(y / _WP[1]), _f(z / _WP[2])
    return (116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz))


def dE(a, b):
    """CIE76 dE*ab. (CIEDE2000은 검증용 기준표를 이 환경에서 확보할 수 없어 쓰지 않는다 —
    검증 못 하는 계산기는 이 저장소에서 쓸 수 없다.)"""
    la, lb_ = lab(a), lab(b)
    return math.sqrt(sum((la[i] - lb_[i]) ** 2 for i in range(3)))


# ============================================================================
# 5. 교정 — 실패하면 죽는다
# ============================================================================


def calibrate(verbose=True):
    checks = []

    def chk(name, got, want, tol):
        checks.append((name, got, want, tol, abs(got - want) <= tol))

    W, K = (255, 255, 255), (0, 0, 0)
    chk("대비 흰/검", CR(W, K), 21.0, 0.0005)
    chk("대비 동일색(흰)", CR(W, W), 1.0, 0.0005)
    chk("대비 동일색(#0D0C0B)", CR(hex2rgb("#0D0C0B"), hex2rgb("#0D0C0B")), 1.0, 0.0005)
    chk("대비 #767676/흰", CR(hex2rgb("#767676"), W), 4.5422, 0.0005)
    chk("대비 #000/#808080", CR(K, hex2rgb("#808080")), 5.3172, 0.0005)

    # HSV 왕복 항등 (경계색 + 무작위)
    rng = random.Random(20260902)
    probes = [W, K, (255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 0),
              (0, 255, 255), (255, 0, 255), (128, 128, 128), (1, 2, 3)]
    probes += [tuple(rng.randrange(256) for _ in range(3)) for _ in range(256)]
    worst = 0
    for p in probes:
        h, s, v = rgb_to_hsv(p)
        q = hsv_to_rgb(h, s, v)
        worst = max(worst, max(abs(q[i] - p[i]) for i in range(3)))
    chk("HSV 왕복 최대오차(채널)", float(worst), 0.0, 0.0)

    # ★ 타 담당자(design-equipment)의 독립 실측값으로 WornColor 포트를 교정한다.
    chk("WornColor #D8B27A -> #CCA873",
        0.0 if rgb2hex(worn(hex2rgb("#D8B27A"))) == "#CCA873" else 1.0, 0.0, 0.0)
    chk("WornColor #F0C25C -> #CCA54E",
        0.0 if rgb2hex(worn(hex2rgb("#F0C25C"))) == "#CCA54E" else 1.0, 0.0, 0.0)

    # LAB
    chk("LAB 흰 L*", lab(W)[0], 100.0, 0.01)
    chk("LAB 흰 a*", lab(W)[1], 0.0, 0.01)
    chk("LAB 검 L*", lab(K)[0], 0.0, 0.01)
    chk("LAB 빨강 L*", lab((255, 0, 0))[0], 53.24, 0.02)
    chk("LAB 빨강 a*", lab((255, 0, 0))[1], 80.09, 0.05)
    chk("LAB 빨강 b*", lab((255, 0, 0))[2], 67.20, 0.05)
    chk("dE 동일색", dE(W, W), 0.0, 1e-9)
    chk("dE 흰/검", dE(W, K), 100.0, 0.01)

    ok = all(c[4] for c in checks)
    if verbose:
        print("=== 색 계산기 교정 ===")
        for name, got, want, tol, p in checks:
            print(f"  {'PASS' if p else 'FAIL'}  {name:32s} {got:12.4f}  (정답 {want}, 허용 {tol})")
        print(f"  교정 판정: {'유효' if ok else '무효'}\n")
    if not ok:
        sys.exit("교정 실패 — 이 스크립트가 내는 모든 숫자를 폐기하십시오.")
    return True


if __name__ == "__main__":
    calibrate()
    print("colorlab 교정 통과. audit.py / packs.py에서 import해 쓰십시오.")
