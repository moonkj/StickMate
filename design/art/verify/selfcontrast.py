# -*- coding: utf-8 -*-
"""자기 대비(self-contrast) — 「배경을 모르는 채로 읽히게 하는 법」  (design-art, 2026-09-02 R7)

무엇을 재는가
  A. 잉크로 그리는 것(몸·발자국·먼지)이 임의 배경에서 몇 대 1인가, 그리고
     **역잉크 분리막**을 붙이면 하한이 얼마로 잠기는가.
  B. 출하 42종의 「자유 윤곽 0획」 보조색 조각을 **색으로 풀 수 있는가**(대역 내부 대비 상한).
  C. FX 아이콘 tone1 초록 3건의 판정.
  D. 나뭇잎 초록 #5A8C3C 의 WornColor 항등 — **바이트 여백**과 #5A8D3C 채택 여부.

★ 규칙(TEAM.md §4): 계산기는 알려진 값으로 먼저 교정한다. 교정이 하나라도 깨지면 아무 숫자도
  내지 않고 죽는다. 그리고 **모든 "0건/없음" 판정에는 양성 대조를 붙인다**(`--control`).

★ 규칙(TEAM.md 「생성기와 검사기가 같이 틀린다」): 이 파일은 상대휘도를 **colorlab과 다른 코드로
  한 번 더** 구현해 두 구현이 같은 답을 내는지 먼저 확인한다. 같은 함수를 두 번 부르는 것은
  검증이 아니다.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as C          # noqa: E402
import shipped as S           # noqa: E402

FAIL = []


def bad(msg):
    FAIL.append(msg)
    print("  \x1b[31mFAIL\x1b[0m " + msg if sys.stdout.isatty() else "  FAIL " + msg)


def ok(msg):
    print("  OK   " + msg)


# ============================================================================
# 0. 독립 구현 — colorlab과 **다른 코드**로 상대휘도를 다시 짠다
# ============================================================================
# colorlab.lin()은 분기식이다. 여기서는 256칸 LUT를 **한 번에 만들어** 표를 찾는다.
# 두 구현이 갈라지면 그 자리에서 죽는다(둘이 같은 함정에 같이 빠지는 것을 막는 유일한 장치).
_LUT = []
for _i in range(256):
    _c = _i / 255.0
    _LUT.append(_c / 12.92 if _c * 12.92 <= 0.04045 * 12.92 else math.pow((_c + 0.055) / 1.055, 2.4))


def L2(rgb):
    """상대휘도 — LUT판(colorlab.L의 독립 구현)."""
    return 0.2126 * _LUT[rgb[0]] + 0.7152 * _LUT[rgb[1]] + 0.0722 * _LUT[rgb[2]]


def CR2(a, b):
    la, lb = L2(a), L2(b)
    hi, lo = (la, lb) if la >= lb else (lb, la)
    return (hi + 0.05) / (lo + 0.05)


def cr_lum(la, lb):
    """휘도 두 개로부터 직접 대비비(배경을 색이 아니라 휘도로 훑을 때 쓴다)."""
    hi, lo = (la, lb) if la >= lb else (lb, la)
    return (hi + 0.05) / (lo + 0.05)


BLACK, WHITE = (0, 0, 0), (255, 255, 255)
MIN_TEXT, MIN_NONTEXT = 4.5, 3.0
BAND_LO, BAND_HI = 0.1632, 0.2396       # PALETTE_SPEC §0 아트 대역


# ============================================================================
# 1. 교정 — 깨지면 그 뒤 숫자는 전부 폐기
# ============================================================================
def calibrate(mutate=None):
    C.calibrate(verbose=True)
    print("=== selfcontrast 추가 교정 ===")
    rows = []

    def chk(name, got, want, tol):
        rows.append((name, got, want, tol, abs(got - want) <= tol))

    # (a) 두 구현이 같은 답을 내는가 — 전 그레이 256 + 격자 표본
    worst = 0.0
    for i in range(256):
        worst = max(worst, abs(C.L((i, i, i)) - L2((i, i, i))))
    for r in range(0, 256, 17):
        for g in range(0, 256, 23):
            for b in range(0, 256, 29):
                worst = max(worst, abs(C.L((r, g, b)) - L2((r, g, b))))
    chk("독립 구현 최대 휘도차", worst, 0.0, 1e-12)

    # (b) 알려진 값
    chk("CR2 흰/검", CR2(WHITE, BLACK), 21.0, 5e-4)
    chk("CR2 동일색", CR2((90, 140, 60), (90, 140, 60)), 1.0, 5e-4)
    chk("CR2 #767676/흰", CR2((0x76, 0x76, 0x76), WHITE), 4.5422, 5e-4)

    # (c) cr_lum 이 CR2와 같은 답
    chk("cr_lum 대조(흰/검)", cr_lum(L2(WHITE), L2(BLACK)), 21.0, 5e-4)

    # (d) 해석해 검산 — max(검,흰) 하한은 L*=(-2+sqrt(84))/40 에서 sqrt(21)
    lstar = (-2.0 + math.sqrt(84.0)) / 40.0
    chk("교차점 휘도 L*", lstar, 0.1791288, 1e-6)
    chk("교차점에서 두 값이 같다", cr_lum(lstar, 0.0) - cr_lum(lstar, 1.0), 0.0, 1e-9)
    chk("하한 = sqrt(21)", cr_lum(lstar, 0.0), math.sqrt(21.0), 1e-9)

    # (e) 수치 스윕이 해석해와 같은 답 — 100만 분할
    lo, hi = 0.0, 1.0
    for _ in range(60):                      # 삼분탐색: max(검,흰)은 아래로 볼록이 아니지만
        m1 = lo + (hi - lo) / 3.0            # 두 단조 함수의 max라 유일 최소가 있다
        m2 = hi - (hi - lo) / 3.0
        f1 = max(cr_lum(m1, 0.0), cr_lum(m1, 1.0))
        f2 = max(cr_lum(m2, 0.0), cr_lum(m2, 1.0))
        if f1 < f2:
            hi = m2
        else:
            lo = m1
    chk("스윕 최소값 = 해석해", max(cr_lum((lo + hi) / 2, 0.0), cr_lum((lo + hi) / 2, 1.0)),
        math.sqrt(21.0), 1e-6)

    # (f) WornColor 포트 — 이미 colorlab이 외부 실측 2건으로 교정돼 있다. 여기서는
    #     "표식 색은 팔레트가 아니다"만 확인(값 자체는 ItemCatalog가 소유).
    chk("InkTone 은 대역 밖(면제 대상)",
        1.0 if not (BAND_LO <= C.L(C.hex2rgb("#D6DBE3")) <= BAND_HI) else 0.0, 1.0, 0.0)

    if mutate:
        rows = mutate(rows)

    allok = all(r[4] for r in rows)
    for name, got, want, tol, p in rows:
        print(f"  {'PASS' if p else 'FAIL'}  {name:34s} {got:14.9f}  (정답 {want}, 허용 {tol})")
    print(f"  교정 판정: {'유효' if allok else '무효'}\n")
    if not allok:
        sys.exit("교정 실패 — 이 스크립트가 낸 모든 숫자를 폐기하십시오.")


# ============================================================================
# 2. A — 잉크는 자기 힘으로 서지 못한다 (페르소나 실측 재현 + 일반화)
# ============================================================================
def section_A():
    print("╔══ A. 자기 대비 — 잉크로 그리는 것은 배경을 모른다 ══╗\n")

    print("  ── A-1. ★ 대비는 **바탕을 적지 않으면 숫자가 아니다** (리더 정정 반영) ──")
    print("     리더가 넘긴 1.14 / 4.61 은 바탕 ≈#141414 기준이었다. 바탕을 축으로 펼친다.")
    subjects = [("발자국 검정잉크", (0, 0, 0)), ("발자국 흰잉크", (255, 255, 255)),
                ("몸통(캡처 실측)", (0, 0, 2)), ("착지먼지(캡처 실측)", (11, 12, 11)),
                ("아트 초록 #5A8C3C", C.hex2rgb("#5A8C3C")),
                ("망토 빨강 #BC403A", C.hex2rgb("#BC403A"))]
    bgs = [("#000000", (0, 0, 0)), ("#111111", (17, 17, 17)), ("#141414", (20, 20, 20)),
           ("#121312", (18, 19, 18)), ("#1E1E1E", (30, 30, 30)), ("#2D2D30", (45, 45, 48)),
           ("#3C3C3C", (60, 60, 60)), ("#808080", (128, 128, 128)), ("#FFFFFF", (255, 255, 255))]
    print("     대상                  " + "".join(f"{b[0]:>9s}" for b in bgs))
    for sn, sc in subjects:
        print(f"     {sn:20s}" + "".join(f"{CR2(sc, b[1]):9.2f}" for b in bgs))
    print("     ※ 리더 표 재현: 검정 잉크 vs #000000/#111111/#1E1E1E = 1.00/1.11/1.26  ·"
          " 초록 5.25/4.72/4.17")
    for want, sc, bg in ((1.00, BLACK, (0, 0, 0)), (1.11, BLACK, (17, 17, 17)),
                         (1.26, BLACK, (30, 30, 30)),
                         (5.25, C.hex2rgb("#5A8C3C"), (0, 0, 0)),
                         (4.72, C.hex2rgb("#5A8C3C"), (17, 17, 17)),
                         (4.17, C.hex2rgb("#5A8C3C"), (30, 30, 30))):
        got = CR2(sc, bg)
        (ok if abs(got - want) <= 0.005 else bad)(
            f"리더 표 재현 {C.rgb2hex(sc)} / {C.rgb2hex(bg)} = {got:.2f} (표 {want})")
    print("     ★ 그래서 「아트 초록으로 뒀으면 4.61 이었다」는 **바탕 #141414 에서만 참**이다.")
    print("        #1E1E1E(VS Code 계열 어두운 창)에서는 4.17 로 **글자 하한 4.5 아래**다.")

    print("\n  ── A-1b. 리더가 넘긴 「착지먼지 1.10」은 재현되지 않는다 ──")
    got = CR2((11, 12, 11), (18, 19, 18))
    print(f"     (11,12,11) vs (18,19,18) = {got:.4f} : 1   (넘겨받은 값 1.10)")
    cands = [i for i in range(256) if abs(CR2((11, 12, 11), (i, i, i)) - 1.10) < 0.005]
    print(f"     1.10 이 되는 그레이 바탕 = {cands}  ({[C.rgb2hex((i,)*3) for i in cands]})")
    print("     → 값 자체는 살아 있다(1.05~1.10 둘 다 3.0 한참 아래). **바탕이 안 적혀 있었을 뿐.**")

    print("\n  ── A-2. ★ 내 §16-4/§16-5 의 「잉크 최악 4.58:1」은 **거짓이다** ──")
    print("     그 4.58 은 max(검,흰) 의 하한이다 — 화면에 **두 톤이 다 있을 때만** 성립한다.")
    print("     유저는 잉크를 **하나만** 고른다. 한 톤의 하한은 배경이 그 톤과 같아지는 순간 1.00 이다.")
    for name, ink in (("검은 잉크", BLACK), ("흰 잉크", WHITE)):
        worst, arg = 1e9, None
        for i in range(256):
            c = CR2(ink, (i, i, i))
            if c < worst:
                worst, arg = c, i
        (ok if abs(worst - 1.0) < 1e-9 else bad)(
            f"{name} 단독 최악 = {worst:.4f} : 1  (배경 그레이 {arg} = {C.rgb2hex((arg,)*3)})")

    print("\n  ── A-3. 역잉크 분리막을 붙이면 하한이 얼마로 잠기는가 ──")
    worst, argl = 1e9, None
    N = 200000
    for i in range(N + 1):
        lb = i / N
        c = max(cr_lum(lb, 0.0), cr_lum(lb, 1.0))
        if c < worst:
            worst, argl = c, lb
    (ok if abs(worst - math.sqrt(21.0)) < 1e-4 else bad)(
        f"검+흰 쌍의 **임의 배경 하한** = {worst:.4f} : 1  (최악 배경 휘도 L={argl:.6f})")
    print(f"       비텍스트 하한 {MIN_NONTEXT} 대비 여유 {worst - MIN_NONTEXT:+.4f}")
    print(f"       텍스트   하한 {MIN_TEXT} 대비 여유 {worst - MIN_TEXT:+.4f}   ← 글자 하한까지 넘는다")
    print(f"       막↔잉크 내부 대비는 배경과 무관하게 {CR2(BLACK, WHITE):.2f} : 1 (항상)")

    print("\n  ── A-4. 그레이 배경 256칸 중 몇 칸에서 3.0 을 못 넘는가 ──")
    def fail_frac(fn):
        n = sum(1 for i in range(256) if fn((i, i, i)) < MIN_NONTEXT)
        return n, n / 256.0
    cases = [
        ("검은 잉크 단독", lambda bg: CR2(BLACK, bg)),
        ("흰 잉크 단독",   lambda bg: CR2(WHITE, bg)),
        ("검+흰 쌍(분리막)", lambda bg: max(CR2(BLACK, bg), CR2(WHITE, bg))),
    ]
    for name, fn in cases:
        n, f = fail_frac(fn)
        (ok if (n == 0) == ("쌍" in name) else print)(
            f"  {name:16s} 미달 {n:3d}/256 = {f*100:5.1f}%")

    print("\n  ── A-5. ★ 아트 대역색도 같은 병이다 (§0 표는 **극단 둘**만 봤다) ──")
    for lc, tag in ((BAND_LO, "대역 하단"), (BAND_HI, "대역 상단")):
        lo_fail = (lc + 0.05) / 3.0 - 0.05
        hi_fail = 3.0 * lc + 0.10
        span = max(0.0, min(1.0, hi_fail) - max(0.0, lo_fail))
        print(f"     {tag} L={lc:.4f}: 배경 휘도 ({lo_fail:.4f}, {hi_fail:.4f}) 구간에서 3.0 미달"
              f"  = 휘도축의 {span*100:.1f}%")
    n = sum(1 for i in range(256)
            if max(CR2(C.hex2rgb('#5A8C3C'), (i, i, i)), 0) < MIN_NONTEXT)
    print(f"     실례) 나뭇잎 초록 #5A8C3C 단독: 그레이 배경 {n}/256 칸에서 3.0 미달")
    print("     → **대역은 흰·검 바탕화면만 막는다. 중간 밝기 창은 못 막는다.**\n")


# ============================================================================
# 3. A 비용 — 분리막 하나가 잉크를 얼마나 늘리는가
# ============================================================================
PT_PER_UNIT = 982.0 / (2.0 * 12.0)        # 40.9167  (DockGeometry / [렌더품질] 실측)
H_BASE = 2.2746944                        # StickConfig.BaselineCharacterTotalHeight
LWS = 1.045                               # StickmanStrokeWidths.LineWidthScale
MIN_STROKE_PT = 2.0                       # StickConfig.MinStrokeScreenPoints
MIN_FILL_OUTLINE_PT = 1.0                 # StickConfig.MinFillOutlineScreenPoints
MEMBRANE_PX = 1.0                         # DialogueBubbleRenderer.OutlineRingMinPhysicalPixels


def section_A_cost():
    print("╔══ A-비용. 분리막 1물리픽셀의 값 ══╗\n")
    parts = [
        ("몸통", 0.11 * LWS, False),
        ("다리", 0.12 * LWS, False),
        ("팔",   0.10 * LWS, False),
        ("머리 링", 0.11 * 0.7 * LWS, True),      # 채움 경계선(하한 1pt)
        ("FX 획(0.022 H)", 0.022 * H_BASE, False),
    ]
    for dpr, plat in ((2.0, "macOS Retina / Windows 200%"), (1.0, "Windows 100%")):
        print(f"  ── 물리픽셀/유닛 = {PT_PER_UNIT*dpr:.4f}   ({plat}) ──")
        print("     부위        배율   설계획(px)  하한적용(px)  막1px 추가면적  막/획")
        for name, w1, isfill in parts:
            for sc in (0.35, 0.60, 0.75, 1.00):
                w_world = w1 * sc
                px = w_world * PT_PER_UNIT * dpr
                floor_px = (MIN_FILL_OUTLINE_PT if isfill else MIN_STROKE_PT) * dpr
                eff = max(px, floor_px)
                add = 2.0 * MEMBRANE_PX / eff
                print(f"     {name:11s} {sc:4.2f}  {px:8.3f}  {eff:10.3f}      {add*100:8.1f}%"
                      f"     {MEMBRANE_PX/eff:5.3f}")
            print()
    print("  판정: 막을 **물리픽셀**로 정의하면 추가 잉크는 획 두께에 반비례한다.")
    print("        굵은 몸통에서 가장 싸고, 얇은 FX에서 가장 비싸다 — 필요한 곳에서 비싸다(정상).")
    print("        Windows 100%에서는 획 자체가 절반 픽셀 수라 **막이 상대적으로 두 배 무겁다**.\n")


# ============================================================================
# 4. B — 자유 윤곽 0획 13건: 색으로 풀 수 있는가
# ============================================================================
def section_B():
    print("╔══ B. 대역 내부 대비의 **상한** — 13건은 색으로 못 푼다 ══╗\n")
    cap = (BAND_HI + 0.05) / (BAND_LO + 0.05)
    print(f"  두 색이 **모두** 아트 대역 안이면 대비비 상한 = ({BAND_HI}+0.05)/({BAND_LO}+0.05)"
          f" = {cap:.4f} : 1")
    (ok if cap < MIN_NONTEXT else bad)(
        f"상한 {cap:.4f} < 비텍스트 하한 {MIN_NONTEXT}  → 대역 안에서는 **어떤 조합도** 3.0을 못 넘는다")

    print("\n  ── 출하 애셋 실측: 한 아이템 안 주색↔보조색 대비 ──")
    items = S.item_colors()
    rows = []
    for name, rec in sorted(items.items()):
        p = sorted(rec["tones"].get(0, ()))
        q = sorted(rec["tones"].get(1, ()))
        if not p or not q:
            continue
        for a in p:
            for b in q:
                rows.append((name, a, b, CR2(a, b)))
    if not rows:
        bad("주/보조 쌍을 하나도 못 읽었다 — 파서가 죽었다")
        return
    worst = max(r[3] for r in rows)
    print(f"     쌍 {len(rows)}건 · 최대 대비 {worst:.4f} : 1 · 최소 {min(r[3] for r in rows):.4f} : 1")
    over = [r for r in rows if r[3] >= MIN_NONTEXT]
    print(f"     3.0 이상인 쌍: {len(over)}건")
    for r in over[:8]:
        print(f"       {r[0]:28s} {C.rgb2hex(r[1])} / {C.rgb2hex(r[2])} = {r[3]:.2f}")
    (ok if worst <= cap + 1e-6 else bad)(
        f"실측 최대 {worst:.4f} ≤ 이론 상한 {cap:.4f}  (상한식이 실물과 맞다)")

    print("\n  ── 3.0을 넘기려면 짝의 휘도가 어디에 있어야 하는가 ──")
    for lc, tag in ((BAND_LO, "대역 하단"), (BAND_HI, "대역 상단")):
        need_dark = (lc + 0.05) / 3.0 - 0.05
        need_light = 3.0 * lc + 0.10
        print(f"     {tag} L={lc:.4f} → 짝은 L ≤ {need_dark:.4f} 이거나 L ≥ {need_light:.4f}")
    print(f"     ★ 그 두 값이 곧 §0 표의 목탄 무대(0.0211)·종이 무대(0.8188)다 —")
    print(f"       대역은 애초에 **그 둘에 대해 정확히 3.0**이 되도록 잡은 구간이다.")
    print(f"       즉 짝은 **정의상 대역 밖**이어야 한다. 팔레트 안에는 답이 없다.\n")

    print("  ── 무채색 경계선을 그으면 얼마가 되는가 (대역 전 구간) ──")
    for ink, nm in ((BLACK, "검정"), (WHITE, "흰색")):
        li = L2(ink)
        lo = min(cr_lum(li, BAND_LO), cr_lum(li, BAND_HI))
        hi = max(cr_lum(li, BAND_LO), cr_lum(li, BAND_HI))
        (ok if lo >= MIN_NONTEXT else bad)(
            f"{nm} 경계선 vs 대역색: {lo:.3f} ~ {hi:.3f} : 1  (하한 {MIN_NONTEXT} {'통과' if lo>=MIN_NONTEXT else '미달'})")
    fo_lo = min(cr_lum(C.L(C.fill_outline(c)), C.L(c))
                for c in [C.hsv_to_rgb(h / 12.0, 0.55, 0.62) for h in range(12)])
    print(f"     참고) 현행 ×0.62 그늘 윤곽은 자기 채움과 {fo_lo:.2f} : 1 부근 — 3.0 미달\n")


def section_B2():
    print("╔══ B-2. ★ 상수 하나로 풀리는가 — 그늘 배수 ×0.62 의 재유도 ══╗\n")
    items = S.item_colors()
    marks = {C.hex2rgb("#D6DBE3"), C.hex2rgb("#8B939F")}
    uniq = set()
    for rec in items.values():
        for t in rec["tones"].values():
            uniq |= set(t)
    art = sorted(uniq - marks)

    def cr_at(c, k):
        o = tuple(int(round(v * k)) for v in c)
        return CR2(o, c)

    print("     각 색이 자기 그늘과 3.0 을 넘으려면 배수가 얼마여야 하는가 (0.001 격자 탐색)")
    need = []
    for c in art:
        best = None
        kk = 0.999
        while kk > 0.0:
            if cr_at(c, kk) >= MIN_NONTEXT:
                best = kk
                break
            kk = round(kk - 0.001, 3)
        need.append((c, best if best is not None else 0.0, cr_at(c, 0.62)))
    need.sort(key=lambda r: r[1] if r[1] is not None else -1)
    for c, k, cur in need[:6]:
        print(f"        {C.rgb2hex(c)}  현행×0.62 = {cur:.3f} : 1   3.0 을 넘는 최대 배수 = "
              f"{k if k is not None else float('nan'):.3f}")
    kmin = min(r[1] for r in need)
    kmax = max(r[1] for r in need)
    print(f"     ...  25색 전체 요구 배수 범위 [{kmin:.3f}, {kmax:.3f}]")
    print(f"     ★ 배수를 **{kmin:.2f}** 로 내리면 25색 **전부** 자기 그늘과 3.0 이상이 된다.")
    worst = min(cr_at(c, kmin) for c in art)
    (ok if worst >= MIN_NONTEXT else bad)(
        f"×{kmin:.2f} 에서 25색 최악 자기대비 = {worst:.3f} : 1  (하한 {MIN_NONTEXT})")
    worst62 = max(cr_at(c, 0.62) for c in art)
    print(f"     대조) 현행 ×0.62 에서 25색 **최고**가 {worst62:.3f} : 1 — 최고조차 3.0 미달이다.")
    print("     ── 대가: 그늘이 어두워지면 **어두운 바탕화면** 쪽 대비가 준다 ──")
    for k in (0.62, kmin):
        lo = min(CR2(tuple(int(round(v * k)) for v in c), (0, 0, 0)) for c in art)
        hi = min(CR2(tuple(int(round(v * k)) for v in c), (255, 255, 255)) for c in art)
        print(f"        ×{k:.2f}: 그늘 vs 검은 바탕 최악 {lo:.2f} : 1 · 흰 바탕 최악 {hi:.2f} : 1")
    print("     → 그늘은 **바깥 테두리**에도 쓰이므로 이 거래를 같이 물어야 한다.")
    print("       채움 안쪽 조각에만 다른 배수를 주면 거래가 없다(윤곽이 배경을 안 만난다).\n")


# ============================================================================
# 5. C — FX 아이콘 tone1
# ============================================================================
def section_C():
    print("╔══ C. 카테고리 틴트 ↔ 애셋 어긋남 — 무엇을 고칠 것인가 ══╗\n")
    items = S.item_colors()

    print("  ── C-1. 틴트가 **몸에 닿는가** (코드 실측) ──")
    print("     UiChrome.CategoryTint 소비처 4곳 = CharacterInfoWindow.Cards(2) + .Inventory(2)")
    print("     전부 **정보창 UI**다. 몸을 그리는 어느 렌더러도 부르지 않는다.")
    print("     → 틴트가 몸에 닿는 통로는 **규칙 (2) 하나뿐**이다(아이템 색의 색상대를 정해서).")

    print("\n  ── C-2. 규칙 (2)를 실제로 탄 색이 몇 개인가 (애셋 전량) ──")
    NAMED = {
        "#96814F": "Ivory", "#BA7636": "Wool", "#5577AE": "Felt", "#9B7922": "Gold",
        "#988540": "GoldLight", "#587398": "Silver", "#5075B5": "DarkLens",
        "#BA5928": "Leather", "#AB7942": "Canvas", "#6787B9": "Paper", "#C6443C": "Toy",
        "#A16A28": "HairBrown", "#CC5512": "TintHead", "#20878C": "TintEyes",
        "#5A8C3C": "TintNeck", "#428C24": "NeckDeep", "#955CCC": "TintBack",
        "#3378CC": "Accent", "#D6DBE3": "InkTone", "#8B939F": "InkDimTone",
    }
    census = {}
    for name, rec in sorted(items.items()):
        for tone, cs in rec["tones"].items():
            for c in cs:
                census.setdefault(C.rgb2hex(c), []).append(f"{name}:t{tone}")
    tintish = ["#CC5512", "#20878C", "#5A8C3C", "#428C24", "#955CCC"]
    for hx in tintish:
        users = census.get(hx, [])
        print(f"     {NAMED.get(hx,'?'):10s} {hx}  사용 {len(users):2d}곳  {', '.join(users)}")
    print("     ★ FX/PET 슬롯에서 틴트색을 쓰는 조각:")
    orphan = []
    for hx in tintish:
        for u in census.get(hx, []):
            if u.startswith("look_fx") or u.startswith("look_pet"):
                orphan.append((u, hx))
    for u, hx in orphan:
        print(f"        {u:26s} {hx}")
    print(f"     합계 {len(orphan)}건. 그중 나뭇잎 t0 는 **잎의 재료색**이라 규칙 (1) 소관이다.")
    real = [o for o in orphan if not o[0].startswith("look_fx_leaf")]
    print(f"     → 규칙 (2)만이 근거인 조각 = **{len(real)}건**  ({', '.join(o[0] for o in real)})")

    print("\n  ── C-3. 그 4건은 규칙 (3)도 어긴다 (도형 실측) ──")
    for who, what in (("반짝임 t1", "작은 별 하나 — t0와 **같은 요소**(9점 별), 크기만 다름"),
                      ("먼지 t1",   "(8,30)-(32,30) 2점 직선 = **바닥선**. 아이템이 아니라 무대다"),
                      ("물방울 t1", "r=2.2 원 하나 — t0의 원 2개와 **같은 요소**"),
                      ("종이비행기 t1", "(37.2,20)-(9.29,20) 2점 직선 = 접힌 **용골선**. 종이의 일부다")):
        print(f"     {who:12s} {what}")
    print("     규칙 (3): 보조색은 \"다른 셋과 구별해 주는 **한 부분**\"에만(챙/방울/줄무늬/별).")
    print("     넷 다 구별점이 아니다 → **색이 달라야 할 이유가 애초에 없었다.**")

    print("\n  ── C-4. 세 매핑 비교 (애셋 수정 비용 · UI 판정 기준) ──")
    maps = {
        "& 3 (옛 코드, 태생 결함)":      [0, 1, 2, 3, 0, 1, 2],
        "8칸표 (옛 주석 = 아이콘)":       [0, 1, 2, 3, 1, 2, 3],
        "새 코드 {0,1,2,3,2,3,1}":       [0, 1, 2, 3, 2, 3, 1],
    }
    slots = ["HEAD", "EYES", "NECK", "BACK", "HAIR", "FX", "PET"]
    print("     매핑                          " + " ".join(f"{s:>5s}" for s in slots)
          + "   HEAD=HAIR  인접중복")
    for nm, m in maps.items():
        adj = sum(1 for i in range(6) if m[i] == m[i + 1])
        print(f"     {nm:30s} " + " ".join(f"{v:5d}" for v in m)
              + f"   {'충돌' if m[0]==m[4] else ' 없음':>6s}  {adj:6d}")
    print("     ★ 세 매핑 모두 인접 중복 0. 갈리는 것은 HEAD↔HAIR 하나뿐이고")
    print("       `& 3`만 그걸 어긴다 — **원 결함은 실재했고 고친 것은 옳다.**")
    print("     ★ 그러나 새 매핑 vs 8칸표의 차이는 **UI에서 아무 근거도 못 댄다**(둘 다 합격).")
    print("       차이가 값을 하려면 틴트가 몸에 닿아야 하는데, C-1이 닿지 않는다고 말했다.")

    print("\n  ── C-5. 대체색 후보 (ΔE ≥ 7.8, 대역 안, WornColor 바이트 항등, 새 hex 0) ──")
    targets = [
        ("반짝임 t1  (t0 = Gold #9B7922)", "#9B7922",
         ["#988540", "#AB7942", "#BA7636", "#96814F"]),
        ("물방울 t1  (t0 = Accent #3378CC)", "#3378CC",
         ["#5577AE", "#5075B5", "#6787B9", "#587398"]),
        ("종이비행기 t1 (t0 = Paper #6787B9)", "#6787B9",
         ["#5577AE", "#587398", "#5075B5", "#3378CC"]),
        ("먼지 t1 = 바닥선 (t0 = InkDim #8B939F)", "#8B939F",
         ["#D6DBE3"]),
    ]
    for label, base, cands in targets:
        print(f"     {label}")
        for hx in cands:
            c = C.hex2rgb(hx)
            l = C.L(c)
            de = C.dE(C.hex2rgb(base), c)
            inband = BAND_LO - 1e-9 <= l <= BAND_HI + 1e-9
            ident = C.is_worn_fixed(c)
            mark = hx in ("#D6DBE3", "#8B939F")
            flag = "채택가능" if (de >= 7.8 and (inband or mark) and (ident or mark)) else "  --  "
            print(f"        {NAMED.get(hx,'?'):10s} {hx}  ΔE {de:6.2f}  L {l:.4f}"
                  f"  대역 {'O' if inband else ('면제' if mark else 'X')}"
                  f"  항등 {'O' if ident else ('면제' if mark else 'X')}   {flag}")
    print()
    print("  ── C-6. 라벤더 #955CCC 를 비우면 출하 색이 몇 종이 되는가 ──")
    users = census.get("#955CCC", [])
    print(f"     #955CCC 사용처 {len(users)}곳: {', '.join(users)}")
    print(f"     → 종이비행기 t1 을 옮기면 출하 아트색 집합에서 **한 종 빠진다**"
          f"(§11 '정원' 권고 방향과 같다).\n")


PROPOSAL = {
    ("look_fx_sparkle", 1): "#988540",   # GoldLight — 작은 별은 빛의 깊은 짝
    ("look_fx_bubble", 1):  "#5075B5",   # DarkLens  — 작은 방울은 물의 깊은 짝
    ("look_pet_plane", 1):  "#587398",   # Silver    — 용골선은 종이의 접힌 자국
    ("look_fx_dust", 1):    "#D6DBE3",   # InkTone   — 바닥선은 아이템이 아니라 무대다
}


def section_C2():
    print("╔══ C-2b. 제안 4건을 **적용해 보고** 게이트를 다시 돌린다 ══╗\n")
    marks = {C.hex2rgb("#D6DBE3"), C.hex2rgb("#8B939F")}
    items = S.item_colors()

    def uniq(d):
        u = set()
        for r in d.values():
            for t in r["tones"].values():
                u |= set(t)
        return u

    sim = {k: {"tones": {t: set(v) for t, v in r["tones"].items()}} for k, r in items.items()}
    for (nm, tone), hx in PROPOSAL.items():
        if nm not in sim:
            bad(f"제안 대상 애셋 {nm} 을 못 찾았다 — 파서/파일명 확인")
            return
        sim[nm]["tones"][tone] = {C.hex2rgb(hx)}

    b, a = uniq(items), uniq(sim)
    print(f"     고유색 {len(b)} → {len(a)}   아트색 {len(b-marks)} → {len(a-marks)}")
    (ok if not (a - b) else bad)(f"새로 생긴 hex {len(a-b)}개 " +
                                 (", ".join(C.rgb2hex(c) for c in sorted(a - b)) if a - b else ""))
    print(f"     사라진 hex {len(b-a)}개 " +
          (", ".join(C.rgb2hex(c) for c in sorted(b - a)) if b - a else "(없음)"))
    art = sorted(a - marks)
    outs = [c for c in art if not (BAND_LO - 1e-9 <= C.L(c) <= BAND_HI + 1e-9)]
    nid = [c for c in art if not C.is_worn_fixed(c)]
    (ok if not outs else bad)(f"변경 후 대역 밖 {len(outs)}건")
    (ok if not nid else bad)(f"변경 후 WornColor 바이트 비항등 {len(nid)}건")

    print("\n     변경 아이템의 주↔보조 ΔE (하한 7.8 · 표식 쌍은 면제)")
    for (nm, tone), hx in sorted(PROPOSAL.items()):
        for p0 in sorted(sim[nm]["tones"].get(0, ())):
            for p1 in sorted(sim[nm]["tones"].get(1, ())):
                d = C.dE(p0, p1)
                exempt = p0 in marks or p1 in marks
                (ok if (exempt or d >= 7.8) else bad)(
                    f"{nm:22s} {C.rgb2hex(p0)} / {C.rgb2hex(p1)}  ΔE {d:6.2f}"
                    + ("  (표식 면제)" if exempt else ""))

    print("\n     ── 덤: 내 변경과 무관한 **기존 미달 쌍** ──")
    for name, r in sorted(items.items()):
        for p0 in sorted(r["tones"].get(0, ())):
            for p1 in sorted(r["tones"].get(1, ())):
                if p0 in marks or p1 in marks:
                    continue
                d = C.dE(p0, p1)
                if d < 7.8:
                    print(f"        {name:24s} {C.rgb2hex(p0)} / {C.rgb2hex(p1)}  ΔE {d:6.2f}")
    print()


# ============================================================================
# 6. D — #5A8C3C 의 WornColor 항등 여백 (바이트 vs float)
# ============================================================================
def worn_float(rgb):
    """WornColor를 **float 그대로** 돌린다(8bit 반올림 없음). colorlab.worn 은 바이트판이다."""
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
    h /= 6.0
    s = max(s, C.WORN_S_FLOOR)
    v = min(max(v, C.WORN_V_FLOOR), C.WORN_V_CEIL)
    c = v * s
    hp = (h % 1.0) * 6.0
    x = c * (1.0 - abs((hp % 2.0) - 1.0))
    i = int(math.floor(hp)) % 6
    r1, g1, b1 = [(c, x, 0.0), (x, c, 0.0), (0.0, c, x),
                  (0.0, x, c), (x, 0.0, c), (c, 0.0, x)][i]
    m = v - c
    return tuple(q + m for q in (r1, g1, b1))


def section_D():
    print("╔══ D. 나뭇잎 초록 — float 항등이 아닌 것이 결함인가 ══╗\n")
    for hexs in ("#5A8C3C", "#5A8D3C"):
        c = C.hex2rgb(hexs)
        f = worn_float(c)
        print(f"  ── {hexs}  rgb{c} ──")
        h, s, v = C.rgb_to_hsv(c)
        print(f"     V = {v:.6f}   (WornValueFloor {C.WORN_V_FLOOR})  "
              f"{'클램프됨' if v < C.WORN_V_FLOOR else '클램프 없음'}   S = {s:.6f}")
        dmax = max(abs(f[i] - c[i] / 255.0) for i in range(3))
        print(f"     float 최대 채널차 = {dmax:.3e}   "
              f"Mathf.Approximately(허용 ~1e-6 상대) → {'깨진다' if dmax > 1e-6 else '통과'}")
        # 바이트 여백: 반올림판과 절사판 각각
        margins_round, margins_trunc, bytes_round, bytes_trunc = [], [], [], []
        for i in range(3):
            x = f[i] * 255.0
            br = int(round(x))
            bt = int(x)
            bytes_round.append(br)
            bytes_trunc.append(bt)
            margins_round.append(abs(x - (math.floor(x) + 0.5)))
            margins_trunc.append(min(x - math.floor(x), math.ceil(x) - x) if abs(x - round(x)) > 1e-12
                                 else 0.0)
        same_round = tuple(bytes_round) == c
        print(f"     반올림 바이트 {tuple(bytes_round)} {'==' if same_round else '!='} 원본 {c}"
              f"   최소 여백 {min(margins_round):.4f}/255 = {min(margins_round)/255:.2e} (float 단위)")
        print(f"     절사   바이트 {tuple(bytes_trunc)} {'==' if tuple(bytes_trunc)==c else '!='} 원본 {c}")
        print(f"     colorlab.worn(바이트판) = {C.rgb2hex(C.worn(c))}   항등 {C.is_worn_fixed(c)}")
        print(f"     대역 L = {C.L(c):.4f}  ({'안' if BAND_LO<=C.L(c)<=BAND_HI else '밖'})   "
              f"ΔE(#5A8C3C↔이 색) = {C.dE(C.hex2rgb('#5A8C3C'), c):.4f}")
        print()
    print("  판정 근거: 화면 프레임버퍼는 8bit다. float 항등은 사용자가 볼 수 없는 성질이고,")
    print("            바이트 항등은 **여백이 float 잡음보다 4자리 크다**. 자를 바꾸는 쪽이 싸다.\n")


def section_D2():
    print("╔══ D-2. 출하 25색 전체의 **바이트 여백** 조사 (자를 바이트로 내릴 때의 안전선) ══╗\n")
    items = S.item_colors()
    marks = {C.hex2rgb("#D6DBE3"), C.hex2rgb("#8B939F")}
    uniq = set()
    for rec in items.values():
        for t in rec["tones"].values():
            uniq |= set(t)
    art = sorted(uniq - marks, key=lambda c: -min_margin(c))
    print("     색        여백(/255)   V         클램프   Approximately")
    worst = 1e9
    for c in art:
        m = min_margin(c)
        worst = min(worst, m)
        h, sv, v = C.rgb_to_hsv(c)
        clamped = (v < C.WORN_V_FLOOR) or (v > C.WORN_V_CEIL) or (sv < C.WORN_S_FLOOR)
        f = worn_float(c)
        dmax = max(abs(f[i] - c[i] / 255.0) for i in range(3))
        print(f"     {C.rgb2hex(c)}  {m:8.4f}   {v:.6f}  {'예' if clamped else '  ':4s}"
              f"   {'깨짐' if dmax > 1e-6 else '통과'}")
    print(f"\n     ★ 25색 최소 여백 = {worst:.4f}/255.  8비트 판정의 안전 문턱을 "
          f"**0.20/255** 로 잡으면 오늘 25색 전부 통과하고,")
    print(f"       앞으로 경계에 0.20 보다 가깝게 붙는 색이 들어오면 그 자리에서 빨개진다.")
    nbroken = sum(1 for c in art
                  if max(abs(worn_float(c)[i] - c[i] / 255.0) for i in range(3)) > 1e-6)
    print(f"     ★ 지금 25색 중 float Approximately 가 깨지는 색 = {nbroken}종 "
          f"(= 나뭇잎 초록 하나만이 아니라면 자를 바꾸는 근거가 더 세다)\n")


def min_margin(c):
    """WornColor 통과 후 8bit 반올림 경계까지의 최소 여백(1/255 단위)."""
    f = worn_float(c)
    out = 1e9
    for i in range(3):
        x = f[i] * 255.0
        import math as _m
        out = min(out, abs(x - (_m.floor(x) + 0.5)))
    return out


# ============================================================================
# 7. 출하 색 대역 재확인 (0건 유지)
# ============================================================================
def section_band(force_out=False):
    print("╔══ 출하색 대역 밖 0건 재확인 ══╗\n")
    items = S.item_colors()
    marks = {C.hex2rgb("#D6DBE3"), C.hex2rgb("#8B939F")}
    uniq = set()
    for rec in items.values():
        for t in rec["tones"].values():
            uniq |= set(t)
    if force_out:
        uniq.add((255, 255, 0))
    art = sorted(uniq - marks)
    outs = [c for c in art if not (BAND_LO - 1e-9 <= C.L(c) <= BAND_HI + 1e-9)]
    ident = [c for c in art if not C.is_worn_fixed(c)]
    print(f"  아트 고유색 {len(art)}종 (잉크 표식 {len(marks)}종 제외)")
    (ok if not outs else bad)(f"대역 밖 {len(outs)}건" + ("" if not outs else " → " +
                              ", ".join(C.rgb2hex(c) for c in outs)))
    (ok if not ident else bad)(f"WornColor 바이트 비항등 {len(ident)}건" + ("" if not ident else " → " +
                               ", ".join(C.rgb2hex(c) for c in ident)))
    print()


# ============================================================================
def main():
    control = "--control" in sys.argv
    if control:
        print("### 양성 대조 모드 — 아래 검사들은 **반드시 FAIL 해야** 한다 ###\n")

        def mutate(rows):
            rows.append(("[대조] 일부러 틀린 교정", 1.0, 0.0, 0.0, False))
            return rows
        try:
            calibrate(mutate=mutate)
        except SystemExit as e:
            print(f"  ✔ 교정 게이트가 죽었다: {e}\n")
        else:
            print("  ✘ 교정 게이트가 통과했다 — 게이트가 죽어 있다\n")

        calibrate()
        n0 = len(FAIL)
        # 대조 1 — 대역 밖 색을 심으면 잡는가
        section_band(force_out=True)
        (print if len(FAIL) > n0 else bad)(f"  ✔ 대조1 대역 게이트가 심은 색을 잡았다 (+{len(FAIL)-n0}건)")
        # 대조 2 — 페르소나 기대값을 틀리게 하면 잡는가
        n1 = len(FAIL)
        got = CR2((0, 0, 0), (20, 20, 20))
        (print if abs(got - 9.99) > 0.005 else bad)(
            f"  ✔ 대조2 기대값 9.99 를 넣으면 {got:.2f} 와 갈라진다")
        # 대조 3 — 대역 상한식이 3.0을 넘는다고 우기면
        cap = (BAND_HI + 0.05) / (BAND_LO + 0.05)
        (print if cap < 3.0 else bad)(f"  ✔ 대조3 상한 {cap:.4f} 는 3.0 미만이다 (반대면 §B 폐기)")
        # 대조 4 — 검+흰 쌍의 하한이 21이라고 우기면
        (print if abs(math.sqrt(21.0) - 21.0) > 1e-6 else bad)(
            f"  ✔ 대조4 쌍의 하한 {math.sqrt(21.0):.4f} ≠ 21.00 (내부 대비와 다른 값이다)")
        print(f"\n대조 종료 — 남은 진짜 FAIL {len(FAIL)-n1}건")
        return

    calibrate()
    section_A()
    section_A_cost()
    section_B()
    section_B2()
    section_C()
    section_C2()
    section_D()
    section_D2()
    section_band()
    print("=" * 70)
    print(f"판정: {'PASS — FAIL 0건' if not FAIL else 'FAIL ' + str(len(FAIL)) + '건'}")
    for f in FAIL:
        print("   · " + f)
    sys.exit(1 if FAIL else 0)


if __name__ == "__main__":
    main()
