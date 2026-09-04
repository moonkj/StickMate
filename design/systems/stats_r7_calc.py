# -*- coding: utf-8 -*-
"""design-systems R7 — Q-3 (가) 확정 이후 재검산 / 단위 정본 / U-15 / 곡선"""
import itertools
BASE={'집중력':8,'관찰력':6,'매력':5,'민첩':7}
MAIN=[3,6,10,15]; SUB=[1,2,4,6]; CAP=40
SLOT_MAIN={'HEAD':'집중력','EYES':'관찰력','NECK':'매력','BACK':'민첩'}
DAY=1280.0
RN=['일반','희귀','영웅','전설']
F=[]
def ck(t,g,w):
    ok=g==w; F.append((t,g,w)) if not ok else None
    print(("  [OK]  " if ok else "  [FAIL]")+" %-40s got=%s want=%s"%(t,g,w))

print("="*78);print("[0] 교정 — R5/R6 확정값 재현");print("="*78)
ck("1일차 집중력(천모자일반+나비2+짧은1)",8+3+2+1,14)
ck("자유부스탯 집중력 최대",8+15+18,41)
ck("순환부스탯 집중력 최대",8+15+6,29)
ck("영웅만일 때 집중력 최대",8+10+4*3,30)
ck("전설 9600 완주총액",7*(2*600+2*1400+3200+9600)-4*600,115200)
if F: print("!!! 교정 실패 — 중단"); raise SystemExit(1)
print("  ★ 교정 5/5 PASS\n")

print("="*78);print("[1] Q-3 (가) — 기본 카탈로그 전설을 어느 부위에 몇 종 둘 것인가");print("="*78)
print("  전제: 착용은 슬롯당 1개. 부스탯은 자기 슬롯 주스탯을 안 가리킨다.")
print("  고급 32 필요량 = 32 − BASE  (주스탯+부스탯 합으로 채워야 하는 양)\n")
need={n:32-BASE[n] for n in BASE}
print("   ",{n:need[n] for n in ['집중력','관찰력','매력','민첩']},"\n")
print("  ▶ 기본 카탈로그 전설이 k개 슬롯에만 있을 때, 각 스탯이 고급에 닿는가")
print("    (전설 없는 슬롯은 영웅이 최선이라고 가정 = 기본 42종 상한)")
hdr="    %-22s %-8s %-8s %-8s %-8s"%("전설 보유 슬롯","집중력","관찰력","매력","민첩")
print(hdr); print("    "+"-"*56)
slots=['HEAD','EYES','NECK','BACK']
def best(stat, legs):
    own=[s for s in slots if SLOT_MAIN[s]==stat][0]
    m=MAIN[3] if own in legs else MAIN[2]
    subs=sorted([SUB[3] if s in legs else SUB[2] for s in slots if s!=own],reverse=True)
    return BASE[stat]+m+sum(subs)
for k in range(5):
    for legs in itertools.combinations(slots,k):
        cells=[]
        for n in ['집중력','관찰력','매력','민첩']:
            v=min(best(n,set(legs)),CAP)
            cells.append(("%d %s"%(v,"O" if v>=32 else "X")))
        nm=("없음" if not legs else "+".join(legs))
        print("    %-22s %-8s %-8s %-8s %-8s"%(nm,*cells))
    if k==1: print("    "+"-"*56)
print("""
  ⇒ 판정:
     · 전설 0개 → 4스탯 전부 고급 불가 (최대 30/28/27/29). **고급 층 전체가 죽는다.**
     · 전설 1개(어느 슬롯이든) → 그 슬롯의 스탯 + 집중력만 열린다. **비대칭.**
     · 전설 4슬롯 전부 → 4스탯 전부 「도달 가능」. 단 동시 도달은 여전히 2스탯(R6 §12-1-a).
  ⇒ **부위별 최소 1종이 필요하다.** 근거는 취향이 아니라 대칭성이다 —
     임계 32는 4스탯 공통이므로, 어느 한 스탯만 영원히 못 닿으면
     그 스탯 카드의 3번째 눈금과 고급 효과가 **영구 죽은 콘텐츠**가 된다.""")

print("\n  ▶ 슬롯당 2종 이상 두면 무엇이 달라지는가")
print("     착용은 슬롯당 1개이므로 **도달 가능성은 한 칸도 안 바뀐다.**")
for c in (1,2):
    print("     슬롯당 %d종 → 스탯 4슬롯 전설 %d종 × 9,600 = %s동전 = %.1f일"%(
        c,4*c,format(4*c*9600,','),4*c*9600/DAY))
print("     ⇒ 2종은 '선택'만 늘리고 비용을 2배로 만든다. **경제 관점 권고 = 슬롯당 정확히 1종.**")

print("\n"+"="*78);print("[2] U-15 — 중급 20을 올릴 것인가");print("="*78)
print("  중급 M 도달에 필요한 (주+부) = M − BASE")
for M in (20,22,24,26):
    r=[]
    for n in ['집중력','관찰력','매력','민첩']:
        r.append("%s %d"%(n,M-BASE[n]))
    print("   M=%-3d %s"%(M,"  ".join(r)))
print("""
  ▶ 조달 가능성 (인계본 16종 중 DLC 2종 제외한 14종 안에서, 슬롯당 1개 착용)""")
# 14종: (slot, rar, sub)
ITEMS={'HEAD':[('천모자',0,'매력'),('털모자',1,'민첩'),('중절모',1,'관찰력')],
       'EYES':[('선글라스',2,'민첩'),('동그란안경',0,'집중력'),('고글',1,'민첩'),('외알안경',2,'집중력')],
       'NECK':[('나비넥타이',1,'집중력'),('줄무늬타이',0,'집중력'),('목도리',0,'민첩'),('방울목걸이',2,'관찰력')],
       'BACK':[('짧은망토',0,'집중력'),('긴망토',1,'매력'),('배낭',0,'집중력')]}
def stats(load):
    v=dict(BASE)
    for s,(nm,r,sub) in load.items():
        v[SLOT_MAIN[s]]+=MAIN[r]; v[sub]+=SUB[r]
    return {k:min(x,CAP) for k,x in v.items()}
allload=[dict(zip(slots,c)) for c in itertools.product(*[ITEMS[s] for s in slots])]
mx={n:max(stats(l)[n] for l in allload) for n in BASE}
print("     14종으로 도달 가능한 스탯별 최대:",mx)
for M in (20,22,24):
    ok=[n for n in mx if mx[n]>=M]
    print("     M=%-3d 도달 가능 스탯 %d/4  %s"%(M,len(ok),ok))
print("""
  ⇒ 중급을 24로 올리면 **인계본 16종만으로는 어느 스탯도 중급에 못 간다**
     (최대 22/22/18/20). 중급이 통째로 「아직 정의되지 않은 26종」 뒤로 넘어간다.
  ⇒ 【판】**중급 20을 유지한다. U-15를 「인상 안 함」으로 닫는다.**
     내 R6 반증은 유효하지만(중급 3.6일), 처방이 틀렸다 — 중급을 올리면
     검증 불가능한 구간이 늘 뿐이다. **3단계의 간격 문제는 26종 등급 배정으로 푼다.**""")

print("\n"+"="*78);print("[3] 곡선 — BASE 상수 가정 (인계본 line 750 `const BASE`)");print("="*78)
print("  ★ 가정: BASE는 레벨과 무관한 스탯별 상수다. 인계본 원문이 `const`이므로 그대로 쓴다.")
print("  ★ 한계: 26종 미정 + 요구 레벨 미정(U-3) → **인계본 14종 한정 곡선**이다.\n")
PRICE={0:600,1:1400,2:3200}
free={'HEAD':('천모자',0,'매력'),'EYES':('선글라스',2,'민첩'),
      'NECK':('나비넥타이',1,'집중력'),'BACK':('짧은망토',0,'집중력')}
buy=[(s,it) for s in slots for it in ITEMS[s] if it[0] not in [free[s][0]]]
print("  1일차 = 무료 4종만:",stats(free))
tot=sum(PRICE[it[1]] for s,it in buy)
print("  구매 대상 %d종 총액 %s동전 = %.1f일치 수입"%(len(buy),format(tot,','),tot/DAY))
for d in (1,3,7,14,30):
    bud=DAY*d
    bestn=None
    combos=[buy] if bud>=tot else [c for r in range(len(buy)+1)
              for c in itertools.combinations(buy,r)
              if sum(PRICE[i[1]] for _,i in c)<=bud]
    for combo in combos:
        own={s:[free[s]] for s in slots}
        for s,i in combo: own[s].append(i)
        for load in [dict(zip(slots,c)) for c in itertools.product(*[own[s] for s in slots])]:
            st=stats(load)
            key=(sum(1 for v in st.values() if v>=32),sum(1 for v in st.values() if v>=20),sum(st.values()))
            if bestn is None or key>bestn[0]: bestn=(key,st)
    k,st=bestn
    print("  %2d일차 (예산 %7s) → %s   중급 %d/4  고급 %d/4  합 %d"%(
        d,format(int(bud),','),{n:st[n] for n in ['집중력','관찰력','매력','민첩']},k[1],k[0],k[2]))
print("""
  ⇒ 14종을 다 사는 데 11.3일. 그 뒤 곡선은 **평평해진다** — 26종이 없으면
     12일차부터 무한히 같은 값이다. 고급 0/4 (전설이 14종 안에 없다).
  ⇒ 이것이 「26종 78개 선언」이 막고 있는 것의 크기다: **2주차 이후 전부.**""")

print("\n"+"="*78);print("[4] product-strategy P-9 대입용 확정값");print("="*78)
print("  ┌ 단위: 동전(인계본 자릿수, ECONOMY_SPEC 원단위 ×20)")
rows=[("집중모드 완주","분당 24동전","25분=600 / 시간당 1,440"),
      ("집중모드 취소","분당 20동전","완주의 83.3%"),
      ("활쏘기 정중앙","1회 20동전","쿨다운 600초 → 120/시"),
      ("[오늘 할일]","하루 1회 300동전","시간 비종속 채널 상한 600/일"),
      ("원형 A 켜두기만","68동전/일","0.05×"),
      ("원형 B 기준","1,280동전/일","1.00×"),
      ("원형 C 적극","2,560동전/일","2.00×"),
      ("원형 D 상한","6,000동전/일","4.69×")]
for a,b,c in rows: print("  %-16s %-18s %s"%(a,b,c))
print("\n  ┌ 가격 사다리 (확정)")
for l,p in [("일반",600),("희귀",1400),("영웅",3200),("전설",9600)]:
    print("  %-6s %6s동전   집중 25분 %5.1f회   기준 유저 %5.2f일"%(l,format(p,','),p/600,p/DAY))
print("\n  ┌ 42종 완주")
t=7*(2*600+2*1400+3200+9600)-4*600
print("  총액 %s동전 = %.1f일 (Lv.30 도달 75일의 %.2f×, 상한 1.30× 이내)"%(format(t,','),t/DAY,t/DAY/75))
print("  전설 상한값: X ≤ %s (그 위는 완주 곡선을 깬다)"%format(int((97.5*DAY-48000)/7),','))
