# -*- coding: utf-8 -*-
"""R13 — 등급 포인트 글로우(영웅 보라 / 전설 금) · 데이터 바 그라디언트 검산
design-art, 2026-09-06.

사용자 지시(2026-09-06 캐릭터 정보창 고도화):
  "all in white/light grey with subtle point-colored glows (purple for hero, gold for legendary)"
  "Subtly glowing points create depth."
  데이터 바: "much higher contrast and a gradient/highlight effect for the progress portion"

★ 교정 먼저. 교정이 하나라도 깨지면 아무 숫자도 내지 않고 죽는다.
교정은 세 겹이다:
  (1) colorlab / cvd 자체 교정
  (2) ★ 바깥 대조 12건 — 내가 이번 라운드에 만들지 않은 소스·문서가 이미 발표한 값을 내 자로 재현
  (3) ★ 알파 법칙 포트를 UiChrome.cs 주석이 발표한 3개 실측값으로 교정
      (0.96 -> 0.9216 / α0.55 위 0.92 -> 0.717 / α0.86 위 1.0 -> 0.88)

실행:  python3 glow.py            (판정)
       python3 glow.py --control  (양성 대조 — 판정기가 알려진 나쁜 값을 실제로 잡는가)
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
import cvd as CVD

H = CL.hex2rgb
HEX = CL.rgb2hex
CR = CL.CR
dE = CL.dE

CONTROL = "--control" in sys.argv


def f3(r, g, b):
    return tuple(int(round(v * 255)) for v in (r, g, b))


# ============================================================================
# 0. 값 — 전부 프로덕션 소스/정본 문서에서 옮겨 왔고, 옮긴 자리를 적는다
# ============================================================================

TOK = {
    "PanelSurface":       f3(0.078, 0.090, 0.110),   # UiChrome.cs:123  #14171C
    "CardSurface":        f3(0.106, 0.122, 0.149),   # :136  #1B1F26
    "CardSurfaceMuted":   f3(0.082, 0.094, 0.118),   # :142  #15181E
    "ThumbSurfaceLocked": f3(0.063, 0.075, 0.094),   # :155  #101318
    "SubtleSurface":      f3(0.098, 0.114, 0.141),   # :165  #191D24
    "PortraitSurface":    f3(0.914, 0.918, 0.902),   # :162  #E9EAE6  ★ 밝은 무대(출하 기본)
    "InkContrastCharcoal": f3(0.145, 0.157, 0.180),  # :152  #252829  ★ 흰 잉크일 때의 무대
    "CardIconInk":        f3(0.910, 0.886, 0.839),   # :148  #E8E2D6
    "Accent":             f3(0.784, 0.631, 0.353),   # :227  #C8A15A 브라스
    "AccentGlowCore":     f3(0.902, 0.945, 1.000),   # :237  #E6F1FF (호출부 0건)
    "CardBorderWorn":     f3(0.365, 0.631, 0.961),   # :193  #5DA1F5 ★ 예약(착용 중)
    "TextPrimary":        f3(0.949, 0.957, 0.969),   # :262
    "TextSecondary":      f3(0.682, 0.706, 0.749),   # :265
    "TextTertiary":       f3(0.545, 0.576, 0.624),   # :268  ★ 예약(잠긴 실루엣)
    "NonTextMuted":       f3(0.424, 0.455, 0.502),   # :276
    "OnAccentSolid":      f3(0.043, 0.063, 0.086),   # :386
    "DisabledControlInk": f3(0.294, 0.322, 0.361),   # :284  #4B525C
}
CATEGORY_TINT = {
    "HEAD 주황":      f3(0.910, 0.514, 0.290),       # :405  #E8834A
    "EYES+PET 청록":  f3(0.310, 0.753, 0.776),       # :406  #4FC0C6  ★ 펫 카테고리
    "NECK+HAIR 초록": f3(0.549, 0.753, 0.431),       # :407  #8CC06E
    "BACK+FX 라벤더": f3(0.690, 0.561, 0.816),       # :408  #B08FD0  ★★ 보라의 임자 1
}
RARITY = {"일반": H(0x9C978C), "희귀": H(0xBCAC8B),
          "영웅": H(0xDEC081), "전설": H(0xFFD375)}   # :479-482
RARITY_TRACK = H(0x3A4049)                            # :501
RARITY_BORDER_ALPHA = 0.55                            # :552

# PALETTE_SPEC 정본 §13-3 처방 C (DLC 6팩)
PACK_MAIN = {"오피스 워커": H("#456ECC"), "사이버 아포칼립스": H("#009682"),
             "네온 낙서": H("#CC1BA9"), "스포츠": H("#CC3F29"),
             "컬러 잉크": H("#9768CC"), "밀리터리": H("#639400")}   # ★★ 보라의 임자 2
PACK_SUB = {"오피스 워커": H("#6080CC"), "사이버 아포칼립스": H("#518C84"),
            "네온 낙서": H("#9C5A8E"), "스포츠": H("#9E655C"),
            "컬러 잉크": H("#8563AB"), "밀리터리": H("#798C51")}

MIN_TEXT = 4.5      # UiChrome.MinTextContrast    :1339
MIN_NONTEXT = 3.0   # UiChrome.MinNonTextContrast :1345
D_DISCRIM = 7.8     # 변별 하한 (PALETTE_SPEC §12-0)
D_IDENT = 48.6      # 식별 하한 (〃)

DARK_SURFACES = ["PanelSurface", "CardSurface", "CardSurfaceMuted",
                 "ThumbSurfaceLocked", "SubtleSurface"]


def reserved_pool():
    """등급 글로우가 침범하면 안 되는 색 전부 — 25색."""
    pool = {}
    pool.update({f"틴트 {k}": v for k, v in CATEGORY_TINT.items()})
    pool.update({f"등급 {k}": v for k, v in RARITY.items()})
    pool.update({f"팩주 {k}": v for k, v in PACK_MAIN.items()})
    pool.update({f"팩보 {k}": v for k, v in PACK_SUB.items()})
    pool["예약 착용중"] = TOK["CardBorderWorn"]
    pool["예약 잠금실루엣"] = TOK["TextTertiary"]
    pool["예약 브라스"] = TOK["Accent"]
    pool["예약 아이콘잉크"] = TOK["CardIconInk"]
    pool["예약 비텍스트"] = TOK["NonTextMuted"]
    return pool


POOL = reserved_pool()


def nearest(rgb, pool=None, k=4, exclude=()):
    """예약 풀 최근접. exclude 는 '이 색 자신'을 빼기 위한 것 —
    전설 글로우는 RarityColor(Legendary) 그 자체라 자기 자신과의 ΔE 0을
    '침범'으로 세면 판정이 거짓말을 한다."""
    pool = pool or POOL
    rows = sorted(((dE(rgb, v), n) for n, v in pool.items() if n not in exclude))
    return rows[:k]


# ============================================================================
# 1. 알파 법칙 포트 — UiChrome.cs 파일 머리 "알파 채널의 법칙"
#    uGUI 기본 셰이더 Blend SrcAlpha OneMinusSrcAlpha 는 알파 채널에도 같이 적용된다:
#        dstA' = srcA*srcA + dstA*(1 - srcA)
# ============================================================================


def blend_alpha(dst_a, src_a):
    return src_a * src_a + dst_a * (1.0 - src_a)


def desktop_bleed(dst_a, layers):
    """겹을 순서대로 쌓았을 때 최종 창 알파와 바탕화면 비침률(%)."""
    a = dst_a
    for s in layers:
        a = blend_alpha(a, s)
    return a, (1.0 - a) * 100.0


# ============================================================================
# 2. 교정 — 세 겹. 하나라도 깨지면 죽는다
# ============================================================================


def calibrate():
    CL.calibrate(verbose=True)
    CVD.calibrate(verbose=True)

    print("=== 바깥 대조 — 내가 만들지 않은 출처의 발표값을 내 자로 재현 ===")
    # ★ 허용오차 주석: UiChrome.cs 주석의 등급 대비 3건은 **내림 표기**다(7.4077->7.40 /
    #   9.4152->9.41 / 11.6691->11.66). PALETTE_SPEC §31-3은 같은 값을 반올림해 7.41/9.42/11.67로
    #   적었고, 그 문서가 이미 *"두 자가 0.02 안에서 맞는다"*고 밝혀 뒀다(UiChrome.cs:190).
    #   그래서 이 3건만 허용 0.02다 — 내 자가 틀린 게 아니라 **출처 두 벌의 표기가 갈린 것**이고,
    #   그 사실 자체를 여기 남긴다.
    outside = [
        ("CR 등급 일반 / CardSurface", CR(RARITY["일반"], TOK["CardSurface"]), 5.68, 0.005,
         "UiChrome.cs:479"),
        ("CR 등급 희귀 / CardSurface", CR(RARITY["희귀"], TOK["CardSurface"]), 7.40, 0.02,
         "UiChrome.cs:480 (내림표기)"),
        ("CR 등급 영웅 / CardSurface", CR(RARITY["영웅"], TOK["CardSurface"]), 9.41, 0.02,
         "UiChrome.cs:481 (내림표기)"),
        ("CR 등급 전설 / CardSurface", CR(RARITY["전설"], TOK["CardSurface"]), 11.66, 0.02,
         "UiChrome.cs:482 (내림표기)"),
        ("CR 등급 영웅 / CardSurface (반올림판)", CR(RARITY["영웅"], TOK["CardSurface"]), 9.42, 0.005,
         "PALETTE_SPEC §31-3"),
        ("CR 등급 전설 / CardSurface (반올림판)", CR(RARITY["전설"], TOK["CardSurface"]), 11.67, 0.005,
         "PALETTE_SPEC §31-3"),
        ("ΔE 브라스 ↔ 등급 영웅", dE(TOK["Accent"], RARITY["영웅"]), 12.86, 0.005,
         "UiChrome.cs:466"),
        ("CR 등급트랙 / 등급 일반(채움 최악)", CR(RARITY_TRACK, RARITY["일반"]), 3.59, 0.005,
         "UiChrome.cs:491"),
        ("CR 등급트랙 / CardSurface", CR(RARITY_TRACK, TOK["CardSurface"]), 1.58, 0.005,
         "UiChrome.cs:491"),
        ("CR 브라스 / CardSurface", CR(TOK["Accent"], TOK["CardSurface"]), 6.85, 0.005,
         "PALETTE_SPEC §31-3"),
        ("CR 브라스 / CardSurfaceMuted", CR(TOK["Accent"], TOK["CardSurfaceMuted"]), 7.37, 0.005,
         "PALETTE_SPEC §31-3"),
        ("CR OnAccentSolid / 브라스", CR(TOK["OnAccentSolid"], TOK["Accent"]), 7.91, 0.005,
         "UiChrome.cs:220"),
        ("ΔE 틴트초록 ↔ 인계본초록 #8FBF6A", dE(CATEGORY_TINT["NECK+HAIR 초록"], H("#8FBF6A")),
         2.12, 0.005, "PALETTE_SPEC §31-2"),
        ("CR TextTertiary / CardSurface", CR(TOK["TextTertiary"], TOK["CardSurface"]), 5.33, 0.005,
         "PALETTE_SPEC §31-1"),
    ]
    ok = True
    for name, got, want, tol, src in outside:
        p = abs(got - want) <= tol
        ok = ok and p
        print(f"  {'PASS' if p else 'FAIL'}  {name:38s} {got:9.4f}  (발표 {want}, {src})")
    print(f"  바깥 대조: {sum(1 for r in outside if abs(r[1]-r[2])<=r[3])} / {len(outside)}\n")

    print("=== 알파 법칙 포트 교정 — UiChrome.cs 주석이 발표한 실측 3건 ===")
    alpha_checks = [
        ("α0.96 한 겹을 알파 0 위에", blend_alpha(0.0, 0.96), 0.9216, 0.0001, "UiChrome.cs:77"),
        ("α0.55 검은 겹을 알파 0.92 위에", blend_alpha(0.92, 0.55), 0.717, 0.0006, "UiChrome.cs:80"),
        ("α0.86 겹을 알파 1.0 위에", blend_alpha(1.0, 0.86), 0.88, 0.005, "UiChrome.cs:1243 (2자리 표기)"),
        ("α0.98 겹 두 번(주석의 0.9604)", 0.98 * 0.98, 0.9604, 0.0001, "UiChrome.cs:1244"),
    ]
    for name, got, want, tol, src in alpha_checks:
        p = abs(got - want) <= tol
        ok = ok and p
        print(f"  {'PASS' if p else 'FAIL'}  {name:38s} {got:9.4f}  (발표 {want}, {src})")
    print()

    if not ok:
        sys.exit("교정 실패 — 이 스크립트가 내는 모든 숫자를 폐기하십시오.")
    print("교정 3겹 전부 통과. 아래 숫자는 유효하다.\n")


# ============================================================================
# 3. 【G-0】 알파 글로우가 이 아키텍처에서 무엇을 하는가
# ============================================================================


def g0_alpha_law():
    print("=" * 78)
    print("G-0. 알파 글로우 = 창에 뚫는 구멍 — 이것이 이 라운드의 첫 제약이다")
    print("=" * 78)
    print("  불투명 표면(dstA=1) 위에 α짜리 글로우 한 겹을 얹었을 때의 창 알파:")
    print(f"  {'글로우 α':>10} {'창 알파':>10} {'바탕화면 비침':>14}")
    for a in (0.06, 0.09, 0.10, 0.14, 0.20, 0.30, 0.40, 0.50, 0.62, 0.75, 0.90, 1.00):
        fin, bleed = desktop_bleed(1.0, [a])
        print(f"  {a:10.2f} {fin:10.4f} {bleed:13.2f}%")
    worst_a, worst = None, 0.0
    for i in range(0, 101):
        a = i / 100.0
        _, b = desktop_bleed(1.0, [a])
        if b > worst:
            worst, worst_a = b, a
    print(f"\n  최악은 α={worst_a:.2f}에서 비침 {worst:.2f}% (dstA'=1-α+α²의 극소).")
    print("  ⇒ 알파를 '살짝'으로 낮춰도 구멍은 안 닫힌다. α=0.5가 가장 나쁘고 양 끝만 안전하다.")

    print("\n  ★ 지금 출하 중인 초상화 무대 실측(심볼로 지목한다 — 그 파일은 지금 편집 중이라 줄번호가 움직인다):")
    print("    CharacterInfoWindow  \"StageSheen\"     sheen.color = UiChrome.PanelSheen     α0.10 (raw)")
    print("    CharacterInfoWindow  \"StageFloorGlow\" glow.color  = UiChrome.AccentSurface  α0.14 (raw)")
    a1, b1 = desktop_bleed(1.0, [0.10])
    a2, b2 = desktop_bleed(1.0, [0.14])
    both, bboth = desktop_bleed(1.0, [0.10, 0.14])
    print(f"    시인 최상단     창 알파 {a1:.4f}  비침 {b1:.2f}%")
    print(f"    바닥 광원 중심   창 알파 {a2:.4f}  비침 {b2:.2f}%   ← 단일 최악")
    print()
    print("  ★★ 정정 — 나는 처음에 이 둘을 겹쳐 쌓아 "
          f"{bboth:.2f}% 라고 적었다. **겹치지 않는다.** 기하를 확인했다:")
    stage_h, fade, glow_h = 238.0, 0.45, 74.0
    print(f"    무대 높이 {stage_h:.0f}pt (StageHeight) · 시인은 위에서 "
          f"{fade*100:.0f}%({stage_h*fade:.0f}pt)에서 알파 0 (UiChrome.SheenFadeRatio)")
    print(f"    바닥 광원은 위에서 {stage_h-glow_h:.0f}pt 지점부터 아래 {glow_h:.0f}pt "
          f"(StageFloorGlowHeight)")
    print(f"    {stage_h*fade:.0f} < {stage_h-glow_h:.0f}  ⇒ 두 겹이 만나는 화소가 없다.")
    print(f"    ⇒ **단일 최악은 {b2:.2f}%이고 {bboth:.2f}%는 도달 불가다.** 그 숫자는 폐기한다.")
    print(f"    그래도 {b2:.2f}%는 크다 — 2026-08-31 「창이 여러 개로 겹쳐 보인다」와 같은 형태다(N-1).")
    print("    ⇒ 여기에 알파 글로우를 더 얹으면 그 위에 또 쌓인다(GB 결론의 근거).")
    print()
    print("  세 번째 raw 알파(전수 확인): ActionCommandPopover \"AcceptFlashPeak\" = AccentSurface α0.14 -> 0.")
    print(f"    접수 플래시라 **일시적**이고 최악 비침은 같은 {b2:.2f}%. 별건으로만 적는다.")

    print("\n  대안: Flatten(글로우, 바탕) — α=1로 미리 합성. 겉보기 색이 같고 창 알파는 1.0 유지.")
    for a in (0.14, 0.30, 0.50):
        over = TOK["Accent"]
        onto = TOK["CardSurface"]
        flat = tuple(round(onto[i] + (over[i] - onto[i]) * a) for i in range(3))
        fin, bleed = desktop_bleed(1.0, [a])
        print(f"    α{a:.2f} 브라스 on CardSurface -> {HEX(flat)}  (알파판 비침 {bleed:.2f}% / 합성판 0.00%)")
    print()


# ============================================================================
# 4. 【G-1】 보라를 이 팔레트가 받을 수 있는가 — 예약 2건과의 거리
# ============================================================================

# 후보 보라 — 출처를 전부 적는다.
PURPLE_CANDIDATES = {
    "P0 라벤더(틴트 재사용)": CATEGORY_TINT["BACK+FX 라벤더"],   # #B08FD0 ★ 임자 있음
    "P1 컬러잉크 팩주(재사용)": PACK_MAIN["컬러 잉크"],           # #9768CC ★ 임자 있음
    "P2 컬러잉크 팩보(재사용)": PACK_SUB["컬러 잉크"],            # #8563AB ★ 임자 있음
    "P3 #A78BFA (Tailwind violet-400)": H("#A78BFA"),
    "P4 #8B5CF6 (violet-500)": H("#8B5CF6"),
    "P5 #C4B5FD (violet-300)": H("#C4B5FD"),
    "P6 #7C5CD6": H("#7C5CD6"),
    "P7 #B388FF": H("#B388FF"),
    "P8 #9D7BE8": H("#9D7BE8"),
}


def hue(rgb):
    return CL.hue_deg(rgb)


def g1_purple_feasibility():
    print("=" * 78)
    print("G-1. 보라는 이 팔레트에 임자가 둘이다 — 후보별 침범 실측")
    print("=" * 78)
    print("  임자 1: CategoryTint(BACK)=CategoryTint(FX) 라벤더 #B08FD0  (망토·이펙트 카드/도트/워시)")
    print("  임자 2: DLC 「컬러 잉크」 팩 주색 #9768CC / 보조 #8563AB\n")
    print(f"  {'후보':30s} {'hex':9s} {'H°':>6s} {'CR카드':>7s} {'CR패널':>7s} "
          f"{'ΔE라벤더':>9s} {'ΔE팩주':>8s} {'최근접 예약':>26s}")
    rows = []
    for name, c in PURPLE_CANDIDATES.items():
        crc = CR(c, TOK["CardSurface"])
        crp = CR(c, TOK["PanelSurface"])
        d_lav = dE(c, CATEGORY_TINT["BACK+FX 라벤더"])
        d_pack = dE(c, PACK_MAIN["컬러 잉크"])
        near = nearest(c, k=1)[0]
        rows.append((name, c, crc, crp, d_lav, d_pack, near))
        print(f"  {name:30s} {HEX(c):9s} {hue(c):6.1f} {crc:7.2f} {crp:7.2f} "
              f"{d_lav:9.2f} {d_pack:8.2f}   {near[1]} {near[0]:.2f}")

    print("\n  판정 규칙(둘 다 넘어야 채택 후보):")
    print(f"    (a) 어두운 면 5종 최악 CR >= {MIN_NONTEXT} (비텍스트 하한)")
    print(f"    (b) 예약 25색 최근접 ΔE >= {D_DISCRIM} (변별 하한)")
    print(f"\n  {'후보':30s} {'최악CR':>7s} {'최근접ΔE':>9s}  판정")
    survivors = []
    for name, c, *_ in rows:
        worst_cr = min(CR(c, TOK[s]) for s in DARK_SURFACES)
        near_d, near_n = nearest(c, k=1)[0]
        ok = worst_cr >= MIN_NONTEXT and near_d >= D_DISCRIM
        if ok:
            survivors.append((name, c))
        print(f"  {name:30s} {worst_cr:7.2f} {near_d:9.2f}  "
              f"{'통과' if ok else '탈락'}  ({near_n})")
    print()
    return survivors


# ============================================================================
# 5. 【G-2】 보라를 등급 램프에 넣을 수 있는가 — 휘도 단조 제약을 푼다
# ============================================================================


def g2_ramp_admission():
    print("=" * 78)
    print("G-2. 「영웅 리본을 보라로 바꾼다」가 가능한가 — 부등식을 푼다")
    print("=" * 78)
    card = TOK["CardSurface"]
    Lc = CL.L(card)
    need_lo = CR(RARITY["희귀"], card)
    need_hi = CR(RARITY["전설"], card)
    print(f"  램프 휘도 단조 조건: CR(희귀)={need_lo:.4f} < CR(영웅) < CR(전설)={need_hi:.4f}")
    L_lo = need_lo * (Lc + 0.05) - 0.05
    L_hi = need_hi * (Lc + 0.05) - 0.05
    print(f"  ⇒ 영웅 자리의 상대휘도 L 은 ({L_lo:.4f}, {L_hi:.4f}) 안에 있어야 한다.")
    print(f"    (현행 영웅 #DEC081 의 L = {CL.L(RARITY['영웅']):.4f})\n")

    print("  그 창 안의 보라를 전수 탐색한다 — H 260~300°, S 0.15~1.0, V 0.30~1.0, 8bit 격자.")
    best = []
    hits = 0
    for hi_ in range(255, 306, 1):          # 0.5° 대신 1° 격자로 260..305
        h = (hi_ % 360) / 360.0
        for si in range(10, 101, 2):
            for vi in range(30, 101, 1):
                c = CL.hsv_to_rgb(h, si / 100.0, vi / 100.0)
                Lv = CL.L(c)
                if not (L_lo < Lv < L_hi):
                    continue
                hits += 1
                near_d, near_n = nearest(c, k=1)[0]
                best.append((near_d, c, near_n, Lv))
    best.sort(reverse=True)
    print(f"  휘도 창을 만족하는 보라 격자점: {hits}개")
    if best:
        print(f"  그중 예약 25색과 가장 멀리 떨어진 상위 6개:")
        print(f"    {'hex':9s} {'H°':>6s} {'S':>5s} {'V':>5s} {'L':>7s} {'최근접ΔE':>9s}  최근접")
        for near_d, c, near_n, Lv in best[:6]:
            h, s, v = CL.rgb_to_hsv(c)
            print(f"    {HEX(c):9s} {h*360:6.1f} {s:5.2f} {v:5.2f} {Lv:7.4f} {near_d:9.2f}  {near_n}")
        top_d = best[0][0]
        print(f"\n  ⇒ 휘도 창 안에서 예약과의 최대 거리는 ΔE {top_d:.2f}.")
        print(f"    변별 하한 {D_DISCRIM} 대비 { top_d/D_DISCRIM:.2f}배.")
    else:
        print("  ⇒ 휘도 창을 만족하는 보라가 하나도 없다.")

    print("\n  ★ 그런데 휘도만으로는 부족하다 — 회색조에서 인접 등급이 갈려야 한다.")
    print("    회색조 = 같은 L 이면 같은 색이다. 램프의 일 자체가 「L을 4단으로 나누는 것」이므로,")
    print("    영웅 자리를 보라로 바꿔도 회색조에서는 #DEC081 과 구분되지 않는다(같은 L 창).")
    print("    ⇒ 색상만 바뀌고 접근성 축은 한 톨도 안 변한다. 얻는 것은 '보라로 보인다' 하나뿐이다.\n")
    return best[:6]


# ============================================================================
# 6. 【G-3】 채택안 — 글로우는 리본과 다른 채널이다
# ============================================================================

GLOW_EPIC = H("#A78BFA")        # G-1 생존자 중 채택 (아래에서 검산)
GLOW_LEGENDARY = RARITY["전설"]  # ★ 신규 hex 0개 — 등급 램프 전설과 같은 값 #FFD375


def g3_adopted():
    print("=" * 78)
    print("G-3. 채택값 — 영웅 보라 / 전설 금")
    print("=" * 78)
    for label, c, note, exc in (
            ("영웅 글로우", GLOW_EPIC, "신규 hex 1개", ()),
            ("전설 글로우", GLOW_LEGENDARY, "★ 신규 hex 0개 = RarityColor(Legendary)",
             ("등급 전설",))):
        h, s, v = CL.rgb_to_hsv(c)
        print(f"\n  {label}  {HEX(c)}   H {h*360:.1f}°  S {s:.3f}  V {v:.3f}   L {CL.L(c):.4f}   [{note}]")
        print(f"    {'면':22s} {'CR':>7s}  비텍스트 3.0")
        worst = 99
        for s_name in DARK_SURFACES:
            cr = CR(c, TOK[s_name])
            worst = min(worst, cr)
            print(f"    {s_name:22s} {cr:7.2f}  {'PASS' if cr >= MIN_NONTEXT else 'FAIL'}")
        for s_name in ("PortraitSurface", "InkContrastCharcoal"):
            cr = CR(c, TOK[s_name])
            print(f"    {s_name:22s} {cr:7.2f}  {'PASS' if cr >= MIN_NONTEXT else '✘ 미달'}"
                  f"   ← 초상화 무대")
        print(f"    어두운 면 최악 {worst:.2f}")
        if exc:
            print(f"    예약 25색 최근접 4 (자기 자신 {exc} 제외 — 같은 토큰이므로 침범이 아니다):")
        else:
            print(f"    예약 25색 최근접 4:")
        for d, n in nearest(c, k=4, exclude=exc):
            print(f"      {n:24s} ΔE {d:8.2f}  {'OK' if d >= D_DISCRIM else '✘ 침범'}")

    print(f"\n  두 글로우 사이 ΔE = {dE(GLOW_EPIC, GLOW_LEGENDARY):.2f}  "
          f"(식별 하한 {D_IDENT} 대비 {dE(GLOW_EPIC, GLOW_LEGENDARY)/D_IDENT:.2f}배)")
    print("  ⇒ 영웅과 전설은 '나란히 놓지 않아도' 서로 무엇인지 알 수 있다 —")
    print("    등급 램프 6쌍 중 이 조건을 넘는 쌍이 「일반↔전설」 하나뿐이었던 것과 대조된다.")

    print("\n  색각 이상 3유형에서 두 글로우가 갈리는가:")
    print(f"    {'유형':12s} {'영웅':9s} {'전설':9s} {'ΔE':>8s}")
    for t in CVD.TYPES:
        a, b = CVD.sim(GLOW_EPIC, t), CVD.sim(GLOW_LEGENDARY, t)
        d = dE(a, b)
        print(f"    {CVD.KOR[t]:12s} {HEX(a):9s} {HEX(b):9s} {d:8.2f}  "
              f"{'PASS' if d >= D_DISCRIM else '✘'}")
    ga, gb = CL.L(GLOW_EPIC), CL.L(GLOW_LEGENDARY)
    print(f"    완전색맹(휘도만)  L {ga:.4f} vs {gb:.4f}  "
          f"CR {max(ga,gb)+0.05:.4f}/{min(ga,gb)+0.05:.4f} = "
          f"{(max(ga,gb)+0.05)/(min(ga,gb)+0.05):.2f}")
    print("    ⇒ 완전색맹에서는 두 글로우가 밝기로만 갈린다. 그래서 글로우는 등급의")
    print("      주 채널이 될 수 없다 — 리본 칸 수가 그대로 주 채널로 남는다.\n")


# ============================================================================
# 7. 【G-4】 감쇠 곡선 — 불투명 계단 글로우의 링 색을 굽는다
# ============================================================================

RING_STOPS = (0.00, 0.34, 0.62, 0.82, 1.00)   # 중심 -> 바깥 (정규화 반경)


def falloff(t):
    """UiChrome.RadialGlow()와 같은 제곱 감쇠. t = 정규화 반경."""
    return 0.0 if t >= 1.0 else (1.0 - t) * (1.0 - t)


def flatten(over, onto, a):
    return tuple(int(round(onto[i] + (over[i] - onto[i]) * a)) for i in range(3))


def g4_bake_law():
    print("=" * 78)
    print("G-4. 구현 형태 — 글로우는 **겹이 아니라 썸네일 바탕면 자체**다")
    print("=" * 78)
    print("  G-0이 알파 겹을 막았다. 그런데 링을 여러 장 겹치는 것도 답이 아니다 —")
    print("  불투명 링을 겹치면 계단이 '띠'로 보이고, 링을 늘리면 Image 수가 늘어난다.")
    print()
    print("  ★ 옳은 형태: 지금도 존재하는 **썸네일 바탕 Image 한 장의 스프라이트를 갈아끼운다.**")
    print("    · 알파 = 지금 쓰는 둥근 사각 마스크 그대로 (경계 1px AA도 그대로)")
    print("    · RGB = 바탕색 -> 글로우색 **제곱 감쇠 램프**를 구워 넣는다")
    print("    ⇒ 새 Image 0장 · 새 알파 겹 0장 · 창 알파 1.0 유지 · 램프가 연속이라 띠 0개.")
    print("    (UiChrome.RadialGlow()는 램프를 **알파**에 굽는다 — 그 스프라이트는 여기 쓸 수 없다.")
    print("     같은 감쇠식을 **RGB**에 굽는 새 프리미티브가 필요하다. 이건 coder 인계 항목이다.)")
    print()
    print("  ★★ 내 첫 프로브가 틀렸다. 그대로 적는다.")
    print("     처음에 나는 '이웃 텍셀 ΔE <= 0.5'를 띠 판정 기준으로 썼고, 그 결과 반경 512텍셀에서도")
    print("     '띠 위험'이 나왔다. 원인은 램프가 아니라 **자**였다 — 어두운 구역에서 8bit 코드 1칸이")
    print("     CIE76으로 이미 ΔE 1을 넘는다. 즉 그 기준은 **어떤 해상도로도 통과할 수 없는 기준**이고,")
    print("     통과 불가능한 기준은 아무것도 재지 못한다. 아래가 옳은 자다.")
    print()
    print("  띠(밴딩)의 정의: **여러 화소가 같은 코드값으로 뭉친 뒤 한 칸 점프**하는 것.")
    print("  ⇒ 화면 화소당 코드 변화량이 1 이상이면 띠는 원리상 생기지 않는다.")
    print("     반경 39pt(썸네일 높이의 절반). macOS 배율 x1 / x2 두 경우를 다 잰다.\n")
    print(f"    {'글로우':10s} {'바탕':20s} {'최대 코드이동':>10s} "
          f"{'코드/px @x1':>11s} {'코드/px @x2':>11s}  판정")
    for gname, gc, peak in (("영웅 보라", GLOW_EPIC, 0.63), ("전설 금", GLOW_LEGENDARY, 0.42)):
        for bname in ("CardSurfaceMuted", "InkContrastCharcoal"):
            back = TOK[bname]
            # d(code)/dt 최대치는 t=0 에서 |glow-back| * peak * 2
            travel = max(abs(gc[i] - back[i]) for i in range(3)) * peak * 2.0
            per1, per2 = travel / 39.0, travel / 78.0
            ok = per2 >= 1.0
            print(f"    {gname:10s} {bname:20s} {travel:10.1f} {per1:11.2f} {per2:11.2f}  "
                  f"{'띠 불가능' if ok else '★ 띠 가능'}")
    print("    ⇒ 네 조합 전부 화소당 1코드 이상 움직인다. **밴딩은 이 반경에서 원리상 생기지 않는다.**")
    print()
    print("  남는 해상도 요구는 하나뿐이다 — **텍셀이 화면 화소보다 크면 텍셀 계단이 보인다.**")
    print("    화면 반경 = 39pt -> x2에서 78px. ⇒ 램프 텍스처 반경 >= 78텍셀.")
    print("    2의 거듭제곱으로 올려 **반경 128 / 지름 256px** 을 요구한다(RGBA32 = 256KB, 1회 굽기).")
    print("    (참고: UiChrome의 원형 프리미티브는 CircleTextureSize 한 값에 묶여 있다 —")
    print("     coder가 이 스프라이트만 별도 크기로 굽도록 인계한다.)\n")


def g4_solve_peak():
    """peak(중심 실효 α)을 세 제약으로 푼다."""
    print("=" * 78)
    print("G-4-b. 세기(peak) — 세 부등식의 교집합을 푼다")
    print("=" * 78)
    print("  제약 (a) 글로우가 **보여야 한다**: CR(중심, 바탕) >= 3.0")
    print("      ★ 이 하한의 출처는 실측 실패다 — 인계본 아이콘 글로우가 슬롯 대비 1.15~1.22:1이라")
    print("        아무도 못 봤다(Tasklist 2026-09-03 「리더 전제 오류 2」). 3.0은 그 실패의 반대편이다.")
    print("  제약 (b) 그 위의 **선화가 살아야 한다**: CR(CardIconInk #E8E2D6, 중심) >= 3.0")
    print("      펫은 '흰/연회색 선화'다(사용자 지시). 글로우가 밝아지면 선이 먹힌다.")
    print("  제약 (c) subtle — (a)(b)를 만족하는 **가장 낮은** peak를 고른다. 참고 지침이")
    print("      'subtle'을 세 번 반복했다. 세기는 취향이 아니라 하한에서 온다.\n")

    ink = TOK["CardIconInk"]
    out = {}
    for gname, gc in (("영웅 보라", GLOW_EPIC), ("전설 금", GLOW_LEGENDARY)):
        for bname in ("CardSurfaceMuted", "InkContrastCharcoal"):
            back = TOK[bname]
            rows = []
            for pi in range(1, 101):
                peak = pi / 100.0
                c = flatten(gc, back, peak)
                rows.append((peak, CR(c, back), CR(ink, c), HEX(c)))
            ok = [r for r in rows if r[1] >= MIN_NONTEXT and r[2] >= MIN_NONTEXT]
            print(f"  [{gname}] on {bname} {HEX(back)}")
            print(f"    {'peak':>6s} {'중심 hex':>9s} {'CR 중심/바탕':>12s} {'CR 잉크/중심':>12s}  판정")
            for peak, c1, c2, hx in rows:
                if int(round(peak * 100)) % 10 == 0 or (ok and abs(peak - ok[0][0]) < 1e-9):
                    print(f"    {peak:6.2f} {hx:>9s} {c1:12.2f} {c2:12.2f}  "
                          f"{'통과' if (c1 >= MIN_NONTEXT and c2 >= MIN_NONTEXT) else '탈락'}")
            if ok:
                lo, hi = ok[0][0], ok[-1][0]
                out[(gname, bname)] = (lo, hi)
                print(f"    -> 통과 구간 peak ∈ [{lo:.2f}, {hi:.2f}]   "
                      f"★ 채택 = 하한 {lo:.2f} (제약 c)")
            else:
                out[(gname, bname)] = None
                print(f"    -> ★ 통과 구간 없음 — (a)와 (b)가 동시에 서지 않는다")
            print()
    return out


def g4_rings(peaks):
    print("=" * 78)
    print("G-4-c. 채택 감쇠 — 반경별 실효색 (구운 램프의 표본 5점)")
    print("=" * 78)
    print("  a(t) = peak x (1-t)^2,  t = r / R.  R = 썸네일 높이의 절반 = 39pt")
    print("  (썸네일 160x78pt / 아이콘 58x58pt — CharacterInfoWindow.cs:268 / :274)")
    print(f"  표본 t = {RING_STOPS}\n")
    for (gname, bname), peak in peaks.items():
        back = TOK[bname]
        print(f"  [{gname}] on {bname} {HEX(back)}   peak = {peak:.2f}")
        print(f"    {'t':>5s} {'r(pt)':>7s} {'a':>6s} {'hex':>9s} {'CR/바탕':>8s} {'CR 잉크':>8s}")
        for t in RING_STOPS:
            a = peak * falloff(t)
            c = flatten(gc_of(gname), back, a)
            print(f"    {t:5.2f} {t*39:7.1f} {a:6.3f} {HEX(c):>9s} "
                  f"{CR(c, back):8.2f} {CR(TOK['CardIconInk'], c):8.2f}")
        edge = flatten(gc_of(gname), back, peak * falloff(1.0))
        print(f"    가장자리 잔차 ΔE(t=1, 바탕) = {dE(edge, back):.4f}  "
              f"{'원반 경계 안 보임' if dE(edge, back) < 0.5 else '★ 원반 경계 보임'}")
        print()


def gc_of(gname):
    return GLOW_EPIC if "영웅" in gname else GLOW_LEGENDARY


def g4_intersect(bands):
    """두 어두운 바탕(카드 썸네일 / 목탄 무대)에서 동시에 서는 peak 하나를 고른다."""
    print("=" * 78)
    print("G-4-d. peak 는 **등급마다 하나**다 — 바탕별로 다르게 두지 않는다")
    print("=" * 78)
    print("  근거: peak가 바탕마다 갈리면 '같은 영웅 펫'이 카드에서와 무대에서 다른 세기로 빛난다.")
    print("        RarityBorderAlpha 주석이 등급마다 α를 갈지 말라고 한 것과 같은 이유다.\n")
    for gname in ("영웅 보라", "전설 금"):
        segs = [(b, bands.get((gname, b))) for b in ("CardSurfaceMuted", "InkContrastCharcoal")]
        if any(v is None for _, v in segs):
            print(f"  {gname}: 한쪽 바탕에 통과 구간이 없다 — 교집합 없음")
            continue
        lo = max(v[0] for _, v in segs)
        hi = min(v[1] for _, v in segs)
        for b, v in segs:
            print(f"    {b:22s} [{v[0]:.2f}, {v[1]:.2f}]")
        print(f"    교집합              [{lo:.2f}, {hi:.2f}]   폭 {hi-lo:.2f}")
        print(f"    ★ 채택 peak = {lo:.2f} (제약 c: subtle — 하한을 고른다)")
        print(f"      여유: 위로 {(hi-lo)/max(lo,1e-9)*100:.0f}% 남는다. "
              f"'너무 흐리다'는 이의가 오면 올릴 손잡이는 이 상수 하나다.\n")


# ============================================================================
# 8. 【G-5】 데이터 바 그라디언트
# ============================================================================


def gray_of(rgb):
    """회색조 변환 — ★ `rampfix.py:41` 의 정의를 **그대로** 쓴다(L^(1/2.2) x 255).
    내가 새 정의를 쓰면 R9가 발표한 8.27/8.08/7.91과 비교가 불가능해진다."""
    g = round(CL.L(rgb) ** (1.0 / 2.2) * 255)
    return (g, g, g)


def gray_of_exact(rgb):
    """정확한 sRGB 역EOTF로 만든 휘도 보존 회색 — 위 근사와의 차이를 재기 위한 두 번째 자."""
    y = CL.L(rgb)
    lo, hi = 0.0, 255.0
    for _ in range(50):
        mid = (lo + hi) / 2.0
        if CL.L((mid, mid, mid)) < y:
            lo = mid
        else:
            hi = mid
    v = int(round((lo + hi) / 2.0))
    return (v, v, v)


def solve_gate_purple():
    """회색조 게이트(인접 ΔE >= 7.8)를 **닫은 채로** 영웅 자리에 들어갈 수 있는 보라를 찾는다."""
    rare, leg = RARITY["희귀"], RARITY["전설"]
    g_rare, g_leg = gray_of(rare), gray_of(leg)
    best = []
    for hi_ in range(240, 301, 2):                 # 보라~자주 대역
        h = hi_ / 360.0
        for si in range(15, 101, 1):
            for vi in range(40, 101, 1):
                c = CL.hsv_to_rgb(h, si / 100.0, vi / 100.0)
                g = gray_of(c)
                if dE(g_rare, g) < D_DISCRIM or dE(g, g_leg) < D_DISCRIM:
                    continue
                if not (CR(rare, TOK["CardSurface"]) < CR(c, TOK["CardSurface"]) < CR(leg, TOK["CardSurface"])):
                    continue
                best.append((si / 100.0, c, g[0], dE(g_rare, g), dE(g, g_leg)))
    best.sort(key=lambda r: -r[0])                  # 채도 높은 순
    return best


def g8_ribbon_merge():
    print("=" * 78)
    print("G-8. 카드 상단 띠(등급 리본)와 글로우를 **한 토큰으로 합칠 수 있는가**")
    print("=" * 78)
    print("  먼저 사실 확인 — 리더 브리프의 「스크린샷 실측: 영웅 카드=보라 띠」는 우리 빌드가 아니다.")
    print("  출하 중인 카드 상단 띠는 UiChrome._rarityRamp 하나이고 **네 색 전부 황동**이다:")
    for k, v in RARITY.items():
        g = gray_of(v)
        print(f"    {k:4s} {HEX(v)}  H {hue(v):6.1f}°  회색조 코드 {g[0]:3d}")
    print("  (CharacterInfoWindow.Cards.cs:517 ApplyRarityRibbon -> UiChrome.RarityColor)")
    print("  ⇒ **보라 띠는 인계본 목업의 색이지 우리 코드의 색이 아니다.** 두 벌이 아직 없다.\n")

    print("  그러면 합치는 방법은 둘뿐이다:")
    print("   (합-A) 글로우를 램프 색으로 내린다  — 영웅 글로우 = #DEC081(황동). 사용자 지시 위반.")
    print("   (합-B) 램프의 영웅 자리를 보라로 올린다 — 사용자 지시 준수. 아래에서 비용을 잰다.\n")

    ramps = {
        "현행(R9)": [RARITY["일반"], RARITY["희귀"], RARITY["영웅"], RARITY["전설"]],
        "합-B 보라": [RARITY["일반"], RARITY["희귀"], H("#C4B5FD"), RARITY["전설"]],
        "합-B' 진보라": [RARITY["일반"], RARITY["희귀"], GLOW_EPIC, RARITY["전설"]],
    }
    card = TOK["CardSurface"]
    print(f"  {'램프':12s} {'CR 4단':28s} {'단조':>5s} {'회색조 코드':>18s} "
          f"{'회색조 인접ΔE 최소':>16s} {'인접 ΔE 최소':>12s}")
    results = {}
    for name, ramp in ramps.items():
        crs = [CR(c, card) for c in ramp]
        mono = all(crs[i] < crs[i + 1] for i in range(3))
        grays = [gray_of(c) for c in ramp]
        gcodes = "→".join(f"{g[0]}" for g in grays)
        gmin = min(dE(grays[i], grays[i + 1]) for i in range(3))
        cmin = min(dE(ramp[i], ramp[i + 1]) for i in range(3))
        results[name] = (crs, mono, grays, gmin, cmin)
        print(f"  {name:12s} " + " ".join(f"{c:5.2f}" for c in crs)
              + f"   {'OK' if mono else '✘':>5s} {gcodes:>18s} {gmin:16.2f} {cmin:12.2f}")
    print(f"\n  판정 하한: 휘도 단조 필수 · 회색조 인접 ΔE >= {D_DISCRIM} · 인접 ΔE >= {D_DISCRIM}")

    print("\n  ★ 그리고 램프를 건드리면 **테두리 승계 α의 하한이 같이 움직인다**"
          "(UiChrome.RarityBorder, α=0.55).")
    print("    α0.55에서 인접 등급이 카드면 위에서 변별 하한 7.8을 유지하는가:")
    for name, ramp in ramps.items():
        eff = [flatten(c, card, RARITY_BORDER_ALPHA) for c in ramp]
        adj = [dE(eff[i], eff[i + 1]) for i in range(3)]
        worn = TOK["CardBorderWorn"]
        worn_cr = CR(worn, card)
        top_cr = CR(eff[-1], card)
        print(f"    {name:12s} 인접 ΔE " + " / ".join(f"{v:6.2f}" for v in adj)
              + f"   최소 {min(adj):5.2f} {'OK' if min(adj) >= D_DISCRIM else '✘ 미달'}"
              + f"   전설 테두리 CR {top_cr:.2f} vs 착용 {worn_cr:.2f} "
              + f"{'착용이 위' if worn_cr > top_cr else '★ 역전'}")

    print("\n  ★ 그리고 합-B는 **강조색(브라스)과 영웅 리본의 관계**를 바꾼다:")
    for name, ramp in ramps.items():
        print(f"    {name:12s} ΔE(Accent, 영웅) = {dE(TOK['Accent'], ramp[2]):6.2f}   "
              f"ΔE(영웅, 틴트라벤더) = {dE(ramp[2], CATEGORY_TINT['BACK+FX 라벤더']):6.2f}   "
              f"ΔE(영웅, 팩주 컬러잉크) = {dE(ramp[2], PACK_MAIN['컬러 잉크']):6.2f}")

    print("\n  ★★ 그러면 **게이트를 닫은 채로** 들어갈 수 있는 보라가 있는가 — 전수로 푼다.")
    sols = solve_gate_purple()
    print(f"    조건: 회색조 인접 ΔE >= {D_DISCRIM} (양쪽) ∩ 카드면 휘도 단조. H 240~300°.")
    print(f"    해의 개수 {len(sols)}개.  채도 높은 순 상위 5:")
    print(f"      {'hex':9s} {'H°':>6s} {'S':>5s} {'회색조':>5s} {'ΔE 희귀':>8s} {'ΔE 전설':>8s}"
          f" {'ΔE 라벤더':>9s}")
    seen = set()
    shown = 0
    for s, c, g, d1, d2 in sols:
        key = round(s, 2)
        if key in seen:
            continue
        seen.add(key)
        print(f"      {HEX(c):9s} {hue(c):6.1f} {s:5.2f} {g:5d} {d1:8.2f} {d2:8.2f}"
              f" {dE(c, CATEGORY_TINT['BACK+FX 라벤더']):9.2f}")
        shown += 1
        if shown >= 5:
            break
    if sols:
        smax = max(r[0] for r in sols)
        print(f"\n    ⇒ ★ 게이트를 닫는 해의 **최대 채도는 S = {smax:.2f}**이다.")
        print("      그 정도 채도는 '보라'가 아니라 **연한 페리윙클**로 읽힌다.")
        print("      즉 '게이트를 지키는 보라'와 '보라로 보이는 보라'는 교집합이 없다.")

    print("\n  ★ 회색조 자 두 벌의 차이 — 정직하게 남긴다:")
    cur = [RARITY[k] for k in ("일반", "희귀", "영웅", "전설")]
    for gname, fn in (("rampfix 근사 L^(1/2.2)", gray_of), ("정확 sRGB 역EOTF", gray_of_exact)):
        gs = [fn(c) for c in cur]
        adj = [dE(gs[i], gs[i + 1]) for i in range(3)]
        print(f"    {gname:24s} {'→'.join(str(g[0]) for g in gs):>20s}  인접 "
              + " / ".join(f"{v:.2f}" for v in adj) + f"  최소 {min(adj):.2f}"
              + f"  {'게이트 통과' if min(adj) >= D_DISCRIM else '★ 게이트 미달'}")
    print("    ⇒ R9가 발표한 8.27 / 8.08 / 7.91은 **근사 자로 재현된다**(내 자가 그 문서와 같은 것을 잰다).")
    print("      다만 **정확한 sRGB 역EOTF로 재면 최소가 7.70으로 하한 7.8에 1.3% 못 미친다.**")
    print("      결함으로 보고하지는 않는다 — 대리 지표의 자 선택 차이이고 R9의 판정을 뒤집을 크기가 아니다.")
    print("      기록해 두는 이유는 하나다: **다음 사람이 다른 자로 재고 「회귀」라고 부르지 않도록.**")
    print()
    return results


def g7_bright_stage():
    print("=" * 78)
    print("G-7. 밝은 무대(#E9EAE6)에서 글로우는 **원리상 성립하지 않는다**")
    print("=" * 78)
    paper = TOK["PortraitSurface"]
    print(f"  초상화 무대 바탕은 잉크에서 파생된다(CharacterPortraitStage.ResolveBackdropColor):")
    print(f"    검은 잉크(출하 기본) -> 종이 {HEX(paper)}   / 흰 잉크 -> 목탄 {HEX(TOK['InkContrastCharcoal'])}")
    print(f"  종이 위에서 두 글로우색의 대비:")
    print(f"    영웅 보라 {HEX(GLOW_EPIC)}  CR {CR(GLOW_EPIC, paper):.2f}   "
          f"{'PASS' if CR(GLOW_EPIC, paper) >= MIN_NONTEXT else '✘ 3.0 미달'}")
    print(f"    전설 금   {HEX(GLOW_LEGENDARY)}  CR {CR(GLOW_LEGENDARY, paper):.2f}   "
          f"{'PASS' if CR(GLOW_LEGENDARY, paper) >= MIN_NONTEXT else '✘ 3.0 미달'}")
    print("  ⇒ 밝은 바탕에서 '빛나는 점'은 **바탕보다 어두운 점**이 된다. 그건 글로우가 아니라")
    print("    그림자이고, 그림자는 2026-09-02 사용자 지시로 이 앱에서 전부 삭제됐다.\n")

    print("  그래서 밝은 무대에서는 '글로우'가 아니라 **포인트 색 한 획**으로 내려앉는다.")
    print("  같은 색상각을 유지한 채 종이 위에서 3.0을 넘는 가장 밝은 값을 각각 푼다:")
    for gname, gc in (("영웅 보라", GLOW_EPIC), ("전설 금", GLOW_LEGENDARY)):
        h, s, v = CL.rgb_to_hsv(gc)
        best = None
        for vi in range(100, 4, -1):
            for si in range(int(s * 100), 101, 2):
                c = CL.hsv_to_rgb(h, si / 100.0, vi / 100.0)
                if CR(c, paper) >= MIN_NONTEXT:
                    if best is None or CL.L(c) > CL.L(best[0]):
                        best = (c, si / 100.0, vi / 100.0)
        if best:
            c, si, vi = best
            print(f"    {gname:10s} -> {HEX(c)}  (H {h*360:.1f}° 유지, S {si:.2f}, V {vi:.2f})")
            print(f"      CR 종이 {CR(c, paper):.2f} / CR 목탄 {CR(c, TOK['InkContrastCharcoal']):.2f} / "
                  f"ΔE 원색 {dE(c, gc):.2f}")
            nb = nearest(c, k=1, exclude=("등급 전설",) if gname.startswith("전설") else ())[0]
            print(f"      예약 최근접 {nb[1]} ΔE {nb[0]:.2f}  "
                  f"{'OK' if nb[0] >= D_DISCRIM else '✘ 침범'}")
    print("\n  ★ 판정: 종이 무대에서는 **글로우를 그리지 않는다**(GB-4). 위 어두운 변종은")
    print("    '필요하면 이 값'이라는 예비이고, 지금 라운드는 채택하지 않는다 — 근거:")
    print("    초상화 무대는 지금 등급을 한 글자도 말하지 않는다(리본 0개). 거기에 등급 신호를")
    print("    새로 여는 것은 사용자 지시 범위 밖이고, 밝은 무대에서 그 신호는 반드시 '어두운 점'이 된다.\n")


def g5_databar():
    print("=" * 78)
    print("G-5. 데이터 바 — 그라디언트 시작/끝 + 하이라이트")
    print("=" * 78)
    track = flatten((255, 255, 255), TOK["CardSurfaceMuted"], 0.09)   # Stats.cs:346
    fill = TOK["Accent"]                                              # Stats.cs:351
    print(f"  현행(실측): 트랙 = Flatten(TrackBackground α0.09, CardSurfaceMuted) = {HEX(track)}")
    print(f"              채움 = UiChrome.Accent = {HEX(fill)}  [평면색, 그라디언트 없음]")
    print(f"    CR(채움/트랙) = {CR(fill, track):.2f}   CR(트랙/카드면) = "
          f"{CR(track, TOK['CardSurfaceMuted']):.2f}")
    print("    ⇒ 사용자가 본 '주황~노랑 그라디언트'는 인계본 목업이다. 우리 출하는 평면 브라스다.\n")

    print("  제안: 채움을 브라스 한 색이 아니라 **브라스 -> 등급 전설 금**으로 잇는다.")
    print("        새 hex 0개(양 끝이 전부 출하 중인 토큰). 왼쪽이 어둡고 오른쪽이 밝다 =")
    print("        '차오른다'가 방향을 갖는다.\n")
    start, end = TOK["Accent"], RARITY["전설"]
    print(f"    시작 {HEX(start)} (Accent)  ->  끝 {HEX(end)} (RarityColor(Legendary))")
    print(f"    {'stop':>5s} {'hex':>9s} {'CR vs 트랙':>11s} {'ΔE 시작대비':>12s}")
    prev = None
    for i in range(0, 5):
        t = i / 4.0
        c = tuple(int(round(start[j] + (end[j] - start[j]) * t)) for j in range(3))
        print(f"    {t:5.2f} {HEX(c):>9s} {CR(c, track):11.2f} {dE(c, start):12.2f}")
        prev = c
    print(f"\n    양 끝 ΔE = {dE(start, end):.2f}  (변별 하한 {D_DISCRIM} — "
          f"{'넘는다: 방향이 읽힌다' if dE(start,end) >= D_DISCRIM else '못 넘는다: 그라디언트가 안 보인다'})")
    print(f"    최악 CR(시작색/트랙) = {min(CR(start, track), CR(end, track)):.2f}  "
          f"(비텍스트 하한 {MIN_NONTEXT})")

    print("\n  하이라이트 후보를 **진행부 끝(밝은 쪽)** 옆에 세워 보면:")
    for cand_name, cand in (("TextPrimary #F2F4F7", TOK["TextPrimary"]),
                            ("AccentGlowCore #E6F1FF", TOK["AccentGlowCore"]),
                            ("CardIconInk #E8E2D6", TOK["CardIconInk"])):
        print(f"    {cand_name:24s} CR vs 끝색 {CR(cand, end):5.2f}  "
              f"CR vs 트랙 {CR(cand, track):5.2f}  ΔE vs 끝색 {dE(cand, end):6.2f}")
    print("  ⇒ ★ **세 후보 전부 끝색과 1.10~1.29:1이다. 캡을 그려도 안 보인다.**")
    print("    끝색 #FFD375 자체가 이미 거의 흰색 밝기라(L 0.691) 그 위에 더 밝은 것을 얹을 자리가 없다.")
    print(f"    반면 진행부 끝 ↔ 트랙 경계는 이미 {CR(end, track):.2f}:1 이다 —")
    print("    **선단은 캡이 아니라 트랙과의 경계가 이미 표시하고 있다. 캡은 기각한다.**")
    print("    (사용자 지시의 'highlight effect'는 그라디언트의 **밝은 끝** 자체가 맡는다.)")
    print("\n  ★ 그리고 흰색 저알파 캡은 G-0 그대로 창에 구멍을 낸다 — 기각의 두 번째 이유다.\n")

    print("  ★★ 사용자 지시의 'much higher contrast'는 채움이 아니라 **트랙**이 지고 있다.")
    print(f"    현행 CR(트랙 {HEX(track)} / 카드면 {HEX(TOK['CardSurfaceMuted'])}) = "
          f"{CR(track, TOK['CardSurfaceMuted']):.2f}  ← 비텍스트 하한 {MIN_NONTEXT} 미달")
    print("    즉 '얼마나 차 있는가'의 분모(빈 칸)가 카드면에 녹아 있다. 부등식을 푼다:")
    card = TOK["CardSurfaceMuted"]
    Lc = CL.L(card)
    lo = MIN_NONTEXT * (Lc + 0.05) - 0.05
    print(f"      트랙이 카드면과 3.0 이상이려면  L(트랙) >= {lo:.4f}")
    hits = []
    for v in range(0, 256):
        g = (v, v, v)
        if CL.L(g) < lo:
            continue
        if CR(start, g) >= MIN_NONTEXT and CR(end, g) >= MIN_NONTEXT:
            hits.append(g)
    if hits:
        print(f"      그리고 채움 양 끝과도 3.0 이상이어야 한다 -> 무채색 해 {len(hits)}개, "
              f"가장 어두운 해 {HEX(hits[0])}")
        g = hits[0]
        print(f"      {HEX(g)}: CR/카드면 {CR(g, card):.2f}  CR(시작색) {CR(start, g):.2f}  "
              f"CR(끝색) {CR(end, g):.2f}")
        print(f"      참고 — 등급 리본 트랙 RarityTrack {HEX(RARITY_TRACK)}는 "
              f"CR/카드면 {CR(RARITY_TRACK, card):.2f} 로 같은 병을 앓고 있고,")
        print("      그쪽은 §12-4가 **양쪽이 동시에 서는 색이 없다**고 부등식으로 닫았다.")
        print("      ★ 게이지 트랙은 다르다 — 채움이 리본보다 밝아서(브라스~금) 창이 열린다.")
    else:
        Ls = CL.L(start)
        hi = (Ls + 0.05) / MIN_NONTEXT - 0.05
        print(f"      ★ 해 없음. 부등식이 공집합이다:")
        print(f"        카드면과 3.0  ->  L(트랙) >= {lo:.4f}")
        print(f"        시작색과 3.0  ->  L(트랙) <= {hi:.4f}   (L(시작색)={Ls:.4f})")
        print("      ⇒ **트랙 색을 더 찾지 마라.** 등급 리본 트랙이 §12-4에서 닫은 것과 같은 형태다.")
        print("        (그쪽 결론: 「트랙은 보이는 구획선이 아니라 홈이다」)")
        print()
        print("      대신 **트랙 테두리 한 겹**으로 푼다 — 테두리는 자기 대비 예산을 따로 갖는다.")
        print("      조건: CR(테두리, 카드면) >= 3.0. 팔레트에 이미 있는 색으로 푼다(새 hex 0개):")
        print(f"        {'토큰':22s} {'hex':9s} {'CR/카드면':>9s} {'CR/트랙':>8s} {'CR/시작색':>9s}  판정")
        for tname in ("NonTextMuted", "TextTertiary", "DisabledControlInk", "TextSecondary"):
            tc = TOK[tname]
            print(f"        {tname:22s} {HEX(tc):9s} {CR(tc, card):9.2f} {CR(tc, track):8.2f} "
                  f"{CR(tc, start):9.2f}  "
                  f"{'PASS' if CR(tc, card) >= MIN_NONTEXT else '✘'}")
        tc = TOK["RarityTrack"] if "RarityTrack" in TOK else RARITY_TRACK
        print(f"        {'RarityTrack':22s} {HEX(RARITY_TRACK):9s} {CR(RARITY_TRACK, card):9.2f} "
              f"{CR(RARITY_TRACK, track):8.2f} {CR(RARITY_TRACK, start):9.2f}  "
              f"{'PASS' if CR(RARITY_TRACK, card) >= MIN_NONTEXT else '✘'}")
        print("      ⇒ 통과하는 값이 있다. **색은 있고, 없는 것은 그 테두리를 그릴 자리(치수)다.**")
    print("    ⇒ **치수(높이·둥글기·테두리 두께)는 ux-designer 소관이고, 나는 색만 낸다.**\n")

    print("  ★ 그리고 이 그라디언트는 **스탯 게이지 전용이다.** 등급 리본에 쓰지 마라 —")
    print("    리본의 채워진 칸이 등급마다 다른 색인데 거기에 램프까지 얹으면")
    print("    '몇 칸이 찼는가'(주 채널)가 '무슨 색인가'에 묻힌다.\n")


# ============================================================================
# 9. 【G-6】 남용 방지선 — 한 화면 동시 발광 상한
# ============================================================================


def g6_budget():
    print("=" * 78)
    print("G-6. 「Subtly glowing points」의 상한 — 숫자로 못 박는다")
    print("=" * 78)
    print("  이 앱의 창은 전체화면 투명 오버레이이고, 상주 앱이다. 상한은 취향이 아니라 예산이다.\n")
    print("  GB-0 — 오늘 글로우를 받는 것은 **펫 2종뿐**이다(사용자 원문이 지목한 것이 펫이다).")
    print("    펫 6종 / requiredLevel 코호트 순위 -> 등급 (ItemCatalog.RarityOfMember):")
    pets = [("작은공", "look_pet_ball", 1), ("종이비행기", "look_pet_plane", 13),
            ("리틀스틱메이트", "look_pet_mini", 19), ("커서친구", "look_pet_cursor", 24),
            ("풍선", "look_pet_balloon", 27), ("달팽이", "look_pet_snail", 30)]
    ladder = ["일반", "일반", "희귀", "희귀", "영웅", "전설"]
    for i, (kor, aid, lv) in enumerate(pets):
        glow = {"영웅": HEX(GLOW_EPIC), "전설": HEX(GLOW_LEGENDARY)}.get(ladder[i], "—")
        print(f"      rank {i}  req {lv:2d}  {kor:10s} {aid:20s} {ladder[i]:4s}  글로우 {glow}")
    print("    ⇒ 오늘 화면에 실제로 뜰 수 있는 발광 지점 = 카드 2 + 무대 1 + 상세 1 = **최대 4**.")
    print("      아래 상한 6은 **훗날 전 슬롯으로 번져도 넘으면 안 되는 천장**이다.\n")
    print("  예산의 근거 3개:")
    print("   (1) 알파 예산 = 0. G-0에 따라 글로우는 전부 불투명 합성이므로 창 알파는 안 내려간다.")
    print("       ⇒ '몇 개까지'는 알파가 아니라 **주의**의 문제로 넘어간다.")
    print("   (2) 등급 분포 — 슬롯당 6종 중 영웅 1 · 전설 1(ItemCatalog._rarityByRank).")
    print("       7슬롯 x (1+1) = 최대 14장이 발광 후보다.")
    print("   (3) 한 섹션은 4열 그리드다(인계본 §1.3 grid-template-columns: repeat(4, 1fr)).")
    print("       한 화면에 통상 1~2섹션 = 카드 6~12장이 보인다.\n")

    print("  상한 계산:")
    slots, per_slot_glow = 7, 2
    print(f"    전 슬롯 발광 후보 = {slots} x {per_slot_glow} = {slots*per_slot_glow}장")
    print("    한 화면 동시 노출(스크롤 위치 최악) = 섹션 2개 x 6장 = 12장,")
    print("      그중 발광 = 섹션당 최대 2장 x 2섹션 = **4장**")
    print("    + 초상화 무대의 펫 1마리 = 1")
    print("    + 상세 패널의 선택 아이템 1 = 1")
    print("    ⇒ 화면 동시 발광 지점 상한 **6**. 이것이 규칙 GB-1이다.\n")

    print("  그리고 발광 '지점'의 정의를 못 박는다(안 그러면 하나가 링 5개로 세어진다):")
    print("    1지점 = 하나의 아이템/펫이 만드는 계단 글로우 1벌(링 몇 개든 1로 센다).")
    print("    리본·테두리·게이지는 **발광이 아니다**(면 색이다). 예산에 안 들어간다.\n")

    print("  GB-2 — 애니메이션 금지. 맥동/호흡/반짝임 0건.")
    print("    근거: 24시간 상주 앱. 움직이는 빛은 주변시가 반드시 잡는다(원칙 2의 정신).")
    print("    참고 지침이 'subtle'을 세 번 반복한 것과 같은 방향이다.\n")

    print("  GB-3 — 바탕화면 위 캐릭터/펫 본체에는 글로우 0건.")
    print("    근거를 숫자로: 바탕화면은 우리가 못 고르는 임의의 그림이다.")
    print("    영웅 보라를 4종 대표 배경에 얹었을 때:")
    for bg_name, bg in (("흰 #FFFFFF", (255, 255, 255)), ("연회색 #CCCCCC", (204, 204, 204)),
                        ("중간회색 #808080", (128, 128, 128)), ("검정 #000000", (0, 0, 0))):
        print(f"      {bg_name:16s} CR(영웅보라) {CR(GLOW_EPIC, bg):6.2f}   "
              f"CR(전설금) {CR(GLOW_LEGENDARY, bg):6.2f}")
    print("    ⇒ 밝은 바탕화면에서는 두 글로우 다 3.0 미달이다 — 거기서 글로우는 그냥 안 보인다.")
    print("      '어떤 배경에서도 보이는 글로우'는 곧 '어떤 배경에서도 눈에 띄는 빛'이고,")
    print("      그건 상주 앱이 사용자 화면에 해서는 안 되는 일이다. **못 하는 게 아니라 안 한다.**\n")


# ============================================================================
# 10. 양성 대조 — 판정기가 알려진 나쁜 값을 실제로 잡는가
# ============================================================================


def control():
    print("=" * 78)
    print("양성 대조 — 알려진 나쁜 값을 판정기가 잡는가 (+ 음성 대조)")
    print("=" * 78)
    rows = []

    # 1. 라벤더 재사용은 예약 침범으로 잡혀야 한다
    d = dE(CATEGORY_TINT["BACK+FX 라벤더"], CATEGORY_TINT["BACK+FX 라벤더"])
    rows.append(("양성1 라벤더 재사용이 예약 침범으로 잡힘", d < D_DISCRIM, True))

    # 2. 컬러잉크 팩주 재사용도 잡혀야 한다
    d2 = nearest(PACK_MAIN["컬러 잉크"], k=1)[0][0]
    rows.append(("양성2 컬러잉크 팩주 재사용이 잡힘", d2 < D_DISCRIM, True))

    # 3. 인계본 수준의 흐린 글로우(슬롯 대비 1.15~1.22)를 CR 하한이 잡는가
    faint = flatten(GLOW_EPIC, TOK["CardSurfaceMuted"], 0.04)
    rows.append((f"양성3 흐린 글로우 {HEX(faint)} CR {CR(faint, TOK['CardSurfaceMuted']):.2f} 가 3.0 미달로 잡힘",
                 CR(faint, TOK["CardSurfaceMuted"]) < MIN_NONTEXT, True))

    # 4. 링 계단이 띠로 보이는 경우(링 2개)를 인접 ΔE 규칙이 잡는가
    back = TOK["CardSurfaceMuted"]
    two = [flatten(GLOW_EPIC, back, 0.80), back]
    rows.append((f"양성4 링 2개 계단 ΔE {dE(two[0], two[1]):.2f} 가 7.8 초과로 잡힘",
                 dE(two[0], two[1]) > D_DISCRIM, True))

    # 5. 알파 글로우가 창 알파를 떨어뜨리는 것을 잡는가
    a, b = desktop_bleed(1.0, [0.20])
    rows.append((f"양성5 α0.20 글로우가 비침 {b:.2f}% 를 만듦(0 아님)", b > 0.5, True))

    # 6. ★ 음성 대조 — 채택 보라는 침범으로 안 잡혀야 한다
    dn = nearest(GLOW_EPIC, k=1)[0][0]
    rows.append((f"음성1 채택 보라 {HEX(GLOW_EPIC)} 최근접 ΔE {dn:.2f} 는 통과",
                 dn >= D_DISCRIM, True))

    # 7. ★ 음성 대조 — 불투명 합성 글로우는 비침 0
    a2, b2 = desktop_bleed(1.0, [1.0])
    rows.append((f"음성2 α=1.0 합성 글로우 비침 {b2:.2f}% = 0", abs(b2) < 1e-9, True))

    # 8. ★ 음성 대조 — 전설 금은 등급 램프 전설 자신이므로 ΔE 0 (그 자신은 침범이 아니다)
    d_self = dE(GLOW_LEGENDARY, RARITY["전설"])
    rows.append((f"음성3 전설 글로우 == RarityColor(Legendary) ΔE {d_self:.2f} = 0",
                 abs(d_self) < 1e-9, True))

    ok = True
    for name, got, want in rows:
        p = got == want
        ok = ok and p
        print(f"  {'PASS' if p else 'FAIL'}  {name}")
    print(f"\n  대조 {sum(1 for n,g,w in rows if g==w)} / {len(rows)}")
    if not ok:
        sys.exit("대조 실패 — 판정기가 죽어 있다. 이 라운드 숫자를 폐기하십시오.")
    print()


# ============================================================================
if __name__ == "__main__":
    calibrate()
    if CONTROL:
        control()
        sys.exit(0)
    g0_alpha_law()
    g1_purple_feasibility()
    g2_ramp_admission()
    g3_adopted()
    g4_bake_law()
    bands = g4_solve_peak()
    picked = {("영웅 보라", "CardSurfaceMuted"): 0.63, ("전설 금", "CardSurfaceMuted"): 0.42,
              ("영웅 보라", "InkContrastCharcoal"): 0.63, ("전설 금", "InkContrastCharcoal"): 0.42}
    g4_rings(picked)
    g4_intersect(bands)
    g8_ribbon_merge()
    g7_bright_stage()
    g5_databar()
    g6_budget()
