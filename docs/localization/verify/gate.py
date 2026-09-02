#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
발화 게이트 단위 오류 — 독립 재계산 + 한국어 불변 검증 + 양성/음성 대조.

`design-narrative` §5, `game-architect` 검증, `design-motion` §3-3의 세 숫자가 서로
다르게 인용되고 있어(분모 2.00/1.50 vs 1.65/1.24) **여기서 한 번 통일해서 다시 푼다.**

교정(calibration) 먼저: 알려진 값으로 맞춘다. 깨지면 그 뒤 숫자는 전부 폐기한다.
"""
import sys, io, os, json, unicodedata

BASE = 0.28
PER_KR = 0.075
PER_EN = 0.0472          # design-narrative §5-3 (말뭉치 전체 비율 112/178 = 0.629 × 0.075)
MIN_S = 0.62
MAX_S = 2.20
FADE_IN = 0.06
POP_IN = 0.18


def clamp(v, lo, hi):
    return lo if v < lo else (hi if v > hi else v)


# ---------------------------------------------------- 현행 / 개정 ReadingSeconds
def reading_current(text):
    """현행 프로덕션 식 그대로: clamp(0.28 + len(text)*0.075, 0.62, 2.20)."""
    n = 0 if not text else len(text)
    return clamp(BASE + n * PER_KR, MIN_S, MAX_S)


HANGUL_SYL = lambda c: 0xAC00 <= ord(c) <= 0xD7A3
JAMO = lambda c: (0x1100 <= ord(c) <= 0x11FF or 0x3130 <= ord(c) <= 0x318F
                  or 0xA960 <= ord(c) <= 0xA97F or 0xD7B0 <= ord(c) <= 0xD7FF)
CJK = lambda c: (0x4E00 <= ord(c) <= 0x9FFF or 0x3400 <= ord(c) <= 0x4DBF
                 or 0xF900 <= ord(c) <= 0xFAFF)
KANA = lambda c: 0x3040 <= ord(c) <= 0x30FF or 0x31F0 <= ord(c) <= 0x31FF


def is_dense_script(text):
    """글자 하나가 음절 하나인 문자체계가 **하나라도** 섞였는가.
    섞이면 비싼 쪽(0.075)으로 청구한다 — 과다 청구의 결과는 침묵이고, 침묵은 거짓말이 아니다."""
    if not text:
        return True
    for c in text:
        if HANGUL_SYL(c) or JAMO(c) or CJK(c) or KANA(c):
            return True
    return False


def reading_revised(text, per_latin=PER_EN):
    n = 0 if not text else len(text)
    per = PER_KR if is_dense_script(text) else per_latin
    return clamp(BASE + n * per, MIN_S, MAX_S)


def required_dwell(text, fn=reading_revised):
    return FADE_IN + fn(text)


# ---------------------------------------------------- 교정
def calibrate():
    """리더/`design-motion`이 제시한 값 재현. 이게 깨지면 아래 숫자를 전부 폐기한다."""
    cases = [(10, 1.090), (8, 0.940), (7, 0.865)]
    ok = True
    print('== 교정 (알려진 값으로 계산기를 맞춘다) ==')
    for n, expect in cases:
        got = required_dwell('가' * n, reading_current)
        good = abs(got - expect) < 5e-4
        ok &= good
        print('  %s  %d자 -> %.3f  (기대 %.3f)' % ('PASS' if good else 'FAIL', n, got, expect))
    return ok


# ---------------------------------------------------- 현행 대사 18줄(한국어) 불변 검증
KR_LINES = [
    # AmbientChatter
    "음...", "여기 좋네", "심심하다", "잠깐 쉬는 중", "오늘 뭐 하지", "하암...",
    "발밑이 단단해", "구경 중이야",
    "산책 중", "저쪽으로 가볼까", "하나 둘 하나 둘", "다리 좀 풀자", "다리가 잘 나가네",
    # States
    "한 발 더!", "오늘은 여기까지",
    "여기로 내려가자", "어우... 꽤 깊네",
    "가뿐하네", "영차...", "헉... 높다",
    "윽...!", "으악!", "으아아아악?!",
    "흥... 그럼 한 입만이다", "나 안 해!", "어... 알았어, 갈게", "심심해서 왔어...",
    "헥헥... 안 되겠다...",
    # StickmanAgent (집중 모드) — 소스 스캐너 사각지대였던 5줄
    "좋아, 감시 시작", "수고했어!", "그래 쉬자", "어? 딴 데 보고 있네?", "아 몰라...",
]


def korean_invariance():
    print('== 한국어 비트 단위 불변 (현행 %d줄) ==' % len(KR_LINES))
    worst = 0.0
    bad = []
    for t in KR_LINES:
        a = reading_current(t)
        b = reading_revised(t)
        d = abs(a - b)
        worst = max(worst, d)
        if d != 0.0:
            bad.append((t, a, b))
    print('  최대 차이 = %r  (0.0 이어야 한다)' % worst)
    for t, a, b in bad:
        print('   DIFF %r  %.6f -> %.6f' % (t, a, b))
    return not bad


def mixed_case():
    print('== 혼합 문자열은 비싼 쪽(한국어 계수)으로 간다 ==')
    t = "Wi-Fi 끊겼네"
    a, b = reading_current(t), reading_revised(t)
    print('  %r  현행 %.4f / 개정 %.4f  -> %s' % (t, a, b, '동일' if a == b else '★다름'))
    return a == b


# ---------------------------------------------------- 영어 풀 24줄
# design-narrative §3-4 표의 English 열. 분모는 **design-motion §3-3이 보증한 값**.
DENOM = {'Idle': 1.65, 'Walk': 1.24, 'ParkourClimb': 1.20, 'LedgeHang': 1.12}
DENOM_NARRATIVE = {'Idle': 2.00, 'Walk': 1.50, 'ParkourClimb': 1.20, 'LedgeHang': 1.12}

POOL24 = [
    (1, 'Idle', '음...', 'Hmm...'),
    (2, 'Idle', '여기 좋네', 'Nice spot.'),
    (3, 'Idle', '잠깐 쉬는 중', 'Taking a break.'),
    (4, 'Idle', '오늘 뭐 하지', 'What now?'),
    (5, 'Idle', '발밑이 단단해', 'Solid footing.'),
    (6, 'Walk', '산책 중', 'Out walking.'),
    (7, 'Walk', '저쪽으로 가볼까', "Let's go that way."),
    (8, 'Walk', '하나 둘 하나 둘', 'Left, right, left.'),
    (9, 'Walk', '다리 좀 풀자', 'Stretching the legs.'),
    (10, 'Walk', '다리가 잘 나가네', 'Good stride today.'),
    (11, 'Idle', '하암...', '*yawn*'),
    (12, 'Idle', '구경 중이야', 'Just having a look.'),
    (13, 'Idle', '월요일이네...', 'Monday again...'),
    (14, 'Idle', '금요일이다!', "It's Friday!"),
    (15, 'Idle', '쉬는 날이네', 'Day off.'),
    (16, 'Walk', '월요일이 왔네', "Monday's here."),
    (17, 'Walk', '주말이 코앞이네', "Weekend's close."),
    (18, 'Walk', '주말 산책이네', 'Weekend walk.'),
    (19, 'Idle', '아침이네', 'Morning.'),
    (20, 'Idle', '점심시간이네', 'Lunchtime.'),
    (21, 'Idle', '밤이 깊었네', "It's late."),
    (22, 'Walk', '아침 산책이네', 'Morning walk.'),
    (23, 'Walk', '점심때 걷네', 'Midday stroll.'),
    (24, 'Walk', '밤에도 걷네', 'Night walk.'),
]

# 리더 브리핑이 언급한 번역 3건(§5-4 재판정 대상)
TRANSLATED3 = [
    ('ParkourClimb', "Whoa, that's high"),
    ('Walk', 'Left, right, left, right'),
    ('LedgeHang', "Whoa... that's deep"),
]


def budget_chars(denom, per, margin):
    """이 분모/계수/여유에서 통과 가능한 최대 글자수."""
    n = 0
    while True:
        need = FADE_IN + clamp(BASE + (n + 1) * per, MIN_S, MAX_S) + margin
        if need > denom + 1e-9:
            return n
        n += 1
        if n > 400:
            return n


def pool_check(margin, denom_table, label):
    print('== 풀 24줄 판정 — 분모=%s, 여유요구=%.2f초 ==' % (label, margin))
    print('  상한 글자수:', {k: (budget_chars(v, PER_KR, margin), budget_chars(v, PER_EN, margin))
                          for k, v in denom_table.items()}, '  (한글, 영어)')
    fails_kr, fails_en = [], []
    min_kr, min_en = 99, 99
    for i, st, kr, en in POOL24:
        d = denom_table[st]
        s_kr = d - required_dwell(kr, reading_current)
        s_en = d - required_dwell(en, reading_revised)
        min_kr = min(min_kr, s_kr)
        min_en = min(min_en, s_en)
        if s_kr < margin:
            fails_kr.append((i, st, kr, len(kr), round(s_kr, 3)))
        if s_en < margin:
            fails_en.append((i, st, en, len(en), round(s_en, 3)))
    print('  최소 여유: 한글 %+0.3f / 영어 %+0.3f' % (min_kr, min_en))
    print('  탈락: 한글 %d줄 / 영어 %d줄' % (len(fails_kr), len(fails_en)))
    for f in fails_kr:
        print('    KR ✗ #%d %-13s %r (%d자) 여유 %+0.3f' % f)
    for f in fails_en:
        print('    EN ✗ #%d %-13s %r (%d자) 여유 %+0.3f' % f)
    return fails_kr, fails_en


def counter_example():
    """§5-2 결정적 반증 재현 — 음절 수가 같은데 영어만 침묵하는가."""
    print('== 결정적 반증 재현 (음절 vs 글자) ==')
    rows = [('헉... 높다', 3, "Whoa, that's high", 3, 'ParkourClimb'),
            ('어우... 꽤 깊네', 5, "Whoa... that's deep", 3, 'LedgeHang')]
    for kr, skr, en, sen, st in rows:
        d = DENOM[st]
        cur_en = required_dwell(en, reading_current)
        rev_en = required_dwell(en, reading_revised)
        cur_kr = required_dwell(kr, reading_current)
        print('  %-14s 음절%d 자%2d  필요체류 %.3f  |  %-20s 음절%d 자%2d  현행 %.3f -> 개정 %.3f  (분모 %.2f)'
              % (kr, skr, len(kr), cur_kr, en, sen, len(en), cur_en, rev_en, d))
        print('      현행: %s / 개정: %s'
              % ('침묵' if cur_en > d else '발화', '침묵' if rev_en > d else '발화'))


def negative_control():
    """★ 양성 대조의 짝 — 계수를 틀리게 넣으면 실제로 빨개지는가."""
    print('== 양성/음성 대조: 계수를 틀리게 넣으면 빨개지는가 ==')
    ok = True

    # (1) 한국어 불변 테스트가 '무조건 통과'하는 껍데기가 아님을 보인다.
    #     ★ **개정 쪽만** 1틱 틀리게 만든다(양쪽을 같이 바꾸면 차이가 0이라 아무것도 못 잰다 —
    #       이것이 이 저장소가 반복해 밟은 "실패한 측정과 성공한 측정이 똑같이 생겼다"의 형태다).
    def reading_revised_broken(text):
        n = 0 if not text else len(text)
        per = 0.0751 if is_dense_script(text) else PER_EN   # ← 한국어 계수를 1틱 틀리게
        return clamp(BASE + n * per, MIN_S, MAX_S)
    diffs = [t for t in KR_LINES if reading_current(t) != reading_revised_broken(t)]
    good = len(diffs) > 0
    ok &= good
    print('  %s 개정 쪽 한국어 계수만 0.075->0.0751 로 틀리면 불변 테스트가 깨진다 (%d/%d줄 차이)'
          % ('PASS' if good else 'FAIL', len(diffs), len(KR_LINES)))

    # (1-b) 분기 자체를 지우면(=영어도 0.075) 역시 깨져야 한다 — 분기가 살아 있음을 증명.
    def reading_revised_nobranch(text):
        n = 0 if not text else len(text)
        return clamp(BASE + n * PER_KR, MIN_S, MAX_S)
    same = all(reading_current(t) == reading_revised_nobranch(t) for t in KR_LINES)
    endiff = sum(1 for _, _, _, en in POOL24 if reading_revised(en) != reading_revised_nobranch(en))
    good = same and endiff >= 20
    ok &= good
    print('  %s 분기를 지우면 한국어는 그대로(%s)인데 영어 %d/24줄이 달라진다 — 분기가 영어에만 걸린다'
          % ('PASS' if good else 'FAIL', '동일' if same else '★다름', endiff))

    # (2) 영어 계수를 한국어 값으로 두면(=고치지 않으면) 영어 풀이 무너져야 한다.
    fk, fe = [], []
    for i, st, kr, en in POOL24:
        d = DENOM[st]
        if d - (FADE_IN + clamp(BASE + len(en) * PER_KR, MIN_S, MAX_S)) < POP_IN:
            fe.append(i)
    good = len(fe) >= 10
    ok &= good
    print('  %s 계수를 고치지 않으면(0.075) 영어 24줄 중 %d줄이 탈락한다' % ('PASS' if good else 'FAIL', len(fe)))

    # (3) 반대 방향 — 계수를 임의로 낮추면(0.030) 조기 소멸 위험 구간이 생긴다.
    lowered = [en for _, _, _, en in POOL24 if reading_revised(en, 0.030) < reading_revised(en, PER_EN) - 0.15]
    good = len(lowered) > 0
    ok &= good
    print('  %s 계수를 0.030으로 낮추면 %d줄의 가독예산이 0.15초 이상 줄어든다(조기 소멸 방향)'
          % ('PASS' if good else 'FAIL', len(lowered)))
    return ok


def handoff():
    """★ design-narrative 인계용 — 상태별 최대 글자수. 리더 판정 (나) 재창작에 쓸 예산이다."""
    print('== ★ design-narrative 인계 예산 (상태별 최대 글자수) ==')
    print('   분모는 design-motion 2026-09-02 R4 §3-3 보증값(지터 반영).')
    print('   %-14s %-7s | %-17s | %s' % ('상태', '분모', '여유 0 (현행 게이트)', '여유 0.18 (§7-3 팝인 마진 도입 시)'))
    for st in ('Idle', 'Walk', 'ParkourClimb', 'LedgeHang'):
        d = DENOM[st]
        print('   %-14s %.2f초  |  한글 %2d / 영어 %2d    |  한글 %2d / 영어 %2d'
              % (st, d,
                 budget_chars(d, PER_KR, 0.0), budget_chars(d, PER_EN, 0.0),
                 budget_chars(d, PER_KR, POP_IN), budget_chars(d, PER_EN, POP_IN)))
    print()
    print('   ★ 리더 판정: 팝인 마진(§7-3)을 적용한다 -> **오른쪽 열이 문안 예산이다**.')
    print('   ★ 주의: 마진을 적용하면 Walk 5줄뿐 아니라 <사건 대사>도 영향을 받는다 —')
    print('           "Whoa, that\'s high"(17자) @ParkourClimb 은 마진 없이는 통과(+0.058)하지만')
    print('           마진 0.18을 요구하면 탈락한다(영어 상한 14자).')
    ev = [('ParkourClimb', "Whoa, that's high"),
          ('LedgeHang', "Whoa... that's deep"),
          ('Walk', 'Stretching the legs.')]
    for st, en in ev:
        d = DENOM[st]
        slack = d - required_dwell(en, reading_revised)
        print('     %-13s %-22r 자%2d  여유 %+0.3f  -> 마진0: %s / 마진0.18: %s'
              % (st, en, len(en), slack,
                 '통과' if slack >= 0 else '탈락',
                 '통과' if slack >= POP_IN else '탈락'))


def main():
    ok = True
    ok &= calibrate()
    if not ok:
        print('\n★ 교정 실패 — 이 아래 숫자는 전부 폐기한다.')
        sys.exit(1)
    print()
    ok &= korean_invariance()
    print()
    ok &= mixed_case()
    print()
    counter_example()
    print()
    print('### A. `design-narrative`가 쓴 분모 (지터 미반영)')
    pool_check(POP_IN, DENOM_NARRATIVE, 'narrative 2.00/1.50/1.20/1.12')
    print()
    print('### B. ★ `design-motion`이 보증한 분모 (지터 반영) — 이쪽이 맞다')
    fk, fe = pool_check(POP_IN, DENOM, 'motion 1.65/1.24/1.20/1.12')
    print()
    print('### B-2. 같은 분모, 여유 요구 0(=팝인 마진 §7-3 미도입 시)')
    pool_check(0.0, DENOM, 'motion 1.65/1.24/1.20/1.12')
    print()
    print('== 리더 브리핑의 번역 3건 재판정 (motion 분모) ==')
    for st, en in TRANSLATED3:
        d = DENOM[st]
        cur = required_dwell(en, reading_current)
        rev = required_dwell(en, reading_revised)
        print('  %-26s @%-13s 자%2d  현행 %.3f(%s) -> 개정 %.3f(%s)  분모 %.2f'
              % (repr(en), st, len(en), cur, '침묵' if cur > d else '발화',
                 rev, '침묵' if rev > d else '발화', d))
    print()
    handoff()
    print()
    ok &= negative_control()
    print()
    print('총평: %s' % ('전부 통과' if ok else '★ 실패 항목 있음'))
    sys.exit(0 if ok else 1)


if __name__ == '__main__':
    main()
