# -*- coding: utf-8 -*-
"""R13 시안 시트 2 — **몸을 올바른 z로 함께 그린다**.
첫 시트(r13_sheet.py)는 몸을 안 그려서 망토가 실제보다 잘 보였다 — design-character 지적 반영.
★ 오프라인 래스터의 한계는 README.md 경고 그대로. 최종 판정은 실기 캡처로만."""
import math, sys
from PIL import Image, ImageDraw
import rig, items
from rig import Shape
import r13_bodyocclusion as B
import r13_capecollar as C

R = 34.0
INK = (30, 34, 40)
PRIM = (204, 60, 60)
ACC = (150, 129, 79)
SCARF, SCARF_A = (204, 85, 18), (186, 89, 40)
BG = (250, 249, 246)
T_HEM = 0.86


def P(p, ox, oy):
    return (ox + p[0] * R, oy - p[1] * R)


def drop(shapes, name):
    """이름으로 조각 하나를 뺀다. **0건 제거는 오류다**(필터 이름이 썩었다는 뜻).
    ★ 2026-09-05 저녁(§12-10) — 옛 코드의 제외 목록은 `("CapeCollar", "CapeYoke")` 였다.
      "CapeYoke" 가 들어 있어 **이 파일은 이중 그리기를 하지 않았다**(r13_sheet.py 와 달랐다).
      다만 "CapeCollar" 는 착지 후 **매치 0건인 죽은 이름**이었다 — 같은 함정의 씨앗이라
      지우고, 0건 제거를 예외로 터뜨리게 바꾼다."""
    out = [s for s in shapes if s.name != name]
    if len(out) == len(shapes):
        raise AssertionError("drop(%r): 매치 0건 — 조각 이름이 바뀌었다. 실재: %s"
                             % (name, [s.name for s in shapes]))
    return out


def no_dup(shapes, tag):
    seen = [s.name for s in shapes]
    dup = sorted({n for n in seen if seen.count(n) > 1})
    if dup:
        raise AssertionError("%s: 이중 그리기 %s  (전체 %s)" % (tag, dup, seen))
    return shapes


def cape_shapes(name, mode):
    """mode: 'now' = 착지 상태(어깨 요크) · 'hem' = 대안 B(요크 대신 밑단 안단)"""
    if mode == "now":
        return no_dup(list(items.BACK[name]), "now/" + name)
    out = drop(items.BACK[name], "CapeYoke")
    return no_dup(out + [Shape("CapeHemFacing", C.hem_facing(name, T_HEM), filled=True, tone=1)],
                  "hem/" + name)


def draw_shapes(d, shapes, ox, oy, colors):
    for s in shapes:
        col = colors.get(s.tone, INK)
        pts = [P(p, ox, oy) for p in s.pts]
        if s.filled:
            d.polygon(pts, fill=col)
            d.line(pts + ([pts[0]] if s.loop else []),
                   fill=tuple(int(c * 0.72) for c in col),
                   width=max(1, int(B.W_FILL * R)), joint="curve")
        else:
            d.line(pts + ([pts[0]] if s.loop else []), fill=col,
                   width=max(1, int(B.W_LINE * R)), joint="curve")


def draw_body(d, ox, oy):
    for a, b in B.LEGS:                                   # 0
        d.line([P(a, ox, oy), P(b, ox, oy)], fill=INK, width=int(B.W_LEG * R))
    d.line([P((0, -1.0), ox, oy), P((0, rig.HIP_R), ox, oy)], fill=INK, width=int(B.W_TORSO * R))  # 1
    for a, b in B.ARMS:                                   # 2
        d.line([P(a, ox, oy), P(b, ox, oy)], fill=INK, width=int(B.W_ARM * R))
    d.ellipse([ox - R, oy - R, ox + R, oy + R], fill=BG, outline=INK, width=int(B.W_RING * R))     # 3·4


def main():
    # ★ 2026-09-05 오후 — items.BACK 이 이미 **착지한 어깨 요크(2.40 R)** 를 담는다.
    #   'now' = 착지 상태 · 'hem' = 대안 B(밑단 안단 t=0.86). 옛 옷깃은 items.clasp() 로만 남아 있다.
    cols = [("착지 요크 단독", "now", False),
            ("+목도리 (착지 요크)", "now", True),
            ("+목도리 (대안 B 밑단안단)", "hem", True)]
    rows = ["긴망토", "짧은망토", "판초"]
    CW, CH = int(R * 8.0), int(R * 11.6)
    img = Image.new("RGB", (CW * 3 + 40, CH * 3 + 40), BG)
    d = ImageDraw.Draw(img)
    for ri, cape in enumerate(rows):
        for ci, (label, mode, scarf) in enumerate(cols):
            ox = 20 + CW * ci + CW * 0.5
            oy = 30 + CH * ri + R * 1.6
            d.text((20 + CW * ci + 6, 30 + CH * ri + 2), "%s / %s" % (cape, label), fill=(90, 95, 105))
            draw_shapes(d, cape_shapes(cape, mode), ox, oy, {0: PRIM, 1: ACC, 2: tuple(int(c * 0.7) for c in PRIM)})
            draw_body(d, ox, oy)
            if scarf:
                draw_shapes(d, items.NECK["목도리"], ox, oy, {0: SCARF, 1: SCARF_A})
    img.save("r13-cape-body.png")
    print("wrote r13-cape-body.png %dx%d" % img.size)
    return 0


if __name__ == "__main__":
    sys.exit(main())
