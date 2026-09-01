# -*- coding: utf-8 -*-
"""
Dock 단차 착지 — 배율 축 전수 계산 (design-motion R3, 2026-09-02)
환산자는 전부 MOTION_SPEC 16-0 표를 그대로 쓴다.
"""
import math
BASE=2.2746944
L1=0.21981; L2=0.19783; HIP=0.41091     # 허벅지/정강이/엉덩이높이 (H 배수, 프리팹 유도)
HEADD=0.44/BASE                          # 머리 지름(반지름 0.22 world @s=1) = 0.19343 H
PT=40.9167
DOCK=1.6375
MINS, MAXS = 0.35, 1.00

soft_t=0.35; react=0.88; deep=3.02; brace=7.10
softD=0.08; minD=0.45
softDur=0.14; shallowDur=0.32; deepDur=0.62; braceDur=0.88
pitchS=6.0; pitchD=22.0; pitchB=30.0
dustFull=3.0; dustMin=0.45

def drop(h,k): return L1*math.cos(math.radians(h))+L2*math.cos(math.radians(h-k))
STAND=max(drop(12,4),drop(-12,4))

def pose(d):
    fh=12+70*d; fk=4+122*d; rh=-12-28*d; rk=4+51*d
    return fk, STAND-max(drop(fh,fk),drop(rh,rk))

def ev(hH):
    t0=max(0,min(1,(hH-soft_t)/(react-soft_t)))
    t =max(0,min(1,(hH-react)/deep))
    u =max(0,min(1,(hH-(react+deep))/brace))
    soft=hH<react
    d=(softD+(minD-softD)*t0) if soft else (minD+(1-minD)*t)
    dur=(softDur+(shallowDur-softDur)*t0) if soft else (shallowDur+(deepDur-shallowDur)*t+u*(braceDur-deepDur))
    pit=pitchS+(pitchD-pitchS)*max(t0*0.35,t)+u*max(0,pitchB-pitchD)
    dust=(dustMin*t0) if soft else (dustMin+(1-dustMin)*max(0,min(1,(hH-react)/dustFull)))
    return d,dur,pit,dust

print("검산  서 있는 엉덩이 높이 = %.5f H  (16-0 표 0.41091)"%STAND)
print("검산  교차 배율 T1 진입 = %.4f"%(DOCK/(react*BASE)))
print("검산  교차 배율 T0.5 진입 = %.4f  (>%.2f 이므로 Dock은 항상 최소 T0.5)"%(DOCK/(soft_t*BASE),MAXS))
print()
print("배율   H(pt)  Dock hH  티어      깊이  앞무릎  몸하강%H  /머리  ★몸하강pt  지속s  상체°  먼지")
for s in [1.00,0.95,0.90,0.85,0.8180,0.80,0.75,0.70,0.65,0.60,0.55,0.50,0.4493,0.45,0.40,0.35]:
    H=BASE*s; hH=DOCK/H
    d,dur,pit,dust=ev(hH); fk,bd=pose(d)
    tier="T0.5" if hH<react else ("T1얕" if hH<react+deep*0.5 else "T1깊")
    star="  <= 교차" if abs(s-0.8180)<1e-3 else ("  <= 매달리기 임계" if abs(s-0.4493)<1e-3 else "")
    print("%5.3f %6.1f %8.3f  %-6s %5.3f %6.1f %8.2f %6.1f %9.2f %6.3f %6.1f %5.2f%s"
          %(s,H*PT,hH,tier,d,fk,bd*100,bd/HEADD*100,bd*H*PT,dur,pit,dust,star))
print()
print("== 대조군: 옛 절대 임계(T0.5 0.7961유닛 / T1 2.0유닛)를 유지했다면 ==")
print("배율   Dock hH_eff  깊이  앞무릎  ★몸하강pt  지속s   먼지")
for s in [1.00,0.75,0.60,0.50,0.4493,0.35]:
    H=BASE*s
    r=2.0/H; so=0.35*BASE/H
    hH=DOCK/H
    t0=max(0,min(1,(hH-so)/max(1e-6,r-so))); t=max(0,min(1,(hH-r)/deep))
    soft=hH<r
    d=(softD+(minD-softD)*t0) if soft else (minD+(1-minD)*t)
    dur=(softDur+(shallowDur-softDur)*t0) if soft else (shallowDur+(deepDur-shallowDur)*t)
    dust=(dustMin*t0) if soft else dustMin
    fk,bd=pose(d)
    print("%5.3f %11.3f %6.3f %6.1f %10.2f %6.3f %6.2f"%(s,r,d,fk,bd*H*PT,dur,dust))
print()
print("== 부팅 낙하(매 실행마다 반드시 보이는 유일한 T3) 5.27 H ==")
for name,bs in [("옛 램프 2.60",2.60),("새 램프 7.10",7.10)]:
    hH=5.27; u=max(0,min(1,(hH-3.90)/bs))
    dur=deepDur+u*(braceDur-deepDur); pit=pitchD+u*(pitchB-pitchD)
    print("  %s : u=%.2f  지속=%.2f초  상체=%.1f도"%(name,u,dur,pit))
print()
print("== 버팀 램프 헤드룸(화면 최대 낙차 23.804유닛 = MOTION_SPEC 16-0) ==")
for s in [1.00,0.75,0.60,0.35]:
    H=BASE*s; hHmax=23.804/H
    for name,bs in [("2.60",2.60),("7.10",7.10)]:
        u=max(0,min(1,(hHmax-3.90)/bs))
        print("  s=%.2f  최대 %.2f H  램프 %s -> u_max=%.3f"%(s,hHmax,name,u))

# =============================================================================
# 추가: 실루엣 기준(몸 하강 12% H = 이 저장소가 "앉았다"로 정의한 유일한 수치)과
#       macOS Dock tilesize(16~128, 낙차 pt = tilesize + 18) 축을 함께 본다.
# =============================================================================
def bd_of_d(d):
    fh=12+70*d; fk=4+122*d; rh=-12-28*d; rk=4+51*d
    return STAND-max(drop(fh,fk),drop(rh,rk))

lo,hi=0.0,1.0
for _ in range(80):
    m=(lo+hi)/2
    if bd_of_d(m)<0.12: lo=m
    else: hi=m
D12=(lo+hi)/2
HH12=react+(D12-minD)/(1-minD)*deep          # 몸하강 12% H 가 되는 낙차(H 배수)
print()
print("== 실루엣 기준의 좌표 ==")
print("  '앉았다'(몸하강 12%%H)  = 깊이 %.4f = 앞무릎 %.1f도 = 낙차 %.3f H"%(D12,4+122*D12,HH12))
print("  '앞무릎 45도'          = 깊이 %.4f = 몸하강 %.2f%%H  <- 12%% 기준의 %.2f배 얕다"
      %((45-4)/122, bd_of_d((45-4)/122)*100, 0.12/bd_of_d((45-4)/122)))
print()
print("== Dock tilesize 축: 어느 tilesize부터 '진짜 앉는가' (낙차pt = tilesize + 18) ==")
print("  배율    앉기 시작 tilesize    T1(무릎앉아) 시작 tilesize   비고")
for s in [1.00,0.8180,0.75,0.60,0.50,0.4493,0.35]:
    t_sit  = HH12*BASE*s*PT-18
    t_t1   = react*BASE*s*PT-18
    note=""
    if t_sit<=49<=128: note="★ 이 개발 머신(49)에서 이미 앉는다"
    elif t_sit>128:    note="tilesize 상한(128)에서도 안 앉는다"
    print("  %5.3f  %14.1f      %14.1f          %s"%(s,t_sit,t_t1,note))
print("  (macOS 기본 tilesize=48, 이 개발 머신=49, 슬라이더 범위 16~128)")
