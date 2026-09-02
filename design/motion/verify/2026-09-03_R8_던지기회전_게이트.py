import math
BASE_H=2.2746944; H=BASE_H*0.75; G=29.43; VMAX=12.0; LEAD=0.1; CLEAN=1.2; SCR=24.0
def spin(v): return min(max(90*v/H,220),720)
n1=n2=n3=tot=tum=0; rev=0; sel2=0
for i in range(200):
    v=CLEAN*H+i*(VMAX-CLEAN*H)/199; s=spin(v)
    for j in range(60):
        th=-90+j*180/59
        for k in range(80):
            d=0.25+k*(SCR-0.25)/79
            vy=v*math.sin(math.radians(th)); t=(vy+math.sqrt(vy*vy+2*G*d))/G; u=t-LEAD; tot+=1
            if u<=0.0001: continue
            turns=max(1,int(round((s*u-360)/360))+1); delta=360*turns
            t0=turns
            while turns>1 and delta/u>720: turns-=1; delta=360*turns
            if delta/u>720: continue
            tum+=1
            if u>=1.0: n1+=1          # ②가 강제 2바퀴를 되돌리지 '않는' 구간
            if u>=1.5: n2+=1          # ②가 강제 3바퀴를 되돌리지 '않는' 구간
            if t0>=2: sel2+=1         # ①이 2 이상을 고른 표본
            if t0!=turns: rev+=1      # ②가 실제로 되돌린 표본
print(f"회전한 표본 {tum}개 중")
print(f"  usable >= 1.00초 (=②가 2바퀴를 허용) : {n1} ({100*n1/tum:.1f}%)")
print(f"  usable >= 1.50초 (=②가 3바퀴를 허용) : {n2} ({100*n2/tum:.1f}%)")
print(f"  ①이 2바퀴 이상을 고른 표본           : {sel2} ({100*sel2/tum:.1f}%)")
print(f"  ②가 실제로 그 선택을 되돌린 표본     : {rev} ({100*rev/tum:.1f}%)  <- ②가 '이긴' 비율")
print(f"  => ②가 되돌리지 못하고 ①의 선택이 그대로 화면에 나온 비율 = {100*(tum-rev)/tum:.1f}%")
import math
from collections import Counter
BASE_H=2.2746944; H=BASE_H*0.75; G=29.43; VMAX=12.0; LEAD=0.1; CLEAN=1.2; SCR=24.0
def run(PER_H,SPIN_MIN,SPIN_MAX):
    c=Counter(); over=0; tot=0
    for i in range(120):
        v=CLEAN*H+i*(VMAX-CLEAN*H)/119
        s=min(max(PER_H*v/H,SPIN_MIN),SPIN_MAX)
        for j in range(40):
            th=-90+j*180/39
            for k in range(50):
                d=0.25+k*(SCR-0.25)/49
                vy=v*math.sin(math.radians(th)); t=(vy+math.sqrt(vy*vy+2*G*d))/G; u=t-LEAD; tot+=1
                if u<=0.0001: c['Fall']+=1; continue
                turns=max(1,int(round((s*u-360)/360))+1); delta=360*turns
                while turns>1 and delta/u>SPIN_MAX: turns-=1; delta=360*turns
                if delta/u<=SPIN_MAX:
                    c[turns]+=1
                    if delta/u>720: over+=1
                else: c['Fall']+=1
    return c,tot,over
print("perHeight  minSpin maxSpin |  1바퀴   2바퀴   3바퀴   4+   Fall  | 계획ω>720인 비율")
for ph,mn,mx in [(90,220,720),(90,220,1080),(140,220,1080),(200,260,1440),(260,300,1800),(320,340,2200)]:
    c,tot,over=run(ph,mn,mx)
    four=sum(c[k] for k in c if isinstance(k,int) and k>=4)
    print(f"{ph:6}  {mn:7} {mx:7} | {100*c[1]/tot:5.1f}% {100*c[2]/tot:5.1f}% {100*c[3]/tot:5.1f}% {100*four/tot:4.1f}% {100*c['Fall']/tot:5.1f}% | {100*over/tot:5.1f}%")
