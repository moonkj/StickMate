"""R26 — 게이트 FG-1 ~ FG-8. 현행과 제안을 같은 자로 잰다."""
import math
import numpy as np
from scipy import ndimage
from fanglyph import *
from fanglyph import _ax
from r26_new import (NEW, W, G0, G1, G2, FIELD_R, FIELD_R_HARD, GAP_MIN, FILL_MIN,
                     NEW_ACCENT, NEW_POWER_GAP_DEG)

LADDER = {round(G1, 3): "g1×0.75", round(G0, 3): "g0×1.00", round(G2, 3): "g2×1.50"}
CUR_ACCENT = {"③ 할일(체크리스트)": ("CheckShort", "CheckLong")}
AREA_LO, AREA_HI = 12.0, 26.0        # 잉크 면적 % (24pt 상자)
DIAG_LO, DIAG_HI = 24.0, 32.0        # 잉크 상자 대각 pt


def rmax_of(union):
    ys, xs = np.where(union)
    return float(np.hypot(_ax[xs], -_ax[ys]).max())


def components(masks):
    """겹치는 조각끼리 묶는다 — 한 덩어리 안의 pt 간극은 규칙 대상이 아니다."""
    n = len(masks)
    par = list(range(n))

    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]
            a = par[a]
        return a

    for i in range(n):
        for j in range(i + 1, n):
            if (masks[i] & masks[j]).any():
                par[find(i)] = find(j)
    return [find(i) for i in range(n)]


def audit(name, pieces, accent):
    mm = measure(pieces)
    masks = mm["masks"]
    comp = components(masks)
    rmax = rmax_of(mm["union"])
    bx0, bx1, by0, by1 = mm["bbox"]
    diag = math.hypot(bx1 - bx0, by1 - by0)
    ths = sorted({round(p.t, 3) for p in pieces if p.t > 0})
    off = [t for t in ths if round(t, 3) not in LADDER]
    bad = [(a, b, c) for a, b, c in mm["clears"] if c < GAP_MIN
           and comp[[p.name for p in pieces].index(a)] != comp[[p.name for p in pieces].index(b)]]
    ok = {
        "FG-1": rmax <= FIELD_R_HARD,
        "FG-2": not off,
        "FG-3": not bad,
        "FG-4": 2 <= mm["n"] <= 6 and not mm["hidden"],
        "FG-7": len(accent) <= 1,
        "FG-8": AREA_LO <= 100 * mm["ink_frac"] <= AREA_HI and DIAG_LO <= diag <= DIAG_HI,
    }
    print(f"\n── {name}   덩어리 {len(set(comp))}개 · 조각 {mm['n']}")
    print(f"   FG-1 필드   r_max {rmax:5.2f} {'✓' if ok['FG-1'] else '✘'}"
          f"   상자 {bx1-bx0:5.2f}×{by1-by0:5.2f} 대각 {diag:5.2f}")
    lad = ", ".join("{0}({1})".format(t, LADDER.get(round(t, 3), "★off")) for t in ths)
    print(f"   FG-2 사다리 {lad}  {'✓' if ok['FG-2'] else '✘ ' + str(off)}")
    if bad:
        print(f"   FG-3 골     ✘ 다른 덩어리인데 {GAP_MIN}pt 미만 {len(bad)}쌍")
        for a, b, c in bad:
            print(f"        {a:11s}↔{b:11s} {c:5.2f}pt = {c/W:4.2f}W  골 {c-1.0:+5.2f}pt "
                  f"({(c-1.0)*1:+.1f}px @1× / {(c-1.0)*2:+.1f}px @2×)")
    else:
        seps = [c for a, b, c in mm["clears"]
                if comp[[p.name for p in pieces].index(a)] != comp[[p.name for p in pieces].index(b)]]
        print(f"   FG-3 골     ✓ 덩어리 간 최소 {min(seps) if seps else float('inf'):.2f}pt "
              f"= {(min(seps) if seps else 0)/W:.2f}W")
    print(f"   FG-4 조각   {mm['n']} (2~6) {'✓' if 2 <= mm['n'] <= 6 else '✘'}"
          f" · 가려진 조각 {mm['hidden'] or '없음'} {'✓' if not mm['hidden'] else '✘'}")
    print(f"   FG-7 보조색 {len(accent)} {'✓' if ok['FG-7'] else '✘'}"
          f"   FG-8 잉크 {100*mm['ink_frac']:5.2f}% {'✓' if ok['FG-8'] else '✘'}")
    mm["diag"] = diag
    mm["ok"] = ok
    return mm


def report(label, sets, accents):
    print("=" * 96)
    print(label)
    print("=" * 96)
    return {n: audit(n, fn(), accents.get(n, ())) for n, fn in sets.items()}


def silhouette(allm, tag):
    names = list(allm)
    print(f"\n── FG-6 실루엣 ({tag})   IoU / 각자 고유 잉크 비율")
    worst_iou, worst_uni = None, None
    for i in range(len(names)):
        for j in range(i + 1, len(names)):
            a, b = allm[names[i]]["union"], allm[names[j]]["union"]
            iou = (a & b).sum() / max(1, (a | b).sum())
            ua = (a & ~b).sum() / max(1, a.sum())
            ub = (b & ~a).sum() / max(1, b.sum())
            uni = min(ua, ub)
            if worst_iou is None or iou > worst_iou[2]:
                worst_iou = (names[i], names[j], iou)
            if worst_uni is None or uni < worst_uni[2]:
                worst_uni = (names[i], names[j], uni)
            if iou > 0.30 or uni < 0.45:
                print(f"     주의 {names[i]} ↔ {names[j]}  IoU {iou:.3f}  고유 {ua:.2f}/{ub:.2f}")
    print(f"   최악 IoU  {worst_iou[0]} ↔ {worst_iou[1]}  {worst_iou[2]:.3f}")
    print(f"   최소 고유 {worst_uni[0]} ↔ {worst_uni[1]}  {worst_uni[2]:.3f} "
          f"{'✓ (≥0.45)' if worst_uni[2] >= 0.45 else '✘'}")
    return worst_iou[2], worst_uni[2]


cur = report("현행 (프로덕션 거울)", CURRENT, CUR_ACCENT)
ic, uc = silhouette(cur, "현행")
print()
new = report("제안 R26", NEW, NEW_ACCENT)
inw, un = silhouette(new, "제안")

print("\n" + "=" * 96)
print("요약")
print("=" * 96)


def tally(d):
    return sum(1 for m in d.values() for v in m["ok"].values() if v), \
           sum(len(m["ok"]) for m in d.values())


pc, tc = tally(cur)
pn, tn = tally(new)
print(f"  게이트 통과      현행 {pc}/{tc}  →  제안 {pn}/{tn}")
thc = sorted({round(p.t, 3) for fn in CURRENT.values() for p in fn() if p.t > 0})
thn = sorted({round(p.t, 3) for fn in NEW.values() for p in fn() if p.t > 0})
print(f"  획 폭 종류       현행 {thc} ({len(thc)}종)  →  제안 {thn} ({len(thn)}종)")
print(f"  조각 합계        현행 {sum(m['n'] for m in cur.values())}  →  제안 {sum(m['n'] for m in new.values())}")
print(f"  곡선 조각(polyline) 현행 0 / 26  →  제안 "
      f"{sum(1 for fn in NEW.values() for p in fn() if p.kind=='poly')} / {sum(m['n'] for m in new.values())}")
ac = [100 * m["ink_frac"] for m in cur.values()]
an = [100 * m["ink_frac"] for m in new.values()]
print(f"  잉크 면적        현행 {min(ac):.1f}~{max(ac):.1f}%  →  제안 {min(an):.1f}~{max(an):.1f}%")
dc = [m["diag"] for m in cur.values()]
dn = [m["diag"] for m in new.values()]
print(f"  잉크 대각        현행 {min(dc):.1f}~{max(dc):.1f}pt  →  제안 {min(dn):.1f}~{max(dn):.1f}pt")
print(f"  최악 IoU         현행 {ic:.3f}  →  제안 {inw:.3f}")
print(f"  최소 고유 잉크   현행 {uc:.3f}  →  제안 {un:.3f}")
