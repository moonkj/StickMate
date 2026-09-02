# -*- coding: utf-8 -*-
"""design-character DLC 팩 3안 검산. 알려진 값으로 먼저 교정한다."""
import math
R0=0.22; H=2.2746944; HEAD_C=H-R0
SH=(1.7646944-HEAD_C)/R0; HIPR=(0.9346944-HEAD_C)/R0; TL=SH-HIPR
W_TORSO=0.11*1.045/R0; W_ARM=0.10*1.045/R0
RING=0.0756501/R0                      # 0.343864 R  (실효 링, R2 9-3)
HEAD_INK_R = 1.0 + RING/2              # 1.171932 R
W_DES=0.048/R0                         # 0.218182 R (배율 0.591+ 채움윤곽 = 이 값)
NECKLINE = SH+0.04; COLLAR=SH+0.10
ARM_UP=0.38/R0; ARM_LO=0.37/R0; SPREAD=40.0; ELBOW=10.0
PPU=40.9167

# ---------- 교정 ----------
assert abs(W_TORSO-0.52250)<1e-4 and abs(W_ARM-0.47500)<1e-4
assert abs(RING-0.343864)<1e-5 and abs(W_DES-0.218182)<1e-5
assert abs(SH+1.31818)<1e-4 and abs(HIPR+5.09091)<1e-4
# 출하 야구모자: 커버선 +0.06, M6 펜 -> 잉크 밑단 = 0.06 - W/2
def head_left(cover_y, pen=W_DES, side_wall=None):
    """모자가 커버선 위를 전부 덮을 때 남는 머리(단위원) 면적비와 최소 두께(획)."""
    y = cover_y - pen/2.0
    if y<=-1: return 0.0, 0.0
    if y>=1:  return 1.0, 2.0/pen
    above = math.acos(y) - y*math.sqrt(1-y*y)     # 단위원에서 y 위 면적
    left = (math.pi-above)/math.pi
    thick = (y-(-1.0))/pen
    return left, thick
a,t = head_left(0.06)
print(f"교정) 야구모자 M6 커버선0.06 -> 남는 머리 면적 {a*100:.1f}% 두께 {t:.2f}획")
print("  (headroom.py 실측 21.5% / 2.16획 — 내 모형은 관 옆벽·챙 하향 잉크를 빼서 낙관적이다.")
print("   그래서 아래 팩 수치는 **같은 모형끼리의 상대 비교**로만 쓰고, 절대 판정은 headroom.py 자로 한다.)\n")

# 옆벽 보정: 관이 |x|>=xw 구간에서 y_wall 까지 내려온다고 두고 몬테카를로 없이 격자로 뺀다
def head_left2(cover_y, wall_x=None, wall_y=None, extra=(), pen=W_DES, step=0.004):
    top = cover_y - pen/2.0
    tot=0; live=0; import math as m
    ys=[]; x=-1.0
    while x<=1.0:
        y=-1.0
        while y<=1.0:
            if x*x+y*y<=1.0:
                tot+=1
                dead = (y>=top)
                if not dead and wall_x is not None and abs(x)>=wall_x and y>=wall_y-pen/2.0: dead=True
                if not dead:
                    for (cx,cy,r) in extra:
                        if (x-cx)**2+(y-cy)**2 <= (r+pen/2.0)**2: dead=True; break
                if not dead: live+=1; ys.append(y)
            y+=step
        x+=step
    return live/tot, (max(ys)-(-1.0))/pen if ys else 0.0
a2,t2 = head_left2(0.06, wall_x=0.60, wall_y=-0.22)
print(f"교정2) 야구모자 + 옆벽(-0.22R) 모형 -> 면적 {a2*100:.1f}% 두께 {t2:.2f}획  (headroom.py 21.5%/2.16획)")
print(f"       오차 면적 {abs(a2*100-21.5):.1f}%p / 두께 {abs(t2-2.16):.2f}획 — 이 정도면 상대 비교에 쓴다.\n")

# ---------- C-5 목 여유 ----------
print("== C-5 목 여유 ==")
gap = (-HEAD_INK_R) - NECKLINE
print(f"머리 잉크 바깥 밑 {-HEAD_INK_R:+.5f} R / 목 부착선 {NECKLINE:+.5f} R -> 여유 {gap:.5f} R = {gap/W_DES:.2f}획")
bow_top = NECKLINE + 0.26
print(f"출하 나비넥타이 위 끝(부착선+0.26) {bow_top:+.5f} R -> 머리 잉크 침범 {(bow_top-(-HEAD_INK_R)):+.4f} R = {(bow_top-(-HEAD_INK_R))/W_DES:+.2f}획  ★미확인(자 정규화 쟁점)")

# ---------- 부조/단차 ----------
def relief(cx,cy,r):
    d=math.hypot(cx,cy)
    return (d+r) - HEAD_INK_R
print("\n== 머리 실루엣 밖 돌출(단차) — 신고 없는 4종 관례 +2.12~+4.35획 ==")
for nm,(cx,cy,r) in {
    "오피스 귀컵 (±0.98,-0.10) r0.34":( 0.98,-0.10,0.34),
    "오피스 귀컵 개정 (±1.06,-0.10) r0.40":( 1.06,-0.10,0.40),
    "스포츠 매듭 (-1.10,+0.20) r0.30":(-1.10, 0.20,0.30),
    "스포츠 매듭 개정 (-1.25,+0.18) r0.42":(-1.25,0.18,0.42),
    "사이버 후드 정수리 (0,+0.30) r1.02":(0.0,0.30,1.02),
}.items():
    v=relief(cx,cy,r); print(f"{nm:38s} {v:+.4f} R = {v/W_DES:+.2f}획  {'통과' if v/W_DES>=2.12 else '★미달'}")

# ---------- BACK 가림 ----------
def seg_dist(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]; L=dx*dx+dy*dy
    t=0.0 if L<1e-12 else max(0.0,min(1.0,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/L))
    return math.hypot(p[0]-(a[0]+dx*t), p[1]-(a[1]+dy*t))
def arm(sign):
    a=math.radians(SPREAD*sign); b=math.radians((SPREAD+ELBOW)*sign)
    ex,ey=ARM_UP*math.sin(a), SH-ARM_UP*math.cos(a)
    return [(0,SH),(ex,ey),(ex+ARM_LO*math.sin(b), ey-ARM_LO*math.cos(b))]
ARMS=[arm(1),arm(-1)]
def hidden(x,y):
    if (abs(x)<=W_TORSO/2 and HIPR<=y<=-1.0) or math.hypot(x,y+1.0)<=W_TORSO/2 or math.hypot(x,y-HIPR)<=W_TORSO/2: return 't'
    for a in ARMS:
        for i in range(len(a)-1):
            if seg_dist((x,y),a[i],a[i+1])<=W_ARM/2: return 'a'
    return None
def inside(poly,q):
    ins=False; n=len(poly)
    for i in range(n):
        a=poly[i]; b=poly[(i+1)%n]
        if (a[1]>q[1])!=(b[1]>q[1]):
            xx=a[0]+(q[1]-a[1])*(b[0]-a[0])/(b[1]-a[1])
            if q[0]<xx: ins=not ins
    return ins
def vis(name, poly, step=0.008):
    xs=[p[0] for p in poly]; ys=[p[1] for p in poly]
    tot=ht=ha=0; x=min(xs)
    while x<=max(xs):
        y=min(ys)
        while y<=max(ys):
            if inside(poly,(x,y)):
                tot+=1; h=hidden(x,y)
                if h=='t': ht+=1
                elif h=='a': ha+=1
            y+=step
        x+=step
    v=100-(ht+ha)/tot*100
    print(f"{name:24s} 가시 {v:5.1f}%  (몸통 {ht/tot*100:4.1f}% 팔 {ha/tot*100:4.1f}%)  {'통과' if v>=65 else '★미달'}")
    return v
print("\n== C-2 등판 가시율 (문턱 65%: 신고3종 66.8~80.3 / 신고된 옷깃띠 36.2 사이에서 교정) ==")
vis("[교정] 짧은망토", [(0.40,COLLAR),(0.85,COLLAR-TL*1.35),(-2.45,COLLAR-TL*1.35),(-0.62,COLLAR)])
vis("[교정] 출하 옷깃 띠", [(0.40,COLLAR+0.10),(0.40,COLLAR-0.34),(-0.66,COLLAR-0.34),(-0.66,COLLAR+0.10)], step=0.004)
vis("오피스 노트북가방", [(-0.35,-2.60),(-1.55,-2.60),(-1.55,-3.90),(-0.35,-3.90)], step=0.006)
vis("사이버 급수통", [(-0.40,-1.60),(-1.50,-1.60),(-1.50,-3.40),(-0.40,-3.40)], step=0.006)
vis("스포츠 번호망토", [(0.40,COLLAR),(0.85,COLLAR-TL*1.35),(-2.45,COLLAR-TL*1.35),(-0.62,COLLAR)])
vis("스포츠 엠블럼(2.0R)", [(1.00,-2.60),(1.00,-3.60),(-1.00,-3.60),(-1.00,-2.60)], step=0.005)

# 엠블럼이 몸통 띠에 갈린 조각 폭
half=(2.0-W_TORSO)/2
print(f"\n엠블럼 폭 2.00 R -> 몸통이 {W_TORSO/2.0*100:.1f}% 지움, 남는 두 조각 각 {half:.4f} R = {half/W_DES:.2f}획 (보강 문턱 1.5획)")
front=0.40-W_TORSO/2; back=0.66-W_TORSO/2
print(f"출하 옷깃 띠   -> 앞 조각 {front:.4f} R = {front/W_DES:.2f}획 ★  뒤 조각 {back:.4f} R = {back/W_DES:.2f}획")

# ---------- 1-C : rho_max ----------
print("\n== 규칙 1-C  rho_max >= W_out(0.21818 R) / 목표 1.20획(0.26182 R) ==")
for nm,(w,h) in {"오피스 사원증 0.62x0.86":(0.62,0.86),"사이버 필터통 0.52x0.52":(0.52,0.52),
                 "스포츠 호루라기 0.52x0.30":(0.52,0.30),"스포츠 호루라기 개정 0.72x0.52":(0.72,0.52),
                 "오피스 렌즈 0.74x0.40":(0.74,0.40)}.items():
    rho=min(w,h)/2; print(f"{nm:32s} rho {rho:.4f} R = {rho/W_DES:.2f}획  {'통과' if rho>=W_DES else '★위반'}{' (목표미달)' if W_DES<=rho<0.26182 else ''}")

# ---------- 화면 크기 ----------
print("\n== 화면 크기 (OS pt) ==")
for s in (0.35,0.60,0.75,1.00):
    print(f"배율 {s:.2f}: 전신 {H*s*PPU:6.1f}pt  머리지름 {(0.44*s+max(0.0756501*s,2/PPU))*PPU:5.2f}pt  1R = {R0*s*PPU:5.2f}pt")
