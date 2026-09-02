# ============================================================================
# ★ 교정 블록 — 알려진 독립 값으로 먼저 맞춘다. 깨지면 아래 숫자를 전부 폐기하라.
#   (CLAUDE.md "계산기를 만들면 알려진 값으로 먼저 교정한다")
# ============================================================================
import math as _m
def _cal():
    H=2.2746944*0.75
    assert abs(H-1.7060208)<1e-6, H                                  # 신장 (소스 상수 곱)
    assert abs(90*12.0/H-633.05)<0.01, 90*12.0/H                     # design-narrative 독립값 633.1
    R=(2.2746944-0.22)*0.75-0.9346944*0.75
    assert abs(R-0.8400)<1e-3, R                                     # design-narrative 독립값 0.840
    assert abs(0.5*29.43*0.68**2-6.804)<1e-3                         # design-narrative 독립값 6.804
    assert abs(R*_m.radians(633.05)*(982/24)-380)<1.0                # narrative 독립값 380 pt/s
    print("[교정] 4개 독립값 일치 (H 1.7060208 / 633.05 / R 0.840 / 6.804 / 380pt/s)")
_cal()

import math
from collections import Counter

# ── 출하 상수 (전부 소스/에셋에서 인용) ───────────────────────────────
BASE_H   = 2.2746944      # StickConfig.BaselineCharacterTotalHeight:1816
CSCALE   = 0.75           # DefaultStickConfig.asset:248 characterScale
H        = BASE_H*CSCALE  # 1.7060208
G        = 9.81*3.0       # Physics2DSettings m_Gravity.y  x  StickConfig.gravityScale(asset:29)
VMAX     = 12.0           # dragThrowMaxSpeed (asset:124)
PER_H    = 90.0           # throwTumbleDegreesPerHeightSpeed (asset:283)
SPIN_MIN = 220.0          # throwTumbleMinSpinDegreesPerSecond (asset:284)
SPIN_MAX = 720.0          # throwTumbleMaxSpinDegreesPerSecond (asset:285)
LEAD     = 0.1            # throwTumbleAlignLeadSeconds (asset:286)
CLEAN_MIN= 1.2            # throwTumbleMinSpeedHeightsPerSecond (asset:282)  [H/s]
SCREEN_H = 24.0           # 카메라 orthographicSize 12 x 2  (DockGeometry.cs:29)

def spin_speed(v):
    return min(max(PER_H*(v/H), SPIN_MIN), max(SPIN_MIN, SPIN_MAX))

def round_to_int(x):           # Unity Mathf.RoundToInt == (int)Math.Round -> banker's
    return int(round(x))

def plan(v, theta_deg, d):
    """returns (outcome, turns, usable, spin_planned) ; outcome in {'FALL','TUMBLE'}"""
    if v/H < CLEAN_MIN: return ('NOT_CLEAN',0,0,0)
    vy = v*math.sin(math.radians(theta_deg))
    disc = vy*vy + 2*G*d
    t = (vy + math.sqrt(disc))/G
    usable = t - LEAD
    s = spin_speed(v)
    if usable <= 0.0001: return ('FALL',0,usable,s)
    ideal = s*usable
    to_next = 360.0                      # _angle==0 at plan time -> RemainingDegreesToUpright()==0 -> 360
    turns = max(1, round_to_int((ideal - to_next)/360.0) + 1)
    delta = to_next + 360.0*(turns-1)
    clamped = False
    while turns > 1 and delta/usable > SPIN_MAX:
        turns -= 1; delta = to_next + 360.0*(turns-1); clamped = True
    if delta/usable <= SPIN_MAX:
        return ('TUMBLE', turns, usable, delta/usable, clamped)
    return ('FALL', 0, usable, s, clamped)

print(f"H={H:.6f}  G={G}  자연회전속도 범위 = {spin_speed(CLEAN_MIN*H):.1f} .. {spin_speed(VMAX):.2f} deg/s")
print(f"상한 720에 닿는 던지기 속도 = {720*H/PER_H:.3f} u/s  (상한 {VMAX}) -> {'도달가능' if 720*H/PER_H<=VMAX else '도달 불가'}")
print()

# ── 표 1: 속도 x 낙차 격자 (수평 던지기, theta=0) ─────────────────────
speeds  = [2.05,3.0,4.17,5.0,6.0,7.0,8.0,9.0,10.0,10.5,11.0,12.0]
drops   = [1,2,3,4,6,8,10,12,16,20,24]
print("표 1 — 수평 던지기(θ=0). 셀 = 계획된 바퀴 수 (F=회전없이 Fall, *=속도클램프 발동)")
print("낙차(u):".ljust(10)+ "".join(f"{d:>6}" for d in drops))
for v in speeds:
    row=[]
    for d in drops:
        r = plan(v,0,d)
        if r[0]=='TUMBLE':
            row.append(f"{r[1]}{'*' if r[4] else ''}")
        else: row.append('F')
    print((f"v={v:5.2f}").ljust(10)+"".join(f"{c:>6}" for c in row))
print()

# ── 표 2: 각도별 (v=12 최대) ────────────────────────────────────────
print("표 2 — 최대 세기(v=12 u/s)에서 던진 각도별. 셀 = 바퀴 수")
angles=[-60,-30,0,30,60,90]
print("낙차(u):".ljust(10)+ "".join(f"{d:>6}" for d in drops))
for th in angles:
    row=[]
    for d in drops:
        r=plan(12.0,th,d)
        row.append(f"{r[1]}{'*' if r[0]=='TUMBLE' and r[4] else ''}" if r[0]=='TUMBLE' else 'F')
    print((f"θ={th:+3d}°").ljust(10)+"".join(f"{c:>6}" for c in row))
print()

# ── 전수 몬테카를로(균등 격자) ──────────────────────────────────────
cnt=Counter(); clampfire=0; tot=0; turns_by_speed={}
for v in [CLEAN_MIN*H + i*(VMAX-CLEAN_MIN*H)/199 for i in range(200)]:
    for th in [-90+ i*180/59 for i in range(60)]:
        for d in [0.25 + i*(SCREEN_H-0.25)/79 for i in range(80)]:
            r=plan(v,th,d); tot+=1
            if r[0]=='TUMBLE':
                cnt[r[1]]+=1
                if r[4]: clampfire+=1
                turns_by_speed.setdefault(round(v/H,0),Counter())[r[1]]+=1
            else: cnt['Fall']+=1
print(f"전수 격자 {tot}점 (속도 200 x 각도 60 x 낙차 80, 균등):")
for k in sorted(cnt, key=lambda x:(isinstance(x,str),x)):
    print(f"   {k}: {cnt[k]:6d}  ({100*cnt[k]/tot:5.2f}%)")
print(f"   속도클램프(while)가 실제로 발동한 표본: {clampfire} ({100*clampfire/tot:.3f}%)")
import math
from collections import Counter
BASE_H=2.2746944; CSCALE=0.75; H=BASE_H*CSCALE; G=29.43; VMAX=12.0
PER_H=90.0; SPIN_MIN=220.0; SPIN_MAX=720.0; LEAD=0.1; CLEAN_MIN=1.2; SCREEN_H=24.0
PT_PER_UNIT_982=982.0/24.0     # 화면 982pt 기준 (StickConfig:3109 "1유닛≈40.9pt")
HIP=0.9346944*CSCALE; HEADC=(BASE_H-0.22)*CSCALE; R=HEADC-HIP
FADE_IN=0.06; READ_MIN=0.62    # DialogueKind.MinSeconds / DialogueTiming 팝인 60ms
def spin(v): return min(max(PER_H*v/H,SPIN_MIN),SPIN_MAX)
def plan(v,th,d):
    vy=v*math.sin(math.radians(th)); t=(vy+math.sqrt(vy*vy+2*G*d))/G; u=t-LEAD; s=spin(v)
    if u<=0.0001: return ('FALL',0,t,0)
    turns=max(1,int(round((s*u-360.0)/360.0))+1); delta=360.0*turns
    while turns>1 and delta/u>SPIN_MAX: turns-=1; delta=360.0*turns
    if delta/u<=SPIN_MAX: return ('TUMBLE',turns,t,delta/u)
    return ('FALL',0,t,0)
print(f"엉덩이 {HIP:.4f}u / 머리중심 {HEADC:.4f}u / 궤도반경 R={R:.4f}u  (narrative 0.840 대조)")
print(f"1유닛 = {PT_PER_UNIT_982:.2f} OS-pt (화면 982pt)")
print(f"자연 회전속도 {SPIN_MIN:.0f}~{spin(VMAX):.2f} deg/s -> 머리 접선속도 "
      f"{R*math.radians(SPIN_MIN)*PT_PER_UNIT_982:.0f}~{R*math.radians(spin(VMAX))*PT_PER_UNIT_982:.0f} pt/s")
print(f"★ 그러나 실제 적용되는 것은 **계획 각속도** delta/usable (218~720) + 정렬 상한 x1.6 (=1152):")
for w in (218,360,720,1152):
    print(f"    {w:5.0f} deg/s -> 머리 {R*math.radians(w)*PT_PER_UNIT_982:6.0f} pt/s , 한 바퀴 {360/w:.3f}초")
print()
print(f"플랜 성립 하한: delta>=360 & delta/usable<=720 -> usable>=0.500초 -> **비행 {LEAD+0.5:.2f}초 미만은 회전 없음**")
print(f"필요 체류(최단 대사) = 팝인 {FADE_IN} + 가독예산 {READ_MIN} = {FADE_IN+READ_MIN:.2f}초")
print(f"자유낙하로 {FADE_IN+READ_MIN:.2f}초를 벌려면 낙차 = {0.5*G*(FADE_IN+READ_MIN)**2:.3f}u = {0.5*G*(FADE_IN+READ_MIN)**2/H:.2f}신장")
print()
cnt=Counter(); dur=[]; short_frames=0; tot=0; ok_dwell=0
for i in range(200):
    v=CLEAN_MIN*H+i*(VMAX-CLEAN_MIN*H)/199
    for j in range(60):
        th=-90+j*180/59
        for k in range(80):
            d=0.25+k*(SCREEN_H-0.25)/79
            o,turns,t,w=plan(v,th,d); tot+=1
            if o=='TUMBLE':
                cnt[turns]+=1; dur.append(t)
                if t>=FADE_IN+READ_MIN: ok_dwell+=1
            else: cnt['Fall(1프레임)']+=1
print(f"전수 격자 {tot}점 — ThrowTumble **진입 후** 결과:")
for k in sorted(cnt,key=lambda x:(isinstance(x,str),x)):
    print(f"   {k}: {cnt[k]:7d} ({100*cnt[k]/tot:5.2f}%)")
tum=sum(cnt[k] for k in (1,2,3))
print(f"   회전한 표본 중 1바퀴 비중 = {100*cnt[1]/tum:.1f}%  / 2바퀴 {100*cnt[2]/tum:.1f}% / 3바퀴 {100*cnt[3]/tum:.2f}%")
dur.sort()
def pc(p): return dur[int(p*len(dur))-1]
print(f"   회전한 표본의 상태 지속(초): 최소 {dur[0]:.3f} / 25% {pc(.25):.3f} / 중앙 {pc(.5):.3f} / 75% {pc(.75):.3f} / 최대 {dur[-1]:.3f}")
print(f"   필요체류 {FADE_IN+READ_MIN:.2f}초를 채우는 비율 = 전체의 {100*ok_dwell/tot:.1f}% (회전한 것 중 {100*ok_dwell/tum:.1f}%)")
