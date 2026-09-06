# -*- coding: utf-8 -*-
"""
시간대 5구간 확정 — 오후·저녁 4줄 검산기 (design-narrative, 2026-09-06)

★ 독립성 원칙 (TEAM.md «생성기와 검사기가 코드를 공유하면 둘 다 같은 방향으로 틀린다»):
  - 풀 24줄을 `design/narrative/verify/pool_effective.py`에서 **import 하지 않는다.**
    R2 §3-4 **마크다운 표를 직접 파싱**한다 -> 표와 .py가 갈라져 있으면 그것도 함께 드러난다.
  - 중복확률 식도 그쪽을 안 쓰고 여기서 다시 세운 뒤, R23이 **발표한 숫자로 교정**한다.
    교정이 깨지면 아래 숫자를 전부 폐기한다.
  - 가독예산은 ReadingSeconds를 흉내 내지 않고 **골든 파일의 같은 글자수 행**에서 승계한다.
"""
import io, os, re, sys

ROOT = "/Users/kjmoon/App/StickMate"
SCRIPTS = os.path.join(ROOT, "Assets/_Project/Scripts")
GOLDEN = os.path.join(SCRIPTS, "Tests/EditMode/Golden/DialogueBudgetKoGolden.txt")
R2 = os.path.join(ROOT, "design/narrative/2026-09-02_R2_발화빈도_풀24_영어게이트.md")

FAIL = []


def check(name, cond, detail=""):
    print(("  [OK]  " if cond else "  [FAIL]") + " " + name + (("  " + detail) if detail else ""))
    if not cond:
        FAIL.append(name)


def read(p):
    with io.open(p, encoding="utf-8") as f:
        return f.read()


# ============================================================================
# 0. 교정 — R23이 발표한 숫자를 내 식으로 재현한다. 깨지면 전부 폐기.
# ============================================================================
print("=" * 78)
print("0. 교정 — design-systems R23 발표값 재현")
print("=" * 78)


def repeat_prob(N, k):
    """k회 발화에서 같은 줄이 두 번 이상 나올 확률(생일 문제). N개에서 복원추출."""
    if k > N:
        return 1.0
    p = 1.0
    for i in range(k):
        p *= (N - i) / float(N)
    return 1.0 - p


def max_k(N, floor=0.5):
    """중복확률이 floor 이하로 유지되는 최대 k."""
    k = 1
    while repeat_prob(N, k + 1) <= floor:
        k += 1
    return k


CAL = [(10, 4, 0.496), (12, 4, 0.427), (16, 4, 0.333), (24, 6, 0.493), (9, 3, 0.309)]
for N, k, want in CAL:
    got = repeat_prob(N, k)
    check("교정 N=%-2d k=%d -> %.1f%%" % (N, k, want * 100), abs(got - want) < 0.001,
          "계산 %.1f%%" % (got * 100))
check("교정 — N=9에서 허용 k가 4->3으로 후퇴", max_k(9) == 3, "max_k(9)=%d" % max_k(9))
check("교정 — N=10/12/16에서 허용 k=4 유지",
      max_k(10) == 4 and max_k(12) == 4 and max_k(16) == 4)
if FAIL:
    print("\n★ 교정이 깨졌다. 아래 숫자를 신뢰할 수 없다.")
    sys.exit(1)
print("  교정 통과 — 아래 숫자를 신뢰한다")

# ============================================================================
# 1. 풀 24줄을 R2 §3-4 «마크다운 표»에서 직접 읽는다 (.py를 import 하지 않는다)
# ============================================================================
print()
print("=" * 78)
print("1. 풀 24줄 — R2 §3-4 마크다운 표 파싱")
print("=" * 78)

ROW = re.compile(r'^\|\s*(\d+)\s*\|\s*\*{0,2}([^|*]+?)\*{0,2}\s*\|\s*(Idle|Walk)\s*\|\s*`([^`]+)`\s*\|'
                 r'\s*(\d+)\s*\|\s*\+?(-?[\d.]+)\s*\|\s*`([^`]+)`\s*\|\s*(\d+)\s*\|', re.M)
pool = []
for m in ROW.finditer(read(R2)):
    idx, gate, state, ko, ko_len, slack, en, en_len = m.groups()
    pool.append(dict(n=int(idx), gate=gate.strip(), state=state, ko=ko,
                     ko_len=int(ko_len), slack=float(slack), en=en, en_len=int(en_len)))

check("표에서 24행을 읽었다", len(pool) == 24, "읽은 행 %d" % len(pool))
check("번호가 1..24로 빠짐없다", [p["n"] for p in pool] == list(range(1, 25)))
# 양성 대조 — 표에 실재하는 줄과 실재하지 않는 줄
kos = [p["ko"] for p in pool]
check("양성 대조 — 표에 «아침 산책이네»가 있다", "아침 산책이네" in kos)
check("음성 대조 — 표에 «오후»가 아직 없다(이번 라운드가 채우는 구멍)",
      not any("오후" in k or "저녁" in k for k in kos))
# 글자수 열이 실제 글자수와 맞는가 = 표 자체의 무결성
bad_len = [(p["ko"], p["ko_len"], len(p["ko"])) for p in pool if len(p["ko"]) != p["ko_len"]]
check("표의 «글자» 열이 실제 글자수와 일치", not bad_len, str(bad_len[:3]))

gates = {}
for p in pool:
    gates.setdefault(p["gate"], []).append(p)
print("  자격축별: " + ", ".join("%s=%d" % (g, len(v)) for g, v in sorted(gates.items())))
always = [p for p in pool if p["gate"] == "상시"]
check("상시(무조건) 줄이 Idle 5 + Walk 5 = 10줄",
      len(always) == 10
      and len([p for p in always if p["state"] == "Idle"]) == 5
      and len([p for p in always if p["state"] == "Walk"]) == 5)

# ★ 프로덕션과의 격차 — 이 풀은 아직 배선되지 않았다
# ★★ 2026-09-06 09:05 — 이 파일이 **측정 도중에 바뀌었다**(다른 라운드가 재생성).
#    그래서 골든의 실제 상태를 여기서 먼저 찍는다. 「내가 언제의 골든을 쟀는가」가 안 남으면
#    다음 사람이 다른 숫자를 보고 내 보고를 거짓으로 판정한다.
import time
print("  [골든 스냅샷] mtime=%s  행수=%d"
      % (time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(os.path.getmtime(GOLDEN))),
         len([l for l in read(GOLDEN).splitlines() if l.strip() and not l.startswith("#")])))

prod_golden = [l.split("\t")[2] for l in read(GOLDEN).splitlines()
               if l.strip() and not l.startswith("#")]
wired = [k for k in kos if k in prod_golden]
unwired = [k for k in kos if k not in prod_golden]
print("  ★ 프로덕션 배선 상태: 24줄 중 배선됨 %d / 미배선 %d" % (len(wired), len(unwired)))
print("     미배선: %s" % ", ".join(unwired))

# ★ 정정 1 — 「조건부 14줄이 전부 미배선」은 **틀렸다**(첫 판에서 내가 그렇게 단언했다가 빨간불).
#   모션 2줄은 **이미 프로덕션에 있다** — 다만 «상시»로 떠 있고 아직 조건화되지 않았다
#   (R2 표 비고가 그 둘만 «현행+조건화»라고 적어 둔 이유다).
motion = [p["ko"] for p in pool if p["gate"].startswith("모션")]
tod_dow = [p["ko"] for p in pool if p["gate"].startswith(("시간", "요일"))]
check("모션 2줄은 이미 프로덕션에 있다(조건화만 남았다)",
      all(k in prod_golden for k in motion), str(motion))
check("요일·시간대 12줄은 전부 미배선 — 내 4줄도 같은 대기열에 선다",
      all(k in unwired for k in tod_dow), "미배선 %d/12" % len([k for k in tod_dow if k in unwired]))

# ★ 정정 2 — 「심심하다가 프로덕션에 남아 있다」도 **틀렸다**. 09:05 재생성에서 삭제됐다.
check("R2 §3-2의 «심심하다» 삭제가 골든에 착지했다",
      "심심하다" not in prod_golden and "심심하다" not in kos)

# ★ 교차 확인 — 같은 재생성에 내 지난 라운드 산출물(댄스 진입 대사)이 예측대로 들어갔는가
check("댄스 진입 대사가 골든에 착지", "저절로 춤이 나와" in prod_golden)
_i = prod_golden.index("저절로 춤이 나와") if "저절로 춤이 나와" in prod_golden else -1
check("착지 위치가 예측대로 «잡혔다» 다음이다",
      _i > 0 and prod_golden[_i - 1] == "잡혔다",
      "앞=%s" % (prod_golden[_i - 1] if _i > 0 else "?"))
_row = [l for l in read(GOLDEN).splitlines() if l.endswith("\t저절로 춤이 나와")]
check("착지 비트가 예측대로 3F747AE2 / 0.955000",
      bool(_row) and _row[0].split("\t")[0] == "3F747AE2" and _row[0].split("\t")[1] == "0.955000",
      _row[0] if _row else "행 없음")

# ============================================================================
# 2. 이번 라운드 확정 — 오후·저녁 4줄
# ============================================================================
print()
print("=" * 78)
print("2. 확정 4줄")
print("=" * 78)

NEW = [
    dict(gate="시간:오후", state="Idle", ko="아직 오후네",     en="Still afternoon.",
         axis="구간 잔여 — «아직 저녁이 아니다»"),
    dict(gate="시간:오후", state="Walk", ko="오후가 지나가네",  en="Afternoon slips by.",
         axis="구간 경과 — «시간이 흐른다»"),
    dict(gate="시간:저녁", state="Idle", ko="저녁은 느긋하네",  en="Evenings are easy.",
         axis="구간 성격 — «일과 뒤의 여유»"),
    dict(gate="시간:저녁", state="Walk", ko="하루를 마무리하네", en="Wrapping up the day.",
         axis="하루 위치 — «하루가 끝 방향이다»"),
]

# ---- 2-a. 가독예산: 골든의 같은 글자수 행에서 승계 (ReadingSeconds를 흉내 내지 않는다)
gold = {}
for line in read(GOLDEN).splitlines():
    if line.strip() and not line.startswith("#"):
        bits, secs, text = line.split("\t")
        gold.setdefault(len(text), set()).add((bits, float(secs)))

FADE_IN = 0.06
FLOOR = {"Idle": 2.00, "Walk": 1.50}      # R1 §5-2 «하한» 열
for r in NEW:
    n = len(r["ko"])
    rows = gold.get(n)
    check("«%s» 골든에 같은 글자수(%d) 행이 유일" % (r["ko"], n), rows is not None and len(rows) == 1)
    if rows and len(rows) == 1:
        r["bits"], r["R"] = list(rows)[0]
        r["dwell"] = FADE_IN + r["R"]
        r["slack"] = FLOOR[r["state"]] - r["dwell"]

print()
print("  | 자격 | 상태 | 대사 | 글자 | 가독예산 | 필요체류 | 하한 | 여유 | 골든비트 |")
for r in NEW:
    print("  | %s | %s | %s | %d | %.3f | %.3f | %.2f | +%.3f | %s |"
          % (r["gate"], r["state"], r["ko"], len(r["ko"]), r["R"], r["dwell"],
             FLOOR[r["state"]], r["slack"], r["bits"]))
for r in NEW:
    check("«%s» 필요체류 %.3f <= 하한 %.2f" % (r["ko"], r["dwell"], FLOOR[r["state"]]),
          r["dwell"] <= FLOOR[r["state"]], "여유 +%.3f초" % r["slack"])
worst = min(r["slack"] for r in NEW)
r2_worst = min(p["slack"] for p in pool)
check("내 4줄의 최소 여유가 기존 24줄의 최소 여유(+%.3f)보다 나쁘지 않다" % r2_worst,
      worst >= r2_worst, "내 최소 여유 +%.3f초" % worst)

# ---- 2-b. 영어 예산 (R4 재창작 형식). 라틴 계수는 DialogueKind.cs 문서값.
PER_LATIN, BASE, MINS, MAXS = 0.0472, 0.28, 0.62, 2.20
# 교정: R2 표의 영어 여유 열을 재현할 수 있는가
def en_dwell(txt):
    return FADE_IN + min(max(BASE + len(txt) * PER_LATIN, MINS), MAXS)
cal_en = [(p, FLOOR[p["state"]] - en_dwell(p["en"])) for p in pool]
bad_en = [(p["en"], round(s, 3)) for p, s in cal_en
          if abs(s - float(re.sub(r"[^\d.\-]", "", "0"))) < -1]   # placeholder, 아래에서 실검
# 실검: R2 표의 영어 여유 열을 파싱해 대조
ROW_EN = re.compile(r'^\|\s*(\d+)\s*\|[^|]*\|\s*(Idle|Walk)\s*\|[^|]*\|[^|]*\|[^|]*\|'
                    r'\s*`([^`]+)`\s*\|\s*(\d+)\s*\|\s*\+?(-?[\d.]+)\s*\|', re.M)
en_pub = {int(m.group(1)): (m.group(3), float(m.group(5))) for m in ROW_EN.finditer(read(R2))}
mism = []
for p in pool:
    if p["n"] in en_pub:
        txt, pub = en_pub[p["n"]]
        got = FLOOR[p["state"]] - en_dwell(txt)
        if abs(got - pub) > 0.002:
            mism.append((txt, pub, round(got, 3)))
check("교정 — R2 표의 영어 여유 24행을 라틴 계수로 재현", not mism, str(mism[:3]))
for r in NEW:
    r["en_dwell"] = en_dwell(r["en"])
    r["en_slack"] = FLOOR[r["state"]] - r["en_dwell"]
print()
for r in NEW:
    print("  EN | %-22s | %2d자 | 필요체류 %.3f | 여유 +%.3f"
          % (r["en"], len(r["en"]), r["en_dwell"], r["en_slack"]))
for r in NEW:
    check("EN «%s» 여유 > 팝인 0.18초" % r["en"], r["en_slack"] > 0.18,
          "+%.3f초" % r["en_slack"])

# ============================================================================
# 3. 어휘 중복 — 5구간이 서로 구분되는가
# ============================================================================
print()
print("=" * 78)
print("3. 어휘 중복 (프로덕션 42 + 계획 24 + 내 4줄 상호)")
print("=" * 78)


def morphs(t):
    return set(re.findall(r"[가-힣]{2,}", t))


existing = set(prod_golden) | set(kos)
for r in NEW:
    m = morphs(r["ko"])
    hits = {t: sorted(m & morphs(t)) for t in existing if m & morphs(t)}
    check("«%s» 기존 %d줄과 2음절+ 낱말 중복 0건" % (r["ko"], len(existing)),
          not hits, str(hits) if hits else "")
# 내 4줄끼리도 겹치면 안 된다(구간 구분이 목적이다)
for i in range(len(NEW)):
    for j in range(i + 1, len(NEW)):
        a, b = NEW[i], NEW[j]
        ov = sorted(morphs(a["ko"]) & morphs(b["ko"]))
        # 같은 구간의 Idle/Walk는 구간 이름을 공유해도 된다(기존 «아침이네»/«아침 산책이네» 전례)
        allowed = {"오후"} if a["gate"] == b["gate"] else set()
        bad = [w for w in ov if w not in allowed]
        check("«%s» x «%s» 불허 중복 0건" % (a["ko"], b["ko"]), not bad, str(bad) if bad else "")

# 구간 대표 낱말이 5구간에서 서로 다른가
band_words = {"아침": "아침", "점심": "점심", "밤": "밤", "오후": "오후", "저녁": "저녁"}
check("5구간 대표 낱말이 서로 겹치지 않는다", len(set(band_words.values())) == 5)
# 주장 축이 5구간에서 서로 다른가(수동 표를 기계로 못 재므로 «축 문자열 유일»만 확인)
axes = [r["axis"] for r in NEW]
check("내 4줄의 «주장하는 사실»이 서로 다르다", len(set(axes)) == 4)

# ============================================================================
# 4. 금칙어 — 시계 밖 사실을 주장하지 않는가
# ============================================================================
print()
print("=" * 78)
print("4. 금칙어 — «시계에서만 파생» 검사")
print("=" * 78)

BANNED = {
    "빛/해(관측 불가)":   ["해가", "햇", "노을", "어둡", "밝", "저물", "동트", "일몰", "그늘"],
    "날씨/계절(관측 불가)": ["비가", "눈이 오", "덥", "춥", "쌀쌀", "포근", "바람", "봄", "여름", "가을", "겨울"],
    "소리(오디오 계약)":   ["소리", "노래", "음악", "조용하"],
    "요일(다른 자격축)":   ["월요일", "금요일", "주말", "평일", "쉬는 날"],
    "특정 사건/유저 주장": ["밥", "먹었", "퇴근", "출근", "학교", "회사", "숙제", "너는", "네가"],
    "미래형 예고":        ["갈게", "할게", "이제부터", "곧 "],
    "팀 내부 용어":       ["발판", "자격", "구간", "풀"],
}
for r in NEW:
    for g, ws in BANNED.items():
        hit = [w for w in ws if w in r["ko"]]
        check("«%s» 금칙 없음 — %s" % (r["ko"], g), not hit, str(hit) if hit else "")

POISON = "해가 저물고 쌀쌀한 바람 소리, 금요일 퇴근하고 곧 갈게 — 발판 자격"
dead = [g for g, ws in BANNED.items() if not any(w in POISON for w in ws)]
check("양성 대조 — 금칙 7개 그룹이 **각각** 고의 위반을 잡는다", not dead,
      ("죽은 그룹: %s" % dead) if dead else "그룹별 히트 확인됨")

# ============================================================================
# 5. 5구간 커버리지 + 최악 조합 N 재계산
# ============================================================================
print()
print("=" * 78)
print("5. 5구간 커버리지와 최악 조합")
print("=" * 78)

BANDS = [("아침", 5, 11), ("점심", 11, 14), ("오후", 14, 18), ("저녁", 18, 22), ("밤", 22, 5)]
covered = []
for name, a, b in BANDS:
    covered += list(range(a, b)) if a < b else list(range(a, 24)) + list(range(0, b))
check("24시간을 빈틈없이 덮는다", sorted(covered) == list(range(24)),
      "덮은 시각 %d개" % len(covered))
check("겹치는 시각이 없다", len(covered) == len(set(covered)))
print("  " + " / ".join("%s %02d-%02d(%dh)" % (n, a, b, (b - a) % 24) for n, a, b in BANDS)
      + "  합계 %dh" % sum((b - a) % 24 for _, a, b in BANDS))

FULL = pool + [dict(gate=r["gate"], state=r["state"], ko=r["ko"]) for r in NEW]
DOWS = [None, "요일:월", "요일:금", "요일:주말"]
TODS = ["시간:아침", "시간:점심", "시간:오후", "시간:저녁", "시간:밤"]
TODS_OLD = ["시간:아침", "시간:점심", "시간:밤", None]      # None = 14-22시 구멍
MOTS = [set(), {"모션:앉기하품"}, {"모션:두리번"}, {"모션:앉기하품", "모션:두리번"}]


def N_of(p, dow, tod, mots):
    return len([x for x in p if x["gate"] == "상시" or x["gate"] == dow
                or x["gate"] == tod or x["gate"] in mots])


def sweep(p, tods):
    out = []
    for d in DOWS:
        for t in tods:
            for m in MOTS:
                out.append(N_of(p, d, t, m))
    return min(out), max(out)


old_min, old_max = sweep(pool, TODS_OLD)
new_min, new_max = sweep(FULL, TODS)
print("  현행 3구간(14-22시 구멍 포함) : N 범위 %d~%d | 최악 중복 %.1f%% | 계약여유 %+.1fpp"
      % (old_min, old_max, repeat_prob(old_min, 4) * 100, (0.5 - repeat_prob(old_min, 4)) * 100))
print("  확정 5구간 + 오후·저녁 4줄     : N 범위 %d~%d | 최악 중복 %.1f%% | 계약여유 %+.1fpp"
      % (new_min, new_max, repeat_prob(new_min, 4) * 100, (0.5 - repeat_prob(new_min, 4)) * 100))
check("현행의 최악 N이 R23 발표값 10과 같다", old_min == 10, "N=%d" % old_min)
check("확정안의 최악 N이 R23 발표값 12와 같다", new_min == 12, "N=%d" % new_min)
check("확정안 최악 중복 42.7% 재현", abs(repeat_prob(new_min, 4) - 0.427) < 0.001)
check("계약여유가 +0.4pp -> +7.3pp", abs((0.5 - repeat_prob(new_min, 4)) * 100 - 7.3) < 0.1)
check("허용 k가 4로 유지된다(후퇴 없음)", max_k(new_min) == 4)
check("★ 모든 시간대에서 «자격 2»(Idle 1 + Walk 1)가 성립한다",
      all(len([x for x in FULL if x["gate"] == t]) == 2 for t in TODS),
      str({t: len([x for x in FULL if x["gate"] == t]) for t in TODS}))
# 한 줄만 빠져도 벼랑인가 — R23 §2 재현
check("N=11이어도 허용 k=4 유지(한 줄 여유가 남는다)", max_k(11) == 4)
check("N=9에서는 허용 k=3으로 후퇴(벼랑의 위치 확인)", max_k(9) == 3)

# 구간 길이별 1회 이상 노출 (R23 §3 재현, 평균간격 345초 · 최악 N=12)
print()
INTERVAL = 345.0
for name, a, b in BANDS:
    hours = (b - a) % 24
    fires = hours * 3600.0 / INTERVAL
    exp = fires / new_min
    p1 = 1 - (1 - 1.0 / new_min) ** fires
    print("  %-3s %2dh  발화 %5.1f회  기대노출 %.2f회  1회이상 %.1f%%" % (name, hours, fires, exp, p1 * 100))
check("신규 2구간(각 4h)의 «1회 이상» 노출이 97% 이상",
      all(1 - (1 - 1.0 / new_min) ** (4 * 3600.0 / INTERVAL) > 0.97 for _ in (0,)))

print()
print("=" * 78)
if FAIL:
    print("실패 %d건: %s" % (len(FAIL), FAIL))
    sys.exit(1)
print("전 항목 통과.")
for r in NEW:
    print("  %-9s %-4s «%s» (%d자, %.3f초, %s)  축: %s"
          % (r["gate"], r["state"], r["ko"], len(r["ko"]), r["R"], r["bits"], r["axis"]))
