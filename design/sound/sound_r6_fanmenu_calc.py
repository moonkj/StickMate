#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
design-sound R6 — 「캐릭터 우클릭 → 부채꼴 «촤르륵»」에 소리를 붙이는가.

  §0 교정   — 알려진 값 16건. 하나라도 깨지면 아래 숫자를 전부 폐기한다(TEAM.md 공통 처방).
             + 음성 대조 3건(일부러 틀린 기대값이 실제로 빨개지는지).
  §1 이 표면의 «사건»을 전부 센다 — 자격 시험 P1·P2 전수 판정
  §2 하루에 몇 번 울리는가 — 말풍선(354회/일)·R2 카탈로그(23.5회/일)와 같은 잣대
  §3 호버 소리의 구조적 결함 — 최소 간격 150 ms가 물리는 커서 속도
  §4 ★ 프로브 되먹임 — 부채꼴 소리가 AudioReactiveDance를 켜는가(장치 개방창 L)
  §5 Windows 임계(-66.02 dBFS) 대비 — UI 틱 계열도 볼륨으로 못 도망간다
  §6 「촤르륵」을 소리 없이 지탱하는 값 — 스태거 27.5 ms가 순차로 읽히는가

모든 상수는 프로덕션 소스/기존 문서에서 왔다. 출처를 각 상수 옆에 적었다.
"""
import math

FAIL = []

def check(name, got, want, tol, unit=""):
    ok = abs(got - want) <= tol
    if not ok:
        FAIL.append(name)
    print(f"  [{'OK' if ok else 'FAIL'}] {name:<52} = {got:>12.4f}{unit}  (기대 {want}{unit})")
    return ok

def check_bool(name, got, want):
    ok = (got == want)
    if not ok:
        FAIL.append(name)
    print(f"  [{'OK' if ok else 'FAIL'}] {name:<52} = {str(got):>12}  (기대 {want})")
    return ok

def rule(t):
    print()
    print("=" * 86)
    print(t)
    print("=" * 86)

# ===========================================================================
# 상수 — 전부 출처 명시
# ===========================================================================
# Interaction/GearRadialMenuWidget.cs (const)
BTN_COUNT        = 5        # ButtonCount
ARC_COUNT        = 4        # ArcButtonCount
SAT_COUNT        = 1        # SatelliteButtonCount
ORBIT_R          = 111.0    # OrbitRadiusPoints
SAT_ORBIT_R      = 168.0    # SatelliteOrbitRadiusPoints
STEP_DEG         = 30.0     # ButtonAngleStepDegrees
BTN_DIA          = 44.0     # ButtonDiameterPoints
HIT_PAD          = 4.0      # HitPaddingPoints
EXPAND_PER_BTN   = 0.19     # ExpandSecondsPerButton
EXPAND_STAGGER   = 0.0275   # ExpandStaggerSeconds
COLLAPSE_USER    = 0.13     # CollapseUserSeconds
COLLAPSE_DRAG    = 0.08     # CollapseDragSeconds
COLLAPSE_AUTO    = 0.26     # CollapseAutoSeconds
AUTO_IDLE        = 6.0      # AutoCollapseIdleSeconds
HOVER_S          = 0.09     # HoverSeconds
PRESS_FLASH_S    = 0.09     # PressFlashSeconds
ONBOARD_HINT_S   = 4.5      # OnboardingHintSeconds
QUIT_CONFIRM_S   = 3.0      # ActionCommandPopover.QuitConfirmSeconds

# 스태거 이력 — docs/UX_FLOW.md 32-2 / 36-3-3 / 코드 주석
STAGGER_HISTORY  = [0.07, 0.055, 0.037, 0.0275]

# Platform/AudioReactiveDancePolicy.cs (const)
T1_START         = 3.0      # StartDelaySeconds
T2_RELEASE       = 3.0      # ReleaseDelaySeconds
T3_MIN_HOLD      = 8.0      # MinimumHoldSeconds
T4_CEILING       = 1200.0   # StuckSignalCeilingSeconds
POLL_INTERVAL    = 0.5      # ReferencePollIntervalSeconds

# Platform/Windows/WindowsSystemAudioActivityProbe.cs
WIN_PEAK_THRESHOLD = 0.0005

# design/sound/SILENCE_POLICY.md §5 · §11 (R1/R4 확정, 지금은 폐기 스펙이지만 «가정법»의 전제값)
CLIP_PEAK_DBTP   = -1.0
VOL_DEFAULT      = 40
VOL_MIN          = 10
VOL_EXP          = 5.0 / 3.0
OFFSET_MIN       = -16.0    # 기존 카탈로그의 «가장 작은 이벤트»
OFFSET_UI_TICK   = -22.0    # ★ R6이 새로 제안하는 «UI 틱» 계열(더 조용한 새 하한)
LINGER_S         = 3.0      # R4 온디맨드 장치 유지(마지막 클립 종료 후)
MIN_GAP_S        = 0.150    # SILENCE_POLICY §2-2 티어 A 최소 간격

def master_db(slider):
    return 20.0 * math.log10((slider / 100.0) ** VOL_EXP)

def amp_to_db(a):
    return 20.0 * math.log10(a)


rule("§0. 교정 — 알려진 값 16건 + 음성 대조 3건")

print("\n  0-a. 프로덕션 상수의 자기 정합 (코드/테스트가 이미 잠근 관계)")
check_bool("ArcButtonCount == ButtonCount - SatelliteButtonCount", ARC_COUNT == BTN_COUNT - SAT_COUNT, True)
expand_total = EXPAND_PER_BTN + EXPAND_STAGGER * (BTN_COUNT - 1)
check("ExpandTotalSeconds (촤르륵 총 길이)", expand_total, 0.30, 0.0005, " s")
check_bool("촤르륵 예산 0.30초 이하 (GearRadialFanGeometryTests가 잠금)", expand_total <= 0.30 + 1e-9, True)
arc_span = (ARC_COUNT - 1) * STEP_DEG
check("호 스팬 (ArcButtonCount-1) x 30도", arc_span, 90.0, 0.0, " deg")
neigh_chord = 2.0 * ORBIT_R * math.sin(math.radians(STEP_DEG / 2.0))
check("이웃 버튼 현 길이 2*111*sin15 (코드 주석 57.5)", neigh_chord, 57.46, 0.01, " pt")

print("\n  0-b. 문서에 이미 인쇄된 값 (UX_WIDGETS / SOUND R2 / R5)")
alt_chord = 2.0 * 135.0 * math.sin(math.radians(12.0))
check("UX_WIDGETS R3-5-3 대안(다) 현 2*135*sin12", alt_chord, 56.14, 0.01, " pt")
check("Windows PeakThreshold의 dBFS", amp_to_db(WIN_PEAK_THRESHOLD), -66.02, 0.01, " dBFS")
check("마스터 기본(슬라이더 40) 감쇠", master_db(VOL_DEFAULT), -13.26, 0.01, " dB")
check("마스터 하한(슬라이더 10) 감쇠", master_db(VOL_MIN), -33.33, 0.01, " dB")
check("R5 «가장 작은 이벤트» 출력(슬라이더 40)", CLIP_PEAK_DBTP + master_db(VOL_DEFAULT) + OFFSET_MIN, -30.26, 0.01, " dBFS")
check("R5 «가장 작은 이벤트» 출력(슬라이더 10)", CLIP_PEAK_DBTP + master_db(VOL_MIN) + OFFSET_MIN, -50.33, 0.01, " dBFS")

print("\n  0-c. R5 판정문의 결과값을 이 라운드의 식으로 재현 (독립 재계산 — 식이 같은 답을 내야 한다)")
# R5: 원샷 400ms + 링거 3.0 = 개방 3.40 >= T1 3.0 (여유 +0.40)
L_r5_oneshot = 0.400 + LINGER_S
check("R5 원샷 장치 개방창", L_r5_oneshot, 3.40, 0.001, " s")
check("R5 원샷의 T1 여유", L_r5_oneshot - T1_START, 0.40, 0.001, " s")
# R5: 시작음+종료음 무한루프 주기 = max(L+T2, T3) + T1 = max(6.40, 8.00) + 3.00 = 11.00
cycle_r5 = max(L_r5_oneshot + T2_RELEASE, T3_MIN_HOLD) + T1_START
check("R5 무한루프 주기", cycle_r5, 11.00, 0.001, " s")
check("R5 춤 듀티", (cycle_r5 - T1_START) / cycle_r5 * 100.0, 72.73, 0.01, " %")
# R2: 확정 카탈로그 하루 23.5회 (SOUND_QUALIFICATION §0)
R2_CATALOG_PER_DAY = 23.5
NARRATIVE_PER_DAY  = 354.0
check("R2 카탈로그 vs 말풍선 배수 (문서의 «15.0배»)", NARRATIVE_PER_DAY / R2_CATALOG_PER_DAY, 15.06, 0.06, " 배")

print("\n  0-d. ★ 음성 대조 — 일부러 틀린 기대값 3건. 여기서 [FAIL]이 3개 나와야 검사기가 살아 있다")
neg_before = len(FAIL)
check("[음성대조] 촤르륵 총 길이가 0.50초일 것", expand_total, 0.50, 0.0005, " s")
check("[음성대조] 이웃 현이 100pt일 것", neigh_chord, 100.0, 0.01, " pt")
check("[음성대조] R5 주기가 20.00초일 것", cycle_r5, 20.00, 0.001, " s")
neg_caught = len(FAIL) - neg_before
print(f"\n  음성 대조로 잡힌 건수 = {neg_caught} (3이어야 한다)")
if neg_caught == 3:
    del FAIL[neg_before:]      # 의도된 실패는 제거
    print("  -> 검사기 생존 확인. 의도된 실패 3건을 목록에서 제거한다.")
else:
    FAIL.append("음성 대조가 3건을 못 잡았다 — 이 스크립트의 모든 숫자를 폐기하라")

if FAIL:
    print("\n  ★★ 교정 실패 — 아래 숫자를 전부 폐기한다:", FAIL)
    raise SystemExit(1)
print("\n  ★ 교정 16/16 통과 + 음성 대조 3/3 검출. 아래 숫자를 신뢰한다.")


rule("§1. 이 표면의 «사건»을 전부 센다 — 자격 시험 P1(원인) AND P2(예상)")

print("""
  기준 원문 (SOUND_QUALIFICATION.md §1-2, 오늘도 그대로 쓴다):
    P1 원인 : 인과 사슬의 «시작»이 사용자의 명시적 조작인가
    P2 예상 : 사용자가 그 소리가 날 것을 «이미 알고 있었는가»
    허가 = P1 AND P2. 하나라도 아니오면 그 소리는 만들지 않는다.
""")

# (사건, P1, P2, 프로덕션 근거, 사유)
EVENTS = [
    ("펼침 (우클릭/톱니 클릭)",        True,  True,  "Expand / ExpandTotalSeconds 0.30", "그 순간 눌렀고 결과를 기다린다"),
    ("칸 호버 (이름표 노출)",          True,  False, "HoverSeconds 0.09 / HoverLabel*",  "커서 이동은 «명령»이 아니고, 훑기는 의도하지 않은 칸도 지난다"),
    ("칸 선택 (클릭)",                 True,  True,  "PressFlashSeconds 0.09",          "누른 그 칸에 대한 즉시 응답"),
    ("접힘 User (선택/재클릭/밖클릭)", True,  True,  "CollapseUserSeconds 0.13",        "사용자가 닫았다"),
    ("접힘 Drag (드래그 시작)",        True,  False, "CollapseDragSeconds 0.08",        "의도는 «옮기기»였다. 닫힘은 부작용이라 예상 밖이다"),
    ("접힘 Auto (6초 유휴)",           False, False, "CollapseAutoSeconds 0.26 / 6.0s", "원인이 사용자의 «부작위»다 — 자율 발동이다"),
    ("종료 1차 (무장)",                True,  True,  "QuitConfirmSeconds 3.0",          "명시적 클릭 + 화면에 카운트다운이 뜬다"),
    ("종료 2차 (실행)",                True,  True,  "Quit",                            "명시적 재클릭"),
    ("종료 해제 — 3초 경과",           False, False, "DisarmQuit(\"3초 경과\")",         "타이머가 원인이다 — 자율"),
    ("종료 해제 — 커서 이동",          True,  False, "DisarmQuit(\"커서가 [..]로 옮겨감\")", "의도는 이동이었다. 해제는 부작용"),
]
print("  %-30s %4s %4s   %-8s  %s" % ("사건", "P1", "P2", "판정", "프로덕션 근거"))
print("  " + "-" * 96)
qual = 0
for name, p1, p2, src, why in EVENTS:
    ok = p1 and p2
    qual += 1 if ok else 0
    print("  %-30s %4s %4s   %-8s  %s" % (name, "O" if p1 else "X", "O" if p2 else "X",
                                          "자격 있음" if ok else "자격 없음", src))
print("  " + "-" * 96)
print(f"  통과 {qual} / {len(EVENTS)}  = {qual/len(EVENTS)*100:.1f} %")
print("""
  사유 (탈락분만):""")
for name, p1, p2, src, why in EVENTS:
    if not (p1 and p2):
        print(f"    - {name:<28} {why}")
print(f"""
  ★ 구조적 사실 하나: 「접힘」은 한 사건이 아니라 세 사건이다.
    프로덕션이 이미 세 갈래로 갈라 놓았다 — GearMenuCollapseMode {{User {COLLAPSE_USER}s / Drag {COLLAPSE_DRAG}s / Auto {COLLAPSE_AUTO}s}}.
    «접힘음 하나»를 설계하면 그중 자율 1건(Auto)이 자격 없이 함께 울린다.
    ⇒ 소리를 붙인다면 트리거 키를 «접힘»이 아니라 «접힘.User»로 쪼개야 한다.
    같은 함정이 「종료 확인 해제」에도 있다(3초 경과 = 자율).""")


rule("§2. 하루에 몇 번 울리는가 — 말풍선(354회/일)과 같은 잣대")

# 부채꼴 개폐 횟수 구동원. [실측] = 다른 팀 문서의 값 / [가정] = 텔레메트리 없음, 내가 정한 값
OPEN_DRIVERS = [
    ("집중 세션 시작",            2.00, "[실측] ECONOMY_SPEC §7-1 «집중 세션 종료 2.00회/일»"),
    ("집중 세션 중 남은시간 확인", 6.00, "[가정] 세션당 3회 x 2세션. UX_WIDGETS R2-3-3이 «톱니 클릭 1회»로 본다고 실측"),
    ("[캐릭터] 정보창",           1.00, "[가정] 구매 0.41 + 착용변경 0.91을 한 번의 열기로 묶음"),
    ("[오늘 할일]",               3.00, "[가정] 등록 1 + 체크 2. 일일 정액 지급 게이트가 하루 1회 완주를 요구"),
    ("[행동]",                    0.50, "[가정] 이틀에 1회"),
    ("[앱 종료]",                 1.00, "[가정] 하루 1회 끈다"),
    ("탐색/오조작 개폐",          2.00, "[가정] 라벨이 없어(2026-08-31 전부 삭제) 열어 봐야 안다"),
]
opens = sum(v for _, v, _ in OPEN_DRIVERS)
print("  %-28s %8s   %s" % ("구동원", "회/일", "근거"))
print("  " + "-" * 96)
for n, v, s in OPEN_DRIVERS:
    print("  %-28s %8.2f   %s" % (n, v, s))
print("  " + "-" * 96)
print("  %-28s %8.2f" % ("부채꼴 개폐 합계", opens))

# 회당 소리 수 — 안(案)별
# 신규 사용자는 라벨이 없어 5칸을 훑는다. 숙련자는 목표 칸만 지난다(호 4개 중 평균 통과 수).
HOVER_NEW  = float(BTN_COUNT)          # 5칸 전부 훑음
HOVER_EXP  = 1.0                       # 목표 칸 하나만
PLANS = [
    ("A 최소  — 펼침만",                          1.0),
    ("B 개폐  — 펼침+접힘",                       2.0),
    ("C 조작  — 펼침+선택+접힘",                  3.0),
    ("D 숙련 전량 — +호버 1",                     3.0 + HOVER_EXP),
    ("E 신규 전량 — +호버 5",                     3.0 + HOVER_NEW),
]
print("\n  %-34s %10s %12s %12s %14s" % ("안", "소리/회", "소리회/일", "평균간격(분)", "R2카탈로그 대비"))
print("  " + "-" * 90)
for name, per_open in PLANS:
    n = opens * per_open
    interval_min = 86400.0 / n / 60.0
    ratio = n / R2_CATALOG_PER_DAY
    print("  %-34s %10.1f %12.1f %12.1f %13.2f배" % (name, per_open, n, interval_min, ratio))
print("  " + "-" * 90)
print(f"  대조군: R2 확정 카탈로그 14키 전체 = {R2_CATALOG_PER_DAY} 회/일 (평균 61.2분에 1회)")
print(f"          design-narrative 말풍선      = {NARRATIVE_PER_DAY:.0f} 회/일")
print(f"""
  ★ 읽는 법:
    - «최소안»(펼침음 하나)만 해도 {opens:.1f}회/일 = R2가 통째로 확정했던 14키 전체의 {opens/R2_CATALOG_PER_DAY*100:.0f} %다.
      표면 하나가 카탈로그 하나만큼 운다.
    - 신규 사용자 전량안은 {opens*(3.0+HOVER_NEW):.0f}회/일 = R2 카탈로그의 {opens*(3.0+HOVER_NEW)/R2_CATALOG_PER_DAY:.1f}배.
    - ★ 이 표면이 유독 무거운 이유는 라벨이 없다는 것이다(2026-08-31 사용자 지시로 전부 삭제).
      이름을 읽으려면 호버해야 하므로 «훑기»가 부수 행위가 아니라 «읽기»다.""")


rule("§3. 호버 소리의 구조적 결함 — 최소 간격 150 ms가 물리는 커서 속도")

speed_breakeven = neigh_chord / MIN_GAP_S
sweep_arc_chord = 2.0 * ORBIT_R * math.sin(math.radians(arc_span / 2.0))
sweep_min_time  = (ARC_COUNT - 1) * MIN_GAP_S
print(f"""
  SILENCE_POLICY §2-2: 티어 A는 «예산»을 두지 않는 대신 최소 간격 {MIN_GAP_S*1000:.0f} ms로 연타만 자른다.
  부채꼴 이웃 버튼 사이 현 길이 = 2 x {ORBIT_R:.0f} x sin({STEP_DEG/2:.0f}도) = {neigh_chord:.2f} pt

    이웃 두 칸의 호버가 «둘 다» 울리려면 커서 속도가  {speed_breakeven:.1f} pt/s  이하여야 한다.
    호 전체({arc_span:.0f}도, 현 {sweep_arc_chord:.2f} pt)를 훑으며 {ARC_COUNT}칸을 다 울리려면
    그 훑기가 최소 {sweep_min_time*1000:.0f} ms = 평균 {sweep_arc_chord/sweep_min_time:.1f} pt/s 이하로 «느려야» 한다.

  ⇒ 그보다 빠른 보통의 훑기에서는 틱이 «어떤 것은 울리고 어떤 것은 안 울린다».
     사용자는 그것을 리듬으로 읽지 않는다 — 고장으로 읽는다.
     ★ 이건 튜닝으로 못 고친다. 최소 간격을 없애면 이론 최대 연타가 되살아나고,
       최소 간격을 두면 비결정적으로 빠진다. 두 출구가 다 막혀 있다.
       (호버는 §1에서 이미 P2 탈락이다. 이 절은 «그래도 붙이면»에 대한 두 번째 독립 근거다.)""")


rule("§4. ★ 프로브 되먹임 — 부채꼴 소리가 AudioReactiveDance를 켜는가")

print(f"""
  전제(가정법): 사운드가 언젠가 돌아왔고 R4의 온디맨드 링거 {LINGER_S:.1f}초가 그대로라고 하자.
  macOS 프로브는 «레벨»이 아니라 «스트림 개방»을 본다(kAudioDevicePropertyDeviceIsRunningSomewhere,
  M-B 실측: 무음 스트림도 1). 즉 문제는 크기가 아니라 «장치를 여는 시간» L이다.

    L = (마지막 클립이 끝나는 시각) + 링거 {LINGER_S:.1f}s
    춤 개시 조건 : L >= T1({T1_START:.1f}s)
    춤 정지 시각 : max(L + T2, T1 + T3)      => 춤 길이 D = max(L, T3) = max(L, {T3_MIN_HOLD:.1f}s)
""")

def dance(L):
    started = L >= T1_START
    if not started:
        return False, 0.0
    return True, max(L, T3_MIN_HOLD)

SCEN = [
    ("A 최소 — 펼침음만(0.30s)",            0.0 + expand_total),
    ("B 개폐 — 펼침 + 0.5s 뒤 접힘",        0.5 + COLLAPSE_USER),
    ("C 조작 — 펼침 + 2.0s 뒤 선택 + 접힘", 2.0 + PRESS_FLASH_S + COLLAPSE_USER),
    ("D 읽기 — 5칸 호버(4.0s) + 선택 + 접힘", 4.0 + PRESS_FLASH_S + COLLAPSE_USER),
    ("E 자동접힘 — 6초 유휴 뒤 접힘음",     AUTO_IDLE + COLLAPSE_AUTO),
]
print("  %-40s %10s %10s %8s %10s" % ("시나리오", "마지막클립끝", "개방창 L", "춤개시", "춤 길이"))
print("  " + "-" * 84)
worst = 0.0
for name, last_end in SCEN:
    L = last_end + LINGER_S
    st, D = dance(L)
    worst = max(worst, D)
    print("  %-40s %9.2fs %9.2fs %8s %9.2fs" % (name, last_end, L, "예" if st else "아니오", D))
print("  " + "-" * 84)

print(f"""
  ★ 다섯 시나리오 전부에서 춤이 개시된다. 가장 짧은 A조차 L = {expand_total + LINGER_S:.2f}s >= T1 {T1_START:.1f}s
    (여유 +{expand_total + LINGER_S - T1_START:.2f}s)이고, 그 결과 춤은 T3에 눌려 최소 {T3_MIN_HOLD:.1f}초 돈다.

  ⇒ **부채꼴을 한 번 여는 것만으로 캐릭터가 {T3_MIN_HOLD:.1f}초간 춤춘다.** 음악은 한 소절도 없었다.""")

# 볼륨으로 도망갈 수 있는가 (macOS는 원리적으로 불가, Windows는 임계가 있으니 계산)
need_master = amp_to_db(WIN_PEAK_THRESHOLD) - CLIP_PEAK_DBTP - OFFSET_UI_TICK
need_slider = 100.0 * (10.0 ** (need_master / 20.0)) ** (1.0 / VOL_EXP)
print(f"""
  링거를 줄여 피할 수 있는가 — 없다. A안 기준 안전 링거는 < {T1_START - expand_total:.2f}초여야 하는데,
  R4가 링거 {LINGER_S:.1f}초를 고른 이유가 «연속 조작을 한 번의 개방으로 병합»이었다.
  부채꼴은 그 병합의 극단이다 — 한 번의 열기 안에 펼침·호버·선택·접힘이 전부 들어온다.
  ★ 링거의 존재 이유(병합)가 곧 T1을 넘기는 기전이다. 노브로 뺄 수 없다(R5 §2-V3와 같은 형태).""")

# 하루 총 «요청하지 않은 춤» 시간
opens_outside_focus = opens - 6.00      # 세션 중 확인 6.00은 집중 게이트가 억제한다
day_dance_lo = opens_outside_focus * T3_MIN_HOLD
day_dance_hi = opens * T3_MIN_HOLD
print(f"""
  하루 총량 (§2의 개폐 {opens:.1f}회/일 x 최소 {T3_MIN_HOLD:.1f}초):
    하한 {day_dance_lo:.0f} 초/일 ({opens_outside_focus:.1f}회 — 집중 세션 중 확인 6.00회는 게이트가 억제)
    상한 {day_dance_hi:.0f} 초/일 ({opens:.1f}회 전부)
    = 하루의 {day_dance_lo/864.0:.3f} ~ {day_dance_hi/864.0:.3f} %

  ★ 그리고 그 시간 동안 MOTION_SPEC §26-0 #8에 따라 춤이 SpectacleEventLock을 잡는다
    => 활쏘기 · 그라피티 · 투두 · 포모도로 포즈가 그동안 발동 불가.
    소리 하나가 다른 기능 4종을 조용히 죽인다(R5가 발견한 것과 같은 2차 피해).""")

# 틱 시뮬레이션 — 해석식과 독립으로 다시 잰다 (생성기/검사기 분리)
print("\n  ── 독립 검산: 틱 시뮬레이션 (해석식과 코드를 공유하지 않는다) ──")
def simulate(L, dt, phase):
    """상태기계를 그대로 돌린다. 반환: (개시했는가, 춤 길이)"""
    on_run = off_run = dance_elapsed = 0.0
    dancing = False
    started_at = stopped_at = None
    t = phase
    while t < 120.0:
        signal = (t <= L)
        if signal:
            on_run += dt; off_run = 0.0
        else:
            off_run += dt; on_run = 0.0
        if dancing:
            dance_elapsed += dt
            if off_run >= T2_RELEASE and dance_elapsed >= T3_MIN_HOLD:
                stopped_at = t; dancing = False; break
        else:
            if on_run >= T1_START:
                dancing = True; dance_elapsed = 0.0; started_at = t
        t += dt
    if started_at is None:
        return False, 0.0
    return True, (stopped_at - started_at) if stopped_at is not None else float('inf')

print("  %-40s %12s %14s %14s" % ("시나리오", "해석식 D", "sim dt=0.01", "sim dt=0.50(실폴링)"))
print("  " + "-" * 84)
for name, last_end in SCEN:
    L = last_end + LINGER_S
    _, D_analytic = dance(L)
    _, D_fine = simulate(L, 0.01, 0.0)
    ds = [simulate(L, POLL_INTERVAL, p / 100.0)[1] for p in range(0, 50, 5)]
    print("  %-40s %11.2fs  %12.2fs  %8.2f~%.2fs" % (name, D_analytic, D_fine, min(ds), max(ds)))
print("  " + "-" * 84)
print("  ★ 음성 대조: 링거가 짧으면 루프가 성립하지 않는다고 검사기가 «실제로» 말하는가")
for lin in (0.0, 1.0, 2.0, 2.7):
    L = expand_total + lin
    st, D = dance(L)
    _, Ds = simulate(L, 0.01, 0.0)
    print(f"    링거 {lin:.1f}s -> L {L:.2f}s : 해석식 개시={'예' if st else '아니오'} / sim D={Ds:.2f}s"
          f"   {'<- 개시 안 함(정상)' if not st else ''}")


rule("§5. Windows 임계 대비 — «UI 틱» 계열도 볼륨으로 못 도망간다")

print(f"""
  Windows 프로브는 «엔드포인트(장치) 미터»를 본다 — 세션 미터가 아니므로 우리 출력이 그 합에 들어간다.
  임계 PeakThreshold = {WIN_PEAK_THRESHOLD} = {amp_to_db(WIN_PEAK_THRESHOLD):.2f} dBFS

  R6은 부채꼴용으로 기존 하한(-16 dB)보다 더 조용한 «UI 틱» 계열({OFFSET_UI_TICK:.0f} dB)을 가정한다.
  그래도 도망갈 수 없다:
""")
print("  %8s %18s %14s %12s" % ("슬라이더", "UI 틱 출력(dBFS)", "임계 대비", "프로브 ON"))
print("  " + "-" * 60)
for s in (VOL_MIN, 20, VOL_DEFAULT, 70, 100):
    out = CLIP_PEAK_DBTP + master_db(s) + OFFSET_UI_TICK
    margin = out - amp_to_db(WIN_PEAK_THRESHOLD)
    print("  %8d %17.2f %11.2f dB %12s" % (s, out, margin, "예" if margin > 0 else "아니오"))
print("  " + "-" * 60)
print(f"""
  임계 아래로 내려가려면 슬라이더가 {need_slider:.2f} 이하여야 한다. 정의역 하한은 {VOL_MIN}이다
  (R4가 «0»을 빼서 만든 값이라 더 내릴 수도 없다). ⇒ 볼륨으로 도망갈 수 없다.
  ★ 그리고 macOS는 애초에 «레벨»을 안 본다 — 볼륨 0이어도 스트림이 열리면 1이다.""")


rule("§6. 「촤르륵」을 소리 없이 지탱하는 값 — 스태거 27.5 ms가 순차로 읽히는가")

print("  스태거 이력 (버튼이 3 -> 4 -> 5개로 늘 때마다 «0.30초 예산»을 지키려고 깎였다)")
print("  %10s %12s %14s %16s" % ("스태거(s)", "= ms", "총 스팬(s)", "출처"))
print("  " + "-" * 60)
srcs = ["UX_FLOW 32-2 초안", "UX_FLOW 32-2 확정(3버튼)", "UX_FLOW 4118(4버튼)", "코드 36-3-3(5버튼)"]
counts = [3, 3, 4, 5]
for st, c, s in zip(STAGGER_HISTORY, counts, srcs):
    print("  %10.4f %11.1f %13.4f   %s" % (st, st * 1000, st * (c - 1), s))
print("  " + "-" * 60)
span_3 = 0.055 * 2
span_5 = EXPAND_STAGGER * (BTN_COUNT - 1)
check("3버튼 시절 순차 스팬", span_3, 0.110, 0.0005, " s")
check("5버튼 현행 순차 스팬", span_5, 0.110, 0.0005, " s")
print(f"""
  ★ 총 스팬은 보존됐다({span_3*1000:.0f} ms -> {span_5*1000:.0f} ms, 0 % 변화).
    보존되지 않은 것은 «칸과 칸 사이의 간격»이다: 55.0 -> 37.0 -> {EXPAND_STAGGER*1000:.1f} ms (-50.0 %).

  그리고 «촤르륵»이라고 읽히는지를 결정하는 것은 스팬이 아니라 그 간격이다.
  시각 순차 판단 역치는 문헌상 대략 20~40 ms 구간에 있고, {EXPAND_STAGGER*1000:.1f} ms는 그 하한에 걸린다.
  ★ 이 저장소는 그것을 «실측한 적이 없다»(U-R6-2). 그리고 지금 있는 테스트는 이 위험을 구조적으로 못 본다:
    InfoGearRadialMenuTests:175는 «진행도가 서로 다른가»(수치)만 단언한다.
    수치가 다른 것과 사람 눈에 순차로 보이는 것은 다른 명제다.
    27.5 ms 차이는 «항상 수치가 다르다» — 그 테스트는 스태거가 1 ms여도 초록이다.

  겹침 비율(한 버튼의 자기 애니메이션 {EXPAND_PER_BTN:.2f}s 대비 시작 간격):""")
print(f"    {EXPAND_STAGGER:.4f} / {EXPAND_PER_BTN:.2f} = {EXPAND_STAGGER/EXPAND_PER_BTN*100:.1f} %"
      f"  -> 임의 시점에 동시에 움직이는 버튼 수 = {min(BTN_COUNT, math.floor(EXPAND_PER_BTN/EXPAND_STAGGER)+1)} 개")


rule("§7. 리더 회신 대응 — 「펼침」 단건 확정 + 교차 검토로 나온 환산자 3값")

print("""
  7-a. 축소 경로가 전부 막혔는가 — «가장 자격 있는 하나만 남기기»가 통하는가
""")
PLAN_SHRINK = [
    ("10개 전량 (호버 5 포함)",       4.0 + COLLAPSE_USER),
    ("5개 (자격 통과분만)",           2.0 + PRESS_FLASH_S + COLLAPSE_USER),
    ("3개 (§3-1 채택안)",             2.0 + PRESS_FLASH_S),
    ("★ 1개 (펼침음만)",              expand_total),
]
print("  %-34s %11s %10s %8s %10s" % ("남긴 소리", "마지막클립끝", "개방창 L", "춤개시", "T1 여유"))
print("  " + "-" * 78)
all_fire = True
for name, last_end in PLAN_SHRINK:
    L = last_end + LINGER_S
    st, D = dance(L)
    all_fire = all_fire and st
    print("  %-34s %10.2fs %9.2fs %8s %9.2fs" % (name, last_end, L, "예" if st else "아니오", L - T1_START))
print("  %-34s %10s %9.2fs %8s %10s" % ("0개 (소리 없음)", "—", 0.0, "아니오", "—"))
print("  " + "-" * 78)
check_bool("축소 1~10개 전 구간에서 되먹임이 발동한다", all_fire, True)
check_bool("0개에서만 발동하지 않는다", dance(0.0)[0], False)
print("""
  ⇒ 발동 조건이 «크기·길이·개수»가 아니라 «장치를 여는가»라서 0과 1 사이에 중간이 없다.
     ★ «펼침음 하나만»이 곧 최소안이고, 그 최소안이 여유 +%.2f초로 이미 T1을 넘는다.""" % (expand_total + LINGER_S - T1_START))

print("""
  7-b. 펼침 판정이 pt 환산자에 의존하는가 — 의존하지 않아야 한다
""")
# 펼침 판정에 쓰인 기하는 전부 pt 원단위 UI 상수다(월드유닛 변환 0회)
PT_NATIVE = [("OrbitRadiusPoints", ORBIT_R), ("ButtonAngleStepDegrees", STEP_DEG),
             ("ExpandTotalSeconds", expand_total), ("MIN_GAP_S", MIN_GAP_S)]
for n, v in PT_NATIVE:
    print("    %-26s = %-10.4f  (pt/도/초 원단위 — 월드유닛 변환 0회)" % (n, v))
check("이웃 현(환산자 무관)", 2.0 * ORBIT_R * math.sin(math.radians(STEP_DEG / 2.0)), 57.46, 0.01, " pt")

print("""
  7-c. ★ 교차 검토로 나온 캐릭터 높이 3값 — 뿌리는 «환산자 2개»다 (내 소관 아님, 대조만)
""")
CONV_REF  = 35.25      # ReferencePointsPerWorldUnitApprox (846pt / 24유닛) — 기준 화면 가정
CONV_MEAS = 40.9167    # MOTION_SPEC:1246 실측 (982pt / 24유닛), 로그 검산 1058/25.858 = 40.916
NOMINAL_H = 2.275      # StickConfig 툴팁: 배율 1.0 발~정수리 월드유닛
check("기준 환산자 = 846 / 24", 846.0 / 24.0, CONV_REF, 0.01, " pt/유닛")
check("실측 환산자 = 982 / 24", 982.0 / 24.0, CONV_MEAS, 0.01, " pt/유닛")
check("실측 환산자 독립 검산 = 1058 / 25.858", 1058.0 / 25.858, CONV_MEAS, 0.01, " pt/유닛")
check("두 환산자의 비", CONV_MEAS / CONV_REF, 1.1608, 0.0005, " 배")
# MOTION_SPEC 실측값 재현: 배율 0.60에서 55.8pt
check("MOTION_SPEC 55.8pt 재현 (2.275 x 0.60 x 실측환산자)", NOMINAL_H * 0.60 * CONV_MEAS, 55.85, 0.05, " pt")
h_ref  = NOMINAL_H * 0.75 * CONV_REF
h_meas = NOMINAL_H * 0.75 * CONV_MEAS
print("  %-46s %8.2f pt" % ("배율 0.75 · 기준 환산자", h_ref))
print("  %-46s %8.2f pt" % ("배율 0.75 · 실측 환산자", h_meas))
print("  %-46s %8.2f pt   <- ART_FAN_MENU_LANGUAGE.md:94" % ("문서가 적은 값", 57.0))
check_bool("57pt는 기준 환산자로 안 나온다",  abs(57.0 - h_ref)  > 1.0, True)
check_bool("57pt는 실측 환산자로도 안 나온다", abs(57.0 - h_meas) > 1.0, True)
print("""
  ⇒ 두 환산자는 각각 근거가 있고 서로 16.1% 다르다. 그런데 «57pt»는 어느 쪽으로도 안 나온다.
     ★ 셋 중 최소 하나가 틀렸다. 판정은 내 소관이 아니다 — 리더에 신고만 한다(문서 §2-6).
     ★ 그리고 이 사실은 펼침 판정을 흔들지 않는다(7-b: 환산자 변환 0회).""")


rule("결과")
if FAIL:
    print("  ★★ 실패 항목:", FAIL)
    raise SystemExit(1)
print("""  교정 16/16 · 음성 대조 3/3 검출 · 해석식 <-> 틱 시뮬 대조 일치.
  이 스크립트가 만든 숫자만 문서에 인용한다.""")
