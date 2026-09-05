# -*- coding: utf-8 -*-
"""R21 — 나머지 26종 중 (나)군 4종 재저작 + (다)군 18종 창작 좌표·색 (design-equipment, 2026-09-05)

R20 §14-11 이 남긴 것: (나) 다시 그린다 4 — 뿔테안경 · 베레모 · 판초 · 밀짚모자(챙만) / (다) 창작 18 — 머리카락 6 · FX 6 · PET 6.
자(尺)는 R20 과 같다: P 조각 4~9 · S 획 배수 + 1pt 하한 · H 흰 α0.42 ×0.75 1개 · C 채움 M/M2 불투명 + 잉크 윤곽, 독립선 재질색,
채움 위 낱선 잉크(α), 등급색 0 · G 정면 64u 아이콘 기하 · L H-1 2층 / H-2 착용선 ≤ +0.30·꼭대기 < 2.551 / N-1 / E-1·E-2 / B-1~B-4.
머리 잉크 원반 = 1.171932 R(정본, M16.HEAD_OUTER_R) · 덮임 판정 원반 = 1.184 R(배율 0.35 상한, M17.HEAD_R_COVER).

저작 방식(인계본 16종과 같은 계약)
  · 모자 2종 · 뿔테안경 · 판초 카드 = **64u 아이콘**(슬롯 박스 70/48/88 · 인계본 획 2.2u). 몸 = 슬롯 변환(EYES) / H-2 맞춤 (u, dy)(HEAD) / 무대 도형(BACK).
  · 머리카락 6 = 몸 좌표를 R 로 직접(머리 중심 원점). 카드 = HAIR 프레이밍(1 R = 16u, 머리 중심 (32,30)) 한 변환.
  · FX/PET 12 = 카드 64u 아이콘만. 몸 효과·펫 기하는 손대지 않는다(appearance.py 그대로).
색은 palette_model.ITEMS 의 M/M2(26/26 있음). 프로덕션 .cs/.asset 0줄.

    python3 r21_model.py > r21_model.out.txt
"""
import math, os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig, handoff, items, appearance
import r16_model as M16
import r17_model as M17
import r19_model as M19
import r20_model as M20
import palette_model as PM

SHIP = M16.SHIP
SH = rig.SHOULDER_R                                   # −1.3182
HEAD_R_SHIP, HEAD_R_COVER = M17.HEAD_R_SHIP, M17.HEAD_R_COVER   # 1.1719 / 1.1842
ONE_PT = M16.ONE_PT_R[SHIP]                           # 0.1719 R
W_OLD = rig.W                                         # 0.3439 R (verify.py 옛 자)
PAL = M20.PAL
Piece = M19.Piece

# ============================================================================
# 0. 좌표 변환 · 조각 생성기
# ============================================================================
def icon_xf(slot):
    """64u 아이콘 → R (handoff.icon_to_R 와 같은 식, 슬롯만 받는다)."""
    box, top = handoff.SLOT_BOX[slot]
    u = box / 64.0; sx = u / handoff.HEAD_R_PX
    return (lambda x: (x - 32.0) * sx), (lambda y: (handoff.HEAD_CY_PX - (top + y * u)) / handoff.HEAD_R_PX), sx

def path(d, seg=16):
    pts, closed = handoff.flatten_path(d, seg)
    if closed and len(pts) > 1 and math.dist(pts[0], pts[-1]) < 1e-9: pts = pts[:-1]   # Z 가 시작점을 되풀이하면 뺀다
    return pts, closed

def circ(cx, cy, r, n=24): return handoff.circle_pts(cx, cy, r, n)

HAIR_U = 16.0          # 카드 HAIR 프레이밍: 1 R = 16u, 머리 중심 (32, 30)
HAIR_CX, HAIR_CY = 32.0, 30.0
def hair_to_icon(p): return (HAIR_CX + p[0] * HAIR_U, HAIR_CY - p[1] * HAIR_U)

def P(kind, name, src, pts, *, loop=True, filled=False, crole=(None, "INK"), layer="front", mult=1.0, slot="head",
      role="", note="", conditional=None, transform="", line_alpha=1.0, call=None):
    """몸 조각. 획 명목 = 슬롯 명목 × 배수(1pt 하한은 width_R 이 건다). line_alpha: 잉크 낱선의 인계본 α(R-4)."""
    base = M16.SLOT_STROKE_R[slot] if slot in M16.SLOT_STROKE_R else M16.SLOT_STROKE_R["head"]
    lr = crole[1]
    line = ("white", M16.HI_ALPHA) if lr == "W" else (("ink", line_alpha) if lr == "INK" else (("ink", 1.0) if lr else None))
    return Piece(kind, name, src, call or src.rstrip("0123456789"), pts, loop, filled, mult, base * mult, None, line, None, 1.0,
                 layer, role, note, False, conditional, transform, None, crole=crole)

def C(kind, name, src, pts, *, loop=True, filled=False, crole=(None, "INK"), mult=1.0, role="", line_alpha=1.0, note=""):
    """카드 전용 조각(64u 아이콘 단위). nominal_R 은 카드에서 안 쓴다 — 2.2u × 배수."""
    lr = crole[1]
    line = ("white", M16.HI_ALPHA) if lr == "W" else (("ink", line_alpha) if lr == "INK" else (("ink", 1.0) if lr else None))
    return Piece(kind, name, src, src.rstrip("0123456789"), pts, loop, filled, mult, 2.2 * mult, None, line, None, 1.0,
                 "card", role, note, False, None, "", None, crole=crole)

def resolve(item_id, crole, surface, ink_hex):
    """M19.resolve 와 같은 뜻(GLASS 포함). 팔레트는 26종 id 로 찾는다."""
    it = PAL[item_id]; m, m2 = it[4], it[5]
    f, l = crole
    fill, fa = None, 1.0
    if f == "M": fill = m
    elif f == "M2": fill = m2
    elif f == "SH": fill = PM.shaded(m)
    elif f == "EYE": fill = PM.CHARCOAL if PM.L(ink_hex) >= 0.22 else PM.INK_WHITE
    elif f == "INKF": fill = PM.CARD_INK if surface == "card" else ink_hex          # 잉크 표식 채움(리틀스틱메이트 머리)
    elif isinstance(f, tuple) and f[0] == "CARDWASH":
        if surface == "card": fill, fa = m2, f[1]
        else: fill, fa = PM.over(PM.rgb(m2), f[1], PM.rgb(ink_hex)), 1.0          # GLASS(R20)
    line = None
    if l == "INK": line = PM.CARD_INK if surface == "card" else ink_hex
    elif l == "M": line = m
    elif l == "M2": line = m2
    elif l == "W": line = PM.INK_WHITE
    return fill, fa, line

def line_alpha(p):
    if p.line is None: return 1.0
    if p.line[0] == "white": return M16.HI_ALPHA
    if p.crole and p.crole[1] == "INK" and p.line[0] == "ink": return p.line[1]
    return 1.0

# ============================================================================
# 1. (나) 모자 2종 — 64u 아이콘 + H-2 맞춤 (M17.fit 와 같은 규칙, 입력만 내 다각형)
# ============================================================================
# 밀짚모자: 관은 R20 임시 좌표의 비례(밑 반폭 1.02 R · 높이 1.24 R · 띠 0.55 R) 를 64u 로 옮겼고, 챙은 중절모 렌즈꼴
# 'M6 41.5 Q32 33 58 41.5 Q32 51.5 Z' 문법(장축에서 위 호 = 먼 쪽/뒤층, 아래 호 = 가까운 쪽/앞층)으로 다시 그렸다.
# 장축 y=41 · 위 제어점 31 · 아래 51 → 렌즈 반높이 5u(중절모 4.25/5.0 과 같은 두께 — 반쪽 채움이 1-C(ρ ≥ 1pt)를 넘는 최소).
STRAW_ICON = [
    ("B", "Crown", "M16 41 Q16 25 32 24 Q48 25 48 41 Z", ("M", "INK"), 1.0, "관(M Canvas + 잉크) — 반폭 16u: 관 혼자 챙 장축 높이의 머리 현을 덮는다"),
    ("F", "Band", "M16 34 Q32 31.5 48 34 L48 40.5 Q32 38 16 40.5 Z", ("M2", None), 1.0, "띠(M2 TintHead, 윤곽 없음 — 인계본 F 문법)"),
    ("B", "Brim", "M2 41 Q32 31 62 41 Q32 51 2 41 Z", ("M", "INK"), 1.0, "챙(렌즈꼴 — H-1 분할: 위 호 뒤층 / 아래 호 앞층) 반높이 5u"),
    ("H", "H", "M23 32 Q25.5 27.5 30 26.5", (None, "W"), 0.75, "하이라이트"),
]
# 베레모: 정면에서 본 납작한 원반이 오른쪽(+x)으로 처진다. 밑변(띠) 위에 몸이 앉고 꼭지가 꼭대기에 선다. 먼 쪽 없음 → 전부 앞층.
BERET_ICON = [
    ("B", "Body", "M13 37 Q8 27 20 19 Q32 13 44 17 Q56 22 55 33 Q54 40 48 42 L13 37 Z", ("M", "INK"), 1.0, "몸(M Felt + 잉크) — 오른쪽으로 처진 원반"),
    ("F", "Band", "M13 37 L48 42 L48.5 37 L13.5 32 Z", ("M2", None), 1.0, "띠(M2, 윤곽 없음) 5u — 1-C(ρ ≥ 1pt) 최소"),
    ("S", "Stem", "M32 14.5 L32.5 10", (None, "M"), 1.2, "꼭지(독립선 M ×1.2)"),
    ("H", "H", "M17 25 Q20 20 26 18", (None, "W"), 0.75, "하이라이트"),
]

def icon_polys(spec):
    out = []
    for i, (call, nm, d, cr, mult, role) in enumerate(spec):
        pts, closed = path(d)
        out.append(dict(i=i, call=call, name=nm, src="%s%d" % (call, i), pts=pts, closed=closed or call in ("B", "F"), crole=cr, mult=mult, role=role))
    return out

def _central_hw(polys, ys):
    out = np.zeros(len(ys))
    for j, y in enumerate(ys):
        iv = []
        for poly in polys: iv += M17._scan_intervals(poly, y)
        if not iv: continue
        iv.sort(); merged = [list(iv[0])]
        for a, b in iv[1:]:
            if a <= merged[-1][1] + 1e-9: merged[-1][1] = max(merged[-1][1], b)
            else: merged.append([a, b])
        for a, b in merged:
            if a <= 32.0 <= b: out[j] = min(32.0 - a, b - 32.0); break
    return out

def evaluate_icon(polys, extent, u, dy, ky=1.0, w_max=0.30, ys=None, chw=None):
    ys = ys if ys is not None else np.arange(0.0, 64.0, 0.25)
    chw = chw if chw is not None else _central_hw(polys, ys)
    y_R = dy - ys * u * ky
    x0, x1, y0, y1 = extent
    top = dy - y0 * u * ky; bottom = dy - y1 * u * ky
    head_hw = np.sqrt(np.clip(HEAD_R_COVER ** 2 - y_R ** 2, 0.0, None))
    need = (y_R <= HEAD_R_COVER) & (y_R >= -HEAD_R_COVER)
    ok_line = (chw * u >= head_hw + M17.COVER_MARGIN) | ~need
    order = np.argsort(-y_R); wear = None; started = False
    for j in order:
        if y_R[j] > HEAD_R_COVER: continue
        if not ok_line[j]: break
        wear = y_R[j]; started = True
    covered_top = started and (max(y_R[(y_R <= HEAD_R_COVER) & ok_line]) >= HEAD_R_COVER - 0.3)
    why = []
    if wear is None or not covered_top: why.append("덮임 실패")
    elif wear > w_max: why.append("착용선 %.2f > %.2f" % (wear, w_max))
    if top >= M17.TOP_LIMIT: why.append("꼭대기 %.2f ≥ %.3f" % (top, M17.TOP_LIMIT))
    if bottom <= M17.CHIN: why.append("밑 %.2f ≤ %.1f" % (bottom, M17.CHIN))
    return dict(u=u, dy=dy, ky=ky, wear=wear, top=top, bottom=bottom, width=(x1 - x0) * u, ok=not why, why=" · ".join(why))

def fit_icon(polys, extent, w_max=0.30, u_range=(0.045, 0.150, 0.001), dy_range=(0.0, 4.5, 0.02)):
    ys = np.arange(0.0, 64.0, 0.25); chw = _central_hw(polys, ys)
    for u in np.arange(*u_range):
        cands = [e for e in (evaluate_icon(polys, extent, u, dy, 1.0, w_max, ys, chw) for dy in np.arange(*dy_range)) if e["ok"]]
        if cands: return min(cands, key=lambda e: abs(e["wear"] - w_max))
    return None

def split_lens(pts):
    """렌즈꼴(왼 끝 → 위 호 16점 → 오른 끝 → 아래 호 16점) → (먼 쪽 채움, 먼 쪽 윤곽, 가까운 쪽 채움, 가까운 쪽 윤곽). 자른 선은 안 그린다."""
    far = pts[0:17]; near = pts[16:] + [pts[0]]
    return far, far, near, near

def hat_pieces(kind, item_id, spec, split_brim, u_min):
    polys = icon_polys(spec)
    allpts = [q for pc in polys for q in pc["pts"]]
    extent = (min(q[0] for q in allpts), max(q[0] for q in allpts), min(q[1] for q in allpts), max(q[1] for q in allpts))
    opaque = [pc["pts"] for pc in polys if pc["call"] in ("B", "CB", "RB")]
    # ★ 맞춤은 앞층만으로 한다(먼 쪽 챙은 머리 뒤라 머리를 못 덮는다 — M17 은 불투명 조각 전부의 합집합으로 쟀다). u 하한 = 챙/몸 폭 목표.
    front_only = []
    for pc in polys:
        if pc["call"] not in ("B", "CB", "RB"): continue
        if split_brim and pc["name"] == "Brim": front_only.append(split_lens(pc["pts"])[2])
        else: front_only.append(pc["pts"])
    fit = fit_icon(front_only, extent, u_range=(u_min, 0.150, 0.001))
    if fit is None: raise SystemExit("모자 맞춤 실패 %s" % kind)
    strict = evaluate_icon(opaque, extent, fit["u"], fit["dy"])           # 대조: M17 규칙(전부 합집합)으로 본 착용선
    fx = lambda x: (x - 32.0) * fit["u"]; fy = lambda y: fit["dy"] - y * fit["u"]
    T = lambda pts: [(fx(x), fy(y)) for x, y in pts]
    tr = "HAT_FIT u=%.4f ky=1.00 dy=%.4f (H-2)" % (fit["u"], fit["dy"])
    body, card = [], []
    for pc in polys:
        cr = pc["crole"]; nm = "%s.%s" % (kind, pc["src"]); filled = pc["call"] in ("B", "F", "CB")
        loop = pc["closed"]
        card.append(C(kind, nm, pc["src"], pc["pts"], loop=loop, filled=filled, crole=cr, mult=pc["mult"], role=pc["role"]))
        if split_brim and pc["name"] == "Brim":
            far_f, far_a, near_f, near_a = split_lens(pc["pts"])
            body.append(P(kind, nm + ".far", pc["src"] + "far", T(far_f), filled=True, crole=(cr[0], None), layer="back", role="챙 먼 쪽(뒤층)", transform=tr, call="B"))
            body.append(P(kind, nm + ".farArc", pc["src"] + "fa", T(far_a), loop=False, crole=(None, cr[1]), layer="back", role="챙 먼 쪽 윤곽(뒤층)", transform=tr, call="B"))
            body.append(P(kind, nm + ".near", pc["src"] + "near", T(near_f), filled=True, crole=(cr[0], None), layer="front", role="챙 가까운 쪽(앞층)", transform=tr, call="B"))
            body.append(P(kind, nm + ".nearArc", pc["src"] + "na", T(near_a), loop=False, crole=(None, cr[1]), layer="front", role="챙 가까운 쪽 윤곽(앞층)", transform=tr, call="B"))
            continue
        body.append(P(kind, nm, pc["src"], T(pc["pts"]), loop=loop, filled=filled, crole=cr, mult=pc["mult"], role=pc["role"], transform=tr, call=pc["call"]))
    return body, card, fit, strict

STRAW_BODY, STRAW_CARD, STRAW_FIT, STRAW_STRICT = hat_pieces("straw", "equip.head.straw", STRAW_ICON, True, 0.075)   # u ≥ 0.075 → 챙 ≥ 4.5 R(중절모 4.06 보다 넓게)
BERET_BODY, BERET_CARD, BERET_FIT, BERET_STRICT = hat_pieces("beret", "equip.head.beret", BERET_ICON, False, 0.070)   # u ≥ 0.070 → 몸 폭 ≥ 3.0 R(머리 2.37 의 1.27 배)

# ============================================================================
# 2. (나) 뿔테안경 — 동그란안경 문법(EYES 64u · 테 선 M · 유리 = 카드 M2 워시 α0.16 / 몸 GLASS) + 굵은 윗바(M 채움 + 잉크)
# ============================================================================
BROWLINE_ICON = [
    ("B", "Bar", "M6 18 H58 Q60.5 18 60 21.5 L59 29 H36.5 Q32 27 27.5 29 H5 L4 21.5 Q3.5 18 6 18 Z", ("M", "INK"), 1.0, "눈썹 바(M DarkLens 채움 + 잉크 윤곽) 11u — 바 가운데 홈이 코다리"),
    ("B", "LensL", "M9 29 L28 29 Q28.5 37.5 25 40 L12 40 Q8.5 37.5 9 29 Z", (("CARDWASH", 0.16), "M"), 1.0, "왼 렌즈(카드 M2 워시 α0.16 / 몸 GLASS) · 테 M 선"),
    ("B", "LensR", "M36 29 L55 29 Q55.5 37.5 52 40 L39 40 Q35.5 37.5 36 29 Z", (("CARDWASH", 0.16), "M"), 1.0, "오른 렌즈"),
    ("S", "TempleL", "M4.5 22 Q1 23 0.5 30", (None, "M"), 1.0, "왼 다리(독립선 M) 8u"),
    ("S", "TempleR", "M59.5 22 Q63 23 63.5 30", (None, "M"), 1.0, "오른 다리"),
    ("H", "HL", "M11 35.5 Q12.5 31 19 30.5", (None, "W"), 0.75, "하이라이트(왼 렌즈) 8u"),
    ("H", "HR", "M38 35.5 Q39.5 31 46 30.5", (None, "W"), 0.75, "하이라이트(오른 렌즈)"),
]
def browline_pieces():
    fx, fy, sx = icon_xf("eyes")
    body, card = [], []
    for pc in icon_polys(BROWLINE_ICON):
        filled = pc["call"] == "B"; nm = "browline.%s" % pc["src"]
        card.append(C("browline", nm, pc["src"], pc["pts"], loop=pc["closed"], filled=filled, crole=pc["crole"], mult=pc["mult"], role=pc["role"]))
        tr = "" if not (isinstance(pc["crole"][0], tuple)) else "[E-2] 몸 채움 = GLASS(잉크 위 M2 α0.16 사전 합성, 불투명) · 테 M"
        body.append(P("browline", nm, pc["src"], [(fx(x), fy(y)) for x, y in pc["pts"]], loop=pc["closed"], filled=filled, crole=pc["crole"],
                      mult=pc["mult"], slot="eyes", role=pc["role"], transform=tr, call=pc["call"]))
    return body, card
BROWLINE_BODY, BROWLINE_CARD = browline_pieces()

# ============================================================================
# 3. (나) 판초 — 망토 무대 문법(뒤판 뒤층 + 앞 조각 앞층)이되 짧은망토와 갈린다: 넓고(밑단 3.20 R) 짧고(−4.20 R) 곧은 단 + 술 4 + V 앞자락(걸쇠 없음)
# ============================================================================
PONCHO_HEM, PONCHO_HEM_HW, PONCHO_TOP_HW, PONCHO_TOP_Y = -4.20, 1.60, 0.75, -1.10
def _collar_curve():
    pts, _ = handoff.flatten_path("M74 72 Q100 88 126 72")            # 인계본 공통 칼라 윗변(무대 200×240)
    return [handoff.stage_to_R(x, y) for x, y in pts]
PONCHO_FRINGE_X = (-1.20, -0.45, 0.45, 1.20)
PONCHO_FRINGE_LEN = 0.36
def poncho_pieces():
    kind = "poncho"
    back = [(-PONCHO_TOP_HW, PONCHO_TOP_Y), (-PONCHO_HEM_HW, PONCHO_HEM), (-0.80, PONCHO_HEM - 0.09), (0.0, PONCHO_HEM - 0.13),
            (0.80, PONCHO_HEM - 0.09), (PONCHO_HEM_HW, PONCHO_HEM), (PONCHO_TOP_HW, PONCHO_TOP_Y)]
    bib = _collar_curve() + [(0.0, -2.40)]                                # 칼라 곡선(왼→오) → V 끝점 → 닫힘
    body = [P(kind, "poncho.StageBack", "STB", back, filled=True, crole=("M", "INK"), layer="back", slot="back", role="뒤판(M Wool + 잉크, 뒤층) — 곧은 단·넓은 폭", call="STAGE")]
    for i, x in enumerate(PONCHO_FRINGE_X):
        body.append(P(kind, "poncho.Fringe%d" % i, "FR%d" % i, [(x, PONCHO_HEM - 0.11 * (1 - abs(x) / 1.6)), (x, PONCHO_HEM - 0.11 * (1 - abs(x) / 1.6) - PONCHO_FRINGE_LEN)],
                       loop=False, crole=(None, "M2"), layer="back", slot="back", role="단 술(독립선 M2 Leather)", call="S"))
    body.append(P(kind, "poncho.StageBib", "STC", bib, filled=True, crole=("M2", "INK"), layer="front", slot="back", role="앞자락(M2 Leather + 잉크, 앞층) — 칼라 곡선 + V(−2.40 R)", call="STAGE"))
    body.append(P(kind, "poncho.H", "H", M20.highlight_arc(-0.95, -1.75, 0.55, 95, 150, 8), loop=False, crole=(None, "W"), layer="back", slot="back", mult=0.75, role="하이라이트(뒤판 왼쪽 어깨)", call="H"))
    # 카드(64u BACK 슬롯 프레이밍) — 같은 물건을 정면 아이콘으로: 담요 사다리꼴 + V 앞자락 + 술 4 + 하이라이트
    card = [C(kind, "poncho.B0", "B0", path("M20 12 L8 50 Q32 54 56 50 L44 12 Z")[0], filled=True, crole=("M", "INK"), role="담요"),
            C(kind, "poncho.B1", "B1", path("M19 12 Q32 21 45 12 L32 32 Z")[0], filled=True, crole=("M2", "INK"), role="V 앞자락"),
            C(kind, "poncho.S2", "S2", [(14, 51), (14, 57)], loop=False, crole=(None, "M2"), role="술"),
            C(kind, "poncho.S3", "S3", [(26, 53), (26, 59)], loop=False, crole=(None, "M2"), role="술"),
            C(kind, "poncho.S4", "S4", [(38, 53), (38, 59)], loop=False, crole=(None, "M2"), role="술"),
            C(kind, "poncho.S5", "S5", [(50, 51), (50, 57)], loop=False, crole=(None, "M2"), role="술"),
            C(kind, "poncho.H6", "H6", path("M17 22 Q14 30 13 40")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    return body, card
PONCHO_BODY, PONCHO_CARD = poncho_pieces()

# ============================================================================
# 4. (다) 머리카락 6 — 정면 기하(머리 중심 원점 R). 앞층 = 머리 링 위(SortHair) · 뒤층 = 머리·몸 뒤(SortBack). 모자 커버선은 그대로 자른다.
# ============================================================================
HAIR_TOP_LIMIT = 1.75                     # 옛 verify HAIR 액자(HairCapMaxRatio)
def hairline(xl=-1.02, yl=0.44, mid=0.62, n=5):
    """이마선(왼 관자 → 오른 관자, 안쪽으로 볼록): 눈(y 0.09 ± 0.14 + 윤곽 0.09)보다 위."""
    out = []
    for i in range(n):
        t = i / (n - 1) * 2 - 1
        out.append((-xl * t, yl + (mid - yl) * (1 - t * t)))
    return out                            # (−1.02,0.44) … (0,0.62) … (1.02,0.44)  ← x 는 xl 부호 반전이라 왼→오
def dome(cap, a0, a1, n=13, wobble=None):
    out = []
    for i in range(n):
        a = a0 + (a1 - a0) * i / (n - 1); r = cap if wobble is None else wobble(i, cap)
        out.append(rig.polar(a, r))
    return out
def strand(x0, y0, x1, y1, x2, y2, n=7):
    pts, _ = handoff.flatten_path("M%g %g Q%g %g %g %g" % (x0, y0, x1, y1, x2, y2), n)
    return pts

def hair_common(kind, cap, sideburn=(1.30, -0.15), cap_pts=None):
    """앞층 덩어리: 이마선(안쪽) + 관자에서 바깥·아래로 내려오는 구레나룻 + 돔(바깥, 오른 → 왼)."""
    hl = hairline()
    right_in = [(1.14, 0.22), (sideburn[0], sideburn[1])]
    right_out = [(sideburn[0] + 0.22, sideburn[1] + 0.06)]
    d = cap_pts if cap_pts is not None else dome(cap, 8, 172)
    left_out = [(-(sideburn[0] + 0.22), sideburn[1] + 0.06)]
    left_in = [(-sideburn[0], sideburn[1]), (-1.14, 0.22)]
    return hl + right_in + right_out + d + left_out + left_in

def hair_cowlick():
    kind = "cowlick"
    def wob(i, cap): return cap + (0.10 if i % 2 == 0 else -0.10)
    mass = hair_common(kind, 1.50, cap_pts=dome(1.50, 8, 172, 11, wob))
    crest = [rig.polar(50, 1.40), rig.polar(63, 1.74), rig.polar(72, 1.74), rig.polar(86, 1.40)]
    return [P(kind, "cowlick.Mass", "B0", mass, filled=True, crole=("M", "INK"), slot="head", role="덩어리(M HairBrown + 잉크) — 톱니 실루엣", call="B"),
            P(kind, "cowlick.Crest", "B1", crest, filled=True, crole=("M2", "INK"), slot="head", role="삐침(M2 HairBowlLit + 잉크) — 유독 뻗친 하나, 끝 1.74 R(사다리꼴: 1-C 통과 최소 두께)", call="B"),
            P(kind, "cowlick.S2", "S2", strand(-0.55, 1.28, -0.35, 1.05, -0.30, 0.78), loop=False, crole=(None, "INK"), slot="head", mult=0.8, line_alpha=0.45, role="결(잉크 α0.45)", call="S"),
            P(kind, "cowlick.H3", "H3", M20.highlight_arc(0, 0, 1.22, 112, 150, 7), loop=False, crole=(None, "W"), slot="head", mult=0.75, role="하이라이트", call="H")]

def hair_neat():
    kind = "neat"
    mass = hair_common(kind, 1.54, sideburn=(1.26, -0.10))
    fall = [(-1.56, 0.40), (-1.52, -0.80), (-1.30, -2.00), (-0.95, -2.05), (-0.98, -1.20), (-0.90, -0.30), (-0.60, 0.30), (0.60, 0.30), (0.90, -0.30),
            (0.98, -1.20), (0.95, -2.05), (1.30, -2.00), (1.52, -0.80), (1.56, 0.40), (0.80, 1.00), (0.0, 1.20), (-0.80, 1.00)]
    part = [(-0.18, 1.53), (0.18, 1.53), (0.18, 0.62), (-0.18, 0.62)]
    return [P(kind, "neat.Fall", "B0", fall, filled=True, crole=("M", "INK"), layer="back", slot="head", role="늘어진 머리(M HairBowl + 잉크, 뒤층) — 어깨까지 −2.05 R", call="B"),
            P(kind, "neat.Mass", "B1", mass, filled=True, crole=("M", "INK"), slot="head", role="덩어리(앞층)", call="B"),
            P(kind, "neat.Part", "B2", part, filled=True, crole=("M2", None), slot="head", role="가르마(M2 HairBowlLit, 윤곽 없음 — 인계본 F 문법) 정수리→이마, 폭 0.36 R = 2pt(1-C 최소)", call="F"),
            P(kind, "neat.S3", "S3", strand(0.55, 1.30, 0.75, 1.05, 0.80, 0.72), loop=False, crole=(None, "INK"), slot="head", mult=0.8, line_alpha=0.45, role="결(잉크 α0.45)", call="S"),
            P(kind, "neat.H4", "H4", M20.highlight_arc(0, 0, 1.26, 112, 150, 7), loop=False, crole=(None, "W"), slot="head", mult=0.75, role="하이라이트", call="H")]

def hair_curly():
    kind = "curly"
    def wob(i, cap): return cap + (0.09 if i % 2 == 0 else -0.09)
    mass = hair_common(kind, 1.56, sideburn=(1.34, -0.55), cap_pts=dome(1.56, 8, 172, 17, wob))
    coil = rig.poly(1.48, -0.78, 0.27, 8, 22.5)
    return [P(kind, "curly.Mass", "B0", mass, filled=True, crole=("M", "INK"), slot="head", role="덩어리(M HairRed + 잉크) — 물결 실루엣, 옆머리 −0.55 R", call="B"),
            P(kind, "curly.Coil", "CB1", coil, filled=True, crole=("M2", "INK"), slot="head", role="컬(M2 HairRedLit + 잉크) — 오른 옆머리 끝", call="CB"),
            P(kind, "curly.S2", "S2", strand(-0.62, 1.20, -0.30, 1.16, -0.20, 0.86), loop=False, crole=(None, "INK"), slot="head", mult=0.8, line_alpha=0.45, role="결(잉크 α0.45)", call="S"),
            P(kind, "curly.H3", "H3", M20.highlight_arc(0, 0, 1.28, 110, 148, 7), loop=False, crole=(None, "W"), slot="head", mult=0.75, role="하이라이트", call="H")]

def hair_bald():
    kind = "bald"
    tuft = [(-1.02, 0.26), (-1.30, 0.36), (-1.44, 0.06), (-1.38, -0.30), (-1.10, -0.36), (-1.04, -0.05)]
    return [P(kind, "bald.TuftL", "B0", tuft, filled=True, crole=("M", "INK"), slot="head", role="왼 귀 옆 머리(M HairBald + 잉크)", call="B"),
            P(kind, "bald.TuftR", "B1", [(-x, y) for x, y in reversed(tuft)], filled=True, crole=("M", "INK"), slot="head", role="오른 귀 옆 머리", call="B"),
            P(kind, "bald.S2", "S2", strand(0.02, 1.20, 0.06, 1.60, 0.42, 1.64), loop=False, crole=(None, "M2"), slot="head", mult=1.0, role="정수리 한 가닥(독립선 M2 TintHead) — 「없는 것」의 표식", call="S"),
            P(kind, "bald.H3", "H3", M20.highlight_arc(-1.22, -0.02, 0.22, 60, 170, 7), loop=False, crole=(None, "W"), slot="head", mult=0.75, role="하이라이트(왼 머리)", call="H")]

def hair_bowl():
    kind = "bowl"
    cap = 1.58
    a0 = math.degrees(math.asin(-0.55 / cap)); a1 = 180 - a0
    d = dome(cap, a0, a1, 15)
    mass = [(-1.05, 0.40), (1.05, 0.40), (1.08, -0.55)] + [d[0]] + d[1:-1] + [d[-1]] + [(-1.08, -0.55)]
    fringe = [(-1.02, 0.40), (1.02, 0.40), (1.02, 0.84), (-1.02, 0.84)]
    return [P(kind, "bowl.Mass", "B0", mass, filled=True, crole=("M", "INK"), slot="head", role="덩어리(M HairBowl + 잉크) — 귀를 덮고 −0.55 R 에서 곧게 자른 단발", call="B"),
            P(kind, "bowl.Fringe", "B1", fringe, filled=True, crole=("M2", "INK"), slot="head", role="앞머리 띠(M2 HairBowlLit + 잉크) +0.40~+0.84 R", call="B"),
            P(kind, "bowl.S2", "S2", strand(-0.90, 1.05, -0.70, 1.28, -0.30, 1.40), loop=False, crole=(None, "INK"), slot="head", mult=0.8, line_alpha=0.45, role="결(잉크 α0.45)", call="S"),
            P(kind, "bowl.H3", "H3", M20.highlight_arc(0, 0, 1.30, 112, 150, 7), loop=False, crole=(None, "W"), slot="head", mult=0.75, role="하이라이트", call="H")]

def hair_ponytail():
    kind = "ponytail"
    mass = hair_common(kind, 1.50, sideburn=(1.28, -0.12))
    tail = [(0.55, 1.45), (0.80, 1.73), (1.12, 1.70), (1.46, 1.20), (1.64, 0.40), (1.58, -0.60), (1.28, -0.88), (1.12, -0.20), (1.18, 0.60), (0.98, 1.22)]
    tie = rig.poly(0.62, 1.34, 0.19, 10)
    return [P(kind, "ponytail.Tail", "B0", tail, filled=True, crole=("M", "INK"), layer="back", slot="head", role="묶음(M HairRed + 잉크, 뒤층) — 정수리 오른쪽에서 솟아 뒤로 떨어진다", call="B"),
            P(kind, "ponytail.Mass", "B1", mass, filled=True, crole=("M", "INK"), slot="head", role="덩어리(앞층)", call="B"),
            P(kind, "ponytail.Tie", "CB2", tie, filled=True, crole=("M2", None), slot="head", role="머리끈(M2 HairRedLit, 윤곽 없음 r 0.19 R)", call="CB"),
            P(kind, "ponytail.S3", "S3", strand(-0.50, 1.24, -0.28, 1.06, -0.24, 0.80), loop=False, crole=(None, "INK"), slot="head", mult=0.8, line_alpha=0.45, role="결(잉크 α0.45)", call="S"),
            P(kind, "ponytail.H4", "H4", M20.highlight_arc(0, 0, 1.22, 112, 150, 7), loop=False, crole=(None, "W"), slot="head", mult=0.75, role="하이라이트", call="H")]

HAIR = {"cowlick": hair_cowlick(), "neat": hair_neat(), "curly": hair_curly(), "bald": hair_bald(), "bowl": hair_bowl(), "ponytail": hair_ponytail()}
HAIR_ID = {"cowlick": "look.hair.cowlick", "neat": "look.hair.neat", "curly": "look.hair.curly", "bald": "look.hair.bald", "bowl": "look.hair.bowl", "ponytail": "look.hair.ponytail"}
HAIR_KO = {"cowlick": "삐친머리", "neat": "단정한머리", "curly": "곱슬머리", "bald": "민머리", "bowl": "바가지머리", "ponytail": "포니테일"}
def hair_card(kind):
    """카드 = 같은 좌표를 HAIR 프레이밍(1 R = 16u, 머리 중심 (32,30))으로. 층은 카드에서 순서(뒤층 먼저)로만 남는다."""
    out = []
    for p in HAIR[kind]:
        out.append(C(kind, p.name, p.src, [hair_to_icon(q) for q in p.pts], loop=p.loop, filled=p.filled, crole=p.crole, mult=p.mult, role=p.role, line_alpha=line_alpha(p) if p.crole[1] == "INK" else 1.0))
    return out
HAIR_CARD = {k: hair_card(k) for k in HAIR}

# ============================================================================
# 5. (다) FX 6 · PET 6 — 카드 64u 아이콘만(M 채움 + 카드 잉크 윤곽 + 하이라이트). 몸 효과·펫 본체는 appearance.py 그대로.
# ============================================================================
def fxpet_cards():
    D = {}
    k = "fx_none"; D[k] = [
        C(k, "fxnone.CB0", "CB0", circ(32, 32, 16), crole=(None, "INK"), role="빈 고리(잉크 선) — 「없음」"),
        C(k, "fxnone.S1", "S1", [(21.5, 42.5), (42.5, 21.5)], loop=False, crole=(None, "INK"), role="빗금")]
    k = "fx_footprint"; D[k] = [
        C(k, "footprint.B0", "B0", path("M24 10 Q36 6 40 16 Q43 26 37 34 Q33 40 31 46 Q28 56 20 54 Q13 50 17 40 Q21 32 20 22 Q19 13 24 10 Z")[0], filled=True, crole=("M", "INK"), role="발바닥(M InkTone + 잉크)"),
        C(k, "footprint.CB1", "CB1", circ(43, 7, 3.5), filled=True, crole=("M", "INK"), role="엄지발가락"),
        C(k, "footprint.S2", "S2", path("M19 36 Q28 33 36 36")[0], loop=False, crole=(None, "INK"), line_alpha=0.5, role="발 허리 주름(잉크 α0.5)"),
        C(k, "footprint.H3", "H3", path("M24 20 Q25 15 29 13")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    k = "fx_sparkle"; D[k] = [
        C(k, "sparkle.B0", "B0", [(30, 6), (34.5, 27.5), (56, 32), (34.5, 36.5), (30, 58), (25.5, 36.5), (4, 32), (25.5, 27.5)], filled=True, crole=("M", "INK"), role="큰 별(M Gold + 잉크)"),
        C(k, "sparkle.CF1", "CF1", [(52, 4), (54, 11), (61, 13), (54, 15), (52, 22), (50, 15), (43, 13), (50, 11)], filled=True, crole=("M2", None), role="작은 별(M2 GoldLight, 윤곽 없음)"),
        C(k, "sparkle.S2", "S2", [(8, 52), (13, 52)], loop=False, crole=(None, "M2"), mult=1.2, role="빛 점(독립선 M2)"),
        C(k, "sparkle.H3", "H3", path("M23 22 Q25 17 29 15")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    k = "fx_dust"; D[k] = [
        C(k, "dust.B0", "B0", path("M10 44 Q4 32 18 30 Q20 16 34 20 Q48 12 52 28 Q62 30 58 44 Z")[0], filled=True, crole=("M", "INK"), role="먼지구름(M InkDimTone + 잉크)"),
        C(k, "dust.CB1", "CB1", circ(13, 52, 4.5), filled=True, crole=("M2", "INK"), role="작은 뭉치(M2 InkTone)"),
        C(k, "dust.CB2", "CB2", circ(53, 53, 3.5), filled=True, crole=("M2", "INK"), role="작은 뭉치"),
        C(k, "dust.H3", "H3", path("M18 36 Q20 30 26 27")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    k = "fx_bubble"; D[k] = [
        C(k, "bubble.CB0", "CB0", circ(30, 29, 21), filled=True, crole=("M", "INK"), role="큰 방울(M Blue + 잉크)"),
        C(k, "bubble.CB1", "CB1", circ(53, 50, 6), filled=True, crole=("M2", "INK"), role="작은 방울(M2 DarkLens)"),
        C(k, "bubble.CB2", "CB2", circ(11, 52, 4), filled=True, crole=("M2", "INK"), role="작은 방울"),
        C(k, "bubble.H3", "H3", path("M16 26 Q17 15 27 11")[0], loop=False, crole=(None, "W"), mult=1.0, role="하이라이트(긴 광택 — 방울의 정체)")]
    k = "fx_leaf"; D[k] = [
        C(k, "leaf.B0", "B0", path("M8 54 Q8 26 28 12 Q50 2 60 6 Q58 38 34 50 Q20 56 8 54 Z")[0], filled=True, crole=("M", "INK"), role="잎(M TintNeck + 잉크)"),
        C(k, "leaf.S1", "S1", path("M12 51 Q30 34 56 12")[0], loop=False, crole=(None, "INK"), line_alpha=0.45, role="잎맥(잉크 α0.45)"),
        C(k, "leaf.S2", "S2", [(8, 54), (3, 61)], loop=False, crole=(None, "M2"), mult=1.2, role="잎자루(독립선 M2 Gold ×1.2)"),
        C(k, "leaf.H3", "H3", path("M16 42 Q19 32 27 26")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    k = "pet_ball"; D[k] = [
        C(k, "ball.CB0", "CB0", circ(32, 32, 23), filled=True, crole=("M", "INK"), role="공(M Toy + 잉크)"),
        C(k, "ball.S1", "S1", path("M32 9 Q47 32 32 55")[0], loop=False, crole=(None, "M2"), mult=1.1, role="솔기(독립선 M2 Paper) — 구(球)의 큰 원"),
        C(k, "ball.S2", "S2", path("M32 9 Q17 32 32 55")[0], loop=False, crole=(None, "M2"), mult=1.1, role="솔기"),
        C(k, "ball.H3", "H3", path("M17 24 Q19 16 26 13")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    k = "pet_balloon"; D[k] = [
        C(k, "balloon.B0", "B0", path("M32 4 Q48 4 48 22 Q48 38 32 46 Q16 38 16 22 Q16 4 32 4 Z")[0], filled=True, crole=("M", "INK"), role="풍선(M Toy + 잉크)"),
        C(k, "balloon.B1", "B1", [(27.5, 45), (36.5, 45), (32, 51)], filled=True, crole=("M2", "INK"), role="매듭(M2 Paper + 잉크)"),
        C(k, "balloon.S2", "S2", path("M32 51 Q26 55 32 59 Q38 62 32 64")[0], loop=False, crole=(None, "M2"), role="줄(독립선 M2)"),
        C(k, "balloon.H3", "H3", path("M22 18 Q23 10 29 8")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    k = "pet_cursor"; D[k] = [
        C(k, "cursor.B0", "B0", [(16, 8), (16, 46), (25, 38), (32, 54), (39, 51), (32, 36), (44, 36)], filled=True, crole=("M", "INK"), role="화살표(M Blue + 잉크)"),
        C(k, "cursor.S1", "S1", [(12, 5), (6, 1)], loop=False, crole=(None, "M2"), mult=1.2, role="클릭 빛(독립선 M2 Paper ×1.2)"),
        C(k, "cursor.S2", "S2", [(11, 13), (4, 13)], loop=False, crole=(None, "M2"), mult=1.2, role="클릭 빛"),
        C(k, "cursor.H3", "H3", path("M19 14 Q19.5 20 21 26")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    k = "pet_mini"; D[k] = [
        C(k, "mini.CB0", "CB0", circ(32, 13, 9), filled=True, crole=("INKF", "INK"), role="머리(잉크 표식 — 카드 잉크 채움)"),
        C(k, "mini.S1", "S1", [(32, 22), (32, 40)], loop=False, crole=(None, "INK"), mult=1.4, role="몸통(잉크 ×1.4)"),
        C(k, "mini.S2", "S2", [(17, 37), (32, 28), (47, 37)], loop=False, crole=(None, "INK"), mult=1.4, role="팔"),
        C(k, "mini.S3", "S3", [(32, 40), (21, 59)], loop=False, crole=(None, "INK"), mult=1.4, role="다리"),
        C(k, "mini.S4", "S4", [(32, 40), (43, 59)], loop=False, crole=(None, "INK"), mult=1.4, role="다리")]
    k = "pet_plane"; D[k] = [
        C(k, "plane.B0", "B0", [(60, 10), (4, 28), (24, 34)], filled=True, crole=("M", "INK"), role="윗날개(M Paper + 잉크)"),
        C(k, "plane.B1", "B1", [(60, 10), (24, 34), (34, 38), (30, 56)], filled=True, crole=("M2", "INK"), role="아랫날개·용골(M2 Silver + 잉크)"),
        C(k, "plane.S2", "S2", [(60, 10), (24, 34)], loop=False, crole=(None, "INK"), line_alpha=0.4, role="접은 선(잉크 α0.4)"),
        C(k, "plane.H3", "H3", path("M16 26 Q22 22 32 20")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    k = "pet_snail"; D[k] = [
        C(k, "snail.B0", "B0", path("M8 52 Q4 52 4 47 Q4 42 10 42 L48 42 Q56 42 58 34 L58 40 Q58 52 48 52 Z")[0], filled=True, crole=("M2", "INK"), role="발·머리(M2 Ivory + 잉크)"),
        C(k, "snail.CB1", "CB1", circ(29, 27, 17), filled=True, crole=("M", "INK"), role="껍데기(M Wool + 잉크)"),
        C(k, "snail.CB2", "CB2", circ(29, 27, 6.5), filled=True, crole=("M2", None), role="나선 심(M2, 윤곽 없음 — 색 경계)"),
        C(k, "snail.S3", "S3", [(54, 36), (58, 26)], loop=False, crole=(None, "M2"), mult=1.1, role="더듬이(독립선 M2)"),
        C(k, "snail.S4", "S4", [(50, 35), (49, 26)], loop=False, crole=(None, "M2"), mult=1.1, role="더듬이"),
        C(k, "snail.H5", "H5", path("M17 26 Q18 18 24 15")[0], loop=False, crole=(None, "W"), mult=0.75, role="하이라이트")]
    return D
FXPET_CARD = fxpet_cards()
FXPET_ID = {"fx_none": "look.fx.none", "fx_footprint": "look.fx.footprint", "fx_sparkle": "look.fx.sparkle", "fx_dust": "look.fx.dust", "fx_bubble": "look.fx.bubble", "fx_leaf": "look.fx.leaf",
            "pet_ball": "look.pet.ball", "pet_balloon": "look.pet.balloon", "pet_cursor": "look.pet.cursor", "pet_mini": "look.pet.mini", "pet_plane": "look.pet.plane", "pet_snail": "look.pet.snail"}
FXPET_KO = {"fx_none": "없음", "fx_footprint": "발자국", "fx_sparkle": "반짝임", "fx_dust": "먼지구름", "fx_bubble": "물방울", "fx_leaf": "나뭇잎",
            "pet_ball": "작은공", "pet_balloon": "풍선", "pet_cursor": "커서친구", "pet_mini": "리틀스틱메이트", "pet_plane": "종이비행기", "pet_snail": "달팽이"}
FXPET_BODY_MIRROR = {"fx_none": [], "fx_footprint": appearance.FX_NOW["발자국"], "fx_sparkle": appearance.FX_NOW["반짝임"], "fx_dust": appearance.FX_NOW["먼지"],
                     "fx_bubble": appearance.FX_NOW["물방울"], "fx_leaf": appearance.FX_NOW["나뭇잎"], "pet_ball": appearance.PET_NOW["작은공"], "pet_balloon": appearance.PET_NOW["풍선"],
                     "pet_cursor": appearance.PET_NOW["커서친구"], "pet_mini": [], "pet_plane": appearance.PET_NOW["종이비행기"], "pet_snail": appearance.PET_NOW["달팽이"]}

# ============================================================================
# 6. 묶음 — (나)(다) 몸/카드 사전
# ============================================================================
NA_ID = {"straw": "equip.head.straw", "beret": "equip.head.beret", "browline": "equip.eyes.browline", "poncho": "equip.shoulders.poncho"}
NA_KO = {"straw": "밀짚모자", "beret": "베레모", "browline": "뿔테안경", "poncho": "판초"}
NA_SLOT = {"straw": "HEAD", "beret": "HEAD", "browline": "EYES", "poncho": "BACK"}
BODY = {"straw": STRAW_BODY, "beret": BERET_BODY, "browline": BROWLINE_BODY, "poncho": PONCHO_BODY, **HAIR}
CARD = {"straw": STRAW_CARD, "beret": BERET_CARD, "browline": BROWLINE_CARD, "poncho": PONCHO_CARD, **HAIR_CARD, **FXPET_CARD}
ITEM_ID = {**NA_ID, **HAIR_ID, **FXPET_ID}
KO = {**NA_KO, **HAIR_KO, **FXPET_KO}
SLOT = {**NA_SLOT, **{k: "HAIR" for k in HAIR}, **{k: ("FX" if k.startswith("fx") else "PET") for k in FXPET_CARD}}
ORDER_NA = ["browline", "straw", "beret", "poncho"]
ORDER_HAIR = ["cowlick", "neat", "curly", "bald", "bowl", "ponytail"]
ORDER_FXPET = ["fx_none", "fx_footprint", "fx_sparkle", "fx_dust", "fx_bubble", "fx_leaf", "pet_ball", "pet_balloon", "pet_cursor", "pet_mini", "pet_plane", "pet_snail"]

# ============================================================================
# 7. 검산
# ============================================================================
def survival(p, scale=SHIP):
    return M16.survival(p, scale) if p.src != "EYE" else (True, "OK")

def card_rule(p, w=2.2):
    """카드 44px 규칙(아이콘 단위, w = 인계본 획 2.2u × 배수 무관하게 기본 획): 1-A 잉크 사각형 ≥ 1.5w · 꺾임-꺾임 변 ≥ 1.0w · 1-C ρ ≥ w(채움) · 자기교차."""
    v = rig.rule_one(rig.Shape(p.name, p.pts, loop=p.loop, filled=p.filled), w)
    if v: return False, "1-A: " + v
    if p.filled:
        rho = M16.rho_max(p.pts, step=0.25)
        if rho < w: return False, "1-C: ρ %.2fu < %.1fu" % (rho, w)
    if p.loop and rig.self_intersects(p.pts): return False, "자기교차"
    return True, "OK"

def eyes_e2():
    """E-2 복제(r20_eyescover 와 같은 자): 몸 표면 Filled 다각형 격자 커버리지 · 6종 쌍별 |A△B|/|A∪B| ≥ 0.20 · 눈 자리 덮음."""
    W = rig.W; CELL = W * 0.5; SPAN = 1.3; N = math.ceil(SPAN * 2 / CELL)
    def cells(fills):
        s = set()
        for i in range(N):
            for j in range(N):
                q = (-SPAN + CELL * (i + 0.5), -SPAN + CELL * (j + 0.5))
                if any(rig.contains(poly, q) for poly in fills): s.add((i, j))
        return s
    V = {k: [p.pts for p in M19.WORN[k] if p.filled] for k in ("sunglasses", "roundglasses", "goggles", "monocle")}
    V["browline"] = [p.pts for p in BROWLINE_BODY if p.filled]
    V["patch"] = [p.pts for p in M20.GA["patch"] if p.filled]
    keys = ["sunglasses", "roundglasses", "goggles", "monocle", "browline", "patch"]
    Cc = {k: cells(V[k]) for k in keys}
    pairs = []
    for a in range(6):
        for b in range(a + 1, 6):
            A, B = Cc[keys[a]], Cc[keys[b]]
            pairs.append((keys[a], keys[b], len(A ^ B) / max(1, len(A | B))))
    EYE_F, EYE_B = (rig.EYE_X, rig.EYE_Y), (-rig.EYE_X, rig.EYE_Y)
    vis = [p.pts for p in BROWLINE_BODY if p.filled]
    cover = (any(rig.contains(poly, EYE_F) for poly in vis), any(rig.contains(poly, EYE_B) for poly in vis))
    return {k: len(Cc[k]) for k in keys}, pairs, cover

def hair_checks():
    rows = []
    pupil = rig.PUPIL_R
    for k in ORDER_HAIR:
        ps = HAIR[k]; pts = [q for p in ps for q in p.pts]
        top = max(q[1] for q in pts); bottom = min(q[1] for q in pts)
        hit = []
        for p in ps:
            if not p.filled or p.layer != "front": continue
            for sx in (1, -1):
                for dx, dy in ((0, 0), (pupil, 0), (-pupil, 0), (0, pupil), (0, -pupil)):
                    if rig.contains(p.pts, (sx * rig.EYE_X + dx, rig.EYE_Y + dy)): hit.append(p.name)
        # 부착: 앞층 채움이 머리 원반 안(반경 < 1.0)에 점을 갖는가(이마선)
        attached = any(min(math.hypot(*q) for q in p.pts) < HEAD_R_SHIP - 1e-6 for p in ps if p.filled and p.layer == "front")   # 앞층 채움이 머리 잉크 원반(링 포함)에 닿는다
        # 모자 커버선(+0.06, 야구모자)으로 잘랐을 때 남는 조각 — y ≤ 0.06 인 점이 있는 조각
        below = [p.name for p in ps if any(q[1] <= 0.06 for q in p.pts)]
        rows.append(dict(kind=k, n=len(ps), back=sum(1 for p in ps if p.layer == "back"), top=top, bottom=bottom, eye_hit=sorted(set(hit)), attached=attached, below=below))
    prof = {k: rig.profile([p.as_shape() for p in HAIR[k]], 0.0) for k in ORDER_HAIR}
    pairs = []
    for i in range(6):
        for j in range(i + 1, 6):
            d = rig.max_delta(prof[ORDER_HAIR[i]], prof[ORDER_HAIR[j]]); pairs.append((ORDER_HAIR[i], ORDER_HAIR[j], d))
    return rows, pairs

def poncho_checks():
    stb = [p for p in PONCHO_BODY if p.src == "STB"][0]
    hem = min(q[1] for q in stb.pts); x0, y0, x1, y1 = rig.bounds(stb.pts)
    cape = M17.CAPE_BACK_PTS["shortcape"]; chem = min(q[1] for q in cape); cw = M17._hem_width(cape)
    bib = [p for p in PONCHO_BODY if p.src == "STC"][0]; bx0, by0, bx1, by1 = rig.bounds(bib.pts)
    import r13_bodyocclusion as B
    # 앞자락 ↔ 팔: 어깨 뿌리에서 팔 중심선을 따라 내려가며 앞자락(또는 인계본 망토 칼라)을 벗어나는 깊이 — 둘 다 팔 뿌리를 덮는 것은 같다
    def exit_depth(poly):
        a, b = B.ARMS[0]; last = None
        for t in np.linspace(0, 1, 401):
            q = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
            if rig.contains(poly, q): last = q[1]
        return last
    collar = [p for p in M16.WORN["shortcape"] if p.src == "STC"][0].pts
    crouch = M17.crouch_offset(1.0)
    return dict(hem=hem, width=x1 - x0, cape_hem=chem, cape_w=cw, flare=(2 * PONCHO_HEM_HW - 2 * PONCHO_TOP_HW) / (PONCHO_TOP_Y - PONCHO_HEM),
                bib=(bx0, by0, bx1, by1), bib_exit=exit_depth(bib.pts), collar_exit=exit_depth(collar), crouch_hem=hem + crouch)

def report(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    w("== 0. 자 == 머리 원반 %.4f R(출하) · 덮임 %.4f R(0.35 상한) · 1pt %.4f R · 옛 W %.4f R · 어깨선 %.4f · 액자 %.3f · 머리카락 액자 %.2f" % (HEAD_R_SHIP, HEAD_R_COVER, ONE_PT, W_OLD, SH, M17.TOP_LIMIT, HAIR_TOP_LIMIT))
    w()
    w("== 1. (나) 모자 2종 — H-2 맞춤 (u 0.001 · dy 0.02 격자, 목표 착용선 ≤ +0.30 · 꼭대기 < 2.551 · 밑 > −1.0) ==")
    for k, f, s in (("straw", STRAW_FIT, STRAW_STRICT), ("beret", BERET_FIT, BERET_STRICT)):
        w("   %-6s u %.4f(카드 ×%.2f) dy %+.3f · 착용선 %+.3f · 꼭대기 %+.3f · 밑 %+.3f · 폭 %.2f R   [앞층만의 합집합으로 맞춤 — 먼 쪽 챙 제외]" % (k, f["u"], f["u"] / M17.ICON_U_HEAD, f["dy"], f["wear"], f["top"], f["bottom"], f["width"]))
        w("          대조(M17 규칙: 불투명 조각 전부): 착용선 %s · %s" % (("%+.3f" % s["wear"]) if s["wear"] is not None else "없음", s["why"] or "규칙 통과"))
    fedora = M17.HAT_FIT["fedora"]
    w("   대조: 중절모(R17) u %.4f 폭 %.2f R — 밀짚모자 챙은 그보다 넓어야 「카테고리 최대 챙」이다 → %.2f R (%+.2f)" % (fedora["u"], fedora["width"], STRAW_FIT["width"], STRAW_FIT["width"] - fedora["width"]))
    for k in ("straw", "beret"):
        for p in BODY[k]:
            x0, y0, x1, y1 = rig.bounds(p.pts)
            w("      %-22s %-5s %-16s ×%.2f 잉크 %.2f×%.2fR  y[%+.2f,%+.2f] x[%+.2f,%+.2f]" % (p.name, p.layer, str(p.crole), p.mult, x1 - x0, y1 - y0, y0, y1, x0, x1))
    w()
    cnt, pairs, cover = eyes_e2()
    w("== 2. (나) 뿔테안경 — E-2 복제(격자 W/2 · ±1.3 R): 칸 수 · 쌍별 |A△B|/|A∪B|(문턱 0.20) · 눈 자리 ==")
    w("   칸: " + " · ".join("%s %d" % (k, v) for k, v in cnt.items()))
    worst = min(d for _, _, d in pairs)
    for a, b, d in pairs:
        if "browline" in (a, b): w("   %-13s vs %-13s %.2f%s" % (a, b, d, "" if d >= 0.20 else " ★ < 0.20"))
    w("   6종 쌍별 최소 %.2f (뿔테 관련 최소 %.2f) · 앞눈 덮음 %s · 뒤눈 덮음 %s (양안: 둘 다 True)" % (worst, min(d for a, b, d in pairs if "browline" in (a, b)), cover[0], cover[1]))
    fx, fy, sx = icon_xf("eyes")
    w("   슬롯 변환 EYES: 1u = %.4f R · 눈 y %+.3f R = 아이콘 y %.1fu · 눈 x ±%.3f R = 아이콘 x %.1f/%.1fu" % (sx, rig.EYE_Y, (handoff.HEAD_CY_PX - 38.0 - rig.EYE_Y * handoff.HEAD_R_PX) / 0.75, rig.EYE_X, 32 - rig.EYE_X / sx, 32 + rig.EYE_X / sx))
    for p in BROWLINE_BODY:
        x0, y0, x1, y1 = rig.bounds(p.pts)
        w("      %-22s %-16s ×%.2f 실폭 %.4f 잉크 %.2f×%.2fR  x[%+.2f,%+.2f] y[%+.2f,%+.2f]" % (p.name, str(p.crole), p.mult, p.width_R(), x1 - x0, y1 - y0, x0, x1, y0, y1))
    w()
    pc = poncho_checks()
    w("== 3. (나) 판초 — B-1/B-2/B-4 ==")
    w("   뒤판 밑단 %.3f R · 폭 %.3f R · 플레어 %.4f R/R  |  새 짧은망토: 밑단 %.3f · 폭 %.3f (플레어 %.4f)  → 판초는 %.2f R 짧고 %.2f R 넓다(곧은 단 + 술 4)" % (
        pc["hem"], pc["width"], pc["flare"], pc["cape_hem"], pc["cape_w"], M17.FLARE, pc["cape_hem"] - pc["hem"] if False else pc["hem"] - pc["cape_hem"], pc["width"] - pc["cape_w"]))
    w("   앞자락(앞층) x[%+.2f,%+.2f] y[%+.2f,%+.2f] — V 끝 −2.40 R · 팔 중심선이 앞자락을 벗어나는 깊이 y %+.3f R(어깨선 아래 %.2f R) · 인계본 망토 칼라(STC)는 %+.3f R(아래 %.2f R) — 팔 뿌리를 덮는 정도가 같다" % (
        pc["bib"][0], pc["bib"][2], pc["bib"][1], pc["bib"][3], pc["bib_exit"], SH - pc["bib_exit"], pc["collar_exit"], SH - pc["collar_exit"]))
    w("   웅크리기 최대(몸 −%.3f R)에서 밑단 %.3f R — 발목 %.3f 위(B-3 클램프 불필요)" % (-M17.crouch_offset(1.0), pc["crouch_hem"], M17.ANKLE_Y))
    for p in PONCHO_BODY:
        x0, y0, x1, y1 = rig.bounds(p.pts)
        w("      %-22s %-5s %-14s ×%.2f 잉크 %.2f×%.2fR" % (p.name, p.layer, str(p.crole), p.mult, x1 - x0, y1 - y0))
    w()
    rows, hp = hair_checks()
    w("== 4. (다) 머리카락 6 — 정면 기하: 액자 ≤ %.2f · 눈동자 침범 0 · 이마선 부착 · 커버선(+0.06) 아래 남는 조각 ==" % HAIR_TOP_LIMIT)
    for r in rows:
        w("   %-9s 조각 %d(뒤층 %d) 꼭대기 %+.3f(%s) 밑 %+.3f · 눈 침범 %s · 부착 %s · 커버선 아래 남는 조각 %d/%d: %s" % (
            r["kind"], r["n"], r["back"], r["top"], "안" if r["top"] <= HAIR_TOP_LIMIT + 1e-9 else "★ 초과", r["bottom"], r["eye_hit"] if r["eye_hit"] else "없음", "✓" if r["attached"] else "★", len(r["below"]), r["n"], ", ".join(r["below"])))
    worst = min(d for _, _, d in hp)
    for a, b, d in hp: w("   %-9s vs %-9s Δr %.3f R = %.2f 획(1pt) / %.2f 획(옛 W)" % (a, b, d, d / ONE_PT, d / W_OLD))
    w("   쌍별 최소 %.3f R = %.2f 획(1pt) / %.2f 획(옛 W, 하한 1.0)" % (worst, worst / ONE_PT, worst / W_OLD))
    w()
    w("== 5. 몸 조각 생존 (1-A · 1-C · 자기교차) — @0.75 / 1.00 / 0.35 ==")
    tot = 0; ok75 = 0
    for k in ORDER_NA + ORDER_HAIR:
        for p in BODY[k]:
            a, wa = survival(p, 0.75); b, _ = survival(p, 1.00); c, wc = survival(p, 0.35)
            tot += 1; ok75 += a
            w("   %-9s %-22s %-5s .75 %s  1.0 %s  .35 %s  %s" % (k, p.name, p.layer, "생존" if a else "소멸", "생존" if b else "소멸", "생존" if c else "소멸", "" if a else wa + ("" if c else "") ))
    w("   @0.75 생존 %d / %d" % (ok75, tot))
    w()
    w("== 6. 카드 조각 규칙 (64u · w = 2.2u: 1-A 잉크 ≥ 3.3u · 꺾임 변 ≥ 2.2u · 1-C ρ ≥ 2.2u · 자기교차) ==")
    tot = 0; okc = 0
    for k in ORDER_NA + ORDER_HAIR + ORDER_FXPET:
        for p in CARD[k]:
            a, why = card_rule(p); tot += 1; okc += a
            x0, y0, x1, y1 = rig.bounds(p.pts)
            w("   %-13s %-22s %-18s ×%.2f 잉크 %5.1f×%5.1fu  %s %s" % (k, p.name, str(p.crole), p.mult, x1 - x0, y1 - y0, "통과" if a else "★", "" if a else why))
    w("   통과 %d / %d" % (okc, tot))
    w()
    w("== 7. 조각 수 · 역할 (P 4~9 · H 1 · M2 조각 · 뒤층) ==")
    for k in ORDER_NA + ORDER_HAIR:
        ps = BODY[k]
        w("   %-9s 몸 %d (뒤층 %d · H %d · M2 %d · 독립선 %d) / 카드 %d" % (k, len(ps), sum(1 for p in ps if p.layer == "back"), sum(1 for p in ps if p.crole[1] == "W"),
                                                                    sum(1 for p in ps if p.crole[0] == "M2" or p.crole[1] == "M2"), sum(1 for p in ps if not p.filled and p.crole[1] in ("M", "M2")), len(CARD[k])))
    for k in ORDER_FXPET:
        ps = CARD[k]
        w("   %-13s 카드 %d (H %d · M2 %d)%s" % (k, len(ps), sum(1 for p in ps if p.crole[1] == "W"), sum(1 for p in ps if p.crole[0] == "M2" or p.crole[1] == "M2"), "  ← P<4: 「없음」/잉크 표식은 예외(사유 §14-12)" if len(ps) < 4 or k == "pet_mini" else ""))
    w()
    w("== 8. 색 해석 — 검은 잉크 몸 / 흰 잉크 몸 / 카드 ==")
    for k in ORDER_NA + ORDER_HAIR:
        for p in BODY[k]:
            fb, _, lb = resolve(ITEM_ID[k], p.crole, "body", PM.INK_BLACK); fw, _, lw = resolve(ITEM_ID[k], p.crole, "body", PM.INK_WHITE)
            w("   %-9s %-22s %-5s %-18s 검: %s/%s | 흰: %s/%s" % (k, p.name, p.layer, str(p.crole), fb, lb, fw, lw))
    for k in ORDER_NA + ORDER_HAIR + ORDER_FXPET:
        for p in CARD[k]:
            fc, fa, lc = resolve(ITEM_ID[k], p.crole, "card", PM.CARD_INK)
            w("   %-13s %-22s card  %-18s %s α%.2f / %s" % (k, p.name, str(p.crole), fc, fa, lc))

if __name__ == "__main__":
    report()
