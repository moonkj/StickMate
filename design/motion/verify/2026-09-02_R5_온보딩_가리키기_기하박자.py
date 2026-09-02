# -*- coding: utf-8 -*-
"""design-motion R5-B — 온보딩 "손을 올려 설명" 자세의 기하·박자 검산.
값의 출처는 Editor/SceneBootstrapper.cs / States/StickmanPoseAnimator.cs / Core/StickConfig.cs /
Interaction/InfoGearIconWidget.cs 이며 이 파일은 산술만 한다."""
import math

# ── 프로덕션 상수 (grep 확인) ──────────────────────────────────────────────
ARM_U, ARM_L   = 0.38, 0.37     # SceneBootstrapper.cs:257 BaselineArm{Upper,Lower}Length
LEG_U, LEG_L   = 0.50, 0.45     # :258
HIP_Y          = 0.45           # :819 BaselineSpecHipY
SHO_Y_SPEC     = 1.28           # :819 BaselineSpecShoulderY
TORSO_TOP_SPEC = 1.35           # :819 BaselineSpecTorsoTopY
HEAD_R         = 0.22           # :210 BaselineHeadVisualRadius
IDLE_LEG_SPREAD= 12.0           # :253 / StickConfig.idleLegSpreadDegrees
IDLE_KNEE      = 4.0            # :263 IdleKneeBendDegrees
IDLE_ARM_SPREAD= 40.0           # :248 / StickConfig.idleArmSpreadDegrees
IDLE_ELBOW     = 10.0           # :264 / StickConfig.idleElbowBendDegrees
SHO_FWD_LIM    = 150.0          # :339 ShoulderSwingForwardLimitDegrees
SHO_BACK_LIM   = 60.0           # :338
MIN_JOINT_BEND = 3.0            # :299 MinJointBendDegrees
BASE_H         = 2.2746944      # StickConfig.cs:1816
poseSmoothing  = 35.0           # StickConfig.cs:80
ArmSmoothRatio = 0.55           # StickmanPoseAnimator.cs:190
LowerSegRatio  = 0.75           # :194
bodyLeanRate   = 12.0           # StickConfig.cs:2934
GEAR_TIP_R_PT  = 13.8           # InfoGearIconWidget.cs:168 TipRadiusPoints
PT_PER_UNIT    = 1004.9/24.5609 # Dock 실측 환산
VIS_FLOOR_PT   = 3.9            # R4 §1-5 가시 하한

# 대사 예산 (DialogueKind.cs — design-narrative R2 SPEC과 같은 식)
D_BASE, D_PER, D_MIN, D_MAX, D_FADE = 0.28, 0.075, 0.62, 2.20, 0.06
D_LATIN = 0.0472               # narrative R2 제안 라틴 계수(아직 프로덕션 아님)

def L(t=""): print(("─"*76) if not t else f"\n{'='*76}\n{t}\n{'='*76}")

# ── 0. 골격 좌표 유도 (SceneBootstrapper와 같은 식) ─────────────────────────
def limb_drop(hip, u, l, knee):   # :348 LimbDrop
    return u*math.cos(math.radians(hip)) + l*math.cos(math.radians(hip - knee))
footLift = max(limb_drop(-IDLE_LEG_SPREAD,LEG_U,LEG_L,IDLE_KNEE),
               limb_drop( IDLE_LEG_SPREAD,LEG_U,LEG_L,IDLE_KNEE)) - HIP_Y
SHO_Y   = SHO_Y_SPEC   + footLift
HEAD_Y  = TORSO_TOP_SPEC + footLift + HEAD_R
TOTAL_H = HEAD_Y + HEAD_R
ARM_MAX = ARM_U + ARM_L

L("[0] 골격 — 유도값이 BaselineCharacterTotalHeight를 재현하는가 (교정)")
print(f"  footLift = {footLift:.4f}")
print(f"  어깨 y = {SHO_Y:.4f} = {SHO_Y/BASE_H:.4f} H")
print(f"  머리중심 y = {HEAD_Y:.4f} = {HEAD_Y/BASE_H:.4f} H,  머리끝 y = {TOTAL_H:.4f}")
print(f"  전신 = {TOTAL_H:.7f}  vs  StickConfig.BaselineCharacterTotalHeight {BASE_H}  "
      f"{'✓ 재현' if abs(TOTAL_H-BASE_H)<1e-4 else '★실패 — 이하 폐기'}")
assert abs(TOTAL_H-BASE_H) < 1e-4
print(f"  팔 전장 = {ARM_MAX:.4f} = {ARM_MAX/BASE_H:.4f} H   (리더 제시 0.3297 H "
      f"{'✓' if abs(ARM_MAX/BASE_H-0.3297)<0.0005 else '✗'} / 인체 0.44)")

def hand(sho, elb):   # 어깨 원점 기준. 0도 = 연직 아래, + = facing 앞. 전완 절대각 = 어깨+팔꿈치
    return (ARM_U*math.sin(math.radians(sho)) + ARM_L*math.sin(math.radians(sho+elb)),
            -(ARM_U*math.cos(math.radians(sho)) + ARM_L*math.cos(math.radians(sho+elb))))

# ── 1. 손끝이 톱니에 닿는가 ────────────────────────────────────────────────
L("[1] ★ 손끝이 톱니에 닿는가 — 숫자로 먼저 판정한다 (리더 지시)")
print("  도달 반경(팔꿈치 굽힘 e에 따라)")
for e in (0,8,12,20,30):
    r = math.sqrt(ARM_U**2 + ARM_L**2 + 2*ARM_U*ARM_L*math.cos(math.radians(e)))
    print(f"    e={e:>3}도 → |손−어깨| = {r:.4f} 유닛 = {r/BASE_H:.4f} H")
r12 = math.sqrt(ARM_U**2+ARM_L**2+2*ARM_U*ARM_L*math.cos(math.radians(12)))
print(f"\n  손끝 최고 높이")
print(f"    관절한계 무시(팔 수직) : {SHO_Y+r12:.4f} = {(SHO_Y+r12)/BASE_H:.4f} H  "
      f"(머리끝 위 {(SHO_Y+r12-TOTAL_H)/BASE_H:+.4f} H)")
hx,hy = hand(SHO_FWD_LIM, 12); hy += SHO_Y
print(f"    어깨한계 {SHO_FWD_LIM:.0f}도 + e=12 : ({hx:.4f}, {hy:.4f}) = {hy/BASE_H:.4f} H  "
      f"(머리끝 위 {(hy-TOTAL_H)/BASE_H:+.4f} H)")
print(f"\n  {'배율':>6}{'신장pt':>9}{'머리반경pt':>11}{'손끝-머리끝 여유pt':>18}{'톱니반경pt':>11}{'톱니/신장':>10}")
for s in (0.35,0.60,0.75,1.00,2.00):
    Hpt = BASE_H*s*PT_PER_UNIT
    print(f"{s:>6.2f}{Hpt:>9.2f}{HEAD_R*s*PT_PER_UNIT:>11.2f}"
          f"{(hy-TOTAL_H)*s*PT_PER_UNIT:>18.2f}{GEAR_TIP_R_PT:>11.1f}{2*GEAR_TIP_R_PT/Hpt:>10.3f}")
print("\n  ★ 톱니는 **월드가 아니라 화면 포인트 고정**이다(InfoGearIconWidget의 반경은 pt 상수).")
print("    배율 0.35에서 톱니 지름은 신장의 0.848배 — 캐릭터만 하다.")
print("    배율이 바뀌면 '닿음'의 성립 여부가 바뀐다 → **닿기를 요구하는 연출은 만들 수 없다.**")

# 닿으려면 톱니 중심이 머리끝 위 몇 pt 안에 있어야 하나 (배율별)
print(f"\n  가정: 톱니 중심이 캐릭터 중심선 위 (x=0, 머리끝+d). 손끝이 톱니 원반에 닿을 조건")
print(f"  {'배율':>6}{'손끝x(pt)':>11}{'손끝y-머리끝(pt)':>17}{'허용 d 최대(pt)':>16}")
for s in (0.35,0.60,0.75,1.00,2.00):
    fx = hx*s*PT_PER_UNIT; fy = (hy-TOTAL_H)*s*PT_PER_UNIT
    disc = GEAR_TIP_R_PT**2 - fx**2
    dmax = (fy + math.sqrt(disc)) if disc>0 else float('nan')
    print(f"{s:>6.2f}{fx:>11.2f}{fy:>17.2f}{dmax:>16.2f}")
print("    → 배율 2.00에서는 손끝 x가 톱니 반경을 넘어 **중심선 위 톱니에는 어떤 높이에서도 못 닿는다**(nan).")
print("    ★ 판정: **닿지 않는다. 가리킨다.** (근거 3건은 문서 §6-2)")

# ── 2. 조준 각도 밴드 ─────────────────────────────────────────────────────
L("[2] 가리키기 — 조준 각도 밴드 (ux-designer 인계 사양)")
E_POINT = 12.0
print(f"  규약: 어깨각 0=연직 아래, +=facing 앞. 전완 절대각 = 어깨 + 팔꿈치.")
print(f"  가리키는 방향 = **전완 절대각** = aim.  어깨 = aim − pointElbowDegrees({E_POINT:.0f})")
aim_max = SHO_FWD_LIM + E_POINT
print(f"  하드 상한: 어깨 ≤ {SHO_FWD_LIM:.0f}(ShoulderSwingForwardLimitDegrees) → aim ≤ {aim_max:.0f}도"
      f"  = 연직 위로부터 {180-aim_max:.0f}도")
print(f"  하드 하한: aim ≥ 100도(그 아래는 '위'로 안 읽힌다) = 연직 위로부터 80도")
print(f"\n  {'aim':>6}{'어깨':>7}{'연직위로부터':>13}{'손끝 x':>9}{'손끝 y':>9}{'머리중심과 거리':>15}{'머리 침범':>10}")
for aim in (100,110,115,122,134,145,155,162):
    sho = aim - E_POINT
    x,y = hand(sho,E_POINT); y += SHO_Y
    d = math.hypot(x-0.0, y-HEAD_Y)
    print(f"{aim:>6}{sho:>7.0f}{180-aim:>13.0f}{x:>9.4f}{y:>9.4f}{d:>15.4f}"
          f"{'★겹침' if d<HEAD_R else '  없음':>10}")
print(f"  (머리 반경 {HEAD_R})  → 밴드 전 구간에서 손이 머리를 침범하지 않는다.")

# ── 3. 톱니 배치 요구 (ux-designer) ───────────────────────────────────────
L("[3] ux-designer 인계 — 톱니를 어디에 놓아야 이 자세가 성립하는가")
print(f"  요구 A (관절): 톱니 중심이 **어깨점**(발밑 기준 높이 {SHO_Y/BASE_H:.4f} H, x=0)에서 봤을 때")
print(f"                 연직 위로부터 진행 방향 쪽으로 **{180-aim_max:.0f}도 ~ 80도** (권장 35~65도)")
print(f"  요구 B (겹침): 톱니가 머리를 덮지 않으려면 중심 간 거리 ≥ 톱니반경 + 머리반경")
print(f"  {'배율':>6}{'머리반경pt':>11}{'필요 중심거리pt':>16}{'그 거리의 H 환산':>17}")
for s in (0.35,0.60,0.75,1.00,2.00):
    Hpt=BASE_H*s*PT_PER_UNIT; hr=HEAD_R*s*PT_PER_UNIT
    need=GEAR_TIP_R_PT+hr
    print(f"{s:>6.2f}{hr:>11.2f}{need:>16.2f}{need/Hpt:>17.3f} H")
print("  ★ 두 요구는 배율에 따라 서로 다른 방향으로 움직인다(A는 각도=배율 불변, B는 pt=배율 의존).")
print("    배율 0.35에서 B가 요구하는 거리는 신장의 0.61배다 — 톱니가 캐릭터에서 멀리 떨어진다.")
print("    ⇒ **톱니 배치는 배율에 따라 달라져야 한다.** 고정 오프셋 하나로는 두 요구를 동시에 못 만족한다.")

# ── 4. 박자 ───────────────────────────────────────────────────────────────
L("[4] 박자 — 올림 / 유지 / 내림")
AIM = 134.0; SHO_P = AIM - E_POINT
travel = SHO_P - IDLE_ARM_SPREAD
print(f"  어깨 이동량 = {SHO_P:.0f} − Idle {IDLE_ARM_SPREAD:.0f} = {travel:.0f}도")
ix,iy = hand(IDLE_ARM_SPREAD, IDLE_ELBOW); iy += SHO_Y
px,py = hand(SHO_P, E_POINT);              py += SHO_Y
hd = math.hypot(px-ix, py-iy)
print(f"  손 이동거리 = {hd:.4f} 유닛 = {hd/BASE_H:.4f} H = 배율0.60에서 {hd*0.6*PT_PER_UNIT:.2f} pt")

# 상한 기준: 정속 보행 팔 스윙의 최대 각속도를 넘지 않는다
FwdSho=[18.,0.,-18.,0.]; AMP=1.35; f_walk=1.2328
def cat(p0,p1,p2,p3,u):
    u2=u*u;u3=u2*u
    return 0.5*((2*p1)+(-p0+p2)*u+(2*p0-5*p1+4*p2-p3)*u2+(-p0+3*p1-3*p2+p3)*u3)
def samp(k,ph):
    n=len(k);x=(ph%1.)*n;i=int(math.floor(x))%n;u=x-math.floor(x)
    return cat(k[(i-1)%n],k[i],k[(i+1)%n],k[(i+2)%n],u)
N=4000
dmax=max(abs(samp(FwdSho,(i+1)/N)-samp(FwdSho,i/N)) for i in range(N))*AMP*N*f_walk
print(f"\n  기준선 = 정속 보행 팔 스윙의 최대 각속도 = {dmax:.1f} 도/초")
print(f"    (60fps {dmax/60:.2f} 도/프레임 · 15fps {dmax/15:.2f} 도/프레임)")
print(f"  smoothstep으로 {travel:.0f}도를 T초에 옮기면 최대 각속도 = 1.5×{travel:.0f}/T")
Tmin = 1.5*travel/dmax
print(f"  ★ 그 기준선을 넘지 않는 조건: T ≥ 1.5×{travel:.0f}/{dmax:.1f} = **{Tmin:.3f}초**")
for T,nm in ((0.20,"15fps 하한만"),(0.42,"archeryDraw 참고"),(0.70,"1차안 — 반증됨"),(Tmin,"기준선 정확히"),(0.80,"★ 채택"),(0.90,"")):
    print(f"    T={T:.2f}s {nm:<16} 최대 {1.5*travel/T:6.1f} 도/초 = 60fps {1.5*travel/T/60:5.2f} / "
          f"15fps {1.5*travel/T/15:5.2f} 도/프레임  {'✓' if 1.5*travel/T<=dmax else '✗ 기준선 초과'}")
T_RAISE=0.80
# 내림: archeryDraw:archeryRecover = 0.42:0.34 = 0.8095 를 그대로 승계
ratio = 0.34/0.42
T_LOWER = round(T_RAISE*ratio, 2)
print(f"\n  내림 = 올림 × (archeryRecoverSeconds/archeryDrawSeconds = {ratio:.4f}) = {T_RAISE*ratio:.4f} → 채택 {T_LOWER:.2f}초")
print(f"    (이 저장소가 이미 쓰는 '준비는 느리고 해제는 빠르다' 비를 승계한다 — 새 숫자를 만들지 않는다)")

# ── 5. 유지 시간 = 대사 예산 상한 (design-narrative 인계) ──────────────────
L("[5] ★ 유지 시간 = 대사 노출 예산 상한 (design-narrative 인계)")
def need(n, per=D_PER):  return D_FADE + min(max(D_BASE+per*n, D_MIN), D_MAX)
print(f"  필요 노출 = FadeIn {D_FADE} + clamp({D_BASE} + {D_PER}×글자수, {D_MIN}, {D_MAX})")
print(f"  {'유지(초)':>9}{'한글 상한':>10}{'라틴 상한':>10}   비고")
for T in (0.80,1.00,1.20,1.40,1.60,1.80,2.00,2.26,2.60):
    kn = max(0,int((T-D_FADE-D_BASE)/D_PER)) if T-D_FADE >= D_MIN else 0
    while need(kn) > T: kn-=1
    la = max(0,int((T-D_FADE-D_BASE)/D_LATIN))
    while need(la, D_LATIN) > T: la-=1
    note = "★ 채택" if abs(T-1.60)<1e-6 else ("예산 포화(2.20)—더 길게 잡아도 글자 안 늘어남" if T>=2.26 else "")
    print(f"{T:>9.2f}{kn:>10}{la:>10}   {note}")
print(f"\n  ★ 채택 유지 = **1.60초**  → 한글 **16자** / 라틴 **26자**가 상한이다. 넘으면 잘린다.")
print(f"     상한 근거: 예산은 {D_FADE}+{D_MAX} = {D_FADE+D_MAX:.2f}초에서 포화한다 — 그 위로 유지해도 글자가 안 는다.")
print(f"     하한 근거: 최소 발화(4자 이하)의 필요 체류 {need(4):.2f}초. 1.60초는 그 {1.60/need(4):.2f}배.")

# ── 6. 흔들기(2단계 "옮길 수 있다") ────────────────────────────────────────
L("[6] 2단계 몸짓 — '옮길 수 있다'는 손이 실제로 움직여야 한다")
d_hand_per_deg = (ARM_U*math.cos(math.radians(SHO_P)) + ARM_L*math.cos(math.radians(SHO_P+E_POINT)))*math.pi/180
print(f"  어깨 1도당 손 수평 이동 = {abs(d_hand_per_deg):.5f} 유닛/도 = "
      f"배율0.60에서 {abs(d_hand_per_deg)*0.6*PT_PER_UNIT:.4f} pt/도")
need_deg = VIS_FLOOR_PT/(abs(d_hand_per_deg)*0.6*PT_PER_UNIT)
print(f"  가시 하한 {VIS_FLOOR_PT} pt를 넘기려면 진폭 peak-to-peak ≥ {need_deg:.1f}도 (= ±{need_deg/2:.1f}도)")
for a in (10,12,16,20):
    pp = 2*a*abs(d_hand_per_deg)*0.6*PT_PER_UNIT
    print(f"    ±{a:>3}도 → 손 이동 {pp:5.2f} pt  {'✓' if pp>=VIS_FLOOR_PT else '✗ 안 보인다'}"
          f"   어깨 범위 [{SHO_P-a:.0f},{SHO_P+a:.0f}] (한계 {SHO_FWD_LIM:.0f}) "
          f"{'✓' if SHO_P+a<=SHO_FWD_LIM else '✗'}")
A_W=16.0; T_HOLD=1.60; CYC=2
fw=CYC/T_HOLD
print(f"\n  ★ 채택 ±{A_W:.0f}도 · {CYC}왕복 / {T_HOLD:.2f}초 = {fw:.3f} Hz (정속 보행 케이던스 {f_walk:.3f} Hz와 거의 같다)")
rate=A_W*2*math.pi*fw
print(f"    최대 각속도 {rate:.1f} 도/초 = 60fps {rate/60:.2f} / 15fps {rate/15:.2f} 도/프레임  "
      f"{'✓ 기준선 이하' if rate<=dmax else '✗'}")

# ── 7. 총 박자 ────────────────────────────────────────────────────────────
L("[7] 총 박자")
seq=[("올림(Idle 팔 → 가리키기)",T_RAISE),("유지 A — 대사 1(메뉴 소개)",T_HOLD),
     ("흔들기 — 대사 2(옮길 수 있다)",T_HOLD),("내림(가리키기 → Idle 팔)",T_LOWER)]
tot=0
for nm,t in seq:
    tot+=t; print(f"  {nm:<34}{t:>6.2f}초   누적 {tot:>5.2f}")
print(f"  {'합계':<34}{tot:>6.2f}초")
print(f"  60fps {tot*60:.0f}프레임 / 15fps {tot*15:.0f}프레임")
print(f"\n  ★ 자동 클릭(사용자 요구 '자동으로 한번 클릭')은 **유지 A 시작 시점**에 건다 —")
print(f"    올림이 끝나 손이 톱니를 가리킨 그 프레임. 그래야 '가리켰더니 열렸다'로 읽힌다.")
print(f"    올림 도중에 열면 원인(손)과 결과(메뉴)가 어긋난다.")

# ── 8. 스무딩 시상수 — 절대각/부호 함정 회피 근거 ──────────────────────────
L("[8] 스무딩 시상수 — 같은 그림을 만드는 세 채널이 서로 다른 속도로 돈다")
for nm,r in (("다리 각도(poseSmoothingRate)",poseSmoothing),
             ("팔 위마디 (×ArmSmoothingRatio)",poseSmoothing*ArmSmoothRatio),
             ("팔 아래마디(×LowerSegmentRatio)",poseSmoothing*ArmSmoothRatio*LowerSegRatio),
             ("몸통 기울임(bodyLeanSmoothingRate)",bodyLeanRate)):
    print(f"  {nm:<34} rate {r:6.2f}/s → τ = {1000/r:6.2f} ms")
print(f"\n  ★ 팔 각도 τ={1000/(poseSmoothing*ArmSmoothRatio):.1f}ms 와 몸통 기울임 τ={1000/bodyLeanRate:.1f}ms 는 {1000/bodyLeanRate/(1000/(poseSmoothing*ArmSmoothRatio)):.2f}배 차이다.")
print(f"    어깨 부착점은 기울임(LeanedLocal)이 옮기고 팔 각도는 따로 돈다 — 두 채널이 같은 그림을 만든다.")
print(f"    ⇒ 온보딩 자세는 **기울임 0 고정 + facing 잠금**으로 이 두 채널을 통째로 죽인다(문서 §6-3).")
