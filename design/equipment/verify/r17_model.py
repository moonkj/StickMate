# -*- coding: utf-8 -*-
"""R17 — 착용 표면 개정 (design-equipment, 2026-09-05). R16 시트에 대한 사용자 지적 5건 + 리더 정정 1건.

사용자 원문: "모자가 불투명해서 안이 안보여야하는데 보임 그리고 머리와 사이즈도 안맞음 외눈 안경은 오른쪽에 착용 되어야하고
외눈안경 착용시에만 반대쪽 눈 표시 해줘야함" / "두번째 넥타이의 위치가 너무 머리쪽으로 올라가있음 목하단에 위치해야함"
★ 디자인 소유자의 지시가 인계본 파일보다 우선한다 — 인계본 착용 프리뷰가 그렇게 돼 있어도 사용자 말대로 바꾼다.

  [1] 모자 4종은 착용 표면에서 **불투명** — 모자 채움 조각(B/CB/RB)마다 **머리 채움색(잉크)의 불투명 바탕**을 먼저 깔고 그 위에
      인계본 레이어(워시·띠·하이라이트)를 그대로 얹는다. 카드는 인계본 알파 그대로.
  [2] 모자 크기를 머리에 맞춘다 — **균일 배율 u(R/아이콘단위) + 세로 오프셋 dy**. 규칙: 착용선 아래로 내려온 모자 불투명 조각의
      합집합이 그 높이의 머리 현(chord)을 덮고, 착용선 ≤ 종류별 목표(감싸는 모자 +0.30 R · 왕관 띠 +0.45 R), 꼭대기 < 2.551 R(초상화
      액자), 밑 > −1.0 R(턱). 머리는 배율 0.35 의 링 하한까지 포함한 **가장 큰 잉크 원반 1.1842 R** 로 잰다. 균일 배율이 액자를 깨면
      세로 압축 ky 를 붙인 대안을 낸다(이유 포함).
  [3] 외알안경은 **보는 사람 기준 오른쪽**(+x) — 몸 표면에서만 x → 64−x 거울 반전(알·하이라이트·줄·구슬). 카드는 그대로.
  [4] 외알안경 착용 시에만 **반대쪽 눈**(보는 사람 기준 왼쪽) — 잉크 원반 위 흰 원반. 평소엔 눈 없음(BakeEyes=false).
  [5] 리더 정정: 강조 A = **등급색**(시트는 일반 #8A8F98 가정), 선/테 = 브라스. R16 「브라스 단색」은 오독.
  [6] 줄무늬타이 매듭을 **목 하단**(어깨선 −1.318 R)에 — 몸 표면 세로 오프셋. 나머지 NECK 3종은 같은 자로 점검만.

★ 카드 표면(R15 CARD_SET · r16 ICON)은 한 좌표도 바꾸지 않는다. 이 파일은 **몸 표면 변형 표**(HAT_FIT · MIRROR · SHIFT · 조건부 조각)를 낸다.
"""
import math, os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig, handoff
import r16_model as M16
from rig import Shape

# ============================================================================
# 0. 자
# ============================================================================
SHIP = M16.SHIP
HEAD_R_COVER = 1.0 + M16.ONE_PT_R[0.35] / 2.0     # 1.1842 R — 배율 0.35 링 하한(1pt) 포함 잉크 원반 바깥 반경(가장 크다)
HEAD_R_SHIP = M16.HEAD_OUTER_R                      # 1.1432 R — 출하 0.75
TOP_LIMIT = 2.551                                   # 초상화 액자 상한 (EQUIPMENT_SHAPE_SPEC §208 · verify.py HEAD)
CHIN = -1.0                                         # 턱 (verify.py HEAD b > -1.0)
SH = rig.SHOULDER_R                                 # −1.3182 어깨선
NECK_LEN = HEAD_R_SHIP + SH                         # 머리 원반 밑 ~ 어깨선 = 0.175 R (음수 부호 없이 길이)
ICON_U_HEAD = handoff.icon_to_R("clothhat")[2]      # 카드/R16 착용의 HEAD 아이콘 단위 0.04944 R
COVER_MARGIN = 0.06                                 # 불투명 바탕 다각형이 머리 현보다 이만큼 더 넓어야 한다(AA 여유, 획 반폭 0.086 은 그 밖에 또 있다)

PALETTE_R17 = {"ink": M16.BRASS, "accent": M16.RAR_COMMON, "white": M16.WHITE}   # [5] 선 브라스 · 강조 등급색(일반 회색)
HEAD_KINDS = ["clothhat", "furhat", "fedora", "crown"]
NECK_KINDS = ["bowtie", "stripedtie", "scarf", "bellnecklace"]

class Piece:
    __slots__ = ("kind", "name", "src", "call", "pts", "loop", "filled", "mult", "nominal_R", "fill", "line", "dash",
                 "group_alpha", "layer", "role", "note", "base", "conditional", "transform", "sway")
    def __init__(self, kind, name, src, call, pts, loop, filled, mult, nominal_R, fill, line, dash=None, group_alpha=1.0,
                 layer="front", role="", note="", base=False, conditional=None, transform="", sway=None):
        self.kind, self.name, self.src, self.call = kind, name, src, call
        self.pts = [(float(a), float(b)) for a, b in pts]
        self.loop, self.filled, self.mult, self.nominal_R = loop, filled, mult, nominal_R
        self.fill, self.line, self.dash, self.group_alpha, self.layer = fill, line, dash, group_alpha, layer
        self.role, self.note, self.base, self.conditional, self.transform = role, note, base, conditional, transform
        self.sway = sway                      # (SwayStart, SwayCount) — 펄럭임 정점 구간, 없으면 None
    def width_R(self, scale=SHIP, floor_pt=M16.FLOOR_PT):
        return M16.floor_R(self.nominal_R, scale, floor_pt)
    def as_shape(self):
        return Shape(self.name, self.pts, loop=self.loop, filled=self.filled)

def _copy16(p, pts=None, **over):
    d = dict(kind=p.kind, name=p.name, src=p.src, call=p.call, pts=p.pts if pts is None else pts, loop=p.loop, filled=p.filled,
             mult=p.mult, nominal_R=p.nominal_R, fill=p.fill, line=p.line, dash=p.dash, group_alpha=p.group_alpha,
             layer=p.layer, role=p.role, note=p.note)
    d.update(over)
    return Piece(**d)

# ============================================================================
# 1. [2] 모자 맞춤 — 불투명 조각 합집합의 중앙 반폭 프로파일(아이콘 단위) → (u, dy[, ky]) 탐색
# ============================================================================
_RAW = handoff.parse_items()
def _icon_polys(kind):
    return handoff.build(kind, _RAW[kind])

def _scan_intervals(poly, y):
    """다각형(아이콘 단위)과 수평선 y 의 교차 x 목록(짝수-홀수)."""
    xs = []
    n = len(poly)
    for i in range(n):
        (ax, ay), (bx, by) = poly[i], poly[(i + 1) % n]
        if (ay > y) != (by > y):
            xs.append(ax + (y - ay) * (bx - ax) / (by - ay))
    xs.sort()
    return [(xs[i], xs[i + 1]) for i in range(0, len(xs) - 1, 2)]

def central_hw_profile(kind, ys, opaque_calls=("B", "CB", "RB")):
    """각 아이콘 y 에서 불투명 조각 합집합 중 x=32 를 품은 구간의 **작은 쪽 반폭**(보수적). 없으면 0."""
    polys = [pc.pts for pc in _icon_polys(kind) if pc.call in opaque_calls]
    out = np.zeros(len(ys))
    for j, y in enumerate(ys):
        iv = []
        for poly in polys: iv += _scan_intervals(poly, y)
        if not iv: continue
        iv.sort(); merged = [list(iv[0])]
        for a, b in iv[1:]:
            if a <= merged[-1][1] + 1e-9: merged[-1][1] = max(merged[-1][1], b)
            else: merged.append([a, b])
        for a, b in merged:
            if a <= 32.0 <= b:
                out[j] = min(32.0 - a, b - 32.0); break
    return out

def hat_extent(kind):
    pts = [q for pc in _icon_polys(kind) for q in pc.pts]
    return min(q[0] for q in pts), max(q[0] for q in pts), min(q[1] for q in pts), max(q[1] for q in pts)

# 종류별 목표: 착용선 상한(R) · 덮어야 하는 높이 상한(None = 머리 꼭대기까지 / 'dip' = 왕관 봉우리 사이 골까지)
HAT_TARGET = {"clothhat": (0.30, None), "fedora": (0.30, None), "furhat": (0.00, None), "crown": (0.45, "dip")}
CROWN_DIP_ICON_Y = 29.0      # 왕관 B0 'L23.5 29' / 'L40.5 29'

def evaluate(kind, u, dy, ky=1.0, ys=None, chw=None):
    """변환 y_R = dy − y_icon·u·ky, x_R = (x−32)·u. 반환 dict(wear, top, bottom, ok, why)."""
    ys = ys if ys is not None else np.arange(0.0, 64.0, 0.25)
    chw = chw if chw is not None else central_hw_profile(kind, ys)
    y_R = dy - ys * u * ky
    x0, x1, y0, y1 = hat_extent(kind)
    top = dy - y0 * u * ky; bottom = dy - y1 * u * ky
    w_max, cover_to = HAT_TARGET[kind]
    cover_top = HEAD_R_COVER if cover_to is None else min(HEAD_R_COVER, dy - CROWN_DIP_ICON_Y * u * ky)
    head_hw = np.sqrt(np.clip(HEAD_R_COVER ** 2 - y_R ** 2, 0.0, None))
    need = (y_R <= cover_top) & (y_R >= -HEAD_R_COVER)
    ok_line = (chw * u >= head_hw + COVER_MARGIN) | ~need
    # 착용선 = cover_top 에서 아래로 내려오며 연속으로 덮이는 가장 낮은 y_R
    order = np.argsort(-y_R)          # y_R 내림차순
    wear = None
    started = False
    for j in order:
        if y_R[j] > cover_top: continue
        if not ok_line[j]: break
        wear = y_R[j]; started = True
    if not started: wear = None
    # 머리 꼭대기(또는 골)까지 덮였는가: 덮는 구간이 cover_top 바로 아래에서 시작해야 한다
    covered_top = started and (max(y_R[(y_R <= cover_top) & ok_line]) >= cover_top - 0.3)
    why = []
    if wear is None or not covered_top: why.append("덮임 실패")
    elif wear > w_max: why.append("착용선 %.2f > %.2f" % (wear, w_max))
    if top >= TOP_LIMIT: why.append("꼭대기 %.2f ≥ %.3f" % (top, TOP_LIMIT))
    if bottom <= CHIN: why.append("밑 %.2f ≤ %.1f" % (bottom, CHIN))
    return dict(kind=kind, u=u, dy=dy, ky=ky, wear=wear, top=top, bottom=bottom, cover_top=cover_top,
                width=(x1 - x0) * u, ok=not why, why=" · ".join(why))

def fit(kind, ky=1.0, u_range=(0.045, 0.150, 0.001), dy_range=(0.0, 4.5, 0.02)):
    """가장 작은 u(모자 전체 균일 배율) 중 규칙을 전부 만족하는 (u, dy). dy 는 착용선을 목표에 가장 가깝게(아래에서)."""
    ys = np.arange(0.0, 64.0, 0.25); chw = central_hw_profile(kind, ys)
    w_max = HAT_TARGET[kind][0]
    best = None; nearest_fail = None
    for u in np.arange(*u_range):
        cands = []
        for dy in np.arange(*dy_range):
            e = evaluate(kind, u, dy, ky, ys, chw)
            if e["ok"]: cands.append(e)
            elif nearest_fail is None or (e["wear"] is not None and e["top"] < TOP_LIMIT + 0.6): nearest_fail = e
        if cands:
            best = min(cands, key=lambda e: abs(e["wear"] - w_max)); break
    return best, nearest_fail

def fit_with_fallback(kind):
    """균일 배율(ky=1) 먼저, 액자를 깨면 ky 0.95→0.70 로 세로 압축 대안."""
    tried = []
    for ky in (1.0, 0.95, 0.90, 0.85, 0.80, 0.75, 0.70):
        best, nf = fit(kind, ky)
        tried.append((ky, best, nf))
        if best: return best, tried
    return None, tried

# ============================================================================
# ★ R25 확정 맞춤 락 (2026-09-06, design-equipment 확정 → 코디네이터 통보 → coder 이식)
# ============================================================================
# 왜 락인가: 아래 값은 **이 파일의 격자 탐색이 낸 값이 아니다.** design-equipment 가 H-2 규칙의
# 근본 결함(착용선에 **하한이 없었다** — 목표는 «≤ w_max» 하나뿐이라 모자가 얼마든지 내려앉을 수
# 있었고, 그것이 「안경을 너무 가린다」는 사용자 신고의 정체다)을 고치고 6종을 다시 맞춘 결과다.
# 그 새 규칙(하한을 포함한 목표 대역)의 **수치 정본은 design-equipment 문서**이고, 이 파일은
# 그 결론을 좌표로 못박기만 한다 — 여기서 목표를 다시 추정하면 정본이 두 개가 된다.
#
# ★ 그래서 아래 HAT_TARGET(구 목표 0.30/0.00/0.45)은 **손대지 않았다.** 그 표는 이제 이 락에
#   대해 「상한」의 뜻을 잃었고(락은 대부분 그 위에 있다), 남은 쓰임은 fit() 을 직접 부르는
#   옛 탐색뿐이다. 락된 종류는 fit() 을 아예 돌리지 않는다.
#
# 블라인드 수용을 막는 장치: 락을 그대로 믿지 않고 evaluate() 로 **이견 없는 불변식 3개**만
# 다시 잰다 — (가) 머리 꼭대기부터 연속으로 덮는가 (나) 꼭대기 < 액자 2.551 (다) 밑 > 턱 −1.0.
# 하나라도 깨지면 import 가 멈춘다. 착용선 자체는 목표 정본이 여기 없으므로 **기록만** 한다.
HAT_FIT_LOCK = {
    "clothhat": dict(u=0.0730, ky=1.0000, dy=3.6560),   # dy 3.4800 → 3.6560 (u·ky 무변경)
    "fedora":   dict(u=0.0780, ky=0.9650, dy=3.7844),   # ky 1.0000 → 0.9650 · dy 3.7600 → 3.7844
    "furhat":   dict(u=0.0790, ky=0.6760, dy=2.8861),   # 안 B — 몸 변환이 (scale 1.5977, ky 0.6760, offsetY +0.13521)이 되는 dy
    "crown":    dict(u=0.0570, ky=1.0000, dy=3.1200),   # 무변경(R17 탐색값 그대로 — 락에 적어 두어야 「안 바뀌었다」가 눈에 보인다)
}

HAT_FIT = {}
HAT_TRIED = {}
for _k in HEAD_KINDS:
    if _k in HAT_FIT_LOCK:
        _l = HAT_FIT_LOCK[_k]
        _e = evaluate(_k, _l["u"], _l["dy"], _l["ky"])
        if _e["wear"] is None:
            raise SystemExit("R25 락 %s: 앞층이 머리 현을 한 줄도 못 덮는다" % _k)
        if _e["top"] >= TOP_LIMIT:
            raise SystemExit("R25 락 %s: 꼭대기 %.4f ≥ 액자 %.3f" % (_k, _e["top"], TOP_LIMIT))
        if _e["bottom"] <= CHIN:
            raise SystemExit("R25 락 %s: 밑 %.4f ≤ 턱 %.1f" % (_k, _e["bottom"], CHIN))
        HAT_FIT[_k], HAT_TRIED[_k] = _e, [("locked", _e, None)]
        continue
    HAT_FIT[_k], HAT_TRIED[_k] = fit_with_fallback(_k)
    if HAT_FIT[_k] is None:
        raise SystemExit("모자 맞춤 실패 %s: %s" % (_k, HAT_TRIED[_k]))

def hat_to_R(kind):
    f = HAT_FIT[kind]
    return (lambda x: (x - 32.0) * f["u"]), (lambda y: f["dy"] - y * f["u"] * f["ky"])

# ============================================================================
# 2. [3][4] 외알안경 — 거울 반전 + 조건부 반대쪽 눈
# ============================================================================
MONOCLE_MIRROR = True
EYE_X, EYE_Y, EYE_R = -0.46, 0.107, M16.ONE_PT_R[SHIP]      # 반대쪽 눈: 인계본 눈 y(43 → +0.107 R) · x 는 알 테 밖으로 · r = 1pt(0.1719 R)
HANDOFF_EYE_R = 3.4 / 28.0                                    # 인계본 프리뷰 흰 점 0.1214 R
HANDOFF_EYE_X = 10.0 / 28.0                                   # ±0.357 R
DRAWN_EYE_OFF, DRAWN_EYE_R = 0.62, 0.33                      # AccessoryShapeBuilder.DrawnEyeOffsetRatio / DrawnEyeRadiusRatio

# ============================================================================
# 3. [6] 줄무늬타이 — 매듭 윗변을 목 하단(어깨선)으로
# ============================================================================
TIE_KNOT_TOP_TARGET = SH + 0.018       # −1.300 R (어깨선 바로 위, 목 하단)
def _tie_shift():
    knot = [p for p in M16.ICON["stripedtie"] if p.src == "B0"][0]
    top = max(q[1] for q in knot.pts)
    return TIE_KNOT_TOP_TARGET - top    # −0.287 R
TIE_DY = _tie_shift()

# NECK 착용선 규칙: 착용 조각 윗변이 머리 원반 밑(−1.143) 아래이고 어깨선 ±0.20 R 안
NECK_ANCHOR = {"bowtie": "RB2", "stripedtie": "B0", "scarf": "B0", "bellnecklace": "S0"}
NECK_BAND = 0.20

# ============================================================================
# 4. 몸 표면 조각 조립
# ============================================================================
def _head_pieces(kind):
    fx, fy = hat_to_R(kind)
    out = []
    icon = _icon_polys(kind)
    srcs = ["%s%d" % (pc.call, i) for i, pc in enumerate(icon)]
    base16 = {p.src: p for p in M16.ICON[kind]}
    tr = "HAT_FIT u=%.4f ky=%.2f dy=%.4f" % (HAT_FIT[kind]["u"], HAT_FIT[kind]["ky"], HAT_FIT[kind]["dy"])
    # (1) 불투명 바탕 — 채움 조각(B/CB/RB) 순서대로 먼저
    for pc, src in zip(icon, srcs):
        if pc.call in ("B", "CB", "RB"):
            pts = [(fx(x), fy(y)) for x, y in pc.pts]
            out.append(Piece(kind, "%s.%s.base" % (kind, src), src + "b", "BASE", pts, True, True, 1.0, 0.0,
                             ("headfill", 1.0), None, role="불투명 바탕", base=True, transform=tr,
                             note="[1] 머리 채움색 불투명 — 머리가 모자 안으로 비치지 않는다"))
    # (2) 인계본 레이어 그대로(변환만)
    for pc, src in zip(icon, srcs):
        p16 = base16[src]
        pts = [(fx(x), fy(y)) for x, y in pc.pts]
        # 획 명목은 아이콘 단위 배수 그대로(슬롯 획 × 배수) — 크기를 키워도 인계본 획 비율은 슬롯 기준으로 둔다
        out.append(_copy16(p16, pts=pts, transform=tr))
    return out

# ★ R17b(사용자 지적 6): 알·하이라이트만 거울 반전(x→64−x)하고, 사슬·구슬은 반전하지 않는다 — 알 중심 이동량(Δx = 64 − 2·25 = +14 u)만큼
#   평행이동해 인계본의 「알에 대한 상대 오프셋」을 그대로 유지한다. 전체 반전은 사슬이 얼굴을 가로질러 왼쪽 아래로 내려갔다(사용자: "너무 이상한데").
MONOCLE_LENS_CX_ICON = 25.0                                   # 인계본 알 중심 x(아이콘 단위)
# ★ R17c(사용자 지적 7 "알을 약간 더 오른쪽으로"): 알 중심 x = **반대쪽 눈의 거울 위치** +0.46 R(단일 출처 EYE_X). 두 눈 자리가 대칭이 된다.
#   R17b 의 단순 거울(+0.237 R)은 알 왼끝이 −0.187 R 로 중심선을 넘어 「가운데로 몰려」 보였다.
MONOCLE_LENS_CX_R = -EYE_X                                    # +0.46 R
_MSX = handoff.icon_to_R("monocle")[2]
MONOCLE_TARGET_CX_ICON = 32.0 + MONOCLE_LENS_CX_R / _MSX      # 45.57 u
MONOCLE_DX_ICON = MONOCLE_TARGET_CX_ICON - MONOCLE_LENS_CX_ICON   # 사슬·구슬 평행이동 +20.57 u (알 상대 오프셋 유지)
MONOCLE_MIRROR_EXTRA = MONOCLE_TARGET_CX_ICON - (64.0 - MONOCLE_LENS_CX_ICON)   # 거울(39 u) 뒤 추가 이동 +6.57 u
MONOCLE_MIRROR_SRC = {"CB0", "H1"}                            # 반전하는 조각
MONOCLE_SHIFT_SRC = {"S2", "CB3"}                             # 평행이동하는 조각(사슬·구슬)
# ★ R17c 구슬 y 미세 조정(R17b 규칙 「구슬이 원반 밖으로 떨어지면 테두리와 겹치지 않게」): 알이 +0.46 R 로 가면서 구슬 중심이 원반 밖
#   (1.324 R)으로 나갔는데 구슬 잉크 안쪽 가장자리 1.130 R 이 출하 원반 1.143 R 과 0.013 R(0.08 pt) 겹친다. 깨끗이 밖(≥ 1.338 R)이 되는
#   최소 y 이동 −0.025 R 을 −0.03 R 로 반올림. 구슬만 옮긴다 — 사슬 끝은 구슬 테 띠(r 0.1085 ± 0.086) 안에 남아 이어져 보인다.
BEAD_DY_R = -0.03

def _monocle_pieces():
    kind = "monocle"; fx, fy, sx = handoff.icon_to_R(kind)
    out = []
    icon = _icon_polys(kind)
    for i, (pc, p16) in enumerate(zip(icon, M16.ICON[kind])):
        src = "%s%d" % (pc.call, i)
        if MONOCLE_MIRROR and src in MONOCLE_MIRROR_SRC:
            pts = [(fx(64.0 - x + MONOCLE_MIRROR_EXTRA), fy(y)) for x, y in pc.pts]
            tr = "MIRROR x→64−x, +%.2fu (알 중심 → +%.2f R = 반대쪽 눈 거울 위치)" % (MONOCLE_MIRROR_EXTRA, MONOCLE_LENS_CX_R)
        elif MONOCLE_MIRROR and src in MONOCLE_SHIFT_SRC:
            bdy = BEAD_DY_R if src == "CB3" else 0.0
            pts = [(fx(x + MONOCLE_DX_ICON), fy(y) + bdy) for x, y in pc.pts]
            tr = "SHIFT x+%.2fu (알 상대 오프셋 유지, 바깥쪽으로)" % MONOCLE_DX_ICON + (" · 구슬 y %+.2f R (원반 가장자리와 안 겹치게)" % bdy if bdy else "")
        else:
            pts = [(fx(x), fy(y)) for x, y in pc.pts]; tr = ""
        out.append(_copy16(p16, pts=pts, transform=tr))
    eye = rig.poly(EYE_X, EYE_Y, EYE_R, 12)
    out.insert(0, Piece(kind, "monocle.ExposedEye", "EYE", "EYE", eye, True, True, 1.0, 0.0, ("white", 1.0), None,
                        role="반대쪽 눈(조건부)", conditional="monocle worn", transform="",
                        note="[4] 외알안경 착용 시에만. 잉크 원반 위 흰 원반 r=1pt(0.1719R) @(%.2f,%.3f)R — WornColor 우회" % (EYE_X, EYE_Y)))
    return out

def _neck_pieces(kind):
    dy = TIE_DY if kind == "stripedtie" else 0.0
    out = []
    for p16 in M16.ICON[kind]:
        pts = [(x, y + dy) for x, y in p16.pts]
        out.append(_copy16(p16, pts=pts, transform=("SHIFT dy=%.4f" % dy) if dy else ""))
    return out

# ============================================================================
# 3b. [R17d] 망토 길이 2단 — 사용자 지적 8 "긴망토 길이를 짧은 망토로, 긴망토는 발목 정도로. 현재 짧은망토가 너무 짧다"
# ============================================================================
# 규칙 B-1: 짧은망토 밑단 = 현행(인계본 무대) 긴망토 밑단 높이 · 긴망토 밑단 = 발목(다리 끝 잉크 원의 윗변).
# 규칙 B-2(폭): 뒤판 밑단 폭 = 어깨 부착 폭 + 플레어율 × 길이 — 플레어율은 인계본 긴망토에서 잰다(길이만 늘리면 좁고 뾰족해진다).
ANKLE_Y = -rig.HEAD_CENTER / rig.BASELINE_HEAD_R            # −9.339 R — 다리 끝점(발목) = 루트 로컬 y 0 = 발바닥선 (SceneBootstrapper footLift)
LEG_UP, LEG_LO = 0.50 / 0.22, 0.45 / 0.22                    # 2.273 / 2.045 R
W_LEG_R = 0.57
HEM_LONG_TARGET = ANKLE_Y + W_LEG_R / 2.0                    # −9.054 R — 발목 잉크 원 윗변 (verify.py BACK 하한 −9.3395 위)
_LONG_D = "M86 78 Q68 138 70 184 Q100 193 130 184 Q132 138 114 78 Z"     # 인계본 긴망토 뒤판(무대 200×240)
_SHORT_D = "M87 78 Q74 106 74 132 Q100 140 126 132 Q126 106 113 78 Z"

def _stage_pts(d):
    pts, _ = handoff.flatten_path(d)
    return [handoff.stage_to_R(x, y) for x, y in pts]

_LONG_OLD = _stage_pts(_LONG_D); _SHORT_OLD = _stage_pts(_SHORT_D)
CAPE_TOP_Y = max(q[1] for q in _LONG_OLD)                    # −1.143 R (무대 y=78)
HEM_LONG_OLD = min(q[1] for q in _LONG_OLD)                  # −5.250 R (무대 y=193)
HEM_SHORT_OLD = min(q[1] for q in _SHORT_OLD)                # −3.357 R (무대 y=140)
TOP_W = (114 - 86) / 28.0                                    # 어깨 부착 폭 1.000 R
def _hem_width(pts):
    ylo = min(q[1] for q in pts); band = [q for q in pts if q[1] <= ylo + 0.35]
    return max(q[0] for q in band) - min(q[0] for q in band)
HEM_W_LONG_OLD = _hem_width(_LONG_OLD)                       # 2.143 R (무대 70..130)
HEM_W_SHORT_OLD = _hem_width(_SHORT_OLD)                     # 1.857 R
FLARE = (HEM_W_LONG_OLD - TOP_W) / (CAPE_TOP_Y - HEM_LONG_OLD)   # 0.278 R/R — 인계본 긴망토의 플레어율
HEM_SHORT_NEW = HEM_LONG_OLD                                 # −5.250 R (규칙 B-1)
HEM_LONG_NEW = HEM_LONG_TARGET                               # −9.054 R
LEN_LONG_NEW = CAPE_TOP_Y - HEM_LONG_NEW                     # 7.911 R
HEM_W_LONG_NEW = TOP_W + FLARE * LEN_LONG_NEW                # 3.199 R (규칙 B-2)
KY_LONG = LEN_LONG_NEW / (CAPE_TOP_Y - HEM_LONG_OLD)         # 1.926
KW_LONG = HEM_W_LONG_NEW / HEM_W_LONG_OLD                    # 1.493 (밑단에서, 위로 갈수록 1로 선형 수렴)

def _long_new_pts():
    """인계본 긴망토 뒤판을 위(어깨 부착) 고정 · 세로 ×KY · 폭은 깊이에 비례해 ×(1→KW) 로 다시 그린다 — 옆선 기울기(플레어)가 인계본과 같다."""
    out = []
    for x, y in _LONG_OLD:
        t = (CAPE_TOP_Y - y) / (CAPE_TOP_Y - HEM_LONG_OLD)  # 0(위)~1(밑단)
        kx = 1.0 + (KW_LONG - 1.0) * t
        out.append((x * kx, CAPE_TOP_Y - (CAPE_TOP_Y - y) * KY_LONG))
    return out

CAPE_BACK_PTS = {"shortcape": list(_LONG_OLD), "longcape": _long_new_pts()}
# 펄럭임(HemSway) 정점 구간 — 무대 경로 M(1) + Q(16)×3 = 49점: 밑단 곡선 = 2번째 Q 의 점 = 인덱스 16..32 (17점, 왼 밑모서리 → 오른 밑모서리)
CAPE_SWAY = (16, 17)
STAGE_STROKE_2 = M16.stage_stroke_R(2.0)

def _cape_pieces17(kind):
    ps = M16._cape_pieces(kind)              # 칼라·걸쇠 그대로, 뒤판만 교체
    out = []
    for p in ps:
        if p.src == "STB":
            q = _copy16(p, pts=CAPE_BACK_PTS[kind],
                        transform=("SHORT := 인계본 긴망토 뒤판 그대로(밑단 %.3f R)" % HEM_SHORT_NEW) if kind == "shortcape"
                        else ("LONG: 위 고정 · 세로 ×%.3f · 밑단 폭 ×%.3f(플레어 %.3f R/R 유지) → 밑단 %.3f R 폭 %.3f R" % (KY_LONG, KW_LONG, FLARE, HEM_LONG_NEW, HEM_W_LONG_NEW)))
            q.sway = CAPE_SWAY
            out.append(q)
        else:
            out.append(_copy16(p))
    return out

# ---- 자세 검산 — 웅크리기(LandingCrouch)에서 몸(컨테이너)이 내려가는 양 = 서 있는 다리 낙차 − 그 자세 다리 낙차 (StickmanPoseAnimator.ComputeFootGroundingOffset)
IDLE_SPREAD, IDLE_KNEE = 12.0, 4.0
CROUCH = {"front": (82.0, 126.0), "rear": (-40.0, 55.0)}    # StickConfig landingCrouchFront/RearHip/KneeDegrees
CROUCH_MIN_DEPTH = 0.45                                       # landingCrouchMinDepth01
def leg_drop(hip_deg, knee_deg):
    return LEG_UP * math.cos(math.radians(hip_deg)) + LEG_LO * math.cos(math.radians(hip_deg - knee_deg))
STAND_DROP = leg_drop(IDLE_SPREAD, IDLE_KNEE)                 # 4.248 R
def crouch_offset(amount):
    """amount 0~1 → 몸 오프셋(R, 음수 = 내려감). 각도는 중립→최심 선형 보간(LerpAngle), 낙차는 두 다리 중 큰 쪽."""
    best = -1e9
    for hip0, (hipD, kneeD) in ((IDLE_SPREAD, CROUCH["front"]), (-IDLE_SPREAD, CROUCH["rear"])):
        hip = hip0 + (hipD - hip0) * amount; knee = IDLE_KNEE + (kneeD - IDLE_KNEE) * amount
        best = max(best, leg_drop(hip, knee))
    return best - STAND_DROP
SIT_DOWN_DROP_R = 0.12 * rig.BASELINE_TOTAL_H / rig.BASELINE_HEAD_R   # LandingSitDownBodyDropHeights 0.12 H = 1.241 R — 「앉는 그림」 문턱

def cape_pose_table():
    rows = []
    for label, amt in (("서 있음", 0.0), ("웅크리기 최소(깊이 0.45)", CROUCH_MIN_DEPTH), ("앉는 그림 문턱(하강 0.12 H)", None), ("웅크리기 최대(깊이 1.0)", 1.0)):
        off = crouch_offset(amt) if amt is not None else -SIT_DOWN_DROP_R
        rows.append((label, off, HEM_SHORT_NEW + off, HEM_LONG_NEW + off, HEM_LONG_OLD + off))
    return rows

def cape_report(w):
    w("== 7. [R17d] 망토 길이 2단 — 규칙 B-1/B-2 ==")
    w("   자: 발목(다리 끝점 = 루트 로컬 y 0 = 발바닥선) y = %.4f R · 다리 획 반폭 %.3f → 긴망토 밑단 목표 %.4f R (verify BACK 하한 −9.3395 위)" % (ANKLE_Y, W_LEG_R / 2, HEM_LONG_TARGET))
    w("   현행(인계본 무대): 짧은 밑단 %.3f R(폭 %.3f) · 긴 밑단 %.3f R(폭 %.3f) · 위 부착 폭 %.3f R @ %.3f R · 프로덕션 CapeOutline 밑단: 짧은 %.3f · 긴 %.3f R" % (
        HEM_SHORT_OLD, HEM_W_SHORT_OLD, HEM_LONG_OLD, HEM_W_LONG_OLD, TOP_W, CAPE_TOP_Y,
        (rig.SHOULDER_R + 0.10) - rig.TORSO_R * 1.35, (rig.SHOULDER_R + 0.10) - rig.TORSO_R * 1.85))
    w("   플레어율(인계본 긴망토) = (%.3f − %.3f) / %.3f = %.4f R/R" % (HEM_W_LONG_OLD, TOP_W, CAPE_TOP_Y - HEM_LONG_OLD, FLARE))
    w("   새 짧은망토 = 현행 긴망토 뒤판 그대로: 밑단 %.3f R · 폭 %.3f R · 길이 %.3f R" % (HEM_SHORT_NEW, HEM_W_LONG_OLD, CAPE_TOP_Y - HEM_SHORT_NEW))
    w("   새 긴망토: 밑단 %.3f R · 길이 %.3f R(×%.3f) · 밑단 폭 %.3f R(×%.3f) · 옆선 기울기 = 인계본과 같음(플레어 유지)" % (HEM_LONG_NEW, LEN_LONG_NEW, KY_LONG, HEM_W_LONG_NEW, KW_LONG))
    lp = CAPE_BACK_PTS["longcape"]; x0, y0, x1, y1 = rig.bounds(lp)
    w("   새 긴망토 뒤판 봉투 x [%+.3f, %+.3f] y [%+.3f, %+.3f] · 점 %d · 밑단 최저 %.4f (verify 하한 −9.3395 %s)" % (x0, x1, y0, y1, len(lp), y0, "통과" if y0 > -9.3395 else "★ 위반"))
    w("   다리: 벌림 ±%.0f° 무릎 %.0f° → 발목 x ±%.3f R · 밑단 반폭 %.3f R → 다리 밖으로 각 %.3f R 보인다(뒤판은 SortBack −1 이라 다리 뒤)" % (
        IDLE_SPREAD, IDLE_KNEE, LEG_UP * math.sin(math.radians(IDLE_SPREAD)) + LEG_LO * math.sin(math.radians(IDLE_SPREAD - IDLE_KNEE)),
        HEM_W_LONG_NEW / 2, HEM_W_LONG_NEW / 2 - (LEG_UP * math.sin(math.radians(IDLE_SPREAD)) + LEG_LO * math.sin(math.radians(IDLE_SPREAD - IDLE_KNEE)))))
    w("   배율 0.35: 1pt = %.3f R → 뒤판 윤곽 획 %.3f R(무대 명목 %.4f 의 ×%.1f) · 다리 획 %.3f R · 다리 밖 보이는 폭 %.3f R = %.1f pt" % (
        M16.ONE_PT_R[0.35], M16.floor_R(STAGE_STROKE_2, 0.35), STAGE_STROKE_2, M16.floor_R(STAGE_STROKE_2, 0.35) / STAGE_STROKE_2,
        max(W_LEG_R, M16.ONE_PT_R[0.35] * 2), HEM_W_LONG_NEW / 2 - 0.757, (HEM_W_LONG_NEW / 2 - 0.757) * M16.head_r_pt(0.35)))
    w("   펄럭임(HemSway): 진폭 = R × 0.16 × walk01 (CharacterAccessoryRenderer.SwayAmplitudeRatio) — 길이에 비례하지 않는다. 정점 구간 인덱스 %d..%d(%d점, 밑단 곡선) — 프로덕션 CapeOutline 은 5점이라 위상 걸음(0.9 rad/점)이 17점에 걸리면 물결이 촘촘해진다 → design-motion 영향 있음" % (CAPE_SWAY[0], CAPE_SWAY[0] + CAPE_SWAY[1] - 1, CAPE_SWAY[1]))
    w("   자세 검산 — 몸(컨테이너) 오프셋 = 그 자세 다리 낙차 − 서 있는 낙차 %.3f R (ComputeFootGroundingOffset). 발목선 %.3f R 아래로 내려간 밑단 = 바닥 관통" % (STAND_DROP, ANKLE_Y))
    w("   %-28s 몸오프셋   짧은(새) 밑단   긴(새) 밑단   [현행 긴 밑단]" % "자세")
    for label, off, hs, hl, ho in cape_pose_table():
        f = lambda y: "%+.3f%s" % (y, " ★관통 %.2f R=%.1fpt" % (ANKLE_Y - y, (ANKLE_Y - y) * M16.head_r_pt(SHIP)) if y < ANKLE_Y else "")
        w("   %-28s %+.3f     %-22s %-22s %s" % (label, off, f(hs), f(hl), f(ho)))
    w("   랙돌: 액세서리 전부 숨김(ResolveWantVisible → Ragdoll false) — 검산 대상 아님. GETUP: TickInkFloorClearance 가 액세서리 잉크 포함 최저점만큼 루트를 들어 올린다(GETUP 한정) — 긴 뒤판이 리프트를 키워 groundSnapMaxDistance 0.6 유닛(=%.2f R @0.75)에 닿는지 실기 미확인." % (0.6 / (0.22 * 0.75)))
    w("   앉기: 별도 상태 없음 — 「앉는 그림」은 LandingCrouch 의 몸 하강 ≥ 0.12 H(%.3f R) 구간(StickConfig.LandingSitDownBodyDropHeights). SitAndYawn 은 기지개(몸이 rise 로 올라간다)라 관통 없음." % SIT_DOWN_DROP_R)
    w("   처방(코더): 렌더러 HemSway 정점 루프(CharacterAccessoryRenderer :1104~1121)가 밑단 정점을 매 프레임 이미 옮긴다 → 같은 루프에 「p.y = max(p.y, 발목선 − 컨테이너 몸 오프셋)」 한 줄 = 밑단 바닥 클램프. 대안 「TickInkFloorClearance 에 LandingCrouch 추가」는 웅크린 몸을 최대 %.2f R 띄우므로 기각." % (-crouch_offset(1.0)))

def _body_pieces(kind):
    if kind in HEAD_KINDS: return _head_pieces(kind)
    if kind == "monocle": return _monocle_pieces()
    if kind in NECK_KINDS: return _neck_pieces(kind)
    if kind in CAPES: return _cape_pieces17(kind)
    return [_copy16(p) for p in M16.WORN[kind]]

KINDS = ["monocle", "clothhat", "furhat", "fedora", "crown", "stripedtie",
         "sunglasses", "roundglasses", "goggles", "bowtie", "scarf", "bellnecklace",
         "shortcape", "longcape", "wings", "backpack"]
PRIORITY = KINDS[:6]
KO, SLOT, ISLOT, CAPES = M16.KO, M16.SLOT, M16.ISLOT, M16.CAPES
WORN = {k: _body_pieces(k) for k in KINDS}

# ============================================================================
# 5. 검산
# ============================================================================
def neck_report():
    rows = []
    for k in NECK_KINDS:
        a = [p for p in WORN[k] if p.src == NECK_ANCHOR[k]][0]
        top = max(q[1] for q in a.pts)
        ok = (top <= -HEAD_R_SHIP - 0.01) and (abs(top - SH) <= NECK_BAND)
        rows.append((k, NECK_ANCHOR[k], top, top - SH, top + HEAD_R_SHIP, ok))
    return rows

def survival_rows():
    rows = []
    for k in KINDS:
        for p in WORN[k]:
            if p.base: continue
            r = {"kind": k, "p": p}
            for s, key in ((0.75, "ok75"), (1.0, "ok100"), (0.60, "ok60"), (0.35, "ok35")):
                ok, why = M16.survival(p, s) if p.call != "EYE" else (True, "OK")
                r[key] = ok; r["why" + key[2:]] = why
            rows.append(r)
    return rows

def report(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    w("== 0. 자 ==")
    w("   머리 잉크 원반 바깥 반경: 출하 0.75 → %.4f R · 배율 0.35(링 1pt 하한) → %.4f R  ← 모자 덮임 판정은 큰 쪽" % (HEAD_R_SHIP, HEAD_R_COVER))
    w("   어깨선 SH = %.4f R · 목 길이(원반 밑~어깨선) = %.4f R = %.2f pt @0.75 · 액자 상한 %.3f R · 턱 %.1f R" % (SH, NECK_LEN, NECK_LEN * M16.head_r_pt(SHIP), TOP_LIMIT, CHIN))
    w("   카드/R16 HEAD 아이콘 단위 = %.5f R (박스 70px / 64 / 22.12px)" % ICON_U_HEAD)
    w()
    w("== 1. [2] 모자 맞춤 — 균일 배율 u · 세로 오프셋 dy (· 세로 압축 ky) ==")
    w("   규칙: 착용선(합집합이 머리 현을 연속으로 덮기 시작하는 가장 낮은 y) ≤ 목표 · 꼭대기 < %.3f · 밑 > %.1f · 덮임 여유 %.2f R" % (TOP_LIMIT, CHIN, COVER_MARGIN))
    w("   %-9s 목표착용선  u(R/u)   ×카드   ky    dy      착용선   꼭대기   밑      폭(R)   덮임상한" % "kind")
    for k in HEAD_KINDS:
        f = HAT_FIT[k]
        w("   %-9s %+.2f       %.4f  ×%.2f  %.2f  %+.3f  %+.3f  %+.3f  %+.3f  %.2f   %+.2f" % (
            k, HAT_TARGET[k][0], f["u"], f["u"] / ICON_U_HEAD, f["ky"], f["dy"], f["wear"], f["top"], f["bottom"], f["width"], f["cover_top"]))
        for ky, best, nf in HAT_TRIED[k]:
            if best is None and nf is not None:
                w("      ky=%.2f 불가 — 가장 가까운 후보 u=%.3f dy=%.2f: %s" % (ky, nf["u"], nf["dy"], nf["why"]))
    # 카드 배율 그대로(R16)였을 때의 착용선 — 사용자가 본 「얹힌」 상태의 숫자
    w("   (R16 그대로 = u %.4f, 인계본 박스 top 6px 배치일 때)" % ICON_U_HEAD)
    for k in HEAD_KINDS:
        fx, fy, sx = handoff.icon_to_R(k)
        e = evaluate(k, sx, fy(0.0))
        w("      %-9s 착용선 %s · 꼭대기 %+.2f · %s" % (k, ("%+.2f" % e["wear"]) if e["wear"] is not None else "없음(덮임 실패)", e["top"], e["why"] or "규칙 통과"))
    w()
    w("== 2. [3][4] 외알안경 ==")
    pod = [p for p in WORN["monocle"] if p.src == "CB0"][0]; x0, y0, x1, y1 = rig.bounds(pod.pts)
    cx, cy, r = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2
    w("   알 중심 (%+.3f, %+.3f) R · 반경 %.3f R (R16: (%+.3f, %+.3f)) — 보는 사람 기준 %s" % (cx, cy, r, -cx, cy, "오른쪽(+x)" if cx > 0 else "왼쪽"))
    rim = pod.width_R(SHIP) / 2.0
    w("   알 테 바깥 왼끝 x = %+.3f R (테 ×1.5 = %.4f R 반폭 포함)" % (cx - r - rim, rim))
    w("   반대쪽 눈: 중심 (%+.2f, %+.3f) R · r %.4f R = %.2f pt(지름 %.2f pt @0.75 = %.1f px 1× / %.1f px 2×)" % (
        EYE_X, EYE_Y, EYE_R, EYE_R * M16.head_r_pt(SHIP), 2 * EYE_R * M16.head_r_pt(SHIP), 2 * EYE_R * M16.head_r_pt(SHIP), 4 * EYE_R * M16.head_r_pt(SHIP)))
    w("      눈 오른끝 x = %+.3f R ↔ 알 테 왼끝 %+.3f R → 간격 %+.3f R (%.2f pt)" % (EYE_X + EYE_R, cx - r - rim, (cx - r - rim) - (EYE_X + EYE_R), ((cx - r - rim) - (EYE_X + EYE_R)) * M16.head_r_pt(SHIP)))
    w("      인계본 프리뷰 눈: 흰 점 r %.4f R @ ±%.3f R, y +0.107 → 우리 눈 r 은 그 %.2f 배 (1× 에서 인계본 크기는 지름 %.2f pt = 1px 점)" % (
        HANDOFF_EYE_R, HANDOFF_EYE_X, EYE_R / HANDOFF_EYE_R, 2 * HANDOFF_EYE_R * M16.head_r_pt(SHIP)))
    w("      프로덕션 DrawnEye: 오프셋 %.2f R · r %.2f R (12각 채움 원반) → 원시도형 재사용 가능, 상수만 오프셋 %.2f→%.2f(부호 −) · r %.2f→%.4f (%.0f %%)" % (
        DRAWN_EYE_OFF, DRAWN_EYE_R, DRAWN_EYE_OFF, -EYE_X, DRAWN_EYE_R, EYE_R, 100 * (EYE_R / DRAWN_EYE_R - 1)))
    w("      규칙 1-A(지름 %.3f R ≥ 1.5w %.3f) %s · 1-C(r %.3f ≥ w %.3f) %s" % (2 * EYE_R, 1.5 * M16.ONE_PT_R[SHIP], "통과" if 2 * EYE_R >= 1.5 * M16.ONE_PT_R[SHIP] else "미달",
                                                                       EYE_R, M16.ONE_PT_R[SHIP], "통과" if EYE_R >= M16.ONE_PT_R[SHIP] - 1e-9 else "미달"))
    w("      머리 현 반폭 @y=%.3f: %.3f R → 눈 왼끝 %+.3f R 은 안쪽" % (EYE_Y, math.sqrt(HEAD_R_SHIP ** 2 - EYE_Y ** 2), EYE_X - EYE_R))
    # R17b — 사슬·구슬 검산
    fx, fy, sx = handoff.icon_to_R("monocle")
    chain = [p for p in WORN["monocle"] if p.src == "S2"][0]; bead = [p for p in WORN["monocle"] if p.src == "CB3"][0]
    bx0, by0, bx1, by1 = rig.bounds(bead.pts); bcx, bcy, br = (bx0 + bx1) / 2, (by0 + by1) / 2, (bx1 - bx0) / 2
    c0 = chain.pts[0]; c1 = chain.pts[-1]
    d_c0 = math.hypot(c0[0] - cx, c0[1] - cy)
    w("   [R17b] 사슬·구슬 — 알·하이라이트만 반전, 사슬·구슬은 Δx +%.2f u 평행이동(알 상대 오프셋 유지)" % MONOCLE_DX_ICON)
    ic = lambda p: (32.0 + p[0] / sx, (handoff.HEAD_CY_PX - p[1] * handoff.HEAD_R_PX - 38.0) / 0.75)
    w("      아이콘(u): 알 중심 (%.2f, %.2f) r 12.5 · 사슬 (%.2f, %.2f)→(%.2f, %.2f) · 구슬 (%.2f, %.2f) r 3.2" % (
        ic((cx, cy)) + ic(c0) + ic(c1) + ic((bcx, bcy))))
    w("      사슬 시작 (%+.4f, %+.4f) R — 알 중심에서 %.4f R (알 반경 %.4f, 테 반폭 %.4f → 테 띠 %.3f~%.3f) → %s" % (
        c0[0], c0[1], d_c0, r, rim, r - rim, r + rim, "테 띠 안에서 만난다" if r - rim <= d_c0 <= r + rim else "★ 테와 안 만난다"))
    w("      사슬 끝 (%+.4f, %+.4f) R · 구슬 중심 (%+.4f, %+.4f) R r %.4f R(+테 반폭 %.4f)%s" % (
        c1[0], c1[1], bcx, bcy, br, bead.width_R(SHIP) / 2, " · 구슬 y %+.2f R 조정 적용" % BEAD_DY_R if BEAD_DY_R else ""))
    dist = math.hypot(bcx, bcy); rr = br + bead.width_R(SHIP) / 2
    d_end = math.hypot(c1[0] - bcx, c1[1] - bcy)
    w("      사슬 끝 ↔ 구슬 중심 %.4f R (구슬 테 띠 %.3f~%.3f) → %s" % (d_end, br - bead.width_R(SHIP) / 2, br + bead.width_R(SHIP) / 2,
                                                            "이어져 보인다" if d_end <= br + bead.width_R(SHIP) / 2 else "★ 떨어진다"))
    w("      구슬 중심 ↔ 머리 중심 %.4f R : 출하 원반 %.4f R(%+.4f, %s) · 배율 0.35 원반 %.4f R(%+.4f) · 구슬 잉크 반경 방향 [%.3f, %.3f]" % (
        dist, HEAD_R_SHIP, dist - HEAD_R_SHIP, "가장자리와 겹침 %.3f R" % (rr - abs(dist - HEAD_R_SHIP)) if abs(dist - HEAD_R_SHIP) < rr else ("깨끗이 바깥(+%.3f R)" % (dist - rr - HEAD_R_SHIP) if dist > HEAD_R_SHIP else "안쪽"),
        HEAD_R_COVER, dist - HEAD_R_COVER, dist - rr, dist + rr))
    w("      깨끗이 안쪽으로 넣으려면 중심 거리 ≤ %.3f(%+.3f R) · 깨끗이 바깥으로 빼려면 ≥ %.3f(%+.3f R)"
      % (HEAD_R_SHIP - rr, (HEAD_R_SHIP - rr) - dist, HEAD_R_SHIP + rr, (HEAD_R_SHIP + rr) - dist))
    # R17c — 알 중심 +0.46 R 검산 4건
    import r13_bodyocclusion as B
    w("   [R17c] 알 중심 x = 반대쪽 눈 거울 위치 %+.2f R (아이콘 u %.2f · 거울 39 u 뒤 +%.2f u · 사슬·구슬 +%.2f u)" % (
        MONOCLE_LENS_CX_R, MONOCLE_TARGET_CX_ICON, MONOCLE_MIRROR_EXTRA, MONOCLE_DX_ICON))
    right = cx + r; right_rim = cx + r + rim; left = cx - r; left_rim = cx - r - rim
    for label_, RR in (("반경 1.0", 1.0), ("출하 원반 %.3f" % HEAD_R_SHIP, HEAD_R_SHIP), ("0.35 원반 %.3f" % HEAD_R_COVER, HEAD_R_COVER)):
        chord = math.sqrt(max(0.0, RR * RR - cy * cy))
        w("      (1) 알 오른끝 %+.3f(테 포함 %+.3f) vs 머리 현 반폭 @y=%+.3f [%s] %.3f → %s" % (
            right, right_rim, cy, label_, chord, "안" if right_rim <= chord else ("원 경로는 안, 테가 %.3f R 밖" % (right_rim - chord) if right <= chord else "★ 밖")))
    w("      (2) 알 왼끝 %+.3f R (테 포함 %+.3f) — 중심선 %s" % (left, left_rim, "안 넘음" if left >= 0 else "★ 넘음") + (" (테 획만 %.3f R 넘는다)" % (-left_rim) if left_rim < 0 and left >= 0 else ""))
    # (3) 몸 간섭 — 어깨선 · 팔 캡슐 · 몸통
    bead_bottom = bcy - rr
    def seg_d(p, a, b):
        ax, ay = a; bx, by = b; px, py = p; dx, dy = bx - ax, by - ay; L = dx * dx + dy * dy
        t = 0.0 if L == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L))
        return math.hypot(px - (ax + t * dx), py - (ay + t * dy))
    arm_gap = min(seg_d((bcx, bcy), a, b) for a, b in B.ARMS) - rr - B.W_ARM / 2.0
    torso_gap = seg_d((bcx, bcy), (0.0, -1.0), (0.0, rig.HIP_R)) - rr - B.W_TORSO / 2.0
    d_sh = math.hypot(bcx - 0.0, bcy - SH)
    w("      (3) 구슬 중심 (%+.3f, %+.3f) · 머리 중심에서 %.3f R (%s 출하 원반, 잉크 반경 방향 %.3f~%.3f · 0.35 원반 %.3f 과 %s)" % (
        bcx, bcy, dist, "밖" if dist > HEAD_R_SHIP else "안", dist - rr, dist + rr, HEAD_R_COVER,
        "겹침 %.3f R" % (HEAD_R_COVER - (dist - rr)) if dist - rr < HEAD_R_COVER else "분리"))
    w("          구슬 밑 y %+.3f vs 어깨선 %+.3f → 위 %.3f R · 팔 시작점(0,%.3f)까지 %.3f R · 팔 캡슐 간격 %.3f R · 몸통 간격 %.3f R" % (
        bead_bottom, SH, bead_bottom - SH, SH, d_sh, arm_gap, torso_gap))
    bead_icon_cx = 32.0 + bcx / sx; bead_icon_r_edge = bead_icon_cx + 3.2 + 3.4 / 2.0
    w("      (4) 슬롯 64: 알 중심 u %.2f · 알 오른끝(테 포함) u %.2f · 사슬 끝 u %.2f · 구슬 중심 u %.2f · 구슬 오른끝(획 포함) u %.2f → %s" % (
        cx / sx + 32, (cx + r) / sx + 32 + 2.55, c1[0] / sx + 32, bead_icon_cx, bead_icon_r_edge,
        "슬롯 안" if bead_icon_r_edge <= 64 else "★ 슬롯을 %.1f u 넘는다 — 몸 표면은 월드 공간 도형이라 슬롯 클리핑이 없다(인계본 SVG 도 overflow:visible). 카드는 불변이라 무관" % (bead_icon_r_edge - 64)))
    w()
    w("== 3. [6] NECK 착용선 — 착용 조각 윗변 (규칙: 머리 원반 밑 −%.3f 아래 · 어깨선 %.3f ±%.2f) ==" % (HEAD_R_SHIP, SH, NECK_BAND))
    for k, src, top, dsh, dhead, ok in neck_report():
        w("   %-13s %-4s 윗변 %+.3f R · 어깨선과 %+.3f · 머리 원반 밑과 %+.3f → %s%s" % (
            k, src, top, dsh, dhead, "통과" if ok else "★ 위반", "  (몸 표면 dy %+.4f 적용)" % TIE_DY if k == "stripedtie" else ""))
    r16tie = max(q[1] for q in [p for p in M16.ICON["stripedtie"] if p.src == "B0"][0].pts)
    w("   줄무늬타이 R16: 매듭 윗변 %+.3f R (머리 원반 안 %+.3f R 침범 · 어깨선 위 %+.3f R) → R17 %+.3f R" % (r16tie, r16tie + HEAD_R_SHIP, r16tie - SH, r16tie + TIE_DY))
    w()
    rows = survival_rows()
    w("== 4. 조각별 생존 (1pt 하한, 몸 표면 변환 후) — 우선 6종 ==")
    for k in PRIORITY:
        for r in rows:
            if r["kind"] != k: continue
            p = r["p"]; x0, y0, x1, y1 = rig.bounds(p.pts)
            w("   %-11s %-5s %-10s 실폭 %.4f 잉크 %.2f×%.2fR  .75 %s  1.0 %s  .60 %s  .35 %s  %s" % (
                k, p.src, p.role, p.width_R(), x1 - x0, y1 - y0, "생존" if r["ok75"] else "소멸", "생존" if r["ok100"] else "소멸",
                "생존" if r["ok60"] else "소멸", "생존" if r["ok35"] else "소멸", "" if r["ok75"] else r["why75"]))
    w("   전체 87+1: @0.75 생존 %d / 소멸 %d (R16: 45 / 42)" % (sum(1 for r in rows if r["ok75"]), sum(1 for r in rows if not r["ok75"])))
    w()
    w("== 5. [5] 색 — 선 브라스 · 강조 등급색(일반 #8A8F98 가정) · 하이라이트 흰 ==")
    for label, c in (("선/테", PALETTE_R17["ink"]), ("강조(일반)", PALETTE_R17["accent"]), ("반대쪽 눈·하이라이트", PALETTE_R17["white"])):
        o, same = M16.worn_color(c)
        w("   %-12s %s → WornColor %s %s" % (label, c, o, "항등" if same else "★ 변형 — 우회 필요"))
    w("   ★ 일반 등급 #8A8F98 은 UiChrome.TextTertiary #8b939f(잠긴 실루엣 예약색)와 ΔE 2.4 — 카드 예약이지 몸이 아니다. 몸에서의 충돌 여부 미확인(design-art).")
    w()
    w("== 6. EYES 쌍별 실루엣 (외알안경 반전 후, 1pt 실폭) ==")
    ks = ["sunglasses", "roundglasses", "goggles", "monocle"]
    prof = {k: rig.profile([p.as_shape() for p in WORN[k] if not p.base], 0.0) for k in ks}
    for i in range(4):
        for j in range(i + 1, 4):
            d = rig.max_delta(prof[ks[i]], prof[ks[j]])
            w("   %-13s vs %-13s Δr %.3f R = %.2f 획(1pt)" % (ks[i], ks[j], d, d / M16.ONE_PT_R[SHIP]))
    w()
    cape_report(w)

if __name__ == "__main__":
    report()
