#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
결과 xml 전수 계수기 — 「998 / 1,015」 재현 방법 기록 (qa-regression, 2026-09-14 후속 ④)

  python3 docs/verify/xml_census.py                      # 기본 뿌리로 지금 센다
  python3 docs/verify/xml_census.py --before 1789379508  # 그 epoch 이전에 이미 있던 고유 결과만(과거 시점 재현 근사)
  python3 docs/verify/xml_census.py <뿌리> [<뿌리> ...]    # 뿌리를 직접 준다

기본 뿌리 = <저장소>/Logs · <저장소>/docs · <저장소>/Tools · /private/tmp/claude-<uid>(있을 때 — 모든 세션의 scratch)

계수 규칙 — 2026-09-14의 두 수치(qa-sfx/sweep.py · reread.py)가 쓴 것과 같다:
  대상  : 뿌리 아래의 일반 파일 `*.xml` (심볼릭 링크 파일·링크 디렉터리는 따라가지 않는다 = `find -type f`와 같다)
  NUnit : 파일 **앞 4,096바이트** 안에 `<test-run`이 있으면 결과 xml로 센다
  고유  : 파일 **바이트 전체의 md5**가 같으면 하나다(경로·이름·mtime 무관 — 미러·복사본은 하나로 접힌다)
  --before : 고유 결과마다 **가장 이른 사본의 mtime**을 잡아 그 값이 EPOCH 이하인 것만 센다

기록된 수치(같은 규칙·같은 뿌리 4개):

  | 시각 (KST)          | 무엇                              | 결과 xml | 고유  | 근거                               |
  |---------------------|-----------------------------------|---------:|------:|------------------------------------|
  | 09-14 18:51:48      | 첫 전수 재판독 (sweep.py)          |    1,046 |   998 | scratch qa-sfx/sweep.out 첫 줄      |
  | 09-14 19:28:13      | 수정된 판정기로 재판독 (reread.py) |        — | 1,015 | scratch qa-sfx/reread.out 첫 줄     |
  | 시각 미상            | verify-change 3단계 독립 재측정    |    1,136 | 1,064 | verify-change 보고(시각 기록 없음)  |
  | 09-14 21:3x         | 같은 규칙의 임시 재계수(스크립트 전) |    1,164 | 1,086 | 후속 ④ 보고                         |
  | 09-14 21:35:10      | 이 스크립트 첫 실행                |    1,173 | 1,095 | 후속 ④ 보고 (Logs 731 · docs 184 · Tools 0 · scratch 258) |

왜 달라졌나 — **지운 것이 아니라 늘어난 것이다.** scratch와 `Logs/`에 새 결과가 계속 떨어진다(몇 분 사이에도 9개가 늘었다).
  - 18:51:48 이후에 수정된 결과 xml 96개(Logs 38 · scratch 58), 고유 88개 증가(21:3x 임시 재계수 기준).
  - `--before 1789379508`(18:51:48)로 재현하면 고유 **998** — 첫 수치와 정확히 같다.
  - `--before 1789381693`(19:28:13)로 재현하면 고유 **1,016** — 기록 1,015와 **1 차이**.
    나중에 생겼지만 mtime이 과거로 찍힌 사본이 과거 시점에 끼어든 것으로 본다
    (`git archive` 미러는 파일을 **커밋 시각**으로 찍는다). 어느 파일인지는 미확인이다.

★ 이 수는 **시점 표지가 아니다.** 과거 판정을 인용할 때는 그때의 산출물 줄(sweep.out 등)을 인용하고,
  지금 다시 세서 맞추려 하지 마라 — 뿌리 안의 파일이 계속 늘고, scratch는 세션이 끝나면 **사라진다**(그러면 수는 줄어든다).
★ 줄어든 수를 «결과가 지워졌다»로, 늘어난 수를 «실행이 늘었다»로 읽지 마라. 뿌리별 줄을 함께 본다.
"""
import hashlib
import os
import sys
import time

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.realpath(__file__))))


def default_roots():
    roots = [os.path.join(REPO, d) for d in ("Logs", "docs", "Tools")]
    scratch = f"/private/tmp/claude-{os.getuid()}" if hasattr(os, "getuid") else None
    if scratch and os.path.isdir(scratch):
        roots.append(scratch)
    return roots


def census(roots, before=None):
    total, first_mtime, by_root = 0, {}, {}
    for root in roots:
        by_root[root] = 0
        for dirpath, dirnames, filenames in os.walk(root, followlinks=False):
            for fn in filenames:
                if not fn.endswith(".xml"):
                    continue
                p = os.path.join(dirpath, fn)
                if os.path.islink(p) or not os.path.isfile(p):
                    continue
                try:
                    with open(p, "rb") as f:
                        raw = f.read()
                    mt = os.stat(p).st_mtime
                except OSError:
                    continue
                if b"<test-run" not in raw[:4096]:
                    continue
                total += 1
                by_root[root] += 1
                h = hashlib.md5(raw).hexdigest()
                first_mtime[h] = min(first_mtime.get(h, mt), mt)
    unique = len(first_mtime) if before is None else sum(1 for v in first_mtime.values() if v <= before)
    return total, unique, by_root


def main(argv):
    before = None
    if "--before" in argv:
        i = argv.index("--before")
        before = float(argv[i + 1])
        argv = argv[:i] + argv[i + 2:]
    roots = argv or default_roots()
    total, unique, by_root = census(roots, before)
    print(f"측정 {time.strftime('%Y-%m-%d %H:%M:%S')} · 뿌리 {len(roots)}개")
    for r, n in by_root.items():
        print(f"  {n:6d}  {r}")
    label = f"고유(첫 사본 mtime ≤ {before:.0f})" if before is not None else "고유(md5)"
    print(f"결과 xml {total} · {label} {unique}")
    if total == 0:
        print("✗ 결과 xml 0개 — 뿌리가 틀렸거나 비었다. 「없다」로 읽지 마라.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
