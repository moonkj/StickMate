# -*- coding: utf-8 -*-
"""R17b — 외알안경 행만 담은 시트 r17b_monocle.png (열 C | C* | D′ | D″ | E, r17_sheet 와 같은 렌더 경로).
    python3 r17b_monocle.py     # ★ Chrome 필요
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import r16_worn_sheet as S16
import r17_sheet as S17

def main():
    worn_c = S16.render_handoff_worn(S17.R_PX, tag="c17b")
    worn_cf = S16.render_handoff_worn(S17.R_PX, faithful=True, tag="cf17b")
    im = S17.build_rows_sheet(["monocle"], "R17b — 외알안경: 알·하이라이트 반전(오른쪽) + 사슬·구슬은 알 상대 오프셋 유지(바깥쪽 오른쪽 아래) + 반대쪽 눈(착용 시에만)", worn_c, worn_cf)
    im.save(os.path.join(HERE, "r17b_monocle.png")); print("wrote r17b_monocle.png %dx%d" % im.size)

if __name__ == "__main__":
    main()
