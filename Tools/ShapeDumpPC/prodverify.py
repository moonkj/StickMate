# -*- coding: utf-8 -*-
"""양성 대조 — Tools/ShapeDump/prodverify.py 를 <b>그대로</b> 돌리고, 먹이는 좌표만
모자 처방 이전(7ab0468^) 빌더로 바꿔치기한다. 검사 코드는 한 줄도 복제하지 않는다.

★ 종료코드가 <b>뒤집혀 있다</b>. 여기서는 빨간불이 정상이다:
     rc 0 : 본 하니스가 위반을 잡아냈다(= 살아 있다)
     rc 1 : 옛 빌더를 먹였는데도 위반 0건 → **본 하니스의 모든 '0건'을 무효로 선언한다**
            (docs/TEAM.md 4절 사고 #4)
"""
import os, sys, runpy

HERE = os.path.dirname(os.path.abspath(__file__))
os.environ["SHAPEDUMP_BUILD"] = os.path.join(HERE, "build.sh")
inner = os.path.join(HERE, os.pardir, "ShapeDump", "prodverify.py")

code = 0
try:
    runpy.run_path(inner, run_name="__main__")
except SystemExit as e:
    code = e.code if isinstance(e.code, int) else 1

print()
if code:
    print("★ 양성 대조 성공 — 옛 빌더에서 하니스가 빨간불을 냈다(내부 rc=%d)." % code)
    sys.exit(0)
print("!! 양성 대조 실패 — 옛 빌더를 먹였는데 위반 0건이다. 하니스가 죽어 있다.")
print("   Tools/ShapeDump/prodverify.py 의 '0건'을 전부 무효로 선언하라.")
sys.exit(1)
