# -*- coding: utf-8 -*-
"""세트 대사 풀 60줄(6세트 x Idle 5 + Walk 5) — 한/영 양쪽 발화 자격 검산 + 어조 마커 측정
+ ★ HemSway 실측 대조(움직임을 주장하는 줄이 실제로 흔들리는 아이템인가)."""
import re, os, sys, io, statistics
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
ROOT = "/Users/kjmoon/App/StickMate"
SRC  = os.path.join(ROOT, "Assets/_Project/Scripts")

k = open(os.path.join(SRC, "Dialogue/DialogueKind.cs"), encoding="utf-8").read()
def const(n):
    m = re.search(r"%s\s*=\s*([\d.]+)f" % n, k); assert m, n; return float(m.group(1))
BASE, PER, MINS, MAXS = const("BaseSeconds"), const("PerGlyphSeconds"), const("MinSeconds"), const("MaxSeconds")
FADEIN, POPIN = const("FadeInSeconds"), const("PopInSeconds")
W_EN = 0.0472                      # R2 §5-3 유도값 (en_budget.out.txt)
DWELL = {"Idle": 2.00, "Walk": 1.50}

def rd_kr(n): return min(max(BASE + n*PER,  MINS), MAXS)
def rd_en(n): return min(max(BASE + n*W_EN, MINS), MAXS)
for n, exp in ((10,1.090),(8,0.940),(7,0.865),(4,0.680),(2,0.680)):
    assert abs(FADEIN + rd_kr(n) - exp) < 1e-9, ("교정 실패", n, FADEIN+rd_kr(n), exp)
print("[교정] 필요체류 10/8/7/4/2자 = 1.090/0.940/0.865/0.680/0.680 재현 OK")

# ---------- ★ HemSway 실측: 어떤 액세서리가 실제로 흔들리는가 ----------
sb = open(os.path.join(SRC, "Interaction/AccessoryShapeBuilder.cs"), encoding="utf-8").read()
idx = {}
for m in re.finditer(r"internal const int ([A-Za-z, =0-9]+);", sb):
    for part in m.group(1).split(","):
        nm, v = part.split("="); idx[nm.strip()] = int(v)
cases = [(m.start(), m.group(1)) for m in re.finditer(r"case (Neck[A-Za-z]+|Back[A-Za-z]+):", sb)]
sways = [m.start() for m in re.finditer(r"swayStart:\s*\d+|swayStart: -1", sb)]
sway_by_case = {}
for i, (pos, nm) in enumerate(cases):
    end = cases[i+1][0] if i+1 < len(cases) else len(sb)
    sway_by_case[nm] = sum(1 for s in sways if pos < s < end)
# 양성 대조: 알려진 사실 2건 — 긴망토는 흔들리고, 배낭은 안 흔들린다
assert sway_by_case.get("BackLongCape", 0) > 0, "양성 대조 실패 — 긴망토조차 0건이면 스캐너가 고장난 것이다"
print("[양성대조] BackLongCape sway %d건 검출 — 스캐너 작동 확인" % sway_by_case["BackLongCape"])
print("\n[실측] 세트 슬롯 아이템의 HemSway 등록 수 (0 = 그 아이템은 절대 흔들리지 않는다)")
SETITEM = {"A":("NeckBowTie","나비넥타이","BackCape","짧은망토"),
           "B":("NeckStriped","줄무늬타이","BackLongCape","긴망토"),
           "C":("NeckScarf","목도리","BackWings","날개"),
           "D":("NeckBell","방울목걸이","BackBackpack","배낭"),
           "E":("NeckPendant","펜던트 목걸이","BackPoncho","판초"),
           "F":("NeckBandana","반다나","BackFairyWings","요정 날개")}
for s,(nk,nkn,bk,bkn) in SETITEM.items():
    print("   세트 %s | %-14s(%s) sway %d | %-14s(%s) sway %d"
          % (s, nkn, nk, sway_by_case.get(nk,0), bkn, bk, sway_by_case.get(bk,0)))
NOSWAY = [n for n in sway_by_case if sway_by_case[n] == 0]
print("   -> ★ 흔들리지 않는 것: %s" % ", ".join(NOSWAY))
print("      **이 아이템에 움직임을 주장하는 대사를 쓰면 원칙 1 위반이다.**")

# ---------- 60줄 ----------
SLOT_I = ["PAUSE","PLACE","REST","GROUND","SELF"]
SLOT_W = ["MOVE","DIR","RHYTHM","LEGS","FLOURISH"]
POOL = {
"중립": [("음...","Hmm..."),("여기 좋네","Nice spot."),("잠깐 쉬는 중","Taking a break."),
        ("발밑이 단단해","Solid footing."),("오늘 뭐 하지","What now?"),
        ("산책 중","Out walking."),("저쪽으로 가볼까","Let's go that way."),
        ("하나 둘 하나 둘","Left, right, left."),("다리 좀 풀자","Stretching the legs."),
        ("다리가 잘 나가네","Good stride today.")],
"A":   [("오...","Oh."),("여기 딱 좋네","This spot's great."),("잠깐 쉬어가지","Break time, then."),
        ("발밑 단단하네","Nice firm floor."),("다 갖춰 입었다","All dressed up."),
        ("산책 나왔지","Out for a walk."),("저쪽 가볼까","That way, maybe."),
        ("하나 둘 하나 둘","One, two, one, two."),("발걸음이 가볍네","Light on my feet."),
        ("망토가 따라온다","Cape's coming too.")],
"B":   [("흐음...","Mmm..."),("여기 아늑하다","Cozy right here."),("좀 나른하다","Feeling drowsy."),
        ("발밑은 단단하다","Firm underfoot."),("이대로 있고 싶다","I could stay here."),
        ("천천히 간다","Going slow."),("저쪽으로 간다","Heading over there."),
        ("한 걸음 한 걸음","One step, then one."),("발끝이 가볍다","Light on the toes."),
        ("망토가 길다","This cape is long.")],
"C":   [("좋아","Right."),("여기서 잠깐","Stopping here."),("한숨 돌린다","Catching my breath."),
        ("발밑 확인","Footing checked."),("장비 점검 완료","Gear all checked."),
        ("계속 간다","Moving on."),("저쪽이다","That way."),("보폭 일정","Steady pace."),
        ("발이 알아서 간다","Legs know the way."),("목도리가 날린다","Scarf's streaming.")],
"D":   [("흠","Ahem."),("여기 서 있겠다","I shall stand here."),("잠시 쉬어가겠다","A short repose."),
        ("발밑이 든든하군","Sturdy ground, this."),("흠, 나쁘지 않군","Hm. Not bad at all."),
        ("행차한다","We proceed."),("저쪽으로 가겠다","Onward, that way."),
        ("보폭은 넉넉히","A measured stride."),("걸음이 당당하다","A stately gait."),
        ("방울이 흔들린다","The bell sways.")],
"E":   [("가만히...","Still..."),("이 자리가 좋다","This place suits me."),("잠시 멈춰 본다","A pause here."),
        ("발밑을 느낀다","I feel the floor."),("생각 중이다","Thinking."),
        ("천천히 움직인다","Moving slowly."),("저쪽을 향한다","Facing that way."),
        ("걸음을 센다","Counting my steps."),("걸음이 고르다","An even gait."),
        ("판초가 흔들린다","The poncho stirs.")],
"F":   [("후...","Hah..."),("여기가 좋겠군","Here'll do."),("숨 고른다","Catching air."),
        ("발 딛고 섰다","Feet planted."),("이 정도면 충분해","This'll do fine."),
        ("간다","Off we go."),("저기로","Over there."),("성큼성큼","Big strides."),
        ("다리가 안 멈춘다","Legs won't stop."),("반다나가 날린다","Bandana's flying.")],
}
for nm, rows in POOL.items():
    assert len(rows) == 10, (nm, len(rows))

print("\n=== 1. 발화 자격 검산 — 60줄 + 중립 10줄, 한/영 양쪽 ===")
print("   여유 = 상태 잔여 하한 − 필요체류.  합격선 = 팝인 %.2f초 이상." % POPIN)
print("   풀 |슬롯      |상태 | 한국어           자 |  여유 | English              자 |  여유")
fails = []
kr_min = en_min = 99
for nm, rows in POOL.items():
    for i, (kr, en) in enumerate(rows):
        st = "Idle" if i < 5 else "Walk"
        slot = (SLOT_I + SLOT_W)[i]
        mk = DWELL[st] - (FADEIN + rd_kr(len(kr)))
        me = DWELL[st] - (FADEIN + rd_en(len(en)))
        kr_min = min(kr_min, mk); en_min = min(en_min, me)
        bad = mk < POPIN or me < POPIN
        if bad: fails.append((nm, kr, en, mk, me))
        if nm in ("중립","A","F") or bad:
            print("   %-3s|%-10s|%-5s| %-16s %2d |%+6.3f | %-20s %2d |%+6.3f %s"
                  % (nm, slot, st, kr, len(kr), mk, en, len(en), me, "★탈락" if bad else ""))
print("   ... (B/C/D/E 40줄은 같은 형식으로 전부 통과 — 아래 요약 참조)")
print("\n   총 %d줄 | 탈락 %d줄 | 최소 여유: 한국어 %+.3f초 / 영어 %+.3f초 (합격선 %+.3f)"
      % (sum(len(r) for r in POOL.values()), len(fails), kr_min, en_min, POPIN))
assert not fails, fails

print("\n=== 2. 글자수 상한 (R1 §3-4: 한국어 9자) ===")
mx = max((len(kr), kr, nm) for nm, rows in POOL.items() for kr, _ in rows)
print("   한국어 최장 = %d자 (%s / %s 풀)  ->  %s" % (mx[0], mx[1], mx[2], "상한 9자 준수 ✔" if mx[0] <= 9 else "★ 위반"))
mxe = max((len(en), en, nm) for nm, rows in POOL.items() for _, en in rows)
print("   영어  최장 = %d자 (%s / %s 풀)" % (mxe[0], mxe[1], mxe[2]))
# 상태별 영어 상한 재계산
for st, d in DWELL.items():
    n = 0
    while FADEIN + rd_en(n+1) <= d - POPIN + 1e-9: n += 1
    print("   개정 게이트에서 %s의 영어 상한 = %2d자 (여유 %.2f 확보 기준)" % (st, n, POPIN))

print("\n=== 3. 어조 마커 — '어조만 바뀌고 사실은 그대로'가 측정되는가 ===")
print("   어조 = (템포, 격식, 시선) 3축. 세 축이 전부 같은 풀 쌍이 있으면 어조 전환이 체감되지 않는다.")
THING = re.compile(r"^(발밑|발끝|발걸음|발이|발 |다리|걸음|보폭|망토|목도리|방울|판초|반다나)")
def markers(rows):
    kr = [x for x, _ in rows]
    def grade(t):
        if t.endswith(("네","지","해","야")):            return 1   # 해체
        if t.endswith(("군","겠다","한다","하군","히")):   return 3   # 격식
        if t.endswith("다"):                             return 2   # 평서
        return 0                                                   # 무종결
    g = [grade(t) for t in kr]
    tempo  = statistics.mean(len(t) for t in kr)
    formal = statistics.mean(g)
    thing  = sum(1 for t in kr if THING.match(t)) / len(kr)
    nofin  = sum(1 for x in g if x == 0) / len(kr)
    punct  = sum(1 for t in kr if any(c in t for c in "!?,"))
    return tempo, formal, thing, nofin, punct
print("   풀   | 템포(평균자) | 격식(0무종결~3격식) | 시선(사물주어) | 무종결 | 쉼표·부호")
M = {}
for nm, rows in POOL.items():
    M[nm] = markers(rows)
    print("   %-4s |     %4.1f자  |         %.2f        |     %.0f%%      |  %.0f%%  |   %d줄"
          % (nm, M[nm][0], M[nm][1], M[nm][2]*100, M[nm][3]*100, M[nm][4]))
print("\n   ★ 쌍별 최소 분리 — 두 풀이 세 축에서 얼마나 떨어져 있는가")
names = list(M)
worstpair, worstsep = None, 9e9
for i in range(len(names)):
    for j in range(i+1, len(names)):
        a, b = M[names[i]], M[names[j]]
        sep = abs(a[0]-b[0])/2.0 + abs(a[1]-b[1]) + abs(a[2]-b[2])*3.0
        if sep < worstsep: worstsep, worstpair = sep, (names[i], names[j])
        if sep < 0.60:
            print("   ★ %s vs %s : 분리도 %.2f — 어조가 겹친다" % (names[i], names[j], sep))
print("   가장 가까운 쌍 = %s vs %s, 분리도 %.2f (기준 0.60 이상)  -> %s"
      % (worstpair[0], worstpair[1], worstsep, "전 쌍 분리 ✔" if worstsep >= 0.60 else "★ 재작업 필요"))
print("   ※ 분리도 = |Δ템포|/2 + |Δ격식| + 3·|Δ시선|. 세 축 각각이 1점 안팎이 되도록 정규화한 값이다.")

print("\n=== 4. ★ 움직임 주장 감사 — 흔들린다고 말한 줄이 실제로 흔들리는가 ===")
CLAIM = {"A":("망토","BackCape"),"B":(None,None),"C":("목도리","NeckScarf"),
         "D":("방울","NeckBell"),"E":("판초","BackPoncho"),"F":("반다나","NeckBandana")}
MOTION = re.compile(r"흔들|날린|따라온|펄럭")
bad = 0
for nm, rows in POOL.items():
    if nm == "중립": continue
    for kr, en in rows:
        if not MOTION.search(kr): continue
        noun, key = CLAIM[nm]
        ok = key is not None and sway_by_case.get(key, 0) > 0
        print("   세트 %s | %-16s -> %-14s sway %d건 | %s"
              % (nm, kr, key, sway_by_case.get(key, 0), "참 ✔" if ok else "★ 거짓"))
        if not ok: bad += 1
print("   움직임 주장 줄 중 거짓 %d건." % bad)
print("\n   ★ 반례 대조 — R1 §3-5가 실제로 낸 줄을 같은 검사에 통과시켜 본다:")
for kr, key in (("배낭이 흔들린다","BackBackpack"), ("목도리가 날린다","NeckScarf")):
    print("     %-16s -> %-14s sway %d건 | %s"
          % (kr, key, sway_by_case.get(key,0), "참" if sway_by_case.get(key,0) else "★ 거짓 — 폐기 대상"))
