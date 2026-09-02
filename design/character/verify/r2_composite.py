# -*- coding: utf-8 -*-
"""R2 ③ **동시 착용** 머리 예산 — 지금 게이트가 재지 않는 축.
headroom.py도 AccessoryFillAreaRuleTests도 아이템을 **하나씩** 잰다.
팩은 세트로 팔리고 세트로 입는다 → HEAD·EYES(+HAIR)가 **항상 함께** 얹힌다."""
import math, sys
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/character/verify")
import rig, items, headroom, hair, r2_body as B
from rig import Shape
B.print_cal()

W75 = headroom.stroke_in_R(0.75); W60 = headroom.stroke_in_R(0.60)
COVER = {"야구모자":0.06, "털모자":-0.06, "중절모":0.08,
         "베레모":0.02, "밀짚모자":0.08, "왕관":float('inf')}
HAIRS = {"삐친머리":hair.cowlick(),"단정한머리":hair.straight(),"곱슬머리":hair.curly(),
         "민머리":hair.bald(),"바가지머리":hair.bowl(),"포니테일":hair.ponytail()}

def clipped_hair(hsh, cover):
    out=[]
    for s in hsh:
        pts = headroom.clip_below(s.pts, cover)
        if len(pts)>=3: out.append(Shape(s.name, pts, loop=s.loop, filled=s.filled))
    return out

def compo(hat, eyes, hairsh, cover, w):
    sh = list(hat)+list(eyes)+clipped_hair(hairsh, cover)
    m = headroom.measure(sh, w)
    return m["area"]*100, m["depth"]*2/w

print("== ③-a 지금 출하 조합의 동시 착용 머리 예산 (배율 0.75) ==")
print("   하한: 면적 12.0% / 두께 1.00획   목표 두께 1.20획")
print(f"{'모자':10s}{'단독':>14s}{'+고글':>12s}{'+고글+머리(최악)':>20s}{'그 최악 머리':>12s}")
worst_global=None
for hn,hsh in items.HEAD.items():
    solo = headroom.measure(hsh, W75)
    ge   = compo(hsh, items.EYES["고글"], [], COVER[hn], W75)
    worst=(999,999,None)
    for an,ash in HAIRS.items():
        a,t = compo(hsh, items.EYES["고글"], ash, COVER[hn], W75)
        if a < worst[0]: worst=(a,t,an)
    print(f"{hn:10s}{solo['area']*100:8.1f}% {solo['depth']*2/W75:4.2f}획"
          f"{ge[0]:7.1f}% {ge[1]:4.2f}획"
          f"{worst[0]:12.1f}% {worst[1]:4.2f}획   {worst[2]}")
    if worst_global is None or worst[0]<worst_global[0]: worst_global=(worst[0],worst[1],hn,worst[2])
print(f"\n★ 출하 조합 최악 = {worst_global[2]} + 고글 + {worst_global[3]} → "
      f"면적 {worst_global[0]:.1f}% / 두께 {worst_global[1]:.2f}획")
print("★ 아이템 하나씩 재는 지금 게이트는 이 값을 **한 번도 보지 않는다**.")

# 모자 없이 머리+안경만
print("\n== ③-b 모자 없이 (머리카락 + 고글) ==")
for an,ash in HAIRS.items():
    a,t = compo([], items.EYES["고글"], ash, float('inf'), W75)
    print(f"  {an:10s} 면적 {a:5.1f}%  두께 {t:5.2f}획")

# 모자에 잘리고 남는 머리카락
print("\n== ③-c 모자를 쓰면 머리카락이 화면에 얼마나 남는가 (면적 R²) ==")
print(f"{'':10s}" + "".join(f"{n:>12s}" for n in items.HEAD))
for an,ash in HAIRS.items():
    row=f"{an:10s}"
    for hn,hsh in items.HEAD.items():
        vis = headroom.hair_visible_area(ash, hsh, COVER[hn], W75)
        row += f"{vis:12.4f}"
    print(row)
solo_hair = {an: headroom.hair_visible_area(ash, [], float('inf'), W75) for an,ash in HAIRS.items()}
print(f"{'(모자 없음)':10s}" + "".join(f"{solo_hair[a]:12.4f}" for a in HAIRS))
print("★ 왕관(HatCoverLocalY=+∞)만 머리카락을 남긴다. 나머지 5종은 사실상 0.")

print("\n== ③-d EYES 6종 단독 머리 예산 (배율 0.75) — 아무도 이 축으로 EYES를 잰 적이 없다 ==")
for en,esh in items.EYES.items():
    m = headroom.measure(esh, W75)
    print(f"  {en:10s} 잔여 면적 {m['area']*100:5.1f}%   잔여 두께 {m['depth']*2/W75:5.2f}획   "
          f"{'★ 하한 12% 미달' if m['area']<0.12 else ''}")

print("\n== ③-e 머리카락 총면적(모자 없음) ==")
for an,ash in HAIRS.items():
    print(f"  {an:10s} {headroom.hair_visible_area(ash, [], float('inf'), W75):7.4f} R²")

print("\n== ③-f 모자별 머리카락 잔존율 ==")
print(f"{'':10s}" + "".join(f"{n:>10s}" for n in items.HEAD))
for an,ash in HAIRS.items():
    full = headroom.hair_visible_area(ash, [], float('inf'), W75)
    row=f"{an:10s}"
    for hn,hsh in items.HEAD.items():
        vis = headroom.hair_visible_area(ash, hsh, COVER[hn], W75)
        row += f"{vis/full*100:9.0f}%"
    print(row)
