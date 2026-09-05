#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""카드 렌더 대조 시트 — 인계본(A) · 설계 모델(B) · **프로덕션 데이터**(P) · **Unity 실제 경로**(U). (coder, 2026-09-05)

    python3 Tools/CardShapeGen/render_card_sheet.py [--unity <CardIconProbe 출력 폴더>] [--out <png>] [--no-chrome]

  A  인계본 카드 — ux-designer 재현기(icons.js)를 headless Chrome 으로. (r15_compare_sheet.render_handoff_cards)
  B  design-equipment R15 설계 모델 렌더 — 사용자 합격 시트의 B 열 그대로(r15_compare_sheet.render_our_card).
  P  프로덕션 데이터 렌더 — 생성 C#/에셋을 verify_card_shapes 의 파서로 **되읽어**(카드 표면 조각만), AccessoryCardIcon.BuildDesigned 와
     같은 식(슬롯 고정 배율 · 2.2/64 × 연속 배수 · 잉크 윤곽 · 워시/하이라이트 사전 합성 · 고정색/머리잉크/대비색 역할)을 파이썬으로
     다시 적어 그린 것. B 와 P 는 같은 래스터(r15_raster)이므로 **둘의 픽셀 차 = 데이터/식의 차**다. 수치로도 찍는다.
  U  Unity 가 실제 AccessoryCardIcon 경로로 찍은 PNG(있으면). 오프라인 래스터가 못 보는 것(uGUI 캡·조인·AA)이 여기 있다.
색 입력은 전부 브라스 #C8A15A(시트 B 와 같은 입력 — 프로덕션의 강조 A 는 등급색이지만 대조를 위해 브라스로 고정. CardIconProbe 의
brass_ 변종이 같은 고정을 Unity 쪽에서 한다). 재질색(Material 역할)은 팔레트 착지 전이라 A 와 같은 경로다.
"""
import argparse, os, sys
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
VERIFY = os.path.join(REPO, "design", "equipment", "verify")
sys.path.insert(0, VERIFY); sys.path.insert(0, HERE)
import handoff                       # noqa: E402
import r15_model as M                # noqa: E402
import r15_raster as RZ              # noqa: E402
import r15_compare_sheet as S        # noqa: E402
import verify_card_shapes as V       # noqa: E402
import palette_model as PM           # noqa: E402  (재질색 M/M2 — 카탈로그와 같은 hex)

CELL = 232
BG = (0x15, 0x18, 0x1E); INK = (0xE8, 0xE2, 0xD6); BRASS = (0xC8, 0xA1, 0x5A)
TONE_PRIMARY, TONE_ACCENT, TONE_SHADE, TONE_HIGHLIGHT, TONE_HEADINK, TONE_INKCONTRAST, TONE_GLASS = 0, 1, 2, 3, 4, 5, 6
SHADE = 0.28
IDX = {"clothhat": 0, "furhat": 1, "fedora": 2, "crown": 3, "sunglasses": 0, "roundglasses": 1, "goggles": 2, "monocle": 3,
       "bowtie": 0, "stripedtie": 1, "scarf": 2, "bellnecklace": 3, "shortcape": 0, "longcape": 1, "wings": 2, "backpack": 3}

def is_card(row): return row["surfaces"] in (0, 2)

def production_pieces():
    """{kind: [row]} — verify 의 파서로 되읽은 프로덕션 데이터의 **카드 표면** 조각(코드 12종 + 에셋 4종)."""
    by_case, _arrays, _xf = V.read_cs()
    out = {}
    for kind, const in V.ITEM_CONST.items(): out[kind] = [r for r in by_case[const] if is_card(r)]
    for kind in V.NECK_ASSET:
        rows, _ = V.asset_rows(kind)
        out[kind] = [r for r in rows if is_card(r)]
    return out

def frame_for(kind):
    box, top = handoff.SLOT_BOX[handoff.ITEM_SLOT[kind]]; u = box / 64.0
    return handoff.HEAD_R_PX / u, (handoff.HEAD_CY_PX - top - 32.0 * u) / handoff.HEAD_R_PX

def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))

def hex_rgb(h):
    h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))

def materials(kind, brass=False):
    """재질색 M/M2 — 카탈로그(.asset tone0/1)와 같은 값(palette_model.ITEMS). brass=True 면 시트 B 입력(브라스 단색)."""
    if brass: return BRASS, BRASS
    it = PM.BY_KIND[kind]; return hex_rgb(it[4]), hex_rgb(it[5])

def render_production(kind, rows, cell, brass=False, ink=INK, bg=BG):
    """AccessoryCardIcon.BuildDesigned 의 파이썬 재구현(계약 v2 + 재질 팔레트 §2-3: 연속 배수 · 역할 표 · 사전 합성)."""
    m, m2 = materials(kind, brass)
    upr, cy = frame_for(kind)
    px_per_r = upr * (cell / 64.0)
    base_stroke = cell * 2.2 / 64.0
    cv = RZ.Canvas(cell, cell, bg)
    flat = {}
    for i, r in enumerate(rows):
        pts = [(cell / 2 + x * px_per_r, cell / 2 - (y - cy) * px_per_r) for x, y in r["pts"]]
        under = flat.get(i - r["under"], bg) if r["under"] > 0 else bg
        sw = base_stroke * (r["mult"] if r["mult"] > 0 else 1.0)
        if r["filled"]:
            if r["tone"] == TONE_ACCENT: base = m2
            elif r["tone"] == TONE_SHADE: base = tuple(int(round(v * SHADE)) for v in m)
            elif r["tone"] == TONE_HEADINK: base = bg          # 카드 팔레트의 머리 채움 = 카드 바탕(CardSurfaceMuted)
            elif r["tone"] == TONE_INKCONTRAST: base = ink
            else: base = m
            a = r["alpha"] if r["alpha"] > 0 else M.GRAD_MEAN
            if r["tone"] == TONE_GLASS: base, a = lerp(bg, m2, a), 1.0   # R20 유리 판 — 바탕 위 M2 α 사전 합성, 결과 불투명
            fill = lerp(under, base, a); cv.fill(pts, fill); flat[i] = fill
        if r["no_stroke"]: continue
        if r["tone"] == TONE_HIGHLIGHT: lbase = (255, 255, 255)
        elif not r["filled"] and r["tone"] == TONE_PRIMARY: lbase = m      # 재질선(R-3)
        elif not r["filled"] and r["tone"] == TONE_ACCENT: lbase = m2
        else: lbase = ink
        la = r["line_alpha"] if r["line_alpha"] > 0 else 1.0
        cv.stroke(pts, sw, lerp(under, lbase, la), r["loop"])
    return cv.done()

def diff_stat(a, b):
    """두 셀의 픽셀 차 — 평균 절대차(0~255)와 8 이상 차이 난 픽셀 비율."""
    pa, pb = a.convert("RGB").load(), b.convert("RGB").load()
    w, h = a.size; tot = 0; big = 0
    for y in range(h):
        for x in range(w):
            d = sum(abs(pa[x, y][c] - pb[x, y][c]) for c in range(3)) / 3.0
            tot += d
            if d >= 8: big += 1
    return tot / (w * h), big / (w * h)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--unity", default=None, help="CardIconProbe 출력 폴더(brass_232_*.png)")
    ap.add_argument("--out", default=os.path.join(REPO, "docs/verify/card-r16/card_sheet.png"))
    ap.add_argument("--no-chrome", action="store_true", help="A 열 생략(Chrome 없을 때)")
    ap.add_argument("--brass", action="store_true", help="P 열을 브라스 단색 입력으로(시트 B 와 형태 대조용). 기본은 재질 팔레트 색")
    a = ap.parse_args()

    prod = production_pieces()
    cards_a = None if a.no_chrome else S.render_handoff_cards(CELL)
    unity = {}
    if a.unity:
        for kind in M.KINDS:
            slot = handoff.OUR_NAME[kind][0]
            cands = sorted(f for f in os.listdir(a.unity) if f.startswith("brass_%d_%s_%d_" % (CELL, slot, IDX[kind])))
            if cands: unity[kind] = Image.open(os.path.join(a.unity, cands[0])).convert("RGB")

    cols = [("A 인계본(Chrome)", cards_a is not None), ("B 설계 모델(R15)", True), ("P 프로덕션 데이터", True), ("U Unity 실제 경로", bool(unity))]
    cols = [c for c in cols if c[1]]
    NOTE_W = 360; GAP = 10; HEADER = 70; ROW = CELL + 34
    W = GAP + len(cols) * (CELL + GAP) + NOTE_W; H = HEADER + ROW * len(M.KINDS) + GAP
    im = Image.new("RGB", (W, H), (250, 250, 248)); d = ImageDraw.Draw(im)
    S.label(d, (GAP, 8), "카드 16종 — 인계본 ↔ 설계 모델 ↔ 프로덕션 데이터 ↔ Unity 실제 경로 (coder, 2026-09-05). 입력색 전부 브라스 #C8A15A · 바탕 #15181E", 15, bold=True)
    S.label(d, (GAP, 30), "P 는 생성 C#/에셋의 카드 표면 조각을 되읽어 AccessoryCardIcon.BuildDesigned 식을 파이썬으로 다시 적은 것. B·P 가 같은 래스터이므로 둘의 차 = 데이터/식의 차(오른쪽 수치).", 12, (70, 70, 70))
    S.label(d, (GAP, 48), "★ B·P 는 오프라인 래스터(둥근 캡). U 가 실제 uGUI 렌더다 — 최종 판정은 U 로.", 12, (150, 40, 40))
    report = []
    for i, k in enumerate(M.KINDS):
        y = HEADER + ROW * i
        S.label(d, (GAP, y), "%s  %s / %s" % (M.KO[k], k, M.SLOT[k]), 14, bold=True)
        y0 = y + 22; x = GAP
        b_im = S.render_our_card(k, CELL)
        p_im = render_production(k, prod[k], CELL, brass=a.brass)
        images = []
        if cards_a is not None: images.append(cards_a[k])
        images.append(b_im); images.append(p_im)
        if unity: images.append(unity.get(k) or Image.new("RGB", (CELL, CELL), (60, 60, 60)))
        for (name, _), img in zip(cols, images):
            im.paste(img, (x, y0)); S.label(d, (x + 4, y0 + 2), name[0], 12, (200, 200, 200), bold=True); x += CELL + GAP
        mean, frac = diff_stat(b_im, p_im)
        lines = ["B↔P 평균차 %.2f/255 · ≥8 차 픽셀 %.2f%%" % (mean, 100 * frac),
                 "카드 조각 %d (모델 %d)" % (len(prod[k]), len(M.CARD_SET[k]))]
        if unity and k in unity:
            um, uf = diff_stat(p_im, unity[k]); lines.append("P↔U 평균차 %.2f/255 · ≥8 차 %.2f%%" % (um, 100 * uf))
        RZ.text_block(d, (x, y0), lines, size=12, color=(50, 50, 50))
        report.append((k, mean, frac))
        print("%-13s B↔P mean %.3f  frac %.4f  pieces %d/%d" % (k, mean, frac, len(prod[k]), len(M.CARD_SET[k])))
    os.makedirs(os.path.dirname(a.out), exist_ok=True)
    im.save(a.out); print("wrote", a.out, im.size)
    worst = max(report, key=lambda r: r[1])
    print("B↔P 최악: %s mean %.3f frac %.4f" % worst)

if __name__ == "__main__":
    main()
