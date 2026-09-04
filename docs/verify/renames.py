#!/usr/bin/env python3
# =============================================================================
# 개명 대장 로더 + 검증기 — qa-regression 전용 (2026-09-03 신설)
#
#   python3 docs/verify/renames.py            # 지금 유효한 규칙을 인쇄
#   python3 docs/verify/renames.py --check    # 자기검사(양성/음성 대조)
#
# 이 모듈이 하는 일은 하나다: **개명을 「삭제 1 + 신설 1」로 오인하지 않게 만든다.**
#
# ★ 그런데 이건 «없던 초록을 만들어 내는» 부류의 도구다. 이 저장소가 아홉 번 당한 형태
#   (「실패한 측정과 성공한 측정이 똑같이 생겼다」)에 정확히 노출돼 있다:
#   규칙이 낡으면 **진짜 삭제를 개명으로 흡수해 조용히 초록**이 된다.
#
#   그래서 규칙의 판정 근거를 규칙 자신에게서 얻지 않는다 — **소스 트리(.cs)** 로 잰다.
#     R1  새 이름이 Tests/*.cs 에 메서드 선언으로 실재하는가   (실재하지 않으면 죽은 규칙)
#     R2  옛 이름이 소스 어디에도 없는가                       (있으면 개명이 아니라 분화다)
#     R3  짧은 이름(메서드명)이 충돌하지 않는가                 (같은 이름이 여러 클래스에 있으면 못 쓴다)
#   깨진 규칙은 **적용하지 않고** 사유와 함께 돌려준다. 부르는 쪽이 반드시 인쇄한다.
#
# ★ TEAM.md 「생성기와 검사기가 같이 틀린다」에 대한 답:
#   로더는 compare(regress.sh)와 baseline.py가 공유하지만, **검증의 자는 소스 트리**다.
#   즉 규칙(데이터)과 판정(소스)이 서로 다른 곳에서 온다.
# =============================================================================
import os
import re
import sys
import glob

REPO = "/Users/kjmoon/App/StickMate"
TSV = os.path.join(REPO, "docs/verify/renames.tsv")
TESTROOT = os.path.join(REPO, "Assets/_Project/Scripts/Tests")


def _test_sources():
    return glob.glob(os.path.join(TESTROOT, "**", "*.cs"), recursive=True)


def declared_methods(sources=None):
    """Tests/*.cs 안의 메서드 선언 이름 -> 그 이름이 나온 파일 목록.

    ★ 「소스 텍스트에 문자열이 있다/없다」로 끝내지 않는다(CLAUDE.md 경고).
      메서드 **선언 형태**(`public void 이름(`)만 센다 — 호출·주석·nameof는 세지 않는다.
    """
    out = {}
    pat = re.compile(
        r"^\s*(?:\[[^\]]*\]\s*)*"                       # 어트리뷰트가 같은 줄에 있어도
        r"(?:public|private|protected|internal)\s+"
        r"(?:static\s+|async\s+|virtual\s+|override\s+|sealed\s+)*"
        r"(?:IEnumerator|void|Task|System\.Collections\.IEnumerator)\s+"
        r"([A-Za-z_가-힣][\w가-힣]*)\s*\(",
        re.MULTILINE)
    for p in sources if sources is not None else _test_sources():
        try:
            txt = open(p, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        for m in pat.finditer(txt):
            out.setdefault(m.group(1), []).append(p)
    return out


def name_anywhere_in_sources(name, sources=None):
    """이름이 소스에 **어떤 형태로든** 나오는가(선언·호출·주석·문자열 전부)."""
    hits = []
    for p in sources if sources is not None else _test_sources():
        try:
            if name in open(p, encoding="utf-8", errors="replace").read():
                hits.append(p)
        except OSError:
            continue
    return hits


def parse_tsv(path=TSV):
    """[(old_full, new_full, date, why, lineno)] — 형태만 읽는다. 검증은 validate가 한다."""
    rows = []
    if not os.path.isfile(path):
        return rows
    for i, line in enumerate(open(path, encoding="utf-8", errors="replace"), 1):
        line = line.rstrip("\n")
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        parts = line.split("\t")
        if len(parts) < 2:
            rows.append((None, None, "", f"형식오류(탭 구분 2칸 미만): {line[:60]}", i))
            continue
        old, new = parts[0].strip(), parts[1].strip()
        date = parts[2].strip() if len(parts) > 2 else ""
        why = parts[3].strip() if len(parts) > 3 else ""
        rows.append((old, new, date, why, i))
    return rows


def validate(rows=None, decls=None, sources=None):
    """(ok, rejected) — ok는 적용해도 되는 규칙, rejected는 (줄번호, 사유)."""
    if rows is None:
        rows = parse_tsv()
    if sources is None:
        sources = _test_sources()
    if decls is None:
        decls = declared_methods(sources)
    ok, rejected = [], []
    for old, new, date, why, ln in rows:
        if old is None:
            rejected.append((ln, why))
            continue
        oshort, nshort = old.rsplit(".", 1)[-1], new.rsplit(".", 1)[-1]
        if oshort == nshort:
            rejected.append((ln, f"R0: 옛/새 짧은 이름이 같다({oshort}) — 개명이 아니다"))
            continue
        # R1 — 새 이름이 실재하는가
        if nshort not in decls:
            rejected.append((ln, f"R1: 새 이름 '{nshort}'가 Tests/*.cs 에 메서드 선언으로 없다 "
                                 f"— 죽은 규칙이다(이 규칙은 진짜 삭제를 개명으로 삼킬 수 있다)"))
            continue
        # R3 — 짧은 이름 충돌
        if len(set(decls[nshort])) > 1:
            rejected.append((ln, f"R3: 새 이름 '{nshort}'가 파일 {len(set(decls[nshort]))}곳에 선언돼 있다 "
                                 f"— 짧은 이름으로는 특정할 수 없다"))
            continue
        # R2 — 옛 이름이 아직 살아 있는가
        if oshort in decls:
            rejected.append((ln, f"R2: 옛 이름 '{oshort}'가 아직 선언돼 있다 "
                                 f"— 개명이 아니라 둘 다 사는 상태다"))
            continue
        ok.append((old, new, date, why, ln))
    return ok, rejected


def _chain(mapping):
    """옛→새를 끝까지 따라가 «현재 이름»으로 정규화. 순환은 그 자리에서 멈춘다."""
    def resolve(n):
        seen = {n}
        while n in mapping:
            n = mapping[n]
            if n in seen:
                return n
            seen.add(n)
        return n
    return resolve


def load(quiet=False, path=TSV):
    """부르는 쪽이 쓰는 진입점.

    반환: (canon_full, canon_short, applied, rejected)
      canon_full / canon_short 는 '이름 -> 현재 이름' 함수다.
    """
    rows = parse_tsv(path)
    ok, rejected = validate(rows)
    full = {o: n for o, n, _, _, _ in ok}
    short = {o.rsplit(".", 1)[-1]: n.rsplit(".", 1)[-1] for o, n, _, _, _ in ok}
    return _chain(full), _chain(short), ok, rejected


def banner(ok, rejected, stream=sys.stdout):
    """R3 — 조용한 흡수 금지. 적용된 규칙은 **매번 전부** 인쇄한다."""
    print(f"\n── 개명 대장 {len(ok)}건 적용 / 거부 {len(rejected)}건 "
          f"(docs/verify/renames.tsv) ──", file=stream)
    for o, n, d, w, ln in ok:
        print(f"   개명 {o.rsplit('.', 1)[-1]}\n      → {n.rsplit('.', 1)[-1]}   [{d}] {w[:90]}",
              file=stream)
    for ln, why in rejected:
        print(f"   ✗ {ln}행 거부 — {why}", file=stream)
    if rejected:
        print("   ⚠ 거부된 규칙은 **적용되지 않았다**. 그 이름들은 대조에서 삭제/신설로 보인다 "
              "— 그게 맞다. 규칙을 고치거나 지워라.", file=stream)


# ---- 자기검사 --------------------------------------------------------------
def check():
    rc = 0
    sources = _test_sources()
    print(f"── 양성 대조 0: 테스트 소스를 실제로 읽는가 — {len(sources)}개 .cs")
    if not sources:
        print("  ✗ 0개다. 이 검증기는 아무것도 재지 못한다."); return 1

    decls = declared_methods(sources)
    print(f"── 양성 대조 1: 메서드 선언 추출이 사는가 — {len(decls)}개 이름")
    if len(decls) < 500:
        print(f"  ✗ {len(decls)}개는 너무 적다. 정규식이 한글 메서드명을 못 잡고 있을 수 있다."); rc = 1
    else:
        print("  ✓ 충분하다")

    known = "줄번호_참조를_새로_만들지_않는다"
    print(f"── 양성 대조 2: 실재하는 한글 테스트명을 잡는가 ({known})")
    if known in decls:
        print(f"  ✓ 잡았다 — {os.path.basename(decls[known][0])}")
    else:
        print("  ✗ 못 잡았다. 한글 이름이 통째로 안 걸리면 R1/R2가 전부 무의미하다."); rc = 1

    print("── 음성 대조 3: 존재할 리 없는 이름은 안 잡히는가")
    if "ZZZ_존재하지_않는_테스트명_XYZ123" in decls:
        print("  ✗ 잡혔다 — 추출기가 아무거나 센다."); rc = 1
    else:
        print("  ✓ 안 잡혔다")

    print("── 실제 대장 검증")
    ok, rejected = validate(rows=parse_tsv(), decls=decls, sources=sources)
    banner(ok, rejected)
    if not ok and not rejected:
        print("  · 대장이 비어 있다. 지금은 흡수할 개명이 없다는 뜻 — 그 자체는 정상이다.")
    if rejected:
        print("  ⚠ 거부된 줄이 있다(위 참고). 이건 실패가 아니라 **거부가 작동한 것**이다.")

    # ★★ 여기부터가 진짜 음성 대조 — 「거부가 실제로 무는가」.
    #    이걸 안 하면 위의 '거부 0건'은 「규칙이 다 옳다」와 「검증이 죽었다」를 구분 못 한다.
    import tempfile
    print("── 음성 대조 4: 새 이름이 실재하지 않는 규칙을 거부하는가(R1)")
    with tempfile.NamedTemporaryFile("w", suffix=".tsv", delete=False, encoding="utf-8") as f:
        f.write("A.B.옛이름_아무거나_XYZ\tA.B.ZZZ_존재하지_않는_새이름_XYZ123\t2026-09-03\t고의 불량\n")
        bad1 = f.name
    o1, r1 = validate(rows=parse_tsv(bad1), decls=decls, sources=sources)
    if o1 or not any("R1" in w for _, w in r1):
        print(f"  ✗ 거부하지 못했다(ok={len(o1)}, rejected={r1}) — R1이 죽었다."); rc = 1
    else:
        print(f"  ✓ 거부했다 — {r1[0][1][:70]}")

    print("── 음성 대조 5: 옛 이름이 아직 살아 있는 규칙을 거부하는가(R2)")
    live = known
    other = next((n for n in decls if n != live and len(set(decls[n])) == 1), None)
    with tempfile.NamedTemporaryFile("w", suffix=".tsv", delete=False, encoding="utf-8") as f:
        f.write(f"A.B.{live}\tA.B.{other}\t2026-09-03\t고의 불량 — 옛 이름이 아직 산다\n")
        bad2 = f.name
    o2, r2 = validate(rows=parse_tsv(bad2), decls=decls, sources=sources)
    if o2 or not any("R2" in w for _, w in r2):
        print(f"  ✗ 거부하지 못했다(ok={len(o2)}, rejected={r2}) — R2가 죽었다."); rc = 1
    else:
        print(f"  ✓ 거부했다 — {r2[0][1][:70]}")

    print("── 양성 대조 6: 정상 규칙은 통과하는가(위 두 대조가 '항상 거부'가 아님을 증명)")
    real = [r for r in parse_tsv() if r[0]]
    if real:
        o3, r3 = validate(rows=real, decls=decls, sources=sources)
        if o3:
            print(f"  ✓ {len(o3)}/{len(real)}건 통과 — 검증기는 '전부 거부'가 아니다")
        else:
            print(f"  ✗ 실제 대장 {len(real)}건이 전부 거부됐다: {r3}"); rc = 1
    else:
        print("  · 대장이 비어 이 대조는 판정 불가(미확인).")

    print("── 양성 대조 7: 연쇄 개명(A→B→C)이 C로 정규화되는가")
    f = _chain({"A": "B", "B": "C"})
    print("  ✓ A→C" if f("A") == "C" else "  ✗ A→" + f("A")); rc |= 0 if f("A") == "C" else 1
    print("── 음성 대조 8: 순환(A→B→A)에서 무한루프에 빠지지 않는가")
    g = _chain({"A": "B", "B": "A"})
    print(f"  ✓ 멈췄다 — A→{g('A')}")

    for p in (bad1, bad2):
        try:
            os.remove(p)
        except OSError:
            pass
    print("자기검사 통과" if rc == 0 else "자기검사 실패")
    return rc


if __name__ == "__main__":
    if "--check" in sys.argv:
        sys.exit(check())
    cf, cs, ok, rejected = load()
    banner(ok, rejected)
    sys.exit(1 if rejected else 0)
