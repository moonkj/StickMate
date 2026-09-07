#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
코스튬 프롭 — 진화 4상태(S0/S1/S2/S3) 단계 조형 좌표 + 분리 감사
design-motion / 2026-09-08

좌표 단위는 전부 H 배수(신장). AccessoryWornBasis.Height(=8)가 실재하므로 그대로 들어간다.
원점 = 캐릭터 루트 x(propAnchorOffsetXInH = 0), y = 접지선.

이 스크립트가 잠그는 것
  (1) 최소 분리 규칙 — 프롭 획 두께에서 유도한다. 「대마법사 24선 뭉개짐」의 실제 원인이 여기 있었다.
  (2) 단조성 — S(n)이 S(n-1)의 도형 이름을 전부 포함하는가.
  (3) 도달 원 — 작업점이 손이 닿는 곳인가(프롭지향 키포즈가 있는 코스튬만).
  (4) 발판 요구 폭 — 단계가 올라 프롭이 넓어지면 폴백이 더 자주 걸린다. 그 값을 찍는다.
"""
import math, itertools

H = 2.2746944                     # StickConfig.BaselineCharacterTotalHeight
PT_PER_UNIT = 846.0 / 24.0        # StickConfig.ReferencePointsPerWorldUnitApprox = 35.25
BAKED_SCALE, MIN_SCALE = 0.75, 0.35
STROKE_RATIO = 0.0339             # Interaction/CostumePropRenderer.cs:65 StrokeWidthRatio
SHOULDER_Y, REACH = 0.7758, 0.3297   # 선행 검산 [0] (H 배수)
OK, NG = "OK", "★위반"

def pt(h, scale=BAKED_SCALE):     # H 배수 -> pt
    return h * H * scale * PT_PER_UNIT

# ── 1. 최소 분리 규칙 유도 ──────────────────────────────────────────────────────
# 나란한 두 획이 갈라져 보이려면 «획 사이에 배경이 최소 1 디바이스 pt» 남아야 한다.
#   중심간 거리(pt) >= 획(pt) + 1
#   gap_H * H * s * PT >= STROKE_RATIO * H * s * PT + 1
#   gap_H >= STROKE_RATIO + 1 / (H * s * PT)
def min_sep(scale):
    return STROKE_RATIO + 1.0 / (H * scale * PT_PER_UNIT)

SEP_SHIP = min_sep(BAKED_SCALE)      # 출하 배율 0.75
SEP_MIN  = min_sep(MIN_SCALE)        # 슬라이더 하한 0.35
print("=" * 92)
print("[1] 최소 분리 규칙 — 「선이 많아 뭉개진다」의 정량 정의")
print("=" * 92)
print(f"  프롭 획 = 신장 x {STROKE_RATIO} = {pt(STROKE_RATIO):.2f} pt @배율0.75 / {pt(STROKE_RATIO,1.0):.2f} pt @1.0")
print(f"  ⇒ 중심간 최소 거리 = 획 + 배경 1pt")
for s in (0.35, 0.50, 0.60, 0.75, 1.00, 2.00):
    print(f"     배율 {s:.2f} -> {min_sep(s):.4f} H  ({pt(min_sep(s), s):.2f} pt)")
print(f"  ★ 채택 설계 하한 = {SEP_SHIP:.4f} H (출하 배율 0.75 기준, 반올림 상수 0.052 H)")
print(f"    배율 0.35에서는 {SEP_MIN:.4f} H가 필요하다 — 즉 0.052 H 간격은 배율 "
      f"{STROKE_RATIO and (1.0/((0.052-STROKE_RATIO)*H*PT_PER_UNIT)):.3f} 이상에서만 갈라져 보인다.")
print("    그 아래 배율에서는 «세부가 뭉쳐 한 덩어리로 읽힌다» — 실루엣은 살아 있으므로 결함이 아니다.")
SEP = 0.052

# ── 2. 도형 정의 ────────────────────────────────────────────────────────────────
def rect(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]

def ellipse(cx, cy, rx, ry, n=24, a0=0.0, a1=360.0):
    return [(cx + rx*math.cos(math.radians(a)), cy + ry*math.sin(math.radians(a)))
            for a in [a0 + (a1-a0)*i/(n-1) for i in range(n)]]

C = {}

# ── C1 사이버펑크 «홀로 콘솔 스탠드» ────────────────────────────────────────────
C['C1 사이버 홀로콘솔'] = dict(
    shapes={
        'Column':     (False, [(0.54, 0.00), (0.54, 0.62)]),
        'Base':       (False, [(0.44, 0.00), (0.64, 0.00)]),
        'PanelFrame': (True,  rect(0.31, 0.62, 0.77, 0.90)),
        'Scan1':      (False, [(0.365, 0.690), (0.710, 0.690)]),
        'Scan2':      (False, [(0.365, 0.760), (0.650, 0.760)]),
        'Scan3':      (False, [(0.365, 0.830), (0.680, 0.830)]),
        'AuxFrame':   (True,  rect(0.59, 0.955, 0.77, 1.075)),
        'AuxScan':    (False, [(0.645, 1.015), (0.715, 1.015)]),
        'Cable1':     (False, [(0.46, 0.62), (0.40, 0.40), (0.37, 0.18), (0.36, 0.00)]),
        'Cable2':     (False, [(0.62, 0.62), (0.68, 0.40), (0.71, 0.18), (0.72, 0.00)]),
        'DataUnit':   (True,  rect(0.78, 0.00, 0.94, 0.13)),
    },
    stages={0: ['Column', 'Base', 'PanelFrame'],
            1: ['Column', 'Base', 'PanelFrame', 'Scan1', 'Scan2', 'Scan3'],
            2: ['Column', 'Base', 'PanelFrame', 'Scan1', 'Scan2', 'Scan3', 'AuxFrame', 'AuxScan'],
            3: ['Column', 'Base', 'PanelFrame', 'Scan1', 'Scan2', 'Scan3', 'AuxFrame', 'AuxScan',
                'Cable1', 'Cable2', 'DataUnit']},
    joins={('Column', 'Base'), ('Column', 'PanelFrame'),
           ('Cable1', 'PanelFrame'), ('Cable2', 'PanelFrame'),
           ('Cable1', 'Column'), ('Cable2', 'Column')},
    work=[(0.31, 0.82), (0.31, 0.72)])

# ── C2 오피스 «스탠딩 책상 + 칸막이» (S0/S1은 이미 구워진 에셋과 비트 단위 동일) ──
C['C2 오피스 스탠딩책상'] = dict(
    shapes={
        'Partition':      (True,  rect(0.72, 0.10, 1.00, 0.88)),
        'DeskTop':        (False, [(0.26, 0.62), (1.00, 0.62)]),
        'DeskLeg':        (False, [(0.32, 0.00), (0.32, 0.62)]),
        'DeskLip':        (False, [(0.26, 0.60), (0.34, 0.60)]),
        'LampPost':       (False, [(0.92, 0.88), (0.92, 1.02)]),
        'LampShade':      (False, [(0.92, 1.02), (0.78, 0.97)]),
        'LampRay1':       (False, [(0.80, 0.98), (0.54, 0.64)]),
        'LampRay2':       (False, [(0.80, 0.98), (0.86, 0.64)]),
        'PartitionSlot1': (False, [(0.80, 0.20), (0.80, 0.80)]),
        'PartitionSlot2': (False, [(0.90, 0.20), (0.90, 0.80)]),
        'Paper1':         (False, [(0.340, 0.672), (0.510, 0.672)]),
        'Paper2':         (False, [(0.352, 0.724), (0.498, 0.724)]),
        'Paper3':         (False, [(0.336, 0.776), (0.516, 0.776)]),
        'MugBody':        (True,  rect(0.66, 0.62, 0.74, 0.72)),
        'MugHandle':      (False, [(0.740, 0.655), (0.782, 0.665), (0.782, 0.695), (0.740, 0.705)]),
        'Memo':           (True,  rect(0.745, 0.88, 0.845, 0.955)),
        'Shelf':          (False, [(0.32, 0.30), (0.98, 0.30)]),
        'Book1':          (False, [(0.44, 0.30), (0.44, 0.44)]),
        'Book2':          (False, [(0.50, 0.30), (0.50, 0.47)]),
        'Book3':          (False, [(0.56, 0.30), (0.56, 0.42)]),
        'Bin':            (True,  [(0.38, 0.00), (0.50, 0.00), (0.48, 0.17), (0.40, 0.17)]),
    },
    stages={0: ['Partition', 'DeskTop', 'DeskLeg', 'DeskLip'],
            1: ['Partition', 'DeskTop', 'DeskLeg', 'DeskLip', 'LampPost', 'LampShade',
                'LampRay1', 'LampRay2', 'PartitionSlot1', 'PartitionSlot2'],
            2: ['Partition', 'DeskTop', 'DeskLeg', 'DeskLip', 'LampPost', 'LampShade',
                'LampRay1', 'LampRay2', 'PartitionSlot1', 'PartitionSlot2',
                'Paper1', 'Paper2', 'Paper3', 'MugBody', 'MugHandle'],
            3: ['Partition', 'DeskTop', 'DeskLeg', 'DeskLip', 'LampPost', 'LampShade',
                'LampRay1', 'LampRay2', 'PartitionSlot1', 'PartitionSlot2',
                'Paper1', 'Paper2', 'Paper3', 'MugBody', 'MugHandle',
                'Memo', 'Shelf', 'Book1', 'Book2', 'Book3', 'Bin']},
    joins={('Partition', 'DeskTop'), ('DeskTop', 'DeskLip'), ('DeskTop', 'DeskLeg'),
           ('Partition', 'LampPost'), ('LampPost', 'LampShade'),
           ('LampShade', 'LampRay1'), ('LampShade', 'LampRay2'), ('LampRay1', 'LampRay2'),
           ('LampRay1', 'DeskTop'), ('LampRay2', 'DeskTop'),
           ('MugBody', 'MugHandle'), ('MugBody', 'DeskTop'), ('LampRay2', 'MugBody'),
           ('Partition', 'Memo'), ('LampRay1', 'Paper1'),
           ('Shelf', 'DeskLeg'), ('Shelf', 'Partition'),
           ('Shelf', 'Book1'), ('Shelf', 'Book2'), ('Shelf', 'Book3'),
           ('Partition', 'PartitionSlot1'), ('Partition', 'PartitionSlot2'),
           ('DeskTop', 'Paper1'), ('DeskTop', 'MugBody'),
           ('DeskLeg', 'DeskLip'), ('Partition', 'LampRay1')},
    work=[(0.28, 0.62)])

# ── C3 광부 «광맥 벽» ──────────────────────────────────────────────────────────
C['C3 광부 광맥벽'] = dict(
    shapes={
        'RockFace':   (True,  [(0.30, 0.00), (0.28, 0.18), (0.31, 0.38), (0.28, 0.60), (0.28, 0.88),
                               (0.33, 0.94), (0.56, 0.93), (0.72, 0.90), (0.78, 0.70), (0.76, 0.44),
                               (0.79, 0.20), (0.75, 0.00)]),
        'GroundSeam': (False, [(0.26, 0.00), (0.80, 0.00)]),
        'Vein1':      (False, [(0.36, 0.26), (0.47, 0.38)]),
        'Vein2':      (False, [(0.46, 0.58), (0.58, 0.69)]),
        'Vein3':      (False, [(0.50, 0.14), (0.60, 0.24)]),
        'Vein4':      (False, [(0.44, 0.76), (0.55, 0.84)]),
        'Chip1':      (True,  [(0.09, 0.00), (0.14, 0.00), (0.115, 0.045)]),
        'Chip2':      (True,  [(0.195, 0.00), (0.245, 0.00), (0.22, 0.04)]),
        'SupportPost':(False, [(0.71, 0.00), (0.71, 0.74)]),
        'Notch':      (False, [(0.28, 0.50), (0.39, 0.545), (0.405, 0.645), (0.28, 0.685)]),
        'Scrape':     (False, [(0.30, 0.42), (0.385, 0.445)]),
        'Vein5':      (False, [(0.575, 0.335), (0.650, 0.405)]),
        'Chip3':      (True,  [(0.86, 0.00), (0.91, 0.00), (0.885, 0.045)]),
        'CaveMouth':  (False, [(0.28, 0.735), (0.42, 0.775), (0.46, 0.855), (0.40, 0.905), (0.28, 0.905)]),
        'Timber1':    (False, [(0.30, 0.935), (0.60, 0.925)]),
        'Timber2':    (False, [(0.335, 0.86), (0.335, 0.99)]),
        'Chip4':      (True,  [(0.965, 0.00), (1.015, 0.00), (0.99, 0.045)]),
        'Vein6':      (False, [(0.545, 0.505), (0.625, 0.570)]),
        'Vein7':      (False, [(0.56, 0.40), (0.65, 0.30)]),
    },
    stages={0: ['RockFace', 'GroundSeam', 'Vein1'],
            1: ['RockFace', 'GroundSeam', 'Vein1', 'Vein2', 'Vein3', 'Vein4',
                'Chip1', 'Chip2', 'SupportPost'],
            2: ['RockFace', 'GroundSeam', 'Vein1', 'Vein2', 'Vein3', 'Vein4',
                'Chip1', 'Chip2', 'SupportPost', 'Notch', 'Scrape', 'Vein5', 'Chip3'],
            3: ['RockFace', 'GroundSeam', 'Vein1', 'Vein2', 'Vein3', 'Vein4',
                'Chip1', 'Chip2', 'SupportPost', 'Notch', 'Scrape', 'Vein5', 'Chip3',
                'CaveMouth', 'Timber1', 'Timber2', 'Chip4', 'Vein6', 'Vein7']},
    joins={('RockFace', 'GroundSeam'), ('RockFace', 'Notch'), ('RockFace', 'CaveMouth'),
           ('RockFace', 'SupportPost'), ('GroundSeam', 'SupportPost'),
           ('RockFace', 'Timber1'), ('RockFace', 'Timber2'), ('Timber1', 'Timber2'),
           ('CaveMouth', 'Timber1'), ('CaveMouth', 'Timber2'), ('CaveMouth', 'Vein4'),
           ('GroundSeam', 'Chip2'), ('RockFace', 'Scrape'), ('Notch', 'Scrape')},
    work=[(0.28, 0.85), (0.28, 0.65)])

# ── C4 대마법사 «마법진 + 지팡이 거치대» ────────────────────────────────────────
# ★ 초판의 «동심 타원 3겹»을 폐기했다 — 세로 반경 간격이 0.017~0.033 H로 획(0.0339 H)보다 작아
#   위/아래에서 세 곡선이 한 덩어리가 된다. 그것이 「S3 24선 뭉개짐」의 실제 원인이었다.
# ★ 초판의 «동심 타원 3겹»을 폐기했다 — 세로 반경 간격이 0.017~0.033 H로 획(0.0339 H)보다 작아
#   위/아래에서 세 곡선이 한 덩어리가 된다. 그것이 「S3 24선 뭉개짐」의 실제 원인이었다.
#   처방 3가지: (a) 안쪽 링을 «좌우 측면 호»로만 둔다(수렴 지점을 아예 안 만든다)
#              (b) 룬은 원 «바깥»이 아니라 «안쪽»으로 뻗는다(지팡이 거치대와의 충돌 제거)
#              (c) 룬 각도는 호가 존재하지 않는 띠(30°~150°, 210°~330°)에만 놓는다
MC, MCY, MRX, MRY = 0.60, 0.02, 0.34, 0.085
def rune(theta_deg, length=0.045):
    t = math.radians(theta_deg)
    px, py = MC + MRX*math.cos(t), MCY + MRY*math.sin(t)
    d = math.hypot(px-MC, py-MCY)
    k = 1.0 - length/d
    return [(round(px, 4), round(py, 4)),
            (round(MC + k*(px-MC), 4), round(MCY + k*(py-MCY), 4))]

C['C4 대마법사 마법진'] = dict(
    shapes={
        'CircleOuter': (True,  ellipse(MC, MCY, MRX, MRY)),
        'Staff':       (False, [(0.90, 0.02), (1.00, 0.86)]),
        'Orb':         (True,  ellipse(1.00, 0.86, 0.055, 0.055)),
        'StandLegA':   (False, [(0.86, 0.00), (0.94, 0.16)]),
        'StandLegB':   (False, [(1.02, 0.00), (0.94, 0.16)]),
        'StandBar':    (False, [(0.88, 0.16), (1.00, 0.16)]),
        'ArcInnerR':   (False, ellipse(MC, MCY, 0.205, 0.051, n=13, a0=-30, a1=30)),
        'ArcInnerL':   (False, ellipse(MC, MCY, 0.205, 0.051, n=13, a0=150, a1=210)),
        'RuneN':       (False, rune(90)),
        'RuneS':       (False, rune(270)),
        'RuneE':       (False, rune(0)),
        'RuneW':       (False, rune(180)),
        'ArcMidR':     (False, ellipse(MC, MCY, 0.272, 0.068, n=13, a0=-30, a1=30)),
        'ArcMidL':     (False, ellipse(MC, MCY, 0.272, 0.068, n=13, a0=150, a1=210)),
        'Rune060':     (False, rune(60)),
        'Rune120':     (False, rune(120)),
        'Rune240':     (False, rune(240)),
        'Rune300':     (False, rune(300)),
        'TriA':        (False, [(0.600, 0.205), (0.196, -0.083)]),
        'TriB':        (False, [(0.196, -0.083), (1.004, -0.083)]),
        'TriC':        (False, [(1.004, -0.083), (0.600, 0.205)]),
        'Rune075':     (False, rune(75)),
        'Rune105':     (False, rune(105)),
        'Rune255':     (False, rune(255)),
        'Rune285':     (False, rune(285)),
        'OrbRing':     (True,  ellipse(1.00, 0.86, 0.112, 0.112)),
    },
    stages={0: ['CircleOuter', 'Staff', 'Orb', 'StandLegA', 'StandLegB'],
            1: ['CircleOuter', 'Staff', 'Orb', 'StandLegA', 'StandLegB', 'StandBar',
                'ArcInnerR', 'ArcInnerL', 'RuneN', 'RuneS', 'RuneE', 'RuneW'],
            2: ['CircleOuter', 'Staff', 'Orb', 'StandLegA', 'StandLegB', 'StandBar',
                'ArcInnerR', 'ArcInnerL', 'RuneN', 'RuneS', 'RuneE', 'RuneW',
                'ArcMidR', 'ArcMidL', 'Rune060', 'Rune120', 'Rune240', 'Rune300'],
            3: ['CircleOuter', 'Staff', 'Orb', 'StandLegA', 'StandLegB', 'StandBar',
                'ArcInnerR', 'ArcInnerL', 'RuneN', 'RuneS', 'RuneE', 'RuneW',
                'ArcMidR', 'ArcMidL', 'Rune060', 'Rune120', 'Rune240', 'Rune300',
                'TriA', 'TriB', 'TriC', 'Rune075', 'Rune105', 'Rune255', 'Rune285', 'OrbRing']},
    joins={('Staff', 'Orb'), ('Staff', 'StandLegA'), ('Staff', 'StandLegB'), ('Staff', 'StandBar'),
           ('StandLegA', 'StandLegB'), ('StandLegA', 'StandBar'), ('StandLegB', 'StandBar'),
           ('Orb', 'OrbRing'), ('TriA', 'TriB'), ('TriB', 'TriC'), ('TriA', 'TriC'),
           ('CircleOuter', 'RuneN'), ('CircleOuter', 'RuneS'),
           ('CircleOuter', 'RuneE'), ('CircleOuter', 'RuneW'),
           ('CircleOuter', 'Rune060'), ('CircleOuter', 'Rune120'),
           ('CircleOuter', 'Rune240'), ('CircleOuter', 'Rune300'),
           ('CircleOuter', 'Rune075'), ('CircleOuter', 'Rune105'),
           ('CircleOuter', 'Rune255'), ('CircleOuter', 'Rune285'),
           ('CircleOuter', 'TriA'), ('CircleOuter', 'TriB'), ('CircleOuter', 'TriC'),
           ('TriB', 'StandLegA'), ('TriB', 'StandLegB'), ('TriC', 'StandLegA'), ('TriC', 'Staff'),
           ('TriC', 'StandBar'), ('TriB', 'CircleOuter')},
    work=[])

# ── 3. 기하 도구 ────────────────────────────────────────────────────────────────
def segs(pts, loop):
    s = [(pts[i], pts[i+1]) for i in range(len(pts)-1)]
    if loop: s.append((pts[-1], pts[0]))
    return s

def pt_seg_dist(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    dx, dy = bx-ax, by-ay
    L2 = dx*dx + dy*dy
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((px-ax)*dx + (py-ay)*dy) / L2))
    return math.hypot(px - (ax+t*dx), py - (ay+t*dy))

def seg_seg_dist(a, b, c, d):
    return min(pt_seg_dist(a, c, d), pt_seg_dist(b, c, d),
               pt_seg_dist(c, a, b), pt_seg_dist(d, a, b))

def shape_dist(s1, s2):
    return min(seg_seg_dist(*e1, *e2) for e1 in s1 for e2 in s2)

# ── 4. 감사 ─────────────────────────────────────────────────────────────────────
print()
print("=" * 92)
print("[2] 단계별 분리 감사 — 모든 «비접합» 도형 쌍의 최근접 거리")
print("=" * 92)
violations = 0
for cname, c in C.items():
    print(f"\n  ── {cname} ──")
    prev = set()
    for st in (0, 1, 2, 3):
        names = c['stages'][st]
        assert prev <= set(names), f"{cname} S{st}: 단조성 위반(이전 단계 도형이 사라졌다)"
        prev = set(names)
        E = {n: segs(c['shapes'][n][1], c['shapes'][n][0]) for n in names}
        worst, worst_pair = 9.9, None
        for n1, n2 in itertools.combinations(names, 2):
            if (n1, n2) in c['joins'] or (n2, n1) in c['joins']: continue
            d = shape_dist(E[n1], E[n2])
            if d < worst: worst, worst_pair = d, (n1, n2)
        xs = [p[0] for n in names for p in c['shapes'][n][1]]
        ys = [p[1] for n in names for p in c['shapes'][n][1]]
        need = max(xs) + 0.10
        bad = worst < SEP
        if bad: violations += 1
        print(f"    S{st}  선 {len(names):>2}개  최근접 «비접합» 쌍 {worst_pair[0]}↔{worst_pair[1]} "
              f"= {worst:.4f} H ({pt(worst):.2f} pt@0.75)  "
              f"{NG if bad else OK}")
        print(f"          x [{min(xs):+.3f}, {max(xs):+.3f}]  y [{min(ys):+.3f}, {max(ys):+.3f}]  "
              f"필요폭 {need:.3f} H = {pt(need):.1f} pt@0.75 / {pt(need,0.35):.1f} pt@0.35")

# ── 5. 작업점이 여전히 도달 원 안인가 ───────────────────────────────────────────
print()
print("=" * 92)
print("[3] 작업점 재검 — 단계가 올라도 손이 닿는 자리가 안 움직였는가")
print("=" * 92)
for cname, c in C.items():
    for w in c['work']:
        d = math.hypot(w[0], w[1] - SHOULDER_Y)
        print(f"    {cname:<20} 작업점 ({w[0]:.2f}, {w[1]:.2f}) H  |어깨-작업점| = {d:.4f} H  "
              f"(도달 {REACH:.4f})  {OK if d <= REACH else NG}")
    if not c['work']:
        print(f"    {cname:<20} 접촉 작업점 없음(«지향» 코스튬) — 지향 착점은 선행 검산 [7]에서 잠근다")

print()
print("=" * 92)
print(f"분리 위반 {violations}건.  0이 아니면 그 단계 조형은 폐기 대상이다.")
print("=" * 92)
