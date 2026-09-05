# -*- coding: utf-8 -*-
"""R14 — 천모자(HatCrown/HatBrim) 처방 후보 검산.

design-character 가 실빌드 캡처에서 낸 3건을 프로덕션 좌표로 재현하고,
후보 좌표를 넣었을 때 (가) 합성 실측이 어떻게 움직이는지 (나) 기존 게이트가 깨지는지를
같이 본다. 검사 코드는 새로 쓰지 않는다 — design/equipment/verify/verify.py 를 그대로 exec 한다.

    R14_DUMP=<prod_dump.txt> python3 r14_capfit.py

좌표: 머리 중심 원점 · R 배수 · +x = 진행 방향.
"""
import os, sys, types, math, io, contextlib
import numpy as np
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig as rigmod
from rig import Shape, W

DUMP = os.environ.get("R14_DUMP")
if not DUMP:
    raise SystemExit("R14_DUMP=<prod_dump.txt> 필요")

def W_at(scale):
    return max(0.048 * scale, 2.0 / 35.25) / (0.22 * scale)

W075, W060 = W_at(0.75), W_at(0.60)

# ---------------------------------------------------------------- 덤프
CATS, COVER = {}, {}
cur = None
for line in open(DUMP, encoding="utf-8"):
    f = line.rstrip("\n").split("\t")
    if f[0] == "@ITEM":
        cur = (f[1], f[2]); CATS.setdefault(f[1], {})[f[2]] = []
    elif f[0] == "@SHAPE":
        pts = [tuple(float(v) for v in p.split(",")) for p in f[6:]]
        CATS[cur[0]][cur[1]].append(
            Shape(f[1], pts, loop=f[2] == "1", filled=f[3] == "1", tone=int(f[4])))
    elif f[0] == "@COVER":
        COVER[int(f[1])] = float("inf") if f[2] == "inf" else float(f[2])

CAP = "야구모자"       # 덤프의 HEAD[0] = 카탈로그의 «천모자»

# ---------------------------------------------------------------- 후보
def crown(half=1.02, top=1.22):
    return [(-half, -0.22), (-half, 0.36), (-0.72, 1.04), (0.0, top),
            (0.72, 1.02), (half, 0.34), (half, -0.06), (0.60, 0.04), (-0.40, 0.06)]

BRIM_C0 = [(-0.40, 0.06), (0.60, 0.04), (1.36, -0.02), (1.92, -0.34),
           (1.22, -0.54), (0.62, -0.26), (-0.06, -0.12)]
# G4(처방) — 밑선을 얼굴 앞에서 평평하게(−0.14), 처짐은 머리 원 밖(x ≥ 1.28)으로 옮긴다.
#            바꾸는 점은 2개뿐이다: (1.22,−0.54)→(1.28,−0.60) · (0.62,−0.26)→(0.92,−0.14)
BRIM_C1 = [(-0.40, 0.06), (0.60, 0.04), (1.36, -0.02), (1.92, -0.34),
           (1.28, -0.60), (0.92, -0.14), (-0.06, -0.12)]

def mk(crown_pts, brim_pts):
    return [Shape("HatCrown", crown_pts, loop=True, filled=True, tone=0),
            Shape("HatBrim",  brim_pts,  loop=True, filled=True, tone=1),
            Shape("HatBand", [(-1.02, -0.02), (1.02, -0.02), (1.02, 0.44), (-1.02, 0.44)],
                  loop=True, filled=True, tone=2)]

def mk_band(half):
    return Shape("HatBand", [(-half, -0.02), (half, -0.02), (half, 0.44), (-half, 0.44)],
                 loop=True, filled=True, tone=2)

CANDS = {
    "C0 프로덕션":            mk(crown(1.02), BRIM_C0),
    "C1 챙밑선만":            mk(crown(1.02), BRIM_C1),
    "C2 C1+관0.94":           [Shape("HatCrown", crown(0.94), True, True, 0),
                               Shape("HatBrim", BRIM_C1, True, True, 1), mk_band(0.94)],
}

# ---------------------------------------------------------------- 래스터
EXT, N = 2.6, 1400
xs = np.linspace(-EXT, EXT, N)
X, Y = np.meshgrid(xs, xs)
PXR = 2 * EXT / (N - 1)
HEAD = (X * X + Y * Y) <= 1.0

def fill_mask(pts):
    m = np.zeros(X.shape, dtype=bool); n = len(pts)
    for i in range(n):
        x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % n]
        cond = ((y1 > Y) != (y2 > Y))
        xint = (x2 - x1) * (Y - y1) / (y2 - y1 + 1e-30) + x1
        m ^= cond & (X < xint)
    return m

def seg_d(a, b):
    ax, ay = a; bx, by = b
    vx, vy = bx - ax, by - ay
    L2 = vx * vx + vy * vy
    if L2 < 1e-12: return np.hypot(X - ax, Y - ay)
    t = np.clip(((X - ax) * vx + (Y - ay) * vy) / L2, 0.0, 1.0)
    return np.hypot(X - (ax + t * vx), Y - (ay + t * vy))

def ink(shapes, w):
    m = np.zeros(X.shape, dtype=bool)
    for s in shapes:
        if s.filled and len(s.pts) >= 3: m |= fill_mask(s.pts)
        n = len(s.pts); segs = n if s.loop else n - 1
        for i in range(segs):
            m |= seg_d(s.pts[i], s.pts[(i + 1) % n]) <= w / 2.0
    return m

def fill_only(shapes):
    m = np.zeros(X.shape, dtype=bool)
    for s in shapes:
        if s.filled and len(s.pts) >= 3: m |= fill_mask(s.pts)
    return m

def max_inscribed_diam(mask):
    """남은 잉크의 최대 내접 지름(R). design-character D1 과 같은 뜻, 다른 방법(거리변환)."""
    if not mask.any(): return 0.0
    return 2.0 * ndimage.distance_transform_edt(mask).max() * PXR

EYES = CATS["EYES"]["선글라스"]
lensF = [s for s in EYES if s.name.endswith("LensFront")]
lensB = [s for s in EYES if s.name.endswith("LensBack")]

print("W(0.75) = %.4f R · W(0.60) = %.4f R · 머리 지름 = %.2f획(0.75) / %.2f획(0.60)"
      % (W075, W060, 2 / W075, 2 / W060))
print()
hdr = "%-14s %9s %9s %9s %9s %9s %9s %9s" % (
    "후보", "잉크최저", "머리덮음", "앞렌즈생존", "뒤렌즈생존", "안경D1(0.75)", "안경D1(0.60)", "모자cx")
print(hdr); print("-" * len(hdr))

eyes_ink75 = ink(EYES, W075)
eyes_fill = fill_only(EYES)
print("  (참고) 모자 없을 때 안경 잉크 cx = %+.3f · 채움 cx = %+.3f · D1 = %.2f획(0.75)"
      % (X[eyes_ink75].mean(), X[eyes_fill].mean(), max_inscribed_diam(eyes_fill) / W075))

for name, shapes in CANDS.items():
    h75 = ink(shapes, W075)
    oh = h75 & HEAD
    ylow = Y[oh].min()
    cover = oh.sum() / HEAD.sum()
    fsurv = fill_only(lensF) & ~h75
    bsurv = fill_only(lensB) & ~h75
    fF = fsurv.sum() / max(fill_only(lensF).sum(), 1)
    fB = bsurv.sum() / max(fill_only(lensB).sum(), 1)
    surv75 = eyes_fill & ~h75
    surv60 = eyes_fill & ~ink(shapes, W060)
    d75 = max_inscribed_diam(surv75) / W075
    d60 = max_inscribed_diam(surv60) / W060
    print("%-14s %+9.3f %8.1f%% %8.1f%% %8.1f%% %10.2f획 %10.2f획 %+9.3f"
          % (name, ylow, cover * 100, fF * 100, fB * 100, d75, d60, X[h75].mean()))
    if name != "C0 프로덕션":
        vis = ink(EYES, W075) & ~h75
        print("               남은 안경 잉크 cx = %+.3f (C0 대비 축 어긋남 축소량 참고)"
              % X[vis].mean())

# 챙 자체의 1-C (채움 최대 내접 지름)
for tag, b in (("C0", BRIM_C0), ("C1", BRIM_C1)):
    m = fill_mask(b)
    print("  챙 %s 채움 ρ_max 지름 = %.3f R = %.2f획(0.75) / %.2f획(0.60)"
          % (tag, max_inscribed_diam(m), max_inscribed_diam(m) / W075, max_inscribed_diam(m) / W060))

# ---------------------------------------------------------------- 기존 게이트
print()
print("── verify.py 전수 (후보를 HEAD[%s]에 끼워 넣고) ──" % CAP)
for name, shapes in CANDS.items():
    m = types.ModuleType("items")
    m.HEAD = dict(CATS["HEAD"]); m.HEAD[CAP] = shapes
    m.EYES, m.NECK, m.BACK = CATS["EYES"], CATS["NECK"], CATS["BACK"]
    m.EYE_FRONT_ONLY = {"외알안경", "안대"}; m.COVER = COVER
    h = types.ModuleType("hair"); h.SET = CATS["HAIR"]
    sys.modules["items"], sys.modules["hair"] = m, h
    cwd = os.getcwd(); os.chdir(HERE)
    src = open("verify.py", encoding="utf-8").read()
    g = {"__name__": "__main__"}
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        exec(compile(src, "verify.py", "exec"), g)
    os.chdir(cwd)
    out = buf.getvalue()
    print("  %-14s 위반 %s건" % (name, g.get("fail")))
    for ln in out.splitlines():
        if "✗" in ln or "HEAD 쌍" in ln: print("      " + ln.strip())
