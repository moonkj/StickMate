# -*- coding: utf-8 -*-
"""프로덕션(AccessoryShapeBuilder.cs)이 실제로 만드는 좌표를 설계 검산 하니스에 그대로 먹인다.

검사 코드는 한 줄도 새로 쓰지 않는다 — design/equipment/verify/verify.py를 그대로 실행하고,
그 파일이 import 하는 items/hair 만 **덤프한 프로덕션 좌표**로 바꿔치기한다.
(검사를 두 벌로 적으면 두 자가 갈라지고, 그 순간 검산이 무엇을 재는지 아무도 모르게 된다.)
"""
import subprocess, sys, os, types

HERE = os.path.dirname(os.path.abspath(__file__))
VERIFY_DIR = "/Users/kjmoon/App/StickMate/design/equipment/verify"
sys.path.insert(0, VERIFY_DIR)
import rig as rigmod
from rig import Shape

# ★ shim 이 베낀 StickConfig 상수가 프로덕션과 어긋나면 여기서 멈춘다.
#   (2026-09-02: MinFillOutlineScreenPoints 결손으로 이 하니스가 조용히 죽어 있었다)
_drift = subprocess.run([sys.executable, os.path.join(HERE, "shimdrift.py")],
                        capture_output=True, text=True)
if _drift.returncode != 0:
    print(_drift.stdout); print(_drift.stderr)
    print("!! CoreShim.cs 가 프로덕션과 어긋났다 — 검산이 무엇을 재는지 알 수 없다.")
    sys.exit(1)

raw = subprocess.run([os.path.join(HERE, "build.sh")], capture_output=True, text=True)
if raw.returncode != 0:
    print(raw.stdout); print(raw.stderr); sys.exit(1)

CATS = {}
COVER = {}
cur_cat = cur_name = None
for line in raw.stdout.splitlines():
    f = line.split("\t")
    if f[0] == "@ITEM":
        cur_cat, cur_name = f[1], f[2]
        CATS.setdefault(cur_cat, {})[cur_name] = []
    elif f[0] == "@SHAPE":
        name, loop, filled, tone = f[1], f[2] == "1", f[3] == "1", int(f[4])
        pts = [tuple(float(v) for v in p.split(",")) for p in f[6:]]
        CATS[cur_cat][cur_name].append(Shape(name, pts, loop=loop, filled=filled, tone=tone))
    elif f[0] == "@COVER":
        COVER[int(f[1])] = float("inf") if f[2] == "inf" else float(f[2])

items = types.ModuleType("items")
items.HEAD, items.EYES, items.NECK, items.BACK = CATS["HEAD"], CATS["EYES"], CATS["NECK"], CATS["BACK"]
items.EYE_FRONT_ONLY = {"외알안경", "안대"}
items.COVER = COVER
hair = types.ModuleType("hair")
hair.SET = CATS["HAIR"]
sys.modules["items"], sys.modules["hair"] = items, hair

os.chdir(VERIFY_DIR)
src = open("verify.py", encoding="utf-8").read()
print("── 대상: 프로덕션 AccessoryShapeBuilder.cs (덤프) ──")
exec(compile(src, "verify.py", "exec"), {"__name__": "__main__"})
