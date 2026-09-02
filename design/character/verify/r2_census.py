# -*- coding: utf-8 -*-
"""R2 ① 본체 실루엣 예산 + ② 기본 42종이 실제로 점유한 자리 (기준선)."""
import math, sys
sys.path.insert(0, "/Users/kjmoon/App/StickMate/design/equipment/verify")
sys.path.insert(0, "/Users/kjmoon/App/StickMate/design/character/verify")
import rig, items, headroom, hair, r2_body as B

B.print_cal()
R_INK = 1.171932                 # 머리 잉크 바깥 반경(R)
SH, HIP, TL = rig.SHOULDER_R, rig.HIP_R, rig.TORSO_R
W75 = headroom.stroke_in_R(0.75)

# ============================== ① 본체 =====================================
print("== ① 본체의 사실 (R 단위 · 머리 중심 원점 · +x = 진행) ==")
print(f"전신                {B.H/B.R0:8.4f} R    배율 0.35/0.75/1.00 = "
      f"{B.H*0.35*B.PPU_RUN:.1f}/{B.H*0.75*B.PPU_RUN:.1f}/{B.H*1.0*B.PPU_RUN:.1f} OS pt")
print(f"머리 잉크 바깥 반경  {R_INK:8.6f} R    (2R 아니다 — 링 두께 절반이 더 붙는다)")
print(f"어깨 / 엉덩이 / 발  {SH:8.5f} / {HIP:.5f} / {(0-(B.H-B.R0))/B.R0:.5f} R")
print(f"몸통 길이           {TL:8.5f} R")
for nm,b in (("몸통",B.W_TORSO0),("팔",B.W_ARM0),("다리",B.W_LEG0),
             ("머리 링",B.RING_EFF),("장비 설계획",B.W_ACC0),("FX/PET 설계획",B.W_FX0)):
    print(f"  획 {nm:12s} {b/B.R0:7.5f} R")

neck_gap = SH*-1 - R_INK        # 어깨(-1.31818)와 머리 잉크 밑(-1.171932) 사이
print(f"\n★ 드러난 목(머리 잉크 밑 ~ 어깨) = {neck_gap:.5f} R")
for s in (0.35,0.60,0.75,1.00):
    w = B.W_acc(s, B.PPU_RUN)
    print(f"   배율 {s:.2f}: {neck_gap/w:5.3f} 획   ({'존재' if neck_gap/w>=1 else '★ 획보다 얇다 = 화면에 없다'})")

# 잉크 면적
head_disc = math.pi*R_INK**2
torso_a = TL*(B.W_TORSO0/B.R0)
arm_a   = 2*((B.ARM_UP+B.ARM_LO)/B.R0)*(B.W_ARM0/B.R0)
leg_a   = 2*((B.LEG_UP+B.LEG_LO)/B.R0)*(B.W_LEG0/B.R0)
tot = head_disc+torso_a+arm_a+leg_a
print(f"\n== 본체 잉크 면적 (R²) ==")
for nm,a,cover in (("머리 원반",head_disc,"덮을 수 있다(HAIR6/NECK7/EYES8/HEAD10)"),
                   ("몸통",torso_a,"못 덮는다"),("팔 2",arm_a,"못 덮는다"),("다리 2",leg_a,"못 덮는다")):
    print(f"  {nm:9s} {a:7.4f}  {a/tot*100:5.1f}%   {cover}")
print(f"  合        {tot:7.4f}")
print(f"★ 장비가 건드릴 수 있는 본체 잉크는 머리 원반 {head_disc/tot*100:.1f}%뿐. "
      f"나머지 {100-head_disc/tot*100:.1f}%는 구조가 지킨다.")

# 손끝
def tip(u,l,ud,ld):
    a=math.radians(ud); b=math.radians(ud+ld)
    return (u*math.sin(a)+l*math.sin(b), -(u*math.cos(a)+l*math.cos(b)))
hx,hy = tip(B.ARM_UP/B.R0, B.ARM_LO/B.R0, B.ARM_SPREAD, B.ELBOW)
HAND = (hx, SH+hy)
print(f"\n★ 중립 손끝 = ({HAND[0]:+.4f}, {HAND[1]:+.4f}) R   잉크 반폭 포함 최대 |x| = "
      f"{hx + (B.W_ARM0/B.R0)/2:.4f} R   (팔 전장 {(B.ARM_UP+B.ARM_LO)/B.H:.4f} H — 인체 0.44)")

# ============================== ② 기본 42종 census ==========================
def bbox(shapes):
    xs=[];ys=[]
    for s in shapes:
        for x,y in s.pts: xs.append(x); ys.append(y)
    return min(xs),min(ys),max(xs),max(ys)

print("\n== ② BACK 6종 — 어깨 위(y)를 쓰는가 ==")
print(f"{'아이템':12s}{'y_max(중심선)':>14s}{'y_max(잉크)':>12s}{'x_min(잉크)':>12s}")
back_ceiling=-9
for nm,sh in items.BACK.items():
    x0,y0,x1,y1 = bbox(sh)
    back_ceiling=max(back_ceiling, y1+W75/2)
    print(f"{nm:12s}{y1:>14.4f}{y1+W75/2:>12.4f}{x0-W75/2:>12.4f}")
print(f"★ BACK 슬롯 6종의 잉크 천장 = {back_ceiling:+.4f} R. 어깨 {SH:.4f} 위로 "
      f"{back_ceiling-SH:.3f} R까지만 올라간다.")
print(f"★ 머리 잉크 밑({-R_INK:+.4f})보다 {(-R_INK)-back_ceiling:+.4f} R 아래 = "
      f"{((-R_INK)-back_ceiling)/W75:.2f}획. **머리 옆·위의 BACK 층은 완전히 비어 있다.**")

print("\n== ② EYES 6종 — 머리 원반 위 세로 점유 ==")
print(f"{'아이템':12s}{'y_min':>9s}{'y_max':>9s}{'|x|max':>9s}{'머리 잉크 밖 돌출(획)':>22s}")
for nm,sh in items.EYES.items():
    x0,y0,x1,y1 = bbox(sh)
    out = max(abs(x0),abs(x1))+W75/2 - R_INK
    print(f"{nm:12s}{y0:>9.3f}{y1:>9.3f}{max(abs(x0),abs(x1)):>9.3f}{out/W75:>22.2f}")

print("\n== ② NECK 6종 — 목 앞(x=0)을 가로막는가 ==")
NECKY = SH+0.04
for nm,sh in items.NECK.items():
    x0,y0,x1,y1 = bbox(sh)
    bridged = any(rig.contains(s.pts,(0.0,NECKY)) for s in sh if s.filled)
    print(f"{nm:12s} x=0 목선 채움 통과: {'예' if bridged else '아니오':4s}   "
          f"세로 {y0:+.3f}..{y1:+.3f}  (밑단 {(NECKY-y0)/TL*100:.0f}% 몸통길이)")

print("\n== ② HAIR 6종 — 머리 원반을 얼마나 먹는가 (모자 없이 단독 착용) ==")
HAIRS = {"삐친머리":hair.cowlick(), "단정한머리":hair.straight(), "곱슬머리":hair.curly(),
         "민머리":hair.bald()}
try:
    HAIRS["바가지머리"]=hair.bowl(); HAIRS["포니테일"]=hair.ponytail()
except Exception as e:
    print("   (바가지/포니테일 함수명 미확인 — 건너뜀: %s)"%e)
for nm,sh in HAIRS.items():
    m = headroom.measure(sh, W75)
    print(f"{nm:12s} 잔여 머리 면적 {m['area']*100:5.1f}%   잔여 두께 {m['depth']*2/W75:5.2f}획")
