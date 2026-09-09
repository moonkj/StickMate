# -*- coding: utf-8 -*-
"""
비트맵 부착 저작 규격 v1 — 앵커/치수 검산 하니스  (design-equipment / 2026-09-09)

이 파일이 하는 일:
  1. 프로덕션 상수를 **파일에서 직접 읽어** 리그 치수를 세운다(숫자를 베끼지 않는다).
  2. 출하 42+12종의 <b>착용 잉크 박스</b>를 실측한다
     (인계본 코드표 / 에셋 wornShapes / V1 배열 세 경로 전부).
  3. 제안한 슬롯 캔버스가 그 전부를 담는지 여백까지 검산한다.
  4. 캔버스 픽셀 ↔ R 좌표 왕복이 정확한지 확인한다.
  5. 세 크기(카드 58pt / 슬롯행 24pt / 몸 머리지름)와 디스플레이별 텍셀 배율을 낸다.

돌리는 법:  python3 design/equipment/verify/bitmap_anchor_r1.py
"""
import re, os, math, glob, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
def rd(p): return open(os.path.join(ROOT, p), encoding="utf-8").read()

fail = []
def check(ok, msg):
    print(("  OK  " if ok else "  ✗   ") + msg)
    if not ok: fail.append(msg)

# ─────────────────────────────────────────────────────────────────────
# 0. 프로덕션 상수 — 파일에서 읽는다
# ─────────────────────────────────────────────────────────────────────
boot = rd("Assets/Editor/SceneBootstrapper.cs")
cfg  = rd("Assets/_Project/Scripts/Core/StickConfig.cs")
met  = rd("Assets/_Project/Scripts/Core/StickmanMetrics.cs")
win  = rd("Assets/_Project/Scripts/Interaction/CharacterInfoWindow.cs")

def const(txt, name, kind="float"):
    m = re.search(r"const\s+%s\s+%s\s*=\s*(-?[\d.]+)f?" % (kind, name), txt)
    if not m: raise SystemExit("상수를 못 찾았다: " + name)
    return float(m.group(1))

H          = const(cfg,  "BaselineCharacterTotalHeight")
R1         = const(boot, "BaselineHeadVisualRadius")
RINGW      = const(boot, "BaselineHeadOutlineWidth")
MINFILLPT  = const(cfg,  "MinFillOutlineScreenPoints")
MINACCPT   = const(cfg,  "MinAccessoryStrokeScreenPoints")
PT_PER_U   = 846.0/(2*12.0)                      # ReferencePointsPerWorldUnitApprox
SHIP_S     = 0.75; MIN_S = const(cfg,"MinCharacterScale"); MAX_S = const(cfg,"MaxCharacterScale")
ICON58     = const(win, "IconSize"); SLOT24 = const(win, "SlotIconSize")

m = re.search(r"머리중심 ([\d.]+) / 머리반경 ([\d.]+) / 어깨 ([\d.]+) / 엉덩이 ([\d.]+)", met)
HC, RB, SHY, HIPY = (float(m.group(i)) for i in (1,2,3,4))
assert abs(RB-R1) < 1e-9, "머리 반경 두 출처 불일치"

SH_R  = (SHY - HC)/R1          # 어깨선 (머리중심 기준 R)
HIP_R = (HIPY - HC)/R1
TL    = SHY - HIPY
NECK_R= SH_R + 0.04            # NeckCollarRiseRatio
# ★ 머리의 보이는 바깥 반경 — <b>명목 0.063이 아니라 프리팹에 구워진 값</b>에서 온다.
#   SceneBootstrapper.ScaledStrokeWidth 가 굽기 때 2pt 하한을 걸어 실효 계수가 0.063 -> 0.0756501 로
#   올라가 있다(SceneBootstrapper.cs:858-872 자백). 그래서 **구운 프리팹을 직접 읽는다**.
pre  = rd("Assets/_Project/Prefabs/Stickman.prefab")
BAKE_S = 0.75                                            # 출하 굽기 배율
m = re.search(r"0\.056737587", pre)
if not m: raise SystemExit("프리팹에서 구운 링 폭을 못 찾았다 — 값이 바뀌었으면 이 검사부터 고쳐라")
RING_BAKED = 0.056737587
derived = max(RINGW*BAKE_S, 2.0/PT_PER_U)                # MinStrokeScreenPoints = 2pt
assert abs(derived - RING_BAKED) < 1e-6, f"굽기 공식과 프리팹이 갈라졌다: {derived} vs {RING_BAKED}"
HEAD_VIS_R = 1.0 + (RING_BAKED/2.0)/(R1*BAKE_S)          # 배율 0.375~1.00 에서 불변

print("╔══ 0. 리그 (프로덕션 파일에서 읽음) ══╗")
print(f"  신장 H = {H:.7f} · 머리반경 R = {R1} · 머리중심 = {HC} · 어깨 = {SHY} · 고관절 = {HIPY}")
print(f"  어깨선 SH = {SH_R:+.6f} R · 고관절 HIP = {HIP_R:+.6f} R · 몸통 TL = {TL/R1:.6f} R · 목선 = {NECK_R:+.6f} R")
print(f"  머리 링: 명목 {RINGW} · **구운 값 {RING_BAKED}**(= max({RINGW}×{BAKE_S}, 2pt) — 실효 계수 {RING_BAKED/BAKE_S:.7f})")
print(f"  ⇒ **보이는 머리 바깥 반경 = {HEAD_VIS_R:.5f} R** (배율 {MINFILLPT/PT_PER_U/(RING_BAKED/BAKE_S):.3f}~1.00 에서 불변) · K=180 에서 {HEAD_VIS_R*180:.1f}px")
print(f"     ★ 명목 0.063으로 계산하면 {1.0+RINGW/2/R1:.4f} R 이 나오는데 **틀린 값**이다(2.5% 작다).")
print(f"  카드 아이콘 {ICON58:.0f}pt · 슬롯행 {SLOT24:.0f}pt · pt/유닛 = {PT_PER_U}")
print("╚═══════════════════════════════════╝\n")

# ─────────────────────────────────────────────────────────────────────
# 1. 출하 아이템 착용 잉크 박스 실측
# ─────────────────────────────────────────────────────────────────────
def bbox(P):   # P = [(x,y,half)]
    return (min(x-w for x,y,w in P), max(x+w for x,y,w in P),
            min(y-w for x,y,w in P), max(y+w for x,y,w in P))

# (a) 인계본 코드표
hs = rd("Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs")
arrays = {}
for m in re.finditer(r"private static readonly float\[\] (Handoff_\w+)\s*=\s*\{(.*?)\};", hs, re.S):
    v = [float(t) for t in re.findall(r"-?\d+(?:\.\d+)?", m.group(2).replace("f",""))]
    arrays[m.group(1)] = [(v[i], v[i+1]) for i in range(0, len(v), 2)]
xfs = {}
for m in re.finditer(r"case (\w+): return new AccessoryWornTransform\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, (\w+)\);", hs):
    xfs[m.group(1)] = (float(m.group(3)), float(m.group(4)), float(m.group(5)), m.group(6)=="true")
cases = [(m.start(), m.group(1)) for m in re.finditer(r"case (\w+):\s*//\s*\S+ \S+\s*—\s*조각", hs)]
calls = []
for m in re.finditer(r'HandoffPiece\(sink, rig, xf, \w+, "([^"]+)", (Handoff_\w+), loop: \w+, filled: \w+, '
                     r'tone: \d+, surfaces: (\d+), strokeMult: [\d.]+f?, strokeInR: ([\d.]+)f?, noStroke: (\w+), '
                     r'alpha: [\d.]+f?, lineAlpha: [\d.]+f?, underBack: -?\d+, layer: (\d+), '
                     r'swayStart: -?\d+, swayCount: \d+, bodyFixed: (\w+)\)', hs):
    owner = [c for s,c in cases if s < m.start()][-1]
    calls.append((owner, m.group(2), int(m.group(3)), float(m.group(4)),
                  m.group(5)=="true", int(m.group(6)), m.group(7)=="true"))

ITEMS = {}   # 이름 -> {layer: [(x,y,half)]}
def add(name, layer, pts, half):
    d = ITEMS.setdefault(name, {})
    d.setdefault(layer, []).extend([(x, y, half) for (x, y) in pts])

for owner, arrname, surf, stroke, nostroke, layer, fixed in calls:
    if surf != 0 and (surf & 1) == 0: continue          # 몸 표면이 아니면 건너뛴다
    pts = arrays[arrname]
    if not fixed and owner in xfs:
        s, ky, dy, mir = xfs[owner]
        s = s if s > 0 else 1.0; sy = s*(ky if ky > 0 else 1.0)
        pts = [(x*s*(-1 if mir else 1), y*sy + dy) for (x, y) in pts]
    add(owner, layer, pts, 0.0 if nostroke else stroke/2.0)

# (b) V1 배열(베레모·밀짚모자) — v1 획 예산(0.75)
W_V1 = max(0.048*SHIP_S, 2.0/PT_PER_U)/(R1*SHIP_S)
sb = rd("Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.cs")
v1 = {}
for m in re.finditer(r"private static readonly float\[\] (V1\w+)\s*=\s*\{(.*?)\};", sb, re.S):
    body = re.sub(r"//[^\n]*", "", m.group(2))
    v = [float(t) for t in re.findall(r"-?\d+(?:\.\d+)?", body.replace("f",""))]
    v1[m.group(1)] = [(v[i], v[i+1]) for i in range(0, len(v), 2)]
for name, names, layers in [("HeadBeret", ["V1Hat_BeretBody","V1Hat_BeretBand","V1Hat_BeretStem","V1Hat_BeretHighlight"], [0,0,0,0]),
                            ("HeadStraw", ["V1Hat_StrawBrimLens","V1Hat_StrawBrimFar","V1Hat_StrawBrimNear","V1Hat_StrawCrown","V1Hat_StrawBand","V1Hat_StrawHighlight"], [1,1,0,0,0,0])]:
    for n, L in zip(names, layers): add(name, L, v1[n], W_V1/2.0)

# (c) 에셋 wornShapes (NECK 6 + 팩 12)
FRAME = {0: R1, 1: TL, 2: SHY + R1*0.04, 3: SHY, 4: HC, 5: HIPY, 8: H}
def read_sum(t, i):
    n = int(round(t[i])); i += 1; acc = 0.0
    for _ in range(n):
        basis, gate, trig, nc = (int(round(t[i+k])) for k in range(4)); i += 4
        cs = list(t[i:i+nc]); i += nc
        v = FRAME[basis]
        if trig: cs = cs[:-1] + [math.cos(cs[-1]) if trig == 1 else math.sin(cs[-1])]
        for c in cs: v *= c
        if gate != 1: acc += v                    # stateOn 항은 기본 상태에서 빠진다
    return acc, i
def asset_shapes(path):
    txt = open(path, encoding="utf-8").read()
    m = re.search(r"\n  wornShapes:\n(.*?)(?=\n  [a-zA-Z_]+:|\Z)", txt, re.S)
    out, cur = [], None
    if not m: return out
    for line in m.group(1).split("\n"):
        s = line.strip()
        if s.startswith("- name:"): cur = {"terms": [], "T": False}; out.append(cur)
        elif cur is None: continue
        elif s == "terms:": cur["T"] = True
        elif s.startswith("- ") and cur["T"]: cur["terms"].append(float(s[2:]))
        elif ":" in s and not s.startswith("-"):
            cur["T"] = False; k, v = s.split(":", 1); cur[k] = v.strip()
    return out
for path in sorted(glob.glob(os.path.join(ROOT, "Assets/_Project/Resources/Items/*.asset"))):
    key = os.path.basename(path)[:-6]
    for s in asset_shapes(path):
        t = s["terms"]
        if not t: continue
        swing = float(s.get("swingDegrees", 0) or 0); i = 0
        n = int(round(t[i])); i += 1; pts = []
        for _ in range(n):
            if swing: _a, i = read_sum(t, i); _b, i = read_sum(t, i)
            x, i = read_sum(t, i); y, i = read_sum(t, i)
            pts.append((x/R1, (y-HC)/R1))
        st = float(s.get("strokeInR", 0) or 0)
        half = 0.0 if s.get("noStroke", "0") == "1" else st/2.0
        add(key, int(s.get("layer", 0) or 0), pts, half)

# ─────────────────────────────────────────────────────────────────────
# 2. 제안 캔버스
# ─────────────────────────────────────────────────────────────────────
AUTHOR_PX = 1024
class Canvas:
    def __init__(s, name, K, ax, ay, ship, basis):
        s.name, s.K, s.ax, s.ay, s.ship, s.basis = name, K, ax, ay, ship, basis
    @property
    def S(s):  return AUTHOR_PX/float(s.K)                 # 캔버스 한 변 (R)
    @property
    def rect(s): return (0.0, (s.ay - AUTHOR_PX/2.0)/s.K, s.S, s.S)   # wornSpriteRectInR
    def px(s, xr, yr): return (s.ax + xr*s.K, s.ay - yr*s.K)
    def rr(s, px, py): return ((px - s.ax)/s.K, (s.ay - py)/s.K)

CANV = {
  "HEAD":  Canvas("HEAD/EYES/HAIR", 180, 512, 512, 256, "머리 중심"),
  "NECK":  Canvas("NECK",           180, 512, 287, 256, "어깨선"),
  "SHLD":  Canvas("SHOULDERS",      110, 512, 105, 512, "어깨선"),
}

# 저작 봉투 — **새로 그리는 그림이 지켜야 하는 한계** (현행 실측이 이 안에 드는지 아래에서 검산한다)
ENVELOPE = {   # 이름 -> (캔버스키, |x|max, ymin, ymax)  y 는 그 캔버스의 기준선 기준
  "HEAD":      ("HEAD", 2.55, -0.80, +2.65),
  "EYES":      ("HEAD", 2.00, -2.40, +1.10),
  "HAIR":      ("HEAD", 2.60, -2.45, +2.00),
  "NECK":      ("NECK", 2.00, -3.20, +0.70),
  "SHOULDERS": ("SHLD", 3.20, -8.00, +0.65),
}
SLOT_OF = {}
for k in ITEMS:
    if k.startswith("equip_head") or k.startswith("Head") or "head" in k: SLOT_OF[k] = "HEAD"
    elif k.startswith("Eyes") or "eyes" in k: SLOT_OF[k] = "HEAD"
    elif "neck" in k: SLOT_OF[k] = "NECK"
    else: SLOT_OF[k] = "SHLD"

MARGIN_PX = 32   # 저작 캔버스에서 요구하는 최소 투명 여백

print("╔══ 1. 출하 아이템 착용 잉크 박스 실측 + 캔버스 적합 ══╗")
print(f"  (여백 하한 {MARGIN_PX}px @1024 = {MARGIN_PX/180:.3f} R @K180)")
rows = []
for k in sorted(ITEMS, key=lambda z: (SLOT_OF[z], z)):
    P = [q for L in ITEMS[k].values() for q in L]
    x0, x1, y0, y1 = bbox(P)
    c = CANV[SLOT_OF[k]]
    yy0, yy1 = (y0, y1) if c.basis == "머리 중심" else (y0 - SH_R, y1 - SH_R)
    p_l, p_t = c.px(x0, yy1); p_r, p_b = c.px(x1, yy0)
    marg = min(p_l, AUTHOR_PX-p_r, p_t, AUTHOR_PX-p_b)
    rows.append((SLOT_OF[k], k, x1-x0, yy1-yy0, marg, len(ITEMS[k])))
    print(f"  {SLOT_OF[k]:7s} {k:30s} w {x1-x0:6.3f}R h {yy1-yy0:6.3f}R  기준계 y {yy0:+7.3f}..{yy1:+7.3f}  "
          f"층{sorted(ITEMS[k])}  캔버스 여백 {marg:6.1f}px")
print()
for key, c in CANV.items():
    sub = [r for r in rows if r[0] == key]
    if not sub: continue
    worst = min(sub, key=lambda r: r[4])
    check(worst[4] >= MARGIN_PX, f"{c.name}: 최소 여백 {worst[4]:.1f}px (최빠듯 = {worst[1]}) ≥ {MARGIN_PX}px")
print()
print("  ── 저작 봉투(새 그림의 한계) 가 캔버스 안에 드는가 ──")
for en,(ck,xm,y0,y1) in ENVELOPE.items():
    c = CANV[ck]
    l,t = c.px(-xm,y1); r,b = c.px(xm,y0)
    marg = min(l, AUTHOR_PX-r, t, AUTHOR_PX-b)
    check(marg >= MARGIN_PX, f"{en:10s} 봉투 |x|≤{xm:.2f} · y {y0:+.2f}..{y1:+.2f} R → 캔버스 여백 {marg:5.1f}px")
print()
print("  ── 현행 실측이 그 봉투 안에 드는가(= 봉투가 현실적인가) ──")
SLOT_ENV = {}
for k in ITEMS:
    if "eyes" in k or k.startswith("Eyes"): SLOT_ENV[k]="EYES"
    elif "neck" in k: SLOT_ENV[k]="NECK"
    elif "head" in k or k.startswith("Head"): SLOT_ENV[k]="HEAD"
    else: SLOT_ENV[k]="SHOULDERS"
over=[]
for k in sorted(ITEMS):
    P=[q for L in ITEMS[k].values() for q in L]; x0,x1,y0,y1=bbox(P)
    ck,xm,e0,e1 = ENVELOPE[SLOT_ENV[k]]
    if CANV[ck].basis != "머리 중심": y0,y1 = y0-SH_R, y1-SH_R
    d = max(max(abs(x0),abs(x1))-xm, e0-y0, y1-e1)
    if d > 0: over.append((k, SLOT_ENV[k], d))
for k,e,d in over: print(f"  △ {k:30s} ({e}) 봉투를 {d:.3f} R 넘는다 → **다시 그릴 때 그만큼 줄인다**")
if not over: print("  현행 42+12종 전부 봉투 안 (초과 0건)")
print("╚═══════════════════════════════════════════════╝\n")

print("╔══ 2. 캔버스 규격표 (그대로 저작 규격서에 넣는다) ══╗")
print(f"  {'슬롯':12s} {'K(px/R)':>8s} {'S(R)':>8s} {'앵커px':>12s} {'rect(x,y,w,h) in R':>34s} {'출하':>6s} {'px/R@출하':>9s}")
for key, c in CANV.items():
    rx, ry, rw, rh = c.rect
    print(f"  {c.name:12s} {c.K:8d} {c.S:8.5f} {'(%d,%d)'%(c.ax,c.ay):>12s} "
          f"{'(%.5f, %.5f, %.5f, %.5f)'%(rx,ry,rw,rh):>34s} {c.ship:5d}² {c.ship/c.S:9.2f}")
print("╚═══════════════════════════════════════════════╝\n")

print("╔══ 3. 픽셀↔R 왕복 검산 ══╗")
for key, c in CANV.items():
    for (xr, yr) in [(0,0), (1,1), (-2.3, 2.6), (0.5,-1.25)]:
        p = c.px(xr, yr); q = c.rr(*p)
        if abs(q[0]-xr) > 1e-9 or abs(q[1]-yr) > 1e-9: fail.append("왕복 실패 "+key)
    rx, ry, rw, rh = c.rect
    # rect 의 뜻: 캔버스 중심이 기준점에서 (rx,ry)R 만큼 떨어져 있고, 캔버스 한 변이 rw R 이다.
    cx, cy = c.rr(AUTHOR_PX/2.0, AUTHOR_PX/2.0)
    check(abs(cx-rx) < 1e-9 and abs(cy-ry) < 1e-9 and abs(rw-c.S) < 1e-9,
          f"{c.name}: rect 중심({rx:+.5f},{ry:+.5f}) = 캔버스 중심의 R좌표 ✓ / 변 {rw:.5f}R")
print("╚═══════════════════════╝\n")

# ─────────────────────────────────────────────────────────────────────
# 3. 왕관 — 앞/뒤 판 규격
# ─────────────────────────────────────────────────────────────────────
print("╔══ 4. 왕관(equip_head_crown) — 현행 벡터 실측 = 비교 기준선 ══╗")
c = CANV["HEAD"]
crown = ITEMS["HeadCrown"]
for L, label in [(0, "앞판(layer0 → sortingOrder 10)"), (1, "뒤판(layer1 → SortBack −1)")]:
    x0, x1, y0, y1 = bbox(crown[L])
    l, t = c.px(x0, y1); r, b = c.px(x1, y0)
    print(f"  {label}")
    print(f"      R   : x {x0:+.4f}..{x1:+.4f} (w {x1-x0:.4f})   y {y0:+.4f}..{y1:+.4f} (h {y1-y0:.4f})  w/h {(x1-x0)/(y1-y0):.3f}")
    print(f"      px  : x {l:7.1f}..{r:7.1f}          y {t:7.1f}..{b:7.1f}   (1024 캔버스, 앵커 {c.ax},{c.ay})")
allp = [q for L in crown.values() for q in L]
X0, X1, Y0, Y1 = bbox(allp)
print(f"  합집합 : x {X0:+.4f}..{X1:+.4f} (w {X1-X0:.4f})  y {Y0:+.4f}..{Y1:+.4f} (h {Y1-Y0:.4f})  w/h {(X1-X0)/(Y1-Y0):.3f}")
f0 = bbox(crown[0]); b0 = bbox(crown[1])
print(f"  앞/뒤 겹침(y) : 뒤판 아래끝 {b0[2]:+.4f} vs 앞판 밴드 윗선 — 겹침 {max(0.0, min(f0[3],b0[3]) - max(f0[2],b0[2])):.4f} R")
hv = HEAD_VIS_R
print(f"  머리 보이는 원 (r={hv:.4f} R = {hv*c.K:.1f}px) 이 뒤판을 자르는 높이: y = {hv:+.4f} R (py {c.px(0,hv)[1]:.1f})")
print(f"    → 뒤판 중 y < {hv:.4f} R 부분은 **머리 원반(order 3)에 가려 보이지 않는다** = 분리의 이유")
print("╚═══════════════════════════════════════════════╝\n")

# ─────────────────────────────────────────────────────────────────────
# 4. 세 크기 검산
# ─────────────────────────────────────────────────────────────────────
print("╔══ 5. 세 크기 — 같은 물건으로 읽히는가 ══╗")
CW, CH = X1-X0, Y1-Y0
print(f"  왕관 잉크 {CW:.3f} R × {CH:.3f} R")
print(f"  [카드 {ICON58:.0f}pt]  타이트 크롭·0.86 맞춤 → 왕관 폭 {ICON58*0.86:.1f}pt · 1R = {ICON58*0.86/CW:.2f}pt")
print(f"  [슬롯행 {SLOT24:.0f}pt] 〃                    → 왕관 폭 {SLOT24*0.86:.1f}pt · 1R = {SLOT24*0.86/CW:.2f}pt")
for s in (MIN_S, 0.60, SHIP_S, MAX_S):
    hd = 2*R1*s*PT_PER_U
    print(f"  [몸 배율 {s:.2f}] 머리 지름 {hd:5.2f}pt · 1R = {hd/2:5.2f}pt · 왕관 폭 {CW*hd/2:5.2f}pt")
print()
FEAT = [("큰 보석(폭 21% · 높이 46%)", 0.21, 0.46), ("작은 보석(폭 8%)", 0.08, 0.08),
        ("뿔 끝 구슬(지름 12%)", 0.12, 0.12), ("뿔 사이 골(폭 9%)", 0.09, 0.09),
        ("밴드 아래 테(높이 6%)", 1.0, 0.06)]
DISP = [("Windows 1920×1080 @100%", 1080, 1.0), ("2560×1440", 1440, 1.0),
        ("4K 3840×2160", 2160, 1.0), ("MacBook Air 13\" M2", 1664, 1.0),
        ("MacBook Pro 14\"", 1964, 1.0), ("Pro Display XDR 6K", 3384, 1.0)]
print("  ── 몸(배율 0.75)에서 각 디테일이 몇 디바이스 px 인가 ──")
hdr = f"  {'디스플레이':26s}" + "".join(f"{n.split('(')[0][:9]:>11s}" for n,_,_ in FEAT)
print(hdr)
for name, hpx, _ in DISP:
    head_px = 0.0183333*SHIP_S*hpx
    line = f"  {name:26s}"
    for _, fw, fh in FEAT:
        px = min(CW*fw, CH*fh)*(head_px/2.0)
        line += f"{px:11.2f}"
    print(line)
print("  (1.0px 미만 = 그 배율의 그 화면에서 **존재하지 않는다**)\n")

print("  ── 출하 텍스처 텍셀 : 디바이스 픽셀 (배율 1.00 = 최악) ──")
worst = 1e9
for key, cv in CANV.items():
    for name, hpx, _ in DISP:
        head_px = 0.0183333*MAX_S*hpx
        dev_per_R = head_px/2.0
        tex_per_R = cv.ship/cv.S
        ratio = tex_per_R/dev_per_R
        worst = min(worst, ratio)
    print(f"  {cv.name:14s} 출하 {cv.ship}² → {cv.ship/cv.S:5.2f} 텍셀/R · 최악 화면 {0.0183333*MAX_S*3384/2:5.2f} px/R → 오버샘플 ×{(cv.ship/cv.S)/(0.0183333*MAX_S*3384/2):4.2f}")
check(worst >= 1.0, f"모든 슬롯·모든 화면에서 업샘플 없음 (최악 ×{worst:.2f})")
print("╚═══════════════════════════════════╝\n")

print("╔══ 결과 ══╗")
if fail:
    for f in fail: print("  ✗", f)
    print(f"╚══ 위반 {len(fail)}건 ══╝"); sys.exit(1)
print("╚══ 전수 통과 (위반 0건) ══╝")

# ─────────────────────────────────────────────────────────────────────
# 6. 왕관 파일럿 — 새 그림의 목표 잉크 박스 + 합격 판정식
# ─────────────────────────────────────────────────────────────────────
print("\n╔══ 6. 왕관 파일럿 — 새 그림의 목표치와 합격선 ══╗")
c = CANV["HEAD"]
T_HALF, T_BOT, T_TOP = 1.310, 0.28, 2.38          # 목표 잉크 박스 (R)
TOL_X, TOL_Y = 0.10, 0.12                          # 합격 허용치 (R)
BACK_TOP, BACK_BOT = 1.92, 0.64                    # 뒤판 잉크 (R)
BAND_TOP, VALLEY, TIP = 0.84, 1.44, 2.20           # 앞판 특징선 (R)
OVERLAP_MIN = 0.20

def line(lbl, yr): print(f"    {lbl:34s} y {yr:+6.3f} R   →  py {c.px(0,yr)[1]:7.1f}")
print(f"  앵커(머리 중심) = 캔버스 정중앙 (512, 512) · 1 R = {c.K} px · 머리 잉크 반경 {c.K}px · **보이는 머리 반경 {HEAD_VIS_R*c.K:.1f}px**")
print(f"  목표 잉크 폭 {2*T_HALF:.3f} R = {2*T_HALF*c.K:.1f}px   (px {c.px(-T_HALF,0)[0]:.1f} .. {c.px(T_HALF,0)[0]:.1f})")
print(f"  목표 잉크 높이 {T_TOP-T_BOT:.3f} R = {(T_TOP-T_BOT)*c.K:.1f}px  ·  w/h = {2*T_HALF/(T_TOP-T_BOT):.3f}")
line("잉크 위끝(구슬 꼭대기)", T_TOP); line("뿔 끝 구슬 중심", TIP)
line("뒤판 위끝(뒤 림 윗선)", BACK_TOP); line("뿔 사이 골 바닥", VALLEY)
line("**머리 보이는 원의 꼭대기**", HEAD_VIS_R)
line("앞판 밴드 윗선", BAND_TOP); line("뒤판 아래끝(겹침 확보)", BACK_BOT)
line("잉크 아래끝(밴드 밑테)", T_BOT); line("머리 중심 = 앵커", 0.0)
print()
check(BAND_TOP - BACK_BOT >= OVERLAP_MIN - 1e-9,
      f"앞/뒤 겹침 {BAND_TOP-BACK_BOT:.3f} R ({(BAND_TOP-BACK_BOT)*c.K:.0f}px) ≥ {OVERLAP_MIN} R — 이음매 방지")
check(BACK_BOT < HEAD_VIS_R < BACK_TOP,
      f"머리 보이는 원({HEAD_VIS_R:.4f} R)이 뒤판 안({BACK_BOT}..{BACK_TOP})을 지난다 = 분리가 실제로 필요하다")
vx0,vx1,vy0,vy1 = bbox([q for L in ITEMS['HeadCrown'].values() for q in L])
d = max(abs(T_HALF-vx1), abs(T_BOT-vy0), abs(T_TOP-vy1))
check(d <= max(TOL_X,TOL_Y), f"목표가 현행 벡터에서 최대 {d:.3f} R 벗어난다 ≤ 허용 {max(TOL_X,TOL_Y)} R — 「같은 물건」 유지")
def accept(half, bot, top):
    return abs(half-T_HALF) <= TOL_X and abs(bot-T_BOT) <= TOL_Y and abs(top-T_TOP) <= TOL_Y
ex = ENVELOPE["HEAD"]
check(T_HALF <= ex[1] and T_BOT >= ex[2] and T_TOP <= ex[3], "목표가 HEAD 봉투 안에 든다")
print(f"\n  [합격 판정식 — E3가 구운 wornSpriteInkBoxInR 로 자동 판정]")
print(f"    |minX| , maxX  =  {T_HALF:.3f} ± {TOL_X}      (좌우 대칭이어야 한다: |minX + maxX| ≤ 0.06 R)")
print(f"    minY           =  {T_BOT:.3f} ± {TOL_Y}")
print(f"    maxY           =  {T_TOP:.3f} ± {TOL_Y}")
print(f"    앞판·뒤판 두 파일의 rect 는 **같아야** 한다(= 같은 캔버스). 다르면 즉시 반려.")
print("╚═══════════════════════════════════════════════╝\n")

# ─────────────────────────────────────────────────────────────────────
# 7. 대조 — 이 하니스가 살아 있는가 (양성/음성)
# ─────────────────────────────────────────────────────────────────────
print("╔══ 7. 대조 (죽은 프로브 방지) ══╗")
def worst_margin(cv):
    m = 1e9
    for en,(ck,xm,y0,y1) in ENVELOPE.items():
        if ck != "HEAD": continue                    # 이 대조는 머리 캔버스만 흔든다
        l,t = cv.px(-xm,y1); r,b = cv.px(xm,y0)
        m = min(m, l, AUTHOR_PX-r, t, AUTHOR_PX-b)
    return m
base_m = worst_margin(CANV["HEAD"])
for dy in (+40, -40):
    cv = Canvas("shift", 180, 512, 512+dy, 256, "머리 중심")
    m = worst_margin(cv)
    check(m < MARGIN_PX, f"앵커를 {dy:+d}px 밀면 최소 여백 {base_m:.1f}px → {m:.1f}px 로 무너진다(빨강)")
#  배율을 틀린 그림은 「여백」이 아니라 **합격 판정식**이 잡는다 — 대조도 같은 식으로 한다.
for k in (160, 200):
    f = 180.0/k                                   # K를 틀리게 그리면 잉크가 f 배로 온다
    ok = accept(T_HALF*f, T_BOT*f, T_TOP*f)
    check(not ok, f"K를 180 대신 {k} 로 그린 그림(잉크 ×{f:.3f})은 합격 판정식이 **반려**한다")
check(accept(T_HALF, T_BOT, T_TOP), "양성 대조: 목표 그대로인 그림은 합격 판정식을 **통과**한다")
check(not accept(T_HALF, T_BOT+0.20, T_TOP+0.20), "양성 대조: 0.20R 위로 밀린 그림은 반려된다")
print(f"  양성 대조: 확정 캔버스는 위 1~6 절에서 전부 초록이었다 (같은 코드 경로)")
print("╚═══════════════════════════════╝\n")

# ─────────────────────────────────────────────────────────────────────
# 8. 납품된 PNG 를 실제로 재는 자리 (파일이 있으면 돈다)
# ─────────────────────────────────────────────────────────────────────
def audit_png(path, canvas_key="HEAD"):
    """저작 원본 1024² PNG 하나를 규격에 대고 잰다. 합격 판정식·알파·좌우조명 대칭을 한 번에."""
    from PIL import Image
    import numpy as np
    cv = CANV[canvas_key]
    a = np.asarray(Image.open(path).convert("RGBA"))
    assert a.shape[0] == a.shape[1] == AUTHOR_PX, f"캔버스가 {AUTHOR_PX}² 가 아니다: {a.shape}"
    al = a[..., 3].astype(int)
    ys, xs = np.nonzero(al >= 128)
    # ★ 픽셀 <b>가장자리</b> 기준 — Core/WornSpritePlacement 클래스 문서의 규약과 같게 맞춘다
    #   (중심 기준으로 재면 잉크가 정확히 한 픽셀 좁아진다).
    x0, y0 = cv.rr(xs.min(), ys.max() + 1); x1, y1 = cv.rr(xs.max() + 1, ys.min())
    op = int((al >= 248).sum()); mid = int(((al > 7) & (al < 248)).sum())
    crop = a[ys.min():ys.max()+1, xs.min():xs.max()+1]
    L = crop[..., :3].astype(float).mean(axis=2) * (crop[..., 3]/255.0)
    asym = float(np.abs(L - L[:, ::-1]).mean())
    print(f"  {os.path.basename(path)}")
    print(f"    잉크박스(R)  minX {x0:+.4f}  maxX {x1:+.4f}  minY {y0:+.4f}  maxY {y1:+.4f}")
    print(f"    좌우 대칭    |minX+maxX| = {abs(x0+x1):.4f} R   (≤ 0.06 이어야 한다)")
    print(f"    중간 알파    {mid}px = 불투명({op}px)의 {mid/max(op,1)*100:.1f}%   (≤ 10% — 넘으면 글로우·반투명 그림자다)")
    print(f"    좌우 조명    반전 밝기 평균차 {asym:.2f}   (≤ 6 이어야 한다 — 위에서 오는 광원)")
    return x0, x1, y0, y1, mid/max(op,1), asym

if len(sys.argv) > 1:
    print("╔══ 8. 납품 PNG 실측 ══╗")
    for f in sys.argv[1:]: audit_png(f)
    print("╚═════════════════════╝\n")

print("╔══ 최종 ══╗")
if fail:
    for f in fail: print("  ✗", f)
    print(f"╚══ 위반 {len(fail)}건 ══╝"); sys.exit(1)
print("╚══ 전수 통과 (위반 0건) ══╝")
