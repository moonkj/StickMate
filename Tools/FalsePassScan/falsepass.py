#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
거짓 통과 사냥 — 「통과하면서 아무것도 재지 않는 테스트」 색출기 (qa-regression, 2026-09-02)

이 저장소가 하루에 아홉 번 당한 형태는 전부 같았다:
**실패한 측정과 성공한 측정이 똑같이 생겼다.** 그중 러너로는 못 잡는 종류가 두 가지다.

  (A) 단언이 아예 없는 테스트          — 예외만 안 나면 초록.
  (B) 빈 컬렉션 위의 foreach 안에만 단언 — 목록이 비면 0회 실행되고 초록.
                                          (TEAM.md 거짓통과 5번: "면제 목록이 비면 foreach가
                                           아무것도 안 재고 초록")

★ 이 도구 자신의 함정 — 「생성기와 검사기가 같이 틀린다」(TEAM.md 신형)
  이 스캐너는 C# 파서가 아니라 **텍스트 휴리스틱**이다. 그래서 결과를 그대로 믿으면 안 되고,
  반드시 --selftest 로 **알려진 값에 먼저 교정**한다. 교정이 깨지면 그날 낸 숫자를 전부 버린다.
  교정 표본은 이 파일 안에 문자열로 박아 둔다 — 저장소 코드를 읽어 만들지 않는다.
  (저장소에서 만들면 저장소가 바뀔 때 기대값도 같이 바뀌어 아무것도 못 잰다.)

사용법:
  python3 Tools/FalsePassScan/falsepass.py --selftest
  python3 Tools/FalsePassScan/falsepass.py <테스트폴더> [--since-mtime "2026-09-02 14:00"]
"""
import argparse
import datetime
import os
import re
import sys

ATTR = re.compile(r'\[\s*(Test|UnityTest|TestCase|TestCaseSource)\b')
# 무언가를 실제로 재는 호출. LogAssert/Expect 는 Unity 테스트에서 유효한 단언이다.
ASSERT = re.compile(
    r'\b(Assert|StringAssert|CollectionAssert|FileAssert|DirectoryAssert|LogAssert)\s*\.'
    r'|\bAssert\.That\b|\bExpect\s*\('
)
# 단언을 감싸는 반복문 머리
LOOPHEAD = re.compile(r'^\s*(foreach\s*\(|for\s*\(|while\s*\()')
# "이 목록이 비어 있지 않다"를 먼저 못 박는 형태들
NONEMPTY = re.compile(
    r'Assert\.(IsNotEmpty|Greater|GreaterOrEqual|AreEqual|AreNotEqual|IsTrue|Positive)'
    r'|CollectionAssert\.IsNotEmpty'
)


def split_methods(src):
    """[Test]/[UnityTest] 가 붙은 메서드 본문을 (이름, 본문, 시작줄) 로 잘라 낸다.
    중괄호 균형으로 자른다 — 문자열/주석 안의 중괄호는 세지 않는다."""
    out = []
    lines = src.split('\n')
    i = 0
    while i < len(lines):
        if ATTR.search(lines[i]):
            # 속성 줄 뒤에서 첫 '{' 를 찾는다(속성이 여러 줄일 수 있다).
            j = i
            name = None
            while j < len(lines) and j < i + 25:
                # ★ 메서드 이름이 한글로 시작하는 테스트가 이 저장소의 다수다.
                #   ASCII 선두를 요구하면 정확히 그것들만 조용히 빠진다 — 교정이 이걸 잡았다.
                m = re.search(r'(?:IEnumerator|void|Task)\s+(\w[\w가-힣]*)\s*\(', lines[j], re.UNICODE)
                if m:
                    name = m.group(1)
                    break
                j += 1
            if name is None:
                i += 1
                continue
            k = j
            while k < len(lines) and '{' not in lines[k]:
                k += 1
            depth, body, start = 0, [], k
            started = False
            while k < len(lines):
                stripped = strip_noise(lines[k])
                depth += stripped.count('{') - stripped.count('}')
                body.append(lines[k])
                if '{' in stripped:
                    started = True
                if started and depth <= 0:
                    break
                k += 1
            out.append((name, '\n'.join(body), start + 1))
            i = k + 1
            continue
        i += 1
    return out


def strip_noise(line):
    """중괄호를 셀 때 방해가 되는 문자열 리터럴과 줄 주석을 지운다."""
    line = re.sub(r'"(?:\\.|[^"\\])*"', '""', line)
    line = re.sub(r"'(?:\\.|[^'\\])*'", "''", line)
    idx = line.find('//')
    return line[:idx] if idx >= 0 else line


def code_lines(body):
    """주석 줄을 걷어낸 코드 줄만 돌려준다."""
    out = []
    for ln in body.split('\n'):
        t = ln.strip()
        if t.startswith('//') or t.startswith('*') or t.startswith('/*'):
            continue
        out.append(ln)
    return out


def analyze(name, body, line_no):
    """(종류, 설명) 목록을 돌려준다. 비어 있으면 이 메서드는 깨끗하다."""
    findings = []
    lines = code_lines(body)
    code = '\n'.join(lines)

    asserts = [i for i, ln in enumerate(lines) if ASSERT.search(strip_noise(ln))]
    if not asserts:
        findings.append(('A', '단언이 한 줄도 없다 — 예외만 안 나면 무조건 초록이다'))
        return findings

    # (B) 모든 단언이 반복문 안에만 있는가 + 그 반복 대상이 비지 않음을 먼저 못 박았는가
    depth_of = []
    d = 0
    for ln in lines:
        s = strip_noise(ln)
        opens = s.count('{')
        closes = s.count('}')
        depth_of.append(d)
        d += opens - closes

    # ★ 이 저장소는 Allman 스타일이라 여는 중괄호가 **반복문 머리 다음 줄**에 온다.
    #   깊이만 보고 자르면 그 한 줄 차이 때문에 반복문 범위가 즉시 닫혀 (B)가 하나도 안 잡힌다
    #   — 교정 표본이 정확히 이걸 잡았다. 그래서 머리에서부터 중괄호를 직접 센다.
    loop_ranges = []
    for i, ln in enumerate(lines):
        if not LOOPHEAD.match(strip_noise(ln)):
            continue
        k = i
        while k < len(lines) and '{' not in strip_noise(lines[k]):
            if k > i and ';' in strip_noise(lines[k]):
                break          # 중괄호 없는 단일 문장 본문
            k += 1
        if k >= len(lines) or '{' not in strip_noise(lines[k]):
            loop_ranges.append((i, min(i + 2, len(lines))))
            continue
        d, j, started = 0, k, False
        while j < len(lines):
            s = strip_noise(lines[j])
            d += s.count('{') - s.count('}')
            if '{' in s:
                started = True
            if started and d <= 0:
                break
            j += 1
        loop_ranges.append((i, j))

    def in_any_loop(i):
        return any(a < i < b for a, b in loop_ranges)

    if loop_ranges and all(in_any_loop(i) for i in asserts):
        # ★ 노이즈 제거 — "비어 있을 수 있는가"가 이 검사의 전부다.
        #   인라인 리터럴(`new[] { a, b }`)이나 숫자 상한 for 문은 **구조적으로 비지 않는다**.
        #   그것까지 세면 238건이 나오고(실측), 그 목록은 아무도 안 본다 = 검사가 죽는다.
        #   진짜 표적은 TEAM.md 5번의 형태다: **프로덕션이 만들어 준 목록** 위의 foreach.
        risky = [r for r in loop_ranges
                 if any(a < i < b for i in asserts for a, b in [r])
                 and can_be_empty(lines, r[0])]
        if risky:
            head_before = '\n'.join(lines[:risky[0][0]])
            if not NONEMPTY.search(head_before):
                findings.append((
                    'B',
                    '단언이 전부 반복문 안에 있고 그 대상이 **런타임에 만들어지는 목록**인데, '
                    '앞에 "비어 있지 않다"를 못 박은 단언이 없다 — 0건이면 0회 실행되고 초록이다: '
                    + lines[risky[0][0]].strip()[:110]))
    return findings


NUMERIC_BOUND = re.compile(r'[<>]=?\s*\d+')
INLINE_LITERAL = re.compile(r'new\s*(?:\w[\w<>,\s\.]*)?\[\s*\]\s*\{|new\s+\w[\w<>,\s\.]*\s*\{|\{\s*[^}]')


def can_be_empty(lines, idx):
    """이 반복문의 대상이 **비어 있을 수 있는가**. 아니면 (B) 위험이 없다."""
    head = strip_noise(lines[idx])
    if head.strip().startswith('for ') or head.strip().startswith('for('):
        # for (int i = 0; i < 3; i++) 처럼 숫자 상한이면 반드시 돈다.
        if NUMERIC_BOUND.search(head):
            return False
        # for (int i = 0; i < xs.Length; i++) — xs가 인라인 리터럴이면 역시 반드시 돈다.
        m = re.search(r'[<>]=?\s*([A-Za-z_]\w*)\s*\.\s*(?:Length|Count)', head)
        if m and not _built_at_runtime(lines, idx, m.group(1)):
            return False
        return True
    m = re.search(r'foreach\s*\((.*?)\s+in\s+(.+?)\)\s*$', head.strip())
    if not m:
        return True
    expr = m.group(2).strip()
    if INLINE_LITERAL.search(expr):
        return False                       # foreach (var x in new[] { ... })
    if re.fullmatch(r'[A-Za-z_]\w*', expr):
        return _built_at_runtime(lines, idx, expr)
    return True


def _built_at_runtime(lines, idx, name):
    """`name`이 이 메서드 안에서 **인라인 리터럴**로 초기화됐으면 비지 않는다(False).
    프로덕션 호출로 채워졌거나 출처를 못 찾으면 비어 있을 수 있다고 본다(True) — 보수적으로."""
    decl = re.compile(r'\b' + re.escape(name) + r'\s*=')
    for j in range(idx):
        s = strip_noise(lines[j])
        if not decl.search(s):
            continue
        if INLINE_LITERAL.search(s):
            return False
        # 여러 줄 초기화: `var xs = new[]` / `new X[]` 다음 줄에 `{`
        if re.search(r'new\s*[\w<>,\s\.]*\[\s*\]\s*$', s.strip()):
            return False
    return True


# ---------------------------------------------------------------------------
# ★ 교정 표본 — 이 문자열들은 **저장소에서 읽어 오지 않는다**. 기대값이 대상과 함께
#   움직이면 아무것도 못 재기 때문이다(TEAM.md 「생성기와 검사기가 같이 틀린다」).
# ---------------------------------------------------------------------------
CALIB = [
    # (소스, 메서드명, 기대 종류들)
    ("""
    [Test]
    public void 아무것도_재지_않는다()
    {
        var x = Compute();
        Debug.Log(x);
    }
    """, '아무것도_재지_않는다', {'A'}),

    ("""
    [Test]
    public void 빈_목록_위의_foreach()
    {
        var exempt = BuildExemptions();
        foreach (var e in exempt)
        {
            Assert.IsTrue(e.IsValid);
        }
    }
    """, '빈_목록_위의_foreach', {'B'}),

    ("""
    [Test]
    public void 비어있지_않음을_먼저_박는다()
    {
        var exempt = BuildExemptions();
        Assert.Greater(exempt.Count, 0, "면제 목록이 비면 아래가 아무것도 재지 않는다");
        foreach (var e in exempt)
        {
            Assert.IsTrue(e.IsValid);
        }
    }
    """, '비어있지_않음을_먼저_박는다', set()),

    ("""
    [Test]
    public void 평범한_단언()
    {
        Assert.AreEqual(3, Compute());
    }
    """, '평범한_단언', set()),

    # ↓ 노이즈 필터 교정 — 이 셋이 (B)로 잡히면 목록이 238건이 되어 아무도 안 본다.
    ("""
    [Test]
    public void 인라인_리터럴_위의_foreach는_비지_않는다()
    {
        foreach (var s in new[] { 0.5f, 1.0f, 2.0f })
        {
            Assert.Greater(Measure(s), 0f);
        }
    }
    """, '인라인_리터럴_위의_foreach는_비지_않는다', set()),

    ("""
    [Test]
    public void 리터럴로_초기화된_지역변수도_비지_않는다()
    {
        var scales = new[] { 0.5f, 1.0f };
        foreach (var s in scales)
        {
            Assert.Greater(Measure(s), 0f);
        }
    }
    """, '리터럴로_초기화된_지역변수도_비지_않는다', set()),

    ("""
    [Test]
    public void 숫자_상한_for는_반드시_돈다()
    {
        for (int i = 0; i < 4; i++)
        {
            Assert.IsTrue(Check(i));
        }
    }
    """, '숫자_상한_for는_반드시_돈다', set()),

    ("""
    [Test]
    public void 리터럴배열_Length_상한_for도_반드시_돈다()
    {
        var scales = new[] { 0.5f, 1.0f, 2.0f };
        for (int i = 0; i < scales.Length; i++)
        {
            Assert.Greater(Measure(scales[i]), 0f);
        }
    }
    """, '리터럴배열_Length_상한_for도_반드시_돈다', set()),

    ("""
    [Test]
    public void 프로덕션이_준_배열의_Length_상한은_비어있을_수_있다()
    {
        var rows = Policy.CollectRows();
        for (int i = 0; i < rows.Length; i++)
        {
            Assert.IsTrue(rows[i].IsValid);
        }
    }
    """, '프로덕션이_준_배열의_Length_상한은_비어있을_수_있다', {'B'}),

    ("""
    [Test]
    public void 프로덕션이_만든_목록_위의_foreach는_위험하다()
    {
        var exemptions = Policy.BuildExemptions();
        foreach (var e in exemptions)
        {
            Assert.IsTrue(e.IsValid);
        }
    }
    """, '프로덕션이_만든_목록_위의_foreach는_위험하다', {'B'}),

    ("""
    [UnityTest]
    public IEnumerator 유니티테스트도_잡는다()
    {
        yield return null;
        Debug.Log("측정 없음");
    }
    """, '유니티테스트도_잡는다', {'A'}),

    ("""
    [Test]
    public void 주석_안의_Assert는_단언이_아니다()
    {
        // Assert.AreEqual(1, 1);
        var x = Compute();
    }
    """, '주석_안의_Assert는_단언이_아니다', {'A'}),
]


def selftest():
    ok = True
    print("── 교정(알려진 값으로 먼저 맞춘다) ──")
    for src, name, expect in CALIB:
        methods = split_methods(src)
        got_names = [m[0] for m in methods]
        if name not in got_names:
            print(f"  ✗ 메서드 '{name}'를 잘라내지 못했다 (찾은 것: {got_names})")
            ok = False
            continue
        body, ln = next((b, l) for n, b, l in methods if n == name)
        kinds = {k for k, _ in analyze(name, body, ln)}
        mark = '✓' if kinds == expect else '✗'
        if kinds != expect:
            ok = False
        print(f"  {mark} {name}: 기대 {sorted(expect) or '깨끗'} / 실제 {sorted(kinds) or '깨끗'}")
    print("교정 통과 — 이 스캐너의 숫자를 쓸 수 있다." if ok
          else "★ 교정 실패 — 이 스캐너가 낸 숫자는 전부 폐기한다.")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('root', nargs='?')
    ap.add_argument('--selftest', action='store_true')
    ap.add_argument('--since-mtime', default=None,
                    help='이 시각 이후에 수정된 파일만 (예: "2026-09-02 14:00")')
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    if not a.root:
        ap.error('대상 폴더가 필요하다')

    # ★ 스캔 전에 항상 교정한다 — 교정이 깨진 채로 낸 "0건"은 "깨끗"이 아니다.
    if selftest() != 0:
        return 1
    print()

    since = None
    if a.since_mtime:
        since = datetime.datetime.strptime(a.since_mtime, '%Y-%m-%d %H:%M').timestamp()

    files, total_methods, hits = 0, 0, []
    for dirpath, _, names in os.walk(a.root):
        for n in sorted(names):
            if not n.endswith('.cs'):
                continue
            p = os.path.join(dirpath, n)
            if since and os.stat(p).st_mtime < since:
                continue
            files += 1
            src = open(p, encoding='utf-8', errors='replace').read()
            for name, body, ln in split_methods(src):
                total_methods += 1
                for kind, why in analyze(name, body, ln):
                    hits.append((kind, p, ln, name, why))

    print(f"검사한 파일 {files}개 / 테스트 메서드 {total_methods}개")
    if total_methods == 0:
        print("★ 테스트 메서드를 0개 셌다 — 스캔이 성립하지 않았다(경로/필터 확인). '깨끗'이 아니다.")
        return 1
    if not hits:
        print("의심 0건.")
        return 0
    for kind, p, ln, name, why in sorted(hits):
        rel = os.path.relpath(p, a.root)
        print(f"  [{kind}] {rel}:{ln}  {name}\n        {why}")
    print(f"\n의심 {len(hits)}건 / 메서드 {total_methods}개")
    return 0


if __name__ == '__main__':
    sys.exit(main())
