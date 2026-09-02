# -*- coding: utf-8 -*-
"""운용점 확정 — 공유 쿨다운 + 소스별 확률. 노출 점유율까지 계산."""
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

# --- DialogueKind.cs 상수 (소스에서 읽는다) ---
src=open(os.path.join(ROOT,"Assets/_Project/Scripts/Dialogue/DialogueKind.cs"),encoding="utf-8").read()
def const(name):
    m=re.search(r"%s\s*=\s*([\d.]+)f"%name,src); return float(m.group(1))
BASE=const("BaseSeconds"); PER=const("PerGlyphSeconds"); MINS=const("MinSeconds"); MAXS=const("MaxSeconds")
POPIN=const("PopInSeconds"); FADEOUT=const("FadeOutSeconds"); FADEIN=const("FadeInSeconds"); READS=const("ReadsBeforeStale")
def reading(t): return min(max(BASE+len(t)*PER,MINS),MAXS)
def maxvis(t): return POPIN+READS*reading(t)+FADEOUT
def required(t): return FADEIN+reading(t)
# 교정
for t,exp in (("가뿐하네",0.680),("영차...",0.715),("헉... 높다",0.865)):
    assert abs(required(t)-exp)<1e-6, (t,required(t),exp)
print("[교정] ParkourClimbState.cs 주석의 알려진 값 3건 재현 OK (0.680/0.715/0.865)")

IDLE_POOL=["음...","여기 좋네","심심하다","잠깐 쉬는 중","오늘 뭐 하지","하암...","발밑이 단단해","구경 중이야"]
WALK_POOL=["산책 중","저쪽으로 가볼까","하나 둘 하나 둘","다리 좀 풀자","다리가 잘 나가네"]

def sim(p_idle,p_walk,p_climb,cd,seconds,seed=1):
    rng=random.Random(seed)
    def jit(v): return v*(1.0+rng.uniform(-JIT,JIT))
    t=0.0; next_ok=-1e9; fires=[]; phase="idle"; visible=0.0
    next_climb=rng.expovariate(1.0/CLIMB)
    while t<seconds:
        dur = jit(rng.uniform(IDLE_MIN,IDLE_MAX)) if phase=="idle" else jit(rng.uniform(WALK_MIN,WALK_MAX))
        while next_climb < t+dur:
            if next_climb>=next_ok and rng.random()<p_climb:
                fires.append((next_climb,"climb")); next_ok=next_climb+cd
                visible+=min(maxvis("영차..."),1.20)      # 등반 총 길이 1.20초에 잘린다
            next_climb+=rng.expovariate(1.0/CLIMB)
        pool = IDLE_POOL if phase=="idle" else WALK_POOL
        p = p_idle if phase=="idle" else p_walk
        if t>=next_ok and rng.random()<p:
            txt=pool[rng.randrange(len(pool))]
            if required(txt) <= dur:                      # 규칙 8 발화 자격 게이트
                fires.append((t,phase)); next_ok=t+cd
                visible+=min(maxvis(txt),dur)             # 서술은 상태 종료 시 즉시 컷
        t+=dur
        phase=("walk" if rng.random()<P_WALK else "idle") if phase=="idle" else "idle"
    fires.sort()
    return fires,visible

H=86400*7
print()
print("=== 운용점 후보 (공유 쿨다운 + 소스별 확률, 7일 시뮬) ===")
print("   쿨다운 | p_idle | p_walk | p_climb | 총간격 | 앰비언트/세션 | 사건/세션 | 최소간격 | 화면점유율 | 하루")
print("   -------|--------|--------|---------|-------|-------------|----------|---------|----------|------")
rows=[("현행",11,0.28,0.14,1.00),
      ("A",120,0.060,0.030,0.35),
      ("B",180,0.055,0.0275,0.30),
      ("C",180,0.045,0.0225,0.25),
      ("D",210,0.050,0.025,0.25),
      ("E",240,0.045,0.0225,0.22)]
for name,cd,pi,pw,pc in rows:
    f,vis=sim(pi,pw,pc,cd,H,seed=33)
    amb=sum(1 for _,k in f if k in("idle","walk")); clm=len(f)-amb
    gaps=sorted(f[i+1][0]-f[i][0] for i in range(len(f)-1))
    print("   %-6s | %6.3f | %6.4f | %7.2f | %5.0f초 | %11.2f회 | %8.2f회 | %6.0f초 | %7.3f%% | %4.0f회"
          %(("%s %d"%(name,cd)) if name=="현행" else "%s/%d"%(name,cd),
            pi,pw,pc,H/len(f),amb/(H/SESSION),clm/(H/SESSION),gaps[0],vis/H*100,86400/(H/len(f))))
print()
print("   화면점유율 = 말풍선이 화면에 떠 있는 시간의 비율.")
print()

# 최종 확정 후보 B로 검증
name,cd,pi,pw,pc="B",180,0.055,0.0275,0.30
print("=== 확정 권고 B 검증 (쿨다운 %d초 / p_idle %.3f / p_walk %.4f / p_climb %.2f) ==="%(cd,pi,pw,pc))
tot=0;amb=0;clm=0;sess=[]
for seed in range(1,41):
    f,vis=sim(pi,pw,pc,cd,SESSION,seed=seed)
    sess.append(len(f)); tot+=len(f)
    amb+=sum(1 for _,k in f if k in("idle","walk")); clm+=sum(1 for _,k in f if k=="climb")
import statistics
print("   23분 세션 40회 반복: 평균 %.2f회 (중앙값 %d, 최소 %d, 최대 %d)"%(tot/40,statistics.median(sess),min(sess),max(sess)))
print("   내역: 앰비언트 %.2f회 + 사건 %.2f회"%(amb/40,clm/40))

def p_norepeat(N,k):
    p=1.0
    for i in range(k): p*=(N-i)/N
    return p
for N in (13,20,24,26):
    ps=[1-p_norepeat(N,k) for k in sess]
    print("   풀 %2d줄일 때 세션 내 중복 확률 기대값: %.1f%%   %s"%(N,sum(ps)/len(ps)*100,
          "계약 충족(<=50%)" if sum(ps)/len(ps)<=0.5 else "계약 위반"))
