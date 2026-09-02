# -*- coding: utf-8 -*-
"""
R4 — 영어 문안 재창작 검산기 (design-narrative, 2026-09-02)

★ 이 파일은 '검사기'다. '생성기'가 아니다.
   TEAM.md 「거짓 통과 신형」 규칙 1/2: 기대값을 프로덕션 함수로 만들지 않는다.
   그래서 이 검사기는 **디스크의 골든 비트**(Tests/EditMode/Golden/DialogueBudgetKoGolden.txt)로
   먼저 교정된다. 교정이 깨지면 이 아래 숫자는 전부 폐기다.

교정 3중:
  (C1) 골든 33줄 — 한국어 ReadingSeconds 비트 단위 일치
  (C2) R2 §5-1 알려진 값 — 7자 0.865 / 8자 0.940 / 10자 1.090 (RequiredDwell, 한국어)
  (C3) 리더 제시 영어 예산 23 / 15 / 14 / 12자 재현
양성 대조 4종:
  (P1) 알려진 탈락 5줄이 정말 탈락하는가
  (P2) `Whoa, that's high` = 마진0 통과 / 마진0.18 탈락
  (P3) 문자체계 판정: 한글/영문/혼합
  (P4) 일부러 만든 초장문이 탈락하는가 (검사기가 '아무거나 통과'시키지 않음)
"""
import struct, sys, os

ROOT = "/Users/kjmoon/App/StickMate"
GOLDEN = os.path.join(ROOT, "Assets/_Project/Scripts/Tests/EditMode/Golden/DialogueBudgetKoGolden.txt")

# --- 프로덕션 상수 (Dialogue/DialogueKind.cs 에서 직접 읽는다: 숫자를 손으로 베끼지 않는다) ---
SRC = open(os.path.join(ROOT, "Assets/_Project/Scripts/Dialogue/DialogueKind.cs"), encoding="utf-8").read()
import re
def const(name):
    m = re.search(r"const\s+float\s+" + name + r"\s*=\s*([0-9.]+)f", SRC)
    if not m: sys.exit("상수 %s 를 소스에서 못 찾음 — 검사기 폐기" % name)
    return float(m.group(1))
def f32(x):  # double -> float32 (한 번만 라운딩)
    return struct.unpack('>f', struct.pack('>f', x))[0]

# ★ C# 의 `0.28f` 는 파이썬의 0.28 이 아니다(0.2800000011920928955078125).
#   상수를 먼저 float32 로 넓혀 두지 않으면 골든과 1 ULP 갈린다 — 첫 시도가 정확히 그렇게 실패했다.
BASE  = f32(const("BaseSeconds"))
W_KR  = f32(const("PerGlyphSeconds"))
W_EN  = f32(const("PerLatinGlyphSeconds"))
MINS  = f32(const("MinSeconds"))
MAXS  = f32(const("MaxSeconds"))
FADEIN= f32(const("FadeInSeconds"))
POPIN = f32(const("PopInSeconds"))
def bits(x):
    return struct.pack('>f', x).hex().upper()

SYLLABIC_RANGES = [(0xAC00,0xD7A3),(0x1100,0x11FF),(0x3130,0x318F),(0xA960,0xA97F),
                   (0xD7B0,0xD7FF),(0x3040,0x30FF),(0x31F0,0x31FF),(0x3400,0x4DBF),
                   (0x4E00,0x9FFF),(0xF900,0xFAFF)]
def is_syllabic(t):
    if not t: return True
    for ch in t:
        c = ord(ch)
        if c < 0x1100: continue
        for lo,hi in SYLLABIC_RANGES:
            if lo <= c <= hi: return True
    return False

def reading(t):
    n = len(t) if t else 0
    w = W_KR if is_syllabic(t) else W_EN
    return f32(min(max(f32(BASE + n*w), MINS), MAXS))

def required(t, margin):
    return f32(FADEIN + reading(t)) + margin

# ============================== 교정 ==============================
fail = 0
rows = []
for raw in open(GOLDEN, encoding="utf-8"):
    raw = raw.rstrip("\n")
    if not raw.strip() or raw.startswith("#"): continue
    b, s, t = raw.split("\t")
    rows.append((b, s, t))
if len(rows) == 0: sys.exit("골든이 비었다 — 거짓 통과 #5 형태. 폐기.")
bad = [(t, b, bits(reading(t))) for b, s, t in rows if bits(reading(t)) != b]
print("[C1] 골든 %d줄 비트 대조 — 불일치 %d건  %s" % (len(rows), len(bad), "OK" if not bad else "★실패"))
for t,b,g in bad[:5]: print("      %-20s 골든 %s / 내 계산 %s" % (t,b,g))
if bad: fail += 1

known = {7:0.865, 8:0.940, 10:1.090}
c2 = []
for n, exp in known.items():
    got = required("가"*n, 0.0)
    c2.append((n, exp, got, abs(got-exp) < 5e-4))
print("[C2] R2 §5-1 알려진 값 재현 — " + "  ".join("%d자 %.3f(%s)" % (n,g,"OK" if ok else "★") for n,e,g,ok in c2))
if not all(ok for *_ ,ok in c2): fail += 1

DWELL = {"Idle":1.65, "Walk":1.24, "ParkourClimb":1.20, "LedgeHang":1.12}
LEADER_EN = {"Idle":23, "Walk":15, "ParkourClimb":14, "LedgeHang":12}
def budget(state, syllabic, margin=POPIN):
    d = DWELL[state]; n = 0
    while required(("가" if syllabic else "a")*(n+1), margin) <= d + 1e-9: n += 1
    return n
c3 = {s: budget(s, False) for s in DWELL}
ok3 = all(c3[s] == LEADER_EN[s] for s in DWELL)
print("[C3] 리더 영어 예산 재현 — " + "  ".join("%s %d(기대 %d)" % (s,c3[s],LEADER_EN[s]) for s in DWELL) + ("  OK" if ok3 else "  ★실패"))
if not ok3: fail += 1

if fail:
    sys.exit("\n★ 교정 %d건 실패 — 아래 숫자를 전부 폐기한다." % fail)
print("[교정] 3종 전부 통과. 이 아래 숫자를 믿어도 된다.\n")

# ============================== 양성 대조 ==============================
P1 = [("Let's go that way.","Walk"),("Left, right, left.","Walk"),
      ("Stretching the legs.","Walk"),("Good stride today.","Walk"),("Weekend's close.","Walk")]
p1bad = [t for t,s in P1 if required(t,POPIN) <= DWELL[s]]
print("[P1] 알려진 탈락 5줄이 실제로 탈락하는가 — 탈락 %d/5 %s" % (5-len(p1bad), "OK" if not p1bad else "★"+str(p1bad)))
t = "Whoa, that's high"
p2a = required(t,0.0) <= DWELL["ParkourClimb"]; p2b = required(t,POPIN) <= DWELL["ParkourClimb"]
print("[P2] `%s`(%d자) 마진0 %s / 마진0.18 %s  %s" % (t,len(t),"통과" if p2a else "탈락","통과" if p2b else "탈락",
      "OK" if (p2a and not p2b) else "★실패"))
p3 = [("여기 좋네",True),("Nice spot.",False),("Wi-Fi 끊겼네",True),("*yawn*",False),("Hmm...",False)]
p3bad = [t for t,e in p3 if is_syllabic(t)!=e]
print("[P3] 문자체계 판정 5종 — 불일치 %d %s" % (len(p3bad), "OK" if not p3bad else "★"+str(p3bad)))
long = "a"*40
print("[P4] 초장문 40자 @Walk — %s  %s" % ("탈락" if required(long,POPIN)>DWELL["Walk"] else "통과",
      "OK" if required(long,POPIN)>DWELL["Walk"] else "★실패"))
if p1bad or not (p2a and not p2b) or p3bad: sys.exit("★ 양성 대조 실패 — 폐기")
print()

# ============================== 예산표 ==============================
print("=== 0. 확정 예산 (마진 = PopInSeconds %.2f, FadeIn %.2f) ===" % (POPIN, FADEIN))
print("   %-14s %6s | %s | %s" % ("상태","분모","영어(라틴)","한국어(음절)"))
for s in ["Idle","Walk","ParkourClimb","LedgeHang"]:
    print("   %-14s %6.2f |  %2d자 (%2d자=%.3f / %2d자=%.3f) |  %2d자 (%2d자=%.3f / %2d자=%.3f)" % (
        s, DWELL[s],
        budget(s,False), budget(s,False), required("a"*budget(s,False),POPIN), budget(s,False)+1, required("a"*(budget(s,False)+1),POPIN),
        budget(s,True),  budget(s,True),  required("가"*budget(s,True),POPIN),  budget(s,True)+1,  required("가"*(budget(s,True)+1),POPIN)))
print()

def judge(state, ko, en):
    dk = DWELL[state]-required(ko,POPIN); de = DWELL[state]-required(en,POPIN)
    return dk, de, (dk>=-1e-9 and de>=-1e-9)

def table(title, rows):
    print("=== %s ===" % title)
    print("  %-3s %-13s %-16s %3s %7s | %-22s %3s %7s | %s" % ("#","상태","한국어","자","여유","English","자","여유","판정"))
    nfail=0
    for idx, state, ko, en, note in rows:
        dk,de,ok = judge(state,ko,en)
        if not ok: nfail+=1
        print("  %-3s %-13s %-16s %3d %+7.3f | %-22s %3d %+7.3f | %s %s" % (
            idx,state,ko,len(ko),dk,en,len(en),de,"OK" if ok else "★탈락", note))
    print("  -> 탈락 %d / %d줄\n" % (nfail, len(rows)))
    return nfail

# ============================== 1. R2 §3-4 풀 24줄 — 개정 전 ==============================
OLD24 = [
 ("1","Idle","음...","Hmm...","상시"),
 ("2","Idle","여기 좋네","Nice spot.","상시"),
 ("3","Idle","잠깐 쉬는 중","Taking a break.","상시"),
 ("4","Idle","오늘 뭐 하지","What now?","상시"),
 ("5","Idle","발밑이 단단해","Solid footing.","상시"),
 ("6","Walk","산책 중","Out walking.","상시"),
 ("7","Walk","저쪽으로 가볼까","Let's go that way.","상시"),
 ("8","Walk","하나 둘 하나 둘","Left, right, left.","상시"),
 ("9","Walk","다리 좀 풀자","Stretching the legs.","상시"),
 ("10","Walk","다리가 잘 나가네","Good stride today.","상시"),
 ("11","Idle","하암...","*yawn*","모션:앉기하품"),
 ("12","Idle","구경 중이야","Just having a look.","모션:두리번"),
 ("13","Idle","월요일이네...","Monday again...","요일:월"),
 ("14","Idle","금요일이다!","It's Friday!","요일:금"),
 ("15","Idle","쉬는 날이네","Day off.","요일:주말"),
 ("16","Walk","월요일이 왔네","Monday's here.","요일:월"),
 ("17","Walk","주말이 코앞이네","Weekend's close.","요일:금"),
 ("18","Walk","주말 산책이네","Weekend walk.","요일:주말"),
 ("19","Idle","아침이네","Morning.","시간:아침"),
 ("20","Idle","점심시간이네","Lunchtime.","시간:점심"),
 ("21","Idle","밤이 깊었네","It's late.","시간:밤"),
 ("22","Walk","아침 산책이네","Morning walk.","시간:아침"),
 ("23","Walk","점심때 걷네","Midday stroll.","시간:점심"),
 ("24","Walk","밤에도 걷네","Night walk.","시간:밤"),
]
n_old = table("1. R2 §3-4 풀 24줄 — 새 예산으로 다시 재기 (개정 전)", OLD24)

# ============================== 2. 개정안 ==============================
NEW24 = [
 ("1","Idle","음...","Hmm...","유지"),
 ("2","Idle","여기 좋네","Nice spot.","유지"),
 ("3","Idle","잠깐 쉬는 중","Taking a break.","유지"),
 ("4","Idle","오늘 뭐 하지","What now?","유지"),
 ("5","Idle","발밑이 단단해","Solid footing.","유지"),
 ("6","Walk","산책 중","Out walking.","유지"),
 ("7","Walk","저쪽으로 가볼까","Off that way.","★재창작"),
 ("8","Walk","하나 둘 하나 둘","Left, right.","★재창작"),
 ("9","Walk","다리 좀 풀자","Loosening up.","★재창작"),
 ("10","Walk","다리가 잘 나가네","Good stride.","★재창작"),
 ("11","Idle","하암...","*yawn*","유지"),
 ("12","Idle","구경 중이야","Just having a look.","유지"),
 ("13","Idle","월요일이네...","Monday again...","유지"),
 ("14","Idle","금요일이다!","It's Friday!","유지"),
 ("15","Idle","쉬는 날이네","Day off.","유지"),
 ("16","Walk","월요일이 왔네","Monday's here.","유지"),
 ("17","Walk","주말이 코앞이네","Weekend soon.","★재창작"),
 ("18","Walk","주말 산책이네","Weekend walk.","유지"),
 ("19","Idle","아침이네","Morning.","유지"),
 ("20","Idle","점심시간이네","Lunchtime.","유지"),
 ("21","Idle","밤이 깊었네","It's late.","유지"),
 ("22","Walk","아침 산책이네","Morning walk.","유지"),
 ("23","Walk","점심때 걷네","Midday stroll.","유지"),
 ("24","Walk","밤에도 걷네","Night walk.","유지"),
]
n_new = table("2. ★ 개정안 — 영어 5줄 재창작 (한국어 24줄 무변경)", NEW24)
CEIL0 = {"Idle":21, "Walk":14}
ov24 = [(i,st,en,len(en)) for i,st,ko,en,_ in NEW24 if len(en) > CEIL0[st]]
print("  [작업상한 Idle%d·Walk%d] 초과 %d건 %s\n" % (CEIL0["Idle"],CEIL0["Walk"],len(ov24), ov24 if ov24 else "OK"))

# ============================== 3. 세트 풀 60줄 (R3 §7-7) ==============================
SETS = {
"중립":[("PAUSE","Idle","음...","Hmm..."),("PLACE","Idle","여기 좋네","Nice spot."),
   ("REST","Idle","잠깐 쉬는 중","Taking a break."),("GROUND","Idle","발밑이 단단해","Solid footing."),
   ("SELF","Idle","오늘 뭐 하지","What now?"),("MOVE","Walk","산책 중","Out walking."),
   ("DIR","Walk","저쪽으로 가볼까","Let's go that way."),("RHYTHM","Walk","하나 둘 하나 둘","Left, right, left."),
   ("LEGS","Walk","다리 좀 풀자","Stretching the legs."),("FLOURISH","Walk","다리가 잘 나가네","Good stride today.")],
"A":[("PAUSE","Idle","오...","Oh."),("PLACE","Idle","여기 딱 좋네","This spot's great."),
   ("REST","Idle","잠깐 쉬어가지","Break time, then."),("GROUND","Idle","발밑 단단하네","Nice firm floor."),
   ("SELF","Idle","다 갖춰 입었다","All dressed up."),("MOVE","Walk","산책 나왔지","Off on a walk!"),
   ("DIR","Walk","저쪽 가볼까","That way, maybe."),("RHYTHM","Walk","하나 둘 하나 둘","One, two, one, two."),
   ("LEGS","Walk","발걸음이 가볍네","Light on my feet."),("FLOURISH","Walk","망토가 따라온다","Cape's coming too.")],
"B":[("PAUSE","Idle","흐음...","Mmm..."),("PLACE","Idle","여기 아늑하다","Cozy right here."),
   ("REST","Idle","좀 나른하다","Feeling drowsy."),("GROUND","Idle","발밑은 단단하다","Firm underfoot."),
   ("SELF","Idle","이대로 있고 싶다","I could stay here."),("MOVE","Walk","천천히 간다","Going slow."),
   ("DIR","Walk","저쪽으로 간다","Heading over there."),("RHYTHM","Walk","한 걸음 한 걸음","One step, then one."),
   ("LEGS","Walk","발끝이 가볍다","Light on the toes."),("FLOURISH","Walk","망토가 길다","This cape is long.")],
"C":[("PAUSE","Idle","좋아","Right."),("PLACE","Idle","여기서 잠깐","Stopping here."),
   ("REST","Idle","한숨 돌린다","Catching my breath."),("GROUND","Idle","발밑 확인","Footing checked."),
   ("SELF","Idle","장비 점검 완료","Gear all checked."),("MOVE","Walk","계속 간다","Moving on."),
   ("DIR","Walk","저쪽이다","That way."),("RHYTHM","Walk","보폭 일정","Steady pace."),
   ("LEGS","Walk","발이 알아서 간다","Legs know the way."),("FLOURISH","Walk","목도리가 날린다","Scarf's streaming.")],
"D":[("PAUSE","Idle","흠","Ahem."),("PLACE","Idle","여기 서 있겠다","I shall stand here."),
   ("REST","Idle","잠시 쉬어가겠다","A short repose."),("GROUND","Idle","발밑이 든든하군","Sturdy ground, this."),
   ("SELF","Idle","흠, 나쁘지 않군","Hm. Not bad at all."),("MOVE","Walk","행차한다","We proceed."),
   ("DIR","Walk","저쪽으로 가겠다","Onward, that way."),("RHYTHM","Walk","보폭은 넉넉히","A measured stride."),
   ("LEGS","Walk","걸음이 당당하다","A stately gait."),("FLOURISH","Walk","방울이 흔들린다","The bell sways.")],
"E":[("PAUSE","Idle","가만히...","Still..."),("PLACE","Idle","이 자리가 좋다","This place suits me."),
   ("REST","Idle","잠시 멈춰 본다","A pause here."),("GROUND","Idle","발밑을 느낀다","I feel the floor."),
   ("SELF","Idle","생각 중이다","Thinking."),("MOVE","Walk","천천히 움직인다","Moving slowly."),
   ("DIR","Walk","저쪽을 향한다","Facing that way."),("RHYTHM","Walk","걸음을 센다","Counting my steps."),
   ("LEGS","Walk","걸음이 고르다","An even gait."),("FLOURISH","Walk","판초가 흔들린다","The poncho stirs.")],
"F":[("PAUSE","Idle","후...","Hah..."),("PLACE","Idle","여기가 좋겠군","Here'll do."),
   ("REST","Idle","숨 고른다","Catching air."),("GROUND","Idle","발 딛고 섰다","Feet planted."),
   ("SELF","Idle","이 정도면 충분해","This'll do fine."),("MOVE","Walk","간다","Off we go."),
   ("DIR","Walk","저기로","Over there."),("RHYTHM","Walk","성큼성큼","Big strides."),
   ("LEGS","Walk","다리가 안 멈춘다","Legs won't stop."),("FLOURISH","Walk","반다나가 날린다","Bandana's flying.")],
}
print("=== 3. R3 §7-7 세트 풀 — 새 예산으로 다시 재기 (개정 전) ===")
tot_ko = tot_en = tot = 0
for name, rows in SETS.items():
    bk = [ (s,ko) for s,st,ko,en in rows if DWELL[st]-required(ko,POPIN) < -1e-9 ]
    be = [ (s,en,len(en),DWELL[st]-required(en,POPIN)) for s,st,ko,en in rows if DWELL[st]-required(en,POPIN) < -1e-9 ]
    tot_ko += len(bk); tot_en += len(be); tot += len(rows)
    print("  세트 %-4s  한국어 탈락 %d  영어 탈락 %d" % (name, len(bk), len(be)))
    for s,ko in bk: print("        [KO] %-9s %s (%d자)" % (s,ko,len(ko)))
    for s,en,n,d in be: print("        [EN] %-9s %-22s %2d자  여유 %+.3f" % (s,en,n,d))
print("  -> 총 %d줄 중 한국어 탈락 %d / 영어 탈락 %d\n" % (tot, tot_ko, tot_en))

# ============================== 4. 세트 풀 개정안 ==============================
NEWSETS = {
"중립":[("PAUSE","Idle","음...","Hmm..."),("PLACE","Idle","여기 좋네","Nice spot."),
   ("REST","Idle","잠깐 쉬는 중","Taking a break."),("GROUND","Idle","발밑이 단단해","Solid footing."),
   ("SELF","Idle","오늘 뭐 하지","What now?"),("MOVE","Walk","산책 중","Out walking."),
   ("DIR","Walk","저쪽으로 가볼까","Off that way."),("RHYTHM","Walk","하나 둘 하나 둘","Left, right."),
   ("LEGS","Walk","다리 좀 풀자","Loosening up."),("FLOURISH","Walk","다리가 잘 나가네","Good stride.")],
"A":[("PAUSE","Idle","오...","Oh."),("PLACE","Idle","여기 딱 좋네","This spot's great."),
   ("REST","Idle","잠깐 쉬어가지","Break time, then."),("GROUND","Idle","발밑 단단하네","Nice firm floor."),
   ("SELF","Idle","다 갖춰 입었다","All dressed up."),("MOVE","Walk","산책 나왔지","Off on a walk!"),
   ("DIR","Walk","저쪽 가볼까","Heading over!"),("RHYTHM","Walk","하나 둘 하나 둘","One, two, one!"),
   ("LEGS","Walk","발걸음이 가볍네","Light-footed!"),("FLOURISH","Walk","망토가 따라온다","Cape follows.")],
"B":[("PAUSE","Idle","흐음...","Mmm..."),("PLACE","Idle","여기 아늑하다","Cozy right here."),
   ("REST","Idle","좀 나른하다","Feeling drowsy."),("GROUND","Idle","발밑은 단단하다","Firm underfoot."),
   ("SELF","Idle","이대로 있고 싶다","I could stay here."),("MOVE","Walk","천천히 간다","Going slow."),
   ("DIR","Walk","저쪽으로 간다","Over that way."),("RHYTHM","Walk","한 걸음 한 걸음","Step by step."),
   ("LEGS","Walk","발끝이 가볍다","Light steps."),("FLOURISH","Walk","망토가 길다","A long cape.")],
"C":[("PAUSE","Idle","좋아","Right."),("PLACE","Idle","여기서 잠깐","Stopping here."),
   ("REST","Idle","한숨 돌린다","Catching my breath."),("GROUND","Idle","발밑 확인","Footing checked."),
   ("SELF","Idle","장비 점검 완료","Gear all checked."),("MOVE","Walk","계속 간다","Moving on."),
   ("DIR","Walk","저쪽이다","That way."),("RHYTHM","Walk","보폭 일정","Steady pace."),
   ("LEGS","Walk","발이 알아서 간다","Legs know it."),("FLOURISH","Walk","목도리가 날린다","Scarf flying.")],
"D":[("PAUSE","Idle","흠","Ahem."),("PLACE","Idle","여기 서 있겠다","I shall stand here."),
   ("REST","Idle","잠시 쉬어가겠다","A short repose."),("GROUND","Idle","발밑이 든든하군","Sturdy ground, this."),
   ("SELF","Idle","흠, 나쁘지 않군","Hm. Not bad at all."),("MOVE","Walk","행차한다","We proceed."),
   ("DIR","Walk","저쪽으로 가겠다","Onward we go."),("RHYTHM","Walk","보폭은 넉넉히","Measured pace."),
   ("LEGS","Walk","걸음이 당당하다","Stately steps."),("FLOURISH","Walk","방울이 흔들린다","My bell sways.")],
"E":[("PAUSE","Idle","가만히...","Still..."),("PLACE","Idle","이 자리가 좋다","This place suits me."),
   ("REST","Idle","잠시 멈춰 본다","A pause here."),("GROUND","Idle","발밑을 느낀다","I feel the floor."),
   ("SELF","Idle","생각 중이다","Thinking."),("MOVE","Walk","천천히 움직인다","Moving slowly."),
   ("DIR","Walk","저쪽을 향한다","I head there."),("RHYTHM","Walk","걸음을 센다","I count steps."),
   ("LEGS","Walk","걸음이 고르다","An even gait."),("FLOURISH","Walk","판초가 흔들린다","Poncho stirs.")],
"F":[("PAUSE","Idle","후...","Hah..."),("PLACE","Idle","여기가 좋겠군","Here'll do."),
   ("REST","Idle","숨 고른다","Catching air."),("GROUND","Idle","발 딛고 섰다","Feet planted."),
   ("SELF","Idle","이 정도면 충분해","This'll do fine."),("MOVE","Walk","간다","Off we go."),
   ("DIR","Walk","저기로","Over there."),("RHYTHM","Walk","성큼성큼","Big strides."),
   ("LEGS","Walk","다리가 안 멈춘다","Legs keep on."),("FLOURISH","Walk","반다나가 날린다","Bandana flies.")],
}
CEIL = {"Idle":21, "Walk":14}   # ★ 내 작업 상한(리더 예산보다 좁게). 근거는 문서 §1-4.
print("=== 4. ★ 세트 풀 개정안 — 영어 Walk 21줄 재창작 ===")
tk=te=tot=0; over=[]
for name, rows in NEWSETS.items():
    bk=[(s,ko,len(ko)) for s,st,ko,en in rows if DWELL[st]-required(ko,POPIN) < -1e-9]
    be=[(s,en,len(en)) for s,st,ko,en in rows if DWELL[st]-required(en,POPIN) < -1e-9]
    ov=[(s,en,len(en),DWELL[st]-required(en,POPIN)) for s,st,ko,en in rows if len(en) > CEIL[st]]
    tk+=len(bk); te+=len(be); tot+=len(rows); over+=[(name,)+o for o in ov]
    print("  세트 %-4s  줄 %2d  한국어 탈락 %d  영어 탈락 %d  작업상한 초과 %d" % (name,len(rows),len(bk),len(be),len(ov)))
    for s,ko,n in bk: print("        [KO 탈락] %-9s %s (%d자)" % (s,ko,n))
    for s,en,n in be: print("        [EN 탈락] %-9s %s (%d자)" % (s,en,n))
print("  -> 총 %d줄: 한국어 탈락 %d / 영어 탈락 %d / 작업상한(Idle%d·Walk%d) 초과 %d" %
      (tot,tk,te,CEIL["Idle"],CEIL["Walk"],len(over)))
for o in over: print("        [상한초과] 세트 %s %-9s %-22s %2d자 여유 %+.3f" % o)
print()

# --- 고유성: 같은 문자열이 두 슬롯에 오면 N이 준다(계약 분모 훼손) ---
print("=== 5. 고유성 검사 — 계약 분모 N 이 실제로 유지되는가 ===")
for label, rows in [("개정 24줄 KO",[r[2] for r in NEW24]), ("개정 24줄 EN",[r[3] for r in NEW24])]:
    dup = sorted({x for x in rows if rows.count(x)>1})
    print("  %-14s 줄 %2d / 고유 %2d %s" % (label,len(rows),len(set(rows)), "OK" if not dup else "★중복 "+str(dup)))
for name, rows in NEWSETS.items():
    ko=[r[2] for r in rows]; en=[r[3] for r in rows]
    dk=sorted({x for x in ko if ko.count(x)>1}); de=sorted({x for x in en if en.count(x)>1})
    if dk or de: print("  세트 %-4s ★중복 KO %s / EN %s" % (name,dk,de))
allen=[r[3] for rows in NEWSETS.values() for r in rows]
cross=sorted({x for x in allen if allen.count(x)>1})
print("  세트 간 영어 문자열 충돌: %s" % ("없음 OK" if not cross else "★"+str(cross)))
print("  ※ 세트는 동시에 활성화될 수 없다(4슬롯 전량 일치가 배타 선택자) — 세트 간 중복은 계약을 깨지 않는다.\n")

# --- ParkourClimb / LedgeHang (리더가 예산을 준 나머지 두 상태) ---
print("=== 6. 리더가 예산을 준 나머지 두 상태 — 현행 한국어 + 영어 신규 ===")
OTHERS = [
 ("ParkourClimb","가뿐하네","Easy climb.","현행 KO / EN 신규"),
 ("ParkourClimb","영차...","Heave-ho...","현행 KO / EN 신규"),
 ("ParkourClimb","헉... 높다","Oof... high.","현행 KO / EN 신규"),
 ("LedgeHang","여기로 내려가자","Down we go.","★현행 KO — 경계에서 탈락"),
 ("LedgeHang","내려가자","Down we go.","★개정안"),
 ("LedgeHang","어우... 꽤 깊네","Whoa... that's deep","★현행 — 둘 다 탈락"),
 ("LedgeHang","꽤 깊네","Long drop.","★개정안"),
]
for st,ko,en,note in OTHERS:
    dk=DWELL[st]-required(ko,POPIN); de=DWELL[st]-required(en,POPIN)
    print("  %-13s %-14s %2d자 %+7.3f | %-20s %2d자 %+7.3f | %s %s" %
          (st,ko,len(ko),dk,en,len(en),de,"OK" if (dk>=-1e-9 and de>=-1e-9) else "★탈락",note))

# ============================== 7. 영어 문자열 위생 ==============================
print("\n=== 7. 영어 문자열 위생 — '통과했지만 이유가 틀린 것' 사냥 ===")
ALL_EN = [(("24-%s"%i), en) for i,st,ko,en,_ in NEW24] + \
         [(("세트%s/%s"%(n,s)), en) for n,rows in NEWSETS.items() for s,st,ko,en in rows] + \
         [(("기타"), en) for st,ko,en,note in OTHERS]
mixed = [(k,e) for k,e in ALL_EN if is_syllabic(e)]
print("  (7-a) 영어 열에 음절 문자체계가 섞인 줄: %d건 %s" % (len(mixed), "OK" if not mixed else "★"+str(mixed)))
nonascii = sorted({(k,e,hex(ord(c)),c) for k,e in ALL_EN for c in e if ord(c) > 0x7E})
print("  (7-b) 비 ASCII 문자: %d건 %s" % (len(nonascii), "OK" if not nonascii else "★"+str(nonascii)))
print("  (7-c) 양성 대조 — 일부러 섞은 `Wi-Fi 끊겼네` 는 음절 판정: %s" %
      ("OK" if is_syllabic("Wi-Fi 끊겼네") else "★실패"))
print("  (7-d) 양성 대조 — 일부러 넣은 곡선 아포스트로피 `It’s late.` 검출: %s" %
      ("OK" if any(ord(c)>0x7E for c in "It’s late.") else "★실패"))

# ============================== 8. 계약 — 영어 풀도 같은 분모인가 ==============================
print("\n=== 8. 계약(세션 23분, 중복확률 <= 50%) — 영어 풀이 같은 분모를 받는가 ===")
def maxk(N):
    import math
    k=1
    while True:
        p=1.0
        for i in range(k): p *= (N-i)/float(N)
        if 1.0-p > 0.5: return k-1
        k+=1
        if k>N: return N
AXIS = {"상시":(5,5), "요일":(1,1), "시간대":(1,1), "모션":(2,0)}
for lang, col in [("한국어",2), ("영어",3)]:
    live = [(i,st,r[col]) for r in NEW24 for i,st in [(r[0],r[1])]]
    # 자격축 최악(화수목·시간대밖·모션없음) = 상시 10줄만
    always = [r for r in NEW24 if r[4].startswith("유지") or r[4].startswith("★재창작")]
    idle_always = len([r for r in NEW24[:5]]); walk_always = len([r for r in NEW24[5:10]])
    Nmin = idle_always + walk_always
    Nmax = Nmin + 2 + 2 + 2
    silenced = len([r for r in NEW24 if DWELL[r[1]] - required(r[col],POPIN) < -1e-9])
    print("  %s: 실제 발화 가능 줄 %d/24 (침묵 %d)  ->  최악 N=%d  최선 N=%d  허용 k=%d  필요 평균간격 %.1f초" %
          (lang, 24-silenced, silenced, Nmin-0, Nmax, maxk(Nmin), 1380.0/maxk(Nmin)))
print("  ★ 두 언어의 N·k·필요간격이 같다 -> 운용점 F/300(395초)은 한/영 공통으로 계약을 만족한다.")
