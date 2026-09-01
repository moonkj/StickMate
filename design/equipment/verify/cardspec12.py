# -*- coding: utf-8 -*-
"""FX/PET 12장 **카드 아이콘 제안**(design-equipment, 2026-09-01 밤).

이 12장만이 `Resources/Items/*.asset`의 폴백을 **실제로 그린다**
(AccessoryCardIcon.TryBuild가 FX/PET에서 false를 돌려주므로 — cards42.py 참고).

원칙: 카드 도형은 **R 공간에서 정의하고**, 30종 카드가 쓰는 것과 **같은 투영**
(AccessoryCardIcon: 잉크 사각형을 0.86·40 = 34.4에 담고 가운데 정렬)으로 viewBox에 옮긴다.
그래서 '손으로 놓은 40×40'이 아니라 **몸 도형에서 유도된 값**이 된다.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, appearance
from rig import Shape
import cards42 as C

WC   = C.STROKE_V          # 1.7 (viewBox 단위 = 카드 획 1.87 캔버스 유닛)
LO, HI = WC/2, C.VIEW-WC/2 # 상자 안쪽 한계(획 반폭)

A_FX, A_PET = appearance.FX_A, appearance.PET_A

def cp(shape, k=1.0, dx=0.0, dy=0.0, tone=None, name=None, kind="poly"):
    s = Shape(name or shape.name, [(x*k+dx, y*k+dy) for x, y in shape.pts],
              loop=shape.loop, filled=False, tone=shape.tone if tone is None else tone)
    s.kind = kind
    return s

def L(name, pts, tone=0, loop=False, kind="poly"):
    s = Shape(name, pts, loop=loop, filled=False, tone=tone); s.kind = kind; return s

def circle(name, cx, cy, r, tone=0, kind="ring"):
    s = Shape(name, [(cx-r, cy-r), (cx+r, cy+r)], loop=False, filled=False, tone=tone)
    s.kind = kind; s.circle = (cx, cy, r); return s

def star(name, cx, cy, r, tone=0, inner=0.34, phase=90.0):
    pts = []
    for i in range(8):
        a = math.radians(phase + 45.0*i)
        rr = r if i % 2 == 0 else r*inner
        pts.append((cx + math.cos(a)*rr, cy + math.sin(a)*rr))
    s = Shape(name, pts, loop=True, filled=False, tone=tone); s.kind = "poly"; return s

# ── 12장 제안 (R 공간, y 위로) ───────────────────────────────────────────────
SOLE = A_FX["발자국"][0]
CARDS = {}

# FX 0 없음 — 카드 전용(월드에 도형이 없다). 가운데 점을 보조색으로: 이 카드의 유일한 식별 특징.
VIEWBOX = {}   # 현행 40 viewBox 값을 그대로 두는 카드(투영하지 않는다)
VIEWBOX["FX 없음"] = [circle("Ring", 20, 20, 9.0, tone=0, kind="dashed"),
                      circle("Dot",  20, 20, 2.0, tone=1, kind="dot")]

# FX 1 발자국 — 39-P: 카드는 한 알이 아니라 **한 무리**. 알은 제안 A의 밑창 그대로.
CARDS["FX 발자국"] = [cp(SOLE, 0.62, -1.05, -0.60, tone=0, name="SoleFar"),
                      cp(SOLE, 0.80,  0.05,  0.00, tone=0, name="SoleMid"),
                      cp(SOLE, 1.00,  1.30,  0.66, tone=1, name="SoleNew")]

# FX 2 반짝임 — 제안 B가 확정한 알(4각 별)을 **카드에서 먼저** 쓴다. 카드 렌더러도 채움이 없으므로
#   닫힌 윤곽으로 그리지만, 꼭짓점이 별의 갈래를 만든다(현행 8조각 십자+눈금을 2조각으로 줄인다).
CARDS["FX 반짝임"] = [star("SparkBig",   -0.18, -0.10, 1.00, tone=0),
                      star("SparkSmall",  0.86,  0.72, 0.46, tone=1)]

# FX 3 먼지 — 알 2개(제안 A 그대로) + 바닥선. 바닥선이 보조색 정원 1개를 가져간다.
CLOUD = [(10,26),(7.96,22.78),(8.79,19.06),(12,17),(14.32,13.58),(18.16,12.06),
         (22.19,12.98),(25,16),(29.12,16.05),(32,19),(31.95,23.12),(29,26)]
VIEWBOX["FX 먼지"] = [L("Cloud", CLOUD, tone=0, loop=True),
                      L("Ground", [(8.0, 30.0), (32.0, 30.0)], tone=1)]

# FX 4 물방울 — **현행 유지**(이미 39-P 무리 + 정원 3 + 보조색 1을 지킨다). 알 반경비 1 : 0.75 : 0.55.
VIEWBOX["FX 물방울"] = [circle("Drop1", 14, 22, 4.0, tone=0),
                        circle("Drop2", 24, 17, 3.0, tone=0),
                        circle("Drop3", 29, 26, 2.2, tone=1)]

# FX 5 나뭇잎 — 제안 A와 **같은 도형**(잎몸 6점 + 잎자루). 현행 카드의 4점 사각형을 버린다.
CARDS["FX 나뭇잎"] = [cp(A_FX["나뭇잎"][0], 1.0, 0, 0, tone=0),
                      cp(A_FX["나뭇잎"][1], 1.0, 0, 0, tone=1)]

# PET 0 작은공 — 제안 A와 같은 구성(테 + 솔기). 현행의 보조색 2개(하이라이트+그림자)를 닫는다.
CARDS["PET 작은공"] = [circle("Ball", 0, 0, appearance.BALL_R, tone=0),
                       cp(A_PET["작은공"][1], 1.0, 0, 0, tone=1, name="Seam")]

# PET 1 종이비행기 — 제안 A(접힘선 2점).
CARDS["PET 종이비행기"] = [cp(A_PET["종이비행기"][0], 1.0, 0, 0, tone=0),
                           cp(A_PET["종이비행기"][1], 1.0, 0, 0, tone=1)]

# PET 2 리틀스틱메이트 — ★ 조형은 design-character 소관. 여기서는 **카드 정원/보조색만** 닫는다:
#   6조각(머리+몸통+팔2+다리2) → 4조각(머리 + 몸통 + 팔 한 줄 + 다리 한 줄). 머리가 보조색.
CARDS["PET 리틀스틱메이트"] = [
    circle("Head", 0.00, 1.30, 0.52, tone=1),
    L("Torso", [(0.00, 0.78), (0.00, -0.62)], tone=0),
    L("Arms",  [(-0.72, -0.28), (0.00, 0.34), (0.72, -0.28)], tone=0),
    L("Legs",  [(-0.56, -1.62), (0.00, -0.62), (0.56, -1.62)], tone=0)]

# PET 3 커서친구 — 제안 A(머리 + 꼬리).
CARDS["PET 커서친구"] = [cp(A_PET["커서친구"][0], 1.0, 0, 0, tone=0),
                         cp(A_PET["커서친구"][1], 1.0, 0, 0, tone=1)]

# PET 4 풍선 — 현행 유지(=제안 A와 같은 구성).
VIEWBOX["PET 풍선"] = [circle("Body", 20, 15, 7.0, tone=0),
                       L("String", [(20,22),(21,29),(19,34)], tone=1)]

# PET 5 달팽이 — 제안 A의 비율로 맞춘다(껍데기 0.78 / 속점 0.26 / 중심 −0.30,+0.76).
VIEWBOX["PET 달팽이"] = [circle("Shell", 18, 20, 7.0, tone=0),
                         L("Foot", [(11,27),(30,27),(33,22)], tone=0),
                         circle("Core", 18, 20, 2.4, tone=1)]

# ── 30종 카드와 **같은 투영**으로 viewBox에 담는다 ────────────────────────────
def project(shapes):
    pts = []
    for s in shapes:
        if hasattr(s, "circle"):
            cx, cy, r = s.circle
            pts += [(cx-r, cy-r), (cx+r, cy+r)]
        else:
            pts += s.pts
    x0, y0, x1, y1 = rig.bounds(pts)
    k = C.FITBOX / max(x1-x0, y1-y0)
    cx0, cy0 = (x0+x1)/2.0, (y0+y1)/2.0
    out = []
    for s in shapes:
        t = Shape(s.name, [((x-cx0)*k + C.VIEW/2.0, C.VIEW/2.0 - (y-cy0)*k) for x, y in s.pts],
                  loop=s.loop, filled=False, tone=s.tone)
        t.kind = s.kind
        if hasattr(s, "circle"):
            cx, cy, r = s.circle
            t.circle = ((cx-cx0)*k + C.VIEW/2.0, C.VIEW/2.0 - (cy-cy0)*k, r*k)
        out.append(t)
    return out

def true_min_edge(s):
    n = len(s.pts); best = None
    for i in range(n if s.loop else n-1):
        Lg = math.dist(s.pts[i], s.pts[(i+1) % n])
        if Lg < 1e-9: continue
        best = Lg if best is None else min(best, Lg)
    return best

KINDNUM = {"poly":0, "ring":1, "dashed":2, "dot":3}

print("╔══ FX/PET 카드 12장 제안 · 검산 (viewBox 40 · 카드 획 %.1f = 1.87 캔버스 유닛) ══╗" % WC)
bad = 0
OUT = {}
ALL = [(n, project(v)) for n, v in CARDS.items()] + [(n, v) for n, v in VIEWBOX.items()]
ORDER = ["FX 없음","FX 발자국","FX 반짝임","FX 먼지","FX 물방울","FX 나뭇잎",
         "PET 작은공","PET 종이비행기","PET 리틀스틱메이트","PET 커서친구","PET 풍선","PET 달팽이"]
ALL.sort(key=lambda t: ORDER.index(t[0]))
for name, P in ALL:
    OUT[name] = P
    msgs = []
    if not (2 <= len(P) <= 4): msgs.append("정원 %d개" % len(P))
    acc = sum(1 for s in P if s.tone == 1)
    if acc != 1: msgs.append("보조색 %d개" % acc)
    info = []
    for s in P:
        if hasattr(s, "circle"):
            cx, cy, r = s.circle
            info.append("%s ⌀%.2f획" % (s.name, 2*r/WC))
            if 2*r < 1.5*WC: msgs.append("%s 지름 %.2f획 < 1.5" % (s.name, 2*r/WC))
            if cx-r < LO or cy-r < LO or cx+r > HI or cy+r > HI: msgs.append("%s 상자 밖" % s.name)
            continue
        tm = true_min_edge(s)
        info.append("%s %.2f획" % (s.name, tm/WC))
        if tm < WC: msgs.append("%s 최단 실제 변 %.2f획 < 1.0" % (s.name, tm/WC))
        x0, y0, x1, y1 = rig.bounds(s.pts)
        if max(x1-x0, y1-y0) < 1.5*WC: msgs.append("%s 잉크 사각형 %.2f획 < 1.5" % (s.name, max(x1-x0, y1-y0)/WC))
        if s.loop and rig.self_intersects(s.pts): msgs.append("%s 자기교차" % s.name)
        if x0 < LO or y0 < LO or x1 > HI or y1 > HI: msgs.append("%s 상자 밖" % s.name)
    print("  %s %-16s 조각%d 보조색%d | %s" % ("✗" if msgs else "✓", name, len(P), acc, " · ".join(info)))
    for m in msgs: print("      - " + m); bad += 1

# 쌍별 실루엣 차 — FX 6 / PET 6 각각
def prof(P):
    ss = []
    for s in P:
        if hasattr(s, "circle"):
            cx, cy, r = s.circle
            ss.append(Shape(s.name, [(cx+math.cos(2*math.pi*i/32)*r - C.VIEW/2,
                                      cy+math.sin(2*math.pi*i/32)*r - C.VIEW/2) for i in range(32)], loop=True))
        else:
            ss.append(Shape(s.name, [(x-C.VIEW/2, y-C.VIEW/2) for x, y in s.pts], loop=s.loop))
    return rig.profile(ss)
for grp in ("FX", "PET"):
    ks = [k for k in OUT if k.startswith(grp)]
    pr = {k: prof(OUT[k]) for k in ks}
    worst = (None, 1e9)
    for i in range(len(ks)):
        for j in range(i+1, len(ks)):
            v = rig.max_delta(pr[ks[i]], pr[ks[j]]) / WC
            if v < worst[1]: worst = ((ks[i], ks[j]), v)
    print("  %s 쌍별 최소 실루엣 차 %.2f획 (%s vs %s)" % (grp, worst[1], worst[0][0], worst[0][1]))
    if worst[1] < 1.0: bad += 1; print("      - 하한 1.0획 미달")
print("╚══ 위반 %d건 ══╝" % bad)

if "--dump" in sys.argv:
    print()
    print("── 에셋에 눕힐 값 (icon: kind / values / tone) ──")
    for name, P in OUT.items():
        print("  %s" % name)
        for s in P:
            if hasattr(s, "circle"):
                cx, cy, r = s.circle
                print("    kind %d(%s)  values %s  tone %d"
                      % (KINDNUM[s.kind], s.kind, "[%.2f, %.2f, %.2f]" % (cx, cy, r), s.tone))
            else:
                v = []
                for x, y in s.pts: v += [x, y]
                if s.loop: v += [s.pts[0][0], s.pts[0][1]]
                print("    kind 0(poly)  values [%s]  tone %d"
                      % (", ".join("%.2f" % t for t in v), s.tone))

# ── 조각 사이 간격 — 규칙 4의 "0(닿음) 또는 >= 1.5획" ─────────────────────────
def as_poly(s, n=32):
    if hasattr(s, "circle"):
        cx, cy, r = s.circle
        return [(cx+math.cos(2*math.pi*i/n)*r, cy+math.sin(2*math.pi*i/n)*r) for i in range(n)], True
    return s.pts, s.loop

def seg_dist(p, a, b):
    dx, dy = b[0]-a[0], b[1]-a[1]; L2 = dx*dx+dy*dy
    t = 0.0 if L2 < 1e-12 else max(0.0, min(1.0, ((p[0]-a[0])*dx + (p[1]-a[1])*dy)/L2))
    return math.hypot(p[0]-(a[0]+dx*t), p[1]-(a[1]+dy*t))

def shape_gap(s1, s2, samples=24):
    p1, l1 = as_poly(s1); p2, l2 = as_poly(s2)
    best = 1e9
    for pts, loop, other in ((p1, l1, p2), (p2, l2, p1)):
        n = len(pts)
        for i in range(n if loop else n-1):
            a, b = pts[i], pts[(i+1) % n]
            for k in range(samples+1):
                q = (a[0]+(b[0]-a[0])*k/samples, a[1]+(b[1]-a[1])*k/samples)
                m = len(other)
                for j in range(m):
                    best = min(best, seg_dist(q, other[j], other[(j+1) % m]))
    return best

print()
print("── 조각 사이 간격 (규칙 4: 0에 닿거나 >= 1.5획. 0 < 간격 < 1.5획이 최악 구간) ──")
INTENDED_TOUCH = {("FX 먼지", "Crescent0", "Crescent1"),      # 4-2-1: 한 덩어리의 혹 — 일부러 겹친다
                  ("PET 작은공", "Ball", "Seam"),              # 솔기 양 끝이 테 위에 얹힌다(간격 0)
                  ("PET 종이비행기", "Body", "Fold"),           # 용골선이 몸의 두 점을 잇는다
                  ("PET 커서친구", "Head", "Tail"),             # 변 하나를 공유한다
                  ("PET 풍선", "Body", "String"),               # 끈이 몸에 매달린다
                  ("PET 달팽이", "Shell", "Core"),              # 동심원
                  ("PET 달팽이", "Shell", "Foot"),              # 껍데기가 발에 닿아 있다(닿음 계약)
                  ("PET 리틀스틱메이트", "Torso", "Arms"),
                  ("PET 리틀스틱메이트", "Torso", "Legs"),
                  ("PET 리틀스틱메이트", "Head", "Torso"),
                  ("FX 나뭇잎", "Blade", "Stem")}
gbad = 0
for name, P in OUT.items():
    line = []
    for i in range(len(P)):
        for j in range(i+1, len(P)):
            g = shape_gap(P[i], P[j]) / WC
            key = (name, P[i].name, P[j].name)
            ok = (g < 0.06) or (g >= 1.5) or (key in INTENDED_TOUCH)
            line.append("%s-%s %.2f획%s" % (P[i].name, P[j].name, g, "" if ok else " ✗"))
            if not ok: gbad += 1
    print("  %-16s %s" % (name, " · ".join(line)))
print("  → 최악 구간 위반 %d건" % gbad)
