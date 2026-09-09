# -*- coding: utf-8 -*-
"""R4 §2 — 색 위계 실측. 「M(주색)과 M2(보조색)가 몸 위에서 실제로 갈라지는가」.
M/M2 의 원천은 .asset 의 icon[].color (AccessoryDefSO.cs:716 — «M = icon[].color(tone 0) = entry.PrimaryColor,
M2 = tone 1 = entry.SecondaryColor. 카드·몸이 그 한 원천을 읽는다»). 팩은 PackManifest_*.asset 의 primary/secondary.
그늘 SH = M × 0.28 (AccessoryShapeContract.Shaded, ShadeFactor)."""
import os, re, math, glob, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SHADE = None

def lin(c): return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
def lum(rgb): r, g, b = (lin(v) for v in rgb); return 0.2126 * r + 0.7152 * g + 0.0722 * b
def lab(rgb):
    r, g, b = (lin(v) for v in rgb)
    X = 0.4124 * r + 0.3576 * g + 0.1805 * b
    Y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    Z = 0.0193 * r + 0.1192 * g + 0.9505 * b
    Xn, Yn, Zn = 0.95047, 1.0, 1.08883
    f = lambda t: t ** (1/3) if t > 0.008856 else (7.787 * t + 16/116)
    fx, fy, fz = f(X/Xn), f(Y/Yn), f(Z/Zn)
    return (116*fy - 16, 500*(fx-fy), 200*(fy-fz))
def de76(a, b): return math.dist(lab(a), lab(b))
def hexs(rgb): return "#%02X%02X%02X" % tuple(int(round(v*255)) for v in rgb)

def parse_shade_factor():
    src = open(os.path.join(ROOT, "Assets/_Project/Scripts/Core/AccessoryShapeContract.cs"), encoding="utf-8").read()
    m = re.search(r"ShadeFactor\s*=\s*([\d.]+)f", src)
    return float(m.group(1))
SHADE = parse_shade_factor()

def item_colors():
    """출하 42종 — .asset icon 조각의 tone0/tone1 색."""
    out = {}
    for p in sorted(glob.glob(os.path.join(ROOT, "Assets/_Project/Resources/Items/equip_*.asset"))):
        txt = open(p, encoding="utf-8").read()
        m0 = re.findall(r"color:\s*\{r:\s*([\d.eE+-]+),\s*g:\s*([\d.eE+-]+),\s*b:\s*([\d.eE+-]+),\s*a:[^}]*\}\s*\n\s*tone:\s*(\d+)", txt)
        prim = sec = None
        for r, g, b, t in m0:
            c = (float(r), float(g), float(b))
            if int(t) == 0 and prim is None: prim = c
            if int(t) == 1 and sec is None: sec = c
        key = os.path.basename(p).replace("equip_", "").replace(".asset", "")
        out[key] = (prim, sec)
    return out

def pack_colors():
    out = {}
    for p in sorted(glob.glob(os.path.join(ROOT, "Assets/_Project/Resources/Items/PackManifest_*.asset"))):
        txt = open(p, encoding="utf-8").read()
        def col(name):
            m = re.search(name + r":\s*\{r:\s*([\d.eE+-]+),\s*g:\s*([\d.eE+-]+),\s*b:\s*([\d.eE+-]+)", txt)
            return (float(m.group(1)), float(m.group(2)), float(m.group(3)))
        out[os.path.basename(p).replace("PackManifest_", "").replace(".asset", "")] = (col("primaryColor"), col("secondaryColor"))
    return out

if __name__ == "__main__":
    print(f"# ShadeFactor = {SHADE} (AccessoryShapeContract.cs 에서 읽음 — 손으로 베끼지 않았다)")
    print(f"# ΔE = CIE76(Lab). 이 저장소의 변별 하한은 ΔE 7.8 (ItemCatalog.cs:848 «변별 하한 ΔE 7.8»)")
    print()
    print("═" * 104)
    print("출하 42종 — 주색 M ↔ 보조색 M2 (한 아이템 안에서 맞닿는 두 색)")
    print("═" * 104)
    print(f"{'아이템':<26}{'M':<10}{'L*':>6}{'M2':<10}{'L*':>6}{'ΔE(M,M2)':>10}{'ΔL*':>7}{'ΔE(M,SH)':>10}{'ΔE(M2,SH)':>11}")
    print("─" * 104)
    rows = []
    for k, (m, m2) in item_colors().items():
        if m is None: continue
        sh = tuple(c * SHADE for c in m)
        if m2 is None:
            print(f"{k:<26}{hexs(m):<10}{lab(m)[0]:>6.1f}{'—':<10}{'':>6}{'—':>10}{'':>7}{de76(m,sh):>10.1f}")
            continue
        d = de76(m, m2); dl = lab(m2)[0] - lab(m)[0]
        rows.append((k, d, dl))
        flag = "  ← 변별 하한 7.8 미만" if d < 7.8 else ""
        print(f"{k:<26}{hexs(m):<10}{lab(m)[0]:>6.1f}{hexs(m2):<10}{lab(m2)[0]:>6.1f}{d:>10.1f}{dl:>7.1f}{de76(m,sh):>10.1f}{de76(m2,sh):>11.1f}{flag}")
    print("─" * 104)
    print(f"출하 M↔M2 ΔE  중앙값 {sorted(r[1] for r in rows)[len(rows)//2]:.1f} · 최소 {min(r[1] for r in rows):.1f} · 최대 {max(r[1] for r in rows):.1f} (n={len(rows)})")
    print()
    print("═" * 104)
    print("★ 팩 3종 — 같은 자로")
    print("═" * 104)
    for k, (m, m2) in pack_colors().items():
        sh = tuple(c * SHADE for c in m)
        d = de76(m, m2)
        flag = "  ★ 변별 하한 7.8 미만 — 보조색이 주색과 갈라지지 않는다" if d < 7.8 else ""
        print(f"{'pack.'+k:<26}{hexs(m):<10}{lab(m)[0]:>6.1f}{hexs(m2):<10}{lab(m2)[0]:>6.1f}{d:>10.1f}"
              f"{lab(m2)[0]-lab(m)[0]:>7.1f}{de76(m,sh):>10.1f}{de76(m2,sh):>11.1f}{flag}")
