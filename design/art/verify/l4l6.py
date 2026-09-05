# -*- coding: utf-8 -*-
"""L-4(스탯 증가값 색) · L-6(무대 잉크) 판정 검산 — design-art, 2026-09-05

★ 교정 먼저. 교정이 하나라도 깨지면 아무 숫자도 내지 않고 죽는다.

교정은 **두 겹**이다:
  (1) colorlab / cvd 자체 교정 (흰/검 21.0 · 동일색 1.0 · LAB · CVD 항등식)
  (2) ★ **바깥 대조 10건** — 내가 이번 라운드에 만들지 않은 문서·소스가 이미 발표한 값을
      내 자로 재현한다. 재현이 깨지면 내 자가 그 문서들과 다른 것을 재고 있는 것이다.
      출처: Assets/.../UiChrome.cs · docs/UX_EQUIPMENT_WINDOW_3COL_PORT.md · PALETTE_SPEC §30-4

실행:  python3 l4l6.py            (판정)
       python3 l4l6.py --control  (양성 대조 — 판정기가 알려진 나쁜 값을 실제로 잡는가)
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
import cvd as CVD

H = CL.hex2rgb
HEX = CL.rgb2hex

# ============================================================================
# 0. 값 — 전부 프로덕션 소스/문서에서 옮겨 왔고, 옮긴 자리를 적는다
# ============================================================================

# --- UiChrome.cs 토큰 (float 3튜플 -> 8bit, Unity Color32 반올림과 같은 식) ---
def f3(r, g, b):
    return tuple(int(round(v * 255)) for v in (r, g, b))


TOK = {
    "PanelSurface":        f3(0.078, 0.090, 0.110),   # :123
    "CardSurface":         f3(0.106, 0.122, 0.149),   # :136
    "CardSurfaceMuted":    f3(0.082, 0.094, 0.118),   # :142
    "ThumbSurfaceLocked":  f3(0.063, 0.075, 0.094),   # :145
    "PortraitSurface":     f3(0.914, 0.918, 0.902),   # :152  ★ 종이 무대
    "SubtleSurface":       f3(0.098, 0.114, 0.141),   # :155
    "CardBorderWorn":      f3(0.365, 0.631, 0.961),   # :183  ★ 예약(착용 중)
    "Accent":              f3(0.784, 0.631, 0.353),   # :217  브라스
    "TextPrimary":         f3(0.949, 0.957, 0.969),   # :252
    "TextSecondary":       f3(0.682, 0.706, 0.749),   # :255
    "TextTertiary":        f3(0.545, 0.576, 0.624),   # :258  ★ 예약(잠긴 실루엣)
    "NonTextMuted":        f3(0.424, 0.455, 0.502),   # :266
    "DisabledControlInk":  f3(0.294, 0.322, 0.361),   # :274
    "TextOnAccent(출하)":  f3(0.561, 0.765, 1.000),   # :370  ★ 코드 현재값
    "OnAccentSolid":       f3(0.043, 0.063, 0.086),   # :376
}
CATEGORY_TINT = {
    "HEAD 주황":       f3(0.910, 0.514, 0.290),       # :395
    "EYES+PET 청록":   f3(0.310, 0.753, 0.776),       # :396
    "NECK+HAIR 초록":  f3(0.549, 0.753, 0.431),       # :397  ★★ L-4의 핵심
    "BACK+FX 라벤더":  f3(0.690, 0.561, 0.816),       # :398
}
RARITY = {"일반": H(0x9C978C), "희귀": H(0xBCAC8B),
          "영웅": H(0xDEC081), "전설": H(0xFFD375)}          # :469-472
RARITY_TRACK = H(0x3A4049)                                     # :491
MIN_TEXT = 4.5      # UiChrome.MinTextContrast   :1329
MIN_NONTEXT = 3.0   # UiChrome.MinNonTextContrast :1335

# --- CharacterPortraitStage.ResolveBackdropColor :369 (토큰 없는 리터럴) ---
CHARCOAL = f3(0.145, 0.157, 0.180)

# --- PALETTE_SPEC 정본 (§13-3 처방 C) ---
PACK_MAIN = {"오피스 워커": H("#456ECC"), "사이버 아포칼립스": H("#009682"),
             "네온 낙서": H("#CC1BA9"), "스포츠": H("#CC3F29"),
             "컬러 잉크": H("#9768CC"), "밀리터리": H("#639400")}
PACK_SUB = {"오피스 워커": H("#6080CC"), "사이버 아포칼립스": H("#518C84"),
            "네온 낙서": H("#9C5A8E"), "스포츠": H("#9E655C"),
            "컬러 잉크": H("#8563AB"), "밀리터리": H("#798C51")}

# --- 인계본 값 (docs/handoff/.../README.md · render/data.js) ---
GREEN = H("#8FBF6A")          # 증가값 · 세트 완성 · 「보유」 배지 · sport 스와치 (4역할)
HANDOFF_DIM = H("#5C574E")    # 인계본 "없으면 —" 색 (§4-4에서 이미 폐기됨)
HANDOFF_CHIP = H("#0E0C0B")   # 인계본 스와치 칩 면
INK_BLACK, INK_WHITE = (0, 0, 0), (255, 255, 255)   # StickConfig :1539/:1542

# --- 대역 (PALETTE_SPEC §0) ---
ART_LO, ART_HI, CHROME_LO = 0.1632, 0.2396, 0.3000
DISCERN, IDENTIFY = 7.8, 48.6   # 변별 / 식별 하한 (§3-3)


def band(rgb):
    l = CL.L(rgb)
    if l > CHROME_LO:
        return "크롬"
    if ART_LO <= l <= ART_HI:
        return "아트"
    return "해자/밖"


def flatten(over, alpha, onto):
    """UiChrome.Flatten 포트 — over를 alpha로 onto 위에 합성(결과 α=1)."""
    return tuple(int(round(over[i] / 255.0 * alpha * 255 + onto[i] * (1.0 - alpha)))
                 for i in range(3))


# --- UiChrome.Ink / SelectInkOnSurface 포트 (:309, :1622) ---
def ladder(role, enabled):
    if role == "Title":
        return TOK["TextPrimary"] if enabled else TOK["TextSecondary"]
    if role == "Body":
        return TOK["TextSecondary"] if enabled else TOK["TextTertiary"]
    return TOK["TextTertiary"]


def ink_on_surface(backdrop, role, enabled=True):
    """UiChrome.SelectInkOnSurface를 값으로 그대로 옮긴 것. (이름, 색, CR, 읽히는가)"""
    lad = ladder(role, enabled)
    if CL.CR(lad, backdrop) >= MIN_TEXT:
        return ("사다리 그대로", lad, CL.CR(lad, backdrop), True)
    if CL.CR(TOK["OnAccentSolid"], backdrop) >= MIN_TEXT:
        return ("OnAccentSolid", TOK["OnAccentSolid"], CL.CR(TOK["OnAccentSolid"], backdrop), True)
    if CL.CR(TOK["TextSecondary"], backdrop) >= MIN_TEXT:
        return ("TextSecondary", TOK["TextSecondary"], CL.CR(TOK["TextSecondary"], backdrop), True)
    if CL.CR(TOK["TextPrimary"], backdrop) >= MIN_TEXT:
        return ("TextPrimary", TOK["TextPrimary"], CL.CR(TOK["TextPrimary"], backdrop), True)
    best = (TOK["TextPrimary"] if CL.CR(TOK["TextPrimary"], backdrop) >= CL.CR(TOK["OnAccentSolid"], backdrop)
            else TOK["OnAccentSolid"])
    return ("★읽히지 않음", best, CL.CR(best, backdrop), False)


# ============================================================================
# 1. 교정 — 두 겹
# ============================================================================
EXTERNAL = [
    # (이름, 계산, 발표값, 허용오차, 출처)
    ("CR 검은 선 / 종이 무대",   lambda: CL.CR(H("#111111"), TOK["PortraitSurface"]), 15.62, 0.02,
     "UX_EQUIPMENT_WINDOW_3COL_PORT §6-4"),
    ("CR 흰 선 / 목탄 무대",     lambda: CL.CR(INK_WHITE, CHARCOAL), 14.77, 0.02,
     "UX_EQUIPMENT_WINDOW_3COL_PORT §6-4"),
    ("ΔE 브라스 ↔ 등급 영웅",    lambda: CL.dE(TOK["Accent"], RARITY["영웅"]), 12.86, 0.02,
     "UiChrome.cs:456"),
    ("CR 등급 일반 / CardSurface", lambda: CL.CR(RARITY["일반"], TOK["CardSurface"]), 5.68, 0.02,
     "UiChrome.cs:469"),
    ("CR 브라스 / CardSurfaceMuted", lambda: CL.CR(TOK["Accent"], TOK["CardSurfaceMuted"]), 7.37, 0.02,
     "UX §4-4 활성 탭 면"),
    ("CR TextTertiary / CardSurface", lambda: CL.CR(TOK["TextTertiary"], TOK["CardSurface"]), 5.33, 0.02,
     "UX §4-4 카드 메타"),
    ("ΔE 인계본 망토빨강 ↔ 우리 스포츠 주색",
     lambda: CL.dE(H("#D2402F"), PACK_MAIN["스포츠"]), 2.65, 0.02, "PALETTE_SPEC §30-4-2"),
    ("ΔE 인계본 초록 ↔ 우리 스포츠 주색",
     lambda: CL.dE(GREEN, PACK_MAIN["스포츠"]), 89.02, 0.02, "PALETTE_SPEC §30-4"),
    ("L(인계본 초록)",           lambda: CL.L(GREEN), 0.4414, 0.0002, "PALETTE_SPEC §30-4"),
    ("CR 인계본 초록 / 인계본 칩면", lambda: CL.CR(GREEN, HANDOFF_CHIP), 9.13, 0.02,
     "PALETTE_SPEC §30-4"),
]


def calibrate():
    CL.calibrate(verbose=True)
    CVD.calibrate(verbose=False)
    print("=== 바깥 대조 10건 — 내가 안 만든 문서/소스의 발표값을 내 자로 재현 ===")
    ok = True
    for name, fn, want, tol, src in EXTERNAL:
        got = fn()
        good = abs(got - want) <= tol
        ok = ok and good
        print(f"  {'PASS' if good else 'FAIL'}  {name:38s} {got:9.4f}  (발표 {want}, ±{tol})  [{src}]")
    print(f"  대조 판정: {'유효' if ok else '무효'}\n")
    if not ok:
        sys.exit("바깥 대조 실패 — 내 자가 다른 것을 재고 있다. 이 스크립트의 숫자를 전부 폐기하십시오.")


# ============================================================================
# 2. L-4 — 스탯 증가값 색
# ============================================================================
CARD_FACES = ["CardSurface", "CardSurfaceMuted", "PanelSurface", "SubtleSurface"]


def l4():
    print("=" * 78)
    print("L-4  스탯 카드 「장비 보너스 +N」 색")
    print("=" * 78)

    print("\n[1] ★ 전제 반증 — 「우리 팔레트에 초록이 없다」는 거짓이다")
    g = CATEGORY_TINT["NECK+HAIR 초록"]
    d = CL.dE(GREEN, g)
    print(f"    UiChrome._categoryTints[2] = {HEX(g)} (NECK + HAIR)  ← 출하 중인 초록")
    print(f"    인계본 증가값 초록 {HEX(GREEN)} ↔ {HEX(g)} : ΔE = {d:.2f}   (변별 하한 {DISCERN})")
    print(f"    HSV 호 {abs(CL.hue_deg(GREEN) - CL.hue_deg(g)):.2f}°  ·  판정: "
          f"{'같은 색으로 보인다 — 자리를 뺏는다' if d < DISCERN else '다른 색'}")
    print("    → 초록은 「없는 색」이 아니라 **임자 있는 색**이다(넥타이·머리카락 카테고리).")

    print("\n[2] 후보 3안 — 텍스트 대비 (하한 4.5)")
    cands = [("(가) 브라스 Accent", TOK["Accent"]),
             ("(나) 인계본 초록", GREEN),
             ("(나') 출하 카테고리 초록", g),
             ("(다) 등급 영웅", RARITY["영웅"]),
             ("(다') 등급 전설", RARITY["전설"]),
             ("[참고] TextPrimary(총합)", TOK["TextPrimary"])]
    print(f"    {'후보':28s}" + "".join(f"{n[:16]:>18s}" for n in CARD_FACES))
    for nm, c in cands:
        row = "".join(f"{CL.CR(c, TOK[f]):18.2f}" for f in CARD_FACES)
        worst = min(CL.CR(c, TOK[f]) for f in CARD_FACES)
        print(f"    {nm:28s}{row}   최악 {worst:5.2f} {'PASS' if worst >= MIN_TEXT else 'FAIL'}")

    print("\n[3] 「같은 카드 안에서 총합과 구분되는가」 — 보너스 ↔ 총합(TextPrimary)")
    for nm, c in cands[:-1]:
        print(f"    {nm:28s} ΔE(총합) = {CL.dE(c, TOK['TextPrimary']):6.2f}"
              f"   CR비 {CL.CR(c, TOK['CardSurface']) / CL.CR(TOK['TextPrimary'], TOK['CardSurface']):.2f}배")

    print("\n[4] 예약·팩·등급 침범 — ΔE 최소 (하한 변별 7.8)")
    reserved = {"예약 잠김 TextTertiary": TOK["TextTertiary"],
                "예약 착용중 CardBorderWorn": TOK["CardBorderWorn"],
                "브라스 Accent": TOK["Accent"],
                "TextOnAccent(출하)": TOK["TextOnAccent(출하)"],
                "RarityTrack": RARITY_TRACK}
    for nm, c in [("(가) 브라스", TOK["Accent"]), ("(나) 인계본 초록", GREEN)]:
        pool = {}
        pool.update({f"카테고리틴트 {k}": v for k, v in CATEGORY_TINT.items()})
        pool.update({f"등급 {k}": v for k, v in RARITY.items()})
        pool.update({f"팩주 {k}": v for k, v in PACK_MAIN.items()})
        pool.update({f"팩보 {k}": v for k, v in PACK_SUB.items()})
        pool.update(reserved)
        pool = {k: v for k, v in pool.items() if v != c}
        near = sorted(((CL.dE(c, v), k) for k, v in pool.items()))[:4]
        flag = "★ 침범" if near[0][0] < DISCERN else "통과"
        print(f"    {nm:16s} 최근접 " + " | ".join(f"{k} {d:.2f}" for d, k in near) + f"   {flag}")

    print("\n[5] 색각 이상 — 「이 색이 무엇을 뜻하는지」가 살아남는가")
    print("    (증가값 색 ↔ 그 옆에 늘 함께 있는 브라스(주스탯·게이지 채움)의 ΔE)")
    for k in CVD.TYPES:
        a = CL.dE(CVD.sim(GREEN, k), CVD.sim(TOK["Accent"], k))
        b = CL.dE(CVD.sim(GREEN, k), CVD.sim(CATEGORY_TINT["NECK+HAIR 초록"], k))
        print(f"    {CVD.KOR[k]:12s} 초록↔브라스 ΔE {a:6.2f} {'✘ 붕괴' if a < DISCERN else '✔'}"
              f"    초록↔카테고리초록 ΔE {b:6.2f} {'✘ 붕괴' if b < DISCERN else '✔'}")
    print("    (정상 시각) 초록↔브라스 ΔE "
          f"{CL.dE(GREEN, TOK['Accent']):.2f}")

    print("\n[6] 대역 — 몸에 칠할 수 있는가 (§0)")
    for nm, c in [("브라스", TOK["Accent"]), ("인계본 초록", GREEN),
                  ("카테고리 초록", CATEGORY_TINT["NECK+HAIR 초록"])]:
        print(f"    {nm:14s} {HEX(c)}  L={CL.L(c):.4f}  대역 {band(c)}"
              f"  WornColor 항등 {'예' if CL.is_worn_fixed(c) else '아니오'}")

    print("\n[7] 「없음(—)」 자리 — 인계본 #5C574E 대 우리 InkMeta")
    for nm, c in [("인계본 #5C574E", HANDOFF_DIM), ("우리 TextTertiary", TOK["TextTertiary"])]:
        worst = min(CL.CR(c, TOK[f]) for f in CARD_FACES)
        print(f"    {nm:20s} 네 면 최악 {worst:5.2f}  {'PASS' if worst >= MIN_TEXT else 'FAIL'}")


# ============================================================================
# 3. L-6 — 무대 잉크
# ============================================================================
SHEEN_ALPHA_TOP = 0.10          # UiChrome.PanelSheen :127
SHEEN_FADE = 0.45               # SheenFadeRatio :1107
STAGE_H = 238.0                 # UX §4-3-1
GLOW_ALPHA = 0.14               # AccentSurface :220


def sheen_at(y_from_top):
    ramp = 0.0 if y_from_top / STAGE_H >= SHEEN_FADE else 1.0 - (y_from_top / STAGE_H) / SHEEN_FADE
    return SHEEN_ALPHA_TOP * ramp


def l6():
    print("\n" + "=" * 78)
    print("L-6  무대 위 `PREVIEW` 라벨 · 프레즌스 줄의 잉크")
    print("=" * 78)

    print("\n[0] ★ 브리프 정정 — 「밝은 무대 = 흰 잉크」가 아니다. 정반대다")
    print("    CharacterPortraitStage.ResolveBackdropColor :365-371")
    print(f"      검은 잉크(출하 기본) → PortraitSurface {HEX(TOK['PortraitSurface'])} "
          f"L={CL.L(TOK['PortraitSurface']):.4f}  ← ★ 밝은 무대")
    print(f"      흰 잉크            → 목탄 {HEX(CHARCOAL)} L={CL.L(CHARCOAL):.4f}  ← 어두운 무대")

    a_prev = sheen_at(14.0)     # PREVIEW 상단 y=-14 (UX §4-3-1) — 시인이 가장 센 지점
    print(f"\n[1] 실제 바탕 — 시인/광원 합성까지 넣는다 (PREVIEW 상단 시인 α={a_prev:.4f})")
    stages = {}
    for nm, base in [("종이(검은 잉크)", TOK["PortraitSurface"]), ("목탄(흰 잉크)", CHARCOAL)]:
        prev_bg = flatten((255, 255, 255), a_prev, base)
        pres_bg = flatten(TOK["Accent"], GLOW_ALPHA, base)   # 바닥 광원 최대(가장 불리한 층 순서)
        stages[nm] = (base, prev_bg, pres_bg)
        print(f"    {nm:16s} 민바탕 {HEX(base)} / PREVIEW 자리 {HEX(prev_bg)}"
              f" / 프레즌스 자리(광원 최대) {HEX(pres_bg)}")

    print("\n[2] 사다리 4단을 세 바탕 전부에서 잰다 (하한 4.5)")
    inks = ["TextPrimary", "TextSecondary", "TextTertiary", "OnAccentSolid",
            "NonTextMuted", "DisabledControlInk", "Accent"]
    for nm, (base, pb, sb) in stages.items():
        print(f"    --- {nm} ---")
        for ik in inks:
            vs = [CL.CR(TOK[ik], x) for x in (base, pb, sb)]
            worst = min(vs)
            print(f"      {ik:20s} 민바탕 {vs[0]:6.2f} | PREVIEW {vs[1]:6.2f} | 프레즌스 {vs[2]:6.2f}"
                  f"  최악 {worst:6.2f} {'PASS' if worst >= MIN_TEXT else 'FAIL'}")

    print("\n[3] ★ 프로덕션 `UiChrome.InkOnSurface`가 이미 무엇을 고르는가 (값으로 포트)")
    for nm, (base, pb, sb) in stages.items():
        for role, where, bg in (("Meta", "PREVIEW", pb), ("Body", "프레즌스", sb)):
            n2, c, cr, rd = ink_on_surface(bg, role)
            print(f"    {nm:16s} {where:8s} role={role:5s} → {n2:16s} {HEX(c)}"
                  f"  CR {cr:6.2f}  {'PASS' if rd else '★FAIL'}")

    print("\n[4] 두 단(라벨/프레즌스) 위계가 남는가")
    for nm, (base, pb, sb) in stages.items():
        a = ink_on_surface(pb, "Meta")[1]
        b = ink_on_surface(sb, "Body")[1]
        print(f"    {nm:16s} 라벨 {HEX(a)} vs 프레즌스 {HEX(b)}  "
              f"{'★ 한 단으로 접힘(색으로는 구분 없음)' if a == b else 'ΔE %.2f 두 단 유지' % CL.dE(a, b)}")

    print("\n[5] 접힌 쪽(종이)에서 두 단을 되살릴 수 있는가 — 기존 토큰 전수")
    pb = stages["종이(검은 잉크)"][1]
    sb = stages["종이(검은 잉크)"][2]
    for k, v in list(TOK.items()) + list(CATEGORY_TINT.items()) + \
            [("RarityTrack", RARITY_TRACK)] + [(f"등급 {a}", b) for a, b in RARITY.items()]:
        cr1, cr2 = CL.CR(v, pb), CL.CR(v, sb)
        if min(cr1, cr2) >= MIN_TEXT:
            print(f"      {k:22s} {HEX(v)}  PREVIEW {cr1:6.2f} / 프레즌스 {cr2:6.2f}")

    print("\n[6] 레이아웃 제약 — 인형 잉크와 겹치면 무슨 일이 나는가")
    for nm, base, ink in [("종이", TOK["PortraitSurface"], INK_BLACK),
                          ("목탄", CHARCOAL, INK_WHITE)]:
        chosen = ink_on_surface(base, "Body")[1]
        print(f"    {nm} 무대: 인형 잉크 {HEX(ink)} 위에 선택 잉크 {HEX(chosen)} "
              f"= CR {CL.CR(chosen, ink):.2f}  ← 겹치면 {'사라진다' if CL.CR(chosen, ink) < MIN_TEXT else '읽힌다'}")
    print("    UX §4-3-1: 발밑 그림자 y=-198.2 · 프레즌스 상단 y=-211 → 세로 여백 12.8pt")

    print("\n[7] 비텍스트 대조 — 무대 자체가 창 바탕에서 보이는가 (하한 3.0)")
    for nm, base in [("종이", TOK["PortraitSurface"]), ("목탄", CHARCOAL)]:
        cr = CL.CR(base, TOK["PanelSurface"])
        print(f"    {nm} 무대 / PanelSurface = {cr:5.2f}  {'PASS' if cr >= MIN_NONTEXT else 'FAIL'}")

    print("\n[8] ★ 액자 테두리 후보 — UGUI AddOutline은 **사각형 안쪽에** 그린다(UX §4-3-1 검산)")
    print("    ⇒ 테두리의 실제 색 = flatten(테두리, 무대 바탕). 바깥 이웃은 PanelSurface.")
    print("    통과 조건: 두 프리셋 **모두**에서 창 바탕과 ≥ 3.0 (경계를 그리는 것이 목적)")
    borders = {"CardBorder α0.10": ((255, 255, 255), 0.10),
               "PanelBorder α0.16": ((255, 255, 255), 0.16),
               "PanelHighlight α0.30": ((255, 255, 255), 0.30),
               "AccentBorder α0.55": (TOK["Accent"], 0.55),
               "Accent α1.00": (TOK["Accent"], 1.00),
               "TextPrimary α1.00": (TOK["TextPrimary"], 1.00)}
    for nm, (col, a) in borders.items():
        line = []
        worst = 99.0
        for sn, base in (("종이", TOK["PortraitSurface"]), ("목탄", CHARCOAL)):
            eff = flatten(col, a, base)
            c_out = CL.CR(eff, TOK["PanelSurface"])
            c_in = CL.CR(eff, base)
            worst = min(worst, c_out)
            line.append(f"{sn} {HEX(eff)} 밖 {c_out:5.2f} 안 {c_in:5.2f}")
        print(f"    {nm:22s} " + " | ".join(line) +
              f"   {'PASS' if worst >= MIN_NONTEXT else 'FAIL'}")

    print("\n[9] 바닥 광원(AccentSurface 브라스 α0.14)이 두 무대에서 보이는가")
    for nm, base in [("종이", TOK["PortraitSurface"]), ("목탄", CHARCOAL)]:
        lit = flatten(TOK["Accent"], GLOW_ALPHA, base)
        print(f"    {nm} 무대 {HEX(base)} → 광원 중심 {HEX(lit)}  CR {CL.CR(lit, base):5.3f}"
              f"  ΔE {CL.dE(lit, base):5.2f}  {'보인다' if CL.dE(lit, base) >= DISCERN else '거의 안 보인다'}")

    print("\n[9-b] ★ N-2 처방 후보 — 종이 무대에서 광원을 「그늘 웅덩이」로 뒤집는다")
    print("     (목탄 쪽 기준선: 브라스 α0.14 → ΔE 11.33)")
    for al in (0.06, 0.08, 0.10, 0.12, 0.14, 0.16, 0.20):
        lit = flatten(TOK["OnAccentSolid"], al, TOK["PortraitSurface"])
        d = CL.dE(lit, TOK["PortraitSurface"])
        print(f"     OnAccentSolid α{al:.2f} → {HEX(lit)}  ΔE {d:5.2f}  CR {CL.CR(lit, TOK['PortraitSurface']):5.3f}"
              f"  {'통과' if d >= DISCERN else '변별 하한 미달'}")

    print("\n[9-c] 채택 3색의 L / C* (플랫폼 절에서 쓰는 값 — 손으로 적지 않는다)")
    import math
    for nm in ("Accent", "OnAccentSolid", "TextSecondary", "TextPrimary", "TextTertiary"):
        c = TOK[nm]
        _, a_, b_ = CL.lab(c)
        print(f"     {nm:16s} {HEX(c)}  L={CL.L(c):.4f}  C*={math.hypot(a_, b_):5.2f}  대역 {band(c)}")

    print("\n[10] 세트 패널 마커 — 인계본 초록(완성) 자리를 브라스로 받았을 때")
    for nm, c in [("완성 Accent", TOK["Accent"]), ("미완성 NonTextMuted", TOK["NonTextMuted"]),
                  ("트랙 RarityTrack", RARITY_TRACK)]:
        print(f"    {nm:22s} {HEX(c)}  CardSurface CR {CL.CR(c, TOK['CardSurface']):5.2f}")
    print(f"    완성 ↔ 미완성 ΔE {CL.dE(TOK['Accent'], TOK['NonTextMuted']):.2f}"
          f"  (변별 하한 {DISCERN})  CR 서로 {CL.CR(TOK['Accent'], TOK['NonTextMuted']):.2f}")


# ============================================================================
# 4. 양성 대조 — 판정기가 알려진 나쁜 값을 실제로 잡는가
# ============================================================================
def control():
    CL.calibrate(verbose=False)
    print("=== 양성 대조 8건 (전부 「잡아야 정상」) ===")
    rows = []

    def chk(name, caught):
        rows.append(("PASS" if caught else "FAIL", name))

    # 1. 인계본 미달 잉크 #5C574E는 네 면에서 텍스트 하한을 못 넘는다
    chk("인계본 #5C574E 텍스트 미달 탐지",
        min(CL.CR(HANDOFF_DIM, TOK[f]) for f in CARD_FACES) < MIN_TEXT)
    # 2. 브라스를 종이 무대 위 글자로 쓰면 무너진다
    chk("브라스=종이무대 글자 미달 탐지", CL.CR(TOK["Accent"], TOK["PortraitSurface"]) < MIN_TEXT)
    # 3. 사다리 3단이 종이 무대에서 전부 무너진다(= BrightTextBackdrops의 정의)
    chk("종이 무대에서 사다리 3단 전멸 탐지",
        all(CL.CR(TOK[k], TOK["PortraitSurface"]) < MIN_TEXT
            for k in ("TextPrimary", "TextSecondary", "TextTertiary")))
    # 4. 인계본 초록 ↔ 출하 카테고리 초록이 변별 하한 미달임을 탐지
    chk("초록 충돌 탐지", CL.dE(GREEN, CATEGORY_TINT["NECK+HAIR 초록"]) < DISCERN)
    # 5. 음성 대조 — 브라스는 그 초록과 충돌하지 **않는다**(무조건 빨간불이 아님을 보인다)
    chk("음성 대조: 브라스는 초록과 충돌 안 함",
        CL.dE(TOK["Accent"], CATEGORY_TINT["NECK+HAIR 초록"]) >= DISCERN)
    # 6. ink_on_surface 포트가 「읽히지 않음」을 실제로 낼 수 있는가 (죽은 프로브 방지)
    #    ★ 첫 시도 (0.55,0.55,0.55)=#8C8C8C 는 **틀렸다** — OnAccentSolid가 5.85로 통과한다.
    #      부등식을 풀면 읽히지 않는 구간은 L ∈ (0.1578, 0.1998) 뿐이다. #767676(L=0.1845)이 그 안이다.
    _, _, _, readable = ink_on_surface((118, 118, 118), "Meta")
    chk("InkOnSurface 포트가 읽히지 않는 면을 잡는가", not readable)
    #    음성 대조 — 바로 옆 밝기(#8C8C8C)는 읽힌다고 나와야 한다(무조건 FAIL이 아님을 보인다)
    chk("음성 대조: #8C8C8C는 읽힌다", ink_on_surface((140, 140, 140), "Meta")[3])
    # 7. 시인 램프가 45% 아래에서 실제로 0이 되는가 (프레즌스 자리에 시인 없음)
    chk("시인 램프 45% 아래 0 확인", sheen_at(211.0) == 0.0 and sheen_at(14.0) > 0.0)

    for st, nm in rows:
        print(f"  {st}  {nm}")
    print(f"  대조 판정: {'유효' if all(r[0] == 'PASS' for r in rows) else '무효'}")
    if not all(r[0] == "PASS" for r in rows):
        sys.exit("양성 대조 실패 — 판정기가 죽어 있다.")


if __name__ == "__main__":
    if "--control" in sys.argv:
        control()
    else:
        calibrate()
        l4()
        l6()
