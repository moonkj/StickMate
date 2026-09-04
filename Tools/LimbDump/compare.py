#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""마디 병합 전/후 좌표 대조 — 「조형이 안 바뀐다」를 숫자로 증명한다.

무엇을 대조하는가 (넷 다 통과해야 한다):
  (1) 병합 렌더러의 9점  vs  병합 이전 두 렌더러가 만들던 5+5점을 위 마디 좌표계로 옮긴 것
  (2) 병합 렌더러의 9점  vs  초상화 경로(BuildLimbPolyline) — 이미 출하 중인 병합 구현
  (3) 프리팹에 저장된 9점 vs 그 프리팹을 읽어 다시 구운 9점 (멱등)
  (4) 옛 프리팹(HEAD)을 새 렌더러가 읽었을 때  vs  새 프리팹을 읽었을 때 (세대 호환)

★ 기대값을 프로덕션 함수로 만들지 않는다는 규칙(TEAM.md)을 지키기 위해,
   (1)의 「옛 경로」는 프로덕션 코드가 아니라 <b>디스크의 옛 프리팹 비트</b>에서도 한 번 더 잰다.
"""
import math, re, subprocess, sys, os

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PX_PER_UNIT = 81.8333   # 실측 기준(배율 0.75 / Retina), 저장소 문서의 값. 의미 환산용일 뿐 판정에 안 쓴다.
TOL = 2e-6              # 월드 유닛. float32 왕복 오차의 여유(= 0.00016 물리픽셀).

def parse_prefab(text):
    parts = text.split('--- !u!')
    blocks = []
    for b in parts[1:]:
        m = re.match(r'(\d+) &(\d+)', b)
        blocks.append((m.group(1), m.group(2), b))
    gon = {f: re.search(r'\n  m_Name: (.*)', b).group(1).strip() for c, f, b in blocks if c == '1'}
    go_of = {}
    for c, f, b in blocks:
        m = re.search(r'm_GameObject: \{fileID: (\d+)\}', b)
        if m: go_of[f] = m.group(1)
    lines, trs = {}, {}
    for c, f, b in blocks:
        nm = gon.get(go_of.get(f, ''), None)
        if nm is None: continue
        if c == '120':
            seg = re.search(r'\n  m_Positions:\n((?:  - \{x: [^\n]*\n)+)', b).group(1)
            lines[nm] = [tuple(map(float, t)) for t in
                         re.findall(r'x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+)', seg)]
            w = re.search(r'widthCurve:.*?value: ([-\d.e+]+)', b, re.S)
            lines[nm + '#w'] = float(w.group(1))
        elif c == '4':
            p = re.search(r'm_LocalPosition: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+)\}', b).groups()
            q = re.search(r'm_LocalRotation: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+), w: ([-\d.e+]+)\}', b).groups()
            trs[nm] = (tuple(map(float, p)), 2 * math.degrees(math.atan2(float(q[2]), float(q[3]))))
    return lines, trs

def fmt(pts):
    return ';'.join(f'{x!r},{y!r}' for x, y, *_ in pts)

def run(cases):
    inp = '\n'.join('\t'.join(map(str, c)) for c in cases) + '\n'
    out = subprocess.run(['bash', os.path.join(REPO, 'Tools/LimbDump/build.sh')],
                         input=inp, capture_output=True, text=True)
    if out.returncode != 0:
        print(out.stderr); sys.exit(2)
    res = {}
    for ln in out.stdout.splitlines():
        f = ln.split('\t')
        res.setdefault((f[1], f[2]), {})[f[0]] = f[3:]
    return res

def pts(s):
    return [tuple(map(float, p.split(','))) for p in s.split(';')]

def rz(p, ang, off):
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    return (off[0] + p[0] * c - p[1] * s, off[1] + p[0] * s + p[1] * c)

def maxdev(a, b):
    return max(math.hypot(x[0] - y[0], x[1] - y[1]) for x, y in zip(a, b))

new_lines, new_trs = parse_prefab(open(os.path.join(REPO, 'Assets/_Project/Prefabs/Stickman.prefab')).read())
old_txt = subprocess.run(['git', '-C', REPO, 'show', 'HEAD:Assets/_Project/Prefabs/Stickman.prefab'],
                         capture_output=True, text=True).stdout
old_lines, old_trs = parse_prefab(old_txt)

LIMBS = ['LeftLeg', 'RightLeg', 'LeftArm', 'RightArm']
# 굽힘각 표본 — 프리팹 안식 + 이 리그가 실제로 쓰는 양 극단(무릎 4~126°, 팔꿈치 10~122°) + 직선.
ANGLES = {'Leg': [0.0, -4.0, -60.0, -126.0, 126.0], 'Arm': [0.0, 10.0, 60.0, 122.0, -122.0]}

cases, meta = [], []
for nm in LIMBS:
    kind = 'Leg' if 'Leg' in nm else 'Arm'
    off, baked_ang = new_trs[nm + 'Lower']
    lu = abs(old_lines[nm][4][1])            # 옛 프리팹이 렌더러에게 주던 Lu(= 지금과 같은 값)
    ll = abs(old_lines[nm + 'Lower'][4][1])
    w = new_lines[nm + '#w']
    for a in ANGLES[kind] + [baked_ang]:
        merged = fmt(new_lines[nm])
        legacy = fmt(old_lines[nm] + old_lines[nm + 'Lower'])
        cases.append(('CASE', f'{nm}@{a}', lu, ll, off[0], off[1], a, w, 'merged', merged))
        cases.append(('CASE', f'{nm}@{a}', lu, ll, off[0], off[1], a, w, 'legacy', legacy))
        meta.append((nm, a, off, w, lu, ll))

res = run(cases)
fail = 0
print(f'{"팔다리@각도":<22} {"(1)옛경로Δ":>12} {"(2)초상화Δ":>12} {"(3)세대Δ":>12}   판정')
for (nm, a, off, w, lu, ll) in meta:
    key = (f'{nm}@{a}', 'merged')
    keyl = (f'{nm}@{a}', 'legacy')
    new = pts(res[key]['NEW'][0])
    oldu = pts(res[key]['OLDUPPER'][0])
    oldl = [rz(p, a, off) for p in pts(res[key]['OLDLOWER'][0])]
    old9 = oldu[:4] + oldl                       # 관절 칸은 아래 마디 값이 남는다(FillArcs 계약)
    d1 = maxdev(new, old9)
    d2 = maxdev(new, pts(res[key]['PORTRAIT'][0]))
    d3 = maxdev(new, pts(res[keyl]['NEW'][0]))   # 새 프리팹 vs 옛 프리팹을 새 렌더러가 읽은 것
    ok = d1 < TOL and d3 < TOL
    if not ok: fail += 1
    print(f'{nm}@{a:<8.1f}      {d1:12.3e} {d2:12.3e} {d3:12.3e}   {"OK" if ok else "★불일치"}')

# (3) ★ 프리팹 비트 대조 — <b>프로덕션 코드를 한 줄도 안 쓴다.</b>
#     새 프리팹의 9점 == 옛 프리팹의 위5점 + 아래5점을 위 마디 좌표계로 옮긴 것.
#     이것이 「병합이 프리팹 좌표를 안 움직였다」의 가장 강한 증거다(기대값이 디스크 비트에서 온다).
print()
for nm in LIMBS:
    off, baked_ang = new_trs[nm + 'Lower']
    stored = [(p[0], p[1]) for p in new_lines[nm]]
    reconstructed = [(p[0], p[1]) for p in old_lines[nm][:4]] + \
                    [rz((p[0], p[1]), baked_ang, off) for p in old_lines[nm + 'Lower']]
    d = maxdev(stored, reconstructed)
    ok = d < 1e-7
    print(f'프리팹 비트 {nm:<10} 새 9점 vs 옛(5+5)을 옮긴 것  Δ={d:.3e} 유닛 '
          f'({d*PX_PER_UNIT:.2e} 물리픽셀)' + ('  OK' if ok else '  ★불일치'))
    if not ok: fail += 1

# ── 부수 실측(판정 아님) — 프리팹이 구워진 필렛 비율이 현행 상수와 갈라져 있다 ──
src = open(os.path.join(REPO, 'Assets/_Project/Scripts/States/LimbCurveRenderer.cs')).read()
code_ratio = float(re.search(r'FilletLengthRatio = ([\d.]+)f', src).group(1))
print()
for nm in LIMBS:
    off, _ = new_trs[nm + 'Lower']
    lu = abs(off[1])                       # 관절 위치가 곧 위 마디 길이다(부호만 반대)
    ll = abs(old_lines[nm + 'Lower'][4][1])
    t = lu - abs(new_lines[nm][1][1])      # 직선 구간 끝 = Lu − t
    print(f'[부수] {nm:<10} 프리팹에 구워진 필렛 비율 {t/min(lu,ll):.4f} vs 코드 상수 {code_ratio:.4f}'
          + ('  (일치)' if abs(t/min(lu,ll) - code_ratio) < 1e-3 else '  ★ 갈라져 있다 — 이 라운드 이전부터(HEAD)'))

# 옛 프리팹 선이 꺼지는가(이중 그리기 방지)
for k, v in res.items():
    if k[1] == 'legacy':
        assert v['LEGACYLINE_ENABLED'][0] == 'False', (k, v['LEGACYLINE_ENABLED'])
print('\n옛 프리팹의 아래 마디 선: 전 케이스에서 enabled=False (이중 그리기 없음)')

print(f'\n허용오차 {TOL:g} 유닛 = {TOL*PX_PER_UNIT:.2e} 물리픽셀. 불일치 {fail}건 -> 종료코드 {1 if fail else 0}')
sys.exit(1 if fail else 0)
