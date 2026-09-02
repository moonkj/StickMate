# -*- coding: utf-8 -*-
"""R2 운용점(B/180, 총간격 244초)이 **정정된 계약**(N=10 -> k=4 -> 345초)을 지키는가.
chatter_solve.py의 시뮬레이터를 그대로 쓰되, 최악 자격 조합에서 다시 잰다."""
import random, re, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
ROOT="/Users/kjmoon/App/StickMate"; ASSET="Assets/_Project/Data/DefaultStickConfig.asset"
def asset(k):
    for line in open(os.path.join(ROOT,ASSET),encoding="utf-8"):
        m=re.match(r"\s*%s:\s*([-\d.]+)\s*$"%re.escape(k),line)
        if m: return float(m.group(1))
    raise KeyError(k)
IDLE_MIN,IDLE_MAX=asset("wanderIdleDurationMin"),asset("wanderIdleDurationMax")
WALK_MIN,WALK_MAX=asset("wanderWalkDurationMin"),asset("wanderWalkDurationMax")
JIT,P_WALK=asset("wanderDurationJitterRatio"),asset("wanderPostIdleWalkChance")
SESSION=1380.0; CLIMB=138.0
src=open(os.path.join(ROOT,"Assets/_Project/Scripts/Dialogue/DialogueKind.cs"),encoding="utf-8").read()
def const(n):
    m=re.search(r"%s\s*=\s*([\d.]+)f"%n,src); return float(m.group(1))
BASE,PER,MINS,MAXS=const("BaseSeconds"),const("PerGlyphSeconds"),const("MinSeconds"),const("MaxSeconds")
FADEIN=const("FadeInSeconds")
def reading(t): return min(max(BASE+len(t)*PER,MINS),MAXS)
def required(t): return FADEIN+reading(t)
for t,exp in (("가뿐하네",0.680),("영차...",0.715),("헉... 높다",0.865)):
    assert abs(required(t)-exp)<1e-6,(t,required(t),exp)
print("[교정] ParkourClimbState.cs 주석 값 3건 재현 OK — chatter_solve.py와 같은 계산기다")

# ★ 최악 자격 조합(화·수·목 / 시간대 밖 / 모션 미발동)의 실제 뽑기 풀 = 상시 10줄
IDLE_POOL=["음...","여기 좋네","잠깐 쉬는 중","오늘 뭐 하지","발밑이 단단해"]
WALK_POOL=["산책 중","저쪽으로 가볼까","하나 둘 하나 둘","다리 좀 풀자","다리가 잘 나가네"]

def sim(p_idle,p_walk,p_climb,cd,seconds,seed=1):
    rng=random.Random(seed)
    def jit(v): return v*(1.0+rng.uniform(-JIT,JIT))
    t=0.0; next_ok=-1e9; fires=[]; phase="idle"
    next_climb=rng.expovariate(1.0/CLIMB)
    while t<seconds:
        dur = jit(rng.uniform(IDLE_MIN,IDLE_MAX)) if phase=="idle" else jit(rng.uniform(WALK_MIN,WALK_MAX))
        while next_climb<t+dur:
            if next_climb>=next_ok and rng.random()<p_climb:
                fires.append(next_climb); next_ok=next_climb+cd
            next_climb+=rng.expovariate(1.0/CLIMB)
        pool=IDLE_POOL if phase=="idle" else WALK_POOL
        p=p_idle if phase=="idle" else p_walk
        if t>=next_ok and rng.random()<p:
            txt=pool[rng.randrange(len(pool))]
            if required(txt)<=dur:
                fires.append(t); next_ok=t+cd
        t+=dur
        phase=("walk" if rng.random()<P_WALK else "idle") if phase=="idle" else "idle"
    return fires

H=86400*7
CAND=[("R2 확정권고 B/180",0.055,0.0275,0.30,180),
      ("E/240",0.045,0.0225,0.22,240),
      ("F/300",0.040,0.0200,0.20,300),
      ("G/300",0.035,0.0175,0.16,300),
      ("H/360",0.035,0.0175,0.15,360)]
print("\n=== 정정된 계약: 최악 자격 N=10 -> k=4 -> 평균간격 >= 345.0초 ===")
print("   운용점              | p_idle | p_walk | p_climb | 쿨다운 | 총간격 | 세션 발화 | 판정")
best=None
for nm,pi,pw,pc,cd in CAND:
    f=sim(pi,pw,pc,cd,H)
    gap=H/len(f); per=len(f)*SESSION/H
    ok = gap>=345.0
    if ok and best is None: best=(nm,pi,pw,pc,cd,gap,per)
    print("   %-19s| %6.3f | %6.4f | %7.2f | %5d초 | %5.0f초 | %8.2f회 | %s"
          %(nm,pi,pw,pc,cd,gap,per,"충족 ✔" if ok else "★ 위반 (k=%.1f > 4)"%per))
print("\n   ★ R2가 확정 권고한 B/180(244초)은 정정된 계약을 **위반한다**.")
if best: print("   -> 최소 변경 통과안: %s (총간격 %.0f초, 세션 %.2f회)"%(best[0],best[5],best[6]))
print("\n   ※ 이 표는 '얼마나 자주 말하는가'(R2 답①) 소관이다. 이번 라운드는 **계약이 바뀌었다는")
print("     사실과 그 크기**만 보고하고, 최종 운용점은 리더가 R2 산출물과 함께 판정할 몫이다.")
