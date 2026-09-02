#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
★ 출하 표면 정밀 계수 — census.py의 OTHER 1125건을 **리터럴 단위로** 다시 가른다.

왜 tier.py(파일 단위 도달성)로 부족한가: 파일 단위는 양쪽으로 틀린다.
  - `Platform/FramePacing.cs`는 UI 파일이 부르지만 **문자열은 로그로만 나간다**(DATA 오판)
  - `Core/StickMateDisplayNames.cs`는 진단 파일처럼 보이지만 **행동창이 그 문자열을 화면에 쓴다**(DIAG 오판)
이 두 오판이 tier.out.txt에 그대로 남아 있다. 그래서 리터럴 단위로 다시 센다.

판정 순서(먼저 맞는 것으로 확정):
  1) INSPECTOR / DEBUG            — census.py가 이미 분류(어휘 분석 기반)
  2) EXC   예외 메시지            — `throw new *Exception(...)` 안. 개발자용.
  3) LOGM  로그 메서드            — 리터럴을 감싸는 **메서드 본문에 Debug.Log*가 있고**
                                    그 메서드가 화면 싱크(.text=/DialogueIntent/DialogueLine)를 안 건드린다
  4) LOGF  로그 파일              — 파일 전체에 화면 싱크가 **한 개도 없고** 화이트리스트에도 없다
  5) SHIP  출하                   — 나머지. 여기에 남으면 번역 대상이다.

화이트리스트(문자열 생산자 — 자신은 UI를 안 그리지만 UI가 그 반환값을 그린다)는
**호출 근거를 파일:줄로 함께 출력한다**(추측 금지).

양성 대조: `--selftest`
"""
import os, re, io, json, sys, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
SCRIPTS = os.path.join(ROOT, "Assets", "_Project", "Scripts")
sys.path.insert(0, HERE)
from census import lex_csharp, HANGUL, classify, census_assets  # noqa

# ------------------------------------------------------------------ 화면 싱크
# ★ 싱크는 "화면에 글자가 나가는 지점"이다. 세 종류뿐이고 전부 실측으로 확인했다.
#   (1) uGUI 텍스트 대입            .text =
#   (2) 말풍선 파이프라인            DialogueIntent / DialogueLine / TimedSpectacleState(대사 공급자)
#   (3) 명령 불가 사유              CommandAvailability.Blocked(reason)
#       — Reason 필드 문서: "불가/부재일 때 <b>사용자에게 그대로 보여줄</b> 한 줄"
#         (Core/CommandAvailability.cs), ActionCommandPopover가 타일 밑에 그린다.
SINK_RE = re.compile(r'\.text\s*=(?!=)|new\s+DialogueIntent|DialogueIntent\s*\(|'
                     r'DialogueLine\s*\.\s*(?:Say|React)|new\s+DialogueLine|'
                     r'new\s+TimedSpectacleState|CommandAvailability\s*\.\s*Blocked')

# 문자열 생산자 화이트리스트 — 값: 이 타입의 문자열을 화면에 쓰는 호출부(근거)
PRODUCER_WHITELIST = {
    'Core/StickMateDisplayNames.cs': 'StickMateDisplayNames',
    'Core/ItemCatalog.cs': 'ItemCatalog',
    'Core/EquipmentModel.cs': 'EquipmentModel',
    'Core/CommandAvailability.cs': 'CommandAvailability',
    'Core/CharacterProgressionModel.cs': 'CharacterProgressionModel',
    'Core/CharacterStatsModel.cs': 'CharacterStatsModel',
    'Core/ShortcutLabel.cs': 'ShortcutLabel',
    'Interaction/StressGaugeRenderer.cs': 'StressGaugeRenderer',
    # ★ Core/KoreanParticle.cs 는 화이트리스트에 **넣지 않는다**.
    #   프로덕션 호출부 4곳이 전부 Debug.Log다(WindowsTopmostWatchdog.cs:291,
    #   CharacterInfoWindow.Cards.cs:410/427, GraffitiRenderer.cs:181 — 전수 확인).
    #   조사 토큰 10개(은/는/이/가/을/를/과/와/으로/로)는 번역 대상이 아니라 한국어 문법 장치다.
}

EXC_RE = re.compile(r'(?:^|\W)new\s+[A-Za-z_][A-Za-z_0-9.]*Exception\s*$')
BLOCK_HEADER_BAD = re.compile(r'\b(if|for|foreach|while|switch|else|using|lock|try|catch|finally|do|fixed|unsafe)\s*$')


def rel(p):
    return os.path.relpath(p, SCRIPTS).replace(os.sep, '/')


def prod_files():
    out = []
    for dp, dn, fn in os.walk(SCRIPTS):
        if os.sep + 'Tests' in dp + os.sep:
            continue
        for f in sorted(fn):
            if f.endswith('.cs'):
                out.append(os.path.join(dp, f))
    return out


def enclosing_method_span(masked, pos):
    """리터럴을 감싸는 **메서드 본문**의 (start, end). 못 찾으면 None."""
    i = pos
    depth = 0
    opens = []
    while i >= 0:
        c = masked[i]
        if c == '}':
            depth += 1
        elif c == '{':
            if depth == 0:
                opens.append(i)
                head = masked[max(0, i - 400):i]
                head_s = head.rstrip()
                # 메서드/생성자/람다 헤더인가: ')' 로 끝나거나 '=>' 로 끝난다. 제어문은 제외.
                if head_s.endswith(')') and not BLOCK_HEADER_BAD.search(head_s[:-1].rstrip().rstrip(')')):
                    # 제어문 여부 재확인: ')' 앞의 여는 괄호 직전 식별자
                    j = len(head_s) - 1
                    d2 = 0
                    while j >= 0:
                        if head_s[j] == ')':
                            d2 += 1
                        elif head_s[j] == '(':
                            d2 -= 1
                            if d2 == 0:
                                break
                        j -= 1
                    ident = head_s[:j].rstrip()
                    if not BLOCK_HEADER_BAD.search(ident):
                        end = matching_brace(masked, i)
                        return (i, end)
            else:
                depth -= 1
        i -= 1
    return None


def matching_brace(masked, i):
    d = 0
    n = len(masked)
    while i < n:
        if masked[i] == '{':
            d += 1
        elif masked[i] == '}':
            d -= 1
            if d == 0:
                return i + 1
        i += 1
    return n


IDENT_TAIL = re.compile(r'([A-Za-z_][A-Za-z_0-9.]*)\s*$')


def is_exception_arg(masked, pos):
    depth = 0
    i = pos - 1
    while i >= 0:
        c = masked[i]
        if c == ')':
            depth += 1
        elif c == '(':
            if depth == 0:
                head = masked[max(0, i - 120):i]
                return bool(EXC_RE.search(head))
            depth -= 1
        elif c in ';{}':
            return False
        i -= 1
    return False


LOGISH = re.compile(r'(?i)(log|report|diagnos|dump|trace|describe|signature|verdict|probe|audit)')
# ★ 이름만으로 진단이 확정되는 좁은 집합 — Debug.Log가 같은 메서드에 없어도 진단이다.
#   (반환한 문자열을 호출부가 로그로 찍는 형태. 실례: WindowTheftDirector.BuildTargetSearchDiagnostic)
#   'label' / 'name' / 'text' 처럼 UI일 수 있는 어근은 **일부러 넣지 않는다**.
STRICT_DIAG = re.compile(r'(?i)(diagnostic|diagnose|dump|trace|signature|audit|probe)')
METHOD_NAME = re.compile(r'([A-Za-z_][A-Za-z_0-9]*)\s*(?:<[^<>]*>)?\s*\($')


def method_name(masked, brace_pos):
    """메서드 본문 여는 '{' 앞 헤더에서 메서드 이름을 뽑는다."""
    head = masked[max(0, brace_pos - 400):brace_pos].rstrip()
    if not head.endswith(')'):
        return ''
    d = 0
    j = len(head) - 1
    while j >= 0:
        if head[j] == ')':
            d += 1
        elif head[j] == '(':
            d -= 1
            if d == 0:
                break
        j -= 1
    m = METHOD_NAME.search(head[:j + 1])
    return m.group(1) if m else ''


def statement_span(masked, pos):
    """리터럴이 속한 **문장**(세미콜론/중괄호 경계, 괄호 깊이 0 기준)의 (start, end)."""
    i = pos
    d = 0
    while i > 0:
        c = masked[i]
        if c in ')]':
            d += 1
        elif c in '([':
            if d == 0:
                pass
            else:
                d -= 1
        elif d == 0 and c in ';{}':
            break
        i -= 1
    start = i
    j = pos
    d = 0
    n = len(masked)
    while j < n:
        c = masked[j]
        if c in '([':
            d += 1
        elif c in ')]':
            if d > 0:
                d -= 1
        elif d == 0 and c in ';{}':
            break
        j += 1
    return (start, min(j + 1, n))


def run():
    files = prod_files()
    src, masked = {}, {}
    for p in files:
        s = io.open(p, encoding='utf-8').read()
        lits, m = lex_csharp(s)
        src[rel(p)] = (s, lits)
        masked[rel(p)] = m

    file_has_sink = {k: bool(SINK_RE.search(v)) for k, v in masked.items()}

    # 화이트리스트 근거 수집
    evidence = {}
    for f, tname in PRODUCER_WHITELIST.items():
        pat = re.compile(r'\b' + re.escape(tname) + r'\s*\.')
        found = []
        for g, m in masked.items():
            if g == f or not file_has_sink.get(g):
                continue
            for mo in pat.finditer(m):
                ln = m.count('\n', 0, mo.start()) + 1
                found.append('%s:%d' % (g, ln))
                break
        evidence[f] = found[:3]

    rows = []
    for f, (s, lits) in src.items():
        m = masked[f]
        for lit in lits:
            if not HANGUL.search(lit['value']):
                continue
            kind, ctx = classify(lit, m, f)
            if kind in ('INSPECTOR', 'DEBUG') or kind.startswith('ATTR:'):
                verdict = 'INSPECTOR' if kind != 'DEBUG' else 'DEBUG'
            elif is_exception_arg(m, lit['start']):
                verdict = 'EXC'
            else:
                span = enclosing_method_span(m, lit['start'])
                body = m[span[0]:span[1]] if span else ''
                mname = method_name(m, span[0]) if span else ''
                has_log = bool(re.search(r'\bDebug\s*\.\s*Log', body)) if body else False
                has_sink_local = bool(SINK_RE.search(body)) if body else False
                stmt = statement_span(m, lit['start'])
                stmt_is_log = bool(re.search(r'\bDebug\s*\.\s*Log', m[stmt[0]:stmt[1]]))
                if (stmt_is_log
                        or (has_log and not has_sink_local and LOGISH.search(mname or ''))
                        or (not has_sink_local and STRICT_DIAG.search(mname or ''))):
                    verdict = 'LOGM'
                elif not file_has_sink.get(f) and f not in PRODUCER_WHITELIST:
                    verdict = 'LOGF'
                else:
                    verdict = 'SHIP'
            rows.append({'file': f, 'line': lit['line'], 'verdict': verdict,
                         'ctx': ctx, 'text': lit['value']})
    return rows, evidence


def report():
    rows, evidence = run()
    by = collections.Counter(r['verdict'] for r in rows)
    print('=' * 78)
    print('출하 표면 정밀 계수 (프로덕션 .cs, Tests/ 제외)')
    print('=' * 78)
    order = ['INSPECTOR', 'DEBUG', 'EXC', 'LOGM', 'LOGF', 'SHIP']
    for k in order:
        print('  %-10s %5d' % (k, by.get(k, 0)))
    print('  %-10s %5d' % ('합계', sum(by.values())))
    print()
    print('  미출하 = INSPECTOR+DEBUG+EXC+LOGM+LOGF = %d' %
          sum(by.get(k, 0) for k in ('INSPECTOR', 'DEBUG', 'EXC', 'LOGM', 'LOGF')))
    print('  ★ 출하(SHIP) = %d' % by.get('SHIP', 0))
    print()

    ship = [r for r in rows if r['verdict'] == 'SHIP']
    per = collections.Counter(r['file'] for r in ship)
    print('--- SHIP 파일별 ---')
    for f, c in per.most_common():
        mark = ''
        if f in PRODUCER_WHITELIST:
            mark = '   [생산자 화이트리스트] 근거: ' + ', '.join(evidence[f]) if evidence[f] else '   [화이트리스트/근거없음★]'
        print('  %5d  %s%s' % (c, f, mark))
    print('  (파일 %d개)' % len(per))
    print()

    assets = census_assets()
    print('--- .asset (디코드 후) ---')
    print('  %d건 / %d파일' % (len(assets), len(set(a['file'] for a in assets))))
    print()
    print('=' * 78)
    print('★ 1.0 번역 대상 = .cs SHIP %d + .asset %d = %d건' % (len(ship), len(assets), len(ship) + len(assets)))
    print('=' * 78)

    with io.open(os.path.join(HERE, 'ship.json'), 'w', encoding='utf-8') as fh:
        fh.write(json.dumps({'cs': rows, 'asset': assets}, ensure_ascii=False, indent=1))


SELF = u'''
using UnityEngine;
class S {
  UnityEngine.UI.Text label;
  void UiPath() { label.text = "화면에 뜨는 글자"; }
  void LogPath() { var s = "로그 조립 조각"; Debug.Log("[진단] " + s); }
  void ExcPath() { throw new System.InvalidOperationException("개발자용 예외"); }
  void Mixed() { var t = "혼합 메서드의 조각"; label.text = t; Debug.Log("찍기"); }
  string BuildFooDiagnostic() { return "호출부가 로그로 찍는 진단 조각"; }
  string TileCaption() { return "싱크 없는 평범한 문자열 생산자"; }
}
'''
SELF_NOSINK = u'''
using UnityEngine;
class N { string Describe() { return "싱크 없는 파일의 조각"; } }
'''


def selftest():
    ok = True

    def chk(name, cond, detail=''):
        nonlocal ok
        print(('  PASS  ' if cond else '  FAIL  ') + name + ('   ' + detail if detail else ''))
        if not cond:
            ok = False

    lits, m = lex_csharp(SELF)
    got = {}
    for lit in lits:
        if not HANGUL.search(lit['value']):
            continue
        kind, ctx = classify(lit, m, 'S.cs')
        if kind == 'DEBUG':
            v = 'DEBUG'
        elif is_exception_arg(m, lit['start']):
            v = 'EXC'
        else:
            span = enclosing_method_span(m, lit['start'])
            body = m[span[0]:span[1]] if span else ''
            has_log = bool(re.search(r'\bDebug\s*\.\s*Log', body))
            has_sink = bool(SINK_RE.search(body))
            mn = method_name(m, span[0]) if span else ''
            st = statement_span(m, lit['start'])
            v = 'LOGM' if (bool(re.search(r'\bDebug\s*\.\s*Log', m[st[0]:st[1]]))
                           or (has_log and not has_sink and LOGISH.search(mn or ''))
                           or (not has_sink and STRICT_DIAG.search(mn or ''))) else 'SHIP'
        got[lit['value']] = v
    chk('UI 대입은 SHIP', got.get('화면에 뜨는 글자') == 'SHIP', str(got.get('화면에 뜨는 글자')))
    chk('Debug.Log 직접 인자는 DEBUG', got.get('[진단] ') == 'DEBUG', str(got.get('[진단] ')))
    chk('로그 메서드 안의 조립 조각은 LOGM', got.get('로그 조립 조각') == 'LOGM', str(got.get('로그 조립 조각')))
    chk('예외 메시지는 EXC', got.get('개발자용 예외') == 'EXC', str(got.get('개발자용 예외')))
    chk('★ 로그와 UI가 같이 있는 메서드는 SHIP(안전측)',
        got.get('혼합 메서드의 조각') == 'SHIP', str(got.get('혼합 메서드의 조각')))

    lits2, m2 = lex_csharp(SELF_NOSINK)
    hs = bool(SINK_RE.search(m2))
    chk('★ 이름이 *Diagnostic 인 메서드는 Debug.Log 없이도 LOGM',
        got.get('호출부가 로그로 찍는 진단 조각') == 'LOGM', str(got.get('호출부가 로그로 찍는 진단 조각')))
    chk('★ 음성 대조 — 진단 어근이 없으면 SHIP으로 남는다(과잉 제외 방지)',
        got.get('싱크 없는 평범한 문자열 생산자') == 'SHIP', str(got.get('싱크 없는 평범한 문자열 생산자')))
    chk('싱크 없는 파일은 file_has_sink=False', hs is False)

    # 음성 대조: 규칙을 망가뜨리면 실제로 빨개지는가
    bad = SINK_RE.pattern
    chk('음성 대조 — 싱크 정규식이 .text= 를 실제로 잡는다', bool(re.search(bad, 'a.text = b')))
    chk('음성 대조 — == 비교는 싱크가 아니다', not bool(re.search(bad, 'if (a.text == b)')))
    return ok


if __name__ == '__main__':
    if '--selftest' in sys.argv:
        sys.exit(0 if selftest() else 1)
    report()
