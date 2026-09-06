#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
집중 모드 — 팔짱 포즈 기하 재설계 + 세션 중 앰비언트 박자 검산
design-motion / 2026-09-06

이 스크립트가 하는 일
  (1) 프리팹 기하(SceneBootstrapper 상수)를 그대로 재현해 어깨/엉덩이/머리 좌표를 유도한다.
  (2) 현행 팔짱(FocusCrossArmUpperDegrees=-14 / FocusCrossElbowDegrees=104 / Stagger=11)의
      손·팔꿈치 좌표를 실제로 찍어 "왜 허리 높이로 읽히는가"를 수치로 확인한다.
  (3) 규칙 B(LimbCurveGeometryTests 크리즈 검사)를 그대로 재현해 **팔꿈치 굽힘의 상한**을 구한다.
      -> 팔짱 각도는 이 상한 안에서만 고를 수 있다. 이게 이번 설계의 진짜 벽이다.
  (4) 그 상한 안에서 (어깨A, 팔꿈치A, 어깨B, 팔꿈치B) 4차원을 전수 탐색해
      "팔짱으로 읽히는" 제약을 전부 만족하는 해를 고른다.

숫자를 베끼지 않는다: 모든 상수는 출처 파일/행을 주석에 적고 여기서 다시 유도한다.
"""
import math
from itertools import product

D2R = math.pi / 180.0

# ─────────────────────────────────────────────────────────────────────────
# 0. 프리팹 기하 (Assets/Editor/SceneBootstrapper.cs)
# ─────────────────────────────────────────────────────────────────────────
BASELINE_ARM_UPPER = 0.38      # :257
BASELINE_ARM_LOWER = 0.37      # :257
BASELINE_LEG_UPPER = 0.50      # :258
BASELINE_LEG_LOWER = 0.45      # :258
HEAD_R             = 0.22      # :210  BaselineHeadVisualRadius
SPEC_HIP_Y         = 0.45      # :819
SPEC_SHOULDER_Y    = 1.28      # :819
SPEC_TORSO_TOP_Y   = 1.35      # :819
IDLE_LEG_SPREAD    = 12.0      # :253
IDLE_KNEE_BEND     = 4.0       # :263
IDLE_ARM_SPREAD    = 40.0      # :248
IDLE_ELBOW_BEND    = 10.0      # :264
KNEE_SIGN          = -1.0      # :268
ELBOW_SIGN         = +1.0      # :269
LINE_WIDTH_SCALE   = 1.045     # :138
BASELINE_ARM_W     = 0.10 * LINE_WIDTH_SCALE   # :144
BASELINE_LEG_W     = 0.12 * LINE_WIDTH_SCALE   # :143
BASELINE_BODY_W    = 0.11 * LINE_WIDTH_SCALE   # :142

# StickConfig
MIN_STROKE_PT      = 2.0                 # StickConfig.cs:1934 MinStrokeScreenPoints
REF_PT_PER_UNIT    = 846.0 / (2 * 12.0)  # StickConfig.cs:1987
MIN_SCALE          = 0.35                # StickConfig.cs:1845
MAX_SCALE          = 1.00                # StickConfig.cs:1869
BAKED_SCALE        = 0.75                # StickConfig.cs:1806 characterScale

# LimbCurveRenderer
FILLET_RATIO       = 0.42   # :157
MAX_SAGITTA_PER_W  = 1.0    # :161
ARC_SAMPLES_HALF   = 4      # :166


def limb_drop(hip_deg, lu, ll):
    hip = hip_deg * D2R
    knee = (hip_deg + KNEE_SIGN * IDLE_KNEE_BEND) * D2R
    return lu * math.cos(hip) + ll * math.cos(knee)


def rig(scale=1.0):
    """SceneBootstrapper.BuildCharacterPrefab의 접지 보정을 그대로 재현."""
    lu, ll = BASELINE_LEG_UPPER * scale, BASELINE_LEG_LOWER * scale
    hip_s, sh_s, top_s = SPEC_HIP_Y * scale, SPEC_SHOULDER_Y * scale, SPEC_TORSO_TOP_Y * scale
    lift = max(limb_drop(-IDLE_LEG_SPREAD, lu, ll), limb_drop(IDLE_LEG_SPREAD, lu, ll)) - hip_s
    hip_y = hip_s + lift
    sh_y = sh_s + lift
    top_y = top_s + lift
    head_y = top_y + HEAD_R * scale
    total = head_y + HEAD_R * scale
    return dict(hip_y=hip_y, shoulder_y=sh_y, torso_top_y=top_y, head_y=head_y,
                total=total, torso=sh_y - hip_y,
                au=BASELINE_ARM_UPPER * scale, al=BASELINE_ARM_LOWER * scale,
                head_r=HEAD_R * scale)


R = rig(1.0)
H = R['total']


def arm_points(theta_u_deg, theta_l_deg, r=R):
    """어깨/팔꿈치/손 좌표. 각도 규약은 StickmanPoseAnimator 클래스 문서 그대로:
    마디 로컬 -y가 끝, +Z 회전이 끝을 +x로 보낸다 -> 방향 = (sin θ, -cos θ).
    아래 마디는 위 마디의 자식이라 절대각 = θ_u + θ_l."""
    sh = (0.0, r['shoulder_y'])
    tu = theta_u_deg * D2R
    el = (sh[0] + r['au'] * math.sin(tu), sh[1] - r['au'] * math.cos(tu))
    tt = (theta_u_deg + theta_l_deg) * D2R
    hd = (el[0] + r['al'] * math.sin(tt), el[1] - r['al'] * math.cos(tt))
    return sh, el, hd


# ─────────────────────────────────────────────────────────────────────────
# 1. 현행 팔짱 실측
# ─────────────────────────────────────────────────────────────────────────
CUR_UPPER, CUR_ELBOW, CUR_STAG = -14.0, 104.0, 11.0

print("=" * 78)
print("[0] 리그 실측 (배율 1.0)")
print("=" * 78)
print(f"  전신 H          = {H:.7f}   (StickConfig.BaselineCharacterTotalHeight = 2.2746944)")
print(f"  엉덩이 y        = {R['hip_y']:.4f}")
print(f"  어깨   y        = {R['shoulder_y']:.4f}")
print(f"  몸통 길이 T     = {R['torso']:.4f}  (= 어깨 − 엉덩이)")
print(f"  몸통 상단 y     = {R['torso_top_y']:.4f}   머리 중심 y = {R['head_y']:.4f}  r={R['head_r']:.3f}")
print(f"  팔 상완/전완    = {R['au']:.3f} / {R['al']:.3f}   총 {R['au']+R['al']:.3f} = {(R['au']+R['al'])/H:.4f} H")
print(f"  전완/몸통       = {R['al']/R['torso']:.3f}  ← 전완 하나가 몸통 길이의 {R['al']/R['torso']*100:.0f}%")


def torso_frac(y, r=R):
    return (y - r['hip_y']) / r['torso']


print()
print("=" * 78)
print("[1] 현행 팔짱 실측 — FocusCrossArmUpperDegrees=-14, Elbow=104, Stagger=11")
print("=" * 78)
cur = {}
for name, sign in (("오른팔(sign+1)", +1.0), ("왼팔(sign−1)", -1.0)):
    stag = sign * CUR_STAG
    tu = CUR_UPPER - stag * 0.5
    tl = ELBOW_SIGN * (CUR_ELBOW + stag)
    sh, el, hd = arm_points(tu, tl)
    cur[sign] = (tu, tl, sh, el, hd)
    print(f"  {name}: 어깨각 {tu:+.1f}°  팔꿈치 {tl:.1f}°  전완 절대각 {tu+tl:.1f}°")
    print(f"      팔꿈치 ({el[0]:+.4f}, {el[1]:.4f})  몸통비 {torso_frac(el[1]):.3f}")
    print(f"      손     ({hd[0]:+.4f}, {hd[1]:.4f})  몸통비 {torso_frac(hd[1]):.3f}"
          f"   앞으로 {hd[0]/R['head_r']:.2f} 머리반경")
fa_cur = abs((cur[+1.0][0] + cur[+1.0][1]) - (cur[-1.0][0] + cur[-1.0][1]))
print(f"  두 전완 절대각 차 = {fa_cur:.1f}°  ← 이만큼이면 두 선이 거의 나란하다(=X가 안 생긴다)")
print(f"  두 손 평균 몸통비 = {(torso_frac(cur[+1.0][4][1])+torso_frac(cur[-1.0][4][1]))/2:.3f}"
      f"  (0.50 = 허리, 0.75 = 가슴)")


# ─────────────────────────────────────────────────────────────────────────
# 2. 규칙 B — 팔꿈치 굽힘 상한 (LimbCurveGeometryTests와 같은 식)
# ─────────────────────────────────────────────────────────────────────────
def world_stroke(baked_w, scale):
    return max(baked_w * (scale / BAKED_SCALE), MIN_STROKE_PT / REF_PT_PER_UNIT)


def rule_b_margin(bend_deg, upper_len_baked, lower_len_baked, baked_w, scale):
    """docs/MOTION_SPEC.md 13-6과 글자 그대로 같은 계산."""
    k = scale / BAKED_SCALE
    w_world = world_stroke(baked_w, scale)
    w_local = w_world / k
    h = abs(bend_deg) * D2R / 2.0
    t = FILLET_RATIO * min(upper_len_baked, lower_len_baked)
    if t * math.tan(h / 2.0) > MAX_SAGITTA_PER_W * w_local:
        t = w_local / math.tan(h / 2.0)
    r = t / math.tan(h)
    ratio = r / (w_local / 2.0)
    dphi = 0.5 * abs(bend_deg) * D2R / (ARC_SAMPLES_HALF - 1)
    return ratio / (1.0 / math.cos(0.5 * dphi))


BAKED_ARM_W = world_stroke(BASELINE_ARM_W * BAKED_SCALE, BAKED_SCALE)   # = 프리팹에 구워진 폭
BAKED_LEG_W = world_stroke(BASELINE_LEG_W * BAKED_SCALE, BAKED_SCALE)
ARM_U_B, ARM_L_B = BASELINE_ARM_UPPER * BAKED_SCALE, BASELINE_ARM_LOWER * BAKED_SCALE
LEG_U_B, LEG_L_B = BASELINE_LEG_UPPER * BAKED_SCALE, BASELINE_LEG_LOWER * BAKED_SCALE

SCALES = [MIN_SCALE + (MAX_SCALE - MIN_SCALE) * i / 9.0 for i in range(10)] + [BAKED_SCALE]
SCALES = sorted(set(round(s, 6) for s in SCALES))

print()
print("=" * 78)
print("[2] 규칙 B 재현 — 팔꿈치 굽힘의 물리적 상한")
print("=" * 78)
print(f"  구워진 팔 획 = {BAKED_ARM_W:.6f},  획 하한(월드) = {MIN_STROKE_PT/REF_PT_PER_UNIT:.6f}")
chk = rule_b_margin(122.0, ARM_U_B, ARM_L_B, BAKED_ARM_W, 0.35)
print(f"  ★ 재현 검증: 팔꿈치 122° 배율 0.35 여유 = {chk:.4f}  (MOTION_SPEC 13-6의 1.0461과 대조)")
leg_worst = min(rule_b_margin(126.0, LEG_U_B, LEG_L_B, BAKED_LEG_W, s) for s in SCALES)
print(f"  ★ 다리 126°(landingCrouchFrontKneeDegrees) 전 배율 최악 = {leg_worst:.4f}"
      f"  ← 전체 병목의 기존 바닥")

print()
print("  팔꿈치 굽힘별 전 배율 최악 여유:")
print(f"    {'굽힘':>6} {'최악여유':>9} {'병목배율':>8}   판정")
elbow_cap = None
for bend in [98, 104, 110, 115, 120, 122, 125, 128, 130, 132, 134, 136, 140, 145, 150]:
    worst, ws = min(((rule_b_margin(bend, ARM_U_B, ARM_L_B, BAKED_ARM_W, s), s) for s in SCALES))
    verdict = "OK" if worst >= 1.0 else "★크리즈"
    tail = ""
    if worst >= leg_worst:
        tail = "  (다리 병목보다 위 = 전체 최악이 안 바뀜)"
    print(f"    {bend:6.0f} {worst:9.4f} {ws:8.2f}   {verdict}{tail}")
    if worst >= 1.0:
        elbow_cap = bend

# 정밀 상한 두 개: (a) 여유 1.0, (b) 다리 병목(leg_worst)을 안 깨는 지점
def bisect(target):
    lo, hi = 90.0, 179.0
    for _ in range(80):
        mid = (lo + hi) / 2.0
        m = min(rule_b_margin(mid, ARM_U_B, ARM_L_B, BAKED_ARM_W, s) for s in SCALES)
        if m >= target:
            lo = mid
        else:
            hi = mid
    return lo


cap_hard = bisect(1.0)
cap_soft = bisect(leg_worst)
print()
print(f"  ▶ 절대 상한(여유 1.000) = {cap_hard:.2f}°   ← 이걸 넘으면 관절 안쪽에 각진 크리즈")
print(f"  ▶ 무손상 상한           = {cap_soft:.2f}°   ← 여기까지는 전체 최악이 다리 {leg_worst:.4f} 그대로")
print("  ※ 현행 팔짱의 실제 최대 팔꿈치 = 104+11 = 115° 인데, 이 값은 StickConfig가 아니라")
print("     StickmanPoseAnimator의 private const라서 **위 검사가 한 번도 본 적이 없다**(감사 구멍).")


# ─────────────────────────────────────────────────────────────────────────
# 3. 팔짱 후보 전수 탐색
# ─────────────────────────────────────────────────────────────────────────
def seg_dist(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    dx, dy = bx - ax, by - ay
    L2 = dx * dx + dy * dy
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L2))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def seg_cross(p1, p2, p3, p4):
    def d(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    d1, d2, d3, d4 = d(p3, p4, p1), d(p3, p4, p2), d(p1, p2, p3), d(p1, p2, p4)
    return ((d1 > 0) != (d2 > 0)) and ((d3 > 0) != (d4 > 0))


def cross_point(p1, p2, p3, p4):
    x1, y1 = p1; x2, y2 = p2; x3, y3 = p3; x4, y4 = p4
    den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4)
    if abs(den) < 1e-12:
        return None
    px = ((x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4)) / den
    py = ((x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4)) / den
    return (px, py)


# ── 3-0. 도달 가능성 정리 — "팔꿈치 상한 = 손이 어깨에서 최소한 얼마나 멀리 있어야 하는가" ──
def hand_radius(theta_l_deg, r=R):
    """어깨-손 거리. 팔꿈치를 깊게 접을수록 짧아진다(= 손이 몸 쪽으로 당겨진다)."""
    interior = (180.0 - theta_l_deg) * D2R
    return math.sqrt(r['au'] ** 2 + r['al'] ** 2 - 2 * r['au'] * r['al'] * math.cos(interior))


print()
print("=" * 78)
print("[3] 도달 가능성 정리 — 팔꿈치 상한이 손 높이를 어떻게 묶는가")
print("=" * 78)
print("  손은 어깨 중심 반지름 ρ(팔꿈치) 원 위에만 있을 수 있다. 팔꿈치를 깊게 접을수록 ρ가 줄어")
print("  '앞으로 덜 나가면서 높이 올라간' 손이 가능해진다. 그 깊이를 막는 것이 규칙 B다.")
print()
print(f"    {'팔꿈치':>7} {'ρ':>7} {'몸통비0.62':>11} {'0.68':>8} {'0.72':>8} {'0.80':>8}  (손 x, 머리반경 배수)")
for tl in (104, 110, 115, 116.55, 120, 122, 124.14):
    rho = hand_radius(tl)
    row = []
    for frac in (0.62, 0.68, 0.72, 0.80):
        d = R['shoulder_y'] - (R['hip_y'] + frac * R['torso'])
        row.append(f"{math.sqrt(rho*rho-d*d)/R['head_r']:8.2f}" if rho > d else "     불가")
    print(f"    {tl:7.2f} {rho:7.4f} " + " ".join(row))
print("  ▶ 읽는 법: 값이 클수록 손이 앞으로 튀어나온다. 1.0 = 머리 반경만큼 앞.")

# ─────────────────────────────────────────────────────────────────────────
# 제약 — ★ 두 팔에 **다른** 띠를 준다.
#   실제 팔짱의 옆모습은 대칭이 아니다: 위 팔의 전완은 높고 위로 기울고, 아래 팔의 전완은
#   낮고 거의 수평이다. 두 팔에 같은 띠를 주면(직전 시도) 해가 0개가 되는데, 그건 자세가
#   불가능해서가 아니라 **내가 대칭을 강요했기 때문**이었다.
# ─────────────────────────────────────────────────────────────────────────
ARM_W_LOCAL = BASELINE_ARM_W         # 배율 1.0 팔 획 폭
BODY_W_LOCAL = BASELINE_BODY_W       # 배율 1.0 몸통 획 폭
MERGE = (ARM_W_LOCAL + BODY_W_LOCAL) / 2.0   # 이 거리 안이면 두 획이 붙어 한 덩어리로 보인다

# ★ 상완 가시성 하한 — 추정이 아니라 유도한다.
#   팔꿈치(어깨에서 상완 길이만큼 떨어진 점)가 몸통 획 밖으로 나와야 "팔이 몸에서 갈라져 나온다"로
#   읽힌다: au·sin|θu| > (팔획반폭 + 몸통획반폭)
AXIS_MIN = math.degrees(math.asin(min(1.0, MERGE / R['au'])))
#   그 절반만 나와도 "팔꿈치가 몸통 획을 뚫고 나온" 것은 보인다 — 위 팔(갈비뼈에 붙는 쪽)의 하한.
AXIS_SOFT = math.degrees(math.asin(min(1.0, MERGE / (2.0 * R['au']))))
FWD_MAX   = R['head_r'] * 1.55       # 손이 몸통선 앞으로 나갈 수 있는 최대
ELB_BACK  = -R['head_r'] * 1.05      # 팔꿈치가 몸통선 뒤로 갈 수 있는 최대
ELBOW_MIN = 104.0
CROSS_FWD_MIN = MERGE                # 교차점이 이보다 앞이어야 X가 몸통선과 안 겹친다

# ★ 몸통 기울임 상한 — 팔 부착점은 루트 고정이고 기울임은 몸통/머리만 돌린다
#   (StickmanPoseAnimator.ApplyBodyPlacement). 어깨 높이에서 그려진 몸통선이 옆으로 밀리는 거리가
#   두 획의 합반폭을 넘으면 팔이 몸에서 떨어져 보인다.
LEAN_MAX = math.degrees(math.asin(min(1.0, MERGE / R['torso'])))

# ★ 교차(X)는 기하학적으로 불가능하다 — 증명은 [4-0]에서 수치로 낸다.
#   그래서 판별 신호를 바꾼다: **어느 손이 가장 앞인가.**
#   실제 팔짱의 옆모습에서 최전방 점은 「가슴 높이의 위쪽 전완」이고 아래 팔은 그 밑으로 들어간다.
#   현행 포즈는 이게 정확히 뒤집혀 있다(가장 앞 = 허리 높이의 아래 손) → 그래서 "손 모으기"로 읽힌다.
BAND = {                             # (손 몸통비 하한, 상한, 전완 상승 하한, 상한)
    'A': (0.655, 0.800, +14.0, +40.0),  # 위 팔 — 가슴, 확실히 위로 기운 전완, 최전방
    'B': (0.500, 0.625, -14.0, +4.0),   # 아래 팔 — 낮고 수평~하강, 안쪽으로 들어감
}
TUCK_MIN = ARM_W_LOCAL               # 아래 손이 위 손보다 이만큼은 안쪽이어야 "밑으로 들어간다"

head_c = (0.0, R['head_y'])
sh0 = (0.0, R['shoulder_y'])


def one_arm_candidates(which, elbow_max, expose):
    lo, hi, rlo, rhi = BAND[which]
    out = []
    gu = [x * 0.5 for x in range(int(-44 / 0.5), int(24 / 0.5) + 1)]
    gl = [x * 0.5 for x in range(int(ELBOW_MIN / 0.5), int(elbow_max / 0.5) + 1)]
    for ua, la in product(gu, gl):
        # B는 팔꿈치가 몸통 획 **밖**으로, A는 최소한 획의 절반은 뚫고 나와야 한다.
        if ua > -(AXIS_MIN if expose else AXIS_SOFT):
            continue
        _, ea, ha = arm_points(ua, la)
        if ea[0] < ELB_BACK or ea[0] > 0.0:   # 팔꿈치는 둘 다 몸통선 뒤
            continue
        if not (0.0 <= ha[0] <= FWD_MAX):
            continue
        if not (lo <= torso_frac(ha[1]) <= hi):
            continue
        if not (rlo <= (ua + la) - 90.0 <= rhi):
            continue
        if math.hypot(ha[0] - head_c[0], ha[1] - head_c[1]) < R['head_r'] + ARM_W_LOCAL:
            continue
        out.append((ua, la, ea, ha))
    return out


# ── [4-0] X 교차가 왜 불가능한가 — 팔꿈치 높이 폭을 먼저 못 박는다 ──────────────
print()
print("=" * 78)
print("[4-0] 「X자 교차」가 기하학적으로 불가능한 이유")
print("=" * 78)
ely = [arm_points(u, 110)[1][1] for u in range(-44, 1)]
print(f"  팔꿈치는 어깨 중심 반지름 {R['au']:.3f} 원 위에만 있다. 어깨각 −44°~0° 구간에서")
print(f"  팔꿈치 y는 {min(ely):.4f} ~ {max(ely):.4f} — 폭이 겨우 {max(ely)-min(ely):.4f}"
      f" (몸통 길이의 {(max(ely)-min(ely))/R['torso']*100:.1f}%, 팔 획 두께의 {(max(ely)-min(ely))/ARM_W_LOCAL:.2f}배)")
print("  두 전완이 X로 교차하려면 시작점(팔꿈치) 높이가 확실히 갈라져 있어야 하는데,")
print(f"  이 리그에서는 두 팔꿈치를 최대로 벌려도 {max(ely)-min(ely):.4f}밖에 못 벌린다.")
print(f"  그래서 교차점은 언제나 몸통선 근처(획 합반폭 {MERGE:.4f} 안쪽)에서 일어나고,")
print("  ▶ **화면에서는 X가 몸통 획에 먹혀 안 보인다.** 교차를 판별 신호로 쓸 수 없다.")


def search(elbow_max):
    ca = one_arm_candidates('A', elbow_max, expose=False)
    cb = one_arm_candidates('B', elbow_max, expose=True)
    best = None
    for (ua, la, ea, ha), (ub, lb, eb, hb) in product(ca, cb):
        fa, fb = ua + la, ub + lb
        if fa - fb < 20.0:                    # 두 전완이 확실히 갈라져야 팔이 둘로 보인다
            continue
        if ha[0] - hb[0] < TUCK_MIN:          # ★ 아래 손이 위 손보다 안쪽 = "밑으로 들어간다"
            continue
        if eb[1] - ea[1] < 0.0:               # 아래 팔의 팔꿈치가 더 높아야 한다(어깨각이 더 뒤)
            continue
        if math.hypot(ha[0] - hb[0], ha[1] - hb[1]) < ARM_W_LOCAL * 1.6:
            continue                          # 두 손이 붙으면 한 팔로 보인다
        score = (
            + torso_frac(ha[1]) * 5.0                                  # 위 손이 높을수록 좋다
            + (ha[0] - hb[0]) / R['head_r'] * 1.5                      # 아래 팔이 깊이 들어갈수록 좋다
            - ha[0] / R['head_r'] * 1.1                                # 그래도 앞으로 덜 나갈수록 좋다
            + min(fa - fb, 45.0) / 45.0 * 1.4                          # 두 전완이 갈라질수록 좋다
            + min(fa - 90.0, 30.0) / 30.0 * 0.8                        # 위 전완이 위로 향할수록 좋다
        )
        if best is None or score > best[0]:
            best = (score, ua, la, ub, lb, ea, ha, eb, hb)
    return len(ca), len(cb), best


def report(title, elbow_max):
    na, nb, best = search(elbow_max)
    print()
    print("-" * 78)
    print(f"  {title}   (팔꿈치 ≤ {elbow_max:.2f}°)")
    print(f"  후보: A {na}개 / B {nb}개")
    if best is None:
        print("  ★ 해 없음.")
        return None
    sc, ua, la, ub, lb, ea, ha, eb, hb = best
    print(f"  점수 {sc:.4f}")
    for tag, u, l, e, h in (("A(위 팔·최전방)", ua, la, ea, ha), ("B(아래 팔·안쪽) ", ub, lb, eb, hb)):
        print(f"  {tag}: 어깨 {u:+6.1f}°  팔꿈치 {l:6.1f}°  전완 절대각 {u+l:+6.1f}°"
              f"  (수평 대비 {u+l-90:+.1f}°)")
        print(f"      팔꿈치 ({e[0]:+.4f}, {e[1]:.4f}) 몸통비 {torso_frac(e[1]):.3f}"
              f"   손 ({h[0]:+.4f}, {h[1]:.4f}) 몸통비 {torso_frac(h[1]):.3f}"
              f"  앞 {h[0]/R['head_r']:.2f}R")
    print(f"  ★ 손 안쪽 물림(위−아래) = {ha[0]-hb[0]:+.4f} = 팔 획의 {(ha[0]-hb[0])/ARM_W_LOCAL:.2f}배"
          f"   (현행 {cur[+1.0][4][0]-cur[-1.0][4][0]:+.4f} = 부호가 반대)")
    print(f"  두 전완 절대각 차 = {(ua+la)-(ub+lb):.1f}°   (현행 {fa_cur:.1f}°)")
    print(f"  ▶ 코드 형태 환산: 어깨 중앙 {(ua+ub)/2:+.1f}° / 어깨 스태거 전폭 {abs(ua-ub):.1f}°"
          f" / 팔꿈치 중앙 {(la+lb)/2:.1f}° / 팔꿈치 스태거 전폭 {abs(la-lb):.1f}°")
    for b in sorted({la, lb}):
        w = min(rule_b_margin(b, ARM_U_B, ARM_L_B, BAKED_ARM_W, s) for s in SCALES)
        tail = ("전체 최악 무변화(다리 %.4f)" % leg_worst) if w >= leg_worst else "★ 전체 최악을 끌어내림"
        print(f"       규칙 B 여유(팔꿈치 {b:5.1f}°) = {w:.4f}  {tail}")
    return best


print()
print("=" * 78)
print("[4] 후보 탐색 — 「위 팔이 최전방, 아래 팔이 안쪽」")
print("=" * 78)
print(f"  ★ 유도 상수: 획 합반폭 = {MERGE:.4f}  →  상완 노출 하한 |θu| ≥ {AXIS_MIN:.2f}°"
      f" (A는 {AXIS_SOFT:.2f}°),  몸통 기울임 상한 |lean| ≤ {LEAN_MAX:.2f}°")
print(f"     (현행 FocusWatchLeanDegrees = −5° 는 이 상한의 {5.0/LEAN_MAX*100:.0f}% 를 쓴다 — 안전)")
print(f"  A(위 팔)  : 손 몸통비 {BAND['A'][0]}~{BAND['A'][1]}, 전완 상승 {BAND['A'][2]:+.0f}~{BAND['A'][3]:+.0f}°"
      f"  (상완은 몸통에 붙어도 된다 — 실제 팔짱에서 상완은 갈비뼈에 붙는다)")
print(f"  B(아래 팔): 손 몸통비 {BAND['B'][0]}~{BAND['B'][1]}, 전완 상승 {BAND['B'][2]:+.0f}~{BAND['B'][3]:+.0f}°"
      f"  (상완 노출 필수)")
print(f"  공통      : 손 x ≤ {FWD_MAX:.3f}({FWD_MAX/R['head_r']:.2f}R) / 팔꿈치 x ∈ [{ELB_BACK:.3f}, 0]"
      f" / 물림 ≥ {TUCK_MIN:.4f}")
W1 = report("W1 ★ 규칙 B 무손상 상한 안 (확정 후보)", cap_soft)
W2 = report("W2 절대 상한까지 허용 (참고 — 규칙 B 여유를 1.17 → 1.00 으로 깎는다)", cap_hard)

best = W1 or W2


# ─────────────────────────────────────────────────────────────────────────
# 4. 배율 3점에서 실루엣이 유지되는가 (각도는 배율 불변이므로 화면상 크기만 확인)
# ─────────────────────────────────────────────────────────────────────────
if best is not None:
    sc, ua, la, ub, lb, *_ = best
    print()
    print("=" * 78)
    print("[5] 배율 스윕 — 전방 돌출이 화면상 몇 pt인가")
    print("=" * 78)
    print(f"    {'배율':>6} {'획(pt)':>8} {'손 돌출(pt)':>11} {'전완 길이(pt)':>13}  판정")
    for s in (0.35, 0.60, 0.75, 1.00):
        r = rig(s)
        _, ea, ha = arm_points(ua, la, r)
        _, eb, hb = arm_points(ub, lb, r)
        w = world_stroke(BASELINE_ARM_W * BAKED_SCALE, s)
        fwd = max(ha[0], hb[0]) * REF_PT_PER_UNIT
        fl = r['al'] * REF_PT_PER_UNIT
        ok = "OK" if fwd > w * REF_PT_PER_UNIT * 1.5 else "★ 획에 먹힘"
        print(f"    {s:6.2f} {w*REF_PT_PER_UNIT:8.2f} {fwd:11.2f} {fl:13.2f}  {ok}")


# ─────────────────────────────────────────────────────────────────────────
# 5. 세션 앰비언트 박자 검산
# ─────────────────────────────────────────────────────────────────────────
print()
print("=" * 78)
print("[6] 세션 앰비언트 — 25분 세션에서 몇 번 보이는가")
print("=" * 78)
SESSION = 25 * 60.0
# AutoWanderController 실측 (StickConfig 기본값)
IDLE_MIN, IDLE_MAX = 2.0, 6.0          # wanderIdleDurationMin/Max
WALK_MIN, WALK_MAX = 1.5, 4.0          # wanderWalkDurationMin/Max
WALK_CHANCE_NOW = 0.75                 # wanderPostIdleWalkChance
WALK_CHANCE_FOCUS = 0.40               # 제안값
for tag, wc, imin, imax in (("현행(세션 무관)", WALK_CHANCE_NOW, IDLE_MIN, IDLE_MAX),
                            ("제안(세션 중)", WALK_CHANCE_FOCUS, 4.0, 11.0)):
    idle_mean = (imin + imax) / 2.0
    walk_mean = (WALK_MIN + WALK_MAX) / 2.0
    # 한 사이클 = Idle 1회 + (확률 wc로) Walk 1회
    cycle = idle_mean + wc * walk_mean
    cycles = SESSION / cycle
    idle_total = cycles * idle_mean
    print(f"  {tag}: 사이클 {cycle:.2f}초 → {cycles:.0f}회, "
          f"Idle 총 {idle_total:.0f}초 = 세션의 {idle_total/SESSION*100:.1f}%, "
          f"걷기 총 {SESSION-idle_total:.0f}초")

print()
print("  ★ 새 타이머를 만들지 않는다 — 발행자는 AutoWanderController.TickResting의 기존 1회 추첨")
print("    (Idle 진입 후 wanderLookAroundDelayMin~Max 뒤 그 Idle 구간에 1회 + 최소 간격 쿨다운)이고,")
print("    세션 중에는 그 **쿨다운 값과 어휘만** 바뀐다.")
INTERVAL = 28.0                        # 제안 focusAmbientGestureCooldownSeconds
IDLE_MEAN_FOCUS = (4.0 + 11.0) / 2.0
WALK_MEAN = (WALK_MIN + WALK_MAX) / 2.0
CYCLE_FOCUS = IDLE_MEAN_FOCUS + WALK_CHANCE_FOCUS * WALK_MEAN
# 쿨다운이 풀린 뒤 실제 발동까지는 «다음 Idle 구간의 추첨 시점»을 기다린다.
extra = CYCLE_FOCUS / 2.0 + (1.0 + 2.5) / 2.0
eff = INTERVAL + extra
n_total = SESSION / eff
print(f"    쿨다운 {INTERVAL:.0f}초 + 다음 추첨까지 대기 평균 {extra:.1f}초 → 실효 간격 {eff:.1f}초")
print(f"    총 발동 {n_total:.0f}회 / 25분")
GEST = [
    ("G1 자세 고쳐 잡기", 1.30, 0.34),
    ("G2 발밑 링 확인", 1.00, 0.26),
    ("G3 화면 쪽 돌아보기", 0.90, 0.24),
    ("G4 관망 자세 바꾸기", 1.60, 0.16),
]
assert abs(sum(w for _, _, w in GEST) - 1.0) < 1e-9, "가중치 합이 1이 아니다"
motion_time = 0.0
for name, dur, w in GEST:
    n = n_total * w
    motion_time += n * dur
    print(f"      {name:<18} 가중치 {w:.2f} → {n:5.1f}회 x {dur:.2f}초 = {n*dur:6.1f}초")
print(f"    제스처 총 {motion_time:.0f}초 = 세션의 {motion_time/SESSION*100:.2f}%")
g4 = n_total * 0.16
print(f"    ※ G4(자세 토글) {g4:.1f}회 → 관망 자세가 평균 {SESSION/max(g4,1e-9)/60:.1f}분마다 바뀐다")
print(f"    ※ 최장 침묵 = 쿨다운 {INTERVAL:.0f} + 걷기 최대 {WALK_MAX:.1f} + 추첨 지연 최대 2.5"
      f" ≈ {INTERVAL+WALK_MAX+2.5:.1f}초")
print()
print("  ── L1 미세 생명감(항상 도는 층) ─────────────────────────────────")
SWAY_AMP, SWAY_PERIOD = 2.0, 11.0
base_lean = -4.0
print(f"    상체 흔들림: 기준 {base_lean:+.1f}° ± {SWAY_AMP:.1f}°, 주기 {SWAY_PERIOD:.0f}초"
      f" → 범위 [{base_lean-SWAY_AMP:+.1f}, {base_lean+SWAY_AMP:+.1f}]°")
print(f"      기울임 상한 {LEAN_MAX:.2f}° 대비 최대 사용률 {abs(base_lean)+SWAY_AMP:.1f}/{LEAN_MAX:.2f}"
      f" = {(abs(base_lean)+SWAY_AMP)/LEAN_MAX*100:.0f}%  → 팔이 몸에서 떨어져 보이지 않는다")
hd_arm = R['head_y'] - R['hip_y']
head_travel = hd_arm * (math.sin((abs(base_lean) + SWAY_AMP) * D2R)
                        - math.sin((abs(base_lean) - SWAY_AMP) * D2R))
print(f"      머리 왕복폭(peak-to-peak) = {head_travel:.4f}유닛 = 배율 0.75에서 "
      f"{head_travel * 0.75 * REF_PT_PER_UNIT:.2f}pt (반주기 {SWAY_PERIOD/2:.1f}초에 걸쳐)")
print(f"      25분에 {SESSION/SWAY_PERIOD:.0f}주기 — 정지 조각상이 되는 구간이 없다")
print()
print("  ── 비교: 지금(오늘 고친 뒤) ─────────────────────────────────────")
print("  현행 노출: FocusStart 2.0초 + FocusNudge(도달 조건부) 뿐")
print(f"    2.0 / {SESSION:.0f} = {2.0/SESSION*100:.4f}%  ← 페르소나가 말한 '99.87%'의 근거")
print(f"    (정확히는 {100-2.0/SESSION*100:.4f}% 가 평소와 동일)")
print(f"  제안 노출: 관망 자세 {IDLE_MEAN_FOCUS/CYCLE_FOCUS*100:.0f}%(Idle 전체) + 제스처"
      f" {motion_time/SESSION*100:.1f}% + L1 흔들림 100%")
print("    ▶ 「세션인지 아닌지 화면만 보고 구분 가능한 시간」이 0.13% → 87% 로 간다.")


# ─────────────────────────────────────────────────────────────────────────
# 7. 대안 실루엣 — 뒷짐(hands behind back)
# ─────────────────────────────────────────────────────────────────────────
# 팔짱은 **정면(관상면) 자세**라 옆모습으로는 원리적으로 근사밖에 안 된다(위에서 증명).
# 반면 뒷짐은 **시상면(옆모습) 자세**라 2D 옆모습에서 손실이 0이다. 세션 내내 같은 자세를
# 유지하지 않기 위해서라도 두 번째 관찰 자세가 필요하므로 여기서 함께 유도한다.
print()
print("=" * 78)
print("[7] 대안/보조 실루엣 — 뒷짐 (옆모습 손실 0)")
print("=" * 78)
best_bs = None
for ua in [x * 0.5 for x in range(int(-58 / 0.5), int(-30 / 0.5) + 1)]:
    for la in [x * 0.5 for x in range(int(40 / 0.5), int(100 / 0.5) + 1)]:
        _, e, h = arm_points(ua, la)
        if h[0] > -MERGE:                       # 손이 몸통선 뒤로 확실히 나가야 뒷짐이다
            continue
        if not (0.20 <= torso_frac(h[1]) <= 0.45):   # 엉치~허리 아래
            continue
        if e[0] > -R['head_r'] * 0.7:           # 팔꿈치도 뒤로 나와야 실루엣이 생긴다
            continue
        score = (- abs(torso_frac(h[1]) - 0.30) * 5.0
                 - abs(h[0] + R['head_r'] * 0.75) * 4.0
                 + min(la, 90.0) / 90.0 * 0.5)
        if best_bs is None or score > best_bs[0]:
            best_bs = (score, ua, la, e, h)
sc, ua, la, e, h = best_bs
print(f"  어깨 {ua:+.1f}°  팔꿈치 {la:.1f}°  전완 절대각 {ua+la:+.1f}°")
print(f"  팔꿈치 ({e[0]:+.4f}, {e[1]:.4f}) 몸통비 {torso_frac(e[1]):.3f}"
      f"   손 ({h[0]:+.4f}, {h[1]:.4f}) 몸통비 {torso_frac(h[1]):.3f}  뒤 {-h[0]/R['head_r']:.2f}R")
w = min(rule_b_margin(la, ARM_U_B, ARM_L_B, BAKED_ARM_W, s) for s in SCALES)
print(f"  규칙 B 여유 = {w:.4f}  (다리 병목 {leg_worst:.4f} 대비 {w/leg_worst:.2f}배 — 여유 충분)")
print(f"  어깨 관절 한계 검사: 뒤쪽 상한 −60° vs 요구 {ua:+.1f}°  →"
      f" {'통과' if ua >= -60 else '★ 초과'} (SceneBootstrapper.ShoulderSwingBackLimitDegrees)")
print("  ※ 두 팔에 스태거를 주면 손 2개가 겹쳐 하나로 보이는 것을 막는다(아래 사양 참고).")

# ─────────────────────────────────────────────────────────────────────────
# 8. 현행 vs 신규 — 한 표로
# ─────────────────────────────────────────────────────────────────────────
print()
print("=" * 78)
print("[8] 현행 vs 신규 — 판정 지표 4개")
print("=" * 78)
NU_A, NL_A, NU_B, NL_B = -8.5, 116.5, -37.0, 113.0
_, eA, hA = arm_points(NU_A, NL_A)
_, eB, hB = arm_points(NU_B, NL_B)
c_hi = max(cur[+1.0][4], cur[-1.0][4], key=lambda p: p[1])
c_fw = max(cur[+1.0][4], cur[-1.0][4], key=lambda p: p[0])
rows = [
    ("최전방 손의 몸통비 (0.50=허리 / 0.75=가슴)", torso_frac(c_fw[1]), torso_frac(hA[1])),
    ("가장 높은 손의 몸통비", torso_frac(c_hi[1]), torso_frac(hA[1])),
    ("두 전완 절대각 차(도)", fa_cur, (NU_A + NL_A) - (NU_B + NL_B)),
    ("손 물림 = 위손x − 아래손x (양수여야 팔짱)",
     cur[+1.0][4][0] - cur[-1.0][4][0], hA[0] - hB[0]),
]
print(f"  {'지표':<44}{'현행':>10}{'신규':>10}")
for name, a, b in rows:
    print(f"  {name:<44}{a:10.3f}{b:10.3f}")
print()
print("  ★ 네 번째 줄이 이번 재설계의 전부다: 현행은 **가장 앞에 나온 손이 가장 낮은 손**이라")
print("     '허리 앞에 두 손을 모은' 그림이 되고, 신규는 부호가 뒤집혀 '가슴 앞 전완 아래로")
print("     다른 팔이 들어간' 그림이 된다.")


# ─────────────────────────────────────────────────────────────────────────
# 9. 「안경 밀어올리기」 박자 — 지금 손끝이 실제로 어디에 떨어지는가
# ─────────────────────────────────────────────────────────────────────────
# StickmanPoseAnimator.TrySolveFocusGlassesAngles:
#   target = (shoulder.x + reach x FocusGlassesHandForwardRatio, _headNeutral.y)
#   reach  = 상완 + 전완,  FocusGlassesHandForwardRatio = 0.30
print()
print("=" * 78)
print("[9] 「안경 밀어올리기」 손끝 실측 — MOTION_SPEC 13절과 같은 잣대")
print("=" * 78)
FWD_RATIO_NOW = 0.30
reach = R['au'] + R['al']


def ik_angles(target):
    """SolveTwoLinkIk와 같은 결과(팔꿈치 부호 +1). (어깨각, 팔꿈치각) 반환."""
    dx, dy = target[0] - 0.0, target[1] - R['shoulder_y']
    rho = math.hypot(dx, dy)
    rho = min(rho, R['au'] + R['al'] - 1e-6)
    beta = math.degrees(math.atan2(dx, -dy))          # (sinθ, −cosθ) 규약
    cos_in = (R['au'] ** 2 + R['al'] ** 2 - rho ** 2) / (2 * R['au'] * R['al'])
    theta_l = 180.0 - math.degrees(math.acos(max(-1.0, min(1.0, cos_in))))
    cos_a = (R['au'] ** 2 + rho ** 2 - R['al'] ** 2) / (2 * R['au'] * rho)
    alpha = math.degrees(math.acos(max(-1.0, min(1.0, cos_a))))
    return beta - alpha, theta_l, rho


tgt_now = (reach * FWD_RATIO_NOW, R['head_y'])
u_now, l_now, rho_now = ik_angles(tgt_now)
d_now = math.hypot(tgt_now[0], tgt_now[1] - R['head_y'])
print(f"  현행 목표점 = ({tgt_now[0]:.4f}, {tgt_now[1]:.4f})  = 머리 중심에서 {d_now:.4f}")
print(f"    머리 시각 반경 {R['head_r']:.3f} 대비 **바깥으로 {d_now - R['head_r']:+.4f}유닛**")
for s in (0.35, 0.60, 0.75, 1.00):
    print(f"      배율 {s:.2f} → 화면상 돌출 {(d_now - R['head_r']) * s * REF_PT_PER_UNIT:+.2f}pt")
print(f"    유도된 각도: 어깨 {u_now:.1f}°, 팔꿈치 {l_now:.1f}°")
mg = min(rule_b_margin(l_now, ARM_U_B, ARM_L_B, BAKED_ARM_W, s) for s in SCALES)
print(f"    규칙 B 여유 = {mg:.4f}"
      f"  {'★ 다리 병목(%.4f) 아래 — 이 박자가 전체 최악이 된다' % leg_worst if mg < leg_worst else '무변화'}")
print("  ▶ 판정: 돌출이 배율 0.75에서 0.2pt 미만 = **손끝이 머리 실루엣과 사실상 같은 자리**다.")
print("    머리는 불투명이고 팔(2)보다 위(HeadFill 3 / HeadOutline 4)에 그려지므로")
print("    **손이 화면에 거의 안 나온다** — MOTION_SPEC 13절이 손차양 122°에서 잡아낸 것과 같은 병.")

print()
print("  ── 개정안: 관자놀이 접촉(손끝이 머리 실루엣 밖으로 확실히 나온다) ──")
# MOTION_SPEC 13-5가 쓴 잣대와 같게 — 손끝이 실루엣 밖으로 나온 양을 배율 1.0에서 2.2pt로 맞춘다.
CLEAR_PT_AT_1 = 2.2
clear = CLEAR_PT_AT_1 / REF_PT_PER_UNIT
BETA_TEMPLE = 75.0                    # 머리 중심 기준 +y에서 앞쪽으로 잰 각(관자놀이)
tgt_new = (R['head_r'] + clear) * math.sin(BETA_TEMPLE * D2R), \
          R['head_y'] + (R['head_r'] + clear) * math.cos(BETA_TEMPLE * D2R)
u_new, l_new, rho_new = ik_angles(tgt_new)
d_new = math.hypot(tgt_new[0], tgt_new[1] - R['head_y'])
print(f"    목표점 = ({tgt_new[0]:.4f}, {tgt_new[1]:.4f})  머리 중심에서 {d_new:.4f}"
      f" (= 반경 + {d_new - R['head_r']:.4f})")
for s in (0.35, 0.60, 0.75, 1.00):
    print(f"      배율 {s:.2f} → 화면상 돌출 {(d_new - R['head_r']) * s * REF_PT_PER_UNIT:+.2f}pt")
print(f"    유도된 각도: 어깨 {u_new:.1f}°, 팔꿈치 {l_new:.1f}°")
mg2 = min(rule_b_margin(l_new, ARM_U_B, ARM_L_B, BAKED_ARM_W, s) for s in SCALES)
print(f"    규칙 B 여유 = {mg2:.4f}"
      f"  {'무변화(다리 %.4f)' % leg_worst if mg2 >= leg_worst else '★ 전체 최악을 끌어내림'}")
print(f"    유휴 '손차양'(어깨 107° / 팔꿈치 98°)과의 각도 차 ="
      f" 어깨 {abs(u_new-107):.1f}° / 팔꿈치 {abs(l_new-98):.1f}°"
      f"  → {'구분됨' if abs(u_new-107) >= 12 else '★ 너무 비슷 — 같은 동작으로 보인다'}")
print()
print("  ▶ 두 파라미터로 표현하면(현행 형태 유지):")
print(f"       FocusGlassesHandForwardRatio  0.30 → {tgt_new[0]/reach:.4f}"
      f"   (손끝 x = 팔 전체 길이 x 이 값)")
print(f"       FocusGlassesHandRiseRatio     (신설) = {(tgt_new[1]-R['head_y'])/reach:.4f}"
      f"   (손끝 y = 머리 중심 + 팔 길이 x 이 값)")
print("       ※ 둘 다 **팔 길이 비율**이라 배율/기하가 바뀌어도 따라온다(현행 규약 그대로).")
