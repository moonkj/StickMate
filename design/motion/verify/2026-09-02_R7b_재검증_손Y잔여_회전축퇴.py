# -*- coding: utf-8 -*-
"""
R7b 재검증 — 앞 라운드(레이트리밋 사망) 산출물의 독립 재측정 + 그 문서가 침묵한 두 지점.

이 스크립트가 답하는 것은 두 개다.
  (A) 5-1(손 X 고정)을 넣어도 **Y는 여전히 흔들린다.** 얼마나?  → 고정할 것인가 말 것인가.
  (B) 던지기 회전 축퇴의 원인이 **반올림 하나가 아니라 둘**이다. 어느 쪽이 지배하는가?

★ 교정 원칙(CLAUDE.md): 계산기를 만들면 **바깥의 알려진 값**으로 먼저 교정한다.
  여기서 쓰는 앵커 3개는 전부 **이 스크립트 바깥**에서 왔다 —
   · 팔 전장/신장 0.3297  (CLAUDE.md 인계계약 표)
   · 손끝 Y 흔들림 "최대 0.01유닛"  (LedgeHangHandAlignmentTests.cs:224 주석 — 잠긴 테스트가 스스로 적어둔 값)
   · 월드1유닛 = 40.9167pt  (DockGeometry.ReferencePointsPerWorldUnit = 982/(2*12))
  ★ (A)의 앵커가 **프로덕션 테스트의 주석**이라는 점이 중요하다. 내 식으로 만든 기대값이 아니다.
"""
import math

# ── 프로덕션에서 직접 확인한 값만 쓴다(이 라운드에 grep으로 재측정) ──────────────
H          = 2.2746944   # StickConfig.BaselineCharacterTotalHeight:1816
ARM_UP     = 0.38        # 프리팹 조립값 (합 0.75 → /H = 0.3297 로 교차확인)
ARM_LO     = 0.37
SPREAD     = 11.0        # ledgeHangArmSpreadDegrees:483
ELBOW      = 8.0         # ledgeHangElbowBendDegrees:486
SWAY_A     = 5.0         # ledgeHangSwayAmplitudeDegrees:497
SWAY_HZ    = 0.9         # ledgeHangSwayFrequencyHz:500
PT         = 982.0/(2*12.0)   # DockGeometry.cs:68  = 40.9167
SCALES     = (0.35, 0.60, 1.00)

# ThrowTumbleState / StickConfig 에서 재측정
SPIN_MIN   = 220.0   # throwTumbleMinSpinDegreesPerSecond:2344
SPIN_MAX   = 720.0   # throwTumbleMaxSpinDegreesPerSecond:2348
LEAD       = 0.1     # throwTumbleAlignLeadSeconds:2353

ok = True
def chk(label, got, want, tol):
    global ok
    good = abs(got-want) <= tol
    ok &= good
    print(f"  {'OK ' if good else 'FAIL'} {label:<52} 계산 {got:.6f} / 기준 {want:.6f}")

def hand_y(sign, sway):
    """HangHandReachAboveRoot 와 같은 식 (StickmanPoseAnimator.cs:876-888).
       루트 기준 손끝 Y. 단 그 함수는 sway 를 안 받는다 — 여기서는 넣어 본다."""
    up = 180.0 - sign*SPREAD - sway
    lo = up + 1.0*ELBOW                      # 현재 코드: ElbowBendSign(+1) 공용
    return ARM_UP*-math.cos(math.radians(up)) + ARM_LO*-math.cos(math.radians(lo))

def hand_x(sign, sway):
    up = 180.0 - sign*SPREAD - sway
    lo = up + 1.0*ELBOW
    return ARM_UP*math.sin(math.radians(up)) + ARM_LO*math.sin(math.radians(lo))

print("="*88); print("교정 (바깥 앵커 3개)"); print("="*88)
chk("팔 전장/신장 (CLAUDE.md 인계계약)", (ARM_UP+ARM_LO)/H, 0.3297, 0.0002)
chk("월드1유닛 pt (DockGeometry.cs:68)", PT, 40.9167, 0.001)
# ★ 핵심 교정: 잠긴 테스트가 주석에 적어둔 "최대 0.01유닛"을 내 식이 재현하는가
#   ⚠ 첫 시도에 이 교정이 **깨졌다**(계산 0.016014 / 기준 0.010). 원인은 식이 아니라 **내가 자를 잘못 댄 것**:
#     테스트가 재는 값은 delta = handY - edgeTopY 이고, 루트는 sway **중립(0도)** 에서 구한
#     LedgeHangDropDepth 로 배치된다. 즉 그 주석의 "최대 0.01"은 **중립 대비 최대 이탈**이지
#     피크투피크가 아니다. 피크투피크(0.0160)를 중립이탈(0.0108)과 비교했으니 깨지는 게 맞다.
#   → 교정을 올바른 자로 다시 댄다. 두 값을 **둘 다** 출력해 다음 사람이 같은 실수를 못 하게 한다.
ys_all = [hand_y(+1, s) for s in (-SWAY_A, -2.5, 0.0, 2.5, +SWAY_A)]
y_neutral = hand_y(+1, 0.0)
bob_pp   = max(ys_all) - min(ys_all)                     # 피크투피크
bob_dev  = max(abs(y - y_neutral) for y in ys_all)       # ★ 테스트가 보는 양: 중립 대비 이탈
chk("손끝 Y 중립대비 최대이탈 (테스트 주석 '최대 0.01유닛')", bob_dev, 0.010, 0.002)
bob = bob_dev

# 네거티브: 팔 길이를 오염시키면 교정이 실제로 깨지는가
_save = ARM_UP
ARM_UP = 0.48
_bad = (ARM_UP+ARM_LO)/H
ARM_UP = _save
print(f"  {'OK ' if abs(_bad-0.3297)>0.0002 else 'FAIL'} [네거티브] 팔 길이 0.10 오염 시 교정이 실제로 깨진다   (오염값 {_bad:.6f})")
ok &= abs(_bad-0.3297) > 0.0002
print(f"  → 교정 {'3/3 + 네거티브 1/1 통과' if ok else '실패 — 아래 숫자 전부 폐기'}")
assert ok, "교정 실패"

print()
print("="*88)
print("(A) 5-1(손 X 고정)을 넣어도 **Y는 안 잡힌다** — 잔여 상하 흔들림")
print("="*88)
print("  HangHandReachAboveRoot(StickmanPoseAnimator.cs:876)는 인자에 sway가 **없다**(재측정 확인).")
print("  즉 LedgeHangDropDepth는 sway 중립(0도) 한 점에서만 계산된 상수다.")
print(f"  실제 손끝 Y는 sway ±{SWAY_A:.0f}도 동안 피크투피크 {bob_pp:.6f}유닛으로 오르내리고,")
print(f"  루트 배치의 기준인 **중립 대비**로는 최대 {bob_dev:.6f}유닛 이탈한다(테스트가 보는 양).")
print(f"  ★ 이탈이 비대칭이다: 아래로 {y_neutral-min(ys_all):+.6f} / 위로 {max(ys_all)-y_neutral:+.6f}유닛.")
print(f"    cos가 180도 근처에서 비선형이라 **손이 올라가는 것보다 더 많이 내려간다** —")
print(f"    sway 한 주기 시간평균은 중립보다 {sum(hand_y(+1, SWAY_A*math.sin(2*math.pi*k/720)) for k in range(720))/720 - y_neutral:+.6f}유닛 낮다.")
print(f"    (= 배율1.0에서 {abs(sum(hand_y(+1, SWAY_A*math.sin(2*math.pi*k/720)) for k in range(720))/720 - y_neutral)*PT:.3f}pt. 서브픽셀이라 조치 불필요 — 기록만 남긴다.)\n")
print(f"     {'배율':>6} {'1유닛(pt)':>12} {'Y이탈(pt)':>12} {'X잔여(고정 전, pt)':>20}   판정")
xs = [hand_x(+1, s) for s in (-SWAY_A, -2.5, 0.0, 2.5, SWAY_A)]
xspan = max(xs)-min(xs)
for s in SCALES:
    upt = PT*s
    print(f"     {s:>6.2f} {upt:>12.4f} {bob*upt:>12.4f} {xspan*upt:>20.4f}   "
          f"{'Y는 육안 임계(1pt) 아래' if bob*upt < 1.0 else '★Y도 보인다'}")
print()
print(f"  · X 잔여는 사용자가 신고한 그림을 만드는 크기다(배율0.60에서 {xspan*PT*0.60:.2f}pt).")
print(f"  · Y 잔여는 모든 배율에서 {bob*PT*1.00:.2f}pt 이하 = **1pt 미만, 서브픽셀**이다.")
print("  → 결론: **X만 고정한다. Y는 고정하지 않는다.**")
print("    Y까지 고정하려면 LedgeHangDropDepth를 sway 의존으로 바꿔야 하는데, 그 값은")
print("    LedgeHangHandAlignmentTests가 0.05유닛 허용오차로 잠근 **바로 그 양**이다.")
print(f"    잠금 여유 0.05유닛 대비 잔여는 {bob:.4f}유닛 = {bob/0.05*100:.0f}% — 이미 안쪽이다.")
print("    ★ 이 문장이 문서에 없으면 다음 라운드가 '대칭이니 Y도 고정하자'로 뒤집는다.")

print()
print("="*88)
print("(B) 던지기 회전 축퇴 — 원인이 **둘**이다 (리더 브리프는 하나만 지목했다)")
print("="*88)
print("  ThrowTumbleState.TryPlanRotation (재측정, :416-445):")
print("    ① 반올림   turns = max(1, RoundToInt((ideal - toNextUpright)/360) + 1)")
print("    ② 속도클램프 while (turns > 1 && delta/usable > maxSpin) turns--;")
print("  ★ ②는 브리프에 없다. 그리고 ②가 더 강하다 — ①을 난수로 바꿔도 ②가 되돌린다.\n")
print("  2바퀴 성립 조건 (toNextUpright=360, 즉 던질 때 몸이 직립인 통상 경우):")
print("    ①이 2를 고르려면 : ideal = spin x usable >= 360 + 180 = 540")
print(f"    ②가 2를 허용하려면: delta/usable = 720/usable <= {SPIN_MAX:.0f}  →  usable >= {720/SPIN_MAX:.2f}초")
print()
print(f"     {'usable(초)':>11} {'①통과 최소spin':>16} {'②통과':>8}   실효 판정")
for usable in (0.4, 0.54, 0.7, 0.9, 1.0, 1.2, 1.6, 2.0, 2.45, 3.0):
    need_spin = 540.0/usable
    p1 = need_spin <= SPIN_MAX
    p2 = (720.0/usable) <= SPIN_MAX
    if not p2:
        verd = "1바퀴 강제 — ②속도클램프 (난수를 넣어도 소용없다)"
    elif not p1:
        verd = "1바퀴 — ①반올림 (spin 상한으로도 540 못 넘김)"
    else:
        verd = f"2바퀴 가능 (spin >= {need_spin:.0f}도/초 일 때)"
    print(f"     {usable:>11.2f} {need_spin:>16.0f} {'통과' if p2 else '탈락':>8}   {verd}")
print()
print(f"  · 데스크톱 던지기의 usable = 비행 - {LEAD}초. 코드 주석의 예시 낙하 0.64초 → usable 0.54초.")
print(f"    그 값은 위 표에서 **②에 걸린다** — 720/0.54 = {720/0.54:.0f}도/초 > {SPIN_MAX:.0f} 상한.")
print("  ★ 그래서 '반올림을 난수화하자'는 처방은 **작동하지 않는다.** ②가 즉시 1로 되돌린다.")
print("    12/12가 정확히 360도인 것은 ②의 결정론적 결과이고, 이것이")
print("    페르소나 실측('전부 정확히 360도')이 흔들림 없이 재현된 이유다.")
print()
print("  [부수 확인] ①의 toNextUpright 는 리터럴 360이 아니라 RemainingDegreesToUpright()다.")
print("    _angle 은 Enter 에서 body.rotation 을 그대로 받는다(:191).")
print("    → 페르소나가 '전부 정확히 360도'를 봤다는 것은 **던질 때 body.rotation ≈ 0**의 방증이다.")
print("      (rotation이 0이 아니었다면 1바퀴가 360도가 아닌 값으로 관측됐어야 한다.)")

print()
print("="*88)
print("(C) 광과민 — 회전이 만드는 공간 점멸 주파수")
print("="*88)
for spin in (SPIN_MIN, 360.0, 540.0, SPIN_MAX):
    print(f"     {spin:>5.0f}도/초  →  막대 팔다리는 180도마다 같은 그림  →  {spin/180.0:>5.2f} Hz  "
          f"{'★위험대(>3Hz) 안' if spin/180.0 > 3.0 else '안전대'}")
print(f"\n  현재 상한 {SPIN_MAX:.0f}도/초 = {SPIN_MAX/180:.1f}Hz 로 3Hz 위험대 안이다.")
print("  다만 광과민 가이드라인은 **면적 조건**을 함께 요구한다(통상 시야의 25% 또는 큰 블록).")
print(f"     배율 0.60에서 캐릭터 신장 = {H*PT*0.60:.1f}pt, 획 두께 2~3pt.")
print("  → 면적으로 두 자릿수 아래라 1차 판정은 '위험 낮음'. **다만 근거를 남긴 적이 없었다**(marketing 지적은 정확).")
