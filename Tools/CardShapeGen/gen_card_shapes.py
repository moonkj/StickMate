#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""인계본 조각 생성기 — R16(카드) + R17(몸) 모델(정본) → 프로덕션 데이터. (coder, 2026-09-05)

    python3 Tools/CardShapeGen/gen_card_shapes.py            # 생성 + 곧바로 역대조(verify_card_shapes.py)
    python3 Tools/CardShapeGen/gen_card_shapes.py --check    # 생성물이 지금 모델과 같은지만 본다(파일 안 씀)

정본:
  · 카드 = design/equipment/verify/r16_model.py ICON (인계본 ItemIcon.dc.html 직접 파싱, 원문 의미 — 재현기 icons.js 가 버린
    fill:A 23건·획 배수 11건을 살린다). 카드 좌표는 R15 이후 한 번도 안 바뀌었다.
  · 몸  = r19_model.py WORN (§14-10-8 = R17 §14-10 + R18 팔레트 + R19 6건): 모자 2층(뒤층 SortBack, 바탕 조각 폐지) · 외알안경 = MIRROR + 조건부 반대쪽 눈 ·
    줄무늬타이 = SHIFT dy · 망토 2종 = 무대 뒤판/칼라/걸쇠(Body 전용) · 나머지 = 카드와 같은 좌표(R16).
    ★ 모자 맞춤은 **좌표를 두 벌 굽지 않고** 카드 좌표 + 아이템 변환(scale=u/u_card · scaleY=ky · offsetY)으로 낸다
      (§14-10-4 #16 「코더 선택」). 생성기가 변환 결과가 r17 좌표와 같음을 스스로 교정하고, 골든 BODY 줄은 r17 좌표 그대로다.
  · R17c 외알안경(알 = 반대쪽 눈의 거울 위치, 사슬·구슬 알 상대 오프셋) · R17d 망토 2단(짧은 = 옛 긴, 긴 = 발목) 착지 — PENDING_R17 비어 있음.
  · 흔들 구간(SWAY_RULES)·월요일 느슨함(MONDAY_DROP_R)은 이 생성기가 조각에 눕히는 계약 값이고, 색 역할은 palette_model.ROLE(정본)에서 굽는다.
세 곳에 기계로 눕힌다:
  1. Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs   — 코드 자리(HEAD/EYES/BACK 12종). partial 생성 파일.
  2. Assets/_Project/Resources/Items/equip_neck_{bowtie,striped,scarf,bell}.asset — 에셋 자리(NECK 4종). wornShapes + 몸 파라미터.
  3. Assets/_Project/Scripts/Tests/EditMode/Golden/CardShapeGolden.txt        — EditMode 골든(모델에서 굽는다).
★ 생성 직후 verify_card_shapes.py 가 생성물을 **다른 경로로 되읽어** 모델과 대조한다.
"""
import argparse, hashlib, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
VERIFY = os.path.join(REPO, "design", "equipment", "verify")
sys.path.insert(0, VERIFY)
import rig                          # noqa: E402
import handoff                      # noqa: E402
import r16_model as M16             # noqa: E402  (import 시 교정: 91조각 / H 17 / 잉크 7건 / 걸쇠 중심)
import r17_model as M17             # noqa: E402  (import 시 모자 맞춤 격자 탐색 — 실패하면 SystemExit)
import palette_model as PM          # noqa: E402  (재질 팔레트 — 조각 → 색 역할 ROLE, docs/EQUIPMENT_PALETTE.md §5)
import r19_model as M19             # noqa: E402  (R18+R19 — 팔레트 적용 + 모자 2층·선글라스/고글/외알안경 구슬·나비넥타이 dy·배낭 착용면. WORN/CARD 에 crole)
import r20_model as M20             # noqa: E402  (R20 — 나머지 26종 (가)군. 여기서는 v1 목 아이템(펜던트·반다나)의 N-1 착용선 dy 만 읽는다)

CS_OUT = os.path.join(REPO, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs")
GOLDEN_OUT = os.path.join(REPO, "Assets/_Project/Scripts/Tests/EditMode/Golden/CardShapeGolden.txt")
ITEMS_DIR = os.path.join(REPO, "Assets/_Project/Resources/Items")

# ---- 프로덕션 계약 상수 (Core/AccessoryShapeContract.cs 와 같은 값. verify 가 C# 소스에서 되읽어 대조한다) ----
TONE_PRIMARY, TONE_ACCENT, TONE_HIGHLIGHT, TONE_HEADINK, TONE_INKCONTRAST, TONE_GLASS = 0, 1, 3, 4, 5, 6   # Fixed(4)는 2026-09-05 제거 — 번호를 당겼다. Glass = R20 유리 판
TONE_SHADE = 2
# ★ 재질 팔레트(docs/EQUIPMENT_PALETTE.md §2 · §5, 2026-09-05): 조각의 역할은 palette_model.ROLE 이 정한다 —
#   채움 M → 0 · M2 → 1 · SH → 2 · EYE → 6 · CARDWASH → 카드 전용 워시 조각(1, α) + 양면 테 조각(선 역할)
#   채움 없는 선  INK → 5(잉크선) · M → 0 · M2 → 1 · W → 3.  H-1 바탕(B0b)은 부모의 채움 역할(잉크 아님). 등급색·고정색 0개.
FILL_TONE = {"M": TONE_PRIMARY, "M2": TONE_ACCENT, "SH": TONE_SHADE, "EYE": TONE_INKCONTRAST}
LINE_TONE = {"INK": TONE_HEADINK, "M": TONE_PRIMARY, "M2": TONE_ACCENT, "W": TONE_HIGHLIGHT}
SURF_UNSET, SURF_BODY, SURF_CARD = 0, 1, 2
DECIMALS = 5
LAYER_SLOT, LAYER_BACK, LAYER_BODY_FRONT = 0, 1, 2   # Core/AccessoryShapeContract.AccessoryPieceLayer
STAGE_BACK_SWAY = (16, 17)          # 무대 뒤판 49점 중 밑단(두 번째 Q 곡선 16점 + 양끝 이음점) — 아래 교정이 실측한다
PENDING_R17 = set()                 # R17c(외알안경) · R17d(망토 2단) 착지 — 보류 없음(코디네이터 통보 2026-09-05)

# ---- 흔들 점 구간(HemSway) — 설명문이 주장하는 동작(원칙 1)을 조각에 선언한다. v1 이 흔들던 것과 같은 자리.
#   ("all",)          조각 전체(v1 Bell: 방울 통째로)
#   ("lower", f)      y 가 [최저, 최저 + f·높이] 안인 연속 구간(v1 ScarfTail: 자락 끝) — 연속이 아니면 생성이 멈춘다
#   ("band", s, n)    인덱스 고정(망토 무대 뒤판 밑단 16..32) — _calibrate_sway 가 실측한다
SWAY_RULES = {
    ("shortcape", "STB"): ("band",) + STAGE_BACK_SWAY, ("longcape", "STB"): ("band",) + STAGE_BACK_SWAY,
    ("scarf", "B1"): ("lower", 0.35), ("scarf", "S2"): ("all",), ("scarf", "S3"): ("all",), ("scarf", "S4"): ("all",),
    ("bellnecklace", "B4"): ("all",), ("bellnecklace", "RB5"): ("all",), ("bellnecklace", "CB6"): ("all",),
    ("bellnecklace", "H7"): ("all",),
    ("stripedtie", "B1"): ("lower", 0.20),      # 날 끝점(줄무늬 F3 아래) — 줄무늬가 날에서 떨어져 나오지 않게 끝만
}
# ---- 줄무늬타이 「월요일마다 조금 느슨해진다」(원칙 1) — 에셋이 정본(v1 TieMondayLoosenDropRatio 0.12 가 그대로 데이터로 내려왔다).
#   AccessoryWornGate.WhenStateOn(1) 항으로 눕힌다: 월요일이면 모든 조각의 y 에 −0.12 R 이 더해진다. 카드는 상태를 모른다.
MONDAY_DROP_R = 0.12
GATE_ALWAYS, GATE_WHEN_STATE_ON = 0, 1

def sway_of(kind, src, pts):
    rule = SWAY_RULES.get((kind, src))
    if rule is None: return -1, 0
    if rule[0] == "all": return 0, len(pts)
    if rule[0] == "band": return rule[1], rule[2]
    lo = min(y for _, y in pts); hi = max(y for _, y in pts)
    idx = [i for i, (_, y) in enumerate(pts) if y <= lo + rule[1] * (hi - lo) + 1e-9]
    if not idx or idx != list(range(idx[0], idx[-1] + 1)):
        raise SystemExit("흔들 구간 실패: %s/%s 하부 %.2f 가 연속 구간이 아니다 %s" % (kind, src, rule[1], idx))
    return idx[0], len(idx)

ITEM_CONST = {
    "clothhat": ("Head", "HeadCap"), "furhat": ("Head", "HeadBeanie"), "fedora": ("Head", "HeadFedora"),
    "crown": ("Head", "HeadCrown"),
    "sunglasses": ("Eyes", "EyesSunglasses"), "roundglasses": ("Eyes", "EyesRound"),
    "goggles": ("Eyes", "EyesGoggles"), "monocle": ("Eyes", "EyesMonocle"),
    "bowtie": ("Neck", "NeckBowTie"), "stripedtie": ("Neck", "NeckStriped"),
    "scarf": ("Neck", "NeckScarf"), "bellnecklace": ("Neck", "NeckBell"),
    "shortcape": ("Shoulders", "BackCape"), "longcape": ("Shoulders", "BackLongCape"),
    "wings": ("Shoulders", "BackWings"), "backpack": ("Shoulders", "BackBackpack"),
}
KINDS = M16.KINDS
SORT_OF_SLOT = {"Head": "SortHead", "Eyes": "SortEyes", "Shoulders": "SortBack"}
NECK_ASSET = {"bowtie": "equip_neck_bowtie", "stripedtie": "equip_neck_striped",
              "scarf": "equip_neck_scarf", "bellnecklace": "equip_neck_bell"}
SLOT_ENUM = {"Head": 0, "Eyes": 1, "Neck": 2, "Shoulders": 3}

def frame_for(slot_key):
    box, top = handoff.SLOT_BOX[slot_key]
    u = box / 64.0
    return handoff.HEAD_R_PX / u, (handoff.HEAD_CY_PX - top - 32.0 * u) / handoff.HEAD_R_PX

def rnd(v): return round(float(v), DECIMALS)
def fmt(v):
    s = ("%%.%df" % DECIMALS) % rnd(v)
    if float(s) == 0.0: return "0"
    return s.rstrip("0").rstrip(".") if "." in s else s
def cs_float(v): return fmt(v) + "f"
def hex_rgb(h):
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def crole_of(kind, p):
    """r19 조각의 색 역할. r19 WORN/CARD 조각은 crole 을 나르고, 없으면 팔레트 ROLE."""
    cr = getattr(p, "crole", None)
    return cr if cr is not None else PM.role_of(kind, p.src)

def is_wash(kind, p):
    f, _ = crole_of(kind, p); return isinstance(f, tuple) and f[0] == "CARDWASH"

def is_matline(kind, p):
    f, l = crole_of(kind, p); return f is not None and not isinstance(f, tuple) and l in ("M", "M2")

def layer_of(kind, p):
    """조각 층(AccessoryPieceLayer): back → 몸 뒤(SortBack) · BACK 슬롯의 front → 몸통 앞(SortCapeFront) · 그 밖 = 슬롯 기본."""
    if getattr(p, "layer", None) == "back": return LAYER_BACK
    if getattr(p, "layer", None) == "front" and ITEM_CONST[kind][0] == "Shoulders": return LAYER_BODY_FRONT
    return LAYER_SLOT

def is_two_list(kind):
    """카드와 몸의 조각 집합이 다르면(망토 무대 · 모자 2층 분할/뒷벽 · 외알안경 눈/거울 · 배낭 착용면) 두 벌(카드 Card 명시 + 몸 bodyFixed)."""
    if kind in M16.CAPES: return True
    return {p.src for p in M19.WORN[kind]} != {p.src for p in M19.CARD[kind]}

class Row:
    """조각 하나의 계약 v2 값(모델 → 프로덕션). 좌표는 **카드 프레임**(R). 몸 좌표는 아이템 변환이 만든다(bodyFixed 제외)."""
    __slots__ = ("kind", "src", "name", "pts", "loop", "filled", "tone", "surfaces", "stroke_mult", "stroke_in_r",
                 "no_stroke", "alpha", "line_alpha", "under_back", "layer",
                 "sway_start", "sway_count", "sort", "role", "body_fixed", "body_pts", "arr_suffix")
    def __init__(self, kind, p, surfaces, pts=None, body_pts=None, body_fixed=False, variant=None, layer_from=None):
        """variant: None | "glass"(R20 유리 판 — 바탕 위 M2 α 사전 합성 불투명, 양면) | "fill"(채움만) | "rim"(테 — 선만). layer_from: 층을 읽을 조각(한 벌은 몸 조각)."""
        self.kind, self.src, self.role = kind, p.src, p.role
        self.name = ("Piece_ExposedEye" if p.call == "EYE" else "Piece_" + p.src) + {"glass": "glass", "rim": ("rim" if is_matline(kind, p) else "")}.get(variant, "")   # 눈은 *Eye 로 끝난다(EyesVisorOpacityTests.IsDrawnEye)
        self.pts = [(rnd(x), rnd(y)) for x, y in (pts if pts is not None else p.pts)]
        self.body_pts = [(rnd(x), rnd(y)) for x, y in body_pts] if body_pts is not None else None
        self.body_fixed = body_fixed
        self.arr_suffix = ""                     # 같은 src 의 카드/몸 조각이 둘 다 코드에 눕는 아이템(외알안경)은 몸 배열에 _worn 을 붙인다
        self.loop, self.filled = bool(p.loop), bool(p.filled)
        self.surfaces = surfaces
        self.stroke_mult = float(p.mult)
        self.stroke_in_r = float(p.nominal_R)
        self.under_back = 0
        # 층은 몸 조각의 것이다 — 한 벌 아이템은 카드(아이콘) 조각으로 Row 를 만들므로 몸 조각(layer_from)에서 읽는다. 카드 전용 조각은 층이 없다.
        self.layer = LAYER_SLOT if surfaces == SURF_CARD else layer_of(kind, layer_from if layer_from is not None else p)
        self.sway_start, self.sway_count = -1, 0
        # ---- 색 역할: r19 조각이 나르는 crole(= palette_model.ROLE + r19_model.ROLE_FIX). 모델의 fill/line 은 선 알파만 쓴다
        fill_role, line_role = crole_of(kind, p)
        wash = isinstance(fill_role, tuple) and fill_role[0] == "CARDWASH"
        matline = fill_role is not None and line_role in ("M", "M2")          # 채움 + 재질 윤곽(선글라스 렌즈) → 채움/테 두 조각
        if (wash or matline) != (variant is not None):
            raise SystemExit("역할 불일치: %s/%s 분할 조각(%s)인데 variant=%r" % (kind, p.src, "wash" if wash else "matline", variant))
        if (fill_role is not None) != bool(p.filled):
            raise SystemExit("역할 불일치: %s/%s 모델 filled=%s vs 역할 %r" % (kind, p.src, p.filled, (fill_role, line_role)))
        model_line_alpha = float(p.line[-1]) if p.line is not None else 0.0
        if variant == "glass":                       # R20 유리 판 — 바탕(카드 바탕 / 몸 잉크) 위 M2 를 α 로 사전 합성한 불투명 채움, 선 없음. 양면
            self.filled, self.tone, self.alpha, self.no_stroke, self.line_alpha = True, TONE_GLASS, float(fill_role[1]), True, 0.0
        elif variant == "fill":                      # 채움만(재질 윤곽은 아래 테 조각이 그린다)
            self.filled, self.tone, self.alpha, self.no_stroke, self.line_alpha = True, FILL_TONE[fill_role], 1.0, True, 0.0
        elif variant == "rim":                       # 테 — 선만(그 조각의 재질선)
            self.filled, self.tone, self.alpha, self.no_stroke, self.line_alpha = False, LINE_TONE[line_role], 0.0, False, model_line_alpha
        elif fill_role is not None:                  # 채움 조각: 재질색 불투명(R-1) · 윤곽 = 잉크(R-2) 또는 없음
            self.filled, self.tone, self.alpha = True, FILL_TONE[fill_role], 1.0
            self.no_stroke, self.line_alpha = line_role is None, (model_line_alpha if line_role is not None else 0.0)
        else:                                        # 선 조각: 잉크선 / 재질선 / 흰 하이라이트
            self.filled, self.tone, self.alpha = False, LINE_TONE[line_role], 0.0
            self.no_stroke, self.line_alpha = False, model_line_alpha
        # 바탕/눈 조각은 명목 획이 0 — 선을 안 그리지만 「인계본 조각」이어야 하므로 슬롯 획을 적는다(noStroke 라 화면에는 안 나온다)
        if self.stroke_in_r <= 0.0:
            self.stroke_in_r = M16.SLOT_STROKE_R[M16.ISLOT[kind]]
            assert self.no_stroke, (kind, p.src)
        # ---- 레이어
        slot_name = ITEM_CONST[kind][0]
        self.sort = SORT_OF_SLOT.get(slot_name, "SortNeck")   # 슬롯 기본 층 — 실제 층은 layer 가 LayerOrder 로 정한다
        # ---- 흔들 구간(원칙 1: 설명문이 말하는 동작). 몸 좌표가 따로 있으면 그 좌표로 재되, 결과는 카드/몸이 같은 점 번호다
        self.sway_start, self.sway_count = sway_of(kind, p.src, self.body_pts if self.body_pts is not None else self.pts)
        if self.sway_start >= 0 and self.no_stroke:
            raise SystemExit("흔들 구간 실패: %s/%s 는 선이 없어 렌더러가 흔들지 않는다" % (kind, p.src))


def under_backs(rows):
    """모델 규칙(r15): 앞 조각 중 채운 것 안에 내 점의 90% 이상이 들어가면 그 위에 얹힌다(여럿이면 마지막)."""
    for i, r in enumerate(rows):
        r.under_back = 0
        for j in range(i):
            q = rows[j]
            if not q.filled: continue
            cov = sum(1 for pt in r.pts if rig.contains(q.pts, pt)) / len(r.pts)
            if cov >= 0.9: r.under_back = i - j

# ============================================================================
# 아이템 변환 — 카드 좌표 → 몸 좌표 (r17 HAT_FIT 을 카드 프레임 기준 아핀으로 분해)
# ============================================================================
def item_transform(kind):
    # 그룹 알파: 인계본 등 아이콘 0.40 은 팔레트(L-3 「장비 전부 불투명」)로 1.0 — 0 = 없음
    ga = 0.0 if PM.GROUP_ALPHA_BODY >= 1.0 else (M16.WORN[kind][0].group_alpha if M16.WORN[kind] and M16.WORN[kind][0].group_alpha != 1.0 else 0.0)
    t = dict(group_alpha=ga, scale=0.0, scale_y=0.0, offset_y=0.0, mirror=False)
    if kind in PENDING_R17 or is_two_list(kind): return t          # 두 벌 아이템은 몸 좌표를 그대로 굽는다(변환 없음)
    if kind in M17.HEAD_KINDS:                                      # 한 벌 모자(털모자): HAT_FIT 을 카드 프레임 아핀으로 분해
        f = M17.HAT_FIT[kind]
        fx, fy, sx = handoff.icon_to_R(kind)
        c0 = fy(0.0)
        sy = (f["u"] / sx) * f["ky"]
        t.update(scale=f["u"] / sx, scale_y=f["ky"], offset_y=f["dy"] - c0 * sy)
    elif kind == "stripedtie":
        t.update(offset_y=M17.TIE_DY)
    elif kind == "bowtie":
        t.update(offset_y=M19.BOWTIE_DY)                            # R19 ⑤ N-1 예외 폐지
    return t

def apply_transform(pts, t):
    s = t["scale"] if t["scale"] > 0 else 1.0
    sy = s * (t["scale_y"] if t["scale_y"] > 0 else 1.0)
    m = -1.0 if t["mirror"] else 1.0
    return [(x * s * m, y * sy + t["offset_y"]) for x, y in pts]

def split_rows(kind, p, surfaces, **kw):
    """역할이 요구하는 분할: CARDWASH(유리 렌즈, R20) → [유리 판(양면), 테] · 채움+재질 윤곽(선글라스 렌즈) → [채움, 테] · 그 밖 [조각]."""
    if is_wash(kind, p):
        return [Row(kind, p, surfaces, variant="glass", **kw), Row(kind, p, surfaces, variant="rim", **kw)]
    if is_matline(kind, p):
        return [Row(kind, p, surfaces, variant="fill", **kw), Row(kind, p, surfaces, variant="rim", **kw)]
    return [Row(kind, p, surfaces, **kw)]

def rows_for(kind):
    """한 아이템의 조각 목록(방출 순서)과 골든용 몸 좌표.
    · 한 벌(카드 = 몸 좌표 + 아이템 변환): 조각 surfaces 0
    · 두 벌(망토 무대 · 모자 2층 · 외알안경 · 배낭 착용면): 카드(Card 명시, 아이콘 좌표) + 몸(Body 명시, bodyFixed, r19 좌표)"""
    t = item_transform(kind)
    if is_two_list(kind):
        icon = [r for p in M19.CARD[kind] for r in split_rows(kind, p, SURF_CARD)]
        card_srcs = {p.src for p in M19.CARD[kind]}
        worn = []
        for p in M19.WORN[kind]:
            for r in split_rows(kind, p, SURF_BODY, pts=p.pts, body_pts=p.pts, body_fixed=True):
                if p.src in card_srcs: r.arr_suffix = "_worn"     # 같은 src 의 카드 배열과 구분(좌표가 다르다)
                worn.append(r)
        under_backs(icon); under_backs(worn)
        return icon + worn
    card16 = {p.src: p for p in M19.CARD[kind]}
    rows = []
    for p in M19.WORN[kind]:
        rows += split_rows(kind, card16[p.src], SURF_UNSET, body_pts=p.pts, layer_from=p)
    # 교정: 카드 좌표에 아이템 변환을 걸면 r19 몸 좌표가 나와야 한다
    for r in rows:
        if r.body_pts is None: continue
        got = apply_transform(r.pts, t)
        worst = max(max(abs(a[0] - b[0]), abs(a[1] - b[1])) for a, b in zip(got, r.body_pts))
        if worst > 2e-5:
            raise SystemExit("교정 실패: %s/%s 아이템 변환이 r19 몸 좌표와 %.6f R 다르다" % (kind, r.src, worst))
    card_rows = [r for r in rows if r.surfaces != SURF_BODY]
    under_backs(card_rows)
    return rows

# ---- 교정: 무대 뒤판의 흔들 구간이 실제로 밑단인가(R17d 뒤판 — 설계 표의 16..32 와 같은지도 본다)
def _calibrate_sway():
    for kind in M16.CAPES:
        p = [q for q in M19.WORN[kind] if q.src == "STB"][0]
        s, n = STAGE_BACK_SWAY
        if getattr(p, "sway", None) not in (None, (s, n)):
            raise SystemExit("교정 실패: %s 설계 표의 흔들 구간 %r ≠ 생성기 %r" % (kind, p.sway, (s, n)))
        band = [p.pts[i][1] for i in range(s, s + n)]
        outside = min(p.pts[s - 1][1], p.pts[(s + n) % len(p.pts)][1])
        lowest = min(range(len(p.pts)), key=lambda i: p.pts[i][1])
        if max(band) > outside + 1e-9 or not (s <= lowest < s + n):
            raise SystemExit("교정 실패: %s 무대 뒤판 %d..%d 이 밑단이 아니다" % (kind, s, s + n - 1))
_calibrate_sway()

def sha(path):
    with open(path, "rb") as f: return hashlib.sha256(f.read()).hexdigest()[:16]

# ★ provenance 스탬프 — 이 생성기가 읽는 정본 파일 전부(요구 E-0, docs/TEST_PLAN_FAN_AND_EQUIPMENT.md E10).
#   여기서 하나라도 빠지면 그 모델이 바뀌어도 스탬프는 그대로라 「현행이다」 판정이 그 변경을 못 본다(2026-09-05 r19 사고).
#   Tests/EditMode/CardShapeGeneratorStampTests 가 이 스크립트의 import 를 되읽어 모델 파일이 전부 스탬프에 있는지 감사한다.
STAMP_SOURCES = [
    ("r16_model.py", os.path.join(VERIFY, "r16_model.py")),
    ("r17_model.py", os.path.join(VERIFY, "r17_model.py")),
    ("r19_model.py", os.path.join(VERIFY, "r19_model.py")),
    ("r20_model.py", os.path.join(VERIFY, "r20_model.py")),
    ("palette_model.py", os.path.join(VERIFY, "palette_model.py")),
    ("ItemIcon.dc.html", handoff.source_path()),
]

def stamp_line():
    return " · ".join("%s sha256[:16]=%s" % (name, sha(path)) for name, path in STAMP_SOURCES)


# ============================================================================
# 1. C# 생성 파일 (HEAD / EYES / BACK)
# ============================================================================
def gen_cs():
    kinds = [k for k in KINDS if ITEM_CONST[k][0] != "Neck"]
    out = []; w = out.append
    w("// <auto-generated>")
    w("//   Tools/CardShapeGen/gen_card_shapes.py 가 design/equipment/verify/r16_model.py(카드) · r19_model.py(몸, r17 위에 R18 팔레트 + R19) · palette_model.py(색 역할)에서 굽는다.")
    w("//   모델은 인계본 docs/handoff/design_handoff_equipment_window/reference/ItemIcon.dc.html 을 직접 파싱해")
    w("//   handoff.icon_to_R / stage_to_R 로 옮긴 것이다(EQUIPMENT_HANDOFF_PORT_SPEC §1-1 · §14-0 · §14-10).")
    w("//   정본 스탬프: " + stamp_line())
    w("//   ★ 손으로 고치지 마라. 고칠 것은 모델이고, 생성 뒤 Tools/CardShapeGen/verify_card_shapes.py 가 역대조한다.")
    w("//   ★ 좌표 단위: 머리 중심 원점 · R 배수 · y 위 · +x 진행 방향, 카드 프레임. 소수 %d자리." % DECIMALS)
    w("//   ★ 한 벌 아이템(눈 3 · 목 4 · 털모자 · 날개): 몸 좌표 = 카드 좌표 × 아이템 변환 — 코드 자리는 WornTransformCode(털모자 HAT_FIT u·ky·dy),")
    w("//     에셋 자리는 wornOffsetYInR(줄무늬타이 · 나비넥타이 dy). 두 벌 아이템(모자 3 · 외알안경 · 망토 2 · 배낭): 몸 조각은 bodyFixed 좌표(_worn 배열 · 반전은 좌표에 이미 들어 있다).")
    w("//   ★ 보류: %s." % (", ".join(sorted(PENDING_R17)) + " (리더 지시 — 갱신본 대기, 몸 = R16)" if PENDING_R17 else "없음 (R17c 외알안경 · R17d 망토 2단 착지)"))
    w("// </auto-generated>")
    w("using System.Collections.Generic;")
    w("using UnityEngine;")
    w("using StickMate.Core;")
    w("")
    w("namespace StickMate.Interaction")
    w("{")
    w("    internal static partial class AccessoryShapeBuilder")
    w("    {")
    w("        /// <summary>이 자리의 좌표를 <b>인계본 조각 표</b>(이 파일)가 갖는가. NECK 4종은 에셋이 갖는다(여기서는 false).</summary>")
    w("        internal static bool IsHandoffCode(EquipmentSlot slot, int item)")
    w("        {")
    w("            switch (slot)")
    w("            {")
    for slot_name in ("Head", "Eyes", "Shoulders"):
        consts = [ITEM_CONST[k][1] for k in kinds if ITEM_CONST[k][0] == slot_name]
        w("                case EquipmentSlot.%s:" % slot_name)
        w("                    return " + " || ".join("item == %s" % c for c in consts) + ";")
    w("                default:")
    w("                    return false;")
    w("            }")
    w("        }")
    w("")
    w("        /// <summary>코드 자리의 아이템 단위 몸 표면 파라미터(그룹 알파 · 배율 u/u_card · 세로 압축 ky · 세로 오프셋 · 반전). 에셋 자리는 ItemCatalog.WornTransform.</summary>")
    w("        internal static AccessoryWornTransform WornTransformCode(EquipmentSlot slot, int item)")
    w("        {")
    w("            switch (slot)")
    w("            {")
    for slot_name in ("Head", "Eyes", "Shoulders"):
        w("                case EquipmentSlot.%s:" % slot_name)
        w("                    switch (item)")
        w("                    {")
        for k in kinds:
            if ITEM_CONST[k][0] != slot_name: continue
            t = item_transform(k)
            w("                        case %s: return new AccessoryWornTransform(%s, %s, %s, %s, %s);   // %s%s" % (
                ITEM_CONST[k][1], cs_float(t["group_alpha"]), cs_float(t["scale"]), cs_float(t["scale_y"]), cs_float(t["offset_y"]),
                "true" if t["mirror"] else "false", k, " (보류)" if k in PENDING_R17 else ""))
        w("                        default: return AccessoryWornTransform.None;")
        w("                    }")
    w("                default:")
    w("                    return AccessoryWornTransform.None;")
    w("            }")
    w("        }")
    w("")
    w("        /// <summary>인계본 조각을 <paramref name=\"sink\"/>에 더한다. 이 자리를 이 표가 갖지 않으면 false(옛 switch 로).</summary>")
    w("        private static bool AppendHandoff(List<Shape> sink, EquipmentSlot slot, int item, in Rig rig, AccessorySurface surface)")
    w("        {")
    w("            if (!IsHandoffCode(slot, item)) return false;")
    w("            AccessoryWornTransform xf = surface == AccessorySurface.Body ? WornTransformCode(slot, item) : AccessoryWornTransform.None;")
    w("            switch (slot)")
    w("            {")
    for slot_name in ("Head", "Eyes", "Shoulders"):
        w("                case EquipmentSlot.%s: return AppendHandoff%s(sink, item, rig, xf);" % (slot_name, slot_name))
    w("                default: return false;")
    w("            }")
    w("        }")
    for slot_name in ("Head", "Eyes", "Shoulders"):
        w("")
        w("        private static bool AppendHandoff%s(List<Shape> sink, int item, in Rig rig, in AccessoryWornTransform xf)" % slot_name)
        w("        {")
        w("            switch (item)")
        w("            {")
        for k in kinds:
            if ITEM_CONST[k][0] != slot_name: continue
            rows = rows_for(k)
            w("                case %s:   // %s %s — 조각 %d" % (ITEM_CONST[k][1], M16.KO[k], k, len(rows)))
            for r in rows:
                w("                    HandoffPiece(sink, rig, xf, %s, \"%s\", Handoff_%s_%s%s, loop: %s, filled: %s, tone: %d, surfaces: %d, "
                  "strokeMult: %s, strokeInR: %s, noStroke: %s, alpha: %s, lineAlpha: %s, underBack: %d, "
                  "layer: %d, swayStart: %d, swayCount: %d, bodyFixed: %s);"
                  % (r.sort, r.name, k, r.src, r.arr_suffix, "true" if r.loop else "false", "true" if r.filled else "false", r.tone,
                     r.surfaces, cs_float(r.stroke_mult), cs_float(r.stroke_in_r), "true" if r.no_stroke else "false",
                     cs_float(r.alpha), cs_float(r.line_alpha), r.under_back,
                     r.layer, r.sway_start, r.sway_count, "true" if r.body_fixed else "false"))
            w("                    return true;")
        w("                default:")
        w("                    return false;")
        w("            }")
        w("        }")
    w("")
    w("        // ==================== 좌표 (R 단위, 카드 프레임 · bodyFixed 조각은 몸 프레임) ====================")
    for k in kinds:
        seen = set()
        for r in rows_for(k):
            key = (r.src, r.arr_suffix)
            if key in seen: continue          # 워시/테 두 조각은 같은 좌표 배열을 쓴다
            seen.add(key)
            w("")
            w("        /// <summary>%s(%s) %s · %s · %d점</summary>" % (M16.KO[k], k, r.src, r.role, len(r.pts)))
            w("        private static readonly float[] Handoff_%s_%s%s =" % (k, r.src, r.arr_suffix))
            w("        {")
            line = "           "
            for x, y in r.pts:
                tok = " %s, %s," % (cs_float(x), cs_float(y))
                if len(line) + len(tok) > 118:
                    w(line); line = "           "
                line += tok
            if line.strip(): w(line)
            w("        };")
    w("    }")
    w("}")
    return "\n".join(out) + "\n"


# ============================================================================
# 2. NECK 에셋 (wornShapes 스트림 문법 + 아이템 몸 파라미터)
# ============================================================================
def terms_for(pts, state_drop_r=0.0):
    """점 스트림. state_drop_r 가 있으면 y 에 「상태 켜짐(월요일)일 때만」 기저 0(R) × −drop 항을 셋째로 더한다."""
    t = [len(pts)]
    for x, y in pts:
        t += [1, 0, 0, 0, 1, x]             # sum(x): 1항 — 기저 0(HeadRadius) 계수 x
        if state_drop_r:
            t += [3, 4, 0, 0, 0, 0, 0, 0, 1, y, 0, GATE_WHEN_STATE_ON, 0, 1, -state_drop_r]
        else:
            t += [2, 4, 0, 0, 0, 0, 0, 0, 1, y]  # sum(y): 2항 — 기저 4(HeadCenterLine) + 기저 0 × y
    return t

def state_drop_of(kind):
    return MONDAY_DROP_R if kind == "stripedtie" else 0.0

def yaml_entry(r):
    drop = state_drop_of(r.kind)
    lines = ["  - name: %s" % r.name,
             "    loop: %d" % (1 if r.loop else 0),
             "    filled: %d" % (1 if r.filled else 0),
             "    tone: %d" % r.tone,
             "    swayStart: %d" % r.sway_start,
             "    swayCount: %d" % r.sway_count,
             "    swingDegrees: 0",
             "    surfaces: %d" % r.surfaces,
             "    strokeMult: %s" % fmt(r.stroke_mult),
             "    strokeInR: %s" % fmt(r.stroke_in_r),
             "    noStroke: %d" % (1 if r.no_stroke else 0),
             "    alpha: %s" % fmt(r.alpha),
             "    lineAlpha: %s" % fmt(r.line_alpha),
             "    underBack: %d" % r.under_back,
             "    layer: %d" % r.layer,
             "    bodyFixed: %d" % (1 if r.body_fixed else 0),
             "    terms:"]
    for v in terms_for(r.pts, drop):
        lines.append("    - %s" % (str(int(v)) if isinstance(v, int) else fmt(v)))
    return lines

ITEM_KEYS = ("wornGroupAlpha", "wornScale", "wornScaleY", "wornOffsetYInR", "wornMirrorX")

# ★ R20 N-1 — v1 목 아이템(조형은 아직 v1 스트림 그대로)의 착용선 dy 만 아이템 파라미터로 전사한다. 정본 = r20_model.ga_*()[1]
#   (머리 잉크 원반 정본 반경 1.17193 R 기준 「원반 밑 0.03 R 아래」). 스트림은 한 비트도 안 바뀐다 — AppendWorn 이 배치만 옮긴다.
V1_NECK_ITEM_DY = {"pendant": ("equip_neck_pendant", M20.ga_pendant()[1]), "bandana": ("equip_neck_bandana", M20.ga_bandana()[1])}

def patch_item_keys(text, transform):
    """wornShapes 는 손대지 않고 아이템 몸 파라미터 5키만 다시 쓴다(끝에 붙인다). 멱등."""
    lines = [l for l in text.replace("\r\n", "\n").split("\n") if not any(l.startswith("  %s:" % k) for k in ITEM_KEYS)]
    while lines and lines[-1] == "": lines.pop()
    lines += ["  wornGroupAlpha: %s" % fmt(transform["group_alpha"]), "  wornScale: %s" % fmt(transform["scale"]),
              "  wornScaleY: %s" % fmt(transform["scale_y"]), "  wornOffsetYInR: %s" % fmt(transform["offset_y"]),
              "  wornMirrorX: %d" % (1 if transform["mirror"] else 0)]
    return "\n".join(lines) + "\n"

def patch_asset(text, kind):
    """wornShapes 목록을 인계본 조각으로 통째로 바꾸고 아이템 몸 파라미터 5키를 그 뒤에 적는다. 멱등."""
    lines = text.replace("\r\n", "\n").split("\n")
    try:
        ws = next(i for i, l in enumerate(lines) if l == "  wornShapes:")
    except StopIteration:
        raise SystemExit("%s: wornShapes 키가 없다" % kind)
    end = len(lines)
    for i in range(ws + 1, len(lines)):
        l = lines[i]
        if l == "": continue
        if l.startswith("  ") and not l.startswith("  - ") and not l.startswith("    "):
            end = i; break
    out = []
    for r in rows_for(kind): out += yaml_entry(r)
    t = item_transform(kind)
    out += ["  wornGroupAlpha: %s" % fmt(t["group_alpha"]), "  wornScale: %s" % fmt(t["scale"]),
            "  wornScaleY: %s" % fmt(t["scale_y"]), "  wornOffsetYInR: %s" % fmt(t["offset_y"]),
            "  wornMirrorX: %d" % (1 if t["mirror"] else 0)]
    tail = [l for l in lines[end:] if l.strip() != "" and not any(l.startswith("  %s:" % k) for k in ITEM_KEYS)]
    return "\n".join(lines[:ws + 1] + out + tail) + "\n"


# ============================================================================
# 3. 골든 (EditMode 테스트 기대값 — 모델에서 굽는다)
# ============================================================================
def gen_golden():
    out = []; w = out.append
    w("# StickMate 인계본 조각 골든 — 계약 v2 / R16 카드 + R17 몸 (2026-09-05). 생성: python3 Tools/CardShapeGen/gen_card_shapes.py")
    w("# 정본은 design/equipment/verify/r16_model.py(카드) · r19_model.py(몸 = r17 + R18 팔레트 + R19) · palette_model.py(색 역할). 이 파일을 손으로 고치지 마라.")
    w("# 정본 스탬프: " + stamp_line())
    w("# PIECE\tslot\titem\tindex\tname\tloop\tfilled\ttone\tsurfaces\tstrokeMult\tstrokeInR\tnoStroke\talpha\tlineAlpha\tunderBack\tlayer\tswayStart\tswayCount\tbodyFixed\tpointCount\tx,y(카드 프레임 R, 소수 %d자리)..." % DECIMALS)
    w("# BODY\tslot\titem\tbodyIndex\tname\tpointCount\tx,y(몸 프레임 R — r17 좌표 그대로)...")
    w("# ITEM\tslot\titem\tgroupAlpha\tscale\tscaleY\toffsetYInR\tmirrorX\tpending")
    w("# FRAME\tslot\tunitsPerR\tcenterYInR   (인계본 슬롯 박스에서 유도 — AccessoryCardIcon.Frame 과 대조)")
    w("# STATE\tslot\titem\tdropInR   (상태 켜짐(월요일)일 때 모든 몸 조각의 y 가 내려가는 양 — 에셋 게이트 항)")
    w("# TOTAL\tpieces\tbodyPieces\tcardPieces\thighlights")
    total = body = card = hl = 0
    for k in KINDS:
        slot_name = ITEM_CONST[k][0]
        t = item_transform(k)
        w("ITEM\t%d\t%s\t%s\t%s\t%s\t%s\t%d\t%d" % (SLOT_ENUM[slot_name], ITEM_CONST[k][1], fmt(t["group_alpha"]), fmt(t["scale"]),
                                                    fmt(t["scale_y"]), fmt(t["offset_y"]), 1 if t["mirror"] else 0, 1 if k in PENDING_R17 else 0))
        if state_drop_of(k):
            w("STATE\t%d\t%s\t%s" % (SLOT_ENUM[slot_name], ITEM_CONST[k][1], fmt(state_drop_of(k))))
        rows = rows_for(k)
        bi = 0
        for i, r in enumerate(rows):
            total += 1
            eff = r.surfaces if r.surfaces else (SURF_BODY | SURF_CARD)
            card += 1 if eff & SURF_CARD else 0
            hl += 1 if r.tone == TONE_HIGHLIGHT else 0
            w("PIECE\t%d\t%s\t%d\t%s\t%d\t%d\t%d\t%d\t%s\t%s\t%d\t%s\t%s\t%d\t%d\t%d\t%d\t%d\t%d\t%s" % (
                SLOT_ENUM[slot_name], ITEM_CONST[k][1], i, r.name, 1 if r.loop else 0, 1 if r.filled else 0, r.tone,
                r.surfaces, fmt(r.stroke_mult), fmt(r.stroke_in_r), 1 if r.no_stroke else 0, fmt(r.alpha),
                fmt(r.line_alpha), r.under_back, r.layer, r.sway_start, r.sway_count,
                1 if r.body_fixed else 0, len(r.pts), "\t".join("%s,%s" % (fmt(x), fmt(y)) for x, y in r.pts)))
        for r in rows:
            eff = r.surfaces if r.surfaces else (SURF_BODY | SURF_CARD)
            if not (eff & SURF_BODY): continue
            body += 1
            bp = r.body_pts if r.body_pts is not None else apply_transform(r.pts, t)
            w("BODY\t%d\t%s\t%d\t%s\t%d\t%s" % (SLOT_ENUM[slot_name], ITEM_CONST[k][1], bi, r.name, len(bp),
                                              "\t".join("%s,%s" % (fmt(x), fmt(y)) for x, y in bp)))
            bi += 1
    for slot_name, key in (("Head", "head"), ("Eyes", "eyes"), ("Neck", "neck"), ("Shoulders", "back")):
        upr, cy = frame_for(key)
        w("FRAME\t%d\t%.6f\t%.6f" % (SLOT_ENUM[slot_name], upr, cy))
    w("TOTAL\t%d\t%d\t%d\t%d" % (total, body, card, hl))
    return "\n".join(out) + "\n"


# ============================================================================
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="파일을 쓰지 않고 지금 생성물과 같은지만 본다")
    ap.add_argument("--no-verify", action="store_true")
    a = ap.parse_args()

    cs = gen_cs(); golden = gen_golden()
    assets = {}
    for kind, name in NECK_ASSET.items():
        path = os.path.join(ITEMS_DIR, name + ".asset")
        with open(path, encoding="utf-8") as f: assets[path] = patch_asset(f.read(), kind)
    for kind, (name, dy) in V1_NECK_ITEM_DY.items():
        path = os.path.join(ITEMS_DIR, name + ".asset")
        t = dict(group_alpha=0.0, scale=0.0, scale_y=0.0, offset_y=rnd(dy), mirror=False)
        with open(path, encoding="utf-8") as f: assets[path] = patch_item_keys(f.read(), t)

    targets = {CS_OUT: cs, GOLDEN_OUT: golden}
    targets.update(assets)
    if a.check:
        stale = [p for p, s in targets.items() if not os.path.exists(p) or open(p, encoding="utf-8").read() != s]
        for p in stale: print("STALE " + os.path.relpath(p, REPO))
        print("check: %d/%d 생성물이 모델과 같다" % (len(targets) - len(stale), len(targets)))
        sys.exit(1 if stale else 0)

    for p, s in targets.items():
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as f: f.write(s)
        print("wrote %s (%d bytes)" % (os.path.relpath(p, REPO), len(s.encode("utf-8"))), flush=True)

    if not a.no_verify:
        import subprocess
        sys.stdout.flush()
        rc = subprocess.call([sys.executable, os.path.join(HERE, "verify_card_shapes.py")])
        sys.exit(rc)

if __name__ == "__main__":
    main()
