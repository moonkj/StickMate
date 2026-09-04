# -*- coding: utf-8 -*-
"""R9 집행 세부 — 안 C 확정 이후 (design-art 2026-09-03)"""
import sys, os, re, math, itertools, collections
sys.path.insert(0,"/Users/kjmoon/App/StickMate/design/art/verify")
import colorlab as CL, band, cvd
import rarityaxis as RA
from rarityaxis import arc, in_band, worst_bd, DISCERN, IDENTIFY, TEXT, NONTEXT, BRASS_RAMP, HANDOFF_CARD, HANDOFF_WORN, BRASS
CL.calibrate(verbose=False); cvd.calibrate(verbose=False)
LO,HI,_=band.limits()
PACK_C=[("오피스 워커",222.0,"#456ECC","#6080CC"),("사이버 아포칼립스",172.0,"#009682","#518C84"),
        ("네온 낙서",312.0,"#CC1BA9","#9C5A8E"),("스포츠",8.0,"#CC3F29","#9E655C"),
        ("컬러 잉크",268.0,"#9768CC","#8563AB"),("밀리터리",80.0,"#639400","#798C51")]
PK=[c for _,_,p,s in PACK_C for c in (p,s)]
# ★ R9 보정 램프 — 이 값의 **유도는 rampfix.py에 있다**(로그 rampfix.out.txt, 최적성 증명 포함).
#   여기서는 일부러 **리터럴로** 들고 온다: 유도기를 import하면 생성기와 검사기가 같은 함정에
#   같이 빠져 서로를 확인해 주지 못한다(TEAM.md 「생성기와 검사기가 같이 틀린다」).
#   값이 갈라지면 rampfix.out.txt §5 결론 줄과 대조해 어느 쪽이 틀렸는지 가른다.
RAMP_FIX=[("일반","#9C978C"),("희귀","#BCAC8B"),("영웅","#DEC081"),("전설","#FFD375")]

print("="*92); print("§28-1. 브라스 램프가 **카드 안에만** 있을 때 6팩과의 각도·거리 — A-1 근거 재확인"); print("="*92)
print(f"\n  {'':18s} " + " ".join(f"{n[:4]:>7s}" for n,_,_,_ in PACK_C) + "   최소호   최소ΔE(주+보조)")
for label,ramp in (("현행 브라스 램프",BRASS_RAMP),("R9 보정 램프",RAMP_FIX),
                   ("[대조] 인계본 램프",HANDOFF_CARD)):
    print(f"  [{label}]")
    for n,hx in ramp:
        c=CL.hex2rgb(hx); h=CL.hue_deg(c)
        arcs=[arc(h,CL.hue_deg(CL.hex2rgb(p))) for _,_,p,_ in PACK_C]
        d=min(CL.dE(c,CL.hex2rgb(x)) for x in PK)
        print(f"   {n:16s} " + " ".join(f"{a:7.1f}" for a in arcs) + f" {min(arcs):8.2f} {d:12.2f}")
    mn_arc=min(arc(CL.hue_deg(CL.hex2rgb(hx)),CL.hue_deg(CL.hex2rgb(p))) for _,hx in ramp for _,_,p,_ in PACK_C)
    mn_dE=min(CL.dE(CL.hex2rgb(hx),CL.hex2rgb(x)) for _,hx in ramp for x in PK)
    print(f"   → 램프 전체 최소 호 {mn_arc:.2f}° · 최소 ΔE {mn_dE:.2f} "
          f"({'변별 통과' if mn_dE>=DISCERN else '★ 변별 미달'})")
b=CL.hex2rgb(BRASS)
print(f"\n  브라스 강조색 {BRASS}: 최소 호 "
      f"{min(arc(CL.hue_deg(b),CL.hue_deg(CL.hex2rgb(p))) for _,_,p,_ in PACK_C):.2f}° · "
      f"최소 ΔE {min(CL.dE(b,CL.hex2rgb(x)) for x in PK):.2f}")
print(f"  [대조] 현행 코드 강조 파랑 #5DA1F5: 최소 호 "
      f"{min(arc(CL.hue_deg(CL.hex2rgb('#5DA1F5')),CL.hue_deg(CL.hex2rgb(p))) for _,_,p,_ in PACK_C):.2f}° · "
      f"최소 ΔE {min(CL.dE(CL.hex2rgb('#5DA1F5'),CL.hex2rgb(x)) for x in PK):.2f}")

print("\n"+"="*92); print("§28-2. 팩 저작 규칙 — 「한 팩 4부위 색상각 스프레드 ≤ 60°」를 우리 값으로 잰다"); print("="*92)
print(f"\n  우리 처방 C 팩의 실제 스프레드 (주색·보조색·그늘색 ×0.62 파생 포함)")
print(f"  {'팩':18s} {'주색 H':>8s} {'보조 H':>8s} {'그늘 H':>8s} {'스프레드':>8s}")
mx=0
for n,hd,p,s in PACK_C:
    sh=CL.rgb2hex(CL.fill_outline(CL.hex2rgb(p)))
    hs=[CL.hue_deg(CL.hex2rgb(x)) for x in (p,s,sh)]
    sp=max(arc(a,b_) for a in hs for b_ in hs)
    mx=max(mx,sp)
    print(f"  {n:18s} {hs[0]:8.2f} {hs[1]:8.2f} {hs[2]:8.2f} {sp:8.2f}")
print(f"  → 우리 팩 최대 스프레드 **{mx:.2f}°**   제안 상한 60° 대비 여유 {60-mx:.2f}°")
print(f"  ⇒ 규칙이 우리 유도 규칙을 **구속하지 않는다**(유도가 한 색상각에서 3색을 뽑으므로).")
print(f"    구속하는 것은 '사람이 손으로 팩 색을 고르는 경우'다.")
# 기본 42종을 테마로 묶었을 때
ITEMS=RA.ITEMS
grp=collections.defaultdict(list)
for f in sorted(os.listdir(ITEMS)):
    if not f.endswith(".asset"): continue
    t=open(os.path.join(ITEMS,f),encoding="utf-8").read()
    for m in re.finditer(r"color: \{r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+), a: ([-\d.eE]+)\}", t):
        hx=CL.rgb2hex(tuple(int(round(float(x)*255)) for x in m.groups()[:3]))
        if hx in {"#D6DBE3","#8B939F"}: continue
        grp[f[:-6]].append(hx)
print(f"\n  [대조] 출하 42종을 **슬롯 4개(모자·안경·목·어깨)에서 한 벌씩 임의로 뽑으면** 스프레드가 얼마인가")
slots={"head":[],"eyes":[],"neck":[],"shoulders":[]}
for k,v in grp.items():
    for s_ in slots:
        if k.startswith("equip_"+s_): slots[s_].append((k,v))
import random
rng=random.Random(20260903)
sps=[]
for _ in range(2000):
    pick=[rng.choice(slots[s_]) for s_ in slots if slots[s_]]
    hs=[CL.hue_deg(CL.hex2rgb(x)) for _,v in pick for x in v]
    if len(hs)<2: continue
    sps.append(max(arc(a,b_) for a in hs for b_ in hs))
sps.sort()
print(f"    무작위 한 벌 2000회: 중앙 {sps[len(sps)//2]:.1f}° · 최소 {sps[0]:.1f}° · 최대 {sps[-1]:.1f}° · "
      f"60° 초과 비율 {sum(1 for x in sps if x>60)/len(sps)*100:.1f}%")

print("\n"+"="*92); print("§28-3. `UX_FLOW` 규칙 8 — 「명도차 ≥ 0.35」를 세 가지 자로 재본다"); print("="*92)
INK_B=(0,0,0); INK_W=(255,255,255)
lo_c=CL.hsv_to_rgb(40/360,0.5,0.62)   # 대역 대표는 실제 팩색으로 대신한다
print(f"\n  아트 대역 L ∈ [{LO:.4f}, {HI:.4f}]  ·  팩 12색이 그 대역 안에 있다")
band_lo=min(PK,key=lambda x:CL.L(CL.hex2rgb(x))); band_hi=max(PK,key=lambda x:CL.L(CL.hex2rgb(x)))
print(f"  대역 실측 양끝: {band_lo} L={CL.L(CL.hex2rgb(band_lo)):.4f} ~ {band_hi} L={CL.L(CL.hex2rgb(band_hi)):.4f}")
print(f"\n  {'자':28s} {'검은 잉크 필요':>16s} {'대역이 낼 수 있는 값':>20s} {'교집합':>8s}")
rows=[("WCAG 상대휘도 차 ≥ 0.35", 0.35, CL.L(CL.hex2rgb(band_hi)), "L"),
      ("CIELAB L* 차 ≥ 35",        35.0, CL.lab(CL.hex2rgb(band_hi))[0], "L*"),
      ("HSV V 차 ≥ 0.35",          0.35, max(CL.rgb_to_hsv(CL.hex2rgb(x))[2] for x in PK), "V")]
for nm,need,got,unit in rows:
    print(f"  {nm:28s} {unit+' >= '+format(need,'.4g' if unit=='L' else '.0f'):>16s} "
          f"{unit+' 최대 '+format(got,'.4f' if unit!='L*' else '.2f'):>20s} "
          f"{'★ 공집합' if got<need else '있음':>8s}")
print(f"\n  그리고 **대비비로 재면 이미 통과한다** (하한 비텍스트 3.0)")
print(f"  {'팩 색':10s} {'L':>7s} {'CR vs 검은 잉크':>15s} {'CR vs 흰 잉크':>13s} {'둘 다 3.0':>9s}")
worst=(99,None)
for x in PK:
    c=CL.hex2rgb(x); a=CL.CR(c,INK_B); b_=CL.CR(c,INK_W)
    if min(a,b_)<worst[0]: worst=(min(a,b_),x)
    print(f"  {x:10s} {CL.L(c):7.4f} {a:15.2f} {b_:13.2f} {'✔' if min(a,b_)>=NONTEXT else '✘':>9s}")
print(f"  → 12색 최악 {worst[0]:.2f} ({worst[1]}) · 하한 3.0 "
      f"{'전건 통과' if worst[0]>=NONTEXT else '★ 미달'}")

print("\n"+"="*92); print("§28-4. `persona-stress` 반증 재현 — 그레이 256칸 전수"); print("="*92)
def fail_rate(colors):
    n=0; tot=0
    for g in range(256):
        bg=(g,g,g)
        for x in colors:
            tot+=1
            if CL.CR(CL.hex2rgb(x),bg) < NONTEXT: n+=1
    return n/tot*100
# 우리 카탈로그 25색
cat=[]
for k,v in grp.items(): cat+=v
cat=sorted(set(cat))
sets={"우리 카탈로그(고유 %d색)"%len(cat): cat,
      "처방 C 팩 12색": PK,
      "인계본 카드 등급색 4": [h for _,h in HANDOFF_CARD],
      "인계본 착용 오버레이 4": [h for _,h in HANDOFF_WORN],
      "인계본 착용→몸(WornColor 후) 4": [CL.rgb2hex(CL.worn(CL.hex2rgb(h))) for _,h in HANDOFF_WORN],
      "브라스 램프 4(크롬 — 참고)": [h for _,h in BRASS_RAMP]}
print(f"\n  {'색 묶음':34s} {'개수':>4s} {'그레이 256칸 실패율':>18s} {'배경4종 최악CR':>14s}")
for nm,cs in sets.items():
    print(f"  {nm:34s} {len(cs):4d} {fail_rate(cs):17.1f}% {min(worst_bd(x) for x in cs):14.2f}")
print(f"\n  ★ 한 색이 그레이 256칸에서 절대 실패하지 않는 것이 가능한가 — 상한을 구한다")
best=(101,None)
for g in range(0,256):
    c=(g,g,g)
    fr=sum(1 for b in range(256) if CL.CR(c,(b,b,b))<NONTEXT)/256*100
    if fr<best[0]: best=(fr,g)
print(f"    무채색 전수 최선: rgb({best[1]},{best[1]},{best[1]}) 실패율 {best[0]:.1f}%")
mm=(101,None)
for r in range(0,256,5):
  for gg in range(0,256,5):
    for b_ in range(0,256,5):
        c=(r,gg,b_)
        fr=sum(1 for x in range(256) if CL.CR(c,(x,x,x))<NONTEXT)
        if fr<mm[0]: mm=(fr,c)
print(f"    유채 포함 전수(5칸 격자) 최선: {CL.rgb2hex(mm[1])} 실패 {mm[0]}/256 = {mm[0]/256*100:.1f}%")
print(f"  ⇒ **어떤 단색도 그레이 배경 전수에서 실패율 0이 될 수 없다.** 최선이 {mm[0]/256*100:.1f}%다.")
print(f"    자립 대역은 '모든 배경에서 산다'는 약속이 아니라 **'우리가 고른 배경 4종에서 산다'**는 약속이다.")

print("\n"+"="*92); print("§28-2-1. 스프레드 상한을 **추측이 아니라 유도**한다 — ΔE 48.6이 깨지는 각도"); print("="*92)
def pick_hue(h):
    best=None
    for si in range(42,101):
        for vi in range(55,81):
            c=CL.hsv_to_rgb(h/360.0,si/100.0,vi/100.0)
            if not (LO<=CL.L(c)<=HI) or CL.worn(c)!=c: continue
            a=CL.lab(c); ch=math.hypot(a[1],a[2])
            if best is None or ch>best[0]: best=(ch,c)
    return best[1] if best else None
print(f"\n  {'기준 색상각':>10s} {'ΔE 7.8':>8s} {'ΔE 25':>8s} {'ΔE 48.6':>9s}")
res=[]
for h0 in (8,80,172,222,268,312):
    c0=pick_hue(h0)
    if c0 is None: continue
    t={7.8:None,25.0:None,48.6:None}
    for dh in range(1,181):
        c1=pick_hue((h0+dh)%360)
        if c1 is None: continue
        d=CL.dE(c0,c1)
        for k in t:
            if t[k] is None and d>=k: t[k]=dh
    res.append((h0,t))
    print(f"  {h0:10d}° {str(t[7.8])+'°':>8s} {str(t[25.0])+'°':>8s} {str(t[48.6])+'°':>9s}")
v=[t[48.6] for _,t in res if t[48.6]]
print(f"\n  → 식별 48.6 도달 최소 각도 = **{min(v)}°** · 중앙 {sorted(v)[len(v)//2]}° · 최대 {max(v)}°")
print(f"  ⇒ 【팩 저작 규칙 P-1】 스프레드 상한 = **{min(v)}°** (ux 제안 60°는 최악 색상각에서 깨진다)")

print("\n"+"="*92); print("§28-4-1. 팩색이 사라지는 그레이 구간"); print("="*92)
print(f"\n  {'색':10s} {'L':>7s} {'실패 그레이 rgb':>18s} {'칸':>4s}")
allbad=set(); inter=set(range(256))
for x in PK:
    c=CL.hex2rgb(x); bad={g for g in range(256) if CL.CR(c,(g,g,g))<NONTEXT}
    allbad|=bad; inter&=bad
    print(f"  {x:10s} {CL.L(c):7.4f} {f'{min(bad)} ~ {max(bad)}':>18s} {len(bad):4d}")
print(f"\n  하나라도 실패: rgb {min(allbad)}~{max(allbad)} ({len(allbad)}/256 = {len(allbad)/256*100:.1f}%)")
print(f"  **전부** 실패: rgb {min(inter)}~{max(inter)} ({len(inter)}칸) · 대표 rgb({(min(inter)+max(inter))//2}) 최악 CR "
      f"{min(CL.CR(CL.hex2rgb(x),(((min(inter)+max(inter))//2),)*3) for x in PK):.2f}")
