#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
NUnit 결과 xml 판정기 — `docs/verify/regress.sh`(report · compare)와 `docs/verify/baseline.py`가 함께 쓴다.
(qa-regression, 2026-09-14 신설 — docs/TEAM.md 「픽스처 끝에서 난 실패는 실패 개수에 안 들어간다」 규칙 1·2·5)

  python3 docs/verify/nunit_verdict.py <결과.xml> [...]
      파일마다 판정 한 덩어리를 찍는다.  rc 0 = 전부 초록 / 3 = 빨강이 하나라도 있다 / 2 = 읽지 못한 파일이 있다

왜 생겼나 — 2026-09-14 coder 실측(`Logs/coder-onbstore/mut-M5p.xml`):
  [SetUpFixture]의 [OneTimeTearDown] 단언이 실패했는데 **xml failed="0", Unity `Exiting with code 0 (Ok)`,
  `regress.sh report` rc=0·실패 목록 없음**이었다. 흔적은 test-run `result="Failed(Child)"`와
  그 스위트의 `result="Failed" site="TearDown"`뿐이었다. NUnit은 **테스트 케이스만 센다.**

규칙 1 — 초록 = failed==0
               AND test-run result ∈ {Passed, Skipped:Ignored}
               AND site가 SetUp/TearDown인 test-suite 0개
               AND result가 Failed로 시작하는 test-suite 0개
               AND test-run failed 속성 == result="Failed"인 test-case 수   ← ★ 다섯째 절(아래)
         종료코드나 failed= 하나로 판정하지 않는다.
         ★ 다섯째 절은 docs/TEAM.md 규칙 1의 네 절보다 **엄격한 쪽**이다(2026-09-14 문서화, 코드는 처음부터 있었다).
           둘이 어긋나면 xml이 잘렸거나 손으로 만든 것이라 네 절이 다 맞아도 초록이라고 말하지 않는다.
           정규식 교차 판정기(regress.sh selfcheck)도 같은 절을 건다.
규칙 2 — **label로 거르지 않는다.** label은 출력만 한다.
         (합성 교정본에서 TearDown 실패가 label="Ignored"로 나왔다 — label="Error"만 찾으면 놓친다.)
규칙 5 — Inconclusive(Assume 실패)는 failed=에 안 들어간다 → **이름을 반드시 돌려준다.**
         초록/빨강 판정에는 섞지 않는다(규칙 1이 정본이다). 대신 부르는 쪽이 이름을 반드시 찍는다.

★ 이 모듈을 regress.sh와 baseline.py가 공유한다 = 둘이 **같이 틀릴 수 있다**
  (TEAM.md 「생성기와 검사기가 같이 틀린다」). 그래서 `regress.sh selfcheck`가
  **이 모듈을 import하지 않는 원시 정규식 판정기**로 같은 xml을 다시 재서 일치를 요구하고,
  실제 러너 출력(mut-M5p.xml = 빨강, edit-full.xml = 초록)으로 교정한다.
"""
import sys
import xml.etree.ElementTree as ET

GREEN_RUN_RESULTS = ("Passed", "Skipped:Ignored")
HARD_SITES = ("SetUp", "TearDown")


def _message(el):
    """failure/message 또는 reason/message의 첫 240자(공백 정리). 없으면 빈 문자열."""
    if el is None:
        return ""
    for tag in ("failure", "reason"):
        m = el.find(f"./{tag}/message")
        if m is not None and (m.text or "").strip():
            return " ".join(m.text.split())[:240]
    return ""


def judge_root(root):
    """이미 파싱한 뿌리로 판정한다. 돌려주는 dict:
       green(bool) · reasons[str] · run_result · failed · failed_cases[(이름, 메시지)] ·
       inconclusive_cases[(이름, 메시지)] · fixture_failures[dict] · failed_suite_count"""
    v = dict(green=False, reasons=[], run_result=None, failed=0,
             failed_cases=[], inconclusive_cases=[], fixture_failures=[],
             failed_suite_count=0, unreadable=False)
    if root is None or root.tag != "test-run":
        v["reasons"].append(f"뿌리 요소가 test-run이 아니다({getattr(root, 'tag', None)}) — NUnit 결과가 아니다")
        return v

    res = root.get("result")
    v["run_result"] = res
    try:
        failed = int(root.get("failed") or 0)
    except ValueError:
        failed = -1
    v["failed"] = failed

    for tc in root.iter("test-case"):
        r = tc.get("result")
        name = tc.get("fullname") or tc.get("name") or "?"
        if r == "Failed":
            v["failed_cases"].append((name, _message(tc)))
        elif r == "Inconclusive":
            v["inconclusive_cases"].append((name, _message(tc)))

    for s in root.iter("test-suite"):
        r = s.get("result") or ""
        if r.startswith("Failed"):
            v["failed_suite_count"] += 1
        site = s.get("site")
        if site in HARD_SITES:                      # ★ 규칙 2: label은 조건에 쓰지 않는다
            v["fixture_failures"].append(dict(
                type=s.get("type"), fullname=s.get("fullname") or s.get("name") or "?",
                result=r, site=site, label=s.get("label"), message=_message(s)))

    reasons = v["reasons"]
    if failed != 0:
        reasons.append(f"failed={failed} — 테스트 케이스 실패")
    if len(v["failed_cases"]) != max(failed, 0):
        reasons.append(f"failed 속성({failed})과 result=Failed 테스트 케이스 수({len(v['failed_cases'])})가 다르다 — "
                       "xml이 잘렸거나 손으로 만든 것이다")
    if res is None:
        reasons.append("test-run에 result 속성이 없다 — 규칙 1을 판정할 수 없다(초록이 아니다)")
    elif res not in GREEN_RUN_RESULTS:
        reasons.append(f"test-run result={res} (초록 허용값: {', '.join(GREEN_RUN_RESULTS)})")
    for f in v["fixture_failures"]:
        reasons.append(f"★ 픽스처 수준 실패 — {f['type']} {f['fullname']} site={f['site']} result={f['result']} "
                       f"label={f['label']} (failed=에 안 잡힌다)"
                       + (f" : {f['message']}" if f["message"] else ""))
    if v["failed_suite_count"]:
        reasons.append(f"result가 Failed로 시작하는 test-suite {v['failed_suite_count']}개")
    v["green"] = not reasons
    return v


def judge(path):
    """파일 경로로 판정한다. 읽지 못하면 unreadable=True + 초록 아님."""
    try:
        root = ET.parse(path).getroot()
    except Exception as e:                       # 없는 파일 · 잘린 xml · 빈 파일
        v = judge_root(None)
        v["reasons"] = [f"결과 xml을 읽지 못했다 — {e}"]
        v["unreadable"] = True
        return v
    return judge_root(root)


def format_lines(v, name_fn=lambda n: n):
    """사람이 읽는 판정 덩어리. regress.sh와 CLI가 같은 모양을 찍게 한다."""
    L = []
    if v["fixture_failures"]:
        L.append(f"── ★ 픽스처 수준 실패 {len(v['fixture_failures'])}건 — failed=에 안 잡힌다 ──")
        for f in v["fixture_failures"]:
            L.append(f"   ✗ {f['type']} {f['fullname']}  site={f['site']} result={f['result']} label={f['label']}")
            if f["message"]:
                L.append(f"       {f['message']}")
    if v["inconclusive_cases"]:
        L.append(f"── ⚠ 판정 불가(Inconclusive) {len(v['inconclusive_cases'])}건 — failed=에 안 잡힌다. 이름을 보고에 적어라 ──")
        for n, m in v["inconclusive_cases"]:
            L.append(f"   ? {name_fn(n)}")
            if m:
                L.append(f"       {m}")
    if v["green"]:
        L.append("✓ R1 초록 — failed=0 · test-run result="
                 f"{v['run_result']} · SetUp/TearDown 스위트 0 · Failed 스위트 0")
    else:
        L.append("✗ R1 빨강 — 이 결과는 초록이 아니다(docs/TEAM.md 「픽스처 끝에서 난 실패는…」 규칙 1):")
        for x in v["reasons"]:
            L.append(f"   · {x}")
    return L


# =============================================================================
# ★ 2026-09-14 — 실행 범위(전량/부분) 판정 — baseline.py 「현재」와 regress.sh report 배너가 **이 한 곳**을 쓴다
# =============================================================================
# 전량/부분은 **기록된 사실로만** 가른다(리더 지시). testcasecount 크기로 추정하지 않는다 — 테스트 수는 계속 는다.
#   (1) Unity 로그의 `COMMAND LINE ARGUMENTS:` 블록 — ★ **한 줄에 인자 하나**다.
#       (2026-09-14 실측: 한 줄로 파싱한 프로브가 848건 전부 «테스트 실행 아님»을 냈다 — 죽은 프로브.)
#       블록의 `-testResults`가 **이 xml 경로**일 때만 그 로그를 이 실행의 기록으로 인정한다.
#       필터 인자(SCOPE_FILTER_ARGS)가 하나라도 있으면 부분, 없으면 전량.
#   (2) 로그가 없을 때만 — regress.sh `.meta` 사이드카(키 집합이 regress.sh의 것이고 라벨·모드가 맞을 때).
#       regress.sh는 필터 인자를 넘기지 않는다(git 이력 6커밋 확인, 2026-09-14).
#   (3) 둘 다 없으면 「미확인」.
#   ★ 모순 검사는 **같은 xml 안의 기록끼리**만: 필터가 없다는데 뿌리 스위트 발견 수 > 실행 수면 「미확인(모순)」.
import os
import re

SCOPE_FILTER_ARGS = ("-testFilter", "-testCategory", "-assemblyNames", "-testSettingsFile",
                     "-orderedTestListFile", "-editorTestsFilter", "-editorTestsCategories", "-testNames")
REGRESS_META_KEYS = {"label", "mode", "head", "dirty", "target", "target_before",
                     "target_shifted", "started", "finished", "unity_rc"}


def unity_cmdline(log_path):
    """Unity 로그의 `COMMAND LINE ARGUMENTS:` 아래 인자 목록. 로그가 없거나 블록이 없으면 None."""
    if not log_path or not os.path.isfile(log_path):
        return None
    try:
        with open(log_path, "rb") as f:
            lines = f.read(65536).decode("utf-8", "replace").split("\n")
    except OSError:
        return None
    try:
        i = next(k for k, l in enumerate(lines) if l.strip() == "COMMAND LINE ARGUMENTS:")
    except StopIteration:
        return None
    args = []
    for l in lines[i + 1:i + 120]:
        t = l.rstrip("\r")
        if not t or re.search(r"\s", t):     # 빈 줄이나 공백이 든 줄(다음 로그 문장)에서 끝난다
            break
        args.append(t)
    return args


def arg_after(cmd, flag):
    if flag in cmd:
        i = cmd.index(flag)
        return cmd[i + 1] if i + 1 < len(cmd) else ""
    return None


def xml_platform(root):
    for p in root.iter("property"):
        if p.get("name") == "platform":
            return p.get("value")
    return None


def read_meta_file(path):
    d = {}
    if path and os.path.isfile(path):
        for line in open(path, encoding="utf-8", errors="replace"):
            if "=" in line:
                k, v = line.rstrip("\n").split("=", 1)
                d[k.strip()] = v.strip()
    return d


def run_scope(xml_path, root, meta=None, log_path=None, label=None, mode=None):
    """(범위, 근거). 범위 ∈ {"전량", "부분", "미확인"}. log_path를 안 주면 xml 옆 같은 이름의 .log를 쓴다."""
    if log_path is None:
        log_path = os.path.splitext(xml_path)[0] + ".log"
    suite = root.find("test-suite")
    try:
        discovered = int(suite.get("testcasecount") or 0) if suite is not None else 0
        total = int(root.get("total") or 0)
    except ValueError:
        discovered = total = 0
    cmd = unity_cmdline(log_path)
    if cmd is not None:
        if not cmd or not cmd[0].endswith("/Unity") or "-runTests" not in cmd:
            return "미확인", "로그 명령줄이 테스트 실행(-runTests)이 아니다"
        tr = arg_after(cmd, "-testResults")
        # realpath — 심볼릭 링크로 가리킨 같은 파일은 같은 실행이다(교정용 링크 폴더에서 짝이 끊기지 않게).
        if not tr or os.path.realpath(tr) != os.path.realpath(xml_path):
            return "미확인", f"로그의 -testResults({tr or '없음'})가 이 xml이 아니다 — 이 실행의 로그라고 확인할 수 없다"
        plat, xplat = arg_after(cmd, "-testPlatform"), xml_platform(root)
        if plat and xplat and plat.lower() != xplat.lower():
            return "미확인", f"로그 -testPlatform {plat} ≠ xml platform {xplat}"
        flt = [a for a in cmd if a in SCOPE_FILTER_ARGS]
        if flt:
            return "부분", f"로그 명령줄 {flt[0]} {arg_after(cmd, flt[0])}"
        if discovered > total:
            return "미확인", f"모순 — 로그에 필터 인자가 없는데 xml은 발견 {discovered}건 중 {total}건만 실행"
        why = "로그 명령줄에 필터 인자 없음"
        if meta and REGRESS_META_KEYS <= set(meta):
            why += " + regress.sh 사이드카"
        return "전량", why
    if meta:
        if not (REGRESS_META_KEYS <= set(meta)):
            return "미확인", "사이드카가 regress.sh 형식이 아니다(키 부족) — 로그도 없다"
        if (label is not None and meta.get("label") != label) or (mode is not None and meta.get("mode") != mode):
            return "미확인", f"사이드카의 라벨·모드({meta.get('label')}/{meta.get('mode')})가 이 xml과 다르다 — 로그도 없다"
        # ★ 2026-09-14 — regress.sh는 이제 사이드카에 **실제로 넘긴 인자**(args=)를 적는다. 있으면 그것이 기록이다.
        margs = (meta.get("args") or "").split()
        if margs:
            if "-runTests" not in margs:
                return "미확인", "regress.sh 사이드카 args에 -runTests가 없다"
            flt = [a for a in margs if a in SCOPE_FILTER_ARGS]
            if flt:
                return "부분", f"regress.sh 사이드카 args {flt[0]} {arg_after(margs, flt[0])}(로그 없음)"
            why = "regress.sh 사이드카 args에 필터 인자 없음(로그 없음)"
        else:
            why = "regress.sh 사이드카(로그 없음 · args 기록 전 판 — 그 판의 regress.sh는 필터 인자를 넘기지 않았다)"
        if discovered > total:
            return "미확인", f"모순 — {why}인데 xml은 발견 {discovered}건 중 {total}건만 실행"
        return "전량", why
    return "미확인", "로그 명령줄도 regress.sh 사이드카도 없다"


# =============================================================================
# ★ 2026-09-14 — 소스 리프 테스트 **하한** (regress.sh report 「부분 실행」 배너의 둘째 자)
# =============================================================================
# 명세: docs/verify/PLAYMODE_RED7_FIX_SPEC.md §8 1안(test-engineer), 리더 채택. C# 파서가 아니라 **텍스트 휴리스틱**이다.
# 규칙(명시):
#   대상  : <Tests>/EditMode/**/*.cs (xml platform=EditMode) · <Tests>/PlayMode/**/*.cs (PlayMode)
#   전처리: /* */ · // 주석을 걷고, 문자열·문자 리터럴 안의 글자를 가린다(그 안의 `[` `]` `//`를 세지 않는다)
#   속성  : 선언 바로 앞의 **속성 줄**(줄 머리가 `[`이고 괄호 균형이 맞는 곳까지 — 여러 줄 가능) + 선언 줄 앞머리의 `[..]`.
#           ★ `["키"] = 값` 같은 **인덱서 초기화 줄은 속성이 아니다**(머리 `[..]` 뒤에 선언이 안 온다) —
#           2026-09-14 교정에서 CommentReferenceAuditTests의 사전 초기화 줄이 뒤 속성에 들러붙어 8건이 사라진 것을 잡았다.
#   메서드 1개의 리프:
#     [TestCaseSource] 있음                               → 전개 미확인(정적으로 못 센다) · 하한에는 1
#     [TestCase(...)] k개                                  → k
#     매개변수에 [Values]/[Range]/[ValueSource]/[Random]   → 전개 미확인 · 하한에는 1
#     [Test] 또는 [UnityTest]만                            → 1
#   [Ignore]/[Explicit] 테스트도 xml total에 들어가므로 그대로 센다.
#   전개하지 않는 것(2026-09-14 실측 0건이라): [TestFixture(인자)] 매개변수 픽스처 · 추상 기반 클래스 상속 · #if 블록.
#   ★ 이것들이 생기면 이 하한이 틀린다 — selfcheck가 실제 전량 xml로 «배너 없음»을 매번 교정한다.
#   ★ 이 수는 **지금 트리**의 것이다. 옛 xml에 대면 그 뒤 늘어난 테스트만큼 모자라 보인다(그래서 판정 rc에 안 섞는다).
import glob

_STR_LIT = re.compile(r'@"(?:[^"]|"")*"|\$?"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])*\'')
_TEST_ATTRS = {"Test", "UnityTest", "TestCase", "TestCaseSource"}
_DECL = re.compile(r'^(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|unsafe|new)\s+)*'
                   r'[\w<>\[\],\.?]+\s+(\w+)\s*\(')
_NOT_METHOD = {"if", "for", "foreach", "while", "switch", "return", "using", "lock", "catch", "nameof", "typeof"}


def _mask_source(text):
    """주석은 공백으로, 문자열·문자 리터럴의 안쪽은 `_`로 가린다. **줄 수와 열 위치를 보존**한다.
    ★ 2026-09-14 — 한 번에 훑는 상태 기계로 바꿨다. 옛 판(블록 주석 → 문자열 정규식 → 줄 주석 순서)은
      줄 주석 안의 `@"` 한 조각이 **여러 줄짜리 축자 문자열로 읽혀** 뒤의 테스트 8개를 삼켰다
      (실측: CommentReferenceAuditTests — 소스 [Test] 12개 중 4개만 셌다, 전량 xml 12건)."""
    out, i, n = [], 0, len(text)

    def blank(s, ch):
        return re.sub(r'[^\n]', ch, s)

    while i < n:
        c = text[i]
        nx = text[i + 1] if i + 1 < n else ''
        if c == '/' and nx == '/':                                   # 줄 주석
            j = text.find('\n', i)
            j = n if j < 0 else j
            out.append(' ' * (j - i))
            i = j
            continue
        if c == '/' and nx == '*':                                   # 블록 주석
            j = text.find('*/', i + 2)
            j = n if j < 0 else j + 2
            out.append(blank(text[i:j], ' '))
            i = j
            continue
        pre = 0
        if c == '@' and nx == '"':
            pre = 2
        elif (c == '$' and nx == '@') or (c == '@' and nx == '$'):
            pre = 3 if text[i + 2:i + 3] == '"' else 0
        if pre:                                                      # 축자 문자열 — ""가 이스케이프, 여러 줄 가능
            j = i + pre
            while j < n:
                if text[j] == '"':
                    if text[j + 1:j + 2] == '"':
                        j += 2
                        continue
                    break
                j += 1
            out.append(text[i:i + pre] + blank(text[i + pre:j], '_') + ('"' if j < n else ''))
            i = j + 1
            continue
        if c == '"' or (c == '$' and nx == '"'):                    # 일반 문자열 — 줄을 넘지 않는다
            k = i + (1 if c == '"' else 2)
            j = k
            while j < n and text[j] not in '"\n':
                j += 2 if (text[j] == '\\' and text[j + 1:j + 2] != '\n') else 1
            j = min(j, n)
            closed = j < n and text[j] == '"'
            out.append(text[i:k] + blank(text[k:j], '_') + ('"' if closed else ''))
            i = j + 1 if closed else j
            continue
        if c == "'":                                                 # 문자 리터럴
            j = i + 1
            while j < n and text[j] not in "'\n" and j - i < 10:
                j += 2 if text[j] == '\\' else 1
            if j < n and text[j] == "'":
                out.append("'" + '_' * (j - i - 1) + "'")
                i = j + 1
                continue
        out.append(c)
        i += 1
    return ''.join(out).split('\n')


def _split_leading_attrs(s):
    """줄 머리의 `[..][..]` 묶음과 나머지를 가른다. 머리가 `[`가 아니면 ("", s)."""
    i, n, groups_end = 0, len(s), 0
    while i < n and s[i] == '[':
        depth = 0
        j = i
        while j < n:
            if s[j] == '[':
                depth += 1
            elif s[j] == ']':
                depth -= 1
                if depth == 0:
                    break
            j += 1
        if j >= n:
            return None                       # 괄호가 이 줄에서 안 닫힌다
        groups_end = j + 1
        i = groups_end
        while i < n and s[i] in ' \t':
            i += 1
    return s[:groups_end], s[groups_end:].strip()


def leaves_in_source(text):
    """[(줄, 메서드, 리프 수, 미확인 사유 또는 None)]"""
    lines = _mask_source(text)
    out, attrs, i = [], "", 0
    while i < len(lines):
        s = lines[i].strip()
        if not s:
            i += 1
            continue
        start = i
        if s.startswith('['):
            buf, j = s, i
            while _split_leading_attrs(buf) is None and j + 1 < len(lines) and j - i < 15:
                j += 1
                buf += ' ' + lines[j].strip()
            sp = _split_leading_attrs(buf)
            if sp is None:
                attrs, i = "", j + 1
                continue
            head, rest = sp
            if not rest:                      # 순수 속성 줄 — 다음 선언에 붙는다
                attrs += ' ' + head
                i = j + 1
                continue
            attrs, s, i = attrs + ' ' + head, rest, j
        m = _DECL.match(s)
        if attrs and m and m.group(1) not in _NOT_METHOD:
            names = set(re.findall(r'(?:\[|,)\s*(?:NUnit\.Framework\.)?(\w+)', attrs))
            if names & _TEST_ATTRS:
                sig, k = s, i
                while ')' not in sig and k + 1 < len(lines) and k - i < 10:   # 여러 줄 서명
                    k += 1
                    sig += ' ' + lines[k].strip()
                params = sig[sig.index('(') + 1:]
                ntc = len(re.findall(r'(?:\[|,)\s*(?:NUnit\.Framework\.)?TestCase\s*\(', attrs))
                if "TestCaseSource" in names:
                    out.append((start + 1, m.group(1), 1, "TestCaseSource"))
                elif ntc:
                    out.append((start + 1, m.group(1), ntc, None))
                elif re.search(r'\[\s*(?:Values|Range|ValueSource|Random)\b', params):
                    out.append((start + 1, m.group(1), 1, "Values/Range/ValueSource/Random 매개변수"))
                else:
                    out.append((start + 1, m.group(1), 1, None))
        attrs = ""
        i += 1
    return out


def source_leaf_count(tests_root, platform):
    """xml platform(EditMode/PlayMode)에 맞는 폴더의 소스 리프 하한. 셀 수 없으면 None(0이라고 하지 않는다)."""
    folder = {"editmode": "EditMode", "playmode": "PlayMode"}.get((platform or "").lower())
    if not folder or not os.path.isdir(os.path.join(tests_root, folder)):
        return None
    known, unknown, files, methods = 0, [], 0, 0
    for p in sorted(glob.glob(os.path.join(tests_root, folder, "**", "*.cs"), recursive=True)):
        files += 1
        try:
            text = open(p, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        for ln, name, n, why in leaves_in_source(text):
            methods += 1
            if why:
                unknown.append((os.path.relpath(p, tests_root), ln, name, why))
            else:
                known += n
    if files == 0 or methods == 0:
        return None                            # 「0건」과 「못 셌다」를 같은 모양으로 내지 않는다
    return dict(known=known, unknown=unknown, lower_bound=known + len(unknown), files=files, methods=methods)


def main(argv):
    if not argv:
        print(__doc__)
        return 2
    rc = 0
    for p in argv:
        v = judge(p)
        print(f"== {p}")
        print(f"   test-run result={v['run_result']} failed={v['failed']} "
              f"실패케이스={len(v['failed_cases'])} 판정불가={len(v['inconclusive_cases'])} "
              f"픽스처실패={len(v['fixture_failures'])}")
        for ln in format_lines(v):
            print("   " + ln)
        if v["unreadable"]:
            rc = max(rc, 2)
        elif not v["green"]:
            rc = max(rc, 3)
    return rc


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
