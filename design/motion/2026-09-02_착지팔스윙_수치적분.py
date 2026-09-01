# -*- coding: utf-8 -*-
"""
MOTION_SPEC 20절(R2-M4) 재현 스크립트 — Fall -> LandingCrouch 팔/다리 각도 수치 적분.

프로덕션 코드를 수정하지 않고 "팔이 두 번 꺾이는가"를 판정하기 위한 오프라인 재현이다.
재현 대상(전부 원문 그대로):
  States/StickmanPoseAnimator.SmoothTo   : LerpAngle(cur, tgt, 1 - exp(-rate*dt))
  States/StickmanPoseAnimator.ApplyLimb  : 팔 rate = baseRate * ArmSmoothingRatio(0.55)
  States/StickmanPoseAnimator.ApplyFallPose / ApplyLandingCrouchPose : 목표각
  States/LandingCrouchState.Enter / EvaluateCrouchCurve               : 램프와 곡선
상수는 Assets/_Project/Data/DefaultStickConfig.asset 실값.

mode:
  None  = 2026-09-01까지의 거동(진입 블렌드 없음)
  'ss'  = ★ 2026-09-02에 **실제로 구현된 것** — 진입 블렌드 = 1 - smoothstep(t / 압축구간).
          구현 위치: States/LandingCrouchState.ComputeEntryBlend01 +
                     States/StickmanPoseAnimator.ApplyLandingCrouchPose(entryBlend01)
          구현은 상수(낙하 자세각)가 아니라 **Enter() 시점의 실제 각도 스냅샷**
          (StickmanPoseAnimator.CaptureLandingEntryPose)을 목표로 쓴다. 이 하니스는 낙하 자세로
          완전히 수렴한 상태에서 착지하므로 둘이 수치적으로 동일하다 — 아래 la(ft, fu0, e)가
          '스냅샷'을 그대로 재현한다(상수를 다시 적지 않는다).

  python3 2026-09-02_착지팔스윙_수치적분.py
"""
import math

# ── DefaultStickConfig.asset 실값 ─────────────────────────────────────────────
IDLE_ARM = 40.0; IDLE_LEG = 12.0; IDLE_KNEE = 4.0; IDLE_ELBOW = 10.0
F_ARM = 143.0; F_ELBOW = 20.0; F_LEGSPREAD = 15.0; F_HIP = 14.0; F_KNEE = 38.0
C_FHIP = 82.0; C_FKNEE = 126.0; C_RHIP = -40.0; C_RKNEE = 55.0
C_FARM = 64.0; C_RARM = -128.0
RATE = 48.0                 # landingCrouchPoseSmoothingRate
ARM_R = 0.55                # StickmanPoseAnimator.ArmSmoothingRatio
COMPRESS = 0.18             # landingCrouchCompressFraction
BRACE_TAIL = 7.10           # landingCrouchBraceTailHeights (2026-09-02: 2.60 -> 7.10)
REBOUND = 0.22              # landingCrouchReboundAmount
RISE_B = 0.62               # LandingCrouchState.RiseSpanBeforeRebound


def curve(t, c, h, r):
    t = max(0.0, min(1.0, t)); c = max(.01, min(.9, c)); h = max(0.0, min(.98 - c, h))
    if t <= c:
        u = t / c; return 1 - (1 - u) ** 3
    if t <= c + h:
        return 1.0
    rs = 1 - c - h
    rr = (t - c - h) / rs if rs > 1e-4 else 1.0
    if rr <= RISE_B:
        u = rr / RISE_B; return 1 - (u * u * (3 - 2 * u))
    b = (rr - RISE_B) / (1 - RISE_B); return -r * math.sin(math.pi * b)


def params(hH):
    """LandingCrouchState.Enter()의 세 램프."""
    rS, rc, ds, bs = 0.35, 0.88, 3.02, BRACE_TAIL   # soft / reaction / deep / braceTail
    t0 = max(0, min(1, (hH - rS) / (rc - rS)))
    t = max(0, min(1, (hH - rc) / ds))
    u = max(0, min(1, (hH - (rc + ds)) / bs))
    soft = hH < rc
    depth = (0.08 + (0.45 - 0.08) * t0) if soft else (0.45 + 0.55 * t)
    dur = (0.14 + (0.32 - 0.14) * t0) if soft else (0.32 + 0.30 * t + u * 0.26)
    hold = 0.24 + 0.16 * u
    return depth, dur, hold


def la(a, b, f):
    d = (b - a + 180) % 360 - 180
    return a + d * f


def run(hH, mode=None, dt=1 / 60, span=0.30):
    depth, dur, hold = params(hH)
    fu, ru = F_ARM, -F_ARM                     # 낙하 자세에서 수렴한 어깨각
    fh, rh = F_LEGSPREAD + F_HIP, -F_LEGSPREAD + F_HIP
    fu0, ru0, fh0, rh0 = fu, ru, fh, rh        # = CaptureLandingEntryPose()의 스냅샷
    rate, lrate = RATE * ARM_R, RATE
    cspan = COMPRESS * dur
    t = 0.0; out = []
    while t <= min(dur, span) + 1e-9:
        p = min(1, t / dur)
        a = curve(p, COMPRESS, hold, REBOUND) * depth
        down = max(0, min(1, a))
        ft = la(IDLE_ARM, C_FARM, down);  rt = la(-IDLE_ARM, C_RARM, down)
        fht = la(IDLE_LEG, C_FHIP, down); rht = la(-IDLE_LEG, C_RHIP, down)
        if mode == 'ss':
            # 구현과 같은 형태: 목표각을 "진입 스냅샷"(= 이 루프의 초기 각도)으로 되끌어당긴다.
            u = min(1.0, t / cspan); e = 1 - (u * u * (3 - 2 * u))
            ft = la(ft, fu0, e);  rt = la(rt, ru0, e)
            fht = la(fht, fh0, e); rht = la(rht, rh0, e)
        out.append((round(t * 1000, 1), round(a, 3), round(fu, 1), round(ru, 1), round(fh, 1), round(rh, 1)))
        k = 1 - math.exp(-rate * dt); kl = 1 - math.exp(-lrate * dt)
        fu = la(fu, ft, k); ru = la(ru, rt, k)
        fh = la(fh, fht, kl); rh = la(rh, rht, kl)
        t += dt
    return out, depth, dur


def reversal(seq):
    """단조에서 벗어나 '되돌아간' 최대 각도(도)."""
    ext = seq[0]; b = 0.0
    diffs = [x for x in (seq[i + 1] - seq[i] for i in range(len(seq) - 1)) if abs(x) > 1e-6]
    if not diffs:
        return 0.0
    d0 = 1 if diffs[0] > 0 else -1
    for v in seq:
        if (v - ext) * d0 > 0:
            ext = v
        b = max(b, (ext - v) * d0)
    return b


if __name__ == '__main__':
    print("낙차(hH)  뒷팔되돌림:현행 / 처방(ss)      앞엉덩이 첫프레임 딥:현행 / 처방")
    for hH in (0.9, 1.2, 1.6, 2.0, 2.4, 3.0, 3.9, 5.0, 6.5, 11.0, 17.44):
        cur, _, _ = run(hH, None)
        fix, _, _ = run(hH, 'ss')
        rc = reversal([r[3] for r in cur])
        rf = reversal([r[3] for r in fix])
        dc = cur[0][4] - min(r[4] for r in cur[:3])
        df = fix[0][4] - min(r[4] for r in fix[:3])
        print(f"{hH:7.2f}   {rc:8.1f} / {rf:8.1f}            {dc:8.1f} / {df:8.1f}")
    # ★ 실루엣 검산 — 다리(앞엉덩이)가 최종 목표의 특정 각도에 도달하는 시각이 바뀌지 않아야 한다.
    #   "팔을 고치느라 압축 박자를 늦췄다"가 아니라는 것을 수치로 남긴다.
    print("\n[압축 타이밍 불변 검산] 앞엉덩이가 81.0도에 처음 도달하는 시각(ms)  현행 / 처방(ss)")
    for hH in (0.9, 2.4, 6.5, 17.44):
        def reach81(rows):
            for r in rows:
                if r[4] >= 81.0:
                    return r[0]
            return None
        a = reach81(run(hH, None, span=1.2)[0])
        b = reach81(run(hH, 'ss', span=1.2)[0])
        print(f"{hH:7.2f}   {a} / {b}")

    print("\n[hH=17.44 현행] ms / amount / 앞팔 / 뒷팔 / 앞엉덩이 / 뒷엉덩이")
    for r in run(17.44, None)[0][:12]:
        print("  ", r)
    print("\n[hH=17.44 처방(ss)]")
    for r in run(17.44, 'ss')[0][:12]:
        print("  ", r)
