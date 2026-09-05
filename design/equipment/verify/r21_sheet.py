# -*- coding: utf-8 -*-
"""R21 시트 — r21_sheet.png((나) 4 + 머리카락 6: [카드 44 | 88] + 착용 4배경 + 추가 칸(판초 웅크리기 / 머리카락 야구모자 착용) + E + 모자 자홍 대조 띠) ·
r21_card_sheet.png(FX/PET 12: [44 | 88] + 현행 몸 도형 대조).    python3 r21_sheet.py   (Chrome 불필요)"""
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig, appearance
import r16_model as M16
import r16_raster as RZ
import r16_worn_sheet as S16
import r17_model as M17
import r17_sheet as S17
import r17d_capes as D17
import r19_model as M19
import r19_sheet as S19
import palette_model as PM
import r21_model as M

WORN_W, WORN_H, R_PX, GAP = S16.WORN_W, S16.WORN_H, S16.R_PX, S16.GAP
HEAD_CX, HEAD_CY = S17.HEAD_CX, S17.HEAD_CY
NOTE_W = 340
CAP_COVER_Y = 0.06          # 프로덕션 HatCoverLocalY(야구모자) — items.COVER["야구모자"]

# ---- 커버선 절단(프로덕션 AppendClippedBelowCover 와 같은 규칙: y > cover 를 잘라낸다) ----
def _cross(a, b, y):
    t = (y - a[1]) / (b[1] - a[1]); return (a[0] + (b[0] - a[0]) * t, y)
def clip_loop(pts, y):
    out = []; n = len(pts)
    for i in range(n):
        cur, nxt = pts[i], pts[(i + 1) % n]; ci, ni = cur[1] <= y, nxt[1] <= y
        if ci: out.append(cur)
        if ci != ni: out.append(_cross(cur, nxt, y))
    return out
def clip_polyline(pts, y):
    runs = []; run = []
    for i, p in enumerate(pts):
        if p[1] <= y:
            if not run and i > 0 and pts[i - 1][1] > y: run.append(_cross(pts[i - 1], p, y))
            run.append(p)
        else:
            if run:
                run.append(_cross(pts[i - 1], p, y)); runs.append(run); run = []
    if run: runs.append(run)
    return [r for r in runs if len(r) >= 2]

def draw_pieces(cv, kind, pieces, to_px, width_px, surface, ink_hex, layer=None, cover=None):
    for p in pieces:
        if layer is not None and p.layer != layer: continue
        fill, fa, line = M.resolve(M.ITEM_ID[kind], p.crole, surface, ink_hex)
        la = M.line_alpha(p)
        polys = [(p.pts, p.loop)]
        if cover is not None:
            polys = [(clip_loop(p.pts, cover), True)] if p.loop else [(r, False) for r in clip_polyline(p.pts, cover)]
        for pts, loop in polys:
            if len(pts) < 2: continue
            px = [to_px(q) for q in pts]
            if p.filled and fill is not None and len(px) >= 3: cv.fill(px, RZ._rgb(fill), alpha=fa)
            if line is not None: cv.stroke(px, width_px(p), RZ._rgb(line), la, loop)

def draw_r19(cv, kind, to_px, width_px, ink_hex, layer):
    """R19 정본(인계본 16종) 조각 — 머리카락 + 모자 조합 칸용."""
    for p in M19.WORN[kind]:
        if p.layer != layer or p.conditional: continue
        fill, fa, line = M19.resolve(kind, p.crole, "body", ink_hex)
        px = [to_px(q) for q in p.pts]
        if p.filled and fill is not None: cv.fill(px, RZ._rgb(fill), alpha=fa)
        if line is not None: cv.stroke(px, width_px(p), RZ._rgb(line), M19.line_alpha(p), p.loop)

def render_body(kind, w, h, bg, ink, r_px=R_PX, scale=M16.SHIP, crouch=False, with_cap=False, figure="ours"):
    cv = RZ.Canvas(w, h, bg)
    off = M17.crouch_offset(1.0) if crouch else 0.0
    cy = HEAD_CY - 60 if crouch else HEAD_CY
    to_px = lambda p: (HEAD_CX + p[0] * r_px, cy - (p[1] + off) * r_px)
    wpx = lambda p: p.width_R(scale) * r_px
    ps = M.BODY[kind]; ink_hex = S19.hexs(ink)
    cover = CAP_COVER_Y if with_cap else None
    draw_pieces(cv, kind, ps, to_px, wpx, "body", ink_hex, layer="back", cover=cover)
    if with_cap: draw_r19(cv, "clothhat", to_px, wpx, ink_hex, "back")
    if figure == "ours" and not crouch: S16.draw_our_figure(cv, HEAD_CX, cy, r_px, ink)
    elif figure == "ours" and crouch: D17.draw_crouch_figure(cv, HEAD_CX, cy, r_px, ink, 1.0)
    elif figure == "magenta": cv.disc(HEAD_CX, cy, M16.HEAD_OUTER_R * r_px, S19.MAGENTA)
    draw_pieces(cv, kind, ps, to_px, wpx, "body", ink_hex, layer="front", cover=cover)
    if with_cap: draw_r19(cv, "clothhat", to_px, wpx, ink_hex, "front")
    im = cv.done()
    if crouch:
        d = ImageDraw.Draw(im); fy = cy - M17.ANKLE_Y * r_px
        for x in range(0, w, 8): d.line([(x, fy), (x + 4, fy)], fill=(200, 40, 40), width=1)
    return im

def render_card(kind, cell):
    cv = RZ.Canvas(cell, cell, S19.CARD_BG)
    k = cell / 64.0
    to_px = lambda p: (p[0] * k, p[1] * k)
    wpx = lambda p: max(1.0, 2.2 * cell / 64.0 * p.mult)
    draw_pieces(cv, kind, M.CARD[kind], to_px, wpx, "card", S19.CARD_INK)
    return cv.done()

def render_mirror(kind, cell, r_px=14.0):
    """현행 몸 효과/펫 도형(appearance.py, 선 그림) — 「같은 물건인가」 대조용."""
    cv = RZ.Canvas(cell, cell, (0x2A, 0x2A, 0x2E))
    shapes = M.FXPET_BODY_MIRROR[kind]
    if not shapes:
        return cv.done()
    pts = [q for s in shapes for q in s.pts]; x0, y0, x1, y1 = rig.bounds(pts); cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    to_px = lambda p: (cell / 2 + (p[0] - cx) * r_px, cell / 2 - (p[1] - cy) * r_px)
    it = M.PAL[M.ITEM_ID[kind]]
    for s in shapes:
        col = RZ._rgb(it[5] if s.tone == 1 else (it[4] if it[4].upper() not in PM.INK_MARKS else "#FFFFFF"))
        px = [to_px(q) for q in s.pts]
        if s.filled: cv.fill(px, col)
        cv.stroke(px, max(1.5, rig.W * r_px * 0.6), col, 1.0, s.loop)
    return cv.done()

# ---- 자홍 대조 (R19 재정의판과 같은 자) ----
def occlusion_tests(kind, r_px=R_PX):
    im = render_body(kind, WORN_W, WORN_H, (40, 40, 40), (0, 0, 0), figure="magenta")
    A = np.asarray(im, np.int16); mag = (A[..., 0] > 180) & (A[..., 1] < 90) & (A[..., 2] > 180)
    to_px = lambda p: (HEAD_CX + p[0] * r_px, HEAD_CY - p[1] * r_px)
    def mask_of(layer):
        m = Image.new("L", (WORN_W, WORN_H), 0); dd = ImageDraw.Draw(m)
        for p in M.BODY[kind]:
            if p.layer == layer and p.filled: dd.polygon([to_px(q) for q in p.pts], fill=255)
        return m
    fm = mask_of("front")
    front = np.asarray(fm.filter(ImageFilter.MinFilter(5))) > 0
    front_cover = np.asarray(fm.filter(ImageFilter.MaxFilter(9))) > 0
    back = np.asarray(mask_of("back")) > 0
    head = Image.new("L", (WORN_W, WORN_H), 0); ImageDraw.Draw(head).ellipse(
        [HEAD_CX - M16.HEAD_OUTER_R * r_px + 2, HEAD_CY - M16.HEAD_OUTER_R * r_px + 2, HEAD_CX + M16.HEAD_OUTER_R * r_px - 2, HEAD_CY + M16.HEAD_OUTER_R * r_px - 2], fill=255)
    headm = np.asarray(head) > 0
    a_inside = int((mag & front).sum()); a_area = int(front.sum())
    region = back & headm & ~front_cover; b_area = int(region.sum()); b_mag = int((mag & region).sum())
    # 머리가 모자 밖으로 보이는가(앞층 채움 합집합 밖 ∩ 머리 원반, 착용선 위): 자홍 픽셀 수
    wear = (M.STRAW_FIT if kind == "straw" else M.BERET_FIT)["wear"]
    above = np.zeros_like(headm); above[: int(HEAD_CY - wear * r_px), :] = True
    peek = int((mag & headm & ~front_cover & above).sum())
    return im, a_inside, a_area, b_mag, b_area, peek

def occlusion_strip(width):
    H = 70 + 200 + 50
    im = Image.new("RGB", (width, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 8), "★ H-1 자홍 대조(R19 재정의판) — (a) 앞층 불투명 채움 합집합(2px 침식) 안 자홍 = 0   (b) 뒤층 ∩ 머리 ∖ 앞층 에서 자홍 비율 ≥ 99 %(뒤층이 머리 뒤)   (c) 착용선 위에서 앞층 밖으로 보이는 머리 픽셀", 12, bold=True)
    S16.label(d, (GAP, 26), "자홍 머리 원반 = 출하 반경 1.172 R · R = 32 px. 베레모는 뒤층이 없어 (b) 해당 없음.", 11, (120, 30, 30))
    res = {}; x = GAP
    for k in ("straw", "beret"):
        img, ai, aa, bm, ba, peek = occlusion_tests(k); res[k] = (ai, aa, bm, ba, peek)
        im.paste(img.crop((WORN_W // 2 - 58, 0, WORN_W // 2 + 58, 200)), (x, 48))
        S16.label(d, (x, 250), M.KO[k], 11, bold=True)
        S16.label(d, (x, 264), "(a) 안 %d / %d" % (ai, aa), 11, (150, 30, 30) if ai else (40, 40, 40))
        S16.label(d, (x, 278), ("(b) 획 밖 자홍 %d/%d" % (bm, ba)) if ba else "(b) 뒤층 없음", 11, (40, 40, 40))
        S16.label(d, (x, 292), "(c) 착용선 위 머리 노출 %d px" % peek, 11, (150, 30, 30) if peek else (40, 40, 40))
        x += 116 + 4
    return im, res

NOTES = {
    "browline": "(나) 동그란안경 문법 + 눈썹 바: 바 = M DarkLens 채움 11u(0.37 R, 1-C 최소) + 잉크 윤곽, 가운데 홈이 코다리 · 렌즈 2 = 카드 M2 워시 α0.16 / 몸 GLASS(E-2) · 테·다리 M 선 · 하이라이트 2. E-2 쌍별 최소 0.27(문턱 0.20). 현행 3/4 렌즈 3조각 → 7조각.",
    "straw": "(나) 챙 = 중절모 렌즈꼴(장축 분할: 위 호 뒤층 / 아래 호 앞층) 폭 4.50 R(중절모 4.06) · 관 반폭 16u 로 넓혀 관 혼자 챙 높이의 머리 현을 덮는다(앞층만으로 H-2 통과, 착용선 +0.283) · 띠 M2 윤곽 없음 · H. R20 임시 좌표(3/4 챙) 폐기.",
    "beret": "(나) 정면 원반이 오른쪽으로 처진다 · 띠 5u(1-C 최소) · 꼭지 = 독립선 M ×1.2 · H. H-2: u 0.070 착용선 +0.290 꼭대기 +2.18 폭 3.05 R. 먼 쪽 없음 → 전부 앞층. 현행 3/4 덩어리 2조각 폐기.",
    "poncho": "(나) 망토 무대 문법 변주: 뒤판(뒤층) 곧은 단 −4.33 R · 폭 3.20 R(짧은망토 −5.09/2.14) + 술 4(M2 독립선) + V 앞자락(M2, 칼라 곡선 + V −2.40, 걸쇠 없음) + H. 웅크리기 최대 밑단 −6.79 > 발목.",
    "cowlick": "(다) 정면 덩어리(톱니 실루엣) + 삐침(M2 사다리꼴, 끝 1.74 R) + 결(잉크 α0.45) + H. 모자 커버선 +0.06 아래엔 구레나룻만 남는다.",
    "neat": "(다) 늘어진 머리(뒤층, 어깨 −2.05 R) + 덩어리(앞층) + 가르마(M2 0.36 R, 윤곽 없음) + 결 + H. 모자를 써도 뒤층 머리는 얼굴 옆에 남는다.",
    "curly": "(다) 물결 실루엣 덩어리(옆머리 −0.55 R) + 컬(M2 8각, 오른 옆) + 결 + H.",
    "bald": "(다) 「없는 것」의 표식: 귀 옆 머리 2(M) + 정수리 한 가닥(M2 독립선) + H. 머리 링에 걸친다(부착 = 원반 접촉).",
    "bowl": "(다) 귀를 덮고 −0.55 R 에서 곧게 자른 단발 + 앞머리 띠(M2 +0.40~+0.84 R) + 결 + H. 눈동자 침범 0.",
    "ponytail": "(다) 덩어리(앞층) + 묶음(뒤층 — 정수리 오른쪽에서 솟아(1.73 R) 뒤로 떨어진다) + 머리끈(M2 r 0.19 R, 윤곽 없음) + 결 + H.",
}

def build_sheet():
    kinds = M.ORDER_NA + M.ORDER_HAIR
    ncol = 5
    W = GAP + 44 + 8 + 88 + 12 + ncol * (WORN_W + GAP) + NOTE_W + GAP
    HEADER = 118; ROW = WORN_H + 34
    strip, res = occlusion_strip(W - 2 * GAP)
    H = HEADER + ROW * len(kinds) + GAP + strip.height + GAP
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 10), "R21 (나)군 4종 재저작 + (다)군 머리카락 6 창작 — [카드 44 | 88] + 착용 4배경(밝은/검은 잉크 · 어두운/검은 · 어두운/흰 · 밝은/흰) + 추가 칸(판초: 웅크리기 최대 / 머리카락: 야구모자(R19) 착용 · 커버선 +0.06 절단) + E", 15, bold=True)
    S16.label(d, (GAP, 34), "규칙 = R20 그대로: 채움 M/M2 불투명 + 잉크 윤곽 · 독립선 재질색 · 채움 위 낱선 잉크 α · 하이라이트 흰 α0.42 ×0.75 · 1pt 하한 · H-1 2층 / H-2(모자) · E-2(뿔테 GLASS) · B-1~B-4(판초) · 머리카락 정면 기하(앞층 = SortHair, 뒤층 = SortBack). 등급색 0.", 12, (70, 70, 70))
    S16.label(d, (GAP, 52), "카드 = 64u 아이콘 단위(인계본과 같은 프레이밍: HEAD 70 / EYES 48 / BACK 88 슬롯 박스 · 머리카락 1 R = 16u). 모자 몸 = 카드 × HAT_FIT(u, dy). R = %.0f px · 획 1pt 하한(출하 0.75)." % R_PX, 12, (70, 70, 70))
    S16.label(d, (GAP, 70), "★ 오프라인 래스터. 최종 판정은 실제 빌드 캡처로만. 1pt 하한의 Windows 1× 실기 미확인. 머리카락 뒤층·FX/PET 카드 문법은 데이터 계약 추가(§14-12) 뒤에야 프로덕션에 존재한다.", 12, (150, 40, 40))
    S16.label(d, (GAP, 88), "머리카락 + 모자 칸: 프로덕션 커버선 상수(야구모자 +0.06 R) 그대로 자른 뒤 R19 야구모자(착용선 +0.29)를 얹었다 — 커버선을 착용선으로 올릴지는 §14-12 미확인.", 12, (150, 40, 40))
    x_card = GAP; x_worn0 = GAP + 44 + 8 + 88 + 12
    for i, k in enumerate(kinds):
        y = HEADER + ROW * i; it = M.PAL[M.ITEM_ID[k]]
        S16.label(d, (GAP, y), "%s  %s / %s   M %s(%s) · M2 %s(%s)" % (M.KO[k], M.ITEM_ID[k], M.SLOT[k], it[4], PM.name_of(it[4]), it[5], PM.name_of(it[5])), 13, bold=True)
        y0 = y + 22
        im.paste(render_card(k, 44), (x_card, y0)); im.paste(render_card(k, 88), (x_card + 52, y0))
        x = x_worn0
        for lab, bg, ink in S19.VARIANTS:
            im.paste(render_body(k, WORN_W, WORN_H, bg, ink), (x, y0)); S16.label(d, (x + 4, y0 + 2), lab, 11, (120, 120, 120), bold=True); x += WORN_W + GAP
        if k == "poncho":
            im.paste(render_body(k, WORN_W, WORN_H, S19.BG_LIGHT, S19.INK_B, crouch=True), (x, y0)); S16.label(d, (x + 4, y0 + 2), "웅크리기 최대", 11, (120, 120, 120), bold=True)
        elif k in M.HAIR:
            im.paste(render_body(k, WORN_W, WORN_H, S19.BG_LIGHT, S19.INK_B, with_cap=True), (x, y0)); S16.label(d, (x + 4, y0 + 2), "야구모자 착용(커버선 +0.06)", 11, (120, 120, 120), bold=True)
        else:
            ImageDraw.Draw(im).rectangle([x, y0, x + WORN_W, y0 + WORN_H], fill=(238, 238, 236))
        x += WORN_W + GAP
        ps = M.BODY[k]
        head = ["조각 %d (뒤층 %d · H %d · M2 %d)" % (len(ps), sum(1 for p in ps if p.layer == "back"), sum(1 for p in ps if p.crole[1] == "W"), sum(1 for p in ps if p.crole[0] == "M2" or p.crole[1] == "M2")), ""]
        RZ.text_block(d, (x, y0), head + RZ.wrap(NOTES[k], 26, 31), size=11, color=(50, 50, 50), lh=15)
    im.paste(strip, (GAP, HEADER + ROW * len(kinds) + GAP))
    return im, res

CARD_NOTES = {
    "fx_none": "빈 고리 + 빗금(잉크 선) — 「없음」. P<4 예외(사유 §14-12).", "fx_footprint": "발바닥(M InkTone) + 엄지 + 발 허리 주름(잉크 α0.5) + H. 몸 효과(점)는 그대로.",
    "fx_sparkle": "큰 별(M Gold) + 작은 별(M2 GoldLight, 윤곽 없음) + 빛 점 + H.", "fx_dust": "먼지구름(M InkDimTone) + 작은 뭉치 2(M2 InkTone) + H.",
    "fx_bubble": "큰 방울(M Blue) + 작은 방울 2(M2) + 긴 광택(방울의 정체).", "fx_leaf": "잎(M TintNeck) + 잎맥(잉크 α0.45) + 잎자루(M2 Gold 독립선) + H.",
    "pet_ball": "공(M Toy) + 솔기 2(M2 Paper 독립선 — 구의 큰 원) + H.", "pet_balloon": "풍선(M Toy) + 매듭(M2) + 줄(M2 독립선) + H.",
    "pet_cursor": "화살표(M Blue) + 클릭 빛 2(M2 독립선) + H.", "pet_mini": "잉크 표식 — 머리(카드 잉크 채움) + 획 ×1.4 4개. H 없음(잉크 표식 예외).",
    "pet_plane": "윗날개(M Paper) + 아랫날개·용골(M2 Silver) + 접은 선(잉크 α0.4) + H.", "pet_snail": "발·머리(M2 Ivory) + 껍데기(M Wool) + 나선 심(M2 윤곽 없음) + 더듬이 2 + H.",
}
def build_card_sheet():
    cols = 6; rows = 2; cw = 44 + 8 + 88 + 8 + 88 + 24; ch = 88 + 40 + 60
    W = GAP + cols * cw; H = 70 + rows * ch
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 8), "R21 카드 시트 — (다) FX 6 · PET 6 × [44px 1× | 88px 2× | 현행 몸 도형(appearance.py 거울, 선 그림 — 같은 물건인가 대조)] · 카드 문법: M 채움 + 카드 잉크 #E8E2D6 윤곽 + 하이라이트 · 바탕 #15181E · 등급색 0", 13, bold=True)
    S16.label(d, (GAP, 28), "몸 효과·펫 본체는 손대지 않았다(카드만). 잉크 표식 4종(없음·발자국·먼지·리틀스틱메이트)은 .asset 의 InkTone/InkDimTone 값을 카드 채움으로 쓴다(몸에서는 유저 잉크). 카드 규칙(w = 2.2u): 1-A/1-C/자기교차 전부 통과(r21_model.out.txt §6).", 11, (70, 70, 70))
    S16.label(d, (GAP, 44), "★ 오프라인 래스터. 최종 판정은 실제 빌드 캡처로만.", 11, (150, 40, 40))
    for i, k in enumerate(M.ORDER_FXPET):
        x = GAP + (i % cols) * cw; y = 70 + (i // cols) * ch
        it = M.PAL[M.ITEM_ID[k]]
        S16.label(d, (x, y), "%s  %s" % (M.KO[k], M.ITEM_ID[k]), 11, bold=True)
        im.paste(render_card(k, 44), (x, y + 16)); im.paste(render_card(k, 88), (x + 52, y + 16)); im.paste(render_mirror(k, 88), (x + 52 + 96, y + 16))
        S16.label(d, (x + 52 + 96, y + 16 + 90), "현행 몸", 9, (120, 120, 120))
        RZ.text_block(d, (x, y + 16 + 92 + 10), RZ.wrap(CARD_NOTES[k], 28, 34), size=10, color=(50, 50, 50), lh=13)
    return im

def main():
    sheet, res = build_sheet()
    for k, (ai, aa, bm, ba, peek) in res.items():
        print("occlusion %-6s (a) 앞층 안 자홍 %d/%d  (b) 뒤층∩머리∖앞층 자홍 %d/%d  (c) 착용선 위 머리 노출 %d px" % (k, ai, aa, bm, ba, peek))
    sheet.save(os.path.join(HERE, "r21_sheet.png")); print("wrote r21_sheet.png %dx%d" % sheet.size)
    cs = build_card_sheet(); cs.save(os.path.join(HERE, "r21_card_sheet.png")); print("wrote r21_card_sheet.png %dx%d" % cs.size)

if __name__ == "__main__":
    main()
