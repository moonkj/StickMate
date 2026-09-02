# -*- coding: utf-8 -*-
"""R3 ④ 「최대 연속 외곽호」 — R2 약점 1번을 정면으로.

★ R2 에서 나는 팩의 **모자+안경** 44° 를 출하 **모자 단독** 73~134° 와 비교했다.
   그건 서로 다른 물건을 잰 것이다(r2_final.py 57행은 모자만 돈다). 여기서 바로잡는다.
자: headroom.head_arc_deg — 반경 1.0R 원둘레 2880표본 중 잉크에 안 덮인 최대 연속 구간.
"""
import sys, os
sys.path.insert(0, "/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import headroom
import r3_prod as P

CATS, COVER, W, RAR, LOG = P.dump()
HATS = ["야구모자", "털모자", "중절모", "왕관", "베레모", "밀짚모자"]
EYES = ["선글라스", "동그란안경", "고글", "외알안경", "뿔테안경", "안대"]

print("=" * 96)
print("④ 최대 연속 외곽호 (배율 0.75, W=%.6f R)" % W)
print("=" * 96)
print("\n-- (a) 모자 단독 --")
hat_only = {}
for h in HATS:
    tot, run = headroom.head_arc_deg(CATS["HEAD"][h], W)
    hat_only[h] = (tot, run)
    print("  %-10s 총 외곽호 %5.0f°   최대 연속 %5.0f°" % (h, tot, run))
print("  대역: 최대연속 %.0f ~ %.0f°" % (min(v[1] for v in hat_only.values()),
                                     max(v[1] for v in hat_only.values())))

print("\n-- (b) 안경 단독 --")
for e in EYES:
    tot, run = headroom.head_arc_deg(CATS["EYES"][e], W)
    print("  %-10s 총 외곽호 %5.0f°   최대 연속 %5.0f°" % (e, tot, run))

print("\n-- (c) 모자 + 안경 36조합 · 최대 연속 외곽호(°) --")
print("%-10s" % "" + "".join("%9s" % e for e in EYES))
allv = []
for h in HATS:
    row = "%-10s" % h
    for e in EYES:
        tot, run = headroom.head_arc_deg(CATS["HEAD"][h] + CATS["EYES"][e], W)
        allv.append((run, h, e)); row += "%9.0f" % run
    print(row)
allv.sort()
print("\n  출하 36조합 최대연속 외곽호: 최저 %.0f° (%s+%s) ~ 최고 %.0f° (%s+%s)"
      % (allv[0][0], allv[0][1], allv[0][2], allv[-1][0], allv[-1][1], allv[-1][2]))
print("  중앙값 %.0f°" % sorted(v[0] for v in allv)[len(allv)//2])

print("\n-- (d) 총 외곽호(°) 36조합 --")
print("%-10s" % "" + "".join("%9s" % e for e in EYES))
tots = []
for h in HATS:
    row = "%-10s" % h
    for e in EYES:
        tot, run = headroom.head_arc_deg(CATS["HEAD"][h] + CATS["EYES"][e], W)
        tots.append(tot); row += "%9.0f" % tot
    print(row)
print("\n  총 외곽호 대역 %.0f ~ %.0f°" % (min(tots), max(tots)))
