# -*- coding: utf-8 -*-
"""★ DLC 팩 제안 「야간 정비반 (Night Shift)」 — 좌표 + 전수 게이트  (design-equipment)

DS-2 제약: 팩 1개 = 스탯 4슬롯(HEAD/EYES/NECK/BACK) 각 1종 + 외형 2종 = 정확히 6종.
전부 같은 등급(희귀) · 전부 Lv.1 · 0동전 즉시 해금 · DLC 전용(기본 42종과 불중복).

좌표계: 머리 중심 원점 · R 배수 · +x = 진행 방향 (EQUIPMENT_SHAPE_SPEC 1절과 같다).
NECK/BACK 도 이 문서 규약대로 **절대 y**로 적고, 게이트가 어깨 기준으로 환산한다.

  python3 pack_nightshift.py            # 전수 게이트
  python3 pack_nightshift.py --dump     # 좌표 전문
  python3 pack_nightshift.py --control  # ★ 양성 대조(일부러 나쁜 값 -> 빨간불이 켜지는가)
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, sectors as S, headroom as H
from rig import Shape

R = 1.0
def pol(deg, r): return (math.cos(math.radians(deg)) * r, math.sin(math.radians(deg)) * r)
def arc(r, d0, d1, n): return [pol(d0 + (d1 - d0) * i / (n - 1), r) for i in range(n)]


# ═══════════════════════════════════════════════════════════════════════════
# 1. HEAD — 목덮개 작업모 (Havelock Work Cap)   equip.head.havelock
#    관은 y=+0.50 위에만 있다 = **얼굴 띠를 비운다**(팩 EYES가 6/6에서 살아남게).
#    감쌈(원칙 4)은 관이 아니라 **뒤로 늘어진 목덮개**가 진다.
# ═══════════════════════════════════════════════════════════════════════════
def head_havelock():
    # ★ 3번 고침. 관 밑변 +0.50 -> +0.72. 이유는 inkload.py 의 한 줄이다:
    #   6/6 상태의 **머리 노출**이 기본 한 벌 중앙(12.6%@0.75)보다 낮으면, 그건 사용자가
    #   실제로 신고한 결함("모자가 머리 전체를 가림")을 팩이 다시 만드는 것이다.
    #   관·렌즈·마스크를 전부 위로 올려 **얼굴 아래-뒤 사분면을 통째로 비운다.**
    crown = [(0.80, 0.72), (1.02, 1.16), (0.50, 1.54), (-0.50, 1.54), (-1.02, 1.16), (-0.80, 0.72)]
    # 폭 0.68R 은 하한이다(잉크 사각형 ≥ 1.5획@0.60 = 0.645R). 앞이 아니라 **뒤로** 넓혔다 —
    # 앞으로 넓히면 머리 노출을 다시 깎는다.
    cloth = [(-0.80, 0.72), (-1.24, 0.34), (-1.48, -0.34), (-1.28, -0.98),
             (-0.82, -0.90), (-0.80, -0.16)]
    seam = [(-0.78, 0.86), (0.78, 0.82)]
    return [Shape("CapCrown", crown, True, filled=True),
            Shape("CapNeckCloth", cloth, True, filled=True, tone=1),   # ★보조색
            Shape("CapSeam", seam, False)]


# ═══════════════════════════════════════════════════════════════════════════
# 2. EYES — 방진 고글 (Respirator Goggles)   equip.eyes.respirator
#    EYES 봉투는 0°/180°(가로)에 여유가 0.07R뿐이다(freeband.py) — 유일한 빈 방향이 **아래**.
#    그래서 식별 잉크(마스크 컵)를 255~290° 대역에 둔다.
# ═══════════════════════════════════════════════════════════════════════════
def eyes_respirator():
    # 렌즈는 **한 장 판**이다(기본 6종에 없는 어휘 — 2렌즈/띠+렌즈/외알/윗테/천 뿐).
    # 세로 0.66R 은 임의값이 아니라 하한이다: 잉크 사각형 ≥ 1.5획@0.60 = 0.645R.
    lens = [(-1.06, 0.68), (1.06, 0.68), (1.02, 0.02), (-1.02, 0.02)]
    # 마스크 컵 = 이 아이템의 보조색. EYES 봉투가 유일하게 비어 있는 방향(255~290°)을 예약한다.
    # 측면도에서 방진 마스크는 **얼굴 앞면만** 감싼다 -> x ≥ +0.02 (뒤쪽 뺨을 비운다).
    cup = [(0.06, 0.00), (0.70, 0.00), (1.04, -0.38), (0.96, -0.92),
           (0.42, -1.16), (0.02, -0.76)]
    strap = [(-1.04, 0.56), (-1.52, 0.92)]
    return [Shape("GoggleLens", lens, True, filled=True),
            Shape("GoggleMaskCup", cup, True, filled=True, tone=1),    # ★보조색
            Shape("GoggleStrap", strap, False)]


# ═══════════════════════════════════════════════════════════════════════════
# 3. NECK — 작업 앞치마 (Work Apron Bib)   equip.neck.apronbib
#    ★ NECK은 B-2 파일럿으로 **형상이 에셋에 있다**. 흔들림(swayStart)도 데이터 필드라
#      자락 흔들림에 **모션 코드 비용이 0**이다(swaycensus.py).
# ═══════════════════════════════════════════════════════════════════════════
def neck_apronbib():
    panel = [(-0.52, -1.34), (0.62, -1.34), (1.06, -2.16), (1.34, -3.40),
             (0.46, -3.76), (-0.38, -3.38), (-0.44, -2.22)]
    pocket = [(0.10, -2.38), (0.86, -2.38), (0.92, -3.08), (0.16, -3.08)]
    strap = [(0.66, -1.30), (0.16, -0.86)]
    return [Shape("BibPanel", panel, True, filled=True),               # sway: 밑단 3점
            Shape("BibPocket", pocket, True, filled=True, tone=1),     # ★보조색
            Shape("BibNeckStrap", strap, False)]

NECK_SWAY = ("BibPanel", 3, 3)     # swayStart=3, swayCount=3 (밑단 3점)


# ═══════════════════════════════════════════════════════════════════════════
# 4. BACK — 연장 가방 (Tool Bag)   equip.shoulders.toolbag
#    ★ 이 자리는 **두 번 고쳤다.** 처음 낸 「도면 통」(어깨 위로 솟는 관)은 게이트에서
#      pack_fit ④ 「팩 안 상호 가림」이 **생존 0.0%** 를 냈다 — 어깨 위 뒤쪽은 이 팩의
#      모자 목덮개와 머리카락이 **이미 쓰고 있는 자리**였다. BACK은 SortBack=−1 이라
#      몸 뒤에 그려지고, 위 레이어(HAIR 6 / NECK 7 / EYES 8 / HEAD 10)에 전부 진다.
#      그래서 「기본 6종이 안 쓰는 방향」이 아니라 **「이 팩의 나머지 5종이 안 쓰는 방향」**으로
#      옮겼다: 뒤아래 먼 쪽(215~225°, r>3.0). 배낭이 x≥−1.50까지만 오므로 그 바깥이다.
# ═══════════════════════════════════════════════════════════════════════════
def back_toolbag():
    body = [(-1.34, -2.34), (-2.62, -2.62), (-2.78, -3.72), (-1.52, -4.02), (-1.28, -3.20)]
    flap = [(-1.30, -2.30), (-2.64, -2.58), (-2.72, -3.16), (-1.32, -2.92)]
    strap = [(-1.40, -2.40), (-0.70, -1.55), (0.20, -1.10)]
    return [Shape("BagBody", body, True, filled=True),
            Shape("BagFlap", flap, True, filled=True, tone=1),          # ★보조색
            Shape("BagStrap", strap, False)]


# ═══════════════════════════════════════════════════════════════════════════
# 5. HAIR — 목덜미 매듭 (Nape Tie)   look.hair.napetie
#    ★ 이 팩이 HAIR를 넣을 수 있는 이유가 여기 있다.
#      hairunderhat.py: 기본 머리 6종은 모자와 함께 쓰면 **평균 22.8%만** 남는다.
#      hairsurvival.py: 모자 6종 잉크의 최저점은 **−0.711R**. 그 아래는 아무 모자도 안 닿는다.
#      freeband.py:     HAIR 봉투가 245~290°에서 **정확히 0.00** — 완전히 빈 방향.
#      세 실측이 같은 곳을 가리킨다 -> 식별 잉크를 전부 **목덜미(245~256°, y ≤ −1.3)**에 둔다.
#    구성은 39-3 확정형 그대로: 돔 + 뒤커튼 + 두피 안쪽 호 + 앞커튼.
# ═══════════════════════════════════════════════════════════════════════════
CAP = 1.56          # 돔 반경. 하한 1.52(=1.0R+1.5W, '뚜껑' 방지) / 액자 1.75
INNER = 0.58        # 두피 안쪽 경계. 규칙 4 판정선 1−W = 0.6561R 보다 작다

def hair_napetie():
    dome = arc(CAP, 12, 202, 9)
    back_curtain = [(-1.44, -1.14), (-1.24, -1.98), (-0.80, -2.16), (-0.34, -2.30), (-0.66, -1.50)]
    inner = arc(INNER, 196, 16, 5)
    front_curtain = [(0.92, -0.30), (1.22, -0.66)]
    mass = dome + back_curtain + inner + front_curtain
    band = [(-1.50, -1.32), (-0.80, -1.46), (-0.88, -2.04), (-1.58, -1.90)]
    return [Shape("HairMass", mass, True, filled=True),
            Shape("HairTieBand", band, True, filled=True, tone=1)]     # ★보조색


# ═══════════════════════════════════════════════════════════════════════════
# 6. PET — 작업등 (Work Lamp)   look.pet.worklamp
#    펫은 몸에 안 붙는다(자기 로컬 원점). 단위는 다른 펫과 같은 R 배수.
#    실루엣 어휘: **위 고리 + 가운데 몸통 + 평평한 바닥**  (풍선 = 위 원 + 아래 실 — 정확히 뒤집힌 것)
# ═══════════════════════════════════════════════════════════════════════════
def pet_worklamp():
    body = [(-0.46, -0.62), (0.46, -0.62), (0.52, 0.02), (0.34, 0.44), (-0.34, 0.44), (-0.52, 0.02)]
    glass = [(-0.34, -0.40), (0.34, -0.40), (0.34, 0.30), (-0.34, 0.30)]
    bail = [(-0.40, 0.44), (-0.26, 1.18), (0.26, 1.18), (0.40, 0.44)]
    return [Shape("LampBody", body, True, filled=True),
            Shape("LampGlass", glass, True, filled=True, tone=1),      # ★보조색
            Shape("LampBail", bail, False)]


PACK = {
    "HEAD": ("목덮개 작업모", "equip.head.havelock", head_havelock, 0.0),
    "EYES": ("방진 고글", "equip.eyes.respirator", eyes_respirator, 0.0),
    "NECK": ("작업 앞치마", "equip.neck.apronbib", neck_apronbib, rig.SHOULDER_R),
    "BACK": ("연장 가방", "equip.shoulders.toolbag", back_toolbag, rig.SHOULDER_R),
    "HAIR": ("목덜미 매듭", "look.hair.napetie", hair_napetie, 0.0),
    "PET": ("작업등", "look.pet.worklamp", pet_worklamp, 0.0),
}
BASE = {"HEAD": items.HEAD, "EYES": items.EYES, "NECK": items.NECK,
        "BACK": items.BACK, "HAIR": hair.SET}


# ═══════════════════════════════════════════════════════════════════════════
#                                 게 이 트
# ═══════════════════════════════════════════════════════════════════════════
ICON, FIT, IST = 44.0, 0.86, 1.7 * 44 / 40      # CharacterInfoWindow: IconSize/FitFraction/IconStroke
W075, W060 = rig.stroke_in_R(0.75), rig.stroke_in_R(0.60)
FLOOR = W060                                     # 모든 실제 변의 하한 = 1획@0.60 (최악 배율)
RATCHET = S.SILHOUETTE_RATCHET_R                 # 쌍별 실루엣 목표 = 1.20획@0.60

_fail = []
def bad(m): _fail.append(m); print("  ✗ " + m)
def ok(m):  print("  OK " + m)


def true_min_edge(sh):
    """꺾임 문턱과 무관한 **최단 실제 변**(verify_appearance.true_min_edge와 같은 뜻)."""
    m, where = 9e9, None
    for s in sh:
        p = s.pts + ([s.pts[0]] if s.loop else [])
        for a, b in zip(p, p[1:]):
            d = math.hypot(b[0]-a[0], b[1]-a[1])
            if d < m: m, where = d, (s.name, a, b)
    return m, where


def ink_rect(s):
    xs = [p[0] for p in s.pts]; ys = [p[1] for p in s.pts]
    return max(xs)-min(xs), max(ys)-min(ys)


def gate(control=False):
    print("╔══ 팩 「야간 정비반」 6종 전수 게이트 ══╗")
    print("  W@0.75 = %.4fR  ·  W@0.60 = %.4fR  ·  변 하한 = %.4fR  ·  래칫 = %.4fR"
          % (W075, W060, FLOOR, RATCHET))
    print("  머리 지름 = 2R = %.2f획@0.75 = %.2f획@0.60  (11.63pt @0.75 · 9.30pt @0.60)"
          % (2/W075, 2/W060))

    P = {k: v[2]() for k, v in PACK.items()}
    if control:   # ★ 양성 대조 — 일부러 망가뜨린다
        P["HEAD"] = [Shape("A", [(0,0),(0.12,0),(0.12,0.12),(0,0.12)], True, filled=True),
                     Shape("B", [(0,0),(0.1,0.1)], False, tone=1),
                     Shape("C", [(0,0),(0.1,0.1)], False, tone=1)]
        P["HAIR"] = [Shape("A", [(0,0),(3,0),(3,3),(0,3)], True, filled=True, tone=1)]

    # ── ① 규칙 1 (변 ≥ 1획) · 잉크 사각형 ≥ 1.5획 · 자기교차 · 정원 · 보조색 ──
    print("\n  ── ① 도형 규칙 (배율 0.60 = 최악 배율 기준) ──")
    for k, sh in P.items():
        n = PACK[k][0] if not control else k
        if not (2 <= len(sh) <= 4): bad("%s 정원 %d개 (2~4)" % (n, len(sh)))
        acc = sum(1 for s in sh if s.tone == 1)
        if acc != 1: bad("%s 보조색 %d개 (정확히 1)" % (n, acc))
        if k in ("HEAD", "EYES", "HAIR") and not any(s.filled for s in sh):
            bad("%s 채움 없음(규칙 2)" % n)
        e, where = true_min_edge(sh)
        if e < FLOOR: bad("%s 최단 실제 변 %.4fR = %.2f획@0.60 < 1.00  (%s)" % (n, e, e/W060, where[0]))
        for s in sh:
            if s.loop and rig.self_intersects(s.pts): bad("%s '%s' 자기교차" % (n, s.name))
            if s.filled:
                w_, h_ = ink_rect(s)
                if min(w_, h_) < 1.5 * W060:
                    bad("%s '%s' 잉크 사각형 %.3f×%.3f < 1.5획@0.60(%.3f)" % (n, s.name, w_, h_, 1.5*W060))
        if e >= FLOOR:
            ok("%-10s 도형 %d · 보조색 %d · 최단변 %.3fR = %.2f획@0.60 = %.2f획@0.75"
               % (n, len(sh), acc, e, e/W060, e/W075))

    # ── ② 카드 44px ──
    print("\n  ── ② 카드 44px (IconSize=44 · FitFraction=0.86 · IconStroke=%.3fpx) ──" % IST)
    for k, sh in P.items():
        n = PACK[k][0] if not control else k
        pts = [p for s in sh for p in s.pts]
        x0, y0, x1, y1 = rig.bounds(pts)
        span = max(x1-x0, y1-y0)
        kk = ICON * FIT / span
        e, where = true_min_edge([Shape(s.name, [(x*kk, y*kk) for x, y in s.pts], s.loop, s.filled, s.tone) for s in sh])
        if e < IST: bad("%s 카드 최단변 %.2fpx < 획 %.2fpx (%s)" % (n, e, IST, where[0]))
        else: ok("%-10s span %.2fR -> %.1fpx · 최단변 %.2fpx = %.2f카드획" % (n, span, span*kk, e, e/IST))

    # ── ③ 슬롯 경계 단언 (verify.py와 같은 식) ──
    print("\n  ── ③ 슬롯 경계 (verify.py 단언 그대로) ──")
    p = [q for s in P["HEAD"] for q in s.pts]
    t, b = max(q[1] for q in p), min(q[1] for q in p)
    if not (1.0 < t < 2.551): bad("HEAD 꼭대기 %.2f (1.0<t<2.551)" % t)
    if b <= -1.0: bad("HEAD 턱 아래 %.2f" % b)
    if not any(abs(q[0]) >= 0.85 and q[1] <= 0.05 for q in p): bad("HEAD 감쌈 실패(원칙 4)")
    else: ok("HEAD 꼭대기 %.2f · 밑단 %.2f · 감쌈 잉크 있음(|x|≥0.85 ∧ y≤0.05)" % (t, b))

    p = [q for s in P["EYES"] for q in s.pts]
    f = [s for s in P["EYES"] if s.filled]
    if max(abs(q[0]) for q in p) >= 1.6: bad("EYES |x| %.2f ≥ 1.6" % max(abs(q[0]) for q in p))
    if max(q[1] for q in p) >= 1.15: bad("EYES 정수리 침범 %.2f" % max(q[1] for q in p))
    if min(q[1] for q in p) <= -2.2: bad("EYES 목 아래 %.2f" % min(q[1] for q in p))
    fe = any(rig.contains(s.pts, (rig.EYE_X, rig.EYE_Y)) for s in f)
    be = any(rig.contains(s.pts, (-rig.EYE_X, rig.EYE_Y)) for s in f)
    if not fe: bad("EYES 앞눈 미커버")
    if not be: bad("EYES 뒤눈 미커버 — 한쪽만 가리면 EYE_FRONT_ONLY 등록 + 드러난 눈 도형이 필요하다(39-5)")
    if fe and be: ok("EYES |x|max %.2f · y[%.2f,%.2f] · 두 눈 모두 커버(=front-only 아님, 눈 도형 불필요)"
                     % (max(abs(q[0]) for q in p), min(q[1] for q in p), max(q[1] for q in p)))

    p = [q for s in P["NECK"] for q in s.pts]
    if max(q[1] for q in p) >= 0.0: bad("NECK 얼굴 침범 %.2f" % max(q[1] for q in p))
    if min(q[1] for q in p) <= rig.HIP_R - 0.517: bad("NECK 고관절 아래 %.2f" % min(q[1] for q in p))
    else: ok("NECK y[%.2f,%.2f]  (상한 0.00 / 하한 %.2f)" % (min(q[1] for q in p), max(q[1] for q in p), rig.HIP_R-0.517))

    p = [q for s in P["BACK"] for q in s.pts]
    if max(q[1] for q in p) >= 1.0: bad("BACK 정수리 위 %.2f" % max(q[1] for q in p))
    if min(q[1] for q in p) <= -9.3395: bad("BACK 바닥 관통 %.2f" % min(q[1] for q in p))
    else: ok("BACK y[%.2f,%.2f]  (상한 1.00 / 하한 −9.34)" % (min(q[1] for q in p), max(q[1] for q in p)))

    p = [q for s in P["HAIR"] for q in s.pts]
    mn = min(math.hypot(*q) for q in p); mx = max(math.hypot(*q) for q in p); t = max(q[1] for q in p)
    if mn > 1 - W075: bad("HAIR 부착 %.3f > %.3f (규칙 4)" % (mn, 1-W075))
    if mx < 1.05: bad("HAIR 두피 안 %.3f" % mx)
    if t > 1.75: bad("HAIR 액자 초과 %.2f" % t)
    pup = False
    for s in P["HAIR"]:
        if not s.filled: continue
        for sx in (1, -1):
            for dx, dy in ((0,0), (rig.PUPIL_R,0), (-rig.PUPIL_R,0), (0,rig.PUPIL_R), (0,-rig.PUPIL_R)):
                if rig.contains(s.pts, (sx*rig.EYE_X+dx, rig.EYE_Y+dy)): pup = True
    if pup: bad("HAIR 눈동자 침범")
    if not pup and mn <= 1-W075 and mx >= 1.05 and t <= 1.75:
        ok("HAIR 부착 %.3f(≤%.3f) · 최대 %.3f · 꼭대기 %.2f(≤1.75) · 눈동자 비침범" % (mn, 1-W075, mx, t))

    # ── ④ 쌍별 실루엣 — 기본 6종 **전부**와 비교 ──
    print("\n  ── ④ 쌍별 실루엣 차 (신규 1종 vs 기본 6종, L∞ 프로파일) ──")
    for k in ("HEAD", "EYES", "NECK", "BACK", "HAIR"):
        anc = PACK[k][3]
        mine = S.profile(P[k], anc)
        worst = (9e9, None)
        for bn, bsh in BASE[k].items():
            sh = bsh() if callable(bsh) else bsh
            d = rig.max_delta(mine, S.profile(sh, anc))
            if d < worst[0]: worst = (d, bn)
        n = PACK[k][0] if not control else k
        tag = "★래칫" if worst[0] >= RATCHET else ("하한만" if worst[0] >= W060 else "✗미달")
        if worst[0] < W060:
            bad("%s 쌍별 최소 %.3fR = %.2f획@0.60 (vs %s) — 하한 1.00 미달" % (n, worst[0], worst[0]/W060, worst[1]))
        else:
            ok("%-10s 최악쌍 vs %-8s %.3fR = %.2f획@0.60 = %.2f획@0.75  %s"
               % (n, worst[1], worst[0], worst[0]/W060, worst[0]/W075, tag))
    # PET 은 다른 하니스(appearance.py)의 자를 쓴다
    import appearance as A
    mine = S.profile(P["PET"], 0.0)
    worst = (9e9, None)
    for bn, bsh in A.PET_NOW.items():
        if not bsh: continue
        sh = [Shape(s[0], s[1], s[2], s[3], s[4] if len(s) > 4 else 0) if isinstance(s, (list, tuple)) else s
              for s in bsh]
        d = rig.max_delta(mine, S.profile(bsh, 0.0))
        if d < worst[0]: worst = (d, bn)
    petfloor = 0.3149    # ★ PET 슬롯이 **이미** 갖고 있는 쌍별 최소(packcap.py 실측). 이보다 나쁘면 후퇴다.
    if worst[0] < petfloor:
        bad("PET 쌍별 최소 %.3fR (vs %s) — 기존 PET 최악쌍 %.3fR보다 나쁘다(후퇴)" % (worst[0], worst[1], petfloor))
    else:
        ok("%-10s 최악쌍 vs %-8s %.3fR = %.2f획@0.60  (기존 PET 최악쌍 %.3fR 대비 %+.3fR)"
           % ("작업등", worst[1], worst[0], worst[0]/W060, petfloor, worst[0]-petfloor))

    print("\n╚══ 결과: %s ══╝" % ("전수 통과 (위반 0건)" if not _fail else "위반 %d건" % len(_fail)))
    return len(_fail)


if __name__ == "__main__":
    if "--dump" in sys.argv:
        for k, (nm, iid, fn, anc) in PACK.items():
            print("\n[%s] %s  %s" % (k, nm, iid))
            for s in fn():
                print("  %-14s loop=%-5s filled=%-5s tone=%d  n=%d" % (s.name, s.loop, s.filled, s.tone, len(s.pts)))
                for x, y in s.pts: print("      (%+.4f, %+.4f)" % (x, y))
        sys.exit(0)
    ctl = "--control" in sys.argv
    if ctl: print("★ 양성 대조 모드 — 일부러 나쁜 값을 넣는다. **빨간불이 켜져야 정상.**\n")
    n = gate(ctl)
    if ctl:
        print("\n★ 양성 대조 판정: %s" % ("OK — 게이트가 실제로 잡는다(위반 %d건)" % n if n else "FAIL — 나쁜 값이 통과했다"))
        sys.exit(0 if n else 1)
    sys.exit(1 if n else 0)
