# -*- coding: utf-8 -*-
"""기동 전이(walk start opening) 수치 검토.
읽은 값: LegHipKeys peak 25 (StickmanPoseAnimator.cs:105), walkPoseAmplitudeScale 1.0(:49),
WalkAmplitudeAtRest .85 / AtFullSpeed 1.35(:170,173), walkStrideScale .93(:59),
idleLegSpreadDegrees 12(StickConfig.cs:63), poseSmoothingRate 35(:80)."""
import math
HIP_PEAK=25.0; WPAS=1.0; AMP_FULL=1.35; STRIDE=0.93; IDLE_SPREAD=12.0; POSE_RATE=35.0
V=1.5; H=1.36; PT=1004.9/24.56
amp_entry_now = HIP_PEAK*WPAS*AMP_FULL
print(f"[현행] Walk 진입 첫 틱의 엉덩이 진폭 = {HIP_PEAK} x {WPAS} x {AMP_FULL} = ±{amp_entry_now:.2f}도")
print(f"       Idle 스탠스 = ±{IDLE_SPREAD:.0f}도  ->  다리 벌림이 {2*IDLE_SPREAD:.0f}도에서 {2*amp_entry_now:.0f}도로")
print(f"       한 관절당 {amp_entry_now-IDLE_SPREAD:.2f}도 점프. poseSmoothingRate={POSE_RATE} (tau={1000/POSE_RATE:.0f}ms)")
for t in (1/60,2/60,3/60,5/60):
    f=1-math.exp(-POSE_RATE*t)
    print(f"         t={t*1000:>5.1f}ms  {f:>5.1%} 도달 = ±{IDLE_SPREAD+(amp_entry_now-IDLE_SPREAD)*f:.1f}도")
print()
# 기준 사이클 주파수
d_cycle_full = 1.0   # 상대값. distancePerCycle는 진폭에 (거의) 비례한다고 두고 비율만 본다.
print("[기준] 정속 사이클 주파수: 설정 주석의 '약 1.35Hz'를 기준으로 비율만 본다.")
F0=1.35
def run(open0, T, label):
    dt=1/240.; ph=0.0; fmax=0; n=int(T/dt)
    for i in range(n):
        t=i*dt; u=t/T; s=u*u*(3-2*u)                 # smoothstep
        o=open0+(1-open0)*s
        f=F0/o                                        # distancePerCycle ∝ 진폭  -> 주파수 ∝ 1/o
        fmax=max(fmax,f); ph+=f*dt
    hip0=amp_entry_now*open0
    print(f"  {label}")
    print(f"    open(0)={open0:.3f} -> 진입 엉덩이 ±{hip0:.2f}도 (Idle ±{IDLE_SPREAD:.0f}도 대비 팝 {abs(hip0-IDLE_SPREAD):.2f}도,"
          f" 현행 {amp_entry_now-IDLE_SPREAD:.2f}도의 {abs(hip0-IDLE_SPREAD)/(amp_entry_now-IDLE_SPREAD):.0%})")
    print(f"    램프 {T:.2f}s 동안 최대 사이클 주파수 {fmax:.2f}Hz (정속 {F0}Hz의 {fmax/F0:.2f}배),"
          f" 위상 진행 {ph:.2f}사이클 = 걸음 {2*ph:.1f}보")
    print(f"    같은 구간 몸 이동 {V*T*PT:.1f}pt = {V*T/H:.2f}H   (발 미끄러짐: 0 — 보폭역산이 같은 진폭을 쓴다)")
run(12.0/amp_entry_now, 0.22, "안 A — 팝 0 (open0 = 12/33.75)")
run(0.55, 0.18, "안 B — 팝 완화(open0=0.55)")
run(0.70, 0.14, "안 C — 보수적(open0=0.70)")
print()
print("[정지 쪽] 대칭 닫힘 — Walk->Idle에서 같은 곡선을 역방향으로")
print("  현행: 진폭이 그대로인 채 Idle 중립각으로 poseSmoothingRate 35(tau 29ms)로 스냅 = 1~2프레임")
print("  소은 실측: 망토 복귀 0.3초가 그 스냅을 덮고 있다(색중심 +3.9 -> +2.4 -> +0.6 -> 0)")
for T in (0.12,0.14,0.16):
    print(f"  닫힘 {T:.2f}s -> 망토 복귀 0.30s의 {T/0.30:.0%}. 닫힘이 망토보다 먼저 끝나야 follow-through로 읽힌다.")
