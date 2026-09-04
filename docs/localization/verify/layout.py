#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
레이아웃 **예산 원장 · 러너 실측 대조기**  (localization / 2026-09-03 R3)

★★ 이 파일은 2026-09-03에 **역할이 바뀌었다**. 예전 판(R2)은 「넘침 예측기」였고
   상자 폭을 «글자 수 × 상수»로 **스스로 계산**했다. 그 상수 4종(11 / 9 / 14 / 11)은
   프로덕션에서 베껴 온 것이었는데, `coder-ui`가 2026-09-03에 그 모형 7곳을 전부
   `Text.preferredWidth` **실측**으로 교체하면서 **프로덕션에서 사라졌다.**

   그런데 R2의 CALIBRATION은 그 뒤에도 **3/3 통과했다.**
   ⇒ **교정 앵커가 자기 상수였기 때문이다.** 예를 들어 «[장비] 상자가 32pt인가»는
     `len("장비") * 14 + 4 == 32`이라는 **산술 항등식**이라 어떤 프로덕션 상태에서도 참이다.
     나머지 셋(776 / 230 / 39자)도 전부 **같은 모형이 만든 주석 숫자**와 대조하고 있었다.
     **측정 도구가 자기 자신을 검증하는 구조** — 이 저장소가 아홉 번 당한 형태 그 자체다.

===============================================================================
그래서 무엇을 하는가 — **폭을 계산하지 않는다**
===============================================================================
이 도구에는 **자폭 계수가 한 개도 없다.** 폭은 전부 **바깥**에서 온다.

  앵커 A. **프로덕션 소스**  — 예산(창 폭·밑줄 끝 같은 **구조 상수**)을 매 실행마다
          소스에서 정규식으로 **다시 읽는다.** 못 읽으면 **빨간불**이고, 낡은 전사본을
          쓰지 않는다. (양성/음성 대조: `--selftest`)
  앵커 B. **Unity 러너가 실제로 뱉은 숫자** — `docs/verify/runs/*.xml`의 `Debug.Log`에서
          `[글자폭모형-TEST]` / `[갭G]` 줄을 뽑는다. 이 숫자는 폰트가 잰 값이고
          이 파일은 그것을 **소비만** 한다.

★★ **「측정값이 없다」는 절대 통과가 아니다.** 원장의 어떤 표면에 대해 러너 숫자를
   찾지 못하면 판정은 `미측정`이고 **rc≠0**이다. 이 저장소 거짓 통과 #4/#5가
   전부 «없어서 0건 = 깨끗»이었다.

===============================================================================
★ R2가 무엇을 맞히고 무엇을 틀렸나 — 부호가 반대인 세 가지 형태
===============================================================================
「글자 수 × 상수」 모형은 **한 종류의 오차가 아니다.** 상자와 문자열의 관계에 따라
**부호가 뒤집힌다.** R2가 맞은 곳과 틀린 곳이 정확히 이 축으로 갈렸다.

  (곱셈형) 상자 = 글자수 × k       라틴에서 상자가 **부푼다** → **가짜 넘침**을 만든다
                                    → R2 오탐. 설정창/정보창 탭바, 세그먼트, 버튼, 배지.
  (나눗셈형) 상한 = floor(폭 / k)   라틴에서 상한이 **과소** → 자리가 남는데 **미리 자른다**
                                    → 실제 결함이지만 「넘침」이 아니다. 보관함 설명 칸.
  (고정형) 상자 = 상수, 문자열은 따로 자란다
                                    → **모형이 유일한 탐지 수단**이었다(프로덕션이 재지 않았으므로)
                                    → **R2 정탐.** 푸터 [지금 종료] 칩.

⇒ **일반화**: 오프라인 모형은 **상자가 문자열에 의존하지 않는 자리에서만** 값을 한다.
  프로덕션이 문자열을 재서 상자를 만들기 시작하면, 모형은 남아도는 정도가 아니라
  **해로워진다** — 방금 제거한 그 부풀림을 다시 더하기 때문이다.

사용법:
    python3 layout.py                 # 원장 대조 (rc: 0=전부 예산 안, 1=초과/미측정)
    python3 layout.py --selftest      # 14종 대조 (이게 깨지면 위 결과를 전부 폐기한다)
"""

import os, re, io, sys, glob, html

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))   # <repo>  (verify -> localization -> docs -> repo)
SCRIPTS = os.path.join(ROOT, "Assets", "_Project", "Scripts")
RUNS = os.path.join(ROOT, "docs", "verify", "runs")

sys.path.insert(0, HERE)
from census import lex_csharp            # noqa: E402  (주석 제거용 — census.py --selftest 10/10)


# =============================================================================
# 앵커 A — 프로덕션 소스에서 구조 상수를 읽는다
# =============================================================================

def _source(rel):
    p = os.path.join(SCRIPTS, rel)
    if not os.path.exists(p):
        return None
    return io.open(p, encoding='utf-8', errors='replace').read()


def _decl_only(src):
    """주석/문자열을 지운 소스. 폐기된 상수 이름이 **설명 주석에 남아 있는** 경우를
    「되살아났다」로 오판하지 않기 위해 반드시 거친다(실측: 3개 파일이 그 상태다)."""
    if src is None:
        return None
    _lits, masked = lex_csharp(src)
    return ''.join(masked)


def read_const(rel, name):
    """`const float <name> = <숫자>f;` 를 **선언에서만** 읽는다. 없으면 None."""
    src = _decl_only(_source(rel))
    if src is None:
        return None
    m = re.search(r'\bconst\s+float\s+' + re.escape(name) + r'\s*=\s*(-?[0-9.]+)f?\s*;', src)
    return float(m.group(1)) if m else None


def has_decl(rel, name):
    """그 이름의 **선언**이 존재하는가(주석 제외). 폐기 확인용."""
    src = _decl_only(_source(rel))
    if src is None:
        return False
    return bool(re.search(r'\bconst\s+(?:float|int)\s+' + re.escape(name) + r'\b', src))


# =============================================================================
# 앵커 B — 러너가 뱉은 실측치를 XML에서 뽑는다
# =============================================================================
#
# ★ 각 측정에 **이름**을 붙이고 정규식을 하나씩 준다. 「대충 숫자를 긁는다」로 하면
#   로그 문구가 바뀌었을 때 조용히 0건이 되고, 그건 이 파일이 막으려는 바로 그 형태다.

PROBES = {
    # 이름                    (정규식, 캡처 이름들)
    'glyph5':  (re.compile(r'\[글자폭모형-TEST\] 5자 기준 실측 폭\((\d+)pt\) — '
                           r'한글 ([\d.]+)pt / 라틴 ([\d.]+)pt / 공백 ([\d.]+)pt'),
                ('font_pt', 'han5', 'lat5', 'sp5')),
    'gapG':    (re.compile(r'\[갭G\] 탭바 끝\(pt\) — 한국어 실측 ([\d.]+) / 라틴 실측 ([\d.]+) / '
                           r'라틴 옛근사 ([\d.]+) / 창 폭 ([\d.]+)'),
                ('sw_tab_ko', 'sw_tab_en', 'sw_tab_retired', 'sw_panel')),
    'quit':    (re.compile(r'\[글자폭모형-TEST\] 종료 칩 — 호스트 «[^»]*» 잉크 ([\d.]+)pt / 칩 ([\d.]+)pt\. '
                           r'Windows 표기 «[^»]*» 잉크 ([\d.]+)pt → 필요 폭 ([\d.]+)pt\. '
                           r'푸터 아랫줄 가용 폭 ([\d.]+)pt'),
                ('quit_host_ink', 'quit_chip', 'quit_win_ink', 'quit_win_need', 'quit_avail')),
    'invdesc': (re.compile(r'\[글자폭모형-TEST\] 보관함 설명 — 칸 폭 ([\d.]+)pt\. '
                           r'화면에 뜬 (\d+)줄 중 (\d+)줄이 폭 기준으로 잘렸다\. '
                           r'옛 글자 수 모형이었다면 상한 (\d+)자 기준으로 카탈로그 전체에서 (\d+)건이 잘렸을 것이다'),
                ('inv_w', 'inv_rows', 'inv_cut_now', 'inv_retired_chars', 'inv_cut_retired')),
}


def ingest(text):
    """로그 텍스트에서 측정치를 뽑아 dict로. 못 찾은 프로브는 **키가 없다**(0이 아니다).

    ★ 한 텍스트 안에 같은 프로브가 여러 번 있으면 **마지막 것**을 쓴다 — 러너 xml은
      같은 실행에서 여러 번 찍힐 수 있고, 뒤엣것이 더 최근이다."""
    out = {}
    for pname, (rx, fields) in PROBES.items():
        last = None
        for m in rx.finditer(text):
            last = m
        if last is None:
            continue
        for i, f in enumerate(fields):
            out[f] = float(last.group(i + 1))
        out['_probe_' + pname] = 1.0
    return out


def load_runs(paths=None):
    """러너 XML을 **오래된 것부터** 훑어 측정치를 채운다. 뒤(최신)가 앞을 덮는다.

    ★ 각 값이 **어느 파일 · 몇 시**에서 왔는지 함께 돌려준다. 이 저장소 거짓 통과 #2가
      «이틀 전 결과 xml을 새 결과로 읽음»이었다 — 출처와 시각을 안 찍으면 같은 함정이다."""
    import datetime
    if paths is None:
        paths = glob.glob(os.path.join(RUNS, '*.xml'))
    paths = sorted(paths, key=os.path.getmtime)
    meas, src, used, seen = {}, {}, [], {}
    for p in paths:
        try:
            raw = io.open(p, encoding='utf-8', errors='replace').read()
        except OSError:
            continue
        if '글자폭모형-TEST' not in raw and '[갭G]' not in raw:
            continue
        got = ingest(html.unescape(raw))
        if not got:
            continue
        stamp = datetime.datetime.fromtimestamp(os.path.getmtime(p)).strftime('%m-%d %H:%M')
        for k, v in got.items():
            meas[k] = v
            src[k] = (os.path.basename(p), stamp)
            seen.setdefault(k, set()).add(v)
        used.append((os.path.basename(p), stamp, sorted(k for k in got if k.startswith('_probe_'))))
    return meas, src, used, seen


# =============================================================================
# 원장 — 「예산이 문자열에 의존하지 않는」 표면만 싣는다
# =============================================================================
#
# ★ 곱셈형(상자를 글자에서 만드는) 표면은 **원장에 없다.** 그쪽은 프로덕션이 폰트에게
#   직접 물으므로 예산이라는 개념 자체가 없고, 러너의 «상자 폭 == 실측 잉크» 단언이
#   구조적으로 더 강하다. 여기 남는 것은 **상자가 먼저 정해진 자리**뿐이다.

def ledger():
    """[(표면, 예산pt, 예산 출처, 실측 키, 비고)] — 예산은 전부 소스에서 방금 읽은 값."""
    sw_panel = read_const('Interaction/SettingsWindow.cs', 'PanelWidth')
    ci_pad = read_const('Interaction/CharacterInfoWindow.cs', 'RightPadX')
    rows = []
    rows.append(('설정창 탭바 — 한국어', sw_panel,
                 'SettingsWindow.PanelWidth', 'sw_tab_ko',
                 '현행 화면. 이것이 넘치면 «지금이 정답»이라는 전제가 깨진다'))
    rows.append(('설정창 탭바 — 라틴 풀네임', sw_panel,
                 'SettingsWindow.PanelWidth', 'sw_tab_en',
                 'Accessibility & Performance 포함 최장 후보'))
    rows.append(('푸터 [지금 종료] — Windows 표기', None,
                 '푸터 아랫줄 가용 폭(러너 실측)', 'quit_win_need',
                 '★ 예산도 실측이다 — 안내 글자 끝에서 창 끝까지'))
    return rows, dict(sw_panel=sw_panel, ci_pad=ci_pad)


def reconcile(meas):
    rows, consts = ledger()
    print('=' * 78)
    print('원장 대조 — 예산(소스) vs 실측(러너). 이 파일은 폭을 계산하지 않는다')
    print('=' * 78)
    bad = []

    if consts['sw_panel'] is None:
        print('  !! 앵커 A 실패 — SettingsWindow.PanelWidth 를 소스에서 못 읽었다. '
              '이름이 바뀌었거나 경로가 틀렸다. **아래 판정 전부 무효.**')
        return False

    # ---- ★ 신선도 게이트 — 러너 xml이 지금 소스와 같은 트리에서 나왔는가 ----------
    #   러너 로그도 «창 폭»을 함께 찍는다. 그 값이 소스에서 방금 읽은 값과 다르면
    #   그 xml은 **다른 트리의 결과**다(거짓 통과 #2: 이틀 전 xml을 새 결과로 읽음).
    if 'sw_panel' in meas and abs(meas['sw_panel'] - consts['sw_panel']) > 0.01:
        print('  !! 신선도 실패 — 러너가 찍은 창 폭 %.0fpt != 소스의 PanelWidth %.0fpt.'
              % (meas['sw_panel'], consts['sw_panel']))
        print('     이 xml은 지금 트리의 결과가 아니다. **아래 판정 전부 무효.** 러너를 다시 돌려라.')
        return False
    print('  신선도 OK — 러너가 찍은 창 폭 %.0fpt == 소스 PanelWidth %.0fpt (같은 트리)'
          % (meas.get('sw_panel', consts['sw_panel']), consts['sw_panel']))

    for name, budget, src, key, note in rows:
        if key == 'quit_win_need':
            budget = meas.get('quit_avail')
        if key not in meas or budget is None:
            print('  미측정  %-26s  예산 %-9s  실측 ——   ← ★ 통과가 아니다. 러너 로그에 %s 가 없다'
                  % (name, ('%.0fpt' % budget) if budget else '?', key))
            bad.append(name)
            continue
        got = meas[key]
        slack = budget - got
        mark = 'OK    ' if slack >= 0 else '초과  '
        if slack < 0:
            bad.append(name)
        print('  %s %-26s  예산 %6.0fpt  실측 %6.0fpt  %s %5.0fpt   (%s)'
              % (mark, name, budget, got, '여유' if slack >= 0 else '초과', abs(slack), src))
        print('           %s' % note)

    # ---- 종료 칩: 칩 자체의 상수 하한과 실측 잉크 --------------------------------
    if 'quit_chip' in meas and 'quit_win_ink' in meas:
        leak = meas['quit_win_ink'] - meas['quit_chip']
        print()
        print('  [고정형 표면] 종료 칩 상수 하한 %.0fpt vs Windows 표기 잉크 %.0fpt → %+.0fpt'
              % (meas['quit_chip'], meas['quit_win_ink'], leak))
        print('     ★ 2026-09-03 이전에는 칩 폭이 이 상수 **하나**였다. 라벨은 '
              'HorizontalWrapMode.Overflow라 넘쳐도 잘리지 않는다 = 테두리를 뚫고 나온 글자.')
        print('     지금은 Mathf.Max(상수, 실측잉크+여백)이라 상수는 **하한**으로만 쓰인다.')

    # ---- 러너가 구조적으로 못 보는 축: Windows 폰트 스택 -------------------------
    print()
    print('=' * 78)
    print('★ 러너가 구조적으로 못 보는 축 — Windows 폰트 스택 (이 머신에 없다)')
    print('=' * 78)
    print('  상자는 이제 «그 폰트가 잰 값»으로 만들어지므로 **상자와 글리프는 어느 폰트에서도 맞다.**')
    print('  남는 위험은 **창 폭이 고정**이라는 것 하나다 — 자간이 넓은 폴백에서 누적 끝이 밀린다.')
    if 'sw_tab_en' in meas and 'sw_panel' in meas and meas['sw_tab_en'] > 0:
        head = meas['sw_panel'] / meas['sw_tab_en'] - 1.0
        print('  설정창 탭바(라틴): 실측 %.0fpt / 창 폭 %.0fpt → **자간이 %.1f%% 넓어질 때까지 안전**'
              % (meas['sw_tab_en'], meas['sw_panel'], head * 100.0))
        print('     ※ 이 수는 실측 두 개의 나눗셈일 뿐이다. 내가 만든 계수는 하나도 안 들어갔다.')
    if 'sw_tab_ko' in meas and 'sw_panel' in meas and meas['sw_tab_ko'] > 0:
        head = meas['sw_panel'] / meas['sw_tab_ko'] - 1.0
        print('  설정창 탭바(한국어): 실측 %.0fpt → 자간 여유 %.1f%% '
              '(★ 한글 폴백은 맑은 고딕/굴림 계열로 **글꼴 자체가 다르다**)'
              % (meas['sw_tab_ko'], head * 100.0))

    # ---- 옛 모형이 무엇을 만들어 냈는지 (반증 기록) -------------------------------
    if 'sw_tab_retired' in meas and 'sw_tab_en' in meas:
        infl = meas['sw_tab_retired'] - meas['sw_tab_en']
        print()
        print('=' * 78)
        print('★ 반증 기록 — 「넘침」은 모형이 만든 것이었다')
        print('=' * 78)
        print('  같은 라틴 문안: 옛 글자수 모형 %.0fpt / 실측 %.0fpt / 창 폭 %.0fpt'
              % (meas['sw_tab_retired'], meas['sw_tab_en'], meas['sw_panel']))
        print('  → 모형이 **%.0fpt(%.0f%%) 를 지어냈다.** 실제로는 %.0fpt 여유였다.'
              % (infl, infl / meas['sw_tab_en'] * 100.0, meas['sw_panel'] - meas['sw_tab_en']))
    if 'inv_cut_retired' in meas:
        print()
        print('  [나눗셈형] 보관함 설명 칸 %.0fpt — 지금 잘리는 줄 %.0f. '
              '옛 상한 %.0f자였다면 카탈로그에서 %.0f건이 잘렸다(반대 부호의 오차).'
              % (meas['inv_w'], meas['inv_cut_now'], meas['inv_retired_chars'], meas['inv_cut_retired']))

    if 'han5' in meas:
        print()
        print('  참고(러너 실측, %dpt 기준): 한글 %.1f / 라틴 %.1f / 공백 %.1f pt·자 — 한글:라틴 = %.2f : 1'
              % (meas['font_pt'], meas['han5'] / 5, meas['lat5'] / 5, meas['sp5'] / 5,
                 meas['han5'] / meas['lat5']))
        print('     ★ 이 수를 새 모형의 계수로 쓰지 마라. 이 도구는 그 유혹 때문에 역할이 바뀌었다 —')
        print('       평균 자폭으로 «Accessibility & Performance»를 다시 계산하면 실측과 수십 pt 갈린다.')
    return not bad


# =============================================================================
# 폐기 감시 — 곱셈/나눗셈형 상수가 되살아나면 이 병이 재발한다
# =============================================================================

RETIRED = [
    ('Interaction/SettingsWindow.cs', 'TabLabelCharWidth', '설정창 탭 라벨 «한 글자 폭»'),
    ('Interaction/SettingsWindow.cs', 'TabBadgeWidth', '탭 배지 폭(글자수 × 캡션 폰트)'),
    ('Interaction/CharacterInfoWindow.cs', 'CaptionKoreanAdvance', '보관함 설명 «한글 한 글자 폭»'),
]
ALIVE = [
    ('Interaction/SettingsWindow.cs', 'TabPadX'),
    ('Interaction/SettingsWindow.cs', 'PanelWidth'),
    ('Interaction/CharacterInfoWindow.cs', 'RightPadX'),
]


def retired_watch():
    print()
    print('=' * 78)
    print('폐기 감시 — 존재 대조를 먼저 하고 부재를 말한다')
    print('=' * 78)
    ok = True
    for rel, name in ALIVE:
        v = read_const(rel, name)
        if v is None:
            print('  !! 존재 대조 실패 — %s 의 %s 를 못 읽었다. 스캐너가 죽었으므로 '
                  '아래 «사라졌다» 판정은 아무것도 증명하지 못한다.' % (rel, name))
            ok = False
        else:
            print('  살아있음  %-46s = %.0f' % (rel + '::' + name, v))
    if not ok:
        return False
    for rel, name, what in RETIRED:
        if has_decl(rel, name):
            print('  !! 되살아남  %s::%s (%s) — 곱셈/나눗셈형이 돌아왔다.' % (rel, name, what))
            ok = False
        else:
            print('  없음      %-46s (%s)' % (rel + '::' + name, what))
    print('  ※ 이 이름들은 **설명 주석에는 남아 있다**(실측 3개 파일). 그래서 주석을 지운 뒤 '
          '«선언»만 본다 — raw grep 이었다면 전부 거짓 빨강이다.')
    return ok


# =============================================================================
# 대조 (selftest)
# =============================================================================

_SYNTH_OK = (
    '[갭G] 탭바 끝(pt) — 한국어 실측 461 / 라틴 실측 655 / 라틴 옛근사 863 / 창 폭 720. '
    '배지 폭: 한국어 33 · 라틴 61.\n'
    '[글자폭모형-TEST] 종료 칩 — 호스트 «가» 잉크 99.0pt / 칩 132.0pt. '
    'Windows 표기 «나» 잉크 150.0pt → 필요 폭 176.0pt. 푸터 아랫줄 가용 폭 587.0pt(안내 글자 끝 113.0pt).\n'
)
_SYNTH_OVER = (
    '[갭G] 탭바 끝(pt) — 한국어 실측 461 / 라틴 실측 999 / 라틴 옛근사 863 / 창 폭 720. '
    '배지 폭: 한국어 33 · 라틴 61.\n'
    '[글자폭모형-TEST] 종료 칩 — 호스트 «가» 잉크 99.0pt / 칩 132.0pt. '
    'Windows 표기 «나» 잉크 150.0pt → 필요 폭 176.0pt. 푸터 아랫줄 가용 폭 587.0pt(안내 글자 끝 113.0pt).\n'
)
_SYNTH_MISSING = (
    '[갭G] 탭바 끝(pt) — 한국어 실측 461 / 라틴 실측 655 / 라틴 옛근사 863 / 창 폭 720. '
    '배지 폭: 한국어 33 · 라틴 61.\n'
)


def selftest():
    ok = [True]

    def chk(name, cond, extra=''):
        print('  %s  %s%s' % ('PASS' if cond else 'FAIL', name, ('   ' + str(extra)) if extra else ''))
        if not cond:
            ok[0] = False

    print('== 앵커 A: 소스 상수 읽기 ==')
    chk('양성 — SettingsWindow.PanelWidth 를 읽는다',
        read_const('Interaction/SettingsWindow.cs', 'PanelWidth') == 720.0,
        read_const('Interaction/SettingsWindow.cs', 'PanelWidth'))
    chk('음성 — 없는 이름은 None (기본값을 지어내지 않는다)',
        read_const('Interaction/SettingsWindow.cs', 'PanelWidthZzz') is None)
    chk('음성 — 없는 파일도 None (경로 오타가 조용히 통과하지 않는다)',
        read_const('Interaction/NoSuchFile.cs', 'PanelWidth') is None)
    chk('★ 주석에만 있는 폐기 이름을 «선언»으로 세지 않는다',
        not has_decl('Interaction/SettingsWindow.cs', 'TabLabelCharWidth'))
    raw = _source('Interaction/SettingsWindow.cs') or ''
    chk('★ 그 짝 — 같은 파일 raw 텍스트에는 그 이름이 실제로 있다(대조가 공허하지 않다)',
        'TabLabelCharWidth' in raw)

    print('== 앵커 B: 러너 로그 파싱 ==')
    m = ingest(_SYNTH_OK)
    chk('양성 — [갭G] 4개 값을 뽑는다',
        (m.get('sw_tab_ko'), m.get('sw_tab_en'), m.get('sw_tab_retired'), m.get('sw_panel'))
        == (461.0, 655.0, 863.0, 720.0))
    chk('양성 — 종료 칩 5개 값을 뽑는다',
        (m.get('quit_chip'), m.get('quit_win_ink'), m.get('quit_win_need'), m.get('quit_avail'))
        == (132.0, 150.0, 176.0, 587.0))
    chk('음성 — 빈 로그에서는 아무 키도 안 생긴다(0을 만들지 않는다)', ingest('') == {})
    chk('음성 — 문구가 바뀐 로그는 매칭되지 않는다(조용한 0 방지)',
        'sw_tab_en' not in ingest('[갭G] 탭바 끝 — 한국어 461 라틴 655'))

    print('== 판정 로직 ==')
    print('  --- (아래 세 블록은 판정기가 실제로 빨개지는지 보이기 위한 것이다) ---')
    r_ok = reconcile(ingest(_SYNTH_OK))
    chk('양성 — 예산 안이면 통과', r_ok is True)
    r_over = reconcile(ingest(_SYNTH_OVER))
    chk('★ 음성 — 라틴 999pt(창 720pt)면 실패한다', r_over is False)
    r_miss = reconcile(ingest(_SYNTH_MISSING))
    chk('★★ «측정값 없음»은 통과가 아니라 실패다', r_miss is False)
    r_stale = reconcile(ingest(_SYNTH_OK.replace('창 폭 720', '창 폭 640')))
    chk('★ 신선도 — 러너 창 폭이 소스와 다르면 실패한다(낡은 xml 차단)', r_stale is False)

    print('== 폐기 감시 ==')
    chk('폐기 상수 3종이 선언으로 남아 있지 않고, 살아 있는 이웃 3종은 읽힌다', retired_watch())
    return ok[0]


def main():
    if '--selftest' in sys.argv:
        good = selftest()
        print()
        print('총평: %s' % ('전부 통과' if good else '★ 실패 — 아래 원장 숫자를 전부 폐기한다'))
        sys.exit(0 if good else 1)

    meas, src, used, seen = load_runs()
    print('=' * 78)
    print('앵커 B — 측정치를 준 러너 결과 %d개 (오래된 것 → 최신 순, 뒤가 앞을 덮는다)' % len(used))
    print('=' * 78)
    for name, stamp, probes in used:
        print('   %-44s %s   %s' % (name, stamp, ' '.join(p[7:] for p in probes)))
    if not used:
        print('  !! 러너 XML에서 [글자폭모형-TEST]/[갭G] 를 하나도 못 찾았다.')
        print('     ★ 이것은 «넘침 없음»이 아니라 «아무것도 못 쟀음»이다. rc=1.')
        sys.exit(1)
    print()
    print('  채택된 값의 출처:')
    for k in sorted(k for k in meas if not k.startswith('_probe_')):
        print('     %-18s %8.1f   <- %s  %s' % (k, meas[k], src[k][0], src[k][1]))
    # ---- ★ 재현성 — 여러 라운드의 독립 실행이 같은 값을 냈는가 -------------------
    #   같은 명령을 다시 돌리는 건 검증이 아니지만, **다른 라운드가 다른 시각에 돌린**
    #   실행끼리의 일치는 값이 우연이 아님을 말해 준다. 갈리면 그 값은 못 믿는다.
    print()
    print('  재현성(다른 라운드의 독립 실행끼리):')
    split = []
    for k in sorted(seen):
        if k.startswith('_'):
            continue
        n = len(seen[k])
        if n > 1:
            split.append((k, sorted(seen[k])))
    total_keys = len([k for k in seen if not k.startswith('_')])
    if split:
        for k, vals in split:
            print('     ★ %s 가 실행마다 다르다: %s — 이 값을 판정에 쓰지 마라.' % (k, vals))
    else:
        print('     %d개 측정 전부가 모든 실행에서 **같은 값**이다(파일 %d개).'
              % (total_keys, len(used)))
    print()
    good = reconcile(meas)
    good = retired_watch() and good
    if split:
        good = False
    print()
    print('총평: %s' % ('예산 안 · 폐기 상수 없음' if good else '★ 초과/미측정/재발 있음'))
    sys.exit(0 if good else 1)


if __name__ == '__main__':
    main()
