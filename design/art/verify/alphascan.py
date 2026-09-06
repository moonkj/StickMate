#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
design-art — raw α<1 사용처 전수 스캐너 (파일:줄 단위).

「α<1 토큰이 Image.color로 흘러가는가」를 소스에서 잡는다.
Flatten(...)으로 감싸인 것은 안전(α=1 보장)이므로 제외한다.
Fade(token, alpha) / Color.Lerp(a, b, t)는 α를 그대로 나르므로 위험으로 분류한다.
"""

import os
import re
import sys

ROOT = "/Users/kjmoon/App/StickMate/Assets/_Project/Scripts"
UICHROME = os.path.join(ROOT, "Interaction/UiChrome.cs")

TOKEN_RE = re.compile(
    r"public\s+static\s+readonly\s+Color\s+(\w+)\s*=\s*new\s+Color\(\s*"
    r"([0-9.]+)f\s*,\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)")


def translucent_tokens():
    src = open(UICHROME, encoding="utf-8").read()
    out = {}
    for m in TOKEN_RE.finditer(src):
        a = float(m.group(5))
        if a < 1.0:
            out[m.group(1)] = a
    return out


def strip_flattened(line):
    """Flatten( ... ) 호출 안의 텍스트를 지운다 — 그 안의 토큰은 안전하다."""
    out = []
    i = 0
    while i < len(line):
        j = line.find("Flatten(", i)
        if j < 0:
            out.append(line[i:])
            break
        out.append(line[i:j])
        # 괄호 균형 맞추기
        k = j + len("Flatten(")
        depth = 1
        while k < len(line) and depth > 0:
            if line[k] == "(":
                depth += 1
            elif line[k] == ")":
                depth -= 1
            k += 1
        out.append("<FLAT>")
        i = k
    return "".join(out)


def main():
    tokens = translucent_tokens()
    print("α<1 토큰 %d종: %s\n" % (len(tokens),
          ", ".join("%s(%.2f)" % (k, v) for k, v in sorted(tokens.items(), key=lambda kv: kv[1]))))

    lit_re = re.compile(r"new\s+Color\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*,\s*(0?\.[0-9]+)f\s*\)")

    hits = {}
    lit_hits = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        if os.sep + "Tests" in dirpath:
            continue
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, ROOT)
            if rel == "Interaction/UiChrome.cs":
                continue
            for n, raw in enumerate(open(path, encoding="utf-8"), 1):
                line = raw.rstrip("\n")
                code = line.split("//")[0]
                if code.strip().startswith("///") or code.strip().startswith("*"):
                    continue
                code = strip_flattened(code)
                for tok, a in tokens.items():
                    if re.search(r"UiChrome\.%s\b" % tok, code):
                        hits.setdefault(tok, []).append((rel, n, line.strip(), a))
                m = lit_re.search(code)
                if m:
                    al = float(m.group(4))
                    if al < 1.0:
                        lit_hits.append((rel, n, line.strip(), al))

    total = 0
    for tok, a in sorted(tokens.items(), key=lambda kv: -kv[1]):
        rows = hits.get(tok, [])
        if not rows:
            continue
        leak = (1 - a * a) * 100
        # 불투명 위(dstA=1)에서의 비침
        leak_on_opaque = a * (1 - a) * 100
        print("=" * 90)
        print("%s  α%.2f   빈 프레임 위 비침 %.2f%% / 불투명 위 비침 %.2f%%  — raw 사용 %d줄"
              % (tok, a, leak, leak_on_opaque, len(rows)))
        print("=" * 90)
        for rel, n, txt, _ in rows:
            print("  %s:%d" % (rel, n))
            print("      %s" % (txt[:150]))
        total += len(rows)
        print("")

    print("=" * 90)
    print("리터럴 new Color(r,g,b,α<1)")
    print("=" * 90)
    for rel, n, txt, al in lit_hits:
        if al == 0.0:
            continue
        print("  %s:%d  (α%.2f, 불투명 위 비침 %.2f%%)" % (rel, n, al, al * (1 - al) * 100))
        print("      %s" % txt[:150])
    print("\n합계: 토큰 raw %d줄 + 리터럴 %d줄" % (total, len([x for x in lit_hits if x[3] > 0])))


if __name__ == "__main__":
    main()
