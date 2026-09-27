# -*- coding: utf-8 -*-
"""출하 **다섯 표면** 자(尺) 재측정 — design-equipment 1층 (2026-09-27)

왜 이 파일이 생겼나
-------------------
이 폴더의 `verify.py` 는 카드 표면을 `ICON,FIT,IST = 44.0, 0.86, 1.7*44/40` 으로 잰다.
그 자는 **2026-09-01 판 프로덕션**의 자다(`CharacterInfoWindow.IconSize = 44f` ·
획 `1.7 * (IconSize/40)` · 봉투 맞춤 `FitFraction 0.86`). 지금 출하 프로덕션은 셋 다 다르다:

  · 카드 벡터 = 58pt, 획 = size × 2.2/64 × strokeMult   (`AccessoryCardIcon.Frame` · `AccessoryStroke.Multiplier`)
  · 배치 = 봉투 맞춤이 아니라 **슬롯 고정 배율 + 넘침 축소**(`AccessoryCardIcon.MeasureBoxFit`)
  · 표면이 셋이 아니라 **다섯**이다(아래 SURFACES)

그래서 44px 로 잰 수치를 그대로 인용하면 **기준과 대상이 같이 낡은 스냅숏**이 된다.
이 파일은 `verify.py` 를 고치지 않는다(그 파일은 2026-09-01 설계 거울이고, 그 판에서는 맞았다).
대신 **출하 자로 다시 재서** 문서가 인용할 수 있는 숫자를 낸다.

★ 이 파일 자신의 자기 정정 (같은 라운드, 커밋 전)
--------------------------------------------------
첫 판은 두 곳에서 **틀린 자**를 썼다. 적어 둔다 — 고친 것만 남기면 다음 사람이 같은 실수를 한다.
 (가) 카드 표면 획에 `strokeMult` 를 안 곱했다. 프로덕션은 조각마다 곱한다
     (`BuildFramed`: `strokeWidth = baseStroke * AccessoryStroke.Multiplier(shape.StrokeMult)`),
     그래서 ×0.75 하이라이트·×1.5 외알안경 테는 획이 다르다.
 (나) **착용 표면을 W(0.3439 R @0.75)로 쟀다.** 계약 v2(인계본) 조각의 실제 폭은 W 가 아니라
     `max(strokeInR × R, MinAccessoryStrokeScreenPoints 1pt)` 다
     (`CharacterAccessoryRenderer.AddLine(handoffWidth)` → `Mathf.Max(handoffWidth, MinAccessoryStrokeWorld)`).
     @0.75 에서 그 값은 **0.1720 R** 로 W 의 절반이다 ⇒ 첫 판의 ④ 표는 획 배수를 **절반으로** 깎아
     통과를 미달로 보이게 만들었다. W 는 v1 조각(`strokeInR == 0`)의 자다.

무엇을 입력으로 쓰는가 — **골든 비트**다, 프로덕션 함수가 아니다
----------------------------------------------------------------
`Tests/EditMode/Golden/CardShapeGolden.txt`(인계본 16종) ·
`Tests/EditMode/Golden/FxPetCardGolden.txt`(FX/PET 12종)를 읽는다.
프로덕션 C# 을 다시 계산하지 않는 이유는 이 저장소의 규칙이다 —
「기대값을 프로덕션 함수로 만들지 마라. 그 함수가 틀어지면 기대값도 함께 틀어져 아무것도 못 잰다.」
골든은 `CardShapeContractTests` / `FxPetCardShapeTests` 가 프로덕션과 대조하는 파일이므로,
여기서 읽은 좌표는 **프로덕션이 실제로 그리는 좌표**다.

교정이 깨지면 이 실행의 숫자를 전부 폐기한다(아래 `calibrate()`).
"""
import hashlib
import math
import os
import sys

# ============================================================================
# 0. 프로덕션 상수 — 값을 베끼지 않고 **식으로** 옮긴다
# ============================================================================
PT_PER_UNIT = 846.0 / (2.0 * 12.0)        # StickConfig.ReferencePointsPerWorldUnitApprox = 35.25
HEAD_R_UNITS = 0.22                        # 본체 머리 반경(월드 단위)
BODY_STROKE_W = 0.048                      # v1 액세서리 설계 획(월드 단위)
MIN_STROKE_PT = 2.0                        # StickConfig.MinStrokeScreenPoints (v1 낱선 하한)
MIN_ACC_STROKE_PT = 1.0                    # StickConfig.MinAccessoryStrokeScreenPoints (계약 v2 하한)
SCALES = (0.35, 0.60, 0.75, 1.00)          # 하한 / 사용자 저장 / 출하 기본 / 상한

ICON_VIEWBOX = 64.0                        # AccessoryCardIcon.Frame.IconViewBox
ICON_STROKE_U = 2.2                        # AccessoryCardIcon.Frame.IconStroke
OLD_V1_STROKE_AT_40 = 1.7                  # 옛 v1 획: 1.7 × (size/40)
RETIRED_CARD_SIZE = 44.0                   # 2026-09-01 판 CharacterInfoWindow.IconSize

# 잉크 문턱 — 문서 출처 자(DESIGN_FAN_MENU_ICONS R26-1, MINER_MAGE J-5 가 인용)
THETA_INK = 0.3476                         # 면색 대비 3.0:1 에 필요한 알파
# 1× 최악 위상 알파 = t_pt − 0.5 (같은 출처). 2× 는 t_px = 2·t_pt 라 1.0 으로 본다.

SURFACES = [
    ("카드(벡터)",          58.0, "vector", "CharacterInfoWindow.IconSize"),
    ("카드(비트맵)",        72.0, "bitmap", "BitmapIconSize = ThumbHeight 78 − BitmapIconInset 6"),
    ("상세 썸네일(벡터)",   38.0, "vector", "CharacterInfoWindow.DetailThumbArtSize"),
    ("상세 썸네일(비트맵)", 46.0, "bitmap", "BitmapDetailArtSize = DetailThumbSize 52 − 6"),
    ("슬롯행",              24.0, "vector", "CharacterInfoWindow.SlotIconSize"),
]
VECTOR_SIZES = (58.0, 38.0, 24.0)

CORNER_DEG = 45.0                          # AccessoryStrokeBudgetTests.DescribeRuleOneViolation
EDGE_MIN_W = 1.0                           # 규칙 1 — 양끝 꺾임 변 ≥ 1.0획
INK_BOX_MIN_W = 1.5                        # 규칙 1 — 잉크 사각형 ≥ 1.5획

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
GOLDEN_CARD = os.path.join(REPO, "Assets/_Project/Scripts/Tests/EditMode/Golden/CardShapeGolden.txt")
GOLDEN_FXPET = os.path.join(REPO, "Assets/_Project/Scripts/Tests/EditMode/Golden/FxPetCardGolden.txt")

SLOT_NAME = {0: "HEAD", 1: "EYES", 2: "NECK", 3: "BACK", 5: "FX", 6: "PET"}
FX_NAME = ["없음", "발자국", "반짝임", "먼지", "물방울", "나뭇잎"]
PET_NAME = ["작은공", "종이비행기", "리틀스틱메이트", "커서친구", "풍선", "달팽이"]


# ============================================================================
# 1. 자 — 표면별 획, 배율별 머리·획
# ============================================================================
def card_stroke_pt(size, stroke_mult=1.0):
    """출하 카드 계통 획(pt). 넘침 축소를 **따라가지 않는다**(BuildFramed 주석)."""
    return size * (ICON_STROKE_U / ICON_VIEWBOX) * (stroke_mult if stroke_mult > 0 else 1.0)


def old_v1_stroke_pt(size):
    """2026-09-01 판 획 — 1.7 × (size/40). 44px 에서 1.87 이 나온 자다."""
    return OLD_V1_STROKE_AT_40 * (size / 40.0)


def head_radius_pt(scale):
    return HEAD_R_UNITS * scale * PT_PER_UNIT


def v1_stroke_in_R(scale):
    """v1 조각(strokeInR == 0)의 W — ShippingStrokeBudgetInHeadRadii."""
    stroke_units = max(BODY_STROKE_W * scale, MIN_STROKE_PT / PT_PER_UNIT)
    return stroke_units / (HEAD_R_UNITS * scale)


def handoff_stroke_in_R(stroke_in_R, scale):
    """계약 v2 조각의 실제 폭(R 배수) = max(명목, 1pt) — AddLine(handoffWidth)."""
    return max(stroke_in_R, MIN_ACC_STROKE_PT / head_radius_pt(scale))


def worst_phase_alpha_1x(t_pt):
    """1× 최악 위상 알파(문서 출처 식). 2× 는 1.0."""
    return max(0.0, min(1.0, t_pt - 0.5))


# ============================================================================
# 2. 기하 — 규칙 1 자(합성 표본으로 교정한다)
# ============================================================================
def turn_deg(a, b, c):
    v1 = (b[0] - a[0], b[1] - a[1])
    v2 = (c[0] - b[0], c[1] - b[1])
    if v1[0] ** 2 + v1[1] ** 2 < 1e-12 or v2[0] ** 2 + v2[1] ** 2 < 1e-12:
        return 0.0
    d = (v1[0] * v2[0] + v1[1] * v2[1]) / (math.hypot(*v1) * math.hypot(*v2))
    return math.degrees(math.acos(max(-1.0, min(1.0, d))))


def min_corner_edge(pts, loop):
    """양끝이 모두 꺾임(≥45°)인 변 중 가장 짧은 것. 그런 변이 없으면 None."""
    n = len(pts)
    if n < 2:
        return None
    corner = [False] * n
    rng = range(n) if loop else range(1, n - 1)
    for i in rng:
        corner[i] = turn_deg(pts[(i - 1) % n], pts[i], pts[(i + 1) % n]) >= CORNER_DEG
    best = None
    for i in range(n if loop else n - 1):
        j = (i + 1) % n
        if corner[i] and corner[j]:
            L = math.dist(pts[i], pts[j])
            best = L if best is None else min(best, L)
    return best


def ink_span(pts):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return max(max(xs) - min(xs), max(ys) - min(ys))


def poly_min_distance(a, b):
    """두 다각형 경계 사이 최소 거리."""
    def seg_dist(p, q0, q1):
        dx, dy = q1[0] - q0[0], q1[1] - q0[1]
        L = dx * dx + dy * dy
        t = 0.0 if L < 1e-12 else max(0.0, min(1.0, ((p[0] - q0[0]) * dx + (p[1] - q0[1]) * dy) / L))
        return math.hypot(p[0] - (q0[0] + dx * t), p[1] - (q0[1] + dy * t))

    best = float("inf")
    for i in range(len(a)):
        for j in range(len(b)):
            p0, p1 = a[i], a[(i + 1) % len(a)]
            q0, q1 = b[j], b[(j + 1) % len(b)]
            best = min(best, seg_dist(p0, q0, q1), seg_dist(p1, q0, q1),
                       seg_dist(q0, p0, p1), seg_dist(q1, p0, p1))
    return best


def regular_polygon(cx, cy, r, n, start_deg=0.0):
    return [(cx + math.cos(math.radians(start_deg) + 2 * math.pi * i / n) * r,
             cy + math.sin(math.radians(start_deg) + 2 * math.pi * i / n) * r) for i in range(n)]


# ============================================================================
# 3. 골든 판독
# ============================================================================
def sha16(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()[:16]


class Piece:
    __slots__ = ("slot", "item", "index", "name", "loop", "filled", "tone",
                 "surfaces", "stroke_mult", "stroke_in_R", "pts")


def read_card_golden(path):
    card, body, frames, items, total = [], [], {}, [], None
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line or line.startswith("#"):
                continue
            t = line.split("\t")
            if t[0] == "FRAME":
                frames[int(t[1])] = (float(t[2]), float(t[3]))
            elif t[0] == "ITEM":
                items.append((int(t[1]), t[2]))
            elif t[0] == "TOTAL":
                total = tuple(int(x) for x in t[1:5])
            elif t[0] == "PIECE":
                p = Piece()
                p.slot, p.item, p.index, p.name = int(t[1]), t[2], int(t[3]), t[4]
                p.loop, p.filled, p.tone = t[5] == "1", t[6] == "1", int(t[7])
                p.surfaces = int(t[8])
                p.stroke_mult, p.stroke_in_R = float(t[9]), float(t[10])
                npts = int(t[19])
                p.pts = [tuple(float(v) for v in s.split(",")) for s in t[20:20 + npts]]
                assert len(p.pts) == npts, "%s %s 점 수 불일치" % (p.item, p.name)
                card.append(p)
            elif t[0] == "BODY":
                p = Piece()
                p.slot, p.item, p.index, p.name = int(t[1]), t[2], int(t[3]), t[4]
                p.loop, p.filled, p.tone, p.surfaces = True, False, 0, 0
                p.stroke_mult, p.stroke_in_R = 1.0, 0.0
                npts = int(t[5])
                p.pts = [tuple(float(v) for v in s.split(",")) for s in t[6:6 + npts]]
                assert len(p.pts) == npts, "%s %s BODY 점 수 불일치" % (p.item, p.name)
                body.append(p)
    return card, body, frames, items, total


def read_fxpet_golden(path):
    """좌표가 **이미 64u 아이콘 단위**다(y 아래)."""
    pieces, total = [], None
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line or line.startswith("#"):
                continue
            t = line.split("\t")
            if t[0] == "TOTAL":
                total = tuple(int(x) for x in t[1:3])
            elif t[0] == "PIECE":
                p = Piece()
                p.slot, p.item, p.index, p.name = int(t[1]), t[2], int(t[3]), t[4]
                p.loop, p.filled, p.tone = t[5] == "1", t[6] == "1", int(t[7])
                p.stroke_mult, p.surfaces, p.stroke_in_R = float(t[8]), 0, 0.0
                npts = int(t[12])
                p.pts = [tuple(float(v) for v in s.split(",")) for s in t[13:13 + npts]]
                assert len(p.pts) == npts, "%s %s 점 수 불일치" % (p.item, p.name)
                pieces.append(p)
    return pieces, total


# ============================================================================
# 4. 카드 배치 — 슬롯 고정 배율 + 넘침 축소
# ============================================================================
def box_fit_shrink(unit_shapes):
    xs = [x for s in unit_shapes for x, _ in s]
    ys = [y for s in unit_shapes for _, y in s]
    if not xs:
        return 1.0
    span = max(max(xs) - min(xs), max(ys) - min(ys))
    return ICON_VIEWBOX / span if span > ICON_VIEWBOX else 1.0


def measure_card_item(pieces, unit_shapes, size):
    """(최단 꺾임변 pt, 그 변의 획 배수, 최소 잉크변 pt, 그 잉크의 획 배수, 축소율).
    획은 **조각마다** size × 2.2/64 × strokeMult 다."""
    shrink = box_fit_shrink(unit_shapes)
    k = shrink * (size / ICON_VIEWBOX)
    worst_edge_pt = worst_edge_w = None
    worst_ink_pt = worst_ink_w = None
    for p, s in zip(pieces, unit_shapes):
        pts = [(x * k, y * k) for x, y in s]
        stroke = card_stroke_pt(size, p.stroke_mult)
        e = min_corner_edge(pts, p.loop)
        if e is not None:
            if worst_edge_w is None or e / stroke < worst_edge_w:
                worst_edge_pt, worst_edge_w = e, e / stroke
        sp = ink_span(pts)
        if worst_ink_w is None or sp / stroke < worst_ink_w:
            worst_ink_pt, worst_ink_w = sp, sp / stroke
    return worst_edge_pt, worst_edge_w, worst_ink_pt, worst_ink_w, shrink


def card_unit_shapes(pieces, units_per_R, center_y_in_R):
    return [[(x * units_per_R, (y - center_y_in_R) * units_per_R) for x, y in p.pts] for p in pieces]


def fxpet_unit_shapes(pieces):
    return [[(x - 32.0, y - 32.0) for x, y in p.pts] for p in pieces]


# ============================================================================
# 5. 교정 — 깨지면 이 실행의 숫자를 전부 폐기한다
# ============================================================================
def calibrate():
    fails, checked = [], 0

    def eq(label, got, want, tol=1e-9):
        nonlocal checked
        checked += 1
        if abs(got - want) > tol:
            fails.append("%s: %.8f ≠ %.8f" % (label, got, want))

    # (가) 표면별 획 — 알려진 값
    eq("카드 58pt 획", card_stroke_pt(58.0), 1.99375)
    eq("슬롯행 24pt 획", card_stroke_pt(24.0), 0.825)
    eq("상세 38pt 획", card_stroke_pt(38.0), 1.30625)
    eq("58pt ×1.5 조각 획", card_stroke_pt(58.0, 1.5), 2.990625)
    eq("58pt 배수 0 = ×1.0", card_stroke_pt(58.0, 0.0), 1.99375)
    # (나) 옛 자 재현 — 44px 에서 1.87
    eq("옛 v1 44px 획", old_v1_stroke_pt(RETIRED_CARD_SIZE), 1.87)
    # (다) 같은 44px 를 **지금 자**로 재면 다른 값
    eq("현행 자로 잰 44px", card_stroke_pt(RETIRED_CARD_SIZE), 1.5125)
    # (라) 머리
    eq("R_pt@1.00", head_radius_pt(1.00), 7.755)
    eq("지름@0.75", 2 * head_radius_pt(0.75), 11.6325)
    eq("지름@0.35", 2 * head_radius_pt(0.35), 5.4285)
    # (마) 획 두 계통
    eq("W@0.75(v1)", v1_stroke_in_R(0.75), 0.3438640, 1e-6)
    eq("W@0.60(v1)", v1_stroke_in_R(0.60), 0.4298300, 1e-6)
    eq("W@1.00(v1)", v1_stroke_in_R(1.00), 0.2578980, 1e-6)
    # ★ 기대값을 손으로 적지 않는다 — 1pt ÷ R_pt(0.75) 로 **유도**하고, 독립 출처와 4자리에서 맞춘다
    #   (`docs/EQUIPMENT_SHAPE_SPEC_PACK_DETAIL_R2.md` §1-2 「하한 1pt = 0.1720 R @0.75」).
    #   첫 판은 여기에 0.1719535 를 손으로 적었고, 교정이 rc=3 으로 실행을 세웠다(맞는 값 0.17193209).
    eq("계약v2 실폭@0.75(유도)", handoff_stroke_in_R(0.11528, 0.75), 1.0 / head_radius_pt(0.75), 1e-12)
    # ★ 문서값 「0.1720 R」은 **3자리 값에 0을 붙여 쓴 것**이다(0.17193209 → 0.172).
    #   4자리로 비교하면 0.1719 ≠ 0.1720 으로 갈린다 — 그 갈림은 측정이 아니라 표기다.
    #   이 교정도 첫 판에 4자리로 적어 rc=3 을 냈다. **한 파일에서 손으로 적은 기대값이 두 번 틀렸다.**
    eq("계약v2 실폭@0.75 ↔ 문서 0.172", round(handoff_stroke_in_R(0.11528, 0.75), 3), 0.172)
    eq("계약v2 명목이 하한보다 크면 그대로",
       handoff_stroke_in_R(0.30, 0.75), 0.30, 1e-9)
    # (바) 기하 자
    square = [(0, 0), (1, 0), (1, 1), (0, 1)]
    eq("정사각 최단 꺾임변", min_corner_edge(square, True), 1.0)
    eq("정사각 잉크변", ink_span(square), 1.0)
    checked += 2
    if min_corner_edge(regular_polygon(0, 0, 1, 12), True) is not None:
        fails.append("정12각형에서 꺾임 변이 잡혔다 — 45° 문턱이 깨졌다")
    if min_corner_edge(regular_polygon(0, 0, 1, 4), True) is None:
        fails.append("정4각형에서 꺾임 변을 못 잡았다 — 검출기가 죽었다")
    # (사) 거리 자
    eq("원-원 간격", poly_min_distance(regular_polygon(-1.5, 0, 1, 64),
                                       regular_polygon(1.5, 0, 1, 64)), 1.0, 2e-3)
    # (아) 잉크 문턱 식 — MINER_MAGE J-5 가 인용한 값과 소수점까지 맞는가
    eq("1× 최악 위상 알파(24pt)", worst_phase_alpha_1x(0.825), 0.325, 1e-9)
    # (자) 음성 대조 — 일부러 틀린 기대값이 잡히는가
    checked += 1
    probe = abs(card_stroke_pt(58.0) - 2.0) > 1e-9
    if not probe:
        fails.append("음성 대조가 조용히 통과했다 — 교정기가 죽었다")
    return fails, checked


# ============================================================================
# 6. 본 실행
# ============================================================================
def main():
    print("╔══ 출하 다섯 표면 재측정 (design-equipment, 2026-09-27) ══╗")
    fails, checked = calibrate()
    if fails:
        print("  ✗ 교정 실패 %d건 — 이 실행의 숫자를 전부 폐기한다" % len(fails))
        for f in fails:
            print("    ·", f)
        return 3
    print("  교정 %d건 전건 통과(양성·음성·오탐 포함)" % checked)

    for p in (GOLDEN_CARD, GOLDEN_FXPET):
        if not os.path.exists(p):
            print("  ✗ 골든 없음:", p)
            return 3
    print("  입력 sha16: CardShapeGolden=%s · FxPetCardGolden=%s"
          % (sha16(GOLDEN_CARD), sha16(GOLDEN_FXPET)))

    card, body, frames, items, total = read_card_golden(GOLDEN_CARD)
    fx, fx_total = read_fxpet_golden(GOLDEN_FXPET)

    n_card_only = sum(1 for p in card if p.surfaces == 2)
    n_body_only = sum(1 for p in card if p.surfaces == 1)
    n_both = sum(1 for p in card if p.surfaces == 0)
    got_body, got_card = n_both + n_body_only, n_both + n_card_only
    ok = total is not None and (len(card) == total[0] and got_body == total[1] and got_card == total[2])
    print("  판독 대조(파일 자신의 TOTAL 행): pieces %d/%d · body %d/%d · card %d/%d → %s"
          % (len(card), total[0], got_body, total[1], got_card, total[2],
             "일치" if ok else "★불일치 — 판독 무효"))
    if not ok:
        return 3
    fx_items = sorted({(p.slot, int(p.item)) for p in fx})
    print("  FX/PET 판독: PIECE %d · 아이템 %d (파일 TOTAL %s)" % (len(fx), len(fx_items), fx_total))

    # ---- ① 다섯 표면 ----
    print("\n╔══ ① 출하 표면은 **다섯**이다 (32pt·44px 는 이 앱에 없다) ══╗")
    print("  %-20s %7s %11s %9s %9s  %s" % ("표면", "한 변", "획(pt)", "1×알파", "판정", "출처"))
    for name, size, kind, src in SURFACES:
        if kind == "vector":
            t = card_stroke_pt(size)
            a = worst_phase_alpha_1x(t)
            verdict = "통과" if a >= THETA_INK else "★미달"
            print("  %-20s %6.0fpt %11.5f %9.3f %9s  %s" % (name, size, t, a, verdict, src))
        else:
            print("  %-20s %6.0fpt %11s %9s %9s  %s" % (name, size, "비트맵", "—", "—", src))
    print("  (1× 알파 = t_pt − 0.5, 문턱 θ_ink = %.4f — 출처 DESIGN_FAN_MENU_ICONS R26-1. 2× 에서는 1.000)"
          % THETA_INK)
    print("\n  착용(몸) 표면 — 머리 지름과 두 획 계통")
    print("  %8s %12s %14s %16s" % ("배율", "머리 지름", "v1 획 W(R)", "계약v2 실폭(R)"))
    for s in SCALES:
        print("  %8.2f %11.2fpt %14.5f %16.5f"
              % (s, 2 * head_radius_pt(s), v1_stroke_in_R(s), handoff_stroke_in_R(0.11528, s)))
    print("  참고 — 은퇴한 44px: 옛 자 %.2fpt / 현행 자로 재면 %.4fpt(옛의 %.0f%%)"
          % (old_v1_stroke_pt(44.0), card_stroke_pt(44.0),
             100 * card_stroke_pt(44.0) / old_v1_stroke_pt(44.0)))
    print("  ★ 획 배수(획 단위)는 세 벡터 표면에서 **같다** — size 가 분자·분모에서 약분된다.")
    print("     세 표면이 실제로 갈라지는 축은 **절대 pt** 와 그에 걸린 잉크 문턱이다(위 표).")

    # ---- ② 인계본 16종 · 벡터 표면 ----
    print("\n╔══ ② 인계본 16종 — 벡터 표면 3곳 (획 배수는 공통, pt 는 표면마다) ══╗")
    print("  %-14s %-5s %8s %8s | %s" % ("아이템", "슬롯", "최단변", "잉크", "58pt / 38pt / 24pt 최단변 실측(pt)"))
    per_slot = {}
    for slot, item in items:
        pieces = [p for p in card if p.item == item and p.surfaces in (0, 2)]
        if not pieces:
            continue
        upr, cy = frames[slot]
        us = card_unit_shapes(pieces, upr, cy)
        cols, ew, iw, shrink = [], None, None, None
        for size in VECTOR_SIZES:
            e_pt, e_w, i_pt, i_w, shrink = measure_card_item(pieces, us, size)
            cols.append(e_pt)
            ew, iw = e_w, i_w
        print("  %-14s %-5s %8s %8.2f | %s  축소 %.3f"
              % (item, SLOT_NAME[slot], "—" if ew is None else "%.2f획" % ew, iw,
                 " / ".join("—" if c is None else "%5.2f" % c for c in cols), shrink))
        key = SLOT_NAME[slot]
        cand = (ew if ew is not None else 99.0, iw, item)
        if key not in per_slot or cand < per_slot[key]:
            per_slot[key] = cand
    print("\n  카테고리별 최악(하한: 변 1.0획 · 잉크 1.5획):")
    for k in ("HEAD", "EYES", "NECK", "BACK"):
        ew, iw, item = per_slot[k]
        print("   %-5s 변 %-7s 잉크 %.2f획  (%s)" % (k, "—" if ew >= 99 else "%.2f획" % ew, iw, item))
    print("  ★ 여기 없는 8종(HEAD 4·5 · EYES 4·5 · NECK 4·5 · BACK 4·5 = v1 잔여)은 골든에 행이 없다 —")
    print("     이 자로는 **미확인**이다. 그 8종을 재려면 프로덕션 좌표 덤프가 필요하다(Tools/ShapeDump).")

    # ---- ③ FX/PET 12종 ----
    print("\n╔══ ③ FX/PET 12종 — 벡터 표면 3곳 (★ 슬롯행에는 애초에 뜨지 않는다 → ⑥) ══╗")
    print("  %-16s %-4s %8s %8s | %s" % ("아이템", "슬롯", "최단변", "잉크", "58 / 38 / 24pt 최단변(pt)"))
    for slot, item in fx_items:
        pieces = [p for p in fx if p.slot == slot and int(p.item) == item]
        us = fxpet_unit_shapes(pieces)
        cols, ew, iw, shrink = [], None, None, None
        for size in VECTOR_SIZES:
            e_pt, e_w, i_pt, i_w, shrink = measure_card_item(pieces, us, size)
            cols.append(e_pt)
            ew, iw = e_w, i_w
        nm = (FX_NAME if slot == 5 else PET_NAME)[item]
        print("  %-16s %-4s %8s %8.2f | %s"
              % (nm, SLOT_NAME[slot], "—" if ew is None else "%.2f획" % ew, iw,
                 " / ".join("—" if c is None else "%5.2f" % c for c in cols)))

    # ---- ④ 착용 표면 — **자가 둘이다.** 문서 게이트와 실제 폭을 나란히 낸다 ----
    print("\n╔══ ④ 인계본 16종 — 착용(몸) 표면, 배율 네 곳 ══╗")
    print("  ★ 자가 **둘**이다. 섞으면 같은 조각이 통과·미달로 갈린다:")
    print("    (가) **문서 게이트** — `EQUIPMENT_HANDOFF_PORT_SPEC.md` §14-6 #14 · §14-9:")
    print("         「규칙은 R15와 같고 w 만 **그 배율의 1pt 실폭**으로 바꿨다」 ⇒ w 가 조각과 무관하게 균일하다.")
    print("    (나) **조각별 실제 폭** — `CharacterAccessoryRenderer.AddLine(handoffWidth)`:")
    print("         `max(strokeInR × 배수 × R, 1pt)` ⇒ 배수가 큰 조각(예 외알안경 테 ×1.5)에서만 (가)와 갈린다.")
    nominal = {}
    for p in card:
        if p.surfaces in (0, 1):
            nominal[(p.item, p.name)] = (p.stroke_in_R, p.stroke_mult)

    def worn_rows(uniform):
        """★ 최악값과 **그 값을 낸 조각 이름**을 함께 돌려준다 — 이름이 없으면
        「이미 사문서로 못박힌 조각인가」(r16_model.R15_DEAD)를 대조할 수 없다."""
        worst_by_slot, out = {}, []
        for slot, item in items:
            pieces = [p for p in body if p.item == item]
            if not pieces:
                continue
            row = []
            for s in SCALES:
                worst, worst_name = None, None
                for p in pieces:
                    e = min_corner_edge(p.pts, p.loop)
                    if e is None:
                        continue
                    nom, mult = nominal.get((p.item, p.name), (0.0, 1.0))
                    if uniform:
                        t = MIN_ACC_STROKE_PT / head_radius_pt(s)      # (가) 문서 게이트 w
                    elif nom > 0:
                        t = handoff_stroke_in_R(nom * (mult if mult > 0 else 1.0), s)   # (나)
                    else:
                        t = v1_stroke_in_R(s)
                    v = e / t
                    if worst is None or v < worst:
                        worst, worst_name = v, p.name
                row.append((worst, worst_name))
                k = (SLOT_NAME[slot], s)
                if worst is not None and (k not in worst_by_slot or worst < worst_by_slot[k][0]):
                    worst_by_slot[k] = (worst, item, worst_name)
            out.append((item, row))
        return out, worst_by_slot

    for label, uniform in (("(가) 문서 게이트 — w = 그 배율의 1pt 실폭", True),
                           ("(나) 조각별 실제 폭 — max(명목 × 배수, 1pt)", False)):
        rows, worst_by_slot = worn_rows(uniform)
        print("\n  %s" % label)
        print("  %-14s | %-38s | %s"
              % ("아이템", "최단 꺾임변(w 배수) @0.35 @0.60 @0.75 @1.00", "@0.75 을 낸 조각"))
        for item, row in rows:
            if all(v is None for v, _ in row):
                print("  %-14s | 꺾임 변 없음(매끈한 곡선)" % item)
            else:
                cells = "  ".join("    —  " if v is None else "%7.2f" % v for v, _ in row)
                binder = row[2][1] or "—"
                print("  %-14s | %-38s | %s" % (item, cells, binder))
        print("  카테고리별 최악(하한 1.0) — 값(아이템 · 조각):")
        for k in ("HEAD", "EYES", "NECK", "BACK"):
            cells = []
            for s in SCALES:
                if (k, s) in worst_by_slot:
                    v, item, nm = worst_by_slot[(k, s)]
                    cells.append("@%.2f %.2f(%s · %s)" % (s, v, item, nm))
            print("   %-5s %s" % (k, " · ".join(cells)))

    # 두 자가 실제로 갈리는 조각을 찍는다 — 「같은 값」이라고 쓰지 않기 위해서다
    print("\n  두 자가 갈리는 조각(배수 때문에 실폭 > 1pt 인 것):")
    split = []
    for (it, nm), (nom, mult) in sorted(nominal.items()):
        if nom <= 0:
            continue
        m = mult if mult > 0 else 1.0
        for s in SCALES:
            floor = MIN_ACC_STROKE_PT / head_radius_pt(s)
            if nom * m > floor + 1e-9:
                split.append((it, nm, s, nom * m, floor))
    if not split:
        print("   없다 — 네 배율 전부에서 1pt 하한이 이겨서 두 자가 **같은 값**이다.")
    else:
        for it, nm, s, w_act, w_floor in split[:12]:
            print("   %s %s @%.2f: 실폭 %.5f R > 게이트 w %.5f R (%.2f배)"
                  % (it, nm, s, w_act, w_floor, w_act / w_floor))
        print("   (총 %d건)" % len(split))

    # ---- ⑤ 외알안경 ----
    print("\n╔══ ⑤ 외알안경 — 「눈–가리개 ≥ 1.5획」 여유와 드러난 눈 크기 ══╗")
    eye = next(p for p in body if p.item == "EyesMonocle" and p.name == "Piece_ExposedEye")
    pod = next(p for p in body if p.item == "EyesMonocle" and p.name == "Piece_CB0")
    def circle(p):
        xs = [x for x, _ in p.pts]
        ys = [y for _, y in p.pts]
        return ((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2), (max(xs) - min(xs)) / 2
    eye_c, eye_r = circle(eye)
    pod_c, pod_r = circle(pod)
    gap = poly_min_distance(eye.pts, pod.pts)
    W = v1_stroke_in_R(0.75)
    print("  골든(몸 표면) 드러난 눈 반경 %.5f R = %.4f W · 중심 (%.4f, %.4f)"
          % (eye_r, eye_r / W, *eye_c))
    print("  골든(몸 표면) 알(가리개) 반경 %.5f R · 중심 (%.4f, %.4f)" % (pod_r, *pod_c))
    print("  경계 간 최소 거리 %.5f R = **%.4f획(W@0.75)** — 하한 1.5획, 여유 %+.2f%%"
          % (gap, gap / W, 100 * (gap / W / INK_BOX_MIN_W - 1)))
    print("  중심거리 검산: %.5f − %.5f − %.5f = %.5f R = %.4f획"
          % (math.dist(eye_c, pod_c), pod_r, eye_r,
             math.dist(eye_c, pod_c) - pod_r - eye_r,
             (math.dist(eye_c, pod_c) - pod_r - eye_r) / W))
    print("  눈 반경이 W/2(%.5f R)와 같은가: %s" % (W / 2, "예" if abs(eye_r - W / 2) < 1e-4 else "아니오"))
    v1_off, v1_pod, v1_eye = 0.62, 0.36, 0.33
    v1_gap = 2 * v1_off - v1_pod - v1_eye
    print("  v1 코드 계통(DrawnEyeRadiusRatio %.2f · MonocleRadiusRatio %.2f · offset %.2f):"
          " 간격 %.4f R = %.4f획" % (v1_eye, v1_pod, v1_off, v1_gap, v1_gap / W))
    print("  ⇒ 드러난 눈 반경이 **사양 %.2f R** 과 **골든 %.5f R** 로 갈린다(%.2f배)."
          % (v1_eye, eye_r, v1_eye / eye_r))
    print("     어느 쪽이 정본인지는 이 라운드에서 판정하지 않는다(design-face 공동 판정 대상).")
    print("  ★ 좌표 한 자리(0.00001 R)를 좁히면 여유가 %.4f획이 된다 — 하한까지의 폭이 좁다."
          % ((gap - 0.00001) / W))

    # ---- ⑥ 표면 소유권 ----
    print("\n╔══ ⑥ 표면 소유권 (코드에서 읽은 사실) ══╗")
    print("  슬롯행 = **4행 · [장비] 탭 전용**(SectionCount = 4 · SyncSlotRows 가 Tab.Equipment 고정)")
    print("   → HEAD·EYES·NECK·SHOULDERS 만 뜬다. FX·PET·HAIR 는 슬롯행에 **없다**.")
    print("  HAIR = IsRetiredSlot 하나가 카드(SectionSlot·SectionCountForTab)·슬롯행·몸(TryWear·RestoreFromSave)을")
    print("   동시에 닫는다 ⇒ 고를 수 있는 자리 0. 데이터는 그대로 둔다(42종이 등급 파생 분모).")
    print("  비트맵 우선순위: 카드(BuildCardArt) · 슬롯행(BuildSlotIcon) · 상세(RefreshDetailThumbArt)")
    print("   세 호출부가 ItemCatalog.CardSprite 를 **먼저** 묻는다 → 선언된 자리는 벡터를 그리지 않는다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
