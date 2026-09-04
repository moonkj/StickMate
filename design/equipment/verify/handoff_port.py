# -*- coding: utf-8 -*-
"""⑥ 이식 사양 입력 — 어떤 단위로 옮겨야 하는가 (design-equipment, 2026-09-03).

교정(handoff_cal.py)이 밝힌 사실: **두 리그의 머리는 같고 몸통은 2배 다르다.**
  머리 반경 R      : 같다 (양쪽 정의)
  몸통(어깨→엉덩이) : 인계본 1.857 R  /  우리 3.773 R   = 0.49 배
⇒ HEAD/EYES 는 R 배수로 옮겨도 되고, NECK/BACK 은 **몸통 배수(TL)** 로 옮겨야 한다.
  이 파일이 그 재표현을 만든다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, handoff, items
from rig import Shape
from handoff import parse_items, build, to_R_shape, stage_to_R, OUR_NAME, ITEM_SLOT

W75 = rig.stroke_in_R(0.75)
FRAME = 1.75                       # AccessoryShapeBuilder.HairCapMaxRatio
H_TL = (46.0 - 88.0) / 28.0 - (46.0 - 140.0) / 28.0    # 인계본 몸통 = 1.857 R
O_TL = rig.TORSO_R                                      # 우리 몸통 = 3.773 R
H_SH = (46.0 - 88.0) / 28.0                             # 인계본 어깨 = -1.5 R
O_SH = rig.SHOULDER_R                                   # 우리 어깨 = -1.318 R

IT = parse_items()
P = {k: build(k, v) for k, v in IT.items()}
def RS(k): return [to_R_shape(k, p, "%s%d" % (p.call, i)) for i, p in enumerate(P[k])]

print("╔══ 교정 ══╗")
print("  [%s] 인계본 몸통 %.4f R · 우리 %.4f R · 비 %.3f"
      % ("OK" if abs(H_TL - 1.857) < 0.01 else "!!", H_TL, O_TL, H_TL / O_TL))
print("  [%s] 인계본 어깨 %.4f R · 우리 %.4f R" % ("OK", H_SH, O_SH))

# ── HEAD 4종 : 액자 · 감쌈 · 커버선 ────────────────────────────────────────
print()
print("╔══ 1. HEAD 4종 — 액자(1.75R) · 감쌈(원칙4) · 커버선 ══╗")
print("  원칙4 = |x| >= 0.85R 이면서 y <= 0.05R 인 잉크가 존재한다(왕관 면제)")
print("%-11s %9s %9s %9s %-9s %-11s %s" %
      ("아이템", "꼭대기R", "밑R", "폭R", "액자", "감쌈", "가장 낮은 잉크의 |x|"))
for k in ("clothhat", "furhat", "fedora", "crown"):
    S = RS(k)
    pts = [p for s in S for p in s.pts]
    x0, y0, x1, y1 = rig.bounds(pts)
    wrap = [(x, y) for x, y in pts if abs(x) >= 0.85 and y <= 0.05]
    low = min(pts, key=lambda p: p[1])
    print("%-11s %9.3f %9.3f %9.3f %-9s %-11s %.3f (y=%.3f)" %
          (k, y1, y0, x1 - x0,
           "★+%.3f" % (y1 - FRAME) if y1 > FRAME else "안",
           "있다" if wrap else "★없다", abs(low[0]), low[1]))
print("  ※ 우리 현행 (같은 자):")
for nm in ("야구모자", "털모자", "중절모", "왕관"):
    S = items.HEAD[nm]; pts = [p for s in S for p in s.pts]
    x0, y0, x1, y1 = rig.bounds(pts)
    wrap = [(x, y) for x, y in pts if abs(x) >= 0.85 and y <= 0.05]
    print("     %-9s 꼭대기 %+.3f 밑 %+.3f 폭 %.3f  액자 %s  감쌈 %s" %
          (nm, y1, y0, x1 - x0, "★초과" if y1 > FRAME else "안",
           "있다" if wrap else "★없다"))

# ── NECK / BACK : 몸통 단위 재표현 ────────────────────────────────────────
print()
print("╔══ 2. NECK / BACK — 몸통(TL) 단위로 재표현하면 얼마나 맞는가 ══╗")
print("  변환: y_ours = SH_ours + (y_handoff − SH_handoff) × (TL_ours / TL_handoff)")
print("  배수 TL_ours/TL_handoff = %.4f" % (O_TL / H_TL))
print()
print("%-13s %11s %11s %13s %11s %s" %
      ("아이템", "인계본 밑R", "→TL재표현", "우리 현행 밑R", "TL배수(인)", "TL배수(우)"))
def retl(y): return O_SH + (y - H_SH) * (O_TL / H_TL)
for k in ("bowtie", "stripedtie", "scarf", "bellnecklace",
          "shortcape", "longcape", "wings", "backpack"):
    S = RS(k); pts = [p for s in S for p in s.pts]
    hy0 = rig.bounds(pts)[1]
    slot, nm = OUR_NAME[k]
    ours = {"NECK": items.NECK, "BACK": items.BACK}[slot][nm]
    oy0 = rig.bounds([p for s in ours for p in s.pts])[1]
    print("%-13s %11.3f %11.3f %13.3f %11.3f %11.3f" %
          (k, hy0, retl(hy0), oy0, (H_SH - hy0) / H_TL, (O_SH - oy0) / O_TL))
print("  ⇒ 'TL배수'가 두 열에서 가까우면 **인계본과 우리가 같은 비례**를 쓴 것이다.")
print("     R 배수로는 2배 갈라져 보이지만 몸통 단위로는 붙는다 — 이식 단위의 근거.")

# ── 망토 착용 전용 도형 (무대 좌표) ────────────────────────────────────────
print()
print("╔══ 3. 망토 착용 전용 도형 — 인계본이 아이콘과 나눈 유일한 자리 ══╗")
CAPES = {
    "긴망토 뒤판":  "M86 78 Q68 138 70 184 Q100 193 130 184 Q132 138 114 78 Z",
    "짧은망토 뒤판": "M87 78 Q74 106 74 132 Q100 140 126 132 Q126 106 113 78 Z",
    "공통 칼라":    "M74 72 Q100 88 126 72 L128 80 Q100 96 72 80 Z",
}
print("%-14s %10s %10s %10s %10s %10s %s" %
      ("도형", "위 y R", "밑 y R", "폭 R", "TL배수", "→우리y R", "우리 현행 밑"))
for nm, d in CAPES.items():
    pts, _ = handoff.flatten_path(d)
    R = [stage_to_R(x, y) for x, y in pts]
    x0, y0, x1, y1 = rig.bounds(R)
    print("%-14s %10.3f %10.3f %10.3f %10.3f %10.3f" %
          (nm, y1, y0, x1 - x0, (H_SH - y0) / H_TL, retl(y0)))
lc = rig.bounds([p for s in items.longcape() for p in s.pts])
sc = rig.bounds([p for s in items.cape() for p in s.pts])
print("  우리 긴망토 밑 %+.3f R (TL배수 %.3f) · 짧은망토 밑 %+.3f R (TL배수 %.3f)"
      % (lc[1], (O_SH - lc[1]) / O_TL, sc[1], (O_SH - sc[1]) / O_TL))
clasp_r = 4.6 / 28.0
print("  클래스프 circle(100,85) r=4.6 → 중심 %s R · 반경 %.4f R = **%.2f W** → %s"
      % (("(%+.3f,%+.3f)" % stage_to_R(100, 85)), clasp_r, clasp_r * 2 / W75,
         "존재" if clasp_r * 2 / W75 >= 1.5 else "★ 소멸"))
print("  뒤판 보더 1.8~2px(무대 158px 기준) = %.4f R = **%.2f W**"
      % (2.0 / handoff.K_PX / 28.0, (2.0 / handoff.K_PX / 28.0) / W75))
print("  ★ 인계본 무대 획 6 무대유닛 = %.2f W — 인계본은 **우리보다 굵은 몸선 대비 가는 장비선**을 쓴다."
      % ((6.0 / 28.0) / W75))

# ── 인계본에 없는 8종 : 규칙 3개 적용 가능성 ──────────────────────────────
print()
print("╔══ 4. 인계본에 없는 8종 — 형제 아이템에서 무엇을 물려받는가 ══╗")
SIBLING = {
    "beret":       ("clothhat", "HEAD", "베레모"),
    "straw":       ("clothhat", "HEAD", "밀짚모자"),
    "browline":    ("goggles",  "EYES", "뿔테안경"),
    "patch":       ("monocle",  "EYES", "안대"),
    "bandana":     ("scarf",    "NECK", "반다나"),
    "pendant":     ("bellnecklace", "NECK", "펜던트"),
    "fairy_wings": ("wings",    "BACK", "요정날개"),
    "poncho":      ("shortcape","BACK", "판초"),
}
print("%-13s %-14s %-11s %8s %8s %s" %
      ("없는 8종", "형제(인계본)", "우리 이름", "형제조각", "형제생존", "물려받을 구성"))
for k, (sib, slot, nm) in SIBLING.items():
    S = RS(sib)
    surv = sum(1 for s in S
               if max(rig.bounds(s.pts)[2] - rig.bounds(s.pts)[0],
                      rig.bounds(s.pts)[3] - rig.bounds(s.pts)[1]) / W75 >= 1.5)
    calls = " ".join(p.call for p in P[sib])
    print("%-13s %-14s %-11s %8d %8d %s" % (k, sib, nm, len(S), surv, calls))
