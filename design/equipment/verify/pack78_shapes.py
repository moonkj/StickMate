# -*- coding: utf-8 -*-
"""★ 신규 유료 코호트 2개 — 7 광부(pack.mine) · 8 대마법사(pack.arcane) 좌표 + 전수 게이트.

  python3 pack78_shapes.py             # 전수 게이트
  python3 pack78_shapes.py --dump      # 좌표 전문
  python3 pack78_shapes.py --control   # ★ 양성 대조(일부러 나쁜 값 -> 빨간불이 켜지는가)

구성 근거(실측):
  · 팩 6종 = HEAD/EYES/NECK/BACK + FX + PET.  HAIR 는 **넣을 수 없다** —
    EquipmentModel.IsRetiredSlot(Hair)=true (2026-09-06 사용자 지시로 은퇴).
  · 세트 완성 판정은 head/eyes/neck/back 4부위만 본다(EquipmentStatRules.IsSetComplete).
좌표계: 머리 중심 원점 · R 배수 · +x = 진행 방향 (EQUIPMENT_SHAPE_SPEC 1절과 같다).
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, sectors as S
from rig import Shape
import numpy as np

R = 1.0

# ─────────────────────────────────────────────────────────────────────────────
# 조형 원형(prototype) 두 개 — 팩당 보조색 조각 한 종류(PACK_THEME_SPEC §4)
# ─────────────────────────────────────────────────────────────────────────────
def wedge(root, tip, w_root, w_tip):
    """광부 모티프 «쐐기» — 대칭 사다리꼴. 평행쌍 = (뿌리, 끝), 테이퍼비 tau = w_tip/w_root."""
    ax, ay = root; bx, by = tip
    L = math.hypot(bx-ax, by-ay)
    ux, uy = (bx-ax)/L, (by-ay)/L
    nx, ny = -uy, ux
    return [(ax+nx*w_root/2, ay+ny*w_root/2), (bx+nx*w_tip/2, by+ny*w_tip/2),
            (bx-nx*w_tip/2, by-ny*w_tip/2), (ax-nx*w_root/2, ay-ny*w_root/2)]


def crescent(cx, cy, A, B, d, adeg, n=13):
    """대마법사 모티프 «초승달» — 바깥 원(A) 빼기 안쪽 원(B, adeg 방향으로 d 만큼 밀린 것).
    두 호가 만나는 두 점이 첨점(뿔) 2개. 최대 두께 = A - (B - d)."""
    a = math.radians(adeg)
    ix, iy = cx + math.cos(a)*d, cy + math.sin(a)*d
    # 교점: 바깥 원 중심 기준, 축(a) 방향 좌표 u
    u = (A*A - B*B + d*d) / (2*d)
    h = math.sqrt(max(A*A - u*u, 1e-9))
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    P1 = (cx + ux*u + px*h, cy + uy*u + py*h)
    P2 = (cx + ux*u - px*h, cy + uy*u - py*h)
    t1 = math.atan2(P1[1]-cy, P1[0]-cx)
    t2 = math.atan2(P2[1]-cy, P2[0]-cx)
    # 바깥 호 : P1 -> (축 반대쪽) -> P2   (긴 쪽)
    def span(a0, a1, ccw):
        d_ = (a1 - a0) % (2*math.pi) if ccw else -((a0 - a1) % (2*math.pi))
        return d_
    # 축 반대 방향 각도
    back = math.atan2(-uy, -ux)
    ccw = ((back - t1) % (2*math.pi)) < ((t2 - t1) % (2*math.pi))
    dA = span(t1, t2, ccw)
    outer = [(cx + math.cos(t1 + dA*i/(n-1))*A, cy + math.sin(t1 + dA*i/(n-1))*A) for i in range(n)]
    s1 = math.atan2(P2[1]-iy, P2[0]-ix)
    s2 = math.atan2(P1[1]-iy, P1[0]-ix)
    backi = math.atan2(-uy, -ux)
    ccwi = ((backi - s1) % (2*math.pi)) < ((s2 - s1) % (2*math.pi))
    dB = span(s1, s2, ccwi)
    inner = [(ix + math.cos(s1 + dB*i/(n-1))*B, iy + math.sin(s1 + dB*i/(n-1))*B) for i in range(n)]
    return outer + inner[1:-1]      # 접합점 중복 제거 — 뿔(첨점) 두 개가 살아야 한다


# ═══════════════════════════════════════════════════════════════════════════
# 코호트 7 — 광부  pack.mine   묶는 축: 「쐐기로 깎고 스스로 빛을 켠다」
# ═══════════════════════════════════════════════════════════════════════════
def mine_head():
    """HEAD 안전모 equip.head.minerhelmet — 관은 y=+0.30 위에만 있다(H-2b 밑단).
    감쌈(원칙 4)은 머리 폭 **밖**으로 나간 차양이 진다(베레모·밀짚모자와 같은 방식)."""
    shell = [(-1.44, 0.30), (-1.44, 0.86), (-1.06, 1.14), (-0.62, 1.40), (0.00, 1.48),
             (0.62, 1.40), (1.06, 1.14), (1.44, 0.86), (1.44, 0.30)]
    visor = [(1.42, 0.98), (2.10, 0.73), (2.10, 0.27), (1.42, 0.02)]     # 쐐기 tau=0.479
    rib   = [(-0.66, 1.06), (0.00, 1.38), (0.66, 1.06)]
    return [Shape("HelmetShell", shell, True, filled=True),
            Shape("HelmetVisor", visor, True, filled=True, tone=1),
            Shape("HelmetRib",   rib,  False)]


def mine_eyes():
    """EYES 분진고글 equip.eyes.dustgoggles — 두 눈을 한 장 판이 덮는다."""
    frame = [(-1.34, 0.52), (1.34, 0.52), (1.42, 0.06), (1.16, -0.30), (-1.16, -0.30), (-1.42, 0.06)]
    pad   = [(-0.58, -0.28), (0.58, -0.28), (0.30, -0.96), (-0.30, -0.96)]   # 쐐기 tau=0.517
    strap = [(-1.28, 0.06), (-1.58, 0.76)]
    return [Shape("GoggleFrame", frame, True, filled=True),
            Shape("GogglePad",   pad,   True, filled=True, tone=1),
            Shape("GoggleStrap", strap, False)]


def mine_neck():
    """NECK 가슴 안전등 equip.neck.minelamp — 팩의 간판(PACK_THEME_SPEC §3-2)."""
    yoke  = [(-0.64, -1.26), (-0.26, -1.70), (0.26, -1.70), (0.64, -1.26)]
    shade = [(-0.74, -1.72), (0.74, -1.72), (0.38, -2.40), (-0.38, -2.40)]   # 쐐기 tau=0.514
    glass = [(-0.44, -2.40), (0.44, -2.40), (0.44, -3.14), (-0.44, -3.14)]
    return [Shape("LampYoke",  yoke,  False),
            Shape("LampShade", shade, True, filled=True, tone=1),
            Shape("LampGlass", glass, True, filled=True)]

NECK_SWAY_MINE = ("LampGlass", 2, 2)


def mine_back():
    """BACK 곡괭이 멜빵 equip.shoulders.pickharness — 등 뒤 대각선."""
    haft  = [(0.66, -1.44), (-1.44, -4.36)]
    blade = wedge((-0.26, -3.36), (-1.72, -4.42), 1.10, 0.48)               # 쐐기 tau=0.436
    pad   = [(0.98, -1.16), (0.32, -1.22), (0.06, -2.10), (0.72, -2.04)]    # 주색 채움(멜빵 어깨패드)
    strap = [(0.10, -2.10), (-0.46, -2.86)]
    return [Shape("PickHaft",  haft,  False),
            Shape("PickPad",   pad,   True, filled=True),
            Shape("PickBlade", blade, True, filled=True, tone=1),
            Shape("PickStrap", strap, False)]


def mine_pet():
    """PET 광차 look.pet.orecart — 위 고리 없음 · 네모 짐칸 + 바퀴 둘(작은공/풍선과 정반대)."""
    body  = [(-0.72, -0.20), (0.72, -0.20), (0.56, 0.52), (-0.56, 0.52)]
    load  = wedge((0.00, 0.46), (0.00, 0.98), 1.12, 0.50)                   # 쐐기 tau=0.446
    wheel = [(-0.52, -0.20), (-0.52, -0.70), (0.52, -0.70), (0.52, -0.20)]
    return [Shape("CartBody",  body,  True, filled=True),
            Shape("CartLoad",  load,  True, filled=True, tone=1),
            Shape("CartWheel", wheel, False)]


def mine_fx():
    """FX 탄가루 look.fx.coaldust — 카드 조각. 몸 효과는 입자라 한 알의 모양이 아니라 무늬가 정체(규칙 39-P)."""
    big   = wedge((-0.62, -0.30), (0.46, 0.34), 1.02, 0.46)                 # 쐐기 tau=0.451
    small = wedge((0.62, 0.62), (1.24, 1.02), 0.90, 0.46)
    line  = [(-1.28, 0.74), (-0.52, 0.98)]
    return [Shape("DustChip",  big,   True, filled=True),
            Shape("DustSpark", small, True, filled=True, tone=1),
            Shape("DustTrail", line,  False)]


# ═══════════════════════════════════════════════════════════════════════════
# 코호트 8 — 대마법사  pack.arcane   묶는 축: 「밤을 두른다 — 초승달이 반복된다」
# ═══════════════════════════════════════════════════════════════════════════
def arcane_head():
    """HEAD 뾰족 모자 equip.head.wizardhat — 관 = 삼각, 챙은 머리 폭 밖에서만 처진다."""
    cone = [(-0.34, 2.48), (1.38, 0.36), (2.22, 0.10), (1.96, -0.32), (1.37, 0.315),
            (-1.37, 0.315), (-1.96, -0.32), (-2.22, 0.10), (-1.38, 0.36)]
    moon = crescent(-0.14, 1.16, 0.74, 0.56, 0.30, 200.0)
    band = [(-1.14, 0.46), (1.22, 0.46)]
    return [Shape("HatCone", cone, True, filled=True),
            Shape("HatMoon", moon, True, filled=True, tone=1),
            Shape("HatBand", band, False)]


def arcane_eyes():
    """EYES 관측 렌즈 equip.eyes.astrolens — 두 렌즈가 두 눈을 덮고, 눈썹 자리에 초승달 눈금."""
    lensf = rig.poly(0.52, 0.02, 0.38, 12)
    lensb = rig.poly(-0.52, 0.02, 0.38, 12)
    # ★ 초승달은 **모자 밑단(+0.28) 아래**에 둔다 — 그 위 잉크는 모자를 쓰면 전부 사라진다(E-대역 규칙).
    cradle = crescent(0.0, -0.72, 1.00, 0.82, 0.30, 90.0)
    arm    = [(-0.90, 0.06), (-1.50, 0.62)]
    return [Shape("LensFront",  lensf,  True, filled=True),
            Shape("LensBack",   lensb,  True, filled=True),
            Shape("LensCradle", cradle, True, filled=True, tone=1),
            Shape("LensArm",    arm,    False)]


def arcane_neck():
    """NECK 초승달 걸쇠 equip.neck.moonclasp — 팩의 간판."""
    yoke   = [(-1.06, -1.30), (-0.62, -1.16), (0.62, -1.16), (1.06, -1.30)]
    collar = [(-1.06, -1.30), (1.06, -1.30), (1.22, -2.02), (-1.22, -2.02)]
    moon   = crescent(0.0, -2.74, 0.78, 0.60, 0.32, 270.0)
    return [Shape("ClaspYoke",   yoke,   False),
            Shape("ClaspCollar", collar, True, filled=True),
            Shape("ClaspMoon",   moon,   True, filled=True, tone=1)]

NECK_SWAY_ARCANE = None


def arcane_back():
    """BACK 초승달 로브 equip.shoulders.moonrobe — 밑단이 초승달로 크게 파인다(긴망토와 반대)."""
    hem = [(1.72, -4.34)]
    for i in range(9):
        t = math.pi * (i / 8.0)
        hem.append((1.72*math.cos(t), -4.34 + 1.02*math.sin(t)))
    robe = [(-1.12, -1.26), (1.12, -1.26), (1.74, -2.70)] + hem[1:] + [(-1.74, -2.70)]
    moon = crescent(-0.16, -2.36, 0.92, 0.72, 0.34, 250.0)
    yoke = [(-1.04, -1.20), (0.00, -1.02), (1.04, -1.20)]
    return [Shape("RobeBody", robe, True, filled=True),
            Shape("RobeMoon", moon, True, filled=True, tone=1),
            Shape("RobeYoke", yoke, False)]


def arcane_pet():
    """PET 마도서 look.pet.grimoire — 펼친 책(가로로 넓은 W 실루엣)."""
    book = [(-1.02, -0.28), (0.00, -0.02), (1.02, -0.28), (1.02, 0.32), (0.00, 0.60), (-1.02, 0.32)]
    moon = crescent(0.0, 1.16, 0.70, 0.54, 0.32, 270.0)
    seam = [(0.00, -0.02), (0.00, 0.60)]
    return [Shape("BookLeaf", book, True, filled=True),
            Shape("BookMoon", moon, True, filled=True, tone=1),
            Shape("BookSeam", seam, False)]


def arcane_fx():
    """FX 달무리 look.fx.moonring — 카드 조각."""
    big   = crescent(-0.10, 0.00, 1.16, 0.94, 0.34, 200.0)
    small = crescent(0.98, 0.98, 0.66, 0.50, 0.30, 200.0)
    line  = [(-0.30, -1.24), (0.62, -0.94)]
    return [Shape("HaloArc",  big,   True, filled=True),
            Shape("HaloSpark", small, True, filled=True, tone=1),
            Shape("HaloTrail", line,  False)]


MINE = {
    "HEAD": ("안전모",      "equip.head.minerhelmet",     mine_head, 0.0),
    "EYES": ("분진고글",    "equip.eyes.dustgoggles",     mine_eyes, 0.0),
    "NECK": ("가슴 안전등", "equip.neck.minelamp",        mine_neck, rig.SHOULDER_R),
    "BACK": ("곡괭이 멜빵", "equip.shoulders.pickharness", mine_back, rig.SHOULDER_R),
    "PET":  ("광차",        "look.pet.orecart",           mine_pet, 0.0),
    "FX":   ("탄가루",      "look.fx.coaldust",           mine_fx, 0.0),
}
ARCANE = {
    "HEAD": ("뾰족 모자",   "equip.head.wizardhat",       arcane_head, 0.0),
    "EYES": ("관측 렌즈",   "equip.eyes.astrolens",       arcane_eyes, 0.0),
    "NECK": ("초승달 걸쇠", "equip.neck.moonclasp",       arcane_neck, rig.SHOULDER_R),
    "BACK": ("초승달 로브", "equip.shoulders.moonrobe",   arcane_back, rig.SHOULDER_R),
    "PET":  ("마도서",      "look.pet.grimoire",          arcane_pet, 0.0),
    "FX":   ("달무리",      "look.fx.moonring",           arcane_fx, 0.0),
}
PACKS = [("광부 pack.mine (코호트 7)", MINE), ("대마법사 pack.arcane (코호트 8)", ARCANE)]


# ═══════════════════════════════════════════════════════════════════════════
#                                   게 이 트
# ═══════════════════════════════════════════════════════════════════════════
W075, W060 = rig.stroke_in_R(0.75), rig.stroke_in_R(0.60)
W100 = rig.stroke_in_R(1.00)
RATCHET = S.SILHOUETTE_RATCHET_R
FILL_OUTLINE_PEN = 0.2181818          # 규칙 1-C 색면 조건(배율 0.509 이상 상수)

# ---- 카드(프로덕션 실측: CharacterInfoWindow.IconSize=58 · AccessoryCardIcon.Frame) ----
CARD_SIZE, SLOT_SIZE = 58.0, 24.0
ICON_VIEWBOX, ICON_STROKE_U = 64.0, 2.2
HEAD_RADIUS_PX = 28.0 * (158.0 / 200.0)                  # 22.12
SLOT_BOX = {"HEAD": 70.0, "EYES": 48.0, "NECK": 54.0, "BACK": 88.0}
FULLBOX_UPR = 16.0

def units_per_R(slot):
    if slot in ("FX", "PET"): return FULLBOX_UPR
    return HEAD_RADIUS_PX / (SLOT_BOX[slot] / ICON_VIEWBOX)

def card_stroke_pt(size): return size * ICON_STROKE_U / ICON_VIEWBOX

_fail = []
def bad(m): _fail.append(m); print("  x  " + m)
def ok(m):  print("  OK " + m)


def true_min_edge(sh):
    m, where = 9e9, None
    for s in sh:
        p = s.pts + ([s.pts[0]] if s.loop else [])
        for a, b in zip(p, p[1:]):
            d = math.hypot(b[0]-a[0], b[1]-a[1])
            if d < m: m, where = d, (s.name, a, b)
    return m, where


def corner_min_edge(sh, w):
    """규칙 1 원문 — **양끝이 모두 꺾임(>=45도)인 변**만 잰다(rig.rule_one 과 같은 식)."""
    worst, where = 9e9, None
    for s in sh:
        p, n = s.pts, len(s.pts)
        if n < 2: continue
        corner = [False]*n
        rng = range(n) if s.loop else range(1, n-1)
        for i in rng:
            corner[i] = rig.turn_deg(p[(i-1) % n], p[i], p[(i+1) % n]) >= rig.CORNER_DEG
        segs = n if s.loop else n-1
        for i in range(segs):
            j = (i+1) % n
            if not (corner[i] and corner[j]): continue
            L = math.dist(p[i], p[j])
            if L < worst: worst, where = L, (s.name, p[i], p[j])
    return worst, where


def ink_rect(s):
    xs = [p[0] for p in s.pts]; ys = [p[1] for p in s.pts]
    return max(xs)-min(xs), max(ys)-min(ys)


def rho_max(pts, grid=0.01):
    """도형 안 최대 내접원 반지름(색면 조건). 격자 스캔 + 변까지의 거리."""
    x0, y0, x1, y1 = rig.bounds(pts)
    n = len(pts)
    best = 0.0
    ys = np.arange(y0, y1, grid); xs = np.arange(x0, x1, grid)
    for y in ys:
        for x in xs:
            if not rig.contains(pts, (x, y)): continue
            d = 9e9
            for i in range(n):
                ax, ay = pts[i]; bx, by = pts[(i+1) % n]
                vx, vy = bx-ax, by-ay
                L2 = vx*vx+vy*vy
                t = 0.0 if L2 < 1e-12 else max(0.0, min(1.0, ((x-ax)*vx+(y-ay)*vy)/L2))
                d = min(d, math.hypot(x-(ax+vx*t), y-(ay+vy*t)))
            best = max(best, d)
    return best


# ---- 모티프 분류(PACK_THEME_SPEC §4-1 결정나무 + 신규 두 잎) ----
SHARP_DEG = 70.0

def sharp_corners(pts, deg=SHARP_DEG):
    n = len(pts); c = 0
    for i in range(n):
        a, b, d = pts[(i-1) % n], pts[i], pts[(i+1) % n]
        v1 = (a[0]-b[0], a[1]-b[1]); v2 = (d[0]-b[0], d[1]-b[1])
        n1 = math.hypot(*v1); n2 = math.hypot(*v2)
        if n1 < 1e-12 or n2 < 1e-12: continue
        cos = max(-1.0, min(1.0, (v1[0]*v2[0]+v1[1]*v2[1])/(n1*n2)))
        if math.degrees(math.acos(cos)) < deg: c += 1
    return c


def taper_ratio(pts):
    """4각형 전용 — 가장 평행한 마주보는 변 쌍의 (짧은 변 / 긴 변). 4각이 아니면 None."""
    if len(pts) != 4: return None
    E = [(pts[(i+1) % 4][0]-pts[i][0], pts[(i+1) % 4][1]-pts[i][1]) for i in range(4)]
    L = [math.hypot(*e) for e in E]
    def ang(u, v):
        a = math.degrees(math.atan2(u[1], u[0])) % 180.0
        b = math.degrees(math.atan2(v[1], v[0])) % 180.0
        d = abs(a-b) % 180.0
        return min(d, 180.0-d)
    pairs = [(ang(E[0], E[2]), L[0], L[2]), (ang(E[1], E[3]), L[1], L[3])]
    pairs.sort(key=lambda t: (t[0], -max(t[1], t[2])))
    _, a, b = pairs[0]
    return min(a, b)/max(a, b)


def convex_deficiency(pts):
    return S.convex_deficiency([Shape("M", list(pts), True, filled=True)])


def classify(pts):
    n = len(pts)
    x0, y0, x1, y1 = rig.bounds(pts)
    w, h = x1-x0, y1-y0
    aspect = max(w, h)/max(1e-9, min(w, h))
    cdef = convex_deficiency(pts)
    sharp = sharp_corners(pts)
    tau = taper_ratio(pts)
    if cdef >= 0.15 and sharp >= 2 and n >= 12: return "대마법사"          # 깊게 베어 문 초승달, 뿔 2
    if n == 4 and tau is not None and tau <= 0.55: return "광부"           # 한쪽으로 좁아지는 쐐기
    if cdef >= 0.15:  return "밀리터리"
    if n <= 3:        return "네온 낙서"
    if n <= 4:        return "오피스 워커"
    if n <= 8:        return "사이버 아포칼립스"
    if sharp >= 1:    return "컬러 잉크"
    return "스포츠"


def measure_row(pts):
    n = len(pts)
    x0, y0, x1, y1 = rig.bounds(pts)
    w, h = x1-x0, y1-y0
    return n, max(w, h)/max(1e-9, min(w, h)), convex_deficiency(pts), sharp_corners(pts), taper_ratio(pts)


# ---- H-2 / H-2b (§14-16-1 정본, r24_hats 의 자를 그대로 import) ----
def h2_measure(shapes):
    import r24_hats as R24
    pieces = [dict(name=s.name, pts=list(s.pts), filled=bool(s.filled), loop=bool(s.loop),
                   tone=s.tone, sort=R24.SORT_HEAD) for s in shapes]
    wear = R24.h2_wear(pieces)
    front = [p for p in pieces if p["filled"] and p["sort"] >= R24.SORT_EYES]
    HR = R24.HEAD_R_COVER
    xs = np.arange(-HR, HR, 0.002)
    bottom = None
    for y in np.arange(HR, -1.2, -0.002):
        row = np.column_stack([xs, np.full_like(xs, y)])
        cov = np.zeros(len(xs), bool)
        for p in front: cov |= R24.inside(p["pts"], row)
        if cov.any(): bottom = float(y)
    # 하한 검사는 격자만큼 봐준다(§14-16-3) -> 보고값 - h
    return wear, (None if bottom is None else bottom - 0.002)


ARM_R = 0.32972 * 2.2746944 / 0.22        # 3.40909 R — MOTION_SPEC 23-2 실측 (상완 0.38 + 전완 0.37)


THETA_INK = 0.3476                       # DESIGN_FAN_MENU_ICONS R26-1 잉크 문턱(면색 대비 3.0:1)

def worst_phase_alpha(t_pt, dpi):
    """★ 내 식을 발명하지 않는다 — docs/DESIGN_FAN_MENU_ICONS.md R26-1 이 이미 낸 모형을 그대로 쓴다:
        「알파 1 인 심은 t 가 아니라 t - 0.5 다」 · 최악 위상 관측값 0.825pt -> 0.325.
    교정: worst_phase_alpha(0.825, 1.0) == 0.325 여야 한다. 깨지면 그 뒤 숫자를 전부 폐기한다."""
    return max(0.0, min(1.0, t_pt * dpi - 0.5))


assert abs(worst_phase_alpha(0.825, 1.0) - 0.325) < 1e-9, "잉크 문턱 자 교정 실패 — 숫자를 쓰지 마라"


def eyes_occlusion(head, eyes, grid=0.01):
    """모자 앞층 채움이 안경 채움 면적의 몇 %를 덮는가."""
    ep = [s for s in eyes if s.filled]
    hp = [s for s in head if s.filled]
    if not ep: return 0.0
    xs = [q[0] for s in ep for q in s.pts]; ys = [q[1] for s in ep for q in s.pts]
    tot = hit = 0
    y = min(ys)
    while y <= max(ys):
        x = min(xs)
        while x <= max(xs):
            if any(rig.contains(s.pts, (x, y)) for s in ep):
                tot += 1
                if any(rig.contains(s.pts, (x, y)) for s in hp): hit += 1
            x += grid
        y += grid
    return 0.0 if tot == 0 else hit/tot


def gate(control=False):
    print("=" * 100)
    print("팩 7·8 (광부 · 대마법사) 12종 전수 게이트")
    print("=" * 100)
    print("  W@0.75 = %.5fR  ·  W@0.60 = %.5fR  ·  W@1.00 = %.5fR  ·  래칫 = %.4fR"
          % (W075, W060, W100, RATCHET))
    print("  머리 지름 2R = %.2f획@0.75 = %.2f획@0.60  (11.63pt @0.75 · 9.30pt @0.60 · 15.51pt @1.00)"
          % (2/W075, 2/W060))
    print("  카드 %.0fpt · 획 %.5fpt   |   슬롯행 %.0fpt · 획 %.5fpt"
          % (CARD_SIZE, card_stroke_pt(CARD_SIZE), SLOT_SIZE, card_stroke_pt(SLOT_SIZE)))

    for packname, PACK in PACKS:
        print("\n" + "=" * 100)
        print("  %s" % packname)
        print("=" * 100)
        P = {k: v[2]() for k, v in PACK.items()}
        if control and PACK is MINE:
            P["HEAD"] = [Shape("A", [(0, 0), (0.12, 0), (0.12, 0.12), (0, 0.12)], True, filled=True),
                         Shape("B", [(0, 0), (0.1, 0.1)], False, tone=1),
                         Shape("C", [(0, 0), (0.1, 0.1)], False, tone=1)]
            P["NECK"] = [Shape("A", [(0, 3), (3, 3), (3, 5), (0, 5)], True, filled=True, tone=1)]

        print("\n  -- (1) 도형 규칙 --")
        for k in ("HEAD", "EYES", "NECK", "BACK", "PET", "FX"):
            sh = P[k]; n = PACK[k][0] if not control else k
            if not (2 <= len(sh) <= 4): bad("%s 정원 %d개 (2~4)" % (n, len(sh)))
            acc = sum(1 for s in sh if s.tone == 1)
            if acc != 1: bad("%s 보조색 %d개 (정확히 1)" % (n, acc))
            if k in ("HEAD", "EYES") and not any(s.filled for s in sh): bad("%s 채움 없음(규칙 2)" % n)
            e0, w0 = corner_min_edge(sh, W075)
            et, wt = true_min_edge(sh)
            if e0 < 9e8 and e0 < W075:
                bad("%s 규칙1(꺾임-꺾임) 최단 %.4fR = %.2f획@0.75 < 1.00 (%s)" % (n, e0, e0/W075, w0[0]))
            for s in sh:
                if s.loop and rig.self_intersects(s.pts): bad("%s '%s' 자기교차" % (n, s.name))
                span = max(ink_rect(s))
                if span < 1.5*W075: bad("%s '%s' 잉크 사각형 span %.3f < 1.5획@0.75(%.3f)" % (n, s.name, span, 1.5*W075))
                if s.filled:
                    w_, h_ = ink_rect(s)
                    if min(w_, h_) < 1.5*W075:
                        bad("%s '%s' 채움 잉크 사각형 %.3fx%.3f < 1.5획@0.75(%.3f)" % (n, s.name, w_, h_, 1.5*W075))
            ok("%-12s 도형 %d · 보조색 %d · 규칙1 최단 %s · 실제 최단변 %.3fR=%.2f획@0.75=%.2f획@0.60"
               % (n, len(sh), acc,
                  ("없음(꺾임쌍 0)" if e0 > 9e8 else "%.3fR=%.2f획@0.75" % (e0, e0/W075)),
                  et, et/W075, et/W060))

        print("\n  -- (2) 채움 색면 조건 (rho_max >= %.5fR = 채움 윤곽 펜 하나, 규칙 1-C) --" % FILL_OUTLINE_PEN)
        for k in ("HEAD", "EYES", "NECK", "BACK", "PET", "FX"):
            for s in P[k]:
                if not s.filled: continue
                r = rho_max(s.pts)
                if r < FILL_OUTLINE_PEN:
                    bad("%s '%s' rho_max %.4fR < %.4f" % (PACK[k][0], s.name, r, FILL_OUTLINE_PEN))
                else:
                    ok("%-12s %-12s rho_max %.4fR = %.2f펜" % (PACK[k][0], s.name, r, r/FILL_OUTLINE_PEN))

        print("\n  -- (3) 슬롯 경계 --")
        p = [q for s in P["HEAD"] for q in s.pts]
        t, b = max(q[1] for q in p), min(q[1] for q in p)
        if not (1.0 < t < 2.551): bad("HEAD 꼭대기 %.2f (1.0<t<2.551)" % t)
        if b <= -1.0: bad("HEAD 턱 아래 %.2f" % b)
        wrap = any(abs(q[0]) >= 0.85 and q[1] <= 0.05 for q in p)
        if not wrap: bad("HEAD 감쌈 실패(원칙 4: |x|>=0.85 & y<=0.05)")
        ok("HEAD 꼭대기 %+.3f · 밑 %+.3f · 감쌈 %s" % (t, b, "있음" if wrap else "없음"))

        p = [q for s in P["EYES"] for q in s.pts]
        f = [s for s in P["EYES"] if s.filled]
        mx = max(abs(q[0]) for q in p)
        if mx >= 1.6: bad("EYES |x| %.2f >= 1.6" % mx)
        if max(q[1] for q in p) >= 1.15: bad("EYES 정수리 침범 %.2f" % max(q[1] for q in p))
        if min(q[1] for q in p) <= -2.2: bad("EYES 목 아래 %.2f" % min(q[1] for q in p))
        fe = any(rig.contains(s.pts, (rig.EYE_X, rig.EYE_Y)) for s in f)
        be = any(rig.contains(s.pts, (-rig.EYE_X, rig.EYE_Y)) for s in f)
        if not fe: bad("EYES 앞눈 미커버")
        if not be: bad("EYES 뒤눈 미커버 (한쪽만 가리면 EYE_FRONT_ONLY 등록 + 드러난 눈 도형 필요 — 39-5)")
        if fe and be: ok("EYES |x|max %.3f · y[%+.3f,%+.3f] · 두 눈 모두 커버" % (mx, min(q[1] for q in p), max(q[1] for q in p)))

        p = [q for s in P["NECK"] for q in s.pts]
        if max(q[1] for q in p) >= 0.0: bad("NECK 얼굴 침범 %.2f" % max(q[1] for q in p))
        if min(q[1] for q in p) <= rig.HIP_R - 0.517: bad("NECK 고관절 아래 %.2f" % min(q[1] for q in p))
        else: ok("NECK y[%+.3f,%+.3f] (상한 0.00 / 하한 %.3f)" % (min(q[1] for q in p), max(q[1] for q in p), rig.HIP_R-0.517))

        p = [q for s in P["BACK"] for q in s.pts]
        if max(q[1] for q in p) >= 1.0: bad("BACK 정수리 위 %.2f" % max(q[1] for q in p))
        if min(q[1] for q in p) <= -9.3395: bad("BACK 바닥 관통 %.2f" % min(q[1] for q in p))
        else: ok("BACK y[%+.3f,%+.3f] (상한 1.00 / 하한 -9.34)" % (min(q[1] for q in p), max(q[1] for q in p)))

        print("\n  -- (4) H-2 / H-2b (신규 모자 정본 대역, docs/EQUIPMENT_HANDOFF_PORT_SPEC 14-16-1) --")
        wear, bot = h2_measure(P["HEAD"])
        if wear is None: bad("HEAD H-2 착용선 없음(꼭대기부터 덮지 못한다)")
        else:
            tail = None if bot is None else wear - bot
            okw = 0.28 <= wear <= 0.45
            okb = bot is not None and 0.28 <= bot <= 0.35
            okt = tail is not None and tail <= 0.170
            if not okw: bad("HEAD 착용선 %+.4f 대역 [+0.28,+0.45] 밖" % wear)
            if not okb: bad("HEAD 밑단 %s 대역 [+0.28,+0.35] 밖" % ("없음" if bot is None else "%+.4f" % bot))
            if not okt: bad("HEAD 꼬리 %s > 0.170" % ("없음" if tail is None else "%.4f" % tail))
            if okw and okb and okt:
                ok("HEAD 착용선 %+.4f  밑단 %+.4f(격자보정)  꼬리 %.4f  -- 세 대역 전부 통과" % (wear, bot, tail))

        print("\n  -- (5) 카드 두 크기 (프로덕션 프레임: 슬롯 고정 배율 + 넘침 축소) --")
        for k in ("HEAD", "EYES", "NECK", "BACK", "PET", "FX"):
            sh = P[k]; n = PACK[k][0] if not control else k
            pts = [q for s in sh for q in s.pts]
            x0, y0, x1, y1 = rig.bounds(pts)
            span = max(x1-x0, y1-y0)
            upr = units_per_R(k)
            shrink = min(1.0, ICON_VIEWBOX / (span*upr))
            et, wt = true_min_edge(sh)
            for size in (CARD_SIZE, SLOT_SIZE):
                ptr = upr * shrink * (size / ICON_VIEWBOX)     # pt per R
                st = card_stroke_pt(size)
                need = st / ptr
                tag = "카드" if size == CARD_SIZE else "슬롯행"
                if et < need: bad("%s %s 최단변 %.3fpt < 획 %.3fpt (%s)" % (n, tag, et*ptr, st, wt[0]))
                else:
                    ok("%-12s %-4s span %.2fR->%.1fu 축소x%.3f · %.2fpt/R · 최단변 %.2fpt = %.2f획"
                       % (n, tag, span, span*upr, shrink, ptr, et*ptr, et*ptr/st))

        print("\n  -- (6) 쌍별 실루엣 차 (신규 1종 vs 기본 6종, L-inf 프로파일) --")
        BASE = {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK, "BACK": items.BACK}
        for k in ("HEAD", "EYES", "NECK", "BACK"):
            anc = PACK[k][3]
            mine = S.profile(P[k], anc)
            worst = (9e9, None)
            for bn, bsh in BASE[k].items():
                sh = bsh() if callable(bsh) else bsh
                d = rig.max_delta(mine, S.profile(sh, anc))
                if d < worst[0]: worst = (d, bn)
            n = PACK[k][0] if not control else k
            tag = "래칫" if worst[0] >= RATCHET else ("하한만" if worst[0] >= W075 else "미달")
            if worst[0] < W075:
                bad("%s 쌍별 최소 %.3fR = %.2f획@0.75 (vs %s) — 하한 1.00 미달" % (n, worst[0], worst[0]/W075, worst[1]))
            else:
                ok("%-12s 최악쌍 vs %-8s %.3fR = %.2f획@0.75 = %.2f획@0.60  %s"
                   % (n, worst[1], worst[0], worst[0]/W075, worst[0]/W060, tag))
        import appearance as A
        petfloor = 0.3149
        for k, table, floor in (("PET", A.PET_NOW, petfloor), ("FX", A.FX_NOW, None)):
            mine = S.profile(P[k], 0.0)
            worst = (9e9, None)
            for bn, bsh in table.items():
                if not bsh: continue
                d = rig.max_delta(mine, S.profile(bsh, 0.0))
                if d < worst[0]: worst = (d, bn)
            n = PACK[k][0] if not control else k
            lim = floor if floor is not None else W075
            if worst[0] < lim:
                bad("%s 쌍별 최소 %.3fR (vs %s) < 하한 %.3fR" % (n, worst[0], worst[1], lim))
            else:
                ok("%-12s 최악쌍 vs %-10s %.3fR = %.2f획@0.75  (하한 %.3fR 대비 %+.3fR)"
                   % (n, worst[1], worst[0], worst[0]/W075, lim, worst[0]-lim))

        print("\n  -- (6b) HEAD 쌍별 — **프로덕션 소스를 직접 파싱한** 모자 6종과 다시 (items.py 는 v1 거울이다) --")
        import r24_hats as _R24
        prod = {n: [Shape(q["name"], q["pts"], q["loop"], q["filled"], q.get("tone", 0)) for q in f()]
                for n, f in _R24.HATS.items()}
        mp = S.profile(P["HEAD"], 0.0)
        worst = (9e9, None)
        for n_, sh_ in prod.items():
            d = rig.max_delta(mp, S.profile(sh_, 0.0))
            if d < worst[0]: worst = (d, n_)
        if worst[0] < W075:
            bad("%s 프로덕션 파싱 쌍별 최소 %.3fR (vs %s) 하한 미달" % (PACK["HEAD"][0], worst[0], worst[1]))
        else:
            ok("%-12s 프로덕션 최악쌍 vs %-6s %.3fR = %.2f획@0.75 (v1 거울보다 %s)"
               % (PACK["HEAD"][0], worst[1], worst[0], worst[0]/W075, "넉넉하다"))

        print("\n  -- (7) 모티프 분류 (팩당 보조색 조각 한 종류, PACK_THEME_SPEC 4-1 + 신규 두 잎) --")
        want = "광부" if PACK is MINE else "대마법사"
        for k in ("HEAD", "EYES", "NECK", "BACK", "PET", "FX"):
            acc = [s for s in P[k] if s.tone == 1]
            if not acc: continue
            n_, a_, c_, s_, t_ = measure_row(acc[0].pts)
            got = classify(acc[0].pts)
            if got != want: bad("%s 보조색 조각이 「%s」로 분류됨(기대 %s)" % (PACK[k][0], got, want))
            else: ok("%-12s n=%2d 종횡비 %.2f 결손 %.4f 첨점 %d 테이퍼 %s -> 「%s」"
                     % (PACK[k][0], n_, a_, c_, s_, ("-" if t_ is None else "%.3f" % t_), got))

        print("\n  -- (8) 팩 안 6/6 동시 착용 — 모자가 안경을 얼마나 먹는가 --")
        cov = eyes_occlusion(P["HEAD"], P["EYES"])
        base = 0.064   # 출하 모자6 x 안경6 36조합에서 «안경 생존» 평균 6.4%(pack_fit.py 대조군)
        if cov > 0.55: bad("%s: 모자가 안경 면적의 %.1f%% 를 덮는다(출하 결함 대역)" % (PACK["HEAD"][0], cov*100))
        else: ok("모자 %s x 안경 %s -> 안경 가려짐 %.1f%% · 생존 %.1f%% (출하 36조합 평균 생존 6.4%%)"
                 % (PACK["HEAD"][0], PACK["EYES"][0], cov*100, (1-cov)*100))

        print("\n  -- (9) 잉크 문턱 — 낱선이 1x(Windows 100%) 최악 위상에서 살아남는가 --")
        for tag, size in (("카드 58pt", CARD_SIZE), ("슬롯행 24pt", SLOT_SIZE)):
            st = card_stroke_pt(size)
            a1, a2 = worst_phase_alpha(st, 1.0), worst_phase_alpha(st, 2.0)
            if a1 < THETA_INK:
                print("  ** %s 낱선 %.4fpt -> 1x 최악 위상 알파 %.4f < 문턱 %.4f  [기본 42종 공통 상속 · 미해결 백로그]"
                      " · 2x 알파 %.4f" % (tag, st, a1, THETA_INK, a2))
            else:
                ok("%s 낱선 %.4fpt -> 1x 최악 위상 알파 %.4f >= 문턱 %.4f · 2x %.4f" % (tag, st, a1, a1 and a1, THETA_INK, a2) if False
                   else "%s 낱선 %.4fpt -> 1x 최악 위상 알파 %.4f >= 문턱 %.4f · 2x %.4f" % (tag, st, a1, THETA_INK, a2))
        for k in ("HEAD", "EYES", "NECK", "BACK", "PET", "FX"):
            acc = [s_ for s_ in P[k] if s_.tone == 1][0]
            r = rho_max(acc.pts)
            upr = units_per_R(k)
            pts_ = [q for s_ in P[k] for q in s_.pts]
            x0, y0, x1, y1 = rig.bounds(pts_); span = max(x1-x0, y1-y0)
            shrink = min(1.0, ICON_VIEWBOX/(span*upr))
            w24 = 2*r*upr*shrink*(SLOT_SIZE/ICON_VIEWBOX)
            if w24 < 1.0: bad("%s 보조색 조각 최대 두께 %.2fpt @24pt < 1.0px (1x 에서 사라진다)" % (PACK[k][0], w24))
            else: ok("%-12s 보조색 조각 최대 두께 %.2fpt @24pt · %.2fpt @58pt (채움이라 알파 1.0)"
                     % (PACK[k][0], w24, 2*r*upr*shrink*(CARD_SIZE/ICON_VIEWBOX)))

        print("\n  -- (9b) ★ 낱선이 사라져도 물건이 갈리는가 (채움만으로 쌍별 실루엣) --")
        BASEF = {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK, "BACK": items.BACK}
        for k in ("HEAD", "EYES", "NECK", "BACK"):
            anc = PACK[k][3]
            mineF = [s_ for s_ in P[k] if s_.filled]
            mine = S.profile(mineF, anc)
            worst = (9e9, None)
            for bn, bsh in BASEF[k].items():
                sh = bsh() if callable(bsh) else bsh
                shF = [x for x in sh if getattr(x, "filled", False)]
                if not shF: continue
                d = rig.max_delta(mine, S.profile(shF, anc))
                if d < worst[0]: worst = (d, bn)
            n = PACK[k][0] if not control else k
            if worst[1] is None: continue
            if worst[0] < W075:
                bad("%s 채움만 쌍별 최소 %.3fR = %.2f획@0.75 (vs %s) — 낱선이 죽으면 안 갈린다" % (n, worst[0], worst[0]/W075, worst[1]))
            else:
                ok("%-12s 채움만 최악쌍 vs %-8s %.3fR = %.2f획@0.75" % (n, worst[1], worst[0], worst[0]/W075))

        print("\n  -- (10) 팔 도달 (팔 길이 %.4f R = 0.32972 H) --" % ARM_R)
        for k in ("NECK", "BACK", "HEAD"):
            pts_ = [q for s_ in P[k] for q in s_.pts]
            far = max(math.hypot(q[0], q[1]-rig.SHOULDER_R) for q in pts_)
            near = min(math.hypot(q[0], q[1]-rig.SHOULDER_R) for q in pts_)
            tag = "손이 닿는다" if near <= ARM_R else "손이 못 닿는다"
            if near > ARM_R:
                bad("%s: 어깨에서 최근접 %.3fR > 팔 %.3fR — 만지는 동작을 설계에 넣지 마라" % (PACK[k][0], near, ARM_R))
            else:
                ok("%-12s 어깨-최근접 %.3fR / 최원 %.3fR · %s" % (PACK[k][0], near, far, tag))

    print("\n" + "=" * 100)
    print("결과: %s" % ("전수 통과 (위반 0건)" if not _fail else "위반 %d건" % len(_fail)))
    return len(_fail)


def motif_tree_control():
    """★ 신규 두 잎이 기존 여섯을 훔치지 않는가 — 음성 대조 6/6 + 양성 대조 4/4."""
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "verify"))
    import importlib
    pm = importlib.import_module("packmotif")
    print("\n" + "=" * 100)
    print("모티프 결정나무 — 음성 대조(기존 6개가 그대로 자기 잎에 남는가)")
    print("=" * 100)
    n_ok = 0
    for name, fn in pm.PROTO.items():
        got = classify(fn())
        good = got == name
        n_ok += good
        print("  %s %-20s -> 「%s」" % ("OK " if good else "x  ", name, got))
    print("  음성 대조 %d/6. **6/6 이 아니면 신규 두 잎이 기존 팩을 훔친 것이다.**" % n_ok)
    print("\n양성 대조 — 신규 모티프를 망가뜨리면 다른 잎으로 떨어지는가")
    cases = [
        ("쐐기의 테이퍼를 없앤다(평행 사각)", wedge((0, 0), (1.0, 0), 0.9, 0.9), "광부"),
        ("쐐기를 삼각으로 만든다",            [(0, 0.45), (1.0, 0.0), (0, -0.45)], "광부"),
        ("초승달의 파임을 얕게(거의 원)",     crescent(0, 0, 0.70, 0.20, 0.62, 200.0), "대마법사"),
        ("초승달을 뿔 없는 다각으로",         rig.poly(0, 0, 0.6, 16), "대마법사"),
    ]
    hit = 0
    for label, pts, want in cases:
        got = classify(pts)
        caught = got != want
        hit += caught
        n_, a_, c_, s_, t_ = measure_row(pts)
        print("  %s %-34s n=%2d 결손 %.3f 첨점 %d 테이퍼 %s -> 「%s」"
              % ("OK " if caught else "x  ", label, n_, c_, s_, ("-" if t_ is None else "%.2f" % t_), got))
    print("  양성 대조 %d/4. **4/4 가 아니면 신규 잎의 판정을 폐기한다.**" % hit)

    # ★ 넓은 프로브 — 원형 6개만 먹이는 것은 **좁은 자**다. 기본 42종 조각 전수를 먹여 본다.
    #   결정나무는 전역 함수라 어떤 도형이든 잎 하나에 떨어진다. 그래서 pass/fail 이 아니라 **관측**이다:
    #   기본 42종의 **보조색(tone 1)** 조각이 신규 두 잎에 떨어지면 그 모티프는 배타적 신호가 아니다.
    import items as _it, hair as _ha, appearance as _ap
    print("\n넓은 프로브 — 기본 42종 조각 전수를 신규 두 잎에 먹인다 (pass/fail 아님, 관측)")
    tables = [("HEAD", _it.HEAD), ("EYES", _it.EYES), ("NECK", _it.NECK), ("BACK", _it.BACK), ("HAIR", _ha.SET),
              ("FX", _ap.FX_NOW), ("PET", _ap.PET_NOW)]
    n_all = n_acc = 0
    for slot, tb in tables:
        for nm, fn in tb.items():
            sh = fn() if callable(fn) else fn
            if not sh: continue
            for sp in sh:
                if len(sp.pts) < 3: continue
                c = classify(sp.pts)
                if c not in ("광부", "대마법사"): continue
                n_all += 1
                mark = ""
                if sp.tone == 1:
                    n_acc += 1
                    mark = "  <-- ★ 보조색 조각. 이 잎이 배타적이지 않다는 뜻"
                print("    %-5s %-9s %-16s -> %-6s tone=%d filled=%s n=%d%s"
                      % (slot, nm, sp.name, c, sp.tone, sp.filled, len(sp.pts), mark))
    print("    합계 %d개(그중 보조색 %d개). ★ 보조색 %d개는 문서 3절 「자기 발견 약점」에 그대로 적혀 있다."
          % (n_all, n_acc, n_acc))
    return n_ok == 6 and hit == 4


if __name__ == "__main__":
    if "--dump" in sys.argv:
        for packname, PACK in PACKS:
            print("\n##### %s" % packname)
            for k, (nm, iid, fn, anc) in PACK.items():
                print("\n[%s] %s  %s   (앵커 y=%.5f)" % (k, nm, iid, anc))
                for s in fn():
                    print("  %-14s loop=%-5s filled=%-5s tone=%d  n=%d"
                          % (s.name, s.loop, s.filled, s.tone, len(s.pts)))
                    for x, y in s.pts: print("      (%+.4f, %+.4f)" % (x, y))
        sys.exit(0)
    ctl = "--control" in sys.argv
    if ctl:
        print("★ 양성 대조 모드 — 일부러 나쁜 값을 넣는다. **빨간불이 켜져야 정상.**\n")
        n = gate(True)
        print("\n★ 도형 게이트 양성 대조: %s" % ("OK — 실제로 잡는다(위반 %d건)" % n if n else "FAIL"))
        motif_tree_control()
        sys.exit(0)
    n = gate(False)
    motif_tree_control()
    sys.exit(1 if n else 0)
