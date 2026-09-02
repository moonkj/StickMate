# -*- coding: utf-8 -*-
"""design-motion R6 — 온보딩 ①~⑤ 시연 모션의 기하·박자 검산.

값의 출처는 전부 프로덕션 파일이며 이 스크립트는 산술만 한다.
  Core/StickConfig.cs / Editor/SceneBootstrapper.cs / States/StickmanPoseAnimator.cs /
  States/LandingCrouchState.cs / States/ThrowTumbleState.cs / States/WalkState.cs /
  Dialogue/DialogueKind.cs / Dialogue/DialogueBubbleRenderer.cs / Interaction/InfoGearIconWidget.cs

★ 교정(양성 대조) 4건. 하나라도 깨지면 assert가 멈추고 그 뒤 숫자는 전부 무효다.
"""
import math

def L(t=""):
    print(("-"*84) if not t else f"\n{'='*84}\n{t}\n{'='*84}")

# ============================================================================
# 프로덕션 상수 (grep 확인)
# ============================================================================
ARM_U, ARM_L   = 0.38, 0.37   # SceneBootstrapper.cs:257
LEG_U, LEG_L   = 0.50, 0.45   # SceneBootstrapper.cs:258
HIP_Y          = 0.45         # :819 BaselineSpecHipY
SHO_Y_SPEC     = 1.28         # :819
TORSO_TOP_SPEC = 1.35         # :819
HEAD_R         = 0.22         # :210
IDLE_LEG_SPREAD, IDLE_KNEE = 12.0, 4.0     # :253 / :263
IDLE_ARM_SPREAD, IDLE_ELBOW = 40.0, 10.0   # :248 / :264
SHO_FWD_LIM    = 150.0        # :339 ShoulderSwingForwardLimitDegrees
BASE_H         = 2.2746944    # StickConfig.cs:1816 BaselineCharacterTotalHeight
ORTHO          = 12.0         # SceneBootstrapper.cs:396
GROUND_FRAC    = 0.0764       # SceneBootstrapper.cs:58  groundTop = cam.y - ortho*(1-2f)
CAM_Y          = 0.0          # :1388
SCREEN_H_PT    = 982.0        # NullPlatformWindowService.ReferenceScreenHeightPoints
PT_PER_UNIT    = SCREEN_H_PT / (2*ORTHO)   # DockGeometry.ReferenceWorldUnitsPerPoint
DOCK_STEP      = 1.63747      # StickConfig.cs:1985 문서의 Dock 단차(월드 유닛)

GRAVITY_SCALE  = 3.0          # StickConfig.cs:133
G              = 9.81 * GRAVITY_SCALE

DIAL           = 0.75         # StickConfig.cs:1745 characterScale (첫 실행 = 저장 파일 없음 = 이 값)
SCALE_MIN, SCALE_MAX = 0.35, 1.00   # StickConfig.cs:1784 / :1808

WALK_SPEED     = 2.5          # StickConfig.cs:26
STRIDE_SCALE   = 0.93         # :59  walkStrideScale
AMP_CFG        = 1.0          # :49  walkPoseAmplitudeScale
AMP_REST, AMP_FULL = 0.85, 1.35   # StickmanPoseAnimator.cs:170 / :173
LEG_HIP_KEYS   = [25.,12.,0.,-15.,-25.,-12.,0.,15.]  # :105
LEG_KNEE_KEYS  = [5.,20.,5.,5.,10.,45.,50.,25.]      # :110
ARM_SHO_KEYS   = [18.,0.,-18.,0.]                    # :113

# 착지 램프 (StickConfig.cs:337~375, 2228~2245)
SOFT_START, REACTION = 0.35, 0.88
DEEP_SPAN, BRACE_SPAN = 3.02, 7.10
SOFT_DUR, SHALLOW_DUR, DEEP_DUR, BRACE_DUR = 0.14, 0.32, 0.62, 0.88
HOLD_BASE, HOLD_BRACE = 0.24, 0.40

# 던지기 회전 (StickConfig.cs:2329~2406)
TUMBLE_MIN_SPIN, TUMBLE_MAX_SPIN = 220.0, 720.0
TUMBLE_PER_H, TUMBLE_LEAD = 90.0, 0.10

# 등반 (StickConfig.cs:212~262)
CLIMB_DUR = 1.20
CLIMB_F   = [0.1833, 0.3250, 0.6583, 0.8917, 1.0]

# 박자 하한 (docs/MOTION_SPEC.md 18-1 — 이 저장소가 비준한 값)
LB_ACTION, LB_HOLD, LB_BLENDOUT, LB_TRANSITION = 0.19, 0.17, 0.12, 0.10

# 대사 예산 (Dialogue/DialogueKind.cs)
D_BASE, D_PER, D_MIN, D_MAX = 0.28, 0.075, 0.62, 2.20
D_FADEIN, D_POPIN, D_FADEOUT = 0.06, 0.18, 0.12
D_LATIN = 0.0472     # design-narrative R2 제안 라틴 계수(아직 프로덕션 아님)

VIS_FLOOR_PT = 3.9   # R4 §1-5 가시 하한
GEAR_TIP_R_PT = 13.8 # InfoGearIconWidget.cs:184
GEAR_DRAG_SCALE, GEAR_DRAG_ALPHA, GEAR_DRAG_THRESH = 1.12, 0.55, 4.0  # :256/:258/:252

# ============================================================================
L("[0] 교정 (양성 대조) — 깨지면 이하 전부 폐기")
# ============================================================================
def limb_drop(hip,u,l,knee):
    return u*math.cos(math.radians(hip)) + l*math.cos(math.radians(hip-knee))
footLift = max(limb_drop(-IDLE_LEG_SPREAD,LEG_U,LEG_L,IDLE_KNEE),
               limb_drop( IDLE_LEG_SPREAD,LEG_U,LEG_L,IDLE_KNEE)) - HIP_Y
SHO_Y  = SHO_Y_SPEC + footLift
HEAD_Y = TORSO_TOP_SPEC + footLift + HEAD_R
TOTAL_H = HEAD_Y + HEAD_R
print(f"  C1 전신 유도 = {TOTAL_H:.7f}  vs  BaselineCharacterTotalHeight {BASE_H}   "
      f"{'OK' if abs(TOTAL_H-BASE_H)<1e-4 else 'FAIL'}")
assert abs(TOTAL_H-BASE_H) < 1e-4

# C2 — 보행 케이던스가 R5의 f_walk 1.2328을 재현하는가
def foot_h(hip,knee,u,l):
    return u*math.sin(math.radians(hip)) + l*math.sin(math.radians(hip-knee))
def dist_per_cycle(amp, dial):
    u,l = LEG_U*dial, LEG_L*dial   # 프리팹은 dial로 구워지고 dial=0.75에서 RootScale=1
    c = foot_h(LEG_HIP_KEYS[0]*amp, LEG_KNEE_KEYS[0]*amp, u, l)
    t = foot_h(LEG_HIP_KEYS[4]*amp, LEG_KNEE_KEYS[4]*amp, u, l)
    return abs(c-t)*2.0
DPC   = dist_per_cycle(AMP_FULL, DIAL) * STRIDE_SCALE
SPEED = WALK_SPEED * DIAL
f_walk = SPEED / DPC
print(f"  C2 보행 케이던스 = {SPEED:.4f} / {DPC:.6f} = {f_walk:.4f} Hz  vs  R5 기준선 1.2328   "
      f"{'OK' if abs(f_walk-1.2328)<5e-4 else 'FAIL'}")
assert abs(f_walk-1.2328) < 5e-4

# C3 — 등반 5박자가 1.20초로 합쳐지는가
beats = [CLIMB_F[0]] + [CLIMB_F[i]-CLIMB_F[i-1] for i in range(1,5)]
climb_secs = [b*CLIMB_DUR for b in beats]
print(f"  C3 등반 5박자 = {' + '.join(f'{s:.3f}' for s in climb_secs)} = {sum(climb_secs):.4f}초  "
      f"vs parkourClimbDuration {CLIMB_DUR}   {'OK' if abs(sum(climb_secs)-CLIMB_DUR)<1e-6 else 'FAIL'}")
assert abs(sum(climb_secs)-CLIMB_DUR) < 1e-6

# C4 — 팔 스윙 최대 각속도 기준선이 R5의 159.8을 재현하는가
def catmull(p0,p1,p2,p3,u):
    u2,u3 = u*u, u*u*u
    return 0.5*((2*p1)+(-p0+p2)*u+(2*p0-5*p1+4*p2-p3)*u2+(-p0+3*p1-3*p2+p3)*u3)
def samp(k,ph):
    n=len(k); x=(ph%1.)*n; i=int(math.floor(x))%n; u=x-math.floor(x)
    return catmull(k[(i-1)%n],k[i],k[(i+1)%n],k[(i+2)%n],u)
N=4000
ARM_RATE_CEIL = max(abs(samp(ARM_SHO_KEYS,(i+1)/N)-samp(ARM_SHO_KEYS,i/N)) for i in range(N))*AMP_FULL*N*f_walk
print(f"  C4 팔 스윙 최대 각속도 = {ARM_RATE_CEIL:.1f} 도/초  vs  R5 기준선 159.8   "
      f"{'OK' if abs(ARM_RATE_CEIL-159.8)<0.2 else 'FAIL'}")
assert abs(ARM_RATE_CEIL-159.8) < 0.2

H = BASE_H*DIAL
H_PT = H*PT_PER_UNIT
GROUND_TOP = CAM_Y - ORTHO*(1-2*GROUND_FRAC)
DOCK_TOP   = GROUND_TOP + DOCK_STEP
SCREEN_TOP = CAM_Y + ORTHO
print(f"\n  파생: 신장 H = {H:.5f} 유닛 = {H_PT:.2f} pt   (배율 {DIAL}, 첫 실행이므로 확정값)")
print(f"        1유닛 = {PT_PER_UNIT:.4f} pt,  화면 상단 y = {SCREEN_TOP:+.4f}")
print(f"        물리 바닥 상단 y = {GROUND_TOP:+.4f},  Dock 상단 y = {DOCK_TOP:+.4f}")
print(f"        중력 g = 9.81 x {GRAVITY_SCALE} = {G:.2f} 유닛/초^2")

# ============================================================================
L("[1] (1) INTRO_FALL — 결정론 vs 물리. 무엇이 흔들리고 무엇이 안 흔들리는가")
# ============================================================================
SPAWN_Y = SCREEN_TOP + H     # UX_FLOW 35-2-2 사양: y = 화면top + 신장
print(f"  UX 사양 스폰 y = 화면top + 신장 = {SCREEN_TOP:.3f} + {H:.3f} = {SPAWN_Y:.3f}")
print(f"  (루트는 발 높이다 — 이 값이면 머리끝이 화면 상단과 정확히 같다)")

def tier_and_duration(hH):
    t0 = min(max((hH-SOFT_START)/(REACTION-SOFT_START),0),1)
    t  = min(max((hH-REACTION)/DEEP_SPAN,0),1)
    u  = min(max((hH-(REACTION+DEEP_SPAN))/BRACE_SPAN,0),1)
    soft = hH < REACTION
    dur = (SOFT_DUR+(SHALLOW_DUR-SOFT_DUR)*t0) if soft else (SHALLOW_DUR+(DEEP_DUR-SHALLOW_DUR)*t + u*(BRACE_DUR-DEEP_DUR))
    hold = HOLD_BASE + (HOLD_BRACE-HOLD_BASE)*u
    if hH >= REACTION+DEEP_SPAN: tier="Brace"
    elif hH <  REACTION:          tier="SoftAbsorb"
    elif hH <  REACTION+DEEP_SPAN*0.5: tier="ShallowCrouch"
    else: tier="DeepCrouch"
    return tier,dur,hold,u

print(f"\n  착지 대상별 (스폰 고정, 착지 y만 다름)")
print(f"  {'착지 대상':<22}{'착지 y':>9}{'낙차(유닛)':>11}{'낙차(H)':>9}{'비행(초)':>9}"
      f"{'착지속도':>9}{'티어':>14}{'착지연출':>9}{'(1)총':>8}")
targets = [("합성 바닥 발판", GROUND_TOP), ("Dock/작업표시줄 상단", DOCK_TOP),
           ("낮은 창 상단 y=-4", -4.0), ("중간 창 상단 y=0", 0.0),
           ("높은 창 상단 y=+6", 6.0), ("최상단 창 y=+11", 11.0)]
rows=[]
for nm,y in targets:
    d = SPAWN_Y - y
    hH = d/H
    tf = math.sqrt(2*d/G)
    v  = G*tf
    tier,dur,hold,u = tier_and_duration(hH)
    rows.append((nm,y,d,hH,tf,v,tier,dur,tf+dur,u))
    print(f"  {nm:<22}{y:>9.2f}{d:>11.3f}{hH:>9.2f}{tf:>9.3f}{v:>9.2f}{tier:>14}{dur:>9.3f}{tf+dur:>8.3f}")
lo = min(r[8] for r in rows); hi = max(r[8] for r in rows)
print(f"\n  => (1)의 총 지속 스프레드 = {lo:.3f} ~ {hi:.3f}초 (폭 {hi-lo:.3f}초). "
      f"고정된 것은 스폰 하나뿐이고 나머지는 전부 착지 대상이 정한다.")

L()
print("  [1-a] 판독 가능성 — 프레임당 이동을 신장으로 잰다")
print("        기준: 연속 두 프레임의 실루엣이 겹쳐야 대응(correspondence)이 성립한다.")
print("        · 겹침 하한(기하): 프레임당 이동 <= 1.0 H     · 권장(관례): <= 0.5 H")
print(f"  {'착지 대상':<22}{'착지속도':>9}{'60fps':>9}{'30fps':>9}{'15fps':>9}   판정(H/프레임)")
for nm,y,d,hH,tf,v,tier,dur,tot,u in rows:
    a,b,c = v/60/H, v/30/H, v/15/H
    verdict = f"60:{'OK' if a<=0.5 else ('경계' if a<=1 else 'X')} " \
              f"30:{'OK' if b<=0.5 else ('경계' if b<=1 else 'X')} " \
              f"15:{'OK' if c<=0.5 else ('경계' if c<=1 else 'X')}"
    print(f"  {nm:<22}{v:>9.2f}{a:>9.3f}{b:>9.3f}{c:>9.3f}   {verdict}")
v_ok = {fps: 0.5*H*fps for fps in (60,30,15)}
print(f"\n  0.5 H/프레임을 넘지 않는 최대 낙하 속도: "
      + " / ".join(f"{fps}fps {v_ok[fps]:.1f} 유닛/초" for fps in (60,30,15)))
for fps in (60,30,15):
    hmax = v_ok[fps]**2/(2*G)
    print(f"    {fps}fps -> 낙차 <= {hmax:.3f} 유닛 = {hmax/H:.2f} H")
print("  ★ 15fps(Still 등급)에서는 어떤 화면종단 낙하도 겹치지 않는다 = 순간이동으로 읽힌다.")

L()
print("  [1-b] 회전(ThrowTumble)이 성립하는가 — TryPlanRotation을 그대로 푼다")
print("        turns=1이 필요로 하는 각속도 360/(t-lead) <= throwTumbleMaxSpinDegreesPerSecond(720)")
t_need = 360.0/TUMBLE_MAX_SPIN + TUMBLE_LEAD
h_need = 0.5*G*t_need**2
print(f"        -> 비행 t >= 360/{TUMBLE_MAX_SPIN:.0f} + {TUMBLE_LEAD} = {t_need:.4f}초")
print(f"        -> 낙차 h >= 0.5 x {G:.2f} x {t_need:.4f}^2 = {h_need:.4f} 유닛 = {h_need/H:.3f} H (배율 {DIAL})")
print(f"  {'착지 대상':<22}{'비행(초)':>9}{'계획 바퀴':>11}{'계획 각속도':>13}   판정")
for nm,y,d,hH,tf,v,tier,dur,tot,u in rows:
    usable = tf - TUMBLE_LEAD
    if usable <= 0:
        print(f"  {nm:<22}{tf:>9.3f}{'-':>11}{'-':>13}   X 회전 불가 -> Fall"); continue
    ideal = TUMBLE_MIN_SPIN*usable
    turns = max(1, round((ideal-360.0)/360.0)+1)
    delta = 360.0 + 360.0*(turns-1)
    while turns > 1 and delta/usable > TUMBLE_MAX_SPIN:
        turns -= 1; delta = 360.0+360.0*(turns-1)
    ok = delta/usable <= TUMBLE_MAX_SPIN
    print(f"  {nm:<22}{tf:>9.3f}{turns:>11}{delta/usable:>13.1f}   "
          f"{'OK 정확히 직립 착지' if ok else 'X 계획 실패 -> Fall(회전 없음)'}")
print(f"\n  ★ 낙차가 {h_need/H:.2f} H 미만이면 회전이 계획되지 않고 평범한 Fall로 빠진다 —")
print(f"    같은 (1)인데 화면의 그림이 둘로 갈린다(회전 낙하 vs 직립 낙하).")

L()
print("  [1-c] 배율 의존성 — 온보딩에서만 사라진다")
print(f"  {'배율':>6}{'신장(유닛)':>11}{'회전 성립 낙차(H)':>18}{'화면종단 낙차(H)':>17}   회전?")
for s in (0.35,0.50,DIAL,1.00):
    h_s = BASE_H*s
    d_full = (SCREEN_TOP+h_s) - DOCK_TOP
    print(f"{s:>6.2f}{h_s:>11.4f}{h_need/h_s:>18.2f}{d_full/h_s:>17.2f}   "
          f"{'성립' if d_full>=h_need else '불가'}")
print(f"  ★ 이 표는 온보딩에서 **읽을 필요가 없다** — 첫 실행은 저장 파일이 없어")
print(f"    characterScale이 코드 기본값 {DIAL}로 확정된다(StickConfig.cs:1745). 배율은 (6)에서 처음 바뀐다.")

# ============================================================================
L("[2] 대사 예산 — 마진 모델 3종을 갈라 둔다 (design-narrative 지적 수용 + 물리값 병기)")
# ============================================================================
def R(n, per=D_PER): return min(max(D_BASE+per*n, D_MIN), D_MAX)
def maxchars(T, per=D_PER, margin=0.0, fade=D_FADEIN, cap=999):
    """슬롯 T에 들어가는 최대 글자수. 가독예산이 D_MAX에서 포화하면 무한히 늘 수 있으므로
    포화 지점(cap)에서 자른다 — 그 위는 '글자수 제한 없음'이다."""
    if R(0,per)+fade+margin > T + 1e-9: return -1
    n=0
    while n < cap and R(n+1,per)+fade+margin <= T + 1e-9: n+=1
    return n
print("  모델 A (내 R5) : 슬롯 >= FadeIn 0.06 + R")
print("  모델 B (채택)  : 슬롯 >= FadeIn 0.06 + R + 마진 0.18   <- design-narrative / 비준 기준")
print("  모델 C (물리)  : 슬롯 >= max(FadeIn 0.06, PopIn 0.18) + R")
print("     ★ 물리적으로 페이드인과 팝인은 **동시에** 시작한다 —")
print("       DialogueBubbleRenderer.ShowInternal이 같은 호출에서 _alpha=0과 _popElapsed=0을 세팅하고")
print("       LateUpdate가 같은 dt로 둘 다 진행시킨다(:1055-1065, :1158, :1196). 순차가 아니다.")
print("       그래서 등장 연출이 끝나는 시각은 0.06+0.18=0.24가 아니라 max=0.18초다.")
print("       모델 B가 그보다 0.06초 더 보수적인 것은 **안전 마진**이고, 그 판단을 그대로 채택한다.")
print(f"\n  {'슬롯(초)':>9}{'A 한글':>8}{'A 라틴':>8}{'B 한글':>8}{'B 라틴':>8}{'C 한글':>8}{'C 라틴':>8}")
for T in (0.65,0.80,0.86,1.00,1.20,1.40,1.60,1.80,2.00,2.44,2.60):
    a1,a2 = maxchars(T), maxchars(T,D_LATIN)
    b1,b2 = maxchars(T,margin=D_POPIN), maxchars(T,D_LATIN,margin=D_POPIN)
    c1,c2 = maxchars(T,fade=max(D_FADEIN,D_POPIN)), maxchars(T,D_LATIN,fade=max(D_FADEIN,D_POPIN))
    f=lambda x: "불가" if x<0 else ("무제한" if x>=999 else str(x))
    print(f"{T:>9.2f}{f(a1):>8}{f(a2):>8}{f(b1):>8}{f(b2):>8}{f(c1):>8}{f(c2):>8}")
minslot_B = D_FADEIN + D_MIN + D_POPIN
minslot_C = max(D_FADEIN,D_POPIN) + D_MIN
print(f"\n  최소 발화 가능 슬롯:  모델 B {minslot_B:.3f}초   /  모델 C(물리 하한) {minslot_C:.3f}초")
print(f"  포화 슬롯(그 위로는 글자가 안 는다): 모델 B {D_FADEIN+D_MAX+D_POPIN:.2f}초 / 모델 C {max(D_FADEIN,D_POPIN)+D_MAX:.2f}초")
print(f"\n  ★ 자기정정 C1: R5 §6-7의 '1.60초 -> 한글 16 / 라틴 26'은 모델 A다. **모델 B로 대체한다: 14 / 22.**")
print(f"     그래도 1.60초라는 박자 자체는 살아남는다 — 채택 문안 `이렇게 옮겨도 돼`(9자)의 여유가")
print(f"     {1.60 - D_FADEIN - R(9) - D_POPIN:+.3f}초다.")
print(f"  ★ 자기정정 C2: R5 §6-5의 올림 0.80초는 모델 B 최소 슬롯 {minslot_B:.3f}초에 **미달**한다.")
print(f"     (물리 하한 {minslot_C:.2f}초와는 정확히 같다 — 경계다.) 어느 모델이든 여기에 대사를 얹지 않는다.")

# ============================================================================
L("[3] (2) INTRO_GREET — 2.60초가 맞는가 + 트리거 시점")
# ============================================================================
print(f"  UX 사양 = TimedSpectacleState(2.60초). 가독예산 R이 MaxSeconds {D_MAX}에서 포화하므로")
print(f"  포화 슬롯({D_FADEIN+D_MAX+D_POPIN:.2f}초) 위에서는 **슬롯이 글자수를 더 이상 제한하지 않는다.**")
print(f"  포화는 {D_FADEIN+D_MAX+D_POPIN:.2f}초이므로 2.60초는 이미 **포화 위**다 —")
print(f"    2.44초로 줄여도 담을 수 있는 글자수가 1자도 줄지 않는다(0.16초는 순수 정지 화면).")
print(f"    2.44초 / 2.60초 -> 둘 다 무제한(슬롯 기준). 실제 상한은 노출상한")
print(f"    MaxVisibleSecondsFor = PopIn {D_POPIN} + 2R + FadeOut {D_FADEOUT}로 넘어간다.")
print(f"\n  ★ 그런데 (2)는 순수 정지가 아니어야 한다 — TimedSpectacleState는 포즈를 전혀 만들지 않는다.")
print(f"    (그 클래스는 GroundedTick만 부르고 자세는 Idle 분기가 그린다.)")
print(f"    2.60초 동안 서 있기만 하면 '멈췄다'로 읽힌다. Idle 호흡만으로는 2.60초를 못 버틴다:")
print(f"    idleAmbientLookAroundSeconds = 0.90초짜리 앰비언트 1회를 여기에 배치할 여지가 있다(권고).")

# ============================================================================
L("[4] (3) DEMO_WALK — 보폭에서 시간을 유도한다 (거리가 먼저, 걸음수는 결과)")
# ============================================================================
step = DPC/2
print(f"  한 사이클 이동 = {DPC:.5f} 유닛 (= {DPC/H:.4f} H = {DPC*PT_PER_UNIT:.2f} pt)")
print(f"  한 걸음        = {step:.5f} 유닛 (= {step/H:.4f} H = {step*PT_PER_UNIT:.2f} pt)")
print(f"  케이던스       = {f_walk:.4f} Hz  ->  걸음당 {1/(2*f_walk):.4f}초")
print(f"\n  {'걸음수':>7}{'거리(유닛)':>12}{'거리(H)':>10}{'거리(pt)':>11}{'시간(초)':>10}   판정")
for n in (2,3,4,5,6,8):
    d = n*step; t = n/(2*f_walk)
    print(f"{n:>7}{d:>12.4f}{d/H:>10.3f}{d*PT_PER_UNIT:>11.1f}{t:>10.4f}   "
          f"{'UX 상한 3.5초 초과' if t>3.5 else ''}")
print(f"\n  UX 사양 '3~4보' = {3/(2*f_walk):.3f} ~ {4/(2*f_walk):.3f}초, {3*step*PT_PER_UNIT:.0f} ~ {4*step*PT_PER_UNIT:.0f} pt")
print(f"  UX 상한 3.5초에 들어가는 최대 걸음수 = {math.floor(3.5*2*f_walk)}보 ({math.floor(3.5*2*f_walk)*step*PT_PER_UNIT:.0f} pt)")
print(f"\n  ★ 기동 전이(R5 §3) 0.22초가 앞에 붙는다. 그 구간은 진폭이 램프되므로 보폭이 줄고 케이던스가 오른다:")
for openv,label in ((0.35556,"진입(open0, 유도값)"),(0.6,""),(1.0,"완전 개방")):
    a = AMP_FULL*openv if openv<1 else AMP_FULL
    dpc_o = dist_per_cycle(a, DIAL)*STRIDE_SCALE
    print(f"    open={openv:.4f} {label:<20} 보폭 {dpc_o/2*PT_PER_UNIT:6.2f} pt, 케이던스 {SPEED/dpc_o:5.3f} Hz")
print(f"  => '걸음수'로 사양을 적으면 기동 전이 때문에 어긋난다. **거리로 적고 시간을 유도한다.**")

# ============================================================================
L("[5] (4) DEMO_CLIMB — 이미 확정된 사양의 승계 + 대사 재검산")
# ============================================================================
names = ["뻗기(동작)","매달림(정지)","당기기(동작)","맨틀(동작)","정착(블렌드아웃)"]
lbs   = [LB_ACTION, LB_HOLD, LB_ACTION, LB_ACTION, LB_BLENDOUT]
print(f"  {'박자':<18}{'초':>8}{'하한':>7}{'여유':>9}")
for nm,s,lb in zip(names,climb_secs,lbs):
    print(f"  {nm:<18}{s:>8.3f}{lb:>7.2f}{s-lb:>+9.3f}")
print(f"  {'합계':<18}{sum(climb_secs):>8.3f}")
print(f"\n  등반 대사(ParkourClimbState 3분기)의 모델 B 상한 = 한글 {maxchars(CLIMB_DUR,margin=D_POPIN)}자 / "
      f"라틴 {maxchars(CLIMB_DUR,D_LATIN,margin=D_POPIN)}자")
for txt in ("가뿐하네","영차...","헉... 높다"):
    need = D_FADEIN + R(len(txt)) + D_POPIN
    print(f"    \"{txt}\" {len(txt)}자 -> 필요 {need:.3f}초, 여유 {CLIMB_DUR-need:+.3f}초  "
          f"{'OK' if need<=CLIMB_DUR else 'X 잘린다'}")
print(f"\n  ★ 등반 진행도 1에서 포즈가 정확히 Idle 중립이다(ApplyParkourClimbPose의 settleW).")
print(f"    (4)->(5) 이음매는 **이미 구조적으로 0**이다 — 내가 새로 할 일이 없다.")

# ============================================================================
L("[6] (5) DEMO_PASSTHRU — 무엇을 시연할 수 있는가를 먼저 잰다")
# ============================================================================
print("  사실 확인(코드): 이 앱의 클릭 관통은 **캐릭터 실루엣 위에서 해제된다.**")
print("    MacWindowService.cs:42  \"창 전체는 관통하되 캐릭터가 그려진 [영역은 앱이 받는다]\"")
print("    ILocalClickCaptureService.cs:21  hitTestType=Raycast(커서 아래 Collider2D 유무)")
print("    StickmanClickHitbox.cs:40  \"커서 아래에 우리 Collider2D가 있을 때만 클릭관통을 풀고\"")
print("  => UX 초안 대사 \"네 클릭은 나를 통과해\"는 **거짓**이다. 통과하지 않는다 — 그래서 드래그가 된다.")

print("\n  [6-a] 커서가 캐릭터와 겹칠 확률 — 자동 시연이 성립하는가")
band_pt   = H_PT                                   # 세로: 발끝~머리끝
path_pt   = 4*step*PT_PER_UNIT                     # 가로: (3)의 보행 경로 4보
screen_w_pt = SCREEN_H_PT * (1512/982)             # 16:10 가정(참고용)
p_y = band_pt/SCREEN_H_PT
p_x = path_pt/screen_w_pt
print(f"    세로 밴드 {band_pt:.1f} pt / 화면 {SCREEN_H_PT:.0f} pt = {p_y*100:.1f}%")
print(f"    가로 경로 {path_pt:.1f} pt / 화면 {screen_w_pt:.0f} pt = {p_x*100:.1f}%")
print(f"    균등 분포 가정 시 동시 성립 = {p_y*p_x*100:.2f}%   -> **자동 시연으로는 성립하지 않는다**")
print(f"    (커서 위치는 우리가 못 정한다. 원칙 3 = 유저 자산 불변.)")

print("\n  [6-b] 겹침이 일어났을 때 그것을 읽을 시간은 있는가")
torso_w_pt = abs(foot_h(LEG_HIP_KEYS[0]*AMP_FULL,LEG_KNEE_KEYS[0]*AMP_FULL,LEG_U*DIAL,LEG_L*DIAL)
               - foot_h(LEG_HIP_KEYS[4]*AMP_FULL,LEG_KNEE_KEYS[4]*AMP_FULL,LEG_U*DIAL,LEG_L*DIAL))*PT_PER_UNIT
head_w_pt = 2*HEAD_R*DIAL*PT_PER_UNIT
walk_pt_s = SPEED*PT_PER_UNIT
for nm,w in (("다리 벌림 폭",torso_w_pt),("머리 지름",head_w_pt),("몸통 선(굵기)",2.0)):
    t_ov = w/walk_pt_s
    print(f"    {nm:<14} {w:6.2f} pt -> 겹침 지속 {t_ov:.3f}초  "
          f"{'OK 정지하한 0.17 위' if t_ov>=LB_HOLD else 'X 하한 미달'}  (보행 {walk_pt_s:.1f} pt/초)")
print("    => 겹침을 '다리 밴드'에서 판정하면 읽을 시간은 충분하다. 머리 밴드는 경계다.")

print("\n  [6-c] '아무 일도 안 일어남'을 무엇이 증명하는가")
print("    정지한 캐릭터 옆을 커서가 지나가면 '무반응'과 '화면 무변화'가 화면상 구별되지 않는다.")
print("    막히지 않았음을 증명할 수 있는 것은 **끊기지 않은 주기 운동**뿐이다.")
print(f"    보행 케이던스 {f_walk:.4f} Hz -> 한 걸음 {1/(2*f_walk):.4f}초.")
print(f"    겹침 구간({torso_w_pt/walk_pt_s:.3f}초)은 걸음의 {torso_w_pt/walk_pt_s*2*f_walk:.2f}보에 해당한다 —")
print(f"    그 구간에서 위상이 흐트러지지 않는 것이 곧 '막히지 않았다'의 물리적 증거다.")
print(f"    ★ 즉 (5)는 **정지 자세로는 만들 수 없다. 걸어야 한다.**")

# ============================================================================
L("[7] 리더 질문 — '흔들기'의 대상은 손인가 톱니인가")
# ============================================================================
E_POINT=12.0
def hand(sho,elb):
    return (ARM_U*math.sin(math.radians(sho))+ARM_L*math.sin(math.radians(sho+elb)),
            -(ARM_U*math.cos(math.radians(sho))+ARM_L*math.cos(math.radians(sho+elb))))
AIM=134.0; SHO_P=AIM-E_POINT
d_hand_per_deg = abs(ARM_U*math.cos(math.radians(SHO_P))+ARM_L*math.cos(math.radians(SHO_P+E_POINT)))*math.pi/180
print(f"  (가) 손만 흔든다 — R5 §6-5의 ±16도 / 2왕복 / 1.60초")
for s in (SCALE_MIN, DIAL, SCALE_MAX):
    pp = 2*16*d_hand_per_deg*s*PT_PER_UNIT
    print(f"      배율 {s:.2f}: 손끝 peak-to-peak {pp:5.2f} pt  "
          f"{'OK' if pp>=VIS_FLOOR_PT else 'X 가시 하한 미달'} (하한 {VIS_FLOOR_PT} pt)")
fw = 2/1.60
print(f"      주파수 {fw:.3f} Hz, 최대 각속도 {16*2*math.pi*fw:.1f} 도/초 (기준선 {ARM_RATE_CEIL:.1f}) "
      f"{'OK' if 16*2*math.pi*fw<=ARM_RATE_CEIL else 'X'}")
print(f"      ★ 그러나 이 몸짓이 무엇으로 읽히는가: 닿지 않는 손의 좌우 왕복 {fw:.2f} Hz / ±16도는")
print(f"        '옮긴다'가 아니라 **인사(waving)**의 전형이다. 실제 '옮기기'는 잡고 끄는 몸짓인데")
print(f"        R5 §6-2에서 **닿지 않는다**를 이미 확정했으므로 그 몸짓은 만들 수 없다.")
print(f"\n  (나) 톱니가 흔들린다 — 손은 가리킨 채로, aim이 톱니를 추종한다")
print(f"      톱니 지름 = {2*GEAR_TIP_R_PT:.1f} pt (배율 불변, InfoGearIconWidget.cs:184는 pt 상수다)")
print(f"      {'배율':>6}{'신장(pt)':>10}{'톱니지름/신장':>14}   보이는 크기")
for s in (SCALE_MIN, DIAL, SCALE_MAX):
    hp = BASE_H*s*PT_PER_UNIT
    print(f"{s:>6.2f}{hp:>10.2f}{2*GEAR_TIP_R_PT/hp:>14.3f}   "
          f"{'톱니가 캐릭터만 하다' if 2*GEAR_TIP_R_PT/hp>0.7 else ''}")
print(f"      => 흔들리는 물체의 크기: 톱니 {2*GEAR_TIP_R_PT:.1f} pt vs 손끝(선 굵기) 약 2 pt.")
print(f"         가시성에서 비교가 되지 않는다. 그리고 톱니는 **배율에 불변**이다.")
print(f"\n      톱니를 진폭 A pt로 흔들면 팔의 aim이 몇 도 도는가 — dθ ≈ A / L (L = 어깨→톱니 거리)")
print(f"      어깨 높이(발밑 기준) = {SHO_Y*DIAL*PT_PER_UNIT:.1f} pt (배율 {DIAL}). L은 ux-designer가 정한다.")
print(f"      {'L(pt)':>8}" + "".join(f"{('A='+str(int(a))+'pt'):>13}" for a in (4,8,12,16)))
for Lpt in (40,60,80,100):
    row=f"{Lpt:>8}"
    for A in (4,8,12,16):
        dth=math.degrees(math.atan2(A,Lpt)); rate=dth*2*math.pi*fw
        row+=f"{dth:>6.2f}/{rate:>5.0f}"
    print(row+"   (도/ 도/초)")
print(f"      기준선 {ARM_RATE_CEIL:.1f} 도/초를 넘지 않는 조건: atan(A/L) x 2pi x {fw:.2f} <= {ARM_RATE_CEIL:.1f}")
print(f"      -> A/L <= tan({ARM_RATE_CEIL/(2*math.pi*fw):.2f}도) = {math.tan(math.radians(ARM_RATE_CEIL/(2*math.pi*fw))):.3f}")
print(f"      드래그 임계 A={GEAR_DRAG_THRESH:.0f} pt에서 필요한 최소 거리 L >= {GEAR_DRAG_THRESH/math.tan(math.radians(ARM_RATE_CEIL/(2*math.pi*fw))):.1f} pt")
print(f"      => 톱니를 드래그 임계({GEAR_DRAG_THRESH:.0f} pt, InfoGearIconWidget.cs:252) 이상 옮기면")
print(f"         팔은 **저절로** 그만큼 따라간다. R5의 aim은 이미 매 프레임 유도값이라 새 상수가 0이다.")
print(f"\n      ★ 결정타: 톱니에는 이미 **자기 드래그 시각 상태**가 있다 —")
print(f"        DragScale {GEAR_DRAG_SCALE} / DragAlpha {GEAR_DRAG_ALPHA} (InfoGearIconWidget.cs:256,258).")
print(f"        '옮길 수 있다'를 그 상태 그대로 보여주면 시연과 실제가 **같은 그림**이 된다.")
print(f"        새 연출을 발명하지 않고 이미 있는 상태를 보여주는 것 — 원칙 1의 형태 그대로다.")

# ============================================================================
L("[8] R5 자기정정 — 배율 상한을 2.00으로 잘못 잡았다")
# ============================================================================
print(f"  StickConfig.MaxCharacterScale = {SCALE_MAX} (StickConfig.cs:1808). R5 §6-2 표는 2.00까지 갔다.")
hx,hy = hand(SHO_FWD_LIM,12); hy += SHO_Y
print(f"  {'배율':>6}{'손끝x(pt)':>11}{'손끝y-머리끝(pt)':>18}{'닿는 최대 d(pt)':>17}"
      f"{'그때 톱니 아래끝':>17}   근거1  근거2")
cover=[]
for s in (0.35,0.50,0.75,1.00):
    fx = hx*s*PT_PER_UNIT; fy = (hy-TOTAL_H)*s*PT_PER_UNIT
    disc = GEAR_TIP_R_PT**2 - fx**2
    dmax = (fy+math.sqrt(disc)) if disc>0 else float('nan')
    bottom = dmax-GEAR_TIP_R_PT if disc>0 else float('nan')
    g1 = "성립" if math.isnan(dmax) else "깨짐"           # '닿을 수 없다'
    g2 = "성립" if (not math.isnan(bottom) and bottom<0) else "깨짐"  # '톱니가 머리를 덮는다'
    cover.append((s,g1,g2))
    print(f"{s:>6.2f}{fx:>11.2f}{fy:>18.2f}{dmax:>17.2f}{bottom:>17.2f}   {g1:>4}  {g2:>4}")
print(f"\n  ★ 실제 선택 가능 구간 [{SCALE_MIN}, {SCALE_MAX}]에서 근거 1(닿을 수 없다)은 **전부 깨진다.**")
print(f"    근거 2(톱니가 머리를 덮는다)는 배율 1.00에서만 성립하고 0.35~0.75에서 깨진다.")
print(f"    => 두 근거의 합집합도 전 구간을 못 덮는다. **전 구간에서 성립하는 것은 근거 3뿐이다**")
print(f"       (\"자동으로 한번 클릭\" — 누르는 주체는 앱이지 손이 아니다. 배율과 무관하다).")
print(f"    결론(닿지 않는다, 가리킨다)은 유지하되 **근거를 근거 3 단독으로 좁힌다.**")

# ============================================================================
L("[9] 총 박자 — 락(SpectacleEventLock) 보유 시간")
# ============================================================================
seq = [("(1) INTRO_FALL  낙하",  rows[1][4], rows[0][4]),
       ("(1) INTRO_FALL  착지",  rows[1][7], rows[0][7]),
       ("(2) INTRO_GREET",        2.44, 2.60),
       ("(3) DEMO_WALK   기동",   0.22, 0.22),
       ("(3) DEMO_WALK   보행",   4/(2*f_walk), 6/(2*f_walk)),
       ("(4) DEMO_CLIMB",         CLIMB_DUR, CLIMB_DUR),
       ("(5) DEMO_PASSTHRU",      0.0, torso_w_pt/walk_pt_s)]
tl=th=0.0
print(f"  {'단계':<24}{'하한(초)':>10}{'상한(초)':>10}")
for nm,a,b in seq:
    tl+=a; th+=b
    print(f"  {nm:<24}{a:>10.3f}{b:>10.3f}")
print(f"  {'합계':<24}{tl:>10.3f}{th:>10.3f}")
print(f"\n  UX 목표 '60초 안에'({'OK' if th<60 else 'X'}) — (6) 카드 이전까지 {tl:.1f}~{th:.1f}초.")
print(f"  락 보유 = 이 전 구간. (6)에서 놓는다(game-architect 판정).")
print(f"  ★ 상한이 스프레드로 나오는 이유는 (1)의 착지 대상이 실행마다 다르기 때문이다 —")
print(f"    그래서 (2)의 트리거를 **시각이 아니라 사건**(LandingCrouch 정상 종료)으로 잡아야 한다.")
