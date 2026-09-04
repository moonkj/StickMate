# -*- coding: utf-8 -*-
"""⑧ 조정 처방 검산 — 6절/5절이 제시한 조정이 실제로 문턱을 넘는가 (design-equipment, 2026-09-03).

★ 처방을 내기 전에 그 처방으로 다시 잰다. 안 하면 "고쳤다"가 주장으로 남는다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, handoff, items
from rig import Shape
from handoff import parse_items, build, to_R_shape

W75 = rig.stroke_in_R(0.75); FRAME = 1.75
IT = parse_items(); P = {k: build(k, v) for k, v in IT.items()}
def RS(k): return [to_R_shape(k, p, "%s%d" % (p.call, i)) for i, p in enumerate(P[k])]

def wrap_ok(shapes):
    return any(abs(x) >= 0.85 and y <= 0.05 for s in shapes for x, y in s.pts)
def top(shapes): return max(y for s in shapes for x, y in s.pts)
def bot(shapes): return min(y for s in shapes for x, y in s.pts)
def aff(shapes, a=1.0, b=0.0, sx=1.0):
    return [Shape(s.name, [(x * sx, y * a + b) for x, y in s.pts], s.loop, s.filled, s.tone)
            for s in shapes]
def viol(shapes, w=W75):
    return [s.name for s in shapes if rig.rule_one(s, w)]

print("╔══ 교정 ══╗")
print("  [OK] W=%.6f · 액자=%.2f R · 감쌈 판정선 |x|>=0.85 & y<=0.05" % (W75, FRAME))
print("  [OK] 음성 대조 — 조정 전 털모자는 실제로 실패하는가:")
fh = RS("furhat")
print("       조정 전  꼭대기 %+.3f(액자 %s) · 감쌈 %s"
      % (top(fh), "★초과" if top(fh) > FRAME else "안", "있다" if wrap_ok(fh) else "★없다"))

# ── 조정 1. 털모자 : y' = a·y + b ─────────────────────────────────────────
print()
print("╔══ 조정 1. 털모자 — 세로 아핀 변환 ══╗")
print("  목표: 폼폼 꼭대기 <= +1.72 R(우리 현행) · 밴드 밑 <= −0.06 R(우리 현행 커버선)")
y_lo, y_hi = bot(fh), top(fh)
T_LO, T_HI = -0.06, 1.72
a = (T_HI - T_LO) / (y_hi - y_lo); b = T_LO - a * y_lo
print("  유도: a = (%.2f − %.2f)/(%.4f − %.4f) = **%.4f** · b = **%.4f**" % (T_HI, T_LO, y_hi, y_lo, a, b))
fh2 = aff(fh, a, b)
print("  결과: 꼭대기 %+.4f (액자 %s) · 밑 %+.4f · 감쌈 %s · 규칙1 위반 %s"
      % (top(fh2), "OK" if top(fh2) <= FRAME + 1e-9 else "★초과",
         bot(fh2), "있다 OK" if wrap_ok(fh2) else "★없다", viol(fh2) or "0건"))
low = min(((x, y) for s in fh2 for x, y in s.pts), key=lambda p: p[1])
wide = max(((x, y) for s in fh2 for x, y in s.pts), key=lambda p: abs(p[0]))
print("  · 가장 낮은 잉크 (%.3f, %.3f) · 가장 넓은 잉크 (%.3f, %.3f)" % (low + wide))
print("  · 가로는 건드리지 않는다 — 인계본 밴드 폭 %.3f R vs 우리 현행 털모자 %.3f R (이미 같다)"
      % (max(abs(x) for s in fh for x, y in s.pts) * 2,
         max(abs(x) for s in items.HEAD["털모자"] for x, y in s.pts) * 2))

# ── 조정 2. 중절모 : 챙 내리기 ────────────────────────────────────────────
print()
print("╔══ 조정 2. 중절모 — 챙만 내려 감쌈을 만든다 ══╗")
fd = RS("fedora")
print("  조정 전: 밑 %+.4f · 감쌈 %s" % (bot(fd), "있다" if wrap_ok(fd) else "★없다"))
for d in (0.10, 0.20, 0.25, 0.30, 0.35):
    # 챙(조각 2)만 내린다
    test = [Shape(s.name, [(x, y - d) if i == 2 else (x, y) for x, y in s.pts],
                  s.loop, s.filled, s.tone) for i, s in enumerate(fd)]
    print("  −%.2f R → 챙 밑 %+.4f · 감쌈 %-8s · 규칙1 %s"
          % (d, min(y for x, y in test[2].pts), "있다 OK" if wrap_ok(test) else "★없다",
             viol(test) or "0건"))
print("  ★ 챙만 내리면 관과 분리된다. 관 밑변도 같이 내려야 한다 — 전체 −d 를 다시 잰다:")
for d in (0.25, 0.30, 0.35):
    test = aff(fd, 1.0, -d)
    print("  전체 −%.2f R → 꼭대기 %+.4f(액자 %s) · 밑 %+.4f · 감쌈 %-8s · 규칙1 %s"
          % (d, top(test), "OK" if top(test) <= FRAME else "★초과", bot(test),
             "있다 OK" if wrap_ok(test) else "★없다", viol(test) or "0건"))

# ── 조정 3. 선글라스 : 렌즈 간격 ─────────────────────────────────────────
print()
print("╔══ 조정 3. 선글라스 — 렌즈 안쪽 끝을 바깥으로 물린다 ══╗")
sg = RS("sunglasses")
b0, f0 = sg[0], sg[1]
gap0 = rig.bounds(f0.pts)[0] - rig.bounds(b0.pts)[2]
print("  조정 전 간격 %.4f R = %.2f W (우리 현행 0.5600 R = 1.63 W)" % (gap0, gap0 / W75))
print("  %8s %10s %8s %12s %s" % ("바깥이동", "간격R", "간격W", "코다리잉크W", "판정"))
for d in (0.05, 0.10, 0.15, 0.17, 0.20):
    # 안쪽 절반(중심 쪽 x)만 |x| 방향으로 d 민다
    def push(s, sign):
        return [(x + sign * d if (sign > 0 and x < rig.bounds(s.pts)[2] * 0.6)
                 or (sign < 0 and x > rig.bounds(s.pts)[0] * 0.6) else x, y) for x, y in s.pts]
    nb = Shape("b", push(b0, -1), True, True); nf = Shape("f", push(f0, +1), True, True)
    g = rig.bounds(nf.pts)[0] - rig.bounds(nb.pts)[2]
    # 코다리는 두 렌즈 안쪽 끝을 잇는 아치 → 폭이 곧 간격
    print("  %8.2f %10.4f %8.2f %12.2f %s" %
          (d, g, g / W75, g / W75,
           "OK (>=1.5W)" if g / W75 >= 1.5 else "★ 미달"))
print("  ⇒ 코다리 폭 = 렌즈 간격이므로 **한 번의 조정이 둘을 동시에 살린다**.")

# ── 조정 4. 정점색 명도 그라디언트 상한 k ────────────────────────────────
print()
print("╔══ 조정 4. 정점색 명도 그라디언트 상한 k (대역 양끝 동시 만족) ══╗")
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "verify"))
import colorlab as C, band
LO, HI, _ = band.limits()
k = (LO / HI) ** (1 / 2.4)
print("  유도(근사): L ∝ m^2.4  →  k = (L_lo/L_hi)^(1/2.4) = (%.4f/%.4f)^(1/2.4) = **%.4f**" % (LO, HI, k))
print("  실측 검증 — 대역 상한 근처 실제 색으로 확인:")
print("  %-10s %-10s %8s %8s %-10s %8s %s" % ("위 색", "→ 아래", "L(위)", "L(아래)", "아래 hex", "최악CR", "판정"))
for hx in ("#A8332A", "#CC3E2E", "#8C231A", "#A16A28"):
    up = C.hex2rgb(hx)
    dn = tuple(v * k for v in up)
    lo_cr = min(C.CR(dn, bg) for _, bg in band.BACKDROPS)
    up_cr = min(C.CR(up, bg) for _, bg in band.BACKDROPS)
    ok = LO <= C.L(dn) <= HI and LO <= C.L(up) <= HI
    print("  %-10s %-10s %8.4f %8.4f %-10s %8.2f %s" %
          (hx, "×%.3f" % k, C.L(up), C.L(dn), C.rgb2hex(dn), min(lo_cr, up_cr),
           "OK" if ok and min(lo_cr, up_cr) >= 3.0 else "★ 밖(위 색이 이미 대역 밖일 수 있다)"))
print("  ★ k는 **위 색이 대역 상한(L=%.4f)에 있을 때의 최악 상한**이다. 위 색이 더 어두우면" % HI)
print("     k는 더 커질 수 있다(= 그라디언트를 더 못 준다). 아이템마다 계산해야 한다:")
print("     k_item = max(k_floor, (L_lo / L_item)^(1/2.4))  — L_item이 대역 하한이면 k=1.0(불가)")
for hx in ("#A8332A", "#CC3E2E", "#A16A28"):
    up = C.hex2rgb(hx); li = C.L(up)
    ki = (LO / li) ** (1 / 2.4) if li > LO else 1.0
    print("       %-10s L=%.4f → k_max = %.4f  (%.1f%% 어둡게 갈 수 있다)"
          % (hx, li, ki, (1 - ki) * 100))
