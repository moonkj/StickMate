"""부채꼴 메뉴 심볼 5종 — 조형 검산 하니스 (design-equipment, 2026-09-06)

프로덕션 .cs 는 한 줄도 건드리지 않는다. 아래 좌표는
Assets/_Project/Scripts/Interaction/GearRadialMenuWidget.cs 의 Build*Symbol() 을
사람이 옮겨 적은 거울이고, §0 교정 블록이 그 거울을 알려진 값으로 검사한다.

단위: pt. 원점 = Symbol 상자(24×24) 중심. y 위.
"""
import math
import numpy as np
from scipy import ndimage

# ───────────────────────── 프로덕션 상수 거울 ─────────────────────────
SYMBOL_BOX = 24.0          # GearRadialMenuWidget.SymbolBoxPoints
SYMBOL_STROKE = 2.0        # GearRadialMenuWidget.SymbolStroke  (= W)
BUTTON_D = 44.0            # ButtonDiameterPoints
HOVER_D = 48.0             # HoverScale = 48/44
POWER_GAP_DEG = 50.0
POWER_RING_D = 20.0
EDGE_FEATHER = 0.5         # UiChrome.EdgeFeather (획 바깥 알파 램프, 한쪽)

# 인계본 카드 문법(현행 실장, 비교 기준)
CARD_BOX = 58.0            # CharacterInfoWindow.IconSize
CARD_STROKE_FRAC = 2.2 / 64.0   # AccessoryCardIcon.Frame.StrokeFraction
CARD_STROKE = CARD_BOX * CARD_STROKE_FRAC
SLOT_BOX = 24.0            # CharacterInfoWindow.SlotIconSize — 부채꼴 심볼과 같은 상자
SLOT_STROKE = SLOT_BOX * CARD_STROKE_FRAC

# ───────────────────────── 래스터 ─────────────────────────
SS = 32          # pt 당 표본
WIN = 32.0       # 그리드 창(넘침을 담기 위해 상자보다 크다)
N = int(WIN * SS)
_ax = (np.arange(N) + 0.5) / SS - WIN / 2.0
GX, GY = np.meshgrid(_ax, -_ax)     # y 위


def _seg_dist(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    wx, wy = px - ax, py - ay
    L2 = vx * vx + vy * vy
    t = 0.0 if L2 == 0 else np.clip((wx * vx + wy * vy) / L2, 0.0, 1.0)
    return np.hypot(wx - t * vx, wy - t * vy)


class Piece:
    """한 Image = 한 조각. mask() 는 core(불투명 심) 불리언."""

    def __init__(self, name, kind, thickness, **kw):
        self.name, self.kind, self.t = name, kind, thickness
        self.kw = kw

    def mask(self):
        k = self.kind
        if k == "capsule":
            c, L, a = self.kw["center"], self.kw["length"], math.radians(self.kw["angle"])
            h = max(0.0, L / 2 - self.t / 2)
            ax, ay = c[0] - math.cos(a) * h, c[1] - math.sin(a) * h
            bx, by = c[0] + math.cos(a) * h, c[1] + math.sin(a) * h
            return _seg_dist(GX, GY, ax, ay, bx, by) <= self.t / 2
        if k == "poly":
            pts = self.kw["points"]
            m = np.zeros_like(GX, dtype=bool)
            for i in range(1, len(pts)):
                m |= _seg_dist(GX, GY, pts[i - 1][0], pts[i - 1][1],
                               pts[i][0], pts[i][1]) <= self.t / 2
            return m
        if k == "ring":
            c, d = self.kw.get("center", (0.0, 0.0)), self.kw["diameter"]
            mid = (d - self.t) / 2
            r = np.hypot(GX - c[0], GY - c[1])
            m = np.abs(r - mid) <= self.t / 2
            gap = self.kw.get("gap_deg", 0.0)
            if gap > 0:
                th = np.degrees(np.arctan2(GY - c[1], GX - c[0])) % 360.0
                g0 = (self.kw.get("gap_center_deg", 90.0) - gap / 2) % 360.0
                g1 = (self.kw.get("gap_center_deg", 90.0) + gap / 2) % 360.0
                inside = ((th >= g0) & (th <= g1)) if g0 < g1 else ((th >= g0) | (th <= g1))
                m &= ~inside
            return m
        if k == "rectoutline":
            c, w, h, t = self.kw["center"], self.kw["w"], self.kw["h"], self.t
            dx = np.abs(GX - c[0]) - w / 2
            dy = np.abs(GY - c[1]) - h / 2
            outside = np.maximum(dx, dy) <= 0
            inner = (np.abs(GX - c[0]) <= w / 2 - t) & (np.abs(GY - c[1]) <= h / 2 - t)
            return outside & ~inner
        if k == "disc":
            c, d = self.kw["center"], self.kw["diameter"]
            return np.hypot(GX - c[0], GY - c[1]) <= d / 2
        raise ValueError(k)


def P(name, L, t, ang, cx, cy):
    return Piece(name, "capsule", t, length=L, angle=ang, center=(cx, cy))


def polar(deg, r):
    return (math.cos(math.radians(deg)) * r, math.sin(math.radians(deg)) * r)


# ───────────────────────── 현행 5종 (코드 거울) ─────────────────────────
def cur_stopwatch():
    W = SYMBOL_STROKE
    hour = math.radians(-30.0)
    return [
        P("Crown", 6.0, 4.0, 0.0, 0.0, 11.5),
        Piece("Ring", "ring", W, diameter=20.0),
        P("MinuteHand", 6.5, W, 90.0, 0.0, 3.25),
        P("HourHand", 5.0, W, -30.0, math.cos(hour) * 2.5, math.sin(hour) * 2.5),
    ]


def cur_stickman():
    sh, pe = (0.0, 3.5), (0.0, -4.5)
    aL, aR = polar(-140, 3.0), polar(-40, 3.0)
    lL, lR = polar(-106, 3.5), polar(-74, 3.5)
    return [
        Piece("IconHead", "ring", 1.8, diameter=7.0, center=(0.0, 8.0)),
        P("IconSpine", 9.0, 1.8, 90.0, 0.0, 0.0),
        P("IconArmL", 6.0, 1.8, -140.0, sh[0] + aL[0], sh[1] + aL[1]),
        P("IconArmR", 6.0, 1.8, -40.0, sh[0] + aR[0], sh[1] + aR[1]),
        P("IconLegL", 7.0, 1.8, -106.0, pe[0] + lL[0], pe[1] + lL[1]),
        P("IconLegR", 7.0, 1.8, -74.0, pe[0] + lR[0], pe[1] + lR[1]),
    ]


def cur_checklist():
    W = SYMBOL_STROKE
    v = (-7.0, -8.0)
    s, l = polar(135, 1.6), polar(45, 3.0)
    return [
        P("Line0", 9.0, W, 0.0, 5.5, 7.0),
        P("Line1", 9.0, W, 0.0, 5.5, 0.0),
        P("Line2", 9.0, W, 0.0, 5.5, -7.0),
        P("Strike", 9.0, 1.4, 0.0, 5.5, -7.0),
        Piece("Box0", "rectoutline", 1.0, center=(-6.0, 7.0), w=4.5, h=4.5),
        Piece("Box1", "rectoutline", 1.0, center=(-6.0, 0.0), w=4.5, h=4.5),
        P("CheckShort", 3.2, 1.6, 135.0, v[0] + s[0], v[1] + s[1]),
        P("CheckLong", 6.0, 1.6, 45.0, v[0] + l[0], v[1] + l[1]),
    ]


def cur_megaphone():
    W = SYMBOL_STROKE
    return [
        P("HornUpper", 13.0, W, 13.0, -1.6, 5.0),
        P("HornLower", 13.0, W, -13.0, -1.6, -5.0),
        P("HornNeck", 5.6, W, 90.0, -8.4, 0.0),
        P("HornMouth", 11.6, W, 90.0, 5.0, 0.0),
        P("WaveUpper", 4.6, 1.6, 30.0, 9.6, 3.4),
        P("WaveLower", 4.6, 1.6, -30.0, 9.6, -3.4),
    ]


def cur_power():
    W = SYMBOL_STROKE
    stem = POWER_RING_D * 0.55
    return [
        Piece("PowerRing", "ring", W, diameter=POWER_RING_D,
              gap_deg=POWER_GAP_DEG, gap_center_deg=90.0),
        P("PowerStem", stem, W, 90.0, 0.0, stem * 0.5),
    ]


CURRENT = {
    "① 집중(스톱워치)": cur_stopwatch,
    "② 캐릭터(스틱맨)": cur_stickman,
    "③ 할일(체크리스트)": cur_checklist,
    "④ 행동명령(확성기)": cur_megaphone,
    "⑤ 종료(전원)": cur_power,
}


# ───────────────────────── 측정 ─────────────────────────
def measure(pieces):
    masks = [p.mask() for p in pieces]
    union = np.zeros_like(masks[0])
    for m in masks:
        union |= m

    # 조각별 최소 간극(코어↔코어). 겹치면 0(음수로 구분하지 않는다).
    clears = []
    edts = [ndimage.distance_transform_edt(~m) / SS for m in masks]
    for i in range(len(pieces)):
        for j in range(i + 1, len(pieces)):
            if not masks[j].any() or not masks[i].any():
                continue
            d = edts[i][masks[j]].min()
            clears.append((pieces[i].name, pieces[j].name, float(d)))
    clears.sort(key=lambda r: r[2])

    # 완전히 가려진 조각
    hidden = []
    for i, m in enumerate(masks):
        other = np.zeros_like(m)
        for j, mj in enumerate(masks):
            if j != i:
                other |= mj
        if m.any() and not (m & ~other).any():
            hidden.append(pieces[i].name)

    ys, xs = np.where(union)
    bx0, bx1 = _ax[xs.min()], _ax[xs.max()]
    by1, by0 = -_ax[ys.min()], -_ax[ys.max()]

    return {
        "n": len(pieces),
        "thicknesses": sorted({round(p.t, 3) for p in pieces}),
        "ink_frac": float(union.sum()) / (SYMBOL_BOX * SS) ** 2,
        "bbox": (bx0, bx1, by0, by1),
        "overflow_box": max(0.0, max(abs(bx0), abs(bx1), abs(by0), abs(by1)) - SYMBOL_BOX / 2),
        "clears": clears,
        "hidden": hidden,
        "union": union,
        "masks": masks,
    }


def alpha_field(union):
    """알파 램프까지 포함한 실제 화면 알파.

    ★ 2026-09-06 정정 — 옛 구현은 램프를 코어 <바깥에 통째로> 붙였다(`1 − d/F`, 코어 안은 전부 1).
    프로덕션은 그렇게 굽지 않는다. `UiChrome.Capsule()`(`UiChrome.cs:948`)과
    `CircleSprite()`(`:892`)가 굽는 식은

        alpha = clamp01((core − d) / feather + 0.5)

    이고, `d` 를 코어 경계에서 잰 <부호 있는 거리> `s = d − core`(바깥이 +)로 바꾸면

        alpha = clamp01(0.5 − s / feather)

    이다. 즉 **알파 0.5 등고선이 정확히 코어 가장자리**이고 램프는 그 가장자리를
    <가운데 두고> `s ∈ [−F/2, +F/2]` 로 걸친다. 코어 안쪽 F/2 도 알파 1이 아니다.
    근거·검산: `design/art/r27_shrink_fg3.out.txt` §0 교정6(텍셀 굽기 재현).
    """
    out = ndimage.distance_transform_edt(~union) / SS   # 코어 바깥으로 나간 거리
    ins = ndimage.distance_transform_edt(union) / SS    # 코어 안으로 들어온 거리
    signed = out - ins                                  # 바깥이 +
    return np.clip(0.5 - signed / EDGE_FEATHER, 0.0, 1.0)


def valley(clear_pt):
    """두 획 사이에 알파 0 인 골이 몇 pt 남는가.

    ★ 2026-09-06 정정 — 옛 식은 `clear_pt − 2 × EDGE_FEATHER`(= 간극 − 1.0pt)였다.
    램프가 코어 가장자리에 <걸치므로> 한 변이 먹는 것은 `EDGE_FEATHER` 가 아니라
    `EDGE_FEATHER/2` 이고, 두 변을 합쳐 `EDGE_FEATHER` 다(위 `alpha_field` 참고).
    배율까지 넣은 일반형은 `(clear_pt − EDGE_FEATHER) · k · S ≥ 1.0px`
    (`k` = 배치 배율 — Ø44는 1, Ø36 축소 폴백은 36/44 —, `S` = DPI 배율).
    """
    return clear_pt - EDGE_FEATHER
