#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
코스튬 x 집중모드 — 소환 오브젝트 기하 + LFVS 키포즈 검산
design-motion / 2026-09-07   (docs/UX_MOTION_COSTUME_FOCUS.md 의 모든 숫자가 여기서 나온다)

숫자를 베끼지 않는다. 상수는 전부 출처 파일/행을 적고 여기서 다시 유도한다.
선행 라운드(2026-09-06_집중세션_팔짱기하_검산.py)의 리그/규칙B 재현을 그대로 승계한다.
"""
import math

D2R = math.pi / 180.0
OK, NG = "OK", "★위반"

# ───────────────────────── 0. 프리팹 기하 (Assets/Editor/SceneBootstrapper.cs) ────
BASELINE_ARM_UPPER = 0.38      # :257
BASELINE_ARM_LOWER = 0.37      # :257
BASELINE_LEG_UPPER = 0.50      # :258
BASELINE_LEG_LOWER = 0.45      # :258
HEAD_R             = 0.22      # :210
SPEC_HIP_Y         = 0.45      # :819
SPEC_SHOULDER_Y    = 1.28      # :819
SPEC_TORSO_TOP_Y   = 1.35      # :819
IDLE_LEG_SPREAD    = 12.0      # StickConfig.idleLegSpreadDegrees :64
IDLE_KNEE_BEND     = 4.0       # StickConfig.idleKneeBendDegrees  :120
IDLE_ARM_SPREAD    = 40.0      # StickConfig.idleArmSpreadDegrees :68
IDLE_ELBOW_BEND    = 10.0      # StickConfig.idleElbowBendDegrees :124
KNEE_SIGN, ELBOW_SIGN = -1.0, +1.0
LINE_WIDTH_SCALE   = 1.045     # :138
BASELINE_ARM_W     = 0.10 * LINE_WIDTH_SCALE
BASELINE_LEG_W     = 0.12 * LINE_WIDTH_SCALE
BASELINE_BODY_W    = 0.11 * LINE_WIDTH_SCALE
SHOULDER_BACK_LIMIT    = 60.0    # SceneBootstrapper :338
SHOULDER_FORWARD_LIMIT = 150.0   # SceneBootstrapper :339

MIN_STROKE_PT   = 2.0
REF_PT_PER_UNIT = 846.0 / (2 * 12.0)     # StickConfig.ReferencePointsPerWorldUnitApprox :2246 = 35.25
MIN_SCALE, MAX_SCALE, BAKED_SCALE = 0.35, 1.00, 0.75
FILLET_RATIO, MAX_SAGITTA_PER_W, ARC_SAMPLES_HALF = 0.42, 1.0, 4

BREATH_AMPLITUDE = 0.012   # StickConfig.idleBreathAmplitude :107 (월드유닛, 배율 비례 아님)

def limb_drop(hip_deg, lu, ll):
    return lu*math.cos(hip_deg*D2R) + ll*math.cos((hip_deg + KNEE_SIGN*IDLE_KNEE_BEND)*D2R)

def rig(scale=1.0):
    lu, ll = BASELINE_LEG_UPPER*scale, BASELINE_LEG_LOWER*scale
    hip_s, sh_s, top_s = SPEC_HIP_Y*scale, SPEC_SHOULDER_Y*scale, SPEC_TORSO_TOP_Y*scale
    lift = max(limb_drop(-IDLE_LEG_SPREAD, lu, ll), limb_drop(IDLE_LEG_SPREAD, lu, ll)) - hip_s
    hip_y, sh_y, top_y = hip_s+lift, sh_s+lift, top_s+lift
    head_y = top_y + HEAD_R*scale
    return dict(hip_y=hip_y, shoulder_y=sh_y, head_y=head_y, total=head_y+HEAD_R*scale,
                torso=sh_y-hip_y, au=BASELINE_ARM_UPPER*scale, al=BASELINE_ARM_LOWER*scale,
                head_r=HEAD_R*scale)

R = rig(1.0); H = R['total']; SH = (0.0, R['shoulder_y']); REACH = R['au']+R['al']

print("="*86)
print("[0] 리그 재현 (배율 1.0)")
print("="*86)
print(f"  전신 H = {H:.7f}  (StickConfig.BaselineCharacterTotalHeight = 2.2746944)  "
      f"{OK if abs(H-2.2746944)<1e-6 else NG}")
print(f"  어깨 y = {R['shoulder_y']:.4f} = {R['shoulder_y']/H:.4f} H   엉덩이 y = {R['hip_y']:.4f} = {R['hip_y']/H:.4f} H")
print(f"  머리중심 y = {R['head_y']:.4f} = {R['head_y']/H:.4f} H, 시각반경 {R['head_r']:.3f} = {R['head_r']/H:.4f} H")
print(f"  팔 상완/전완 = {R['au']:.3f}/{R['al']:.3f}, 합(=도달원 반지름) = {REACH:.3f} = {REACH/H:.4f} H")
print(f"  ★ 도달 원 : 중심 (0, {R['shoulder_y']/H:.4f} H), 반지름 {REACH/H:.4f} H")
print(f"     -> 프롭 «작업점»은 전부 이 원 안에 있어야 한다. 이 문서의 모든 프롭 x는 여기서 나온다.")

# 도달 원의 전방 경계: 주어진 x에서 손이 닿는 y 범위
def reach_band_at(x_h):
    dx = x_h*H
    if abs(dx) > REACH: return None
    dy = math.sqrt(REACH**2 - dx**2)
    return ((R['shoulder_y']-dy)/H, (R['shoulder_y']+dy)/H)

print()
print("  전방 x별 도달 y 밴드(H):")
for xh in (0.20, 0.26, 0.28, 0.30, 0.31, 0.32, 0.33, 0.3297):
    b = reach_band_at(xh)
    print(f"    x={xh:.4f} H -> y ∈ [{b[0]:.4f}, {b[1]:.4f}] H" if b else f"    x={xh:.4f} H -> 도달 불가")

# ───────────────────────── 1. 규칙 B (크리즈) 재현 ────────────────────────────────
def world_stroke(baked_w, scale):
    return max(baked_w*(scale/BAKED_SCALE), MIN_STROKE_PT/REF_PT_PER_UNIT)

def rule_b_margin(bend_deg, upper_b, lower_b, baked_w, scale):
    k = scale/BAKED_SCALE
    w_local = world_stroke(baked_w, scale)/k
    h = abs(bend_deg)*D2R/2.0
    t = FILLET_RATIO*min(upper_b, lower_b)
    if t*math.tan(h/2.0) > MAX_SAGITTA_PER_W*w_local:
        t = w_local/math.tan(h/2.0)
    r = t/math.tan(h)
    ratio = r/(w_local/2.0)
    dphi = 0.5*abs(bend_deg)*D2R/(ARC_SAMPLES_HALF-1)
    return ratio/(1.0/math.cos(0.5*dphi))

BAKED_ARM_W = world_stroke(BASELINE_ARM_W*BAKED_SCALE, BAKED_SCALE)
BAKED_LEG_W = world_stroke(BASELINE_LEG_W*BAKED_SCALE, BAKED_SCALE)
ARM_U_B, ARM_L_B = BASELINE_ARM_UPPER*BAKED_SCALE, BASELINE_ARM_LOWER*BAKED_SCALE
LEG_U_B, LEG_L_B = BASELINE_LEG_UPPER*BAKED_SCALE, BASELINE_LEG_LOWER*BAKED_SCALE
SCALES = sorted(set(round(MIN_SCALE+(MAX_SCALE-MIN_SCALE)*i/9.0, 6) for i in range(10)) | {BAKED_SCALE})

def arm_rule_b_worst(bend):
    return min(rule_b_margin(bend, ARM_U_B, ARM_L_B, BAKED_ARM_W, s) for s in SCALES)

LEG_WORST = min(rule_b_margin(126.0, LEG_U_B, LEG_L_B, BAKED_LEG_W, s) for s in SCALES)
CHK122 = rule_b_margin(122.0, ARM_U_B, ARM_L_B, BAKED_ARM_W, 0.35)

print()
print("="*86)
print("[1] 규칙 B(크리즈) 재현 — 이 문서의 팔꿈치 예산")
print("="*86)
print(f"  ★ 재현 검증(교정점): 팔꿈치 122° 배율 0.35 여유 = {CHK122:.4f}  "
      f"(MOTION_SPEC 13-6 기록 1.0461)  {OK if abs(CHK122-1.0461)<0.001 else NG}")
print(f"  다리 병목(무릎 126°, 전 배율 최악) = {LEG_WORST:.4f}   <- 이 값 아래로 내려가면 전체 최악을 깎는다")
print(f"  팔꿈치 무손상 상한(선행 라운드 확정) = 116.55°  -> 여유 {arm_rule_b_worst(116.55):.4f}")

# ───────────────────────── 2. 순기구학 / 역기구학 ────────────────────────────────
def fk(tu, tl, r=R):
    """어깨/팔꿈치/손. 각도 규약: 마디 로컬 -y가 끝, +Z가 끝을 +x(=facing 방향)로.
    전완 절대각 = tu + tl (아래 마디는 위 마디의 자식)."""
    sh = (0.0, r['shoulder_y'])
    el = (sh[0] + r['au']*math.sin(tu*D2R), sh[1] - r['au']*math.cos(tu*D2R))
    ta = (tu+tl)*D2R
    hd = (el[0] + r['al']*math.sin(ta), el[1] - r['al']*math.cos(ta))
    return sh, el, hd

def ik(target, r=R):
    """두 마디 IK — ElbowBendSign(+1, 전완이 앞으로 접힘) 해만 돌려준다."""
    dx, dy = target[0]-0.0, target[1]-r['shoulder_y']
    d = math.hypot(dx, dy)
    if d > r['au']+r['al'] or d < abs(r['au']-r['al']): return None
    c = (r['au']**2 + r['al']**2 - d**2)/(2*r['au']*r['al'])
    interior = math.degrees(math.acos(max(-1.0, min(1.0, c))))
    bend = 180.0 - interior                              # = tl (>=0)
    theta_line = math.degrees(math.atan2(dx, -dy))       # 어깨->목표 방향의 규약각
    a = math.degrees(math.asin(max(-1.0, min(1.0, r['al']*math.sin(interior*D2R)/d))))
    for tu in (theta_line - a, theta_line + a):
        _, _, hd = fk(tu, bend)
        if math.hypot(hd[0]-target[0], hd[1]-target[1]) < 1e-6:
            return tu, bend
    return None

def ray_ground_hit(tu, tl, ground_h):
    """전완이 가리키는 반직선이 지면(y = ground_h·H)과 만나는 x(H). 안 만나면 None."""
    _, _, hd = fk(tu, tl)
    ta = (tu+tl)*D2R
    dirv = (math.sin(ta), -math.cos(ta))
    if dirv[1] >= -1e-6: return None
    t = (hd[1] - ground_h*H)/(-dirv[1])
    return (hd[0] + dirv[0]*t)/H

def report_pose(name, tu, tl, lean, dyH, work_target_h=None, point_ground_h=None, point_span_h=None):
    sh, el, hd = fk(tu, tl)
    d = math.hypot(hd[0], hd[1]-R['shoulder_y'])
    marg = arm_rule_b_worst(tl)
    flags = []
    if not (-SHOULDER_BACK_LIMIT <= tu <= SHOULDER_FORWARD_LIMIT): flags.append("어깨한계")
    if not (8.0 <= tl <= 116.55): flags.append("팔꿈치예산")
    if marg < LEG_WORST: flags.append("규칙B")
    if abs(lean) > 7.60: flags.append("기울임상한")
    if abs(dyH) > 0.032: flags.append("몸오프셋상한")
    if 0.0 < abs(dyH) < 0.017: flags.append("몸오프셋하한")
    extra = ""
    if work_target_h is not None:
        gap = math.hypot(hd[0]-work_target_h[0]*H, hd[1]-work_target_h[1]*H)/H
        extra = f" 접촉오차 {gap:.4f} H ({gap*H*BAKED_SCALE*REF_PT_PER_UNIT:.2f}pt@0.75)"
        if gap > 0.012: flags.append("접촉오차")
    if point_ground_h is not None:
        x = ray_ground_hit(tu, tl, point_ground_h)
        if x is None:
            extra += " 지면 미교차"; flags.append("지향")
        else:
            inside = point_span_h[0] <= x <= point_span_h[1]
            extra += f" 지향 착점 x={x:.4f} H ({'원 안' if inside else '★원 밖'})"
            if not inside: flags.append("지향")
    print(f"    {name:<22} θu={tu:+7.2f} θl={tl:6.2f} lean={lean:+5.2f} dY={dyH:+.4f}H | "
          f"손=({hd[0]/H:+.4f},{hd[1]/H:.4f})H |어깨-손|={d/H:.4f}H 규칙B={marg:.4f}"
          f"{extra}  {'/'.join(flags) if flags else OK}")
    return hd

# ───────────────────────── 3. 스텝레이트 약수 검증 ───────────────────────────────
print()
print("="*86)
print("[2] LFVS 스텝레이트 — 왜 3fps / 5fps 둘뿐인가")
print("="*86)
STILL_DIVISOR  = 4   # Platform/ViewerPresence.cs:512 FramePacingPolicy.DefaultStillDivisor
CALM_DIVISOR   = 2   # FramePacingPolicy.BuildPlan (Calm 1/2)
ACTIVE_DIVISOR = 2   # StickConfig.activeTierRenderDivisor (2026-09-07 사용자 승인)
LOOP_HZ = 60.0
tiers = {"Active": ACTIVE_DIVISOR, "Calm": CALM_DIVISOR, "Still": STILL_DIVISOR}
print(f"  게임 루프 {LOOP_HZ:.0f}Hz. 제출 fps = 60/분주.  "
      + ", ".join(f"{k} {LOOP_HZ/v:.0f}fps(분주{v})" for k, v in tiers.items()))
print()
print(f"    {'스텝':>5} {'게임프레임/스텝':>15} " + " ".join(f"{k+' 잔차':>11}" for k in tiers) + "   판정")
for fps in (1, 2, 3, 4, 5, 6, 7, 8, 10, 15):
    gf = LOOP_HZ/fps
    ok = gf == int(gf)
    row, verdict = [], OK if ok else "게임프레임 비정수"
    for k, v in tiers.items():
        if not ok: row.append("—"); continue
        rem = int(gf) % v
        row.append(f"{rem}")
        if rem != 0: verdict = f"{NG} {k} 잔차{rem}"
    mark = "  <-- 채택" if fps in (3, 5) and verdict == OK else ""
    print(f"    {fps:>3}fps {int(gf) if ok else gf:>13}  " + " ".join(f"{r:>11}" for r in row) + f"   {verdict}{mark}")
print()
print("  ★ 잔차가 0이 아니면 «스텝 경계가 제출 프레임에 대해 매번 다른 지연으로 나타난다» —")
print("    같은 4fps 스텝이 Still에서 3.75게임프레임이라 지연이 0,3,2,1프레임으로 순환한다(눈에 띄는 불규칙).")
print("  ★ 15의 약수만 남는다: {1,3,5,15}. 1fps는 «움직임»으로 안 읽히고 15fps는 절감이 0 -> 3fps/5fps 둘뿐.")

# 상체 기울임 감쇠가 한 스텝 안에서 얼마나 수렴하는가
LEAN_RATE = 12.0   # StickConfig.bodyLeanSmoothingRate :3469
print()
print("  스텝 안 상체기울임 수렴(bodyLeanSmoothingRate=12/s):")
for fps in (3, 5):
    t = 1.0/fps
    print(f"    {fps}fps 스텝 {t:.4f}초 -> 목표의 {100*(1-math.exp(-LEAN_RATE*t)):.1f}% 도달"
          f"  ({'스텝처럼 읽힘' if (1-math.exp(-LEAN_RATE*t))>0.85 else '번짐'})")
print("  ★ 팔다리는 smoothingRate=0으로 넘겨 즉시 대입한다(SmoothTo의 «rate<=0이면 즉시» 폴백,")
print("    danceRobotPoseSmoothingRate=0가 이미 쓰는 경로 — 신규 배관 0줄).")
print("    상체 기울임만 감쇠 경로를 공유하므로 위 표가 그 잔차의 전부다.")

# ───────────────────────── 4. 구간 3분할 (game-architect 식 채택) ────────────────
print()
print("="*86)
print("[3] 세션 3구간 — game-architect FocusSessionPhases 채택 재현")
print("="*86)
NOMINAL_FRACTION, MIN_EDGE, MAX_EDGE, MAX_EDGE_FRACTION = 0.20, 60.0, 300.0, 0.40
def edge_seconds(D):
    return min(min(max(NOMINAL_FRACTION*D, MIN_EDGE), MAX_EDGE), MAX_EDGE_FRACTION*D)
def phases_sec(D):
    e = edge_seconds(D)
    return e, D-2*e, e
print("  채택: docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md 8-1  Core/FocusSessionPhases.EdgeSeconds")
print("        edge = min( clamp(0.20 x D, 60, 300),  0.40 x D )")
print("  ★ 내 1차 초안 clamp(0.20 x D, 20, 300)은 폐기한다 — 그쪽 식이 «두 가장자리가 세션을 다")
print("    먹는» 구간을 0.40D로 추가로 막고, D=60초에서도 몰입기가 0이 안 된다(같은 라운드 교차검토).")
print()
print(f"    {'세션':>9} {'적응기':>9} {'몰입기':>10} {'한계':>9} {'합':>9}   검산")
for D in (60, 120, 150, 180, 300, 900, 1500, 3000, 3600):
    e, im, _ = phases_sec(float(D))
    note = ""
    if D == 1500:
        note = ("사용자 명시 0~5 / 5~20 / 20~25분과 정확히 일치 " + OK) if (abs(e-300)<1e-6 and abs(im-900)<1e-6) else NG
    if D == 60:
        note = ("몰입기 > 0 " + OK) if im > 0 else NG
    print(f"    {D:>7}초 {e:>8.1f}초 {im:>9.1f}초 {e:>8.1f}초 {2*e+im:>8.1f}초   {note}")

# ───────────────────────── 5. 몰입기 내부 3분할 / 듀티 ───────────────────────────
print()
print("="*86)
print("[4] 몰입기 내부 3분할 + 듀티 사이클 — 값의 유도와 Transform 쓰기 절감")
print("="*86)
print("  ★ game-architect 8-3 매핑 채택: 코스튬 LFVS와 프롭은 «몰입기»에만 산다.")
print("    사용자 요구 «세션 내 3단계 실시간 미세변화»는 그래서 몰입기를 다시 3등분해 만든다 —")
print("    코스튬이 보이지 않는 구간(적응기·한계)에 미세변화를 넣어 봐야 아무도 못 본다.")
MIN_CYCLES_PER_SEG, MIN_CYCLE, MAX_CYCLE = 3.0, 4.0, 8.0
WRITES_PER_STEP = 10
KEYS = 4
SEGS = [("I-a 진입", 3, 1.0/3.0), ("I-b 절정", 5, 0.50), ("I-c 이완", 3, 1.0/6.0)]
def cycle_seconds(immersion):
    return min(max((immersion/3.0)/MIN_CYCLES_PER_SEG, MIN_CYCLE), MAX_CYCLE)
def plan(immersion, keys=KEYS):
    cyc = cycle_seconds(immersion)
    out = []
    for name, fps, duty in SEGS:
        loop = keys/fps
        loops = max(1, int(duty*cyc/loop + 0.5))
        W = loops*loop
        out.append((name, fps, loops, W, cyc-W, W/cyc, loops*keys))
    return cyc, out
print(f"  주기 = clamp( (몰입기/3) / {MIN_CYCLES_PER_SEG:.0f}, {MIN_CYCLE:.1f}, {MAX_CYCLE:.1f} )초")
print(f"    하한 근거: 한 소구간에 주기가 {MIN_CYCLES_PER_SEG:.0f}번은 들어가야 «리듬»으로 읽힌다")
print(f"    상한 근거: 정지 구간 R이 L1 상시 흔들림 반주기(11.0/2 = 5.5초)를 넘어야 «멈췄다»로 읽힌다")
print(f"              -> 최대 듀티 0.50에서 R = 0.5 x 8.0 = 4.0초... 는 5.5초에 못 미치므로,")
print(f"              몰입기 절정 구간은 «완전히 멈추는 그림»을 목표로 하지 않는다(의도된 것).")
print(f"  루프 수 = round(목표듀티 x 주기 / 루프길이),  루프길이 = 키수 / 스텝레이트 -> W는 언제나 정수 루프")
for D in (60.0, 300.0, 1500.0, 3600.0):
    _, im, _ = phases_sec(D)
    cyc, rows = plan(im)
    print(f"\n  세션 {D:.0f}초 (몰입기 {im:.1f}초, 소구간 {im/3:.1f}초, 주기 {cyc:.4f}초)")
    print(f"    {'소구간':<10} {'fps':>4} {'루프':>4} {'W(초)':>8} {'R(초)':>8} {'듀티':>7} {'스텝/주기':>9} {'쓰기/초':>8}")
    for name, fps, loops, W, Rr, duty, steps in rows:
        assert abs(W*fps - round(W*fps)) < 1e-9, "W가 정수 스텝이 아니다"
        assert Rr > 0.0, "정지 구간이 사라졌다"
        assert abs((W/(KEYS/fps)) - round(W/(KEYS/fps))) < 1e-9, "W가 정수 루프가 아니다"
        print(f"    {name:<10} {fps:>4} {loops:>4} {W:>8.4f} {Rr:>8.4f} {duty*100:>6.1f}% {steps:>9} "
              f"{steps/cyc*WRITES_PER_STEP:>8.2f}")

_, IM25, _ = phases_sec(1500.0)
cyc25, rows25 = plan(IM25)
seg25 = IM25/3.0
avg_w = sum(seg25*r[6]/cyc25*WRITES_PER_STEP for r in rows25)/IM25
base_w = LOOP_HZ*WRITES_PER_STEP
print()
print(f"  25분 세션 몰입기 평균 Transform 쓰기 = {avg_w:.2f}/초  (현행 관망자세 {base_w:.0f}/초 대비 "
      f"{100*(1-avg_w/base_w):.1f}% 감소)")
print(f"  ★ 몰입기는 배회 확률이 0이라(game-architect 4-3) 이 구간에 걷기 60fps 지분이 없다.")
sess = (IM25*avg_w + (1500.0-IM25)*base_w)/1500.0
print(f"  세션 전체 근사 = (몰입기 {IM25:.0f}초 x {avg_w:.2f}) + (나머지 {1500.0-IM25:.0f}초 x {base_w:.0f}) / 1500")
print(f"                = {sess:.1f}/초  ({100*(1-sess/base_w):.1f}% 감소)")
print("  ★ 이것은 CPU/쓰기 절감이지 GPU 절감이 아니다(game-architect 판정: GPU는 면적 x 제출 고정분).")
print("    절감이 실제로 나오려면 «스텝이 안 바뀐 프레임에는 어떤 Transform도 건드리지 않고 return»해야 한다.")
print()
print("  ── 3키 코스튬(오피스 초안)에서의 같은 계산 — 키 수가 다르면 W 표가 통째로 달라진다 ──")
cyc3, rows3 = plan(IM25, keys=3)
for name, fps, loops, W, Rr, duty, steps in rows3:
    assert abs(W*fps - round(W*fps)) < 1e-9
    print(f"    {name:<10} {fps:>4}fps {loops:>2}루프  W={W:>6.4f}초  R={Rr:>6.4f}초  듀티 {duty*100:>5.1f}%  스텝 {steps}")

# ───────────────────────── 6. 프롭 수명 ─────────────────────────────────────────
print()
print("="*86)
print("[5] 프롭 수명 — game-architect 6-3 채택(몰입기 진입 1회 빌드 / 한계 진입 철거)")
print("="*86)
WALK_CHANCE, IDLE_MIN, IDLE_MAX, IDLE_SHARE = 0.40, 4.0, 11.0, 0.872
idle_mean = (IDLE_MIN+IDLE_MAX)/2
draft_life = (1/WALK_CHANCE)*idle_mean
draft_count = 1500.0*IDLE_SHARE/draft_life
print(f"  ★ 내 1차 초안(«Idle 정착마다 소환 / Idle 이탈마다 소멸»)은 폐기한다.")
print(f"    그 안이었다면 소환 {draft_count:.1f}회/세션 = 평균 {1500.0/draft_count:.1f}초마다 1회 —")
print(f"    내가 스스로 «이 설계 최대 리스크»로 적어 올린 값이다. 채택안이 그것을 0으로 만든다.")
INTRO, OUTRO = 0.35, 0.22
print(f"  채택: 몰입기 진입 1회 빌드 + 한계 진입 1회 철거 = 세션당 정확히 1회  ({draft_count:.0f}회 -> 1회)")
print(f"    성립 조건: 몰입기 배회 확률 0 (game-architect 4-3). 걷기를 0으로 안 내리면 프롭만 남는다 — 한 묶음이다.")
print(f"    등장 {INTRO}초 + 퇴장 {OUTRO}초 스케일 램프 = 세션 전체 {(INTRO+OUTRO)*LOOP_HZ:.0f}프레임 x 1 쓰기")
print(f"      -> 세션 평균 {(INTRO+OUTRO)*LOOP_HZ/1500.0:.4f} 쓰기/초. 그 밖 구간 쓰기 0 (PC-1/PC-2).")
print(f"    진화 단계는 세션 시작에 래치된다(game-architect 규칙 C-6) -> 세션 중 프롭 재빌드 0회.")

# ───────────────────────── 7. 프롭 기하 ─────────────────────────────────────────
print()
print("="*86)
print("[6] 소환 오브젝트 4종 — 기하와 발판 요구 폭")
print("="*86)
def pt(h_units_multiple, scale):   # H배수 -> pt
    return h_units_multiple*H*scale*REF_PT_PER_UNIT

PROPS = {
 "사이버 홀로콘솔": dict(near=0.31, far=0.77, top=0.90, lines=6),
 "오피스 스탠딩책상": dict(near=0.26, far=1.00, top=1.02, lines=10),
 "광부 광맥벽":      dict(near=0.28, far=0.78, top=0.94, lines=9),
 "대마법사 마법진":   dict(near=0.26, far=1.055, top=0.915, lines=11),
}
MARGIN = 0.10
print(f"    {'프롭':<18} {'근단(H)':>8} {'원단(H)':>8} {'폭(H)':>7} {'높이(H)':>8} "
      f"{'필요폭+여유(H)':>14} {'@0.75(pt)':>10} {'x0.70축소(pt)':>13} {'선':>4}")
for k, v in PROPS.items():
    w = v['far']-v['near']; need = v['far']+MARGIN
    print(f"    {k:<18} {v['near']:>8.4f} {v['far']:>8.4f} {w:>7.4f} {v['top']:>8.4f} "
          f"{need:>14.4f} {pt(need,BAKED_SCALE):>10.1f} {pt(need*0.70,BAKED_SCALE):>13.1f} {v['lines']:>4}")
print(f"  ★ 필요폭은 «캐릭터 루트 x부터 프롭 원단까지 + 여유 {MARGIN}H»다 — 발판(창) 상단이 그만큼")
print(f"    남아 있어야 소환한다(ArcheryDirector의 «과녁은 지금 딛고 있는 발판 위» 규칙과 같은 잣대).")
print(f"  ★ 배율 0.35에서는 위 pt가 {0.35/BAKED_SCALE:.3f}배가 된다 -> 최대 프롭 "
      f"{pt(PROPS['대마법사 마법진']['far']+MARGIN, 0.35):.1f}pt.")

# ───────────────────────── 8. 키포즈 표 ─────────────────────────────────────────
print()
print("="*86)
print("[7] LFVS 키포즈 4종 x 4장 — 각도표와 전 항목 검산")
print("="*86)
print("  각도 규약: facing 중립 공간. θu 0=팔이 바로 아래, +가 진행 방향(앞). θl = 팔꿈치 굽힘(>=0).")
print("  A = 앞팔(limb.NeutralSign>0, Idle 중립 +40) / B = 뒷팔(NeutralSign<0, 중립 -40).")
print("  다리는 전 키포즈 무수정(고관절 ±12, 무릎 -4) — 접지 계약(ComputeFootGroundingOffset)을 안 건드린다.")

def ik_pose(target_h):
    s = ik((target_h[0]*H, target_h[1]*H))
    assert s, f"IK 실패 {target_h}"
    return round(s[0], 2), round(s[1], 2)

print()
print("  ── C1 사이버펑크 연구원 «홀로 콘솔 조작» (프롭지향 2장: K1·K2) ──")
c1_k1 = ik_pose((0.31, 0.82)); c1_k2 = ik_pose((0.31, 0.72))
print(f"     (IK 유도) K1 A 작업점 (0.31, 0.82)H -> θu={c1_k1[0]}, θl={c1_k1[1]}")
print(f"     (IK 유도) K2 B 작업점 (0.31, 0.72)H -> θu={c1_k2[0]}, θl={c1_k2[1]}")
C1 = [
 ("K0 대기(정지 키포즈)", (62.0, 56.0), (48.0, 68.0), -2.0,  0.0,    None, None),
 ("K1 좌측 입력",         c1_k1,        (44.0, 72.0), -1.0,  0.0,    (0.31,0.82), None),
 ("K2 우측 입력",         (58.0, 60.0), c1_k2,        -1.0,  0.0,    None, (0.31,0.72)),
 ("K3 판독 끄덕임",       (70.0, 44.0), (56.0, 58.0), +2.0, -0.018,  None, None),
]
for name, a, b, lean, dy, ta, tb in C1:
    report_pose(name+" A", a[0], a[1], lean, dy, work_target_h=ta)
    report_pose(name+" B", b[0], b[1], lean, dy, work_target_h=tb)

print()
print("  ── C2 현대 직장인/독서실 «서류 넘기기» (프롭지향 1장: K1) ── [P-13 티어 게이트: 미출하 초안]")
c2_k1 = ik_pose((0.28, 0.62))
print(f"     (IK 유도) K1 A 작업점 (0.28, 0.62)H -> θu={c2_k1[0]}, θl={c2_k1[1]}")
C2 = [
 ("K0 대기",     (46.0, 66.0), (-26.0, 22.0), -2.0,  0.0,   None, None),
 ("K1 상판 짚기", c2_k1,       (-26.0, 22.0), +2.5, -0.018, (0.28,0.62), None),
 ("K2 넘기기",   (40.0, 78.0), (-22.0, 26.0), +1.0,  0.0,   None, None),
]
for name, a, b, lean, dy, ta, tb in C2:
    report_pose(name+" A", a[0], a[1], lean, dy, work_target_h=ta)
    report_pose(name+" B", b[0], b[1], lean, dy, work_target_h=tb)

print()
print("  ── C3 광부 «광맥 두드리기» (프롭지향 2장: K1·K2) ──")
c3_k1 = ik_pose((0.28, 0.85)); c3_k2 = ik_pose((0.28, 0.65))
print(f"     (IK 유도) K2 A 타격점 (0.28, 0.85)H -> θu={c3_k1[0]}, θl={c3_k1[1]}")
print(f"     (IK 유도) K3 A 훑는점 (0.28, 0.65)H -> θu={c3_k2[0]}, θl={c3_k2[1]}")
C3 = [
 ("K0 겨눔(정지 키포즈)", (86.0, 34.0), (-24.0, 28.0), -1.0,  0.0,    None, None),
 ("K1 치켜듦",            (142.0, 52.0),(-30.0, 24.0), -4.0, +0.018,  None, None),
 ("K2 내리침",            c3_k1,        (-18.0, 34.0), +5.0, -0.030,  (0.28,0.85), None),
 ("K3 여파(훑기)",        c3_k2,        (-20.0, 30.0), +2.0, -0.020,  (0.28,0.65), None),
]
for name, a, b, lean, dy, ta, tb in C3:
    report_pose(name+" A", a[0], a[1], lean, dy, work_target_h=ta)
    report_pose(name+" B", b[0], b[1], lean, dy, work_target_h=tb)

print()
print("  ── C4 판타지 대마법사 «영창» (프롭지향 2장: K1·K2 — 접촉이 아니라 «지향») ──")
MAGIC_SPAN = (0.26, 0.94)   # 마법진 외타원의 지면 x 범위(H)
GROUND_H = 0.02
C4 = [
 ("K0 영창 준비(정지)", (-30.0, 92.0), (-34.0, 96.0), -2.0,  0.0,   None, None),
 ("K1 지목(앞팔)",      (-34.0, 84.0), (-32.0, 92.0), +1.5,  0.0,   None, None),
 ("K2 지목(뒷팔)",      (-26.0, 88.0), (-40.0, 96.0), +1.0,  0.0,   None, None),
 ("K3 끌어올림",        (16.0, 84.0),  (-28.0, 88.0), -3.0, +0.020, None, None),
]
POINTING = {"K1 지목(앞팔)": "A", "K2 지목(뒷팔)": "B"}
for name, a, b, lean, dy, _, _ in C4:
    pa = (GROUND_H, MAGIC_SPAN) if POINTING.get(name) == "A" else (None, None)
    pb = (GROUND_H, MAGIC_SPAN) if POINTING.get(name) == "B" else (None, None)
    report_pose(name+" A", a[0], a[1], lean, dy, point_ground_h=pa[0], point_span_h=pa[1])
    report_pose(name+" B", b[0], b[1], lean, dy, point_ground_h=pb[0], point_span_h=pb[1])

# ───────────────────────── 9. 대사 예산 ─────────────────────────────────────────
print()
print("="*86)
print("[8] 대사 예산(원칙 1) — 붙일 수 있는 자리는 하나뿐이다")
print("="*86)
BASE, PER_KR, MIN_S, MAX_S = 0.28, 0.075, 0.62, 2.20   # Dialogue/DialogueKind.DialogueBudget
MAX_VIS_SCALE = 2.0                                    # DialogueKind.MaxVisibleScale
def reading(n): return min(max(BASE+n*PER_KR, MIN_S), MAX_S)
HOSTS = [("FocusComplete(세션 완료)", 2.5),    # StickConfig.pomodoroCompletePoseHoldSeconds :1330
         ("FocusStart(세션 시작)",   2.0),    # pomodoroStartPoseHoldSeconds :1327
         ("FocusCancelled",          1.5)]    # pomodoroCancelPoseHoldSeconds :1333
print(f"    {'숙주 상태':<24} {'지속(초)':>9} {'100% 최대 글자':>15} {'200%(노출상한) 최대 글자':>24}")
for name, dur in HOSTS:
    n100 = max(n for n in range(0, 60) if reading(n) <= dur) if reading(0) <= dur else -1
    cand = [n for n in range(0, 60) if MAX_VIS_SCALE*reading(n) <= dur]
    n200 = max(cand) if cand else -1
    n100s = "제한 없음(상한 2.20 < 지속)" if reading(59) <= dur else f"{n100}자"
    print(f"    {name:<24} {dur:>9.2f} {n100s:>15} {(str(n200)+'자') if n200>=0 else '0자(불가)':>24}")
print("  ★ 코스튬 «전용 모션»과 «구간 전이»에는 대사를 붙이지 않는다 — 둘 다 상태 전이가 아니다(원칙 1).")
print("  ★ «진화 단계 승급»만이 사실이고, 그 사실을 말할 수 있는 유일한 숙주는 FocusComplete다.")
print("    노출 배율 상한 200%까지 안전하려면 한국어 12자 이하여야 한다(위 표).")

print()
print("="*86)
print("검산 끝 — 위 표에서 '★위반'이 하나라도 있으면 그 키포즈는 폐기 대상이다.")
print("="*86)
