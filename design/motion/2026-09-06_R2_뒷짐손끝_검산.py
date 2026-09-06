#!/usr/bin/env python3
"""P2 뒷짐 손끝 간격 검산 — 프리팹 실측 + 프로덕션 상수만 입력으로 쓴다.
출처:
  Assets/_Project/Prefabs/Stickman.prefab
    LeftArm/RightArm HingeJoint2D.connectedAnchor = (0, 1.3235208)   ← 두 팔이 **같은 점**
    LeftArm BoxCollider2D.size.y  = 0.285   (상완)
    LeftArmLower BoxCollider2D.size.y = 0.2775 (전완)
    LeftArm LineRenderer width     = 0.078375 (팔 획)
  States/StickmanPoseAnimator.cs
    FocusWatchBackArmUpperDegrees = -51.5
    FocusWatchBackElbowDegrees    =  72.5
    FocusWatchBackShoulderStaggerDegrees = 4
    FocusWatchBackElbowStaggerDegrees    = 6
    FocusCrossArmUpperDegrees = -22.75 / FocusCrossShoulderStaggerDegrees = 14.25
    FocusCrossElbowDegrees    = 114.75 / FocusCrossElbowStaggerDegrees    = 1.75
"""
import math, re, os

REPO = "/Users/kjmoon/App/StickMate"
SRC = open(os.path.join(REPO, "Assets/_Project/Scripts/States/StickmanPoseAnimator.cs")).read()


def const(name):
    m = re.search(r"const\s+float\s+" + name + r"\s*=\s*(-?[\d.]+)f", SRC)
    return float(m.group(1))


BACK_UP = const("FocusWatchBackArmUpperDegrees")
BACK_ELB = const("FocusWatchBackElbowDegrees")
BACK_S = const("FocusWatchBackShoulderStaggerDegrees")
BACK_E = const("FocusWatchBackElbowStaggerDegrees")
CROSS_UP = const("FocusCrossArmUpperDegrees")
CROSS_S = const("FocusCrossShoulderStaggerDegrees")
CROSS_ELB = const("FocusCrossElbowDegrees")
CROSS_E = const("FocusCrossElbowStaggerDegrees")

SH = (0.0, 1.3235208)
LU, LL = 0.285, 0.2775
W = 0.078375                 # 팔 획(월드 유닛)
R = W / 0.475                # 머리 반경 R (획 = 0.475 R, design-character 확정)
BAKED = 0.75                 # 프리팹이 구워진 배율
PT_PER_R = 5.816             # 배율 0.75에서 1R = 5.816pt (R26과 같은 환산)


def fk(u, l):
    ur, lr = math.radians(u), math.radians(u + l)
    e = (SH[0] + LU * math.sin(ur), SH[1] - LU * math.cos(ur))
    h = (e[0] + LL * math.sin(lr), e[1] - LL * math.cos(lr))
    return e, h


def back_arms(shoulder_stagger=BACK_S, elbow_stagger=BACK_E, elbow_sign=-1.0, mid_elb=BACK_ELB):
    """elbow_sign = -1 이 현재 코드(어깨와 반대 부호). +1 이면 같은 부호."""
    front = (BACK_UP + shoulder_stagger, mid_elb + elbow_sign * elbow_stagger)
    rear = (BACK_UP - shoulder_stagger, mid_elb - elbow_sign * elbow_stagger)
    return front, rear


def seg_dist(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    dx, dy = bx - ax, by - ay
    L2 = dx * dx + dy * dy
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L2))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def polyline(u, l, n=200):
    e, h = fk(u, l)
    pts = [(SH[0] + (e[0] - SH[0]) * i / n, SH[1] + (e[1] - SH[1]) * i / n) for i in range(n + 1)]
    pts += [(e[0] + (h[0] - e[0]) * i / n, e[1] + (h[1] - e[1]) * i / n) for i in range(1, n + 1)]
    return pts


def report(tag, front, rear):
    fe, fh = fk(*front)
    re_, rh = fk(*rear)
    hand = math.hypot(fh[0] - rh[0], fh[1] - rh[1])
    elb = math.hypot(fe[0] - re_[0], fe[1] - re_[1])
    fore_gap = abs((front[0] + front[1]) - (rear[0] + rear[1]))
    # 중심선 최소 거리(어깨 공유 때문에 언제나 0이다 — 그 사실을 수치로 보인다)
    A, B = polyline(*front), polyline(*rear)
    mind = min(seg_dist(p, B[i], B[i + 1]) for p in A for i in (0, len(B) - 2))
    # 팔 길이 진행에 따른 중심선 이격(어깨에서 손끝까지)
    prof = []
    n = len(A) - 1
    for frac in (0.0, 0.25, 0.5, 0.75, 1.0):
        i = int(frac * n)
        prof.append(math.hypot(A[i][0] - B[i][0], A[i][1] - B[i][1]) / R)
    print(f"[{tag}]")
    print(f"  앞팔 어깨 {front[0]:+.2f}° 팔꿈치 {front[1]:+.2f}°  손 ({fh[0]:+.4f},{fh[1]:.4f})")
    print(f"  뒤팔 어깨 {rear[0]:+.2f}° 팔꿈치 {rear[1]:+.2f}°  손 ({rh[0]:+.4f},{rh[1]:.4f})")
    print(f"  손끝 간격 {hand / R:.4f} R = 획의 {hand / W:.2f}배 ({hand / R * PT_PER_R:.2f} pt @0.75)")
    print(f"  팔꿈치 간격 {elb / R:.4f} R = 획의 {elb / W:.2f}배")
    print(f"  전완 절대각 차 {fore_gap:.2f}°   두 중심선 최소거리 {mind / R:.4f} R")
    print(f"  중심선 이격 프로필(어깨0→손끝1): " + " / ".join(f"{v:.3f}" for v in prof) + " R")
    print(f"  가장 깊은 어깨각 {max(abs(front[0]), abs(rear[0])):.2f}° (뒤쪽 한계 60°)")
    return hand / W


print(f"1R = {R:.6f} 월드유닛 · 팔 획 = {W} = 0.475 R · 배율 {BAKED} 에서 1R = {PT_PER_R} pt\n")

f, r = back_arms()
cur = report("현행 P2 뒷짐 (어깨 스태거 4, 팔꿈치 스태거 6, 반대 부호)", f, r)
print()

cf = (CROSS_UP + CROSS_S, CROSS_ELB + CROSS_E)
cr = (CROSS_UP - CROSS_S, CROSS_ELB - CROSS_E)
report("참고: P1 팔짱", cf, cr)
print()

print("── 팔꿈치 스태거 E 를 훑는다(어깨 스태거 4 고정, 부호는 현행=반대) ──")
print("   E(도)   손끝간격(R)  획배수   전완각차   앞팔꿈치  뒤팔꿈치")
for E in [0, 3, 6, 9, 12, 15, 18, 21, 24, 27, 30]:
    f, r = back_arms(elbow_stagger=E)
    _, fh = fk(*f); _, rh = fk(*r)
    d = math.hypot(fh[0] - rh[0], fh[1] - rh[1])
    fg = abs((f[0] + f[1]) - (r[0] + r[1]))
    print(f"   {E:5.1f}   {d/R:9.4f}   {d/W:6.2f}   {fg:7.2f}°   {f[1]:7.2f}°  {r[1]:7.2f}°")

print()
print("── 같은 부호로 걸었을 때(어깨 스태거 4 고정) ──")
print("   E(도)   손끝간격(R)  획배수   전완각차   앞팔꿈치  뒤팔꿈치")
for E in [0, 3, 6, 9, 12, 15, 18, 21, 24]:
    f, r = back_arms(elbow_stagger=E, elbow_sign=+1.0)
    _, fh = fk(*f); _, rh = fk(*r)
    d = math.hypot(fh[0] - rh[0], fh[1] - rh[1])
    fg = abs((f[0] + f[1]) - (r[0] + r[1]))
    print(f"   {E:5.1f}   {d/R:9.4f}   {d/W:6.2f}   {fg:7.2f}°   {f[1]:7.2f}°  {r[1]:7.2f}°")

print()
print("── 어깨 스태거 S 를 훑는다(팔꿈치 스태거 6, 반대 부호 고정) ──")
print("   S(도)   손끝간격(R)  획배수   팔꿈치간격(R)  가장깊은어깨각")
for S in [0, 2, 4, 6, 8, 10, 12]:
    f, r = back_arms(shoulder_stagger=S)
    fe, fh = fk(*f); re_, rh = fk(*r)
    d = math.hypot(fh[0] - rh[0], fh[1] - rh[1])
    de = math.hypot(fe[0] - re_[0], fe[1] - re_[1])
    print(f"   {S:5.1f}   {d/R:9.4f}   {d/W:6.2f}   {de/R:11.4f}   {max(abs(f[0]),abs(r[0])):8.2f}°")

# ────────────────────────────────────────────────────────────────────────────
# 추가 검산 — 최소점 위치 / 손끝 잉크 합집합 / 결합식
# ────────────────────────────────────────────────────────────────────────────
def hand_gap_R(S, E, sign=-1.0):
    f, r = back_arms(shoulder_stagger=S, elbow_stagger=E, elbow_sign=sign)
    _, fh = fk(*f); _, rh = fk(*r)
    return math.hypot(fh[0] - rh[0], fh[1] - rh[1]) / R

print()
print("── 결합식과 최소점 ─────────────────────────────────────────────────")
g, e = min((hand_gap_R(BACK_S, x * 0.05), x * 0.05) for x in range(0, 401))
print(f"  손끝 간격 최소 {g:.4f} R @ 팔꿈치 스태거 {e:.2f}°  (어깨 스태거 {BACK_S}° 고정)")
print(f"  현행 E={BACK_E}° -> {hand_gap_R(BACK_S, BACK_E):.4f} R / E=0° -> {hand_gap_R(BACK_S, 0):.4f} R")
print(f"  ★ 즉 «반대 부호 팔꿈치 스태거»는 손끝을 벌리는 것이 아니라 «모으는» 장치다.")
print()
print("  전완 절대각 차 = |(어깨+팔꿈치)앞 − (어깨+팔꿈치)뒤| = 2·|S − E|")
for S, E in ((4, 0), (4, 4), (4, 6), (6, 6), (4, 15)):
    print(f"    S={S}, E={E} -> {2*abs(S-E)}°   (S = E 는 두 전완이 «정확히 평행» = 퇴화)")
print()
d = hand_gap_R(BACK_S, BACK_E)
print("── 손끝 잉크(둥근 캡 2개)의 합집합 ────────────────────────────────")
print(f"  {d + 0.475:.4f} R x {0.475:.4f} R  (장단비 {(d + 0.475) / 0.475:.2f} : 1)")
print(f"  배율 0.75 : {(d + 0.475) * PT_PER_R:.2f} x {0.475 * PT_PER_R:.2f} pt"
      f"  /  Retina 물리픽셀 {(d + 0.475) * 11.63:.1f} x {0.475 * 11.63:.1f} px")
print("  -> «두 개의 손»이 아니라 «살짝 길쭉한 점 하나»로 그려진다. 이것이 실측의 실체다.")
