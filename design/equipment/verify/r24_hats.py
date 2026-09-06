# -*- coding: utf-8 -*-
"""R24 — 모자 4종(천모자·중절모·베레모·밀짚모자) H-2 「앞층만」 진단·처방.  python3 r24_hats.py

★ r23_crown.py 와 같은 자 — **프로덕션 소스를 직접 파싱**한다(모델 재실행이 아니다).
  · 인계본 2종(천모자 clothhat / 중절모 fedora): AccessoryShapeBuilder.Handoff.cs 의 좌표 배열
  · v1 2종(베레모 beret / 밀짚모자 straw):      AccessoryShapeBuilder.cs 의 const 식

여기서 재는 것
  ① 앞층만의 반폭 프로필 vs 머리 현 — **어느 y 대역이 왜 비었는가**
  ② H-2 착용선(앞층만) — r23 과 같은 알고리즘
  ③ 처방 후보를 걸었을 때의 ①②
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

_B = open(CS_BUILDER, encoding="utf-8").read()
_H = open(CS_HANDOFF, encoding="utf-8").read()


def const(name):
    m = re.search(r"const float %s\s*=\s*(-?\d+\.?\d*)f" % name, _B)
    if not m:
        raise SystemExit("상수 %s 없음" % name)
    return float(m.group(1))


def sort_const(name):
    return int(re.search(r"const int %s\s*=\s*(-?\d+)" % name, _B).group(1))


SORT_BACK, SORT_EYES, SORT_HEAD = sort_const("SortBack"), sort_const("SortEyes"), sort_const("SortHead")
BASE_HEAD_R, SHIP = 0.22, 0.75
W = max(0.048 * SHIP, 2.0 / (846.0 / 24.0)) / (BASE_HEAD_R * SHIP)
HEAD_R_COVER = 1.184212950170397
COVER_MARGIN = 0.06
TOP_LIMIT, CHIN = 2.551, -1.0
# ★ 2026-09-06 R25 — 옛 목표는 «상한 하나»(감싸는 모자 ≤ +0.30 · 털모자 ≤ 0.00 · 왕관 ≤ +0.45)였고,
#   design-equipment 가 찾은 근본 결함이 바로 그것이다: **하한이 없어서** 모자가 얼마든지 내려앉을 수
#   있었고, 그것이 사용자 신고 「안경 착용시 모자들이 가리는 현상이 많다」의 정체다.
#   R25 는 6종을 대역 안으로 다시 맞췄다.
# ★ 2026-09-06 정본화 — 임시값 (0.30, 0.45) 를 **폐기**하고 §14-16-1 정본으로 교체했다.
#   (0.30 은 확정 6종 실측 최솟값 +0.3254 에서 역산한 추정이었다. 규칙을 실측에 맞춰 깎으면
#    다음 모자에 대해 아무것도 말해 주지 못한다 — §14-16-2.)
#
#   H2_BAND  착용선 ∈ [+0.28, +0.45]
#            상한: 왕관(얹는 물건, 6종 중 가장 위) 실측 +0.4392 를 올림한 R25 정본.
#            하한: **독립된 숫자가 아니다.** 앞층 채움이 없는 높이에서는 덮임도 불가능하므로
#                  `착용선 ≥ 밑단` 이 항등이고, H2B_BAND 하한이 그대로 내려온 값이다.
#   H2B_BAND 밑단(머리 폭 안) ∈ [+0.28, +0.35] · 목표 +0.315
#            상한은 하한을 목표에 **거울대칭**한 값(0.315 + (0.315 − 0.28)). 하한만 걸면 탐색이
#            「가능한 한 높이」로 도망가 모자가 머리에서 뜬다(R25 가 베레모 밑단 +0.4355 로 실측).
#
# ★★ **H-2 대역 통과는 합격이 아니다.** 아래 ② 는 6종을 전부 「통과」로 찍지만, 출하 중인
#    털모자는 H-2b 하한을 0.024 R 미달한다(§14-16-4, 리더 판정 대기). 가려짐을 정하는 것은
#    착용선이 아니라 **밑단**이다 — H-2b 는 `r23_crown.py` ④ 또는 `r25_hats.py` 가 잰다.
# ★ 이 대역은 **「앞층 채움만」** 자에만 건다. 아래 ② 가 나란히 찍는 「채움 ∪ 잉크 윤곽」 열은
#   별개 진단자(§14-14-4 (나))이고, 그 열에 이 대역을 적용하면 6종 중 5종이 **거짓 실패**한다.
# ★ 이 파일의 h2_wear 는 격자 0.004 라 보고값이 참값보다 최대 0.004 **크다**(과대). 그래서
#   상한 검사는 보수적이고 **하한 검사는 0.004 만큼 봐준다**(§14-16-3). 하한을 빠듯하게 볼 때는
#   `r23_crown.py` 의 정확 스캔라인을 쓴다.
H2_BAND = (0.28, 0.45)
H2B_BAND = (0.28, 0.35)
H2B_AIM = 0.315
H2_TARGET = {k: H2_BAND[1] for k in ("천모자", "털모자", "중절모", "왕관", "베레모", "밀짚모자")}

ARR = {}
for _m in re.finditer(r"Handoff_([A-Za-z0-9_]+)\s*=\s*\{(.*?)\};", _H, re.S):
    _v = [float(x[:-1]) for x in re.findall(r"-?\d+\.?\d*f", _m.group(2))]
    ARR[_m.group(1)] = [(_v[i * 2], _v[i * 2 + 1]) for i in range(len(_v) // 2)]

CASES, _cur = {}, None
for _line in _H.splitlines():
    _c = re.search(r"case (Head\w+|Eyes\w+|Back\w+):\s*//\s*(\S+)\s+(\S+)", _line)
    if _c:
        _cur = _c.group(3)
        CASES[_cur] = []
        continue
    _p = re.search(r'HandoffPiece\(sink, rig, xf, (\w+), "([^"]+)", Handoff_([A-Za-z0-9_]+), '
                   r"loop: (\w+), filled: (\w+), tone: (\d+), surfaces: (\d+),.*?layer: (\d+),.*?bodyFixed: (\w+)\)",
                   _line)
    if _p and _cur:
        CASES[_cur].append(dict(sort=_p.group(1), name=_p.group(2), arr=_p.group(3), loop=_p.group(4) == "true",
                                filled=_p.group(5) == "true", tone=int(_p.group(6)),
                                surfaces=int(_p.group(7)), layer=int(_p.group(8)),
                                bodyfixed=_p.group(9) == "true"))

SORTNAME = {"SortBack": SORT_BACK, "SortEyes": SORT_EYES, "SortHead": SORT_HEAD}
LAYER_SORT = {1: SORT_BACK, 2: 3}

_fur = re.search(r"case HeadBeanie: return new AccessoryWornTransform\("
                 r"\s*(-?[\d.]+)f,\s*(-?[\d.]+)f,\s*(-?[\d.]+)f,\s*(-?[\d.]+)f", _H)
FUR_U, FUR_KY, FUR_DY = float(_fur.group(2)), float(_fur.group(3)), float(_fur.group(4))


def handoff_pieces(kind):
    out = []
    for pc in CASES[kind]:
        if pc["surfaces"] not in (0, 1):
            continue
        pts = ARR[pc["arr"]]
        if not pc["bodyfixed"] and kind == "furhat":
            pts = [(x * FUR_U, y * FUR_U * FUR_KY + FUR_DY) for x, y in pts]
        # 배열 이름은 Handoff_<kind>_<src>[_worn] — kind 를 잘라 내야 src 가 나온다
        #   (첫 판은 split("_",2)[-1] 이라 «B0_worn» 이 «worn» 이 됐고, 그 탓에 아래 역변환이 조용히 아무것도 안 했다)
        _src = pc["arr"][len(kind) + 1:]
        if _src.endswith("_worn"):
            _src = _src[:-5]
        out.append(dict(name=pc["name"], src=_src, pts=pts, filled=pc["filled"],
                        loop=pc["loop"], tone=pc["tone"], sort=LAYER_SORT.get(pc["layer"], SORTNAME[pc["sort"]])))
    return out


BAND = const("AccentBandThicknessRatio")


# ============================================================================
# ★ 2026-09-06 R25 — 베레모·밀짚모자도 **좌표를 직접 파싱**한다 (유령 계측 제거)
# ============================================================================
# 전에는 이 두 함수가 «v1 switch 의 조립 로직을 파이썬으로 다시 구현»하고 상수만 프로덕션에서
# 읽었다. 그 형태의 위험은 <b>구조가 갈라지면 아무도 모른다</b>는 것이다 — 프로덕션 도형이
# 2층·곡선으로 바뀌어도 이 파일은 옛 7점 다각형을 계속 그려 「측정했다」고 말한다.
# R25 가 두 종을 R21 재저작본으로 갈아치우면서 그 위험이 실제가 됐다.
#
# 지금은 인계본과 **같은 방식**이다: `private static readonly float[] V1Hat_* = {...}` 배열과
# `HeadV1Piece(...)` 호출을 그대로 읽는다. 조립 로직이 이 파일에 없으므로 갈라질 자리가 없다.
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


def beret(over=None):
    return _v1_pieces("HeadBeret")


def straw(over=None):
    return _v1_pieces("HeadStraw")


def browline():
    cy = const("GlassesCenterRatio")
    bb = cy + const("BrowlineBarBottomRatio")
    o, i2, tp = const("BrowlineBarOuterRatio"), const("BrowlineBarInnerRatio"), const("BrowlineBarTopRatio")
    li = const("BrowlineLensInnerRatio")
    bar = [(-o, bb), (-i2, cy + tp), (i2, cy + tp - 0.02), (o, bb - 0.02), (li, bb - 0.04), (-li, bb - 0.02)]
    lf = [(li, bb - 0.04), (0.44, cy - 0.52), (1.00, cy - 0.34), (o, bb - 0.02)]
    lb = [(-li, bb - 0.02), (-o, bb), (-0.98, cy - 0.32), (-0.44, cy - 0.50)]
    return [dict(name="BrowlineBar", src="BAR", pts=bar, filled=True, loop=True, tone=1, sort=SORT_EYES),
            dict(name="BrowlineLensB", src="LB", pts=lb, filled=True, loop=True, tone=0, sort=SORT_EYES),
            dict(name="BrowlineLensF", src="LF", pts=lf, filled=True, loop=True, tone=0, sort=SORT_EYES)]


def patch():
    cy = const("GlassesCenterRatio")
    hw, hh = const("PatchHalfWidthRatio"), const("PatchHalfHeightRatio")
    cx = const("DrawnEyeOffsetRatio")
    reach, deg = const("PatchStrapReachRatio"), const("PatchStrapDegrees")
    er = const("DrawnEyeRadiusRatio")
    top, bot = (cx - hw, cy + hh), (cx - hw + 0.04, cy - hh * 0.82)
    cover = [top, (cx + hw, cy + hh * 0.82), (cx + hw - 0.06, cy - hh), bot]
    pol = lambda d: (math.cos(math.radians(d)) * reach, math.sin(math.radians(d)) * reach)
    strap = [pol(deg), top, bot, pol(360.0 - deg)]
    eye = [(-cx + er * math.cos(2 * math.pi * i / 24), cy + er * math.sin(2 * math.pi * i / 24)) for i in range(24)]
    return [dict(name="PatchCover", src="CV", pts=cover, filled=True, loop=True, tone=0, sort=SORT_EYES),
            dict(name="PatchStrap", src="ST", pts=strap, filled=False, loop=False, tone=0, sort=SORT_EYES),
            dict(name="PatchEye", src="EY", pts=eye, filled=True, loop=True, tone=1, sort=SORT_EYES)]


HATS = {"천모자": lambda: handoff_pieces("clothhat"), "털모자": lambda: handoff_pieces("furhat"),
        "중절모": lambda: handoff_pieces("fedora"), "왕관": lambda: handoff_pieces("crown"),
        "베레모": beret, "밀짚모자": straw}
EYES = {"선글라스": lambda: handoff_pieces("sunglasses"), "동그란안경": lambda: handoff_pieces("roundglasses"),
        "고글": lambda: handoff_pieces("goggles"), "외알안경": lambda: handoff_pieces("monocle"),
        "뿔테": browline, "안대": patch}

STEP = 0.004
_gx = np.arange(-2.8, 2.8, STEP)
_gy = np.arange(-1.6, 2.9, STEP)
_GX, _GY = np.meshgrid(_gx, _gy)
PTS = np.column_stack([_GX.ravel(), _GY.ravel()])


def inside(poly, pts):
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


XS = np.arange(-3.2, 3.2, 0.002)
I0 = int(np.searchsorted(XS, 0.0))


def hw_at(front, y, with_stroke=False):
    row = np.column_stack([XS, np.full_like(XS, y)])
    cov = np.zeros(len(XS), bool)
    for p in front:
        cov |= inside(p["pts"], row)
        if with_stroke:
            cov |= stroke(p["pts"], p["loop"], row)
    if not cov[I0]:
        return 0.0
    a = I0
    while a > 0 and cov[a - 1]:
        a -= 1
    b = I0
    while b < len(XS) - 1 and cov[b + 1]:
        b += 1
    return min(abs(XS[a]), abs(XS[b]))


def h2_wear(pieces, trace=False, with_stroke=False):
    """with_stroke=False → §14-12 자(앞층 **채움만**). True → 둘째 자(채움 ∪ 잉크 윤곽).
    윤곽은 tone HEADINK = 머리와 같은 잉크색이라 머리를 실제로 가린다 — 그래서 따로 잰다."""
    front = [p for p in pieces if p["sort"] >= SORT_EYES and (p["filled"] or with_stroke)]
    wear, rows = None, []
    for y in np.arange(HEAD_R_COVER, -1.2, -0.004):
        hw = hw_at(front, y, with_stroke)
        need = math.sqrt(max(HEAD_R_COVER ** 2 - y * y, 0.0)) + COVER_MARGIN
        if trace:
            rows.append((float(y), hw, need))
        if hw >= need:
            wear = float(y)
        elif wear is not None:
            break
    return (wear, rows) if trace else wear


def diag(name, pieces):
    front = [p for p in pieces if p["filled"] and p["sort"] >= SORT_EYES]
    print("\n── %s — 앞층 채움 조각 %d개" % (name, len(front)))
    for p in front:
        ys = [q[1] for q in p["pts"]]
        xs = [q[0] for q in p["pts"]]
        print("     %-14s y[%+.3f,%+.3f] x[%+.3f,%+.3f]" % (p["name"], min(ys), max(ys), min(xs), max(xs)))
    back = [p for p in pieces if p["filled"] and p["sort"] < SORT_EYES]
    for p in back:
        ys = [q[1] for q in p["pts"]]
        print("     (뒤층) %-8s y[%+.3f,%+.3f]" % (p["name"], min(ys), max(ys)))
    print("     y      앞층반폭  필요     여유")
    for y in np.arange(1.15, 0.05, -0.05):
        hw = hw_at(front, float(y))
        need = math.sqrt(max(HEAD_R_COVER ** 2 - y * y, 0.0)) + COVER_MARGIN
        print("    %+.2f   %6.3f   %6.3f   %+6.3f %s" % (y, hw, need, hw - need, "" if hw >= need else "  ← 빈다"))


def coverage_runs(pieces, with_stroke=False, step=0.01):
    """머리 꼭대기에서 내려오며 「덮인 y 구간」 전부. r23 의 h2_wear 는 **첫 구간의 끝**만 돌려주므로
    구간이 둘로 끊긴 것과 애초에 위쪽을 안 덮은 것을 구별하지 못한다 — 그것을 여기서 갈라 본다."""
    front = [p for p in pieces if p["sort"] >= SORT_EYES and (p["filled"] or with_stroke)]
    ys = np.arange(HEAD_R_COVER, -1.2, -step)
    ok = []
    for y in ys:
        hw = hw_at(front, float(y), with_stroke)
        need = math.sqrt(max(HEAD_R_COVER ** 2 - y * y, 0.0)) + COVER_MARGIN
        ok.append(hw >= need)
    out, i = [], 0
    while i < len(ys):
        if ok[i]:
            j = i
            while j + 1 < len(ys) and ok[j + 1]:
                j += 1
            out.append((float(ys[i]), float(ys[j])))
            i = j + 1
        else:
            i += 1
    return out


def main():
    hats = {k: f() for k, f in HATS.items()}
    eyes = {k: f() for k, f in EYES.items()}
    ei = {k: ink(v) for k, v in eyes.items()}
    print("R24 — 자: 프로덕션 소스 직접 파싱(모델 재실행 아님). 획 W = %.5f R · 머리 %.4f R · 여유 %.2f R"
          % (W, HEAD_R_COVER, COVER_MARGIN))

    print("\n① 앞층 채움이 머리 현을 덮는 **구간 전부** — 「착용선 하나」로는 안 보이는 것")
    print("   ★ r23 의 h2_wear 는 **첫 구간의 끝**만 돌려준다. 구간이 [1.18→0.07] 처럼 위가 비어 있어도")
    print("     아래에서 처음 덮이면 그 값을 「착용선」이라 부른다 — 밀짚모자의 「통과」가 그 경우다.")
    for k in hats:
        r1 = coverage_runs(hats[k], False)
        r2 = coverage_runs(hats[k], True)
        top_ok = bool(r1) and r1[0][0] >= HEAD_R_COVER - 0.02
        print("   %-6s 채움만   %-34s %s" % (k, ["%+.2f→%+.2f" % r for r in r1], "" if top_ok else "★ 머리 꼭대기부터 안 덮는다"))
        print("          채움∪윤곽 %s" % (["%+.2f→%+.2f" % r for r in r2],))

    print("\n② H-2 착용선 — 두 자  (정본 대역 [%+.2f, %+.2f] · §14-16-1)" % H2_BAND)
    print("   ★ 대역 판정은 **왼쪽(채움만)** 열에만 건다. 오른쪽(채움∪잉크윤곽)은 별개 진단자다(§14-14-4 (나)).")
    print("   ★ 그리고 이 표의 「통과」는 합격이 아니다 — H-2b(밑단 ∈ [%+.2f, %+.2f])를 같이 봐야 한다."
          % H2B_BAND)
    print("     실제로 6종 전부 여기서 통과하면서 털모자만 H-2b 하한을 0.024 R 미달한다(§14-16-4).")
    print("   %-8s %-12s %-12s %s" % ("모자", "채움만", "채움∪잉크윤곽", "대역 판정"))
    for k in hats:
        a, b = h2_wear(hats[k]), h2_wear(hats[k], with_stroke=True)
        if a is None:
            verdict = "★ 실패(덮임 없음)"
        elif a < H2_BAND[0] - 1e-6:
            verdict = "★ 하한 미달(너무 내려왔다 — 안경을 가린다)"
        elif a > H2_BAND[1] + 1e-6:
            verdict = "★ 상한 초과(너무 올라갔다 — 얹힌다)"
        else:
            verdict = "통과"
        print("   %-8s %-12s %-12s %s"
              % (k, ("%+.4f" % a) if a is not None else "없음", ("%+.4f" % b) if b is not None else "없음", verdict))

    print("\n③ 안경 6종 가려짐 %(안경 잉크 면적 중 모자 앞층 채움에 덮인 비율)")
    hm = {k: front_fill(v) for k, v in hats.items()}
    print("   %-8s" % "" + "".join("%-11s" % e for e in eyes))
    for k in hats:
        print("   %-8s" % k + "".join("%-11s" % ("%.1f" % (100.0 * (ei[e] & hm[k]).sum() / ei[e].sum())) for e in eyes))

    print("\n④ 앞층 채움 반폭 프로필 (수정 대상 4종)")
    for k in ("천모자", "중절모", "베레모", "밀짚모자"):
        diag(k, hats[k])


if __name__ == "__main__":
    main()
