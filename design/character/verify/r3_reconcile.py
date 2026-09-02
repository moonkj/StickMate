# -*- coding: utf-8 -*-
"""R3 ⑥ **design-equipment 와 내 숫자를 가른다.**

두 라운드가 독립적으로 같은 결함에 도달했는데 숫자가 다르다:
   나                 5.5 ~ 46.5%   (최악 중절모+동그란안경 5.5%)
   design-equipment   평균 6.4%@0.75 / 3.9%@0.60, 최악 외알안경+야구모자 0.0%

★ 원인 가설: **분자에 안경의 획 팽창을 넣느냐**다.
   eyesunderhat.py 는 `headroom.hair_visible_area` 를 쓴다. 그 함수는
     · 분자(안경) = `_poly_spans(pts, y)`      -> **채움 다각형만. 획 팽창 없음.**
     · 가림(모자) = `ink_spans(shapes, y, w)`  -> **획 팽창 포함.**
   즉 **비대칭**이다. 안경은 알몸 다각형으로, 모자는 잉크로 잰다.
   나는 양쪽 다 잉크(채움+획 W/2)로 쟀다.
   화면에 실제로 그려지는 것은 채움 **+ 윤곽선**이므로 「화면에 남는 잉크」는 내 정의가 맞고,
   「판이 살아남았는가」는 그쪽 정의가 더 엄하다. **둘 다 참이고 묻는 질문이 다르다.**

여기서 그 가설을 **재현으로** 검증한다 — 내 하니스(프로덕션 좌표 + 래스터)로 그쪽 정의를
그대로 구현해서 6.4% / 0.0% 가 나오는가.
"""
import sys, os
sys.path.insert(0, "/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import headroom
import r3_prod as P, r3_raster as RS

CATS, COVER, W, RAR, LOG = P.dump()
HATS = ["야구모자", "털모자", "중절모", "왕관", "베레모", "밀짚모자"]
EYES = ["선글라스", "동그란안경", "고글", "외알안경", "뿔테안경", "안대"]
H = 0.0015
X0, X1, Y0, Y1 = -1.80, 1.80, -1.45, 1.15


def area_ratio(eye, hat, w, eye_stroke):
    """eye_stroke=True  : 안경도 잉크(채움+획)로 — 내 정의
       eye_stroke=False : 안경은 채움 다각형만  — design-equipment 정의"""
    es = CATS["EYES"][eye] if eye_stroke else [s for s in CATS["EYES"][eye] if s.filled]
    me, _, _ = RS.mask_of(es, w if eye_stroke else 0.0, X0, X1, Y0, Y1, H)
    mh, _, _ = RS.mask_of(CATS["HEAD"][hat], w, X0, X1, Y0, Y1, H)
    tot = me.sum()
    return ((me & ~mh).sum() / tot) if tot else 0.0


for scale in (0.75, 0.60):
    w = headroom.stroke_in_R(scale)
    print("\n" + "=" * 92)
    print("배율 %.2f   W = %.4f R" % (scale, w))
    print("=" * 92)
    for tag, st in (("[A] 내 정의  — 안경 잉크(채움+획) 기준", True),
                    ("[B] eq 정의 — 안경 채움 다각형만(획 제외)", False)):
        print("\n  %s" % tag)
        print("  %-11s" % "" + "".join("%9s" % h for h in HATS) + "   ★평균")
        allv = []
        for e in EYES:
            cells = [area_ratio(e, h, w, st) for h in HATS]
            allv += cells
            print("  %-11s" % e + "".join("%8.1f%%" % (c * 100) for c in cells)
                  + "  %7.1f%%" % (sum(cells) / 6 * 100))
        print("  %-11s 36조합 평균 %.1f%%  최저 %.1f%%  최고 %.1f%%"
              % ("", sum(allv) / 36 * 100, min(allv) * 100, max(allv) * 100))
