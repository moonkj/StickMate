# -*- coding: utf-8 -*-
"""
design-systems R5 — 핸드오프 장비창(1042) 수치 확정 계산기
규칙(CLAUDE.md): 알려진 값으로 먼저 교정한다. 교정이 깨지면 그 뒤 숫자를 전부 폐기한다.

교정 기준 = 사용자가 본 렌더의 실제 표시값 (독립 출처: equipment-screen.dc.html 의 데이터 + 표시 문자열)
"""
import math, sys

FAIL = []
def check(tag, got, want):
    ok = (got == want)
    if not ok: FAIL.append((tag, got, want))
    print(("  [OK]  " if ok else "  [FAIL]") + " %-34s got=%-22s want=%s" % (tag, repr(got), repr(want)))

# ───────────────────────────────────────────────────────────────
# [0] 교정 — 핸드오프 렌더를 독립 재구현해서 표시값을 그대로 재현하는가
# ───────────────────────────────────────────────────────────────
print("="*78); print("[0] 교정 (calibration) — 핸드오프 렌더 표시값 재현"); print("="*78)

H_MAINV = [3, 6, 10, 15]     # 일반 희귀 영웅 전설
H_SUBV  = [1, 2, 4, 6]
H_BASE  = {'집중력':8, '관찰력':6, '매력':5, '민첩':7}
H_TIERS = [('초급',10), ('중급',20), ('고급',32)]
H_CAP   = 40
SLOT_MAIN = {'HEAD':'집중력','EYES':'관찰력','NECK':'매력','BACK':'민첩'}

# 렌더의 16종 데모 카탈로그 (rar, sub, price, owned, theme)
H_ITEMS = {
 'h1':('HEAD',0,'매력',600,True,'ink'),   'h2':('HEAD',1,'민첩',1400,True,'sport'),
 'h3':('HEAD',1,'관찰력',1400,True,'office'), 'h4':('HEAD',3,'매력',None,False,'cyber'),
 'e1':('EYES',2,'민첩',3200,True,'mil'),   'e2':('EYES',0,'집중력',600,True,'office'),
 'e3':('EYES',1,'민첩',1400,True,'cyber'), 'e4':('EYES',2,'집중력',3200,False,'ink'),
 'n1':('NECK',1,'집중력',1400,True,'office'), 'n2':('NECK',0,'집중력',600,True,'office'),
 'n3':('NECK',0,'민첩',600,True,'sport'),  'n4':('NECK',2,'관찰력',3200,True,'neon'),
 'b1':('BACK',0,'집중력',600,True,'mil'),  'b2':('BACK',1,'매력',1400,True,'cyber'),
 'b3':('BACK',3,'관찰력',None,False,'neon'), 'b4':('BACK',0,'집중력',600,True,'office'),
}
EQ_A = {'HEAD':'h3','EYES':'e2','NECK':'n2','BACK':'b2'}   # 렌더 기본 착용(안 A)

def handoff_rows(eq):
    bonus = {k:0 for k in SLOT_MAIN.values()}
    theme = {}
    for slot, iid in eq.items():
        s, rar, sub, price, owned, th = H_ITEMS[iid]
        bonus[SLOT_MAIN[slot]] += H_MAINV[rar]
        bonus[sub]             += H_SUBV[rar]
        theme[th] = theme.get(th,0)+1
    rows = []
    for n in ['집중력','관찰력','매력','민첩']:
        total = H_BASE[n] + bonus[n]
        stage = -1
        for i,(l,v) in enumerate(H_TIERS):
            if total >= v: stage = i
        nxt = H_TIERS[stage+1] if stage+1 < len(H_TIERS) else None
        rows.append(dict(name=n, bonus=bonus[n], total=total,
                         stage=(H_TIERS[stage][0] if stage>=0 else '미달'),
                         nextText=(nxt[0]+'까지 '+str(nxt[1]-total)) if nxt else '최고 단계'))
    return rows, bonus, theme

rows, bonus, theme = handoff_rows(EQ_A)
disp = {r['name']: r for r in rows}
check("집중력 표시",  "%d (+%d)"%(disp['집중력']['total'], disp['집중력']['bonus']), "16 (+8)")
check("관찰력 표시",  "%d (+%d)"%(disp['관찰력']['total'], disp['관찰력']['bonus']), "11 (+5)")
check("매력 표시",    "%d (+%d)"%(disp['매력']['total'],   disp['매력']['bonus']),   "10 (+5)")
check("민첩 표시",    "%d (+%d)"%(disp['민첩']['total'],   disp['민첩']['bonus']),   "13 (+6)")
check("장비 합",      "+%d"%sum(bonus.values()), "+24")
check("집중력 잔여",  disp['집중력']['nextText'], "중급까지 4")
check("관찰력 잔여",  disp['관찰력']['nextText'], "중급까지 9")
check("매력 잔여",    disp['매력']['nextText'],   "중급까지 10")
check("민첩 잔여",    disp['민첩']['nextText'],   "중급까지 7")
check("매력 등급명",  disp['매력']['stage'], "초급")
top = max(theme.items(), key=lambda kv: kv[1])
check("테마 세트",    "%s %d/4"%({'office':'오피스 워커'}.get(top[0],top[0]), top[1]), "오피스 워커 3/4")
check("보유 카운터",  "보유 %d / %d"%(sum(1 for v in H_ITEMS.values() if v[4]), len(H_ITEMS)), "보유 13 / 16")

print()
print("  ▶ 리더 가설 검산: 「중급까지 N」의 N + 현재값이 전부 같은 수인가?")
sums = sorted(set(disp[n]['total'] + int(disp[n]['nextText'].split()[-1]) for n in ['집중력','관찰력','매력','민첩']))
check("현재값+잔여 (4스탯 전부 동일)", sums, [20])
print("  ⇒ 임계 경계는 스탯마다 다르지 않다. 전부 중급=20 공통이고, 남은 거리만 다르다.")

if FAIL:
    print("\n!!! 교정 실패 %d건 — 이 아래 숫자는 전부 무효다. 중단한다." % len(FAIL))
    for t,g,w in FAIL: print("   ", t, g, w)
    sys.exit(1)
print("\n  ★ 교정 %d/%d PASS. 아래 계산을 진행한다.\n" % (13+1, 13+1))

# ───────────────────────────────────────────────────────────────
# [1] 인계본 16종 ↔ 우리 42종 대조
# ───────────────────────────────────────────────────────────────
print("="*78); print("[1] 인계본 16종은 우리 42종의 무엇인가"); print("="*78)
# 출처: Assets/_Project/Scripts/Tests/EditMode/Golden/ItemCatalogGolden.txt (실측 추출)
OUR = {
 'Head':      [('천모자',1),('털모자',5),('중절모',9),('왕관',20),('베레모',23),('밀짚모자',26)],
 'Eyes':      [('선글라스',1),('동그란안경',6),('고글',11),('외알안경',15),('뿔테안경',19),('안대',23)],
 'Neck':      [('나비넥타이',1),('줄무늬타이',8),('목도리',12),('방울목걸이',18),('펜던트목걸이',21),('반다나',25)],
 'Shoulders': [('짧은망토',1),('긴망토',13),('날개',17),('배낭',22),('판초',25),('요정날개',28)],
}
H_SLOT_OF = {'HEAD':'Head','EYES':'Eyes','NECK':'Neck','BACK':'Shoulders'}
H_NAME = {'h1':'천모자','h2':'털모자','h3':'중절모','h4':'왕관',
          'e1':'선글라스','e2':'동그란안경','e3':'고글','e4':'외알안경',
          'n1':'나비넥타이','n2':'줄무늬타이','n3':'목도리','n4':'방울목걸이',
          'b1':'짧은망토','b2':'긴망토','b3':'날개','b4':'배낭'}
for hs, os_ in H_SLOT_OF.items():
    hn = [H_NAME[i] for i in H_ITEMS if H_ITEMS[i][0]==hs]
    hn = [H_NAME[i] for i in ['h1','h2','h3','h4','e1','e2','e3','e4','n1','n2','n3','n4','b1','b2','b3','b4']
          if H_ITEMS[i][0]==hs]
    first4 = [n for n,_ in OUR[os_][:4]]
    print("  %-10s 인계본 %s" % (hs, hn))
    print("  %-10s 우리 상위4 %s   %s" % ('', first4, "일치" if sorted(hn)==sorted(first4) else "★불일치"))
print("  ⇒ 인계본 16종 = 우리 스탯 4슬롯의 **요구 레벨 하위 4종**. 상위 2종×4슬롯 = 8종이 인계본에 없다.")
print("  ⇒ 외형 3슬롯(Hair/Fx/Pet) 18종도 인계본에 없다. 미정의 = 8 + 18 = 26종.")

# ───────────────────────────────────────────────────────────────
# [2] 등급 단조성 — 요구 레벨이 오르면 등급도 오르는가
# ───────────────────────────────────────────────────────────────
print(); print("="*78); print("[2] 등급 단조성 검사 (인계본 등급 그대로 이식했을 때)"); print("="*78)
RARN = ['일반','희귀','영웅','전설']
order = {'HEAD':['h1','h2','h3','h4'],'EYES':['e1','e2','e3','e4'],
         'NECK':['n1','n2','n3','n4'],'BACK':['b1','b2','b3','b4']}
broke = 0
for hs, ids in order.items():
    lv = [l for _,l in OUR[H_SLOT_OF[hs]][:4]]
    rr = [H_ITEMS[i][1] for i in ids]
    mono = all(rr[i] <= rr[i+1] for i in range(3))
    if not mono: broke += 1
    print("  %-6s lv=%-18s 등급=%-22s %s" % (hs, lv, [RARN[r] for r in rr], "단조 OK" if mono else "★단조 깨짐"))
print("  ⇒ 4슬롯 중 %d개에서 단조가 깨진다." % broke)
print("     최악 사례: 선글라스 Lv.1=영웅(주+10)  >  배낭 Lv.22=일반(주+3).  격차 +7")
print("     → 우리 코드가 출하한 문구 `Lv.{N}에 열림`(ItemCatalog.cs:299) 위에서 '나중에 열리는 물건이 더 약하다'가 된다.")

# ───────────────────────────────────────────────────────────────
# [3] CAP 40 — 넘는가? 도달하는가?
# ───────────────────────────────────────────────────────────────
print(); print("="*78); print("[3] 캡 40 검산 — 초과 가능성 / 도달 가능성"); print("="*78)
print("  부스탯 방향이 아이템별 자유(인계본 방식)일 때 한 스탯이 받을 수 있는 최대:")
print("    = BASE + 주스탯(자기 슬롯 전설 15) + 부스탯(나머지 3슬롯이 전부 그 스탯을 가리킴 6×3=18)")
for n in ['집중력','관찰력','매력','민첩']:
    mx = H_BASE[n] + 15 + 18
    print("    %-4s BASE %2d + 15 + 18 = %2d   %s" % (n, H_BASE[n], mx, "★ CAP 40 초과" if mx > H_CAP else ("CAP 정확히 도달" if mx==H_CAP else "CAP 미달 %d"%(H_CAP-mx))))
print("  ⇒ 집중력만 41로 CAP을 1 넘는다. 인계본 코드는 막대만 min(100%%)로 자르고 **숫자는 안 자른다**")
print("     (`pct: Math.min(100, ...)` / `total`은 클램프 없음) → 화면에 '41 / 40'이 뜬다.")
print()
print("  ECONOMY_SPEC의 순환 부스탯(스탯당 부스탯 정확히 1개)일 때 최대:")
for n in ['집중력','관찰력','매력','민첩']:
    mx = H_BASE[n] + 15 + 6
    print("    %-4s BASE %2d + 15 + 6 = %2d   고급(32) %s" % (n, H_BASE[n], mx, "도달" if mx>=32 else "★도달 불가 (%d 모자람)"%(32-mx)))
print("  ⇒ 순환 규칙을 유지하면 **고급 32에 어떤 스탯도 도달하지 못한다.** 게이지는 영원히 72.5%% 이하.")
print("  ⇒ 두 규칙은 양립 불가: 인계본의 임계 32는 **아이템별 자유 부스탯 + 부스탯 수렴 빌드**를 전제한다.")

# ───────────────────────────────────────────────────────────────
# [4] ECONOMY_SPEC 규칙과 인계본 16종의 일치율
# ───────────────────────────────────────────────────────────────
print(); print("="*78); print("[4] 기존 파생 규칙(rank 0,1=일반/2,3=희귀/4=영웅/5=전설)과의 일치율"); print("="*78)
DERIVED = [0,0,1,1,2,3]
agree_r = 0; agree_s = 0
CYCLE = {'HEAD':'관찰력','EYES':'매력','NECK':'민첩','BACK':'집중력'}
for hs, ids in order.items():
    for k,i in enumerate(ids):
        d = DERIVED[k]; h = H_ITEMS[i][1]
        if d == h: agree_r += 1
        if H_ITEMS[i][2] == CYCLE[hs]: agree_s += 1
print("  등급  일치 %2d/16 = %.1f%%   (파생 규칙 ≠ 인계본 선언)" % (agree_r, agree_r/16*100))
print("  부스탯 일치 %2d/16 = %.1f%%   (순환 규칙 ≠ 인계본 아이템별 지정)" % (agree_s, agree_s/16*100))
print("  ⇒ 두 축 모두 **유도로 메울 수 없다.** 나머지 26종은 규칙 파생이 아니라 선언이 필요하다.")

# ───────────────────────────────────────────────────────────────
# [5] 가격 — 인계본 vs ECONOMY_SPEC. 단위인가 충돌인가
# ───────────────────────────────────────────────────────────────
print(); print("="*78); print("[5] 가격 대조 — '20배 충돌'의 정체"); print("="*78)
OLD = [30, 70, 150, 330]
NEW = [600, 1400, 3200, None]
print("  등급     ECONOMY_SPEC   ×20      인계본     차이")
for i,l in enumerate(RARN):
    o20 = OLD[i]*20
    n = NEW[i]
    s = "—(DLC 전용, 가격 없음)" if n is None else ("%+d (%.1f%%)" % (n-o20, (n-o20)/o20*100))
    print("  %-6s %6d       %6d   %8s   %s" % (l, OLD[i], o20, ("없음" if n is None else n), s))
print("  ⇒ 일반·희귀는 ×20이 **정확히 일치**한다(600, 1400). 영웅만 3000 vs 3200 = +6.7%%.")
print("  ⇒ '20배 충돌'은 충돌이 아니라 **표기 단위**다. 두 문서가 같은 경제를 다른 자리수로 적었다.")
print("  ⇒ 진짜 충돌은 마지막 줄 하나뿐 — **전설이 동전으로 살 수 없다.**")


# ───────────────────────────────────────────────────────────────
# [6] 1일차 유저 — 무료 시작 장비(각 슬롯 rank0)만으로 무엇이 열리는가
# ───────────────────────────────────────────────────────────────
print(); print("="*78); print("[6] 1일차(Lv.1) 검산 — 무료 시작 장비 4종의 임계 돌파 여부"); print("="*78)
START = {'HEAD':'h1','EYES':'e1','NECK':'n1','BACK':'b1'}   # 우리 카탈로그 rank0 = 요구 레벨 1
rows1,_ ,_ = handoff_rows(START)
for r in rows1:
    print("  %-4s = %2d   단계 %-4s   %s" % (r['name'], r['total'], r['stage'], r['nextText']))
print("  ⇒ Lv.1 무료 장비만으로 **4스탯 전부 초급(10) 돌파**.")
print("     매력 초급 = '오라 이펙트 발현' → 설치 첫날 오라가 켜진다.")
print("     ECONOMY_SPEC 3-1이 명시적으로 막으려던 상태다: \"1일차 유저에게 오라와 잔상이 켜지는 것은")
print("     이 앱에서 가장 하면 안 되는 일\". 임계는 영구이므로 **되돌릴 수도 없다**.")

# ───────────────────────────────────────────────────────────────
# [7] 경제 — ×20 단위로 환산했을 때 인계본 화면 숫자가 성립하는가
# ───────────────────────────────────────────────────────────────
print(); print("="*78); print("[7] 경제 환산 — 인계본 자릿수(×20)에서의 획득·소모"); print("="*78)
K = 20
per_min_done, per_min_cancel = 1.2*K, 1.0*K
print("  집중모드 완주 분당 %.0f동전 / 취소 분당 %.0f동전  (ECONOMY_SPEC 4-2 ×%d)" % (per_min_done, per_min_cancel, K))
for m in (15,25,50):
    print("    %2d분 완주 = %5d동전   (시간당 %d)" % (m, int(per_min_done*m), int(per_min_done*60)))
print("  활쏘기 정중앙 1회 = %d동전, 쿨다운 600초 → %d동전/시" % (1*K, 6*K))
print("  [오늘 할일] 하루 1회 정액 = %d동전" % (15*K))
dayB = 64*K
print("  원형 B(기준 유저) 일일 수입 = %d동전/일" % dayB)
print()
print("  ▶ 렌더의 '보유 1,240'은 기준 유저 %.2f일치 수입이다  (1,240 / %d)" % (1240/dayB, dayB))
print("  ▶ 렌더의 '배낭 600'  = 집중 25분 %.1f회 = %.2f일" % (600/(per_min_done*25), 600/dayB))
for l,p in zip(RARN[:3], NEW[:3]):
    print("  ▶ %-4s %5d동전 = 집중 25분 %4.1f회 = %4.1f일" % (l, p, p/(per_min_done*25), p/dayB))
print("  ▶ 전설(가격 미정) — ECONOMY_SPEC ×20이면 6,600 = 집중 25분 11.0회 = 5.2일")
priced16 = sum(v[3] for v in H_ITEMS.values() if v[3])
print()
print("  인계본 16종 중 가격 있는 14종 합계 = %s동전 = %.1f일" % (format(priced16,','), priced16/dayB))
print("  (42종 총액은 나머지 26종 등급이 미정이라 **계산 불가** — 미정으로 남긴다)")

# ───────────────────────────────────────────────────────────────
# [8] 표시 가능성 — 창 1042 / 카드 메타 41pt
# ───────────────────────────────────────────────────────────────
print(); print("="*78); print("[8] 표시 가능성 (1042 / 메타 41pt)"); print("="*78)
# 교정: UX_SHOP_AND_CURRENCY 3-3 폭 검산표에서 역산 (숫자 5.5pt / 한글 10.0pt)
def w(s):
    t=0.0
    for ch in s:
        if ch.isdigit(): t+=5.5
        elif ch==',': t+=2.75
        elif ch==' ': t+=3.0
        else: t+=10.0
    return t
for s,want in [("59분",21.0),("80시간",31.0),("5시간",25.5),("3시간 뒤",38.5),("12시간 뒤",44.0)]:
    check("폭 교정 %s"%s, w(s), want)
if FAIL: print("  !!! 폭 교정 실패 — 아래 폭 판정 전부 무효"); sys.exit(1)
print("  ★ 폭 모형 교정 5/5 PASS (숫자 5.5pt / 한글 10.0pt / 쉼표 2.75pt)")
print()
for s in ["3,200","6,600","600","동전 3,200","3,200동전","LV.20","보유 중","고급까지 22","집중력 41 (+33)"]:
    print("    %-16s %6.2fpt  %s" % ('"'+s+'"', w(s), "메타41 OK" if w(s)<=41 else "★메타41 초과"))
print("  ⇒ 메타 41pt에는 **숫자만** 들어간다. '동전'이라는 낱말은 못 넣는다(47.75pt).")
print("  ⇒ 인계본은 '동전 3,200으로 해금'을 **하단 버튼**(mainLabel)에 두었다 — 그 자리는 폭 여유가 있다. 정합.")
print("  ⇒ 스탯 카드 최대 표기 '집중력 41 (+33)'도 2자리 안에 머문다. 3자리는 어떤 조합에서도 안 나온다.")
