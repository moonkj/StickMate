"""R26 구현 검산 — 프로덕션 .cs 를 <직접 파싱>해서 다시 잰다.

★ 왜 파싱인가: design-equipment 의 거울(fanglyph.py)은 사람이 좌표를 옮겨 적은 사본이라
  «내가 구현한 것»이 아니라 «내가 구현했다고 믿는 것»을 잰다. 같은 함정에 같이 빠진다.
  이 스크립트는 GearRadialMenuWidget.cs 의 상수/배열/호출을 읽어 좌표를 <유도>한다.

측정: FG-1(광학 필드) FG-2(획 사다리) FG-3(골) FG-4(조각·가려짐) FG-6(실루엣) FG-8(광학 크기)
"""
import math, re, sys
import numpy as np
from scipy import ndimage

SRC = sys.argv[1] if len(sys.argv) > 1 else \
    "/Users/kjmoon/App/StickMate/Assets/_Project/Scripts/Interaction/GearRadialMenuWidget.cs"
CHROME = "/Users/kjmoon/App/StickMate/Assets/_Project/Scripts/Interaction/UiChrome.cs"
text = open(SRC, encoding="utf-8").read()
chrome = open(CHROME, encoding="utf-8").read()

# ─────────────── 0. UiChrome 규약을 소스에서 확인(가정을 박제하지 않는다) ───────────────
m = re.search(r"private const float EdgeFeather = ([0-9.]+)f;", chrome)
assert m, "EdgeFeather 를 못 읽었다"
EDGE_FEATHER = float(m.group(1))
# AddPolyline 이 length+thickness 보정을 여전히 하는가(하면 캡 중심 = 꼭짓점).
assert "AddStroke(parent, name, length + thickness, thickness," in chrome, \
    "AddPolyline 의 length+thickness 보정이 사라졌다 — 아래 좌표 모형이 무효다"
# AddStroke 의 보이는 길이가 length 인가(캡 중심 ±(L/2 - t/2)).
assert "rt.sizeDelta = new Vector2(length + EdgeFeather * 2f, boxHeight);" in chrome
# AddCircle 의 ringThickness 생략 = 채운 원반인가.
assert "? CircleSprite(ringThickness / diameter, coreFraction)" in chrome
assert ": CircleSprite(0.5f, coreFraction);" in chrome

# ─────────────── 1. const float 수집 ───────────────
class Vec(tuple):
    def __new__(cls, x, y): return super().__new__(cls, (float(x), float(y)))
    def __add__(self, o): return Vec(self[0] + o[0], self[1] + o[1])
    def __neg__(self): return Vec(-self[0], -self[1])

def Polar(deg, r):
    a = math.radians(deg)
    return Vec(math.cos(a) * r, math.sin(a) * r)

NS = {"Polar": Polar, "Vector2": type("V", (), {"zero": Vec(0, 0)}),
      "Vec": Vec, "Mathf": type("M", (), {"Max": max, "Lerp": lambda a, b, t: a + (b - a) * t,
                              "Cos": math.cos, "Sin": math.sin,
                              "Deg2Rad": math.pi / 180.0})}

def norm(e):
    e = re.sub(r"(?<=[0-9.])f\b", "", e)                 # 1.5f -> 1.5
    e = e.replace("new Vector2(", "Vec(")
    e = e.replace("Vector2.zero", "Vec(0,0)")
    return e.strip()

for name, expr in re.findall(r"private const float (\w+)\s*=\s*([^;]+);", text):
    NS[name] = eval(norm(expr), {"__builtins__": {}}, NS)
for name, expr in re.findall(r"private const int (\w+)\s*=\s*([^;]+);", text):
    NS[name] = eval(norm(expr), {"__builtins__": {}}, NS)
for name, expr in re.findall(r"private static readonly Vector2 (\w+)\s*=\s*([^;]+);", text):
    NS[name] = eval(norm(expr), {"__builtins__": {}}, NS)

# Vector2[] 배열 (중괄호 블록)
for name, body in re.findall(
        r"private static readonly Vector2\[\] (\w+)\s*=\s*\{(.*?)\};", text, re.S):
    body = re.sub(r"//.*", "", body)
    NS[name] = [eval(norm(x), {"__builtins__": {}}, NS)
                for x in re.split(r",(?![^()]*\))", body) if x.strip()]

# BuildArcPath 로 만든 배열
for name, args in re.findall(
        r"private static readonly Vector2\[\] (\w+) = BuildArcPath\((.*?)\);", text, re.S):
    a = [norm(x) for x in re.split(r",(?![^()]*\))", re.sub(r"\s+", " ", args))]
    c = eval(a[0], {"__builtins__": {}}, NS); r = eval(a[1], {"__builtins__": {}}, NS)
    s = eval(a[2], {"__builtins__": {}}, NS); e = eval(a[3], {"__builtins__": {}}, NS)
    n = int(eval(a[4], {"__builtins__": {}}, NS))
    NS[name] = [c + Polar(s + (e - s) * i / n, r) for i in range(n + 1)]

W = NS["SymbolStroke"]; BOX = NS["SymbolBoxPoints"]
# ★ 옛 트리(HEAD)에는 사다리 상수가 없다 — 그때도 규칙은 «W의 0.75/1.00/1.50»이므로 W에서 유도한다.
LADDER = {"g1": NS.get("SymbolStrokeDetail", 0.75 * W), "g0": W,
          "g2": NS.get("SymbolStrokeHeavy", 1.5 * W)}
NS.setdefault("SymbolFieldRadiusPoints", 14.0)

# ─────────────── 2. 빌더 본문에서 조각 추출 ───────────────
def body_of(sig):
    i = text.index(sig); d = 0; j = text.index("{", i)
    k = j
    while True:
        if text[k] == "{": d += 1
        elif text[k] == "}":
            d -= 1
            if d == 0: return text[j:k + 1]
        k += 1

def args_of(call):
    d = 0; out = []; cur = ""
    for ch in call:
        if ch in "(": d += 1
        if ch in ")": d -= 1
        if ch == "," and d == 0: out.append(cur); cur = ""; continue
        cur += ch
    out.append(cur)
    return [x.strip() for x in out if x.strip()]

def calls(body, fn):
    out = []
    for m in re.finditer(re.escape(fn) + r"\(", body):
        i = m.end(); d = 1; j = i
        while d:
            if body[j] == "(": d += 1
            elif body[j] == ")": d -= 1
            j += 1
        out.append(args_of(body[i:j - 1]))
    return out

INACTIVE = set()   # SetActive(false) 되는 조각 이름
for m in re.finditer(r'AddCircle\(p, "(\w+)"', text):
    pass
# 비활성 조각은 필드 변수로 잡히므로 이름으로 잡는다(소스에 근거가 있어야 한다).
for nm in ("RingFill", "QuitCountdown"):
    assert f'"{nm}"' in text, f"{nm} 이 사라졌다"
    INACTIVE.add(nm)
assert text.count("view.RingFill.gameObject.SetActive(false);") == 2, \
    "RingFill/QuitCountdown 이 기본 활성으로 바뀌었다 — 이 검산의 전제가 깨졌다"


class Piece:
    def __init__(self, name, kind, t, **kw):
        self.name, self.kind, self.t, self.kw = name, kind, t, kw

def parse(sig):
    b = body_of(sig); ps = []
    for nm2, ex2 in re.findall(r"\n\s+(?:float|var) (\w+) = ([^;]+);", b):
        try: NS[nm2] = eval(norm(ex2), {"__builtins__": {}}, NS)
        except Exception: pass
    order = []
    for m in re.finditer(r"UiChrome\.(AddPolyline|AddStroke|AddCircle)\(|(?<![\w.])(AddSmallBox)\(", b):
        order.append((m.start(), m.group(1) or m.group(2)))
    for pos, fn in order:
        i = b.index("(", pos) + 1; d = 1; j = i
        while d:
            if b[j] == "(": d += 1
            elif b[j] == ")": d -= 1
            j += 1
        a = args_of(b[i:j - 1])
        nm = a[1].strip('"')
        if nm in INACTIVE: continue
        ev = lambda s: eval(norm(s), {"__builtins__": {}}, NS)
        if fn == "AddPolyline":
            ps.append(Piece(nm, "poly", ev(a[3]), points=NS[a[2]]))
        elif fn == "AddStroke":
            ps.append(Piece(nm, "capsule", ev(a[3]), length=ev(a[2]),
                            angle=ev(a[4]), center=ev(a[5])))
        elif fn == "AddSmallBox":
            # 옛 헬퍼: 4.5pt 정사각 · RoundedOutline(2, 1) = 획 1pt 사각 윤곽.
            mm = re.search(r"rt\.sizeDelta = new Vector2\(([0-9.]+)f, [0-9.]+f\);\s*"
                           r"rt\.anchoredPosition = center;\s*.*?RoundedOutline\((\d+), (\d+)\)", text, re.S)
            assert mm, "옛 AddSmallBox 의 치수를 소스에서 못 읽었다"
            ps.append(Piece(nm, "rect", float(mm.group(3)), side=float(mm.group(1)), center=ev(a[2])))
        else:  # AddCircle(parent, name, diameter, color[, ringThickness[, center]])
            dia = ev(a[2])
            ring = ev(a[4]) if len(a) > 4 else 0.0
            ctr = ev(a[5]) if len(a) > 5 else Vec(0, 0)
            # 옛 트리는 중심을 다음 줄의 anchoredPosition 으로 줬다.
            mm = re.search(re.escape(nm).replace("Icon", "Icon") +
                           r'"[^;]*;\s*\w+\.rectTransform\.anchoredPosition = (new Vector2\([^;]*\));', b)
            if mm and ctr == Vec(0, 0): ctr = ev(mm.group(1))
            if ring > 0:
                ps.append(Piece(nm, "ring", ring, diameter=dia, center=ctr))
            else:
                ps.append(Piece(nm, "disc", 0.0, diameter=dia, center=ctr))
    return ps

GLYPHS = {
    "① 집중(스톱워치)": parse("private Image[] BuildStopwatchSymbol"),
    "② 캐릭터(스틱맨)": parse("private Image[] BuildStickmanSymbol"),
    "③ 할일(체크리스트)": parse("private Image[] BuildChecklistSymbol"),
    "④ 행동명령(확성기)": parse("private static Image[] BuildMegaphoneSymbol"),
    "⑤ 종료(전원)": parse("private Image[] BuildPowerSymbol"),
}
# ⑤ 링의 틈 — fillAmount/rotation 배선에서 유도한다.
pw = body_of("private Image[] BuildPowerSymbol")
assert "ring.fillAmount = 1f - PowerGapDegrees / 360f;" in pw
assert "Quaternion.Euler(0f, 0f, -PowerGapDegrees * 0.5f)" in pw
for p in GLYPHS["⑤ 종료(전원)"]:
    if p.name == "PowerRing":
        p.kw["gap_deg"] = NS["PowerGapDegrees"]

# 체크리스트의 Accent 고정 조각(색 보간 대상 밖) — 조형 판정에는 포함, 색 규칙만 따로 센다.
ACCENT = {"③ 할일(체크리스트)": {"Check"}}

# ─────────────── 3. 래스터 ───────────────
SS = 32; WIN = 36.0; N = int(WIN * SS)
_ax = (np.arange(N) + 0.5) / SS - WIN / 2
GX, GY = np.meshgrid(_ax, -_ax)

def segd(ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    wx, wy = GX - ax, GY - ay
    L2 = vx * vx + vy * vy
    t = 0.0 if L2 == 0 else np.clip((wx * vx + wy * vy) / L2, 0, 1)
    return np.hypot(wx - t * vx, wy - t * vy)

def mask(p):
    if p.kind == "poly":
        m = np.zeros_like(GX, bool)
        for i in range(1, len(p.kw["points"])):
            a, b = p.kw["points"][i - 1], p.kw["points"][i]
            m |= segd(a[0], a[1], b[0], b[1]) <= p.t / 2
        return m
    if p.kind == "capsule":
        c, L, ang = p.kw["center"], p.kw["length"], math.radians(p.kw["angle"])
        h = max(0.0, L / 2 - p.t / 2)
        return segd(c[0] - math.cos(ang) * h, c[1] - math.sin(ang) * h,
                    c[0] + math.cos(ang) * h, c[1] + math.sin(ang) * h) <= p.t / 2
    if p.kind == "ring":
        c, d = p.kw["center"], p.kw["diameter"]
        r = np.hypot(GX - c[0], GY - c[1])
        m = np.abs(r - (d - p.t) / 2) <= p.t / 2
        g = p.kw.get("gap_deg", 0.0)
        if g > 0:
            th = np.degrees(np.arctan2(GY - c[1], GX - c[0])) % 360
            lo, hi = (90 - g / 2) % 360, (90 + g / 2) % 360
            ins = (th >= lo) & (th <= hi) if lo < hi else (th >= lo) | (th <= hi)
            m &= ~ins
        return m
    if p.kind == "disc":
        c, d = p.kw["center"], p.kw["diameter"]
        return np.hypot(GX - c[0], GY - c[1]) <= d / 2
    if p.kind == "rect":
        c, s, t = p.kw["center"], p.kw["side"], p.t
        outer = (np.abs(GX - c[0]) <= s / 2) & (np.abs(GY - c[1]) <= s / 2)
        inner = (np.abs(GX - c[0]) <= s / 2 - t) & (np.abs(GY - c[1]) <= s / 2 - t)
        return outer & ~inner
    raise ValueError(p.kind)

def seg_images(p):
    """실제로 만들어지는 Image 단위(꺾은선은 선분마다 1개)."""
    if p.kind != "poly": return [(p.name, mask(p))]
    out = []
    pts = p.kw["points"]
    for i in range(1, len(pts)):
        a, b = pts[i - 1], pts[i]
        out.append((f"{p.name}[{i-1}]", segd(a[0], a[1], b[0], b[1]) <= p.t / 2))
    return out

GAP_MIN = 1.5 * W
FIELD_R, FIELD_R_HARD = NS["SymbolFieldRadiusPoints"], 15.0

print(f"자: W={W}  g1={LADDER['g1']}  g2={LADDER['g2']}  상자={BOX}  "
      f"EdgeFeather={EDGE_FEATHER}  필드 r≤{FIELD_R}(단일돌출 {FIELD_R_HARD})")
print()

rows = []
unions = {}
for gname, ps in GLYPHS.items():
    ms = [mask(p) for p in ps]
    union = np.zeros_like(ms[0]);  [union.__ior__(m) for m in ms]
    unions[gname] = union
    R = np.hypot(GX, GY)
    rmax = R[union].max()
    ink = union.sum() / (BOX * SS) ** 2 * 100
    ys, xs = np.where(union)
    bw = _ax[xs.max()] - _ax[xs.min()]; bh = _ax[ys.max()] - _ax[ys.min()]
    diag = math.hypot(bw, bh)

    lab, nblob = ndimage.label(union, structure=np.ones((3, 3)))
    worst = (None, 1e9)
    for i in range(1, nblob + 1):
        d = ndimage.distance_transform_edt(lab != i) / SS
        for j in range(i + 1, nblob + 1):
            g = d[lab == j].min()
            if g < worst[1]: worst = ((i, j), g)

    imgs = [x for p in ps for x in seg_images(p)]
    hidden = []
    for k, (nm, m) in enumerate(imgs):
        oth = np.zeros_like(m)
        for l, (_, m2) in enumerate(imgs):
            if l != k: oth |= m2
        if m.any() and not (m & ~oth).any(): hidden.append(nm)

    ths = sorted({round(p.t, 4) for p in ps if p.t > 0})
    grades = []
    for t in ths:
        g = [k for k, v in LADDER.items() if abs(v - t) < 1e-6]
        grades.append(f"{t:g}({g[0] if g else '★사다리밖'})")
    g2n = sum(1 for p in ps if abs(p.t - LADDER["g2"]) < 1e-6)

    rows.append(dict(name=gname, parts=len(ps), imgs=len(imgs), th=grades, g2=g2n,
                     ink=ink, rmax=rmax, bw=bw, bh=bh, diag=diag,
                     blobs=nblob, gap=worst[1] if worst[0] else float("inf"),
                     hidden=hidden))

hdr = f"{'칸':<18}{'조각':>4}{'Image':>6}{'덩어리':>6}{'최소간극':>9}{'r_max':>8}{'잉크%':>7}{'대각':>7}  획 등급"
print(hdr); print("-" * 108)
for r in rows:
    gp = "—" if r["blobs"] < 2 else f"{r['gap']:.2f}"
    print(f"{r['name']:<16}{r['parts']:>4}{r['imgs']:>7}{r['blobs']:>7}{gp:>10}"
          f"{r['rmax']:>8.2f}{r['ink']:>7.1f}{r['diag']:>7.2f}  {' '.join(r['th'])}"
          + (f"   ★가려짐 {r['hidden']}" if r["hidden"] else ""))
print()

# ─────────────── 4. 게이트 판정 ───────────────
fails = []
for r in rows:
    n = r["name"]
    if r["rmax"] > FIELD_R + 1e-6: fails.append(f"FG-1 {n}: r_max {r['rmax']:.2f} > {FIELD_R}")
    if any("사다리밖" in t for t in r["th"]): fails.append(f"FG-2 {n}: 획 {r['th']}")
    if r["g2"] > 1: fails.append(f"FG-2 {n}: g2 조각 {r['g2']}개(최대 1)")
    if r["blobs"] >= 2 and r["gap"] < GAP_MIN - 1.5 / SS:
        fails.append(f"FG-3 {n}: 덩어리 간극 {r['gap']:.2f} < {GAP_MIN}")
    if not (2 <= r["parts"] <= 6): fails.append(f"FG-4 {n}: 조각 {r['parts']}개")
    if r["hidden"]: fails.append(f"FG-4 {n}: 100% 가려진 Image {r['hidden']}")
    if not (12 <= r["ink"] <= 26): fails.append(f"FG-8 {n}: 잉크 {r['ink']:.1f}%")
    if not (24 <= r["diag"] <= 32): fails.append(f"FG-8 {n}: 잉크 대각 {r['diag']:.2f}pt")

names = list(GLYPHS)
print("FG-6 실루엣 (IoU ≤ 0.35 · 고유 잉크 ≥ 0.45)")
print("        " + "".join(f"{k[:2]:>9}" for k in names))
uniq = {}
for a in names:
    line = f"{a[:2]:<8}"
    for b in names:
        ua, ub = unions[a], unions[b]
        iou = (ua & ub).sum() / (ua | ub).sum()
        line += f"{iou:>9.3f}"
    print(line)
# ★ FG-6 은 <쌍별>이다(r26_gate.py::silhouette 와 같은 식):
#   IoU = |A∩B| / |A∪B| · 고유 = min(|A\B|/|A|, |B\A|/|B|)
worst_iou = (None, 0.0); worst_uni = (None, 1.0)
for i, a in enumerate(names):
    for b in names[i + 1:]:
        ua, ub = unions[a], unions[b]
        iou = (ua & ub).sum() / (ua | ub).sum()
        uni = min((ua & ~ub).sum() / ua.sum(), (ub & ~ua).sum() / ub.sum())
        if iou > worst_iou[1]: worst_iou = (f"{a[0]}↔{b[0]}", iou)
        if uni < worst_uni[1]: worst_uni = (f"{a[0]}↔{b[0]}", uni)
        if iou > 0.35: fails.append(f"FG-6 {a}↔{b}: IoU {iou:.3f} > 0.35")
        if uni < 0.45: fails.append(f"FG-6 {a}↔{b}: 쌍별 고유 잉크 {uni:.3f} < 0.45")
print(f"최악 IoU  {worst_iou[0]} {worst_iou[1]:.3f}   최소 쌍별 고유 잉크  "
      f"{worst_uni[0]} {worst_uni[1]:.3f}")

print()
for g, s in ACCENT.items():
    got = {p.name for p in GLYPHS[g] if p.name in s}
    print(f"FG-7 {g}: Accent 고정 조각 {len(got)}개 {sorted(got)}")
    if len(got) != 1: fails.append(f"FG-7 {g}: Accent 조각 {len(got)}개")

print()
if fails:
    print("★ 미달 " + str(len(fails)) + "건"); [print("  - " + f) for f in fails]
    sys.exit(1)
print("게이트 전항 통과")
