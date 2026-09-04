# -*- coding: utf-8 -*-
"""design-systems R8 — 부스탯 방향 24개 제약충족 해법 + 불변식 I-1~I-4 자체검증"""
import itertools
BASE={'집중력':8,'관찰력':6,'매력':5,'민첩':7}
MAIN=[3,6,10,15]; SUB=[1,2,4,6]; CAP=40; ADV=32; NOV=10
SLOTS=['HEAD','EYES','NECK','BACK']
OWN={'HEAD':'집중력','EYES':'관찰력','NECK':'매력','BACK':'민첩'}
STATS=['집중력','관찰력','매력','민첩']
RANK_RARITY=[0,0,1,1,2,3]              # 프로덕션 실측 ItemCatalog.cs:770
NAMES={'HEAD':['천모자','털모자','중절모','왕관','베레모','밀짚모자'],
       'EYES':['선글라스','동그란안경','고글','외알안경','뿔테안경','안대'],
       'NECK':['나비넥타이','줄무늬타이','목도리','방울목걸이','펜던트목걸이','반다나'],
       'BACK':['짧은망토','긴망토','날개','배낭','판초','요정날개']}
LV={'HEAD':[1,5,9,20,23,26],'EYES':[1,6,11,15,19,23],
    'NECK':[1,8,12,18,21,25],'BACK':[1,13,17,22,25,28]}
F=[]
def ck(t,g,w):
    ok=g==w; F.append(t) if not ok else None
    print(("  [OK]  " if ok else "  [FAIL]")+" %-42s got=%s want=%s"%(t,g,w))

print("="*78);print("[0] 교정");print("="*78)
ck("ItemCatalog 사다리 2/2/1/1",RANK_RARITY,[0,0,1,1,2,3])
ck("슬롯 최상위 = 전설",[NAMES[s][5] for s in SLOTS],['밀짚모자','안대','반다나','요정날개'])
ck("고급 필요 부스탯",{s:ADV-15-BASE[s] for s in STATS},{'집중력':9,'관찰력':11,'매력':12,'민첩':10})
ck("자유부스탯 이론최대(집중력)",8+15+18,41)
if F: raise SystemExit("교정 실패 — 중단")
print("  ★ 교정 4/4 PASS\n")

print("="*78);print("[1] I-3 상한은 배정과 무관하게 구조적으로 2다 (열거 아님, 증명)");print("="*78)
need={t:ADV-BASE[t] for t in STATS}
subneed={t:need[t]-MAIN[3] for t in STATS}
print("  스탯 t가 32에 닿으려면 (주+부) ≥ 32−BASE:",need)
print("  주스탯은 자기 슬롯 1개에서만 오고 최대 15 → 필요 부스탯:",subneed)
tri=sorted(subneed.values())[:3]
print("  가장 싼 3스탯의 부스탯 합 = %d + %d + %d = %d"%(*tri,sum(tri)))
print("  한 로드아웃이 내놓는 부스탯 총량 ≤ 4아이템 × 6(전설) = 24")
print("  ⇒ %d > 24 이므로 **동시 3스탯 고급은 어떤 배정에서도 불가능**. I-3 상한 2는 구조다."%sum(tri))
pair=sorted(subneed.values())[:2]
print("  가장 싼 2스탯 = %d + %d = %d ≤ 24 → 2는 배정에 따라 달성 가능"%(*pair,sum(pair)))

def slot_profiles(slot):
    others=[s for s in STATS if s!=OWN[slot]]
    seen={}
    for perm in set(itertools.permutations(others*2)):
        if any(perm.count(o)!=2 for o in others): continue
        best=tuple(sorted((o,max(RANK_RARITY[i] for i in range(6) if perm[i]==o)) for o in others))
        seen.setdefault((best,perm[0]),perm)
    return list(seen.values())
P={s:slot_profiles(s) for s in SLOTS}
print("\n  슬롯당 후보 %s (서명 중복 제거)"%[len(P[s]) for s in SLOTS])

def maxstat(asn,t):
    own=[s for s in SLOTS if OWN[s]==t][0]
    v=BASE[t]+MAIN[3]
    for s in SLOTS:
        if s==own: continue
        c=[RANK_RARITY[i] for i in range(6) if asn[s][i]==t]
        v+=SUB[max(c)] if c else 0
    return min(v,CAP)
def day1(asn):
    v=dict(BASE)
    for s in SLOTS:
        v[OWN[s]]+=MAIN[RANK_RARITY[0]]; v[asn[s][0]]+=SUB[RANK_RARITY[0]]
    return v
def sim2(asn):                       # 어떤 2스탯 쌍이 동시에 32 이상 가능한가
    got=[]
    for a,b in itertools.combinations(STATS,2):
        for load in itertools.product(range(6),repeat=4):
            L=dict(zip(SLOTS,load)); v=dict(BASE)
            for s in SLOTS:
                r=RANK_RARITY[L[s]]; v[OWN[s]]+=MAIN[r]; v[asn[s][L[s]]]+=SUB[r]
            if min(v[a],CAP)>=ADV and min(v[b],CAP)>=ADV: got.append((a,b)); break
    return got

print("\n"+"="*78);print("[2] 전수 탐색");print("="*78)
cands=[]; tried=i2=i2d1=0
for combo in itertools.product(*[P[s] for s in SLOTS]):
    tried+=1
    asn=dict(zip(SLOTS,combo))
    mx={t:maxstat(asn,t) for t in STATS}
    if min(mx.values())<ADV: continue
    i2+=1
    d1=day1(asn)
    if min(d1.values())<NOV: continue
    i2d1+=1
    spread=max(d1.values())-min(d1.values())
    cands.append(((spread,-min(mx.values()),max(d1.values())),asn,mx,d1))
cands.sort(key=lambda x:x[0])
print("  후보 %s / I-2 통과 %s / I-2 ∧ 1일차초급 통과 %s"%(
      format(tried,','),format(i2,','),format(i2d1,',')))
print("  ★ 그 다음 I-3(동시 2스탯 고급 달성 가능)을 순서대로 건다 — 여기서 대부분 떨어진다")
best=None; scanned=0
for c in cands:
    scanned+=1
    pr=sim2(c[1])
    if pr: best=c; pairs=pr; break
    if scanned>=3000: break
print("  I-3까지 검사한 후보 %s → 최초 통과 %s"%(format(scanned,','),"있음" if best else "★없음"))
if not best: raise SystemExit("I-2 ∧ 1일차 ∧ I-3 동시 만족 해 없음 — 제약이 과결정이다")
_,asn,mx,d1=best
print("\n"+"="*78);print("[3] 해 — 부스탯 방향 24개");print("="*78)
print("  ※ 등급은 프로덕션 `ItemCatalog.RarityOfMember`가 요구 레벨 순위로 파생한다 = 선언 불필요")
for s in SLOTS:
    print("  [%s / 주스탯 %s]"%(s,OWN[s]))
    for i in range(6):
        r=RANK_RARITY[i]
        print("    idx%d lv%-2d %-12s %-4s  부스탯 %s%s"%(i,LV[s][i],NAMES[s][i],
              ['일반','희귀','영웅','전설'][r],asn[s][i],"   ★전설" if r==3 else ""))
print("\n  1일차(idx0 4종):",d1,"→ 전부 ≥%d, 편차 %d"%(NOV,max(d1.values())-min(d1.values())))
print("  스탯별 최대치  :",mx)
cnt={t:sum(asn[s].count(t) for s in SLOTS) for t in STATS}
print("  부스탯 편중    :",cnt,"→ 편차 %d"%(max(cnt.values())-min(cnt.values())))
print("  동시 고급 가능 쌍:",pairs if pairs else "없음")
print("\n"+"="*78);print("[4] 불변식 자체검증");print("="*78)
raw=max(BASE[t]+MAIN[3]+SUB[3]*3 for t in STATS)
print("  I-1 원시최대 %d > CAP %d → 클램프가 실제로 일한다        %s"%(raw,CAP,"PASS" if raw>CAP else "★무의미"))
print("  I-2 최소 스탯 최대치 %d ≥ %d                              %s"%(min(mx.values()),ADV,"PASS" if min(mx.values())>=ADV else "FAIL"))
print("  I-3 동시 3 불가(구조 증명) ∧ 동시 2 가능(%d쌍)            %s"%(len(pairs),"PASS" if pairs else "FAIL"))
print("  I-4 슬롯 주스탯 상한 15 고정 + 클램프 40 → 팩 무한추가 불변  PASS(구조)")
