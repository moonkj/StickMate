# -*- coding: utf-8 -*-
"""★ 코스튬 소환 오브젝트(프롭) 4종 × 진화 4상태 — 좌표 + 전수 게이트.

  python3 r28_props.py             # 전수 게이트
  python3 r28_props.py --dump      # 좌표 전문  -> r28_props_coords.txt
  python3 r28_props.py --control   # ★ 양성 대조(일부러 나쁜 값 -> 빨간불이 켜지는가)

좌표계 (design-motion `docs/UX_MOTION_COSTUME_FOCUS.md` 4-1절 그대로):
    원점  = 몰입기 진입 프레임의 캐릭터 루트 x (스냅샷 후 고정)
    y = 0 = 같은 프레임의 발바닥(접지선)
    +x   = facing.  단위 = **H(전신 신장) 배수**.  ★ 절대 유닛/pt 로 적지 않는다.

★ 이 파일은 장비 12종 하니스(`pack78_shapes.py`)와 **같은 자**를 쓰되 단위만 R -> H 다.
  1 H = 10.339520 R (= StickConfig.BaselineCharacterTotalHeight / 0.22, 프로덕션 실측).
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig
from rig import Shape
import pack78_shapes as P78

# ─────────────────────────────────────────────────────────────────────────────
# 0. 자(尺) — 전부 프로덕션에서 뽑은 값
# ─────────────────────────────────────────────────────────────────────────────
H_IN_R      = rig.BASELINE_TOTAL_H / rig.BASELINE_HEAD_R      # 10.339520
PT_PER_H    = rig.BASELINE_TOTAL_H * rig.PT_PER_UNIT          # 80.18298 pt @배율 1.0
ARM_H       = 0.32972                                          # SceneBootstrapper:257 (상완 .38 + 전완 .37)
SHOULDER_H  = 0.7758
HEAD_C_H    = rig.HEAD_CENTER / rig.BASELINE_TOTAL_H          # 0.903284
HEAD_R_H    = rig.BASELINE_HEAD_R / rig.BASELINE_TOTAL_H      # 0.096716

#: ArcheryRenderer.StrokeWidthRatio = 0.0339f — "캐릭터 몸통 획과 같은 굵기(0.077/2.2747)".
#  프롭 렌더러는 `ArcheryRenderer` 구조를 그대로 베끼라고 인계됐으므로(모션 9절 #7) 이것이 명목 획이다.
WP          = 0.0339                                           # H 배수. 배율 불변(명목).
MIN_PT      = rig.MIN_STROKE_PT                                # StickConfig.MinStrokeScreenPoints = 2.0
THETA_INK   = 0.3476                                           # DESIGN_FAN_MENU_ICONS R26-1
SCALES      = (0.35, 0.50, 0.55, 0.75, 1.00)                   # StickConfig.Min/MaxCharacterScale = 0.35 / 1.00
SHIP_SCALE  = 0.75

#: 실루엣 래칫 — 장비와 같은 상수를 H 로 환산(sectors.SILHOUETTE_RATCHET_R = 0.515796 R).
RATCHET_H   = 1.50 * rig.stroke_in_R(0.75) / H_IN_R            # 0.049886 H
#: 채움 윤곽 펜(규칙 1-C 색면 조건) — AccessoryShapeBuilder.FillOutlineBudgetInHeadRadii = 0.21818 R.
FILL_PEN_H  = 0.2181818 / H_IN_R                               # 0.021102 H


def prop_stroke_pt(scale, floored=True):
    """프롭 획의 실폭(pt). floored=False 가 **오늘의 ArcheryRenderer** (하한 없음),
    True 가 이 문서의 권고(몸/액세서리와 같은 MinStrokeScreenPoints 하한을 공유)."""
    nominal = WP * rig.BASELINE_TOTAL_H * scale * rig.PT_PER_UNIT
    return max(nominal, MIN_PT) if floored else nominal


def body_stroke_pt(scale):
    """몸/액세서리 획(pt). 배율 0.35~1.00 전 구간에서 하한 2.0pt 에 눌려 **상수**다."""
    return max(rig.BASELINE_STROKE_W * scale, MIN_PT / rig.PT_PER_UNIT) * rig.PT_PER_UNIT


def stroke_in_H(scale, floored=True):
    """그 배율에서 획이 **H 배수로 몇인가**. 저배율에서 하한이 걸리면 이 값이 커진다."""
    return prop_stroke_pt(scale, floored) / (PT_PER_H * scale)


def valley_px(center_gap_H, scale, dpi=1.0, floored=True):
    """두 조각의 **중심 간 최소거리**가 center_gap_H 일 때 화면에 남는 알파 0 골(px).
    모형은 내가 발명하지 않는다 — DESIGN_FAN_MENU_ICONS R26-1 §1-3:
        코어 간극 g = d - 획,  화면 골 = (g_pt - 0.5pt) x DPI."""
    g_H = center_gap_H - stroke_in_H(scale, floored)
    return (g_H * PT_PER_H * scale - 0.5) * dpi


# 교정 — 알려진 값으로 먼저 맞춘다(교정이 깨지면 아래 숫자를 전부 폐기한다).
assert abs(H_IN_R - 10.339520) < 1e-5, "H<->R 환산 교정 실패"
assert abs(ARM_H * H_IN_R - 3.40909) < 1e-3, "팔 길이 교정 실패(MOTION_SPEC 23-2 = 3.40909 R)"
assert abs(2 * HEAD_R_H - 0.193433) < 1e-6, "머리 지름 교정 실패"
assert abs(PT_PER_H * 0.75 * 0.88 - 52.92) < 0.02, "필요폭 교정 실패(모션 4-3절 광부 52.9pt @0.75)"
assert abs(prop_stroke_pt(0.75) - 2.039) < 0.001, "프롭 획 교정 실패(ArcheryRenderer 0.0339 x H)"
assert abs(body_stroke_pt(0.75) - 2.000) < 1e-9, "몸 획 교정 실패(MinStrokeScreenPoints)"


# ─────────────────────────────────────────────────────────────────────────────
# 1. 기하 도구
# ─────────────────────────────────────────────────────────────────────────────
def ell_arc(cx, cy, rx, ry, d0, d1, n):
    return [(cx + math.cos(math.radians(d0 + (d1 - d0) * i / (n - 1))) * rx,
             cy + math.sin(math.radians(d0 + (d1 - d0) * i / (n - 1))) * ry) for i in range(n)]


def segs(sh):
    p = sh.pts
    n = len(p)
    rng = range(n) if sh.loop else range(n - 1)
    return [(p[i], p[(i + 1) % n]) for i in rng]


def _pt_seg(p, a, b):
    vx, vy = b[0] - a[0], b[1] - a[1]
    L2 = vx * vx + vy * vy
    t = 0.0 if L2 < 1e-15 else max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / L2))
    return math.hypot(p[0] - (a[0] + vx * t), p[1] - (a[1] + vy * t))


def _seg_seg(a, b, c, d):
    if rig.seg_int(a, b, c, d):
        return 0.0
    return min(_pt_seg(a, c, d), _pt_seg(b, c, d), _pt_seg(c, a, b), _pt_seg(d, a, b))


def shape_dist(s1, s2):
    """두 조각 사이의 진짜 2D 최소거리(H)와 그 자리."""
    best, where = 9e9, None
    for a, b in segs(s1):
        for c, d in segs(s2):
            v = _seg_seg(a, b, c, d)
            if v < best:
                best, where = v, (a, b, c, d)
    return best, where


def _collinear_overlap(a, b, c, d, eps=1e-9):
    """두 «거의 공선» 변이 축 위에서 몇이나 겹치는가(H). 끝끼리 만나기만 하면 0 이다."""
    ux, uy = b[0] - a[0], b[1] - a[1]
    L = math.hypot(ux, uy)
    if L < 1e-12:
        return 0.0
    ux, uy = ux / L, uy / L
    t = sorted(((c[0] - a[0]) * ux + (c[1] - a[1]) * uy, (d[0] - a[0]) * ux + (d[1] - a[1]) * uy))
    lo, hi = max(0.0, t[0]), min(L, t[1])
    return max(0.0, hi - lo)


def junction_angle(s1, s2, tol=1e-6):
    """접합(거리 0)한 두 조각이 **몇 도로 갈라지는가** — 최소값(도). 접합 없으면 None.

    ★ 공선인데 «끝끼리 이어지기만» 하는 쌍(겹침 0)은 제외한다 — 그건 한 물건의 이어진 선이고
      (책상 앞턱 + 상판), 잡아야 하는 것은 **같은 자리에 두 번 그린 선**(겹침 > 0.5 W_P)이다."""
    worst = None
    for a, b in segs(s1):
        for c, d in segs(s2):
            if _seg_seg(a, b, c, d) > tol:
                continue
            u = (b[0] - a[0], b[1] - a[1])
            v = (d[0] - c[0], d[1] - c[1])
            nu, nv = math.hypot(*u), math.hypot(*v)
            if nu < 1e-12 or nv < 1e-12:
                continue
            cs = max(-1.0, min(1.0, (u[0] * v[0] + u[1] * v[1]) / (nu * nv)))
            ang = min(math.degrees(math.acos(cs)), 180.0 - math.degrees(math.acos(cs)))
            if ang < 1.0 and _collinear_overlap(a, b, c, d) <= 0.5 * WP:
                continue                      # 이어짐(겹침 0) — 중복 잉크가 아니다
            worst = ang if worst is None else min(worst, ang)
    return worst


def span_of(sh):
    x0, y0, x1, y1 = rig.bounds(sh.pts)
    return max(x1 - x0, y1 - y0), (x1 - x0), (y1 - y0)


def corner_min_edge(sh):
    p, n = sh.pts, len(sh.pts)
    if n < 2:
        return 9e9, None
    corner = [False] * n
    rng = range(n) if sh.loop else range(1, n - 1)
    for i in rng:
        corner[i] = rig.turn_deg(p[(i - 1) % n], p[i], p[(i + 1) % n]) >= rig.CORNER_DEG
    worst, where = 9e9, None
    for i in (range(n) if sh.loop else range(n - 1)):
        j = (i + 1) % n
        if not (corner[i] and corner[j]):
            continue
        L = math.dist(p[i], p[j])
        if L < worst:
            worst, where = L, (i, j)
    return worst, where


def true_min_edge(sh):
    return min(math.dist(a, b) for a, b in segs(sh))


# ─────────────────────────────────────────────────────────────────────────────
# 2. 프롭 4종 — 조각마다 «몇 단계에서 들어오는가»를 달고 있다(가산성 E-2가 구조적으로 참).
# ─────────────────────────────────────────────────────────────────────────────
def S(name, pts, loop=False, filled=False, tone=0, stage=0):
    sh = Shape(name, pts, loop, filled, tone)
    sh.stage = stage
    return sh


# ══════════════════════════════════════════════════════════════════════════
# C1 사이버 「홀로 콘솔 스탠드」  costume.cyber
#    모티프 = 모서리 잘린 6~8각(커넥터 패드).  PACK_THEME_SPEC §4-1 「사이버 아포칼립스」 잎
# ══════════════════════════════════════════════════════════════════════════
def cyber():
    # ★ 이 함수는 **R28 설계본의 기록**이다. `BasePad` 는 R31(2026-09-08)에서 교체됐고
    #   출하 좌표는 `r30_costume_assets.cyber()` 의 오버라이드에 있다 — 검산은 `r31_fillsolid.py`.
    #   여기를 갈아 끼우지 않는 이유는 `r28_props.out.txt` 가 그 시점의 기록으로 인용되기 때문이다.
    return [
        # ---- S0 : 아직 아무것도 안 띄운 빈 콘솔 (3선)
        S("Mast", [(0.5400, 0.0900), (0.5400, 0.6200)], stage=0),
        S("BasePad", [(0.4400, 0.0300), (0.4700, 0.0000), (0.5900, 0.0000), (0.6200, 0.0300),
                      (0.6200, 0.0900), (0.5900, 0.1200), (0.4700, 0.1200), (0.4400, 0.0900)],
          loop=True, filled=True, tone=1, stage=0),
        S("PanelRim", [(0.3100, 0.6200), (0.7200, 0.6200), (0.7700, 0.6700),
                       (0.7700, 0.9500), (0.7200, 1.0000), (0.3100, 1.0000)], loop=True, stage=0),
        # ---- S1 : 화면이 켜진다 (+3 = 6선)   행 간격 0.095 H = 2.80 W_P
        S("ScanTop", [(0.4100, 0.9050), (0.6700, 0.9050)], stage=1),
        S("ScanMid", [(0.4100, 0.8100), (0.6300, 0.8100)], stage=1),
        S("ScanLow", [(0.4100, 0.7150), (0.6700, 0.7150)], stage=1),
        # ---- S2 : 작업대가 붙는다 (+2 = 8선)
        S("Tray", [(0.5400, 0.4600), (0.4400, 0.4600), (0.4400, 0.3400), (0.5400, 0.3400)], stage=2),
        S("SidePost", [(0.7200, 0.0000), (0.7200, 0.6200)], stage=2),
        # ---- S3 : 배선이 산다 (+3 = 11선)
        S("Crossbar", [(0.3500, 0.2200), (0.7200, 0.2200)], stage=3),
        S("Cable", [(0.6300, 0.2200), (0.6300, 0.4400), (0.7200, 0.5200)], stage=3),
        S("RiserLeft", [(0.3500, 0.2200), (0.3500, 0.6200)], stage=3),
    ]


# ══════════════════════════════════════════════════════════════════════════
# C2 오피스 「스탠딩 책상 + 칸막이」  costume.office   모티프 = 납작한 4각 판
# ══════════════════════════════════════════════════════════════════════════
def office():
    return [
        # ---- S0 (4선)
        S("Partition", [(0.7200, 0.1600), (1.0000, 0.1600), (1.0000, 0.8800), (0.7200, 0.8800)],
          loop=True, stage=0),
        S("DeskTop", [(0.3850, 0.6200), (0.7200, 0.6200)], stage=0),
        S("DeskLeg", [(0.3200, 0.0000), (0.3200, 0.5520)], stage=0),
        S("DeskLip", [(0.2800, 0.5520), (0.3850, 0.5520), (0.3850, 0.6200), (0.2800, 0.6200)],
          loop=True, filled=True, tone=1, stage=0),
        # ---- S1 : 스탠드가 켜지고 칸막이에 결이 생긴다 (+6 = 10선)
        S("LampPost", [(0.6200, 0.6200), (0.6200, 0.9000)], stage=1),
        S("LampHood", [(0.6200, 0.9000), (0.4600, 0.8200)], stage=1),
        S("BeamNear", [(0.4600, 0.8200), (0.4200, 0.7200)], stage=1),
        S("BeamFar", [(0.5400, 0.8600), (0.5400, 0.7600)], stage=1),
        S("GrooveA", [(0.8000, 0.2400), (0.8000, 0.5600)], stage=1),
        S("GrooveB", [(0.9000, 0.2400), (0.9000, 0.5600)], stage=1),
        # ---- S2 : 일감이 올라온다 (+4 = 14선)
        S("Shelf", [(0.3200, 0.3000), (0.7200, 0.3000)], stage=2),
        S("Box", [(0.4000, 0.3000), (0.4000, 0.3800), (0.4800, 0.3800), (0.4800, 0.3000)], stage=2),
        S("Book", [(0.5600, 0.3000), (0.5600, 0.4000), (0.6400, 0.4000), (0.6400, 0.3000)], stage=2),
        S("Memo", [(0.8000, 0.7000), (0.8800, 0.7000), (0.8800, 0.7800), (0.8000, 0.7800)],
          loop=True, stage=2),
        # ---- S3 : 자리가 사람 사는 곳이 된다 (+4 = 18선)
        S("TrussA", [(0.7200, 0.1600), (1.0000, 0.4400)], stage=3),
        S("TrussB", [(0.7200, 0.4400), (1.0000, 0.1600)], stage=3),
        S("FloorLine", [(0.2800, 0.0000), (0.7200, 0.0000)], stage=3),
        S("FloorBox", [(0.4400, 0.0000), (0.4400, 0.1200), (0.5600, 0.1200), (0.5600, 0.0000)], stage=3),
    ]


# ══════════════════════════════════════════════════════════════════════════
# C3 광부 「광맥 벽」  costume.miner   모티프 = 쐐기(taper wedge, n=4, tau <= 0.55)
# ══════════════════════════════════════════════════════════════════════════
def miner():
    # ★ 위 cyber() 와 같다 — `Vein1` 은 R31 에서 교체됐다(뿌리폭 0.12 -> 0.078). 여기는 R28 기록.
    vein1 = P78.wedge((0.4300, 0.7400), (0.5300, 0.5800), 0.1200, 0.0500)   # tau = 0.417
    return [
        # ---- S0 : 아직 못 찾았다 — 그냥 바위 + 광맥 한 줄 (3선)
        #  ★ (0.28,0.65) 와 (0.28,0.85) 는 **꼭짓점 자체**다 — 모션이 못박은 두 작업점.
        S("RockFace", [(0.2800, 0.0000), (0.3000, 0.2000), (0.2800, 0.4400), (0.2800, 0.6500),
                       (0.3200, 0.7600), (0.2800, 0.8500), (0.3800, 0.9400), (0.6000, 0.9000),
                       (0.7600, 0.5600), (0.7600, 0.0000)], stage=0),
        S("GroundSeam", [(0.1000, 0.0000), (0.8000, 0.0000)], stage=0),
        S("Vein1", vein1, loop=True, filled=True, tone=1, stage=0),
        # ---- S1 : 갱이 받쳐지고 광맥이 갈라진다 (+5 = 8선)  ★ 갱목 3선은 서로 «붙어» 있어 골을 안 먹는다
        S("TimberL", [(0.3800, 0.0000), (0.3800, 0.4200)], stage=1),
        S("TimberR", [(0.6800, 0.0000), (0.6800, 0.4200)], stage=1),
        S("TimberCap", [(0.3800, 0.4200), (0.6800, 0.4200)], stage=1),
        S("Chip1", [(0.1000, 0.0000), (0.1350, 0.0520), (0.1700, 0.0000)], stage=1),
        S("Vein2", [(0.6100, 0.6700), (0.6500, 0.5900), (0.6300, 0.5300)], stage=1),
        # ---- S2 : 파 들어간 자국 (+3 = 11선)
        S("Notch", [(0.4600, 0.0000), (0.4600, 0.1400), (0.6000, 0.1400), (0.6000, 0.0000)], stage=2),
        S("BraceL", [(0.3800, 0.3400), (0.4600, 0.4200)], stage=2),
        S("BraceR", [(0.6800, 0.3400), (0.6000, 0.4200)], stage=2),
        # ---- S3 : 갱이 제대로 된 일터가 된다 (+2 = 13선)
        #  ★ 14번째 조각을 놓을 자리가 벽 안에 **없다** — 문서 §4-3-C 의 「선 수 상한」이 이것이다.
        S("CapTie", [(0.3800, 0.2600), (0.6800, 0.2600)], stage=3),
        S("Crack1", [(0.4150, 0.8400), (0.4750, 0.8400)], stage=3),
    ]


# ══════════════════════════════════════════════════════════════════════════
# C4 대마법사 「마법진 + 지팡이 거치대」  costume.archmage   모티프 = 초승달
# ══════════════════════════════════════════════════════════════════════════
def archmage():
    # ★ 마법진은 «지면에 눕힌 타원»이 아니라 **지면 위로 솟은 반타원 3겹**이다(§5-4-A).
    #   눕힌 타원(ry 0.085)으로는 동심 3겹의 정점 간극이 0.033 H = 0.97 W_P 라 한 줄로 합쳐진다.
    CX, CY = 0.5800, 0.0200
    RX, RY = 0.3000, 0.2400
    outer = ell_arc(CX, CY, RX, RY, 180.0, 0.0, 15)
    mid = ell_arc(CX, CY, 0.2100, 0.1400, 180.0, 0.0, 13)
    inner = ell_arc(CX, CY, 0.1300, 0.0650, 180.0, 0.0, 9)
    moon = P78.crescent(1.0400, 0.9600, 0.0950, 0.0680, 0.0420, 90.0)

    def rune(deg, f0=0.95, f1=1.28):
        a = math.radians(deg)
        return [(CX + math.cos(a) * RX * f0, CY + math.sin(a) * RY * f0),
                (CX + math.cos(a) * RX * f1, CY + math.sin(a) * RY * f1)]

    def ring(y):
        return rig.poly(1.0400, y, 0.0500, 12)

    out = [
        # ---- S0 : 룬 없는 맨 원 + 거치대에 선 지팡이 (5선)
        S("OuterArc", outer, stage=0),
        S("Staff", [(1.0400, 0.0200), (1.0400, 0.9000)], stage=0),
        S("CradleA", [(0.9600, 0.0200), (1.1200, 0.2600)], stage=0),
        S("CradleB", [(1.1200, 0.0200), (0.9600, 0.2600)], stage=0),
        S("StaffMoon", moon, loop=True, filled=True, tone=1, stage=0),
        # ---- S1 (+4 = 9선)
        S("MidArc", mid, stage=1),
        S("RuneA1", rune(150.0), stage=1),
        S("RuneA2", rune(30.0), stage=1),
        S("Ring1", ring(0.7400), loop=True, stage=1),
        # ---- S2 (+4 = 13선)
        S("InnerArc", inner, stage=2),
        S("RuneA3", rune(115.0), stage=2),
        S("RuneA4", rune(65.0), stage=2),
        S("Ring2", ring(0.5600), loop=True, stage=2),
        # ---- S3 (+4 = 17선)
        S("RuneB1", rune(132.0), stage=3),
        S("RuneB2", rune(48.0), stage=3),
        S("Ring3", ring(0.3800), loop=True, stage=3),
        S("Axis", [(CX, CY), (CX, CY + RY * 1.25)], stage=3),
    ]
    return out


# ─────────────────────────────────────────────────────────────────────────────
# 3. 프롭 등록 — 선언(모션 문서가 못박은 값) vs 실측
# ─────────────────────────────────────────────────────────────────────────────
PROPS = [
    # key, 표시명, 조형 fn, 단계별 선 수(선언), 작업점, 지향(대마법사 전용), 모티프 잎
    ("cyber", "C1 사이버 홀로콘솔", cyber, (3, 6, 8, 11),
     [(0.31, 0.82), (0.31, 0.72)], [], "사이버 아포칼립스"),
    ("office", "C2 오피스 스탠딩책상", office, (4, 10, 14, 18),
     [(0.28, 0.62)], [], "오피스 워커"),
    ("miner", "C3 광부 광맥벽", miner, (3, 8, 11, 13),
     [(0.28, 0.85), (0.28, 0.65)], [], "광부"),
    ("archmage", "C4 대마법사 마법진", archmage, (5, 9, 13, 17),
     [], [0.6423, 0.8234], "대마법사"),
]

#: 광부 K2 전용 `propFrame` 1 — 타격 섬광. 좌표는 접촉점 (0.28, 0.85) H 기준의 방사 획 3개.
def miner_flash():
    """★ 접촉점 (0.28, 0.85) H = RockFace 의 **꼭짓점 그 자체**에서 뻗는 방사 3획.
    세 방향은 취향이 아니라 남은 창이다 — 그 꼭짓점의 벽선 두 변이 42도/114도(선각)라,
    두 변과 각각 30도 이상 벌어지는 선각 창은 (144,180) · (0,12) · (72,84) 뿐이고
    150 / 190 / 258 도가 그 셋을 하나씩 쓴다."""
    cx, cy = 0.2800, 0.8500
    out = []
    for i, (deg, L) in enumerate(((150.0, 0.0700), (190.0, 0.0620), (258.0, 0.0650))):
        a = math.radians(deg)
        out.append(S("Flash%d" % (i + 1),
                     [(cx, cy), (cx + math.cos(a) * L, cy + math.sin(a) * L)], stage=0))
    return out


_fail = []


def bad(m):
    _fail.append(m)
    print("  x  " + m)


def ok(m):
    print("  OK " + m)


def stage_set(shapes, st):
    return [s for s in shapes if s.stage <= st]


# ─────────────────────────────────────────────────────────────────────────────
# 4. 게이트
# ─────────────────────────────────────────────────────────────────────────────
def gate(control=False):
    print("=" * 108)
    print("코스튬 소환 오브젝트(프롭) 4종 x 진화 4상태 — 전수 게이트")
    print("=" * 108)
    print("  1 H = %.6f R = %.3f pt @0.75 = %.3f pt @1.00" % (H_IN_R, PT_PER_H * 0.75, PT_PER_H))
    print("  프롭 명목 획 W_P = %.4f H (ArcheryRenderer.StrokeWidthRatio)" % WP)
    print("  획 실폭(pt):  " + "  ".join(
        "@%.2f 몸 %.3f / 프롭 %.3f(바닥) %.3f(무바닥)"
        % (s, body_stroke_pt(s), prop_stroke_pt(s, True), prop_stroke_pt(s, False)) for s in SCALES))
    print("  실루엣 래칫 = %.6f H (= 1.50 x W@0.75, 장비와 같은 상수)" % RATCHET_H)

    allsets = {}
    for key, label, fn, counts, work, aim, leaf in PROPS:
        shapes = fn()
        if control and key == "cyber":
            shapes = [S("A", [(0.30, 0.30), (0.312, 0.30), (0.312, 0.312), (0.30, 0.312)],
                        loop=True, filled=True, tone=1, stage=0),
                      S("B", [(0.30, 0.33), (0.40, 0.33)], stage=0),
                      S("C", [(0.30, 0.35), (0.40, 0.35)], stage=0),
                      S("D", [(0.30, 0.36), (0.40, 0.36)], tone=1, stage=1)]
            counts = (3, 4, 4, 4)
        allsets[key] = shapes
        print("\n" + "=" * 108)
        print("  %s   (%s)" % (label, key))
        print("=" * 108)

        # ---- (1) 단계 구성 · 가산성 · 보조색
        print("\n  -- (1) 단계 구성 · 가산성(E-2) · 보조색 --")
        for st in range(4):
            cur = stage_set(shapes, st)
            n = len(cur)
            if n != counts[st]:
                bad("%s S%d 선 수 %d (선언 %d)" % (key, st, n, counts[st]))
            acc = [s for s in cur if s.tone == 1]
            if len(acc) != 1:
                bad("%s S%d 보조색 %d개 (정확히 1)" % (key, st, len(acc)))
            if st > 0:
                prev = set(s.name for s in stage_set(shapes, st - 1))
                if not prev <= set(s.name for s in cur):
                    bad("%s S%d 가 S%d 를 포함하지 않는다(E-2 가산성 위반)" % (key, st, st - 1))
        acc_names = set(s.name for s in shapes if s.tone == 1)
        if len(acc_names) == 1:
            ok("%-9s S0..S3 = %s 선 · 보조색 조각 = '%s' 하나가 S0부터 끝까지 (E-2 가산 · PR-11)"
               % (key, "/".join(str(len(stage_set(shapes, s))) for s in range(4)), list(acc_names)[0]))

        # ---- (2) 조각 단위 규칙
        print("\n  -- (2) PR-1 꺾임변 >= 1.0 W_P · PR-2 잉크 사각형 >= 1.5 W_P · PR-3 자기교차 --")
        worst_edge = (9e9, None)
        for s in shapes:
            ce, _ = corner_min_edge(s)
            if ce < 9e8 and ce < WP:
                bad("%s '%s' 꺾임-꺾임 변 %.4f H = %.2f W_P < 1.00" % (key, s.name, ce, ce / WP))
            te = true_min_edge(s)
            if te < worst_edge[0]:
                worst_edge = (te, s.name)
            sp, w_, h_ = span_of(s)
            if sp < 1.5 * WP:
                bad("%s '%s' 잉크 사각형 span %.4f H = %.2f W_P < 1.50" % (key, s.name, sp, sp / WP))
            if s.filled and min(w_, h_) < 1.5 * WP:
                bad("%s '%s' 채움 잉크 사각형 %.4f x %.4f, 짧은 변 %.2f W_P < 1.50"
                    % (key, s.name, w_, h_, min(w_, h_) / WP))
            if s.loop and rig.self_intersects(s.pts):
                bad("%s '%s' 자기교차" % (key, s.name))
        ok("%-9s 조각 %d개 · 실제 최단변 %.4f H = %.2f W_P ('%s')"
           % (key, len(shapes), worst_edge[0], worst_edge[0] / WP, worst_edge[1]))
        # PR-2b 색면 조건 — 채움이 «면»으로 읽히려면 내접원 반지름 >= 채움 윤곽 펜(0.21818 R)
        for s in shapes:
            if not s.filled:
                continue
            r = P78.rho_max([(q[0] * H_IN_R, q[1] * H_IN_R) for q in s.pts], grid=0.01) / H_IN_R
            if r < FILL_PEN_H:
                bad("%s '%s' 색면 조건 rho_max %.4f H < 채움 윤곽 펜 %.4f H" % (key, s.name, r, FILL_PEN_H))
            else:
                ok("%-9s '%s' 채움 rho_max %.4f H = %.2f 펜 = %.2f W_P (%.2f pt @0.75)"
                   % (key, s.name, r, r / FILL_PEN_H, r / WP, r * PT_PER_H * 0.75))

        # ---- (3) PR-4 금지대 (겹치거나, 코어 간극 >= 1.0 W_P) + PR-4b 접합각
        print("\n  -- (3) PR-4 금지대: 두 조각은 ①접함(d=0) 또는 ②중심거리 >= 2.0 W_P (= %.4f H) --" % (2 * WP))
        for st in range(4):
            cur = stage_set(shapes, st)
            worst = (9e9, None)
            for i in range(len(cur)):
                for j in range(i + 1, len(cur)):
                    d, _ = shape_dist(cur[i], cur[j])
                    if d <= 1e-9:
                        ja = junction_angle(cur[i], cur[j])
                        if ja is not None and ja < 30.0:
                            bad("%s S%d 접합 '%s'x'%s' 갈라짐 %.1f도 < 30 (PR-4b)"
                                % (key, st, cur[i].name, cur[j].name, ja))
                        continue
                    if d < worst[0]:
                        worst = (d, (cur[i].name, cur[j].name))
            if worst[0] < 2 * WP:
                bad("%s S%d 금지대: '%s'x'%s' 중심거리 %.4f H = %.2f W_P < 2.00 (골 %.2f px @0.75/1x)"
                    % (key, st, worst[1][0], worst[1][1], worst[0], worst[0] / WP,
                       valley_px(worst[0], SHIP_SCALE)))
            else:
                ok("%-9s S%-2d 최악쌍 '%s'x'%s' %.4f H = %.2f W_P · 골 %.2f px @0.75/1x · %.2f px @1.00/1x"
                   % (key, st, worst[1][0], worst[1][1], worst[0], worst[0] / WP,
                      valley_px(worst[0], 0.75), valley_px(worst[0], 1.00)))

        # ---- (4) 배율 하한 s_min — 어느 배율부터 화면에서 한 줄로 합쳐지는가
        print("\n  -- (4) PR-14 배율 하한: 골이 1.0 px 아래로 내려가는 배율 (1x = Windows 100%) --")
        for st in range(4):
            cur = stage_set(shapes, st)
            worst = 9e9
            for i in range(len(cur)):
                for j in range(i + 1, len(cur)):
                    d, _ = shape_dist(cur[i], cur[j])
                    if d > 1e-9:
                        worst = min(worst, d)
            smin = None
            s = 0.35
            while s <= 1.0005:
                if valley_px(worst, s, 1.0) >= 1.0:
                    smin = s
                    break
                s += 0.005
            row = "  ".join("%.2f:%s%.2fpx" % (sc, "OK " if valley_px(worst, sc, 1.0) >= 1.0 else "x ",
                                               valley_px(worst, sc, 1.0)) for sc in SCALES)
            print("     S%-2d 최소 중심거리 %.4f H | %s | 하한 s_min = %s"
                  % (st, worst, row, "%.3f" % smin if smin else "없음(전 배율 미달)"))
            if smin is None or smin > SHIP_SCALE:
                bad("%s S%d 는 출하 배율 0.75 에서 이미 무너진다(s_min=%s)"
                    % (key, st, "%.3f" % smin if smin else "없음"))

        # ---- (5) 배치 봉투 — 모션 문서 선언값과 대조
        print("\n  -- (5) 배치 봉투(모션 4-3절 선언과 대조) --")
        pts = [q for s in shapes for q in s.pts]
        x0, y0, x1, y1 = rig.bounds(pts)
        need = x1 + 0.10
        ok("%-9s 근단 %+.4f / 원단 %+.4f / 최저 %+.4f / 높이 %+.4f H · 필요폭 %.4f H = %.1f pt @0.75"
           % (key, x0, x1, y0, y1, need, need * PT_PER_H * 0.75))
        if y0 < -1e-9:
            print("     ** 접지선 아래 잉크 %.4f H (= %.2f pt @0.75) — 발판 표면 밖" % (-y0, -y0 * PT_PER_H * 0.75))

        # ---- (6) PR-8 작업점 접지 — 손이 닿는 자리에 실제로 잉크가 있는가
        print("\n  -- (6) PR-8 작업점 접지 (모션이 못박은 프롭지향 키포즈의 접촉점) --")
        for wp in work:
            d = min(min(_pt_seg(wp, a, b) for a, b in segs(s)) for s in shapes)
            hit = min(((min(_pt_seg(wp, a, b) for a, b in segs(s)), s.name) for s in shapes))
            if d > 0.5 * WP:
                bad("%s 작업점 (%.2f,%.2f) 에서 가장 가까운 잉크가 %.4f H = %.2f W_P 떨어져 있다(손이 허공)"
                    % (key, wp[0], wp[1], d, d / WP))
            else:
                ok("%-9s 작업점 (%.2f, %.2f) -> '%s' 까지 %.4f H = %.2f W_P (%.2f pt @0.75)"
                   % (key, wp[0], wp[1], hit[1], d, d / WP, d * PT_PER_H * 0.75))
            # 어깨 도달 원 재확인(모션 2-1절)
            r = math.hypot(wp[0], wp[1] - SHOULDER_H)
            if r > ARM_H:
                bad("%s 작업점 (%.2f,%.2f) 이 도달 원 밖 %.4f > %.4f H" % (key, wp[0], wp[1], r, ARM_H))
        for ax in aim:
            inside = x0 <= ax <= x1
            d = min(min(_pt_seg((ax, 0.0), a, b) for a, b in segs(s)) for s in shapes)
            if not inside:
                bad("%s 지향 착점 x=%.4f 이 프롭 x 범위 [%.3f,%.3f] 밖" % (key, ax, x0, x1))
            else:
                ok("%-9s 지향 착점 x=%.4f — 프롭 x 범위 안 · 지면(y=0)에서 가장 가까운 잉크 %.4f H = %.2f W_P"
                   % (key, ax, d, d / WP))

        # ---- (7) 모티프 분류
        print("\n  -- (7) 모티프 잎 (PACK_THEME_SPEC §4-1 결정나무 + 신규 두 잎) --")
        acc = [s for s in shapes if s.tone == 1]
        if acc:
            got = P78.classify(acc[0].pts)
            n_, a_, c_, s_, t_ = P78.measure_row(acc[0].pts)
            if got != leaf:
                bad("%s 보조색 조각 '%s' 이 「%s」로 분류됨(기대 %s)" % (key, acc[0].name, got, leaf))
            else:
                ok("%-9s '%s' n=%2d 종횡비 %.2f 결손 %.4f 첨점 %d 테이퍼 %s -> 「%s」"
                   % (key, acc[0].name, n_, a_, c_, s_, ("-" if t_ is None else "%.3f" % t_), got))

        # ---- (8) 캐릭터와의 겹침 (BACK 6종 · PET 6종) — 모션 13-3 미측정 항목
        print("\n  -- (8) 캐릭터 장비/펫과의 겹침 (모션 13-3 이 나에게 넘긴 미측정 항목) --")
        import items as _it
        worst = (9e9, None)
        for bn, bfn in _it.BACK.items():
            bsh = bfn() if callable(bfn) else bfn
            for b in bsh:
                bH = Shape(b.name, [(q[0] / H_IN_R, (q[1] + rig.HEAD_CENTER / rig.BASELINE_HEAD_R) / H_IN_R)
                                    for q in b.pts], b.loop, b.filled, b.tone)
                for s in shapes:
                    d, _ = shape_dist(s, bH)
                    if d < worst[0]:
                        worst = (d, (bn, b.name, s.name))
        tag = "겹침" if worst[0] <= 1e-9 else ("금지대" if worst[0] < 2 * WP else "안전")
        line = ("%-9s BACK 최악 '%s/%s' x '%s' %.4f H = %.2f W_P · 골 %.2f px @0.75/1x [%s]"
                % (key, worst[1][0], worst[1][1], worst[1][2], worst[0], worst[0] / WP,
                   valley_px(worst[0], 0.75), tag))
        (ok if worst[0] >= 2 * WP else print)(line if worst[0] >= 2 * WP else "  ** " + line)
        # PET — 종이비행기만 궤도가 앞으로 나온다
        plane_x = 6.00 / H_IN_R
        plane_cy = (rig.HEAD_CENTER / rig.BASELINE_HEAD_R - 1.75) / H_IN_R
        plane_hy = 3.35 / H_IN_R
        trail = {"공": 0.55, "리틀스틱메이트": 0.75, "달팽이": 0.95, "풍선": 3.5 / H_IN_R}
        if x0 <= plane_x and y0 <= plane_cy + plane_hy and y1 >= plane_cy - plane_hy:
            print("  ** %-9s PET 종이비행기 궤도(x <= %+.4f H · y %.4f~%.4f H)가 프롭 영역과 **겹친다**"
                  % (key, plane_x, plane_cy - plane_hy, plane_cy + plane_hy))
        ok("%-9s PET 나머지 5종은 전부 **뒤로** 따라온다(공 %.2f H · 리틀 %.2f H · 달팽이 %.2f H · 풍선 %.4f H 뒤) -> 겹침 0"
           % (key, trail["공"], trail["리틀스틱메이트"], trail["달팽이"], trail["풍선"]))

    # ---- (9) 프롭 4종 쌍별 실루엣 + 기존 월드 오브젝트(과녁)와의 구별
    print("\n" + "=" * 108)
    print("  (9) 쌍별 실루엣 — 프롭 4종 서로 + 기존 과녁(ArcheryRenderer)")
    print("=" * 108)
    prof = {}
    for key, label, fn, counts, work, aim, leaf in PROPS:
        sh = allsets[key]
        pts = [q for s in sh for q in s.pts]
        x0, y0, x1, y1 = rig.bounds(pts)
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        prof[key] = rig.profile([Shape(s.name, [(q[0] - cx, q[1] - cy) for q in s.pts], s.loop)
                                 for s in sh], 0.0)
    # 과녁: 링 3 + 불즈아이 + 받침(ArcheryRenderer 실측 비율, TargetRadius = 0.40 H)
    tr = 0.40
    tcy = 1.0 - tr
    tgt = [Shape("RingOuter", rig.poly(0, 0, tr, 32), True),
           Shape("RingMid", rig.poly(0, 0, tr * 0.68, 32), True),
           Shape("RingInner", rig.poly(0, 0, tr * 0.36, 32), True),
           Shape("Stand", [(-0.11, -tcy), (0.0, 0.0), (0.11, -tcy)], False)]
    prof["과녁"] = rig.profile(tgt, 0.0)
    keys = list(prof.keys())
    for i in range(len(keys)):
        for j in range(i + 1, len(keys)):
            d = rig.max_delta(prof[keys[i]], prof[keys[j]])
            if d < RATCHET_H:
                bad("실루엣 %s x %s = %.4f H < 래칫 %.4f H" % (keys[i], keys[j], d, RATCHET_H))
            else:
                ok("%-9s x %-9s %.4f H = %.2f W_P = 래칫의 %.1f배"
                   % (keys[i], keys[j], d, d / WP, d / RATCHET_H))

    # ---- (10) 광부 propFrame 1 (타격 섬광)
    print("\n" + "=" * 108)
    print("  (10) 광부 `propFrame` 1 — 타격 섬광 3획 (모션 5-3절 · V15)")
    print("=" * 108)
    fl = miner_flash()
    base = allsets["miner"]
    for s in fl:
        sp, _, _ = span_of(s)
        if sp < 1.5 * WP:
            bad("섬광 '%s' 길이 %.4f H = %.2f W_P < 1.50 (모션 초안 0.04 H = %.2f W_P 는 이 하한 아래다)"
                % (s.name, sp, sp / WP, 0.04 / WP))
        else:
            ok("섬광 %-7s 길이 %.4f H = %.2f W_P = %.2f pt @0.75" % (s.name, sp, sp / WP, sp * PT_PER_H * 0.75))
    for i in range(len(fl)):
        for j in range(i + 1, len(fl)):
            d, _ = shape_dist(fl[i], fl[j])
            if d > 1e-9 and d < 2 * WP:
                bad("섬광 '%s'x'%s' 중심거리 %.4f H = %.2f W_P — 금지대"
                    % (fl[i].name, fl[j].name, d, d / WP))
            else:
                ok("섬광 %sx%s %.4f H = %.2f W_P" % (fl[i].name, fl[j].name, d, d / WP))
    worst = (9e9, None)
    for s in fl:
        for b in base:
            d, _ = shape_dist(s, b)
            if d < worst[0]:
                worst = (d, (s.name, b.name))
    if worst[0] > 1e-9 and worst[0] < 2 * WP:
        bad("섬광 '%s' x 벽 '%s' %.4f H = %.2f W_P — 금지대" % (worst[1][0], worst[1][1], worst[0], worst[0] / WP))
    else:
        ok("섬광 x 벽 최악 '%s'x'%s' %.4f H = %.2f W_P" % (worst[1][0], worst[1][1], worst[0], worst[0] / WP))

    # ---- (11) 잉크 문턱 — 1x(Windows 100%) 최악 위상
    print("\n" + "=" * 108)
    print("  (11) 잉크 문턱 — 프롭 획이 Windows 100%%(1x) 최악 위상에서 살아남는가 (theta_ink=%.4f)" % THETA_INK)
    print("=" * 108)
    for s in SCALES:
        for floored, tagf in ((False, "오늘(ArcheryRenderer, 하한 없음)"), (True, "권고(MinStrokeScreenPoints 공유)")):
            t = prop_stroke_pt(s, floored)
            a1 = max(0.0, min(1.0, t * 1.0 - 0.5))
            a2 = max(0.0, min(1.0, t * 2.0 - 0.5))
            mark = "  " if a1 >= THETA_INK else "**"
            print("   %s 배율 %.2f · %-38s 획 %.3f pt · 1x 알파 %.3f · 2x 알파 %.3f · 몸 획 대비 %.3f배"
                  % (mark, s, tagf, t, a1, a2, t / body_stroke_pt(s)))

    print("\n" + "=" * 108)
    print("결과: %s" % ("전수 통과 (위반 0건)" if not _fail else "위반 %d건" % len(_fail)))
    return len(_fail)


def dump():
    out = []
    out.append("코스튬 소환 오브젝트(프롭) 좌표 전문 — 단위 H(전신 신장) · 원점 = 캐릭터 루트 x / 접지선")
    out.append("1 H = %.6f R = %.3f pt @0.75.  프롭 명목 획 W_P = %.4f H = %.3f pt @0.75"
               % (H_IN_R, PT_PER_H * 0.75, WP, prop_stroke_pt(0.75)))
    for key, label, fn, counts, work, aim, leaf in PROPS:
        out.append("")
        out.append("#" * 100)
        out.append("## %s   (costume.%s)   S0..S3 = %s 선" % (label, key, "/".join(map(str, counts))))
        out.append("#" * 100)
        for s in fn():
            out.append("")
            out.append("  [S%d] %-12s loop=%-5s filled=%-5s tone=%d n=%d"
                       % (s.stage, s.name, s.loop, s.filled, s.tone, len(s.pts)))
            for x, y in s.pts:
                out.append("        (%+.4f, %+.4f)" % (x, y))
    out.append("")
    out.append("#" * 100)
    out.append("## 광부 propFrame 1 — 타격 섬광 3획 (K2 스텝에서만 enabled)")
    out.append("#" * 100)
    for s in miner_flash():
        out.append("  %-8s n=%d  " % (s.name, len(s.pts)) + " ".join("(%+.4f,%+.4f)" % p for p in s.pts))
    txt = "\n".join(out)
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "r28_props_coords.txt"), "w") as f:
        f.write(txt + "\n")
    print(txt)


if __name__ == "__main__":
    if "--dump" in sys.argv:
        dump()
        sys.exit(0)
    if "--control" in sys.argv:
        print("★ 양성 대조 모드 — 일부러 나쁜 값을 넣는다. **빨간불이 켜져야 정상.**\n")
        n = gate(True)
        print("\n★ 프롭 게이트 양성 대조: %s" % ("OK — 실제로 잡는다(위반 %d건)" % n if n else "FAIL — 아무것도 안 잡았다"))
        sys.exit(0)
    sys.exit(1 if gate(False) else 0)
