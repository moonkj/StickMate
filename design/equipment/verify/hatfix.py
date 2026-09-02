# -*- coding: utf-8 -*-
"""★ 모자 6종 "과하게 덮는다" 처방 좌표 — 검산 하니스 (2026-09-02, design-equipment)

사용자 신고 2건:
  "털모자착용시 거의 머리전체를가림"
  "지금 모자도 과하게 머리를 덮는거 같아 ㅁ자 창때문에 이것도 대부분의 머리를 가림"

이 파일은 **처방 좌표를 items.py 대신 끼워 넣고 verify.py를 그대로 실행한다**
(prodverify.py와 같은 방식 — 검사를 두 벌 적으면 두 자가 갈라진다).

    python3 hatfix.py            # 처방 좌표로 30종 전수 + 남는 머리 + 배율축
    python3 hatfix.py --control  # ★ 양성 대조: 일부러 나쁜 값을 넣고 검사가 빨간불을 내는지

처방의 근거는 docs/EQUIPMENT_SHAPE_SPEC.md 14절.
"""
import sys, os, math, types
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, headroom as H
from rig import Shape

# ---------------------------------------------------------------------------
# 처방 좌표 (머리 중심 원점, R 배수). 왕관·베레모는 **손대지 않는다** — 이미 목표치 통과.
# ---------------------------------------------------------------------------
CAP_CROWN = [(-0.94,-0.22), (-1.02, 0.36), (-0.72, 1.02), (0.00, 1.24),
             ( 0.72, 1.00), ( 1.00, 0.34), ( 0.94,-0.06), (0.60, 0.04), (-0.40, 0.06)]
CAP_BRIM  = [(-0.40, 0.06), ( 0.60, 0.04), ( 1.36,-0.02), ( 1.92,-0.34),
             ( 1.22,-0.54), ( 0.62,-0.26), (-0.06,-0.12)]

# 털모자 — 관과 단을 **한 채움**으로 합치고, 접힌 자리는 그늘색 **낱선**으로 긋는다.
#   근거: 규칙 1-C(색면 조건, AccessoryFillAreaRuleTests)는 채운 도형에 ρ_max >= 0.21818R
#   (= 두께 0.436R)을 요구한다. 밑단을 −0.26R까지 올린 단은 두께가 0.20R뿐이라 **채움으로는
#   그 규칙을 통과할 수 없다.** 이 배율에서 단은 덩어리가 아니라 선이다(원칙 7 선 위계).
BEA_CROWN = [(-0.56,-0.26), (-0.96,-0.06), (-1.06, 0.52), (-0.62, 1.16), (0.00, 1.32),
             ( 0.62, 1.14), ( 1.06, 0.50), ( 0.96,-0.06), ( 0.56,-0.26)]
BEA_FOLD  = [(-0.96,-0.06), (0.96,-0.06)]          # 관 허리 두 점을 그대로 받는다(규칙 4-a)
BEA_POM   = rig.poly(-0.10, 1.44, 0.28, 10, 90.0)

# 중절모 = 스냅 브림. **앞 챙만 두껍고 아래로 꺾인다**(뒤는 얇고 살짝 들린다).
#   앞뒤를 다 두껍게 하면 규칙 1-C는 더 여유롭지만 뒤통수 머리카락을 덮어 화면 면적이
#   17% -> 14%로 떨어진다(13-3-a가 이미 지목한 축). 앞만 두껍게 하면 16%에서 멈춘다.
FED_BRIM  = [(-1.68, 0.16), (-0.98, 0.10), ( 0.98, 0.06), ( 2.06, 0.28),
             ( 1.44,-0.46), ( 0.94,-0.24), (-0.94,-0.26), (-1.40,-0.22)]
FED_CROWN = [(-0.98, 0.10), (-0.92, 0.86), (-0.42, 1.16), (0.42, 1.14), (0.92, 0.82), (0.98, 0.06)]

STR_BRIM  = [(-2.06, 0.16), (-0.86, 0.10), ( 0.86, 0.08), ( 2.18, 0.30),
             ( 1.56,-0.40), ( 0.92,-0.24), (-0.92,-0.26), (-1.52,-0.20)]
STR_CROWN = [(-0.86, 0.10), (-0.74, 0.92), (0.00, 1.14), (0.74, 0.90), (0.86, 0.08)]


def prescribed():
    return {
        "야구모자": [Shape("HatCrown", CAP_CROWN, filled=True),
                     Shape("HatBrim",  CAP_BRIM,  filled=True, tone=1)],
        "털모자":   [Shape("BeanieCrown", BEA_CROWN, filled=True),
                     Shape("BeanieCuff",  BEA_FOLD, loop=False, tone=2),   # tone 2 = Shade(= 채움색 x 0.62)
                     Shape("BeaniePom",   BEA_POM,   filled=True, tone=1)],
        "중절모":   [Shape("FedoraBrim",  FED_BRIM,  filled=True),
                     Shape("FedoraCrown", FED_CROWN, filled=True),
                     Shape("FedoraBand",  [(-0.98, 0.10), (0.98, 0.06)], loop=False, tone=1)],
        "밀짚모자": [Shape("StrawBrim",  STR_BRIM,  filled=True),
                     Shape("StrawCrown", STR_CROWN, filled=True),
                     Shape("StrawBand",  [(-0.86, 0.10), (0.86, 0.08)], loop=False, tone=1)],
    }


# ---------------------------------------------------------------------------
# ★ 감쌈 엄격판 — verify.py의 감쌈 린트는 "|x| >= 0.85 이면서 y <= 0.05 인 정점"만 본다.
#   그 정점이 **머리 원 밖**이어도 통과한다(현행 밀짚 (−1.20,−0.22) r=1.22가 그 예다).
#   여기서는 r <= 1.05 를 더해 "머리 옆구리에 실제로 잉크가 있는가"를 잰다.
# ---------------------------------------------------------------------------
WRAP_MAX_RADIUS = 1.05

def wrap_strict(HEADSET):
    out = []
    for n, sh in HEADSET.items():
        if n == "왕관": continue
        ok = any(abs(q[0]) >= 0.85 and q[1] <= 0.05 and math.hypot(*q) <= WRAP_MAX_RADIUS
                 for s in sh for q in s.pts)
        if not ok: out.append(n)
    return out



# ---------------------------------------------------------------------------
# ★ 규칙 1-C — 색면 조건 (Tests/EditMode/AccessoryFillAreaRuleTests의 오프라인 거울)
#   채운 도형은 자기 윤곽선(폭 W_out, 경계에 중심)에 안쪽으로 W_out/2를 잃는다.
#   색면 폭 >= 획 하나  <=>  ρ_max >= W_out.  ρ_max = 최대 내접원 반경.
#   ★ W_out은 **낱선 획이 아니다** — M6(2026-09-02) 이후 채움 윤곽선의 하한은 1.00pt이고,
#     배율 0.591 이상에서는 하한이 안 물려 0.048/0.22 = 0.21818 R **상수**다.
#     (headroom.py는 낱선 획 2.00pt로 재므로 모자 잉크를 **과대평가**한다 — 보수적이다.)
# ---------------------------------------------------------------------------
FILL_OUTLINE_PEN_IN_R = 0.048 / 0.22        # = AccessoryShapeBuilder.FillOutlineBudgetInHeadRadii(s>=0.591)
RULE_1C_GATE_STROKES = 1.00
RULE_1C_TARGET_STROKES = 1.20


def _inside(pts, q):
    inside = False; n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        if (a[1] > q[1]) != (b[1] > q[1]):
            x = a[0] + (q[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])
            if q[0] < x: inside = not inside
    return inside


def _dist_to_edge(pts, q):
    best = 1e9; n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        dx, dy = b[0] - a[0], b[1] - a[1]; L2 = dx * dx + dy * dy
        t = 0.0 if L2 < 1e-12 else max(0.0, min(1.0, ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy) / L2))
        best = min(best, math.hypot(q[0] - (a[0] + dx * t), q[1] - (a[1] + dy * t)))
    return best


def rho_max(pts, coarse=0.004, refine=6):
    """최대 내접원 반경(R). 거리함수가 1-Lipschitz라 격자 + 국소 정밀화로 충분하다."""
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    best = 0.0; bp = (xs[0], ys[0]); h = coarse
    x = min(xs)
    while x <= max(xs):
        y = min(ys)
        while y <= max(ys):
            if _inside(pts, (x, y)):
                d = _dist_to_edge(pts, (x, y))
                if d > best: best, bp = d, (x, y)
            y += h
        x += h
    for _ in range(refine):
        h *= 0.4
        cx, cy = bp
        for i in range(-3, 4):
            for j in range(-3, 4):
                q = (cx + i * h, cy + j * h)
                if _inside(pts, q):
                    d = _dist_to_edge(pts, q)
                    if d > best: best, bp = d, q
    return best


def rule_1c(HEADSET, quiet=False):
    bad = []
    if not quiet:
        print("╔══ 규칙 1-C 색면 조건 (ρ_max >= %.5fR = 1획, 권장 %.5fR = 1.20획) ══╗"
              % (FILL_OUTLINE_PEN_IN_R, FILL_OUTLINE_PEN_IN_R * RULE_1C_TARGET_STROKES))
    for n, sh in HEADSET.items():
        for s in sh:
            if not s.filled: continue
            r = rho_max(s.pts); k = r / FILL_OUTLINE_PEN_IN_R
            if k < RULE_1C_GATE_STROKES: bad.append("%s %s %.4fR" % (n, s.name, r))
            if not quiet:
                print("  %s %-6s %-12s ρ_max %.4fR = %.2f획%s"
                      % ("OK " if k >= RULE_1C_GATE_STROKES else "✗  ", n, s.name, r, k,
                         "   ← 권장 1.20 미달" if k < RULE_1C_TARGET_STROKES else ""))
    if not quiet:
        print("╚══ 위반 %d건 ══╝" % len(bad))
    return bad


def install(HEADSET):
    """items.HEAD만 바꿔치기하고 verify.py를 그대로 실행한다."""
    here = os.path.dirname(os.path.abspath(__file__))
    sys.path.insert(0, here)
    import items as real_items, hair
    items = types.ModuleType("items")
    items.HEAD = dict(real_items.HEAD); items.HEAD.update(HEADSET)
    items.EYES, items.NECK, items.BACK = real_items.EYES, real_items.NECK, real_items.BACK
    items.EYE_FRONT_ONLY, items.COVER = real_items.EYE_FRONT_ONLY, real_items.COVER
    sys.modules["items"] = items
    return items


def run_verify(HEADSET, title):
    items = install(HEADSET)
    here = os.path.dirname(os.path.abspath(__file__))
    os.chdir(here)
    print("── 대상: %s ──" % title)
    src = open("verify.py", encoding="utf-8").read()
    exec(compile(src, "verify.py", "exec"), {"__name__": "__main__"})
    ws = wrap_strict(items.HEAD)
    print("★ 감쌈(엄격, r<=%.2f): %s" % (WRAP_MAX_RADIUS, "6종 통과" if not ws else "위반 %s" % ws))
    print()
    rule_1c({k: items.HEAD[k] for k in ("야구모자", "털모자", "중절모", "왕관", "베레모", "밀짚모자")})


if __name__ == "__main__":
    if "--control" in sys.argv:
        import control_hatfix   # noqa: F401  (양성 대조는 별도 파일)
    else:
        run_verify(prescribed(), "처방 좌표 (docs/EQUIPMENT_SHAPE_SPEC.md 14절)")
