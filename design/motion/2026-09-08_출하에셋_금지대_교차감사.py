#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
★ 이미 구워진 CostumeManifest_office.asset 의 금지대 교차 감사
design-motion / 2026-09-08

design-equipment 가 같은 라운드에 PR-4 「금지대」(두 조각은 닿거나(d=0) 중심거리 >= 2.00 W_P,
그 사이는 금지)를 세우고 신규 4프롭 59조각을 그 규칙 위에서 authored 했다.
**그런데 그 규칙이 나오기 전에 이미 구워져 출하 경로에 들어간 조각이 있다** — office 프롭 S0/S1.
이 스크립트는 그 에셋을 디스크에서 직접 읽어 같은 규칙으로 잰다(내가 손으로 옮겨 적지 않는다).
"""
import math, re, itertools

H = 2.2746944
PT_PER_UNIT = 846.0 / 24.0
STROKE_RATIO = 0.0339          # Interaction/CostumePropRenderer.cs:65
W_P = STROKE_RATIO             # 프롭 획 = 1 W_P
FORBIDDEN = 2.00 * W_P         # design-equipment PR-4
ASSET = 'Assets/_Project/Resources/Items/CostumeManifest_office.asset'
OK, NG = "OK", "★위반"

def pt(h, s=0.75): return h * H * s * PT_PER_UNIT

# ── 에셋에서 도형을 직접 디코드한다(값을 문서에서 베끼지 않는다) ────────────────
def decode(path):
    txt = open(path, encoding='utf-8').read()
    head, _, tail = txt.partition('\n  stageShapes:')
    out = {}
    for label, chunk in (('propShapes(=S1)', head), ('stageShapes', tail)):
        blocks = re.split(r'\n(?=\s*- name: )', chunk)
        cur = []
        for b in blocks:
            m = re.search(r'- name: (\S+)', b)
            if not m: continue
            loop = bool(int(re.search(r'loop: (\d)', b).group(1)))
            nums = [float(x) for x in re.findall(r'^\s*- (-?[\d.eE+]+)\s*$', b, re.M)]
            if not nums: continue
            i = 0; n = int(round(nums[i])); i += 1; pts = []
            for _ in range(n):
                coord = []
                for _ in range(2):
                    tc = int(round(nums[i])); i += 1; v = 0.0
                    for _ in range(tc):
                        basis = int(round(nums[i])); cc = int(round(nums[i+3])); i += 4
                        c = 1.0
                        for k in range(cc): c *= nums[i+k]
                        i += cc
                        assert basis == 8, f"기저가 Height(8)가 아니다: {basis}"
                        v += c
                    coord.append(v)
                pts.append(tuple(coord))
            cur.append((m.group(1), loop, pts))
        out[label] = cur
    return out

def segs(pts, loop):
    s = [(pts[i], pts[i+1]) for i in range(len(pts)-1)]
    if loop: s.append((pts[-1], pts[0]))
    return s

def pt_seg(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    dx, dy = bx-ax, by-ay; L2 = dx*dx + dy*dy
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((px-ax)*dx + (py-ay)*dy)/L2))
    return math.hypot(px-(ax+t*dx), py-(ay+t*dy))

def _cross(o, a, b):
    return (a[0]-o[0])*(b[1]-o[1]) - (a[1]-o[1])*(b[0]-o[0])

def _intersects(a, b, c, d):
    """두 선분이 실제로 교차하는가. ★ 이게 없으면 «가로지르는 쌍»의 거리가 0이 아니라
    끝점 거리로 나와, 닿아 있는 것을 «금지대 위반»으로 잘못 신고한다(2026-09-08 자기발견)."""
    d1, d2 = _cross(c, d, a), _cross(c, d, b)
    d3, d4 = _cross(a, b, c), _cross(a, b, d)
    return ((d1 > 0) != (d2 > 0)) and ((d3 > 0) != (d4 > 0))

def ss(a, b, c, d):
    if _intersects(a, b, c, d): return 0.0
    return min(pt_seg(a, c, d), pt_seg(b, c, d), pt_seg(c, a, b), pt_seg(d, a, b))

def shape_dist(s1, s2):
    return min(ss(*e1, *e2) for e1 in s1 for e2 in s2)

TOUCH = 1e-6   # 「닿는다」의 수치 정의

print("=" * 94)
print("[출하 에셋 금지대 교차 감사] CostumeManifest_office.asset")
print("=" * 94)
print(f"  프롭 획 W_P = {W_P:.4f} H = {pt(W_P):.2f} pt @0.75")
print(f"  PR-4 금지대 = (0, {FORBIDDEN:.4f} H) 열린 구간 — 닿거나(0), 아니면 {FORBIDDEN:.4f} H 이상")
print()

data = decode(ASSET)
s1 = data['propShapes(=S1)']
s0 = data['stageShapes']
print(f"  디코드: propShapes {len(s1)}조각 / stageShapes(S0) {len(s0)}조각   "
      f"{OK if (len(s1), len(s0)) == (10, 4) else NG + ' 조각 수가 예상과 다르다'}")

for label, shapes in (('S0 (stageShapes[0])', s0), ('S1 (propShapes 기본)', s1)):
    E = {n: segs(p, l) for n, l, p in shapes}
    names = [n for n, _, _ in shapes]
    print(f"\n  ── {label} — 전 쌍 {len(names)*(len(names)-1)//2}개 ──")
    bad = []
    for a, b in itertools.combinations(names, 2):
        d = shape_dist(E[a], E[b])
        if d <= TOUCH:  continue                 # 닿는다 → 허용
        if d >= FORBIDDEN: continue              # 충분히 떨어졌다 → 허용
        bad.append((d, a, b))
    for d, a, b in sorted(bad):
        print(f"    {NG}  {a} ↔ {b} = {d:.4f} H = {d/W_P:.2f} W_P = {pt(d):.2f} pt @0.75")
    if not bad:
        print(f"    {OK}  금지대 위반 0건")

# ★ 양성 대조 — 검사기가 실제로 무언가를 잡는가
print()
print("  ── 양성 대조(검사기 생존 확인) ──")
probe = [('X', False, [(0.10, 0.10), (0.40, 0.10)]),
         ('Y', False, [(0.10, 0.10 + FORBIDDEN * 0.5), (0.40, 0.10 + FORBIDDEN * 0.5)])]
d = shape_dist(segs(probe[0][2], False), segs(probe[1][2], False))
print(f"    (가) 고의로 {FORBIDDEN*0.5:.4f} H(=금지대 한복판) 떨어뜨린 쌍 -> 측정 {d:.4f} H  "
      f"{OK + ' 검사기 생존' if TOUCH < d < FORBIDDEN else NG + ' 검사기가 죽었다 — 위 0건은 무효'}")
cx = shape_dist(segs([(0.0, 0.0), (1.0, 0.0)], False), segs([(0.5, -0.5), (0.5, 0.5)], False))
print(f"    (나) 십자로 «가로지르는» 쌍 -> 측정 {cx:.4f} H  "
      f"{OK + ' 교차 감지 살아 있음' if cx <= TOUCH else NG + ' 교차를 못 본다 — 닿은 쌍을 위반으로 오신고한다'}")
print()
print("=" * 94)
