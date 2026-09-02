# -*- coding: utf-8 -*-
"""
활쏘기 사거리 밴드 재설계 — 검산기.
★ 규칙: 알려진 값으로 먼저 교정한다. 교정이 깨지면 그 뒤 숫자를 전부 폐기한다.

교정 표본(전부 다른 출처):
  (1) 사용자 실기 로그 3줄 (2026-09-02) — 밴드 3.72~6.76 / 추첨 4.60·6.00·5.42 / 구간 최대 40.92·15.43
  (2) design-motion 산출물 2026-09-02_활쏘기_사거리하한_계산.out.txt
      "Dock W=1058pt, s=0.60 -> S=1004.9pt = 24.56유닛"
  (3) Core/StickConfig.cs / Interaction/ArcheryDirector.cs / States/ArcheryState.cs 의 실제 상수
"""
import math, random

# ── 코드에서 읽은 값 (베끼지 않고 출처를 주석으로 남긴다) ───────────────────────
H_BASE   = 2.2746944   # StickConfig.BaselineCharacterTotalHeight
PT_UNIT  = 982.0/24.0  # Core/DockGeometry: 화면 982pt <-> 카메라 세로 2*orthoSize(12)=24유닛
MIN_R    = 2.6         # archeryMinTargetDistanceRatio   (asset 2.6 == 코드 2.6f)
MAX_R    = 6.6         # archeryMaxTargetDistanceRatio   (asset 6.6)
F_SPAN   = 0.55        # archeryMinDistanceSpanFraction  (asset 0.55)
RAD_R    = 0.40        # archeryTargetRadiusRatio        (asset 0.4)
CHAR_INS = 0.35        # ArcheryDirector.CharacterEdgeInsetRatio
TGT_INS  = 0.20        # ArcheryDirector.TargetEdgeInsetRatio  (+반지름)
BACKSTEP = 1.0         # ArcheryDirector.BackStepRatio
ARRIVE   = 0.12        # ArcheryState.ArriveToleranceRatio
WALK     = 2.5         # walkSpeed (asset 2.5) — 실효 = WALK*scale, H/s로는 WALK/H_BASE
T_BASE   = 0.62        # archeryArrowFlightSeconds
T_MAX    = 1.25        # archeryArrowFlightMaxSeconds
REF_R    = 4.6         # archeryTargetDistanceRatio (비행시간 기준 사거리)
INTRO    = 0.55; DRAW = 0.42; AIM = 0.30; RECOVER = 0.34; OUTRO = 0.55
MISS_SF  = 1.5         # archeryMissShortfallRadii

SPAN_INSET = CHAR_INS + RAD_R + TGT_INS   # span = 발판폭 - 0.95H

def clamp(v,a,b): return a if v<a else (b if v>b else v)

# ── 프로덕션 ResolvePlacement 포팅(현행) ─────────────────────────────────────
def resolve(footX, lo, hi, H, roll, band_hi_fn, band_lo_fn, remap=lambda r,*a: r):
    charI = H*CHAR_INS; tgtI = H*(RAD_R+TGT_INS); back = H*BACKSTEP
    minD  = H*MIN_R
    standLo, standHi = lo+charI, hi-charI
    targetLo, targetHi = lo+tgtI, hi-tgtI
    if hi<=lo or standHi<standLo or targetHi<targetLo: return None
    spanR, spanL = targetHi-standLo, standHi-targetLo
    facing = 1.0 if abs(footX-lo) <= abs(hi-footX) else -1.0
    span = spanR if facing>0 else spanL
    if span < minD:
        facing = -facing; span = spanR if facing>0 else spanL
        if span < minD: return None
    bandHi = band_hi_fn(span, H)
    bandLo = band_lo_fn(bandHi, span, H)
    d = bandLo + (bandHi-bandLo)*remap(clamp(roll,0,1), bandLo, bandHi, H)
    if facing>0: slotLo, slotHi = standLo, min(standHi, targetHi-d)
    else:        slotLo, slotHi = max(standLo, targetLo+d), standHi
    if slotHi < slotLo:
        if slotLo-slotHi > 0.001: return None
        slotLo = slotHi = (slotLo+slotHi)*0.5
    standX = clamp(footX - facing*back, slotLo, slotHi)
    targetX = standX + facing*d
    return dict(d=d, standX=standX, targetX=targetX, facing=facing, span=span,
                bandLo=bandLo, bandHi=bandHi, standLo=standLo, standHi=standHi,
                targetLo=targetLo, targetHi=targetHi, footX=footX)

# ── 밴드 정의 3종 ───────────────────────────────────────────────────────────
CUR_HI = lambda span,H: min(H*MAX_R, span)
CUR_LO = lambda bh,span,H: max(H*MIN_R, F_SPAN*bh)

def mk_prop_hi(g, ucap):
    return lambda span,H: min(span, max(H*MAX_R, min(g*span, H*ucap)))
# 제안 하한: f × min(bandHi, 기준상한 6.6H)  ← 현행에서는 항상 무효(bandHi<=6.6H이므로)
PROP_LO = lambda bh,span,H: max(H*MIN_R, F_SPAN*min(bh, H*MAX_R))
NAIVE_LO = CUR_LO   # "그냥 넓히기"

def mk_remap(q):
    """구간 [bandLo, U0]에 확률 1-q, 확장구간 [U0, bandHi]에 q. bandHi<=U0이면 항등(균등)."""
    def remap(r, bandLo, bandHi, H):
        U0 = H*MAX_R
        if bandHi <= U0 + 1e-9 or bandHi-bandLo < 1e-9: return r
        cut = (U0-bandLo)/(bandHi-bandLo)      # U0의 정규화 위치
        if r < (1.0-q): return cut * (r/(1.0-q))
        return cut + (1.0-cut) * ((r-(1.0-q))/q)
    return remap

def flight(d, H):
    return clamp(T_BASE*math.sqrt(max(0.25, d/(H*REF_R))), T_BASE*0.6, T_MAX)

# ── 교정 ────────────────────────────────────────────────────────────────────
def calib():
    ok = True
    def chk(name, got, want, tol):
        nonlocal ok
        good = abs(got-want) <= tol
        ok &= good
        print(f"  [{'OK ' if good else 'FAIL'}] {name}: 계산 {got:.4f} / 기대 {want} (허용 ±{tol})")
    print("== 교정 ==")
    H = H_BASE*0.45
    chk("신장(배율0.45)", H, 1.024, 0.001)
    chk("밴드 상한 6.6H", H*MAX_R, 6.76, 0.005)
    chk("밴드 하한 0.55*상한", F_SPAN*H*MAX_R, 3.72, 0.005)
    # 로그 3줄의 roll 역산이 [0,1] 안인가
    for want in (4.60, 6.00, 5.42):
        r = (want - F_SPAN*H*MAX_R)/(H*MAX_R - F_SPAN*H*MAX_R)
        good = 0.0 <= r <= 1.0
        ok_ = good; print(f"  [{'OK ' if good else 'FAIL'}] 추첨 {want:.2f} -> roll {r:.4f} (0~1 이내)")
        if not good: globals()['__bad']=True
    # design-motion 산출물 재현
    Hm = H_BASE*0.60
    span_pt = (1058.0/PT_UNIT - SPAN_INSET*Hm)*PT_UNIT
    chk("design-motion Dock W=1058pt,s=0.60 -> span pt", span_pt, 1004.9, 0.6)
    chk("design-motion 같은 줄 -> span 유닛", span_pt/PT_UNIT, 24.56, 0.02)
    # 실제 배치 함수가 로그 밴드를 그대로 내는가 (발판 폭은 로그의 span에서 역산)
    for span_log, drawn in ((40.92,4.60),(15.43,6.00),(15.43,5.42)):
        W = span_log + SPAN_INSET*H
        lo, hi = 0.0, W
        r = (drawn - F_SPAN*H*MAX_R)/(H*MAX_R - F_SPAN*H*MAX_R)
        p = resolve(lo+W*0.35, lo, hi, H, r, CUR_HI, CUR_LO)
        chk(f"재현 span={span_log} 밴드하한", p['bandLo'], 3.72, 0.006)
        chk(f"재현 span={span_log} 밴드상한", p['bandHi'], 6.76, 0.006)
        chk(f"재현 span={span_log} 실제 span", p['span'], span_log, 0.005)
        chk(f"재현 span={span_log} 추첨거리", p['d'], drawn, 0.006)
    # 실기 화면 형상 역산 — design-motion이 쓰는 SCREEN_W_PT=1512(16:10.4 내장화면)와 모순되는가
    vis16x9  = 2*12*(16/9)          # 카메라 세로 24유닛 x 종횡비
    vis16x10 = 2*12*1.539           # 1512x982 내장화면 종횡비
    need = 40.92 + SPAN_INSET*H     # 로그 span이 성립하려면 걸어다닐 수 있는 폭이 최소 이만큼
    print(f"  [참고] 로그 span 40.92유닛 -> 걸어다닐 수 있는 폭 >= {need:.2f}유닛")
    print(f"         1512x982 내장화면 가시폭 {vis16x10:.2f}유닛 -> {'가능' if vis16x10>=need else '★불가능(모순)'}")
    print(f"         16:9 외장화면 가시폭 {vis16x9:.2f}유닛, 여유 {vis16x9-need:.2f}유닛 "
          f"(좌우 클램프 여유 2x(8pt+시각반폭) 예상 0.7~0.9유닛) -> {'정합' if 0.5 <= vis16x9-need <= 1.2 else '불일치'}")
    print("== 교정 " + ("통과 ==" if ok else "실패 — 이하 숫자 전부 폐기 =="))
    return ok

if __name__ == "__main__":
    if not calib(): raise SystemExit(1)

# ============================================================================
# 분석
# ============================================================================
def hdr(s): print("\n"+"="*78+"\n"+s+"\n"+"="*78)

SCALE_USER = 0.45          # 사용자 실기 배율(로그 신장 1.024에서 역산)
G_DEF, UCAP_DEF, Q_DEF = 0.35, 13.4, 0.35

def band(span, H, hi_fn, lo_fn):
    bh = hi_fn(span,H); return lo_fn(bh,span,H), bh

def analysis():
    H = H_BASE*SCALE_USER
    PROP_HI = mk_prop_hi(G_DEF, UCAP_DEF)

    hdr("[A] 발판 폭별 밴드 — 사용자 배율 0.45 (H=%.4f유닛=%.1fpt)" % (H, H*PT_UNIT))
    print("  발판폭W       span      현행 밴드(H)      제안 밴드(H)     그냥넓히기(H)   상한/span")
    rows = [("Dock/바탕(실기)", 40.92+SPAN_INSET*H), ("작은 창(실기)", 15.43+SPAN_INSET*H)]
    rows += [(f"가상 W={w}pt", w/PT_UNIT) for w in (200,300,400,500,700,900,1200,1714,2400,3400)]
    for name,W in rows:
        p = resolve(W*0.35, 0, W, H, 0.5, CUR_HI, CUR_LO)
        if p is None: print(f"  {name:>16}  {W*PT_UNIT:7.0f}pt  포기"); continue
        sp = p['span']
        c = band(sp,H,CUR_HI,CUR_LO); q = band(sp,H,PROP_HI,PROP_LO); n = band(sp,H,PROP_HI,NAIVE_LO)
        print(f"  {name:>16} {sp/H:6.2f}H  {c[0]/H:5.2f}~{c[1]/H:5.2f}   "
              f"{q[0]/H:5.2f}~{q[1]/H:5.2f}   {n[0]/H:5.2f}~{n[1]/H:5.2f}   "
              f"현행{c[1]/sp*100:5.1f}% 제안{q[1]/sp*100:5.1f}%")

    hdr("[B] '무조건 화면 끝' 재발 검사 — 과녁이 구간 끝(targetHi)에 못박히는 비율")
    print("  모형: footX는 [standLo,standHi] 균등, roll 균등, 표본 200000. 못박힘 = targetHi-targetX < 0.01H")
    print("  이론: 못박힘 없음 <=> bandHi <= 0.5*span + 0.875H  (유도는 설계문서 3-2)")
    random.seed(20260902)
    for label, W_units in (("바탕(span 39.98H)", 40.92+SPAN_INSET*H), ("작은 창(span 15.07H)", 15.43+SPAN_INSET*H)):
        print(f"  --- {label}")
        for gname, hi_fn, lo_fn in (("현행", CUR_HI, CUR_LO),
                                     ("제안 g=0.35", mk_prop_hi(0.35,UCAP_DEF), PROP_LO),
                                     ("제안 g=0.50", mk_prop_hi(0.50,UCAP_DEF), PROP_LO),
                                     ("g=0.65(위험)", mk_prop_hi(0.65,UCAP_DEF), PROP_LO),
                                     ("상한없음(8-31 이전)", lambda s,H: s, PROP_LO)):
            pin=0; N=200000; edge=[]
            for _ in range(N):
                p = resolve(random.uniform(0.35*H, W_units-0.35*H), 0, W_units, H, random.random(), hi_fn, lo_fn)
                if p is None: continue
                gap = min(p['targetHi']-p['targetX'], p['targetX']-p['targetLo'])
                if gap < 0.01*H: pin += 1
                edge.append(gap/p['span'])
            bh = band(W_units-SPAN_INSET*H, H, hi_fn, lo_fn)[1]
            bound = 0.5*(W_units-SPAN_INSET*H) + 0.875*H
            print(f"    {gname:>20}: 못박힘 {pin/N*100:5.1f}%  | bandHi {bh/H:5.2f}H "
                  f"vs 방어선 {bound/H:5.2f}H  -> {'안전' if bh<=bound+1e-6 else '★위반'}")

    hdr("[C] 분포 — 사거리 백분위 (바탕 span 39.98H, 사용자 배율)")
    W = 40.92+SPAN_INSET*H
    random.seed(7)
    for name, hi_fn, lo_fn, rm in (("★상한없음(8-29~8-31 신고본)", lambda sp,H: sp, CUR_LO, lambda r,*a: r),
                                    ("현행(균등)", CUR_HI, CUR_LO, lambda r,*a: r),
                                    ("그냥넓히기 g=.35 균등", mk_prop_hi(G_DEF,UCAP_DEF), NAIVE_LO, lambda r,*a: r),
                                    ("제안 g=.35 균등(q=1의 극단)", mk_prop_hi(G_DEF,UCAP_DEF), PROP_LO, lambda r,*a:r),
                                    ("제안 g=.35 q=0.35", mk_prop_hi(G_DEF,UCAP_DEF), PROP_LO, mk_remap(0.35)),
                                    ("제안 g=.35 q=0.50", mk_prop_hi(G_DEF,UCAP_DEF), PROP_LO, mk_remap(0.50))):
        ds=[]; edge10=0; edge20=0; m=0
        for _ in range(200000):
            p = resolve(random.uniform(0.35*H, W-0.35*H), 0, W, H, random.random(), hi_fn, lo_fn, rm)
            if p:
                ds.append(p['d']/H); m+=1
                u = min(p['targetHi']-p['targetX'], p['targetX']-p['targetLo'])/(p['targetHi']-p['targetLo'])
                if u < 0.10: edge10+=1
                if u < 0.20: edge20+=1
        ds.sort(); n=len(ds)
        pct = lambda x: ds[int(x*(n-1))]
        near = sum(1 for d in ds if d <= 6.6)/n
        far  = sum(1 for d in ds if d >= 8.0)/n
        random.seed(1234)
        diff = sum(abs(ds[random.randrange(n)]-ds[random.randrange(n)]) for _ in range(200000))/200000.0
        print(f"  {name:>26}: 평균{sum(ds)/n:6.2f}H  p10 {pct(.1):5.2f}  p50 {pct(.5):5.2f}  "
              f"p90 {pct(.9):5.2f}  max {ds[-1]:5.2f} | <=6.6H {near*100:4.1f}%  >=8H {far*100:4.1f}%"
              f" | 연속2회 사거리차 E={diff:5.2f}H {'OK' if diff>=1.0 else '★<1H'}"
              f" | 과녁이 구간 바깥 10%/20%대 {edge10/m*100:4.1f}%/{edge20/m*100:4.1f}%")

    hdr("[C-2] 2026-09-02 아침 수정(하한만 폭비례)이 실제로 한 일 — 넓은 발판 기준")
    for lbl, a, b in (("8-31~9-02아침(하한 2.6H)", 2.6*H, 6.6*H),
                      ("9-02 아침 이후(하한 3.63H)", F_SPAN*6.6*H, 6.6*H),
                      ("제안 g=.35 q=.35(바탕)", None, None)):
        if a is None: continue
        print(f"  {lbl:>26}: 밴드폭 {(b-a)/H:5.2f}H  평균 {((a+b)/2)/H:5.2f}H ({((a+b)/2)*PT_UNIT:6.1f}pt)  "
              f"연속2회차 E={((b-a)/3)/H:5.2f}H  {'OK' if (b-a)/3>=H else '★<1H(랜덤이 눈에 안 보인다)'}")
    print("  -> 하한만 올리면 평균은 4.60H->5.11H(+11%, 화면상 +21pt)뿐인데 밴드폭은 4.00H->2.97H(-26%)로 줄어든다.")
    print("     사용자가 같은 문장을 두 번 말한 이유가 여기 있다: '더 멀리'는 거의 안 됐고 '랜덤'은 오히려 죽었다.")

    hdr("[D] 연출 시간 — 3발 + 접근 (초)")
    print("  접근 = 뒤로 1H 물러섬, 도착허용 0.12H -> 실보행 0.88H / 보행속도 %.4f H/s" % (WALK/H_BASE))
    walk_t = (BACKSTEP-ARRIVE)/(WALK/H_BASE)
    print(f"  접근 {walk_t:.2f}초 + 인트로 {INTRO} + 3x(당김{DRAW}+조준{AIM}+회복{RECOVER}={DRAW+AIM+RECOVER:.2f}) + 아웃트로")
    print("   d(H)   비행(초)  총 사이클(초)   마지막화살 착탄후 잔류(초)  다음발 발사 전 착탄?")
    for d in (2.60,3.63,4.60,6.60,8.00,10.00,12.00,13.40,13.99,16.00,18.70,22.00):
        T = flight(d*H, H)
        total = walk_t + INTRO + 3*(DRAW+AIM+RECOVER) + T_BASE + OUTRO
        dwell = RECOVER + T_BASE + OUTRO - T          # Outro는 고정 0.62를 쓴다(코드 실측)
        overlap_ok = T <= (RECOVER+DRAW+AIM)
        print(f"  {d:5.2f}  {T:6.3f}   {total:8.2f}      {dwell:8.3f}            "
              f"{'예' if overlap_ok else '★아니오(2발 동시 체공)'}")
    print(f"  ※ 총 사이클은 사거리와 무관하게 {walk_t + INTRO + 3*(DRAW+AIM+RECOVER) + T_BASE + OUTRO:.2f}초 고정이다 —")
    print("    Outro가 resolve된 비행시간이 아니라 고정 archeryArrowFlightSeconds(0.62)를 읽기 때문(코드 실측).")
    print(f"  ※ 두 발 동시 체공 임계: 비행 > 회복+당김+조준 = {RECOVER+DRAW+AIM:.2f}초 <=> d > "
          f"{REF_R*((RECOVER+DRAW+AIM)/T_BASE)**2:.2f}H")
    print(f"  ※ 비행시간 상한 {T_MAX}초에 물리는 사거리 = {REF_R*(T_MAX/T_BASE)**2:.2f}H (그 위는 화살이 빨라지기만 한다)")

    hdr("[D-2] design-motion R4 '착탄 비트 0.26초 게이트'와 겹쳤을 때 (교차 검토)")
    print("  그쪽 제안: gate = max(recover 0.34, 비행 + 0.26). 그러면 다음 Draw는 항상 착탄 0.26초 뒤 -> ")
    print("  ★ '2발 동시 체공'이 구조적으로 사라진다 => 내 Ucap 13.4H 제약이 풀린다(상한을 18.7H까지 열 수 있다).")
    BEAT=0.26; AIM2=0.22
    # ★ R4 수정 (2026-09-02, design-motion R5 지적으로 발견):
    #   여기에 접근 시간을 **0.907로 하드코딩**하고 있었다[인용]. 그 값은 §6-1 #5에서 내가 스스로
    #   "0.801이 맞다"고 교정한 값의 **교정 전** 버전이다 — 교정본과 사용처가 갈라져 있었다.
    #   이제 리터럴을 없애고 위에서 이미 코드에서 읽어 둔 상수로만 유도한다.
    #   그리고 분모(현행 총계)도 같은 접근 값을 쓰는 프레임으로 맞춘다 — 분자만 고치면
    #   서로 다른 두 프레임을 나누게 되어 새로운 오차가 생긴다.
    A_MIN = (BACKSTEP-ARRIVE)/(WALK/H_BASE)          # 도착 허용오차를 전부 쓰는 극단 = 하한
    A_MAX = BACKSTEP/(WALK/H_BASE)                   # 허용오차를 안 쓰는 극단 = 상한(design-motion R4 전제)
    def total_gate(d,H,A):
        T=flight(d*H,H)
        return A + INTRO + (DRAW+AIM) + (T+BEAT) + (DRAW+AIM2) + (T+BEAT) + (DRAW+AIM2) + (T+OUTRO)
    def total_cur(A):
        return A + INTRO + 3*(DRAW+AIM+RECOVER) + T_BASE + OUTRO
    H3=H_BASE*SCALE_USER
    THEIRS_A = 0.907   # [인용] design-motion R4 §2-5가 쓴 값. 계산에 쓰지 않고 교정에만 쓴다.
    print(f"  교정A(남의 숫자로): 그쪽 전제 A={THEIRS_A}로 d=4.60H 총계 "
          f"{total_gate(4.60,H3,THEIRS_A):.3f} / design-motion 표 6.387 -> "
          f"{'OK' if abs(total_gate(4.60,H3,THEIRS_A)-6.387)<0.005 else '★불일치'}")
    print(f"  교정B(같은 전제의 분모): 현행 총계 {total_cur(THEIRS_A):.3f} / 그쪽 표 5.807 -> "
          f"{'OK' if abs(total_cur(THEIRS_A)-5.807)<0.005 else '★불일치'}")
    print(f"  교정C(내 §6 값): 교정 접근 {A_MIN:.3f}초로 현행 총계 {total_cur(A_MIN):.3f} / §6 표 5.70 -> "
          f"{'OK' if abs(total_cur(A_MIN)-5.70)<0.005 else '★불일치'}")
    base=total_cur(A_MIN)
    print(f"   d(H)  비행(초)  게이트(초)  총계(초)  현행 {base:.3f} 대비")
    for d in (3.63,4.60,6.60,10.00,13.40,18.70):
        T=flight(d*H3,H3); t=total_gate(d,H3,A_MIN)
        print(f"  {d:5.2f}  {T:6.3f}   {T+BEAT:7.3f}  {t:7.3f}   {(t/base-1)*100:+6.1f}%")
    lo=total_gate(3.63,H3,A_MIN); hi=total_gate(13.40,H3,A_MIN)
    print(f"  -> 내 밴드(3.63~13.40H)를 넣으면 사이클이 {lo:.2f}~{hi:.2f}초로 **사거리에 따라 변한다**(폭 {hi-lo:.2f}초).")
    print(f"     design-motion R4 표는 비행 0.62 고정 전제라 +10.0%였지만, 실제로는 "
          f"{(lo/base-1)*100:+.1f}%~{(hi/base-1)*100:+.1f}%다.")
    print(f"  ※ 접근 시간은 구간이다: 허용오차를 다 쓰면 {A_MIN:.3f}초, 하나도 안 쓰면 {A_MAX:.3f}초.")
    print(f"     ArcheryDirector.cs 주석의 실기 4/4 잔차 -0.13유닛(배율 0.60)로 역산하면 "
          f"{(1.0*H_BASE*0.60-0.13)/(WALK*0.60):.3f}초 — 물리 틱(0.02초) 안에서 A_MIN 바로 위다.")
    print(f"     설계는 하한 {A_MIN:.3f}초를 쓴다(보수적: 사이클을 짧게 잡아 겹침 위험을 과대평가하는 쪽).")

    hdr("[E] g 스윕 — 바탕(span 39.98H) / 작은창(span 15.07H)")
    for g in (0.0,0.20,0.25,0.30,0.35,0.40,0.45,0.50):
        f_ = mk_prop_hi(g, UCAP_DEF)
        for lbl, sp in (("바탕",39.98*H),("작은창",15.07*H)):
            bh = f_(sp,H); bl = PROP_LO(bh,sp,H)
            print(f"  g={g:.2f} {lbl}: 밴드 {bl/H:5.2f}~{bh/H:6.2f}H "
                  f"({bl*PT_UNIT:6.1f}~{bh*PT_UNIT:6.1f}pt) 상한/span {bh/sp*100:5.1f}% "
                  f"방어선 {'안전' if bh <= 0.5*sp+0.875*H else '★위반'}", end="")
        print()

    hdr("[F] 왜 지금 신고가 나왔나 — 배율에 따른 '화면 대비 사거리'")
    print("  발판 = 바탕 전폭(사용자 화면). 상한 6.6H가 화면 폭에서 차지하는 비율")
    W_units = 40.92 + SPAN_INSET*(H_BASE*SCALE_USER)     # 사용자 화면 폭(유닛) — 배율과 무관
    for s in (0.35,0.45,0.60,0.75,1.00):
        Hs = H_BASE*s; sp = W_units - SPAN_INSET*Hs
        cur = min(Hs*MAX_R, sp); prop = mk_prop_hi(G_DEF,UCAP_DEF)(sp,Hs)
        print(f"  배율 {s:.2f}: H={Hs*PT_UNIT:5.1f}pt  현행상한 {cur*PT_UNIT:6.1f}pt = 화면의 {cur/sp*100:5.1f}%"
              f"   제안상한 {prop*PT_UNIT:6.1f}pt = {prop/sp*100:5.1f}%  ({prop/cur:.2f}배)")

    hdr("[J] 구현 후 실기 로그 예상 — 리더 검증용(같은 형식으로 대조할 것)")
    H2 = H; W2 = 40.92 + SPAN_INSET*H2
    PH = mk_prop_hi(G_DEF, UCAP_DEF); RMq = mk_remap(0.50)
    bl, bh = band(40.92, H2, PH, PROP_LO)
    print(f"  바탕(span 40.92유닛)에서 밴드는 항상 {bl:.2f}~{bh:.2f}유닛으로 찍혀야 한다"
          f" (현행은 3.72~6.76). 밴드 하한 근거=폭비례 {F_SPAN:.2f}x기준상한={bl/H2:.2f}H")
    random.seed(2026)
    outs=[]
    for _ in range(30):
        pp = resolve(random.uniform(0.35*H2, W2-0.35*H2), 0, W2, H2, random.random(), PH, PROP_LO, RMq)
        outs.append(pp['d'])
    print("  추첨 30회 예시(유닛):")
    for i in range(0,30,10): print("    " + " ".join(f"{d:5.2f}" for d in outs[i:i+10]))
    print(f"  그중 6.76유닛(현행 상한) 초과 = {sum(1 for d in outs if d>6.76)}/30  "
          f"(설계 기대 정확히 50% — 30표본 95%구간 9~21건)")
    print(f"  작은 창(span 15.43유닛)에서는 밴드가 3.72~6.76으로 **현행과 한 톨도 다르지 않아야** 한다: "
          f"{band(15.43,H2,PH,PROP_LO)[0]:.2f}~{band(15.43,H2,PH,PROP_LO)[1]:.2f}")

    hdr("[G] 활쏘기 통계 — 왜 33%에서 멈춰 있나")
    print("  현행: 한 사이클 3발이 항상 {Miss 1, Hit 1, Bullseye 1} 고정(ArcheryState.BuildScenario).")
    print("        CharacterStatsModel은 Bullseye만 분자로 세고 이름을 '명중'이라 붙인다.")
    print(f"        -> 명중률 = 1/3 = {1/3*100:.2f}% 상수. 51/153도 정확히 51사이클 x (1,3).")
    print("  사용자가 보는 것: 3발 중 2발이 과녁에 꽂힘 = 2/3 = 66.67%.")
    print("  제안 A(정의만 교정): 명중 = Bullseye+Hit -> 66.67% 상수(여전히 죽은 숫자, 그러나 화면과 일치)")
    print("  제안 B(A + 앞 2발 독립 베르누이 p_miss=0.5):")
    print("        사이클당 기대 빗나감 = 2*0.5 = 1.00발 = 현행 시나리오와 **평균이 정확히 동일**")
    for N in (5,10,20,51,100,300):
        sd = math.sqrt(2*0.25)/(3*math.sqrt(N))
        print(f"        {N:4d}사이클 후 표시값 표준편차 {sd*100:4.1f}%p (수렴값 66.7%)")
    print("        분포: 100% 확률 0.25 / 66.7% 확률 0.50 / 33.3% 확률 0.25")


def guarantees():
    hdr("[H] 불변식 검증 — 몬테카를로. ★ 절대치가 아니라 '현행 대비 악화 여부'를 본다")
    H = H_BASE*SCALE_USER
    PROP_HI = mk_prop_hi(G_DEF, UCAP_DEF); RM = mk_remap(Q_DEF)
    random.seed(31337)
    st = {k: dict(walk=0.0, floor=0, over=0, collapse=0, pin=0, n=0) for k in ("현행","제안")}
    abandon = {"현행":0, "제안":0}
    ident = identN = 0
    for _ in range(300000):
        W = random.uniform(2.0*H, 90.0*H)
        footX = random.uniform(0, W); roll = random.random()
        res = {"현행": resolve(footX,0,W,H,roll,CUR_HI,CUR_LO),
               "제안": resolve(footX,0,W,H,roll,PROP_HI,PROP_LO,RM)}
        for k,v in res.items():
            if v is None: abandon[k]+=1; continue
            d=st[k]; d['n']+=1
            d['walk']=max(d['walk'], abs(v['standX']-v['footX'])/H)
            if v['d'] < H*MIN_R-1e-6: d['floor']+=1
            if v['d'] > v['span']+1e-6: d['over']+=1
            if v['bandHi']-v['bandLo'] < 0.10*v['bandHi']-1e-6: d['collapse']+=1
            if min(v['targetHi']-v['targetX'], v['targetX']-v['targetLo']) < 0.01*H: d['pin']+=1
        a,b = res["현행"], res["제안"]
        if (a is None)!=(b is None): print("  ★ 포기 판정 불일치!"); break
        if a is not None and b['span'] <= H*(MAX_R/G_DEF):
            identN += 1
            if abs(a['d']-b['d'])<1e-6 and abs(a['standX']-b['standX'])<1e-6: ident += 1
    print(f"  발판 폭 2~90H 균등, footX 균등, roll 균등, 표본 300000")
    print(f"  포기 건수: 현행 {abandon['현행']} / 제안 {abandon['제안']} -> "
          f"{'OK(동일)' if abandon['현행']==abandon['제안'] else '★차이'}")
    print(f"  {'지표':<34}{'현행':>12}{'제안':>12}   판정")
    def cmp(name, key, fmt="{:.4f}", worse=lambda a,b: b>a+1e-6):
        a,b = st['현행'][key], st['제안'][key]
        print(f"  {name:<34}{fmt.format(a):>12}{fmt.format(b):>12}   "
              f"{'★악화' if worse(a,b) else 'OK(동일 또는 개선)'}")
    cmp("최대 이동거리(H, 접근 박자)", 'walk')
    cmp("절대하한 2.6H 미만 추첨(건)", 'floor', "{:d}")
    cmp("span 초과 추첨(건)", 'over', "{:d}")
    cmp("밴드 붕괴(폭<10%상한, 건)", 'collapse', "{:d}")
    cmp("과녁이 구간 끝에 못박힘(건)", 'pin', "{:d}")
    print(f"  span <= {MAX_R/G_DEF:.2f}H(교차점)에서 현행과 비트 동일: {ident}/{identN} "
          f"-> {'OK' if ident==identN else '★위반'}")
    same = tot = 0
    Z_HI = mk_prop_hi(0.0, UCAP_DEF); random.seed(4242)
    for _ in range(100000):
        W = random.uniform(2.0*H, 90.0*H); footX = random.uniform(0,W); roll = random.random()
        a = resolve(footX,0,W,H,roll,CUR_HI,CUR_LO); b = resolve(footX,0,W,H,roll,Z_HI,PROP_LO,mk_remap(Q_DEF))
        if (a is None)!=(b is None): tot+=1; continue
        if a is None: continue
        tot+=1
        if abs(a['d']-b['d'])<1e-7 and abs(a['targetX']-b['targetX'])<1e-7: same+=1
    print(f"  킬스위치 g=0 이 현행과 비트 동일: {same}/{tot} -> {'OK' if same==tot else '★위반'}")
    hdr("[I] 위 두 '★' 지표가 현행에도 이미 있는가 — 발생 구간 규명")
    random.seed(99)
    ex_walk = ex_col = None
    for _ in range(200000):
        W = random.uniform(2.0*H, 90.0*H); footX = random.uniform(0,W); roll = random.random()
        a = resolve(footX,0,W,H,roll,CUR_HI,CUR_LO)
        if a is None: continue
        w = abs(a['standX']-a['footX'])/H
        if ex_walk is None or w > ex_walk[0]: ex_walk = (w, a['span']/H, a['d']/H, a['footX']/H)
        cw = (a['bandHi']-a['bandLo'])/a['bandHi']
        if ex_col is None or cw < ex_col[0]: ex_col = (cw, a['span']/H, a['bandLo']/H, a['bandHi']/H)
    print(f"  현행 최악 이동거리 {ex_walk[0]:.2f}H : span {ex_walk[1]:.2f}H, 사거리 {ex_walk[2]:.2f}H, footX {ex_walk[3]:.2f}H")
    print(f"    -> 원인: bandHi(6.6H)가 '못박힘 방어선' 0.5*span+0.875H 를 넘는 구간(span < 11.45H)."
          f" 제안은 이 구간을 한 톨도 건드리지 않는다(교차점 18.86H).")
    print(f"  현행 최악 밴드폭 {ex_col[0]*100:.1f}% : span {ex_col[1]:.2f}H, 밴드 {ex_col[2]:.2f}~{ex_col[3]:.2f}H")
    print(f"    -> 원인: span이 절대하한 2.6H 바로 위인 좁은 발판. 같은 이유로 제안과 무관하다.")

analysis()
guarantees()
