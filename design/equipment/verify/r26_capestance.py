# -*- coding: utf-8 -*-
"""R26 — 집중 세션 «뒷짐(P2)» 자세 × 망토 2종 기하 충돌 실측.

배정(리더, 2026-09-06): 「뒷짐 자세가 망토와 겹칠 가능성」 크로스체크.

이 스크립트가 **프로덕션에서 그대로 옮겨오는 것**(사본을 만들지 않는다):
  · 망토 좌표      : Assets/.../Interaction/AccessoryShapeBuilder.Handoff.cs 를 **직접 파싱**
  · 팔 각도        : States/StickmanPoseAnimator.cs 의 상수를 **직접 파싱**
  · 팔 폴리라인    : States/LimbCurveRenderer.SolveFilletLength / FillArcs 를 같은 식으로 옮김
  · 리그 치수      : Assets/Editor/SceneBootstrapper.cs 의 Baseline* 상수

좌표계: 머리 중심 원점 · +x = 진행 방향 · 단위 = 머리 반경 R(= 0.22 × 배율).
        (design/equipment/verify/rig.py 와 같은 규약)
"""
import math, os, re, sys

ROOT = "/Users/kjmoon/App/StickMate"
HANDOFF = os.path.join(ROOT, "Assets/_Project/Scripts/Interaction/AccessoryShapeBuilder.Handoff.cs")
POSE    = os.path.join(ROOT, "Assets/_Project/Scripts/States/StickmanPoseAnimator.cs")
BOOT    = os.path.join(ROOT, "Assets/Editor/SceneBootstrapper.cs")
CONFIG  = os.path.join(ROOT, "Assets/_Project/Scripts/Core/StickConfig.cs")
CURVE   = os.path.join(ROOT, "Assets/_Project/Scripts/States/LimbCurveRenderer.cs")

def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()

# ────────────────────────────────────────────────────────────────────────────────
# 0. 프로덕션 상수 추출 (베껴 적지 않는다 — 파일에서 읽는다)
# ────────────────────────────────────────────────────────────────────────────────
def const_f(src, name):
    m = re.search(r"const\s+float\s+%s\s*=\s*(-?[\d.]+)f" % re.escape(name), src)
    if not m: raise SystemExit("상수 못 찾음: " + name)
    return float(m.group(1))

def field_f(src, name):
    m = re.search(r"public\s+float\s+%s\s*=\s*(-?[\d.]+)f" % re.escape(name), src)
    if not m: raise SystemExit("필드 못 찾음: " + name)
    return float(m.group(1))

pose_src, boot_src, cfg_src, curve_src = read(POSE), read(BOOT), read(CONFIG), read(CURVE)

BACK_UPPER   = const_f(pose_src, "FocusWatchBackArmUpperDegrees")     # -51.5
BACK_ELBOW   = const_f(pose_src, "FocusWatchBackElbowDegrees")        #  72.5
BACK_SH_STAG = const_f(pose_src, "FocusWatchBackShoulderStaggerDegrees")  # 4
BACK_EL_STAG = const_f(pose_src, "FocusWatchBackElbowStaggerDegrees")     # 6
CROSS_UPPER  = const_f(pose_src, "FocusCrossArmUpperDegrees")
CROSS_ELBOW  = const_f(pose_src, "FocusCrossElbowDegrees")
CROSS_SH_STG = const_f(pose_src, "FocusCrossShoulderStaggerDegrees")
CROSS_EL_STG = const_f(pose_src, "FocusCrossElbowStaggerDegrees")
LEAN_BACK    = const_f(pose_src, "FocusStanceBackLeanDegrees")        # -2.5
LEAN_CROSS   = const_f(pose_src, "FocusStanceCrossLeanDegrees")       # -4
LEAN_SWAY    = const_f(pose_src, "FocusStanceLeanSwayDegrees")        # 2
SH_SWAY      = const_f(pose_src, "FocusStanceShoulderSwayDegrees")    # 1.5
ELBOW_SIGN   = const_f(pose_src, "ElbowBendSign")                     # +1

ARM_SPREAD   = field_f(cfg_src, "idleArmSpreadDegrees")               # 40
IDLE_ELBOW   = field_f(cfg_src, "idleElbowBendDegrees")               # 10
BREATH_ARM   = field_f(cfg_src, "idleBreathArmDegrees")               # 1.5

FILLET_RATIO = const_f(curve_src, "FilletLengthRatio")                # 0.42
MAX_SAG_W    = const_f(curve_src, "MaxSagittaPerStrokeWidth")         # 1.0
ARC_SAMPLES  = int(re.search(r"const\s+int\s+ArcSamplesPerHalf\s*=\s*(\d+)", curve_src).group(1))
MIN_HALF_RAD = const_f(curve_src, "MinHalfAngleRadians")

LINE_W_SCALE = const_f(boot_src, "LineWidthScale")                    # 1.045
ARM_LINE_W   = float(re.search(r"BaselineArmLineWidth\s*=\s*([\d.]+)f", boot_src).group(1)) * LINE_W_SCALE
UPPER_LEN    = float(re.search(r"BaselineArmUpperLength\s*=\s*([\d.]+)f", boot_src).group(1))
LOWER_LEN    = float(re.search(r"BaselineArmLowerLength\s*=\s*([\d.]+)f", boot_src).group(1))
HEAD_R_BASE  = const_f(boot_src, "BaselineHeadVisualRadius")          # 0.22

# 리그(배율 1.0 실측 — design/equipment/verify/rig.py 와 같은 값)
BASE_TOTAL_H  = 2.2746944
BASE_SHOULDER = 1.7646944
BASE_HIP      = 0.9346944
HEAD_CENTER   = BASE_TOTAL_H - HEAD_R_BASE
R             = HEAD_R_BASE
SHOULDER_R    = (BASE_SHOULDER - HEAD_CENTER) / R      # -1.31818
HIP_R         = (BASE_HIP      - HEAD_CENTER) / R      # -5.09091
FOOT_R        = (0.0           - HEAD_CENTER) / R      # -9.33952
UPPER_R       = UPPER_LEN / R                          #  1.72727
LOWER_R       = LOWER_LEN / R                          #  1.68182

PT_PER_UNIT   = 846.0 / 24.0                           # 35.25
MIN_STROKE_PT = 2.0

def arm_width_R(scale):
    """그려지는 팔 획 두께(R 배수). 화면상 하한 2pt가 걸리는 배율에서 R 대비로 두꺼워진다."""
    world = max(ARM_LINE_W * scale, MIN_STROKE_PT / PT_PER_UNIT)
    return world / (R * scale)

def R_to_pt(scale):
    return R * scale * PT_PER_UNIT

# ────────────────────────────────────────────────────────────────────────────────
# 1. 망토 좌표 파싱 (몸 프레임 = bodyFixed 조각)
# ────────────────────────────────────────────────────────────────────────────────
hs = read(HANDOFF)
def handoff_poly(name):
    m = re.search(r"float\[\]\s+%s\s*=\s*\{(.*?)\};" % re.escape(name), hs, re.S)
    if not m: raise SystemExit("좌표 배열 못 찾음: " + name)
    v = [float(x) for x in re.findall(r"(-?[\d.]+)f", m.group(1))]
    return [(v[i], v[i+1]) for i in range(0, len(v), 2)]

CAPES = {
    "짧은망토": {"back": handoff_poly("Handoff_shortcape_STB"),
                 "collar": handoff_poly("Handoff_shortcape_STC"),
                 "clasp": handoff_poly("Handoff_shortcape_STK")},
    "긴망토":   {"back": handoff_poly("Handoff_longcape_STB"),
                 "collar": handoff_poly("Handoff_longcape_STC"),
                 "clasp": handoff_poly("Handoff_longcape_STK")},
}

# ────────────────────────────────────────────────────────────────────────────────
# 2. 팔 폴리라인 (LimbCurveRenderer 와 같은 식)
# ────────────────────────────────────────────────────────────────────────────────
def solve_fillet(lu, ll, ang, w):
    half = 0.5 * abs(ang) * math.pi / 180.0
    t = FILLET_RATIO * min(lu, ll)
    if half > MIN_HALF_RAD and w > 0:
        sag = t * math.tan(0.5 * half)
        cap = MAX_SAG_W * w
        if sag > cap: t *= cap / sag
    return t

def limb_polyline(lu, ll, ang, w):
    """위 마디 로컬(뿌리 원점, 위 마디가 −Y). LimbCurveRenderer.BuildLimbPolyline 과 같은 결과."""
    n = ARC_SAMPLES
    t = solve_fillet(lu, ll, ang, w)
    half = 0.5 * abs(ang) * math.pi / 180.0
    sign = 1.0 if ang >= 0 else -1.0
    up = [(0.0, 0.0)]
    lo = [None] * n + [(0.0, -ll)]
    us = lu - t
    if half <= MIN_HALF_RAD:
        for k in range(n):
            u = k / (n - 1.0)
            up.append((0.0, -(us + t * u)))
            lo[n - 1 - k] = (0.0, -t + t * u)
    else:
        rad = t / math.tan(half)
        for k in range(n):
            phi = half * (k / (n - 1.0))
            x = sign * rad * (1 - math.cos(phi)); y = rad * math.sin(phi)
            up.append((x, -us - y))
            lo[n - 1 - k] = (x, -t + y)
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    lo2 = [(p[0]*c - p[1]*s, p[0]*s + p[1]*c - lu) for p in lo]
    return up + lo2[1:]      # 관절점은 한 번만

def arm_points(upper_deg, elbow_deg, w, shoulder=(0.0, SHOULDER_R)):
    """루트/컨테이너 프레임의 팔 폴리라인. 각도 규약: 끝 방향 = (sinθ, −cosθ)."""
    pts = limb_polyline(UPPER_R, LOWER_R, elbow_deg, w)
    a = math.radians(upper_deg)
    # 위 마디 로컬 → 방향중립 루트: 로컬 (x,y) 는 θ 회전(z) 뒤 어깨로 평행이동.
    c, s = math.cos(a), math.sin(a)
    return [(shoulder[0] + p[0]*c - p[1]*s, shoulder[1] + p[0]*s + p[1]*c) for p in pts]

# ────────────────────────────────────────────────────────────────────────────────
# 3. 기하 유틸
# ────────────────────────────────────────────────────────────────────────────────
def contains(poly, q):
    inside = False; n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i+1) % n]
        if (a[1] > q[1]) != (b[1] > q[1]):
            x = a[0] + (q[1]-a[1]) * (b[0]-a[0]) / (b[1]-a[1])
            if q[0] < x: inside = not inside
    return inside

def dist_pt_seg(p, a, b):
    dx, dy = b[0]-a[0], b[1]-a[1]; L = dx*dx + dy*dy
    t = 0.0 if L < 1e-15 else max(0.0, min(1.0, ((p[0]-a[0])*dx + (p[1]-a[1])*dy) / L))
    return math.hypot(p[0]-(a[0]+dx*t), p[1]-(a[1]+dy*t))

def dist_to_boundary(poly, q):
    n = len(poly)
    return min(dist_pt_seg(q, poly[i], poly[(i+1) % n]) for i in range(n))

def dist_pt_polyline(p, line):
    return min(dist_pt_seg(p, line[i], line[i+1]) for i in range(len(line)-1))

def polyline_length(line):
    return sum(math.dist(line[i], line[i+1]) for i in range(len(line)-1))

def densify(line, step=0.004):
    out = []
    for i in range(len(line)-1):
        a, b = line[i], line[i+1]
        L = math.dist(a, b); n = max(1, int(L/step))
        for k in range(n):
            u = k/float(n)
            out.append((a[0]+(b[0]-a[0])*u, a[1]+(b[1]-a[1])*u))
    out.append(line[-1]); return out

def inside_run_from_tip(line, poly, step=0.004):
    """끝점에서 거슬러 올라가며 «폴리곤 안에 연속으로 잠긴 중심선 길이»(R). 끝점이 밖이면 0."""
    d = densify(line, step)
    if not contains(poly, d[-1]): return 0.0
    run = 0.0
    for i in range(len(d)-1, 0, -1):
        if not contains(poly, d[i-1]): break
        run += math.dist(d[i], d[i-1])
    return run

def centerline_inside_length(line, poly, step=0.004):
    d = densify(line, step); tot = 0.0
    for i in range(len(d)-1):
        mid = (0.5*(d[i][0]+d[i+1][0]), 0.5*(d[i][1]+d[i+1][1]))
        if contains(poly, mid): tot += math.dist(d[i], d[i+1])
    return tot

def ink_overlap_area(line, half_w, poly, minus=(), step=0.02):
    """팔 잉크(폴리라인 ⊕ 반경 half_w) ∩ poly 의 면적(R²). minus 폴리곤들에 덮인 부분은 뺀다."""
    xs = [p[0] for p in poly]; ys = [p[1] for p in poly]
    lx = [p[0] for p in line]; ly = [p[1] for p in line]
    x0 = max(min(xs), min(lx)-half_w); x1 = min(max(xs), max(lx)+half_w)
    y0 = max(min(ys), min(ly)-half_w); y1 = min(max(ys), max(ly)+half_w)
    if x1 <= x0 or y1 <= y0: return 0.0
    n = 0; x = x0 + step*0.5
    while x < x1:
        y = y0 + step*0.5
        while y < y1:
            q = (x, y)
            if dist_pt_polyline(q, line) <= half_w and contains(poly, q) \
               and not any(contains(m, q) for m in minus):
                n += 1
            y += step
        x += step
    return n * step * step

def ink_clearance(line, half_w, poly):
    """겹치지 않을 때의 최소 이격(R, 음수면 침투 깊이). 잉크 경계 ↔ 폴리곤 경계."""
    d = densify(line, 0.004)
    best = 1e9
    for q in d:
        db = dist_to_boundary(poly, q)
        sd = -db if contains(poly, q) else db
        best = min(best, sd - half_w)
    return best

# ────────────────────────────────────────────────────────────────────────────────
# 4. 자세 정의 (프로덕션 식 그대로 — 망토 프레임으로 환산)
# ────────────────────────────────────────────────────────────────────────────────
# 망토(=액세서리 컨테이너)는 엉덩이를 축으로 상체 기울임 λ 만큼 회전하고, 팔은 «각도를 일부러
# 돌리지 않는다»(StickmanPoseAnimator.TickBodyLean 문서). 어깨 부착점은 함께 돈다.
# ⇒ 망토 프레임에서 보면 어깨는 제자리이고 팔 각도만 +λ 만큼 돌아간다.
def watch_stance_arms(back_hands, s, blend=1.0, lean_scale=1.0, breath=0.0):
    """s = L1 왕복 위상값 sin(...) ∈ [-1,1]. 반환: [(θu_망토프레임, θl), ...] 두 팔."""
    base_lean = LEAN_BACK if back_hands else LEAN_CROSS
    lam = (base_lean + s * LEAN_SWAY) * blend * lean_scale
    sh_sway = -s * SH_SWAY
    out = []
    for sign in (+1.0, -1.0):
        if back_hands:
            su = BACK_UPPER + sign * BACK_SH_STAG
            sl = ELBOW_SIGN * (BACK_ELBOW - sign * BACK_EL_STAG)
        else:
            su = CROSS_UPPER + sign * CROSS_SH_STG
            sl = ELBOW_SIGN * (CROSS_ELBOW + sign * CROSS_EL_STG)
        nu = sign * ARM_SPREAD + sign * breath * BREATH_ARM
        nl = ELBOW_SIGN * IDLE_ELBOW
        u = nu + (su + sh_sway - nu) * blend      # LerpAngle(≤180° 차라 선형과 동일)
        l = nl + (sl - nl) * blend
        out.append((u + lam, l))
    return out

def idle_arms(breath=0.0):
    """평소 Idle(집중 세션 밖). 기울임 0 → 망토 프레임 = 루트 프레임."""
    return [(sign * ARM_SPREAD + sign * breath * BREATH_ARM, ELBOW_SIGN * IDLE_ELBOW)
            for sign in (+1.0, -1.0)]

# ────────────────────────────────────────────────────────────────────────────────
# 5. 측정
# ────────────────────────────────────────────────────────────────────────────────
def measure(label, arms, cape, scale, verbose=True):
    w = arm_width_R(scale); half = 0.5 * w
    back = cape["back"]; collar = cape["collar"]; clasp = cape["clasp"]
    rows = []
    for i, (u, l) in enumerate(arms):
        line = arm_points(u, l, w)
        tip = line[-1]
        elbow_idx = ARC_SAMPLES           # 관절점 인덱스(PolylineJointIndex)
        elbow = line[elbow_idx]
        rows.append(dict(
            arm="A" if i == 0 else "B", u=u, l=l,
            tip=tip, elbow=elbow,
            tip_in=contains(back, tip),
            tip_depth=dist_to_boundary(back, tip) if contains(back, tip) else -dist_to_boundary(back, tip),
            elbow_in=contains(back, elbow),
            run=inside_run_from_tip(line, back),
            cl_in=centerline_inside_length(line, back),
            total=polyline_length(line),
            area=ink_overlap_area(line, half, back),
            area_vis=ink_overlap_area(line, half, back, minus=(collar, clasp)),
            clear=ink_clearance(line, half, back),
            line=line))
    if verbose:
        pt = R_to_pt(scale)
        print("  [%s] 배율 %.2f · 팔 획 %.3f R (%.2f pt) · 1R = %.3f pt" % (label, scale, w, w*pt, pt))
        for r in rows:
            print("    팔%s θu=%+7.2f° θl=%+6.2f° | 손끝 (%+.3f,%+.3f) %s 깊이 %+.3f R (%+.2f pt)"
                  % (r["arm"], r["u"], r["l"], r["tip"][0], r["tip"][1],
                     "안" if r["tip_in"] else "밖", r["tip_depth"], r["tip_depth"]*pt))
            print("        팔꿈치 (%+.3f,%+.3f) %s | 끝에서 잠긴 중심선 %.3f R (%.2f pt) "
                  "| 중심선 총 잠김 %.3f/%.3f R (%.0f%%)"
                  % (r["elbow"][0], r["elbow"][1], "안" if r["elbow_in"] else "밖",
                     r["run"], r["run"]*pt, r["cl_in"], r["total"], 100.0*r["cl_in"]/r["total"]))
            print("        잉크∩뒤판 %.4f R² (칼라 가림 뺀 실제 노출 %.4f R² = %.2f pt²) | 잉크 이격 %+.4f R"
                  % (r["area"], r["area_vis"], r["area_vis"]*pt*pt, r["clear"]))
    return rows

def sweep_worst(back_hands, cape, scale):
    """L1 위상 s · 혼합비 blend · 호흡 breath 전 구간에서 «끝에서 잠긴 길이» 최악값."""
    w = arm_width_R(scale); half = 0.5*w; back = cape["back"]
    worst = None
    for bi in range(0, 21):
        blend = bi/20.0
        for si in range(-4, 5):
            s = si/4.0
            for br in (-1.0, 0.0, 1.0):
                for u, l in watch_stance_arms(back_hands, s, blend, 1.0, br):
                    line = arm_points(u, l, w)
                    run = inside_run_from_tip(line, back, 0.01)
                    if worst is None or run > worst[0]:
                        worst = (run, blend, s, br, u, l,
                                 ink_overlap_area(line, half, back,
                                                  minus=(cape["collar"], cape["clasp"]), step=0.03))
    return worst

def main():
    print("=" * 100)
    print("R26 — 뒷짐(P2) × 망토 기하 충돌 실측   (design-equipment / 2026-09-06)")
    print("=" * 100)
    print("리그(R 배수): 어깨 y=%.5f · 엉덩이 y=%.5f · 발바닥 y=%.5f · 상완 %.5f · 전완 %.5f"
          % (SHOULDER_R, HIP_R, FOOT_R, UPPER_R, LOWER_R))
    print("팔 각도 상수: 뒷짐 어깨 %.1f±%.1f° · 팔꿈치 %.1f∓%.1f° | 팔짱 어깨 %.2f±%.2f° · 팔꿈치 %.2f±%.2f°"
          % (BACK_UPPER, BACK_SH_STAG, BACK_ELBOW, BACK_EL_STAG,
             CROSS_UPPER, CROSS_SH_STG, CROSS_ELBOW, CROSS_EL_STG))
    print("기울임: 뒷짐 %.1f°±%.1f · 팔짱 %.1f°±%.1f · 어깨 미세회전 ∓%.1f°"
          % (LEAN_BACK, LEAN_SWAY, LEAN_CROSS, LEAN_SWAY, SH_SWAY))
    for nm, c in CAPES.items():
        xs = [p[0] for p in c["back"]]; ys = [p[1] for p in c["back"]]
        print("망토 «%s» 뒤판: x %.3f~%.3f R · y %.3f~%.3f R · 점 %d개 (칼라 y %.3f~%.3f)"
              % (nm, min(xs), max(xs), min(ys), max(ys), len(c["back"]),
                 min(p[1] for p in c["collar"]), max(p[1] for p in c["collar"])))
    print()

    for scale in (0.75, 1.00):
        print("─" * 100)
        print("배율 %.2f — 머리 지름 %.2f pt" % (scale, 2 * R_to_pt(scale)))
        print("─" * 100)
        for nm, cape in CAPES.items():
            print(" ■ %s" % nm)
            measure("P2 뒷짐(정지 s=0)", watch_stance_arms(True, 0.0), cape, scale)
            measure("P1 팔짱(정지 s=0)", watch_stance_arms(False, 0.0), cape, scale)
            measure("대조: 평소 Idle", idle_arms(), cape, scale)
            print()

    print("=" * 100)
    print("전 구간 스윕(혼합비 0~1 × L1 위상 ±1 × 호흡 ±1) — «끝에서 잠긴 중심선» 최악값")
    print("=" * 100)
    for scale in (0.75, 1.00):
        for nm, cape in CAPES.items():
            for bh, tag in ((True, "P2 뒷짐"), (False, "P1 팔짱")):
                run, blend, s, br, u, l, area = sweep_worst(bh, cape, scale)
                print("  배율 %.2f · %s · %s : 최악 %.3f R (%.2f pt) @ blend=%.2f s=%+.2f breath=%+.0f "
                      "(θu=%+.2f θl=%+.2f) 노출 %.4f R²"
                      % (scale, nm, tag, run, run*R_to_pt(scale), blend, s, br, u, l, area))

if __name__ == "__main__":
    main()
