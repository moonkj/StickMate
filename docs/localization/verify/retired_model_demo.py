#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
★★ **이 파일은 예측기가 아니다. 절대 예측에 쓰지 마라.**

폐기된 「글자 수 × 자폭」 모형을 **러너 실측 계수로 최대한 유리하게** 재현해서,
**그래도 라틴에서 계통적으로 과대하다**는 것을 보이기 위해서만 존재한다.
`PLAN_1.0.md` §10-1의 표(한국어 464 vs 461 / 라틴 696 vs 655)를 재현하는 코드다.

왜 `layout.py`에 안 넣었나: `layout.py`는 **자폭 계수가 0개**라는 것이 그 파일의 성질이고,
여기 계수를 두면 다음 사람이 그것을 꺼내 쓴다. **격리해 둔다.**

기대값은 **러너가 뱉은 숫자**에서 온다(내가 고른 값이 아니다):
    ui-textwidth_play.xml  [글자폭모형-TEST] 5자 기준 실측 폭(12pt) — 한글 60.0 / 라틴 34.0 / 공백 15.0
    ui-textwidth_edit.xml  [갭G] 탭바 끝(pt) — 한국어 실측 461 / 라틴 실측 655 / 창 폭 720

    python3 retired_model_demo.py     # rc: 0 = «모형이 라틴에서 과대»가 여전히 참
"""
import math, sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import layout   # 러너 xml에서 실측치를 읽어 오기 위해서만 쓴다


# ---- 폐기된 모형 (do not use) --------------------------------------------------
def _retired_model_do_not_use(s, per_ko, per_lat, per_sp, pt=12):
    k = pt / 12.0
    t = 0.0
    for c in s:
        if c == ' ':
            t += per_sp * k
        elif '가' <= c <= '힣':
            t += per_ko * k
        else:
            t += per_lat * k
    return math.ceil(t)


def strip_end(names, ready, badge, f):
    PAD, GAP, SP1, CPX = 10.0, 8.0, 4.0, 20.0     # 여백 상수는 프로덕션에서 그대로 살아 있다
    bw = f(badge, 10)
    x = CPX
    for n, r in zip(names, ready):
        x += PAD * 2 + f(n) + (0.0 if r else GAP + bw) + SP1
    return x - SP1


def main():
    meas, src, used, _seen = layout.load_runs()
    need = ('han5', 'lat5', 'sp5', 'sw_tab_ko', 'sw_tab_en')
    missing = [k for k in need if k not in meas]
    if missing:
        print('★ 러너 실측치가 없다: %s — 이 시연은 **성립하지 않는다**(rc=1).' % missing)
        print('  «측정값 없음»을 «모형이 맞다»로 읽지 마라.')
        sys.exit(1)

    ko, la, sp = meas['han5'] / 5, meas['lat5'] / 5, meas['sp5'] / 5
    print('러너 실측 계수(%dpt): 한글 %.1f / 라틴 %.1f / 공백 %.1f pt·자   (출처 %s %s)'
          % (meas['font_pt'], ko, la, sp, src['han5'][0], src['han5'][1]))

    def f(s, pt=12):
        return _retired_model_do_not_use(s, ko, la, sp, pt)

    ready = [True, True, False, False, False]
    cases = [
        ('한국어(현행)', ["일반", "캐릭터", "이벤트", "접근성 · 성능", "데이터"], "준비 중", meas['sw_tab_ko']),
        ('라틴 풀네임', ["General", "Character", "Events", "Accessibility & Performance", "Data"],
         "Coming soon", meas['sw_tab_en']),
    ]
    print()
    print('  %-14s %10s %10s %10s' % ('문안', '폐기모형', '러너실측', '오차'))
    err = {}
    for tag, names, badge, actual in cases:
        model = strip_end(names, ready, badge, f)
        err[tag] = (model - actual) / actual
        print('  %-14s %9.0fpt %9.0fpt %+9.1f%%' % (tag, model, actual, err[tag] * 100))

    print()
    ok = True
    if abs(err['한국어(현행)']) > 0.02:
        print('  !! 한국어 오차가 2%%를 넘었다(%.1f%%). PLAN §10-1의 «한글은 자폭이 사실상 고정»이라는'
              ' 근거 문단을 다시 써라.' % (err['한국어(현행)'] * 100))
        ok = False
    else:
        print('  PASS  한국어 오차 %.1f%% — 한글은 자폭이 사실상 고정이라 평균이 곧 참값이다.'
              % (err['한국어(현행)'] * 100))
    if err['라틴 풀네임'] <= 0.02:
        print('  !! 라틴에서 모형이 더는 과대하지 않다(%.1f%%). 폐기 근거의 핵심이 무너졌다 —'
              ' §10-1을 다시 열어라.' % (err['라틴 풀네임'] * 100))
        ok = False
    else:
        print('  PASS  라틴 오차 %+.1f%% — **가짜 넘침을 만드는 방향으로** 계통 과대다.'
              % (err['라틴 풀네임'] * 100))

    if 'sw_panel' in meas:
        slack = meas['sw_panel'] - meas['sw_tab_en']
        model_slack = meas['sw_panel'] - strip_end(cases[1][1], ready, cases[1][2], f)
        print()
        print('  실측 여유 %.0fpt → 재전사 모형이 보고할 여유 %.0fpt '
              '(모형 오차가 여유의 %.0f%%를 먹는다)'
              % (slack, model_slack, (slack - model_slack) / slack * 100))
        print('  ⇒ 재전사해도 «문안을 줄여라»는 잘못된 요구가 그대로 나온다. 그래서 폐기다.')
    sys.exit(0 if ok else 1)


if __name__ == '__main__':
    main()
