#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Ø36 축소 폴백이 부채꼴 안의 모든 조각에 무엇을 하는가 — pt / 물리픽셀 / 시각각(′) 3단 환산.

★★ 2026-09-06 자기 정정 — 이 파일 <마지막 블록>의 두 줄은 폐기됐다. `r27_shrink_fg3.py` 를 보라.
    폐기 대상: «알파0 골 = 골 − 2×0.5pt» 와 그로부터 나온 «빈 화소 열 확률 Ø44 100 % / Ø36 65 %».
    이유: 램프 모형이 프로덕션과 달랐다. `UiChrome.Capsule()`/`CircleSprite()` 의 텍셀 굽기는
      alpha = clamp01((core − d)/feather + 0.5)
    라 <알파 0.5 등고선이 정확히 코어 가장자리>다. 램프는 가장자리를 <가운데 두고> ±feather/2로
    걸치므로 두 코어 사이에 남는 알파 0 구간은 «g − 1.0pt»가 아니라 **«g − 0.5pt»**다.
    (같은 오류가 DESIGN_FAN_MENU_ICONS §1-3 에도 있다 — r27 권고2.)
    정정된 값: Ø44 골 3.00pt → 알파0 2.50pt · Ø36 → 2.05px @1×.
    ★ 오류의 방향은 <안전한 쪽>이었다(실제보다 0.5pt 비관적) — 그때 통과한 것은 지금도 통과다.
    아래 표(조각별 pt/px/시각각 환산)는 배율 산술뿐이라 <그대로 유효하다>.

자(尺)는 전부 이 저장소가 <이미 쓰고 있는 것>만 쓴다:
  · UiChrome.MinTextContrast 4.5 / MinNonTextContrast 3.0
  · UI_SURFACE_SPEC §15 자② : 중심시 MAR 1.0′ · 5° 이심 MAR 2.5~3.0′ · 색 판별 안정선 10′
  · 그 표의 환산: 96dpi 100% 60cm 에서 2pt = 3.03′  ->  1.515 ′/pt
                 최악(4K27 150%)                    ->  1.34  ′/pt
  · StickConfig 획 하한 : 낱선 2.00pt / 채움경계선 1.00pt / 인계본획 1.00pt
"""
import sys
from PIL import Image, ImageDraw, ImageFont

FONT_BOLD = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"

BUTTON_D, SHRUNK_D = 44.0, 36.0                  # GearRadialMenuWidget.cs:212-213
K = SHRUNK_D / BUTTON_D
ARCMIN_TYPICAL = 3.03 / 2.0                      # UI_SURFACE_SPEC §15 자② 96dpi/100%/60cm
ARCMIN_WORST = 2.68 / 2.0                        # 같은 표의 «최악» 열
MAR_FOVEAL = 1.0
MAR_ECCENTRIC = 3.0                              # 5° 이심의 나쁜 쪽

# 부채꼴 안에서 크기를 가진 것 전부 (Ø44 기준 pt) — 출처를 함께 적는다.
ITEMS = [
    ("버튼 지름",              44.0,  "ButtonDiameterPoints :212"),
    ("호버 지름",              48.0,  "HoverScale 48/44 :214"),
    ("버튼 테두리 두께",        1.2,  "AddCircle(Border, …, 1.2f) :2487"),
    ("심볼 상자",              24.0,  "SymbolBoxPoints :466"),
    ("심볼 획 (기본 W)",        2.0,  "SymbolStroke :454"),
    ("심볼 획 (디테일 0.75W)",  1.5,  "SymbolStrokeDetail :458"),
    ("심볼 획 (두꺼움 1.5W)",   3.0,  "SymbolStrokeHeavy :461"),
    ("가장자리 램프/변",        0.5,  "UiChrome.EdgeFeather :847"),
    ("전원 링 지름",           22.0,  "PowerRingDiameterPoints :2871"),
    ("스톱워치 링 지름",        20.0,  "StopwatchRingDiameterPoints :2552"),
    ("배지 지름",              16.0,  "Badge.sizeDelta :2516"),
    ("배지 글자 em",           10.0,  "AddText(…, 10, …) :2524"),
    ("덩어리 간 최소 골",       3.02,  "r26_gate.out.txt «최소 3.02pt = 1.51W»"),
]


def calibrate():
    ok = True
    a = 2.0 * ARCMIN_TYPICAL
    print(f"  교정1 2pt -> {a:.2f}′   (UI_SURFACE_SPEC §15 표 = 3.03′)")
    ok &= abs(a - 3.03) < 0.01
    b = 4.0 * ARCMIN_TYPICAL
    print(f"  교정2 리본 높이 4pt -> {b:.2f}′  (같은 표 = 6.06′)")
    ok &= abs(b - 6.06) < 0.01
    c = 133.0 * ARCMIN_TYPICAL
    print(f"  교정3 전설 리본 133pt -> {c:.1f}′  (같은 표 = 201.4′)")
    ok &= abs(c - 201.4) < 0.2
    d = 2.0 * ARCMIN_WORST
    print(f"  교정4 최악 열 2pt -> {d:.2f}′   (같은 표 최악 = 2.68′)")
    ok &= abs(d - 2.68) < 0.01
    # 축소 계수 자체
    print(f"  교정5 k = {SHRUNK_D:.0f}/{BUTTON_D:.0f} = {K:.6f}  (coder 보고 «0.8181배» = "
          f"{abs(K - 0.8181) < 0.0002})")
    ok &= abs(K - 0.8181) < 0.0002
    return ok


def font_metrics():
    """Arial Bold 의 캡 높이 / 숫자 세로획 폭을 em 대비로 <실측>한다(암기값 금지)."""
    N = 512
    f = ImageFont.truetype(FONT_BOLD, N)
    im = Image.new("L", (N * 3, N * 3))
    ImageDraw.Draw(im).text((N // 2, N // 2), "0", font=f, fill=255)
    bbox = im.getbbox()
    cap = (bbox[3] - bbox[1]) / N

    im2 = Image.new("L", (N * 3, N * 3))
    ImageDraw.Draw(im2).text((N // 2, N // 2), "1", font=f, fill=255)
    b2 = im2.getbbox()
    px = im2.load()
    y = (b2[1] + b2[3]) // 2                     # 「1」의 세로획 한가운데
    run = best = 0
    for x in range(b2[0], b2[2]):
        run = run + 1 if px[x, y] >= 128 else 0
        best = max(best, run)
    stem = best / N
    return cap, stem


def main():
    print("=" * 104)
    print("교정 (이 저장소가 이미 발표한 값으로 자를 맞춘다)")
    print("=" * 104)
    if not calibrate():
        print("\n★ 교정 실패 — 아래 숫자 전부 폐기."); sys.exit(2)
    print("  => 교정 통과.\n")

    cap, stem = font_metrics()
    print(f"  Arial Bold 실측: 캡 높이 {cap:.4f} em · 숫자 세로획 {stem:.4f} em\n")

    print("=" * 104)
    print(f"Ø44 -> Ø36 (균일 배율 {K:.4f}) — 모든 조각")
    print("=" * 104)
    print(f"{'조각':<24}{'Ø44 pt':>9}{'Ø36 pt':>9}{'Ø36 @1x':>10}{'Ø36 @2x':>10}"
          f"{'Ø44 각(′)':>11}{'Ø36 각(′)':>11}  {'출처'}")
    print("-" * 104)
    for name, v44, src in ITEMS:
        v36 = v44 * K
        print(f"{name:<22}{v44:>9.2f}{v36:>9.2f}{v36:>9.2f}px{v36*2:>9.2f}px"
              f"{v44*ARCMIN_TYPICAL:>11.2f}{v36*ARCMIN_TYPICAL:>11.2f}  {src}")

    print()
    print("=" * 104)
    print("배지 글자 — em 이 아니라 «실제로 눈에 걸리는 크기»")
    print("=" * 104)
    for label, em in (("Ø44", 10.0), ("Ø36", 10.0 * K)):
        c = em * cap
        s = em * stem
        print(f"  {label}: em {em:>5.2f}pt · 캡높이 {c:>5.2f}pt = {c*ARCMIN_TYPICAL:>5.2f}′ "
              f"(최악 {c*ARCMIN_WORST:>5.2f}′) · 세로획 {s:>5.2f}pt = "
              f"{s*ARCMIN_TYPICAL:>5.2f}′ (최악 {s*ARCMIN_WORST:>5.2f}′)")
    print(f"  기준선: 중심시 MAR {MAR_FOVEAL:.1f}′ · 5° 이심 MAR {MAR_ECCENTRIC:.1f}′ · "
          f"판독 여유선 = MAR x 5 ≈ {MAR_FOVEAL*5:.0f}′(캡 높이 기준 20/20 문턱)")

    print()
    print("=" * 104)
    print("덩어리 간 «알파 0 인 골» — ★★ 이 블록은 폐기됐다(2026-09-06). r27_shrink_fg3.py 를 보라.")
    print("=" * 104)
    print("  폐기 이유: 램프를 <전부 코어 바깥>에 둔 모형이었다. 실제 텍셀 굽기는 알파 0.5 등고선이")
    print("  코어 가장자리에 놓이므로 알파0 골 = g − 0.5pt 다(아래는 g − 1.0pt 로 계산한 옛 값).")
    print("  정정값: Ø44 골 3.00pt → 알파0 2.50pt / Ø36 → 2.05px @1× (빈 화소 열은 어느 위상에서도 생긴다).")
    print("  방향은 안전한 쪽이었다 — 실제보다 비관적이라 «통과» 판정은 뒤집히지 않는다.")
    print("-" * 104)
    gap44 = 3.02
    feather = 0.5
    zero44 = gap44 - 2 * feather
    zero36 = zero44 * K
    print(f"  Ø44: 골 {gap44:.2f}pt − 램프 2x{feather}pt = 알파0 구간 {zero44:.2f}pt "
          f"= {zero44:.2f}px @1x / {zero44*2:.2f}px @2x")
    print(f"  Ø36: 골 {gap44*K:.2f}pt − 램프 2x{feather*K:.3f}pt = 알파0 구간 {zero36:.2f}pt "
          f"= {zero36:.2f}px @1x / {zero36*2:.2f}px @2x")
    print(f"  @1x 에서 «완전히 빈 화소 열»이 생길 확률(위상 무작위) : "
          f"Ø44 {min(1.0, max(0.0, zero44-1)):.0%} 이상 확정, Ø36 {max(0.0, zero36-1):.0%}")
    print(f"  FG-3 절대 하한 3.0pt 대비: Ø44 {gap44:.2f}pt 통과 / "
          f"Ø36 {gap44*K:.2f}pt {'통과' if gap44*K >= 3.0 else '★미달'}")
    print(f"  FG-3 상대 하한 1.5W 대비: W 도 함께 줄므로 {gap44/2.0:.2f}W 로 <불변> — 통과")


if __name__ == "__main__":
    main()
