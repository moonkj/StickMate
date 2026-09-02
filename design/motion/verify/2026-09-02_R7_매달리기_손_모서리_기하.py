# -*- coding: utf-8 -*-
"""
R7 — 「창 끝쪽 띠에 매달린다」 기하 검산 (design-motion, 2026-09-02)
====================================================================
사용자 신고(1차): "창에서 매달려 떨어질때 창끝보다는 좀 떨어져서 매달림, 좀 안쪽에 매달려야하는데.
                   그리고 맥같은 경우도 창모서리가 타원이라 끝은 비어있는 공간에 매달려있음"
사용자 완화(2차): "굳이 창끝에서만 매달리다가 떨어질 필요없잖아. 완전 끝말고 적당히 끝쪽만 되도 될거 같은데"

→ 요구가 **점(모서리 정합)에서 띠(끝쪽 구간)로** 바뀌었다.
  그래서 인셋을 '모서리 반경 R + 여유'로 유도하지 않는다. **손이 쥔 것으로 읽히는 겹침**에서 유도하고,
  모서리 곡률은 **화면공간 하한(pt)** 하나로만 받친다(= 획 두께가 MinStrokeScreenPoints로 받쳐지는 것과 같은 구조).

★ 프로덕션 함수를 한 줄도 부르지 않는다. 저장소의 다른 라운드가 남긴 실측값 5개로 교정한다.
"""
import math
def d(x): return math.radians(x)

H          = 2.2746944            # StickConfig.BaselineCharacterTotalHeight
SHOULDER_Y = 1.28 + 0.4846944
ARM_UP, ARM_LO = 0.38, 0.37
LINE_SCALE = 1.045
W_ARM  = 0.10 * LINE_SCALE
W_LEG  = 0.12 * LINE_SCALE
HEAD_R = 0.22
PT     = 982.0/(2*12.0)           # DockGeometry.ReferencePointsPerWorldUnit = 40.9167
SPREAD, ELBOW, SWAY_A = 11.0, 8.0, 5.0
EDGE_OFF   = 0.14                 # ledgeHangEdgeOffset (현재: 모서리 '바깥')
ELBOW_SIGN = +1.0
SCALES = (0.35, 0.60, 1.00)       # MinCharacterScale .. 사용자 저장값 .. MaxCharacterScale

def hand(sign, sway, mirror=False):
    up = 180.0 - sign*SPREAD - sway
    lo = up + ELBOW_SIGN*(sign if mirror else 1.0)*ELBOW
    return (ARM_UP*math.sin(d(up)) + ARM_LO*math.sin(d(lo)),
            -ARM_UP*math.cos(d(up)) - ARM_LO*math.cos(d(lo)))
SW = [-SWAY_A, -2.5, 0.0, 2.5, SWAY_A]

print("="*84); print("교정(양성 대조)"); print("="*84)
ok=True
def cal(n,g,w,t):
    global ok; good=abs(g-w)<=t; ok&=good
    print(f"  {'OK ' if good else 'XX '}{n:44s} 계산 {g:.6f} / 기록 {w:.6f}")
cal("팔 전장/신장 (CLAUDE.md 인계계약)", (ARM_UP+ARM_LO)/H, 0.3297, 2e-4)
cal("다리 획/신장 (StickConfig 툴팁)", W_LEG/H, 0.055128, 2e-5)
cal("어깨/신장 (R5 실측표)", SHOULDER_Y/H, 0.775792, 2e-4)
_,hy = hand(+1,0)
cal("LedgeHangDropDepth (StickConfig 문서 2.5072)", SHOULDER_Y+hy, 2.5072, 6e-4)
cal("  같은 값 H 배수 (문서 1.1022 H)", (SHOULDER_Y+hy)/H, 1.1022, 2e-4)
assert abs((ARM_UP+ARM_LO+0.01)/H - 0.3297) > 2e-4
print("  OK  [네거티브] 팔 길이 0.01 오염 시 교정이 실제로 깨진다")
if not ok: raise SystemExit("교정 실패 — 아래 숫자 전부 무효")
print("  → 교정 5/5 + 네거티브 1/1 통과\n")

print("="*84); print("① 지금 손끝 위치 (+ = 모서리 바깥 = 허공)"); print("="*84)
fs=[EDGE_OFF+hand(+1,s)[0] for s in SW]; bs=[EDGE_OFF+hand(-1,s)[0] for s in SW]
print(f"  루트 X = 모서리 {EDGE_OFF:+.4f} 유닛 ({EDGE_OFF/H:+.6f} H)   ← ledgeHangEdgeOffset 은 '바깥'이다")
print(f"  앞손(바깥쪽) : {min(fs):+.4f} ~ {max(fs):+.4f} 유닛  ({min(fs)/H:+.4f} ~ {max(fs)/H:+.4f} H)")
print(f"  뒷손         : {min(bs):+.4f} ~ {max(bs):+.4f} 유닛  ({min(bs)/H:+.4f} ~ {max(bs)/H:+.4f} H)")
print(f"  ★ 앞손은 **완전한 직사각형 창에서도 sway 전 구간 내내 허공**이다. 반경 R은 그 위에 더해지는 것일 뿐,")
print(f"    원인이 아니다 — R=0 인 Windows 10 에서도 이 그림은 이미 깨져 있다.")
print(f"  두 손 간격 {abs(hand(1,0)[0]-hand(-1,0)[0])/H:.4f} H (머리 지름 {2*HEAD_R/H:.4f} H의 "
      f"{abs(hand(1,0)[0]-hand(-1,0)[0])/(2*HEAD_R)*100:.0f}%) / 높이차 {hand(1,0)[1]-hand(-1,0)[1]:+.4f} 유닛\n")

print("="*84); print("② sway는 지금 '손을 끌고' 있다 (물리 반대)"); print("="*84)
span_asym = max(hand(+1,s)[0] for s in SW) - min(hand(+1,s)[0] for s in SW)
print(f"  루트는 매 프레임 고정(hangPos 대입)인데 팔 각도만 ±{SWAY_A}° 움직인다 →")
print(f"  손끝이 {span_asym:.4f} 유닛({span_asym/H:.6f} H) 폭으로 모서리 위를 미끄러진다.")
print(f"  실제 매달림은 손이 고정되고 몸이 흔들린다. **부착 대상이 뒤바뀌어 있다.**\n")

print("="*84); print("③ 팔꿈치 부호 대칭화 — lower = upper + NeutralSign x ElbowBendSign x elbow"); print("="*84)
fx0,fy0 = hand(+1,0,True)
for s in SW:
    f,fy = hand(+1,s,True); b,by = hand(-1,s,True)
    print(f"  sway {s:+5.1f}: 앞손 {f:+.4f} ({f/H:+.6f} H)  뒷손 {b:+.4f} ({b/H:+.6f} H)  높이차 {fy-by:+.5f}")
print(f"  → ±{abs(fx0)/H:.6f} H 대칭, 높이 동일. 간격이 {2*abs(fx0)/abs(hand(1,0)[0]-hand(-1,0)[0])*100:.0f}%로 줄어든다.")
print(f"  → DropDepth {SHOULDER_Y+fy0:.4f} 불변(오른팔은 sign=+1이라 식이 그대로) → 손 정렬 계약(Y) 무영향\n")

print("="*84); print("④ 인셋 — 플랫폼 사실에 의존하지 않는 유도 (사용자 완화 반영)"); print("="*84)
r_cap  = W_ARM/2/H                # 손끝 캡 반경 (H)
head_d = 2*HEAD_R/H               # 머리 지름 (H)
outer  = abs(fx0)/H               # 바깥손이 루트에서 떨어진 거리 (H) — 대칭화 후
print(f"  손끝은 팔 획의 둥근 캡이다. 캡 반경 r = W/2 = {r_cap:.6f} H")
print(f"  머리 지름 = {head_d:.6f} H,  바깥손 오프셋(대칭화 후) = {outer:.6f} H")
print()
print("  [정의] g = 창 모서리에서 **바깥손 끝**까지의 안쪽 거리(H).")
print(f"    하한 g >= r = {r_cap:.6f} H")
print(f"           근거: 캡이 모서리를 넘으면 그 부분이 허공에 그려진다. 순수 기하, 판단 없음.")
print(f"    상한 g <= r + 머리지름 = {r_cap+head_d:.6f} H")
print(f"           근거: 캡 바깥면과 모서리의 틈이 캐릭터 자신의 최대 폭(머리 지름)을 넘으면")
print(f"                 '끝쪽'이 아니라 '그냥 창 위'로 읽힌다. ★판단값이지 실측이 아니다.")
g_lo, g_hi = r_cap, r_cap+head_d
g_tg = (g_lo+g_hi)/2
print(f"    목표 g  = {g_tg:.6f} H  (밴드 중앙, 양쪽 여유 ±{(g_hi-g_lo)/2:.6f} H)")
print()
root_h = g_tg + outer
print(f"  루트 X 인셋(신장 배수) = g + 바깥손오프셋 = {g_tg:.6f} + {outer:.6f} = **{root_h:.6f} H**")
print(f"  (현재는 {EDGE_OFF/H:+.6f} H 바깥. 총 이동량 {EDGE_OFF/H + root_h:.6f} H 안쪽으로.)\n")

print("="*84); print("⑤ 화면공간 하한 — 모서리 곡률을 '반경을 몰라도' 받치는 장치"); print("="*84)
print("  획 두께가 MinStrokeScreenPoints로 받쳐지는 것과 **같은 구조**를 쓴다:")
print("      실효 g = max(g_설계 x H,  ScreenPointsToWorld(g_최소pt))")
print("  이유: 모서리 반경은 OS가 pt로 그린다 = 화면공간 사실이다. 배율에 비례하지 않는다.")
print("        H 배수만 쓰면 작은 배율에서 반드시 곡률 안으로 들어간다(아래 표).")
print()
print(f"  {'배율':>6} {'1H(pt)':>9} {'설계 g(pt)':>12} {'설계 인셋(pt)':>14}   판정(모서리 10~12pt 대비)")
for sc in SCALES:
    hp=H*sc*PT
    print(f"  {sc:6.2f} {hp:9.2f} {g_tg*hp:12.2f} {root_h*hp:14.2f}   "
          f"{'통과' if g_tg*hp>=12 else '★ 부족 — 곡률 구간 안'}")
print()
print("  → 배율 0.35/0.60 에서 설계 g가 12pt에 못 미친다. 그래서 pt 하한이 반드시 필요하다.")
print()
Rmax = 12.0
need = Rmax + r_cap*H*max(SCALES)*PT
print(f"  [하한값 유도] g_최소pt >= R_max + r(최대 배율에서의 pt)")
print(f"                        = {Rmax:.1f} + {r_cap*H*max(SCALES)*PT:.2f} = {need:.2f} pt  →  **14 pt** 로 올림")
print(f"     · R_max = 12 는 '일반적인 macOS 창 모서리 10~12pt'의 **위쪽 끝**이다(리더 제시).")
print(f"       ★미확인 — 실제 반경은 dev-platform이 확정할 플랫폼 사실이다.")
print(f"     · 위쪽 끝을 쓰는 이유는 실패가 비대칭이기 때문이다(⑥).")
print(f"     · dev-platform이 실측 R을 주면 이 한 줄만 다시 계산하면 되고 결론은 안 움직인다.")
G_MIN_PT = 14.0
print()
print(f"  {'배율':>6} {'1H(pt)':>9} {'실효 g(pt)':>12} {'실효 인셋(pt)':>14} {'실효 인셋(H)':>14}  지배항")
for sc in SCALES:
    hp=H*sc*PT
    g_eff_pt = max(g_tg*hp, G_MIN_PT)
    ins_pt = g_eff_pt + outer*hp
    print(f"  {sc:6.2f} {hp:9.2f} {g_eff_pt:12.2f} {ins_pt:14.2f} {ins_pt/hp:14.6f}  "
          f"{'pt 하한' if G_MIN_PT>g_tg*hp else 'H 설계값'}")
print()

print("="*84); print("⑥ 실패는 비대칭이다 — 어느 쪽으로 틀리는 게 싼가"); print("="*84)
print("  바깥으로 틀림(과소 인셋): 손이 허공. **사용자가 오늘 두 번 신고한 그림 그대로.**")
print("  안쪽으로 틀림(과다 인셋): 손이 창 면 위. '끝쪽'이 흐려질 뿐, 그림이 깨지지는 않는다.")
print("  → R 추정은 항상 큰 쪽. pt 하한을 쓰는 것도 같은 이유다.")
print()
print("  [Windows 10 검산 — R = 0 인 유일한 경우]")
for sc in SCALES:
    hp=H*sc*PT
    g_eff_pt = max(g_tg*hp, G_MIN_PT)
    over = g_eff_pt - g_hi*hp     # R=0 일 때의 상한 초과분
    print(f"    배율 {sc:.2f}: 실효 g {g_eff_pt:6.2f}pt / 상한 {g_hi*hp:6.2f}pt → "
          f"{'초과 ' + format(over,'.2f') + 'pt' if over>0 else '밴드 안'}")
print("  → Win10 에서는 상한을 넘는다(= 필요보다 안쪽). 싼 쪽 실패라 허용하되,")
print("    **모서리가 둥근 플랫폼인가(불리언)** 만 알면 이 초과가 통째로 사라진다.")
print("    ★dev-platform 에 물어야 할 것은 '반경 몇 pt'가 아니라 **'이 발판의 이 모서리가 둥근가'**다.")
print("      (Windows: 빌드번호 + DWMWA_WINDOW_CORNER_PREFERENCE / macOS: 항상 예 / 합성 발판: 항상 아니오)")
print()

print("="*84); print("⑦ 진입 판정 — 띠로 바꾸면 오히려 여유가 생긴다"); print("="*84)
for sc in SCALES:
    hp=H*sc*PT
    half = 0.4*sc                                  # BaselineBodyPhysicsHalfWidth x 배율
    edge_stop = max(0.3, half+0.10)                # DockGeometry.ResolveEdgeStopDistance
    probe = max(0.5, edge_stop+0.10)               # ResolveEdgeProbeReach (여유 0.10 가정)
    g_eff_pt = max(g_tg*hp, G_MIN_PT)
    ins_units = (g_eff_pt + outer*hp)/PT
    print(f"  배율 {sc:.2f}: 경계판정거리 {edge_stop:.3f} / 프로브도달 {probe:.3f} 유닛 "
          f"({probe*PT:.1f}pt) vs 인셋 {ins_units:.3f} 유닛({g_eff_pt+outer*hp:.1f}pt) → "
          f"{'여유 있음' if ins_units < probe else '★ 프로브 밖'}")
print("  → 인셋이 프로브 도달거리 안이라 **AutoWanderController의 경계 도달 판정은 손댈 필요가 없다.**")
print("    캐릭터는 이미 모서리에서 최대 프로브 도달거리만큼 안쪽에 선 채로 추첨을 받는다.")
print()
print("  단 하나 반드시 함께 옮겨야 하는 것: GroundSensor.TryFindDescendTarget 의 dropX.")
print("    손을 놓은 뒤 수평 속도가 0이므로(LedgeHangState가 매 프레임 재확정) 점 프로브가 정확하다.")
print("    프로브 x != 실제 놓는 x 이면 대사의 낙차가 거짓이 된다 = **불변 원칙 1 위반**.")
for sc in SCALES:
    hp=H*sc*PT
    g_eff_pt = max(g_tg*hp, G_MIN_PT)
    print(f"    배율 {sc:.2f}: dropX 이동 = {EDGE_OFF*PT:.2f}pt(바깥분 제거) + {g_eff_pt+outer*hp:.2f}pt(인셋) "
          f"= {EDGE_OFF*PT + g_eff_pt+outer*hp:.2f}pt 안쪽")
print("    ⚠ 현재 시그니처는 Mathf.Max(0f, dropOutwardOffset) 이다 — 음수를 넣으면 **조용히 0**이 된다.")
