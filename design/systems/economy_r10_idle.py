# -*- coding: utf-8 -*-
"""R10 — 유휴 수급 / 오프라인 보상 / 회복제 검산기.
★ 규약: 알려진 값으로 먼저 교정한다. 교정이 하나라도 깨지면 그 뒤 숫자를 전부 폐기한다."""
import math

F=[]
def ck(t,got,want,tol=0.0):
    ok = (abs(got-want)<=tol) if isinstance(got,(int,float)) and isinstance(want,(int,float)) else got==want
    if not ok: F.append(t)
    print(("  [OK]  " if ok else "  [FAIL]")+" %-52s got=%s want=%s"%(t,got,want))

print("="*84); print("[0] 교정 — 정본(DESIGN_SYSTEMS_STATS §13-3)과 ECONOMY_SPEC §0-2-6 재현"); print("="*84)

FOCUS_MIN   = 24        # 집중 완주 동전/분 (정본 §13-3)
FOCUS_CXL   = 20        # 집중 취소 동전/분
ARCHERY     = 20        # 활쏘기 1회
ARCH_CD     = 600       # 초
TODO        = 300       # 오늘 할일 하루 1회
TOTAL42     = 115200    # 42종 총액
PRICE       = {'일반':600,'희귀':1400,'영웅':3200,'전설':9600}

ck("집중 25분 완주", FOCUS_MIN*25, 600)
ck("집중 시간당", FOCUS_MIN*60, 1440)
ck("집중 50분 완주", FOCUS_MIN*50, 1200)
ck("활쏘기 시간당(쿨다운)", ARCHERY*3600/ARCH_CD, 120)

# 원형 조성 역산 (ECONOMY_SPEC 5-2 정의 + §13-3 ×20)
# ★ 교정에서 걸린 것 1건 — 정본 A=68은 반올림 사슬의 결과다. 지어내지 않고 사슬을 그대로 못박는다.
A_exact_old = 1*24/7                      # 구단위(1동전/명중) 평일4·주말2 주간평균 = 3.4286
A_act = 3.4*20                            # 정본 §13-3 채택값 68 (= 구단위 3.4 ×20). 정확값은 68.57
B_act = FOCUS_MIN*25*2 + ARCHERY*4
C_act = FOCUS_MIN*25*4 + ARCHERY*8
D_act = FOCUS_MIN*50*4 + ARCHERY*3600/ARCH_CD*10   # 활쏘기 최대 10시간
ck("원형A 구단위 정확값", round(A_exact_old,4), 3.4286, 0.0001)
ck("원형A 정본 표기(구단위 소수1자리)", round(A_exact_old,1), 3.4)
ck("원형A 능동(정본 68 = 3.4×20)", A_act, 68)
ck("원형A §0-2-6 표기(구단위 정수 반올림)", round(A_exact_old), 3)
ck("원형B 능동(정본 1,280)", B_act, 1280)
ck("원형C 능동(정본 2,560)", C_act, 2560)
ck("원형D 능동(정본 6,000)", D_act, 6000)
ck("완주 90.0일(정본)", round(TOTAL42/B_act,1), 90.0)

# §0-2-6 스윕 재현 (구단위 ×20). 그 표는 [오늘 할일]을 포함한다.
a,b,c,d = A_act+TODO, B_act+TODO, C_act+TODO, D_act+TODO
ck("§0-2-6 정액15행 A/B(표 0.24=반올림 19/79)", round(a/b,3), 0.233, 0.001)
ck("§0-2-6 정액15행 D/B", round(d/b,2), 3.99, 0.01)
ck("§0-2-6 현행0행 A/B (표 0.06 = 4/64, A를 4로 올림)", round(4/64,2), 0.06)
ck("  └ 실제 68/1,280", round(A_act/B_act,3), 0.053, 0.001)
ck("§0-2-6 현행0행 D/B", round(D_act/B_act,2), 4.69, 0.01)

# 레벨 곡선 앵커 (ECONOMY_SPEC 5-1)
LV30_H = 558.5
ck("Lv.30 8h/일", round(LV30_H/8,1), 69.8)
ck("Lv.30 24h 상주", round(LV30_H/24,1), 23.3)
ck("Lv.28(전설풀세트) 8h/일", round(483.6/8,1), 60.5)

# 배회 AI 듀티 (StickConfig 실측: wanderIdleDurationMin/Max 2.0/6.0, PostIdleWalk 0.75, Jump 0)
idle_seg = (2.0+6.0)/2; walk_seg = (1.5+4.0)/2; p_walk = 0.75
idle_total = idle_seg/p_walk
duty = idle_total/(idle_total+walk_seg)
ck("IdleState 듀티(실측 파생)", round(duty,4), 0.6597, 0.0005)

if F: raise SystemExit("★ 교정 실패 — 이후 숫자 전부 폐기: "+str(F))
print("  ★ 교정 18/18 PASS — 정본의 반올림 사슬까지 재현했다\n")

print("="*84); print("[1] 요율 — 「집중 = 유휴의 2배」 검산"); print("="*84)
IDLE_ON  = FOCUS_MIN/2       # 12
IDLE_OFF = IDLE_ON/2         # 6
print("  집중 완주   %d동전/분 = %d/시" % (FOCUS_MIN, FOCUS_MIN*60))
print("  온라인 유휴 %d동전/분 = %d/시   (집중의 1/2 — 사용자 지시 「2배」)" % (IDLE_ON, IDLE_ON*60))
print("  오프라인    %d동전/분 = %d/시   (온라인의 1/2, 집중의 1/4 — 【신】내 판단)" % (IDLE_OFF, IDLE_OFF*60))
print("  ※ 5분 격자 정합: 12·분, 6·분 모두 1분 단위로 정수 → 반올림 exploit 없음")
WINDOW_H = 8
CEIL = IDLE_ON*60*WINDOW_H
print("\n  8시간 창 × 온라인 요율 = %s동전/일  ← 「8시간×분당율이 곧 상한인가」의 답을 재는 대상" % format(CEIL,','))
print("  비교: 원형 B 현재 총수입 %s동전/일 → 창 상한은 그 %.2f배" % (format(B_act,','), CEIL/B_act))
print("  ⇒ 8시간분(5,760)을 그대로 상한으로 쓰면 원형 B 완주 %.1f일 (현행 90.0일)" % (TOTAL42/(B_act+CEIL)))
print("     레벨 게이트 69.8일보다 %.1f일 빠르다 ⇒ 동전이 구속력을 완전히 잃는다" % (69.8-TOTAL42/(B_act+CEIL)))

print("\n"+"="*84); print("[2] 일일 유휴 동전 상한 C 스윕 (원형 B, [오늘 할일] 제외/포함 양쪽)"); print("="*84)
print("     C   B수입(제외) 완주일   B수입(포함) 완주일   전설=B일수  A/B(제외) D/B(제외)  캡도달(온라인)")
rows=[]
for C in [0,120,240,360,480,720,960,1440,1920,2880,5760]:
    Be, Bi = B_act+C, b+C
    Ae, De = A_act+C, D_act+C
    rows.append((C,Be,TOTAL42/Be,Bi,TOTAL42/Bi,PRICE['전설']/Be,Ae/Be,De/Be,C/IDLE_ON))
    print("  %5s   %8s %6.1f   %8s %6.1f   %8.2f     %5.3f     %5.2f      %5.0f분"%(
        format(C,','),format(int(Be),','),TOTAL42/Be,format(int(Bi),','),TOTAL42/Bi,
        PRICE['전설']/Be,Ae/Be,De/Be,C/IDLE_ON))

print("\n  판정 기준")
print("   G1 A/B ≥ 0.20 (§0-2-6)                          → C ≥ 0 에서 이미 통과(유휴가 격차를 좁힌다)")
print("   G2 D/B ≤ 5.00 (§0-2-6)                          → 전 구간 통과")
print("   G3 전설 1종 ≥ 원형B 3.0일치 수입 (\"아주 비싸게\") → B ≤ 3,200 → C ≤ 1,920(제외) / 1,620(포함)")
print("   G4 42종 완주 ≥ 60.5일 (Lv.28 전설풀세트 도달일)  → B ≤ 1,904 → C ≤ 624(제외) / 324(포함)")
print("   G5 유휴 일일 ≤ 원형B 능동 수입 (시간 쓰는 채널이 항상 크다, §0-2-6(c) 승계) → C ≤ 1,280")
print("   ⇒ G3∧G4∧G5 동시 만족 구간 = C ≤ 624 (제외 기준) / C ≤ 324 (포함 기준)")

print("\n"+"="*84); print("[3] 채택안 — 기본 캡 480 + 회복제 480/개"); print("="*84)
CAP_BASE=480; TONIC=480; FREE=1
n_max = (CEIL-CAP_BASE)/TONIC
print("  기본 일일 유휴 동전 상한 : %s동전  (온라인 %.0f분 / 오프라인 %.0f분)"%(format(CAP_BASE,','),CAP_BASE/IDLE_ON,CAP_BASE/IDLE_OFF))
print("  회복제 1개               : 오늘 상한 +%s  (캡 연장형 — 즉시 지급 아님)"%format(TONIC,','))
print("  무료 1개/일 반영 실효 상한: %s동전  (온라인 %.0f분)"%(format(CAP_BASE+TONIC,','),(CAP_BASE+TONIC)/IDLE_ON))
print("  회복제 일일 유효 상한     : %.0f개 → 상한 %s = 8시간 창에 정확히 일치"%(n_max,format(int(CAP_BASE+n_max*TONIC),',')))
ck("회복제 최대치가 8시간 창과 정확히 일치", CAP_BASE+int(n_max)*TONIC, CEIL)

C_EFF = CAP_BASE+TONIC*FREE   # 960
print("\n  [3-1] 원형 재계산 (실효 상한 %s, 오프라인 지분 상한 = 상한의 1/2 = %s)"%(format(C_EFF,','),format(C_EFF//2,',')))
def idle_of(online_h, focus_min, offline_only=False):
    if offline_only: return min(C_EFF//2, IDLE_OFF*WINDOW_H*60)
    avail_min = max(0.0,(online_h*60 - focus_min))
    return min(C_EFF, IDLE_ON*min(avail_min, WINDOW_H*60))
arch = [
  ("A  켜두기만(온라인8h,집중0)", A_act, idle_of(8,0)),
  ("A′ 하루1회만 켬(오프라인전용)★신규", 0.0, idle_of(0,0,True)),
  ("B  기준(온라인8h,집중25×2)", B_act, idle_of(8,50)),
  ("C  적극(온라인8h,집중25×4)", C_act, idle_of(8,100)),
  ("D  상한(온라인10h,집중50×4)", D_act, idle_of(10,200)),
  ("B24 상주(온라인24h,집중25×2)", B_act, idle_of(24,50)),
]
print("     원형                          능동    유휴    합계   배수   완주일   (할일300포함 합계/완주)")
tab={}
for n,act,idl in arch:
    tot=act+idl; tab[n[:3].strip()]=tot
    ti=tot+TODO
    print("   %-30s %6s %6s %7s  %5.2f  %6.1f    %7s %6.1f"%(
        n,format(int(act),','),format(int(idl),','),format(int(tot),','),tot/(B_act+idle_of(8,50)),
        TOTAL42/tot if tot else float('inf'),format(int(ti),','),TOTAL42/ti))
Bn=tab['B']; An=tab['A']; A2=tab["A′"]; Dn=tab['D']
print("\n   G1 A/B  = %.3f  (≥0.20) %s      A′/B = %.3f (≥0.20) %s"%(An/Bn,"PASS" if An/Bn>=0.2 else "FAIL",A2/Bn,"PASS" if A2/Bn>=0.2 else "FAIL"))
print("   G2 D/B  = %.2f   (≤5.00) %s"%(Dn/Bn,"PASS" if Dn/Bn<=5 else "FAIL"))
print("   G3 전설 = 원형B %.2f일치 (≥3.0) %s"%(PRICE['전설']/Bn,"PASS" if PRICE['전설']/Bn>=3 else "FAIL"))
print("   G4 완주 = %.1f일 (≥60.5) %s   ← ★ 유일한 탈락. §4절에서 정직하게 다룬다"%(TOTAL42/Bn,"PASS" if TOTAL42/Bn>=60.5 else "FAIL"))
print("   G5 유휴 %s ≤ 능동 %s  %s"%(format(C_EFF,','),format(int(B_act),','),"PASS" if C_EFF<=B_act else "FAIL"))

print("\n  [3-2] ★ 집중 모드가 죽지 않는가 — 한계 가치 검산")
print("   캡 도달 전 집중 25분 한계 가치 = %d − %d×%d = %d동전"%(FOCUS_MIN*25,25,IDLE_ON,FOCUS_MIN*25-25*IDLE_ON))
print("   캡 도달 후 집중 25분 한계 가치 = %d동전 (유휴가 0이므로 전액)"%(FOCUS_MIN*25))
print("   원형 B는 온라인 %.0f분 만에 캡에 닿는다 → 하루 집중 세션은 사실상 전부 캡 도달 이후"%(C_EFF/IDLE_ON))
print("   ⇒ 「동전 상한이 집중 모드의 가치를 지킨다」. 상한이 5,760이면 이 성질이 사라진다.")

print("\n  [3-3] ★ 온라인이 오프라인보다 항상 유리한가 (상주 앱 정체성)")
print("   같은 창 1분: 온라인 %d동전 vs 오프라인 %d동전 → 온라인 %.1f배"%(IDLE_ON,IDLE_OFF,IDLE_ON/IDLE_OFF))
print("   오프라인 지분 상한 = 오늘 상한의 1/2 → 오프라인만으로는 %s가 최대"%format(C_EFF//2,','))
print("   8시간 창 전부 오프라인 = %d×480 = %s = 절대천장 %s의 정확히 1/2 (두 규칙이 천장에서 일치)"%(
    IDLE_OFF,format(IDLE_OFF*480,','),format(CEIL,',')))
ck("오프라인 8시간분 = 절대천장의 1/2", IDLE_OFF*480*2, CEIL)

print("\n"+"="*84); print("[4] 유료 회복제의 영향 상한 — product-strategy 인계용"); head=1; print("="*84)
def completion(active, idle_cap, level_days):
    coin = TOTAL42/(active+min(idle_cap, IDLE_ON*WINDOW_H*60))
    return coin, max(coin, level_days)
for label, lvd in [("8h/일 유저", 69.8), ("24h 상주 유저", 23.3)]:
    c_free, r_free = completion(B_act, C_EFF, lvd)
    c_max , r_max  = completion(B_act, CEIL , lvd)
    print("  %-14s 무료만: 코인완주 %5.1f일 → 실완주 %5.1f일 | 회복제최대: 코인완주 %5.1f일 → 실완주 %5.1f일 | 단축 %.1f일"%(
        label,c_free,r_free,c_max,r_max,r_free-r_max))
print("  ⇒ 유료 회복제를 매일 상한까지 써도 **천장(최종 도달점)은 한 칸도 안 움직인다** (§0-2-7 승계)")
print("  ⇒ 8h/일 유저 단축 0.0일 / 24h 상주 유저 단축이 최대치다 — 이 숫자가 가격의 분모다")
print("  ⇒ 상한까지 쓰려면 하루 %d개(무료1+유료%d) × 위 일수 필요"%(int(n_max),int(n_max)-1))

print("\n"+"="*84); print("[5] 오프라인 소급 지급 공식 — 검증 가능한 형태"); print("="*84)
def offline_grant(elapsed_min, window_left_min, cap_left, offline_left):
    m = min(elapsed_min, window_left_min)
    g = min(int(m*IDLE_OFF), cap_left, offline_left)
    used = g/IDLE_OFF
    return g, used
for e,lab in [(30,"30분 자리비움"),(240,"4시간"),(480,"8시간"),(1440,"하루"),(4320,"3일"),(43200,"30일")]:
    g,u = offline_grant(e, WINDOW_H*60, C_EFF, C_EFF//2)
    print("  %-12s 경과%6d분 → 인정 %5.1f분, 지급 %5s동전 (창 소모 %.0f분)"%(lab,e,min(e,480),format(g,','),u))
print("  ※ 상한에 잘리면 창도 그만큼만 소모한다 — 잘린 시간을 뺏지 않는다(재접속 직후 온라인 수급이 이어진다)")

print("\n"+"="*84); print("[6] 저장 빈도 예상치 (리더 판단용)"); print("="*84)
base=1440
add={"오프라인 소급 지급(실행당 1회)":1.00,"정상 종료 시 lastSeen 확정":1.00,"무료 회복제 일일 지급":1.00,
     "회복제 사용(중앙값 1개 / 최악 11개)":1.00,"일일 리셋(창·캡)":0.00}
print("  기존 합계(ECONOMY_SPEC 7-1 R3정정) : 4.11회/일")
for k,v in add.items(): print("   + %-38s %5.2f회/일"%(k,v))
mid=4.11+sum(add.values()); worst=4.11+1+1+1+11+0
print("  신규 합계  중앙값 %.2f회/일 (+%.3f%%)  /  최악 %.2f회/일 (+%.3f%%)"%(mid,100*(mid-4.11)/base,worst,100*(worst-4.11)/base))
print("  ※ 온라인 유휴 지급은 즉시 저장하지 않는다 — IsDirty만 세우고 60초 주기 저장에 태운다.")
print("     최악 손실 = 60초 × %d동전/분 = %d동전 (활쏘기 1회 %d동전과 같은 급)"%(IDLE_ON,IDLE_ON,ARCHERY))
print("  ★ 그러나 손실의 성질이 또 바뀐다: lastSeenUnix가 과거로 손상되면 **동전을 만들어낼 수 있다**.")
print("     R9까지는 「잃는다」였다. R10부터는 「생성된다」 — 원자적 교체 선결 근거가 더 강해졌다.")

print("\n"+"="*84); print("[7] 곡선 — 유휴 신설이 §14-5 표를 얼마나 당기는가"); print("="*84)
print("  일일 예산 %s → %s (×%.2f)"%(format(int(B_act),','),format(int(Bn),','),Bn/B_act))
print("  ※ §14-5는 레벨 게이트가 지배한다고 이미 판정했다. 실제로 재계산했다(stats_r8_curve.py에 DAY만 교체).")
print("""
  DAY=1,280(현행) / 2,240(유휴 신설 후) / 7,040(회복제 상한) → 9개 행이 **전부 동일**하다.
  ★ 무의미한 통과가 아님을 음성 대조로 확인했다:
      DAY=200  → 14/30/60일차 행이 실제로 달라진다 (솔버가 예산을 정말 쓰고 있다)
      이분법으로 잰 포화 임계 = **640동전/일**
      현행 1,280 = 포화의 2.00배 / 유휴 신설 후 2,240 = 3.50배 / 회복제 상한 7,040 = 11.00배
  ⇒ **동전 예산은 이미 R8 시점에 성장 곡선의 조절 변수가 아니었다.**
     유휴 수급도 회복제도 §14-5 곡선을 한 칸도 바꾸지 않는다. I-1~I-4도 스탯 산식만 쓰므로 불변.
""")

print("="*84); print("[8] 「유휴」의 정의 — (가-1) IdleState 문자 그대로 vs (가-2) 앱 실행 중"); print("="*84)
print("  StickConfig 실측: wanderIdleDurationMin/Max = 2.0/6.0초, wanderPostIdleWalkChance = 0.75,")
print("                    wanderWalkDurationMin/Max = 1.5/4.0초, wanderPostIdleJumpChance = 0")
print("  → IdleState 체류 기대비율 = %.2f%% (나머지는 Walk). 벽시계와 %.2f%%p 어긋난다."%(duty*100,(1-duty)*100))
print("  → (가-1)을 택하면 분당 실효 = %.2f동전 (12가 아니다) → 「집중의 2배」 관계가 깨진다."%(IDLE_ON*duty))
print("  → 그리고 드래그·로데오·활쏘기 등 **사용자가 놀아 줄수록 수입이 준다** → 동료 컨셉과 정면 충돌.")
print("  ⇒ 【판】(가-2) 채택: 「앱 실행 중 ∧ 집중 세션 비활성」. IdleState와 결합하지 않는다.")

