# -*- coding: utf-8 -*-
"""R8 — §23-3 처방(×0.62 → ×0.35)이 **안 듣는 7건**의 조형 처방을 숫자로 낸다.

  통로 확인(프로덕션 실측):
    CharacterAccessoryRenderer.AddShape  →  if (shape.Filled) outline = FillOutlineColor(color);
    즉 Filled == false 인 조각은 **배수가 호출되지 않는다. 바뀌는 픽셀이 0개다.**

  7건은 두 무리다.
    A군(5) 겹친 띠/테 — 부모 채움의 변 **그 자체**라 자유 윤곽이 구조적으로 0.00획.
           처방: 띠에 **두께**를 줘 닫힌 채움으로 만든다. 그러면 ×0.35 가 이 조각에 닿는다.
    B군(2) 코다리 — 이미 자유 윤곽이 있으나 **짧다**(0.43 / 0.82획).
           처방: 아치 정점을 올려 자유 길이를 1획 위로. ★ 색 처방과 무관한 조형 결함이다.

교정을 먼저 통과시킨다(자유 윤곽 계산기 · 규칙 1 · 실루엣 자).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, headroom as H, sectors as S
from rig import Shape
from r5_mono import free_outline

W75, W60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)


def get(tbl, k):
    v = tbl[k]
    return v() if callable(v) else list(v)


def calib():
    print("╔══ 교정 ══╗")
    p = Shape("P", [(-2, -2), (2, -2), (2, 2), (-2, 2)], True, filled=True)
    a = Shape("A", [(-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5)], True, filled=True, tone=1)
    b = Shape("A", [(5, 5), (6, 5), (6, 6), (5, 6)], True, filled=True, tone=1)
    c0 = free_outline([p, a], W75)[0]; c1 = free_outline([p, b], W75)[0]
    ok1 = abs(c0) < 1e-9 and abs(c1 - 1) < 1e-9
    print("  [1] 자유윤곽 안=0.0 / 밖=1.0            %-5s (%.4f / %.4f)" % (ok1, c0, c1))
    # 규칙 1 : 짧은 변을 일부러 넣으면 잡히는가 / 정상 사각형은 안 잡히는가
    short = Shape("S", [(0, 0), (2, 0), (2, 0.05 * W75), (0, 0.05 * W75)], True, filled=True)
    good = Shape("G", [(0, 0), (2, 0), (2, 2), (0, 2)], True, filled=True)
    ok2 = rig.rule_one(short, W75) is not None and rig.rule_one(good, W75) is None
    print("  [2] 규칙1 짧은변 잡힘 / 정상 통과       %-5s (%s)" % (ok2, rig.rule_one(short, W75)))
    tiny = Shape("T", [(0, 0), (W75, 0), (W75, W75), (0, W75)], True, filled=True)
    ok3 = "잉크 사각형" in (rig.rule_one(tiny, W75) or "")
    print("  [3] 잉크 사각형 하한 잡힘               %-5s (%s)" % (ok3, rig.rule_one(tiny, W75)))
    if not (ok1 and ok2 and ok3):
        sys.exit("★ 교정 실패 — 판정 폐기")
    print("  교정 3/3 OK\n")


# ---------------------------------------------------------------- A군
def band_quad(base, h):
    """열린 띠 선(밑변)을 위로 h 만큼 밀어올려 닫힌 사각/다각 띠로 만든다."""
    up = [(x, y + h) for x, y in base]
    return base + list(reversed(up))


A_GROUP = [
    ("HEAD 중절모", items.HEAD, "중절모", "FedoraBand", "FedoraCrown"),
    ("HEAD 밀짚모자", items.HEAD, "밀짚모자", "StrawBand", "StrawCrown"),
    ("HEAD 왕관", items.HEAD, "왕관", "CrownRim", "CrownBody"),
    ("HEAD 베레모", items.HEAD, "베레모", "BeretRim", "BeretBody"),
    ("HAIR 바가지머리", hair.SET, "바가지머리", "HairFringe", "HairMass"),
]


def solve_A():
    print("╔══ A군 — 겹친 띠/테 5종을 **채운 띠**로 (h = 띠 두께, R 배수) ══╗")
    print("  판정선: 두 배율에서 규칙 1 통과 + 자기교차 없음 + 부모 채움 안에 머무름")
    for label, tbl, key, aname, pname in A_GROUP:
        sh = get(tbl, key)
        acc = [s for s in sh if s.name == aname][0]
        par = [s for s in sh if s.name == pname][0]
        ptop = max(y for _, y in par.pts)
        base = list(acc.pts)
        # 규칙 1 하한: 세로 변(양끝 꺾임)이 1.0W 이상이어야 한다
        need75, need60 = W75, W60
        ok = {}
        for w, nm in ((W75, "0.75"), (W60, "0.60")):
            h = w                                  # 하한 그대로
            cand = Shape(aname, band_quad(base, h), True, filled=True, tone=1)
            v = rig.rule_one(cand, w)
            si = rig.self_intersects(cand.pts)
            inside = max(y for _, y in cand.pts) <= ptop + 1e-9
            ok[nm] = (h, v, si, inside)
        h75, v75, s75, i75 = ok["0.75"]; h60, v60, s60, i60 = ok["0.60"]
        # 부모 높이 대비 비율
        pbot = min(y for _, y in par.pts); ph = ptop - pbot
        print("  %-14s 띠 폭 %.3fR · 부모 높이 %.3fR" % (label, max(x for x, _ in base) - min(x for x, _ in base), ph))
        print("      h(0.75) = %.4fR = %.0f%% 부모높이 · 규칙1 %s · 자기교차 %s · 부모 안 %s"
              % (h75, 100 * h75 / ph, v75 or "OK", s75, i75))
        print("      h(0.60) = %.4fR = %.0f%% 부모높이 · 규칙1 %s · 자기교차 %s · 부모 안 %s"
              % (h60, 100 * h60 / ph, v60 or "OK", s60, i60))


# ---------------------------------------------------------------- B군
B_GROUP = [("EYES 선글라스", "선글라스", "SunglassBridge"),
           ("EYES 동그란안경", "동그란안경", "RoundBridge")]


def solve_B():
    print("\n╔══ B군 — 코다리 2종: 아치 정점을 얼마나 올려야 자유 윤곽 1획인가 ══╗")
    for label, key, aname in B_GROUP:
        sh = get(items.EYES, key)
        acc = [s for s in sh if s.name == aname][0]
        others = [s for s in sh if s.name != aname]
        apex0 = acc.pts[1][1]
        for w, nm in ((W75, "0.75"), (W60, "0.60")):
            cur = free_outline(sh, w)[1]
            lo, hi = apex0, apex0 + 1.5
            for _ in range(60):
                mid = (lo + hi) / 2
                pts = [acc.pts[0], (acc.pts[1][0], mid), acc.pts[2]]
                trial = others + [Shape(aname, pts, False, tone=1)]
                if free_outline(trial, w)[1] >= w: hi = mid
                else: lo = mid
            need = hi
            pts = [acc.pts[0], (acc.pts[1][0], need), acc.pts[2]]
            trial = others + [Shape(aname, pts, False, tone=1)]
            f, L = free_outline(trial, w)
            r1 = rig.rule_one(Shape(aname, pts, False, tone=1), w)
            top = max(y for s in trial for _, y in s.pts)
            print("  %-14s 배율 %s : 현재 %.2f획 → 정점 y %.3f → %.3fR 이면 %.2f획 "
                  "(정점 +%.3fR) 규칙1 %s · 최고점 %.3fR(< 1.15 정수리)"
                  % (label, nm, cur / w, apex0, need, L / w, need - apex0, r1 or "OK", top))




# ---------------------------------------------------------------- 규칙 1-C (색면 조건)
# ★ 이 규칙이 A군 처방의 **진짜 구속 조건**이다. 털모자의 접힌 단이 정확히 여기서 죽었다
#   (AccessoryShapeBuilder.cs 「단을 얇게가 아니라 선으로」 주석 / AccessoryFillAreaRuleTests).
#      색면이 존재한다  ⟺ ρ_max > W_out/2
#      색면 폭 ≥ 1획    ⟺ ρ_max ≥ W_out = 0.21818 R  (배율 0.509 이상에서 상수)
W_OUT = 0.048 / 0.22          # BaselineStrokeWidth / BaselineHeadVisualRadius = 0.21818 R


def rho_max(pts, grid=400):
    """최대 내접원 반경 — 격자 + 국소 정련. bbox 짧은변/2 는 **상계**라 쓰지 않는다."""
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)

    def inside(q):
        c = False; n = len(pts)
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            if (a[1] > q[1]) != (b[1] > q[1]):
                x = a[0] + (q[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])
                if q[0] < x: c = not c
        return c

    def dist(q):
        best = 1e9; n = len(pts)
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            dx, dy = b[0] - a[0], b[1] - a[1]
            l2 = dx * dx + dy * dy
            t = 0.0 if l2 < 1e-15 else max(0.0, min(1.0, ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy) / l2))
            best = min(best, math.hypot(q[0] - (a[0] + dx * t), q[1] - (a[1] + dy * t)))
        return best

    best, bq = 0.0, None
    for i in range(grid + 1):
        for j in range(grid + 1):
            q = (x0 + (x1 - x0) * i / grid, y0 + (y1 - y0) * j / grid)
            if inside(q):
                d = dist(q)
                if d > best: best, bq = d, q
    step = max((x1 - x0), (y1 - y0)) / grid
    for _ in range(40):                      # 국소 정련
        step *= 0.7; improved = False
        for dx in (-step, 0, step):
            for dy in (-step, 0, step):
                q = (bq[0] + dx, bq[1] + dy)
                if inside(q):
                    d = dist(q)
                    if d > best: best, bq, improved = d, q, True
        if not improved: continue
    return best


def rule1c():
    print("\n╔══ ★ A군의 진짜 구속 조건 — 규칙 1-C 색면 조건 (ρ_max ≥ W_out = %.5f R) ══╗" % W_OUT)
    print("  [교정] 반지름 0.5 원판 ρ_max = %.4f (기대 0.5) · 폭 0.3 띠 ρ_max = %.4f (기대 0.15)"
          % (rho_max([(0.5 * math.cos(a * math.pi / 12), 0.5 * math.sin(a * math.pi / 12)) for a in range(24)]),
             rho_max([(-2, 0), (2, 0), (2, 0.3), (-2, 0.3)])))
    for h in (0.3439, 0.4298, 0.44, 0.46, 0.50):
        line = []
        for label, tbl, key, aname, pname in A_GROUP:
            acc = [s for s in get(tbl, key) if s.name == aname][0]
            r = rho_max(band_quad(list(acc.pts), h))
            line.append("%s %.4f%s" % (label.split()[1], r, "" if r >= W_OUT else "✗"))
        print("  h = %.4f R : %s" % (h, " · ".join(line)))
    print("  ⇒ 얇은 띠의 ρ_max ≈ h/2 이므로 하한은 **h ≥ 2·W_out = %.5f R**." % (2 * W_OUT))


if __name__ == "__main__":
    calib(); solve_A(); rule1c(); solve_B()
