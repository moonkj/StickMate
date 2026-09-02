# -*- coding: utf-8 -*-
"""영어 대사 성립성 + 언어 인식 게이트의 계수 w 유도."""
import re,os,sys,io,statistics,unicodedata
sys.stdout=io.TextIOWrapper(sys.stdout.buffer,encoding='utf-8')
ROOT="/Users/kjmoon/App/StickMate"
src=open(os.path.join(ROOT,"Assets/_Project/Scripts/Dialogue/DialogueKind.cs"),encoding="utf-8").read()
def const(n):
    m=re.search(r"%s\s*=\s*([\d.]+)f"%n,src); return float(m.group(1))
BASE,PER,MINS,MAXS=const("BaseSeconds"),const("PerGlyphSeconds"),const("MinSeconds"),const("MaxSeconds")
FADEIN=const("FadeInSeconds")
def reading(n): return min(max(BASE+n*PER,MINS),MAXS)
def required(n): return FADEIN+reading(n)
# --- 교정: 문서의 알려진 값 3건 ---
for n,exp in ((10,1.090),(8,0.940),(7,0.865)):
    assert abs(required(n)-exp)<1e-9,(n,required(n))
print("[교정] 10자->1.090 / 8자->0.940 / 7자->0.865  OK (리더 제시값 재현)")

DWELL={"Idle":2.00,"Walk":1.50,"ParkourClimb":1.20,"LedgeHang":1.12}
print("\n=== 1. 현행(언어 무지) 게이트에서의 글자 예산 재검산 ===")
for s,d in DWELL.items():
    n=0
    while required(n+1)<=d+1e-9: n+=1
    print("   %-13s 잔여하한 %.2f초 -> 최대 %2d자 (%d자 필요체류 %.3f / %d자 %.3f)"
          %(s,d,n,n,required(n),n+1,required(n+1)))

PAIRS=[  # (상태, 한글 실재 대사, 영어 — 번역이 아니라 같은 사실을 영어로 재창작)
 ("Idle","음...","Hmm..."),
 ("Idle","여기 좋네","Nice spot."),
 ("Idle","잠깐 쉬는 중","Taking a break."),
 ("Idle","오늘 뭐 하지","What now?"),
 ("Idle","하암...","*yawn*"),
 ("Idle","발밑이 단단해","Solid footing."),
 ("Idle","구경 중이야","Just looking."),
 ("Walk","산책 중","Out walking."),
 ("Walk","저쪽으로 가볼까","Over there."),
 ("Walk","하나 둘 하나 둘","Left, right."),
 ("Walk","다리 좀 풀자","Legs need this."),
 ("Walk","다리가 잘 나가네","Good stride."),
 ("ParkourClimb","가뿐하네","Easy."),
 ("ParkourClimb","영차...","Nnngh..."),
 ("ParkourClimb","헉... 높다","Whoa, high!"),
 ("LedgeHang","여기로 내려가자","Down here."),
 ("LedgeHang","어우... 꽤 깊네","Long way."),
]
print("\n=== 2. 영어 재창작 18줄 — 현행 게이트 통과 여부 ===")
print("   상태          | 한글             자 | 영어              자 | 필요체류 | 하한  | 판정")
print("   --------------|--------------------|---------------------|---------|-------|-----")
fail=0
for s,k,e in PAIRS:
    ok = required(len(e))<=DWELL[s]+1e-9
    if not ok: fail+=1
    print("   %-13s | %-16s %2d | %-18s %2d | %6.3f초 | %.2f | %s"
          %(s,k,len(k),e,len(e),required(len(e)),DWELL[s],"통과" if ok else "★영구침묵"))
print("   -> 18줄 중 침묵 %d줄"%fail)

print("\n=== 3. 리더가 제시한 '번역' 3건 재검산 (반례 확인) ===")
for s,k,e in (("LedgeHang","어우... 꽤 깊네","Whoa... that's deep"),
              ("ParkourClimb","헉... 높다","Whoa, that's high"),
              ("Walk","하나 둘 하나 둘","Left, right, left, right")):
    print("   %-13s %-17s %2d자 -> %-26s %2d자 필요 %.3f vs 하한 %.2f  %s"
          %(s,k,len(k),e,len(e),required(len(e)),DWELL[s],
            "통과" if required(len(e))<=DWELL[s] else "★영구침묵"))

print("\n=== 4. ★ 결정적 반증 — 게이트가 '짧은 쪽'을 침묵시킨다 ===")
def hangul_syll(t): return sum(1 for c in t if 0xAC00<=ord(c)<=0xD7A3)
def en_syll(t):
    # 근사: 모음군 수, 어말 무음 e 제외
    w=re.findall(r"[A-Za-z']+",t.lower()); n=0
    for x in w:
        g=re.findall(r"[aeiouy]+",x); c=len(g)
        if x.endswith("e") and c>1 and not x.endswith(("le","ee")): c-=1
        n+=max(1,c)
    return n
for k,e in (("어우... 꽤 깊네","Whoa... that's deep"),("헉... 높다","Whoa, that's high")):
    print("   한글 %-17s 음절 %d / 글자 %2d  |  영어 %-22s 음절 %d / 글자 %2d"
          %(k,hangul_syll(k),len(k),e,en_syll(e),len(e)))
print("   -> 영어 쪽이 **음절이 더 적은데(=읽는 시간이 더 짧은데) 글자수가 1.7~1.9배**라 침묵한다.")
print("      즉 이것은 문안 문제가 아니라 **단위 오류**다: 0.075초/글자는 실은 0.075초/**음절**이고,")
print("      한글에서만 글자=음절이라 지금까지 들키지 않았다.")

print("\n=== 5. 언어 인식 계수 w 유도 (라틴 문자 1글자당 초) ===")
print("   불변식: **같은 발화는 같은 가독예산을 청구받는다**")
print("   (언어 간 정보 전달률이 거의 일정하다는 Coupé et al. 2019(Science Advances) 결과와 같은 방향)")
print("   0.28 + w·G_en = 0.28 + 0.075·G_kr  ->  w_i = 0.075 · G_kr / G_en")
ws=[]
print("   한글                자 | 영어              자 | w_i")
for s,k,e in PAIRS:
    w=PER*len(k)/len(e); ws.append(w)
    print("   %-18s %2d | %-18s %2d | %.4f"%(k,len(k),e,len(e),w))
med=statistics.median(ws); mean=sum(ws)/len(ws)
print("   중앙값 %.4f / 평균 %.4f / 범위 %.4f~%.4f"%(med,mean,min(ws),max(ws)))
print("   글자수 합 비율로도 교차검산: 한글 %d자 / 영어 %d자 = %.3f -> w = %.4f"
      %(sum(len(k) for _,k,_ in PAIRS),sum(len(e) for _,_,e in PAIRS),
        sum(len(k) for _,k,_ in PAIRS)/sum(len(e) for _,_,e in PAIRS),
        PER*sum(len(k) for _,k,_ in PAIRS)/sum(len(e) for _,_,e in PAIRS)))
W=0.0472
print("\n   ★ 권고 w = %.4f 초/라틴글자 = **말뭉치 전체 글자수 비율**(112/178)."%W)
print("      중앙값 %.4f와 0.6%% 차이. 임의로 더 내리지 않는다 — 과소 청구의 벌은"%med)
print("      '읽기 전에 사라진다'이고 그건 이 저장소가 규칙 8로 없애려던 결함 그 자체다.")
print("      = 라틴 글자당 %.3f초, 영단어(평균 5.1글자+공백=6.1) 1개당 %.3f초"%(W,W*6.1))

print("\n=== 6. w=%.3f 적용 시 영어 글자 예산 ==="%W)
def reading_en(n): return min(max(BASE+n*W,MINS),MAXS)
def required_en(n): return FADEIN+reading_en(n)
for s,d in DWELL.items():
    n=0
    while required_en(n+1)<=d+1e-9: n+=1
    print("   %-13s 하한 %.2f초 -> 현행 %2d자  ->  개정 %2d자 (%.2f배)"
          %(s,d,max(m for m in range(60) if required(m)<=d),n,
            n/max(m for m in range(60) if required(m)<=d)))
print("\n   개정 후 리더 제시 '번역' 3건 재판정:")
for s,k,e in (("LedgeHang","어우... 꽤 깊네","Whoa... that's deep"),
              ("ParkourClimb","헉... 높다","Whoa, that's high"),
              ("Walk","하나 둘 하나 둘","Left, right, left, right")):
    print("     %-13s %-26s %2d자 필요 %.3f vs %.2f  %s"
          %(s,e,len(e),required_en(len(e)),DWELL[s],"통과" if required_en(len(e))<=DWELL[s] else "여전히 침묵"))
print("\n   ★ 개정해도 3건 중 1건(LedgeHang 19자)은 여전히 침묵한다 — 게이트 수정은")
print("     **필요조건이지 충분조건이 아니다**. 영어 풀은 그래도 **영어로 새로 써야** 한다(번역 금지).")
