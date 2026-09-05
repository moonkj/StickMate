# -*- coding: utf-8 -*-
"""R14 — 모자 6종 x 안경(선글라스) 합성 실측.

design-character 가 실빌드 캡처에서 잰 3건을 <b>프로덕션 좌표로</b> 재현한다.
좌표는 Tools/ShapeDump/build.sh 덤프(= 프로덕션 C# 이 실제로 만드는 점)를 그대로 읽는다.

재는 것
  (1) 모자 잉크가 머리 원 위에서 내려온 최저 y, 그리고 선글라스 두 렌즈의 <b>가려진 비율</b>
  (2) 모자 실루엣 폭 / 머리 지름, 그리고 머리 원반을 모자 잉크가 덮은 비율
  (3) 모자 잉크 무게중심 x, 안경 <b>보이는</b> 잉크 무게중심 x, 둘의 어긋남

단위는 전부 머리 반경 R. 획 W 는 rig.py 와 같은 유도(배율 0.75)를 쓴다.
"""
import math, sys, os, subprocess
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from rig import W          # 0.343864 R  (배율 0.75)

DUMP = os.environ.get("R14_DUMP")
if not DUMP:
    raise SystemExit("R14_DUMP=<prod_dump.txt> 를 지정해라 (Tools/ShapeDump/build.sh 출력)")

# ---- 덤프 읽기 -----------------------------------------------------------------
def read_dump(path):
    items = {}   # (cat, name) -> [ (shapename, loop, filled, tone, pts) ]
    cur = None
    for line in open(path, encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        if f[0] == "@ITEM":
            cur = (f[1], f[2]); items[cur] = []
        elif f[0] == "@SHAPE" and cur:
            name = f[1]; loop = f[2] == "1"; filled = f[3] == "1"; tone = int(f[4])
            pts = [tuple(float(v) for v in p.split(",")) for p in f[6:]]
            items[cur].append((name, loop, filled, tone, pts))
    return items

ITEMS = read_dump(DUMP)

# ---- 래스터 --------------------------------------------------------------------
EXT = 2.6
N   = 1600                       # 3.25e-3 R/px
xs  = np.linspace(-EXT, EXT, N)
ys  = np.linspace(-EXT, EXT, N)
X, Y = np.meshgrid(xs, ys)
PX2 = (2 * EXT / (N - 1)) ** 2    # 픽셀 하나의 R^2 면적

def seg_dist(px, py, a, b):
    ax, ay = a; bx, by = b
    vx, vy = bx - ax, by - ay
    L2 = vx * vx + vy * vy
    if L2 < 1e-12:
        return np.hypot(px - ax, py - ay)
    t = np.clip(((px - ax) * vx + (py - ay) * vy) / L2, 0.0, 1.0)
    return np.hypot(px - (ax + t * vx), py - (ay + t * vy))

def fill_mask(pts):
    """짝수-홀수 규칙 폴리곤 채움."""
    m = np.zeros(X.shape, dtype=bool)
    n = len(pts)
    for i in range(n):
        x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % n]
        cond = ((y1 > Y) != (y2 > Y))
        with np.errstate(divide="ignore", invalid="ignore"):
            xint = (x2 - x1) * (Y - y1) / (y2 - y1 + 1e-30) + x1
        m ^= cond & (X < xint)
    return m

def ink_mask(shape, w=W):
    name, loop, filled, tone, pts = shape
    m = np.zeros(X.shape, dtype=bool)
    if filled and len(pts) >= 3:
        m |= fill_mask(pts)
    n = len(pts)
    segs = n if loop else n - 1
    half = w / 2.0
    for i in range(segs):
        m |= seg_dist(X, Y, pts[i], pts[(i + 1) % n]) <= half
    return m

HEAD = (X * X + Y * Y) <= 1.0

def item_ink(cat, name):
    m = np.zeros(X.shape, dtype=bool)
    for s in ITEMS[(cat, name)]:
        m |= ink_mask(s)
    return m

def stats(mask):
    a = mask.sum() * PX2
    if mask.sum() == 0:
        return 0.0, float("nan"), float("nan")
    return a, X[mask].mean(), Y[mask].mean()

# ---- 측정 ----------------------------------------------------------------------
HATS = [n for (c, n) in ITEMS if c == "HEAD"]
EYES_NAME = "선글라스"
eye_shapes = ITEMS[("EYES", EYES_NAME)]

print("획 W = %.6f R · 머리 지름 = %.2f 획" % (W, 2.0 / W))
print()
hdr = ("%-8s %8s %8s %8s %8s %8s %8s %8s" %
       ("모자", "폭/2R", "머리덮음", "잉크최저", "앞렌즈가림", "뒤렌즈가림", "모자cx", "눈보임cx"))
print(hdr); print("-" * len(hdr))

eye_all = np.zeros(X.shape, dtype=bool)
lens_front = None; lens_back = None
for s in eye_shapes:
    m = ink_mask(s)
    eye_all |= m
    if s[0].endswith("LensFront"): lens_front = m
    if s[0].endswith("LensBack"):  lens_back = m

rows = []
for hat in HATS:
    hm = item_ink("HEAD", hat)
    width = (X[hm].max() - X[hm].min()) / 2.0            # / (2R) -> 지름 대비
    over_head = hm & HEAD
    cover = over_head.sum() / HEAD.sum()
    ylow = Y[over_head].min() if over_head.any() else float("nan")
    fcov = (lens_front & hm).sum() / max(lens_front.sum(), 1)
    bcov = (lens_back & hm).sum() / max(lens_back.sum(), 1)
    vis_eye = eye_all & ~hm
    _, hcx, _ = stats(hm)
    _, ecx, _ = stats(vis_eye)
    rows.append((hat, width, cover, ylow, fcov, bcov, hcx, ecx))
    print("%-8s %7.3f%% %7.1f%% %+8.3f %7.1f%% %7.1f%% %+8.3f %+8.3f" %
          (hat, width * 100, cover * 100, ylow, fcov * 100, bcov * 100, hcx, ecx))

print()
# 챙만 따로 — 천모자
cap = ITEMS[("HEAD", HATS[0])]
for s in cap:
    m = ink_mask(s)
    oh = m & HEAD
    a, cx, cy = stats(m)
    print("  [%s] 잉크면적 %.3f R² · cx %+.3f · 머리위 최저 y %+.3f · 머리덮음 %.1f%%" %
          (s[0], a, cx, (Y[oh].min() if oh.any() else float("nan")),
           100.0 * oh.sum() / HEAD.sum()))
