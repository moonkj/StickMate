# -*- coding: utf-8 -*-
"""R2 ⑥ 마감 숫자 — 배율표 / 팔 함정 / 챙 함정 / 머리카락 / 외형 2종 선택 근거."""
import math, sys
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/character/verify")
import rig, items, headroom, hair, r2_body as B, r2_pack as P
from rig import Shape
R_INK=1.171932; SH=rig.SHOULDER_R; W75=P.W75

print("== ⑥-1 배율표 — 이 팩의 요소가 화면에서 몇 pt인가 (F_run = 40.9167 pt/unit) ==")
print(f"{'배율':>5}{'전신pt':>8}{'머리지름pt':>11}{'장비펜R':>10}{'장비펜pt':>10}{'모자폭pt':>10}{'렌즈높이pt':>11}{'우산폭pt':>10}")
for s in (0.35,0.60,0.75,1.00):
    ppu=B.PPU_RUN; Rw=0.22*s
    pen_world=max(0.048*s, 2.0/ppu)
    row=(s, B.H*s*ppu, B.head_ink_D(s)*ppu, pen_world/Rw, pen_world*ppu,
         (1.892*2)*Rw*ppu, (1.16-0.36)*Rw*ppu, (2.612+0.432)*Rw*ppu)
    print(f"{row[0]:5.2f}{row[1]:8.1f}{row[2]:11.2f}{row[3]:10.4f}{row[4]:10.2f}{row[5]:10.1f}{row[6]:11.2f}{row[7]:10.1f}")
print("  ★ 0.35은 프로덕션이 스스로 '실루엣 전용 구간'이라 선언한 대역이다"
      " (AccessoryStrokeBudgetTests 185~187행: 획 0.74R). 디테일 게이트가 아니다.")

print("\n== ⑥-2 함정 A — 팔 0.3297 H. 「우산을 든다」가 가능한가 ==")
hand=(2.39860, SH-2.40416)
hook=(0.06,-3.24)
d=math.hypot(hand[0]-hook[0], hand[1]-hook[1])
print(f"  중립 손끝 ({hand[0]:+.3f},{hand[1]:+.3f})  우산 손잡이 끝 ({hook[0]:+.3f},{hook[1]:+.3f})")
print(f"  거리 {d:.3f} R = {d/W75:.1f}획  → 「쥐었다」로 읽힐 수 없다.")
print("  ★ 게다가 BACK(sort -1)은 **팔에 따라오지 않는다**. 보행 중 팔은 흔들리고 우산은 안 흔들린다.")
print("  ⇒ 설계 결론: 우산은 「든 것」이 아니라 「어깨에 기댄 것」이다. 손이 없으니 애초에 못 쥔다.")

print("\n== ⑥-3 함정 B — 챙은 바깥으로. 머리 잉크 밖 돌출량 δ_out (획) ==")
def dout(sh):
    best=-9
    for s_ in sh:
        for x,y in s_.pts:
            best=max(best, math.hypot(x,y)+W75/2-R_INK)
    return best/W75
for nm,sh in items.HEAD.items(): print(f"    {nm:8s} {dout(sh):6.2f}획")
print(f"    ---- 팩 신문 모자 {dout(P.PACK['HEAD'][1]):6.2f}획  (뿔 끝 |x|=1.72 R)")
print(f"    팩 김 서린 안경 {dout(P.PACK['EYES'][1]):6.2f}획  (렌즈 밑 — 턱 아래로 나가는 첫 EYES)")
for nm,sh in items.EYES.items(): print(f"      [대조] {nm:8s} {dout(sh):6.2f}획")

print("\n== ⑥-4 외형 2종 선택 — HAIR / FX / PET 중 둘 ==")
HAIRS={"삐친머리":hair.cowlick(),"단정한머리":hair.straight(),"곱슬머리":hair.curly(),
       "민머리":hair.bald(),"바가지머리":hair.bowl(),"포니테일":hair.ponytail()}
hat=P.PACK["HEAD"][1]
print("  (a) HAIR를 넣으면 — 팩의 모자가 팩의 머리카락을 자른다(HatCoverLocalY=%.2f)"%P.HAT_COVER)
for an,ash in HAIRS.items():
    full=headroom.hair_visible_area(ash,[],float('inf'),W75)
    vis =headroom.hair_visible_area(ash,hat,P.HAT_COVER,W75)
    print(f"      {an:10s} {vis:.3f}/{full:.3f} R² = {vis/full*100:4.0f}% 잔존")
print("  (b) 머리 원반을 놓고 싸우는 아이템 수: HAIR 포함 3/6 vs 미포함 2/6")
print("  (c) FX = 서 있을 때 본체 잉크 잠식 0 / PET = 본체 밖 → 둘 다 실루엣 예산 0")
print("  ⇒ 외형 2종 = **FX + PET**. (조건부 예외는 본문 6-3절)")

print("\n== ⑥-5 팩 모자 단독 외곽호 vs 기본 6종 ==")
for nm,sh in items.HEAD.items():
    m=headroom.measure(sh,W75); print(f"    {nm:8s} 외곽호 {m['arc']:5.0f}°  최대연속 {m['arc_run']:5.0f}°  잔여면적 {m['area']*100:4.1f}%")
m=headroom.measure(hat,W75)
print(f"    ---- 신문 모자 외곽호 {m['arc']:5.0f}°  최대연속 {m['arc_run']:5.0f}°  잔여면적 {m['area']*100:4.1f}%")
me=headroom.measure(hat+P.PACK['EYES'][1],W75)
print(f"    ---- 모자+안경   외곽호 {me['arc']:5.0f}°  최대연속 {me['arc_run']:5.0f}°  잔여면적 {me['area']*100:4.1f}%")
print(f"  머리 위에 얹힌 깊이(모자 잉크 밑단 {m['ink_bottom']:+.3f} ~ 머리 잉크 위 +{R_INK:.3f}) = "
      f"{(R_INK-m['ink_bottom']):.3f} R = {(R_INK-m['ink_bottom'])/W75:.2f}획  (떠 있지 않다)")
