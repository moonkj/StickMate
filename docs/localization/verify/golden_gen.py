#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Tests/EditMode/Golden/DialogueBudgetKoGolden.txt 생성기.

★ 이 스크립트는 **판정하지 않는다**. 판정은 C# 테스트
  (Tests/EditMode/DialogueLanguageBudgetTests)가 한다. 여기는 골든을 처음 만들거나
  대사가 늘었을 때 다시 굽는 자리다.

★ 왜 float32 비트를 적는가: "비트 단위로 불변"이 요구사항이고, 십진 표기는 포매터가
  달라지면 함께 흔들린다. IEEE754 single 의 32비트 자체를 적으면 그 흔들림이 사라진다.
  C# 쪽 대응: BitConverter.SingleToInt32Bits(v).ToString("X8").

★ 이 스크립트는 **개정 전 식**(0.075 단일 계수)으로 굽는다 — 골든의 목적이
  "개정 전 한국어 결과"의 동결이기 때문이다. 개정 후 값이 여기에 맞아야 통과다.

★★ 2026-09-02 정정 — <b>float32 산술을 잘못 흉내 냈다가 13줄이 1 ULP 어긋났다.</b>
  처음에는 "연산마다 float32로 라운딩"(stepwise)으로 구웠는데, Unity Mono는
  <c>float</c> 식을 <b>double로 계산하고 결과를 쓸 때 한 번만</b> 라운딩한다(ECMA가 허용하는
  "더 넓은 정밀도"). 두 방식은 글자수 5·7·15·18·20·25에서 <b>정확히 1 ULP</b> 갈린다.
  실측(EditMode 러너 loc-gate): 5자 -> 3F27AE15, 7자 -> 3F4E147B 가 진짜 값이다.

  ★ 더 나쁜 것은 <b>오프라인 예행(dryrun.py)이 같은 오류를 공유해 "전부 통과"를 냈다</b>는 점이다.
    생성기와 검사기가 같은 함정에 같이 빠지면 <b>서로를 확인해 주지 못한다</b>.
    그래서 지금은 아래 CALIBRATION 이 <b>러너가 실제로 뱉은 비트</b>로 계산기를 먼저 교정하고,
    교정이 깨지면 <b>굽기를 거부</b>한다(TEAM.md 공통 처방: 교정이 깨지면 그 뒤 숫자를 전부 폐기).
"""
import re, io, os, sys, glob, struct   # sys: 교정 실패 시 sys.exit(1) — 빠져 있으면 NameError로 죽는다

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
S = os.path.join(ROOT, 'Assets', '_Project', 'Scripts')
GOLDEN = os.path.join(S, 'Tests', 'EditMode', 'Golden', 'DialogueBudgetKoGolden.txt')


def rd(p):
    return io.open(os.path.join(S, p), encoding='utf-8').read()


def f32(x):
    return struct.unpack('<f', struct.pack('<f', x))[0]


def bits(x):
    return struct.pack('>f', x).hex().upper()


BASE, PER, MIN, MAX = f32(0.28), f32(0.075), f32(0.62), f32(2.20)


def reading_legacy(t):
    """개정 **전** 프로덕션 식을 <b>Unity Mono의 평가 방식 그대로</b> 재현한다.

    Mono는 float 식을 double로 계산하고 <b>결과를 float으로 쓸 때 한 번만</b> 라운딩한다.
    여기서는 Mathf.Clamp(float, ...) 의 인자로 넘어가는 지점이 그 한 번이다.
    (연산마다 라운딩하면 글자수 5·7·15·18·20·25 에서 1 ULP 어긋난다 — 머리말 참고.)
    """
    n = len(t) if t else 0
    v = f32(BASE + n * PER)          # ← double로 계산하고 여기서 한 번만 라운딩
    return MIN if v < MIN else (MAX if v > MAX else v)


# ★ 교정 — Unity EditMode 러너(docs/verify/runs/loc-gate_edit.xml)가 실제로 보고한 비트.
#   계산기가 이 값을 못 내면 굽지 않는다. "내 파이썬이 맞겠지"가 이미 한 번 틀렸다.
CALIBRATION = {
    5: '3F27AE15',    # "그래 쉬자" / "여기 좋네" / "하암..." / "윽...!" / "영차..." / "수고했어!"
    7: '3F4E147B',    # "헉... 높다" / "잠깐 쉬는 중" / "오늘 뭐 하지" / "발밑이 단단해" / "아 몰라..."
    4: '3F1EB852',    # 하한(MinSeconds)에 걸리는 구간 — 러너가 어긋남으로 보고하지 않은 값
    9: '3F747AE2',
    10: '3F83D70A',
}


def calibrate():
    bad = [(n, want, bits(reading_legacy('가' * n)))
           for n, want in sorted(CALIBRATION.items()) if bits(reading_legacy('가' * n)) != want]
    for n, want, got in bad:
        print('  교정 실패: %d자 기대 %s / 계산 %s' % (n, want, got))
    if bad:
        print('★ 교정이 깨졌다 — 굽지 않는다. 이 계산기가 내는 숫자는 전부 폐기한다.')
        sys.exit(1)
    print('  교정 통과(%d개 표본, 러너 실측 비트 기준)' % len(CALIBRATION))


def scan():
    """C# DialogueCorpus 와 **같은 규칙**으로 훑는다. 한쪽만 고치면 테스트가 빨개진다."""

    def arr(src, name):
        m = re.search(r'private static readonly string\[\]\s+' + name + r'\s*=\s*\{(.*?)\n        \};', src, re.S)
        if m is None:
            print('  ★ 대사표를 찾지 못했다: %s — 이름이 바뀌었으면 이 스캐너도 함께 고쳐라.' % name)
            sys.exit(1)
        body = '\n'.join(l for l in m.group(1).split('\n') if not l.strip().startswith('//'))
        found = re.findall(r'"([^"]*)"', body)
        if not found:
            print('  ★ 대사표가 비었다: %s — 0건을 그대로 구우면 골든이 조용히 쪼그라든다.' % name)
            sys.exit(1)
        return found

    ambient = rd('Dialogue/AmbientChatter.cs')
    texts = arr(ambient, 'IdleLines') + arr(ambient, 'WalkLines')

    # ★ 사각지대 3(2026-09-02) — 붙잡힘 반응 9줄. AmbientChatter 와 같은 **배열** 형태라
    #   DialogueLine.Say|React 리터럴 스캐너에는 구조적으로 걸리지 않는다.
    #   HeadPool/LegPool 은 위 표들의 **합본**이라 훑지 않는다(중복만 늘어난다).
    grab = rd('Dialogue/GrabReactionLines.cs')
    for name in ('HeadLines', 'LegLines', 'AnyLines', 'FallbackLines'):
        texts += arr(grab, name)
    for f in sorted(glob.glob(os.path.join(S, 'States', '**', '*.cs'), recursive=True)):
        texts += re.findall(r'DialogueLine\.(?:Say|React)\(\s*"([^"]*)"',
                            io.open(f, encoding='utf-8').read())
    # ★ 사각지대 2종 (2026-09-02 이전 스캐너가 못 보던 7줄)
    texts += re.findall(r'TriggerSelfReturn\(\s*"([^"]*)"', rd('States/RunawayState.cs'))
    texts += re.findall(r'cfg\s*=>\s*"([^"]*)"', rd('Core/StickmanAgent.cs'))
    return sorted(set(t for t in texts if t))


HEADER = u"""# 한국어 가독예산 골든 — DialogueBudget.ReadingSeconds 의 <b>비트 단위</b> 동결
#
# 왜 이 파일이 있는가: 2026-09-02 게이트가 문자체계 인식형이 되었다(단위 오류 수정).
#   그 변경의 절대 조건은 "한국어 결과가 한 톨도 바뀌지 않는다"이고, 그것을 식으로 다시
#   확인하면 식이 함께 틀어질 때 같이 틀어진다. 그래서 <b>결과 숫자 자체</b>를 얼린다.
#
# 형식: <IEEE754 single 비트(빅엔디안 16진)>\\t<초, F6>\\t<대사>
#   1열이 판정 기준이다. 2열은 사람이 diff를 읽기 위한 것이고 함께 검사한다.
#
# ★ 이 파일을 손으로 고치지 마라. 대사를 추가/삭제하면 테스트가 <b>양방향으로</b> 실패한다 —
#   그게 의도다. 리뷰어가 diff에서 예산 변화를 직접 보게 하려는 것이다.
#   생성기: docs/localization/verify/golden_gen.py
#
# 표본 = AmbientChatter 배열 + GrabReactionLines 배열 + States/ DialogueLine.Say|React +
#        RunawayState.TriggerSelfReturn + StickmanAgent 집중모드 람다  (고유 {n}줄)
#
"""


def main():
    calibrate()
    uniq = scan()
    with io.open(GOLDEN, 'w', encoding='utf-8') as out:
        out.write(HEADER.replace('{n}', str(len(uniq))))
        for t in uniq:
            v = reading_legacy(t)
            out.write('%s\t%.6f\t%s\n' % (bits(v), v, t))
    print('골든 %d줄 -> %s' % (len(uniq), os.path.relpath(GOLDEN, ROOT)))


if __name__ == '__main__':
    main()
