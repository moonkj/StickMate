# -*- coding: utf-8 -*-
"""§4~§8  팩 색상환 · 등급 램프 · 브라스 · 잉크 처방 검산 (design-art, 2026-09-02)

    python3 packs.py
    python3 packs.py --control     # ★ 양성 대조: 일부러 나쁜 값 -> 게이트가 빨간불을 내는가

여기서 정하는 상수는 전부 **측정에서 유도**된다. 취향으로 고른 값은 그렇다고 적는다.
"""
import sys

import colorlab as C
import shipped as S

W, K = (255, 255, 255), (0, 0, 0)

# ---------------------------------------------------------------------------
# 유도 상수 (box.py §3에서 나온다)
#   흰 바탕 CR>=X  <=>  L <= 1.05/X - 0.05
#   검은 바탕 CR>=X <=>  L >= 0.05X - 0.05
#   두 식이 만나는 점 L* = sqrt(1.05*0.05) - 0.05 = 0.17910  ->  양쪽 4.5826:1
# ---------------------------------------------------------------------------
SELF_L_IDEAL = (1.05 * 0.05) ** 0.5 - 0.05
SELF_L_LO = 0.05 * 3.0 - 0.05          # 0.1000  검은 바탕 3.0
SELF_L_HI = 1.05 / 3.0 - 0.05          # 0.3000  흰 바탕 3.0
CHROME_L_FLOOR = SELF_L_HI             # 크롬은 이 위, 아트는 이 아래

BRASS = C.hex2rgb("#C8A15A")
OUR_CARD = C.hex2rgb("#1B1F26")        # UiChrome.CardSurface
OUR_PANEL = C.hex2rgb("#14171C")       # UiChrome.PanelSurface
HO_CARD = C.hex2rgb("#161311")         # 핸드오프 카드
HO_WIN = C.hex2rgb("#0D0C0B")          # 핸드오프 창

INK_TONE = C.hex2rgb("#D6DBE3")        # ItemCatalog.InkTone   (표식색 — 피해야 한다)
INK_DIM = C.hex2rgb("#8B939F")         # ItemCatalog.InkDimTone(표식색 — 피해야 한다)


def sec(t):
    print("\n" + "=" * 78 + f"\n{t}\n" + "=" * 78)


def best_in_box(hue_deg, l_target=SELF_L_IDEAL):
    """이 색상각에서 **상자 안**(S>=0.42, V 0.55~0.80)이면서 흑·백 양쪽 대비가 가장 큰 색.
    동률이면 채도(LAB C*)가 큰 쪽. 상자를 1% 격자로 전수 탐색한다(근사가 아니라 전수)."""
    best = None
    for si in range(42, 101):
        for vi in range(55, 81):
            c = C.hsv_to_rgb(hue_deg / 360.0, si / 100.0, vi / 100.0)
            m = min(C.CR(c, W), C.CR(c, K))
            la = C.lab(c)
            chroma = (la[1] ** 2 + la[2] ** 2) ** 0.5
            key = (round(m, 3), round(chroma, 1))
            if best is None or key > best[0]:
                best = (key, c, m, chroma)
    return best[1], best[2], best[3]


def grey_of_luminance(target):
    """상대휘도가 target인 무채색(8bit 반올림 후 가장 가까운 것)."""
    return min(((v, v, v) for v in range(256)), key=lambda g: abs(C.L(g) - target))


# ===========================================================================
def s4_floor():
    sec("§4  '다르게 보인다'의 하한 **두 개**를 우리가 이미 출하한 색에서 뽑는다")
    print("  색을 구별하는 일에는 난이도가 다른 두 가지가 있다. 하나의 숫자로 둘 다 판정하면 틀린다.")
    print("    · 식별 — 옆에 비교 대상이 없다. '이 색이 어느 팩인가?' (어렵다)")
    print("    · 변별 — 나란히 있다. '이 둘이 다른가?' (쉽다)")
    print("  둘 다 우리가 **이미 출하해서 통과시킨** 색에서 뽑는다. 내가 발명한 숫자가 아니다.\n")

    tints = {"TintHead #E8834A": "#E8834A", "TintEyes #4FC0C6": "#4FC0C6",
             "TintNeck #8CC06E": "#8CC06E", "TintBack #B08FD0": "#B08FD0"}
    ks = list(tints)
    worst = None
    print("  [식별] UiChrome.CategoryTint 4종 — 카테고리 하나만 보고 무엇인지 알아야 한다")
    for i in range(len(ks)):
        for j in range(i + 1, len(ks)):
            a, b = C.hex2rgb(tints[ks[i]]), C.hex2rgb(tints[ks[j]])
            d = C.dE(a, b)
            print(f"    ΔE {d:6.1f}   {ks[i]:18s} ↔ {ks[j]}")
            if worst is None or d < worst[0]:
                worst = (d, ks[i], ks[j])
    id_floor = worst[0]
    print(f"    ★ 식별 하한 ΔE_ID = {id_floor:.1f}  ({worst[1]} ↔ {worst[2]})")

    print("\n  [변별] UiChrome.TintWash(tint) = 같은 4색 알파 30/255를 CardSurface 위에 합성한 값 —")
    print("         착용 카드 썸네일 배경. 그리드에서 **나란히** 놓인다.")
    wash = {}
    for k, v in tints.items():
        t = C.hex2rgb(v)
        a = 30 / 255.0
        wash[k] = tuple(round(t[i] * a + OUR_CARD[i] * (1 - a)) for i in range(3))
    wworst = None
    for i in range(len(ks)):
        for j in range(i + 1, len(ks)):
            d = C.dE(wash[ks[i]], wash[ks[j]])
            if wworst is None or d < wworst[0]:
                wworst = (d, ks[i], ks[j])
    for k in ks:
        print(f"    {k:18s} -> wash {C.rgb2hex(wash[k])}")
    disc_floor = wworst[0]
    print(f"    ★ 변별 하한 ΔE_DISC = {disc_floor:.1f}  ({wworst[1]} ↔ {wworst[2]})")
    print("\n  → 팩색(스와치 6칸이 나란히 선다)·팩 내부 주/보조는 변별. 등급 리본만 보고 등급을 맞히는 것은 식별.")
    return id_floor, disc_floor


PACKS = [
    # (팩명, 색상각, 의미)  ★ 색상각은 의미로 고르고, S/V는 §3의 자립 최적화가 정한다.
    ("오피스 워커",     222.0, "군청 — 사무용 잉크/정장"),
    ("사이버 아포칼립스", 172.0, "독성 청록 — 형광 배선"),
    ("네온 낙서",       312.0, "마젠타 — 스프레이"),
    ("스포츠",    8.0, "주홍 — 유니폼/트랙"),
    ("컬러 잉크",       268.0, "보라 잉크 (★ 유저가 색상각을 돌린다. 이 값은 상점 표시 기본값)"),
    ("밀리터리",        80.0, "올리브 — 캔버스/야전"),
]


def s5_wheel(idf, disc):
    sec("§5  DLC 6팩 색상환 — '하나의 상자, 여섯 개의 각도'")
    print(f"  규칙: 색상각만 다르다. S/V는 전부 같은 자를 쓴다 —")
    print(f"        상자 안(S>=0.42, V 0.55~0.80) + 흑·백 양쪽 대비 최대화.")
    print(f"  목표 휘도 L* = {SELF_L_IDEAL:.4f} (흰·검 양쪽 {1.05/(SELF_L_IDEAL+0.05):.2f}:1로 같아지는 점)\n")
    print(f"  {'팩':14s} {'H':>6s} {'주색':>9s} {'L':>7s} {'vs흰':>6s} {'vs검':>6s} "
          f"{'항등?':>6s} {'우리카드':>8s} {'HO카드':>7s}")
    rows = []
    for name, h, why in PACKS:
        c, m, chroma = best_in_box(h)
        ident = "예" if C.worn(c) == c else "★아니오"
        rows.append((name, h, c, why))
        print(f"  {name:14s} {h:6.1f} {C.rgb2hex(c):>9s} {C.L(c):7.4f} "
              f"{C.CR(c, W):6.2f} {C.CR(c, K):6.2f} {ident:>6s} "
              f"{C.CR(c, OUR_CARD):8.2f} {C.CR(c, HO_CARD):7.2f}")
    print("\n  -- 팩 사이 거리 --")
    worst = None
    for i in range(len(rows)):
        for j in range(i + 1, len(rows)):
            d = C.dE(rows[i][2], rows[j][2])
            if worst is None or d < worst[0]:
                worst = (d, rows[i][0], rows[j][0])
    print(f"     최소 ΔE {worst[0]:.1f}  ({worst[1]} ↔ {worst[2]})")
    print(f"       변별(스와치 6칸이 나란히 선다) 하한 {disc:.1f}  "
          f"{'PASS' if worst[0] >= disc else 'FAIL'}")
    print(f"       식별(색 하나만 보고 팩을 맞힌다) 하한 {idf:.1f}  "
          f"{'PASS' if worst[0] >= idf else '★FAIL'}")
    if worst[0] < idf:
        print(f"       → ★ 여섯 색을 한 휘도에 세우면 식별 하한을 **물리적으로 못 넘는다**.")
        print(f"         팩은 색만으로 식별될 수 없다 — 조형 모티프(design-equipment)와 이름이 함께 진다.")
        print(f"         색은 '확인'이지 '식별'이 아니다. 이건 처방이 아니라 측정 결과다.")

    print("\n  -- 보조색: 같은 색상각, **자립 대역의 밝은 끝**. 새 색상각을 만들지 않는다 --")
    print("     한 색상각에 3단 사다리가 선다:  그늘(x0.62 파생) < 주색(L*) < 보조색(자립 상한)")
    print(f"  {'팩':14s} {'주색':>9s} {'보조색':>9s} {'보조 L':>7s} {'ΔE(주↔보)':>10s} "
          f"{'CR(주↔보)':>10s} {'보조vs흰':>8s} {'보조vs검':>8s} {'항등?':>6s}")
    sub_worst = None
    subs = []
    for name, h, c, why in rows:
        # 보조색 = 같은 색상각 · 상자 안 · 자립 대역 상한(L<=0.300)에서 가장 밝은 것
        best = None
        for si in range(42, 101):
            for vi in range(55, 81):
                q = C.hsv_to_rgb(h / 360.0, si / 100.0, vi / 100.0)
                if C.L(q) > SELF_L_HI:
                    continue
                key = (round(C.L(q), 4), si)
                if best is None or key > best[0]:
                    best = (key, q)
        q = best[1]
        subs.append((name, q))
        d = C.dE(c, q)
        if sub_worst is None or d < sub_worst[0]:
            sub_worst = (d, name)
        print(f"  {name:14s} {C.rgb2hex(c):>9s} {C.rgb2hex(q):>9s} {C.L(q):7.4f} {d:10.1f} "
              f"{C.CR(c, q):10.2f} {C.CR(q, W):8.2f} {C.CR(q, K):8.2f} "
              f"{'예' if C.worn(q) == q else '★아니오':>6s}")
    print(f"     주↔보조 최소 ΔE {sub_worst[0]:.1f} ({sub_worst[1]})  변별 하한 {disc:.1f}  "
          f"{'PASS' if sub_worst[0] >= disc else '★FAIL — 한 팩 안에서 두 색이 구별 안 된다'}")
    # 보조색끼리도 팩을 가른다
    sw = None
    for i in range(len(subs)):
        for j in range(i + 1, len(subs)):
            d = C.dE(subs[i][1], subs[j][1])
            if sw is None or d < sw[0]:
                sw = (d, subs[i][0], subs[j][0])
    print(f"     보조색끼리 최소 ΔE {sw[0]:.1f} ({sw[1]} ↔ {sw[2]})  변별 하한 {disc:.1f}  "
          f"{'PASS' if sw[0] >= disc else 'FAIL'}")

    print("\n  -- 그늘색(FillOutlineColor x0.62)은 자립 대역을 벗어나는가 --")
    print(f"  {'팩':14s} {'주색':>9s} {'그늘':>9s} {'그늘 L':>8s} {'그늘vs검':>8s} {'주↔그늘':>8s}")
    for name, h, c, why in rows:
        sh = C.fill_outline(c)
        print(f"  {name:14s} {C.rgb2hex(c):>9s} {C.rgb2hex(sh):>9s} {C.L(sh):8.4f} "
              f"{C.CR(sh, K):8.2f} {C.CR(c, sh):8.2f}")
    print("     → 그늘선은 검은 바탕에서 3.0을 못 넘는다. 경계는 **채움**이 지고 그늘선은 장식이다.")
    return rows, subs


#: 후보 A — 장르 관습(회/청/보/금). ux-designer의 영웅 교정값 + 잉크 표식색을 피해 일반을 옮긴 값.
#   (핸드오프의 일반 #8A8F98은 ItemCatalog.InkDimTone #8B939F와 ΔE 2.4 — 사실상 같은 색이다.)
RANK_A = [("일반", "#9AA1AB"), ("희귀", "#6E9BE8"), ("영웅", "#C39BF5"), ("전설", "#E0B24A")]

#: 후보 B — **황동 함량 램프**. 색상각 하나(황동 40°)에 채도·명도만 오른다.
#   등급색은 '식별'이 아니라 '서열'을 진다. 서열은 단조 휘도로만 정직하게 표현된다.
RANK_B_HUE = 40.0
RANK_B_TARGET = [("일반", 0.28, 0.10), ("희귀", 0.40, 0.26),
                 ("영웅", 0.52, 0.42), ("전설", 0.64, 0.55)]   # (이름, 목표 L, 채도 S)


def solve_v(hue, s, target_l):
    """이 색상각·채도에서 상대휘도가 target_l에 가장 가까운 V(1/1000 격자)."""
    best = None
    for vi in range(0, 1001):
        c = C.hsv_to_rgb(hue / 360.0, s, vi / 1000.0)
        d = abs(C.L(c) - target_l)
        if best is None or d < best[0]:
            best = (d, c)
    return best[1]


def rank_check(label, cols, idf, disc, packrows, subs):
    print(f"\n[{label}]")
    print(f"  {'등급':6s} {'색':>9s} {'L':>7s} {'흑백':>5s} {'우리카드':>8s} {'HO카드':>7s} "
          f"{'표식ΔE':>7s} {'브라스ΔE':>8s}")
    for name, c in cols:
        mark = min(C.dE(c, INK_TONE), C.dE(c, INK_DIM))
        print(f"  {name:6s} {C.rgb2hex(c):>9s} {C.L(c):7.4f} "
              f"{grey_of_luminance(C.L(c))[0]:5d} {C.CR(c, OUR_CARD):8.2f} "
              f"{C.CR(c, HO_CARD):7.2f} {mark:7.1f} {C.dE(c, BRASS):8.1f}")
    ls = [C.L(c) for _, c in cols]
    mono = all(ls[i] < ls[i + 1] for i in range(3))
    lo_nontext = min(C.CR(c, OUR_CARD) for _, c in cols)
    lo_text = min(min(C.CR(c, OUR_CARD), C.CR(c, HO_CARD)) for _, c in cols)
    adj = [(C.dE(cols[i][1], cols[i + 1][1]), cols[i][0], cols[i + 1][0]) for i in range(3)]
    pworst = min((C.dE(rc, pc), rn, pn) for rn, rc in cols for pn, ph, pc, _ in packrows)
    sworst = min((C.dE(rc, sc), rn, sn) for rn, rc in cols for sn, sc in subs)
    mk = min(min(C.dE(c, INK_TONE), C.dE(c, INK_DIM)) for _, c in cols)
    br = min(C.dE(c, BRASS) for _, c in cols)
    r = [
        ("① 휘도 단조 증가", mono, f"L = {' < '.join(f'{v:.4f}' for v in ls)}"),
        ("② 리본 >= 3.0", lo_nontext >= 3.0, f"최악 {lo_nontext:.2f}:1 (우리 카드)"),
        ("③ 등급 낱말 >= 4.5", lo_text >= 4.5, f"최악 {lo_text:.2f}:1 (두 카드 통틀어)"),
        (f"④ 인접 ΔE >= {disc:.1f}(변별)", min(a[0] for a in adj) >= disc,
         " / ".join(f"{a[1]}↔{a[2]} {a[0]:.1f}" for a in adj)),
        (f"⑤ 팩 주색과 ΔE >= {disc:.1f}", pworst[0] >= disc,
         f"최소 {pworst[0]:.1f} ({pworst[1]} ↔ {pworst[2]})"),
        (f"⑥ 팩 보조색과 ΔE >= {disc:.1f}", sworst[0] >= disc,
         f"최소 {sworst[0]:.1f} ({sworst[1]} ↔ {sworst[2]})"),
        ("⑦ 잉크 표식색과 ΔE >= 10", mk >= 10.0, f"최소 {mk:.1f}"),
        (f"⑧ 브라스(조작면)와 ΔE >= {disc:.1f}", br >= disc, f"최소 {br:.1f}"),
        (f"⑨ 색만으로 **식별** ΔE >= {idf:.1f}", min(a[0] for a in adj) >= idf,
         f"인접 최소 {min(a[0] for a in adj):.1f} — 여기가 깨지면 등급은 색이 아니라 "
         f"칸 수/낱말이 져야 한다"),
    ]
    for name, ok, note in r:
        print(f"  {'PASS' if ok else '★FAIL':>6s}  {name:26s} {note}")
    return sum(1 for _, ok, _ in r if not ok)


def s6_rank(idf, disc, packrows, subs):
    sec("§6  등급 램프 — **카드 크롬 전용**. 몸에는 안 내려간다")
    print("  ★ 몸 위 등급 구분은 색으로 안 된다(§1). 카드에서만 색을 쓴다면 8가지를 동시에 만족해야 한다.")
    print("  후보 두 개를 같은 자로 잰다. 판정은 표가 한다.")

    print("\n  -- 먼저 핸드오프 원안 --")
    ho = [("일반", C.hex2rgb("#8A8F98")), ("희귀", C.hex2rgb("#6E9BE8")),
          ("영웅", C.hex2rgb("#B07BE0")), ("전설", C.hex2rgb("#E0B24A"))]
    nho = rank_check("핸드오프 원안", ho, idf, disc, packrows, subs)

    a = [(n, C.hex2rgb(h)) for n, h in RANK_A]
    na = rank_check("후보 A — 장르 관습 + ux 영웅 교정", a, idf, disc, packrows, subs)

    b = [(n, solve_v(RANK_B_HUE, s, l)) for n, l, s in RANK_B_TARGET]
    nb = rank_check("후보 B — 황동 함량 램프 (색상각 40° 하나)", b, idf, disc, packrows, subs)

    print(f"\n  위반 수: 핸드오프 {nho} / 후보A {na} / 후보B {nb}")
    return a, b


def s7_brass(packrows, rankcols):
    sec("§7  브라스 #C8A15A — 어디까지 받을 것인가")
    print(f"  브라스 L = {C.L(BRASS):.4f}.  아트 자립 대역 상한 L = {SELF_L_HI:.4f}.")
    print(f"  → 브라스는 아트 대역 **위**에 있다. 이 한 줄이 '크롬과 아트를 무엇이 가르는가'의 답이다.\n")
    print(f"  {'바탕':22s} {'CR':>7s}")
    for nm, bg in (("우리 CardSurface #1B1F26", OUR_CARD), ("우리 PanelSurface #14171C", OUR_PANEL),
                   ("핸드오프 카드 #161311", HO_CARD), ("핸드오프 창 #0D0C0B", HO_WIN)):
        print(f"  {nm:22s} {C.CR(BRASS, bg):7.2f}")
    print(f"\n  대조 — 지금 우리 강조색 #5DA1F5:")
    acc = C.hex2rgb("#5DA1F5")
    for nm, bg in (("우리 CardSurface", OUR_CARD), ("우리 PanelSurface", OUR_PANEL)):
        print(f"  {nm:22s} {C.CR(acc, bg):7.2f}")
    print(f"  브라스 위 글자 #160F06: {C.CR(BRASS, C.hex2rgb('#160F06')):.2f}:1  "
          f"(우리 OnAccentSolid #0B1016 위: {C.CR(BRASS, C.hex2rgb('#0B1016')):.2f}:1)")

    print(f"\n  -- 브라스가 아이템 색과 겹치는가 (출하 42종) --")
    items = S.item_colors()
    allc = sorted({c for v in items.values() for c in v["tones"].values() for c in c},
                  key=C.hue_deg)
    warm = [c for c in allc if C.hue_deg(c) < 60]
    near = sorted(((C.dE(c, BRASS), c) for c in allc))[:5]
    print(f"     출하 고유색 {len(allc)}종 중 색상각 0~60°(브라스 대역) {len(warm)}종")
    print(f"     브라스와 가장 가까운 5종:")
    for d, c in near:
        print(f"       ΔE {d:5.1f}  {C.rgb2hex(c)}  L={C.L(c):.4f}  "
              f"(자립 대역 안? {'예' if SELF_L_LO <= C.L(c) <= SELF_L_HI else '아니오'})")
    over = [c for c in allc if C.L(c) > SELF_L_HI]
    print(f"     ★ 출하 아이템색 {len(allc)}종 중 자립 대역을 **넘는**(L>{SELF_L_HI:.2f}) 것 {len(over)}종 — "
          f"이들은 밝은 바탕화면에서 3.0을 못 넘는다")
    for c in sorted(over, key=lambda x: -C.L(x))[:6]:
        print(f"       {C.rgb2hex(c)} L={C.L(c):.4f} 흰바탕 {C.CR(c, W):.2f}:1 "
              f"-> worn {C.rgb2hex(C.worn(c))} L={C.L(C.worn(c)):.4f} 흰바탕 {C.CR(C.worn(c), W):.2f}:1")

    print(f"\n  -- 브라스와 팩색/등급색의 거리 --")
    for nm, h, c, _ in packrows:
        print(f"     팩 {nm:14s} ΔE {C.dE(c, BRASS):6.1f}  CR {C.CR(c, BRASS):5.2f}")
    for nm, c in rankcols:
        print(f"     등급 {nm:12s} ΔE {C.dE(c, BRASS):6.1f}  CR {C.CR(c, BRASS):5.2f}")


def s8_ink():
    sec("§8  핸드오프 잉크 2종 — ux-designer 처방을 **다른 방법으로** 다시 잰다")
    print("  ux는 핸드오프 표면 10종 위에서 쟀다. 나는 **우리 표면 + 핸드오프 표면 + 잉크 양극**에서 잰다.")
    print("  (같은 방법으로 다시 재는 것은 검증이 아니다 — 같은 함정에 같이 빠진다.)\n")
    surfaces = {
        "핸드오프 창 #0D0C0B": HO_WIN, "핸드오프 카드 #161311": HO_CARD,
        "핸드오프 활성 #17130E": C.hex2rgb("#17130E"), "핸드오프 착용 #1D1813": C.hex2rgb("#1D1813"),
        "핸드오프 칩 #1B1713": C.hex2rgb("#1B1713"),
        "우리 Panel #14171C": OUR_PANEL, "우리 Card #1B1F26": OUR_CARD,
        "우리 Subtle #191D24": C.hex2rgb("#191D24"),
    }
    inks = {"라벨 #6E665C(현행)": "#6E665C", "비활성 #5C574E(현행)": "#5C574E",
            "ux 제안 #8A8073": "#8A8073", "ux 제안 #878073": "#878073",
            "우리 T3 #8B939F": "#8B939F", "우리 NonText #6C7480": "#6C7480"}
    print(f"  {'잉크':22s} " + " ".join(f"{k.split()[0][:6]:>7s}" for k in surfaces) + f" {'최악':>7s}")
    res = {}
    for kn, kh in inks.items():
        c = C.hex2rgb(kh)
        vals = [C.CR(c, s) for s in surfaces.values()]
        res[kn] = min(vals)
        print(f"  {kn:22s} " + " ".join(f"{v:7.2f}" for v in vals) + f" {min(vals):7.2f}")
    print("\n  하한 4.50 판정:")
    for k, v in res.items():
        print(f"    {k:22s} 최악 {v:5.2f}  {'PASS' if v >= 4.5 else 'FAIL'}")
    a, b = C.hex2rgb("#8A8073"), C.hex2rgb("#878073")
    print(f"\n  ★ ux 제안 두 값 사이 거리: ΔE {C.dE(a, b):.1f} / CR {C.CR(a, b):.2f}:1")
    print("     — 서로 다른 두 단으로 쓸 수 없다는 ux의 판정을 내 자로도 확인한다.")
    print(f"\n  내 제안(한 단으로 합치고 **우리 것으로 잡는다**):")
    for hx in ("#8B939F", "#8A8073", "#948A7C"):
        c = C.hex2rgb(hx)
        v = min(C.CR(c, s) for s in surfaces.values())
        print(f"    {hx}  최악 {v:5.2f}  브라스와 ΔE {C.dE(c, BRASS):5.1f}  "
              f"{'PASS' if v >= 4.5 else 'FAIL'}")


def s9_unowned():
    sec("§9  미보유 표시 — opacity 대신 **채도 0, 휘도 유지**")
    print("  ux H-3: 핸드오프의 opacity .34는 합성 후 1.40:1 (이 라운드 최악값).")
    print("  내 처방: 같은 휘도의 무채색으로 바꾼다. 휘도가 같으므로 **대비가 1도 안 떨어진다.**\n")
    print(f"  {'팩':14s} {'주색':>9s} {'L':>7s} {'우리카드CR':>10s} | {'무채화':>9s} {'L':>7s} "
          f"{'우리카드CR':>10s} {'ΔE':>6s}")
    for name, h, why in PACKS:
        c, _, _ = best_in_box(h)
        g = grey_of_luminance(C.L(c))
        print(f"  {name:14s} {C.rgb2hex(c):>9s} {C.L(c):7.4f} {C.CR(c, OUR_CARD):10.2f} | "
              f"{C.rgb2hex(g):>9s} {C.L(g):7.4f} {C.CR(g, OUR_CARD):10.2f} {C.dE(c, g):6.1f}")
    print("\n  -- 무채화 회색이 등급 '일반'과 헷갈리는가 (둘 다 회색이다) --")
    b = [(n, solve_v(RANK_B_HUE, s_, l)) for n, l, s_ in RANK_B_TARGET]
    normal = b[0][1]
    for name, h, why in PACKS:
        c, _, _ = best_in_box(h)
        g = grey_of_luminance(C.L(c))
        print(f"     미보유 {C.rgb2hex(g)} ↔ 등급 일반 {C.rgb2hex(normal)}  ΔE {C.dE(g, normal):5.1f}  "
              f"CR {C.CR(g, normal):4.2f}  (자리: 아이콘 58px vs 리본 2px)")
    print("\n  대조 — 핸드오프 방식(#6E665C를 카드 위에 opacity .34로 합성):")
    ho = C.hex2rgb("#6E665C")
    comp = tuple(round(ho[i] * 0.34 + HO_CARD[i] * 0.66) for i in range(3))
    print(f"    합성색 {C.rgb2hex(comp)}  카드 대비 {C.CR(comp, HO_CARD):.2f}:1")


def control():
    """양성 대조 — 게이트가 나쁜 값에 실제로 빨간불을 내는가."""
    sec("★ 양성 대조 — 일부러 나쁜 값을 넣는다")
    floor = 15.0
    cases = [
        ("등급 램프에 단조 역전을 심는다", ["#9AA1AB", "#C39BF5", "#5DA1F5", "#E0B24A"], "단조"),
        ("팩 두 개를 같은 색상각에 놓는다", None, "ΔE"),
        ("잉크를 #5C574E로 되돌린다", None, "대비"),
    ]
    # 1) 단조 역전
    cs = [C.hex2rgb(h) for h in cases[0][1]]
    ls = [C.L(c) for c in cs]
    mono = all(ls[i] < ls[i + 1] for i in range(3))
    print(f"  1) {cases[0][0]}: 단조 {'PASS(★대조 실패 — 게이트가 못 잡는다)' if mono else 'FAIL(정상: 게이트가 잡았다)'}"
          f"  L={[round(v,4) for v in ls]}")
    # 2) 같은 색상각 두 팩
    a, _, _ = best_in_box(222.0)
    b, _, _ = best_in_box(226.0)
    d = C.dE(a, b)
    print(f"  2) 오피스 222° 옆에 226°를 놓는다: ΔE {d:.1f} vs 하한 {floor:.1f} "
          f"{'FAIL(정상: 게이트가 잡았다)' if d < floor else 'PASS(★대조 실패)'}")
    # 3) 잉크
    c = C.hex2rgb("#5C574E")
    v = min(C.CR(c, HO_WIN), C.CR(c, HO_CARD), C.CR(c, OUR_CARD))
    print(f"  3) 잉크 #5C574E: 최악 {v:.2f} vs 4.50 "
          f"{'FAIL(정상: 게이트가 잡았다)' if v < 4.5 else 'PASS(★대조 실패)'}")
    # 4) 자립 대역 밖
    bright = C.hex2rgb("#E8E2D4")
    print(f"  4) 아이보리 #E8E2D4를 아트에 쓴다: L {C.L(bright):.4f} > 상한 {SELF_L_HI:.4f} "
          f"→ 흰 바탕 대비 {C.CR(bright, W):.2f}:1 "
          f"{'FAIL(정상: 게이트가 잡았다)' if C.CR(bright, W) < 3.0 else 'PASS(★대조 실패)'}")
    print("\n  대조 4건 모두 '게이트가 잡았다'가 나와야 한다. 하나라도 PASS(대조 실패)면 그 검사는 무효다.")


if __name__ == "__main__":
    C.calibrate()
    if "--control" in sys.argv:
        control()
        sys.exit(0)
    idf, disc = s4_floor()
    rows, subs = s5_wheel(idf, disc)
    ranka, rankb = s6_rank(idf, disc, rows, subs)
    s7_brass(rows, rankb)
    s8_ink()
    s9_unowned()
    print("\n" + "=" * 78)
    print("끝. 이 숫자들은 전부 선언값 계산이다 — 최종 판정은 실제 빌드 캡처로만 한다.")
    print("=" * 78)
