# -*- coding: utf-8 -*-
"""R10-b — 회복제 유료부를 「재고형」에서 「영구 해금(C층)」으로 교체한 뒤의 재계산.
계기: security ENTITLEMENT_CONTRACT E-13 (소모품 잔량 = 평문 무한 표적) → 사용자 확정.
★ 규약: 알려진 값으로 먼저 교정한다. 깨지면 이후 숫자 전부 폐기."""

F=[]
def ck(t,got,want,tol=0.0):
    ok=(abs(got-want)<=tol) if isinstance(got,(int,float)) and isinstance(want,(int,float)) else got==want
    if not ok: F.append(t)
    print(("  [OK]  " if ok else "  [FAIL]")+" %-56s got=%s want=%s"%(t,got,want))

print("="*88); print("[0] 교정 — R10(§16) 확정값과 정본 §13-3을 다시 재현한다"); print("="*88)
FOCUS=24; ARCH=20; ARCH_CD=600; TODO=300; TOTAL42=115200; LEG=9600
A_act=3.4*20; B_act=FOCUS*25*2+ARCH*4; C_act=FOCUS*25*4+ARCH*8; D_act=FOCUS*50*4+ARCH*3600/ARCH_CD*10
IDLE_ON=12; IDLE_OFF=6; WIN_MIN=480; CAP_BASE=480; TONIC=480
ck("집중 25분 완주",FOCUS*25,600); ck("원형B 능동",B_act,1280); ck("원형D 능동",D_act,6000)
ck("완주 90.0일(정본, 유휴 이전)",round(TOTAL42/B_act,1),90.0)
ck("R10 온라인 유휴 요율",IDLE_ON,FOCUS/2)
ck("R10 절대 천장 = 창×요율",WIN_MIN*IDLE_ON,5760)
ck("R10 실효 상한(기본+무료 1개)",CAP_BASE+TONIC,960)
ck("R10 원형B 합계",B_act+960,2240); ck("R10 원형B 코인완주",round(TOTAL42/2240,1),51.4)
ck("Lv.30 8h/일",round(558.5/8,1),69.8); ck("Lv.30 24h 상주",round(558.5/24,1),23.3)
if F: raise SystemExit("★ 교정 실패 — 이후 폐기: "+str(F))
print("  ★ 교정 11/11 PASS\n")

print("="*88); print("[1] ★★ 먼저 반증 — 「요율만」 영구 상승시키면 수입이 1동전도 안 는다"); print("="*88)
print("  상한이 요율보다 먼저 걸린다. 캡 도달 시각 = 상한 / 요율 이므로:")
print("   요율   캡도달시각   온라인 8h 유저 하루 수입   온라인 40분 유저 하루 수입")
for r in [12,14,16,18,24]:
    cap=960
    d8 =min(cap, r*min(8*60, WIN_MIN)); d40=min(cap, r*40)
    print("   %2d/분   %5.1f분     %8s동전            %8s동전"%(r,cap/r,format(int(d8),','),format(int(d40),',')))
print("""
  ⇒ 온라인 %d분 이상 켜는 유저에게 요율 인상의 효과는 **정확히 0동전**이다.
     내가 모형화한 원형 A·B·C·D·B24는 **전부 8시간 이상** 온라인이다 → 전원 0.
  ⇒ 효과가 나는 것은 「하루 %d분 미만만 켜는 유저」뿐인데, 그 유저는 이 상품의 구매자가 아니다.
  ★ 【판】「요율 영구 상승」을 **글자 그대로 구현하면 팔 것이 없다.**
     수입을 만드는 축은 **일일 유휴 동전 상한**이다. 아래는 그 축으로 다시 짠 것이다."""%(960/12,960/12))

print("="*88); print("[2] 채택안 — 「유휴 일일 상한」 영구 상승 3단계 (세이브 0필드, C층 조회 파생)"); print("="*88)
STEP=240; TIERS=[0,1,2,3]
def cap_of(t): return CAP_BASE+TONIC+STEP*t          # 기본480 + 무료회복제480 + 유료240×단계
print("   단계  실효 일일 상한   캡도달(온라인)   원형B 합계   코인완주   전설=B일수   A/B    D/B   A′/B")
rows=[]
for t in TIERS:
    cap=cap_of(t); B=B_act+cap; A=A_act+cap; D=D_act+cap; A2=cap/2
    rows.append((t,cap,B))
    print("   T%d    %8s      %5.1f분      %8s   %6.1f     %5.2f    %5.3f  %5.2f  %5.3f"%(
        t,format(cap,','),cap/IDLE_ON,format(int(B),','),TOTAL42/B,LEG/B,A/B,D/B,A2/B))
print("""
  게이트 (R10 §16-3과 같은 기준)
   G1 A/B ≥0.20 · A′/B ≥0.20 : 전 단계 PASS
   G2 D/B ≤5.00              : 전 단계 PASS (T3에서 2.59 — 오히려 더 좁아진다)
   G3 전설 ≥ 원형B 3.0일치    : T3 3.24 PASS / T4(1,920)=3.00 경계 / T5(2,160)=2.79 FAIL
      ⇒ **판매 가능한 단계의 구조적 상한은 4단계다.** 권고는 여유를 둔 3단계.
   G5′ 어느 1분을 봐도 집중 ≥ 유휴 : 요율이 12로 **영구 불변**이므로 24 ≥ 12, **결제와 무관하게 항상 PASS**
      ★ 이게 상한축을 고른 두 번째 이유다 — 요율축이면 이 불변식이 단계 의존이 된다.""")

print("="*88); print("[3] 집중 모드가 죽지 않는가 — 단계별 재검산"); print("="*88)
for t in TIERS:
    cap=cap_of(t)
    print("   T%d: 캡 도달 %5.1f분 = 8시간 세션의 %4.1f%% → 그 뒤 집중 25분 한계가치 %d동전(전액)"%(
        t,cap/IDLE_ON,100*(cap/IDLE_ON)/480,FOCUS*25))
print("   ⇒ 최상위 단계에서도 하루의 %.1f%%는 유휴가 0이라 집중이 전액을 받는다."%(100*(1-(cap_of(3)/IDLE_ON)/480)))

print("\n"+"="*88); print("[4] product-strategy 인계 — ★ 재고형의 「최대 28.1일」은 폐기. 새 값"); print("="*88)
def real(cap, lvd): 
    c=TOTAL42/(B_act+cap); return c, max(c,lvd)
for lab,lvd in [("8h/일 유저",69.8),("24h 상주 유저",23.3)]:
    base_c,base_r=real(cap_of(0),lvd)
    print("  %-14s T0 실완주 %5.1f일"%(lab,base_r), end="")
    for t in [1,2,3]:
        c,r=real(cap_of(t),lvd); print("  | T%d %5.1f일(단축 %4.1f)"%(t,r,base_r-r), end="")
    print()
print("""
  ⇒ 최종 도달점(42종·스탯 천장)은 여전히 한 칸도 안 움직인다 (§0-2-7 승계)
  ⇒ 8h/일 유저 단축 0.0일 — **재고형과 같다**. 이 상품은 여전히 상주 코호트 전용이다
  ⇒ 상주 유저 최대 단축이 **28.1일 → 12.5일로 절반 이하가 됐다** (재고형은 비축 몰아쓰기가 가능했다)
  ⇒ 번들("묶음") = 3단계를 한 번에 사는 SKU. 개수 묶음이 아니다""")

print("="*88); print("[5] 8시간 창은 이제 발화하는가 — 정직하게 잰다"); print("="*88)
print("   창 × 요율 = %s동전   vs   최상위 단계 상한 %s동전  →  창은 상한의 %.2f배"%(
    format(WIN_MIN*IDLE_ON,','),format(cap_of(3),','),WIN_MIN*IDLE_ON/cap_of(3)))
print("   창 소모 최대 = 상한/요율 = %.0f분 (창 480분의 %.1f%%)"%(cap_of(3)/IDLE_ON,100*(cap_of(3)/IDLE_ON)/WIN_MIN))
print("""   ⇒ **평상시 창은 절대 발화하지 않는다.** 동전 상한이 항상 먼저 건다.
   ★ R10(재고형)에서는 회복제 11개를 몰아 쓰면 창에 정확히 닿았다 — 그 경로가 사라졌다.
     이것이 이번 교체의 **정직한 비용**이다. 창은 이제 「도달 가능한 상한」이 아니라
     **「어떤 결제·위조·시계 조작으로도 못 넘는 절대 천장」**으로만 산다(불변식 I-6′).
   ★ 그래서 I-6′ 테스트에는 **양성 대조가 필수다**: 동전 상한을 인위적으로 무한대로 두면
     창이 실제로 %s에서 자르는지 같은 테스트에서 확인한다. 없으면 영원히 조용한 초록이다."""%format(WIN_MIN*IDLE_ON,','))

print("="*88); print("[6] C층 조회 실패(Unknown) 시 피해 상한 — E-1/E-2/E-3 대응"); print("="*88)
over=cap_of(3)-cap_of(0)
print("   Unknown에서 **상위 단계를 적용**한다(E-2 「조회 실패는 회수하지 않는다」 + 정의서 원칙).")
print("   미보유자가 Unknown 상태로 하루 종일 있어도 초과 취득 = %s − %s = **%s동전/일**"%(
    format(cap_of(3),','),format(cap_of(0),','),format(over,',')))
print("   = 원형 B 하루 수입의 %.1f%% / 전설 1종의 %.1f%%. 그리고 그날 상한에 갇힌다."%(
    100*over/(B_act+cap_of(0)),100*over/LEG))
print("   ⇒ 관대하게 열어도 **피해가 유계**다. 반대로 NotOwned로 붕괴시키면 정상 결제자가 매일 잠긴다.")

print("\n"+"="*88); print("[7] 저장 빈도 — 재고 폐기로 **줄어든다**"); print("="*88)
print("   R10(재고형) 중앙값 8.11 / 최악 18.11회/일")
new_mid=4.11+1.00+1.00
print("   R10-b        중앙값 %.2f / 최악 %.2f회/일   (+%.3f%% / +%.3f%%)"%(new_mid,new_mid,100*(new_mid-4.11)/1440,100*(new_mid-4.11)/1440))
print("""   사라진 유발원 3개: 회복제 사용(1~11) · 무료 회복제 일일 지급(1.00) · 보유 수 변경
   남은 추가 2개: 오프라인 소급 지급(1.00) · 정상 종료 시 lastSeen 확정(1.00)
   ★ 사라진 저장 필드 3개: tonicOwned · tonicUsedToday · tonicFreeGrantedDateLocal
     그리고 **유료 단계는 세이브에 0필드다**(E-4 실행 간 캐시 금지 / E-8-c 화살표 금지)""")
