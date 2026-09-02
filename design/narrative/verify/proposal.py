# -*- coding: utf-8 -*-
"""
신규 대사 제안(세트 42 + 요일·시간대 7 + 1회성 8 + 신규 9종 11)의 가독예산 전수 검산.
계산기는 census.py 가 소유한다 — 상수를 두 번 정의하지 않는다.
★ 사용법:  python3 design/narrative/verify/proposal.py       (저장소 루트에서)
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from census import reading, required, maxvis

SETS = {
 "A": dict(idle=["다 갖춰 입었다","이거 좀 괜찮네","오늘은 좀 다르지","폼은 나는데"],
           walk=["발걸음이 가볍네","망토가 따라온다","가볍게 걷는다"]),
 "B": dict(idle=["따뜻하다","여기 아늑하네","좀 나른하다","이대로 있고 싶다"],
           walk=["천천히 간다","망토가 길다","발끝이 가볍다"]),
 "C": dict(idle=["준비는 끝났다","여기서 잠깐","장비 점검 완료","시야 확보"],
           walk=["계속 간다","목도리가 날린다","발이 알아서 간다"]),
 "D": dict(idle=["흠, 나쁘지 않군","여기 서 있겠다","짐은 만족한다","위엄이 좀 있나"],
           walk=["행차한다","천천히 걷는다","배낭이 흔들린다"]),
 "E": dict(idle=["생각 중이다","이 자리가 좋다","잠시 멈춰 본다","서두를 것 없다"],
           walk=["걸으며 생각한다","판초가 흔들린다","천천히 움직인다"]),
 "F": dict(idle=["여기가 좋겠군","한숨 돌린다","이 정도면 충분해","발 딛고 섰다"],
           walk=["간다","반다나가 날린다","거침없이 간다"]),
}
OCC = [("Idle/월요일","월요일이네..."),("Walk/월요일","다리가 무겁네"),
       ("Idle/금요일","금요일이다!"),("Walk/금요일","발이 빨라지네"),
       ("Idle/아침","아침이네"),("Idle/점심","점심시간이네"),("Idle/밤","밤이 깊었네")]
PULSE = [("저전력","배터리 아끼는 중"),("복귀 3~6일","오랜만이야"),("복귀 7~29일","많이 기다렸어"),
         ("복귀 30일+","정말 오랜만이야"),
         ("세트힌트 Head","모자만 더"),("세트힌트 Eyes","안경만 더"),
         ("세트힌트 Neck","넥타이만 더"),("세트힌트 Back","망토만 더")]
NEW9 = [("Fishing","던졌다",3.00),("Fishing","안 물리네",1.00),("Fishing","월척!",None),
        ("Fishing","오늘은 아닌가",None),("RopeClimb","걸렸다",2.19),("RopeClimb","올라왔다",None),
        ("WallpaperGazing","잘 골랐네",2.60),("CursorPetting","가만있어 봐",1.46),
        ("WindowShadeNap","잠깐만...",8.00),("WindowShadeNap","잘 잤다",None),
        ("TaskbarPolish","문지르는 중",10.20)]

IDLE_MIN, WALK_MIN = 2.00, 1.50
CAP = 9
print("=== 세트 대사 풀 (Idle 하한 2.00초 / Walk 하한 1.50초, 9자 상한) ===")
bad=0; total=0
for s,d in SETS.items():
    for kind,lines,dmin in (("Idle",d["idle"],IDLE_MIN),("Walk",d["walk"],WALK_MIN)):
        for t in lines:
            total+=1
            req=required(t); ok = req<=dmin and len(t)<=CAP
            if not ok: bad+=1
            print(f" {s} {kind:<4} {t:<16} {len(t):>2}자 가독{reading(t):.3f} 필요{req:.3f} 하한{dmin:.2f} 노출상한{maxvis(t):.2f} {'OK' if ok else '★위반'}")
print(f"세트 대사 {total}줄, 위반 {bad}\n")
print("=== 요일/시간대 (중립 풀 편입, 자격 술어로 거른다) ===")
for k,t in OCC:
    dmin = IDLE_MIN if k.startswith("Idle") else WALK_MIN
    req=required(t); print(f" {k:<12} {t:<14} {len(t):>2}자 필요{req:.3f} 하한{dmin:.2f} {'OK' if req<=dmin and len(t)<=CAP else '★위반'}")
print("\n=== 1회성 펄스(반응 — 게이트 무관) ===")
for k,t in PULSE:
    print(f" {k:<14} {t:<14} {len(t):>2}자 가독{reading(t):.3f} 최소노출{reading(t):.3f} 노출상한{maxvis(t):.2f}")
print("\n=== 신규 9종 (내 개정안) ===")
for st,t,dmin in NEW9:
    req=required(t)
    v = "통과(반응)" if dmin is None else ("통과" if dmin>=req else "★침묵")
    print(f" {st:<16} {t:<14} {len(t):>2}자 가독{reading(t):.3f} 필요{req:.3f} 잔여{('  —' if dmin is None else f'{dmin:.2f}')} {v}")
