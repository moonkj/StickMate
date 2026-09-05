# -*- coding: utf-8 -*-
"""R19 시트 — r19_priority_sheet.png(모자 4 → 선글라스·고글 → 외알안경 → 나비넥타이 → 배낭 + 자홍 대조 띠 재정의판) ·
r19_worn_sheet.png(16행: C | 밝은/검은 잉크 | 어두운/검은 | 어두운/흰 | 밝은/흰 | 웅크리기(망토만) | E) · r19_card_sheet.png(44px + 2×).
    python3 r19_sheet.py          # ★ Chrome 필요(C 열)
"""
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig, handoff
import r16_model as M16
import r16_raster as RZ
import r16_worn_sheet as S16
import r17_model as M17
import r17_sheet as S17
import r17d_capes as D17
import palette_model as PM
import r19_model as M

WORN_W, WORN_H, R_PX, GAP = S16.WORN_W, S16.WORN_H, S16.R_PX, S16.GAP
HEAD_CX, HEAD_CY, STAGE_X = S17.HEAD_CX, S17.HEAD_CY, S17.STAGE_X
NOTE_W = 330
BG_LIGHT, BG_DARK = (0xF2, 0xF2, 0xF2), (0x1E, 0x1E, 0x1E)
INK_B, INK_W = (0x11, 0x11, 0x11), (0xFF, 0xFF, 0xFF)
CARD_BG, CARD_INK = (0x15, 0x18, 0x1E), "#E8E2D6"
MAGENTA = (255, 0, 255)
VARIANTS = [("밝은/검은 잉크", BG_LIGHT, INK_B), ("어두운/검은", BG_DARK, INK_B), ("어두운/흰", BG_DARK, INK_W), ("밝은/흰", BG_LIGHT, INK_W)]

def hexs(rgb): return "#%02X%02X%02X" % rgb

def draw_pieces(cv, kind, pieces, to_px, width_px, surface, ink_hex, layer=None, worn_monocle=True):
    for p in pieces:
        if layer is not None and p.layer != layer: continue
        if p.conditional == "monocle worn" and not worn_monocle: continue
        fill, fa, line = M.resolve(kind, p.crole, surface, ink_hex)
        pts = [to_px(q) for q in p.pts]
        if p.filled and fill is not None:
            cv.fill(pts, RZ._rgb(fill), alpha=fa)
        if line is not None:
            cv.stroke(pts, width_px(p), RZ._rgb(line), M.line_alpha(p), p.loop, dash=None)

def render_body(kind, w, h, head_cx, head_cy, r_px, bg, ink, scale=M16.SHIP, figure="ours", crouch=False):
    cv = RZ.Canvas(w, h, bg)
    off = M17.crouch_offset(1.0) if crouch else 0.0
    to_px = lambda p: (head_cx + p[0] * r_px, head_cy - (p[1] + off) * r_px)
    wpx = lambda p: p.width_R(scale) * r_px
    ps = M.WORN[kind]; ink_hex = hexs(ink)
    draw_pieces(cv, kind, ps, to_px, wpx, "body", ink_hex, layer="back")
    if figure == "ours" and not crouch: S16.draw_our_figure(cv, head_cx, head_cy, r_px, ink)
    elif figure == "ours" and crouch: D17.draw_crouch_figure(cv, head_cx, head_cy, r_px, ink, 1.0)
    elif figure == "magenta": cv.disc(head_cx, head_cy, M16.HEAD_OUTER_R * r_px, MAGENTA)
    draw_pieces(cv, kind, ps, to_px, wpx, "body", ink_hex, layer="front")
    im = cv.done()
    if crouch:
        d = ImageDraw.Draw(im); fy = head_cy - M17.ANKLE_Y * r_px
        for x in range(0, w, 8): d.line([(x, fy), (x + 4, fy)], fill=(200, 40, 40), width=1)
    return im

# ---- 카드 ------------------------------------------------------------------------
def card_to_px(kind, cell):
    box, top = handoff.SLOT_BOX[handoff.ITEM_SLOT[kind]]
    u = box / 64.0; k = cell / 64.0
    def f(p):
        xi = p[0] / (u / handoff.HEAD_R_PX) + 32.0
        yi = (handoff.HEAD_CY_PX - p[1] * handoff.HEAD_R_PX - top) / u
        return (xi * k, yi * k)
    return f

def render_card(kind, cell):
    cv = RZ.Canvas(cell, cell, CARD_BG)
    to_px = card_to_px(kind, cell)
    wpx = lambda p: max(1.0, 2.2 * cell / 64.0 * p.mult)
    draw_pieces(cv, kind, M.CARD[kind], to_px, wpx, "card", CARD_INK)
    return cv.done()

def build_card_sheet():
    cols = 8; rows = 2; cw = 44 + 8 + 88 + 24; ch = 88 + 40
    W = GAP + cols * cw; H = 60 + rows * ch
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 8), "R19 카드 시트 — 16종 × [44px 1× | 88px 2×] · 인계본 아이콘 좌표 그대로 · 색 = palette_model.ROLE + R19 정정 · 바탕 #15181E · 선 #E8E2D6 · 등급색 0", 13, bold=True)
    S16.label(d, (GAP, 28), "채움 M/M2 불투명(그라디언트 폐기) · 유리 워시 예외: 동그란안경 α0.16 · 외알안경 α0.20(M2) · 선글라스 렌즈 = 그늘(SH) + 테 M2 · 고글 렌즈 윤곽 없음 · 구슬 M2 윤곽 없음", 11, (70, 70, 70))
    for i, k in enumerate(M.KINDS):
        x = GAP + (i % cols) * cw; y = 60 + (i // cols) * ch
        S16.label(d, (x, y), "%s" % M.KO[k], 11, bold=True)
        im.paste(render_card(k, 44), (x, y + 16)); im.paste(render_card(k, 88), (x + 52, y + 16))
    return im

# ---- 자홍 대조 (재정의) ---------------------------------------------------------------
def occlusion_tests(kind, r_px=R_PX):
    """(a) 앞층 불투명 채움 합집합(2px 침식) 안 자홍 = 0  (b) 뒤층 영역 ∩ 머리 원반 ∖ 앞층 합집합 에서 자홍 비율 ≥ 0.99 (머리가 뒤층 앞에 있다)."""
    im = render_body(kind, WORN_W, WORN_H, HEAD_CX, HEAD_CY, r_px, (40, 40, 40), (0, 0, 0), figure="magenta")
    A = np.asarray(im, np.int16); mag = (A[..., 0] > 180) & (A[..., 1] < 90) & (A[..., 2] > 180)
    to_px = lambda p: (HEAD_CX + p[0] * r_px, HEAD_CY - p[1] * r_px)
    def mask_of(layer):
        m = Image.new("L", (WORN_W, WORN_H), 0); dd = ImageDraw.Draw(m)
        for p in M.WORN[kind]:
            if p.layer == layer and p.filled: dd.polygon([to_px(q) for q in p.pts], fill=255)
        return m
    fm = mask_of("front")
    front = np.asarray(fm.filter(ImageFilter.MinFilter(5))) > 0            # (a) 2px 침식 — AA 가장자리 제외
    # (b) 는 앞층 윤곽(1pt = 5.5px @R32, 반폭 2.75px)까지 「앞층이 덮는 영역」이다 → 4px 팽창한 마스크로 뺀다
    front_cover = np.asarray(fm.filter(ImageFilter.MaxFilter(9))) > 0
    back = np.asarray(mask_of("back")) > 0
    head = Image.new("L", (WORN_W, WORN_H), 0); ImageDraw.Draw(head).ellipse(
        [HEAD_CX - M16.HEAD_OUTER_R * r_px + 2, HEAD_CY - M16.HEAD_OUTER_R * r_px + 2, HEAD_CX + M16.HEAD_OUTER_R * r_px - 2, HEAD_CY + M16.HEAD_OUTER_R * r_px - 2], fill=255)
    headm = np.asarray(head) > 0
    a_inside = int((mag & front).sum()); a_area = int(front.sum())
    region = back & headm & ~front_cover
    b_area = int(region.sum()); b_mag = int((mag & region).sum())
    # 기하만(획 무시): 앞층 다각형 밖 ∩ 머리 ∩ 뒤층 — 「머리가 봉우리 사이로 보이는」 기하적 넓이. 획이 이걸 얼마나 먹는지 같이 적는다.
    front_geo = np.asarray(fm) > 0
    region_geo = back & headm & ~front_geo
    g_area = int(region_geo.sum()); g_mag = int((mag & region_geo).sum())
    return im, a_inside, a_area, b_mag, b_area, g_mag, g_area

def occlusion_strip(width=None):
    W = width or (GAP + 4 * (WORN_W // 2 + 4) + GAP); H = 70 + 200 + 50     # 폭을 시트 폭에 맞춘다(제목이 500px 에서 잘렸다)
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 8), "★ [①] 모자 2층 자홍 대조(재정의) — (a) 앞층 불투명 채움 합집합(2px 침식) 안 자홍 픽셀 = 0   (b) 뒤층 ∩ 머리 원반 ∖ 앞층 에서 자홍 비율 ≥ 99 % = 뒤층이 머리 뒤에 있다", 12, bold=True)
    S16.label(d, (GAP, 26), "합격 = (a) 0 이고 (b) ≥ 99 %(뒤층이 없는 털모자는 (b) 해당 없음). 자홍 머리 원반 = 출하 반경 1.143 R.", 11, (120, 30, 30))
    res = {}; x = GAP
    for k in M.HEAD_KINDS:
        img, ai, aa, bm, ba, gm, ga = occlusion_tests(k); res[k] = (ai, aa, bm, ba, gm, ga)
        im.paste(img.crop((WORN_W // 2 - 58, 0, WORN_W // 2 + 58, 200)), (x, 48))
        S16.label(d, (x, 250), "%s" % M.KO[k], 11, bold=True)
        S16.label(d, (x, 264), "(a) 안 %d / %d" % (ai, aa), 11, (150, 30, 30) if ai else (40, 40, 40))
        S16.label(d, (x, 278), ("(b) 획 밖 %d/%d · 기하 %d/%d" % (bm, ba, gm, ga)) if ga else "(b) 뒤층∩머리 0px", 11, (40, 40, 40))
        x += 116 + 4
    return im, res

# ---- 잔여 차이 -------------------------------------------------------------------------
NOTES = {
    "clothhat": "[①] 챙 B2 를 장축(y=41u)에서 갈라 먼 쪽 절반(위 호)은 머리 뒤, 가까운 절반은 앞. 자른 선은 안 그린다. 관·띠 앞. [R18] 관 Ivory · 챙 TintHead 주황 · 띠 그늘 · 윤곽 잉크.",
    "furhat": "[①] 먼 쪽이 보이는 부위가 없다(단·관이 머리를 감싼다) → 전부 앞층. [R18] 관 Wool · 단·폼폼 Ivory · 결 잉크 α0.55.",
    "fedora": "[①] 챙 장축(y=41.5u)에서 분할 — 먼 쪽 뒤층. 관·띠 앞. [R18] 펠트 파랑 · 띠 주황.",
    "crown": "[①] 안쪽 뒷벽(테 윗변~봉우리 중간 높이, ROLE BW = SH #2B220A 윤곽 없음) 뒤층 — 봉우리 사이 머리 위 틈을 안쪽 색으로 채운다. 재맞춤(골 +0.773)으로도 머리는 봉우리 사이로 보이지 않는다(기하 36px, 윤곽이 전부 먹음 — 리더 판정 현행 유지). [L-2] 보석 #C6443C.",
    "sunglasses": "[②] 원인: 렌즈 윤곽이 잉크 → 검은 머리에서 테 소실 + M/M2 밝기 동일(L 0.18/0.17). 정정: 렌즈 = 그늘(#16213F) · 테 = M2 Silver 선 · 다리·코다리 M2. C* 의 「어두운 렌즈+밝은 테」 복원.",
    "goggles": "[③] 원인: r 0.24 R 렌즈 원에 1pt 잉크 윤곽 → 점. 정정: 렌즈 원 윤곽 없음(주황 M2 on 은색 판 M, 대비 1.62… 경계는 색차로) · 판 윤곽 잉크 · 브리지 잉크 ×1.4 · 끈 M.",
    "monocle": "[④] 구슬 M2 Silver 윤곽 없음 r 1pt. [R20] 렌즈 몸 채움 = GLASS(잉크 위 M2 α0.20, 불투명 판) + 알·눈 ±0.56 R(R17c ±0.46 → 1.5 W 간격 시험 통과 최소값 1.52 W). 테·사슬 Gold(M). 반대쪽 눈 = 잉크 대비색.",
    "bowtie": "[⑤] dy +0.30 R: 매듭 윗변 −1.681 → −1.381(어깨선 −0.063, N-1 밴드 안) · 날개 윗변 −1.171(머리 원반 밑 0.028 아래). 예외 폐지. [R18] 날개 TintNeck · 매듭 Ivory.",
    "backpack": "[⑥] 착용면 = 뒷판 B1(민무늬 Canvas, 잉크 윤곽, 뒤층) + 손잡이 S0(뒤층) + 어깨끈 2(M2 초록, 앞층, 1.8pt) — 덮개·버클·주머니·하이라이트는 카드에만. 색 hex 카드와 동일. 그룹 α 1.0.",
    "roundglasses": "[L-4→R20] 렌즈 몸 채움 = GLASS(잉크 위 M2 α0.16 사전 합성, 불투명 판) — 보이는 것은 투명 워시와 같고 도형은 Filled(EyesVisorOpacityTests 격자 27칸). 테·다리·코다리 Silver(M). 카드는 M2 워시 α0.16.",
    "stripedtie": "[R17] dy −0.287 유지. [R18] 타이 NeckDeep · 줄무늬 Ivory(M2) · 잉크 윤곽.",
    "scarf": "[R18] 천 TintHead 주황 · 술 Leather 선(M2).",
    "bellnecklace": "[R18] 목줄 Leather 선(M) · 방울·구슬·추 Gold(M2) + 잉크 윤곽.",
    "shortcape": "[R17d] 밑단 −5.089 R. [R18] 뒤판 CapeRed(M) · 칼라 Ivory(M2) · 걸쇠 M + 잉크 윤곽(무대색 #A8332A/#8E241C/#7E1F17 폐기).",
    "longcape": "[R17d] 밑단 −9.055 R · 웅크리기 칸: 클램프 없음(관통 2.18 R). [R18] 색은 짧은망토와 같다.",
    "wings": "[⑥ 확인] 깃은 얇아 앞뒤 면이 같고 척추(0.20 R)는 몸통 뒤 — 그대로 맞다. [L-3] 그룹 α 1.0 · 깃 Paper · 척추 TintBack.",
}

def diff_lines(kind):
    ps = M.WORN[kind]
    return ["조각 %d (뒤층 %d · 앞층 %d)" % (len(ps), sum(1 for p in ps if p.layer == "back"), sum(1 for p in ps if p.layer == "front"))]

def build_rows_sheet(kinds, title, worn_c, extra=None, crouch_for_capes=True):
    ncol = 6 if crouch_for_capes else 5
    W = GAP + ncol * (WORN_W + GAP) + NOTE_W + GAP
    HEADER = 118; ROW = WORN_H + 34
    H = HEADER + ROW * len(kinds) + GAP + (extra.height + GAP if extra is not None else 0)
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 10), title, 15, bold=True)
    S16.label(d, (GAP, 34), "C 인계본 착용(재현기 그대로) | 새 착용 4변형: 밝은 바탕 #F2F2F2/검은 잉크 · 어두운 #1E1E1E/검은 · 어두운/흰 · 밝은/흰 | 웅크리기(망토만, 최대 깊이·클램프 없음) | E. R = %.0f px · 획 1pt 하한(출하 0.75)." % R_PX, 12, (70, 70, 70))
    S16.label(d, (GAP, 52), "색 = design-art palette_model.ROLE(재질 M/M2 불투명 + 잉크 윤곽 · 독립선 재질 · 하이라이트 흰 α0.42 · 등급색 0) + R19 정정 4건(선글라스 렌즈 SH/테 M2 · 고글 렌즈 윤곽 없음 · 구슬 M2 윤곽 없음 r 1pt · 배낭 끈 M2). 카드와 같은 hex.", 12, (70, 70, 70))
    S16.label(d, (GAP, 70), "★ [①] 모자 2층: 챙 먼 쪽·왕관 안쪽 뒷벽은 머리 뒤(SortBack), 관 앞·띠·가까운 챙은 앞. 불투명 바탕 조각 폐지(채움이 불투명). [⑤] 나비넥타이 +0.30 R. [⑥] 배낭 = 뒷판 + 어깨끈(앞면은 카드에만).", 12, (150, 40, 40))
    S16.label(d, (GAP, 88), "★ 오프라인 래스터. 최종 판정은 실제 빌드 캡처로만. 1pt 하한의 Windows 1× 실기 미확인. 반대쪽 눈 = 흰(검은 잉크) / 목탄 #252829(흰 잉크).", 12, (150, 40, 40))
    xs = [GAP + i * (WORN_W + GAP) for i in range(ncol)]; xE = xs[-1] + WORN_W + GAP
    for i, k in enumerate(kinds):
        y = HEADER + ROW * i
        it = PM.BY_KIND[k]
        S16.label(d, (xs[0], y), "%s  %s / %s   M %s(%s) · M2 %s(%s)" % (M.KO[k], k, M.SLOT[k], it[4], PM.name_of(it[4]), it[5], PM.name_of(it[5])), 13, bold=True)
        y0 = y + 22
        im.paste(worn_c[k], (xs[0], y0))
        for j, (lab, bg, ink) in enumerate(VARIANTS):
            im.paste(render_body(k, WORN_W, WORN_H, HEAD_CX, HEAD_CY, R_PX, bg, ink), (xs[1 + j], y0))
        if crouch_for_capes:
            if k in M.CAPES:
                im.paste(render_body(k, WORN_W, WORN_H, HEAD_CX, HEAD_CY - 60, R_PX, BG_LIGHT, INK_B, crouch=True), (xs[5], y0))
            else:
                ImageDraw.Draw(im).rectangle([xs[5], y0, xs[5] + WORN_W, y0 + WORN_H], fill=(238, 238, 236))
        RZ.text_block(d, (xE, y0), diff_lines(k) + [""] + RZ.wrap(NOTES[k], 26, 31), size=11, color=(50, 50, 50), lh=15)
        labs = ["C"] + [v[0] for v in VARIANTS] + (["웅크리기"] if crouch_for_capes else [])
        for x, lab in zip(xs, labs):
            S16.label(d, (x + 4, y0 + 2), lab, 11, (200, 200, 200) if lab == "C" else (120, 120, 120), bold=True)
    if extra is not None: im.paste(extra, (GAP, HEADER + ROW * len(kinds) + GAP))
    return im

def main():
    worn_c = S16.render_handoff_worn(R_PX, tag="c19")
    strip, res = occlusion_strip(width=GAP + 5 * (WORN_W + GAP) + NOTE_W + GAP - 2 * GAP)
    for k, (ai, aa, bm, ba, gm, ga) in res.items():
        print("occlusion %-9s (a) 앞층 안 자홍 %d/%d  (b) 뒤층∩머리∖앞층: 획 밖 %d/%d · 기하(획 무시) %d/%d" % (k, ai, aa, bm, ba, gm, ga))
    pri = build_rows_sheet(M.PRIORITY, "R19 우선 시트 — 모자 4(2층) → 선글라스·고글(ROLE 정정) → 외알안경(구슬) → 나비넥타이(+0.30 R) → 배낭(뒷판+끈)  [R18 팔레트 적용 포함]", worn_c, extra=strip, crouch_for_capes=False)
    pri.save(os.path.join(HERE, "r19_priority_sheet.png")); print("wrote r19_priority_sheet.png %dx%d" % pri.size)
    full = build_rows_sheet(M.KINDS, "R19 착용 시트 전체(R18 팔레트 + R19 6건) — 16행", worn_c, crouch_for_capes=True)
    full.save(os.path.join(HERE, "r19_worn_sheet.png")); print("wrote r19_worn_sheet.png %dx%d" % full.size)
    cs = build_card_sheet(); cs.save(os.path.join(HERE, "r19_card_sheet.png")); print("wrote r19_card_sheet.png %dx%d" % cs.size)

if __name__ == "__main__":
    main()
