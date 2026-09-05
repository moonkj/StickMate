# -*- coding: utf-8 -*-
"""R18+R19 — 재질 팔레트 적용(design-art ROLE) + 사용자 지적 6건 (design-equipment, 2026-09-05)

R18(리더 판정 L-1~L-4): 채움 = M/M2 불투명 + 잉크 윤곽 · 독립선 = 그 조각의 M/M2 · 채움 위 낱선 = 잉크(인계본 α) ·
  야구모자 띠 = 그늘 · 하이라이트 흰 α0.42 · 등급색은 조각에 0개 · 왕관 M2 #C6443C · 날개·배낭 그룹 α 1.0 ·
  투명 렌즈(동그란안경·외알안경)는 몸 채움 없음(카드만 M2 워시) · 반대쪽 눈 = 잉크 대비색.
R19(사용자 원문): "모자 같은경우 머리가 모자속으로 들어가는데 모자속 색상이 머리앞으로 나와있음 그리고 선글라스는 색상을 넣으면서
  디자인이 변했음 선글라스 확인필요 고글도 다시확인 외눈안경 손잡이 끝 색상을 다른색으로 … 나비넥타이 위치가 너무 내려와있음 …
  가방디자인이 이상함, 가방의 뒷면이 몸쪽에서 봤을땐 보여야하는데 가방의 앞면이 보임"
  ① 모자 2층(H-1 개정): 챙의 먼 쪽 절반·왕관 안쪽 뒷벽은 머리 뒤(back), 관 앞·띠·가까운 챙은 머리 앞(front). 불투명 바탕 조각 폐지(채움이 불투명).
  ② 선글라스: ROLE 정정 — 렌즈 채움 SH(어두운 렌즈) · 윤곽 M2(테). ③ 고글: 렌즈 원 윤곽 없음. ④ 외알안경 구슬: M2 채움·윤곽 없음·r 1pt.
  ⑤ 나비넥타이 dy +0.30 R (N-1 예외 폐지). ⑥ 배낭 착용면 = 뒷판(민무늬 M) + 어깨끈 2(M2, 앞층); 덮개·버클·주머니는 카드에만.
★ 좌표: ①의 분할·뒷벽·왕관 재맞춤, ④ 구슬 반경, ⑤ dy, ⑥ 끈 외에는 0점 변경. 카드 좌표 0점 변경. 조각 이름 유지(base 조각만 사라진다).
★ 색의 정본은 palette_model.ROLE — 이 파일은 ROLE 위에 R19 정정(ROLE_FIX)만 얹는다. palette_* 는 읽기만.
"""
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig, handoff
import r16_model as M16
import r17_model as M17
import palette_model as PM

SHIP = M16.SHIP
KINDS = M17.KINDS
KO, SLOT, ISLOT, CAPES = M17.KO, M17.SLOT, M17.ISLOT, M17.CAPES
HEAD_KINDS, NECK_KINDS = M17.HEAD_KINDS, M17.NECK_KINDS
PRIORITY = ["clothhat", "furhat", "fedora", "crown", "sunglasses", "goggles", "monocle", "bowtie", "backpack"]
HEAD_R_COVER, HEAD_R_SHIP = M17.HEAD_R_COVER, M17.HEAD_R_SHIP

class Piece(M17.Piece):
    __slots__ = ("crole",)          # (fill_role, line_role) — ROLE/ROLE_FIX 에서 푼 색 역할
    def __init__(self, *a, crole=None, **k):
        super().__init__(*a, **k); self.crole = crole

def _cp(p, pts=None, **over):
    """r16/r17/r19 어느 Piece 든 받아 r19 Piece 로 복사한다(r16 Piece 에는 base/conditional/transform/sway 가 없다)."""
    g = lambda a, dflt: getattr(p, a, dflt)
    d = dict(kind=p.kind, name=p.name, src=p.src, call=p.call, pts=p.pts if pts is None else pts, loop=p.loop, filled=p.filled,
             mult=p.mult, nominal_R=p.nominal_R, fill=p.fill, line=p.line, dash=p.dash, group_alpha=p.group_alpha,
             layer=p.layer, role=p.role, note=p.note, base=g("base", False), conditional=g("conditional", None),
             transform=g("transform", ""), sway=g("sway", None), crole=g("crole", None))
    d.update(over)
    return Piece(**d)

# ============================================================================
# 1. 색 역할 — ROLE + R19 정정
# ============================================================================
# ★ R19 정정(design-art ROLE 위에 얹는다 — 사유는 각 줄): (fill_role, line_role)
ROLE_FIX = {
    # ★ R20 정정: R19 의 정정 4건(선글라스 SH/M2 · 고글 렌즈 윤곽 없음 · 구슬 M2 윤곽 없음 · 배낭 끈 M2)은 design-art 가 교차 확인해
    #   정본 palette_model.ROLE 에 편입했다(R-5′). 왕관 뒷벽 BW 는 내 안(M)이 아니라 **SH #2B220A 윤곽 없음**으로 대체됐다
    #   (M 은 봉우리와 대비 1.00 이라 사각 덩어리 — 리더가 design-art 안 채택). 그래서 이 표는 비어 있고, 전부 ROLE 에서 읽는다.
}
BEAD_R = M16.ONE_PT_R[SHIP]      # 0.1719 R — 윤곽 없는 채움 원반이 1× 에서 2px 로 남는 최소 = r ≥ w(1pt). 1-A 만 보면 r ≥ 0.75w = 0.129 R.

def crole_of(kind, src):
    if (kind, src) in ROLE_FIX: return ROLE_FIX[(kind, src)]
    return PM.role_of(kind, src)

def resolve(kind, crole, surface, ink_hex):
    """→ (fill_hex|None, fill_alpha, line_hex|None). surface 'card'|'body'. crole = (fill_role, line_role)."""
    it = PM.BY_KIND[kind]; m, m2 = it[4], it[5]
    f, l = crole
    fill, fa = None, 1.0
    if f == "M": fill = m
    elif f == "M2": fill = m2
    elif f == "SH": fill = PM.shaded(m)
    elif f == "EYE": fill = PM.CHARCOAL if PM.L(ink_hex) >= 0.22 else PM.INK_WHITE
    elif isinstance(f, tuple) and f[0] == "CARDWASH":
        if surface == "card": fill, fa = m2, f[1]
        else:
            # ★ R20(EyesVisorOpacityTests 충돌 해소): 몸의 유리 렌즈 = 「잉크 위에 M2 를 α 로 미리 합성한 불투명 판」(GLASS).
            #   보이는 것은 L-4 의 투명 워시와 같고(머리가 균일 잉크라 합성 결과가 같다), 도형은 Filled 라 격자 커버리지·눈 자리 덮음 시험을 통과한다.
            fill, fa = PM.over(PM.rgb(m2), f[1], PM.rgb(ink_hex)), 1.0
    line = None
    if l == "INK": line = PM.CARD_INK if surface == "card" else ink_hex
    elif l == "M": line = m
    elif l == "M2": line = m2
    elif l == "W": line = PM.INK_WHITE
    return fill, fa, line

def line_alpha(p):
    """채움 위 낱선(잉크)은 인계본 α 유지(R-4), 독립선(M/M2)·하이라이트(W)는 자기 α."""
    if p.line is None: return 1.0
    if p.line[0] == "white": return M16.HI_ALPHA
    if p.crole and p.crole[1] == "INK" and p.line[0] == "ink": return p.line[1]
    return 1.0

# ============================================================================
# 2. 기하 — ① 모자 2층
# ============================================================================
def _split_lens(p, tip_a=0, tip_b=16):
    """인계본 챙(렌즈꼴 M tip Q top tip Q bottom Z, 16분할)을 위 호(먼 쪽)와 아래 호(가까운 쪽)로 가른다.
    반환 (far_fill, far_arc, near_fill, near_arc) — 자른 선(장축)은 그리지 않는다(실제 챙에 그 선은 없다)."""
    pts = p.pts
    far = pts[tip_a:tip_b + 1]                     # 왼 끝 → 위 호 → 오른 끝
    near = pts[tip_b:] + [pts[tip_a]]              # 오른 끝 → 아래 호 → 왼 끝
    return list(far), list(far), list(near), list(near)

def _head_pieces19(kind):
    src17 = [p for p in M17.WORN[kind] if not p.base]      # 불투명 바탕 조각 폐지(채움이 M/M2 불투명)
    if kind == "crown":
        src17 = _refit_crown(src17)
    out = []
    for p in src17:
        cr = crole_of(kind, p.src)
        if kind in ("clothhat", "fedora") and p.src == "B2":
            far_f, far_a, near_f, near_a = _split_lens(p)
            out.append(_cp(p, pts=far_f, name=p.name + ".far", src=p.src + "far", layer="back", loop=True, filled=True,
                           line=None, crole=(cr[0], None), role="챙 먼 쪽(뒤층)",
                           note="[①] 챙 위 호 절반 — 머리 뒤. 자른 선(장축)은 안 그린다"))
            out.append(_cp(p, pts=far_a, name=p.name + ".farArc", src=p.src + "fa", layer="back", loop=False, filled=False,
                           fill=None, crole=(None, cr[1]), role="챙 먼 쪽 윤곽(뒤층)"))
            out.append(_cp(p, pts=near_f, name=p.name + ".near", src=p.src + "near", layer="front", loop=True, filled=True,
                           line=None, crole=(cr[0], None), role="챙 가까운 쪽(앞층)"))
            out.append(_cp(p, pts=near_a, name=p.name + ".nearArc", src=p.src + "na", layer="front", loop=False, filled=False,
                           fill=None, crole=(None, cr[1]), role="챙 가까운 쪽 윤곽(앞층)"))
            continue
        out.append(_cp(p, layer="front", crole=cr))
    if kind == "crown":
        # 안쪽 뒷벽: 몸(B0) 옆선 사이, 테 윗변(y 40.5)에서 봉우리 중간 높이(y 21)까지 — 머리 뒤 층. ★ 리더 판정(R19): 봉우리 사이로 머리가 보이는 일은 **기하적으로 없다**
        #   (틈 36px 를 1pt 윤곽이 전부 먹는다, verify-change 픽셀 실측 자홍 0px) — 이 벽은 봉우리 사이 「머리 위」 틈을 안쪽 색(ROLE BW = SH)으로 채우는 조각이다.
        fx, fy = M17.hat_to_R("crown")
        def side(y, left):   # B0 옆선 (13,41)→(15,17) / (51,41)→(49,17)
            t = (41.0 - y) / 24.0
            return (13.0 + 2.0 * t) if left else (51.0 - 2.0 * t)
        wall = [(side(40.5, True), 40.5), (side(21.0, True), 21.0), (side(21.0, False), 21.0), (side(40.5, False), 40.5)]
        b0 = [p for p in out if p.src == "B0"][0]
        out.insert(0, _cp(b0, pts=[(fx(x), fy(y)) for x, y in wall], name="crown.BackWall", src="BW", layer="back", loop=True, filled=True,
                          crole=PM.ROLE["crown"]["BW"], role="왕관 안쪽 뒷벽(뒤층)", note="[①] 봉우리 사이로 보이는 안쪽 — 머리 뒤. 색 = ROLE BW (design-art: SH #2B220A 윤곽 없음)"))
    return out

CROWN_SHOW = 0.40     # 골(y=29) 높이 ≤ 머리 꼭대기 − 0.40 — 재맞춤 파라미터(현행 유지). ★ 이 값으로도 머리는 봉우리 사이로 **보이지 않는다**(리더 판정 「기하적으로 불가」, 자홍 0px).
                      # 첫 판 0.20 은 앞층 윤곽(1pt = 0.172 R, 골 양쪽 두 변)이 그 틈을 다 먹어 0 px 였다(자홍 대조 (b) 8 px 중 0).
def _refit_crown(pieces):
    """왕관 재맞춤(H-2 + 골 높이 ≤ HEAD_R_COVER − CROWN_SHOW). 가장 작은 u. ★ 골을 낮춰도 머리 노출은 실질 0(리더 판정) — 파라미터는 현행 유지."""
    import numpy as np
    ys = np.arange(0.0, 64.0, 0.25); chw = M17.central_hw_profile("crown", ys)
    best = None
    for u in np.arange(0.045, 0.150, 0.001):
        cands = []
        for dy in np.arange(0.0, 4.5, 0.02):
            e = M17.evaluate("crown", u, dy, 1.0, ys, chw)
            dip = dy - M17.CROWN_DIP_ICON_Y * u
            if e["ok"] and dip <= HEAD_R_COVER - CROWN_SHOW: e["dip"] = dip; cands.append(e)
        if cands:
            best = min(cands, key=lambda e: abs(e["dip"] - (HEAD_R_COVER - CROWN_SHOW))); break
    if best is None: raise SystemExit("왕관 재맞춤 실패")
    global CROWN_FIT
    CROWN_FIT = best
    M17.HAT_FIT["crown"] = best          # hat_to_R 이 이 값을 읽는다
    fx, fy = M17.hat_to_R("crown")
    icon = M17._icon_polys("crown"); srcs = ["%s%d" % (pc.call, i) for i, pc in enumerate(icon)]
    bymap = {s: pc for s, pc in zip(srcs, icon)}
    out = []
    for p in pieces:
        pc = bymap[p.src]
        out.append(_cp(p, pts=[(fx(x), fy(y)) for x, y in pc.pts], transform="HAT_FIT u=%.4f ky=1.00 dy=%.4f (R19 재맞춤: 골 %.3f R)" % (best["u"], best["dy"], best["dip"])))
    return out
CROWN_FIT = None

# ============================================================================
# 3. 기하 — ④ 구슬 · ⑤ 나비넥타이 · ⑥ 배낭 · 렌즈 · 그룹 α
# ============================================================================
_BOWTIE_WING_TOP = max(q[1] for p in M17.WORN["bowtie"] if p.src == "B0" for q in p.pts)      # −1.4708 R (인계본 날개 윗변)
BOWTIE_DY = round((-HEAD_R_SHIP - 0.03) - _BOWTIE_WING_TOP, 3)   # 날개 윗변을 머리 잉크 원반 밑 0.03 R 아래로 — 정본 원반 1.17193 에서 +0.269
# ★ 2026-09-05 정정: 첫 판 +0.30 은 원반 1.1432 기준이었고 정본 1.17193 에서는 날개 윗변이 원반과 −0.0009 R 로 사실상 접선(design-character 감사)
#   → 계산식으로 바꿔 +0.269 (매듭 윗변 −1.412, 어깨선 −0.094, N-1 밴드 안).
STRAP_MULT = 2.5      # 어깨끈 = 독립선 M2, 명목 0.1243×2.5 = 0.311 R(1.8pt @0.75)
SH = rig.SHOULDER_R
STRAPS = {"STRAP_L": [(-0.42, SH + 0.10), (-0.42, SH - 1.70), (-0.95, SH - 1.95)],
          "STRAP_R": [(0.42, SH + 0.10), (0.42, SH - 1.70), (0.95, SH - 1.95)]}
BACKPACK_CARD_ONLY = {"B2", "RB3", "RB4", "H5"}     # 덮개·버클·주머니·하이라이트 = 앞면 → 카드에만

# ★ R20 — 외알안경 알·눈을 ±0.56 R 로(R17c ±0.46 에서 바깥으로 0.10). 렌즈에 불투명 판(GLASS)이 생기면 EyesVisorOpacityTests
#   「한쪽만_가리는_물건만_반대쪽_눈을_보여준다」의 간격 조건(가리개 채움 왼끝 − 눈 오른끝 ≥ 1.5 W = 0.516 R)에 +0.46 은 0.94 W 로 걸린다.
#   ±0.56 이 그 조건을 넘는 최소 0.02 격자값(1.52 W). E-1 「알 중심 = 반대쪽 눈 거울 위치」는 그대로다. 복제 계측: r20_eyescover.py.
MONOCLE_DX20 = 0.10
MONOCLE_CX_R20, EYE_X_R20 = M17.MONOCLE_LENS_CX_R + MONOCLE_DX20, M17.EYE_X - MONOCLE_DX20      # +0.56 / −0.56

def _body_pieces19(kind):
    if kind in HEAD_KINDS: return _head_pieces19(kind)
    out = []
    for p in M17.WORN[kind]:
        if p.base: continue
        cr = crole_of(kind, p.src) if p.src != "EYE" else ("EYE", None)
        q = _cp(p, crole=cr, group_alpha=PM.GROUP_ALPHA_BODY)
        if kind == "monocle":
            sdx = -MONOCLE_DX20 if p.src == "EYE" else MONOCLE_DX20
            q = _cp(q, pts=[(x + sdx, y) for x, y in q.pts], transform=(q.transform + " · " if q.transform else "") + "R20 dx %+.2f (알·눈 ±%.2f)" % (sdx, MONOCLE_CX_R20))
        if kind == "monocle" and p.src == "CB3":
            x0, y0, x1, y1 = rig.bounds(q.pts); cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
            q = _cp(q, pts=rig.poly(cx, cy, BEAD_R, 24), line=None, transform=p.transform + " · [④] r %.4f→%.4f R · 윤곽 없음 · M2" % ((x1 - x0) / 2, BEAD_R))
        if kind == "bowtie":
            q = _cp(q, pts=[(x, y + BOWTIE_DY) for x, y in p.pts], transform="SHIFT dy=%+.2f R [⑤]" % BOWTIE_DY)
        if kind == "backpack" and p.src in BACKPACK_CARD_ONLY: continue
        if kind in ("roundglasses", "monocle") and p.src in ("CB0", "CB1"):
            q = _cp(q, transform=(p.transform + " · " if p.transform else "") + "[L-4→R20] 몸 채움 = GLASS(잉크 위 M2 α 사전 합성, 불투명) · 테 M")
        out.append(q)
    if kind == "backpack":
        base = out[0]
        for s, pts in STRAPS.items():
            out.append(_cp(base, pts=pts, name="backpack." + s, src=s, call="S", loop=False, filled=False, fill=None, line=("ink", 1.0),
                           mult=STRAP_MULT, nominal_R=M16.SLOT_STROKE_R["back"] * STRAP_MULT, layer="front", group_alpha=1.0,
                           crole=PM.ROLE["backpack"][s], role="어깨끈(앞층)", conditional=None, transform="",
                           note="[⑥] 어깨 위에서 가슴을 지나 뒷판 아래 모서리로 — 앞층(몸 앞)"))
    return out

WORN = {k: _body_pieces19(k) for k in KINDS}

# 카드 = 인계본 아이콘 좌표(M16.ICON) + ROLE(+정정). 알파는 카드 워시 예외만.
def card_pieces(kind):
    out = []
    for p in M16.ICON[kind]:
        cr = crole_of(kind, p.src)
        out.append(_cp(p, crole=cr, group_alpha=1.0))
    return out
CARD = {k: card_pieces(k) for k in KINDS}

# ============================================================================
# 4. 검산
# ============================================================================
def neck_table():
    rows = []
    for k in NECK_KINDS:
        a = [p for p in WORN[k] if p.src == M17.NECK_ANCHOR[k]][0]
        top = max(q[1] for q in a.pts)
        ok = (top <= -HEAD_R_SHIP - 0.01) and (abs(top - SH) <= M17.NECK_BAND)
        rows.append((k, M17.NECK_ANCHOR[k], top, top - SH, top + HEAD_R_SHIP, ok))
    return rows

def report(out=sys.stdout):
    w = lambda s="": out.write(s + "\n")
    w("== 0. 팔레트 — design-art palette_model.ROLE 적용 + R19 정정 %d건 ==" % len(ROLE_FIX))
    for (k, s), (f, l) in ROLE_FIX.items():
        w("   %-12s %-8s fill %-6s line %-4s  (ROLE 원본: %s)" % (k, s, f, l, PM.ROLE[k].get(s, "(신설)")))
    w("   구슬 최소 반경: 윤곽 없는 채움 원반이 1× 에서 살아남는 r ≥ w(1pt) = %.4f R(지름 2px) · 규칙 1-A 만 보면 r ≥ 0.75w = %.4f R. 채택 r = %.4f R (인계본 3.2u = 0.1085 R 의 ×%.2f)" % (
        M16.ONE_PT_R[SHIP], 0.75 * M16.ONE_PT_R[SHIP], BEAD_R, BEAD_R / 0.1085))
    bead = [p for p in WORN["monocle"] if p.src == "CB3"][0]; x0, y0, x1, y1 = rig.bounds(bead.pts)
    bc = ((x0 + x1) / 2, (y0 + y1) / 2); dist = math.hypot(*bc)
    w("   구슬 중심 (%+.3f, %+.3f) · 머리 중심에서 %.3f R · 안쪽 가장자리 %.3f vs 출하 원반 %.3f → %s" % (bc[0], bc[1], dist, dist - BEAD_R, HEAD_R_SHIP, "깨끗이 밖" if dist - BEAD_R >= HEAD_R_SHIP else "★ 겹침 %.3f" % (HEAD_R_SHIP - (dist - BEAD_R))))
    w("   선글라스 렌즈 SH = %s (M %s ×0.28) · 테 M2 %s: 렌즈↔테 대비 %.2f · 렌즈↔검은 머리 %.2f · 테↔검은 머리 %.2f · 테↔흰 머리 %.2f" % (
        PM.shaded(PM.BY_KIND["sunglasses"][4]), PM.BY_KIND["sunglasses"][4], PM.BY_KIND["sunglasses"][5],
        PM.cr(PM.shaded(PM.BY_KIND["sunglasses"][4]), PM.BY_KIND["sunglasses"][5]), PM.cr(PM.shaded(PM.BY_KIND["sunglasses"][4]), "#111111"),
        PM.cr(PM.BY_KIND["sunglasses"][5], "#111111"), PM.cr(PM.BY_KIND["sunglasses"][5], "#FFFFFF")))
    w("   (ROLE 원안 M 렌즈 %s ↔ M2 테 %s 대비 %.2f — 그래서 테가 안 읽혔다)" % (PM.BY_KIND["sunglasses"][4], PM.BY_KIND["sunglasses"][5], PM.cr(PM.BY_KIND["sunglasses"][4], PM.BY_KIND["sunglasses"][5])))
    w("   고글 렌즈 M2 %s ↔ 판 M %s 대비 %.2f (윤곽 없이 경계가 선다)" % (PM.BY_KIND["goggles"][5], PM.BY_KIND["goggles"][4], PM.cr(PM.BY_KIND["goggles"][5], PM.BY_KIND["goggles"][4])))
    w()
    w("== 1. [①] 모자 2층 ==")
    for k in HEAD_KINDS:
        ps = WORN[k]
        back = [p.name for p in ps if p.layer == "back"]; front = [p.name for p in ps if p.layer == "front"]
        w("   %-9s 뒤층 %d: %s" % (k, len(back), ", ".join(back) if back else "(없음 — 먼 쪽이 보이는 부위가 없다)"))
        w("             앞층 %d: %s" % (len(front), ", ".join(front)))
    f = CROWN_FIT
    w("   왕관 재맞춤: u %.4f(카드 ×%.2f) dy %+.3f · 착용선(테 밑) %+.3f · 골 %+.3f (머리 꼭대기 %.3f − %.2f) · 꼭대기 %+.3f · 밑 %+.3f · 폭 %.2f R  (R17: u 0.0570 dy 3.120 골 +1.467 — 머리가 안 보였다)" % (
        f["u"], f["u"] / M17.ICON_U_HEAD, f["dy"], f["wear"], f["dip"], HEAD_R_COVER, CROWN_SHOW, f["top"], f["bottom"], f["width"]))
    for k in ("clothhat", "fedora"):
        fh = M17.HAT_FIT[k]; fx, fy = M17.hat_to_R(k); ymid = fy(41.0 if k == "clothhat" else 41.5)
        chord = math.sqrt(max(0, HEAD_R_SHIP ** 2 - ymid ** 2))
        w("   %-9s 챙 장축 y %+.3f R · 그 높이 머리 현 반폭 %.3f · 챙 반폭 %.3f → 먼 쪽 챙이 머리 옆으로 각 %.3f R 보인다" % (k, ymid, chord, fh["width"] / 2, fh["width"] / 2 - chord))
    w()
    w("== 2. [⑤] NECK 착용선 (N-1 예외 폐지) ==")
    for k, src, top, dsh, dhead, ok in neck_table():
        w("   %-13s %-4s 윗변 %+.3f R · 어깨선과 %+.3f · 머리 원반 밑과 %+.3f → %s" % (k, src, top, dsh, dhead, "통과" if ok else "★ 위반"))
    wt = max(q[1] for p in WORN["bowtie"] if p.src == "B0" for q in p.pts)
    w("   나비넥타이 날개 윗변 %+.3f R (머리 원반 밑 %.3f 아래 %.3f R) — dy %+.2f" % (wt, -HEAD_R_SHIP, -HEAD_R_SHIP - wt, BOWTIE_DY))
    w()
    w("== 3. [⑥] 배낭 착용면 ==")
    for p in WORN["backpack"]:
        x0, y0, x1, y1 = rig.bounds(p.pts)
        w("   %-22s %-5s %-12s fill %-4s line %-4s 잉크 %.2f×%.2fR x[%+.2f,%+.2f] y[%+.2f,%+.2f]" % (p.name, p.layer, p.role, p.crole[0], p.crole[1], x1 - x0, y1 - y0, x0, x1, y0, y1))
    w("   카드에만: %s · 어깨끈 명목 %.4f R = %.2f pt @0.75 (M2 독립선, 윤곽 없음) · 몸통 반폭 %.3f R 뒤에 뒷판(x ±1.24)이 각 %.2f R 보인다" % (
        ", ".join(sorted(BACKPACK_CARD_ONLY)), M16.SLOT_STROKE_R["back"] * STRAP_MULT, M16.SLOT_STROKE_R["back"] * STRAP_MULT * M16.head_r_pt(SHIP), M16.W_TORSO_R / 2, 1.24 - M16.W_TORSO_R / 2))
    w("   날개: 깃 B0/B1·정맥·척추 RB4 — 얇은 깃은 앞뒤 면이 같고 척추(폭 0.20 R)는 몸통(반폭 %.3f R) 뒤에 숨는다 → 그대로 맞다" % (M16.W_TORSO_R / 2))
    w()
    w("== 4. 표면별 색 해석 (검은 잉크 몸 / 흰 잉크 몸 / 카드) — 조각별 ==")
    for k in KINDS:
        for p in WORN[k]:
            fb, _, lb = resolve(k, p.crole, "body", PM.INK_BLACK); fw, _, lw = resolve(k, p.crole, "body", PM.INK_WHITE)
            w("   %-13s %-24s %-5s %-14s 검: fill %s line %s | 흰: fill %s line %s" % (k, p.name, p.layer, str(p.crole), fb, lb, fw, lw))
        for p in CARD[k]:
            fc, fa, lc = resolve(k, p.crole, "card", PM.CARD_INK)
            w("   %-13s %-24s card  %-14s fill %s α%.2f line %s" % (k, p.name, str(p.crole), fc, fa, lc))

if __name__ == "__main__":
    report()
