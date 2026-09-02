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
"""
import os, re, io, json, sys, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
SCRIPTS = os.path.join(ROOT, "Assets", "_Project", "Scripts")
CENSUS = os.path.join(HERE, "census.json")

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


def main():
    d = json.load(io.open(CENSUS, encoding='utf-8'))
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


if __name__ == '__main__':
    main()
