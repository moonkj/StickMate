# -*- coding: utf-8 -*-
"""R17 — 착용 비교 시트 (사용자 지적 5건 + 리더 정정 반영). 열 구성은 R16 과 같다: C | C* | D′ | D″ | E.

    r17_priority_sheet.png   외알안경 → 모자 4 → 줄무늬타이 (6행) + ★ 모자 불투명 증명 띠(자홍 머리 테스트)
    r17_worn_sheet.png       16행 (위 6행 → 나머지 10)
    r17_worn_truesize.png    실제 크기

D′ = 인계본 무대+캐릭터 위에 우리 장비만(모자 불투명 바탕 = 머리 채움색 #111). D″ = 출하 화면(검은 잉크·밝은 바탕, 바탕 = 잉크).
"""
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig, handoff
import r16_model as M16
import r16_raster as RZ
import r16_worn_sheet as S16
import r17_model as M

WORN_W, WORN_H, R_PX, GAP, NOTE_W = S16.WORN_W, S16.WORN_H, S16.R_PX, S16.GAP, 360
HEAD_CX, HEAD_CY, STAGE_X = S16.HEAD_CX, S16.HEAD_CY, S16.STAGE_X
KINDS = M.KINDS
HANDOFF_HEADFILL = RZ._rgb("#111111")
MAGENTA = (255, 0, 255)

def draw_pieces(cv, pieces, to_px, width_px, palette, headfill, layer=None, worn_monocle=True):
    ps = [p for p in pieces if layer is None or p.layer == layer]
    if not ps: return
    ga = ps[0].group_alpha
    g = cv.group() if ga < 1.0 else cv
    for p in ps:
        if p.conditional == "monocle worn" and not worn_monocle: continue
        pts = [to_px(q) for q in p.pts]
        if p.fill is not None:
            if p.fill[0] == "grad": g.fill(pts, RZ._rgb(palette["accent"]), grad=(p.fill[1], p.fill[2]))
            elif p.fill[0] == "accent": g.fill(pts, RZ._rgb(palette["accent"]), alpha=p.fill[1])
            elif p.fill[0] == "headfill": g.fill(pts, headfill, alpha=1.0)
            elif p.fill[0] == "white": g.fill(pts, RZ._rgb(palette["white"]), alpha=p.fill[1])
            else: g.fill(pts, RZ._rgb(p.fill[1]), alpha=1.0)
        if p.line is not None:
            if p.line[0] == "solid": col, a = RZ._rgb(p.line[1]), p.line[2]
            else: col, a = RZ._rgb(palette[p.line[0]]), p.line[1]
            dash = None
            if p.dash is not None:
                k = width_px(p) / p.width_R(); dash = (p.dash[0] * k, p.dash[1] * k)
            g.stroke(pts, width_px(p), col, a, p.loop, dash=dash)
    if ga < 1.0: cv.composite(g, ga)

def render_ours(kind, w, h, head_cx, head_cy, r_px, scale=M16.SHIP, palette=M.PALETTE_R17, figure="handoff",
                bg=None, ink=S16.INK_DARK, head_color=None, stage_x=None, pieces=None):
    cv = RZ.Canvas(w, h, RZ._rgb(S16.STAGE_BG) if bg is None else bg)
    if figure == "handoff": cv.paste_background(S16.stage_background(w, h, r_px, stage_x))
    to_px = lambda p: (head_cx + p[0] * r_px, head_cy - p[1] * r_px)
    wpx = lambda p: p.width_R(scale) * r_px
    ps = (pieces or M.WORN)[kind]
    headfill = HANDOFF_HEADFILL if figure == "handoff" else (head_color or ink)
    draw_pieces(cv, ps, to_px, wpx, palette, headfill, layer="back")
    if figure == "handoff": S16.draw_handoff_figure(cv, head_cx, head_cy, r_px)
    elif figure == "ours": S16.draw_our_figure(cv, head_cx, head_cy, r_px, ink)
    elif figure == "magenta":   # 증명용 — 머리 원반만 자홍
        cv.disc(head_cx, head_cy, M16.HEAD_OUTER_R * r_px, MAGENTA)
    draw_pieces(cv, ps, to_px, wpx, palette, headfill, layer="front")
    return cv.done()

# ============================================================================
# 모자 불투명 증명 — 자홍 머리 위에 모자를 그리고 모자 합집합 안의 자홍 픽셀을 센다
# ============================================================================
def occlusion_proof(kind, r_px=R_PX, with_base=True):
    ps = M.WORN[kind]
    if not with_base: ps = [p for p in ps if not p.base]
    im = render_ours(kind, WORN_W, WORN_H, HEAD_CX, HEAD_CY, r_px, figure="magenta", bg=(40, 40, 40), ink=(0, 0, 0),
                     head_color=(0, 0, 0), pieces={kind: ps})
    A = np.asarray(im, np.int16)
    mag = (A[..., 0] > 180) & (A[..., 1] < 90) & (A[..., 2] > 180)
    # 모자 불투명 조각 합집합 마스크(2px 침식 — AA 가장자리 제외)
    mask = Image.new("L", (WORN_W, WORN_H), 0); d = ImageDraw.Draw(mask)
    to_px = lambda p: (HEAD_CX + p[0] * r_px, HEAD_CY - p[1] * r_px)
    for p in M.WORN[kind]:
        if p.base: d.polygon([to_px(q) for q in p.pts], fill=255)
    from PIL import ImageFilter
    er = np.asarray(mask.filter(ImageFilter.MinFilter(5))) > 0
    inside = int((mag & er).sum()); total = int(mag.sum()); area = int(er.sum())
    return im, inside, total, area

def occlusion_strip():
    W = GAP + 4 * 2 * (WORN_W // 2 + 4) + GAP; H = 60 + WORN_H // 2 + 40
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 8), "★ [1] 모자 불투명 증명 — 머리 원반을 자홍으로 그리고 모자를 얹었다. 각 쌍: 바탕 없음(R16, 비침) | 바탕 있음(R17). 숫자 = 모자 합집합(2px 침식) 안 자홍 픽셀 / 화면 전체 자홍 픽셀", 12, bold=True)
    S16.label(d, (GAP, 26), "합격 = 오른쪽 그림의 「안」이 0. 왼쪽이 0 이 아닌 것이 양성 대조(테스트가 비침을 실제로 잡는가).", 11, (120, 30, 30))
    res = {}
    x = GAP
    for k in M.HEAD_KINDS:
        for wb in (False, True):
            img, inside, total, area = occlusion_proof(k, with_base=wb)
            res[(k, wb)] = (inside, total, area)
            small = img.crop((WORN_W // 2 - 58, 0, WORN_W // 2 + 58, 200))
            im.paste(small, (x, 48))
            S16.label(d, (x, 48 + 202), "%s %s" % (M.KO[k], "바탕 있음" if wb else "바탕 없음"), 11, bold=True)
            S16.label(d, (x, 48 + 216), "안 %d / 전체 %d" % (inside, total), 11, (150, 30, 30) if (wb and inside) else (40, 40, 40))
            x += 116 + 4
    return im, res

# ============================================================================
# 잔여 차이
# ============================================================================
_ROWS = None
def diff_lines(kind):
    global _ROWS
    if _ROWS is None: _ROWS = M.survival_rows()
    rows = [r for r in _ROWS if r["kind"] == kind]
    n = len(rows); a75 = sum(1 for r in rows if r["ok75"]); a100 = sum(1 for r in rows if r["ok100"]); a35 = sum(1 for r in rows if r["ok35"])
    dead = [r for r in rows if not r["ok75"]]
    return ["조각 %d · 1pt 생존 @0.75 %d · @1.0 %d · @0.35 %d" % (n, a75, a100, a35),
            ("@0.75 규칙 미달: " + " ".join(r["p"].src for r in dead)) if dead else "@0.75 규칙 미달: 없음"]

def hat_note(kind):
    f = M.HAT_FIT[kind]
    return ("[2] 균일 배율 u %.4f R/u (카드의 ×%.2f)%s · dy %+.3f · 착용선 %+.2f R · 꼭대기 %+.2f R · 밑 %+.2f R · 폭 %.2f R. [1] 채움 조각마다 머리색 불투명 바탕 → 머리 원반이 모자 안에서 안 보인다(증명 띠)."
            % (f["u"], f["u"] / M.ICON_U_HEAD, "" if f["ky"] == 1.0 else " · 세로 압축 ky %.2f(균일 배율은 액자 %.3f R 를 깬다)" % (f["ky"], M.TOP_LIMIT), f["dy"], f["wear"], f["top"], f["bottom"], f["width"]))

NOTES = {
    "monocle":   "[3][R17c] 알 중심 = 반대쪽 눈의 거울 위치 (+0.46, +0.185) R — 알·하이라이트만 반전(x→64−x, +6.57u). 알 왼끝 +0.036 R(중심선 안), 오른끝 +0.884 R(머리 현 안). [R17b] 사슬·구슬은 알 상대 오프셋 그대로(+20.57u) 오른쪽 아래 바깥으로 — 구슬은 원반 밖(중심 1.341 R, y −0.03 R 미세 조정으로 가장자리와 안 겹침), 슬롯 64 를 5.5u 넘지만 몸 표면엔 클리핑이 없다. 구슬 밑은 어깨선 위 0.36 R · 팔 캡슐 간격 0.77 R. [4] 반대쪽 눈 흰 원반 r 1pt @(−0.46,+0.107)R — 착용 시에만. 카드 불변. [5] 알 워시·구슬 = 등급색.",
    "clothhat":  "", "furhat": "", "fedora": "", "crown": "",
    "stripedtie": "[6] 매듭 윗변을 −1.013 R(머리 원반 안 +0.130 침범)에서 −1.300 R(어깨선 +0.018)로 −0.287 R 내렸다. 목 길이는 0.175 R(1.0pt)라 「목 하단」= 어깨선이다. 나머지 NECK 3종은 어깨선 아래(−1.40~−1.68)라 손대지 않았다. [5] 줄무늬·매듭 = 등급색.",
    "sunglasses": "[5] 렌즈 = 등급색 α0.6(회색) — C* 와 같은 색. 그 밖은 R16 과 같다(다리 점·하이라이트 점·획 1.49×).",
    "roundglasses": "[5] 렌즈 α0.16 등급색. 그 밖은 R16 과 같다. 눈은 없다(외알안경에만 반대쪽 눈).",
    "goggles":   "[5] 렌즈 원 = 등급색 α0.6. 그 밖은 R16.",
    "bowtie":    "점검: 매듭 윗변 −1.68 R(어깨선 −0.36) — 규칙 밴드(±0.20) 밖이지만 **머리 쪽이 아니라 아래쪽**이라 사용자 지적과 반대 방향. 변경 없음(리더 판단). [5] 매듭 등급색.",
    "scarf":     "점검: 감은 띠 윗변 −1.47 R(어깨선 −0.15) 통과. 변경 없음.",
    "bellnecklace": "점검: 목줄 윗변 −1.40 R(어깨선 −0.08) 통과. 변경 없음. [5] 방울·구슬·추 등급색.",
    "shortcape": "R16 과 같다(무대 도형·재질색). 요크 등급색 α0.45 는 무대 도형에 없다(아이콘은 안 얹는다).",
    "longcape":  "R16 과 같다.",
    "wings":     "R16 과 같다. [5] 척추 등급색 α0.4.",
    "backpack":  "R16 과 같다. [5] 뚜껑·버클·주머니 등급색.",
}

def build_rows_sheet(kinds, title, worn_c, worn_cf, extra=None):
    W = GAP + 4 * (WORN_W + GAP) + NOTE_W + GAP
    HEADER = 118; ROW = WORN_H + 34
    H = HEADER + ROW * len(kinds) + GAP + (extra.height + GAP if extra is not None else 0)
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 10), title, 15, bold=True)
    S16.label(d, (GAP, 34), "C 인계본 착용(재현기 그대로) | C* 인계본 착용(원문 의미) | D′ 인계본 무대+캐릭터 위에 우리 장비만(1pt 하한 · 선 브라스 · 강조 등급색 · 모자 불투명 · 외알안경 오른쪽+반대쪽 눈) | D″ 출하 화면(검은 잉크·밝은 바탕·눈 없음) | E", 12, (70, 70, 70))
    S16.label(d, (GAP, 52), "★ 인계본 파일보다 디자인 소유자(사용자) 지시가 우선한다 — D′/D″ 는 인계본 프리뷰(C/C*)와 일부러 다르다: 모자 크기·불투명, 외알안경 좌우, 반대쪽 눈, 줄무늬타이 높이. 같은 크기 R = %.0f px." % R_PX, 12, (150, 40, 40))
    S16.label(d, (GAP, 70), "모자 규칙: 착용선 아래 불투명 합집합이 머리 현을 덮는다(머리 = 배율 0.35 링 포함 1.184 R) · 착용선 ≤ 목표(+0.30 / 털모자 0.00 / 왕관 띠 +0.45) · 꼭대기 < 2.551 R(액자) · 밑 > −1.0 R. 균일 배율 우선, 액자를 깨면 세로 압축(E 에 명시).", 12, (70, 70, 70))
    S16.label(d, (GAP, 88), "★ 오프라인 래스터(둥근 캡). 최종 판정은 실제 빌드 캡처로만. 1pt 가 Windows 1× 에서 어떻게 보이는지는 실기 미확인. 강조 등급색은 일반 #8A8F98 가정(WornColor 는 이 색을 #586F98 로 바꾼다 — 우회 필요).", 12, (150, 40, 40))
    xs = [GAP + i * (WORN_W + GAP) for i in range(4)]; xE = xs[3] + WORN_W + GAP
    for i, k in enumerate(kinds):
        y = HEADER + ROW * i
        S16.label(d, (xs[0], y), "%s  %s / %s" % (M.KO[k], k, M.SLOT[k]), 14, bold=True)
        y0 = y + 22
        im.paste(worn_c[k], (xs[0], y0)); im.paste(worn_cf[k], (xs[1], y0))
        im.paste(render_ours(k, WORN_W, WORN_H, HEAD_CX, HEAD_CY, R_PX, figure="handoff", stage_x=STAGE_X), (xs[2], y0))
        im.paste(render_ours(k, WORN_W, WORN_H, HEAD_CX, HEAD_CY, R_PX, figure="ours", bg=S16.DESK_BG, ink=S16.INK_DARK), (xs[3], y0))
        note = hat_note(k) if k in M.HEAD_KINDS else NOTES[k]
        RZ.text_block(d, (xE, y0), diff_lines(k) + [""] + RZ.wrap(note, 28, 33), size=11, color=(50, 50, 50), lh=15)
        for x, lab in zip(xs, ("C", "C*", "D′", "D″")):
            S16.label(d, (x + 4, y0 + 2), lab, 12, (200, 200, 200), bold=True)
    if extra is not None:
        im.paste(extra, (GAP, HEADER + ROW * len(kinds) + GAP))
    return im

def build_truesize():
    cols = 2; rows = 8
    cellw = 60 + 6 + 40 + 6 + 60 + 6 + 48 + 6 + 72 + 6 + 60 + 20
    W = GAP + cols * (cellw + 10); H = 70 + rows * 150
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 8), "R17 실제 크기 — 인계본 프리뷰 1×(R 22.12px) | 출하 화면 @0.75 1×(R 5.8px) · 2× · @1.00 1× · 2× | 어두운 바탕·흰 잉크 @0.75 2×(모자 바탕 = 흰 잉크)", 13, bold=True)
    S16.label(d, (GAP, 30), "★ 1× Windows 에서 1pt 획 = 1 픽셀. 오프라인 래스터 — 실기 미확인.", 12, (150, 40, 40))
    worn1 = S16.render_handoff_worn(22.12, faithful=True, tag="ts17")
    for i, k in enumerate(KINDS):
        cx = GAP + (i % cols) * (cellw + 10); cy = 60 + (i // cols) * 150
        S16.label(d, (cx, cy), "%s (%s)" % (M.KO[k], k), 12, bold=True)
        y = cy + 18; x = cx
        w1 = worn1[k].crop((WORN_W // 2 - 30, 0, WORN_W // 2 + 30, 120)); im.paste(w1, (x, y)); x += 60 + 6
        for r_px, scale, cw in ((5.816, 0.75, 40), (11.632, 0.75, 60), (7.755, 1.0, 48), (15.51, 1.0, 72)):
            hcy = r_px * HEAD_CY / R_PX
            im.paste(render_ours(k, cw, 120, cw / 2.0, hcy, r_px, scale=scale, figure="ours", bg=S16.DESK_BG, ink=S16.INK_DARK), (x, y)); x += cw + 6
        hcy = 11.632 * HEAD_CY / R_PX
        im.paste(render_ours(k, 60, 120, 30, hcy, 11.632, scale=0.75, figure="ours", bg=S16.DARK_DESK, ink=S16.INK_WHITE), (x, y))
    return im

def main():
    worn_c = S16.render_handoff_worn(R_PX, tag="c17")
    worn_cf = S16.render_handoff_worn(R_PX, faithful=True, tag="cf17")
    strip, res = occlusion_strip()
    for (k, wb), (inside, total, area) in sorted(res.items()):
        print("occlusion %-9s %s 안 %5d / 전체 %5d / 합집합 %6d px" % (k, "바탕있음" if wb else "바탕없음", inside, total, area))
    pri = build_rows_sheet(M.PRIORITY, "R17 우선 시트 — 외알안경 · 모자 4 · 줄무늬타이 (사용자 지적 5건 + 리더 정정 1건)", worn_c, worn_cf, extra=strip)
    pri.save(os.path.join(HERE, "r17_priority_sheet.png")); print("wrote r17_priority_sheet.png %dx%d" % pri.size)
    full = build_rows_sheet(KINDS, "R17 착용 시트 전체 — 외알안경 → 모자 4 → 줄무늬타이 → 나머지 10", worn_c, worn_cf)
    full.save(os.path.join(HERE, "r17_worn_sheet.png")); print("wrote r17_worn_sheet.png %dx%d" % full.size)
    ts = build_truesize(); ts.save(os.path.join(HERE, "r17_worn_truesize.png")); print("wrote r17_worn_truesize.png %dx%d" % ts.size)

if __name__ == "__main__":
    main()
