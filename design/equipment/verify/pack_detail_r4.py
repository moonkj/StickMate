# -*- coding: utf-8 -*-
"""★ 팩 장비 12종 R4 — «중절모급 퀄리티» (사용자 지시 2026-09-08: "지금 구현되어 있는 중절모정도의 퀄리티는 되어야함").

R2/R3 와 같은 자를 그대로 쓴다 — PACKS/FILES 만 갈아 끼우고 R2.gate() + R3 5축을 부른다.
R4 가 새로 세운 자는 r4_gate.py 에 있고, **문턱을 발명하지 않고 출하 중절모를 재서 뽑았다**:

  R4-1 티끌 0        : 채움 조각의 「읽히는 색면」 >= 4.0 pt²   (중절모 최소 24.1pt²)
  R4-2 채움 <= 4     : 색면을 내는 조각 수                      (중절모 4 · 야구모자 4 · 왕관 9)
  R4-3 얇은판 규율   : 짧은 변 < 3획(6.00pt) 인 채움에 닫힌 윤곽 금지 — 잉크는 **열린 낱선**으로
                       (중절모 챙 2장 1.64/2.41pt · 띠 3.06pt 가 전부 noStroke 이고 tone4 열린 선 2줄이 가장자리를 그린다)
  R4-4 곡선 해상도   : 아이템의 곡선 변 평균 <= 0.22R (중절모 0.219R = 1.28pt)
  R4-5 4종 동시 착용 : 장치 해상도 연결성분 >= 2 · 머리 원반 가림 <= 80% (출하 4종 = 2개 / 70.2%)

★ 조형 문법으로 옮기면 「중절모 문법」 4줄:
  (가) **색면은 4개까지.** 다섯 번째 색면은 장식이 아니라 소음이다.
  (나) **얇은 것은 윤곽을 두르지 않는다.** 두르면 그 조각은 화면에서 «검은 점»이 된다
       (착용 배율에서 획 2.00pt, 머리 지름 11.63pt — 폭 4pt 짜리 보석의 재질색은 0.7pt² 만 남는다).
       잉크가 필요하면 **바깥으로 향하는 가장자리에만 열린 낱선**을 긋는다 = kind "edge".
  (다) **모티프는 키우거나 지운다.** 초승달·마름모를 «작게 얹는» 선택지는 이 배율에 없다.
  (라) **곡선은 중절모만큼 촘촘히.** 0.22R 보다 긴 변으로 곡선을 깎으면 면이 보인다.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pack_detail_r2 as R2
import pack_detail_r3 as R3
from pack_detail_r2 import (Piece, qbez, qbez_at, qbez_x_at_y, ellipse_arc, rrect, hexpad,
                            dedup, crescent, wedge, NOMINAL, SORT, ANCHOR, W075, GAP)
from pack_detail_r3 import ngon, lozenge, octa, arcslice, polylen

# ── R4 조각 문법 확장: kind="edge" — **중절모 챙의 잉크선 그대로**
#    (AccessoryShapeBuilder.Handoff.cs 의 Handoff_fedora_B2fa/B2na: tone 4 · loop 0 · filled 0 ·
#     strokeMult 1.0 · lineAlpha 1.0 · noStroke 0). R2 의 "seam"(×0.8 · α0.55)과 다르다 —
#    seam 은 «면 위의 접힘 자국»이고 edge 는 «실루엣을 지는 가장자리»라 굵기·불투명도가 다르다.
_r3_params = Piece.params
def _params(self, slot):
    if self.kind == "edge":
        n = NOMINAL[slot]
        return dict(strokeMult=1, strokeInR=n, noStroke=0, alpha=0, lineAlpha=1)
    return _r3_params(self, slot)
Piece.params = _params

_r3_init = Piece.__init__
def _init(self, name, pts, loop=True, filled=False, tone=0, kind="fill", layer=0, sway=None, host=None):
    if kind == "edge":
        self.name, self.pts, self.loop, self.filled, self.tone, self.kind, self.layer, self.sway = \
            name, [(float(x), float(y)) for x, y in pts], loop, filled, tone, kind, layer, sway
        assert tone == 4 and not filled and not loop, "edge 는 tone 4 · 열린 낱선이어야 한다(중절모 B2fa/B2na)"
        self.host = host
        return
    _r3_init(self, name, pts, loop, filled, tone, kind, layer, sway, host)
Piece.__init__ = _init

# ── ★ R2 게이트 축 1개 교체: 「카드 두 크기 최단변 >= 획」의 «최단변» 정의
#    R2/R3 는 `true_min_edge`(꺾임과 무관한 **모든** 변의 최솟값)를 썼다. 그 자를 **출하 인계본 12종**에
#    그대로 대면 **12종 전부가 58pt·24pt 양쪽에서 미달**로 찍힌다 — 중절모는 0.33pt vs 필요 1.994pt 로
#    6배 미달이다(계산: r4_cardedge.py). 즉 이 축은 「곡선을 촘촘히 그리는 것」을 구조적으로 금지한다:
#    매끄러운 곡선은 정의상 짧은 변이 많다. R2/R3 팩이 조각당 9.7점짜리 거친 다각형이 된 이유의 하나가
#    이 축일 가능성이 높다(미확인 — 당시 판단 기록은 남아 있지 않다).
#    R4 는 **프로덕션 린트와 같은 정의**로 되돌린다: 「양끝이 모두 45° 이상 꺾인 변」만 본다
#    (`rig.rule_one` = `AccessoryStrokeBudgetTests.DescribeRuleOneViolation`). 그 자로 재면
#    출하 인계본 12종이 **전부 통과**하고 최악값도 하한의 2.4배다.
#    ※ 원래 축이 잡으려던 것(FX/PET 의 «작은 다각형»)은 규칙 1-C(rho_max >= 펜)와
#      「채움 잉크 사각형 >= 1.5획」이 이미 잡는다 — 그 둘은 그대로 둔다.
def _corner_min_edge(pcs):
    m, who = 9e9, None
    for p in pcs:
        pts = p.pts; n = len(pts)
        if n < 3: continue
        corner = [False] * n
        rng = range(n) if p.loop else range(1, n - 1)
        for i in rng:
            import rig as _rig
            corner[i] = _rig.turn_deg(pts[(i - 1) % n], pts[i], pts[(i + 1) % n]) >= 45.0
        segs = n if p.loop else n - 1
        for i in range(segs):
            j = (i + 1) % n
            if not (corner[i] and corner[j]): continue
            L = math.hypot(pts[i][0] - pts[j][0], pts[i][1] - pts[j][1])
            if L < m: m, who = L, p.name
    return (m, who if who else "없음")
R2.true_min_edge = _corner_min_edge
R3.true_min_edge = _corner_min_edge

# ═════════════════════════════════════════════════════════════════════════════
#  사이버 pack.cyber
# ═════════════════════════════════════════════════════════════════════════════
def cyber_head():
    """Patched Hood.  R3 대비 — (1) 보석을 0.88×0.68 -> **1.24×1.08 R**(짧은 변 6.28pt >= 3획)로 키워
    닫힌 윤곽을 두르고도 재질색이 남게 했다(R3: 색면 4.6pt² -> R4: 22pt² 대). (2) 돔 곡선의 분할을 늘렸다."""
    dome = [(1.60, -0.36), (1.56, -0.02), (1.46, 0.40)]
    dome += qbez((1.46, 0.40), (1.34, 1.16), (0.58, 1.78), 13)
    dome += qbez((0.58, 1.78), (0.10, 2.10), (-0.62, 2.06), 10)[1:]
    dome += qbez((-0.62, 2.06), (-1.32, 1.86), (-1.58, 0.62), 15)[1:]
    dome += [(-1.66, 0.20), (-1.72, -0.20), (-1.74, -0.50), (-1.19, -0.44), (-1.19, 0.34),
             (-0.60, 0.34), (0.00, 0.34), (0.60, 0.34), (1.19, 0.34), (1.19, -0.30)]
    shell = dedup(dome)
    lining = [(-0.76, 0.36), (0.76, 0.36), (0.76, 0.90), (-0.76, 0.90)]
    gem = hexpad(1.06, 0.94, 0.62, 0.54)                                     # ★ R4-1/R4-3: 짧은 변 1.08R = 6.28pt
    i_hem_b = shell.index((-1.19, 0.34))
    trim = arcslice(shell, len(shell) - 1, i_hem_b)
    seam = [(0.00, 0.34)] + qbez((0.00, 0.34), (-0.20, 1.30), (-0.62, 2.06), 11)[1:]
    hi = [(-1.19, 0.34), (-1.02, 0.72), (-0.92, 1.10)]
    return [Piece("HoodShell", shell, True, True, 0, "fill"), Piece("HoodLining", lining, True, True, 2, "fillns"),
            Piece("HoodGem", gem, True, True, 1, "fill"), Piece("HoodSeam", seam, False, False, 4, "seam"),
            Piece("HoodTrim", trim, False, False, 3, "trim", host="HoodShell"), Piece("HoodHighlight", hi, False, False, 3, "hi", host="HoodShell")]

def cyber_eyes():
    """Slit Visor.  ★ R4 재치수 — R3 의 바는 **머리 원반의 83%를 덮었다**(출하 EYES 최대 52.0%).
    출하 EYES 4종은 전부 «채움 noStroke + 별도 테»(선글라스 B0/B0rim · 동그란안경 CB0glass/CB0) 문법이다 —
    그래야 렌즈가 2.4pt 두께여도 잉크에 먹히지 않는다. R4 는 그것을 그대로 가져온다.
    바를 **렌즈꼴**(중앙 0.60R · 양 끝은 점으로 수렴)로 바꿨다 — 직사각 바는 머리 원반의 가운데 띠를
    통째로 먹지만 렌즈꼴은 끝으로 갈수록 얇아져 같은 폭에서 가림이 12%p 낮다.
    단자 블록은 **1개**로 줄였다(2개면 암부 점유가 비트맵의 3.2배가 된다 — T-3)."""
    barT = qbez((1.58, 0.02), (0.0, 0.62), (-1.58, 0.02), 17)
    barB = qbez((-1.58, 0.02), (0.0, -0.58), (1.58, 0.02), 17)
    bar = dedup(barT + barB[1:])
    blkF = rrect(1.02, -0.28, 1.56, 0.28, 0.18, 3)
    gem = hexpad(0.80, -0.08, 0.78, 0.36)                       # 짧은 변 0.88R = 5.12pt >= 2획 -> 닫힌 윤곽 · y max +0.28 = E-대역 상한
    trim = arcslice(bar, 0, len(bar) - 1)
    strap = [(-1.58, 0.02)] + qbez((-1.58, 0.02), (-1.44, 0.52), (-0.80, 0.70), 6)[1:]
    ant = [(-1.58, 0.02), (-1.52, 0.70), (-1.10, 0.90)]
    hi = [bar[3], (0.70, 0.24), (0.20, 0.28)]
    return [Piece("VisorBar", bar, True, True, 0, "fillns"),
            Piece("VisorBlock", blkF, True, True, 2, "fillns"),
            Piece("VisorGem", gem, True, True, 1, "fill"),
            Piece("VisorStrap", strap, False, False, 0, "line", layer=1),
            Piece("VisorAntenna", ant, False, False, 0, "line"),
            Piece("VisorTrim", trim, False, False, 3, "trim", host="VisorBar"), Piece("VisorHighlight", hi, False, False, 3, "hi", host="VisorBar")]

def cyber_neck():
    """Cable Collar.  R3 대비 — (1) **CollarDrop 삭제**(채움 5 -> 4). 늘어진 줄 끝의 0.64R 네모는
    이 배율에서 «잉크 점»이고, 그 자리는 이미 CollarBezel 이 갖고 있다.
    (2) 보석을 0.88×0.68 -> 1.24×1.08 R 로 키웠다. (3) 띠 높이를 0.84 -> 1.06 R."""
    top = qbez((-1.06, -1.08), (0.0, -1.26), (1.06, -1.08), 15)
    bottom = qbez((1.10, -2.04), (0.0, -2.24), (-1.10, -2.04), 15)
    band = dedup(top + [(1.12, -1.56)] + bottom + [(-1.12, -1.56)])
    shade = dedup([(1.10, -1.52)] + bottom + [(-1.10, -1.52)])
    gem = hexpad(0.30, -1.94, 0.62, 0.54)
    trim = arcslice(band, 0, len(band) - 1)
    ic = band.index(bottom[13])
    cable = [band[ic]] + qbez(band[ic], (-1.16, -2.78), (-0.72, -3.18), 7)[1:]
    bezel = ngon(-0.72, -3.56, 0.58, 20)
    return [Piece("CollarBand", band, True, True, 0, "fill"), Piece("CollarShade", shade, True, True, 2, "fillns"),
            Piece("CollarGem", gem, True, True, 1, "fill"), Piece("CollarCable", cable, False, False, 0, "line"),
            Piece("CollarBezel", bezel, True, True, 0, "fill"),
            Piece("CollarTrim", trim, False, False, 3, "trim", host="CollarBand")]

def cyber_back():
    """Tarp Cape.  R3 대비 — 보석 확대 + 몸판 곡선 분할 증가(곡선 변 0.526 -> 0.22R 이하)."""
    front = qbez((1.10, -1.06), (1.30, -2.50), (1.26, -3.86), 16)
    hem = [(1.26, -3.86), (0.94, -4.34), (0.62, -3.74), (0.00, -4.38), (-0.62, -3.76), (-1.24, -4.36), (-1.86, -3.86)]
    back = qbez((-1.86, -3.86), (-1.78, -2.40), (-1.10, -1.06), 17)
    body = dedup([(-1.10, -1.06)] + front + hem[1:] + back[1:])
    sway = (body.index(hem[0]), len(hem))
    yoke = [(-1.06, -1.10), (1.06, -1.10), (1.22, -1.86), (-1.22, -1.86)]
    gem = hexpad(0.62, -1.30, 0.62, 0.54)
    trim = arcslice(body, 0, len(body) - 1)
    hi = [body[1], (1.02, -1.70), (1.06, -2.20)]
    return [Piece("CapeBody", body, True, True, 0, "fill", sway=sway), Piece("CapeYoke", yoke, True, True, 2, "fillns"),
            Piece("CapeGem", gem, True, True, 1, "fill"),
            Piece("CapeTrim", trim, False, False, 3, "trim", sway=sway, host="CapeBody"), Piece("CapeHighlight", hi, False, False, 3, "hi", host="CapeBody")]

# ═════════════════════════════════════════════════════════════════════════════
#  광부 pack.mine
# ═════════════════════════════════════════════════════════════════════════════
def mine_head():
    """Miner Helmet.  R3 대비 — ★ 이 아이템이 R4 의 대표 사례다.
    (1) **HelmetRimBack(색면 2.4pt²) · HelmetVent(그늘 통풍구) 삭제** — 채움 6 -> 4.
    (2) **챙을 중절모 챙으로 바꿨다**: 앞으로 2.24R 뻗고 두께 0.44R 인 얇은 판을 `fillns` 로 두고,
        잉크는 **바깥 가장자리 열린 낱선 하나**(kind edge)로만 긋는다. R3 의 닫힌 윤곽 챙은 색면이
        13.0 -> 2.8pt² 로 줄어 화면에서 «검은 막대»였다."""
    shell = ellipse_arc(0.0, 0.30, 1.44, 1.60, 180, 0, 27)
    crest = dedup(ellipse_arc(0.0, 0.30, 1.30, 1.46, 158, 22, 15) + list(reversed(ellipse_arc(0.0, 0.30, 0.84, 0.98, 158, 22, 15))))
    # 챙 — 자리는 R3 그대로(머리 폭 밖 x>=1.28: H-2b 밑단과 감쌈 판정을 건드리지 않는다)이고,
    # **처리만 중절모 챙 문법으로** 바꿨다: 채움은 윤곽 없이(fillns) 두고 잉크는 열린 낱선 한 줄.
    # tau = 0.54/1.00 = 0.540 <= 0.55 (광부 쐐기)
    brim = [(1.26, 1.04), (2.14, 0.76), (2.14, 0.16), (1.26, -0.06)]      # tau = 0.60/1.10 = 0.545 <= 0.55
    brim_edge = [brim[0], brim[1], brim[2], brim[3]]                # 위·앞·아래 = 바깥으로 향하는 가장자리 전부
    lamp = ngon(0.86, 1.22, 0.56, 20)
    ring = ngon(0.86, 1.22, 0.56, 20)
    trim = arcslice(shell, 17, 26)
    hi = [shell[8], (-0.58, 1.64), (-0.14, 1.78)]
    return [Piece("HelmetShell", shell, True, True, 0, "fill"),
            Piece("HelmetCrest", crest, True, True, 2, "fillns"),
            Piece("HelmetBrim", brim, True, True, 1, "fillns"), Piece("HelmetBrimEdge", brim_edge, False, False, 4, "edge", host="HelmetBrim"),
            Piece("HelmetLamp", lamp, True, True, 0, "fill"),
            Piece("HelmetLampRing", ring, True, False, 3, "trim", host="HelmetLamp"),
            Piece("HelmetTrimFront", trim, False, False, 3, "trim", host="HelmetShell"),
            Piece("HelmetHighlight", hi, False, False, 3, "hi", host="HelmetShell")]

def mine_eyes():
    """Dust Goggles.  ★ R4 재치수 — 렌즈 높이 1.30 -> **0.72R** · 렌즈 채움은 noStroke(출하 EYES 문법).
    ★ 보조색을 **얼굴 밖으로 옮겼다**: R4 중간안은 코받침 쐐기를 얼굴 한가운데 두었다가
      머리 원반 가림이 59.7% -> 76.0% 로 뛰었다(§4-5). 보조색 = 앞쪽 표시등 쐐기(|x| 0.81~1.55)."""
    lens = [(-1.16, 0.26), (1.16, 0.26), (1.40, 0.00), (1.00, -0.34), (0.34, -0.34),
            (0.00, -0.44), (-0.34, -0.34), (-1.00, -0.34), (-1.40, 0.00)]
    blkB = rrect(-1.56, -0.34, -0.94, 0.32, 0.20, 5)
    brow = [(-1.06, 0.44), (1.06, 0.44), (1.06, -0.10), (-1.06, -0.10)]   # 0.54R >= 1.5획 · 렌즈 위로 솟은 어두운 윗테     # 눈썹 그늘 — 암부(T-3) 분담
    ind = wedge((1.18, 0.28), (1.18, -0.56), 0.74, 0.40)                   # tau = 0.541 <= 0.55 · 짧은 변 0.74R = 4.30pt >= 2획 -> 닫힌 윤곽
    trim = arcslice(lens, 0, len(lens) - 1)
    strap = [lens[0]] + qbez(lens[0], (-1.14, 0.62), (-0.42, 0.76), 6)[1:]
    return [Piece("GoggleLens", lens, True, True, 0, "fill"),
            Piece("GoggleBrow", brow, True, True, 2, "fillns"), Piece("GoggleBlockBack", blkB, True, True, 2, "fillns"),
            Piece("GoggleIndicator", ind, True, True, 1, "fillns"),
            Piece("GoggleStrap", strap, False, False, 0, "line", layer=1),
            Piece("GoggleTrim", trim, False, False, 3, "trim", host="GoggleLens")]

def mine_neck():
    """Mine Lamp.  ★ R4 재구성 — R3 은 조각 8개 중 채움 6개였고 그중 4개가 4pt² 미만이었다.
    구조를 셋으로 줄인다: **넓은 멜빵 쐐기(보조색) + 어두운 램프 몸통(그늘) + 발광 렌즈(주색)**.
    R3 의 「띠 + 위판 + 걸쇠 2 + 몸통 + 렌즈」는 서로를 덮어 색면이 남지 않았다
    (R4a 중간 실측: 띠 색면 0.3pt² · 가림 83%). 덮이는 조각은 지우고 **덮는 조각을 키운다**."""
    keep = wedge((0.0, -1.44), (0.0, -2.92), 3.24, 1.72)        # tau = 0.531 <= 0.55 (광부 쐐기) · 멜빵이 어깨에서 허리로 좁아진다
    body = rrect(-0.86, -2.94, 0.86, -1.30, 0.24, 6)
    lens = ngon(0.0, -1.66, 0.58, 22)
    ring = ngon(0.0, -1.66, 0.58, 22)
    return [Piece("LampKeeper", keep, True, True, 1, "fillns"),
            Piece("LampBody", body, True, True, 2, "fill"), Piece("LampLens", lens, True, True, 0, "fill"),
            Piece("LampLensRing", ring, True, False, 3, "trim", host="LampLens")]

def mine_back():
    """Pick Harness.  R3 대비 — **날 2장 -> 1장(양날 한 조각)**: 채움 5 -> 4.
    R3 의 두 날은 각각 4점 쐐기라 모티프는 맞았지만 색면이 12.7/12.4 로 자루(23.2)와 경쟁했다."""
    p0, p1 = (0.72, -1.36), (-1.52, -4.40)
    ux, uy = p1[0]-p0[0], p1[1]-p0[1]; L = math.hypot(ux, uy); ux, uy = ux/L, uy/L
    nx, ny = -uy, ux
    hw = 0.30
    haft = [(p0[0]+nx*hw, p0[1]+ny*hw), (p1[0]+nx*hw, p1[1]+ny*hw), (p1[0]-nx*hw, p1[1]-ny*hw), (p0[0]-nx*hw, p0[1]-ny*hw)]
    ax = lambda t: (p0[0]+ux*L*t, p0[1]+uy*L*t)
    root = ax(0.88)
    ca, sa = math.cos(math.radians(34.0)), math.sin(math.radians(34.0))
    dAx, dAy = nx*ca + ux*sa, ny*ca + uy*sa
    dBx, dBy = -nx*ca + ux*sa, -ny*ca + uy*sa
    tipA = (root[0]+dAx*1.26, root[1]+dAy*1.26)
    tipB = (root[0]+dBx*1.26, root[1]+dBy*1.26)
    # 양날을 **한 조각**으로 — 자루를 관통하는 비대칭 쐐기(앞날 넓고 뒷날 좁다).
    # tau = 0.46/0.90 = 0.511 <= 0.55 (광부 쐐기) · rho_max ~ 0.23 >= 펜 0.2182 · 자루를 가로지르므로 규칙 4 거리 0
    head = wedge(tipB, tipA, 0.90, 0.46)
    block = [(root[0]+ux*0.54+nx*0.42, root[1]+uy*0.54+ny*0.42), (root[0]+ux*0.54-nx*0.42, root[1]+uy*0.54-ny*0.42),
             (root[0]-ux*0.54-nx*0.42, root[1]-uy*0.54-ny*0.42), (root[0]-ux*0.54+nx*0.42, root[1]-uy*0.54+ny*0.42)]
    pad = rrect(-0.12, -2.20, 0.96, -1.14, 0.24, 4)
    g1 = ax(0.56)
    grip1 = [(g1[0]+nx*0.34, g1[1]+ny*0.34), (g1[0]-nx*0.34, g1[1]-ny*0.34)]
    strapP = arcslice(pad, 0, len(pad) - 1)
    strapH = arcslice(haft, 0, 3)
    return [Piece("PickHaft", haft, True, True, 0, "fill"),
            Piece("PickHead", head, True, True, 1, "fill"),
            Piece("PickBlock", block, True, True, 2, "fill"), Piece("HarnessPad", pad, True, True, 0, "fill"),
            Piece("HaftGrip", grip1, False, False, 4, "seam"),
            Piece("HarnessStrap", strapP, False, False, 3, "trim", host="HarnessPad"), Piece("HaftTrim", strapH, False, False, 3, "trim", host="PickHaft")]

# ═════════════════════════════════════════════════════════════════════════════
#  대마법사 pack.arcane
# ═════════════════════════════════════════════════════════════════════════════
def arcane_head():
    """Wizard Hat.  R3 대비 — ★ **보석 2개 삭제**(색면 0.7 / 0.4 pt² — 둘 다 주색 위 주색이라 화면에는
    «잉크 마름모 두 개»만 남았다) + **초승달을 두껍게(0.47 -> 0.72R) 하고 fillns 로**.
    R3 초승달은 뿔 두께가 획(2.00pt)보다 얇았고 닫힌 윤곽이라 색면이 26.5 -> 4.4pt² 였다."""
    far = qbez((-2.20, 0.34), (0.0, 1.62), (2.20, 0.34), 23)
    brim = dedup(far + [(2.08, -0.06), (1.80, -0.30), (1.48, -0.04), (1.20, 0.34), (0.0, 0.34), (-1.20, 0.34), (-1.46, -0.02), (-1.76, -0.18), (-2.06, -0.02)])
    F = ((1.40, 0.315), (0.85, 1.55), (-0.78, 2.40)); B = ((-0.78, 2.40), (-1.25, 1.55), (-1.40, 0.315))
    cone = dedup(qbez(*F, 17) + qbez(*B, 15)[1:])
    band = [(1.34, 0.36), (qbez_x_at_y(*F, 0.90, True), 0.90), (qbez_x_at_y(*B, 0.90, False), 0.90), (-1.34, 0.36)]
    moon = crescent(-0.86, 1.34, 0.90, 0.62, 0.44, 205.0, 21)      # 최대 두께 0.90-(0.62-0.44) = 0.72R = 4.19pt
    trimB = arcslice(brim, 0, 22)
    trimC = arcslice(cone, 0, 16)
    return [Piece("HatBrim", brim, True, True, 0, "fill"), Piece("HatCone", cone, True, True, 0, "fill"),
            Piece("HatBand", band, True, True, 2, "fillns"), Piece("HatMoon", moon, True, True, 1, "fillns"),
            Piece("HatTrimBrim", trimB, False, False, 3, "trim", host="HatBrim"), Piece("HatTrimCone", trimC, False, False, 3, "trim", host="HatCone")]

def arcane_eyes():
    """Astro Lens.  ★ R4 재치수 — R3 의 원반은 반경 0.86R 이라 **머리 원반의 97%를 덮었다**(얼굴이 통째로 사라진다).
    0.86 -> **0.50R**: 두 눈(±0.341, +0.091)까지의 거리 0.345R 이라 «두 눈 커버» 계약은 그대로 만족한다.
    원반 채움은 윤곽 없이 두고(흰 베젤이 테를 진다 — 출하 동그란안경 CB0glass/CB0 와 같은 문법),
    **윤곽 있는 채움은 초승달 하나**가 진다(T-2 실루엣 둘레의 분모). LensPointTop(색면 0.7pt²) 삭제."""
    disc = ngon(0.0, 0.04, 0.56, 24)
    core = ngon(0.0, 0.04, 0.28, 16)
    cradle = crescent(0.0, -1.56, 0.62, 0.42, 0.26, 90.0, 21)   # 최대 두께 0.46R(rho 0.23 >= 펜) · 뿔 y=-1.03 → **머리 원반(y>=-1) 밖**
                                                                 # 원반까지 0.617R >= 1.5획 (규칙 4 «떨어짐» 쪽)
    bez = arcslice(disc, 0, len(disc) - 1)
    arm = [disc[8]] + qbez(disc[8], (-0.94, 0.46), (-1.22, 0.72), 6)[1:]
    return [Piece("LensDisc", disc, True, True, 0, "fill"), Piece("LensCore", core, True, True, 2, "fillns"),
            Piece("LensCradle", cradle, True, True, 1, "fillns"),
            Piece("LensArm", arm, False, False, 0, "line"), Piece("LensBezel", bez, False, False, 3, "trim", host="LensDisc")]

def arcane_neck():
    """Moon Clasp.  R3 대비 — ★ **좌우 판 2 + 안감 2 + 사슬 + 펜던트(6조각) -> 가로 판 1개**.
    R3 는 조각 10개 중 채움 7개였고 그중 4개가 4pt² 미만이었다. 판을 하나로 잇고 높이를
    0.64 -> 1.06R 로 키우면 닫힌 윤곽을 두르고도 색면이 남는다(중절모 관 B0 와 같은 처리)."""
    plate = [(-1.44, -1.18), (1.44, -1.18), (1.44, -2.24), (-1.44, -2.24)]
    moon = crescent(0.0, -1.66, 1.00, 0.70, 0.42, 90.0, 29)        # 최대 두께 0.72R = 4.19pt
    orb = ngon(0.0, -1.62, 0.54, 22)
    trim = arcslice(plate, 0, 3)
    return [Piece("ClaspPlate", plate, True, True, 0, "fill"),
            Piece("ClaspMoon", moon, True, True, 1, "fillns"), Piece("ClaspOrb", orb, True, True, 2, "fillns"),
            Piece("ClaspTrim", trim, False, False, 3, "trim", host="ClaspPlate")]

def arcane_back():
    """Moon Robe.  R3 대비 — **RobeGem 삭제**(색면 1.0pt²) · **요크 2장 -> 1장** · 칼라를 `fillns` + 열린 잉크선 ·
    초승달을 두껍게 + fillns · 몸판 곡선 분할 증가."""
    F = ((1.12, -1.20), (1.82, -2.60), (1.74, -4.20)); B = ((-1.74, -4.20), (-1.82, -2.60), (-1.12, -1.20))
    hem = [(1.74, -4.20), (1.22, -4.66), (0.74, -4.08), (0.00, -4.80), (-0.74, -4.08), (-1.22, -4.66), (-1.74, -4.20)]
    body = dedup([(-1.12, -1.20)] + qbez(*F, 17) + hem[1:] + qbez(*B, 17)[1:])
    sway = (body.index(hem[0]), len(hem))
    yoke = [(-0.86, -1.22), (0.86, -1.22), (1.10, -3.30), (-1.10, -3.30)]
    collar = [(-0.84, -1.44), (-0.70, -0.86), (0.70, -0.86), (0.84, -1.44)]
    collar_edge = [collar[0], collar[1], collar[2], collar[3]]
    moon = crescent(0.0, -3.62, 1.04, 0.74, 0.42, 270.0, 25)
    trim = arcslice(body, 0, len(body) - 1)
    return [Piece("RobeBody", body, True, True, 0, "fill", sway=sway), Piece("RobeYoke", yoke, True, True, 2, "fillns"),
            Piece("RobeCollar", collar, True, True, 0, "fillns"), Piece("RobeCollarEdge", collar_edge, False, False, 4, "edge", host="RobeCollar"),
            Piece("RobeMoon", moon, True, True, 1, "fillns"),
            Piece("RobeTrim", trim, False, False, 3, "trim", sway=sway, host="RobeBody")]

PACKS = [
    ("사이버 아포칼립스 pack.cyber (코호트 2)", "cyber", "사이버 아포칼립스", {
        "HEAD": ("Patched Hood", "equip.head.patchedhood", cyber_head), "EYES": ("Slit Visor", "equip.eyes.slitvisor", cyber_eyes),
        "NECK": ("Cable Collar", "equip.neck.cablecollar", cyber_neck), "BACK": ("Tarp Cape", "equip.shoulders.tarpcape", cyber_back)}),
    ("광부 pack.mine (코호트 7)", "mine", "광부", {
        "HEAD": ("Miner Helmet", "equip.head.minerhelmet", mine_head), "EYES": ("Dust Goggles", "equip.eyes.dustgoggles", mine_eyes),
        "NECK": ("Mine Lamp", "equip.neck.minelamp", mine_neck), "BACK": ("Pick Harness", "equip.shoulders.pickharness", mine_back)}),
    ("대마법사 pack.arcane (코호트 8)", "arcane", "대마법사", {
        "HEAD": ("Wizard Hat", "equip.head.wizardhat", arcane_head), "EYES": ("Astro Lens", "equip.eyes.astrolens", arcane_eyes),
        "NECK": ("Moon Clasp", "equip.neck.moonclasp", arcane_neck), "BACK": ("Moon Robe", "equip.shoulders.moonrobe", arcane_back)}),
]
FILES = dict(R2.FILES)
OUTDIR = "pack_detail_r4"

def emit(outdir=OUTDIR):
    import pack_detail_r3_extra as EX
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", outdir)
    os.makedirs(out, exist_ok=True)
    for packname, key, motif, PACK in PACKS:
        for slot in ("HEAD", "EYES", "NECK", "BACK"):
            disp, iid, fn = PACK[slot]
            path = os.path.join(out, FILES[(key, slot)] + ".wornShapes.yaml")
            open(path, "w", encoding="utf-8").write(EX.emit_yaml(fn(), slot))
            print("wrote", os.path.relpath(path))

def verify_emit(outdir=OUTDIR):
    """방출본을 **에셋 파서**(pack_assets.parse_asset — 생성기와 다른 경로)로 되읽어 설계 좌표와 대조."""
    import pack_assets as PA
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", outdir)
    bad = pieces = pts = 0
    for packname, key, motif, PACK in PACKS:
        for slot in ("HEAD", "EYES", "NECK", "BACK"):
            disp, iid, fn = PACK[slot]
            path = os.path.join(out, FILES[(key, slot)] + ".wornShapes.yaml")
            txt = "  displayName: %s\n  slot: %d\n" % (disp, {"HEAD": 0, "EYES": 1, "NECK": 2, "BACK": 3}[slot]) \
                  + open(path, encoding="utf-8").read() + "  wornGroupAlpha: 0\n"
            tmp = os.path.join(out, "_roundtrip.asset"); open(tmp, "w", encoding="utf-8").write(txt)
            _, _, shapes = PA.parse_asset(tmp); os.remove(tmp)
            design = fn()
            if [s["name"] for s in shapes] != [q.name for q in design]:
                bad += 1; print("  x 조각 이름/순서 불일치:", disp)
            for sh, q in zip(shapes, design):
                pieces += 1
                pr = q.params(slot)
                if (sh["loop"], sh["filled"], sh["tone"], sh["noStroke"], sh["layer"]) != (q.loop, q.filled, q.tone, bool(pr["noStroke"]), q.layer) \
                   or abs(sh["strokeInR"] - pr["strokeInR"]) > 1e-9 or abs(sh["strokeMult"] - pr["strokeMult"]) > 1e-9 \
                   or abs(sh["lineAlpha"] - pr["lineAlpha"]) > 1e-9:
                    bad += 1; print("  x 속성 불일치:", disp, q.name, sh, pr)
                if len(sh["pts"]) != len(q.pts): bad += 1; print("  x 점 수 불일치:", disp, q.name); continue
                for (x1, y1), (x2, y2) in zip(sh["pts"], q.pts):
                    pts += 1
                    if abs(x1 - x2) > 5e-5 or abs(y1 - y2) > 5e-5:
                        bad += 1; print("  x 좌표 불일치: %s %s (%.5f,%.5f) vs (%.5f,%.5f)" % (disp, q.name, x1, y1, x2, y2))
    print("왕복 검산: 조각 %d · 점 %d · 불일치 %d건 -> %s" % (pieces, pts, bad, "OK" if bad == 0 else "FAIL"))
    # 양성 대조 — 좌표 한 칸을 흔든 사본이 실제로 빨개지는가
    path = os.path.join(out, FILES[("cyber", "HEAD")] + ".wornShapes.yaml")
    txt = open(path, encoding="utf-8").read().replace("    - 1.6\n", "    - 1.61\n", 1)
    tmp = os.path.join(out, "_roundtrip.asset")
    open(tmp, "w", encoding="utf-8").write("  displayName: x\n  slot: 0\n" + txt + "  wornGroupAlpha: 0\n")
    _, _, shapes = PA.parse_asset(tmp); os.remove(tmp)
    d = cyber_head()
    moved = any(abs(a - b) > 5e-5 for (a, _), (b, _) in zip(shapes[0]["pts"], d[0].pts))
    print("왕복 양성 대조(좌표 한 칸 +0.01): %s" % ("잡는다" if moved else "★ 못 잡는다 — 위 OK 를 믿지 마라"))
    return bad == 0 and moved

if __name__ == "__main__":
    R2.PACKS = PACKS
    R2.FILES = FILES
    if "--emit" in sys.argv: emit(); sys.exit(0)
    if "--verify-emit" in sys.argv: sys.exit(0 if verify_emit() else 1)
    import pack_detail_r3_extra as EX
    EX.main(PACKS, FILES)
