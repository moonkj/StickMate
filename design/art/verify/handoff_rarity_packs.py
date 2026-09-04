# -*- coding: utf-8 -*-
"""R11 — 인계본 등급색 RAR_A · 팩 스와치 6색 vs 우리 팔레트 (design-art, 2026-09-03)

★ 자 교정 먼저. 교정이 깨지면 아무 숫자도 내지 않고 죽는다.
★ 이번 라운드는 **LCh 자를 새로 쓴다**(§29-4가 확정한 가족 판정자). HSV 자와 이름이 겹치므로
  출력에 매번 어느 자인지 적는다. LCh 자도 알려진 값 + 이미 출하된 값으로 교정한다.
"""
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from colorlab import (hex2rgb, rgb2hex, L, CR, lab, dE, hue_deg, worn, is_worn_fixed,
                      rgb_to_hsv, calibrate)
import shipped

FAIL = []

ITEMCATALOG = os.path.join(shipped.ROOT if hasattr(shipped, "ROOT") else "",
                           "Assets/_Project/Scripts/Core/ItemCatalog.cs")


def prod_alpha(token):
    """UiChrome.cs 에서 토큰의 **α를 직접 읽는다.** 값을 베끼지 않는다.

    ★ 이 함수가 존재하는 이유: 2026-09-03 에 `coder-ui` 가 CardBorderWorn 의 α를
      0.55 -> 0.75 로 올렸는데, 이 파일 198행이 «α=0.55» 라고 **문자열로 베껴** 두고 있었다.
      프로덕션이 움직여도 도구는 조용히 자기 말만 했다 — CLAUDE.md 가 경고하는 바로 그 형태다.
      찾지 못하면 **죽는다**(기본값으로 물러나지 않는다 — 폴백이 거짓 초록을 만든다, §13-4).
    """
    src = open(shipped.UICHROME, encoding="utf-8").read()
    m = re.search(r"public static readonly Color " + token +
                  r"\s*=\s*new Color\(([0-9.f]+),\s*([0-9.f]+),\s*([0-9.f]+),\s*([0-9.f]+)\)", src)
    if not m:
        raise SystemExit("!! UiChrome.cs 에서 %s 를 못 찾았다 — 토큰 이름이 바뀌었다. 중단." % token)
    return float(m.group(4).rstrip("f"))


def prod_const_float(path, name):
    """`private const float NAME = 0.55f;` 같은 상수를 소스에서 직접 읽는다."""
    src = open(path, encoding="utf-8").read()
    m = re.search(r"const float " + name + r"\s*=\s*([0-9.]+)f", src)
    if not m:
        raise SystemExit("!! %s 에서 %s 를 못 찾았다. 중단." % (os.path.basename(path), name))
    return float(m.group(1))


def lch(c):
    """CIELAB -> LCh(ab). h는 도(0..360)."""
    Ls, a, b = lab(c)
    C = math.hypot(a, b)
    h = math.degrees(math.atan2(b, a)) % 360.0
    return (Ls, C, h)


def dH(c1, c2):
    """CIE ΔH* — 색상 성분만. sqrt(dE^2 - dL^2 - dC^2)."""
    L1, a1, b1 = lab(c1)
    L2, a2, b2 = lab(c2)
    C1, C2 = math.hypot(a1, b1), math.hypot(a2, b2)
    v = (a1 - a2) ** 2 + (b1 - b2) ** 2 - (C1 - C2) ** 2
    return math.sqrt(max(0.0, v))


def arc(h1, h2):
    d = abs(h1 - h2) % 360.0
    return min(d, 360.0 - d)


def over(fg_rgb, alpha, bg_rgb):
    """sRGB 공간 알파 합성(Unity UI 기본)."""
    return tuple(int(round(fg_rgb[i] * alpha + bg_rgb[i] * (1 - alpha))) for i in range(3))


def gray_level(c):
    """회색조 변환(상대휘도 기준) -> 0..255 sRGB 등가."""
    y = L(c)
    # 선형 -> sRGB
    s = 12.92 * y if y <= 0.0031308 else 1.055 * (y ** (1 / 2.4)) - 0.055
    return round(s * 255)


WORN_ALPHA = prod_alpha("CardBorderWorn")   # ★ 소스에서 읽는다. 여기에 숫자를 적지 않는다.
HOVER_ALPHA = prod_alpha("CardBorderHover")
CARDBORDER_ALPHA = prod_alpha("CardBorder")
RARITY_BORDER_ALPHA = prod_const_float(shipped.UICHROME, "RarityBorderAlpha")


# ===========================================================================
# 0. 교정
# ===========================================================================
print("=" * 78)
print("0. 자 교정 — 기본 자 (colorlab.calibrate)")
print("=" * 78)
if not calibrate(verbose=True):
    print("!! 기본 교정 실패 — 중단")
    sys.exit(1)

print()
print("=" * 78)
print("0-B. ★ LCh / ΔH* 자 교정 — 알려진 값")
print("=" * 78)
chk = []


def c2(name, got, want, tol):
    ok = abs(got - want) <= tol
    chk.append(ok)
    print("  %s %-52s %12.4f  (정답 %s, 허용 %s)" %
          ("PASS" if ok else "FAIL", name, got, want, tol))


# 순수 빨강의 LCh(ab): L 53.2408 / C 104.5518 / h 39.9990 (외부 검증 가능)
Ls, C, h = lch((255, 0, 0))
c2("LCh 순수빨강 L*", Ls, 53.2408, 0.01)
c2("LCh 순수빨강 C*", C, 104.5518, 0.01)
c2("LCh 순수빨강 h", h, 39.9990, 0.01)
Ls, C, h = lch((0, 0, 255))
c2("LCh 순수파랑 h", h, 306.2849, 0.01)   # sRGB blue Lab(32.30, 79.19, -107.86)
c2("LCh 중성회 C*", lch((128, 128, 128))[1], 0.0, 0.001)
c2("ΔH* 동일색", dH((200, 100, 50), (200, 100, 50)), 0.0, 1e-9)
# 순수 무채색 두 개는 색상 성분이 0 -> ΔH*=0
c2("ΔH* 흰/검(무채끼리)", dH((255, 255, 255), (0, 0, 0)), 0.0, 1e-9)
# ΔE^2 = ΔL^2 + ΔC^2 + ΔH^2 항등 (CIE76에서 성립)
A, B = (200, 90, 40), (60, 140, 200)
lhs = dE(A, B) ** 2
rhs = (lch(A)[0] - lch(B)[0]) ** 2 + (lch(A)[1] - lch(B)[1]) ** 2 + dH(A, B) ** 2
c2("ΔE² = ΔL²+ΔC²+ΔH² 항등", lhs - rhs, 0.0, 1e-6)
# 알파 합성 항등
c2("합성 α=1 항등", sum(abs(over((10, 20, 30), 1.0, (200, 200, 200))[i] - (10, 20, 30)[i]) for i in range(3)), 0, 0)
c2("합성 α=0 항등", sum(abs(over((10, 20, 30), 0.0, (200, 200, 200))[i] - 200) for i in range(3)), 0, 0)
c2("회색조 흰", gray_level((255, 255, 255)), 255, 0)
c2("회색조 검", gray_level((0, 0, 0)), 0, 0)

print()
print("=" * 78)
print("0-C. ★ 출하된 값과의 대조 — 자와 대상이 갈라지는 것을 막는다")
print("=" * 78)
tok = shipped.uichrome_tokens()
print("  UiChrome.cs 파싱 토큰 %d개" % len(tok))

BRASS = hex2rgb("#C8A15A")
WORN_BORDER = hex2rgb("#5DA1F5")
LOCKED = hex2rgb("#8b939f")
RAMP = {"일반": hex2rgb("#9C978C"), "희귀": hex2rgb("#BCAC8B"),
        "영웅": hex2rgb("#DEC081"), "전설": hex2rgb("#FFD375")}
CARD = hex2rgb("#1B1F26")

# (1) 파싱한 토큰이 문서의 hex와 같은가
c2("출하 Accent == #C8A15A", sum(abs(tok["Accent"][0][i] - BRASS[i]) for i in range(3)), 0, 0)
c2("출하 CardBorderWorn RGB == #5DA1F5",
   sum(abs(tok["CardBorderWorn"][0][i] - WORN_BORDER[i]) for i in range(3)), 0, 0)
c2("출하 TextTertiary == #8b939f", sum(abs(tok["TextTertiary"][0][i] - LOCKED[i]) for i in range(3)), 0, 0)
# ★ §29 확정값 #FBE4C0 은 **아직 코드에 안 들어갔다**. 출하 실물은 여전히 #8FC3FF 다.
#   자를 출하 실물에 맞춘다(문서가 아니라 소스가 사실이다). 이 갈라짐 자체를 기록한다.
c2("출하 TextOnAccent == #8FC3FF (§29 판정 미반영 상태)",
   sum(abs(tok["TextOnAccent"][0][i] - hex2rgb("#8FC3FF")[i]) for i in range(3)), 0, 0)
print("  [사실] §29 확정값 #FBE4C0 vs 출하 실물 %s — coder-ui 미적용." % rgb2hex(tok["TextOnAccent"][0]))
# (2) 램프 4색은 RarityHex(0x....) 형태라 uichrome_tokens 정규식에 안 걸린다 — 소스에서 직접 읽는다
src = open(shipped.UICHROME, encoding="utf-8").read()
ramp_src = re.findall(r"RarityHex\(0x([0-9A-Fa-f]{6})\),\s*//\s*(\S+)", src)
print("  소스 램프 파싱:", ramp_src)
c2("소스 램프 4색", len(ramp_src), 4, 0)
for hx, ko in ramp_src:
    c2("  램프 %s == 문서값" % ko,
       sum(abs(hex2rgb("#" + hx)[i] - RAMP[ko][i]) for i in range(3)), 0, 0)

# (2-B) ★ 썩은 하드코딩 사냥 — 도구가 베낀 프로덕션 값이 아직 프로덕션과 같은가.
#       colorlab.py 는 WornColor 상자(S/V)를 상수로 들고 있다. 프로덕션이 움직이면 이 대조가 죽는다.
import colorlab as _CL
_IC = os.path.join(os.path.dirname(shipped.UICHROME), "..", "Core", "ItemCatalog.cs")
_IC = os.path.normpath(_IC)
_icsrc = open(_IC, encoding="utf-8").read()
for _nm, _tool in (("WornSaturationFloor", _CL.WORN_S_FLOOR),
                   ("WornValueFloor", _CL.WORN_V_FLOOR),
                   ("WornValueCeiling", _CL.WORN_V_CEIL)):
    _m = re.search(r"Worn\w*\s*=\s*([0-9.]+)f", "")   # placeholder
    _m = re.search(_nm + r"\s*=\s*([0-9.]+)f", _icsrc)
    if not _m:
        print("  !! ItemCatalog.cs 에서 %s 를 못 찾았다 — 상수 이름이 바뀌었다" % _nm)
        chk.append(False)
    else:
        c2("colorlab %s == 프로덕션" % _nm, _tool, float(_m.group(1)), 1e-9)
# 잉크 표식 2색도 소스에서 읽어 대조 (문서에 베껴 둔 #D6DBE3 / #8B939F 가 아직 맞는가)
for _nm, _want in (("InkTone", "#D6DBE3"), ("InkDimTone", "#8B939F")):
    _m = re.search(r"readonly Color " + _nm + r"\s*=\s*Rgb\(0x([0-9A-Fa-f]{6})\)", _icsrc)
    c2("ItemCatalog.%s == %s" % (_nm, _want), 0.0 if _m and ("#" + _m.group(1).upper()) == _want else 1.0, 0.0, 0.0)
# 알파 3종이 소스에서 실제로 읽혔는가 (0.0 이면 파싱이 죽은 것이다)
c2("prod_alpha(CardBorderWorn) > 0", 1.0 if WORN_ALPHA > 0 else 0.0, 1.0, 0.0)
c2("prod_alpha(CardBorderHover) > 0", 1.0 if HOVER_ALPHA > 0 else 0.0, 1.0, 0.0)
c2("RarityBorderAlpha > 0", 1.0 if RARITY_BORDER_ALPHA > 0 else 0.0, 1.0, 0.0)
print("  [소스에서 읽은 α] CardBorderWorn %.2f · CardBorderHover %.2f · CardBorder %.2f · RarityBorder %.2f"
      % (WORN_ALPHA, HOVER_ALPHA, CARDBORDER_ALPHA, RARITY_BORDER_ALPHA))

# (3) 이 저장소가 이미 낸 숫자 재현
c2("ΔE(브라스, CardBorderWorn) = UiChrome:167 의 90.52", dE(BRASS, WORN_BORDER), 90.52, 0.01)
c2("ΔE(브라스, 영웅) = UiChrome:187 의 12.86", dE(BRASS, RAMP["영웅"]), 12.86, 0.01)
c2("L(브라스) = §0 의 0.3851", L(BRASS), 0.3851, 0.0001)
c2("§29-3 ΔE(#E3B971, 영웅) = 7.735", dE(hex2rgb("#E3B971"), RAMP["영웅"]), 7.735, 0.001)
c2("§29-5 ΔE(#FBE4C0, 영웅) = 19.79", dE(hex2rgb("#FBE4C0"), RAMP["영웅"]), 19.79, 0.01)
c2("§29-5 L(#FBE4C0) = 0.7980", L(hex2rgb("#FBE4C0")), 0.7980, 0.0001)
c2("§27-2 ΔE(인계본 일반 #8A8F98, InkDimTone #8B939F) = 2.40",
   dE(hex2rgb("#8A8F98"), hex2rgb("#8B939F")), 2.40, 0.01)
# (4) LCh 자가 §29-4 표를 재현하는가 — 출하값 기준 대조
for nm, hx, wl, wc, wh in [("브라스", "#C8A15A", 68.4, 42.4, 82.0),
                           ("일반", "#9C978C", 62.6, 6.4, 91.5),
                           ("희귀", "#BCAC8B", 70.9, 19.1, 88.4),
                           ("영웅", "#DEC081", 78.9, 35.7, 86.5),
                           ("전설", "#FFD375", 86.6, 52.0, 85.0)]:
    Ls, C, hh = lch(hex2rgb(hx))
    c2("§29-4 LCh %s L*/C*/h" % nm, abs(Ls - wl) + abs(C - wc) + abs(hh - wh), 0.0, 0.16)
# (5) HSV 자와 LCh 자가 서로 다른 값을 낸다는 사실(§29-1) 재현
c2("§29-1 HSV h(#8FC3FF) = 212.1", hue_deg(hex2rgb("#8FC3FF")), 212.1, 0.1)
c2("§29-1 LCh h(#8FC3FF) = 267.2", lch(hex2rgb("#8FC3FF"))[2], 267.2, 0.1)
c2("§29-5 ΔH*(#FBE4C0, 브라스) = 0.65", dH(hex2rgb("#FBE4C0"), BRASS), 0.65, 0.01)
# (6) 팩 12색(처방 C) 재현 — §13-3
PACKS = {
    "오피스 워커": (222.0, "#456ECC", "#6080CC"),
    "사이버 아포칼립스": (172.0, "#009682", "#518C84"),
    "네온 낙서": (312.0, "#CC1BA9", "#9C5A8E"),
    "스포츠": (8.0, "#CC3F29", "#9E655C"),
    "컬러 잉크": (268.0, "#9768CC", "#8563AB"),
    "밀리터리": (80.0, "#639400", "#798C51"),
}
pack12 = []
for k, (H, m, s) in PACKS.items():
    mr, sr = hex2rgb(m), hex2rgb(s)
    shade = tuple(int(round(v * 0.62)) for v in mr)
    pack12 += [(k + "·주", mr), (k + "·보조", sr)]
    c2("팩 %s 주색 HSV h == %g" % (k, H), hue_deg(mr), H, 0.6)
c2("§13-3 팩 12색 자립대역 12/12",
   sum(1 for _, c in pack12 if 0.1632 <= L(c) <= 0.2396), 12, 0)
c2("§13-3 팩 12색 WornColor 항등 12/12",
   sum(1 for _, c in pack12 if is_worn_fixed(c)), 12, 0)
mind = min(dE(a, b) for i, (_, a) in enumerate(pack12) for _, b in pack12[i + 1:])
c2("§13-3 팩 12색 내부 최소 ΔE = 13.34", mind, 13.34, 0.01)
c2("§28-1 램프4 ↔ 팩12 최소 ΔE = 24.14",
   min(dE(r, c) for r in RAMP.values() for _, c in pack12), 24.14, 0.01)
c2("§28-1 브라스 ↔ 팩12 최소 ΔE = 29.32",
   min(dE(BRASS, c) for _, c in pack12), 29.32, 0.01)

if not all(chk):
    print("\n!! 교정 실패 — 아래 숫자는 전부 폐기. 중단한다.")
    sys.exit(1)
print("\n  ★ 교정 판정: 유효 (%d/%d)" % (sum(chk), len(chk)))


DISC = 7.8        # 변별 하한 (§27-8)
IDENT = 48.6      # 「두 물건」 식별 하한 (§27-8)
BAND = (0.1632, 0.2396)
CHROME = 0.30
SWBG = hex2rgb("#0E0C0B")   # 인계본 스와치 칩 배경 (line 378)

RAR_A = [("일반", "#8A8F98"), ("희귀", "#6E9BE8"), ("영웅", "#B07BE0"), ("전설", "#E0B24A")]
OURS = [("일반", "#9C978C"), ("희귀", "#BCAC8B"), ("영웅", "#DEC081"), ("전설", "#FFD375")]
SWATCH = [("office", "#C8A15A"), ("cyber", "#6E9BE8"), ("neon", "#B07BE0"),
          ("sport", "#8FBF6A"), ("ink", "#D2402F"), ("mil", "#8A8578")]
WORN_COMP = over(WORN_BORDER, WORN_ALPHA, CARD)


def band_of(c):
    l = L(c)
    if l > CHROME:
        return "크롬"
    if BAND[0] <= l <= BAND[1]:
        return "아트"
    if BAND[1] < l <= CHROME:
        return "해자"
    return "대역밖(아래)"


print()
print("=" * 78)
print("1. 뭉치 ① — 인계본 등급색 RAR_A 를 예약색에 대고 잰다")
print("=" * 78)
print("  예약색: CardBorderWorn(착용중) #5DA1F5 α%.2f(소스에서 읽음) → 카드면 합성 %s"
        % (WORN_ALPHA, rgb2hex(WORN_COMP)))
print("          TextTertiary(잠김)   #8b939f")
print()
print("%-6s %-9s %8s %7s %7s %8s | %8s %8s %8s | %8s %8s" %
      ("등급", "hex", "L", "L*", "C*", "h(LCh)", "ΔE→파랑", "ΔE→합성", "ΔE→회색", "호→파랑", "호→회색"))
for ko, hx in RAR_A:
    c = hex2rgb(hx)
    Ls, C, h = lch(c)
    print("%-6s %-9s %8.4f %7.1f %7.1f %8.1f | %8.2f %8.2f %8.2f | %7.1f° %7.1f°" %
          (ko, hx, L(c), Ls, C, h, dE(c, WORN_BORDER), dE(c, WORN_COMP), dE(c, LOCKED),
           arc(h, lch(WORN_BORDER)[2]), arc(h, lch(LOCKED)[2])))
print()
print("  [대조] 우리 브라스 램프")
print("%-6s %-9s %8s %7s %7s %8s | %8s %8s %8s | %8s %8s" %
      ("등급", "hex", "L", "L*", "C*", "h(LCh)", "ΔE→파랑", "ΔE→합성", "ΔE→회색", "호→파랑", "호→회색"))
for ko, hx in OURS:
    c = hex2rgb(hx)
    Ls, C, h = lch(c)
    print("%-6s %-9s %8.4f %7.1f %7.1f %8.1f | %8.2f %8.2f %8.2f | %7.1f° %7.1f°" %
          (ko, hx, L(c), Ls, C, h, dE(c, WORN_BORDER), dE(c, WORN_COMP), dE(c, LOCKED),
           arc(h, lch(WORN_BORDER)[2]), arc(h, lch(LOCKED)[2])))

print()
print("1-2. 그 밖 축")
print("%-6s %-9s | 카드면 CR | 회색조 | 팩12 최근접ΔE | 브라스ΔE | 대역" % ("등급", "hex"))
for nm, tbl in (("인계본", RAR_A), ("우리", OURS)):
    for ko, hx in tbl:
        c = hex2rgb(hx)
        pmin = min((dE(c, pc), pn) for pn, pc in pack12)
        print("%-4s %-4s %-9s | %9.2f | %6d | %6.2f (%s) | %8.2f | %s" %
              (nm, ko, hx, CR(c, CARD), gray_level(c), pmin[0], pmin[1], dE(c, BRASS), band_of(c)))

print()
print("1-3. 회색조 서열 (상대휘도 기준 sRGB 등가) — 단조 증가여야 등급이 흑백에서도 읽힌다")
for nm, tbl in (("인계본 RAR_A", RAR_A), ("우리 램프", OURS)):
    g = [gray_level(hex2rgb(h)) for _, h in tbl]
    mono = all(g[i] < g[i + 1] for i in range(3))
    steps = [g[i + 1] - g[i] for i in range(3)]
    print("  %-12s %s  단조=%s  단차 %s  최소단차 %d" % (nm, g, "예" if mono else "★아니오", steps, min(steps)))

print()
print("1-4. ★ 인계본 램프 안에서의 자기 충돌 (같은 파일 안)")
print("  ΔE(인계본 일반 #8A8F98, 인계본 희귀 #6E9BE8) = %.2f  (LCh 호 %.2f°)" %
      (dE(hex2rgb("#8A8F98"), hex2rgb("#6E9BE8")),
       arc(lch(hex2rgb("#8A8F98"))[2], lch(hex2rgb("#6E9BE8"))[2])))
print("  ΔE(인계본 전설 #E0B24A, 브라스 강조 #C8A15A)  = %.2f  ← 전설이 강조색과 같은 가족" %
      dE(hex2rgb("#E0B24A"), BRASS))
print("  ΔE(인계본 일반 #8A8F98, ItemCatalog.InkDimTone #8B939F) = %.2f  ← 잉크 표식과 사실상 동일" %
      dE(hex2rgb("#8A8F98"), hex2rgb("#8B939F")))
print("  ΔE(인계본 일반 #8A8F98, TextTertiary 잠김 #8b939f)      = %.2f" %
      dE(hex2rgb("#8A8F98"), LOCKED))


print()
print("=" * 78)
print("2. 절충안 D 탐색 — 「색조는 인계본 방향, 값은 충돌 회피」가 가능한가 (전수)")
print("=" * 78)

AVOID = [("착용중 #5DA1F5", WORN_BORDER), ("착용중 합성 " + rgb2hex(WORN_COMP), WORN_COMP),
         ("잠김 #8b939f", LOCKED), ("강조 브라스 #C8A15A", BRASS)] + \
        [("팩 " + n, c) for n, c in pack12]

HUE_WINDOW = {"일반": None,            # 무채 (C* <= 12)
              "희귀": (240.0, 300.0),  # 파랑~청보라
              "영웅": (300.0, 340.0),  # 보라~자주
              "전설": (70.0, 100.0)}   # 금


def slot_ok(ko, c):
    """한 등급 자리의 단독 조건. (통과여부, 실패사유)"""
    Ls, C, h = lch(c)
    if L(c) <= CHROME:
        return False, "크롬대역밖"
    if CR(c, CARD) < 4.5:
        return False, "카드면 CR<4.5"
    w = HUE_WINDOW[ko]
    if w is None:
        if C > 12.0:
            return False, "무채 아님"
    else:
        if C < 20.0:
            return False, "채도부족"
        if not (w[0] <= h <= w[1]):
            return False, "색상창 밖"
    for nm, a in AVOID:
        if dE(c, a) < DISC:
            return False, "예약/팩 충돌 " + nm
    return True, ""


# --- 양성 대조: 알려진 나쁜 값이 실제로 걸리는가 -------------------------------
print("  [양성 대조] 알려진 값이 조건에 걸리는가")
ctl = [("인계본 희귀 #6E9BE8", "희귀", "#6E9BE8", False),
       ("인계본 일반 #8A8F98", "일반", "#8A8F98", False),
       ("인계본 영웅 #B07BE0", "영웅", "#B07BE0", False),
       # ★ 기대를 실측에 맞춰 정정했다 — 인계본 전설은 **단독 조건을 통과한다**(예약색 최근접
       #   ΔE 16.96 = 브라스, 크롬대역, CR 8.37, LCh h 84.2°). 인계본 램프에서 깨지는 것은
       #   일반·희귀·영웅 셋이고 전설은 아니다. 이 사실 자체를 대조로 박제한다.
       ("인계본 전설 #E0B24A", "전설", "#E0B24A", True),
       ("우리 전설 #FFD375", "전설", "#FFD375", True)]
ctl_ok = True
for nm, ko, hx, want in ctl:
    got, why = slot_ok(ko, hex2rgb(hx))
    mark = "OK " if got == want else "★어긋남"
    if got != want:
        ctl_ok = False
    print("    %s %-22s 기대=%-5s 실측=%-5s %s" % (mark, nm, want, got, why))
if not ctl_ok:
    print("  !! 대조 실패 — 탐색기를 믿을 수 없다. 중단")
    sys.exit(1)

# --- 전수 탐색 ---------------------------------------------------------------
STEP = 3
pool = {k: [] for k in HUE_WINDOW}
for r in range(0, 256, STEP):
    for g in range(0, 256, STEP):
        for b in range(0, 256, STEP):
            c = (r, g, b)
            if L(c) <= CHROME:
                continue
            for ko in HUE_WINDOW:
                ok, _ = slot_ok(ko, c)
                if ok:
                    pool[ko].append(c)
print()
print("  자리별 해 개수 (sRGB %d칸 격자, %d색 전수)" % (256 // STEP, (256 // STEP) ** 3))
for ko in ("일반", "희귀", "영웅", "전설"):
    print("    %-4s %7d색" % (ko, len(pool[ko])))

_MARGIN = {}
for ko in pool:
    for c in pool[ko]:
        if c not in _MARGIN:
            _MARGIN[c] = min(dE(c, a) for _, a in AVOID)


def margin(c):
    return _MARGIN[c]


# ★ 각 자리에서 **회색조 값마다 여유 최대인 대표색 1개**만 남긴다(자리당 <=256색).
#   회색조 단차가 이 탐색의 축이므로 같은 회색조 안에서는 여유가 큰 쪽만 남기면 충분하다.
#   그래도 「대표 1개 축소가 해를 지웠을 가능성」은 남으므로, 확정 해는 전수 풀에서 다시 검증한다.
_REP = {}
for ko in pool:
    d = {}
    for c in pool[ko]:
        g = gray_level(c)
        if g not in d or _MARGIN[c] > _MARGIN[d[g]]:
            d[g] = c
    _REP[ko] = sorted(d.values(), key=gray_level)
_PRE = _REP


def chain(thresh):
    """여유 >= thresh 인 색만으로 회색조 단조(단차>=8) + 램프 내부 ΔE>=7.8 사슬을 만든다.
    ★ 「상위 N개만」 같은 잘라내기를 쓰지 않는다 — 그 잘라내기가 거짓 「불가」를 낸다(1차 시도에서
      실제로 그랬다). 회색조로 정렬해 앞에서부터 그리디로 잇고, 막히면 뒤로 물린다."""
    cand = {}
    for ko in _PRE:
        f = [c for c in _PRE[ko] if _MARGIN[c] >= thresh]
        cand[ko] = f
        if not f:
            return None
    order = ["일반", "희귀", "영웅", "전설"]

    def rec(k, chosen):
        if k == 4:
            return list(chosen)
        lo = gray_level(chosen[-1]) + 8 if chosen else -1
        for c in cand[order[k]]:
            if gray_level(c) < lo:
                continue
            if any(dE(c, x) < DISC for x in chosen):
                continue
            r = rec(k + 1, chosen + [c])
            if r:
                return r
        return None
    return rec(0, [])


lo, hi, bestramp = DISC, 60.0, None
if chain(DISC) is None:
    print()
    print("  ⇒ ★ 절충안 D **성립 불가** — 여유를 변별 하한(7.8)까지 낮춰도 사슬이 없다")
else:
    for _ in range(24):
        mid = (lo + hi) / 2.0
        r = chain(mid)
        if r:
            lo, bestramp = mid, r
        else:
            hi = mid
    m = min(margin(c) for c in bestramp)
    print()
    print("  ★ 절충안 D 최적해 존재. 예약/팩 최소여유 ΔE = %.2f" % m)
    print("  %-6s %-9s %8s %7s %7s %8s | %-22s %7s | %8s" %
          ("등급", "hex", "L", "L*", "C*", "h(LCh)", "최근접 예약/팩", "회색조", "호→예약"))
    for (ko, _), c in zip(RAR_A, bestramp):
        Ls, C, h = lch(c)
        near = min((dE(c, a), n) for n, a in AVOID)
        ref = WORN_BORDER if ko in ("희귀", "영웅") else LOCKED
        print("  %-6s %-9s %8.4f %7.1f %7.1f %8.1f | %6.2f %-15s %5d | %7.1f°" %
              (ko, rgb2hex(c), L(c), Ls, C, h, near[0], near[1][:15],
               gray_level(c), arc(h, lch(ref)[2])))
    print()
    print("  ★ 그러나 「가족」은 여전히 겹친다 — LCh 색상 호로 다시 잰다:")
    print("     절충 희귀 %s ↔ 착용중 #5DA1F5 : 호 %5.1f°   ΔE %5.2f" %
          (rgb2hex(bestramp[1]), arc(lch(bestramp[1])[2], lch(WORN_BORDER)[2]),
           dE(bestramp[1], WORN_BORDER)))
    print("     절충 일반 %s : C* %.1f (무채) ↔ 잠김 #8b939f C* %.1f  ΔE %5.2f" %
          (rgb2hex(bestramp[0]), lch(bestramp[0])[1], lch(LOCKED)[1], dE(bestramp[0], LOCKED)))
    print("     [대조] 우리 램프 희귀 #BCAC8B ↔ 착용중 : 호 %5.1f°   ΔE %5.2f" %
          (arc(lch(hex2rgb("#BCAC8B"))[2], lch(WORN_BORDER)[2]), dE(hex2rgb("#BCAC8B"), WORN_BORDER)))
    print("     [대조] 우리 램프 일반 #9C978C : C* %.1f  ΔE→잠김 %5.2f" %
          (lch(hex2rgb("#9C978C"))[1], dE(hex2rgb("#9C978C"), LOCKED)))


# --- 2-B. 서열 폭의 천장 — 절충안 D가 회색조 단차를 얼마까지 벌릴 수 있는가 -------
def chain_step(minstep, thresh=DISC):
    cand = {ko: [c for c in _PRE[ko] if _MARGIN[c] >= thresh] for ko in _PRE}
    order = ["일반", "희귀", "영웅", "전설"]
    if any(not cand[k] for k in order):
        return None

    def rec(k, chosen):
        if k == 4:
            return list(chosen)
        lo = gray_level(chosen[-1]) + minstep if chosen else -1
        for c in cand[order[k]]:
            if gray_level(c) < lo:
                continue
            if any(dE(c, x) < DISC for x in chosen):
                continue
            r = rec(k + 1, chosen + [c])
            if r:
                return r
        return None
    return rec(0, [])


print()
print("2-B. 서열 폭의 천장 — 회색조 단차를 얼마까지 벌릴 수 있는가 (여유는 7.8로 고정)")
best_s, best_r = None, None
for st in range(4, 70):
    r = chain_step(st)
    if r is None:
        break
    best_s, best_r = st, r
ours_steps = [gray_level(hex2rgb(OURS[i + 1][1])) - gray_level(hex2rgb(OURS[i][1])) for i in range(3)]
print("  절충안 D 최대 최소단차 = %d" % (best_s if best_s else -1))
if best_r:
    print("    %s  회색조 %s" % ([rgb2hex(c) for c in best_r], [gray_level(c) for c in best_r]))
    print("    L* %s" % ["%.1f" % lch(c)[0] for c in best_r])
print("  우리 브라스 램프 최소단차 = %d   회색조 %s" %
      (min(ours_steps), [gray_level(hex2rgb(h)) for _, h in OURS]))
print("  인계본 RAR_A 최소단차 = %d (음수 = 서열 역전)" %
      min(gray_level(hex2rgb(RAR_A[i + 1][1])) - gray_level(hex2rgb(RAR_A[i][1])) for i in range(3)))


# --- 2-C. 색각이상 · 팩 가족 · 색상 자리 점유 ---------------------------------
import cvd as CVD
print()
print("2-C. 추가 축 — 색각이상 · 팩과의 가족 거리 · 색상환 점유")
if not CVD.calibrate(verbose=False):
    print("  !! CVD 교정 실패 — 이 소절 폐기")
else:
    print("  (cvd.py 자체 교정 통과)")
    cands = {"인계본 RAR_A": [hex2rgb(h) for _, h in RAR_A],
             "절충안 D(여유최대)": list(bestramp) if bestramp else [],
             "절충안 D(단차최대)": list(best_r) if best_r else [],
             "우리 브라스 램프": [hex2rgb(h) for _, h in OURS]}
    print()
    print("  (a) 램프 4색 **서로**의 최근접 ΔE — 등급이 몇 단으로 남는가")
    print("  %-20s %9s %9s %9s %9s" % ("램프", "정상", "1형", "2형", "3형"))
    for nm, r in cands.items():
        if not r:
            continue
        row = []
        for kind in (None, "protan", "deutan", "tritan"):
            v = [c if kind is None else CVD.sim(c, kind) for c in r]
            row.append(min(dE(v[i], v[j]) for i in range(4) for j in range(i + 1, 4)))
        print("  %-20s %9.2f %9.2f %9.2f %9.2f" % (nm, *row))
    print()
    print("  (b) 팩 12색과의 최근접 ΔE / 6팩 주색과의 최소 LCh 색상 호")
    packmain = [(n, hex2rgb(m)) for n, (H, m, s) in PACKS.items()]
    print("  %-20s %12s %14s %s" % ("램프", "팩12 최근접ΔE", "6팩 최소 호", "최근접 팩"))
    for nm, r in cands.items():
        if not r:
            continue
        d = min((dE(c, pc), pn) for c in r for pn, pc in pack12)
        a = min((arc(lch(c)[2], lch(pc)[2]), pn) for c in r for pn, pc in packmain)
        print("  %-20s %12.2f %13.1f° %s" % (nm, d[0], a[0], a[1]))
    print()
    print("  (c) 색상환 점유 — 램프가 먹는 색상 자리 수 (7번째 팩의 여지)")
    for nm, r in cands.items():
        if not r:
            continue
        hs = sorted(lch(c)[2] for c in r if lch(c)[1] >= 12.0)
        span = 0.0 if len(hs) < 2 else max(hs) - min(hs)
        print("  %-20s 유채 %d색 · LCh 색상 %s · 폭 %.1f°" %
              (nm, len(hs), ["%.0f°" % x for x in hs], span))


# --- 2-D. 공정하게 다시 — 절충안 D 에 색각이상 조건을 넣고 재탐색 -----------------
print()
print("2-D. ★ 공정 재탐색 — D 에 「색각이상 3형 전부에서 램프 내부 ΔE >= 7.8」을 조건으로 넣는다")
print("     (앞의 D 해는 그 조건이 없었다. 우리 램프는 그 조건을 이미 만족한다 — 비교가 기울어 있었다)")
_SIMC = {}


def simc(c, kind):
    k = (c, kind)
    if k not in _SIMC:
        _SIMC[k] = CVD.sim(c, kind)
    return _SIMC[k]


def cvd_ok(ramp):
    for kind in CVD.TYPES:
        v = [simc(c, kind) for c in ramp]
        if min(dE(v[i], v[j]) for i in range(4) for j in range(i + 1, 4)) < DISC:
            return False
    return True


def chain_step_cvd(minstep):
    cand = {ko: [c for c in _PRE[ko] if _MARGIN[c] >= DISC] for ko in _PRE}
    order = ["일반", "희귀", "영웅", "전설"]

    def rec(k, chosen):
        if k == 4:
            return list(chosen) if cvd_ok(chosen) else None
        lo = gray_level(chosen[-1]) + minstep if chosen else -1
        for c in cand[order[k]]:
            if gray_level(c) < lo:
                continue
            if any(dE(c, x) < DISC for x in chosen):
                continue
            if chosen and any(min(dE(simc(c, kd), simc(x, kd)) for x in chosen) < DISC
                              for kd in CVD.TYPES):
                continue
            r = rec(k + 1, chosen + [c])
            if r:
                return r
        return None
    return rec(0, [])


bs, br = None, None
for st in range(4, 70):
    r = chain_step_cvd(st)
    if r is None:
        break
    bs, br = st, r
if br is None:
    print("  ⇒ ★ 색각이상 조건을 넣으면 절충안 D 는 **해가 없다**")
else:
    print("  절충안 D(색각 조건 포함) 최대 최소단차 = %d" % bs)
    print("    %s  회색조 %s" % ([rgb2hex(c) for c in br], [gray_level(c) for c in br]))
    row = []
    for kind in (None, "protan", "deutan", "tritan"):
        v = [c if kind is None else simc(c, kind) for c in br]
        row.append(min(dE(v[i], v[j]) for i in range(4) for j in range(i + 1, 4)))
    print("    램프 내부 최근접 ΔE  정상 %.2f / 1형 %.2f / 2형 %.2f / 3형 %.2f" % tuple(row))
    print("    LCh 색상 %s" % ["%.0f°" % lch(c)[2] for c in br])
    print("    카드면 CR %s" % ["%.2f" % CR(c, CARD) for c in br])
    print("    예약/팩 최근접 ΔE %.2f" % min(_MARGIN[c] for c in br))
    print("    희귀 ↔ 착용중 #5DA1F5 LCh 호 %.1f°  /  일반 C* %.1f (잠김 회색 C* %.1f)" %
          (arc(lch(br[1])[2], lch(WORN_BORDER)[2]), lch(br[0])[1], lch(LOCKED)[1]))
    row2 = []
    for kind in (None, "protan", "deutan", "tritan"):
        v = [c if kind is None else simc(c, kind) for c in [hex2rgb(h) for _, h in OURS]]
        row2.append(min(dE(v[i], v[j]) for i in range(4) for j in range(i + 1, 4)))
    print("  [대조] 우리 브라스 램프 최소단차 21 · 램프 내부 최근접 ΔE "
          "정상 %.2f / 1형 %.2f / 2형 %.2f / 3형 %.2f" % tuple(row2))


# --- 2-E. 「7번째 팩의 자리」— 램프가 색상환을 먹으면 다음 팩이 몇 각도 죽는가 -------
print()
print("2-E. 7번째 팩의 자리 — 램프를 「이미 자리 잡은 색」에 넣고 유도 규칙을 다시 돌린다")
print("     (§13-3 처방 C 와 같은 탐색. 5° 간격 72각도에서 주색·보조색 둘 다 해가 있는가)")
import packclash as PC
import band as BND
LO_, HI_ = BND.limits()[0], BND.limits()[1]
BD_ = BND.BACKDROPS
cur = PC.load_current()
cat = [hex2rgb(h) for h in cur if h not in PC.INK_MARKERS]


def chroma(c):
    a = lab(c)
    return math.hypot(a[1], a[2])


def pick(hue, want_max, avoid, gap):
    best = None
    for si in range(42, 101):
        for vi in range(55, 81):
            from colorlab import hsv_to_rgb
            c = hsv_to_rgb(hue / 360.0, si / 100.0, vi / 100.0)
            if not (LO_ <= L(c) <= HI_) or worn(c) != c:
                continue
            if any(dE(c, a) < gap for a in avoid):
                continue
            k = chroma(c) if want_max else -chroma(c)
            key = (round(k, 1), round(-abs(min(CR(c, b) for _, b in BD_) - 3.50), 3))
            if best is None or key > best[0]:
                best = (key, c)
    return best[1] if best else None


def free_angles(extra):
    """추가 예약색 extra 를 넣었을 때, 주·보조 둘 다 해가 있는 각도 수."""
    placed = list(cat) + [BRASS] + [hex2rgb(m) for _, (H, m, s) in PACKS.items()] + \
             [hex2rgb(s) for _, (H, m, s) in PACKS.items()] + list(extra)
    n, dead = 0, []
    for a in range(0, 360, 5):
        m = pick(a, True, placed, DISC)
        s = pick(a, False, placed, DISC)
        if m is not None and s is not None:
            n += 1
        else:
            dead.append(a)
    return n, dead


for nm, ramp in (("램프 없음(기준선)", []),
                 ("우리 브라스 램프", [hex2rgb(h) for _, h in OURS]),
                 ("인계본 RAR_A", [hex2rgb(h) for _, h in RAR_A]),
                 ("절충안 D(색각조건)", list(br) if br else [])):
    if nm != "램프 없음(기준선)" and not ramp:
        continue
    n, dead = free_angles(ramp)
    print("  %-18s 72각도 중 살아있는 각도 %2d개 · 막힌 각도 %s" %
          (nm, n, dead if len(dead) <= 12 else "%d개" % len(dead)))

# --- 2-F. §29 확정값 #FBE4C0 이 램프 교체를 견디는가 ---------------------------
print()
print("2-F. §29 확정 TextOnAccent #FBE4C0 이 램프 교체를 견디는가 (하한 7.8)")
T = hex2rgb("#FBE4C0")
for nm, ramp in (("우리 브라스 램프", [hex2rgb(h) for _, h in OURS]),
                 ("인계본 RAR_A", [hex2rgb(h) for _, h in RAR_A]),
                 ("절충안 D(색각조건)", list(br) if br else [])):
    if not ramp:
        continue
    d = min(dE(T, c) for c in ramp)
    print("  %-18s ΔE(#FBE4C0, 램프 최근접) = %6.2f  %s" %
          (nm, d, "통과" if d >= DISC else "★미달 — §29 판정이 다시 열린다"))


# --- 2-G. 강건성 — 색을 뺏겼을 때 램프가 얼마나 무너지는가 ----------------------
print()
print("2-G. 강건성 — 색 정보를 뺏겼을 때 램프 내부 변별이 얼마나 남는가 (정상 대비 %)")
print("  %-20s %8s %8s %8s %8s %10s" % ("램프", "정상", "1형", "2형", "3형", "최악보존율"))
for nm, ramp in (("인계본 RAR_A", [hex2rgb(h) for _, h in RAR_A]),
                 ("절충안 D(색각조건)", list(br) if br else []),
                 ("우리 브라스 램프", [hex2rgb(h) for _, h in OURS])):
    if not ramp:
        continue
    vals = []
    for kind in (None,) + CVD.TYPES:
        v = [c if kind is None else simc(c, kind) for c in ramp]
        vals.append(min(dE(v[i], v[j]) for i in range(4) for j in range(i + 1, 4)))
    keep = min(vals[1:]) / vals[0] * 100.0
    print("  %-20s %8.2f %8.2f %8.2f %8.2f %9.1f%%" % (nm, *vals, keep))
print("  ★ 단색 램프는 서열을 **휘도**가 지고, 휘도는 세 이색각 전부에서 보존된다.")
print("    색상 램프는 서열을 **색상**이 지고, 색상은 이색각에서 사영으로 붕괴한다.")

print()
print("=" * 78)
print("3. 뭉치 ② — 팩 스와치 6색")
print("=" * 78)
print("  인계본 위치: 외형 탭 「파쿠르 파티클 색상 / 보유 팩 테마 색을 따릅니다」 (html:373-381)")
print("  칩 배경 #0E0C0B · 10x10px 원 · 선택 테두리 #EDE7DB / 비선택 #231F1B")
print()
print("3-1. 대역 · 대비 · 자기 팩과의 거리")
print("  %-7s %-9s %8s %7s %7s %8s | %6s | %8s | %s" %
      ("팩", "hex", "L", "L*", "C*", "h(LCh)", "칩CR", "자기팩ΔE", "대역"))
for k, hx in SWATCH:
    c = hex2rgb(hx)
    Ls, C, h = lch(c)
    kmap = {"office": "오피스 워커", "cyber": "사이버 아포칼립스", "neon": "네온 낙서",
            "sport": "스포츠", "ink": "컬러 잉크", "mil": "밀리터리"}
    ours = hex2rgb(PACKS[kmap[k]][1])
    print("  %-7s %-9s %8.4f %7.1f %7.1f %8.1f | %6.2f | %8.2f | %s" %
          (k, hx, L(c), Ls, C, h, CR(c, SWBG), dE(c, ours), band_of(c)))
print()
print("  [대조] 우리 처방 C 팩 주색을 같은 칩에 올리면")
print("  %-7s %-9s %8s %7s %7s %8s | %6s | %s" % ("팩", "hex", "L", "L*", "C*", "h(LCh)", "칩CR", "대역"))
for nm, (H, m, s) in PACKS.items():
    c = hex2rgb(m)
    Ls, C, h = lch(c)
    print("  %-7s %-9s %8.4f %7.1f %7.1f %8.1f | %6.2f | %s" % (nm[:6], m, L(c), Ls, C, h, CR(c, SWBG), band_of(c)))

print()
print("3-2. ★ 스와치 6색이 그 화면에서 **이미 다른 뜻을 갖는가** (인계본 원문 실측)")
ROLES = {
    "#C8A15A": "강조색 · 활성 탭 배경 · 스탯 글자 · 구매 버튼 · 게이지 (equipment-screen 53회)",
    "#6E9BE8": "★ 등급 「희귀」 (RAR_A[1] · RARC[1])",
    "#B07BE0": "★ 등급 「영웅」 (RAR_A[2] · RARC[2])",
    "#8FBF6A": "스탯 상승 · 세트 완성 · 프리셋 「세트 완성」 태그 · 팩 「보유」 배지 (4역할)",
    "#D2402F": "★ ItemIcon.dc.html MAT.shortcape / MAT.longcape (망토 재질색)",
    "#8A8578": "비활성 탭 글자 · 보조 본문 · 가격 칩 · 프리셋 「이종 조합」 태그 (12회)",
}
for k, hx in SWATCH:
    print("  %-7s %-9s %s" % (k, hx, ROLES[hx]))
print()
print("3-3. 「같은 색을 등급에도 팩에도」 — 한 화면(상점 탭)에서 무엇으로 읽히는가")
print("  ΔE(cyber 스와치, 인계본 희귀)   = %.2f   ← 같은 값(0 = 문자 그대로 같은 색)" %
      dE(hex2rgb("#6E9BE8"), hex2rgb("#6E9BE8")))
print("  ΔE(neon  스와치, 인계본 영웅)   = %.2f" % dE(hex2rgb("#B07BE0"), hex2rgb("#B07BE0")))
print("  ΔE(office 스와치, 강조 브라스)  = %.2f" % dE(hex2rgb("#C8A15A"), BRASS))
print("  ΔE(ink   스와치, MAT.shortcape) = %.2f" % dE(hex2rgb("#D2402F"), hex2rgb("#D2402F")))
print("  ΔE(mil   스와치, 비활성탭 글자)  = %.2f" % dE(hex2rgb("#8A8578"), hex2rgb("#8A8578")))
print("  ⇒ 6색 중 %d색이 같은 인계본 안에서 ΔE 0.00 의 이중 배정." %
      sum(1 for hx in ("#C8A15A", "#6E9BE8", "#B07BE0", "#8FBF6A", "#8A8578")))
print()
print("3-4. ink #D2402F — 우연인가 재질색이 새어나온 것인가")
print("  HSV 색상각: ink %.2f°  /  우리 스포츠 팩 주색 #CC3F29 %.2f°  → 호 %.2f°" %
      (hue_deg(hex2rgb("#D2402F")), hue_deg(hex2rgb("#CC3F29")),
       arc(hue_deg(hex2rgb("#D2402F")), hue_deg(hex2rgb("#CC3F29")))))
print("  ΔE(ink #D2402F, 우리 스포츠 주색 #CC3F29) = %.2f  ← %s" %
      (dE(hex2rgb("#D2402F"), hex2rgb("#CC3F29")),
       "변별 하한 미달" if dE(hex2rgb("#D2402F"), hex2rgb("#CC3F29")) < DISC else "변별 통과"))
print("  ΔE(ink #D2402F, 우리 컬러잉크 주색 #9768CC) = %.2f  (LCh 호 %.1f°)" %
      (dE(hex2rgb("#D2402F"), hex2rgb("#9768CC")),
       arc(lch(hex2rgb("#D2402F"))[2], lch(hex2rgb("#9768CC"))[2])))
print("  WornColor 항등? ink %s / 스포츠 주색 %s" %
      (is_worn_fixed(hex2rgb("#D2402F")), is_worn_fixed(hex2rgb("#CC3F29"))))
print("  자립 대역? ink L=%.4f → %s" % (L(hex2rgb("#D2402F")), band_of(hex2rgb("#D2402F"))))


print()
print("=" * 78)
print("4. 팩 저작 규칙 — H-17 「≤ 60°」 vs §28-2-2 「≤ 26°」 를 닫는다")
print("=" * 78)
# 인계본 프리셋 「오피스 워커 풀세트」 4부위 = 출하 애셋에서 직접 읽는다
PRESETS = {
    "오피스 워커 풀세트": ["equip_head_fedora", "equip_eyes_round",
                     "equip_neck_striped", "equip_shoulders_backpack"],
    "야간 잠행": ["equip_head_fur", "equip_eyes_goggles",
               "equip_neck_scarf", "equip_shoulders_long_cape"],
    "연회장": ["equip_head_crown", "equip_eyes_monocle",
             "equip_neck_bowtie", "equip_shoulders_short_cape"],
}
items = shipped.item_colors()
print("  ★ 색은 출하 `.asset`에서 파싱한다(문서를 베끼지 않는다).")
for nm, ids in PRESETS.items():
    cols = []
    miss = []
    for i in ids:
        if i in items and 0 in items[i]["tones"]:
            cols.append(sorted(items[i]["tones"][0])[0])
        else:
            miss.append(i)
    if len(cols) < 2:
        print("  %-14s 애셋 없음 %s" % (nm, miss))
        continue
    hs_hsv = [hue_deg(c) for c in cols]
    hs_lch = [lch(c)[2] for c in cols]

    def spread(hs):
        hs = sorted(hs)
        gaps = [hs[(i + 1) % len(hs)] - hs[i] + (360 if i == len(hs) - 1 else 0) for i in range(len(hs))]
        return 360.0 - max(gaps)
    pmax = max(dE(cols[i], cols[j]) for i in range(len(cols)) for j in range(i + 1, len(cols)))
    print("  %-14s %s" % (nm, [rgb2hex(c) for c in cols]) + (" (누락 %s)" % miss if miss else ""))
    print("      HSV 스프레드 %6.1f°  ·  LCh 스프레드 %6.1f°  ·  쌍 최대 ΔE %5.1f  "
          "(식별 하한 %.1f)  →  26° 게이트 %s / 60° 게이트 %s / ΔE 게이트 %s" %
          (spread(hs_hsv), spread(hs_lch), pmax, IDENT,
           "통과" if spread(hs_lch) <= 26 else "★불통",
           "통과" if spread(hs_lch) <= 60 else "★불통",
           "통과" if pmax < IDENT else "★불통"))
print()
print("  [대조] 우리 유도 팩 6종 (처방 C, 주·보조·그늘 3색)")
for nm, (H, m, s) in PACKS.items():
    cols = [hex2rgb(m), hex2rgb(s), tuple(int(round(v * 0.62)) for v in hex2rgb(m))]
    hs = [lch(c)[2] for c in cols]
    sp = max(hs) - min(hs)
    pmax = max(dE(cols[i], cols[j]) for i in range(3) for j in range(i + 1, 3))
    print("  %-12s LCh 스프레드 %5.2f°  쌍 최대 ΔE %5.1f  → 26° %s / ΔE %s" %
          (nm, sp, pmax, "통과" if sp <= 26 else "★불통", "통과" if pmax < IDENT else "★불통"))
print()
print("  [무규칙 대조] 출하 42종에서 4부위 무작위 한 벌 2,000회")
random_ids = {}
for iid, rec in items.items():
    if iid.startswith("equip_") and 0 in rec["tones"]:
        slot = iid.split("_")[1]
        random_ids.setdefault(slot, []).append(sorted(rec["tones"][0])[0])
import random as _rnd
_rnd.seed(20260903)
slots = [s for s in ("head", "eyes", "neck", "shoulders") if s in random_ids]
sps, over26, over60, overE = [], 0, 0, 0
for _ in range(2000):
    pick4 = [_rnd.choice(random_ids[s]) for s in slots]
    hs = sorted(lch(c)[2] for c in pick4)
    gaps = [hs[(i + 1) % len(hs)] - hs[i] + (360 if i == len(hs) - 1 else 0) for i in range(len(hs))]
    sp = 360.0 - max(gaps)
    sps.append(sp)
    over26 += sp > 26
    over60 += sp > 60
    overE += max(dE(pick4[i], pick4[j]) for i in range(4) for j in range(i + 1, 4)) >= IDENT
sps.sort()
print("      LCh 스프레드 중앙 %.1f° · 최소 %.1f° · 최대 %.1f°" % (sps[1000], sps[0], sps[-1]))
print("      26° 초과 %.1f%% · 60° 초과 %.1f%% · 쌍 ΔE>=48.6 %.1f%%" %
      (over26 / 20.0, over60 / 20.0, overE / 20.0))


print()
print("4-B. ★ 자의 단위를 못박는다 — H-17 의 60°도 §28-2-2 의 26°도 **HSV**였다. LCh 로 다시 유도")
HYPO_A = [hex2rgb(x) for x in ("#AB7942", "#9B7922", "#BA5928", "#BA7636")]


def sp_of(cols, ruler):
    hs = sorted((hue_deg(c) if ruler == "hsv" else lch(c)[2]) for c in cols)
    gaps = [hs[(i + 1) % len(hs)] - hs[i] + (360 if i == len(hs) - 1 else 0) for i in range(len(hs))]
    return 360.0 - max(gaps)


print("  UX_HANDOFF_REVIEW §3-2 「가정 팩 A」 %s" % [rgb2hex(c) for c in HYPO_A])
print("    HSV 스프레드 %.1f° (문서값 23.0) · LCh 스프레드 %.1f° · 쌍 최대 ΔE %.1f" %
      (sp_of(HYPO_A, "hsv"), sp_of(HYPO_A, "lch"),
       max(dE(HYPO_A[i], HYPO_A[j]) for i in range(4) for j in range(i + 1, 4))))

# 아트 대역 ∩ WornColor 상자 안의 색을 전수로 깔고, 각 팩 주색에서 ΔE 48.6 에 닿는 LCh 호를 찾는다
from colorlab import hsv_to_rgb
grid = []
for hi in range(0, 360, 1):
    for si in range(42, 101, 2):
        for vi in range(55, 81, 1):
            c = hsv_to_rgb(hi / 360.0, si / 100.0, vi / 100.0)
            if BAND[0] <= L(c) <= BAND[1] and worn(c) == c:
                grid.append(c)
print("  아트 대역 ∩ 상자 격자 %d색" % len(grid))
print("  %-14s %10s %10s | %10s %10s" %
      ("기준 팩 주색", "HSV 호", "LCh 호", "ΔE 7.8 도달", "ΔE 48.6 도달"))
lch_caps, hsv_caps = [], []
for nm, (H, m, s) in PACKS.items():
    base = hex2rgb(m)
    a78h = a78l = a48h = a48l = None
    for c in grid:
        d = dE(base, c)
        ah = arc(hue_deg(base), hue_deg(c))
        al = arc(lch(base)[2], lch(c)[2])
        if d >= DISC:
            a78h = ah if a78h is None else min(a78h, ah)
            a78l = al if a78l is None else min(a78l, al)
        if d >= IDENT:
            a48h = ah if a48h is None else min(a48h, ah)
            a48l = al if a48l is None else min(a48l, al)
    hsv_caps.append(a48h)
    lch_caps.append(a48l)
    print("  %-14s %9.1f° %9.1f° | %4.1f°/%4.1f° %5.1f°/%5.1f°" %
          (nm, a48h, a48l, a78h, a78l, a48h, a48l))
print()
print("  ⇒ ΔE 48.6 에 닿는 **최소** 호:  HSV %.1f°  /  **LCh %.1f°**" % (min(hsv_caps), min(lch_caps)))
print("     (중앙 HSV %.1f° / LCh %.1f°,  최대 HSV %.1f° / LCh %.1f°)" %
      (sorted(hsv_caps)[3], sorted(lch_caps)[3], max(hsv_caps), max(lch_caps)))


print()
print("4-C. ★ 난간 값을 「추측」이 아니라 **일치율**로 고른다")
print("  기계 게이트(구속력) = 한 벌 내부 모든 쌍 ΔE < 48.6.  각도 게이트는 사람 눈 검토용 난간이다.")
print("  난간 값은 **기계 게이트와 가장 적게 어긋나는 각도**로 고른다 (무작위 4부위 5,000벌).")
_rnd.seed(20260903)
sets = []
for _ in range(5000):
    p4 = [_rnd.choice(random_ids[s]) for s in slots]
    hs = sorted(lch(c)[2] for c in p4)
    gaps = [hs[(i + 1) % len(hs)] - hs[i] + (360 if i == len(hs) - 1 else 0) for i in range(len(hs))]
    sp = 360.0 - max(gaps)
    ok_de = max(dE(p4[i], p4[j]) for i in range(4) for j in range(i + 1, 4)) < IDENT
    sets.append((sp, ok_de))
print("  %6s %10s %10s %10s %10s" % ("난간°", "거짓기각", "거짓통과", "총 불일치", "일치율"))
best_rail = None
for rail in (20, 26, 30, 35, 40, 45, 46, 50, 55, 60, 70, 80):
    fr = sum(1 for sp, ok in sets if ok and sp > rail)      # ΔE 통과인데 각도가 기각
    fa = sum(1 for sp, ok in sets if (not ok) and sp <= rail)  # ΔE 불통인데 각도가 통과
    tot = fr + fa
    print("  %6d %9d %10d %10d %9.2f%%" % (rail, fr, fa, tot, (1 - tot / len(sets)) * 100))
    if best_rail is None or tot < best_rail[1]:
        best_rail = (rail, tot)
print("  ⇒ 최소 불일치 난간 = **LCh %d°** (불일치 %d / 5000)" % best_rail)
print()
print("  알려진 사례로 교차 확인 (LCh 스프레드 / 쌍 최대 ΔE / 기계 게이트)")
for nm, cols in (("가정 팩 A(문서상 「가족」)", HYPO_A),
                 ("인계본 오피스 프리셋(현 카탈로그)",
                  [hex2rgb(x) for x in ("#5577AE", "#587398", "#428C24", "#AB7942")]),
                 ("인계본 야간 잠행",
                  [hex2rgb(x) for x in ("#BA7636", "#587398", "#CC5512", "#CC3C3C")]),
                 ("우리 유도 팩 최악(사이버 3색)",
                  [hex2rgb("#009682"), hex2rgb("#518C84"),
                   tuple(int(round(v * 0.62)) for v in hex2rgb("#009682"))])):
    sp = sp_of(cols, "lch")
    pm = max(dE(cols[i], cols[j]) for i in range(len(cols)) for j in range(i + 1, len(cols)))
    print("  %-28s %6.1f° / %5.1f / %s  → 난간 %d° %s" %
          (nm, sp, pm, "통과" if pm < IDENT else "불통", best_rail[0],
           "통과" if sp <= best_rail[0] else "기각"))


print()
print("=" * 78)
print("5. 대체안 — 우리 팩 스와치 6색 (새 hex 0개. 유도된 팩 주색을 그대로 쓴다)")
print("=" * 78)
SURF = [("PanelSurface #14171C", hex2rgb("#14171C")),
        ("CardSurface #1B1F26", CARD),
        ("SubtleSurface #191D24", hex2rgb("#191D24")),
        ("인계본 칩면 #0E0C0B", SWBG)]
print("  스와치 = 그 팩의 **주색**(= 파쿠르 파티클에 실제로 칠해지는 색, §6-2). 새로 고른 색 없음.")
print("  %-12s %-9s | %s" % ("팩", "주색", " / ".join(n.split()[0] for n, _ in SURF)))
worst = 9e9
for nm, (H, m, s) in PACKS.items():
    c = hex2rgb(m)
    crs = [CR(c, b) for _, b in SURF]
    worst = min(worst, min(crs))
    print("  %-12s %-9s | %s" % (nm, m, "  ".join("%5.2f" % x for x in crs)))
print("  ⇒ 네 면 최악 %.2f : 1  (비텍스트 하한 3.0 %s)" % (worst, "통과" if worst >= 3.0 else "★미달"))
print()
print("  선택/비선택 테두리 — 인계본 #EDE7DB / #231F1B 를 우리 토큰으로")
for nm, hx in (("인계본 선택 #EDE7DB", "#EDE7DB"), ("우리 TextPrimary", None),
               ("인계본 비선택 #231F1B", "#231F1B"), ("우리 CardBorder(합성)", None)):
    if hx:
        c = hex2rgb(hx)
        print("    %-22s L %.4f  CR(카드면) %5.2f  대역 %s" % (nm, L(c), CR(c, CARD), band_of(c)))
tp = tok["TextPrimary"][0]
cb = over((255, 255, 255), CARDBORDER_ALPHA, CARD)
print("    %-22s %s L %.4f  CR(카드면) %5.2f" % ("우리 TextPrimary", rgb2hex(tp), L(tp), CR(tp, CARD)))
print("    %-22s %s L %.4f  CR(카드면) %5.2f  (α%.2f 합성, 소스)"
      % ("우리 CardBorder", rgb2hex(cb), L(cb), CR(cb, CARD), CARDBORDER_ALPHA))
print("    ΔE(인계본 선택 #EDE7DB, 우리 TextPrimary) = %.2f" % dE(hex2rgb("#EDE7DB"), tp))
print()
print("  스와치 색이 예약색을 침범하는가 (하한 7.8)")
for nm2, ref in (("착용중 #5DA1F5", WORN_BORDER), ("잠김 #8b939f", LOCKED),
                 ("강조 브라스 #C8A15A", BRASS), ("TextOnAccent #FBE4C0", hex2rgb("#FBE4C0"))):
    d = min((dE(hex2rgb(m), ref), n) for n, (H, m, s) in PACKS.items())
    print("    %-22s 최근접 팩 주색 ΔE %6.2f (%s) %s" %
          (nm2, d[0], d[1], "통과" if d[0] >= DISC else "★미달"))
d = min((dE(hex2rgb(m), r), n, ko) for n, (H, m, s) in PACKS.items() for ko, r in RAMP.items())
print("    %-22s 최근접 팩 주색 ΔE %6.2f (%s ↔ %s) 통과" % ("등급 램프 4색", d[0], d[1], d[2]))
print()
print("  ★ 대역 판정: 스와치는 **아트 대역**에 둔다 (크롬으로 올리지 않는다)")
print("    이유는 실측이다 — 스와치는 「창 안의 크롬」이 아니라 「바탕화면에 칠해질 색의 미리보기」다.")
print("    같은 형태가 이미 출하돼 있다: 카드 아이콘이 아트 대역 재질색을 창 안에 그린다(§27 안 C).")
print("    크롬으로 올리면 스와치와 실제 파티클이 다른 색이 된다 — 고른 것과 나온 것이 어긋난다.")
print("    네 면 최악 %.2f : 1 로 비텍스트 하한을 넘으므로 창 안에서도 안 사라진다." % worst)
print()
print("  [계산상 통과 / 실기 미확인] 위 전부 선언값 계산이다. 빌드 캡처 없음.")


# ===========================================================================
# 6. 양성 대조 — 판정기가 「알려진 나쁜 값」을 실제로 잡는가
#    (통과만 보는 검사는 이 저장소에서 여러 번 거짓 초록을 냈다)
# ===========================================================================
print()
print("=" * 78)
print("6. 양성 대조 — 일부러 틀린 값을 넣고 잡히는지 본다")
print("=" * 78)
ctl2 = []


def c3(name, got, want):
    ok = (got == want)
    ctl2.append(ok)
    print("  %s %-56s 실측=%-6s 기대=%s" % ("OK " if ok else "★놓침", name, got, want))


c3("대역 판정: 브라스 L 0.3851 → 크롬", band_of(BRASS), "크롬")
c3("대역 판정: 오피스 주색 L 0.1678 → 아트", band_of(hex2rgb("#456ECC")), "아트")
c3("대역 판정: 해자값 L 0.27 → 해자", band_of(hex2rgb("#8A8F98")), "해자")
c3("대역 판정: 비선택테두리 L 0.014 → 대역밖", band_of(hex2rgb("#231F1B")), "대역밖(아래)")
g = [gray_level(hex2rgb(h)) for _, h in RAR_A]
c3("서열 검출: 인계본 램프는 단조가 아니다", all(g[i] < g[i + 1] for i in range(3)), False)
g = [gray_level(hex2rgb(h)) for _, h in OURS]
c3("서열 검출: 우리 램프는 단조다", all(g[i] < g[i + 1] for i in range(3)), True)
c3("변별 검출: 인계본 희귀 ↔ 착용중 ΔE < 7.8", dE(hex2rgb("#6E9BE8"), WORN_BORDER) < DISC, True)
c3("변별 검출: 인계본 일반 ↔ 잠김 ΔE < 7.8", dE(hex2rgb("#8A8F98"), LOCKED) < DISC, True)
c3("변별 검출: 우리 일반 ↔ 잠김 ΔE >= 7.8", dE(hex2rgb("#9C978C"), LOCKED) >= DISC, True)
c3("난간 검출: 인계본 오피스 프리셋 LCh 스프레드 > 35°",
   sp_of([hex2rgb(x) for x in ("#5577AE", "#587398", "#428C24", "#AB7942")], "lch") > 35.0, True)
c3("난간 검출: 우리 사이버 3색 LCh 스프레드 <= 35°",
   sp_of([hex2rgb("#009682"), hex2rgb("#518C84"),
          tuple(int(round(v * 0.62)) for v in hex2rgb("#009682"))], "lch") <= 35.0, True)
c3("ink 스와치 ↔ 우리 스포츠 주색 ΔE < 7.8", dE(hex2rgb("#D2402F"), hex2rgb("#CC3F29")) < DISC, True)
c3("ink 스와치는 WornColor 항등이 아니다", is_worn_fixed(hex2rgb("#D2402F")), False)
c3("자 대조: HSV 색상각 != LCh 색상각 (#8FC3FF)",
   abs(hue_deg(hex2rgb("#8FC3FF")) - lch(hex2rgb("#8FC3FF"))[2]) > 50.0, True)
print()
print("  ★ 대조 판정: %d / %d 잡힘" % (sum(ctl2), len(ctl2)))
if not all(ctl2):
    print("  !! 놓친 항목이 있다 — 위 숫자를 믿지 마라")
    sys.exit(2)


# ===========================================================================
# 7. 【추가 배정 ①】 등급 램프 vs 「착용 중 썸네일 wash」 — 배경이 넷이었다
# ===========================================================================
print()
print("=" * 78)
print("7. 등급 낱말이 앉는 배경은 셋이 아니라 **넷**이다 — 넷째를 재고 고칠 수 있는지 본다")
print("=" * 78)
ACCENT_SURF_A = prod_alpha("AccentSurface")
ACCENT_RGB = tok["AccentSurface"][0]
WASH = over(ACCENT_RGB, ACCENT_SURF_A, CARD)
print("  Flatten(AccentSurface α%.2f, CardSurface) = %s   (coder-ui 보고 #33312D)  %s" %
      (ACCENT_SURF_A, rgb2hex(WASH), "일치" if rgb2hex(WASH) == "#33312D" else "★불일치"))
LOCKED_CARD = tok["CardSurfaceMuted"][0]
LOCKED_THUMB = tok["ThumbSurfaceLocked"][0]
BGS = [("카드면 CardSurface", CARD), ("잠김 카드면 CardSurfaceMuted", LOCKED_CARD),
       ("잠긴 썸네일 ThumbSurfaceLocked", LOCKED_THUMB), ("★ 착용중 wash " + rgb2hex(WASH), WASH)]
print()
print("  %-6s %-9s | %s" % ("등급", "hex", " | ".join("%-10s" % n.split()[0] for n, _ in BGS)))
for ko, hx in OURS:
    c = hex2rgb(hx)
    print("  %-6s %-9s | %s" % (ko, hx, " | ".join("%10.2f" % CR(c, b) for _, b in BGS)))
print("  텍스트 하한 4.5 · 비텍스트 하한 3.0")
worstko = min((CR(hex2rgb(hx), WASH), ko, hx) for ko, hx in OURS)
print("  ⇒ wash 위 최악 = %s %s %.2f  (%s)" %
      (worstko[1], worstko[2], worstko[0], "★4.5 미달" if worstko[0] < 4.5 else "통과"))

print()
print("7-1. ★ 브리프의 ΔE 22.83 을 내 자로 재현하지 못했다 — 먼저 그것부터 적는다")
print("  ΔE(일반 #9C978C, 잠김 #8b939f) = %.2f  [내 자, CIE76]" % dE(hex2rgb("#9C978C"), LOCKED))
print("  브리프값 22.83 과 %.2f 차이. 자가 다르거나 대상이 다르다 —" %
      abs(dE(hex2rgb("#9C978C"), LOCKED) - 22.83))
print("  내 자는 교정 55/55 를 통과했고 §29-5 의 출하 문서값(19.79 등)을 그대로 재현한다.")
print("  ⇒ **하한은 22.83 이 아니라 변별 하한 7.8 로 잡는다**(자가 확인된 쪽). 현행 여유는 %.2f 다."
      % dE(hex2rgb("#9C978C"), LOCKED))

print()
print("7-2. 일반색을 밝혀 wash 위 4.5 를 넘길 수 있는가 — 전수 탐색")
RARE = hex2rgb("#BCAC8B")
EPIC = hex2rgb("#DEC081")
LEG = hex2rgb("#FFD375")
BRASS_H = lch(BRASS)[2]
AVOID2 = [("착용중 합성 " + rgb2hex(WORN_COMP), WORN_COMP), ("착용중 #5DA1F5", WORN_BORDER),
          ("잠김 #8b939f", LOCKED), ("TextOnAccent #FBE4C0", hex2rgb("#FBE4C0")),
          ("강조 브라스", BRASS)] + [("팩 " + n, c) for n, c in pack12]
# 등급 테두리(등급색 α RarityBorderAlpha) 로도 나오므로 그 합성면도 함께 본다
print("  ★ 표면이 하나 늘었다 — 카드 테두리 「기본」이 등급색 α%.2f 를 승계한다(RarityBorder)." % RARITY_BORDER_ALPHA)
print("    현행 4색의 테두리 실효색 / 카드면 대비:")
for ko, hx in OURS:
    b = over(hex2rgb(hx), RARITY_BORDER_ALPHA, CARD)
    print("      %-4s %s → %s  CR %.2f" % (ko, hx, rgb2hex(b), CR(b, CARD)))

cands = []
for r in range(120, 256):
    for g in range(110, 256):
        for bb in range(90, 256):
            c = (r, g, bb)
            if CR(c, WASH) < 4.5:
                continue
            if L(c) <= CHROME:
                continue
            if CR(c, CARD) < 4.5:
                continue
            Ls, C, h = lch(c)
            if C > 12.0:                       # 일반은 「거의 무채인 브라스」다 — 채도 상한 유지
                continue
            if gray_level(c) >= gray_level(RARE):   # 휘도 단조 유지
                continue
            if dE(c, RARE) < DISC:
                continue
            if min(dE(c, x) for _, x in AVOID2) < DISC:
                continue
            if arc(h, BRASS_H) > 20.0:         # 브라스 가족(LCh 색상) 유지
                continue
            cands.append(c)
print()
print("  조건: wash CR>=4.5 ∩ 카드면 CR>=4.5 ∩ 크롬대역 ∩ C*<=12 ∩ 회색조<희귀 ∩ 일반↔희귀 ΔE>=7.8")
print("        ∩ 예약·팩 전건 ΔE>=7.8 ∩ 브라스 LCh 호<=20°")
print("  해 %d색" % len(cands))
if not cands:
    print("  ⇒ ★ 못 넘긴다. 「일반색을 밝혀서 푸는 길」은 없다.")
else:
    # 일반↔희귀 ΔE 를 최대화(그것이 이 수정의 진짜 비용이므로)
    cands.sort(key=lambda c: (dE(c, RARE), min(dE(c, x) for _, x in AVOID2)), reverse=True)
    print("  %-9s %8s %7s %7s %8s | %7s %7s %7s %7s | %8s %8s %6s" %
          ("hex", "L", "L*", "C*", "h(LCh)", "wash", "카드면", "잠김카드", "잠김썸", "↔희귀ΔE", "↔잠김ΔE", "회색조"))
    for c in cands[:6]:
        Ls, C, h = lch(c)
        print("  %-9s %8.4f %7.1f %7.1f %8.1f | %7.2f %7.2f %7.2f %7.2f | %8.2f %8.2f %6d" %
              (rgb2hex(c), L(c), Ls, C, h, CR(c, WASH), CR(c, CARD), CR(c, LOCKED_CARD),
               CR(c, LOCKED_THUMB), dE(c, RARE), dE(c, LOCKED), gray_level(c)))
    cur = hex2rgb("#9C978C")
    Ls, C, h = lch(cur)
    print("  %-9s %8.4f %7.1f %7.1f %8.1f | %7.2f %7.2f %7.2f %7.2f | %8.2f %8.2f %6d  ← 현행" %
          ("#9C978C", L(cur), Ls, C, h, CR(cur, WASH), CR(cur, CARD), CR(cur, LOCKED_CARD),
           CR(cur, LOCKED_THUMB), dE(cur, RARE), dE(cur, LOCKED), gray_level(cur)))
    print()
    print("  ★ 진짜 비용 — 일반↔희귀 인접 ΔE 가 어떻게 되는가")
    print("    현행 %.2f  →  최선 후보 %.2f  (변화 %+.2f)" %
          (dE(cur, RARE), dE(cur if not cands else cands[0], RARE),
           dE(cands[0], RARE) - dE(cur, RARE)))
    print("    램프 4색 인접 ΔE:  현행 %s" %
          ["%.2f" % dE(hex2rgb(OURS[i][1]), hex2rgb(OURS[i + 1][1])) for i in range(3)])
    newramp = [cands[0], RARE, EPIC, LEG]
    print("                       교체 후 %s" %
          ["%.2f" % dE(newramp[i], newramp[i + 1]) for i in range(3)])
    print("    회색조 단차: 현행 %s → 교체 후 %s" %
          ([gray_level(hex2rgb(h)) for _, h in OURS], [gray_level(c) for c in newramp]))
    for kind in CVD.TYPES:
        v = [simc(c, kind) for c in newramp]
        v0 = [simc(hex2rgb(h), kind) for _, h in OURS]
        print("    색각 %s 램프 내부 최근접 ΔE: 현행 %.2f → 교체 후 %.2f" %
              (CVD.KOR[kind], min(dE(v0[i], v0[j]) for i in range(4) for j in range(i + 1, 4)),
               min(dE(v[i], v[j]) for i in range(4) for j in range(i + 1, 4))))
    nb = over(cands[0], RARITY_BORDER_ALPHA, CARD)
    print("    테두리 실효색: 현행 %s CR %.2f → 교체 후 %s CR %.2f" %
          (rgb2hex(over(cur, RARITY_BORDER_ALPHA, CARD)), CR(over(cur, RARITY_BORDER_ALPHA, CARD), CARD),
           rgb2hex(nb), CR(nb, CARD)))
    print("    ΔH*(후보, 브라스) = %.2f  (§29 가 「가족」 판정에 쓴 자)" % dH(cands[0], BRASS))


# ===========================================================================
# 8. 【추가 배정 ②】 카드 표면 조합 전량 — 두 번 빠뜨린 표를 한 번에 센다
# ===========================================================================
print()
print("=" * 78)
print("8. 카드 한 장에서 나올 수 있는 색 조합 **전량** (소스에서 조합을 읽는다)")
print("=" * 78)
print("  출처 CharacterInfoWindow.Cards.cs:277-300(면) / :315-318(테두리) / :423-431(리본)")
CARD_MUTED = tok["CardSurfaceMuted"][0]
THUMB_LOCKED = tok["ThumbSurfaceLocked"][0]
TRACK = hex2rgb("#3A4049")
TXT = {k: tok[k][0] for k in ("TextPrimary", "TextSecondary", "TextTertiary", "NonTextMuted")}
STATES = [("잠김(미보유)", CARD_MUTED, THUMB_LOCKED),
          ("보유·미착용", CARD, CARD_MUTED),
          ("보유·착용중", CARD, WASH)]
print()
print("8-1. 면 — 카드면 x 썸네일면 = 3조합 (4번째는 없다. 썸네일면이 3종인 것이 요점)")
print("  %-12s %-22s %-22s" % ("상태", "카드면", "썸네일면"))
for nm, sf, th in STATES:
    print("  %-12s %-22s %-22s" % (nm, "%s L %.4f" % (rgb2hex(sf), L(sf)), "%s L %.4f" % (rgb2hex(th), L(th))))
print()
print("8-2. 테두리 — 상태 3 + 등급 4 = **7색**. 한 그리드에 동시에 뜬다")


def borders(rar_a, worn_a, ramp=None):
    ramp = ramp or [hex2rgb(h) for _, h in OURS]
    out = [("선택", TXT["TextPrimary"]),
           ("호버", over((255, 255, 255), HOVER_ALPHA, CARD)),
           ("착용", over(WORN_BORDER, worn_a, CARD))]
    for (ko, _), c in zip(OURS, ramp):
        out.append(("등급 " + ko, over(c, rar_a, CARD)))
    return out


cur_b = borders(RARITY_BORDER_ALPHA, WORN_ALPHA)
print("  %-8s %-9s %8s %8s" % ("자리", "실효색", "카드면CR", "L*"))
for nm, c in cur_b:
    print("  %-8s %-9s %8.2f %8.1f" % (nm, rgb2hex(c), CR(c, CARD), lch(c)[0]))
pmin = min((dE(cur_b[i][1], cur_b[j][1]), cur_b[i][0], cur_b[j][0])
           for i in range(7) for j in range(i + 1, 7))
print("  21쌍 최소 ΔE %.2f (%s ↔ %s)  — §15 검산 2 의 9.06 %s" %
      (pmin[0], pmin[1], pmin[2], "재현" if abs(pmin[0] - 9.06) < 0.05 else "★불일치"))
inv = CR(cur_b[2][1], CARD) - max(CR(c, CARD) for nm, c in cur_b[3:])
print("  ★ 서열 위반: 착용 %.2f  −  등급 최대 %.2f  =  %+.2f  (양수여야 한다)" %
      (CR(cur_b[2][1], CARD), max(CR(c, CARD) for _, c in cur_b[3:]), inv))
print("     L* 로도: 착용 %.1f  vs  전설 %.1f  =  %+.1f" %
      (lch(cur_b[2][1])[0], lch(cur_b[6][1])[0], lch(cur_b[2][1])[0] - lch(cur_b[6][1])[0]))

print()
print("8-3. ★ 최소 간격을 무엇으로 잡는가 — 이 설계가 **이미 의지하고 있는** 간격에서 유도한다")
gaps = [("선택 ↔ 호버", lch(cur_b[0][1])[0] - lch(cur_b[1][1])[0]),
        ("호버 ↔ 착용", lch(cur_b[1][1])[0] - lch(cur_b[2][1])[0]),
        ("등급 일반 ↔ 희귀", lch(cur_b[4][1])[0] - lch(cur_b[3][1])[0]),
        ("등급 희귀 ↔ 영웅", lch(cur_b[5][1])[0] - lch(cur_b[4][1])[0]),
        ("등급 영웅 ↔ 전설", lch(cur_b[6][1])[0] - lch(cur_b[5][1])[0])]
for nm, g in gaps:
    print("    %-16s ΔL* %+6.1f" % (nm, g))
GAP_L = min(g for _, g in gaps if g > 0)
print("  ⇒ 이 사다리가 이미 「한 단」으로 쓰고 있는 **최소 ΔL\\* = %.1f**." % GAP_L)
print("     착용 ↔ 등급최대 간격도 그보다 작으면 안 된다. **하한 ΔL\\* >= %.1f 로 못박는다.**" % GAP_L)
print("     (CR 로만 재면 「밝다」가 색상에 오염된다 — §25 「자의 단위」와 같은 함정이다)")


print()
print("8-4. 해가 있는가 — (등급 α) x (착용 α) 2차원 전수. 조건 넷을 동시에")
print("  ① 등급 인접 ΔE >= 7.8 (§15 검산 1 의 α 하한 근거를 값이 아니라 **조건**으로 다시 건다)")
print("  ② 7색 21쌍 전량 ΔE >= 7.8")
print("  ③ L* 서열  선택 > 호버 > 착용 > 전설 > 영웅 > 희귀 > 일반")
print("  ④ 착용 − 전설 ΔL* >= %.1f (8-3 유도)" % GAP_L)


def ladder_check(ra, wa, ramp=None):
    b = borders(ra, wa, ramp)
    cs = [c for _, c in b]
    ls = [lch(c)[0] for c in cs]
    if min(dE(cs[3 + i], cs[3 + i + 1]) for i in range(3)) < DISC:
        return None
    if min(dE(cs[i], cs[j]) for i in range(7) for j in range(i + 1, 7)) < DISC:
        return None
    if not (ls[0] > ls[1] > ls[2] > ls[6] > ls[5] > ls[4] > ls[3]):
        return None
    g = ls[2] - ls[6]
    if g < GAP_L:
        return None
    return g


sols = []
for rai in range(400, 561):
    for wai in range(550, 1001, 5):
        ra, wa = rai / 1000.0, wai / 1000.0
        g = ladder_check(ra, wa)
        if g is not None:
            sols.append((g, ra, wa))
print()
print("  해 %d개 / 탐색 %d개" % (len(sols), 161 * 91))
if not sols:
    print("  ⇒ 두 α만으로는 못 푼다. 램프 값을 건드려야 한다.")
else:
    sols.sort(reverse=True)
    print("  %-8s %-8s %-8s %-8s %-9s %s" % ("등급α", "착용α", "간격ΔL*", "착용CR", "전설CR", "등급 인접 ΔE"))
    seen = set()
    for g, ra, wa in sols[:200]:
        k = (round(ra, 2), round(wa, 2))
        if k in seen:
            continue
        seen.add(k)
        b = borders(ra, wa)
        cs = [c for _, c in b]
        print("  %-8.3f %-8.3f %-8.2f %-8.2f %-9.2f %s" %
              (ra, wa, g, CR(cs[2], CARD), CR(cs[6], CARD),
               ["%.2f" % dE(cs[3 + i], cs[3 + i + 1]) for i in range(3)]))
        if len(seen) >= 8:
            break
    print()
    # 현행에서 **가장 적게** 움직이는 해 (α 이동량 합 최소)
    best = min(sols, key=lambda t: abs(t[1] - RARITY_BORDER_ALPHA) + abs(t[2] - WORN_ALPHA))
    g, ra, wa = best
    print("  ★ 최소 이동 해: 등급 α %.2f → %.3f (%+.3f) · 착용 α %.2f → %.3f (%+.3f) · 간격 ΔL* %.2f" %
          (RARITY_BORDER_ALPHA, ra, ra - RARITY_BORDER_ALPHA, WORN_ALPHA, wa, wa - WORN_ALPHA, g))
    # 착용 α만 올리는 해 / 등급 α만 내리는 해를 따로 뽑는다
    only_w = [t for t in sols if abs(t[1] - RARITY_BORDER_ALPHA) < 1e-9]
    only_r = [t for t in sols if abs(t[2] - WORN_ALPHA) < 1e-9]
    print("  ★ 착용 α만 올려서 푸는 해: %s" %
          ("최소 착용 α %.3f (간격 ΔL* %.2f)" % (min(only_w, key=lambda t: t[2])[2],
                                            min(only_w, key=lambda t: t[2])[0]) if only_w else "없음"))
    print("  ★ 등급 α만 내려서 푸는 해: %s" %
          ("최대 등급 α %.3f (간격 ΔL* %.2f)" % (max(only_r, key=lambda t: t[1])[1],
                                            max(only_r, key=lambda t: t[1])[0]) if only_r else "없음"))


print()
print("8-5. §15 검산 1 의 α 하한 0.4640 을 내 자로 재현하지 못했다 — 다시 유도한다")
lo_a, hi_a = 0.20, 0.55
for _ in range(40):
    mid = (lo_a + hi_a) / 2
    b = [c for _, c in borders(mid, WORN_ALPHA)][3:]
    if min(dE(b[i], b[i + 1]) for i in range(3)) >= DISC:
        hi_a = mid
    else:
        lo_a = mid
print("  내 자의 등급 α 하한(인접 ΔE >= 7.8) = **%.4f**  (§15 기재값 0.4640)" % hi_a)
for a in (0.45, 0.4640, 0.50, 0.55):
    b = [c for _, c in borders(a, WORN_ALPHA)][3:]
    print("    α %.4f → 인접 ΔE %s (최소 %.2f)  §15 기재: %s" %
          (a, ["%.2f" % dE(b[i], b[i + 1]) for i in range(3)],
           min(dE(b[i], b[i + 1]) for i in range(3)),
           "7.59" if abs(a - 0.45) < 1e-9 else ("9.06/11.13/11.01" if abs(a - 0.55) < 1e-9 else "-")))
print("  ⇒ **α0.45 에서 내 값은 %.2f 이고 §15 는 7.59 다.** 자가 갈라져 있다." %
      min(dE(x, y) for x, y in zip([c for _, c in borders(0.45, WORN_ALPHA)][3:6],
                                   [c for _, c in borders(0.45, WORN_ALPHA)][4:7])))
print("     내 자는 교정 55/55 를 통과했고 §15 의 다른 값(9.06 · 실효색 hex 7종)은 전부 재현한다.")
print("     ★ 그래서 **0.4640 을 값으로 인용하지 않고 조건(인접 ΔE >= 7.8)으로 다시 건다.**")

print()
print("8-6. 【권고】 착용 α 만 올린다 — 등급 체계를 한 값도 안 건드리는 해")
print("  %-8s %-9s %8s %8s %10s %10s %s" %
      ("착용α", "실효색", "CR", "L*", "간격ΔL*", "vs호버ΔE", "판정"))
for a in (0.75, 0.85, 0.89, 0.90, 0.95, 1.00):
    b = borders(RARITY_BORDER_ALPHA, a)
    cs = [c for _, c in b]
    g = lch(cs[2])[0] - lch(cs[6])[0]
    ok = ladder_check(RARITY_BORDER_ALPHA, a) is not None
    print("  %-8.2f %-9s %8.2f %8.1f %10.2f %10.2f %s" %
          (a, rgb2hex(cs[2]), CR(cs[2], CARD), lch(cs[2])[0], g, dE(cs[2], cs[1]),
           "통과" if ok else "★불통"))
print("  ★ α1.00 은 **불투명**이라 「알파 채널의 법칙」(UiChrome) 걱정이 아예 없어진다.")
print("    카드 외곽선은 지금 Flatten 을 안 거치고 생 α 를 얹는다(Cards.cs:315-318) —")
print("    같은 파일 :395 의 액션 버튼 외곽선은 Flatten 을 거친다. **두 자리의 규약이 다르다.**")
print("    [미확인] 이것이 실제로 바탕화면을 비추는지는 캡처로만 확정된다. 다만 Flatten 은")
print("    **겉보기 색을 안 바꾸므로**(정의상) 이 절의 어떤 숫자도 안 움직인다 — 비용 0의 정리다.")

print()
print("8-7. 두 배정의 **상호작용** — ①의 일반색 교체가 ②의 사다리를 깨는가")
print("  ★ 그리고 ① 최선 후보 #999897 은 C* %.1f 로 **완전 무채**다 — TEAM.md 색 예약이 금한 회색이다."
      % lch(hex2rgb("#999897"))[1])
print("    그래서 조건을 두 개 더 걸고 다시 찾는다: C* >= 4.0 (브라스 기운 유지) ∩ ΔE(잠김) >= 13.88(현행 이상)")
cands2 = [c for c in cands if lch(c)[1] >= 4.0 and dE(c, LOCKED) >= dE(hex2rgb("#9C978C"), LOCKED)]
print("    해 %d색" % len(cands2))
if cands2:
    cands2.sort(key=lambda c: dE(c, RARE), reverse=True)
    print("  %-9s %7s %7s %8s | %6s %6s | %8s %8s %6s | %s" %
          ("hex", "L*", "C*", "h(LCh)", "wash", "카드면", "↔희귀ΔE", "↔잠김ΔE", "회색조", "사다리"))
    for c in cands2[:5]:
        Ls, C, h = lch(c)
        ok = ladder_check(RARITY_BORDER_ALPHA, 0.90, [c, RARE, EPIC, LEG])
        print("  %-9s %7.1f %7.1f %8.1f | %6.2f %6.2f | %8.2f %8.2f %6d | %s" %
              (rgb2hex(c), Ls, C, h, CR(c, WASH), CR(c, CARD), dE(c, RARE), dE(c, LOCKED),
               gray_level(c), "통과 ΔL* %.2f" % ok if ok else "★불통"))
    top = cands2[0]
    print("  ΔH*(후보 %s, 브라스) = %.2f · ΔE(후보, 팩12 최근접) = %.2f" %
          (rgb2hex(top), dH(top, BRASS), min(dE(top, c) for _, c in pack12)))
    for kind in CVD.TYPES:
        v = [simc(x, kind) for x in [top, RARE, EPIC, LEG]]
        v0 = [simc(hex2rgb(h), kind) for _, h in OURS]
        print("  색각 %s: 현행 %.2f → 교체 %.2f" %
              (CVD.KOR[kind], min(dE(v0[i], v0[j]) for i in range(4) for j in range(i + 1, 4)),
               min(dE(v[i], v[j]) for i in range(4) for j in range(i + 1, 4))))
else:
    print("  ⇒ ★ C* >= 4 를 지키면서 wash 4.5 를 넘는 일반색은 **없다.** ①은 램프 쪽에서 못 푼다.")


print()
print("8-8. ★ 확정 후보 고르기 — 조건을 다 걸고 **현행에서 가장 적게 움직이는** 색")
print("  추가 조건: wash CR >= 4.55(경계 여유) ∩ 희귀와 회색조 단차 >= 15 ∩ ΔH*(브라스) <= 3.0")
CUR = hex2rgb("#9C978C")
fin = [c for c in cands2
       if CR(c, WASH) >= 4.55
       and gray_level(RARE) - gray_level(c) >= 15
       and dH(c, BRASS) <= 3.0]
print("  해 %d색" % len(fin))
if fin:
    fin.sort(key=lambda c: dE(c, CUR))
    print("  %-9s %7s %7s %7s | %6s %6s %6s %6s | %7s %7s %6s %6s" %
          ("hex", "L*", "C*", "ΔH*브", "wash", "카드면", "잠김카", "잠김썸", "↔희귀", "↔잠김", "회색조", "이동ΔE"))
    for c in fin[:6]:
        Ls, C, h = lch(c)
        print("  %-9s %7.1f %7.1f %7.2f | %6.2f %6.2f %6.2f %6.2f | %7.2f %7.2f %6d %6.2f" %
              (rgb2hex(c), Ls, C, dH(c, BRASS), CR(c, WASH), CR(c, CARD), CR(c, LOCKED_CARD),
               CR(c, LOCKED_THUMB), dE(c, RARE), dE(c, LOCKED), gray_level(c), dE(c, CUR)))
    PICK = fin[0]
    print()
    print("  ★★ 권고값  일반 = %s   (현행 #9C978C 에서 ΔE %.2f 이동)" % (rgb2hex(PICK), dE(PICK, CUR)))
    print("     Unity  new Color(%.3ff, %.3ff, %.3ff, 1f)" % tuple(v / 255 for v in PICK))
    rt = tuple(int(round(float("%.3f" % (v / 255)) * 255)) for v in PICK)
    print("     3자리 float 왕복: %s → %s  %s" %
          (rgb2hex(PICK), rgb2hex(rt), "무손실" if rt == PICK else "★손실"))
    NEW = [PICK, RARE, EPIC, LEG]
    print()
    print("     전량 검산")
    print("       회색조     %s → %s (단차 %s)" %
          ([gray_level(hex2rgb(h)) for _, h in OURS], [gray_level(c) for c in NEW],
           [gray_level(NEW[i + 1]) - gray_level(NEW[i]) for i in range(3)]))
    print("       인접 ΔE    %s → %s" %
          (["%.2f" % dE(hex2rgb(OURS[i][1]), hex2rgb(OURS[i + 1][1])) for i in range(3)],
           ["%.2f" % dE(NEW[i], NEW[i + 1]) for i in range(3)]))
    print("       네 배경 CR %s" % ["%.2f" % CR(PICK, b) for _, b in BGS])
    print("       예약색 ΔE  착용 %.2f · 착용합성 %.2f · 잠김 %.2f · 브라스 %.2f · TextOnAccent %.2f" %
          (dE(PICK, WORN_BORDER), dE(PICK, WORN_COMP), dE(PICK, LOCKED), dE(PICK, BRASS),
           dE(PICK, hex2rgb("#FBE4C0"))))
    print("       팩12 최근접 ΔE %.2f · 대역 %s (L %.4f)" %
          (min(dE(PICK, c) for _, c in pack12), band_of(PICK), L(PICK)))
    for kind in CVD.TYPES:
        v = [simc(x, kind) for x in NEW]
        v0 = [simc(hex2rgb(h), kind) for _, h in OURS]
        print("       색각 %s 램프 내부 최근접 ΔE %.2f → %.2f" %
              (CVD.KOR[kind], min(dE(v0[i], v0[j]) for i in range(4) for j in range(i + 1, 4)),
               min(dE(v[i], v[j]) for i in range(4) for j in range(i + 1, 4))))
    for wa in (0.90, 1.00):
        g = ladder_check(RARITY_BORDER_ALPHA, wa, NEW)
        print("       사다리(등급α %.2f · 착용α %.2f): %s" %
              (RARITY_BORDER_ALPHA, wa, "통과 간격 ΔL* %.2f" % g if g else "★불통"))
    b = borders(RARITY_BORDER_ALPHA, 1.00, NEW)
    print("       7색 테두리 %s" % [(n, rgb2hex(c)) for n, c in b])
    print("       21쌍 최소 ΔE %.2f" %
          min(dE(b[i][1], b[j][1]) for i in range(7) for j in range(i + 1, 7)))
    print("       ±1 LSB 강건성(모든 조건 재검): %d/27" %
          sum(1 for dr in (-1, 0, 1) for dg in (-1, 0, 1) for db in (-1, 0, 1)
              if (lambda q: CR(q, WASH) >= 4.5 and CR(q, CARD) >= 4.5 and L(q) > CHROME
                  and lch(q)[1] >= 4.0 and dE(q, LOCKED) >= 13.88 and dE(q, RARE) >= DISC
                  and gray_level(q) < gray_level(RARE)
                  and min(dE(q, x) for _, x in AVOID2) >= DISC)
              ((max(0, min(255, PICK[0] + dr)), max(0, min(255, PICK[1] + dg)),
                max(0, min(255, PICK[2] + db))))))
else:
    print("  ⇒ 해 없음.")


print()
print("8-9. 후보 6종 강건성 비교 (±1 LSB 이웃 27색 중 전 조건 통과 수)")


def robust(p):
    n = 0
    for dr in (-1, 0, 1):
        for dg in (-1, 0, 1):
            for db in (-1, 0, 1):
                q = (max(0, min(255, p[0] + dr)), max(0, min(255, p[1] + dg)), max(0, min(255, p[2] + db)))
                if (CR(q, WASH) >= 4.5 and CR(q, CARD) >= 4.5 and L(q) > CHROME
                        and lch(q)[1] >= 4.0 and dE(q, LOCKED) >= 13.88 and dE(q, RARE) >= DISC
                        and gray_level(q) < gray_level(RARE)
                        and min(dE(q, x) for _, x in AVOID2) >= DISC):
                    n += 1
    return n


print("  %-9s %8s %8s %8s %8s %8s" % ("hex", "이동ΔE", "wash", "ΔH*브", "↔희귀", "강건성"))
scored = []
for c in fin[:40]:
    r = robust(c)
    scored.append((r, -dE(c, CUR), c))
    if dE(c, CUR) <= 2.0:
        print("  %-9s %8.2f %8.2f %8.2f %8.2f %6d/27" %
              (rgb2hex(c), dE(c, CUR), CR(c, WASH), dH(c, BRASS), dE(c, RARE), r))
scored.sort(reverse=True)
print("  ⇒ 강건성 최대 = %s (%d/27, 이동 ΔE %.2f)" %
      (rgb2hex(scored[0][2]), scored[0][0], -scored[0][1]))


print()
print("8-10. ★★ 최종 권고값 확정 — 강건성 27/27 중 브라스 가족(ΔH*) 최우수")
top27 = [c for c in fin[:60] if robust(c) == 27]
top27.sort(key=lambda c: (dH(c, BRASS), dE(c, CUR)))
FINAL = top27[0]
NEW = [FINAL, RARE, EPIC, LEG]
print("  후보(27/27) 중 ΔH* 최소: %s" % [(rgb2hex(c), round(dH(c, BRASS), 2)) for c in top27[:5]])
print()
print("  ★★ 확정 권고  등급 일반 = %s   (현행 #9C978C, 이동 ΔE %.2f)" % (rgb2hex(FINAL), dE(FINAL, CUR)))
f3 = tuple(round(v / 255, 3) for v in FINAL)
rt = tuple(int(round(x * 255)) for x in f3)
print("     Unity new Color(%.3ff, %.3ff, %.3ff, 1f) · 3자리 왕복 %s → %s %s" %
      (f3[0], f3[1], f3[2], rgb2hex(FINAL), rgb2hex(rt), "무손실" if rt == FINAL else "★손실"))
print("     L %.4f (%s) · L* %.1f · C* %.1f · LCh h %.1f° · ΔH*(브라스) %.2f" %
      (L(FINAL), band_of(FINAL), lch(FINAL)[0], lch(FINAL)[1], lch(FINAL)[2], dH(FINAL, BRASS)))
print("     네 배경 CR: %s  (텍스트 하한 4.5)" %
      "  ".join("%s %.2f" % (n.split()[0], CR(FINAL, b)) for n, b in BGS))
print("     회색조 %s 단차 %s · 인접 ΔE %s" %
      ([gray_level(c) for c in NEW], [gray_level(NEW[i + 1]) - gray_level(NEW[i]) for i in range(3)],
       ["%.2f" % dE(NEW[i], NEW[i + 1]) for i in range(3)]))
print("     예약 ΔE: 착용 %.2f · 착용합성 %.2f · 잠김 %.2f · 브라스 %.2f · TextOnAccent %.2f · 팩12 %.2f" %
      (dE(FINAL, WORN_BORDER), dE(FINAL, WORN_COMP), dE(FINAL, LOCKED), dE(FINAL, BRASS),
       dE(FINAL, hex2rgb("#FBE4C0")), min(dE(FINAL, c) for _, c in pack12)))
print("     색각 램프 내부 최근접 ΔE: " + " · ".join(
    "%s %.2f→%.2f" % (CVD.KOR[k],
                      min(dE(simc(hex2rgb(OURS[i][1]), k), simc(hex2rgb(OURS[j][1]), k))
                          for i in range(4) for j in range(i + 1, 4)),
                      min(dE(simc(NEW[i], k), simc(NEW[j], k)) for i in range(4) for j in range(i + 1, 4)))
    for k in CVD.TYPES))
print("     ±1 LSB 강건성 %d/27" % robust(FINAL))
for wa in (0.90, 1.00):
    print("     사다리(등급α %.2f · 착용α %.2f) %s" %
          (RARITY_BORDER_ALPHA, wa,
           "통과 간격 ΔL* %.2f" % ladder_check(RARITY_BORDER_ALPHA, wa, NEW)
           if ladder_check(RARITY_BORDER_ALPHA, wa, NEW) else "★불통"))
bb = borders(RARITY_BORDER_ALPHA, 1.00, NEW)
print("     테두리 7색 %s · 21쌍 최소 ΔE %.2f" %
      ([rgb2hex(c) for _, c in bb], min(dE(bb[i][1], bb[j][1]) for i in range(7) for j in range(i + 1, 7))))
print()
print("  [계산상 통과 / 실기 미확인] 캡처 없음.")


print()
print("8-11. ★ 8-10 을 정정한다 — ΔH* 만 최소화했더니 **테두리 21쌍 최소 ΔE 가 7.87** 로 벼랑에 붙었다")
print("  목적함수를 다시 짠다: 강건성 >= 26/27 ∩ 테두리 21쌍 최소 ΔE >= 8.5 ∩ ΔH*(브라스) <= 2.0")
print("  그 안에서 **현행에서 가장 적게 움직이는** 색.")


def border_min(p):
    b = borders(RARITY_BORDER_ALPHA, 1.00, [p, RARE, EPIC, LEG])
    return min(dE(b[i][1], b[j][1]) for i in range(7) for j in range(i + 1, 7))


ok2 = [c for c in fin[:80] if robust(c) >= 26 and border_min(c) >= 8.5 and dH(c, BRASS) <= 2.0]
ok2.sort(key=lambda c: dE(c, CUR))
print("  해 %d색: %s" % (len(ok2), [(rgb2hex(c), round(dE(c, CUR), 2)) for c in ok2[:6]]))
if ok2:
    FINAL = ok2[0]
    NEW = [FINAL, RARE, EPIC, LEG]
    print()
    print("  ★★★ 확정 권고  등급 일반 = %s  (현행 #9C978C 에서 ΔE %.2f)" % (rgb2hex(FINAL), dE(FINAL, CUR)))
    f3 = tuple(round(v / 255, 3) for v in FINAL)
    rt = tuple(int(round(x * 255)) for x in f3)
    print("      Unity new Color(%.3ff, %.3ff, %.3ff, 1f) · 3자리 왕복 %s %s" %
          (f3[0], f3[1], f3[2], rgb2hex(rt), "무손실" if rt == FINAL else "★손실"))
    print("      L %.4f (%s) · L* %.1f · C* %.1f · LCh h %.1f° · ΔH*(브라스) %.2f · 강건성 %d/27" %
          (L(FINAL), band_of(FINAL), lch(FINAL)[0], lch(FINAL)[1], lch(FINAL)[2],
           dH(FINAL, BRASS), robust(FINAL)))
    print("      네 배경 CR %s" % "  ".join("%s %.2f" % (n.split()[0], CR(FINAL, b)) for n, b in BGS))
    print("      회색조 %s 단차 %s · 인접 ΔE %s" %
          ([gray_level(c) for c in NEW], [gray_level(NEW[i + 1]) - gray_level(NEW[i]) for i in range(3)],
           ["%.2f" % dE(NEW[i], NEW[i + 1]) for i in range(3)]))
    print("      예약 ΔE 착용 %.2f · 착용합성 %.2f · 잠김 %.2f · 브라스 %.2f · TextOnAccent %.2f · 팩12 %.2f" %
          (dE(FINAL, WORN_BORDER), dE(FINAL, WORN_COMP), dE(FINAL, LOCKED), dE(FINAL, BRASS),
           dE(FINAL, hex2rgb("#FBE4C0")), min(dE(FINAL, c) for _, c in pack12)))
    print("      색각 " + " · ".join(
        "%s %.2f→%.2f" % (CVD.KOR[k],
                          min(dE(simc(hex2rgb(OURS[i][1]), k), simc(hex2rgb(OURS[j][1]), k))
                              for i in range(4) for j in range(i + 1, 4)),
                          min(dE(simc(NEW[i], k), simc(NEW[j], k)) for i in range(4) for j in range(i + 1, 4)))
        for k in CVD.TYPES))
    print("      테두리 21쌍 최소 ΔE %.2f · 사다리(착용α1.00) 간격 ΔL* %.2f · (착용α0.90) %.2f" %
          (border_min(FINAL), ladder_check(RARITY_BORDER_ALPHA, 1.00, NEW),
           ladder_check(RARITY_BORDER_ALPHA, 0.90, NEW)))
    print("      테두리 7색 %s" % [rgb2hex(c) for _, c in borders(RARITY_BORDER_ALPHA, 1.00, NEW)])
