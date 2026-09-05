# -*- coding: utf-8 -*-
"""R20 — (가)군 5종 좌표 + 층 + 색 역할 전문 → r20_coords.txt (r19 와 같은 형식).  python3 r20_dump.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig
import palette_model as PM
import r20_model as M

def dump(path):
    with open(path, "w", encoding="utf-8") as f:
        f.write("# R20 (가)군 5종 — 몸 표면 좌표·층·색 역할 전문 (R 단위 · 머리 중심 원점 · y 위로 · +x 보는 사람 오른쪽). 카드 = 같은 좌표(봉투 ×0.86 맞춤, 인계본 아이콘 없음)\n")
        f.write("# 생성: python3 r20_dump.py  (정본 r20_model.py → 거울 items.py(현행 프로덕션 도형) + palette_model.ITEMS 재질색)\n")
        f.write("# 색 해석: fill M/M2/SH/EYE/None · line INK(잉크)/M/M2/W(흰 α0.42)/None. 층 back = 머리·몸 뒤. 등급색 0. 획: 슬롯 명목 × 배수, 1pt 하한.\n")
        f.write("# ── 변환 표 ──\n")
        f.write("#   straw    HAT_FIT u=%.3f dy=%+.3f (H-2, 균일 배율) · H-1 미적용(임시 — 챙 재저작 뒤) · 착용선 %+.3f 꼭대기 %+.3f 밑 %+.3f · 등급 (나)\n" % (
            M.STRAW_FIT["u"], M.STRAW_FIT["dy"], M.STRAW_FIT["wear"], M.STRAW_FIT["top"], M.STRAW_FIT["bottom"]))
        f.write("#   pendant  SHIFT dy=%+.3f (N-1) · bandana SHIFT dy=%+.3f (N-1) · patch ExposedEye 조건부(착용 시) r 0.1719 @(−0.46,+0.107) · fairywings 그룹 α 1.0\n" % (M.PENDANT_DY, M.BANDANA_DY))
        f.write("# 필드: name layer fill_role line_role | 검은잉크 fill/line | 흰잉크 fill/line | filled loop cond mult 명목R 실폭@.75 잉크 transform\n\n")
        for k, ps in M.GA.items():
            it = M.PAL[M.GA_ID[k]]
            f.write("=" * 78 + "\n%s  %s / %s   M %s  M2 %s\n" % (M.GA_ID[k], M.GA_KO[k], M.GA_SLOT[k], it[4], it[5]))
            for p in ps:
                fb, _, lb = M.resolve(k, p.crole, "body", PM.INK_BLACK); fw, _, lw = M.resolve(k, p.crole, "body", PM.INK_WHITE)
                x0, y0, x1, y1 = rig.bounds(p.pts)
                f.write("  %-24s %-5s fill=%-5s line=%-4s | 검 %s/%s | 흰 %s/%s | filled=%d loop=%d cond=%-11s ×%.2f 명목 %.4f 실폭 %.4f 잉크 %.3f×%.3fR %s\n" % (
                    p.name, p.layer, str(p.crole[0]), str(p.crole[1]), fb, lb, fw, lw, int(p.filled), int(p.loop), p.conditional or "-",
                    p.mult, p.nominal_R, p.width_R(0.75), x1 - x0, y1 - y0, p.transform))
                f.write("      " + " ".join("(%.4f,%.4f)" % q for q in p.pts) + "\n")
            f.write("\n")

def dump_lens(path):
    """R20 추가 — EyesVisorOpacityTests 충돌 해소분(동그란안경·외알안경 몸 표면)을 r19 정본에서 그대로 옮겨 적는다."""
    import r19_model as M19
    with open(path, "a", encoding="utf-8") as f:
        f.write("#" * 78 + "\n# R20 추가 — 몸 표면 렌즈류 최소 커버리지(EyesVisorOpacityTests): 유리 렌즈 = GLASS(잉크 위 M2 α 사전 합성, 불투명 Filled) · 외알안경 알·눈 ±%.2f R\n" % M19.MONOCLE_CX_R20)
        f.write("#   (정본 r19_model.py / r19_coords.txt 와 동일. 색 역할 CARDWASH(M2,α) 의 몸 해석이 None → GLASS 로 바뀌었다.)\n\n")
        for k in ("roundglasses", "monocle"):
            it = PM.BY_KIND[k]
            f.write("=" * 78 + "\n%s  %s / EYES   M %s  M2 %s\n-- BODY\n" % (k, M19.KO[k], it[4], it[5]))
            for p in M19.WORN[k]:
                fb, _, lb = M19.resolve(k, p.crole, "body", PM.INK_BLACK); fw, _, lw = M19.resolve(k, p.crole, "body", PM.INK_WHITE)
                x0, y0, x1, y1 = rig.bounds(p.pts)
                f.write("  %-24s %-5s fill=%-16s line=%-4s | 검 %s/%s | 흰 %s/%s | filled=%d loop=%d cond=%-12s ×%.2f 명목 %.4f 실폭 %.4f 잉크 %.3f×%.3fR %s\n" % (
                    p.name, p.layer, str(p.crole[0]), str(p.crole[1]), fb, lb, fw, lw, int(p.filled), int(p.loop), p.conditional or "-",
                    p.mult, p.nominal_R, p.width_R(0.75), x1 - x0, y1 - y0, p.transform))
                f.write("      " + " ".join("(%.4f,%.4f)" % q for q in p.pts) + "\n")
            f.write("\n")

if __name__ == "__main__":
    dump(os.path.join(HERE, "r20_coords.txt"))
    dump_lens(os.path.join(HERE, "r20_coords.txt"))
    print("wrote r20_coords.txt")
