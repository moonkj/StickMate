# -*- coding: utf-8 -*-
"""R3 ② **출하 모자 6 x 안경 6 = 36조합** 전수 — 가시율 / 남는 두께 / 파편 / 구분 / 외곽호.

★ 기하는 전부 **프로덕션 실행 덤프**(r3_prod)에서 온다. 설계 거울은 교정 대상일 뿐 입력이 아니다.
★ 자는 둘이다 — 면적은 구간 대수(headroom), 두께/파편/프로파일은 래스터 EDT(r3_raster).
  두 자를 §교정 (f) 에서 서로 대조했다(상대오차 <0.02%).
"""
import math, sys, os
sys.path.insert(0, "/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import headroom, rig
import r3_cal, r3_prod as P, r3_raster as RS

CATS, COVER, W, RARITY = r3_cal.run()
HATS  = ["야구모자", "털모자", "중절모", "왕관", "베레모", "밀짚모자"]
EYES  = ["선글라스", "동그란안경", "고글", "외알안경", "뿔테안경", "안대"]

H = 0.0015                     # 격자 (두께 편향대역 ±2h = ±0.0087획)
X0, X1, Y0, Y1 = -1.80, 1.80, -1.45, 1.15


def masks(hat_shapes, eyes_shapes, w=W, h=H, dyh=0.0, dye=0.0):
    hs = [s.shifted(0, dyh) for s in hat_shapes]
    es = [s.shifted(0, dye) for s in eyes_shapes]
    me, xs, ys = RS.mask_of(es, w, X0, X1, Y0, Y1, h)
    mh, _, _   = RS.mask_of(hs, w, X0, X1, Y0, Y1, h)
    return me, (me & ~mh), xs, ys


def combo(hat, eye, w=W, h=H, dyh=0.0, dye=0.0):
    me, mv, xs, ys = masks(CATS["HEAD"][hat], CATS["EYES"][eye], w, h, dyh, dye)
    tot = me.sum()
    vis = mv.sum()
    ratio = vis / tot if tot else 0.0
    thick = RS.thickness_W(mv, h, w)
    n, sizes = RS.components(mv)
    big = (sizes[0] / vis) if vis else 0.0
    return dict(vis=ratio, thick=thick, ncomp=n, big=big, mask=mv, xs=xs, ys=ys)


def arc_run(shapes, w=W):
    _, run = headroom.head_arc_deg(shapes, w)
    return run


if __name__ == "__main__":
    print("=" * 100)
    print("② 출하 36조합 — 모자(행) x 안경(열).  배율 0.75 · W = %.6f R" % W)
    print("=" * 100)

    res = {}
    for hn in HATS:
        for en in EYES:
            res[(hn, en)] = combo(hn, en)

    for title, key, fmt in (("가시 면적률 %", "vis", lambda v: "%7.1f" % (v*100)),
                            ("남는 잉크 최대두께(획)", "thick", lambda v: "%7.2f" % v),
                            ("파편 수", "ncomp", lambda v: "%7d" % v)):
        print("\n-- %s --" % title)
        print("%-10s" % "" + "".join("%9s" % e for e in EYES))
        for hn in HATS:
            print("%-10s" % hn + "".join("%9s" % fmt(res[(hn, en)][key]) for en in EYES))

    # 슬롯 내 구분 — 같은 모자 아래에서 안경 6종의 **남은 형태**가 서로 구분되는가
    print("\n-- 같은 모자 아래 남은 형태끼리의 최소 실루엣 차 (획, 문턱 >1.00) --")
    print("%-10s%10s%14s" % ("", "최소차", "가장 닮은 쌍"))
    for hn in HATS:
        profs = {en: RS.profile_from_mask(res[(hn, en)]["mask"], res[(hn, en)]["xs"],
                                          res[(hn, en)]["ys"]) for en in EYES}
        worst = (9e9, None)
        for i in range(6):
            for j in range(i + 1, 6):
                d = rig.max_delta(profs[EYES[i]], profs[EYES[j]])
                if d < worst[0]: worst = (d, (EYES[i], EYES[j]))
        print("%-10s%10.2f   %s~%s %s" % (hn, worst[0] / W, worst[1][0], worst[1][1],
                                          "OK" if worst[0] > W else "★ 미달"))

    # 알몸 기준선 — 모자 없이 안경 6종끼리
    profs0 = {}
    for en in EYES:
        m0, xs, ys = RS.mask_of(CATS["EYES"][en], W, X0, X1, Y0, Y1, H)
        profs0[en] = RS.profile_from_mask(m0, xs, ys)
    worst = (9e9, None)
    for i in range(6):
        for j in range(i + 1, 6):
            d = rig.max_delta(profs0[EYES[i]], profs0[EYES[j]])
            if d < worst[0]: worst = (d, (EYES[i], EYES[j]))
    print("%-10s%10.2f   %s~%s  [기준선: 모자 없음]" % ("(민머리)", worst[0] / W,
                                                    worst[1][0], worst[1][1]))
