#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
`.asset` 로컬라이즈 부채 감시 — **디코드해서 재는** 감사  (localization / 2026-09-03)

===============================================================================
왜 이 파일이 따로 있는가
===============================================================================
사용자 확정 3건이 부채를 **구조화**했다:
    "6팩모두" · "출시 이후부터 계속 추가팩 만들거야" · "집중 시계도 디자인 추가할거고"
⇒ 팩 하나당 `.asset` 4종 × (이름 + 설명) = **8건이 매번 는다. 영구히.**

규약은 이미 있다 — `docs/ARCHITECTURE.md:528` 「로컬라이즈 키 · 원문 문자열 금지」.
**그리고 이미 한 번 어겨졌다** — 같은 문서 `:557` 자기 정정: 기본 42종에 한글 84건.

★★★ 그 위반을 `grep`으로는 **영원히 못 본다.**
    Unity YAML은 비ASCII를 `\\uXXXX`로 이스케이프해 적는다.
        displayName: "\\uBCA0\\uB808\\uBAA8"      ← 「베레모」
    그래서 `grep -c '[가-힣]' equip_head_beret.asset` 는 **0** 을 낸다(실측, rc=1).
    **«0건 = 깨끗»으로 읽는 감사는 42개 파일을 구조적으로 못 본다.**

===============================================================================
이 감사가 지키는 원칙
===============================================================================
1. **디코드한 뒤에 센다.** 그리고 raw 스캔도 **함께** 돌려 «raw 0 / 디코드 N» 이라는
   **함정의 실재**를 매번 증명한다. 둘 다 0이면 그건 «깨끗»이 아니라 **스캐너 사망**이다.
2. **원장은 두 칸이다** — `기본 42종`(유예된 84건)과 `그 밖 전부`(상한 0).
   새 파일은 원장에 없으므로 기준선이 0이고, 팩이 하나 들어오는 순간 **8건이 즉시 빨개진다.**
3. **양성/음성 대조를 짝으로** 붙인다(`census.py --selftest` 10/10 을 본으로 삼았다).

사용법:
    python3 packguard.py                 # 실제 트리 감사 (rc: 0=상한 안, 1=초과/무효)
    python3 packguard.py --selftest      # 대조 (깨지면 위 결과를 전부 폐기한다)
    python3 packguard.py --write-ledger  # 현재 상태로 원장을 다시 굽는다 (리더 승인 필요)
"""

import os, re, io, sys, glob

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
ASSETS = os.path.join(ROOT, "Assets")
LEDGER = os.path.join(os.path.dirname(HERE), "ASSET_DEBT.tsv")

HANGUL = re.compile(r'[가-힣ᄀ-ᇿ㄰-㆏]')
ESCAPE = re.compile(r'\\u([0-9a-fA-F]{4})')

# 유저에게 보이는 값을 담는 필드. ★ 이름이 `...Key` 로 끝나면 **키 자리**이고,
#   거기 한글이 들어오면 그건 부채가 아니라 **즉시 결함**이다(PackManifestKeys.IsWellFormed 위반).
USER_FACING = ('displayName', 'description', 'label', 'caption', 'tooltip')
KEY_FIELDS = re.compile(r'^\s*([A-Za-z_]\w*Key)\s*:\s*(.*)$')
VAL_FIELD = re.compile(r'^\s*([A-Za-z_]\w*)\s*:\s*(.*)$')


def decode(s):
    """`\\uXXXX` 를 실제 문자로. 이 한 줄이 이 파일의 존재 이유다."""
    return ESCAPE.sub(lambda m: chr(int(m.group(1), 16)), s)


def scan_text(text, path='<mem>'):
    """한 `.asset` 본문에서 (필드, 값, 종류) 를 뽑는다.
    종류: 'DEBT'(유저 노출 필드의 원문 한글) / 'KEYVIOL'(키 필드에 한글) / 'OK'"""
    hits = []
    for i, line in enumerate(text.split('\n'), 1):
        m = VAL_FIELD.match(line)
        if not m:
            continue
        field, raw = m.group(1), m.group(2).strip()
        if not raw or raw.startswith('{') or raw.startswith('-'):
            continue
        val = decode(raw).strip('"')
        if not HANGUL.search(val):
            continue
        if field.endswith('Key'):
            hits.append((i, field, val, 'KEYVIOL'))
        elif field in USER_FACING:
            hits.append((i, field, val, 'DEBT'))
        else:
            hits.append((i, field, val, 'OTHER'))
    return hits


def scan_file(path):
    return scan_text(io.open(path, encoding='utf-8', errors='replace').read(), path)


def raw_hangul_count(path):
    """★ 함정 증명용 — **디코드하지 않고** 센다. 정상 트리에서는 0이어야 한다."""
    return len(HANGUL.findall(io.open(path, encoding='utf-8', errors='replace').read()))


# ---------------------------------------------------------------- 원장

def read_ledger():
    """{경로: 상한}. 파일이 없으면 None(=원장 없음, 그것도 실패다)."""
    if not os.path.exists(LEDGER):
        return None
    out = {}
    for line in io.open(LEDGER, encoding='utf-8'):
        line = line.rstrip('\n')
        if not line.strip() or line.lstrip().startswith('#'):
            continue
        p = line.split('\t')
        if len(p) != 2:
            continue
        out[p[0]] = int(p[1])
    return out


def rel(path):
    return os.path.relpath(path, ROOT)


def asset_files():
    return sorted(glob.glob(os.path.join(ASSETS, '**', '*.asset'), recursive=True))


def audit():
    files = asset_files()
    ledger = read_ledger()

    print('=' * 78)
    print('.asset 로컬라이즈 부채 감사 — 디코드 기준')
    print('=' * 78)

    # ---- 대조 D: 스캐너가 실제로 파일을 읽었는가 (빈 순회 차단) -------------------
    if not files:
        print('  !! %s 아래 .asset 을 하나도 못 찾았다. 이것은 «부채 0»이 아니라 «측정 무효»다.'
              % rel(ASSETS))
        return False
    print('  훑은 파일 %d개' % len(files))

    if ledger is None:
        print('  !! 원장(%s)이 없다. 상한이 없으면 이 감사는 아무것도 막지 못한다.' % rel(LEDGER))
        return False
    if not ledger:
        print('  !! 원장이 **비어 있다**. 빈 목록을 순회하면 무엇을 넣어도 초록이다(거짓 통과 #5).')
        return False
    print('  원장 항목 %d개 / 합계 상한 %d건' % (len(ledger), sum(ledger.values())))

    # ---- 대조 B: 함정이 실재하는가 ---------------------------------------------
    trapped = [f for f in files if raw_hangul_count(f) == 0 and
               any(k == 'DEBT' for _l, _fl, _v, k in scan_file(f))]
    if not trapped:
        print('  !! 양성 대조 실패 — «raw 0건인데 디코드하면 한글이 나오는» 파일이 하나도 없다.')
        print('     이스케이프 함정이 사라졌거나 **디코더가 죽었다**. 어느 쪽이든 아래 숫자는 무효다.')
        return False
    print('  ★ 함정 실재 확인 — raw grep 0건 / 디코드 후 부채 있음: %d파일 (예: %s)'
          % (len(trapped), os.path.basename(trapped[0])))

    # ---- 본 검사 ---------------------------------------------------------------
    over, keyviol, total = [], [], 0
    unknown_new = []
    print()
    print('  %-58s %5s %5s' % ('파일', '실측', '상한'))
    for f in files:
        hits = scan_file(f)
        debt = [h for h in hits if h[3] == 'DEBT']
        kv = [h for h in hits if h[3] == 'KEYVIOL']
        r = rel(f)
        cap = ledger.get(r)
        total += len(debt)
        if kv:
            keyviol.append((r, kv))
        if cap is None:
            if debt:
                unknown_new.append((r, len(debt)))
                print('  %-58s %5d %5s  ← ★ 원장에 없는 파일 = 상한 0' % (r, len(debt), '0'))
        elif len(debt) > cap:
            over.append((r, len(debt), cap))
            print('  %-58s %5d %5d  ← 초과 +%d' % (r, len(debt), cap, len(debt) - cap))

    if not over and not unknown_new:
        print('  (상한을 넘는 파일 없음)')
    print()
    print('  총 부채 %d건 / 원장 합계 %d건' % (total, sum(ledger.values())))

    ok = True
    if unknown_new:
        print()
        print('  ★★ 원장에 없는 파일에 원문 한글이 들어왔다 — 팩 부채가 자라기 시작한 지점이다:')
        for r, n in unknown_new:
            print('     +%d  %s' % (n, r))
        print('     ⇒ 새 `.asset` 은 `displayNameKey`/`descriptionKey` 를 담아야 한다.')
        ok = False
    if keyviol:
        print()
        print('  ★★★ **키 자리에 한글** — 이건 부채가 아니라 즉시 결함이다')
        print('     (Core/PackManifestKeys.IsWellFormed 가 false 를 낸다 = 팩 로드 거부):')
        for r, kv in keyviol:
            for ln, fld, val, _ in kv:
                print('     %s:%d  %s = %r' % (r, ln, fld, val))
        ok = False
    if over:
        print()
        print('  ★ 원장 상한을 넘었다(숫자는 **내려갈 때만** 손으로 고친다. 올리려면 리더 승인):')
        for r, n, c in over:
            print('     %s  %d > %d' % (r, n, c))
        ok = False
    if total > sum(ledger.values()):
        print('  ★ 총합 초과: %d > %d' % (total, sum(ledger.values())))
        ok = False
    return ok


def write_ledger():
    files = asset_files()
    lines = [
        '# `.asset` 로컬라이즈 부채 원장 (localization, 2026-09-03)',
        '# 형식: <경로>\\t<유저 노출 필드의 원문 한글 건수 상한>',
        '#',
        '# ★ 규칙 1 — **여기 없는 파일의 상한은 0이다.** 새 팩이 원문 한글을 담고 들어오면',
        '#            그 순간 빨개진다. 그것이 이 원장의 유일한 목적이다.',
        '# ★ 규칙 2 — 숫자는 **내려갈 때만** 손으로 고친다(이관이 진행된 만큼). 올리려면 리더 승인.',
        '# ★ 규칙 3 — 아래 84건은 기본 42종의 **유예분**이다(ARCHITECTURE.md:557 자기 정정).',
        '#            새로 생긴 부채가 아니라 이미 있던 것이고, PLAN §10-4 순서로 갚는다.',
        '# 생성기: docs/localization/verify/packguard.py --write-ledger',
        '#         (감사/대조: packguard.py / packguard.py --selftest)',
        '#',
    ]
    total = 0
    for f in files:
        n = len([h for h in scan_file(f) if h[3] == 'DEBT'])
        if n:
            lines.append('%s\t%d' % (rel(f), n))
            total += n
    lines.append('#')
    lines.append('# TOTAL\t%d' % total)
    io.open(LEDGER, 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print('원장 갱신: %s (%d건)' % (rel(LEDGER), total))


# ---------------------------------------------------------------- 대조

_SYNTH_ESCAPED = (
    'MonoBehaviour:\n'
    '  itemId: pack.office.mug\n'
    '  displayName: "\\uBCA0\\uB808\\uBAA8"\n'
    '  description: "\\uD55C\\uCABD\\uC73C\\uB85C \\uB298\\uC5B4\\uC9C4"\n'
    '  requiredLevel: 23\n'
)
_SYNTH_KEYS = (
    'MonoBehaviour:\n'
    '  itemId: pack.office.mug\n'
    '  displayNameKey: pack.office.mug.name\n'
    '  descriptionKey: pack.office.mug.desc\n'
)
_SYNTH_KEYVIOL = (
    'MonoBehaviour:\n'
    '  displayNameKey: "\\uBCA0\\uB808\\uBAA8"\n'
)
_SYNTH_PLAIN = (
    'MonoBehaviour:\n'
    '  displayName: 베레모\n'
)


def selftest():
    ok = [True]

    def chk(name, cond, extra=''):
        print('  %s  %s%s' % ('PASS' if cond else 'FAIL', name, ('   ' + str(extra)) if extra else ''))
        if not cond:
            ok[0] = False

    print('== 대조 A: 디코더 ==')
    chk('\\uXXXX 를 실제 문자로 바꾼다', decode('"\\uBCA0\\uB808\\uBAA8"') == '"베레모"',
        decode('"\\uBCA0\\uB808\\uBAA8"'))
    chk('음성 — 이스케이프가 없으면 원문 그대로', decode('mug') == 'mug')
    chk('음성 — 4자리가 아니면 손대지 않는다', decode('\\u12') == '\\u12')

    print('== 대조 B: ★ 함정의 실재 (이 짝이 이 파일의 핵심) ==')
    raw_hits = len(HANGUL.findall(_SYNTH_ESCAPED))
    chk('① raw 텍스트에서 grep [가-힣] = 0건 — **함정이 실재한다**', raw_hits == 0, 'hits=%d' % raw_hits)
    dec = [h for h in scan_text(_SYNTH_ESCAPED) if h[3] == 'DEBT']
    chk('② 디코드하면 같은 조각에서 2건 — **우리는 안 빠졌다**', len(dec) == 2,
        [h[2] for h in dec])

    print('== 대조 C: 분류 ==')
    chk('키만 담은 조각은 부채 0', scan_text(_SYNTH_KEYS) == [])
    kv = [h for h in scan_text(_SYNTH_KEYVIOL) if h[3] == 'KEYVIOL']
    chk('★ 키 자리에 한글이면 KEYVIOL(부채가 아니라 결함)', len(kv) == 1, kv)
    pl = [h for h in scan_text(_SYNTH_PLAIN) if h[3] == 'DEBT']
    chk('평문(이스케이프 안 된) 한글도 잡는다', len(pl) == 1, pl)

    print('== 대조 D: 실제 트리 ==')
    files = asset_files()
    chk('.asset 을 하나 이상 읽었다(빈 순회 아님)', len(files) > 0, '%d개' % len(files))
    real_total = sum(len([h for h in scan_file(f) if h[3] == 'DEBT']) for f in files)
    chk('실제 트리 부채가 0이 아니다(스캐너가 살아 있다)', real_total > 0, '%d건' % real_total)
    raw_zero = [f for f in files if raw_hangul_count(f) == 0]
    chk('★ 실제 트리에서도 raw 스캔은 대부분 0건이다(함정 재현)',
        len(raw_zero) >= len(files) - 1, '%d/%d 파일이 raw 0건' % (len(raw_zero), len(files)))

    print('== 대조 E: 원장 ==')
    led = read_ledger()
    chk('원장이 존재한다', led is not None)
    chk('원장이 비어 있지 않다(빈 목록 순회 차단)', bool(led), '%d항목' % (len(led) if led else 0))
    if led:
        chk('원장 합계가 실측 총합과 같다', sum(led.values()) == real_total,
            '원장 %d / 실측 %d' % (sum(led.values()), real_total))
        chk('★ 음성 — 원장에 없는 새 경로의 상한은 0이다',
            led.get('Assets/_Project/Resources/Items/pack_office_mug.asset') is None)
    print('== 대조 F: ★★ 감사 경로 생존 — 합성 트리로 실제로 빨개지는지 본다 ==')
    import tempfile, shutil
    global ASSETS, LEDGER
    keep_a, keep_l = ASSETS, LEDGER
    tmp = tempfile.mkdtemp(prefix='packguard-')
    try:
        ASSETS = os.path.join(tmp, 'Assets')
        os.makedirs(os.path.join(ASSETS, 'Items'))
        base = os.path.join(ASSETS, 'Items', 'equip_head_beret.asset')
        io.open(base, 'w', encoding='utf-8').write(_SYNTH_ESCAPED)
        LEDGER = os.path.join(tmp, 'ASSET_DEBT.tsv')
        io.open(LEDGER, 'w', encoding='utf-8').write(
            '# 합성\n%s\t2\n' % os.path.relpath(base, ROOT))
        chk('① 유예분만 있으면 통과(음성 대조)', audit() is True)

        newpack = os.path.join(ASSETS, 'Items', 'pack_office_mug.asset')
        io.open(newpack, 'w', encoding='utf-8').write(_SYNTH_ESCAPED)
        chk('② ★ 새 팩이 원문 한글로 들어오면 실패한다(양성 대조)', audit() is False)

        io.open(newpack, 'w', encoding='utf-8').write(_SYNTH_KEYS)
        chk('③ 같은 새 팩이 **키**를 담으면 통과한다', audit() is True)

        io.open(newpack, 'w', encoding='utf-8').write(_SYNTH_KEYVIOL)
        chk('④ ★ 키 자리에 한글이면 실패한다', audit() is False)

        os.remove(newpack)
        io.open(LEDGER, 'w', encoding='utf-8').write('# 비어 있음\n')
        chk('⑤ ★ 원장이 비면 «통과»가 아니라 실패다(거짓 통과 #5)', audit() is False)

        os.remove(LEDGER)
        chk('⑥ ★ 원장 파일이 없어도 실패다', audit() is False)
    finally:
        ASSETS, LEDGER = keep_a, keep_l
        shutil.rmtree(tmp, ignore_errors=True)
    return ok[0]


def main():
    if '--write-ledger' in sys.argv:
        write_ledger()
        return
    if '--selftest' in sys.argv:
        good = selftest()
        print()
        print('총평: %s' % ('전부 통과' if good else '★ 실패 — 아래/위 감사 숫자를 전부 폐기한다'))
        sys.exit(0 if good else 1)
    good = audit()
    print()
    print('총평: %s' % ('상한 안' if good else '★ 초과/결함/무효'))
    sys.exit(0 if good else 1)


if __name__ == '__main__':
    main()
