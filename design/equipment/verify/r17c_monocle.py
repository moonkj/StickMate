# -*- coding: utf-8 -*-
"""R17c — 외알안경 행만 담은 시트 r17c_monocle.png (알 중심 +0.46 R = 반대쪽 눈 거울 위치). 열 C | C* | D′ | D″ | E.
    python3 r17c_monocle.py     # ★ Chrome 필요
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r16_worn_sheet as S16
import r17_sheet as S17

def main():
    worn_c = S16.render_handoff_worn(S17.R_PX, tag="c17c")
    worn_cf = S16.render_handoff_worn(S17.R_PX, faithful=True, tag="cf17c")
    im = S17.build_rows_sheet(["monocle"], "R17c — 외알안경: 알 중심 = 반대쪽 눈의 거울 위치 (+0.46, +0.185) R · 사슬·구슬은 알 상대 오프셋 유지(바깥쪽) · 반대쪽 눈(착용 시에만)", worn_c, worn_cf)
    im.save(os.path.join(HERE, "r17c_monocle.png")); print("wrote r17c_monocle.png %dx%d" % im.size)

if __name__ == "__main__":
    main()
