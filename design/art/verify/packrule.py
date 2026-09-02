# -*- coding: utf-8 -*-
"""팩 색 유도 규칙에 **자리 비움 제약**을 하나 더 건다 (design-art, 2026-09-02 3차)

지금 규칙:  주색 = 그 색상각에서 채도 최대 / 보조색 = 채도 최소  (상자 ∩ 자립 대역)
문제      : '채도 최소'가 여섯 팩을 전부 **탈채도 중심**으로 몰아넣는데, 카탈로그 25색도
            거기 산다. 그래서 팩을 옮겨도 다음 팩이 같은 자리에서 또 부딪힌다.

새 규칙   : 같은 탐색에 **"이미 자리 잡은 색과 ΔE >= 7.8"**을 조건으로 건다.
            사람이 hex를 고르는 자리는 여전히 없다. 조건이 하나 늘 뿐이다.

  python3 packrule.py
"""
import sys, os, itertools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL, band, derive_packs as DP
from packclash import load_current, INK_MARKERS, DISCERN, BRASS

LO, HI = band.limits()[0], band.limits()[1]
BD = band.BACKDROPS


def chroma(c):
    a = CL.lab(c)
    return (a[1] ** 2 + a[2] ** 2) ** 0.5


def pick(hue, want_max, avoid=(), gap=0.0):
    """derive_packs.pick과 같은 탐색 + 'avoid의 어떤 색과도 ΔE >= gap' 조건."""
    av = [CL.hex2rgb(x) for x in avoid]
    best = None
    for si in range(42, 101):
        for vi in range(55, 81):
            c = CL.hsv_to_rgb(hue / 360.0, si / 100.0, vi / 100.0)
            if not (LO <= CL.L(c) <= HI) or CL.worn(c) != c:
                continue
            if gap > 0 and any(CL.dE(c, a) < gap for a in av):
                continue
            k = chroma(c) if want_max else -chroma(c)
            bal = -abs(min(CL.CR(c, b) for _, b in BD) - 3.50)
            key = (round(k, 1), round(bal, 3))
            if best is None or key > best[0]:
                best = (key, c)
    return best[1] if best else None


def main():
    CL.calibrate()
    cur = load_current()
    cat = [h for h in cur if h not in INK_MARKERS]
    hues = [(n, h) for n, h, _ in DP.PACK_HUES]

    print("=" * 100)
    print("§1. 자리 비움 제약을 건 재유도 — 팩 순서대로 자리를 잡는다(결정적)")
    print("=" * 100)
    placed = list(cat) + [BRASS]
    rows = []
    for name, h in hues:
        p = pick(h, True, placed, DISCERN)
        if p is None:
            p = pick(h, True)          # 해가 없으면 제약 없이 (그리고 그렇다고 적는다)
            note_p = "★ 제약 아래 해 없음 — 무제약 값"
        else:
            note_p = ""
        placed.append(CL.rgb2hex(p))
        s = pick(h, False, placed, DISCERN)
        if s is None:
            s = pick(h, False); note_s = "★ 제약 아래 해 없음 — 무제약 값"
        else:
            note_s = ""
        placed.append(CL.rgb2hex(s))
        rows.append((name, h, CL.rgb2hex(p), CL.rgb2hex(s)))
        wp = min(CL.CR(p, b) for _, b in BD); ws = min(CL.CR(s, b) for _, b in BD)
        print(f"  {name:16s} {h:6.1f}°  주 {CL.rgb2hex(p)} (C* {chroma(p):5.1f} 최악 {wp:.2f}) {note_p}")
        print(f"  {'':16s} {'':6s}   보조 {CL.rgb2hex(s)} (C* {chroma(s):5.1f} 최악 {ws:.2f}) {note_s}")

    new_pk = [c for _, _, p, s in rows for c in (p, s)]
    old_pk = [c for _, _, p, s in DP.PACK_HUES and
              [(n, h, CL.rgb2hex(DP.pick(h, True)), CL.rgb2hex(DP.pick(h, False))) for n, h, _ in DP.PACK_HUES]
              for c in (p, s)]

    print("\n" + "=" * 100)
    print("§2. 옛 규칙 vs 새 규칙")
    print("=" * 100)
    for label, pkset in (("옛 규칙(무제약)", old_pk), ("★ 새 규칙(자리 비움)", new_pk)):
        pr = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a in cat for b in pkset)
        pp = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a, b in itertools.combinations(pkset, 2))
        Ls = [CL.L(CL.hex2rgb(c)) for c in pkset]
        worst = min(min(CL.CR(CL.hex2rgb(c), b) for _, b in BD) for c in pkset)
        ident = sum(1 for c in pkset if CL.worn(CL.hex2rgb(c)) == CL.hex2rgb(c))
        inband = sum(1 for c in pkset if LO <= CL.L(CL.hex2rgb(c)) <= HI)
        print(f"  {label:22s} 카탈로그↔팩 최소 ΔE {pr[0][0]:6.2f} (미달 {sum(1 for x in pr if x[0]<DISCERN)}쌍) | "
              f"팩내부 최소 {pp[0][0]:6.2f} | L {min(Ls):.4f}~{max(Ls):.4f} ({(max(Ls)+.05)/(min(Ls)+.05):.2f}:1) | "
              f"배경최악 {worst:.2f} | 항등 {ident}/12 | 대역 {inband}/12")
        for d, a, b in pr[:3]:
            print(f"      최근접: 카탈로그 {a} ↔ 팩 {b}  ΔE {d:.2f}")
    print("\n  주↔보조(같은 팩 안) ΔE — 세트 인지의 절반")
    for name, h, p, s in rows:
        print(f"    {name:16s} ΔE {CL.dE(CL.hex2rgb(p), CL.hex2rgb(s)):6.2f} "
              f"{'★ 아이템 내부 하한 7.8 미달' if CL.dE(CL.hex2rgb(p), CL.hex2rgb(s)) < DISCERN else ''}")

    print("\n" + "=" * 100)
    print("§3. 7번째 팩이 와도 자리가 있는가 — 새 규칙으로 전 각도 시험")
    print("=" * 100)
    ok = 0; fail = []
    for hdeg in range(0, 360, 5):
        p = pick(float(hdeg), True, placed, DISCERN)
        s = pick(float(hdeg), False, placed, DISCERN)
        if p is not None and s is not None: ok += 1
        else: fail.append(hdeg)
    print(f"  5° 간격 72개 각도 중 주·보조 둘 다 해가 있는 각도: {ok}/72")
    print(f"  해가 없는 각도: {fail if fail else '없음'}")

    print("\n  PACKS_V2 = [")
    for name, h, p, s in rows:
        print(f'      ("{name}", {h}, "{p}", "{s}", 그늘 "{CL.rgb2hex(CL.fill_outline(CL.hex2rgb(p)))}"),')
    print("  ]")


if __name__ == "__main__":
    main()
