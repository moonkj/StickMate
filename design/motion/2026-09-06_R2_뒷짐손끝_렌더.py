#!/usr/bin/env python3
"""프로덕션 규칙을 그대로 옮긴 오프라인 렌더 — 판정용이 아니라 «실기 캡처와 대조»용이다.
  · 마디 폴리라인: States/LimbCurveRenderer.SolveFilletLength / FillArcs 와 같은 식
  · 획: 둥근 캡 + 둥근 조인(프리팹 LineRenderer 관례)
  · 치수: Assets/_Project/Prefabs/Stickman.prefab 실측
"""
import math, sys, os
from PIL import Image, ImageDraw

# ── 프리팹 실측(월드 유닛, 배율 0.75로 구워짐) ─────────────────────────────
SH = (0.0, 1.3235208)          # 두 팔 공용 어깨 부착점
ARM_U, ARM_L = 0.285, 0.2775
ARM_W = 0.078375
HIP = (0.0, 0.7010208)
LEG_U, LEG_L = 0.375, 0.33749998
LEG_W = 0.09404999
TORSO_TOP, TORSO_BOT = 1.3760209, 0.7010208
TORSO_W = 0.08621249
HEAD_C, HEAD_R, HEAD_W = (0.0, 1.5410209), 0.165, 0.056737587
R = 0.165

# ── LimbCurveRenderer ─────────────────────────────────────────────────────
FILLET_RATIO = 0.42
MAX_SAG_PER_W = 1.0
MIN_HALF = 0.00436
ARC_SAMPLES = 4


def fillet_len(lu, ll, ang_deg, w):
    h = 0.5 * abs(ang_deg) * math.pi / 180.0
    t = FILLET_RATIO * min(lu, ll)
    if h > MIN_HALF and w > 0:
        sag = t * math.tan(0.5 * h)
        mx = MAX_SAG_PER_W * w
        if sag > mx:
            t *= mx / sag
    return t


def limb_polyline(origin, upper_deg, lower_deg, lu, ll, w):
    """어깨 로컬 각도(0=아래, +=시계 반대?) 규약은 프로덕션과 같다: 마디 끝 = (sinθ, −cosθ)."""
    t = fillet_len(lu, ll, lower_deg, w)
    h = 0.5 * abs(lower_deg) * math.pi / 180.0
    s = 1.0 if lower_deg >= 0 else -1.0
    pts_u = [(0.0, 0.0)]
    if h <= MIN_HALF:
        for k in range(ARC_SAMPLES):
            u = k / (ARC_SAMPLES - 1)
            pts_u.append((0.0, -((lu - t) + t * u)))
        pts_l = [(0.0, -t + t * (1 - k / (ARC_SAMPLES - 1))) for k in range(ARC_SAMPLES)]
        pts_l.append((0.0, -ll))
    else:
        r = t / math.tan(h)
        for k in range(ARC_SAMPLES):
            phi = h * k / (ARC_SAMPLES - 1)
            pts_u.append((s * r * (1 - math.cos(phi)), -(lu - t) - r * math.sin(phi)))
        pts_l = []
        for k in range(ARC_SAMPLES - 1, -1, -1):
            phi = h * k / (ARC_SAMPLES - 1)
            pts_l.append((s * r * (1 - math.cos(phi)), -t + r * math.sin(phi)))
        pts_l.append((0.0, -ll))

    def rot(p, deg):
        # R·(0,−L) = (L·sinθ, −L·cosθ) 가 되도록 = 표준 반시계 회전
        a = deg * math.pi / 180.0
        return (p[0] * math.cos(a) - p[1] * math.sin(a),
                p[0] * math.sin(a) + p[1] * math.cos(a))

    out = [(origin[0] + rot(p, upper_deg)[0], origin[1] + rot(p, upper_deg)[1]) for p in pts_u]
    elbow_local = rot((0.0, -lu), upper_deg)
    elbow = (origin[0] + elbow_local[0], origin[1] + elbow_local[1])
    for p in pts_l[1:]:
        q = rot(p, upper_deg + lower_deg)
        out.append((elbow[0] + q[0], elbow[1] + q[1]))
    return out


# ── 스트로크(둥근 캡·조인) ────────────────────────────────────────────────
class Canvas:
    def __init__(self, ppu, w_units, h_units, x0, y0, ss=4, bg=(255, 255, 255)):
        self.ppu = ppu * ss
        self.ss = ss
        self.x0, self.y0 = x0, y0
        self.W = int(w_units * self.ppu)
        self.H = int(h_units * self.ppu)
        self.im = Image.new("RGB", (self.W, self.H), bg)
        self.d = ImageDraw.Draw(self.im)

    def px(self, p):
        return ((p[0] - self.x0) * self.ppu, (self.y0 - p[1]) * self.ppu)

    def stroke(self, pts, width, color=(0, 0, 0)):
        r = width * self.ppu / 2.0
        q = [self.px(p) for p in pts]
        for i in range(len(q) - 1):
            self.d.line([q[i], q[i + 1]], fill=color, width=max(1, int(round(2 * r))))
        for p in q:
            self.d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=color)

    def circle_outline(self, c, rad, width, color=(0, 0, 0)):
        n = 64
        pts = [(c[0] + rad * math.cos(2 * math.pi * i / n), c[1] + rad * math.sin(2 * math.pi * i / n))
               for i in range(n + 1)]
        self.stroke(pts, width, color)

    def out(self, path, final_scale=1.0):
        im = self.im.resize((max(1, int(self.W / self.ss * final_scale)),
                             max(1, int(self.H / self.ss * final_scale))), Image.LANCZOS)
        im.save(path)
        return im.size


def draw_figure(cv, arms, legs=((6.0, -6.0), (-6.0, 6.0))):
    for (u, l) in legs:
        cv.stroke(limb_polyline(HIP, u, l, LEG_U, LEG_L, LEG_W), LEG_W)
    cv.stroke([(0, TORSO_BOT), (0, TORSO_TOP)], TORSO_W)
    cv.circle_outline(HEAD_C, HEAD_R, HEAD_W)
    for (u, l) in arms:
        cv.stroke(limb_polyline(SH, u, l, ARM_U, ARM_L, ARM_W), ARM_W)


def back_arms(S=4.0, E=6.0, sign=-1.0, mid=72.5, base=-51.5):
    return [(base + S, mid + sign * E), (base - S, mid - sign * E)]


CROSS = [(-8.5, 116.5), (-37.0, 113.0)]

if __name__ == "__main__":
    SP = os.path.dirname(os.path.abspath(__file__))
    PPU_TRUE = 70.5     # 배율 0.75 · Retina 실측: 1 world = 70.5 물리px (1R = 11.63px)
    cases = [
        ("cross", CROSS),
        ("back_cur", back_arms()),
        ("back_E0", back_arms(E=0.0)),
        ("back_E6same", back_arms(E=6.0, sign=+1.0)),
        ("back_E15", back_arms(E=15.0)),
        ("back_E18", back_arms(E=18.0)),
        ("back_S8E12", back_arms(S=8.0, E=12.0)),
    ]
    for name, arms in cases:
        for tag, ppu, ss in (("true", PPU_TRUE, 6), ("zoom", PPU_TRUE * 6, 3)):
            cv = Canvas(ppu, 1.1, 1.95, -0.55, 1.82, ss=ss)
            draw_figure(cv, arms)
            print(name, tag, cv.out(f"{SP}/render_{name}_{tag}.png"))
