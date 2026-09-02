#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'handoff_palette_audit.py')).read().split('print("=== 교정 ===")')[0])
print("=== 교정 ==="); calibrate()
S=[1.25,1.5,1.75,2.0]
def integral(v): return all(abs(v*s-round(v*s))<1e-9 for s in S)

print("=== H. 핸드오프 타입 스케일의 배율 정수성 (Windows DPI 사다리) ===")
sizes=[21,17,16,15,14,13,12.5,12,11.5,11,10.5,10,9.5]
bad=[]
for v in sizes:
    ok=integral(v)
    if not ok: bad.append(v)
    print(f"  {'OK  ' if ok else 'FAIL'} {v:5}px → " + " / ".join(f"{v*s:g}" for s in S))
print(f"  ▶ 13계단 중 {len(bad)}건이 비정수: {bad}")
print("  참고: 우리 계단 20 / 14 / 12 / 10 — 전부 정수")
for v in [20,14,12,10]:
    print(f"    {'OK  ' if integral(v) else 'FAIL'} {v}px → " + " / ".join(f"{v*s:g}" for s in S))
print()

print("=== I. 세리프/산스 페어가 없을 때 무너지는 위계 ===")
print("  핸드오프: 제목·수치 = Noto Serif KR 600 / 본문·UI = Pretendard 400~600")
serif=[21,17,16,15,14,13]; sans=[13,12.5,12,11.5,11,10.5,10,9.5]
both=sorted(set(serif)&set(sans))
print(f"  두 계열이 <같은 px>를 공유하는 크기: {both}")
print("  → 그 크기에서는 <서체>만이 유일한 구분이다. 폰트가 하나면 위계가 사라진다.")
print("  해당 자리 예: 카드 이름 13px(Pretendard 600) vs 상세 메타/카테고리 13.5px 부근(Noto Serif 600)")
print()

print("=== J. 폭이 줄었을 때의 리플로우 — 핸드오프에 규칙이 없다. 최소치를 계산한다 ===")
COL1,COL2,DIV=306,292,2
PAD=26*2; GAP=12
inner=1240
col3=inner-COL1-COL2-DIV
card3=(col3-PAD-GAP*2)/3
print(f"  설계 폭 1240 내부: 컬럼3 = {col3} → 3열 카드 폭 {card3:.1f}pt")
for n in (3,2):
    need_col3=card3*n+GAP*(n-1)+PAD
    need=COL1+COL2+DIV+need_col3
    print(f"  카드 {n}열 유지 최소 내부폭 = {COL1}+{COL2}+{DIV}+{need_col3:.0f} = {need:.0f}  (창 {need+2:.0f})")
print(f"  컬럼2까지 접었을 때(1+3열) 최소 = {COL1+DIV+card3*2+GAP+PAD:.0f}")
print()

print("=== K. 화면 점유율 — 창 밖 클릭으로 안 닫히는 창이 얼마나 덮는가 ===")
for name,w,h in [("mac 1512×982",1512,982),("Win 2560×1600 @150% (1707×1067)",1706.67,1066.67),
                 ("Win 2560×1600 @200% (1280×800)",1280,800),("Win 1920×1080 @150% (1280×720)",1280,720)]:
    scr=w*h
    print(f"  {name:34s} 현행 880×861 {880*861/scr*100:5.1f}%   핸드오프 1242×802 {1242*802/scr*100:5.1f}%")
print()

print("=== L. 공정성 대조 — '카드 면이 낮다'는 우리 앱도 같은가 ===")
OURPANEL=(20,23,28); OURCARD=(27,31,38); OURMUTED=(21,24,30)
print(f"  우리 CardSurface #1B1F26 대 PanelSurface #14171C = {CR(OURCARD,OURPANEL):.2f}:1")
print(f"  핸드오프 카드 #161311 대 창 #0D0C0B                 = {CR(H('161311'),H('0D0C0B')):.2f}:1")
print("  → 카드 '면'의 낮은 대비는 핸드오프만의 결함이 아니다. 우리도 같다. 이 항목은 반려 사유가 아니다.")
print()
print("=== M. 진짜 문제는 '누를 수 있는 것'의 면이다 (우리 규칙 3.60) ===")
for n,face,bg in [("핸드오프 '동전 N' 버튼",H('17130E'),H('161311')),
                  ("핸드오프 '해제' 버튼",H('2A2119'),H('161311')),
                  ("핸드오프 '미보유' 버튼",H('17130E'),H('161311')),
                  ("핸드오프 토글 꺼짐",H('231F1B'),H('141210')),
                  ("(오늘 밤 고치기 전) 우리 [✕]",(21,23,28),(21,23,28)),
                  ("(고친 뒤) 우리 [✕] ChromeButtonSurface",(138,139,142),OURPANEL),
                  ("우리 카드 [착용] 버튼(미수정, §10)",(50,53,60),OURCARD)]:
    v=CR(face,bg); print(f"  {'PASS' if v>=3.6 else 'FAIL'}  {n:36s} {hx(face)}/{hx(bg)} = {v:5.2f}:1")
