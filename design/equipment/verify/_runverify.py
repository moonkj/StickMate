# -*- coding: utf-8 -*-
"""처방을 설치한 뒤 verify.py **전량**을 그대로 돌린다(30종 규칙 + 남는 머리 + 배율 축).
   python3 _runverify.py            처방
   python3 _runverify.py --current  현행(대조)"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import items, wornfix
if "--current" not in sys.argv:
    h, e, b = wornfix.prescribed(); wornfix.install(h, e, b)
    print("### 처방 설치됨 (털모자 v3 / 왕관 v2 / 선글라스 v2 / 동그란안경 v2 / 망토 3종 밑단단)\n")
else:
    print("### 현행 좌표\n")
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "verify.py"), encoding="utf-8").read()
src = src.replace("import rig, items, hair\n", "import rig, hair\nimport items\n")
exec(compile(src, "verify.py", "exec"), {"__name__": "__main__"})
