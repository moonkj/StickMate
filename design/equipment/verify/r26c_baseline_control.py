# -*- coding: utf-8 -*-
"""R26c — 대조군: «망토 위에 본체 잉크가 얹히는 것»은 뒷짐이 만든 일인가.

R26/R26b가 뒷짐의 겹침을 쟀다. 그 숫자가 «크다/작다»를 말하려면 **이미 출하된 그림**의
같은 숫자가 있어야 한다. 이 스크립트가 그 자를 만든다:

  (가) 보행 사이클 팔 — ArmShoulderKeys{18,0,−18,0}는 한 사이클에 두 번 **어깨 0°**를 지난다.
       그때 팔은 수직이고, 망토 뒤판(x ±1.08 R)의 한가운데다. 이미 출하된 그림이다.
  (나) 중립 다리 — 다리는 sortingOrder 0, 긴망토 뒤판 채움은 −2다. 긴망토 밑단(−9.055 R)은
       발바닥(−9.340 R)보다 위이므로 **다리 전체가 망토 위에 그려진다**. 역시 출하된 그림이다.

두 대조가 뒷짐보다 크면, 뒷짐의 겹침은 «새 결함»이 아니라 «이 망토 조형의 성질»이다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r26_capestance as B
import r26b_capestance_detail as D
import re

boot = B.read(B.BOOT); cfg = B.read(B.CONFIG); pose = B.read(B.POSE)

LEG_U = float(re.search(r"BaselineLegUpperLength\s*=\s*([\d.]+)f", boot).group(1)) / B.R
LEG_L = float(re.search(r"BaselineLegLowerLength\s*=\s*([\d.]+)f", boot).group(1)) / B.R
LEG_SPREAD = B.field_f(cfg, "idleLegSpreadDegrees")
KNEE_IDLE  = B.field_f(cfg, "idleKneeBendDegrees")
KNEE_SIGN  = B.const_f(pose, "KneeBendSign")
LEG_W = max(float(re.search(r"BaselineLegLineWidth\s*=\s*([\d.]+)f", boot).group(1)) * B.LINE_W_SCALE, 0)

def arr(src, name):
    m = re.search(r"%s\s*=\s*\{([^}]*)\}" % re.escape(name), src)
    return [float(x) for x in re.findall(r"(-?[\d.]+)f", m.group(1))]

ARM_SH = arr(pose, "ArmShoulderKeys")   # {18, 0, -18, 0}
ARM_EL = arr(pose, "ArmElbowKeys")      # {15, 20, 25, 20}

def sample(keys, t):
    n = len(keys); x = (t % 1.0) * n
    i = int(x) % n; f = x - int(x)
    return keys[i] + (keys[(i+1) % n] - keys[i]) * f

def leg_width_R(scale):
    world = max(LEG_W * scale, B.MIN_STROKE_PT / B.PT_PER_UNIT)
    return world / (B.R * scale)

def main():
    print("="*104)
    print("R26c — 대조군: 보행 팔 · 중립 다리   (design-equipment / 2026-09-06)")
    print("="*104)
    print("다리(R 배수): 대퇴 %.4f · 정강이 %.4f · 벌림 %.1f° · Idle 무릎 %.1f°(부호 %+.0f)"
          % (LEG_U, LEG_L, LEG_SPREAD, KNEE_IDLE, KNEE_SIGN))
    print("보행 팔 키표: 어깨 %s · 팔꿈치 %s" % (ARM_SH, ARM_EL))

    for scale in (0.75, 1.00):
        w = B.arm_width_R(scale); half = 0.5*w
        lw = leg_width_R(scale); lhalf = 0.5*lw
        pt = B.R_to_pt(scale)
        print("\n배율 %.2f (1R = %.3f pt · 팔 획 %.3f R · 다리 획 %.3f R)" % (scale, pt, w, lw))
        for nm, cape in B.CAPES.items():
            back, collar, clasp = cape["back"], cape["collar"], cape["clasp"]
            # (가) 보행 팔 — 한 사이클 32표본에서 최악
            worst = None
            for k in range(32):
                t = k/32.0
                u = sample(ARM_SH, t); l = B.ELBOW_SIGN * sample(ARM_EL, t)
                L = B.arm_points(u, l, w)
                a = B.ink_overlap_area(L, half, back, minus=(collar, clasp), step=0.02)
                if worst is None or a > worst[0]:
                    worst = (a, t, u, l, L)
            a, t, u, l, L = worst
            nx, seq = D.crossings(L, back)
            tip = L[-1]
            print("  ■ %s / 보행 팔 최악 t=%.3f (θu=%+.1f θl=%+.1f)" % (nm, t, u, l))
            print("      노출 잉크 %.4f R² (%.2f pt²) | 경계 교차 %d회 (%s) | 손끝 %s · 잉크 돌출 %+.4f R (%+.2f pt)"
                  % (a, a*pt*pt, nx, seq, "안" if B.contains(back, tip) else "밖",
                     D.ink_out(back, tip, half), D.ink_out(back, tip, half)*pt))
            # (나) 중립 다리
            hip = (0.0, B.HIP_R)
            legs = []
            for sign in (+1.0, -1.0):
                u = sign * LEG_SPREAD; l = KNEE_SIGN * KNEE_IDLE
                pts = B.limb_polyline(LEG_U, LEG_L, l, lw)
                a_ = math.radians(u); c, s = math.cos(a_), math.sin(a_)
                legs.append([(hip[0] + p[0]*c - p[1]*s, hip[1] + p[0]*s + p[1]*c) for p in pts])
            la = D.union_area(legs, lhalf, back, minus=(collar, clasp), step=0.015)
            ins = [B.centerline_inside_length(L2, back) for L2 in legs]
            tot = [B.polyline_length(L2) for L2 in legs]
            print("  ■ %s / 중립 다리 두 짝 합집합 노출 잉크 %.4f R² (%.2f pt²) — 중심선 잠김 %.0f%%/%.0f%%"
                  % (nm, la, la*pt*pt, 100*ins[0]/tot[0], 100*ins[1]/tot[1]))

if __name__ == "__main__":
    main()
