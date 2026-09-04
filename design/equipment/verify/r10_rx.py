# -*- coding: utf-8 -*-
"""R10 처방 — 진단(`r10_fit.py`)이 찍은 4건에 대한 **값의 판정과 검산**.

 B. 긴망토 `CapeFold2.endRatioOverride`   0.96 → ?
 C. 털모자 `BeanieCuff` 낱선 → 채운 띠     (관을 두 채움으로 가른다)
 D. 선글라스 `SunglassInnerRatio` / 동그란안경 코다리 span
 E. 44px 카드에서 선글라스 ↔ 뿔테안경이 갈라지는가

원칙(2026-09-03 이 팀이 배운 것): **여유 0짜리 값을 출하하지 않는다.**
그래서 어느 축이든 "통과한다"가 아니라 "얼마나 여유를 두고 통과하는가"를 찍는다.

실행:  cd design/equipment/verify && python3 r10_rx.py
"""
import math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig, items, headroom as H
from r10_fit import (production, prod_const, inside, sdist, seg_dist, sample,
                     rho_max, clip_poly, sun_lens, calib)

W75, W60 = H.stroke_in_R(0.75), H.stroke_in_R(0.60)
W_OUT = 0.048 / 0.22          # 채움 윤곽선 폭(R) — 배율 0.509 이상에서 상수


# ============================================================ B
def rx_B(P):
    print("╔══ B 처방 — 긴망토 CapeFold2 endRatioOverride ══╗")
    L = {s["name"]: s["pts"] for s in P["BACK"]["긴망토"]}
    out, f1, f2 = L["CapeOutline"], L["CapeFold"], L["CapeFold2"]
    trail = prod_const("LongCapeSpreadRatio")
    yend = f2[1][1]

    def measure(e):
        seg = [f2[0], (-trail * e, yend)]
        q = sample(seg, 4000)
        d = [sdist(out, p) for p in q]
        gapv = [min(seg_dist(p, f1[i], f1[i + 1]) for i in range(len(f1) - 1)) for p in q]
        return dict(minclr=min(d),
                    o75=sum(1 for x in d if x < W75 / 2) / len(d),
                    o60=sum(1 for x in d if x < W60 / 2) / len(d),
                    gap=min(gapv),
                    m75=sum(1 for g in gapv if g < W75) / len(gapv),
                    m60=sum(1 for g in gapv if g < W60) / len(gapv))

    # 형제(짧은망토·판초)의 두 주름이 서로 얼마나 붙어 있는가 = 기준선
    print("  ── 기준선: 건강한 형제 2종의 CapeFold ↔ CapeFold2 ──")
    for it in ("짧은망토", "판초"):
        S = {s["name"]: s["pts"] for s in P["BACK"][it]}
        a, b = S["CapeFold"], S["CapeFold2"]
        q = sample(b, 4000)
        gv = [min(seg_dist(p, a[i], a[i + 1]) for i in range(len(a) - 1)) for p in q]
        print("    %-6s 최소간격 %.4f R · 합쳐진 길이 %5.1f%%(0.75) / %5.1f%%(0.60)"
              % (it, min(gv), 100 * sum(1 for g in gv if g < W75) / len(gv),
                 100 * sum(1 for g in gv if g < W60) / len(gv)))

    print("\n  %-6s %9s %10s %9s %9s %10s %8s %8s"
          % ("e", "끝점x", "최소여유R", "W/2@.75", "W/2@.60", "주름간격R", "합침.75", "합침.60"))
    rows = []
    e = 0.800
    while e <= 0.941:
        m = measure(e)
        rows.append((e, m))
        print("  %-6.3f %9.4f %10.4f %8.1f%% %8.1f%% %10.4f %7.1f%% %7.1f%%"
              % (e, -trail * e, m["minclr"], 100 * m["o75"], 100 * m["o60"],
                 m["gap"], 100 * m["m75"], 100 * m["m60"]))
        e = round(e + 0.01, 3)

    # 판정 — 문턱 두 개(경계 여유)만이 판별력을 갖는다. 합침 축은 아래에서 따로 다룬다.
    def okrow(m):
        return m["minclr"] >= 0.1241 and m["o75"] <= 0.030 and m["o60"] <= 0.030
    win = [e for e, m in rows if okrow(m)]
    print("\n  문턱: 최소여유 ≥ 0.1241R(건강한 5건의 최저) · W/2내 ≤ 3.0%(0.75 **및** 0.60)")
    print("  창 = [%.3f, %.3f]" % (min(win), max(win)))
    print("  ★ 창의 아래끝(0.80)은 **CapeFold와 교차하는 자리**다 — 두 주름의 끝 y가 같고")
    print("     CapeFold2의 시작이 더 뒤라, e < 0.80이면 순서가 뒤집혀 천 위에 X가 생긴다.")
    print("  ★ 창의 위끝(0.86)은 **절벽**이다 — 0.87에서 W/2내가 4.0%% -> 100%%로 튄다.")
    print("     선이 뒤쪽 변과 거의 평행해져 길이 전체가 획 반폭 안에 들어간다. 벼랑 끝에 앉히지 않는다.")
    pick = 0.84
    m = measure(pick)
    print("\n  ★ 최소 처방 e = %.2f (끝점 x = %.4f R) — 창의 아래쪽 3분의 2 지점" % (pick, -trail * pick))
    print("     최소여유 %.4f R (문턱 0.1241, 여유 %+.4f) · W/2내 %.1f%%(0.75) / %.1f%%(0.60)"
          % (m["minclr"], m["minclr"] - 0.1241, 100 * m["o75"], 100 * m["o60"]))
    print("     ↑ 건강한 5건 기준선 W/2내 0.9~1.5%(0.75) / 1.6~2.9%(0.60) **안**이다.")
    cur = measure(0.96)
    print("     (현행 0.96: 최소여유 %.4f · 안쪽 82.8%% · W/2내 %.1f%%/%.1f%% — 밖으로 나간다)"
          % (cur["minclr"], 100 * cur["o75"], 100 * cur["o60"]))

    print("\n  ── 정직한 대가: 두 주름이 한 줄로 합쳐진다 ──")
    print("  %-24s %10s %9s %9s" % ("", "최소간격R", "합침@.75", "합침@.60"))
    for lab, mm in (("현행 0.96", cur), ("처방 0.84", m)):
        print("  %-24s %10.4f %8.1f%% %8.1f%%" % (lab, mm["gap"], 100 * mm["m75"], 100 * mm["m60"]))
    print("  형제 기준선: 짧은망토 38.7%/66.6% · 판초 59.5%/99.6% — **합침은 이 가족의 상수**다.")
    print("  0.84가 0.96보다 나쁜 유일한 축이고, 대신 0.96은 천 밖으로 17.2%가 나간다.")

    print("\n  ── 완전 처방(별도 배정 후보): CapeFold의 끝도 함께 당겨 부채를 만든다 ──")
    print("  %-9s %-9s %10s %9s %9s %10s %8s %8s"
          % ("f1 e", "f2 e", "f2최소여유", "f1여유", "W/2@.60", "주름간격R", "합침.75", "합침.60"))
    for e1 in (0.80, 0.70, 0.60, 0.52):
        for e2 in (0.84, 0.86):
            s1 = [f1[0], (-trail * e1, yend)]
            s2 = [f2[0], (-trail * e2, yend)]
            q1 = sample(s1, 3000); q2 = sample(s2, 3000)
            d1 = [sdist(out, p) for p in q1]; d2 = [sdist(out, p) for p in q2]
            gv = [min(seg_dist(p, s1[0], s1[1]) for _ in (0,)) for p in q2]
            gv = [seg_dist(p, s1[0], s1[1]) for p in q2]
            print("  %-9.2f %-9.2f %10.4f %9.4f %8.1f%% %10.4f %7.1f%% %7.1f%%"
                  % (e1, e2, min(d2), min(d1),
                     100 * sum(1 for x in d2 if x < W60 / 2) / len(d2),
                     min(gv), 100 * sum(1 for g in gv if g < W75) / len(gv),
                     100 * sum(1 for g in gv if g < W60) / len(gv)))
    print()
    return pick


# ============================================================ C
def crown_x_at(crown, y, sign):
    """관 경계의 x (y 높이, sign=-1 뒤 / +1 앞)."""
    best = None
    n = len(crown)
    for i in range(n):
        p, q = crown[i], crown[(i + 1) % n]
        if (p[1] > y) != (q[1] > y):
            x = p[0] + (y - p[1]) * (q[0] - p[0]) / (q[1] - p[1])
            if sign * x > 0 and (best is None or sign * x > sign * best): best = x
    return best


def rx_C(P):
    print("╔══ C 처방 — 털모자 BeanieCuff: 낱선 → 채운 사다리꼴 ══╗")
    B = {s["name"]: s["pts"] for s in P["HEAD"]["털모자"]}
    crown, cuff = B["BeanieCrown"], B["BeanieCuff"]
    hem = prod_const("BeanieBandBottomRatio")
    half = prod_const("BeanieCuffBottomHalfWidthRatio")
    waist = prod_const("BeanieBandTopRatio")
    TARGET = W_OUT * 1.20      # AccessoryFillAreaRuleTests.TargetStrokes

    print("  관 밑단 %+.2f · 커버선(허리) %+.2f · 어깨 %+.2f/%+.2f" % (hem, waist, 0.52, 0.50))
    print("  1-C 하한 %.5f R(1.00획) · **권장 %.5f R(1.20획)** — 테스트가 "
          "'오프라인 모형이 낙관적'이라고 적어 둔 값이다." % (W_OUT, TARGET))

    print("\n  ── 먼저 반증: 관을 두 채움으로 **가르는** 길은 구조적으로 막혀 있다 ──")
    print("  관 허리(%+.2f)~어깨(%+.2f) 변의 길이 = %.4f R = %.2f획@0.75."
          % (waist, 0.52, math.dist((-0.96, -0.06), (-1.06, 0.52)),
             math.dist((-0.96, -0.06), (-1.06, 0.52)) / W75))
    print("  이 변을 y=ytop 에서 자르면 두 조각이 그 변을 나눠 갖는다. 둘 다 1획이려면")
    print("  %.4f ≥ 2 × %.4f = %.4f 이어야 하는데 **아니다** — 어느 ytop에서도 한쪽이 위반한다."
          % (math.dist((-0.96, -0.06), (-1.06, 0.52)), W75, 2 * W75))
    for ytop in (0.20, 0.28, 0.36):
        low = clip_poly(crown, ytop, True); up = clip_poly(crown, ytop, False)
        v1 = rig.rule_one(rig.Shape("low", low, True, True, 0), W75)
        v2 = rig.rule_one(rig.Shape("up", up, True, True, 0), W75)
        print("     ytop %+.2f → 하단 %s / 상단 %s" % (ytop, v1 or "OK", v2 or "OK"))
    print("  ⇒ **가르기 폐기.** 관은 그대로 두고 그 위에 사다리꼴 하나를 얹는다(겹치는 채움은")
    print("     이미 이 저장소에 있다 — 망토 3종의 CapeCollar가 CapeOutline 위에 얹혀 있다).")

    print("\n  ── 사다리꼴 높이 h 掃引 (밑변은 관 밑변 그대로, 윗변 두 끝은 관 경계에서 유도) ──")
    print("  %-6s %8s %9s %10s %9s %9s %9s"
          % ("h", "ytop", "ρ_max", "1-C획", "옆변획@.75", "옆변획@.60", "관밖잉크"))
    pick = None
    for h in (0.40, 0.46, 0.50, 0.54, 0.58, 0.62):
        tz, ytop = trapezoid(crown, hem, half, h)
        r = rho_max(tz)
        side = math.dist(tz[0], tz[1])
        outside = ink_outside(tz, crown)
        mark = ""
        if pick is None and r >= TARGET: pick = h
        print("  %-6.2f %+8.3f %9.5f %10.2f %9.2f %9.2f %9s"
              % (h, ytop, r, r / W_OUT, side / W75, side / W60,
                 "0.0%" if outside < 1e-9 else "★%.2f%%" % (100 * outside)))
    print("  ★ 권장 1.20획을 처음 넘는 h = %.2f" % pick)

    tz, ytop = trapezoid(crown, hem, half, pick)
    r = rho_max(tz)
    print("\n  ★ 채택안 — h = %.2f R, 윗변 y = %+.3f R" % (pick, ytop))
    print("     BeanieCuff (4점, loop=True, **filled=True**, tone=Shade 유지)")
    for p in tz: print("        (%+.6f, %+.6f)" % p)
    print("     ρ_max %.5f R = %.2f획 (하한 1.00 여유 %+.1f%% · 권장 1.20 여유 %+.1f%%)"
          % (r, r / W_OUT, 100 * (r / W_OUT - 1), 100 * (r / TARGET - 1)))
    print("     규칙1 @0.75 %s / @0.60 %s · 자기교차 %s"
          % (rig.rule_one(rig.Shape("cuff", tz, True, True, 2), W75) or "OK",
             rig.rule_one(rig.Shape("cuff", tz, True, True, 2), W60) or "OK",
             "★있다" if rig.self_intersects(tz) else "없다"))
    print("     변 길이: %s" % " · ".join("%.4f(%.2f획@0.60)" % (math.dist(tz[i], tz[(i + 1) % 4]),
                                                              math.dist(tz[i], tz[(i + 1) % 4]) / W60)
                                        for i in range(4)))
    print("     관 밖으로 나가는 잉크: %.4f%%  (윤곽 반폭 %.4f R까지 포함해 잰다)"
          % (100 * ink_outside(tz, crown), W_OUT / 2))
    print("     ★ BeanieCrown 은 **한 점도 안 바뀐다** → 실루엣·남는 머리·커버선·감쌈 전부 불변.")

    print("\n  ── 현행(낱선) ↔ 채택안(사다리꼴) ──")
    q = sample(cuff, 4000)
    d = [sdist(crown, p) for p in q]
    print("  %-26s %14s %14s" % ("", "현행 낱선", "채택 사다리꼴"))
    print("  %-26s %14s %14s" % ("잉크 반폭 @0.75", "%.4f R(펜)" % (W75 / 2), "%.4f R(윤곽)" % (W_OUT / 2)))
    print("  %-26s %14s %14s" % ("잉크 반폭 @0.60", "%.4f R(펜)" % (W60 / 2), "%.4f R(윤곽)" % (W_OUT / 2)))
    print("  %-26s %14s %14s" % ("관 밖으로 나감 @0.75",
                                 "%.0f%%" % (100 * sum(1 for x in d if x < W75 / 2) / len(d)), "0%"))
    print("  %-26s %14s %14s" % ("관 밖으로 나감 @0.60",
                                 "%.0f%%" % (100 * sum(1 for x in d if x < W60 / 2) / len(d)), "0%"))
    print("  %-26s %14s %14s" % ("밑단 윤곽선과 붙음", "100%(두 배율)",
                                 "위변은 %.2f R 떨어짐" % (ytop - hem)))
    print("  %-26s %14s %14s" % ("색면(덩어리)", "없음(낱선)", "%.4f R²" % poly_area(tz)))
    print("  %-26s %14s %14s" % ("밑단 위 높이", "0.20 R", "%.2f R" % pick))
    print()
    return pick, ytop, tz


def trapezoid(crown, hem, half, h):
    ytop = round(hem + h, 6)
    xa = crown_x_at(crown, ytop, -1); xb = crown_x_at(crown, ytop, +1)
    return [(-half, hem), (xa, ytop), (xb, ytop), (half, hem)], ytop


def poly_area(p):
    n = len(p)
    return abs(sum(p[i][0] * p[(i + 1) % n][1] - p[(i + 1) % n][0] * p[i][1] for i in range(n))) / 2


def ink_outside(shape, parent, n=4000):
    """도형의 잉크(윤곽 중심선 ± W_out/2)가 부모 채움 밖으로 나가는 길이 비율."""
    q = sample(list(shape) + [shape[0]], n)
    return sum(1 for p in q if sdist(parent, p) < -W_OUT / 2) / len(q)


# ============================================================ D
def rx_D(P):
    print("╔══ D 처방 — B군 코다리 2종 ══╗")
    I = prod_const("SunglassInnerRatio")
    bias = prod_const("SunglassFrontBiasRatio")
    need = 1.5 * W60 / 2.0                     # span = 2I ≥ 1.5 W60

    print("  ── D-1 선글라스: 아치 꼭대기(변 규칙) ──")
    foot = (-I, 0.34)
    for sc, w in ((0.75, W75), (0.60, W60)):
        dy2 = w * w - foot[0] * foot[0]
        print("     배율 %.2f: 변 = 1.00획이 되는 꼭대기 y = %.4f R (현행 0.46 → 변 %.3f획)"
              % (sc, foot[1] + math.sqrt(dy2), math.dist(foot, (0.0, 0.46)) / w))
    print("     ※ 이 변은 **열린 끝**이라 규칙 1 린트가 면제한다 — 위반이 아니라 조형 문제다:")
    print("        0.886획짜리 변은 둥근 캡에 먹혀 아치의 꼭대기가 **점으로 수렴하지 못한다**.")

    print("\n  ── D-2 선글라스: 안쪽 비율 창(窓)이 렌즈 안쪽-아래 x에 어떻게 달렸는가 ──")
    print("  %-10s %10s %10s %9s %9s %9s" % ("안쪽아래x", "아래벽 I≥", "위벽 I≤", "창 폭", "가운데", "여유%"))
    best = None
    for xb in (-0.42, -0.36, -0.32, -0.28, -0.24, -0.22, -0.20, -0.16):
        cap = eye_cap(xb, bias)
        wid = cap - need
        mid = (need + cap) / 2
        marg = min(100 * (mid / need - 1), 100 * (1 - mid / cap)) if wid > 0 else 0.0
        star = ""
        if wid > 0 and (best is None or marg > best[1]): best, star = (xb, marg, mid, cap), ""
        print("  %+10.2f %10.4f %10.4f %9.4f %9.4f %8.1f%%%s"
              % (xb, need, cap, wid, mid, marg, " ← 현행" if abs(xb + 0.32) < 1e-9 else ""))
    print("  ★ 현행 안쪽-아래 x(−0.32)에서 창은 [%.4f, %.4f] 폭 %.4f R, 최대 여유 %.1f%%다."
          % (need, eye_cap(-0.32, bias), eye_cap(-0.32, bias) - need,
             min(100 * ((need + eye_cap(-0.32, bias)) / 2 / need - 1),
                 100 * (1 - (need + eye_cap(-0.32, bias)) / 2 / eye_cap(-0.32, bias)))))
    print("     ★ 위벽은 **의미의 벽**이다 — 넘으면 렌즈가 '눈이 있던 자리'를 안 덮는다.")
    print("       여유 %.4f R = %.3f획@0.75 = %.2f pt(배율 0.75) — Windows 100%%에서 1물리픽셀 미만."
          % ((eye_cap(-0.32, bias) - need) / 2, (eye_cap(-0.32, bias) - need) / 2 / W75,
             (eye_cap(-0.32, bias) - need) / 2 * 0.22 * 0.75 * (846.0 / 24.0)))

    print("\n  ── D-3 동그란안경: span은 렌즈 중심 간격·반경에서 유도된다 ──")
    off = prod_const("RoundLensOffsetRatio"); rad = prod_const("RoundLensRadiusRatio")
    inner = off - rad * math.cos(math.radians(30))
    print("     안쪽 꼭짓점 x = off(%.2f) − r(%.2f)·cos30 = %.5f  →  span %.5f = %.3f획@0.60"
          % (off, rad, inner, 2 * inner, 2 * inner / W60))
    print("     span ≥ 1.5획@0.60 이 되는 두 경로:")
    o_need = need + rad * math.cos(math.radians(30))
    r_need = (off - need) / math.cos(math.radians(30))
    print("       (가) off ≥ %.5f (현행 %.2f, +%.4f)  — 바깥 끝 %.4f R, EYES |x|<1.6 OK"
          % (o_need, off, o_need - off, o_need + rad))
    print("       (나) r  ≤ %.5f (현행 %.2f, −%.4f)  — 렌즈 지름 %.4f R = %.3f획@0.60"
          % (r_need, rad, rad - r_need, 2 * r_need, 2 * r_need / W60))
    # (가)의 눈 커버 여유
    d = math.hypot(o_need - rig.EYE_X, prod_const("RoundLensCenterRiseRatio") - rig.EYE_Y)
    inr = rad * math.cos(math.radians(180.0 / 12))
    print("       (가) 눈 커버: 렌즈 중심↔눈 %.4f R vs 12각형 내접반경 %.4f R → 여유 %+.4f R (%.1f%%)"
          % (d, inr, inr - d, 100 * (inr / d - 1)))
    print("       (나) 12각형 한 변 %.4f R = %.3f획@0.60 (꺾임 30° < 45°라 규칙 1 린트의 사각지대)"
          % (2 * r_need * math.sin(math.radians(15)), 2 * r_need * math.sin(math.radians(15)) / W60))
    print()
    return need, eye_cap(-0.32, bias)


def eye_cap(xbottom, bias):
    """뒤/앞 렌즈가 '눈이 있던 자리'를 계속 덮는 SunglassInnerRatio 상한."""
    def lens(I, forward):
        back = [(-I, 0.34), (-0.96, 0.30), (-1.02, -0.16), (xbottom, -0.44)]
        src = list(reversed(back)) if forward else back
        return [((I + (-p[0] - I) * bias) if forward else p[0], p[1]) for p in src]
    lo, hi = 0.10, 0.90
    for _ in range(90):
        m = (lo + hi) / 2
        if (inside(lens(m, False), (-rig.EYE_X, rig.EYE_Y))
                and inside(lens(m, True), (rig.EYE_X, rig.EYE_Y))): lo = m
        else: hi = m
    return (lo + hi) / 2


# ============================================================ E — 44px 카드
def rx_E(P):
    print("╔══ E — 44px 카드에서 선글라스 ↔ 뿔테안경이 갈라지는가 ══╗")
    ICON, FIT = 44.0, 0.86
    def card(shapes, rise=None, I=None):
        sh = []
        for s in shapes:
            pts = s["pts"]
            if I is not None and s["name"].startswith("SunglassLens"):
                pts = sun_lens(I, s["name"].endswith("Front"))
            if s["name"] == "SunglassBridge":
                bi = I if I is not None else prod_const("SunglassInnerRatio")
                pts = [sun_lens(bi, False)[0], (0.0, rise if rise else prod_const("SunglassBridgeRiseRatio")),
                       sun_lens(bi, True)[-1]]
            sh.append(rig.Shape(s["name"], pts, s["loop"], s["filled"], s["tone"]))
        allp = [p for s in sh for p in s.pts]
        x0, y0, x1, y1 = rig.bounds(allp)
        k = ICON * FIT / max(x1 - x0, y1 - y0)
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        return [rig.Shape(s.name, [((x - cx) * k, (y - cy) * k) for x, y in s.pts],
                          s.loop, s.filled, s.tone) for s in sh], k

    brow, kb = card(P["EYES"]["뿔테안경"])
    pb = rig.profile(brow, 0.0)
    IST = 1.7 * 44 / 40
    print("  카드 획 폭 %.4f px (IST). 프로파일 차는 px 단위 → 획 배수로 환산." % IST)
    print("  %-28s %10s %10s %10s" % ("선글라스 변형", "차(px)", "차(획)", "코다리 변(획)"))
    for label, rise, I in (("현행 rise 0.46 / I 0.28", None, None),
                           ("rise 0.54 / I 0.28", 0.54, None),
                           ("rise 0.666 / I 0.28", 0.666, None),
                           ("rise 0.54 / I 0.3365", 0.54, 0.3365)):
        sg, ks = card(P["EYES"]["선글라스"], rise, I)
        d = rig.max_delta(rig.profile(sg, 0.0), pb)
        br = [s for s in sg if s.name == "SunglassBridge"][0]
        L = min(math.dist(br.pts[i], br.pts[i + 1]) for i in range(len(br.pts) - 1))
        print("  %-28s %10.2f %10.2f %10.2f" % (label, d, d / IST, L / IST))
    print("  ★ 카드는 자기 bbox로 정규화되므로 rise를 올리면 **아이템 전체가 작아진다** —")
    print("     그 축까지 봐야 한다(아래 배율 k).")
    for label, rise, I in (("현행", None, None), ("rise 0.54", 0.54, None), ("rise 0.666", 0.666, None)):
        sg, ks = card(P["EYES"]["선글라스"], rise, I)
        print("     %-10s k = %.3f px/R (뿔테 %.3f)" % (label, ks, kb))
    print()



# ============================================================ F — 배율 문턱 랭킹
def rx_F():
    import items, hair
    def Ws(sc): return max(0.048 * sc, 2.0 / 35.25) / (0.22 * sc)
    rows = []
    for cat, d in (("HEAD", items.HEAD), ("EYES", items.EYES), ("NECK", items.NECK),
                   ("BACK", items.BACK), ("HAIR", hair.SET)):
        for n, sh in d.items():
            for s in sh:
                if not rig.rule_one(s, Ws(0.30)): continue
                lo, hi = 0.30, 1.60
                if rig.rule_one(s, Ws(hi)):
                    rows.append((99.0, cat, n, s.name, "1.60에서도 위반")); continue
                for _ in range(60):
                    m = (lo + hi) / 2
                    if rig.rule_one(s, Ws(m)): lo = m
                    else: hi = m
                rows.append((hi, cat, n, s.name, rig.rule_one(s, Ws(lo))))
    rows.sort(reverse=True)
    print("╔══ F — '규칙1 위반 0이 되는 최소 배율'을 도형별로 (상위 10) ══╗")
    for r in rows[:10]:
        print("  %.4f  %-5s %-6s %-16s %s" % r)
    print("  ★ 코다리 2종만 고쳐도 최소 배율은 %.4f → %.4f 로만 내려간다(Δ %.4f)."
          % (rows[0][0], [r[0] for r in rows if "Bridge" not in r[3]][0],
             rows[0][0] - [r[0] for r in rows if "Bridge" not in r[3]][0]))
    print("     그 다음 벽은 %s %s %s 다 — 0.60에 닿으려면 최소 %d개 도형이 함께 움직여야 한다."
          % (*[r[1:4] for r in rows if "Bridge" not in r[3]][0],
             sum(1 for r in rows if r[0] > 0.60)))
    print()


if __name__ == "__main__":
    P = calib()
    rx_B(P)
    rx_C(P)
    rx_D(P)
    rx_E(P)
    rx_F()
