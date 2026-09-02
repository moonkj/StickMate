# -*- coding: utf-8 -*-
"""휘도 순서가 무엇을 뜻하는가 — 실측 (design-art, 2026-09-02)

리더 질문: "같은 색상대 안의 휘도 순서가 우리 카탈로그에서 무엇을 뜻하는가."
문서를 믿지 않는다. **에셋을 파싱해서 잰다.**

  python3 lumorder.py

교정이 깨지면 아무 숫자도 내지 않고 죽는다(colorlab.calibrate).
"""
import json, os, re, subprocess, sys, math, statistics, itertools

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ITEMS = os.path.join(ROOT, "Assets/_Project/Resources/Items")

# 자립 대역 (PALETTE_SPEC §0)
BAND_LO, BAND_HI = 0.1632, 0.2396
BACKGROUNDS = [("흰 바탕화면", (255, 255, 255)), ("검은 바탕화면", (0, 0, 0)),
               ("종이 무대 #E9EAE6", CL.hex2rgb("#E9EAE6")), ("목탄 무대 #25282E", CL.hex2rgb("#25282E"))]
NONTEXT = 3.0
DISCERN = 7.8            # 변별 하한 (PALETTE_SPEC §3-3)

INK_MARKERS = {"#D6DBE3", "#8B939F"}   # ItemCatalog.InkTone / InkDimTone — 몸에서는 잉크로 대체된다

# ---------------------------------------------------------------- 에셋 파싱
def parse_asset(text, asset):
    out = []
    g = lambda p: (re.search(p, text, re.M).group(1) if re.search(p, text, re.M) else None)
    item_id, req = g(r"^  itemId: (.*)$"), g(r"^  requiredLevel: (\d+)$")
    slot, idx = g(r"^  slot: (-?\d+)$"), g(r"^  itemIndex: (-?\d+)$")
    for pi, p in enumerate(re.split(r"^  - kind: ", text, flags=re.M)[1:]):
        m = re.search(r"color: \{r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+), a: ([-\d.eE]+)\}", p)
        if not m: continue
        t = re.search(r"^    tone: (\d+)$", p, re.M)
        rgb = tuple(int(round(float(x) * 255)) for x in m.groups()[:3])
        out.append(dict(asset=asset, itemId=item_id or "", slot=int(slot), itemIndex=int(idx),
                        req=int(req) if req else None, part=pi, kind=int(p.split("\n", 1)[0]),
                        tone=int(t.group(1)) if t else 0, alpha=float(m.group(4)),
                        hex=CL.rgb2hex(rgb)))
    return out

def load(mode):
    rows = []
    for f in sorted(os.listdir(ITEMS)):
        if not f.endswith(".asset"): continue
        path = os.path.join(ITEMS, f)
        if mode == "new":
            text = open(path, encoding="utf-8").read()
        else:
            text = subprocess.run(["git", "-C", ROOT, "show",
                                   "HEAD:" + os.path.relpath(path, ROOT)],
                                  capture_output=True, text=True).stdout
        rows += parse_asset(text, f[:-6])
    return rows

# 코드 주석의 이름표 (ItemCatalog.cs 소재 팔레트 절) — 값은 에셋에서 확인한다
NAMES_NEW = dict(zip("Ivory Wool Felt Gold GoldLight Silver DarkLens Leather Canvas Paper Toy "
                     "HairBrown TintHead TintEyes TintNeck NeckDeep TintBack Accent".split(),
                     "#96814F #A66930 #5577AE #9B7922 #988540 #6183B4 #5075B5 #BA5928 #A26B2F "
                     "#6787B9 #CC423A #A16A28 #BE5B23 #20878C #4E8C2B #428C24 #955CCC #3378CC".split()))
NAMES_OLD = dict(zip("Ivory Wool Felt Gold GoldLight Silver DarkLens Leather Canvas Paper Toy "
                     "HairBrown TintHead TintEyes TintNeck NeckDeep TintBack Accent".split(),
                     "#E8E2D4 #C08F60 #8C96A6 #E8C15A #FFF0B8 #D3DAE4 #7C8AA3 #C9744A #C0925F "
                     "#EEF2F8 #E0574F #B8894F #E8834A #4FC0C6 #8CC06E #6FA957 #B08FD0 #5DA1F5".split()))

# 리더가 제시한 W=0.10 후보 (채도만 규칙 목표 둘레 ±0.10에서 푼 결과, 6색만 다름)
W010 = {"#BE5B23": "#CC5512", "#A66930": "#BA7636", "#A26B2F": "#AB7942",
        "#4E8C2B": "#5A8C3C", "#6183B4": "#587398", "#CC423A": "#C6443C"}

# ---------------------------------------------------------------- 측정 도구
def worst_bg(hx):
    rgb = CL.hex2rgb(hx)
    return min(CL.CR(rgb, bg) for _, bg in BACKGROUNDS)

def band_state(hx):
    l = CL.L(CL.hex2rgb(hx))
    return BAND_LO <= l <= BAND_HI

def stats(cols):
    """25색 300쌍 dE 통계 (몸 위 = worn 적용)."""
    w = [CL.worn(CL.hex2rgb(c)) for c in cols]
    ds = [CL.dE(a, b) for a, b in itertools.combinations(w, 2)]
    ds.sort()
    n = len(ds)
    p5 = ds[max(0, int(round(0.05 * (n - 1))))]
    return dict(n=n, mn=ds[0], med=statistics.median(ds), p5=p5,
                under=sum(1 for d in ds if d < DISCERN))

def hue_gap(a, b):
    d = abs(CL.hue_deg(CL.hex2rgb(a)) - CL.hue_deg(CL.hex2rgb(b))) % 360.0
    return min(d, 360.0 - d)

def main():
    CL.calibrate()
    new, old = load("new"), load("old")

    # --- 0. 옛->새 1:1 대응 (같은 asset/part) -------------------------------
    k = lambda r: (r["asset"], r["part"])
    mo = {k(r): r["hex"] for r in old}
    corr = {}
    for r in new:
        corr.setdefault(mo[k(r)], set()).add(r["hex"])
    multi = {a: b for a, b in corr.items() if len(b) != 1}
    print("=== 0. 옛 -> 새 대응 ===")
    print("  조각 %d개 / 고유색 옛 %d · 새 %d / 다중대응 %s"
          % (len(new), len({r['hex'] for r in old}), len({r['hex'] for r in new}),
             multi if multi else "없음(1:1)"))
    o2n = {a: list(b)[0] for a, b in corr.items()}
    label = {}
    inv_o, inv_n = {v: a for a, v in NAMES_OLD.items()}, {v: a for a, v in NAMES_NEW.items()}
    for o, n in o2n.items():
        label[n] = inv_o.get(o) or inv_n.get(n) or ("(무명:옛%s)" % o)

    cols_new = sorted({r["hex"] for r in new} - INK_MARKERS)
    cols_old = sorted({r["hex"] for r in old} - INK_MARKERS)
    print("  잉크 표식 2색 제외 -> 측정 대상 옛 %d · 새 %d색" % (len(cols_old), len(cols_new)))

    # --- 1. 대역 이탈 / 최악 대비 (리더 주장 재측정) --------------------------
    print("\n=== 1. 몸 위(WornColor 후) 자립 대역 이탈 · 배경 4종 최악 대비 ===")
    for tag, cols in (("출하 현행(HEAD)", cols_old), ("새 팔레트(작업트리)", cols_new)):
        wcols = [CL.rgb2hex(CL.worn(CL.hex2rgb(c))) for c in cols]
        outs = [(c, w, worst_bg(w)) for c, w in zip(cols, wcols) if worst_bg(w) < NONTEXT]
        worst = min((worst_bg(w), c, w) for c, w in zip(cols, wcols))
        print("  %-18s 대역 밖 %2d/%d · 최악 %s -> 몸 %s = %.2f:1"
              % (tag, len(outs), len(cols), worst[1], worst[2], worst[0]))
        ident = sum(1 for c in cols if CL.is_worn_fixed(CL.hex2rgb(c)))
        print("  %-18s WornColor 항등 %d/%d" % ("", ident, len(cols)))

    # --- 2. dE 통계 --------------------------------------------------------
    cols_w010 = [W010.get(c, c) for c in cols_new]
    print("\n=== 2. 몸 위 dE 통계 (25색 %d쌍) ===" % stats(cols_new)["n"])
    print("  %-18s %8s %8s %8s %8s" % ("", "최소", "중앙", "5퍼센타일", "<7.8 쌍"))
    for tag, cols in (("출하 현행", cols_old), ("새 팔레트 W=0.00", cols_new), ("후보 W=0.10", cols_w010)):
        s = stats(cols)
        print("  %-18s %8.2f %8.2f %8.2f %8d" % (tag, s["mn"], s["med"], s["p5"], s["under"]))

    # --- 3. W=0.10이 뒤집는 휘도 순위 --------------------------------------
    print("\n=== 3. W=0.10이 뒤집는 휘도 순위 (색상대 폭별) ===")
    Lm = {c: CL.L(CL.hex2rgb(c)) for c in cols_new}
    Lv = {c: CL.L(CL.hex2rgb(W010.get(c, c))) for c in cols_new}
    flips_by_band = {}
    for band in (15, 20, 25, 30, 40, 45, 60, 90, 360):
        fl = []
        for a, b in itertools.combinations(cols_new, 2):
            if hue_gap(a, b) > band: continue
            if (Lm[a] - Lm[b]) * (Lv[a] - Lv[b]) < 0:
                fl.append((a, b))
        flips_by_band[band] = fl
        print("  색상대 폭 ±%3d°  역전 %2d쌍" % (band, len(fl)))
    print()
    target = None
    for band, fl in flips_by_band.items():
        if len(fl) == 14: target = band
    print("  리더 보고 '14쌍'과 일치하는 폭: %s" % (("±%d°" % target) if target else "없음"))
    use = flips_by_band[target] if target else flips_by_band[30]
    print("\n  역전 쌍 전량 (폭 ±%d°):" % (target or 30))
    for a, b in sorted(use, key=lambda p: -abs(Lm[p[0]] - Lm[p[1]])):
        print("    %-22s %s(%s) L %.4f -> %.4f  |  %s(%s) L %.4f -> %.4f   [색상각차 %.1f°]"
              % ("", label.get(a, a), a, Lm[a], Lv[a], label.get(b, b), b, Lm[b], Lv[b], hue_gap(a, b)))

    # --- 4. 휘도 순서에 의미가 있는가 — 후보 3가지를 각각 잰다 ---------------
    print("\n=== 4. 휘도 순서가 뜻을 나르는가 ===")

    print("\n  4-1. 이름이 명시적으로 휘도를 주장하는 쌍 (Light/Deep)")
    for hi, lo in (("Gold", "GoldLight"), ("TintNeck", "NeckDeep")):
        a, b = NAMES_NEW[hi], NAMES_NEW[lo]
        oa, ob = NAMES_OLD[hi], NAMES_OLD[lo]
        va, vb = W010.get(a, a), W010.get(b, b)
        print("    %-10s %s L %.4f  vs  %-10s %s L %.4f   (옛: %.4f vs %.4f)"
              % (hi, a, CL.L(CL.hex2rgb(a)), lo, b, CL.L(CL.hex2rgb(b)),
                 CL.L(CL.hex2rgb(oa)), CL.L(CL.hex2rgb(ob))))
        print("       W=0.10 후: %s L %.4f  vs  %s L %.4f  -> 순서 %s"
              % (va, CL.L(CL.hex2rgb(va)), vb, CL.L(CL.hex2rgb(vb)),
                 "유지" if ((CL.L(CL.hex2rgb(a)) - CL.L(CL.hex2rgb(b))) *
                            (CL.L(CL.hex2rgb(va)) - CL.L(CL.hex2rgb(vb))) > 0) else "★역전"))

    print("\n  4-2. 한 아이템 안 tone0(주) vs tone1(보조) — 같은 물건의 두 부품")
    intra = []
    for asset in sorted({r["asset"] for r in new}):
        rs = [r for r in new if r["asset"] == asset]
        t0 = {r["hex"] for r in rs if r["tone"] == 0} - INK_MARKERS
        t1 = {r["hex"] for r in rs if r["tone"] == 1} - INK_MARKERS
        for a in sorted(t0):
            for b in sorted(t1):
                if a == b: continue
                intra.append((asset, a, b))
    print("    아이템 내 (주,보조) 서로 다른 색 쌍: %d건" % len(intra))
    higher = sum(1 for _, a, b in intra if CL.L(CL.hex2rgb(a)) > CL.L(CL.hex2rgb(b)))
    print("    그중 주색이 더 밝은 것 %d / 보조색이 더 밝은 것 %d  -> 방향 %s"
          % (higher, len(intra) - higher, "일관" if higher in (0, len(intra)) else "★불일관"))
    flipped = [(s, a, b) for s, a, b in intra
               if (CL.L(CL.hex2rgb(a)) - CL.L(CL.hex2rgb(b))) *
                  (CL.L(CL.hex2rgb(W010.get(a, a))) - CL.L(CL.hex2rgb(W010.get(b, b)))) < 0]
    print("    W=0.10이 뒤집는 아이템 내 쌍: %d건 %s"
          % (len(flipped), [(s, a, b) for s, a, b in flipped] if flipped else ""))
    for s, a, b in intra:
        la, lb_ = CL.L(CL.hex2rgb(a)), CL.L(CL.hex2rgb(b))
        va, vb = CL.L(CL.hex2rgb(W010.get(a, a))), CL.L(CL.hex2rgb(W010.get(b, b)))
        mark = "★역전" if (la - lb_) * (va - vb) < 0 else ""
        print("      %-28s 주 %s L %.4f -> %.4f | 보조 %s L %.4f -> %.4f  %s"
              % (s, a, la, va, b, lb_, vb, mark))

    print("\n  4-3. 휘도가 requiredLevel(획득 난도)과 상관하는가")
    # 아이템 대표색 = tone0 중 첫 조각
    reps = []
    for asset in sorted({r["asset"] for r in new}):
        rs = [r for r in new if r["asset"] == asset]
        prim = next((r["hex"] for r in rs if r["tone"] == 0 and r["hex"] not in INK_MARKERS), None)
        if prim is None or rs[0]["req"] is None: continue
        reps.append((asset, rs[0]["req"], prim, CL.L(CL.hex2rgb(prim))))
    if len(reps) >= 3:
        xs = [r[1] for r in reps]; ys = [r[3] for r in reps]
        mx, my = statistics.mean(xs), statistics.mean(ys)
        num = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
        den = math.sqrt(sum((x - mx) ** 2 for x in xs) * sum((y - my) ** 2 for y in ys))
        r = num / den if den else float("nan")
        print("    아이템 %d종 · requiredLevel vs 대표색 휘도 피어슨 r = %+.4f" % (len(reps), r))
        print("    (참고) 옛 팔레트에서의 같은 상관:")
        m_new2old = {v: kk for kk, v in o2n.items()}
        ys2 = [CL.L(CL.hex2rgb(m_new2old[p])) for _, _, p, _ in reps]
        my2 = statistics.mean(ys2)
        num2 = sum((x - mx) * (y - my2) for x, y in zip(xs, ys2))
        den2 = math.sqrt(sum((x - mx) ** 2 for x in xs) * sum((y - my2) ** 2 for y in ys2))
        print("      옛 r = %+.4f" % (num2 / den2 if den2 else float("nan")))
        lv = sorted({x for x in xs})
        print("    레벨별 대표색 휘도 (같은 레벨끼리 뭉치는가):")
        for L_ in lv:
            g = [y for x, y in zip(xs, ys) if x == L_]
            print("      lv%-3d n=%2d  휘도 %.4f ~ %.4f" % (L_, len(g), min(g), max(g)))

    # --- 5. 최근접 쌍 (색상각이 겹친 원죄) ----------------------------------
    print("\n=== 5. 최근접 쌍 — 색상각이 겹쳐 휘도로 못 가르는 것 ===")
    for tag, cols in (("출하 현행", cols_old), ("새 팔레트", cols_new)):
        w = {c: CL.worn(CL.hex2rgb(c)) for c in cols}
        ps = sorted(((CL.dE(w[a], w[b]), a, b) for a, b in itertools.combinations(cols, 2)))[:6]
        print("  [%s]" % tag)
        for d, a, b in ps:
            print("    dE %6.2f  %-24s %-24s 색상각차 %.2f°  (몸 %s / %s)"
                  % (d, "%s %s" % (label.get(a, inv_o.get(a, '?')), a) if tag == "새 팔레트"
                     else "%s %s" % (inv_o.get(a, label.get(o2n.get(a, ''), '?')), a),
                     "%s %s" % (label.get(b, inv_o.get(b, '?')), b) if tag == "새 팔레트"
                     else "%s %s" % (inv_o.get(b, label.get(o2n.get(b, ''), '?')), b),
                     hue_gap(a, b), CL.rgb2hex(w[a]), CL.rgb2hex(w[b])))

    # --- 6. 산문 정정용 실측 ------------------------------------------------
    print("\n=== 6. 산문 정정용 실측 ===")
    trap = "#7690CC"
    print("  함정색 %s: 종이 무대 #E9EAE6 대비 = %.4f:1" % (trap, CL.CR(CL.hex2rgb(trap), CL.hex2rgb("#E9EAE6"))))
    for nm, bg in BACKGROUNDS:
        print("      vs %-18s %.4f:1" % (nm, CL.CR(CL.hex2rgb(trap), bg)))
    print("  L(%s) = %.4f" % (trap, CL.L(CL.hex2rgb(trap))))

if __name__ == "__main__":
    main()
