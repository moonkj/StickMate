#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""핸드오프 팔레트를 우리 하한(MinTextContrast 4.5 / MinNonTextContrast 3.0)으로 전수 측정.
   Tools/ContrastProbe/measure_chip.py 와 같은 lin/L/CR. 교정 실패 시 죽는다."""
import sys
def lin(c):
    c=max(0.0,min(1.0,c)); return c/12.92 if c<=0.04045 else ((c+0.055)/1.055)**2.4
def L(rgb): return 0.2126*lin(rgb[0]/255)+0.7152*lin(rgb[1]/255)+0.0722*lin(rgb[2]/255)
def CR(a,b):
    la,lb=L(a),L(b); hi,lo=max(la,lb),min(la,lb); return (hi+0.05)/(lo+0.05)
def H(s):
    s=s.lstrip('#'); return (int(s[0:2],16),int(s[2:4],16),int(s[4:6],16))
def hx(c): return "#%02X%02X%02X"%c
def flat(over,a,onto): return tuple(round(over[i]*a+onto[i]*(1-a)) for i in range(3))
def calibrate():
    ck=[("흰/검",CR((255,)*3,(0,)*3),21.0,0.005),("동일색(흰)",CR((255,)*3,(255,)*3),1.0,0.0005),
        ("동일색(#0D0C0B)",CR(H('0D0C0B'),H('0D0C0B')),1.0,0.0005),
        ("#767676/흰",CR((0x76,)*3,(255,)*3),4.54,0.005),("#000/#808080",CR((0,)*3,(0x80,)*3),5.32,0.005)]
    ok=True
    for n,v,e,t in ck:
        p=abs(v-e)<=t; ok&=p; print(f"  {'PASS' if p else 'FAIL'}  {n:16s} {v:.4f} (정답 {e})")
    if not ok: sys.exit("교정 실패 — 이하 숫자 전부 폐기")
    print("  교정 판정: 유효\n")

TXT, NONTXT = 4.5, 3.0
print("=== 교정 ==="); calibrate()

# ---- 핸드오프 토큰 ----
WIN   = H('0D0C0B')   # 창 배경
SLOT  = H('0F0D0C')   # 아이콘 슬롯 / 스테이지 심부
CARD1 = H('161311')   # 카드 그라디언트 밝은 끝
CARD2 = H('100E0D')   # 카드 그라디언트 어두운 끝
CARD15= H('151311')   # 상세카드 위
ACT   = H('17130E')   # 활성/재화 배경
WORN  = H('1D1813')   # 착용 카드
TABBG = H('141210')   # 탭 컨테이너
BRASS = H('C8A15A')
ONBRS = H('160F06')
INKS = [("기본 #EDE7DB",H('EDE7DB')),("보조 #A79E90",H('A79E90')),("본문 #9A9184",H('9A9184')),
        ("3차 #8A8578",H('8A8578')),("라벨 #6E665C",H('6E665C')),("비활성 #5C574E",H('5C574E')),
        ("황동 #C8A15A",BRASS),("증가값 #8FBF6A",H('8FBF6A'))]
BGS = [("창 #0D0C0B",WIN),("카드밝 #161311",CARD1),("카드어둠 #100E0D",CARD2),
       ("활성/재화 #17130E",ACT),("착용카드 #1D1813",WORN),("탭컨테이너 #141210",TABBG),("슬롯 #0F0D0C",SLOT)]

print("=== A. 텍스트 잉크 × 표면 전수 (하한 4.50) ===")
print(f"  {'잉크':18s}" + "".join(f"{n.split()[0]:>10s}" for n,_ in BGS))
fails=[]
for iname,ink in INKS:
    row=f"  {iname:18s}"
    for bn,bg in BGS:
        v=CR(ink,bg); row+=f"{v:10.2f}"
        if v<TXT: fails.append((iname,bn,v))
    print(row)
print(f"\n  ▶ 텍스트 하한 4.50 미달 조합 {len(fails)}건")
for i,b,v in fails: print(f"      FAIL  {i:18s} / {b:18s} = {v:.2f}:1")
print()

print("=== B. 실제로 쓰이는 조합만 (README가 지정한 자리) ===")
uses=[
 ("앱명 '스틱메이트' 17px",H('EDE7DB'),WIN,TXT),
 ("레벨 'Lv.7' 11px",H('6E665C'),WIN,TXT),
 ("탭 비활성 12.5px",H('8A8578'),TABBG,TXT),
 ("탭 활성 글자",ONBRS,BRASS,TXT),
 ("보유 칩 라벨 12px",H('A79E90'),WIN,TXT),
 ("보유 칩 수치",H('EDE7DB'),WIN,TXT),
 ("동전 칩 글자",BRASS,ACT,TXT),
 ("PREVIEW 라벨 10px",H('5C574E'),SLOT,TXT),
 ("착용행 이름(착용)",H('EDE7DB'),ACT,TXT),
 ("착용행 이름(빔) 12.5px",H('5C574E'),H('111010'),TXT),
 ("착용행 라벨 9.5px",H('6E665C'),ACT,TXT),
 ("착용행 기여값 11px",BRASS,ACT,TXT),
 ("상세 설명 12.5px",H('9A9184'),CARD15,TXT),
 ("스탯칩 글자 11px",BRASS,H('1B1713'),TXT),
 ("가격칩 글자",H('8A8578'),H('161311'),TXT),
 ("스탯 '모자 주스탯' 9.5px",H('6E665C'),CARD1,TXT),
 ("스탯 보너스 '+8' 10.5px",H('8FBF6A'),CARD1,TXT),
 ("단계칩 미달 10px",H('5C574E'),H('1B1713'),TXT),
 ("단계칩 초급 10px",H('9AA1AB'),H('1B1713'),TXT),
 ("단계칩 중급 10px",BRASS,H('1B1713'),TXT),
 ("단계칩 고급 10px",H('E0B24A'),H('1B1713'),TXT),
 ("단계 효과 문구 10.5px",H('8A8578'),CARD1,TXT),
 ("'중급까지 4' 10px",H('5C574E'),CARD1,TXT),
 ("세트 효과 설명 10.5px",H('5C574E'),CARD15,TXT),
 ("세트 하단 주석 10.5px",H('5C574E'),CARD15,TXT),
 ("세트 완성 상태 텍스트",H('8FBF6A'),CARD15,TXT),
 ("카테고리 영문 라벨 10px",H('6E665C'),WIN,TXT),
 ("카드 이름 13px",H('EDE7DB'),CARD1,TXT),
 ("카드 부스탯 10.5px",H('8A8578'),CARD1,TXT),
 ("카드 메타 테마명 10px",H('6E665C'),CARD1,TXT),
 ("카드 '보유' 10px",H('5C574E'),CARD1,TXT),
 ("등급라벨 일반 10px",H('8A8F98'),CARD1,TXT),
 ("등급라벨 희귀 10px",H('6E9BE8'),CARD1,TXT),
 ("등급라벨 영웅 10px",H('B07BE0'),CARD1,TXT),
 ("등급라벨 전설 10px",H('E0B24A'),CARD1,TXT),
 ("등급라벨 일반(착용카드)",H('8A8F98'),WORN,TXT),
 ("등급라벨 희귀(착용카드)",H('6E9BE8'),WORN,TXT),
 ("버튼 '착용' 글자",ONBRS,BRASS,TXT),
 ("버튼 '해제' 글자",BRASS,H('2A2119'),TXT),
 ("버튼 '동전 3,200'",BRASS,ACT,TXT),
 ("보관함 슬롯명 10px",H('5C574E'),CARD1,TXT),
 ("보관함 환급 금액",BRASS,H('141210'),TXT),
 ("프리셋 '미보유' 버튼",H('5C574E'),ACT,TXT),
]
nf=0
for n,fg,bg,fl in uses:
    v=CR(fg,bg); ok=v>=fl
    if not ok: nf+=1
    print(f"  {'PASS' if ok else 'FAIL'}  {n:30s} {hx(fg)}/{hx(bg)} = {v:6.2f}:1")
print(f"\n  ▶ 실사용 조합 {len(uses)}건 중 미달 {nf}건")

print()
print("=== C. 비텍스트(면·선·게이지) — 하한 3.00 ===")
nt=[
 ("헤더 하단 보더",H('1E1B18'),WIN),("컬럼 구분선",H('1E1B18'),WIN),
 ("탭 컨테이너 보더",H('221E1A'),WIN),("카드 보더(기본)",H('231F1B'),WIN),
 ("카드 보더(선택)",H('4A4036'),CARD1),("카드 보더(착용)",BRASS,WORN),
 ("강조 보더",H('2A2622'),WIN),("재화 칩 보더",H('3A3026'),ACT),
 ("게이지 트랙",H('221E1A'),CARD1),("게이지 채움(끝)",BRASS,H('221E1A')),
 ("게이지 채움(시작)",H('8A6C33'),H('221E1A')),("임계 눈금",H('0D0C0B'),BRASS),
 ("세트 도트(채움)",BRASS,CARD15),("세트 도트(미달)",H('2A2622'),CARD15),
 ("세트 마커 ○(비활성)",H('332E28'),CARD15),
 ("등급 리본 일반",H('8A8F98'),CARD1),("등급 리본 희귀",H('6E9BE8'),CARD1),
 ("등급 리본 영웅",H('B07BE0'),CARD1),("등급 리본 전설",H('E0B24A'),CARD1),
 ("토글 켜짐",BRASS,H('141210')),("토글 꺼짐",H('231F1B'),H('141210')),
 ("토글 노브(꺼짐)",H('6E665C'),H('231F1B')),
 ("스크롤 thumb",H('2A2622'),WIN),
 ("착용 오버레이 일반",H('D8B27A'),SLOT),("착용 오버레이 희귀",H('7FB0F2'),SLOT),
 ("착용 오버레이 영웅",H('C08FEC'),SLOT),("착용 오버레이 전설",H('F0C25C'),SLOT),
 ("캐릭터 흰 선",(255,255,255),SLOT),("캐릭터 검은 머리",H('111111'),SLOT),
 ("망토 뒤판",H('A8332A'),SLOT),("망토 선",H('7E1F17'),H('A8332A')),
 ("상점 탭 배지 5x5",BRASS,TABBG),
]
nf=0
for n,fg,bg in nt:
    v=CR(fg,bg); ok=v>=NONTXT
    if not ok: nf+=1
    print(f"  {'PASS' if ok else 'FAIL'}  {n:24s} {hx(fg)}/{hx(bg)} = {v:6.2f}:1")
print(f"\n  ▶ 비텍스트 {len(nt)}건 중 미달 {nf}건")

print()
print("=== D. 미보유 dim (opacity .34 합성 후) ===")
for base,bg,label in [(H('6E665C'),CARD1,"미보유 이름 #6E665C @.34")]:
    c=flat(base,0.34,bg); print(f"  합성색 {hx(c)}  대 카드 = {CR(c,bg):.2f}:1  (텍스트 하한 4.50 → {'PASS' if CR(c,bg)>=TXT else 'FAIL'})")
for r,name in [(H('8A8F98'),'일반'),(H('6E9BE8'),'희귀'),(H('B07BE0'),'영웅'),(H('E0B24A'),'전설')]:
    c=flat(r,0.25,CARD1); print(f"  등급 리본 {name} @.25 → {hx(c)} 대 카드 = {CR(c,CARD1):.2f}:1 (비텍스트 3.00 → {'PASS' if CR(c,CARD1)>=NONTXT else 'FAIL'})")

print()
print("=== E. 미달 잉크 2종을 살리려면 얼마나 밝혀야 하는가 ===")
for name,ink in [("라벨 #6E665C",H('6E665C')),("비활성 #5C574E",H('5C574E'))]:
    print(f"  {name}: 최악 표면 #1D1813(착용카드) 대비 {CR(ink,H('1D1813')):.2f}:1  / 최고 표면 #0D0C0B {CR(ink,WIN):.2f}:1")
print("  같은 색조를 유지하며 4.50을 넘기는 최소 밝기(착용카드 #1D1813 기준, HSV 밝기만 올림):")
def scale_to(ink,bg,target):
    import math
    for k in range(100,401):
        c=tuple(min(255,round(ch*k/100)) for ch in ink)
        if CR(c,bg)>=target: return c,k/100
    return None,None
for name,ink in [("라벨 #6E665C",H('6E665C')),("비활성 #5C574E",H('5C574E'))]:
    c,k=scale_to(ink,H('1D1813'),TXT)
    allbg=min(CR(c,b) for _,b in [("",WIN),("",CARD1),("",CARD2),("",ACT),("",WORN),("",TABBG),("",SLOT),("",H('1B1713')),("",H('151311')),("",H('111010'))])
    print(f"    {name} → {hx(c)} (×{k:.2f})  7표면 최악 {allbg:.2f}:1  {'PASS' if allbg>=TXT else 'FAIL'}")
print("  참고: 우리 기존 잉크 서열 — TextTertiary #8B939F / NonTextMuted #6C7480(글자 금지)")
for n,c in [("#8B939F(우리 3단)",H('8B939F')),("#A79E90(핸드오프 보조)",H('A79E90')),("#8A8578(핸드오프 3차)",H('8A8578'))]:
    print(f"    {n} 최악 {min(CR(c,b) for b in [WIN,CARD1,CARD2,ACT,WORN,TABBG,SLOT,H('1B1713'),H('151311'),H('111010')]):.2f}:1")

print()
print("=== F. ★ '누를 수 있는 것의 면' — 오늘 밤 [✕](1.00:1)가 걸린 그 검사 ===")
print("   ControlFaceContrastTarget = 3.00 × 1.20 = 3.60 / ControlInkContrastTarget = 4.50 × 1.15 = 5.175")
FT, IT = 3.6, 5.175
faces=[
 ("동전 칩 면",ACT,WIN,BRASS),
 ("보유 칩 면(배경 없음 = 창 그대로)",WIN,WIN,H('A79E90')),
 ("탭 컨테이너 면",TABBG,WIN,None),
 ("탭 비활성 면(컨테이너 그대로)",TABBG,TABBG,H('8A8578')),
 ("탭 활성 면",BRASS,TABBG,ONBRS),
 ("카드 면(기본)",CARD1,WIN,H('EDE7DB')),
 ("카드 면(착용)",WORN,WIN,H('EDE7DB')),
 ("버튼 '착용' 면",BRASS,CARD1,ONBRS),
 ("버튼 '해제' 면",H('2A2119'),CARD1,BRASS),
 ("버튼 '동전 N' 면",ACT,CARD1,BRASS),
 ("버튼 '미보유'(프리셋) 면",ACT,CARD1,H('5C574E')),
 ("스탯칩 면",H('1B1713'),CARD1,BRASS),
 ("단계칩 면",H('1B1713'),CARD1,BRASS),
 ("착용행 면(착용)",ACT,WIN,H('EDE7DB')),
 ("착용행 면(빔)",H('111010'),WIN,H('5C574E')),
 ("아이콘 슬롯 면",SLOT,CARD1,None),
 ("판매 환급 바 면",H('141210'),CARD1,BRASS),
]
nf=0
for n,face,bg,ink in faces:
    fv=CR(face,bg); fok=fv>=FT
    s=f"  {'PASS' if fok else 'FAIL'}  {n:30s} 면 {hx(face)}/{hx(bg)} = {fv:5.2f}:1"
    if ink is not None:
        iv=CR(ink,face); s+=f"   잉크 {hx(ink)} = {iv:5.2f}:1 {'ok' if iv>=IT else '<5.175'}"
    if not fok: nf+=1
    print(s)
print(f"\n  ▶ 면 {len(faces)}건 중 3.60 미달 {nf}건")

print()
print("=== G. 창 크기 산술 (캔버스 1유닛 = OS 논리 포인트) ===")
print("   ClampPanelToScreen:  width = min(PanelWidth, max(320, Screen.w/scale - 32))")
print("                        height= min(PanelH,    max(320, Screen.h/scale - 32))")
cases=[("mac 1512×982 (@2x Retina)",1512,982),
       ("Win 2560×1600 @100%",2560,1600),
       ("Win 2560×1600 @125%",2560/1.25,1600/1.25),
       ("Win 2560×1600 @150%",2560/1.5,1600/1.5),
       ("Win 2560×1600 @175%",2560/1.75,1600/1.75),
       ("Win 2560×1600 @200%",1280,800),
       ("Win 1920×1080 @100%",1920,1080),
       ("Win 1920×1080 @150%",1280,720)]
for name,w,h in cases:
    aw,ah=w-32,h-32
    cur = ("OK " if (880<=aw and 861<=ah) else "잘림")
    new = ("OK " if (1242<=aw and 802<=ah) else "잘림")
    print(f"  {name:26s} 논리 {w:7.1f}×{h:6.1f}  가용 {aw:7.1f}×{ah:6.1f}   현행 880×861 {cur}"
          f"(부족 {max(0,880-aw):.0f}×{max(0,861-ah):.0f})   핸드오프 1242×802 {new}(부족 {max(0,1242-aw):.0f}×{max(0,802-ah):.0f})")
print()
for name,w,h in [("mac 1512×982",1512,982),("Win @200% 1280×800",1280,800),("Win @150% 1706×1067",1706.67,1066.67)]:
    scr=w*h
    print(f"  {name:22s} 화면 {scr:,.0f}pt²   현행 880×861 = {880*861/scr*100:5.1f}%   핸드오프 1242×802 = {1242*802/scr*100:5.1f}%")
