# -*- coding: utf-8 -*-
"""머리 액세서리(sortingOrder 6)가 '주위 살피기' 손끝을 가리는가.

입력(전부 남의 실측을 그대로 인용 — 내가 지어낸 값이 없다):
 · docs/MOTION_SPEC.md 13-3/13-4 — 손끝은 머리 중심에서 **1.179 R**, 방향 β=52.8°(0°=정수리, +x=진행).
   (0.1946 로컬 / 머리 외곽 반지름 0.165 = 1.179)
 · 팔 획 반폭 0.03919 로컬 = **0.2375 R** → 손끝 캡은 반지름 0.2375 R의 원.
 · 액세서리 획 W(R 배수)는 배율마다 다르다(2pt 바닥):  W = max(0.048·s, 2/35.25) / (0.22·s)
좌표: docs/EQUIPMENT_SHAPE_SPEC.md 부록 A(머리 중심 원점, R 배수, +x 진행).
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig, items, hair
from rig import contains, bounds

BETA = 52.8
HAND_R  = 1.179          # 손끝 ~ 머리중심 (R)
CAP_R   = 0.2375         # 손끝 캡 반지름 (R) — 팔 획 반폭
HAND = (math.sin(math.radians(BETA))*HAND_R, math.cos(math.radians(BETA))*HAND_R)

def W_at(s): return max(0.048*s, 2.0/35.25) / (0.22*s)
SCALES = [0.35, 0.60, 0.75, 1.00]

def seg_dist(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]; L2=dx*dx+dy*dy
    t=0.0 if L2<1e-12 else max(0.0,min(1.0,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/L2))
    return math.hypot(p[0]-(a[0]+dx*t), p[1]-(a[1]+dy*t))

def ink_distance(shapes, q, w):
    """점 q에서 이 아이템의 **잉크**까지의 부호 있는 거리(음수 = 잉크 안). 획 반폭 w/2를 얹는다."""
    best = 1e9
    for s in shapes:
        n=len(s.pts)
        if s.filled and contains(s.pts, q): return -1e-3
        for i in range(n if s.loop else n-1):
            d = seg_dist(q, s.pts[i], s.pts[(i+1)%n]) - w/2.0
            best = min(best, d)
    return best

print("╔══ 머리 액세서리가 '주위 살피기' 손끝을 가리는가 ══╗")
print("  손끝 = (%.3f, %.3f) R  ·  β=%.1f° ·  머리중심에서 %.3f R (실루엣 1.0R 바로 바깥)" % (HAND[0],HAND[1],BETA,HAND_R))
print("  손 캡 반지름 %.4f R · 액세서리 획 W: " % CAP_R
      + " / ".join("배율 %.2f → %.3f R" % (s, W_at(s)) for s in SCALES))
print()
hdr = "%-10s %-9s " % ("아이템","최대반경") + " ".join("%-11s"%("s=%.2f"%s) for s in SCALES) + "  판정"
print(hdr); print("-"*100)
def run(cat, table):
    for nm, sh in table.items():
        pts=[p for s in sh for p in s.pts]
        rmax=max(math.hypot(*p) for p in pts)
        cells=[]; hidden_any=False; hidden_all=True
        for s in SCALES:
            d = ink_distance(sh, HAND, W_at(s))
            # 손 캡 전체가 잉크 안이면 완전 가림 / 캡이 잉크에 걸치면 부분 가림
            if d <= -CAP_R: tag="완전가림"; hidden_any=True
            elif d < CAP_R: tag="일부가림 %.2f"%d; hidden_any=True; hidden_all=False
            else: tag="안 가림 %.2f"%d; hidden_all=False
            cells.append("%-11s"%tag)
        verdict = ("★ 가림" if hidden_any else "영향 없음")
        print("%-10s %-9s "%(nm,"%.2f R"%rmax) + " ".join(cells) + "  " + verdict)
print("[HEAD 모자 6종]"); run("HEAD", items.HEAD)
print("[HAIR 머리 6종]"); run("HAIR", hair.SET)

# 챙 반지름 — 모션 담당이 미확인으로 남긴 숫자
print()
print("── 모자별 '앞(진행 방향) 최대 뻗음'과 손끝 방향 반경 (모션 담당 요청 숫자) ──")
print("%-10s %-14s %-16s %-16s %s"%("모자","앞 최대 x","β=52.8° 방향 반경","손끝 반경 1.179","여유(R)"))
for nm, sh in items.HEAD.items():
    pts=[p for s in sh for p in s.pts]
    fx=max(p[0] for p in pts)
    # β 방향으로 광선을 쏴 잉크 바깥 경계까지의 반경
    u=(math.sin(math.radians(BETA)), math.cos(math.radians(BETA)))
    far=0.0; t=0.0
    while t<3.0:
        q=(u[0]*t, u[1]*t)
        if ink_distance(sh,q,rig.W) <= 0.0: far=t
        t+=0.005
    print("%-10s %-14s %-16s %-16s %s"%(nm,"%.2f R"%fx,"%.3f R"%far,"1.179 R","%+.3f"%(HAND_R-far)))

print()
print("── 머리 6종도 같은 자로 (β=52.8° 방향 잉크 반경) ──")
u=(math.sin(math.radians(BETA)), math.cos(math.radians(BETA)))
def ray_radius(sh, w, beta=BETA):
    uu=(math.sin(math.radians(beta)), math.cos(math.radians(beta)))
    far=0.0; t=0.0
    while t<3.2:
        if ink_distance(sh,(uu[0]*t, uu[1]*t), w) <= 0.0: far=t
        t+=0.005
    return far
for nm, sh in hair.SET.items():
    far=ray_radius(sh, rig.W)
    print("  %-10s %.3f R   여유 %+.3f"%(nm, far, HAND_R-far))

print()
print("── 손끝이 **모든** 머리 액세서리 잉크 밖으로 나오려면 (배율 0.75 기준) ──")
worst=0; who=None
for cat,tbl in (("모자",items.HEAD),("머리",hair.SET)):
    for nm,sh in tbl.items():
        r=ray_radius(sh, rig.W)
        if r>worst: worst,who=r,(cat,nm)
print("  필요 반경 ≥ %.3f R  (지금 %.3f R · 부족 %+.3f R) — 가장 멀리 나온 것: %s %s"
      %(worst, HAND_R, worst-HAND_R, who[0], who[1]))

print()
print("── β를 바꾸면? 방향별 '모든 액세서리를 덮는 최대 잉크 반경' (배율 0.75) ──")
print("  %-8s %-10s %s"%("β(도)","필요 반경","가장 멀리 나온 것"))
for beta in (20,30,40,52.8,65,80,90,105,120):
    worst=0; who=None
    for cat,tbl in (("모자",items.HEAD),("머리",hair.SET)):
        for nm,sh in tbl.items():
            r=ray_radius(sh, rig.W, beta)
            if r>worst: worst,who=r,nm
    print("  %-8s %-10s %s"%("%.1f"%beta, "%.3f R"%worst, who))

print()
print("── 배율별 요구 반경 (β=52.8°, 액세서리 획이 배율에 반비례해 R 배수로 굵어진다) ──")
print("  %-8s %-9s %-14s %-14s %s"%("배율","W(R)","중심선 기준","손 전체 기준","가장 멀리"))
for s in SCALES:
    w=W_at(s); worst=0; who=None
    for tbl in (items.HEAD, hair.SET):
        for nm,sh in tbl.items():
            r=ray_radius(sh, w)
            if r>worst: worst,who=r,nm
    print("  %-8s %-9s %-14s %-14s %s"%("%.2f"%s,"%.3f"%w,"≥ %.3f R"%worst,"≥ %.3f R"%(worst+CAP_R),who))
