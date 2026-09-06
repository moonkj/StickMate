"""R26 — 실제 부채꼴 배치(호 4 + 위성 1)에 얹어 <실기 크기>로 본다.

기하는 GearRadialMenuWidget 상수 거울: 궤도 111 / 위성 168 / 간격 30° / Ø44 / 호버 Ø48.
바탕은 <임의의 바탕화면>을 대신해 회색 3단(가장 나쁜 경우 포함).
"""
import math
import numpy as np
from PIL import Image, ImageDraw
from fanglyph import *
from r26_new import NEW
from r26_sheet import render_button

ORBIT, SAT_ORBIT, STEP, ARC_N = 111.0, 168.0, 30.0, 4
THETA0 = 90.0


def centers():
    out = []
    for i in range(ARC_N):
        a = math.radians(THETA0 + ((ARC_N - 1) * 0.5 - i) * STEP)
        out.append((ORBIT * math.cos(a), ORBIT * math.sin(a)))
    a = math.radians(THETA0)
    out.append((SAT_ORBIT * math.cos(a), SAT_ORBIT * math.sin(a)))
    return out


def fan(sets, path, px=1.0, bgs=((0x7A, 0x7A, 0x7A), (0x1E, 0x1E, 0x1E), (0xE0, 0xDE, 0xD8))):
    cs = centers()
    Wd, Ht = int(420 * px), int(250 * px)
    tiles = []
    for bg in bgs:
        canvas = Image.new("RGBA", (Wd, Ht), bg + (255,))
        cx, cy = Wd / 2, Ht - int(20 * px)
        for (fx, fy), (name, fn) in zip(cs, sets):
            btn = render_button(fn(), px, accent_names=("Check",))
            canvas.alpha_composite(btn, (int(cx + fx * px - btn.width / 2),
                                         int(cy - fy * px - btn.height / 2)))
        tiles.append(canvas.convert("RGB"))
    out = Image.new("RGB", (Wd * len(tiles) + 12 * (len(tiles) - 1), Ht), (10, 12, 16))
    for i, t in enumerate(tiles):
        out.paste(t, (i * (Wd + 12), 0))
    out.save(path)
    print("wrote", path, out.size)


order = [("focus", NEW["① 집중(스톱워치)"]), ("character", NEW["② 캐릭터(스틱맨)"]),
         ("todo", NEW["③ 할일(체크리스트)"]), ("action", NEW["④ 행동명령(확성기)"]),
         ("quit", NEW["⑤ 종료(전원)"])]

if __name__ == "__main__":
    from fanglyph import CURRENT
    cur = [("focus", CURRENT["① 집중(스톱워치)"]), ("character", CURRENT["② 캐릭터(스틱맨)"]),
           ("todo", CURRENT["③ 할일(체크리스트)"]), ("action", CURRENT["④ 행동명령(확성기)"]),
           ("quit", CURRENT["⑤ 종료(전원)"])]
    fan(cur, "r26_fan_current.png", px=1.0)
    fan(order, "r26_fan_new.png", px=1.0)
    fan(cur, "r26_fan_current_2x.png", px=2.0)
    fan(order, "r26_fan_new_2x.png", px=2.0)
