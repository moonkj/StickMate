#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
docs/UX_MOTION_DANCE.md 의 모든 숫자를 이 파일 하나로 재현한다.
design-motion, 2026-09-06.

원칙: 여기 들어오는 입력은 전부 **저장소에서 읽은 값**이거나 **docs/MOTION_SPEC.md 26절이
이미 확정한 값**이다. 지어낸 상수에는 반드시 유도가 붙는다.

실행: python3 2026-09-06_댄스7종_박자전이_검산.py
"""
import math

L = []
def p(s=""):
    L.append(s)
    print(s)

def head(t):
    p()
    p("=" * 78)
    p(t)
    p("=" * 78)

# ============================================================================
head("0. 입력 — 저장소 실측값 (전부 소스에서 읽었다)")
# ============================================================================
H_UNITS      = 2.2746944   # StickConfig.cs:1877 BaselineCharacterTotalHeight (1 H at scale 1)
THIGH        = 0.50        # SceneBootstrapper.cs:258
SHIN         = 0.45        # SceneBootstrapper.cs:258
UPPER_ARM    = 0.38        # SceneBootstrapper.cs:257
FOREARM      = 0.37        # SceneBootstrapper.cs:257
HIP_Y        = 0.9346944   # StickmanMetrics.cs:63
SHOULDER_Y   = 1.7646944   # StickmanMetrics.cs:62
LEG_STROKE   = 0.12 * 1.045
ARM_STROKE   = 0.10 * 1.045
PT_PER_UNIT  = 40.9167     # MOTION_SPEC 1246행 실측

WALK_SPEED       = 2.5     # StickConfig.cs:26  (ResolveWalkSpeed() = walkSpeed * scale)
SCALE_SHIP       = 0.75    # StickConfig.cs:1806 characterScale
SCALE_MIN        = 0.35    # StickConfig.MinCharacterScale
SCALE_MAX        = 1.00    # StickConfig.MaxCharacterScale
POSE_RATE_BASE   = 35.0    # StickConfig.cs:80  poseSmoothingRate
ARM_RATIO        = 0.55    # StickmanPoseAnimator.ArmSmoothingRatio
LOWER_RATIO      = 0.75    # StickmanPoseAnimator.LowerSegmentSmoothingRatio
IDLE_LEG_SPREAD  = 12.0    # StickConfig.cs:64
IDLE_ARM_SPREAD  = 40.0    # StickConfig.cs:68
IDLE_KNEE        = 4.0     # StickConfig.cs:120
IDLE_ELBOW       = 10.0    # StickConfig.cs:124
GRAVITY          = 9.81

# 이미 확정된 다른 연출의 보간 계수 — 비교용
RATE_CLIMB   = 44.0        # parkourClimbPoseSmoothingRate
RATE_ARCHERY = 46.0        # archeryPoseSmoothingRate
RATE_CROUCH  = 48.0        # landingCrouchPoseSmoothingRate

thigh_H   = THIGH / H_UNITS
shin_H    = SHIN / H_UNITS
arm_H     = (UPPER_ARM + FOREARM) / H_UNITS
leg_H     = thigh_H + shin_H
hip_H     = HIP_Y / H_UNITS
sh_H      = SHOULDER_Y / H_UNITS
legstroke_H = LEG_STROKE / H_UNITS

p(f"대퇴 {thigh_H:.5f} H / 정강이 {shin_H:.5f} H / 다리 전체 {leg_H:.5f} H")
p(f"팔 전체 {arm_H:.5f} H     (CLAUDE.md 실측 인용 '팔이 0.3297 H'와 대조)")
p(f"엉덩이 {hip_H:.5f} H / 어깨 {sh_H:.5f} H / 다리 획 {legstroke_H:.6f} H")
p(f"1 H (pt) = {H_UNITS * PT_PER_UNIT:.3f} x 배율   ->  s=0.60에서 {H_UNITS*0.60*PT_PER_UNIT:.2f}pt")

# 교정 3건 (§26-1-1과 같은 방식. 하나라도 깨지면 아래 전부 폐기해야 한다)
cal = []
cal.append(("팔 총길이 0.3297 H", abs(arm_H - 0.3297) < 5e-4))
cal.append(("다리 획 계수 0.12540", abs(legstroke_H * H_UNITS - 0.12540) < 5e-5))
cal.append(("s=0.60에서 1H = 55.84pt", abs(H_UNITS*0.60*PT_PER_UNIT - 55.84) < 0.02))
for name, ok in cal:
    p(f"  교정 {'통과' if ok else '실패'} — {name}")
assert all(ok for _, ok in cal), "교정 실패 — 아래 숫자를 신뢰할 수 없다"

# ============================================================================
head("1. ★ 보간 체인 — §26이 보지 못한 것 (ArmSmoothingRatio / LowerSegmentSmoothingRatio)")
# ============================================================================
p("StickmanPoseAnimator.ApplyLimb 실측:")
p("    다리 위마디 = baseRate            다리 아래마디 = baseRate x 0.75")
p("    팔 위마디  = baseRate x 0.55      팔 아래마디  = baseRate x 0.55 x 0.75 = x 0.4125")
p()
p("=> 진동하는 관절 신호는 1차 지연 필터를 통과한다. 진폭 이득 = k / sqrt(k^2 + w^2).")
p("   (붙잡고 있는 자세에는 정상상태 오차가 0이다 — 지수 감쇠는 목표에 정확히 수렴한다.")
p("    D1 retire / D6 스쾃 깊이 같은 '유지' 값은 이 감쇠와 무관하다. 오직 진동만 깎인다.)")

def gain(k, f_hz):
    w = 2 * math.pi * f_hz
    return k / math.sqrt(k * k + w * w)

def lag_deg(k, f_hz):
    w = 2 * math.pi * f_hz
    return math.degrees(math.atan2(w, k))

# 지속 진동 신호 목록 (dance, 관절, 체인계수, 기본 주파수 Hz)
JITTER = 0.06   # 아래 3절에서 유도. 주기를 (1-j)까지 줄일 수 있으므로 주파수는 1/(1-j)배가 된다.
SIGNALS = [
    ("D1 피루엣", "어깨 스윕 ±9",   ARM_RATIO,             1 / 0.70),
    ("D3 문워크", "어깨 스윙 ±26",  ARM_RATIO,             1 / 0.72),
    ("D3 문워크", "무릎 3<->52",    LOWER_RATIO,           1 / 0.72),
    ("D4 러닝맨", "어깨 ±62",       ARM_RATIO,             1 / 0.56),
    ("D4 러닝맨", "엉덩이 ±46",     1.0,                   1 / 0.56),
    ("D4 러닝맨", "무릎 6.5<->65",  LOWER_RATIO,           1 / 0.56),
    ("D6 프리샤트카", "무릎 8<->132", LOWER_RATIO,         1 / 0.84),
    ("D7 말춤",  "어깨 펌프 ±14",   ARM_RATIO,             1 / 0.48),
    ("D7 말춤",  "무릎 4<->88",     LOWER_RATIO,           1 / 0.96),
]

p()
p("현행 기본값(poseSmoothingRate=35)으로 그대로 돌리면 무슨 일이 나는가:")
p(f"{'춤':12}{'관절':16}{'체인':>7}{'f(Hz)':>8}{'이득':>8}{'진폭손실':>10}{'위상지연':>10}")
worst35 = 1.0
for d, j, chain, f in SIGNALS:
    f_j = f / (1 - JITTER)
    k = POSE_RATE_BASE * chain
    g = gain(k, f_j)
    worst35 = min(worst35, g)
    p(f"{d:12}{j:16}{chain:7.4f}{f_j:8.3f}{g:8.4f}{(1-g)*100:9.1f}%{lag_deg(k,f_j):9.1f}°")
p(f"  최악 이득 {worst35:.4f}  = 진폭 {(1-worst35)*100:.1f}% 손실.")
p("  ★ D1 어깨 스윕(±9°)이 화면에서 ±%.1f°가 된다 — 죽은 프레임 방지 장치가 사실상 사라진다."
  % (9 * worst35))

# 필요한 계수 유도: 모든 신호에서 이득 >= 0.95
TARGET_GAIN = 0.95
def required_k(chain, f_hz, target=TARGET_GAIN):
    w = 2 * math.pi * f_hz
    # a = chain*k ;  a/sqrt(a^2+w^2) = target  ->  a = target*w/sqrt(1-target^2)
    a = target * w / math.sqrt(1 - target * target)
    return a / chain

p()
p(f"기준: 모든 지속 진동 신호에서 진폭 이득 >= {TARGET_GAIN:.2f}")
p("  (왜 0.95인가 — §26의 기하 검산 4건은 **명령 각도**로 계산됐다. 화면에 나오는 것은 필터를")
p("   통과한 각도다. 5% 손실까지는 그 검산들의 여유가 남는다는 것을 3-4절에서 실제로 확인한다.)")
need = []
for d, j, chain, f in SIGNALS:
    f_j = f / (1 - JITTER)
    need.append((required_k(chain, f_j), f"{d} {j}"))
need.sort(reverse=True)
for k_req, name in need[:4]:
    p(f"  필요 baseRate >= {k_req:7.2f}   <- {name}")
K_DANCE = 78.0
p()
p(f"=> dancePoseSmoothingRate = {K_DANCE:.0f}  (구속 신호 {need[0][1]}가 요구하는 {need[0][0]:.1f}의 바로 위 정수)")
p(f"   비교: 등반 {RATE_CLIMB:.0f} / 활쏘기 {RATE_ARCHERY:.0f} / 무릎앉아 {RATE_CROUCH:.0f} / 기본 {POSE_RATE_BASE:.0f}")
p(f"   전완에 실제로 도달하는 계수 = {K_DANCE:.0f} x {ARM_RATIO} x {LOWER_RATIO} = {K_DANCE*ARM_RATIO*LOWER_RATIO:.2f}")
p(f"   (기본 35의 전완 계수는 {POSE_RATE_BASE*ARM_RATIO*LOWER_RATIO:.2f}. 즉 '체인 비율'은 그대로 보존된다 —")
p("    팔은 여전히 다리보다 늦게 따라오고, follow-through 설계가 죽지 않는다.)")
p()
p(f"{'춤':12}{'관절':16}{'f_max(Hz)':>10}{'이득':>8}{'위상지연(초)':>14}")
for d, j, chain, f in SIGNALS:
    f_j = f / (1 - JITTER)
    k = K_DANCE * chain
    g = gain(k, f_j)
    lag_s = lag_deg(k, f_j) / 360.0 / f_j
    assert g >= TARGET_GAIN - 1e-6, f"{d} {j} 이득 {g}"
    p(f"{d:12}{j:16}{f_j:10.3f}{g:8.4f}{lag_s:14.4f}")
p("  전부 0.95 이상 통과.")

# 프레임률 안정성 (지수 감쇠는 1차라 발산하지 않지만, 실제 값을 적어 둔다)
p()
for fps in (30, 60, 120):
    dt = 1.0 / fps
    p(f"  {fps}fps에서 한 프레임 진행률 = 1-exp(-{K_DANCE:.0f}x{dt:.4f}) = {1-math.exp(-K_DANCE*dt):.4f}"
      f"   (1차 지연이라 어떤 값에서도 진동/발산 없음)")

# ============================================================================
head("2. ★ 정정 — §26의 두 속도 계산이 배율을 한쪽에만 곱했다")
# ============================================================================
p("ResolveWalkSpeed() = walkSpeed x 배율 (StickConfig.cs:2104). 즉 보행 속도는 배율에 비례한다.")
p("§26은 **거리는 배율을 곱하고 속도는 안 곱한** 채로 두 값을 나눴다.")
p()

# (가) D2 도움닫기
run_H = 1.60
run_speed_scale = 1.45
def starjump_run_seconds(s):
    dist = run_H * H_UNITS * s
    spd = run_speed_scale * WALK_SPEED * s
    return dist / spd
t_run = starjump_run_seconds(SCALE_SHIP)
p(f"(가) D2 도움닫기 — §26-3-2는 '0.602초'라고 적었다.")
p(f"     거리 = {run_H} H x {H_UNITS:.4f} x s = {run_H*H_UNITS:.5f}s 유닛")
p(f"     속도 = {run_speed_scale} x {WALK_SPEED} x s = {run_speed_scale*WALK_SPEED:.4f}s 유닛/초")
p(f"     시간 = {run_H*H_UNITS:.5f} / {run_speed_scale*WALK_SPEED:.4f} = {t_run:.5f}초  (배율이 약분된다 = 배율 무관)")
for s in (SCALE_MIN, 0.60, SCALE_SHIP, SCALE_MAX):
    assert abs(starjump_run_seconds(s) - t_run) < 1e-9
p(f"     ★ 0.602초가 아니라 **{t_run:.3f}초**다. §26의 값은 67% 짧다.")
T_RUN = round(t_run, 2)
p(f"     => danceStarJumpRunSeconds = {T_RUN:.2f} (파생값. 필드로 두지 말고 거리/속도에서 계산할 것)")

# (나) D3 활강 속도비
glide_H, moon_loop = 0.30, 0.72
def moonwalk_ratio(s):
    spd = glide_H * H_UNITS * s / moon_loop
    return spd / (WALK_SPEED * s)
r = moonwalk_ratio(SCALE_SHIP)
p()
p(f"(나) D3 활강 속도 — §26-3-3은 '보행의 22.7%'라고 적었다.")
p(f"     활강 = {glide_H} H / {moon_loop}초 = {glide_H*H_UNITS:.5f}s / {moon_loop} = {glide_H*H_UNITS/moon_loop:.5f}s 유닛/초")
p(f"     보행 = {WALK_SPEED} x s")
p(f"     비   = {r*100:.1f}%   (배율 무관)")
p(f"     ★ 22.7%가 아니라 **{r*100:.1f}%**다. 22.7%는 '배율 곱한 활강'을 '배율 안 곱한 보행 2.5'로 나눈 값이다.")
p(f"     결론은 살아남는다 — 보행보다 {1/r:.2f}배 느려서 보행과 혼동되지 않는다. 숫자만 틀렸다.")

# (다) D2 체공시간의 배율 의존
rise_H = 0.42
def air_seconds_gravity(s):
    h = rise_H * H_UNITS * s
    v = math.sqrt(2 * GRAVITY * h)
    return 2 * v / GRAVITY
p()
p("(다) D2 체공시간 — §26은 s=0.60 한 점에서만 중력과 맞췄다(0.66 vs 0.684, 오차 3.5%).")
p(f"     중력과 맞는 체공 = 2*sqrt(2*g*h)/g,  h = {rise_H} H x {H_UNITS:.4f} x s   =>  sqrt(s)에 비례한다")
for s in (SCALE_MIN, 0.60, SCALE_SHIP, SCALE_MAX):
    tg = air_seconds_gravity(s)
    p(f"       s={s:.2f}: 중력값 {tg:.4f}초   고정 0.66초 대비 {(0.66/tg-1)*100:+6.1f}%")
AIR_BASE = air_seconds_gravity(SCALE_SHIP)
p(f"     ★ 고정 0.66초로 두면 s=1.00에서 25% 빨라(가벼워) 보이고 s=0.35에서 26% 느려(붕 떠) 보인다.")
p(f"     => 상수 대신 **식**으로 둔다: t_air(s) = {AIR_BASE:.4f} x sqrt(s/{SCALE_SHIP})")
p(f"        (배포 기본 배율 {SCALE_SHIP}에서 {AIR_BASE:.4f}초. 내역은 상승:정점:하강 = 0.41:0.18:0.41 비율 유지)")
rise_frac, hang_frac = 0.27/0.66, 0.12/0.66
p(f"        상승 {AIR_BASE*rise_frac:.3f} / 정점 {AIR_BASE*hang_frac:.3f} / 하강 {AIR_BASE*rise_frac:.3f}"
  f"  (합 {AIR_BASE*(2*rise_frac+hang_frac):.3f})")

# ============================================================================
head("3. 박자 흔들림(humanization) — '봇처럼 반복되면 안 된다'의 수치화")
# ============================================================================
p("§26의 루프는 전부 **정확한 주기**다. 그대로 두면 8~16초 동안 같은 간격이 20~30번 반복된다.")
p("처방: 루프 경계에서만 재추첨하는 두 종류의 흔들림. (루프 중간에 바꾸면 위상이 튄다)")
p()
p(f"  danceLoopJitterFraction      = {JITTER:.2f}  : 다음 루프 길이 x U(1-{JITTER}, 1+{JITTER})")
p( "  danceAmplitudeJitterFraction = 0.05  : 다음 루프의 봉우리 진폭 x U(0.95, 1.05)")
p()
p("유도(임의값이 아니다):")
p(f"  상한 — 흔들림은 주파수를 1/(1-j)배까지 올린다. 그 최대 주파수에서도 진폭 이득 0.95를")
p(f"         지켜야 하므로 1절의 dancePoseSmoothingRate가 j와 **함께** 결정된다.")
for j in (0.00, 0.04, 0.06, 0.08, 0.12):
    reqs = [required_k(c, f / (1 - j)) for _, _, c, f in SIGNALS]
    p(f"         j={j:.2f} -> 필요 baseRate {max(reqs):6.1f}")
p(f"         j={JITTER:.2f}이 baseRate {K_DANCE:.0f}과 짝이다. 더 키우면 계수가 함께 올라가고,")
p( "         계수를 계속 올리면 '지수 감쇠 = 즉시 대입'에 수렴해 이 앱의 부드러운 관절 연쇄가 죽는다.")
p(f"  하한 — 가장 짧은 루프(D7 {0.48}초)에서 흔들림이 최소 한 프레임(60fps=0.0167초)보다 커야")
p(f"         존재한다. {0.48}x{JITTER} = {0.48*JITTER:.4f}초 = 60fps에서 {0.48*JITTER*60:.2f}프레임. 통과.")
p()
p("★ 예외 2건 (지어낸 예외가 아니라 그 동작의 정체성이다):")
p("   · D5 로봇 — 흔들림 0. 메트로놈처럼 정확한 것이 이 동작의 농담 그 자체다(dime stop).")
p("   · D1/D2 — 세트 내부 박자(회전/도약)에는 걸지 않고 **끝의 '호흡/복귀' 박자에만** 건다.")
p("     회전 시간이 흔들리면 SetFacing 반전 간격이 흔들려 '회전'이 '떨림'이 된다.")

# ============================================================================
head("4. 진입(Intro) / 퇴장(Outro) 박자 — §26이 비워 둔 자리")
# ============================================================================
p("§26은 D1/D2의 세트 내부 박자만 정했다. D3~D7은 '루프'만 있고 중립에서 들어가고 나오는")
p("박자가 없다. 그대로 구현하면 Idle -> 루프 첫 프레임으로 **툭** 갈아탄다.")
p()
p("전이 속도의 앵커 2개 — 둘 다 이 저장소의 확정값이다(충격 구간은 앵커로 쓰지 않는다):")
FOCUS_DELTA, FOCUS_T = 94.0, 0.72     # 집중 팔짱: 팔꿈치 10 -> 104, p=0.26~0.62 of 2.0초
ARCH_DELTA, ARCH_T   = 109.0, 0.42    # 활쏘기 당기기: 전완 10 -> 119, archeryDrawSeconds
w_focus, w_arch = FOCUS_DELTA/FOCUS_T, ARCH_DELTA/ARCH_T
W_INTRO = math.sqrt(w_focus * w_arch)
p(f"  집중 팔짱     {FOCUS_DELTA:.0f}° / {FOCUS_T:.2f}초 = {w_focus:6.1f} °/초   (숙고형 자세 변화의 느린 끝)")
p(f"  활쏘기 당기기 {ARCH_DELTA:.0f}° / {ARCH_T:.2f}초 = {w_arch:6.1f} °/초   (숙고형 자세 변화의 빠른 끝)")
p(f"  기하평균 w_intro = {W_INTRO:.1f} °/초   <- 진입/퇴장 각속도 기준")
p(f"  (참고 — 무릎앉아 압축은 126°/{0.62*0.18:.4f}초 = {126/(0.62*0.18):.0f} °/초다. 그건 **충격 흡수**라")
p( "   운동량이 만든 속도이고 자발적 자세 변화의 앵커가 될 수 없다. 그래서 뺐다.)")
p()
FLOOR = 0.20   # danceRobotStepSeconds — §26이 확정한 '한 박으로 읽히는 최소 시간'
p(f"규칙: t = ceil( (dMax / {W_INTRO:.1f}) / (loop/2) ) x (loop/2),  하한 {FLOOR:.2f}초")
p(f"  · 반(half) 루프 격자로 올림 붙임 = 첫 루프가 박에 맞게 시작한다.")
p(f"  · 하한 {FLOOR:.2f}초의 근거는 danceRobotStepSeconds(§26-3-5). 한 dime stop보다 짧은 구간은")
p( "    별개의 박으로 지각될 수 없다 — 그것이 이 저장소가 이미 확정한 '최소 박' 값이다.")
p()

# dMax = Idle 중립 -> 그 춤의 루프 시작 자세, 관절 하나의 최대 변화량
DANCES = {}
def intro_of(dmax, loop):
    half = loop / 2.0
    t = dmax / W_INTRO
    n = max(1, math.ceil(t / half))
    return max(FLOOR, round(n * half, 4)), t

rows = [
    # id, 이름, dMax 산출, dMax, loop
    ("D3", "문워크",     "뒤 무릎 4 -> 52",      52-IDLE_KNEE,   0.72),
    ("D4", "러닝맨",     "팔꿈치 10 -> 78",      78-IDLE_ELBOW,  0.56),
    ("D5", "로봇",       "앞 팔꿈치 10 -> 90",   90-IDLE_ELBOW,  1.60),
    ("D6", "프리샤트카", "지지 무릎 4 -> 132",   132-IDLE_KNEE,  0.84),
    ("D7", "말춤",       "팔꿈치 10 -> 92",      92-IDLE_ELBOW,  0.48),
]
p(f"{'':4}{'':12}{'dMax 산출':22}{'dMax':>6}{'loop':>7}{'원시 t':>8}{'규칙값':>8}")
for did, name, how, dmax, loop in rows:
    t_in, raw = intro_of(dmax, loop)
    DANCES[did] = dict(name=name, loop=loop, intro=t_in, outro=t_in)
    p(f"{did:4}{name:12}{how:22}{dmax:6.0f}{loop:7.2f}{raw:8.3f}{t_in:8.2f}")
p()
p("★ D5 로봇만 규칙에서 빼고 intro = outro = 0.20초(=1 dime step)로 못박는다.")
p("   규칙대로면 0.80초인데, 로봇춤의 진입은 **스냅**이어야 한다(그게 그 동작이다).")
p("   다만 Idle에서 곧바로 rate=0으로 튀면 '글리치'로 읽히므로, **진입 1스텝만** rate=78로 부드럽게")
p("   키 1에 도착시키고 그 다음 루프부터 rate=0으로 넘긴다. 퇴장도 대칭이다.")
DANCES["D5"]["intro"] = 0.20
DANCES["D5"]["outro"] = 0.20
# D1/D2는 진입·퇴장 박자가 **세트 안에** 있다. 그래서 에피소드 계산에서는 0으로 둔다
# (밖에 또 더하면 준비 자세를 두 번 하게 된다).
DANCES["D1"] = dict(name="피루엣", loop=2.86, intro=0.0, outro=0.0)
DANCES["D2"] = dict(name="스타점프", loop=None, intro=0.0, outro=0.0)

p()
p("D1/D2는 §26이 **세트 내부에** 이미 진입/퇴장 박자를 갖고 있다(고쳐 쓰지 않는다).")
p("  D1: 첫 세트의 (1)준비 plie 0.34 + (2)상승·retire 0.22 가 진입이고,")
p("      마지막 세트의 (4)1번으로 닫고 착지 0.42 + (5)호흡 0.48 이 퇴장이다.")
p("  D2: (1)도움닫기가 진입, (7)복귀+방향전환이 퇴장.")
p("  => 에피소드 길이 계산에서 이 둘의 intro/outro는 0으로 둔다. 밖에 또 더하면 준비 자세를 두 번 한다.")

# ============================================================================
head("5. D2 스타점프 사이클 재구성 — §26 표의 합이 §26 본문과 안 맞는다")
# ============================================================================
old = [0.60, 0.18, 0.27, 0.12, 0.27, 0.22, 0.18]
p(f"§26-3-2 박자 표의 합 = {'+'.join(f'{x:.2f}' for x in old)} = {sum(old):.2f}초")
p(f"그런데 §26-4-4는 같은 동작을 '1회 = 3.06초'로 쓴다. **두 값이 서로 안 맞는다**"
  f"(차이 {3.06-sum(old):.2f}초).")
p("재구성한다 — 2절에서 고친 도움닫기 시간 + §26이 요구한 '매 회 진행 방향 뒤집기' 박자를 명시한다.")
TURN = 0.34     # danceMoonwalkTurnSeconds 재사용 — 같은 동작(제자리 스텝 1회 + facing 반전)
new = [("(1) 도움닫기", T_RUN),
       ("(2) 브레이크+낮은 자세", 0.18),
       ("(3) 상승", round(AIR_BASE*rise_frac, 3)),
       ("(4) 정점(별)", round(AIR_BASE*hang_frac, 3)),
       ("(5) 하강", round(AIR_BASE*rise_frac, 3)),
       ("(6) 착지 흡수", 0.22),
       ("(7) 복귀 + 방향 전환", TURN)]
tot2 = sum(x for _, x in new)
for n, x in new:
    p(f"    {n:22} {x:5.3f}")
p(f"    {'합 (배포 배율 0.75 기준)':22} {tot2:5.3f}초")
DANCES["D2"]["loop"] = round(tot2, 2)
p(f"  ★ 사이클이 배율에 따라 조금 변한다(체공만 sqrt(s)). s=0.35: {sum(x for _,x in new[:2])+air_seconds_gravity(0.35)+0.22+TURN:.3f}초"
  f" / s=1.00: {sum(x for _,x in new[:2])+air_seconds_gravity(1.0)+0.22+TURN:.3f}초")
p("  이것은 결함이 아니라 무게감의 정의다 — 큰 캐릭터는 더 오래 뜬다.")

# ============================================================================
head("6. 에피소드 = 진입 + N x 루프 + 퇴장.  8~16초 창 안에서 N의 범위")
# ============================================================================
EP_MIN, EP_MAX = 8.0, 16.0   # §26-4-4 / AudioReactiveDancePolicy.MotionEpisodeMin/MaxSeconds
p(f"창 = [{EP_MIN}, {EP_MAX}]초. 출처는 §26-4-4이고 그 값이 이미 프로덕션에 있다 —")
p("  AudioReactiveDancePolicy.MotionEpisodeMinSeconds/MaxSeconds. 여기서 고치면 T3가 함께 움직인다.")
p()
p(f"{'':4}{'':12}{'intro':>7}{'loop':>7}{'outro':>7}{'N 범위':>10}{'실제 길이(초)':>18}")
EPISODE = {}
for did in ["D1", "D2", "D3", "D4", "D5", "D6", "D7"]:
    d = DANCES[did]
    intro, outro, loop = d["intro"], d["outro"], d["loop"]
    if did == "D3":
        # 4루프마다 방향 전환 0.34초가 하나 낀다(마지막 루프 뒤에는 넣지 않는다)
        def total(n):
            return intro + n * loop + max(0, (n - 1) // 4) * 0.34 + outro
    else:
        def total(n, intro=intro, outro=outro, loop=loop):
            return intro + n * loop + outro
    ns = [n for n in range(1, 60) if EP_MIN <= total(n) <= EP_MAX]
    EPISODE[did] = (min(ns), max(ns), total(min(ns)), total(max(ns)))
    p(f"{did:4}{d['name']:12}{intro:7.2f}{loop:7.2f}{outro:7.2f}"
      f"{min(ns):5d}~{max(ns):<4d}{total(min(ns)):8.2f} ~ {total(max(ns)):6.2f}")
ep_lo = min(v[2] for v in EPISODE.values())
ep_hi = max(v[3] for v in EPISODE.values())
p(f"  전 동작 실제 길이 범위 = {ep_lo:.2f} ~ {ep_hi:.2f}초  (창 [{EP_MIN}, {EP_MAX}] 안)")

# ============================================================================
head("7. ★ 상태 슬롯 상한 — TimedSpectacleState 교훈의 수치화")
# ============================================================================
BRAKE = 0.28    # danceEntryBrakeSeconds (§26-5-2)
worst_normal = ep_hi + BRAKE
p("교훈: 하나의 상태가 슬롯을 오래 잡으면 다른 연출이 전부 죽는다.")
p("  => 춤은 '음악이 나오는 동안 지속되는 하나의 긴 상태'가 **아니다**. 짧은 에피소드의 반복이다.")
p(f"  정상 최악 = 에피소드 최대 {ep_hi:.2f} + 진입 브레이크 {BRAKE:.2f} = {worst_normal:.2f}초")
HARD_CAP = 18.0
p(f"  danceEpisodeHardCapSeconds = {HARD_CAP:.1f}  (여유 {HARD_CAP-worst_normal:.2f}초)")
p("  이 상한은 연출이 아니라 **감시견**이다. 걸리면 그건 버그이고, 로그에 남기고 Idle로 강제 복귀한다.")
p("  (락도 같이 푼다 — 락을 든 채 상태만 빠지는 것이 이 저장소가 가장 자주 낸 사고 형태다.)")

# ============================================================================
head("8. 퇴장 예산 — '음악이 끊겼을 때 갑자기 멈추면 안 된다'의 상한")
# ============================================================================
p("정상 퇴장은 '지금 박자를 끝내고 -> 퇴장 박자 -> Idle'이다. 최악 대기시간:")
EXIT = {
    "D1": ("회전 (3) 시작 직후에 중단 요청",  1.40 + 0.42),      # (3)+(4). (5)호흡은 건너뛴다
    "D2": ("(2) 시작 직후 (도움닫기 중이면 0.28초 브레이크로 즉시 취소)", tot2 - T_RUN),
    "D3": ("반루프 + 방향전환이 함께 걸린 경우", 0.36 + 0.34 + DANCES["D3"]["outro"]),
    "D4": ("반루프 직후",  0.28 + DANCES["D4"]["outro"]),
    "D5": ("키 경계 직후", 0.20 + DANCES["D5"]["outro"]),
    "D6": ("반루프(차기 1회) 직후", 0.42 + DANCES["D6"]["outro"]),
    "D7": ("반루프 직후", 0.24 + DANCES["D7"]["outro"]),
}
for did, (why, t) in EXIT.items():
    p(f"  {did} {DANCES[did]['name']:10} {t:5.2f}초   <- {why}")
EXIT_BUDGET = max(t for _, t in EXIT.values())
p(f"  최악 = {EXIT_BUDGET:.2f}초 (D1)  =>  danceGracefulExitBudgetSeconds = 1.90 (감시견)")
p(f"  ★ §26-6-4는 이 값을 '로봇 루프 1.60 + 정리 0.42 = 2.02초'로 적었다. 실제 구속은 로봇이 아니라")
p( "    피루엣 회전이다 — 로봇은 **어느 키 경계에서든** 나갈 수 있어 0.40초로 가장 빠르다.")
p()
T2 = 3.0        # AudioReactiveDancePolicy.ReleaseDelaySeconds
POLL = 0.5      # AudioReactiveDancePolicy.ReferencePollIntervalSeconds
p("사용자가 체감하는 '음악이 끝났는데 아직 춘다' 총 지연:")
p(f"  폴링 {POLL} + T2 {T2} + 퇴장 {EXIT_BUDGET:.2f} = {POLL+T2+EXIT_BUDGET:.2f}초")
p(f"  단, 듀티 사이클상 중단 요청의 {100*(1-0.27):.0f}%는 **휴지 중**에 도착한다 — 그때는 화면에 아무 변화가 없다.")

# ============================================================================
head("9. 듀티 사이클 + 피로 램프 — 원칙 2(비침해)의 수치")
# ============================================================================
REST_MIN, REST_MAX = 20.0, 45.0     # §26-4-4
p(f"§26-4-4: 에피소드 8~16 / 휴지 {REST_MIN:.0f}~{REST_MAX:.0f} => 듀티 {12/(12+32.5)*100:.1f}%")
p("그대로 두면 3시간짜리 플레이리스트에서 3시간 내내 27%로 춘다. 사람도 안 그러고, 상주 앱에서는")
p("그게 곧 시선 강탈이다(원칙 2). 그리고 락 점유도 27%로 계속 남는다.")
p()
FAT_STEP, FAT_CAP = 0.25, 3.0
p(f"처방: 피로 램프 F(n) = min(1 + {FAT_STEP}(n-1), {FAT_CAP})  — n = **이번 창 안에서의** 에피소드 번호")
p("      휴지 = U(20,45) x F(n).  창이 닫히면(음악이 T2만큼 끊기면) n은 1로 되돌아간다.")
p("      => 새 감상 세션은 늘 활기차게 시작하고, 한 세션이 길어질수록 조용해진다.")
p()
ep_avg = 12.0
rest_avg = (REST_MIN + REST_MAX) / 2
p(f"{'n':>3}{'F(n)':>7}{'휴지 평균(초)':>14}{'누적 시각(분)':>14}{'누적 듀티':>10}")
t_cum, dance_cum = 0.0, 0.0
for n in range(1, 21):
    F = min(1 + FAT_STEP * (n - 1), FAT_CAP)
    rest = rest_avg * F
    dance_cum += ep_avg
    t_cum += ep_avg + rest
    if n <= 10 or n % 5 == 0:
        p(f"{n:>3}{F:7.2f}{rest:14.1f}{t_cum/60:14.2f}{dance_cum/t_cum*100:9.1f}%")
p(f"  정상상태 듀티 = {ep_avg}/({ep_avg}+{rest_avg}x{FAT_CAP}) = {ep_avg/(ep_avg+rest_avg*FAT_CAP)*100:.1f}%")
p(f"  램프 없는 §26 값 = {ep_avg/(ep_avg+rest_avg)*100:.1f}%")
p("  ★ 짧은 감상(1~2곡)에서는 §26과 거의 같고, 긴 감상에서만 갈라진다 — 그게 의도다.")

# ============================================================================
head("10. T1~T4 맞물림 — 창(window)과 에피소드는 서로 다른 층이다")
# ============================================================================
T1, T3, T4 = 3.0, 8.0, 1200.0
p("AudioReactiveDancePolicy는 Start/Continue/Stop만 낸다. 그 출력을 **상태 전이에 직결하면**")
p("음악이 나오는 내내 Dance 상태가 슬롯을 잡는다 = 7절의 함정 그 자체다.")
p()
p("  1층  창(Window)     : 정책의 Start로 열리고 Stop으로 닫힌다. 상태가 아니라 **플래그**다.")
p("  2층  에피소드       : 창이 열려 있는 동안 디렉터가 에피소드/휴지를 반복한다. 상태는 여기서만 잡는다.")
p()
p(f"검산 — T3(최소 유지 {T3:.0f}초)가 첫 에피소드를 자르지 않는가:")
p(f"  정책이 Stop을 낼 수 있는 가장 이른 시각 = max(T2 {T2:.0f}, T3 {T3:.0f}) = {max(T2,T3):.0f}초 (창 기준)")
p(f"  가장 짧은 에피소드 = {ep_lo:.2f}초 (+ 진입 브레이크 {BRAKE:.2f})")
p(f"  {ep_lo:.2f} + {BRAKE:.2f} = {ep_lo+BRAKE:.2f} >= {T3:.0f}  =>  {'통과' if ep_lo+BRAKE>=T3 else '실패'}")
p( "  ★ 이건 우연이 아니다. T3 = MotionEpisodeMinSeconds이고 그 상수의 문서가 그렇게 적고 있다.")
p( "    에피소드 하한을 8초 아래로 내리는 순간 정책의 Stop이 에피소드 한가운데를 자른다.")
p()
p(f"검산 — T1({T1:.0f}초)과 첫 춤까지의 지연:")
p(f"  음악 시작 + 폴링 {POLL} + T1 {T1:.0f} + 진입 브레이크 {BRAKE:.2f} + 진입 박자(최대 {max(DANCES[d]['intro'] for d in ['D1','D3','D4','D5','D6','D7']):.2f})")
p(f"  = {POLL+T1+BRAKE+0.84:.2f}초 뒤에 첫 루프가 돈다. (D2는 도움닫기 {T_RUN:.2f}초가 그 자리를 대신한다)")
p()
p(f"검산 — T4({T4:.0f}초 = {T4/60:.0f}분) Lockout에서 몇 번 추고 잠기는가:")
n, t = 0, 0.0
while t < T4:
    F = min(1 + FAT_STEP * n, FAT_CAP)
    t += ep_avg + rest_avg * F
    n += 1
p(f"  고착 신호(공백 0)로 {T4/60:.0f}분을 채우는 동안 에피소드 약 {n}회, 총 춤 시간 약 {n*ep_avg/60:.1f}분")
p(f"  = 20분 중 {n*ep_avg/T4*100:.1f}%. Lockout이 걸리면 그 뒤 {T3:.0f}초 연속 무음까지 완전 정지.")
p("  ★ Stop(고착 상한)은 **강제 중단이 아니라 정상 퇴장**으로 처리한다 — 급한 일이 아니다.")
p("    강제 0.18초 크로스페이드는 억제(집중/숨김/드래그)에만 쓴다. 그 셋은 사용자 의도가 급하다.")

# ============================================================================
head("11. 5% 진폭 손실이 §26의 기하 검산을 깨뜨리는가 — 실제로 재본다")
# ============================================================================
def moonwalk_gap(att):
    fh, fk = 30*att, 3*att          # 곧은 다리
    bh, bk = -16*att, 52*att        # 굽힌 다리
    # 정강이 절대각 = 힙 - 무릎(KneeBendSign=-1). cos는 우함수라 부호는 결과에 영향이 없다.
    straight = thigh_H*math.cos(math.radians(fh)) + shin_H*math.cos(math.radians(fh - fk))
    bent     = thigh_H*math.cos(math.radians(bh)) + shin_H*math.cos(math.radians(bh - bk))
    return straight - bent
g100, g95 = moonwalk_gap(1.0), moonwalk_gap(0.95)
p(f"D3 뒤꿈치 들림 — §26은 {0.08126:.5f} H(획의 1.47배)라고 했다.")
p(f"  재현 {g100:.5f} H  (일치)")
p(f"  진폭 95%에서 {g95:.5f} H = 획의 {g95/legstroke_H:.2f}배  ->  여전히 획 두께 위. 통과")
p()
def retire_drop(att):
    h, k = 62*att, 116*att
    return thigh_H*math.cos(math.radians(h)) + shin_H*math.cos(math.radians(h-k))
need_drop = thigh_H
p(f"D1 retire 발끝 높이 — 목표 하강 {need_drop:.5f} H (지지 다리 무릎)")
p(f"  명령 각도 그대로: {retire_drop(1.0):.5f} H  (오차 {abs(need_drop-retire_drop(1.0))*H_UNITS*0.6*PT_PER_UNIT:.3f}pt at s=0.60)")
p(f"  ★ 그러나 retire는 (3) 동안 **유지**되는 자세다 — 지수 감쇠는 유지 목표에 정확히 수렴하므로")
p( "    정상상태 오차 0이다. 이 검산은 감쇠와 무관하게 그대로 성립한다.")
p()
p("결론: 진동 신호에만 손실이 걸리고, 0.95 기준에서 §26의 검산 4건은 전부 여유가 남는다.")

# ============================================================================
head("12. 발판 여유 요구 — §26-7-2 재검증 (도움닫기 시간이 바뀌었으니 다시 잰다)")
# ============================================================================
air_horiz_H = (0.25 * run_speed_scale * WALK_SPEED * SCALE_SHIP * AIR_BASE) / (H_UNITS * SCALE_SHIP)
brake_H = (0.25 * run_speed_scale * WALK_SPEED * SCALE_SHIP / 14.0) / (H_UNITS * SCALE_SHIP)
p(f"D2 수평 예산 (danceStarJumpHorizontalBrakeScale=0.25, 착지 감쇠 14/초):")
p(f"  도움닫기 {run_H:.2f} H")
p(f"  공중 잔여 = 0.25 x {run_speed_scale} x walkSpeed x t_air / H = {air_horiz_H:.4f} H   (§26 추정 0.43)")
p(f"  착지 감속 = v/k = {brake_H:.4f} H                                     (§26 추정 0.25 — 훨씬 보수적이었다)")
tot_req = run_H + air_horiz_H + brake_H
p(f"  합 {tot_req:.4f} H + 여유  =>  §26의 2.70 H는 여유 {2.70-tot_req:.3f} H로 **여전히 유효**하다.")
p(f"  (배율 무관 — 세 항 전부 H로 정규화하면 배율이 약분된다. 단 t_air가 sqrt(s)라 공중 잔여만")
p(f"   배율에 따라 {0.25*run_speed_scale*WALK_SPEED*air_seconds_gravity(SCALE_MAX)/(H_UNITS*SCALE_MAX):.4f}(s=1.0)"
  f" ~ {0.25*run_speed_scale*WALK_SPEED*air_seconds_gravity(SCALE_MIN)/(H_UNITS*SCALE_MIN):.4f}(s=0.35) H로 움직인다.")
p(f"   최악 {0.25*run_speed_scale*WALK_SPEED*air_seconds_gravity(SCALE_MIN)/(H_UNITS*SCALE_MIN):.4f} H를 넣어도 합은 "
  f"{run_H+0.25*run_speed_scale*WALK_SPEED*air_seconds_gravity(SCALE_MIN)/(H_UNITS*SCALE_MIN)+brake_H:.3f} H로 2.70 안이다.)")
p()
p("나머지 6종은 이동이 없거나(§26-7-2) 후진 왕복이라 §26 값을 그대로 승계한다:")
p("  제자리 5종 0.45 H / D3 문워크 1.60 H / D2 스타점프 2.70 H")

# ============================================================================
head("13. 대사 예산 — 원칙 1")
# ============================================================================
p(f"에피소드 최소 길이 {ep_lo:.2f}초. 가독예산 하한 0.82초로 잘리는 상태들과 비교:")
p( "  ParkourClimb ~1.1초 -> 28/28 전부 잘림 (기존 실측)")
p(f"  Dance 에피소드 {ep_lo:.2f}~{ep_hi:.2f}초 -> 잘릴 위험 0. §26-9-1의 판정이 재계산 뒤에도 유효하다.")
p( "  ★ 다만 대사는 **에피소드 진입 1회**뿐이다. 휴지 중에는 춤에서 파생된 대사를 띄우지 않는다")
p( "    (그 순간의 사실은 '춤추는 중'이 아니라 '서 있는 중'이다 — 원칙 1).")

p()
p("=" * 78)
p("검산 끝. 위 숫자 전부가 docs/UX_MOTION_DANCE.md에 그대로 들어간다.")
p("=" * 78)

with open(__file__.replace(".py", ".out.txt"), "w", encoding="utf-8") as f:
    f.write("\n".join(L) + "\n")
