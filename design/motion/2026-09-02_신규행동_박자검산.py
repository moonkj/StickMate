# -*- coding: utf-8 -*-
"""
2026-09-02 — 집중모드 2종 + 유휴 7종 신규 행동의 박자/각도/기하 검산.
design-motion 산출. 프로덕션 .cs 무수정. 이 스크립트는 문서의 모든 숫자를 재생산한다.

입력은 전부 저장소에서 읽은 실측이다:
  · Editor/SceneBootstrapper.cs      팔/다리 마디 길이
  · Core/StickmanMetrics.cs          어깨/엉덩이/머리 비율
  · Core/StickConfig.cs              walkSpeed, characterScale, 착지/등반 상수
  · Dialogue/DialogueKind.cs         가독예산 식과 상수
  · docs/MOTION_SPEC.md 18-1         박자 종류별 하한
"""
import math

# ── 저장소 실측 상수 ───────────────────────────────────────────────────────────
H_BASE      = 2.2747          # StickConfig.BaselineCharacterTotalHeight
ARM_U, ARM_L = 0.38, 0.37     # SceneBootstrapper.BaselineArm{Upper,Lower}Length
LEG_U, LEG_L = 0.50, 0.45     # SceneBootstrapper.BaselineLeg{Upper,Lower}Length
SHOULDER_Y  = 1.7646944       # StickmanMetrics.BaselineShoulderRatio 분자
HIP_Y       = 0.9346944       # BaselineHipRatio 분자
HEADC_Y     = 2.0546944       # BaselineHeadCenterRatio 분자
HEAD_R      = 0.22            # BaselineHeadRadiusRatio 분자
WALK_SPEED  = 2.5             # StickConfig.walkSpeed (ResolveWalkSpeed = walkSpeed x scale)
RING_R      = 0.54            # FocusWatchRenderer.RingRadiusRatio 분자
PT_PER_UNIT = 67.0 / 1.6375   # Dock 단차 실측: 1.6375유닛 = 67pt

# 신장 비율(H 단위) — 배율 불변
hip   = HIP_Y      / H_BASE
sho   = SHOULDER_Y / H_BASE
headc = HEADC_Y    / H_BASE
headr = HEAD_R     / H_BASE
armU, armL = ARM_U / H_BASE, ARM_L / H_BASE
legU, legL = LEG_U / H_BASE, LEG_L / H_BASE
arm_reach  = armU + armL
leg_reach  = legU + legL
ring_r     = RING_R / H_BASE
walk_Hps   = WALK_SPEED / H_BASE     # H/s — 배율 불변

# 박자 하한 (MOTION_SPEC 18-1)
FLOOR = {'action': 0.19, 'hold': 0.17, 'settle': 0.12, 'transition': 0.10}

# 가독예산 (Dialogue/DialogueKind.cs)
def reading(text):  return min(max(0.28 + len(text) * 0.075, 0.62), 2.20)
def required(text): return 0.06 + reading(text)      # FadeInSeconds 0.06 + 가독예산

def eff(u, l, bend_deg):
    """2마디 팔다리를 bend_deg만큼 접었을 때의 관절-끝 직선 거리."""
    return math.sqrt(u*u + l*l + 2*u*l*math.cos(math.radians(bend_deg)))

def pt(h_units, scale=0.60):
    """H 단위 -> 화면 pt(기본: 사용자 저장 배율 0.60)."""
    return h_units * H_BASE * scale * PT_PER_UNIT

print("=" * 78)
print("0. 리그 기하 (H = 신장, 배율 불변)")
print("=" * 78)
for n, v in [("엉덩이", hip), ("어깨", sho), ("머리중심", headc), ("머리반경", headr),
             ("팔 도달", arm_reach), ("다리 길이", leg_reach), ("링 반지름", ring_r)]:
    print(f"  {n:8s} = {v:.6f} H   ({pt(v):6.2f} pt @배율0.60)")
print(f"  보행속도 = {walk_Hps:.5f} H/s  ({pt(walk_Hps):.2f} pt/s @배율0.60)  [배율 불변]")
print(f"  1 H @배율0.60 = {pt(1.0):.2f} pt")

# ─────────────────────────────────────────────────────────────────────────────
print()
print("=" * 78)
print("1. 박자표 전수 — 하한 검사 + 민첩 배율 하한 k_min")
print("=" * 78)

BEATS = {
 "곡괭이질 1사이클": [("들기",'action',0.34),("내려찍기",'action',0.22),
                     ("충격 정지",'hold',0.20),("뽑기",'action',0.26),("숨 고르기",'hold',0.24)],
 "책상업무 1줄":    [("쓰기 스트로크",'action',0.33),("되돌리기",'action',0.21),("줄 넘김",'hold',0.19)],
 "책상업무 종이넘김":[("넘기기",'action',0.42),("정지",'hold',0.19)],
 "종료임박 체크":    [("멈춤",'hold',0.19),("확인",'action',0.26),("응시",'hold',0.20),("복귀",'action',0.21)],
 "로프 던지기":     [("뒤로 넘기기",'action',0.22),("던지기",'action',0.26),
                     ("걸림 확인",'hold',0.19),("당겨 팽팽",'action',0.22)],
 "로프 등반 1사이클":[("접기",'action',0.26),("뻗기",'action',0.30),
                     ("당기기",'action',0.23),("그립 확정",'hold',0.20)],
 "로프 맨틀":       [("올라서기",'action',0.28),("정착",'settle',0.14)],
 "낚시 고정분":     [("정지",'transition',0.14),("앉기",'action',0.42),("대 꺼내기",'action',0.26),
                     ("던지기",'action',0.21),("입질",'hold',0.20),("챔질",'action',0.21),
                     ("감기x3",'action',0.63),("획득",'action',0.24),("확인 정지",'hold',0.20),
                     ("일어서기",'action',0.42)],
 "감상":            [("정지",'transition',0.14),("액자 소환",'action',0.30),("감상",'hold',2.60),
                     ("리액션",'action',0.42),("액자 거두기",'action',0.26),("정착",'settle',0.14)],
 "인형극":          [("정지",'transition',0.14),("자세 잡기",'action',0.30),("날갯짓x4",'action',1.72),
                     ("전환",'action',0.22),("개 입x3",'action',1.28),("마무리",'action',0.24),
                     ("정착",'settle',0.14)],
 "쓰다듬기":        [("정지",'transition',0.14),("손 뻗기",'action',0.30),("쓰다듬x3",'action',1.02),
                     ("마주보기",'hold',0.20),("손 떼기",'action',0.24),("정착",'settle',0.14)],
 "닦기 1구간":      [("밀기",'action',0.21),("당기기",'action',0.21),
                     ("밀기",'action',0.21),("당기기",'action',0.21),("엉덩이 옆걸음",'action',0.46)],
 "낮잠":            [("정지",'transition',0.14),("앉기",'action',0.42),("눕기",'action',0.52),
                     ("깨기",'action',0.46),("일어서기",'action',0.52),("정착",'settle',0.14)],
}
SPLIT = {"감기x3":3, "날갯짓x4":4, "개 입x3":3, "쓰다듬x3":3}
worst_k = 0.0
for name, beats in BEATS.items():
    tot = sum(d for _, _, d in beats)
    ks, bad = [], []
    for bn, kind, d in beats:
        unit = d / SPLIT.get(bn, 1)          # 반복 박자는 1회분으로 검사
        f = FLOOR[kind]
        if unit < f - 1e-9: bad.append(f"{bn}={unit:.3f}<{f}")
        ks.append(f / unit)
    k = max(ks); worst_k = max(worst_k, k)
    flag = "  ✗ " + ", ".join(bad) if bad else "  ✓"
    print(f"  {name:18s} 합계 {tot:5.2f}초  k_min={k:.3f}{flag}")
print(f"\n  ★ 전 행동 공통 민첩 배율 하한 k_min = {worst_k:.3f}"
      f"  (= 박자를 최대 {(1-worst_k)*100:.1f}%까지만 줄일 수 있다)")

# ─────────────────────────────────────────────────────────────────────────────
print()
print("=" * 78)
print("2. 정지 박자(brake) — 수평 표류 결함의 모션 처방")
print("=" * 78)
T_BRAKE = 0.14
d = 0.5 * walk_Hps * T_BRAKE
print(f"  진입 속도 = 보행 최대 {walk_Hps:.5f} H/s")
print(f"  {T_BRAKE}초 SmoothStep 감속 -> 정지 거리 = {d:.5f} H = {pt(d):.2f} pt @배율0.60")
print(f"  감속률 = {walk_Hps/T_BRAKE:.3f} H/s^2 = {pt(walk_Hps/T_BRAKE):.1f} pt/s^2")
print(f"  60fps 기준 {T_BRAKE*60:.1f}프레임 / 전이박자 하한 0.10초 대비 +{(T_BRAKE/0.10-1)*100:.0f}%")
print(f"  [대조] 리더 보고 실측 표류: 3.4초에 192pt, 감속 -0.68 pt/s^2")
print(f"         -> 이 처방의 감속률은 그 {pt(walk_Hps/T_BRAKE)/0.68:.0f}배다.")

# ─────────────────────────────────────────────────────────────────────────────
print()
print("=" * 78)
print("3. 곡괭이질 — 머리 스윕 실측 (타이머 앵커 판정 근거)")
print("=" * 78)
LEAN_BACK, LEAN_HIT = -14.0, 27.0
SINK = 0.030                       # 충격 프레임 몸 전체 침하 (H)
r = headc - hip
x1, x2 = r*math.sin(math.radians(LEAN_BACK)), r*math.sin(math.radians(LEAN_HIT))
y1, y2 = hip + r*math.cos(math.radians(LEAN_BACK)), hip + r*math.cos(math.radians(LEAN_HIT))
dx, dy = abs(x2-x1), abs(y1-y2) + SINK
print(f"  상체 기울임 {LEAN_BACK}° -> {LEAN_HIT}° (진폭 {LEAN_HIT-LEAN_BACK}°), 회전중심=엉덩이")
print(f"  머리중심 회전반경 = {r:.6f} H")
print(f"  머리 수평 스윕 = {dx:.6f} H = {pt(dx):.2f} pt @배율0.60")
print(f"  머리 수직 스윕 = {dy:.6f} H = {pt(dy):.2f} pt  (침하 {SINK} H 포함)")
print(f"  발(루트) 스윕 = 0.00 pt  (수평 비소유 + 접지 고정)")
print(f"  링 지름 = {2*ring_r:.6f} H = {pt(2*ring_r):.2f} pt")
print(f"  ★ 머리 앵커면 계기가 자기 지름의 {dx/(2*ring_r)*100:.1f}% 를 매 사이클(0.794Hz) 왕복한다.")
htop_hit  = y2 + headr - SINK
htop_back = y1 + headr
print(f"  정수리 최고 = {max(htop_hit,htop_back):.6f} H  < 1.000 H"
      f"  -> 루트+1.0H 앵커는 곡괭이질 중 절대 머리와 겹치지 않는다 (여유 {pt(1-max(htop_hit,htop_back)):.2f} pt)")

# ─────────────────────────────────────────────────────────────────────────────
print()
print("=" * 78)
print("4. 로프 등반 — 사이클당 상승량과 속도")
print("=" * 78)
KNEE_TUCK, KNEE_DRIVE = 126.0, 20.0
ELB_PULL, ELB_OPEN    = 118.0, 22.0
leg_tuck  = eff(legU, legL, KNEE_TUCK)   # bend 0 = 곧게 폄
leg_drive = eff(legU, legL, KNEE_DRIVE)
arm_pull  = eff(armU, armL, ELB_PULL)
arm_open  = eff(armU, armL, ELB_OPEN)
rise_leg, rise_arm = leg_drive - leg_tuck, arm_open - arm_pull
cyc = 0.26+0.30+0.23+0.20
rise = rise_leg + rise_arm
print(f"  다리 접힘 {KNEE_TUCK}° -> 유효 {leg_tuck:.6f} H / 폄 {KNEE_DRIVE}° -> {leg_drive:.6f} H")
print(f"    다리 행정 = {rise_leg:.6f} H")
print(f"  팔 접힘 {ELB_PULL}° -> 유효 {arm_pull:.6f} H / 폄 {ELB_OPEN}° -> {arm_open:.6f} H")
print(f"    팔 행정 = {rise_arm:.6f} H")
print(f"  1사이클 {cyc:.2f}초, 상승 {rise:.6f} H -> 등반속도 {rise/cyc:.5f} H/s"
      f" = {pt(rise/cyc):.1f} pt/s @배율0.60")
print(f"  보행 대비 {rise/cyc/walk_Hps*100:.1f}%  /  ParkourClimb(1.637유닛÷1.20초=55.8pt/s) 대비"
      f" {pt(rise/cyc)/55.8*100:.1f}%")
for h_pt in (120, 200, 300, 420):
    print(f"    창 높이 {h_pt:3d} pt -> 등반 {h_pt/pt(rise/cyc):5.2f}초"
          f" ({math.ceil(h_pt/pt(rise/cyc)/cyc):2d} 사이클)")
CAP = 12.0
print(f"  ★ 상한 ropeClimbMaxSeconds = {CAP:.1f}초 -> 오를 수 있는 최대 높이"
      f" {pt(rise/cyc)*CAP:.0f} pt = {rise/cyc*CAP:.2f} H")
print(f"    그보다 높은 창은 **시작하지 않는다**(전제조건 미충족). 도중에 포기하지 않는다.")
MIN_RISE = 1.20
print(f"  ★ 하한 ropeClimbMinRiseHeights = {MIN_RISE:.2f} H = {pt(MIN_RISE):.0f} pt"
      f"  (로프가 캐릭터 키보다 길어야 로프로 읽힌다). 최소 {math.ceil(MIN_RISE/rise):d} 사이클,"
      f" {MIN_RISE/(rise/cyc):.2f}초")
print(f"  ★ 유효 대역 = [{MIN_RISE:.2f}, {rise/cyc*CAP:.2f}] H = [{pt(MIN_RISE):.0f}, {pt(rise/cyc*CAP):.0f}] pt @배율0.60")
print(f"  ★ 대상은 **창만**이다 — 하단 예약 막대(Dock/작업표시줄)는 제외한다."
      f" 그러지 않으면 배율 0.35에서 Dock 단차가 2.06 H가 되어 대역에 들어온다(로프를 Dock에 건다).")
print("\n  상한 후보별 트레이드오프:")
for c in (9.0, 12.0, 14.0, 18.0):
    print(f"    {c:4.1f}초 -> 최대 {pt(rise/cyc*c):3.0f} pt ({rise/cyc*c:.2f} H), {int(c/cyc):2d} 사이클"
          f"  | 유휴 연출 최장(낚시 11.85초) 대비 {c/11.85*100:5.1f}%")

# ─────────────────────────────────────────────────────────────────────────────
print()
print("=" * 78)
print("5. 걸터앉기(SitOnEdge) 공용 자세 — 낚시/닦기/낮잠 진입이 공유")
print("=" * 78)
SIT_OFFSET = -hip
print(f"  몸 전체 오프셋 = {SIT_OFFSET:.6f} H  (엉덩이가 발판 상단에 놓인다)")
print(f"  앉은 실루엣 높이 = {1.0+SIT_OFFSET:.6f} H = {pt(1.0+SIT_OFFSET):.2f} pt"
      f"  (선 자세의 {(1+SIT_OFFSET)*100:.1f}%)")
sit_sho = sho + SIT_OFFSET
print(f"  앉은 어깨 높이 = {sit_sho:.6f} H = {pt(sit_sho):.2f} pt")
print(f"  손끝 최저(팔 수직 하강) = {sit_sho-arm_reach:.6f} H = {pt(sit_sho-arm_reach):+.2f} pt")
print(f"  다리 늘어뜨림 끝(발) = {SIT_OFFSET:.6f} H = {pt(SIT_OFFSET):.1f} pt  (발판 아래)")
print(f"  Dock 두께 75pt 대비 다리가 차지하는 비율 = {abs(pt(SIT_OFFSET))/75*100:.1f}%  -> 화면 밖으로 안 나간다")

print()
print("  [닦기] 앉은 채 발판 상면(y=0)에 손이 닿는가 — 상체 굽힘별 도달 x 구간")
for lean in (0, 12, 18, 26, 30):
    sx = (hip + SIT_OFFSET) + 0  # 앉은 상태의 엉덩이 = 발판 상단(y=0)
    R  = sho - hip
    shx = R*math.sin(math.radians(lean))
    shy = 0 + R*math.cos(math.radians(lean))
    if arm_reach >= abs(shy):
        half = math.sqrt(arm_reach**2 - shy**2)
        lo, hi = shx-half, shx+half
        print(f"    상체 +{lean:2d}° -> 어깨({shx:.4f},{shy:.4f})"
              f"  y=0 도달 x = [{lo:+.4f}, {hi:+.4f}] H  (폭 {pt(2*half):.2f} pt)")
    else:
        print(f"    상체 +{lean:2d}° -> 어깨 y={shy:.4f} > 팔 {arm_reach:.4f} -> **도달 불가**")
# 문지름 궤적: 상체 18 <-> 30 왕복이 만드는 손끝 x 이동
def hand_x_at(lean, want_y=0.0, sign=+1):
    R = sho - hip
    shx, shy = R*math.sin(math.radians(lean)), R*math.cos(math.radians(lean))
    half = math.sqrt(max(0.0, arm_reach**2 - shy**2))
    return shx + sign*half
xa, xb = hand_x_at(26), hand_x_at(30)
print(f"  ★ 문지름 궤적(상체 +26°<->+30°, 팔 최대 신장 유지) = {abs(xb-xa):.6f} H"
      f" = {pt(abs(xb-xa)):.2f} pt @배율0.60")
print(f"    -> 이 리그의 팔 길이({arm_reach:.4f} H)로는 이보다 큰 발판면 문지름 궤적이 나오지 않는다.")

print()
print("  [반증] 서서 발판면(y=0)을 닦을 수 있는가 — 무릎 굽힘별 손끝 최저")
for knee in (0, 60, 96, 126, 138):
    hip_y = eff(legU, legL, knee) if knee > 0 else leg_reach
    R = sho - hip
    shy = hip_y + R*math.cos(math.radians(30))   # 상체 30°(리그 상한)까지 굽힘
    print(f"    무릎 {knee:3d}° -> 엉덩이 {hip_y:.4f} H, 어깨 {shy:.4f} H,"
          f" 손끝 최저 {shy-arm_reach:+.4f} H = {pt(shy-arm_reach):+6.2f} pt")
print("    -> 무릎을 138°까지 접고 상체를 상한 30°까지 굽혀도 손끝이 발판 위 "
      f"{pt(eff(legU,legL,138)+(sho-hip)*math.cos(math.radians(30))-arm_reach):.2f} pt에 머문다.")
print("       **서서 닦기는 기하학적으로 불가능하다. 걸터앉기가 유일한 해다.**")

# ─────────────────────────────────────────────────────────────────────────────
print()
print("=" * 78)
print("6. 행동-텍스트 싱크 (불변 원칙 1) — 필요체류 vs 발화 시점 잔여")
print("=" * 78)
# (행동, 텍스트, 종류, 발화 시점부터 상태 종료까지의 잔여초, 비고)
LINES = [
 ("곡괭이질",  "캐면서 버틴다",  "서술", 60.00, "세션 하한 60초(MinimumSessionSeconds)"),
 ("곡괭이질",  "다 캤다!",       "반응",  0.00, "종료 시점 발화 — 반응은 상태 밖에서 예산 소진"),
 ("곡괭이질",  "그만할래",       "반응",  0.00, "유저 취소만. 발판 소실은 대사 없음"),
 ("책상업무",  "서류 좀 밀어내자","서술", 60.00, "세션 하한"),
 ("책상업무",  "다 끝냈다",      "반응",  0.00, ""),
 ("낚시",      "던졌다",         "서술",  3.00, "대기 하한 3.0초"),
 ("낚시",      "안 물리네",      "서술",  1.00, "잔여 대기 1.0초 이상일 때만 발화"),
 ("낚시",      "월척!",          "반응",  0.00, "Caught"),
 ("낚시",      "오늘은 아닌가",  "반응",  0.00, "Empty(꽝)"),
 ("로프등반",  "걸렸다",         "서술",  2.19, "당겨팽팽0.22+최소2사이클1.98 = 2.20 중 보수적"),
 ("로프등반",  "올라왔다",       "반응",  0.00, "맨틀 완료"),
 ("감상",      "잘 골랐네",      "서술",  2.60, "Gaze 박자 전체"),
 ("쓰다듬기",  "가만있어 봐",    "서술",  1.46, "쓰다듬1.02+마주보기0.20+손떼기0.24"),
 ("낮잠",      "잠깐만...",      "서술",  8.00, "수면 루프 하한"),
 ("낮잠",      "잘 잤다",        "반응",  0.00, ""),
 ("닦기",      "먼지 좀 봐",     "서술", 10.20, "루프 하한"),
]
print(f"  {'행동':10s} {'대사':16s} {'종류':4s} {'글자':>3s} {'가독예산':>8s} {'필요체류':>8s} {'잔여':>7s}  판정")
for act, txt, kind, remain, note in LINES:
    need = required(txt)
    if kind == "반응":
        verdict = "✓ (반응 — 상태 종료 후에도 예산 소진)"
    else:
        verdict = "✓" if remain >= need else f"✗ 부족 {need-remain:.2f}초"
    print(f"  {act:10s} {txt:16s} {kind:4s} {len(txt):3d} {reading(txt):7.3f}초 {need:7.3f}초"
          f" {remain:6.2f}초  {verdict}")
print("\n  ★ 서술(Narrative)은 상태 종료 프레임에 즉시 컷된다 -> 상태보다 오래 살 수 없다.")
print("  ★ 반응(Reaction)만 상태 밖으로 예산을 넘긴다 — 전부 '방금 일어난 점 사건'이라 종료 후에도 참이다.")
print("  ★ Aborted(발판 소실/드래그/강제) 경로는 **모든 행동에서 대사 0건**이다(계약).")

# ─────────────────────────────────────────────────────────────────────────────
print()
print("=" * 78)
print("7. 종료 임박 체크 리액션 — 삽입 지연 상한")
print("=" * 78)
longest = max(d for _,_,d in BEATS["곡괭이질 1사이클"])
print(f"  트리거 = RemainingSeconds가 60.0을 아래로 교차한 프레임")
print(f"  삽입 규칙 = 진행 중인 박자를 끝낸 뒤 (박자 중간 절단 금지)")
print(f"  최장 박자 = 들기 {longest:.2f}초 -> 실제 발동 잔여 = {60-longest:.2f} ~ 60.00초")
print(f"  -> '마지막 1분' 안에 100% 들어간다. 체크 리액션 자체 길이"
      f" = {sum(d for _,_,d in BEATS['종료임박 체크']):.2f}초, 1회만.")
