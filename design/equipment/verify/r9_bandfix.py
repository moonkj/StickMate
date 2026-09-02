# -*- coding: utf-8 -*-
"""R9 — A군 5종 「겹친 띠/테」 처방: 열린 낱선을 **두께 h = 0.46 R 인 채운 띠**로 바꾼다.

왜 h = 0.46 R 인가 (리더 승인, 근거는 r8_rx7.out.txt)
  · 규칙 1-C(색면 조건): 색면이 1획 폭을 가지려면 최대내접원 ρ_max ≥ W_out = 0.048/0.22 = 0.21818 R.
    얇은 띠는 ρ_max ≈ h/2 이므로 **h ≥ 2·W_out = 0.43636 R**.
  · h = 0.4298 R(배율 0.60 의 1획)은 ρ_max 0.2149 로 **간발 미달**(여유 0).
  · h = 0.46 R  → ρ_max 0.2299~0.2300 ≥ 0.21818 (여유 +0.0118 R = +5.4%).

★ 이 파일은 **설계 거울(items.py / hair.py)을 고치지 않는다.**
  거울은 프로덕션을 비추는 물건이고(mirrordrift.py 가 0건을 지킨다), 처방은 `install()`로
  런타임에 얹는다 — `wornfix.py` + `_runverify.py` 가 쓰는 이 디렉터리의 기존 규약이다.
  프로덕션 .cs 반영은 coder 배정이고, 그때 거울도 함께 따라간다.

    python3 r9_bandfix.py            검산표
    python3 r9_runverify.py          처방 설치 + verify.py 전량
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, headroom as H
from rig import Shape

H_BAND = 0.46
W_OUT = 0.048 / 0.22            # 0.21818 R — 규칙 1-C 색면 하한
W75, W60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)

# (표시명, 표, 키, 띠 도형명, 부모 채움 도형명)
A_GROUP = [("HEAD 중절모", items.HEAD, "중절모", "FedoraBand", "FedoraCrown"),
           ("HEAD 밀짚모자", items.HEAD, "밀짚모자", "StrawBand", "StrawCrown"),
           ("HEAD 왕관", items.HEAD, "왕관", "CrownRim", "CrownBody"),
           ("HEAD 베레모", items.HEAD, "베레모", "BeretRim", "BeretBody"),
           ("HAIR 바가지머리", hair.SET, "바가지머리", "HairFringe", "HairMass")]


def get(tbl, k):
    v = tbl[k]
    return v() if callable(v) else list(v)


# ★ 평면 압출이 부모 채움 밖으로 나가는 자리 하나 — 베레모.
#   베레모 몸통의 왼쪽 변은 (-1.46,-0.10) -> (-1.02,+0.62) 로 **기울어 있다**. 그래서 왼끝을
#   수직으로 0.46 올리면 x=-1.46 인 채 y=+0.36 에 놓이고, 그 높이의 몸통 변은 x=-1.1789 다
#   -> 띠가 0.2811 R 만큼 몸통 밖으로 삐져나온다(면적 6.2%, 최대 이탈 0.237 R).
#   0.237 R > W/2 (0.1719 @0.75) 이므로 **자기 윤곽선에 안 가려지고 눈에 보인다.**
#   나머지 4종의 이탈 최대는 0.000~0.064 R 로 전부 W/2 아래라 손대지 않는다.
#   처방: 그 꼭짓점 하나만 몸통 변 위로 물린다. 값은 몸통 좌표에서 **유도**한 것이지 손으로
#   고른 것이 아니다 — assert_clip() 가 매 실행마다 그 유도를 다시 확인한다.
TOP_CLIP = {"BeretRim": {0: (-1.1789, 0.36)}}   # {도형명: {거꾸로 센 윗변 꼭짓점 번호: 좌표}}


def band_quad(base, h=H_BAND, name=None):
    """열린 띠 낱선을 +y 로 h 만큼 밀어 올려 닫힌 채움 띠로 만든다.
    ★ +y 로 미는 이유: 5종 모두 띠가 부모 채움의 **아래쪽 변**이라 위가 부모 안쪽이다."""
    top = [(x, y + h) for x, y in reversed(list(base))]
    for i, pt in (TOP_CLIP.get(name) or {}).items():
        top[i] = pt
    return list(base) + top


def assert_clip():
    """TOP_CLIP 값이 정말 부모 변 위의 점인가 — 손으로 적은 숫자가 아님을 매 실행마다 증명한다."""
    sh = get(items.HEAD, "베레모")
    body = [s for s in sh if s.name == "BeretBody"][0].pts
    a, b = (-1.46, -0.10), (-1.02, 0.62)
    assert a in body and b in body, "베레모 몸통 왼쪽 변이 바뀌었다 — TOP_CLIP 을 다시 유도해라"
    t = ((a[1] + H_BAND) - a[1]) / (b[1] - a[1])
    x = a[0] + (b[0] - a[0]) * t
    got = TOP_CLIP["BeretRim"][0]
    assert abs(got[0] - x) < 5e-4 and abs(got[1] - (a[1] + H_BAND)) < 1e-9, \
        "TOP_CLIP 유도값 %.4f ≠ 적힌 값 %.4f" % (x, got[0])
    return x


def install(h=H_BAND):
    """처방을 items.HEAD / hair.SET 에 얹는다(런타임). 거울 파일은 안 건드린다."""
    for _, tbl, key, aname, _ in A_GROUP:
        sh = get(tbl, key)
        tbl[key] = [Shape(s.name, band_quad(s.pts, h, s.name), True, filled=True, tone=s.tone)
                    if s.name == aname else s for s in sh]


# ---------------------------------------------------------------- 자
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


def rho_max(pts, grid=400):
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    best, bq = 0.0, None
    for i in range(grid + 1):
        for j in range(grid + 1):
            q = (x0 + (x1 - x0) * i / grid, y0 + (y1 - y0) * j / grid)
            if inside(pts, q):
                d = dist_edge(pts, q)
                if d > best: best, bq = d, q
    step = max(x1 - x0, y1 - y0) / grid
    for _ in range(40):
        step *= 0.7
        for dx in (-step, 0, step):
            for dy in (-step, 0, step):
                q = (bq[0] + dx, bq[1] + dy)
                if inside(pts, q):
                    d = dist_edge(pts, q)
                    if d > best: best, bq = d, q
    return best


def spill(band, parents, grid=400):
    """띠 면적 중 부모 채움 합집합 **밖**에 있는 비율(%)과 최대 이탈 거리(R)."""
    xs = [p[0] for p in band]; ys = [p[1] for p in band]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    tot = out = 0; worst = 0.0
    for i in range(grid):
        for j in range(grid):
            q = (x0 + (x1 - x0) * (i + .5) / grid, y0 + (y1 - y0) * (j + .5) / grid)
            if not inside(band, q): continue
            tot += 1
            if not any(inside(p, q) for p in parents):
                out += 1
                worst = max(worst, min(dist_edge(p, q) for p in parents))
    return (100.0 * out / tot if tot else 0.0), worst


def calib():
    print("╔══ 교정 ══╗")
    ok = []
    disc = [(0.5 * math.cos(a * math.pi / 12), 0.5 * math.sin(a * math.pi / 12)) for a in range(24)]
    r = rho_max(disc); ok.append(("ρ_max 반지름0.5 원판 = 0.5", abs(r - 0.5) < 0.006, "%.4f" % r))
    r = rho_max([(-2, 0), (2, 0), (2, 0.3), (-2, 0.3)])
    ok.append(("ρ_max 폭0.3 띠 = 0.15", abs(r - 0.15) < 1e-3, "%.4f" % r))
    short = Shape("S", [(0, 0), (2, 0), (2, 0.05 * W75), (0, 0.05 * W75)], True, filled=True)
    good = Shape("G", [(0, 0), (2, 0), (2, 2), (0, 2)], True, filled=True)
    ok.append(("규칙1 짧은변 잡힘/정상 통과",
               rig.rule_one(short, W75) is not None and rig.rule_one(good, W75) is None, ""))
    sq = [(0, 0), (2, 0), (2, 2), (0, 2)]
    s, w = spill([(1, 1), (3, 1), (3, 1.5), (1, 1.5)], [sq])
    ok.append(("이탈 계산기 = 50%", abs(s - 50) < 2, "%.1f%% 최대 %.3f" % (s, w)))
    for n, v, x in ok: print("  [%s] %-28s %s" % ("OK" if v else "★ ", n, x))
    if not all(v for _, v, _ in ok): sys.exit("★ 교정 실패 — 판정 폐기")
    print()


def table():
    calib()
    print("  [OK] TOP_CLIP 유도 재확인 — 베레모 몸통 변 위 x = %.4f\n" % assert_clip())
    print("╔══ A군 5종 — h = %.2f R 처방 검산 ══╗" % H_BAND)
    print("  W(0.75) = %.4f R · W(0.60) = %.4f R · 규칙 1-C 하한 W_out = %.5f R"
          % (W75, W60, W_OUT))
    print("  %-14s %-12s %8s %9s %9s %8s %9s %9s"
          % ("아이템", "도형", "ρ_max", "1-C", "자기교차", "규칙1@.75", "규칙1@.60", "이탈면적%"))
    for label, tbl, key, aname, pname in A_GROUP:
        sh = get(tbl, key)
        acc = [s for s in sh if s.name == aname][0]
        pars = [s.pts for s in sh if s.tone == 0 and s.filled]
        q = band_quad(acc.pts, H_BAND, aname)
        cand = Shape(aname, q, True, filled=True, tone=acc.tone)
        r = rho_max(q)
        sp, worst = spill(q, pars)
        print("  %-14s %-12s %8.4f %9s %9s %8s %9s %8.1f%% (최대 %.3fR)"
              % (label, aname, r, "OK" if r >= W_OUT else "★미달",
                 "없음" if not rig.self_intersects(q) else "★있음",
                 rig.rule_one(cand, W75) or "OK", rig.rule_one(cand, W60) or "OK", sp, worst))
    print("\n╔══ 좌표 전후 (coder 배정용) ══╗")
    for label, tbl, key, aname, pname in A_GROUP:
        acc = [s for s in get(tbl, key) if s.name == aname][0]
        q = band_quad(acc.pts, H_BAND, aname)
        print("  %s / %s" % (label, aname))
        print("     전: loop=False filled=False tone=%d  %d점  %s"
              % (acc.tone, len(acc.pts), " ".join("(%+.4f,%+.4f)" % p for p in acc.pts)))
        print("     후: loop=True  filled=True  tone=%d  %d점  %s"
              % (acc.tone, len(q), " ".join("(%+.4f,%+.4f)" % p for p in q)))


if __name__ == "__main__":
    table()
