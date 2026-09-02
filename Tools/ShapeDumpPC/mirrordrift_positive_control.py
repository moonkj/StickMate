# -*- coding: utf-8 -*-
"""양성 대조 — design/equipment/verify/mirrordrift.py 를 한 줄도 고치지 않고,
먹이는 프로덕션 덤프만 "모자 처방 이전(7ab0468^)" 빌더로 바꿔치기해 빨간불이 나오는지 본다.

★ 종료코드는 <b>뒤집혀 있다</b>(이 폴더의 공통 규약 — prodverify.py 와 같다):
     rc 0 : 빨간불이 났다 = mirrordrift 가 살아 있다
     rc 1 : 옛 빌더인데도 어긋남 0건 → mirrordrift 의 모든 '0건'을 무효로 선언한다
"""
import sys, importlib.util, os

HERE = os.path.dirname(os.path.abspath(__file__))
V = os.path.join(HERE, os.pardir, os.pardir, "design", "equipment", "verify")
spec = importlib.util.spec_from_file_location("mirrordrift", os.path.join(V, "mirrordrift.py"))
m = importlib.util.module_from_spec(spec)
sys.path.insert(0, os.path.abspath(V))
spec.loader.exec_module(m)
m.BUILD = os.path.join(HERE, "build.sh")     # 처방 이전 빌더

drifted = m.main()
print()
if drifted:
    print("★ 양성 대조 성공 — 옛 빌더에서 mirrordrift 가 어긋남을 잡아냈다.")
    sys.exit(0)
print("!! 양성 대조 실패 — 옛 빌더인데 어긋남 0건이다. mirrordrift 의 '0건'을 전부 무효로 선언하라.")
sys.exit(1)
