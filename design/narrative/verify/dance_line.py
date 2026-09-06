# -*- coding: utf-8 -*-
"""
댄스 에피소드 진입 대사 1줄 — 실측 검산기 (design-narrative, 2026-09-06)

무엇을 재는가
  1) 프로덕션 대사 풀의 실제 크기 (문서가 아니라 .cs에서 센다)
  2) 서술(Narrative) / 반응(Reaction) 분리 후 «!» 사용 빈도
     -> 「느낌표는 반응의 표지다」가 실측인지 인상인지 가른다
  3) 후보 대사의 가독예산 (DialogueBudget.ReadingSeconds 재구현이 아니라
     **디스크의 골든 파일에서 같은 글자수 행을 찾아 대조**한다)
     ★ TEAM.md 규칙: 기대값을 프로덕션 함수로 만들지 마라.
  4) 금칙어 검사 (design-motion 제약 5건 + 오디오 계약에서 유도된 추가 금칙)
  5) 노출 타이밍 — 말풍선이 화면에 있는 구간과 실제로 춤이 도는 구간이 겹치는가
  6) 반복 노출 — 3시간 감상에서 같은 문장이 몇 번 뜨는가 (피로 램프 적분)

양성 대조가 전부 붙어 있다. 하나라도 깨지면 그 아래 숫자는 전부 무효다.
"""
import io, os, re, sys, struct, math

ROOT = "/Users/kjmoon/App/StickMate"
SCRIPTS = os.path.join(ROOT, "Assets/_Project/Scripts")
GOLDEN = os.path.join(SCRIPTS, "Tests/EditMode/Golden/DialogueBudgetKoGolden.txt")

FAIL = []


def check(name, cond, detail=""):
    print(("  [OK]  " if cond else "  [FAIL]") + " " + name + (("  " + detail) if detail else ""))
    if not cond:
        FAIL.append(name)


# ----------------------------------------------------------------------------
# 1. 프로덕션 대사 풀 — 실제 코드에서 센다
# ----------------------------------------------------------------------------
print("=" * 78)
print("1. 프로덕션 대사 풀 실측 (문서가 아니라 .cs)")
print("=" * 78)

RE_SAYREACT = re.compile(r'DialogueLine\.(Say|React)\(\s*"([^"]*)"')
RE_SELFRET = re.compile(r'TriggerSelfReturn\(\s*"([^"]*)"')
RE_CFG = re.compile(r'cfg\s*=>\s*"([^"]*)"')
RE_ARRAY = re.compile(r'private static readonly string\[\]\s+(\w+)\s*=\s*\{(.*?)\};', re.S)
RE_STR = re.compile(r'"([^"]*)"')

narrative = []   # (텍스트, 출처)
reaction = []

def read(p):
    with io.open(p, encoding="utf-8") as f:
        return f.read()


def strip_comments(s):
    """★ 이 저장소의 대사 배열 안에는 «옛 대사를 인용한 주석»이 들어 있다
    (AmbientChatter의 "발판 참 좁네" / "창 위는 미끄러워" — 삭제 근거를 적어 둔 것).
    주석을 안 지우면 **삭제된 대사가 살아 있는 대사로 집계된다.**
    첫 판에서 실제로 그렇게 나왔다(고유 56줄 vs 골든 42줄)."""
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    s = re.sub(r'^[ \t]*//.*?$', '', s, flags=re.M)
    s = re.sub(r'(?<![:"])//[^\n"]*$', '', s, flags=re.M)
    return s

# 1-a. AmbientChatter 배열 = 전부 Narrative (Resolve가 DialogueLine.Say 고정)
src = strip_comments(read(os.path.join(SCRIPTS, "Dialogue/AmbientChatter.cs")))
check("AmbientChatter.Resolve가 Say 고정(=Narrative)", "DialogueLine.Say(table[" in src)
for name, body in RE_ARRAY.findall(src):
    for s in RE_STR.findall(body):
        narrative.append((s, "AmbientChatter." + name))

# 1-b. GrabReactionLines 배열 = 전부 Reaction (Resolve가 DialogueLine.React 고정)
src = strip_comments(read(os.path.join(SCRIPTS, "Dialogue/GrabReactionLines.cs")))
check("GrabReactionLines.Resolve가 React 고정(=Reaction)", "DialogueLine.React(table[" in src)
for name, body in RE_ARRAY.findall(src):
    for s in RE_STR.findall(body):
        reaction.append((s, "GrabReactionLines." + name))

# 1-c. States/ 인라인 + TriggerSelfReturn + StickmanAgent 람다
for dirpath, _, files in os.walk(SCRIPTS):
    if os.sep + "Tests" + os.sep in dirpath + os.sep:
        continue
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(dirpath, fn)
        s = strip_comments(read(p))
        rel = os.path.relpath(p, SCRIPTS)
        for kind, text in RE_SAYREACT.findall(s):
            (narrative if kind == "Say" else reaction).append((text, rel))
        for text in RE_SELFRET.findall(s):
            reaction.append((text, rel + " (SelfReturn)"))
        if fn == "StickmanAgent.cs":
            for text in RE_CFG.findall(s):
                reaction.append((text, rel + " (집중모드 람다)"))

allpairs = narrative + reaction
uniq = sorted(set(t for t, _ in allpairs))
print("  발생 %d건 / 고유 %d줄  (서술 %d, 반응 %d)"
      % (len(allpairs), len(uniq), len(narrative), len(reaction)))

# 골든과 대조 — 이것이 추출기의 양성 대조다
gold = []
for line in read(GOLDEN).splitlines():
    if line.startswith("#") or not line.strip():
        continue
    bits, secs, text = line.split("\t")
    gold.append((bits, float(secs), text))
gold_texts = sorted(t for _, _, t in gold)
only_gold = sorted(set(gold_texts) - set(uniq))
only_src = sorted(set(uniq) - set(gold_texts))

# ★★ 2026-09-06 착지 후 — 이 단언은 «양방향 대조»의 오프라인 판이다.
#    지금 깨져 있고, 그것이 **실재하는 결함**이다(아래 PICK 절에서 원인을 특정한다).
check("추출 고유 집합 == 골든 집합", uniq == gold_texts,
      "고유 %d / 골든 %d  | 골든에만=%s | 소스에만=%s" % (len(uniq), len(gold_texts), only_gold, only_src))

# 양성 대조: 존재하지 않는 낱말은 0건이어야 한다
check("양성 대조 — 없는 낱말 프로브가 0건", not any("피루엣" in t for t in uniq))
check("양성 대조 — 있는 낱말 프로브가 1건 이상", any("놔줘" in t for t in uniq))

# ----------------------------------------------------------------------------
# 2. 느낌표는 반응의 표지인가 — 실측
# ----------------------------------------------------------------------------
print()
print("=" * 78)
print("2. 종류별 «!» 사용 — 새 서술 대사에 «!»를 붙일 것인가")
print("=" * 78)

nar_u = sorted(set(t for t, _ in narrative))
rea_u = sorted(set(t for t, _ in reaction))
nar_bang = [t for t in nar_u if "!" in t]
rea_bang = [t for t in rea_u if "!" in t]
print("  서술 %2d줄 중 «!» %d건" % (len(nar_u), len(nar_bang)), nar_bang)
print("  반응 %2d줄 중 «!» %d건" % (len(rea_u), len(rea_bang)), rea_bang)
check("서술에 «!»가 한 건도 없다 (=새 서술 대사도 «!» 없음)", len(nar_bang) == 0)
check("반응에는 «!»가 실제로 쓰인다 (양성 대조)", len(rea_bang) > 0)

# ----------------------------------------------------------------------------
# 3. 후보 가독예산 — 골든에서 같은 글자수 행을 찾아 대조 (프로덕션 함수 재구현 금지)
# ----------------------------------------------------------------------------
print()
print("=" * 78)
print("3. 후보 대사의 가독예산 — 골든 실측 행에서 되살린다")
print("=" * 78)

by_len = {}
for bits, secs, text in gold:
    by_len.setdefault(len(text), set()).add((bits, secs))

CANDIDATES = [
    "저절로 춤이 나와",     # ★ 채택안
    "몸이 먼저 춤춘다",
    "몸이 먼저 움직이네",
    "가만히 못 있겠네",
]
for c in CANDIDATES:
    n = len(c)
    rows = by_len.get(n)
    if rows and len(rows) == 1:
        bits, secs = list(rows)[0]
        # 같은 글자수 한글 대사가 골든에 이미 있으므로 그 값을 그대로 승계한다.
        print("  %-12s 글자 %2d -> 가독예산 %.6f초  골든비트 %s  (동일 글자수 실측행에서 승계)"
              % (c, n, secs, bits))
    else:
        print("  %-12s 글자 %2d -> 골든에 같은 글자수 행 %s"
              % (c, n, "없음" if not rows else "복수 %s" % rows))

PICK = "저절로 춤이 나와"
pick_rows = by_len.get(len(PICK))
check("채택안과 같은 글자수의 골든 행이 유일하게 존재한다", pick_rows is not None and len(pick_rows) == 1)
PICK_BITS, PICK_SECS = list(pick_rows)[0]

# 단조성 양성 대조: 글자수가 늘면 예산도 늘어야 한다
lens = sorted(by_len)
mono = all(min(s for _, s in by_len[a]) <= min(s for _, s in by_len[b])
           for a, b in zip(lens, lens[1:]))
check("골든이 글자수에 대해 단조 비감소 (추출·해석 건전성)", mono)

# 골든 1열(IEEE754 비트)이 2열과 실제로 같은 수인지 — 골든 자체의 무결성.
# 2열은 F6 반올림 표기이므로 «비트를 풀어서 6자리로 반올림»한 값과 비교한다.
bad_bits = [(b, s, t) for b, s, t in gold
            if round(struct.unpack(">f", bytes.fromhex(b))[0], 6) != round(s, 6)]
check("골든 1열 비트와 2열 초가 서로 일치", not bad_bits, str(bad_bits[:3]))
# ★ 양성 대조 — 그리고 여기서 골든 헤더의 주장("1열이 판정 기준이다")이 실측으로 확인된다.
_b, _s, _t = gold[0]
_ulp1 = "%08X" % (int(_b, 16) + 1)
_ulp1_val = struct.unpack(">f", bytes.fromhex(_ulp1))[0]
# (가) 2열(F6 초)은 1 ULP를 **못 본다** — 0.62에서 1 ULP는 약 6e-8이라 6자리 반올림에 흡수된다.
check("2열(F6 초)은 1 ULP 표류를 구조적으로 못 본다 = 1열이 판정 기준인 이유",
      round(_ulp1_val, 6) == round(_s, 6),
      "1ULP=%.9f vs %.6f" % (_ulp1_val, _s))
# (나) 1열(비트)은 1 ULP를 **본다**. 이것이 탐지 경로 생존 증명이다.
check("양성 대조 — 1열(비트)은 1 ULP 표류를 잡는다", _ulp1 != _b, "%s != %s" % (_ulp1, _b))
# (다) F6 해상도를 넘는 오차는 2열도 잡는다(2열이 죽은 열은 아니다).
_big = struct.pack(">f", _s + 1e-4).hex().upper()
check("양성 대조 — 2열도 F6 해상도 위의 오차는 잡는다",
      round(struct.unpack(">f", bytes.fromhex(_big))[0], 6) != round(_s, 6))

# ----------------------------------------------------------------------------
# 4. 금칙어 — design-motion 제약 5건 + 오디오 계약에서 유도한 추가 금칙
# ----------------------------------------------------------------------------
print()
print("=" * 78)
print("4. 금칙어 검사")
print("=" * 78)

BANNED = {
    "곡/가사/장르(제약3)": ["노래", "음악", "곡", "가사", "가수", "멜로디", "선율"],
    "소리 주장(오디오 계약)": ["소리", "볼륨", "스피커"],
    "박자/BPM(제약3)":       ["박자", "비트", "리듬", "템포", "박에", "장단"],
    "춤 종류 특정(제약4)":   ["피루엣", "발레", "스타점프", "문워크", "러닝맨", "로봇",
                              "프리샤트카", "말춤", "회전", "점프", "뒷걸음"],
    "피로(제약5)":           ["지쳤", "지친", "힘들", "피곤", "숨차", "헥헥", "쉬어야"],
    "팀 내부 용어(전례)":     ["발판", "에피소드", "휴지", "반응형", "창"],
    "미래형 예고(AmbientChatter 계약)": ["할게", "간다!", "이제부터", "출발"],
}
for group, words in BANNED.items():
    hit = [w for w in words if w in PICK]
    check("금칙 없음 — " + group, not hit, ("걸림: %s" % hit) if hit else "")

# ★ 양성 대조 — 7개 그룹 **각각**이 실제로 탐지 능력을 갖고 있는지 따로 증명한다.
#   («한 문장이 몇 건 걸렸나»만 세면, 조용히 죽은 그룹이 살아 있는 그룹 뒤에 숨는다.)
POISON = "이 노래 소리 박자에 맞춰 피루엣 도는데 지쳤다, 발판에서 이제부터 출발"
dead_groups = [g for g, ws in BANNED.items() if not any(w in POISON for w in ws)]
check("양성 대조 — 금칙 7개 그룹이 **각각** 고의 위반을 잡는다", not dead_groups,
      ("죽은 그룹: %s" % dead_groups) if dead_groups
      else "그룹별 히트 " + str({g: [w for w in ws if w in POISON] for g, ws in BANNED.items()}))

# 중복 검사 — 기존 42줄과 낱말이 겹치는가
def morphs(t):
    return set(re.findall(r"[가-힣]{2,}", t))
pick_m = morphs(PICK)
# ★ 2026-09-06 09:22 — 대사가 착지하면서 `uniq`에 **자기 자신**이 들어왔고, 이 단언이
#   «자기와 자기가 겹친다»로 빨간불을 냈다. 비교 대상은 «나를 뺀 나머지»다.
others = [t for t in uniq if t != PICK]
check("착지분을 뺀 비교 대상이 비어 있지 않다(0건을 훑고 통과하는 것 방지)", len(others) > 0,
      "비교 대상 %d줄" % len(others))
dups = {t: sorted(pick_m & morphs(t)) for t in others if pick_m & morphs(t)}
check("기존 대사와 2음절 이상 낱말 중복 0건", not dups, str(dups) if dups else "")
# 양성 대조 — 일부러 겹치는 문장을 넣으면 실제로 잡히는가
check("양성 대조 — 겹치는 문장을 섞으면 잡는다",
      bool({t: 1 for t in ["춤이 좋네"] if pick_m & morphs(t)}))

# ============================================================================
# 4-b. ★★ 착지 감사 (2026-09-06 09:15 이후) — 배치 권고가 지켜졌는가
# ============================================================================
print()
print("-" * 78)
print("4-b. 착지 감사 — 수집기가 착지한 대사를 실제로 보는가")
print("-" * 78)

DANCE = os.path.join(SCRIPTS, "States/DanceState.cs")
if not os.path.exists(DANCE):
    print("  (DanceState.cs 미착지 — 착지 감사 건너뜀)")
else:
    dsrc = read(DANCE)
    check("대사가 States/DanceState.cs 안에 있다(권고한 파일 ✔)", PICK in dsrc)
    # ★ 수집기 정규식은 «DialogueLine.Say( 안의 인라인 리터럴»만 본다.
    hits = RE_SAYREACT.findall(strip_comments(dsrc))
    got = [t for _, t in hits]
    check("★ 수집기 정규식이 그 대사를 잡는다", PICK in got,
          "DanceState.cs 히트=%s" % got)
    # 양성 대조 — 같은 정규식이 다른 상태 파일에서는 실제로 잡는가(프로브 생존)
    ctrl = [t for _, t in RE_SAYREACT.findall(read(os.path.join(SCRIPTS, "States/ParkourClimbState.cs")))]
    check("양성 대조 — 같은 정규식이 ParkourClimbState에서는 잡는다", len(ctrl) == 3, str(ctrl))
    # ★ 회귀 가드 — `const` 우회 형태가 다시 들어오면 여기서 잡는다.
    #   (2026-09-06 09:15에 실제로 그 형태로 착지해 수집기가 0건이었다. 09:22에 인라인으로 정정됨.)
    const_form = re.search(r'const\s+string\s+(\w+)\s*=\s*"' + re.escape(PICK) + r'"', dsrc)
    check("`const string` 우회 형태가 아니다(수집기가 못 보는 형태)",
          const_form is None,
          ("`const string %s`로 되돌아갔다" % const_form.group(1)) if const_form else "인라인 리터럴 ✔")

    # ★★ 극성 정정 (2026-09-06 09:22) — 원래 이 항목은 «재생성하면 사라진다»를 확인했다.
    #    그건 **결함이 살아 있을 때 통과하는 프로브**라, 결함이 고쳐지자 반대로 빨간불을 냈다.
    #    지켜야 할 불변식은 «수집기가 본다» 쪽이므로 그 방향으로 뒤집는다.
    #    (프로브의 극성을 결함에 맞춰 두면, 고쳐진 순간부터 그 프로브가 거짓말을 한다.)
    would_regen = sorted(set(uniq))
    check("★ 지금 골든을 재생성해도 이 대사가 골든에 남는다(조용한 초록 입구 봉쇄)",
          PICK in would_regen,
          "재생성 시 %d줄, 그 안에 채택안 %s" % (len(would_regen), "있음" if PICK in would_regen else "없음"))
    # 음성 대조 — 실재하지 않는 문장은 재생성 집합에 없어야 한다(이 프로브가 «항상 있음»을 내지 않는지)
    check("음성 대조 — 없는 대사는 재생성 집합에 없다", "저절로 춤이 안 나와" not in would_regen)
    # 골든과 소스가 실제로 같은 집합인가 (§1 단언의 재확인 — 두 방향 모두)
    check("골든과 소스 스캔이 이 대사에 대해 일치",
          (PICK in would_regen) and (PICK in gold_texts))

# ----------------------------------------------------------------------------
# 5. 노출 타이밍 — 말풍선이 떠 있는 동안 화면이 실제로 춤을 보여주는가
# ----------------------------------------------------------------------------
print()
print("=" * 78)
print("5. 노출 타이밍 (UX_MOTION_DANCE.md 1·5·6절 값)")
print("=" * 78)

FADE_IN, FADE_OUT, POP_IN = 0.06, 0.12, 0.18     # DialogueTiming
R = PICK_SECS
required_dwell = FADE_IN + R
max_visible = POP_IN + 2.0 * 1.0 * R + FADE_OUT   # m=1.0 (MinVisibleScale)

BRAKE = 0.28                                       # danceEntryBrakeSeconds
INTRO = {"D1 피루엣": 0.56, "D2 스타점프": 1.004, "D3 문워크": 0.36,
         "D4 러닝맨": 0.56, "D5 로봇": 0.20, "D6 프리샤트카": 0.84, "D7 말춤": 0.48}
EP_MIN = 8.16                                      # 최단 에피소드(D7)

print("  가독예산 R = %.3f초 / 필요체류 = %.3f초 / 화면상한 = %.3f초" % (R, required_dwell, max_visible))
check("규칙 8 통과 — 필요체류 <= 최단 에피소드+브레이크",
      required_dwell <= EP_MIN + BRAKE, "%.3f <= %.3f" % (required_dwell, EP_MIN + BRAKE))
check("말풍선이 에피소드 안에서 소멸한다(휴지로 새어나가지 않는다)",
      max_visible <= EP_MIN + BRAKE, "%.3f <= %.3f" % (max_visible, EP_MIN + BRAKE))
worst = max(INTRO.values())
print("  진입 브레이크+진입박자 최장 = %.3f초 (%s)"
      % (BRAKE + worst, [k for k, v in INTRO.items() if v == worst][0]))
overlap = max_visible - (BRAKE + worst)
check("최악(D2)에서도 말풍선이 «루프가 도는 화면»과 겹친다",
      overlap > 0, "겹침 %.3f초" % overlap)
for k, v in sorted(INTRO.items(), key=lambda kv: kv[1]):
    print("     %-12s 루프 시작 %.3f초 -> 말풍선과 겹치는 시간 %.3f초"
          % (k, BRAKE + v, max_visible - (BRAKE + v)))

# ----------------------------------------------------------------------------
# 6. 반복 노출 — 3시간 감상에서 같은 문장이 몇 번 뜨는가
# ----------------------------------------------------------------------------
print()
print("=" * 78)
print("6. 반복 노출 (7-2절 피로 램프 적분)")
print("=" * 78)

EP_AVG = 12.0                      # 에피소드 평균(문서 «12/(12+32.5x3.0)»의 분자)
REST_AVG = 32.5                    # U(20,45) 평균
def F(n):
    return min(1.0 + 0.25 * (n - 1), 3.0)

for horizon_min in (10, 30, 60, 180):
    t, n = 0.0, 1
    while True:
        t += EP_AVG + REST_AVG * F(n)
        if t > horizon_min * 60:
            break
        n += 1
    print("  연속 감상 %3d분 -> 에피소드 진입 약 %3d회 = 같은 문장 %3d회 노출"
          % (horizon_min, n, n))

t, n = 0.0, 1
while t <= 180 * 60:
    t += EP_AVG + REST_AVG * F(n)
    n += 1
n3h = n - 1
check("3시간 감상에서 노출 100회를 넘는다(=1줄 풀의 구조적 한계 실재)", n3h > 100,
      "%d회" % n3h)

# 기존 앰비언트와 비교 — 쿨다운 11초 + 확률 게이트가 있는 쪽
print("  참고: 앰비언트는 풀 13줄 + 쿨다운 11초 + 확률 게이트를 함께 쓴다.")
print("        댄스 진입 대사는 풀 1줄 + 게이트 없음 -> 같은 문장이 %d회." % n3h)

print()
print("=" * 78)
if FAIL:
    print("실패 %d건: %s" % (len(FAIL), FAIL))
    sys.exit(1)
print("전 항목 통과. 채택안 = «%s» (%d자, 가독예산 %.6f초, 골든비트 %s)"
      % (PICK, len(PICK), PICK_SECS, PICK_BITS))
