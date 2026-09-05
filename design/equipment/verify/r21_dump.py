# -*- coding: utf-8 -*-
"""R21 — r20_coords.txt 를 다시 굽는다: R20 (가)군 5종 + R20 렌즈 추가분(그대로) + (나)군 4종 + (다)군 18종.  python3 r21_dump.py
카드 좌표는 인계본과 같은 **64u 아이콘 단위**(슬롯 박스 프레이밍 HEAD 70/EYES 48/BACK 88 · HAIR 는 1 R = 16u 머리 중심 (32,30) · FX/PET 는 64u 상자 전체).
몸 좌표는 R(머리 중심 원점 · y 위로 · +x 보는 사람 오른쪽)."""
import os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig
import palette_model as PM
import r20_dump as D20
import r21_model as M
import r21_theme as TH

OUT = os.path.join(HERE, "r20_coords.txt")

def body_line(f, k, p):
    fb, _, lb = M.resolve(M.ITEM_ID[k], p.crole, "body", PM.INK_BLACK); fw, _, lw = M.resolve(M.ITEM_ID[k], p.crole, "body", PM.INK_WHITE)
    x0, y0, x1, y1 = rig.bounds(p.pts)
    la = M.line_alpha(p)
    f.write("  %-24s %-5s fill=%-18s line=%-4s | 검 %s/%s | 흰 %s/%s | %-5s filled=%d loop=%d cond=%-11s ×%.2f 명목 %.4f 실폭 %.4f 선α %.2f 잉크 %.3f×%.3fR %s\n" % (
        p.name, p.layer, str(p.crole[0]), str(p.crole[1]), fb, lb, fw, lw, p.call, int(p.filled), int(p.loop), p.conditional or "-",
        p.mult, p.nominal_R, p.width_R(0.75), la, x1 - x0, y1 - y0, p.transform))
    f.write("      " + " ".join("(%.4f,%.4f)" % q for q in p.pts) + "\n")

def card_line(f, k, p):
    fc, fa, lc = M.resolve(M.ITEM_ID[k], p.crole, "card", PM.CARD_INK)
    x0, y0, x1, y1 = rig.bounds(p.pts)
    f.write("  %-24s card  fill=%-18s line=%-4s | %s α%.2f / %s | %-5s filled=%d loop=%d ×%.2f 선α %.2f 잉크 %.1f×%.1fu  %s\n" % (
        p.name, str(p.crole[0]), str(p.crole[1]), fc, fa, lc, p.call, int(p.filled), int(p.loop), p.mult, M.line_alpha(p), x1 - x0, y1 - y0, p.role))
    f.write("      " + " ".join("(%.2f,%.2f)" % q for q in p.pts) + "\n")

def dump_r21(path):
    with open(path, "a", encoding="utf-8") as f:
        f.write("#" * 78 + "\n# R21 — (나)군 4종 재저작 + (다)군 18종 창작 (design-equipment, 2026-09-05). 정본 r21_model.py → r21_model.out.txt · 시트 r21_sheet.png / r21_card_sheet.png\n")
        f.write("# 몸: R 단위(머리 중심 원점). 카드: **64u 아이콘 단위**(인계본 ItemIcon 과 같은 viewBox 64 · 획 2.2u × 배수) — HEAD 슬롯 박스 70px/top 6 · EYES 48/38 · BACK 88/72 · HAIR 1 R = 16u 머리 중심 (32,30) · FX/PET 64u 상자.\n")
        f.write("# 모자 몸 = 카드 좌표 × HAT_FIT(u, dy): x_R = (x−32)·u · y_R = dy − y·u (§14-10-4 #16). 뿔테 몸 = EYES 슬롯 변환(1u = 0.0339 R). 판초 몸 = 무대 도형(카드와 별개). 머리카락 카드 = 몸 좌표 × HAIR 프레이밍.\n")
        f.write("# 색 해석: fill M/M2/SH/EYE/INKF(잉크 표식 채움)/CARDWASH(M2,α: 카드 워시 · 몸 GLASS 사전 합성)/None · line INK/M/M2/W/None · 선α = 채움 위 잉크 낱선의 인계본 α(R-4). 등급색 0.\n")
        f.write("# ── 변환 표 ──\n")
        for k, fit in (("straw", M.STRAW_FIT), ("beret", M.BERET_FIT)):
            f.write("#   %-6s HAT_FIT u=%.4f ky=1.00 dy=%+.4f (H-2, 앞층만의 합집합으로 맞춤 · u 하한 %s) · 착용선 %+.3f 꼭대기 %+.3f 밑 %+.3f 폭 %.2f R\n" % (
                k, fit["u"], fit["dy"], "0.075(챙 ≥ 4.5 R)" if k == "straw" else "0.070(몸 ≥ 3.0 R)", fit["wear"], fit["top"], fit["bottom"], fit["width"]))
        f.write("#   browline EYES 슬롯 변환 · 렌즈 몸 채움 = GLASS(잉크 위 M2 #20878C α0.16) · poncho 뒤판 밑단 %.2f R 폭 %.2f R · 앞자락 V −2.40 R · 술 4(x %s, 길이 %.2f R)\n" % (
            M.PONCHO_HEM - 0.13, 2 * M.PONCHO_HEM_HW, ", ".join("%+.2f" % x for x in M.PONCHO_FRINGE_X), M.PONCHO_FRINGE_LEN))
        f.write("#   hair 뒤층(back) = 머리·몸 뒤(SortBack) — neat.Fall · ponytail.Tail. 모자 커버선(HatCoverLocalY)은 프로덕션 절단 그대로(y > cover 잘림).\n\n")
        for k in M.ORDER_NA + M.ORDER_HAIR:
            it = M.PAL[M.ITEM_ID[k]]
            f.write("=" * 78 + "\n%s  %s / %s   M %s  M2 %s\n-- BODY\n" % (M.ITEM_ID[k], M.KO[k], M.SLOT[k], it[4], it[5]))
            for p in M.BODY[k]: body_line(f, k, p)
            f.write("-- CARD (64u 아이콘 단위%s)\n" % (" · HAIR 프레이밍 1 R = 16u, 머리 중심 (32,30)" if k in M.HAIR else (" · 슬롯 박스 %s" % M.SLOT[k])))
            for p in M.CARD[k]: card_line(f, k, p)
            f.write("\n")
        for k in M.ORDER_FXPET:
            it = M.PAL[M.ITEM_ID[k]]
            f.write("=" * 78 + "\n%s  %s / %s   M %s  M2 %s   (몸 효과·펫 기하는 손대지 않는다 — appearance.py 현행)\n-- CARD (64u 아이콘 단위 · 상자 전체)\n" % (M.ITEM_ID[k], M.KO[k], M.SLOT[k], it[4], it[5]))
            for p in M.CARD[k]: card_line(f, k, p)
            f.write("\n")

def add_theme_fields(path):
    """R21 테마 배정(r21_theme.py 안 B): 아이템 헤더 줄 끝에 theme=… 을 붙인다. 외형 18종은 none(무소속). 렌즈 절(roundglasses/monocle)은 kind 로 시작해 id 로 찾는다."""
    kind_id = {"roundglasses": "equip.eyes.round", "monocle": "equip.eyes.monocle"}
    lines = open(path, encoding="utf-8").read().split("\n"); out = []; n = 0
    straw_seen = 0
    for ln in lines:
        m = re.match(r"^((equip|look)\.[a-z_.]+)  ", ln)
        if m:
            ln = ln + "   theme=%s" % TH.theme_of(m.group(1)); n += 1
            if m.group(1) == "equip.head.straw":
                straw_seen += 1
                if straw_seen == 1: ln = ln + "   ★ R20 임시 좌표(3/4 챙) — 아래 R21 절이 대체한다. 전사 금지"
        else:
            m2 = re.match(r"^(roundglasses|monocle)  ", ln)
            if m2: ln = ln + "   theme=%s" % TH.theme_of(kind_id[m2.group(1)]); n += 1
        out.append(ln)
    hdr = "# theme = R21 테마 배정 안 B(r21_theme.py / r21_theme.out.txt — 인계본 16종 원문 파싱 + E1~E3 검산). 스탯 4슬롯 24종만 세트 계산, 외형 18종 = none(무소속). 리더 채택 대기.\n"
    out.insert(3, hdr)
    open(path, "w", encoding="utf-8").write("\n".join(out)); return n

if __name__ == "__main__":
    D20.dump(OUT)            # R20 (가)군 5종 — 그대로
    D20.dump_lens(OUT)       # R20 렌즈 추가분 — 그대로
    dump_r21(OUT)
    n = add_theme_fields(OUT)
    m = sum(1 for _ in open(OUT, encoding="utf-8"))
    print("wrote r20_coords.txt (%d lines) — R20 (가)+렌즈 + R21 (나)(다) · theme 필드 %d개" % (m, n))
