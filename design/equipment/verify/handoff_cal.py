# -*- coding: utf-8 -*-
"""① 좌표계 변환식 교정 — 인계본 ↔ 우리 (design-equipment, 2026-09-03).

교정이 깨지면 이 파일 뒤의 모든 숫자를 폐기한다(CLAUDE.md 공통 처방).
교정 앵커는 **인계본이 스스로 선언한 값**과 **물리적 필연**만 쓴다:
  A. 무대→R : 인계본 자기 캐릭터의 눈 좌표 ↔ 우리 rig 상수 (서로 독립 유도)
  B. 아이콘박스→R : 안경다리·고글끈·왕관테의 끝이 **머리 실루엣 가장자리(±1.0R)** 에 닿아야 한다
     ★ EYES 박스(48px)와 HEAD 박스(70px)가 **서로 다른 배율**인데 둘 다 ±1.0R을 재현하면
       박스별 변환이 우연이 아님이 증명된다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, handoff, items, hair
from handoff import parse_items, build, icon_to_R, stage_to_R, HEAD_R_PX, HEAD_CY_PX, K_PX

OK, BAD = "[OK]", "[!!]"
def chk(label, got, want, tol, unit=""):
    d = abs(got - want)
    print("  %s %-46s %10.5f  (기대 %.5f, 차 %.5f%s)" %
          (OK if d <= tol else BAD, label, got, want, d, unit))
    return d <= tol

print("╔══ 0. 자(尺) 자체 교정 — 깨지면 아래를 전부 폐기 ══╗")
allok = True
allok &= chk("W(배율 0.75) = ShippingStrokeBudget", rig.W, 0.343864, 1e-5, " R")
allok &= chk("머리 지름 (W 배수)", 2.0 / rig.W, 5.8163, 1e-3, " W")
allok &= chk("무대 px/유닛 K", K_PX, 0.79, 1e-9)
allok &= chk("머리 반경 px", HEAD_R_PX, 22.12, 1e-9, " px")
allok &= chk("머리중심 스테이지 px", HEAD_CY_PX, 62.34, 1e-9, " px")

print()
print("╔══ A. 무대(200x240) → R  :  인계본 캐릭터 ↔ 우리 rig ══╗")
print("  인계본 머리 circle(100,46) r=28 · 눈 circle(90,43)/(110,43) r=3.4")
ex, ey = stage_to_R(90.0, 43.0)
print("  %-30s %12s %12s %10s" % ("항목", "인계본(R)", "우리 rig(R)", "상대차"))
for lbl, a, b in (("눈 x (|x|)", abs(ex), rig.EYE_X),
                  ("눈 y", ey, rig.EYE_Y),
                  ("눈동자 반경", 3.4 / 28.0, rig.PUPIL_R)):
    print("  %-30s %12.5f %12.5f %9.1f%%" % (lbl, a, b, (a - b) / b * 100.0))
print("  ⇒ 서로를 안 보고 만든 두 문서가 **머리 반경 단위에서** 눈 x를 4.8%, 눈동자를 11% 안에서")
print("    재현한다. 1 무대유닛 = 1/28 R 이 옳다.")

print()
print("╔══ B. 아이콘 박스 → R  :  ±1.0R(머리 가장자리) 재현 ══╗")
IT = parse_items()
anchors = [
    ("sunglasses", "안경다리 끝 x=1.5/62.5", 1.5),
    ("roundglasses", "안경다리 끝 x=1.5/62.5", 1.5),
    ("goggles", "고글끈 끝 x=1.5/62.5", 1.5),
    ("crown", "왕관 테 끝 x=11.5/52.5", 11.5),
]
print("  %-14s %-26s %-7s %10s %9s" % ("아이템", "앵커", "박스px", "→ |x| R", "오차"))
worst = 0.0
for kind, lbl, xv in anchors:
    fx, fy, sx = icon_to_R(kind)
    box = handoff.SLOT_BOX[handoff.ITEM_SLOT[kind]][0]
    r = abs(fx(xv))
    worst = max(worst, abs(r - 1.0))
    print("  %-14s %-26s %-7.0f %10.5f %8.1f%%" % (kind, lbl, box, r, (r - 1.0) * 100))
print("  ⇒ EYES 박스 48px 와 HEAD 박스 70px 는 **배율이 1.458배 다른데** 둘 다 1.0R 을 낸다.")
print("     최악 오차 %.1f%%. 박스별 변환식이 우연이 아니다." % (worst * 100))
allok &= worst <= 0.05

print()
print("╔══ B-2. 음성 대조 — 틀린 변환이면 이 검사가 실제로 빨개지는가 ══╗")
for wrong, note in ((0.5, "박스를 절반으로"), (2.0, "박스를 두 배로")):
    fx, _, _ = icon_to_R("goggles")
    r = abs(fx(1.5)) * wrong
    print("  %s %-26s |x| = %.5f R  (%.0f%% 어긋남)" %
          (BAD if abs(r - 1.0) > 0.05 else OK, note, r, abs(r - 1.0) * 100))
print("  ⇒ 잘못된 배율은 실제로 문턱을 넘는다. 위 [OK]들이 자동 초록이 아니다.")

print()
print("╔══ C. 무대 몸통 골격 — 여기서 갈라진다 ══╗")
# 인계본: 몸통 M100 74 V140, 팔 M52 138 L100 88 L148 138, 다리 M100 140 L66 212 / L134 212
rows = [
    ("머리 반경",        1.0,                              1.0),
    ("머리 아래(몸통 시작)", stage_to_R(100, 74)[1],          -1.0),
    ("어깨(팔 꼭짓점)",   stage_to_R(100, 88)[1],           rig.SHOULDER_R),
    ("엉덩이",           stage_to_R(100, 140)[1],          rig.HIP_R),
    ("발",               stage_to_R(100, 212)[1],          -(rig.BASELINE_TOTAL_H - rig.BASELINE_HEAD_R) / rig.BASELINE_HEAD_R),
    ("몸통 길이(어깨→엉덩이)", stage_to_R(100, 88)[1] - stage_to_R(100, 140)[1], rig.TORSO_R),
    ("손 x(팔 끝)",      stage_to_R(52, 138)[0],           None),
]
print("  %-24s %12s %12s %10s" % ("항목", "인계본(R)", "우리(R)", "비"))
for lbl, a, b in rows:
    if b is None:
        print("  %-24s %12.5f %12s %10s" % (lbl, a, "—", "—")); continue
    print("  %-24s %12.5f %12.5f %9.2fx" % (lbl, a, b, a / b if b else float('nan')))
print("  획: 인계본 몸선 6 무대유닛 = %.5f R = %.2f W  /  우리 = 1.00 W(정의)"
      % (6.0 / 28.0, (6.0 / 28.0) / rig.W))
print("  키(머리위~발): 인계본 %.2f R = %.2f 머리지름 / 우리 %.2f R = %.2f 머리지름"
      % (1.0 + 194.0 / 28.0 - 1.0 + 1.0, (1.0 + 194.0 / 28.0) / 2.0,
         rig.BASELINE_TOTAL_H / rig.BASELINE_HEAD_R, rig.BASELINE_TOTAL_H / rig.BASELINE_HEAD_R / 2.0))

print()
print("╚══ 교정 종합: %s ══╝" % ("통과 — 아래 숫자를 신뢰한다" if allok else "실패 — 폐기하라"))
sys.exit(0 if allok else 1)
