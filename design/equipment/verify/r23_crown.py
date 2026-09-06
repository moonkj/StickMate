# -*- coding: utf-8 -*-
"""R23 — 왕관(HEAD) 부착 위치 결함 실측·검산.  python3 r23_crown.py > r23_crown.out.txt

사용자 신고(2026-09-06): "왕관착용시 머리 중간넘어서까지 착용이 되서 안경같은게 하나도 안보임 착용위치가 잘못됨".

★ 이 자는 **모델(r17/r19)을 다시 돌리지 않는다.** 프로덕션 생성 파일
  `Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs` 의 좌표 배열과
  `AccessoryShapeBuilder.cs` 의 v1 상수식을 **직접 파싱**해서 잰다 — 모델이 틀렸을 때
  모델로 검산하면 같은 함정에 같이 빠지기 때문이다(verify-change 규약: "다른 방법으로 다시 잰다").

재는 것 4가지
  ① 모자 6종의 **앞층 잉크 최하단**(정렬 ≥ SortEyes 인 조각만) — "머리 중간을 넘는가"
  ② 모자 6종 × 안경 6종 **가려짐률** = 안경 잉크 면적 중 모자 채움에 덮인 비율
  ③ 모자 6종 **쌍별 실루엣 차**(AccessorySilhouetteMetrics.ProfileOf 와 같은 72×5도 자)
  ④ H-2 **「앞층만의 합집합」** 착용선(§14-12-8 이 미확인으로 남긴 자) — 6종 전부

좌표계: 머리 중심 원점 · R 배수 · y 위 · +x 진행 방향 · facing +1(정면).

============================================================================
★ 2026-09-06 복구 (design-equipment, §14-16-5)
============================================================================
이 파일은 R25 라운드 뒤 **크게 멈춰 있었다**: `상수 BeretBackDroopRatio 를 … 못 찾았다`.
R25 가 베레모·밀짚모자를 R21 재저작본으로 갈아치우며 v1 조형 상수를 없앴는데, 아래
`beret()`/`straw()` 가 **v1 switch 의 조립 로직을 파이썬으로 재구현**하고 그 상수만 읽고
있었기 때문이다. (조용히 틀린 것이 아니라 크게 멈췄다 — **안전한 실패**였다.)

복구 3건:
  (가) `beret()`/`straw()` 를 **좌표 배열 직접 파싱**으로 옮겼다(`r24_hats.py` 와 같은 자).
       `V1Hat_*` 배열과 `HeadV1Piece(...)` 호출을 그대로 읽는다 — 조립 로직이 이 파일에
       없으므로 프로덕션 도형이 바뀌어도 **갈라질 자리가 없다**.
  (나) `h2_wear` 의 **꼭대기 맹점**을 막았다(§14-14-4 (가)). 옛 판은 「첫 덮임 구간의 끝」만
       돌려주고 머리 꼭대기부터 덮였는지를 안 봐서, 밀짚모자 `[+0.07 → −0.23]` 을
       「착용선 −0.228 통과」로 보고했다 — **거짓 초록**이었다.
  (다) `H2_TARGET`(단일 상한, R25 에서 폐기된 값)을 §14-16-1 정본 대역으로 교체했다.

기록 보존: 복구 전 출력(= R23 당시, R25 이식 **전** 상태)은 `r23_crown.r23era.out.txt`.
이 스크립트가 지금 찍는 것은 **현재 프로덕션 상태**다.
"""
import math
import os
import re
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CS_HANDOFF = os.path.join(REPO, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs")
CS_BUILDER = os.path.join(REPO, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs")

# ---- 프로덕션 상수(같은 소스에서 읽는다) -------------------------------------------
_B = open(CS_BUILDER, encoding="utf-8").read()


def const(name):
    m = re.search(r"const float %s\s*=\s*(-?\d+\.?\d*)f" % name, _B)
    if not m:
        raise SystemExit("상수 %s 를 %s 에서 못 찾았다" % (name, CS_BUILDER))
    return float(m.group(1))


def sort_const(name):
    m = re.search(r"const int %s\s*=\s*(-?\d+)" % name, _B)
    return int(m.group(1))


SORT_BACK, SORT_EYES, SORT_HEAD = sort_const("SortBack"), sort_const("SortEyes"), sort_const("SortHead")
BASE_HEAD_R, SHIP = 0.22, 0.75
W = max(0.048 * SHIP, 2.0 / (846.0 / 24.0)) / (BASE_HEAD_R * SHIP)   # 0.34386 R (출하 획)
HEAD_R_COVER = 1.184212950170397     # H-2 가 쓰는 「가장 큰 머리」(배율 0.35 링 하한 포함 잉크 원반)
COVER_MARGIN = 0.06
TOP_LIMIT, CHIN = 2.551, -1.0

# ── H-2 / H-2b 정본 대역 (docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §14-16-1) ──────────────
# ★ 옛 값 {감싸는 +0.30 · 털모자 0.00 · 왕관 +0.45} 은 R25 에서 폐기됐다.
# H2_BAND  착용선 ∈ [+0.28, +0.45]  — 상한: 왕관(얹는 물건) 실측 +0.4392 올림.
#                                     하한: **새 숫자가 아니다.** 앞층 채움이 없는 높이에서는
#                                     덮임도 불가능하므로 `착용선 ≥ 밑단` 이 항등이고,
#                                     H2B_BAND 하한이 그대로 내려온다(§14-16-2).
# H2B_BAND 밑단(머리 폭 안) ∈ [+0.28, +0.35] · 목표 +0.315 — 가려짐을 정하는 것은 착용선이
#                                     아니라 이 밑단이다. 상한은 하한을 목표에 거울대칭한 값.
# ★ 이 대역은 **「앞층 채움만」** 자에만 건다. 「채움 ∪ 잉크 윤곽」은 별개 진단자다(§14-14-4 (나)).
# ★ 하한 검사는 격자만큼 **낙관적**이다 — 아래 h2_wear/bottom_in_head 는 정확 스캔라인을 쓴다(§14-16-3).
H2_BAND = (0.28, 0.45)
H2B_BAND = (0.28, 0.35)
H2B_AIM = 0.315

# ---- 인계본 조각 표 파싱 ----------------------------------------------------------
_H = open(CS_HANDOFF, encoding="utf-8").read()
ARR = {}
for _m in re.finditer(r"Handoff_([A-Za-z0-9_]+)\s*=\s*\{(.*?)\};", _H, re.S):
    _v = [float(x[:-1]) for x in re.findall(r"-?\d+\.?\d*f", _m.group(2))]
    ARR[_m.group(1)] = [(_v[i * 2], _v[i * 2 + 1]) for i in range(len(_v) // 2)]

CASES, _cur = {}, None
for _line in _H.splitlines():
    # ★ Back 까지 받아야 한다 — 안 받으면 마지막 EYES(외알안경) 절이 BACK 조각을 삼킨다(실제로 한 번 틀렸다).
    _c = re.search(r"case (Head\w+|Eyes\w+|Back\w+):\s*//\s*(\S+)\s+(\S+)", _line)
    if _c:
        _cur = _c.group(3)
        CASES[_cur] = []
        continue
    _p = re.search(r'HandoffPiece\(sink, rig, xf, (\w+), "([^"]+)", Handoff_([A-Za-z0-9_]+), '
                   r"loop: (\w+), filled: (\w+), tone: (\d+), surfaces: (\d+),.*?layer: (\d+),.*?bodyFixed: (\w+)\)",
                   _line)
    if _p and _cur:
        CASES[_cur].append(dict(sort=_p.group(1), arr=_p.group(3), loop=_p.group(4) == "true",
                                filled=_p.group(5) == "true", tone=int(_p.group(6)),
                                surfaces=int(_p.group(7)), layer=int(_p.group(8)),
                                bodyfixed=_p.group(9) == "true"))

SORTNAME = {"SortBack": SORT_BACK, "SortEyes": SORT_EYES, "SortHead": SORT_HEAD}
LAYER_SORT = {1: SORT_BACK, 2: 3}       # AccessoryPieceLayer.Back / BodyFront

# 털모자만 bodyFixed=false 라 WornTransformCode 를 걸어야 한다(생성 파일에서 그대로 읽는다).
_fur = re.search(r"case HeadBeanie: return new AccessoryWornTransform\("
                 r"\s*(-?[\d.]+)f,\s*(-?[\d.]+)f,\s*(-?[\d.]+)f,\s*(-?[\d.]+)f", _H)
FUR_U, FUR_KY, FUR_DY = float(_fur.group(2)), float(_fur.group(3)), float(_fur.group(4))


def handoff_pieces(kind):
    out = []
    for pc in CASES[kind]:
        if pc["surfaces"] not in (0, 1):          # 몸 표면만(카드 전용 조각 제외)
            continue
        pts = ARR[pc["arr"]]
        if not pc["bodyfixed"] and kind == "furhat":
            pts = [(x * FUR_U, y * FUR_U * FUR_KY + FUR_DY) for x, y in pts]
        out.append(dict(pts=pts, filled=pc["filled"], loop=pc["loop"], tone=pc["tone"],
                        sort=LAYER_SORT.get(pc["layer"], SORTNAME[pc["sort"]])))
    return out


# ---- v1(에셋 아닌 코드 switch) — ★ 좌표 배열 직접 파싱 (2026-09-06 복구, §14-16-5) ----
# 전에는 아래 두 함수가 v1 switch 의 조립 로직을 파이썬으로 **다시 구현**하고 상수만 프로덕션에서
# 읽었다. 그 형태의 위험은 두 가지였고 **둘 다 실제로 터졌다**:
#   (ㄱ) 상수가 없어지면 크게 멈춘다      ← R25 가 v1 조형 상수를 폐기하며 이 파일을 세웠다
#   (ㄴ) 구조가 갈라지면 **아무도 모른다** ← 프로덕션이 2층·곡선으로 바뀌어도 옛 7점 다각형을
#        계속 그리며 「측정했다」고 말한다. (ㄱ)이 시끄러웠던 것이 다행이었다.
# 지금은 인계본과 **같은 방식**이다 — `private static readonly float[] V1Hat_*` 배열과
# `HeadV1Piece(...)` 호출을 그대로 읽는다. 조립 로직이 이 파일에 없으므로 갈라질 자리가 없다.
BAND = const("AccentBandThicknessRatio")

ARR_V1 = {}
for _m in re.finditer(r"float\[\]\s+V1Hat_([A-Za-z0-9_]+)\s*=\s*\{(.*?)\};", _B, re.S):
    _v = [float(x[:-1]) for x in re.findall(r"-?\d+\.?\d*f", _m.group(2))]
    ARR_V1[_m.group(1)] = [(_v[i * 2], _v[i * 2 + 1]) for i in range(len(_v) // 2)]

_V1_CALL = re.compile(
    r'HeadV1Piece\(sink,\s*rig,\s*"([^"]+)",\s*V1Hat_([A-Za-z0-9_]+),\s*'
    r"loop:\s*(\w+),\s*filled:\s*(\w+)(?:,\s*tone:\s*(\w+))?(?:,\s*noStroke:\s*(\w+))?"
    r"(?:,\s*layer:\s*\(byte\)AccessoryPieceLayer\.(\w+))?(?:,\s*underBack:\s*(\d+))?\s*\)", re.S)

TONE_NUM = {"Accent": 1, "Shade": 2, "Highlight": 3}


def _v1_case(case_name):
    """AppendHead 의 `case <name>:` 블록 본문(다음 `break;` 까지)."""
    i = _B.index("case %s:" % case_name, _B.index("private static void AppendHead"))
    return _B[i:_B.index("break;", i)]


def _v1_pieces(case_name):
    out = []
    for m in _V1_CALL.finditer(_v1_case(case_name)):
        name, arr, loop, filled, tone, _nostroke, layer, _ub = m.groups()
        out.append(dict(name=name, src=arr, pts=ARR_V1[arr],
                        filled=filled == "true", loop=loop == "true",
                        tone=TONE_NUM.get(tone, 0),
                        sort=SORT_BACK if layer == "Back" else SORT_HEAD))
    if not out:
        raise SystemExit("v1 모자 조각을 못 읽었다: %s (HeadV1Piece 형태가 바뀌었나)" % case_name)
    return out


def beret():
    return _v1_pieces("HeadBeret")


def straw():
    return _v1_pieces("HeadStraw")


def browline():
    cy = const("GlassesCenterRatio")
    bb = cy + const("BrowlineBarBottomRatio")
    o, i2, tp = const("BrowlineBarOuterRatio"), const("BrowlineBarInnerRatio"), const("BrowlineBarTopRatio")
    li = const("BrowlineLensInnerRatio")
    bar = [(-o, bb), (-i2, cy + tp), (i2, cy + tp - 0.02), (o, bb - 0.02), (li, bb - 0.04), (-li, bb - 0.02)]
    lf = [(li, bb - 0.04), (0.44, cy - 0.52), (1.00, cy - 0.34), (o, bb - 0.02)]
    lb = [(-li, bb - 0.02), (-o, bb), (-0.98, cy - 0.32), (-0.44, cy - 0.50)]
    return [dict(pts=bar, filled=True, loop=True, tone=1, sort=SORT_EYES),
            dict(pts=lb, filled=True, loop=True, tone=0, sort=SORT_EYES),
            dict(pts=lf, filled=True, loop=True, tone=0, sort=SORT_EYES)]


def patch():
    cy = const("GlassesCenterRatio")
    hw, hh = const("PatchHalfWidthRatio"), const("PatchHalfHeightRatio")
    cx = const("DrawnEyeOffsetRatio")           # PatchCenterRatio = DrawnEyeOffsetRatio
    reach, deg = const("PatchStrapReachRatio"), const("PatchStrapDegrees")
    er = const("DrawnEyeRadiusRatio")
    top, bot = (cx - hw, cy + hh), (cx - hw + 0.04, cy - hh * 0.82)
    cover = [top, (cx + hw, cy + hh * 0.82), (cx + hw - 0.06, cy - hh), bot]
    pol = lambda d: (math.cos(math.radians(d)) * reach, math.sin(math.radians(d)) * reach)
    strap = [pol(deg), top, bot, pol(360.0 - deg)]
    eye = [(-cx + er * math.cos(2 * math.pi * i / 24), cy + er * math.sin(2 * math.pi * i / 24)) for i in range(24)]
    return [dict(pts=cover, filled=True, loop=True, tone=0, sort=SORT_EYES),
            dict(pts=strap, filled=False, loop=False, tone=0, sort=SORT_EYES),
            dict(pts=eye, filled=True, loop=True, tone=1, sort=SORT_EYES)]


HATS = {"천모자": lambda: handoff_pieces("clothhat"), "털모자": lambda: handoff_pieces("furhat"),
        "중절모": lambda: handoff_pieces("fedora"), "왕관": lambda: handoff_pieces("crown"),
        "베레모": beret, "밀짚모자": straw}
EYES = {"선글라스": lambda: handoff_pieces("sunglasses"), "동그란안경": lambda: handoff_pieces("roundglasses"),
        "고글": lambda: handoff_pieces("goggles"), "외알안경": lambda: handoff_pieces("monocle"),
        "뿔테": browline, "안대": patch}

# ---- 래스터 자 --------------------------------------------------------------------
STEP = 0.004
_gx = np.arange(-2.8, 2.8, STEP)
_gy = np.arange(-1.6, 2.9, STEP)
_GX, _GY = np.meshgrid(_gx, _gy)
PTS = np.column_stack([_GX.ravel(), _GY.ravel()])


def inside(poly, pts):
    """짝수-홀수(even-odd) 점-다각형 판정 — 프로덕션 채움 규칙과 같다."""
    p = np.asarray(poly, float)
    x, y = pts[:, 0], pts[:, 1]
    res = np.zeros(len(pts), bool)
    n = len(p)
    for i in range(n):
        x1, y1 = p[i]
        x2, y2 = p[(i + 1) % n]
        cond = (y1 > y) != (y2 > y)
        with np.errstate(divide="ignore", invalid="ignore"):
            xin = (x2 - x1) * (y - y1) / (y2 - y1) + x1
        res ^= cond & (x < xin)
    return res


def stroke(pts, loop, pts_grid=None, w=W):
    g = PTS if pts_grid is None else pts_grid
    m = np.zeros(len(g), bool)
    q = list(pts) + ([pts[0]] if loop else [])
    for a, b in zip(q, q[1:]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        l2 = dx * dx + dy * dy
        if l2 < 1e-12:
            continue
        t = np.clip(((g[:, 0] - a[0]) * dx + (g[:, 1] - a[1]) * dy) / l2, 0, 1)
        m |= np.hypot(g[:, 0] - (a[0] + t * dx), g[:, 1] - (a[1] + t * dy)) <= w * 0.5
    return m


def front_fill(pieces):
    """안경 위에 오는 조각(정렬 ≥ SortEyes)의 채움 합집합."""
    m = np.zeros(len(PTS), bool)
    for p in pieces:
        if p["filled"] and p["sort"] >= SORT_EYES:
            m |= inside(p["pts"], PTS)
    return m


def ink(pieces):
    m = np.zeros(len(PTS), bool)
    for p in pieces:
        if p["filled"]:
            m |= inside(p["pts"], PTS)
        m |= stroke(p["pts"], p["loop"])
    return m


def profile(pieces, buckets=72, bdeg=5.0, spe=64):
    """AccessorySilhouetteMetrics.ProfileOf 와 같은 자(72×5도, 변 조밀표본, 머리 중심 기준)."""
    pr = [0.0] * buckets
    for pc in pieces:
        pts = pc["pts"]
        n = len(pts)
        for e in range(n if pc["loop"] else n - 1):
            ax, ay = pts[e]
            bx, by = pts[(e + 1) % n]
            for k in range(spe + 1):
                t = k / spe
                x, y = ax + (bx - ax) * t, ay + (by - ay) * t
                d = math.degrees(math.atan2(y, x))
                if d < 0:
                    d += 360.0
                i = min(int(d / bdeg), buckets - 1)
                pr[i] = max(pr[i], math.hypot(x, y))
    return pr


# ---- H-2 / H-2b 자 — ★ 정확 스캔라인 (래스터 아님, §14-16-3) --------------------------
# 하한 검사(밑단 ≥ +0.28 · 착용선 ≥ +0.28)는 내려가며 훑는 격자 자에서 **격자만큼 낙관적**이다
# (보고값이 참값보다 최대 h 크다). 그래서 여기서는 격자를 쓰지 않고 교차점을 직접 푼다.
def _fill_intervals(polys, y):
    """even-odd 채움 구간의 **합집합**. ★ 짝짓기는 **다각형마다 따로** 해야 한다 —
    여러 조각의 교차점을 한 줄로 섞어 짝지으면 겹친 조각의 채운 곳이 빈 곳으로 뒤집힌다."""
    segs = []
    for poly in polys:
        xs = []
        n = len(poly)
        for i in range(n):
            x1, y1 = poly[i]
            x2, y2 = poly[(i + 1) % n]
            if (y1 > y) != (y2 > y):
                xs.append((x2 - x1) * (y - y1) / (y2 - y1) + x1)
        xs.sort()
        segs += [(xs[i], xs[i + 1]) for i in range(0, len(xs) - 1, 2)]
    segs.sort()
    merged = []
    for a, b in segs:
        if merged and a <= merged[-1][1] + 1e-12:
            merged[-1] = (merged[-1][0], max(merged[-1][1], b))
        else:
            merged.append((a, b))
    return merged


def _central_hw(polys, y):
    """중심선(x=0)을 포함한 채움 구간의 반폭. 0 을 안 포함하면 0."""
    for a, b in _fill_intervals(polys, y):
        if a <= 0.0 <= b:
            return min(abs(a), abs(b))
    return 0.0


def _need(y):
    return math.sqrt(max(HEAD_R_COVER ** 2 - y * y, 0.0)) + COVER_MARGIN


def h2_wear(pieces, step=0.0002):
    """H-2 「앞층만의 합집합」 착용선 → (착용선, 꼭대기부터 덮었나).

    ★ 2026-09-06 복구(§14-14-4 (가) · §14-16-5) — 옛 판은 **첫 덮임 구간의 끝**만 돌려주고
      머리 꼭대기부터 덮였는지를 안 봤다(`r17_model.evaluate` 의 `covered_top` 항이 없었다).
      그래서 밀짚모자가 `[+0.07 → −0.23]` **하나만** 덮고도 「착용선 −0.228 통과」로 찍혔다.
      이제 둘째 값으로 그것을 **시끄럽게** 알린다. 대역 안이어도 top_ok=False 면 실패다."""
    polys = [p["pts"] for p in pieces if p["filled"] and p["sort"] >= SORT_EYES]
    if not polys:
        return None, False
    top_ok = _central_hw(polys, HEAD_R_COVER - 1e-9) >= _need(HEAD_R_COVER - 1e-9)
    wear, y = None, HEAD_R_COVER
    while y > -1.2:
        if _central_hw(polys, y) >= _need(y):
            wear = y
        elif wear is not None:
            break
        y -= step
    return wear, top_ok


def bottom_in_head(pieces, step=0.0002):
    """H-2b — 앞층 **채움** 잉크의 밑단을 **머리 폭(|x| ≤ HEAD_R_COVER) 안**에서 잰다.
    챙 끝(±1.8 R)이나 베레모의 옆 처짐은 얼굴을 못 가리므로 세지 않는다."""
    polys = [p["pts"] for p in pieces if p["filled"] and p["sort"] >= SORT_EYES]
    if not polys:
        return None
    ylo = min(y for q in polys for _, y in q)
    yhi = max(y for q in polys for _, y in q)
    lo, y = None, yhi
    while y >= ylo - step:
        if any(b >= -HEAD_R_COVER and a <= HEAD_R_COVER for a, b in _fill_intervals(polys, y)):
            lo = y
        y -= step
    return lo


def main():
    hats = {k: f() for k, f in HATS.items()}
    eyes = {k: f() for k, f in EYES.items()}
    w = sys.stdout.write

    w("R23 — 왕관 부착 위치. 자: 프로덕션 좌표 직접 파싱(모델 재실행 아님).\n")
    w("획 W(배율 %.2f) = %.5f R · 정렬 BACK %d / EYES %d / HEAD %d — **모자가 언제나 안경 위**다.\n"
      % (SHIP, W, SORT_BACK, SORT_EYES, SORT_HEAD))
    w("H-2 머리 %.4f R · 여유 %.2f R · 액자 상한 %.3f R · 턱 %.1f R\n\n" % (HEAD_R_COVER, COVER_MARGIN, TOP_LIMIT, CHIN))

    w("① 앞층 잉크 최하단(= 얼굴을 어디까지 덮는가)\n")
    w("   ★ 「전체」는 조각 좌표의 최소 y · 「머리폭 안」이 H-2b 가 재는 값이다(챙 끝·옆 처짐은 얼굴을 못 가린다).\n")
    for k, v in hats.items():
        ys = [q[1] for p in v if p["sort"] >= SORT_EYES for q in p["pts"]]
        bi = bottom_in_head(v)
        w("   %-6s 전체 %+.4f R · 머리폭 안 %+.4f R%s\n"
          % (k, min(ys), bi, "" if abs(bi - min(ys)) < 5e-4 else "   ← 다르다(얼굴 밖 조형)"))
    # ★ sys.stdout.write 는 printf 가 아니다 — 옛 판의 "%%" 가 화면에 그대로 찍혀 있었다(표시 결함).
    w("\n② 가려짐 % (안경 잉크 면적 중 모자 앞층 채움에 덮인 비율)\n")
    hm = {k: front_fill(v) for k, v in hats.items()}
    ei = {k: ink(v) for k, v in eyes.items()}
    w("   %-8s" % "" + "".join("%-11s" % e for e in eyes) + "\n")
    for hk in hats:
        w("   %-8s" % hk + "".join("%-11s" % ("%.1f" % (100.0 * (ei[ek] & hm[hk]).sum() / ei[ek].sum()))
                                   for ek in eyes) + "\n")

    w("\n③ 모자 6종 쌍별 실루엣 차(문턱 1.00획 = %.4f R)\n" % W)
    pr = {k: profile(v) for k, v in hats.items()}
    ks = list(hats)
    rows = sorted((max(abs(a - b) for a, b in zip(pr[ks[i]], pr[ks[j]])) / W, "%s↔%s" % (ks[i], ks[j]))
                  for i in range(len(ks)) for j in range(i + 1, len(ks)))
    for v, n in rows:
        w("   %-14s %.2f획%s\n" % (n, v, "   ★ 위반" if v <= 1.0 else ""))

    w("\n④ H-2 「앞층만의 합집합」 착용선 + H-2b 밑단 — 정본 대역 판정(§14-16-1)\n")
    w("   자: 정확 스캔라인(격자 0.0002 R). 하한 검사는 격자 자에서 낙관적이라 격자를 안 쓴다(§14-16-3).\n")
    w("   H-2  착용선 ∈ [%+.2f, %+.2f] 이고 **머리 꼭대기부터 한 구간으로** 덮을 것\n" % H2_BAND)
    w("   H-2b 밑단(머리폭 안) ∈ [%+.2f, %+.2f] · 목표 %+.3f      H-2c 꼬리 = 착용선 − 밑단 ≤ %.3f\n"
      % (H2B_BAND[0], H2B_BAND[1], H2B_AIM, H2_BAND[1] - H2B_BAND[0]))
    for k, v in hats.items():
        wear, top_ok = h2_wear(v)
        bi = bottom_in_head(v)
        ys = [q[1] for p in v for q in p["pts"]]
        why = []
        if wear is None:
            why.append("덮임 실패")
        else:
            if not top_ok:
                why.append("꼭대기 미덮음(구간이 위에서 시작 안 함)")
            if wear < H2_BAND[0] - 1e-9:
                why.append("H-2 하한 미달 %+.4f < %+.2f" % (wear, H2_BAND[0]))
            if wear > H2_BAND[1] + 1e-9:
                why.append("H-2 상한 초과 %+.4f > %+.2f" % (wear, H2_BAND[1]))
        if bi is not None:
            if bi < H2B_BAND[0] - 1e-9:
                why.append("H-2b 하한 미달 %+.4f (부족 %.4f R)" % (bi, H2B_BAND[0] - bi))
            if bi > H2B_BAND[1] + 1e-9:
                why.append("H-2b 상한 초과 %+.4f (모자가 뜬다)" % bi)
        if wear is not None and bi is not None and (wear - bi) > (H2_BAND[1] - H2B_BAND[0]) + 1e-9:
            why.append("꼬리 %.3f R — 평행이동으로 못 맞춘다(조형 수정)" % (wear - bi))
        if max(ys) >= TOP_LIMIT:
            why.append("꼭대기 %.3f ≥ %.3f" % (max(ys), TOP_LIMIT))
        if min(ys) <= CHIN:
            why.append("밑 %.3f ≤ %.1f" % (min(ys), CHIN))
        w("   %-6s 착용선 %-9s 밑단 %-9s 꼬리 %-7s 꼭대기 %+.4f  밑 %+.4f   %s\n"
          % (k, ("%+.4f" % wear) if wear is not None else "없음",
             ("%+.4f" % bi) if bi is not None else "없음",
             ("%.4f" % (wear - bi)) if (wear is not None and bi is not None) else "-",
             max(ys), min(ys), "통과" if not why else "★ " + " · ".join(why)))
    w("\n   ★ 「H-2 대역 통과」는 합격이 아니다 — 6종 전부 H-2 안에 있으면서 털모자만 H-2b 하한을\n")
    w("     미달한다(§14-16-4). 가려짐을 정하는 것은 착용선이 아니라 **밑단**이다.\n")


if __name__ == "__main__":
    main()
