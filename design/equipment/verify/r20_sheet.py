# -*- coding: utf-8 -*-
"""R20 시트 — (가)군 5종: 카드(44px + 88px, 바탕 #15181E · 선 #E8E2D6) + 착용 4배경(밝은/검은 잉크 · 어두운/검은 · 어두운/흰 · 밝은/흰).
카드 = 현행 카드 경로(AccessoryCardIcon: 봉투 최대변을 셀×0.86 에 맞춤)로 같은 좌표를 그린다 — 인계본 아이콘이 없는 종이라 정면 슬롯 프레이밍이 없다.
    python3 r20_sheet.py"""
import math, os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig
import r16_model as M16
import r16_raster as RZ
import r16_worn_sheet as S16
import r17_sheet as S17
import r19_sheet as S19
import palette_model as PM
import r20_model as M

WORN_W, WORN_H, R_PX, GAP = S16.WORN_W, S16.WORN_H, S16.R_PX, S16.GAP
HEAD_CX, HEAD_CY = S17.HEAD_CX, S17.HEAD_CY
NOTE_W = 330

def draw_pieces(cv, kind, pieces, to_px, width_px, surface, ink_hex, layer=None):
    for p in pieces:
        if layer is not None and p.layer != layer: continue
        fill, fa, line = M.resolve(kind, p.crole, surface, ink_hex)
        pts = [to_px(q) for q in p.pts]
        if p.filled and fill is not None: cv.fill(pts, RZ._rgb(fill), alpha=fa)
        if line is not None: cv.stroke(pts, width_px(p), RZ._rgb(line), M16.HI_ALPHA if p.crole[1] == "W" else 1.0, p.loop)

def render_body(kind, w, h, bg, ink, r_px=R_PX, scale=M16.SHIP):
    cv = RZ.Canvas(w, h, bg)
    to_px = lambda p: (HEAD_CX + p[0] * r_px, HEAD_CY - p[1] * r_px)
    wpx = lambda p: p.width_R(scale) * r_px
    ps = M.GA[kind]; ink_hex = S19.hexs(ink)
    draw_pieces(cv, kind, ps, to_px, wpx, "body", ink_hex, layer="back")
    S16.draw_our_figure(cv, HEAD_CX, HEAD_CY, r_px, ink)
    draw_pieces(cv, kind, ps, to_px, wpx, "body", ink_hex, layer="front")
    return cv.done()

def render_card(kind, cell):
    cv = RZ.Canvas(cell, cell, S19.CARD_BG)
    pts = [q for p in M.GA[kind] if p.src != "EYE" for q in p.pts]
    x0, y0, x1, y1 = rig.bounds(pts); span = max(x1 - x0, y1 - y0); s = cell * 0.86 / span
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    to_px = lambda p: (cell / 2 + (p[0] - cx) * s, cell / 2 - (p[1] - cy) * s)
    wpx = lambda p: max(1.0, 2.2 * cell / 64.0 * p.mult)
    ps = [p for p in M.GA[kind] if p.src != "EYE"]
    draw_pieces(cv, kind, ps, to_px, wpx, "card", S19.CARD_INK)
    return cv.done()

NOTES = {
    "patch": "(가) 안대: 가리개 M Felt + 잉크 윤곽 · 끈 M2 독립선 · 하이라이트 · E-1 반대쪽 눈 r 1pt @(−0.46,+0.107) 조건부(현행 DrawnEye r 0.33 @−0.62 대체). 좌표 0점 변경(눈 제외).",
    "straw": "★ (나)로 재분류 — 임시 좌표: H-2 균일 배율 u 1.190 dy +0.580 → 착용선 +0.29 · 꼭대기 +1.94 · 챙 폭 5.05 R · 관 M Canvas · 띠 M2(윤곽 없음) · 하이라이트. H-1 챙 분할은 첫 판에서 3/4 챙이 찢어져 철회 — 정면 렌즈꼴 챙으로 재저작 뒤 적용.",
    "pendant": "(가) 펜던트: 목줄 M Silver 독립선 · 돌 M2 Gold + 잉크 윤곽 · 하이라이트 · N-1 dy −0.055(목줄 윗변을 머리 원반 밑 0.03 R 아래로).",
    "bandana": "(가) 반다나: 띠 M TintNeck + 잉크 · 자락 M2 NeckDeep + 잉크 · 하이라이트 · N-1 dy −0.175. 자락 한쪽 = 목도리 문법과 같다(3/4 잔재가 아니라 의도).",
    "fairywings": "(가) 요정날개: 깃 2 M Paper + 잉크(뒤층) · 척추 M2 독립선(몸통 뒤에 숨는다) · 하이라이트 · 그룹 α 1.0.",
}

def main():
    kinds = list(M.GA.keys())
    W = GAP + 4 * (WORN_W + GAP) + 44 + 8 + 88 + 12 + NOTE_W + GAP
    HEADER = 100; ROW = WORN_H + 34; H = HEADER + ROW * len(kinds) + GAP
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 10), "R20 (가)군 4종 + 밀짚모자 임시(나) — 규칙만 적용한 좌표: [카드 44 | 88] + 착용 4배경(밝은/검은 잉크 · 어두운/검은 · 어두운/흰 · 밝은/흰) + E", 15, bold=True)
    S16.label(d, (GAP, 34), "현행 도형(거울 items.py)에 인계본 규칙을 적용: 채움 M/M2 불투명 + 잉크 윤곽 · 독립선 재질색 · 하이라이트 흰 α0.42 ×0.75 · 1pt 하한 · H-1/H-2(밀짚모자) · N-1(펜던트·반다나) · E-1(안대) · B-4(요정날개). 등급색 0.", 12, (70, 70, 70))
    S16.label(d, (GAP, 52), "카드는 인계본 아이콘이 없어 현행 카드 경로(봉투 ×0.86 맞춤)로 같은 좌표를 그렸다 — 16종 카드(64u 슬롯 프레이밍)와 크기 관계가 다를 수 있다(미확인). 나·다군은 §14-11 방침만.", 12, (70, 70, 70))
    S16.label(d, (GAP, 70), "★ 오프라인 래스터. 최종 판정은 실제 빌드 캡처로만. 1pt 하한의 Windows 1× 실기 미확인.", 12, (150, 40, 40))
    for i, k in enumerate(kinds):
        y = HEADER + ROW * i; it = PM.BY_KIND.get(k) or M.PAL[M.GA_ID[k]]
        S16.label(d, (GAP, y), "%s  %s / %s   M %s(%s) · M2 %s(%s)" % (M.GA_KO[k], M.GA_ID[k], M.GA_SLOT[k], it[4], PM.name_of(it[4]), it[5], PM.name_of(it[5])), 13, bold=True)
        y0 = y + 22; x = GAP
        im.paste(render_card(k, 44), (x, y0)); im.paste(render_card(k, 88), (x + 52, y0)); x += 44 + 8 + 88 + 12
        for lab, bg, ink in S19.VARIANTS:
            im.paste(render_body(k, WORN_W, WORN_H, bg, ink), (x, y0)); S16.label(d, (x + 4, y0 + 2), lab, 11, (120, 120, 120), bold=True); x += WORN_W + GAP
        RZ.text_block(d, (x, y0), ["조각 %d (뒤층 %d)" % (len(M.GA[k]), sum(1 for p in M.GA[k] if p.layer == "back")), ""] + RZ.wrap(NOTES[k], 26, 31), size=11, color=(50, 50, 50), lh=15)
    im.save(os.path.join(HERE, "r20_sheet.png")); print("wrote r20_sheet.png %dx%d" % im.size)

if __name__ == "__main__":
    main()
