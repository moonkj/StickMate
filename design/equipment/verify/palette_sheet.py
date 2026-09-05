# -*- coding: utf-8 -*-
"""재질 팔레트 비교 시트 — design-art, 2026-09-05.  palette_sheet.png / palette_truesize.png

열:  현행 카드 44 | 새 카드 44 | 새 카드 88(2×) | 몸 현행 R17(브라스 선·등급색 강조, 밝은 바탕·검은 잉크) |
     몸 새 팔레트 — 밝은 바탕 #F2F2F2·검은 잉크 | 어두운 바탕 #1E1E1E·검은 잉크 | 어두운 바탕·흰 잉크 | 밝은 바탕·흰 잉크 | 메모
★ 도형은 r17_model.WORN(몸) · handoff.build(카드)의 좌표를 그대로 쓴다 — 새로 그린 도형 0개. 바꾸는 것은 색뿐이다.
★ 오프라인 래스터(r16_raster, 둥근 캡). 최종 판정은 실제 빌드 캡처로만. 1× 띠(palette_truesize.png)는 실제 픽셀 크기다.
"""
import os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import handoff, rig
import r16_model as M16
import r16_raster as RZ
import r16_worn_sheet as S16
import r17_model as M
import palette_model as P

CELL_W, CELL_H = 232, 300           # 몸 셀(R17 232×400 을 허리 위로 자른다 — 장비는 전부 어깨 위)
R_PX = S16.R_PX; HEAD_CX = S16.HEAD_CX; HEAD_CY = S16.HEAD_CY
GAP = 10; NOTE_W = 330; CARD = 44
LIGHT, DARK = RZ._rgb("#F2F2F2"), RZ._rgb("#1E1E1E")
INK_BLACK, INK_WHITE = RZ._rgb("#111111"), RZ._rgb("#FFFFFF")
KINDS = ["clothhat", "furhat", "fedora", "crown", "sunglasses", "roundglasses", "goggles", "monocle",
         "bowtie", "stripedtie", "scarf", "bellnecklace", "shortcape", "longcape", "wings", "backpack"]
# 현행(R17/프로덕션) 등급 — 슬롯 안 requiredLevel 순위 → [일반,일반,희귀,희귀,영웅,전설] (ItemCatalog._rarityByRank)
RARITY_NOW = {"clothhat": "일반", "furhat": "일반", "fedora": "희귀", "crown": "희귀",
              "sunglasses": "일반", "roundglasses": "일반", "goggles": "희귀", "monocle": "희귀",
              "bowtie": "일반", "stripedtie": "일반", "scarf": "희귀", "bellnecklace": "희귀",
              "shortcape": "일반", "longcape": "일반", "wings": "희귀", "backpack": "희귀"}
_RAW = handoff.parse_items()

# ============================================================================
# 1. 몸 — 새 팔레트로 r17 조각 그리기
# ============================================================================
def draw_body_pieces(cv, pieces, to_px, width_px, kind, ink_hex, layer):
    for p in pieces:
        if p.layer != layer: continue
        if p.conditional == "monocle worn": pass      # 착용 시트이므로 반대쪽 눈을 그린다
        fill, fa, line = P.resolve(kind, p.src, "body", ink_hex)
        pts = [to_px(q) for q in p.pts]
        if p.base:
            if fill is not None: cv.fill(pts, RZ._rgb(fill), alpha=1.0)
            continue
        if p.filled and fill is not None:
            cv.fill(pts, RZ._rgb(fill), alpha=fa)
        if line is not None and p.line is not None:
            la = p.line[1] if p.line[0] in ("ink", "white") else p.line[2]
            dash = None
            if p.dash is not None:
                k = width_px(p) / p.width_R(); dash = (p.dash[0] * k, p.dash[1] * k)
            cv.stroke(pts, width_px(p), RZ._rgb(line), la, p.loop, dash=dash)

def render_body_new(kind, bg, ink, w=CELL_W, h=CELL_H, head_cx=HEAD_CX, head_cy=HEAD_CY, r_px=R_PX, scale=M16.SHIP):
    cv = RZ.Canvas(w, h, bg)
    to_px = lambda p: (head_cx + p[0] * r_px, head_cy - p[1] * r_px)
    wpx = lambda p: p.width_R(scale) * r_px
    ink_hex = "#%02X%02X%02X" % ink
    ps = M.WORN[kind]
    draw_body_pieces(cv, ps, to_px, wpx, kind, ink_hex, "back")
    S16.draw_our_figure(cv, head_cx, head_cy, r_px, ink)
    draw_body_pieces(cv, ps, to_px, wpx, kind, ink_hex, "front")
    return cv.done()

def render_body_now(kind, bg, ink, w=CELL_W, h=CELL_H):
    """현행 R17: 선 브라스 · 강조 = 그 아이템의 실제 등급색(UiChrome 램프) · 모자 바탕 = 잉크. r17_sheet.render_ours 와 같은 규칙."""
    import r17_sheet as S17
    pal = {"ink": P.BRASS, "accent": P.RARITY[RARITY_NOW[kind]], "white": "#FFFFFF"}
    im = S17.render_ours(kind, CELL_W, 400, HEAD_CX, HEAD_CY, R_PX, palette=pal, figure="ours", bg=bg, ink=ink, head_color=ink)
    return im.crop((0, 0, w, h))

# ============================================================================
# 2. 카드 — 인계본 아이콘 좌표(64 단위) 그대로, 색만
# ============================================================================
def render_card(kind, cell, mode="new"):
    """mode 'new' = 재질 팔레트 · 'now' = 현행(카드 잉크 선 + 등급색 워시 α0.21/조각 α)."""
    bg = RZ._rgb("#15181E")
    cv = RZ.Canvas(cell, cell, bg)
    k = cell / 64.0
    pieces = handoff.build(kind, _RAW[kind])
    rar = P.RARITY[RARITY_NOW[kind]]
    for i, pc in enumerate(pieces):
        src = "%s%d" % (pc.call, i)
        pts = [(x * k, y * k) for x, y in pc.pts]
        wpx = pc.stroke_w * k
        if mode == "new":
            fill, fa, line = P.resolve(kind, src, "card", P.CARD_INK)
            if pc.filled and fill is not None: cv.fill(pts, RZ._rgb(fill), alpha=fa)
            if line is not None:
                la = 0.42 if pc.call == "H" else (handoff._num(pc.opts["strokeOpacity"]) if "strokeOpacity" in pc.opts else 1.0)
                cv.stroke(pts, wpx, RZ._rgb(line), la, pc.closed or pc.filled)
        else:
            if pc.filled:
                fo = pc.fill_opacity
                a = fo if fo is not None else (0.81 if kind in M16.CAPES else P.GRAD_MEAN)
                col = "#D2402F" if kind in M16.CAPES else rar
                cv.fill(pts, RZ._rgb(col), alpha=a)
            if pc.call == "H":
                cv.stroke(pts, wpx, (255, 255, 255), 0.42, False)
            elif pc.call not in ("F", "CF"):
                la = handoff._num(pc.opts["strokeOpacity"]) if "strokeOpacity" in pc.opts else 1.0
                cv.stroke(pts, wpx, RZ._rgb(P.CARD_INK), la, pc.closed or pc.filled)
    return cv.done()

# ============================================================================
# 3. 시트
# ============================================================================
def note_for(kind):
    it = P.BY_KIND[kind]
    roles = P.ROLE[kind]
    parts = []
    for src, (f, l) in roles.items():
        ftxt = {"M": "M", "M2": "M2", "SH": "그늘", None: "-", "EYE": "눈(대비색)"}.get(f if not isinstance(f, tuple) else "W_", "카드워시 M2 α%.2f/몸 없음" % f[1] if isinstance(f, tuple) else "?")
        if isinstance(f, tuple): ftxt = "카드 M2 α%.2f·몸 없음" % f[1]
        ltxt = {"INK": "잉크", "M": "M선", "M2": "M2선", "W": "흰", None: ""}[l]
        parts.append("%s:%s%s" % (src, ftxt, ("/" + ltxt) if ltxt else ""))
    return "M %s %s · M2 %s %s | " % (it[4], P.name_of(it[4]), it[5], P.name_of(it[5])) + " ".join(parts) + " | " + it[7]

def build_sheet():
    xs = []; x = GAP
    widths = [CARD, CARD, CARD * 2, CELL_W, CELL_W, CELL_W, CELL_W, CELL_W]
    for wdt in widths: xs.append(x); x += wdt + GAP
    xN = x; W = xN + NOTE_W + GAP
    HEADER = 150; ROW = CELL_H + 26
    H = HEADER + ROW * len(KINDS) + GAP
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 10), "재질 팔레트 시트 — 인계본 16종 · 채움 = 재질색 불투명 · 윤곽 = 잉크 · 독립선 = 재질선 · 등급색은 조각에 없음 (design-art 2026-09-05)", 15, bold=True)
    S16.label(d, (GAP, 34), "열: 현행 카드 44px | 새 카드 44px(1×) | 새 카드 2× | 몸 현행 R17(브라스 선·등급색 강조·잉크 바탕, 밝은 바탕·검은 잉크) | 새: 밝은 바탕 #F2F2F2·검은 잉크 | 어두운 바탕 #1E1E1E·검은 잉크 | 어두운 바탕·흰 잉크 | 밝은 바탕·흰 잉크", 12, (70, 70, 70))
    S16.label(d, (GAP, 52), "★ 도형은 r17_model.WORN / handoff.build 좌표 그대로(새 도형 0). 색만 바꿨다. R = %.0f px. 카드 바탕 #15181E(CardSurfaceMuted) · 카드 선 #E8E2D6(CardIconInk)." % R_PX, 12, (70, 70, 70))
    S16.label(d, (GAP, 70), "★ 오프라인 래스터(둥근 캡)다 — 코너 붕괴를 가릴 수 있다. 최종 판정은 실제 빌드 캡처로만. 실제 크기 띠는 palette_truesize.png.", 12, (150, 40, 40))
    S16.label(d, (GAP, 88), "검은 잉크+어두운 바탕 / 흰 잉크+밝은 바탕 열은 「잉크가 안 보이는 최악」이다 — 캐릭터 본체도 같이 사라지는 조합이고, 그때도 재질 채움은 3.0 이상으로 남는 것을 보이는 열이다(유저는 잉크를 바꾼다).", 12, (70, 70, 70))
    S16.label(d, (GAP, 106), "왕관 보석만 .asset 값이 바뀐다(GoldLight → Toy #C6443C). 나머지 15종의 M/M2 는 지금 .asset 의 tone0/tone1 그대로다. 날개·배낭 그룹 α0.40 → 1.0.", 12, (70, 70, 70))
    S16.label(d, (GAP, 124), "동그란안경·외알안경 렌즈: 몸 = 채움 없음(투명 = 머리가 비친다, 부분 알파 아님) · 카드 = 인계본 유리 워시 α0.16/0.20 을 M2 로. 이 셋이 카드≠몸의 유일한 조각이다.", 12, (70, 70, 70))
    for i, k in enumerate(KINDS):
        y = HEADER + ROW * i
        it = P.BY_KIND[k]
        S16.label(d, (xs[0], y), "%s  %s / %s   M %s  M2 %s" % (it[1], k, M16.SLOT[k], it[4], it[5]), 13, bold=True)
        y0 = y + 20
        im.paste(render_card(k, CARD, "now"), (xs[0], y0))
        im.paste(render_card(k, CARD, "new"), (xs[1], y0))
        im.paste(render_card(k, CARD * 2, "new"), (xs[2], y0))
        im.paste(render_body_now(k, LIGHT, INK_BLACK), (xs[3], y0))
        im.paste(render_body_new(k, LIGHT, INK_BLACK), (xs[4], y0))
        im.paste(render_body_new(k, DARK, INK_BLACK), (xs[5], y0))
        im.paste(render_body_new(k, DARK, INK_WHITE), (xs[6], y0))
        im.paste(render_body_new(k, LIGHT, INK_WHITE), (xs[7], y0))
        for xx, lab in zip(xs, ("현행", "새", "새 2×", "현행 R17", "새 밝/검", "새 어/검", "새 어/흰", "새 밝/흰")):
            S16.label(d, (xx + 3, y0 + CELL_H + 2), lab, 10, (90, 90, 90))
        RZ.text_block(d, (xN, y0), RZ.wrap(note_for(k), 30, 36), size=10, color=(50, 50, 50), lh=13)
    return im

def build_truesize():
    """실제 크기: 카드 44px(1×) · 몸 @0.75 1×(R 5.816px) · 2×(R 11.632) · @1.00 1×(R 7.755) — 밝은/검은 잉크, 어두운/흰 잉크."""
    cols = 2; rows = 8
    cellw = 44 + 6 + 40 + 6 + 60 + 6 + 48 + 6 + 40 + 6 + 60 + 6 + 48 + 16
    W = GAP + cols * (cellw + 10); H = 80 + rows * 140
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S16.label(d, (GAP, 8), "실제 크기 — 새 카드 44px 1× | 밝은 바탕·검은 잉크 @0.75 1× · 2× · @1.00 1× | 어두운 바탕·흰 잉크 @0.75 1× · 2× · @1.00 1×", 13, bold=True)
    S16.label(d, (GAP, 30), "★ 1× Windows 에서 1pt 획 = 1 픽셀. 오프라인 래스터 — 실기 미확인. 이 띠가 코너 붕괴를 가릴 수 있으므로 빌드 캡처가 판정이다.", 12, (150, 40, 40))
    S16.label(d, (GAP, 48), "보는 법: 잉크가 안 보이는 조합이 아니라 「출하 기본(검은 잉크·밝은 바탕)」과 「흰 잉크 전환(어두운 바탕)」 둘이다. 채움이 머리·바탕 양쪽과 갈리는가만 본다.", 12, (70, 70, 70))
    for i, k in enumerate(KINDS):
        cx = GAP + (i % cols) * (cellw + 10); cy = 70 + (i // cols) * 140
        S16.label(d, (cx, cy), "%s (%s)" % (M16.KO[k], k), 12, bold=True)
        y = cy + 18; x = cx
        im.paste(render_card(k, CARD, "new"), (x, y + 30)); x += CARD + 6
        for bg, ink in ((LIGHT, INK_BLACK), (DARK, INK_WHITE)):
            for r_px, scale, cw in ((5.816, 0.75, 40), (11.632, 0.75, 60), (7.755, 1.0, 48)):
                hcy = r_px * HEAD_CY / R_PX
                im.paste(render_body_new(k, bg, ink, w=cw, h=110, head_cx=cw / 2.0, head_cy=hcy, r_px=r_px, scale=scale), (x, y)); x += cw + 6
            x += 4
    return im

def main():
    sh = build_sheet(); sh.save(os.path.join(HERE, "palette_sheet.png")); print("wrote palette_sheet.png %dx%d" % sh.size)
    ts = build_truesize(); ts.save(os.path.join(HERE, "palette_truesize.png")); print("wrote palette_truesize.png %dx%d" % ts.size)

if __name__ == "__main__":
    main()
