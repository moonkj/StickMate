# -*- coding: utf-8 -*-
"""R16 — 착용 표면 좌표 전문 → r16_coords.txt.  python3 r16_dump.py
정본은 r16_model.py(인계본 HTML 파싱 + README 오버레이 명세)다 — 이 텍스트를 손으로 고치지 마라."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import rig
import r16_model as M

def dump(path):
    with open(path, "w", encoding="utf-8") as f:
        f.write("# R16 착용 표면 좌표 전문 — R 단위(머리 중심 원점 · y 위로 · +x 진행 방향), 소수 4자리\n")
        f.write("# 생성: python3 r16_dump.py   (r16_model.py 가 정본이다 — 이 파일을 손으로 고치지 마라)\n")
        f.write("# 착용 기하 = 인계본 착용 프리뷰 그대로(정면 아이콘 오버레이 + 망토 무대 도형). 카드 표면(R15 CARD_SET)과 좌표 동일.\n")
        f.write("# 획: 명목(R) = 슬롯 획(head .1384 / eyes .1153 / neck .1297 / back .1243 R, 무대 2 = .0714 R) × 조각 배수.\n")
        f.write("#     실폭(R) = max(명목, 1pt/머리반경pt)  — 배율 0.75: 1pt = 0.1719 R · 1.00: 0.1289 R · 0.35: 0.3684 R\n")
        f.write("# 색(브라스 단색): ink=#C8A15A(UiChrome.Accent) accent=#C8A15A white=#FFFFFF · 망토 무대 고정색 #A8332A/#8E241C/#7E1F17/#C8A15A\n")
        f.write("# 필드: name role call filled loop layer groupα mult 명목R 실폭R@.75 실폭R@1.0 fill line dash 생존(.75/1.0/.60/.35)\n\n")
        rows = {(r["kind"], r["p"].src): r for r in M.classify()}
        for k in M.KINDS:
            f.write("=" * 78 + "\n%s  %s / %s  (인계본 슬롯 박스 %s)\n" % (k, M.KO[k], M.SLOT[k], M.ISLOT[k]))
            for p in M.WORN[k]:
                r = rows[(k, p.src)]; x0, y0, x1, y1 = rig.bounds(p.pts)
                f.write("  %-24s %-6s %-5s filled=%d loop=%d layer=%-5s gα=%.2f ×%.2f 명목 %.4f 실폭 %.4f/%.4f fill=%s line=%s dash=%s 잉크 %.3f×%.3fR 생존 %s/%s/%s/%s\n" % (
                    p.name, p.role, p.call, int(p.filled), int(p.loop), p.layer, p.group_alpha, p.mult, p.nominal_R,
                    p.width_R(0.75), p.width_R(1.0), p.fill, p.line, p.dash, x1 - x0, y1 - y0,
                    "O" if r["ok75"] else "X", "O" if r["ok100"] else "X", "O" if r["ok60"] else "X", "O" if r["ok35"] else "X"))
                f.write("      " + " ".join("(%.4f,%.4f)" % q for q in p.pts) + "\n")
            f.write("\n")

if __name__ == "__main__":
    dump(os.path.join(HERE, "r16_coords.txt"))
    print("wrote r16_coords.txt")
