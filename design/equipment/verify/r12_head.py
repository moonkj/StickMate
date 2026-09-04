# -*- coding: utf-8 -*-
"""★ R12 ⑧ — **HEAD 1단계 한 벌(bundle).** 처방을 전부 얹은 뒤 **한 번에** 검산한다.

리더 판정(2026-09-03): *"HEAD 빚 5건을 1단계 안에서 함께 처리한다. 별도 라운드로 빼지 마라.
  상수 항등 때문에 부분 되돌리기가 불가능하므로 슬롯 단위로 커밋을 끊어야 한다 —
  빚을 밖으로 빼면 HEAD를 두 번 건드리게 되고, 그 사이에 되돌릴 수 없는 구조가 들어앉는다."*

★ **왜 한 벌로 재야 하는가 (이 파일의 존재 이유)**
  처방들이 **같은 좌표를 서로 다른 방향으로 민다.** 따로 재면 각각 초록인데 합이 빨갈 수 있다.
  실제로 순서가 결과를 바꾼다 — **액자 축소를 먼저 하면 ρ 가 같이 줄어서 두께 처방을 다시 풀어야 한다.**
  그래서 이 파일은 순서를 고정한다:

      1) 형태 처방(아핀·배율·재구성)   ← 봉투를 정한다
      2) 두께 처방(1-C)              ← 1)의 결과 위에서 s 를 **다시** 푼다
      3) c 재부여(§1-4-4 2단계)       ← 평행이동. 무손실

    python3 r12_head.py
    python3 r12_head.py --control   # 음성 대조: 처방을 빼면 5건이 그대로 빨간가
"""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig, items, hatfix, handoff
from rig import Shape
from handoff import parse_items, build, to_R_shape

CONTROL = "--control" in sys.argv
W = rig.W
GATE = hatfix.FILL_OUTLINE_PEN_IN_R          # 0.21818 R — 무르게 하지 않는다
FRAME = 1.75                                 # AccessoryShapeBuilder.HairCapMaxRatio
BAND = 0.46                                  # AccessoryShapeBuilder.AccentBandThicknessRatio
PAIR_MIN, CORNER = 1.0, rig.CORNER_DEG
S_MAX = 3.0

IT = parse_items()
RAW = {}
for k in ("clothhat", "furhat", "fedora", "crown"):
    RAW[k] = [to_R_shape(k, p, "%s%d" % (p.call, i)) for i, p in enumerate(build(k, IT[k]))]

def rho(pts): return hatfix.rho_max(pts, coarse=0.02, refine=9)
def S(name, pts, **kw): return Shape(name, pts, kw.get("loop", True), kw.get("filled", True),
                                     kw.get("tone", 0), 0)
def aff(sh, a, b):   return S(sh.name, [(x, y * a + b) for x, y in sh.pts], loop=sh.loop, filled=sh.filled, tone=sh.tone)
def scale_about(sh, s, y0, ny0):
    return S(sh.name, [(x * s, (y - y0) * s + ny0) for x, y in sh.pts], loop=sh.loop, filled=sh.filled, tone=sh.tone)
def band(name, hw, y0, tone=1):
    """강조띠를 **곧은 띠로 다시 세운다**(§5-4-1). ρ = 두께/2 = 0.23 R · 끝면 %.2f W."""
    return S(name, [(-hw, y0), (hw, y0), (hw, y0 + BAND), (-hw, y0 + BAND)], tone=tone)

# ── 처방 ────────────────────────────────────────────────────────────────────
# (표에 없는 값은 하나도 안 쓴다. 출처를 주석에 단다.)
RX_DOC = [
 ("clothhat", "관 σ=1.461 (기준점 관 밑변) → 밑변 −0.2200 R", "§1-4-5 신설"),
 ("clothhat", "챙: 인계본 좌표 폐기 → 우리 현행 HatBrim 유지", "§1-4-5 (A 성분)"),
 ("clothhat", "띠: 인계본 좌표 폐기 → 곧은 띠 0.46 R", "§5-4-1"),
 ("furhat",   "세로 아핀 y' = 0.8500·y − 0.1550", "§5-1 기존"),
 ("fedora",   "전체 −0.3500 R", "§5-4 기존"),
 ("fedora",   "띠: 인계본 좌표 폐기 → 곧은 띠 0.46 R", "§5-4-1"),
 ("crown",    "★ 세로 아핀 y' = 0.9164·y − 0.2025", "R12 신설 — 액자"),
 ("crown",    "★ 림: 인계본 19점 폐기 → 곧은 띠 0.46 R", "R12 신설 — 몽당변 + 1-C"),
]

def prescribe():
    OUT = {}
    # clothhat — 관만 인계본에서 온다
    c0 = RAW["clothhat"][0]
    y0 = min(q[1] for q in c0.pts)
    crown = scale_about(c0, 1.461, y0, -0.2200)
    brim = [s for s in items.HEAD["야구모자"] if s.name == "HatBrim"][0]
    OUT["clothhat"] = [S("CapCrown", crown.pts), S("CapBrim", brim.pts, tone=brim.tone),
                       band("CapBand", 0.6982 * 1.461, -0.2200 + 0.20)]
    # furhat — §5-1 아핀. 몸에 오르는 조각만(B1 관 · B3 단)
    # §5-1 조정 3: 인계본 폼폼(지름 1.32 W)은 소멸한다 → **우리 현행 BeaniePom(0.28 R)을 그대로 쓴다**.
    #   폼폼을 빼놓고 액자를 재면 §5-1 이 적은 초과 +0.456 을 재현할 수 없다 — 그 값을 만드는 게 폼폼이다.
    pom = [s for s in items.HEAD["털모자"] if s.name == "BeaniePom"][0]
    OUT["furhat"] = ([aff(s, 0.85, -0.155) for s in RAW["furhat"] if s.name in ("B1", "B3")]
                     + [S("BeaniePom", pom.pts, tone=pom.tone)])
    # fedora — §5-4 −0.35. 관 B0 · 챙 B2 · 띠는 재구성
    fed = [S(s.name, [(x, y - 0.35) for x, y in s.pts], loop=s.loop, filled=s.filled, tone=s.tone)
           for s in RAW["fedora"] if s.name in ("B0", "B2")]
    brimtop = max(q[1] for q in [s for s in fed if s.name == "B2"][0].pts)
    OUT["fedora"] = fed + [band("FedoraBand", 0.6055, brimtop - 0.02)]
    # crown — ★ 신설 아핀 + 림 재구성
    OUT["crown"] = [aff(RAW["crown"][0], 0.9164, -0.2025), band("CrownRim", 1.0136, -0.1000)]
    return OUT

BASE = {k: [s for s in RAW[k]
            if max(rig.bounds(s.pts)[2] - rig.bounds(s.pts)[0],
                   rig.bounds(s.pts)[3] - rig.bounds(s.pts)[1]) >= 1.5 * W
            and not s.name.startswith("H")] for k in RAW}
SET = BASE if CONTROL else prescribe()

print("╔══ ⑧-1 HEAD 처방 목록 (한 벌) ══╗")
if CONTROL:
    print("  ★★ 음성 대조: 처방을 **하나도 안 건다**. 빚 5건이 그대로 빨개야 이 자가 산다.\n")
else:
    for k, what, src in RX_DOC: print("  %-9s %-46s  ← %s" % (k, what, src))
    print()

# ── 2) 두께 처방 — 형태 처방 **뒤에** 다시 푼다 ─────────────────────────────
def minor(pts):
    n = len(pts); cx = sum(p[0] for p in pts) / n; cy = sum(p[1] for p in pts) / n
    sxx = sum((p[0]-cx)**2 for p in pts)/n; syy = sum((p[1]-cy)**2 for p in pts)/n
    sxy = sum((p[0]-cx)*(p[1]-cy) for p in pts)/n
    tr = sxx+syy; d = max(0.0, tr*tr/4 - (sxx*syy-sxy*sxy))**0.5; lm = tr/2-d
    v = (lm-syy, sxy) if abs(sxy) > 1e-12 else ((1.0,0.0) if sxx < syy else (0.0,1.0))
    L = math.hypot(*v) or 1.0
    return (cx, cy), (v[0]/L, v[1]/L)
def thicken(pts, s):
    (cx, cy), (nx, ny) = minor(pts); o = []
    for x, y in pts:
        dx, dy = x-cx, y-cy; t = dx*nx+dy*ny
        o.append((cx+dx+(s-1)*t*nx, cy+dy+(s-1)*t*ny))
    return o

print("╔══ ⑧-2 두께 처방을 **형태 처방 뒤에** 다시 푼다 (1-C %.5f R) ══╗" % GATE)
print("  ★ 순서가 결과를 바꾼다 — 액자 축소가 ρ 를 같이 줄이므로 s 를 다시 풀어야 한다.\n")
resolved = 0
for k in SET:
    for i, sh in enumerate(SET[k]):
        if not (sh.filled and sh.loop): continue
        r0 = rho(sh.pts)
        if r0 >= GATE: continue
        lo, hi = 1.0, S_MAX
        if rho(thicken(sh.pts, hi)) < GATE:
            print("  ★ %-9s %-11s ρ %.4f → 상한 ×%.1f 로도 못 올린다 ⇒ **DROP 후보**" % (k, sh.name, r0, S_MAX)); continue
        for _ in range(22):
            m = (lo+hi)/2
            if rho(thicken(sh.pts, m)) >= GATE: hi = m
            else: lo = m
        SET[k][i] = S(sh.name, thicken(sh.pts, hi), loop=sh.loop, filled=sh.filled, tone=sh.tone)
        print("  %-9s %-11s ρ %.4f → %.4f  (s = ×%.3f)" % (k, sh.name, r0, rho(SET[k][i].pts), hi))
        resolved += 1
print("  ⇒ 두께 재처방 %d건\n" % resolved)

# ── 3) c 재부여 — ★ **조각 단위**로 건다 ────────────────────────────────────
#   ★ 자기 정정: 아이템 단위 c 를 관에 걸면 **이중계상**이다.
#     야구모자의 아이템 c = +0.4500 은 **챙(A 성분)이 만든 값**이고, 관 자체의 중심은 −0.0100 이다.
#     아이템 c 를 관에 걸면 챙(우리 것, 이미 c 를 지고 있다)과 합쳐져 액자·봉투가 두 번 밀린다.
#     ⇒ §1-4-4 1단계가 「조각 단위」인 이유가 여기서도 그대로 나온다.
PIECE_C = {                      # 우리 현행 대응 조각의 중심 x (실측값. 새 숫자를 만들지 않는다)
    ("clothhat", "CapCrown"): "HatCrown",
    ("furhat",   "B1"):       "BeanieCrown",
    ("furhat",   "B3"):       "BeanieCrown",
    ("fedora",   "B0"):       "FedoraCrown",
    ("fedora",   "B2"):       "FedoraBrim",
    ("crown",    "B0"):       "CrownBody",
}
OURPC = {}
for nm, sh in items.HEAD.items():
    for s0 in sh:
        xs = [q[0] for q in s0.pts]; OURPC[s0.name] = (max(xs)+min(xs))/2.0
def cof(k, s):
    t = PIECE_C.get((k, s.name))
    return OURPC[t] if t else 0.0      # 우리 조각을 그대로 쓴 것·새로 세운 띠는 이미 제자리다
SETC = {k: [S(s.name, [(x+cof(k, s), y) for x, y in s.pts], loop=s.loop, filled=s.filled, tone=s.tone)
            for s in SET[k]] for k in SET}

# ── 검산 ────────────────────────────────────────────────────────────────────
fails = []
print("╔══ ⑧-3 HEAD 빚 5건 재검산 ══╗")
print("  %-9s %8s %8s %8s %7s %6s %6s  %s" % ("아이템","액자꼭대기","한계1.75","잉크밑단","감쌈","몽당변","1-C","판정"))
for k in SETC:
    p = [q for s in SETC[k] for q in s.pts]
    # ★ 액자는 **반경 도달이 아니라 꼭대기 y** 다 (§5-1 furhat +0.456 · §5-4 crown +0.184 가 그 값이다).
    #   반경으로 재면 우리 출하 모자가 먼저 죽는다: 야구모자 +0.200 · 중절모 +0.329 · 밀짚모자 +0.451.
    top = max(q[1] for q in p); bot = min(q[1] for q in p); reach = top
    wrap = any(abs(q[0]) >= 0.85 and q[1] <= 0.05 for q in p)
    stub = 0
    for s in SETC[k]:
        pt = s.pts; n = len(pt)
        cor = [rig.turn_deg(pt[(i-1)%n], pt[i], pt[(i+1)%n]) >= CORNER for i in range(n)]
        for i in range(n if s.loop else n-1):
            j = (i+1) % n
            if cor[i] and cor[j] and math.dist(pt[i], pt[j]) < W: stub += 1
    thin = sum(1 for s in SETC[k] if s.filled and s.loop and rho(s.pts) < GATE)
    bad = []
    if reach > FRAME: bad.append("액자 +%.4f" % (reach-FRAME))
    if not (1.0 < top < 2.551): bad.append("꼭대기 %.3f" % top)
    if not wrap and k != "crown": bad.append("감쌈 없음")
    if stub: bad.append("몽당변 %d" % stub)
    if thin: bad.append("1-C %d" % thin)
    print("  %-9s %8.4f %+8.3f %+8.3f %7s %6d %6d  %s" %
          (k, reach, top, bot, "있다" if wrap else ("면제" if k=="crown" else "★없다"),
           stub, thin, "OK" if not bad else "✗ " + " · ".join(bad)))
    if bad: fails.append("%s: %s" % (k, ", ".join(bad)))

# 1-A · 자기교차
n_ra = 0
for k in SETC:
    for s in SETC[k]:
        v = rig.rule_one(s, W)
        if v: fails.append("[1-A] %s %s: %s" % (k, s.name, v)); n_ra += 1
        si = rig.self_intersects(s.pts) if s.loop else None
        if si: fails.append("[교차] %s %s %s" % (k, s.name, si)); n_ra += 1
print("  [1-A]·[교차] 위반 %d건" % n_ra)

# 쌍별 실루엣 — 이식 4 + 우리 2
d = {"port:"+k: SETC[k] for k in SETC}
for nm in ("베레모", "밀짚모자"): d["our:"+nm] = items.HEAD[nm]
ks = list(d); pr = {x: rig.profile(d[x]) for x in ks}; w = (None, 99.0)
for i in range(len(ks)):
    for j in range(i+1, len(ks)):
        v = rig.max_delta(pr[ks[i]], pr[ks[j]])/W
        if v < w[1]: w = ((ks[i], ks[j]), v)
print("  [쌍] 최소 실루엣 차 %.2f획 (%s vs %s)  %s" %
      (w[1], w[0][0], w[0][1], "OK" if w[1] >= PAIR_MIN else "✗ 하한 %.1f 미달" % PAIR_MIN))
if w[1] < PAIR_MIN: fails.append("[쌍] %.2f획" % w[1])

print("\n╔══ ⑧-4 결론 ══╗")
if CONTROL:
    print("  ★ 대조 판정: 처방을 안 걸었으므로 **빚이 그대로 남아야** 한다. 실패 %d건: %s"
          % (len(fails), "OK (자가 살아 있다)" if fails else "✗ 자가 눈이 멀었다"))
    for f in fails: print("     · " + f)
    sys.exit(0 if fails else 1)
if fails:
    print("  ✗ 남은 위반 %d건 — **한 벌이 아직 안 선다**" % len(fails))
    for f in fails: print("    " + f)
    sys.exit(1)
print("★ HEAD 한 벌 통과 — 액자 · 감쌈 · 꼭대기 · 몽당변 · 1-A · 1-C · 자기교차 · 쌍별 실루엣 전부.")
print("  ⇒ 이식 1단계(HEAD)를 이 사양으로 `coder-systems` 에 넘길 수 있다.")
