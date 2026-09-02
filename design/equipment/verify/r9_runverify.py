# -*- coding: utf-8 -*-
"""R9 — A군 처방(h = 0.46 R 채운 띠 + 베레모 왼끝 물림)을 설치하고 **verify.py 전량**을 돌린다.
   30종 규칙 + 남는 머리 + 배율 축. 프로덕션 .cs 도 설계 거울 파일도 안 건드린다.

     python3 r9_runverify.py            처방
     python3 r9_runverify.py --current  현행(대조 기준선)
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import items, hair
if "--current" not in sys.argv:
    import r9_bandfix
    r9_bandfix.install()
    print("### 처방 설치됨 — A군 5종 띠 h = %.2f R (베레모 왼끝 물림 포함)\n" % r9_bandfix.H_BAND)
else:
    print("### 현행 좌표 (기준선)\n")
src = open(os.path.join(HERE, "verify.py"), encoding="utf-8").read()
src = src.replace("import rig, items, hair\n", "import rig\nimport items, hair\n")
exec(compile(src, "verify.py", "exec"), {"__name__": "__main__"})
