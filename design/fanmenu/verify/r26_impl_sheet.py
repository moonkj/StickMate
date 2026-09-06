"""R26 구현본 실크기 시트 — r26_impl_verify.py 가 <프로덕션 소스에서 파싱한> 좌표로 굽는다.

  python3 r26_impl_sheet.py [비교할_옛_소스.cs]

인자를 주면 그 소스로도 한 벌 더 굽는다(before/after 대조).
산출: impl_after_1x.png / impl_after_4x.png (+ impl_before_*.png)
"""
import importlib.util, sys, math, os
import numpy as np
from scipy import ndimage
from PIL import Image as PImage

HERE = os.path.dirname(os.path.abspath(__file__))
PROD = "/Users/kjmoon/App/StickMate/Assets/_Project/Scripts/Interaction/GearRadialMenuWidget.cs"


def load(src):
    sys.argv = ["r26_impl_verify.py", src]
    spec = importlib.util.spec_from_file_location("fg", os.path.join(HERE, "r26_impl_verify.py"))
    m = importlib.util.module_from_spec(spec)
    try:
        spec.loader.exec_module(m)
    except SystemExit:      # 게이트 미달이어도 그림은 굽는다(옛 소스 대조용)
        pass
    return m


def sheet(m, scale, path):
    names = list(m.GLYPHS)
    btn = 44.0
    side = int(btn * scale)
    img = PImage.new("RGB", (side * len(names) + 8 * (len(names) + 1), side + 16), (26, 28, 33))
    px = img.load()
    for k, n in enumerate(names):
        u = np.zeros_like(m.GX, bool)
        for p in m.GLYPHS[n]:
            u |= m.mask(p)
        d = ndimage.distance_transform_edt(~u) / m.SS
        a = np.clip(1.0 - d / m.EDGE_FEATHER, 0, 1)
        a[u] = 1.0
        ox, oy = 8 + k * (side + 8), 8
        for yy in range(side):
            for xx in range(side):
                cx, cy = xx - side / 2 + .5, yy - side / 2 + .5
                if math.hypot(cx, cy) > side / 2:
                    continue
                ix = int((cx / scale + m.WIN / 2) * m.SS)
                iy = int((cy / scale + m.WIN / 2) * m.SS)
                al = a[iy, ix] if 0 <= ix < a.shape[1] and 0 <= iy < a.shape[0] else 0.0
                base, ink = (44, 47, 54), (233, 236, 241)
                px[ox + xx, oy + yy] = tuple(int(base[i] + (ink[i] - base[i]) * al) for i in range(3))
    img.save(path)
    print("wrote", path, img.size)


if len(sys.argv) > 1:
    old = load(sys.argv[1])
    sheet(old, 1, os.path.join(HERE, "impl_before_1x.png"))
    sheet(old, 4, os.path.join(HERE, "impl_before_4x.png"))
new = load(PROD)
sheet(new, 1, os.path.join(HERE, "impl_after_1x.png"))
sheet(new, 4, os.path.join(HERE, "impl_after_4x.png"))
