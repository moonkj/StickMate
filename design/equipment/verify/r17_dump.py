# -*- coding: utf-8 -*-
"""R17 — 몸 표면 좌표 전문 + 변환 표 → r17_coords.txt (코더 기계 전사용).  python3 r17_dump.py
정본은 r17_model.py(→ r16_model.py → 인계본 HTML 파싱)다 — 이 텍스트를 손으로 고치지 마라."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig
import r16_model as M16
import r17_model as M

def dump(path):
    rows = {(r["kind"], r["p"].src): r for r in M.survival_rows()}
    with open(path, "w", encoding="utf-8") as f:
        f.write("# R17 착용(몸) 표면 좌표 전문 — R 단위(머리 중심 원점 · y 위로 · +x = 보는 사람 오른쪽/진행 방향), 소수 4자리\n")
        f.write("# 생성: python3 r17_dump.py   (r17_model.py 가 정본 — 손으로 고치지 마라). 카드 표면은 R15/R16 그대로(여기 없음).\n")
        f.write("#\n# ── 몸 표면 변환 표 (아이콘 64 단위 → R) ──\n")
        f.write("#   공통: x_R = (x − 32)·u,  y_R = dy − y·u·ky.   카드/R16 값: HEAD u %.5f(top 6px) · EYES %.5f · NECK %.5f · BACK %.5f\n" % (
            M.ICON_U_HEAD, M16.handoff.icon_to_R("monocle")[2], M16.handoff.icon_to_R("bowtie")[2], M16.handoff.icon_to_R("wings")[2]))
        for k in M.HEAD_KINDS:
            fh = M.HAT_FIT[k]
            f.write("#   %-9s HAT_FIT  u=%.4f (카드 ×%.3f)  ky=%.2f  dy=%+.4f   착용선 %+.3f  꼭대기 %+.3f  밑 %+.3f  폭 %.3f R   불투명 바탕 = 머리 채움색(잉크)\n" % (
                k, fh["u"], fh["u"] / M.ICON_U_HEAD, fh["ky"], fh["dy"], fh["wear"], fh["top"], fh["bottom"], fh["width"]))
        f.write("#   monocle   알 중심 = 반대쪽 눈 거울 위치 (%+.2f, +0.185) R = 아이콘 u %.2f · MIRROR x→64−x 뒤 +%.2fu (알 CB0 · 하이라이트 H1) · SHIFT x+%.2fu (사슬 S2 · 구슬 CB3 — 알 상대 오프셋 유지, 바깥쪽 오른쪽 아래로; 구슬은 슬롯 64 를 넘지만 몸 표면엔 클리핑 없음) · 조건부 조각 ExposedEye(착용 시에만) 흰 12각 r %.4f R @(%+.2f, %+.3f) · WornColor 우회\n" % (
            M.MONOCLE_LENS_CX_R, M.MONOCLE_TARGET_CX_ICON, M.MONOCLE_MIRROR_EXTRA, M.MONOCLE_DX_ICON, M.EYE_R, M.EYE_X, M.EYE_Y))
        f.write("#   stripedtie SHIFT dy=%+.4f R (매듭 윗변 −1.013 → %+.3f R)\n" % (M.TIE_DY, M.TIE_KNOT_TOP_TARGET))
        f.write("#   shortcape  StageBack := 인계본 긴망토 뒤판 그대로 (밑단 %.3f R · 폭 %.3f R) · 칼라·걸쇠 그대로 · sway 정점 %d..%d\n" % (
            M.HEM_SHORT_NEW, M.HEM_W_LONG_OLD, M.CAPE_SWAY[0], M.CAPE_SWAY[0] + M.CAPE_SWAY[1] - 1))
        f.write("#   longcape   StageBack = 인계본 긴망토 뒤판을 위(y %.3f) 고정 · 세로 ×%.4f · 폭 깊이비례 ×(1→%.4f) → 밑단 %.4f R(발목 %.4f + 다리 획 반폭) · 밑단 폭 %.4f R(플레어 %.4f R/R) · sway 정점 %d..%d · 밑단 바닥 클램프 필요(§14-10-7)\n" % (
            M.CAPE_TOP_Y, M.KY_LONG, M.KW_LONG, M.HEM_LONG_NEW, M.ANKLE_Y, M.HEM_W_LONG_NEW, M.FLARE, M.CAPE_SWAY[0], M.CAPE_SWAY[0] + M.CAPE_SWAY[1] - 1))
        f.write("#   그 밖 10종: 변환 없음(R16 = 인계본 착용 프리뷰 그대로)\n")
        f.write("#   획: 명목(R) = 슬롯 획(head .1384 / eyes .1153 / neck .1297 / back .1243, 무대 .0714) × 배수 → 실폭 = max(명목, 1pt/머리반경pt)\n")
        f.write("#   색: line ink=#C8A15A(브라스) · fill accent=등급색(일반 #8A8F98 가정) · white=#FFFFFF · headfill=머리 채움색 · solid=고정 hex\n")
        f.write("# 필드: name role call base cond filled loop layer gα mult 명목R 실폭@.75 실폭@1.0 fill line dash 잉크 생존(.75/1.0/.60/.35) transform\n\n")
        for k in M.KINDS:
            f.write("=" * 78 + "\n%s  %s / %s\n" % (k, M.KO[k], M.SLOT[k]))
            for p in M.WORN[k]:
                x0, y0, x1, y1 = rig.bounds(p.pts)
                r = rows.get((k, p.src))
                surv = ("%s/%s/%s/%s" % tuple("O" if r[key] else "X" for key in ("ok75", "ok100", "ok60", "ok35"))) if r else "-"
                f.write("  %-26s %-10s %-4s base=%d cond=%-12s filled=%d loop=%d layer=%-5s gα=%.2f ×%.2f 명목 %.4f 실폭 %.4f/%.4f fill=%s line=%s dash=%s 잉크 %.3f×%.3fR 생존 %s  %s\n" % (
                    p.name, p.role, p.call, int(p.base), p.conditional or "-", int(p.filled), int(p.loop), p.layer, p.group_alpha, p.mult,
                    p.nominal_R, p.width_R(0.75), p.width_R(1.0), p.fill, p.line, p.dash, x1 - x0, y1 - y0, surv, p.transform))
                f.write("      " + " ".join("(%.4f,%.4f)" % q for q in p.pts) + "\n")
            f.write("\n")

if __name__ == "__main__":
    dump(os.path.join(HERE, "r17_coords.txt"))
    print("wrote r17_coords.txt")
