# -*- coding: utf-8 -*-
"""design-motion R4 검산 — 3건 전부.
값의 출처는 전부 StickConfig.cs / DialogueKind.cs / stickmate.log이며 이 파일은 산술만 한다.
"""
import math

# ── 프로덕션에서 읽은 값 (베끼지 않고 grep으로 확인한 실제 값) ────────────────
walkSpeed                = 2.5     # StickConfig.cs:26
characterScale           = 0.60    # 로그 유도: 뛰어내리기 내딛는속도 1.20 / hopDownStepOffSpeedScale 0.8
V                        = walkSpeed * characterScale          # ResolveWalkSpeed()
walkSpeedSmoothingRate   = 6.0     # StickConfig.cs:85
poseSmoothingRate        = 35.0    # StickConfig.cs:80
bodyLeanSmoothingRate    = 12.0    # StickConfig.cs:2934
bodyLeanRunMaxDegrees    = 10.0    # StickConfig.cs:2911
H_units                  = 1.36    # 로그 "신장 1.36유닛"
PT_PER_UNIT              = 1004.9 / 24.56   # 활쏘기 사거리 계산 문서의 Dock 실측 환산

BaseSeconds       = 0.28   # DialogueKind.cs:79
PerGlyphSeconds   = 0.075  # DialogueKind.cs:82
MinSeconds        = 0.62   # DialogueKind.cs:85
MaxSeconds        = 2.20   # DialogueKind.cs:89
FadeInSeconds     = 0.06   # DialogueKind.cs:352
PerLatinGlyph     = 0.0472 # design-narrative R2 제안값(아직 프로덕션 아님)

wanderIdleDurationMin = 2.0    # StickConfig.cs:805
wanderWalkDurationMin = 1.5    # StickConfig.cs:811
wanderDurationJitterRatio = 0.175  # StickConfig.cs:857
parkourClimbDuration  = 1.20   # StickConfig.cs:228
ledgeHangGrabDuration = 0.28   # StickConfig.cs:458
ledgeHangHoldDurationMin = 0.84# StickConfig.cs:466
getupDuration         = 0.60   # StickConfig.cs:185
attackDuration        = 0.40   # StickConfig.cs:880
landingCrouchDurationBrace = 0.88  # StickConfig.cs:375

archery = dict(intro=0.55, draw=0.42, aim=0.30, recover=0.34,
               recoil=0.18, outro=0.55, flight=0.62, damping=14.0)

print("="*78); print("[0] 기준 스케일"); print("="*78)
print(f"  ResolveWalkSpeed = {walkSpeed} x {characterScale} = {V:.3f} 유닛/s")
print(f"  1유닛 = {PT_PER_UNIT:.3f} pt  ->  걷기 {V*PT_PER_UNIT:.1f} pt/s   (소은 실측 62~63 pt/s)")
print(f"  신장 H = {H_units} 유닛 = {H_units*PT_PER_UNIT:.1f} pt")

print(); print("="*78); print("[1] 출발/정지 비대칭 — 8.6fps 표본이 무엇을 만들 수 있는가"); print("="*78)
fps=8.6; win=1.0/fps
v_pt = V*PT_PER_UNIT
print(f"  표본 간격 = {win*1000:.1f} ms, 정속 {v_pt:.1f} pt/s")
# 순간 정지가 창 안 t* 지점에서 일어났을 때 그 창의 평균 속도
for avg in (10.0,):
    t_star = avg*win/v_pt
    print(f"  가설: 감속 0(즉시). 그 창의 평균속도가 {avg:.0f} pt/s가 되려면 t* = {t_star*1000:.1f} ms")
    print(f"        -> 그 창의 실제 이동거리 = {v_pt*t_star:.2f} pt   (소은 실측 '정지 후 추가이동 1.0~1.5pt')")
# 출발에서 중간 표본이 안 보일 확률
lo,hi=0.15,0.85
print(f"  출발에서 '중간값(전속의 {lo:.0%}~{hi:.0%})'이 한 표본도 안 잡힐 확률 = {1-(hi-lo):.0%}")
print(f"     -> n=1 관측으로 출발/정지 비대칭을 구분할 수 없다(정지 쪽 관측도 같은 앨리어싱으로 전부 설명된다).")

print(); print("--- 만약 속도 램프를 넣으면? (금지 근거) ---")
def slip_for_ramp(T, rate=walkSpeedSmoothingRate, Vv=V):
    """v(t)=Vt/T 램프 + 포즈가 보는 속도 s는 rate로 지수추종.
       s(t) = V/T (t - (1-e^-rt)/r).  미끄러짐 = ∫(v-s)dt"""
    r=rate
    peak = Vv/T*(1-math.exp(-r*T))/r
    tot  = Vv/T*(T/r + (math.exp(-r*T)-1)/r**2)
    return peak, tot
print(f"  {'램프T(s)':>8}{'램프끝 순간슬립':>16}{'전속대비':>10}{'누적슬립(유닛)':>15}{'pt':>8}{'H':>8}")
for T in (0.10,0.15,0.18,0.25,0.35):
    p,t = slip_for_ramp(T)
    print(f"  {T:>8.2f}{p:>16.3f}{p/V:>9.0%}{t:>15.4f}{t*PT_PER_UNIT:>8.2f}{t/H_units:>8.3f}")
print("  * 포즈의 사이클 주파수는 _smoothedSpeed(walkSpeedSmoothingRate=6, tau=167ms)로 돈다.")
print("    램프 길이가 tau와 같은 자릿수라 다리가 몸보다 느리게 논다 = 문워크. 이게 금지 근거다.")
print()
for T in (0.18,):
    print(f"  램프 T={T}s가 실제로 벌어 주는 그림상의 차이:")
    d_ramp = V*T/2; d_inst = V*T
    print(f"    같은 {T}s 동안 이동  램프 {d_ramp*PT_PER_UNIT:.2f} pt / 즉시 {d_inst*PT_PER_UNIT:.2f} pt")
    print(f"    차이 {abs(d_inst-d_ramp)*PT_PER_UNIT:.2f} pt = 신장의 {abs(d_inst-d_ramp)/H_units:.1%}")

print(); print("--- 제안: 속도는 그대로, 상체 역기울임(anticipation) ---")
A=4.0; L=bodyLeanRunMaxDegrees; k=bodyLeanSmoothingRate
print(f"  현행: 목표각이 첫 틱에 {L:.0f}도로 점프, 실제각은 rate={k}(tau={1000/k:.0f}ms)로 추종")
for t in (0.05,0.083,0.15,0.25,0.35):
    print(f"    t={t*1000:>4.0f}ms  실제 기울임 {L*(1-math.exp(-k*t)):>5.2f}도")
print(f"  제안: 목표각 = {L:.0f}*s01 - {A:.0f}*exp(-t/0.10)  (0.10s 시정수의 역방향 킥)")
for t in (0.0,0.05,0.10,0.15,0.20,0.30,0.45):
    tgt = L - A*math.exp(-t/0.10)
    print(f"    t={t*1000:>4.0f}ms  목표 {tgt:>6.2f}도")
# 실제각 수치적분
dt=1/240.; x=0.0; log=[]
for i in range(int(0.6/dt)):
    t=i*dt; tgt = L - A*math.exp(-t/0.10)
    x += (tgt-x)*(1-math.exp(-k*dt))
    if abs(t-round(t,2))<dt/2 and round(t,2) in (0.0,0.05,0.10,0.15,0.20,0.30,0.45,0.60): log.append((t,x))
print("  실제각(rate=12 추종, 수치적분 dt=1/240):")
for t,x in log: print(f"    t={t*1000:>4.0f}ms  실제 {x:>6.2f}도")
xs=[]; x=0.0
for i in range(int(0.6/dt)):
    t=i*dt; tgt=L-A*math.exp(-t/0.10); x+=(tgt-x)*(1-math.exp(-k*dt)); xs.append(x)
print(f"  최저점 {min(xs):+.2f}도 @ t={xs.index(min(xs))*dt*1000:.0f}ms  (음수 = 진행 반대쪽으로 젖힘)")
# 어깨 이동량
shoulder_hip = 0.30*H_units   # 어깨~엉덩이 대략 0.30H (StickConfig 툴팁의 유도 방식 차용, 근사)
print(f"  머리쪽 수평 변위 = (어깨~엉덩이 {shoulder_hip:.3f}유닛) x sin(각도)")
print(f"    -4.0도 -> {shoulder_hip*math.sin(math.radians(4))*PT_PER_UNIT:.2f} pt 뒤로")
print(f"   +10.0도 -> {shoulder_hip*math.sin(math.radians(10))*PT_PER_UNIT:.2f} pt 앞으로")
print("  ※ 몸통 속도는 한 프레임도 늦추지 않는다 -> 발 미끄러짐 0, 상태 최소지속 비용 0.")

print(); print("="*78); print("[2] 활쏘기 박자"); print("="*78)
a=archery
cyc_now = a['draw']+a['aim']+a['recover']
print(f"  현행 1발 주기 = draw {a['draw']} + aim {a['aim']} + recover {a['recover']} = {cyc_now:.2f}s")
print(f"  화살 비행 {a['flight']:.2f}s > recover {a['recover']:.2f}s  ->  착탄이 다음 Draw의")
print(f"     {a['flight']-a['recover']:.2f}s 지점에 떨어진다 = 결과를 보는 비트가 0")
approach_units = H_units
approach_s = approach_units/V
print(f"  현행 접근 = {approach_units:.2f}유닛(=1.00H) / {V:.2f} = {approach_s:.3f}s   (로그 4/4 동일)")
tot_now = approach_s + a['intro'] + 3*cyc_now + a['flight'] + a['outro']
print(f"  현행 총 길이 = {approach_s:.2f} + {a['intro']} + 3x{cyc_now:.2f} + {a['flight']} + {a['outro']} = {tot_now:.2f}s")
beat=0.26
gate = max(a['recover'], a['flight']+beat)
cyc_new = gate + a['draw'] + a['aim']
print(f"\n  제안 착탄 비트 = {beat:.2f}s (= 현행 초과분 {a['flight']-a['recover']:.2f}s와 같은 값)")
print(f"  제안 1발 주기 = max(recover {a['recover']}, flight {a['flight']} + beat {beat}) + draw + aim = {cyc_new:.2f}s")
tot_b = 0.18 + a['intro'] + 0 + 2*cyc_new + (a['draw']+a['aim']) + a['flight'] + beat + (a['outro']-beat)
print(f"  제안 총 길이(접근 삭제, 마지막 비트는 outro가 흡수)")
print(f"     = 정지 0.18 + intro {a['intro']} + 접근 0 + 2x{cyc_new:.2f} + (draw+aim {a['draw']+a['aim']:.2f})"
      f" + flight {a['flight']} + outro {a['outro']} = {tot_b:.2f}s")
print(f"  차이 = {tot_b-tot_now:+.2f}s ({(tot_b-tot_now)/tot_now:+.1%})")
print(f"  수평 정지: archeryHorizontalDamping={a['damping']} -> tau={1000/a['damping']:.0f}ms,"
      f" 0.18s에 잔류 {math.exp(-a['damping']*0.18):.1%} ({V*math.exp(-a['damping']*0.18)*PT_PER_UNIT:.2f} pt/s)")

print(); print("  --- 뒷걸음이 기능적으로 필요한가 (실측 4회) ---")
edge = 12.929
eps=[(3.45,-1),(-6.90,+1),(3.06,-1),(-11.79,+1)]
band_hi = 9.01
for x0,d in eps:
    room = (x0-(-edge)) if d<0 else (edge-x0)
    print(f"    x0={x0:>7.2f} 사격방향={'왼쪽' if d<0 else '오른쪽'}"
          f"  가용 {room:>6.2f}유닛({room/H_units:>5.2f}H)  밴드상한 {band_hi:.2f}유닛  -> 후퇴 필요 {'예' if room<band_hi else '아니오'}")
print("    4/4 전부 '아니오'인데 4/4 전부 1.00H 후퇴했다 = 무조건 후퇴.")

print(); print("="*78); print("[3] 대사 가독예산 vs 상태 최소 지속"); print("="*78)
def read_ko(n): return min(max(BaseSeconds+n*PerGlyphSeconds, MinSeconds), MaxSeconds)
def read_en(n): return min(max(BaseSeconds+n*PerLatinGlyph , MinSeconds), MaxSeconds)
def cap(dwell, fn):
    n=0
    while fn(n+1)+FadeInSeconds <= dwell+1e-9: n+=1
    return n
print("  [교정] 리더 제시값 재현:  10자->%.3f  8자->%.3f  7자->%.3f (기대 1.090/0.940/0.865)"
      % (read_ko(10)+FadeInSeconds, read_ko(8)+FadeInSeconds, read_ko(7)+FadeInSeconds))
rows=[
 ("Idle",  "계획(narrative가 쓴 분모)", wanderIdleDurationMin, "wanderIdleDurationMin"),
 ("Idle",  "계획 최악(지터 -17.5%)",    wanderIdleDurationMin*(1-wanderDurationJitterRatio), "x(1-jitter)"),
 ("Idle",  "실측 최소(로그 241건)",      0.18, "그라피티 강제발동이 끊음"),
 ("Walk",  "계획(narrative가 쓴 분모)", wanderWalkDurationMin, "wanderWalkDurationMin"),
 ("Walk",  "계획 최악(지터 -17.5%)",    wanderWalkDurationMin*(1-wanderDurationJitterRatio), "x(1-jitter)"),
 ("Walk",  "실측 최소(로그 241건)",      0.16, "되올라가기가 끊음"),
 ("ParkourClimb","고정",               parkourClimbDuration, "parkourClimbDuration"),
 ("LedgeHang","보장 하한",             ledgeHangGrabDuration+ledgeHangHoldDurationMin, "grab+holdMin"),
 ("Getup", "고정",                      getupDuration, "getupDuration"),
 ("Attack","고정",                      attackDuration, "attackDuration"),
 ("LandingCrouch(Brace)","고정",        landingCrouchDurationBrace, "landingCrouchDurationBrace"),
]
print(f"\n  {'상태':<22}{'구분':<24}{'초':>6}{'한글상한':>9}{'영어상한':>9}  근거")
for st,kind,d,src in rows:
    print(f"  {st:<22}{kind:<24}{d:>6.2f}{cap(d,read_ko):>9}{cap(d,read_en):>9}  {src}")
print(f"\n  최소 발화(4자 이하) 필요체류 = {MinSeconds+FadeInSeconds:.2f}s"
      f"  -> 이보다 짧은 상태는 어떤 대사도 못 싣는다.")
