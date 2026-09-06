#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 — 「팔을 꼬고 있는데 모양이 이상함」(사용자 실기 신고)의 처방 검산.

이 스크립트는 **판정용이 아니라 유도·검산용**이다. 최종 판정은 실제 빌드 캡처로만 한다
(디자인 7인 공통 규칙). 렌더는 `2026-09-06_R2_뒷짐손끝_렌더.py`의 스트로크/필렛 규칙을
그대로 재사용하고, 거기 없던 **몸통 기울임(RequestBodyLean)**과 **팔 그리기 순서**를 더했다.

증명하는 것:
  T1  «최전방 손이 가슴 높이 & 앞» ⟹ 어깨각 θu_A가 **유일하게 결정**된다(팔꿈치 예산 고정 시).
  T2  그 θu_A에서 A의 상완은 **몸통 획 안에 완전히 묻힌다** → A는 «몸에서 떨어진 막대»로 보인다.
  T3  손을 몸통에 붙이려면 손 몸통비가 0.53 이하로 내려간다(= 2026-09-06 이전의 «손 모으기»).
  T4  두 팔꿈치는 반지름 0.380 원 위에서 기울기 tanθu로 움직인다 ⟹ Δy ≪ Δx ⟹ 교차점은 항상 뒤.
  T5  «상완 노출(16.78°)»과 «손 몸통비 0.65»는 팔꿈치 예산 전 구간에서 **동시 만족 불가능**.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

# ── 리그 실측 (배율 1.0, docs/UX_MOTION_FOCUS_SESSION.md 1절 재유도) ─────────
H = 2.2746944
HIP_Y = 0.9347
SH_Y = 1.7647
TORSO = SH_Y - HIP_Y                 # 0.8300
ARM_U, ARM_L = 0.380, 0.370
ARM_W = 0.1045
TORSO_W = 0.11495
HALF_SUM = 0.5 * (ARM_W + TORSO_W)   # 0.1097
HEAD_R = 0.22
HEAD_Y = 2.0547

EXPOSE_FULL = math.degrees(math.asin(HALF_SUM / ARM_U))       # 16.78
EXPOSE_HALF = math.degrees(math.asin(HALF_SUM / (2 * ARM_U)))  # 8.30
LEAN_CAP = math.degrees(math.asin(HALF_SUM / TORSO))           # 7.60


def sh_to_hand(b_deg):
    """팔꿈치 굽힘 b에서 (|어깨→손|, 어깨→손 방향이 어깨→팔꿈치보다 앞선 각 α)."""
    g = math.radians(180.0 - b_deg)
    d = math.sqrt(ARM_U ** 2 + ARM_L ** 2 - 2 * ARM_U * ARM_L * math.cos(g))
    a = math.degrees(math.asin(max(-1.0, min(1.0, ARM_L * math.sin(g) / d))))
    return d, a


def fk(theta_u, b):
    """어깨각 θu(0=아래, +=앞) · 팔꿈치 b → (팔꿈치, 손, 전완 절대각°)."""
    d, a = sh_to_hand(b)
    e_dir = math.radians(theta_u - 90.0)
    h_dir = math.radians(theta_u - 90.0 + a)
    elbow = (ARM_U * math.cos(e_dir), SH_Y + ARM_U * math.sin(e_dir))
    hand = (d * math.cos(h_dir), SH_Y + d * math.sin(h_dir))
    return elbow, hand, theta_u - 90.0 + b


def ratio(y):
    return (y - HIP_Y) / TORSO


def torso_x(y, lean_deg):
    """기울임 lean(− = 뒤)에서 높이 y의 몸통 중심 x. 회전 중심은 엉덩이."""
    return -(y - HIP_Y) * math.sin(math.radians(-lean_deg)) if lean_deg else 0.0


# ══════════════════════════════════════════════════════════════════════════
print("=" * 78)
print("R3 팔짱 실루엣 처방 — 유도·검산")
print("=" * 78)
print(f"몸통 길이 T = {TORSO:.4f} / 팔 획 {ARM_W:.4f} / 몸통 획 {TORSO_W:.5f} / 합반폭 {HALF_SUM:.4f}")
print(f"상완 노출 하한 {EXPOSE_FULL:.2f}° · 반노출 하한 {EXPOSE_HALF:.2f}° · 기울임 상한 {LEAN_CAP:.2f}°")

CUR_A = (-8.5, 116.5)
CUR_B = (-37.0, 113.0)

print("\n── 현행(2026-09-06 야간 반영본) 실측 ─────────────────────────────")
for tag, (u, b) in (("A 앞팔", CUR_A), ("B 뒷팔", CUR_B)):
    e, hnd, fa = fk(u, b)
    print(f"  {tag}: θu={u:+6.2f}° b={b:6.2f}°  팔꿈치({e[0]:+.4f},{e[1]:.4f}) 비{ratio(e[1]):.3f}"
          f"  손({hnd[0]:+.4f},{hnd[1]:.4f}) 비{ratio(hnd[1]):.3f}  전완절대각{fa:+.1f}°")
eA, hA, faA = fk(*CUR_A)
eB, hB, faB = fk(*CUR_B)
print(f"  손 물림 = {hA[0]-hB[0]:+.4f} (팔 획의 {(hA[0]-hB[0])/ARM_W:.2f}배)   "
      f"두 전완 절대각 차 = {abs(faA-faB):.1f}°")

# ── T1 : 손 몸통비 0.685를 요구하면 θu_A가 유일하다 ────────────────────────
print("\n── T1  «가슴 높이 + 앞» ⟹ 어깨각 유일 (팔꿈치 116.5° 고정) ───────")
d, a = sh_to_hand(116.5)
print(f"  |어깨→손| = {d:.4f} (팔꿈치 116.5°에서 고정) · 손 방향 = θu − {90-a:.2f}°")
for r in (0.75, 0.70, 0.685, 0.65, 0.60, 0.55, 0.528):
    dy = HIP_Y + r * TORSO - SH_Y
    if abs(dy) > d:
        print(f"  비 {r:.3f} → 도달 불가(|dy|={abs(dy):.4f} > {d:.4f})")
        continue
    x = math.sqrt(d * d - dy * dy)
    th = math.degrees(math.atan2(dy, x)) + (90.0 - a)
    print(f"  비 {r:.3f} → 손 x = {x:+.4f} (유일)   ⟹ θu_A = {th:+7.2f}°"
          f"   상완 노출? {'예' if abs(th) >= EXPOSE_FULL else ('반' if abs(th) >= EXPOSE_HALF else '아니오')}")

# ── T2 : A의 상완이 몸통에 묻히는 양 ─────────────────────────────────────
print("\n── T2  A 상완의 «몸통 밖 노출» 실측 (기울임 −4°) ─────────────────")
for lean in (0.0, -4.0):
    ex, ey = eA
    tc = torso_x(ey, lean)
    back = tc - 0.5 * TORSO_W
    stroke_back = ex - 0.5 * ARM_W
    print(f"  lean {lean:+.1f}° : 팔꿈치 x {ex:+.4f}, 획 뒤끝 {stroke_back:+.4f}, "
          f"몸통 뒤끝 {back:+.4f} → 노출 {max(0.0, back-stroke_back):+.4f} "
          f"({max(0.0,back-stroke_back)/ARM_W:.2f} 획)")

# ── T3 : 손이 몸통 실루엣 안으로 들어오는 높이 ────────────────────────────
print("\n── T3  손을 몸통 안(|x| ≤ 몸통반폭+팔반폭)에 넣으려면 ─────────────")
lim = 0.5 * TORSO_W + 0.5 * ARM_W
for b in (100.0, 108.0, 113.0, 116.5, 124.14):
    dd, aa = sh_to_hand(b)
    if lim > dd:
        continue
    dy = -math.sqrt(dd * dd - lim * lim)
    th = math.degrees(math.atan2(dy, lim)) + (90.0 - aa)
    print(f"  b={b:6.2f}° : 손 x={lim:+.4f} 일 때 손 몸통비 = {ratio(SH_Y+dy):.3f}  (θu={th:+.2f}°)")
print("  ⟹ 어느 팔꿈치 예산에서도 «손이 몸통에 붙는» 높이는 허리(0.50~0.55)다.")

# ── T4 : 팔꿈치 궤적의 기울기 ────────────────────────────────────────────
print("\n── T4  두 팔꿈치는 반지름 0.380 원 위 · 국소 기울기 = tanθu ───────")
for th in (0.0, -8.5, -16.78, -25.0, -37.0, -50.0):
    e, _, _ = fk(th, 116.5)
    print(f"  θu={th:+7.2f}° → 팔꿈치({e[0]:+.4f},{e[1]:.4f})  d(y)/d(x) = {math.tan(math.radians(th)):+.4f}")
dxe = eB[0] - eA[0]
dye = eB[1] - eA[1]
print(f"  현행 A↔B 팔꿈치 Δx={dxe:+.4f} Δy={dye:+.4f}  (|Δy/Δx| = {abs(dye/dxe):.3f})")
xc = (eB[1] - eA[1] - eB[0] * math.tan(math.radians(faB)) + eA[0] * math.tan(math.radians(faA))) / \
     (math.tan(math.radians(faA)) - math.tan(math.radians(faB)))
print(f"  두 전완의 교차 x = {xc:+.4f}  (몸통 뒤끝 {torso_x(1.45,-4)-0.5*TORSO_W:+.4f} ~ "
      f"앞끝 {torso_x(1.45,-4)+0.5*TORSO_W:+.4f} 사이 → 화면에서 X가 안 보인다)")

# ── T5 : 노출과 가슴높이의 동시 만족 가능성 ──────────────────────────────
print("\n── T5  «상완 노출 16.78°» ∧ «손 몸통비 ≥ 0.65» 동시 만족? ────────")
ok = False
for b10 in range(900, 1250, 10):
    b = b10 / 10.0
    dd, aa = sh_to_hand(b)
    th = -EXPOSE_FULL
    hd = math.radians(th - 90.0 + aa)
    r = ratio(SH_Y + dd * math.sin(hd))
    x = dd * math.cos(hd)
    flag = "OK" if (r >= 0.65 and x > 0) else "✗"
    if b in (100.0, 110.0, 116.5, 120.0, 124.0):
        print(f"  b={b:6.1f}° · θu=−16.78° → 손 비 {r:.3f}, 손 x {x:+.4f}  {flag}")
    ok = ok or (r >= 0.65 and x > 0)
print(f"  전 구간(90~124.5°) 동시 만족 = {ok}  ⟹ 상완을 보이게 하려면 «가슴 높이»를 포기해야 한다.")

# ── 후보 실루엣 검산 ─────────────────────────────────────────────────────
CANDS = {
    #  이름            A(θu,b)           B(θu,b)         lean
    "V0 현행":        (CUR_A,            CUR_B,          -4.0),
    "V0L 현행·직립":  (CUR_A,            CUR_B,           0.0),
    "V1 쐐기열기":    (CUR_A,            (-44.0, 100.0), -4.0),
    "V2 쐐기열기+":   (CUR_A,            (-47.0,  94.0), -4.0),
    "V5 허리팔짱":    ((-26.0, 116.5),   (-50.0,  98.0), -4.0),
    "★처방A(보수)":   ((-13.0, 116.5),   (-38.0,  96.0), -2.0),
    "★처방B(권장)":   ((-22.0, 116.5),   (-38.0,  92.0), -2.0),
}

print("\n" + "=" * 78)
print("후보 실루엣 — 잠긴 단언(FocusSessionAmbientTests V1/V9) 대조")
print("=" * 78)
print(f"{'후보':<16}{'물림':>9}{'/획':>6}{'앞손비':>8}{'뒷손비':>8}{'전완차':>8}"
      f"{'B후방돌출':>10}{'A돌출':>8}{'쐐기틈':>9}  판정")
for name, (A, B, lean) in CANDS.items():
    ea, ha, fa = fk(*A)
    eb, hb, fb = fk(*B)
    mesh = ha[0] - hb[0]
    ra, rb = ratio(ha[1]), ratio(hb[1])
    gap = abs(fa - fb)
    # B 팔꿈치가 몸통 뒤끝 밖으로 나온 양(획 단위)
    back_edge = torso_x(eb[1], lean) - 0.5 * TORSO_W
    bulge = max(0.0, back_edge - (eb[0] - 0.5 * ARM_W))
    # A 손끝이 몸통 앞끝 밖으로 나온 양
    front_edge = torso_x(ha[1], lean) + 0.5 * TORSO_W
    prot = (ha[0] + 0.5 * ARM_W) - front_edge
    # «쐐기 틈» = 두 전완 중심선이 팔 획 하나만큼 벌어지는 지점이 B의 손 앞인가
    tA, tB = math.tan(math.radians(fa)), math.tan(math.radians(fb))
    denom = tA - tB
    wedge = float('nan')
    if abs(denom) > 1e-6:
        # 중심선 간격이 ARM_W가 되는 x
        c = (ea[1] - ea[0] * tA) - (eb[1] - eb[0] * tB)
        x_gap = (ARM_W - c) / denom
        # 그 지점이 B 손끝(눈에 보이는 쐐기 구간) 안이면 «갈라짐이 보인다»
        wedge = min(hb[0], ha[0]) - x_gap
    ok = (mesh > ARM_W) and (ra >= 0.65) and (ra > rb) and (gap > 20.0) and (A[0] > B[0])
    print(f"{name:<16}{mesh:>+9.4f}{mesh/ARM_W:>6.2f}{ra:>8.3f}{rb:>8.3f}{gap:>8.1f}"
          f"{bulge/ARM_W:>10.2f}{prot/HEAD_R:>8.2f}{wedge:>9.4f}  {'통과' if ok else '★단언위반'}")
print("  · B후방돌출/A돌출 = 획 배수 / 머리반경 배수.  쐐기틈 > 0 이면 두 전완이 «보이는 구간에서» 갈라진다.")

# ── 규칙 B(크리즈) 여유 재확인 ───────────────────────────────────────────
FILLET_RATIO, MAX_SAG_PER_W, MIN_HALF = 0.42, 1.0, 0.00436
SCALES = (0.35, 0.45, 0.60, 0.75, 1.00)
LINE_SCALE = 1.045
BASE_ARM_W, BASE_LEG_W = 0.1045, 0.1254
MIN_STROKE_PT, PPU = 2.0, 47.0   # LimbCurveRenderer 관례(R2 검산과 동일 가정)


def crease_margin(bend_deg, upper, lower, width):
    h = 0.5 * math.radians(abs(bend_deg))
    t = FILLET_RATIO * min(upper, lower)
    if h > MIN_HALF and width > 0:
        sag = t * math.tan(0.5 * h)
        if sag > MAX_SAG_PER_W * width:
            t *= (MAX_SAG_PER_W * width) / sag
    r = t / math.tan(h)
    return r / (0.5 * width)


print("\n── 규칙 B(크리즈) 여유 — 후보의 팔꿈치 값 ────────────────────────")
for b in sorted({round(v[0][1], 2) for v in CANDS.values()} | {round(v[1][1], 2) for v in CANDS.values()}):
    m = crease_margin(b, ARM_U, ARM_L, ARM_W)
    print(f"  팔꿈치 {b:6.2f}° → 여유 {m:.4f}  {'OK' if m >= 1.1682 else '★다리 병목(1.1682) 아래'}")
print("  (배율 무관 — 필렛/획이 같은 배율로 스케일되므로 비는 불변. R2 검산과 같은 결론)")

# ══════════════════════════════════════════════════════════════════════════
# 인계 — coder에게 넘기는 확정 상수 (중앙 ± 스태거 형태)
# ══════════════════════════════════════════════════════════════════════════
IDLE_ARM_SPREAD = 40.0     # Editor/SceneBootstrapper.IdleArmSpreadDegrees
IDLE_ELBOW = 10.0          # Editor/SceneBootstrapper.IdleElbowBendDegrees
BACK_LIMIT = 60.0          # ShoulderSwingBackLimitDegrees

print("\n" + "=" * 78)
print("인계 — 중앙 ± 스태거 환산 / 전이 이동량 / 관절 한계")
print("=" * 78)
for name, (A, B, lean) in (("현행", (CUR_A, CUR_B, -4.0)),
                           ("처방A", ((-13.0, 116.5), (-38.0, 96.0), -2.0)),
                           ("처방B", ((-22.0, 116.5), (-38.0, 92.0), -2.0))):
    cu = 0.5 * (A[0] + B[0]); su = 0.5 * (A[0] - B[0])
    ce = 0.5 * (A[1] + B[1]); se = 0.5 * (A[1] - B[1])
    # 전이 이동량: NeutralSign>0 팔은 +40 → A, NeutralSign<0 팔은 −40 → B
    travel = abs(IDLE_ARM_SPREAD - A[0]) + abs(-IDLE_ARM_SPREAD - B[0])
    swapped = abs(IDLE_ARM_SPREAD - B[0]) + abs(-IDLE_ARM_SPREAD - A[0])
    el_travel = abs(A[1] - IDLE_ELBOW) + abs(B[1] - IDLE_ELBOW)
    print(f"  {name}: ArmUpper중앙 {cu:+7.3f}° · 어깨스태거 {su:6.3f}° · "
          f"Elbow중앙 {ce:7.3f}° · 팔꿈치스태거 {se:6.3f}° · lean {lean:+.1f}°")
    print(f"        어깨 전이 {travel:.1f}° (역배정 {swapped:.1f}° = {swapped/travel:.2f}배) · "
          f"팔꿈치 전이 {el_travel:.1f}° · 최심 어깨 {max(abs(A[0]),abs(B[0])):.1f}° "
          f"< 뒤쪽 한계 {BACK_LIMIT:.0f}° {'OK' if max(abs(A[0]),abs(B[0])) < BACK_LIMIT else '★위반'}")
    print(f"        최심 팔꿈치 {max(A[1],B[1]):.2f}° "
          f"{'(현행과 동일 — 크리즈 최악 무변화)' if max(A[1],B[1]) == 116.5 else ''}")
