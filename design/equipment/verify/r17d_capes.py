# -*- coding: utf-8 -*-
"""R17d — 망토 2행 시트 r17d_capes.png: 열 C | C* | D′ | D″ | 웅크리기(최대 깊이, 클램프 없음, 발목선 표시) | E.
    python3 r17d_capes.py     # ★ Chrome 필요
"""
import math, os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig
import r16_model as M16
import r16_raster as RZ
import r16_worn_sheet as S16
import r17_model as M
import r17_sheet as S17

WORN_W, WORN_H, R_PX, GAP = S16.WORN_W, S16.WORN_H, S16.R_PX, S16.GAP
CROUCH_H = 400
NOTE_W = 360

def draw_crouch_figure(cv, ox, oy, r_px, ink, amount=1.0):
    """웅크리기 자세(LandingCrouch 최심): 몸(머리·몸통·팔·컨테이너)은 crouch_offset 만큼 내려가고 다리는 각도로 굽는다."""
    off = M.crouch_offset(amount)
    P = lambda p: (ox + p[0] * r_px, oy - p[1] * r_px)
    hip = (0.0, rig.HIP_R + off)
    for sign, key in ((+1, "front"), (-1, "rear")):
        hipD, kneeD = M.CROUCH[key]
        hip0 = M.IDLE_SPREAD if sign > 0 else -M.IDLE_SPREAD
        h = hip0 + (hipD - hip0) * amount; k = M.IDLE_KNEE + (kneeD - M.IDLE_KNEE) * amount
        knee = (hip[0] + M.LEG_UP * math.sin(math.radians(h)), hip[1] - M.LEG_UP * math.cos(math.radians(h)))
        foot = (knee[0] + M.LEG_LO * math.sin(math.radians(h - k)), knee[1] - M.LEG_LO * math.cos(math.radians(h - k)))
        cv.line(P(hip), P(knee), M.W_LEG_R * r_px, ink); cv.line(P(knee), P(foot), M.W_LEG_R * r_px, ink)
    cv.line(P((0, -1.0 + off)), P(hip), M16.W_TORSO_R * r_px, ink)
    import r13_bodyocclusion as B
    for a, b in B.ARMS: cv.line(P((a[0], a[1] + off)), P((b[0], b[1] + off)), M16.W_ARM_R * r_px, ink)
    cv.disc(ox, oy - off * r_px, M16.HEAD_OUTER_R * r_px, ink)
    return off

def render_crouch(kind, w, h, head_cx, head_cy, r_px):
    """출하 화면 + 웅크리기 최대. 망토는 컨테이너와 함께 off 만큼 내려간다(클램프 없음). 발목선(바닥) = 빨간 점선."""
    cv = RZ.Canvas(w, h, S16.DESK_BG)
    off = M.crouch_offset(1.0)
    to_px = lambda p: (head_cx + p[0] * r_px, head_cy - (p[1] + off) * r_px)
    wpx = lambda p: p.width_R(M16.SHIP) * r_px
    ps = M.WORN[kind]
    S17.draw_pieces(cv, ps, to_px, wpx, M.PALETTE_R17, S16.INK_DARK, layer="back")
    draw_crouch_figure(cv, head_cx, head_cy, r_px, S16.INK_DARK, 1.0)
    S17.draw_pieces(cv, ps, to_px, wpx, M.PALETTE_R17, S16.INK_DARK, layer="front")
    im = cv.done(); d = ImageDraw.Draw(im)
    fy = head_cy - M.ANKLE_Y * r_px
    for x in range(0, w, 8): d.line([(x, fy), (x + 4, fy)], fill=(200, 40, 40), width=1)
    S16.label(d, (4, min(h - 14, fy + 2)), "발목선(바닥) %.2f R" % M.ANKLE_Y, 10, (200, 40, 40))
    return im, off

def main():
    kinds = ["shortcape", "longcape"]
    worn_c = S16.render_handoff_worn(R_PX, tag="c17d")
    worn_cf = S16.render_handoff_worn(R_PX, faithful=True, tag="cf17d")
    W = GAP + 5 * (WORN_W + GAP) + NOTE_W + GAP
    HEADER = 118; ROW = WORN_H + 34
    H = HEADER + ROW * 2 + GAP
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 10), "R17d — 망토 길이 2단: 짧은망토 밑단 = 현행 긴망토 밑단(%.2f R) · 긴망토 밑단 = 발목(%.2f R). 열 C | C* | D′ | D″ | 웅크리기(최대, 클램프 없음) | E" % (M.HEM_SHORT_NEW, M.HEM_LONG_NEW), 15, bold=True)
    S16.label(d, (GAP, 34), "규칙 B-1(길이 2단) · B-2(밑단 폭 = 부착 폭 1.0 R + 플레어 %.3f R/R × 길이 — 인계본 긴망토의 옆선 기울기 유지). 뒤판은 SortBack −1 이라 다리 뒤. 카드는 인계본 그대로." % M.FLARE, 12, (70, 70, 70))
    S16.label(d, (GAP, 52), "★ 웅크리기 칸: LandingCrouch 최심(앞다리 82°/126°, 뒷다리 −40°/55°)에서 몸(컨테이너)이 %.2f R 내려간다(ComputeFootGroundingOffset). 긴망토 밑단은 발목선 아래로 %.2f R(%.1f pt @0.75) 관통 → 처방: HemSway 정점 루프에 밑단 y 하한(클램프) 한 줄." % (
        -M.crouch_offset(1.0), M.ANKLE_Y - (M.HEM_LONG_NEW + M.crouch_offset(1.0)), (M.ANKLE_Y - (M.HEM_LONG_NEW + M.crouch_offset(1.0))) * M16.head_r_pt(M16.SHIP)), 12, (150, 40, 40))
    S16.label(d, (GAP, 70), "펄럭임 진폭은 R×0.16 고정(길이 비례 아님) — 밑단 정점 17점에 위상 걸음이 걸려 물결이 촘촘해진다(design-motion 영향 있음). 랙돌은 액세서리 숨김. GETUP 리프트가 커지는지는 실기 미확인.", 12, (70, 70, 70))
    S16.label(d, (GAP, 88), "★ 오프라인 래스터. 최종 판정은 실제 빌드 캡처로만. 1pt 하한의 Windows 1× 실기 미확인. 강조 등급색은 일반 #8A8F98 가정.", 12, (150, 40, 40))
    xs = [GAP + i * (WORN_W + GAP) for i in range(5)]; xE = xs[4] + WORN_W + GAP
    for i, k in enumerate(kinds):
        y = HEADER + ROW * i; S16.label(d, (xs[0], y), "%s  %s / BACK" % (M.KO[k], k), 14, bold=True); y0 = y + 22
        im.paste(worn_c[k], (xs[0], y0)); im.paste(worn_cf[k], (xs[1], y0))
        im.paste(S17.render_ours(k, WORN_W, WORN_H, S17.HEAD_CX, S17.HEAD_CY, R_PX, figure="handoff", stage_x=S17.STAGE_X), (xs[2], y0))
        im.paste(S17.render_ours(k, WORN_W, WORN_H, S17.HEAD_CX, S17.HEAD_CY, R_PX, figure="ours", bg=S16.DESK_BG, ink=S16.INK_DARK), (xs[3], y0))
        cr, off = render_crouch(k, WORN_W, WORN_H, S17.HEAD_CX, S17.HEAD_CY - 60, R_PX)
        im.paste(cr, (xs[4], y0))
        hem = M.HEM_SHORT_NEW if k == "shortcape" else M.HEM_LONG_NEW
        pen = M.ANKLE_Y - (hem + off)
        note = (("짧은망토 := 현행 긴망토 뒤판 그대로. 밑단 %.3f R · 폭 %.3f R · 길이 %.3f R. 웅크리기 최대에서 밑단 %.2f R — 발목선 위 %.2f R(관통 없음)."
                 % (M.HEM_SHORT_NEW, M.HEM_W_LONG_OLD, M.CAPE_TOP_Y - M.HEM_SHORT_NEW, hem + off, -pen)) if k == "shortcape" else
                ("긴망토: 위 고정 · 세로 ×%.3f · 밑단 폭 ×%.3f → 밑단 %.3f R · 폭 %.3f R · 길이 %.3f R. 다리 밖으로 각 %.2f R 보인다. 웅크리기 최대에서 밑단 %.2f R → 발목선 아래 %.2f R(%.1f pt) 관통 → 클램프 처방(코더)."
                 % (M.KY_LONG, M.KW_LONG, M.HEM_LONG_NEW, M.HEM_W_LONG_NEW, M.LEN_LONG_NEW, M.HEM_W_LONG_NEW / 2 - 0.757, hem + off, pen, pen * M16.head_r_pt(M16.SHIP))))
        RZ.text_block(d, (xE, y0), S17.diff_lines(k) + [""] + RZ.wrap(note, 28, 33), size=11, color=(50, 50, 50), lh=15)
        for x, lab in zip(xs, ("C", "C*", "D′", "D″", "웅크리기")):
            S16.label(d, (x + 4, y0 + 2), lab, 12, (200, 200, 200) if lab != "웅크리기" else (60, 60, 60), bold=True)
    im.save(os.path.join(HERE, "r17d_capes.png")); print("wrote r17d_capes.png %dx%d" % im.size)

if __name__ == "__main__":
    main()
