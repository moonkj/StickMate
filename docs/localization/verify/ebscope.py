#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""E-B(결제 경로 UI) 부분집합 산정 — `product-strategy` ROADMAP §12-7 요청분.

> §12-7: *"지금 아무도 이 부분집합을 세지 않았다 — `localization`의 파일별 표는 **파일 단위**라
>          「결제 경로」라는 축으로 갈려 있지 않다."*
> §14 다음 회차 질문 3: *"`localization`이 E-B(결제 경로 부분집합)를 세었는가"*

**이 스크립트는 세는 것이 아니라 「가르는 것」이다.** 숫자는 전부 `ship.json`(=`ship.py`의 산출물)에서
읽는다. 여기서 다시 세면 «생성기와 검사기가 같이 틀린다»(TEAM.md 열 번째 형태)가 된다.

E-B의 정의 — ROADMAP §12-7의 네 낱말(**상점 · 팩 상세 · 구매 확인 · 보관함**)을 우리 코드 표면에 대입한다.
  ★ 판정 기준 한 줄: **「이 문장을 못 읽으면 유저가 무엇을 사는지 / 샀는지 알 수 없는가」**
    - 그렇다  → E-B
    - 아니다  → E-C(나머지 UI)  ※ E-C도 1.0에 필요하다. 등급이 다를 뿐이다(§12-7).

두 층으로 나눠 낸다. **한 숫자로 뭉치지 마라** — 소관도 비용 성격도 다르다:
  (1) 크롬(CHROME)  — 상태·등급·카테고리·탭 라벨. 기계적 번역으로 충분. `ux-designer` 검수.
  (2) 콘텐츠(CONTENT) — 아이템/행동의 이름과 설명. **재창작에 가깝고 스토어 문안과 어휘를 공유한다**
      (PLAN §5-3: *"`ItemCatalog`의 행동 12종 설명이 곧 스토어의 기능 목록이다"*).

사용:  python3 ebscope.py            # 산정
       python3 ebscope.py --selftest # 대조(교정이 깨지면 아래 숫자를 전부 폐기한다)
종료 코드: 0 = 통과 / 1 = 대조 실패(FAIL) / 2 = ★ 판정 불가
           (원천 앵커가 안 잡힘 · ship.json 이 현재 소스와 다름 — 이때 찍힌 숫자는 인용하지 마라)

★ 2026-09-15 정정 — **죽은 니들 1건 + 낡은 스냅숏이 초록을 만들고 있었다.**
  - `ITEMCATALOG_CHROME`에 화면 문구를 **글자 그대로** 베껴 두었다(`'톱니 메뉴에서'`). `eb4670d`에서
    `ItemCatalog.MenuOnlyStatus`가 `"캐릭터 우클릭"`으로 바뀌었는데 `ship.json`(마지막 생성 `0229f52`)이
    옛 문자열을 들고 있어 「죽은 니들 방지」 검사가 **낡은 스냅숏 위에서 초록**이었다. 존재 대조가
    **원천이 아니라 같은 낡은 스냅숏**을 보고 있었기 때문이다(기준과 대상이 같이 낡았다).
    스냅숏을 다시 만들면 그 검사는 빨개지지만, **본실행은 rc 0인 채로 새 문구를 CONTENT로 조용히 옮겨 담는다**
    (CHROME 36→33 중 1건이 이 원인) — 시끄러운 쪽과 조용한 쪽이 동시에 있었다.
  - 교정 ③의 `상점은 다음 업데이트에 들어옵니다.`도 `349048f`(2026-09-06)에 소스에서 지워진 문장이었다.
  ⇒ 니들을 **프로덕션 원천의 식별자 앵커에서 읽는다**(CLAUDE.md «테스트에 프로덕션 상수·식별자를
    문자열로 베끼지 않는다»). 앵커가 정해진 횟수만큼 안 잡히면 판정 불가다.
  ⇒ **`ship.json`이 지금 `ship.py`가 낼 결과와 다르면 판정 불가**로 끝낸다(스냅숏 신선도 가드).
"""
from __future__ import print_function

import collections
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
SHIP_JSON = os.path.join(HERE, 'ship.json')
SCRIPTS = os.path.join(ROOT, 'Assets', '_Project', 'Scripts')

RC_OK, RC_FAIL, RC_UNDECIDABLE = 0, 1, 2

# ---------------------------------------------------------------- 표면 정의
#
# ★ 파일 전량이 E-B인 표면. 근거를 한 줄씩 단다 — 근거 없는 칸을 만들지 않는다.
WHOLE_FILE = {
    'Interaction/CharacterInfoWindow.Tabs.cs':
        ('CHROME', '탭 라벨 4종 + [상점] 본문 문구. 「상점」/「보관함」에 들어가는 유일한 입구다'),
    'Interaction/CharacterInfoWindow.Inventory.cs':
        ('CHROME', '보관함 목록 자체(§12-7의 「보관함」)'),
    'Interaction/CharacterInfoWindow.Cards.cs':
        ('CHROME', '카드의 소유/착용/잠김 표기 — 「샀는가」를 유저가 읽는 자리'),
    'Core/EquipmentModel.cs':
        ('CHROME', '카테고리 라벨 7종. 상점/보관함 양쪽의 섹션 제목이다'),
}

# ★ 파일 일부만 E-B인 표면 — `Core/ItemCatalog.cs`의 상태·등급·카테고리 크롬.
#   **화면 문구를 여기 적지 않는다**(2026-09-15 정정). 문구는 바뀌고 식별자는 남는다 —
#   `MenuOnlyStatus` 선언부 주석이 스스로 «식별자는 바꾸지 않는다(니들 보호)»라고 적고 있다.
#   각 앵커: (이름, 원천 파일, 정규식(캡처 그룹 = 문구), 정확히 잡혀야 하는 횟수)
#   횟수가 어긋나면 «넓어졌다/사라졌다» 어느 쪽이든 판정 불가 — 조용히 넓어지거나 비지 않게.
ITEMCATALOG_FILE = 'Core/ItemCatalog.cs'
_S = r'"([^"\\]*)"'  # 이스케이프 없는 일반 문자열 리터럴 한 개
ITEMCATALOG_CHROME_ANCHORS = (
    ('AutoOnlyStatus 상수', ITEMCATALOG_FILE,
     r'\bconst\s+string\s+AutoOnlyStatus\s*=\s*' + _S + r'\s*;', 1),
    ('MenuOnlyStatus 상수', ITEMCATALOG_FILE,
     r'\bconst\s+string\s+MenuOnlyStatus\s*=\s*' + _S + r'\s*;', 1),
    ('CategoryLabel — 행동일 때 부제', ITEMCATALOG_FILE,
     r'\bCategoryLabel\s*=>[^;]*:\s*' + _S + r'\s*;', 1),
    ('상태 — 미보유(잠김)', ITEMCATALOG_FILE,
     r'!\s*IsOwned\s*\(\s*config\s*\)\s*\)\s*return\s*\$' + _S + r'\s*;', 1),
    ('상태 — 착용 중 / 보유', ITEMCATALOG_FILE,
     r'\bIsEquipped\s*\(\s*\)\s*\?\s*' + _S + r'\s*:\s*' + _S + r'\s*;', 1),
    ('등급 라벨 4종', ITEMCATALOG_FILE,
     r'\bcase\s+ItemRarity\s*\.\s*(?:Common|Rare|Epic|Legendary)\s*:\s*return\s*' + _S + r'\s*;', 4),
)

# 교정·대조에 쓰는 문구도 원천에서 읽는다(같은 이유).
TABS_FILE = 'Interaction/CharacterInfoWindow.Tabs.cs'
TAB_DEF_RE = re.compile(r'\bnew\s+TabDef\s*\(\s*' + _S + r'\s*,\s*TabPage\s*\.\s*([A-Za-z_]\w*)')
IDLE_CAPTION_ANCHOR = ('상태 캡션 — Idle', 'Interaction/CharacterInfoWindow.cs',
                       r'\bcase\s+StickmanStateId\s*\.\s*Idle\s*:\s*return\s*' + _S + r'\s*;', 1)

# `.asset` 84건 = 장비/외형 42종의 이름+설명. **팩이 파는 물건 그 자체**다.
ASSET_LAYER = ('CONTENT', '장비·외형 42종 이름+설명. DLC 팩이 싣는 물건 그 자체(§10-4)')


# ---------------------------------------------------------------- 원천 읽기
def _read_source(rel):
    path = os.path.join(SCRIPTS, rel)
    if not os.path.exists(path):
        return None
    with io.open(path, encoding='utf-8') as fh:
        return fh.read()


def resolve_anchor(anchor, src_override=None):
    """(값 목록, 문제 문자열 또는 None). src_override 는 selftest 변이 대조용."""
    name, rel, pattern, expected = anchor
    src = src_override if src_override is not None else _read_source(rel)
    if src is None:
        return [], '%s: 원천 파일 없음 (%s)' % (name, rel)
    hits = list(re.finditer(pattern, src))
    if len(hits) != expected:
        return [], '%s: %s 에서 %d번 잡힘(기대 %d)' % (name, rel, len(hits), expected)
    values = [g for m in hits for g in m.groups() if g is not None]
    return values, None


def resolve_chrome(src_override=None):
    """ItemCatalog 크롬 문구 집합을 원천에서 읽는다. (집합, 앵커별 값, 문제 목록)"""
    chrome, per, problems = set(), [], []
    for a in ITEMCATALOG_CHROME_ANCHORS:
        vals, prob = resolve_anchor(a, src_override)
        per.append((a[0], vals))
        if prob:
            problems.append(prob)
        chrome.update(vals)
    return chrome, per, problems


def source_literals(rel):
    """원천 파일의 문자열 리터럴 값 집합 — `census.lex_csharp`(ship.json 을 만든 바로 그 렉서)."""
    if HERE not in sys.path:
        sys.path.insert(0, HERE)
    from census import lex_csharp  # noqa
    src = _read_source(rel)
    if src is None:
        return set()
    lits, _ = lex_csharp(src)
    return set(l['value'] for l in lits)


def snapshot_drift(data):
    """ship.json 과 **지금 소스로 ship.py 가 낼 결과**의 차이. 다시 세는 것이 아니라 «낡았는가»만 본다.

    같은 생성기(`ship.run`)를 부르므로 생성기 자체의 오판은 못 잡는다 — 그건 `ship.py --selftest` 소관이다.
    파일은 쓰지 않는다(`ship.run`은 계산만 하고, 쓰기는 `ship.report`에만 있다).
    """
    if HERE not in sys.path:
        sys.path.insert(0, HERE)
    import ship  # noqa
    import census  # noqa
    rows, _ = ship.run()
    assets = census.census_assets()
    old_cs = collections.Counter((e['file'], e['verdict'], e['text']) for e in data['cs'])
    new_cs = collections.Counter((e['file'], e['verdict'], e['text']) for e in rows)
    old_as = collections.Counter((e['file'], e['text']) for e in data['asset'])
    new_as = collections.Counter((e['file'], e['text']) for e in assets)
    gone, born = old_cs - new_cs, new_cs - old_cs
    a_gone, a_born = old_as - new_as, new_as - old_as
    files = collections.Counter(k[0] for k in list(gone.elements()) + list(born.elements()))
    files.update(k[0] for k in list(a_gone.elements()) + list(a_born.elements()))
    return {
        'stale': bool(gone or born or a_gone or a_born),
        'gone': sum(gone.values()), 'born': sum(born.values()),
        'asset_gone': sum(a_gone.values()), 'asset_born': sum(a_born.values()),
        'files': files,
        'gone_rows': gone, 'born_rows': born,
    }


def drift_lines(d, focus=()):
    out = ['cs 행 사라짐 %d / 생김 %d · asset 사라짐 %d / 생김 %d · 달라진 파일 %d개'
           % (d['gone'], d['born'], d['asset_gone'], d['asset_born'], len(d['files']))]
    for f, c in d['files'].most_common(5):
        out.append('  %4d  %s' % (c, f))
    for f in focus:
        for k in sorted(d['gone_rows']):
            if k[0] == f:
                out.append('  - [%s] %s %r' % (f, k[1], k[2]))
        for k in sorted(d['born_rows']):
            if k[0] == f:
                out.append('  + [%s] %s %r' % (f, k[1], k[2]))
    return out


# ---------------------------------------------------------------- 분류
def load():
    with io.open(SHIP_JSON, encoding='utf-8') as fh:
        return json.load(fh)


def classify(data, chrome):
    """(층, 파일, 문자열) 목록을 낸다. 분류하지 못한 것은 남기지 않고 E-C로 떨어뜨린다."""
    eb, ec = [], []
    for e in data['cs']:
        if e['verdict'] != 'SHIP':
            continue
        f, t = e['file'], e['text']
        if f in WHOLE_FILE:
            eb.append((WHOLE_FILE[f][0], f, t))
        elif f == ITEMCATALOG_FILE:
            if t in chrome:
                eb.append(('CHROME', f, t))
            else:
                # 나머지 = 행동 12종의 이름과 설명
                eb.append(('CONTENT', f, t))
        else:
            ec.append((f, t))
    for a in data['asset']:
        eb.append((ASSET_LAYER[0], a.get('file', '(asset)'), a.get('text', '')))
    return eb, ec


def report():
    if not os.path.exists(SHIP_JSON):
        print('★ 판정 불가 — ship.json 이 없다. `python3 ship.py` 로 만든다.')
        return RC_UNDECIDABLE
    chrome, per, problems = resolve_chrome()
    if problems:
        print('★ 판정 불가 — ItemCatalog 크롬 앵커가 원천에서 안 잡힌다(식별자가 바뀌었는가):')
        for p in problems:
            print('  ' + p)
        return RC_UNDECIDABLE
    data = load()
    drift = snapshot_drift(data)
    stale_banner = ('★ 판정 불가 — ship.json 이 현재 소스와 다르다(낡은 스냅숏). 아래 숫자는 인용하지 마라.\n'
                    '  `python3 ship.py` 로 다시 만든 뒤 재실행한다.')
    if drift['stale']:
        print(stale_banner)
        for ln in drift_lines(drift):
            print('  ' + ln)
        print()

    eb, ec = classify(data, chrome)
    chrome_rows = [x for x in eb if x[0] == 'CHROME']
    content = [x for x in eb if x[0] == 'CONTENT']

    def u(rows):
        return len(set(r[2] for r in rows))

    print('=' * 78)
    print('E-B (결제 경로 UI) 부분집합 — ROADMAP §12-7 요청분')
    print('=' * 78)
    print('  출처: ship.json (mtime %s) · 스냅숏 %s'
          % (__import__('time').strftime('%m-%d %H:%M',
                                         __import__('time').localtime(os.path.getmtime(SHIP_JSON))),
             '낡음 ★' if drift['stale'] else '현재 소스와 일치'))
    print('  ItemCatalog 크롬(원천 앵커에서 읽음): ' + ' · '.join(
        '%s=%s' % (n, '/'.join(v)) for n, v in per))
    print()
    print('  %-10s %6s %6s   %s' % ('층', '총건수', '고유', '무엇'))
    print('  %-10s %6d %6d   %s' % ('CHROME', len(chrome_rows), u(chrome_rows), '상태·등급·카테고리·탭 라벨'))
    print('  %-10s %6d %6d   %s' % ('CONTENT', len(content), u(content), '행동 12종 + 장비/외형 42종의 이름·설명'))
    print('  %-10s %6d %6d' % ('★ E-B 계', len(eb), u(eb)))
    print()
    print('  %-10s %6d %6d   %s' % ('E-C(나머지)', len(ec), len(set(x[1] for x in ec)), '설정창·정보창 좌열·팝오버 등'))
    print()
    print('--- CHROME 파일별 ---')
    byf = {}
    for _, f, t in chrome_rows:
        byf.setdefault(f, []).append(t)
    for f in sorted(byf, key=lambda k: -len(byf[k])):
        why = WHOLE_FILE.get(f, (None, 'ItemCatalog 상태/등급/카테고리 크롬'))[1]
        print('  %3d  %-48s %s' % (len(byf[f]), f, why))
    print()
    print('--- CONTENT 파일별 ---')
    byf = {}
    for _, f, t in content:
        byf.setdefault('.asset (42종)' if f.endswith('.asset') else f, []).append(t)
    for f in sorted(byf, key=lambda k: -len(byf[k])):
        print('  %3d  %s' % (len(byf[f]), f))
    print()
    print('★ 아직 코드에 없는 E-B 부채는 여기 안 잡힌다 — `UX_EQUIPMENT_WINDOW_3COL_PORT.md` §4-6/§4-7이')
    print('  예고한 「스토어에서 구매」·「동전 N」·「저장하지 못했어요 …」 등은 **구현되면** 늘어난다.')
    if drift['stale']:
        print()
        print(stale_banner)
        return RC_UNDECIDABLE
    return RC_OK


# ---------------------------------------------------------------- 대조
def selftest():
    state = {'fail': False, 'undecidable': False}

    def chk(name, cond, detail='', undecidable=False):
        tag = '  PASS  ' if cond else ('  판정불가 ' if undecidable else '  FAIL  ')
        print(tag + name + ('   ' + detail if detail else ''))
        if not cond:
            state['undecidable' if undecidable else 'fail'] = True

    def rc():
        return RC_UNDECIDABLE if state['undecidable'] else (RC_FAIL if state['fail'] else RC_OK)

    chk('ship.json 이 있다', os.path.exists(SHIP_JSON), undecidable=True)
    if not os.path.exists(SHIP_JSON):
        return rc()

    # ---- 원천 앵커 — 니들을 소스에서 읽는다. 여기가 깨지면 아래 전부 무의미 ----
    chrome, per, problems = resolve_chrome()
    chk('원천 앵커 — ItemCatalog 크롬 %d종이 정해진 횟수만큼 잡힌다' % len(ITEMCATALOG_CHROME_ANCHORS),
        not problems, '; '.join(problems) or ' · '.join('%s=%s' % (n, '/'.join(v)) for n, v in per),
        undecidable=True)
    cat_lits = source_literals(ITEMCATALOG_FILE)
    chk('★ 원천 양성 — 앵커 값이 전부 렉서가 뽑은 실제 리터럴이다(주석 조각을 잡지 않았다)',
        bool(chrome) and chrome <= cat_lits,
        '리터럴 아님: %s' % (sorted(chrome - cat_lits) or '(없음)'), undecidable=True)
    tabs_src = _read_source(TABS_FILE) or ''
    tab_defs = TAB_DEF_RE.findall(tabs_src)
    shop_labels = [lab for lab, page in tab_defs if page == 'Shop']
    tab_hangul = [lab for lab, _ in tab_defs if re.search(u'[가-힣]', lab)]
    chk('원천 앵커 — Tabs.cs `new TabDef(…, TabPage.Shop)` 가 정확히 1개, 한글 탭 라벨이 1개 이상',
        len(shop_labels) == 1 and len(tab_hangul) >= 1,
        'Shop=%s 한글 라벨=%s' % (shop_labels, tab_hangul), undecidable=True)
    idle_vals, idle_prob = resolve_anchor(IDLE_CAPTION_ANCHOR)
    chk('원천 앵커 — Idle 상태 캡션', not idle_prob, idle_prob or '/'.join(idle_vals), undecidable=True)

    # ---- ★ 스냅숏 신선도 — 존재 대조를 **낡은 스냅숏에 대고** 하면 기준과 대상이 같이 낡는다 ----
    data = load()
    drift = snapshot_drift(data)
    chk('★ 스냅숏 신선도 — ship.json 이 지금 소스로 ship.py 가 낼 결과와 같다',
        not drift['stale'], '\n           '.join(drift_lines(drift, focus=(ITEMCATALOG_FILE,))),
        undecidable=True)

    eb, ec = classify(data, chrome)

    # 교정 — 알려진 값으로 먼저 맞춘다. 깨지면 위 숫자를 전부 폐기한다.
    chk('교정 ① .asset 84건이 전부 E-B다',
        len([x for x in eb if x[1].endswith('.asset')]) == 84,
        str(len([x for x in eb if x[1].endswith('.asset')])))
    chk('교정 ② [상점] 탭 라벨(원천에서 읽음)이 E-B CHROME에 실제로 들어 있다',
        bool(shop_labels) and any(k == 'CHROME' and f == TABS_FILE and t == shop_labels[0]
                                  for k, f, t in eb),
        repr(shop_labels[0]) if shop_labels else '(앵커 없음)')
    chk('교정 ③ 한글 탭 라벨 전부(원천에서 읽음)가 E-B CHROME에 들어 있다',
        bool(tab_hangul) and all(any(k == 'CHROME' and f == TABS_FILE and t == lab for k, f, t in eb)
                                 for lab in tab_hangul),
        '빠진 것: %s' % (sorted(lab for lab in tab_hangul
                               if not any(k == 'CHROME' and f == TABS_FILE and t == lab
                                          for k, f, t in eb)) or '(없음)'))

    # ★ 음성 대조 — 결제와 무관한 문장이 E-B로 새지 않는가
    idle = idle_vals[0] if idle_vals else None
    chk('★ 음성 — Idle 상태 캡션(원천에서 읽음)은 E-B가 아니고 E-C에 실재한다',
        idle is not None
        and not any(t == idle for _, _, t in eb)
        and any(t == idle for _, t in ec), repr(idle))
    chk('★ 음성 — 설정창 문자열은 하나도 E-B가 아니다',
        not any('SettingsWindow' in f for _, f, _ in eb))

    # ★ 양성 — 분류기가 살아 있는가(빈 목록을 순회하고 초록이 되지 않는가)
    chk('★ 양성 — E-B가 비어 있지 않다', len(eb) > 0, '%d건' % len(eb))
    chk('★ 양성 — E-C도 비어 있지 않다(전부 E-B로 쓸어담지 않았다)', len(ec) > 0, '%d건' % len(ec))
    chk('★ 양성 — E-B + E-C = SHIP 전량 + .asset (아무것도 흘리지 않았다)',
        len(eb) + len(ec) ==
        len([e for e in data['cs'] if e['verdict'] == 'SHIP']) + len(data['asset']))

    # ★ 이 저장소가 반복해 당한 형태 — 니들이 실재하는지 같은 자리에서 못박는다
    cat_ship = set(e['text'] for e in data['cs']
                   if e['verdict'] == 'SHIP' and e['file'] == ITEMCATALOG_FILE)
    chk('★ 죽은 니들 방지 — 원천 크롬 %d문구가 전부 ship.json 의 ItemCatalog SHIP 에 있다' % len(chrome),
        bool(chrome) and chrome <= cat_ship,
        '없는 것: ' + (', '.join(sorted(chrome - cat_ship)) or '(없음)'))
    chk('★ 죽은 니들 방지 — WHOLE_FILE 4경로가 전부 실존한다',
        all(os.path.exists(os.path.join(SCRIPTS, f)) for f in WHOLE_FILE),
        '없는 것: ' + (', '.join(
            f for f in WHOLE_FILE
            if not os.path.exists(os.path.join(SCRIPTS, f))) or '(없음)'))

    # ---- ★ 변이 대조(메모리 안) — 위 초록이 «살아 있는 초록»인가 ----
    menu_vals = dict(per).get('MenuOnlyStatus 상수') or []
    cat_src = _read_source(ITEMCATALOG_FILE) or ''
    if menu_vals and ('MenuOnlyStatus = "%s"' % menu_vals[0]) in cat_src:
        decl = 'MenuOnlyStatus = "%s"' % menu_vals[0]
        _, _, p_rename = resolve_chrome(cat_src.replace(decl, 'MenuOnlyStatusX = "%s"' % menu_vals[0]))
        chk('★ 변이 — 식별자 MenuOnlyStatus 가 사라지면 앵커가 판정 불가를 낸다', bool(p_rename),
            '; '.join(p_rename) or '(문제 없음 = 앵커가 죽어 있다)')
        mutated = u'변이 문구 가나다'
        c_mut, _, p_mut = resolve_chrome(cat_src.replace(decl, 'MenuOnlyStatus = "%s"' % mutated))
        chk('★ 변이 — 리터럴이 바뀌면 니들이 원천을 따라간다(옛 문구는 빠진다)',
            not p_mut and mutated in c_mut and menu_vals[0] not in c_mut)
        hit = [x for x in classify(data, chrome)[0] if x[1] == ITEMCATALOG_FILE and x[2] == menu_vals[0]]
        miss = [x for x in classify(data, chrome - {menu_vals[0]})[0]
                if x[1] == ITEMCATALOG_FILE and x[2] == menu_vals[0]]
        if not hit:
            chk('★ 변이 — 그 니들이 분류를 실제로 움직인다(빼면 CHROME → CONTENT)', False,
                '스냅숏에 %r 이 없어 잴 자리가 없다(낡은 스냅숏)' % menu_vals[0], undecidable=True)
        else:
            chk('★ 변이 — 그 니들이 분류를 실제로 움직인다(빼면 CHROME → CONTENT)',
                [x[0] for x in hit] == ['CHROME'] and [x[0] for x in miss] == ['CONTENT'],
                '있을 때 %s / 뺐을 때 %s' % ([x[0] for x in hit], [x[0] for x in miss]))
    else:
        chk('★ 변이 대조 — MenuOnlyStatus 선언을 원천에서 찾아야 변이를 심을 수 있다', False,
            '선언 없음', undecidable=True)

    return rc()


if __name__ == '__main__':
    if '--selftest' in sys.argv:
        sys.exit(selftest())
    sys.exit(report())
