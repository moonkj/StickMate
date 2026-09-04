# -*- coding: utf-8 -*-
"""R8 곡선 — 24종 확정 후. 레벨 게이트(출하 사실) + 동전 예산 동시 적용"""
import itertools
BASE={'집중력':8,'관찰력':6,'매력':5,'민첩':7}
MAIN=[3,6,10,15]; SUB=[1,2,4,6]; CAP=40; ADV=32; MID=20; NOV=10
PRICE=[600,1400,3200,9600]; RANK=[0,0,1,1,2,3]; DAY=1280.0
SLOTS=['HEAD','EYES','NECK','BACK']; OWN={'HEAD':'집중력','EYES':'관찰력','NECK':'매력','BACK':'민첩'}
STATS=['집중력','관찰력','매력','민첩']
LV={'HEAD':[1,5,9,20,23,26],'EYES':[1,6,11,15,19,23],'NECK':[1,8,12,18,21,25],'BACK':[1,13,17,22,25,28]}
NM={'HEAD':['천모자','털모자','중절모','왕관','베레모','밀짚모자'],'EYES':['선글라스','동그란안경','고글','외알안경','뿔테안경','안대'],
    'NECK':['나비넥타이','줄무늬타이','목도리','방울목걸이','펜던트목걸이','반다나'],'BACK':['짧은망토','긴망토','날개','배낭','판초','요정날개']}
SUBT={'HEAD':['민첩','관찰력','매력','민첩','관찰력','매력'],
      'EYES':['매력','민첩','집중력','매력','민첩','집중력'],
      'NECK':['관찰력','민첩','집중력','집중력','관찰력','민첩'],
      'BACK':['매력','집중력','관찰력','집중력','관찰력','매력']}
# ECONOMY_SPEC 5-1 실측 레벨 곡선 (8h/일)
LVDAY=[(1,0.0),(5,1.5),(9,5.4),(13,12.0),(17,21.2),(20,29.9),(25,47.7),(28,60.5),(30,69.8)]
def level_at(d):
    L=1
    for lv,dd in LVDAY:
        if d>=dd: L=lv
    return L
F=[]
def ck(t,g,w):
    ok=g==w; F.append(t) if not ok else None
    print(("  [OK]  " if ok else "  [FAIL]")+" %-40s got=%s want=%s"%(t,g,w))
print("="*76);print("[0] 교정");print("="*76)
ck("1일차 4종 = R8 해",{s:(NM[s][0],SUBT[s][0]) for s in ['HEAD']},{'HEAD':('천모자','민첩')})
ck("Lv at day30",level_at(30),20)
ck("Lv at day7",level_at(7),9)
ck("전설 가격",PRICE[3],9600)
if F: raise SystemExit("교정 실패")
print("  ★ 교정 4/4 PASS\n")
print("="*76);print("[1] 곡선 — 레벨 게이트(출하) ∧ 동전 예산 ∧ R8 부스탯 배정");print("="*76)
def stat(load):
    v=dict(BASE)
    for s in SLOTS:
        i=load[s]; r=RANK[i]; v[OWN[s]]+=MAIN[r]; v[SUBT[s][i]]+=SUB[r]
    return {k:min(x,CAP) for k,x in v.items()}
print("   일차  Lv   예산      집중력 관찰력 매력 민첩   초급 중급 고급  비고")
prev=None
for d in [1,3,7,14,30,45,60,75,90]:
    L=level_at(d); bud=DAY*d
    avail={s:[i for i in range(6) if LV[s][i]<=L] for s in SLOTS}
    # rank0(lv1)은 기본 지급 = 무료. 나머지는 구매
    best=None
    for combo in itertools.product(*[avail[s] for s in SLOTS]):
        load=dict(zip(SLOTS,combo))
        cost=sum(0 if load[s]==0 else PRICE[RANK[load[s]]] for s in SLOTS)
        if cost>bud: continue
        st=stat(load)
        key=(sum(1 for v in st.values() if v>=ADV),sum(1 for v in st.values() if v>=MID),sum(st.values()))
        if best is None or key>best[0]: best=(key,st,load,cost)
    k,st,load,cost=best
    note="" if prev is None else ("평평" if st==prev else "")
    print("   %3d  %2d  %7s   %4d %4d %4d %4d    %d/4 %d/4 %d/4  %s"%(
        d,L,format(int(bud),','),st['집중력'],st['관찰력'],st['매력'],st['민첩'],
        sum(1 for v in st.values() if v>=NOV),k[1],k[0],note))
    prev=st
print("\n  ※ 위 표는 「그날 최선의 4벌」이다. 슬롯당 1개만 착용하므로 나머지 구매는 수집이다.")
print("  ※ 예산 제약이 아니라 **레벨 게이트**가 지배한다 — 30일차 예산 38,400 중 실제 지출 %s"%format(cost,','))
