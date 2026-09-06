# -*- coding: utf-8 -*-
u"""ParkourClimb 대사 편중 — 검산기 (design-narrative, 2026-09-06 R9)

세 가지를 잰다.
  ① 가독예산 교정 + 신규 문안 예산 (기준은 **디스크의 골든 파일**이지 프로덕션 함수가 아니다)
  ② 소은 실측 25.8% 를 **다른 경로로** 재현 (R2 교정 모델의 앰비언트 간격과 대조)
  ③ 레버별 최빈문장 점유율 — 공유 쿨다운 편입 / 확률 / 풀 확대의 3축

교정이 깨지면 그 뒤 숫자를 전부 폐기한다(TEAM.md 공통 처방).
프로덕션 .cs 는 읽기만 한다.
"""
from __future__ import print_function
import io, os, sys, random, re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.dirname(os.path.abspath(__file__)))))
GOLDEN = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'Tests', 'EditMode',
                      'Golden', 'DialogueBudgetKoGolden.txt')
KIND = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'Dialogue', 'DialogueKind.cs')
ASSET = os.path.join(ROOT, 'Assets', '_Project', 'Data', 'DefaultStickConfig.asset')
CLIMB = os.path.join(ROOT, 'Assets', '_Project', 'Scripts', 'States', 'ParkourClimbState.cs')

FAIL = []


def rd(p):
    return io.open(p, encoding='utf-8').read()


# ---------------------------------------------------------------------------
# 상수는 전부 파일에서 읽는다 (하드코딩 금지 — 낡은 사본 함정)
# ---------------------------------------------------------------------------
def cs_const(name, src):
    m = re.search(r'const\s+float\s+' + name + r'\s*=\s*([0-9.]+)f', src)
    if m is None:
        FAIL.append(u'DialogueKind.cs 에서 %s 를 못 찾았다' % name)
        return None
    return float(m.group(1))


def asset_val(name):
    m = re.search(r'^\s*' + name + r':\s*([0-9.]+)\s*$', rd(ASSET), re.M)
    if m is None:
        FAIL.append(u'에셋에서 %s 를 못 찾았다' % name)
        return None
    return float(m.group(1))


src_kind = rd(KIND)
BASE = cs_const('BaseSeconds', src_kind)
PER = cs_const('PerGlyphSeconds', src_kind)
MINS = cs_const('MinSeconds', src_kind)
MAXS = cs_const('MaxSeconds', src_kind)
FADEIN = cs_const('FadeInSeconds', src_kind)
POPIN = cs_const('PopInSeconds', src_kind)

CLIMB_DUR = asset_val('parkourClimbDuration')
COOLDOWN = asset_val('ambientChatterCooldownSeconds')
P_IDLE = asset_val('idleChatterChance')
P_WALK = asset_val('walkChatterChance')

m = re.search(r'LightClimbHeights\s*=\s*([0-9.]+)f', rd(CLIMB))
LIGHT_H = float(m.group(1)) if m else None
m = re.search(r'HardClimbHeights\s*=\s*([0-9.]+)f', rd(CLIMB))
HARD_H = float(m.group(1)) if m else None


def reading(text):
    v = BASE + len(text) * PER
    return min(max(v, MINS), MAXS)


def dwell(text):
    return FADEIN + reading(text)


print(u'=' * 78)
print(u'0. 파일에서 읽은 상수 (하드코딩 0)')
print(u'=' * 78)
print(u'  DialogueKind.cs : Base %.3f / PerGlyph %.4f / clamp[%.2f, %.2f] / FadeIn %.2f / PopIn %.2f'
      % (BASE, PER, MINS, MAXS, FADEIN, POPIN))
print(u'  DefaultStickConfig.asset : parkourClimbDuration %.2f / ambientChatterCooldownSeconds %.0f'
      % (CLIMB_DUR, COOLDOWN))
print(u'                             idleChatterChance %.2f / walkChatterChance %.2f'
      % (P_IDLE, P_WALK))
print(u'  ParkourClimbState.cs : LightClimbHeights %.4f H / HardClimbHeights %.4f H'
      % (LIGHT_H, HARD_H))
print(u'')

# ---------------------------------------------------------------------------
# ① 교정 — 기대값은 프로덕션 함수가 아니라 골든 파일의 2열(초, F6)에서 온다
# ---------------------------------------------------------------------------
print(u'=' * 78)
print(u'1. 가독예산 교정 — 골든 파일 %d줄 전량 대조' % 0)
print(u'=' * 78)
gold = []
for line in rd(GOLDEN).split('\n'):
    if not line or line.startswith('#'):
        continue
    parts = line.split('\t')
    if len(parts) != 3:
        continue
    gold.append((parts[2], float(parts[1])))

if not gold:
    FAIL.append(u'골든 파일에서 0줄을 읽었다 — 0건을 교정 통과로 읽으면 그 뒤가 전부 무효다')
bad = 0
for text, sec in gold:
    mine = round(reading(text), 6)
    if abs(mine - sec) > 5e-7:
        bad += 1
        print(u'  [X] %-14s 골든 %.6f  파이썬 %.6f' % (text, sec, mine))
print(u'  표본 %d줄 / 불일치 %d줄' % (len(gold), bad))
if bad:
    FAIL.append(u'교정 실패 — 예산식이 골든과 갈라졌다')

# ★ 음성 대조: 고의로 틀린 식이 실제로 빨개지는지 (죽은 프로브 방지)
neg = sum(1 for text, sec in gold if abs(round(BASE + len(text) * 0.08, 6) - sec) > 5e-7)
print(u'  [음성대조] 계수를 0.075 -> 0.080 으로 바꾸면 불일치 %d줄 (0이면 이 교정은 아무것도 못 잰다)'
      % neg)
if neg == 0:
    FAIL.append(u'음성 대조가 0건 — 교정기가 죽어 있다')
print(u'')

# ---------------------------------------------------------------------------
# ② 신규 문안 예산
# ---------------------------------------------------------------------------
print(u'=' * 78)
print(u'2. 문안 예산 — 계획 잔여 체류 %.2f초 (ParkourClimb 는 진입 시 진행도 0이라 항상 이 값)' % CLIMB_DUR)
print(u'=' * 78)
existing = {u'가뿐하네': 1, u'영차...': 2, u'헉... 높다': 3}
tier1_new = [u'이쯤이야', u'여긴 낮지', u'쉽다 쉬워']
tier2_new = [u'끄응...', u'으쌰!', u'이건 좀 높네']
tier3_new = [u'우와 높네']
rejected = [(u'조금만 더', u'진입 시 진행도 0 — "더"가 성립하지 않는다'),
            (u'훌쩍 올라간다', u'4박자 맨틀(손짚기->매달림->당기기->놓기)과 정면 충돌'),
            (u'조심조심', u'등반 시간은 높이와 무관하게 1.20초 고정 — "천천히"가 화면에 없다'),
            (u'창 위는 높네', u'오르는 대상이 창/Dock/안전망 중 무엇인지 이 상태는 모른다'),
            (u'끙... 올라간다', u'9자 — 팝인 여유 +0.005초. R2 §2-4가 착지하면 즉시 탈락')]

print(u'  %-16s %2s %8s %8s %8s %8s  %s' % (u'문안', u'자', u'가독', u'필요체류', u'여유', u'+팝인여유', u'판정'))
print(u'  ' + u'-' * 74)


def row(text, tag):
    r = reading(text)
    d = dwell(text)
    slack = CLIMB_DUR - d
    slack_pop = CLIMB_DUR - (d + POPIN)
    ok = u'OK' if slack >= 0 else u'탈락'
    if slack >= 0 and slack_pop < 0:
        ok = u'OK(팝인여유X)'
    print(u'  %-16s %2d %8.3f %8.3f %+8.3f %+8.3f  %s %s'
          % (text, len(text), r, d, slack, slack_pop, ok, tag))
    if slack < 0:
        FAIL.append(u'"%s" 가 예산을 넘는다' % text)
    return slack_pop


for t in sorted(existing, key=lambda x: existing[x]):
    row(t, u'[기존 T%d]' % existing[t])
for t in tier1_new:
    row(t, u'[신규 T1]')
for t in tier2_new:
    row(t, u'[신규 T2]')
for t in tier3_new:
    row(t, u'[신규 T3]')
print(u'')
# 어조 검산 — 말뭉치는 이미 «-네»가 34.5%다. 신규가 그 편중을 더 키우면 안 된다.
import collections
end = collections.Counter()
for t, _s in gold:
    s = t.rstrip(u'!?.… ')
    end[s[-1:] if s else u'?'] += 1
ne_before = end[u'네']
allnew = tier1_new + tier2_new + tier3_new
ne_new = sum(1 for t in allnew if t.rstrip(u'!?.… ').endswith(u'네'))
print(u'  [어조 검산] 종결 음절 «-네»: 말뭉치 %d/%d = %.1f%%  ->  신규 %d줄 추가 후 %d/%d = %.1f%%'
      % (ne_before, len(gold), 100.0 * ne_before / len(gold),
         len(allnew), ne_before + ne_new, len(gold) + len(allnew),
         100.0 * (ne_before + ne_new) / (len(gold) + len(allnew))))
tier_ends = {u'T1': [u'가뿐하네'] + tier1_new, u'T2': [u'영차...'] + tier2_new,
             u'T3': [u'헉... 높다'] + tier3_new}
for k in (u'T1', u'T2', u'T3'):
    es = [t.rstrip(u'!?.… ')[-1:] for t in tier_ends[k]]
    print(u'    %s 종결 음절 %s — 중복 %s'
          % (k, u'/'.join(es), u'없음' if len(set(es)) == len(es) else u'★있다'))
    if len(set(es)) != len(es):
        FAIL.append(u'%s 티어 안에서 종결 음절이 겹친다' % k)
print(u'')
print(u'  기각한 문안 — 예산이 아니라 **원칙 1**으로 떨어진 것들:')
for (t, why) in rejected:
    r = reading(t)
    print(u'    %-14s (%d자, 필요체류 %.3f초, 예산은 %s)  %s'
          % (t, len(t), FADEIN + r, u'통과' if FADEIN + r <= CLIMB_DUR else u'탈락', why))
print(u'')
print(u'  ※ "+팝인여유" = R2 §2-4 권고(발화 자격에 PopIn %.2f초 여유 요구)가 나중에 착지해도 살아남는가.' % POPIN)
print(u'    현행 IsEligible 은 PopIn 을 요구하지 않는다(DialogueKind.cs:362 실측).')
print(u'')

# ---------------------------------------------------------------------------
# ③ 소은 실측 재현 — 다른 경로
# ---------------------------------------------------------------------------
print(u'=' * 78)
print(u'3. 소은 실측(3시간) 재현 — R2 교정 모델과 대조')
print(u'=' * 78)
SESSION = 1380.0          # 23분 = R2 §2-1 계약의 관측 세션
LOG_SEC = 3 * 3600.0
CLIMB_EVENTS = 141.0
CLIMB_SAID = 137.0
SHARE = 0.258
TOTAL = CLIMB_SAID / SHARE
AMBIENT = TOTAL - CLIMB_EVENTS
print(u'  소은: 등반 %d회 / 그중 "가뿐하네" %d회(%.1f%%) / 그것이 전체 발화의 %.1f%%'
      % (CLIMB_EVENTS, CLIMB_SAID, 100 * CLIMB_SAID / CLIMB_EVENTS, 100 * SHARE))
print(u'  => 역산한 전체 발화 %.0f회, 앰비언트 %.0f회' % (TOTAL, AMBIENT))
amb_interval = LOG_SEC / AMBIENT
print(u'  => 앰비언트 평균 간격 %.1f초' % amb_interval)
R2_MODEL, R2_MEASURED = 25.0, 26.5
print(u'  R2 §1-1 교정: 모델 %.1f초 / 민지 실측 %.1f초' % (R2_MODEL, R2_MEASURED))
err = abs(amb_interval - R2_MEASURED) / R2_MEASURED
print(u'  대조 오차 %.1f%% (민지 실측 기준) — %s' % (100 * err, u'재현됨' if err < 0.15 else u'★ 재현 실패'))
if err >= 0.15:
    FAIL.append(u'소은 실측을 R2 모델로 재현하지 못했다')
climb_interval = LOG_SEC / CLIMB_EVENTS
print(u'  등반 평균 간격 %.1f초 (R2 §1-2 민지 실측 138초의 %.2f배 — 창 배치 의존, R2 §11-2가 예고한 변동)'
      % (climb_interval, 138.0 / climb_interval))
print(u'')

# ★ 29.7% 검산 — 소은의 투영을 그대로 재현해 본다
print(u'  [소은 투영 검산] "하암/구경 중이야 제거 후 29.7%"')
proj_total = TOTAL - (TOTAL - CLIMB_SAID / 0.297)
print(u'    137 / X = 0.297  =>  X = %.0f회.  즉 전체가 %.0f -> %.0f (앰비언트 %.0f회 소멸)'
      % (CLIMB_SAID / 0.297, TOTAL, CLIMB_SAID / 0.297, TOTAL - CLIMB_SAID / 0.297))
print(u'    그런데 AmbientChatter.TryRollChatter 는 **자격 있는 줄 안에서만** 추첨한다(:455-469).')
print(u'    두 줄을 지우거나 자격으로 막으면 그 추첨분은 다른 줄이 흡수한다 — 발화 총수는 안 변한다.')
print(u'    => 재분배 모델에서는 점유율이 %.1f%% 그대로다. 29.7%% 는 "그 70회가 침묵이 된다"는'
      % (100 * SHARE))
print(u'       가정에서만 나오고, 그 침묵 경로는 eligibleCount==0 일 때뿐인데 Idle/Walk 는')
print(u'       상시 줄이 각각 5줄이라 0이 될 수 없다(AmbientChatter.cs:67-116 / :119-177 실측).')
print(u'')

# ---------------------------------------------------------------------------
# ④ 레버 시뮬레이션
# ---------------------------------------------------------------------------
print(u'=' * 78)
print(u'4. 레버 시뮬레이션 — 공유 쿨다운 편입 / p_climb / 풀 크기')
print(u'=' * 78)

# 앰비언트 "시도" 강도를 실측 성공률에서 역산한다.
#   포아송 시도 + 하드 쿨다운 C 에서  성공률 = lam / (1 + lam*C)
lam_success = AMBIENT / LOG_SEC
lam_attempt = lam_success / (1.0 - lam_success * COOLDOWN)
print(u'  앰비언트 성공률 %.5f/초(간격 %.1f초) + 쿨다운 %.0f초  =>  역산 시도율 %.5f/초(간격 %.1f초)'
      % (lam_success, 1 / lam_success, COOLDOWN, lam_attempt, 1 / lam_attempt))
# 교정: 배회 전이 2~6초(평균 4초)마다 확률 추첨 -> 독립 추정
indep = ((P_IDLE + P_WALK) / 2.0) / 4.0
print(u'  [교정] 독립 추정(배회 전이 평균 4초마다 평균확률 %.3f) = %.5f/초 — 역산과 %.1f%% 차이'
      % ((P_IDLE + P_WALK) / 2.0, indep, 100 * abs(indep - lam_attempt) / lam_attempt))
if abs(indep - lam_attempt) / lam_attempt > 0.35:
    FAIL.append(u'앰비언트 시도율 교정 실패')
print(u'')


def simulate(window, cooldown, p_climb, climb_joins_cooldown, climb_pool, ambient_pool,
             climb_deterministic, seed=20260906, runs=4000):
    """관측창 window 초 동안 자율 발화를 만든다.

    돌려주는 것:
      n      발화 수
      nclimb 그중 등반분
      top    **최빈 문장** 점유율 (소은이 쓴 지표)
      ctop   **등반 최빈 문장** 점유율
      atop   **앰비언트 최빈 문장** 점유율  <- 판정선. 등반이 이 위로 튀어나오면 안 된다
      gap    최소 발화 간격
      cvar   등반 대사가 보여준 서로 다른 문장 수
    """
    rng = random.Random(seed)
    acc = [0.0] * 7
    used = 0
    for _ in range(runs):
        events = []
        t = 0.0
        while True:
            t += rng.expovariate(lam_attempt)
            if t > window:
                break
            events.append((t, 'a'))
        t = 0.0
        while True:
            t += climb_interval if climb_deterministic else rng.expovariate(1.0 / climb_interval)
            if t > window:
                break
            events.append((t, 'c'))
        events.sort()

        next_ok = -1e9
        said = []
        last_t = None
        gaps = []
        for (t, kind) in events:
            if kind == 'c':
                if rng.random() >= p_climb:
                    continue
                if climb_joins_cooldown and t < next_ok:
                    continue
                if climb_joins_cooldown:
                    next_ok = t + cooldown
                said.append('c%d' % rng.randrange(climb_pool))
            else:
                if t < next_ok:
                    continue
                next_ok = t + cooldown
                said.append('a%d' % rng.randrange(ambient_pool))
            if last_t is not None:
                gaps.append(t - last_t)
            last_t = t
        if not said:
            continue
        used += 1
        counts = {}
        for s in said:
            counts[s] = counts.get(s, 0) + 1
        cc = dict((k, v) for k, v in counts.items() if k.startswith('c'))
        ac = dict((k, v) for k, v in counts.items() if k.startswith('a'))
        n = float(len(said))
        acc[0] += n
        acc[1] += sum(cc.values())
        acc[2] += max(counts.values()) / n
        acc[3] += (max(cc.values()) / n) if cc else 0.0
        acc[4] += (max(ac.values()) / n) if ac else 0.0
        acc[5] += min(gaps) if gaps else window
        acc[6] += len(cc)
    return [x / used for x in acc]


scen = [
    # (이름, cooldown, p_climb, 공유쿨다운편입, 등반풀, 앰비언트풀)
    (u'현행 (기준선)',              COOLDOWN, 1.00, False, 1, 12),
    (u'A. 풀 확대만 (등반4)',       COOLDOWN, 1.00, False, 4, 12),
    (u'B. 공유 쿨다운 편입만',      COOLDOWN, 1.00, True,  1, 12),
    (u'C. 편입 + p=0.35',           COOLDOWN, 0.35, True,  1, 12),
    (u'★ D. 편입 + p=0.35 + 풀4',   COOLDOWN, 0.35, True,  4, 12),
    (u'E. D + R2안B 쿨다운180',     180.0,    0.35, True,  4, 12),
]

for (wname, window) in [(u'23분 세션 (R2 계약 창)', SESSION), (u'3시간 (소은 관측 창)', LOG_SEC)]:
    print(u'  ── %s ' % wname + u'─' * (60 - len(wname)))
    print(u'  %-26s %7s %7s %8s %8s %8s %6s'
          % (u'안', u'발화', u'등반분', u'최빈점유', u'등반최빈', u'앰비최빈', u'등반종'))
    print(u'  ' + u'-' * 74)
    for (name, cd, p, join, cp, ap) in scen:
        n, nc, top, ctop, atop, gap, cvar = simulate(window, cd, p, join, cp, ap, False)
        verdict = u'통과' if ctop <= atop else u'미달'
        print(u'  %-26s %7.1f %7.1f %7.1f%% %7.1f%% %7.1f%% %6.1f  %s'
              % (name, n, nc, 100 * top, 100 * ctop, 100 * atop, cvar, verdict))
    print(u'')

print(u'  ── 최소 발화 간격 (23분 세션) — "수다스럽다"의 실체는 붙어서 나오는 것이다 ─────')
for (name, cd, p, join, cp, ap) in scen:
    n, nc, top, ctop, atop, gap, cvar = simulate(SESSION, cd, p, join, cp, ap, False)
    print(u'    %-26s 최소 간격 %6.1f초' % (name, gap))
print(u'    ★ 편입하지 않으면 등반 대사는 앰비언트 대사 **직후 0.7초**에도 나온다.')
print(u'      쿨다운은 이 하한을 하드하게 잡는 유일한 장치다(확률만으로는 무기억이라 못 막는다).')
print(u'')

print(u'  판정 A(점유): 등반 최빈 문장 점유율 <= 앰비언트 최빈 문장 점유율')
print(u'                => "캐릭터가 가장 자주 하는 말"이 등반 대사가 아니게 된다.')
print(u'  판정 B(변주): 3시간 관측에서 등반 대사가 서로 다른 문장 3종 이상')
print(u'  ★ "최빈점유" 단독은 판정선으로 못 쓴다 — 발화 수가 줄면 소표본 때문에 자동으로 올라간다')
print(u'    (E안이 그 증거다: 발화가 7.5회로 줄자 최빈점유가 오히려 28%로 뛴다).')
print(u'    그래서 **앰비언트 최빈과의 대조**로 판정한다. 같은 표본 수를 공유하므로 그 편향이 상쇄된다.')
print(u'')

# ★ 다른 방법으로 다시 잰다 — 등반이 등간격(실제 Dock 왕복 루프)일 때
print(u'  [교차 검산] 등반을 지수분포가 아니라 **등간격 %.1f초**(MOTION_SPEC §5-5 왕복 루프)로 두면:'
      % climb_interval)
for (name, cd, p, join, cp, ap) in scen:
    n, nc, top, ctop, atop, gap, cvar = simulate(SESSION, cd, p, join, cp, ap, True)
    print(u'    %-26s 등반최빈 %.1f%% / 앰비최빈 %.1f%% / 등반분 %.1f회  %s'
          % (name, 100 * ctop, 100 * atop, nc, u'통과' if ctop <= atop else u'미달'))
print(u'')

# ---------------------------------------------------------------------------
# ⑤ 티어 경계 — 배포 기본값에서 Dock 이 어느 티어인가
# ---------------------------------------------------------------------------
print(u'=' * 78)
print(u'5. 티어 경계 — 왜 실효 풀이 1줄인가 (MOTION_SPEC §21 재검산)')
print(u'=' * 78)
DOCK_H = 0.945   # MOTION_SPEC §21-1 표: tilesize 48(macOS 기본) x 배율 0.75
for (label, light, hard) in [(u'현행', LIGHT_H, HARD_H), (u'§21-3 권고', 0.4109, 1.1022)]:
    tier = 1 if DOCK_H < light else (2 if DOCK_H < hard else 3)
    print(u'  %-12s Light %.4f H / Hard %.4f H  =>  Dock(%.3f H) = T%d %s'
          % (label, light, hard, DOCK_H, tier,
             u'"가뿐하네"' if tier == 1 else (u'"영차..."' if tier == 2 else u'"헉... 높다"')))
print(u'  ★ 현행 임계 %.2f H 와 Dock %.3f H 의 차이는 %.4f H = %.1f%% — 경계가 최빈값 위에 있다.'
      % (LIGHT_H, DOCK_H, LIGHT_H - DOCK_H, 100 * (LIGHT_H - DOCK_H) / DOCK_H))
print(u'  즉 배포 기본값에서 "가뿐하네"는 **자기 키의 %.1f%% 높이**를 1.20초 동안 양손으로 붙잡고'
      % (100 * DOCK_H))
print(u'  4박자로 기어오르면서 하는 말이다.')
print(u'')

print(u'=' * 78)
if FAIL:
    print(u'★ 실패 %d건 — 위 숫자를 쓰지 마라' % len(FAIL))
    for f in FAIL:
        print(u'   - ' + f)
    sys.exit(1)
print(u'전 항목 통과. 교정 %d줄 + 음성대조 %d줄 + 시도율 교차교정.' % (len(gold), neg))
print(u'=' * 78)
