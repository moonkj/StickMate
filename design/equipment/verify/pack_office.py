# -*- coding: utf-8 -*-
"""★ DLC 팩 1호 "오피스 워커" 4종 좌표 + 게이트 (2026-09-02, design-equipment)

왜 오피스로 증명하는가
----------------------
6팩 중 **조형 자유도가 가장 낮은 팩**이기 때문이다. 사이버/네온/밀리터리는 안테나·가시·헬멧처럼
실루엣을 마음대로 부술 수 있다. 오피스는 사무실에 실재하는 밋밋한 물건만으로 기존 30종과
1획 이상 갈라져야 한다. **여기서 통과하는 규칙은 나머지 5팩에서 여유가 남는다.**
게다가 오피스는 **기본 팩**이라 전원에게 나간다 — 실패 비용도 가장 크다.

좌표계: 머리 중심 원점 · R 배수 · +x 진행 방향 (docs/EQUIPMENT_SHAPE_SPEC.md 1절과 같다)

    python3 pack_office.py            # 전수 게이트 (30종 + 오피스 4종 = 34종)
    python3 pack_office.py --control  # ★ 양성 대조: 일부러 나쁜 값 → 게이트가 빨간불을 내는가
    python3 pack_office.py --dump     # 좌표 전문
"""
import sys, os, math, types
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, headroom as H, sectors as S
from rig import Shape, W

SH, TL = rig.SHOULDER_R, rig.TORSO_R
NECKY  = SH + 0.04
COLLARY = SH + 0.10

# ============================================================================
# ★ 팩 예약 대역 (sectors.py의 빈 대역 지도에서 고른 자리)
#   "이 각도 구간에서 오피스 4종만 멀리 나간다" — 아이템이 몇 개 더 늘든 안 흔들리는 하한.
# ============================================================================
OFFICE_SECTORS = {          # 슬롯: (시작각, 구간수)
    "HEAD": (320.0, 3),     # 앞-아래 대각. 챙 6종이 −25°~+5°에 몰려 있어 여기가 비어 있다
    "EYES": (250.0, 3),     # 아래-뒤 (좌우 대칭이라 280°도 함께 확보된다)
    "NECK": ( 35.0, 3),     # 어깨 위 (넥타이·목도리는 전부 아래로만 간다)
    "BACK": (310.0, 3),     # 앞-아래 (망토 5종은 전부 뒤로만 간다)
}
EYES_MIRROR_SECTOR = (280.0, 3)

# ============================================================================
# ★ 팩 모티프 — 보조색 조각을 4종에서 **같은 부류**로 통일한다.
#   오피스 = "납작한 4각 판"(서류·명찰·모니터). 이것이 세트 완성 인지 장치의 본체다(5-3).
#   두께 하한은 취향이 아니라 규칙 1-C가 정한다: rho_max >= 0.21818R  =>  판 두께 >= 0.436R.
# ============================================================================
MOTIF_VERTS = 4
MOTIF_THICK = 0.46          # 판 두께 (>= 0.436R = 규칙 1-C)
MOTIF_ASPECT = (1.50, 2.20) # 종횡비 창


def plate(cx, cy, length, thick, deg):
    """모티프 판 하나. 중심 (cx,cy), 긴변 length, 두께 thick, 긴변 방향 deg."""
    a = math.radians(deg)
    ux, uy = math.cos(a), math.sin(a)
    nx, ny = -uy, ux
    hl, ht = length * 0.5, thick * 0.5
    return [(cx + ux*hl + nx*ht, cy + uy*hl + ny*ht),
            (cx + ux*hl - nx*ht, cy + uy*hl - ny*ht),
            (cx - ux*hl - nx*ht, cy - uy*hl - ny*ht),
            (cx - ux*hl + nx*ht, cy - uy*hl + ny*ht)]


def arcpts(r, d0, d1, n):
    return [(math.cos(math.radians(d0 + (d1-d0)*i/(n-1)))*r,
             math.sin(math.radians(d0 + (d1-d0)*i/(n-1)))*r) for i in range(n)]


# ============================================================================
# 1) HEAD — 콜센터 헤드셋 (equip.head.headset)   등급: 영웅
#    부피를 머리 원 **밖**에서 만든다: 관이 없다. 밴드는 머리 위를 **지나가고**,
#    덩어리는 귀덮개(옆)와 마이크 판(앞-아래)에 있다.
# ============================================================================
def headset():
    # 밴드 = 닫힌 띠(바깥호 + 안쪽호). 안쪽호를 8분할하면 현이 0.30R(0.70획)로 내려간다 —
    # 규칙 1 린트는 꺾임 45° 미만이라 건너뛰지만, 그건 린트의 사각지대이지 안전이 아니다.
    outer = arcpts(1.32, 160.0, 22.0, 7)
    inner = arcpts(0.88,  22.0, 160.0, 5)   # 6분할이면 현이 0.42R = 0.98획@0.60로 하한 미달
    band  = outer + inner
    # 귀덮개 — 모든 변 >= 0.44R. 첫 시안(세로변 0.32R)은 배율 0.75에서, 둘째 시안(0.40R/빗변
    # 0.428R)은 **배율 0.60에서 0.996획**으로 깼다. 0.75만 보고 통과시키면 안 되는 자리다.
    cup   = [(0.50, 0.14), (0.86, 0.42), (1.22, 0.14),
             (1.22,-0.30), (0.86,-0.58), (0.50,-0.30)]
    mic   = plate(1.44, -0.72, 0.72, MOTIF_THICK, 0.0)     # ★ 모티프 판 (예약 대역 320°~330°)
    # 붐대는 **두 점짜리 곧은 선**이다. 세 점으로 꺾으면 꺾임 변이 0.93획으로 규칙 1을 깬다
    # (첫 시안이 그렇게 죽었다 — 이 배율에서 "짧게 꺾인 선"은 존재할 수 없다).
    boom  = [outer[-1], mic[3]]                            # 밴드 앞끝 → 마이크 판 왼위 (규칙 4-a)
    return [Shape("HeadsetBand", band, filled=True),
            Shape("HeadsetCup",  cup,  filled=True),
            Shape("HeadsetBoom", boom, loop=False, tone=2),
            Shape("HeadsetMic",  mic,  filled=True, tone=1)]


# ============================================================================
# 2) EYES — 모니터 안경 (equip.eyes.office)      등급: 희귀
#    렌즈는 위아래로 **쪼갠다**(겹치지 않는다 — 9-3절 그리기 순서 함정 회피).
#    아래 절반이 보조색 = 모니터 반사광 = 모티프 판.
# ============================================================================
LENS_TOP, LENS_MID, LENS_BOT = 0.50, 0.04, -0.42
def office_glasses():
    back = [(-0.24, LENS_TOP), (-1.10, LENS_TOP), (-1.10, LENS_BOT), (-0.24, LENS_BOT)]
    frontUpper = [(0.24, LENS_TOP), (0.24, LENS_MID), (1.10, LENS_MID), (1.10, LENS_TOP)]
    glare = [(0.24, LENS_MID), (0.24, LENS_BOT), (1.10, LENS_BOT), (1.10, LENS_MID)]
    cord  = [(-1.10, LENS_BOT), (-0.46,-1.26), (0.44,-1.26), (1.10, LENS_BOT)]
    return [Shape("OfficeLensBack",  back,       filled=True),
            Shape("OfficeLensFront", frontUpper, filled=True),
            Shape("OfficeCord",      cord, loop=False, tone=2),
            Shape("OfficeGlare",     glare,      filled=True, tone=1)]


# ============================================================================
# 3) NECK — 사원증 목줄 (equip.neck.badge)       등급: 일반
#    도형 2개. 카테고리에서 유일하게 줄이 **어깨 위로 올라간다**(예약 대역 35°~45°).
# ============================================================================
def badge():
    cord = [(-0.82,-0.62), (-0.30,-1.52), (0.00,-1.98), (0.34,-1.52), (0.82,-0.62)]
    card = plate(0.00, -2.26, 0.92, 0.48, 0.0)
    return [Shape("LanyardCord", cord, loop=False),
            Shape("BadgeCard",   card, filled=True, tone=1)]


# ============================================================================
# 4) BACK — 어깨에 걸친 재킷 (equip.shoulders.blazer)   등급: 전설
#    망토 5종은 전부 **뒤로만** 흐른다. 재킷은 소매 하나가 **앞으로** 늘어진다.
# ============================================================================
def blazer():
    # ★ 제비꼬리 밑단 — 뒤/앞 두 자락이 **거울쌍이 아니게** 어긋나 있다(돌출지수 2를 만든다).
    drape = [(0.40, COLLARY), (-0.62, COLLARY+0.04), (-1.55,-3.42),
             (-1.35,-4.34), (-0.42,-3.50), (0.30,-4.34), (0.86,-3.30)]
    cuff = plate(1.28, -3.02, 0.80, MOTIF_THICK, 10.2)          # ★ 모티프 판 (예약 310°~320°)
    sleeve = [(0.40, COLLARY), (0.98,-1.36), cuff[0], cuff[3], (0.58,-1.66)]
    fold = [(-0.28,-1.34), (-0.96,-3.52)]
    return [Shape("BlazerDrape",  drape,  filled=True),
            Shape("BlazerSleeve", sleeve, filled=True),
            Shape("BlazerFold",   fold, loop=False, tone=2),
            Shape("BlazerCuff",   cuff,   filled=True, tone=1)]


PACK = {
    "HEAD": ("헤드셋",   headset()),
    "EYES": ("모니터안경", office_glasses()),
    "NECK": ("사원증",   badge()),
    "BACK": ("걸친재킷",  blazer()),
}
PACK_COVER = {"헤드셋": float('inf')}     # HatCoverLocalY — 머리카락을 안 가린다(hidesHair=false)
#: 선언 등급. **측정으로 검증한다**(sectors.item_tier). 손으로 적은 등급과 측정이 어긋나면 빨간불.
GRADE = {"헤드셋": "전설", "모니터안경": "영웅", "사원증": "희귀", "걸친재킷": "전설"}


# ---------------------------------------------------------------------------
def install(pack=None):
    """items.py 표에 팩 4종을 **더해서** 끼워 넣고 verify.py를 그대로 실행할 준비를 한다."""
    pack = pack or PACK
    import items as real, hair
    m = types.ModuleType("items")
    m.HEAD, m.EYES = dict(real.HEAD), dict(real.EYES)
    m.NECK, m.BACK = dict(real.NECK), dict(real.BACK)
    for slot, (name, shapes) in pack.items():
        getattr(m, slot)[name] = shapes
    m.EYE_FRONT_ONLY = real.EYE_FRONT_ONLY
    m.COVER = dict(real.COVER); m.COVER.update(PACK_COVER)
    sys.modules["items"] = m
    return m


def run_verify(pack=None, title="오피스 워커 팩 + 기존 30종"):
    m = install(pack)
    os.chdir(os.path.dirname(os.path.abspath(__file__)))
    print("── 대상: %s ──" % title)
    src = open("verify.py", encoding="utf-8").read()
    g = {"__name__": "__main__"}
    exec(compile(src, "verify.py", "exec"), g)
    return m, g.get("fail", 0)


# ---- 규칙 1-C(색면 조건) — hatfix.py의 자를 그대로 쓴다 ----------------------
import hatfix
FILL_PEN = hatfix.FILL_OUTLINE_PEN_IN_R


def rule_1c(pack):
    bad = []
    print("╔══ 규칙 1-C 색면 조건 (ρ_max >= %.5fR) ══╗" % FILL_PEN)
    for slot, (name, shapes) in pack.items():
        for s in shapes:
            if not s.filled: continue
            r = hatfix.rho_max(s.pts); k = r / FILL_PEN
            if k < 1.0: bad.append("%s %s %.4fR" % (name, s.name, r))
            print("  %s %-6s %-16s ρ_max %.4fR = %.2f획" % ("OK " if k >= 1.0 else "✗  ", name, s.name, r, k))
    print("╚══ 위반 %d건 ══╝" % len(bad))
    return bad


def sector_report(m, pack):
    print("╔══ 예약 대역 (여유 하한 %.4fR = 래칫) ══╗" % S.SECTOR_CLEARANCE_R)
    bad = []
    for slot, (name, shapes) in pack.items():
        anchor = rig.SHOULDER_R if slot in ("NECK", "BACK") else 0.0
        table = {k: v for k, v in getattr(m, slot).items() if k != name}
        secs = [OFFICE_SECTORS[slot]] + ([EYES_MIRROR_SECTOR] if slot == "EYES" else [])
        for sec in secs:
            a, b, gap, ok = S.sector_check(shapes, table, sec, anchor)
            if not ok: bad.append("%s %s %.0f° 여유 %.3fR" % (slot, name, sec[0], gap))
            print("  %s %-4s %-6s %3.0f°~%3.0f°  내 %.2fR / 남 %.2fR / 여유 %.3fR"
                  % ("OK " if ok else "✗  ", slot, name, sec[0], sec[0] + sec[1]*5, a, b, gap))
    print("╚══ 위반 %d건 ══╝" % len(bad))
    return bad


def ratchet_report(m):
    """★ 래칫은 상수가 아니다 — **기존 30종에서 잰 슬롯별 최소값**이다.
    "1.50획"처럼 반올림한 숫자를 상수로 박으면 기준선(실측 1.4974획)보다 높아져 손도 안 댄
    기존 쌍이 빨간불을 낸다(첫 시안이 그랬다). 기준선은 재서 얻는다."""
    import importlib, items as _cur
    sys.modules.pop("items", None)
    base = importlib.import_module("items")            # 팩을 안 끼운 원본
    print("╔══ 실루엣 래칫 (하한 %.4fR = 1획@0.60 / 기준선 = 기존 30종 실측) ══╗" % S.SILHOUETTE_FLOOR_R)
    bad = []
    for slot, anchor in (("HEAD", 0.0), ("EYES", 0.0), ("NECK", rig.SHOULDER_R), ("BACK", rig.SHOULDER_R)):
        b0 = S.pairwise_table(getattr(base, slot), anchor)[0][0]
        d, x, y = S.pairwise_table(getattr(m, slot), anchor)[0]
        tag = "OK "
        if d < b0 - 1e-9: tag = "✗  "; bad.append("%s 래칫 %.4f -> %.4f (%s vs %s)" % (slot, b0, d, x, y))
        if d < S.SILHOUETTE_FLOOR_R: tag = "✗  "; bad.append("%s 하한 %.4fR (%s vs %s)" % (slot, d, x, y))
        print("  %s %-4s 기준선 %.4fR -> 팩 포함 %.4fR = %.2f획@0.75 = %.2f획@0.60   (%s vs %s)"
              % (tag, slot, b0, d, d / S.W075, d / S.W060, x, y))
    sys.modules["items"] = _cur
    print("╚══ 래칫 위반 %d건 ══╝" % len(bad))
    return bad


def motif_report(pack):
    print("╔══ 세트 인지 장치 — 팩 모티프 (보조색 조각 4개) ══╗")
    rows = []
    for slot, (name, shapes) in pack.items():
        acc = [s for s in shapes if s.tone == 1]
        s = acc[0]
        x0, y0, x1, y1 = rig.bounds(s.pts)
        L = max(x1-x0, y1-y0); T = min(x1-x0, y1-y0)
        # 회전한 판이면 경계상자로는 못 잰다 — 변 길이로 잰다.
        n = len(s.pts)
        e = sorted(math.dist(s.pts[i], s.pts[(i+1) % n]) for i in range(n))
        if n == 4: L, T = (e[2]+e[3])/2, (e[0]+e[1])/2
        cy = sum(p[1] for p in s.pts)/n
        rows.append((slot, name, s.name, n, L, T, L/T, cy, hatfix.rho_max(s.pts)))
    bad = []
    for slot, name, sn, n, L, T, a, cy, rho in rows:
        ok = (n == MOTIF_VERTS and MOTIF_ASPECT[0] <= a <= MOTIF_ASPECT[1]
              and T >= MOTIF_THICK - 1e-6)
        if not ok: bad.append("%s %s (n=%d 종횡비 %.2f 두께 %.3f)" % (slot, name, n, a, T))
        print("  %s %-4s %-6s %-14s 꼭짓점 %d  긴변 %.2fR  두께 %.2fR  종횡비 %.2f  중심y %+.2fR  ρ %.3fR"
              % ("OK " if ok else "✗  ", slot, name, sn, n, L, T, a, cy, rho))
    # ★ 첫 시안은 "인접 y 간격의 최대/최소 <= 2.6"(등간격 사다리)을 걸었다가 3.40으로 깨졌다.
    #   깨진 이유가 설계 결함이 아니라 **규칙이 틀렸기 때문**이다 — 네 모티프가 붙는 자리는
    #   머리/눈/가슴/허리라서 등간격일 이유가 없다. 규칙을 다음 둘로 바꾼다:
    #     (a) 서로 뭉쳐 보이지 않는다  : 중심 사이 거리 >= 0.90R
    #     (b) 세로로 흩어져 보인다     : 중심 y가 서로 >= 0.40R 떨어진다
    cs = [(r[7], r[1]) for r in rows]
    cxy = {r[1]: (sum(p[0] for p in [q for q in []]) , 0) for r in rows}
    pts = []
    for slot, (name, shapes) in pack.items():
        s0 = [q for q in shapes if q.tone == 1][0]
        n0 = len(s0.pts)
        pts.append((name, sum(p[0] for p in s0.pts)/n0, sum(p[1] for p in s0.pts)/n0))
    worst_d = min((math.dist(a[1:], b[1:]), a[0], b[0]) for i, a in enumerate(pts) for b in pts[i+1:])
    worst_y = min((abs(a[2]-b[2]), a[0], b[0]) for i, a in enumerate(pts) for b in pts[i+1:])
    print("  %s 모티프 최소 중심거리 %.2fR (>= 0.90)  %s vs %s" %
          ("OK " if worst_d[0] >= 0.90 else "✗  ", worst_d[0], worst_d[1], worst_d[2]))
    print("  %s 모티프 최소 y 간격 %.2fR (>= 0.40)  %s vs %s" %
          ("OK " if worst_y[0] >= 0.40 else "✗  ", worst_y[0], worst_y[1], worst_y[2]))
    if worst_d[0] < 0.90: bad.append("모티프 중심거리 %.2fR" % worst_d[0])
    if worst_y[0] < 0.40: bad.append("모티프 y간격 %.2fR" % worst_y[0])
    # 착용 크기에서 보이는가 — 판의 짧은 변이 배율 0.60에서 1획 이상인가
    for slot, name, sn, n, L, T, a, cy, rho in rows:
        k = T / S.W060
        if k < 1.0: bad.append("%s %s 판 두께 %.2f획@0.60" % (slot, name, k))
        print("  %s %-4s 판 두께 %.2f획@0.60 / %.2f획@0.75" % ("OK " if k >= 1.0 else "✗  ", slot, T/S.W060, T/S.W075))
    print("╚══ 모티프 위반 %d건 ══╝" % len(bad))
    return bad


def grade_report(m, pack):
    print("╔══ 등급 조형 규칙 (부품 / 돌출 / 보조색 중 가장 높은 것) ══╗")
    bad = []
    for slot, (name, shapes) in pack.items():
        anchor = rig.SHOULDER_R if slot in ("NECK", "BACK") else 0.0
        table = {k: v for k, v in getattr(m, slot).items() if k != name}
        tier, (p, g, a) = S.item_tier(shapes, anchor, table, OFFICE_SECTORS[slot])
        got = S.TIERS[tier]
        ok = got == GRADE[name]
        if not ok: bad.append("%s 선언 %s ≠ 측정 %s" % (name, GRADE[name], got))
        print("  %s %-6s 선언 %-2s / 측정 %-2s   (부품 %s · 돌출 %s · 보조색 %s)"
              % ("OK " if ok else "✗  ", name, GRADE[name], got, S.TIERS[p], S.TIERS[g], S.TIERS[a]))
    print("╚══ 등급 위반 %d건 ══╝" % len(bad))
    return bad


#: ★ 신규 아이템 전용 하한 — **꺾임 문턱과 무관한 모든 변**이 배율 0.60에서 1획 이상.
#   프로덕션 규칙 1은 "양끝이 45° 이상 꺾인 변"만 본다. 42° 꺾임은 눈에 보이지만 린트는 넘긴다
#   (양성 대조 1번이 이 사각지대를 뚫고 지나갔다 — 그래서 신규 24종에는 이 하한을 따로 건다).
MIN_EDGE_FLOOR_R = S.W060


def min_edge_report(pack):
    print("╔══ 최단 실제 변 (꺾임 문턱 무관 · 하한 %.4fR = 1획@0.60) ══╗" % MIN_EDGE_FLOOR_R)
    bad = []
    for slot, (name, shapes) in pack.items():
        for s in shapes:
            n = len(s.pts); best = None; where = None
            for i in range(n if s.loop else n - 1):
                L = math.dist(s.pts[i], s.pts[(i+1) % n])
                if L < 1e-9: continue
                if best is None or L < best: best, where = L, i
            if best is None: continue
            ok = best >= MIN_EDGE_FLOOR_R - 1e-9
            if not ok: bad.append("%s %s 변%d %.4fR" % (name, s.name, where, best))
            print("  %s %-6s %-16s 최단 %.4fR = %.2f획@0.60 = %.2f획@0.75 (변 %d)"
                  % ("OK " if ok else "✗  ", name, s.name, best, best / S.W060, best / S.W075, where))
    print("╚══ 최단 변 위반 %d건 ══╝" % len(bad))
    return bad


def headroom_report(pack):
    print("╔══ 남는 머리 (모자 슬롯만) ══╗")
    bad = []
    name, shapes = pack["HEAD"]
    for sc in H.HEADROOM_GATE_SCALES:
        w = H.stroke_in_R(sc); mm = H.measure(shapes, w)
        th = mm['depth']*2.0/w
        ok = th >= H.HEADROOM_THICKNESS_FLOOR_W and mm['area'] >= H.HEADROOM_AREA_FLOOR
        if not ok: bad.append("%s @%.2f 두께 %.2f획 면적 %.1f%%" % (name, sc, th, mm['area']*100))
        # 13-2 "머리 위 색 밴드 >= 2획" — x=0 수직선의 잉크 두께
        sp = H.ink_spans(shapes, 0.60, w)   # 머리 위쪽(y=0.60)에서의 밴드가 아니라 x=0 세로 두께를 따로 잰다
        band = _vertical_band(shapes, w)
        print("  %s %-6s @%.2f  두께 %5.2f획  면적 %5.1f%%  외곽호 %5.1f°  잉크밑단 %+.3fR  머리위 색밴드 %.2f획"
              % ("OK " if ok else "✗  ", name, sc, th, mm['area']*100, mm['arc'], mm['ink_bottom'], band/w))
    print("╚══ 남는 머리 위반 %d건 ══╝" % len(bad))
    return bad


def _vertical_band(shapes, w):
    """x=0 수직선에서 모자 잉크가 차지하는 세로 길이(R). 13-2의 '머리 위 색 밴드'."""
    lo, hi, hit = 9.9, -9.9, False
    N = 4001
    for k in range(N):
        y = -1.0 + 4.0 * k / (N - 1)
        if any(a <= 0.0 <= b for a, b in H.ink_spans(shapes, y, w)):
            lo = min(lo, y); hi = max(hi, y); hit = True
    return (hi - lo) if hit else 0.0


def main(pack=None, title=None):
    pack = pack or PACK
    m, fail = run_verify(pack, title or "오피스 워커 팩 4종 + 기존 30종 (= 34종)")
    print()
    bad = []
    bad += min_edge_report(pack); print()
    bad += rule_1c(pack); print()
    bad += sector_report(m, pack); print()
    bad += ratchet_report(m); print()
    bad += motif_report(pack); print()
    bad += grade_report(m, pack); print()
    bad += headroom_report(pack)
    print()
    print("★ verify.py 위반 %d건 + 팩 전용 게이트 위반 %d건 = 총 %d건" % (fail, len(bad), fail + len(bad)))
    for b in bad: print("   · %s" % b)
    return fail + len(bad)


if __name__ == "__main__":
    if "--dump" in sys.argv:
        for slot, (name, shapes) in PACK.items():
            print("== %s %s (%s) ==" % (slot, name, GRADE[name]))
            for s in shapes:
                print("  %-16s loop=%d fill=%d tone=%d  %s" % (
                    s.name, s.loop, s.filled, s.tone,
                    " ".join("(%+.4f,%+.4f)" % p for p in s.pts)))
    elif "--control" in sys.argv:
        # ★ import 로 부르면 `__name__ == "__main__"` 이 아니라 **아무것도 안 돌고 조용히 끝난다**
        #   — 그게 정확히 지난 라운드의 "거짓 초록"이었다. 여기서는 main 으로 실행한다.
        src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                "control_pack.py"), encoding="utf-8").read()
        exec(compile(src, "control_pack.py", "exec"), {"__name__": "__main__"})
    else:
        sys.exit(1 if main() else 0)
