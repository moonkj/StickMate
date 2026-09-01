# -*- coding: utf-8 -*-
"""StickMate 액세서리 도형 오프라인 검산 자(尺).

프로덕션과 같은 규칙을 그대로 옮긴다:
 · W  = AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii  (배율 0.75)
 · 규칙1 = AccessoryStrokeBudgetTests.DescribeRuleOneViolation
 · 실루엣 프로파일 = AccessorySilhouetteMetrics.ProfileOf (72구간 x 5도, 변 조밀표본)
좌표계: 머리 중심 원점, +x = 진행 방향, 단위 = 머리 반경 R.
"""
import math

# ---- 프로덕션 상수(그대로 옮김) -------------------------------------------------
BASELINE_TOTAL_H   = 2.2746944
BASELINE_HEAD_R    = 0.22
BASELINE_SHOULDER  = 1.7646944
BASELINE_HIP       = 0.9346944
BASELINE_STROKE_W  = 0.048
MIN_STROKE_PT      = 2.0
PT_PER_UNIT        = 846.0 / (2.0 * 12.0)      # 35.25
SHIP_SCALE         = 0.75

def stroke_in_R(scale=SHIP_SCALE):
    stroke = max(BASELINE_STROKE_W * scale, MIN_STROKE_PT / PT_PER_UNIT)
    return stroke / (BASELINE_HEAD_R * scale)

W = stroke_in_R()                                # 0.343864 R
HEAD_CENTER = BASELINE_TOTAL_H - BASELINE_HEAD_R # 2.0546944
SHOULDER_R  = (BASELINE_SHOULDER - HEAD_CENTER) / BASELINE_HEAD_R   # -1.31818 R
HIP_R       = (BASELINE_HIP      - HEAD_CENTER) / BASELINE_HEAD_R   # -5.09091 R
TORSO_R     = (BASELINE_SHOULDER - BASELINE_HIP) / BASELINE_HEAD_R  #  3.77273 R
EYE_X       = 0.075 / 0.22                       # 0.340909
EYE_Y       = 0.020 / 0.22                       # 0.090909
PUPIL_R     = 0.030 / 0.22                       # 0.136364
CORNER_DEG  = 45.0

# ---- 도형 ---------------------------------------------------------------------
class Shape:
    def __init__(self, name, pts, loop=True, filled=False, tone=0, sort=0):
        self.name, self.pts, self.loop, self.filled = name, [(float(a),float(b)) for a,b in pts], loop, filled
        self.tone, self.sort = tone, sort
    def __repr__(self): return "Shape(%s,%d점)" % (self.name, len(self.pts))

def polar(deg, r):
    a = math.radians(deg); return (math.cos(a)*r, math.sin(a)*r)

def arc(r, d0, d1, n):
    return [polar(d0 + (d1-d0)*i/(n-1), r) for i in range(n)]

def poly(cx, cy, r, n, start_deg=0.0):
    return [(cx + math.cos(math.radians(start_deg)+2*math.pi*i/n)*r,
             cy + math.sin(math.radians(start_deg)+2*math.pi*i/n)*r) for i in range(n)]

# ---- 규칙 1 (프로덕션 린트와 동일) ------------------------------------------------
def bounds(pts):
    xs=[p[0] for p in pts]; ys=[p[1] for p in pts]
    return min(xs),min(ys),max(xs),max(ys)

def turn_deg(a,b,c):
    v1=(b[0]-a[0],b[1]-a[1]); v2=(c[0]-b[0],c[1]-b[1])
    if v1[0]**2+v1[1]**2 < 1e-12 or v2[0]**2+v2[1]**2 < 1e-12: return 0.0
    d=(v1[0]*v2[0]+v1[1]*v2[1])/(math.hypot(*v1)*math.hypot(*v2))
    return math.degrees(math.acos(max(-1.0,min(1.0,d))))

def rule_one(shape, w=W):
    p=shape.pts; n=len(p)
    if n<2: return "점 %d개" % n
    x0,y0,x1,y1 = bounds(p)
    span = max(x1-x0, y1-y0)
    if span < w*1.5:
        return "잉크 사각형 %.2f획 < 1.5획" % (span/w)
    corner=[False]*n
    rng = range(n) if shape.loop else range(1,n-1)
    for i in rng:
        corner[i] = turn_deg(p[(i-1)%n], p[i], p[(i+1)%n]) >= CORNER_DEG
    segs = n if shape.loop else n-1
    for i in range(segs):
        j=(i+1)%n
        if not (corner[i] and corner[j]): continue
        L=math.dist(p[i],p[j])
        if L < w:
            return "%d->%d 변이 %.2f획 < 1.0획(양끝 꺾임)" % (i,j,L/w)
    return None

def min_corner_seg(shape, w=W):
    """양끝이 모두 꺾임인 변 중 가장 짧은 것(획 배수). 없으면 None."""
    p=shape.pts; n=len(p); corner=[False]*n
    rng = range(n) if shape.loop else range(1,n-1)
    for i in rng: corner[i]=turn_deg(p[(i-1)%n],p[i],p[(i+1)%n])>=CORNER_DEG
    best=None
    for i in range(n if shape.loop else n-1):
        j=(i+1)%n
        if corner[i] and corner[j]:
            L=math.dist(p[i],p[j])/w
            best=L if best is None else min(best,L)
    return best

# ---- 점 포함 / 자기교차 ----------------------------------------------------------
def contains(pts, q):
    inside=False; n=len(pts)
    for i in range(n):
        a=pts[i]; b=pts[(i+1)%n]
        if (a[1]>q[1]) != (b[1]>q[1]):
            x=a[0]+(q[1]-a[1])*(b[0]-a[0])/(b[1]-a[1])
            if q[0]<x: inside = not inside
    return inside

def seg_int(p1,p2,p3,p4):
    d=(p2[0]-p1[0])*(p4[1]-p3[1])-(p2[1]-p1[1])*(p4[0]-p3[0])
    if abs(d)<1e-12: return False
    t=((p3[0]-p1[0])*(p4[1]-p3[1])-(p3[1]-p1[1])*(p4[0]-p3[0]))/d
    u=((p3[0]-p1[0])*(p2[1]-p1[1])-(p3[1]-p1[1])*(p2[0]-p1[0]))/d
    return 1e-9<t<1-1e-9 and 1e-9<u<1-1e-9

def self_intersects(pts):
    n=len(pts)
    for i in range(n):
        for j in range(i+1,n):
            if j==i or (i==0 and j==n-1) or j==i+1: continue
            if seg_int(pts[i],pts[(i+1)%n],pts[j],pts[(j+1)%n]): return (i,j)
    return None

# ---- 실루엣 프로파일(72구간) ------------------------------------------------------
BINS=72; BIN_DEG=5.0; EDGE_SAMPLES=64
def profile(shapes, anchor_y=0.0):
    prof=[0.0]*BINS
    for s in shapes:
        p=s.pts; n=len(p); segs = n if s.loop else n-1
        for i in range(segs):
            a=p[i]; b=p[(i+1)%n]
            for k in range(EDGE_SAMPLES+1):
                t=k/EDGE_SAMPLES
                x=a[0]+(b[0]-a[0])*t; y=a[1]+(b[1]-a[1])*t-anchor_y
                r=math.hypot(x,y)
                d=math.degrees(math.atan2(y,x)) % 360.0
                idx=int(d/BIN_DEG) % BINS
                if r>prof[idx]: prof[idx]=r
    return prof

def max_delta(a,b): return max(abs(x-y) for x,y in zip(a,b))

def report(label, shapes, w=W):
    bad=[]
    for s in shapes:
        v=rule_one(s,w)
        if v: bad.append("%s: %s" % (s.name, v))
        si=self_intersects(s.pts) if s.loop else None
        if si: bad.append("%s: 자기교차 변 %s" % (s.name, si))
    return bad

def fill_overlap(shapes, step=0.02):
    """같은 아이템 안에서 **채운 도형 둘이 겹치는가**. 겹치면 채움 레이어가 동률이라
    그리기 순서가 미정이 된다(이 프로젝트의 33-2-0 함정). 겹침 면적을 R^2 단위로 돌려준다."""
    fills=[s for s in shapes if s.filled]
    worst=0.0; pair=None
    for i in range(len(fills)):
        for j in range(i+1,len(fills)):
            a,b=fills[i],fills[j]
            x0=max(bounds(a.pts)[0],bounds(b.pts)[0]); x1=min(bounds(a.pts)[2],bounds(b.pts)[2])
            y0=max(bounds(a.pts)[1],bounds(b.pts)[1]); y1=min(bounds(a.pts)[3],bounds(b.pts)[3])
            if x1<=x0 or y1<=y0: continue
            n=0; x=x0
            while x<x1:
                y=y0
                while y<y1:
                    if contains(a.pts,(x,y)) and contains(b.pts,(x,y)): n+=1
                    y+=step
                x+=step
            area=n*step*step
            if area>worst: worst,pair=area,(a.name,b.name)
    return worst,pair

def stroke_gap(line, shape, w=W):
    """열린 선(스트로크)과 다른 도형 사이의 **최소 거리**(R). 0<간격<1W가 규칙 4의 최악 구간."""
    best=1e9
    for i in range(len(line)-1):
        for t in range(21):
            p=(line[i][0]+(line[i+1][0]-line[i][0])*t/20,
               line[i][1]+(line[i+1][1]-line[i][1])*t/20)
            if contains(shape,p): return 0.0
            n=len(shape)
            for k in range(n):
                a,b=shape[k],shape[(k+1)%n]
                dx,dy=b[0]-a[0],b[1]-a[1]; L=dx*dx+dy*dy
                s=0.0 if L<1e-12 else max(0.0,min(1.0,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/L))
                d=math.hypot(p[0]-(a[0]+dx*s), p[1]-(a[1]+dy*s))
                best=min(best,d)
    return best
