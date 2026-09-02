# -*- coding: utf-8 -*-
"""R9 — 그늘 배수 `FillOutlineColor` ×0.62 → ×0.35 vs ×0.30 을 **세 축에서 다시 잰다**,
그리고 리더가 지목한 **두 번째 소비처(tone == Shade)의 7건**을 따로 판정한다.

소비처는 프로덕션 실측상 정확히 두 갈래다.
  (가) *.AddShape        : if (shape.Filled) outline = FillOutlineColor(color);
       → 채운 조각의 **자기 윤곽**. Filled == false 면 아예 안 불린다.
  (나) *.ToneColor       : if (tone == Shade) return FillOutlineColor(primary);
       → tone==2 낱선. **Filled 와 무관하게** 배수를 탄다. 7건(망토3×2 + 털모자1).

축 정의 (이 세 개가 서로 다른 일을 한다 — 섞으면 판정이 안 된다):
  A 자기 채움 축 : CR(C, C×k)                채움과 그 위에 얹은 자기 윤곽
  B 부모 채움 축 : CR(C_sec×k, C_pri)        보조 조각의 윤곽 vs 그 밑에 깔린 주색 채움
  C 바탕화면 축  : CR(C_pri×k, 검/흰)        가장 바깥 채움의 윤곽 vs 바탕화면
  S 그늘 낱선 축 : CR(C_pri, C_pri×k)        (나) 경로 — A와 **식이 같다**(C_sec == C_pri)

★ 색은 문서를 베끼지 않고 **애셋에서 읽는다**(shipped.item_colors).
★ 배수 연산은 프로덕션과 같이 **float 0..1** 에서 한다(colorlab.fill_outline은 8bit 반올림이라 안 쓴다).
"""
import math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "design", "art", "verify"))
sys.path.insert(0, HERE)
import colorlab as C, shipped
import rig, items, headroom as H

INK = {"#D6DBE3", "#8B939F"}          # 잉크 표식 — 색이 아니라 지시다. 축에서 뺀다
KS = [0.62, 0.45, 0.40, 0.36, 0.35, 0.30]
FLOOR = 3.0


def mul(rgb, k):
    """프로덕션 FillOutlineColor 포트 — float 에서 곱하고 CR 계산까지 float 로 간다."""
    return tuple(v / 255.0 * k for v in rgb)


def lin1(c):
    return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4


def Lf(rgb01):
    r, g, b = (lin1(max(0.0, min(1.0, v))) for v in rgb01)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def CRf(a01, b01):
    la, lb = Lf(a01), Lf(b01)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def f01(rgb):
    return tuple(v / 255.0 for v in rgb)


# ---------------------------------------------------------------- 교정
def calib():
    print("╔══ 교정 — 깨지면 아래 숫자를 전부 폐기한다 ══╗")
    ok = []
    C.calibrate(verbose=False)
    ok.append(("colorlab 교정", True, ""))
    ok.append(("CR 흰/검 = 21.0", abs(CRf((1, 1, 1), (0, 0, 0)) - 21.0) < 5e-4, "%.4f" % CRf((1, 1, 1), (0, 0, 0))))
    ok.append(("CR 동일색 = 1.0", abs(CRf((.3, .4, .5), (.3, .4, .5)) - 1.0) < 5e-4, ""))
    ok.append(("CR #767676/흰 = 4.5422",
               abs(CRf(f01(C.hex2rgb("#767676")), (1, 1, 1)) - 4.5422) < 5e-4,
               "%.4f" % CRf(f01(C.hex2rgb("#767676")), (1, 1, 1))))
    # 내 float CR 과 colorlab 의 8bit CR 이 같은 색에서 일치하는가(독립 구현 대조)
    a, b = C.hex2rgb("#9B7922"), C.hex2rgb("#3378CC")
    ok.append(("float CR ≡ colorlab CR", abs(CRf(f01(a), f01(b)) - C.CR(a, b)) < 1e-9, ""))
    # ★ 죽은 프로브 방지 — 배수를 내리면 CR 이 실제로 **움직여야** 한다
    m62, m30 = CRf(f01(a), mul(a, 0.62)), CRf(f01(a), mul(a, 0.30))
    ok.append(("배수가 CR을 움직인다", m30 > m62 + 0.5, "%.3f -> %.3f" % (m62, m30)))
    for n, v, x in ok:
        print("  [%s] %-26s %s" % ("OK" if v else "★ ", n, x))
    if not all(v for _, v, _ in ok):
        sys.exit("★ 교정 실패 — 판정 폐기")
    print()


# ---------------------------------------------------------------- 색 census
def colors():
    ic = shipped.item_colors()
    rows = []
    for name in sorted(ic):
        t = ic[name]["tones"]
        pri = sorted(t.get(0, ()))
        sec = sorted(t.get(1, ()))
        rows.append((name, pri[0] if pri else None, sec[0] if sec else (pri[0] if pri else None)))
    return rows


def axis_A(rows):
    fills = sorted({h for _, p, s in rows for h in (p, s) if h and C.rgb2hex(h) not in INK})
    print("╔══ 축 A — 자기 채움 축  CR(C, C×k)   대상 %d색 ══╗" % len(fills))
    print("  %-6s %8s %8s %8s" % ("배수", "최악", "최고", "3.0 미달"))
    for k in KS:
        v = [CRf(f01(c), mul(c, k)) for c in fills]
        print("  ×%-5.2f %8.3f %8.3f %8d" % (k, min(v), max(v), sum(1 for x in v if x < FLOOR)))
    # 각 색이 3.0을 넘는 최대 배수
    worst_k = []
    for c in fills:
        lo, hi = 0.05, 1.0
        for _ in range(60):
            mid = (lo + hi) / 2
            if CRf(f01(c), mul(c, mid)) >= FLOOR: lo = mid
            else: hi = mid
        worst_k.append((lo, C.rgb2hex(c)))
    worst_k.sort()
    print("  각 색이 3.0을 넘는 **최대 배수** 범위 [%.3f, %.3f] · 구속색 %s"
          % (worst_k[0][0], worst_k[-1][0], worst_k[0][1]))
    return fills


def axis_B(rows):
    pairs = [(n, p, s) for n, p, s in rows if p and s
             and C.rgb2hex(p) not in INK and C.rgb2hex(s) not in INK]
    print("\n╔══ 축 B — 부모 채움 축  CR(C_sec×k, C_pri)   대상 %d아이템 "
          "(잉크 표식 %d 제외) ══╗" % (len(pairs), len(rows) - len(pairs)))
    print("  %-6s %8s %8s %8s   %s" % ("배수", "최악", "최고", "3.0 미달", "최악 아이템"))
    for k in KS:
        v = sorted((CRf(mul(s, k), f01(p)), n) for n, p, s in pairs)
        print("  ×%-5.2f %8.3f %8.3f %8d   %s" % (k, v[0][0], v[-1][0],
                                                  sum(1 for x, _ in v if x < FLOOR), v[0][1]))
    for k in (0.35, 0.30):
        v = sorted((CRf(mul(s, k), f01(p)), n) for n, p, s in pairs)
        bad = [x for x in v if x[0] < FLOOR]
        print("  ×%.2f 미달 %d건: %s" % (k, len(bad), ", ".join("%s %.3f" % (n, c) for c, n in bad) or "(없음)"))
    return pairs


def axis_C(rows):
    pri = sorted({p for _, p, _ in rows if p and C.rgb2hex(p) not in INK})
    print("\n╔══ 축 C — 바탕화면 축  CR(C_pri×k, 검/흰)   대상 %d색 ══╗" % len(pri))
    print("  %-6s %10s %10s   %10s %10s" % ("배수", "검바탕 최악", "검바탕 최고", "흰바탕 최악", "흰바탕 최고"))
    for k in KS:
        bk = [CRf(mul(c, k), (0, 0, 0)) for c in pri]
        wt = [CRf(mul(c, k), (1, 1, 1)) for c in pri]
        print("  ×%-5.2f %10.3f %10.3f   %10.3f %10.3f" % (k, min(bk), max(bk), min(wt), max(wt)))
    print("  ★ 이 축은 어느 배수에서도 3.0을 못 넘는다 — **임계를 넘나드는 축이 아니다.**")
    print("     배경에 대한 일은 채움이 한다(자립 대역 L∈[0.1632,0.2396]이 그 일을 이미 보장한다).")


# ---------------------------------------------------------------- (나) 경로 7건
SHADE7 = [("BACK 짧은망토", "equip_shoulders_cape", items.BACK, "짧은망토", "CapeOutline",
           ["CapeFold", "CapeFold2"]),
          ("BACK 긴망토", "equip_shoulders_long_cape", items.BACK, "긴망토", "CapeOutline",
           ["CapeFold", "CapeFold2"]),
          ("BACK 판초", "equip_shoulders_poncho", items.BACK, "판초", "CapeOutline",
           ["CapeFold", "CapeFold2"]),
          ("HEAD 털모자", "equip_head_fur", items.HEAD, "털모자", "BeanieCrown", ["BeanieCuff"])]


def inside(poly, q):
    c = False; n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        if (a[1] > q[1]) != (b[1] > q[1]):
            x = a[0] + (q[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])
            if q[0] < x: c = not c
    return c


def dist_edge(poly, q):
    best = 1e9; n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        dx, dy = b[0] - a[0], b[1] - a[1]
        l2 = dx * dx + dy * dy
        t = 0.0 if l2 < 1e-15 else max(0.0, min(1.0, ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy) / l2))
        best = min(best, math.hypot(q[0] - (a[0] + dx * t), q[1] - (a[1] + dy * t)))
    return best


def shade_path():
    ic = shipped.item_colors()
    print("\n╔══ (나) tone == Shade 7건 — **Filled 와 무관하게 배수를 탄다** ══╗")
    print("  축 S = CR(주색, 주색×k). C_sec == C_pri 이므로 **축 A의 부분집합**이다.")
    print("  %-14s %-9s %8s %8s %8s" % ("아이템", "주색", "×0.62", "×0.35", "×0.30"))
    prim = {}
    for label, stem, tbl, key, pname, snames in SHADE7:
        p = sorted(ic[stem]["tones"][0])[0]
        prim[label] = p
        print("  %-14s %-9s %8.3f %8.3f %8.3f"
              % (label, C.rgb2hex(p), CRf(f01(p), mul(p, 0.62)),
                 CRf(f01(p), mul(p, 0.35)), CRf(f01(p), mul(p, 0.30))))

    print("\n  ── 그늘 낱선이 **부모 채움 안에 있는가**(밖이면 축 C가 걸린다) ──")
    print("  획 반폭 W/2 = %.4f R (0.75) · %.4f R (0.60)"
          % (H.stroke_in_R(0.75) / 2, H.stroke_in_R(0.60) / 2))
    print("  %-14s %-11s %9s %9s %9s %9s" % ("아이템", "낱선", "안쪽비율", "최소여유R", "밖면적%0.75", "밖면적%0.60"))
    for label, stem, tbl, key, pname, snames in SHADE7:
        sh = tbl[key]
        sh = sh() if callable(sh) else sh
        par = [s for s in sh if s.name == pname][0].pts
        for sn in snames:
            seg = [s for s in sh if s.name == sn][0].pts
            N = 2000
            d = []
            for i in range(N + 1):
                t = i / N
                q = (seg[0][0] + (seg[-1][0] - seg[0][0]) * t,
                     seg[0][1] + (seg[-1][1] - seg[0][1]) * t)
                dd = dist_edge(par, q)
                d.append(dd if inside(par, q) else -dd)
            frac_in = sum(1 for x in d if x > 0) / (N + 1)
            out75 = sum(1 for x in d if x < H.stroke_in_R(0.75) / 2) / (N + 1)
            out60 = sum(1 for x in d if x < H.stroke_in_R(0.60) / 2) / (N + 1)
            print("  %-14s %-11s %8.1f%% %9.4f %9.1f%% %9.1f%%"
                  % (label, sn, 100 * frac_in, min(d), 100 * out75, 100 * out60))


if __name__ == "__main__":
    calib()
    rows = colors()
    axis_A(rows)
    axis_B(rows)
    axis_C(rows)
    shade_path()
