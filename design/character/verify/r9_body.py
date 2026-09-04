#!/usr/bin/env python3
"""R9 (design-character) — A'(numCapVertices=0) 를 조립된 몸에 적용했을 때의 조형 결과.
perf-doc 의 T7 은 팔 하나만 봤다. 이 스크립트는 프리팹 실좌표로 몸 전체를 본다.
프로덕션 .cs/.asset 0줄 수정. 프리팹은 읽기만 한다."""
import re, math, pickle, numpy as np

PT_PER_UNIT   = 40.9167          # FORM_SPEC 9-1 실측 (이 맥)
PX_WIN100     = 40.9167          # Windows 100%: 1pt = 1물리px
PX_RETINA     = 81.8333          # macOS Retina / Win200%
FLOOR_LINE_PT = 2.0              # StickConfig.MinStrokeScreenPoints
FLOOR_FILL_PT = 1.0              # StickConfig.MinFillOutlineScreenPoints
BAKE_SCALE    = 0.75
MEM_PX        = 1.0              # 막 두께(물리픽셀) — design-art §22-3

def parse_prefab(path="Assets/_Project/Prefabs/Stickman.prefab"):
    """프리팹 YAML 을 직접 읽어 LineRenderer 의 월드 좌표/폭/캡을 뽑는다(읽기 전용)."""
    src = open(path, encoding="utf-8").read()
    parts = re.split(r"^--- !u!(\d+) &(\d+).*$\n", src, flags=re.M)
    objs = [(parts[i], parts[i+1], parts[i+2]) for i in range(1, len(parts), 3)]
    goname = {fid: (re.search(r"^  m_Name:\s*(.*)$", b, re.M).group(1).strip()
                    if re.search(r"^  m_Name:\s*(.*)$", b, re.M) else "?")
              for c, fid, b in objs if c == "1"}
    tr = {}
    for c, fid, b in objs:
        if c != "4": continue
        g = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", b).group(1)
        lp = re.search(r"m_LocalPosition:\s*\{x:\s*([-\d.eE+]+),\s*y:\s*([-\d.eE+]+)", b)
        q  = re.search(r"m_LocalRotation:\s*\{x:\s*([-\d.eE+]+),\s*y:\s*([-\d.eE+]+),\s*z:\s*([-\d.eE+]+),\s*w:\s*([-\d.eE+]+)\}", b)
        par= re.search(r"m_Father:\s*\{fileID:\s*(-?\d+)\}", b)
        if not (lp and q and par): continue
        tr[fid] = dict(go=g, x=float(lp.group(1)), y=float(lp.group(2)),
                       ang=2*math.atan2(float(q.group(3)), float(q.group(4))), par=par.group(1))
    go2t = {v["go"]: k for k, v in tr.items()}
    def to_world(t, px, py):
        x, y = px, py
        while t in tr:
            n = tr[t]; c_, s_ = math.cos(n["ang"]), math.sin(n["ang"])
            x, y = n["x"] + c_*x - s_*y, n["y"] + s_*x + c_*y
            t = n["par"]
        return x, y
    out = []
    for c, fid, b in objs:
        if c != "120": continue
        g   = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", b).group(1)
        so  = int(re.search(r"m_SortingOrder:\s*(-?\d+)", b).group(1))
        lp_ = int(re.search(r"m_Loop:\s*(\d+)", b).group(1))
        cap = int(re.search(r"numCapVertices:\s*(\d+)", b).group(1))
        seg = b[b.index("m_Positions"):b.index("m_Parameters")]
        pts = [(float(a), float(bb)) for a, bb, cc in
               re.findall(r"- \{x:\s*([-\d.eE+]+),\s*y:\s*([-\d.eE+]+),\s*z:\s*([-\d.eE+]+)\}", seg)]
        w   = float(re.search(r"widthCurve:.*?value:\s*([-\d.eE+]+)", b, re.S).group(1))
        t   = go2t[g]
        out.append((so, goname.get(g, "?"), w, cap, lp_, [to_world(t, a, bb) for a, bb in pts]))
    out.sort()
    return out

rows = parse_prefab()
L = {name: dict(w=w, rho=w/2, cap=cap, loop=loop, pts=[np.array(p) for p in wp]) for so,name,w,cap,loop,wp in rows}

HEAD_C   = np.array([0.0, 1.541021])
HEAD_R   = 0.165000                       # HeadOutline 링 중심 반경 (프리팹 첫 점 x)
RING_W   = L["HeadOutline"]["w"]          # 0.056738
print("=== 0. 프리팹 실측 (배율 0.75 로 구워짐) ===")
print(f"  머리 링 중심반경 R = {HEAD_R:.6f} / 링 폭 = {RING_W:.6f} -> 머리 잉크 바깥반경 = {HEAD_R+RING_W/2:.6f}")
print(f"  획 폭: 팔 {L['LeftArm']['w']:.6f} / 몸통 {L['Torso']['w']:.6f} / 다리 {L['LeftLeg']['w']:.6f}")
print(f"  위계  팔:몸통:다리 = {L['LeftArm']['w']/L['Torso']['w']:.4f} : 1.0000 : {L['LeftLeg']['w']/L['Torso']['w']:.4f}")
print(f"  검산 몸통획 x {PT_PER_UNIT} = {L['Torso']['w']*PT_PER_UNIT:.3f} pt  [FORM_SPEC 0.75행 = 3.527pt]")

# ---------- 1. 열린 선 18개 끝 — 파묻힘 / 노출 판정 (배율 무관, 비율 논증)
print("\n=== 1. 열린 선 9개 · 끝 18개 census (배율 무관) ===")
def perp(a,b):
    d=b-a; n=np.linalg.norm(d); d=d/n; return np.array([-d[1],d[0]]), d
rows_out=[]
def report(nm, end, kind, margin, note):
    rows_out.append((nm,end,kind,margin,note))

# 몸통 위 끝: 머리 원반(중심 HEAD_C, 반경 HEAD_R+RING_W/2) 안에 있는가
top = L["Torso"]["pts"][0]
d_top = np.linalg.norm(top-HEAD_C); head_out = HEAD_R+RING_W/2
report("Torso","위(목)","파묻힘", head_out-d_top,
       f"머리 중심에서 {d_top:.6f} < 머리 잉크 바깥 {head_out:.6f}. 둥근캡이 덮던 {L['Torso']['rho']:.6f}도 원래 머리 속")
# 몸통 아래 끝: 다리 두 개의 반원판이 덮는가 (아래 3.에서 각도로 증명)
report("Torso","아래(고관절)","파묻힘", None, "고관절 각도 논증(3절)")
for side in ("Left","Right"):
    a=L[f"{side}Arm"]; n0,_=perp(a["pts"][0],a["pts"][1])
    report(f"{side}Arm","위(어깨)","파묻힘", L["Torso"]["rho"]-a["rho"],
           f"어깨점이 몸통 축 위(x=0). 각진 끝의 반길이 rho_arm={a['rho']:.6f} < rho_torso={L['Torso']['rho']:.6f} -> 자세 무관 항상 덮인다")
    report(f"{side}Arm","아래(팔꿈치)","병합시 소멸", None, "병합하면 끝이 아니라 꼭짓점이 된다")
    report(f"{side}ArmLower","위(팔꿈치)","병합시 소멸", None, "")
    report(f"{side}ArmLower","아래(손목)","★노출", None, "실루엣 바깥")
    lg=L[f"{side}Leg"]
    report(f"{side}Leg","위(고관절)","조건부", L["Torso"]["rho"]-lg["rho"],
           f"rho_leg={lg['rho']:.6f} > rho_torso={L['Torso']['rho']:.6f} -> 몸통만으로는 못 덮는다. 반대쪽 다리가 필요(3절)")
    report(f"{side}Leg","아래(무릎)","병합시 소멸", None, "")
    report(f"{side}LegLower","위(무릎)","병합시 소멸", None, "")
    report(f"{side}LegLower","아래(발목)","★노출", None, "실루엣 바깥")
print(f"{'선':16s} {'끝':12s} {'판정':10s} {'여유(월드)':>12s}  근거")
for nm,end,kind,mg,note in rows_out:
    m = f"{mg:+12.6f}" if mg is not None else " "*12
    print(f"{nm:16s} {end:12s} {kind:10s} {m}  {note}")
n_exp=sum(1 for r in rows_out if r[2]=="★노출")
n_bur=sum(1 for r in rows_out if r[2]=="파묻힘")
n_mer=sum(1 for r in rows_out if r[2]=="병합시 소멸")
n_cond=sum(1 for r in rows_out if r[2]=="조건부")
print(f"\n  합계 18 = 노출 {n_exp} + 파묻힘 {n_bur} + 병합소멸 {n_mer} + 조건부 {n_cond}")

# ---------- 2. 실루엣 높이 · 머리 개수
print("\n=== 2. 실루엣 높이 · 머리 개수 (잉크만 — 밝은 배경에서 실제로 보이는 그림) ===")
def leg_bottom(k, rho, cap):
    ys=[]
    for nm in ("LeftLegLower","RightLegLower"):
        p=L[nm]["pts"]; a,b=p[-2]*k, p[-1]*k
        d=(b-a)/np.linalg.norm(b-a)
        if cap=="round": ys.append(b[1]-rho)
        else:            ys.append(b[1]-rho*abs(d[0]))   # 각진 끝: 법선의 y성분 = |d.x|
    return min(ys)
def geom(s, ppu):
    k=s/BAKE_SCALE
    floor_line=FLOOR_LINE_PT/PT_PER_UNIT
    floor_fill=FLOOR_FILL_PT/PT_PER_UNIT
    w_leg=max(L["LeftLeg"]["w"]*k, floor_line); rho_leg=w_leg/2
    w_ring=max(RING_W*k, floor_fill)
    top=HEAD_C[1]*k + HEAD_R*k + w_ring/2
    return k, rho_leg, w_ring, top
print(f"{'배율':>5s} {'ppu':>6s} | {'잉크높이 cap8':>12s} {'cap0':>10s} {'차이':>8s} | {'머리지름':>9s} | {'머리개수 cap8':>12s} {'cap0':>8s} {'Δ':>7s}")
for ppu,pname in ((PX_WIN100,"Win100"),(PX_RETINA,"Retina")):
    for s in (0.35,0.50,0.60,0.75,1.00):
        k,rho_leg,w_ring,top=geom(s,ppu)
        b8=leg_bottom(k,rho_leg,"round"); b0=leg_bottom(k,rho_leg,"butt")
        h8=top-b8; h0=top-b0
        hd=2*(HEAD_R*k+w_ring/2)
        print(f"{s:5.2f} {pname:>6s} | {h8:12.6f} {h0:10.6f} {(h0-h8)/h8*100:7.2f}% | {hd:9.6f} | {h8/hd:12.4f} {h0/hd:8.4f} {h0/hd-h8/hd:+7.4f}")
    print()

print("=== 2-1. 참고 대역과의 거리 (참고 4.80~5.00 머리) ===")
k,rho_leg,w_ring,top=geom(0.75,PX_WIN100)
b8=leg_bottom(k,rho_leg,"round"); b0=leg_bottom(k,rho_leg,"butt"); hd=2*(HEAD_R*k+w_ring/2)
print(f"  현행(cap8) {(top-b8)/hd:.4f} 머리 — 하한 4.80 까지 {4.80-(top-b8)/hd:.4f}")
print(f"  A'(cap0)   {(top-b0)/hd:.4f} 머리 — 하한 4.80 까지 {4.80-(top-b0)/hd:.4f}  ({((4.80-(top-b0)/hd)/(4.80-(top-b8)/hd)-1)*100:+.1f}%)")

# ---------- 3. 고관절 · 어깨 각도 논증 (자세 무관 조건)
print("\n=== 3. 각진 끝이 관절에서 구멍을 여는 조건 (반평면 합집합) ===")
def dirs(k=1.0):
    out={}
    for nm,i0,i1 in (("Torso(위로)",1,0),("LeftLeg",0,1),("RightLeg",0,1)):
        p=L[nm.split("(")[0]]["pts"]; a,b=p[i0],p[i1]
        d=(b-a)/np.linalg.norm(b-a); out[nm]=math.degrees(math.atan2(d[1],d[0]))
    return out
D=dirs()
print("  고관절에서 뻗어 나가는 방향(도):", {k:f"{v:+.2f}" for k,v in D.items()})
cover=np.zeros(3600,bool)
for nm,a in D.items():
    ang=(np.arange(3600)/10.0)
    diff=np.abs(((ang-a+180)%360)-180)
    cover |= diff<=90
print(f"  각진 끝 3개(몸통·좌다리·우다리)의 반원판 합집합 커버리지 = {cover.mean()*100:.1f}%  -> {'구멍 없음' if cover.all() else '구멍 있음'}")
print("  일반 조건: 세 방향이 원을 덮으려면 '연속한 두 방향의 간격 <= 180도'.")
srt=sorted(D.values()); gaps=[(srt[(i+1)%3]-srt[i])%360 for i in range(3)]
print(f"    현재 간격 = {[f'{g:.1f}' for g in gaps]}  최대 {max(gaps):.1f}도  (여유 {180-max(gaps):.1f}도)")
print("    ⇒ 두 다리가 같은 쪽으로 45도 넘게 함께 쏠리면 고관절 뒤에 쐐기가 열린다(랙돌·점프).")
print(f"  어깨: 팔의 각진 끝 반길이 {L['LeftArm']['rho']:.6f} < 몸통 반폭 {L['Torso']['rho']:.6f}")
print(f"        -> 팔 각도와 무관하게 항상 몸통 안. 여유 {L['Torso']['rho']-L['LeftArm']['rho']:.6f} 월드 "
      f"= {(L['Torso']['rho']-L['LeftArm']['rho'])*PX_WIN100:.3f} 물리px(Win100/0.75)")

# ---------- 4. 자세별 고관절 쐐기 (프로덕션 애셋 값 사용)
print("\n=== 4. 자세별 고관절 쐐기 — 각진 끝에서만 생기는 신규 결함 ===")
print("   규약: 허벅지 월드방향 = -90도 + A_i,  A_i = (NeutralSign x spread) + commonHip")
POSES = [   # (이름, 좌 A, 우 A, 출처)
 ("IDLE 서기",            -12,  +12, "idleLegSpreadDegrees: 12"),
 ("WALK 최대",            -25,  +25, "LegHipKeys peak 25"),
 ("FALL 낙하",       14-15, 14+15, "fallPoseHipDegrees 14 + LegSpread 15"),
 ("LANDING CROUCH",     -40,  +82, "landingCrouchRear -40 / Front 82"),
 ("PARKOUR 맨틀",       -40,  +40, "parkourClimbMantleHipDegrees 40 (NeutralSign)"),
 ("ARCHERY",            -18,  +16, "archeryRear -18 / Front 16"),
 ("DRAG 발버둥(swing=1)", 34-12, 34+12, "dragStruggleHipDegrees 34 + idleSpread 12"),
 ("THROW TUMBLE",     76-9, 76+9, "throwTumbleHipDegrees 76 + LimbSpread 9"),
]
def maxgap(A_l, A_r):
    ds = sorted([-90+A_l, -90+A_r, 90.0])
    gaps = [(ds[(i+1) % 3] - ds[i]) % 360 for i in range(3)]
    return max(gaps)
rho_leg_075 = L["LeftLeg"]["rho"]
print(f"{'자세':22s} {'좌허벅지':>8s} {'우허벅지':>8s} {'최대간격':>8s} {'쐐기각':>7s} {'현(Win100/0.75)':>15s} {'Retina/1.0':>11s}  근거")
for nm, al, ar, src in POSES:
    g = maxgap(al, ar); wedge = max(0.0, g-180.0)
    ch_w = 2*rho_leg_075*math.sin(math.radians(wedge/2))*PX_WIN100
    ch_r = 2*(rho_leg_075/0.75)*math.sin(math.radians(wedge/2))*PX_RETINA
    mark = "" if wedge <= 0 else ("  <-- 쐐기" if ch_r >= 1.0 else "  (서브픽셀)")
    print(f"{nm:22s} {-90+al:+8.1f} {-90+ar:+8.1f} {g:8.1f} {wedge:7.1f} {ch_w:14.2f}px {ch_r:10.2f}px{mark}   {src}")
print("  ※ 부호 규약은 프리팹 안식 자세(-102/-78)에서 역산한 것이다. design-motion 확인 필요(미확인).")

# ---------- 5. 캡 제거로 실제로 사라지는 것 (물리픽셀)
print("\n=== 5. 둥근 캡 -> 각진 캡: 물리픽셀로 무엇이 사라지나 ===")
print(f"{'배율':>5s} | {'팔 rho(px) Win/Ret':>20s} | {'다리 rho(px) Win/Ret':>21s} | {'손끝 돌출 소실':>13s} | {'발끝 돌출 소실':>13s}")
for s in (0.35,0.50,0.60,0.75,1.00):
    k=s/BAKE_SCALE; fl=FLOOR_LINE_PT/PT_PER_UNIT
    ra=max(L["LeftArm"]["w"]*k, fl)/2; rl=max(L["LeftLeg"]["w"]*k, fl)/2
    print(f"{s:5.2f} | {ra*PX_WIN100:9.2f}/{ra*PX_RETINA:9.2f} | {rl*PX_WIN100:10.2f}/{rl*PX_RETINA:9.2f} | "
          f"{ra*PX_WIN100:5.2f}/{ra*PX_RETINA:6.2f}px | {rl*PX_WIN100:5.2f}/{rl*PX_RETINA:6.2f}px")

# ---------- 6. 막의 상대 무게 vs 참고 이미지의 실제 아웃라인
print("\n=== 6. 막 1물리px 의 상대 무게 — 참고 이미지의 실제 아웃라인과 비교 ===")
print("   참고(@alanbecker 노랑): 획 34.1px 에 어두운 아웃라인 ~1px  = 획의 2.9% / 머리지름(~153px)의 0.65%")
for ppu,pname in ((PX_WIN100,"Win100"),(PX_RETINA,"Retina")):
    for s in (0.35,0.75,1.00):
        k=s/BAKE_SCALE; fl=FLOOR_LINE_PT/PT_PER_UNIT; ff=FLOOR_FILL_PT/PT_PER_UNIT
        w_t=max(L["Torso"]["w"]*k, fl)*ppu
        hd=(2*(HEAD_R*k+max(RING_W*k,ff)/2))*ppu
        print(f"   {pname} s={s:4.2f}: 몸통획 {w_t:5.2f}px -> 막/획 {MEM_PX/w_t*100:5.1f}%   "
              f"머리지름 {hd:6.2f}px -> 막/머리 {MEM_PX/hd*100:5.2f}%  (참고 대비 {MEM_PX/hd*100/0.65:4.1f}배)")

# ---------- 7. 몸 전체 기준: 종단 캡이 실루엣 둘레에서 차지하는 비율
print("\n=== 7. 전신 실루엣 둘레 중 「막이 못 덮는 종단 캡」의 비율 ===")
N=2400; pad=0.06
allp=np.concatenate([np.array(v["pts"]) for v in L.values()])
x0,y0=allp.min(0)-pad; x1,y1=allp.max(0)+pad
sc=N/max(x1-x0,y1-y0)
W_=int((x1-x0)*sc)+2; H_=int((y1-y0)*sc)+2
gx,gy=np.meshgrid(np.arange(W_)/sc+x0, np.arange(H_)/sc+y0)
def capsule(a,b,r):
    ax,ay=a; bx,by=b; vx,vy=bx-ax,by-ay; l2=vx*vx+vy*vy
    t=np.clip(((gx-ax)*vx+(gy-ay)*vy)/l2,0,1)
    return np.hypot(gx-ax-t*vx, gy-ay-t*vy)<=r
def rect(a,b,r):
    ax,ay=a; bx,by=b; vx,vy=bx-ax,by-ay; l=math.hypot(vx,vy)
    t=((gx-ax)*vx+(gy-ay)*vy)/(l*l); pp=np.abs((gx-ax)*(-vy)+(gy-ay)*vx)/l
    return (t>=0)&(t<=1)&(pp<=r)
def build(cap):
    m=np.zeros((H_,W_),bool)
    for nm,v in L.items():
        p=v["pts"]; r=v["rho"]
        idx=list(range(len(p)-1))+([len(p)-1] if v["loop"] else [])
        for i in idx:
            a,b=p[i],p[(i+1)%len(p)]
            if np.allclose(a,b): continue
            m |= capsule(a,b,r) if (cap=="round" or i not in (0,len(p)-2)) else rect(a,b,r)
        if cap=="butt" and not v["loop"]:
            # 내부 꼭짓점은 둥근 조인 유지(코너 정점 8), 양 끝만 각지게
            for i in range(1,len(p)-1): m |= capsule(p[i],p[i],r)
    return m
def perim(m):
    return int((m & ~(np.roll(m,1,0)&np.roll(m,-1,0)&np.roll(m,1,1)&np.roll(m,-1,1))).sum())
m8=build("round"); m0=build("butt")
p8=perim(m8)
tips=[("LeftArmLower",-1),("RightArmLower",-1),("LeftLegLower",-1),("RightLegLower",-1)]
capmask=np.zeros((H_,W_),bool)
for nm,i in tips:
    v=L[nm]; c=v["pts"][i]
    capmask |= (np.hypot(gx-c[0],gy-c[1])<=v["rho"]*1.02)
edge = m8 & ~(np.roll(m8,1,0)&np.roll(m8,-1,0)&np.roll(m8,1,1)&np.roll(m8,-1,1))
print(f"  래스터 {W_}x{H_} (1px = {1/sc:.6f} 월드)")
print(f"  전신 실루엣 둘레(잉크,cap8) = {p8} px = {p8/sc:.4f} 월드")
print(f"  그중 종단 4캡 반원호      = {int((edge&capmask).sum())} px = {(edge&capmask).sum()/sc:.4f} 월드"
      f"  -> 둘레의 {(edge&capmask).sum()/p8*100:.2f}%")
print(f"  해석 검산: 반원호 4개 = 2*pi*(rho_arm+rho_leg) = {2*math.pi*(L['LeftArm']['rho']+L['LeftLeg']['rho']):.4f} 월드")
print(f"  잉크 면적 cap8 {m8.sum()/sc**2:.5f} / cap0 {m0.sum()/sc**2:.5f}  ({(m0.sum()/m8.sum()-1)*100:+.2f}%)")

# ---------- 8. 각진 끝 + 회전 = 끝점 도달거리의 「호흡」 (발 실패 C1과 같은 읽힘)
print("\n=== 8. 각진 종단은 회전 불변이 아니다 — 보행 1주기 동안 발끝 도달거리가 흔들린다 ===")
HIP=[25,12,0,-15,-25,-12,0,15]; KNEE=[5,20,5,5,10,45,50,25]
def sample(keys,t):
    n=len(keys); x=t*n; i=int(math.floor(x))%n; f=x-math.floor(x)
    return keys[i]*(1-f)+keys[(i+1)%n]*f
print(f"{'t':>5s} {'대퇴°':>7s} {'무릎°':>7s} {'정강이 수직각°':>12s} | {'둥근캡 도달':>10s} {'각진캡 도달':>10s}  (rho 배수)")
vals=[]
for i in range(16):
    t=i/16; h=sample(HIP,t); k=sample(KNEE,t)
    shin = h - k                      # 정강이의 수직 대비 각(도)
    reach_round=1.0; reach_butt=abs(math.sin(math.radians(shin)))
    vals.append(reach_butt)
    print(f"{t:5.3f} {h:7.1f} {k:7.1f} {shin:12.1f} | {reach_round:10.3f} {reach_butt:10.3f}")
lo,hi=min(vals),max(vals)
print(f"  각진캡 도달거리 범위 = {lo:.3f}~{hi:.3f} rho  (진폭 {hi-lo:.3f} rho) / 둥근캡은 항상 1.000 rho")
for ppu,pn in ((PX_WIN100,"Win100"),(PX_RETINA,"Retina")):
    for s in (0.75,1.00):
        rl=max(L["LeftLeg"]["w"]*s/BAKE_SCALE, FLOOR_LINE_PT/PT_PER_UNIT)/2*ppu
        print(f"   {pn} s={s:.2f}: rho_leg={rl:5.2f}px -> 발끝 도달거리가 보행 중 {lo*rl:.2f}~{hi*rl:.2f}px 사이로 "
              f"{(hi-lo)*rl:.2f}px 흔들린다 (물리 발목 위치는 그대로)")
print("  ※ 이 진폭은 '발이 커졌다 작아진다'(LimbCurveRenderer 발 기록 C1)와 같은 읽힘을 만든다.")

# ---------- 9. A-carve (막을 획 안쪽에서 깎기) — 22.3% 법칙에 미치는 영향
print("\n=== 9. A-carve 판정 — 22.3% 법칙 (획 / 머리 잉크 지름) ===")
base=L["Torso"]["w"]/(2*(HEAD_R+RING_W/2))
print(f"  현행 비율 = {L['Torso']['w']:.6f} / {2*(HEAD_R+RING_W/2):.6f} = {base*100:.2f}%   [팀 확정 22.3% +-0.2%]")
print(f"{'배율':>5s} {'화면':>7s} | {'몸통획px':>9s} {'머리지름px':>11s} | {'현행 비율':>9s} | {'A-carve 잉크px':>13s} {'A-carve 비율':>12s}")
for ppu,pn in ((PX_WIN100,"Win100"),(PX_RETINA,"Retina")):
    for s in (0.35,0.60,0.75,1.00):
        k=s/BAKE_SCALE
        w=max(L["Torso"]["w"]*k, FLOOR_LINE_PT/PT_PER_UNIT)*ppu
        hd=2*(HEAD_R*k+max(RING_W*k,FLOOR_FILL_PT/PT_PER_UNIT)/2)*ppu
        carve=max(0.0, w-2*MEM_PX)
        print(f"{s:5.2f} {pn:>7s} | {w:9.3f} {hd:11.3f} | {w/hd*100:8.2f}% | {carve:13.3f} {carve/hd*100:11.2f}%")
