# -*- coding: utf-8 -*-
"""R19 — 좌표 + 층 + 색 역할 전문 → r19_coords.txt (코더 기계 전사용: 색은 palette_model.ROLE(+R19 정정)에서, 좌표는 여기서).
    python3 r19_dump.py"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig
import r16_model as M16
import r17_model as M17
import palette_model as PM
import r19_model as M

def dump(path):
    with open(path, "w", encoding="utf-8") as f:
        f.write("# R19 착용(몸) + 카드 표면 좌표·층·색 역할 전문 — R 단위(머리 중심 원점 · y 위로 · +x 보는 사람 오른쪽), 소수 4자리\n")
        f.write("# 생성: python3 r19_dump.py  (정본: 좌표 r19_model.py → r17_model.py → 인계본 HTML · 색 역할 palette_model.ROLE + r19_model.ROLE_FIX)\n")
        f.write("# 색 해석: fill M=material · M2=material2 · SH=M×0.28 · EYE=잉크 대비색(흰/목탄 #252829) · CARDWASH(M2,α)=카드만 · None=채움 없음\n")
        f.write("#          line INK=잉크(카드 #E8E2D6 · 몸 유저 잉크, 인계본 α 유지) · M/M2=재질선 α1 · W=흰 α0.42 · None=윤곽 없음\n")
        f.write("# 층: back = 머리·몸 뒤(SortBack) · front = 앞. 그룹 α 1.0(날개·배낭). 등급색 0.\n")
        f.write("# ── 몸 표면 변환 표 ──\n")
        for k in M17.HEAD_KINDS:
            fh = M17.HAT_FIT[k]
            f.write("#   %-9s HAT_FIT u=%.4f ky=%.2f dy=%+.4f 착용선 %+.3f 꼭대기 %+.3f 밑 %+.3f%s\n" % (
                k, fh["u"], fh["ky"], fh["dy"], fh["wear"], fh["top"], fh["bottom"], (" 골 %+.3f (R19 재맞춤)" % fh["dip"]) if "dip" in fh else ""))
        f.write("#   clothhat/fedora 챙 B2 → B2far(뒤층 채움)+B2fa(뒤층 위 호 윤곽) / B2near(앞층 채움)+B2na(앞층 아래 호 윤곽) — 장축 y=41/41.5u 에서 분할, 자른 선 안 그림\n")
        f.write("#   crown BW 뒷벽(뒤층): B0 옆선 사이 y 40.5u~21u\n")
        f.write("#   monocle 알 (+0.46,+0.185)R MIRROR+SHIFT(R17c) · 구슬 CB3 r %.4f R M2 윤곽 없음 · ExposedEye 조건부(착용 시)\n" % M.BEAD_R)
        f.write("#   stripedtie dy %+.4f · bowtie dy %+.2f\n" % (M17.TIE_DY, M.BOWTIE_DY))
        f.write("#   backpack 몸 = B1 뒷판(뒤층) + S0 손잡이(뒤층) + STRAP_L/R(앞층, M2 독립선 ×%.1f) · 카드에만: %s\n" % (M.STRAP_MULT, ", ".join(sorted(M.BACKPACK_CARD_ONLY))))
        f.write("#   shortcape/longcape 무대 뒤판 R17d(밑단 %.3f / %.3f R) · sway 정점 16..32 · 긴망토 밑단 클램프(B-3)\n" % (M17.HEM_SHORT_NEW, M17.HEM_LONG_NEW))
        f.write("# 필드: name layer fill_role line_role | 검은잉크 fill/line | 흰잉크 fill/line | call filled loop cond mult 명목R 실폭@.75 잉크 transform\n\n")
        for k in M.KINDS:
            it = PM.BY_KIND[k]
            f.write("=" * 78 + "\n%s  %s / %s   M %s  M2 %s\n" % (k, M.KO[k], M.SLOT[k], it[4], it[5]))
            f.write("-- BODY\n")
            for p in M.WORN[k]:
                fb, _, lb = M.resolve(k, p.crole, "body", PM.INK_BLACK); fw, _, lw = M.resolve(k, p.crole, "body", PM.INK_WHITE)
                x0, y0, x1, y1 = rig.bounds(p.pts)
                f.write("  %-24s %-5s fill=%-16s line=%-4s | 검 %s/%s | 흰 %s/%s | %-5s filled=%d loop=%d cond=%-12s ×%.2f 명목 %.4f 실폭 %.4f 잉크 %.3f×%.3fR %s\n" % (
                    p.name, p.layer, str(p.crole[0]), str(p.crole[1]), fb, lb, fw, lw, p.call, int(p.filled), int(p.loop), p.conditional or "-",
                    p.mult, p.nominal_R, p.width_R(0.75), x1 - x0, y1 - y0, p.transform))
                f.write("      " + " ".join("(%.4f,%.4f)" % q for q in p.pts) + "\n")
            f.write("-- CARD (인계본 아이콘 좌표, R 단위 — R15/R16 과 동일)\n")
            for p in M.CARD[k]:
                fc, fa, lc = M.resolve(k, p.crole, "card", PM.CARD_INK)
                f.write("  %-24s card  fill=%-16s line=%-4s | %s α%.2f / %s | %-5s ×%.2f\n" % (p.name, str(p.crole[0]), str(p.crole[1]), fc, fa, lc, p.call, p.mult))
            f.write("\n")

if __name__ == "__main__":
    dump(os.path.join(HERE, "r19_coords.txt"))
    print("wrote r19_coords.txt")
