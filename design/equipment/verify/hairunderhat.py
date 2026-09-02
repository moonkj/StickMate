# -*- coding: utf-8 -*-
"""★ 「팩에 HAIR를 넣으면 그 팩의 모자가 그것을 얼마나 지우는가」 실측.

왜 이걸 재는가
--------------
DS-2가 확정한 팩 구성은 **스탯 4슬롯(HEAD/EYES/NECK/BACK) 1종씩 + 외형 2종**이다.
HEAD가 강제이므로, 외형 2종에 HAIR를 고르면 **팩이 파는 그 상태(6/6 착용 = 세트 완성)**에서
팩의 모자가 팩의 머리카락 위에 얹힌다(레이어: SortHead > SortHair). 그 손실을 숫자로 잰다.

  가시 면적비 = (모자를 쓴 뒤 남는 머리카락 잉크 면적) / (모자 없을 때의 면적)

★ 첫 시도의 교정이 깨졌고 그 숫자를 폐기했다.
  나는 "왕관은 커버선이 +∞니까 가시비 1.000"을 기대값으로 썼는데 **그 기대가 틀렸다** —
  왕관은 머리카락을 **자르지(clip)** 않을 뿐, 자기 채움 잉크로 여전히 **가린다(occlude)**.
  두 가지가 다른 일이라는 것을 기대값이 뭉갰다. 아래 교정 3건은 그 둘을 갈라서 잰다.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, hair, items, headroom as H
from rig import Shape

HATS = {"야구모자": items.cap, "털모자": items.beanie, "중절모": items.fedora,
        "왕관": items.crown_hat, "베레모": items.beret, "밀짚모자": items.straw}
INF = float("inf")


# ═══════════════ 교정 — 눈금이 맞는가 (깨지면 아래 숫자를 전부 폐기) ═══════════════
def calibrate(w):
    hs = hair.SET["단정한머리"]
    base = H.hair_visible_area(hs, [], INF, w)
    ok = True

    # (A) 아주 멀리 있는 모자 + 커버선 +∞  ->  가리는 것이 없다  ->  1.000
    far = [Shape(s.name, [(x, y + 40.0) for x, y in s.pts], s.loop, s.filled) for s in items.cap()]
    a = H.hair_visible_area(hs, far, INF, w) / base
    ok &= abs(a - 1.0) < 1e-6
    print("  %s 멀리 치운 모자(+40R) + 커버선 +∞ -> 가시비 %.6f (기대 1.000000)"
          % ("OK " if abs(a - 1.0) < 1e-6 else "FAIL", a))

    # (B) 전부 덮는 거대 채움 + 커버선 +∞  ->  0.000   (가림 경로가 실제로 작동하는가)
    big = [Shape("Big", [(-40, -40), (40, -40), (40, 40), (-40, 40)], True, True)]
    b = H.hair_visible_area(hs, big, INF, w) / base
    ok &= b < 1e-9
    print("  %s 전부 덮는 채움 + 커버선 +∞ -> 가시비 %.6f (기대 0.000000)"
          % ("OK " if b < 1e-9 else "FAIL", b))

    # (C) 커버선 −40R (전부 잘라냄) -> 0.000   (자르기 경로가 실제로 작동하는가)
    c = H.hair_visible_area(hs, [], -40.0, w) / base
    ok &= c < 1e-9
    print("  %s 커버선 −40R(전부 자름) -> 가시비 %.6f (기대 0.000000)"
          % ("OK " if c < 1e-9 else "FAIL", c))

    # (D) 스캔라인 면적 == 다각형 면적 (겹침 없는 단일 채움: 바가지머리 HairMass)
    solo = [s for s in hair.SET["바가지머리"] if s.filled][:1]
    sc = H.hair_visible_area(solo, [], INF, w)
    pa = abs(H.polygon_area(solo[0].pts))
    ok &= abs(sc - pa) / pa < 2e-3
    print("  %s 스캔라인 면적 %.5f R^2 vs 다각형 면적 %.5f R^2  (상대차 %.4f%%)"
          % ("OK " if abs(sc - pa) / pa < 2e-3 else "FAIL", sc, pa, abs(sc - pa) / pa * 100))
    return ok


def run(scale):
    w = H.stroke_in_R(scale)
    print("\n╔══ 배율 %.2f  (W = %.4f R) ══╗" % (scale, w))
    print("  ── 교정 ──")
    if not calibrate(w):
        print("  !! 교정 실패 — 이 배율의 숫자를 폐기한다"); sys.exit(1)

    names = list(HATS)
    print("\n  가시 면적비 (머리카락 세로 \\ 모자 가로)")
    print("  %-11s" % "" + "".join("%9s" % h for h in names) + "   ★6종평균")
    worst = (9e9, None); best = (-1, None)
    for hn in hair.SET:
        hs = hair.SET[hn]
        base = H.hair_visible_area(hs, [], INF, w)
        if base <= 1e-9:
            print("  %-11s (채운 도형 없음)" % hn); continue
        cells = [H.hair_visible_area(hs, HATS[h](), items.COVER[h], w) / base for h in names]
        print("  %-11s" % hn + "".join("%8.1f%%" % (c * 100) for c in cells)
              + "   %7.1f%%" % (sum(cells) / len(cells) * 100))
        for h, c in zip(names, cells):
            if c < worst[0]: worst = (c, (hn, h))
            if c > best[0]: best = (c, (hn, h))
    print("  ── 최악 %s+%s = %.1f%%   최선 %s+%s = %.1f%%"
          % (worst[1][0], worst[1][1], worst[0]*100, best[1][0], best[1][1], best[0]*100))


for s in (0.75, 0.60):
    run(s)

print("""
╔══ 읽는 법 ══╗
  칸 = "그 머리카락을 그 모자와 **함께** 썼을 때 화면에 남는 잉크 면적의 비율".
  팩은 HEAD를 반드시 포함한다(DS-2). 즉 팩에 HAIR를 넣으면, 팩이 파는 대표 상태(6/6 = 세트 완성)에서
  그 HAIR는 이 표만큼만 보인다. **최선의 칸(왕관 + 곱슬)조차 56%다.**
  왕관은 머리카락을 자르지 않는 유일한 모자인데도 자기 채움 잉크로 절반을 먹는다.
╚═════════════╝""")
