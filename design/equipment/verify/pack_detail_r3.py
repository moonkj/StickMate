# -*- coding: utf-8 -*-
"""★ 팩 장비 12종 R3 — «카드 비트맵과 같은 물건으로 읽히게» (사용자 지시 2026-09-08:
   "장비창과 실제 착용 이미지가 너무 다르잖아" -> "장비창에 있는 이미지와 같아야지", 리더 선택지 (2)).

  python3 pack_detail_r3.py               # 전수 게이트 (R2 게이트 전부 + R3 신규 4축)
  python3 pack_detail_r3.py --control     # 양성 대조
  python3 pack_detail_r3.py --dump        # 좌표 전문
  python3 pack_detail_r3.py --emit        # design/equipment/pack_detail_r3/*.wornShapes.yaml
  python3 pack_detail_r3.py --verify-emit # 방출본 왕복 검산
  python3 pack_detail_r3.py --bitmap      # ★ R3 신규 — 비트맵 12장과 구조 대조(종횡·역할점유·폭프로파일)

게이트는 R2(pack_detail_r2.py)의 것을 **그대로 재사용**한다 — PACKS/FILES 만 갈아 끼우고 R2.gate() 를 부른다.
같은 자를 두 벌 짜면 어느 쪽이 거짓말을 하는지 알 수 없다(이 저장소가 반복해 당한 형태).

R3 가 새로 세운 자 4개
  (T-1) 트림선(tone 3, lineAlpha 0.85)은 **채움 윤곽 위에** 있어야 한다 — 점을 윤곽에서 «빌려» 쓴다.
        이유는 규칙 4다: 윤곽에서 0.03~0.50R 안쪽으로 «나란히» 들어간 선은 금지대(0 < d < 1.5W)에 걸린다.
        띄우려면 0.516R 을 띄워야 하는데 그건 로브 반폭의 30%라 트림이 아니라 두 번째 옷이 된다.
  (T-2) 트림선 길이 합 / 실루엣 둘레 >= 0.25 — 비트맵의 트림 점유(전경 대비 7~31%, 중앙값 13.5%)에 대응.
  (T-3) 암부(tone 2 + 잉크 솔기) 면적 점유가 비트맵 암부 점유의 [0.45, 1.20] 배 안.
  (T-4) 종횡비(W/H)가 비트맵 종횡비의 [0.60, 1.60] 배 안.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import rig, sectors as S
from rig import Shape
import pack78_shapes as P78
import pack_detail_r2 as R2
from pack_detail_r2 import (Piece, qbez, qbez_at, qbez_x_at_y, ellipse_arc, rrect, hexpad,
                            dedup, crescent, wedge, NOMINAL, SORT, ANCHOR, W075, GAP)

# ── R3 조각 문법 확장: kind="trim" (tone 3 · strokeMult 0.75 · lineAlpha 0.85)
#    R2 의 "hi"(lineAlpha 0.42)와 **같은 획**이고 알파만 다르다 — 두께로 위계를 만들지 않는다(스펙 §3 원칙 7).
_r2_params = Piece.params
def _params(self, slot):
    if self.kind == "trim":
        n = NOMINAL[slot]
        return dict(strokeMult=0.75, strokeInR=round(n*0.75, 5), noStroke=0, alpha=0, lineAlpha=0.85)
    return _r2_params(self, slot)
Piece.params = _params
_r2_init = Piece.__init__
def _init(self, name, pts, loop=True, filled=False, tone=0, kind="fill", layer=0, sway=None, host=None):
    """host — ★ R3 신설. 이 조각이 «얹히는» 채움 조각의 이름. tone 3(흰 선)은 반드시 적는다.
    이유: 카드 경로(AccessoryCardIcon.cs:331-334)는 underBack 이 0 이면 밑색을 «카드 바탕»으로 잡아
    Lerp(#15181E, 흰, α) 을 굽는데, 몸(CharacterAccessoryRenderer)은 «그 아래 실제로 그려진 조각» 위에
    런타임 알파로 얹는다. 즉 underBack 을 비우면 **카드와 몸이 다른 색이 된다** — 이번 라운드가 없애려는
    바로 그 어긋남이다. R2 방출본은 12종 전부 underBack: 0 이라 하이라이트(α0.42)에서 이 갈라짐이 실재한다."""
    if kind == "trim":
        self.name, self.pts, self.loop, self.filled, self.tone, self.kind, self.layer, self.sway = \
            name, [(float(x), float(y)) for x, y in pts], loop, filled, tone, kind, layer, sway
        assert tone == 3 and not filled
        self.host = host
        return
    _r2_init(self, name, pts, loop, filled, tone, kind, layer, sway)
    self.host = host
Piece.__init__ = _init

# ── 기하 도우미 (R3 신규) ────────────────────────────────────────────────────
def ngon(cx, cy, r, n, phase=0.0, ry=None):
    ry = r if ry is None else ry
    return [(cx + r*math.cos(math.radians(phase + 360.0*i/n)), cy + ry*math.sin(math.radians(phase + 360.0*i/n))) for i in range(n)]

def lozenge(cx, cy, hw, hh):
    """마름모(보석). 비트맵 12장 중 7장이 이 형태를 갖는다 — 대마법사 4 · 사이버 3."""
    return [(cx, cy+hh), (cx+hw, cy), (cx, cy-hh), (cx-hw, cy)]

def octa(cx, cy, r, ry=None):
    """8각 베젤 — 사이버 모티프(볼록 5~8각). 비트맵의 «금 테 안의 보석»이 전부 8각이다."""
    return ngon(cx, cy, r, 8, 22.5, ry)

def arcslice(pts, i, j):
    """채움 윤곽에서 [i, j] 구간을 **그대로** 빌려 트림선을 만든다 → 규칙 4 거리 0(닿음)이 구조적으로 보장된다."""
    return [tuple(p) for p in (pts[i:j+1] if i <= j else pts[i:] + pts[:j+1])]

def polylen(pts, loop=False):
    s = sum(math.hypot(pts[i+1][0]-pts[i][0], pts[i+1][1]-pts[i][1]) for i in range(len(pts)-1))
    if loop and len(pts) > 2: s += math.hypot(pts[0][0]-pts[-1][0], pts[0][1]-pts[-1][1])
    return s

def star(cx, cy, r):
    return [(cx-r, cy), (cx+r, cy)], [(cx, cy-r), (cx, cy+r)]

# ═════════════════════════════════════════════════════════════════════════════
#  ★ R3 조형 규율 3개 (이것이 R2 와 다른 점의 전부다)
#    (가) **트림선은 채움 윤곽의 점을 그대로 빌린다** — arcslice(). 규칙 4 거리 0(닿음)이 구조적으로 보장된다.
#         윤곽 안쪽으로 «나란히» 0.03~0.50R 들어간 선은 전부 금지대(0 < d < 1.5W = 0.516R)에 걸린다.
#         띄우려면 0.516R 이어야 하는데 그건 로브 반폭의 30%라 «트림»이 아니라 두 번째 옷이 된다.
#    (나) **장식(보석·램프·걸쇠)은 얹지 않고 «가로지른다»** — 반드시 host 윤곽을 넘어가게 놓는다(d=0).
#    (다) **어두운 덩어리는 되도록 fillns(윤곽 없는 채움)** — 규칙 4 면제 대상이라 안쪽에 자유롭게 놓인다.
#         비트맵 암부 점유(광부 39~62% · 대마법사 24~49%)를 이 통로로 채운다.
# ═════════════════════════════════════════════════════════════════════════════
#  사이버 pack.cyber — 비트맵: 옥색 천 + 크림/금 트림 + 6~8각 베젤 안의 초록 보석
# ═════════════════════════════════════════════════════════════════════════════
def cyber_head():
    """Patched Hood.  비트맵: 둥근 후드 · **바깥 실루엣 전체를 크림 트림이 두른다** · 앞쪽 위 **금 베젤 보석** ·
    후드 안쪽(이마 위)이 어둡다 · 옆자락이 턱선까지 내려온다.
    R2 대비 — 패드를 뒤(−1.28,+1.30)에서 앞(+1.22,+0.92)으로 옮기고, 바깥 윤곽 트림선을 새로 넣었다."""
    dome = [(1.60, -0.50), (1.56, -0.05), (1.46, 0.40)]
    dome += qbez((1.46, 0.40), (1.34, 1.16), (0.58, 1.78), 7)
    dome += qbez((0.58, 1.78), (0.10, 2.10), (-0.62, 2.06), 6)[1:]
    dome += qbez((-0.62, 2.06), (-1.32, 1.86), (-1.58, 0.62), 8)[1:]
    dome += [(-1.66, 0.20), (-1.72, -0.28), (-1.74, -0.66), (-1.19, -0.60), (-1.19, 0.30),
             (-0.60, 0.30), (0.00, 0.30), (0.60, 0.30), (1.19, 0.30), (1.19, -0.42)]
    shell = dedup(dome)
    lining = [(-0.76, 0.32), (0.76, 0.32), (0.76, 0.86), (-0.76, 0.86)]      # H-2b: 밑단 +0.30(shell) 유지 · 최소 변 0.54 >= 1.5W
    gem = hexpad(1.22, 0.92, 0.44, 0.34)                                     # 사이버 모티프 = 볼록 6각
    i_hem_b = shell.index((-1.19, 0.30))
    trim = arcslice(shell, len(shell) - 1, i_hem_b)                          # 앞 밑단 -> 앞·위·뒤 -> 뒤 밑단·안쪽 시작점 (바깥 실루엣 전부)
    seam = [(0.00, 0.30)] + qbez((0.00, 0.30), (-0.20, 1.30), (-0.62, 2.06), 6)[1:]
    hi = [(-1.19, 0.30), (-1.02, 0.72), (-0.92, 1.10)]
    return [Piece("HoodShell", shell, True, True, 0, "fill"), Piece("HoodLining", lining, True, True, 2, "fillns"),
            Piece("HoodGem", gem, True, True, 1, "fill"), Piece("HoodSeam", seam, False, False, 4, "seam"),
            Piece("HoodTrim", trim, False, False, 3, "trim", host="HoodShell"), Piece("HoodHighlight", hi, False, False, 3, "hi", host="HoodShell")]

def cyber_eyes():
    """Slit Visor.  비트맵: 8각 크림 테 안의 **가로로 긴 옥색 슬릿** · 양끝 **어두운 단자 블록** ·
    머리 뒤로 도는 끈 · 뒤 위로 솟은 **가는 안테나**.
    R2 대비 — 빛띠(보조색 채움, 윤곽 없음) -> **테를 밝게(트림) + 슬릿을 보조색 채움**으로 뒤집었다.
    비트맵에서 «밝은 것»은 테이고 R2 는 반대로 칠하고 있었다."""
    top = qbez((1.38, 0.42), (0.0, 0.50), (-1.40, 0.42), 7)
    bot = qbez((-1.40, -0.36), (0.0, -0.42), (1.38, -0.36), 7)
    bar = dedup([(1.56, 0.32)] + top + [(-1.58, 0.32), (-1.58, -0.26)] + bot + [(1.56, -0.26)])
    blkF = rrect(1.02, -0.24, 1.56, 0.30, 0.20, 2)      # 단자 블록 — 0.54 x 0.54 = 규칙 1 의 최소 채움(1.5W)
    blkB = rrect(-1.56, -0.24, -1.02, 0.30, 0.20, 2)
    ib = bar.index(bot[0])
    slit = [bar[ib+1], bar[ib+3], bar[ib+5], (1.02, 0.02), (0.0, 0.28), (-1.04, 0.02)]   # 밑변 3점을 윤곽에서 빌린다
    trim = arcslice(bar, 0, len(bar) - 1)                                    # 8각 테 전체
    strap = [(-1.58, 0.32)] + qbez((-1.58, 0.32), (-1.42, 0.72), (-0.78, 0.86), 5)[1:]
    ant = [(-1.58, 0.32), (-1.56, 0.86), (-1.14, 1.02)]
    hi = [bar[ib+5], (0.86, -0.20), (0.30, -0.16)]
    return [Piece("VisorBar", bar, True, True, 0, "fill"),
            Piece("VisorBlockFront", blkF, True, True, 2, "fillns"), Piece("VisorBlockBack", blkB, True, True, 2, "fillns"),
            Piece("VisorSlit", slit, True, True, 1, "fill"),
            Piece("VisorStrap", strap, False, False, 0, "line", layer=1),
            Piece("VisorAntenna", ant, False, False, 0, "line"),
            Piece("VisorTrim", trim, False, False, 3, "trim", host="VisorBar"), Piece("VisorHighlight", hi, False, False, 3, "hi", host="VisorSlit")]

def cyber_neck():
    """Cable Collar.  비트맵: 두꺼운 **고리 칼라** + 그 위 크림 금 판 · 앞쪽 **금 베젤 보석** ·
    아래로 늘어진 줄 끝에 **네모 금테 안의 초록 보석**.
    R2 대비 — 3코일 물결을 매끈한 고리로, 플러그(0.54 네모)를 **금테 + 보석 2겹**으로 키웠다."""
    top = qbez((-1.06, -1.14), (0.0, -1.30), (1.06, -1.14), 9)
    bottom = qbez((1.10, -1.90), (0.0, -2.06), (-1.10, -1.90), 9)
    band = dedup(top + [(1.12, -1.52)] + bottom + [(-1.12, -1.52)])
    shade = dedup([(1.10, -1.46)] + bottom + [(-1.10, -1.46)])
    gem = hexpad(0.44, -1.92, 0.44, 0.34)
    trim = arcslice(band, 0, len(band) - 1)
    ic = band.index(bottom[7])
    cable = [band[ic]] + qbez(band[ic], (-1.16, -2.66), (-0.72, -3.06), 6)[1:]
    bezel = ngon(-0.72, -3.44, 0.52, 12)
    drop = lozenge(-0.72, -3.44, 0.32, 0.32)
    return [Piece("CollarBand", band, True, True, 0, "fill"), Piece("CollarShade", shade, True, True, 2, "fillns"),
            Piece("CollarGem", gem, True, True, 1, "fill"), Piece("CollarCable", cable, False, False, 0, "line"),
            Piece("CollarBezel", bezel, True, True, 0, "fill"), Piece("CollarDrop", drop, True, True, 2, "fillns"),
            Piece("CollarTrim", trim, False, False, 3, "trim", host="CollarBand")]

def cyber_back():
    """Tarp Cape.  비트맵: 옥색 천 · **네 변 전부 크림 트림** · 밑단이 **뾰족한 3봉우리** · 어깨 위 금 베젤 보석.
    R2 대비 — 찢긴 랜덤 지그재그를 규칙적 3봉우리로 정리하고(비트맵 폭 프로파일 1.00→0.76→0.60→0.34) 트림을 넣었다."""
    front = qbez((1.10, -1.06), (1.30, -2.50), (1.26, -3.86), 6)
    hem = [(1.26, -3.86), (0.94, -4.34), (0.62, -3.74), (0.00, -4.38), (-0.62, -3.76), (-1.24, -4.36), (-1.86, -3.86)]
    back = qbez((-1.86, -3.86), (-1.78, -2.40), (-1.10, -1.06), 7)
    body = dedup([(-1.10, -1.06)] + front + hem[1:] + back[1:])
    sway = (body.index(hem[0]), len(hem))
    yoke = [(-1.06, -1.10), (1.06, -1.10), (1.22, -1.80), (-1.22, -1.80)]
    gem = hexpad(0.78, -1.20, 0.44, 0.34)
    trim = arcslice(body, 0, len(body) - 1)
    hi = [body[1], (1.02, -1.70), (1.06, -2.20)]
    return [Piece("CapeBody", body, True, True, 0, "fill", sway=sway), Piece("CapeYoke", yoke, True, True, 2, "fillns"),
            Piece("CapeGem", gem, True, True, 1, "fill"),
            Piece("CapeTrim", trim, False, False, 3, "trim", sway=sway, host="CapeBody"), Piece("CapeHighlight", hi, False, False, 3, "hi", host="CapeBody")]

# ═════════════════════════════════════════════════════════════════════════════
#  광부 pack.mine — 비트맵: 호박색 금속 + 검은 판/띠 + 크림 챙 + 원형 발광 램프
# ═════════════════════════════════════════════════════════════════════════════
def mine_head():
    """Miner Helmet.  비트맵: 호박색 돔 · **앞뒤로 지나는 검은 정수리 판** · 크림 챙 · 앞쪽 **큰 원형 램프**
    (밝은 렌즈 + 금속 베젤 링) · 옆면 검은 통풍 패널.
    R2 대비 — 네모 램프 -> **원형 램프 + 밝은 베젤 링**, 어두운 렌즈(tone 2)를 **주색 발광**으로 뒤집었다.
    비트맵에서 램프는 이 아이템에서 가장 밝은 물건인데 R2 는 가장 어둡게 칠하고 있었다."""
    shell = ellipse_arc(0.0, 0.30, 1.44, 1.60, 180, 0, 21)      # 돔을 높였다 — 종횡을 비트맵 1.203 에 붙이는 유일한 방향
    crest = dedup(ellipse_arc(0.0, 0.30, 1.30, 1.46, 158, 22, 11) + list(reversed(ellipse_arc(0.0, 0.30, 0.84, 0.98, 158, 22, 11))))
    brim = [(1.28, 0.90), (1.82, 0.70), (1.82, 0.20), (1.28, -0.02)]
    rimb = [(-1.28, 0.86), (-1.86, 0.58), (-1.86, 0.16), (-1.28, 0.02)]   # tau = 0.42/0.84 = 0.500 <= 0.55 (광부 쐐기)
    lamp = ngon(1.26, 0.92, 0.52, 12)
    ring = ngon(1.26, 0.92, 0.52, 12)
    vent = rrect(-1.02, 0.36, -0.28, 1.02, 0.20, 2)
    trim = arcslice(shell, 13, 20)
    hi = [shell[6], (-0.52, 1.62), (-0.06, 1.74)]
    return [Piece("HelmetShell", shell, True, True, 0, "fill"), Piece("HelmetRimBack", rimb, True, True, 1, "fill", layer=1),
            Piece("HelmetCrest", crest, True, True, 2, "fillns"), Piece("HelmetVent", vent, True, True, 2, "fillns"),
            Piece("HelmetBrim", brim, True, True, 1, "fill"), Piece("HelmetLamp", lamp, True, True, 0, "fill"),
            Piece("HelmetLampRing", ring, True, False, 3, "trim", host="HelmetLamp"), Piece("HelmetTrimFront", trim, False, False, 3, "trim", host="HelmetShell"),
            Piece("HelmetHighlight", hi, False, False, 3, "hi", host="HelmetShell")]

def mine_eyes():
    """Dust Goggles.  비트맵: **크림 테 안의 큰 호박색 렌즈** · 밑변 가운데가 코 자리로 V 파임 ·
    양옆 **검은 단자 블록** 안에 작은 호박색 표시등 · 위로 검은 끈.
    R2 대비 — 유리를 어둡게(tone 2) 칠하던 것을 **주색 발광 렌즈 + 밝은 테**로 뒤집었다(R2 는 «검은 안경»으로 읽힌다)."""
    lens = [(-1.18, 0.46), (1.18, 0.46), (1.32, 0.10), (1.02, -0.34), (0.34, -0.34),
            (0.00, -0.78), (-0.34, -0.34), (-1.02, -0.34), (-1.32, 0.10)]
    blkF = rrect(1.02, -0.28, 1.56, 0.44, 0.20, 2)
    blkB = rrect(-1.56, -0.28, -1.02, 0.44, 0.20, 2)
    slitF = wedge((1.28, 0.04), (1.00, -0.30), 0.68, 0.36)
    slitB = wedge((-1.28, 0.04), (-1.00, -0.30), 0.68, 0.36)
    trim = arcslice(lens, 0, len(lens) - 1)
    strap = [slitB[0]] + qbez(slitB[0], (-1.14, 0.70), (-0.42, 0.84), 5)[1:]     # 표시등 쐐기의 실재 꼭짓점에서 출발(닿음) -> 렌즈 윗변을 가로지른다
    return [Piece("GoggleLens", lens, True, True, 0, "fill"),
            Piece("GoggleBlockFront", blkF, True, True, 2, "fillns"), Piece("GoggleBlockBack", blkB, True, True, 2, "fillns"),
            Piece("GoggleSlitFront", slitF, True, True, 1, "fill"), Piece("GoggleSlitBack", slitB, True, True, 1, "fill"),
            Piece("GoggleStrap", strap, False, False, 0, "line", layer=1),
            Piece("GoggleTrim", trim, False, False, 3, "trim", host="GoggleLens")]

def mine_neck():
    """Mine Lamp.  비트맵: 가운데 **큰 원형 발광 렌즈 + 흰 베젤 링** · 어두운 몸통 · 좌우로 뻗은 **넓은 어두운 띠**와
    그 위 호박색 걸쇠 2개 · 위로 솟은 세로 부품.
    R2 대비 — 멜빵 낱선(V)을 **채운 가로 띠**로 바꾸고(비트맵 종횡 1.393) 렌즈를 발광으로 뒤집었다."""
    belt = [(-1.80, -1.92), (1.80, -1.92), (1.80, -2.54), (-1.80, -2.54)]
    top = rrect(-0.40, -1.92, 0.40, -1.12, 0.20, 2)
    body = rrect(-0.62, -2.90, 0.62, -1.62, 0.22, 2)
    lens = ngon(0.0, -2.26, 0.64, 12)
    ring = ngon(0.0, -2.26, 0.64, 12)
    belttrim = arcslice(belt, 0, 1)
    keepF = wedge((1.52, -1.82), (1.52, -2.64), 0.68, 0.36)
    keepB = wedge((-1.52, -1.82), (-1.52, -2.64), 0.68, 0.36)
    return [Piece("LampBelt", belt, True, True, 2, "fill"), Piece("LampTop", top, True, True, 2, "fill"),
            Piece("LampKeeperBack", keepB, True, True, 1, "fill"), Piece("LampKeeperFront", keepF, True, True, 1, "fill"),
            Piece("LampBody", body, True, True, 0, "fill"), Piece("LampLens", lens, True, True, 0, "fill"),
            Piece("LampLensRing", ring, True, False, 3, "trim", host="LampLens"), Piece("LampBeltTrim", belttrim, False, False, 3, "trim", host="LampBelt")]

def mine_back():
    """Pick Harness.  비트맵: **양날 곡괭이**(호박색 날 2장) · 가운데 검은 머리 블록 · 호박색 자루 + 검은 감기 ·
    **크림 멜빵** · 어깨 패드.
    R2 대비 — 외날 -> **양날**(비트맵의 결정적 실루엣), 끈을 주색 낱선 -> **크림 트림**으로."""
    p0, p1 = (0.72, -1.36), (-1.52, -4.40)
    ux, uy = p1[0]-p0[0], p1[1]-p0[1]; L = math.hypot(ux, uy); ux, uy = ux/L, uy/L
    nx, ny = -uy, ux
    hw = 0.26
    haft = [(p0[0]+nx*hw, p0[1]+ny*hw), (p1[0]+nx*hw, p1[1]+ny*hw), (p1[0]-nx*hw, p1[1]-ny*hw), (p0[0]-nx*hw, p0[1]-ny*hw)]
    ax = lambda t: (p0[0]+ux*L*t, p0[1]+uy*L*t)
    root = ax(0.88)
    ca, sa = math.cos(math.radians(34.0)), math.sin(math.radians(34.0))   # 34도 — 두 날이 «막대»가 아니라 V 로 읽히는 최소각(실측)
    dAx, dAy = nx*ca + ux*sa, ny*ca + uy*sa
    dBx, dBy = -nx*ca + ux*sa, -ny*ca + uy*sa
    tipA = (root[0]+dAx*1.26, root[1]+dAy*1.26)
    tipB = (root[0]+dBx*1.26, root[1]+dBy*1.26)
    headA = wedge(root, tipA, 1.04, 0.42)
    headB = wedge(root, tipB, 1.04, 0.42)
    block = [(root[0]+ux*0.54+nx*0.42, root[1]+uy*0.54+ny*0.42), (root[0]+ux*0.54-nx*0.42, root[1]+uy*0.54-ny*0.42),
             (root[0]-ux*0.54-nx*0.42, root[1]-uy*0.54-ny*0.42), (root[0]-ux*0.54+nx*0.42, root[1]-uy*0.54+ny*0.42)]
    pad = rrect(-0.12, -2.20, 0.96, -1.14, 0.24, 2)
    g1 = ax(0.56)      # 감기는 **한 줄**이다 — 어깨패드(t<=0.42)와 머리 블록(t>=0.737) 사이에 규칙 4(0.516R)를 지키며
    grip1 = [(g1[0]+nx*0.34, g1[1]+ny*0.34), (g1[0]-nx*0.34, g1[1]-ny*0.34)]   # 들어갈 수 있는 t 구간이 하나뿐이다
    strapP = arcslice(pad, 0, len(pad) - 1)
    strapH = arcslice(haft, 0, 3)
    return [Piece("PickHaft", haft, True, True, 0, "fill"),
            Piece("PickHeadFront", headA, True, True, 1, "fill"), Piece("PickHeadBack", headB, True, True, 1, "fill"),
            Piece("PickBlock", block, True, True, 2, "fill"), Piece("HarnessPad", pad, True, True, 0, "fill"),
            Piece("HaftGrip", grip1, False, False, 4, "seam"),
            Piece("HarnessStrap", strapP, False, False, 3, "trim", host="HarnessPad"), Piece("HaftTrim", strapH, False, False, 3, "trim", host="PickHaft")]

# ═════════════════════════════════════════════════════════════════════════════
#  대마법사 pack.arcane — 비트맵: 에메랄드 + 금 트림 + 마름모 보석 + 초승달(걸쇠)
# ═════════════════════════════════════════════════════════════════════════════
def arcane_head():
    """Wizard Hat.  비트맵: 에메랄드 원뿔 · **챙 가장자리를 금 테가 두른다** · 밑단 금 띠 · 띠 앞 **마름모 보석** ·
    꼭대기가 뒤로 꺾이고 그 근처에 보석.
    R2 대비 — 트림 2줄과 마름모 보석 2개를 새로 넣었다. 초승달(모티프 계약)은 남긴다 -> 문서 M-1."""
    far = qbez((-2.20, 0.34), (0.0, 1.62), (2.20, 0.34), 11)
    brim = dedup(far + [(2.08, -0.06), (1.80, -0.30), (1.48, -0.04), (1.20, 0.30), (0.0, 0.30), (-1.20, 0.30), (-1.46, -0.02), (-1.76, -0.18), (-2.06, -0.02)])
    F = ((1.40, 0.315), (0.85, 1.55), (-0.78, 2.40)); B = ((-0.78, 2.40), (-1.25, 1.55), (-1.40, 0.315))
    cone = dedup(qbez(*F, 10) + qbez(*B, 9)[1:])
    band = [(1.34, 0.36), (qbez_x_at_y(*F, 0.90, True), 0.90), (qbez_x_at_y(*B, 0.90, False), 0.90), (-1.34, 0.36)]
    moon = crescent(-1.30, 1.10, 0.74, 0.57, 0.30, 200.0)
    gem = lozenge(1.24, 0.62, 0.32, 0.34)
    tipgem = lozenge(-0.10, 2.10, 0.32, 0.30)
    trimB = arcslice(brim, 0, 10)
    trimC = arcslice(cone, 0, 9)          # 앞변만 — 윤곽 전체를 트림하면 모자가 «흰 X»로 읽힌다
    return [Piece("HatBrim", brim, True, True, 0, "fill"), Piece("HatCone", cone, True, True, 0, "fill"),
            Piece("HatBand", band, True, True, 2, "fillns"), Piece("HatMoon", moon, True, True, 1, "fill"),
            Piece("HatGem", gem, True, True, 0, "fill"), Piece("HatTipGem", tipgem, True, True, 0, "fill"),
            Piece("HatTrimBrim", trimB, False, False, 3, "trim", host="HatBrim"), Piece("HatTrimCone", trimC, False, False, 3, "trim", host="HatCone")]

def arcane_eyes():
    """Astro Lens.  비트맵: **하나의 큰 원형 금테 안에 에메랄드 돔** · 베젤 둘레에 마름모 3~4개 ·
    뒤로 휘어 나가는 금 팔.
    R2 대비 — 렌즈 2개(안경) -> **렌즈 1개(관측 기구)**. 비트맵은 안경이 아니라 «외눈 관측기»다.
    두 눈 커버는 큰 판 하나(r 0.86 > EYE_X 0.341)가 그대로 만족한다."""
    disc = ngon(0.0, 0.04, 0.86, 16)
    core = ngon(0.0, 0.04, 0.44, 12)
    ptT = lozenge(0.0, 0.80, 0.32, 0.34)
    cradle = crescent(0.0, -1.10, 0.96, 0.78, 0.30, 90.0)
    bez = arcslice(disc, 0, len(disc) - 1)
    arm = [disc[7]] + qbez(disc[7], (-1.22, 0.52), (-1.46, 0.92), 5)[1:]
    return [Piece("LensDisc", disc, True, True, 0, "fill"), Piece("LensCore", core, True, True, 2, "fillns"),
            Piece("LensPointTop", ptT, True, True, 0, "fill"), Piece("LensCradle", cradle, True, True, 1, "fill"),
            Piece("LensArm", arm, False, False, 0, "line"), Piece("LensBezel", bez, False, False, 3, "trim", host="LensDisc")]

def arcane_neck():
    """Moon Clasp.  비트맵: 가운데 **금 초승달(뿔이 위로)** + 둥근 초록 구슬 · 좌우로 뻗은 **금 트림 두른 칼라 판** ·
    아래로 마름모 펜던트.
    R2 대비 — V 칼라 -> **좌우 수평 판**(비트맵 폭 프로파일이 5~9단에서 1.00) · 초승달을 아래(270°) -> **위(90°)** 로 뒤집었다."""
    plateB = [(-1.48, -1.28), (-0.52, -1.28), (-0.52, -1.92), (-1.48, -1.92)]
    plateF = [(0.52, -1.28), (1.48, -1.28), (1.48, -1.92), (0.52, -1.92)]
    moon = crescent(0.0, -1.72, 0.92, 0.74, 0.30, 90.0)
    orb = ngon(0.0, -1.66, 0.54, 12)
    linB = [(-1.44, -1.38), (-0.86, -1.38), (-0.86, -1.90), (-1.44, -1.90)]   # 판 «바깥 끝»만 그늘 — 안감이 판 전체를 덮으면 빈 상자가 된다
    linF = [(0.86, -1.38), (1.44, -1.38), (1.44, -1.90), (0.86, -1.90)]
    chain = [(0.0, -2.50), (0.0, -3.30)]
    drop = lozenge(0.0, -3.58, 0.34, 0.36)
    trimB = arcslice(plateB, 0, 3)
    trimF = arcslice(plateF, 0, 3)
    return [Piece("ClaspPlateBack", plateB, True, True, 0, "fill"), Piece("ClaspPlateFront", plateF, True, True, 0, "fill"),
            Piece("ClaspLiningBack", linB, True, True, 2, "fillns"), Piece("ClaspLiningFront", linF, True, True, 2, "fillns"),
            Piece("ClaspMoon", moon, True, True, 1, "fill"), Piece("ClaspOrb", orb, True, True, 2, "fillns"),
            Piece("ClaspChain", chain, False, False, 4, "seam"), Piece("ClaspDrop", drop, True, True, 0, "fill"),
            Piece("ClaspTrimBack", trimB, False, False, 3, "trim", host="ClaspPlateBack"), Piece("ClaspTrimFront", trimF, False, False, 3, "trim", host="ClaspPlateFront")]

def arcane_back():
    """Moon Robe.  비트맵: 짙은 에메랄드 로브 · **세운 칼라** · **모든 가장자리를 금 트림이 두른다**(트림 13.5%) ·
    **뾰족한 다봉 밑단** · 칼라 밑동에 마름모 보석.
    R2 대비 — 초승달 밑단 -> **다봉 밑단**(비트맵 폭 프로파일 0.97→0.87→0.77→0.72→0.67→0.54) · 트림 · 세운 칼라."""
    F = ((1.12, -1.20), (1.82, -2.60), (1.74, -4.20)); B = ((-1.74, -4.20), (-1.82, -2.60), (-1.12, -1.20))
    hem = [(1.74, -4.20), (1.22, -4.66), (0.74, -4.08), (0.00, -4.80), (-0.74, -4.08), (-1.22, -4.66), (-1.74, -4.20)]
    body = dedup([(-1.12, -1.20)] + qbez(*F, 7) + hem[1:] + qbez(*B, 7)[1:])
    sway = (body.index(hem[0]), len(hem))
    yokeF = [(0.42, -1.22), (1.08, -1.22), (1.62, -3.30), (0.60, -3.30)]
    yokeB = [(-1.08, -1.22), (-0.42, -1.22), (-0.60, -3.30), (-1.62, -3.30)]
    collar = [(-0.76, -1.20), (-0.64, -0.50), (0.64, -0.50), (0.76, -1.20)]
    moon = crescent(0.0, -3.70, 0.98, 0.76, 0.34, 270.0)
    gem = lozenge(0.0, -1.12, 0.34, 0.36)
    trim = arcslice(body, 0, len(body) - 1)
    return [Piece("RobeBody", body, True, True, 0, "fill", sway=sway), Piece("RobeYokeBack", yokeB, True, True, 2, "fillns"), Piece("RobeYokeFront", yokeF, True, True, 2, "fillns"),
            Piece("RobeCollar", collar, True, True, 0, "fill"), Piece("RobeMoon", moon, True, True, 1, "fill"),
            Piece("RobeGem", gem, True, True, 0, "fill"),
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

if __name__ == "__main__":
    R2.PACKS = PACKS
    R2.FILES = FILES
    import pack_detail_r3_extra as EX
    EX.main(PACKS, FILES)
