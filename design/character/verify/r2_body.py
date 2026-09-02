# -*- coding: utf-8 -*-
"""design-character R2 — 본체 실루엣 예산의 자(尺).
★ 먼저 알려진 값으로 교정한다. 교정이 깨지면 뒤 숫자를 전부 폐기한다.

교정의 출처는 **내가 만든 것이 아니다**(TEAM.md §"생성기와 검사기"):
 · 획/비율      : Core/StickConfig.cs · Core/StickmanStrokeWidths.cs · Editor/SceneBootstrapper.cs
 · 머리 예산 실측: design/equipment/verify/headroom.py 문서의 **실기 캡처 교정표**(6종)
 · 도형         : design/equipment/verify/{rig,items,hair,appearance}.py (남의 자 — 내가 안 만들었다)
"""
import math, sys, os
EQ = "/Users/kjmoon/App/StickMate/design/equipment/verify"
sys.path.insert(0, EQ)
import rig, items, headroom, hair

# ---------------------------------------------------------------- 프로덕션 상수(그대로 옮김)
H        = 2.2746944       # StickConfig.BaselineCharacterTotalHeight
R0       = 0.22            # 머리 반경(world, s=1)  SceneBootstrapper
SHOULDER = 1.7646944
HIP      = 0.9346944
HEAD_C   = H - R0
LWS      = 1.045           # StickmanStrokeWidths.LineWidthScale
W_TORSO0 = 0.11 * LWS
W_LEG0   = 0.12 * LWS
W_ARM0   = 0.10 * LWS
RING_EFF = 0.0756501       # 실효 링 계수
W_ACC0   = 0.048           # AccessoryShapeBuilder.BaselineStrokeWidth
W_FX0    = 0.022 * H       # CharacterFxRenderer.StrokeRatio x 신장 = 0.050043
ARM_UP, ARM_LO = 0.38, 0.37
LEG_UP, LEG_LO = 0.50, 0.45
ARM_SPREAD, ELBOW = 40.0, 10.0
PPU_RUN  = 40.9167         # 이 맥 실측
PPU_BAKE = 846.0/24.0      # 35.25  굽기 근사
MIN_LINE_PT = 2.0          # StickConfig.MinStrokeScreenPoints
MIN_FILL_PT = 1.0          # StickConfig.MinFillOutlineScreenPoints

def Rw(s):   return R0*s
def ring(s, ppu=PPU_RUN):        return max(RING_EFF*s, MIN_LINE_PT/ppu)
def head_ink_D(s, ppu=PPU_RUN):  return 0.44*s + ring(s, ppu)
def w_world(base, s, ppu=PPU_RUN, floor=MIN_LINE_PT): return max(base*s, floor/ppu)
def inR(world, s): return world/Rw(s)
def W_acc(s, ppu=PPU_BAKE): return inR(w_world(W_ACC0, s, ppu), s)   # 장비 펜(R 단위)

# ---------------------------------------------------------------- 교정 (전부 통과해야 한다)
CAL = []
def cal(name, got, want, tol):
    ok = abs(got-want) <= tol
    CAL.append((name, got, want, ok)); return ok

cal("몸통획/머리잉크지름 @0.75", w_world(W_TORSO0,0.75)/head_ink_D(0.75), 0.2229, 0.0002)
cal("몸통획/머리잉크지름 @1.00", w_world(W_TORSO0,1.00)/head_ink_D(1.00), 0.2229, 0.0002)
cal("팔전장/신장",              (ARM_UP+ARM_LO)/H, 0.3297, 0.0002)
cal("장비펜 W(0.75,bake)",       W_acc(0.75), 0.343864, 1e-5)
cal("rig.W",                     rig.W,        0.343864, 1e-5)
cal("머리잉크 바깥반경(R)",       1.0 + (RING_EFF/R0)/2.0, 1.171932, 1e-5)
cal("headroom 획(0.75)",         headroom.stroke_in_R(0.75), 0.343864, 1e-5)

# ★ 남의 자(headroom.py)를 그 문서가 적어 둔 **실기 교정표**로 다시 잰다.
#   이 표는 내가 만든 값이 아니다. 깨지면 그 자가 낡은 것이고 내 숫자는 전부 무효다.
# ★ 남의 자(headroom.py)를 **내가 만들지 않은 표**로 다시 잰다 —
#   design/equipment/HAT_HEADROOM_PRESCRIPTION.md §4의 "after" 열(= 현행 프로덕션 좌표).
#   before 열이 아니라 after 열인 이유: 그 처방이 이미 프로덕션에 들어가 있다
#   (AccessoryShapeBuilder.BeanieBandBottomRatio = -0.26f, HatBrimRootDropRatio = 0.18f).
#   headroom.py **독스트링**의 교정표는 before 값이라 낡았다 — 그걸 쓰면 4/6이 깨진다(실제로 깨 봤다).
HAT_CAL = {
  0.75: {"야구모자":(29.2,2.01), "털모자":(25.2,1.65), "중절모":(24.0,1.68),
         "왕관":(34.1,2.12), "베레모":(38.2,2.38), "밀짚모자":(24.0,1.68)},
  0.60: {"야구모자":(26.6,1.51), "털모자":(22.5,1.22), "중절모":(21.5,1.25),
         "왕관":(31.4,1.59), "베레모":(35.5,1.80), "밀짚모자":(21.5,1.25)},
}
HATS = items.HEAD
for sc, tab in HAT_CAL.items():
    wsc = headroom.stroke_in_R(sc)
    for nm,(a_want,t_want) in tab.items():
        m = headroom.measure(HATS[nm], wsc)
        cal("headroom 면적 %s@%.2f"%(nm,sc), m["area"]*100.0, a_want, 0.06)
        cal("headroom 두께 %s@%.2f"%(nm,sc), m["depth"]*2.0/wsc, t_want, 0.006)
w75 = headroom.stroke_in_R(0.75)

def print_cal():
    bad = [c for c in CAL if not c[3]]
    print("== 교정 %d/%d ==" % (len(CAL)-len(bad), len(CAL)))
    for nm,g,w,ok in CAL:
        if not ok: print("  ✗ %-28s got %.5f  want %.5f" % (nm,g,w))
    if bad:
        print("★ 교정이 깨졌다 — 이 실행의 숫자를 전부 폐기한다."); sys.exit(1)
    print("교정 전부 통과 — 아래 숫자를 신뢰한다.\n")

if __name__ == "__main__":
    print_cal()
