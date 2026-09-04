# -*- coding: utf-8 -*-
"""R12 ① — `handoff_dump.py` 솎기·존폐판정 결함의 **양성·음성 대조** (design-equipment, 2026-09-03).

무엇을 증명하려는가
  구판 `handoff_dump.simplify()`가 형태를 잃었는데도 덤프가 **조용히 초록**이었다.
  고쳤다는 말만으로는 같은 병이 재발한다 — **고친 자가 실패를 실패로 보이는지**를 먼저 증명한다.

대조 4종
  A. 음성 — 구판(각도 8도)을 새 감사에 걸면 **반드시 실패**해야 한다. (실패 0건이면 감사가 눈이 먼 것)
  B. 양성 — 신판(DP 0.010 R)은 48조각 전량 통과해야 한다.
  C. 합성 — 해석해를 아는 도형 3종으로 두 판의 행동 차이를 직접 본다.
  D. 변이 — 신판 결과를 **일부러 훼손**하면 감사가 잡아야 한다. (안 잡으면 B의 초록은 무의미하다)

    python3 handoff_dumpfix.py
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, handoff, hatfix
from handoff import parse_items, build, to_R_shape

# 덤프를 import 하면 본문이 돌아버린다 — 함수만 소스에서 뽑아 쓴다.
import importlib.util
_spec = importlib.util.spec_from_file_location("_hd", os.path.join(os.path.dirname(os.path.abspath(__file__)), "handoff_dump.py"))
_src = open(_spec.origin, encoding="utf-8").read()
_cut = _src.index("# ── 본문 ")
_ns = {"__name__": "_hd", "__file__": _spec.origin}
exec(compile(_src[:_cut], "handoff_dump.py", "exec"), _ns)
simplify_new = _ns["simplify"]; audit = _ns["audit_simplify"]; deviation = _ns["deviation"]
piece_metrics = _ns["piece_metrics"]; verdict_new = _ns["verdict"]; TOL = _ns["SIMPLIFY_TOL_R"]

W = rig.stroke_in_R(0.75)
fails = []

def simplify_old(pts, loop, tol_deg=8.0):
    """구판 그대로 — 정점당 회전각 문턱."""
    n = len(pts); keep = []
    for i in range(n):
        if not loop and (i == 0 or i == n - 1): keep.append(i); continue
        if rig.turn_deg(pts[(i - 1) % n], pts[i], pts[(i + 1) % n]) >= tol_deg: keep.append(i)
    if len(keep) < 3: keep = list(range(n))
    return [pts[i] for i in keep]

def verdict_old(long_w):
    """구판 존폐 판정 — max(가로,세로) 하나만 봤다."""
    return "존재" if long_w >= 1.5 else "★소멸"

def try_audit(name, raw, simp, loop):
    try:
        audit(name, raw, simp, loop); return None
    except SystemExit as e:
        return str(e)

# ── 대상 수집 ───────────────────────────────────────────────────────────────
IT = parse_items()
TARGETS = []
for kind, pieces in IT.items():
    for i, p in enumerate(build(kind, pieces)):
        s = to_R_shape(kind, p, "%s%d" % (p.call, i))
        lw, sw, rho, rk = piece_metrics(s)
        if lw >= 1.5: TARGETS.append((kind, s, lw, sw, rk))

print("╔══ A. 음성 대조 — 구판 솎기를 새 감사에 건다 (실패해야 정상) ══╗")
n_old_fail = 0; worst = []
for kind, s, lw, sw, rk in TARGETS:
    sp = simplify_old(s.pts, s.loop)
    br, bs = rig.bounds(s.pts), rig.bounds(sp)
    berr = max(abs(a - b) for a, b in zip(br, bs))
    if try_audit("%s %s" % (kind, s.name), s.pts, sp, s.loop) is not None:
        n_old_fail += 1; worst.append((berr, kind, s.name, len(s.pts), len(sp),
                                       br[3] - br[1], bs[3] - bs[1]))
worst.sort(reverse=True)
print("  구판이 감사에 걸린 조각: %d / %d" % (n_old_fail, len(TARGETS)))
for e in worst[:6]:
    print("    경계손실 %.4fR  %-12s %-5s %3d→%-3d점  세로 %.4fR → %.4fR" % e)
if n_old_fail == 0:
    fails.append("A: 구판이 감사를 통과했다 — 감사가 눈이 멀었다")
print("  판정: %s\n" % ("OK (감사가 구판 결함을 잡는다)" if n_old_fail > 0 else "✗ 실패"))

print("╔══ B. 양성 대조 — 신판 솎기 (전량 통과해야 정상) ══╗")
n_new_fail = 0; dmax = 0.0; ptot = [0, 0]
for kind, s, lw, sw, rk in TARGETS:
    sp = simplify_new(s.pts, s.loop)
    msg = try_audit("%s %s" % (kind, s.name), s.pts, sp, s.loop)
    if msg: n_new_fail += 1; print("    ✗ " + msg.splitlines()[0])
    else: dmax = max(dmax, deviation(s.pts, sp, s.loop))
    ptot[0] += len(s.pts); ptot[1] += len(sp)
print("  신판 감사 실패: %d / %d · 최대편차 %.5f R (= %.3f W, 상한 %.4f R) · 점 %d→%d (%.0f%% 축약)"
      % (n_new_fail, len(TARGETS), dmax, dmax / W, TOL, ptot[0], ptot[1],
         100.0 * (1 - ptot[1] / ptot[0])))
if n_new_fail: fails.append("B: 신판 %d조각 감사 실패" % n_new_fail)
print("  판정: %s\n" % ("OK" if n_new_fail == 0 else "✗ 실패"))

print("╔══ C. 합성 대조 — 해석해를 아는 도형 ══╗")
def circle(r, n):  return [(math.cos(2 * math.pi * i / n) * r, math.sin(2 * math.pi * i / n) * r) for i in range(n)]
def rect(w, h):    return [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
cases = [
    ("원 r=0.5 · 64각형 (정점당 회전 5.63도 < 8도)", circle(0.5, 64), True, 1.0, 0.5),
    ("실오라기 5W x 0.1W (채움)",                     rect(5 * W, 0.1 * W), True, 0.1 * W, None),
    ("정상 색면 3W x 1.6W (채움)",                    rect(3 * W, 1.6 * W), True, 1.6 * W, None),
]
for label, pts, loop, exp_h, _ in cases:
    o = simplify_old(pts, loop); nw = simplify_new(pts, loop)
    ho = rig.bounds(o)[3] - rig.bounds(o)[1]; hn = rig.bounds(nw)[3] - rig.bounds(nw)[1]
    print("  %s" % label)
    print("      원본 세로 %.5fR   구판 %.5fR (%d점)   신판 %.5fR (%d점)"
          % (exp_h, ho, len(o), hn, len(nw)))
    if abs(hn - exp_h) > 1e-9: fails.append("C: 신판이 %s 의 세로를 잃었다" % label)
print()
print("  ── 존폐 판정 대조 (구판 max(가로,세로) vs 신판 1-A + 1-C) ──")
for label, pts, loop, _, _ in cases:
    class _S: pass
    s = _S(); s.pts = pts; s.loop = loop; s.filled = True
    lw, sw, rho, rk = piece_metrics(s)
    vo, vn = verdict_old(lw), verdict_new(s, lw, sw, rk)
    mark = "  ← 구판이 놓친 것" if vo != vn else ""
    print("      %-40s 긴변 %5.2fW  ρ %4.2f획   구판 %-6s → 신판 %-8s%s" % (label, lw, rk, vo, vn, mark))
lw, sw, rho, rk = piece_metrics(_ns["rig"].Shape("t", rect(5 * W, 0.1 * W), True, True))
if verdict_new(_ns["rig"].Shape("t", rect(5 * W, 0.1 * W), True, True), lw, sw, rk) == "존재":
    fails.append("C: 실오라기가 신판에서도 「존재」다")
print()

print("╔══ D. 변이 대조 — 신판 결과를 일부러 훼손하면 감사가 잡는가 ══╗")
kind, s, lw, sw, rk = [t for t in TARGETS if t[0] == "sunglasses"][0]
sp = simplify_new(s.pts, s.loop)
mutations = [
    ("최하단 점 1개 제거", [q for q in sp if q[1] != min(p[1] for p in sp)]),
    ("전체 y 를 0.02R 압축", [(x, y * 0.96) for x, y in sp]),
    ("가운데 점 1개를 0.03R 밀기", [(x + (0.03 if i == len(sp) // 2 else 0), y) for i, (x, y) in enumerate(sp)]),
]
caught = 0
for nm, mut in mutations:
    msg = try_audit("변이:%s" % nm, s.pts, mut, s.loop)
    print("  %-24s → %s" % (nm, "잡힘" if msg else "★ 놓침"))
    if msg: caught += 1
if caught != len(mutations): fails.append("D: 변이 %d건을 감사가 놓쳤다" % (len(mutations) - caught))
print("  판정: %s\n" % ("OK (%d/%d 잡음)" % (caught, len(mutations)) if caught == len(mutations) else "✗ 실패"))

print("═" * 62)
if fails:
    for f in fails: print("✗ " + f)
    sys.exit(1)
print("★ 대조 4종 전량 통과 — 감사는 실패를 실패로 보이고, 신판은 형태를 지킨다.")
