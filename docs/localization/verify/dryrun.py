#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Tests/EditMode/DialogueLanguageBudgetTests 의 **오프라인 예행** — Unity 배치모드 순서 대기 중에
같은 주장을 <b>다른 방법으로</b> 다시 잰다(같은 명령 재실행은 검증이 아니다).

★ 상수를 베끼지 않는다. `Dialogue/DialogueKind.cs` 소스에서 읽는다 —
  C# 테스트가 `DialogueBudget.*` 를 참조하는 것과 같은 취지다.
★ 결과 정밀도는 float32다. 다만 <b>중간 계산은 double</b>이다 — Unity Mono의 실제 평가
  방식이 그렇다(머리말 ★★ 참고). 어느 쪽이든 <b>추측하지 말고 러너 실측으로 교정</b>한다.

이 스크립트는 배치모드를 <b>대체하지 않는다</b>. 실기 실행 전까지의 정직한 상태는
"이렇게 동작할 것으로 판단한다, 러너 미확인"이다.

★★ 2026-09-02 — <b>이 파일이 실제로 거짓 통과를 냈다.</b> 처음 판은 float32 산술을
  "연산마다 라운딩"으로 흉내 냈고, 골든 생성기(golden_gen.py)가 <b>같은 오류를 공유</b>했다.
  둘이 같은 함정에 같이 빠졌으므로 서로를 확인해 주지 못했고, 38종 전부 초록이었다.
  <b>Unity EditMode 러너가 13줄의 1 ULP 어긋남을 잡았다.</b>
  진짜 거동: Mono는 float 식을 double로 계산하고 <b>결과를 쓸 때 한 번만</b> 라운딩한다.
  지금은 아래 CALIBRATION 이 <b>러너 실측 비트</b>로 계산기를 먼저 교정한다 —
  교정이 깨지면 그 아래 숫자는 전부 폐기다.
"""
import io, os, re, sys, struct, glob

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
S = os.path.join(ROOT, 'Assets', '_Project', 'Scripts')
GOLDEN = os.path.join(S, 'Tests', 'EditMode', 'Golden', 'DialogueBudgetKoGolden.txt')

FAIL = []


def chk(name, cond, detail=''):
    print(('  PASS  ' if cond else '  FAIL  ') + name + ('   ' + detail if detail else ''))
    if not cond:
        FAIL.append(name)


def f32(x):
    return struct.unpack('<f', struct.pack('<f', x))[0]


def bits(x):
    return struct.pack('>f', f32(x)).hex().upper()


def rd(p):
    return io.open(os.path.join(S, p), encoding='utf-8').read()


# ---------------------------------------------------------------- 상수: 소스에서 읽는다
KIND = rd('Dialogue/DialogueKind.cs')


def const(name):
    m = re.search(r'\b' + name + r'\s*=\s*(-?[0-9]*\.?[0-9]+)f\s*;', KIND)
    if not m:
        print('FATAL: %s 를 DialogueKind.cs 에서 찾지 못했다' % name)
        sys.exit(3)
    return f32(float(m.group(1)))


BASE = const('BaseSeconds')
PER_SYL = const('PerGlyphSeconds')
PER_LAT = const('PerLatinGlyphSeconds')
MIN_S = const('MinSeconds')
MAX_S = const('MaxSeconds')
FADE_IN = const('FadeInSeconds')

# ---------------------------------------------------------------- IsSyllabicScript: 소스의 범위표를 읽는다
RANGES = [(int(a, 16), int(b, 16)) for a, b in
          re.findall(r"c >= '\\u([0-9A-Fa-f]{4})' && c <= '\\u([0-9A-Fa-f]{4})'", KIND)]


def is_syllabic(t):
    if not t:
        return True
    for ch in t:
        o = ord(ch)
        for lo, hi in RANGES:
            if lo <= o <= hi:
                return True
    return False


def clamp(v):
    return MIN_S if v < MIN_S else (MAX_S if v > MAX_S else v)


def reading_with(t, per_syl, per_lat):
    """★ Mono 평가 방식: double로 계산하고 <b>한 번만</b> float으로 라운딩한다."""
    n = len(t) if t else 0
    per = per_syl if is_syllabic(t) else per_lat
    return clamp(f32(BASE + n * per))


# ★ 교정 — Unity EditMode 러너(loc-gate)가 실제로 보고한 비트. 못 내면 아래를 전부 폐기한다.
CALIBRATION = {4: '3F1EB852', 5: '3F27AE15', 7: '3F4E147B', 9: '3F747AE2', 10: '3F83D70A'}


def reading(t):
    return reading_with(t, PER_SYL, PER_LAT)


# ---------------------------------------------------------------- 말뭉치 수집 (C# DialogueCorpus 와 같은 규칙)
RE_SAY = re.compile(r'DialogueLine\.(?:Say|React)\(\s*"([^"]*)"')
RE_SELF = re.compile(r'TriggerSelfReturn\(\s*"([^"]*)"')
RE_LAMBDA = re.compile(r'cfg\s*=>\s*"([^"]*)"')

# ★★ 사각지대 3 (2026-09-03에 이 예행이 놓치고 있던 것) ─────────────────────────────
#   `Dialogue/GrabReactionLines.cs`의 9줄은 `DialogueLine.Say(...)`가 아니라
#   **배열 초기화자**(`static readonly string[] HeadLines = { "머리 놔", ... }`)에 있다.
#   golden_gen.py 는 2026-09-03 00:12에 이 형태를 흡수했는데 이 파일은 안 따라가서,
#   같은 날 예행이 **«골든에는 있는데 소스에 없다» 9줄**을 «삭제»로 보고했다.
#   실제로는 아무것도 삭제되지 않았고 **검사기가 눈이 먼 것**이었다.
#
#   ⇒ 고치는 방식이 중요하다. golden_gen 의 함수를 **import 하지 않는다** —
#     그러면 생성기와 검사기가 코드를 공유해 **같은 방향으로 함께 틀어진다**
#     (TEAM.md «생성기와 검사기가 같이 틀린다»). 대신
#     (a) 형태가 다른 독립 구현으로 짜고(이름 목록이 아니라 **디렉터리 전수 스캔**),
#     (b) **제3의 측정**(census.py 의 C# 어휘 분석기)으로 두 결과를 심판한다.
#
#   ★ 그리고 이 형태는 곧 **주류가 된다** — PLAN §3-4가 영어 대사 풀을 정확히 이
#     모양(`string[] IdleLines`)으로 만들라고 정해 뒀다. 지금 안 고치면 대사가
#     테이블로 옮겨가는 순간 골든이 **조용히 텅 빈다**.
RE_POOL = re.compile(r'static\s+readonly\s+string\[\]\s+(\w+)\s*=\s*\{(.*?)\n\s*\};', re.S)


def pool_literals(src):
    """한 소스 안의 **모든** `static readonly string[] X = { ... };` 에서 리터럴을 긁는다.
    주석 줄은 버린다(`// ★ 사용자 요청 원문…` 안의 인용부호가 대사로 둔갑하지 않게)."""
    out = []
    for _name, body in RE_POOL.findall(src):
        body = '\n'.join(l for l in body.split('\n') if not l.strip().startswith('//'))
        out += re.findall(r'"([^"]*)"', body)
    return out


def scan_all():
    out = []
    # (1) 대사 풀 배열 — 파일 이름을 열거하지 않고 Dialogue/ 전수. 새 풀 파일이 생겨도 따라간다.
    for f in sorted(glob.glob(os.path.join(S, 'Dialogue', '**', '*.cs'), recursive=True)):
        out += pool_literals(io.open(f, encoding='utf-8').read())
    # (2) 상태에서 직접 말하는 형태
    for f in sorted(glob.glob(os.path.join(S, 'States', '**', '*.cs'), recursive=True)):
        out += RE_SAY.findall(io.open(f, encoding='utf-8').read())
    out += RE_SELF.findall(rd('States/RunawayState.cs'))
    out += RE_LAMBDA.findall(rd('Core/StickmanAgent.cs'))
    return [t for t in out if t]


def lexer_arbiter(rel):
    """★ 제3의 측정 — census.py 의 C# 어휘 분석기로 그 파일의 한글 리터럴을 센다.
    정규식 수집기 두 개(golden_gen / 이 파일)가 다투면 이쪽이 심판한다.
    census.py 는 자기 양성/음성 대조 10/10을 따로 갖고 있다."""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from census import lex_csharp, HANGUL
    lits, _masked = lex_csharp(rd(rel))
    return [l['value'] for l in lits if HANGUL.search(l['value'])]


def read_golden():
    rows = []
    for raw in io.open(GOLDEN, encoding='utf-8'):
        raw = raw.rstrip('\n')
        if not raw.strip() or raw.startswith('#'):
            continue
        p = raw.split('\t')
        assert len(p) == 3, raw
        rows.append(tuple(p))
    return rows


LATIN_PROBE = ["Hmm...", "Nice spot.", "Taking a break.", "What now?", "Solid footing.",
               "Out walking.", "Left, right, left.", "Good stride today.",
               "Whoa, that's high", "Whoa... that's deep"]


def main():
    print('=' * 78)
    print('DialogueLanguageBudgetTests 오프라인 예행 (Unity 러너 미실행)')
    print('=' * 78)
    print('소스에서 읽은 상수: Base=%.4f PerSyl=%.4f PerLat=%.4f Min=%.2f Max=%.2f FadeIn=%.2f'
          % (BASE, PER_SYL, PER_LAT, MIN_S, MAX_S, FADE_IN))
    print('IsSyllabicScript 범위 %d개: %s' % (len(RANGES), ' '.join('%04X-%04X' % r for r in RANGES)))
    print()

    print('== 교정 (러너 실측 비트로 계산기를 먼저 맞춘다) ==')
    cal_bad = [(n, w, bits(reading('가' * n))) for n, w in sorted(CALIBRATION.items())
               if bits(reading('가' * n)) != w]
    for n, w, g in cal_bad:
        print('  FAIL  %d자 기대 %s / 계산 %s' % (n, w, g))
    if cal_bad:
        print('\n★ 교정 실패 — 이 아래 숫자는 전부 폐기한다.')
        sys.exit(1)
    print('  PASS  %d개 표본 전부 일치' % len(CALIBRATION))
    print()

    golden = read_golden()
    scanned = sorted(set(scan_all()))

    print('== 0. 말뭉치 ==')
    chk('골든이 비어 있지 않다', len(golden) > 0, '%d줄' % len(golden))
    chk('소스에서 대사를 찾았다', len(scanned) > 0, '%d줄(고유)' % len(scanned))
    gset, sset = set(g[2] for g in golden), set(scanned)
    chk('골든 -> 소스 (삭제 감지)', not (gset - sset), repr(sorted(gset - sset)))
    chk('소스 -> 골든 (추가 감지)', not (sset - gset), repr(sorted(sset - gset)))
    blind = ["좋아, 감시 시작", "수고했어!", "그래 쉬자", "어? 딴 데 보고 있네?", "아 몰라...",
             "어... 알았어, 갈게", "심심해서 왔어..."]
    chk('★ 사각지대 7줄이 전부 들어와 있다', all(b in sset for b in blind),
        repr([b for b in blind if b not in sset]))
    # ★★ 사각지대 3 — 배열 초기화자 대사(붙잡힘 9줄). 2026-09-03에 이 예행이 «삭제»로
    #    잘못 보고했던 자리다. 제3의 측정(census.py 어휘 분석기)이 심판한다.
    grab_lex = set(lexer_arbiter('Dialogue/GrabReactionLines.cs'))
    chk('★★ 사각지대 3 — 배열형 대사 %d줄을 수집기가 전부 본다' % len(grab_lex),
        grab_lex and grab_lex <= sset, repr(sorted(grab_lex - sset)))
    chk('  그 짝(심판) — 어휘 분석기도 같은 파일에서 0건이 아니다',
        len(grab_lex) > 0, '%d건' % len(grab_lex))

    print()
    print('== 0-b. 수집기 양성/음성 대조 (합성 소스) ==')
    with_all = ('void A(){ var i = new DialogueIntent(ctx, _ => DialogueLine.Say("세이형태")); }\n'
                'void B(){ TriggerSelfReturn("자진복귀형태"); }\n'
                'var s = new TimedSpectacleState(bb, id, cfg => cfg.hold, cfg => "람다형태");\n')
    with_none = 'void C(){ var t = "평범한 리터럴"; Debug.Log(t); }\n'
    chk('Say/React 형태를 찾는다', RE_SAY.findall(with_all) == ['세이형태'])
    chk('TriggerSelfReturn 형태를 찾는다(사각지대 1)', RE_SELF.findall(with_all) == ['자진복귀형태'])
    chk('cfg => 형태를 찾는다(사각지대 2)', RE_LAMBDA.findall(with_all) == ['람다형태'])
    pool_src = ('        private static readonly string[] 풀A =\n        {\n'
                '            "배열형1",\n'
                '            // "주석속가짜",\n'
                '            "배열형2",\n        };\n'
                '        private static readonly string[] 합본 = Combine(풀A, 풀A);\n')
    chk('★ 배열 초기화자 형태를 찾는다(사각지대 3)', pool_literals(pool_src) == ['배열형1', '배열형2'])
    chk('★ 그 안의 **주석 줄**은 대사로 세지 않는다', '주석속가짜' not in pool_literals(pool_src))
    chk('★ Combine(...) 합본 배열은 리터럴이 없으므로 중복을 안 만든다',
        pool_literals(pool_src).count('배열형1') == 1)
    chk('음성 대조 — 형태가 없으면 정말 0이다',
        not RE_SAY.findall(with_none) and not RE_SELF.findall(with_none)
        and not RE_LAMBDA.findall(with_none) and not pool_literals(with_none))

    print()
    print('== 1. ★★ 한국어 비트 단위 불변 ==')
    bad = [(t, gb, bits(reading(t))) for gb, gs, t in golden if bits(reading(t)) != gb]
    chk('개정 후 값이 골든과 비트 단위로 같다', not bad, repr(bad[:3]))
    badf = [(t, gs, '%.6f' % reading(t)) for gb, gs, t in golden if '%.6f' % reading(t) != gs]
    chk('F6 표기도 같다', not badf, repr(badf[:3]))

    legacy_bad = [t for t in scanned
                  if bits(clamp(f32(BASE + len(t) * PER_SYL))) != bits(reading(t))]
    chk('구식 단일계수 식과 구조적으로 동치', not legacy_bad, repr(legacy_bad[:3]))

    # ★ 기대값을 골든 비트에서 되살려 만든다(프로덕션 함수로 만들면 함께 틀어져 아무것도 못 잰다).
    def from_bits(h):
        return struct.unpack('>f', bytes.fromhex(h))[0]

    dwell_bad = [t for gb, gs, t in golden
                 if bits(f32(FADE_IN + reading(t))) != bits(f32(FADE_IN + from_bits(gb)))]
    chk('소비자 경로(필요체류)가 골든에서 되살린 값과 같다', not dwell_bad, repr(dwell_bad[:3]))
    # 음성 대조: 이 비교가 공허하지 않은가 — 골든을 1틱 흔들면 반드시 어긋나야 한다
    shaken = [t for gb, gs, t in golden
              if bits(f32(FADE_IN + reading(t))) != bits(f32(FADE_IN + f32(from_bits(gb) + 0.001)))]
    chk('음성 대조 — 골든을 흔들면 필요체류 비교가 깨진다', len(shaken) == len(golden),
        '%d/%d' % (len(shaken), len(golden)))

    print()
    print('== 2. ★★★ 양성 대조 — 틀리게 넣으면 빨개지는가 ==')
    one_tick = f32(PER_SYL + 0.0001)
    chk('변이 자체가 값을 바꾼다', bits(one_tick) != bits(PER_SYL), '%.6f -> %.6f' % (PER_SYL, one_tick))
    differ = sum(1 for gb, gs, t in golden if bits(reading_with(t, one_tick, PER_LAT)) != gb)
    chk('★ 개정쪽 음절계수만 1틱 틀리면 골든과 어긋난다',
        differ > len(golden) // 2, '%d/%d줄' % (differ, len(golden)))

    kr_same = all(bits(reading_with(t, PER_SYL, PER_SYL)) == gb for gb, gs, t in golden)
    en_diff = all(bits(reading_with(e, PER_SYL, PER_SYL)) != bits(reading(e)) for e in LATIN_PROBE)
    chk('★ 분기를 지우면 한국어는 그대로', kr_same)
    chk('★ 분기를 지우면 영어는 전부 달라진다', en_diff)

    en = "Whoa, that's high"
    climb = 1.20
    before = f32(FADE_IN + reading_with(en, PER_SYL, PER_SYL))
    after = f32(FADE_IN + reading(en))
    chk('★ 개정 전에는 침묵, 개정 후에는 발화',
        before > climb and after < climb, '전 %.3f / 후 %.3f (분모 %.2f)' % (before, after, climb))

    print()
    print('== 3. 문자체계 판정 ==')
    cases = [("헉... 높다", True), ("음...", True), ("ㅋㅋㅋ", True), ("こんにちは", True),
             ("カタカナ", True), ("漢字", True), ("Wi-Fi 끊겼네", True),
             ("Whoa, that's high", False), ("Review PR", False), ("...", False), ("9+", False), ("", True)]
    for t, exp in cases:
        chk('  %-20r -> %s' % (t, exp), is_syllabic(t) == exp)

    mixed = "Wi-Fi 끊겼네"
    chk('혼합은 음절 계수로 청구된다',
        bits(reading(mixed)) == bits(clamp(f32(BASE + len(mixed) * PER_SYL))))
    chk('두 계수가 실제로 다른 값을 낸다(단언이 공허하지 않다)',
        bits(reading(mixed)) != bits(reading_with(mixed, PER_LAT, PER_LAT)))

    lat_bad = [e for e in LATIN_PROBE
               if bits(reading(e)) != bits(clamp(f32(BASE + len(e) * PER_LAT)))]
    chk('라틴 전용은 라틴 계수 식과 같다', not lat_bad, repr(lat_bad))

    ratio = PER_LAT / PER_SYL
    chk('계수 비율이 말뭉치 유도값(112/178)과 같다', abs(ratio - 112.0 / 178.0) < 0.001,
        '%.4f vs %.4f' % (ratio, 112.0 / 178.0))

    print()
    print('== 4. 사용자 입력 문자열 ==')
    chk('영문 할일은 라틴 계수', not is_syllabic("Review PR")
        and bits(reading("Review PR")) == bits(clamp(f32(BASE + 9 * PER_LAT))))
    chk('한글 할일은 음절 계수', is_syllabic("리뷰 확인하기"))
    chk('빈 문자열/None 은 하한', bits(reading("")) == bits(MIN_S) and bits(reading(None)) == bits(MIN_S))

    print()
    print('=' * 78)
    print('결과: %s' % ('전부 통과' if not FAIL else '★ 실패 %d건: %s' % (len(FAIL), FAIL)))
    print('=' * 78)
    sys.exit(0 if not FAIL else 1)


if __name__ == '__main__':
    main()
