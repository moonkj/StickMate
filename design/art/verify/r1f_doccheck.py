# -*- coding: utf-8 -*-
"""R1F 문서 편집 게이트 — ① 굵게-조사 렌더 파손  ② 커밋판 글자 삭제 0.

사용:
  python3 r1f_doccheck.py --calibrate
  python3 r1f_doccheck.py --bold <파일> [<파일> ...]
  python3 r1f_doccheck.py --chars <기준판> <새판>

★ 교정 없이는 아무 판정도 내지 않는다(TEAM.md 공통 처방). `--calibrate`가 FAIL이면
  그 뒤 숫자를 전부 폐기한다. 교정에는 **오탐 형태와 고친 형태를 둘 다** 넣어
  「잡는 것」과 「정상으로 읽는 것」을 같이 증명한다(TEAM.md §5 굵게 규칙의 계기 의무).
"""
import re
import sys

from markdown_it import MarkdownIt

HANGUL = re.compile(r"[가-힣ㄱ-ㅎㅏ-ㅣ]")
PUNCT = set("」』)]}%.,!?:;·’\"'>」")


# ----------------------------------------------------------------- 코드 스팬 비우기
def blank_code_spans(line):
    """백틱은 **남기고** 안쪽만 공백으로 바꾼다.

    ★ 백틱을 지우면 열/닫 짝이 흔들려 그 줄의 `**` 위치 판정이 통째로 어긋난다.
      그래서 「안쪽만 비우고 백틱은 남긴다」가 규약이다.
    """
    out = []
    i = 0
    n = len(line)
    while i < n:
        if line[i] == "`":
            # 여는 백틱 런의 길이를 세고 같은 길이의 닫는 런을 찾는다(CommonMark 규칙).
            j = i
            while j < n and line[j] == "`":
                j += 1
            fence = line[i:j]
            k = line.find(fence, j)
            if k == -1:
                out.append(line[i:j])
                i = j
                continue
            out.append(fence)
            out.append(" " * (k - j))
            out.append(fence)
            i = k + len(fence)
        else:
            out.append(line[i])
            i += 1
    return "".join(out)


def strip_fences(text):
    """```펜스``` 블록을 줄 수를 보존한 채 비운다."""
    lines = text.split("\n")
    out = []
    infence = False
    for ln in lines:
        if re.match(r"^\s*(```|~~~)", ln):
            infence = not infence
            out.append("")
            continue
        out.append("" if infence else ln)
    return "\n".join(out)


# ----------------------------------------------------------------- ① 굵게 검사
_md = MarkdownIt("commonmark")


def bold_broken_renderer(text):
    """렌더러 기준: code/pre를 걷어낸 렌더 결과에 리터럴 `**`가 남으면 파손."""
    html = _md.render(text)
    html = re.sub(r"<code>.*?</code>", "", html, flags=re.S)
    html = re.sub(r"<pre>.*?</pre>", "", html, flags=re.S)
    return html.count("**")


def bold_broken_sites(text):
    """구조 기준: 닫는 `**`의 직전이 문장부호이고 직후가 한글인 자리를 센다."""
    sites = []
    for no, raw in enumerate(strip_fences(text).split("\n"), 1):
        ln = blank_code_spans(raw)
        for m in re.finditer(r"\*\*", ln):
            s, e = m.start(), m.end()
            prev = ln[s - 1] if s > 0 else ""
            nxt = ln[e] if e < len(ln) else ""
            if prev in PUNCT and HANGUL.match(nxt or ""):
                sites.append((no, raw.strip()[:110]))
    return sites


CAL = [
    # (이름, 본문, 렌더 파손 기대, 구조 적중 기대)
    ("깨짐 「」+조사",      "**「내용」**조사입니다.", True, True),
    ("깨짐 ()+조사",       "**(내용)**조사입니다.", True, True),
    ("깨짐 %+조사",        "**12.3%**조사입니다.", True, True),
    ("깨짐 .+조사",        "**내용.**조사입니다.", True, True),
    ("정상 「」안쪽 굵게",   "「**내용**」조사입니다.", False, False),
    ("정상 [] 안쪽 굵게",   "[**내용**]조사입니다.", False, False),
    ("정상 () 안쪽 굵게",   "(**내용**)조사입니다.", False, False),
    ("정상 문장 전체",      "**내용까지 전부 감싼다**", False, False),
    ("정상 가나다",         "가**나**다", False, False),
    ("정상 닫은 뒤 공백",    "**내용** 조사입니다.", False, False),
    ("제외 코드 스팬",      "`**내용.**조사`", False, False),
    ("제외 펜스",          "```\n**내용.**조사\n```", False, False),
]


def calibrate():
    ok = True
    print("=" * 84)
    print("[교정] 굵게 검사기 — 오탐 형태와 고친 형태를 둘 다 넣는다")
    print("=" * 84)
    for name, body, want_r, want_s in CAL:
        got_r = bold_broken_renderer(body) > 0
        got_s = len(bold_broken_sites(body)) > 0
        good = (got_r == want_r) and (got_s == want_s)
        ok = ok and good
        print("  %-22s 렌더 %-5s(기대 %-5s) 구조 %-5s(기대 %-5s) %s"
              % (name, got_r, want_r, got_s, want_s, "OK" if good else "**깨짐**"))
    print()
    print("=" * 84)
    print("[교정] 글자 삭제 검사기 — 항등 · 삽입만 · 공백 1자 삭제 · 1자 치환 · 취소선 삽입")
    print("=" * 84)
    base = "카운트다운 링 Ø22 `Accent` 1→0 입니다."
    cases = [
        ("항등",              base, True),
        ("삽입만(취소선)",     base.replace("`Accent`", "~~`Accent`~~ **`WarmAccent`**"), True),
        ("공백 1자 삭제",      base.replace("Ø22 `", "Ø22`", 1), False),
        ("1자 치환(숫자)",     base.replace("Ø22", "Ø20", 1), False),
        ("백틱 삼킴",          base.replace("`Accent`", "Accent"), False),
    ]
    for name, new, want in cases:
        got = chars_survive(base, new)[0]
        good = got == want
        ok = ok and good
        print("  %-22s 생존 %-5s (기대 %-5s) %s" % (name, got, want, "OK" if good else "**깨짐**"))
    print()
    print("교정 결과: %s" % ("전건 통과" if ok else "**깨짐 — 이 도구의 판정을 쓰지 마라**"))
    return ok


# ----------------------------------------------------------------- ② 글자 삭제 검사
def chars_survive(old, new):
    """old의 모든 글자가 new에 **순서대로** 남아 있는가(삽입만 허용).

    줄 단위 다중집합 비교는 「제자리 숫자 교체」를 못 본다 — 그래서 선형 부분수열이다.
    반환: (통과여부, 실패한 old 인덱스, 그 부근 원문)
    """
    i = 0
    n = len(new)
    for k, ch in enumerate(old):
        while i < n and new[i] != ch:
            i += 1
        if i == n:
            return (False, k, old[max(0, k - 60):k + 60])
        i += 1
    return (True, -1, "")


def main():
    args = sys.argv[1:]
    if not args or args[0] == "--calibrate":
        sys.exit(0 if calibrate() else 1)

    if not calibrate():
        sys.exit("교정 실패 — 측정 중단")
    print()

    if args[0] == "--bold":
        bad = 0
        for path in args[1:]:
            text = open(path, encoding="utf-8").read()
            r = bold_broken_renderer(text)
            sites = bold_broken_sites(text)
            bad += r + len(sites)
            print("  %-46s 렌더 리터럴 ** %d개(÷2 = %d자리) · 구조 적중 %d자리"
                  % (path.split("/")[-1], r, r // 2, len(sites)))
            for no, snip in sites:
                print("        :%d  %s" % (no, snip))
        print()
        print("  총 파손 지표 %d" % bad)
    elif args[0] == "--chars":
        old = open(args[1], encoding="utf-8").read()
        new = open(args[2], encoding="utf-8").read()
        ok, idx, ctx = chars_survive(old, new)
        print("  기준판 %d자 · 새판 %d자" % (len(old), len(new)))
        if ok:
            print("  ★ 글자 삭제 0 — 기준판 전체가 순서대로 생존")
        else:
            print("  ✘ 삭제 발생: 기준판 인덱스 %d 부근" % idx)
            print("    ...%s..." % ctx.replace("\n", "\\n"))
        sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
