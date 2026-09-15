#!/usr/bin/env python3
# =============================================================================
# 문서가 약속한 테스트 실존 스캐너 — qa-regression (2026-09-15 신설)
#
#   python3 docs/verify/promised_tests_scan.py                # 교정 → 전수 판정 → 요약·목록 인쇄
#   python3 docs/verify/promised_tests_scan.py --selftest     # 교정만
#   python3 docs/verify/promised_tests_scan.py --markdown     # 결과 문서용 절(교정·사전·목록·스냅숏 블록) 인쇄
#   python3 docs/verify/promised_tests_scan.py --check docs/verify/PROMISED_TEST_EXISTENCE_SCAN.md
#                                                             # 결과 문서 스냅숏 신선도 가드
#   python3 docs/verify/promised_tests_scan.py --json <경로>  # 전 기록 덤프(분석용 — 저장소 밖에 둘 것)
#
# 종료코드: 0 = 교정 통과 · 미표기 부재 0 / 3 = 교정 통과 · 미표기 (나)(다) ≥1
#           2 = 판정 불가(교정 실패 · 입력 대조 불일치 · 스냅숏 낡음 · 개인정보 누출 가드) — 숫자 인용 금지
#
# 왜 생겼나: security T-11 #1 `WallClockReadScopeAuditTests`가 문서에 「있는 감사」로 적혀 있었는데
#   한 번도 존재한 적이 없었다(`git log -S` 2커밋 전부 문서, `.cs` 추가 0). 09-06에 「기대치 낡음」까지 적혔는데
#   아무도 부재를 몰랐다. ⇒ 문서가 이름으로 약속한 테스트가 저장소에 **선언으로** 실재하는지 전수 대조한다.
#
# 판정의 자 (문서 자신이 아니다):
#   실체 = `Assets/**/Tests/**/*.cs`에서 **주석·문자열·문자 리터럴을 걷어낸** 코드의 형식·메서드·멤버 **선언**.
#          주석(`<c>이름</c>`)이나 로그 문자열에만 이름이 있으면 실체가 아니다
#          (실측: 삭제된 `CornerHoverPanelTests`가 HEAD 테스트 주석에 남아 있다 — 교정 C3가 이것을 쓴다).
#   이력 = HEAD 1부모 사슬의 모든 테스트 `.cs` blob을 같은 방식으로 해석해 선언의 등장·소멸 커밋을 구한다
#          (+ `--all` 곁가지 blob). `git log -S`(`.cs` 도입 커밋 수)는 **근거로 병기**한다 — 부분 문자열·주석도 세므로 자로 쓰지 않는다.
#   보조 = `docs/verify/renames.tsv`(개명 대장) — 커밋되지 않고 러너 xml에만 있던 중간 이름을 (다)로 되살린다.
#
# 분류(이름 단위 → 출현 단위):
#   (가) 실재 — 작업 트리 선언 존재. HEAD에 없고 작업 트리에만 있으면 「작업 트리 실재 · 미커밋」
#   (나) 한 번도 존재한 적 없음 — 어느 커밋의 테스트 blob에도 선언 없음(개명 대장에도 없음)
#   (다) 있었다가 사라짐 — 도입 커밋 · 소멸 커밋(+ 소멸 커밋에서 같은 클래스에 새로 생긴 선언 = 개명 후보)
#   (라) 문서가 스스로 부재를 표기 — (나)(다)인데 표기 사전 항목이 범위 안에 있음. 범위 4종:
#        단위(같은 문단·표 행·목록 항목, 이름에서 MARKER_FAR자 이내) / 표 머리 행 / 절(직전 제목~그 줄, 80줄 한도) / 문서 머리(첫 20줄)
#        — 표 머리·절·문서 머리는 좁은 사전(WIDE_SCOPE_MARKERS)만 쓴다
#   (마) 판정 불가 — 런타임 조립 이름(`TestName =`·`SetName(`·`GetType().Name + "…"`), 잘린 이름 불일치,
#        테스트 파일 주석에만 있는 사례 ID, 같은 ID 접두의 다른 문구 선언
#        ★ 「소스 텍스트에 없다 = 없다」로 단정하지 않는다(CLAUDE.md — 런타임 조립 이름을 구조적으로 못 본다)
#   제외 — 테스트 약속이 아닌 식별자(프로덕션 선언·문자열, `…ForTests` 훅, 약한 영문 밑줄 토큰). 개수는 늘 인쇄한다
#
# ★ 죽은 프로브 방지 — 교정(하나라도 실패하면 rc=2, 판정 숫자 인쇄 안 함). 목록은 calibrate() 참고:
#   C1 실재 표본 → (가) / C2 T-11-a → (나) + 1f7e139판 표기 없는 행 (나) · HEAD판 「명세만」 행 (라)
#   C3 삭제 테스트(`git log --diff-filter=D`) → (다) + 주석 잔존 양성 / C4 합성 음성(ZzNoSuchPromiseTests 등)
#   C5 해석기 불변식 / C6 입력 대조(glob = git ls-files) / C7 사전 비공허 / C8 스냅숏 변이 3종 / C9 누출 가드
#   C10 `.git/index` 무쓰기(실행 전후 mtime·크기·내용 동일) / C4 숨김 탐침 4종(이름 속 표기어 · 300자 안 무관한 표기 · 8자리 날짜 커밋 앵커 ·
#   절 제목 기능 낱말) + 같은 형태 실데이터 3건(`CALIB_COMMIT` 커밋판 문서)
#
# 스냅숏: 결과 문서에 `(분류, 이름, 파일, 문자열 앵커, 파일 내 앵커 총수, 그 분류 출현 수)` 행을 박는다.
#   줄 번호는 키가 아니다(정보용). `--check`는 원천을 지금 다시 계산해 한 행이라도 다르면 rc=2.
#
# 공개 저장소 규칙: 출력은 저장소 상대경로만. 사용자명은 실행 시점 `id -un`으로만 얻어 **가림·출력 검사에만** 쓴다.
# 쓰기: 저장소 파일을 쓰지 않는다. ★ 2026-09-15 정정 — 첫 판은 「파일을 쓰지 않는다」고 적었지만 plain `git status`가 `.git/index`를
#   고쳐 썼다(verify-change 적발). 지금은 모든 git 호출이 `git --no-optional-locks`이고 교정 C10이 `.git/index` mtime 불변을 잰다.
#   `--json <경로>`만 파일을 쓰며, 저장소 안 경로는 거부한다(scratchpad 등 저장소 밖만). 문서·Assets 무수정. Unity 무관.
# =============================================================================
import argparse
import bisect
import collections
import difflib
import glob
import json
import os
import re
import subprocess
import sys
import unicodedata

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.realpath(__file__))))
if not os.path.isfile(os.path.join(REPO, "docs", "verify", "promised_tests_scan.py")):
    raise SystemExit("✗ 저장소 루트를 찾지 못했다 (docs/verify/ 위치 기준)")

RESULT_DOC = "docs/verify/PROMISED_TEST_EXISTENCE_SCAN.md"
# 결과 문서 자신은 대상에서 뺀다 — 목록에 적은 이름이 다음 실행에서 「약속」으로 되먹임된다.
EXCLUDED_DOCS = {RESULT_DOC: "결과 문서 자신(자기 인용 순환)"}
LOG_DOCS = {"Tasklist.md"}  # 날짜별 기록 — 「그때 참」인 서술이라 거짓 서술 후보에서 따로 센다
RENAMES_TSV = "docs/verify/renames.tsv"

# ----------------------------------------------------------------------------
# 표기 사전 — (라) 판정
# ----------------------------------------------------------------------------
MARKERS_PLAN = (  # 계획·미작성형 (단위 범위)
    "명세만", "미작성", "작성 예정", "미착수", "착수 전", "미구현", "구현 예정", "추가 예정", "신설 예정",
    "만들 예정", "작성할 것", "계획", "예정", "TODO", "아직 없", "테스트 없음", "테스트가 없", "존재한 적 없",
    "부재", "한 번도 없", "존재하지 않", "이름 제안", "이름(안)", "추가 권고",
)
MARKERS_GONE = (  # 소멸 자기표기형 (단위 범위)
    "삭제", "제거됨", "제거했", "제거된", "폐기", "개명", "이름이 바뀌", "사라졌", "사라짐", "사라진", "대체됐", "대체됨",
    "개작", "옛 인용", "이관", "정정",
)
EXPECTED_MARKER_COUNTS = (24, 16)
# 정규식 표기 — 「N차 이름」(`5차 이름 X에서 … 정정`) · 「N-x 이름」(`5-b 이름 X`) = 옛 이름을 스스로 밝힌 병기
MARKER_REGEX = ((re.compile(r'\d+(?:차|-[a-z]) 이름'), "N차 이름"),)
EXPECTED_MARKER_REGEX_COUNT = 1
# ★ V1(verify-change 2026-09-15) — 표기어를 찾기 전에 **식별자 토큰**(밑줄 이음 토큰 · `.cs` 경로)을 같은 길이로 가린다.
#   실측: `미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다`가 **자기 이름 안의 「미구현」**으로 (라)가 됐다.
#   가리기만 하면 `TASKBAR_REVEAL.md` 「5차 이름 `…`에서 … 정정」이 표기를 잃는다 — 그래서 「정정」·「N차 이름」을 같이 넣었다.
_IDENT_TOKEN = re.compile(r'[\w가-힣]+(?:_[\w가-힣]+)+|[\w가-힣.\-/]*[\w가-힣]\.cs(?![A-Za-z0-9_])')
# (다) 전용 — 커밋에 못박은 인용(`1f7e139` 기준 / 판 / 시점)은 TEAM.md 「낡은 스냅숏」 규칙 5가 권하는 병기 형식이라 자기표기로 센다.
# ★ 2026-09-15 수정: 16진 토큰이 **실재 커밋일 때만** 앵커로 인정한다(`git cat-file -e <토큰>^{commit}`). 8자리 날짜 `20260915`와
#   git 객체가 아닌 `2746944`가 앵커로 샜다(verify-change). 「[a-f] 1자 이상」 규칙은 쓰지 않았다 — 숫자만인 **실재** 해시
#   `2051739`(`CornerHoverPanelTests` 소멸 커밋)를 잃는다(교정 C4 양성).
_COMMIT_ANCHOR = re.compile(r'`?\b([0-9a-f]{7,40})\b`?\s*(?:기준|판|시점)')
# 표 머리 · 절 · 문서 머리 범위에서만 쓰는 좁은 사전 — 넓은 범위에 「삭제」「계획」 같은 흔한 낱말을 쓰면 문서 전체가 (라)로 숨는다
# ★ 「`.cs` 0줄」은 뺐다(2회차 실행 감사): 보고 문서 머리의 「이 라운드 `.cs` 0줄」이 그 문서가 인용한 사라진 테스트까지 (라)로 숨겼다.
# ★ V3(verify-change 2026-09-15): 「미구현」·「구현 예정」을 뺐다 — 절 제목의 **기능** 낱말(「안쪽이 미구현이면」)이 그 절에 인용된
#   사라진 테스트를 (라)로 숨겼다(`manual/03-disappeared.md` 생성 시점 (다) → 매뉴얼 라운드가 절에 문장을 넣자 조용히 (라)).
WIDE_SCOPE_MARKERS = ("명세만", "미작성", "작성 예정", "미착수", "이름 제안", "이름(안)", "추가 권고", "존재한 적 없")
EXPECTED_WIDE_COUNT = 8
# ★ 2026-09-15 첫 실행 교정 C2 실패로 추가 — 사전 낱말이 들어 있지만 「부재 표기」가 아닌 복합어.
#   실측: `1f7e139`판 T-11 표 행(표기 없음)이 같은 행 354자 뒤 「**부재 단언**이 아니라 집합 등호로 쓴다」의 「부재」에
#   걸려 (라)로 숨었다 — 이 도구가 잡으려던 바로 그 사례를 사전이 가렸다. 그래서 (1) 맨 「부재」를 「테스트 부재」로 좁히고
#   (2) 아래 복합어 안의 적중은 무시하고 (3) 이름에서 MARKER_FAR 자 넘게 떨어진 단위 표기는 (라)로 인정하지 않는다(「먼 표기」 비고).
MARKER_NOT_ABSENCE = ("부재 단언", "부재단언", "부재 대조", "이동·삭제", "삭제·수정", "삭제하지 않", "삭제 금지", "삭제되지 않")
EXPECTED_NOT_ABSENCE_COUNT = 8
# 사전 밖 표기 후보 — (나)(다)인데 사전 표기는 없고 이 낱말이 이름 ±MARKER_FAR 안에 있으면 따로 목록
SOFT_MARKERS = (
    "없다", "없음", "없는", "필요", "제안", "후보", "신규", "신설", "추가", "만들", "후속", "인계", "백로그",
    "보류", "가칭", "예시", "예:", "가정", "권고", "쓸 것", "쓴다", "거짓", "옛 이름", "옛 판", "명세", "승인 후",
)
EXPECTED_SOFT_COUNT = 26
# 심각도 사전 — 출시 게이트 · 원칙 1~3 · 보안 · 세이브를 약속하면 높음
# ★ 맨 「게이트」는 뺐다(첫 실행에서 「대기 톱니 게이트」「개발 게이트」까지 전부 「출시 게이트」로 셌다).
SEVERITY_HIGH = (
    ("출시", "출시 게이트"), ("릴리즈", "출시 게이트"), ("릴리스", "출시 게이트"), ("출시 게이트", "출시 게이트"),
    ("Phase 게이트", "출시 게이트"), ("관문", "출시 게이트"), ("출하", "출시 게이트"), ("체크표", "출시 게이트"),
    ("원칙 1", "원칙"), ("원칙 2", "원칙"), ("원칙 3", "원칙"), ("원칙1", "원칙"), ("원칙2", "원칙"), ("원칙3", "원칙"),
    ("행동-텍스트", "원칙"), ("비침해", "원칙"), ("클릭 관통", "원칙"), ("유저 자산", "원칙"), ("사용자 자산", "원칙"),
    ("자산 불변", "원칙"),
    ("보안", "보안"), ("위협", "보안"), ("변조", "보안"),
    ("세이브", "세이브"), ("저장 파일", "세이브"), ("저장 스키마", "세이브"), ("스키마", "세이브"),
    ("CurrentVersion", "세이브"), ("하위 호환", "세이브"), ("데이터 유실", "세이브"),
)
SEVERITY_HIGH_PATHS = (
    ("docs/security/", "보안"), ("CRITICAL_PATH", "출시 게이트"), ("WINDOWS_CHECK_SESSION", "출시 게이트"),
    ("MILESTONES", "출시 게이트"), ("docs/marketing/", "출시 게이트"),
)
EXPECTED_SEVERITY_COUNTS = (30, 5)
MARKER_FAR = 300        # 단위 표기 인정 거리(글자)
SECTION_LINES = 80      # 절 범위 한도(줄)
DOC_HEAD_LINES = 20     # 문서 머리(줄)
SEVERITY_WINDOW = 400   # 긴 단위(>800자)에서는 이름 ±400자만 본다(Tasklist 한 줄이 수천 자라 전부 「높음」이 되는 것 방지)
KO_PARTICLES = "은는이가을를와과로도의에"
ID_RE = re.compile(r'^(?:[A-Z]{1,3}\d+[a-z]?)(?:_[A-Z]{1,3}\d+[a-z]?)*$')        # T3n · G1 · B3_B4
ID_PREFIX_RE = re.compile(r'^((?:[A-Z]{1,3}\d+[a-z]?_)+)')                         # F28_… 의 「F28_」


def nfc(s):
    return unicodedata.normalize("NFC", s)


# ★ 2026-09-15 verify-change 적발: plain `git status`는 stat 정보를 갱신하면서 `.git/index`를 **고쳐 쓰고** `index.lock`을 잡는다.
#   리더 커밋과 경합한다. 그래서 이 파일의 git 호출은 **전부** 호출 자리에 `"git", "--no-optional-locks"`(전역 옵션 위치)를
#   글자 그대로 쓴다 — 변수에 담지 않는다(리더 지시: 검색 한 번으로 모든 호출을 확인할 수 있게). 교정 C10이 `.git/index` 불변을 잰다.
_GIT_ENV = dict(os.environ, GIT_OPTIONAL_LOCKS="0")


def run_git(args, check=True, input_bytes=None):
    p = subprocess.run(["git", "--no-optional-locks", "-C", REPO] + args, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                       input=input_bytes, env=_GIT_ENV)
    if check and p.returncode != 0:
        raise RuntimeError("git " + " ".join(args[:3]) + " 실패: " + p.stderr.decode("utf-8", "replace")[:200])
    return p.stdout


# =============================================================================
# C# 해석기 — 주석·문자열·문자 리터럴을 공백으로 지운다(줄바꿈 보존). C# 9 기준(원시 문자열 없음 — 저장소의 `"""` 10건은 전부 @"" 이스케이프).
# =============================================================================
_SPECIAL = re.compile(r'//|/\*|["\'@$#{}\n]')
_CHAR_LIT = re.compile(r"'(?:\\(?:u[0-9A-Fa-f]{4}|x[0-9A-Fa-f]{1,4}|.)|[^'\\\n])'")
_STR_PREFIX = re.compile(r'(\$@|@\$|\$|@)?"')


class LexError(Exception):
    pass


def strip_cs(src):
    """반환: (code, literals) — literals = [(시작 위치, 내용, 보간 여부)]. 해석 실패 시 LexError."""
    src = nfc(src.replace("\r\n", "\n").replace("\r", "\n").lstrip("﻿"))
    n = len(src)
    out = list(src)
    literals = []

    def blank(a, b):
        for k in range(a, b):
            if out[k] != "\n":
                out[k] = " "

    def lex_code(i, in_hole):
        depth = 0
        at_line_start = True
        while True:
            m = _SPECIAL.search(src, i)
            if not m:
                if in_hole:
                    raise LexError("보간 구멍이 닫히지 않음")
                return n
            j = m.start()
            tok = m.group(0)
            if src[i:j].strip():
                at_line_start = False
            if tok == "\n":
                at_line_start = True
                i = j + 1
                continue
            if tok == "#" and at_line_start and not in_hole:
                e = src.find("\n", j)
                e = n if e < 0 else e
                blank(j, e)
                i = e
                continue
            at_line_start = False
            if tok == "//":
                e = src.find("\n", j)
                e = n if e < 0 else e
                blank(j, e)
                i = e
                continue
            if tok == "/*":
                e = src.find("*/", j + 2)
                if e < 0:
                    raise LexError("블록 주석이 닫히지 않음")
                blank(j, e + 2)
                i = e + 2
                continue
            if tok == "{":
                depth += 1
                i = j + 1
                continue
            if tok == "}":
                if in_hole and depth == 0:
                    return j
                depth -= 1
                i = j + 1
                continue
            if tok == "'":
                cm = _CHAR_LIT.match(src, j)
                if cm:
                    blank(j + 1, cm.end() - 1)
                    i = cm.end()
                else:
                    i = j + 1
                continue
            pm = _STR_PREFIX.match(src, j)
            if not pm:
                i = j + 1  # @식별자 · $ 단독 등
                continue
            prefix = pm.group(1) or ""
            verb = "@" in prefix
            interp = "$" in prefix
            k = pm.end()
            body_start = k
            while True:
                if k >= n:
                    raise LexError("문자열이 닫히지 않음")
                ch = src[k]
                if verb:
                    if ch == '"':
                        if k + 1 < n and src[k + 1] == '"':
                            k += 2
                            continue
                        break
                else:
                    if ch == "\\":
                        k += 2
                        continue
                    if ch == '"':
                        break
                    if ch == "\n":
                        raise LexError("일반 문자열 안 줄바꿈")
                if interp and ch == "{":
                    if k + 1 < n and src[k + 1] == "{":
                        k += 2
                        continue
                    k = lex_code(k + 1, True) + 1
                    continue
                if interp and ch == "}" and k + 1 < n and src[k + 1] == "}":
                    k += 2
                    continue
                k += 1
            literals.append((j, src[body_start:k], interp))
            blank(j, k + 1)
            i = k + 1

    lex_code(0, False)
    code = "".join(out)
    bal = 0
    for m in re.finditer(r'[{}]', code):
        bal += 1 if m.group(0) == "{" else -1
        if bal < 0:
            raise LexError("닫는 중괄호 과잉")
    if bal != 0:
        raise LexError("중괄호 불균형 %d" % bal)
    return code, literals


_TYPE_DECL = re.compile(r'(?<![\w가-힣.])(class|struct|interface|enum|record)\s+(?:(?:class|struct)\s+)?([A-Za-z_가-힣][\w가-힣]*)')
_METHOD_CAND = re.compile(r'(?<![\w가-힣])([A-Za-z_가-힣][\w가-힣]*)\s*(?:<[^<>(){};=]*(?:<[^<>(){};=]*>[^<>(){};=]*)*>)?\s*\(')
_MEMBER_CAND = re.compile(r'(?<![\w가-힣.])([A-Za-z_가-힣][\w가-힣]*)\s*(?==(?![=>])|;|\{|=>)')
_NOT_METHOD = frozenset("if for foreach while switch catch using lock return nameof typeof sizeof default new base this "
                        "checked unchecked fixed when stackalloc throw await yield else case do in is as out ref params "
                        "goto operator delegate get set add remove value var where select from try finally init".split())
_EXPR_PREV = frozenset("return new await throw yield else case in is as out ref params using goto not and or with "
                       "from select where do lock operator delegate".split())
_ATTR = re.compile(r'\[\s*(?:NUnit\.Framework\.)?([A-Za-z_]\w*)')


def _match_braces(code, open_ch, close_ch):
    st, pairs = [], {}
    for m in re.finditer(re.escape(open_ch) + "|" + re.escape(close_ch), code):
        i = m.start()
        if m.group(0) == open_ch:
            st.append(i)
        elif st:
            pairs[st.pop()] = i
    return pairs


def _prev_ok(code, s):
    """선언 앞자리: 형식 이름·`>`·`]`·`?`(nullable) — 식 키워드·`=>`·연산자 뒤는 아니다."""
    pre = code[max(0, s - 200):s].rstrip()
    if not pre:
        return False
    pc = pre[-1]
    if pc == ">" and pre.endswith("=>"):
        return False
    if pc == "?":
        return len(pre) >= 2 and (pre[-2].isalnum() or pre[-2] in "_>]")
    isword = pc.isalnum() or pc == "_" or "가" <= pc <= "힣"
    if not (isword or pc in ">]"):
        return False
    if isword:
        wm = re.search(r'([\w가-힣]+)$', pre)
        if wm and wm.group(1) in _EXPR_PREV:
            return False
    return True


def extract_decls(code, literals):
    braces = _match_braces(code, "{", "}")
    type_spans = []  # (open, close, name)
    types = collections.defaultdict(list)
    for m in _TYPE_DECL.finditer(code):
        name = m.group(2)
        types[name].append(code.count("\n", 0, m.start()) + 1)
        k = m.end()
        while k < len(code) and code[k] not in "{;":
            k += 1
        if k < len(code) and code[k] == "{" and k in braces:
            type_spans.append((k, braces[k], name))
    type_spans.sort()
    type_open = {o: nm for (o, c, nm) in type_spans}

    def chain_at(pos):
        return tuple(nm for (o, c, nm) in type_spans if o < pos < c)

    parens = _match_braces(code, "(", ")")
    methods = []
    for m in _METHOD_CAND.finditer(code):
        name = m.group(1)
        if name in _NOT_METHOD:
            continue
        s = m.start()
        if not _prev_ok(code, s):
            continue
        op = m.end() - 1
        if op not in parens:
            continue
        rest = code[parens[op] + 1:parens[op] + 64].lstrip()
        chain = chain_at(s)
        ok = rest.startswith("{") or rest.startswith("=>") or rest.startswith(";") or rest.startswith("where")
        if not ok and rest.startswith(":") and chain and chain[-1] == name:
            ok = True
        if not ok:
            continue
        back = max(code.rfind(";", 0, s), code.rfind("{", 0, s), code.rfind("}", 0, s))
        head = code[back + 1:s]
        attrs = set(_ATTR.findall(head))
        methods.append((name, chain, frozenset(attrs), code.count("\n", 0, s) + 1))

    # 멤버(필드·속성·상수): 가장 안쪽 중괄호가 **형식 본문**인 자리에서만 — 메서드 안 지역 변수는 세지 않는다
    members = []
    cands = [m for m in _MEMBER_CAND.finditer(code) if m.group(1) not in _NOT_METHOD]
    if cands:
        bpos = [(mm.start(), mm.group(0)) for mm in re.finditer(r'[{}]', code)]
        stack, bi = [], 0
        for m in cands:
            s = m.start()
            while bi < len(bpos) and bpos[bi][0] < s:
                if bpos[bi][1] == "{":
                    stack.append(bpos[bi][0])
                elif stack:
                    stack.pop()
                bi += 1
            if not stack or stack[-1] not in type_open:
                continue
            if not _prev_ok(code, s):
                continue
            members.append((m.group(1), chain_at(s)))

    runtime_names, runtime_patterns, runtime_suffixes = set(), [], set()
    for (pos, body, interp) in literals:
        before = code[max(0, pos - 64):pos]
        if re.search(r'SetName\s*\(\s*$', before) or re.search(r'TestName\s*=\s*$', before):
            if interp:
                runtime_patterns.append("^" + re.sub(r'\\\{.*?\\\}', ".*", re.escape(body)) + "$")
            else:
                runtime_names.add(body)
        if re.search(r'(?:GetType\s*\(\s*\)\s*\.\s*Name|nameof\s*\([^()]*\))\s*\+\s*$', before) and len(body) >= 3:
            runtime_suffixes.add(body)
    return {"types": dict(types), "methods": methods, "members": members, "runtime_names": runtime_names,
            "runtime_patterns": runtime_patterns, "runtime_suffixes": runtime_suffixes,
            "literal_bodies": [b for (_, b, _) in literals]}


def keys_of(decl):
    ks = set()
    for t in decl["types"]:
        ks.add(("type", t))
    for (name, chain, attrs, line) in decl["methods"]:
        ks.add(("method", name))
        for c in chain:
            ks.add(("cm", c, name))
    for (name, chain) in decl["members"]:
        for c in chain:
            ks.add(("cm", c, name))
    return ks


class Index:
    """소스 집합의 선언 색인."""

    def __init__(self):
        self.types = collections.defaultdict(set)      # 이름 -> {파일}
        self.methods = collections.defaultdict(list)   # 이름 -> [(파일, 사슬, attrs, 줄)]
        self.cm = collections.defaultdict(set)         # (C, M) -> {파일}   M = 메서드·멤버·중첩 형식
        self.file_types = {}                           # 파일 -> 형식 수
        self.runtime_names = set()
        self.runtime_patterns = []
        self.runtime_suffixes = set()
        self.class_runtime_tokens = collections.defaultdict(set)
        self.literal_blob = []
        self.lex_failures = []
        self.raw_text = {}
        self._by_class = None

    def members_of(self, c):
        if self._by_class is None:
            self._by_class = collections.defaultdict(set)
            for (cc, mm) in self.cm:
                self._by_class[cc].add(mm)
        return self._by_class.get(c, set())

    def add(self, relpath, text):
        self.raw_text[relpath] = text
        try:
            code, lits = strip_cs(text)
        except LexError as e:
            self.lex_failures.append((relpath, str(e)))
            return
        d = extract_decls(code, lits)
        self.file_types[relpath] = len(d["types"])
        for t in d["types"]:
            self.types[t].add(relpath)
        for (name, chain, attrs, line) in d["methods"]:
            self.methods[name].append((relpath, chain, attrs, line))
            for c in chain:
                self.cm[(c, name)].add(relpath)
        for (name, chain) in d["members"]:
            for c in chain:
                self.cm[(c, name)].add(relpath)
        self._by_class = None
        self.runtime_names |= d["runtime_names"]
        self.runtime_patterns += d["runtime_patterns"]
        self.runtime_suffixes |= d["runtime_suffixes"]
        toks = set()
        for rn in d["runtime_names"]:
            toks |= set(re.findall(r'[\w가-힣]+', rn))
        for rp in d["runtime_patterns"]:
            toks |= set(re.findall(r'[A-Za-z가-힣][\w가-힣]+', rp.replace("\\", " ")))
        if toks:
            for t in d["types"]:
                self.class_runtime_tokens[t] |= toks
        self.literal_blob.append("\n".join(d["literal_bodies"]))


# =============================================================================
# 입력 수집 + 대조(C6)
# =============================================================================
def _glob_rel(patterns):
    out = set()
    for p in patterns:
        for f in glob.glob(os.path.join(REPO, p), recursive=True):
            if os.path.isfile(f):
                out.add(nfc(os.path.relpath(f, REPO).replace(os.sep, "/")))
    return out


def _git_list(args):
    raw = run_git(["-c", "core.quotepath=off", "ls-files", "-z"] + args)
    return {nfc(x.decode("utf-8")) for x in raw.split(b"\0") if x}


def is_test_cs(p):
    return p.startswith("Assets/") and "/Tests/" in p and p.endswith(".cs")


def is_target_doc(p):
    return ((p.startswith("docs/") and p.endswith(".md")) or p in ("CLAUDE.md", "Tasklist.md")
            or (p.startswith(".claude/agents/") and p.endswith(".md") and p.count("/") == 2))


def collect_inputs():
    problems = []
    g_tests = {p for p in _glob_rel(["Assets/**/*.cs"]) if is_test_cs(p)}
    g_docs = {p for p in _glob_rel(["docs/**/*.md", "CLAUDE.md", "Tasklist.md", ".claude/agents/*.md"]) if is_target_doc(p)}
    tracked = _git_list([])
    untracked = _git_list(["--others", "--exclude-standard"])
    deleted = _git_list(["--deleted"])
    ignored = _git_list(["--others", "--ignored", "--exclude-standard"])
    for label, gset, pred in (("테스트 .cs", g_tests, is_test_cs), ("문서 .md", g_docs, is_target_doc)):
        want = {p for p in (tracked | untracked) if pred(p)} - deleted
        ign = {p for p in ignored if pred(p)}
        if gset - ign != want:
            problems.append("C6 %s: glob %d vs git %d (glob만 %s / git만 %s)" % (
                label, len(gset - ign), len(want), sorted(gset - ign - want)[:3], sorted(want - gset)[:3]))
    tests = sorted(g_tests - {p for p in ignored if is_test_cs(p)})
    docs = sorted(p for p in g_docs - {p for p in ignored if is_target_doc(p)} if p not in EXCLUDED_DOCS)
    status = {}
    for line in run_git(["-c", "core.quotepath=off", "status", "--porcelain", "-z"]).split(b"\0"):
        if len(line) > 3:
            status[nfc(line[3:].decode("utf-8"))] = line[:2].decode()
    return tests, docs, status, problems


def read(rel):
    with open(os.path.join(REPO, rel), encoding="utf-8", errors="replace") as f:
        return nfc(f.read())


def cat_blobs(shas):
    shas = sorted(set(shas))
    if not shas:
        return {}
    raw = run_git(["cat-file", "--batch"], input_bytes=("\n".join(shas) + "\n").encode())
    out, i = {}, 0
    while i < len(raw):
        nl = raw.index(b"\n", i)
        hdr = raw[i:nl].split()
        if len(hdr) < 3 or hdr[1] != b"blob":
            i = nl + 1
            continue
        size = int(hdr[2])
        out[hdr[0].decode()] = nfc(raw[nl + 1:nl + 1 + size].decode("utf-8", "replace"))
        i = nl + 1 + size + 1
    return out


def load_renames():
    """옛 메서드 이름 -> (클래스, 새 메서드 이름). 대장 파일이 없으면 빈 dict(판정은 대장 없이도 성립)."""
    out = {}
    p = os.path.join(REPO, RENAMES_TSV)
    if not os.path.isfile(p):
        return out
    for line in read(RENAMES_TSV).split("\n"):
        if not line.strip() or line.startswith("#"):
            continue
        f = line.split("\t")
        if len(f) < 2:
            continue
        old, new = f[0].split("."), f[1].split(".")
        if len(old) >= 2 and len(new) >= 2:
            out[old[-1]] = (old[-2], new[-1])
    return out


# =============================================================================
# 이력 — HEAD 1부모 사슬 타임라인
# =============================================================================
class History:
    def __init__(self):
        raw = run_git(["-c", "core.quotepath=off", "log", "--reverse", "--first-parent", "-m", "--raw", "--no-abbrev",
                       "--no-renames", "--date=short", "--format=@@%H %ad", "HEAD", "--", "Assets"]).decode("utf-8", "replace")
        self.commits = []  # [(short, date, [(status, path, newsha, oldsha)])]
        cur = None
        for line in raw.split("\n"):
            if line.startswith("@@"):
                parts = line[2:].split()
                cur = (parts[0][:7], parts[1], [])  # --no-abbrev 가 %h 까지 전체 해시로 만든다 — %H 앞 7자
                self.commits.append(cur)
            elif line.startswith(":") and cur is not None:
                meta, path = line.split("\t", 1)
                path = nfc(path)
                f = meta.split()
                if is_test_cs(path):
                    cur[2].append((f[4][0], path, f[3], f[2]))
        raw_all = run_git(["-c", "core.quotepath=off", "log", "--all", "--raw", "--no-abbrev", "--no-renames", "--format=",
                           "--", "Assets"]).decode("utf-8", "replace")
        all_shas = set()
        for line in raw_all.split("\n"):
            if line.startswith(":"):
                meta, path = line.split("\t", 1)
                f = meta.split()
                if is_test_cs(nfc(path)) and set(f[3]) != {"0"}:
                    all_shas.add(f[3])
        tl_shas = {sha for c in self.commits for (_, _, sha, _) in c[2] if set(sha) != {"0"}}
        texts = cat_blobs(all_shas | tl_shas)
        self.blob_keys, self.blob_types, self.lex_failures = {}, {}, []
        for sha, text in texts.items():
            try:
                code, lits = strip_cs(text)
                d = extract_decls(code, lits)
                self.blob_keys[sha] = keys_of(d)
                self.blob_types[sha] = len(d["types"])
            except LexError as e:
                self.lex_failures.append((sha[:10], str(e)))
                self.blob_keys[sha] = set()
                self.blob_types[sha] = 0
        self.side_keys = set()
        for sha in all_shas - tl_shas:
            self.side_keys |= self.blob_keys.get(sha, set())
        self.all_method_names = {k[1] for ks in self.blob_keys.values() for k in ks if k[0] == "method"}
        self.all_cm = {(k[1], k[2]) for ks in self.blob_keys.values() for k in ks if k[0] == "cm"}
        self.cm_by_class = collections.defaultdict(set)
        for (cc, mm) in self.all_cm:
            self.cm_by_class[cc].add(mm)

    def build_events(self, tracked_keys):
        """tracked 키마다 [(short, date, 'A'|'V', 같은 커밋에 같은 클래스로 새로 생긴 선언들)] + 경로별 이벤트."""
        tracked = set(tracked_keys)
        tracked_classes = {k[1] for k in tracked if k[0] == "cm"}
        state, counts = {}, collections.Counter()
        events = collections.defaultdict(list)
        path_events = collections.defaultdict(list)
        for (short, date, changes) in self.commits:
            before = {k for k in tracked if counts[k] > 0}
            added_cm = collections.defaultdict(set)
            for (st, path, sha, oldsha) in changes:
                old = state.get(path)
                oldkeys = self.blob_keys.get(old, set()) if old is not None else set()
                newkeys = self.blob_keys.get(sha, set()) if st != "D" else set()
                for k in oldkeys & tracked:
                    counts[k] -= 1
                for k in newkeys & tracked:
                    counts[k] += 1
                for k in newkeys - oldkeys:
                    if k[0] == "cm" and k[1] in tracked_classes:
                        added_cm[k[1]].add(k[2])
                had = old is not None and self.blob_types.get(old, 0) > 0
                if st == "D":
                    state.pop(path, None)
                    new_has = False
                else:
                    state[path] = sha
                    new_has = self.blob_types.get(sha, 0) > 0
                if had != new_has:
                    path_events[path].append((short, date, "A" if new_has else "V", ()))
            for k in tracked:
                now = counts[k] > 0
                if (k in before) != now:
                    cands = ()
                    if not now and k[0] == "cm":
                        cands = tuple(sorted(added_cm.get(k[1], ())))
                    events[k].append((short, date, "A" if now else "V", cands))
        return events, path_events


# =============================================================================
# 문서 약속 추출
# =============================================================================
_P_BRACE = re.compile(r'(?<![\w가-힣])([A-Z][A-Za-z0-9_]*)\{([A-Za-z0-9_, ]+)\}([A-Za-z0-9_]*)(\.cs)?')
_P_PATH = re.compile(r'(?<![\w가-힣])(?:[\w가-힣.\-]+/)*(EditMode|PlayMode)/((?:[\w가-힣]+/)*[\w가-힣][\w가-힣.]*?)\.cs(?![A-Za-z0-9_])')
_P_CM = re.compile(r'(?<![\w가-힣])([A-Z][A-Za-z0-9_]*Tests)\.([A-Za-z_가-힣][\w가-힣]*)((?:…|\.\.\.)([\w가-힣]*))?')
_P_CLASS = re.compile(r'(?<![\w가-힣])([A-Z][A-Za-z0-9_]*Tests)(?![\w가-힣])')
_P_KO = re.compile(r'(?<![\w가-힣./\-])([\w가-힣]+)((?:…|\.\.\.)([\w가-힣]*))?(?![\w가-힣])')
_P_EN = re.compile(r'`([A-Za-z][A-Za-z0-9]*(?:_[A-Za-z0-9]+)+)(?:\([^`]*\))?`')
_EN_TESTLIKE = re.compile(r'^(?:NegativeControl|T\d+[a-z]?_|(?:[A-Z][a-z]+){3,}_[A-Z][a-z]+(?:[A-Z][a-z0-9]*)+)')
_IS_ITEM = re.compile(r'^(\s*)(?:[-*+]|\d+[.)])\s')


def doc_units(lines):
    """(첫 줄, 끝 줄) — 표 행 = 한 줄, 제목 = 한 줄, 코드 펜스 = 한 덩어리, 목록 항목 = 항목 + 들여쓴 이음줄, 그 외 문단."""
    units = []
    i, N = 0, len(lines)
    while i < N:
        L = lines[i]
        s = L.strip()
        if not s:
            i += 1
            continue
        if s.startswith("```"):
            j = i + 1
            while j < N and not lines[j].strip().startswith("```"):
                j += 1
            units.append((i, min(j, N - 1)))
            i = j + 1
            continue
        if s.startswith("|") or s.startswith("#"):
            units.append((i, i))
            i += 1
            continue
        im = _IS_ITEM.match(L)
        if im:
            ind = len(im.group(1))
            j = i + 1
            while j < N:
                t = lines[j]
                ts = t.strip()
                if not ts or ts.startswith("|") or ts.startswith("#") or ts.startswith("```"):
                    break
                jm = _IS_ITEM.match(t)
                if jm and len(jm.group(1)) <= ind:
                    break
                if not jm and (len(t) - len(t.lstrip())) <= ind and not t.startswith(" "):
                    break
                j += 1
            units.append((i, j - 1))
            i = j
            continue
        j = i + 1
        while j < N:
            t = lines[j]
            ts = t.strip()
            if not ts or ts.startswith("|") or ts.startswith("#") or ts.startswith("```") or _IS_ITEM.match(t):
                break
            j += 1
        units.append((i, j - 1))
        i = j
    return units


def _scope_texts(lines, a):
    """줄 a(0기준)의 넓은 범위 텍스트: (표 머리 행, 절 텍스트, 문서 머리)."""
    header = ""
    if lines[a].lstrip().startswith("|"):
        k = a
        while k - 1 >= 0 and lines[k - 1].lstrip().startswith("|"):
            k -= 1
        header = lines[k]
    k = a
    lo = max(0, a - SECTION_LINES)
    while k > lo and not lines[k].lstrip().startswith("#"):
        k -= 1
    section = ""
    if lines[k].lstrip().startswith("#"):
        # ★ 절 안에서 **다른 이름을 품은 줄**(표 행·목록 항목)의 표기는 그 이름 몫이다 — 절 전체로 번지면 뒤 이름이 전부 (라)로 숨는다
        #   (합성 교정 C8③ 실측: 표 한 행의 「명세만」이 같은 절 뒤 이름 10개를 (라)로 만들었다).
        section = "\n".join([lines[k]] + [L for L in lines[k + 1:a] if not _PROMISE_LIKE.search(L)])
    return header, section, _doc_head(lines)


_PROMISE_LIKE = re.compile(r'[A-Z][A-Za-z0-9_]*Tests(?![\w가-힣])|[\w가-힣]*[가-힣][\w가-힣]*_[\w가-힣]*_|(?:EditMode|PlayMode)/')


_HEAD_CACHE = {}


def _doc_head(lines):
    """문서 머리 = 첫 `##` 제목 전 서문 중 **문단 줄**만(표 행·목록 항목·제목 제외), 최대 DOC_HEAD_LINES줄.
    ★ 첫 20줄을 통째로 보면 서문의 표 한 행 「명세만」이 문서 전체를 (라)로 숨긴다(합성 교정에서 실측)."""
    key = id(lines)
    if key in _HEAD_CACHE and _HEAD_CACHE[key][0] is lines:
        return _HEAD_CACHE[key][1]
    keep = []
    for i, L in enumerate(lines[:DOC_HEAD_LINES]):
        s = L.strip()
        if i > 0 and s.startswith("## "):
            break
        if not s or s.startswith("|") or s.startswith("#") or _IS_ITEM.match(L) or s.startswith("```"):
            continue
        keep.append(L)
    head = "\n".join(keep)
    _HEAD_CACHE[key] = (lines, head)
    return head


def extract_promises(rel, text):
    """반환: ([출현 dict], 패턴 표기 수). 출현 dict: file line kind key anchor unit off pat scopes"""
    lines = text.split("\n")
    out, wildcard = [], 0
    for (a, b) in doc_units(lines):
        unit = "\n".join(lines[a:b + 1])
        starts, acc = [], 0
        for k in range(a, b + 1):
            starts.append(acc)
            acc += len(lines[k]) + 1
        taken = []

        def line_of(off):
            return a + bisect.bisect_right(starts, off)

        def free(s, e):
            return all(e <= ts or s >= te for (ts, te) in taken)

        def emit(off, kind, key, anchor, pat=None):
            ln = line_of(off)
            out.append(dict(file=rel, line=ln, kind=kind, key=key, anchor=anchor, unit=unit, off=off, pat=pat,
                            scopes=_scope_texts(lines, ln - 1)))

        for m in _P_BRACE.finditer(unit):
            alts = [x.strip() for x in m.group(2).split(",") if x.strip()]
            names = [m.group(1) + x + m.group(3) for x in alts]
            anchor = m.group(0)
            if not (m.group(4) or any(nm.endswith("Tests") for nm in names)):
                continue
            for x, nm in zip(alts, names):
                if m.group(4):
                    emit(m.start(), "file", ("file", nm + ".cs"), anchor)
                else:
                    emit(m.start(), "class", ("type", nm), anchor)
            taken.append((m.start(), m.end()))
        for m in _P_PATH.finditer(unit):
            if not free(m.start(), m.end()):
                continue
            emit(m.start(), "path", ("path", "Tests/%s/%s.cs" % (m.group(1), m.group(2))), m.group(2) + ".cs")
            taken.append((m.start(), m.end()))
        for m in _P_CM.finditer(unit):
            if not free(m.start(), m.end()):
                continue
            c, meth = m.group(1), m.group(2)
            if re.match(r'(cs|meta|md|xml|json|log|png|txt)(?![A-Za-z0-9_])', meth):
                if unit[max(0, m.start() - 1):m.start()] in ("*", "…", "{"):
                    wildcard += 1  # `Tests/EditMode/*AuditTests.cs` 같은 패턴 표기
                elif meth.startswith("cs"):
                    emit(m.start(), "class", ("type", c), c)
                taken.append((m.start(), m.start() + len(c) + 1 + len(meth)))
                continue
            after = unit[m.end():m.end() + 3]
            if not m.group(3) and after == ".cs":
                emit(m.start(), "file", ("file", c + "." + meth + ".cs"), c + "." + meth + ".cs")
                taken.append((m.start(), m.end() + 3))
                continue
            if re.match(r'[A-Za-z_]', meth) and re.search(r'[가-힣]', meth):  # 영문 이름 뒤에 붙은 조사: EquipEverySlot은
                cut = re.match(r'[A-Za-z0-9_]+', meth).group(0)
                if not re.search(r'[가-힣]', meth[:len(cut)]) and re.fullmatch(r'[가-힣]{1,3}', meth[len(cut):]):
                    meth = cut
            pat = None
            if m.group(3):
                pat = ("glob", meth, m.group(4) or "")
            emit(m.start(), "cm", ("cm", c, meth if not pat else meth + "…" + pat[2]), m.group(0), pat)
            taken.append((m.start(), m.end()))
        for m in _P_CLASS.finditer(unit):
            if not free(m.start(), m.end()):
                continue
            prev = unit[max(0, m.start() - 1):m.start()]
            if prev in ("*", "…", "{"):
                wildcard += 1
                taken.append((m.start(), m.end()))
                continue
            emit(m.start(), "class", ("type", m.group(1)), m.group(1))
            taken.append((m.start(), m.end()))
        for m in _P_KO.finditer(unit):
            if not free(m.start(), m.end()):
                continue
            left, right = m.group(1), m.group(3) or ""
            full = m.group(0)
            if not re.search(r'[가-힣]', full) or full.count("_") < 2:
                continue
            if re.match(r'\.[A-Za-z]{1,5}\b', unit[m.end():m.end() + 6]):  # 파일명(…_…_….md)
                continue
            if re.match(r'\d', left) or (left.startswith("_") and left.endswith("_") and not m.group(2)):
                continue  # C# 식별자는 숫자로 시작하지 못한다(`1_수정전_붙잡힘=Fallen.png`) / 마크다운 기울임 `_전에_`
            if not re.search(r'[가-힣]', re.sub(r'(?<=[A-Za-z0-9])[가-힣]{1,2}$', '', left + right)):
                continue  # 한글이 조사뿐인 영문 대문자 식별자(`STICKMATE_ACTIVE_DIVISOR로`)
            if m.group(2):
                pat = ("glob", left, right)
                key = ("method", left + "…" + right)
            elif unit[max(0, m.start() - 1):m.start()] == ">" and re.search(r'<[^<>]*[가-힣\s][^<>]*>$', unit[max(0, m.start() - 40):m.start()]):
                # 자리표시자 뒤 꼬리(`v10_파일을_읽어도_<새묶음>이_…`). ★ `<br>`·`<b>` 같은 HTML 태그 뒤는 아니다(3회차 실행 감사 — BASELINE 표)
                pat = ("glob", "", left)
                key = ("method", "…" + left)
            elif left.endswith("_"):
                pat = ("glob", left, "")
                key = ("method", left + "…")
            else:
                pat = None
                key = ("method", left)
            emit(m.start(), "ko", key, full, pat)
            taken.append((m.start(), m.end()))
        for m in _P_EN.finditer(unit):
            tok = m.group(1)
            s = m.start(1)
            if not free(s, s + len(tok)):
                continue
            if not (re.search(r'[a-z]', tok) and re.search(r'[A-Z]', tok)):
                continue
            emit(s, "en", ("method", tok), tok)
            taken.append((s, s + len(tok)))
    return out, wildcard


# =============================================================================
# 판정
# =============================================================================
def _name_match(pat, name):
    kind, pre, suf = pat
    return name.startswith(pre) and name.endswith(suf) and len(name) >= len(pre) + len(suf)


class Resolver:
    def __init__(self, work, head, hist, prod, renames, pickaxe=True, exclude_names=()):
        self.work, self.head, self.hist, self.prod, self.renames = work, head, hist, prod, renames
        self.exclude_names = set(exclude_names)
        self.pickaxe_enabled = pickaxe
        self.cache = {}
        self._events = {}
        self._path_events = {}
        self._pickaxe = {}
        self._prod_literals = "\n".join(prod.literal_blob)

    # ---- 이력 준비 ----
    def prepare_history(self, occs):
        tracked = set()
        for o in occs:
            k = o["key"]
            if k[0] in ("path", "file"):
                continue
            if k[0] == "type":
                tracked.add(k)
            elif k[0] == "method":
                for nm in self._hist_candidates_method(k[1], o.get("pat")):
                    tracked.add(("method", nm))
            elif k[0] == "cm":
                c, m = k[1], k[2]
                tracked.add(("type", c))
                for mm in self.hist.cm_by_class.get(c, ()):
                    if self._cm_name_ok(m, mm, o.get("pat")):
                        tracked.add(("cm", c, mm))
                if not o.get("pat"):
                    tracked.add(("method", m))
                    tracked.add(("type", m))
        self._events, self._path_events = self.hist.build_events(tracked)

    def _hist_candidates_method(self, name, pat):
        if pat:
            return sorted(n for n in self.hist.all_method_names if _name_match(pat, n))
        c = [name] if name in self.hist.all_method_names else []
        if not c:
            c = sorted(n for n in self.hist.all_method_names if len(name) >= 8 and name in n)[:3]
        if not c and name[-1:] in KO_PARTICLES and name[:-1] in self.hist.all_method_names:
            c = [name[:-1]]
        return c

    @staticmethod
    def _cm_name_ok(m, mm, pat):
        if pat:
            return _name_match(pat, mm)
        if mm == m:
            return True
        if ID_RE.match(m) and mm.startswith(m + "_"):
            return True
        if re.search(r'[가-힣]', m) and m.count("_") >= 2 and (mm.startswith(m) or (m[-1:] in KO_PARTICLES and mm == m[:-1])):
            return True
        return False

    def pickaxe(self, token, path="*.cs"):
        if not self.pickaxe_enabled or not token:
            return []
        k = (token, path)
        if k not in self._pickaxe:
            raw = run_git(["log", "--all", "--format=%h %ad", "--date=short", "-S", token, "--", path]).decode()
            self._pickaxe[k] = [x for x in raw.split("\n") if x]
        return self._pickaxe[k]

    def _hist(self, key):
        ev = self._events.get(key, [])
        if ev:
            if ev[-1][2] == "V":
                return dict(intro=ev[0][0] + " " + ev[0][1], vanish=ev[-1][0] + " " + ev[-1][1], vanish_c=ev[-1][0],
                            rename_cands=list(ev[-1][3]), reappear=sum(1 for e in ev if e[2] == "A") > 1)
            return dict(intro=ev[0][0] + " " + ev[0][1], vanish="(HEAD에 있음 · 작업 트리에서만 사라짐 — 미커밋)",
                        vanish_c=None, rename_cands=[], reappear=False)
        if key in self.hist.side_keys:
            return dict(intro="(곁가지)", vanish="(병합 전 곁가지에서 소멸)", vanish_c=None, rename_cands=[], reappear=False)
        return None

    # ---- 실재 ----
    def _present(self, idx, key, pat):
        kind = key[0]
        if kind == "type":
            if idx.types.get(key[1]):
                return (sorted(idx.types[key[1]])[0], "")
            # ★ 파일 줄기 대조(verify-change 2026-09-15): `PopoverAndHoverPanelOpacityTests`는 **파일**이 실재하고 안의 형식명만 다르다
            #   (`PopoverPanelOpacityTests`). 형식 선언만 보면 (나)로 오판한다.
            stem = [p for p in idx.file_types if idx.file_types[p] > 0 and p.rsplit("/", 1)[-1][:-3] == key[1]]
            return (stem[0], "파일 줄기 일치 — 파일은 실재, 안의 형식명은 다름") if stem else None
        if kind == "method":
            name = key[1]
            if pat:
                hits = sorted(n for n in idx.methods if _name_match(pat, n))
                return (hits[0], "잘린 이름 — 접두·접미 일치") if hits else None
            if idx.methods.get(name):
                return (name, "")
            if name[-1:] in KO_PARTICLES and idx.methods.get(name[:-1]):
                return (name[:-1], "조사 제거 일치")
            if len(name) >= 8:
                hits = sorted(n for n in idx.methods if name in n)
                if hits:
                    return (hits[0], "부분 일치 → " + hits[0])
            return None
        if kind == "cm":
            c, m = key[1], key[2]
            if pat:
                hits = sorted(mm for mm in idx.members_of(c) if _name_match(pat, mm))
                return (hits[0], "잘린 이름 — 접두·접미 일치") if hits else None
            if idx.cm.get((c, m)):
                return (m, "")
            if idx.types.get(m) and idx.types.get(c) and set(idx.types[m]) & set(idx.types[c]):
                return (m, "중첩 형식")
            for mm in sorted(idx.members_of(c)):
                if self._cm_name_ok(m, mm, None) and mm != m:
                    note = "ID 접두 인용 → " + mm if ID_RE.match(m) else ("조사 제거 일치" if mm == m[:-1] else "접두 일치 → " + mm)
                    return (mm, note)
            return None
        if kind == "path":
            hits = [p for p in idx.file_types if p.endswith("/" + key[1]) and idx.file_types[p] > 0]
            return (hits[0], "") if hits else None
        if kind == "file":
            hits = [p for p in idx.file_types if p.rsplit("/", 1)[-1] == key[1] and idx.file_types[p] > 0]
            return (hits[0], "") if hits else None
        return None

    def resolve(self, key, pat=None):
        ck = (key, pat)
        if ck not in self.cache:
            self.cache[ck] = self._resolve(key, pat)
        return self.cache[ck]

    def _runtime(self, name):
        for idx in (self.work, self.prod):
            if name in idx.runtime_names:
                return "런타임 이름 문자열(TestName/SetName)과 일치"
            for p in idx.runtime_patterns:
                if re.match(p, name):
                    return "보간 런타임 이름 틀과 일치"
            for suf in idx.runtime_suffixes:
                if name.endswith(suf) and len(name) > len(suf):
                    return "GetType().Name/nameof + \"%s\" 조립 가능" % suf
        return None

    def _fragment_anywhere(self, frag):
        return (any(frag in n for n in self.work.methods) or any(frag in n for n in self.hist.all_method_names)
                or any(frag in mm for (cc, mm) in self.work.cm) or any(frag in mm for (cc, mm) in self.hist.all_cm))

    def _in_production(self, name):
        """프로덕션 **선언** 또는 **문자열 리터럴**에 있으면 테스트 약속이 아니다(주석은 보지 않는다 — 주석에 테스트 이름을 적는 일이 흔하다)."""
        if self.prod.methods.get(name) or self.prod.types.get(name) or any(mm == name for (cc, mm) in self.prod.cm):
            return True
        return name in self._prod_literals

    def _resolve(self, key, pat):
        kind = key[0]
        name = key[-1]
        r = dict(key=key, v=None, notes=[], evidence=[])
        if self.exclude_names and any(part.split("…")[0] in self.exclude_names for part in key[1:]):
            r["v"] = "제외"
            r["notes"].append("스캐너 합성 교정 이름(보고 요약이 실데이터로 되먹인 것) — 테스트 약속 아님")
            return r
        hit = self._present(self.work, key, pat)
        if hit:
            r["v"] = "가"
            if hit[1]:
                r["notes"].append(hit[1])
            if not self._present(self.head, key, pat):
                r["notes"].append("작업 트리 실재 · 미커밋")
            return r
        # ---- 테스트 약속 아님 ----
        if kind == "type" and name.endswith("ForTests"):
            r["v"] = "제외"
            r["notes"].append("테스트 훅(…ForTests) — 테스트 클래스 약속 아님")
            return r
        if key[0] == "method" and not re.search(r'[가-힣]', name) and not pat:
            if not self._hist_candidates_method(name, None) and (not _EN_TESTLIKE.match(name) or self._in_production(name)):
                r["v"] = "제외"
                r["notes"].append("약한 영문 밑줄 토큰(네이티브·직렬화 필드·수식 기호 등) — 테스트 이름 근거 없음")
                return r
        if kind == "cm":
            c, m = key[1], key[2]
            if not pat and (self.work.methods.get(m) or self.work.types.get(m)):
                where = sorted({ch[-1] if ch else "?" for (f, ch, a, l) in self.work.methods.get(m, [])})
                r["v"] = "가"
                r["notes"].append("메서드는 실재하나 인용 클래스 불일치 → 실제 %s" % ",".join(where)[:80])
                return r
        if kind in ("path", "file"):
            base = key[1].rsplit("/", 1)[-1][:-3]
            if [p for p in self.work.raw_text if p.endswith("/" + key[1])]:
                r["notes"].append("파일은 있으나 선언 0(전체 주석 등)")
            ev = []
            for p, e in self._path_events.items():
                if (kind == "path" and p.endswith("/" + key[1])) or (kind == "file" and p.rsplit("/", 1)[-1] == key[1]):
                    ev = e
                    break
            cls = base.split(".")[0]
            if self.work.types.get(cls):
                r["notes"].append("같은 이름 형식은 다른 파일에 실재: %s" % sorted(self.work.types[cls])[0])
            if ev:
                r.update(v="다", intro=ev[0][0] + " " + ev[0][1],
                         vanish=(ev[-1][0] + " " + ev[-1][1]) if ev[-1][2] == "V" else "(작업 트리에서 삭제 · 미커밋)",
                         vanish_c=ev[-1][0] if ev[-1][2] == "V" else None)
            else:
                rt = self._runtime(cls)
                r["v"] = "마" if rt else "나"
                if rt:
                    r["notes"].append(rt)
            r["evidence"] = self.pickaxe(cls)
            return r
        # ---- 이력 ----
        hv = None
        if kind == "type":
            hv = self._hist(key)
            if not hv:
                for p, e in self._path_events.items():
                    if p.rsplit("/", 1)[-1] == name + ".cs" and e and e[-1][2] == "V":
                        hv = dict(intro=e[0][0] + " " + e[0][1], vanish=e[-1][0] + " " + e[-1][1], vanish_c=e[-1][0],
                                  rename_cands=[], reappear=False)
                        r["notes"].append("파일 줄기 이력 — 같은 이름의 파일이 사라짐")
                        break
        elif kind == "method":
            for nm in self._hist_candidates_method(name, pat):
                hv = self._hist(("method", nm))
                if hv:
                    if nm != name:
                        r["notes"].append("이력 부분 일치 → " + nm)
                    break
        elif kind == "cm":
            c, m = key[1], key[2]
            for mm in sorted(self.hist.cm_by_class.get(c, ())):
                if self._cm_name_ok(m, mm, pat):
                    cc = c
                    hv = self._hist(("cm", cc, mm))
                    if hv:
                        if mm != m:
                            r["notes"].append("이력 일치 → " + mm)
                        break
            if not hv and not pat:
                hv = self._hist(("type", m))
                if hv:
                    r["notes"].append("사라진 중첩 형식")
            if not hv and not pat and self._hist(("method", m)):
                hv = self._hist(("method", m))
                r["notes"].append("이력상 다른 클래스에 있던 메서드")
        if hv:
            r.update(hv)
            r["v"] = "다"
            if hv.get("reappear"):
                r["notes"].append("등장·소멸 반복")
            if hv.get("rename_cands"):
                ranked = sorted(hv["rename_cands"], key=lambda c: -difflib.SequenceMatcher(None, name, c).ratio())
                r["notes"].append("소멸 커밋에서 같은 클래스에 새로 생긴 선언(유사도순): " + " · ".join(ranked[:3]))
        else:
            short = name.split("…")[0].rstrip("_")
            rn = self.renames.get(name) or (self.renames.get(short) if not pat else None)
            rt = self._runtime(name)
            if kind == "cm" and not rt:
                toks = self.work.class_runtime_tokens.get(key[1], set())
                if key[2] in toks:
                    rt = "클래스의 TestName/SetName 런타임 이름에 이 낱말이 있다"
            if kind == "cm" and not rt and ID_RE.match(key[2]):
                files = self.work.types.get(key[1], set())
                if any(re.search(r'(?<![\w])' + re.escape(key[2]) + r'(?![\w])', self.work.raw_text.get(f, "")) for f in files):
                    rt = "사례 ID가 테스트 파일 주석에만 있다 — 어느 선언인지 불명"
            idm = ID_PREFIX_RE.match(short)
            if not rt and idm and not rn:
                same = sorted(n for n in self.work.methods if n.startswith(idm.group(1)))
                if same:
                    rt = "같은 ID 접두 선언 실재(문구 다름): " + same[0]
            if rn:
                r["v"] = "다"
                r.update(intro="(커밋 안 됨 — 러너 xml에만)", vanish="개명 대장 → %s.%s" % rn, vanish_c=None, rename_cands=[])
                r["notes"].append("renames.tsv 옛 이름")
            elif rt:
                r["v"] = "마"
                r["notes"].append(rt)
            elif pat and len(pat[1]) >= 8 and not self._fragment_anywhere(pat[1]):
                # ★ 잘린 이름(verify-change 2026-09-15): 앞 조각이 8자 이상인데 현재·이력 선언 **어디에도** 없으면 (나)다 — (마)로 두면
                #   `미해결_Windows_작업표시줄_버튼은_…`(한 번도 없던 이름) 같은 부재가 판정 불가로 흡수된다.
                #   ★ 접두만 보지 않고 **이름 안 어디에든** 있는지 본다 — `«사용자숨김_세계에서_톱니…»`처럼 실재 이름의 **가운데**를 인용한
                #   문장(`등급1_사용자숨김_세계에서_톱니를_…`)을 (나)로 오판하지 않게. 그 경우는 (마)로 남는다.
                r["v"] = "나"
                r["notes"].append("잘린 이름 — 앞 조각 「%s」이 현재·이력 선언 어디에도 없다" % pat[1])
            elif pat:
                r["v"] = "마"
                r["notes"].append("잘린 이름이 어느 선언(현재·이력)과도 일치 안 함 — 앞 조각 8자 미만이거나 조각은 어딘가에 실재")
            elif kind == "method" and self._in_production(name):
                r["v"] = "제외"
                r["notes"].append("프로덕션 선언·문자열 식별자 — 테스트 약속 아님")
                return r
            else:
                r["v"] = "나"
                if kind == "cm" and self.work.types.get(key[1]):
                    r["notes"].append("클래스는 실재, 멤버만 없음")
                if kind == "type" and name.startswith(("Zz", "ZZ")):
                    r["notes"].append("임시 탐침 이름 형태(Zz 접두)")
        token = key[2] if kind == "cm" else name
        r["evidence"] = self.pickaxe(token.split("…")[0])
        if r["v"] == "나" and r["evidence"]:
            r["notes"].append("`.cs` pickaxe %d커밋 — 주석·문자열·부분 문자열에만 등장(선언 이력 0)" % len(r["evidence"]))
        return r


def mask_identifiers(s):
    """V1 — 식별자 토큰을 같은 길이의 \\x01로 가린다(위치 보존). 이름 안의 「미구현」「사라진」이 표기로 읽히지 않게."""
    return _IDENT_TOKEN.sub(lambda m: "\x01" * len(m.group(0)), s)


_SENT_END = re.compile(r'(?:[.!?。]|다\.)(?=\s|$)')


def segment_of(masked, pos):
    """V2(약한 표기) 판정용 — 표 행이면 `|` 개수(셀 번호), 아니면 pos 앞 문장 끝 개수(문장 번호). 가린 텍스트에서 센다."""
    if masked.lstrip().startswith("|"):
        return masked.count("|", 0, pos)
    return len(_SENT_END.findall(masked, 0, pos))


_COMMIT_CACHE = {}


def commit_exists(tok):
    if tok not in _COMMIT_CACHE:
        p = subprocess.run(["git", "--no-optional-locks", "-C", REPO, "cat-file", "-e", tok + "^{commit}"],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env=_GIT_ENV)
        _COMMIT_CACHE[tok] = p.returncode == 0
    return _COMMIT_CACHE[tok]


def classify_occurrence(o, res):
    """이름 판정 + 표기 범위 → 출현 판정.
    ★ V2(같은 문장·셀 안의 표기만 인정)는 **하드 규칙이 아니다**(verify-change 실측: 뒤집힘 26건 중 옳음 12 · 틀림 14). 표기가 이름과
      다른 문장·셀에 있으면 (라)로 두되 `weak=True`(「약한 표기」)로 표시해 F절 사람 확인 목록에 올린다."""
    unit, off = o["unit"], o["off"]
    v = res["v"]
    occ = dict(o)
    occ.update(name_v=v, v=v, marker=None, marker_scope=None, marker_far=False, soft=None, weak=False)
    if v in ("나", "다"):
        masked = mask_identifiers(unit)
        for ph in MARKER_NOT_ABSENCE:
            masked = masked.replace(ph, "\0" * len(ph))
        hits = [(mk, mm.start()) for mk in MARKERS_PLAN + MARKERS_GONE for mm in re.finditer(re.escape(mk), masked)]
        hits += [(label, mm.start()) for rx, label in MARKER_REGEX for mm in rx.finditer(masked)]
        best = min(((mk, abs(p - off), p) for mk, p in hits), key=lambda t: t[1], default=None)
        if best and best[1] <= MARKER_FAR:
            occ.update(v="라", marker=best[0], marker_scope="단위", weak=segment_of(masked, best[2]) != segment_of(masked, off))
        else:
            if best:
                occ.update(marker=best[0], marker_far=best[1])
            anc = ([ca for ca in _COMMIT_ANCHOR.finditer(unit) if abs(ca.start() - off) <= MARKER_FAR and commit_exists(ca.group(1))]
                   if v == "다" else [])
            if anc:
                occ.update(v="라", marker="‹커밋› 기준", marker_scope="단위", marker_far=False,
                           weak=segment_of(masked, anc[0].start()) != segment_of(masked, off))
            else:
                header, section, head = o.get("scopes") or ("", "", "")
                for scope, txt in (("표 머리", header), ("절", section), ("문서 머리", head)):
                    txt = mask_identifiers(txt)
                    hit = next((mk for mk in WIDE_SCOPE_MARKERS if mk in txt), None)
                    if hit:
                        occ.update(v="라", marker=hit, marker_scope=scope, marker_far=False)
                        break
        if occ["v"] != "라":
            for sk in SOFT_MARKERS:
                if sk in unit[max(0, off - MARKER_FAR):off + MARKER_FAR]:
                    occ["soft"] = sk
                    break
    window = unit if len(unit) <= 800 else unit[max(0, off - SEVERITY_WINDOW):off + SEVERITY_WINDOW]
    sev = []
    for kw, cat in SEVERITY_HIGH:
        if kw in window and cat not in sev:
            sev.append(cat)
    for pp, cat in SEVERITY_HIGH_PATHS:
        if pp in o["file"] and cat not in sev:
            sev.append(cat)
    occ["severity"] = "높음" if sev else "낮음"
    occ["sev_cats"] = sev
    return occ


# =============================================================================
# 요지·누출 가드
# =============================================================================
_SECRETS = None


def _secrets():
    global _SECRETS
    if _SECRETS is None:
        try:
            user = subprocess.run(["id", "-un"], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL).stdout.decode().strip()
        except OSError:
            user = ""
        _SECRETS = (user, os.path.expanduser("~"))
    return _SECRETS


def redact(s):
    user, home = _secrets()
    if REPO:
        s = s.replace(REPO, "<프로젝트 경로>")
    if home and home != "/":
        s = s.replace(home, "~")
    if user and len(user) >= 3:
        s = re.sub(r'(?<![\w])' + re.escape(user) + r'(?![\w])', "<사용자>", s)
    return s


def leak_check(text):
    user, home = _secrets()
    bad = []
    if home and home != "/" and home in text:
        bad.append("홈 경로")
    if user and len(user) >= 3 and re.search(r'(?<![\w])' + re.escape(user) + r'(?![\w])', text):
        bad.append("사용자명")
    if REPO in text:
        bad.append("저장소 절대경로")
    return bad


def gist(o, width=110):
    unit, off = o["unit"], o["off"]
    s = unit[max(0, off - 45):off + width - 45]
    s = re.sub(r'\s+', " ", s).replace("|", "¦").replace("`", "'").strip()
    return redact(s)


# =============================================================================
# 스캔 본체
# =============================================================================
def build_indexes(tests):
    work = Index()
    for p in tests:
        work.add(p, read(p))
    head = Index()
    shas = {}
    for ent in run_git(["-c", "core.quotepath=off", "ls-tree", "-r", "-z", "HEAD", "--", "Assets"]).split(b"\0"):
        if not ent:
            continue
        meta, path = ent.split(b"\t", 1)
        path = nfc(path.decode())
        if is_test_cs(path):
            shas[path] = meta.split()[2].decode()
    texts = cat_blobs(shas.values())
    for p, sha in shas.items():
        head.add(p, texts.get(sha, ""))
    prod = Index()
    for p in sorted(_glob_rel(["Assets/**/*.cs"])):
        if not is_test_cs(p):
            prod.add(p, read(p))
    return work, head, prod


def classify_all(occs, resolver):
    resolver.prepare_history(occs)
    out = []
    for o in occs:
        res = resolver.resolve(o["key"], o.get("pat"))
        occ = classify_occurrence(o, res)
        occ["res"] = res
        out.append(occ)
    return out


def scan_docs(doc_texts, resolver):
    occs, wildcard = [], 0
    for rel, text in doc_texts:
        o, w = extract_promises(rel, text)
        occs += o
        wildcard += w
    return classify_all(occs, resolver), wildcard


def snapshot_rows(classified, doc_texts):
    """(분류, 이름, 파일, 앵커, 파일 내 앵커 총수, 그 분류 출현 수) — (가)·제외는 뺀다."""
    texts = dict(doc_texts)
    grp = collections.Counter()
    for o in classified:
        if o["v"] in ("가", "제외"):
            continue
        grp[(o["v"] + ("약" if o.get("weak") else ""), "/".join(o["key"][1:]), o["file"], o["anchor"])] += 1
    return [(v, name, f, anchor, texts[f].count(anchor), cnt) for (v, name, f, anchor), cnt in sorted(grp.items())]


def counts_of(classified):
    c = collections.Counter(o["v"] for o in classified)
    return {k: c.get(k, 0) for k in ("가", "나", "다", "라", "마", "제외")}


SNAP_BEGIN = "<!-- PROMISED-SCAN-SNAPSHOT v1"
SNAP_END = "PROMISED-SCAN-SNAPSHOT-END -->"


def render_snapshot(rows, counts):
    lines = [SNAP_BEGIN, "counts\t" + "\t".join("%s=%d" % (k, counts[k]) for k in ("가", "나", "다", "라", "마", "제외"))]
    for r in rows:
        lines.append("row\t" + "\t".join(str(x) for x in r))
    lines.append(SNAP_END)
    return "\n".join(lines)


def parse_snapshot(text):
    s, e = text.find(SNAP_BEGIN), text.find(SNAP_END)
    if s < 0 or e < 0:
        return None, None
    counts, rows = None, []
    for line in text[s:e].split("\n")[1:]:
        f = line.split("\t")
        if f[0] == "counts":
            counts = {x.split("=")[0]: int(x.split("=")[1]) for x in f[1:]}
        elif f[0] == "row" and len(f) == 7:
            rows.append((f[1], f[2], f[3], f[4], int(f[5]), int(f[6])))
    return rows, counts


def freshness_diff(snap_rows, snap_counts, rows, counts):
    if snap_rows is None:
        return ["스냅숏 블록 없음"]
    diffs = []
    a, b = set(snap_rows), set(rows)
    for r in sorted(a - b):
        diffs.append("스냅숏에만: " + " | ".join(map(str, r)))
    for r in sorted(b - a):
        diffs.append("지금 재계산에만: " + " | ".join(map(str, r)))
    if snap_counts != counts:
        diffs.append("분류 개수: 스냅숏 %s vs 지금 %s" % (snap_counts, counts))
    return diffs


# =============================================================================
# 교정
# =============================================================================
CALIB_COMMIT = "ad49497"  # 숨김 탐침 실데이터 교정의 기준 커밋(묶음 ⑦ — 작업 트리는 다른 라운드가 고치는 중이라 기대가 흔들린다)

REAL_SAMPLES = (
    ("type", "AudioReactiveDanceGateAuditTests"),
    ("type", "UserAssetImmutabilityAuditTests"),
    ("type", "PlatformParityAuditTests"),
    ("type", "IncomeTimeSourceAuditTests"),
    ("cm", "AudioReactiveDanceGateAuditTests", "집중_모드_판정은_상태ID가_아니라_세션_플래그를_읽는다"),
    ("method", "숨김_판정은_기존_술어를_재사용하고_다시_구현하지_않는다"),
    ("path", "Tests/EditMode/PlatformParityAuditTests.cs"),
    ("file", "AccessoryHatBandAndBellTests.NetInk.cs"),
    ("cm", "AccessoryRuleOneCoverageTests", "Waivers"),
    ("cm", "LineRendererUvBandProbeTests", "T2"),
)

SYNTH_CS = r'''
using NUnit.Framework;
namespace Zz.Synth {
    // class ZzCommentOnlyTests { [Test] public void Zz_주석_안_이름() {} }
    /* public sealed class ZzBlockCommentTests { public void Zz_블록_주석_이름() {} } */
    /// <summary><c>ZzDocCommentTests</c> 는 문서 주석에만 있다</summary>
    public sealed class ZzRealSynthTests {
        private const string S1 = "class ZzStringOnlyTests { public void Zz_문자열_안_이름() {} }";
        private const string S2 = @"verbatim ""quoted"" // not a comment { ";
        private static string S3(int x) => $"{(x > 0 ? "}" : "{")} // not a comment either {{";
        private const char C1 = '"';
        private const char C2 = '\'';
        public static readonly string[] ZzWaivers = { };
        private int ZzProp { get; set; }
        [Test] public void Zz_진짜_선언() { int zzLocal = 0; zzLocal++; var q = S1 + S2 + C1 + C2; Assert.Pass(q); }
        [TestCase(1, TestName = "Zz_런타임_조립_이름")] public void Zz_파라미터(int v) { }
        [UnityEngine.TestTools.UnityTest] public System.Collections.IEnumerator Zz_유니티_선언() { yield return null; }
        public void Zz_람다아님() { System.Action a = () => Zz_호출만(); a(); }
        [Test] public void T3z_합성_ID_선언() { }
        [Test] public void Q7_Q8_합성_중간_생략_끝() { }
        [Test] public void Zz_합성_부분_일치_긴_이름_꼬리() { }
        void Zz_호출만() {}
    }
}
'''

SYNTH_CS_STEM = r'''
namespace Zz.Synth { public sealed class ZzOtherNameTests { [NUnit.Framework.Test] public void Zz_줄기_파일_안_선언() { } } }
'''

SYNTH_DOC = """# 합성 문서

- `ZzNoSuchPromiseTests` 가 원칙 3을 지킨다고 적는다.

| 1 | `ZzPlannedPromiseTests` ★ 명세만 있고 테스트 없음 | 세이브 |

- `ZzRealSynthTests.Zz_진짜_선언` 과 `ZzCommentOnlyTests` · `ZzStringOnlyTests` · `ZzDocCommentTests` · `ZzBlockCommentTests`
- `Zz_주석_안_이름` 과 `Zz_문자열_안_이름` 과 `Zz_런타임_조립_이름`
- `ZzRealSynthTests.ZzWaivers` · `ZzRealSynthTests.ZzProp` · `ZzRealSynthTests.zzLocal` · `ZzRealSynthTests.T3z` · `ZzRealSynthTests.Q7_Q8_…중간_생략_끝`
- 파일 ZzRealSynthTests.cs가 있다 · `Zz{RealSynth,NoSuchBrace}Tests` · `합성_부분_일치_긴_이름` · `Zz_진짜_선언은`
- `-runTests` 와 `ResetForTests()` 와 `SM_ConfigureOverlayWindow` 와 `ZzAscendCancelWhenWall_GoesToFall`
- `Tests/EditMode/*ZzWildAuditTests.cs` 는 패턴 · `1_수정전_합성=Fallen.png` · 환경변수 ZZ_ENV_VAR로 · 기울임 _합성_강조_ 이다
- `AudioActivationPolicyTests` 는 여전히 인용된다
- `CornerHoverPanelTests` (`2051739` 기준 인용)
- `ZzAbsentMarkTests` 부재 · 틀 `v10_<묶음>이_합성_템플릿_꼬리` 도 있다
- `Zz_합성_기능이_미구현이다` 는 이름 안에 표기어가 있다
- `SizeDialWidgetHitTestTests` 를 인용한다. 무관한 다음 문장에서 설정 값을 삭제했다.
- `BattleRetryDialogueSyncTests` 는 `20260915` 기준 기록이다
- `ZzStemFileTests` 는 파일 줄기로만 있다 · `ZzRealSynthTests.Zz_아무데도_없는_앞조각…꼬리` · `ZzRealSynthTests.Zz…없는꼬리`

## 합성 절 — 이름 제안이다

- 설명 한 줄
- `Zz_절_제안_이름_하나`

## 합성 미구현 기능 절

- `Zz_기능절_아래_이름_하나`
"""

SYNTH_DOC_HEAD = "# 합성 명세\n\n작성: 합성 · **명세만 있다 — `.cs` 0줄**\n" + "\n".join("채움 %d" % i for i in range(30)) + \
    "\n\n## 멀리\n\n" + "\n".join("줄 %d" % i for i in range(90)) + "\n\n- `Zz_머리_표기_이름_하나`\n"


def _fixture_names():
    """스캐너 자신의 합성 교정 이름 — 보고 요약(Tasklist 등)이 이 이름을 실데이터에 되먹이면 제외한다(verify-change 2026-09-15 적발).
    ★ 정확히 이 이름들만 뺀다 — 실데이터의 다른 Zz 이름(`ZzVc5ReentrancyProbeTests` 등)은 그대로 판정한다(교정 C4 음성)."""
    text = SYNTH_CS + SYNTH_CS_STEM + SYNTH_DOC + SYNTH_DOC_HEAD
    names = set(re.findall(r'(?<![\w가-힣])Zz[\w가-힣]+', text)) | set(re.findall(r'[\w가-힣]*합성[\w가-힣]*', text))
    names |= {"ZzNoSuchBraceTests", "ZzNoSuchPromiseRenamedTests", "ZzFarTests", "Q7_Q8_합성_중간_생략_끝", "T3z_합성_ID_선언"}
    return frozenset(n for n in names if "_" in n or n.endswith("Tests"))


SYNTH_FIXTURE_NAMES = _fixture_names()


def calibrate(work, head, hist, prod, renames, doc_texts, input_problems, classified):
    fails, oks = [], []

    def ok(cond, label):
        (oks if cond else fails).append(label)

    fails += input_problems
    ok(not input_problems, "C6 입력 대조: glob = git ls-files(+미추적 −삭제) 문서·테스트")
    ok(len(doc_texts) >= 100 and any(p == "CLAUDE.md" for p, _ in doc_texts)
       and any(p.startswith(".claude/agents/") for p, _ in doc_texts) and any(p == "Tasklist.md" for p, _ in doc_texts),
       "C6 문서 집합 비공허(≥100 · CLAUDE.md · Tasklist.md · .claude/agents 포함) — 실제 %d" % len(doc_texts))
    ok(len(work.file_types) >= 400, "C6 테스트 파일 해석 수 ≥400 — 실제 %d" % len(work.file_types))
    ok(len(prod.file_types) >= 200, "C6 프로덕션 파일 해석 수 ≥200 — 실제 %d (실패 %d)" % (len(prod.file_types), len(prod.lex_failures)))
    ok(len(renames) >= 5, "C6 개명 대장 로드 ≥5 — 실제 %d" % len(renames))
    ok(not work.lex_failures, "C5 작업 트리 테스트 .cs 해석 실패 0 — 실제 %s" % work.lex_failures[:3])
    ok(not hist.lex_failures, "C5 이력 blob 해석 실패 0 — 실제 %d %s" % (len(hist.lex_failures), hist.lex_failures[:2]))
    ok(len(hist.blob_keys) >= 900, "C5 이력 blob 해석 수 ≥900(09-15 첫 실측 1000에 여유 — 문턱이 현재값과 같으면 1개만 줄어도 빨갛다) — 실제 %d" % len(hist.blob_keys))
    n_attr = sum(1 for ms in work.methods.values() for (f, ch, a, l) in ms if a & {"Test", "UnityTest", "TestCase", "TestCaseSource"})
    ok(n_attr >= 3000, "C5 [Test]류 어트리뷰트 달린 메서드 선언 ≥3000 — 실제 %d" % n_attr)
    ok((len(MARKERS_PLAN), len(MARKERS_GONE)) == EXPECTED_MARKER_COUNTS and len(SOFT_MARKERS) == EXPECTED_SOFT_COUNT
       and (len(SEVERITY_HIGH), len(SEVERITY_HIGH_PATHS)) == EXPECTED_SEVERITY_COUNTS
       and len(WIDE_SCOPE_MARKERS) == EXPECTED_WIDE_COUNT and len(MARKER_NOT_ABSENCE) == EXPECTED_NOT_ABSENCE_COUNT
       and len(MARKER_REGEX) == EXPECTED_MARKER_REGEX_COUNT and len(SYNTH_FIXTURE_NAMES) >= 20,
       "C7 사전 개수 = 명시 기대값 (단위 %d+%d · 넓은 범위 %d · 비표기 복합어 %d · 사전밖 %d · 심각도 %d+%d)" % (
           len(MARKERS_PLAN), len(MARKERS_GONE), len(WIDE_SCOPE_MARKERS), len(MARKER_NOT_ABSENCE), len(SOFT_MARKERS),
           len(SEVERITY_HIGH), len(SEVERITY_HIGH_PATHS)))

    real = Resolver(work, head, hist, prod, renames)
    probe_occs = [dict(key=k, pat=None) for k in REAL_SAMPLES] + [dict(key=("type", n), pat=None) for n in
                  ("WallClockReadScopeAuditTests", "AudioActivationPolicyTests", "CornerHoverPanelTests")] + \
                 [dict(key=("cm", "CardShapeContractTests", "열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다"), pat=None),
                  dict(key=("cm", "FullscreenPanelRetreatTests", "등급1에서_톱니를_누르면_설정창까지_도달한다"), pat=None),
                  dict(key=("type", "PopoverAndHoverPanelOpacityTests"), pat=None)]
    # 숨김 탐침 실데이터 — **커밋된 판**(CALIB_COMMIT)을 읽는다
    blob_occ = {}
    for path, anchor in (("docs/GAME_ARCHITECTURE_REVIEW.md", "미해결_오디오_네이티브_개폐가_양_플랫폼_모두_미구현이다"),
                         ("docs/TASKBAR_REVEAL.md", "R7_정상_종료_기록은_실제로_쓴_뒤에만_서고_기동이_되돌린다"),
                         ("docs/manual/03-disappeared.md", "FullscreenPanelRetreatTests.등급1에서_톱니를_누르면_설정창까지_도달한다")):
        txt = nfc(run_git(["show", "%s:%s" % (CALIB_COMMIT, path)]).decode("utf-8", "replace"))
        oc, _ = extract_promises(path + "@" + CALIB_COMMIT, txt)
        blob_occ[path] = [o for o in oc if o["anchor"] == anchor]
        probe_occs += blob_occ[path]
    real.prepare_history(probe_occs)
    # C1
    for k in REAL_SAMPLES:
        r = real.resolve(k)
        ok(r["v"] == "가", "C1 실재 표본 %s → (가) — 실제 %s %s" % ("/".join(k[1:])[:60], r["v"], r["notes"]))
    # C2
    r = real.resolve(("type", "WallClockReadScopeAuditTests"))
    ok(r["v"] == "나", "C2 WallClockReadScopeAuditTests → 이름 (나) — 실제 %s" % r["v"])
    pk = real.pickaxe("WallClockReadScopeAuditTests")
    ok(pk == [], "C2 WallClockReadScopeAuditTests `.cs` pickaxe 0커밋 — 실제 %d" % len(pk))
    pk_docs = real.pickaxe("WallClockReadScopeAuditTests", "docs/security/SECURITY_MODEL.md")
    ok(len(pk_docs) >= 1, "C2 양성: 같은 이름 문서 pickaxe ≥1커밋(프로브 생존) — 실제 %d" % len(pk_docs))
    old = nfc(run_git(["show", "1f7e139:docs/security/SECURITY_MODEL.md"]).decode("utf-8", "replace"))
    old_occ, _ = extract_promises("docs/security/SECURITY_MODEL.md@1f7e139", old)
    old_rows = [classify_occurrence(o, real.resolve(o["key"])) for o in old_occ if o["key"] == ("type", "WallClockReadScopeAuditTests")]
    ok(len(old_rows) == 2 and all(o["v"] == "나" for o in old_rows),
       "C2 1f7e139판 표 행 2곳(표기 없음) → 출현 (나) — 실제 %s" % [(o["line"], o["v"], o["marker"], o["marker_scope"]) for o in old_rows])
    far = [o for o in old_rows if o["line"] == 997]
    ok(len(far) == 1 and far[0]["marker"] is None,
       "C2 1f7e139판 997행의 「부재 단언」(354자 뒤)은 표기로 세지 않음 — 실제 %s" % [(o["marker"], o["marker_far"]) for o in far])
    far_probe = classify_occurrence(dict(file="x.md", unit="`ZzFarTests` " + "가" * (MARKER_FAR + 5) + " 명세만", off=0), dict(v="나"))
    ok(far_probe["v"] == "나" and far_probe["marker_far"] and far_probe["marker"] == "명세만",
       "C2 먼 표기(%d자 밖 「명세만」) → (라) 아님 · 먼 표기 비고" % MARKER_FAR)
    head_sec = [o for o in classified if o["file"] == "docs/security/SECURITY_MODEL.md" and o["key"] == ("type", "WallClockReadScopeAuditTests")]
    rows_1045 = [o for o in head_sec if o["unit"].lstrip().startswith("| 1 |")]
    ok(len(rows_1045) == 2 and all(o["v"] == "라" and o["marker"] == "명세만" for o in rows_1045),
       "C2 HEAD판 표 행 2곳(「명세만」) → 출현 (라) — 실제 %s" % [(o["line"], o["v"], o["marker"]) for o in rows_1045])
    # C3
    r = real.resolve(("type", "AudioActivationPolicyTests"))
    ok(r["v"] == "다" and r.get("intro", "").startswith("1eb0e2b") and r.get("vanish", "").startswith("1f7e139"),
       "C3 AudioActivationPolicyTests(`git log --diff-filter=D`) → (다) 1eb0e2b→1f7e139 — 실제 %s %s→%s" % (r["v"], r.get("intro"), r.get("vanish")))
    r = real.resolve(("type", "CornerHoverPanelTests"))
    raw_hits = [p for p, t in work.raw_text.items() if "CornerHoverPanelTests" in t]
    ok(len(raw_hits) >= 2, "C3 양성: CornerHoverPanelTests 가 HEAD 테스트 원문(주석)에 실재 ≥2파일 — 실제 %d" % len(raw_hits))
    ok(r["v"] == "다" and r.get("vanish", "").startswith("2051739"),
       "C3 CornerHoverPanelTests(주석에만 남음) → (다) …→2051739 — 실제 %s %s" % (r["v"], r.get("vanish")))
    gar = [o for o in classified if o["file"] == "docs/GAME_ARCHITECTURE_REVIEW.md" and "AudioActivationPolicyTests" in o["anchor"]]
    ok(len(gar) >= 1 and all(o["name_v"] == "다" for o in gar),
       "C3 GAME_ARCHITECTURE_REVIEW 인용 → 이름 (다) — 실제 %s" % [(o["line"], o["name_v"], o["v"], o["marker"]) for o in gar])
    r = real.resolve(("cm", "FullscreenPanelRetreatTests", "등급1에서_톱니를_누르면_설정창까지_도달한다"))
    ok(r["v"] == "다" and r.get("vanish", "").startswith("eb4670d")
       and any("등급1_사용자숨김_세계에서_톱니를_누르면_설정창까지_도달한다" in n for n in r["notes"]),
       "C3 개명 소멸 → (다) eb4670d + 개명 후보에 새 이름 — 실제 %s %s %s" % (r["v"], r.get("vanish"), r["notes"]))
    r = real.resolve(("cm", "CardShapeContractTests", "열세_종은_몸과_카드가_같은_목록이고_망토_둘만_갈린다"))
    ok(r["v"] == "다" and "개명 대장" in r.get("vanish", ""),
       "C3 커밋 안 된 중간 이름(renames.tsv) → (다) 개명 대장 — 실제 %s %s" % (r["v"], r.get("vanish")))
    r = real.resolve(("type", "PopoverAndHoverPanelOpacityTests"))
    ok(r["v"] == "가" and any("파일 줄기" in n for n in r["notes"]),
       "C1 파일 줄기만 실재(`PopoverAndHoverPanelOpacityTests.cs` 안 형식명 다름) → (가) — 실제 %s %s" % (r["v"], r["notes"]))

    def blob_cls(path):
        return [classify_occurrence(o, real.resolve(o["key"], o.get("pat"))) for o in blob_occ[path]]

    g = blob_cls("docs/GAME_ARCHITECTURE_REVIEW.md")
    ok(len(g) >= 1 and all(o["v"] == "다" for o in g),
       "C4 숨김 탐침 실데이터(%s) — 이름 속 「미구현」 GAR `미해결_오디오_…_미구현이다` → (다) — 실제 %s" % (
           CALIB_COMMIT, [(o["line"], o["name_v"], o["v"], o["marker"]) for o in g]))
    t = blob_cls("docs/TASKBAR_REVEAL.md")
    ok(len(t) >= 1 and all(o["v"] == "라" and o["marker"] in ("정정", "N차 이름") for o in t),
       "C4 V1 뒤 실데이터(%s) — TASKBAR_REVEAL 「5차 이름 `…`에서 … 정정」 → (라) 정정/N차 이름 — 실제 %s" % (
           CALIB_COMMIT, [(o["line"], o["name_v"], o["v"], o["marker"]) for o in t]))
    m3 = blob_cls("docs/manual/03-disappeared.md")
    ok(len(m3) >= 1 and not any(o["v"] == "라" and o["marker_scope"] == "절" for o in m3),
       "C4 숨김 탐침 실데이터(%s) — 매뉴얼 03 사라진 테스트가 절 기능 낱말로 (라) 아님 — 실제 %s" % (
           CALIB_COMMIT, [(o["line"], o["name_v"], o["v"], o["marker"], o["marker_scope"]) for o in m3]))
    # 넓은 범위 표기 — 실문서
    tk = [o for o in classified if o["file"] == "docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md" and o["anchor"] == "레거시_v1_닫힌_흔적은_시스템에_쓰지_않는다"]
    ok(len(tk) >= 1 and all(o["v"] == "라" for o in tk),
       "C2' 문서 머리 「명세만」 문서의 사례 이름 → (라) — 실제 %s" % [(o["line"], o["name_v"], o["v"], o["marker_scope"]) for o in tk])
    f28 = [o for o in classified if o["file"] == "docs/UX_RIGHTCLICK_FAN_MENU.md" and o["anchor"].startswith("F28_")]
    ok(len(f28) >= 1 and all(o["v"] == "라" and o["marker_scope"] in ("절", "단위") for o in f28),
       "C2' 절 머리 「이름 제안」 아래 사례 이름 → (라) — 실제 %s" % [(o["line"], o["name_v"], o["v"], o["marker_scope"]) for o in f28])
    rt = [o for o in classified if o["anchor"] == "runTests"]
    ok(not rt, "C4' `-runTests` 플래그를 테스트 클래스로 뽑지 않음 — 실제 %d" % len(rt))
    # C4 합성
    sidx = Index()
    sidx.add("Assets/Zz/Tests/EditMode/ZzRealSynthTests.cs", SYNTH_CS)
    sidx.add("Assets/Zz/Tests/EditMode/ZzStemFileTests.cs", SYNTH_CS_STEM)
    ok(not sidx.lex_failures, "C4 합성 .cs 해석(까다로운 리터럴 포함) — 실제 %s" % sidx.lex_failures)
    sres = Resolver(sidx, Index(), hist, Index(), {})

    def synth(doc, name="synthetic.md"):
        oc, _ = extract_promises(name, doc)
        return classify_all(oc, sres), oc

    got_list, socc = synth(SYNTH_DOC)
    got = collections.defaultdict(list)
    for o in got_list:
        got[o["anchor"]].append(o)
    exp = {
        "ZzNoSuchPromiseTests": ("나", "나"), "ZzPlannedPromiseTests": ("나", "라"),
        "ZzRealSynthTests.Zz_진짜_선언": ("가", "가"),
        "ZzCommentOnlyTests": ("나", "나"), "ZzStringOnlyTests": ("나", "나"),
        "ZzDocCommentTests": ("나", "나"), "ZzBlockCommentTests": ("나", "나"),
        "Zz_주석_안_이름": ("나", "나"), "Zz_문자열_안_이름": ("나", "나"),
        "Zz_런타임_조립_이름": ("마", "마"),
        "ZzRealSynthTests.ZzWaivers": ("가", "가"), "ZzRealSynthTests.ZzProp": ("가", "가"),
        "ZzRealSynthTests.zzLocal": ("나", "나"), "ZzRealSynthTests.T3z": ("가", "가"),
        "ZzRealSynthTests.Q7_Q8_…중간_생략_끝": ("가", "가"),
        "합성_부분_일치_긴_이름": ("가", "가"), "Zz_진짜_선언은": ("가", "가"),
        "ResetForTests": ("제외", "제외"), "SM_ConfigureOverlayWindow": ("제외", "제외"),
        "ZzAscendCancelWhenWall_GoesToFall": ("나", "나"),
        "Zz_절_제안_이름_하나": ("나", "라"),
        "AudioActivationPolicyTests": ("다", "다"),
        "CornerHoverPanelTests": ("다", "라"),
        "ZzAbsentMarkTests": ("나", "라"),
        "이_합성_템플릿_꼬리": ("마", "마"),
        "Zz_합성_기능이_미구현이다": ("나", "나"),              # 숨김 탐침 ① 이름 속 표기어
        "BattleRetryDialogueSyncTests": ("다", "다"),          # 숨김 탐침 ③ 8자리 날짜 `20260915` 기준 — 커밋 앵커 아님
        "Zz_기능절_아래_이름_하나": ("나", "나"),               # 숨김 탐침 ④ 절 제목 기능 낱말 「미구현」
        "ZzStemFileTests": ("가", "가"),                       # 파일 줄기 대조
        "ZzRealSynthTests.Zz_아무데도_없는_앞조각…꼬리": ("나", "나"),  # 잘린 이름 — 앞 조각 8자 이상이 어디에도 없음
        "ZzRealSynthTests.Zz…없는꼬리": ("마", "마"),           # 잘린 이름 — 앞 조각 8자 미만(첫 판 `Zz…꼬리`는 합성 선언 `Zz_합성_…_꼬리`와 실제로 맞아 (가)였다 — 표본 실수)
    }
    for junk in ("ZzWildAuditTests", "1_수정전_합성", "ZZ_ENV_VAR로", "_합성_강조_"):
        ok(not any(junk in g["anchor"] for g in got_list), "C4 합성 비약속 토큰 미추출: %s" % junk)
    for anchor, (nv, ov) in exp.items():
        gl = got.get(anchor, [])
        cond = len(gl) >= 1 and all(g["name_v"] == nv and (ov is None or g["v"] == ov) for g in gl)
        ok(cond, "C4 합성 %s → 이름 (%s)%s — 실제 %s" % (anchor, nv, "" if ov is None else " 출현 (%s)" % ov,
                                                    [(g["name_v"], g["v"], g["marker_scope"]) for g in gl]))
    br = sorted(g["name_v"] for g in got.get("Zz{RealSynth,NoSuchBrace}Tests", []))
    ok(br == ["가", "나"], "C4 합성 중괄호 전개 Zz{RealSynth,NoSuchBrace}Tests → (가)+(나) — 실제 %s" % br)
    csg = [g for g in got_list if g["key"] == ("type", "ZzRealSynthTests") and g["kind"] == "class"]
    ok(any(g["name_v"] == "가" for g in csg) and not any(g["key"][0] == "cm" and g["key"][2].startswith("cs") for g in got_list),
       "C4 합성 `ZzRealSynthTests.cs가` → 클래스 (가), 메서드 「cs가」로 뽑지 않음")
    ok(not got.get("runTests"), "C4 합성 `-runTests` 미추출")
    sz = got.get("SizeDialWidgetHitTestTests", [])
    ok(len(sz) == 1 and sz[0]["name_v"] == "다" and sz[0]["v"] == "라" and sz[0]["weak"],
       "C4 숨김 탐침 ② 300자 안 무관한 「삭제」(다른 문장) → (라)이되 **약한 표기**로 F절 사람 확인 목록(V2는 하드 규칙 아님) — 실제 %s" % (
           [(o["name_v"], o["v"], o["marker"], o["weak"]) for o in sz]))
    ok(not commit_exists("20260915") and not commit_exists("2746944") and commit_exists("2051739"),
       "C4 커밋 앵커 실재 확인 — 날짜 `20260915`·비객체 `2746944` 거부 / 숫자만인 실재 해시 `2051739` 인정")
    exr = Resolver(sidx, Index(), hist, Index(), {}, pickaxe=False, exclude_names=SYNTH_FIXTURE_NAMES)
    ok(exr.resolve(("type", "ZzNoSuchPromiseTests"))["v"] == "제외" and exr.resolve(("type", "ZzVc5ReentrancyProbeTests"))["v"] != "제외",
       "C4 합성 교정 이름 제외 규칙 — 양성 `ZzNoSuchPromiseTests` 제외 · 음성 실데이터 Zz 이름 `ZzVc5ReentrancyProbeTests`는 제외 아님")
    fx = [o for o in classified if any(p.split("…")[0] in SYNTH_FIXTURE_NAMES for p in o["key"][1:])]
    ok(all(o["v"] == "제외" for o in fx), "C4 실문서에 되먹인 합성 교정 이름 %d출현 → 전부 제외" % len(fx))
    ok(got["ZzNoSuchPromiseTests"][0]["severity"] == "높음" and got["ZzPlannedPromiseTests"][0]["severity"] == "높음",
       "C4 합성 심각도: 원칙 3 · 세이브 문맥 → 높음")
    hd, _ = synth(SYNTH_DOC_HEAD, "synthetic_head.md")
    hd = [o for o in hd if o["anchor"] == "Zz_머리_표기_이름_하나"]
    ok(len(hd) == 1 and hd[0]["v"] == "라" and hd[0]["marker_scope"] == "문서 머리",
       "C4 합성 문서 머리 「명세만」(120줄 아래 이름) → (라) 문서 머리 — 실제 %s" % [(o["v"], o["marker_scope"]) for o in hd])
    for nm in ("Zz_유니티_선언", "Zz_파라미터", "Zz_람다아님", "Zz_호출만"):
        ok(bool(sidx.methods.get(nm)), "C5 합성 선언 %s 추출" % nm)
    ok(not any(n in sidx.types for n in ("ZzCommentOnlyTests", "ZzStringOnlyTests", "ZzDocCommentTests", "ZzBlockCommentTests")),
       "C5 합성 주석·문자열 안 class 선언 0")
    ok(len(sidx.methods.get("Zz_호출만", [])) == 1, "C5 합성 호출(`=> Zz_호출만()`)을 선언으로 세지 않음 — 실제 선언 %d" % len(sidx.methods.get("Zz_호출만", [])))
    ok(("ZzRealSynthTests", "zzLocal") not in sidx.cm, "C5 합성 메서드 안 지역 변수를 멤버로 세지 않음")
    # C8 스냅숏 변이 3종
    base_rows, base_counts = snapshot_rows(got_list, [("synthetic.md", SYNTH_DOC)]), counts_of(got_list)

    def rerun(doc):
        cl, _ = synth(doc)
        return snapshot_rows(cl, [("synthetic.md", doc)]), counts_of(cl)

    m1 = rerun("\n\n앞에 끼운 줄\n\n" + SYNTH_DOC)
    ok(m1 == (base_rows, base_counts), "C8① 줄만 밀림 → 스냅숏 행 불변")
    m2 = rerun(SYNTH_DOC.replace("ZzNoSuchPromiseTests", "ZzNoSuchPromiseRenamedTests"))
    ok(bool(freshness_diff(base_rows, base_counts, *m2)), "C8② 앵커 문자열 변경 → 신선도 가드 불일치(rc=2)")
    m3 = rerun("\n".join(l for l in SYNTH_DOC.split("\n") if "ZzPlannedPromiseTests" not in l))
    ok(m3[1]["라"] == base_counts["라"] - 1 and all(m3[1][k] == base_counts[k] for k in base_counts if k != "라")
       and bool(freshness_diff(base_rows, base_counts, *m3)),
       "C8③ 항목 삭제 → (라) 정확히 -1 · 나머지 불변 · 가드 불일치 — 실제 %s→%s" % (base_counts["라"], m3[1]["라"]))
    user, home = _secrets()
    probe = "x " + home + "/y"
    ok(bool(leak_check(probe)) and not leak_check(redact(probe)), "C9 누출 가드 양성 대조(홈 경로를 잡고, 가린 뒤 통과)")
    return oks, fails


# =============================================================================
# 출력
# =============================================================================
def fmt_occ(o):
    r = o["res"]
    parts = ["%s:%d" % (o["file"], o["line"]), "[%s]" % o["kind"], o["anchor"]]
    if r.get("intro") or r.get("vanish"):
        parts.append("도입 %s → 소멸 %s" % (r.get("intro"), r.get("vanish")))
    parts.append("pickaxe(.cs) %d" % len(r.get("evidence", [])))
    if o["v"] == "라":
        parts.append("표기「%s」(%s)" % (o["marker"], o["marker_scope"]))
    elif o["marker_far"]:
        parts.append("먼 표기「%s」%d자 밖(불인정)" % (o["marker"], o["marker_far"]))
    if o["soft"]:
        parts.append("사전밖후보「%s」" % o["soft"])
    parts.append("심각도 %s%s" % (o["severity"], "(" + ",".join(o["sev_cats"]) + ")" if o["sev_cats"] else ""))
    if r["notes"]:
        parts.append("; ".join(r["notes"]))
    return " | ".join(parts)


def false_statement_label(o):
    """(다) 미표기 출현 줄의 git blame → 소멸 커밋과의 선후."""
    r = o["res"]
    raw = run_git(["blame", "--porcelain", "-L", "%d,%d" % (o["line"], o["line"]), "--", o["file"]], check=False).decode("utf-8", "replace")
    first = raw.split("\n", 1)[0].split()
    if not first:
        return "blame 불가(미추적 파일)"
    bc = first[0]
    if set(bc) == {"0"}:
        return "미커밋 줄(작업 트리)"
    vc = r.get("vanish_c")
    if not vc:
        return "줄 %s · 소멸 커밋 불명" % bc[:7]
    after = subprocess.run(["git", "--no-optional-locks", "-C", REPO, "merge-base", "--is-ancestor", vc, bc],
                           env=_GIT_ENV).returncode == 0
    return ("소멸(%s) 뒤에 쓰거나 고친 줄 %s" if after else "소멸(%s) 전에 쓴 줄 %s — 방치") % (vc, bc[:7])


def render_markdown(classified, counts, names, wildcard, rows, docs, tests, hist, oks):
    L = []

    def table(items, cols):
        L.append("| " + " | ".join(cols) + " |")
        L.append("|" + "---|" * len(cols))
        for it in items:
            L.append("| " + " | ".join(str(x).replace("|", "¦").replace("\n", " ") for x in it) + " |")

    def ev(o):
        e = o["res"].get("evidence", [])
        return "`.cs` %d커밋" % len(e) + (" (%s)" % ", ".join(x.split()[0] for x in e[:3]) if e else "")

    def note_of(o):
        parts = list(o["res"]["notes"])
        if o["marker_far"]:
            parts.append("먼 표기「%s」%d자 밖(불인정)" % (o["marker"], o["marker_far"]))
        if o["soft"]:
            parts.append("사전 밖 표기 후보「%s」" % o["soft"])
        if o.get("volatile"):
            parts.append("작업 트리 기준, 변동 중")
        return "; ".join(parts)

    L.append("## A. 교정 결과 — %d/%d 통과\n" % (len(oks), len(oks)))
    for s in oks:
        L.append("- ✓ " + s)
    L.append("\n## B. 분류 개수\n")
    L.append("- 대상: 문서 %d · 테스트 `.cs` %d · 이력 blob %d · HEAD 1부모 커밋(Assets 변경) %d · 제외 문서: %s" % (
        len(docs), len(tests), len(hist.blob_keys), len(hist.commits), ", ".join("`%s`(%s)" % kv for kv in EXCLUDED_DOCS.items())))
    L.append("- **출현 단위**: " + " · ".join("(%s) %d" % (k, counts[k]) for k in ("가", "나", "다", "라", "마")) +
             " · 제외 %d · 패턴 표기(와일드카드, 판정 안 함) %d" % (counts["제외"], wildcard))
    L.append("- **이름 단위(고유 키)**: " + " · ".join("(%s) %d" % (k, names.get(k, 0)) for k in ("가", "나", "다", "마", "제외")))
    sc = collections.Counter(o["marker_scope"] for o in classified if o["v"] == "라")
    L.append("- (라) 표기 범위별: " + " · ".join("%s %d" % (k, sc[k]) for k in ("단위", "표 머리", "절", "문서 머리")))
    L.append("- (라) 중 **약한 표기**(표기가 이름과 다른 문장·표 셀 — F절 사람 확인 목록) %d" % sum(
        1 for o in classified if o["v"] == "라" and o.get("weak")))
    mk = collections.Counter(o["marker"] for o in classified if o["v"] == "라")
    L.append("- (라) 표기 적중 낱말: " + " · ".join("「%s」 %d" % kv for kv in mk.most_common()))
    L.append("- 표기 사전(단위, %d): %s" % (len(MARKERS_PLAN + MARKERS_GONE), " · ".join("「%s」" % x for x in MARKERS_PLAN + MARKERS_GONE)))
    L.append("- 표기 사전(표 머리·절·문서 머리, %d): %s" % (len(WIDE_SCOPE_MARKERS), " · ".join("「%s」" % x for x in WIDE_SCOPE_MARKERS)))
    L.append("- 표기로 세지 않는 복합어(%d): %s" % (len(MARKER_NOT_ABSENCE), " · ".join("「%s」" % x for x in MARKER_NOT_ABSENCE)))
    L.append("- 사전 밖 표기 후보 낱말(%d): %s" % (len(SOFT_MARKERS), " · ".join("「%s」" % x for x in SOFT_MARKERS)))
    unc = sorted({"/".join(o["key"][1:]) for o in classified if "작업 트리 실재 · 미커밋" in o["res"]["notes"]})
    L.append("- (가) 중 「작업 트리 실재 · 미커밋」 이름 %d: %s" % (len(unc), " · ".join("`%s`" % u for u in unc)))
    exc = collections.Counter(o["res"]["notes"][0] if o["res"]["notes"] else "?" for o in classified if o["v"] == "제외")
    L.append("- 제외 사유별: " + " · ".join("%s %d" % kv for kv in exc.most_common()))

    for v, title in (("나", "(나) 한 번도 존재한 적 없음 — 미표기"), ("다", "(다) 있었다가 사라짐 — 미표기")):
        for part, sel in (("설계·명세·체크표 문서", lambda o: o["file"] not in LOG_DOCS), ("`Tasklist.md`(날짜별 기록)", lambda o: o["file"] in LOG_DOCS)):
            items = sorted([o for o in classified if o["v"] == v and sel(o)], key=lambda o: (o["severity"] != "높음", o["file"], o["line"]))
            L.append("\n## %s %s — %s %d건\n" % ("C." if v == "나" else "D.", title, part, len(items)))
            tb = []
            for o in items:
                r = o["res"]
                hist_s = ("도입 %s → 소멸 %s; " % (r.get("intro"), r.get("vanish")) if v == "다" else "") + ev(o)
                tb.append(("`%s:%d`" % (o["file"], o["line"]), "`%s`" % o["anchor"], o["kind"], gist(o, 90), hist_s,
                           o["severity"] + ("(" + ",".join(o["sev_cats"]) + ")" if o["sev_cats"] else ""), note_of(o)))
            table(tb, ("파일:줄", "이름", "형태", "약속 문장 요지", "`git log -S`·선언 이력", "심각도", "비고"))
    items = sorted([o for o in classified if o["v"] == "다" and o["file"] not in LOG_DOCS], key=lambda o: (o["severity"] != "높음", o["file"], o["line"]))
    L.append("\n## E. 거짓 서술 후보 — (다) 미표기 · 기록 문서 제외 %d건\n" % len(items))
    L.append("문서가 사라진 테스트를 표기 없이 이름으로 인용한다. 과거형 기록일 수 있어 **후보**다 — 문장을 읽고 판정한다(사람 판정은 이 문서 앞머리).\n")
    table([("`%s:%d`" % (o["file"], o["line"]), "`%s`" % o["anchor"], o["severity"], false_statement_label(o), gist(o, 90)) for o in items],
          ("파일:줄", "이름", "심각도", "blame(줄 작성 시점 vs 소멸)", "요지"))
    items = sorted([o for o in classified if (o["v"] in ("나", "다") and (o["soft"] or o["marker_far"])) or (o["v"] == "라" and o.get("weak"))],
                   key=lambda o: (o["v"] != "라", o["file"], o["line"]))
    L.append("\n## F. 사람 확인 목록 — 약한 표기 (라) · 사전 밖 표기 후보 (나)(다) %d건\n" % len(items))
    L.append("① **약한 표기**: (라)로 셌지만 표기가 이름과 **다른 문장·표 셀**에 있다(V2는 하드 규칙이 아니라 목록으로 둔다 — verify-change 실측 "
             "뒤집힘 26 중 옳음 12·틀림 14). 읽고 그 표기가 이 이름의 부재를 말하지 않으면 실제로는 (나)(다)다. "
             "② **사전 밖 후보**: 사전 표기가 아니라 (라)로 세지 않았다. 「스스로 부재를 밝힌 문장」이면 사전에 넣을 후보다.\n")
    table([("`%s:%d`" % (o["file"], o["line"]), "`%s`" % o["anchor"],
            o["v"] + ("(약) · 이름 (%s)" % o["name_v"] if o["v"] == "라" else ""),
            ("표기「%s」" % o["marker"] if o["v"] == "라" else "") + ("「%s」" % o["soft"] if o["soft"] else "")
            + (" 먼 표기「%s」%d자" % (o["marker"], o["marker_far"]) if o["marker_far"] else ""),
            gist(o, 90)) for o in items], ("파일:줄", "이름", "분류", "낱말", "요지"))
    items = sorted([o for o in classified if o["v"] == "마"], key=lambda o: (o["file"], o["line"]))
    L.append("\n## G. (마) 판정 불가 — %d건\n" % len(items))
    table([("`%s:%d`" % (o["file"], o["line"]), "`%s`" % o["anchor"], "; ".join(o["res"]["notes"])) for o in items], ("파일:줄", "이름", "사유"))
    L.append("\n## H. (라) 문서가 스스로 표기 — %d건 (범위·낱말만 목록, 전수는 `--json`)\n" % counts["라"])
    grp = collections.Counter((o["file"], o["marker_scope"], o["marker"]) for o in classified if o["v"] == "라")
    table([("`%s`" % f, s, "「%s」" % m, c) for (f, s, m), c in sorted(grp.items())], ("문서", "범위", "표기", "출현"))
    L.append("\n## I. 스냅숏(신선도 가드 입력 — 손으로 고치지 말 것)\n")
    L.append(render_snapshot(rows, counts))
    return "\n".join(L)


def _file_state(p):
    try:
        with open(p, "rb") as f:
            data = f.read()
        return (os.stat(p).st_mtime_ns, len(data), data)
    except OSError:
        return None


def _emit(text, rc):
    """모든 출력(교정·신선도·목록)을 누출 가드에 통과시킨 뒤 인쇄한다(verify-change 경미 적발 — 첫 판은 목록 출력만 검사했다)."""
    bad = leak_check(text)
    if bad:
        print("★ 누출 가드 — 출력에 %s 흔적 (rc=2)" % ",".join(bad))
        return 2
    print(text)
    return rc


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--markdown", action="store_true")
    ap.add_argument("--check", metavar="DOC")
    ap.add_argument("--json", metavar="PATH")
    args = ap.parse_args()
    if args.json and (os.path.realpath(args.json) + os.sep).startswith(os.path.realpath(REPO) + os.sep):
        print("★ --json 경로가 저장소 안이다 — 거부(rc=2). 저장소 밖(scratchpad 등) 경로를 준다.")
        return 2
    index_path = os.path.join(REPO, run_git(["rev-parse", "--git-path", "index"]).decode().strip())
    index_before = _file_state(index_path)

    tests, docs, status, problems = collect_inputs()
    work, head, prod = build_indexes(tests)
    hist = History()
    renames = load_renames()
    doc_texts = [(p, read(p)) for p in docs]
    resolver = Resolver(work, head, hist, prod, renames, exclude_names=SYNTH_FIXTURE_NAMES)
    classified, wildcard = scan_docs(doc_texts, resolver)
    oks, fails = calibrate(work, head, hist, prod, renames, doc_texts, problems, classified)
    index_after = _file_state(index_path)
    (oks if index_before is not None and index_before == index_after else fails).append(
        "C10 `.git/index` 무쓰기 — 실행 전후 mtime·크기·내용 동일(모든 git 호출 `--no-optional-locks`). "
        "★ 같은 시각 다른 프로세스(리더 커밋)가 인덱스를 바꿔도 여기서 빨개진다 — 그때는 다시 돌린다")

    out = ["== 교정 %d/%d 통과" % (len(oks), len(oks) + len(fails))]
    out += ["  ✗ " + f for f in fails]
    if args.selftest or fails:
        out += ["  ✓ " + s for s in oks]
    if fails:
        out.append("★ 교정 실패 — 이 실행의 판정 숫자는 인용 금지 (rc=2)")
        return _emit(redact("\n".join(out)), 2)
    if args.selftest:
        return _emit(redact("\n".join(out)), 0)

    for o in classified:
        o["volatile"] = status.get(o["file"])
    counts = counts_of(classified)
    rows = snapshot_rows(classified, doc_texts)

    if args.check:
        rel = os.path.relpath(os.path.abspath(os.path.join(REPO, args.check)), REPO)
        snap_rows, snap_counts = parse_snapshot(read(rel))
        diffs = freshness_diff(snap_rows, snap_counts, rows, counts)
        out.append("== 신선도 가드: %s" % rel)
        if diffs:
            out.append("★ 스냅숏 낡음 — 결과 문서 숫자 인용 금지 (rc=2). 차이 %d:" % len(diffs))
            out += ["  " + d for d in diffs[:60]]
            return _emit(redact("\n".join(out)), 2)
        out.append("  스냅숏 = 원천 재계산 (행 %d · 분류 %s)" % (len(rows), counts))
        return _emit(redact("\n".join(out)), 0)

    names, seen = collections.Counter(), set()
    for o in classified:
        k = (o["key"], o.get("pat"))
        if k not in seen:
            seen.add(k)
            names[o["res"]["v"]] += 1
    out.append("== 대상 문서 %d (제외 %s) · 테스트 .cs %d · 이력 blob %d · 커밋(1부모) %d" % (
        len(docs), ", ".join(EXCLUDED_DOCS), len(tests), len(hist.blob_keys), len(hist.commits)))
    out.append("== 출현 분류: " + " ".join("(%s) %d" % (k, counts[k]) for k in ("가", "나", "다", "라", "마")) +
               " · 제외 %d · 패턴 표기(와일드카드) %d" % (counts["제외"], wildcard))
    out.append("== 이름 분류(고유 키): " + " ".join("(%s) %d" % (k, names.get(k, 0)) for k in ("가", "나", "다", "마", "제외")))

    if args.markdown:
        text = redact(render_markdown(classified, counts, names, wildcard, rows, docs, tests, hist, oks))
    else:
        for label, sel in (("(나) 미표기", lambda o: o["v"] == "나"), ("(다) 미표기", lambda o: o["v"] == "다"), ("(마)", lambda o: o["v"] == "마")):
            items = [o for o in classified if sel(o)]
            out.append("\n== %s %d" % (label, len(items)))
            for o in sorted(items, key=lambda o: (o["severity"] != "높음", o["file"], o["line"])):
                out.append("  " + fmt_occ(o) + ("  〔작업 트리 기준, 변동 중〕" if o["volatile"] else "") + "\n      » " + gist(o))
        text = redact("\n".join(out))
    bad = leak_check(text)
    if bad:
        print("★ 누출 가드 — 출력에 %s 흔적 (rc=2)" % ",".join(bad))
        return 2
    print(text)
    if args.json:
        dump = []
        for o in classified:
            d = {k: v for k, v in o.items() if k not in ("unit", "res", "scopes")}
            d["key"] = list(o["key"])
            d["res"] = {k: (list(v) if isinstance(v, tuple) else v) for k, v in o["res"].items() if k != "key"}
            d["gist"] = gist(o, 200)
            dump.append(d)
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(dump, f, ensure_ascii=False, indent=1)
    return 3 if any(o["v"] in ("나", "다") for o in classified) else 0


if __name__ == "__main__":
    sys.exit(main())
