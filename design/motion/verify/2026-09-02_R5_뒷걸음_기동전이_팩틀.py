# -*- coding: utf-8 -*-
"""design-motion R5 검산 — 뒷걸음 / 기동전이 / 착탄게이트 / 팩 누적위험.

★ 교정 규칙(TEAM.md): 계산기는 알려진 값으로 먼저 교정한다. 교정이 깨지면 그 뒤 숫자를 전부 폐기.
   여기서는 R4가 낸 값과 design-systems가 낸 값 **양쪽**으로 교정한다(서로 다른 팀, 다른 스크립트).
"""
import math

# ══════════════════════════════════════════════════════════════════════════════
# 0. 프로덕션에서 grep으로 읽은 값 (베끼지 않고 파일/행을 병기)
# ══════════════════════════════════════════════════════════════════════════════
walkSpeed            = 2.5      # StickConfig.cs:26
walkPoseAmpScale     = 1.0      # StickConfig.cs:49
walkStrideScale      = 0.93     # StickConfig.cs:59
poseSmoothingRate    = 35.0     # StickConfig.cs:80
walkSpeedSmoothRate  = 6.0      # StickConfig.cs:85
BASE_H               = 2.2746944  # StickConfig.cs:1816 BaselineCharacterTotalHeight
scale                = 0.60     # 실기 로그 유도(R4 검산과 동일 전제)
PT_PER_UNIT          = 1004.9 / 24.5609   # Dock 실측 환산 (design-motion 사거리 문서)

LEG_U, LEG_L = 0.50, 0.45       # SceneBootstrapper.cs:258 Baseline{Leg}Upper/LowerLength
ARM_U, ARM_L = 0.38, 0.37       # SceneBootstrapper.cs:257
HIP_Y, SHO_Y = 0.45, 1.28       # SceneBootstrapper.cs:819 BaselineSpecHipY / ShoulderY
HIP_LIMIT    = 65.0             # SceneBootstrapper.cs:305 HipSwingLimitDegrees
JOINT_MAX    = 100.0            # SceneBootstrapper.cs:294 MaxJointBendDegrees
SHO_FWD_LIM  = 150.0            # SceneBootstrapper.cs:339
SHO_BACK_LIM = 60.0             # SceneBootstrapper.cs:338
IDLE_SPREAD  = 12.0             # SceneBootstrapper.cs:253 / StickConfig idleLegSpreadDegrees

AMP_REST, AMP_FULL = 0.85, 1.35 # StickmanPoseAnimator.cs:171,173

# StickmanPoseAnimator.cs:105/110/113/116 — 전진 키표
FwdHip  = [ 25.0, 12.0,  0.0, -15.0, -25.0, -12.0,  0.0, 15.0]
FwdKnee = [  5.0, 20.0,  5.0,   5.0,  10.0,  45.0, 50.0, 25.0]
FwdSho  = [ 18.0,  0.0,-18.0,   0.0]
FwdElb  = [ 15.0, 20.0, 25.0,  20.0]

# ★ R5 제안 — 뒷걸음 키표 (§1)
BackHip  = [-16.0, -8.0,  0.0,   8.0,  16.0,   6.0, -8.0, -15.0]
BackKnee = [ 22.0, 16.0, 12.0,  10.0,  14.0,  34.0, 26.0,  10.0]
BackSho  = [ 18.0, 10.0,  2.0,  10.0]     # = R4의 {8,0,-8,0} + 상수 10 을 표에 흡수
BackElb  = [ 24.0, 26.0, 28.0,  26.0]

# 활쏘기 (StickConfig.cs:2582~2628, ArcheryState.cs:333, ArcheryDirector.cs:629)
A = dict(intro=0.55, draw=0.42, aim=0.30, recover=0.34, outro=0.55,
         flight=0.62, flightMax=1.25, refRatio=4.6)
ARRIVE_TOL   = 0.12   # ArcheryState.ArriveToleranceRatio
BACKSTEP_NOW = 1.00   # ArcheryDirector.BackStepRatio
ARROW_SHAFT_RATIO = 0.34  # ArcheryRenderer.cs:60

H_world = BASE_H * scale
H_pt    = H_world * PT_PER_UNIT
V_units = walkSpeed * scale          # ResolveWalkSpeed()
V_H     = walkSpeed / BASE_H         # H/s — 배율 불변

def catmull(p0,p1,p2,p3,u):
    u2=u*u; u3=u2*u
    return 0.5*((2*p1) + (-p0+p2)*u + (2*p0-5*p1+4*p2-p3)*u2 + (-p0+3*p1-3*p2+p3)*u3)

def sample(keys, phase):
    n=len(keys); x=(phase%1.0)*n; i1=int(math.floor(x))%n; u=x-math.floor(x)
    return catmull(keys[(i1-1)%n],keys[i1],keys[(i1+1)%n],keys[(i1+2)%n],u)

def foot_x(hip,knee):   # FootHorizontalOffset (StickmanPoseAnimator.cs:491)
    return LEG_U*math.sin(math.radians(hip)) + LEG_L*math.sin(math.radians(hip-knee))
def foot_drop(hip,knee): # ComputeFootGroundingOffset (…:1868)
    return LEG_U*math.cos(math.radians(hip)) + LEG_L*math.cos(math.radians(hip-knee))
def hand_x(sho,elb):    # 팔꿈치는 ElbowBendSign=+1 → 전완 절대각 = 어깨 + 팔꿈치
    return ARM_U*math.sin(math.radians(sho)) + ARM_L*math.sin(math.radians(sho+elb))

def dist_per_cycle(hip,knee,amp):   # ComputeDistancePerCycle (…:474)
    c = foot_x(hip[0]*amp, max(0.0,knee[0])*amp)
    t = foot_x(hip[4]*amp, max(0.0,knee[4])*amp)
    return abs(c-t)*2.0

def line(t=""): print(("─"*78) if not t else f"\n{'='*78}\n{t}\n{'='*78}")

# ══════════════════════════════════════════════════════════════════════════════
line("[교정] 알려진 값 재현 — 깨지면 아래 숫자 전부 폐기")
# ══════════════════════════════════════════════════════════════════════════════
cal=[]
v_H = walkSpeed/BASE_H
cal.append(("design-systems 보행속도 1.0990 H/s", v_H, 1.0990, 0.0005))
cal.append(("design-systems 실보행 0.88H(후퇴1.0−허용0.12)", BACKSTEP_NOW-ARRIVE_TOL, 0.88, 1e-9))
cal.append(("design-systems 접근 0.801초", (BACKSTEP_NOW-ARRIVE_TOL)/v_H, 0.801, 0.002))
cal.append(("design-systems 18.70H에서 비행=상한 1.25초",
            A['flight']*math.sqrt(18.70/A['refRatio']), 1.250, 0.002))
cal.append(("design-systems 13.40H 비행 1.058초",
            A['flight']*math.sqrt(13.40/A['refRatio']), 1.058, 0.002))
cal.append(("R4 Walk 첫틱 다리 ±33.75도", FwdHip[0]*AMP_FULL, 33.75, 1e-9))
cal.append(("R4 walkStartOpenAtEntry 0.356", IDLE_SPREAD/(FwdHip[0]*AMP_FULL), 0.356, 0.0005))
cal.append(("R4 신장 1.36유닛", H_world, 1.3648, 0.002))
ok=True
for name,got,exp,tol in cal:
    good = abs(got-exp)<=tol
    ok = ok and good
    print(f"  [{'OK ' if good else 'FAIL'}] {name:<44s} 계산={got:.4f}  기대={exp}")
print(f"\n  교정 결과: {'통과 — 이하 숫자 유효' if ok else '★실패 — 이하 전부 폐기'}")
assert ok

# ══════════════════════════════════════════════════════════════════════════════
line("[1] 뒷걸음 키표 — 부호규약 / 관절한계 / 스플라인 오버슈트")
# ══════════════════════════════════════════════════════════════════════════════
N=2000
def audit(nm,hip,knee,sho,elb,amp=AMP_FULL):
    kmin=min(sample(knee,i/N) for i in range(N)); kmax=max(sample(knee,i/N) for i in range(N))
    hmax=max(abs(sample(hip,i/N)) for i in range(N))
    smin=min(sample(sho,i/N) for i in range(N)); smax=max(sample(sho,i/N) for i in range(N))
    emin=min(sample(elb,i/N) for i in range(N)); emax=max(sample(elb,i/N) for i in range(N))
    print(f"  {nm}")
    print(f"    무릎 스플라인 최소 {kmin:+7.3f}도  → 진폭{amp} 적용 {kmin*amp:+7.3f}도 "
          f"{'✓ 클램프 없음' if kmin>=0 else '★ Mathf.Max(0,·)에 잘림 = 그 구간이 평평해진다'}")
    print(f"    무릎 최대 {kmax*amp:6.2f}도  (한계 {JOINT_MAX})  {'✓' if kmax*amp<=JOINT_MAX else '✗'}")
    print(f"    엉덩이 최대 |{hmax*amp:5.2f}|도 (한계 {HIP_LIMIT})  여유배수 {HIP_LIMIT/(hmax*amp):.3f}  "
          f"{'✓' if hmax*amp<=HIP_LIMIT else '✗'}")
    print(f"    어깨 [{smin*amp:+6.2f},{smax*amp:+6.2f}]도 (한계 [-{SHO_BACK_LIM},{SHO_FWD_LIM}]) "
          f"{'✓' if -SHO_BACK_LIM<=smin*amp and smax*amp<=SHO_FWD_LIM else '✗'}")
    print(f"    팔꿈치 [{emin*amp:+6.2f},{emax*amp:+6.2f}]도 (한계 [0,{JOINT_MAX}]) "
          f"{'✓' if emin>=0 and emax*amp<=JOINT_MAX else '✗'}")
    return hmax
audit("전진(현행)", FwdHip,FwdKnee,FwdSho,FwdElb)
hmax_b = audit("뒷걸음(R5 제안)", BackHip,BackKnee,BackSho,BackElb)
print(f"\n  ★ 팩 진폭 여유배수(엉덩이 65도 한계까지): 전진 {HIP_LIMIT/(25*AMP_FULL):.4f}배 — §4에서 쓴다")

# ══════════════════════════════════════════════════════════════════════════════
line("[2] 뒷걸음 — 보폭 · 케이던스 · 속도계수 유도")
# ══════════════════════════════════════════════════════════════════════════════
dpc_f = dist_per_cycle(FwdHip,FwdKnee,AMP_FULL)*walkStrideScale
dpc_b = dist_per_cycle(BackHip,BackKnee,AMP_FULL)*walkStrideScale
print(f"  사이클 이동거리(baseline유닛, strideScale {walkStrideScale} 포함)")
print(f"    전진   {dpc_f:.4f}   = {dpc_f/BASE_H:.4f} H  (한 보 {dpc_f/2/BASE_H:.4f} H = {dpc_f/2/BASE_H*H_pt:.2f} pt)")
print(f"    뒷걸음 {dpc_b:.4f}   = {dpc_b/BASE_H:.4f} H  (한 보 {dpc_b/2/BASE_H:.4f} H = {dpc_b/2/BASE_H*H_pt:.2f} pt)")
print(f"    보폭비 = {dpc_b/dpc_f:.4f}   ← R4가 '65%'라 적은 값의 정확한 기하값")
f_fwd = walkSpeed/dpc_f
print(f"\n  정속 전진 케이던스 = {walkSpeed}/{dpc_f:.4f} = {f_fwd:.4f} Hz")
k_cad = dpc_b/dpc_f
print(f"  ★ 케이던스 일치 조건: 뒷걸음 속도계수 k 는 k = 보폭비 = {k_cad:.4f}")
print(f"     → 채택 k = 0.68  (그때 케이던스 {0.68*walkSpeed/dpc_b:.4f} Hz, 전진 대비 {0.68*walkSpeed/dpc_b/f_fwd:+.2%})")
K = 0.68
V_back_H = K*V_H
print(f"     뒷걸음 대지속도 = {K}×{V_H:.4f} = {V_back_H:.4f} H/s = {V_back_H*H_pt:.1f} pt/s")

print("\n  후퇴거리 B(신장배수) 선택 — 실보행 = B − 허용오차 0.12H")
print(f"  {'B':>6} {'실보행H':>8} {'초':>7} {'보수':>7} {'사이클':>7}  판정")
best=None
for B in (0.60,0.70,0.72,0.75,0.80,0.90,1.00):
    d=B-ARRIVE_TOL; sec=d/V_back_H; steps=d/(dpc_b/2/BASE_H); cyc=steps/2
    v = "✓ 1사이클↑" if steps>=2.0 else "✗ 1사이클 미만"
    print(f"  {B:>6.2f} {d:>8.2f} {sec:>7.3f} {steps:>7.3f} {cyc:>7.3f}  {v}")
B_SEL=0.75
d=B_SEL-ARRIVE_TOL; T_back=d/V_back_H; steps=d/(dpc_b/2/BASE_H)
print(f"\n  ★ 채택 BackStepRatio = {B_SEL}  → {T_back:.3f}초 / {steps:.3f}보 / {steps/2:.3f}사이클")
print(f"     현행(전진 1.00H) {(BACKSTEP_NOW-ARRIVE_TOL)/V_H:.3f}초 대비 {T_back-(BACKSTEP_NOW-ARRIVE_TOL)/V_H:+.3f}초 "
      f"({(T_back/((BACKSTEP_NOW-ARRIVE_TOL)/V_H)-1):+.1%})")
print(f"     ★ 사이클수 {steps/2:.3f} ≈ 1 → 나가는 위상 ≈ 들어온 위상(잔차 {abs(steps/2-1)*100:.1f}% 사이클)")
print(f"       = 진입 위상이 무엇이든 종료 자세가 진입 자세와 같다. 진입위상 미지(未知)에 불변인 성질이다.")

# ══════════════════════════════════════════════════════════════════════════════
line("[3] 뒷걸음 — 발 미끄러짐 선형성 (전진 대비 '더 나쁘지 않은가')")
# ══════════════════════════════════════════════════════════════════════════════
def linearity(hip,knee,amp=AMP_FULL):
    xs=[foot_x(hip[i]*amp,knee[i]*amp) for i in range(8)]
    d=[xs[i+1]-xs[i] for i in range(4)]      # 디딤 국면 idx0→4
    return d, max(map(abs,d))/min(map(abs,d))
df,rf = linearity(FwdHip,FwdKnee); db,rb = linearity(BackHip,BackKnee)
print(f"  디딤 국면 1/8사이클당 발 수평이동(진폭 {AMP_FULL})")
print(f"    전진   {['%+.4f'%x for x in df]}  최대/최소 = {rf:.3f}")
print(f"    뒷걸음 {['%+.4f'%x for x in db]}  최대/최소 = {rb:.3f}")
print(f"  ★ 뒷걸음이 전진보다 {rf/rb:.2f}배 더 균일하다 → 순간 미끄러짐이 전진보다 작다.")
print(f"    (평균 미끄러짐은 어느 표든 0이다 — distancePerCycle이 같은 표에서 역산되므로 항등식)")

# ══════════════════════════════════════════════════════════════════════════════
line("[4] 뒷걸음 — 무엇이 실제로 보이는가 (가시성 하한 대조)")
# ══════════════════════════════════════════════════════════════════════════════
VIS_FLOOR = 3.9   # R4 §1-5: 소은이 '보인다'고 한 망토 복귀 색중심 이동량(pt)
def excursion(hip,knee,amp=AMP_FULL):
    xs=[foot_x(sample(hip,i/N)*amp, max(0,sample(knee,i/N))*amp) for i in range(N)]
    return (max(xs)-min(xs))*scale*PT_PER_UNIT
def liftpt(hip,knee,amp=AMP_FULL):
    ds=[foot_drop(sample(hip,i/N)*amp, max(0,sample(knee,i/N))*amp) for i in range(N)]
    gr=[max(ds[i], ds[(i+N//2)%N]) for i in range(N)]
    return (max(gr)-min(gr))*scale*PT_PER_UNIT, (max(gr)-min(ds))*scale*PT_PER_UNIT, sum(gr)/N
def handpt(sho,elb,amp=AMP_FULL):
    hs=[hand_x(sample(sho,i/N)*amp, max(0,sample(elb,i/N))*amp) for i in range(N)]
    return (max(hs)-min(hs))*scale*PT_PER_UNIT, (sum(hs)/N)*scale*PT_PER_UNIT
ef,eb = excursion(FwdHip,FwdKnee), excursion(BackHip,BackKnee)
bf,lf,mf = liftpt(FwdHip,FwdKnee);  bb,lb,mb = liftpt(BackHip,BackKnee)
hf,cf = handpt(FwdSho,FwdElb);      hb,cb = handpt(BackSho,BackElb)
rows=[("발 수평 excursion", ef, eb),
      ("발 들림(최대 접지고 − 최저 발끝)", lf, lb),
      ("몸통 상하 바운스(p-p)", bf, bb),
      ("손 수평 swing 폭", hf, hb),
      ("손 평균 위치(전진 대비 앞쪽)", cf, cb),
      ("엉덩이 평균 높이", mf*scale*PT_PER_UNIT, mb*scale*PT_PER_UNIT)]
print(f"  {'채널':<34}{'전진(pt)':>10}{'뒷걸음(pt)':>12}{'차이':>9}   가시({VIS_FLOOR}pt)")
for nm,a,b in rows:
    d=b-a
    print(f"  {nm:<34}{a:>10.2f}{b:>12.2f}{d:>+9.2f}   {'보인다' if abs(d)>=VIS_FLOOR else '★ 안 보인다'}")
lean_f, lean_b = 10.0, 7.0
sh = (SHO_Y-HIP_Y)*scale*PT_PER_UNIT
print(f"\n  상체 기울임(어깨−엉덩이 {SHO_Y-HIP_Y:.2f} baseline유닛 = {sh:.2f} pt)")
print(f"    전진 {lean_f}도 → 머리 {sh*math.sin(math.radians(lean_f)):.2f} pt")
print(f"    뒷걸음 {lean_b}도 → 머리 {sh*math.sin(math.radians(lean_b)):.2f} pt   "
      f"★ 단독으로는 {VIS_FLOOR}pt 미만 = 안 보인다(자세 정합성용, 신호 아님)")
print(f"    ※ R4 §1-5는 어깨−엉덩이를 0.408유닛(0.30H)로 잡았는데 실측은 {(SHO_Y-HIP_Y)*scale:.4f}유닛"
      f"({(SHO_Y-HIP_Y)/BASE_H:.4f} H) — R4가 {(1-0.408/((SHO_Y-HIP_Y)*scale))*100:.0f}% 과소계상했다(자기정정).")

# ══════════════════════════════════════════════════════════════════════════════
line("[5] 표 교체 팝 — 뒷걸음 진입에 교차페이드가 필요한가")
# ══════════════════════════════════════════════════════════════════════════════
jump=max(abs(sample(FwdHip,i/N)-sample(BackHip,i/N))*AMP_FULL for i in range(N))
jknee=max(abs(sample(FwdKnee,i/N)-sample(BackKnee,i/N))*AMP_FULL for i in range(N))
print(f"  같은 위상에서 전진표↔뒷걸음표 최대 각도차: 엉덩이 {jump:.2f}도 / 무릎 {jknee:.2f}도")
tau=1/poseSmoothingRate
print(f"  poseSmoothingRate {poseSmoothingRate} → τ={tau*1000:.1f}ms. 60fps 1프레임에 "
      f"{(1-math.exp(-poseSmoothingRate/60))*100:.1f}% 도달 → 엉덩이 {jump*(1-math.exp(-poseSmoothingRate/60)):.1f}도/프레임")
print(f"  ★ 이 저장소가 이미 '버그로 보인다'고 판정한 값 = 프레임당 무릎 9도"
      f" (FramePacingPolicy.cs AwaySeconds 문서). 위 값은 그 "
      f"{jump*(1-math.exp(-poseSmoothingRate/60))/9:.1f}배다 → 교차페이드 필요.")

# ══════════════════════════════════════════════════════════════════════════════
line("[6] 기동 전이(안 A) — 재현 + 15fps/60fps 이중 예산")
# ══════════════════════════════════════════════════════════════════════════════
open0 = IDLE_SPREAD/(FwdHip[0]*AMP_FULL)
print(f"  walkStartOpenAtEntry = idleLegSpread {IDLE_SPREAD} / (25×{AMP_FULL}) = {open0:.4f}")
print(f"    → 진입 벌림 = {FwdHip[0]*AMP_FULL*open0:.4f}도 (Idle {IDLE_SPREAD}도와 소수점까지 동일 = 팝 0)")
for T,nm in ((0.05,"현행(사실상 램프 없음)"),(0.12,"F1 하한"),(0.20,"F2 하한"),(0.22,"안 A"),(0.18,"안 B")):
    print(f"  램프 {T:.2f}s ({nm:<20s}) : 60fps {T*60:5.1f}프레임 / 15fps {T*15:4.1f}프레임 / "
          f"최악(첫 66.7ms 소실 뒤 60fps) {max(0,(T-1/15))*60:5.1f}프레임")
# 진입 사이클 주파수
def freq_at(openv):
    amp=AMP_FULL*openv
    return walkSpeed/(dist_per_cycle(FwdHip,FwdKnee,amp)*walkStrideScale)
print(f"\n  진입 순간 사이클 주파수 = {freq_at(open0):.4f} Hz (정속 {f_fwd:.4f} Hz의 {freq_at(open0)/f_fwd:.2f}배)")
def ss(u): return u*u*(3-2*u)
T=0.22; phase=0.0; dt=1/60.0; t=0.0
while t<T:
    o=open0+(1-open0)*ss(min(1.0,t/T)); phase+=freq_at(o)*dt; t+=dt
print(f"  램프 {T}s 동안 진행한 위상 = {phase:.4f} 사이클 = {phase*2:.3f}보 "
      f"(몸 이동 {phase*dpc_f/BASE_H*H_pt:.1f} pt)")

# 판정 지표: 프레임당 무릎 각 변화
def max_knee_rate(freq, amp, fps):
    m=0.0
    for i in range(N):
        a=max(0,sample(FwdKnee,i/N))*amp; b=max(0,sample(FwdKnee,i/N+freq/fps))*amp
        m=max(m,abs(b-a))
    return m
print(f"\n  ★ 판정 지표 — 인접 프레젠트 프레임 간 무릎 각 변화(도)")
print(f"    {'구간':<38}{'주파수Hz':>9}{'60fps':>8}{'15fps':>8}")
for nm,fr,am in (("정속 전진(양성 대조 기준값)",f_fwd,AMP_FULL),
                 ("안 A 진입 피크",freq_at(open0),AMP_FULL*open0),
                 ("안 B 진입 피크",freq_at(0.55),AMP_FULL*0.55),
                 ("현행 진입(램프 없음)",f_fwd,AMP_FULL)):
    print(f"    {nm:<38}{fr:>9.3f}{max_knee_rate(fr,am,60):>8.2f}{max_knee_rate(fr,am,15):>8.2f}")
print(f"    기준선: 9.0도/프레임 = 이 저장소가 '버그로 보인다'고 이미 판정한 값")

# ══════════════════════════════════════════════════════════════════════════════
line("[7] 착탄 비트 게이트 — 채택 판정 + Ucap 개방 가능성")
# ══════════════════════════════════════════════════════════════════════════════
BEAT=0.26
def flight(dH): return min(max(A['flight']*math.sqrt(max(0.25,dH/A['refRatio'])), A['flight']*0.6), A['flightMax'])
cyc = A['draw']+A['aim']+A['recover']
print(f"  현행 1발 주기 = draw {A['draw']} + aim {A['aim']} + recover {A['recover']} = {cyc:.2f}초")
print(f"  {'d(H)':>7}{'비행':>8}{'착탄−다음Draw':>15}{'착탄−다음Release':>18}   현행 판정")
for dH in (2.60,3.63,4.60,6.60,10.0,13.40,13.99,18.70):
    fl=flight(dH)
    print(f"  {dH:>7.2f}{fl:>8.3f}{fl-A['recover']:>+15.3f}{fl-(A['recover']+A['draw']+A['aim']):>+18.3f}"
          f"   {'★2발 동시체공' if fl>cyc else ('다음 Draw 중 착탄' if fl>A['recover'] else '회복 중 착탄')}")
print(f"\n  게이트 도입: gate = max(recover {A['recover']}, 비행 + 비트 {BEAT})")
print(f"  {'d(H)':>7}{'gate':>8}{'착탄→다음Draw':>15}   {'총 사이클(3발,접근 0.75H 뒷걸음)':>20}")
T_app = (B_SEL-ARRIVE_TOL)/V_back_H
for dH in (3.63,4.60,6.60,10.0,13.40,18.70):
    fl=flight(dH); g=max(A['recover'],fl+BEAT)
    total = T_app + A['intro'] + 3*(A['draw']+A['aim']) + 2*g + (fl+A['outro'])
    base  = (BACKSTEP_NOW-ARRIVE_TOL)/V_H + A['intro'] + 3*(A['draw']+A['aim']+A['recover']) \
            - A['recover'] + A['flight'] + A['outro']
    print(f"  {dH:>7.2f}{g:>8.3f}{g-fl:>+15.3f}   {total:>20.3f}초  (현행 {base:.3f}초 대비 {total/base-1:+.1%})")
print(f"\n  ★ 게이트가 있으면 '착탄 → 다음 Draw' 간격이 어떤 사거리에서도 ≥ {BEAT}초로 고정된다")
print(f"    → 2발 동시체공이 구조적으로 불가능 → design-systems의 Ucap 13.4 제약이 사라진다")
print(f"    Ucap 18.70H 근거 재확인: 비행 {flight(18.70):.4f}초 = 상한 {A['flightMax']} (여기서 포화)")
sp=18.70/flight(18.70)
print(f"    18.70H 화살 평균속도 = {sp:.2f} H/s = {sp*H_pt:.0f} pt/s = 60fps에서 {sp*H_pt/60:.2f} pt/프레임")
print(f"    화살대 길이 = {ARROW_SHAFT_RATIO}H = {ARROW_SHAFT_RATIO*H_pt:.2f} pt  → "
      f"프레임당 이동/화살대 = {sp*H_pt/60/(ARROW_SHAFT_RATIO*H_pt):.3f} "
      f"({'✓ <1 = 프레임끼리 겹친다(스트로빙 없음)' if sp*H_pt/60<ARROW_SHAFT_RATIO*H_pt else '✗ 끊긴다'})")
print(f"\n  ★ 전제조건 C2 — Outro는 지금 고정 {A['flight']}초를 읽는다(ArcheryState.cs:265).")
print(f"    18.70H에서 비행 {flight(18.70):.3f}초 > {A['flight']}초 → "
      f"마지막 화살이 꽂히기 {flight(18.70)-A['flight']:.3f}초 전에 상태가 끝날 수 있다.")
print(f"    Outro가 _lastFlightSeconds를 읽지 않으면 Ucap 개방은 성립하지 않는다.")

# ══════════════════════════════════════════════════════════════════════════════
line("[8] DLC 팩 — 모션에도 '6개 더하면 넘친다'가 있는가 (사운드 +15.56dB 대응)")
# ══════════════════════════════════════════════════════════════════════════════
print("  사운드의 위험: 같은 키에 6클립 동시재생 → 이론합성 +15.56 dB (design-sound §3-1)")
print(f"  모션 대응 위험 A — 진폭 곱셈 누적")
head = HIP_LIMIT/(FwdHip[0]*AMP_FULL)
print(f"    엉덩이 여유배수 = 관절한계 {HIP_LIMIT} / 정속 봉우리 {FwdHip[0]*AMP_FULL} = {head:.4f}배")
print(f"    팩 N개가 각각 k배를 곱하면 k^N ≤ {head:.4f} 이어야 한다")
for n in (1,2,3,6):
    print(f"      팩 {n}개 → 팩당 허용 k ≤ {head**(1/n):.4f}  (= +{ (head**(1/n)-1)*100:.1f}%)")
k=1.15
tot=k**6
print(f"    ★ 팩마다 '조금만' +15%를 얹으면 6팩에서 {k}^6 = {tot:.3f}배")
print(f"      엉덩이 {FwdHip[0]*AMP_FULL*tot:.2f}도 > 한계 {HIP_LIMIT} (초과 {FwdHip[0]*AMP_FULL*tot-HIP_LIMIT:.2f}도)")
d2=dist_per_cycle(FwdHip,FwdKnee,AMP_FULL*tot)*walkStrideScale
print(f"      보폭 {dpc_f:.4f} → {d2:.4f} ({d2/dpc_f:.3f}배), 케이던스 {f_fwd:.3f} → "
      f"{walkSpeed/d2:.3f} Hz ({walkSpeed/d2/f_fwd:.3f}배)")
print(f"      = 같은 속도로 '느리게 크게 벌리는' 걸음. 사운드의 +15.56dB와 같은 자리의 고장이다.")
print(f"\n  모션 대응 위험 B — 채널 배타성(사운드에는 없는 형태)")
print("    ApplyLimb(limb, upper, lower, …)은 한 관절에 한 프레임 한 각도만 쓴다.")
print("    두 팩이 같은 (상태 × 관절)을 원하면 물리적으로 섞이지 않는다 →")
print("    '마지막 기입자 승리' = 어느 팩이 보일지 비결정적. 소리는 시끄러워지지만 모션은 조용히 사라진다.")
print(f"\n  모션 대응 위험 C — 박자는 다른 팀 수치의 입력이다(실증)")
print(f"    design-systems의 Ucap 유도식이 내 박자를 쓴다: "
      f"d ≤ {A['refRatio']}H × ((recover+draw+aim)/{A['flight']})² = {A['refRatio']*(cyc/A['flight'])**2:.2f}H")
print("    팩이 draw/aim/recover를 만질 수 있으면 사거리 밴드가 조용히 바뀐다 → 팩은 박자를 못 바꾼다.")

print("\n" + "="*78); print("검산 끝 — 교정 통과 상태에서 산출된 값만 위에 있다"); print("="*78)
