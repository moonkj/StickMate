# -*- coding: utf-8 -*-
"""역잉크 분리막의 조형 검산 — design-character R8.
모든 상수는 소스 인용. 계산이며 판정이 아니다(실기 캡처 필요).
"""
import math

# ---- 소스 인용 ----
LWS   = 1.045                     # SceneBootstrapper.cs:138 LineWidthScale
TORSO = 0.11 * LWS                # :142
LEG   = 0.12 * LWS                # :143
ARM   = 0.10 * LWS                # :144
RING  = 0.0756501                 # 실효 링 계수 (SceneBootstrapper.cs:181 / FORM_SPEC 9-3)
R     = 0.22                      # BaselineHeadVisualRadius :210
H     = 2.2746944                 # StickConfig.BaselineCharacterTotalHeight :1816
LU_L, LL_L = 0.375, 0.3375        # 프리팹 로컬(0.50/0.45 x 0.75) 다리
AU_L, AL_L = 0.285, 0.2775        # 프리팹 로컬(0.38/0.37 x 0.75) 팔
FILLET = 0.42                     # LimbCurveRenderer.FilletLengthRatio:107
MAXSAG = 1.0                      # MaxSagittaPerStrokeWidth:111
ARCN   = 4                        # ArcSamplesPerHalf:116
PT_FLOOR_LINE = 2.0               # StickConfig.MinStrokeScreenPoints:1873
PT_FLOOR_FILL = 1.0               # StickConfig.MinFillOutlineScreenPoints:1911
PET_RATIO = 0.022                 # CharacterPetRenderer.StrokeRatio:176
MINI      = 0.45                  # AppearanceShapeBuilder.MiniScale:99
MINI_HEAD = 0.14                  # MiniFigure r = h*0.14
MEM_PX = 1.0                      # DialogueBubbleRenderer.OutlineRingMinPhysicalPixels

SCREENS = [
    ("macOS Retina / Win200%", 40.9167, 2.0),   # 실측 FORM_SPEC 8-1
    ("Windows 100%",           40.9167, 1.0),
    ("굽기근사 846pt x Win100%", 35.25,  1.0),   # F_bake 화면 = 최악
]
SCALES = [0.35, 0.50, 0.60, 0.75, 1.00]

def W(base, s, ppt, floor):  # world units
    return max(base*s, floor/ppt)

def rho_over_r(theta_deg, Lu, Ll, s, Wworld):
    """r_world / rho_world  (필렛 원호 곡률반경 / 획 반폭)"""
    lossy = s/0.75
    Wlocal = Wworld/lossy
    half = math.radians(abs(theta_deg))/2.0
    t = FILLET*min(Lu, Ll)
    sag = t*math.tan(half/2)
    if sag > MAXSAG*Wlocal:
        t *= (MAXSAG*Wlocal)/sag
    r_local = t/math.tan(half)
    r_world = r_local*lossy
    return r_world/(Wworld/2.0), math.degrees(half)/(ARCN-1)

print("### A. 획·막 두께와 위계 (물리픽셀)")
for name, ppt, dpi in SCREENS:
    ppx = ppt*dpi
    print(f"\n[{name}]  {ppx:.4f} 물리px/유닛")
    print(f"{'배율':>5} {'팔':>6} {'몸통':>6} {'다리':>6} {'링':>6} | {'막/몸통':>7} "
          f"{'몸통면적':>8} | {'잉크 팔/다리':>11} {'막포함 팔/다리':>13} {'위계압축':>8}")
    for s in SCALES:
        a = W(ARM,s,ppt,PT_FLOOR_LINE)*ppx
        t_= W(TORSO,s,ppt,PT_FLOOR_LINE)*ppx
        l = W(LEG,s,ppt,PT_FLOOR_LINE)*ppx
        g = W(RING,s,ppt,PT_FLOOR_FILL)*ppx
        ink_h = a/l
        mem_h = (a+2*MEM_PX)/(l+2*MEM_PX)
        comp  = (1-mem_h)/(1-ink_h)*100 if ink_h < 1 else float('nan')
        print(f"{s:>5.2f} {a:>6.3f} {t_:>6.3f} {l:>6.3f} {g:>6.3f} | {MEM_PX/t_:>7.3f} "
              f"{2*MEM_PX/t_*100:>7.1f}% | {ink_h:>11.4f} {mem_h:>13.4f} {comp:>7.1f}%")

print("\n\n### B. 머리 개수 (잉크만 / 막 포함)  — 참고 4.80~5.00, 우리 잉크 4.411")
for name, ppt, dpi in SCREENS:
    ppx = ppt*dpi
    print(f"\n[{name}]")
    print(f"{'배율':>5} {'머리잉크지름px':>13} {'전신px':>8} {'머리개수(잉크)':>14} {'머리개수(막포함)':>16} {'차이':>7}")
    for s in SCALES:
        g = W(RING,s,ppt,PT_FLOOR_FILL)*ppx
        D = 2*R*s*ppx + g
        ht= H*s*ppx
        n0 = ht/D
        n1 = (ht+2*MEM_PX)/(D+2*MEM_PX)
        print(f"{s:>5.2f} {D:>13.3f} {ht:>8.2f} {n0:>14.3f} {n1:>16.3f} {n1-n0:>+7.3f}")

print("\n\n### C. 규칙 B/C — 필렛 원호가 자기교차하지 않는가 (r/rho >= 1/cos(dphi/2))")
POSES = [("다리 · 착지웅크림 앞무릎 126도", 126.0, LU_L, LL_L, LEG),
         ("팔  · 주위살피기 122도",        122.0, AU_L, AL_L, ARM)]
for name, ppt, dpi in SCREENS:
    ppx = ppt*dpi
    print(f"\n[{name}]")
    for pname, th, Lu, Ll, base in POSES:
        print(f"  {pname}")
        print(f"  {'배율':>5} {'W px':>7} {'r/rho(잉크)':>11} {'r/rho(막)':>10} {'기준':>7} {'잉크여유':>8} {'막여유':>7}")
        for s in SCALES:
            Ww = W(base,s,ppt,PT_FLOOR_LINE)
            rr, dphi = rho_over_r(th, Lu, Ll, s, Ww)
            crit = 1.0/math.cos(math.radians(dphi)/2)
            Wpx = Ww*ppx
            rr_mem = rr * (Wpx/(Wpx+2*MEM_PX))
            mark = "OK " if rr_mem >= crit else "위반"
            print(f"  {s:>5.2f} {Wpx:>7.3f} {rr:>11.4f} {rr_mem:>10.4f} {crit:>7.4f} "
                  f"{rr/crit:>8.3f} {rr_mem/crit:>6.3f} {mark}")

print("\n\n### D. 주인 vs 리틀스틱메이트 — 막이 작은 쪽에 더 무겁게 걸린다")
for name, ppt, dpi in SCREENS:
    ppx = ppt*dpi
    print(f"\n[{name}]")
    print(f"{'배율':>5} {'주인 머리개수':>12} {'펫 머리개수':>11} {'주인(막)':>9} {'펫(막)':>8} {'벌어짐':>7}")
    for s in SCALES:
        g = W(RING,s,ppt,PT_FLOOR_FILL)*ppx
        Do = 2*R*s*ppx + g
        ho = H*s*ppx
        pet_stroke = max(H*s*PET_RATIO, PT_FLOOR_LINE/ppt)*ppx
        hp = H*s*MINI*ppx
        Dp = 2*MINI_HEAD*hp + pet_stroke
        n_o0, n_p0 = ho/Do, hp/Dp
        n_o1, n_p1 = (ho+2*MEM_PX)/(Do+2*MEM_PX), (hp+2*MEM_PX)/(Dp+2*MEM_PX)
        print(f"{s:>5.2f} {n_o0:>12.3f} {n_p0:>11.3f} {n_o1:>9.3f} {n_p1:>8.3f} "
              f"{abs(n_o1-n_p1)-abs(n_o0-n_p0):>+7.3f}")
# -*- coding: utf-8 -*-
import math
LWS=1.045; TORSO=0.11*LWS; LEG=0.12*LWS; ARM=0.10*LWS
RING=0.0756501; R=0.22; H=2.2746944
LU,LL=0.375,0.3375; AU,AL=0.285,0.2775
FILLET=0.42; MAXSAG=1.0; ARCN=4
FL_LINE=2.0; FL_FILL=1.0; MEM=1.0
SCREENS=[("macOS Retina / Win200%",40.9167,2.0),("Windows 100%",40.9167,1.0),
         ("굽기근사 846pt x Win100%",35.25,1.0)]

def W(base,s,ppt,fl): return max(base*s, fl/ppt)
def arc(theta,Lu,Ll,s,Ww):
    lossy=s/0.75; Wl=Ww/lossy; half=math.radians(abs(theta))/2
    t=FILLET*min(Lu,Ll); sag=t*math.tan(half/2)
    if sag>MAXSAG*Wl: t*=MAXSAG*Wl/sag
    return (t/math.tan(half))*lossy, math.degrees(half)/(ARCN-1)

print("### C-2. 겹침 깊이 delta = rho_mem - r*cos(dphi/2)  [물리픽셀]  (양수 = 자기교차)")
for name,ppt,dpi in SCREENS:
    ppx=ppt*dpi; print(f"\n[{name}]")
    print(f"{'배율':>5} {'다리 delta':>10} {'팔 delta':>9} {'다리 겹침/획':>12} {'팔 겹침/획':>11}")
    for s in (0.35,0.50,0.60,0.75,1.00):
        row=[]
        for th,Lu,Ll,base in ((126.0,LU,LL,LEG),(122.0,AU,AL,ARM)):
            Ww=W(base,s,ppt,FL_LINE); r,dphi=arc(th,Lu,Ll,s,Ww)
            rpx=r*ppx; Wpx=Ww*ppx; rho_mem=Wpx/2+MEM
            row.append((rho_mem-rpx*math.cos(math.radians(dphi)/2), Wpx))
        print(f"{s:>5.2f} {row[0][0]:>+10.3f} {row[1][0]:>+9.3f} "
              f"{row[0][0]/row[0][1]:>+12.3f} {row[1][0]/row[1][1]:>+11.3f}")

print("\n\n### C-3. 막 1.000물리px가 관절 규칙과 양립하는 최소 배율")
for name,ppt,dpi in SCREENS:
    ppx=ppt*dpi
    out=[]
    for th,Lu,Ll,base,lbl in ((126.0,LU,LL,LEG,"다리"),(122.0,AU,AL,ARM,"팔")):
        lo,hi=0.35,1.00; ok_at_hi=None
        def viol(s):
            Ww=W(base,s,ppt,FL_LINE); r,dphi=arc(th,Lu,Ll,s,Ww)
            return (Ww*ppx/2+MEM) - r*ppx*math.cos(math.radians(dphi)/2)
        if viol(1.00)>0: out.append(f"{lbl}: 전 구간 위반"); continue
        if viol(0.35)<=0: out.append(f"{lbl}: 전 구간 OK"); continue
        for _ in range(60):
            m=(lo+hi)/2
            if viol(m)>0: lo=m
            else: hi=m
        out.append(f"{lbl}: s >= {hi:.4f}")
    print(f"[{name}]  " + " / ".join(out))

print("\n\n### E. 배경이 잉크와 같아질 때 — 몇 칸에서 잉크가 실질적으로 사라지는가 (그레이 256칸)")
def lum(v):
    c=v/255.0
    c=c/12.92 if c<=0.04045 else ((c+0.055)/1.055)**2.4
    return c
def cr(a,b):
    la,lb=lum(a),lum(b)
    hi,lo=max(la,lb),min(la,lb)
    return (hi+0.05)/(lo+0.05)
for th in (1.5,2.0,3.0):
    blk=sum(1 for v in range(256) if cr(0,v)<th)
    wht=sum(1 for v in range(256) if cr(255,v)<th)
    print(f"  CR < {th}:  검 잉크 {blk:>3}/256 ({blk/2.56:.1f}%)   흰 잉크 {wht:>3}/256 ({wht/2.56:.1f}%)")
print("  (교정: cr(0,255) =", round(cr(0,255),4), ", cr(128,128) =", round(cr(128,128),4), ")")

print("\n\n### F. 위계가 사라지는 정도 — 막이 팔<몸통<다리 설계차보다 큰가")
for name,ppt,dpi in SCREENS:
    ppx=ppt*dpi; print(f"\n[{name}]")
    print(f"{'배율':>5} {'팔↔다리 설계차 px':>16} {'막 총폭 px':>10} {'막/설계차':>9}")
    for s in (0.35,0.50,0.60,0.75,1.00):
        gap=(W(LEG,s,ppt,FL_LINE)-W(ARM,s,ppt,FL_LINE))*ppx
        print(f"{s:>5.2f} {gap:>16.3f} {2*MEM:>10.3f} {(2*MEM/gap if gap>0 else float('inf')):>9.2f}")

print("\n\n### G. 실루엣 기준 관절 뭉툭함 — 바깥 경계 곡률반경 (r + rho)")
for name,ppt,dpi in SCREENS:
    ppx=ppt*dpi; print(f"\n[{name}]")
    print(f"{'배율':>5} {'다리 잉크 바깥R px':>16} {'막 바깥R px':>11} {'증가':>7} | {'팔 잉크':>8} {'팔 막':>7} {'증가':>7}")
    for s in (0.35,0.50,0.60,0.75,1.00):
        vals=[]
        for th,Lu,Ll,base in ((126.0,LU,LL,LEG),(122.0,AU,AL,ARM)):
            Ww=W(base,s,ppt,FL_LINE); r,_=arc(th,Lu,Ll,s,Ww)
            rpx=r*ppx; rho=Ww*ppx/2
            vals.append((rpx+rho, rpx+rho+MEM))
        print(f"{s:>5.2f} {vals[0][0]:>16.3f} {vals[0][1]:>11.3f} {(vals[0][1]/vals[0][0]-1)*100:>+6.1f}% | "
              f"{vals[1][0]:>8.3f} {vals[1][1]:>7.3f} {(vals[1][1]/vals[1][0]-1)*100:>+6.1f}%")

# =====================================================================
# H. 몸의 선 개수 — 프리팹을 직접 세는 것 외에 답이 없다(소스 상수가 아니다)
#    양성 대조 포함: 존재하지 않는 클래스 ID로 0이 나오는지 함께 찍는다.
# =====================================================================
import re, os
PREFAB = os.path.join(os.path.dirname(__file__), "..", "..", "..",
                      "Assets/_Project/Prefabs/Stickman.prefab")
PREFAB = os.path.normpath(PREFAB)
print("\n\n### H. 본체 LineRenderer 실측 —", PREFAB)
if not os.path.exists(PREFAB):
    print("  프리팹을 찾지 못했다 — 미측정")
else:
    txt = open(PREFAB, encoding="utf-8").read()
    docs = re.split(r'\n--- !u!(\d+) &(\d+)\n', txt)
    objs = {}
    i = 1
    while i + 2 <= len(docs) - 1:
        objs[docs[i+1]] = (docs[i], docs[i+2]); i += 3
    names = {f: re.search(r'm_Name:\s*(.+)', b).group(1).strip()
             for f, (c, b) in objs.items() if c == '1' and re.search(r'm_Name:', b)}
    lr = [(names.get(re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', b).group(1), '?'),
           int(re.search(r'm_SortingOrder:\s*(-?\d+)', b).group(1)),
           int(re.search(r'm_Loop:\s*(\d)', b).group(1)),
           len(re.findall(r'- \{', re.search(r'm_Positions:\s*\n((?:\s*- \{.*\n)+)', b).group(1))))
          for f, (c, b) in objs.items() if c == '120']
    for n, so, lo, np_ in sorted(lr, key=lambda x: (x[1], x[0])):
        print(f"  {n:14s} sortingOrder={so:>2d} loop={lo} points={np_}")
    print(f"  --> LineRenderer {len(lr)}개 / GameObject {len(names)}개")
    print(f"  독립 대조 grep: {txt.count(chr(10) + 'LineRenderer:')}개")
    ghost = sum(1 for f, (c, b) in objs.items() if c == '99999')
    print(f"  양성 대조(존재하지 않는 클래스 99999): {ghost}개  (0이 아니면 이 계수 전부 무효)")
    orders = sorted({so for _, so, _, _ in lr})
    print(f"  잉크 정렬층 = {orders}  →  '각 획의 order-1'이 비어 있는가: "
          f"{[o - 1 for o in orders if (o - 1) not in orders and (o - 1) != -1]}")


# I. 코너/캡 정점 — 프리팹 실측 + 양성 대조
if os.path.exists(PREFAB):
    t2 = open(PREFAB, encoding="utf-8").read()
    print("\n### I. 코너/캡 정점")
    print("  numCornerVertices: 8 ->", t2.count("numCornerVertices: 8"),
          "/ numCapVertices: 8 ->", t2.count("numCapVertices: 8"),
          "/ 전체 numCornerVertices ->", t2.count("numCornerVertices:"))
    print("  양성 대조 numCornerVertices: 99 ->", t2.count("numCornerVertices: 99"),
          " (0이 아니면 이 계수 무효)")
