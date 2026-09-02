# 양성 대조: mirrordrift.py 를 한 줄도 고치지 않고, 프로덕션 덤프만
# "모자 처방 이전(7ab0468^)" 빌더로 바꿔치기해서 빨간불이 나오는지 본다.
import sys, importlib.util, os
V = "/Users/kjmoon/App/StickMate/design/equipment/verify"
sys.path.insert(0, V)
spec = importlib.util.spec_from_file_location("mirrordrift", os.path.join(V, "mirrordrift.py"))
m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
m.BUILD = "/Users/kjmoon/App/StickMate/Tools/ShapeDumpPC/build.sh"   # 처방 이전 빌더
sys.exit(m.main())
