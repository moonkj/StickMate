# -*- coding: utf-8 -*-
"""R10 — 조형 결함 2건(긴망토 `CapeFold2` · 털모자 `BeanieCuff`)과 B군 코다리 2건을 잰다.

★ 이 파일이 재는 것은 **프로덕션 좌표**다(`Tools/ShapeDump/build.sh`).
  설계 거울(`items.py`/`hair.py`)을 재면 "거울이 맞나"와 "도형이 맞나"가 섞인다.
  거울 일치는 `mirrordrift.py`가 따로 지킨다.

왜 지금 재는가
--------------
그늘 배수가 `×0.62 → ×0.28`로 착지하면서 축 S 대비가 1.955 → 3.392가 됐다.
**안 보이던 그늘 낱선이 보이게 됐고, 그와 함께 그 낱선의 조형 결함도 보이게 됐다.**
(스펙 14-2 말미가 "별건, 이 라운드에서 안 고침"으로 넘긴 2건이 이것이다.)

축
--
 A. 그늘 낱선 7건 — 부모 채움 안쪽 비율 / 최소 여유 / 획 반폭 안에 든 길이   (14-2 재측정)
 B. 긴망토 `CapeFold2` — `endRatioOverride` 掃引. 안전 창과 여유를 찍는다
 C. 털모자 `BeanieCuff` — 낱선 vs 채운 띠. ρ_max·잉크 확장·남는 머리
 D. B군 코다리 2종 — 변 길이 / 잉크 사각형(span) / 눈 커버 한계 / 배율 문턱
 E. 선글라스 ↔ 뿔테안경 실루엣 차 (D의 처방이 혼동을 부르는가)

실행:  cd design/equipment/verify && python3 r10_fit.py
"""
import math, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
import rig, items, headroom as H

BUILD = os.path.join(ROOT, "Tools", "ShapeDump", "build.sh")

# ── 프로덕션 상수(이름만 옮긴다. 값은 아래 assert가 프로덕션 소스와 대조한다) ──
SRC = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Interaction", "AccessoryShapeBuilder.cs")


def prod_const(name):
    """AccessoryShapeBuilder.cs에서 상수 값을 직접 읽는다 — 손으로 안 베낀다."""
    import re
    txt = open(SRC, encoding="utf-8").read()
    m = re.search(r"const\s+(?:float|int)\s+" + name + r"\s*=\s*(-?[0-9.]+)f?\s*;", txt)
    if not m:
        raise SystemExit("★ 상수 %s 를 프로덕션에서 못 찾았다 — 이름이 바뀌었나?" % name)
    return float(m.group(1))


# ============================================================ 기하 유틸
def inside(poly, q):
    c = False; n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        if (a[1] > q[1]) != (b[1] > q[1]):
            x = a[0] + (q[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])
            if q[0] < x: c = not c
    return c


def seg_dist(q, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    l2 = dx * dx + dy * dy
    t = 0.0 if l2 < 1e-15 else max(0.0, min(1.0, ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy) / l2))
    return math.hypot(q[0] - (a[0] + dx * t), q[1] - (a[1] + dy * t))


def dist_edge(poly, q):
    n = len(poly)
    return min(seg_dist(q, poly[i], poly[(i + 1) % n]) for i in range(n))


def sdist(poly, q):
    d = dist_edge(poly, q)
    return d if inside(poly, q) else -d


def rho_max(poly):
    """최대 내접원 반경 — 프로덕션 AccessoryFillAreaRuleTests.MaxInscribedRadius 이식(분지한정)."""
    xs = [p[0] for p in poly]; ys = [p[1] for p in poly]
    minx, maxx, miny, maxy = min(xs), max(xs), min(ys), max(ys)
    span = max(maxx - minx, maxy - miny)
    if span <= 0: return 0.0
    TOL = 1.0 / 4000.0 / 0.22          # 월드 0.00025 유닛 → R 단위(R = 0.22 월드)
    h = span / 48.0
    cand = []
    x = minx
    while x <= maxx + h * 0.5:
        y = miny
        while y <= maxy + h * 0.5:
            cand.append((x, y)); y += h
        x += h
    best = -1e30
    while True:
        vals = [sdist(poly, p) for p in cand]
        if vals: best = max(best, max(vals))
        if h <= TOL or not cand: break
        cut = best - h * 0.70711
        nh = h / 3.0
        nxt = []
        for p, v in zip(cand, vals):
            if v < cut: continue
            for a in (-1, 0, 1):
                for b in (-1, 0, 1):
                    if a == 0 and b == 0: continue
                    nxt.append((p[0] + a * nh, p[1] + b * nh))
        cand, h = nxt, nh
    return max(0.0, best)


# ============================================================ 교정
def calib():
    print("╔══ 교정 — 깨지면 아래 숫자를 전부 폐기한다 ══╗")
    ok = []
    # 획 예산 — 프로덕션 식과 같은가
    ok.append(("W(0.75) = 0.343864", abs(H.stroke_in_R(0.75) - 0.343864) < 1e-5, "%.6f" % H.stroke_in_R(0.75)))
    ok.append(("W(0.60) = 0.429830", abs(H.stroke_in_R(0.60) - 0.429830) < 1e-5, "%.6f" % H.stroke_in_R(0.60)))
    # ρ_max 교정 — 알려진 값 3개
    sq = [(0, 0), (1, 0), (1, 1), (0, 1)]
    ok.append(("ρ(단위정사각) = 0.5", abs(rho_max(sq) - 0.5) < 2e-3, "%.4f" % rho_max(sq)))
    band = [(-1, 0), (1, 0), (1, 0.46), (-1, 0.46)]
    ok.append(("ρ(두께0.46 띠) = 0.23", abs(rho_max(band) - 0.23) < 2e-3, "%.4f" % rho_max(band)))
    tri = [(0, 0), (1, 0), (0, 1)]                     # 내접원 r = (a+b-c)/2 = (2-√2)/2
    ok.append(("ρ(직각삼각형)", abs(rho_max(tri) - (2 - math.sqrt(2)) / 2) < 2e-3, "%.4f" % rho_max(tri)))
    # 부호거리 — 밖은 음수
    ok.append(("sdist 밖 = 음수", sdist(sq, (2, 0.5)) < 0, "%.3f" % sdist(sq, (2, 0.5))))
    # ★ 죽은 프로브 방지 — 실제 결함이 실제로 음수로 잡히는가(양성 대조)
    P = production()
    lc = {s["name"]: s["pts"] for s in P["BACK"]["긴망토"]}
    mn = min(sdist(lc["CapeOutline"], q) for q in sample(lc["CapeFold2"], 2000))
    ok.append(("양성대조: CapeFold2 < 0", mn < -0.02, "%.4f R" % mn))
    bn = {s["name"]: s["pts"] for s in P["HEAD"]["털모자"]}
    mb = min(sdist(bn["BeanieCrown"], q) for q in sample(bn["BeanieCuff"], 2000))
    ok.append(("양성대조: BeanieCuff ≈ 0", abs(mb) < 1e-6, "%.6f R" % mb))
    # 음성 대조 — 건강한 낱선은 양수
    sc = {s["name"]: s["pts"] for s in P["BACK"]["짧은망토"]}
    ms = min(sdist(sc["CapeOutline"], q) for q in sample(sc["CapeFold"], 2000))
    ok.append(("음성대조: 짧은망토 CapeFold > 0", ms > 0.10, "%.4f R" % ms))
    for n, v, x in ok:
        print("  [%s] %-30s %s" % ("OK" if v else "★ ", n, x))
    if not all(v for _, v, _ in ok):
        sys.exit("★ 교정 실패 — 판정 폐기")
    print()
    return P


def sample(pts, n):
    """폴리라인을 길이 균등으로 n+1점 표본."""
    segs = [(pts[i], pts[i + 1]) for i in range(len(pts) - 1)]
    L = [math.dist(a, b) for a, b in segs]
    tot = sum(L)
    out = []
    for i in range(n + 1):
        s = tot * i / n
        acc = 0.0
        for (a, b), l in zip(segs, L):
            if acc + l >= s or (a, b) is segs[-1]:
                t = 0.0 if l < 1e-12 else (s - acc) / l
                t = max(0.0, min(1.0, t))
                out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
                break
            acc += l
    return out


# ============================================================ 프로덕션 좌표
_PROD = None


def production():
    global _PROD
    if _PROD is not None: return _PROD
    raw = subprocess.run([BUILD], capture_output=True, text=True)
    if raw.returncode != 0:
        print(raw.stdout, raw.stderr)
        raise SystemExit("!! Tools/ShapeDump/build.sh 실패")
    cats, cat, name = {}, None, None
    for line in raw.stdout.splitlines():
        f = line.split("\t")
        if f[0] == "@ITEM":
            cat, name = f[1], f[2]; cats.setdefault(cat, {})[name] = []
        elif f[0] == "@SHAPE":
            cats[cat][name].append(dict(name=f[1], loop=f[2] == "1", filled=f[3] == "1",
                                        tone=int(f[4]),
                                        pts=[tuple(float(v) for v in p.split(",")) for p in f[6:]]))
    _PROD = cats
    return cats


# ============================================================ A. 그늘 낱선 7건
SHADE7 = [("짧은망토", "BACK", "CapeOutline", ["CapeFold", "CapeFold2"]),
          ("긴망토", "BACK", "CapeOutline", ["CapeFold", "CapeFold2"]),
          ("판초", "BACK", "CapeOutline", ["CapeFold", "CapeFold2"]),
          ("털모자", "HEAD", "BeanieCrown", ["BeanieCuff"])]


def axis_A(P):
    print("╔══ A. 그늘 낱선 7건 — 부모 채움 안에 있는가 (프로덕션 좌표) ══╗")
    print("  펜 반폭 W/2 = %.4f R (0.75) · %.4f R (0.60)"
          % (H.stroke_in_R(0.75) / 2, H.stroke_in_R(0.60) / 2))
    print("  %-8s %-11s %8s %10s %10s %10s" % ("아이템", "낱선", "안쪽%", "최소여유R", "W/2내@0.75", "W/2내@0.60"))
    rows = []
    for item, cat, pname, snames in SHADE7:
        sh = {s["name"]: s["pts"] for s in P[cat][item]}
        for sn in snames:
            q = sample(sh[sn], 4000)
            d = [sdist(sh[pname], p) for p in q]
            fin = sum(1 for x in d if x > 0) / len(d)
            o75 = sum(1 for x in d if x < H.stroke_in_R(0.75) / 2) / len(d)
            o60 = sum(1 for x in d if x < H.stroke_in_R(0.60) / 2) / len(d)
            rows.append((item, sn, fin, min(d), o75, o60))
            print("  %-8s %-11s %7.1f%% %10.4f %9.1f%% %9.1f%%"
                  % (item, sn, 100 * fin, min(d), 100 * o75, 100 * o60))
    healthy = [r for r in rows if r[3] > 0.05]
    print("  ── 건강한 %d건의 기준선: 최소여유 %.4f~%.4f R · W/2내 %.1f~%.1f%%(0.75) / %.1f~%.1f%%(0.60)"
          % (len(healthy), min(r[3] for r in healthy), max(r[3] for r in healthy),
             100 * min(r[4] for r in healthy), 100 * max(r[4] for r in healthy),
             100 * min(r[5] for r in healthy), 100 * max(r[5] for r in healthy)))
    print("  ★ 건강한 낱선의 최소여유를 만드는 것은 **시작점**이다 — 옷깃선 아래 0.10R에 찍힌다.")
    print()
    return rows


# ============================================================ B. 긴망토 CapeFold2
def axis_B(P):
    print("╔══ B. 긴망토 CapeFold2 — endRatioOverride 掃引 ══╗")
    L = {s["name"]: s["pts"] for s in P["BACK"]["긴망토"]}
    out, f1, f2 = L["CapeOutline"], L["CapeFold"], L["CapeFold2"]
    trail = prod_const("LongCapeSpreadRatio")
    cur_e = -f2[1][0] / trail
    e1 = -f1[1][0] / trail
    print("  trail = %.4f R  ·  현행 CapeFold e=%.4f / CapeFold2 e=%.4f" % (trail, e1, cur_e))
    print("  끝점 y = %.4f R (두 주름 공통 — CapeFold가 유도한다)" % f2[1][1])

    yend = f2[1][1]
    xs = []
    for i in range(200001):
        x = -3.3 + 6.6 * i / 200000
        if inside(out, (x, yend)): xs.append(x)
    # 연속 구간으로 자른다
    runs, s = [], xs[0]
    for a, b in zip(xs, xs[1:]):
        if b - a > 1e-4:
            runs.append((s, a)); s = b
    runs.append((s, xs[-1]))
    print("  끝점 높이에서 천이 있는 x 구간: %s"
          % " · ".join("[%.4f, %.4f] 폭 %.4f" % (a, b, b - a) for a, b in runs))

    print("  %-7s %9s %10s %10s %10s %10s" % ("e", "끝점x", "안쪽%", "최소여유R", "W/2내@0.75", "W/2내@0.60"))
    best = None
    for e in [0.55, 0.60, 0.64, 0.66, 0.68, 0.70, 0.72, 0.74, 0.76, 0.80, 0.86, 0.90, 0.96]:
        seg = [f2[0], (-trail * e, yend)]
        q = sample(seg, 4000)
        d = [sdist(out, p) for p in q]
        o75 = sum(1 for x in d if x < H.stroke_in_R(0.75) / 2) / len(d)
        o60 = sum(1 for x in d if x < H.stroke_in_R(0.60) / 2) / len(d)
        mark = " ← 현행" if abs(e - cur_e) < 1e-3 else ""
        print("  %-7.2f %9.4f %8.1f%% %10.4f %9.1f%% %9.1f%%%s"
              % (e, -trail * e, 100 * sum(1 for x in d if x > 0) / len(d), min(d),
                 100 * o75, 100 * o60, mark))
    # 최적: 최소여유를 최대화하는 e (끝점만 움직인다)
    lo, hi = 0.45, 0.96
    for _ in range(80):
        m1 = lo + (hi - lo) / 3; m2 = hi - (hi - lo) / 3
        def f(e):
            seg = [f2[0], (-trail * e, yend)]
            return min(sdist(out, p) for p in sample(seg, 3000))
        if f(m1) < f(m2): lo = m1
        else: hi = m2
    eopt = (lo + hi) / 2
    seg = [f2[0], (-trail * eopt, yend)]
    print("  ★ 최소여유를 최대화하는 e = %.4f (여유 %.4f R) — 이 값은 상한이지 처방이 아니다"
          % (eopt, min(sdist(out, p) for p in sample(seg, 3000))))

    # 두 주름 사이 간격(같은 색이라 붙으면 한 줄로 읽힌다)
    print("  ── 두 주름 사이 최소 중심선 간격(같은 색 → W 미만이면 한 줄로 합쳐진다) ──")
    for e in [0.64, 0.66, 0.68, 0.70, 0.72, 0.74, 0.76, 0.80, 0.96]:
        seg = [f2[0], (-trail * e, yend)]
        q2 = sample(seg, 2000)
        gap = min(min(seg_dist(p, f1[i], f1[i + 1]) for i in range(len(f1) - 1)) for p in q2)
        # 길이 중 "붙어 있는" 비율
        merged75 = sum(1 for p in q2
                       if min(seg_dist(p, f1[i], f1[i + 1]) for i in range(len(f1) - 1))
                       < H.stroke_in_R(0.75)) / len(q2)
        merged60 = sum(1 for p in q2
                       if min(seg_dist(p, f1[i], f1[i + 1]) for i in range(len(f1) - 1))
                       < H.stroke_in_R(0.60)) / len(q2)
        print("  e=%.2f  최소간격 %.4f R  ·  합쳐진 길이 %5.1f%%(0.75) / %5.1f%%(0.60)"
              % (e, gap, 100 * merged75, 100 * merged60))
    print()


# ============================================================ C. 털모자 BeanieCuff
def axis_C(P):
    print("╔══ C. 털모자 BeanieCuff — 낱선 vs 채운 띠 ══╗")
    B = {s["name"]: s["pts"] for s in P["HEAD"]["털모자"]}
    crown, cuff = B["BeanieCrown"], B["BeanieCuff"]
    hem = prod_const("BeanieBandBottomRatio")
    top = prod_const("BeanieBandTopRatio")
    thick = prod_const("AccentBandThicknessRatio")
    print("  관 밑단 %+.2f R · 커버선(단 윗변) %+.2f R · 둘 사이 %.2f R" % (hem, top, top - hem))
    print("  A군 5종이 쓴 띠 두께 AccentBandThicknessRatio = %.2f R" % thick)

    # 현행 낱선의 잉크가 관 밖으로 얼마나 나가는가
    print("\n  ── 현행 낱선(펜 폭)의 잉크가 관 채움 밖으로 나가는 양 ──")
    for sc in (0.75, 0.60):
        w2 = H.stroke_in_R(sc) / 2
        q = sample(cuff, 4000)
        d = [sdist(crown, p) for p in q]
        outside = [w2 - x for x in d if x < w2]
        print("    배율 %.2f  W/2=%.4f  ·  W/2 안에 든 길이 %5.1f%%  ·  최대 초과 %.4f R (= %.2f획)"
              % (sc, w2, 100 * len(outside) / len(d), max(outside), max(outside) / H.stroke_in_R(sc)))
    # 관 자신의 밑단 윤곽선과 겹치는가 — 같은 색이라 겹치면 한 덩어리
    print("\n  ── 낱선과 관 밑단 윤곽선이 겹치는가(같은 색 primary×0.28) ──")
    for sc in (0.75, 0.60):
        w2 = H.stroke_in_R(sc) / 2
        q = sample(cuff, 4000)
        d = [sdist(crown, p) for p in q]
        merged = sum(1 for x in d if x < 2 * w2) / len(d)     # 두 획이 닿는다
        print("    배율 %.2f  두 획이 닿는 길이 %5.1f%%  (닿으면 단과 밑단이 한 줄로 읽힌다)"
              % (sc, 100 * merged))

    # 대안 — 채운 띠
    print("\n  ── 대안: 관을 두 채움으로 가르고 이음선을 커프 윗변으로 올린다 ──")
    print("  %-8s %9s %9s %10s %10s %10s" % ("띠 윗변", "ρ_max", "1-C여유", "이음선↔밑단", "@0.75획", "@0.60획"))
    W_OUT = 0.048 / 0.22
    for ytop in (0.02, 0.10, 0.14, 0.18, 0.20, 0.24, 0.30):
        band = band_poly(crown, ytop)
        r = rho_max(band)
        gap = ytop - hem
        print("  %+8.2f %9.4f %+9.4f %10.4f %10.2f %10.2f"
              % (ytop, r, r - W_OUT, gap, gap / H.stroke_in_R(0.75), gap / H.stroke_in_R(0.60)))
    print("  (1-C 하한 W_out = %.5f R — 배율 0.509 이상에서 상수)" % W_OUT)

    # 채택안 검산
    ytop = round(hem + thick, 6)
    band = band_poly(crown, ytop)
    print("\n  ★ 채택안 — 띠 윗변 y = 밑단 %+.2f + %.2f = %+.2f R" % (hem, thick, ytop))
    print("     띠 좌표 (%d점): %s" % (len(band), " ".join("(%.4f,%.4f)" % p for p in band)))
    print("     ρ_max = %.5f R  (하한 %.5f, 여유 %+.5f = %+.1f%%)"
          % (rho_max(band), W_OUT, rho_max(band) - W_OUT, 100 * (rho_max(band) / W_OUT - 1)))
    print("     자기교차: %s" % ("있다 ★" if rig.self_intersects(band) else "없다"))
    # 남은 관(위쪽 조각)
    upper = upper_poly(crown, ytop)
    print("     남은 관 %d점 · ρ_max %.5f R (여유 %+.5f) · 자기교차 %s"
          % (len(upper), rho_max(upper), rho_max(upper) - W_OUT,
             "있다 ★" if rig.self_intersects(upper) else "없다"))
    # 합집합 = 원래 관인가 (실루엣 불변 확인)
    print("     합집합 = 원래 관인가: %s" % union_same(crown, band, upper))
    # 이음선 잉크가 관 밖으로 나가는가 — 채움 윤곽선 폭으로
    seam = [(band[0][0], ytop), (band[-1][0], ytop)]
    for sc in (0.75, 0.60):
        w2 = W_OUT / 2                        # 채움 윤곽선(하한 1pt)은 배율 0.509↑에서 상수
        q = sample(seam, 4000)
        d = [sdist(crown, p) for p in q]
        print("     배율 %.2f  이음선 잉크 반폭 %.4f  ·  관 밖으로 나가는 길이 %.1f%%"
              % (sc, w2, 100 * sum(1 for x in d if x < w2) / len(d)))
    # 남는 머리 — 채움 합집합이 그대로라 불변이어야 한다
    print()
    return band, upper


def band_poly(crown, ytop):
    """관 채움의 y ≤ ytop 부분(하단 조각). 관 경계를 그대로 따르고 윗변만 수평."""
    return clip_poly(crown, ytop, keep_below=True)


def upper_poly(crown, ytop):
    return clip_poly(crown, ytop, keep_below=False)


def clip_poly(poly, ycut, keep_below):
    """수평선 y=ycut 로 볼록/오목 다각형을 자른다(서덜랜드-호지먼 반평면 클립)."""
    def ok(p): return (p[1] <= ycut) if keep_below else (p[1] >= ycut)
    out, n = [], len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        ia, ib = ok(a), ok(b)
        if ia: out.append(a)
        if ia != ib:
            t = (ycut - a[1]) / (b[1] - a[1])
            out.append((a[0] + (b[0] - a[0]) * t, ycut))
    # 같은 점 중복 제거
    ded = []
    for p in out:
        if not ded or math.dist(p, ded[-1]) > 1e-9: ded.append(p)
    if len(ded) > 1 and math.dist(ded[0], ded[-1]) < 1e-9: ded.pop()
    return ded


def union_same(orig, a, b, n=400):
    """원 다각형과 두 조각의 합집합이 같은가 — 격자 표본 대조."""
    xs = [p[0] for p in orig]; ys = [p[1] for p in orig]
    bad = 0; tot = 0
    for i in range(n):
        for j in range(n):
            x = min(xs) + (max(xs) - min(xs)) * (i + 0.5) / n
            y = min(ys) + (max(ys) - min(ys)) * (j + 0.5) / n
            o = inside(orig, (x, y)); u = inside(a, (x, y)) or inside(b, (x, y))
            tot += 1
            if o != u and abs(sdist(orig, (x, y))) > 2e-3: bad += 1
    return "같다 (경계 2e-3R 밖 불일치 0/%d)" % tot if bad == 0 else "★ 다르다 %d/%d" % (bad, tot)


# ============================================================ D. B군 코다리
def axis_D(P):
    print("╔══ D. B군 코다리 2종 — 변 길이 · 잉크 사각형(span) · 눈 커버 한계 ══╗")
    I = prod_const("SunglassInnerRatio")
    rise_s = prod_const("SunglassBridgeRiseRatio")
    off = prod_const("RoundLensOffsetRatio")
    rad = prod_const("RoundLensRadiusRatio")
    riseY = prod_const("RoundLensCenterRiseRatio")
    rise_r = prod_const("RoundBridgeRiseRatio")
    segs = int(prod_const("RoundLensSegments"))
    print("  프로덕션 상수: SunglassInnerRatio=%.2f rise=%.2f · RoundLensOffset=%.2f r=%.2f "
          "rise=%.2f seg=%d" % (I, rise_s, off, rad, rise_r, segs))

    S = {s["name"]: s["pts"] for s in P["EYES"]["선글라스"]}
    R = {s["name"]: s["pts"] for s in P["EYES"]["동그란안경"]}
    for nm, pts in (("SunglassBridge", S["SunglassBridge"]), ("RoundBridge", R["RoundBridge"])):
        x0, y0, x1, y1 = rig.bounds(pts)
        span = max(x1 - x0, y1 - y0)
        L = min(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))
        print("\n  %s  꼭짓점 %s" % (nm, " ".join("(%.4f,%.4f)" % p for p in pts)))
        print("    변 %.4f R  ·  bbox %.4f × %.4f  ·  span %.4f R" % (L, x1 - x0, y1 - y0, span))
        for sc in (0.75, 0.60):
            w = H.stroke_in_R(sc)
            print("    배율 %.2f  변 %.3f획 (하한 1.00, 열린 끝은 면제)  ·  span %.3f획 (하한 1.50) %s"
                  % (sc, L / w, span / w, "OK" if span >= 1.5 * w else "★ 위반"))
        # 문턱 배율 (span 규칙)
        thr = 1.5 * 0.257899 / span
        print("    span 규칙이 통과하는 최소 배율 = %.4f" % thr)
        # 변 규칙 1획을 만드는 rise
        base = pts[0]
        need = {}
        for sc in (0.75, 0.60):
            w = H.stroke_in_R(sc)
            dy2 = w * w - base[0] * base[0]
            need[sc] = None if dy2 < 0 else base[1] + math.sqrt(dy2)
        print("    변 = 1.00획이 되는 아치 꼭대기 y: %s" %
              " · ".join("%.4f(배율 %.2f)" % (v, k) for k, v in need.items() if v))

    # 선글라스 안쪽 비율을 옮길 때의 두 벽
    print("\n  ── SunglassInnerRatio 창(窓) ──")
    need_span = 1.5 * H.stroke_in_R(0.60) / 2.0
    print("    아래벽: span ≥ 1.5획@0.60 → I ≥ %.4f" % need_span)
    # 위벽: 뒤 렌즈가 뒤쪽 눈을 계속 덮는가
    lo, hi = 0.20, 0.60
    for _ in range(80):
        m = (lo + hi) / 2
        if inside(sun_lens(m, False), (-rig.EYE_X, rig.EYE_Y)): lo = m
        else: hi = m
    cap_back = (lo + hi) / 2
    lo, hi = 0.20, 0.60
    for _ in range(80):
        m = (lo + hi) / 2
        if inside(sun_lens(m, True), (rig.EYE_X, rig.EYE_Y)): lo = m
        else: hi = m
    cap_front = (lo + hi) / 2
    print("    위벽: 뒤 렌즈가 뒤 눈을 덮는 한계 I ≤ %.4f · 앞 렌즈 한계 I ≤ %.4f" % (cap_back, cap_front))
    cap = min(cap_back, cap_front)
    print("    창 = [%.4f, %.4f]  폭 %.4f R  (현행 %.2f는 아래벽 밖 %.4f R)"
          % (need_span, cap, cap - need_span, I, need_span - I))
    mid = (need_span + cap) / 2
    print("    창 한가운데 I = %.4f  (아래벽 여유 %+.4f = %+.1f%% · 위벽 여유 %+.4f = %+.1f%%)"
          % (mid, mid - need_span, 100 * (mid / need_span - 1), cap - mid,
             100 * (1 - mid / cap)))
    print()
    return I, cap, need_span


def sun_lens(I, forward):
    back = [(-I, 0.34), (-0.96, 0.30), (-1.02, -0.16), (-0.32, -0.44)]
    bias = prod_const("SunglassFrontBiasRatio")
    out = []
    src = list(reversed(back)) if forward else back
    for p in src:
        x = I + (-p[0] - I) * bias if forward else p[0]
        out.append((x, p[1]))
    return out


# ============================================================ E. 혼동 위험
def axis_E(P, Ivals):
    print("╔══ E. 선글라스 ↔ 뿔테안경 실루엣 차 (코다리를 키우면 붙는가) ══╗")
    def to_shapes(lst):
        return [rig.Shape(s["name"], s["pts"], s["loop"], s["filled"], s["tone"]) for s in lst]
    brow = to_shapes(P["EYES"]["뿔테안경"])
    pb = rig.profile(brow, 0.0)
    print("  %-10s %12s %12s" % ("SunglassInner", "차(획@0.75)", "차(획@0.60)"))
    for I in Ivals:
        sg = [rig.Shape("SunglassLensBack", sun_lens(I, False), True, True, 0),
              rig.Shape("SunglassLensFront", sun_lens(I, True), True, True, 0),
              rig.Shape("SunglassBridge",
                        [sun_lens(I, False)[0], (0.0, prod_const("SunglassBridgeRiseRatio")),
                         sun_lens(I, True)[-1]], False, False, 1)]
        d = rig.max_delta(rig.profile(sg, 0.0), pb)
        print("  %-10.4f %12.2f %12.2f"
              % (I, d / H.stroke_in_R(0.75), d / H.stroke_in_R(0.60)))
    print("  (하한 1.00획. 실루엣 프로파일은 채움 경계 기준이라 코다리 rise는 이 축을 안 움직인다)")
    print()


if __name__ == "__main__":
    P = calib()
    axis_A(P)
    axis_B(P)
    axis_C(P)
    I, cap, need = axis_D(P)
    axis_E(P, [I, need, (need + cap) / 2, cap])
