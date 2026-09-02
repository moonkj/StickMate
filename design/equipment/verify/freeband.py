# -*- coding: utf-8 -*-
"""★ 슬롯별 「빈 방향」 지도 — 팩 신규 6종의 식별 잉크를 **어디에** 둘 것인가.
   봉투(기존 6종 프로파일 최대) 대비 도달 상한(게이트 단언)까지의 여유를 5도 구간으로 찍는다."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair, sectors as S

CL = S.SILHOUETTE_RATCHET_R      # 0.51580 R = 1.20획@0.60

# 도달 상한 r_cap(θ) — verify.py 가 슬롯마다 실제로 거는 경계 단언에서 그대로 옮긴다
def cap_head(d):
    a = math.radians(d); c, s = math.cos(a), math.sin(a)
    r = 9e9
    if s > 1e-9: r = min(r, 2.551 / s)          # 액자 상한 top<2.551
    if s < -1e-9: r = min(r, 1.0 / -s)          # 턱 아래 금지 bottom>-1.0
    return r
def cap_eyes(d):
    a = math.radians(d); c, s = math.cos(a), math.sin(a)
    r = 9e9
    if abs(c) > 1e-9: r = min(r, 1.60 / abs(c))  # |x|<1.6
    if s > 1e-9: r = min(r, 1.15 / s)            # 정수리 침범 y<1.15
    if s < -1e-9: r = min(r, 2.20 / -s)          # 목 아래 y>-2.2
    return r
def cap_neck(d):   # anchor = 어깨. y<0.0(절대) → 어깨기준 y < 1.318 ; y > HIP-0.517 = -5.608(절대)
    a = math.radians(d); s = math.sin(a); r = 9e9
    if s > 1e-9: r = min(r, (0.0 - rig.SHOULDER_R) / s)
    if s < -1e-9: r = min(r, ((rig.HIP_R - 0.517) - rig.SHOULDER_R) / s)
    return r
def cap_back(d):
    a = math.radians(d); s = math.sin(a); r = 9e9
    if s > 1e-9: r = min(r, (1.0 - rig.SHOULDER_R) / s)
    if s < -1e-9: r = min(r, (-9.3395 - rig.SHOULDER_R) / s)
    return r
def cap_hair(d):
    a = math.radians(d); s = math.sin(a); r = 9e9
    if s > 1e-9: r = min(r, 1.75 / s)            # 액자 1.75
    return min(r, 3.0)                            # 실용 상한(포니테일 2.42 기준 여유)

SLOTS = [("HEAD", items.HEAD, 0.0, cap_head), ("EYES", items.EYES, 0.0, cap_eyes),
         ("NECK", items.NECK, rig.SHOULDER_R, cap_neck),
         ("BACK", items.BACK, rig.SHOULDER_R, cap_back),
         ("HAIR", hair.SET, 0.0, cap_hair)]

# ── 교정 ──────────────────────────────────────────────────────────────────
print("╔══ 교정 ══╗")
print("  clearance(래칫) = %.5f R = %.2f획@0.60 = %.2f획@0.75"
      % (CL, CL / S.stroke_in_R(0.60), CL / S.stroke_in_R(0.75)))
print("  cap_eyes(0°)  = %.4f (기대 1.6000)  %s" % (cap_eyes(0), "OK" if abs(cap_eyes(0)-1.6)<1e-9 else "FAIL"))
print("  cap_head(270°)= %.4f (기대 1.0000)  %s" % (cap_head(270), "OK" if abs(cap_head(270)-1.0)<1e-9 else "FAIL"))
print("  cap_head(90°) = %.4f (기대 2.5510)  %s" % (cap_head(90), "OK" if abs(cap_head(90)-2.551)<1e-9 else "FAIL"))
env_probe = S.envelope(items.EYES, 0.0)
print("  EYES 봉투 최대 = %.4f (기대 = 고글 r_max 1.5400 근처)  %s"
      % (max(env_probe), "OK" if abs(max(env_probe)-1.54)<0.02 else "FAIL"))
print("╚══════════╝")

for name, tbl, anc, cap in SLOTS:
    env = S.envelope(tbl, anc)
    print("\n╔══ %s — 방향별 여유 (도달상한 − 기존봉투). 래칫 %.3fR 이상이면 ★ ══╗" % (name, CL))
    runs = []
    cur = None
    for i in range(72):
        d = i * 5.0
        head = cap(d) - env[i]
        free = head >= CL
        if free and cur is None: cur = [d, d]
        elif free: cur[1] = d
        elif cur is not None: runs.append(tuple(cur)); cur = None
    if cur is not None: runs.append(tuple(cur))
    if runs and runs[0][0] == 0.0 and runs[-1][1] == 355.0 and len(runs) > 1:
        runs[0] = (runs[-1][0] - 360.0, runs[0][1]); runs.pop()
    if not runs:
        print("  (래칫 여유가 있는 방향 없음)")
    for a, b in runs:
        idx = [int(((a + k * 5) % 360) / 5) for k in range(int((b - a) / 5) + 1)]
        hd = min(cap(i * 5) - env[i] for i in idx)
        print("  ★ %+6.0f° ~ %+6.0f°  (%2d구간)  최소여유 %.3fR = %.2f획@0.60"
              % (a, b, len(idx), hd, hd / S.stroke_in_R(0.60)))
    # 대표 방향 표
    print("  대표 방향   " + "".join("%8s" % ("%+d°" % d) for d in (0, 45, 90, 135, 180, 225, 270, 315)))
    print("  기존 봉투   " + "".join("%8.2f" % env[int(d/5)] for d in (0,45,90,135,180,225,270,315)))
    print("  도달 상한   " + "".join("%8.2f" % min(cap(d), 9.99) for d in (0,45,90,135,180,225,270,315)))
    print("  여유        " + "".join("%8.2f" % min(cap(d)-env[int(d/5)], 9.99) for d in (0,45,90,135,180,225,270,315)))
