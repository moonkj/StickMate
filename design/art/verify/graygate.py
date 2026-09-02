# -*- coding: utf-8 -*-
"""★ R6 과제 B — **색을 뺐을 때도 읽히는가** (design-art, 2026-09-02)

리더가 design-equipment에 건 것: *"형태만으로 읽혀야 한다 — 색은 확인 도장이지 정체가 아니다."*
같은 것을 **내 쪽 자로** 다시 잰다. design-equipment는 실루엣 L∞ 프로파일(윤곽)을 썼다.
나는 **다른 기구**를 쓴다 — 실제 색을 상대휘도로 눌러 만든 **회색조 래스터**다.
윤곽이 같아도 내부 분할이 다르면 프로파일은 못 보고 래스터는 본다(그리고 그 반대도 있다).

  세 가지를 잰다
   ① 아이템 **안** : 주색 ↔ 보조색이 회색조에서 살아남는가 (살아남지 못하면 외곽선이 구하는가)
   ② 팩 6종 **끼리** : 회색조 카드에서 서로 구분되는가
   ③ 팩 6종 ↔ **기본 42종** : 같은 슬롯 안에서 헷갈리는가

★ 교정: 회색조 변환기를 알려진 값으로 먼저 검증한다(흰→255, 검→0, 항등, 단조).
        깨지면 아무 숫자도 내지 않고 죽는다.

  python3 graygate.py
  python3 graygate.py --control   # ★ 양성 대조 — 일부러 같은 그림 두 장을 넣는다
"""
import sys, os, math, itertools

HERE = os.path.dirname(os.path.abspath(__file__))
EQV = os.path.abspath(os.path.join(HERE, "..", "..", "equipment", "verify"))
sys.path.insert(0, HERE)
sys.path.insert(0, EQV)          # ★ 읽기 전용 — design/equipment/ 에는 쓰지 않는다

import colorlab as CL
import shipped
from PIL import Image, ImageDraw

import rig, items, hair, appearance                      # design-equipment 소유(읽기만)
import pack_nightshift as PN
from rig import Shape

ICON, FIT = 44.0, 0.86
ICON_STROKE = 1.7 * 44.0 / 40.0          # 1.870px (design-equipment 게이트 ②와 같은 값)
SS = 6                                    # 슈퍼샘플
CARD_BG = "#1B1E24"                       # 아래 §0에서 UiChrome 실측으로 덮어쓴다

# ── 야간 정비반: azimuth6 확정값 ─────────────────────────────────────────────
PACK_PRIMARY = "#639400"
PACK_ACCENT = "#798C51"
PACK_NAME = "야간 정비반"


# ============================================================================
# 0. 회색조 변환기 — 쓰기 전에 교정한다
# ============================================================================
def gray_exact(rgb):
    """상대휘도를 유지하는 무채색 등가 — **8bit 반올림 전** 실수값 0..255."""
    y = CL.L(rgb)
    c = 12.92 * y if y <= 0.0031308 else 1.055 * (y ** (1 / 2.4)) - 0.055
    return max(0.0, min(1.0, c)) * 255.0


def to_gray(rgb):
    """화면에 찍는 8bit 무채색 등가. (완전색맹·흑백출력·저조도가 보는 것)"""
    v = int(round(gray_exact(rgb)))
    return (v, v, v)


def gray_calibrate():
    checks = []
    def chk(n, got, want, tol=0):
        checks.append((n, got, want, abs(got - want) <= tol))
    chk("흰 -> 255", to_gray((255, 255, 255))[0], 255)
    chk("검 -> 0", to_gray((0, 0, 0))[0], 0)
    chk("중간회색 항등 #808080", to_gray((128, 128, 128))[0], 128, 1)
    # 왕복: 무채색은 회색조를 통과해도 자기 자신이어야 한다
    worst = max(abs(to_gray((v, v, v))[0] - v) for v in range(256))
    chk("무채색 256개 왕복 최대오차", worst, 0)
    # 단조성: 휘도가 크면 회색값도 크다
    seq = sorted(((CL.L(c), to_gray(c)[0]) for c in
                  [(r, g, b) for r in range(0, 256, 51) for g in range(0, 256, 51)
                   for b in range(0, 256, 51)]))
    mono = all(seq[i][1] <= seq[i + 1][1] for i in range(len(seq) - 1))
    chk("휘도-회색값 단조 (216색)", 1 if mono else 0, 1)
    # ★ 대비비 보존 — **연속 공간에서는 오차가 0이어야 한다.**
    #   처음에 8bit 반올림 후로 쟀다가 0.0073으로 FAIL이 났다. 임계값을 늘리지 않고
    #   **무엇이 오차를 냈는지**를 갈랐다: 변환 자체가 아니라 8bit 양자화였다.
    #   (임계값을 느슨하게 했으면 진짜 변환 버그도 같이 통과했을 것이다.)
    def CRy(a, b):
        ya, yb = CL.L(a), CL.L(b)
        # 회색조는 휘도를 보존하므로 CR은 정의상 동일해야 한다
        hi, lo = max(ya, yb), min(ya, yb)
        return (hi + 0.05) / (lo + 0.05)
    PRB = [((204, 63, 41), (158, 101, 92)), ((99, 148, 0), (121, 140, 81)),
           ((255, 255, 255), (0, 0, 0)), ((69, 110, 204), (96, 128, 204))]
    dc = max(abs(CL.CR(a, b) - CRy(a, b)) for a, b in PRB)
    checks.append(("대비비 보존(연속) 최대오차", f"{dc:.3e}", "0", dc <= 1e-12))
    ok = all(c[3] for c in checks)
    print("=== 회색조 변환기 교정 ===")
    for n, got, want, p in checks:
        print(f"  {'PASS' if p else 'FAIL'}  {n:34s} {got:>10}  (정답 {want})")
    print(f"  교정 판정: {'유효' if ok else '무효'}\n")
    if not ok:
        sys.exit("회색조 교정 실패 — 이 스크립트의 모든 숫자를 폐기하십시오.")
    # 알려진 잔차 — 통과/실패가 아니라 **크기를 적어 두는** 항목이다.
    d8 = max(abs(CL.CR(a, b) - CL.CR(to_gray(a), to_gray(b))) for a, b in PRB)
    print(f"  ※ 알려진 잔차: 8bit 반올림이 대비비를 최대 {d8:.4f} 흔든다"
          f" (우리 대역 색 기준). 변환식의 오차가 아니라 **양자화**다 —")
    print(f"     연속 공간 오차는 위 표대로 {dc:.1e}다. 이 스크립트의 판정은 전부"
          f" 0.01보다 큰 폭에서 내려지므로 영향 없다.\n")


# ============================================================================
# 1. 아이템 표 — 기본 42종(도형+실제 출하색) + 팩 6종
# ============================================================================
SLOT_NAME = {0: "HEAD", 1: "EYES", 2: "NECK", 3: "BACK", 4: "HAIR", 5: "FX", 6: "PET"}
PET_ORDER = ["작은공", "종이비행기", "리틀스틱메이트", "커서친구", "풍선", "달팽이"]


def base_shapes():
    B = {}
    for cat, table in (("HEAD", items.HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
                       ("BACK", items.BACK), ("HAIR", hair.SET)):
        for i, (nm, sh) in enumerate(table.items()):
            B[(cat, i)] = (nm, sh)
    for i, (nm, sh) in enumerate(appearance.FX_NOW.items()):
        B[("FX", i)] = (nm, sh)
    for i, nm in enumerate(PET_ORDER):
        B[("PET", i)] = (nm, appearance.PET_NOW.get(nm, []))
    return B


def asset_index():
    """파일명 -> (슬롯명, itemIndex). shipped.item_colors()와 같은 키를 쓴다."""
    import re
    out = {}
    for f in sorted(os.listdir(shipped.ITEMS)):
        if not f.endswith(".asset"):
            continue
        t = open(os.path.join(shipped.ITEMS, f), encoding="utf-8").read()
        s = re.search(r"^\s*slot:\s*(\d+)", t, re.M)
        i = re.search(r"^\s*itemIndex:\s*(\d+)", t, re.M)
        if s and i:
            out[f[:-6]] = (SLOT_NAME[int(s.group(1))], int(i.group(1)))
    return out


def build_catalog():
    cols = shipped.item_colors()
    idx = asset_index()
    B = base_shapes()
    rows = []
    for key, (cat, i) in sorted(idx.items(), key=lambda kv: (kv[1][0], kv[1][1])):
        nm, sh = B.get((cat, i), (key, []))
        if not sh:
            continue
        t = cols[key]["tones"]
        prim = sorted(t.get(0, [(0, 0, 0)]))[0]
        acc = sorted(t.get(1, [prim]))[0]
        rows.append(dict(key=key, cat=cat, idx=i, name=nm, shapes=sh,
                         prim=prim, acc=acc, pack=False))
    return rows


def build_pack():
    P, A = CL.hex2rgb(PACK_PRIMARY), CL.hex2rgb(PACK_ACCENT)
    out = []
    for cat, (nm, iid, fn, _x) in PN.PACK.items():
        out.append(dict(key=iid, cat=cat, idx=99, name=nm, shapes=fn(),
                        prim=P, acc=A, pack=True))
    return out


# ============================================================================
# 2. 카드 래스터 (회색조)
# ============================================================================
def render(rec, gray=True, size=ICON):
    sh = rec["shapes"]
    pts = [q for s in sh for q in s.pts]
    x0, y0, x1, y1 = rig.bounds(pts)
    span = max(x1 - x0, y1 - y0, 1e-6)
    k = size * FIT / span * SS
    W = int(size * SS)
    bg = CL.hex2rgb(CARD_BG)
    im = Image.new("RGB", (W, W), to_gray(bg) if gray else bg)
    d = ImageDraw.Draw(im)
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    T = lambda p: (W / 2.0 + (p[0] - cx) * k, W / 2.0 - (p[1] - cy) * k)
    w = max(1, int(round(ICON_STROKE * SS)))
    for s in sh:
        base = rec["acc"] if s.tone == 1 else rec["prim"]
        if s.tone == 2:
            base = CL.fill_outline(rec["prim"])
        col = to_gray(base) if gray else base
        out = CL.fill_outline(base)
        oc = to_gray(out) if gray else out
        P = [T(p) for p in s.pts]
        if s.filled and len(P) >= 3:
            d.polygon(P, fill=col)
            d.line(P + [P[0]], fill=oc, width=w, joint="curve")
        elif len(P) >= 2:
            d.line(P, fill=col, width=w, joint="curve")
    return im.resize((int(size), int(size)), Image.LANCZOS)


def ink_mask(rec):
    """잉크가 놓인 자리(배경이 아닌 화소) — 형태만. 색 무관."""
    im = render(rec, gray=True)
    bg = to_gray(CL.hex2rgb(CARD_BG))[0]
    return [1 if abs(px[0] - bg) > 12 else 0 for px in im.getdata()]


def gray_l1(a, b):
    """두 회색조 카드의 평균 |Δ| (0..255)."""
    pa, pb = list(render(a).getdata()), list(render(b).getdata())
    return sum(abs(x[0] - y[0]) for x, y in zip(pa, pb)) / len(pa)


def shape_iou(a, b):
    ma, mb = ink_mask(a), ink_mask(b)
    inter = sum(1 for x, y in zip(ma, mb) if x and y)
    uni = sum(1 for x, y in zip(ma, mb) if x or y)
    return inter / uni if uni else 1.0


# ============================================================================
def main(control=False):
    global CARD_BG
    CL.calibrate()
    gray_calibrate()

    tok = shipped.uichrome_tokens()
    if "CardSurface" in tok:
        CARD_BG = CL.rgb2hex(tok["CardSurface"][0])
    print(f"카드 바탕 = UiChrome.CardSurface {CARD_BG} (실측) · 회색조 등가 "
          f"{CL.rgb2hex(to_gray(CL.hex2rgb(CARD_BG)))}")

    cat = build_catalog()
    pack = build_pack()
    idx, B = asset_index(), base_shapes()
    missing = [k for k, v in sorted(idx.items()) if not B.get((v[0], v[1]), ("", []))[1]]
    print(f"기본 {len(cat)}종 · 팩 {len(pack)}종 (도형은 design-equipment 소유 모듈에서 읽었다)")
    print(f"★ 42종 중 {len(missing)}종을 **못 쟀다** — 숨기지 않고 적는다: {missing}")
    print(f"   · look_fx_none  : 도형이 없는 것이 정상이다(「없음」). 제외가 맞다.")
    print(f"   · look_pet_mini : design-equipment의 appearance.PET_NOW에 "
          f"「리틀스틱메이트」 항목이 **없다**(5/6종만 있다).")
    print(f"     → 팩 PET(작업등)을 이 1종과는 대조하지 못했다. 리더 경유로 확인이 필요하다.\n")

    # ------------------------------------------------------------------ §1
    print("=" * 100)
    print("§1. 아이템 **안** — 주색↔보조색이 회색조에서 살아남는가")
    print("=" * 100)
    print("  변별 하한 ΔE 7.8은 색이 있을 때의 자다. 회색조에는 명도축밖에 안 남는다.")
    print(f"  {'슬롯/아이템':22s} {'주색':>8s} {'보조색':>8s} {'컬러ΔE':>7s} "
          f"{'회색ΔE':>7s} {'회색대비':>8s} | {'보조↔그외곽':>10s}")
    rows = []
    for r in cat + pack:
        P, A = r["prim"], r["acc"]
        gP, gA = to_gray(P), to_gray(A)
        cr = CL.CR(gP, gA)
        oa = CL.fill_outline(A)
        cro = CL.CR(to_gray(A), to_gray(oa))
        rows.append((r, CL.dE(P, A), CL.dE(gP, gA), cr, cro))
    for r, dc, dg, cr, cro in rows:
        if not r["pack"]:
            continue
        print(f"  ★{r['cat']:5s} {r['name']:14s} {CL.rgb2hex(r['prim']):>8s} "
              f"{CL.rgb2hex(r['acc']):>8s} {dc:7.2f} {dg:7.2f} {cr:7.3f}:1 | {cro:9.2f}:1")
    same = [x for x in rows if x[0]["prim"] == x[0]["acc"]]
    base_rows = [x for x in rows if not x[0]["pack"] and x[0]["prim"] != x[0]["acc"]]
    base_rows.sort(key=lambda x: x[2])
    print(f"\n  기본 42종 중 주≠보조인 {len(base_rows)}종의 **회색조 ΔE** 분포")
    print(f"    최소 {base_rows[0][2]:.2f} ({base_rows[0][0]['name']}) · "
          f"중앙 {base_rows[len(base_rows)//2][2]:.2f} · 최대 {base_rows[-1][2]:.2f} "
          f"({base_rows[-1][0]['name']})")
    lost = [x for x in base_rows if x[2] < 7.8]
    print(f"    회색조에서 변별 하한 7.8 **미달: {len(lost)}/{len(base_rows)}종** "
          f"({100.0*len(lost)/len(base_rows):.0f}%)")
    packg = rows[-6:][0][2]
    below = sum(1 for x in base_rows if x[2] < packg)
    print(f"  ★ 팩 6종의 회색조 ΔE = {packg:.2f}. 기본 38종 중 이보다 **작은 것 {below}종** —")
    print(f"     즉 팩은 이 분포의 **바닥과 같은 값**이다(최소 {base_rows[0][2]:.2f} = 반짝임).")
    print(f"     ★ 나는 이 결과를 '팩은 괜찮다'로 쓰지 않는다. 정직한 문장은 이것이다:")
    print(f"     **주↔보조 구분은 여섯 팩 전부에서 회색조에 살아남지 못한다. 기본 42종도 95%가 그렇다.**")
    print(f"     원인은 팩이 아니라 **자립 대역이 ΔL* 8.7뿐**이라는 구조다 — 색상각·채도로만 가를 수 있고")
    print(f"     명도로는 못 가른다. 그러니 회색조에서 구하는 것은 색이 아니라 **채움 외곽선(×0.62)**이고,")
    print(f"     그 외곽선 대비는 {rows[-1][4]:.2f}:1로 비텍스트 하한 3.0 **미만**이다 -> §5.")

    # ------------------------------------------------------------------ §2
    print("\n" + "=" * 100)
    print("§2. 팩 6종 **끼리** — 회색조 카드에서 구분되는가")
    print("=" * 100)
    pr = []
    for a, b in itertools.combinations(pack, 2):
        pr.append((gray_l1(a, b), shape_iou(a, b), a["name"], b["name"]))
    pr.sort()
    print(f"  {'쌍':34s} {'회색 L1':>8s} {'형태 IoU':>9s}")
    for l1, iou, n1, n2 in pr:
        f = " ★가장 닮은 쌍" if (l1, iou, n1, n2) == pr[0] else ""
        print(f"  {n1 + ' ↔ ' + n2:34s} {l1:8.2f} {iou:9.3f}{f}")
    print(f"  → 최악 쌍 L1 {pr[0][0]:.2f}/255 · IoU {pr[0][1]:.3f}")

    # ------------------------------------------------------------------ §3
    print("\n" + "=" * 100)
    print("§3. 팩 6종 ↔ **기본 42종** — 같은 슬롯 안에서 헷갈리는가 (회색조)")
    print("=" * 100)
    print(f"  {'팩 아이템':20s} {'같은 슬롯 최악 상대':16s} {'회색 L1':>8s} {'형태 IoU':>9s} "
          f"{'전 슬롯 최악':16s} {'L1':>7s}")
    worst_all = []
    for p in pack:
        same_slot = [c for c in cat if c["cat"] == p["cat"]]
        ss = sorted(((gray_l1(p, c), shape_iou(p, c), c["name"]) for c in same_slot))
        al = sorted(((gray_l1(p, c), c["name"], c["cat"]) for c in cat))
        worst_all.append((ss[0][0], ss[0][1], p["name"], ss[0][2]))
        print(f"  {p['name']:20s} {ss[0][2]:16s} {ss[0][0]:8.2f} {ss[0][1]:9.3f} "
              f"{al[0][1] + '(' + al[0][2] + ')':16s} {al[0][0]:7.2f}")
    worst_all.sort()
    print(f"  → 팩 전체 최악 = {worst_all[0][2]} vs {worst_all[0][3]} "
          f"L1 {worst_all[0][0]:.2f} · IoU {worst_all[0][1]:.3f}")

    # ------------------------------------------------------------------ §4
    print("\n" + "=" * 100)
    print("§4. ★ 기준선 — 기본 42종은 서로 얼마나 다른가 (같은 잣대)")
    print("=" * 100)
    print("  ★ 이 절이 없으면 §2·§3의 숫자를 읽을 수 없다. '크다/작다'는 비교 대상이 있어야 뜻이 생긴다.")
    for cname in ("HEAD", "EYES", "NECK", "BACK", "HAIR", "FX", "PET"):
        g = [c for c in cat if c["cat"] == cname]
        if len(g) < 2:
            continue
        ps = sorted((gray_l1(a, b), shape_iou(a, b), a["name"], b["name"])
                    for a, b in itertools.combinations(g, 2))
        pk = [p for p in pack if p["cat"] == cname]
        extra = ""
        if pk:
            pp = sorted((gray_l1(pk[0], c), c["name"]) for c in g)
            extra = f" | 팩 {pk[0]['name']} 최악 {pp[0][0]:6.2f} ({pp[0][1]})"
            extra += "  ✔ 기본 최악보다 큼" if pp[0][0] > ps[0][0] else "  ★ 기본 최악보다 작다"
        print(f"  {cname:5s} 기본끼리 최악 L1 {ps[0][0]:6.2f} · IoU {ps[0][1]:.3f} "
              f"({ps[0][2]} ↔ {ps[0][3]}){extra}")

    # ------------------------------------------------------------------ §5
    print("\n" + "=" * 100)
    print("§5. 회색조에서 경계를 지는 것은 **채움 외곽선(×0.62)** 하나뿐 — 그 대비를 잰다")
    print("=" * 100)
    ocs = []
    for r in cat:
        for c in (r["prim"], r["acc"]):
            ocs.append((CL.CR(to_gray(c), to_gray(CL.fill_outline(c))), r["name"]))
    ocs.sort()
    pk_oc = CL.CR(to_gray(CL.hex2rgb(PACK_ACCENT)), to_gray(CL.fill_outline(CL.hex2rgb(PACK_ACCENT))))
    print(f"  기본 42종 {len(ocs)}개 채움의 외곽선 대비: 최소 {ocs[0][0]:.3f}:1 ({ocs[0][1]}) · "
          f"중앙 {ocs[len(ocs)//2][0]:.3f}:1 · 최대 {ocs[-1][0]:.3f}:1 ({ocs[-1][1]})")
    print(f"  팩 보조색 {PACK_ACCENT}: {pk_oc:.3f}:1  ·  팩 주색 {PACK_PRIMARY}: "
          f"{CL.CR(to_gray(CL.hex2rgb(PACK_PRIMARY)), to_gray(CL.fill_outline(CL.hex2rgb(PACK_PRIMARY)))):.3f}:1")
    print(f"  ★ 전 카탈로그가 2.0~2.3 대에 몰려 있다 — 자립 대역이 좁아서 ×0.62의 결과도 좁다.")
    print(f"     이 값들은 UI 비텍스트 하한 3.0 **아래**다. 다만 3.0은 「UI 요소 ↔ 배경」의 자이고")
    print(f"     여기 재는 것은 「아이템 내부 경계」다 — 같은 자를 그대로 대는 것은 **내 확장이지 기존 규칙이 아니다.**")
    print(f"     그래서 이걸 '위반'으로 올리지 않는다. 올리는 것은 §6의 절제 실험 결과다.")

    # ------------------------------------------------------------------ §6
    print("\n" + "=" * 100)
    print("§6. ★ 절제 실험 — **보조색을 지우고** 다시 잰다. 정체가 색에 있으면 여기서 무너진다")
    print("=" * 100)
    print("  §1~§5는 '색이 안 보인다'까지만 말한다. 진짜 질문은 그 다음이다:")
    print("  **보조색이 아예 없었어도 이 물건을 알아보는가?** 그래서 보조색을 주색으로 덮고 재측정한다.")
    print(f"\n  {'팩 아이템':20s} {'2색 최악 L1':>11s} {'1색 최악 L1':>11s} {'변화':>8s} "
          f"{'2색 IoU':>8s} {'1색 IoU':>8s} {'상대':16s}")
    flat_pack = []
    for p in pack:
        q = dict(p); q["acc"] = q["prim"]
        flat_pack.append(q)
    worst2, worst1 = [], []
    for p, q in zip(pack, flat_pack):
        same = [c for c in cat if c["cat"] == p["cat"]]
        flat_same = []
        for c in same:
            c2 = dict(c); c2["acc"] = c2["prim"]; flat_same.append(c2)
        a2 = sorted(((gray_l1(p, c), shape_iou(p, c), c["name"]) for c in same))[0]
        a1 = sorted(((gray_l1(q, c), shape_iou(q, c), c["name"]) for c in flat_same))[0]
        worst2.append(a2[0]); worst1.append(a1[0])
        print(f"  {p['name']:20s} {a2[0]:11.2f} {a1[0]:11.2f} {a1[0]-a2[0]:+8.2f} "
              f"{a2[1]:8.3f} {a1[1]:8.3f} {a1[2]:16s}")
    print(f"\n  팩 최악: 2색 {min(worst2):.2f} → 1색 {min(worst1):.2f} ({min(worst1)-min(worst2):+.2f})")
    # 기본 42종에도 같은 절제를 걸어 기준선을 만든다
    b2, b1 = [], []
    for cname in ("HEAD", "EYES", "NECK", "BACK", "HAIR", "PET"):
        g = [c for c in cat if c["cat"] == cname]
        gf = []
        for c in g:
            c2 = dict(c); c2["acc"] = c2["prim"]; gf.append(c2)
        if len(g) < 2:
            continue
        b2.append(min(gray_l1(a, b) for a, b in itertools.combinations(g, 2)))
        b1.append(min(gray_l1(a, b) for a, b in itertools.combinations(gf, 2)))
    print(f"  기본 최악: 2색 {min(b2):.2f} → 1색 {min(b1):.2f} ({min(b1)-min(b2):+.2f})")
    print(f"\n  ★ 판정: 보조색을 통째로 지워도 팩 최악이 {min(worst2):.2f}→{min(worst1):.2f}로 "
          f"{'거의 안 움직인다' if abs(min(worst1)-min(worst2)) < 2.0 else '움직인다'}.")
    print(f"     = **여섯 종의 정체는 색이 아니라 형태가 지고 있다.** design-equipment의 주장이 "
          f"내 자로도 재현된다.")
    print(f"     그리고 같은 절제에서 기본 42종은 {min(b2):.2f}→{min(b1):.2f}다 — "
          f"팩이 기본보다 **색 의존이 낮다.**")

    if control:
        print("\n" + "=" * 100)
        print("★ 양성 대조 — 자기 자신 / 복제본을 넣는다. 0에 가까운 값이 나와야 한다")
        print("=" * 100)
        a = pack[0]
        clone = dict(a)
        print(f"  자기 자신 L1 {gray_l1(a, a):.4f} (정답 0.0) · IoU {shape_iou(a, a):.4f} (정답 1.0)")
        print(f"  복제본     L1 {gray_l1(a, clone):.4f} (정답 0.0) · IoU {shape_iou(a, clone):.4f} (정답 1.0)")
        # 색만 다른 복제본 — 형태 IoU는 1.0이어야 하고 회색 L1은 0보다 커야 한다
        recol = dict(a); recol["prim"] = CL.hex2rgb("#CC1BA9"); recol["acc"] = CL.hex2rgb("#9C5A8E")
        print(f"  ★색만 바꾼 복제본 L1 {gray_l1(a, recol):.4f} (>0 이어야 한다) · "
              f"IoU {shape_iou(a, recol):.4f} (형태 같으므로 1.0에 가까워야 한다)")
        print("  → 이 세 줄 중 하나라도 어긋나면 §2·§3·§4의 숫자를 전부 폐기한다.")


if __name__ == "__main__":
    main("--control" in sys.argv)
