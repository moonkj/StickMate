# -*- coding: utf-8 -*-
"""design-character DLC 팩 검산자.
★ 먼저 알려진 값으로 교정한다. 교정이 깨지면 뒤 숫자를 전부 폐기한다.
출처: Assets/Editor/SceneBootstrapper.cs, design/equipment/verify/rig.py,
      docs/CHARACTER_FORM_SPEC.md R2 8-2 / 9-2 / 21-4
"""
import math

# ---- 프로덕션 상수(그대로 옮김) ----------------------------------------
H          = 2.2746944          # BaselineCharacterTotalHeight (world)
R0         = 0.22               # 머리 반경 (world, s=1)
SHOULDER   = 1.7646944
HIP        = 0.9346944
HEAD_C     = H - R0             # 2.0546944
LWS        = 1.045              # LineWidthScale
W_TORSO0   = 0.11 * LWS         # 0.114950
W_LEG0     = 0.12 * LWS         # 0.125400
W_ARM0     = 0.10 * LWS         # 0.104500
RING_EFF   = 0.0756501          # 실효 링 계수 (R2 9-3)
W_ACC0     = 0.048              # 액세서리/장비 설계 획 (BaselineStrokeWidth)
ARM_UP, ARM_LO = 0.38, 0.37
LEG_UP, LEG_LO = 0.50, 0.45
ARM_SPREAD, ELBOW = 40.0, 10.0
LEG_SPREAD, KNEE  = 12.0, -4.0

PPU_RUN  = 40.9167              # 이 맥 실측 (R2 8-1)
PPU_BAKE = 846.0/24.0           # 35.25  굽기 근사
MIN_LINE_PT = 2.0               # MinStrokeScreenPoints
MIN_FILL_PT = 1.0               # MinFillOutlineScreenPoints (M6)

def R(s): return R0*s
def ring(s, ppu=PPU_RUN): return max(RING_EFF*s, MIN_LINE_PT/ppu)
def head_ink_D(s, ppu=PPU_RUN): return 0.44*s + ring(s, ppu)
def w_world(base, s, ppu=PPU_RUN, floor_pt=MIN_LINE_PT):
    return max(base*s, floor_pt/ppu)
def in_R(world, s): return world/R(s)

# ---- 교정 1: 몸통 획 ÷ 머리 잉크 지름 = 22.29% (배율 0.75, 1.00) --------
for s in (0.75, 1.00):
    v = w_world(W_TORSO0, s)/head_ink_D(s)
    assert abs(v-0.2229) < 0.0002, (s, v)
# ---- 교정 2: pt 값이 9-2표와 맞는가 (배율 1.00) -------------------------
chk = {'arm':4.28,'torso':4.70,'leg':5.13,'ring':3.10,'D':21.10}
got = {'arm':w_world(W_ARM0,1)*PPU_RUN,'torso':w_world(W_TORSO0,1)*PPU_RUN,
       'leg':w_world(W_LEG0,1)*PPU_RUN,'ring':ring(1)*PPU_RUN,'D':head_ink_D(1)*PPU_RUN}
for k in chk: assert abs(got[k]-chk[k])<0.02, (k,got[k],chk[k])
# ---- 교정 3: rig.py의 W(배율 0.75, F_bake) = 0.343864 R ------------------
assert abs(in_R(w_world(W_ACC0,0.75,PPU_BAKE),0.75)-0.343864) < 1e-5
# ---- 교정 4: 배정문의 W_line@0.60(F_bake) = 0.4298 R ---------------------
assert abs(in_R(w_world(W_ACC0,0.60,PPU_BAKE),0.60)-0.42983) < 1e-4
# ---- 교정 5: 팔 = 0.3297 H (CLAUDE.md 인계계약 기록값) -------------------
assert abs((ARM_UP+ARM_LO)/H - 0.3297) < 0.0002
# ---- 교정 6: 등급 사다리 — 기본 42종 슬롯당 2/2/1/1 ----------------------
LADDER = ['일반','일반','희귀','희귀','영웅','전설']
def rarity_of_rank(rank, count):
    if count<=0: return '일반'
    rank = max(0, min(rank, count-1))
    step = rank*len(LADDER)//count
    return LADDER[min(step, len(LADDER)-1)]
base6 = [rarity_of_rank(r,6) for r in range(6)]
assert base6 == LADDER, base6
print("교정 6/6 통과 — 아래 숫자를 신뢰한다.\n")

# ---- 파생 상수 (R 단위, 머리 중심 원점) ---------------------------------
SH  = (SHOULDER-HEAD_C)/R0      # -1.31818
HIPR= (HIP-HEAD_C)/R0           # -5.09091
TL  = (SHOULDER-HIP)/R0         #  3.77273
FOOT= (0-HEAD_C)/R0             # -9.33952
TOTAL_R = H/R0
NECK_LEN = (H - R0*2 - (SHOULDER-0))/R0 if False else (HEAD_C-R0-SHOULDER)/R0
print("== 몸의 사실 (R 단위, 머리 중심 원점) ==")
print(f"전신 높이            {TOTAL_R:.4f} R      머리 지름 2R = 2")
print(f"어깨 SH              {SH:.5f} R")
print(f"엉덩이 HIP           {HIPR:.5f} R")
print(f"몸통 길이 TL         {TL:.5f} R")
print(f"발바닥               {FOOT:.5f} R")
print(f"드러난 목 길이       {NECK_LEN:.5f} R   (턱 -1.0 -> 어깨 {SH:.3f})")
print(f"팔 전장              {(ARM_UP+ARM_LO)/R0:.4f} R   ({(ARM_UP+ARM_LO)/H:.4f} H)")
print(f"다리 전장            {(LEG_UP+LEG_LO)/R0:.4f} R")

# 획 두께(R 단위) — 하한 안 물리는 구간에서 배율 무관
print("\n== 획 두께 (R 단위, 하한 미작동 구간) ==")
for nm, b in (('몸통',W_TORSO0),('팔',W_ARM0),('다리',W_LEG0),('머리 링',RING_EFF),('장비 설계획',W_ACC0)):
    print(f"{nm:10s} {b/R0:.5f} R")
print(f"목 = 몸통과 같은 선(Torso가 턱까지 올라온다) -> 목 폭 {W_TORSO0/R0:.5f} R")
print(f"목 종횡비 = 길이 {NECK_LEN:.4f} / 폭 {W_TORSO0/R0:.4f} = {NECK_LEN/(W_TORSO0/R0):.4f}")

# ---- 본체 잉크 면적 (R^2) ------------------------------------------------
print("\n== 본체 잉크 면적 (R^2, 하한 미작동 구간) ==")
head_disc = math.pi*(1.0 + (RING_EFF/R0)/2.0)**2   # 원반 + 링 바깥 절반
torso_a   = TL*(W_TORSO0/R0)
arm_a     = 2*((ARM_UP+ARM_LO)/R0)*(W_ARM0/R0)
leg_a     = 2*((LEG_UP+LEG_LO)/R0)*(W_LEG0/R0)
neck_a    = NECK_LEN*(W_TORSO0/R0)
total_ink = head_disc+torso_a+arm_a+leg_a
print(f"머리 원반(링 포함) {head_disc:7.4f}   {head_disc/total_ink*100:5.1f}%")
print(f"몸통               {torso_a:7.4f}   {torso_a/total_ink*100:5.1f}%")
print(f"팔 2개             {arm_a:7.4f}   {arm_a/total_ink*100:5.1f}%")
print(f"다리 2개           {leg_a:7.4f}   {leg_a/total_ink*100:5.1f}%")
print(f"合                 {total_ink:7.4f}")
print(f"★ 머리가 본체 잉크의 {head_disc/total_ink*100:.1f}% — 단일 최대 덩어리이자 유일한 '면'")

# ---- 중립 자세 실루엣 폭 -------------------------------------------------
def limb_tip(up_len, lo_len, up_deg, lo_deg):
    a = math.radians(up_deg); b = math.radians(up_deg+lo_deg)
    return (up_len*math.sin(a)+lo_len*math.sin(b), -(up_len*math.cos(a)+lo_len*math.cos(b)))
hx,hy = limb_tip(ARM_UP/R0, ARM_LO/R0, ARM_SPREAD, ELBOW)
ex,ey = ( (ARM_UP/R0)*math.sin(math.radians(ARM_SPREAD)), -(ARM_UP/R0)*math.cos(math.radians(ARM_SPREAD)) )
fx,fy = limb_tip(LEG_UP/R0, LEG_LO/R0, LEG_SPREAD, KNEE)
print("\n== 중립 자세 실루엣 (R, 어깨/엉덩이 기준 상대) ==")
print(f"팔꿈치  x {ex:+.4f}  y {SH+ey:+.4f}")
print(f"손끝    x {hx:+.4f}  y {SH+hy:+.4f}   (팔 반폭 {W_ARM0/R0/2:.4f} 포함 시 x {hx+W_ARM0/R0/2:+.4f})")
print(f"발끝    x {fx:+.4f}  y {HIPR+fy:+.4f}")
print(f"★ 어깨~손끝 구간 몸 반폭(잉크 포함) = {hx+W_ARM0/R0/2:.4f} R  vs 머리 반경 1.0 R")
print(f"★ 즉 중립 실루엣의 최대 반폭은 {max(1.0, hx+W_ARM0/R0/2):.4f} R — 머리가 폭도 지배한다"
      if hx+W_ARM0/R0/2 < 1.0 else
      f"★ 손끝이 머리보다 {(hx+W_ARM0/R0/2)-1.0:+.4f} R 바깥")

# ---- 화면상 획/하한 표 ---------------------------------------------------
print("\n== 배율별 실제 펜 (R 단위) — F_run(이 맥) 기준 ==")
print(f"{'배율':>6} {'낱선 2pt':>10} {'채움윤곽 1pt(M6)':>16} {'설계획':>8} {'머리지름R':>10}")
for s in (0.35,0.50,0.60,0.75,1.00):
    wl = in_R(w_world(W_ACC0,s,PPU_RUN,MIN_LINE_PT),s)
    wf = in_R(w_world(W_ACC0,s,PPU_RUN,MIN_FILL_PT),s)
    print(f"{s:6.2f} {wl:10.4f} {wf:16.4f} {W_ACC0/R0:8.4f} {head_ink_D(s)/R(s):10.4f}")
