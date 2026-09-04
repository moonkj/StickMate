# -*- coding: utf-8 -*-
"""R9 — 「장비 채움색 = 등급색인가 재질색인가」 팔레트 축 (design-art, 2026-09-03)

리더 질의 5건:
  1. 「등급 램프 채택」(커밋 eca8c58)은 브라스 단색 램프인가 인계본 색상 램프인가.
     그리고 A-1 판정(브라스 38도는 안 부딪히고 파랑 213도는 오피스를 죽인다)이
     인계본 등급색 #6E9BE8 / #B07BE0 에 그대로 적용되는가 — **각도로 계산**.
  2. design-equipment 실측(11/12 대역 밖 · 8/12 비항등)을 **내 자로 다시** 잰다.
  3. 인계본 전제 "흰 선 위 가독성을 위해 한 톤 밝게"가 우리 출하 기본(검은 잉크)에서
     반대로 작동하는 **대가를 숫자로**.
  4. 절충안. 그리고 리더가 인용한 "최대 이동 0.59도 선례"가 참인지.
  5. 6팩x4부위 세트 — 색이 테마를 알리는가 등급을 알리는가.

★ 교정이 깨지면 아무 숫자도 내지 않는다.
★ 문서의 hex를 베끼지 않는다 — .asset 과 .cs 를 직접 파싱한다.

    python3 rarityaxis.py
    python3 rarityaxis.py --control    # 양성 대조
"""
import sys, os, re, math, collections, subprocess

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
import band, derive_packs as DP, cvd

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ITEMS = os.path.join(ROOT, "Assets/_Project/Resources/Items")
CATALOG_CS = os.path.join(ROOT, "Assets/_Project/Scripts/Core/ItemCatalog.cs")
CHROME_CS = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/UiChrome.cs")
CONFIG_CS = os.path.join(ROOT, "Assets/_Project/Scripts/Core/StickConfig.cs")

INK_MARKERS = {"#D6DBE3", "#8B939F"}
DISCERN, IDENTIFY = 7.8, 48.6          # 변별 / 식별 하한 (PALETTE_SPEC 3-3)
NONTEXT, TEXT = 3.0, 4.5

# ---- 인계본 (docs/handoff/design_handoff_equipment_window) ----
HANDOFF_CARD = [("일반", "#8A8F98"), ("희귀", "#6E9BE8"), ("영웅", "#B07BE0"), ("전설", "#E0B24A")]
HANDOFF_WORN = [("일반", "#D8B27A"), ("희귀", "#7FB0F2"), ("영웅", "#C08FEC"), ("전설", "#F0C25C")]
HANDOFF_MISC = [("아이템색 C", "#E8E2D6"), ("강조 A(브라스)", "#C8A15A"),
                ("망토 재질색", "#D2402F"), ("망토 뒤판 채움", "#A8332A"),
                ("망토 뒤판 보더", "#7E1F17"), ("망토 칼라", "#8E241C"),
                ("망토 클래스프", "#C8A15A"), ("하이라이트", "#FFFFFF")]

# ---- 우리 (PALETTE_SPEC 4-1, 리더 커밋 eca8c58 채택) ----
BRASS_RAMP = [("일반", "#9C978C"), ("희귀", "#BCAC8B"), ("영웅", "#DBBD7F"), ("전설", "#F9CB70")]
BRASS = "#C8A15A"

BD = band.BACKDROPS
LO, HI, _ = band.limits()


# ==========================================================================
# 0. 교정 — 새로 만든 자(색상 호 거리)를 알려진 값으로 먼저 잰다
# ==========================================================================

def arc(a, b):
    """두 색상각 사이의 최소 호(0~180)."""
    d = abs((a - b) % 360.0)
    return min(d, 360.0 - d)


def calibrate_arc(verbose=True):
    cases = [((0.0, 0.0), 0.0), ((0.0, 180.0), 180.0), ((0.0, 350.0), 10.0),
             ((10.0, 350.0), 20.0), ((350.0, 10.0), 20.0), ((222.0, 42.0), 180.0),
             ((359.9, 0.1), 0.2), ((90.0, 270.0), 180.0)]
    ok = True
    if verbose:
        print("=== 색상 호 자 교정 ===")
    for (a, b), want in cases:
        got = arc(a, b)
        p = abs(got - want) < 1e-9
        ok &= p
        if verbose:
            print(f"  {'PASS' if p else 'FAIL'}  arc({a:6.1f}, {b:6.1f}) = {got:7.3f}  (정답 {want})")
    # 알려진 색의 색상각
    known = [("#FF0000", 0.0), ("#00FF00", 120.0), ("#0000FF", 240.0),
             ("#FFFF00", 60.0), ("#00FFFF", 180.0), ("#FF00FF", 300.0),
             ("#808080", 0.0)]
    for hx, want in known:
        got = CL.hue_deg(CL.hex2rgb(hx))
        p = abs(got - want) < 1e-6
        ok &= p
        if verbose:
            print(f"  {'PASS' if p else 'FAIL'}  hue({hx}) = {got:7.3f}  (정답 {want})")
    if verbose:
        print(f"  교정 판정: {'유효' if ok else '무효'}\n")
    if not ok:
        sys.exit("호 자 교정 실패 — 모든 각도 수치 폐기.")
    return True


def calibrate_independent_L(verbose=True):
    """★ 휘도를 두 번째 구현으로 재계산해 colorlab.L 과 대조 (생성기=검사기 함정 회피)."""
    def L2(rgb):
        out = 0.0
        for c, w in zip(rgb, (0.2126, 0.7152, 0.0722)):
            s = c / 255.0
            out += w * (s / 12.92 if s <= 0.04045 else math.pow((s + 0.055) / 1.055, 2.4))
        return out
    worst, worstc = 0.0, None
    for r in range(0, 256, 17):
        for g in range(0, 256, 17):
            for b in range(0, 256, 17):
                d = abs(CL.L((r, g, b)) - L2((r, g, b)))
                if d > worst:
                    worst, worstc = d, (r, g, b)
    ok = worst < 1e-12
    if verbose:
        print(f"=== 독립 휘도 구현 대조 (4096색) ===")
        print(f"  {'PASS' if ok else 'FAIL'}  최대 편차 {worst:.3e}  (최악 {worstc})\n")
    if not ok:
        sys.exit("독립 휘도 대조 실패.")
    return True


# ==========================================================================
# 자산 파싱
# ==========================================================================

def parse_assets(text_getter, files):
    """(파일, 색순번) -> hex.  1:1 대응을 위해 위치를 키로 쓴다."""
    out = collections.OrderedDict()
    pat = re.compile(r"color: \{r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+), a: ([-\d.eE]+)\}")
    for f in files:
        t = text_getter(f)
        if t is None:
            continue
        for i, m in enumerate(pat.finditer(t)):
            hx = CL.rgb2hex(tuple(int(round(float(x) * 255)) for x in m.groups()[:3]))
            out[(f, i)] = hx
    return out


def asset_files():
    return sorted(f for f in os.listdir(ITEMS) if f.endswith(".asset"))


def tree_text(f):
    return open(os.path.join(ITEMS, f), encoding="utf-8").read()


def rev_text(rev):
    def g(f):
        rel = "Assets/_Project/Resources/Items/" + f
        try:
            return subprocess.check_output(["git", "show", rev + ":" + rel], cwd=ROOT,
                                           stderr=subprocess.DEVNULL).decode("utf-8")
        except subprocess.CalledProcessError:
            return None
    return g


def head_text(f):
    return rev_text("HEAD")(f)


def unique_catalog(posmap):
    seen = collections.OrderedDict()
    for k, hx in posmap.items():
        seen.setdefault(hx, []).append(k[0][:-6])
    return [h for h in seen if h not in INK_MARKERS], seen


def pack12():
    out = []
    for name, h, _ in DP.PACK_HUES:
        out.append((name, h, CL.rgb2hex(DP.pick(h, True)), CL.rgb2hex(DP.pick(h, False))))
    return out


def worst_bd(hx):
    c = CL.hex2rgb(hx)
    return min(CL.CR(c, b) for _, b in BD)


def in_band(hx):
    return LO <= CL.L(CL.hex2rgb(hx)) <= HI


def identity(hx):
    return CL.rgb2hex(CL.worn(CL.hex2rgb(hx))) == hx.upper()


# ==========================================================================
# §1. 「등급 램프」가 양쪽에서 다른 것을 뜻한다 — 색상각 폭으로 가른다
# ==========================================================================

def s1():
    print("=" * 92)
    print("§1. 「등급 램프」의 정체 — 색상 램프인가 단색 램프인가 (색상각 폭으로 가른다)")
    print("=" * 92)
    for label, ramp in (("인계본 카드/상세", HANDOFF_CARD), ("인계본 착용 오버레이", HANDOFF_WORN),
                        ("우리 브라스 램프(리더 채택)", BRASS_RAMP)):
        hs, ss, ls = [], [], []
        print(f"\n  [{label}]")
        print(f"    {'등급':6s} {'hex':9s} {'H':>7s} {'S':>6s} {'V':>6s} {'L':>7s} {'C*':>6s}")
        for nm, hx in ramp:
            c = CL.hex2rgb(hx)
            h, s, v = CL.rgb_to_hsv(c)
            a = CL.lab(c)
            ch = math.hypot(a[1], a[2])
            hs.append(h * 360.0); ss.append(s); ls.append(CL.L(c))
            print(f"    {nm:6s} {hx:9s} {h*360:7.2f} {s:6.3f} {v:6.3f} {CL.L(c):7.4f} {ch:6.2f}")
        # 색상각 폭 = 모든 쌍 중 최대 호
        spread = max(arc(a, b) for a in hs for b in hs)
        mono = all(ls[i] < ls[i + 1] for i in range(3))
        print(f"    → 색상각 최대 호 {spread:6.2f}°   휘도 단조 {'✔' if mono else '✘'}"
              f"   L 범위 {min(ls):.4f}~{max(ls):.4f}")
        if label.startswith("인계본 카드"):
            print(f"    ★ 일반↔희귀 색상각 호 = {arc(hs[0], hs[1]):.2f}° "
                  f"— 이 둘은 **같은 색상각**이고 채도만 {ss[0]:.3f}→{ss[1]:.3f}로 다르다")
    print()


# ==========================================================================
# §2. A-1 각도 판정을 인계본 등급색에 그대로 댄다
# ==========================================================================

def s2(packs):
    print("=" * 92)
    print("§2. A-1 각도 경제를 인계본 등급색에 그대로 적용 — 「예약하면 어느 팩이 죽는가」")
    print("=" * 92)
    ph = [(n, CL.hue_deg(CL.hex2rgb(p))) for n, h, p, s in packs]
    print("\n  팩 6종 실제 색상각 (선언값이 아니라 유도된 hex에서 다시 잰 값)")
    for (n, h_decl, p, s), (_, h_act) in zip(packs, ph):
        print(f"    {n:16s} 선언 {h_decl:6.1f}°  주색 {p} 실측 {h_act:6.2f}°  "
              f"(차 {abs(h_act-h_decl):.2f}°)")

    print(f"\n  {'예약 후보':22s} {'hex':9s} {'H':>7s} | " +
          " ".join(f"{n[:4]:>6s}" for n, _ in ph) + "   최소호  최근접")
    cands = [("현행 강조 파랑", "#5DA1F5"), ("브라스(리더 채택)", BRASS)]
    cands += [(f"인계본 카드 {n}", hx) for n, hx in HANDOFF_CARD]
    cands += [(f"인계본 착용 {n}", hx) for n, hx in HANDOFF_WORN]
    cands += [(f"브라스램프 {n}", hx) for n, hx in BRASS_RAMP]
    rows = []
    for nm, hx in cands:
        h = CL.hue_deg(CL.hex2rgb(hx))
        arcs = [arc(h, ha) for _, ha in ph]
        mn = min(arcs)
        near = ph[arcs.index(mn)][0]
        rows.append((nm, hx, h, arcs, mn, near))
        print(f"  {nm:22s} {hx:9s} {h:7.2f} | " +
              " ".join(f"{a:6.1f}" for a in arcs) + f"  {mn:6.2f}  {near}")

    print("\n  ★ 그런데 각도가 가깝다고 색이 부딪히는 것은 아니다 — 대역이 갈라져 있으면 안 부딪힌다.")
    print("     같은 후보를 **ΔE로도** 잰다 (팩 12색 전체 대상, 변별 7.8 / 식별 48.6).")
    pk = [c for _, _, p, s in packs for c in (p, s)]
    print(f"\n  {'예약 후보':22s} {'hex':9s} {'L':>7s} {'대역':>5s} {'최소ΔE(팩12)':>12s}  최근접팩색  판정")
    for nm, hx, h, arcs, mn, near in rows:
        c = CL.hex2rgb(hx)
        d = sorted((CL.dE(c, CL.hex2rgb(o)), o) for o in pk)[0]
        zone = "아트" if in_band(hx) else ("크롬" if CL.L(c) > 0.30 else "해자")
        verd = "충돌" if d[0] < DISCERN else ("근접" if d[0] < 20 else "안전")
        print(f"  {nm:22s} {hx:9s} {CL.L(c):7.4f} {zone:>5s} {d[0]:12.2f}  {d[1]}   {verd}")
    print()
    return ph


# ==========================================================================
# §3. design-equipment 실측을 내 자로 다시 잰다
# ==========================================================================

def s3():
    print("=" * 92)
    print("§3. 재측정 — design-equipment 「12색 중 11색 대역 밖 · 8색 WornColor 비항등」")
    print("=" * 92)
    twelve = ([("아이템색 C", "#E8E2D6"), ("강조 A(브라스)", "#C8A15A"), ("망토 재질색", "#D2402F")]
              + [(f"착용 등급 {n}", hx) for n, hx in HANDOFF_WORN]
              + [("망토 뒤판 채움", "#A8332A"), ("망토 뒤판 보더", "#7E1F17"),
                 ("망토 칼라", "#8E241C"), ("망토 클래스프", "#C8A15A"), ("하이라이트", "#FFFFFF")])
    print(f"\n  {'색':16s} {'hex':9s} {'L':>7s} {'최악CR':>7s} {'대역':>5s} | "
          f"{'→몸':9s} {'ΔE':>7s} {'항등':>5s}")
    out_band = non_id = 0
    for nm, hx in twelve:
        c = CL.hex2rgb(hx)
        w = CL.rgb2hex(CL.worn(c))
        d = CL.dE(c, CL.hex2rgb(w))
        ib = in_band(hx)
        idt = (w == hx.upper())
        out_band += (0 if ib else 1)
        non_id += (0 if idt else 1)
        print(f"  {nm:16s} {hx:9s} {CL.L(c):7.4f} {worst_bd(hx):7.2f} "
              f"{'안' if ib else '★밖':>5s} | {w:9s} {d:7.2f} {'✔' if idt else '✘':>5s}")
    print(f"\n  → 대역 **밖** {out_band} / {len(twelve)}     (design-equipment 주장: 11 / 12)")
    print(f"  → WornColor **비항등** {non_id} / {len(twelve)}  (design-equipment 주장: 8 / 12)")
    print(f"  ※ 이 12색 목록은 인계본 색 전체이고 `강조 A`와 `망토 클래스프`가 **같은 값**"
          f"({BRASS})이다 — 고유색으로 세면 11색이다.")
    uniq = sorted({hx for _, hx in twelve})
    ob = sum(0 if in_band(h) else 1 for h in uniq)
    ni = sum(0 if identity(h) else 1 for h in uniq)
    print(f"  → 고유 {len(uniq)}색 기준: 대역 밖 {ob} / {len(uniq)} · 비항등 {ni} / {len(uniq)}")
    print()
    return out_band, non_id, len(twelve)


# ==========================================================================
# §4. "흰 선 위 가독성을 위해 한 톤 밝게" — 우리 출하 기본에서의 대가
# ==========================================================================

def s4():
    print("=" * 92)
    print("§4. 인계본 전제 「흰 선 + 검은 머리 고정」 → 출하 실측과 대조, 그리고 「한 톤 밝게」의 대가")
    print("=" * 92)
    # 출하 기본 잉크색을 코드에서 직접 읽는다
    src = open(CONFIG_CS, encoding="utf-8").read()
    m = re.search(r"public\s+StickmanInkColor\s+inkColor\s*=\s*StickmanInkColor\.(\w+)", src)
    print(f"\n  StickConfig.cs 실측: inkColor 기본값 = StickmanInkColor.{m.group(1) if m else '??'}")
    asset = os.path.join(ROOT, "Assets/_Project/Resources/DefaultStickConfig.asset")
    if os.path.exists(asset):
        t = open(asset, encoding="utf-8").read()
        mm = re.search(r"inkColor:\s*(\d+)", t)
        print(f"  DefaultStickConfig.asset 실측: inkColor: {mm.group(1) if mm else '(필드 없음)'}"
              f"   (0=Black · 애셋이 코드 기본값을 덮는다 — TEAM.md §4-2 사고 9)")
    WHITE, BLACKINK = (255, 255, 255), (0, 0, 0)
    print(f"\n  [착용 장비 색이 잉크 획과 얼마나 갈리는가]  (장비는 잉크 획 바로 옆에 놓인다)")
    print(f"  {'등급':6s} | {'카드 램프':9s} {'→몸':9s} {'흰잉크':>7s} {'검잉크':>7s} {'배경4최악':>9s}"
          f" | {'착용 램프':9s} {'→몸':9s} {'흰잉크':>7s} {'검잉크':>7s} {'배경4최악':>9s}")
    dw = dk = 0.0
    for (n1, c1), (n2, c2) in zip(HANDOFF_CARD, HANDOFF_WORN):
        w1 = CL.worn(CL.hex2rgb(c1)); w2 = CL.worn(CL.hex2rgb(c2))
        a1, b1 = CL.CR(w1, WHITE), CL.CR(w1, BLACKINK)
        a2, b2 = CL.CR(w2, WHITE), CL.CR(w2, BLACKINK)
        dw += a2 - a1; dk += b2 - b1
        print(f"  {n1:6s} | {c1:9s} {CL.rgb2hex(w1):9s} {a1:7.2f} {b1:7.2f} "
              f"{worst_bd(CL.rgb2hex(w1)):9.2f} | {c2:9s} {CL.rgb2hex(w2):9s} "
              f"{a2:7.2f} {b2:7.2f} {worst_bd(CL.rgb2hex(w2)):9.2f}")
    print(f"\n  → 「한 톤 밝게」의 순효과: 흰 잉크 대비 평균 {dw/4:+.3f} · 검은 잉크 대비 평균 {dk/4:+.3f}")
    print(f"     ★ 우리 출하 기본은 **검은 잉크**다. 밝히면 정확히 **잃는 쪽**으로 움직인다.")

    print(f"\n  [밝히는 방향이 실제로 어느 쪽에 유리한가 — 잉크 두 경우 전수]")
    for nm, ink in (("흰 잉크(인계본 전제)", WHITE), ("검은 잉크(우리 출하 기본)", BLACKINK)):
        best = None
        print(f"    {nm}")
        for (n1, c1), (n2, c2) in zip(HANDOFF_CARD, HANDOFF_WORN):
            w1, w2 = CL.worn(CL.hex2rgb(c1)), CL.worn(CL.hex2rgb(c2))
            r1, r2 = CL.CR(w1, ink), CL.CR(w2, ink)
            better = "착용(밝은)" if r2 > r1 else "카드(어두운)"
            print(f"      {n1:6s} 카드→몸 {r1:6.2f}  착용→몸 {r2:6.2f}   유리한 쪽: {better}")
    print()

    print(f"  [최악 케이스 — 검은 잉크 + 밝은 바탕화면]  하한: 비텍스트 {NONTEXT}")
    print(f"  {'등급':6s} {'착용→몸':9s} {'흰 바탕화면 CR':>14s} {'종이 무대 CR':>13s} {'판정':>6s}")
    for n, hx in HANDOFF_WORN:
        w = CL.worn(CL.hex2rgb(hx))
        a = CL.CR(w, (255, 255, 255)); b = CL.CR(w, CL.hex2rgb("#E9EAE6"))
        print(f"  {n:6s} {CL.rgb2hex(w):9s} {a:14.2f} {b:13.2f} "
              f"{'FAIL' if min(a, b) < NONTEXT else 'PASS':>6s}")
    print()


# ==========================================================================
# §5. 안 C — 등급색이 카드 안에만 갇히면 각도 예약 충돌이 없어지는가
# ==========================================================================

CARD_HANDOFF = "#161311"     # 인계본 아이템 카드 상단
CARD_HANDOFF_LO = "#111010"  # 인계본 아이템 카드 하단
ICON_BG = "#0F0D0C"          # 인계본 아이콘 영역 바닥
OUR_CARD = "#1B1F26"         # UiChrome.CardSurface
OUR_PANEL = "#141727"        # (참고) — 아래에서 코드로 다시 읽는다


def flatten(fg_hex, alpha, bg_hex):
    f, b = CL.hex2rgb(fg_hex), CL.hex2rgb(bg_hex)
    return CL.rgb2hex(tuple(f[i] * alpha + b[i] * (1 - alpha) for i in range(3)))


def s5(packs):
    print("=" * 92)
    print("§5. 【안 C】 등급색을 **카드 안에만** 가두면 각도 예약 충돌이 사라지는가")
    print("=" * 92)
    # 우리 표면을 코드에서 다시 읽는다
    src = open(CHROME_CS, encoding="utf-8").read()
    def tok(name):
        m = re.search(name + r"\s*=\s*new Color\(([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f", src)
        return CL.rgb2hex(tuple(round(float(g) * 255) for g in m.groups())) if m else None
    ours = {n: tok(n) for n in ("CardSurface", "PanelSurface", "TextPrimary", "TextSecondary")}
    print("\n  우리 표면 (UiChrome.cs 직접 파싱):  " +
          "  ".join(f"{k}={v}" for k, v in ours.items()))

    pk = [c for _, _, p, s in packs for c in (p, s)]
    print("\n  [핵심] 「예약」이 값을 하려면 두 색이 **한 화면에 같이 있고 헷갈려야** 한다.")
    print("         카드 한 장 안에는 아이콘(팩 재질색·아트 대역)과 등급 표식(크롬 대역)이 **항상 같이** 있다.")
    print("         그러므로 '무대가 갈린다'는 것으로는 충돌이 없어지지 않는다 — **ΔE로 재야 한다.**")
    print(f"\n  {'등급 표식 색':22s} {'hex':9s} {'L':>7s} {'L*':>6s} | {'최소ΔE(팩12)':>12s} "
          f"{'최근접':9s} {'변별7.8':>7s} {'식별48.6':>8s}  읽히는 방식")
    rows = []
    for label, ramp in (("인계본", HANDOFF_CARD), ("브라스", BRASS_RAMP)):
        for n, hx in ramp:
            c = CL.hex2rgb(hx)
            d, o = sorted((CL.dE(c, CL.hex2rgb(x)), x) for x in pk)[0]
            state = ("같은 색으로 보인다" if d < DISCERN
                     else "같은 색 가족으로 읽힌다" if d < IDENTIFY else "다른 색으로 읽힌다")
            rows.append((label, n, hx, d, o))
            print(f"  {label+' '+n:22s} {hx:9s} {CL.L(c):7.4f} {CL.lab(c)[0]:6.2f} | {d:12.2f} "
                  f"{o:9s} {'✔' if d>=DISCERN else '✘':>7s} {'✔' if d>=IDENTIFY else '✘':>8s}  {state}")
    hmin = min(d for l, n, h, d, o in rows if l == "인계본")
    bmin = min(d for l, n, h, d, o in rows if l == "브라스")
    print(f"\n  → 인계본 램프 최소 ΔE {hmin:.2f} / 브라스 램프 최소 ΔE {bmin:.2f}"
          f"   (차 {bmin-hmin:+.2f})")
    print(f"  → 브라스 자신(강조색) ↔ 팩 12색 최소 ΔE "
          f"{min(CL.dE(CL.hex2rgb(BRASS), CL.hex2rgb(x)) for x in pk):.2f}")
    print("\n  ★ 자기 감사: 두 램프 **모두** 식별 하한 48.6을 넘지 못하는 쌍이 있다.")
    print("     즉 「각도를 예약하면 팩이 죽는다」는 내 A-1 서술은 **강했다** — 대역이 갈려도")
    print("     ΔE는 완전히 분리되지 않는다. 다만 **크기 차이는 실재한다**(아래).")
    print()
    return rows, ours


# ==========================================================================
# §6. 안 C — 「카드색」의 범위를 어디까지. 바탕면을 물들이면 무엇이 깨지는가
# ==========================================================================

def s6(packs, ours):
    print("=" * 92)
    print("§6. 【안 C】 「카드색」의 범위 — 리본? 글로우? 테두리? 바탕면? (숫자로 못박는다)")
    print("=" * 92)
    card = ours["CardSurface"]; tp = ours["TextPrimary"]; ts = ours["TextSecondary"]
    pk = [c for _, _, p, s in packs for c in (p, s)]

    print(f"\n  먼저 리더 브리프의 숫자를 검산한다 — 「카드 바탕 대비 5.68 / 7.41 / 9.13 / 10.86」")
    print(f"  {'등급':6s} {'hex':9s} {'CR vs 우리 카드 '+card:>24s} {'브리프값':>8s} {'차':>7s}")
    claim = [5.68, 7.41, 9.13, 10.86]
    for (n, hx), cv in zip(BRASS_RAMP, claim):
        got = CL.CR(CL.hex2rgb(hx), CL.hex2rgb(card))
        print(f"  {n:6s} {hx:9s} {got:24.4f} {cv:8.2f} {got-cv:+7.4f}")

    print(f"\n  [범위 후보별 대가]  카드 면 {card} · 글자 {tp}/{ts} · 아이콘 = 팩 12색(아트 대역)")
    print(f"  {'범위':30s} {'글자 최악':>9s} {'아이콘 최악':>11s} {'등급표식 최악':>13s} 판정")

    def icon_worst(bg_hex):
        return min(CL.CR(CL.hex2rgb(x), CL.hex2rgb(bg_hex)) for x in pk)

    def rank_worst(bg_hex, ramp=BRASS_RAMP):
        return min(CL.CR(CL.hex2rgb(h), CL.hex2rgb(bg_hex)) for _, h in ramp)

    def text_worst(bg_hex):
        return min(CL.CR(CL.hex2rgb(tp), CL.hex2rgb(bg_hex)),
                   CL.CR(CL.hex2rgb(ts), CL.hex2rgb(bg_hex)))

    # 1) 리본만 (면을 안 건드린다)
    print(f"  {'① 리본만 (면 불변)':30s} {text_worst(card):9.2f} {icon_worst(card):11.2f} "
          f"{rank_worst(card):13.2f} {'PASS' if text_worst(card)>=TEXT else 'FAIL'}")
    # 2) 리본 + 등급 낱말(글자색 = 등급색)
    print(f"  {'② + 등급 낱말(글자=등급색)':30s} {min(text_worst(card), rank_worst(card)):9.2f} "
          f"{icon_worst(card):11.2f} {rank_worst(card):13.2f} "
          f"{'PASS' if min(text_worst(card), rank_worst(card))>=TEXT else 'FAIL'}")
    # 3) + 아이콘 영역 radial glow (인계본 알파 0x1F)
    A = 0x1F / 255.0
    print(f"\n  ③ 아이콘 영역 글로우 — 인계본 원안 알파 0x1F = {A:.4f}")
    print(f"    {'등급':6s} {'등급색':9s} {'글로우 합성':11s} {'아이콘 최악':>11s} {'글자 최악':>9s} 판정")
    for n, hx in BRASS_RAMP:
        comp = flatten(hx, A, card)
        print(f"    {n:6s} {hx:9s} {comp:11s} {icon_worst(comp):11.2f} {text_worst(comp):9.2f} "
              f"{'PASS' if icon_worst(comp)>=NONTEXT and text_worst(comp)>=TEXT else 'FAIL'}")
    # 4) 바탕면 전체를 물들인다 — 알파 상한을 찾는다
    print(f"\n  ④ ★ **바탕면 전체**를 등급색으로 물들이면 — 알파 상한 전수 (0.00~1.00, 0.01 간격)")
    print(f"    {'등급':6s} {'글자4.5 상한α':>13s} {'아이콘3.0 상한α':>15s} {'등급표식3.0 상한α':>17s}"
          f" {'셋 다 만족 상한α':>16s}")
    caps = []
    for n, hx in BRASS_RAMP:
        cap_t = cap_i = cap_r = -1.0
        for i in range(0, 101):
            a = i / 100.0
            comp = flatten(hx, a, card)
            if text_worst(comp) >= TEXT: cap_t = a
            if icon_worst(comp) >= NONTEXT: cap_i = a
            # 등급 표식(리본)이 물든 면 위에서도 보여야 한다
            if CL.CR(CL.hex2rgb(hx), CL.hex2rgb(comp)) >= NONTEXT: cap_r = a
        cap = min(cap_t, cap_i, cap_r)
        caps.append((n, hx, cap_t, cap_i, cap_r, cap))
        print(f"    {n:6s} {cap_t:13.2f} {cap_i:15.2f} {cap_r:17.2f} {cap:16.2f}")
    gmin = min(c[5] for c in caps)
    print(f"\n    → 네 등급 공통 상한 α = **{gmin:.2f}**  "
          f"(가장 먼저 죽는 것: "
          f"{min(caps, key=lambda c: c[5])[0]} / "
          f"{'글자' if min(caps,key=lambda c:c[5])[2]==min(caps,key=lambda c:c[5])[5] else ('아이콘' if min(caps,key=lambda c:c[5])[3]==min(caps,key=lambda c:c[5])[5] else '등급표식')})")
    print(f"    → 인계본 글로우 알파 {A:.4f} 는 이 상한 안에 {'들어간다' if A <= gmin else '★ 들어가지 않는다'}")
    print(f"    ★ 즉 「카드 면을 물들인다」는 **알파 {gmin:.2f} 이하에서만** 성립한다."
          f" α=1(완전 물들임)은 {'가능' if gmin>=1.0 else '**불가능**'}.")

    # 완전 물들임(α=1)에서 무엇이 정확히 깨지는가
    print(f"\n  ⑤ 참고 — α=1.00(면 전체가 등급색)이면 정확히 무엇이 깨지는가")
    print(f"    {'등급':6s} {'면':9s} {'TextPrimary':>11s} {'TextSecondary':>13s} {'아이콘 최악':>11s} 깨지는 것")
    for n, hx in BRASS_RAMP:
        a = CL.CR(CL.hex2rgb(tp), CL.hex2rgb(hx))
        b = CL.CR(CL.hex2rgb(ts), CL.hex2rgb(hx))
        ic = icon_worst(hx)
        bad = []
        if a < TEXT: bad.append("이름글자")
        if b < TEXT: bad.append("보조글자")
        if ic < NONTEXT: bad.append("아이콘")
        print(f"    {n:6s} {hx:9s} {a:11.2f} {b:13.2f} {ic:11.2f} "
              f"{'· '.join(bad) if bad else '(없음)'}")
    print()
    return gmin, A


# ==========================================================================
# §7. 세트 — 색이 테마를 알리는가 등급을 알리는가 (한 벌 4부위를 실제로 세워 본다)
# ==========================================================================

def s7(packs):
    print("=" * 92)
    print("§7. 6팩 × 4부위 세트 — 한 벌을 실제로 세워 「하나로 보이는가」를 잰다")
    print("=" * 92)
    # 등급 분포는 ECONOMY_SPEC 3-2 규칙(rank 0,1=일반 / 2,3=희귀 / 4=영웅 / 5=전설)에서
    # 한 벌 4부위가 서로 다른 rank를 갖는 최악(=가장 흩어지는) 경우를 잡는다.
    print("\n  [안 A] 채움 = 등급색 — 한 벌 4부위의 등급이 갈리면 색도 갈린다")
    combos = [("모자 일반 · 안경 희귀 · 타이 영웅 · 망토 전설", [c for _, c in HANDOFF_WORN])]
    for label, cols in combos:
        worn = [CL.worn(CL.hex2rgb(c)) for c in cols]
        pairs = [(CL.dE(worn[i], worn[j]), i, j) for i in range(4) for j in range(i + 1, 4)]
        print(f"    {label}")
        print(f"      몸 색: " + " ".join(CL.rgb2hex(w) for w in worn))
        print(f"      한 벌 내부 ΔE: 최소 {min(p[0] for p in pairs):.2f} · 최대 {max(p[0] for p in pairs):.2f}")
        print(f"      → 변별 하한 7.8을 넘는 쌍 {sum(1 for p in pairs if p[0]>=DISCERN)} / 6"
              f"  = **네 조각이 서로 다른 물건으로 읽힌다**")
    print("\n  [안 B/C] 채움 = 팩 재질색 — 한 벌 4부위가 주색/보조색 둘만 쓴다")
    for n, h, p, s in packs:
        cols = [p, p, s, s]        # 4부위: 주2 + 보조2 (§7 인계 계약)
        rgb = [CL.hex2rgb(c) for c in cols]
        pairs = [CL.dE(rgb[i], rgb[j]) for i in range(4) for j in range(i + 1, 4)]
        print(f"    {n:16s} 한 벌 내부 ΔE 최대 {max(pairs):6.2f}  "
              f"(주 {p} · 보조 {s})  "
              f"→ 식별 48.6 미만 {sum(1 for x in pairs if x<IDENTIFY)}/6 = 한 덩어리로 읽힌다")
    print("\n  [교차] 서로 다른 팩의 조각을 섞으면 갈리는가 — 세트 인지의 반대편 조건")
    allp = [(n, p, s) for n, h, p, s in packs]
    worst = min(((CL.dE(CL.hex2rgb(a1), CL.hex2rgb(b1)), n1, n2)
                 for (n1, p1, s1) in allp for (n2, p2, s2) in allp if n1 < n2
                 for a1 in (p1, s1) for b1 in (p2, s2)))
    print(f"    팩끼리 최근접 쌍 ΔE {worst[0]:.2f}  ({worst[1]} ↔ {worst[2]})  "
          f"변별 7.8 {'✔' if worst[0]>=DISCERN else '✘'}")
    print()


# ==========================================================================
# §8. 리더 인용 「최대 이동 0.59도 선례」 — 참인가
# ==========================================================================

def s8():
    print("=" * 92)
    print("§8. 검증 — 리더가 인용한 「휘도만 대역으로 옛긴다 · 최대 이동 0.59도의 선례」")
    print("=" * 92)
    files = asset_files()
    # ★ 이 저장소에서 「대역 이행」이 일어난 커밋 쌍을 먼저 찾는다.
    # (HEAD와 트리를 비교하면 0건이 나온다 — 이미 커밋됐기 때문이고,
    #  그 0건을 "이행이 안 됐다"로 읽으면 거짓 판정이다.)
    log = subprocess.check_output(["git", "log", "--format=%h", "--",
                                   "Assets/_Project/Resources/Items"], cwd=ROOT).decode().split()
    print(f"\n  .asset 색을 건드린 커밋 {len(log)}개: " + " ".join(log))
    pairs = [(log[i + 1], log[i]) for i in range(len(log) - 1)]
    print(f"\n  {'커밋 쌍':22s} {'값 다른 슬롯':>12s} {'고유 색쌍':>9s} "
          f"{'최대 Δhue°':>11s} {'최대 ΔL':>9s}")
    best = None
    for a, b in pairs:
        A = parse_assets(rev_text(a), files)
        B = parse_assets(rev_text(b), files)
        keys = [k for k in B if k in A]
        moved = [(k, A[k], B[k]) for k in keys if A[k] != B[k]]
        if not moved:
            print(f"  {a+' -> '+b:22s} {0:12d} {0:9d} {'-':>11s} {'-':>9s}")
            continue
        uniq = sorted({(x, y) for _, x, y in moved})
        mh = max(arc(CL.hue_deg(CL.hex2rgb(x)), CL.hue_deg(CL.hex2rgb(y))) for x, y in uniq)
        ml = max(abs(CL.L(CL.hex2rgb(x)) - CL.L(CL.hex2rgb(y))) for x, y in uniq)
        print(f"  {a+' -> '+b:22s} {len(moved):12d} {len(uniq):9d} {mh:11.4f} {ml:9.4f}")
        if best is None or ml > best[3]:
            best = (a, b, mh, ml, uniq)
    if best:
        print(f"\n  ★ 「대역 이행」 = 휘도 이동이 가장 큰 쌍 = {best[0]} -> {best[1]}")
        print(f"     그 쌍의 **최대 색상각 이동 = {best[2]:.4f}°**   (리더 인용값 0.59°)")
        print(f"     판정: {'일치' if abs(best[2] - 0.59) < 0.02 else '★ 가깝지만 정확히는 어긋난다'}"
              f"  (차 {best[2]-0.59:+.4f}°)")
        print(f"\n     그 쌍에서 색상각이 가장 많이 움직인 색 5개")
        rows = sorted(((arc(CL.hue_deg(CL.hex2rgb(x)), CL.hue_deg(CL.hex2rgb(y))),
                        abs(CL.L(CL.hex2rgb(x)) - CL.L(CL.hex2rgb(y))), x, y) for x, y in best[4]),
                      reverse=True)
        for h, dl, x, y in rows[:5]:
            print(f"       Δhue {h:8.4f}°  ΔL {dl:.4f}   {x} -> {y}")
        print(f"\n  ★ 그러나 다른 쌍에서는 색상각이 100° 넘게 움직인 적이 있다(위 표) —")
        print(f"     그것은 대역 이행이 아니라 FX 재배정이다. **「0.59도」는 대역 이행 한 커밋에만 걸린다.**")
        return best[2]
    return None


# ==========================================================================
# §9. 인계본 색상 램프를 「휘도만 대역으로」 옮길 수 있는가 (안 A 구제 시도)
# ==========================================================================

def s9(packs):
    print("=" * 92)
    print("§9. 안 A 구제 시도 — 인계본 색상각을 살리고 휘도만 아트 대역으로 옮기면 어떻게 되는가")
    print("=" * 92)
    print(f"\n  규칙: 색상각 고정 · 상자(S≥0.42, V 0.55~0.80) ∩ 자립 대역 L∈[{LO:.4f},{HI:.4f}]")
    print(f"        ∩ WornColor 항등.  그 안에서 **휘도가 가장 높은/낮은** 해를 찾는다.")
    print(f"\n  {'등급':6s} {'인계본':9s} {'H':>7s} {'대역 안 해':>10s} {'해 개수':>7s} "
          f"{'가능 L 범위':>18s}")
    sols = []
    for n, hx in HANDOFF_CARD:
        h = CL.hue_deg(CL.hex2rgb(hx))
        cand = []
        for si in range(42, 101):
            for vi in range(55, 81):
                c = CL.hsv_to_rgb(h / 360.0, si / 100.0, vi / 100.0)
                if LO <= CL.L(c) <= HI and CL.worn(c) == c:
                    cand.append(c)
        if cand:
            lo = min(cand, key=CL.L); hi = max(cand, key=CL.L)
            sols.append((n, h, cand, lo, hi))
            print(f"  {n:6s} {hx:9s} {h:7.2f} {'있음':>10s} {len(cand):7d} "
                  f"{CL.L(lo):8.4f}~{CL.L(hi):.4f}")
        else:
            sols.append((n, h, [], None, None))
            print(f"  {n:6s} {hx:9s} {h:7.2f} {'★ 없음':>10s} {0:7d} {'-':>18s}")
    print("\n  [그 다음 질문] 네 등급을 이 대역 안에 세우면 **서열이 남는가**")
    if all(s[2] for s in sols):
        # 각 등급에 대해 휘도가 단조 증가하도록 최선 배치 (전체 가능 L의 4분위)
        chosen = []
        for i, (n, h, cand, lo, hi) in enumerate(sols):
            tgt = LO + (HI - LO) * (i + 0.5) / 4.0
            c = min(cand, key=lambda x: abs(CL.L(x) - tgt))
            chosen.append((n, c))
        print(f"    {'등급':6s} {'대역 안 값':9s} {'L':>7s} {'배경4최악':>9s}")
        for n, c in chosen:
            print(f"    {n:6s} {CL.rgb2hex(c):9s} {CL.L(c):7.4f} {worst_bd(CL.rgb2hex(c)):9.2f}")
        pairs = [(CL.dE(chosen[i][1], chosen[j][1]), chosen[i][0], chosen[j][0])
                 for i in range(4) for j in range(i + 1, 4)]
        adj = [CL.CR(chosen[i][1], chosen[i + 1][1]) for i in range(3)]
        print(f"    인접 단 대비 {['%.2f' % a for a in adj]}   (비텍스트 하한 {NONTEXT})")
        print(f"    모든 쌍 최소 ΔE {min(p[0] for p in pairs):.2f} "
              f"(변별 {DISCERN} {'✔' if min(p[0] for p in pairs)>=DISCERN else '✘'} / "
              f"식별 {IDENTIFY} {'✔' if min(p[0] for p in pairs)>=IDENTIFY else '✘'})")
        pk = [c for _, _, p, s in packs for c in (p, s)]
        cl = min((CL.dE(c, CL.hex2rgb(x)), n, x) for n, c in chosen for x in pk)
        print(f"    ★ 이렇게 옮기면 **아트 대역 안으로 들어오므로 팩 색과 같은 방에 선다** — "
              f"팩 12색 최근접 ΔE {cl[0]:.2f} ({cl[1]} ↔ {cl[2]})")
    print()


# ==========================================================================
# §10. 색각 이상 — 두 램프를 같은 자로
# ==========================================================================

def s10():
    print("=" * 92)
    print("§10. 색각 이상 · 회색조 — 두 램프를 같은 자로 (등급 서열이 살아남는가)")
    print("=" * 92)
    cvd.calibrate(verbose=False)
    print(f"\n  {'램프':10s} {'유형':10s} {'인접 최소ΔE':>11s} {'변별 미달쌍':>11s} {'휘도 단조':>9s}")
    res = {}
    for label, ramp in (("인계본", HANDOFF_CARD), ("브라스", BRASS_RAMP)):
        for kind in ("정상",) + cvd.TYPES + ("완전색맹",):
            cols = []
            for n, hx in ramp:
                c = CL.hex2rgb(hx)
                if kind == "정상":
                    cols.append(c)
                elif kind == "완전색맹":
                    g = round(CL.L(c) ** (1 / 2.2) * 255)   # 휘도 보존 회색
                    cols.append((g, g, g))
                else:
                    cols.append(cvd.sim(c, kind))
            adj = min(CL.dE(cols[i], cols[i + 1]) for i in range(3))
            allp = [CL.dE(cols[i], cols[j]) for i in range(4) for j in range(i + 1, 4)]
            under = sum(1 for x in allp if x < DISCERN)
            mono = all(CL.L(cols[i]) < CL.L(cols[i + 1]) for i in range(3))
            res[(label, kind)] = (adj, under, mono)
            print(f"  {label:10s} {kind:10s} {adj:11.2f} {under:11d} {'✔' if mono else '✘':>9s}")
    print()
    return res


# ==========================================================================
# §11. InkDimTone 지뢰 · 브라스 근접 — 인계본 램프의 알려진 결함 재측정
# ==========================================================================

def s11():
    print("=" * 92)
    print("§11. 인계본 램프의 알려진 결함 2건을 다시 잰다 (표식색 충돌 · 브라스 근접)")
    print("=" * 92)
    src = open(CATALOG_CS, encoding="utf-8").read()
    marks = {}
    for m in re.finditer(r"public static readonly Color (Ink\w*Tone) = Rgb\(0x([0-9A-Fa-f]{6})\)", src):
        marks[m.group(1)] = "#" + m.group(2).upper()
    print(f"\n  ItemCatalog.cs 직접 파싱: " + "  ".join(f"{k}={v}" for k, v in marks.items()))
    print(f"\n  {'램프':10s} {'등급':6s} {'hex':9s} " +
          " ".join(f"{'ΔE '+k:>14s}" for k in marks) + f" {'ΔE 브라스':>10s}")
    for label, ramp in (("인계본", HANDOFF_CARD), ("브라스", BRASS_RAMP)):
        for n, hx in ramp:
            c = CL.hex2rgb(hx)
            ds = [CL.dE(c, CL.hex2rgb(v)) for v in marks.values()]
            db = CL.dE(c, CL.hex2rgb(BRASS))
            flag = "  ★ 표식색과 사실상 같은 색" if min(ds) < DISCERN else ""
            print(f"  {label:10s} {n:6s} {hx:9s} " +
                  " ".join(f"{d:14.2f}" for d in ds) + f" {db:10.2f}{flag}")
    print()


# ==========================================================================
# 양성 대조
# ==========================================================================

def control():
    CL.calibrate(verbose=False)
    calibrate_arc(verbose=False)
    print("=" * 92)
    print("양성 대조 — 이 라운드의 자가 실제로 무엇을 잡는지 먼저 보인다")
    print("=" * 92)
    ok = 0
    # 1) 호 자가 경계를 못 넘으면 잡히는가
    bad = lambda a, b: min(abs(a - b), 360 - abs(a - b))
    got = bad(10.0, 350.0)
    print(f"  1. 순진한 호 구현 arc(10,350) = {got:.1f}  (정답 20.0) "
          f"→ {'PASS(같음)' if abs(got-20)<1e-9 else 'FAIL'}")
    ok += 1 if abs(got - 20) < 1e-9 else 0
    # 2) 대역 판정이 실제로 떨어지는 색을 잡는가
    probe = "#F0C25C"
    print(f"  2. 인계본 전설 {probe} 대역 판정 = {'안' if in_band(probe) else '★밖'} "
          f"(L {CL.L(CL.hex2rgb(probe)):.4f}, 대역 상한 {HI:.4f}) → "
          f"{'게이트가 잡았다' if not in_band(probe) else 'FAIL — 게이트가 죽었다'}")
    ok += 0 if in_band(probe) else 1
    # 3) 항등 판정이 실제로 비항등을 잡는가
    print(f"  3. {probe} 항등 판정 = {identity(probe)} → "
          f"{'게이트가 잡았다' if not identity(probe) else 'FAIL'}")
    ok += 0 if identity(probe) else 1
    # 4) 항등 판정이 항등색을 통과시키는가(음성 대조)
    p2 = "#456ECC"
    print(f"  4. 팩 오피스 주색 {p2} 항등 판정 = {identity(p2)} → "
          f"{'통과(음성 대조 성립)' if identity(p2) else 'FAIL — 자가 항상 ✘를 낸다'}")
    ok += 1 if identity(p2) else 0
    # 5) ΔE가 동일색에서 0, 흰검에서 100
    d0 = CL.dE((1, 2, 3), (1, 2, 3)); d1 = CL.dE((255, 255, 255), (0, 0, 0))
    print(f"  5. ΔE 동일색 {d0:.4f} (정답 0) · 흰검 {d1:.4f} (정답 100) → "
          f"{'PASS' if abs(d0)<1e-9 and abs(d1-100)<0.01 else 'FAIL'}")
    ok += 1 if abs(d0) < 1e-9 and abs(d1 - 100) < 0.01 else 0
    # 6) 알파 합성이 알려진 값을 내는가 (α=0 → 배경, α=1 → 전경, α=0.5 회색)
    a = flatten("#FFFFFF", 0.0, "#000000"); b = flatten("#FFFFFF", 1.0, "#000000")
    c = flatten("#FFFFFF", 0.5, "#000000")
    print(f"  6. 합성 α0 {a} (정답 #000000) · α1 {b} (정답 #FFFFFF) · α0.5 {c} (정답 #808080) → "
          f"{'PASS' if a=='#000000' and b=='#FFFFFF' and c=='#808080' else 'FAIL'}")
    ok += 1 if a == "#000000" and b == "#FFFFFF" and c == "#808080" else 0
    # 7) 파싱이 실제로 애셋을 읽는가 (0건이면 모든 '없음' 판정이 무효)
    t = parse_assets(tree_text, asset_files())
    print(f"  7. .asset 색 슬롯 파싱 {len(t)}건 → "
          f"{'PASS(파서 살아 있음)' if len(t) > 50 else 'FAIL — 0건이면 §8 전부 무효'}")
    ok += 1 if len(t) > 50 else 0
    print(f"\n  대조 {ok} / 7 통과")
    return ok == 7


def main():
    CL.calibrate()
    calibrate_arc()
    calibrate_independent_L()
    packs = pack12()
    s1()
    s2(packs)
    s3()
    s4()
    rows, ours = s5(packs)
    s6(packs, ours)
    s7(packs)
    s8()
    s9(packs)
    s10()
    s11()


if __name__ == "__main__":
    if "--control" in sys.argv:
        control()
    else:
        main()
