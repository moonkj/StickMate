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
"""
from __future__ import print_function

import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
SHIP_JSON = os.path.join(HERE, 'ship.json')

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

# ★ 파일 일부만 E-B인 표면. **정규식이 아니라 문자열 자체**로 지목한다 —
#   행 번호는 다음 라운드에 썩고, 정규식은 조용히 넓어진다.
ITEMCATALOG_CHROME = {
    '가끔 알아서', '톱니 메뉴에서', '행동', '착용 중', '보유',
    '일반', '희귀', '영웅', '전설',
}
ITEMCATALOG_CHROME_RE = re.compile(r'^Lv\.\{.*\}에 열림$')

# `.asset` 84건 = 장비/외형 42종의 이름+설명. **팩이 파는 물건 그 자체**다.
ASSET_LAYER = ('CONTENT', '장비·외형 42종 이름+설명. DLC 팩이 싣는 물건 그 자체(§10-4)')


def load():
    with io.open(SHIP_JSON, encoding='utf-8') as fh:
        return json.load(fh)


def classify(data):
    """(층, 파일, 문자열) 목록을 낸다. 분류하지 못한 것은 남기지 않고 E-C로 떨어뜨린다."""
    eb, ec = [], []
    for e in data['cs']:
        if e['verdict'] != 'SHIP':
            continue
        f, t = e['file'], e['text']
        if f in WHOLE_FILE:
            eb.append((WHOLE_FILE[f][0], f, t))
        elif f == 'Core/ItemCatalog.cs':
            if t in ITEMCATALOG_CHROME or ITEMCATALOG_CHROME_RE.match(t):
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
    data = load()
    eb, ec = classify(data)
    chrome = [x for x in eb if x[0] == 'CHROME']
    content = [x for x in eb if x[0] == 'CONTENT']

    def u(rows):
        return len(set(r[2] for r in rows))

    print('=' * 78)
    print('E-B (결제 경로 UI) 부분집합 — ROADMAP §12-7 요청분')
    print('=' * 78)
    print('  출처: ship.json (mtime %s)'
          % __import__('time').strftime('%m-%d %H:%M',
                                        __import__('time').localtime(os.path.getmtime(SHIP_JSON))))
    print()
    print('  %-10s %6s %6s   %s' % ('층', '총건수', '고유', '무엇'))
    print('  %-10s %6d %6d   %s' % ('CHROME', len(chrome), u(chrome), '상태·등급·카테고리·탭 라벨'))
    print('  %-10s %6d %6d   %s' % ('CONTENT', len(content), u(content), '행동 12종 + 장비/외형 42종의 이름·설명'))
    print('  %-10s %6d %6d' % ('★ E-B 계', len(eb), u(eb)))
    print()
    print('  %-10s %6d %6d   %s' % ('E-C(나머지)', len(ec), len(set(x[1] for x in ec)), '설정창·정보창 좌열·팝오버 등'))
    print()
    print('--- CHROME 파일별 ---')
    byf = {}
    for _, f, t in chrome:
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
    return 0


def selftest():
    ok = [True]

    def chk(name, cond, detail=''):
        print(('  PASS  ' if cond else '  FAIL  ') + name + ('   ' + detail if detail else ''))
        if not cond:
            ok[0] = False

    chk('ship.json 이 있다', os.path.exists(SHIP_JSON))
    if not os.path.exists(SHIP_JSON):
        return False
    data = load()
    eb, ec = classify(data)

    # 교정 — 알려진 값으로 먼저 맞춘다. 깨지면 위 숫자를 전부 폐기한다.
    chk('교정 ① .asset 84건이 전부 E-B다',
        len([x for x in eb if x[1].endswith('.asset')]) == 84,
        str(len([x for x in eb if x[1].endswith('.asset')])))
    chk('교정 ② 탭 라벨 「상점」이 E-B에 실제로 들어 있다',
        any(t == '상점' for _, _, t in eb))
    chk('교정 ③ 「상점은 다음 업데이트에 들어옵니다.」도 들어 있다',
        any(t.startswith('상점은 다음 업데이트') for _, _, t in eb))

    # ★ 음성 대조 — 결제와 무관한 문장이 E-B로 새지 않는가
    chk('★ 음성 — 상태 캡션(「가만히 있는 중」)은 E-B가 아니다',
        not any(t == '가만히 있는 중' for _, _, t in eb)
        and any(t == '가만히 있는 중' for _, t in ec))
    chk('★ 음성 — 설정창 문자열은 하나도 E-B가 아니다',
        not any('SettingsWindow' in f for _, f, _ in eb))

    # ★ 양성 — 분류기가 살아 있는가(빈 목록을 순회하고 초록이 되지 않는가)
    chk('★ 양성 — E-B가 비어 있지 않다', len(eb) > 0, '%d건' % len(eb))
    chk('★ 양성 — E-C도 비어 있지 않다(전부 E-B로 쓸어담지 않았다)', len(ec) > 0, '%d건' % len(ec))
    chk('★ 양성 — E-B + E-C = SHIP 전량 + .asset (아무것도 흘리지 않았다)',
        len(eb) + len(ec) ==
        len([e for e in data['cs'] if e['verdict'] == 'SHIP']) + len(data['asset']))

    # ★ 이 저장소가 반복해 당한 형태 — 니들이 실재하는지 같은 자리에서 못박는다
    chk('★ 죽은 니들 방지 — ITEMCATALOG_CHROME 9개가 전부 실제 ship.json 에 있다',
        all(any(e['text'] == n and e['file'] == 'Core/ItemCatalog.cs'
                for e in data['cs'] if e['verdict'] == 'SHIP')
            for n in ITEMCATALOG_CHROME),
        '없는 것: ' + (', '.join(sorted(
            n for n in ITEMCATALOG_CHROME
            if not any(e['text'] == n and e['file'] == 'Core/ItemCatalog.cs'
                       for e in data['cs'] if e['verdict'] == 'SHIP'))) or '(없음)'))
    chk('★ 죽은 니들 방지 — WHOLE_FILE 4경로가 전부 실존한다',
        all(os.path.exists(os.path.join(ROOT, 'Assets/_Project/Scripts', f)) for f in WHOLE_FILE),
        '없는 것: ' + (', '.join(
            f for f in WHOLE_FILE
            if not os.path.exists(os.path.join(ROOT, 'Assets/_Project/Scripts', f))) or '(없음)'))
    return ok[0]


if __name__ == '__main__':
    if '--selftest' in sys.argv:
        sys.exit(0 if selftest() else 1)
    sys.exit(report())
