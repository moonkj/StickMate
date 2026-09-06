#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 팔짱 실루엣 후보 렌더 — 프로덕션 규칙을 그대로 옮긴 오프라인 렌더.

`2026-09-06_R2_뒷짐손끝_렌더.py`에서 그대로 가져온 것:
  · 마디 폴리라인 = States/LimbCurveRenderer.SolveFilletLength / FillArcs 와 같은 식
  · 획 = 둥근 캡 + 둥근 조인 (프리팹 LineRenderer 관례)
  · 치수 = Assets/_Project/Prefabs/Stickman.prefab 실측(배율 0.75로 구워짐)

R3에서 더한 것 (R2 렌더러에 없어서 실기와 갈라졌던 두 가지):
  · **몸통 기울임** — RequestBodyLean은 Torso/Head만 엉덩이 기준으로 돌리고
    팔 부착점은 루트 고정이다(StickmanPoseAnimator.ApplyBodyPlacement / LeanedFacing).
  · **그리기 순서** — 다리(0) → 몸통(1) → 팔(2) → 머리채움(3) → 머리링(4).
    R2 렌더러는 머리를 팔보다 먼저 그려서 실기와 순서가 달랐다.
  · 배경색 선택(투명 오버레이 대조용) + 헤일로 시안(비용을 눈으로 보기 위한 것).

**판정용이 아니다.** 최종 판정은 실제 빌드 캡처로만 한다.
"""
import math
import os
from PIL import Image, ImageDraw

# ── 프리팹 실측(월드 유닛, 배율 0.75로 구워짐) ─────────────────────────────
SH = (0.0, 1.3235208)
ARM_U, ARM_L = 0.285, 0.2775
ARM_W = 0.078375
HIP = (0.0, 0.7010208)
LEG_U, LEG_L = 0.375, 0.33749998
LEG_W = 0.09404999
TORSO_TOP, TORSO_BOT = 1.3760209, 0.7010208
TORSO_W = 0.08621249
HEAD_C, HEAD_R, HEAD_W = (0.0, 1.5410209), 0.165, 0.056737587

FILLET_RATIO, MAX_SAG_PER_W, MIN_HALF, ARC_SAMPLES = 0.42, 1.0, 0.00436, 4

INK = (0, 0, 0)
DESKTOP = (26, 47, 138)      # 실기 캡처(real_p1_row.png)의 바탕화면 파랑
PAPER = (255, 255, 255)


def fillet_len(lu, ll, ang_deg, w):
    h = 0.5 * abs(ang_deg) * math.pi / 180.0
    t = FILLET_RATIO * min(lu, ll)
    if h > MIN_HALF and w > 0:
        sag = t * math.tan(0.5 * h)
        if sag > MAX_SAG_PER_W * w:
            t *= (MAX_SAG_PER_W * w) / sag
    return t


def limb_polyline(origin, upper_deg, lower_deg, lu, ll, w):
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
        a = deg * math.pi / 180.0
        return (p[0] * math.cos(a) - p[1] * math.sin(a),
                p[0] * math.sin(a) + p[1] * math.cos(a))

    out = [(origin[0] + rot(p, upper_deg)[0], origin[1] + rot(p, upper_deg)[1]) for p in pts_u]
    el = rot((0.0, -lu), upper_deg)
    elbow = (origin[0] + el[0], origin[1] + el[1])
    for p in pts_l[1:]:
        q = rot(p, upper_deg + lower_deg)
        out.append((elbow[0] + q[0], elbow[1] + q[1]))
    return out


def lean_pt(p, lean_deg):
    """엉덩이 기준 회전. lean − = 뒤로 젖힘(ApplyBodyPlacement의 부호 규약)."""
    if not lean_deg:
        return p
    a = math.radians(-lean_deg)
    dx, dy = p[0] - HIP[0], p[1] - HIP[1]
    return (HIP[0] + dx * math.cos(a) - dy * math.sin(a),
            HIP[1] + dx * math.sin(a) + dy * math.cos(a))


class Canvas:
    def __init__(self, ppu, w_units, h_units, x0, y0, ss=4, bg=PAPER):
        self.ppu = ppu * ss
        self.ss = ss
        self.x0, self.y0 = x0, y0
        self.W = int(w_units * self.ppu)
        self.H = int(h_units * self.ppu)
        self.im = Image.new("RGB", (self.W, self.H), bg)
        self.d = ImageDraw.Draw(self.im)

    def px(self, p):
        return ((p[0] - self.x0) * self.ppu, (self.y0 - p[1]) * self.ppu)

    def stroke(self, pts, width, color=INK):
        r = width * self.ppu / 2.0
        q = [self.px(p) for p in pts]
        for i in range(len(q) - 1):
            self.d.line([q[i], q[i + 1]], fill=color, width=max(1, int(round(2 * r))))
        for p in q:
            self.d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=color)

    def disc(self, c, rad, color=INK):
        p = self.px(c)
        r = rad * self.ppu
        self.d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=color)

    def out(self, path, final_scale=1.0):
        im = self.im.resize((max(1, int(self.W / self.ss * final_scale)),
                             max(1, int(self.H / self.ss * final_scale))), Image.LANCZOS)
        im.save(path)
        return im


def draw_figure(cv, arm_a, arm_b, lean=0.0, legs=((6.0, -6.0), (-6.0, 6.0)),
                halo=0.0, halo_color=PAPER):
    """arm_a = 앞팔(A, 최전방·나중에 그림) / arm_b = 뒷팔(B). 각각 (어깨각, 팔꿈치각)."""
    # 0 다리
    for (u, l) in legs:
        cv.stroke(limb_polyline(HIP, u, l, LEG_U, LEG_L, LEG_W), LEG_W)
    # 1 몸통 (기울임은 몸통/머리에만)
    cv.stroke([lean_pt((0, TORSO_BOT), lean), lean_pt((0, TORSO_TOP), lean)], TORSO_W)
    # 2 팔 — B(뒤) 먼저, A(앞) 나중. 헤일로는 A 밑에 한 겹.
    pb = limb_polyline(SH, arm_b[0], arm_b[1], ARM_U, ARM_L, ARM_W)
    pa = limb_polyline(SH, arm_a[0], arm_a[1], ARM_U, ARM_L, ARM_W)
    cv.stroke(pb, ARM_W)
    if halo > 0.0:
        cv.stroke(pa, ARM_W + 2.0 * halo, halo_color)
    cv.stroke(pa, ARM_W)
    # 3/4 머리 (팔보다 위)
    hc = lean_pt(HEAD_C, lean)
    cv.disc(hc, HEAD_R + HEAD_W * 0.5)


# ── 후보 (배율 1.0 각도 그대로 — 각도는 배율에 안 걸린다) ───────────────────
CANDS = [
    ("V0 현행 lean-4",   (-8.5, 116.5), (-37.0, 113.0), -4.0),
    ("V0L 현행 lean0",   (-8.5, 116.5), (-37.0, 113.0),  0.0),
    ("V1 쐐기 -44/100",  (-8.5, 116.5), (-44.0, 100.0), -4.0),
    ("V2 쐐기 -47/94",   (-8.5, 116.5), (-47.0,  94.0), -4.0),
    ("V6 채택 -45/97",   (-8.5, 116.5), (-45.0,  97.0),  0.0),
    ("V3 A노출 -15",     (-15.0, 116.5), (-42.0, 104.0), -4.0),
    ("V5 허리 -26",      (-26.0, 116.5), (-50.0,  98.0), -4.0),
    ("P2 뒷짐(대조)",     (-47.5, 66.5), (-55.5, 78.5),  -2.5),
]

PPU_TRUE = 70.5   # 배율 0.75 · Retina 실측: 1 world = 70.5 물리px


def sheet(path, cands, ppu, ss, zoom, bg, halo=0.0, label=True):
    from PIL import ImageFont
    tiles = []
    for name, a, b, lean in cands:
        cv = Canvas(ppu, 1.15, 1.05, -0.55, 1.62, ss=ss, bg=bg)
        draw_figure(cv, a, b, lean, halo=halo)
        tiles.append((name, cv.out("/dev/null" if False else
                                   os.path.join(os.path.dirname(path), "_tmp_tile.png"))))
    w, h = tiles[0][1].size
    cols = 4
    rows = (len(tiles) + cols - 1) // cols
    pad, head = 6, 18 if label else 0
    sheetim = Image.new("RGB", (cols * (w + pad) + pad, rows * (h + pad + head) + pad), (245, 245, 245))
    dr = ImageDraw.Draw(sheetim)
    try:
        font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Unicode.ttf", 12)
    except Exception:
        font = None
    for i, (name, im) in enumerate(tiles):
        c, r = i % cols, i // cols
        x = pad + c * (w + pad)
        y = pad + r * (h + pad + head)
        if label:
            dr.text((x + 2, y), name, fill=(20, 20, 20), font=font)
        sheetim.paste(im, (x, y + head))
    sheetim = sheetim.resize((int(sheetim.width * zoom), int(sheetim.height * zoom)), Image.LANCZOS)
    sheetim.save(path)
    return sheetim.size


if __name__ == "__main__":
    SP = os.path.dirname(os.path.abspath(__file__))
    print("true(실기 크기) ", sheet(os.path.join(SP, "2026-09-06_R3_팔짱후보_실크기.png"),
                                CANDS, PPU_TRUE, 6, 1.0, DESKTOP))
    print("zoom(6배)      ", sheet(os.path.join(SP, "2026-09-06_R3_팔짱후보_확대.png"),
                                CANDS, PPU_TRUE * 4, 3, 1.0, PAPER))
    print("halo 시안      ", sheet(os.path.join(SP, "2026-09-06_R3_헤일로_비용시안.png"),
                                CANDS[:4], PPU_TRUE * 4, 3, 1.0, DESKTOP, halo=ARM_W * 0.35))
    tmp = os.path.join(SP, "_tmp_tile.png")
    if os.path.exists(tmp):
        os.remove(tmp)
