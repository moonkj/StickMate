# -*- coding: utf-8 -*-
"""★ 팩 장비 12종 디테일 고도화 R2 — 사이버(2) · 광부(7) · 대마법사(8) 각 HEAD/EYES/NECK/BACK.

  python3 pack_detail_r2.py               # 전수 게이트 (종료코드 0 = 위반 0건)
  python3 pack_detail_r2.py --control     # ★ 양성 대조 — 일부러 나쁜 값 5종을 넣어 빨간불이 켜지는가
  python3 pack_detail_r2.py --dump        # 좌표 전문 (docs 부록)
  python3 pack_detail_r2.py --emit        # design/equipment/pack_detail_r2/*.wornShapes.yaml (coder 가 .asset 에 그대로 붙인다)
  python3 pack_detail_r2.py --refresh-prod  # 프로덕션 좌표 캐시를 Tools/ShapeDump/build.sh 로 다시 뽑는다

좌표계: 머리 중심 원점 · R 배수 · +x = 진행 방향. 계약 v2 조각(strokeInR > 0) 문법은 AccessoryShapeBuilder.Handoff.cs
의 인계본 조각과 같다 — 채움(잉크 윤곽) · 그늘 채움(tone 2, noStroke) · 잉크 낱선(tone 4, ×0.8, α0.55) ·
하이라이트(tone 3, ×0.75, α0.42) · 뒤층(layer 1 = 머리 뒤).
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import rig, sectors as S
from rig import Shape
import pack78_shapes as P78
import r24_hats as R24
import pack_assets as PA
import legibility as LG

W075, W060, W100 = rig.stroke_in_R(0.75), rig.stroke_in_R(0.60), rig.stroke_in_R(1.00)
GAP = 1.5 * W075                      # 규칙 4 — 조각 쌍은 닿거나(0) 1.5획 이상 떨어진다 (0.51580 R)
RATCHET = S.SILHOUETTE_RATCHET_R
PEN = 0.2181818                       # 규칙 1-C 채움 윤곽 펜
NOMINAL = {"HEAD": 0.1384, "EYES": 0.1153, "NECK": 0.1297, "BACK": 0.1243}   # 출하 팩 에셋의 슬롯 명목 획(R)
SORT = {"HEAD": 10, "EYES": 8, "NECK": 7, "BACK": -1}
NECKY = rig.SHOULDER_R + 0.04         # AccessorySilhouetteMetrics.AnchorLocalY = NeckLocalY
ANCHOR = {"HEAD": 0.0, "EYES": 0.0, "NECK": NECKY, "BACK": NECKY}
V_STROKE_075 = 1.0 / (0.22 * 0.75 * rig.PT_PER_UNIT)     # v2 획 실폭 @0.75 = 1pt 하한 = 0.17195 R

# ─────────────────────────────────────────────────────────────────────────────
class Piece:
    def __init__(self, name, pts, loop=True, filled=False, tone=0, kind="fill", layer=0, sway=None):
        """kind: fill(잉크 윤곽 채움) · fillns(윤곽 없는 채움 — 그늘띠·빛띠·틴트) · line(재질 낱선) · seam(tone4 ×0.8 α0.55) · hi(tone3 ×0.75 α0.42)"""
        self.name, self.pts, self.loop, self.filled, self.tone, self.kind, self.layer, self.sway = \
            name, [(float(x), float(y)) for x, y in pts], loop, filled, tone, kind, layer, sway
        assert kind in ("fill", "fillns", "line", "seam", "hi")
        if kind == "fillns": assert filled
        if kind == "seam": assert not filled and tone == 4
        if kind == "hi": assert not filled and tone == 3
    @property
    def noStroke(self): return self.kind == "fillns"
    def params(self, slot):
        n = NOMINAL[slot]
        if self.kind == "hi":    return dict(strokeMult=0.75, strokeInR=round(n*0.75, 5), noStroke=0, alpha=0, lineAlpha=0.42)
        if self.kind == "seam":  return dict(strokeMult=0.8,  strokeInR=round(n*0.8, 5),  noStroke=0, alpha=0, lineAlpha=0.55)
        if self.kind == "fillns":return dict(strokeMult=1,    strokeInR=n,                noStroke=1, alpha=1, lineAlpha=0)
        if self.kind == "line":  return dict(strokeMult=1,    strokeInR=n,                noStroke=0, alpha=1, lineAlpha=1)
        return dict(strokeMult=1, strokeInR=n, noStroke=0, alpha=1, lineAlpha=1)
    def to_shape(self, slot):
        return Shape(self.name, self.pts, self.loop, self.filled, self.tone, (-1 if self.layer == 1 else SORT[slot]))
    def to_dict(self):
        return dict(name=self.name, pts=self.pts, loop=self.loop, filled=self.filled, tone=self.tone,
                    noStroke=self.noStroke, layer=self.layer, kind=self.kind)

# ---- 기하 도우미 ----------------------------------------------------------
def qbez(p0, c, p1, n=9):
    return [qbez_at(p0, c, p1, i / (n - 1)) for i in range(n)]

def qbez_at(p0, c, p1, t):
    return ((1-t)**2*p0[0] + 2*(1-t)*t*c[0] + t*t*p1[0], (1-t)**2*p0[1] + 2*(1-t)*t*c[1] + t*t*p1[1])

def qbez_x_at_y(p0, c, p1, y, rising=True):
    """2차 곡선에서 y 가 주어진 값이 되는 점의 x (곡선 위 y 가 t 에 대해 단조인 구간 전제)."""
    lo, hi = 0.0, 1.0
    for _ in range(50):
        m = (lo + hi) / 2
        ym = qbez_at(p0, c, p1, m)[1]
        if (ym < y) == rising: lo = m
        else: hi = m
    return qbez_at(p0, c, p1, lo)[0]

def ellipse_arc(cx, cy, rx, ry, d0, d1, n):
    return [(cx + rx*math.cos(math.radians(d0 + (d1-d0)*i/(n-1))), cy + ry*math.sin(math.radians(d0 + (d1-d0)*i/(n-1)))) for i in range(n)]

def rrect(x0, y0, x1, y1, r, n=2):
    """모서리 반지름 r 인 둥근 사각형 (반시계). 모서리마다 n+1 점 — n=2 면 변 길이 2r·sin(22.5°)=0.765r (카드 24pt 획 이상이 되게 r>=0.20)."""
    pts = []
    pts += ellipse_arc(x1-r, y0+r, r, r, 270, 360, n+1)
    pts += ellipse_arc(x1-r, y1-r, r, r, 0, 90, n+1)
    pts += ellipse_arc(x0+r, y1-r, r, r, 90, 180, n+1)
    pts += ellipse_arc(x0+r, y0+r, r, r, 180, 270, n+1)
    return pts

def hexpad(cx, cy, hw, hh):
    return [(cx-hw, cy), (cx-hw/2, cy+hh), (cx+hw/2, cy+hh), (cx+hw, cy), (cx+hw/2, cy-hh), (cx-hw/2, cy-hh)]

def dedup(pts, eps=1e-6):
    out = []
    for p in pts:
        if not out or math.hypot(p[0]-out[-1][0], p[1]-out[-1][1]) > eps: out.append(p)
    if len(out) > 1 and math.hypot(out[0][0]-out[-1][0], out[0][1]-out[-1][1]) <= eps: out.pop()
    return out

crescent, wedge = P78.crescent, P78.wedge

# ═════════════════════════════════════════════════════════════════════════════
#  사이버 아포칼립스 pack.cyber (코호트 2) — 각진 금속 · 보조색 = 커넥터 패드/빛띠(6각, 볼록)
# ═════════════════════════════════════════════════════════════════════════════
def cyber_head():
    """Patched Hood — 후드. 뒤로 기운 꼭대기 · 얼굴 양옆으로 내려오는 옆자락(머리 폭 밖 |x|>=1.19) · 이마 위 안감 그늘 ·
    뒷면 커넥터 패드(윤곽에 걸침) · 이마에서 꼭대기로 가는 테이프 솔기 · 뒷자락 테이프 · 앞면 하이라이트.
    H-2b: 앞층 채움 밑단은 |x|<=1.184 안에서 +0.30 — 옆자락만 그 밖에서 내려간다."""
    dome = [(1.60, -0.50), (1.56, -0.05), (1.46, 0.40)]
    dome += qbez((1.46, 0.40), (1.34, 1.16), (0.58, 1.78), 7)
    dome += qbez((0.58, 1.78), (0.10, 2.10), (-0.62, 2.06), 6)[1:]
    dome += qbez((-0.62, 2.06), (-1.32, 1.86), (-1.58, 0.62), 8)[1:]
    dome += [(-1.66, 0.20), (-1.72, -0.28), (-1.74, -0.66), (-1.19, -0.60), (-1.19, 0.30),
             (-0.60, 0.30), (0.00, 0.30), (0.60, 0.30), (1.19, 0.30), (1.19, -0.42)]
    shell = dedup(dome)
    v1 = qbez_at((1.46, 0.40), (1.34, 1.16), (0.58, 1.78), 1/6)          # 돔 앞 곡선의 꼭짓점 하나(정확히 윤곽 위)
    lining = [(-1.19, 0.30), (1.19, 0.30), (1.19, 0.82), (-1.19, 0.82)]
    pad = hexpad(-1.28, 1.30, 0.40, 0.30)
    seamA = qbez((0.10, 0.30), (-0.05, 1.30), (-0.62, 2.06), 7)
    seamB = [(-1.19, -0.30), (-1.71, -0.36)]
    hi = [v1, (1.16, 0.80), (0.96, 0.88), (0.80, 0.92)]
    return [Piece("HoodShell", shell, True, True, 0, "fill"), Piece("HoodLining", lining, True, True, 2, "fillns"),
            Piece("HoodPad", pad, True, True, 1, "fill"), Piece("HoodSeam", seamA, False, False, 4, "seam"),
            Piece("HoodTape", seamB, False, False, 4, "seam"), Piece("HoodHighlight", hi, False, False, 3, "hi")]

def cyber_eyes():
    """Slit Visor — 한 줄 빛의 판. 양끝 단자 블록(잉크 솔기로 분리) · 안쪽 빛띠(보조색, 윤곽 없음, E-대역 y<=+0.28) ·
    머리 뒤로 도는 끈(뒤층, 뒤 솔기에서 출발) · 앞 솔기에서 출발하는 하이라이트."""
    top = qbez((1.38, 0.42), (0.0, 0.50), (-1.40, 0.42), 7)
    bot = qbez((-1.40, -0.36), (0.0, -0.42), (1.38, -0.36), 7)
    bar = dedup([(1.56, 0.32)] + top + [(-1.58, 0.32), (-1.58, -0.26)] + bot + [(1.56, -0.26)])
    glow = [(-1.16, 0.02), (-0.86, -0.24), (0.84, -0.24), (1.14, 0.02), (0.84, 0.28), (-0.86, 0.28)]
    seamF = [(1.20, 0.46), (1.20, -0.40)]
    seamB = [(-1.22, 0.46), (-1.22, -0.40)]
    strap = qbez((-1.22, 0.36), (-1.34, 0.62), (-0.60, 0.74), 5)
    hi = [(1.20, 0.44), (0.80, 0.30), (0.40, 0.27), (0.10, 0.26)]
    return [Piece("VisorBar", bar, True, True, 0, "fill"), Piece("VisorGlow", glow, True, True, 1, "fillns"),
            Piece("VisorSeamFront", seamF, False, False, 4, "seam"), Piece("VisorSeamBack", seamB, False, False, 4, "seam"),
            Piece("VisorStrap", strap, False, False, 0, "line", layer=1), Piece("VisorHighlight", hi, False, False, 3, "hi")]

def cyber_neck():
    """Cable Collar — 케이블 다발 칼라. 윗변 3코일 · 아랫단 그늘 · 목 앞 커넥터 패드(보조색, 밑변에 걸침) · 뒤로 늘어진 케이블 + 플러그 · 앞면 하이라이트."""
    top = [(-1.02, -1.18), (-0.68, -1.02), (-0.34, -1.18), (0.00, -1.02), (0.34, -1.18), (0.68, -1.02), (1.02, -1.18)]
    bottom = qbez((1.10, -1.90), (0.0, -2.06), (-1.10, -1.90), 7)
    band = dedup(top + [(1.12, -1.52)] + bottom + [(-1.12, -1.52)])
    shade = dedup([(1.10, -1.42)] + bottom + [(-1.10, -1.42)])
    pad = hexpad(0.30, -1.98, 0.38, 0.30)
    cs = qbez_at((1.10, -1.90), (0.0, -2.06), (-1.10, -1.90), 0.86)
    cable = qbez((cs[0], cs[1] + 0.05), (-1.24, -2.62), (-0.72, -3.10), 7)
    plug = [(-1.00, -3.06), (-0.46, -3.06), (-0.46, -3.60), (-1.00, -3.60)]
    hi = [(0.68, -1.02), (0.92, -1.30), (1.116, -1.60)]
    return [Piece("CollarBand", band, True, True, 0, "fill"), Piece("CollarShade", shade, True, True, 2, "fillns"),
            Piece("CollarPad", pad, True, True, 1, "fill"), Piece("CollarCable", cable, False, False, 0, "line"),
            Piece("CollarPlug", plug, True, True, 0, "fill"), Piece("CollarHighlight", hi, False, False, 3, "hi")]

def cyber_back():
    """Tarp Cape — 방수포 망토. 앞변은 곧고 뒷변만 크게 벌어지는 비대칭 · 찢긴 지그재그 밑단 · 어깨 요크 그늘 ·
    앞어깨 고정판(보조색, 윗변·앞변에 걸침) · 주름 2줄(윗변/패드에서 밑단 홈으로) · 뒷면 하이라이트."""
    front = qbez((1.10, -1.06), (1.28, -2.50), (1.24, -3.94), 6)
    hem = [(1.24, -3.94), (0.92, -4.10), (0.62, -3.70), (0.26, -4.14), (-0.16, -4.02), (-0.56, -3.62), (-0.96, -4.16), (-1.44, -4.10), (-1.86, -3.98)]
    back = qbez((-1.86, -3.98), (-1.74, -2.40), (-1.10, -1.06), 7)
    body = dedup([(-1.10, -1.06)] + front + hem[1:] + back[1:])
    sway = (body.index(hem[0]), len(hem))
    yoke = [(-1.10, -1.06), (1.10, -1.06), (1.22, -1.58), (-1.22, -1.58)]
    pad = hexpad(0.72, -1.20, 0.40, 0.30)
    foldB = qbez((-0.42, -1.06), (-0.80, -2.40), (-0.56, -3.62), 7)
    foldF = qbez((0.72, -1.50), (0.72, -2.60), (0.62, -3.70), 7)
    hs = qbez_at((-1.86, -3.98), (-1.74, -2.40), (-1.10, -1.06), 0.5)
    hi = [hs, (-1.40, -2.90), (-1.44, -3.40)]
    return [Piece("CapeBody", body, True, True, 0, "fill", sway=sway), Piece("CapeYoke", yoke, True, True, 2, "fillns"),
            Piece("CapePad", pad, True, True, 1, "fill"), Piece("CapeFoldBack", foldB, False, False, 4, "seam"),
            Piece("CapeFoldFront", foldF, False, False, 4, "seam"), Piece("CapeHighlight", hi, False, False, 3, "hi")]

# ═════════════════════════════════════════════════════════════════════════════
#  광부 pack.mine (코호트 7) — 쐐기 · 보조색 = 4각 쐐기(tau <= 0.55)
# ═════════════════════════════════════════════════════════════════════════════
def mine_head():
    """Miner Helmet — 안전모. 타원 돔 · 앞 쐐기 챙(보조색) · 뒤로 도는 뒤챙(뒤층, 쐐기) · 헤드램프(어두운 렌즈) ·
    능선(뒤챙 안에서 출발해 램프 밑으로 들어간다) · 정수리 뒤 하이라이트(윤곽 꼭짓점에서 능선까지)."""
    shell = ellipse_arc(0.0, 0.30, 1.46, 1.22, 180, 0, 21)          # 꼭짓점 각도 180, 171, ..., 0
    brim = [(1.28, 0.90), (2.10, 0.70), (2.10, 0.22), (1.28, -0.02)]
    rim = [(-1.30, 0.76), (-1.96, 0.54), (-1.96, 0.18), (-1.30, 0.02)]
    lamp = [(0.98, 0.84), (1.60, 0.84), (1.60, 1.44), (0.98, 1.44)]
    lens = rig.poly(1.29, 1.14, 0.27, 12)                            # 사각 등체 안의 둥근 렌즈 — 「상자」가 아니라 「램프」로 읽히게
    rib = [shell[1], (-1.10, 0.98), (-0.60, 1.24), (-0.05, 1.32), (0.50, 1.22), (1.00, 1.00), (1.30, 0.88)]
    hi = [shell[7], (-0.40, 1.42), (-0.05, 1.32)]
    return [Piece("HelmetShell", shell, True, True, 0, "fill"), Piece("HelmetRimBack", rim, True, True, 1, "fill", layer=1),
            Piece("HelmetBrim", brim, True, True, 1, "fill"), Piece("HelmetRib", rib, False, False, 4, "seam"),
            Piece("HelmetLamp", lamp, True, True, 0, "fill"), Piece("HelmetLens", lens, True, True, 2, "fillns"),
            Piece("HelmetHighlight", hi, False, False, 3, "hi")]

def mine_eyes():
    """Dust Goggles — 분진 고글. 둥근 마스크 판 · 어두운 두 유리(윤곽 없는 그늘) · 코 쐐기(보조색, E-대역) · 가운데 잉크 솔기 · 뒤층 끈 · 윗변 하이라이트."""
    frame = rrect(-1.38, -0.34, 1.38, 0.46, 0.22, 2)
    paneF = rrect(0.20, -0.20, 0.92, 0.36, 0.20, 2)
    paneB = rrect(-0.92, -0.20, -0.20, 0.36, 0.20, 2)
    pad = [(-0.58, -0.26), (0.58, -0.26), (0.30, -0.92), (-0.30, -0.92)]
    seam = [(0.0, 0.46), (0.0, -0.34)]
    strap = qbez((-1.34, 0.10), (-1.22, 0.50), (-0.78, 0.66), 5)
    hi = [(0.90, 0.46), (0.50, 0.40), (0.0, 0.36)]
    return [Piece("GoggleFrame", frame, True, True, 0, "fill"), Piece("GogglePaneFront", paneF, True, True, 2, "fillns"),
            Piece("GogglePaneBack", paneB, True, True, 2, "fillns"), Piece("GoggleNosePad", pad, True, True, 1, "fill"),
            Piece("GoggleSeam", seam, False, False, 4, "seam"), Piece("GoggleStrap", strap, False, False, 0, "line", layer=1),
            Piece("GoggleHighlight", hi, False, False, 3, "hi")]

def mine_neck():
    """Mine Lamp — 가슴 안전등. 곡선 멜빵(V) · 쐐기 갓(보조색) · 둥근 등체 · 어두운 렌즈(윤곽 없음) · 아래로 퍼지는 빛(하이라이트 V) · 갓 하이라이트."""
    strap = dedup(qbez((-0.92, -1.22), (-0.60, -1.60), (-0.26, -1.78), 5) + [(0.26, -1.78)] + qbez((0.26, -1.78), (0.60, -1.60), (0.92, -1.22), 5))
    shade = [(-0.78, -1.78), (0.78, -1.78), (0.40, -2.46), (-0.40, -2.46)]
    body = rrect(-0.52, -3.24, 0.52, -2.46, 0.20, 2)
    lens = rig.poly(0.0, -2.85, 0.30, 12)
    beam = [(-0.62, -3.90), (0.0, -3.24), (0.62, -3.90)]
    hi = [(-0.26, -1.78), (0.05, -2.10), (0.30, -2.46)]
    return [Piece("LampStrap", strap, False, False, 0, "line"), Piece("LampShade", shade, True, True, 1, "fill"),
            Piece("LampBody", body, True, True, 0, "fill"), Piece("LampLens", lens, True, True, 2, "fillns"),
            Piece("LampBeam", beam, False, False, 3, "hi"), Piece("LampHighlight", hi, False, False, 3, "hi")]

def mine_back():
    """Pick Harness — 곡괭이 멜빵. 두께 있는 자루 · 쐐기 날(보조색) + 뭉툭한 뒷머리 · 둥근 어깨패드 · 곡선 끈(첫 감기에 닿는다) · 손잡이 감기 2줄 · 날등 하이라이트."""
    p0, p1 = (0.72, -1.36), (-1.52, -4.40)
    ux, uy = p1[0]-p0[0], p1[1]-p0[1]; L = math.hypot(ux, uy); ux, uy = ux/L, uy/L
    nx, ny = -uy, ux            # (0.805, -0.593) — 앞·아래
    hw = 0.24
    haft = [(p0[0]+nx*hw, p0[1]+ny*hw), (p1[0]+nx*hw, p1[1]+ny*hw), (p1[0]-nx*hw, p1[1]-ny*hw), (p0[0]-nx*hw, p0[1]-ny*hw)]
    ax = lambda t: (p0[0]+ux*L*t, p0[1]+uy*L*t)
    root = ax(0.84); tip = (root[0]+nx*1.30, root[1]+ny*1.30)
    head = wedge(root, tip, 1.10, 0.44)
    pr = (root[0]-nx*0.52, root[1]-ny*0.52)
    poll = [(root[0]+ux*0.55, root[1]+uy*0.55), (pr[0]+ux*0.55, pr[1]+uy*0.55), (pr[0]-ux*0.55, pr[1]-uy*0.55), (root[0]-ux*0.55, root[1]-uy*0.55)]
    pad = rrect(0.10, -2.10, 0.96, -1.18, 0.22, 2)
    g1, g2 = ax(0.38), ax(0.52)
    grip1 = [(g1[0]+nx*0.33, g1[1]+ny*0.33), (g1[0]-nx*0.33, g1[1]-ny*0.33)]
    grip2 = [(g2[0]+nx*0.33, g2[1]+ny*0.33), (g2[0]-nx*0.33, g2[1]-ny*0.33)]
    send = (g1[0]+nx*hw, g1[1]+ny*hw)
    strap = qbez((0.40, -2.10), (0.10, -2.40), send, 4)
    a, b = (tip[0]+ux*0.22, tip[1]+uy*0.22), (root[0]+ux*0.55, root[1]+uy*0.55)
    mid = ((a[0]+b[0])/2 - ux*0.07, (a[1]+b[1])/2 - uy*0.07)
    hi = [a, mid, b]
    return [Piece("PickHaft", haft, True, True, 0, "fill"), Piece("PickHead", head, True, True, 1, "fill"),
            Piece("PickPoll", poll, True, True, 0, "fill"), Piece("HarnessPad", pad, True, True, 0, "fill"),
            Piece("HarnessStrap", strap, False, False, 0, "line"), Piece("HaftGrip1", grip1, False, False, 4, "seam"),
            Piece("HaftGrip2", grip2, False, False, 4, "seam"), Piece("PickHighlight", hi, False, False, 3, "hi")]

# ═════════════════════════════════════════════════════════════════════════════
#  대마법사 pack.arcane (코호트 8) — 초승달 · 보조색 = 초승달(뿔 2, 결손 >= 0.15, n >= 12)
# ═════════════════════════════════════════════════════════════════════════════
def arcane_head():
    """Wizard Hat — 뾰족 모자. 렌즈꼴 넓은 챙(양끝 처짐) · 뒤로 휘어 늘어진 원뿔 · 밑단 띠 그늘 · 앞 가장자리에 걸린 초승달 브로치(보조색) · 주름선 · 뒷면 하이라이트."""
    far = qbez((-2.20, 0.34), (0.0, 1.62), (2.20, 0.34), 11)
    brim = dedup(far + [(2.08, -0.06), (1.80, -0.30), (1.48, -0.04), (1.20, 0.30), (0.0, 0.30), (-1.20, 0.30), (-1.46, -0.02), (-1.76, -0.18), (-2.06, -0.02)])
    F = ((1.40, 0.315), (0.85, 1.55), (-0.78, 2.40)); B = ((-0.78, 2.40), (-1.25, 1.55), (-1.40, 0.315))
    cone = dedup(qbez(*F, 10) + qbez(*B, 9)[1:])
    band = [(1.40, 0.315), (qbez_x_at_y(*F, 0.835, True), 0.835), (qbez_x_at_y(*B, 0.835, False), 0.835), (-1.40, 0.315)]
    moon = crescent(1.02, 1.06, 0.70, 0.53, 0.28, 15.0)
    crease = qbez((-0.40, 0.315), (-0.62, 1.30), (-0.78, 2.40), 7)
    hi = [qbez_at(*B, 0.55), (-0.90, 1.58), crease[4]]
    return [Piece("HatBrim", brim, True, True, 0, "fill"), Piece("HatCone", cone, True, True, 0, "fill"),
            Piece("HatBand", band, True, True, 2, "fillns"), Piece("HatMoon", moon, True, True, 1, "fill"),
            Piece("HatCrease", crease, False, False, 4, "seam"), Piece("HatHighlight", hi, False, False, 3, "hi")]

def arcane_eyes():
    """Astro Lens — 관측 렌즈. 렌즈 2(12각, 간격 0.56R) · 어두운 틴트 2(윤곽 없음) · 초승달 받침(보조색, y<=+0.28) · 브릿지 · 곡선 다리."""
    lf = rig.poly(0.68, 0.02, 0.40, 12); lb = rig.poly(-0.68, 0.02, 0.40, 12)
    tf = rig.poly(0.68, 0.02, 0.30, 12); tb = rig.poly(-0.68, 0.02, 0.30, 12)
    cradle = crescent(0.0, -1.00, 1.00, 0.82, 0.30, 90.0)
    bridge = [(0.30, 0.02), (-0.30, 0.02)]
    arm = qbez((-1.00, 0.20), (-1.36, 0.36), (-1.56, 0.74), 5)
    return [Piece("LensFront", lf, True, True, 0, "fill"), Piece("LensBack", lb, True, True, 0, "fill"),
            Piece("LensTintFront", tf, True, True, 2, "fillns"), Piece("LensTintBack", tb, True, True, 2, "fillns"),
            Piece("LensCradle", cradle, True, True, 1, "fill"), Piece("LensBridge", bridge, False, False, 0, "line"),
            Piece("LensArm", arm, False, False, 0, "line")]

def arcane_neck():
    """Moon Clasp — 초승달 걸쇠. 곡선 윗변 + 두 옷깃 꼭지의 칼라 · 안감 그늘 · 가운데 초승달 브로치(보조색) · 접힘선 2 · 하이라이트."""
    T = ((-1.16, -1.24), (0.0, -1.02), (1.16, -1.24))
    top = qbez(*T, 7)
    collar = dedup(top + [(1.26, -1.70), (0.70, -2.16), (0.0, -1.84), (-0.70, -2.16), (-1.26, -1.70)])
    lining = dedup(top + [(x, y-0.52) for x, y in reversed(top)])
    moon = crescent(0.0, -2.36, 0.74, 0.56, 0.30, 270.0)
    yR = qbez_at(*T, (0.70+1.16)/2.32)[1]; yL = qbez_at(*T, (-0.70+1.16)/2.32)[1]
    seamR = [(0.70, yR), (0.70, -2.16)]; seamL = [(-0.70, yL), (-0.70, -2.16)]
    hi = [(-0.70, yL), (-0.98, -1.38), (-1.243, -1.62)]
    return [Piece("ClaspCollar", collar, True, True, 0, "fill"), Piece("ClaspLining", lining, True, True, 2, "fillns"),
            Piece("ClaspMoon", moon, True, True, 1, "fill"), Piece("ClaspSeamLeft", seamL, False, False, 4, "seam"),
            Piece("ClaspSeamRight", seamR, False, False, 4, "seam"), Piece("ClaspHighlight", hi, False, False, 3, "hi")]

def arcane_back():
    """Moon Robe — 초승달 로브. 곡선 옆선 · 초승달로 파인 밑단 · 어깨 요크 그늘 · 밑단 홈에 앉은 초승달(보조색) · 뒷주름 · 별(+) · 앞면 하이라이트."""
    F = ((1.12, -1.26), (1.80, -2.60), (1.72, -4.36)); B = ((-1.72, -4.36), (-1.80, -2.60), (-1.12, -1.26))
    hem = [(1.72*math.cos(math.pi*i/8), -4.36 + 1.02*math.sin(math.pi*i/8)) for i in range(9)]
    body = dedup([(-1.12, -1.26)] + qbez(*F, 7) + hem[1:] + qbez(*B, 7)[1:])
    sway = (body.index(hem[0]), 9)
    yoke = [(-1.12, -1.26), (1.12, -1.26), (1.30, -1.78), (-1.30, -1.78)]
    moon = crescent(0.0, -3.60, 0.80, 0.61, 0.32, 90.0)
    fend = (-1.48, -4.36 + 1.02*math.sin(math.acos(1.48/1.72)))
    fold = qbez((-0.72, -1.26), (-1.30, -2.80), fend, 7)
    starH = [(0.09, -2.05), (0.63, -2.05)]; starV = [(0.36, -2.32), (0.36, -1.78)]
    hi = [qbez_at(*F, 0.4), (1.50, -2.80), (1.48, -3.20)]
    return [Piece("RobeBody", body, True, True, 0, "fill", sway=sway), Piece("RobeYoke", yoke, True, True, 2, "fillns"),
            Piece("RobeMoon", moon, True, True, 1, "fill"), Piece("RobeFold", fold, False, False, 4, "seam"),
            Piece("RobeStarH", starH, False, False, 3, "hi"), Piece("RobeStarV", starV, False, False, 3, "hi"),
            Piece("RobeHighlight", hi, False, False, 3, "hi")]

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
FILES = {("cyber", "HEAD"): "pack_cyber_head_patched_hood", ("cyber", "EYES"): "pack_cyber_eyes_slit_visor",
         ("cyber", "NECK"): "pack_cyber_neck_cable_collar", ("cyber", "BACK"): "pack_cyber_back_tarp_cape",
         ("mine", "HEAD"): "pack_mine_head_miner_helmet", ("mine", "EYES"): "pack_mine_eyes_dust_goggles",
         ("mine", "NECK"): "pack_mine_neck_mine_lamp", ("mine", "BACK"): "pack_mine_back_pick_harness",
         ("arcane", "HEAD"): "pack_arcane_head_wizard_hat", ("arcane", "EYES"): "pack_arcane_eyes_astro_lens",
         ("arcane", "NECK"): "pack_arcane_neck_moon_clasp", ("arcane", "BACK"): "pack_arcane_back_moon_robe"}

# ═════════════════════════════════════════════════════════════════════════════
#                                   자 (尺)
# ═════════════════════════════════════════════════════════════════════════════
def seg_dist(p1, q1, p2, q2):
    """선분-선분 거리 (CostumePropForbiddenZoneTests.SegmentDistance 와 같은 식 — 교차·T접합이면 0)."""
    def cross(o, a, b): return (a[0]-o[0])*(b[1]-o[1]) - (a[1]-o[1])*(b[0]-o[0])
    def onspan(p, q, r): return min(p[0], r[0])-1e-9 <= q[0] <= max(p[0], r[0])+1e-9 and min(p[1], r[1])-1e-9 <= q[1] <= max(p[1], r[1])+1e-9
    d1, d2, d3, d4 = cross(p1, q1, p2), cross(p1, q1, q2), cross(p2, q2, p1), cross(p2, q2, q1)
    if (d1 > 0) != (d2 > 0) and (d3 > 0) != (d4 > 0): return 0.0
    if abs(d1) < 1e-9 and onspan(p1, p2, q1): return 0.0
    if abs(d2) < 1e-9 and onspan(p1, q2, q1): return 0.0
    if abs(d3) < 1e-9 and onspan(p2, p1, q2): return 0.0
    if abs(d4) < 1e-9 and onspan(p2, q1, q2): return 0.0
    def pseg(p, a, b):
        dx, dy = b[0]-a[0], b[1]-a[1]; l2 = dx*dx+dy*dy
        t = 0.0 if l2 <= 0 else max(0.0, min(1.0, ((p[0]-a[0])*dx+(p[1]-a[1])*dy)/l2))
        return math.hypot(p[0]-(a[0]+t*dx), p[1]-(a[1]+t*dy))
    return min(pseg(p1, p2, q2), pseg(q1, p2, q2), pseg(p2, p1, q1), pseg(q2, p1, q1))

def segments(pc):
    p = pc.pts; n = len(p)
    s = [(p[i], p[i+1]) for i in range(n-1)]
    if pc.loop and n > 2: s.append((p[-1], p[0]))
    return s

def piece_dist(a, b):
    return min(seg_dist(a0, a1, b0, b1) for a0, a1 in segments(a) for b0, b1 in segments(b))

def true_min_edge(pcs):
    """그려지는 변만 잰다 — 윤곽 없는 채움(fillns)의 변은 선으로 그려지지 않는다(삼각분할 경계일 뿐)."""
    m, where = 9e9, None
    for pc in pcs:
        if pc.noStroke: continue
        for a, b in segments(pc):
            d = math.hypot(b[0]-a[0], b[1]-a[1])
            if d < m: m, where = d, pc.name
    return m, where

def curved_pieces(pcs):
    n = 0
    for pc in pcs:
        p = pc.pts; k = len(p)
        if k < 4: continue
        idx = range(k) if pc.loop else range(1, k-1)
        small = [rig.turn_deg(p[(i-1) % k], p[i], p[(i+1) % k]) < 30.0 for i in idx]
        run = 0; found = False
        for s in small:
            run = run+1 if s else 0
            if run >= 3: found = True
        n += found
    return n

def h2_layers(pcs):
    pieces = [dict(name=p.name, pts=list(p.pts), filled=bool(p.filled), loop=bool(p.loop), tone=p.tone,
                   sort=(R24.SORT_BACK if p.layer == 1 else R24.SORT_HEAD)) for p in pcs]
    wear = R24.h2_wear(pieces)
    front = [p for p in pieces if p["filled"] and p["sort"] >= R24.SORT_EYES]
    HR = R24.HEAD_R_COVER
    xs = np.arange(-HR, HR, 0.002)
    bottom = None
    for y in np.arange(HR, -1.2, -0.002):
        row = np.column_stack([xs, np.full_like(xs, y)])
        cov = np.zeros(len(xs), bool)
        for p in front: cov |= R24.inside(p["pts"], row)
        if cov.any(): bottom = float(y)
    return wear, (None if bottom is None else bottom - 0.002)

def front_fill_shapes(pcs, slot):
    return [p.to_shape(slot) for p in pcs if p.filled and p.layer == 0]

_fail = []
def bad(m): _fail.append(m); print("  x  " + m)
def ok(m): print("  OK " + m)

def gate(control=False):
    base = PA.prod_base("--refresh-prod" in sys.argv)
    shipped = PA.pack_items()
    print("=" * 104)
    print("팩 12종 디테일 고도화 R2 — 전수 게이트   (W@0.75 %.5fR · 규칙4 간격 %.5fR · 래칫 %.4fR · 펜 %.5fR)" % (W075, GAP, RATCHET, PEN))
    print("=" * 104)
    # 자 교정 — 출하 Patched Hood ↔ 프로덕션 베레모 = 0.100
    hood = [dict(pts=s["pts"], loop=s["loop"], filled=s["filled"]) for s in shipped[("HEAD", "cyber")][1]]
    beret = [s for n, s in base["HEAD"] if n == "베레모"][0]
    d, okc = LG.calibrate(hood, beret)
    print("  자 교정: 출하 Patched Hood ↔ 베레모 카드 차 = %.3f (테스트 실측 0.100 ± 0.02) -> %s" % (d, "OK" if okc else "★ 실패 — 아래 숫자 폐기"))
    if not okc: sys.exit(2)
    base_cells = {slot: [(n, LG.cells(sh)) for n, sh in base[slot]] for slot in ("HEAD", "EYES", "NECK", "BACK")}
    base_shapes = {slot: [(n, [Shape(s["name"], s["pts"], s["loop"], s["filled"], s["tone"], s["sort"]) for s in sh]) for n, sh in base[slot]]
                   for slot in ("HEAD", "EYES", "NECK", "BACK")}
    census_base = {}
    for slot in ("HEAD", "EYES", "NECK", "BACK"):
        L = base[slot]
        census_base[slot] = (sum(len(sh) for _, sh in L)/len(L), sum(sum(len(s["pts"]) for s in sh) for _, sh in L)/len(L))

    designs = {}
    for packname, key, motif, PACK in PACKS:
        for slot, (disp, iid, fn) in PACK.items():
            designs[(key, slot)] = fn()
    if control:
        # ① 후드를 프로덕션 베레모 사본으로 바꾼다 → 카드 판별성 미달이 나와야 한다
        designs[("cyber", "HEAD")] = [Piece(s["name"], s["pts"], s["loop"], s["filled"], s["tone"], "fill" if s["filled"] else "line") for s in beret]
        # ② 광부 EYES 보조색을 눈썹 위로 올린다 → E-대역 위반
        e = designs[("mine", "EYES")]; e[3] = Piece("GoggleNosePad", [(x, y+1.0) for x, y in e[3].pts], True, True, 1, "fill")
        # ③ 대마법사 NECK 솔기를 0.2R 띄운다 → 규칙 4 금지대
        n = designs[("arcane", "NECK")]; n[3] = Piece("ClaspSeamLeft", [(-0.70, -1.40), (-0.70, -1.96)], False, False, 4, "seam")
        # ④ 사이버 BACK 패드를 0.2R 짜리로 줄인다 → 규칙 1-A/1-C
        b = designs[("cyber", "BACK")]; b[2] = Piece("CapePad", hexpad(0.72, -1.20, 0.10, 0.08), True, True, 1, "fill")
        # ⑤ 광부 HEAD 돔 밑단을 +0.10 으로 내린다 → H-2b
        h = designs[("mine", "HEAD")]; h[0] = Piece("HelmetShell", ellipse_arc(0.0, 0.10, 1.46, 1.42, 180, 0, 21), True, True, 0, "fill")

    for packname, key, motif, PACK in PACKS:
        print("\n" + "=" * 104 + "\n  %s\n" % packname + "=" * 104)
        for slot in ("HEAD", "EYES", "NECK", "BACK"):
            disp, iid, _ = PACK[slot]
            pcs = designs[(key, slot)]
            shapes = [p.to_shape(slot) for p in pcs]
            print("\n  ── [%s] %s  %s ──" % (slot, disp, iid))
            # (0) 밀도 센서스
            old = shipped[(slot, key)][1]
            npts = sum(len(p.pts) for p in pcs)
            print("     조각 %d(출하 %d · 기본 %s 평균 %.1f) · 점 %d(출하 %d · 기본 평균 %.0f) · 곡선조각 %d · 톤 %s · 뒤층 %d"
                  % (len(pcs), len(old), slot, census_base[slot][0], npts, sum(len(s["pts"]) for s in old), census_base[slot][1],
                     curved_pieces(pcs), sorted(set(p.tone for p in pcs)), sum(1 for p in pcs if p.layer == 1)))
            # (1) 규칙 1-A / 자기교차 / 1-C
            for p in pcs:
                sh = p.to_shape(slot)
                v = rig.rule_one(sh, W075)
                if v: bad("%s '%s' 규칙1: %s" % (disp, p.name, v))
                if p.loop and rig.self_intersects(p.pts): bad("%s '%s' 자기교차" % (disp, p.name))
                if p.filled:
                    w_, h_ = P78.ink_rect(sh)
                    if min(w_, h_) < 1.5*W075: bad("%s '%s' 채움 잉크 사각형 %.3fx%.3f < 1.5획" % (disp, p.name, w_, h_))
                    r = P78.rho_max(p.pts)
                    if r < PEN: bad("%s '%s' rho_max %.4fR < 펜 %.4f (규칙 1-C)" % (disp, p.name, r, PEN))
            e0, w0 = P78.corner_min_edge(shapes, W075); et, wt = true_min_edge(pcs)
            ok("규칙1 꺾임-꺾임 최단 %s · 실제 최단변 %.3fR = %.2f획@0.75 (%s) · 1-C 전 채움 통과" %
               (("없음" if e0 > 9e8 else "%.3fR=%.2f획" % (e0, e0/W075)), et, et/W075, wt))
            # (2) 규칙 4 — 획 있는 조각 쌍: 닿거나 1.5획 이상
            stroked = [p for p in pcs if not p.noStroke]
            worst = None
            for i in range(len(stroked)):
                for j in range(i+1, len(stroked)):
                    dd = piece_dist(stroked[i], stroked[j])
                    if 1e-6 < dd < GAP - 1e-6:
                        bad("%s 금지대: '%s' ↔ '%s' d=%.4fR = %.2f획 (0 또는 >= 1.5획)" % (disp, stroked[i].name, stroked[j].name, dd, dd/W075))
                    if dd > 1e-6 and (worst is None or dd < worst[0]): worst = (dd, stroked[i].name, stroked[j].name)
            touching = sum(1 for i in range(len(stroked)) for j in range(i+1, len(stroked)) if piece_dist(stroked[i], stroked[j]) <= 1e-6)
            ok("규칙4 획 조각 %d · 쌍 %d · 닿음 %d · 떨어진 쌍 최소 %s" % (len(stroked), len(stroked)*(len(stroked)-1)//2, touching,
               ("없음" if worst is None else "%.3fR=%.2f획 (%s↔%s)" % (worst[0], worst[0]/W075, worst[1], worst[2]))))
            # (3) 슬롯 경계 (pack78 §5-1 그대로)
            allp = [q for p in pcs for q in p.pts]
            ys = [q[1] for q in allp]; xs = [q[0] for q in allp]
            if slot == "HEAD":
                if not (1.0 < max(ys) < 2.551): bad("%s 꼭대기 %.3f (1.0<t<2.551)" % (disp, max(ys)))
                if min(ys) <= -1.0: bad("%s 턱 아래 %.3f" % (disp, min(ys)))
                if not any(abs(q[0]) >= 0.85 and q[1] <= 0.05 for q in allp): bad("%s 감쌈 실패(원칙 4)" % disp)
                wear, bot = h2_layers(pcs)
                if wear is None: bad("%s H-2 착용선 없음" % disp)
                else:
                    tail = None if bot is None else wear - bot
                    if not (0.28 <= wear <= 0.45): bad("%s H-2 착용선 %+.4f 대역 [+0.28,+0.45] 밖" % (disp, wear))
                    if bot is None or not (0.28 <= bot <= 0.35): bad("%s H-2b 밑단 %s 대역 [+0.28,+0.35] 밖" % (disp, "없음" if bot is None else "%+.4f" % bot))
                    if tail is None or tail > 0.170: bad("%s 꼬리 %s > 0.170" % (disp, tail))
                    ok("HEAD 꼭대기 %+.3f · 밑 %+.3f · 착용선 %+.4f · 앞층 밑단 %+.4f · 꼬리 %.4f" % (max(ys), min(ys), wear, bot or 0, tail or 0))
                eyes = designs[(key, "EYES")]
                cov = P78.eyes_occlusion(front_fill_shapes(pcs, "HEAD"), [p.to_shape("EYES") for p in eyes])
                acc = [p.to_shape("EYES") for p in eyes if p.tone == 1]
                cova = P78.eyes_occlusion(front_fill_shapes(pcs, "HEAD"), acc)
                if cov > 0.55: bad("%s 가 같은 팩 안경 %.1f%% 를 덮는다" % (disp, cov*100))
                ok("같은 팩 안경 가려짐 %.1f%% · 보조색 조각만 %.1f%% (출하 36조합 평균 생존 6.4%%)" % (cov*100, cova*100))
            if slot == "EYES":
                f = [p for p in pcs if p.filled and p.layer == 0]
                if max(abs(x) for x in xs) >= 1.6: bad("%s |x| %.2f >= 1.6" % (disp, max(abs(x) for x in xs)))
                if max(ys) >= 1.15: bad("%s 정수리 침범 %.2f" % (disp, max(ys)))
                if min(ys) <= -2.2: bad("%s 목 아래 %.2f" % (disp, min(ys)))
                fe = any(rig.contains(p.pts, (rig.EYE_X, rig.EYE_Y)) for p in f); be = any(rig.contains(p.pts, (-rig.EYE_X, rig.EYE_Y)) for p in f)
                if not (fe and be): bad("%s 두 눈 커버 실패 (앞 %s 뒤 %s)" % (disp, fe, be))
                acc_top = max(q[1] for p in pcs if p.tone == 1 for q in p.pts)
                if acc_top > 0.28: bad("%s E-대역: 보조색 조각 y max %+.3f > +0.28 (모자가 먹는다)" % (disp, acc_top))
                ok("EYES |x|max %.3f · y[%+.3f,%+.3f] · 두 눈 커버 · 보조색 y max %+.3f (E-대역 <= +0.28)" % (max(abs(x) for x in xs), min(ys), max(ys), acc_top))
            if slot == "NECK":
                if max(ys) >= 0.0: bad("%s 얼굴 침범 %.2f" % (disp, max(ys)))
                if min(ys) <= rig.HIP_R - 0.517: bad("%s 고관절 아래 %.2f" % (disp, min(ys)))
                ok("NECK y[%+.3f,%+.3f]" % (min(ys), max(ys)))
            if slot == "BACK":
                if max(ys) >= 1.0: bad("%s 정수리 위 %.2f" % (disp, max(ys)))
                if min(ys) <= -9.3395: bad("%s 바닥 관통 %.2f" % (disp, min(ys)))
                ok("BACK y[%+.3f,%+.3f] · x[%+.3f,%+.3f]" % (min(ys), max(ys), min(xs), max(xs)))
            # (4) 카드 판별성 — 기본 6종 + 같은 슬롯 다른 팩 2종 (AccessoryNameLegibilityTests 자)
            mine_cells = LG.cells([p.to_dict() for p in pcs])
            worstc = (9, None)
            for bn, bc in base_cells[slot]:
                dd = LG.difference(mine_cells, bc)
                if dd < worstc[0]: worstc = (dd, bn)
                if dd < LG.MIN_CARD_DIFFERENCE: bad("%s ↔ 기본 %s 카드 차 %.3f < 0.15" % (disp, bn, dd))
            for (k2, s2), pcs2 in designs.items():
                if s2 != slot or k2 == key: continue
                dd = LG.difference(mine_cells, LG.cells([p.to_dict() for p in pcs2]))
                if dd < LG.MIN_CARD_DIFFERENCE: bad("%s ↔ 팩 %s 카드 차 %.3f < 0.15" % (disp, k2, dd))
                worstc = min(worstc, (dd, "팩:" + k2))
            ok("카드 판별성 최악쌍 %.3f (%s) — 문턱 0.15" % (worstc[0], worstc[1]))
            # (5) 쌍별 실루엣 (L-inf 프로파일, 프로덕션 기본 6종)
            mp = S.profile(shapes, ANCHOR[slot]); worsts = (9e9, None)
            for bn, bsh in base_shapes[slot]:
                dd = rig.max_delta(mp, S.profile(bsh, ANCHOR[slot]))
                if dd < worsts[0]: worsts = (dd, bn)
            if worsts[0] < W075: bad("%s 실루엣 최악쌍 %.3fR < 1획 (vs %s)" % (disp, worsts[0], worsts[1]))
            ok("실루엣 최악쌍 vs %s %.3fR = %.2f획@0.75 %s" % (worsts[1], worsts[0], worsts[0]/W075, "래칫" if worsts[0] >= RATCHET else "하한만"))
            # (6) 카드 두 크기
            x0, y0, x1, y1 = rig.bounds(allp); span = max(x1-x0, y1-y0)
            upr = P78.units_per_R(slot); shrink = min(1.0, P78.ICON_VIEWBOX/(span*upr))
            for size in (P78.CARD_SIZE, P78.SLOT_SIZE):
                ptr = upr*shrink*(size/P78.ICON_VIEWBOX); st = P78.card_stroke_pt(size)
                if et*ptr < st: bad("%s %.0fpt 최단변 %.2fpt < 획 %.3fpt (%s)" % (disp, size, et*ptr, st, wt))
            accs = [p for p in pcs if p.tone == 1]
            r_acc = max(P78.rho_max(p.pts) for p in accs)
            w24 = 2*r_acc*upr*shrink*(P78.SLOT_SIZE/P78.ICON_VIEWBOX)
            if w24 < 1.0: bad("%s 보조색 최대 두께 %.2fpt @24pt < 1px" % (disp, w24))
            ok("카드 58pt %.2fpt/R (축소 x%.3f) · 슬롯행 24pt 최단변 %.2fpt(획 0.825) · 보조색 두께 %.2fpt @24pt" %
               (upr*shrink*(58/64.0), shrink, et*upr*shrink*(24/64.0), w24))
            # (7) 모티프 — 보조색 조각 전부가 자기 팩 잎으로 분류되는가
            for p in accs:
                got = P78.classify(p.pts)
                if got != motif: bad("%s 보조색 '%s' 가 「%s」로 분류(기대 %s)" % (disp, p.name, got, motif))
            ok("모티프 보조색 %d조각 전부 「%s」" % (len(accs), motif))
            # (8) v2 획 실폭 참고
        # 팩 안 HEAD × NECK · HEAD × BACK 겹침 참고(가로지르는 채움만 경고)
    print("\n" + "=" * 104)
    print("결과: %s" % ("전수 통과 (위반 0건)" if not _fail else "위반 %d건" % len(_fail)))
    return len(_fail)

def fmt(v):
    """소수 5자리 — 인계본 조각의 strokeInR(0.13845 · 0.10384)과 같은 자릿수. 4자리로 자르면 ×0.8 명목(0.11072)이 0.1107 로 되읽힌다(왕복 검산이 잡았다)."""
    s = ("%.5f" % v).rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s

def emit_yaml(pcs, slot):
    lines = ["  wornShapes:"]
    for p in pcs:
        pr = p.params(slot)
        sway = p.sway or (-1, 0)
        lines += ["  - name: %s" % p.name, "    loop: %d" % (1 if p.loop else 0), "    filled: %d" % (1 if p.filled else 0),
                  "    tone: %d" % p.tone, "    swayStart: %d" % sway[0], "    swayCount: %d" % sway[1], "    swingDegrees: 0",
                  "    surfaces: 0", "    strokeMult: %s" % fmt(pr["strokeMult"]), "    strokeInR: %s" % fmt(pr["strokeInR"]),
                  "    noStroke: %d" % pr["noStroke"], "    alpha: %s" % fmt(pr["alpha"]), "    lineAlpha: %s" % fmt(pr["lineAlpha"]),
                  "    underBack: 0", "    layer: %d" % p.layer, "    bodyFixed: 0", "    terms:", "    - %d" % len(p.pts)]
        for x, y in p.pts:
            # x = HeadRadius × x ;  y = HeadCenterLine + HeadRadius × y   (출하 팩 에셋과 같은 항 문법)
            lines += ["    - 1", "    - 0", "    - 0", "    - 0", "    - 1", "    - %s" % fmt(x),
                      "    - 2", "    - 4", "    - 0", "    - 0", "    - 0", "    - 0", "    - 0", "    - 0", "    - 1", "    - %s" % fmt(y)]
    return "\n".join(lines) + "\n"

# ── 왕복 검산: 방출한 YAML 을 «에셋 파서»(pack_assets.decode_terms — 생성기와 다른 경로)로 되읽어 설계 좌표와 대조한다.
def verify_emit():
    import re
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pack_detail_r2")
    bad_n = 0; pieces = 0; pts = 0
    for packname, key, motif, PACK in PACKS:
        for slot, (disp, iid, fn) in PACK.items():
            path = os.path.join(out, FILES[(key, slot)] + ".wornShapes.yaml")
            txt = "  displayName: %s\n  slot: %d\n" % (disp, {"HEAD": 0, "EYES": 1, "NECK": 2, "BACK": 3}[slot]) + open(path, encoding="utf-8").read() + "  wornGroupAlpha: 0\n"
            _, _, shapes = PA.parse_asset(txt) if False else PA.parse_asset.__wrapped__(txt) if hasattr(PA.parse_asset, "__wrapped__") else (None, None, None)
            if shapes is None:
                # parse_asset 은 경로를 받는다 — 임시 파일로 돌린다
                tmp = os.path.join(out, "_roundtrip.asset")
                open(tmp, "w", encoding="utf-8").write(txt)
                _, _, shapes = PA.parse_asset(tmp); os.remove(tmp)
            design = fn()
            if [s["name"] for s in shapes] != [p.name for p in design]: bad_n += 1; print("  x 조각 이름/순서 불일치:", disp)
            for s, p in zip(shapes, design):
                pieces += 1
                pr = p.params(slot)
                if (s["loop"], s["filled"], s["tone"], s["noStroke"], s["layer"]) != (p.loop, p.filled, p.tone, p.noStroke, p.layer) \
                   or abs(s["strokeInR"] - pr["strokeInR"]) > 1e-9 or abs(s["strokeMult"] - pr["strokeMult"]) > 1e-9 or abs(s["lineAlpha"] - pr["lineAlpha"]) > 1e-9:
                    bad_n += 1; print("  x 속성 불일치:", disp, p.name)
                if len(s["pts"]) != len(p.pts): bad_n += 1; print("  x 점 수 불일치:", disp, p.name); continue
                for (x1, y1), (x2, y2) in zip(s["pts"], p.pts):
                    pts += 1
                    if abs(x1 - x2) > 5e-5 or abs(y1 - y2) > 5e-5:      # 방출 소수 4자리 반올림 한계
                        bad_n += 1; print("  x 좌표 불일치: %s %s (%.5f,%.5f) vs (%.5f,%.5f)" % (disp, p.name, x1, y1, x2, y2))
    print("왕복 검산: 조각 %d · 점 %d · 불일치 %d건 -> %s" % (pieces, pts, bad_n, "OK" if bad_n == 0 else "FAIL"))
    # 양성 대조 — 좌표 한 칸을 흔든 사본이 실제로 빨개지는가
    path = os.path.join(out, FILES[("cyber", "HEAD")] + ".wornShapes.yaml")
    txt = open(path, encoding="utf-8").read().replace("    - 1.6\n", "    - 1.61\n", 1)
    tmp = os.path.join(out, "_roundtrip.asset"); open(tmp, "w", encoding="utf-8").write("  displayName: x\n  slot: 0\n" + txt + "  wornGroupAlpha: 0\n")
    _, _, shapes = PA.parse_asset(tmp); os.remove(tmp)
    d = cyber_head()
    moved = any(abs(x1 - x2) > 5e-5 for (x1, _), (x2, _) in zip(shapes[0]["pts"], d[0].pts))
    print("왕복 양성 대조(좌표 한 칸 +0.01): %s" % ("잡는다" if moved else "★ 못 잡는다 — 위 OK 를 믿지 마라"))
    return bad_n == 0 and moved


if __name__ == "__main__":
    if "--verify-emit" in sys.argv: sys.exit(0 if verify_emit() else 1)
    if "--dump" in sys.argv:
        for packname, key, motif, PACK in PACKS:
            print("\n##### %s" % packname)
            for slot, (disp, iid, fn) in PACK.items():
                pcs = fn()
                print("\n[%s] %s  %s   (앵커 y=%.5f)" % (slot, disp, iid, ANCHOR[slot]))
                for p in pcs:
                    pr = p.params(slot)
                    print("  %-16s %-5s loop=%-5s filled=%-5s tone=%d layer=%d strokeMult=%.2f strokeInR=%.5f noStroke=%d lineAlpha=%.2f n=%d%s"
                          % (p.name, p.kind, p.loop, p.filled, p.tone, p.layer, pr["strokeMult"], pr["strokeInR"], pr["noStroke"], pr["lineAlpha"], len(p.pts),
                             ("" if not p.sway else "  sway=%d+%d" % p.sway)))
                    for x, y in p.pts: print("      (%+.4f, %+.4f)" % (x, y))
        sys.exit(0)
    if "--emit" in sys.argv:
        out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pack_detail_r2")
        os.makedirs(out, exist_ok=True)
        for packname, key, motif, PACK in PACKS:
            for slot, (disp, iid, fn) in PACK.items():
                path = os.path.join(out, FILES[(key, slot)] + ".wornShapes.yaml")
                open(path, "w", encoding="utf-8").write(emit_yaml(fn(), slot))
                print("wrote", os.path.relpath(path))
        sys.exit(0)
    ctl = "--control" in sys.argv
    if ctl: print("★ 양성 대조 모드 — 나쁜 값 5종을 넣는다. 빨간불이 켜져야 정상.\n")
    n = gate(ctl)
    if ctl:
        print("\n★ 양성 대조: %s" % ("OK — 실제로 잡는다(위반 %d건)" % n if n >= 5 else "FAIL — 위반 %d건뿐" % n))
        sys.exit(0 if n >= 5 else 1)
    sys.exit(1 if n else 0)
