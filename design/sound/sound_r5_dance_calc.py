#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
design-sound R5 — 「음악 감지 → 댄스 반응」에 소리를 붙여야 하는가.

★ 이 스크립트가 하는 일은 판정이 아니라 **검산**이다.
   §0에서 저장소 실측값 14건으로 교정하고, 음성 대조 2건이 실제로 빨개지는지 먼저 보인 뒤에야
   §1~§4를 계산한다. 교정이 하나라도 깨지면 그 뒤 숫자를 전부 폐기한다(docs/TEAM.md §4 공통 처방).

★ TEAM.md 「생성기와 검사기가 같이 틀린다」 회피:
   §3의 자기 되먹임 주기를 **해석식**과 **틱 시뮬레이션** 두 방법으로 따로 구하고 서로 대조한다.
   한쪽만 맞으면 그 숫자를 못 쓴다.

출력: design/sound/sound_r5_dance_calc.out.txt
"""

import math
import sys

FAILURES = []


def check(label, got, want, tol=0.01, unit=""):
    ok = abs(got - want) <= tol
    if not ok:
        FAILURES.append(f"{label}: got={got!r} want={want!r} tol={tol}")
    mark = "OK " if ok else "★FAIL"
    print(f"  [{mark}] {label:<58} = {got:>12.4f}{unit}  (기대 {want}{unit}, 허용 ±{tol})")
    return ok


def check_bool(label, got, want):
    ok = (got == want)
    if not ok:
        FAILURES.append(f"{label}: got={got!r} want={want!r}")
    mark = "OK " if ok else "★FAIL"
    print(f"  [{mark}] {label:<58} = {got}  (기대 {want})")
    return ok


# ============================================================================
# 실측 앵커 — 전부 저장소 소스에서 읽었다. 여기 숫자를 지어내지 않는다.
# ============================================================================
# Platform/AudioReactiveDancePolicy.cs (const)
T1_START_DELAY      = 3.0     # StartDelaySeconds
T2_RELEASE_DELAY    = 3.0     # ReleaseDelaySeconds
T3_MIN_HOLD         = 8.0     # MinimumHoldSeconds
T4_STUCK_CEILING    = 1200.0  # StuckSignalCeilingSeconds
T_LOCKOUT_RELEASE   = 8.0     # LockoutReleaseSeconds (= T3)
MEASURED_TRACK_GAP  = 1.09    # MeasuredTrackGapSeconds        (game-architect M-D)
MEASURED_NOTIF_ON   = 2.38    # MeasuredNotificationOnSeconds  (game-architect M-D)
POLL_INTERVAL       = 0.5     # ReferencePollIntervalSeconds
MOTION_EP_MIN       = 8.0     # MotionEpisodeMinSeconds  (MOTION_SPEC §26)
MOTION_EP_MAX       = 16.0    # MotionEpisodeMaxSeconds

# Platform/Windows/WindowsSystemAudioActivityProbe.cs (const)
WIN_PEAK_THRESHOLD  = 0.0005  # PeakThreshold (선형 진폭)

# design/sound/SILENCE_POLICY.md §5 · §11 (design-sound R1/R4 확정)
CLIP_PEAK_DBTP      = -1.0    # 전 클립 정규화
VOL_DEFAULT         = 40      # 볼륨 슬라이더 기본
VOL_MIN             = 10      # 슬라이더 정의역 하한 (R4에서 0을 뺐다)
VOL_EXP             = 5.0 / 3.0
OFFSET_MAX          = 0.0     # 가장 큰 이벤트 오프셋
OFFSET_MIN          = -16.0   # 가장 작은 이벤트 오프셋
CLIP_ONESHOT_S      = 0.400   # 원샷 상한
CLIP_ACHIEVE_S      = 0.800   # 성취 상한
LINGER_S            = 3.0     # R4 온디맨드 장치 유지(마지막 클립 «종료» 후)

# MOTION_SPEC §26 (design-motion)
MOTION_REST_MIN     = 20.0
MOTION_REST_MAX     = 45.0


def master_db(slider):
    """슬라이더 → 마스터 감쇠 dB. SILENCE_POLICY §5 (s/100)^(5/3)."""
    return 20.0 * math.log10((slider / 100.0) ** VOL_EXP)


def db_to_amp(db):
    return 10.0 ** (db / 20.0)


def amp_to_db(amp):
    return 20.0 * math.log10(amp)


print("=" * 78)
print("§0. 교정 — 알려진 값 13건. 하나라도 깨지면 아래 숫자를 전부 폐기한다")
print("=" * 78)

# --- 0-a. 저장소 상수 자기 정합 ---
check("T3(최소유지)가 design-motion 에피소드 최소와 같다", T3_MIN_HOLD, MOTION_EP_MIN, 0.0, " s")
check("Lockout 해제가 T3를 재사용한다", T_LOCKOUT_RELEASE, T3_MIN_HOLD, 0.0, " s")
check_bool("T1 > 알림음 1회 ON 실측(2.38s) — 알림 1회를 거른다", T1_START_DELAY > MEASURED_NOTIF_ON, True)
check_bool("T2 > 곡 사이 공백 실측(1.09s) — 트랙 전환에 안 끊긴다", T2_RELEASE_DELAY > MEASURED_TRACK_GAP, True)
check("T2 / 곡간 공백 배수 (SOUND 문서의 «2.75배»)", T2_RELEASE_DELAY / MEASURED_TRACK_GAP, 2.75, 0.01, " 배")

# --- 0-b. 볼륨 곡선(SILENCE_POLICY §5 표를 그대로 재현) ---
check("마스터 기본(슬라이더 40) 감쇠", master_db(VOL_DEFAULT), -13.26, 0.01, " dB")
check("마스터 하한(슬라이더 10) 감쇠", master_db(VOL_MIN), -33.33, 0.01, " dB")
check("기본 마스터 진폭", db_to_amp(master_db(VOL_DEFAULT)), 0.2172, 0.0001, "")
loud_max_dbfs = CLIP_PEAK_DBTP + master_db(VOL_DEFAULT) + OFFSET_MAX
loud_min_dbfs = CLIP_PEAK_DBTP + master_db(VOL_DEFAULT) + OFFSET_MIN
check("가장 큰 이벤트 출력", loud_max_dbfs, -14.26, 0.01, " dBFS")
check("가장 작은 이벤트 출력", loud_min_dbfs, -30.26, 0.01, " dBFS")
# 슬라이더 절반 = 체감 절반 (라우드니스 배가 ≈ +10 dB)
half_db = 20.0 * math.log10(0.5 ** VOL_EXP)
check("슬라이더 절반의 체감 배율(2^(dB/10))", 2.0 ** (half_db / 10.0), 0.4988, 0.0005, " 배")

# --- 0-c. design-motion 듀티(§26-0 #8) ---
motion_duty = ((MOTION_EP_MIN + MOTION_EP_MAX) / 2.0) / (
    (MOTION_EP_MIN + MOTION_EP_MAX) / 2.0 + (MOTION_REST_MIN + MOTION_REST_MAX) / 2.0)
check("design-motion 의도 듀티 (8~16 춤 / 20~45 휴지)", motion_duty * 100.0, 26.97, 0.05, " %")

# --- 0-d. Windows 임계 ---
check("Windows PeakThreshold의 dBFS", amp_to_db(WIN_PEAK_THRESHOLD), -66.02, 0.01, " dBFS")

print()
print("=" * 78)
print("§0-N. 음성 대조 — 틀린 기대값이 실제로 빨개지는가 (이게 초록이면 §0 전체가 무의미하다)")
print("=" * 78)
neg = []
neg.append(abs(master_db(VOL_DEFAULT) - (-6.02)) <= 0.01)      # 일부러 틀린 마스터
neg.append(abs(amp_to_db(WIN_PEAK_THRESHOLD) - (-40.0)) <= 0.01)  # 일부러 틀린 임계
for i, wrong_passed in enumerate(neg, 1):
    mark = "★FAIL(탐지 경로 죽음)" if wrong_passed else "OK "
    print(f"  [{mark}] 음성 대조 {i}: 고의로 틀린 기대값이 통과하는가 = {wrong_passed} (기대 False)")
    if wrong_passed:
        FAILURES.append(f"음성 대조 {i}가 통과했다 — 검사기가 아무것도 안 재고 있다")

if FAILURES:
    print()
    print("★★ 교정이 깨졌다. 아래 계산을 전부 폐기한다.")
    for f in FAILURES:
        print("   -", f)
    sys.exit(1)

print()
print("=" * 78)
print("§1. Windows — 우리 소리가 우리 프로브를 켜는가")
print("=" * 78)
print("  프로브: IAudioMeterInformation::GetPeakValue — 장치(엔드포인트) 미터.")
print("  세션 단위가 아니라 **장치 단위**라 우리 출력도 그 합에 들어간다.")
print()
for slider in (VOL_MIN, 20, VOL_DEFAULT, 70, 100):
    out_dbfs = CLIP_PEAK_DBTP + master_db(slider) + OFFSET_MIN   # 가장 «작은» 이벤트
    amp = db_to_amp(out_dbfs)
    ratio = amp / WIN_PEAK_THRESHOLD
    margin = out_dbfs - amp_to_db(WIN_PEAK_THRESHOLD)
    trips = amp > WIN_PEAK_THRESHOLD
    print(f"  슬라이더 {slider:>3}  가장 작은 이벤트 {out_dbfs:>8.2f} dBFS "
          f"(진폭 {amp:.6f})  임계 대비 {ratio:>8.2f}배 / +{margin:>5.2f} dB  → 프로브 ON: {trips}")

# 프로브를 안 켜려면 슬라이더가 얼마여야 하는가
need_master_db = amp_to_db(WIN_PEAK_THRESHOLD) - CLIP_PEAK_DBTP - OFFSET_MIN
need_amp = db_to_amp(need_master_db)
need_slider = 100.0 * (need_amp ** (1.0 / VOL_EXP))
print()
print(f"  ★ 가장 작은 이벤트조차 프로브를 안 켜려면 슬라이더 ≤ {need_slider:.2f} 가 필요하다.")
print(f"     정의역 하한은 {VOL_MIN} 이다 → **볼륨 전 구간에서 우리 소리가 우리 프로브를 켠다.**")
print(f"     (하한 {VOL_MIN}은 R4가 «0을 빼서» 만든 값이라 더 내릴 수도 없다.)")

print()
print("=" * 78)
print("§2. macOS — 레벨이 아니라 「스트림 개방」이라 더 나쁘다")
print("=" * 78)
print("  프로브: kAudioDevicePropertyDeviceIsRunningSomewhere — 불리언, 임계 개념이 성립하지 않는다.")
print("  M-B 실측: **무음을 출력하는 스트림도 1로 잡힌다.** 즉 볼륨 0이어도 소용없다.")
print()
for name, clip in (("원샷 상한", CLIP_ONESHOT_S), ("성취 상한", CLIP_ACHIEVE_S)):
    open_window = clip + LINGER_S
    trips_t1 = open_window >= T1_START_DELAY
    print(f"  {name}({clip*1000:.0f} ms) + R4 링거 {LINGER_S:.1f}s = 장치 개방 {open_window:.2f}s"
          f"  vs T1 {T1_START_DELAY:.1f}s → 단독으로 춤 개시: {trips_t1}"
          f"  (여유 {open_window - T1_START_DELAY:+.2f}s)")

max_linger_safe = T1_START_DELAY - CLIP_ACHIEVE_S
print()
print(f"  ★ 단발 자기발동을 막으려면 링거 < {max_linger_safe:.2f}s 여야 한다(성취 클립 기준).")
print(f"     그런데 R4가 링거 {LINGER_S:.1f}s를 고른 근거가 «미리듣기를 1~2초 간격으로 비교하며 누르는")
print(f"     조작을 한 번의 개방으로 합치기»였다. 링거를 {max_linger_safe:.2f}s로 줄여도 그 병합은 남고,")
gap = 1.5   # R4가 근거로 든 «사람이 비교하며 누르는 간격» 중앙값
merged = gap + CLIP_ONESHOT_S + max_linger_safe
print(f"     간격 {gap:.1f}s로 두 번 누르면 개방 {merged:.2f}s ≥ T1 {T1_START_DELAY:.1f}s → 여전히 발동한다.")
print(f"     ⇒ **링거의 존재 이유(병합) 자체가 T1을 넘기는 기전이다.** 노브로 못 뺀다.")

print()
print("=" * 78)
print("§3. 자기 되먹임 루프 — 「시작음 + 종료음」을 붙이면 음악 없이 영원히 춤춘다")
print("=" * 78)


def analytic_loop(clip_s, linger_s, t1=T1_START_DELAY, t2=T2_RELEASE_DELAY, t3=T3_MIN_HOLD):
    """해석식. 우리 소리가 만드는 장치 개방창 L 하나로 전부 결정된다."""
    L = clip_s + linger_s
    if L < t1:
        return None          # 종료음이 T1을 못 채우면 재점화가 없다
    stop_at = max(L + t2, t3)      # Start로부터 Stop까지
    restart_at = stop_at + t1      # 종료음이 T1을 채우는 시각
    return {"L": L, "dance_s": stop_at, "period_s": restart_at,
            "duty": stop_at / restart_at}


def simulate_loop(clip_s, linger_s, dt, horizon=600.0):
    """
    틱 시뮬레이션 — 프로덕션 상태기계를 **파이썬으로 다시 쓴 것**이 아니라,
    Evaluate의 계약(§2절 상태표)만 보고 독립 구현했다. 해석식과 대조하는 것이 목적이다.
    """
    phase = "Armed"
    on_run = off_run = dance_elapsed = 0.0
    device_open_until = -1.0
    starts = []
    t = 0.0
    seeded = False
    while t < horizon:
        # 씨앗: 외부 음악 3.5초(한 번만). 그 뒤에는 외부 소리가 전혀 없다.
        external = (0.0 <= t < 3.5)
        if external:
            seeded = True
        ours = t < device_open_until
        playing = external or ours

        on_run = on_run + dt if playing else 0.0
        off_run = 0.0 if playing else off_run + dt
        if phase == "Dancing":
            dance_elapsed += dt

        action = None
        if phase == "Armed" and on_run >= T1_START_DELAY:
            phase, dance_elapsed, action = "Dancing", 0.0, "Start"
        elif phase == "Dancing":
            if on_run >= T4_STUCK_CEILING:
                phase, dance_elapsed, action = "Lockout", 0.0, "Stop"
            elif off_run >= T2_RELEASE_DELAY and dance_elapsed >= T3_MIN_HOLD:
                phase, dance_elapsed, action = "Armed", 0.0, "Stop"
        elif phase == "Lockout" and off_run >= T_LOCKOUT_RELEASE:
            phase, dance_elapsed = "Armed", 0.0

        if action in ("Start", "Stop"):
            device_open_until = t + clip_s + linger_s   # ← 우리가 소리를 낸다
            if action == "Start":
                starts.append(t)
        t += dt

    periods = [round(b - a, 4) for a, b in zip(starts, starts[1:])]
    return {"seeded": seeded, "start_count": len(starts),
            "periods": periods,
            "period_mode": max(set(periods), key=periods.count) if periods else None}


print("  가정: 시작음 400 ms + 종료음 400 ms, R4 링거 3.0s. 외부 음악은 «맨 처음 3.5초 한 번»뿐.")
print()
a = analytic_loop(CLIP_ONESHOT_S, LINGER_S)
print(f"  [해석식]  장치 개방창 L = {a['L']:.2f}s")
print(f"            Stop 시각  = max(L+T2, T3) = max({a['L']+T2_RELEASE_DELAY:.2f}, {T3_MIN_HOLD:.1f}) "
      f"= {a['dance_s']:.2f}s")
print(f"            재시작 주기 = Stop + T1 = {a['period_s']:.2f}s")
print(f"            듀티        = {a['duty']*100:.2f} %")

sim_fine = simulate_loop(CLIP_ONESHOT_S, LINGER_S, dt=0.01)
sim_poll = simulate_loop(CLIP_ONESHOT_S, LINGER_S, dt=POLL_INTERVAL)
print()
print(f"  [시뮬 dt=0.01]  600초 동안 Start {sim_fine['start_count']}회, 최빈 주기 {sim_fine['period_mode']}s")
print(f"  [시뮬 dt=0.50]  600초 동안 Start {sim_poll['start_count']}회, 최빈 주기 {sim_poll['period_mode']}s"
      f"   (실제 폴링 주기)")

check("해석식 ↔ 시뮬(dt=0.01) 주기 일치", sim_fine["period_mode"], a["period_s"], 0.02, " s")
check("해석식 ↔ 시뮬(dt=0.50) 주기 일치(1틱 이내)", sim_poll["period_mode"], a["period_s"], POLL_INTERVAL, " s")
check_bool("외부 음악이 3.5초뿐인데 600초 동안 계속 춤이 재점화된다", sim_fine["start_count"] >= 50, True)

print()
print(f"  ★ T4(고착 상한 {T4_STUCK_CEILING:.0f}s)가 이걸 못 잡는다 — 매 사이클 onRun이 0으로 리셋되기 때문이다.")
print(f"     T4는 «끊기지 않는 ON»만 본다. 이 루프는 스스로 끊었다가 다시 켠다.")
print()
print(f"  ★ 듀티 대조: 이 루프 {a['duty']*100:.2f}% vs design-motion 의도 {motion_duty*100:.2f}% "
      f"= {a['duty']/motion_duty:.2f}배")
print(f"     MOTION_SPEC §26-0 #8: 춤은 SpectacleEventLock을 잡는다 → 활쏘기·그라피티·투두·포모도로")
print(f"     포즈가 그 {a['duty']*100:.1f}% 동안 발동 불가가 된다.")

print()
print("  --- 음성 대조: 루프가 «어떤 링거에서도» 성립한다고 말하면 그건 틀린 검사기다 ---")
for linger in (3.0, 2.6, 2.0, 1.0, 0.0):
    r = analytic_loop(CLIP_ONESHOT_S, linger)
    if r is None:
        print(f"    링거 {linger:.1f}s → L={CLIP_ONESHOT_S+linger:.2f}s < T1 {T1_START_DELAY:.1f}s → 루프 없음 (기대대로)")
    else:
        print(f"    링거 {linger:.1f}s → L={r['L']:.2f}s ≥ T1 → 주기 {r['period_s']:.2f}s / 듀티 {r['duty']*100:.2f}%")
check_bool("링거 0s(즉시 닫음)에서는 루프가 성립하지 않는다 — 검사기가 조건을 실제로 본다",
           analytic_loop(CLIP_ONESHOT_S, 0.0) is None, True)

print()
print("=" * 78)
print("§4. 「시작음만 붙인다」로 도망갈 수 있는가")
print("=" * 78)
sim_start_only_len = CLIP_ONESHOT_S + LINGER_S
print(f"  종료음을 빼면 재점화 씨앗이 없어져 무한 루프는 사라진다. 그러나 잔여 왜곡이 남는다:")
print(f"   - 우리 시작음이 onRun을 {sim_start_only_len:.2f}s 늘린다 → 음악이 실제로 끝난 뒤에도")
print(f"     T2(연속 OFF {T2_RELEASE_DELAY:.1f}s) 계산이 최대 {sim_start_only_len:.2f}s 늦게 시작된다")
print(f"     ⇒ 춤이 음악보다 최대 {sim_start_only_len:.2f}s 더 길어진다(설계값 아님, 부작용).")
print(f"   - macOS [미리듣기] 버튼 1회 = 개방 {sim_start_only_len:.2f}s ≥ T1 → **설정창에서 미리듣기를**")
print(f"     **누르면 캐릭터가 춤추기 시작한다.** 사용자가 요청한 적 없는 인과다.")
print(f"   - 그리고 이 왜곡은 **관측이 불가능하다** — 프로브는 «누가» 냈는지 못 말한다(§1·§2).")

print()
print("=" * 78)
print(f"교정 {'전건 통과' if not FAILURES else '실패'} / 실패 {len(FAILURES)}건")
print("=" * 78)
sys.exit(1 if FAILURES else 0)
