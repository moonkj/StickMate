# -*- coding: utf-8 -*-
"""프로덕션(AccessoryShapeBuilder.cs)이 실제로 만드는 좌표를 설계 검산 하니스에 그대로 먹인다.

검사 코드는 한 줄도 새로 쓰지 않는다 — design/equipment/verify/verify.py를 그대로 실행하고,
그 파일이 import 하는 items/hair 만 **덤프한 프로덕션 좌표**로 바꿔치기한다.
(검사를 두 벌로 적으면 두 자가 갈라지고, 그 순간 검산이 무엇을 재는지 아무도 모르게 된다.)

★ 종료코드 (2026-09-02 변경) — 이전에는 **위반이 있어도 rc=0** 이었다
   (verify.py 에 sys.exit 가 없다). 게이트로 감싸는 사람이 그것을 모르면
   "돌렸고 통과했다"가 그냥 "돌렸다"가 된다. 지금은:

     rc 0 : 위반 0건
     rc 1 : 위반 N건 · 컴파일 실패 · shim 어긋남 · **위반 수를 읽지 못함**

   마지막 항목이 중요하다. verify.py 의 집계 변수(fail)를 못 읽으면 그건 통과가 아니라
   **판정 불능**이다 — 하니스가 바뀐 것이므로 조용히 초록을 주지 않는다.

★ 환경변수 SHAPEDUMP_BUILD — 좌표를 뽑을 build.sh 를 바꿔치기한다(Tools/ShapeDumpPC 양성 대조용).
   이 파일을 복제하지 않는 이유는, 복제본은 반드시 갈라지기 때문이다.
"""
import subprocess, sys, os, types

HERE = os.path.dirname(os.path.abspath(__file__))
VERIFY_DIR = "/Users/kjmoon/App/StickMate/design/equipment/verify"
BUILD = os.environ.get("SHAPEDUMP_BUILD", os.path.join(HERE, "build.sh"))
sys.path.insert(0, VERIFY_DIR)
import rig as rigmod
from rig import Shape

# ★ shim 이 프로덕션과 어긋났으면 여기서 멈춘다 — 상수든 타입이든.
#   (2026-09-02 두 번: ① MinFillOutlineScreenPoints 결손으로 이 하니스가 조용히 죽었고,
#    ② AccessoryWornFrame 등 타입 5종이 shim 에 없어 컴파일 단계에서 죽었다.)
_drift = subprocess.run([sys.executable, os.path.join(HERE, "shimdrift.py")],
                        capture_output=True, text=True)
if _drift.returncode != 0:
    print(_drift.stdout); print(_drift.stderr)
    print("!! shim 이 프로덕션과 어긋났다 — 검산이 무엇을 재는지 알 수 없다.")
    sys.exit(1)

raw = subprocess.run([BUILD], capture_output=True, text=True)
if raw.returncode != 0:
    print(raw.stdout); print(raw.stderr); sys.exit(1)

# 덤프가 stderr 로 뱉은 Debug.LogError 는 **버리지 않는다** — 그것이 곧 반쪽만 잰 신호다.
if raw.stderr.strip():
    print("── 덤프 경고/에러 (stderr) ──")
    print(raw.stderr.rstrip())

CATS = {}
COVER = {}
LOGCOUNT = None
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
    elif f[0] == "@LOG":
        LOGCOUNT = (int(f[1]), int(f[2]))

# ★ 생존 확인 — 덤프가 도형을 하나도 안 내놨는데 "위반 0건"이면 그건 통과가 아니라 침묵이다.
_empty = [("%s %s" % (c, n)) for c, t in CATS.items() for n, s in t.items() if not s]
if not CATS or _empty:
    print("!! 덤프가 도형을 못 만든 아이템이 있다 — 위반 0건은 무효다: %s" % (", ".join(_empty) or "전 카테고리"))
    sys.exit(1)
if LOGCOUNT is None:
    print("!! 덤프에 @LOG 줄이 없다 — Dump.cs 가 바뀌었다. 판정 불능.")
    sys.exit(1)
if LOGCOUNT[0]:
    print("!! 덤프가 Debug.LogError 를 %d건 냈다 — 형상/카탈로그가 이미 깨져 있다. 판정 불능." % LOGCOUNT[0])
    sys.exit(1)

items = types.ModuleType("items")
items.HEAD, items.EYES, items.NECK, items.BACK = CATS["HEAD"], CATS["EYES"], CATS["NECK"], CATS["BACK"]
items.EYE_FRONT_ONLY = {"외알안경", "안대"}
items.COVER = COVER
hair = types.ModuleType("hair")
hair.SET = CATS["HAIR"]
sys.modules["items"], sys.modules["hair"] = items, hair

os.chdir(VERIFY_DIR)
src = open("verify.py", encoding="utf-8").read()
print("── 대상: 프로덕션 AccessoryShapeBuilder.cs (덤프: %s) ──" % BUILD)
g = {"__name__": "__main__"}
exec(compile(src, "verify.py", "exec"), g)

fail = g.get("fail")
if not isinstance(fail, int):
    print("!! verify.py 의 집계 변수 fail 을 읽지 못했다(%r) — 하니스가 바뀌었다. 판정 불능." % (fail,))
    sys.exit(1)
print("[prodverify] 위반 %d건 -> 종료코드 %d" % (fail, 1 if fail else 0))
sys.exit(1 if fail else 0)
