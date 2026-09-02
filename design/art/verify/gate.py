# -*- coding: utf-8 -*-
"""★ 팔레트 게이트 — 정본 토큰이 **지금도** 규칙을 지키는가 (design-art, 2026-09-02)

`design/art/PALETTE_SPEC.md`의 값과 이 파일의 값은 **같아야 한다**.
토큰을 고쳤으면 이것을 다시 돌려라. 위반 0건이어야 한다.

    python3 gate.py
    python3 gate.py --control    # 양성 대조: 일부러 나쁜 값 -> 게이트가 빨간불을 내는가

★ 이 게이트는 '없음'을 판정한다. TEAM.md §4-4에 따라 **모든 "없음" 판정에 양성 대조**를 붙인다.
"""
import sys

import colorlab as C

W, K = (255, 255, 255), (0, 0, 0)

# ---- 유도 상수 ------------------------------------------------------------
#: ★ 아트 자립 대역 — band.py가 **네 배경**에서 유도한다(흰/검 바탕화면 + 종이/목탄 무대).
#   구속하는 것은 극단이 아니라 두 무대다: 종이가 위(0.2396), 목탄이 아래(0.1632)를 막는다.
#   내 첫 유도는 극단만 봐서 [0.1000, 0.3000]이었고, 그 대역에서 고른 보조색이
#   종이 무대에서 2.48:1로 미달했다 — 대역 자체를 다시 유도했다.
ART_L_LO, ART_L_HI, _BD_ROWS = __import__("band").limits()
CHROME_L_FLOOR = 1.05 / 3.0 - 0.05    # 0.3000 — 크롬은 이 위. 사이(0.24~0.30)는 비워 둔다
DE_ID = 48.6        # 식별 하한  (UiChrome.CategoryTint 최근접 쌍)
DE_DISC = 7.8       # 변별 하한  (UiChrome.TintWash(CardSurface) 최근접 쌍)

MIN_TEXT = 4.5      # UiChrome.MinTextContrast
MIN_NONTEXT = 3.0   # UiChrome.MinNonTextContrast
FACE_TARGET = 3.60  # UiChrome.ControlFaceContrastTarget
FACE_INK_TARGET = 5.175  # UiChrome.ControlInkContrastTarget

# ---- 표면 -----------------------------------------------------------------
BACKDROPS = __import__("band").BACKDROPS   # 흰/검 바탕화면 + 종이/목탄 무대

SURFACES = {
    "우리 PanelSurface #14171C": "#14171C",
    "우리 CardSurface  #1B1F26": "#1B1F26",
    "우리 SubtleSurface #191D24": "#191D24",
    "핸드오프 창  #0D0C0B": "#0D0C0B",
    "핸드오프 카드 #161311": "#161311",
    "핸드오프 활성 #17130E": "#17130E",
    "핸드오프 착용 #1D1813": "#1D1813",
    "핸드오프 칩  #1B1713": "#1B1713",
}

# ---- 정본 토큰 ------------------------------------------------------------
#: DLC 6팩. (팩명, 색상각, 주색, 보조색)  ★ 그늘색은 FillOutlineColor(x0.62) 파생 — 값을 적지 않는다.
PACKS = [
    ("오피스 워커",      222.0, "#456ECC", "#5C709E"),
    ("사이버 아포칼립스", 172.0, "#009682", "#518C84"),
    ("네온 낙서",        312.0, "#CC1BA9", "#9C5A8E"),
    ("스포츠",      8.0, "#CC3F29", "#9E655C"),
    ("컬러 잉크",        268.0, "#8D56CC", "#8563AB"),
    ("밀리터리",          80.0, "#639400", "#798C51"),
]

#: 등급 — 카드 크롬 전용. 색상각 40도(황동) 하나에 밝기·채도만 오른다.
RANKS = [("일반", "#9C978C"), ("희귀", "#BCAC8B"), ("영웅", "#DBBD7F"), ("전설", "#F9CB70")]

BRASS = "#C8A15A"          # 1차 조작면
BRASS_SECOND = "#A08148"   # 2차 조작면 (브라스 x0.80)
ON_BRASS = "#0D0C0B"       # 브라스 면 위 글자
INK_ONE = "#948A7C"        # 핸드오프 잉크 2종을 합친 한 단

MARKERS = ["#D6DBE3", "#8B939F"]   # ItemCatalog.InkTone / InkDimTone


def q(h):
    return C.hex2rgb(h)


class Gate:
    def __init__(self):
        self.fail = 0
        self.n = 0

    def check(self, ok, name, note=""):
        self.n += 1
        if not ok:
            self.fail += 1
        print(f"  {'PASS' if ok else '★FAIL':>6s}  {name:52s} {note}")
        return ok


def run(packs=PACKS, ranks=RANKS, ink=INK_ONE, brass2=BRASS_SECOND):
    g = Gate()
    prim = [(n, q(p)) for n, _, p, _ in packs]
    sub = [(n, q(s)) for n, _, _, s in packs]
    art = prim + sub

    print("\n[A] 아트 대역 — 몸과 이펙트는 임의의 바탕화면 위에 그려진다")
    for n, c in art:
        l = C.L(c)
        g.check(ART_L_LO <= l <= ART_L_HI, f"A1 {n} 자립 대역 L∈[{ART_L_LO:.2f},{ART_L_HI:.2f}]",
                f"L={l:.4f} 흰{C.CR(c, W):.2f} 검{C.CR(c, K):.2f}")
    for n, c in art:
        w = min((C.CR(c, b), nm) for nm, b in BACKDROPS)
        g.check(w[0] >= MIN_NONTEXT,
                f"A2 {n} 배경 4종 전부 >= {MIN_NONTEXT}",
                f"최악 {w[0]:.2f}:1 ({w[1]})")
    for n, c in art:
        g.check(C.worn(c) == c, f"A3 {n} WornColor 항등(카드색 == 몸색)",
                f"{C.rgb2hex(c)} -> {C.rgb2hex(C.worn(c))}")
    for n, c in art:
        d = min(C.dE(c, q(m)) for m in MARKERS)
        g.check(d >= 10.0, f"A4 {n} 잉크 표식색과 ΔE >= 10", f"최소 {d:.1f}")

    lspread = max(C.L(c) for _, c in art) / max(1e-9, min(C.L(c) for _, c in art))
    lcr = ((max(C.L(c) for _, c in art) + 0.05) / (min(C.L(c) for _, c in art) + 0.05))
    g.check(lcr <= 1.50, "A5 아트 12색이 **한 밝기 대역**에 있다(최명/최암 CR <= 1.50)",
            f"{lcr:.2f}:1  L {min(C.L(c) for _, c in art):.4f}~{max(C.L(c) for _, c in art):.4f}")

    print("\n[B] 팩 — 여섯 색이 한 상자 안에서 각도만 다르다")
    worst = min((C.dE(a[1], b[1]), a[0], b[0])
                for i, a in enumerate(prim) for b in prim[i + 1:])
    g.check(worst[0] >= DE_DISC, f"B1 팩 주색끼리 변별 ΔE >= {DE_DISC}",
            f"최소 {worst[0]:.1f} ({worst[1]} ↔ {worst[2]})")
    wsub = min((C.dE(a[1], b[1]), a[0], b[0])
               for i, a in enumerate(sub) for b in sub[i + 1:])
    g.check(wsub[0] >= DE_DISC, f"B2 팩 보조색끼리 변별 ΔE >= {DE_DISC}",
            f"최소 {wsub[0]:.1f} ({wsub[1]} ↔ {wsub[2]})")
    for (n, p), (_, s) in zip(prim, sub):
        g.check(C.dE(p, s) >= DE_DISC, f"B3 {n} 주↔보조 변별 ΔE >= {DE_DISC}",
                f"{C.dE(p, s):.1f}")
    for (n, _, p, s) in packs:
        hp, hs = C.hue_deg(q(p)), C.hue_deg(q(s))
        d = min((hp - hs) % 360, (hs - hp) % 360)
        g.check(d <= 6.0, f"B4 {n} 보조색은 **같은 색상각**(새 각도 금지)", f"차 {d:.1f}도")
    for (n, _, p, s) in packs:
        cp = (lambda a: (a[1] ** 2 + a[2] ** 2) ** 0.5)(C.lab(q(p)))
        cs = (lambda a: (a[1] ** 2 + a[2] ** 2) ** 0.5)(C.lab(q(s)))
        g.check(cs < cp, f"B5 {n} 보조색이 주색보다 **채도가 낮다**(같은 재질의 다른 부품)",
                f"C* {cp:.1f} -> {cs:.1f}")

    print("\n[C] 등급 — 카드 크롬 전용. 서열은 단조, 식별은 색이 지지 않는다")
    rc = [(n, q(h)) for n, h in ranks]
    ls = [C.L(c) for _, c in rc]
    g.check(all(ls[i] < ls[i + 1] for i in range(3)), "C1 휘도 단조 증가",
            " < ".join(f"{v:.4f}" for v in ls))
    greys = [min(range(256), key=lambda v: abs(C.L((v, v, v)) - l)) for l in ls]
    g.check(all(greys[i] < greys[i + 1] for i in range(3)),
            "C2 흑백으로 눌러도 단조(전색맹)", " < ".join(map(str, greys)))
    chroma = [(lambda a: (a[1] ** 2 + a[2] ** 2) ** 0.5)(C.lab(c)) for _, c in rc]
    g.check(all(chroma[i] < chroma[i + 1] for i in range(3)),
            "C3 채도 단조 증가(귀할수록 색이 짙다)",
            " < ".join(f"{v:.1f}" for v in chroma))
    hs = [C.hue_deg(c) for _, c in rc]
    span = max(hs) - min(hs)
    g.check(span <= 30.0, "C4 색상각 폭 <= 30도(한 가족 — 팩을 잡아먹지 않는다)",
            f"{min(hs):.1f}~{max(hs):.1f} ({span:.1f}도)")
    lo = min(C.CR(c, q(s)) for _, c in rc for s in SURFACES.values())
    g.check(lo >= MIN_TEXT, f"C5 등급 낱말 텍스트 >= {MIN_TEXT} (표면 {len(SURFACES)}종)",
            f"최악 {lo:.2f}:1")
    adj = [(C.dE(rc[i][1], rc[i + 1][1]), rc[i][0], rc[i + 1][0]) for i in range(3)]
    g.check(min(a[0] for a in adj) >= DE_DISC, f"C6 인접 등급 변별 ΔE >= {DE_DISC}",
            " / ".join(f"{a[1]}↔{a[2]} {a[0]:.1f}" for a in adj))
    pw = min((C.dE(r, p), rn, pn) for rn, r in rc for pn, p in art)
    g.check(pw[0] >= DE_DISC, f"C7 등급색 ↔ 팩 12색 변별 ΔE >= {DE_DISC}",
            f"최소 {pw[0]:.1f} ({pw[1]} ↔ {pw[2]})")
    mk = min(min(C.dE(c, q(m)) for m in MARKERS) for _, c in rc)
    g.check(mk >= 10.0, "C8 잉크 표식색과 ΔE >= 10", f"최소 {mk:.1f}")
    g.check(all(C.L(c) > CHROME_L_FLOOR for _, c in rc),
            f"C9 등급색은 전부 크롬 대역(L > {CHROME_L_FLOOR:.2f}) — 몸에 안 내려간다",
            f"최소 L {min(ls):.4f}")

    print("\n[D] 크롬 — 조작면과 그 위 글자")
    for nm, face, ink_ in (("1차(브라스)", BRASS, ON_BRASS), ("2차(브라스x0.80)", brass2, ON_BRASS)):
        fw = min(C.CR(q(face), q(s)) for s in SURFACES.values())
        g.check(fw >= FACE_TARGET, f"D1 {nm} 면 >= {FACE_TARGET} (표면 {len(SURFACES)}종)",
                f"최악 {fw:.2f}:1")
        iw = C.CR(q(face), q(ink_))
        g.check(iw >= FACE_INK_TARGET, f"D2 {nm} 위 글자 >= {FACE_INK_TARGET}", f"{iw:.2f}:1")
    g.check(C.dE(q(BRASS), q(brass2)) >= DE_DISC, f"D3 1차면 ↔ 2차면 변별 ΔE >= {DE_DISC}",
            f"{C.dE(q(BRASS), q(brass2)):.1f}")
    g.check(C.L(q(BRASS)) > CHROME_L_FLOOR, f"D4 브라스는 크롬 대역(L > {CHROME_L_FLOOR:.2f})",
            f"L={C.L(q(BRASS)):.4f}")
    bw = min((C.dE(q(BRASS), c), n) for n, c in art)
    g.check(bw[0] >= DE_DISC, f"D5 브라스 ↔ 아트 12색 변별 ΔE >= {DE_DISC}",
            f"최소 {bw[0]:.1f} ({bw[1]})")

    print("\n[E] 잉크 한 단 — 핸드오프 6단 중 하위 2단을 폐기하고 합친 값")
    iw = min(C.CR(q(ink), q(s)) for s in SURFACES.values())
    g.check(iw >= MIN_TEXT, f"E1 잉크 텍스트 >= {MIN_TEXT} (표면 {len(SURFACES)}종)",
            f"최악 {iw:.2f}:1")
    for dead in ("#6E665C", "#5C574E"):
        d = min(C.CR(q(dead), q(s)) for s in SURFACES.values())
        g.check(d < MIN_TEXT, f"E2 폐기 잉크 {dead}는 여전히 미달(폐기 사유가 살아 있는가)",
                f"최악 {d:.2f}:1")

    print("\n[F] 미보유 — opacity가 아니라 채도 0, 휘도 유지")
    for n, c in prim:
        gr = min(((v, v, v) for v in range(256)), key=lambda x: abs(C.L(x) - C.L(c)))
        lost = abs(C.CR(c, q("#1B1F26")) - C.CR(gr, q("#1B1F26")))
        g.check(lost <= 0.05, f"F1 {n} 무채화로 잃는 대비 <= 0.05",
                f"{C.rgb2hex(c)}->{C.rgb2hex(gr)} 손실 {lost:.3f}")

    print(f"\n검사 {g.n}건 / 위반 {g.fail}건")
    return g.fail


CONTROL = [
    ("팩 하나를 밝은 아이보리로 바꾼다(자립 대역 이탈)",
     lambda: [("오피스 워커", 222.0, "#E8E2D4", "#5C709E")] + PACKS[1:], None, None, None),
    ("팩 보조색을 다른 색상각으로 옮긴다",
     lambda: [("오피스 워커", 222.0, "#456ECC", "#5C9E70")] + PACKS[1:], None, None, None),
    ("★ 첫 유도(흑·백만)로 고른 보조색으로 되돌린다 — 종이 무대 2.48:1",
     lambda: [("오피스 워커", 222.0, "#456ECC", "#7690CC")] + PACKS[1:], None, None, None),
    ("등급 램프에 단조 역전을 심는다", None,
     lambda: [("일반", "#BCAC8B"), ("희귀", "#9C978C"), ("영웅", "#DBBD7F"), ("전설", "#F9CB70")],
     None, None),
    ("잉크를 핸드오프 원본 #6E665C로 되돌린다", None, None, "#6E665C", None),
    ("2차 조작면을 핸드오프 #17130E로 되돌린다(면 1.00 결함)", None, None, None, "#17130E"),
]


def control():
    print("\n" + "=" * 78)
    print("★ 양성 대조 — 일부러 나쁜 값을 넣었을 때 게이트가 실제로 빨간불을 내는가")
    print("=" * 78)
    base = run()
    if base != 0:
        sys.exit("본안이 이미 빨간불이다 — 대조 이전에 그것부터 고쳐라.")
    ok = True
    for name, pf, rf, inkv, b2 in CONTROL:
        print(f"\n--- 대조: {name} ---")
        n = run(packs=pf() if pf else PACKS, ranks=rf() if rf else RANKS,
                ink=inkv or INK_ONE, brass2=b2 or BRASS_SECOND)
        caught = n > 0
        ok &= caught
        print(f"  판정: {'정상 — 게이트가 잡았다' if caught else '★대조 실패 — 게이트에 구멍이 있다'}")
    print("\n" + "=" * 78)
    print("대조 전건 '게이트가 잡았다'" if ok else "★ 대조 실패 — 이 게이트의 0건 판정을 전부 폐기하십시오")
    print("=" * 78)
    return 0 if ok else 1


if __name__ == "__main__":
    C.calibrate()
    if "--control" in sys.argv:
        sys.exit(control())
    print("=" * 78)
    print("팔레트 게이트 — design/art/PALETTE_SPEC.md 정본 토큰")
    print("=" * 78)
    sys.exit(1 if run() else 0)
