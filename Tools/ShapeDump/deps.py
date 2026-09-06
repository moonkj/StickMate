# -*- coding: utf-8 -*-
"""컴파일 에러를 읽고 **빠진 프로덕션 소스 파일**을 찾아 준다 — build.sh 의 자동 보강 장치.

왜 있는가 (2026-09-06, qa-regression)
------------------------------------
`build.sh` 의 컴파일 목록은 **손으로 적은 집합**이라 실제 의존성을 못 따라간다. 그 형태가
**두 번 연속** 같은 사고를 냈다:

  · 2026-09-05 (커밋 1f7e139) `ItemCatalog.IsOwned` -> `CurrencyModel` : CS0103 하나로 빌드 사망
  · 2026-09-06 (커밋 0229f52) `ItemCatalog.SubStatTable` -> `CharacterStat` : CS0246 하나로 빌드 사망

두 번 다 그 위에 얹힌 **상시 게이트 둘(prodverify.py · mirrordrift.py)이 함께 멎었다.**
그리고 두 번 다 «게이트가 빨간 상태»가 아니라 **«게이트가 안 도는 상태»**로 나타났다 —
이 저장소가 반복해서 당하는 형태(실패한 측정과 성공한 측정이 똑같이 생겼다) 그대로다.
2026-09-05 에는 `build.sh` 가 **자기 주석으로 이 재발을 예고까지 했는데** 다음 라운드가 그대로 밟았다.
주석은 사람에게 말하고, 사람은 그 파일을 안 읽는다. 그래서 **기계에게 시킨다.**

무엇을 하는가
-------------
컴파일러가 낸 CS0246/CS0103/CS0234 의 **못 찾은 이름**을 뽑아, `Assets/_Project/Scripts` 에서
그 이름을 선언하는 파일을 찾아 돌려준다. C# 을 파싱해 의존성을 추론하지 않는다 —
**진짜 컴파일러가 실제로 못 찾은 것만** 따라간다(추론은 틀리지만 컴파일러는 안 틀린다).

안 건드리는 것
-------------
`CoreShim.cs` 가 일부러 흉내내는 타입의 프로덕션 파일은 **절대 자동으로 넣지 않는다.**
넣으면 흉내와 실물이 같은 이름으로 둘 다 서고(CS0101), 자동 보강이 스스로 빌드를 깬다.
그 집합은 `CoreShim.cs` 를 읽어 **유도**한다 — 여기에 이름을 다시 적지 않는다(그러면 또 갈라진다).

사용법
------
    csc ... 2>&1 | python3 deps.py --known <이미-넣은-파일...>
      stdout : 추가할 파일 절대경로 (한 줄에 하나)
      stderr : 사람이 읽을 진단
      rc 0   : 1개 이상 찾았다
      rc 1   : 못 찾았다(= 자동 보강으로 못 살린다. 컴파일 에러를 사람이 봐야 한다)

    python3 deps.py --selftest     ★ 탐지 경로 생존 + 양성/음성 대조
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "Assets", "_Project", "Scripts")
CORESHIM = os.path.join(HERE, "CoreShim.cs")

# 못 찾은 «이름»을 알려 주는 에러들. 여기 없는 코드(CS0117 같은)는 **자동 보강 대상이 아니다** —
# CS0117 은 "타입은 있는데 멤버가 없다"라서 파일을 더해도 안 낫는다(Shim.cs 를 사람이 고쳐야 한다).
NAME_ERRORS = re.compile(r"error (CS0246|CS0103|CS0234):\s*'([^']+)'")

# 선언부. `partial` 도 잡는다(AccessoryShapeBuilder 가 partial 이다).
DECL_TMPL = r"(?:^|\n)[^\n/]*\b(?:class|struct|interface|enum|record)\s+%s\b"

SKIP_DIRS = {"Tests", "Editor"}


def _strip_comments(text):
    """주석 안의 선언처럼 생긴 문장에 속지 않게 한다(문서 주석에 `class Foo` 가 자주 적혀 있다)."""
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


def _sources():
    for root, dirs, files in os.walk(SRC):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for f in files:
            if f.endswith(".cs"):
                yield os.path.join(root, f)


def shimmed_files():
    """`CoreShim.cs` 가 흉내내는 타입을 선언하는 프로덕션 파일 집합 — 자동 보강 금지 목록.

    ★ 목록을 손으로 적지 않는다. 손으로 적은 목록이 이 도구가 존재하는 이유다."""
    if not os.path.exists(CORESHIM):
        return set()
    shim = _strip_comments(open(CORESHIM, encoding="utf-8").read())
    names = set(re.findall(r"\b(?:class|struct|interface|enum|record)\s+(\w+)", shim))
    out = set()
    for name in names:
        out |= set(declaring_files(name))
    return out


def declaring_files(name):
    pat = re.compile(DECL_TMPL % re.escape(name))
    hits = []
    for path in _sources():
        if pat.search(_strip_comments(open(path, encoding="utf-8").read())):
            hits.append(path)
    return sorted(hits)


def missing_names(errtext):
    """에러 본문에서 «못 찾은 이름»만. 순서를 유지한다(먼저 터진 것부터 갚는다)."""
    out = []
    for _, name in NAME_ERRORS.findall(errtext):
        if name not in out:
            out.append(name)
    return out


def resolve(errtext, known):
    """(추가할 파일 목록, 사람이 읽을 진단 줄 목록)."""
    known = {os.path.abspath(k) for k in known}
    blocked = shimmed_files()
    add, notes = [], []
    for name in missing_names(errtext):
        cands = [c for c in declaring_files(name) if c not in known]
        if not cands:
            notes.append("  ? %-28s 선언한 파일을 못 찾았다 — shim(Shim.cs) 쪽 결손이거나 오타다" % name)
            continue
        blocked_hits = [c for c in cands if c in blocked]
        if blocked_hits:
            notes.append("  x %-28s %s 는 CoreShim.cs 가 일부러 흉내내는 타입이다 — 자동으로 넣지 않는다"
                         % (name, os.path.relpath(blocked_hits[0], SRC)))
            continue
        if len(cands) > 1:
            notes.append("  ? %-28s 선언한 파일이 %d개다(%s) — 사람이 골라야 한다"
                         % (name, len(cands), ", ".join(os.path.relpath(c, SRC) for c in cands)))
            continue
        if cands[0] not in add:
            add.append(cands[0])
            notes.append("  + %-28s <- %s" % (name, os.path.relpath(cands[0], SRC)))
    return add, notes


# ---------------------------------------------------------------------------
# ★ 자기검사 — 「아무것도 안 찾았다」와 「찾을 게 없었다」가 출력상 똑같아지지 않게.
# ---------------------------------------------------------------------------
def selftest():
    ok = True
    print("== deps.py --selftest ==")

    # [0] census — 소스 트리를 실제로 걷고 있는가. 0이면 아래 전부 무의미하다.
    n = sum(1 for _ in _sources())
    print("  [0] census        스캔 대상 .cs %d개" % n)
    if n < 50:
        print("      xx 소스 트리를 못 걷고 있다. 아래 결과는 전부 무효.")
        return 1

    # [1] 탐지 경로 생존 — 이번 사고의 실제 에러 문장 그대로.
    real = ("Assets/_Project/Scripts/Core/ItemCatalog.cs(1178,57): error CS0246: "
            "'CharacterStat' 형식 또는 네임스페이스 "
            "이름을 찾을 수 없습니다.")
    add, _ = resolve(real, [])
    hit = [os.path.relpath(a, SRC) for a in add]
    good = hit == ["Core/EquipmentStatRules.cs"]
    print("  [1] 탐지 경로     CS0246 'CharacterStat' -> %s  %s"
          % (hit or "(없음)", "OK" if good else "xx 못 짚었다"))
    ok &= good

    # [2] 양성 대조 — 2026-09-05 사고(CS0103)도 같은 경로로 잡히는가.
    add, _ = resolve("x.cs(1,1): error CS0103: 'CurrencyModel' ...", [])
    good = [os.path.relpath(a, SRC) for a in add] == ["Core/CurrencyModel.cs"]
    print("  [2] 양성 대조     CS0103 'CurrencyModel' -> %s"
          % ("OK" if good else "xx %s" % [os.path.relpath(a, SRC) for a in add]))
    ok &= good

    # [3] 음성 대조 A — 이미 넣은 파일은 다시 제안하지 않는다(무한 루프 방지).
    add, _ = resolve(real, [os.path.join(SRC, "Core", "EquipmentStatRules.cs")])
    print("  [3] 음성 대조(중복) 이미 넣은 파일은 재제안 안 함             %s"
          % ("OK" if not add else "xx %s" % add))
    ok &= not add

    # [4] 음성 대조 B — CS0117(멤버 없음)은 파일을 더해도 안 낫는다. 손대면 안 된다.
    add, _ = resolve("x.cs(1,1): error CS0117: 'Mathf'에는 'FloorToInt'...", [])
    print("  [4] 음성 대조(CS0117) 멤버 결손은 자동 보강 대상 아님          %s"
          % ("OK" if not add else "xx %s" % add))
    ok &= not add

    # [5] 음성 대조 C — CoreShim 이 흉내내는 타입은 절대 자동으로 넣지 않는다(CS0101 자폭 방지).
    add, notes = resolve("x.cs(1,1): error CS0103: 'EquipmentModel' ...", [])
    blocked = not add and any(" x " in s for s in notes)
    print("  [5] 음성 대조(흉내) 'EquipmentModel' 은 차단됐는가             %s"
          % ("OK" if blocked else "xx %s / %s" % (add, notes)))
    ok &= blocked

    # [6] 차단 목록이 **실제로 비어 있지 않은가**. 비면 [5]가 우연히 통과한다.
    nb = len(shimmed_files())
    print("  [6] 차단 목록     CoreShim.cs 에서 유도한 금지 파일 %d개        %s"
          % (nb, "OK" if nb >= 4 else "xx 유도가 죽었다"))
    ok &= nb >= 4

    print("== selftest %s ==" % ("통과" if ok else "실패"))
    return 0 if ok else 1


def main():
    if "--selftest" in sys.argv:
        return selftest()
    known = []
    if "--known" in sys.argv:
        known = sys.argv[sys.argv.index("--known") + 1:]
    add, notes = resolve(sys.stdin.read(), known)
    for line in notes:
        sys.stderr.write(line + "\n")
    for a in add:
        print(a)
    return 0 if add else 1


if __name__ == "__main__":
    sys.exit(main())
