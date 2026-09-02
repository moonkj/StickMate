# -*- coding: utf-8 -*-
"""
StickMate 재화/성장 경제 계산기.

★ 이 저장소는 오늘 밤 거짓 통과 9건을 겪었다. 그래서 이 파일은
   **알려진 값으로 먼저 교정(CALIBRATE)하고, 교정이 하나라도 깨지면
   그 뒤 숫자를 전부 폐기(SystemExit)** 한다.

교정 기준값의 출처는 전부 **실제 로그 문자열**이다(추정 아님):
  · "[성장] 스틱메이트 Lv.2 (196/207 XP)"            -> need(2)  = 207   (exp 1.05)
  · "[성장] ... Lv.3 6/317"                          -> need(3)  = 317   (exp 1.05)
  · "[성장] 보너스 +15 XP ... Lv.127 5700/16181"     -> need(127)= 16181 (exp 1.05, float32)
  · "[성장] 보너스 +15 XP ... Lv.3 235/354"          -> need(3)  = 354   (구 exp 1.15)
  · "[기록] ... 활쏘기 16/48발"                       -> 사이클당 3발 / 정중앙 1발
"""
import numpy as np

BASE = 100.0
EXP  = 1.05
PASSIVE_XP_PER_MIN = 1.5      # StickConfig.progressionPassiveXpPerMinute
BULLSEYE_XP        = 15.0     # StickConfig.progressionBullseyeXp

def need(level, base=BASE, exp=EXP):
    """CharacterProgressionModel.XpToNextLevel — Unity는 float 연산이므로 float32로 흉내낸다."""
    b = np.float32(base); e = np.float32(exp); L = np.float32(max(1, level))
    return float(np.float32(b) * np.float32(np.power(L, e, dtype=np.float32)))

def fmt0(x):
    """Unity의 {x:F0} — MidpointRounding.AwayFromZero 계열 반올림."""
    import decimal
    return int(decimal.Decimal(repr(x)).quantize(decimal.Decimal('1'), rounding=decimal.ROUND_HALF_UP))

# ============================== 교정 ==============================
CAL = [
    ("need(2)  exp1.05", fmt0(need(2)),            207),
    ("need(3)  exp1.05", fmt0(need(3)),            317),
    ("need(127)exp1.05", fmt0(need(127)),        16181),
    ("need(3)  exp1.15", fmt0(need(3, exp=1.15)),  354),
]
print("=" * 74)
print("교정 (알려진 실제 로그 값과 대조)")
print("=" * 74)
fail = 0
for name, got, want in CAL:
    ok = (got == want)
    fail += (not ok)
    print(f"  {'PASS' if ok else '**FAIL**'}  {name:18s} 계산={got:<8d} 로그={want}")
if fail:
    raise SystemExit(f"\n★ 교정 {fail}건 실패 — 이 계산기가 내는 모든 숫자를 폐기한다.\n")
print("  -> 교정 4/4 통과. 아래 숫자는 이 곡선을 신뢰해도 된다.\n")

# ============================== 누적 곡선 ==============================
def cum_xp(level):
    return sum(need(i) for i in range(1, level))

def hours_passive(level):
    return cum_xp(level) / (PASSIVE_XP_PER_MIN * 60.0)

REQ_LEVELS = {
  "Head":      [1,5,9,20,23,26],  "Eyes":      [1,6,11,15,19,23],
  "Neck":      [1,8,12,18,21,25], "Shoulders": [1,13,17,22,25,28],
  "Hair":      [1,5,9,14,18,22],  "Fx":        [1,6,12,16,20,24],
  "Pet":       [1,13,19,24,27,30],
}

print("=" * 74); print("A. 레벨 곡선 — 패시브 90XP/시 기준"); print("=" * 74)
print(f"{'Lv':>4} {'need':>7} {'누적XP':>9} {'누적시간':>9} {'8h/일':>7} {'24h상주':>8}  해금")
unlock_at = {}
for slot, lv in REQ_LEVELS.items():
    for i, L in enumerate(lv): unlock_at.setdefault(L, []).append(f"{slot}#{i}")
for L in [1,2,3,4,5,6,8,9,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,30]:
    h = hours_passive(L)
    print(f"{L:>4} {fmt0(need(L)):>7} {fmt0(cum_xp(L)):>9} {h:>8.1f}h {h/8:>6.1f}d {h/24:>7.1f}d  "
          f"{','.join(unlock_at.get(L,[])) or '-'}")

print()
print("=" * 74); print("B. 활쏘기 — 구조적 사실 (ArcheryState.cs)"); print("=" * 74)
SHOTS, BULLS = 3, 1
print(f"  사이클당 {SHOTS}발 / 정중앙 {BULLS}발 (BuildScenario: 마지막 발 항상 Bullseye 고정)")
print(f"  -> 명중률은 영구히 {BULLS/SHOTS*100:.1f}% 로 **구조 고정**. 실기 로그 16/48 = {48//SHOTS}사이클 (16/48={16/48*100:.1f}%) 일치.")
print(f"  -> 사이클당 XP = {BULLSEYE_XP:.0f} (확률 아님, 확정)")
CEREMONY_MIN = 0.55 + 3*(0.42+0.30+0.62+0.34) + 0.55   # Intro + 3*(Draw+Aim+Flight_min+Recover) + Outro
CEREMONY_MAX = 0.55 + 3*(0.42+0.30+1.25+0.34) + 0.55
print(f"  의식 구간(접근 제외) = {CEREMONY_MIN:.2f}~{CEREMONY_MAX:.2f}초, 접근 0~12초(archeryApproachTimeoutSeconds)")
print(f"  수동 발동(ForceTriggerNow)은 확률/쿨다운을 **건너뛴다** -> 사이클 최소 간격 = {CEREMONY_MIN:.1f}초")
print(f"  -> 동전을 무제한으로 주면 시간당 최대 {3600/CEREMONY_MIN:.0f}회 파밍 가능. **반드시 지급 쿨다운이 필요.**")

print()
print("=" * 74); print("C. 재화 규칙 (제안) + 3개 유저 원형의 동전 곡선"); print("=" * 74)
FOCUS_PER_MIN, FOCUS_COMPLETE_BONUS = 1, 5
ARCHERY_COIN, ARCHERY_COIN_CD_SEC = 1, 600.0   # 기존 archeryCooldownSeconds 재사용
def focus_pay(minutes, completed=True):
    return minutes*FOCUS_PER_MIN + (FOCUS_COMPLETE_BONUS if completed else 0)
for m in (15,25,50):
    print(f"  집중 {m:>2}분 완주 = {focus_pay(m):>3}동전   (중도취소 = 경과 분당 {FOCUS_PER_MIN}동전, 완주보너스 없음)")
print(f"  활쏘기 정중앙 1회 = {ARCHERY_COIN}동전, 지급 쿨다운 {ARCHERY_COIN_CD_SEC:.0f}초 -> 시간당 최대 {3600/ARCHERY_COIN_CD_SEC:.0f}동전")

ARCH = {
 "A 켜두기만(8h, 집중0, 활0)":      dict(hours=8, focus=[],        arch=0),
 "B 기준(8h, 집중25x2, 활4)":       dict(hours=8, focus=[25,25],   arch=4),
 "C 적극(10h, 집중25x4, 활8)":      dict(hours=10, focus=[25]*4,   arch=8),
 "D 파밍상한(10h, 집중50x4, 활60)": dict(hours=10, focus=[50]*4,   arch=60),
}
print(f"\n{'원형':<30}{'XP/일':>8}{'동전/일':>9}{'동전/주*':>10}{'동전/30일*':>11}")
daily = {}
for k, v in ARCH.items():
    arch_capped = min(v["arch"], int(v["hours"]*3600/ARCHERY_COIN_CD_SEC))
    xp = v["hours"]*60*PASSIVE_XP_PER_MIN + arch_capped*BULLSEYE_XP
    c  = sum(focus_pay(m) for m in v["focus"]) + arch_capped*ARCHERY_COIN
    daily[k] = (xp, c)
    print(f"{k:<30}{xp:>8.0f}{c:>9}{c*5+ (0 if 'A' in k[:1] else 2)*2:>10}{int(c*22):>11}")
print("  * 주/30일은 '주5일 근무일 + 주말은 켜두기만'을 가정(주 5일, 월 22일)")

print()
print("=" * 74); print("D. 집중모드 지급률 — 세션 길이 중립성 검증"); print("=" * 74)
print("  [기각안] 분당1 + 완주보너스 5(정액):")
for m in (15,25,50): print(f"     {m:>2}분 = {m+5:>3}동전 -> 시간당 {(m+5)/m*60:>5.1f}동전")
print("     -> 15분 스팸이 시간당 80동전으로 최적. **짧은 세션을 강요하는 역인센티브**. 기각.")
print("  [채택안] 완주 분당 1.2동전 / 중도취소 분당 1.0동전:")
for m in (15,25,50): print(f"     {m:>2}분 완주 = {int(m*1.2):>3}동전 -> 시간당 {m*1.2/m*60:>5.1f}동전")
print("     -> 세 길이가 **전부 시간당 72동전**. 세션 길이는 집중 이유로만 고른다. 채택.")

FOCUS_RATE_DONE, FOCUS_RATE_CANCEL = 1.2, 1.0
def focus_pay(m, done=True): return int(m*(FOCUS_RATE_DONE if done else FOCUS_RATE_CANCEL))

PRICE = {"일반":20, "희귀":45, "영웅":100, "전설":220}
GRADE_BY_RANK = ["일반","일반","희귀","희귀","영웅","전설"]   # 슬롯 내 요구레벨 순위 = 등급
print()
print("=" * 74); print("E. 가격표 + 검산"); print("=" * 74)
print(f"  등급 분포 = 슬롯당 일반2 / 희귀2 / 영웅1 / 전설1 (rank0은 기본 지급 = 0동전)")
paid_per_slot = sum(PRICE[GRADE_BY_RANK[i]] for i in range(1,6))
print(f"  슬롯당 유료 총액 = {'+'.join(str(PRICE[GRADE_BY_RANK[i]]) for i in range(1,6))} = {paid_per_slot}동전")
print(f"  7슬롯 전체(35종 유료, 7종 무료) = {paid_per_slot*7}동전")
for g,p in PRICE.items():
    print(f"  {g} {p:>3}동전 = 집중25분 {p/30:>4.1f}회 = 기준유저 {p/64:>4.1f}근무일 = 활쏘기 {p}회(최소 {p*10/60:>4.1f}시간)")

WEEK_XP   = 5*(8*60*PASSIVE_XP_PER_MIN + 4*BULLSEYE_XP) + 2*(4*60*PASSIVE_XP_PER_MIN + 2*BULLSEYE_XP)
WEEK_COIN = 5*(focus_pay(25)*2 + 4*ARCHERY_COIN)        + 2*(2*ARCHERY_COIN)
print(f"\n  기준유저 주간: XP {WEEK_XP:.0f} / 동전 {WEEK_COIN}  (평일 8h+집중2+활4, 주말 4h+활2)")
print(f"  Lv.28(마지막 스탯장비) 누적 {fmt0(cum_xp(28))}XP = {cum_xp(28)/WEEK_XP:.1f}주 = {cum_xp(28)/WEEK_XP*7:.0f}일")
print(f"  전체 구매 총액 {paid_per_slot*7}동전            = {paid_per_slot*7/WEEK_COIN:.1f}주 = {paid_per_slot*7/WEEK_COIN*7:.0f}일")
print(f"  -> 동전 완주가 레벨 완주보다 {(paid_per_slot*7/WEEK_COIN)/(cum_xp(28)/WEEK_XP)*100-100:+.0f}% 지점. "
      f"목표는 '동전이 레벨보다 늦지 않을 것'.")

print()
print("=" * 74); print("F. 일 단위 시뮬레이션 — 해금된 것을 살 수 있는가 (기준유저 B)"); print("=" * 74)
CATALOG = []
for slot, lvs in REQ_LEVELS.items():
    for i, L in enumerate(lvs):
        CATALOG.append(dict(slot=slot, rank=i, lv=L,
                            grade=GRADE_BY_RANK[i],
                            price=0 if i == 0 else PRICE[GRADE_BY_RANK[i]]))
CATALOG.sort(key=lambda e: (e["lv"], e["price"]))

def simulate(days=200, weekday=(8,2,4), weekend=(4,0,2), levelup_coin=0, verbose_days=()):
    """(hours, focus25_count, archery_count) 튜플 2개. 반환: 일별 상태."""
    xp_total, coins, owned = 0.0, 0, set()
    lv, cur = 1, 0.0
    waited, first_wait, rows = 0, None, []
    for d in range(1, days+1):
        h, f, a = weekday if (d-1) % 7 < 5 else weekend
        a = min(a, int(h*3600/ARCHERY_COIN_CD_SEC))
        gain = h*60*PASSIVE_XP_PER_MIN + a*BULLSEYE_XP
        cur += gain; xp_total += gain
        while cur >= need(lv):
            cur -= need(lv); lv += 1; coins += levelup_coin*lv
        coins += focus_pay(25)*f + a*ARCHERY_COIN
        # 살 수 있는 것은 산다(싼 것부터)
        avail = [e for e in CATALOG if e["lv"] <= lv and id(e) not in owned]
        blocked = False
        for e in sorted(avail, key=lambda e: e["price"]):
            if coins >= e["price"]: coins -= e["price"]; owned.add(id(e))
            else: blocked = True
        if blocked:
            waited += 1
            if first_wait is None: first_wait = d
        rows.append(dict(d=d, lv=lv, coins=coins, owned=len(owned),
                         locked=sum(1 for e in CATALOG if e["lv"] > lv), blocked=blocked))
    return rows, waited, first_wait

rows, waited, first_wait = simulate()
print(f"{'일':>4}{'Lv':>4}{'보유':>5}{'해금대기':>8}{'동전잔액':>9}  상태")
for d in (1,2,3,5,7,14,21,30,45,60,65,75,90,120,150,180,200):
    r = rows[d-1]
    print(f"{r['d']:>4}{r['lv']:>4}{r['owned']:>5}{r['locked']:>8}{r['coins']:>9}  "
          f"{'동전 부족(대기)' if r['blocked'] else '해금된 것 전부 보유'}")
print(f"\n  동전이 없어서 기다린 날 = {waited}일 / 200일 ({waited/2:.0f}%), 첫 대기 = {first_wait}일차")
print(f"  42종 전부 보유 시점 = {next((r['d'] for r in rows if r['owned']==42), None)}일차")

print()
print("=" * 74); print("G. 가격 배율 스윕 — '상점이 가끔은 안 된다고 말해야 한다'"); print("=" * 74)
print("  ★ F의 결론: 3,010동전 가격표에서는 200일 중 대기 0일 = **동전이 장식**이다.")
print("     레벨 게이트가 항상 먼저 물려서 상점이 한 번도 거절하지 않는다.\n")
BASE_PRICE = dict(PRICE)
print(f"{'배율':>5}{'일반':>5}{'희귀':>5}{'영웅':>5}{'전설':>5}{'총액':>7}{'대기일/200':>11}{'첫대기':>7}{'42종완주':>9}")
best = None
for mult in (1.0, 1.25, 1.5, 1.75, 2.0, 2.25, 2.5, 3.0):
    for g in BASE_PRICE: PRICE[g] = int(round(BASE_PRICE[g]*mult/5)*5)
    for e in CATALOG: e["price"] = 0 if e["rank"] == 0 else PRICE[e["grade"]]
    rows, waited, fw = simulate(days=200)
    done = next((r["d"] for r in rows if r["owned"] == 42), None)
    tot = sum(PRICE[GRADE_BY_RANK[i]] for i in range(1,6))*7
    print(f"{mult:>5.2f}{PRICE['일반']:>5}{PRICE['희귀']:>5}{PRICE['영웅']:>5}{PRICE['전설']:>5}"
          f"{tot:>7}{waited:>11}{str(fw):>7}{str(done):>9}")
print("\n  판정 기준: (a) 첫 대기가 1주차 안에 오면 안 된다(첫인상), (b) 대기일 15~35%가 적정,")
print("            (c) 42종 완주가 Lv.30(75일차)보다 너무 늦으면 레벨 표가 죽는다(<=120일 목표).")

for g,v in dict(일반=30, 희귀=70, 영웅=150, 전설=330).items(): PRICE[g]=v
for e in CATALOG: e["price"] = 0 if e["rank"]==0 else PRICE[e["grade"]]
TOTAL = sum(PRICE[GRADE_BY_RANK[i]] for i in range(1,6))*7
print()
print("=" * 74); print("H. 채택 가격표 1.50배 + 유저 원형 비교"); print("=" * 74)
print(f"  일반 30 / 희귀 70 / 영웅 150 / 전설 330 — 유료 35종 총액 {TOTAL}동전")
for g,p in PRICE.items():
    print(f"    {g} {p:>3}동전 = 집중25분 {p/30:>4.1f}회 = 활쏘기 {p}회(쿨다운상 최소 {p*10/60:>5.1f}시간) = 기준유저 {p/46.3:>4.1f}일")

PROFILES = {
 "A 켜두기만 (평일8h/주말4h, 집중0)":  dict(weekday=(8,0,4), weekend=(4,0,2)),
 "B 기준     (집중25x2/평일)":         dict(weekday=(8,2,4), weekend=(4,0,2)),
 "C 적극     (집중25x4/평일)":         dict(weekday=(10,4,8), weekend=(6,1,4)),
}
print(f"\n{'원형':<34}{'1일차':>16}{'7일차':>16}{'30일차':>16}")
print(f"{'':<34}{'Lv/보유/동전':>16}{'Lv/보유/동전':>16}{'Lv/보유/동전':>16}")
SNAP = {}
for name, kw in PROFILES.items():
    rows,_,_ = simulate(days=400, **kw); SNAP[name]=rows
    cells = ""
    for d in (1,7,30):
        r = rows[d-1]
        cells += "{:>16}".format("{}/{}/{}".format(r["lv"], r["owned"], r["coins"]))
    print(f"{name:<34}{cells}")
print(f"\n{'원형':<34}{'42종 완주':>12}{'대기일/200':>12}{'200일 잔액':>12}")
for name, rows in SNAP.items():
    done = next((r['d'] for r in rows if r['owned']==42), None)
    w = sum(1 for r in rows[:200] if r['blocked'])
    print(f"{name:<34}{str(done)+'일' if done else '미완주':>12}{w:>12}{rows[199]['coins']:>12}")

print()
print("=" * 74); print("I. 문제 2건과 처방"); print("=" * 74)
print("  문제1: 원형 A(켜두기만)는 30일차에 Lv.19인데 보유 10종/동전 14. 200일 중 199일 대기, 영구 미완주.")
print("         -> 레벨업 로그는 '새 장비가 열렸습니다'라고 말하는데 살 수가 없다. 레벨업 메시지가 거짓말이 된다.")
print("         -> 앱 철학('아무것도 안 해도 자란다')을 재화가 정면으로 배신한다.")
print("  문제2: 원형 C는 56일에 완주하고 200일차에 15,786동전이 죽은 채로 쌓인다. 소비처가 없다.\n")

print(f"{'레벨업보상':>14}{'A완주':>9}{'A대기':>7}{'B완주':>9}{'B대기':>7}{'C완주':>9}{'B 30일 동전':>12}")
for label, fn in [("없음", lambda L:0), ("Lv x2", lambda L:2*L), ("Lv x4", lambda L:4*L),
                  ("Lv x6", lambda L:6*L), ("Lv x8", lambda L:8*L)]:
    out=[]
    for nm, kw in PROFILES.items():
        rows,_,_ = simulate(days=400, levelup_coin=0, **kw) if fn(2)==0 else (None,None,None)
        if rows is None:
            # levelup_coin 파라미터는 정수 배율만 받으므로 직접 재구현
            xp,coins,owned,lv,cur,w = 0.0,0,set(),1,0.0,0
            rows=[]
            for d in range(1,401):
                h,f,a = kw["weekday"] if (d-1)%7<5 else kw["weekend"]
                a=min(a,int(h*3600/ARCHERY_COIN_CD_SEC)); cur+=h*60*PASSIVE_XP_PER_MIN+a*BULLSEYE_XP
                while cur>=need(lv):
                    cur-=need(lv); lv+=1; coins+=fn(lv)
                coins+=focus_pay(25)*f+a*ARCHERY_COIN
                blocked=False
                for e in sorted([e for e in CATALOG if e["lv"]<=lv and id(e) not in owned], key=lambda e:e["price"]):
                    if coins>=e["price"]: coins-=e["price"]; owned.add(id(e))
                    else: blocked=True
                if blocked and d<=200: w+=1
                rows.append(dict(d=d,lv=lv,coins=coins,owned=len(owned),blocked=blocked))
        out.append((next((r['d'] for r in rows if r['owned']==42), None), w, rows))
    tot=sum(fn(L) for L in range(2,31))
    print(f"{label:>14}{str(out[0][0] or '미완주'):>9}{out[0][1]:>7}{str(out[1][0] or '미완주'):>9}{out[1][1]:>7}"
          f"{str(out[2][0] or '미완주'):>9}{out[1][2][29]['coins']:>12}   (Lv30까지 총 {tot}동전)")

print()
print("  ★ 위 표의 결론: 원형 A와 B는 **레벨 곡선도 실행 시간도 완전히 동일**하다(차이는 집중모드뿐).")
print("    그래서 레벨 기반/시간 기반 채널은 A를 도우면 B도 똑같이 돕는다 — Lv x4 이상에서 B의 대기가")
print("    0이 되어 방금 만든 텍스처가 붕괴한다. **정액 레벨업 보상은 이 문제의 도구가 아니다.**\n")
print("=" * 74); print("J. 채택 모델 — 이중 게이트 + 유예 자동 해금 ('상점은 거절하지 않고, 기다리라고 한다')")
print("=" * 74)
GRACE_H = {"일반":5, "희귀":15, "영웅":35, "전설":80}   # 요구레벨 도달 후 무상 자동해금까지의 '함께한 시간'
print("  규칙: (1) 요구 레벨 도달 -> 상점 진열, 동전으로 **즉시** 구매 가능")
print("        (2) 요구 레벨 도달 + 등급별 유예시간 경과 -> **동전 없이 자동 해금**")
print(f"        유예 = 일반 {GRACE_H['일반']}h / 희귀 {GRACE_H['희귀']}h / 영웅 {GRACE_H['영웅']}h / 전설 {GRACE_H['전설']}h (함께한 시간 기준)")
print("  -> 동전의 역할이 '해금'에서 '앞당기기'로 바뀐다. 현금->동전->시간단축, 그 이상 아무것도 아니다.")
print("  -> 페이투윈이 **원리적으로** 불가능하다: 어느 경로도 도달 가능한 최대치를 바꾸지 않는다.\n")

def sim2(days=400, weekday=(8,2,4), weekend=(4,0,2)):
    coins, lv, cur, hrs = 0, 1, 0.0, 0.0
    reached = {}; owned = {}; log=[]
    for d in range(1, days+1):
        h, f, a = weekday if (d-1)%7 < 5 else weekend
        a = min(a, int(h*3600/ARCHERY_COIN_CD_SEC))
        hrs += h; cur += h*60*PASSIVE_XP_PER_MIN + a*BULLSEYE_XP
        while cur >= need(lv): cur -= need(lv); lv += 1
        coins += focus_pay(25)*f + a*ARCHERY_COIN
        for i,e in enumerate(CATALOG):
            if e["lv"] <= lv and i not in reached: reached[i] = hrs
        # 자동 해금
        for i,e in enumerate(CATALOG):
            if i in owned or i not in reached: continue
            if e["rank"]==0 or hrs >= reached[i] + GRACE_H[e["grade"]]: owned[i] = (d, "자동")
        # 동전 구매 (싼 것부터)
        for i,e in sorted([(i,e) for i,e in enumerate(CATALOG) if i not in owned and i in reached],
                          key=lambda t: t[1]["price"]):
            if coins >= e["price"]: coins -= e["price"]; owned[i] = (d, "구매")
        log.append(dict(d=d, lv=lv, coins=coins, owned=len(owned),
                        bought=sum(1 for v in owned.values() if v[1]=="구매")))
    return log, owned

print(f"{'원형':<34}{'42종 완주':>10}{'구매/자동':>11}{'200일 잔액':>11}{'30일차 Lv/보유/동전':>22}")
RES={}
for nm, kw in PROFILES.items():
    log, owned = sim2(**kw); RES[nm]=(log,owned)
    done = next((r['d'] for r in log if r['owned']==42), None)
    b = log[-1]['bought']
    r30 = log[29]
    snap = "{}/{}/{}".format(r30["lv"], r30["owned"], r30["coins"])
    print("{:<34}{:>10}{:>11}{:>11}{:>22}".format(
        nm, str(done)+"일", "{}/{}".format(b, 42-b), log[199]["coins"], snap))

print()
print("=" * 74); print("K. 동전이 사는 것 = 시간. 그 시간의 **상한**이 페이투윈 차단의 검산이다."); print("=" * 74)
logA, ownA = RES["A 켜두기만 (평일8h/주말4h, 집중0)"]
print(f"{'원형':<34}{'완주일':>8}{'A대비 앞당김':>13}{'앞당김 비율':>12}")
baseA = next(r['d'] for r in logA if r['owned']==42)
for nm,(log,own) in RES.items():
    dn = next(r['d'] for r in log if r['owned']==42)
    print(f"{nm:<34}{dn:>7}일{baseA-dn:>12}일{(baseA-dn)/baseA*100:>11.0f}%")
print(f"\n  ★ 동전을 아무리 모아도 완주는 {baseA}일 -> {min(next(r['d'] for r in l if r['owned']==42) for l,_ in RES.values())}일이 한계다.")
print( "    (레벨 요구치는 동전으로 못 산다 — 이 상한을 만드는 것이 바로 요구 레벨 표다.)")
print( "  ★ 최종 도달점은 세 원형이 **완전히 동일**하다: 42종 전부 / 4스탯 전부 최대치(20).")
print( "    돈이 사는 것은 순서와 속도뿐, 천장이 아니다. = 페이투윈 아님.\n")

print(f"{'등급':<6}{'가격':>6}{'유예':>6}{'B가 앞당긴 평균':>16}{'동전/일 환산':>14}")
for g in ("일반","희귀","영웅","전설"):
    idxs=[i for i,e in enumerate(CATALOG) if e["grade"]==g and e["rank"]>0]
    advs=[]
    for i in idxs:
        dB,kB = RES["B 기준     (집중25x2/평일)"][1][i]
        dA,_  = ownA[i]
        if kB=="구매": advs.append(dA-dB)
    avg = sum(advs)/len(advs) if advs else 0
    print(f"{g:<6}{PRICE[g]:>6}{GRACE_H[g]:>5}h{avg:>14.1f}일{(PRICE[g]/avg if avg else 0):>13.0f}")
print("  -> 등급이 올라갈수록 '하루를 사는 값'이 올라간다(일반이 가장 싸게 앞당겨진다). 의도한 곡선이다.")

print()
print("=" * 74); print("L. 스탯 체계 검산"); print("=" * 74)
STATS = ["집중력","관찰력","매력","민첩"]
SLOT_MAIN = {"Head":"집중력","Eyes":"관찰력","Neck":"매력","Shoulders":"민첩"}
SUB_CYCLE = {"Head":"관찰력","Eyes":"매력","Neck":"민첩","Shoulders":"집중력"}  # 부스탯 순환
MAIN = {"일반":3,"희귀":5,"영웅":7,"전설":10}
SUB  = {"일반":0,"희귀":1,"영웅":2,"전설":3}
SET_BONUS, CAP = 2, 20
THRESH = {"초급":6,"중급":12,"고급":18}
def lvbase(L): return min((L-1)//4, 5)
def stats(level, worn, set_done, sub_override=None):
    """worn = {slot: grade}. sub_override = {slot: stat}"""
    t = {s: lvbase(level) for s in STATS}
    for slot, g in worn.items():
        t[SLOT_MAIN[slot]] += MAIN[g]
        sub = (sub_override or {}).get(slot, SUB_CYCLE[slot])
        t[sub] += SUB[g]
    if set_done:
        for s in STATS: t[s] += SET_BONUS
    return {s: min(v, CAP) for s, v in t.items()}
def band(v): 
    return "고급" if v>=THRESH["고급"] else "중급" if v>=THRESH["중급"] else "초급" if v>=THRESH["초급"] else "-"

print(f"  레벨 기본치 = min(floor((Lv-1)/4), 5)  ->  Lv1:0 Lv5:1 Lv9:2 Lv13:3 Lv17:4 Lv21+:5 (포화)")
print(f"  등급별 주스탯/부스탯: " + " / ".join(f"{g} +{MAIN[g]}/+{SUB[g]}" for g in MAIN))
print(f"  부스탯 순환: " + " -> ".join(f"{k}({SLOT_MAIN[k]})->{v}" for k,v in SUB_CYCLE.items()))
print(f"  세트 완성 보너스 = 4스탯 전부 +{SET_BONUS} / 상단 캡 = {CAP} / 임계 = " +
      " ".join(f"{k}{v}" for k,v in THRESH.items()))

SET_RANK = {0:("A 일반",1),1:("B 일반",13),2:("C 희귀",17),3:("D 희귀",22),4:("E 영웅",25),5:("F 전설",28)}
print(f"\n  {'세트':<8}{'완성Lv':>7}{'등급':>6}   " + "".join(f"{s:>8}" for s in STATS) + "   구간")
for rank,(nm,L) in SET_RANK.items():
    g = GRADE_BY_RANK[rank]
    t = stats(L, {s:g for s in SLOT_MAIN}, True)
    print(f"  {nm:<8}{L:>7}{g:>6}   " + "".join(f"{t[s]:>8}" for s in STATS) +
          f"   {band(t[STATS[0]])}" + ("  <- 캡 도달" if t[STATS[0]]==CAP else ""))

print(f"\n  ★ 페이투윈 차단 하드 조건 (DLC 0개 보유 유저가 기본 42종만으로):")
tF = stats(28, {s:"전설" for s in SLOT_MAIN}, True)
print(f"     세트 F(기본 전설 4종) 완성 @Lv.28 -> " + " / ".join(f"{s} {tF[s]}" for s in STATS))
ok_cap = all(v==CAP for v in tF.values())
print(f"     [{'PASS' if ok_cap else 'FAIL'}] 4스탯 전부 상단 캡 {CAP} 도달 = **기본 콘텐츠만으로 최대치 달성 가능**")
dlc = stats(21, {s:"전설" for s in SLOT_MAIN}, True, sub_override={"Eyes":"집중력","Neck":"집중력","Shoulders":"집중력"})
raw = lvbase(21)+MAIN["전설"]+3*SUB["전설"]+SET_BONUS
print(f"     DLC 부스탯 몰빵 가정(3종 모두 집중력): 원시값 {raw} -> 클램프 {dlc['집중력']}")
print(f"     [{'PASS' if dlc['집중력']<=CAP else 'FAIL'}] DLC로도 캡을 넘지 못한다 = **재구매 압박 0**")
print(f"     [{'PASS' if MAIN['전설']==10 else 'FAIL'}] 전설 주스탯은 슬롯 상한 10으로 고정 — DLC 전설도 **정확히 같은 수치**")

print(f"\n  ★ 무장비 / 무료 세트A 만의 값 (상점을 한 번도 안 쓴 유저):")
for L in (1,5,13,21,30):
    naked = stats(L, {}, False); setA = stats(L, {s:"일반" for s in SLOT_MAIN}, True)
    print(f"     Lv.{L:<3} 무장비 {naked['집중력']:>2}({band(naked['집중력']):<2})   무료 세트A 완성 {setA['집중력']:>2}({band(setA['집중력'])})")
print(f"     -> 무장비는 어떤 임계도 못 연다(장비가 의미를 갖는다). 무료 세트A는 Lv.5에 초급 4종 동시 해금.")

print()
print("=" * 74); print("M. 1일차 / 1주차 / 1개월차 — 원형 B(기준)가 실제로 무엇을 갖고 있는가"); print("=" * 74)
logB, ownB = RES["B 기준     (집중25x2/평일)"]
STAT_SLOTS = ["Head","Eyes","Neck","Shoulders"]
def best_at(day):
    have = [CATALOG[i] for i,(d,_) in ownB.items() if d <= day]
    per = {}
    for s in STAT_SLOTS:
        c = [e for e in have if e["slot"]==s]
        per[s] = max(c, key=lambda e:e["rank"]) if c else None
    # 세트 = 4스탯 슬롯이 같은 rank
    ranks = {r for r in range(6) if all(any(e["slot"]==s and e["rank"]==r for e in have) for s in STAT_SLOTS)}
    best = None
    for r in sorted(ranks, reverse=True):     # 세트 완성 빌드
        t = stats(logB[day-1]["lv"], {s:GRADE_BY_RANK[r] for s in STAT_SLOTS}, True); best=(t,f"세트 {SET_RANK[r][0]} 완성"); break
    mix = stats(logB[day-1]["lv"], {s:GRADE_BY_RANK[per[s]["rank"]] for s in per if per[s]}, False)
    if best is None or sum(mix.values()) > sum(best[0].values()): best=(mix,"세트 미완성(최고등급 조합)")
    return best, len(have), per
print(f"{'시점':<10}{'Lv':>4}{'보유':>5}{'동전':>7}  {'집중력':>7}{'관찰력':>7}{'매력':>7}{'민첩':>7}  구간        빌드")
for label, d in [("1일차",1),("3일차",3),("1주차",7),("2주차",14),("1개월차",30),("2개월차",60),("완주(75일)",75)]:
    (t, why), n, per = best_at(d); r = logB[d-1]
    print(f"{label:<10}{r['lv']:>4}{n:>5}{r['coins']:>7}  " + "".join(f"{t[s]:>7}" for s in STATS) +
          f"  {band(t['집중력']):<10}{why}")
print()
print("  임계 최초 도달(원형 B):")
for name, need_v in THRESH.items():
    d = next((d for d in range(1,200) if best_at(d)[0][0]["집중력"] >= need_v), None)
    print(f"    {name} {need_v:>2} -> {d}일차" + (f" ({d/30:.1f}개월)" if d else ""))

print()
print("=" * 74); print("N. 저장 빈도 예상치 (기획 5-7 선결조건 판단용)"); print("=" * 74)
TICK, AUTOSAVE = 10.0, 60.0   # progressionPassiveTickSeconds / progressionAutoSaveIntervalSeconds
print(f"  현재 기준선 — CharacterProgressionDirector.Update():")
print(f"    · 패시브 XP가 {TICK:.0f}초마다 AddXp() -> IsDirty=true  (progressionPassiveXpPerMinute > 0인 한 항상)")
print(f"    · 자동 저장이 {AUTOSAVE:.0f}초마다 IsAnythingDirty()를 보는데 위 때문에 **항상 true**")
print(f"    -> 즉 지금도 이미 **{60/AUTOSAVE:.0f}회/분 = {24*60*60/AUTOSAVE:,.0f}회/일**을 무조건 쓰고 있다.\n")
ADDED = [
  ("집중 세션 종료(동전 지급)", 2,    True,  "재화 손실 방지 — 즉시 저장 필수"),
  ("아이템 구매",              0.41, True,  "2초 디바운스 권고(연속 구매 묶기)"),
  ("유예 자동 해금",           0.15, True,  "드묾"),
  ("부스탯 재분배(무료 1회/Lv)", 0.39, True,  "레벨업 빈도와 동일"),
  ("임계 최초 돌파",           0.16, True,  "평생 12회(4스탯x3단계)뿐"),
  ("활쏘기 동전 지급",         6*8,  False, "IsDirty만 — 주기 저장에 태운다(최대 1분/1동전 손실)"),
]
print(f"  {'추가 저장 유발원':<26}{'회/일':>7}{'즉시저장':>9}  비고")
imm = 0
for nm, per_day, immediate, note in ADDED:
    imm += per_day if immediate else 0
    print(f"  {nm:<26}{per_day:>7.2f}{'예' if immediate else '아니오':>9}  {note}")
base_day = 24*60*60/AUTOSAVE
print(f"\n  추가 **즉시** 저장 = {imm:.1f}회/일 = {imm/1440:.5f}회/분")
print(f"  기준선 {base_day:,.0f}회/일 대비 **+{imm/base_day*100:.2f}%** -> 저장 '빈도'는 사실상 안 변한다.")
print(f"  ★ 그런데 **손실의 성질이 바뀐다**:")
print(f"     지금  : 저장 손상 -> XP/레벨 손실 -> 켜두면 저절로 복구된다(되돌릴 수 있는 불편)")
print(f"     이후  : 저장 손상 -> **동전 잔액 + 보유 42종 + 임계 영구해금이 증발** -> 다시 못 번다")
N = 75
print(f"\n  노출량 검산 — 원형 B가 완주(75일)까지 겪는 저장 횟수 = {base_day*N:,.0f}회")
for p in (0.01, 0.1, 1.0):
    print(f"     File.Replace 실패율 {p:>4}% 가정 -> 비원자적 직접쓰기 {base_day*N*p/100:>8,.0f}회 "
          f"(= 손상 창 {base_day*N*p/100:,.0f}개)")
print(f"  Windows에서 File.Replace 실패는 드문 사건이 아니다(백신/OneDrive/Steam이 대상 핸들을 쥔다).")
print(f"  실기 로그에 **이미 관측됐다**: '[성장] 저장 파일을 원자적으로 교체하지 못해 직접 쓰기로 물러섰습니다(IOException)'")
print(f"  -> **선결 수정 필수.** 재화 착수 전에 고쳐야 한다는 5-7의 판단은 이 숫자로 지지된다.")
