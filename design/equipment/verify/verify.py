# -*- coding: utf-8 -*-
import sys, math; sys.path.insert(0,'.')
import rig, items, hair
from rig import W, Shape
ICON,FIT,IST=44.0,0.86,1.7*44/40
CATS=[("HEAD",items.HEAD,0.0),("EYES",items.EYES,0.0),("NECK",items.NECK,rig.SHOULDER_R),
      ("BACK",items.BACK,rig.SHOULDER_R),("HAIR",hair.SET,0.0)]
fail=0
def bad(msg):
    global fail; fail+=1; print("  ✗",msg)

print("╔══ 30종 전수 검산 (배율 0.75, W = %.4fR) ══╗"%W)
for cat,d,anchor in CATS:
    for n,sh in d.items():
        pts=[p for s in sh for p in s.pts]
        # 규칙 1
        for s in sh:
            v=rig.rule_one(s,W)
            if v: bad("%s %s 규칙1: %s"%(cat,n,v))
            if s.loop and rig.self_intersects(s.pts): bad("%s %s '%s' 자기교차"%(cat,n,s.name))
        # 규칙 5 정원 / 규칙 3-2 보조색
        if not (2<=len(sh)<=4): bad("%s %s 정원 %d"%(cat,n,len(sh)))
        if sum(1 for s in sh if s.tone==1)!=1: bad("%s %s 보조색 %d"%(cat,n,sum(1 for s in sh if s.tone==1)))
        # 규칙 2 채움
        if cat in ("EYES","HAIR","HEAD") and not any(s.filled for s in sh): bad("%s %s 채움 없음"%(cat,n))
        # 카드
        x0,y0,x1,y1=rig.bounds(pts); k=ICON*FIT/max(x1-x0,y1-y0)
        for s in sh:
            v=rig.rule_one(Shape(s.name,[(x*k,y*k) for x,y in s.pts],s.loop,s.filled,s.tone),IST)
            if v: bad("%s %s 카드: %s"%(cat,n,v))
        # 다른 톤끼리 채움 겹침이면 순서 의존 -> 경고만(의도된 것은 통과)
    ks=list(d); pr={x:rig.profile(d[x],anchor) for x in ks}; worst=(None,99)
    for i in range(len(ks)):
        for j in range(i+1,len(ks)):
            v=rig.max_delta(pr[ks[i]],pr[ks[j]])/W
            if v<worst[1]: worst=((ks[i],ks[j]),v)
    if worst[1]<1.0: bad("%s 쌍 %s가 %.2f획"%(cat,worst[0],worst[1]))
    print("  %s 쌍별 최소 실루엣 차 %.2f획 (%s vs %s)"%(cat,worst[1],worst[0][0],worst[0][1]))

# 슬롯별 경계
for n,sh in items.HEAD.items():
    p=[q for s in sh for q in s.pts]; t=max(q[1] for q in p); b=min(q[1] for q in p)
    if not (1.0<t<2.551): bad("HEAD %s 꼭대기 %.2f"%(n,t))
    if b<=-1.0: bad("HEAD %s 턱 아래 %.2f"%(n,b))
    if not any(abs(q[0])>=0.85 and q[1]<=0.05 for q in p) and n!="왕관": bad("HEAD %s 감쌈 실패"%n)
for n,sh in items.EYES.items():
    p=[q for s in sh for q in s.pts]
    if max(abs(q[0]) for q in p)>=1.6: bad("EYES %s |x|>=1.6"%n)
    if max(q[1] for q in p)>=1.15: bad("EYES %s 정수리 침범"%n)
    if min(q[1] for q in p)<=-2.2: bad("EYES %s 목 아래"%n)
    f=[s for s in sh if s.filled]
    if not any(rig.contains(s.pts,(rig.EYE_X,rig.EYE_Y)) for s in f): bad("EYES %s 앞눈 미커버"%n)
    back=any(rig.contains(s.pts,(-rig.EYE_X,rig.EYE_Y)) for s in f)
    if (n in items.EYE_FRONT_ONLY)==back: bad("EYES %s 뒤눈 커버=%s"%(n,back))
for n,sh in items.NECK.items():
    p=[q for s in sh for q in s.pts]
    if max(q[1] for q in p)>=0.0: bad("NECK %s 얼굴 침범"%n)
    if min(q[1] for q in p)<=rig.HIP_R-0.517: bad("NECK %s 고관절 아래"%n)
for n,sh in items.BACK.items():
    p=[q for s in sh for q in s.pts]
    if max(q[1] for q in p)>=1.0: bad("BACK %s 정수리 위"%n)
    if min(q[1] for q in p)<=-9.3395: bad("BACK %s 바닥 관통"%n)
for n,sh in hair.SET.items():
    p=[q for s in sh for q in s.pts]
    mn=min(math.hypot(*q) for q in p); mx=max(math.hypot(*q) for q in p); t=max(q[1] for q in p)
    if mn>1-W: bad("HAIR %s 부착 %.3f"%(n,mn))
    if mx<1.05: bad("HAIR %s 두피 안"%n)
    if t>1.75: bad("HAIR %s 액자 초과 %.2f"%(n,t))
    for s in sh:
        if not s.filled: continue
        for sx in (1,-1):
            for dx,dy in ((0,0),(rig.PUPIL_R,0),(-rig.PUPIL_R,0),(0,rig.PUPIL_R),(0,-rig.PUPIL_R)):
                if rig.contains(s.pts,(sx*rig.EYE_X+dx, rig.EYE_Y+dy)): bad("HAIR %s 눈동자 침범"%n)
# 눈 노출 2종
for n in items.EYE_FRONT_ONLY:
    sh=items.EYES[n]; eye=[s for s in sh if s.name.endswith("Eye")][0]
    vis=[s for s in sh if s.filled and s is not eye][0]
    g=rig.bounds(vis.pts)[0]-rig.bounds(eye.pts)[2]
    if g<1.5*W: bad("EYES %s 눈-가리개 간격 %.2f획"%(n,g/W))
    for s in sh:
        if s.loop: continue
        gg=rig.stroke_gap(s.pts,eye.pts)
        if not (gg<1e-6 or gg>=1.5*W): bad("EYES %s '%s' 눈과 %.2f획(최악 구간)"%(n,s.name,gg/W))
print("╚══ 결과: %s ══╝"%("전수 통과 (위반 0건)" if fail==0 else "위반 %d건"%fail))

# ══════════════════════════════════════════════════════════════════════════════
# ★ 배율 축 — 상시 검산 지점 (2026-09-01 추가, design-equipment)
#   0.35/0.75/1.0/1.5만 재던 관행이 **0.60 구멍**을 만들었다. 사용자의 저장 배율이 0.60이었고
#   그 배율에서 규칙 1 위반이 11건이었는데 아무도 못 봤다. 이제 verify.py가 매번 훑는다.
# ══════════════════════════════════════════════════════════════════════════════
def _W(s): return max(0.048*s, 2.0/35.25) / (0.22*s)
_SCALES = [0.35, 0.50, 0.60, 0.75, 1.00, 1.50]
_ALL = [("HEAD",items.HEAD),("EYES",items.EYES),("NECK",items.NECK),("BACK",items.BACK),("HAIR",hair.SET)]
print()
print("╔══ 배율 축 (상시) ══╗")
for _s in _SCALES:
    _w=_W(_s); _v=[]
    for _c,_t in _ALL:
        for _n,_sh in _t.items():
            for _x in _sh:
                _m=rig.rule_one(_x,_w)
                if _m: _v.append("%s %s %s"%(_c,_n,_x.name))
    _note = "  ← 9-5절이 면제한 실루엣 전용 구간" if _s<=0.35 else ("  ← 사용자 저장 배율(실측)" if abs(_s-0.60)<1e-9 else ("  ← 출하 기본" if abs(_s-0.75)<1e-9 else ""))
    print("  배율 %.2f  W=%.4f R  규칙1 위반 %3d건%s"%(_s,_w,len(_v),_note))
_lo,_hi=0.20,1.20
for _ in range(48):
    _m=(_lo+_hi)/2
    if any(rig.rule_one(_x,_W(_m)) for _c,_t in _ALL for _n,_sh in _t.items() for _x in _sh): _lo=_m
    else: _hi=_m
_last=[(_c,_n,_x.name) for _c,_t in _ALL for _n,_sh in _t.items() for _x in _sh if rig.rule_one(_x,_W(_lo))]
print("  ★ 규칙 1 위반 0이 되는 **최소 배율 = %.4f** (출하 0.75까지 여유 %.4f)"%(_hi,0.75-_hi))
print("     마지막까지 남는 것: %s"%", ".join("%s %s %s"%t for t in _last[:3]))
print("  자기교차: 좌표가 R 배수라 배율 변환이 닮음 → 배율 불변. 0.75 결과가 전 배율 결과다.")
print("╚═══════════════════╝")
