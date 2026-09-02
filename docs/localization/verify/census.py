#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
로컬라이제이션 표면 인구조사 (localization / 2026-09-02)

이 스크립트가 존재하는 이유 — 두 가지 함정이 이 영역의 본거지다.
  (A) `.asset` 42개의 한글은 전부 `\\uXXXX` 이스케이프라 `grep '[가-힣]'`이 **영원히 0건**을 낸다.
  (B) `.cs`의 한글 리터럴 대다수는 `[Tooltip]`/`[Header]`(인스펙터 전용)와 `Debug.Log`(로그)라
      **출하되지 않는다.** 총 리터럴 수로 규모를 말하면 몇 배 과대다.

그래서 이 스크립트는
  1) C# 소스를 **어휘 분석**해 주석/문자열을 구분하고(주석 안의 한글은 리터럴이 아니다),
  2) 각 리터럴을 **감싸는 호출부/애트리뷰트**로 분류하고,
  3) `.asset`은 **디코드한 뒤** 센다.

모든 "0건" 판정에는 양성 대조가 붙는다(`--selftest`).
"""

import os, re, sys, json, io

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
SCRIPTS = os.path.join(ROOT, "Assets", "_Project", "Scripts")
ASSETS = os.path.join(ROOT, "Assets")

HANGUL = re.compile(r'[가-힣ᄀ-ᇿ㄰-㆏]')

# ---------------------------------------------------------------- C# 어휘 분석
# 반환: (literals, masked)
#   literals: [ {start, end, line, value, raw} ]
#   masked  : 문자열/주석을 같은 길이의 치환 문자로 덮은 소스(괄호 세기용)


def lex_csharp(src):
    n = len(src)
    i = 0
    out = []                      # literal records
    masked = list(src)
    line = 1
    line_of = [0] * (n + 1)
    # 미리 줄번호 테이블
    ln = 1
    for k, ch in enumerate(src):
        line_of[k] = ln
        if ch == '\n':
            ln += 1
    line_of[n] = ln

    def blank(a, b, filler=' '):
        for k in range(a, b):
            masked[k] = '\n' if src[k] == '\n' else filler

    while i < n:
        ch = src[i]
        # 주석
        if ch == '/' and i + 1 < n and src[i + 1] == '/':
            j = src.find('\n', i)
            j = n if j < 0 else j
            blank(i, j)
            i = j
            continue
        if ch == '/' and i + 1 < n and src[i + 1] == '*':
            j = src.find('*/', i + 2)
            j = n if j < 0 else j + 2
            blank(i, j)
            i = j
            continue
        # 문자 리터럴 'x'
        if ch == "'":
            j = i + 1
            while j < n:
                if src[j] == '\\':
                    j += 2
                    continue
                if src[j] == "'":
                    j += 1
                    break
                j += 1
            blank(i, j, '\x01')
            i = j
            continue
        # 문자열 리터럴: "  @"  $"  $@"  @$"
        if ch == '"' or (ch in '$@' and _starts_string(src, i)):
            start = i
            verbatim = False
            interp = False
            while i < n and src[i] in '$@':
                if src[i] == '@':
                    verbatim = True
                else:
                    interp = True
                i += 1
            assert src[i] == '"'
            i += 1
            buf = []
            if verbatim:
                while i < n:
                    if src[i] == '"' and i + 1 < n and src[i + 1] == '"':
                        buf.append('"')
                        i += 2
                        continue
                    if src[i] == '"':
                        i += 1
                        break
                    buf.append(src[i])
                    i += 1
            else:
                while i < n:
                    c = src[i]
                    if c == '\\' and i + 1 < n:
                        esc = src[i + 1]
                        if esc == 'u' and i + 5 < n:
                            try:
                                buf.append(chr(int(src[i + 2:i + 6], 16)))
                            except ValueError:
                                buf.append('?')
                            i += 6
                            continue
                        buf.append({'n': '\n', 't': '\t', 'r': '\r'}.get(esc, esc))
                        i += 2
                        continue
                    if c == '"':
                        i += 1
                        break
                    if c == '\n':          # 비정상 — 방어
                        break
                    buf.append(c)
                    i += 1
            end = i
            out.append({
                'start': start, 'end': end, 'line': line_of[start],
                'value': ''.join(buf), 'interp': interp, 'verbatim': verbatim,
            })
            blank(start, end, '\x01')
            continue
        i += 1
    return out, ''.join(masked)


def _starts_string(src, i):
    j = i
    while j < len(src) and src[j] in '$@':
        j += 1
    return j < len(src) and src[j] == '"' and j > i


IDENT_TAIL = re.compile(r'([A-Za-z_][A-Za-z_0-9]*(?:\s*\.\s*[A-Za-z_][A-Za-z_0-9]*)*)\s*$')


def enclosing_call(masked, pos):
    """literal 위치를 감싸는 가장 가까운 열린 '(' 앞의 식별자 체인. 없으면 None."""
    depth = 0
    i = pos - 1
    while i >= 0:
        c = masked[i]
        if c == ')':
            depth += 1
        elif c == '(':
            if depth == 0:
                m = IDENT_TAIL.search(masked[max(0, i - 200):i])
                return re.sub(r'\s+', '', m.group(1)) if m else ''
            depth -= 1
        elif c == ';' or c == '{' or c == '}':
            if depth == 0:
                return None
        i -= 1
    return None


def in_attribute(masked, pos):
    """literal 이 애트리뷰트 [ ... ] 안에 있는가. 있으면 애트리뷰트 이름."""
    depth = 0
    parens = 0
    i = pos - 1
    while i >= 0:
        c = masked[i]
        if c == ']':
            depth += 1
        elif c == '[':
            if depth == 0:
                m = re.match(r'\s*([A-Za-z_][A-Za-z_0-9]*)', masked[i + 1:i + 80])
                return m.group(1) if m else '?'
            depth -= 1
        elif c in ';{}':
            if depth == 0 and parens == 0:
                return None
        elif c == ')':
            parens += 1
        elif c == '(':
            if parens > 0:
                parens -= 1
        i -= 1
    return None


INSPECTOR_ATTRS = {
    'Tooltip', 'Header', 'Space', 'CreateAssetMenu', 'AddComponentMenu',
    'MenuItem', 'InspectorName', 'HelpURL', 'Range', 'ContextMenu',
}
DEBUG_CALLS = re.compile(
    r'(^|\.)(Log|LogWarning|LogError|LogFormat|LogWarningFormat|LogErrorFormat|LogException|LogAssertion)$')
TEST_CALLS = re.compile(r'^(Assert|Debug|UnityEngine\.Debug|NUnit)\b')


def classify(lit, masked, path):
    attr = in_attribute(masked, lit['start'])
    if attr:
        return ('INSPECTOR' if attr in INSPECTOR_ATTRS else 'ATTR:' + attr), attr
    call = enclosing_call(masked, lit['start'])
    if call:
        base = call.split('.')[-1]
        if DEBUG_CALLS.search(call) and ('Debug' in call or call in ('Log', 'LogWarning', 'LogError')):
            return 'DEBUG', call
        if base in ('Log', 'LogWarning', 'LogError', 'LogFormat', 'LogException'):
            return 'DEBUG', call
        if base in ('nameof',):
            return 'CODE', call
    return 'OTHER', (call or '')


# ---------------------------------------------------------------- 수집

def cs_files(base, include_tests=False):
    for dirpath, dirnames, filenames in os.walk(base):
        if not include_tests and os.sep + 'Tests' in dirpath + os.sep:
            continue
        for f in sorted(filenames):
            if f.endswith('.cs'):
                yield os.path.join(dirpath, f)


def census_cs(include_tests=False):
    rows = []
    for path in cs_files(SCRIPTS, include_tests):
        src = io.open(path, encoding='utf-8').read()
        lits, masked = lex_csharp(src)
        for lit in lits:
            if not HANGUL.search(lit['value']):
                continue
            kind, ctx = classify(lit, masked, path)
            rows.append({
                'file': os.path.relpath(path, ROOT),
                'line': lit['line'],
                'kind': kind,
                'ctx': ctx,
                'text': lit['value'],
            })
    return rows


UNI_ESC = re.compile(r'\\u([0-9a-fA-F]{4})')


def decode_yaml_scalar(s):
    s = s.strip()
    if len(s) >= 2 and s[0] == '"' and s[-1] == '"':
        s = s[1:-1]
    return UNI_ESC.sub(lambda m: chr(int(m.group(1), 16)), s)


USER_FACING_ASSET_KEYS = ('displayName', 'description')


def census_assets():
    rows = []
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        for f in sorted(filenames):
            if not f.endswith('.asset'):
                continue
            path = os.path.join(dirpath, f)
            raw = io.open(path, encoding='utf-8', errors='replace').read()
            for ln, line in enumerate(raw.splitlines(), 1):
                m = re.match(r'\s*([A-Za-z_][A-Za-z_0-9]*)\s*:\s*(.+?)\s*$', line)
                if not m:
                    continue
                key, val = m.group(1), m.group(2)
                if key not in USER_FACING_ASSET_KEYS:
                    continue
                dec = decode_yaml_scalar(val)
                if not HANGUL.search(dec):
                    continue
                rows.append({
                    'file': os.path.relpath(path, ROOT), 'line': ln,
                    'kind': 'ASSET:' + key, 'ctx': key, 'text': dec,
                })
    return rows


# ---------------------------------------------------------------- 양성 대조

SELFTEST_SRC = u'''
using UnityEngine;
namespace T {
  // 주석 안의 한글 "이것은 리터럴이 아니다" — 세면 안 된다
  /// <summary>XML 주석의 한글 "여기도 아니다"</summary>
  public class A : MonoBehaviour {
    [Tooltip("툴팁 한글")] public int a;
    [Header("헤더 한글")] public int b;
    void M() {
      Debug.Log("로그 한글");
      Debug.LogWarning($"보간 로그 {a} 한글");
      var s = "유저 노출 한글";
      var t = $"보간 유저 {a} 한글";
      var v = @"축자 한글";
      var esc = "\\uD55C\\uAE00";           // 이스케이프된 한글 — 디코드해야 보인다
      var en = "pure english";
      var ch = '가';
    }
  }
}
'''


def selftest():
    ok = True

    def check(name, cond, detail=''):
        nonlocal ok
        print(('  PASS  ' if cond else '  FAIL  ') + name + ('' if not detail else '   ' + detail))
        if not cond:
            ok = False

    print('== 양성 대조 A: C# 어휘 분석/분류 ==')
    lits, masked = lex_csharp(SELFTEST_SRC)
    han = [l for l in lits if HANGUL.search(l['value'])]
    got = {}
    for l in han:
        k, ctx = classify(l, masked, 'selftest.cs')
        got.setdefault(k, []).append(l['value'])
    check('주석 안 한글은 리터럴로 세지 않는다',
          all('아니다' not in v for vs in got.values() for v in vs), repr(got.get('OTHER')))
    check('[Tooltip]/[Header] 2건 = INSPECTOR', sorted(got.get('INSPECTOR', [])) == sorted(['툴팁 한글', '헤더 한글']),
          repr(got.get('INSPECTOR')))
    check('Debug.Log/LogWarning 2건 = DEBUG', len(got.get('DEBUG', [])) == 2, repr(got.get('DEBUG')))
    check('일반 리터럴 4건(일반/보간/축자/이스케이프) = OTHER', len(got.get('OTHER', [])) == 4, repr(got.get('OTHER')))
    check('\\uXXXX 이스케이프가 디코드된다', any(v == '한글' for v in got.get('OTHER', [])), repr(got.get('OTHER')))
    check('영어 전용 리터럴은 안 잡힌다', all('english' not in v for vs in got.values() for v in vs))

    print('== 양성 대조 B: .asset 디코드 ==')
    sample = None
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        for f in filenames:
            if f.startswith('equip_') and f.endswith('.asset'):
                sample = os.path.join(dirpath, f)
                break
        if sample:
            break
    if not sample:
        check('.asset 표본을 찾았다', False)
    else:
        raw = io.open(sample, encoding='utf-8').read()
        grep_hits = len(HANGUL.findall(raw))
        rows = [r for r in census_assets() if r['file'].endswith(os.path.basename(sample))]
        check('생(raw) 바이트에서 grep [가-힣]은 0건 = 이것이 함정이다', grep_hits == 0,
              os.path.basename(sample) + ' raw hits=%d' % grep_hits)
        check('디코드하면 같은 파일에서 2건이 나온다', len(rows) == 2,
              repr([r['text'][:12] for r in rows]))

    print('== 양성 대조 C: 주입 검출(스캐너가 실제로 무언가를 세는가) ==')
    probe = 'var x = "주입된감사프로브";'
    lits2, masked2 = lex_csharp('class Z { void M(){ ' + probe + ' } }')
    hits = [l for l in lits2 if l['value'] == '주입된감사프로브']
    check('주입한 한글 리터럴 1건을 찾는다', len(hits) == 1)
    k, _ = classify(hits[0], masked2, 'z.cs') if hits else ('', '')
    check('그리고 OTHER로 분류된다', k == 'OTHER', k)
    return ok


def main():
    if '--selftest' in sys.argv:
        sys.exit(0 if selftest() else 1)

    prod = census_cs(include_tests=False)
    assets = census_assets()

    by_kind = {}
    for r in prod:
        by_kind.setdefault(r['kind'], []).append(r)

    print('=' * 78)
    print('로컬라이제이션 표면 인구조사  (프로덕션 .cs = Tests/ 제외)')
    print('=' * 78)
    print()
    print('--- .cs 한글 리터럴 분류 ---')
    total = 0
    for k in sorted(by_kind, key=lambda k: -len(by_kind[k])):
        print('  %-14s %5d' % (k, len(by_kind[k])))
        total += len(by_kind[k])
    print('  %-14s %5d' % ('(합계)', total))
    print()
    ship = by_kind.get('OTHER', [])
    print('★ 출하 표면(OTHER) = %d건' % len(ship))
    print('★ 미출하(INSPECTOR+DEBUG) = %d건' % (len(by_kind.get('INSPECTOR', [])) + len(by_kind.get('DEBUG', []))))
    print()

    print('--- OTHER 상위 파일 ---')
    perfile = {}
    for r in ship:
        perfile[r['file']] = perfile.get(r['file'], 0) + 1
    for f, c in sorted(perfile.items(), key=lambda kv: -kv[1])[:30]:
        print('  %5d  %s' % (c, f))
    print('  (파일 수: %d)' % len(perfile))
    print()

    print('--- .asset (디코드 후) ---')
    ak = {}
    for r in assets:
        ak[r['kind']] = ak.get(r['kind'], 0) + 1
    for k in sorted(ak):
        print('  %-22s %5d' % (k, ak[k]))
    print('  파일 수: %d' % len(set(r['file'] for r in assets)))
    print('  합계: %d' % len(assets))
    print()

    print('--- 함정 재현: raw grep vs 디코드 ---')
    rawhits = 0
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        for f in filenames:
            if f.endswith('.asset'):
                raw = io.open(os.path.join(dirpath, f), encoding='utf-8', errors='replace').read()
                rawhits += len(HANGUL.findall(raw))
    print('  .asset 전체 raw grep [가-힣] 히트: %d  <-- 영원히 0' % rawhits)
    print('  .asset 전체 디코드 후 유저 노출 문자열: %d' % len(assets))
    print()

    print('=' * 78)
    print('★ 총 지역화 표면 = OTHER(%d) + .asset(%d) = %d건' % (len(ship), len(assets), len(ship) + len(assets)))
    print('=' * 78)

    outjson = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'census.json')
    with io.open(outjson, 'w', encoding='utf-8') as fh:
        fh.write(json.dumps({'cs': prod, 'asset': assets}, ensure_ascii=False, indent=1))
    print('상세: ' + os.path.relpath(outjson, ROOT))


if __name__ == '__main__':
    main()
