# -*- coding: utf-8 -*-
"""★ R12 ⑥ — 강조띠 두께 판정. 사양서 §5-4 의 「0.3461 R vs 0.2670 R」 불일치를 끝낸다.

결론 먼저
  · **§5-4 의 0.3461 R (1.01 W) 이 맞다.** 0.2670 R (0.777 W) 은 `handoff_dump.py` 의
    솎기 결함(각도 8도 문턱)이 만든 **가짜 숫자**다 — 원본 34점 중 4점만 남으면서 띠의
    아랫변이 통째로 지워졌다. R12에서 그 결함을 고쳤고, 고친 덤프는 0.3461 R 을 낸다.
  · 그런데 **띠의 존폐를 가르는 자는 「높이 >= 1.0 W」가 아니다.** 띠는 **채운 도형**이므로
    규칙 1-C(ρ_max >= 0.21818 R)가 지배한다. 그 자로 재면 인계본 띠 셋은 **전부 탈락**이고,
    높이 1.0 W(=0.3439 R)로는 **원리상 통과할 수 없다** — 필요 두께가 0.43636 R 이기 때문이다.
  · 우리 `AccessoryShapeBuilder.AccentBandThicknessRatio = 0.46f` 는 그 하한을 5.4% 넘긴다.

    python3 r12_band.py
    python3 r12_band.py --control   # 음성 대조: 0.43 로 낮추면 실제로 빨개지는가
"""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig, items, hatfix, handoff
from handoff import parse_items, build, to_R_shape

CONTROL = "--control" in sys.argv
W = rig.stroke_in_R(0.75)
GATE = hatfix.FILL_OUTLINE_PEN_IN_R          # 0.21818 R = 색면 폭 1획
BAND = 0.43 if CONTROL else 0.46             # AccessoryShapeBuilder.AccentBandThicknessRatio
fails = []

def simplify_old(pts, loop, tol_deg=8.0):
    n = len(pts); keep = []
    for i in range(n):
        if not loop and (i == 0 or i == n - 1): keep.append(i); continue
        if rig.turn_deg(pts[(i - 1) % n], pts[i], pts[(i + 1) % n]) >= tol_deg: keep.append(i)
    if len(keep) < 3: keep = list(range(n))
    return [pts[i] for i in keep]

IT = parse_items()
print("╔══ ⑥-1 인계본 강조띠 — 원본(raw) vs 구판 덤프 ══╗")
print("  W@0.75 = %.6f R · 규칙 1-C 하한 ρ >= %.5f R ⇒ 곧은 띠라면 두께 >= %.5f R (= %.3f W)\n"
      % (W, GATE, 2 * GATE, 2 * GATE / W))
print("  %-10s %-4s %10s %8s %10s %8s   %8s %s" %
      ("아이템", "조각", "raw 두께", "raw W", "구판 두께", "구판 W", "ρ_max", "1-C"))
TARGET = [("clothhat", 1), ("fedora", 1), ("crown", 1)]
for kind, idx in TARGET:
    P = build(kind, IT[kind])
    s = to_R_shape(kind, P[idx], "%s%d" % (P[idx].call, idx))
    x0, y0, x1, y1 = rig.bounds(s.pts)
    raw_t = y1 - y0
    o = simplify_old(s.pts, s.loop)
    ox0, oy0, ox1, oy1 = rig.bounds(o)
    old_t = oy1 - oy0
    rho = hatfix.rho_max(s.pts, coarse=0.02, refine=9)
    ok = rho >= GATE
    print("  %-10s [%d]  %10.4f %8.3f %10.4f %8.3f   %8.4f %s" %
          (kind, idx, raw_t, raw_t / W, old_t, old_t / W, rho, "OK" if ok else "✗ %.2f획" % (rho / GATE)))
    if ok: fails.append("%s 띠가 1-C 를 통과했다 — 예상과 다르다" % kind)
print()
print("  ⇒ 리더가 인용한 「clothhat 0.79 W · fedora 0.78 W · crown 0.94 W」는")
print("     **구판 덤프의 훼손된 숫자**다(위 표 5열). raw 는 1.04 / 1.01 / 1.26 W 다.")
print("     ⇒ 「우리 획 하한 1.0 W 미달 3건」이라는 진단은 **철회한다** — 세 개 다 1.0 W 를 넘는다.")
print("     ⇒ 그러나 **1-C 로는 셋 다 탈락**이다. 자를 바꿔야 하는 것이지 숫자가 아깝지 않다.\n")

print("╔══ ⑥-2 왜 1.0 W 로는 원리상 안 되는가 ══╗")
print("  곧은 띠의 ρ_max = 두께/2. 1-C 는 ρ >= %.5f R 을 요구한다." % GATE)
print("  ⇒ 필요 두께 = %.5f R = %.3f W.  **1.0 W(= %.5f R)는 %.1f%% 모자란다.**"
      % (2 * GATE, 2 * GATE / W, W, (1 - W / (2 * GATE)) * 100))
print("  ⇒ 즉 「띠 >= 1획」은 **낱선의 자**를 채움에 잘못 댄 것이다. 채움은 자기 윤곽선에")
print("     안쪽으로 W_out/2 씩 양쪽을 잃으므로 획 하나가 남으려면 획 두 개로 시작해야 한다.\n")

print("╔══ ⑥-3 우리 AccentBandThicknessRatio = %.2f 검산 ══╗" % BAND)
print("  %-14s %10s %10s %8s  %s" % ("도형", "두께(R)", "ρ_max(R)", "획", "1-C"))
bandnames = {"FedoraBand", "CrownRim", "StrawBand", "BeretRim"}
nb = 0
for nm, S in items.HEAD.items():
    for s in S:
        if s.name not in bandnames: continue
        x0, y0, x1, y1 = rig.bounds(s.pts)
        rho = hatfix.rho_max(s.pts, coarse=0.02, refine=9)
        k = rho / GATE
        nb += 1
        print("  %-14s %10.4f %10.4f %8.2f  %s" % (s.name, y1 - y0, rho, k, "OK" if k >= 1.0 else "✗"))
        if k < 1.0: fails.append("%s 가 1-C 미달 (%.2f획)" % (s.name, k))
print("  ⇒ 상수 %.2f R 은 하한 %.5f R 을 **%.1f%% 넘긴다**(여유 %.5f R = %.3f W)"
      % (BAND, 2 * GATE, (BAND / (2 * GATE) - 1) * 100, BAND - 2 * GATE, (BAND - 2 * GATE) / W))
if BAND < 2 * GATE:
    fails.append("상수 %.2f 가 하한 %.5f 미만이다" % (BAND, 2 * GATE))

print()
print("═" * 62)
if CONTROL:
    print("★ 음성 대조 모드(상수 %.2f) — 하한 %.5f 미만이므로 **실패가 나와야 정상**이다." % (BAND, 2 * GATE))
    print("   실패 %d건: %s" % (len(fails), "OK (자가 살아 있다)" if fails else "✗ 자가 눈이 멀었다"))
    sys.exit(0 if fails else 1)
if fails:
    for f in fails: print("✗ " + f)
    sys.exit(1)
print("★ 판정: §5-4 는 0.3461 R 로 **정정 없이 유지**. 다만 게이트를 1-C 로 바꾸고,")
print("   띠 두께는 우리 상수 AccentBandThicknessRatio = 0.46 R 을 인계본에도 적용한다.")
