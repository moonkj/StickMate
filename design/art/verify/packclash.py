# -*- coding: utf-8 -*-
"""DLC 6팩 ↔ 카탈로그 색 충돌 판정 (design-art, 2026-09-02 3차 라운드)

리더 질의: "「컬러 잉크」 팩 주색 #8D56CC가 기본 아이템 TintBack #955CCC와 ΔE 4.26이다.
           TintBack +8.0°가 답인지, 팩 색을 옮길지 판정해라."

★ 문서의 hex를 베끼지 않는다. **지금 트리의 .asset을 파싱한다**(리더 지시).

  python3 packclash.py
"""
import sys, os, itertools, subprocess, re, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL
import band, derive_packs as DP

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ITEMS = os.path.join(ROOT, "Assets/_Project/Resources/Items")
INK_MARKERS = {"#D6DBE3", "#8B939F"}      # ItemCatalog.InkTone / InkDimTone — 색이 아니라 지시
DISCERN = 7.8
BRASS = "#C8A15A"


def load_current():
    """지금 트리의 .asset에서 고유색을 읽는다."""
    seen = collections.OrderedDict()
    for f in sorted(os.listdir(ITEMS)):
        if not f.endswith(".asset"): continue
        text = open(os.path.join(ITEMS, f), encoding="utf-8").read()
        for m in re.finditer(r"color: \{r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+), a: ([-\d.eE]+)\}", text):
            hx = CL.rgb2hex(tuple(int(round(float(x) * 255)) for x in m.groups()[:3]))
            seen.setdefault(hx, []).append(f[:-6])
    return seen


def pack_colors(hues):
    out = []
    for name, h in hues:
        p, s = DP.pick(h, True), DP.pick(h, False)
        out.append((name, h, CL.rgb2hex(p), CL.rgb2hex(s)))
    return out


def min_dE(hx, others):
    c = CL.hex2rgb(hx)
    return min(((CL.dE(c, CL.hex2rgb(o)), o) for o in others), default=(999, None))


def rotate(hx, dh):
    h, s, v = CL.rgb_to_hsv(CL.hex2rgb(hx))
    return CL.rgb2hex(CL.hsv_to_rgb(((h * 360.0 + dh) % 360.0) / 360.0, s, v))


def main():
    CL.calibrate()
    cur = load_current()
    cat = [h for h in cur if h not in INK_MARKERS]
    print(f"지금 트리 고유색 {len(cur)} (잉크 표식 {len(cur)-len(cat)} 제외 → 카탈로그 {len(cat)}색)")
    print("  " + " ".join(sorted(cat)))

    packs = pack_colors([(n, h) for n, h, _ in DP.PACK_HUES])
    pk = [c for _, _, p, s in packs for c in (p, s)]
    print(f"\n팩 12색 (derive_packs 규칙 그대로 재유도)")
    for n, h, p, s in packs:
        print(f"  {n:16s} {h:6.1f}°  주 {p}  보조 {s}")

    # ---------------------------------------------------------------- 1
    print("\n" + "=" * 78)
    print("§1. 카탈로그 ↔ 팩 최근접 (지금 트리 · 하한 7.8)")
    print("=" * 78)
    pairs = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a in cat for b in pk)
    for d, a, b in pairs[:10]:
        who = [n for n, _, p, s in packs if b in (p, s)][0]
        role = "주색" if any(b == p for _, _, p, _ in packs) else "보조색"
        print(f"  ΔE {d:6.2f}  카탈로그 {a} ({','.join(sorted(set(cur[a]))[:2])}…) "
              f"↔ {who} {role} {b} {'★하한 미달' if d < DISCERN else ''}")
    print(f"  하한 미달 쌍 {sum(1 for d,_,_ in pairs if d < DISCERN)} / {len(pairs)}")

    # ---------------------------------------------------------------- 2
    print("\n" + "=" * 78)
    print("§2. 두 처방 비교 — 카탈로그를 옮기는가, 팩을 옮기는가")
    print("=" * 78)
    ink_hue = 268.0
    ink_pri = [p for n, _, p, _ in packs if n == "컬러 잉크"][0]
    tintback = "#955CCC"
    print(f"  문제 쌍: 「컬러 잉크」 주색 {ink_pri} ↔ TintBack {tintback} "
          f"ΔE {CL.dE(CL.hex2rgb(ink_pri), CL.hex2rgb(tintback)):.2f}")
    print(f"  TintBack 실제 사용처: {sorted(set(cur.get(tintback, [])))}")

    print("\n  [처방 A] TintBack +8.0° (내 §11-3 안)")
    tb2 = rotate(tintback, 8.0)
    cat_a = [tb2 if c == tintback else c for c in cat]
    d, o = min_dE(tb2, pk)
    print(f"    {tintback} -> {tb2} · 팩 최근접 ΔE {d:.2f} ({o})")
    pa = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a in cat_a for b in pk)
    print(f"    카탈로그↔팩 최소 ΔE {pa[0][0]:.2f} ({pa[0][1]} ↔ {pa[0][2]}) · 하한 미달 "
          f"{sum(1 for x in pa if x[0] < DISCERN)}쌍")
    ca = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a, b in itertools.combinations(cat_a, 2))
    print(f"    카탈로그 내부 최소 ΔE {ca[0][0]:.2f} ({ca[0][1]} ↔ {ca[0][2]}) · 하한 미달 "
          f"{sum(1 for x in ca if x[0] < DISCERN)}쌍")
    print(f"    대역/항등 유지: L {CL.L(CL.hex2rgb(tb2)):.4f} "
          f"(대역 {band.limits()[0]:.4f}~{band.limits()[1]:.4f}) · "
          f"WornColor 항등 {CL.worn(CL.hex2rgb(tb2)) == CL.hex2rgb(tb2)}")

    print("\n  [처방 B] 「컬러 잉크」 팩 색상각 이동 — 전 각도 전수 탐색")
    others = [c for n, _, p, s in packs if n != "컬러 잉크" for c in (p, s)]
    fixed = cat + others + [BRASS]
    best = []
    for hdeg in range(0, 360):
        p, s = DP.pick(float(hdeg), True), DP.pick(float(hdeg), False)
        if p is None or s is None: continue
        ph, sh = CL.rgb2hex(p), CL.rgb2hex(s)
        m = min(min_dE(ph, fixed)[0], min_dE(sh, fixed)[0])
        best.append((m, hdeg, ph, sh))
    best.sort(reverse=True)
    print(f"    {'순위':>4s} {'H':>5s} {'주색':>9s} {'보조색':>9s} {'고정색과 최소 ΔE':>16s}")
    for i, (m, h, p, s) in enumerate(best[:8]):
        print(f"    {i+1:4d} {h:5d}° {p:>9s} {s:>9s} {m:16.2f}")
    print("    ...")
    cur268 = [x for x in best if x[1] == 268]
    print(f"    현재 268°: 고정색과 최소 ΔE {cur268[0][0]:.2f}  (순위 "
          f"{[x[1] for x in best].index(268)+1} / {len(best)})")
    # 보라 계열(250~300°)만 놓고 최선
    purple = [x for x in best if 250 <= x[1] <= 300]
    purple.sort(reverse=True)
    print(f"    보라 계열(250~300°) 최선: {purple[0][1]}° 주 {purple[0][2]} 보조 {purple[0][3]} "
          f"최소 ΔE {purple[0][0]:.2f}")
    for m, h, p, s in purple[:6]:
        d1, o1 = min_dE(p, fixed)
        print(f"      {h:3d}° 주 {p} 보조 {s}  최소 ΔE {m:5.2f}  (주색 최근접 {o1} {d1:.2f})")

    # ---------------------------------------------------------------- 3
    print("\n" + "=" * 78)
    print("§3. 처방 A+B 동시 적용")
    print("=" * 78)
    hb = purple[0][1]
    pb, sb = purple[0][2], purple[0][3]
    packs_b = [(n, h, (pb if n == "컬러 잉크" else p), (sb if n == "컬러 잉크" else s))
               for n, h, p, s in packs]
    pkb = [c for _, _, p, s in packs_b for c in (p, s)]
    for label, catset, pkset in (("현행 (아무것도 안 함)", cat, pk),
                                 ("A만 (TintBack +8°)", cat_a, pk),
                                 (f"B만 (팩 268°→{hb}°)", cat, pkb),
                                 (f"A+B", cat_a, pkb)):
        pr = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a in catset for b in pkset)
        pp = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a, b in itertools.combinations(pkset, 2))
        print(f"  {label:26s} 카탈로그↔팩 최소 ΔE {pr[0][0]:6.2f} (미달 "
              f"{sum(1 for x in pr if x[0]<DISCERN)}쌍) · 팩 내부 최소 ΔE {pp[0][0]:6.2f}")

    # ---------------------------------------------------------------- 4
    print("\n" + "=" * 78)
    print("§4. 정원 갈래 — design-equipment의 머리카락 판정에 따라 달라지는가")
    print("=" * 78)
    HAIR = ["#A16A28", "#A86E1F", "#BD501F", "#AF651C"]   # 폐기 후보 (§11-3)
    present = [h for h in HAIR if h in cur]
    print(f"  폐기 후보 4색 중 지금 트리에 있는 것: {present}")
    cat21 = [c for c in cat if c not in HAIR]
    cat21a = [c for c in cat_a if c not in HAIR]
    for label, catset, pkset in ((f"머리 유지({len(cat)}색) · 현행", cat, pk),
                                 (f"머리 병합({len(cat21)}색) · 현행", cat21, pk),
                                 (f"머리 병합({len(cat21a)}색) · A", cat21a, pk),
                                 (f"머리 병합({len(cat21)}색) · B", cat21, pkb),
                                 (f"머리 병합({len(cat21a)}색) · A+B", cat21a, pkb)):
        pr = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a in catset for b in pkset)
        ci = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a, b in itertools.combinations(catset, 2))
        print(f"  {label:28s} 카탈로그내부 최소 {ci[0][0]:6.2f} (미달 {sum(1 for x in ci if x[0]<DISCERN):2d}) "
              f"| 카탈로그↔팩 최소 {pr[0][0]:6.2f} (미달 {sum(1 for x in pr if x[0]<DISCERN):2d})")

    # ---------------------------------------------------------------- 5
    print("\n" + "=" * 78)
    print("§5. 「컬러 잉크」가 각도를 돌리면 — 고정 처방으로 막을 수 있는가")
    print("=" * 78)
    hits = 0
    worst = []
    for hdeg in range(0, 360):
        p = DP.pick(float(hdeg), True)
        if p is None: continue
        d, o = min_dE(CL.rgb2hex(p), cat)
        worst.append((d, hdeg, CL.rgb2hex(p), o))
        if d < DISCERN: hits += 1
    print(f"  유저가 고를 수 있는 색상각 {len(worst)}개 중 카탈로그와 ΔE<7.8이 되는 각도: {hits}개 "
          f"({100.0*hits/len(worst):.1f}%)")
    worst.sort()
    print("  가장 심한 5개:")
    for d, h, p, o in worst[:5]:
        print(f"    {h:3d}° 주색 {p} ↔ 카탈로그 {o} ΔE {d:.2f}")
    print("  → 팩 색을 어디로 옮겨도 **유저가 그 각도로 돌리면** 다시 붙는다.")
    print("     그러므로 처방의 대상은 '팩이 낼 수 있는 모든 색'이 아니라 **간판(기본) 색 하나**다.")


if __name__ == "__main__":
    main()
