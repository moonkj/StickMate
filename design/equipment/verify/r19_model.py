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
def _split_lens(p, y_cut=None, tip_a=0, tip_b=16):
    """인계본 챙(렌즈꼴 M tip Q top tip Q bottom Z, 16분할)을 위 호(먼 쪽)와 아래 호(가까운 쪽)로 가른다.
    반환 (far_fill, far_arc, near_fill, near_arc) — 자른 선(장축)은 그리지 않는다(실제 챙에 그 선은 없다).

    ★ 2026-09-06 R24 — 자르는 높이를 **관 밑변(관·챙 이음선)** 으로 옮겼다(y_cut, R 좌표).
      옛 판은 언제나 렌즈 끝(장축)에서 잘랐는데, 중절모는 관 밑변(+0.562)이 챙 장축(+0.523)보다
      **0.039 R 위**라 그 사이에 **앞층 채움이 하나도 없는 띠**가 생겼다(실측: y=+0.55 에서 앞층 반폭 0.000).
      물리적으로도 그 띠에 보이는 것은 관의 아랫부분(머리 앞)이지 챙의 먼 쪽(머리 뒤)이 아니다.
      천모자는 두 높이가 같아(둘 다 아이콘 41.0) **한 점도 안 바뀐다** — 아래 검산이 그것을 확인한다."""
    raw = list(p.pts)
    eps = 1e-9
    # 자르는 선이 렌즈 끝(장축)과 같으면 **옛 식 그대로** — 천모자가 한 점도 안 바뀌는 것이 여기서 보장된다.
    if y_cut is None or abs(raw[tip_a][1] - y_cut) <= 1e-6:
        far = raw[tip_a:tip_b + 1]
        near = raw[tip_b:] + [raw[tip_a]]
        return list(far), list(far), list(near), list(near)
    pts = raw[:-1] if (abs(raw[0][0] - raw[-1][0]) < eps and abs(raw[0][1] - raw[-1][1]) < eps) else raw
    up = [i for i in range(tip_a, tip_b + 1) if pts[i][1] >= y_cut - eps]      # R 좌표: 위 = y 큰 쪽
    if not up:
        raise SystemExit("챙 분할 실패: 자르는 선 %.4f 가 렌즈 위 호 밖이다" % y_cut)
    a, b = up[0], up[-1]

    def _cross(i, j):
        (x1, y1), (x2, y2) = pts[i], pts[j]
        if abs(y2 - y1) < 1e-12:
            return (x1, y_cut)
        t = (y_cut - y1) / (y2 - y1)
        return (x1 + (x2 - x1) * t, y_cut)

    c1 = pts[a] if a == tip_a else _cross(a - 1, a)
    c2 = pts[b] if b == tip_b else _cross(b, b + 1)

    def _dedup(q):
        o = []
        for pt in q:
            if not o or abs(pt[0] - o[-1][0]) > 1e-9 or abs(pt[1] - o[-1][1]) > 1e-9:
                o.append(pt)
        return o

    far = _dedup([c1] + pts[a:b + 1] + [c2])
    near = _dedup([c2] + pts[b + 1:] + [pts[tip_a]] + [c1]) if b < tip_b else _dedup(pts[tip_b:] + [pts[tip_a]])
    return far, list(far), near, list(near)

# ============================================================================
# ★★ 2026-09-06 R24 — 천모자·중절모 「앞층만」 H-2 실패 수습 (§14-13-5 별건 배정)
# ============================================================================
# 실측(프로덕션 좌표 직접 파싱, r24_hats.py): 앞층 채움이 머리 현을 덮는 구간이 **끊겨 있었다**.
#   천모자  +1.18→+0.68 · [빈 띠 0.20 R] · +0.48→+0.28      → 착용선 +0.680 (목표 +0.30)
#   중절모  +1.18→+0.78 · [빈 띠 0.27 R] · +0.51→+0.27      → 착용선 +0.776 (목표 +0.30)
# 빈 띠의 정체는 두 가지이고, 둘 다 R19 ①(모자 2층)이 **드러낸** 선행 결함이다:
#   (가) **관이 머리보다 좁다.** 관 반폭 천모자 1.031 R · 중절모 0.955 R < 그 높이의 머리 현
#        (+0.505 에서 1.131 · +0.581 에서 1.092). R17 맞춤은 먼 쪽 챙(= 지금은 뒤층)까지 합집합에
#        넣어 통과했었다 — 뒤층은 머리를 못 덮는다. 즉 이 실패는 R19 분할이 드러낸 **선행 결함**이다.
#   (나) **중절모만**: 관 밑변(+0.562)이 챙 장축(+0.523)보다 위라 그 사이 0.039 R 에 앞층 채움이 0.
#        → `_split_lens(y_cut=관 밑변)` 이 닫는다(위 문단).
# 처방 = §14-13-5 가 적은 그대로 **「관 반폭을 넓혀」**. 다만 인계본 **카드 좌표는 한 점도 못 바꾼다**
#   (R15 이후 불변 · 골든·AccessoryCardIconTests 가 그 위에 서 있다) → **몸 표면에서만 x 배수**를 건다.
#   u·dy·ky 는 **안 건드린다** → 챙 폭·꼭대기·앞층 밑단(= 안경 가려짐)이 한 값도 안 움직인다.
# 배수의 하한은 계산값이다: 필요한 최소 x배수 = max_y(머리 현 + 여유) / (관 반폭) —
#   천모자 1.1057 · 중절모 1.1649 (아이콘 y 40.75 에서 최대). 여유를 얹어 아래 값을 쓴다.
CROWN_WIDEN = {"clothhat": 1.12, "fedora": 1.18}      # 관(B0)·그늘/띠(F1)·하이라이트(H3) 의 x 배수(몸 전용)
CROWN_WIDEN_SRC = ("B0", "F1", "H3")                  # 챙(B2)은 제외 — 챙을 넓히면 실루엣·팔 폭이 움직인다


def _head_pieces19(kind):
    src17 = [p for p in M17.WORN[kind] if not p.base]      # 불투명 바탕 조각 폐지(채움이 M/M2 불투명)
    # ★ 2026-09-06 — 왕관 R19 재맞춤(_refit_crown)을 폐기했다. 아래 CROWN_SHOW 문단이 근거다.
    #   여기서 다시 부르면 왕관이 눈 대역으로 되돌아간다(EYES 6종 가려짐 43~100%).
    kx = CROWN_WIDEN.get(kind)
    if kx:
        src17 = [_cp(p, pts=[(x * kx, y) for x, y in p.pts],
                     transform=(p.transform + " · " if p.transform else "") + "R24 관 x×%.2f(몸 전용)" % kx)
                 if p.src in CROWN_WIDEN_SRC else p for p in src17]
    crown_bottom = (min(q[1] for p in src17 if p.src == "B0" for q in p.pts)
                    if kind in ("clothhat", "fedora") else None)
    out = []
    for p in src17:
        cr = crole_of(kind, p.src)
        if kind in ("clothhat", "fedora") and p.src == "B2":
            far_f, far_a, near_f, near_a = _split_lens(p, y_cut=crown_bottom)
            out.append(_cp(p, pts=far_f, name=p.name + ".far", src=p.src + "far", layer="back", loop=True, filled=True,
                           line=None, crole=(cr[0], None), role="챙 먼 쪽(뒤층)",
                           note="[①] 관 밑변 위의 챙(먼 쪽) — 머리 뒤. 자른 선은 안 그린다(R24: 자르는 높이 = 관 밑변)"))
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

# ============================================================================
# ★★ 2026-09-06 — 왕관 R19 재맞춤(CROWN_SHOW / _refit_crown) **폐기**. 사용자 신고 대응.
# ============================================================================
# 신고 원문: "왕관착용시 머리 중간넘어서까지 착용이 되서 안경같은게 하나도 안보임 착용위치가 잘못됨".
#
# 무엇이 있었나 — R19 재맞춤은 "왕관 봉우리 사이 골(icon y=29)을 머리 꼭대기 아래 CROWN_SHOW(0.40 R)
# 까지 내려서 봉우리 사이로 머리가 보이게" 하려던 제약이었다. 그 제약 때문에 격자 탐색이
# R17 H-2 기본 맞춤(u=0.0570 · dy=+3.1200)을 버리고 **u=0.0630 · dy=+2.6000** 을 골랐고,
# 왕관 전체가 **0.8155 R 아래로** 내려갔다(밑단 +0.3127 R → **−0.5028 R**).
#
# 그 결과(design-equipment 실측, 프로덕션 좌표 래스터 · 안경 잉크 면적 대비 모자 채움 덮임률):
#   왕관 × 선글라스 99.8% · 동그란안경 99.9% · 고글 98.7% · 뿔테 91.0% · 안대 84.1% · 외알안경 80.1%
#   대조 — 같은 자로 잰 나머지 모자 5종의 최악값: 밀짚모자 78.4% · 털모자 71.3% · 베레모 56.3%
#          · 천모자 43.4% · 중절모 39.5%. 왕관만 유일하게 앞층 밑단이 음수(−0.5028 R)였다
#          (천모자 +0.1220 · 중절모 +0.1330 · 베레모 −0.1000 · 털모자 −0.1326 · 밀짚모자 −0.4000).
#   즉 "모자가 안경 위에 온다"(HEAD 정렬 10 > EYES 8)는 6종 공통 규칙이고 원인이 아니다 —
#   **좌표가 원인이다.** 층이 원인이라면 천모자도 안경을 지웠어야 하는데 그러지 않는다.
#
# 그리고 그 제약이 사려던 이득은 **같은 라운드에 이미 반증됐다**: 리더 판정 R19 —
# "봉우리 사이로 머리가 보이는 일은 기하적으로 없다(틈 36px 를 1pt 윤곽이 전부 먹는다,
# verify-change 픽셀 실측 자홍 0px)". 그때 결론이 「파라미터는 현행 유지」로 남으면서
# **이득 0 · 비용 = 안경 6종 전멸**인 상태가 그대로 출하됐다. 그 자리를 여기서 닫는다.
#
# 지금 — 왕관도 나머지 인계본 모자 3종과 **같은 H-2 기본 맞춤**(r17_model.HAT_FIT)을 쓴다.
#   u=0.0570 · ky=1.00 · dy=+3.1200 → 밑단 **+0.3127 R** · 꼭대기 **+2.4132 R** · 폭 2.337 R.
#   · 안경 가려짐 12.1 / 16.0 / 12.8 / 20.3 / 30.2 / 22.4 % — **모자 6종 중 가장 적게 가린다**
#     (얹는 물건이라는 왕관의 정체와 맞다).
#   · 머리 덮임: cover_top = HEAD_R_COVER 1.1842 R 이라 머리 꼭대기까지 그대로 덮인다
#     (골 +1.4670 R 이 머리 꼭대기 위에 있어 봉우리 사이로 머리가 새지 않는다 — 위 반증과 일치).
#   · H-2 **「앞층만의 합집합」**(§14-12-8 이 미확인으로 남겨 둔 자) 로 다시 재도 **통과**한다 —
#     착용선 +0.440 ≤ 목표 +0.45. 같은 자로 재면 천모자 +0.680 · 중절모 +0.776 은 **실패**이고
#     (§14-12-8 이 중절모에 대해 예측한 그대로. 천모자도 함께 실패한다는 것은 이번이 첫 실측),
#     털모자 −0.040 ≤ 0.00 통과 · 밀짚모자 −0.228 통과(단 그 통과의 정체는 v1 챙이 얼굴 앞
#     −0.400 R 까지 내려온 것이고, 그래서 밀짚모자가 안경을 78.4% 가린다) · 베레모는 덮임 실패.
#     → 천모자·중절모·베레모·밀짚모자의 좌표는 이번 라운드에서 **건드리지 않았다**(별건).
#   · 초상화 액자: 꼭대기 2.4132 R < TallestAccessoryAboveHeadCenterInR 2.551 R,
#     그리고 최고 아이템은 여전히 털모자(2.5437 R)라 액자 상수는 안 건드린다.
#   · 모자 6종 쌍별 실루엣 차: 왕관 쌍 4.26~6.40획(옛 2.55~3.90획), 전체 최소는 그대로
#     천모자↔중절모 1.69획 — 문턱 1.00획 통과.
#   · 규칙 1: 배율이 0.9048 배로 줄어 보석(CF3/CF4) 잉크 사각형이 0.2394 → 0.2166 R.
#     하한 1.5획(0.13845×1.5 = 0.2077 R) 위, 여유 +4.3%. **여기가 가장 빠듯한 자리다** —
#     왕관을 더 줄이는 변경은 이 값을 먼저 다시 재라.
#
# ★ 되살리지 마라. 되살리려면 (가) 봉우리 사이 머리 노출이 실제 빌드 캡처에서 0px 이 아님을
#   먼저 증명하고, (나) 안경 6종 가려짐을 나머지 모자 5종의 최악값(78.4%) 아래로 유지하는
#   u·dy 가 존재함을 함께 보여라. 지금 격자에는 그런 (u, dy) 가 없다 —
#   u=0.0630 을 유지한 채 밑단을 +0.3127 R 로 올리면 꼭대기가 2.6343 R 로 액자(2.551)를 넘는다.
# ============================================================================

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
    f = M17.HAT_FIT["crown"]
    w("   왕관: R17 기본 맞춤 그대로(R19 재맞춤 폐기) u %.4f(카드 ×%.2f) dy %+.3f · 꼭대기 %+.3f · 밑 %+.3f · 폭 %.2f R" % (
        f["u"], f["u"] / M17.ICON_U_HEAD, f["dy"], f["top"], f["bottom"], f["width"]))
    w("== 1-b. [R24] 관 x 배수 — 앞층만 H-2 (u·dy·ky 무변경 = 챙 폭·꼭대기·밑단 불변) ==")
    for k in ("clothhat", "fedora"):
        fh = M17.HAT_FIT[k]; fx, fy = M17.hat_to_R(k)
        kx = CROWN_WIDEN[k]
        b0 = [p for p in WORN[k] if p.src == "B0"][0]
        x0, y0, x1, y1 = rig.bounds(b0.pts)
        cb = y0                                  # 관 밑변(R 좌표는 y 위 → 최소 y) = 챙 분할선
        near = [p for p in WORN[k] if p.src == "B2near"][0]
        nx0, ny0, nx1, ny1 = rig.bounds(near.pts)
        chord = math.sqrt(max(0.0, HEAD_R_COVER ** 2 - cb ** 2))
        w("   %-9s 관 x×%.2f → 관 반폭 %.3f R (카드 %.3f) · 관 밑변 %+.3f R · 그 높이 머리 현 %.3f + 여유 %.2f = %.3f → %s"
          % (k, kx, x1, x1 / kx, cb, chord, M17.COVER_MARGIN, chord + M17.COVER_MARGIN,
             "덮는다" if x1 >= chord + M17.COVER_MARGIN else "★ 모자란다"))
        w("             챙 분할선 %+.3f R(= 관 밑변) · 가까운 쪽 챙 y[%+.3f,%+.3f] 반폭 %.3f — 관 밑변에서 이어받는다"
          % (cb, ny0, ny1, nx1))
        w("             챙 폭 %.3f R · 꼭대기 %+.3f · 앞층 밑단 %+.3f (셋 다 R19 값과 같다 — u·dy 를 안 건드렸다)"
          % (fh["width"], fh["top"], fh["bottom"]))
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
