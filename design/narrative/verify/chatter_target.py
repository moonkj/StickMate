# -*- coding: utf-8 -*-
"""제안값 산출 — (1) 계약에서 간격을 유도 (2) 그 간격을 내는 설정값을 역산 (3) 분포 검사."""
import random, re, os, sys, io, math
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

SESSION=1380.0   # 민지 실측 관측창 23분

# ---------- 1. 계약 -> 허용 발화 수 (생일문제) ----------
def p_norepeat(N,k):
    p=1.0
    for i in range(k): p*= (N-i)/N
    return p
def max_draws(N, limit=0.5):
    """중복을 볼 확률이 limit 이하로 유지되는 최대 뽑기 수 k."""
    k=1
    while 1-p_norepeat(N,k+1) <= limit: k+=1
    return k

print("=== 1. 계약: '한 세션(23분)에 이미 본 문장을 다시 볼 확률 ≤ 50%' ===")
print("    (민지 판정 '그중 절반은 이미 본 문장입니다'를 그대로 계약으로 옮긴 것)")
print()
print("   풀 N | 허용 발화 k | 그때 중복확률 | 필요 간격 | 5분당 | 하루(24h)")
print("   -----|------------|-------------|----------|-------|--------")
for N in (13,16,18,20,24,26,30,34,40,55):
    k=max_draws(N)
    gap=SESSION/k
    print("   %4d | %10d | %10.1f%% | %6.0f초 | %5.2f | %6.0f회"%(N,k,(1-p_norepeat(N,k))*100,gap,300/gap,86400/gap))
print()
print("   ★ k는 √N로만 자란다 — 풀을 13->34(2.6배)로 키워도 허용 발화는 %d->%d(%.2f배)뿐이다."
      %(max_draws(13),max_draws(34),max_draws(34)/max_draws(13)))
print("   ★ 즉 **간격이 주 레버, 풀은 보조 레버**다. 풀만 키우면 문제가 안 풀린다.")
print()
print("   현행 검산: 풀 13 / 세션 발화 52회 -> 중복확률 %.4f%% (사실상 100%%)"%((1-p_norepeat(13,52))*100))
print()

# ---------- 2. 설정값 역산 ----------
def sim(p_idle,p_walk,cooldown,seconds,seed=1,climb_period=None,climb_shares_cd=True):
    rng=random.Random(seed)
    def jit(v): return v*(1.0+rng.uniform(-JIT,JIT))
    t=0.0; next_ok=-1e9; fires=[]; phase="idle"
    next_climb = rng.expovariate(1.0/climb_period) if climb_period else 1e18
    while t<seconds:
        dur = jit(rng.uniform(IDLE_MIN,IDLE_MAX)) if phase=="idle" else jit(rng.uniform(WALK_MIN,WALK_MAX))
        # 등반 사건이 이 구간 안에 있으면 먼저 처리
        while next_climb < t+dur:
            if (not climb_shares_cd) or next_climb>=next_ok:
                fires.append((next_climb,"climb"))
                if climb_shares_cd: next_ok=next_climb+cooldown
            next_climb += rng.expovariate(1.0/climb_period)
        p=p_idle if phase=="idle" else p_walk
        if t>=next_ok and rng.random()<p:
            fires.append((t,phase)); next_ok=t+cooldown
        t+=dur
        phase=("walk" if rng.random()<P_WALK else "idle") if phase=="idle" else "idle"
    fires.sort()
    return fires

CLIMB=138.0  # 실측: "영차..." 10회/1380초 (ParkourClimb는 확률·쿨다운이 없어 100% 발화)
H=86400*7    # 7일

print("=== 2. 설정값 역산 — 목표 간격 240초(풀 24줄 계약값) ===")
print("    자율 발화 전체 = 앰비언트(Idle/Walk) + 사건(ParkourClimb/LedgeHang)")
print()
print("   cooldown |  p_idle | p_walk | 총 간격 | 앰비언트 | 등반 | 최소간격 | 하위10% | 세션당")
print("   ---------|---------|--------|--------|---------|------|---------|--------|-------")
cands=[(11,0.28,0.14),(60,0.10,0.05),(120,0.08,0.04),(180,0.072,0.036),(180,0.05,0.025),
       (210,0.06,0.03),(240,0.05,0.025),(300,0.04,0.02)]
best=None
for cd,pi,pw in cands:
    f=sim(pi,pw,cd,H,seed=21,climb_period=CLIMB,climb_shares_cd=True)
    gaps=[f[i+1][0]-f[i][0] for i in range(len(f)-1)]
    amb=sum(1 for _,k in f if k in("idle","walk")); clm=sum(1 for _,k in f if k=="climb")
    tot=H/len(f)
    g=sorted(gaps)
    print("   %8.0f | %7.3f | %6.3f | %6.0f초 | %7.0f초 | %4.0f초 | %7.1f초 | %6.0f초 | %5.2f회"
          %(cd,pi,pw,tot,H/max(amb,1),H/max(clm,1),g[0],g[len(g)//10],SESSION/tot))
print()
print("   ★ 현행(11/0.28/0.14) 행의 '최소간격'을 보라 — 자율 발화 두 개가 그만큼 붙어서 나온다.")
print()
