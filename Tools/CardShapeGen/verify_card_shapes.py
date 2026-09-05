#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""인계본 조각 **역대조** — 생성물을 다른 경로로 되읽어 R16(카드)/R17(몸) 모델과 맞춘다. (coder, 2026-09-05)

    python3 Tools/CardShapeGen/verify_card_shapes.py        # 종료코드 0 = 전부 일치

생성기(gen_card_shapes.py)와 코드를 공유하지 않는 부분:
  · C# 생성 파일은 **정규식으로 리터럴/호출/변환 표를 되읽는다**.
  · NECK 에셋은 **YAML 을 직접 읽고 항 스트림을 독립 디코더**로 푼다.
  · 기대값은 r16/r17 모델(정본) **과** r17_coords.txt(설계자 산출물, 4자리) 둘 다에 맞춘다.
  · 몸 좌표 = 카드 좌표 × 아이템 변환 — 그 변환을 **여기서 다시 적용**해 r17 몸 좌표와 맞춘다(생성기의 분해와 독립).
교정(양성 대조): 마지막에 좌표 한 칸을 흔든 사본으로 같은 비교기를 돌려 **실제로 빨개지는지** 본다.
"""
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
VERIFY = os.path.join(REPO, "design", "equipment", "verify")
sys.path.insert(0, VERIFY)
import rig                          # noqa: E402
import handoff                      # noqa: E402
import r16_model as M16             # noqa: E402
import r17_model as M17             # noqa: E402
import palette_model as PM          # noqa: E402
import r19_model as M19             # noqa: E402
import r20_model as M20             # noqa: E402

CS = os.path.join(REPO, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs")
CONTRACT = os.path.join(REPO, "Assets/_Project/Scripts/Core/AccessoryShapeContract.cs")
CARD_ICON = os.path.join(REPO, "Assets/_Project/Scripts/Interaction/AccessoryCardIcon.cs")
GOLDEN = os.path.join(REPO, "Assets/_Project/Scripts/Tests/EditMode/Golden/CardShapeGolden.txt")
ITEMS = os.path.join(REPO, "Assets/_Project/Resources/Items")
COORDS_TXT = os.path.join(VERIFY, "r19_coords.txt")
DECIMALS = 5
TOL = 0.5 * 10 ** -DECIMALS + 1e-9
PENDING_R17 = set()                 # R17c/R17d 착지(코디네이터 통보 2026-09-05)

NECK_ASSET = {"bowtie": "equip_neck_bowtie", "stripedtie": "equip_neck_striped",
              "scarf": "equip_neck_scarf", "bellnecklace": "equip_neck_bell"}
ITEM_CONST = {
    "clothhat": "HeadCap", "furhat": "HeadBeanie", "fedora": "HeadFedora", "crown": "HeadCrown",
    "sunglasses": "EyesSunglasses", "roundglasses": "EyesRound", "goggles": "EyesGoggles", "monocle": "EyesMonocle",
    "shortcape": "BackCape", "longcape": "BackLongCape", "wings": "BackWings", "backpack": "BackBackpack",
}

# 흔들 구간(원칙 1) — 생성기 SWAY_RULES 와 같은 뜻을 **다른 형태**로 적는다: 조각 전체 / 밑단 y 문턱 / 고정 인덱스.
SWAY_ALL = {("scarf", "S2"), ("scarf", "S3"), ("scarf", "S4"),
            ("bellnecklace", "B4"), ("bellnecklace", "RB5"), ("bellnecklace", "CB6"), ("bellnecklace", "H7")}
SWAY_LOWER = {("scarf", "B1"): 0.35, ("stripedtie", "B1"): 0.20}
SWAY_BAND = {("shortcape", "STB"): (16, 17), ("longcape", "STB"): (16, 17)}
MONDAY_DROP_R = 0.12          # v1 TieMondayLoosenDropRatio — 에셋 게이트 항으로 눕는다(줄무늬타이만)
# 팔레트 역할 → 톤(생성기와 같은 뜻을 별개 표로): 채움 M/M2/SH/EYE = 0/1/2/6 · 선 INK/M/M2/W = 5/0/1/3
FILL_T = {"M": 0, "M2": 1, "SH": 2, "EYE": 5}
TONE_GLASS = 6
LINE_T = {"INK": 4, "M": 0, "M2": 1, "W": 3}

def expected_sway(kind, src, pts):
    if (kind, src) in SWAY_ALL: return (0, len(pts))
    if (kind, src) in SWAY_BAND: return SWAY_BAND[(kind, src)]
    f = SWAY_LOWER.get((kind, src))
    if f is None: return (-1, 0)
    ys = [y for _, y in pts]; cut = min(ys) + f * (max(ys) - min(ys)) + 1e-9
    on = [i for i, y in enumerate(ys) if y <= cut]
    return (on[0], on[-1] - on[0] + 1)

fails = []
def fail(msg):
    fails.append(msg)
    print(("CONTROL(의도된 빨강) " if msg.startswith("control/") else "FAIL ") + msg)

# ============================================================================
# 기대값 — 모델(정본)에서 **여기서 다시** 유도한다(생성기의 Row 와 별개 구현)
# ============================================================================
def crole(kind, p):
    cr = getattr(p, "crole", None)
    return cr if cr is not None else PM.role_of(kind, p.src)

def is_wash(kind, p):
    fr, _ = crole(kind, p); return isinstance(fr, tuple) and fr[0] == "CARDWASH"

def is_matline(kind, p):
    fr, lr = crole(kind, p); return fr is not None and not isinstance(fr, tuple) and lr in ("M", "M2")

def layer_of(kind, p):
    if getattr(p, "layer", None) == "back": return 1
    if getattr(p, "layer", None) == "front" and M16.ISLOT[kind] == "back": return 2
    return 0

def two_list(kind):
    return kind in M16.CAPES or {p.src for p in M19.WORN[kind]} != {p.src for p in M19.CARD[kind]}

def expect_piece(kind, p, surfaces, pts=None, body_pts=None, body_fixed=False, variant=None, layer_from=None):
    suffix = {"glass": "glass", "rim": ("rim" if is_matline(kind, p) else "")}.get(variant, "")
    d = dict(name=("Piece_ExposedEye" if p.call == "EYE" else "Piece_" + p.src) + suffix,
             pts=[(float(x), float(y)) for x, y in (pts if pts is not None else p.pts)],
             body_pts=[(float(x), float(y)) for x, y in body_pts] if body_pts is not None else None,
             loop=bool(p.loop), filled=bool(p.filled), surfaces=surfaces, mult=float(p.mult), in_r=float(p.nominal_R),
             no_stroke=p.line is None, alpha=0.0, line_alpha=0.0,
             layer=(0 if surfaces == 2 else layer_of(kind, layer_from if layer_from is not None else p)),
             sway=expected_sway(kind, p.src, body_pts if body_pts is not None else (pts if pts is not None else p.pts)),
             body_fixed=body_fixed, extra=(variant == "rim" and is_matline(kind, p)) or variant == "glass", r19name=p.name)
    fr, lr = crole(kind, p)
    la = float(p.line[-1]) if p.line is not None else 0.0
    if variant == "glass":
        d.update(filled=True, tone=TONE_GLASS, alpha=float(fr[1]), no_stroke=True, line_alpha=0.0)
    elif variant == "fill":
        d.update(filled=True, tone=FILL_T[fr], alpha=1.0, no_stroke=True, line_alpha=0.0)
    elif variant == "rim":
        d.update(filled=False, tone=LINE_T[lr], alpha=0.0, no_stroke=False, line_alpha=la)
    elif fr is not None:
        d.update(filled=True, tone=FILL_T[fr], alpha=1.0, no_stroke=lr is None, line_alpha=(la if lr is not None else 0.0))
    else:
        d.update(filled=False, tone=LINE_T[lr], alpha=0.0, no_stroke=False, line_alpha=la)
    if d["in_r"] <= 0: d["in_r"] = M16.SLOT_STROKE_R[M16.ISLOT[kind]]
    return d

def expect_split(kind, p, surfaces, **kw):
    if is_wash(kind, p):
        return [expect_piece(kind, p, surfaces, variant="glass", **kw), expect_piece(kind, p, surfaces, variant="rim", **kw)]
    if is_matline(kind, p):
        return [expect_piece(kind, p, surfaces, variant="fill", **kw), expect_piece(kind, p, surfaces, variant="rim", **kw)]
    return [expect_piece(kind, p, surfaces, **kw)]

def under_of(rows):
    for i, r in enumerate(rows):
        r["under"] = 0
        for j in range(i):
            q = rows[j]
            if not q["filled"]: continue
            cov = sum(1 for pt in r["pts"] if rig.contains(q["pts"], pt)) / len(r["pts"])
            if cov >= 0.9: r["under"] = i - j

def expected_transform(kind):
    ga = 0.0 if PM.GROUP_ALPHA_BODY >= 1.0 else (M16.WORN[kind][0].group_alpha if M16.WORN[kind][0].group_alpha != 1.0 else 0.0)   # 팔레트 L-3
    t = dict(group_alpha=ga, scale=0.0, scale_y=0.0, offset_y=0.0, mirror=False)
    if kind in PENDING_R17 or two_list(kind): return t
    if kind in M17.HEAD_KINDS:
        f = M17.HAT_FIT[kind]; fx, fy, sx = handoff.icon_to_R(kind)
        s_ = f["u"] / sx; sy = s_ * f["ky"]
        t.update(scale=s_, scale_y=f["ky"], offset_y=f["dy"] - fy(0.0) * sy)
    elif kind == "stripedtie": t.update(offset_y=M17.TIE_DY)
    elif kind == "bowtie": t.update(offset_y=M19.BOWTIE_DY)
    return t

def apply_transform(pts, t):
    s_ = t["scale"] if t["scale"] > 0 else 1.0
    sy = s_ * (t["scale_y"] if t["scale_y"] > 0 else 1.0)
    m = -1.0 if t["mirror"] else 1.0
    return [(x * s_ * m, y * sy + t["offset_y"]) for x, y in pts]

def expected_rows(kind):
    if two_list(kind):
        icon = [r for p in M19.CARD[kind] for r in expect_split(kind, p, 2)]
        worn = [r for p in M19.WORN[kind] for r in expect_split(kind, p, 1, body_pts=p.pts, body_fixed=True)]
        under_of(icon); under_of(worn); return icon + worn
    card = {p.src: p for p in M19.CARD[kind]}
    rows = [r for p in M19.WORN[kind] for r in expect_split(kind, card[p.src], 0, body_pts=p.pts, layer_from=p)]
    under_of([r for r in rows if r["surfaces"] != 1])
    for r in rows:
        if "under" not in r: r["under"] = 0
    return rows

def count_expected():
    """조각 총수 — 모델에서 독립적으로 센다: 아이콘/몸 조각 + 투명 렌즈 워시(카드) + 재질 윤곽 테(채움+테)."""
    n = 0
    for k in M16.KINDS:
        def expand(pieces, body_only):
            c = 0
            for p in pieces:
                if is_wash(k, p): c += 2
                elif is_matline(k, p): c += 2
                else: c += 1
            return c
        n += expand(M19.CARD[k], False) + (expand(M19.WORN[k], True) if two_list(k) else 0)
    return n

def hexnorm(h): return None if h in (None, "-", "") else h.upper().lstrip("#")

def compare_rows(label, got, want):
    if len(got) != len(want):
        fail("%s: 조각 수 %d ≠ 모델 %d" % (label, len(got), len(want))); return
    for g, w in zip(got, want):
        where = "%s/%s" % (label, w["name"])
        if g["name"] != w["name"]: fail("%s: 이름 %s" % (where, g["name"])); continue
        for f in ("loop", "filled", "tone", "surfaces", "no_stroke", "under", "layer", "sway", "body_fixed"):
            if g[f] != w[f]: fail("%s: %s %r ≠ %r" % (where, f, g[f], w[f]))
        for f in ("mult", "in_r", "alpha", "line_alpha"):
            if abs(g[f] - w[f]) > TOL: fail("%s: %s %r ≠ %r" % (where, f, g[f], w[f]))
        if len(g["pts"]) != len(w["pts"]):
            fail("%s: 점 수 %d ≠ %d" % (where, len(g["pts"]), len(w["pts"]))); continue
        worst = max(max(abs(a[0] - b[0]), abs(a[1] - b[1])) for a, b in zip(g["pts"], w["pts"]))
        if worst > TOL: fail("%s: 좌표 최대 차 %.7f R > %.7f" % (where, worst, TOL))

def check_body(label, got, want, t):
    """되읽은 카드 좌표에 되읽은 변환을 걸면 r17 몸 좌표가 나오는가(bodyFixed 는 그대로)."""
    for g, w in zip(got, want):
        if w["body_pts"] is None: continue
        bp = g["pts"] if g["body_fixed"] else apply_transform(g["pts"], t)
        worst = max(max(abs(a[0] - b[0]), abs(a[1] - b[1])) for a, b in zip(bp, w["body_pts"]))
        if worst > 3e-5: fail("%s/%s: 몸 좌표(변환 후) r17 과 %.6f R 차" % (label, w["name"], worst))

# ============================================================================
# 1. C# 생성 파일 — 정규식 되읽기
# ============================================================================
ARR_RE = re.compile(r"private static readonly float\[\] (Handoff_\w+?_(\w+)) =\s*\{([^}]*)\};", re.S)
CALL_RE = re.compile(r'HandoffPiece\(sink, rig, xf, (Sort\w+), "(\w+)", (Handoff_\w+), loop: (true|false), filled: (true|false), '
                     r'tone: (\d+), surfaces: (\d+), strokeMult: ([0-9.]+)f, strokeInR: ([0-9.]+)f, noStroke: (true|false), '
                     r'alpha: ([0-9.]+)f, lineAlpha: ([0-9.]+)f, underBack: (\d+), layer: (\d+), '
                     r'swayStart: (-?\d+), swayCount: (\d+), bodyFixed: (true|false)\);')
CASE_RE = re.compile(r"^\s*case (\w+):", re.M)
XF_RE = re.compile(r"case (\w+): return new AccessoryWornTransform\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f, (-?[0-9.]+)f, (true|false)\);")

def hexarg(s): return None if s == "default" else "#" + s[6:12]

def read_cs():
    src = open(CS, encoding="utf-8").read()
    arrays = {}
    for m in ARR_RE.finditer(src):
        vals = [float(v.strip().rstrip("f")) for v in m.group(3).replace("\n", " ").split(",") if v.strip()]
        assert len(vals) % 2 == 0, m.group(1)
        arrays[m.group(1)] = [(vals[i], vals[i + 1]) for i in range(0, len(vals), 2)]
    by_case = {}; cur = None
    for line in src.split("\n"):
        cm = CASE_RE.match(line)
        if cm: cur = cm.group(1); continue
        pm = CALL_RE.search(line)
        if pm and cur is not None:
            g = pm.groups()
            by_case.setdefault(cur, []).append(dict(
                sort=g[0], name=g[1], arr=g[2], pts=arrays[g[2]], loop=g[3] == "true", filled=g[4] == "true", tone=int(g[5]),
                surfaces=int(g[6]), mult=float(g[7]), in_r=float(g[8]), no_stroke=g[9] == "true", alpha=float(g[10]),
                line_alpha=float(g[11]), under=int(g[12]), layer=int(g[13]),
                sway=(int(g[14]), int(g[15])), body_fixed=g[16] == "true"))
        elif "HandoffPiece(" in line and "private static void" not in line:
            fail("cs: HandoffPiece 호출을 정규식이 못 읽었다: " + line.strip()[:80])
    xf = {m.group(1): dict(group_alpha=float(m.group(2)), scale=float(m.group(3)), scale_y=float(m.group(4)),
                           offset_y=float(m.group(5)), mirror=m.group(6) == "true") for m in XF_RE.finditer(src)}
    return by_case, arrays, xf

# ============================================================================
# 2. NECK 에셋 — YAML + 항 스트림 독립 디코더
# ============================================================================
COLOR_RE = re.compile(r"\{r: ([0-9.]+), g: ([0-9.]+), b: ([0-9.]+), a: ([0-9.]+)\}")

def read_asset(path):
    lines = open(path, encoding="utf-8").read().split("\n")
    try: ws = next(i for i, l in enumerate(lines) if l == "  wornShapes:")
    except StopIteration: raise SystemExit(path + ": wornShapes 없음")
    shapes = []; cur = None; in_terms = False; item = {}
    for l in lines[ws + 1:]:
        if l.startswith("  - name: "):
            cur = {"name": l[len("  - name: "):].strip(), "terms": []}; shapes.append(cur); in_terms = False; continue
        if l.startswith("  ") and not l.startswith("    ") and not l.startswith("  - "):
            m = re.match(r"^  (\w+): (.+)$", l)
            if m: item[m.group(1)] = m.group(2).strip()
            cur = None; continue
        if cur is None: continue
        if l.startswith("    terms:"): in_terms = True; continue
        if in_terms and l.startswith("    - "):
            cur["terms"].append(float(l[6:].strip())); continue
        m = re.match(r"^    (\w+): (.+)$", l)
        if m: cur[m.group(1)] = m.group(2).strip(); in_terms = False; continue
    return shapes, item

def yaml_color_hex(s):
    m = COLOR_RE.match(s or "")
    if not m or float(m.group(4)) <= 0: return None
    return "#%02X%02X%02X" % tuple(int(round(float(m.group(i)) * 255)) for i in (1, 2, 3))

def decode_stream(t, head_r=1.0, head_cy=0.0):
    """상태 꺼짐(평일) 좌표와, 점마다 「상태 켜짐일 때만 더해지는 y 항」의 합을 따로 돌려준다."""
    basis_val = {0: head_r, 4: head_cy}
    i = 0; count = int(t[i]); i += 1; pts = []; gated = []
    for _ in range(count):
        sums = []; gate_y = 0.0
        for axis in range(2):
            nterm = int(t[i]); i += 1; acc = 0.0; any_ = False
            for _k in range(nterm):
                basis, gate, trig, nco = int(t[i]), int(t[i + 1]), int(t[i + 2]), int(t[i + 3]); i += 4
                if trig != 0: raise ValueError("삼각")
                if gate not in (0, 1): raise ValueError("게이트 %d" % gate)
                if basis not in basis_val: raise ValueError("기저 %d" % basis)
                v = basis_val[basis]
                for c in range(nco): v *= t[i + c]
                i += nco
                if gate == 1:
                    if axis != 1: raise ValueError("x 에 게이트 항")
                    gate_y += v; continue
                acc = acc + v if any_ else v; any_ = True
            sums.append(acc if any_ else 0.0)
        pts.append((sums[0], sums[1])); gated.append(gate_y)
    if i != len(t): raise ValueError("스트림 %d칸 중 %d칸만 썼다" % (len(t), i))
    return pts, gated

def asset_rows(kind):
    shapes, item = read_asset(os.path.join(ITEMS, NECK_ASSET[kind] + ".asset"))
    got = []
    for s in shapes:
        try: pts, gated = decode_stream(s["terms"])
        except ValueError as e: fail("%s/%s: 스트림 %s" % (kind, s["name"], e)); continue
        want_drop = -MONDAY_DROP_R if kind == "stripedtie" else 0.0
        if any(abs(g - want_drop) > TOL for g in gated):
            fail("%s/%s: 월요일 항 %r ≠ %r (원칙 1 — 줄무늬타이만 느슨해진다)" % (kind, s["name"], sorted(set(round(g, 5) for g in gated)), want_drop))
        if s.get("swingDegrees", "0") != "0": fail("%s/%s: 기울임이 있다" % (kind, s["name"]))
        for dead in ("bodyAlpha", "fixedFill", "fixedLine"):
            if dead in s: fail("%s/%s: 제거된 키 %s 가 에셋에 남아 있다(2026-09-05 I-22/I-23)" % (kind, s["name"], dead))
        got.append(dict(name=s["name"], pts=pts, loop=s["loop"] == "1", filled=s["filled"] == "1", tone=int(s["tone"]),
                        surfaces=int(s.get("surfaces", 0)), mult=float(s.get("strokeMult", 0)), in_r=float(s.get("strokeInR", 0)),
                        no_stroke=s.get("noStroke", "0") == "1", alpha=float(s.get("alpha", 0)),
                        line_alpha=float(s.get("lineAlpha", 0)), under=int(s.get("underBack", 0)), layer=int(s.get("layer", 0)),
                        sway=(int(s.get("swayStart", -1)), int(s.get("swayCount", 0))), body_fixed=s.get("bodyFixed", "0") == "1"))
    xf = dict(group_alpha=float(item.get("wornGroupAlpha", 0)), scale=float(item.get("wornScale", 0)),
              scale_y=float(item.get("wornScaleY", 0)), offset_y=float(item.get("wornOffsetYInR", 0)), mirror=item.get("wornMirrorX", "0") == "1")
    return got, xf

# ============================================================================
# 3. r17_coords.txt (설계자 산출물, 4자리) — 몸 표면 좌표
# ============================================================================
def read_coords_txt():
    """r19_coords.txt — 아이템 절 안의 '-- BODY' 절만(카드 절은 R16 아이콘 그대로라 골든이 잠근다)."""
    out = {}; kind = None; section = "BODY"
    for l in open(COORDS_TXT, encoding="utf-8"):
        l = l.rstrip("\n")
        if l.startswith("=" * 10): kind = None; section = "BODY"; continue
        if kind is None and re.match(r"^\w+\s+\S+ / \w+", l): kind = l.split()[0]; continue
        if kind and l.startswith("-- "): section = "BODY" if l.startswith("-- BODY") else "CARD"; continue
        if kind and section == "BODY" and l.startswith("  ") and not l.startswith("      "):
            out.setdefault(kind, []).append([l.split()[0], None]); continue
        if kind and section == "BODY" and l.startswith("      "):
            out[kind][-1][1] = [tuple(map(float, q.split(","))) for q in re.findall(r"\(([-0-9.]+,[-0-9.]+)\)", l)]
    return out

def xf_equal(a, b):
    return all(abs(a[k] - b[k]) <= TOL for k in ("group_alpha", "scale", "scale_y", "offset_y")) and a["mirror"] == b["mirror"]

# ============================================================================
def main():
    by_case, arrays, xf = read_cs()
    total = 0
    for kind, const in ITEM_CONST.items():
        got = by_case.get(const, []); want = expected_rows(kind); t = expected_transform(kind)
        compare_rows("cs:" + kind, got, want); total += len(got)
        if const not in xf or not xf_equal(xf[const], t): fail("cs:%s WornTransform %r ≠ %r" % (kind, xf.get(const), t))
        if const in xf and len(got) == len(want): check_body("cs:" + kind, got, want, xf[const])
    referenced = {r["arr"] for rows in by_case.values() for r in rows}
    unused = set(arrays) - referenced
    if unused: fail("cs: 어느 HandoffPiece 도 쓰지 않는 배열 %s" % sorted(unused))
    # -- R17c 단일 출처: 몸 외알안경 알(CB0) 중심 x = 반대쪽 눈(ExposedEye) 중심 x 의 부호 반전 — 모델 MONOCLE_LENS_CX_R = −EYE_X
    mono = [r for r in expected_rows("monocle") if r["surfaces"] == 1]
    def cx(r): xs = [x for x, _ in r["pts"]]; return (min(xs) + max(xs)) / 2
    lens = [r for r in mono if r["name"] == "Piece_CB0"]; eye = [r for r in mono if r["tone"] == FILL_T["EYE"]]
    if len(lens) != 1 or len(eye) != 1: fail("monocle: 몸 알/눈 조각이 하나씩이 아니다 %d/%d" % (len(lens), len(eye)))
    else:
        eye_x = getattr(M19, "EYE_X_R20", getattr(M19, "EYE_X", M17.EYE_X))   # R20 이 눈 자리를 ±0.56 으로 옮겼다 — r19 상수가 정본
        if abs(cx(lens[0]) + cx(eye[0])) > 1e-4 or abs(cx(eye[0]) - eye_x) > 1e-4:
            fail("monocle: 알 중심 x %.4f 가 눈 중심 x %.4f(모델 EYE_X %.4f)의 부호 반전이 아니다" % (cx(lens[0]), cx(eye[0]), eye_x))
    for kind in NECK_ASSET:
        got, axf = asset_rows(kind); want = expected_rows(kind); t = expected_transform(kind)
        compare_rows("asset:" + kind, got, want); total += len(got)
        if not xf_equal(axf, t): fail("asset:%s 몸 파라미터 %r ≠ %r" % (kind, axf, t))
        if len(got) == len(want): check_body("asset:" + kind, got, want, axf)
    # -- 총수 교정: 아이콘 91 + 무대 6 + 모자 바탕(B/CB/RB) + 반대쪽 눈(보류면 0)
    want_total = count_expected()
    if total != want_total: fail("조각 총수 %d ≠ 모델 %d" % (total, want_total))
    # -- r17_coords.txt(4자리)와도 맞는가 — 몸 표면(보류 아이템은 r16 좌표라 대조에서 뺀다)
    txt = read_coords_txt()
    for kind in M16.KINDS:
        if kind in PENDING_R17: continue
        body = [r for r in expected_rows(kind) if r["surfaces"] in (0, 1) and not r["extra"]]
        entries = txt.get(kind, [])
        if len(entries) != len(body): fail("coords.txt %s: 조각 수 %d ≠ %d" % (kind, len(entries), len(body))); continue
        t = expected_transform(kind)
        for (name, pts), row in zip(entries, body):
            if name != row["r19name"]: fail("coords.txt %s: %s ≠ %s(%s)" % (kind, name, row["r19name"], row["name"]))
            bp = row["body_pts"] if row["body_pts"] is not None else apply_transform(row["pts"], t)
            if len(pts) != len(bp): fail("coords.txt %s/%s: 점 수 %d ≠ %d" % (kind, name, len(pts), len(bp))); continue
            worst = max(max(abs(a[0] - b[0]), abs(a[1] - b[1])) for a, b in zip(pts, bp))
            if worst > 0.5e-4 + 1e-9: fail("coords.txt %s/%s: 4자리 표와 %.6f R 차" % (kind, name, worst))
    # -- R20 N-1: v1 목 아이템(펜던트·반다나)의 착용선 dy — 스트림은 v1 그대로(strokeInR 0)여야 하고 아이템 오프셋만 모델과 같다
    for kind, (asset, dy) in (("pendant", ("equip_neck_pendant", M20.ga_pendant()[1])), ("bandana", ("equip_neck_bandana", M20.ga_bandana()[1]))):
        shapes, item = read_asset(os.path.join(ITEMS, asset + ".asset"))
        if not shapes: fail("v1 %s: wornShapes 가 비었다" % kind)
        if any(float(sh.get("strokeInR", 0)) > 0 for sh in shapes): fail("v1 %s: 조형이 인계본으로 바뀌었다 — 이 dy 전사는 v1 스트림 전제다" % kind)
        got = float(item.get("wornOffsetYInR", 0))
        if abs(got - dy) > TOL: fail("v1 %s: wornOffsetYInR %.5f ≠ 모델 dy %.5f" % (kind, got, dy))
        if dy >= 0: fail("v1 %s: 모델 dy %.5f 가 아래(음수)가 아니다 — N-1 의 뜻이 바뀌었다" % (kind, dy))
    # -- provenance 스탬프(E-0): C# 헤더와 골든 헤더의 sha 가 지금 정본 파일과 같은가(낡은 생성물 = 즉시 빨강)
    import hashlib
    def sha16(path):
        with open(path, "rb") as f: return hashlib.sha256(f.read()).hexdigest()[:16]
    sources = {"r16_model.py": os.path.join(VERIFY, "r16_model.py"), "r17_model.py": os.path.join(VERIFY, "r17_model.py"),
               "r19_model.py": os.path.join(VERIFY, "r19_model.py"), "r20_model.py": os.path.join(VERIFY, "r20_model.py"),
               "palette_model.py": os.path.join(VERIFY, "palette_model.py"),
               "ItemIcon.dc.html": handoff.source_path()}
    for label, path in (("cs", CS), ("golden", GOLDEN)):
        head = "\n".join(open(path, encoding="utf-8").read().split("\n")[:12])
        stamped = dict(re.findall(r"(\S+\.(?:py|html)) sha256\[:16\]=([0-9a-f]{16})", head))
        for name, spath in sources.items():
            if name not in stamped: fail("스탬프(%s): %s 가 없다" % (label, name))
            elif stamped[name] != sha16(spath): fail("스탬프(%s): %s 가 낡았다(%s ≠ 지금 %s) — 재생성" % (label, name, stamped[name], sha16(spath)))
    # -- 골든
    golden = open(GOLDEN, encoding="utf-8").read().split("\n")
    gp = [l for l in golden if l.startswith("PIECE\t")]
    if len(gp) != want_total: fail("골든 PIECE 줄 %d ≠ %d" % (len(gp), want_total))
    # -- 계약 상수
    src = open(CONTRACT, encoding="utf-8").read()
    for name, want in (("Primary", 0), ("Accent", 1), ("Shade", 2), ("Highlight", 3), ("HeadInk", 4), ("InkContrast", 5), ("Glass", TONE_GLASS)):
        m = re.search(r"public const byte %s = (\d+);" % name, src)
        if not m or int(m.group(1)) != want: fail("계약: AccessoryTone.%s ≠ %d" % (name, want))
    if re.search(r"public const byte Fixed = ", src): fail("계약: 고정색(Fixed)이 되살아났다 — 2026-09-05 제거(I-23)")
    for name, want in (("Body", 1), ("Card", 2), ("Portrait", 4)):
        m = re.search(r"\b%s = (\d+),?" % name, src)
        if not m or int(m.group(1)) != want: fail("계약: AccessorySurface.%s ≠ %d" % (name, want))
    for name, want in (("Slot", 0), ("Back", 1), ("BodyFront", 2)):
        m = re.search(r"\b%s = (\d+),?" % name, src)
        if not m or int(m.group(1)) != want: fail("계약: AccessoryPieceLayer.%s ≠ %d" % (name, want))
    m = re.search(r"GradientMeanAlpha = ([0-9.]+)f", src)
    if not m or abs(float(m.group(1)) - 0.21) > 1e-9: fail("계약: GradientMeanAlpha ≠ 0.21")
    m = re.search(r"HighlightWhiteAlpha = ([0-9.]+)f", src)
    if not m or abs(float(m.group(1)) - M16.HI_ALPHA) > 1e-9: fail("계약: HighlightWhiteAlpha ≠ 모델 HI_ALPHA")
    # -- 카드 프레임
    ci = open(CARD_ICON, encoding="utf-8").read()
    for name, want in (("StageViewBoxWidth", handoff.STAGE_VB_W), ("StageHeadRadius", handoff.STAGE_HEAD_R),
                       ("StageHeadCenterY", handoff.STAGE_HEAD_CY), ("StageRenderWidth", handoff.STAGE_RENDER_W),
                       ("StageTopPx", handoff.STAGE_TOP_PX), ("IconViewBox", 64.0), ("IconStroke", handoff.STROKE)):
        m = re.search(r"\b%s = ([0-9.]+)f" % name, ci)
        if not m or abs(float(m.group(1)) - want) > 1e-9: fail("프레임: AccessoryCardIcon.%s ≠ %r" % (name, want))
    for key, (box, top) in handoff.SLOT_BOX.items():
        m = re.search(r"case EquipmentSlot\.\w+: box = ([0-9.]+)f; top = ([0-9.]+)f; // %s" % key, ci)
        if not m or abs(float(m.group(1)) - box) > 1e-9 or abs(float(m.group(2)) - top) > 1e-9:
            fail("프레임: 슬롯 박스 %s ≠ (%g, %g)" % (key, box, top))
    # -- 슬롯 기본 획: 조각 명목 = 슬롯 획 × 배수 (아이콘 조각)
    for kind in M16.KINDS:
        base = M16.SLOT_STROKE_R[M16.ISLOT[kind]]
        for r in expected_rows(kind):
            if r["surfaces"] == 1: continue
            if abs(r["in_r"] - base * r["mult"]) > 1e-9: fail("획: %s/%s 명목 %.5f ≠ 슬롯 %.5f × %.2f" % (kind, r["name"], r["in_r"], base, r["mult"]))

    # -- 양성 대조
    before = len(fails)
    rows = expected_rows("clothhat"); shaken = [dict(r) for r in rows]
    shaken[0]["pts"] = list(shaken[0]["pts"]); x, y = shaken[0]["pts"][3]; shaken[0]["pts"][3] = (x + 2 * 10 ** -DECIMALS, y)
    compare_rows("control", shaken, rows)
    if len(fails) == before:
        print("★ 양성 대조 실패 — 좌표를 2e-%d 흔들었는데 비교기가 침묵했다. 이 실행의 0건은 무효다." % DECIMALS); sys.exit(4)
    control = fails.pop(); assert control.startswith("control/"), control
    print("양성 대조 통과 — 좌표 2e-%d 흔든 사본에서 비교기가 실제로 빨개졌다(위 CONTROL 줄)." % DECIMALS)
    print("조각 %d · 실패 %d · 보류 %s" % (total, len(fails), sorted(PENDING_R17)))
    sys.exit(1 if fails else 0)

if __name__ == "__main__":
    main()
