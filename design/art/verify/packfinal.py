# -*- coding: utf-8 -*-
"""처방 C 확정 유도 + 민감도 (design-art, 2026-09-02 3차)  →  PALETTE_SPEC §13-3/4/6/8/9

  python3 packfinal.py

★ 폴백 없음. 제약 아래 해가 없으면 assert로 죽는다 (§13-4의 함정 방지).
"""
import sys, os, itertools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import colorlab as CL, band, derive_packs as DP
from packclash import load_current, INK_MARKERS, DISCERN, BRASS
from packrule import pick, chroma

GAP = 8.0
LO, HI = band.limits()[0], band.limits()[1]
BD = band.BACKDROPS
RAMP = ["#9C978C", "#BCAC8B", "#DBBD7F", "#F9CB70"]
HAIR = ["#A16A28", "#A86E1F", "#BD501F", "#AF651C"]
HUE_MOVE = {"#5577AE": "#5586AE", "#955CCC": "#A45CCC", "#587398": "#587698",
            "#C6443C": "#C64A3C", "#5075B5": "#5071B5", "#9B7922": "#9B7C22"}


def derive(catset, gap=GAP, order=None, strict=True):
    placed = list(catset) + [BRASS]
    rows = []
    for name, h, _ in (order or DP.PACK_HUES):
        p = pick(h, True, placed, gap)
        if p is None:
            if strict: return None, f"{name} 주색 해 없음"
            p = pick(h, True)
        placed.append(CL.rgb2hex(p))
        s = pick(h, False, placed, gap)
        if s is None:
            if strict: return None, f"{name} 보조색 해 없음"
            s = pick(h, False)
        placed.append(CL.rgb2hex(s))
        rows.append((name, h, CL.rgb2hex(p), CL.rgb2hex(s)))
    return rows, None


def stats(catset, rows):
    pk = [c for _, _, p, s in rows for c in (p, s)]
    pr = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a in catset for b in pk)
    pp = sorted((CL.dE(CL.hex2rgb(a), CL.hex2rgb(b)), a, b) for a, b in itertools.combinations(pk, 2))
    return pk, pr, pp


def main():
    CL.calibrate()
    cur = load_current()
    cat = [h for h in cur if h not in INK_MARKERS]

    print("=" * 96)
    print(f"§1. 확정 유도 (gap={GAP}) — 폴백 없음")
    print("=" * 96)
    rows, err = derive(cat)
    assert err is None, err
    print(f"{'팩':16s} {'H':>6s} {'주색':>9s} {'C*':>5s} {'L':>7s} {'최악':>5s} | "
          f"{'보조색':>9s} {'C*':>5s} {'L':>7s} {'최악':>5s} | {'주↔보조':>7s} | {'그늘':>9s}")
    for name, h, p, s in rows:
        P, S = CL.hex2rgb(p), CL.hex2rgb(s)
        print(f"{name:16s} {h:6.1f} {p:>9s} {chroma(P):5.1f} {CL.L(P):7.4f} "
              f"{min(CL.CR(P,b) for _,b in BD):5.2f} | {s:>9s} {chroma(S):5.1f} {CL.L(S):7.4f} "
              f"{min(CL.CR(S,b) for _,b in BD):5.2f} | {CL.dE(P,S):7.2f} | "
              f"{CL.rgb2hex(CL.fill_outline(P)):>9s}")

    pk, pr, pp = stats(cat, rows)
    Ls = [CL.L(CL.hex2rgb(c)) for c in pk]
    print(f"\n검산")
    print(f"  카탈로그({len(cat)})↔팩(12) 최소 ΔE {pr[0][0]:.2f} ({pr[0][1]} ↔ {pr[0][2]}) · "
          f"하한 {DISCERN} 미달 {sum(1 for x in pr if x[0] < DISCERN)}/{len(pr)}쌍")
    print(f"  팩 12색 내부 최소 ΔE {pp[0][0]:.2f} ({pp[0][1]} ↔ {pp[0][2]})")
    print(f"  L 범위 {min(Ls):.4f}~{max(Ls):.4f} · 최대/최소 대비 {(max(Ls)+.05)/(min(Ls)+.05):.2f}:1")
    print(f"  배경 4종 최악 {min(min(CL.CR(CL.hex2rgb(c), b) for _, b in BD) for c in pk):.2f}:1")
    print(f"  WornColor 항등 {sum(1 for c in pk if CL.worn(CL.hex2rgb(c))==CL.hex2rgb(c))}/12 · "
          f"자립 대역 {sum(1 for c in pk if LO <= CL.L(CL.hex2rgb(c)) <= HI)}/12")
    print(f"  브라스 최근접 ΔE {min(CL.dE(CL.hex2rgb(BRASS), CL.hex2rgb(c)) for c in pk):.2f}")
    print(f"  등급 램프 4색 최근접 ΔE {min(CL.dE(CL.hex2rgb(r), CL.hex2rgb(c)) for r in RAMP for c in pk):.2f}")
    old = {n: (CL.rgb2hex(DP.pick(h, True)), CL.rgb2hex(DP.pick(h, False))) for n, h, _ in DP.PACK_HUES}
    ch = [(n, old[n], (p, s)) for n, h, p, s in rows if old[n] != (p, s)]
    print(f"  옛 규칙 대비 바뀌는 색 {len(ch)}/12: " +
          ", ".join(f"{n} {o}→{ns}" for n, o, ns in ch))
    print(f"  색상각 이동 0건 · .asset 변경 0바이트")

    print("\n" + "=" * 96)
    print("§2. 여유(gap) 민감도 — ★ 크게 잡을수록 좋은 것이 아니다")
    print("=" * 96)
    for gap in (7.0, 7.8, 8.0, 8.2, 8.5, 9.0, 10.0, 12.0):
        r_strict, e = derive(cat, gap, strict=True)
        r_fb, _ = derive(cat, gap, strict=False)
        _, prf, _ = stats(cat, r_fb)
        tag = "해 있음" if e is None else f"★ {e}"
        print(f"  gap {gap:5.1f}  {tag:28s} | 폴백 허용 시 카탈로그↔팩 최소 ΔE {prf[0][0]:6.2f} "
              f"(미달 {sum(1 for x in prf if x[0] < DISCERN)}쌍)")
    print("  → 8.5부터 오피스 보조색에 해가 없다. 폴백을 두면 그때 조용히 무제약 값으로 돌아가")
    print("    **제약을 강화했는데 결과가 나빠지는** 거짓 신호가 난다. 그래서 폴백을 금지한다.")

    print("\n" + "=" * 96)
    print("§3. 팩 배치 순서 민감도")
    print("=" * 96)
    base = derive(cat)[0]
    for label, order in (("사양서 순", DP.PACK_HUES), ("역순", DP.PACK_HUES[::-1]),
                         ("이름순", sorted(DP.PACK_HUES)),
                         ("각도순", sorted(DP.PACK_HUES, key=lambda x: x[1]))):
        r, e = derive(cat, order=order)
        assert e is None, e
        d = {n: (p, s) for n, _, p, s in r}
        diff = [n for n, _, p, s in base if d[n] != (p, s)]
        _, prx, _ = stats(cat, r)
        print(f"  {label:8s} 사양서 순과 다른 팩: {diff if diff else '없음'} · 최소 ΔE {prx[0][0]:.2f}")

    print("\n" + "=" * 96)
    print("§4. 「정원」 갈래 — design-equipment의 머리카락 판정이 팩 색을 바꾸는가")
    print("=" * 96)
    for label, cs in (("현행 25색(머리 유지)", cat),
                      ("머리 병합 21색", [c for c in cat if c not in HAIR]),
                      ("§11-3 전체 21색", [HUE_MOVE.get(c, c) for c in cat if c not in HAIR])):
        r, e = derive(cs)
        assert e is None, e
        _, prx, _ = stats(cs, r)
        same = "★ 현행과 바이트 동일" if [x[2:] for x in r] == [x[2:] for x in base] else "다름"
        print(f"  {label:20s} 최소 ΔE {prx[0][0]:5.2f} · {same}")
        print(f"      " + " ".join(f"{p}/{s}" for _, _, p, s in r))

    print("\n" + "=" * 96)
    print("§5. 7번째 팩의 자리 (5° 간격 72각도)")
    print("=" * 96)
    placed = list(cat) + [BRASS] + pk
    ok, fail = 0, []
    for hd in range(0, 360, 5):
        if pick(float(hd), True, placed, GAP) and pick(float(hd), False, placed, GAP): ok += 1
        else: fail.append(hd)
    print(f"  주·보조 둘 다 해가 있는 각도 {ok}/72 · 막힌 각도 {fail if fail else '없음'}")

    print("\nPACKS_V2 (정본 후보)")
    for name, h, p, s in rows:
        print(f'    ("{name}", {h}, "{p}", "{s}"),')


if __name__ == "__main__":
    main()
