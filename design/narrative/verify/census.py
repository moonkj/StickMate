# -*- coding: utf-8 -*-
"""
대사 전수 census + 가독예산 검산. design/narrative/2026-09-02_대사체계_실측과_계약.md 의 모든 표를 재생산한다.
문서와 이 출력이 어긋나면 **출력이 옳다**.

★ 사용법:  python3 design/narrative/verify/census.py          (저장소 루트에서)
★ 이 스크립트는 프로덕션 .cs 를 읽기만 한다. 아무것도 쓰지 않는다.
"""
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SCRIPTS = os.path.join(ROOT, "Assets", "_Project", "Scripts")

# ---------------------------------------------------------------------------
# 0. 가독예산 미러 — Dialogue/DialogueKind.cs 의 DialogueBudget / DialogueTiming
#    ★ 상수를 손으로 베끼지 않는다. 프로덕션 소스에서 읽어 온다.
# ---------------------------------------------------------------------------
def read_consts():
    src = open(os.path.join(SCRIPTS, "Dialogue", "DialogueKind.cs"), encoding="utf-8").read()
    def num(name):
        m = re.search(r"\b" + name + r"\s*=\s*([0-9.]+)f", src)
        if not m:
            sys.exit(f"★ 상수 {name} 를 DialogueKind.cs 에서 찾지 못했습니다 — 미러가 낡았습니다.")
        return float(m.group(1))
    return dict(BASE=num("BaseSeconds"), PER=num("PerGlyphSeconds"),
                MINS=num("MinSeconds"), MAXS=num("MaxSeconds"),
                FADE_IN=num("FadeInSeconds"), FADE_OUT=num("FadeOutSeconds"),
                POP_IN=num("PopInSeconds"), READS=num("ReadsBeforeStale"))

C = read_consts()
def reading(t):
    return min(max(C["BASE"] + len(t) * C["PER"], C["MINS"]), C["MAXS"])
def required(t):                 # DialogueBudget.RequiredDwellSeconds
    return C["FADE_IN"] + reading(t)
def maxvis(t, m=1.0):            # DialogueBudget.MaxVisibleSecondsFor
    return C["POP_IN"] + C["READS"] * m * reading(t) + C["FADE_OUT"]

# ---------------------------------------------------------------------------
# 0-1. ★ 양성 대조 — 프로덕션 주석이 직접 적어 둔 값으로 계산기를 교정한다.
#      교정이 깨지면 이 아래 숫자는 전부 폐기한다(TEAM.md 공통 처방).
# ---------------------------------------------------------------------------
CALIB = [("가뿐하네", 0.680), ("영차...", 0.715), ("헉... 높다", 0.865)]
print(f"[상수] 읽어온 값 {C}")
for t, exp in CALIB:
    got = round(required(t), 3)
    mark = "OK" if abs(got - exp) < 1e-9 else "★불일치"
    print(f"[교정] {t!r:<14} 필요체류 {got:.3f} / ParkourClimbState.cs 주석 {exp:.3f}  {mark}")
    if mark != "OK":
        sys.exit("★ 교정 실패 — 이 아래 숫자를 전부 폐기하십시오.")

# ---------------------------------------------------------------------------
# 1. 실재 대사 전수 — 코드에서 센다(문서를 믿지 않는다)
# ---------------------------------------------------------------------------
def ambient(field):
    src = open(os.path.join(SCRIPTS, "Dialogue", "AmbientChatter.cs"), encoding="utf-8").read()
    m = re.search(r"string\[\]\s+" + field + r"\s*=\s*\{(.*?)\n        \};", src, re.S)
    if not m:
        sys.exit(f"★ AmbientChatter.{field} 표를 찾지 못했습니다 — 스캐너가 낡았습니다.")
    body = re.sub(r"//[^\n]*", "", m.group(1))          # 주석 제거(주석 안에 옛 문장이 있다)
    return re.findall(r'"([^"]+)"', body)

LIT = re.compile(r'DialogueLine\.(?:Say|React)\(\s*"([^"]+)"')
def scan(rel):
    out = []
    d = os.path.join(SCRIPTS, rel)
    for root, _, fs in os.walk(d):
        for f in sorted(fs):
            if not f.endswith(".cs"):
                continue
            txt = open(os.path.join(root, f), encoding="utf-8").read()
            for mm in LIT.finditer(txt):
                out.append((f, mm.group(1)))
    return out

# TimedSpectacleState 주입 대사(Core/StickmanAgent.cs) + RunawayState 자진복귀 대사(변수 경유)
INJECTED = re.compile(r'cfg\s*=>\s*"([^"]+)"')
def injected():
    src = open(os.path.join(SCRIPTS, "Core", "StickmanAgent.cs"), encoding="utf-8").read()
    return [("StickmanAgent.cs", t) for t in INJECTED.findall(src)]
SELFRET = re.compile(r'TriggerSelfReturn\(\s*"([^"]+)"')
def selfreturn():
    src = open(os.path.join(SCRIPTS, "States", "RunawayState.cs"), encoding="utf-8").read()
    return [("RunawayState.cs", t) for t in dict.fromkeys(SELFRET.findall(src))]

idle, walk = ambient("IdleLines"), ambient("WalkLines")
states = scan("States")
inj, selfr = injected(), selfreturn()

print(f"\n=== 1. 실재 대사 전수(코드 기준) ===")
print(f"  AmbientChatter.IdleLines            {len(idle):>2}줄")
print(f"  AmbientChatter.WalkLines            {len(walk):>2}줄")
print(f"  States/*.cs  DialogueLine 리터럴     {len(states):>2}줄")
print(f"  States/RunawayState 자진복귀(변수)   {len(selfr):>2}줄  ← 소스 스캐너 사각지대")
print(f"  Core/StickmanAgent 주입(TimedSpectacle) {len(inj):>2}줄  ← 소스 스캐너 사각지대")
print(f"  Interaction/TodoListModel 동적(유저 할일)  0줄(작성 대사 아님)")
print(f"  ------------------------------------------")
print(f"  합계 작성 대사                       {len(idle)+len(walk)+len(states)+len(selfr)+len(inj):>2}줄")

# 발화 지점(생성 호출부)
CREATE = re.compile(r"new DialogueIntent\(|DialogueIntent\.TryCreate\(")
sites = 0
for root, _, fs in os.walk(SCRIPTS):
    if os.sep + "Tests" + os.sep in root + os.sep:
        continue
    for f in fs:
        if f.endswith(".cs") and f != "DialogueIntent.cs":
            # ★ 주석 줄은 세지 않는다 — GetupState.cs 의 TODO 주석이 "생성부"로 잡혀 12곳이 됐다.
            for line in open(os.path.join(root, f), encoding="utf-8"):
                if line.lstrip().startswith("//"):
                    continue
                sites += len(CREATE.findall(line))
print(f"  대사 생성 호출부(프로덕션)            {sites:>2}곳")

# ---------------------------------------------------------------------------
# 2. 게이트 검산 — 계획 잔여 체류(에셋 실값)
# ---------------------------------------------------------------------------
def asset(key, default=None):
    p = os.path.join(ROOT, "Assets", "_Project", "Data", "DefaultStickConfig.asset")
    m = re.search(r"^\s*" + key + r":\s*([0-9.]+)\s*$", open(p, encoding="utf-8").read(), re.M)
    if m: return float(m.group(1))
    if default is not None: return default
    sys.exit(f"★ 에셋에서 {key} 를 찾지 못했습니다.")

DWELL = {
    "Idle":         asset("wanderIdleDurationMin"),
    "Walk":         asset("wanderWalkDurationMin"),
    "ParkourClimb": asset("parkourClimbDuration"),
    "LedgeHang":    asset("ledgeHangGrabDuration") + asset("ledgeHangHoldDurationMin"),
}
POOL = {"Idle": idle, "Walk": walk}
NARRATIVE_STATES = {"ParkourClimbState.cs": "ParkourClimb", "LedgeHangState.cs": "LedgeHang"}

print(f"\n=== 2. 발화 자격 게이트(규칙 8) 검산 — 서술 대사만 ===")
print(f"  계획 잔여 하한: " + " / ".join(f"{k} {v:.2f}초" for k, v in DWELL.items()))
worst = None
rows = []
for st, lines in POOL.items():
    for t in lines: rows.append((st, t, DWELL[st]))
for f, t in states:
    if f in NARRATIVE_STATES:
        st = NARRATIVE_STATES[f]; rows.append((st, t, DWELL[st]))
fails = 0
for st, t, d in rows:
    req = required(t); slack = d - req
    if slack < 0: fails += 1
    if worst is None or slack < worst[0]: worst = (slack, st, t, req, d)
    print(f"  {st:<13}{t:<16}{len(t):>2}자 필요{req:.3f} 잔여하한{d:.2f} 여유{slack:+.3f} "
          f"{'★침묵' if slack < 0 else 'OK'}")
print(f"  서술 {len(rows)}줄 중 침묵 {fails}건. **최소 여유 {worst[0]:+.3f}초** "
      f"({worst[1]} \"{worst[2]}\" 필요 {worst[3]:.3f} vs 잔여하한 {worst[4]:.2f})")

# ---------------------------------------------------------------------------
# 3. 소스 스캐너 사각지대 — DialogueVisibleScaleContractTests 가 훑는 범위
# ---------------------------------------------------------------------------
seen = len(idle) + len(walk) + len(states)
blind = len(selfr) + len(inj)
print(f"\n=== 3. 감사 사각지대 ===")
print(f"  Tests/EditMode/DialogueVisibleScaleContractTests 가 훑는 대사  {seen}줄")
print(f"  훑지 못하는 대사                                            {blind}줄  ({blind*100.0/(seen+blind):.0f}%)")
for f, t in selfr + inj:
    print(f"    · {f:<20}\"{t}\"")
