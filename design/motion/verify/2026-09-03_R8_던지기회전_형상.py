import math
# 제안: 회전율 프로파일 r(u) = 1 + A*cos(pi*u),  u = 계획 이후 경과/usable
# 성질: int_0^1 r du = 1  -> 총 회전각 = ωbar*usable = delta  (정수 바퀴 계약 무손상)
# 기존 컨트롤러(remaining/timeLeft)에 곱할 계수 K(u) = r(u)*(1-u)/rho(u), rho = 1-u-(A/pi)sin(pi u)
def K(u,A):
    if u>=1-1e-9: return 1.0
    rho = 1-u-(A/math.pi)*math.sin(math.pi*u)
    return (1+A*math.cos(math.pi*u))*(1-u)/rho
print("A     u=0.00  0.10  0.25  0.50  0.75  0.90  1.00   |  최대K  (정렬 상한 factor=1.6 대비)")
for A in (0.0,0.15,0.30,0.45,0.55,0.60):
    vals=[K(u,A) for u in (0,.1,.25,.5,.75,.9,1.0)]
    mx=max(K(i/2000,A) for i in range(2001))
    print(f"{A:.2f} "+" ".join(f"{v:6.3f}" for v in vals)+f"   |  {mx:.3f}  {'OK' if mx<=1.6 else '초과!'}")
print()
print("적분 검산 — 실제로 delta에 정확히 도달하는가 (수치적분, usable=1.0, ωbar=360):")
for A in (0.0,0.30,0.55):
    N=200000; s=0.0
    for i in range(N):
        u=(i+0.5)/N; s+=360*(1+A*math.cos(math.pi*u))*(1.0/N)
    print(f"   A={A:.2f} -> 총 회전 {s:.6f}도 (목표 360.000000, 오차 {s-360:+.2e})")
print()
BASE_H=2.2746944; H=BASE_H*0.75; VMAX=12.0; CLEAN=1.2
def s01(v): return max(0.0,min(1.0,(v/H-CLEAN)/(VMAX/H-CLEAN)))
print("던지기 세기 -> A -> 회전율 (usable=1.0초, 1바퀴 계획 = 평균 360도/초 기준)")
print(" v(u/s)  v(H/s)  s01    A     시작ω    끝ω    시작:끝   첫1/4바퀴  마지막1/4바퀴")
for v in (2.05,3.5,5.0,6.5,8.0,9.5,11.0,12.0):
    A=0.55*s01(v); w0=360*(1+A); w1=360*(1-A)
    # 첫 90도에 걸리는 시간 / 마지막 90도
    def tfor(target):
        N=200000; acc=0.0
        for i in range(N):
            u=(i+0.5)/N; acc+=360*(1+A*math.cos(math.pi*u))*(1.0/N)
            if acc>=target: return (i+1)/N
        return 1.0
    t25=tfor(90); t75=tfor(270)
    print(f" {v:5.2f}  {v/H:5.2f}  {s01(v):.3f}  {A:.3f}  {w0:6.1f}  {w1:6.1f}   {w0/w1:4.2f}:1     {t25:.3f}초    {1-t75:.3f}초")
import math
BASE_H=2.2746944; H=BASE_H*0.75; VMAX=12.0; CLEAN=1.2; AMAX=0.45; FACTOR=1.6
def K(u,A):
    if u>=1-1e-9: return 1.0
    return (1+A*math.cos(math.pi*u))*(1-u)/(1-u-(A/math.pi)*math.sin(math.pi*u))
print(f"A_max={AMAX} -> max K = {max(K(i/5000,AMAX) for i in range(5001)):.4f}  (기존 정렬 상한 factor={FACTOR}, 여유 {100*(FACTOR/max(K(i/5000,AMAX) for i in range(5001))-1):.1f}%)")
def s01(v): return max(0.0,min(1.0,(v/H-CLEAN)/(VMAX/H-CLEAN)))
print()
print("표 3 — 던지기 세기 -> 회전율 프로파일 (A = 0.45 x s01), usable=1.00초·1바퀴 계획 기준")
print(" v(u/s) v(H/s)  s01     A   시작ω   중간ω   끝ω   시작:끝  |  웅크림 배율  엉덩이 무릎  어깨  팔꿈치 벌림")
for v in (2.05,4.0,6.0,8.0,10.0,12.0):
    s=s01(v); A=AMAX*s
    sc=0.65+(1.25-0.65)*s
    print(f" {v:5.2f}  {v/H:5.2f}  {s:.3f} {A:.3f} {360*(1+A):6.1f} {360.0:6.1f} {360*(1-A):6.1f}  {(1+A)/(1-A):5.2f}:1 |"
          f"  x{sc:.3f}     {76*sc:5.1f} {104*sc:5.1f} {46*sc:5.1f} {96*sc:6.1f} {9*(1.33-(1.33-0.78)*s):5.1f}")
print()
print("표 4 — 박자표 (usable 1.00초 / 1바퀴). 각 국면이 소비하는 회전각과 시간")
for label,v in (("약(2.05)",2.05),("중(7.0)",7.0),("강(12.0)",12.0)):
    A=AMAX*s01(v)
    # 시간 u에서의 누적각
    N=100000
    acc=0.0; marks={}
    for i in range(N):
        u=(i+0.5)/N; acc+=360*(1+A*math.cos(math.pi*u))/N
        for deg in (90,180,270,360):
            if deg not in marks and acc>=deg: marks[deg]=(i+1)/N
    marks.setdefault(360,1.0)
    q=[marks[90],marks[180]-marks[90],marks[270]-marks[180],marks[360]-marks[270]]
    print(f" {label:9s} A={A:.3f} | 1/4바퀴별 소요: {q[0]:.3f} {q[1]:.3f} {q[2]:.3f} {q[3]:.3f} 초"
          f"   (마지막/처음 = {q[3]/q[0]:.2f}배)")
print()
print("착지 준비(마지막 90도) 지속시간:")
for label,v in (("약",2.05),("중",7.0),("강",12.0)):
    A=AMAX*s01(v); N=100000; acc=0.0; t270=1.0
    for i in range(N):
        u=(i+0.5)/N; acc+=360*(1+A*math.cos(math.pi*u))/N
        if acc>=270: t270=(i+1)/N; break
    print(f"   {label}: {1-t270:.3f}초  (오늘 = 0.250초 고정)")
