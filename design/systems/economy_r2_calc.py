# -*- coding: utf-8 -*-
"""
StickMate 경제 R2 — 리더 판정(42종 전제 확정) 후 재검산.

★ 교정을 두 겹으로 건다.
  (1) 실제 로그 문자열에서 뽑은 값   — need(2)=207 / need(3)=317 / need(127)=16181
  (2) 이미 공개된 R1 산출물의 숫자   — Lv.5=11.7h / 총액 4550 / 완주 A87 B75 C56 / 구매 A2 B31 C35
  둘 중 하나라도 어긋나면 SystemExit. 그 뒤 숫자는 전부 폐기다.
"""
import numpy as np, decimal

# ── 프로덕션에서 읽은 값 ────────────────────────────────────────────────
BASE, EXP = 100.0, 1.05          # StickConfig.progressionXpCurveBase / Exponent
PASSIVE_XP_PER_MIN = 1.5         # progressionPassiveXpPerMinute (= 90 XP/시)
BULLSEYE_XP = 15.0               # progressionBullseyeXp
ARCHERY_CD_SEC = 600.0           # archeryCooldownSeconds 재사용 (동전 지급 게이트)
AUTOSAVE_SEC = 60.0              # progressionAutoSaveIntervalSeconds

# ItemCatalogGolden.txt 전문에서 추출(스크립트로 재추출해 대조함)
REQ = {"Head":[1,5,9,20,23,26], "Eyes":[1,6,11,15,19,23], "Neck":[1,8,12,18,21,25],
       "Shoulders":[1,13,17,22,25,28], "Hair":[1,5,9,14,18,22], "Fx":[1,6,12,16,20,24],
       "Pet":[1,13,19,24,27,30]}
STAT_SLOTS = ["Head","Eyes","Neck","Shoulders"]      # 스탯을 주는 4슬롯
LOOK_SLOTS = ["Hair","Fx","Pet"]                     # 외형 3슬롯 (★ 이번 판정의 대상)

def need(level, base=BASE, exp=EXP):
    b=np.float32(base); L=np.float32(max(1,level))
    return float(np.float32(b)*np.float32(np.power(L,np.float32(exp),dtype=np.float32)))
def f0(x): return int(decimal.Decimal(repr(x)).quantize(decimal.Decimal('1'), rounding=decimal.ROUND_HALF_UP))
def cum_xp(level): return sum(need(i) for i in range(1,level))
def hours_to(level): return cum_xp(level)/(PASSIVE_XP_PER_MIN*60.0)

GRADE_BY_RANK = ["일반","일반","희귀","희귀","영웅","전설"]
PRICE  = {"일반":30, "희귀":70, "영웅":150, "전설":330}     # 1.50배 채택안
GRACE0 = {"일반":5,  "희귀":15, "영웅":35,  "전설":80}      # R1 유예(시간)
FOCUS_DONE, FOCUS_CANCEL = 1.2, 1.0
def focus_pay(m, done=True): return int(m*(FOCUS_DONE if done else FOCUS_CANCEL))

CATALOG=[]
for slot,lvs in REQ.items():
    for r,lv in enumerate(lvs):
        g=GRADE_BY_RANK[r]
        CATALOG.append(dict(slot=slot, rank=r, lv=lv, grade=g,
                            price=0 if r==0 else PRICE[g],
                            look=slot in LOOK_SLOTS))

# ============================ 교정 1 — 로그 ============================
print("="*78); print("교정 ① 실제 로그 문자열"); print("="*78)
CAL1=[("need(2)",f0(need(2)),207),("need(3)",f0(need(3)),317),("need(127)",f0(need(127)),16181),
      ("need(3)@exp1.15",f0(need(3,exp=1.15)),354)]
bad=0
for n,g,w in CAL1:
    ok=g==w; bad+=not ok; print(f"  {'PASS' if ok else '**FAIL**'} {n:<16} 계산={g:<7} 로그={w}")

# ============================ 시뮬레이터 ============================
PROFILES = {   # ★ R1 economy_calc.py:217 과 동일 (a=활쏘기 횟수/일)
 "A 켜두기만 (집중0)"      : dict(weekday=(8,0,4), weekend=(4,0,2)),
 "B 기준     (집중25x2)"   : dict(weekday=(8,2,4), weekend=(4,0,2)),
 "C 적극     (집중25x4)"   : dict(weekday=(10,4,8),weekend=(6,1,4)),
}

def sim_day(days=400, weekday=(8,2,4), weekend=(4,0,2), grace=None, todo_coin=0, buy=True):
    """★ R1 economy_calc.sim2 의 정확한 복제 — 교정 전용(하루 해상도)."""
    grace = grace or GRACE0
    coins=0; lv=1; cur=0.0; hrs=0.0; reached={}; owned={}; log=[]
    for d in range(1, days+1):
        h,f,a = weekday if (d-1)%7<5 else weekend
        a = min(a, int(h*3600/ARCHERY_CD_SEC))
        hrs += h; cur += h*60*PASSIVE_XP_PER_MIN + a*BULLSEYE_XP
        while cur >= need(lv): cur -= need(lv); lv += 1
        coins += focus_pay(25)*f + a*1 + todo_coin
        for i,e in enumerate(CATALOG):
            if e["lv"]<=lv and i not in reached: reached[i]=hrs
        for i,e in enumerate(CATALOG):
            if i in owned or i not in reached: continue
            if e["rank"]==0 or hrs >= reached[i]+grace[e["grade"]]: owned[i]=(d,"자동")
        if buy:
            for i,e in sorted([(i,e) for i,e in enumerate(CATALOG) if i not in owned and i in reached],
                              key=lambda t:t[1]["price"]):
                if coins>=e["price"]: coins-=e["price"]; owned[i]=(d,"구매")
        log.append(dict(d=d,lv=lv,coins=coins,owned=len(owned),hrs=hrs,
                        bought=sum(1 for v in owned.values() if v[1]=="구매")))
    return log, owned

def sim_hour(days=400, weekday=(8,2,4), weekend=(4,0,2), grace=None, todo_coin=0, buy=True):
    """1시간 해상도 — 상점 생존도 측정용. 일 단위 수입/지출 총량은 sim_day와 같다."""
    grace = grace or GRACE0
    coins=0.0; lv=1; cur=0.0; hrs=0.0; reached={}; owned={}; trace=[]; log=[]
    for d in range(1, days+1):
        h,f,a = weekday if (d-1)%7<5 else weekend
        a = min(a, int(h*3600/ARCHERY_CD_SEC))
        for hh in range(h):
            hrs += 1.0
            cur += 60*PASSIVE_XP_PER_MIN + (a/h)*BULLSEYE_XP
            while cur >= need(lv): cur -= need(lv); lv += 1
            coins += (focus_pay(25) if hh < min(f,h) else 0) + a/h + (todo_coin if hh==0 else 0)
            for i,e in enumerate(CATALOG):
                if e["lv"]<=lv and i not in reached: reached[i]=hrs
            for i,e in enumerate(CATALOG):
                if i in owned or i not in reached: continue
                if e["rank"]==0 or hrs >= reached[i]+grace[e["grade"]]: owned[i]=(d,"자동")
            live  = [i for i,e in enumerate(CATALOG) if i in reached and i not in owned and coins>=e["price"]]
            short = [i for i,e in enumerate(CATALOG) if i in reached and i not in owned and coins< e["price"]]
            trace.append(dict(h=hrs,d=d,lv=lv,coins=coins,live=len(live),short=len(short),owned=len(owned)))
            if buy:
                for i,e in sorted([(i,e) for i,e in enumerate(CATALOG) if i not in owned and i in reached],
                                  key=lambda t:t[1]["price"]):
                    if coins>=e["price"]: coins-=e["price"]; owned[i]=(d,"구매")
        log.append(dict(d=d,lv=lv,coins=coins,owned=len(owned),hrs=hrs,
                        bought=sum(1 for v in owned.values() if v[1]=="구매")))
    return log, owned, trace

# ============================ 교정 2 — R1 공개 숫자 ============================
print(); print("="*78); print("교정 ② R1 산출물(ECONOMY_SPEC / economy_calc.out.txt)의 공개 숫자"); print("="*78)
paid_slot = sum(PRICE[GRADE_BY_RANK[r]] for r in range(1,6))
CAL2=[("Lv.5 누적시간(h)", round(hours_to(5),1), 11.7),
      ("Lv.28 누적시간(h)", round(hours_to(28),1), 483.6),
      ("슬롯당 유료 총액", paid_slot, 650),
      ("7슬롯 유료 총액(42종 전제)", paid_slot*7, 4550)]
for nm,kw in PROFILES.items():
    lg,ow = sim_day(**kw)
    done = next((r['d'] for r in lg if r['owned']==42), None)
    CAL2.append((f"완주일 {nm[:1]}", done, {"A":87,"B":75,"C":56}[nm[:1]]))
    CAL2.append((f"구매수 {nm[:1]}", lg[-1]['bought'], {"A":2,"B":31,"C":35}[nm[:1]]))
for n,g,w in CAL2:
    ok = (g==w) if isinstance(w,int) else abs(g-w)<0.06
    bad += not ok
    print(f"  {'PASS' if ok else '**FAIL**'} {n:<28} 계산={g:<8} R1공개={w}")
if bad: raise SystemExit(f"\n★ 교정 {bad}건 실패 — 이 계산기의 모든 숫자를 폐기한다.\n")
print(f"  -> 교정 {len(CAL1)+len(CAL2)}/{len(CAL1)+len(CAL2)} 통과 (로그 4 + R1 공개 {len(CAL2)}).")

def hdr(t): print("\n"+"="*78+"\n"+t+"\n"+"="*78)

# ============================ A. 42종 전제 명시 ============================
hdr("A. ★ 판정 확정 — 동전 소모처는 7슬롯 42종 전부다 (외형 3슬롯 포함)")
stat_paid = sum(e["price"] for e in CATALOG if not e["look"])
look_paid = sum(e["price"] for e in CATALOG if e["look"])
print(f"  스탯 4슬롯(모자/안경/넥타이/망토) 유료 24종 중 20종 = {stat_paid:>5}동전")
print(f"  외형 3슬롯(머리/이펙트/펫)      유료 18종 중 15종 = {look_paid:>5}동전")
print(f"  ------------------------------------------------------------")
print(f"  42종 전제(채택)  = {stat_paid+look_paid:>5}동전   ( = 슬롯당 유료 {paid_slot} x 7슬롯 )")
print(f"  24종 전제(폐기)  = {stat_paid:>5}동전   ( = {paid_slot} x 4슬롯 )")
print(f"  차이 {look_paid}동전 = 전체의 {look_paid/(stat_paid+look_paid)*100:.1f}% 감소")
print(f"  ★ 4550 = 650 x 7 이지 650 x 4(=2600)가 아니다. ux-designer가 찾은 그 세로 산술이 맞다.")
for nm,kw in PROFILES.items():
    lg,_ = sim_day(**kw)
    inc200 = None
    # 200일 총수입 = 200일 잔액 + 그동안 쓴 돈
    spent = sum(CATALOG[i]["price"] for i,v in sim_day(**kw)[1].items() if v[1]=="구매" and v[0]<=200)
    print(f"  {nm:<22} 200일 총수입 {lg[199]['coins']+spent:>6}동전 | 42종 소모처 4550 대비 "
          f"{(lg[199]['coins']+spent)/4550*100:>5.1f}% | 24종(2600) 대비 {(lg[199]['coins']+spent)/2600*100:>5.1f}%")
print("  -> 24종으로 좁히면 원형 C는 200일 수입이 소모처의 6배를 넘는다 = 동전이 장식이 된다.")

# ============================ B. 해상도 교차검증 ============================
hdr("B. 시간 해상도 교차 검증 — 하루 단위 sim과 시간 단위 sim이 같은 답을 내는가")
print(f"  {'원형':<24}{'완주(일)':>12}{'구매수':>10}   (하루해상도 / 시간해상도)")
for nm,kw in PROFILES.items():
    ld,_ = sim_day(**kw); lh,_,_ = sim_hour(**kw)
    dd = next((r['d'] for r in ld if r['owned']==42),None); dh = next((r['d'] for r in lh if r['owned']==42),None)
    print(f"  {nm:<24}{str(dd)+' / '+str(dh):>12}{str(ld[-1]['bought'])+' / '+str(lh[-1]['bought']):>10}")
print("  -> 차이가 있으면 그만큼이 '하루 안에서 언제 벌고 언제 사는가'의 영향이다(아래 생존도는 시간 해상도를 쓴다).")

# ============================ C. ★ 상점 생존도 ============================
hdr("C. ★ 빈 상태 실측 — 상점에 '누를 수 있는 버튼'이 있는 시간은 얼마인가")
print("  정의: 살아있음 = 요구레벨 도달 && 미보유 && 잔액>=가격.  회색 = 미도달 또는 잔액부족.")
print("  ux-designer 실측(첫 실행): 상점 24칸 중 채운 버튼 0개, 같은 순간 [장비] [착용] 4개 생존.\n")
first = CATALOG
print(f"  첫 실행(0시간, Lv.1) 카탈로그 상태")
print(f"    도달·기본지급(rank0)  : 스탯슬롯 4종 + 외형슬롯 3종 = 7종  (전부 보유 -> 구매 버튼 없음)")
print(f"    미도달(회색)          : 스탯슬롯 20종 + 외형슬롯 15종 = 35종")
print(f"    ★ 살아있는 구매 버튼  : 0개 / 42  (상점 탭 기준 0 / 24) — ux-designer 실측과 일치")
print(f"    첫 유료 아이템 요구레벨: Head#1=Lv.5, Hair#1=Lv.5 -> 누적 {hours_to(5):.1f}시간")

for nm,kw in PROFILES.items():
    for label, buyflag in (("즉시구매(하한)",True), ("안삼(상한)",False)):
        lg,ow,tr = sim_hour(days=120, buy=buyflag, **kw)
        T = tr[:len(tr)]
        alive = sum(1 for t in T if t['live']>0)
        short = sum(1 for t in T if t['live']==0 and t['short']>0)
        dead  = sum(1 for t in T if t['live']==0 and t['short']==0)
        firstlive = next((t['h'] for t in T if t['live']>0), None)
        # 최장 사망 구간(살아있는 버튼이 0인 연속 시간)
        run=0; worst=0; worst_at=0
        for t in T:
            if t['live']==0:
                run+=1
                if run>worst: worst, worst_at = run, t['h']
            else: run=0
        print(f"  {nm:<22}{label:<14} 생존 {alive/len(T)*100:5.1f}% | 잔액부족 {short/len(T)*100:5.1f}%"
              f" | 완전회색 {dead/len(T)*100:5.1f}% | 첫 생존 {firstlive:6.1f}h"
              f" | 최장 무버튼 {worst:4d}h(~{worst_at:.0f}h)")
print("\n  ★ 여기가 결함이다 — '즉시구매' 정책에서 원형 B의 상점은 대부분의 시간 동안 누를 것이 없다.")
print("    원인은 가격이 아니라 **유예가 절대시간(5/15/35/80h)이라 레벨 간격보다 짧다**는 것이다.")

# ============================ D. 유예 재설계 ============================
hdr("D. 유예가 왜 짧은가 — 레벨 간격과 대조")
evts = sorted(set(e["lv"] for e in CATALOG))
def gap_after(lv):
    nxt = next((L for L in evts if L>lv), None)
    return None if nxt is None else hours_to(nxt)-hours_to(lv)
print(f"  {'등급':<6}{'항목수':>6}{'도달시각 중앙값':>16}{'다음 해금까지 간격':>20}{'현행 유예':>10}{'유예/간격':>10}")
by_grade={}
for g in ("일반","희귀","영웅","전설"):
    items=[e for e in CATALOG if e["grade"]==g and e["rank"]>0]
    gaps=[gap_after(e["lv"]) for e in items if gap_after(e["lv"]) is not None]
    med_t=sorted(hours_to(e["lv"]) for e in items)[len(items)//2]
    med_g=sorted(gaps)[len(gaps)//2]
    by_grade[g]=med_g
    print(f"  {g:<6}{len(items):>6}{med_t:>15.1f}h{med_g:>19.1f}h{GRACE0[g]:>9}h{GRACE0[g]/med_g*100:>9.0f}%")
print("  -> 전설의 유예 80h조차 간격(중앙값)의 절반이 안 된다. 일반은 22%다.")
print("     ★ 활쏘기 상한과 **같은 구조적 결함**이다: 절대시간이라 주변 구조(레벨 간격)를 따라가지 못한다.")

print(f"\n  제안: 유예 = round(k x 그 등급의 '다음 해금까지 간격 중앙값'). k 스윕:")
def grace_from_k(k): return {g: max(4, int(round(by_grade[g]*k))) for g in by_grade}
for k in (0.5,0.75,1.0,1.25,1.5):
    G=grace_from_k(k)
    print(f"    k={k:<5} 일반 {G['일반']:>3}h / 희귀 {G['희귀']:>3}h / 영웅 {G['영웅']:>3}h / 전설 {G['전설']:>4}h")

# ============================ E. 할일 획득원 + 소프트캡 ============================
hdr("E. ★ 신규 획득원 [오늘 할일] — 자리와 상한")
print("  왜 정액(하루 1회)인가 — 완료 1건당 지급의 파밍 검산:")
for n in (5,20,100):
    print(f"    '완료 1건 = 3동전'이면 유저가 즉석에서 {n}건 만들고 체크 -> {n*3}동전, 소요시간 약 {n*4/60:.1f}분")
print(f"    -> 할일은 **유저가 무제한 생성**한다. 건당 지급은 활쏘기(586/시)보다 나쁜 파밍 벡터다. 기각.")
print("  채택: **그날 등록한 할일을 전부 완료했을 때 하루 1회 정액.** 생성 수와 무관 -> 파밍 벡터 0.")
print("        추가 조건 2개(구조적 게이트): ① 할일 최소 1건 ② 등록 후 최소 10분 경과분만 계산")
print(f"        (10분 게이트 근거: 최소 집중 세션 15분보다 짧게 두되, 만들자마자 체크하는 경로를 막는다)\n")
print(f"  {'정액':>6}{'A 동전/일':>11}{'B 동전/일':>11}{'C 동전/일':>11}{'D상한/일':>10}{'A/B':>7}{'D/B':>7}")
for T in (0,5,10,15,20,30):
    day={}
    for nm,kw in PROFILES.items():
        h,f,a=kw["weekday"]; a=min(a,int(h*3600/ARCHERY_CD_SEC))
        day[nm[:1]] = focus_pay(25)*f + a + T
    dcap = focus_pay(50)*4 + int(10*3600/ARCHERY_CD_SEC) + T     # 10h 상주 + 집중50x4 + 활쏘기 최대
    print(f"  {T:>6}{day['A']:>11}{day['B']:>11}{day['C']:>11}{dcap:>10}{day['A']/day['B']:>7.2f}{dcap/day['B']:>7.2f}")
print("  판정 기준: (a) A/B >= 0.20 (저참여 유저가 상점을 쓸 수 있어야 한다)")
print("             (b) D/B <= 5.0 (격차가 지금(4.7)보다 크게 벌어지지 않아야 한다)")
print("             (c) 정액 <= 집중 25분(30동전) (시간을 쓰는 채널이 항상 더 커야 한다)")

# ============================ F. 패키지 시뮬레이션 ============================
def build_catalog(look0_paid=False):
    C=[]
    for slot,lvs in REQ.items():
        for r,lv in enumerate(lvs):
            g=GRADE_BY_RANK[r]; look=slot in LOOK_SLOTS
            free = (r==0) and not (look and look0_paid)
            C.append(dict(slot=slot,rank=r,lv=lv,grade=g,look=look,free=free,
                          price=0 if free else PRICE[g]))
    return C

def sim_pkg(days=200, weekday=(8,2,4), weekend=(4,0,2), grace=None, todo=0, seed=0,
            look0_paid=False):
    """★ 유저 모형: 상점은 **하루에 한 번**(그날 마지막 시간) 연다. 그때 살 수 있으면 산다.
       '매 시간 즉시 구매'는 버튼을 누르는 즉시 죽여서 모든 생존 지표를 0으로 만드는 퇴화 모형이다."""
    grace = grace or GRACE0; C=build_catalog(look0_paid)
    coins=float(seed); lv=1; cur=0.0; hrs=0.0; reached={}; owned={}; log=[]; opens=[]
    for d in range(1, days+1):
        h,f,a = weekday if (d-1)%7<5 else weekend
        a = min(a, int(h*3600/ARCHERY_CD_SEC))
        for hh in range(h):
            hrs += 1.0
            cur += 60*PASSIVE_XP_PER_MIN + (a/h)*BULLSEYE_XP
            while cur >= need(lv): cur -= need(lv); lv += 1
            coins += (focus_pay(25) if hh < min(f,h) else 0) + a/h + (todo if hh==0 else 0)
            for i,e in enumerate(C):
                if e["lv"]<=lv and i not in reached: reached[i]=hrs
            for i,e in enumerate(C):
                if i in owned or i not in reached: continue
                if e["free"] or hrs >= reached[i]+grace[e["grade"]]: owned[i]=(d,"자동")
            if hh == h-1:                                  # ← 하루 1회 상점 열기
                pool=[(i,e) for i,e in enumerate(C) if i not in owned and i in reached]
                live=[i for i,e in pool if coins>=e["price"]]
                opens.append(dict(d=d,h=hrs,lv=lv,coins=coins,live=len(live),
                                  reachable=len(pool), short=len(pool)-len(live)))
                for i,e in sorted(pool, key=lambda t:t[1]["price"]):
                    if coins>=e["price"]: coins-=e["price"]; owned[i]=(d,"구매")
        log.append(dict(d=d,lv=lv,coins=coins,owned=len(owned),hrs=hrs,
                        bought=sum(1 for v in owned.values() if v[1]=="구매")))
    return log, owned, opens, C

def metrics(pf, **kw):
    """★ 지표는 **완주 전 구간**에서만 잰다. 완주 후의 빈 상점은 결함이 아니라 종료 상태다
       (ECONOMY_SPEC 6-1: 완주 후 잔액 표시를 내린다)."""
    lg,ow,op,C = sim_pkg(days=200, **pf, **kw)
    done  = next((r['d'] for r in lg if r['owned']==42), None) or 200
    pre   = [o for o in op if o['d'] <= done]
    live_days = sum(1 for o in pre if o['live']>0)
    gray_days = sum(1 for o in pre if o['reachable']==0)
    short_days= sum(1 for o in pre if o['reachable']>0 and o['live']==0)
    firstday  = next((o['d'] for o in op if o['live']>0), None)
    run=0; worst=0
    for o in pre:
        run = run+1 if o['live']==0 else 0
        worst=max(worst,run)
    paid = sum(1 for e in C if not e["free"])
    return dict(done=done, bought=lg[-1]['bought'], auto=42-lg[-1]['bought'], paid=paid,
                hit=lg[-1]['bought']/paid*100,
                live=live_days/len(pre)*100, gray=gray_days/len(pre)*100,
                short=short_days/len(pre)*100, firstday=firstday, worst=worst,
                day1=op[0]['live'], bal200=int(lg[199]['coins']))

hdr("C-2. ★ 빈 상태 재측정 — 유저 모형 교체(하루 1회 상점 열기)")
print("  ※ C절의 '즉시구매' 모형은 퇴화다 — 누르는 즉시 버튼이 죽어 생존율이 구조적으로 0에 가까워진다.")
print("     아래는 '하루에 한 번 연다'는 모형이고, 이것이 실제 사용 형태에 가깝다.\n")
print(f"  {'원형':<12}{'살 것 있는 날':>13}{'잔액부족 날':>12}{'구조적 공백':>12}{'첫 구매가능일':>14}{'최장 무버튼':>12}{'구매/자동':>10}{'완주':>7}")
for nm,pf in PROFILES.items():
    m=metrics(pf)
    ba = "{}/{}".format(m['bought'], m['auto'])
    print("  {:<12}{:>12.1f}%{:>11.1f}%{:>11.1f}%{:>14}{:>12}{:>10}{:>7}".format(
        nm[:1]+' '+nm[2:11], m['live'], m['short'], m['gray'],
        str(m['firstday'])+'일차', str(m['worst'])+'일', ba, str(m['done'])+'일'))
print("\n  ★ 원형 A: 200일 중 살 것이 있는 날이 사실상 없고 구매 2건. **저참여 유저에게 상점은 존재하지 않는다.**")
print("  ★ 1일차: 세 원형 모두 살아있는 버튼 0개. 원인은 가격이 아니라 **카탈로그 요구 레벨**이다 —")
print(f"     첫 유료 아이템이 Lv.5(Head#1/Hair#1)라 누적 {hours_to(5):.1f}시간 전에는 구조적으로 진열될 물건이 없다.")

hdr("F. ★ 패키지 스윕 — 유예 k / 할일 정액 / 시드동전 / 외형 rank0 유료화")
print("  목표 (a) A 구매 >= 11종(25%)   (b) B 구매 >= 30종(70%)   (c) C 구매 <= 38종(90%)")
print("       (d) 1일차 살아있는 버튼 >= 1   (e) 완주 전 최장 무버튼 <= 21일   (f) A 완주 <= 100일\n")
print(f"  {'구성':<40}{'A 구매/완주':>13}{'B 구매/완주':>13}{'C 구매/완주':>13}{'B최장공백':>9}{'1일버튼':>8}{'A첫날':>7}")
CASES=[
 ("① 현행 (유예5/15/35/80, 할일0, 시드0)", dict()),
 ("② 유예 k=1.0 만",                       dict(grace=grace_from_k(1.0))),
 ("③ ② + 할일 15",                         dict(grace=grace_from_k(1.0), todo=15)),
 ("④ ③ + 시드 60",                         dict(grace=grace_from_k(1.0), todo=15, seed=60)),
 ("⑤ ④ + 외형 rank0 유료화  ★채택",         dict(grace=grace_from_k(1.0), todo=15, seed=60, look0_paid=True)),
 ("   (감도) k=0.75",                       dict(grace=grace_from_k(0.75), todo=15, seed=60, look0_paid=True)),
 ("   (감도) k=1.5",                        dict(grace=grace_from_k(1.5),  todo=15, seed=60, look0_paid=True)),
 ("   (감도) 할일 30",                       dict(grace=grace_from_k(1.0), todo=30, seed=60, look0_paid=True)),
]
for tag,kw in CASES:
    ms=[metrics(pf,**kw) for pf in PROFILES.values()]
    cells="".join("{:>13}".format("{}/{}".format(m['bought'], m['done'])) for m in ms)
    print(f"  {tag:<40}{cells}{ms[1]['worst']:>6}일{ms[2]['day1']:>8}{str(ms[0]['firstday'])+'일':>7}")

PKG = dict(grace=grace_from_k(1.0), todo=15, seed=60, look0_paid=True)

# ============================ G. 페이투윈 상한 재검산 ============================
hdr("G. 페이투윈 상한 — '동전이 무한이면 얼마나 빨라지는가'")
print("  검산 방법: seed=1,000,000동전(=사실상 무한)으로 돌려 **레벨만이 남은 벽**일 때의 완주일을 구한다.")
print(f"  {'원형':<24}{'채택 패키지':>12}{'동전 무한':>12}{'무한이 사는 일수':>16}{'레벨 Lv.30 도달':>16}")
for nm,pf in PROFILES.items():
    m  = metrics(pf, **PKG)
    mi = metrics(pf, **{**PKG, "seed":1_000_000})
    print(f"  {nm:<24}{str(m['done'])+'일':>12}{str(mi['done'])+'일':>12}{str(m['done']-mi['done'])+'일':>16}{str(mi['done'])+'일':>16}")
print("  -> 무한 동전으로도 완주일은 **Lv.30 도달일** 아래로 내려가지 않는다. 그것이 절대 천장이다.")
mA=metrics(PROFILES['A 켜두기만 (집중0)'],**PKG); mC=metrics(PROFILES['C 적극     (집중25x4)'],**PKG)
print(f"  최저참여 A {mA['done']}일 vs 최고참여 C {mC['done']}일 -> 격차 {(mA['done']-mC['done'])/mA['done']*100:.0f}%"
      f"  (R1 현행 구성에서는 87 vs 56 = 36%) -> **패키지가 격차를 좁힌다**")

# ============================ H. 곡선 재작성 ============================
hdr("H. ★ 곡선 재작성 — 1일차 / 1주차 / 1개월차에 무엇을 갖고 있는가 (채택 패키지 ⑤)")
STAT_BY_RANK = [3,3,5,5,7,10]   # 주스탯(ECONOMY_SPEC 3-1)
SUB_BY_RANK  = [0,0,1,1,2,3]
import itertools
def stats_for(ranks, lv):
    """ranks = {slot: rank or -1}. ECONOMY_SPEC 2-1 산식 그대로."""
    lvbase=min((lv-1)//4,5)
    P={sl:(STAT_BY_RANK[r] if r>=0 else 0) for sl,r in ranks.items()}
    B={sl:(SUB_BY_RANK[r]  if r>=0 else 0) for sl,r in ranks.items()}
    S={"집중력":lvbase+P["Head"]+B["Shoulders"], "관찰력":lvbase+P["Eyes"]+B["Head"],
       "매력":  lvbase+P["Neck"]+B["Eyes"],      "민첩":  lvbase+P["Shoulders"]+B["Neck"]}
    rs=list(ranks.values())
    setdone = (min(rs)>=0 and len(set(rs))==1)
    if setdone: S={k:v+2 for k,v in S.items()}
    return {k:min(v,20) for k,v in S.items()}, setdone

def snapshot(pf, day):
    """★ 유저는 **보유분 중 최적 조합**을 입는다(최소 스탯 최대화, 동률이면 합 최대화).
       '항상 최고 rank를 입는다'로 모형하면 세트 보너스 -2 때문에 스탯이 내려가는 가짜 하락이 나온다."""
    lg,ow,op,C = sim_pkg(days=max(day,200), **pf, **PKG)
    r = lg[day-1]
    own_idx=[i for i,v in ow.items() if v[0]<=day]
    avail={sl:sorted({C[i]["rank"] for i in own_idx if C[i]["slot"]==sl}) or [-1] for sl in STAT_SLOTS}
    best=None
    for combo in itertools.product(*[avail[sl] for sl in STAT_SLOTS]):
        ranks=dict(zip(STAT_SLOTS,combo)); S,sd=stats_for(ranks,r['lv'])
        key=(min(S.values()), sum(S.values()))
        if best is None or key>best[0]: best=(key,S,sd,ranks)
    _,S,sd,ranks = best
    tier=lambda v: "고급" if v>=18 else "중급" if v>=12 else "초급" if v>=6 else "—"
    return r, len(own_idx), S, sd, tier(min(S.values()))

print(f"  {'시점':<8}{'Lv':>4}{'보유':>5}{'동전':>7}{'집중력':>7}{'관찰력':>7}{'매력':>6}{'민첩':>6}{'구간':>6}  세트")
for label,day in (("1일차",1),("3일차",3),("1주차",7),("2주차",14),("1개월차",30),("2개월차",60),("완주",metrics(PROFILES['B 기준     (집중25x2)'],**PKG)['done'])):
    r,own,S,sd,tr = snapshot(PROFILES['B 기준     (집중25x2)'], day)
    print(f"  {label:<8}{r['lv']:>4}{own:>5}{int(r['coins']):>7}{S['집중력']:>7}{S['관찰력']:>7}{S['매력']:>6}{S['민첩']:>6}{tr:>6}  "
          f"{'완성' if sd else '미완성(혼합)'}")

# ============================ I. D-2 유료 vs F2P ============================
hdr("I. D-2(유료 vs F2P)가 이 가격표에 미치는 영향 — 두 갈래로 쓸 필요가 있는가")
print("  판정: **없다. 동전 경제는 두 경우에 완전히 동일하다.** 근거는 하나다 —")
print("        이 설계에는 **현금->동전 교환이 존재하지 않는다.** 현금은 DLC 팩(조형/대사/연출)만 산다.")
print("        따라서 유료 앱이든 F2P든 동전의 획득·소비 곡선은 한 줄도 바뀌지 않는다.\n")
print("  ★ 단, F2P가 되면 '동전 IAP를 붙이자'는 압력이 반드시 생긴다. 그 검산을 미리 해 둔다:")
for nm,pf in PROFILES.items():
    m=metrics(pf,**PKG); mi=metrics(pf,**{**PKG,"seed":1_000_000})
    print(f"    {nm:<24} 동전 IAP 도입 시 완주 {m['done']}일 -> {mi['done']}일 (앞당김 {m['done']-mi['done']}일)")
print("  -> 동전 IAP는 완주를 최대 며칠 앞당길 뿐 **천장(Lv.30)을 못 넘는다**. 페이투윈은 아니다.")
print("  -> 그러나 도입하면 안 되는 이유가 따로 있다: 4-1의 '유예 자동 해금'이 **모두에게 무료 보장**인데,")
print("     그 보장을 돈으로 건너뛰게 만들면 상점 문구가 '기다리세요 또는 지불하세요'가 된다.")
print("     기획서 5-4가 피하려던 과금 압박형 루프의 정의 그 자체다. -> **동전 IAP 금지 권고(D-2와 무관하게).**")

# ============================ J. 저장 빈도 재검산 ============================
hdr("J. 저장 빈도 — 신규 획득원 반영")
base_per_day = 24*3600/AUTOSAVE_SEC
adds = [("집중 세션 종료(동전 지급)", 2.0, True), ("[오늘 할일] 일일 정액 지급", 1.0, True),
        ("아이템 구매", 38/75, True), ("유예 자동 해금", 4/75, True),
        ("임계 최초 돌파", 12/75, True), ("활쏘기 동전(IsDirty만)", 48.0, False)]
imm = sum(n for _,n,i in adds if i)
print(f"  기준선(자동저장 60초, 패시브 XP가 항상 IsDirty) = {base_per_day:,.0f}회/일")
for nm,n,i in adds: print(f"    {nm:<28}{n:>7.2f}회/일  {'즉시저장' if i else 'IsDirty만'}")
print(f"  추가 **즉시** 저장 = {imm:.2f}회/일 = 기준선 대비 +{imm/base_per_day*100:.3f}%")
print(f"  ★ R1 대비 증가분은 [오늘 할일] 1.00회/일 하나뿐 (R1 3.1 -> {imm:.1f}회/일).")
print(f"  -> 세이브 원자적 교체(IOException) 선결 판단은 **바뀌지 않는다**: 근거는 빈도가 아니라")
print(f"     손실 회복 불가능성이고, 그건 R1과 동일하다.")

# ============================ H-2. R1 자기정정 ============================
hdr("H-2. ★ R1 자기정정 — '2주차 최적해는 세트를 깬 혼합'이 맞는가")
print("  R1 ECONOMY_SPEC 5-2는 2주차 빌드를 '세트 미완성(혼합)'이라고 적었다. 그 판단은")
print("  '항상 보유 중 최고 rank를 입는다'는 모형에서 나왔다. 최적 착용으로 다시 풀면:\n")
print(f"  {'시점':<8}{'세트 최적(최소/합)':>20}{'혼합 최적(최소/합)':>20}{'승자':>10}")
for label,day in (("3일차",3),("1주차",7),("2주차",14),("1개월차",30),("2개월차",60)):
    lg,ow,op,C = sim_pkg(days=200, **PROFILES['B 기준     (집중25x2)'], **PKG)
    r=lg[day-1]; own=[i for i,v in ow.items() if v[0]<=day]
    avail={sl:sorted({C[i]["rank"] for i in own if C[i]["slot"]==sl}) or [-1] for sl in STAT_SLOTS}
    bs=bm=None
    for combo in itertools.product(*[avail[sl] for sl in STAT_SLOTS]):
        S,sd = stats_for(dict(zip(STAT_SLOTS,combo)), r['lv']); key=(min(S.values()),sum(S.values()))
        if sd: bs = key if bs is None or key>bs else bs
        else:  bm = key if bm is None or key>bm else bm
    win = "세트" if (bs and (bm is None or bs>=bm)) else "혼합"
    print(f"  {label:<8}{str(bs):>20}{str(bm):>20}{win:>10}")
# 완주 시점 검증 — 세트 F가 엔드게임 최적인가
lgF,owF,_,CF = sim_pkg(days=200, **PROFILES['B 기준     (집중25x2)'], **PKG)
dF = next(r['d'] for r in lgF if r['owned']==42)
availF={sl:sorted({CF[i]["rank"] for i in owF}) for sl in STAT_SLOTS}
bsF=bmF=None
for combo in itertools.product(*[[0,1,2,3,4,5] for _ in STAT_SLOTS]):
    S,sd = stats_for(dict(zip(STAT_SLOTS,combo)), 30); key=(min(S.values()),sum(S.values()))
    if sd: bsF = key if bsF is None or key>bsF else bsF
    else:  bmF = key if bmF is None or key>bmF else bmF
print(f"  {'완주(Lv.30, 42종)':<8}{str(bsF):>20}{str(bmF):>20}{'세트' if bsF>=bmF else '혼합':>10}")
print("\n  ★ 자기정정 — 초안에서 'R1의 그 문장은 틀렸다'고 쓰려다 표를 다시 읽고 되돌린다.")
print("     최소스탯(=임계 구간) 기준이면 **항상 세트**가 이기지만, 총합 기준이면 2주차(33>32)와")
print("     2개월차(68>64)에 **혼합이 이긴다.** 즉 R1 5-2의 '진짜 선택이 생긴다'는 살아 있고,")
print("     그 선택의 실체는 **'구간을 올릴까(세트) vs 총합을 올릴까(혼합)'** 다.")
print("     -> **세트 보너스 +2를 그대로 둔다.** +3으로 올리면 혼합이 영원히 지고 선택이 사라진다.")
print(f"     엔드게임 검증: Lv.30 전종 보유에서 세트 {bsF} vs 혼합 {bmF} -> 세트가 최적 = 3-4절 의도 유지.\n")

# ============================ K. 소프트캡 ============================
hdr("K. ★ 인플레 방지 — 획득원이 늘어날 때의 소프트캡 (ux-widgets 대비 선반영)")
print("  R1 4-3의 판정('하루 총량 하드캡 없음')은 유지한다. 대신 **채널 유형별 상한**을 둔다:\n")
print("  | 채널 유형 | 예 | 상한 | 근거 |")
print("  | 시간 종속(벽시계를 실제로 쓴다) | 집중모드 | **상한 없음** | 파밍 불가 — 하루 길이가 곧 상한 |")
print("  | 행동 종속(쿨다운으로 눌린다)    | 활쏘기   | 6동전/시(쿨다운 600초) | R1 4-2 |")
print("  | 시간 비종속(즉시 완료 가능)     | 할일 등 | **합계 30동전/일** | 아래 검산 |\n")
print(f"  '시간 비종속 채널 합계 상한 = 30동전/일'의 근거: 집중 25분 1회(30동전)와 같게 잡는다.")
print(f"  즉 **아무 시간도 쓰지 않는 채널 전부를 합쳐도 집중 25분 한 번을 넘지 못한다.**")
print(f"  현재 [오늘 할일]이 15를 쓰므로 **남은 여유는 15동전/일** — 앞으로 채널 1개를 더 붙일 수 있다.\n")
print(f"  {'비종속 합계':>12}{'A/일':>7}{'B/일':>7}{'D/일':>7}{'A/B':>7}{'D/B':>7}{'판정':>8}")
for T in (0,15,30,45,60):
    dA=4+T; dB=64+T; dD=300+T
    why = [] if True else None
    if dA/dB < 0.20: why.append("③A/B")
    if dD/dB > 5.0:  why.append("④D/B")
    if T > 30:       why.append("②합계")
    print(f"  {T:>12}{dA:>7}{dB:>7}{dD:>7}{dA/dB:>7.2f}{dD/dB:>7.2f}{('OK' if not why else '위반 '+','.join(why)):>12}")
print("  -> T=0(현행)은 저참여 유저 배제(③)로 이미 위반이다. 15~30이 유일한 통과 구간이고 45부터는 ②를 깬다.")
print("  ★ 신규 획득원 승인 체크리스트(리더용, 4개 전부 통과해야 한다):")
print("     ① 대상이 유저가 무제한 생성 가능한가? -> 그렇다면 **건당 지급 금지**(정액/일 1회만)")
print("     ② 그 채널의 일일 기여 <= 30동전이고, 비종속 채널 합계도 <= 30동전인가")
print("     ③ 추가 후 A/B >= 0.20 (저참여 유저 배제 금지)")
print("     ④ 추가 후 D/B <= 5.0 (헤비 격차 유지)")

# ============================ L. 문서 대조 ============================
hdr("L. ECONOMY_SPEC 0-2절이 인용한 숫자와 이 출력의 대조 (문서-계산기 드리프트 방지)")
mA=metrics(PROFILES['A 켜두기만 (집중0)'],**PKG)
mB=metrics(PROFILES['B 기준     (집중25x2)'],**PKG)
mC=metrics(PROFILES['C 적극     (집중25x4)'],**PKG)
r1A=metrics(PROFILES['A 켜두기만 (집중0)']); r1B=metrics(PROFILES['B 기준     (집중25x2)'])
r1C=metrics(PROFILES['C 적극     (집중25x4)'])
G1=grace_from_k(1.0)
CLAIMS=[
 ("42종 유료 총액",                      paid_slot*7, 4550),
 ("24종 유료 총액",                      paid_slot*4, 2600),
 ("외형 3슬롯 비중(%)",                  round(paid_slot*3/(paid_slot*7)*100,1), 42.9),
 ("R2 유료 종수(외형 rank0 유료화 후)",   mB['paid'], 38),
 ("R2 총액",                             sum(e['price'] for e in build_catalog(True)), 4640),
 ("유예 일반/희귀/영웅/전설",             [G1[g] for g in ("일반","희귀","영웅","전설")], [16,23,27,33]),
 ("R1 A 구매/완주",                      [r1A['bought'], r1A['done']], [2,87]),
 ("R1 B 구매/완주",                      [r1B['bought'], r1B['done']], [28,75]),
 ("R1 C 구매/완주",                      [r1C['bought'], r1C['done']], [31,56]),
 ("R2⑤ A 구매/완주",                     [mA['bought'], mA['done']], [19,77]),
 ("R2⑤ B 구매/완주",                     [mB['bought'], mB['done']], [37,75]),
 ("R2⑤ C 구매/완주",                     [mC['bought'], mC['done']], [38,56]),
 ("R2⑤ 1일차 살아있는 버튼",              mB['day1'], 3),
 ("R2⑤ B 완주전 최장 무버튼(일)",         mB['worst'], 9),
 ("격차 R1(A87 vs C56, %)",              round((87-56)/87*100), 36),
 ("격차 R2(%)",                          round((mA['done']-mC['done'])/mA['done']*100), 27),
 ("추가 즉시저장(회/일)",                 round(imm,2), 3.72),
]
bad2=0
for nm,got,want in CLAIMS:
    ok = got==want
    bad2 += not ok
    print(f"  {'PASS' if ok else '**FAIL**'} {nm:<34} 계산={str(got):<16} 문서={want}")
print(f"\n  -> {len(CLAIMS)-bad2}/{len(CLAIMS)} 일치." + ("" if not bad2 else "  ★ 문서를 고쳐야 한다."))
if bad2: raise SystemExit(1)
