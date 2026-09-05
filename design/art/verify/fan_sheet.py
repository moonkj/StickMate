# -*- coding: utf-8 -*-
"""우클릭 부채꼴 — 오프라인 시트 (design-art R14). fan_menu_sheet.png · fan_menu_truesize.png
행 = 바탕(밝은 #F2F2F2 · 어두운 #1E1E1E · 원판 근처 회색 #262A31 · 2톤 최악 회색 #888888 · 사진풍 그라디언트) × 열 = 현행(검은 잉크) | 제안(검은 잉크) | 제안(흰 잉크) | 제안 상태(호버·무장·배지)
★ 기하는 프로덕션 상수(Ø44 · 궤도 111 · 위성 168 · 간격 30° · 기호 24/획 2)를 그대로 쓴다. 우클릭 앵커·기준각은 ux-widgets 소관 — 여기서는 머리 중심·θ₀ 90° 가정.
★ 오프라인 래스터(둥근 캡). 최종 판정은 실기 캡처.
"""
import os, sys, math
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
EQ = os.path.abspath(os.path.join(HERE, "../../equipment/verify")); sys.path.insert(0, EQ)
import r16_raster as RZ, r16_worn_sheet as S16, r16_model as M16
import fan_menu as F

PX = 2.0                      # 1pt = 2px (Retina 2×)
D_BTN, ORBIT, SAT, STEP, BOX, PEN = 44.0, 111.0, 168.0, 30.0, 24.0, 2.0
RING_PT = 2.0                 # 제안 링 폭
HEAD_R_PT = M16.head_r_pt(M16.SHIP)   # 5.816pt @0.75
NAMES = ["집중 모드", "캐릭터", "오늘 할일", "행동", "앱 종료"]
T = F.TOK

def slot_pos(i, theta0=90.0):
    if i == 4: ang, r = theta0, SAT
    else: ang, r = theta0 + (1.5 - i) * STEP, ORBIT
    return (math.cos(math.radians(ang)) * r, math.sin(math.radians(ang)) * r)

def gradient_bg(w, h):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = (xx / w * 0.6 + yy / h * 0.4)
    c0, c1 = np.array((234, 214, 178), np.float32), np.array((38, 52, 84), np.float32)
    img = c0 + (c1 - c0) * t[..., None]
    # 사진풍 얼룩
    img += 18 * np.sin(xx / 23.0) * np.cos(yy / 31.0)[..., None] if False else 0
    return np.clip(img, 0, 255).astype(np.uint8)

def stroke(cv, pts, wpt, col, alpha=1.0, loop=False):
    cv.stroke(pts, wpt * PX, RZ._rgb(col), alpha, loop)

def seg(cx, cy, length, angle_deg, center):
    a = math.radians(angle_deg); hx_, hy_ = math.cos(a) * length / 2, math.sin(a) * length / 2
    return [(cx + (center[0] - hx_) * PX, cy - (center[1] - hy_) * PX), (cx + (center[0] + hx_) * PX, cy - (center[1] + hy_) * PX)]

def circle_pts(cx, cy, r_pt, n=48, a0=0.0, a1=360.0):
    return [(cx + math.cos(math.radians(a0 + (a1 - a0) * i / n)) * r_pt * PX, cy - math.sin(math.radians(a0 + (a1 - a0) * i / n)) * r_pt * PX) for i in range(n + 1)]

def glyph(cv, i, cx, cy, col, accent):
    P = lambda x, y: (cx + x * PX, cy - y * PX)
    if i == 0:   # 스톱워치
        stroke(cv, circle_pts(cx, cy, 10, 64)[:-1], PEN, col, loop=True)
        stroke(cv, seg(cx, cy, 6, 0, (0, 11.5)), 4, col)
        stroke(cv, seg(cx, cy, 6.5, 90, (0, 3.25)), PEN, col)
        stroke(cv, seg(cx, cy, 5, -30, (math.cos(math.radians(-30)) * 2.5, math.sin(math.radians(-30)) * 2.5)), PEN, col)
    elif i == 1: # 스틱맨
        stroke(cv, circle_pts(cx, cy - 8 * PX, 3.5, 32)[:-1], 1.8, col, loop=True)
        stroke(cv, seg(cx, cy, 9, 90, (0, 0)), 1.8, col)
        for ang, org, ln in ((-140, (0, 3.5), 6), (-40, (0, 3.5), 6), (-106, (0, -4.5), 7), (-74, (0, -4.5), 7)):
            c = (org[0] + math.cos(math.radians(ang)) * ln / 2, org[1] + math.sin(math.radians(ang)) * ln / 2)
            stroke(cv, seg(cx, cy, ln, ang, c), 1.8, col)
    elif i == 2: # 체크리스트
        for y in (7, 0, -7): stroke(cv, seg(cx, cy, 9, 0, (5.5, y)), PEN, col)
        stroke(cv, seg(cx, cy, 9, 0, (5.5, -7)), 1.4, col)
        for y in (7, 0):
            b = [P(-6 - 2.25, y - 2.25), P(-6 + 2.25, y - 2.25), P(-6 + 2.25, y + 2.25), P(-6 - 2.25, y + 2.25)]
            stroke(cv, b, 1.0, col, loop=True)
        v = (-7, -8)
        stroke(cv, seg(cx, cy, 3.2, 135, (v[0] + math.cos(math.radians(135)) * 1.6, v[1] + math.sin(math.radians(135)) * 1.6)), 1.6, accent)
        stroke(cv, seg(cx, cy, 6, 45, (v[0] + math.cos(math.radians(45)) * 3, v[1] + math.sin(math.radians(45)) * 3)), 1.6, accent)
    elif i == 3: # 확성기
        stroke(cv, seg(cx, cy, 13, 13, (-1.6, 5.0)), PEN, col); stroke(cv, seg(cx, cy, 13, -13, (-1.6, -5.0)), PEN, col)
        stroke(cv, seg(cx, cy, 5.6, 90, (-8.4, 0)), PEN, col); stroke(cv, seg(cx, cy, 11.6, 90, (5.0, 0)), PEN, col)
        stroke(cv, seg(cx, cy, 4.6, 30, (9.6, 3.4)), 1.6, col); stroke(cv, seg(cx, cy, 4.6, -30, (9.6, -3.4)), 1.6, col)
    else:        # 전원
        stroke(cv, circle_pts(cx, cy, 10, 64, 115, 425), PEN, col)
        stroke(cv, seg(cx, cy, 11, 90, (0, 5.5)), PEN, col)

def button(cv, cx, cy, i, style, ink_hex, state="idle"):
    """style: 'now' | 'new'. state: idle | hover | armed(위성) | badge(할일)"""
    face = T["CardSurface"]; sym = T["TextPrimary"]; acc = T["Accent"]
    if style == "now":
        cv.disc(cx, cy, D_BTN / 2 * PX, RZ._rgb(face))
        stroke(cv, circle_pts(cx, cy, D_BTN / 2 - 0.6, 96)[:-1], 1.2, "#FFFFFF", 0.10, loop=True)
        if state == "hover":
            cv.disc(cx, cy, D_BTN / 2 * PX, RZ._rgb(acc), alpha=0.14); sym = acc
        if state == "armed":
            cv.disc(cx, cy, D_BTN / 2 * PX, RZ._rgb(acc), alpha=0.14); sym = acc
    else:
        hover_face = F.flatten(acc, 0.14, face)
        f = hover_face if state in ("hover", "armed") else face
        cv.disc(cx, cy, (D_BTN / 2 - RING_PT) * PX + 0.5, RZ._rgb(f))
        stroke(cv, circle_pts(cx, cy, D_BTN / 2 - RING_PT / 2, 96)[:-1], RING_PT, T["TextPrimary"], loop=True)
        if state in ("hover", "armed"): sym = acc
    glyph(cv, i, cx, cy, sym, acc)
    if state == "armed":   # 카운트다운 링(전원 원호 위, 60% 남음)
        stroke(cv, circle_pts(cx, cy, 10, 64, 90, 90 - 0.6 * 360), PEN, acc)
    if state == "badge":
        bx, by = cx + 15 * PX, cy - 15 * PX
        cv.disc(bx, by, 8 * PX, RZ._rgb(acc))
        fs = int(10 * PX * RZ.SS); d = ImageDraw.Draw(cv.im)
        d.text((bx * RZ.SS - fs * 0.30, by * RZ.SS - fs * 0.62), "3", fill=RZ._rgb(T["OnAccentSolid"]) + (255,), font=RZ.font(fs, bold=True))

def cell(w, h, bg, style, ink_hex, states=None):
    cv = RZ.Canvas(w, h, None)
    if isinstance(bg, str): cv.im.paste(tuple(RZ._rgb(bg)) + (255,), (0, 0, cv.im.width, cv.im.height))
    else: cv.paste_background(bg)
    cx, cy = w / 2.0, h * 0.72
    S16.draw_our_figure(cv, cx, cy, HEAD_R_PT * PX, RZ._rgb(ink_hex))
    for i in range(5):
        x, y = slot_pos(i)
        st = (states or {}).get(i, "idle")
        button(cv, cx + x * PX, cy - y * PX, i, style, ink_hex, st)
    return cv.done()

def main():
    W, H = 560, 560
    bgs = [("밝은 바탕 #F2F2F2", "#F2F2F2"), ("어두운 바탕 #1E1E1E", "#1E1E1E"), ("원판 근처 회색 #262A31", "#262A31"),
           ("2톤 최악 회색 #888888", "#888888"), ("사진풍 그라디언트", gradient_bg(W, H))]
    cols = [("현행 · 검은 잉크", "now", "#111111", None), ("제안 · 검은 잉크", "new", "#111111", None), ("제안 · 흰 잉크", "new", "#FFFFFF", None),
            ("제안 · 상태(호버④ · 무장⑤ · 배지③)", "new", "#111111", {3: "hover", 4: "armed", 2: "badge"})]
    GAP, HEADER, LBL = 10, 96, 22
    im = Image.new("RGB", (GAP + 4 * (W + GAP), HEADER + 5 * (H + LBL + GAP)), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 8), "우클릭 부채꼴 시각 언어 — 현행(원판 CardSurface + 흰 α0.10 테) vs 제안(면 CardSurface Ø40 + 밝은 링 TextPrimary 2pt = 2톤 가장자리). 기하 불변(Ø44·궤도 111·위성 168·30°). design-art R14 2026-09-05", 14, bold=True)
    S16.label(d, (GAP, 32), "각 셀: 캐릭터(출하 0.75, 머리 중심 = 앵커, θ₀ 90° 가정 — 앵커·기준각은 ux-widgets 소관) 위에 5칸. 1pt = 2px. 글리프는 GearRadialMenuWidget 코드 기하 그대로.", 12, (70, 70, 70))
    S16.label(d, (GAP, 50), "★ 오프라인 래스터(둥근 캡). 최종 판정은 실기 캡처. 회색 스윕 보장값: 현행 원판 1.00(배경 #1B1F26 근처) → 제안 2톤 3.87(배경 #888888 근처). 숫자 전문 design/art/fan_menu.out.txt", 12, (150, 40, 40))
    S16.label(d, (GAP, 68), "보는 법: 3행(#262A31)에서 현행 원판이 사라지고 기호만 남는다 · 4행(#888888)이 제안의 최악 — 링과 면 둘 다 3.87 · 검은 잉크 팔다리가 원판에 닿는 자리는 2·5행 현행 열에서 한 덩어리가 된다.", 12, (70, 70, 70))
    for r, (bnm, bg) in enumerate(bgs):
        y0 = HEADER + r * (H + LBL + GAP)
        for c, (cnm, style, ink, states) in enumerate(cols):
            x0 = GAP + c * (W + GAP)
            im.paste(cell(W, H, bg, style, ink, states), (x0, y0 + LBL))
            S16.label(d, (x0, y0 + 4), "%s | %s" % (bnm, cnm), 12, bold=True)
    im.save(os.path.join(HERE, "fan_menu_sheet.png")); print("wrote fan_menu_sheet.png %dx%d" % im.size)
    # ---- 실제 크기(1×): 버튼 5개, 현행 vs 제안, 바탕 3종 ----
    global PX; PX = 1.0
    bgs1 = ["#F2F2F2", "#1E1E1E", "#888888"]
    cw = 56; W1 = 20 + 3 * (5 * cw + 30); H1 = 60 + 2 * (cw + 26)
    ts = Image.new("RGB", (W1, H1), (250, 250, 248)); d = ImageDraw.Draw(ts)
    S16.label(d, (10, 6), "실제 크기 1× (Ø44pt = 44px, 획 2px) — 위: 현행 · 아래: 제안 | 바탕 #F2F2F2 · #1E1E1E · #888888", 12, bold=True)
    S16.label(d, (10, 24), "★ Windows 1× 실기 미확인. 글리프 여유 1.5W 미달 3건(분침·다리·소리선)은 이 크기에서 2px 획 사이 1.5~2.4px 틈이다.", 11, (150, 40, 40))
    for bi, bg in enumerate(bgs1):
        for row, style in enumerate(("now", "new")):
            for i in range(5):
                cv = RZ.Canvas(cw, cw, RZ._rgb(bg))
                button(cv, cw / 2, cw / 2, i, style, "#111111", "idle")
                ts.paste(cv.done(), (10 + bi * (5 * cw + 30) + i * cw, 44 + row * (cw + 26)))
    ts.save(os.path.join(HERE, "fan_menu_truesize.png")); print("wrote fan_menu_truesize.png %dx%d" % ts.size)

if __name__ == "__main__":
    main()
