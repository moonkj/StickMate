# -*- coding: utf-8 -*-
import itertools
BASE={'집중력':8,'관찰력':6,'매력':5,'민첩':7}
MAIN=[3,6,10,15]; SUB=[1,2,4,6]; TIER={'초급':10,'중급':20,'고급':32}; CAP=40
DAY=1280.0; SESS=600.0   # 원형 B 일일수입 / 집중 25분 1회

print("="*76); print("[A] 전설 가격 — 두 안"); print("="*76)
L=[600,1400,3200]
print("  기존 사다리 배율: %.4f, %.4f  (기하평균 %.4f)"%(1400/600,3200/1400,(1400/600*3200/1400)**0.5))
g=(1400/600*3200/1400)**0.5
cands=[("ECONOMY_SPEC ×20 연장",6600),("안 α 기하 연장",round(3200*g/100)*100),
       ("안 β 3.0배",9600),("안 β' 3.5배",11200),("(참고) 4.0배",12800)]
# 완주 총액 모형: 슬롯당 2일반+2희귀+1영웅+1전설, 7슬롯, 스탯4슬롯 rank0 기본지급(-600×4)
print("\n  가격      배율   전설1개    전설4개   42종완주총액   완주일   Lv30(75일) 대비")
for nm,X in cands:
    tot=7*(2*600+2*1400+3200+X)-4*600
    d=tot/DAY
    print("  %6d  %5.2f   %4.1f일    %5.1f일   %8s     %5.1f일   %.2f×  %s  <%s>"%(
        X, X/3200, X/DAY, 4*X/DAY, format(tot,','), d, d/75,
        "OK" if d<=97.5 else "★초과", nm))
print("\n  ▶ 교정: 영웅 3,000(=ECONOMY_SPEC ×20)로 두면 총액 %s = 문서의 4,640×20=92,800 과 일치"%format(7*(2*600+2*1400+3000+6600)-4*600,','))
print("  ▶ ECONOMY_SPEC 5-3 판정기준 (c) '완주 ≤ Lv.30 도달일 75일의 1.3배(97.5일)' 상한:")
print("     7X ≤ 97.5×1280 − 48,000 = %s  →  X ≤ %s"%(format(int(97.5*DAY-48000),','),format(int((97.5*DAY-48000)/7),',')))

print(); print("="*76); print("[B] ② 고급 32 — B 결정으로 풀리는가"); print("="*76)
print("  순환 부스탯(스탯당 부스탯 정확히 1개), 전설 동전 구매 가능:")
for n in BASE:
    m=BASE[n]+15+6
    print("    %-4s %2d  고급32 %s"%(n,m,"도달" if m>=32 else "★불가(%d 모자람)"%(32-m)))
print("  ⇒ ★ B 결정만으로는 안 풀린다. 순환이면 전설을 사도 최대 29다.")
print("\n  자유 부스탯(아이템이 sub 지정), 전설 동전 구매 가능:")
for n in BASE:
    m=BASE[n]+15+18
    print("    %-4s %2d  고급32 %s / 캡40 %s"%(n,m,"도달" if m>=32 else "불가","★초과" if m>CAP else "이내"))
print("  ⇒ 풀린다. 단 클램프 40 필수.")

print("\n  ▶ 동시에 몇 스탯이 고급인가 — 부스탯은 제로섬이다")
print("     필요 부스탯 = 32 − 15(자기슬롯 전설) − BASE")
need={n:32-15-BASE[n] for n in BASE}
print("    ",{n:need[n] for n in ['집중력','관찰력','매력','민첩']})
SLOT={'HEAD':'집중력','EYES':'관찰력','NECK':'매력','BACK':'민첩'}
best=0;bestasn=None
for asn in itertools.product(['집중력','관찰력','매력','민첩'],repeat=4):
    if any(asn[i]==list(SLOT.values())[i] for i in range(4)): continue  # 부스탯은 자기 슬롯 주스탯을 안 가리킨다
    tot={n:BASE[n]+15 for n in BASE}
    for i,t in enumerate(asn): tot[t]+=6
    c=sum(1 for n in tot if min(tot[n],CAP)>=32)
    if c>best: best,bestasn=c,(asn,dict(tot))
print("     4아이템 전부 전설일 때 sub 배정 %d^4 중 최대 동시 고급 = **%d스탯**"%(4,best))
print("     예:",dict(zip(['HEAD','EYES','NECK','BACK'],bestasn[0])),"→",
      {k:min(v,CAP) for k,v in bestasn[1].items()})
print("     4스탯 전부 고급에 필요한 부스탯 합 = %d > 가용 24 → **원리적으로 불가능**"%sum(need.values()))

print(); print("="*76); print("[C] ③ 1일차 초급 돌파 — 반증 시도"); print("="*76)
# day1 = rank0 4종(천모자 일반/선글라스 영웅/나비넥타이 희귀/짧은망토 일반)
d1={'집중력':8+3+2+1,'관찰력':6+10,'매력':5+6+1,'민첩':7+3+4}
print("  1일차:",d1," → 4스탯 전부 초급(10) 돌파. 게이지 %s"%{k:"%.0f%%"%(v/40*100) for k,v in d1.items()})
# 중급 20까지 최단 구매
print("\n  중급 20 최단 경로 (인계본 16종 안에서만):")
print("    관찰력: 중절모(1,400,sub관찰력+2)+방울목걸이(3,200,sub관찰력+4) → 6+10+6 = 22")
print("            비용 4,600동전 = %.1f일"%(4600/DAY))
print("    집중력: 왕관(전설)이 필요 → 8+15+3 = 26.  전설가 X에 따라 %.1f~%.1f일"%(6600/DAY,11200/DAY))
print("  ⇒ 3단계 중 1단계가 0일, 2단계가 3.6일에 열린다. **긴장은 사실상 고급 한 층에만 남는다.**")

print(); print("="*76); print("[D] 팩의 동전 환산 (product-strategy 인계용)"); print("="*76)
print("  인계본 팩 = 4종(스탯 슬롯 1개씩). 외형 2종이 아니다 (line 989-995 `kinds` 4개).")
print("  office = '기본 제공' owned:true → **무료 팩**")
for X in (9600,):
    for nm,comp in [("전설 1 + 희귀 3",X+3*1400),("전설 1 + 영웅 1 + 희귀 2",X+3200+2*1400)]:
        print("    %-22s = %8s동전 = %5.1f일 앞당김"%(nm,format(comp,','),comp/DAY))
print("  (구성 등급이 26종 미정에 걸려 있어 **범위로만** 낼 수 있다)")
