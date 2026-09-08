# -*- coding: utf-8 -*-
"""
코스튬 3종(사이버 · 광부 · 대마법사) 집중모드 키포즈 재검산 — design-motion 2026-09-08

★ 이 스크립트는 2026-09-07 검산(`2026-09-07_코스튬집중_LFVS_프롭기하_검산.py`)을 **한 줄도
   재사용하지 않는다.** TEAM.md 「생성기와 검사기가 같이 틀린다」 규칙 때문이다 — 같은 코드를
   공유하면 같은 함정에 같이 빠져 서로를 확인해 주지 못한다. 여기의 모든 식은
   프로덕션 소스(SceneBootstrapper.cs / StickmanPoseAnimator.cs)를 읽고 **다시 적었다.**

교정(calibration) 3건이 먼저 통과해야 그 뒤 숫자를 믿는다:
  [C1] 리그 재현 : SceneBootstrapper 상수로 유도한 전신 높이 == StickConfig.BaselineCharacterTotalHeight
  [C2] 순 FK    : 사이버 K0 손끝 == 09-07 문서가 적은 (0.2911, 0.7737) H
  [C3] 순 FK    : 광부 K1 손끝 y == 09-07 문서가 적은 1.0653 H
교정이 깨지면 그 뒤 출력은 전부 폐기한다.
"""
import math

# ============================================================================
# 0. 프로덕션에서 읽은 상수 (출처를 각 줄에 적는다 — 손으로 지어낸 값 0개)
# ============================================================================
H_UNITS         = 2.2746944   # StickConfig.cs:2319  BaselineCharacterTotalHeight
ARM_UP, ARM_LO  = 0.38, 0.37  # SceneBootstrapper.cs:257
LEG_UP, LEG_LO  = 0.50, 0.45  # SceneBootstrapper.cs:258
LEG_SPREAD      = 12.0        # SceneBootstrapper.cs:253  IdleLegSpreadDegrees
KNEE_BEND       = 4.0         # SceneBootstrapper.cs:262  IdleKneeBendDegrees
KNEE_SIGN       = -1.0        # SceneBootstrapper.cs:267
SPEC_HIP_Y      = 0.45        # SceneBootstrapper.cs:947
SPEC_SHOULDER_Y = 1.28        # SceneBootstrapper.cs:947
SPEC_TORSO_TOP  = 1.35        # SceneBootstrapper.cs:947
HEAD_R          = 0.22        # SceneBootstrapper.cs:210  BaselineHeadVisualRadius
ARM_STROKE      = 0.10 * 1.045  # SceneBootstrapper.cs:144,138 (BaselineArmLineWidth × LineWidthScale)
PT_PER_UNIT     = 846.0 / (2.0 * 12.0)   # StickConfig.cs:2429 ReferencePointsPerWorldUnitApprox
SHOULDER_BACK, SHOULDER_FWD = -60.0, 150.0  # SceneBootstrapper.cs:344,345
ELBOW_RULE_B_MAX = 116.55     # docs/UX_MOTION_FOCUS_SESSION.md 2절 (무손상 상한)
MAX_LEAN_CODE    = 30.0       # StickmanPoseAnimator.cs:439  MaxBodyLeanDegrees (★ 실제 클램프)
MAX_LEAN_DESIGN  = 7.60       # 설계 상한(선행 문서 1절) — 코드는 이걸 강제하지 않는다
SCALE_SHIP       = 0.75       # StickConfig.cs:2248 characterScale 출하 기본
PROP_STROKE_H    = 0.0339     # design-equipment W_P (EQUIPMENT_SHAPE_SPEC_COSTUME_PROPS 3-3에서 역산)

def limb_drop(hip_deg):
    hip = math.radians(hip_deg)
    knee = math.radians(hip_deg + KNEE_SIGN * KNEE_BEND)
    return LEG_UP * math.cos(hip) + LEG_LO * math.cos(knee)

foot_lift  = max(limb_drop(-LEG_SPREAD), limb_drop(LEG_SPREAD)) - SPEC_HIP_Y
HIP_Y      = SPEC_HIP_Y + foot_lift
SHOULDER_Y = SPEC_SHOULDER_Y + foot_lift
TOTAL_H    = SPEC_TORSO_TOP + foot_lift + 2 * HEAD_R

# H 배수 좌표계로 환산
h_hip, h_sh = HIP_Y / TOTAL_H, SHOULDER_Y / TOTAL_H
h_lu, h_ll  = ARM_UP / TOTAL_H, ARM_LO / TOTAL_H
h_head_y    = (SPEC_TORSO_TOP + foot_lift + HEAD_R) / TOTAL_H
h_head_r    = HEAD_R / TOTAL_H
H_PT        = TOTAL_H * SCALE_SHIP * PT_PER_UNIT          # 1 H가 배율 0.75에서 몇 pt인가
ARM_W_PT    = ARM_STROKE * SCALE_SHIP * PT_PER_UNIT       # 팔 획 두께(pt @0.75)
PROP_W_PT   = PROP_STROKE_H * H_PT

out = []
def p(s=""): out.append(s)

p("=" * 96)
p("코스튬 3종 키포즈 재검산 — design-motion 2026-09-08 (독립 재구현, 09-07 코드 미사용)")
p("=" * 96)
p()
p("[C1] 리그 재현")
p(f"  발끝 보정 footLift = {foot_lift:.7f}")
p(f"  엉덩이 y = {HIP_Y:.7f}유닛 = {h_hip:.6f} H     어깨 y = {SHOULDER_Y:.7f}유닛 = {h_sh:.6f} H")
p(f"  전신 = {TOTAL_H:.7f}유닛   vs StickConfig {H_UNITS:.7f}   차 = {abs(TOTAL_H-H_UNITS):.9f}")
C1 = abs(TOTAL_H - H_UNITS) < 1e-6
p(f"  → {'통과' if C1 else '★ 실패 — 이후 숫자 전부 폐기'}")
p(f"  팔 도달 반지름 = {(ARM_UP+ARM_LO)/TOTAL_H:.6f} H,  1 H @0.75 = {H_PT:.2f} pt,  팔 획 = {ARM_W_PT:.2f} pt")
p()

# ============================================================================
# 1. 순 FK / 실제 FK
#    각도 규약: ψ = 0이 «바로 아래», + = 진행 방향. 방향벡터 = (sinψ, −cosψ)
#    실제 = 어깨 부착점이 (a) 엉덩이 축 lean 회전 (b) bodyOffsetY 평행이동을 받는다
#           — StickmanPoseAnimator.ApplyAngle: pivot = LeanedLocal(PivotLocal), +_bodyOffsetY
#           팔 «각도»는 lean을 안 받는다(TickBodyLean 문서: "부착점만 함께 돌린다")
# ============================================================================
def fk_offset(tu, tl):
    """어깨 → 손끝 벡터(H). lean/dY와 무관한 순수 팔 기하."""
    a, b = math.radians(tu), math.radians(tu + tl)
    return (h_lu*math.sin(a) + h_ll*math.sin(b), -(h_lu*math.cos(a) + h_ll*math.cos(b)))

def shoulder_actual(lean, dy):
    """실제 어깨 부착점 — Rz(−lean)로 엉덩이 축 회전 후 dY 평행이동."""
    d = h_sh - h_hip
    L = math.radians(lean)
    return (d*math.sin(L), h_hip + d*math.cos(L) + dy)

def hand_bare(tu, tl):
    o = fk_offset(tu, tl); return (o[0], h_sh + o[1])

def hand_actual(tu, tl, lean, dy):
    s = shoulder_actual(lean, dy); o = fk_offset(tu, tl); return (s[0]+o[0], s[1]+o[1])

cyber_K0_A = hand_bare(62.0, 56.0)
miner_K1_A = hand_bare(142.0, 52.0)
p("[C2] 순 FK — 사이버 K0 A팔 손끝")
p(f"  계산 ({cyber_K0_A[0]:.4f}, {cyber_K0_A[1]:.4f}) H   09-07 문서 (0.2911, 0.7737) H")
C2 = abs(cyber_K0_A[0]-0.2911) < 5e-4 and abs(cyber_K0_A[1]-0.7737) < 5e-4
p(f"  → {'통과' if C2 else '★ 실패'}")
p("[C3] 순 FK — 광부 K1 A팔 손끝 높이")
p(f"  계산 {miner_K1_A[1]:.4f} H   09-07 문서 1.0653 H  → {'통과' if abs(miner_K1_A[1]-1.0653)<5e-4 else '★ 실패'}")
C3 = abs(miner_K1_A[1]-1.0653) < 5e-4
p()
if not (C1 and C2 and C3):
    p("★ 교정 실패 — 아래 숫자를 쓰지 마라."); print("\n".join(out)); raise SystemExit(1)

# ============================================================================
# 2. 키포즈 표 (09-07 설계값 그대로 — 자산 필드 순서로 옮겨 적었다)
#    (leftUpper, leftLower, rightUpper, rightLower, lean, dY, propFrame)
#    ★ A(앞팔) = right*,  B(뒷팔) = left*   ← CostumeKeypose.cs 39~41행 규약
# ============================================================================
TABLES = {
 "costume.cyber": dict(steps=3, keys=[
    ("K0 대기",      48.0, 68.0, 62.0, 56.0, -2.0,  0.000, 0),
    ("K1 좌측입력",  44.0, 72.0, 80.0, 36.5, -1.0,  0.000, 0),
    ("K2 우측입력",  63.0, 34.5, 58.0, 60.0, -1.0,  0.000, 0),
    ("K3 판독끄덕",  56.0, 58.0, 70.0, 44.0,  2.0, -0.018, 0)]),
 "costume.miner": dict(steps=3, keys=[
    ("K0 겨눔",     -24.0, 28.0, 86.0, 34.0, -1.0,  0.000, 0),
    ("K1 치켜듦",   -30.0, 24.0,142.0, 52.0, -4.0,  0.018, 0),
    ("K2 내리침",   -18.0, 34.0, 76.5, 57.0,  5.0, -0.030, 1),
    ("K3 훑기",     -20.0, 30.0, 45.0, 43.0,  2.0, -0.020, 0)]),
 "costume.archmage": dict(steps=3, keys=[
    ("K0 영창준비", -34.0, 96.0,-30.0, 92.0, -2.0,  0.000, 0),
    ("K1 지목A",    -32.0, 92.0,-34.0, 84.0,  1.5,  0.000, 0),
    ("K2 지목B",    -40.0, 96.0,-26.0, 88.0,  1.0,  0.000, 0),
    ("K3 끌어올림", -28.0, 88.0, 16.0, 84.0, -3.0,  0.020, 0)]),
 "costume.office(출하중)": dict(steps=3, keys=[
    ("K0 대기",     -26.0, 22.0, 46.0, 66.0, -2.0,  0.000, 0),
    ("K1 상판짚기", -26.0, 22.0, 47.5, 27.5,  2.5, -0.018, 0),
    ("K2 넘기기",   -22.0, 26.0, 40.0, 78.0,  1.0,  0.000, 0)]),
}

# ============================================================================
# 3. 프롭 잉크 (design-equipment `EQUIPMENT_SHAPE_SPEC_COSTUME_PROPS.md` 3절 S1 기준)
#    ★ 09-07 모션 초안의 좌표가 아니라 **착지한 조형 사양**을 옮겼다.
# ============================================================================
def ell_arc(cx, cy, rx, ry, n):
    return [(cx + rx*math.cos(math.radians(t)), cy + ry*math.sin(math.radians(t)))
            for t in [180.0 - 180.0*i/(n-1) for i in range(n)]]

PROPS = {
 "costume.cyber": {
   "Mast":     [(0.54,0.09),(0.54,0.62)],
   "BasePad":  [(0.44,0.03),(0.47,0.00),(0.59,0.00),(0.62,0.03),(0.62,0.09),(0.59,0.12),(0.47,0.12),(0.44,0.09),(0.44,0.03)],
   "PanelRim": [(0.31,0.62),(0.72,0.62),(0.77,0.67),(0.77,0.95),(0.72,1.00),(0.31,1.00),(0.31,0.62)],
   "ScanTop":  [(0.41,0.9050),(0.67,0.9050)],
   "ScanMid":  [(0.41,0.8100),(0.63,0.8100)],
   "ScanLow":  [(0.41,0.7150),(0.67,0.7150)]},
 "costume.miner": {
   "RockFace": [(0.28,0.00),(0.30,0.20),(0.28,0.44),(0.28,0.65),(0.32,0.76),(0.28,0.85),(0.38,0.94),(0.60,0.90),(0.76,0.56),(0.76,0.00)],
   "GroundSeam":[(0.10,0.00),(0.80,0.00)],
   "Vein1":    [(0.4809,0.7718),(0.5512,0.5932),(0.5088,0.5668),(0.3791,0.7082),(0.4809,0.7718)],
   "TimberL":  [(0.38,0.00),(0.38,0.42)],
   "TimberR":  [(0.68,0.00),(0.68,0.42)],
   "TimberCap":[(0.38,0.42),(0.68,0.42)],
   "Chip1":    [(0.10,0.00),(0.135,0.052),(0.17,0.00)],
   "Vein2":    [(0.61,0.67),(0.65,0.59),(0.63,0.53)]},
 "costume.archmage": {
   "OuterArc": ell_arc(0.58,0.02,0.30,0.24,15),
   "MidArc":   ell_arc(0.58,0.02,0.21,0.14,13),
   "Staff":    [(1.04,0.02),(1.04,0.90)],
   "CradleA":  [(0.96,0.02),(1.12,0.26)],
   "CradleB":  [(1.12,0.02),(0.96,0.26)]},
 "costume.office(출하중)": {
   "Partition":[(0.72,0.16),(1.00,0.16),(1.00,0.88),(0.72,0.88),(0.72,0.16)],
   "DeskTop":  [(0.385,0.62),(0.72,0.62)],
   "DeskLeg":  [(0.32,0.00),(0.32,0.552)],
   "DeskLip":  [(0.28,0.552),(0.385,0.552),(0.385,0.62),(0.28,0.62),(0.28,0.552)],
   "LampPost": [(0.62,0.62),(0.62,0.90)],
   "LampHood": [(0.62,0.90),(0.46,0.82)],
   "BeamNear": [(0.46,0.82),(0.42,0.72)],
   "BeamFar":  [(0.54,0.86),(0.54,0.76)],
   "GrooveA":  [(0.80,0.24),(0.80,0.56)],
   "GrooveB":  [(0.90,0.24),(0.90,0.56)]},
}

def seg_dist(pt, a, b):
    px,py = pt; ax,ay = a; bx,by = b
    dx,dy = bx-ax, by-ay
    L2 = dx*dx+dy*dy
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((px-ax)*dx + (py-ay)*dy)/L2))
    return math.hypot(px-(ax+t*dx), py-(ay+t*dy))

def nearest_ink(costume, pt):
    best, name = 1e9, "-"
    for nm, poly in PROPS[costume].items():
        for i in range(len(poly)-1):
            d = seg_dist(pt, poly[i], poly[i+1])
            if d < best: best, name = d, nm
    return best, name

# 「닿았다」로 읽히는 문턱 = 팔 획 반두께 + 프롭 획 반두께
TOUCH_PT = 0.5*ARM_W_PT + 0.5*PROP_W_PT

p("=" * 96)
p("1. 손끝 실좌표 — 순 FK vs **lean·dY를 실제로 반영한 값**")
p("=" * 96)
p(f"  「닿았다」문턱 = 팔 획 반 {0.5*ARM_W_PT:.2f}pt + 프롭 획 반 {0.5*PROP_W_PT:.2f}pt = {TOUCH_PT:.2f} pt @0.75")
p()
CONTACT = {   # (코스튬, 키인덱스, 어느 팔) : 접촉 의도
  ("costume.cyber",1,"A"): "패널 근단 세로변",
  ("costume.cyber",2,"B"): "패널 근단 세로변",
  ("costume.miner",2,"A"): "벽 꼭짓점 (0.28,0.85)",
  ("costume.miner",3,"A"): "벽 꼭짓점 (0.28,0.65)",
  ("costume.office(출하중)",1,"A"): "DeskLip 좌상단 (0.28,0.62)",
}
findings = []
for cos, t in TABLES.items():
    p(f"── {cos}  (스텝레이트 {t['steps']} fps · {len(t['keys'])}키)")
    p(f"{'키':<14}{'팔':<3}{'순FK(x,y)':>20}{'실제(x,y)':>20}{'이동':>9}{'프롭까지':>11}  비고")
    for i,(nm,lu,ll,ru,rl,lean,dy,pf) in enumerate(t["keys"]):
        for arm,(tu,tl) in (("A",(ru,rl)), ("B",(lu,ll))):
            hb = hand_bare(tu,tl); ha = hand_actual(tu,tl,lean,dy)
            shift = math.hypot(ha[0]-hb[0], ha[1]-hb[1]) * H_PT
            d,nmink = nearest_ink(cos, ha)
            tag = CONTACT.get((cos,i,arm), "")
            mark = ""
            if tag:
                mark = f"★{tag} → 최근접 {nmink} {d*H_PT:.2f}pt " + ("[닿음]" if d*H_PT <= TOUCH_PT else "[벌어짐]")
                findings.append((cos,nm,arm,tag,d*H_PT,shift))
            p(f"{nm:<14}{arm:<3}({hb[0]:7.4f},{hb[1]:7.4f}){'':>3}({ha[0]:7.4f},{ha[1]:7.4f}){shift:8.2f}pt{d*H_PT:10.2f}pt  {mark}")
    p()

p("=" * 96)
p("2. ★ 접촉 의도가 있는 프레임만 — 09-07 표의 「접촉오차」와 대조")
p("=" * 96)
p(f"{'코스튬':<24}{'키':<14}{'09-07 선언':>12}{'실제 간극':>12}{'lean·dY가 민 거리':>18}  판정")
DECLARED = {("costume.cyber","K1 좌측입력"):0.04, ("costume.cyber","K2 우측입력"):0.07,
            ("costume.miner","K2 내리침"):0.08, ("costume.miner","K3 훑기"):0.13,
            ("costume.office(출하중)","K1 상판짚기"):0.05}
for cos,nm,arm,tag,gap,shift in findings:
    dec = DECLARED.get((cos,nm), float('nan'))
    p(f"{cos:<24}{nm:<14}{dec:11.2f}pt{gap:11.2f}pt{shift:17.2f}pt  "
      f"{'닿음' if gap<=TOUCH_PT else '★ 벌어짐(재해 필요)'}")
p()
# ============================================================================
# 4. 재해(re-solve) — lean·dY가 실린 **실제 어깨**에서 목표점으로 2링크 IK
#    (프로덕션 SolveTwoLinkIk를 부르지 않는다. 코사인법칙을 여기서 다시 적는다)
# ============================================================================
def ik(target, lean, dy):
    sx, sy = shoulder_actual(lean, dy)
    vx, vy = target[0]-sx, target[1]-sy
    d = math.hypot(vx, vy)
    reach = h_lu + h_ll
    if d > reach: return None, None, d
    c = (d*d - h_lu*h_lu - h_ll*h_ll) / (2*h_lu*h_ll)
    tl = math.degrees(math.acos(max(-1.0, min(1.0, c))))
    psi = math.degrees(math.atan2(vx, -vy))
    a = math.degrees(math.atan2(h_ll*math.sin(math.radians(tl)),
                                h_lu + h_ll*math.cos(math.radians(tl))))
    return psi - a, tl, d

p("=" * 96)
p("3. 재해 — 실제 어깨(lean·dY 반영)에서 목표점으로 다시 푼 각도")
p("=" * 96)
RESOLVE = [
 ("costume.cyber","K1 좌측입력","A",(0.31,0.82),-1.0,0.000),
 ("costume.cyber","K2 우측입력","B",(0.31,0.72),-1.0,0.000),
 ("costume.miner","K2 내리침","A",(0.28,0.85), 5.0,-0.030),
 ("costume.miner","K3 훑기","A",(0.28,0.65), 2.0,-0.020),
 ("costume.office(출하중)","K1 상판짚기","A",(0.28,0.62), 2.5,-0.018),
]
OLD = {("costume.cyber","K1 좌측입력"):(80.0,36.5), ("costume.cyber","K2 우측입력"):(63.0,34.5),
       ("costume.miner","K2 내리침"):(76.5,57.0), ("costume.miner","K3 훑기"):(45.0,43.0),
       ("costume.office(출하중)","K1 상판짚기"):(47.5,27.5)}
p(f"{'코스튬':<24}{'키':<14}{'기존 θu/θl':>16}{'재해 θu/θl':>16}{'Δθu':>8}{'Δθl':>8}  규칙B/어깨한계")
for cos,nm,arm,tgt,lean,dy in RESOLVE:
    tu, tl, d = ik(tgt, lean, dy)
    ou, ol = OLD[(cos,nm)]
    ok = (tl is not None and tl <= ELBOW_RULE_B_MAX and SHOULDER_BACK <= tu <= SHOULDER_FWD)
    p(f"{cos:<24}{nm:<14}{ou:7.1f}/{ol:<8.1f}{tu:7.2f}/{tl:<8.2f}{tu-ou:8.2f}{tl-ol:8.2f}  "
      f"{'통과' if ok else '★위반'} (팔꿈치 {tl:.1f}≤{ELBOW_RULE_B_MAX}, 어깨 {tu:.1f}∈[{SHOULDER_BACK},{SHOULDER_FWD}])")
    # 재해값이 실제로 목표에 닿는지 되짚는다(양성 대조)
    h = hand_actual(tu, tl, lean, dy)
    p(f"{'':<38}되짚기 손끝 ({h[0]:.4f},{h[1]:.4f}) → 목표까지 "
      f"{math.hypot(h[0]-tgt[0],h[1]-tgt[1])*H_PT:.3f} pt")
p()

# ============================================================================
# 5. 대마법사 — 지향 판정을 **착지한 반타원**으로 다시 세운다
#    09-07은 「눕힌 타원」 전제로 «지면 착점 x가 [0.26,0.94]인가»를 봤다.
#    조형이 「지면 위 반타원 3겹」으로 바뀌었으므로 그 잣대는 낡았다 —
#    새 잣대: 전완 반직선이 바깥 반타원과 **실제로 만나는가**.
# ============================================================================
p("=" * 96)
p("4. 대마법사 지향 — 반직선 × 바깥 반타원 교점")
p("=" * 96)
CX, CY, RX, RY = 0.58, 0.02, 0.30, 0.24
def ray_hits_arc(hand, psi_deg):
    dx, dy = math.sin(math.radians(psi_deg)), -math.cos(math.radians(psi_deg))
    # ((x-cx)/rx)^2 + ((y-cy)/ry)^2 = 1  에 대입
    ax, ay = (hand[0]-CX)/RX, (hand[1]-CY)/RY
    bx, by = dx/RX, dy/RY
    A = bx*bx + by*by; B = 2*(ax*bx + ay*by); C = ax*ax + ay*ay - 1
    disc = B*B - 4*A*C
    if disc < 0: return None
    hits = []
    for s in ((-B - math.sqrt(disc))/(2*A), (-B + math.sqrt(disc))/(2*A)):
        if s <= 0: continue
        x, y = hand[0]+s*dx, hand[1]+s*dy
        if y >= CY - 1e-9: hits.append((x, y, s))   # 위쪽 반원만 잉크가 있다
    return hits[0] if hits else None

for i,(nm,lu,ll,ru,rl,lean,dy,pf) in enumerate(TABLES["costume.archmage"]["keys"]):
    for arm,(tu,tl) in (("A",(ru,rl)), ("B",(lu,ll))):
        if not ((i==1 and arm=="A") or (i==2 and arm=="B")): continue
        hand = hand_actual(tu,tl,lean,dy)
        psi = tu + tl
        hit = ray_hits_arc(hand, psi)
        gx = hand[0] + (hand[1]/math.cos(math.radians(psi))*math.sin(math.radians(psi))) if math.cos(math.radians(psi))>0 else float('nan')
        p(f"  {nm} {arm}팔  손끝({hand[0]:.4f},{hand[1]:.4f})  전완 절대각 {psi:.1f}°")
        p(f"    지면(y=0) 착점 x = {gx:.4f} H     (09-07 선언: K1 0.6423 / K2 0.8234)")
        if hit: p(f"    ★ 바깥 반타원과 만난다 → 교점 ({hit[0]:.4f}, {hit[1]:.4f}) H — 손이 마법진 자체를 겨눈다")
        else:   p(f"    ★ 반타원과 안 만난다 — 손이 마법진 «너머 바닥»을 가리킨다")
p()

# ============================================================================
# 6. 박자 · 성능 — CostumeFocusRhythm 상수를 그대로 다시 적어 유도
# ============================================================================
SUB, REST_FPS, PEAK_FPS = 3, 3, 5
DUTY = (1/3, 1/2, 1/6)
MINP, MAXP = 4.0, 8.0
def edge(D): return min(max(min(0.20*D, 300.0), 60.0), 0.40*D)
def loops(period, L, duty): return max(1, math.floor(duty*period/L + 0.5))
p("=" * 96)
p("5. 박자·성능 — 25분 세션(D=1500초)")
p("=" * 96)
D = 1500.0
E = edge(D); IM = D - 2*E; sub = IM/SUB
period = min(max(IM/SUB/SUB, MINP), MAXP)
p(f"  적응기 {E:.1f}s / 몰입기 {IM:.1f}s / 한계 {E:.1f}s,  소구간 {sub:.1f}s,  듀티 주기 {period:.1f}s")
p()
p(f"{'키수':<6}{'소구간':<8}{'fps':>4}{'루프길이':>10}{'루프':>6}{'W(초)':>9}{'R(초)':>9}{'듀티':>8}{'스텝/주기':>10}{'쓰기/초':>10}")
summary = {}
for keys in (4, 3):
    tot = 0.0
    for s in range(SUB):
        fps = PEAK_FPS if s == 1 else REST_FPS
        L = keys/fps
        n = loops(period, L, DUTY[s]); W = min(n*L, period); R = period-W
        steps = W*fps
        writes = (steps + 1)*10/period      # +1 = 작업 끝에 정지 키포즈(K0)로 돌아가는 쓰기
        tot += writes
        p(f"{keys:<6}{['I-a진입','I-b절정','I-c이완'][s]:<8}{fps:>4}{L:>10.4f}{n:>6}{W:>9.4f}{R:>9.4f}"
          f"{W/period*100:>7.1f}%{steps:>10.0f}{writes:>10.2f}")
    summary[keys] = tot/SUB
    p(f"{'':6}{'평균':<8}{'':>4}{'':>10}{'':>6}{'':>9}{'':>9}{'':>8}{'':>10}{summary[keys]:>10.2f}")
p()
p(f"  4키(신규 3종) 평균 {summary[4]:.3f} 쓰기/초   vs   3키(출하 오피스) {summary[3]:.3f} 쓰기/초")
p(f"  → 신규 3종이 출하 오피스보다 {100*(1-summary[4]/summary[3]):.2f}% 가볍다")
p(f"  → 관망 자세(60fps × 10 = 600 쓰기/초) 대비 몰입기 {100*(1-summary[4]/600):.2f}% 절감")
p(f"     (09-07 문서의 −97.8%는 「정지 키포즈 복귀 1회」를 안 센 값이다)")
p()
p("  세션 전체(코스튬은 몰입기에만 산다):")
full = (IM*summary[4] + (D-IM)*600)/D
p(f"    (몰입기 {IM:.0f}s × {summary[4]:.2f} + 나머지 {D-IM:.0f}s × 600) / {D:.0f} = {full:.1f} 쓰기/초 "
  f"→ 전체 {100*(1-full/600):.1f}% 절감")
p()

# ============================================================================
# 7. 무게감 — 광부 타격의 실제 낙차(lean·dY 포함)
# ============================================================================
p("=" * 96)
p("6. 광부 타격 무게감 — K1 → K2 한 스텝의 실제 낙차")
p("=" * 96)
k1 = TABLES["costume.miner"]["keys"][1]; k2 = TABLES["costume.miner"]["keys"][2]
h1 = hand_actual(k1[3],k1[4],k1[5],k1[6]); h2 = hand_actual(k2[3],k2[4],k2[5],k2[6])
s1 = shoulder_actual(k1[5],k1[6]); s2 = shoulder_actual(k2[5],k2[6])
p(f"  손끝  K1 ({h1[0]:.4f},{h1[1]:.4f}) → K2 ({h2[0]:.4f},{h2[1]:.4f})   "
  f"세로 낙차 {abs(h1[1]-h2[1]):.4f} H = {abs(h1[1]-h2[1])*H_PT:.2f} pt @0.75")
p(f"  어깨  K1 ({s1[0]:.4f},{s1[1]:.4f}) → K2 ({s2[0]:.4f},{s2[1]:.4f})   "
  f"세로 {abs(s1[1]-s2[1])*H_PT:.2f} pt · 가로 {abs(s2[0]-s1[0])*H_PT:.2f} pt")
p(f"  기울임 반전 {k1[5]:.1f}° → {k2[5]:.1f}° = {abs(k2[5]-k1[5]):.1f}°")
p(f"  ★ 한 스텝(3fps = 0.3333초)에 보간 없이 통째로 일어난다")
p()
p("=" * 96)
p("7. 전 키포즈 안전 점검")
p("=" * 96)
worst_elbow = 0; worst_lean = 0
for cos,t in TABLES.items():
    for nm,lu,ll,ru,rl,lean,dy,pf in t["keys"]:
        for arm,(tu,tl) in (("A",(ru,rl)),("B",(lu,ll))):
            assert SHOULDER_BACK <= tu <= SHOULDER_FWD, f"{cos} {nm} {arm} 어깨 {tu} 한계 밖"
            worst_elbow = max(worst_elbow, tl)
        worst_lean = max(worst_lean, abs(lean)+1.5)   # I-c 이완 바이어스 포함
        assert abs(dy) <= 0.032 or dy == 0.0, f"{cos} {nm} dY {dy} 대역 밖"
p(f"  어깨각 전수: 전부 [{SHOULDER_BACK}, {SHOULDER_FWD}] 안 (최대 142.0 = 광부 K1)")
p(f"  팔꿈치 최댓값 {worst_elbow:.1f}° ≤ 규칙B 무손상 상한 {ELBOW_RULE_B_MAX}° → 여유 {ELBOW_RULE_B_MAX-worst_elbow:.2f}°")
p(f"  기울임 최댓값(이완 바이어스 +1.5 포함) {worst_lean:.1f}° ≤ 설계 상한 {MAX_LEAN_DESIGN}°")
p(f"  ★ 단 코드 클램프는 {MAX_LEAN_CODE}°다(StickmanPoseAnimator:439) — 설계 상한 {MAX_LEAN_DESIGN}°를 "
  f"코드가 강제하지 않는다.")
p(f"    CostumeKeypose.leanDegrees 툴팁은 «MaxBodyLeanDegrees(7.60)로 클램프된다»고 적혀 있는데 "
  f"실제 상수는 {MAX_LEAN_CODE:.0f}이다 → 문서 결함 1건(에셋 작성자가 20°를 적으면 그대로 들어간다).")
p(f"  dY 전수: |dY| ≤ 0.032 H (설계 대역 0.017~0.032)")
p()
print("\n".join(out))
