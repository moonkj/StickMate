# -*- coding: utf-8 -*-
"""⑤ 인계본 색 모델을 **우리 색 게이트**로 잰다 (design-equipment, 2026-09-03).

인계본 모델 : 채움 = 등급색 그라디언트(위 34% → 아래 8% 알파) · 외곽선 = 아이템색 ·
              좌상단 흰 42% 하이라이트 · 망토류만 재질색 #D2402F 강제
우리   모델 : 주색/보조색 = 25색 팔레트(재질색) · WornColor 가 몸에서 잉크로 바꾼다 ·
              자립 대역 L ∈ [0.1632, 0.2396] (배경 4종 CR ≥ 3.0) · 주↔보조 ΔE ≥ 7.8

★ 알파가 있는 색은 **배경과 합성한 뒤** 재야 한다. 알파를 무시하고 원색만 재면
  "게이트 통과"가 거짓이 된다 — 그 함정을 이 파일이 명시적으로 연다.
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "design", "art", "verify"))
import colorlab as C, band

LO, HI, ROWS = band.limits()
FLOOR = 3.0

C.calibrate()
print("╔══ 교정 ══╗")
print("  [OK] colorlab 교정 통과 · 대역 L ∈ [%.4f, %.4f]" % (LO, HI))
print("  [OK] 흰/검 대비 %.4f · 동일색 %.4f" % (C.CR((255,)*3, (0,)*3), C.CR((128,)*3, (128,)*3)))

# ── 인계본이 선언한 색 전부 ────────────────────────────────────────────────
HANDOFF = [
    ("아이템색 C (외곽선 기본)",   "#E8E2D6", "icon"),
    ("강조색 A (기본)",           "#C8A15A", "icon"),
    ("망토 재질색 MAT",           "#D2402F", "icon"),
    ("착용 등급색 · 일반",         "#D8B27A", "worn"),
    ("착용 등급색 · 희귀",         "#7FB0F2", "worn"),
    ("착용 등급색 · 영웅",         "#C08FEC", "worn"),
    ("착용 등급색 · 전설",         "#F0C25C", "worn"),
    ("망토 뒤판 채움",            "#A8332A", "worn"),
    ("망토 뒤판 보더",            "#7E1F17", "worn"),
    ("망토 칼라 채움",            "#8E241C", "worn"),
    ("망토 클래스프",             "#C8A15A", "worn"),
    ("하이라이트(흰 42%)",        "#FFFFFF", "worn"),
]

print()
print("╔══ 1. 자립 대역 — 인계본 색이 우리 몸에서 사는가 ══╗")
print("%-26s %-9s %8s %9s %9s %9s %9s  %s" %
      ("색", "hex", "L", "흰바탕", "검바탕", "종이무대", "목탄무대", "판정"))
band_bad = []
for name, hx, scope in HANDOFF:
    rgb = C.hex2rgb(hx); l = C.L(rgb)
    crs = [C.CR(rgb, b) for _, b in band.BACKDROPS]
    ok = LO <= l <= HI
    worst = min(crs)
    if not ok: band_bad.append((name, hx, l, worst))
    print("%-26s %-9s %8.4f %9.2f %9.2f %9.2f %9.2f  %s" %
          (name, hx, l, crs[0], crs[1], crs[2], crs[3],
           "OK" if ok else "★대역밖 (최악 %.2f:1)" % worst))
print("  ⇒ 인계본 색 %d개 중 **대역 밖 %d개**" % (len(HANDOFF), len(band_bad)))

# ── WornColor 항등 ────────────────────────────────────────────────────────
print()
print("╔══ 2. WornColor 항등 — 카드 색과 몸 색이 바이트 단위로 같은가 ══╗")
print("  (우리 출하 27색은 전부 항등이다. 항등이 깨지면 카드/몸 이중 정의가 생긴다.)")
print("%-26s %-9s %-9s %8s %s" % ("색", "카드", "→ 몸", "ΔE", "판정"))
ident_bad = 0
for name, hx, scope in HANDOFF:
    rgb = C.hex2rgb(hx)
    worn = C.worn(rgb)
    hw = C.rgb2hex(worn)
    de = C.dE(rgb, worn)
    same = hw.upper() == hx.upper()
    if not same: ident_bad += 1
    print("%-26s %-9s %-9s %8.2f %s" % (name, hx, hw, de, "항등" if same else "★비항등"))
print("  ⇒ 비항등 %d/%d" % (ident_bad, len(HANDOFF)))

# ── 알파 합성 ─────────────────────────────────────────────────────────────
print()
print("╔══ 3. ★ 알파 합성 — 인계본 채움을 **그 알파 그대로** 몸에 얹으면 ══╗")
print("  인계본 카드 무대는 #0F0D0C 단색이다. 우리 몸 뒤에는 머리 원반·획·머리카락이 있다.")
print("  아래는 등급색 '전설 #F0C25C'를 여러 알파로 **네 배경 위에** 합성한 결과다.")
def over(fg, bg, a):
    return tuple(fg[i] * a + bg[i] * (1 - a) for i in range(3))
print("%8s %10s %9s %9s %9s %9s  %s" %
      ("알파", "합성색(검)", "흰바탕", "검바탕", "종이무대", "목탄무대", "판정"))
GRADE = C.hex2rgb("#F0C25C")
for a in (0.08, 0.14, 0.16, 0.22, 0.34, 0.45, 0.55, 0.62, 0.80, 1.00):
    crs = []
    for _, b in band.BACKDROPS:
        crs.append(C.CR(over(GRADE, b, a), b))
    comp_k = C.rgb2hex(over(GRADE, (0, 0, 0), a))
    ok = min(crs) >= FLOOR
    print("%8.2f %10s %9.2f %9.2f %9.2f %9.2f  %s" %
          (a, comp_k, crs[0], crs[1], crs[2], crs[3],
           "OK" if ok else "★ 미달 (최악 %.2f:1)" % min(crs)))
print("  ★ 알파가 붙은 채움은 **자기 색이 아니라 배경색이 된다.** 대비가 알파에 비례해 무너진다.")
print("     0.34(그라디언트 위끝)에서도 네 배경 전부 미달이고, 0.08(아래끝)은 사실상 투명이다.")

# ── 그늘색 ────────────────────────────────────────────────────────────────
print()
print("╔══ 4. 그늘 윤곽선 — 우리 FillOutlineShadeFactor 0.28 ══╗")
print("  인계본은 외곽선을 **아이템색 그대로** 쓴다(채움보다 밝을 수도 있다).")
print("  우리는 채움색 × 0.28 로 **어둡게** 만들어 경계를 세운다. 두 방식의 대비를 잰다.")
print("%-22s %-9s %-9s %8s %8s" % ("아이템색", "채움", "외곽선", "CR", "ΔE"))
for name, hx, scope in HANDOFF[:3]:
    rgb = C.hex2rgb(hx)
    ours = C.fill_outline(rgb)
    print("%-22s %-9s %-9s %8.2f %8.2f  ← 우리(x0.28)" %
          (name, hx, C.rgb2hex(ours), C.CR(rgb, ours), C.dE(rgb, ours)))
    print("%-22s %-9s %-9s %8.2f %8.2f  ← 인계본(동일색)" %
          ("", hx, hx, C.CR(rgb, rgb), C.dE(rgb, rgb)))

# ── 주↔보조 ΔE ────────────────────────────────────────────────────────────
print()
print("╔══ 5. 주↔보조 변별 ΔE (하한 7.8) ══╗")
pairs = [("아이템색 C ↔ 강조색 A", "#E8E2D6", "#C8A15A"),
         ("망토 뒤판 ↔ 보더",       "#A8332A", "#7E1F17"),
         ("망토 뒤판 ↔ 칼라",       "#A8332A", "#8E241C"),
         ("망토 뒤판 ↔ 클래스프",    "#A8332A", "#C8A15A"),
         ("등급 일반 ↔ 강조 A",     "#D8B27A", "#C8A15A"),
         ("등급 전설 ↔ 강조 A",     "#F0C25C", "#C8A15A")]
for name, a, b in pairs:
    de = C.dE(C.hex2rgb(a), C.hex2rgb(b))
    print("  %-24s %-9s %-9s ΔE %6.2f  %s" % (name, a, b, de, "OK" if de >= 7.8 else "★ 미달"))
