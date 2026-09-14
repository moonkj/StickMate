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

종료 코드: 0 = 통과 / 1 = 대조 실패 / 2 = ★ 판정 불가
           (수동 목록 앵커가 원천에서 정해진 횟수만큼 안 잡힘 · ship.json 이 현재 소스와 다름)

★ 2026-09-15 — **수동 목록 키를 (파일, 줄 번호) → (파일, 문자열 앵커)로 바꿨다.**
  줄 번호 키 17줄이 `ship.json` 재생성 뒤 **적중 0**이 됐는데 rc 0으로 「번역 대상 459」를 냈다
  (경고 한 줄만 찍고 초록 — 거짓 통과 형태). 줄 번호가 이미 **어느 커밋과도 맞지 않는** 상태였고
  (작업 트리 기준으로 적힌 값), 낡은 스냅숏이 우연히 5건을 맞춰 주고 있었다.
  ⇒ 앵커 = 원천 `.cs` 에서 `census.lex_csharp`(ship.json 을 만든 렉서)가 뽑는 리터럴 값.
    각 리터럴이 그 파일에서 **기대 횟수만큼** 안 잡히면 판정 불가. 원천에서 사라진 항목은
    **지우지 않고** 기대 0 + 사라진 커밋으로 남긴다 — 되살아나면 그것도 판정 불가다.
  ⇒ 이 스크립트는 SHIP 리터럴을 ship.json 의 **줄 번호로** 고르므로, ship.json 이 지금
    `ship.run()` 결과와 (파일, 줄, 판정, 문자열)까지 같지 않으면 판정 불가로 끝낸다.
"""
import os, re, sys, json, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
SCRIPTS = os.path.join(ROOT, "Assets", "_Project", "Scripts")
SHIP_JSON = os.path.join(HERE, 'ship.json')
sys.path.insert(0, HERE)
from census import lex_csharp, HANGUL  # noqa

RC_OK, RC_FAIL, RC_UNDECIDABLE = 0, 1, 2

# ============================================================================
# ★ 자동 검출기가 **구조적으로 못 보는** 비번역 문자열 — 수동 목록
# ============================================================================
# 이 검출기는 "호출부가 있는 리터럴"만 본다. 대입 / 배열 초기화 / 식 본문(=>)에 있는
# 비번역 문자열은 감싸는 괄호가 없어 잡히지 않는다. 각 줄에 **왜 번역 대상이 아닌지**를 적는다 —
# 사유 없는 면제는 다음 사람이 지울 수도 되살릴 수도 없다.
#
# 항목: (옛 키 — 추적용, 파일, 앵커 리터럴들, 파일 안 리터럴별 기대 횟수, 사유, 원천에서 사라진 커밋 또는 None)
#   옛 키의 줄 번호는 **판정에 쓰지 않는다**. 2026-09-15 전환 때 각 줄이 가리키던 리터럴을
#   그 줄이 실제로 맞았던 커밋에서 렉서로 복원해 앵커로 옮겼다(괄호 안 = 복원한 커밋).
MANUAL_NONTRANSLATABLE = (
    # 폰트 패밀리 이름 = OS 리소스 식별자. 번역하면 폰트를 못 찾는다. (3694244)
    ('DialogueBubbleRenderer.cs:2342', 'Dialogue/DialogueBubbleRenderer.cs', ('맑은 고딕 Bold',), 1,
     '맑은 고딕 Bold — Windows 폰트 패밀리명', None),
    ('DialogueBubbleRenderer.cs:2363', 'Dialogue/DialogueBubbleRenderer.cs', ('맑은 고딕',), 1,
     '맑은 고딕 — Windows 폰트 패밀리명', None),
    # 글리프 커버리지 프로브 문자열. 한글이 그려지는지 묻는 것이므로 영어로 바꾸면 검사가 무의미해진다. (3694244)
    ('DialogueBubbleRenderer.cs:2429', 'Dialogue/DialogueBubbleRenderer.cs', ('한글',), 1,
     'RequestCharactersInTexture("한글") — 커버리지 프로브', None),
    # 준비 완료 로그의 화자 상태 라벨. (89de9de~da71068)
    ('DialogueBubbleRenderer.cs:937', 'Dialogue/DialogueBubbleRenderer.cs', ('지정됨',), 1,
     '지정됨 — Debug.Log 조립 조각', None),
    ('DialogueBubbleRenderer.cs:938', 'Dialogue/DialogueBubbleRenderer.cs',
     ('미지정(바인딩 전까지 아무것도 그리지 않음)', '미지정(모든 대사 수신)'), 1,
     '미지정(...) — Debug.Log 조립 조각', None),
    # 로그용 종류 라벨(KindLabel). XML 문서가 "로그용"이라고 스스로 적었다. (da71068~3694244)
    ('DialogueBubbleRenderer.cs:1130', 'Dialogue/DialogueBubbleRenderer.cs', ('서술', '반응'), 1,
     '서술/반응 — KindLabel, 로그 전용', None),
    # DescribeSuspendReason() — Suspend/Resume 로그에만 붙는다. (7ed996d)
    ('StickmanAgent.cs:1303', 'Core/StickmanAgent.cs',
     ('전체화면 감지 + 사용자 직접 숨김(둘 다 켜져 있어 한쪽만 풀려도 계속 숨습니다)',), 1,
     'DescribeSuspendReason — 로그 전용', None),
    ('StickmanAgent.cs:1304', 'Core/StickmanAgent.cs', ('사용자 직접 숨김(', ' / 설정창 [일반])'), 1,
     'DescribeSuspendReason — 로그 전용', None),
    ('StickmanAgent.cs:1305', 'Core/StickmanAgent.cs', ('전체화면 앱 감지(자동 숨김, 원칙 2)',), 1,
     'DescribeSuspendReason — 로그 전용', None),
    ('StickmanAgent.cs:1306', 'Core/StickmanAgent.cs', ('두 축 모두 해제',), 1,
     'DescribeSuspendReason — 로그 전용', None),
    # ★ 2026-09-15 추가(리더 조건부 채택 — 반환값이 로그로만 흐를 때만). 호출 사슬 전수(줄 번호):
    #   DescribeSuspendReason(StickmanAgent.cs:1721, 이 리터럴 :1728) → 유일한 호출 :1676 `_lastSuspendReason`
    #   (이 필드는 :1676/:1677/:1683/:1684/:1717 밖에서 안 쓰인다) → Suspend(:1677, 정의 :1816):
    #   `reason` 출현 :1881 Debug.Log · :1827 ExpireUserSummonGrant("등급 2 진입(" + reason + ")")
    #   → 정의 :425, `why` 출현은 :430 Debug.Log 하나뿐 / Resume(:1683, 정의 :1894): :1928 Debug.Log.
    #   사용자 화면 문자열(설정창·툴팁·트레이·말풍선·캡션)로 흐르는 경로 0. 사슬이 바뀌면 이 항목부터 다시 잰다.
    ('(2026-09-15 추가) DescribeSuspendReason 축 4', 'Core/StickmanAgent.cs',
     ('다른 가상 데스크톱(공개 API로 소속만 확인 — 돌아오면 스스로 복귀합니다)',), 1,
     'DescribeSuspendReason — 로그 전용', None),
    # HotkeySource() — ForceTriggerNow(reason) 로 흘러가는 사유 접두사. (1eb0e2b)
    ('AppControlDirector.cs:331', 'Interaction/AppControlDirector.cs', ('전역 단축키 ',), 1,
     'HotkeySource — 사유 접두사', None),
    # TryResolvePlacement 의 out kindLabel — Begin() 로그에만 쓰인다. (7ab0468~4a5a4de)
    ('ArcheryDirector.cs:401', 'Interaction/ArcheryDirector.cs', ('창/Dock 발판', '바탕화면'), 1,
     'kindLabel — Begin() 로그 전용', None),
    # ModeLabel() / CollapseReason* — 접힘 사유 라벨. 로그에만 나간다. (1eb0e2b~7ed996d)
    ('GearRadialMenuWidget.cs:562', 'Interaction/GearRadialMenuWidget.cs', ('이동 시작',), 0,
     'ModeLabel — 접힘 사유, 로그 전용', '0229f52'),   # ★ 원천에서 사라짐 — 같은 Drag 분기가 "앵커 이동"으로 바뀌었다
    # ★ 2026-09-15 추가(리더 채택) — 위 항목의 후속 문구. 근거: 리터럴은 전 원천에 이 파일 1곳(렉서 기준),
    #   ModeLabel 호출부는 GearRadialMenuWidget.cs:942 `Debug.Log($"[부채꼴] 접힘({ModeLabel(mode)}) …")` 1곳뿐.
    ('(2026-09-15 추가) ModeLabel Drag', 'Interaction/GearRadialMenuWidget.cs', ('앵커 이동',), 1,
     'ModeLabel — 접힘 사유, 로그 전용', None),
    ('GearRadialMenuWidget.cs:563', 'Interaction/GearRadialMenuWidget.cs', ('무반응 자동',), 1,
     'ModeLabel — 접힘 사유, 로그 전용', None),
    ('GearRadialMenuWidget.cs:564', 'Interaction/GearRadialMenuWidget.cs', ('사용자 동작',), 1,
     'ModeLabel — 접힘 사유, 로그 전용', None),
    ('TodoPostItWidget.cs:163', 'Interaction/TodoPostItWidget.cs', ('사용자 동작',), 1,
     'CollapseReasonUser — 로그 전용', None),
    ('TodoPostItWidget.cs:164', 'Interaction/TodoPostItWidget.cs', ('무반응 자동',), 1,
     'CollapseReasonAuto — 로그 전용', None),
)


def _source_literals(rel):
    """원천 파일의 리터럴 값 목록(렉서 추출). 파일이 없으면 None."""
    p = os.path.join(SCRIPTS, rel)
    if not os.path.exists(p):
        return None
    with open(p, encoding='utf-8') as fh:
        return [L['value'] for L in lex_csharp(fh.read())[0]]


def resolve_manual(entries=None, lits_of=_source_literals):
    """(면제 앵커 집합 {(파일, 리터럴)}, 문제 목록). 문제가 하나라도 있으면 판정 불가."""
    entries = MANUAL_NONTRANSLATABLE if entries is None else entries
    cache, anchors, problems = {}, set(), []
    for old, f, texts, expected, _why, gone in entries:
        if f not in cache:
            cache[f] = lits_of(f)
        lits = cache[f]
        if lits is None:
            problems.append('%s: 원천 파일 없음 (%s)' % (old, f))
            continue
        cnt = collections.Counter(lits)
        for t in texts:
            n = cnt.get(t, 0)
            if n != expected:
                problems.append('%s: %s 에서 %r 이 %d번 잡힘(기대 %d)%s'
                                % (old, f, t, n, expected,
                                   ' — 「%s에서 사라짐」 기록이 뒤집혔다' % gone if gone else ''))
            if expected > 0:
                anchors.add((f, t))
    return anchors, problems


def manual_exclusions(ss, anchors, auto):
    """SHIP 행 중 수동 앵커에 걸리고 자동 검출기가 이미 잡지 않은 것."""
    return [r for r in ss if (r['file'], r['text']) in anchors and (r['file'], r['line']) not in auto]


def ship_drift(ship):
    """ship.json 과 지금 소스로 `ship.run()`이 낼 결과의 차이. **줄 번호까지** 본다 —
    이 스크립트는 SHIP 리터럴을 줄 번호로 고르기 때문이다. 파일은 쓰지 않는다."""
    import ship as ship_py  # noqa
    import census  # noqa
    rows, _ = ship_py.run()
    key = lambda rs: collections.Counter((e['file'], e['line'], e['verdict'], e['text']) for e in rs)
    akey = lambda rs: collections.Counter((e['file'], e['text']) for e in rs)
    gone, born = key(ship['cs']) - key(rows), key(rows) - key(ship['cs'])
    assets = census.census_assets()
    a_gone, a_born = akey(ship['asset']) - akey(assets), akey(assets) - akey(ship['asset'])
    files = collections.Counter(k[0] for k in list(gone.elements()) + list(born.elements()))
    return {'stale': bool(gone or born or a_gone or a_born), 'gone': sum(gone.values()),
            'born': sum(born.values()), 'asset_gone': sum(a_gone.values()),
            'asset_born': sum(a_born.values()), 'files': files}


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
    if not os.path.exists(SHIP_JSON):
        print("★ 판정 불가 — ship.json 이 없다. `python3 ship.py` 로 만든다.")
        return RC_UNDECIDABLE
    # ★ 싸고 결정적인 가드를 먼저 — 느린 검출(수 분)을 돌리기 전에 끝낸다.
    anchors, problems = resolve_manual()
    if problems:
        print("★ 판정 불가 — 수동 비번역 목록 앵커가 원천에서 정해진 횟수만큼 안 잡힌다:")
        for p in problems:
            print("  " + p)
        print("  (문구가 바뀌었으면 그 항목의 앵커를 새 문구로 옮기되, 사라진 항목은 지우지 말고 기대 0 + 커밋으로 남긴다)")
        return RC_UNDECIDABLE
    ship = json.load(open(SHIP_JSON, encoding='utf-8'))
    drift = ship_drift(ship)
    if drift['stale']:
        print("★ 판정 불가 — ship.json 이 현재 소스와 다르다(줄 번호 포함). `python3 ship.py` 로 다시 만든 뒤 재실행한다.")
        print("  cs 행 사라짐 %d / 생김 %d · asset 사라짐 %d / 생김 %d · 달라진 파일 %d개"
              % (drift['gone'], drift['born'], drift['asset_gone'], drift['asset_born'], len(drift['files'])))
        for f, c in drift['files'].most_common(5):
            print("    %5d  %s" % (c, f))
        return RC_UNDECIDABLE

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
        return RC_OK
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
    man = manual_exclusions(ss, anchors, auto)

    # ★ 항목별로 무엇을 했는지 전부 찍는다 — 「적중 0」이 «면제할 것이 없음»인지 «니들이 죽었음»인지
    #   출력만으로 갈라야 한다(앵커는 위에서 이미 실재가 확인됐다).
    print("\n  수동 비번역 목록 %d항목 — 항목별 기여 (원천 앵커 확인됨)" % len(MANUAL_NONTRANSLATABLE))
    print("    %-32s %4s %4s %4s  %s" % ('옛 키(추적용)', 'SHIP', '자동', '제외', '앵커'))
    for old, f, texts, expected, why, gone in MANUAL_NONTRANSLATABLE:
        rows = [r for r in ss if r['file'] == f and r['text'] in texts]
        absorbed = [r for r in rows if (r['file'], r['line']) in auto]
        excl = [r for r in man if r['file'] == f and r['text'] in texts]
        note = (' ★ 원천에서 사라짐 — %s' % gone) if gone else (
            '' if rows else '  (SHIP 아님 — ship.py 가 이미 거른다)')
        print("    %-32s %4d %4d %4d  %s%s" % (old, len(rows), len(absorbed), len(excl),
                                            ' / '.join(repr(t) for t in texts), note))
    print("\n  수동 비번역 목록 적중 = %d건 (목록 %d항목)" % (len(man), len(MANUAL_NONTRANSLATABLE)))
    print("  ★ SHIP %d − 진단사유 %d − 수동 %d = **번역 대상 .cs %d건**"
          % (len(ss), len(hits), len(man), len(ss) - len(hits) - len(man)))
    print("  ★ + .asset %d = **총 %d건** (%s 스냅샷)"
          % (len(ship['asset']), len(ss) - len(hits) - len(man) + len(ship['asset']),
             __import__('time').strftime('%Y-%m-%d %H:%M')))
    print("\n  ※ 이 검출기는 **호출부가 있는** 리터럴만 본다. 대입/배열초기화/식본문에 있는")
    print("     비번역 문자열(폰트명·진단라벨)은 잡지 못한다 — 수동 목록이 따로 필요하다.")
    return RC_OK


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
    undecidable = False
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

    def chk(tag, cond, detail='', undec=False):
        nonlocal ok, undecidable
        n[0] += 1
        print("  %-4s %-52s %s" % ('PASS' if cond else ('판정불가' if undec else 'FAIL'), tag, detail))
        if not cond:
            if undec:
                undecidable = True
            else:
                ok = False

    print("== 양성 대조 ==")
    probe("진단 사유 인자를 실제로 DIAG 로 잡는다", SELF_DIAG, "[✕] 클릭", 'DIAG')
    probe("전이(Outer→Inner)도 따라가 DIAG", SELF_CHAIN, "전체화면 감지", 'DIAG')
    probe("★ 보간 문자열 안에서만 쓰이는 사유도 DIAG", SELF_INTERP, "앱제어 톱니", 'DIAG')
    print("== 음성 대조 (과잉 제외 방지) ==")
    probe("화면에 나가는 인자는 SHIP 으로 남는다", SELF_SHIP, "안녕하세요", 'SHIP')
    probe("★ 같은 소스의 화면 대입 리터럴은 DIAG 가 아니다", SELF_DIAG, "화면 글자", 'NOCALL')
    probe("★ 기본값 파라미터(= null)가 DIAG 오판을 만들지 않는다", SELF_DEFAULT, "장비", 'UNKNOWN')

    # ---- 수동 목록 앵커 (2026-09-15) — 실제 원천에 대고 잰다. 변이는 메모리 사본만 ----
    print("== 수동 목록 앵커 (원천 실재 + 변이 대조) ==")
    anchors, problems = resolve_manual()
    chk("원천 앵커 — %d항목 전부 기대 횟수만큼 잡힌다" % len(MANUAL_NONTRANSLATABLE),
        not problems, '; '.join(problems) or '앵커 %d개' % len(anchors), undec=True)
    live = [e for e in MANUAL_NONTRANSLATABLE if e[3] > 0]
    gone = [e for e in MANUAL_NONTRANSLATABLE if e[5]]
    chk("★ 양성 — 살아 있는 항목과 사라짐 기록 항목이 둘 다 실재한다(빈 목록 초록 방지)",
        len(live) > 0 and len(gone) > 0, '살아 있음 %d · 사라짐 기록 %d' % (len(live), len(gone)))
    if live and not problems:
        e0 = live[0]
        path0 = os.path.join(SCRIPTS, e0[1])
        with open(path0, encoding='utf-8') as fh:
            src0 = fh.read()

        def lits_with(mutated):
            return lambda rel: ([L['value'] for L in lex_csharp(mutated)[0]] if rel == e0[1]
                                else _source_literals(rel))

        _, p1 = resolve_manual(lits_of=lits_with('\n\n\n' + src0))
        chk("★ 변이 ① 줄만 밀림(파일 앞 빈 줄 3개) → 앵커 그대로(판정 가능)", not p1, '; '.join(p1) or e0[0])
        needle = '"%s"' % e0[2][0]
        if needle in src0:
            _, p2 = resolve_manual(lits_of=lits_with(src0.replace(needle, '"%s변이"' % e0[2][0], 1)))
            chk("★ 변이 ② 앵커 문자열이 원천에서 바뀜 → 판정 불가", bool(p2), '; '.join(p2) or '(문제 없음 = 앵커가 죽어 있다)')
        else:
            chk("★ 변이 ② 앵커 리터럴을 원천에서 찾아야 변이를 심을 수 있다", False, needle, undec=True)
        if os.path.exists(SHIP_JSON):
            ship = json.load(open(SHIP_JSON, encoding='utf-8'))
            ss = [r for r in ship['cs'] if r['verdict'] == 'SHIP']
            full = manual_exclusions(ss, anchors, set())
            contrib = {e[0]: len([r for r in full if r['file'] == e[1] and r['text'] in e[2]]) for e in live}
            victim = max(live, key=lambda e: contrib[e[0]])
            rest, _ = resolve_manual(entries=[e for e in MANUAL_NONTRANSLATABLE if e is not victim])
            # 같은 파일·같은 문구를 다른 항목이 겹쳐 들고 있지 않은 경우에만 «정확히 기여만큼»이 성립한다
            after = manual_exclusions(ss, rest, set())
            chk("★ 변이 ③ 항목 삭제(%s) → 수동 제외가 그 항목 기여만큼 준다(= 번역 대상이 그만큼 는다)" % victim[0],
                contrib[victim[0]] > 0 and len(full) - len(after) == contrib[victim[0]],
                '전체 %d → 삭제 후 %d, 기여 %d (자동 흡수 무시한 앵커 층 계산)'
                % (len(full), len(after), contrib[victim[0]]))
        else:
            chk("★ 변이 ③ ship.json 이 있어야 잴 수 있다", False, '', undec=True)

    if not ok:
        print("\n★★ 대조 실패 — 이 스크립트의 모든 숫자를 폐기한다.")
        return RC_FAIL
    if undecidable:
        print("\n★ 판정 불가 — 위 표시 항목을 먼저 해소하라.")
        return RC_UNDECIDABLE
    print("\n★ %d/%d 통과." % (n[0], n[0]))
    return RC_OK


if __name__ == '__main__':
    if '--selftest' in sys.argv:
        sys.exit(selftest())
    else:
        sys.exit(run())
