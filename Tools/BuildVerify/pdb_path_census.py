#!/usr/bin/env python3
"""배포물 안 .NET DLL의 PE 디버그 디렉터리(CodeView RSDS) PDB 경로를 전수로 센다. 읽기 전용.

    python3 Tools/BuildVerify/pdb_path_census.py <빌드폴더|zip|dll> [...]
    python3 Tools/BuildVerify/pdb_path_census.py --selftest [<빌드폴더|zip|dll> ...]

왜 이게 있는가 (security L5, 2026-09-14 조사 → 2026-09-28 저장소로 이전):
  배포 DLL의 PE 디버그 디렉터리에는 컴파일러가 PDB 파일의 «경로»를 적어 넣는다. `.pdb` 를
  배포하지 않아도(우리 배포물의 .pdb 는 0개다) 그 경로 문자열은 DLL 안에 남는다. 그래서
  공개 zip 의 StickMate.Runtime.dll 에 개발 PC 의 홈 경로가 실려 나갔다.
  조사·판정의 정본은 docs/security/BUILD_PDB_PATH_LEAK.md 이고, 이 스크립트는 그 문서 §5-1 의
  계수기를 저장소에 옮긴 것이다(다음 사람이 다시 짜지 않게).

이 스크립트의 규칙 세 개 — verify-build.py 의 「양성 대조 강제」와 같은 계열이다:
  1. ★ 종료코드로 판정하지 않는다. 판정은 마지막 「판정:」 줄들이다.
     rc 는 «계기가 살아 있었는가»만 말한다(0 = 살아 있음, 3 = 무효, 2 = 사용법).
     적중이 있어도 rc 는 0이다 — 이 도구는 적용 «전» 산출물에도 돌려 양성 대조로 쓰기 때문이다.
  2. ★ 「0건」을 그냥 내지 않는다. DLL 을 하나도 못 읽었거나 CodeView 가 0이면 그 입력의
     모든 0을 폐기하고 「계기 무효」를 찍는다(경로·형식을 잘못 준 것과 깨끗한 것이 똑같이 생긴다).
  3. ★ 사용자 이름을 출력에 남기지 않는다. 절대경로의 사용자 부분은 <이름>으로 가린다.
     이 저장소는 PUBLIC 이고, 보고서에 붙는 출력이 새 유출 경로가 되어서는 안 된다.

판정 규칙(적용 뒤 첫 빌드) — 문서 §5-2:
  · 본 판정: 새 산출물이 「우리_절대경로=0」이고 [우리] 줄이 0개.
    같은 줄에서 DLL≥100 · CodeView≥80 으로 계기 생존을 함께 본다.
  · 양성 대조: 같은 실행에 적용 «전» 산출물(예: 공개 zip)을 함께 넘겨 그쪽이 1 이상이어야 한다.
    아니면 그 실행의 0은 전부 무효다.
  · 불변 대조: Unity원본_사용자경로(Windows 2 / macOS 3)와 기타절대(Windows 1)는 Unity 배포
    원본이라 그대로여야 정상이다. 줄었다면 다른 DLL 을 읽고 있다는 뜻이다.
"""
import os
import re
import struct
import sys
import zipfile

# 이 프로젝트의 Bee 산출 경로 — 기계·계정과 무관한 표지다.
BEE = re.compile(r'[\\/]Library[\\/]Bee[\\/]artifacts[\\/]')
USER_ABS = re.compile(r'^(/Users/[^/]+/|/home/[^/]+/|[A-Za-z]:[\\/]+Users[\\/]+[^\\/]+[\\/])')

OURS, UNITY_ABS, OTHER_ABS, RELATIVE = '우리', 'Unity원본', '기타절대', '상대'


def mask(p):
    """출력에 사용자 이름을 남기지 않는다."""
    return re.sub(r'^(/Users/|/home/|[A-Za-z]:[\\/]+Users[\\/]+)[^\\/]+', r'\1<이름>', p)


def classify(path):
    """CodeView 경로 하나를 네 갈래로 가른다. 순수 함수 — selftest 가 알려진 값으로 교정한다."""
    absolute = path.startswith('/') or bool(re.match(r'^[A-Za-z]:', path))
    if absolute and BEE.search(path):
        return OURS
    if absolute and USER_ABS.match(path):
        return UNITY_ABS
    if absolute:
        return OTHER_ABS
    return RELATIVE


def codeview_paths(b):
    """PE 를 파싱해 CodeView(RSDS) 경로 목록을 돌려준다. PE 가 아니면 None."""
    if len(b) < 0x40 or b[:2] != b'MZ':
        return None
    pe = struct.unpack_from('<I', b, 0x3c)[0]
    if b[pe:pe + 4] != b'PE\0\0':
        return None
    nsec = struct.unpack_from('<H', b, pe + 6)[0]
    optsz = struct.unpack_from('<H', b, pe + 20)[0]
    opt = pe + 24
    dd = opt + (96 if struct.unpack_from('<H', b, opt)[0] == 0x10b else 112)
    rva, size = struct.unpack_from('<II', b, dd + 6 * 8)
    if not rva or not size:
        return []
    secs, s = [], opt + optsz
    for _ in range(nsec):
        vsz, va, rsz, rp = struct.unpack_from('<IIII', b, s + 8)
        secs.append((va, max(vsz, rsz), rp))
        s += 40
    o = next((rp + rva - va for va, sz, rp in secs if va <= rva < va + sz), None)
    if o is None:
        return []
    out = []
    for i in range(size // 28):
        typ, sz, _, ptr = struct.unpack_from('<IIII', b, o + i * 28 + 12)
        if typ == 2 and b[ptr:ptr + 4] == b'RSDS':
            out.append(b[ptr + 24:ptr + sz].split(b'\0')[0].decode('utf-8', 'replace'))
    return out


def items(arg):
    """입력 하나(폴더 · zip · dll)에서 (표시이름, 바이트)를 흘린다."""
    if not os.path.exists(arg):
        return
    if arg.endswith('.zip'):
        with zipfile.ZipFile(arg) as z:
            for n in z.namelist():
                if n.endswith('.dll') and '/Managed/' in '/' + n:
                    yield n, z.read(n)
    elif os.path.isdir(arg):
        for dp, _, fn in os.walk(arg):
            for f in fn:
                p = os.path.join(dp, f)
                if f.endswith('.dll') and '/Managed' in dp:
                    yield os.path.relpath(p, arg), open(p, 'rb').read()
    else:
        yield os.path.basename(arg), open(arg, 'rb').read()


def census(arg):
    """입력 하나를 센다. (요약 딕트, 적중 목록)을 돌려준다."""
    counts = {OURS: 0, UNITY_ABS: 0, OTHER_ABS: 0, RELATIVE: 0}
    dlls = cv = 0
    hits = []
    for name, b in items(arg):
        paths = codeview_paths(b)
        if paths is None:
            continue
        dlls += 1
        for p in paths:
            cv += 1
            kind = classify(p)
            counts[kind] += 1
            if kind != RELATIVE:
                hits.append((kind, name, mask(p)))
    return {'dlls': dlls, 'cv': cv, 'counts': counts}, hits


def selftest():
    """★ 교정이 깨지면 그 실행의 숫자를 전부 폐기한다. 알려진 값 · 오탐 형태 · 음성을 함께 먹인다."""
    # (경로, 기대 분류) — 문서 §1-1 · §2 의 실측 모양을 그대로 쓴다. 계정 이름은 합성값이다.
    cases = [
        ('/Users/someone/App/StickMate/Library/Bee/artifacts/1900b0aP.dag/StickMate.Runtime.pdb', OURS),
        ('C:\\proj\\Library\\Bee\\artifacts\\1900b0aP.dag\\StickMate.Runtime.pdb', OURS),
        ('./Library/Bee/artifacts/1900b0aP.dag/Kirurobo.UniWindowController.pdb', RELATIVE),
        ('/Users/builduser/build/output/unity/Runtime/UnityEngine.pdb', UNITY_ABS),
        ('C:\\build\\output\\unity\\Runtime\\UnityEngine.pdb', OTHER_ABS),
        # ★ 오탐 형태 — 「상대경로인데 Bee 를 포함」은 우리 조치가 적용된 모양이고 «상대»여야 한다.
        ('Library/Bee/artifacts/1900b0aP.dag/StickMate.Runtime.pdb', RELATIVE),
    ]
    bad = [(p, want, classify(p)) for p, want in cases if classify(p) != want]

    masked = mask('/Users/someone/App/StickMate/Library/Bee/artifacts/x.dag/a.pdb')
    mask_ok = 'someone' not in masked and '<이름>' in masked
    # 음성 — PE 가 아닌 바이트는 None 이어야 한다(「0건」과 「못 읽었다」를 가르는 계기).
    not_pe_ok = codeview_paths(b'not a pe file at all') is None

    for p, want, got in bad:
        print(f'  ✗ 교정 실패: {mask(p)} -> {got} (기대 {want})')
    if not mask_ok:
        print('  ✗ 교정 실패: 가리기가 사용자 이름을 지우지 못했다')
    if not not_pe_ok:
        print('  ✗ 교정 실패: PE 가 아닌 입력을 PE 로 읽었다')

    ok = not bad and mask_ok and not_pe_ok
    print(f'selftest: 분류 {len(cases) - len(bad)}/{len(cases)} · 가리기 {"OK" if mask_ok else "FAIL"} · '
          f'음성(PE 아님) {"OK" if not_pe_ok else "FAIL"} -> {"통과" if ok else "실패"}')
    if not ok:
        print('판정: ✗ 계기 무효 — 교정이 깨졌으므로 이 실행의 모든 숫자를 폐기한다.')
    return ok


def main(argv):
    args = [a for a in argv if a != '--selftest']
    want_selftest = '--selftest' in argv

    if want_selftest and not selftest():
        return 3
    if not args:
        if want_selftest:
            print('입력이 없다 — 교정만 했다. 실제 판정에는 산출물을 함께 넘겨라'
                  '(적용 전 산출물도 같이 넘겨 양성 대조로 쓴다).')
            return 0
        print(__doc__)
        return 2

    invalid = []
    for arg in args:
        if not os.path.exists(arg):
            print(f'  ✗ 계기 무효: {os.path.basename(arg)} 없음')
            invalid.append(arg)
            continue

        summary, hits = census(arg)
        for kind, name, p in hits:
            print(f'  [{kind}] {name} -> {p}')

        label = os.path.basename(os.path.abspath(arg.rstrip(os.sep))) or arg
        c = summary['counts']
        print(f'{label}: DLL={summary["dlls"]} CodeView={summary["cv"]} '
              f'우리_절대경로={c[OURS]} Unity원본_사용자경로={c[UNITY_ABS]} '
              f'기타절대={c[OTHER_ABS]} 상대={c[RELATIVE]}')

        if summary['dlls'] == 0 or summary['cv'] == 0:
            print('  ✗ 계기 무효: DLL 이나 CodeView 를 하나도 못 읽었다 — 경로나 형식을 잘못 준 것이다'
                  '(이 0건을 초록으로 읽지 마라).')
            invalid.append(arg)
        else:
            verdict = '우리 절대경로 0건' if c[OURS] == 0 else f'우리 절대경로 {c[OURS]}건 잔존'
            print(f'  판정: {verdict} (계기 생존: DLL {summary["dlls"]} · CodeView {summary["cv"]})')

    if invalid:
        print(f'판정: ✗ 계기 무효 입력 {len(invalid)}개 — 그 입력의 0건은 근거가 없다.')
        return 3
    print('판정: 계기는 전부 살아 있었다. 위 「우리_절대경로」 숫자로 판정하라'
          '(적용 전 산출물이 1 이상이어야 그 실행의 0이 의미를 갖는다).')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
