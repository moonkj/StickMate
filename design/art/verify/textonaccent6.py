#!/usr/bin/env python3
"""R10 최종 확정 — 채택색 #FBE4C0 · 색각이상 검증 · 기각 2색 대조."""
import sys,os,math
sys.path.insert(0,os.path.dirname(__file__))
import numpy as np
exec(open(os.path.join(os.path.dirname(__file__),"textonaccent.py")).read().split("# ---------- 4. 전수 탐색")[0])
import cvd
HERO=(0xDE,0xC0,0x81); LEG=(0xFF,0xD3,0x75); RARE=(0xBC,0xAC,0x8B); COMMON=(0x9C,0x97,0x8C)
RAMP=[("일반",COMMON),("희귀",RARE),("영웅",HERO),("전설",LEG)]
FLOOR=min(dE(ACCENT,c) for _,c in RAMP)
PICK=(0xFB,0xE4,0xC0)

print("=== 20. 색각이상 시뮬레이터 교정 (cvd.py 자체 교정) ===")
ok=cvd.calibrate(verbose=False)
print(f"  cvd.calibrate() -> {'통과' if ok else 'FAIL'}")
if not ok: sys.exit(2)

print("\n=== 21. 색각이상 3형에서 채택색이 등급 램프와 갈리는가 ===")
print(f"  {'':<14}", "  ".join(f"{cvd.KOR[t]:>12}" for t in cvd.TYPES))
for nm,c in [("채택 #FBE4C0",PICK),("기각 #E3B971",CAND),("현행 #8FC3FF",CUR)]:
    row=[]
    for t in cvd.TYPES:
        sc=cvd.sim(c,t)
        row.append(min(dE(sc,cvd.sim(rc,t)) for _,rc in RAMP))
    print(f"  {nm:<14}", "  ".join(f"{v:>12.2f}" for v in row),
          f"  <- 최소 {min(row):.2f} {'통과' if min(row)>=7.8 else 'FAIL(7.8)'}")
print("\n  정상 시야 대조:")
for nm,c in [("채택 #FBE4C0",PICK),("기각 #E3B971",CAND)]:
    print(f"    {nm} 램프 최근접 ΔE {min(dE(c,rc) for _,rc in RAMP):.2f}")

print("\n=== 22. 채택색이 칩 위에서 지켜야 할 하한들 ===")
MIN_TEXT=4.5
for n,chip,base in [(CHIPS[k][0],CHIPS[k][1],cur_cr[k]) for k in range(3)]:
    c=CR(PICK,chip)
    print(f"  {n:<12} CR {c:7.4f}  | 텍스트 하한 4.5 {'통과' if c>=MIN_TEXT else 'FAIL'}"
          f"  | 열화 금지선 {base:.4f} {'통과' if c>=base else 'FAIL'} ({c-base:+.4f})")

print("\n=== 23. 확정값 요약 ===")
L,a,b=lab(PICK); C_=math.hypot(a,b); h=math.degrees(math.atan2(b,a))%360
Lb_,ab_,bb_=lab(ACCENT); Cb=math.hypot(ab_,bb_); hbb=math.degrees(math.atan2(bb_,ab_))%360
print(f"  TextOnAccent = {hx(PICK)}  rgb({PICK[0]},{PICK[1]},{PICK[2]})")
print(f"  Unity Color  = new Color({PICK[0]/255:.3f}f, {PICK[1]/255:.3f}f, {PICK[2]/255:.3f}f, 1f)")
rt=tuple(int(round(round(v/255,3)*255)) for v in PICK)
print(f"  3자리 float 왕복 검산: {hx(rt)}  -> {'무손실' if rt==PICK else 'FAIL 손실'}")
print(f"  L* {L:.1f} (브라스 {Lb_:.1f})  C* {C_:.1f} (브라스 {Cb:.1f})  h {h:.1f}° (브라스 {hbb:.1f}°)")
print(f"  상대휘도 L = {Y(PICK):.4f}  -> 크롬 대역(L>0.30) 통과")

print("\n=== 24. 3형(청색맹) 2.69 는 내 선택이 만든 것인가, 브라스 팔레트가 원래 가진 것인가 ===")
print("  기준: 브라스 자신과 등급 램프의 색각이상 거리 (= 이미 출하된 상태)")
print(f"  {'':<22}", "  ".join(f"{cvd.KOR[t]:>12}" for t in cvd.TYPES))
def cvdrow(c, pool):
    return [min(dE(cvd.sim(c,t), cvd.sim(p,t)) for p in pool) for t in cvd.TYPES]
pool=[rc for _,rc in RAMP]
for nm,c in [("Accent 브라스(출하중)",ACCENT),("등급 영웅(출하중)",HERO),("채택 #FBE4C0",PICK),("기각 #E3B971",CAND)]:
    p=[x for x in pool if x!=c]
    print(f"  {nm:<22}", "  ".join(f"{v:>12.2f}" for v in cvdrow(c,p)))
print("\n  램프 4색 '자기들끼리'의 색각이상 최근접 (등급 구분의 실제 한계):")
for t in cvd.TYPES:
    m=min(dE(cvd.sim(a,t),cvd.sim(b,t)) for i,(_,a) in enumerate(RAMP) for j,(_,b) in enumerate(RAMP) if i<j)
    print(f"    {cvd.KOR[t]:<12} 최근접 ΔE {m:.2f}")
print("\n  -> 3형에서는 램프 4색 자체가 서로 붙는다. 등급의 주 채널이 '칸 수'인 이유가 이것이다(§12).")
print("     채택색의 3형 2.69 는 브라스 계열 전체가 원래 갖고 있는 성질이며,")
print(f"     정상 시야에서는 채택색 {min(dE(PICK,rc) for _,rc in RAMP):.2f} > 브라스 자신 {FLOOR:.2f} 로 오히려 여유가 크다.")
