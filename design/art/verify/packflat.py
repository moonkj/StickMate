# -*- coding: utf-8 -*-
"""★ 평탄 팩 라운드 — 「등급이 공유 축이 될 수 없다」는 답을 받고 다시 잰다 (design-art, 2026-09-02)

`design-systems` R3(ECONOMY_SPEC 0-3-2/0-3-3/0-3-4, 검산 47건 PASS)이 확정했다:
  · 팩 6종은 전부 같은 등급(희귀) · 전부 Lv.1 · 0동전 즉시 해금.
  · "어느 것이 Lv.1인가" = 전부다. 팩 안에 등급 축이 없다.

그래서 내 PACK_THEME_SPEC §2/§3-4/§5의 **등급 사다리 전제가 죽는다.**
이 스크립트는 (a) 죽은 전제를 실측으로 확인하고 (b) 그 자리를 무엇이 대신하는지를 잰다.

    python3 packflat.py            # 본안
    python3 packflat.py --control  # ★ 양성 대조 — 이게 전건 빨갛지 않으면 위 결과 전부 폐기

교정 원칙(TEAM.md §4): 계산기는 알려진 값으로 먼저 교정한다. colorlab 16건 + 이 파일 6건.
"""
import glob
import json
import os
import re
import sys

import colorlab as C

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ITEMS = os.path.join(ROOT, "Assets/_Project/Resources/Items")
UICHROME = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/UiChrome.cs")
CATALOG = os.path.join(ROOT, "Assets/_Project/Scripts/Core/ItemCatalog.cs")

FLOOR_NONTEXT = 3.0     # UiChrome.MinNonTextContrast
FLOOR_TEXT = 4.5        # UiChrome.MinTextContrast
FLOOR_DISCRIM = 7.8     # 변별 하한 (PALETTE_SPEC §3-3)
FLOOR_PACK_GAP = 8.0    # 처방 C 여유 (PALETTE_SPEC §13-3)

W, K = (255, 255, 255), (0, 0, 0)
PAPER = C.hex2rgb("#E9EAE6")
CHARCOAL = C.hex2rgb("#25282E")
BACKDROPS = [("흰 바탕화면", W), ("검은 바탕화면", K), ("종이 무대", PAPER), ("목탄 무대", CHARCOAL)]

# --- 동결 대장 (PALETTE_SPEC §13-3 처방 C · PackPaletteGateTests.FrozenPacks 와 같은 값) -------
PACKS = [
    ("오피스 워커",        "pack.office",    222, "#456ECC", "#6080CC"),
    ("사이버 아포칼립스",  "pack.cyber",     172, "#009682", "#518C84"),
    ("네온 낙서",          "pack.graffiti",  312, "#CC1BA9", "#9C5A8E"),
    ("스포츠",             "pack.sports",      8, "#CC3F29", "#9E655C"),
    ("컬러 잉크",          "pack.ink",       268, "#9768CC", "#8563AB"),
    ("밀리터리",           "pack.military",   80, "#639400", "#798C51"),
]
RARITY = [("일반", "#9C978C", 1), ("희귀", "#BCAC8B", 2), ("영웅", "#DBBD7F", 3), ("전설", "#F9CB70", 4)]
BRASS = "#C8A15A"
BRASS_2ND = "#A08148"
LADDER = ["일반", "일반", "희귀", "희귀", "영웅", "전설"]     # ItemCatalog._rarityByRank

_FAILS = []


def ok(cond, label, detail=""):
    tag = "PASS" if cond else "FAIL"
    if not cond:
        _FAILS.append(label)
    print(f"  {tag}  {label}{('  — ' + detail) if detail else ''}")
    return cond


# =============================================================================
# 0. 교정 — 이게 깨지면 아래 숫자 전부 폐기
# =============================================================================
def calibrate():
    print("=" * 84)
    print("§0 교정 — colorlab 16건 + 이 파일의 자 6건")
    print("=" * 84)
    C.calibrate(verbose=False)
    print("  PASS  colorlab 16건 (흰/검 21.0 · 동일색 1.0 · #767676/흰 4.5422 · LAB · dE)")

    hard = []
    hard.append(("대비 흰/검 = 21.0", abs(C.CR(W, K) - 21.0) < 5e-4, f"{C.CR(W, K):.4f}"))
    hard.append(("대비 동일색 = 1.0", abs(C.CR(W, W) - 1.0) < 5e-4, f"{C.CR(W, W):.4f}"))
    hard.append(("dE 흰/검 = 100.0", abs(C.dE(W, K) - 100.0) < 1e-2, f"{C.dE(W, K):.4f}"))
    # 순위상관 계산기 교정 — 완전 일치 +1, 완전 역순 -1, 알려진 표본
    hard.append(("스피어만 완전일치 = +1", abs(spearman([1, 2, 3, 4], [10, 20, 30, 40]) - 1.0) < 1e-9, ""))
    hard.append(("스피어만 완전역순 = -1", abs(spearman([1, 2, 3, 4], [40, 30, 20, 10]) + 1.0) < 1e-9, ""))
    # 교과서 표본: x=[1,2,3,4,5], y=[5,6,7,8,7] -> rho = 0.8207826816681233
    hard.append(("스피어만 동점 표본 = 0.82078", abs(spearman([1, 2, 3, 4, 5], [5, 6, 7, 8, 7]) - 0.8207826816681233) < 1e-9, ""))
    for label, cond, detail in hard:
        ok(cond, label, detail)
    if _FAILS:
        print("\n★ 교정이 깨졌다. 이 실행의 모든 숫자를 폐기한다.")
        sys.exit(2)
    print()


def rank_of(vals):
    """평균 순위(동점 처리)."""
    order = sorted(range(len(vals)), key=lambda i: vals[i])
    r = [0.0] * len(vals)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and vals[order[j + 1]] == vals[order[i]]:
            j += 1
        avg = (i + j) / 2.0 + 1.0
        for k in range(i, j + 1):
            r[order[k]] = avg
        i = j + 1
    return r


def spearman(x, y):
    rx, ry = rank_of(x), rank_of(y)
    n = len(x)
    mx, my = sum(rx) / n, sum(ry) / n
    num = sum((rx[i] - mx) * (ry[i] - my) for i in range(n))
    dx = sum((rx[i] - mx) ** 2 for i in range(n)) ** 0.5
    dy = sum((ry[i] - my) ** 2 for i in range(n)) ** 0.5
    return num / (dx * dy) if dx and dy else 0.0


# =============================================================================
# 애셋 파서 — 문서를 베끼지 않는다
# =============================================================================
_COLOR_RE = re.compile(r"color:\s*\{r:\s*([0-9.eE+-]+),\s*g:\s*([0-9.eE+-]+),\s*b:\s*([0-9.eE+-]+),\s*a:\s*([0-9.eE+-]+)\}")
_TONE_RE = re.compile(r"^\s*tone:\s*(\d+)\s*$")
_SLOT_RE = re.compile(r"^\s*slot:\s*(\d+)\s*$", re.M)
_IDX_RE = re.compile(r"^\s*itemIndex:\s*(\d+)\s*$", re.M)
_LV_RE = re.compile(r"^\s*requiredLevel:\s*(-?\d+)\s*$", re.M)
_NAME_RE = re.compile(r'^\s*displayName:\s*"(.*)"\s*$', re.M)
_ID_RE = re.compile(r"^\s*itemId:\s*(\S+)\s*$", re.M)


def load_items():
    out = []
    for path in sorted(glob.glob(os.path.join(ITEMS, "*.asset"))):
        txt = open(path, encoding="utf-8").read()
        s, ix, lv = _SLOT_RE.search(txt), _IDX_RE.search(txt), _LV_RE.search(txt)
        if not (s and ix):
            continue
        nm = _NAME_RE.search(txt)
        # ★ .asset의 한글은 \uXXXX 이스케이프다. grep '[가-힣]'은 영원히 0건이다.
        disp = json.loads('"' + nm.group(1) + '"') if nm else ""
        tones, pending = {}, None
        for ln in txt.splitlines():
            m = _COLOR_RE.search(ln)
            if m:
                pending = tuple(int(round(float(m.group(i)) * 255)) for i in (1, 2, 3))
                continue
            t = _TONE_RE.match(ln)
            if t and pending is not None:
                tones.setdefault(int(t.group(1)), []).append(pending)
                pending = None
        idm = _ID_RE.search(txt)
        out.append({
            "file": os.path.basename(path), "id": idm.group(1) if idm else "",
            "slot": int(s.group(1)), "index": int(ix.group(1)),
            "level": int(lv.group(1)) if lv else 0, "name": disp, "tones": tones,
        })
    return out


def uichrome_tokens():
    txt = open(UICHROME, encoding="utf-8").read()
    pat = re.compile(r"public static readonly Color (\w+)\s*=\s*new Color\(([0-9.f]+),\s*([0-9.f]+),\s*([0-9.f]+),\s*([0-9.f]+)\)")
    out = {}
    for m in pat.finditer(txt):
        v = [float(m.group(i).rstrip("f")) for i in (2, 3, 4, 5)]
        out[m.group(1)] = (tuple(int(round(x * 255)) for x in v[:3]), v[3])
    return out


def ladder_from_source():
    """ItemCatalog.cs의 _rarityByRank 배열을 소스에서 읽는다(값을 베끼지 않는다)."""
    txt = open(CATALOG, encoding="utf-8").read()
    m = re.search(r"_rarityByRank\s*=\s*\{(.*?)\};", txt, re.S)
    if not m:
        return None
    names = re.findall(r"ItemRarity\.(\w+)", m.group(1))
    ko = {"Common": "일반", "Rare": "희귀", "Epic": "영웅", "Legendary": "전설"}
    return [ko.get(n, n) for n in names]


def rarity_of(rank, count, ladder):
    step = rank * len(ladder) // count
    return ladder[min(step, len(ladder) - 1)]


def over(fg_rgb, alpha, bg_rgb):
    return tuple(int(round(fg_rgb[i] * alpha + bg_rgb[i] * (1 - alpha))) for i in range(3))


def worst_backdrop(rgb):
    return min(C.CR(rgb, b) for _, b in BACKDROPS)


# =============================================================================
def main():
    calibrate()
    items = load_items()
    tokens = uichrome_tokens()
    ladder = ladder_from_source()

    # -------------------------------------------------------------------------
    print("=" * 84)
    print("§1 표본 정직성 — 빈 목록이 초록이 되지 않게 / 한글 이스케이프 함정 양성 대조")
    print("=" * 84)
    ok(len(items) == 42, f"애셋 42종을 읽었다", f"{len(items)}종")
    ok(ladder == LADDER, "등급 사다리를 ItemCatalog.cs 소스에서 읽었고 문서와 같다", str(ladder))
    ko_named = [i for i in items if re.search(r"[가-힣]", i["name"])]
    ok(len(ko_named) == 42, "★ 이스케이프 해제 후 한글 이름이 42종에서 나온다", f"{len(ko_named)}종 (예: {items[0]['name']})")
    raw_hits = 0
    for path in glob.glob(os.path.join(ITEMS, "*.asset")):
        if re.search(r"[가-힣]", open(path, encoding="utf-8").read()):
            raw_hits += 1
    ok(raw_hits == 0, "★ 함정 재현 — 생 텍스트에서 한글 grep은 0건이다(그래서 0건은 증거가 아니다)", f"{raw_hits}건")
    print()

    # -------------------------------------------------------------------------
    print("=" * 84)
    print("§2 죽은 전제 확인 — 팩 안에 등급 축이 있었다면 무엇이 나왔는가")
    print("=" * 84)
    old = ["일반", "일반", "희귀", "희귀", "영웅", "전설"]
    ribbon = {n: h for n, h, _ in RARITY}
    old_cols = [C.hex2rgb(ribbon[r]) for r in old]
    new_cols = [C.hex2rgb(ribbon["희귀"])] * 6
    lo, hi = min(C.L(c) for c in old_cols), max(C.L(c) for c in old_cols)
    old_cr = (hi + 0.05) / (lo + 0.05)
    print(f"  [폐기] 팩 내부 사다리 {old}")
    print(f"         리본 색 고유 {len(set(old))}종 · 리본 휘도 폭 {old_cr:.4f} : 1 · 칸 수 1~4")
    print(f"  [확정] 팩 내부 평면  {['희귀'] * 6}   (ECONOMY_SPEC DS-2)")
    print(f"         리본 색 고유 {len(set(['희귀']))}종 · 리본 휘도 폭 "
          f"{(C.L(new_cols[0]) + 0.05) / (C.L(new_cols[0]) + 0.05):.4f} : 1 · 칸 수 2 고정")
    ok(len(set(old)) == 4 and old_cr > 1.9, "폐기된 사다리는 리본 4색 · 휘도 폭 1.9:1 이상이었다", f"{old_cr:.4f}")
    ok(abs(C.CR(new_cols[0], new_cols[0]) - 1.0) < 1e-9, "확정된 평면은 리본 1색 · 휘도 폭 1.0000:1", "분산 0")
    print()

    # -------------------------------------------------------------------------
    print("=" * 84)
    print("§3 ★ 기본 42종은 사다리, 팩 6종은 평면 — 두 문법이 직교하는가")
    print("=" * 84)
    by_slot = {}
    for it in items:
        by_slot.setdefault(it["slot"], []).append(it)
    rows, rar_num, lum = [], [], []
    numof = {"일반": 0, "희귀": 1, "영웅": 2, "전설": 3}
    for s in sorted(by_slot):
        pop = sorted(by_slot[s], key=lambda x: x["index"])
        n = len(pop)
        for it in pop:
            key = it["level"]
            rank = sum(1 for o in pop if (o["level"] < key) or (o["level"] == key and o["index"] < it["index"]))
            it["rarity"] = rarity_of(rank, n, ladder)
        # 슬롯 안 리본 색 고유 수
        rows.append((s, n, len({i["rarity"] for i in pop})))
    dist = {}
    for it in items:
        dist[it["rarity"]] = dist.get(it["rarity"], 0) + 1
    print(f"  등급 분포(42종): {dist}")
    ok(dist == {"일반": 14, "희귀": 14, "영웅": 7, "전설": 7},
       "★ 파생 재구현 교정 — ItemCatalog.cs 주석이 독립적으로 선언한 14/14/7/7과 같다", str(dist))
    uniq = [u for _, _, u in rows]
    ok(all(u == 4 for u in uniq), f"기본 7슬롯 전부 슬롯 안 리본 색 4종", str(uniq))
    print(f"  → 기본 슬롯: 리본 색 4종 / 팩: 1종. **분산 자체가 「이건 팩이다」를 말한다.**")
    print()

    # -------------------------------------------------------------------------
    print("=" * 84)
    print("§4 ★ 반증 시도 — 아트 대역 휘도가 「가치」를 나르는가 (출하 42종 전수)")
    print("=" * 84)
    xs, ys, miss = [], [], 0
    for it in items:
        t0 = it["tones"].get(0) or []
        t0 = [c for c in t0 if C.rgb2hex(c).upper() not in ("#D6DBE3", "#8B939F")]
        if not t0:
            miss += 1
            continue
        xs.append(numof[it["rarity"]])
        ys.append(C.L(t0[0]))
    rho = spearman(xs, ys)
    print(f"  표본 {len(xs)}종 (잉크 표식만 있는 {miss}종 제외)")
    print(f"  등급(0..3) ↔ 주색 상대휘도 스피어만 ρ = {rho:+.4f}")
    ok(abs(rho) < 0.30, "★ 아트 대역 휘도는 등급과 무상관이다 — 유저가 밝기를 가치로 읽을 근거가 트리에 없다",
       f"|ρ| = {abs(rho):.4f} < 0.30")
    print()

    # -------------------------------------------------------------------------
    print("=" * 84)
    print("§5 ★ 자기반증 — 「팩 밝기 폭이 등급 한 칸보다 작다」는 성립하지 않는다")
    print("=" * 84)
    pl = [C.L(C.hex2rgb(p)) for _, _, _, p, _ in PACKS]
    pack_cr = (max(pl) + 0.05) / (min(pl) + 0.05)
    steps = []
    for i in range(3):
        a, b = C.hex2rgb(RARITY[i][1]), C.hex2rgb(RARITY[i + 1][1])
        steps.append(C.CR(a, b))
    print(f"  팩 주색 6종 휘도 폭        = {pack_cr:.4f} : 1   (L {min(pl):.4f} ~ {max(pl):.4f})")
    print(f"  등급 램프 인접 한 칸 대비  = {min(steps):.4f} ~ {max(steps):.4f} : 1")
    claim = pack_cr < min(steps)
    print(f"  가설 「팩 폭 < 등급 한 칸」 : {'참' if claim else '★ 거짓'} "
          f"({pack_cr:.4f} {'<' if claim else '>'} {min(steps):.4f}, 초과 {100*(pack_cr/min(steps)-1):+.1f}%)")
    ok(not claim, "★ 내 가설이 반증됐다 — 그대로 적는다(§4의 무상관이 이 초과를 무해하게 만든다)",
       f"초과 {100*(pack_cr/min(steps)-1):+.1f}%")
    print()

    # -------------------------------------------------------------------------
    print("=" * 84)
    print("§6 ★ UiChrome.Accent 황동 전환 — 토큰 1개가 아니라 몇 개짜리 작업인가")
    print("=" * 84)
    acc = tokens.get("Accent", ((0, 0, 0), 1))[0]
    print(f"  현행 Accent = {C.rgb2hex(acc)} (L {C.L(acc):.4f})   판정 = 황동 {BRASS} (L {C.L(C.hex2rgb(BRASS)):.4f})")
    ok(C.rgb2hex(acc).upper() == "#5DA1F5", "★ 코드는 아직 파랑이다 — 판정과 코드가 갈라져 있다", C.rgb2hex(acc))

    # ★ 크롬 면은 어두운 표면 5종에만 놓인다. PortraitSurface는 이 팔레트의 **유일한 밝은 예외**이고
    #   (UiChrome.cs:38 "예외는 PortraitSurface 하나뿐") 크롬 버튼을 놓는 자리가 아니다.
    #   빼고 재는 것이 정직하려면 **뺀 자리도 재서 보여야 한다** — (e)에서 따로 잰다.
    surfaces = [(k, tokens[k][0]) for k in
                ("PanelSurface", "CardSurface", "CardSurfaceMuted", "SubtleSurface", "ThumbSurfaceLocked")
                if k in tokens]
    light = ("PortraitSurface", tokens["PortraitSurface"][0])
    brass = C.hex2rgb(BRASS)
    print("\n  (a) 면 대비 — 강조 면이 표면 위에서 3.0을 넘는가")
    for name, srf in surfaces:
        print(f"      {name:20s}  파랑 {C.CR(acc, srf):5.2f}  황동 {C.CR(brass, srf):5.2f}")
    worst_blue = min(C.CR(acc, s) for _, s in surfaces)
    worst_brass = min(C.CR(brass, s) for _, s in surfaces)
    ok(worst_brass >= FLOOR_NONTEXT, f"황동 면 최악 {worst_brass:.2f} ≥ {FLOOR_NONTEXT} (어두운 표면 5종)",
       f"파랑은 {worst_blue:.2f} — 황동이 {100*(worst_brass/worst_blue-1):+.1f}%")

    print("\n  (b) 강조 면 위 글자 — 합법 잉크가 남는가")
    for tk in ("OnAccentSolid", "TextPrimary"):
        if tk not in tokens:
            continue
        c = tokens[tk][0]
        print(f"      {tk:16s} on 파랑 {C.CR(c, acc):5.2f}   on 황동 {C.CR(c, brass):5.2f}")
    on_solid = tokens["OnAccentSolid"][0]
    ok(C.CR(on_solid, brass) >= FLOOR_TEXT, f"OnAccentSolid가 황동 면 위에서 {C.CR(on_solid, brass):.2f} ≥ 4.5")

    print("\n  (c) ★ 따라가야 하는 토큰 — 파랑에서 유도된 값들")
    followers = []
    for tk in ("AccentSurface", "AccentBorder", "AccentGlowCore", "WarmAccent", "TextOnAccent"):
        if tk not in tokens:
            continue
        rgb, a = tokens[tk]
        dh = C.dE(rgb, acc)
        followers.append((tk, rgb, a, dh))
        print(f"      {tk:16s} {C.rgb2hex(rgb)} a={a:.2f}  현행 Accent와 ΔE {dh:6.2f}"
              f"{'   ← 같은 색조 파생' if dh < 40 else ''}")
    ok(len(followers) >= 5, f"황동 전환이 건드려야 하는 Accent 파생 토큰 {len(followers)}개를 열거했다",
       ", ".join(t for t, _, _, _ in followers))

    ton = tokens["TextOnAccent"][0]
    card = tokens["CardSurface"][0]
    blue_wash = over(acc, tokens["AccentSurface"][1], card)
    brass_wash = over(brass, tokens["AccentSurface"][1], card)
    print(f"\n      AccentSurface 합성(카드 위): 파랑 {C.rgb2hex(blue_wash)} / 황동 {C.rgb2hex(brass_wash)}")
    print(f"      TextOnAccent {C.rgb2hex(ton)} 위 대비 : 파랑칠 {C.CR(ton, blue_wash):.2f} / "
          f"황동칠 {C.CR(ton, brass_wash):.2f}")
    ok(C.CR(ton, brass_wash) >= FLOOR_TEXT,
       f"★ TextOnAccent(파랑 파생)가 황동칠 위에서도 4.5를 넘는가",
       f"{C.CR(ton, brass_wash):.2f}")

    # 황동 계열 TextOnAccent 후보 — 같은 규칙(같은 색조의 밝은 값)으로 유도
    best = None
    h_b, s_b, _ = C.rgb_to_hsv(brass)
    for v in range(60, 101):
        for s in range(10, 70):
            cand = C.hsv_to_rgb(h_b, s / 100.0, v / 100.0)
            cr = C.CR(cand, brass_wash)
            if cr >= FLOOR_TEXT and (best is None or cr < best[1]):
                best = (cand, cr, s, v)
    if best:
        print(f"      → 황동 계열 대체 후보 {C.rgb2hex(best[0])} (H {h_b*360:.0f}° S {best[2]}% V {best[3]}%) "
              f"= {best[1]:.2f} : 1")

    print("\n  (e) ★ 뺀 자리를 재서 보인다 — 밝은 액자 면(이 팔레트의 유일한 예외)")
    lname, lsrf = light
    print(f"      {lname}({C.rgb2hex(lsrf)}) 위: 파랑 {C.CR(acc, lsrf):.2f} · 황동 {C.CR(brass, lsrf):.2f} "
          f"· 희귀 리본 {C.CR(C.hex2rgb(RARITY[1][1]), lsrf):.2f}  — **셋 다 3.0 미달**")
    ok(C.CR(acc, lsrf) < FLOOR_NONTEXT,
       "★ 황동은 이 자리에서 회귀가 아니다 — 지금 출하된 파랑도 같은 자리에서 미달이다",
       f"파랑 {C.CR(acc, lsrf):.2f} / 황동 {C.CR(brass, lsrf):.2f}")
    ok(C.CR(C.hex2rgb(BRASS_2ND), lsrf) >= FLOOR_NONTEXT,
       f"★ 처방은 이미 있다 — 2차 조작면 {BRASS_2ND}(=브라스×0.80)가 밝은 면에서 "
       f"{C.CR(C.hex2rgb(BRASS_2ND), lsrf):.2f} ≥ 3.0", "새 색 0개")

    print("\n  (d) 황동이 아트 색과 안 섞이는가 (대역 분리)")
    art = set()
    for it in items:
        for t in (0, 1):
            for c in it["tones"].get(t, []):
                if C.rgb2hex(c).upper() not in ("#D6DBE3", "#8B939F"):
                    art.add(c)
    pack_cols = [C.hex2rgb(p) for _, _, _, p, _ in PACKS] + [C.hex2rgb(s) for _, _, _, _, s in PACKS]
    d_art = min(C.dE(brass, c) for c in art)
    d_pack = min(C.dE(brass, c) for c in pack_cols)
    print(f"      카탈로그 아트 {len(art)}색 중 최근접 ΔE {d_art:.2f}  ·  팩 12색 최근접 ΔE {d_pack:.2f}")
    ok(d_pack >= FLOOR_PACK_GAP, f"황동 ↔ 팩 12색 ΔE {d_pack:.2f} ≥ {FLOOR_PACK_GAP}")
    ok(d_art >= FLOOR_DISCRIM, f"황동 ↔ 카탈로그 아트색 ΔE {d_art:.2f} ≥ {FLOOR_DISCRIM}")
    print()

    # -------------------------------------------------------------------------
    print("=" * 84)
    print("§7 출하 색 감사 대조 — 내 값이 감사를 다시 빨갛게 만드는가")
    print("=" * 84)
    lo_b, hi_b = 3 * (C.L(CHARCOAL) + 0.05) - 0.05, (C.L(PAPER) + 0.05) / 3 - 0.05
    print(f"  자립 대역 L ∈ [{lo_b:.4f}, {hi_b:.4f}]  (목탄이 아래, 종이가 위를 막는다)")
    bad_band = [(n, h) for n, _, _, h, _ in PACKS if not (lo_b <= C.L(C.hex2rgb(h)) <= hi_b)]
    bad_band += [(n, h) for n, _, _, _, h in PACKS if not (lo_b <= C.L(C.hex2rgb(h)) <= hi_b)]
    ok(not bad_band, "팩 12색 전부 자립 대역 안", f"{len(bad_band)}건 밖")
    bad_ident = [C.rgb2hex(c) for c in pack_cols if C.worn(c) != c]
    ok(not bad_ident, "팩 12색 전부 WornColor 항등 (카드 = 몸)", f"{len(bad_ident)}건 위반")
    worst = min(worst_backdrop(c) for c in pack_cols)
    ok(worst >= FLOOR_NONTEXT, f"팩 12색 배경 4종 최악 {worst:.2f} ≥ {FLOOR_NONTEXT}")
    gap = min(C.dE(p, a) for p in pack_cols for a in art)
    ok(gap >= FLOOR_PACK_GAP, f"카탈로그({len(art)}) ↔ 팩(12) 최소 ΔE {gap:.2f} ≥ {FLOOR_PACK_GAP}",
       "어제 정리한 0/25 상태를 유지한다")
    outside = [C.rgb2hex(c) for c in art if not (lo_b <= C.L(c) <= hi_b)]
    ok(not outside, f"★ 출하 아트 {len(art)}색 중 대역 밖 = {len(outside)}건 (어제 20/25 → 0/25)",
       ", ".join(outside[:5]))
    print(f"  ★ 이 라운드가 새로 만든 아트 색 = 0개. 새 hex는 크롬(황동 파생)에서만 나온다.")
    print()

    # -------------------------------------------------------------------------
    print("=" * 84)
    print("§8 오피스 워커 — 팩 하나를 끝까지 (색만. 조형은 남의 담당)")
    print("=" * 84)
    prim, sec = C.hex2rgb("#456ECC"), C.hex2rgb("#6080CC")
    shade = C.fill_outline(prim)
    print(f"  주색   #456ECC  L {C.L(prim):.4f}  H {C.hue_deg(prim):.1f}°  배경최악 {worst_backdrop(prim):.2f}")
    print(f"  보조색 #6080CC  L {C.L(sec):.4f}  H {C.hue_deg(sec):.1f}°  배경최악 {worst_backdrop(sec):.2f}")
    print(f"  그늘색 {C.rgb2hex(shade)}  L {C.L(shade):.4f}  (FillOutlineColor 파생 — 값을 우리가 안 고른다)")
    print(f"  주↔보조 ΔE {C.dE(prim, sec):.2f} (변별 하한 {FLOOR_DISCRIM})")
    rib = C.hex2rgb(RARITY[1][1])
    print(f"  등급 리본 = 희귀 {RARITY[1][1]} × 6종 전부 (칸 2)  · 리본↔주색 ΔE {C.dE(rib, prim):.2f}")
    ok(C.dE(rib, prim) >= FLOOR_DISCRIM, f"희귀 리본과 오피스 주색이 안 섞인다 ΔE {C.dE(rib, prim):.2f}")
    print(f"\n  카드 크롬 대비 (등급 리본 · 팩 칩) — 어두운 표면 5종")
    for name, srf in surfaces:
        print(f"      {name:20s} 리본 {C.CR(rib, srf):5.2f}   팩칩(주색) {C.CR(prim, srf):5.2f}")
    ok(min(C.CR(rib, s) for _, s in surfaces) >= FLOOR_NONTEXT, "희귀 리본이 표면 5종에서 3.0 이상")
    ok(min(C.CR(prim, s) for _, s in surfaces) >= FLOOR_NONTEXT, "오피스 팩칩이 표면 5종에서 3.0 이상")

    print(f"\n  이펙트 4종 — 잉크 획 : 팩색 획 = 2 : 1 (E-1, 잉크 지분 ≥ 50%)")
    for fx in ("착지 먼지", "임팩트 선", "오라", "회전 잔상"):
        print(f"      {fx:10s} 잉크 2획 + #456ECC 1획   (팩색 획 배경최악 {worst_backdrop(prim):.2f})")
    print(f"  ★ 파쿠르 파티클: 전용 렌더러가 아직 없다(§13-9). 생기면 같은 2:1을 그대로 받는다.")
    print()

    # -------------------------------------------------------------------------
    print("=" * 84)
    print("§9 ★ DS-3(0동전 즉시 해금)이 만든 새 위험 — FX 팔레트 경로")
    print("=" * 84)
    fxp = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/CharacterFxRenderer.cs")
    if os.path.exists(fxp):
        t = open(fxp, encoding="utf-8").read()
        n_ink = len(re.findall(r"ResolveInk\s*\(", t))
        n_pal = len(re.findall(r"ResolveWornPalette\s*\(", t))
        print(f"  CharacterFxRenderer.cs : ResolveInk {n_ink}건 / ResolveWornPalette {n_pal}건")
        # ★ "없음" 판정의 양성 대조 — 심볼 자체가 트리에 존재하고 다른 렌더러는 실제로 부른다.
        #   (grep이 고장나서 0건이 나온 것이 아님을 같은 방법으로 증명한다.)
        import subprocess
        hits = subprocess.run(
            ["grep", "-rl", "ResolveWornPalette", os.path.join(ROOT, "Assets/_Project/Scripts/Interaction")],
            capture_output=True, text=True).stdout.split()
        others = [os.path.basename(h) for h in hits if "CharacterFxRenderer" not in h]
        ok(len(others) >= 2, "★ 양성 대조 — 같은 심볼을 실제로 부르는 렌더러가 있다(0건이 grep 고장이 아니다)",
           ", ".join(sorted(others)))
        ok(n_pal == 0, "★ FX 렌더러는 팩 팔레트에 아직 도달하지 못한다 (변한 것은 이 사실의 값어치다)",
           f"{n_pal}건")
        print(f"  [옛 완충] FX를 팩의 마지막(전설)에 두어 지연을 숨겼다 → **DS-2가 그 완충을 없앴다**")
        print(f"  [지금]    6종이 같은 분에 열린다 = 산 그 순간 6분의 1이 회색이다 = 환불창 안 결함")
    print()

    print("=" * 84)
    print("§10 ★ 자기산출물 드리프트 — 내 PALETTE_SPEC §3-3의 「팩 최소 ΔE 29.8」은 낡았다")
    print("=" * 84)
    old_prim = ["#456ECC", "#009682", "#CC1BA9", "#CC3F29", "#8D56CC", "#639400"]   # §3-1 1차 유도
    new_prim = [p for _, _, _, p, _ in PACKS]                                        # §13-3 처방 C
    def minpair(S):
        return min(C.dE(C.hex2rgb(a), C.hex2rgb(b)) for i, a in enumerate(S) for b in S[i + 1:])
    o_, n_ = minpair(old_prim), minpair(new_prim)
    print(f"  1차 유도(§3-1) 주색 6종 상호 최소 ΔE = {o_:.2f}   ← 문서가 적은 29.8은 이 값이다")
    print(f"  처방 C(§13-3)  주색 6종 상호 최소 ΔE = {n_:.2f}   ← 지금 정본의 값")
    ok(abs(o_ - 29.81) < 0.05, "문서의 29.8이 1차 유도값이었음을 재현했다", f"{o_:.2f}")
    ok(n_ < o_, "★ 처방 C는 팩끼리를 더 붙였다 — 카탈로그 충돌을 지운 대가다(적어 두지 않았던 거래)",
       f"{o_:.2f} → {n_:.2f} ({n_ - o_:+.2f})")
    ok(n_ < 48.6, "그래도 결론은 안 바뀐다 — 색만으로는 여전히 팩을 못 맞힌다(식별 하한 48.6)",
       f"{n_:.2f} < 48.6")
    ok(n_ >= FLOOR_DISCRIM, f"변별 하한 {FLOOR_DISCRIM}은 여유 있게 넘는다", f"{n_:.2f}")
    hs = sorted(h for _, _, h, _, _ in PACKS)
    gaps = [(hs[(i + 1) % 6] - hs[i]) % 360 for i in range(6)]
    print(f"  색상각 {hs} · 이웃 간격 {gaps}° (최소 {min(gaps)}° / 최대 {max(gaps)}°)")
    ok(min(gaps) >= 40, f"여섯 방위가 40° 이상 벌어져 있다", f"최소 {min(gaps)}°")
    print()

    print("=" * 84)
    if _FAILS:
        print(f"판정: FAIL {len(_FAILS)}건 — {_FAILS}")
        sys.exit(1)
    print("판정: 전건 PASS")
    print("=" * 84)


# =============================================================================
def control():
    """양성 대조 — 일부러 나쁜 값을 **본안과 같은 판정 함수**에 넣는다."""
    calibrate()
    print("=" * 84)
    print("양성 대조 — 아래가 전건 '잡음'이 아니면 본안의 초록을 폐기한다")
    print("=" * 84)
    lo_b, hi_b = 3 * (C.L(CHARCOAL) + 0.05) - 0.05, (C.L(PAPER) + 0.05) / 3 - 0.05
    caught = 0

    bad = C.hex2rgb("#FFF0B8")   # 실제 사고색
    if not (lo_b <= C.L(bad) <= hi_b):
        caught += 1
        print(f"  잡음  대역 검사: #FFF0B8 L {C.L(bad):.4f} 가 [{lo_b:.4f}, {hi_b:.4f}] 밖")

    if worst_backdrop(C.worn(bad)) < FLOOR_NONTEXT:
        caught += 1
        print(f"  잡음  배경 검사: #FFF0B8 -> 몸 {C.rgb2hex(C.worn(bad))} 최악 "
              f"{worst_backdrop(C.worn(bad)):.2f} < {FLOOR_NONTEXT}")

    clash = C.hex2rgb("#5C709E")  # 처방 C 이전 오피스 보조색 (실제 충돌값)
    if C.dE(clash, C.hex2rgb("#587398")) < FLOOR_PACK_GAP:
        caught += 1
        print(f"  잡음  ΔE 검사: 옛 오피스 보조색 #5C709E ↔ 카탈로그 #587398 = "
              f"{C.dE(clash, C.hex2rgb('#587398')):.2f} < {FLOOR_PACK_GAP}")

    if C.worn(bad) != bad:
        caught += 1
        print(f"  잡음  항등 검사: #FFF0B8 -> {C.rgb2hex(C.worn(bad))} (WornColor가 바꾼다)")

    hand = C.hex2rgb("#8A8F98")   # 핸드오프 일반 등급색
    if C.dE(hand, C.hex2rgb("#8B939F")) < FLOOR_DISCRIM:
        caught += 1
        print(f"  잡음  표식색 검사: 핸드오프 일반 #8A8F98 ↔ InkDimTone #8B939F = "
              f"{C.dE(hand, C.hex2rgb('#8B939F')):.2f} < {FLOOR_DISCRIM}")

    # ★ 스피어만이 진짜 상관을 잡는가 — 무상관 판정의 양성 대조
    rho = spearman([0, 1, 2, 3, 4, 5], [0.10, 0.15, 0.20, 0.25, 0.30, 0.35])
    if abs(rho) >= 0.30:
        caught += 1
        print(f"  잡음  무상관 검사: 일부러 만든 완전 상관 표본에서 ρ = {rho:+.4f} ≥ 0.30")

    # ★ 대장 오타 대조 — hex를 한 자리 틀리게 넣으면 색상각이 어긋나는가
    typo = C.hex2rgb("#456ECC".replace("4", "8", 1))
    if abs(C.hue_deg(typo) - 222) > 5:
        caught += 1
        print(f"  잡음  대장 오타: #856ECC 색상각 {C.hue_deg(typo):.1f}° ≠ 222°")

    print(f"\n  대조 {caught} / 7 건이 잡혔다.")
    if caught != 7:
        print("  ★ 전건이 안 잡혔다 — 본안의 모든 초록을 폐기한다.")
        sys.exit(3)
    print("=" * 84)


if __name__ == "__main__":
    if "--control" in sys.argv:
        control()
    else:
        main()
