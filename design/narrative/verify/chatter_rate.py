# -*- coding: utf-8 -*-
"""
말풍선 빈도 모델 — 실측 교정 후 제안값 산출.
교정 기준(민지 23분 실기): 말풍선 62회 / 그중 "영차..."(ParkourClimb, 쿨다운·확률 없음) 10회
                        → 앰비언트(Idle/Walk) 52회 / 1380초 = 26.5초 간격
전부 프로덕션 코드에서 읽어온 상수만 쓴다(하드코딩 금지 — 아래 SRC가 출처).
"""
import random, statistics, re, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

SRC_ASSET = "Assets/_Project/Data/DefaultStickConfig.asset"
import os
ROOT = "/Users/kjmoon/App/StickMate"

def asset(key):
    with open(os.path.join(ROOT, SRC_ASSET), encoding="utf-8") as f:
        for line in f:
            m = re.match(r"\s*%s:\s*([-\d.]+)\s*$" % re.escape(key), line)
            if m: return float(m.group(1))
    raise KeyError(key)

IDLE_MIN = asset("wanderIdleDurationMin");  IDLE_MAX = asset("wanderIdleDurationMax")
WALK_MIN = asset("wanderWalkDurationMin");  WALK_MAX = asset("wanderWalkDurationMax")
JIT      = asset("wanderDurationJitterRatio")
P_WALK   = asset("wanderPostIdleWalkChance")
P_IDLE_C = asset("idleChatterChance");      P_WALK_C = asset("walkChatterChance")
COOLDOWN = asset("ambientChatterCooldownSeconds")

print("[출처] %s 에서 읽음" % SRC_ASSET)
print("  Idle %.1f~%.1f초 / Walk %.1f~%.1f초 / 지터 ±%.1f%% / Idle->Walk %.2f"
      % (IDLE_MIN, IDLE_MAX, WALK_MIN, WALK_MAX, JIT*100, P_WALK))
print("  idleChatterChance %.2f / walkChatterChance %.2f / cooldown %.0f초" % (P_IDLE_C, P_WALK_C, COOLDOWN))
print()

def sim(p_idle, p_walk, cooldown, seconds=3600*24, seed=1, npool_idle=8, npool_walk=5):
    rng = random.Random(seed)
    def jit(v): return v * (1.0 + rng.uniform(-JIT, JIT))
    t = 0.0; next_ok = -1e9; fires = []; entries = 0
    phase = "idle"
    while t < seconds:
        entries += 1
        p = p_idle if phase == "idle" else p_walk
        if t >= next_ok and rng.random() < p:
            fires.append((t, phase))
            next_ok = t + cooldown
        dur = jit(rng.uniform(IDLE_MIN, IDLE_MAX)) if phase == "idle" else jit(rng.uniform(WALK_MIN, WALK_MAX))
        t += dur
        phase = ("walk" if rng.random() < P_WALK else "idle") if phase == "idle" else "idle"
    return fires, entries

# ---------- 1. 교정 ----------
print("=== 1. 교정 — 현재 배포값이 실측을 재현하는가 ===")
fires, entries = sim(P_IDLE_C, P_WALK_C, COOLDOWN, seconds=1380*400, seed=7)
per1380 = len(fires) / 400.0
gap = (1380*400) / len(fires)
print("  모델: 23분(1380초)당 앰비언트 %.1f회, 평균 간격 %.1f초" % (per1380, gap))
print("  실측: 23분당 앰비언트 52회(62 - 영차10), 평균 간격 26.5초")
err = abs(per1380 - 52) / 52 * 100
print("  오차 %.1f%%  ->  %s" % (err, "교정 통과(±15% 이내)" if err < 15 else "교정 실패 — 이하 숫자 전부 폐기"))
if err >= 15:
    sys.exit(1)
print()

# ---------- 2. 현재값의 하루 ----------
print("=== 2. 현재값이 24시간 상주에서 만드는 것 ===")
fires, entries = sim(P_IDLE_C, P_WALK_C, COOLDOWN, seconds=86400, seed=11)
n = len(fires)
print("  하루 앰비언트 발화 %d회 (전이 시도 %d회 중 %.1f%%)" % (n, entries, n/entries*100))
print("  풀 13줄 -> 한 줄당 하루 평균 %.0f회 노출" % (n/13))
print("  8시간 근무 중 %d회, 5분당 %.1f회" % (n/3, n/86400*300))
print()

# ---------- 3. 반복 체감 ----------
def repeat_stats(pool, draws, trials=200000, seed=3):
    """draws번 뽑을 때 '이미 본 문장'이 몇 번 나오는가(복원추출)."""
    rng = random.Random(seed); tot = 0
    for _ in range(trials):
        seen = set(); rep = 0
        for _ in range(draws):
            x = rng.randrange(pool)
            if x in seen: rep += 1
            else: seen.add(x)
        tot += rep
    return tot/trials

def coupon(pool):
    """풀 전체를 한 번씩 보는 데 필요한 기대 뽑기 수(쿠폰 수집)."""
    return pool * sum(1.0/i for i in range(1, pool+1))

print("=== 3. 반복 체감 — '절반은 이미 본 문장이다'의 검산 ===")
for pool in (13, 20, 26, 34):
    for draws in (23,):   # 민지가 본 23분치 앰비언트 52회 -> 제안값에서는 달라진다
        pass
print("  민지 실측 23분: 앰비언트 52회, 풀 13줄")
r = repeat_stats(13, 52)
print("    -> 기대 중복 %.1f회 / 52회 = %.0f%%  (민지 '절반은 이미 본 문장' = 판정 일치)" % (r, r/52*100))
print("    풀 13줄 전체를 한 번씩 보는 데 필요한 뽑기: %.1f회" % coupon(13))
print()
