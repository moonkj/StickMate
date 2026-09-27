#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
census.py의 OTHER 1125건을 **출하 여부**로 다시 가른다.

왜 필요한가: census.py의 OTHER는 "인스펙터도 Debug.Log도 아닌 것"일 뿐이다.
그런데 `Platform/OverlayCompositionSnapshot.cs`의 92건처럼 **진단 리포트 문자열**은
호출부(`WindowsCompositionProbe`)에서 `Debug.LogWarning(sb.ToString())`으로 끝난다 —
`Debug.Log("...")` 직접 호출이 아니라서 census.py가 못 거른다.

그래서 여기서는 **싱크 도달성**으로 가른다. 판정 근거를 파일마다 출력한다(추측 금지).

  UI   : 그 파일이 UnityEngine.UI 텍스트 싱크를 직접 쓴다
  DLG  : 그 파일이 DialogueIntent / DialogueLine 을 만든다(말풍선 파이프라인)
  DATA : UI/DLG 파일이 그 파일의 타입을 호출해 **문자열을 받아 간다**(enum->이름 테이블 등)
  DIAG : 위 어느 것도 아니다 = 로그/리포트/예외 메시지. 출하 UI가 아니다.

DATA 판정은 **호출 근거(어느 UI 파일이 어느 타입을 부르는가)를 함께 출력**한다.

사용:  python3 tier.py            # 재분류 + tier.json 쓰기
       python3 tier.py --selftest # 입력 신선도 + 변이 대조
종료 코드: 0 = 통과 / 1 = 대조 실패 / 2 = ★ 판정 불가(census.json 이 현재 소스와 다름 — 이때 tier.json 은 쓰지 않는다)

★ 2026-09-15 — **입력 스냅숏 신선도 가드**(ebscope.py 와 같은 형태). `census.json`은 `census.py`가 쓰는 스냅숏인데
  `1eb0e2b`(2026-09-03) 이후 한 번도 다시 만들어지지 않아 **110개 파일이 갈라진 채** 이 스크립트의 입력이었다.
  입력은 낡았는데 UI/DLG/DATA 판정은 **현재 소스**로 하므로, 옛 행 수와 새 판정이 섞인 숫자가 rc 0으로 나왔다.
  ⇒ `census.json`이 지금 `census.census_cs()` · `census_assets()`가 낼 결과와 다르면 판정 불가로 끝내고
    **tier.json 을 덮어쓰지 않는다.** 비교 키는 (파일, 분류, 문자열) — 줄 번호만 밀린 것은 낡음으로 치지 않는다
    (이 스크립트는 파일별 행 수만 쓰기 때문이다. selftest 변이 ④가 그 선택을 못박는다).
  ※ 이 도구는 `PLAN_1.0.md` 표에서 «폐기된 접근»으로 분류돼 있다. 가드는 폐기 판정을 뒤집지 않는다 —
    돌렸을 때 낡은 입력으로 조용히 초록이 되지 않게 할 뿐이다.
"""
import os, re, io, json, sys, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
SCRIPTS = os.path.join(ROOT, "Assets", "_Project", "Scripts")
CENSUS = os.path.join(HERE, "census.json")

RC_OK, RC_FAIL, RC_UNDECIDABLE = 0, 1, 2

sys.path.insert(0, HERE)
from census import lex_csharp, HANGUL  # noqa


def rel(p):
    return os.path.relpath(p, SCRIPTS).replace(os.sep, '/')


def all_files():
    out = []
    for dp, dn, fn in os.walk(SCRIPTS):
        if os.sep + 'Tests' in dp + os.sep:
            continue
        for f in sorted(fn):
            if f.endswith('.cs'):
                out.append(os.path.join(dp, f))
    return out


SRC = {}
for p in all_files():
    SRC[rel(p)] = io.open(p, encoding='utf-8').read()

# 주석/문자열을 제거한 코드 본문(참조 탐지용) — 주석 안의 <see cref="X"/>를 호출로 오인하지 않기 위해
CODE = {}
for k, v in SRC.items():
    _, masked = lex_csharp(v)
    CODE[k] = masked

UI_FILES = sorted(k for k, v in CODE.items()
                  if re.search(r'\busing\s+UnityEngine\.UI\b|\bUnityEngine\.UI\.Text\b', v))
DLG_FILES = sorted(k for k, v in CODE.items()
                   if re.search(r'\bDialogueLine\s*\.\s*(Say|React)\b|new\s+DialogueLine\b|new\s+DialogueIntent\b|DialogueIntent\s*\(', v))

TYPE_DECL = re.compile(r'\b(?:public|internal)\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+)*'
                       r'(?:class|struct|enum|interface)\s+([A-Za-z_][A-Za-z_0-9]*)')

DECLS = {}
for k, v in CODE.items():
    DECLS[k] = set(TYPE_DECL.findall(v))

SEED = set(UI_FILES) | set(DLG_FILES)


def referenced_from_seed(fname):
    """이 파일이 선언한 타입을 UI/DLG 파일이 부르는가. (타입, 부르는 파일) 근거를 돌려준다."""
    ev = []
    for t in sorted(DECLS.get(fname, ())):
        if len(t) < 4:
            continue
        pat = re.compile(r'\b' + re.escape(t) + r'\s*\.')
        for s in sorted(SEED):
            if s == fname:
                continue
            if pat.search(CODE[s]):
                ev.append((t, s))
                break
    return ev


# ---------------------------------------------------------------- 입력 신선도
def fresh_census():
    """지금 소스로 census.py 가 낼 결과(메모리). census.json 은 쓰지 않는다(쓰기는 census.main 에만 있다)."""
    import census  # noqa
    return {'cs': census.census_cs(include_tests=False), 'asset': census.census_assets()}


def drift_between(old, new):
    """(파일, 분류, 문자열) 멀티셋 차이. 줄 번호는 비교하지 않는다."""
    k_cs = lambda rs: collections.Counter((e['file'], e['kind'], e['text']) for e in rs)
    k_as = lambda rs: collections.Counter((e['file'], e['text']) for e in rs)
    gone, born = k_cs(old['cs']) - k_cs(new['cs']), k_cs(new['cs']) - k_cs(old['cs'])
    a_gone, a_born = k_as(old['asset']) - k_as(new['asset']), k_as(new['asset']) - k_as(old['asset'])
    files = collections.Counter(k[0] for k in list(gone.elements()) + list(born.elements()))
    files.update(k[0] for k in list(a_gone.elements()) + list(a_born.elements()))
    return {'stale': bool(gone or born or a_gone or a_born),
            'gone': sum(gone.values()), 'born': sum(born.values()),
            'asset_gone': sum(a_gone.values()), 'asset_born': sum(a_born.values()),
            'files': files,
            # ★ 규칙 24(2026-09-27) — 합계만 남기면 「계수는 같은데 내용이 다른」 변경을 못 본다.
            'gone_rows': gone, 'born_rows': born,
            'asset_gone_rows': a_gone, 'asset_born_rows': a_born}


def drift_lines(d):
    out = ['cs 행 사라짐 %d / 생김 %d · asset 사라짐 %d / 생김 %d · 달라진 파일 %d개'
           % (d['gone'], d['born'], d['asset_gone'], d['asset_born'], len(d['files']))]
    for f, c in d['files'].most_common(5):
        out.append('  %5d  %s' % (c, f))
    # ★ 규칙 24 — 키 집합을 함께 찍는다(공용 구현: driftkeys.py). 차이가 0이면 아무 줄도 늘지 않는다.
    import driftkeys  # noqa
    empty = collections.Counter()
    out.extend(driftkeys.key_lines(d.get('gone_rows', empty), d.get('born_rows', empty),
                                   label='cs 키(파일, 분류, 문자열)'))
    out.extend(driftkeys.key_lines(d.get('asset_gone_rows', empty), d.get('asset_born_rows', empty),
                                   label='.asset 키(파일, 문자열)'))
    return out


def load_census():
    return json.load(io.open(CENSUS, encoding='utf-8'))


# ---------------------------------------------------------------- 재분류
def main():
    if not os.path.exists(CENSUS):
        print('★ 판정 불가 — census.json 이 없다. `python3 census.py` 로 만든다.')
        return RC_UNDECIDABLE
    d = load_census()
    drift = drift_between(d, fresh_census())
    if drift['stale']:
        print('★ 판정 불가 — census.json 이 현재 소스와 다르다(낡은 스냅숏). `python3 census.py` 로 다시 만든 뒤 재실행한다.')
        for ln in drift_lines(drift):
            print('  ' + ln)
        print('  tier.json 은 쓰지 않았다(낡은 입력으로 덮어쓰지 않는다).')
        return RC_UNDECIDABLE

    rows = [r for r in d['cs'] if r['kind'] == 'OTHER']
    per = collections.defaultdict(list)
    for r in rows:
        per[r['file'].replace('Assets/_Project/Scripts/', '')].append(r)

    tiers = collections.defaultdict(list)
    evidence = {}
    for f, rs in per.items():
        if f in UI_FILES:
            t = 'UI'
        elif f in DLG_FILES:
            t = 'DLG'
        else:
            ev = referenced_from_seed(f)
            if ev:
                t = 'DATA'
                evidence[f] = ev
            else:
                t = 'DIAG'
        tiers[t].append((f, len(rs)))

    print('=' * 78)
    print('출하 여부 재분류  (census.py OTHER %d건)' % len(rows))
    print('=' * 78)
    print('UI 싱크 파일 %d개 / DLG 생산 파일 %d개' % (len(UI_FILES), len(DLG_FILES)))
    print()
    order = ['UI', 'DLG', 'DATA', 'DIAG']
    tot = {}
    for t in order:
        n = sum(c for _, c in tiers[t])
        tot[t] = n
        print('--- %s : %d건 / %d파일 ---' % (t, n, len(tiers[t])))
        for f, c in sorted(tiers[t], key=lambda kv: -kv[1]):
            extra = ''
            if t == 'DATA':
                e = evidence[f][0]
                extra = '   <- %s 를 %s 가 호출' % (e[0], e[1])
            print('   %5d  %s%s' % (c, f, extra))
        print()

    ship = tot['UI'] + tot['DLG'] + tot['DATA']
    print('=' * 78)
    print('★ 출하 UI/대사 표면(.cs) = UI %d + DLG %d + DATA %d = %d건' % (tot['UI'], tot['DLG'], tot['DATA'], ship))
    print('★ 진단/로그/예외(비출하) = %d건' % tot['DIAG'])
    print('★ .asset(디코드 후)      = %d건' % len(d['asset']))
    print('★ 1.0 번역 대상 합계      = %d건' % (ship + len(d['asset'])))
    print('=' * 78)

    with io.open(os.path.join(HERE, 'tier.json'), 'w', encoding='utf-8') as fh:
        fh.write(json.dumps({t: sorted(tiers[t], key=lambda kv: -kv[1]) for t in order},
                            ensure_ascii=False, indent=1))
    return RC_OK


# ---------------------------------------------------------------- 대조
def selftest():
    state = {'fail': False, 'undecidable': False}

    def chk(name, cond, detail='', undecidable=False):
        tag = '  PASS  ' if cond else ('  판정불가 ' if undecidable else '  FAIL  ')
        print(tag + name + ('   ' + detail if detail else ''))
        if not cond:
            state['undecidable' if undecidable else 'fail'] = True

    chk('census.json 이 있다', os.path.exists(CENSUS), undecidable=True)
    if not os.path.exists(CENSUS):
        return RC_UNDECIDABLE
    d = load_census()
    fresh = fresh_census()   # 한 번만 센다(수 초) — 아래 변이 대조가 전부 이 값을 재사용한다

    real = drift_between(d, fresh)
    chk('★ 입력 신선도 — census.json 이 지금 소스로 census.py 가 낼 결과와 같다',
        not real['stale'], '\n           '.join(drift_lines(real)), undecidable=True)
    chk('★ 규칙 24 — 차이 dict가 키 집합을 실제로 들고 있다(합계만 남기지 않았다)',
        'gone_rows' in real and 'born_rows' in real
        and 'asset_gone_rows' in real and 'asset_born_rows' in real)

    # ---- ★ 변이 대조 — 가드가 «살아 있는 초록/빨강»인가(메모리 사본만 바꾼다) ----
    def copy(x):
        return {'cs': [dict(e) for e in x['cs']], 'asset': [dict(e) for e in x['asset']]}

    others = [i for i, e in enumerate(fresh['cs']) if e['kind'] == 'OTHER']
    chk('★ 양성 — 현재 소스에 OTHER 행과 UI 싱크 파일이 실재한다(빈 입력으로 초록이 되지 않는다)',
        len(others) > 0 and len(UI_FILES) > 0, 'OTHER %d · UI 파일 %d' % (len(others), len(UI_FILES)))
    same = drift_between(copy(fresh), fresh)
    chk('★ 변이 ① 무변경 사본 → 낡음 아님', not same['stale'], drift_lines(same)[0])
    if others:
        m = copy(fresh)
        m['cs'][others[0]]['text'] = m['cs'][others[0]]['text'] + u' 변이'
        dm = drift_between(m, fresh)
        chk('★ 변이 ② 리터럴 한 개의 글자를 바꾸면 → 낡음(사라짐 1 · 생김 1)',
            dm['stale'] and dm['gone'] == 1 and dm['born'] == 1, drift_lines(dm)[0])
        chk('★ 규칙 24 양성 — 그 변이가 키 목록에 글자로 찍힌다(합계 차는 0인데도)',
            dm['gone'] == dm['born'] and any(u'변이' in ln for ln in drift_lines(dm)[1:]),
            '키 줄 %d개' % len(drift_lines(dm)[1:]))
        ml = copy(fresh)
        ml['cs'][others[0]]['line'] = ml['cs'][others[0]]['line'] + 7
        dl = drift_between(ml, fresh)
        chk('★ 변이 ④ 줄 번호만 밀리면 → 낡음 아님(파일별 행 수만 쓰는 도구라서)', not dl['stale'], drift_lines(dl)[0])
    if fresh['asset']:
        ma = copy(fresh)
        ma['asset'].pop()
        da = drift_between(ma, fresh)
        chk('★ 변이 ③ .asset 한 건이 빠지면 → 낡음(asset 생김 1)',
            da['stale'] and da['asset_born'] == 1 and da['gone'] == 0, drift_lines(da)[0])
    else:
        chk('★ 변이 ③ .asset 표본이 있어야 잴 수 있다', False, '.asset 0건', undecidable=True)

    return RC_UNDECIDABLE if state['undecidable'] else (RC_FAIL if state['fail'] else RC_OK)


if __name__ == '__main__':
    if '--selftest' in sys.argv:
        sys.exit(selftest())
    sys.exit(main())
