#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""배선 교차 검증 — 프로덕션 C# 소스의 자격 축 데이터를 design 문서 표와 대조한다.

★ golden_gen.py 와 코드를 공유하지 않는다(TEAM.md: 생성기와 검사기가 같은 코드를 쓰면
  둘 다 같은 방향으로 틀린다). 여기서 파서를 새로 쓰고, 아래 «교정»으로 먼저 스스로를 검증한다.

판정: 모든 항목 OK 여야 한다. 하나라도 깨지면 그 뒤 숫자를 전부 폐기한다.
"""
import io, os, re, sys, itertools

ROOT = '/Users/kjmoon/App/StickMate'
SRC = os.path.join(ROOT, 'Assets/_Project/Scripts/Dialogue/AmbientChatter.cs')
R2 = os.path.join(ROOT, 'design/narrative/2026-09-02_R2_발화빈도_풀24_영어게이트.md')
R23_LINES = os.path.join(ROOT, 'design/narrative/2026-09-06_시간대5구간_오후저녁대사.md')
R23_BOUNDS = os.path.join(ROOT, 'design/systems/timeofday_r23_bounds.out.txt')

fails = []


def check(ok, label, detail=''):
    print(('  OK   ' if ok else '  FAIL ') + label + (('  — ' + detail) if detail else ''))
    if not ok:
        fails.append(label)


src = io.open(SRC, encoding='utf-8').read()


def parse_array(name):
    """`private static readonly <T>[] <name> = { ... };` 의 원소를 (값, 주석) 쌍으로."""
    m = re.search(r'private static readonly [\w\.\?]+\[\]\s+' + name + r'\s*=\s*\{(.*?)\n        \};',
                  src, re.S)
    if m is None:
        print('  ★ 배열을 못 찾았다: %s' % name)
        sys.exit(1)
    items = []
    for raw in m.group(1).split('\n'):
        line = raw.strip()
        if not line or line.startswith('//'):
            continue
        # "값,   // 주석"  또는  "문자열",
        val, _, comment = line.partition('//')
        val = val.strip().rstrip(',').strip()
        if not val:
            continue
        items.append((val, comment.strip()))
    return items


def parse_strings(name):
    """문자열 배열 — 리터럴만 뽑는다(주석 줄 제거 뒤)."""
    m = re.search(r'private static readonly string\[\]\s+' + name + r'\s*=\s*\{(.*?)\n        \};',
                  src, re.S)
    if m is None:
        print('  ★ 대사표를 못 찾았다: %s' % name)
        sys.exit(1)
    body = '\n'.join(l for l in m.group(1).split('\n') if not l.strip().startswith('//'))
    return re.findall(r'"([^"]*)"', body)


print('=== 0. 교정 — 파서가 살아 있는가 (알려진 값으로 먼저 맞춘다) ===')
idle = parse_strings('IdleLines')
walk = parse_strings('WalkLines')
# 이 저장소가 이 라운드 이전부터 갖고 있던 값 — 파서가 죽으면 여기서 먼저 깨진다.
check(idle[0] == '음...', 'IdleLines[0] == "음..."', repr(idle[0]))
check(walk[0] == '산책 중', 'WalkLines[0] == "산책 중"', repr(walk[0]))
check('여기 좋네' in idle, '기존 상시 줄이 살아 있다')
check('심심하다' not in idle, '삭제된 "심심하다"가 되살아나지 않았다')
if fails:
    print('★ 교정이 깨졌다 — 아래 숫자를 전부 폐기한다.'); sys.exit(1)

print()
print('=== 1. 병렬 배열 정합 — 길이 + 주석 라벨이 실제 그 줄인가 ===')
axes = {
    'Idle': {
        'lines': idle,
        'motion': parse_array('IdleLineMotionRequirement'),
        'day': parse_array('IdleLineDayRequirement'),
        'time': parse_array('IdleLineTimeRequirement'),
    },
    'Walk': {
        'lines': walk,
        'day': parse_array('WalkLineDayRequirement'),
        'time': parse_array('WalkLineTimeRequirement'),
    },
}
for table, a in axes.items():
    n = len(a['lines'])
    for key in ('motion', 'day', 'time'):
        if key not in a:
            continue
        check(len(a[key]) == n, '%s 표 %d줄 == %s 축 %d칸' % (table, n, key, len(a[key])))
        # ★ 주석 라벨이 그 인덱스의 실제 대사와 같은가 — 병렬 배열이 한 칸 밀리면 여기서 걸린다.
        mismatched = [(i, a['lines'][i], c) for i, (_, c) in enumerate(a[key]) if c != a['lines'][i]]
        check(not mismatched, '%s/%s 축의 주석 라벨이 전부 그 인덱스의 대사와 일치' % (table, key),
              '' if not mismatched else repr(mismatched[:3]))

print()
print('=== 2. 자격 축이 design 문서 표와 일치하는가 ===')
# --- R2 §3-4 마크다운 표에서 (자격, 상태, 한국어) 를 직접 파싱 ---
r2 = io.open(R2, encoding='utf-8').read()
want = {}   # 대사 -> (축종류, 값, 상태)
AXIS_MAP = {
    '요일:월': ('day', 'Monday'), '요일:금': ('day', 'Friday'), '요일:주말': ('day', 'Weekend'),
    '시간:아침': ('time', 'Morning'), '시간:점심': ('time', 'Lunch'), '시간:밤': ('time', 'Night'),
    '시간:오후': ('time', 'Afternoon'), '시간:저녁': ('time', 'Evening'),
}
row_re = re.compile(r'^\|\s*\d+\s*\|\s*(.+?)\s*\|\s*(Idle|Walk)\s*\|\s*`([^`]+)`\s*\|')
for raw in r2.split('\n'):
    m = row_re.match(raw)
    if not m:
        continue
    label = m.group(1).replace('*', '').strip()
    if label not in AXIS_MAP:
        continue
    want[m.group(3)] = AXIS_MAP[label] + (m.group(2),)
check(len(want) == 12, 'R2 §3-4에서 조건부 12줄을 파싱했다', '파싱 %d줄' % len(want))

# --- 2026-09-06 §1 표에서 신규 4줄 ---
r23 = io.open(R23_LINES, encoding='utf-8').read()
row2 = re.compile(r'^\|\s*시간:(오후|저녁)[^|]*\|\s*(Idle|Walk)\s*\|\s*\*\*`([^`]+)`\*\*\s*\|')
new = 0
for raw in r23.split('\n'):
    m = row2.match(raw)
    if not m:
        continue
    want[m.group(3)] = AXIS_MAP['시간:' + m.group(1)] + (m.group(2),)
    new += 1
check(new == 4, '2026-09-06 §1에서 신규 4줄을 파싱했다', '파싱 %d줄' % new)
check(len(want) == 16, '대조 대상 조건부 16줄', '%d줄' % len(want))

# --- 프로덕션 쪽 실제 배선 ---
got = {}
for table, a in axes.items():
    for i, text in enumerate(a['lines']):
        d = a['day'][i][0].split('.')[-1]
        t = a['time'][i][0].split('.')[-1]
        if d != 'None':
            got[text] = ('day', d, table)
        elif t != 'None':
            got[text] = ('time', t, table)

for text, spec in sorted(want.items()):
    check(got.get(text) == spec, '「%s」 -> %s' % (text, spec), '실제 %s' % (got.get(text),))
extra = set(got) - set(want)
check(not extra, '문서에 없는 조건부 줄이 배선되지 않았다', repr(sorted(extra)))

print()
print('=== 3. 기각된 문구 «발이 빨라지네» (부재 단언 + 대조) ===')
check('주말이 코앞이네' in walk, '[대조] 정본 대체 문구 «주말이 코앞이네»가 실재한다')
check('발이 빨라지네' not in idle + walk, '기각된 «발이 빨라지네»가 배선되지 않았다')

print()
print('=== 4. 시간대 5구간 경계 — R23 확정값과 대조 ===')
policy = io.open(os.path.join(ROOT, 'Assets/_Project/Scripts/Dialogue/AmbientCalendarPolicy.cs'),
                 encoding='utf-8').read()
bounds = {k: int(v) for k, v in
          re.findall(r'public const int (\w+StartHour) = (\d+);', policy)}
expect_bounds = {'MorningStartHour': 5, 'LunchStartHour': 11, 'AfternoonStartHour': 14,
                 'EveningStartHour': 18, 'NightStartHour': 22}
check(bounds == expect_bounds, '경계 상수 5개가 R23 확정값(05/11/14/18/22)과 같다', repr(bounds))

r23b = io.open(R23_BOUNDS, encoding='utf-8').read()
for label, lo, hi, hours in [('아침', 5, 11, 6), ('점심', 11, 14, 3), ('오후', 14, 18, 4),
                             ('저녁', 18, 22, 4), ('밤', 22, 5, 7)]:
    check('%s %02d-%02d' % (label, lo, hi) in r23b,
          'R23 출력이 %s %02d-%02d 를 그대로 적고 있다' % (label, lo, hi))


def time_bucket(h):
    if h >= bounds['NightStartHour'] or h < bounds['MorningStartHour']: return 'Night'
    if h < bounds['LunchStartHour']: return 'Morning'
    if h < bounds['AfternoonStartHour']: return 'Lunch'
    if h < bounds['EveningStartHour']: return 'Afternoon'
    return 'Evening'


hours = {}
for h in range(24):
    hours.setdefault(time_bucket(h), []).append(h)
check(sum(len(v) for v in hours.values()) == 24 and len(hours) == 5,
      '5구간이 24시간을 빈틈 0 · 겹침 0으로 덮는다',
      ' / '.join('%s %dh' % (k, len(v)) for k, v in hours.items()))

print()
print('=== 5. 후보 수 N 전수 (7요일 x 24시) — R23 계약 숫자와 대조 ===')
DAY = {0: 'Monday', 4: 'Friday', 5: 'Weekend', 6: 'Weekend'}   # 0=월 … 6=일


def eligible(table, day_b, time_b, motion_on):
    a = axes[table]
    n = 0
    for i in range(len(a['lines'])):
        d = a['day'][i][0].split('.')[-1]
        t = a['time'][i][0].split('.')[-1]
        m = a['motion'][i][0] if table == 'Idle' else 'null'
        if d != 'None' and d != day_b: continue
        if t != 'None' and t != time_b: continue
        if m != 'null' and not motion_on: continue
        n += 1
    return n


tally = {}
for d in range(7):
    for h in range(24):
        db, tb = DAY.get(d, 'None'), time_bucket(h)
        n = eligible('Idle', db, tb, False) + eligible('Walk', db, tb, False)
        tally.setdefault(n, []).append((d, h))
worst = min(tally)
best = max(tally)
print('     후보 수 분포: ' + ' / '.join('N=%d %d시간' % (k, len(v)) for k, v in sorted(tally.items())))
check(worst == 12, 'R23 «최악 조합 N=12» 재현', 'worst=%d' % worst)
check(best == 14, '요일 자격이 있는 날은 N=14(상시10 + 요일2 + 시간대2)', 'best=%d' % best)

# 모션 2줄까지 켠 상한 = R23이 적은 N=16 (mon·morn·look,yawn)
n_max = eligible('Idle', 'Monday', 'Morning', True) + eligible('Walk', 'Monday', 'Morning', True)
check(n_max == 16, 'R23 «N=16 (mon·morn·look,yawn)» 재현', 'n=%d' % n_max)
check('N=12' in r23b and 'N=16' in r23b, '[대조] R23 출력에 실제로 N=12 / N=16 이 적혀 있다')

# 어느 조합에서도 각 표가 0이 되지 않는다
zero = [(d, h) for d in range(7) for h in range(24)
        if min(eligible('Idle', DAY.get(d, 'None'), time_bucket(h), False),
               eligible('Walk', DAY.get(d, 'None'), time_bucket(h), False)) == 0]
check(not zero, '어떤 (요일 x 시각)에서도 Idle/Walk 후보가 0이 되지 않는다', repr(zero[:3]))

print()
print('=== 6. 발화 자격 게이트 — 새 16줄이 구조적으로 도달 가능한가 ===')
cfg = io.open(os.path.join(ROOT, 'Assets/_Project/Scripts/Core/StickConfig.cs'), encoding='utf-8').read()
kind = io.open(os.path.join(ROOT, 'Assets/_Project/Scripts/Dialogue/DialogueKind.cs'), encoding='utf-8').read()


def const_f(text, pattern):
    m = re.search(pattern, text)
    assert m, pattern
    return float(m.group(1))


base = const_f(kind, r'BaseSeconds = ([\d.]+)f')
per = const_f(kind, r'PerGlyphSeconds = ([\d.]+)f')
mn = const_f(kind, r'MinSeconds = ([\d.]+)f')
mx = const_f(kind, r'MaxSeconds = ([\d.]+)f')
fade = const_f(kind, r'FadeInSeconds = ([\d.]+)f')
jit = const_f(cfg, r'wanderDurationJitterRatio = ([\d.]+)f')
idle_min = const_f(cfg, r'wanderIdleDurationMin = ([\d.]+)f')
walk_min = const_f(cfg, r'wanderWalkDurationMin = ([\d.]+)f')
floor = 1.0 - jit
print('     상수 참조: base=%.3f per=%.3f min=%.2f max=%.2f fadeIn=%.2f jitter=%.3f' %
      (base, per, mn, mx, fade, jit))
# [대조] 기존 최장 줄로 계산기를 먼저 교정한다(설계 문서가 발표한 값과 맞아야 한다).
cal = min(max(base + 9 * per, mn), mx) + fade
check(abs(cal - 1.015) < 1e-9, '[교정] 9자 필요체류 = 1.015초 (설계 문서 발표값)', '%.4f' % cal)

for table, plan in (('Idle', idle_min * floor), ('Walk', walk_min * floor)):
    for text, spec in sorted(want.items()):
        if spec[2] != table:
            continue
        need = min(max(base + len(text) * per, mn), mx) + fade
        check(need < plan, '%s 「%s」(%d자) 필요체류 %.3f < 최단계획 %.4f'
              % (table, text, len(text), need, plan))

print()
if fails:
    print('★ FAIL %d건: %s' % (len(fails), fails))
    sys.exit(1)
print('전 항목 통과.')
