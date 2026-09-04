# -*- coding: utf-8 -*-
"""③ 조각 단위 해상도 예산 — 인계본의 어느 조각이 우리 몸에서 **존재할 수 있는가**.

배율 0.75에서 머리 지름은 5.82 W 뿐이다. 그 위에서 잉크 사각형이 1.5 W 미만인 조각은
화면에 **덩어리로 존재하지 않는다**(획 하나에 먹힌다). 이 표가 이식 사양의 입력이다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, handoff, items
from rig import Shape
from handoff import parse_items, build, to_R_shape, OUR_NAME, ITEM_SLOT

W75 = rig.stroke_in_R(0.75)
W60 = rig.stroke_in_R(0.60)

ROLE = {"B": "채움+외곽", "S": "낱선", "F": "강조채움", "H": "하이라이트",
        "CB": "원(채움+외곽)", "CF": "강조원", "RB": "둥근사각"}

IT = parse_items()
print("╔══ 조각별 해상도 예산 (배율 0.75, W = %.6f R, 머리지름 = %.2f W) ══╗" % (W75, 2 / W75))
print("%-13s %-4s %-14s %8s %8s %8s %8s  %s" %
      ("아이템", "#", "역할", "폭W", "높이W", "잉크W", "알파", "판정"))
survive = {}
tot_ok = tot_no = 0
for kind, pieces in IT.items():
    P = build(kind, pieces)
    keep = []
    for i, p in enumerate(P):
        s = to_R_shape(kind, p, "%s%d" % (p.call, i))
        x0, y0, x1, y1 = rig.bounds(s.pts)
        w, h = (x1 - x0) / W75, (y1 - y0) / W75
        ink = max(w, h)
        a = p.fill_opacity
        astr = ("grad" if p.filled and a is None else ("%.2f" % a if a is not None else "—"))
        if p.is_highlight: astr = "0.42선"
        ok = ink >= 1.5
        if ok: keep.append(i); tot_ok += 1
        else: tot_no += 1
        note = "존재" if ok else "★소멸 (%.2f < 1.5W)" % ink
        # 낱선은 색면이 아니라 선이라 1-C(색면 하한)가 아니라 길이가 문제다
        print("%-13s %-4d %-14s %8.2f %8.2f %8.2f %8s  %s" %
              (kind if i == 0 else "", i, ROLE[p.call], w, h, ink, astr, note))
    survive[kind] = keep
print("  ⇒ 조각 %d개 중 **존재 %d · 소멸 %d**" % (tot_ok + tot_no, tot_ok, tot_no))

print()
print("╔══ 소멸 조각을 뺀 뒤의 구성 정원 ══╗")
print("%-13s %8s %8s %8s %s" % ("아이템", "인계본", "생존", "우리", "정원 2~4 판정"))
for kind in IT:
    P = build(kind, IT[kind]); k = len(survive[kind])
    slot, nm = OUR_NAME[kind]
    ours = len({"HEAD": items.HEAD, "EYES": items.EYES,
                "NECK": items.NECK, "BACK": items.BACK}[slot][nm])
    print("%-13s %8d %8d %8d %s" %
          (kind, len(P), k, ours, "OK" if 2 <= k <= 4 else "★ %d개 — 정원 밖" % k))

print()
print("╔══ 알파 재고 — 사용자 제약 '착용 장비는 불투명' 대상 ══╗")
print("  ★ 우리 엔진 실측: ItemCatalog.WornColor 의 마지막 줄이 `result.a = ink.a` 다.")
print("     = 조각별 알파가 **존재하지 않는다**. 알파는 잉크 하나로 전역이다(페이드용).")
print()
cnt = {}
for kind in IT:
    for p in build(kind, IT[kind]):
        if p.is_highlight: k = "하이라이트 흰 0.42(선)"
        elif not p.filled: k = "낱선(불투명, 알파 없음)"
        elif p.fill_opacity is None:
            k = "그라디언트 1.00→0.62(재질색)" if kind in ("shortcape", "longcape") \
                else "그라디언트 0.34→0.08(등급색)"
        else: k = "고정 채움 알파 %.2f" % p.fill_opacity
        cnt[k] = cnt.get(k, 0) + 1
for k in sorted(cnt, key=lambda x: -cnt[x]):
    print("  %-34s %3d 조각" % (k, cnt[k]))
print("  합계 %d 조각 · 그중 **완전 불투명(알파 1.0)은 %d 조각**" %
      (sum(cnt.values()), cnt.get("낱선(불투명, 알파 없음)", 0) + cnt.get("고정 채움 알파 1.00", 0)))
