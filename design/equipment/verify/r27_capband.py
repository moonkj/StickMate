# -*- coding: utf-8 -*-
"""R27 — 천모자·중절모 **챙 띠 잔여 색면** 계측기.  python3 r27_capband.py

★ r23_crown.py · r24_hats.py 와 같은 자 — **프로덕션 소스를 직접 파싱**한다(모델 재실행이 아니다).
  좌표: Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs 의 배열 + HandoffPiece 호출 표.

무엇을 재는가
  화면에 실제로 남는 **보조색(M2) 순색면**의 세로 두께. 화가 알고리즘을 프로덕션 z 규약 그대로 옮긴다:
    · 채움  → sortingOrder − 1        (CharacterAccessoryRenderer.AddFill / Shape.FillSortingOrder)
    · 선    → sortingOrder            (AddLine)
    ⇒ **한 아이템의 모든 선은 그 아이템의 모든 채움 위에 온다.** 이것이 결함의 기하학적 원인이다.
    · 층(layer) 1 = Back → SortBack(−1) : 머리(4)보다 뒤 · 층 0 → 슬롯 기본(SortHead=10)
    · 머리 잉크 원반(반경 HEAD_OUTER_R)은 정렬 4.
  획 폭(월드 → R) = max(strokeInR, 액세서리 하한 1.00pt ÷ 머리 반경 pt)
    (CharacterAccessoryRenderer.AddLine 의 handoffWidth 분기 · StickConfig.MinAccessoryStrokeScreenPoints)

★ 자기교정: 아래 `_selftest()` 가 답을 손으로 아는 도형(사각 채움 + 그 위 가로선)에서 잔여 두께를 먼저 맞춘다.
  교정이 깨지면 이 파일의 숫자는 전부 폐기다(TEAM.md 공통 처방).
"""
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CS_HANDOFF = os.path.join(REPO, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs")
CS_BUILDER = os.path.join(REPO, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs")

_H = open(CS_HANDOFF, encoding="utf-8").read()
_B = open(CS_BUILDER, encoding="utf-8").read()

# ---- 프로덕션 상수(소스에서 되읽는다) -------------------------------------------------
def _sort_const(name):
    return int(re.search(r"const int %s\s*=\s*(-?\d+)" % name, _B).group(1))

SORT_BACK = _sort_const("SortBack")          # −1
SORT_HEAD = _sort_const("SortHead")          # 10
SORT_CAPE_FRONT = _sort_const("SortCapeFront")
HEAD_SORT = 4                                # 머리 링(AccessoryShapeBuilder.SortCapeFront 문서: "머리 링(4)")

BASE_HEAD_R = 0.22                           # AccessoryShapeBuilder.BaselineHeadVisualRadius
PT_PER_UNIT = 846.0 / (2.0 * 12.0)           # StickConfig.ReferencePointsPerWorldUnitApprox = 35.25
MIN_ACCESSORY_PT = 1.0                       # StickConfig.MinAccessoryStrokeScreenPoints
HEAD_OUTER_R = 1.0 + (2.0 / 3.0) * 0.0 + 0.171932 / 2.0   # r16_model.HEAD_OUTER_R = 1.171932 (배율 0.75 링 1pt)

def head_r_pt(scale):
    return BASE_HEAD_R * scale * PT_PER_UNIT

def acc_stroke_in_R(stroke_in_r, scale):
    """그 배율에서 이 획이 실제로 그리는 폭(R 배수)."""
    return max(stroke_in_r, MIN_ACCESSORY_PT / head_r_pt(scale))

# ---- 프로덕션 좌표·조각 표 파싱 -------------------------------------------------------
CALL = re.compile(
    r'HandoffPiece\(sink, rig, xf, (\w+), "([^"]+)", Handoff_([A-Za-z0-9_]+), '
    r"loop: (\w+), filled: (\w+), tone: (\d+), surfaces: (\d+), "
    r"strokeMult: ([\d.]+)f?, strokeInR: ([\d.]+)f?, noStroke: (\w+), alpha: ([\d.]+)f?, "
    r"lineAlpha: ([\d.]+)f?, underBack: (\d+), layer: (\d+), swayStart: (-?\d+), swayCount: (\d+), "
    r"bodyFixed: (\w+)\)")


def parse(text):
    """생성 파일 원문 → {아이템종: [조각 dict, ...]}. 옛 판(git show)과 새 판을 같은 자로 재기 위해 함수로 둔다."""
    arr = {}
    for m in re.finditer(r"Handoff_([A-Za-z0-9_]+)\s*=\s*\{(.*?)\};", text, re.S):
        v = [float(x[:-1]) for x in re.findall(r"-?\d+\.?\d*f", m.group(2))]
        arr[m.group(1)] = [(v[i * 2], v[i * 2 + 1]) for i in range(len(v) // 2)]
    cases, cur = {}, None
    for line in text.splitlines():
        c = re.search(r"case (Head\w+|Eyes\w+|Back\w+):\s*//\s*(\S+)\s+(\S+)", line)
        if c:
            cur = c.group(3)
            cases[cur] = []
            continue
        p = CALL.search(line)
        if p and cur:
            g = p.groups()
            cases[cur].append(dict(sort=g[0], name=g[1], arr=g[2], pts=arr[g[2]],
                                   loop=g[3] == "true", filled=g[4] == "true", tone=int(g[5]),
                                   surfaces=int(g[6]), stroke_in_r=float(g[8]),
                                   no_stroke=g[9] == "true", alpha=float(g[10]), line_alpha=float(g[11]),
                                   layer=int(g[13]), body_fixed=g[16] == "true"))
    return cases

CASES = parse(_H)

# ---- 화가 알고리즘(세로 스캔) ---------------------------------------------------------
class Elem:
    """칠해질 것 하나. kind='fill'(다각형) | 'stroke'(폴리라인, 반폭 r) | 'disc'(원)."""
    __slots__ = ("kind", "pts", "loop", "r", "z", "sub", "tag")
    def __init__(self, kind, pts, loop, r, z, sub, tag):
        self.kind, self.pts, self.loop, self.r, self.z, self.sub, self.tag = kind, pts, loop, r, z, sub, tag

def _fill_spans(pts, x):
    """다각형(항상 닫힌 것으로 본다 — BuildFillMesh 는 loop 를 안 본다)의 세로 구간들."""
    ys = []
    n = len(pts)
    for i in range(n):
        (ax, ay), (bx, by) = pts[i], pts[(i + 1) % n]
        if (ax > x) == (bx > x):
            continue
        ys.append(ay + (x - ax) * (by - ay) / (bx - ax))
    ys.sort()
    return [(ys[k], ys[k + 1]) for k in range(0, len(ys) - 1, 2)]

def _stroke_spans(pts, loop, r, x, step=0.0004):
    """폴리라인 획(반폭 r, 둥근 캡/조인)이 세로선 x 에서 덮는 구간들."""
    spans = []
    n = len(pts)
    segs = n if loop else n - 1
    for i in range(segs):
        (ax, ay), (bx, by) = pts[i], pts[(i + 1) % n]
        L = math.hypot(bx - ax, by - ay)
        m = max(2, int(L / step) + 2)
        for k in range(m + 1):
            t = k / m
            px, py = ax + (bx - ax) * t, ay + (by - ay) * t
            d = abs(x - px)
            if d > r:
                continue
            h = math.sqrt(r * r - d * d)
            spans.append((py - h, py + h))
    return spans

def _disc_spans(cx, cy, rad, x):
    d = abs(x - cx)
    if d > rad:
        return []
    h = math.sqrt(rad * rad - d * d)
    return [(cy - h, cy + h)]

def paint_column(elems, x, y_lo, y_hi, ny=40000):
    """세로선 x 를 z 오름차순으로 칠하고, 각 y 표본의 최종 tag 를 돌려준다."""
    dy = (y_hi - y_lo) / ny
    out = [None] * ny
    for e in sorted(elems, key=lambda q: (q.z, q.sub)):
        if e.kind == "fill":
            spans = _fill_spans(e.pts, x)
        elif e.kind == "stroke":
            spans = _stroke_spans(e.pts, e.loop, e.r, x)
        else:
            spans = _disc_spans(e.pts[0][0], e.pts[0][1], e.r, x)
        for a, b in spans:
            i0 = max(0, int(math.ceil((a - y_lo) / dy)))
            i1 = min(ny - 1, int(math.floor((b - y_lo) / dy)))
            for i in range(i0, i1 + 1):
                out[i] = e.tag
    return out, dy

def runs_of(col, dy, y_lo, tag):
    """그 tag 로 남은 연속 구간 [(y0, y1, 두께R), ...]."""
    res, i, n = [], 0, len(col)
    while i < n:
        if col[i] != tag:
            i += 1
            continue
        j = i
        while j + 1 < n and col[j + 1] == tag:
            j += 1
        res.append((y_lo + i * dy, y_lo + (j + 1) * dy, (j + 1 - i) * dy))
        i = j + 1
    return res

# ---- 모자 한 종의 착용 조각 → Elem 목록 -------------------------------------------
def worn_elems(kind, scale, drop_closing_edge_of=(), cases=None):
    """drop_closing_edge_of: 그 조각 이름(name)의 **닫힘변 획**을 없앤 가정(= 이번 처방 시뮬레이션)."""
    src = CASES if cases is None else cases
    pieces = [p for p in src[kind] if p["body_fixed"]]
    elems = [Elem("disc", [(0.0, 0.0)], True, HEAD_OUTER_R, HEAD_SORT, 0, "HEAD")]
    for idx, p in enumerate(pieces):
        sort = SORT_BACK if p["layer"] == 1 else (SORT_CAPE_FRONT if p["layer"] == 2 else SORT_HEAD)
        if p["filled"] and p["alpha"] > 0:
            elems.append(Elem("fill", p["pts"], True, 0.0, sort - 1, idx, ("FILL", p["name"], p["tone"])))
        if not p["no_stroke"] and p["line_alpha"] > 0:
            loop = p["loop"]
            pts = p["pts"]
            if p["name"] in drop_closing_edge_of:
                loop = False          # 닫힘변만 안 그린다(채움은 그대로)
            w = acc_stroke_in_R(p["stroke_in_r"], scale)
            elems.append(Elem("stroke", pts, loop, w / 2.0, sort, idx, ("LINE", p["name"], p["tone"])))
    return elems, pieces

ACCENT_TONE = 1     # TONE_ACCENT = M2(보조색)

def band_profile(kind, scale, xs, drop=()):
    elems, _ = worn_elems(kind, scale, drop)
    Rpt = head_r_pt(scale)
    rows = []
    for x in xs:
        col, dy = paint_column(elems, x, -1.6, 2.6)
        accent = [r for r in runs_of_tone(col, dy, -1.6, ACCENT_TONE)]
        rows.append((x, [(a, b, t, t * Rpt) for a, b, t in accent]))
    return rows

def runs_of_tone(col, dy, y_lo, tone):
    res, i, n = [], 0, len(col)
    def ok(t):
        return t is not None and t[0] == "FILL" and t[2] == tone
    while i < n:
        if not ok(col[i]):
            i += 1
            continue
        j = i
        while j + 1 < n and ok(col[j + 1]):
            j += 1
        res.append((y_lo + i * dy, y_lo + (j + 1) * dy, (j + 1 - i) * dy))
        i = j + 1
    return res

# ---- 자기교정 ----------------------------------------------------------------------
def _selftest():
    # 사각 채움(두께 1.0 R, tone 1) 위에 윗변을 정확히 덮는 폭 0.2 R 가로 획 → 잔여 = 1.0 − 0.1 = 0.9 R
    square = [(-1.0, 0.0), (1.0, 0.0), (1.0, 1.0), (-1.0, 1.0)]
    elems = [Elem("fill", square, True, 0.0, 0, 0, ("FILL", "sq", 1)),
             Elem("stroke", [(-1.0, 1.0), (1.0, 1.0)], False, 0.1, 1, 1, ("LINE", "top", 4))]
    col, dy = paint_column(elems, 0.0, -1.0, 2.0)
    runs = runs_of_tone(col, dy, -1.0, 1)
    assert len(runs) == 1, runs
    assert abs(runs[0][2] - 0.9) < 3e-4, runs
    # 위·아래 둘 다 획이면 잔여 = 0.8 R
    elems.append(Elem("stroke", [(-1.0, 0.0), (1.0, 0.0)], False, 0.1, 1, 2, ("LINE", "bot", 4)))
    col, dy = paint_column(elems, 0.0, -1.0, 2.0)
    runs = runs_of_tone(col, dy, -1.0, 1)
    assert len(runs) == 1 and abs(runs[0][2] - 0.8) < 3e-4, runs
    # 같은 아이템의 «선이 채움 위»라는 규약이 안 지켜지면(순서를 뒤집으면) 잔여가 1.0 으로 되돌아온다 — 양성 대조
    elems2 = [Elem("stroke", [(-1.0, 1.0), (1.0, 1.0)], False, 0.1, 0, 0, ("LINE", "top", 4)),
              Elem("fill", square, True, 0.0, 1, 1, ("FILL", "sq", 1))]
    col, dy = paint_column(elems2, 0.0, -1.0, 2.0)
    runs = runs_of_tone(col, dy, -1.0, 1)
    assert len(runs) == 1 and abs(runs[0][2] - 1.0) < 3e-4, runs
    print("자 교정 OK — 잔여 0.9 / 0.8 / (양성대조)1.0 R")

# ---- 보고 --------------------------------------------------------------------------
def report(out=sys.stdout):
    w = lambda s="": print(s, file=out)
    _selftest()
    w()
    w("== R27 챙 보조색(M2) 잔여 색면 — 프로덕션 좌표 직접 파싱 ==")
    w("   머리 반경 pt: 배율 0.75 → %.4f pt · 1.00 → %.4f pt" % (head_r_pt(0.75), head_r_pt(1.0)))
    w("   인계본 획 실폭: 0.75 → %.5f R(%.3f pt) · 1.00 → %.5f R(%.3f pt)"
      % (acc_stroke_in_R(0.13845, 0.75), acc_stroke_in_R(0.13845, 0.75) * head_r_pt(0.75),
         acc_stroke_in_R(0.13845, 1.0), acc_stroke_in_R(0.13845, 1.0) * head_r_pt(1.0)))
    xs = [0.0, 0.4, 0.8, 1.0, 1.14, 1.2, 1.4, 1.6, 1.75]
    old = None
    if len(sys.argv) > 2 and sys.argv[1] == "--baseline":
        old = parse(open(sys.argv[2], encoding="utf-8").read())
        w("   기준(옛 판): %s" % sys.argv[2])
    for kind, drop_name in (("clothhat", "Piece_B0"), ("fedora", "Piece_B0")):
        for scale in (0.75, 1.0):
            Rpt = head_r_pt(scale)
            w()
            w("-- %s · 배율 %.2f (macOS Retina = 2px/pt · Windows 100%% = 1px/pt) --" % (kind, scale))
            w("      x         옛 판(pt / macOSpx / Winpx)      현행(pt / macOSpx / Winpx)      증가")
            base_e, _ = (worn_elems(kind, scale, cases=old) if old is not None
                         else worn_elems(kind, scale, drop_closing_edge_of=()))
            fix_e, _ = worn_elems(kind, scale)
            for x in xs:
                c0, dy = paint_column(base_e, x, -1.6, 2.6)
                c1, _ = paint_column(fix_e, x, -1.6, 2.6)
                r0 = runs_of_tone(c0, dy, -1.6, ACCENT_TONE)
                r1 = runs_of_tone(c1, dy, -1.6, ACCENT_TONE)
                t0 = max([t for _, _, t in r0], default=0.0) * Rpt
                t1 = max([t for _, _, t in r1], default=0.0) * Rpt
                w("   %6.3f   %6.3f pt %5.2f px %5.2f px      %6.3f pt %5.2f px %5.2f px   %+6.3f pt (덩어리 %d→%d)"
                  % (x, t0, t0 * 2, t0, t1, t1 * 2, t1, t1 - t0, len(r0), len(r1)))

if __name__ == "__main__":
    report()
