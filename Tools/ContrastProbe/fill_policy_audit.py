#!/usr/bin/env python3
"""장비 채움색 판정 — 안 A(등급 채움) / B(재질 채움) / C(카드면만 등급) 실측.

★ 교정이 깨지면 아무 숫자도 내지 않고 죽는다. (docs/TEAM.md §4 공통 처방)
★ 대비는 반드시 배경을 명시해서 낸다.
"""
import sys, math

# ---------------- 계산기 ----------------
def lin(c):
    c = min(max(c, 0.0), 1.0)
    return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
def L(rgb):  # rgb = 0..255 tuple
    return 0.2126*lin(rgb[0]/255)+0.7152*lin(rgb[1]/255)+0.0722*lin(rgb[2]/255)
def CR(a,b):
    la,lb = L(a),L(b); hi,lo = max(la,lb),min(la,lb)
    return (hi+0.05)/(lo+0.05)
def over(src, dst, a):
    """source-over 합성 (sRGB 8bit, 브라우저 기본)"""
    return tuple(src[i]*a + dst[i]*(1-a) for i in range(3))
_WP=(0.95047,1.0,1.08883)
def rgb2xyz(rgb):
    r,g,b=(lin(v/255.0) for v in rgb)
    return (0.4124564*r+0.3575761*g+0.1804375*b,
            0.2126729*r+0.7151522*g+0.0721750*b,
            0.0193339*r+0.1191920*g+0.9503041*b)
def _f(t): return t**(1/3) if t>216/24389 else (841/108)*t+4/29
def lab(rgb):
    x,y,z=rgb2xyz(rgb); fx,fy,fz=_f(x/_WP[0]),_f(y/_WP[1]),_f(z/_WP[2])
    return (116*fy-16, 500*(fx-fy), 200*(fy-fz))
def dE(a,b):
    la,lb=lab(a),lab(b); return math.sqrt(sum((la[i]-lb[i])**2 for i in range(3)))
def H(s):
    s=s.lstrip('#'); return tuple(int(s[i:i+2],16) for i in (0,2,4))
def hx(c): return "#%02X%02X%02X"%tuple(int(round(v)) for v in c)

# ---------------- 교정 ----------------
def calibrate():
    checks=[("CR 흰/검",CR((255,255,255),(0,0,0)),21.0,5e-3),
            ("CR 동일색(흰)",CR((255,255,255),(255,255,255)),1.0,5e-4),
            ("CR 동일색(#0D0C0B)",CR(H('#0D0C0B'),H('#0D0C0B')),1.0,5e-4),
            ("CR #767676/흰",CR(H('#767676'),(255,255,255)),4.5422,5e-3),
            ("CR #000/#808080",CR((0,0,0),H('#808080')),5.3172,5e-3),
            ("L*(흰)",lab((255,255,255))[0],100.0,1e-3),
            ("L*(검)",lab((0,0,0))[0],0.0,1e-3),
            ("a*b*(회)",abs(lab((128,128,128))[1])+abs(lab((128,128,128))[2]),0.0,1e-3),
            ("dE(흰,검)",dE((255,255,255),(0,0,0)),100.0,1e-3),
            ("dE(동일)",dE((160,98,42),(160,98,42)),0.0,1e-9),
            # 합성기 교정: a=1이면 src, a=0이면 dst, a=0.5 회색 중간
            ("합성 a=1",sum(abs(over((255,0,0),(0,0,0),1.0)[i]-(255,0,0)[i]) for i in range(3)),0.0,1e-9),
            ("합성 a=0",sum(abs(over((255,0,0),(9,12,11),0.0)[i]-(9,12,11)[i]) for i in range(3)),0.0,1e-9),
            ("합성 a=.5",over((200,200,200),(100,100,100),0.5)[0],150.0,1e-9)]
    ok=True
    for n,v,e,tol in checks:
        p=abs(v-e)<=tol; ok&=p
        print(f"  [{'OK ' if p else 'FAIL'}] {n:22s} {v:.6f}  (정답 {e})")
    if not ok: sys.exit("\n교정 실패 — 이 스크립트가 낸 모든 숫자를 폐기하십시오.")
    print("  교정 판정: 유효\n")

# ---------------- 상수 (출처 주석 필수) ----------------
# 인계본 (docs/handoff/.../README.md + reference/*.html 실측)
HO_GRADE = {'일반':'#8A8F98','희귀':'#6E9BE8','영웅':'#B07BE0','전설':'#E0B24A'}
HO_WEAR  = {'일반':'#D8B27A','희귀':'#7FB0F2','영웅':'#C08FEC','전설':'#F0C25C'}
HO_SLOTBG= '#0F0D0C'   # 아이콘 슬롯 바탕
HO_CARD_T= '#161311'   # 카드 그라디언트 위끝
HO_CARD_B= '#111010'   # 카드 그라디언트 아래끝
HO_STROKE= '#E8E2D6'   # ic — 16종 전부 이 값 (equipment-screen.dc.html:805)
HO_STROKE_DIM='#6E665C'
HO_GLOW_A= 0x1F/255    # {등급색}1F
HO_FILL_TOP=0.34; HO_FILL_BOT=0.08     # ItemIcon.dc.html linearGradient
HO_CAPE_TOP=1.00; HO_CAPE_BOT=0.62
HO_MAT_CAPE='#D2402F'

# 우리 프로덕션
OUR_CARD  = (0.106*255,0.122*255,0.149*255)      # UiChrome.CardSurface
OUR_CARD_MUTED=(0.082*255,0.094*255,0.118*255)   # UiChrome.CardSurfaceMuted
OUR_PANEL = (0.078*255,0.090*255,0.110*255)      # UiChrome.PanelSurface
OUR_RAMP  = {'일반':'#9C978C','희귀':'#BCAC8B','영웅':'#DBBD7F','전설':'#F9CB70'}
OUR_TRACK = '#3A4049'                             # UiChrome.RarityTrack
OUR_TEXT  = {'기본':(0.949*255,0.957*255,0.969*255),'보조':(0.682*255,0.706*255,0.749*255),
             '3차':(0.545*255,0.576*255,0.624*255),'비텍스트뮤트':(0.424*255,0.455*255,0.502*255)}
MIN_TEXT=4.5; MIN_NONTEXT=3.0; FACE_TGT=3.60; INK_TGT=5.175
BAND=(0.1632,0.2396)   # ItemCatalog 자립 대역
INK_BLACK=(0,0,0)      # DefaultStickConfig.asset primaryOutlineColor + inkColor:0
INK_WHITE=(255,255,255)
BACKDROPS=[("흰 바탕화면",(255,255,255)),("검은 바탕화면",(0,0,0)),
           ("종이 무대",(0.914*255,0.918*255,0.902*255)),("목탄 무대",(0.145*255,0.157*255,0.180*255))]

def sec(t): print("\n"+"="*78+f"\n{t}\n"+"="*78)

def main():
    print("=== 0. 계산기 교정 (깨지면 전부 폐기) ===")
    calibrate()

    # ---------- 1. 인계본 채움이 실제로 무슨 색이 되는가 ----------
    sec("1. 안 A 실측 — 「채움 = 등급색」은 솔리드가 아니다 (α 0.34→0.08)")
    print("아이콘 슬롯 바탕 = radial({등급색}1F) over #0F0D0C  →  글로우 중심에서 잰다(인계본 최선치)")
    print(f"{'등급':<5}{'등급색':<9}{'글로우면':<9}{'글로우대비':>9}{'채움위':<9}{'채움아래':<9}{'채움위 vs 슬롯':>13}{'채움아래 vs 슬롯':>15}")
    comp_top={}
    for g,hexv in HO_GRADE.items():
        gc=H(hexv)
        glow=over(gc,H(HO_SLOTBG),HO_GLOW_A)            # 아이콘 슬롯 실제 면
        ftop=over(gc,glow,HO_FILL_TOP)                   # 채움 위끝
        fbot=over(gc,glow,HO_FILL_BOT)                   # 채움 아래끝
        comp_top[g]=ftop
        print(f"{g:<5}{hexv:<9}{hx(glow):<9}{CR(glow,H(HO_SLOTBG)):>9.2f}{hx(ftop):<9}{hx(fbot):<9}"
              f"{CR(ftop,glow):>13.2f}{CR(fbot,glow):>15.2f}")
    print(f"\n  하한: 비텍스트 {MIN_NONTEXT}")
    print("  → 「등급색 채움」이 실제로 화면에 내는 색은 위 '채움위' 열이다. 등급색 원본이 아니다.")

    sec("1-B. 그 합성색끼리 등급이 구별되는가 (ΔE, 변별 7.8 / 식별 48.6)")
    ks=list(HO_GRADE)
    print(f"{'쌍':<12}{'원본 ΔE':>9}{'합성 채움 ΔE':>14}{'감쇠율':>9}")
    for i in range(len(ks)-1):
        a,b=ks[i],ks[i+1]
        d0=dE(H(HO_GRADE[a]),H(HO_GRADE[b])); d1=dE(comp_top[a],comp_top[b])
        print(f"{a+'↔'+b:<12}{d0:>9.2f}{d1:>14.2f}{(1-d1/d0)*100:>8.1f}%")
    d0=dE(H(HO_GRADE['일반']),H(HO_GRADE['전설'])); d1=dE(comp_top['일반'],comp_top['전설'])
    print(f"{'일반↔전설':<12}{d0:>9.2f}{d1:>14.2f}{(1-d1/d0)*100:>8.1f}%   ← 최대 쌍")

    sec("1-C. 글로우(radial {등급색}1F)는 보이는가")
    for g,hexv in HO_GRADE.items():
        glow=over(H(hexv),H(HO_SLOTBG),HO_GLOW_A)
        print(f"  {g:<4} 글로우면 {hx(glow)} vs 슬롯 {HO_SLOTBG} = {CR(glow,H(HO_SLOTBG)):.3f}:1"
              f"   {'보임' if CR(glow,H(HO_SLOTBG))>=MIN_NONTEXT else '**안 보임**'}")

    # ---------- 2. 리본 ----------
    sec("2. 리본만으로 등급 4개가 구별되는가 (안 C의 주 채널 후보)")
    print("[인계본] 리본 = 높이 2px 실선, 미보유 opacity .25, 바탕 = 카드 위끝 #161311")
    print(f"{'등급':<5}{'보유 리본':<9}{'대 카드':>8}{'dim 리본':<10}{'대 카드':>8}")
    dimc={}
    for g,hexv in HO_GRADE.items():
        full=H(hexv); dim=over(full,H(HO_CARD_T),0.25); dimc[g]=dim
        print(f"{g:<5}{hexv:<9}{CR(full,H(HO_CARD_T)):>8.2f}{hx(dim):<10}{CR(dim,H(HO_CARD_T)):>8.2f}")
    print(f"\n  dim 리본끼리 ΔE (변별 하한 7.8):")
    for i in range(len(ks)-1):
        a,b=ks[i],ks[i+1]; d=dE(dimc[a],dimc[b])
        print(f"    {a+'↔'+b:<12}{d:>7.2f}  {'OK' if d>=7.8 else '**미달**'}")
    print(f"    {'일반↔전설':<12}{dE(dimc['일반'],dimc['전설']):>7.2f}")

    print("\n[우리] 리본 = 칸 수 (int)rarity+1 + 빈칸 RarityTrack — 색이 주 채널이 아니다")
    for g,hexv in OUR_RAMP.items():
        c=H(hexv)
        print(f"  {g:<4}{hexv} vs 카드면 {hx(OUR_CARD)} = {CR(c,OUR_CARD):>5.2f}:1"
              f"   vs 빈칸 {OUR_TRACK} = {CR(c,H(OUR_TRACK)):>5.2f}:1")
    print(f"  빈칸 {OUR_TRACK} vs 카드면 = {CR(H(OUR_TRACK),OUR_CARD):.2f}:1")

    # ---------- 3. 대역 ----------
    sec("3. 자립 대역 L ∈ [0.1632, 0.2396] — 몸에 칠할 수 있는 색인가")
    print("  (카드 색 = 몸 색 항등이 우리 구조. 카드에 쓴 색이 그대로 바탕화면 위로 나간다)")
    def bandrow(label, hexv):
        c=H(hexv); l=L(c); inb = BAND[0]<=l<=BAND[1]
        worst=min(CR(c,b) for _,b in BACKDROPS)
        wn=min(BACKDROPS,key=lambda kv:CR(c,kv[1]))[0]
        print(f"  {label:<22}{hexv}  L={l:.4f} {'대역 내' if inb else '**대역 밖**':<10}"
              f"배경4종 최악 {worst:>5.2f}:1 ({wn}) {'OK' if worst>=MIN_NONTEXT else '**미달**'}")
    print(" [인계본 등급색]")
    for g,h_ in HO_GRADE.items(): bandrow(f"등급 {g}", h_)
    print(" [인계본 착용 오버레이(한 톤 밝게)]")
    for g,h_ in HO_WEAR.items(): bandrow(f"착용 {g}", h_)
    print(" [우리 브라스 램프]")
    for g,h_ in OUR_RAMP.items(): bandrow(f"램프 {g}", h_)
    print(" [우리 재질색 표본]")
    for n,h_ in [("Canvas 밀짚",'#AB7942'),("Gold 왕관",'#9B7922'),("TintHead 목도리",'#CC5512'),
                 ("Leather",'#BA5928'),("Silver",'#587398'),("인계본 망토 재질",HO_MAT_CAPE)]:
        bandrow(n,h_)

    # ---------- 4. 착용 오버레이 ----------
    sec("4. 「흰 선 위 가독성을 위해 한 톤 밝게」 — 그 전제를 잰다")
    print("  실측: 우리 출하 기본 잉크 = 검정. DefaultStickConfig.asset `inkColor: 0`(=Black),")
    print("        primaryOutlineColor {0,0,0,1}. StickConfig.cs:1489 코드 기본값도 Black. 둘 다 일치.")
    print(f"\n{'등급':<5}{'기본 등급색':<10}{'vs 흰선':>8}{'한 톤 밝게':<11}{'vs 흰선':>8}{'개선?':>7}{'vs 검은선':>10}{'개선?':>7}")
    for g in ks:
        base=H(HO_GRADE[g]); wear=H(HO_WEAR[g])
        bw,ww = CR(base,INK_WHITE), CR(wear,INK_WHITE)
        bb,wb = CR(base,INK_BLACK), CR(wear,INK_BLACK)
        print(f"{g:<5}{HO_GRADE[g]:<10}{bw:>8.2f}{HO_WEAR[g]:<11}{ww:>8.2f}"
              f"{('개선' if ww>bw else '**악화**'):>9}{wb:>10.2f}{('개선' if wb>bb else '악화'):>9}")

    # ---------- 5. 안 C 범위별 텍스트 대비 ----------
    sec("5. ★ 안 C — 「카드색」 범위를 넓힐 때 글자 대비가 어떻게 되는가")
    print("  C1 = 리본 + 이름옆 라벨만 (바탕면 순수)")
    print("  C2 = C1 + 아이콘 배경 radial glow")
    print("  C3 = C2 + 카드 바탕면까지 등급색 물들임")
    print(f"\n  우리 카드면 기준 {hx(OUR_CARD)}. 글자 3종 대비 (하한 {MIN_TEXT}):")
    for tn,tc in OUR_TEXT.items():
        print(f"    {tn:<8}{hx(tc)} on {hx(OUR_CARD)} = {CR(tc,OUR_CARD):>6.2f}:1  "
              f"{'OK' if CR(tc,OUR_CARD)>=MIN_TEXT else '(비텍스트)'}")
    print("\n  C3에서 바탕면을 등급색 α로 물들였을 때 — 글자 대비 재측정:")
    for alpha in (0.06,0.10,0.16,0.24):
        print(f"\n   ── 바탕면 틴트 α={alpha:.2f} ──")
        hdr=f"    {'등급':<5}{'물든 면':<9}"+"".join(f"{t:>11}" for t in ('기본','보조','3차'))+f"{'램프 대비':>10}{'판정':>16}"
        print(hdr)
        for g,hexv in OUR_RAMP.items():
            face=over(H(hexv),OUR_CARD,alpha)
            vals=[CR(OUR_TEXT[t],face) for t in ('기본','보조','3차')]
            ramp=CR(H(hexv),face)
            fails=sum(1 for v in vals if v<MIN_TEXT) + (1 if ramp<MIN_NONTEXT else 0)
            print(f"    {g:<5}{hx(face):<9}"+"".join(f"{v:>11.2f}" for v in vals)+
                  f"{ramp:>10.2f}   {'OK' if fails==0 else f'**{fails}건 미달**'}")
    return 0

if __name__=="__main__":
    sys.exit(main())
