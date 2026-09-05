# -*- coding: utf-8 -*-
"""R13 시안 시트 — 오프라인 래스터.
★ 경고(design/equipment/verify/README.md): 오프라인 래스터는 **둥근 캡으로 코너 붕괴를 가린다**.
   최종 판정은 실제 빌드 캡처로만. 이 그림은 「겹침이 어디서 일어나는가」를 눈으로 보기 위한 것이다."""
import math, sys
from PIL import Image, ImageDraw
import rig, items
from rig import Shape, W
import r13_polish as P   # ← r13_capecollar(C)는 더 쓰지 않는다. 요크를 합성하지 않고 착지본을 그린다

R = 46.0                      # 머리 반경 픽셀 — 비율은 실물과 같다(획 = W*R)
SW = W * R
INK = (30, 34, 40)
PRIM = (204, 60, 60)
ACC = (150, 129, 79)
SCARF = (204, 85, 18)
SCARF_A = (186, 89, 40)
BG = (250, 249, 246)


def to_px(p, ox, oy):
    return (ox + p[0] * R, oy - p[1] * R)


def draw(d, shapes, ox, oy, colors):
    for s in shapes:
        col = colors.get(s.tone, INK)
        pts = [to_px(p, ox, oy) for p in s.pts]
        if s.filled:
            d.polygon(pts, fill=col)
            outline = tuple(int(c * 0.72) for c in col)
            d.line(pts + ([pts[0]] if s.loop else []), fill=outline, width=int(SW), joint="curve")
        else:
            d.line(pts + ([pts[0]] if s.loop else []), fill=col, width=int(SW), joint="curve")


# ---------------------------------------------------------------------------
# ★ 2026-09-05 저녁 — 도구 버그 수정 (§12-10)
#
#   옛 코드:  out = [s for s in items.BACK[name] if s.name != "CapeCollar"]
#             return out + [Shape("CapeYoke", yoke(...), ...)]
#
#   `coder` 가 어깨 요크를 착지시키면서 `items.BACK` 의 조각 이름이
#   "CapeCollar" → **"CapeYoke"** 로 바뀌었다(프로덕션 조각 이름도 "CapeYoke"다 —
#   `CapeShoulderYoke` 는 그 점열을 만드는 **메서드** 이름이지 조각 이름이 아니다).
#   그래서 위 필터는 **아무것도 걸러내지 못하고**, 이미 들어 있는 요크 위에
#   똑같은 요크를 한 장 더 얹었다 = **요크 2장**(짙은 경계선이 두 번 찍혀 실물보다 굵어 보인다).
#
#   ★ 병의 형태는 이 저장소의 단골이다 — **매치 0건이 성공과 똑같이 생겼다.**
#     그래서 아래 `drop()` 은 **0건 제거를 예외로 터뜨린다.** 이름이 또 바뀌면 조용히
#     지나가지 않고 그 자리에서 빨개진다.
# ---------------------------------------------------------------------------
def drop(shapes, name):
    """이름으로 조각 하나를 뺀다. **0건 제거는 오류다**(필터 이름이 썩었다는 뜻)."""
    out = [s for s in shapes if s.name != name]
    if len(out) == len(shapes):
        raise AssertionError("drop(%r): 매치 0건 — 조각 이름이 바뀌었다. 실재: %s"
                             % (name, [s.name for s in shapes]))
    return out


def no_dup(shapes, tag):
    """같은 이름이 두 번 그려지면 터진다 — 이 라운드가 실제로 당한 버그의 직접 게이트."""
    seen = [s.name for s in shapes]
    dup = sorted({n for n in seen if seen.count(n) > 1})
    if dup:
        raise AssertionError("%s: 이중 그리기 %s  (전체 %s)" % (tag, dup, seen))
    return shapes


def cape_landed(name):
    """★ 착지 상태 그대로 — `items.BACK` 이 이미 어깨 요크(2.40 R)를 담는다. 합성하지 않는다."""
    return no_dup(list(items.BACK[name]), "landed/" + name)


def cape_with_clasp(name):
    """옛 상태(옷깃 띠) 재현 — 착지 요크를 빼고 `items.clasp()` 을 얹는다.
    `items.clasp()` 는 바로 이 대조를 위해 남겨 둔 도형이다(items.py 주석)."""
    out = drop(items.BACK[name], "CapeYoke")
    return no_dup(out + [Shape("CapeCollar", items.clasp(), filled=True, tone=1)],
                  "clasp/" + name)


def body(d, ox, oy):
    """참고용 몸 — 머리 링 + 몸통. 액세서리 자리 감을 잡기 위한 것이지 캐릭터 사양이 아니다."""
    d.ellipse([ox - R, oy - R, ox + R, oy + R], outline=INK, width=int(SW))
    d.line([(ox, oy + R * rig.SHOULDER_R * -0 - R * 0), (ox, oy - R * rig.HIP_R * -1)],
           fill=INK, width=int(SW * 1.3))


def main():
    # ★ 열 정의를 「무엇을 그리는가」로 못박는다. 옛 코드는 1·2열이 `items.BACK` 을 그대로 썼는데
    #   착지 뒤로는 그게 **옛 옷깃이 아니라 요크**라 라벨(「현행」)이 그림과 갈라져 있었다.
    cols = [("옛 옷깃 단독", cape_with_clasp, False),
            ("옛 옷깃 + 목도리", cape_with_clasp, True),
            ("★ 착지 요크 + 목도리", cape_landed, True)]
    rows = ["긴망토", "짧은망토", "판초"]
    CW, CH = int(R * 6.2), int(R * 12.0)
    img = Image.new("RGB", (CW * 3 + 40, CH * 3 + 60), BG)
    d = ImageDraw.Draw(img)
    for ri, cape in enumerate(rows):
        for ci, (label, build, scarf) in enumerate(cols):
            ox = 20 + CW * ci + CW * 0.5
            oy = 40 + CH * ri + R * 1.6
            d.text((ox - CW * 0.45, 40 + CH * ri + 4), "%s / %s" % (cape, label), fill=(90, 95, 105))
            d.line([(ox, oy + R * 1.0), (ox, oy - rig.HIP_R * -R + R * 0)], fill=INK, width=int(SW * 1.3))
            draw(d, build(cape), ox, oy, {0: PRIM, 1: ACC, 2: tuple(int(c * 0.7) for c in PRIM)})
            d.ellipse([ox - R, oy - R, ox + R, oy + R], outline=INK, width=int(SW))
            if scarf:
                draw(d, items.NECK["목도리"], ox, oy, {0: SCARF, 1: SCARF_A})
    img.save("r13-cape-yoke.png")
    print("wrote r13-cape-yoke.png  %dx%d" % img.size)

    # EYES before/after
    img2 = Image.new("RGB", (int(R * 3.2) * 4 + 40, int(R * 3.4) + 40), BG)
    d2 = ImageDraw.Draw(img2)
    variants = [("외알안경 현행", items.EYES["외알안경"]),
                ("외알안경 P1", P.build(True)["EYES"]["외알안경"]),
                ("안대 현행", items.EYES["안대"]),
                ("안대 P1", P.build(True)["EYES"]["안대"])]
    for k, (label, shapes) in enumerate(variants):
        ox = 20 + int(R * 3.2) * k + int(R * 1.6)
        oy = 20 + int(R * 1.7)
        d2.ellipse([ox - R, oy - R, ox + R, oy + R], outline=INK, width=int(SW))
        draw(d2, shapes, ox, oy, {0: (60, 66, 78), 1: ACC})
        d2.text((ox - R, 4), label, fill=(90, 95, 105))
    img2.save("r13-eyes.png")
    print("wrote r13-eyes.png  %dx%d" % img2.size)

    # 동그란안경 코다리
    img3 = Image.new("RGB", (int(R * 3.2) * 2 + 40, int(R * 3.4) + 40), BG)
    d3 = ImageDraw.Draw(img3)
    for k, (label, shapes) in enumerate([("동그란안경 현행", items.EYES["동그란안경"]),
                                         ("동그란안경 P4", P.build(True)["EYES"]["동그란안경"])]):
        ox = 20 + int(R * 3.2) * k + int(R * 1.6)
        oy = 20 + int(R * 1.7)
        d3.ellipse([ox - R, oy - R, ox + R, oy + R], outline=INK, width=int(SW))
        draw(d3, shapes, ox, oy, {0: (60, 66, 78), 1: ACC})
        d3.text((ox - R, 4), label, fill=(90, 95, 105))
    img3.save("r13-roundglasses.png")
    print("wrote r13-roundglasses.png  %dx%d" % img3.size)
    return 0


if __name__ == "__main__":
    sys.exit(main())
