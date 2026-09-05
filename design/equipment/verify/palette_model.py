# -*- coding: utf-8 -*-
"""장비 재질 팔레트 모델 — design-art, 2026-09-05 (사용자 지시: "각각 아이템들의 색상을 채워 넣어야함")

이 파일이 정하는 것
  1. 42종 × (재질색 material · 보조 재질색 material2)      — 값은 전부 .asset 에 이미 있는 25색에서 고른다(새 hex 0개)
  2. 인계본 16종의 조각(r17_coords.txt 이름) → 색 역할 표   — M / M2 / SH(그늘) / W(흰 하이라이트) / NONE / INK
  3. 표면별 색 해석                                        — 카드(선 = CardIconInk) · 몸(선 = 유저 잉크)
  4. 검산(전부 숫자)                                       — 배경 6종 대비 · 카드 대비 · 등급색 충돌 · 팩 6 · 흰 잉크 · 선 색 4안 비교

★ 계산기는 design/art/verify/colorlab.py 를 그대로 쓰고, 쓰기 전에 교정한다(흰/검 21.0 · 동일색 1.0 · #767676/흰 4.5422 ·
  WornColor 독립 실측 2건 · LAB 3점). 교정이 깨지면 아무 숫자도 내지 않고 죽는다.
★ 프로덕션 .cs / .asset 0줄. r17_* 파일은 읽기만 한다. 산출물은 palette_* 접두사뿐이다.
★ 오프라인 계산이다 — 최종 판정은 실제 빌드 캡처로만.
"""
import os, sys, math
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
ART = os.path.abspath(os.path.join(HERE, "../../art/verify")); sys.path.insert(0, ART)
import colorlab as CL

# ============================================================================
# 0. 자 — 배경 · 토큰 (UiChrome.cs / 과제 배경 / PALETTE_SPEC §0)
# ============================================================================
BG = {
    "밝은 바탕화면 #F2F2F2": "#F2F2F2",
    "어두운 바탕화면 #1E1E1E": "#1E1E1E",
    "검은 잉크 머리 #111111": "#111111",
    "흰 잉크 머리 #FFFFFF": "#FFFFFF",
    "카드 썸네일 바탕 #15181E": "#15181E",     # UiChrome.CardSurfaceMuted (AccessoryHandoffPalette.Card 의 headFill 도 이것)
    "카드 면 #1B1F26": "#1B1F26",             # UiChrome.CardSurface
}
BG_SPEC = {"종이 무대 #E9EAE6": "#E9EAE6", "목탄 무대 #25282E": "#25282E"}   # PALETTE_SPEC §0 자립 대역의 두 무대
MIN_NONTEXT, MIN_TEXT = 3.0, 4.5                                          # UiChrome.MinNonTextContrast / MinTextContrast
BRASS = "#C8A15A"                 # UiChrome.Accent — 크롬(창 안)
CARD_INK = "#E8E2D6"              # UiChrome.CardIconInk — 인계본 C
INK_BLACK, INK_WHITE = "#111111", "#FFFFFF"
CHARCOAL = "#252829"              # UiChrome.InkContrastCharcoal — 흰 잉크의 반대쪽 눈
RARITY = {"일반": "#9C978C", "희귀": "#BCAC8B", "영웅": "#DEC081", "전설": "#FFD375"}   # UiChrome._rarityRamp (R9 보정판)
RESERVED = {"TextTertiary(잠긴 실루엣)": "#8B939F", "CardBorderWorn(착용 중)": "#5DA1F5"}  # TEAM.md 색 예약 2건
SHADE = 0.28                      # AccessoryTone.ShadeFactor
HI_ALPHA = 0.42                   # AccessoryTone.HighlightWhiteAlpha
GRAD_MEAN = 0.21                  # AccessoryCardWash.GradientMeanAlpha (현행 카드 워시 — 비교용)
PACKS = [("오피스 워커", 222, "#456ECC", "#6080CC"), ("사이버 아포칼립스", 172, "#009682", "#518C84"),
         ("네온 낙서", 312, "#CC1BA9", "#9C5A8E"), ("스포츠", 8, "#CC3F29", "#9E655C"),
         ("컬러 잉크", 268, "#9768CC", "#8563AB"), ("밀리터리", 80, "#639400", "#798C51")]   # PackPaletteGateTests 동결값

# ============================================================================
# 1. 재질 25색 — 이름은 ItemCatalog.cs 주석의 것, 값은 .asset(2026-09-05 파싱)
# ============================================================================
MAT = {
    "Ivory": "#96814F", "Wool": "#BA7636", "Felt": "#5577AE", "Gold": "#9B7922", "GoldLight": "#988540",
    "Silver": "#587398", "DarkLens": "#5075B5", "Leather": "#BA5928", "Canvas": "#AB7942", "Paper": "#6787B9",
    "Toy": "#C6443C", "TintHead": "#CC5512", "TintEyes": "#20878C", "TintNeck": "#5A8C3C", "NeckDeep": "#428C24",
    "TintBack": "#955CCC", "Accent(Blue)": "#3378CC", "CapeRed": "#CC3C3C",
    "HairBald": "#936C3F", "HairBowl": "#A0622A", "HairBowlLit": "#A07830", "HairBrown": "#A16A28",
    "HairRed": "#BD501F", "HairRedLit": "#AF651C",
    "InkTone": "#D6DBE3", "InkDimTone": "#8B939F",      # 잉크 표식 2건 — 색이 아니라 지시(몸에서 유저 잉크로 대체)
}
INK_MARKS = {"#D6DBE3", "#8B939F"}
def name_of(hexs):
    for k, v in MAT.items():
        if v.upper() == hexs.upper(): return k
    return "?"

# ============================================================================
# 2. 42종 표 — (id, 한글, 슬롯, material, material2, 현행 .asset 과 다른가, 사유)
#    ★ 인계본 16종은 handoff kind 로도 적는다. 값은 .asset tone0/tone1 그대로이고, 예외 1건(왕관 보석)만 바꾼다.
# ============================================================================
ITEMS = [
    # id, 한글, 슬롯, handoff kind, material, material2, 변경?, 근거
    ("equip.head.cap", "천모자(야구모자)", "HEAD", "clothhat", "#96814F", "#CC5512", False, "관 아이보리 · 챙 주황(보조색 1조각 = 챙, SHAPE_SPEC cap) · 띠 = 그늘"),
    ("equip.head.fur", "털모자", "HEAD", "furhat", "#BA7636", "#96814F", False, "관 울 · 털 단+폼폼 아이보리(대역 상한 L 0.228 — 흰색은 밝은 바탕에서 1.1:1 이라 불가)"),
    ("equip.head.fedora", "중절모", "HEAD", "fedora", "#5577AE", "#CC5512", False, "펠트 파랑 · 띠 주황"),
    ("equip.head.crown", "왕관", "HEAD", "crown", "#9B7922", "#C6443C", True, "★ 보석 = Toy 빨강(현행 GoldLight #988540 은 금과 대비 1.15 · ΔE 아래 표 — 보석으로 안 읽힌다). 새 hex 0"),
    ("equip.eyes.sunglasses", "선글라스", "EYES", "sunglasses", "#5075B5", "#587398", False, "렌즈 = 그늘 SH #162133(M×0.28, R19) · 테/다리/코다리 Silver 선 — 원안 M 렌즈는 테와 1.05 로 테가 안 읽혔다"),
    ("equip.eyes.round", "동그란안경", "EYES", "roundglasses", "#587398", "#20878C", False, "테 Silver 선 · 렌즈 = 몸 채움 없음(투명 = 머리가 비친다) · 카드만 유리 워시 α0.16(인계본 값)"),
    ("equip.eyes.goggles", "고글", "EYES", "goggles", "#587398", "#CC5512", False, "프레임/끈 Silver · 렌즈 주황 불투명"),
    ("equip.eyes.monocle", "외알안경", "EYES", "monocle", "#9B7922", "#587398", False, "금테·사슬·구슬 Gold · 렌즈 = 몸 채움 없음 · 카드만 유리 워시 α0.2(Silver)"),
    ("equip.neck.bowtie", "나비넥타이", "NECK", "bowtie", "#5A8C3C", "#96814F", False, "날개 초록 · 매듭 아이보리"),
    ("equip.neck.striped", "줄무늬타이", "NECK", "stripedtie", "#428C24", "#96814F", False, "타이 진초록 · 줄무늬 아이보리 (2색)"),
    ("equip.neck.scarf", "목도리", "NECK", "scarf", "#CC5512", "#BA5928", False, "천 주황 · 술(fringe) 가죽빛 선"),
    ("equip.neck.bell", "방울목걸이", "NECK", "bellnecklace", "#BA5928", "#9B7922", False, "목줄 가죽 선 · 방울·구슬·추 Gold(놋쇠, 염색 불가 §11)"),
    ("equip.shoulders.cape", "짧은망토", "BACK", "shortcape", "#CC3C3C", "#96814F", False, "천 빨강 · 칼라 아이보리 · 걸쇠 = 빨강+잉크 윤곽 (인계본 #D2402F 는 L 0.176 대역 안이지만 WornColor 비항등 V0.824→#CC3E2E · 무대 뒤판 #A8332A/칼라 #8E241C 는 L 0.109/0.071 대역 밖(어두운 바탕 2.52/1.92) → .asset #CC3C3C 항등)"),
    ("equip.shoulders.long_cape", "긴망토", "BACK", "longcape", "#CC3C3C", "#96814F", False, "짧은망토와 같은 천"),
    ("equip.shoulders.wings", "날개", "BACK", "wings", "#6787B9", "#955CCC", False, "깃 Paper 파랑 · 척추 라벤더 · ★ 그룹 α0.40 → 1.0(장비 불투명)"),
    ("equip.shoulders.backpack", "배낭", "BACK", "backpack", "#AB7942", "#5A8C3C", False, "캔버스 · 덮개/버클 초록 · ★ 그룹 α0.40 → 1.0"),
    # ---- 나머지 26종: 현행 .asset 그대로(v1 경로 = 이미 재질색 불투명) ----
    ("equip.eyes.browline", "뿔테 안경", "EYES", None, "#5075B5", "#20878C", False, "PORT_SPEC §6-1 그대로"),
    ("equip.eyes.patch", "안대", "EYES", None, "#5577AE", "#20878C", False, "§6-1"),
    ("equip.head.beret", "베레모", "HEAD", None, "#5577AE", "#CC5512", False, "§6-1"),
    ("equip.head.straw", "밀짚모자", "HEAD", None, "#AB7942", "#CC5512", False, "§6-1 · 염색 불가(밀짚)"),
    ("equip.neck.bandana", "반다나", "NECK", None, "#5A8C3C", "#428C24", False, "§6-1"),
    ("equip.neck.pendant", "펜던트 목걸이", "NECK", None, "#587398", "#9B7922", False, "§6-1(보조색 = 펜던트 금)"),
    ("equip.shoulders.fairy_wings", "요정 날개", "BACK", None, "#6787B9", "#955CCC", False, "§6-1"),
    ("equip.shoulders.poncho", "판초", "BACK", None, "#BA7636", "#BA5928", False, "§6-1"),
    ("look.hair.bald", "민머리", "HAIR", None, "#936C3F", "#CC5512", False, "머리카락 계열(PALETTE_SPEC §11-3 통합 권고는 리더 보류)"),
    ("look.hair.bowl", "바가지머리", "HAIR", None, "#A0622A", "#A07830", False, ""),
    ("look.hair.cowlick", "삐친머리", "HAIR", None, "#A16A28", "#A07830", False, ""),
    ("look.hair.curly", "곱슬", "HAIR", None, "#BD501F", "#AF651C", False, ""),
    ("look.hair.neat", "단정한머리", "HAIR", None, "#A0622A", "#A07830", False, ""),
    ("look.hair.ponytail", "포니테일", "HAIR", None, "#BD501F", "#AF651C", False, ""),
    ("look.fx.bubble", "물방울", "FX", None, "#3378CC", "#5075B5", False, "캐릭터 밖 물체 — 고정색(§16-3)"),
    ("look.fx.dust", "먼지구름", "FX", None, "#8B939F", "#D6DBE3", False, "잉크 표식(캐릭터가 일으킨 것)"),
    ("look.fx.footprint", "발자국", "FX", None, "#D6DBE3", "#D6DBE3", False, "잉크 표식"),
    ("look.fx.leaf", "나뭇잎", "FX", None, "#5A8C3C", "#9B7922", False, "고정색"),
    ("look.fx.none", "없음", "FX", None, "#8B939F", "#8B939F", False, "잉크 표식"),
    ("look.fx.sparkle", "반짝임", "FX", None, "#9B7922", "#988540", False, "고정색(빛)"),
    ("look.pet.ball", "작은공", "PET", None, "#C6443C", "#6787B9", False, ""),
    ("look.pet.balloon", "풍선", "PET", None, "#C6443C", "#6787B9", False, ""),
    ("look.pet.cursor", "커서친구", "PET", None, "#3378CC", "#6787B9", False, ""),
    ("look.pet.mini", "리틀스틱메이트", "PET", None, "#D6DBE3", "#D6DBE3", False, "잉크 표식(몸의 일부로 읽힌다)"),
    ("look.pet.plane", "종이비행기", "PET", None, "#6787B9", "#587398", False, ""),
    ("look.pet.snail", "달팽이", "PET", None, "#BA7636", "#96814F", False, ""),
]
assert len(ITEMS) == 42, len(ITEMS)
BY_KIND = {it[3]: it for it in ITEMS if it[3]}
assert len(BY_KIND) == 16

# ============================================================================
# 3. 인계본 16종 조각 → 색 역할 (r17_coords.txt 조각 이름 = 인계본 call+index)
#    fill: M | M2 | SH | W | NONE | CARDWASH(M2, α)   line: INK | M | M2 | W | None
#    ★ 규칙(문장은 docs/EQUIPMENT_PALETTE.md §2):  채움이 있는 조각의 윤곽 = 잉크(카드 CardIconInk · 몸 유저 잉크)
#                                                채움 없는 독립 선(안경 다리·코다리·목줄·끈·술·사슬) = 그 조각의 재질색(대역 안)
#                                                채움 위 내부 낱선(결·주름·중앙 막대) = 잉크(인계본 알파 그대로)
# ============================================================================
ROLE = {
    "clothhat":   {"B0": ("M", "INK"), "F1": ("SH", None), "B2": ("M2", "INK"), "H3": (None, "W")},
    "furhat":     {"CB0": ("M2", "INK"), "B1": ("M", "INK"), "H2": (None, "W"), "B3": ("M2", "INK"), "S4": (None, "INK")},
    "fedora":     {"B0": ("M", "INK"), "F1": ("M2", None), "B2": ("M", "INK"), "H3": (None, "W")},
    # ★ R19 교차 확인(design-equipment ROLE_FIX 4건 + 왕관 뒷벽) 반영 — 판정 근거는 docs/EQUIPMENT_PALETTE.md §11.
    #   BW(왕관 안쪽 뒷벽, 뒤층) = SH(Gold×0.28 = #2B220A) · 윤곽 없음. 리더 판정(봉우리 사이 머리 노출 없음 — 기하 불가)으로 「검은 머리↔벽」은 무의미해졌고,
    #   남는 자는 벽↔봉우리 옆면이다: M 은 1.00(실루엣을 먹는다) · SH 는 3.86. 윤곽을 두면 봉우리 사이에 수평 잉크 막대가 생기므로 None.
    "crown":      {"B0": ("M", "INK"), "B1": ("M", "INK"), "CF2": ("M2", None), "CF3": ("M2", None), "CF4": ("M2", None),
                   "CB5": ("M", "INK"), "CB6": ("M", "INK"), "CB7": ("M", "INK"), "H8": (None, "W"), "BW": ("SH", None)},
    #   선글라스 렌즈 = SH(M×0.28 = #162133) + 테 M2 선: 원안 M 렌즈↔M2 테 1.05 로 테가 안 읽혔다. SH↔테 3.32. 렌즈 배경은 머리뿐(바탕화면 아님)이라 대역 밖 허용(R-5′).
    "sunglasses": {"B0": ("SH", "M2"), "B1": ("SH", "M2"), "S2": (None, "M2"), "S3": (None, "M2"), "S4": (None, "M2"), "H5": (None, "W")},
    "roundglasses": {"CB0": (("CARDWASH", 0.16), "M"), "CB1": (("CARDWASH", 0.16), "M"), "S2": (None, "M"), "S3": (None, "M"),
                     "S4": (None, "M"), "H5": (None, "W"), "H6": (None, "W")},
    #   고글 렌즈 원 = M2 채움·윤곽 없음(경계는 색상 ΔE 91.2 가 선다, 휘도 1.13 — 회색조에서는 판 실루엣만 남는다).
    "goggles":    {"B0": ("M", "INK"), "CB1": ("M2", None), "CB2": ("M2", None), "S3": (None, "INK"), "S4": (None, "M"),
                   "S5": (None, "M"), "H6": (None, "W")},
    #   외알안경 구슬 = M2 채움·윤곽 없음(r 1pt 원반은 윤곽이 전부를 먹는다) — 사슬 Gold 와 ΔE 73.1 로 갈린다(휘도 1.19).
    "monocle":    {"EYE": ("EYE", None), "CB0": (("CARDWASH", 0.2), "M"), "H1": (None, "W"), "S2": (None, "M"), "CB3": ("M2", None)},
    "bowtie":     {"B0": ("M", "INK"), "B1": ("M", "INK"), "RB2": ("M2", "INK"), "H3": (None, "W")},
    "stripedtie": {"B0": ("M", "INK"), "B1": ("M", "INK"), "F2": ("M2", None), "F3": ("M2", None), "H4": (None, "W")},
    "scarf":      {"B0": ("M", "INK"), "B1": ("M", "INK"), "S2": (None, "M2"), "S3": (None, "M2"), "S4": (None, "M2"), "H5": (None, "W")},
    "bellnecklace": {"S0": (None, "M"), "CF1": ("M2", None), "CF2": ("M2", None), "CF3": ("M2", None), "B4": ("M2", "INK"),
                     "RB5": ("M2", "INK"), "CB6": ("M2", "INK"), "H7": (None, "W")},
    # 망토: 몸 = 무대 조각(STB/STC/STK) · 카드 = 아이콘 조각
    "shortcape":  {"STB": ("M", "INK"), "STC": ("M2", "INK"), "STK": ("M", "INK"),
                   "B0": ("M", "INK"), "B1": ("M2", "INK"), "S2": (None, "INK"), "H3": (None, "W")},
    "longcape":   {"STB": ("M", "INK"), "STC": ("M2", "INK"), "STK": ("M", "INK"),
                   "B0": ("M", "INK"), "B1": ("M2", "INK"), "CB2": ("M", "INK"), "S3": (None, "INK"), "S4": (None, "INK"), "H5": (None, "W")},
    "wings":      {"B0": ("M", "INK"), "B1": ("M", "INK"), "S2": (None, "INK"), "S3": (None, "INK"), "RB4": ("M2", "INK"), "H5": (None, "W")},
    #   배낭 어깨끈(R19 신설, 앞층 독립선 1.8pt) = M2 선: 뒷판 Canvas 위 휘도 1.06(회색조 소실)이지만 잉크 끈은 몸통 옆에서 팔다리로 읽힌다 — 검은 잉크와 4.72 · 흰 잉크와 4.00 으로 몸과 갈리는 쪽을 택했다.
    "backpack":   {"S0": (None, "M"), "B1": ("M", "INK"), "B2": ("M2", "INK"), "RB3": ("M2", "INK"), "RB4": ("M", "INK"), "H5": (None, "W"),
                   "STRAP_L": (None, "M2"), "STRAP_R": (None, "M2")},
}
GROUP_ALPHA_BODY = 1.0     # ★ 날개·배낭 인계본 그룹 α0.40 → 1.0 (장비 전부 불투명 — 사용자 확정)

def role_of(kind, src):
    """base 조각(src 'B0b')은 부모 조각의 채움 역할을 그대로 받는다(불투명 바탕 = 재질색, 잉크 아님)."""
    if src.endswith("b") and src[:-1] in ROLE[kind]:
        return (ROLE[kind][src[:-1]][0], None)
    return ROLE[kind][src]

# ============================================================================
# 4. 색 해석 — 표면별
# ============================================================================
def hx(c): return CL.rgb2hex(c)
def rgb(h): return CL.hex2rgb(h)
def shaded(h): return hx(tuple(int(round(v * SHADE)) for v in rgb(h)))
def over(top, alpha, under): return hx(tuple(int(round(under[i] + (top[i] - under[i]) * alpha)) for i in range(3)))

def resolve(kind, src, surface, ink):
    """→ (fill_hex|None, fill_alpha, line_hex|None, note). surface: 'card' | 'body'. ink: 그 표면의 잉크 hex."""
    it = BY_KIND[kind]; m, m2 = it[4], it[5]
    f, l = role_of(kind, src)
    fill, fa = None, 1.0
    if f == "M": fill = m
    elif f == "M2": fill = m2
    elif f == "SH": fill = shaded(m)
    elif f == "EYE": fill = CHARCOAL if CL.L(rgb(ink)) >= 0.22 else INK_WHITE   # AccessoryHandoffPalette.ContrastTo
    elif isinstance(f, tuple) and f[0] == "CARDWASH":
        if surface == "card": fill, fa = m2, f[1]
        else: fill = None
    line = None
    if l == "INK": line = CARD_INK if surface == "card" else ink
    elif l == "M": line = m
    elif l == "M2": line = m2
    elif l == "W": line = INK_WHITE
    return fill, fa, line

# ============================================================================
# 5. 검산
# ============================================================================
def cr(a, b): return CL.CR(rgb(a), rgb(b))
def L(h): return CL.L(rgb(h))
def dE(a, b): return CL.dE(rgb(a), rgb(b))
def hue(h): return CL.hue_deg(rgb(h))
def sat(h): return CL.rgb_to_hsv(rgb(h))[1]

def band_from(bgs, floor=MIN_NONTEXT):
    """배경 목록에서 대비 ≥ floor 를 전부 만족하는 L 구간 [lo, hi].
    배경마다 「더 밝게」(L ≥ floor·(lb+0.05)−0.05) 또는 「더 어둡게」(L ≤ (lb+0.05)/floor−0.05) 둘 중 성립 가능한 쪽만 건다 —
    어두운 배경은 「더 어둡게」가 음수라 불가능하고, 밝은 배경은 「더 밝게」가 1을 넘어 불가능하다.
    ★ 자기 교정: 처음엔 두 부등식을 모든 배경에 걸어 [3.1, −0.03] 이 나왔다(거짓 「대역 밖」 24건). 그 출력은 폐기했다."""
    lo, hi = 0.0, 1.0
    for h in bgs:
        lb = L(h)
        need_lighter = floor * (lb + 0.05) - 0.05
        need_darker = (lb + 0.05) / floor - 0.05
        if need_darker < 0.0 and need_lighter <= 1.0: lo = max(lo, need_lighter)        # 어두운 배경
        elif need_lighter > 1.0 and need_darker >= 0.0: hi = min(hi, need_darker)       # 밝은 배경
        else: raise SystemExit("중간 밝기 배경 %s(L %.4f) — 대역이 두 구간으로 갈라진다. 이 함수로 못 잰다" % (h, lb))
    return lo, hi

def used_colors():
    """42종이 실제로 쓰는 재질색 집합(잉크 표식 제외) + 그늘색(망토 칼라는 안 쓰지만 야구모자 띠가 쓴다)."""
    s = set()
    for it in ITEMS:
        for h in (it[4], it[5]):
            if h.upper() not in INK_MARKS: s.add(h.upper())
    return sorted(s)

def report(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    CL.calibrate(verbose=False)
    w("=== 0. 자 교정 === colorlab.calibrate() 통과 (흰/검 21.0 · 동일색 1.0 · #767676/흰 4.5422 · #000/#808080 5.3172 · WornColor 2건 · LAB 3점 · dE 2점)")
    w("   추가 교정: CR(#F2F2F2,#111111) = %.4f  (독립 계산 16.81 근사)  ·  L(#C8A15A) = %.4f (PALETTE_SPEC §0: 0.3851)" % (cr("#F2F2F2", "#111111"), L(BRASS)))
    assert abs(L(BRASS) - 0.3851) < 0.0006, "브라스 휘도가 PALETTE_SPEC 값과 어긋난다 — 계산기 폐기"
    w()
    # --- 5-1 대역 -------------------------------------------------------------
    lo_t, hi_t = band_from(BG.values()); lo_s, hi_s = band_from(list(BG.values()) + list(BG_SPEC.values()))
    w("=== 1. 자립 대역(비텍스트 3.0) ===")
    for k, h in list(BG.items()) + list(BG_SPEC.items()):
        w("   %-22s L %.4f" % (k, L(h)))
    w("   과제 배경 6종 교집합:  L ∈ [%.4f, %.4f]   (아래 = 카드 썸네일 바탕 #15181E 가 막고, 위 = #F2F2F2 가 막는다)" % (lo_t, hi_t))
    w("   + PALETTE_SPEC 두 무대: L ∈ [%.4f, %.4f]   (= §0 의 [0.1632, 0.2396] — 더 좁다. 이 대역 안이면 과제 6종은 자동 통과)" % (lo_s, hi_s))
    lo45, hi45 = band_from(["#F2F2F2", "#1E1E1E"], MIN_TEXT)
    w("   ★ 1pt 선 4.5 를 밝은/어두운 바탕화면 둘 다에서: L ≤ %.4f 그리고 L ≥ %.4f → %s" % (hi45, lo45, "공집합 — 어떤 고정색도 불가" if lo45 > hi45 else "가능"))
    w()
    # --- 5-2 재질색 전량 -------------------------------------------------------
    w("=== 2. 재질색 전량 — 배경 6종 대비 · 대역 · WornColor 항등 ===")
    w("   %-12s %-8s  L      %s  대역  항등" % ("이름", "hex", "  ".join("%6s" % k.split()[-1][:7] for k in BG)))
    worst_all = 99
    for h in used_colors():
        crs = [cr(h, b) for b in BG.values()]; worst_all = min(worst_all, min(crs))
        inb = lo_s - 1e-9 <= L(h) <= hi_s + 1e-9
        idn = CL.is_worn_fixed(rgb(h))
        w("   %-12s %-8s %.4f  %s  %s  %s" % (name_of(h), h, L(h), "  ".join("%6.2f" % c for c in crs), "안" if inb else "★밖", "O" if idn else "★X"))
    w("   → 재질색 %d색 최악 대비 %.2f : 1 (하한 3.0)" % (len(used_colors()), worst_all))
    sh = {shaded(BY_KIND["clothhat"][4]): "야구모자 띠(그늘 = Ivory×0.28)"}
    for h, what in sh.items():
        w("   그늘 %s %s: L %.4f · 밝은 바탕 %.2f · 자기 채움(Ivory) 위 %.2f — 채움 위에만 놓이므로 배경 대비는 재지 않는다" % (h, what, L(h), cr(h, "#F2F2F2"), cr(h, BY_KIND["clothhat"][4])))
    w()
    # --- 5-3 선 색 4안 ---------------------------------------------------------
    w("=== 3. 선 색 후보 비교 (독립 선이 놓이는 곳 = 배경 4종 + 잉크 2종) ===")
    bgs4 = ["#F2F2F2", "#1E1E1E", "#111111", "#FFFFFF"]
    cands = [("브라스 #C8A15A (R17 현행)", BRASS), ("재질 어두운 톤 (Ivory×0.28)", shaded("#96814F")), ("검은 잉크 #111", INK_BLACK),
             ("흰 잉크 #FFF", INK_WHITE), ("대역 재질선 Silver #587398", "#587398"), ("대역 재질선 Gold #9B7922", "#9B7922"), ("카드 잉크 #E8E2D6", CARD_INK)]
    w("   %-30s %s" % ("후보", "  ".join("%8s" % b for b in bgs4)))
    for nm, h in cands:
        w("   %-30s %s" % (nm, "  ".join("%8.2f" % cr(h, b) for b in bgs4)))
    w("   판정: 브라스는 밝은 바탕 %.2f · 흰 잉크 %.2f 로 미달. 어두운 톤은 어두운 바탕 %.2f · 검은 잉크 %.2f 로 미달. 잉크는 반대편 바탕에서 죽지만 그건 캐릭터 본체와 같은 계약(유저가 잉크를 고른다)."
      % (cr(BRASS, "#F2F2F2"), cr(BRASS, "#FFFFFF"), cr(shaded("#96814F"), "#1E1E1E"), cr(shaded("#96814F"), "#111111")))
    w("         대역 재질선만 넷 다 3.0 이상(최악 %.2f) — 그러나 4.5 는 §1 대로 어떤 고정색도 못 낸다." % min(cr("#587398", b) for b in bgs4))
    w()
    # --- 5-4 카드 표면: 윤곽↔채움 · 채움↔바탕 ----------------------------------
    w("=== 4. 카드(#15181E) — 채움↔바탕 · 카드 잉크 윤곽↔채움 ===")
    for h in used_colors():
        w("   %-12s %-8s 채움↔바탕 %5.2f  잉크 #E8E2D6↔채움 %5.2f  (현행 워시 α0.21 채움 %s ↔ 바탕 %.2f)" % (
            name_of(h), h, cr(h, "#15181E"), cr(CARD_INK, h), over(rgb(h), GRAD_MEAN, rgb("#15181E")), cr(over(rgb(h), GRAD_MEAN, rgb("#15181E")), "#15181E")))
    w("   → 채움↔바탕 최악 %.2f(하한 3.0 통과). 윤곽↔채움은 %.2f~%.2f — 윤곽은 형태의 주 채널이 아니다(형태는 채움↔바탕이 진다). 현행 워시는 채움↔바탕 최악 %.2f 로 채움이 안 보이던 상태." % (
        min(cr(h, "#15181E") for h in used_colors()), min(cr(CARD_INK, h) for h in used_colors()), max(cr(CARD_INK, h) for h in used_colors()),
        min(cr(over(rgb(h), GRAD_MEAN, rgb("#15181E")), "#15181E") for h in used_colors())))
    w()
    # --- 5-5 몸 표면: 잉크 윤곽↔채움 ------------------------------------------
    w("=== 5. 몸 — 잉크 윤곽↔채움 (검은 잉크 / 흰 잉크) ===")
    for h in used_colors():
        w("   %-12s %-8s 검은 잉크 %5.2f  흰 잉크 %5.2f" % (name_of(h), h, cr(INK_BLACK, h), cr(INK_WHITE, h)))
    w("   → 최악 검은 잉크 %.2f · 흰 잉크 %.2f (대역이 보장: 하한 0.1632 ↔ #111 = %.2f, 상한 0.2396 ↔ 흰 = %.2f)" % (
        min(cr(INK_BLACK, h) for h in used_colors()), min(cr(INK_WHITE, h) for h in used_colors()),
        (0.1632 + 0.05) / (L("#111111") + 0.05), 1.05 / (0.2396 + 0.05)))
    w()
    # --- 5-6 등급색 -----------------------------------------------------------
    w("=== 6. 등급색 4단을 조각에 칠하면 (R17 C-1 「강조 A = 등급색」의 실측) ===")
    w("   %-5s %-8s L      밝은바탕  흰잉크  어두운바탕  검은잉크  카드바탕 | 재질 채움 위(최악/최선)         WornColor" % ("등급", "hex"))
    for nm, h in RARITY.items():
        onfill = [(cr(h, m), name_of(m)) for m in used_colors()]
        wf = min(onfill); bf = max(onfill)
        wc = hx(CL.worn(rgb(h)))
        w("   %-5s %-8s %.4f  %6.2f   %6.2f  %6.2f     %6.2f    %6.2f  | %.2f(%s) / %.2f(%s)   → %s%s" % (
            nm, h, L(h), cr(h, "#F2F2F2"), cr(h, "#FFFFFF"), cr(h, "#1E1E1E"), cr(h, "#111111"), cr(h, "#15181E"),
            wf[0], wf[1], bf[0], bf[1], wc, "" if wc.upper() == h.upper() else " ★비항등"))
    w("   → 몸: 4단 전부 밝은 바탕·흰 잉크 미달. 재질 채움 위에서는 일반 %.2f~%.2f 로 사라지고 전설만 3.0 근처 — 「일반 아이템의 보석이 안 보인다」." % (
        min(cr(RARITY["일반"], m) for m in used_colors()), max(cr(RARITY["일반"], m) for m in used_colors())))
    w("   → 카드: 바탕 위에서는 4단 다 살지만 재질 채움 위(보석·매듭·띠)는 같은 값이라 같은 결론. 등급색이 남는 자리 = 리본 칸·낱말·썸네일 프레임(안 C, 이미 Cards.cs 에 있다).")
    w()
    w("=== 6-1. 등급 램프 ↔ 재질색 색상환 30° 안 (같은 가족) ===")
    rh = sum(hue(h) for h in RARITY.values()) / 4
    w("   등급 램프 색상각 %.1f°(4색 평균, 개별 %s) · S %s" % (rh, "/".join("%.0f" % hue(h) for h in RARITY.values()), "/".join("%.2f" % sat(h) for h in RARITY.values())))
    w("   %-12s %-8s 색상각  L      ΔE(일반/희귀/영웅/전설)   최근접 등급과 대비" % ("재질", "hex"))
    for h in used_colors():
        d = abs((hue(h) - rh + 180) % 360 - 180)
        if d <= 30:
            des = [dE(h, r) for r in RARITY.values()]
            near = min(RARITY.values(), key=lambda r: dE(h, r))
            w("   %-12s %-8s %5.1f°  %.4f  %s   %.2f (%s)" % (name_of(h), h, hue(h), L(h), "/".join("%5.1f" % x for x in des), cr(h, near), [k for k, v in RARITY.items() if v == near][0]))
    w("   → 같은 가족이지만 서로 다른 대역(등급 L ≥ %.2f 크롬 · 재질 L ≤ %.4f 아트)에 산다. 등급색은 조각에 안 칠하므로 한 표면에서 맞닿는 자리는 「리본 ↔ 그 아래 아이콘」뿐이고 둘 사이엔 카드 바탕이 있다." % (L(RARITY["일반"]), max(L(h) for h in used_colors())))
    w("   예약색 대조: 일반 #9C978C ↔ TextTertiary #8B939F ΔE %.2f (리더 확정 램프 — 인계본 #8A8F98 의 2.40 과 달리 변별 하한 7.8 통과) · 재질색 ↔ CardBorderWorn #5DA1F5 최소 ΔE %.2f" % (
        dE(RARITY["일반"], "#8B939F"), min(dE(h, "#5DA1F5") for h in used_colors())))
    w()
    # --- 5-7 팩 6 -------------------------------------------------------------
    w("=== 7. DLC 6팩 ↔ 기본 재질 세계 ===")
    mats = used_colors()
    Ls = [L(h) for h in mats]; Ss = [sat(h) for h in mats]
    w("   기본 재질 %d색: L %.4f~%.4f (최대/최소 대비 %.2f) · S %.2f~%.2f · 색상각 군집: 따뜻 %d · 초록 %d · 파랑/청록 %d · 보라 %d" % (
        len(mats), min(Ls), max(Ls), (max(Ls) + 0.05) / (min(Ls) + 0.05), min(Ss), max(Ss),
        sum(1 for h in mats if hue(h) < 60 or hue(h) >= 330), sum(1 for h in mats if 60 <= hue(h) < 160),
        sum(1 for h in mats if 160 <= hue(h) < 250), sum(1 for h in mats if 250 <= hue(h) < 330)))
    w("   %-12s 각도  주색     L      S     보조색    L      S     기본재질 최근접 ΔE(주/보)  같은 대역" % "팩")
    allL = list(Ls)
    for nm, hdeg, p, s in PACKS:
        dp = min((dE(p, m), name_of(m)) for m in mats); ds = min((dE(s, m), name_of(m)) for m in mats)
        allL += [L(p), L(s)]
        w("   %-12s %3d°  %s %.4f %.2f  %s %.4f %.2f   %5.2f(%s) / %5.2f(%s)   %s" % (
            nm, hdeg, p, L(p), sat(p), s, L(s), sat(s), dp[0], dp[1], ds[0], ds[1],
            "O" if lo_s <= L(p) <= hi_s and lo_s <= L(s) <= hi_s else "★"))
    w("   → 재질 %d색 + 팩 12색 = %d색이 한 휘도 대역 L %.4f~%.4f (최대/최소 대비 %.2f : 1). 팩은 색상각으로만 갈리고 밝기·채도 범위는 기본과 같다 = 「한 세계」." % (
        len(mats), len(mats) + 12, min(allL), max(allL), (max(allL) + 0.05) / (min(allL) + 0.05)))
    pack_cols = [c for _, _, p, s in PACKS for c in (p, s)]
    w("   팩↔재질 최소 ΔE %.2f (PackPaletteGateTests 하한 8.0 · PALETTE_SPEC §13-3 실측 8.06) — 이 라운드는 재질 hex 를 하나도 안 만들었으므로 그 게이트 값이 그대로다." % min(dE(c, m) for c in pack_cols for m in mats))
    w()
    # --- 5-8 흰 잉크 -----------------------------------------------------------
    w("=== 8. 흰 잉크(Ctrl+Opt+Cmd+C) 재검산 — 재질색은 WornColor 틴트를 안 받는다는 전제 ===")
    w("   재질 채움 ↔ 흰 잉크 머리: 최악 %.2f (Ivory/GoldLight 계열, L 상한 쪽) · 재질 독립선 ↔ 흰 머리: Silver %.2f · Gold %.2f · Leather %.2f" % (
        min(cr(h, INK_WHITE) for h in mats), cr("#587398", INK_WHITE), cr("#9B7922", INK_WHITE), cr("#BA5928", INK_WHITE)))
    w("   흰 잉크 윤곽 ↔ 재질 채움: 최악 %.2f (§5) · 모자 불투명 바탕 = 재질색이라 흰 원반이 모자 안으로 비치는 일도, 흰 모자가 흰 머리와 붙는 일도 없다." % min(cr(INK_WHITE, h) for h in mats))
    w("   반대쪽 눈(외알안경): 흰 잉크면 목탄 #252829 — 흰 머리 위 %.2f (AccessoryHandoffPalette.ContrastTo 그대로)." % cr(CHARCOAL, INK_WHITE))
    w()
    # --- 5-9 아이템별 요약 ------------------------------------------------------
    w("=== 9. 인계본 16종 — 표면별 최소 대비 요약 (채움↔그 표면 바탕 · 독립선↔바탕) ===")
    for k in ["clothhat", "furhat", "fedora", "crown", "sunglasses", "roundglasses", "goggles", "monocle",
              "bowtie", "stripedtie", "scarf", "bellnecklace", "shortcape", "longcape", "wings", "backpack"]:
        it = BY_KIND[k]
        fills = set(); lines = set()
        for src, (f, l) in ROLE[k].items():
            fh, _, lh = resolve(k, src, "body", INK_BLACK)
            if fh and f != "EYE" and f != "SH": fills.add(fh)
            if l in ("M", "M2"): lines.add(lh)
        bfa = min(cr(h, b) for h in fills for b in ["#F2F2F2", "#1E1E1E", "#111111", "#FFFFFF"]) if fills else None
        bla = min(cr(h, b) for h in lines for b in ["#F2F2F2", "#1E1E1E", "#111111", "#FFFFFF"]) if lines else None
        cfa = min(cr(h, "#15181E") for h in fills) if fills else None
        fmt = lambda v: ("%.2f" % v) if v is not None else "  -  "
        w("   %-13s %-8s M %s(%s) M2 %s(%s)  몸 채움↔배경4 %s · 몸 독립선↔배경4 %s · 카드 채움↔바탕 %s · M↔M2 ΔE %.1f" % (
            it[1], k, it[4], name_of(it[4]), it[5], name_of(it[5]), fmt(bfa), fmt(bla), fmt(cfa), dE(it[4], it[5])))
    w("   (선글라스: 대역 밖 SH 렌즈는 채움 집계에서 뺐다 — 배경이 머리뿐이라 §11 의 자로 따로 잰다: SH↔테 %.2f · SH↔검은 머리 %.2f · SH↔흰 머리 %.2f · SH↔카드 %.2f)" % (
        cr(shaded("#5075B5"), "#587398"), cr(shaded("#5075B5"), "#111111"), cr(shaded("#5075B5"), "#FFFFFF"), cr(shaded("#5075B5"), "#15181E")))
    w()
    w("=== 11. R19 교차 확인 재측정 (design-equipment ROLE_FIX 4건 + 왕관 뒷벽) ===")
    SHL = shaded("#5075B5")
    w("   (1) 선글라스 렌즈 SH %s(L %.4f) · 테 M2 #587398: 렌즈↔테 %.2f · 렌즈↔검은 머리 %.2f · 렌즈↔흰 머리 %.2f · 테↔검은 머리 %.2f · 테↔흰 머리 %.2f · 렌즈↔카드 %.2f · 테↔카드 %.2f | 원안 M↔M2 %.2f" % (
        SHL, L(SHL), cr(SHL, "#587398"), cr(SHL, "#111111"), cr(SHL, "#FFFFFF"), cr("#587398", "#111111"), cr("#587398", "#FFFFFF"), cr(SHL, "#15181E"), cr("#587398", "#15181E"), cr("#5075B5", "#587398")))
    alts = [(cr(h, "#587398"), h) for h in used_colors()]
    w("       (b) 대체색: .asset 25색 중 테와 ≥3.0 = %d개(최대 %s %.2f) · 그늘 배수 0.40 → 테 %.2f · 0.50 → %.2f (둘 다 3.0 미달) ⇒ (a) 배수 0.28 채택" % (
        sum(1 for c, _ in alts if c >= 3.0), max(alts)[1], max(alts)[0],
        cr(hx(tuple(int(round(v * 0.40)) for v in rgb("#5075B5"))), "#587398"), cr(hx(tuple(int(round(v * 0.50)) for v in rgb("#5075B5"))), "#587398")))
    w("   (2) 고글 렌즈 M2 #CC5512 ↔ 판 M #587398: 휘도 %.2f · ΔE %.1f · 렌즈↔검은 머리 %.2f · ↔흰 머리 %.2f · ↔카드 %.2f (설계자 1.62 는 내 자로 재현 안 됨 — 어느 쌍인지 미확인)" % (
        cr("#CC5512", "#587398"), dE("#CC5512", "#587398"), cr("#CC5512", "#111111"), cr("#CC5512", "#FFFFFF"), cr("#CC5512", "#15181E")))
    w("   (3) 외알안경 구슬 M2 #587398 ↔ 사슬/테 Gold: 휘도 %.2f · ΔE %.1f · 구슬↔밝은 바탕 %.2f · ↔어두운 바탕 %.2f · ↔카드 %.2f | 대안 M Gold 구슬 ↔ 사슬 1.00/ΔE 0" % (
        cr("#587398", "#9B7922"), dE("#587398", "#9B7922"), cr("#587398", "#F2F2F2"), cr("#587398", "#1E1E1E"), cr("#587398", "#15181E")))
    CV = "#AB7942"
    w("   (4) 배낭 어깨끈 후보 ↔ 뒷판 Canvas / 밝은 바탕 / 어두운 바탕 / 검은 잉크 / 흰 잉크 / ΔE↔Canvas")
    for nm, h in (("M2 초록 #5A8C3C", "#5A8C3C"), ("그늘 Canvas×0.28", shaded(CV)), ("검은 잉크", "#111111"), ("흰 잉크", "#FFFFFF"), ("Leather #BA5928", "#BA5928")):
        w("       %-18s %5.2f  %5.2f  %5.2f  %5.2f  %5.2f   %.1f" % (nm, cr(h, CV), cr(h, "#F2F2F2"), cr(h, "#1E1E1E"), cr(h, "#111111"), cr(h, "#FFFFFF"), dE(h, CV)))
    GS = shaded("#9B7922")
    w("   (5) 왕관 뒷벽 그늘 %s(L %.4f) vs M Gold: ↔봉우리 Gold %.2f / %.2f · ↔검은 머리 %.2f / %.2f · ↔흰 머리 %.2f / %.2f · ↔밝은 바탕 %.2f / %.2f · ↔어두운 바탕 %.2f / %.2f" % (
        GS, L(GS), cr(GS, "#9B7922"), 1.0, cr(GS, "#111111"), cr("#9B7922", "#111111"), cr(GS, "#FFFFFF"), cr("#9B7922", "#FFFFFF"),
        cr(GS, "#F2F2F2"), cr("#9B7922", "#F2F2F2"), cr(GS, "#1E1E1E"), cr("#9B7922", "#1E1E1E")))
    w()
    w("=== 10. 변경 요약 ===")
    w("   .asset 변경 권고 1건: equip_head_crown tone1 #988540 → #C6443C (보석). 금↔보석 대비 %.2f · ΔE %.1f (현행 GoldLight: %.2f · %.1f)" % (
        cr("#9B7922", "#C6443C"), dE("#9B7922", "#C6443C"), cr("#9B7922", "#988540"), dE("#9B7922", "#988540")))
    w("   새 hex: 0개 · WornColor 비항등: %d개 · 대역 밖: %d개" % (
        sum(1 for h in mats if not CL.is_worn_fixed(rgb(h))), sum(1 for h in mats if not (lo_s - 1e-9 <= L(h) <= hi_s + 1e-9))))
    w("   인계본 채움 알파 폐기(몸): 그라디언트 0.34→0.08 · 강조 α0.14~0.8 · 그룹 α0.40 전부 → 1.0. 카드 예외 3조각: 동그란안경 CB0/CB1 α0.16 · 외알안경 CB0 α0.2 (유리 워시, M2).")

if __name__ == "__main__":
    report()
