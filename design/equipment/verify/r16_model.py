# -*- coding: utf-8 -*-
"""R16 — 착용 표면 재설계 (design-equipment, 2026-09-05).

리더 결정(전제) 세 가지 위에서 16종 착용 표면을 다시 세운다:
  1. 착용 획 하한 2pt → **1pt** (1pt = Windows 1× 모니터의 디바이스 픽셀 1개. 그 아래는 없다).
  2. 착용 색 = **브라스 단색톤**(인계본 착용 프리뷰 C열처럼). 아이템별 카테고리 틴트 폐기.
  3. 목표 = D열이 C열과 **같은 물건으로 읽히는 것**. 사용자 추가 판정: "동일하게 읽힌다"가 합격선이고
     안경(EYES 4종)이 산성 시험이다.

그래서 이 모델의 착용 기하는 **인계본 착용 프리뷰 그 자체**다 — README 「장비 오버레이 위치」가 말하는
정면 아이콘 오버레이(슬롯 박스 70/48/54/88 · 획 2.8/3.4/3.4/2 · 등 아이콘 40 %) + 망토 전용 무대 도형
(뒤판·칼라·걸쇠). 3/4 재저작 기하(R12~R15 BODY_SET)는 **이 표면에서 폐기**한다. 좌표는 손으로 옮기지
않고 handoff.py 가 인계본 HTML 을 파싱한 것을 icon_to_R / stage_to_R 로 그대로 R 단위에 놓는다.

이 파일이 답하는 질문
  · 인계본 착용 획(슬롯별 0.115~0.138 R)은 출하 배율 0.75 에서 몇 pt 인가 → 전부 1pt 아래(0.67~0.81pt).
  · 1pt 로 올리면 91조각 중 무엇이 살고(1-A/1-C/자기교차) 무엇이 여전히 못 사는가.
  · R15 「구조적 소멸 29」 중 1pt 로 되살아나는 것 / 1pt 로도 못 사는 것 / 배율 0.35 에서 사라지는 것.
  · 브라스 단색 팔레트가 WornColor 상자(S ≥ 0.42, V ∈ [0.55, 0.80])를 통과하는가.

★ 교정: 조각 91 · H 17 · 인계본 잉크값 7건(r15_model 과 같은 값)이 맞지 않으면 SystemExit.
★ 숫자 자는 「존재」만 잰다. 「C열과 같은 물건으로 읽히는가」는 r16_worn_sheet.png 를 눈으로 본다.
"""
import math, os, sys, colorsys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig, handoff
from rig import Shape

# ============================================================================
# 0. 자 — 전부 프로덕션 상수/인계본 선언값에서 유도 (숫자를 적어 두지 않는다)
# ============================================================================
PT_PER_UNIT = 846.0 / 24.0            # StickConfig.ReferencePointsPerWorldUnitApprox = 35.25
HEAD_R_BASE = 0.22                    # AccessoryShapeBuilder.BaselineHeadVisualRadius
BASE_STROKE = 0.048                   # AccessoryShapeBuilder.BaselineStrokeWidth (본체와 무관한 액세서리 획)
FLOOR_PT = 1.0                        # ★ 리더 결정 — 착용 표면 획 하한 (구 2.0 = StickConfig.MinStrokeScreenPoints)
OLD_FLOOR_PT = 2.0
SHIP = 0.75
SCALES = (0.35, 0.60, 0.75, 1.00)     # MinCharacterScale 0.35 · 사용자 저장 0.60 · 출하 0.75 · MaxCharacterScale 1.00

def head_r_pt(scale):
    """머리 반경(pt). 0.75 → 5.816pt (머리 지름 11.63pt)."""
    return HEAD_R_BASE * scale * PT_PER_UNIT

def floor_R(nominal_R, scale, floor_pt=FLOOR_PT):
    """명목 획(R)에 화면 하한을 건 실폭(R)."""
    return max(nominal_R, floor_pt / head_r_pt(scale))

ONE_PT_R = {s: 1.0 / head_r_pt(s) for s in SCALES}      # 1pt 가 R 로 얼마인가: 0.75 → 0.17193 R

# 인계본 착용 획 — README 「장비 오버레이 위치」: 박스 한 변(px) × 획(아이콘 단위, /64) → px → R
WORN_STROKE_ICON = {"head": 2.8, "eyes": 3.4, "neck": 3.4, "back": 2.0}
def slot_stroke_R(slot):
    box, _ = handoff.SLOT_BOX[slot]
    return WORN_STROKE_ICON[slot] * box / 64.0 / handoff.HEAD_R_PX
SLOT_STROKE_R = {s: slot_stroke_R(s) for s in handoff.SLOT_BOX}   # head .1384 eyes .1153 neck .1297 back .1243

def stage_stroke_R(w_units):
    """무대(viewBox 200×240, 렌더 폭 158px) 획 → R."""
    return w_units * handoff.K_PX / handoff.HEAD_R_PX

# 우리 본체(그림용 · design-character 실측을 r13_bodyocclusion 과 같은 식으로) — 착용 표면 판정에는 안 쓴다
def body_w(base_units, min_pt, scale=SHIP):
    return max(base_units * scale, min_pt / PT_PER_UNIT) / (HEAD_R_BASE * scale)
# ★ 2026-09-05 정정(design-character 감사 docs/CHARACTER_BODY_AUDIT_2026-09-05.md §2): 첫 판은 명목 0.063 에 1pt 하한을 걸어 0.2864 R(원반 1.1432)이었다.
#   프로덕션은 굽기에서 2pt 하한을 먹은 실효 계수 0.0756501 을 배율비로 다시 쓰므로 링 = max(0.0756501·s, 1pt/PPU)/(0.22·s) = 0.343864 R @0.75,
#   머리 잉크 원반 바깥 반경 = **1.171932 R**(창 높이 ≤ 906pt & 배율 < s* 구석에서만 최대 1.18421). 드러난 목 = 1.17193 − 1.31818 = 0.14625 R(0.85pt).
HEAD_RING_EFFECTIVE = 0.0756501
W_RING_R = max(HEAD_RING_EFFECTIVE * SHIP, 1.0 / PT_PER_UNIT) / (HEAD_R_BASE * SHIP)   # 0.343864 R
HEAD_OUTER_R = 1.0 + W_RING_R / 2.0   # 1.171932 R (링과 채움이 같은 색 — 잉크 원반 바깥 반경)
assert abs(HEAD_OUTER_R - 1.171932) < 1e-5, HEAD_OUTER_R
W_TORSO_R = 0.11 * 1.045 * SHIP / (HEAD_R_BASE * SHIP)
W_ARM_R = 0.10 * 1.045 * SHIP / (HEAD_R_BASE * SHIP)
W_LEG_R = 0.57

# ============================================================================
# 1. 색 — 브라스 단색 (D′) / 인계본 프리뷰 그대로 (C 대조용)
# ============================================================================
BRASS = "#C8A15A"           # UiChrome.Accent (2026-09-03 브라스 채택) — WornColor 항등
WEAR_COMMON = "#D8B27A"     # 인계본 착용 오버레이 「일반」 선색 (PALETTE_SPEC §28-5 폐기 표시)
RAR_COMMON = "#8A8F98"      # 인계본 카드 등급색 「일반」 = 착용 프리뷰의 강조(accent) 입력
CAPE_BACK, CAPE_COLLAR, CAPE_LINE, CLASP = "#A8332A", "#8E241C", "#7E1F17", "#C8A15A"
WHITE = "#FFFFFF"
HI_ALPHA = 0.42

# 착용 팔레트(브라스 단색) — 역할 → 색. 인계본 문법(선 C · 강조 A · 하이라이트 흰 42 %)을 브라스 하나로 접는다.
PALETTE_BRASS = {"ink": BRASS, "accent": BRASS, "white": WHITE}
# 인계본 프리뷰 그대로(일반 등급) — C열을 내 래스터로 재현해 도구를 교정할 때만 쓴다.
PALETTE_HANDOFF = {"ink": WEAR_COMMON, "accent": RAR_COMMON, "white": WHITE}

def hex_rgb(h):
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))

def worn_color(hexs):
    """ItemCatalog.WornColor 의 독립 구현(S ≥ 0.42, V ∈ [0.55, 0.80], 색상각 불변). 항등 여부 판정용."""
    r, g, b = [c / 255.0 for c in hex_rgb(hexs)]
    H, S, V = colorsys.rgb_to_hsv(r, g, b)
    r2, g2, b2 = colorsys.hsv_to_rgb(H, max(S, 0.42), min(max(V, 0.55), 0.80))
    out = "#%02X%02X%02X" % (round(r2 * 255), round(g2 * 255), round(b2 * 255))
    return out, out.upper() == hexs.upper()

def rel_lum(hexs):
    def ch(c):
        c /= 255.0
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = hex_rgb(hexs)
    return 0.2126 * ch(r) + 0.7152 * ch(g) + 0.0722 * ch(b)

def contrast(a, b):
    la, lb = rel_lum(a), rel_lum(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)

# ============================================================================
# 2. 조각 — 인계본 착용 프리뷰를 그대로 R 단위로
# ============================================================================
KINDS = ["sunglasses", "roundglasses", "goggles", "monocle",           # ★ EYES 먼저 (사용자: "특히 안경은 심함")
         "clothhat", "furhat", "fedora", "crown",
         "bowtie", "stripedtie", "scarf", "bellnecklace",
         "shortcape", "longcape", "wings", "backpack"]
KO = {k: handoff.OUR_NAME[k][1] for k in KINDS}
SLOT = {k: handoff.OUR_NAME[k][0] for k in KINDS}
ISLOT = {k: handoff.ITEM_SLOT[k] for k in KINDS}
CAPES = {"shortcape", "longcape"}

class Piece:
    """착용 조각. fill = None | ("grad", a0, a1) | ("accent", a) | ("solid", hex) ;
    line = None | ("ink", a) | ("white", a) | ("solid", hex, a). 색 이름은 팔레트에서 푼다."""
    __slots__ = ("kind", "name", "src", "call", "pts", "loop", "filled", "mult", "nominal_R",
                 "fill", "line", "dash", "group_alpha", "layer", "role", "note")
    def __init__(self, kind, name, src, call, pts, loop, filled, mult, nominal_R, fill, line,
                 dash=None, group_alpha=1.0, layer="front", role="", note=""):
        self.kind, self.name, self.src, self.call = kind, name, src, call
        self.pts = [(float(a), float(b)) for a, b in pts]
        self.loop, self.filled, self.mult, self.nominal_R = loop, filled, mult, nominal_R
        self.fill, self.line, self.dash, self.group_alpha, self.layer = fill, line, dash, group_alpha, layer
        self.role, self.note = role, note

    def width_R(self, scale=SHIP, floor_pt=FLOOR_PT):
        return floor_R(self.nominal_R, scale, floor_pt)

    def as_shape(self):
        return Shape(self.name, self.pts, loop=self.loop, filled=self.filled)

    def __repr__(self):
        return "Piece(%s.%s)" % (self.kind, self.src)

_RAW = handoff.parse_items()

# ★ 속성 의미 — 인계본 원문(ItemIcon.dc.html, React)과 ux-designer 재현기(render/icons.js, 문자열 SVG)가 **다르게 그린다**.
#   React 는 `{...o}` 가 나중이라 `fill: A` / `strokeWidth: w*1.5` 가 기본값을 **덮어쓴다**.
#   icons.js 는 `<path fill="url(#G)" stroke-width="${w}" ... ${at(o)}/>` 로 같은 이름의 속성을 **두 번** 낸다 — HTML 파서는
#   중복 속성에서 **첫 것만** 남긴다(duplicate-attribute parse error → 나중 것 제거; Chrome 실험 r16_dupattr 로 확인).
#   그래서 Chrome 이 찍은 A/C 열(R15)과 icons_16.png 는 `fill: A` 채움 23건 · 획 배수 11건(조각 33/91)을 **조용히 버린 그림**이다
#   (semantics_delta() 실측 — 외알안경 CB0 은 둘 다 해당).
#   "original" = 인계본 원문 의미(사용자가 준 디자인본) / "icons_js" = 사용자가 지금까지 본 C열 그대로.
ATTR_SEMANTICS = "original"

def _icon_pieces(kind, semantics=None):
    sem = semantics or ATTR_SEMANTICS
    slot = ISLOT[kind]
    fx, fy, sx = handoff.icon_to_R(kind)
    base = SLOT_STROKE_R[slot]
    out = []
    for i, pc in enumerate(handoff.build(kind, _RAW[kind])):
        pts = [(fx(x), fy(y)) for x, y in pc.pts]
        mult = pc.stroke_w / handoff.STROKE
        src = "%s%d" % (pc.call, i)
        name = "%s.%s" % (kind, src)
        dash = None
        if "strokeDasharray" in pc.opts:
            on, off = [float(v) for v in pc.opts["strokeDasharray"].strip("'\"").split()]
            dash = (on * sx, off * sx)
        la = handoff._num(pc.opts["strokeOpacity"]) if "strokeOpacity" in pc.opts else 1.0
        if pc.call == "H":
            out.append(Piece(kind, name, src, "H", pts, False, False, mult, base * mult, None, ("white", HI_ALPHA),
                             role="하이라이트"))
            continue
        if sem == "icons_js" and pc.call in ("B", "S", "CB", "RB") and "strokeWidth" in pc.opts:
            mult = 1.0                      # 중복 stroke-width → 첫 것(기본 w)이 이긴다
        fill = None; role = "낱선"
        if pc.filled:
            fo = pc.fill_opacity
            g0, g1 = (0.34, 0.08) if kind not in CAPES else (1.0, 0.62)
            if pc.call in ("F", "CF"):
                fill = ("accent", fo if fo is not None else (0.55 if pc.call == "F" else 1.0)); role = "강조"
            elif pc.is_accent:
                if sem == "icons_js":       # 중복 fill → 첫 것(그라디언트)이 이기고 fill-opacity 만 곱해진다
                    fo = fo if fo is not None else 1.0
                    fill = ("grad", g0 * fo, g1 * fo); role = "본체(워시×α)"
                else:
                    fill = ("accent", fo if fo is not None else 1.0); role = "강조채움" if fo is not None else "강조"
            else:
                fill = ("grad", g0, g1); role = "본체"
        line = None if pc.call in ("F", "CF") else ("ink", la)
        out.append(Piece(kind, name, src, pc.call, pts, pc.closed or pc.filled, pc.filled, mult, base * mult,
                         fill, line, dash=dash, role=role))
    return out

def semantics_delta():
    """원문 의미와 icons.js 의미가 갈리는 조각 목록(리더 보고용)."""
    rows = []
    for k in KINDS:
        a = _icon_pieces(k, "original"); b = _icon_pieces(k, "icons_js")
        for p, q in zip(a, b):
            what = []
            if p.mult != q.mult: what.append("획 ×%.2f→×1.0" % p.mult)
            if p.fill != q.fill: what.append("채움 %s→%s" % (p.fill, q.fill))
            if what: rows.append((k, p.src, " · ".join(what)))
    return rows

def _stage_path(d):
    pts, closed = handoff.flatten_path(d)
    return [handoff.stage_to_R(x, y) for x, y in pts], closed

def _cape_pieces(kind):
    """README 「망토 착용 렌더링」 — 아이콘을 얹지 않고 체형 전용 도형(뒤판 z0 · 칼라+걸쇠 z4)."""
    back_d = ("M86 78 Q68 138 70 184 Q100 193 130 184 Q132 138 114 78 Z" if kind == "longcape"
              else "M87 78 Q74 106 74 132 Q100 140 126 132 Q126 106 113 78 Z")
    bp, _ = _stage_path(back_d)
    cp, _ = _stage_path("M74 72 Q100 88 126 72 L128 80 Q100 96 72 80 Z")
    clasp = [handoff.stage_to_R(*q) for q in handoff.circle_pts(100, 85, 4.6, 24)]
    return [Piece(kind, kind + ".StageBack", "STB", "STAGE", bp, True, True, 1.0, stage_stroke_R(2.0),
                  ("solid", CAPE_BACK), ("solid", CAPE_LINE, 1.0), layer="back", role="무대 뒤판"),
            Piece(kind, kind + ".StageCollar", "STC", "STAGE", cp, True, True, 1.0, stage_stroke_R(2.0),
                  ("solid", CAPE_COLLAR), ("solid", CAPE_LINE, 1.0), layer="front", role="무대 칼라"),
            Piece(kind, kind + ".StageClasp", "STK", "STAGE", clasp, True, True, 0.9, stage_stroke_R(1.8),
                  ("solid", CLASP), ("solid", CAPE_LINE, 1.0), layer="front", role="무대 걸쇠")]

def _worn_pieces(kind, semantics=None):
    if kind in CAPES:
        return _cape_pieces(kind)          # 인계본: 망토는 등 아이콘을 그리지 않는다(eq.back = null)
    ps = _icon_pieces(kind, semantics)
    if ISLOT[kind] == "back":
        for p in ps:
            p.group_alpha = 0.40; p.layer = "back"
    return ps

def worn_set(semantics=None):
    return {k: _worn_pieces(k, semantics) for k in KINDS}

WORN = worn_set()                               # ATTR_SEMANTICS("original") — 사용자가 준 디자인본의 의미
ICON = {k: _icon_pieces(k) for k in KINDS}     # 교정용(망토 아이콘 포함 91조각)

# ---- 교정 --------------------------------------------------------------------
_total = sum(len(v) for v in ICON.values()); _h = sum(1 for v in ICON.values() for p in v if p.call == "H")
if _total != 91 or _h != 17:
    raise SystemExit("교정 실패: 조각 %d(기대 91) 하이라이트 %d(기대 17)" % (_total, _h))
_CAL = [("crown", 3, 0.1879), ("crown", 6, 0.2077), ("sunglasses", 3, 0.1390),
        ("roundglasses", 2, 0.1763), ("goggles", 3, 0.1356), ("bellnecklace", 1, 0.1297), ("goggles", 1, 0.4747)]
for _k, _i, _want in _CAL:
    x0, y0, x1, y1 = rig.bounds(ICON[_k][_i].pts); _ink = max(x1 - x0, y1 - y0)
    if abs(_ink - _want) > 0.0015:
        raise SystemExit("교정 실패: %s[%d] 잉크 %.4f ≠ %.4f" % (_k, _i, _ink, _want))
# 무대 도형 교정 — README 걸쇠 circle(100,85) r4.6 → R: 중심 (0, −1.3929), 반경 0.1643
_cl = [p for p in WORN["longcape"] if p.src == "STK"][0]
_cx = sum(q[0] for q in _cl.pts) / len(_cl.pts); _cy = sum(q[1] for q in _cl.pts) / len(_cl.pts)
if abs(_cx) > 1e-6 or abs(_cy - (46 - 85) / 28.0) > 1e-6:
    raise SystemExit("교정 실패: 걸쇠 중심 (%.4f, %.4f)" % (_cx, _cy))

# ============================================================================
# 3. 생존 판정 — 규칙 1-A(잉크 사각형 ≥ 1.5w · 꺾임-꺾임 변 ≥ 1.0w) / 1-C(채움 ρ_max ≥ w) / 자기교차
# ============================================================================
def rho_max(pts, step=0.01):
    """채움의 최대 내접 반지름(규칙 1-C). r15_model.rho_max 와 같은 정의(격자 0.01 R, 짝수-홀수 포함 판정,
    변까지의 최소 거리)를 numpy 로 벡터화한 것. 교정: 아래 `_rho_selftest` 가 r15 순수 파이썬 판과 대조한다."""
    import numpy as np
    P = np.asarray(pts, dtype=np.float64); n = len(P)
    x0, y0, x1, y1 = rig.bounds(pts)
    xs = np.arange(x0, x1 + 1e-9, step); ys = np.arange(y0, y1 + 1e-9, step)
    X, Y = np.meshgrid(xs, ys); X = X.ravel(); Y = Y.ravel()
    A = P; Bp = np.roll(P, -1, axis=0)
    # 포함 판정 (ray casting, rig.contains 와 같은 규칙)
    inside = np.zeros(X.shape, dtype=bool)
    for k in range(n):
        ax, ay = A[k]; bx, by = Bp[k]
        cond = (ay > Y) != (by > Y)
        if by == ay: continue
        xint = ax + (Y - ay) * (bx - ax) / (by - ay)
        inside ^= cond & (X < xint)
    if not inside.any(): return 0.0
    Xi, Yi = X[inside], Y[inside]
    d = np.full(Xi.shape, 1e9)
    for k in range(n):
        ax, ay = A[k]; bx, by = Bp[k]
        dx, dy = bx - ax, by - ay; L = dx * dx + dy * dy
        if L < 1e-12:
            dd = np.hypot(Xi - ax, Yi - ay)
        else:
            t = np.clip(((Xi - ax) * dx + (Yi - ay) * dy) / L, 0.0, 1.0)
            dd = np.hypot(Xi - (ax + dx * t), Yi - (ay + dy * t))
        d = np.minimum(d, dd)
    return float(d.max())

def _rho_pure(pts, step=0.01):
    """r15_model.rho_max 원문(순수 파이썬) — 벡터화 판 교정용."""
    x0, y0, x1, y1 = rig.bounds(pts); best = 0.0; n = len(pts)
    x = x0
    while x <= x1:
        y = y0
        while y <= y1:
            if rig.contains(pts, (x, y)):
                d = 1e9
                for k in range(n):
                    a, b = pts[k], pts[(k + 1) % n]
                    dx, dy = b[0] - a[0], b[1] - a[1]; L = dx * dx + dy * dy
                    t = 0.0 if L < 1e-12 else max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / L))
                    d = min(d, math.hypot(x - (a[0] + dx * t), y - (a[1] + dy * t)))
                if d > best: best = d
            y += step
        x += step
    return best

def _rho_selftest():
    """작은 조각 3개로 벡터화 판 ↔ 순수 판 대조(±0.5 격자 안). 깨지면 SystemExit — 이 뒤 1-C 숫자는 전부 무효다."""
    for k, src in (("roundglasses", "CB0"), ("crown", "CF2"), ("bowtie", "RB2")):
        p = [q for q in ICON[k] if q.src == src][0]
        a, b = rho_max(p.pts), _rho_pure(p.pts)
        if abs(a - b) > 0.006:
            raise SystemExit("rho 교정 실패 %s.%s: numpy %.4f vs pure %.4f" % (k, src, a, b))
_rho_selftest()

_RHO = {}
def rho_of(p):
    if p.name not in _RHO: _RHO[p.name] = rho_max(p.pts)
    return _RHO[p.name]

def path_len(pts, loop):
    n = len(pts); segs = n if loop else n - 1
    return sum(math.dist(pts[i], pts[(i + 1) % n]) for i in range(segs))

def survival(p, scale=SHIP, floor_pt=FLOOR_PT):
    """(생존 여부, 사유). 획 실폭 = 그 배율에서 하한을 건 값."""
    w = p.width_R(scale, floor_pt)
    v = rig.rule_one(p.as_shape(), w)
    if v: return False, "1-A(%.3fR): %s" % (w, v)
    if p.filled:
        rho = rho_of(p)
        if rho < w: return False, "1-C: ρ_max %.3fR < w %.3fR" % (rho, w)
    if p.loop and rig.self_intersects(p.pts): return False, "자기교차"
    return True, "OK"

# R15 §13-1-2 「구조적으로 못 사는 29조각」 — src 로 못박는다(원형 장식 10 · 하이라이트 9 · 가는 낱선 9 · 얇은 채움 1)
R15_DEAD = {
    "crown": ["CF2", "CF3", "CF4", "CB5", "CB6", "CB7", "H8"],
    "bellnecklace": ["CF1", "CF2", "CF3", "H7"],
    "monocle": ["CB3", "H1"],
    "sunglasses": ["S3", "S4", "H5"],
    "roundglasses": ["S3", "S4", "H5", "H6"],
    "goggles": ["S3", "H6"],
    "bowtie": ["H3"], "stripedtie": ["H4"],
    "scarf": ["S2", "S3", "S4"],
    "furhat": ["S4"],
    "backpack": ["RB4"],
}
assert sum(len(v) for v in R15_DEAD.values()) == 29

def classify():
    """(가) 1pt 로 되살아난 것 (나) 1pt 로도 못 사는 것 (다) 배율 0.35 에서 사라지는 것 — 전 조각."""
    rows = []
    for k in KINDS:
        for p in WORN[k]:
            r15 = p.src in R15_DEAD.get(k, [])
            ok75, why75 = survival(p, 0.75)
            ok100, why100 = survival(p, 1.00)
            ok35, why35 = survival(p, 0.35)
            ok60, why60 = survival(p, 0.60)
            okold, whyold = survival(p, 0.75, OLD_FLOOR_PT)
            rows.append(dict(kind=k, p=p, r15dead=r15, ok75=ok75, why75=why75, ok100=ok100, why100=why100,
                             ok35=ok35, why35=why35, ok60=ok60, why60=why60, okold=okold, whyold=whyold))
    return rows

# ============================================================================
# 4. 안경 — 산성 시험용 기하 실측 (렌즈 구멍 · 다리 길이 · 브리지 간격, pt)
# ============================================================================
def eyes_metrics(scale=SHIP):
    Rpt = head_r_pt(scale); w1 = ONE_PT_R[scale] if scale in ONE_PT_R else 1.0 / Rpt
    out = []
    def line(kind, src, what, val_R, extra=""):
        out.append("%-13s %-4s %-34s %.3f R = %.2f pt %s" % (kind, src, what, val_R, val_R * Rpt, extra))
    # 동그란안경: 렌즈 r 10.5u → 안쪽 맑은 지름 = 2r − w
    fx, fy, sx = handoff.icon_to_R("roundglasses")
    r = 10.5 * sx; w = floor_R(SLOT_STROKE_R["eyes"], scale)
    line("roundglasses", "CB0", "렌즈 지름 2r", 2 * r)
    line("roundglasses", "CB0", "렌즈 안쪽 맑은 지름 2r − w", 2 * r - w)
    gap = (45 - 19) * sx - 2 * r
    line("roundglasses", "S2", "두 렌즈 테 사이 간격(중심선)", gap, "(획 w=%.3fR 를 빼면 %.2f pt)" % (w, (gap - w) * Rpt))
    for kind, srcs in (("sunglasses", ("S3", "S4")), ("roundglasses", ("S3", "S4")), ("goggles", ("S4", "S5"))):
        for src in srcs:
            p = [q for q in ICON[kind] if q.src == src][0]
            L = path_len(p.pts, False)
            line(kind, src, "다리(temple) 길이", L, "= %.2f w" % (L / p.width_R(scale)))
    p = [q for q in ICON["sunglasses"] if q.src == "S2"][0]; L = path_len(p.pts, False)
    line("sunglasses", "S2", "코다리 길이", L, "= %.2f w" % (L / p.width_R(scale)))
    p = [q for q in ICON["goggles"] if q.src == "S3"][0]; L = path_len(p.pts, False)
    line("goggles", "S3", "브리지 길이", L, "= %.2f w" % (L / p.width_R(scale)))
    p = [q for q in ICON["monocle"] if q.src == "S2"][0]
    line("monocle", "S2", "줄 파선 주기(0.5+3.6 u)", p.dash[0] + p.dash[1], "· 점 지름 = w %.3fR = %.2f pt → 점 간격/점 = %.2f" %
         (p.width_R(scale), p.width_R(scale) * Rpt, (p.dash[0] + p.dash[1]) / p.width_R(scale)))
    p = [q for q in ICON["monocle"] if q.src == "CB3"][0]; x0, y0, x1, y1 = rig.bounds(p.pts)
    line("monocle", "CB3", "끝 구슬 지름", x1 - x0)
    for kind in ("sunglasses", "roundglasses", "goggles", "monocle"):
        for p in ICON[kind]:
            if p.call == "H":
                L = path_len(p.pts, False)
                line(kind, p.src, "하이라이트 길이", L, "= %.2f w (2 미만이면 점)" % (L / p.width_R(scale)))
    return out

def eyes_pairwise(scale=SHIP):
    """EYES 4종 쌍별 실루엣 차(72구간 max Δr) — verify.py 와 같은 자를 1pt 실폭과 옛 W(0.3439R) 둘로 나눈다."""
    w1 = ONE_PT_R[scale]; W_old = rig.W
    ks = [k for k in KINDS if ISLOT[k] == "eyes"]
    prof = {k: rig.profile([p.as_shape() for p in WORN[k]], 0.0) for k in ks}
    rows = []
    for i in range(len(ks)):
        for j in range(i + 1, len(ks)):
            d = rig.max_delta(prof[ks[i]], prof[ks[j]])
            rows.append((ks[i], ks[j], d, d / w1, d / W_old))
    return rows

# ============================================================================
# 5. 보고
# ============================================================================
def report(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    w("== 0. 자 ==")
    for s in SCALES:
        w("   배율 %.2f: 머리 반경 %.3f pt (지름 %.2f pt) · 1pt = %.4f R · 2pt = %.4f R"
          % (s, head_r_pt(s), 2 * head_r_pt(s), ONE_PT_R[s], 2 * ONE_PT_R[s]))
    w("   본체(그림용): 링 %.4f R(1pt 하한) · 머리 잉크 바깥반경 %.4f R · 몸통 %.4f · 팔 %.4f · 다리 %.2f R"
      % (W_RING_R, HEAD_OUTER_R, W_TORSO_R, W_ARM_R, W_LEG_R))
    w()
    w("== 1. 인계본 착용 획(슬롯별) → 배율별 pt · 1pt 하한 실폭 ==")
    w("   슬롯  명목(R)   | " + " | ".join("s=%.2f 명목pt→실폭R(배수)" % s for s in SCALES))
    for slot in ("head", "eyes", "neck", "back"):
        n = SLOT_STROKE_R[slot]
        cells = []
        for s in SCALES:
            f = floor_R(n, s); cells.append("%.3fpt→%.4fR(×%.2f)" % (n * head_r_pt(s), f, f / n))
        w("   %-5s %.4f R | %s" % (slot, n, " | ".join(cells)))
    n = stage_stroke_R(2.0)
    w("   %-5s %.4f R | %s" % ("무대2", n, " | ".join("%.3fpt→%.4fR(×%.2f)" % (n * head_r_pt(s), floor_R(n, s), floor_R(n, s) / n) for s in SCALES)))
    w("   ★ 배율 0.75 에서 인계본 획은 전부 1pt 아래(0.67~0.81pt)다 → 1pt 하한이 ×1.24~1.49 로 키운다(2pt 하한은 ×2.48~2.98 이었다).")
    w("   ★ 배율 0.75 에서는 등급(×0.7~×1.5)이 전부 같은 1pt 로 접힌다 — 외알안경 테 ×1.5 = %.3fpt 만 1pt 를 살짝 넘는다."
      % (SLOT_STROKE_R["eyes"] * 1.5 * head_r_pt(0.75)))
    w()
    w("== 2. 색 — 브라스 단색 팔레트와 WornColor 상자(S≥0.42, V∈[0.55,0.80]) ==")
    for label, c in (("선·강조(브라스)", BRASS), ("인계본 착용 선 일반", WEAR_COMMON), ("인계본 강조 일반", RAR_COMMON),
                     ("하이라이트 흰", WHITE), ("망토 뒤판", CAPE_BACK), ("망토 칼라", CAPE_COLLAR), ("망토 선", CAPE_LINE), ("걸쇠", CLASP)):
        o, same = worn_color(c)
        w("   %-14s %s → WornColor %s %s" % (label, c, o, "항등" if same else "★ 변형"))
    w("   대비(비텍스트 하한 3.0): 브라스 vs 검정 잉크 %.2f · vs 흰 잉크/흰 바탕 %.2f · vs 인계본 무대 #0F0D0C %.2f · vs 종이 #EEEEEC %.2f"
      % (contrast(BRASS, "#000000"), contrast(BRASS, "#FFFFFF"), contrast(BRASS, "#0F0D0C"), contrast(BRASS, "#EEEEEC")))
    w("   인계본 선 #D8B27A vs 무대 %.2f (C열의 실제 대비)" % contrast(WEAR_COMMON, "#0F0D0C"))
    w()
    rows = classify()
    w("== 3. 조각별 생존 (1pt 하한) — s=0.75 / 1.00 / 0.60 / 0.35 · 옛 2pt 하한(0.75) 대조 ==")
    w("   %-13s %-5s %-8s 명목R  실폭R@.75 잉크(R)        .75  1.0  .60  .35 | 2pt  R15  사유(.75)" % ("kind", "src", "역할"))
    for r in rows:
        p = r["p"]; x0, y0, x1, y1 = rig.bounds(p.pts)
        w("   %-13s %-5s %-8s %.4f %.4f  %.2f×%.2f  %s  %s  %s  %s | %s  %s  %s" % (
            r["kind"], p.src, p.role, p.nominal_R, p.width_R(), x1 - x0, y1 - y0,
            "생존" if r["ok75"] else "소멸", "생존" if r["ok100"] else "소멸", "생존" if r["ok60"] else "소멸",
            "생존" if r["ok35"] else "소멸", "생존" if r["okold"] else "소멸", "★" if r["r15dead"] else " ",
            "" if r["ok75"] else r["why75"]))
    w()
    dead = [r for r in rows if r["r15dead"]]
    rev = [r for r in dead if r["ok75"]]; still = [r for r in dead if not r["ok75"]]
    w("== 4. R15 「구조적 소멸 29」 재판정 (1pt 하한, s=0.75) ==")
    w("   (가) 1pt 로 되살아난 것: %d / 29" % len(rev))
    for r in rev: w("        %-13s %-4s %s" % (r["kind"], r["p"].src, r["p"].role))
    w("   (나) 1pt 로도 못 사는 것: %d / 29" % len(still))
    for r in still: w("        %-13s %-4s %s — %s" % (r["kind"], r["p"].src, r["p"].role, r["why75"]))
    w("   (다) 배율 0.35 에서 사라지는 것(전 조각 기준): %d / %d" % (sum(1 for r in rows if not r["ok35"]), len(rows)))
    for r in rows:
        if not r["ok35"]: w("        %-13s %-4s %s — %s" % (r["kind"], r["p"].src, r["p"].role, r["why35"]))
    w("   배율 0.60 에서 사라지는 것: %d / %d · 1.00 에서 사라지는 것: %d / %d · 옛 2pt(0.75)에서 사라졌을 것: %d / %d"
      % (sum(1 for r in rows if not r["ok60"]), len(rows), sum(1 for r in rows if not r["ok100"]), len(rows),
         sum(1 for r in rows if not r["okold"]), len(rows)))
    w()
    w("== 5. 안경 산성 시험 — 기하 실측 (s=0.75) ==")
    for ln in eyes_metrics(0.75): w("   " + ln)
    w("   (s=1.00)")
    for ln in eyes_metrics(1.00): w("   " + ln)
    w()
    w("== 6. EYES 쌍별 실루엣 차 (72구간 max Δr) — 1pt 실폭 기준 / 옛 W 0.3439R 기준 ==")
    for a, b, d, d1, dold in eyes_pairwise():
        w("   %-13s vs %-13s Δr %.3f R = %.2f 획(1pt) / %.2f 획(옛 W)" % (a, b, d, d1, dold))
    w()
    w("== 6-1. ★ 인계본 원문(React) ↔ 재현기(icons.js) 의미 차 — Chrome 이 버린 속성 (r16_dupattr 실험: 첫 속성이 이긴다) ==")
    d = semantics_delta()
    w("   갈리는 조각 %d / 91 (획 배수 %d · 채움 %d)" % (len(d), sum(1 for r in d if "획" in r[2]), sum(1 for r in d if "채움" in r[2])))
    for k, src, what in d: w("   %-13s %-4s %s" % (k, src, what))
    w()
    w("== 7. 아이템별 조각 수 · 보조색(강조) 수 · 레이어 ==")
    for k in KINDS:
        ps = WORN[k]
        w("   %-13s %-8s 조각 %d · 강조 %d · 하이라이트 %d · 뒤층 %d · 그룹α %.2f" % (
            k, KO[k], len(ps), sum(1 for p in ps if p.fill and p.fill[0] == "accent"),
            sum(1 for p in ps if p.call == "H"), sum(1 for p in ps if p.layer == "back"), ps[0].group_alpha))


if __name__ == "__main__":
    report()
