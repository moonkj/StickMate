# -*- coding: utf-8 -*-
"""★ 「잉크 총량」 — 시안 그림이 numeric 게이트가 못 본 것을 보여줬다: **팩이 덩어리로 읽힌다.**
   그래서 잰다: 6/6 상태의 가시 잉크 면적을, 기본 42종으로 만들 수 있는 한 벌들과 비교한다.
   기준선을 하나만 잡으면 편향된다 — 가벼운 한 벌부터 무거운 한 벌까지 훑는다."""
import sys, os, itertools, random, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, headroom as H
import pack_nightshift as P
INF = float("inf")

def total_visible(order, w):
    """order = [(shapes, cover)] 아래 레이어부터. 위 레이어에 가려진 뒤 남는 잉크 면적 합."""
    t = 0.0
    for j, (sj, cj) in enumerate(order):
        above = [x for sk, _ in order[j+1:] for x in sk]
        t += H.hair_visible_area(sj, above, cj, w)
    return t

def head_exposed(order, w):
    """머리 원반 중 **어떤 아이템에도** 안 덮인 면적 비율. verify.py 의 남는 머리는 HEAD만 본다 —
       안경/머리카락까지 얹으면 실제로 얼마나 남는지는 아무도 재지 않았다."""
    allsh = [x for sj, _ in order for x in sj]
    return H.head_area_ratio(allsh, w)

for scale in (0.75, 0.60):
    w = H.stroke_in_R(scale)
    print("\n╔══ 배율 %.2f (W=%.4fR) ══╗" % (scale, w))
    # 교정: 아무것도 안 쓰면 머리 100%
    e0 = head_exposed([], w)
    print("  [교정] 아무것도 안 씀 -> 머리 노출 %.4f (기대 1.0000)  %s"
          % (e0, "OK" if abs(e0-1) < 1e-4 else "FAIL"))
    if abs(e0-1) > 1e-4: sys.exit(1)

    pack = [(P.back_toolbag(), INF), (P.hair_napetie(), 0.50),
            (P.neck_apronbib(), INF), (P.eyes_respirator(), INF), (P.head_havelock(), INF)]
    pv, pe = total_visible(pack, w), head_exposed(pack, w)
    print("  팩 「야간 정비반」   가시 잉크 %6.3f R²   머리 노출 %5.1f%%" % (pv, pe*100))

    # 기본 42종으로 만든 한 벌 24개(고정 시드) 훑기
    HN = list(items.HEAD); EN = list(items.EYES); NN = list(items.NECK)
    BN = list(items.BACK); RN = list(hair.SET)
    rnd = random.Random(20260902)
    combos = [(rnd.choice(HN), rnd.choice(EN), rnd.choice(NN), rnd.choice(BN), rnd.choice(RN))
              for _ in range(24)]
    res = []
    for hn, en, nn, bn, rn in combos:
        o = [(items.BACK[bn], INF), (hair.SET[rn], items.COVER[hn]),
             (items.NECK[nn], INF), (items.EYES[en], INF), (items.HEAD[hn], INF)]
        res.append((total_visible(o, w), head_exposed(o, w), (hn, en, nn, bn, rn)))
    res.sort()
    lo, hi = res[0], res[-1]
    med = res[len(res)//2]
    inks = [r[0] for r in res]; exps = [r[1] for r in res]
    print("  기본 한 벌 24조합   가시 잉크 %6.3f ~ %6.3f (중앙 %6.3f)   머리 노출 %4.1f ~ %4.1f%% (중앙 %4.1f%%)"
          % (min(inks), max(inks), med[0], min(exps)*100, max(exps)*100, sorted(exps)[12]*100))
    print("     가장 가벼움: %s  %.3f" % (" ".join(lo[2]), lo[0]))
    print("     가장 무거움: %s  %.3f" % (" ".join(hi[2]), hi[0]))
    over = sum(1 for i in inks if i < pv)
    print("  ★ 팩은 기본 24조합 중 **%d개보다 무겁다** (%d/24 백분위)" % (over, over))
    print("  ★ 팩 머리 노출 %.1f%% vs 기본 중앙 %.1f%%  — verify.py 의 '남는 머리'는 HEAD만 보므로 이 축을 못 본다"
          % (pe*100, sorted(exps)[12]*100))
