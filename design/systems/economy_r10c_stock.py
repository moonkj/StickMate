# -*- coding: utf-8 -*-
"""R10-c — 사용자 확정 수치(2026-09-03): 재고형 회복제 + 오프라인 획득 전면 삭제.
  기본 일일 상한 1,500 / 회복제 1개 +500 / 하루 최대 2개 / 무료 1개(이월 없음)
★ 규약: 알려진 값으로 먼저 교정한다. 깨지면 이후 숫자 전부 폐기."""
F=[]
def ck(t,got,want,tol=0.0):
    ok=(abs(got-want)<=tol) if isinstance(got,(int,float)) and isinstance(want,(int,float)) else got==want
    if not ok: F.append(t)
    print(("  [OK]  " if ok else "  [FAIL]")+" %-58s got=%s want=%s"%(t,got,want))

print("="*90); print("[0] 교정 — 정본 §13-3 · ECONOMY_SPEC §5-1 · §0-2-6 을 다시 재현"); print("="*90)
FOCUS=24; ARCH=20; ARCH_CD=600; TODO=300; TOTAL42=115200; LEG=9600
A_act=3.4*20; B_act=FOCUS*25*2+ARCH*4; C_act=FOCUS*25*4+ARCH*8; D_act=FOCUS*50*4+ARCH*3600/ARCH_CD*10
ck("집중 25분 완주",FOCUS*25,600); ck("집중 시간당",FOCUS*60,1440)
ck("원형A 능동(정본 68=3.4×20)",A_act,68); ck("원형B 능동",B_act,1280)
ck("원형C 능동",C_act,2560); ck("원형D 능동",D_act,6000)
ck("완주 90.0일(유휴 이전)",round(TOTAL42/B_act,1),90.0)
ck("§0-2-6 정액15행 D/B",round((D_act+TODO)/(B_act+TODO),2),3.99,0.01)
ck("Lv.30 8h/일",round(558.5/8,1),69.8); ck("Lv.30 24h 상주",round(558.5/24,1),23.3)
ck("Lv.28 전설풀세트 8h/일",round(483.6/8,1),60.5)
if F: raise SystemExit("★ 교정 실패 — 이후 폐기: "+str(F))
print("  ★ 교정 11/11 PASS\n")

# ── 사용자 확정 상수 ─────────────────────────────────────────────
IDLE=12                 # 온라인 유휴 동전/분 (집중 24의 1/2 — 사용자 최초 지시)
CAP_BASE=1500           # 기본 일일 상한        ★사용자 확정
TONIC=500               # 회복제 1개당 상한 증가 ★사용자 확정
TONIC_MAX=2             # 하루 최대 사용 개수    ★사용자 확정
TONIC_FREE=1            # 무료 1개/일, 이월 없음  ★사용자 확정
WIN_MIN=480             # 인정 시간 창 8시간     ★사용자 확정(최초)
print("="*90); print("[1] 사용자 확정 수치의 자체 정합 검산"); print("="*90)
ck("총 추가 = 2개 × 500",TONIC*TONIC_MAX,1000)
ck("최종 상한 = 기본 + 총추가",CAP_BASE+TONIC*TONIC_MAX,2500)
print("""  ★ 「무료 1개」가 「하루 2개」에 **포함**된다 — 산술이 그것을 강제한다:
     포함이 아니라면 무료1 + 유료2 = 3회 = +1,500이 되어 사용자 원문 「총 1000개 추가」와 어긋난다.
  ⇒ **유료로 살 수 있는 것은 하루 1개뿐이고, 그 가치는 하루 %d동전이다.**"""%TONIC)
print("\n  실효 일일 상한 3구간")
for lab,n in [("무료분 미사용",0),("무료 1개 사용(기본 시나리오)",1),("무료1+유료1(최대)",2)]:
    cap=CAP_BASE+TONIC*n
    print("   %-26s 상한 %5s동전   캡 도달 %5.1f분 (8h 세션의 %4.1f%%)"%(lab,format(cap,','),cap/IDLE,100*(cap/IDLE)/480))
print("   ⇒ 하루 **2시간 5분**만 켜면 기본 상한(1,500)을 다 채운다 — 상주 앱에서 사실상 항상 충족")

print("\n"+"="*90); print("[2] 원형 재계산 — ★ 오프라인 삭제로 원형 A′는 폐기, 원형 A는 접속시간을 다시 잡는다"); print("="*90)
def idle_of(online_min, focus_min, cap):
    return min(cap, IDLE*min(max(0,online_min-focus_min), WIN_MIN))
scen=[("무료 미사용",CAP_BASE),("무료 1개(기본)",CAP_BASE+TONIC),("무료1+유료1(최대)",CAP_BASE+2*TONIC)]
arch=[("A  켜두기만(온라인 8h, 집중0)",A_act,480,0),
      ("A2 ★신규 가볍게 켬(온라인 2h)",A_act,120,0),
      ("B  기준(온라인 8h, 집중 25×2)",B_act,480,50),
      ("C  적극(온라인 8h, 집중 25×4)",C_act,480,100),
      ("D  상한(온라인 10h, 집중 50×4)",D_act,600,200)]
for lab,cap in scen:
    print("\n  ── 시나리오: %s (상한 %s) ──"%(lab,format(cap,',')))
    print("     원형                            능동    유휴     합계   배수   완주일   전설=B일수")
    tot={}
    for n,act,on,fo in arch:
        idl=idle_of(on,fo,cap); t=act+idl; tot[n[:2].strip()]=t
    Bn=tot['B']
    for n,act,on,fo in arch:
        idl=idle_of(on,fo,cap); t=act+idl
        print("   %-32s %5s %6s %8s  %5.2f  %6.1f     %5.2f"%(
            n,format(int(act),','),format(int(idl),','),format(int(t),','),t/Bn,TOTAL42/t,LEG/t))
    A1,A2,Dn=tot['A'],tot['A2'],tot['D']
    g1=A1/Bn; g1b=A2/Bn; g2=Dn/Bn; g3=LEG/Bn
    print("   G1 A/B=%.3f %s · A2/B=%.3f %s   G2 D/B=%.2f %s   G3 전설=%.2f일치 %s   G5′ 분당 집중24 ≥ 유휴12 PASS(요율 불변)"%(
        g1,"PASS" if g1>=.2 else "FAIL",g1b,"PASS" if g1b>=.2 else "FAIL",
        g2,"PASS" if g2<=5 else "FAIL",g3,"PASS" if g3>=3 else "★FAIL"))

print("\n"+"="*90); print("[3] 완주 곡선 — §13-3의 90.0일과 비교"); print("="*90)
print("   시나리오            원형B 수입   코인완주   Lv.30(8h)=69.8   Lv.30(상주)=23.3   실완주(8h/상주)")
for lab,cap in scen:
    B=B_act+idle_of(480,50,cap); c=TOTAL42/B
    print("   %-18s %8s   %6.1f일        %s              %s          %5.1f / %5.1f일"%(
        lab,format(int(B),','),c,"비구속" if c<69.8 else "구속","비구속" if c<23.3 else "구속",max(c,69.8),max(c,23.3)))
b_free=B_act+idle_of(480,50,CAP_BASE+TONIC); b_paid=B_act+idle_of(480,50,CAP_BASE+2*TONIC)
print("\n   ⇒ 8h/일 유저: 무료 %.1f일 → 유료 %.1f일   **단축 0.0일**(둘 다 레벨 69.8일이 지배)"%(
    max(TOTAL42/b_free,69.8),max(TOTAL42/b_paid,69.8)))
print("   ⇒ 24h 상주 유저: 무료 %.1f일 → 유료 %.1f일   **단축 %.1f일** ← product-strategy 가격 분모"%(
    TOTAL42/b_free,TOTAL42/b_paid,TOTAL42/b_free-TOTAL42/b_paid))

print("\n"+"="*90); print("[4] ★ 집중 모드가 죽는가 — 사용자 수치는 내 권고(960)보다 2.1배 크다"); print("="*90)
for lab,cap in scen:
    reach=cap/IDLE
    print("   %-18s 캡 도달 %5.1f분 → 8h 세션의 %4.1f%%가 캡 도달 후 = 집중 25분이 %d동전 전액"%(
        lab,reach,100*(1-reach/480),FOCUS*25))
print("   유휴 일일(%s) vs 원형B 능동(%s) = %.2f배 → ★ 내 G5(일일 총액) 기준은 **떨어진다**"%(
    format(CAP_BASE+TONIC,','),format(int(B_act),','),(CAP_BASE+TONIC)/B_act))
print("   그러나 분당 기준 G5′(집중 24 ≥ 유휴 12)는 **요율이 안 변하므로 영구 PASS**")

print("\n"+"="*90); print("[5] ★★ 재고 위조 피해 상한 — security 인계용 (막지 않고 「얼마나 나쁜가」만 잰다)"); print("="*90)
print("   위조 대상 1: tonicOwned(유료 재고)")
print("     → 사용자 확정 「하루 2개까지만」이 **그 자체로 클램프**다. 무료 1개는 어차피 받으므로")
print("        위조로 얻는 것은 **하루 유료 1개 = %d동전/일**뿐이다. **무한이 아니다.**"%TONIC)
print("     → 40일 누적 = %s동전 = 전설 %.1f종. security E-13-1의 「유료 SKU 무한」이 **구조적으로 무력화**된다"%(
    format(TONIC*40,','),TONIC*40/LEG))
print("   위조 대상 2: tonicUsedToday → min(n, %d) 클램프로 상한 %s 고정"%(TONIC_MAX,format(CAP_BASE+TONIC*TONIC_MAX,',')))
print("   ★ 위조 대상 3(진짜 위험): **일자 롤오버 = 상한 리셋**")
print("     → 시계를 하루 전진시킬 때마다 상한이 다시 열린다. 실제 획득에는 여전히 벽시계가 필요하지만,")
print("        상한을 무한히 리셋하면 실효 상한이 **요율 × 실제 시간**이 된다:")
print("        하루 24시간 전부 = 12 × 1,440분 = %s동전/일 (정상 최대 %s의 %.1f배)"%(
    format(IDLE*1440,','),format(CAP_BASE+2*TONIC,','),IDLE*1440/(CAP_BASE+2*TONIC)))
print("     → ★ **8시간 창을 「단조 증가 래칫」으로 구현하면** 시계를 몇 번 돌리든 하루 %s에서 잘린다"%format(WIN_MIN*IDLE,','))
print("        = 정상 최대의 %.2f배로 유계. **창이 이 모델에서 다시 살아 있는 규칙이 된다.**"%(WIN_MIN*IDLE/(CAP_BASE+2*TONIC)))

print("\n"+"="*90); print("[6] 저장 — 오프라인 삭제로 빠지고, 재고 복귀로 들어온다"); print("="*90)
print("   삭제: lastSeenUnixSeconds(오프라인 소급용) · idleOfflineCoinGrantedToday")
print("   부활: tonicOwned · tonicUsedToday(0~2) · tonicFreeGrantedToday")
print("   유지: idleDayStampLocal · idleWindowUsedSeconds · idleCoinGrantedToday")
add={"회복제 사용(최대 2회/일)":2.00,"무료 회복제 일일 지급":1.00,"유료 회복제 구매(평생 드묾)":0.00}
mid=4.11+sum(add.values())
print("   추가 즉시 저장 = 기존 4.11 + %.2f = **%.2f회/일** (+%.3f%%)  ※ 오프라인 소급(1.00)·종료시 확정(1.00)은 사라짐"%(
    sum(add.values()),mid,100*(mid-4.11)/1440))
print("   ⇒ R10 재고형 8.11(중앙값)/18.11(최악) → **%.2f (최악도 같음, 상한이 2개라 최악=중앙값)**"%mid)

print("\n"+"="*90); print("[7] ★ security T-14 반영 — T-D-11 최소 리필 간격 G의 경제적 영향"); print("="*90)
print("   공격: 시계를 하루씩 반복 전진 → 상한 리셋. G시간 게이트가 있으면 하루 최대 리필 = 24/G회.")
print("   단 실제 획득에는 벽시계가 필요하다: 상한 C를 채우려면 C/12분.")
print("     G(h)  하루 리필  이론 상한   창(480분) 적용 후   정상최대 2,500 대비   채우는 데 필요한 실시간")
for G in [24,22,20,18,12,8,4,2]:
    n=24/G; raw=n*(CAP_BASE+2*TONIC); eff=min(raw, IDLE*WIN_MIN); need=eff/IDLE
    print("     %4d   %6.2f회  %8s   %10s      %6.2f배          %6.0f분"%(
        G,n,format(int(raw),','),format(int(eff),','),eff/(CAP_BASE+2*TONIC),need))
print("""
   ⇒ **G=20h이면 시계 조작 상한이 3,000동전/일 = 정상 최대의 1.20배**로 눌린다.
      내가 §18-7에서 낸 「창 래칫만 = 2.30배」보다 **더 강하다.**""")

print("\n   ★ 그러나 두 방어선은 **둘 다 필요하다** — 무력화 경로가 서로 다르다")
print("     방어 조합                                 실효 상한/일   정상 최대 대비")
for lab,eff in [("무방비",IDLE*1440),("창 래칫만",IDLE*WIN_MIN),("G=20h 게이트만",24/20*(CAP_BASE+2*TONIC)),
                ("둘 다 (게이트 유효)",min(24/20*(CAP_BASE+2*TONIC),IDLE*WIN_MIN)),
                ("둘 다 (★앱 재시작으로 게이트 우회)",IDLE*WIN_MIN)]:
    print("     %-40s %8s      %5.2f배"%(lab,format(int(eff),','),eff/(CAP_BASE+2*TONIC)))
print("""     ★ security 원문이 게이트를 「세이브에 안 남는 단조 시계」로 규정했다.
        그러면 **앱을 재시작할 때마다 게이트가 초기화**된다(첫 실행은 항상 통과 = 오탐 0의 근거이자 구멍).
        공격 1사이클에 앱 재시작이 추가될 뿐이므로 **창 래칫이 최후 방어선으로 남아야 한다.**""")

print("\n   ★ G=20h의 오탐 비용 — 0이 아니다. 정직하게 잰다")
print("     직전 리필이 23:59에 났다면 다음 리필 가능 시각 = +20h = 다음날 19:59")
print("     → 자정에 날짜는 바뀌었는데 무료 회복제가 **최대 19.98시간 지연**된다")
print("     → 그날 상한이 %s에 머물러 손실 ≤ **%d동전**(회복제 1개분). 잃는 것이 아니라 **늦는 것**이다"%(
    format(CAP_BASE,','),TONIC))
print("     → 발생 조건: 그날 첫 접속이 자정 직전. 상주 앱에서는 드물지만 0은 아니다")

print("\n"+"="*90); print("[8] ★★ T-D-12 검증 — security의 「4,550동전 / 9.1회」는 **구값이다**"); print("="*90)
OLD=4550
print("   security T-14 원문: \"총 소모처가 4,550동전(42종 완주)\" → 9.1회 / 회복제 약 3개어치")
print("   ★ 4,550은 ECONOMY_SPEC §5-4의 **구단위(×1) 유료 35종 총액**이고, 2026-09-03에 ×20으로 대체됐다.")
print("     정본(§13-3) 42종 완주 총액 = %s동전 = 4,550의 %.1f배"%(format(TOTAL42,','),TOTAL42/OLD))
print("   ⇒ 「9.1회면 살 게 없어진다」를 정본으로 다시 계산하면:")
print("        %s / %s(1회 리셋 최대) = **%.1f회**  (9.1회가 아니다)"%(
    format(TOTAL42,','),format(CAP_BASE+2*TONIC,','),TOTAL42/(CAP_BASE+2*TONIC)))
print("\n   ★ 그러나 **T-D-12의 결론 자체는 살아 있다** — 오히려 더 좋은 형태로 다시 유도된다:")
print("     피해 상한은 「몇 회」가 아니라 **「완주가 며칠 당겨지는가」**이고, 레벨 게이트가 그 위를 막는다.")
print("       원형                     일일수입    코인완주   실완주(상주, 레벨 23.3일)")
for lab,extra in [("정상(무료 1개)",CAP_BASE+TONIC),("창 래칫만 뚫림",IDLE*WIN_MIN),("무방비",IDLE*1440)]:
    inc=B_act+extra; c=TOTAL42/inc
    print("       %-22s %8s  %6.1f일     %6.1f일"%(lab,format(int(inc),','),c,max(c,23.3)))
base=TOTAL42/(B_act+CAP_BASE+TONIC)
print("     ⇒ **1인당 피해 상한 = %.1f − 23.3 = %.1f일 단축**이고, 그 이상은 레벨이 막는다."%(base,base-23.3))
print("        최종 도달점(42종·스탯 천장)은 **한 칸도 안 움직인다** — §0-2-7이 여기서도 성립한다.")
print("     ⇒ 이 유한성이 성립하는 **유일한 이유가 「살 것이 유한하다」**는 것이다 (T-D-12 원칙 확인).")
