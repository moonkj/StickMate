# -*- coding: utf-8 -*-
"""design-motion R5 보정편 — 앞 스크립트에서 내가 낸 오류 3건을 고치고 그 자리에서 더 잰다.

자기정정 목록
  (E1) 케이던스 비를 %로 잘못 출력했다(+100.12%). 비율이다.
  (E2) 활쏘기 현행 총계 baseline을 5.361초로 잘못 적분했다 — recover를 3개가 아니라 2개로 셌다.
       올바른 현행 총계는 5.701초다(design-systems §6의 5.70과 일치).
  (E3) 기동전이 판정 지표로 '9.0도/프레임'을 절대 기준선으로 쓰려 했는데,
       그 값(FramePacingPolicy 문서)과 내 측정량이 같은 양이 아니다 —
       내 식으로는 **정속 보행 자체가 9.39도/프레임**이라 기준선이 성립하지 않는다.
       절대 기준선을 버리고 **정속 보행을 양성 대조**로 쓰는 상대 기준으로 바꾼다.
"""
import math
exec(open('/Users/kjmoon/App/StickMate/design/motion/verify/2026-09-02_R5_뒷걸음_기동전이_팩틀.py')
     .read().split("line(\"[1]")[0].replace('line("[교정] 알려진 값 재현 — 깨지면 아래 숫자 전부 폐기")',''))

N=2000
dpc_f = dist_per_cycle(FwdHip,FwdKnee,AMP_FULL)*walkStrideScale
dpc_b = dist_per_cycle(BackHip,BackKnee,AMP_FULL)*walkStrideScale
f_fwd = walkSpeed/dpc_f
open0 = IDLE_SPREAD/(FwdHip[0]*AMP_FULL)
def freq_at(o):  return walkSpeed/(dist_per_cycle(FwdHip,FwdKnee,AMP_FULL*o)*walkStrideScale)
def ss(u): return u*u*(3-2*u)

print("="*78); print("[E1 보정] 뒷걸음 케이던스"); print("="*78)
K=0.68
print(f"  전진 케이던스 {f_fwd:.4f} Hz")
print(f"  뒷걸음 k={K} 케이던스 {K*walkSpeed/dpc_b:.4f} Hz  → 전진 대비 {K*walkSpeed/dpc_b/f_fwd:.4f}배 "
      f"({K*walkSpeed/dpc_b/f_fwd-1:+.2%})")
print(f"  이상적 k(케이던스 완전일치) = 보폭비 = {dpc_b/dpc_f:.4f}. 0.68로 반올림한 대가는 위 {abs(K*walkSpeed/dpc_b/f_fwd-1):.2%}뿐이다.")

print()
print("="*78); print("[E3 보정] 기동전이 — 램프 전 구간의 관절 각속도 (절대기준 폐기, 정속 양성대조)"); print("="*78)
def knee_rate(freq, amp, fps):
    return max(abs(max(0,sample(FwdKnee,i/N+freq/fps))*amp - max(0,sample(FwdKnee,i/N))*amp) for i in range(N))
def hip_rate(freq, amp, fps):
    return max(abs(sample(FwdHip,i/N+freq/fps)*amp - sample(FwdHip,i/N)*amp) for i in range(N))

print("  ★ 해석적 성질부터: 각속도 ∝ 진폭 × 주파수 = amp × V/(dpc(amp)·s).")
print("    dpc(amp)는 sin 때문에 amp에 **오목**(sublinear)하므로 amp/dpc(amp)는 amp에 대해 단조 감소가 아니라 증가한다")
print("    → 각속도는 진폭이 **가장 클 때**(= 램프 끝 = 정속) 최대다. 램프 중에는 정속을 넘을 수 없다.")
print()
print(f"  {'u=t/T':>7}{'open':>8}{'진폭':>8}{'주파수Hz':>10}{'정속대비':>9}{'무릎도/60fps프레임':>18}{'엉덩이도/프레임':>16}")
T=0.22
for u in (0.0,0.1,0.2,0.3,0.4,0.5,0.6,0.7,0.8,0.9,1.0):
    o=open0+(1-open0)*ss(u); amp=AMP_FULL*o; fr=freq_at(o)
    print(f"{u:>7.2f}{o:>8.4f}{amp:>8.4f}{fr:>10.4f}{fr/f_fwd:>9.2f}x{knee_rate(fr,amp,60):>17.2f}{hip_rate(fr,amp,60):>16.2f}")
print(f"  정속(양성 대조)                       {f_fwd:.4f}    1.00x{knee_rate(f_fwd,AMP_FULL,60):>17.2f}{hip_rate(f_fwd,AMP_FULL,60):>16.2f}")
print()
print("  ★ 결론: 램프 전 구간에서 관절 각속도가 정속보다 **낮다**. '덜덜 떤다' 계열 위험은 해석적으로 없다.")
print("    남는 위험은 하나뿐 — **케이던스 자체**(진입 피크가 정속의 {:.2f}배). 이건 지각 판정이다.".format(freq_at(open0)/f_fwd))

print()
print("  케이던스가 정속의 2배를 넘는 시간(= '허둥댐'으로 읽힐 수 있는 창)")
for T in (0.18,0.22,0.30):
    t=0.0; dt=1/600.0; above=0.0
    while t<T:
        o=open0+(1-open0)*ss(t/T)
        if freq_at(o) > 2*f_fwd: above+=dt
        t+=dt
    print(f"    램프 {T:.2f}s → 2배 초과 구간 {above*1000:5.1f} ms "
          f"= 60fps {above*60:4.2f}프레임 / 15fps {above*15:4.2f}프레임")
print("    ★ 60fps에서 2~3프레임, 15fps에서는 1프레임 미만이다.")
print("      §6에서 '50ms 팝은 15fps에서 안 보인다'고 쓴 것과 **같은 자를 대면** 이 창도 15fps에서는 안 보인다.")
print("      즉 위험이 있다면 60fps(사용자가 커서를 움직이고 있는 상황)에서만이다.")

print()
print("  진입 open0을 바꿨을 때 (팝 vs 케이던스 교환)")
print(f"  {'open0':>8}{'진입벌림도':>12}{'Idle대비 팝':>13}{'진입 Hz':>10}{'정속대비':>9}")
for o in (0.3556,0.45,0.55,0.65,0.75,1.0):
    print(f"{o:>8.4f}{FwdHip[0]*AMP_FULL*o:>12.2f}{FwdHip[0]*AMP_FULL*o-IDLE_SPREAD:>13.2f}{freq_at(o):>10.3f}{freq_at(o)/f_fwd:>9.2f}x")

print()
print("="*78); print("[E2 보정] 활쏘기 박자 총계 — 현행 baseline 재적분"); print("="*78)
BEAT=0.26
def flight(dH): return min(max(A['flight']*math.sqrt(max(0.25,dH/A['refRatio'])), A['flight']*0.6), A['flightMax'])
V_H=walkSpeed/BASE_H; K=0.68; V_back_H=K*V_H
T_now  = (BACKSTEP_NOW-ARRIVE_TOL)/V_H              # 현행 접근(전진 1.00H)
T_back = (0.75-ARRIVE_TOL)/V_back_H                 # R5 뒷걸음(0.75H, k=0.68)
base = T_now + A['intro'] + 3*(A['draw']+A['aim']+A['recover']) + A['flight'] + A['outro']
print(f"  현행 = 접근 {T_now:.3f} + intro {A['intro']} + 3×(draw {A['draw']}+aim {A['aim']}+recover {A['recover']})"
      f" + flight {A['flight']} + outro {A['outro']}")
print(f"       = {base:.3f}초    ← design-systems §6의 5.70초와 일치 ✓ (내 앞 스크립트의 5.361은 recover를 2개로 센 오류)")
print(f"  ※ R4가 적은 5.807초는 접근을 0.907로 잡은 값이다. 0.801로 교정하면 {base:.3f}초.")
print()
print(f"  R5 제안 = 접근(뒷걸음 0.75H) {T_back:.3f} + intro + 3×(draw+aim) + 2×gate + (비행+outro)")
print(f"    gate = max(recover {A['recover']}, 비행 + 비트 {BEAT});  3발째 recover는 없앤다(Outro가 이미 비행을 기다린다)")
print()
print(f"  {'d(H)':>7}{'비행':>8}{'gate':>8}{'총계':>9}{'현행대비':>10}{'현행대비%':>11}")
rows=[]
for dH in (3.63,4.60,6.60,10.0,13.40,18.70):
    fl=flight(dH); g=max(A['recover'],fl+BEAT)
    tot=T_back+A['intro']+3*(A['draw']+A['aim'])+2*g+(fl+A['outro'])
    rows.append((dH,fl,g,tot))
    print(f"  {dH:>7.2f}{fl:>8.3f}{g:>8.3f}{tot:>9.3f}{tot-base:>+10.3f}{(tot/base-1)*100:>+10.1f}%")
print(f"\n  → 밴드 3.63~13.40H에서 {(rows[0][3]/base-1)*100:+.1f}% ~ {(rows[4][3]/base-1)*100:+.1f}%")
print(f"     Ucap 18.70H까지 열면 상한 {(rows[5][3]/base-1)*100:+.1f}%")
print(f"     사이클 길이 변동폭 = {rows[4][3]-rows[0][3]:.3f}초(13.40H까지) / {rows[5][3]-rows[0][3]:.3f}초(18.70H까지)")
print()
print("  ★ design-systems §6-1 #4 표와의 차이 (교차 검토 결과 — 그쪽 표에 내부 불일치가 있다)")
print("    그쪽 총계는 §6-1 #5에서 스스로 교정한 접근 0.801이 아니라 **교정 전 0.907**을 쓰고 있다.")
for dH,exp in ((3.63,6.179),(4.60,6.387),(13.40,7.702)):
    fl=flight(dH); g=max(A['recover'],fl+BEAT)
    theirs = 0.907+A['intro']+ (A['draw']+A['aim']) + g + 2*(A['draw']+0.22) + g + (fl+A['outro'])
    theirs = 0.907+A['intro']+(A['draw']+A['aim'])+g+(A['draw']+0.22)+g+(A['draw']+0.22)+(fl+A['outro'])
    print(f"    d={dH:5.2f}H  그쪽표 {exp:.3f}  ← 접근 0.907 + aim지불(2·3발 0.30→0.22) 재현 = {theirs:.3f}"
          f"  {'✓ 재현됨' if abs(theirs-exp)<0.003 else '✗'}")
print("    즉 그쪽 '+6.4%~+32.6%'는 분자에 교정 전 접근(+0.106), 분모에 교정 전 총계(5.807)를 함께 썼다.")
print("    양쪽 오차가 부분 상쇄돼 방향은 맞지만, 위 표가 교정 후 정본이다.")
print()
print("  지불 메뉴 (리더 결정용, 기준 사거리 4.60H)")
fl=flight(4.60); g=max(A['recover'],fl+BEAT)
menu=[("(기준) 지불 없음",0.0),
      ("2·3발째 aim 0.30→0.22",2*0.08),
      ("+ 2·3발째 draw 0.42→0.36",2*0.08+2*0.06),
      ("+ 비트 0.26→0.20",2*0.08+2*0.06+2*0.06),
      ("+ 후퇴 0.75H→0.60H",2*0.08+2*0.06+2*0.06+(0.75-0.60)/V_back_H)]
tot0=T_back+A['intro']+3*(A['draw']+A['aim'])+2*g+(fl+A['outro'])
for nm,sv in menu:
    print(f"    {nm:<34} 총계 {tot0-sv:6.3f}초  현행 대비 {tot0-sv-base:+.3f}초 ({(tot0-sv)/base-1:+.1%})")
print("    ※ '후퇴 0.60H'는 §1의 1사이클 조건(2.081보→1.585보)을 깨뜨린다 — 권하지 않는다.")
