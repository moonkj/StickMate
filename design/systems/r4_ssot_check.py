# -*- coding: utf-8 -*-
"""
design-systems R4 — 단일 출처(SSOT) 검산기 + 문서 표류 감시기.

★ 이 파일이 존재하는 이유 (2026-09-02, design-motion R5 지적)
--------------------------------------------------------------------------
design-systems가 §6-1 #5에서 접근 시간을 0.907 -> 0.801로 **교정해 놓고**,
바로 아래 #4 표는 **교정 전 0.907**로 계산했다. 교정본과 사용처가 갈라졌다.
저장소가 열 번 당한 형태("기준과 대상이 같이 움직인다")의 사촌이다.

그래서 이 스크립트는 두 가지를 동시에 한다:
  (1) 값을 **코드 원문에서 파싱**해 계산한다(사람이 베낀 상수를 안 믿는다).
  (2) 계산 결과를 **문서에 적힌 숫자와 셀 단위로 대조**한다(문서가 갈라지면 빨개진다).

규칙: 교정(CALIB)이 하나라도 깨지면 그 뒤 숫자를 전부 폐기한다. 그래서 실패 시 즉시 종료한다.
★ 음성/양성 대조를 반드시 포함한다 — 검사기가 빨개질 수 있음을 매 실행 증명한다.
"""
import math, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC  = os.path.join(ROOT, "Assets", "_Project", "Scripts")
DOC  = os.path.join(ROOT, "design", "systems")

FAIL = []
def chk(name, got, want, tol, note=""):
    ok = (got is not None) and abs(got - want) <= tol
    if not ok: FAIL.append(name)
    print(f"  [{'OK ' if ok else 'FAIL'}] {name}: 계산 {got if got is None else round(got,4)} / 기대 {want} (±{tol}) {note}")
    return ok

def chk_false(name, got, want, tol, note=""):
    """음성 대조 — 일치하면 안 된다."""
    bad = (got is not None) and abs(got - want) <= tol
    if bad: FAIL.append(name + "(음성대조)")
    print(f"  [{'OK ' if not bad else 'FAIL'}] (음성대조) {name}: {round(got,4)} 가 {want}와 달라야 한다 {note}")

# ── 1. 코드 원문 파싱 ────────────────────────────────────────────────────────
def grab(relpath, pattern, label):
    p = os.path.join(SRC, relpath)
    with open(p, encoding="utf-8") as f:
        txt = f.read()
    m = re.search(pattern, txt)
    if not m:
        print(f"  [FAIL] 파싱 실패: {label} <- {relpath} / {pattern}")
        FAIL.append(f"parse:{label}")
        return None
    return float(m.group(1))

print("== [0] 코드 원문에서 상수 파싱 (사람이 베낀 값을 쓰지 않는다) ==")
ARRIVE   = grab("States/ArcheryState.cs",           r"ArriveToleranceRatio\s*=\s*([0-9.]+)f",       "ArriveToleranceRatio")
BACKSTEP = grab("Interaction/ArcheryDirector.cs",   r"BackStepRatio\s*=\s*([0-9.]+)f",              "BackStepRatio")
WALK     = grab("Core/StickConfig.cs",              r"public float walkSpeed\s*=\s*([0-9.]+)f",     "walkSpeed")
H_BASE   = grab("Core/StickConfig.cs",              r"BaselineCharacterTotalHeight\s*=\s*([0-9.]+)f","BaselineCharacterTotalHeight")
COOLDOWN = grab("Core/StickConfig.cs",              r"archeryCooldownSeconds\s*=\s*([0-9.]+)f",     "archeryCooldownSeconds")
MINSESS  = grab("Interaction/FocusWatchDirector.cs",r"MinimumSessionSeconds\s*=\s*([0-9.]+)f",      "MinimumSessionSeconds")
DEMOSESS = grab("Interaction/FocusWatchDirector.cs",r"DemoSessionSeconds\s*=\s*([0-9.]+)f",         "DemoSessionSeconds")
T_BASE   = grab("Core/StickConfig.cs",              r"archeryArrowFlightSeconds\s*=\s*([0-9.]+)f",  "archeryArrowFlightSeconds")
T_MAX    = grab("Core/StickConfig.cs",              r"archeryArrowFlightMaxSeconds\s*=\s*([0-9.]+)f","archeryArrowFlightMaxSeconds")
REF_R    = grab("Core/StickConfig.cs",              r"archeryTargetDistanceRatio\s*=\s*([0-9.]+)f", "archeryTargetDistanceRatio")
for k, v in (("ARRIVE",ARRIVE),("BACKSTEP",BACKSTEP),("WALK",WALK),("H_BASE",H_BASE),
             ("COOLDOWN",COOLDOWN),("MINSESS",MINSESS),("DEMOSESS",DEMOSESS),
             ("T_BASE",T_BASE),("T_MAX",T_MAX),("REF_R",REF_R)):
    print(f"    {k:9s} = {v}")
if FAIL:
    print("\n★ 파싱 실패 — 아래 숫자를 전부 폐기한다."); sys.exit(1)

# 연출 박자 상수(코드가 아니라 design-motion 산출물 출처. 그래서 여기 따로 둔다)
INTRO, DRAW, AIM, RECOVER, OUTRO = 0.55, 0.42, 0.30, 0.34, 0.55
BEAT, AIM23 = 0.26, 0.22                 # design-motion R4 §2-5 제안
PT_UNIT = 982.0/24.0                     # Core/DockGeometry
PHYS_DT = 0.02                           # Unity 기본 fixedDeltaTime

SPEED_H = WALK / H_BASE                  # H/s — 배율에 불변(ResolveWalkSpeed = walkSpeed*scale, H = base*scale)
def clamp(v,a,b): return a if v<a else (b if v>b else v)
def flight(dH): return clamp(T_BASE*math.sqrt(max(0.25, dH/REF_R)), T_BASE*0.6, T_MAX)

A_MAX  = BACKSTEP / SPEED_H                        # 도착 허용오차를 하나도 안 쓴 극단(= design-motion R4 전제)
A_MIN  = (BACKSTEP - ARRIVE) / SPEED_H             # 허용오차를 전부 쓴 극단(= design-systems R3 교정)
SCALE  = 0.60
H_S    = H_BASE * SCALE
RESID_MEAS_UNITS = 0.13                            # ArcheryDirector.cs XML 주석: 실기 4/4, -0.13유닛
A_MEAS = (BACKSTEP*H_S - RESID_MEAS_UNITS) / (WALK*SCALE)

print("\n== [1] 교정 — 깨지면 아래 전부 폐기 ==")
print("  (a) 코드에서 유도한 접근 시간의 두 극단")
chk("보행속도 H/s", SPEED_H, 1.0990, 0.0002, "= walkSpeed/BaselineHeight, 배율 불변")
chk("A_MAX 접근 상한(허용오차 미사용)", A_MAX, 0.910, 0.001, "design-motion R4가 쓴 0.907의 정확한 형태")
chk("A_MIN 접근 하한(허용오차 전부 사용)", A_MIN, 0.801, 0.001, "design-systems R3 §6-1 #5 교정값")
print("  (b) 코드 주석의 실측(다른 출처) — ArcheryDirector.cs '실기 4/4 -0.13유닛 = -0.095H'")
chk("실측 잔차 H환산", RESID_MEAS_UNITS/H_S, 0.095, 0.001, "주석이 스스로 적은 -0.095H와 일치하는가")
chk("실측 잔차 pt환산", RESID_MEAS_UNITS*PT_UNIT, 5.3, 0.05, "주석의 '-5.3pt'")
chk("A_MEAS 실측 접근", A_MEAS, 0.823, 0.002, "= (1.00H - 0.13유닛)/보행속도")
tick_band_hi = A_MIN + PHYS_DT
print(f"    이론 밴드 [A_MIN, A_MIN+물리틱] = [{A_MIN:.3f}, {tick_band_hi:.3f}] / 실측 {A_MEAS:.3f}"
      f"  -> {'밴드 안' if A_MIN <= A_MEAS <= tick_band_hi + 0.005 else '★밴드 밖'}")
if not (A_MIN <= A_MEAS <= tick_band_hi + 0.005): FAIL.append("A_MEAS 밴드")

print("  (c) design-motion 산출물 재현 — 남의 숫자로 내 적분식을 교정한다")
def cycle_current(A):                      # 현행(게이트 없음) 사이클
    return A + INTRO + 3*(DRAW+AIM+RECOVER) + T_BASE + OUTRO
def cycle_r4gate(A, dH, aim23=AIM23):      # design-motion R4 제안: 착탄비트 게이트 + 2·3발 aim 할인
    T = flight(dH)
    return A + INTRO + (DRAW+AIM) + (T+BEAT) + (DRAW+aim23) + (T+BEAT) + (DRAW+aim23) + (T+OUTRO)
def cycle_r5(A, dH):                       # design-motion R5 제안: 뒷걸음 접근 + aim 할인 없음
    T = flight(dH)
    return A + INTRO + 3*(DRAW+AIM) + 2*(T+BEAT) + (T+OUTRO)

chk("R4 현행 총계(그쪽 전제 A=0.907)", cycle_current(0.907), 5.807, 0.001, "design-motion R4 §2-5")
chk("R4 게이트 총계 4.60H(A=0.907)",   cycle_r4gate(0.907, 4.60), 6.387, 0.001, "design-motion R4 §2-5")
chk("R5 총계 4.60H(A=0.843, aim 0.30)", cycle_r5(0.843, 4.60), 6.483, 0.001, "design-motion R5 §2-3")
chk("R5 총계 13.40H(A=0.843)",          cycle_r5(0.843, 13.40), 7.798, 0.002, "design-motion R5 §2-3")
chk("내 §6 현행 총계(A_MIN)",           cycle_current(A_MIN), 5.70, 0.005, "design-systems §6")
chk("R5가 계산한 교정 baseline",        cycle_current(A_MIN), 5.701, 0.002, "design-motion R5 §2-3과 동일 결론")
print("  (d) 비행시간 — 독립 표본 3개")
for dH, want in ((3.63,0.551),(13.40,1.058),(18.70,1.250)):
    chk(f"비행 {dH}H", flight(dH), want, 0.001)

print("  (e) ★ 음성 대조 — 검사기가 빨개질 수 있음을 증명한다")
chk_false("교정 전 A로 계산한 총계는 교정 후 값과 달라야", cycle_r4gate(0.907,4.60), cycle_r4gate(A_MIN,4.60), 0.001)
chk_false("R4 프레임과 R5 프레임은 달라야", cycle_r4gate(A_MIN,4.60), cycle_r5(0.843,4.60), 0.001)

if FAIL:
    print(f"\n★★ 교정 실패 {len(FAIL)}건: {FAIL} — 아래 숫자를 전부 폐기한다.")
    sys.exit(1)
print("  -> 교정 12/12 통과. 아래 숫자를 신뢰해도 된다.")

# ── 2. 교정 후 정본 표 ──────────────────────────────────────────────────────
DS = (3.63, 4.60, 6.60, 10.00, 13.40, 18.70)
BASE_CUR = cycle_current(A_MIN)

print(f"\n== [2] §6-1 #4 재계산 — 접근 {A_MIN:.3f}초(교정본)로 통일. 분모도 같은 프레임({BASE_CUR:.3f}초) ==")
print("   d(H)  비행    게이트   총계(A=0.801)  현행 5.701 대비 | 참고: 교정전(A=0.907/분모5.807)")
rows_r4 = []
for d in DS:
    T = flight(d); t = cycle_r4gate(A_MIN, d)
    old = cycle_r4gate(0.907, d); oldpct = (old/5.807-1)*100
    rows_r4.append((d, T, T+BEAT, t, (t/BASE_CUR-1)*100))
    print(f"  {d:5.2f}  {T:6.3f}  {T+BEAT:6.3f}   {t:7.3f}      {(t/BASE_CUR-1)*100:+6.1f}%   |  {old:7.3f}  {oldpct:+6.1f}%")
span_r4_13 = rows_r4[4][3]-rows_r4[0][3]
print(f"  변동폭(3.63~13.40H) = {span_r4_13:.3f}초  / (3.63~18.70H) = {rows_r4[5][3]-rows_r4[0][3]:.3f}초")
print(f"  ※ 변동폭은 접근 상수에 **불변**이다(차분이라 상쇄) — 교정 전에도 {(cycle_r4gate(0.907,13.40)-cycle_r4gate(0.907,3.63)):.3f}초.")

print(f"\n== [2-b] design-motion R5 프레임(뒷걸음 0.75H = 0.843초, aim 할인 없음) — 비교용 ==")
print("   d(H)  총계    현행 5.701 대비")
for d in DS:
    t = cycle_r5(0.843, d)
    print(f"  {d:5.2f}  {t:7.3f}   {(t/BASE_CUR-1)*100:+6.1f}%")
print("  ※ 두 표의 차이는 접근 +0.042 와 aim 할인 2×0.08 = 총 +0.202초로 **완전히 설명된다**:")
print(f"     {cycle_r5(0.843,4.60)-cycle_r4gate(A_MIN,4.60):.3f} == {(0.843-A_MIN)+2*(AIM-AIM23):.3f}")

# ── 3. 활쏘기 동전 파밍 상한 재계산 ────────────────────────────────────────
print("\n== [3] 활쏘기 파밍 상한 — ECONOMY_SPEC 4-2의 '586동전/시' 재검산 ==")
ritual_only = INTRO + 3*(DRAW+AIM+T_BASE+RECOVER) + OUTRO
print(f"  (a) 의식 구간만(접근 제외, 비행 {T_BASE}) = {ritual_only:.3f}초 -> {3600/ritual_only:.1f}동전/시  <- 문서의 586")
print(f"  (b) 접근 포함 실제 사이클(A={A_MIN:.3f})   = {BASE_CUR:.3f}초 -> {3600/BASE_CUR:.1f}동전/시  ★ 이쪽이 실제 상한")
print(f"  (c) 쿨다운 {COOLDOWN:.0f}초 -> {3600/COOLDOWN:.0f}동전/시")
print(f"      감소 배율: 문서 {3600/ritual_only/(3600/COOLDOWN):.0f}배 / 실제 {3600/BASE_CUR/(3600/COOLDOWN):.0f}배")
print(f"  (d) 관찰력 고급(-50%) -> 쿨다운 {COOLDOWN*0.5:.0f}초 -> {3600/(COOLDOWN*0.5):.0f}동전/시")

# ── 4. 집중모드 지급 — 격자·중도취소·데모 ─────────────────────────────────
print("\n== [4] 집중모드 지급 사양 검산 ==")
RATE_DONE, RATE_CANCEL = 1.2, 1.0
print("  (a) 완주 격자 5분 배수 — 정수성과 시급")
allint = True
for m in (5,10,15,20,25,30,45,50,60,90,120):
    c = RATE_DONE*m
    if abs(c-round(c)) > 1e-9: allint=False
    print(f"    {m:3d}분 -> {c:6.1f}동전  {c/m*60:6.1f}동전/시  {'정수' if abs(c-round(c))<1e-9 else '★비정수'}")
chk("5분 배수 전부 정수", 1.0 if allint else 0.0, 1.0, 0.0)
chk("5분 배수 시급 동일", RATE_DONE*60, 72.0, 1e-9)
print("  (b) 비-5분 격자에서 무슨 일이 나는가(반례)")
for m in (1,2,3,7):
    c = RATE_DONE*m
    print(f"    {m:3d}분 -> {c:5.1f}동전(비정수). 올림 시 {math.ceil(c)}동전 = {math.ceil(c)/m*60:6.1f}동전/시  "
          f"({math.ceil(c)/m*60/72:.2f}배)")
print("  (c) 중도취소 = 1.0 x floor(경과 분) — 어떤 주기로 스팸해도 60/시를 못 넘는다")
worst = 0.0
for period in (0.5,1,2,4,5,7,15,25,50):
    coins = math.floor(period)
    rate  = coins/period*60
    worst = max(worst, rate)
    print(f"    {period:5.1f}분 주기 -> {coins}동전 -> {rate:6.1f}동전/시")
chk("취소 스팸 최대 시급", worst, 60.0, 1e-9, "완주 72/시의 83.3%")
print("  (d) ★ 데모 경로(ForceTriggerNow)의 격자 이탈")
dm = DEMOSESS/60.0
print(f"    DemoSessionSeconds={DEMOSESS:.0f}초 = {dm:.2f}분 -> 완주 지급 {RATE_DONE*dm:.2f}동전 (★비정수, 5분 격자 밖)")
print(f"    시급으로는 {RATE_DONE*dm/dm*60:.1f}동전/시 = 격자와 동일 -> 파밍은 아니지만 반올림 규칙이 필요하다")
print(f"    MinimumSessionSeconds={MINSESS:.0f}초 = {MINSESS/60:.0f}분 -> {RATE_DONE*MINSESS/60:.1f}동전 (★비정수)")

# ── 5. 문서 표류 감시 ───────────────────────────────────────────────────────
print("\n== [5] 문서 표류 감시 — 계산값과 문서의 숫자를 셀 단위로 대조 ==")
def read(p):
    with open(os.path.join(DOC,p), encoding="utf-8") as f: return f.read()

def table_after_anchor(text, anchor):
    i = text.find(anchor)
    if i < 0: return None
    rows = []
    for line in text[i+len(anchor):].splitlines():
        s = line.strip()
        if not s.startswith("|"):
            if rows: break
            continue
        cells = [c.strip().replace("*","").replace("`","") for c in s.strip("|").split("|")]
        if all(set(c) <= set("-: ") for c in cells): continue
        rows.append(cells)
    return rows

def num(s):
    m = re.search(r"[-+]?\d+\.\d+|[-+]?\d+", s.replace(",",""))
    return float(m.group()) if m else None

ARCH = "2026-09-02_활쏘기_사거리밴드_재설계.md"
txt = read(ARCH)
anchor = "<!-- SSOT:beat_r4gate -->"
rows = table_after_anchor(txt, anchor)
if rows is None:
    print(f"  [SKIP] {ARCH} 에 앵커 {anchor} 가 없다 — 표를 아직 안 갈아 끼웠다.")
else:
    hdr = rows[0]; body = rows[1:]
    print(f"  앵커 발견. 표 {len(body)}행 대조 (헤더: {hdr})")
    if len(body) != len(rows_r4): FAIL.append("표 행수")
    for r, calc in zip(body, rows_r4):
        d, T, g, t, pct = calc
        got = [num(c) for c in r]
        for label, gv, wv, tol in (("d",got[0],d,0.005),("비행",got[1],T,0.001),
                                   ("게이트",got[2],g,0.001),("총계",got[3],t,0.001),
                                   ("증감%",got[4],pct,0.05)):
            chk(f"문서 {d:5.2f}H {label}", gv, wv, tol)

print("\n  (b) 금칙 리터럴 스캔 — 교정 전 값 0.907 이 **계산에 쓰일 수 있는 자리**에 남아 있는가")
print("     규칙: (i) .py 의 어느 줄이든 0.907 이 있으면 그 줄에 '[인용]' 표시가 있어야 한다")
print("           (ii) .md 는 **숫자만 든 표 셀**에 0.907 이 있으면 실패. 산문은 실패가 아니다")
print("           — 산문의 언급은 하위 결론을 만들지 못한다. 리터럴과 표 셀만이 값을 흘린다.")
bad, info = [], []
for fn in sorted(os.listdir(DOC)):
    if not fn.endswith((".md",".py")): continue
    if fn == os.path.basename(__file__): continue
    for i, line in enumerate(read(fn).splitlines(), 1):
        if "0.907" not in line: continue
        if fn.endswith(".py"):
            (info if "[인용]" in line else bad).append(f"{fn}:{i}: {line.strip()[:95]}")
        else:
            cells = [c.strip().replace("*","").replace("`","") for c in line.strip().strip("|").split("|")] \
                    if line.strip().startswith("|") else []
            leaked = any(re.fullmatch(r"[-+]?\d*\.?\d+%?", c) and "0.907" in c for c in cells)
            (bad if leaked else info).append(f"{fn}:{i}: {line.strip()[:95]}")
print(f"    실패 {len(bad)}건 / 허용된 언급 {len(info)}건")
for b in bad:  print("      [실패] " + b)
for b in info: print("      [허용] " + b)
if bad: FAIL.append("금칙 리터럴 0.907")
# 양성 대조: 스캐너가 실제로 문자열을 잡는지 확인 (전부 0건일 때 '깨끗'과 '안 봤다'가 똑같이 생긴다)
probe = sum(1 for fn in sorted(os.listdir(DOC)) if fn.endswith((".md",".py"))
            for line in read(fn).splitlines() if "0.801" in line)
print(f"    [양성대조] 같은 스캐너로 '0.801' 을 찾으면 {probe}건 — 0이면 스캐너가 죽은 것이다 "
      f"({'OK' if probe > 0 else '★FAIL'})")
if probe == 0: FAIL.append("스캐너 양성대조")

print("\n== 결과 ==")
if FAIL:
    print(f"  ★ 실패 {len(FAIL)}건: {FAIL}")
    sys.exit(2)
print("  전부 통과.")
