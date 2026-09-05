# -*- coding: utf-8 -*-
"""R15 — 인계본 16종을 **안 D 데이터 모델**(strokeGrade · Tone 3 · surfaces)에 담아 두 표면으로 설계한다.
(design-equipment, 2026-09-05)

이 파일이 답하는 질문은 하나다 — 「인계본 조각 91개가 우리 데이터 모델 안에서 **어느 표면에 어떤 모양으로**
살아남는가」. 판정은 숫자(규칙 1-A/1-C, 획 등급별 실폭)로 하되, **최종 합격은 r15_compare_sheet.png 를
눈으로 보는 것**이다(§13-0). 이 파일의 숫자는 그 그림을 만든 재료이지 합격 증명이 아니다.

좌표계: 우리 R 단위(머리 중심 원점 · y 위로 · +x 진행 방향). 인계본 → R 변환은 handoff.icon_to_R
(앵커 A/B 교정 완료, handoff_cal.py)를 **그대로** 쓴다 — 새 변환식을 만들지 않았다.

  CARD[kind]  = 인계본 91조각 전량을 R 단위로 옮긴 것(정면 도형, 레이어 전부). 표면 = Card.
  BODY[kind]  = 프로덕션 거울(items.py)의 3/4 도형 + 인계본 레이어(띠·하이라이트·결·정맥·손잡이·걸쇠)를
                조각 단위 상자 아핀으로 얹은 것. 각 조각을 획 등급 실폭으로 규칙 1-A/1-C에 걸어
                **살면 Body 비트를 켜고, 죽으면 Card 전용으로 내린다.**
  SHARED[kind]= 「좌표 한 벌」 변형 — BODY 기하 위에 카드 전용 조각까지 전부 얹은 것. 카드가 이 기하를
                쓰면 어떻게 보이는지(§13-4 데이터 계약 판정용).

★ 교정: 조각 총수 91 / H 17 / 인계본 잉크값 7건(r15_handoff_geom.py 와 같은 값)이 맞지 않으면 SystemExit.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, handoff
from rig import Shape

# ============================================================================
# 0. 획 등급(strokeGrade) — 안 D. 값은 전부 프로덕션 식에서 유도한다(숫자를 적어 두지 않는다)
# ============================================================================
BASE_STROKE = 0.048          # AccessoryShapeBuilder.BaselineStrokeWidth
HEAD_R_BASE = 0.22           # BaselineHeadVisualRadius
PT = 1.0 / (846.0 / 24.0)    # 1pt (베이스라인 유닛) = 1 / ReferencePointsPerWorldUnitApprox
SHIP = 0.75

# grade: (배수, 화면 하한 pt)
#   0 낱선(현행)  1 채움윤곽(현행)  2 디테일획(신설 ×0.75, 1pt)  3 굵은획(신설 ×1.5, 1pt)
GRADE = {0: (1.00, 2.0), 1: (1.00, 1.0), 2: (0.75, 1.0), 3: (1.50, 1.0)}
GRADE_NAME = {0: "낱선", 1: "채움윤곽", 2: "디테일", 3: "굵은획"}

def grade_width_R(grade, scale=SHIP):
    mult, floor_pt = GRADE[grade]
    return max(BASE_STROKE * mult * scale, floor_pt * PT) / (HEAD_R_BASE * scale)

W0 = grade_width_R(0)   # 0.343864
W1 = grade_width_R(1)   # 0.218182
W2 = grade_width_R(2)   # 0.171875 (디테일)
W3 = grade_width_R(3)   # 0.327273 (굵은획)
assert abs(W0 - 0.343864) < 1e-5 and abs(W1 - 0.218182) < 1e-5, (W0, W1)

BODY, CARD = 1, 2

# ============================================================================
# 1. 조각 — 안 D 필드를 전부 지닌다
# ============================================================================
class Piece:
    __slots__ = ("name", "pts", "loop", "filled", "tone", "grade", "surfaces",
                 "alpha", "line_alpha", "src", "note", "under")
    def __init__(self, name, pts, loop=True, filled=False, tone=0, grade=None,
                 surfaces=BODY | CARD, alpha=None, line_alpha=1.0, src="", note="", under=None):
        self.name = name
        self.pts = [(float(a), float(b)) for a, b in pts]
        self.loop, self.filled, self.tone = loop, filled, tone
        # 등급 기본값: 채움이면 채움윤곽(1), 아니면 낱선(0). 하이라이트(톤 3)는 디테일(2).
        self.grade = grade if grade is not None else (2 if tone == 3 else (1 if filled else 0))
        self.surfaces = surfaces
        self.alpha = alpha            # 카드 사전합성용 원본 채움 알파(None = 그라디언트 0.34→0.08 → 평균 0.21)
        self.line_alpha = line_alpha  # 낱선 알파(S 의 strokeOpacity)
        self.src, self.note, self.under = src, note, under

    def as_shape(self):
        return Shape(self.name, self.pts, loop=self.loop, filled=self.filled, tone=self.tone)

    def width_R(self, scale=SHIP):
        return grade_width_R(self.grade, scale)

    def on(self, surf):
        return bool(self.surfaces & surf)

    def __repr__(self):
        return "Piece(%s t%d g%d %s)" % (self.name, self.tone, self.grade,
                                          {1: "B", 2: "C", 3: "BC"}[self.surfaces])


def bounds(pts):
    return rig.bounds(pts)

def bbox_map(src_box, dst_box):
    """상자 → 상자 아핀(비등방 허용). 조각 단위 정합(§1-4-4 1단계)."""
    sx0, sy0, sx1, sy1 = src_box; dx0, dy0, dx1, dy1 = dst_box
    kx = (dx1 - dx0) / max(1e-9, sx1 - sx0); ky = (dy1 - dy0) / max(1e-9, sy1 - sy0)
    return lambda p: (dx0 + (p[0] - sx0) * kx, dy0 + (p[1] - sy0) * ky)

def uniform_map(src_box, dst_box):
    """등방 배율(작은 쪽) + 중심 정렬. 나침반이 필요 없는 소형 조각(걸쇠·손잡이)용."""
    sx0, sy0, sx1, sy1 = src_box; dx0, dy0, dx1, dy1 = dst_box
    k = min((dx1 - dx0) / max(1e-9, sx1 - sx0), (dy1 - dy0) / max(1e-9, sy1 - sy0))
    scx, scy = (sx0 + sx1) / 2, (sy0 + sy1) / 2; dcx, dcy = (dx0 + dx1) / 2, (dy0 + dy1) / 2
    return lambda p: (dcx + (p[0] - scx) * k, dcy + (p[1] - scy) * k)

def rho_max(pts, step=0.01):
    """채움의 최대 내접 반지름(규칙 1-C, AccessoryFillAreaRuleTests 와 같은 뜻). 격자 탐색."""
    x0, y0, x1, y1 = bounds(pts); best = 0.0; n = len(pts)
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

# ============================================================================
# 2. CARD — 인계본 91조각을 R 단위로 (레이어 전부)
# ============================================================================
KINDS = ["clothhat", "furhat", "fedora", "crown", "sunglasses", "roundglasses", "goggles", "monocle",
         "bowtie", "stripedtie", "scarf", "bellnecklace", "shortcape", "longcape", "wings", "backpack"]
KO = {k: handoff.OUR_NAME[k][1] for k in KINDS}
SLOT = {k: handoff.OUR_NAME[k][0] for k in KINDS}
MAT = {"shortcape", "longcape"}          # 재질색 강제(#D2402F), 그라디언트 1.0→0.62

_RAW = handoff.parse_items()
_PIECES = {k: handoff.build(k, _RAW[k]) for k in KINDS}

def _card_pieces(kind):
    fx, fy, s = handoff.icon_to_R(kind)
    out = []
    for i, pc in enumerate(_PIECES[kind]):
        pts = [(fx(x), fy(y)) for x, y in pc.pts]
        if pc.call == "H":
            out.append(Piece("%s.H%d" % (kind, i), pts, loop=False, filled=False, tone=3, grade=2,
                             surfaces=CARD, src="H%d" % i))
            continue
        # 인계본 8단계 배수 → 4등급 양자화: ×0.7/0.8/0.9 → 디테일(×0.75) · ×1.1 → 기본(×1.0) ·
        # ×1.4/1.5 → 굵은획(×1.5). 잔여 차이는 §13-3 아이템별 표에 적는다.
        mult = pc.stroke_w / handoff.STROKE
        grade = 1 if pc.filled else 0
        if mult < 0.95: grade = 2
        elif mult > 1.15: grade = 3
        tone = 1 if pc.is_accent else 0
        alpha = pc.fill_opacity if pc.filled else None
        # `{ fill: A }` 만 있고 fillOpacity 가 없는 B/CB/RB(왕관 봉우리 원 3 · 긴망토 걸쇠 · 외알안경 끝 구슬 ·
        # 방울 추)는 **불투명 강조색**이다 — 그라디언트(None)로 읽으면 워시가 되어 첫 시트에서 검게 나왔다.
        if pc.filled and alpha is None and pc.is_accent and pc.call in ("B", "CB", "RB"):
            alpha = 1.0
        la = handoff._num(pc.opts["strokeOpacity"]) if "strokeOpacity" in pc.opts else 1.0
        note = ""
        if "strokeDasharray" in pc.opts: note = "파선→실선"
        if pc.filled and alpha is None: note = (note + " " if note else "") + "그라디언트→평면"
        out.append(Piece("%s.%s%d" % (kind, pc.call, i), pts, loop=pc.closed or pc.filled,
                         filled=pc.filled, tone=tone, grade=grade, surfaces=CARD,
                         alpha=alpha, line_alpha=la, src="%s%d" % (pc.call, i), note=note))
    # 사전합성 배경(카드): 앞서 그려진 채움 안에 90% 이상 들어가면 그 조각 위에 합성한다
    for i, p in enumerate(out):
        under = None
        for q in out[:i]:
            if not q.filled: continue
            cov = sum(1 for pt in p.pts if rig.contains(q.pts, pt)) / len(p.pts)
            if cov >= 0.9: under = q
        p.under = under
    return out

CARD_SET = {k: _card_pieces(k) for k in KINDS}

# ---- 교정 --------------------------------------------------------------------
_total = sum(len(v) for v in CARD_SET.values()); _h = sum(1 for v in CARD_SET.values() for p in v if p.tone == 3)
if _total != 91 or _h != 17:
    raise SystemExit("교정 실패: 조각 %d(기대 91) 하이라이트 %d(기대 17)" % (_total, _h))
_CAL = [("crown", 3, 0.1879), ("crown", 6, 0.2077), ("sunglasses", 3, 0.1390),
        ("roundglasses", 2, 0.1763), ("goggles", 3, 0.1356), ("bellnecklace", 1, 0.1297),
        ("goggles", 1, 0.4747)]
for _k, _i, _want in _CAL:
    _p = CARD_SET[_k][_i]; x0, y0, x1, y1 = bounds(_p.pts); _ink = max(x1 - x0, y1 - y0)
    if abs(_ink - _want) > 0.0015:
        raise SystemExit("교정 실패: %s[%d] 잉크 %.4f ≠ %.4f" % (_k, _i, _ink, _want))

def card_piece(kind, src):
    for p in CARD_SET[kind]:
        if p.src == src: return p
    raise KeyError(kind + "." + src)

# ============================================================================
# 3. BODY — 프로덕션 3/4 도형 + 인계본 레이어(조각 단위 상자 아핀)
# ============================================================================
def _prod(slot, ko):
    return {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK, "BACK": items.BACK}[slot][ko]

def _from_shape(s, tone=None, grade=None, note=""):
    return Piece(s.name, s.pts, loop=s.loop, filled=s.filled,
                 tone=s.tone if tone is None else tone, grade=grade, surfaces=BODY | CARD, note=note)

def _layer(kind, src, anchor_src, dst_pts, name, tone, grade=2, filled=False, loop=False,
           mode="bbox", under=None, note=""):
    """인계본 조각 `src` 를 인계본 앵커 조각 `anchor_src` 의 상자에서 우리 조각 `dst_pts` 의 상자로 옮긴다."""
    p = card_piece(kind, src); a = card_piece(kind, anchor_src)
    f = (bbox_map if mode == "bbox" else uniform_map)(bounds(a.pts), bounds(dst_pts))
    return Piece(name, [f(q) for q in p.pts], loop=loop, filled=filled, tone=tone, grade=grade,
                 surfaces=BODY | CARD, src=src, under=under, note=note)

def _body_pieces(kind):
    slot, ko = SLOT[kind], KO[kind]
    P = {s.name: s for s in _prod(slot, ko)}
    out = []
    if kind == "clothhat":
        crown, brim, band = P["HatCrown"], P["HatBrim"], P["HatBand"]
        out = [_from_shape(crown, tone=0),
               _from_shape(brim, tone=0, note="챙 보조색→주색(인계본 B)"),
               _from_shape(band, tone=1, note="띠 그늘→보조색(인계본 F)"),
               _layer(kind, "H3", "B0", crown.pts, "HatHighlight", 3)]
    elif kind == "furhat":
        crown = P["BeanieCrown"]; pom = P["BeaniePom"]
        # 관을 단 위(y≥0.52)로 자르고 단은 채운 띠로 다시 세운다(인계본 furhat 구성: 관 B + 단 B(accent) + 결 S)
        cr = [(-0.57, 0.50), (-0.50, 1.06), (0.00, 1.44), (0.50, 1.06), (0.57, 0.50)]
        cuff = [(-1.05, -0.06), (1.05, -0.06), (1.05, 0.52), (-1.05, 0.52)]
        out = [Piece("BeanieCrown", cr, filled=True, tone=0, note="단을 분리(0.50 위만)"),
               Piece("BeanieCuff", cuff, filled=True, tone=1, note="접힌 단 = 채운 띠 0.58R(보조색)"),
               _from_shape(pom, tone=0, note="폼폼 보조색→주색(인계본 CB)"),
               _layer(kind, "S4", "B3", cuff, "BeanieCuffWave", 2, grade=2, under="BeanieCuff",
                      note="털 결 — 규칙은 통과(1-A 1.58R)하지만 첫 시트에서 눈으로 기각: 진폭 0.17R = 디테일획 폭이라 "
                           "「결」이 아니라 「굵은 물결선」으로 읽힌다(2× Retina 에서 2px). 카드 전용."),
               _layer(kind, "H2", "B1", cr, "BeanieHighlight", 3)]
    elif kind == "fedora":
        brim, crown, band = P["FedoraBrim"], P["FedoraCrown"], P["FedoraBand"]
        out = [_from_shape(brim, tone=0), _from_shape(crown, tone=0), _from_shape(band, tone=1),
               _layer(kind, "H3", "B0", crown.pts, "FedoraHighlight", 3)]
    elif kind == "crown":
        out = [_from_shape(P["CrownBody"], tone=0), _from_shape(P["CrownRim"], tone=1)]
    elif kind == "sunglasses":
        out = [_from_shape(P["SunglassLensBack"], tone=0), _from_shape(P["SunglassLensFront"], tone=0),
               _from_shape(P["SunglassBridge"], tone=1)]
    elif kind == "roundglasses":
        out = [_from_shape(P["RoundLensBack"], tone=0, note="렌즈 불투명(사용자 제약)"),
               _from_shape(P["RoundLensFront"], tone=0), _from_shape(P["RoundBridge"], tone=1)]
    elif kind == "goggles":
        plate = P["GoggleLens"]; strap = P["GoggleStrap"]
        lb = rig.poly(-0.50, -0.22, 0.26, 12); lf = rig.poly(0.50, -0.22, 0.26, 12)
        out = [_from_shape(strap, tone=0, note="끈 보조색→주색"),
               _from_shape(plate, tone=0),
               Piece("GoggleLensBack", lb, filled=True, tone=1, note="렌즈 원반 r=0.26R(§4-2-5 PROMOTE ×1.087 → 0.52R)"),
               Piece("GoggleLensFront", lf, filled=True, tone=1)]
    elif kind == "monocle":
        pod = rig.poly(0.58, 0.02, 0.46, 12)
        chain = [rig.polar(280.0, 0.46), (0.62, -0.80), (0.80, -1.20)]
        chain = [(pod[0][0] * 0 + 0.58 + chain[0][0], 0.02 + chain[0][1]), chain[1], chain[2]]
        out = [Piece("MonoclePod", pod, filled=True, tone=0, grade=3,
                     note="알 r 0.36→0.46R(인계본 비 1.19×동그란안경) · 테 굵은획 ×1.5"),
               Piece("MonocleChain", chain, loop=False, tone=0, grade=2, note="줄 디테일획(파선→실선)"),
               Piece("MonocleEye", items.drawn_eye(-1), filled=True, tone=1, surfaces=BODY,
                     note="드러난 눈 — 몸 전용(인계본 카드에 없다)")]
    elif kind == "bowtie":
        out = [_from_shape(P["BowTieLeftWing"], tone=0), _from_shape(P["BowTieRightWing"], tone=0),
               _from_shape(P["BowTieKnot"], tone=1)]
    elif kind == "stripedtie":
        knot, blade = P["TieKnot"], P["TieBlade"]
        ky = items.NECKY; b0 = ky - 0.28; L = items.TL * 0.55
        def hw(t):   # 블레이드 반폭(길이 비율 t)
            return 0.34 + (0.39984 - 0.34) * min(t, 0.72) / 0.72 if t <= 0.72 else 0.39984 * (1 - (t - 0.72) / 0.28)
        def stripe(t0, t1, name):
            y0, y1 = b0 - L * t0, b0 - L * t1
            return Piece(name, [(-hw(t0), y0), (hw(t0), y0 - 0.20), (hw(t1), y1 - 0.20), (-hw(t1), y1)],
                         filled=True, tone=1,
                         note="줄무늬 2개(인계본 F×2), 각 0.48R(0.44R 은 1-C 를 2% 미달) · 간격 0.29R")
        # 0.23 L = 0.477 R 높이. 0.212 L(0.44 R)은 ρ_max 0.213 < 0.218 R 로 탈락했다(첫 실행 실측).
        out = [_from_shape(knot, tone=0), _from_shape(blade, tone=0),
               stripe(0.20, 0.43, "TieStripe"), stripe(0.57, 0.80, "TieStripe2")]
    elif kind == "scarf":
        wrap = P["ScarfWrap"]
        out = [_from_shape(P["ScarfTailBack"], tone=0), _from_shape(P["ScarfTailFront"], tone=0),
               _from_shape(wrap, tone=1),
               _layer(kind, "H5", "B0", wrap.pts, "ScarfHighlight", 3, under="ScarfWrap")]
    elif kind == "bellnecklace":
        # 인계본 방울 = 몸(B) + 테(RB) + 추(CB). 몸·테는 각각 1-C 미달(§4-2-8 ×1.15 / ×2.54)이라 **한 실루엣으로
        # 합친다**(폭 0.84 · 높이 0.80R, ρ≈0.35R). 추는 r 0.26R(1-A 하한 0.516R 의 원반)로 확대. 구슬 3은 소멸.
        low = items.NECKY + 0.16 - 0.32          # 목줄 최저점(items.bell 과 같은 식)
        # 윗변 ±0.12R = 0.24R: ±0.10(0.20R)은 1-A 몽당변(0.92획)으로 탈락했다(둘째 실행 실측)
        bell = [(-0.12, low), (0.12, low), (0.30, low - 0.30), (0.40, low - 0.62), (0.42, low - 0.80),
                (-0.42, low - 0.80), (-0.40, low - 0.62), (-0.30, low - 0.30)]
        clapper = rig.poly(0.0, low - 0.80 - 0.14, 0.26, 8, 90.0)
        out = [_from_shape(P["Collar"], tone=0),
               Piece("Bell", bell, filled=True, tone=1, note="방울 실루엣(몸+테 합침, 인계본 비례 0.84×0.80R)"),
               Piece("BellClapper", clapper, filled=True, tone=0, note="추 r 0.26R(인계본 0.10R 확대)")]
    elif kind == "shortcape":
        o = P["CapeOutline"]
        out = [_from_shape(o, tone=0), _from_shape(P["CapeFold"], tone=2), _from_shape(P["CapeFold2"], tone=2),
               _from_shape(P["CapeYoke"], tone=1),
               _layer(kind, "H3", "B0", o.pts, "CapeHighlight", 3, note="뒤쪽 가장자리 광택")]
    elif kind == "longcape":
        o = P["CapeOutline"]; yoke = P["CapeYoke"]
        cy = items.COLLARY
        clasp = rig.poly(-0.11, cy - 0.45, 0.26, 10, 90.0)
        out = [_from_shape(o, tone=0), _from_shape(P["CapeFold"], tone=2), _from_shape(P["CapeFold2"], tone=2),
               _from_shape(yoke, tone=1),
               Piece("CapeClasp", clasp, filled=True, tone=0, note="걸쇠 r=0.26R(인계본 0.16R 은 소멸 → 확대)"),
               _layer(kind, "H5", "B0", o.pts, "CapeHighlight", 3)]
    elif kind == "wings":
        fa, fb = P["WingFeatherA"], P["WingFeatherB"]
        # 인계본 왼 날개 B0 + 그 정맥 S2 / 오른 B1 + S3. 우리 A(-x) ↔ 인계본 왼(B0)
        out = [_from_shape(P["WingSpine"], tone=1), _from_shape(fa, tone=0), _from_shape(fb, tone=0),
               _layer(kind, "S2", "B0", fa.pts, "WingVeinA", 2, grade=2, under="WingFeatherA", note="정맥 그늘색(주색 위 주색선은 안 보인다)"),
               _layer(kind, "S3", "B1", fb.pts, "WingVeinB", 2, grade=2, under="WingFeatherB"),
               _layer(kind, "H5", "B0", fa.pts, "WingHighlight", 3, under="WingFeatherA")]
    elif kind == "backpack":
        body, lid = P["PackBody"], P["PackLid"]
        bx0, by0, bx1, by1 = bounds(body.pts)
        handle = _layer(kind, "S0", "B1", body.pts, "PackHandle", 0, grade=0, mode="bbox")
        # 손잡이는 몸통 상자 밖(위)으로 나가야 한다 — bbox 정합은 안쪽으로 눌러 버리므로 y 를 되돌린다
        hp = card_piece(kind, "S0"); ap = card_piece(kind, "B1")
        ax0, ay0, ax1, ay1 = bounds(ap.pts); k = (bx1 - bx0) / (ax1 - ax0)
        handle.pts = [(bx0 + (x - ax0) * k, by1 + (y - ay1) * k * 0.6) for x, y in hp.pts]
        handle.note = "손잡이(낱선) — 몸통 위 0.5R 까지만"
        out = [_from_shape(body, tone=0), _from_shape(lid, tone=0), _from_shape(P["PackBuckle"], tone=1),
               _from_shape(P["PackStrap"], tone=0), handle,
               _layer(kind, "H5", "B1", body.pts, "PackHighlight", 3, under="PackBody")]
    return out

# ---- 생존 판정 (규칙 1-A 를 **그 조각의 획 등급 실폭**으로, 채움은 1-C 까지) --------------------
def survival(p, scale=SHIP):
    w = p.width_R(scale)
    v = rig.rule_one(p.as_shape(), w)
    if v: return False, "1-A(%s %.3fR): %s" % (GRADE_NAME[p.grade], w, v)
    if p.filled:
        rho = rho_max(p.pts)
        if rho < W1: return False, "1-C: ρ_max %.3fR < %.3fR" % (rho, W1)
    if p.loop and rig.self_intersects(p.pts): return False, "자기교차"
    return True, "OK"

# ★ 규칙은 통과했지만 **눈으로 기각**한 조각(첫 비교 시트 판정). 숫자 자는 「존재」만 재고 「무엇으로 읽히는가」는 못 잰다.
EYE_REJECT = {("furhat", "BeanieCuffWave")}

def _build_body():
    out = {}
    for k in KINDS:
        ps = _body_pieces(k)
        for p in ps:
            ok, why = survival(p)
            if not ok:
                p.surfaces &= ~BODY
                p.note = (p.note + " | " if p.note else "") + "몸 소멸: " + why
            elif (k, p.name) in EYE_REJECT:
                p.surfaces &= ~BODY
        out[k] = ps
    return out

BODY_SET = _build_body()

# ============================================================================
# 4. SHARED — 「좌표 한 벌」 변형: BODY 기하 + 카드 전용 조각(인계본에서 상자 아핀으로)
# ============================================================================
# 카드 전용 조각 → 우리 앵커 조각 (인계본 앵커 조각 → 우리 조각 상자)
_ANCHOR = {
    "clothhat":  {},
    "furhat":    {},
    "fedora":    {},
    "crown":     {"CF2": ("B1", "CrownRim"), "CF3": ("B1", "CrownRim"), "CF4": ("B1", "CrownRim"),
                  "CB5": ("B0", "CrownBody"), "CB6": ("B0", "CrownBody"), "CB7": ("B0", "CrownBody"),
                  "H8": ("B0", "CrownBody")},
    "sunglasses": {"S3": ("B0", "SunglassLensBack"), "S4": ("B1", "SunglassLensFront"), "H5": ("B0", "SunglassLensBack")},
    "roundglasses": {"S3": ("CB0", "RoundLensBack"), "S4": ("CB1", "RoundLensFront"),
                     "H5": ("CB0", "RoundLensBack"), "H6": ("CB1", "RoundLensFront")},
    "goggles":   {"S3": ("B0", "GoggleLens"), "S4": ("B0", "GoggleStrap"), "S5": ("B0", "GoggleStrap"), "H6": ("CB1", "GoggleLensBack")},
    "monocle":   {"H1": ("CB0", "MonoclePod"), "CB3": ("CB0", "MonoclePod")},
    "bowtie":    {"H3": ("B0", "BowTieLeftWing")},
    "stripedtie": {"H4": ("B0", "TieKnot")},
    "scarf":     {"S2": ("B1", "ScarfTailFront"), "S3": ("B1", "ScarfTailFront"), "S4": ("B1", "ScarfTailFront")},
    "bellnecklace": {"CF1": ("S0", "Collar"), "CF2": ("S0", "Collar"), "CF3": ("S0", "Collar"),
                     "RB5": ("B4", "Bell"), "CB6": ("B4", "Bell"), "H7": ("B4", "Bell")},
    "shortcape": {"S2": ("B0", "CapeOutline")},
    "longcape":  {"S3": ("B0", "CapeOutline"), "S4": ("B0", "CapeOutline")},
    "wings":     {"RB4": ("B0", "WingFeatherA")},
    "backpack":  {"RB3": ("B1", "PackBody"), "RB4": ("B1", "PackBody")},
}

def _shared(kind):
    body = BODY_SET[kind]
    have = {p.src for p in body if p.src}
    named = {p.name: p for p in body}
    out = list(body)
    for p in CARD_SET[kind]:
        if p.src in have: continue
        anc = _ANCHOR.get(kind, {}).get(p.src)
        if anc is None: continue
        a = card_piece(kind, anc[0]); dst = named.get(anc[1])
        if dst is None: continue
        f = bbox_map(bounds(a.pts), bounds(dst.pts))
        q = Piece(kind + ".shared." + p.src, [f(t) for t in p.pts], loop=p.loop, filled=p.filled,
                  tone=p.tone, grade=p.grade, surfaces=CARD, alpha=p.alpha, line_alpha=p.line_alpha,
                  src=p.src, note="카드 전용(공유 기하 위)")
        out.append(q)
    return out

SHARED = {k: _shared(k) for k in KINDS}

# ============================================================================
# 5. 색 — 사전 합성 (안 D Tone 3)
# ============================================================================
def hex_rgb(h):
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))

def over(top, alpha, under):
    return tuple(int(round(under[i] + (top[i] - under[i]) * alpha)) for i in range(3))

def mul(c, k):
    return tuple(int(round(v * k)) for v in c)

CARD_BG = (0x15, 0x18, 0x1E)          # UiChrome.CardSurfaceMuted (카드 아이콘 영역 바탕)
HANDOFF_INK = hex_rgb("#E8E2D6")      # 인계본 C
BRASS = hex_rgb("#C8A15A")
CAPE_MAT = hex_rgb("#D2402F")
GRAD_MEAN = 0.21                      # 인계본 B 그라디언트 0.34→0.08 의 평균(평면 대체)
GRAD_MEAN_MAT = 0.81                  # 망토류 1.00→0.62
HI_ALPHA = 0.42
FILL_OUTLINE_SHADE = 0.28             # AccessoryShapeBuilder.FillOutlineShadeFactor

def card_colors(pieces, accent=BRASS, ink=HANDOFF_INK, bg=CARD_BG, mat=None):
    """카드 표면: 인계본 문법(밝은 잉크 윤곽 + 어두운 워시 채움)을 **불투명 색 한 벌**로 굽는다.
    반환 {piece: (fill, outline)} — fill 은 None 이면 채움 없음."""
    A = mat if mat is not None else accent
    res = {}
    def flat(p):
        return res[p][0] if (p is not None and p in res and res[p][0] is not None) else bg
    for p in pieces:
        under = flat(p.under) if p.under is not None else bg
        if p.tone == 3:
            res[p] = (None, over((255, 255, 255), HI_ALPHA, under)); continue
        if p.filled:
            if p.alpha is None:
                a = GRAD_MEAN_MAT if mat is not None else GRAD_MEAN
                fill = over(A, a, under)
            else:
                fill = over(A, p.alpha, under)
            # B/CB/RB(그리고 src 없는 우리 본체 채움)는 잉크 윤곽, CF/F 는 윤곽 없음
            outline = None if (p.src.startswith("CF") or p.src.startswith("F")) else ink
            res[p] = (fill, outline)
        else:
            res[p] = (None, over(ink, p.line_alpha, under))
    return res

def body_colors(pieces, primary, secondary):
    """몸 표면(프로덕션 색 정책 C1): 톤 0 주색 · 1 보조색 · 2 그늘(주색×0.28) · 3 하이라이트(흰 42% 사전합성).
    채움 윤곽 = 채움색×0.28(FillOutlineColor)."""
    res = {}
    named = {p.name: p for p in pieces}
    def base(p):
        return {0: primary, 1: secondary, 2: mul(primary, FILL_OUTLINE_SHADE)}[p.tone]
    for p in pieces:
        if p.tone == 3:
            u = named.get(p.under) if isinstance(p.under, str) else None
            uc = base(u) if u is not None else primary
            res[p] = (None, over((255, 255, 255), HI_ALPHA, uc)); continue
        c = base(p)
        if p.filled: res[p] = (c, mul(c, FILL_OUTLINE_SHADE))
        else:        res[p] = (None, c)
    return res

# 16종 프로덕션 색(.asset tone0/tone1, 2026-09-05 실측)
PROD_COLOR = {
    "clothhat": ("#96814F", "#CC5512"), "furhat": ("#BA7636", "#96814F"), "fedora": ("#5577AE", "#CC5512"),
    "crown": ("#9B7922", "#988540"), "sunglasses": ("#5075B5", "#587398"), "roundglasses": ("#587398", "#20878C"),
    "goggles": ("#587398", "#CC5512"), "monocle": ("#9B7922", "#587398"), "bowtie": ("#5A8C3C", "#96814F"),
    "stripedtie": ("#428C24", "#96814F"), "scarf": ("#CC5512", "#BA5928"), "bellnecklace": ("#BA5928", "#9B7922"),
    "shortcape": ("#CC3C3C", "#96814F"), "longcape": ("#CC3C3C", "#96814F"), "wings": ("#6787B9", "#955CCC"),
    "backpack": ("#AB7942", "#5A8C3C"),
}

# ============================================================================
# 6. 보고
# ============================================================================
def report(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    w("== 획 등급 실폭 @0.75 (R) ==")
    for g in GRADE: w("   %d %-5s ×%.2f 하한 %.1fpt → %.4f R" % (g, GRADE_NAME[g], GRADE[g][0], GRADE[g][1], grade_width_R(g)))
    w()
    w("== 표면별 조각 수 · 몸 생존 ==")
    w("   %-13s %-8s 카드 | 몸설계 몸생존 카드전용 | 공유기하" % ("kind", "우리"))
    tb = tc = tl = 0
    for k in KINDS:
        ps = BODY_SET[k]; nb = sum(1 for p in ps if p.on(BODY)); nc = len(CARD_SET[k])
        card_only = [p for p in ps if not p.on(BODY)]
        # 공유 가능한 기하: 몸 조각 중 인계본 src 를 그대로 옮긴 것(레이어) — 본체 도형은 3/4 재저작이라 공유 불가
        shared_geom = sum(1 for p in ps if p.src and p.on(BODY))
        tb += nb; tc += nc
        w("   %-13s %-8s %4d | %6d %6d %8d | %d" % (k, KO[k], nc, len(ps), nb, len(card_only), shared_geom))
    w("   합계 카드 %d · 몸 생존 %d" % (tc, tb))
    w()
    w("== 몸 소멸 조각(설계했으나 규칙에 걸린 것) ==")
    for k in KINDS:
        for p in BODY_SET[k]:
            if not p.on(BODY): w("   %-13s %-18s %s" % (k, p.name, p.note))
    w()
    w("== 몸 조각별 판정 ==")
    for k in KINDS:
        for p in BODY_SET[k]:
            ok, why = survival(p)
            x0, y0, x1, y1 = bounds(p.pts)
            w("   %-13s %-18s t%d g%d(%.3fR) 잉크 %.2f×%.2fR %s %s" %
              (k, p.name, p.tone, p.grade, p.width_R(), x1 - x0, y1 - y0, "생존" if ok else "소멸", p.note))
    w()
    w("== 카드 사전합성 색(브라스 #C8A15A · 잉크 #E8E2D6 · 바탕 #15181E) ==")
    for k in KINDS:
        cc = card_colors(CARD_SET[k], mat=CAPE_MAT if k in MAT else None)
        for p in CARD_SET[k]:
            f, o = cc[p]
            w("   %-13s %-5s t%d g%d fill=%s line=%s %s" % (k, p.src, p.tone, p.grade,
              ("#%02X%02X%02X" % f) if f else "-", ("#%02X%02X%02X" % o) if o else "-", p.note))
    w()
    w("== 몸 하이라이트 사전합성(흰 42% over 재질색) ==")
    for k in KINDS:
        pr, sc = hex_rgb(PROD_COLOR[k][0]), hex_rgb(PROD_COLOR[k][1])
        bc = body_colors(BODY_SET[k], pr, sc)
        for p in BODY_SET[k]:
            if p.tone == 3 and p.on(BODY):
                w("   %-13s %-16s over %s → #%02X%02X%02X" % (k, p.name, PROD_COLOR[k][0], *bc[p][1]))
    # 쌍별 실루엣 (EYES — 동그란안경 vs 외알안경 회귀)
    w()
    w("== 쌍별 실루엣 차 (몸 생존 조각, verify.py 와 같은 자: 72구간 max Δr / W) ==")
    for slot, anchor in (("HEAD", 0.0), ("EYES", 0.0), ("NECK", rig.SHOULDER_R), ("BACK", rig.SHOULDER_R)):
        ks = [k for k in KINDS if SLOT[k] == slot]
        prof = {k: rig.profile([p.as_shape() for p in BODY_SET[k] if p.on(BODY)], anchor) for k in ks}
        for i in range(len(ks)):
            for j in range(i + 1, len(ks)):
                a, b = ks[i], ks[j]
                d = rig.max_delta(prof[a], prof[b]) / W0
                frac = sum(1 for x, y in zip(prof[a], prof[b]) if abs(x - y) >= 0.5 * W0) / rig.BINS
                w("   %-5s %-13s vs %-13s maxΔ %.2f획  |Δ|≥0.5W 구간 %.1f%%" % (slot, a, b, d, 100 * frac))


if __name__ == "__main__":
    report()
