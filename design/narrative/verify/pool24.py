# -*- coding: utf-8 -*-
"""제안 앰비언트 풀 24줄 — 한/영 양쪽 발화 자격 검산."""
import re,os,sys,io
sys.stdout=io.TextIOWrapper(sys.stdout.buffer,encoding='utf-8')
src=open("Assets/_Project/Scripts/Dialogue/DialogueKind.cs",encoding="utf-8").read()
def c(n): return float(re.search(r"%s\s*=\s*([\d.]+)f"%n,src).group(1))
BASE,PER,MINS,MAXS,FADEIN,POPIN,FADEOUT=c("BaseSeconds"),c("PerGlyphSeconds"),c("MinSeconds"),c("MaxSeconds"),c("FadeInSeconds"),c("PopInSeconds"),c("FadeOutSeconds")
W_EN=0.0472
def req(n,per): return FADEIN+min(max(BASE+n*per,MINS),MAXS)
for n,e in ((10,1.090),(8,0.940),(7,0.865),(4,0.680),(5,0.715)):
    assert abs(req(n,PER)-e)<1e-9,(n,req(n,PER),e)
print("[교정] 알려진 값 5건 재현 OK (4자0.680/5자0.715/7자0.865/8자0.940/10자1.090)")

IDLE, WALK = 2.00, 1.50
MARGIN = POPIN   # 제안: 발화 자격에 팝인만큼의 여유를 요구

POOL=[
 # (자격, 상태, 한글, 영어)
 ("상시","Idle","음...","Hmm..."),
 ("상시","Idle","여기 좋네","Nice spot."),
 ("상시","Idle","잠깐 쉬는 중","Taking a break."),
 ("상시","Idle","오늘 뭐 하지","What now?"),
 ("상시","Idle","발밑이 단단해","Solid footing."),
 ("상시","Walk","산책 중","Out walking."),
 ("상시","Walk","저쪽으로 가볼까","Let's go that way."),
 ("상시","Walk","하나 둘 하나 둘","Left, right, left."),
 ("상시","Walk","다리 좀 풀자","Stretching the legs."),
 ("상시","Walk","다리가 잘 나가네","Good stride today."),
 ("모션:앉기하품","Idle","하암...","*yawn*"),
 ("모션:두리번","Idle","구경 중이야","Just having a look."),
 ("요일:월","Idle","월요일이네...","Monday again..."),
 ("요일:금","Idle","금요일이다!","It's Friday!"),
 ("요일:주말","Idle","쉬는 날이네","Day off."),
 ("요일:월","Walk","월요일이 왔네","Monday's here."),
 ("요일:금","Walk","주말이 코앞이네","Weekend's close."),
 ("요일:주말","Walk","주말 산책이네","Weekend walk."),
 ("시간:아침","Idle","아침이네","Morning."),
 ("시간:점심","Idle","점심시간이네","Lunchtime."),
 ("시간:밤","Idle","밤이 깊었네","It's late."),
 ("시간:아침","Walk","아침 산책이네","Morning walk."),
 ("시간:점심","Walk","점심때 걷네","Midday stroll."),
 ("시간:밤","Walk","밤에도 걷네","Night walk."),
]
print("\n=== 제안 앰비언트 풀 24줄 — 발화 자격 검산 ===")
print("   한국어는 현행 게이트(0.075/글자), 영어는 개정 게이트(w=%.4f/글자)"%W_EN)
print("   여유 요구 = 팝인 %.2f초 (제안 §2-3)"%MARGIN)
print()
print("   자격            상태  한글               자 필요   여유   | 영어                 자 필요   여유")
print("   ----------------|-----|------------------|--|-----|------|---------------------|--|-----|-----")
bad=0
for q,s,k,e in POOL:
    d = IDLE if s=="Idle" else WALK
    rk,re_=req(len(k),PER),req(len(e),W_EN)
    mk,me=d-rk,d-re_
    ok = mk>=MARGIN-1e-9 and me>=MARGIN-1e-9
    if not ok: bad+=1
    print("   %-15s %-5s %-18s %2d %.3f %+.3f | %-20s %2d %.3f %+.3f %s"
          %(q,s,k,len(k),rk,mk,e,len(e),re_,me,"" if ok else "  ★탈락"))
print("\n   24줄 중 탈락 %d줄"%bad)
print("   한글 최소 여유 %+.3f초 / 영어 최소 여유 %+.3f초"
      %(min((IDLE if s=='Idle' else WALK)-req(len(k),PER) for _,s,k,_ in POOL),
        min((IDLE if s=='Idle' else WALK)-req(len(e),W_EN) for _,s,_,e in POOL)))

print("\n=== 여유 마진(=팝인 0.18초) 도입이 기존 대사에 미치는 영향 ===")
EX=[("Idle","음...",2.00),("Idle","구경 중이야",2.00),("Walk","하나 둘 하나 둘",1.50),
    ("Walk","다리가 잘 나가네",1.50),("ParkourClimb","헉... 높다",1.20),
    ("LedgeHang","여기로 내려가자",1.12),("LedgeHang","어우... 꽤 깊네",1.12)]
for s,t,d in EX:
    m=d-req(len(t),PER)
    print("   %-13s %-16s 여유 %+.3f초  %s"%(s,t,m,"통과" if m>=MARGIN-1e-9 else "★탈락(하한에서)"))
GRAB,HMIN,HMAX=0.28,0.84,1.50
need=req(len("어우... 꽤 깊네"),PER)+MARGIN
hold=need-GRAB
p=max(0.0,min(1.0,(HMAX-hold)/(HMAX-HMIN)))
print("   ★ 탈락 1줄은 '영구 침묵'이 아니다 — LedgeHang 체류는 잡기 %.2f + 매달림 %.2f~%.2f초 균등."%(GRAB,HMIN,HMAX))
print("     필요 매달림 %.3f초 -> 깊은 낙차의 %.0f%%에서는 여전히 말한다(%.0f%%만 침묵)."%(hold,p*100,(1-p)*100))
print("     완전 해소안: '어우... 꽤 깊네'(10자) -> '꽤 깊네'(4자) 여유 %+.3f초"%(1.12-req(4,PER)))
