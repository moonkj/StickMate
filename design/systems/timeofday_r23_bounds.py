from math import prod
def dup(N,k):
    if k>N: return 1.0
    return 1.0 - prod((N-i)/N for i in range(k))

print("=== 0. 교정 — design-narrative가 이미 발표한 값 재현 ===")
print("   (A) 닫힌 식 3건 — pool_effective.out.txt / R2 §3-4")
known=[(10,4,0.496,"pool_effective 표 '화수목·그밖·없음'"),
       (16,4,0.333,"pool_effective 표 'mon·morn·look,yawn'"),
       (24,6,0.493,"R2 §3-4 풀24 계약 통과선")]
ok=True
for N,k,exp,src in known:
    g=dup(N,k); h=abs(g-exp)<0.0015; ok&=h
    print(f"       N={N:2d} k={k}  발표 {exp*100:5.1f}%  계산 {g*100:5.1f}%  {'OK' if h else '★불일치'}  <- {src}")
print("   (B) ★ 내가 처음 잘못 물린 값 — R2의 '풀13 -> 71.8%'는 닫힌 식이 아니다")
print(f"       닫힌식 N=13,k=6 = {dup(13,6)*100:.1f}%  != 71.8%  (여기서 교정이 한 번 깨졌다)")
print( "       R2 원문: 'k 평균 5.88회 (중앙값 6, 최소 5, 최대 7)' -> k 분포 위의 시뮬레이션이다")
for k in (5,6,7): print(f"         k={k}: {dup(13,k)*100:5.1f}%")
w={5:0.24,6:0.64,7:0.12}
mix=sum(w[k]*dup(13,k) for k in w); mean=sum(w[k]*k for k in w)
print(f"       분포 재구성 (평균 k={mean:.2f}, 중앙값 6) -> {mix*100:.1f}%  vs 발표 71.8%  {'OK' if abs(mix-0.718)<0.01 else '불일치'}")
print("   교정", "통과 — 아래 숫자를 신뢰한다" if ok else "★실패")
if not ok: raise SystemExit(1)

GAP=345.0; SESSION=23*60
K=round(SESSION/GAP)
print(f"\n   [k 유도] 계약 세션 {SESSION:.0f}초 / 평균간격 {GAP:.0f}초 = {SESSION/GAP:.2f} -> k={K}  (pool_effective §2와 동일)")

print("\n=== 1. 시간대 커버리지가 '최악 조합'에 미치는 영향 ===")
print("   최악 조합 = 화·수·목(요일 자격 0) x 모션 없음.  시간대 자격만 다르다.")
for lab,tod in (("현행 3구간 · 14-22시에 있을 때 (시간대 자격 0)",0),
                ("현행 3구간 · 시간대 안에 있을 때        ",2),
                ("전구간 커버 확정안 (항상 자격 2)        ",2)):
    N=10+tod; k=max(x for x in range(1,N+1) if dup(N,x)<0.5)
    print(f"   {lab} N={N:2d} 허용k={k} 중복 {dup(N,k)*100:5.1f}%  계약여유 {50-dup(N,k)*100:+5.1f}pp")

print("\n=== 2. 구멍이 왜 '칼날 위'인가 — N=10에서 한 줄만 빠지면 ===")
for N in (12,11,10,9):
    k=max(x for x in range(1,N+1) if dup(N,x)<0.5)
    print(f"   N={N:2d} 허용k={k} 중복 {dup(N,k)*100:5.1f}%  {'계약 k=4 유지' if k>=4 else '★ 허용 k가 4->3 후퇴 = 필요간격 460초로 급증'}")

print("\n=== 3. 구간 길이별 노출 검산 (평균간격 345초, 최악 N=12) ===")
for name,h in (("아침 05-11",6),("점심 11-14",3),("오후 14-18",4),("저녁 18-22",4),("밤 22-05",7),("(하한참고) 2h",2),("(하한참고) 1h",1)):
    d=h*3600/GAP; p=1/12
    print(f"   {name:14s} {h}h  발화 {d:5.1f}회  기대노출 {d*p:4.2f}회  1회이상 {1-(1-p)**d:5.1%}")

print("\n=== 4. 경계 판정을 요일과 같은 60초 폴링으로 재사용할 때의 오차 ===")
POLL=60.0; p=POLL/GAP
print(f"   경계 후 최대 {POLL:.0f}초 동안 지난 구간으로 판정될 수 있다")
print(f"   그 창에 발화가 걸릴 확률 {p:.3f} ({p*100:.1f}%), 하필 시간대 줄일 확률 x 1/12 = {p/12*100:.2f}%")
print(f"   경계 5회/일 -> 어긋난 시간대 대사 관측 기대 {p/12*5:.4f}회/일 = 연 {p/12*5*365:.2f}회")
print(f"   ★ 시각(분) 단위 경계를 쓰면 이 값이 그대로이나, '분' 경계는 폴링 60초와 정렬되지 않는다 -> 정시 경계만 쓴다")

print("\n=== 5. 저장 영향 (세이브 IOException 선결조건 관련) ===")
print("   시간대 구간 = DateTime.Now.Hour 파생. 저장 필드 0개, 스키마 변경 0건, 저장 호출 +0.00회/분")
