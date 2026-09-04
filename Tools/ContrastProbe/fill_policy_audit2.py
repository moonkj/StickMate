#!/usr/bin/env python3
"""안 C 범위 확정용 — 틴트 상한 / 44px 화소 예산 / 팩 카드 색 가족성."""
import sys, os, re, math, colorsys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fill_policy_audit import (lin,L,CR,over,lab,dE,H,hx,calibrate,
    OUR_CARD,OUR_RAMP,OUR_TRACK,OUR_TEXT,MIN_TEXT,MIN_NONTEXT,HO_GRADE,HO_CARD_T,sec)

def main():
    print("=== 0. 교정 (fill_policy_audit 과 같은 계산기 — 매 실행 재교정) ===")
    calibrate()

    # ---- 1. C3 틴트 상한 ----
    sec("1. ★ C3 바탕면 틴트 α 상한 — 어디까지 물들여도 글자가 사나")
    print(f"  구속 조건 3개를 동시에 만족하는 최대 α:")
    print(f"    (a) 3차 잉크 #8B939F ≥ {MIN_TEXT}   (가장 먼저 죽는 글자)")
    print(f"    (b) 등급 램프 vs 물든 면 ≥ {MIN_NONTEXT}")
    print(f"    (c) 빈칸 {OUR_TRACK} vs 물든 면 — 참고(원래 1.58, 비텍스트 면제 대상)")
    worst_a=1.0
    for g,hexv in OUR_RAMP.items():
        lo,hi=0.0,1.0
        for _ in range(60):
            mid=(lo+hi)/2
            face=over(H(hexv),OUR_CARD,mid)
            ok = CR(OUR_TEXT['3차'],face)>=MIN_TEXT and CR(H(hexv),face)>=MIN_NONTEXT
            if ok: lo=mid
            else: hi=mid
        face=over(H(hexv),OUR_CARD,lo)
        binding = '3차잉크' if abs(CR(OUR_TEXT['3차'],face)-MIN_TEXT)<abs(CR(H(hexv),face)-MIN_NONTEXT) else '램프'
        print(f"    {g:<5}최대 α = {lo:.4f}  (면 {hx(face)}, 3차 {CR(OUR_TEXT['3차'],face):.2f} / 램프 {CR(H(hexv),face):.2f})  구속={binding}")
        worst_a=min(worst_a,lo)
    print(f"\n  → 4등급 공통 상한 α = {worst_a:.4f}  (가장 빡빡한 등급이 결정한다)")
    print(f"     인계본 글로우 α = {0x1F/255:.4f} → {'상한 이내' if 0x1F/255<=worst_a else '상한 초과'}")

    # ---- 2. 44px 화소 예산 ----
    sec("2. 44px 아이콘 / 161×108 카드 — 등급 채널별 화소 예산")
    CW,CH,ICON=161.0,108.0,44.0
    card_px=CW*CH
    chans=[("상단 리본(인계본 규격 h=2, 좌우 14 인셋)",(CW-28)*2),
           ("아이콘 배경 글로우(78 높이 영역 전체)",CW*78*0.0 + 0),  # 우리 카드엔 78 영역 없음 → 아래서 별도
           ("아이콘 채움(44×44 중 잉크 점유 가정 38%)",ICON*ICON*0.38),
           ("이름 옆 등급 라벨(10px 글자 2자, 글리프 점유 ~28%)",10*10*2*0.28)]
    print(f"  카드 {CW:.0f}×{CH:.0f} = {card_px:.0f} px²")
    for n,a in chans:
        if a<=0: continue
        print(f"    {n:<46}{a:8.0f} px²  = 카드의 {a/card_px*100:5.2f}%")
    print(f"\n  우리 리본은 칸 수 방식: (int)rarity+1 칸 + 빈칸. 총 폭 고정.")
    print(f"  → 등급을 **세는** 채널이라 화소가 아니라 **칸 경계**가 정보를 진다.")

    # ---- 3. 팩 카드 색 가족성 ----
    sec("3. ★ DLC 팩 카드 — 4칸이 재질색이면 「한 팩」으로 읽히나")
    D="Assets/_Project/Resources/Items"
    prim={}
    for f in sorted(os.listdir(D)):
        if not f.endswith(".asset"): continue
        t=open(os.path.join(D,f),encoding="utf-8",errors="replace").read()
        m=re.search(r'^\s*itemId:\s*(\S+)',t,re.M)
        if not m: continue
        parts=re.findall(r'color:\s*\{r:\s*([\d.]+),\s*g:\s*([\d.]+),\s*b:\s*([\d.]+)[^}]*\}\s*\n\s*tone:\s*(\d+)',t)
        for r,g,b,tone in parts:
            if tone=='0':
                prim[m.group(1)]=tuple(float(x)*255 for x in (r,g,b)); break
    print(f"  주색 추출 {len(prim)}종 (양성대조: 0이면 파서 실패)")
    # 색상 가족 분류
    fam={}
    for iid,c in prim.items():
        h,s,v=colorsys.rgb_to_hsv(*[x/255 for x in c])
        hd=h*360
        if s<0.2: k="무채(잉크표식)"
        elif hd<15 or hd>=345: k="적 (0°대)"
        elif hd<60: k="호박·가죽·캔버스 (18~43°)"
        elif hd<180: k="녹 (97~103°)"
        else: k="청 (213~218°)"
        fam.setdefault(k,[]).append(iid)
    print("\n  현 카탈로그 42종의 색상 가족:")
    for k in sorted(fam,key=lambda x:-len(fam[x])):
        print(f"    {k:<30}{len(fam[k]):>3}종")
    print(f"  → 가족 {len(fam)}개뿐이다. 4칸 팩이면 통계적으로 2~3가족이 섞인다.")

    # 인계본 office 팩에 대응하는 우리 아이템 4종으로 실측
    packs={
      "오피스(인계본 office 대응)":["equip.head.fedora","equip.eyes.round","equip.neck.striped","equip.shoulders.backpack"],
      "가정 팩 A(같은 가족으로 짰을 때)":["equip.head.straw","equip.eyes.monocle","equip.neck.bell","equip.shoulders.poncho"],
    }
    for pn,ids in packs.items():
        cs=[prim[i] for i in ids if i in prim]
        if len(cs)<4: print(f"  [{pn}] 아이템 누락 — 건너뜀"); continue
        ds=[dE(cs[i],cs[j]) for i in range(4) for j in range(i+1,4)]
        hs=[colorsys.rgb_to_hsv(*[x/255 for x in c])[0]*360 for c in cs]
        spread=max(hs)-min(hs)
        print(f"\n  [{pn}]")
        for i,c in zip(ids,cs): print(f"     {i:<32}{hx(c)}")
        print(f"     쌍별 ΔE  최소 {min(ds):5.2f} / 최대 {max(ds):5.2f} / 평균 {sum(ds)/len(ds):5.2f}")
        print(f"     색상각 스프레드 {spread:.1f}°   {'**가족 아님**' if spread>60 else '가족'}")
    print("\n  [대조] 안 A 방식 = 4칸 전부 같은 등급색이면 ΔE 0.00 / 스프레드 0.0° (완전 균질)")
    print(f"  [대조] 인계본이 실제로 쓴 값 = 4칸 전부 브라스 #C8A15A (accent 고정, "
          f"equipment-screen.dc.html:501) → ΔE 0.00")
    return 0

if __name__=="__main__":
    sys.exit(main())
