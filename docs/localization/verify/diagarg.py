#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
★ 진단 사유 인자 검출기 — ship.py 의 SHIP 안에 남아 있는 **비번역 문자열**을 가려낸다.
   (localization / 2026-09-02 R2)

무엇이 문제였나
---------------
ship.py 는 리터럴을 "감싸는 메서드에 Debug.Log 가 있는가"로 로그를 걸렀다. 그런데 이 저장소의
창/팝오버/디렉터는 사유를 **호출부에서 문자열로 넘긴다**:

    Close("[✕] 클릭")                 → private void Close(string source) { ... Debug.Log($"...({source})"); }
    SetTab(Tab.General, "탭 클릭")     → ... Debug.Log($"... ({source})");
    ForceTriggerNow($"앱제어 {source}") → ... Debug.Log(...)

리터럴이 있는 **호출부** 메서드에는 Debug.Log 가 없고 화면 싱크(.text=)는 있다. 그래서 ship.py 가
SHIP 으로 남긴다. 실제로는 **로그에만 나가는 개발자 문자열**이고 번역 대상이 아니다.

판정
----
1) SHIP 리터럴을 감싸는 **호출부 이름**을 괄호 균형으로 찾는다.
2) 그 이름의 메서드 선언을 전 소스에서 찾아, string 파라미터의 **모든 출현**이 Debug.Log 문장
   안에 있으면 DIAG.
3) 다른 메서드로 넘어가면(ESCAPES) 그 대상까지 **전이적으로** 따라간다. 끝까지 DIAG 면 DIAG.
4) 화면 싱크(.text= / DialogueIntent / DialogueLine / _pending*Text 대입)에 닿으면 SHIP 확정.

★ "0건"에 양성 대조를 붙인다 — `--selftest`.
   합성 소스로 (a) 진단 사유를 실제로 잡는가 (b) 화면에 나가는 것을 잘못 잡지 않는가
   양쪽을 모두 찍는다. 한쪽이라도 깨지면 이 스크립트의 모든 숫자를 폐기한다.
"""
import os, re, sys, json, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
SCRIPTS = os.path.join(ROOT, "Assets", "_Project", "Scripts")
sys.path.insert(0, HERE)
from census import lex_csharp, HANGUL  # noqa

# ============================================================================
# ★ 자동 검출기가 **구조적으로 못 보는** 비번역 문자열 — 수동 목록
# ============================================================================
# 이 검출기는 "호출부가 있는 리터럴"만 본다. 대입 / 배열 초기화 / 식 본문(=>)에 있는
# 비번역 문자열은 감싸는 괄호가 없어 잡히지 않는다. 각 줄에 **왜 번역 대상이 아닌지**를 적는다 —
# 사유 없는 면제는 다음 사람이 지울 수도 되살릴 수도 없다.
MANUAL_NONTRANSLATABLE = {
    # 폰트 패밀리 이름 = OS 리소스 식별자. 번역하면 폰트를 못 찾는다.
    ('Dialogue/DialogueBubbleRenderer.cs', 2342): '맑은 고딕 Bold — Windows 폰트 패밀리명',
    ('Dialogue/DialogueBubbleRenderer.cs', 2363): '맑은 고딕 — Windows 폰트 패밀리명',
    # 글리프 커버리지 프로브 문자열. 한글이 그려지는지 묻는 것이므로 영어로 바꾸면 검사가 무의미해진다.
    ('Dialogue/DialogueBubbleRenderer.cs', 2429): 'RequestCharactersInTexture("한글") — 커버리지 프로브',
    # 준비 완료 로그의 화자 상태 라벨.
    ('Dialogue/DialogueBubbleRenderer.cs', 937): '지정됨 — Debug.Log 조립 조각',
    ('Dialogue/DialogueBubbleRenderer.cs', 938): '미지정(...) — Debug.Log 조립 조각',
    # 로그용 종류 라벨(KindLabel). XML 문서가 "로그용"이라고 스스로 적었다.
    ('Dialogue/DialogueBubbleRenderer.cs', 1130): '서술/반응 — KindLabel, 로그 전용',
    # DescribeSuspendReason() — Suspend/Resume 로그에만 붙는다.
    ('Core/StickmanAgent.cs', 1303): 'DescribeSuspendReason — 로그 전용',
    ('Core/StickmanAgent.cs', 1304): 'DescribeSuspendReason — 로그 전용',
    ('Core/StickmanAgent.cs', 1305): 'DescribeSuspendReason — 로그 전용',
    ('Core/StickmanAgent.cs', 1306): 'DescribeSuspendReason — 로그 전용',
    # HotkeySource() — ForceTriggerNow(reason) 로 흘러가는 사유 접두사.
    ('Interaction/AppControlDirector.cs', 331): 'HotkeySource — 사유 접두사',
    # TryResolvePlacement 의 out kindLabel — Begin() 로그에만 쓰인다.
    ('Interaction/ArcheryDirector.cs', 401): 'kindLabel — Begin() 로그 전용',
    # ModeLabel() / CollapseReason* — 접힘 사유 라벨. 로그에만 나간다.
    ('Interaction/GearRadialMenuWidget.cs', 562): 'ModeLabel — 접힘 사유, 로그 전용',
    ('Interaction/GearRadialMenuWidget.cs', 563): 'ModeLabel — 접힘 사유, 로그 전용',
    ('Interaction/GearRadialMenuWidget.cs', 564): 'ModeLabel — 접힘 사유, 로그 전용',
    ('Interaction/TodoPostItWidget.cs', 163): 'CollapseReasonUser — 로그 전용',
    ('Interaction/TodoPostItWidget.cs', 164): 'CollapseReasonAuto — 로그 전용',
}


SINK = re.compile(r'\.text\s*=(?!=)|new\s+DialogueIntent|DialogueIntent\s*\(|'
                  r'DialogueLine\s*\.\s*(?:Say|React)|new\s+DialogueLine|'
                  r'new\s+TimedSpectacleState|CommandAvailability\s*\.\s*Blocked|'
                  r'_pending[A-Za-z]*Text\s*=(?!=)')
LOGCALL = re.compile(r'Debug\.Log')
CALLEE = re.compile(r'([A-Za-z_][A-Za-z_0-9]*)\s*(?:<[^<>()]*>)?\s*\($')
MAX_DEPTH = 6


HOLE = re.compile(r'\{[A-Za-z_][^{}"\n]*\}')


def unmask_interpolation(raw, masked):
    """★ 어휘 분석기는 문자열 **내용을 통째로 지운다.** 그런데 이 저장소의 로그는 거의 전부
       보간 문자열($"...{reason}...")이라, 지운 채로 세면 파라미터 출현이 **0번**으로 보이고
       '증거 없음'으로 접힌다. 보간 구멍 안의 코드만 되살린다.

       ※ 주석 안의 `{...}` 도 함께 되살아날 수 있다. 그 방향은 **안전하다** — 출현이 늘면
         판정이 DIAG 가 아니라 UNKNOWN 쪽으로 밀리고, UNKNOWN 은 SHIP 으로 남는다."""
    out = list(masked)
    for m in HOLE.finditer(raw):
        a, b = m.start(), m.end()
        if masked[a:b] == raw[a:b]:
            continue          # 지워지지 않은 곳 = 진짜 코드. 그대로 둔다.
        # ★ 중괄호는 **되살리지 않는다.** 되살리면 아래 '문장 경계' 계산이 그 `{`를 블록
        #   시작으로 읽어 문장이 잘리고, Debug.Log 안인데도 밖으로 판정된다(실측 사고).
        for k in range(a + 1, b - 1):
            out[k] = raw[k]
    return ''.join(out)


def masked_of(src):
    _, m = lex_csharp(src)
    return ''.join(m) if isinstance(m, list) else m


def enclosing_callee(masked, start):
    """리터럴 시작 위치를 감싸는 호출부 이름. 없으면 None."""
    i, depth = start - 1, 0
    while i >= 0:
        c = masked[i]
        if c in ')]}':
            depth += 1
        elif c == '(':
            if depth == 0:
                m = CALLEE.search(masked[:i + 1])
                return m.group(1) if m else None
            depth -= 1
        elif c in '[{':
            if depth == 0:
                return None
            depth -= 1
        elif c == ';':
            return None
        i -= 1
    return None


def method_bodies(sources, name):
    """이름이 name 인 메서드들의 (string 파라미터 목록, 마스킹된 본문)."""
    rx = re.compile(r'\b(?:public|private|protected|internal|static|virtual|override|sealed|async)\b'
                    r'[^;{}()\n]*\b' + re.escape(name) + r'\s*\(([^)]*)\)\s*\n?\s*\{')
    # 식 본문 메서드( => ) 도 본다: void IExclusiveSurface.CloseSurface(string reason) => Close(reason);
    rx2 = re.compile(r'\b' + re.escape(name) + r'\s*\(([^)]*)\)\s*=>\s*([^;]+);')
    out = []
    for path, (src, masked) in sources.items():
        for m in rx.finditer(masked):
            b = masked.index('{', m.end() - 1)
            depth = 0
            e = b
            for k in range(b, len(masked)):
                if masked[k] == '{':
                    depth += 1
                elif masked[k] == '}':
                    depth -= 1
                    if depth == 0:
                        e = k
                        break
            out.append((path, _string_params(m.group(1)),
                        unmask_interpolation(src[b:e + 1], masked[b:e + 1])))
        for m in rx2.finditer(masked):
            out.append((path, _string_params(m.group(1)),
                        unmask_interpolation(src[m.start(2):m.end(2)], m.group(2))))
    return out


def _string_params(params):
    """★ 기본값(`string notice = null`)을 먼저 잘라낸다.
       자르지 않으면 파라미터 이름이 `null` 로 잡히고, 그 이름은 본문에 0번 나오므로
       '로그 밖 출현이 없다 = DIAG' 로 **거꾸로** 판정된다(실제로 TabDef 4건이 그렇게 오판됐다)."""
    res = []
    for chunk in params.split(','):
        c = chunk.split('=')[0].strip()
        if not c:
            continue
        toks = c.split()
        if len(toks) >= 2 and ('string' in toks[:-1]):
            res.append(toks[-1])
    return res


def verdict(sources, name, depth=0, seen=None):
    """DIAG / SHIP / UNKNOWN"""
    seen = seen or set()
    if name in seen or depth > MAX_DEPTH:
        return 'UNKNOWN'
    seen = seen | {name}
    bodies = method_bodies(sources, name)
    if not bodies:
        return 'UNKNOWN'
    any_diag = False
    for _path, sparams, body in bodies:
        if not sparams:
            continue
        for sp in sparams:
            escapes = []
            occurrences = 0
            for mm in re.finditer(r'\b' + re.escape(sp) + r'\b', body):
                occurrences += 1
                o = mm.start()
                s = max(body.rfind(';', 0, o), body.rfind('{', 0, o))
                t = body.find(';', o)
                t = len(body) if t < 0 else t
                stmt = body[s + 1:t]
                if LOGCALL.search(stmt):
                    continue
                if SINK.search(stmt):
                    return 'SHIP'
                escapes.append(stmt)
            if not occurrences:
                # ★ 본문에 한 번도 안 나오는 파라미터는 **아무 증거도 아니다.**
                #   여기서 DIAG 로 접으면 화면에 나가는 문자열이 조용히 번역 목록에서 빠진다
                #   (안전한 방향은 SHIP 쪽에 남기는 것이다).
                continue
            if not escapes:
                any_diag = True
                continue
            # 전이: 넘겨받는 메서드를 따라간다
            for stmt in escapes:
                for m2 in re.finditer(r'([A-Za-z_][A-Za-z_0-9]*)\s*\([^()]*\b'
                                      + re.escape(sp) + r'\b[^()]*\)', stmt):
                    v = verdict(sources, m2.group(1), depth + 1, seen)
                    if v == 'SHIP':
                        return 'SHIP'
                    if v == 'DIAG':
                        any_diag = True
    return 'DIAG' if any_diag else 'UNKNOWN'


def load_sources(root):
    src = {}
    for dp, _dn, fn in os.walk(root):
        if os.sep + 'Tests' in dp:
            continue
        for f in fn:
            if not f.endswith('.cs'):
                continue
            p = os.path.join(dp, f)
            s = open(p, encoding='utf-8').read()
            src[os.path.relpath(p, root)] = (s, masked_of(s))
    return src


def run():
    ship = json.load(open(os.path.join(HERE, 'ship.json'), encoding='utf-8'))
    want = collections.defaultdict(set)
    for r in ship['cs']:
        if r['verdict'] == 'SHIP':
            want[r['file']].add(r['line'])
    sources = load_sources(SCRIPTS)

    cache = {}
    hits, unknown = [], collections.Counter()
    for f, lines in want.items():
        src, masked = sources[f]
        lits, _ = lex_csharp(src)
        for L in lits:
            if L['line'] not in lines or not HANGUL.search(L['value']):
                continue
            name = enclosing_callee(masked, L['start'])
            if not name:
                continue
            if name not in cache:
                cache[name] = verdict(sources, name)
            if cache[name] == 'DIAG':
                hits.append((f, L['line'], name, L['value'][:60]))
            elif cache[name] == 'UNKNOWN':
                unknown[name] += 1

    if '--json' in sys.argv:
        json.dump([{'file': f, 'line': l, 'callee': n, 'text': v} for f, l, n, v in sorted(hits)],
                  open(os.path.join(HERE, 'diagarg.json'), 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)
        print("diagarg.json 에 %d건" % len(hits))
        return
    print("=" * 78)
    print("진단 사유 인자 — SHIP 에 잘못 남아 있는 비번역 문자열")
    print("=" * 78)
    for f, l, n, v in sorted(hits):
        print("  %-42s:%-5d %-26s %s" % (f, l, n, v))
    print("\n  ★ DIAG 판정 = %d건" % len(hits))
    print("  (UNKNOWN 호출부 %d종 — 선언을 못 찾았거나 순환. 수동 확인 대상)"
          % len(unknown))
    auto = {(f, l) for f, l, _n, _v in hits}
    ss = [r for r in ship['cs'] if r['verdict'] == 'SHIP']
    man = [r for r in ss if (r['file'], r['line']) in MANUAL_NONTRANSLATABLE
           and (r['file'], r['line']) not in auto]
    print("\n  수동 비번역 목록 적중 = %d건 (목록 %d줄)" % (len(man), len(MANUAL_NONTRANSLATABLE)))
    if not man:
        print("  !! 수동 목록이 하나도 안 맞았다 — 줄 번호가 밀렸다는 뜻이다. 목록을 갱신하라.")
    print("  ★ SHIP %d − 진단사유 %d − 수동 %d = **번역 대상 .cs %d건**"
          % (len(ss), len(hits), len(man), len(ss) - len(hits) - len(man)))
    print("  ★ + .asset %d = **총 %d건** (%s 스냅샷)"
          % (len(ship['asset']), len(ss) - len(hits) - len(man) + len(ship['asset']),
             __import__('time').strftime('%Y-%m-%d %H:%M')))
    print("\n  ※ 이 검출기는 **호출부가 있는** 리터럴만 본다. 대입/배열초기화/식본문에 있는")
    print("     비번역 문자열(폰트명·진단라벨)은 잡지 못한다 — 수동 목록이 따로 필요하다.")


# ------------------------------------------------------------------ 양성 대조
SELF_DIAG = '''
namespace X {
  class A {
    void Caller() { _label.text = "화면 글자"; Close("[✕] 클릭"); }
    private void Close(string source) { Debug.Log($"닫힘({source})"); }
  }
}
'''
SELF_INTERP = '''
namespace X {
  class E {
    void Caller() { _label.text = "화면 글자"; Fire("앱제어 톱니"); }
    private void Fire(string reason) { Debug.Log($"[앱제어] 발동({reason})"); }
  }
}
'''

SELF_SHIP = '''
namespace X {
  class B {
    void Caller() { _label.text = "화면 글자"; Show("안녕하세요"); }
    private void Show(string source) { _label.text = source; }
  }
}
'''
SELF_DEFAULT = '''
namespace X {
  class D {
    void Caller() { _label.text = "화면 글자"; Reg(new Def("장비")); }
  }
  struct Def {
    public readonly string Name;
    public Def(string name, string notice = null) { Name = name; Notice = notice; }
  }
}
'''

SELF_CHAIN = '''
namespace X {
  class C {
    void Caller() { _label.text = "화면 글자"; Outer("전체화면 감지"); }
    private void Outer(string reason) { Inner(reason); }
    private void Inner(string reason) { Debug.Log(reason); }
  }
}
'''


def selftest():
    ok = True
    n = [0]

    def probe(tag, code, target, expect):
        """target = 이 대조가 겨누는 **바로 그 리터럴**. 어느 리터럴을 잰 것인지 출력에 찍는다 —
           '마지막 것'을 잡는 식으로 두면 프로브가 조용히 다른 것을 재고도 초록이 된다."""
        nonlocal ok
        src = {'T.cs': (code, masked_of(code))}
        lits, masked = lex_csharp(code)
        masked = ''.join(masked) if isinstance(masked, list) else masked
        picked = [L for L in lits if L['value'] == target]
        if len(picked) != 1:
            print("  FAIL %-52s 프로브가 대상 리터럴 %r 을 %d개 찾았다(1이어야 한다)"
                  % (tag, target, len(picked)))
            ok = False
            return
        L = picked[0]
        name = enclosing_callee(masked, L['start'])
        got = (name, verdict(src, name) if name else 'NOCALL')
        good = got[1] == expect
        n[0] += 1
        print("  %-4s %-52s %r → %s" % ('PASS' if good else 'FAIL', tag, target, got))
        if not good:
            ok = False

    print("== 양성 대조 ==")
    probe("진단 사유 인자를 실제로 DIAG 로 잡는다", SELF_DIAG, "[✕] 클릭", 'DIAG')
    probe("전이(Outer→Inner)도 따라가 DIAG", SELF_CHAIN, "전체화면 감지", 'DIAG')
    probe("★ 보간 문자열 안에서만 쓰이는 사유도 DIAG", SELF_INTERP, "앱제어 톱니", 'DIAG')
    print("== 음성 대조 (과잉 제외 방지) ==")
    probe("화면에 나가는 인자는 SHIP 으로 남는다", SELF_SHIP, "안녕하세요", 'SHIP')
    probe("★ 같은 소스의 화면 대입 리터럴은 DIAG 가 아니다", SELF_DIAG, "화면 글자", 'NOCALL')
    probe("★ 기본값 파라미터(= null)가 DIAG 오판을 만들지 않는다", SELF_DEFAULT, "장비", 'UNKNOWN')
    if not ok:
        print("\n★★ 대조 실패 — 이 스크립트의 모든 숫자를 폐기한다.")
        sys.exit(1)
    print("\n★ %d/%d 통과." % (n[0], n[0]))


if __name__ == '__main__':
    if '--selftest' in sys.argv:
        selftest()
    else:
        run()
