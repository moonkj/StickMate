#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""★ 규칙 24 — 계수만 보는 검사는 눈이 없다. 「무엇이 바뀌었는지의 키 집합」을 함께 찍는다.

왜 이 파일이 생겼는가 (2026-09-27 localization)
------------------------------------------------
트리 7파일이 바뀌었는데 **출하 계수는 사라짐 0 · 생김 0**이었다(교체분이 전부 인스펙터 1 + Debug.Log 6).
같은 날 `design-narrative`는 **두 캡션의 마지막 절이 한 글자도 다르지 않아** 요약 숫자 네 개가
하나도 움직이지 않는 사례를 냈다. 두 사건의 형태가 같다 — **합계는 눈이 없다.**

⇒ 처방: 먼저 **키의 사라짐·생김 목록**을 찍고, **합계는 마지막에** 본다.
   키는 이 저장소의 관례대로 `(파일, 앵커, 기대)` 꼴이다(줄 번호를 키로 쓰지 않는다 —
   `docs/TEAM.md` 「기준과 대상이 같이 낡은 스냅숏」 규칙 3).

`tier.py` · `ebscope.py` · `diagarg.py` 세 판정 스크립트가 **이 한 구현을 공유한다.**
같은 출력 규칙을 세 곳에 베끼면 세 곳이 따로 썩는다(이 저장소가 반복해서 당한 형태다).

사용:  python3 driftkeys.py --selftest
종료 코드: 0 = 통과 / 1 = 대조 실패
"""
from __future__ import print_function

import collections
import sys

CAP = 30           # 출력 상한. 넘으면 「그리고 N개 더」로 끝낸다(출력이 폭발하지 않게).
WIDTH = 70         # 키 한 조각의 표시 상한. 넘으면 잘림 표시를 붙인다.


def _short(part, width=WIDTH):
    s = part if isinstance(part, str) else repr(part)
    return s if len(s) <= width else s[:width] + u'…'


def _sort_key(k):
    return tuple(str(p) for p in k)


def key_lines(gone, born, cap=CAP, label=u'키'):
    """사라짐/생김 키 Counter 두 개를 사람이 읽는 줄 목록으로 바꾼다.

    gone · born : 키 -> 개수 Counter. 키는 튜플(예: (파일, 분류, 문자열)).
    둘 다 비어 있으면 **빈 목록**을 돌려준다 — 없는 변화를 지어내지 않는다.
    """
    total = sum(gone.values()) + sum(born.values())
    if not total:
        return []
    out = [u'★ 바뀐 %s %d개 — 판정 근거는 이 목록이다(합계는 요약일 뿐이다)' % (label, total)]
    shown = 0
    for sign, cnt in ((u'-', gone), (u'+', born)):
        for k in sorted(cnt, key=_sort_key):
            if shown >= cap:
                out.append(u'    … 그리고 %d개 더(출력 상한 %d)' % (total - shown, cap))
                return out
            out.append(u'    %s %s' % (sign, u' | '.join(_short(p) for p in k)))
            shown += cnt[k]
    return out


# ---------------------------------------------------------------- 대조
def selftest():
    ok = [True]

    def chk(name, cond, detail=''):
        print((u'  PASS  ' if cond else u'  FAIL  ') + name + (u'   ' + detail if detail else u''))
        if not cond:
            ok[0] = False

    empty = collections.Counter()
    chk(u'★ 음성 — 차이가 0이면 줄이 0개다(없는 변화를 지어내지 않는다)',
        key_lines(empty, empty) == [])

    gone = collections.Counter({('A.cs', 'DEBUG', u'옛 원문'): 1})
    born = collections.Counter({('A.cs', 'DEBUG', u'심은 변이'): 1})
    lines = key_lines(gone, born)
    body = u'\n'.join(lines)
    chk(u'★ 양성 — 사라짐·생김이 둘 다 글자로 실린다', u'옛 원문' in body and u'심은 변이' in body,
        u'%d줄' % len(lines))
    chk(u'★ 양성 — 부호가 갈린다(- 사라짐 / + 생김)',
        any(ln.strip().startswith(u'- ') and u'옛 원문' in ln for ln in lines)
        and any(ln.strip().startswith(u'+ ') and u'심은 변이' in ln for ln in lines))

    # ★ 이 저장소가 당한 바로 그 형태 — 합계는 한 글자도 안 움직이는데 내용이 다르다.
    chk(u'★ 계수 불변 대조 — 사라짐 1 · 생김 1이라 합계 차가 0인데도 키는 찍힌다',
        sum(gone.values()) == sum(born.values()) and len(lines) >= 3)

    many = collections.Counter({('F%02d.cs' % i, 'DEBUG', u't%d' % i): 1 for i in range(40)})
    capped = key_lines(many, empty, cap=5)
    chk(u'★ 상한 — 넘치면 「그리고 N개 더」로 끝난다(출력이 폭발하지 않는다)',
        any(u'개 더' in ln for ln in capped) and len(capped) <= 8, u'%d줄' % len(capped))

    longish = collections.Counter({('X.cs', 'DEBUG', u'가' * 200): 1})
    chk(u'★ 긴 문자열은 잘리고 잘림 표시가 붙는다',
        any(u'…' in ln for ln in key_lines(longish, empty)))

    # 튜플 조각이 문자열이 아니어도 죽지 않는다(줄 번호 키를 실수로 넘겨도 판정이 서야 한다).
    mixed = collections.Counter({('X.cs', 12, None): 1})
    chk(u'★ 문자열이 아닌 조각도 표시된다(프로브가 예외로 죽지 않는다)',
        bool(key_lines(mixed, empty)))

    return 0 if ok[0] else 1


if __name__ == '__main__':
    if '--selftest' in sys.argv:
        sys.exit(selftest())
    print(__doc__)
    sys.exit(0)
