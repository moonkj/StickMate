#!/usr/bin/env python3
"""verify-change 독립 재현 — macOS 조합키 글리프가 런타임 소스의 <b>문자열 리터럴</b>에 있는가.

PlatformParityAuditTests.단축키_표기가_플랫폼별_단일_정의처를_거친다 와 같은 규칙을
C#이 아니라 파이썬으로 다시 구현한다. ★ 탐지력을 먼저 증명하고 나서만 "0건"을 말한다.
"""
import os, sys

ROOT = os.path.join(os.path.dirname(__file__), "../../Assets/_Project/Scripts")
SINGLE = os.path.normpath(os.path.join(ROOT, "Core/ShortcutLabel.cs"))
GLYPHS = set("\u2303\u2325\u2318")          # ⌃ ⌥ ⌘

def glyph_in_literal(line):
    """줄 안의 문자열 리터럴 안에만 글리프가 있는가. 주석/식별자는 세지 않는다."""
    i, n = 0, len(line)
    in_str = verbatim = False
    while i < n:
        c = line[i]
        if not in_str:
            if c == '/' and i+1 < n and line[i+1] == '/': return False   # 줄 주석 이후는 없다
            if c == '@' and i+1 < n and line[i+1] == '"': in_str, verbatim = True, True; i += 2; continue
            if c == '"': in_str, verbatim = True, False; i += 1; continue
            if c == "'":                                              # char 리터럴은 건너뛴다
                i += 1
                while i < n and line[i] != "'":
                    i += 2 if line[i] == '\\' else 1
                i += 1; continue
            i += 1
        else:
            if verbatim:
                if c == '"':
                    if i+1 < n and line[i+1] == '"': i += 2; continue
                    in_str = False; i += 1; continue
                if c in GLYPHS: return True
                i += 1
            else:
                if c == '\\': i += 2; continue
                if c == '"': in_str = False; i += 1; continue
                if c in GLYPHS: return True
                i += 1
    return False

def scan(extra=None):
    off = []
    for dp, _, fns in os.walk(ROOT):
        if "/Tests/" in dp.replace("\\","/") + "/": continue
        for fn in fns:
            if not fn.endswith(".cs"): continue
            p = os.path.join(dp, fn)
            if os.path.normpath(p) == SINGLE: continue
            for k, line in enumerate(open(p, encoding="utf-8", errors="replace").read()
                                     .replace("\r\n","\n").split("\n"), 1):
                if glyph_in_literal(line): off.append(f"{fn}:{k}  {line.strip()[:90]}")
    return off

def main():
    print("=== 0. 탐지력(양성 대조) — 스캐너가 실제로 무는가 ===")
    cases = [
        ('        string s = "\u2318X";',                    True,  "리터럴 안 ⌘ — 반드시 잡아야"),
        ('        // \u2318X 는 macOS 표기다',               False, "줄 주석 — 잡으면 오탐"),
        ('        Settings,   // \u2303\u2325\u2318,',       False, "줄 끝 주석 — 옛 오탐 형태"),
        ('        /// <c>\u2318X</c>',                       False, "문서 주석"),
        ('        char c = \'\u2318\';',                     False, "char 리터럴(문자열 아님)"),
        ('        string s = @"\u2325A";',                   True,  "verbatim 리터럴"),
        ('        Log("ok"); // \u2318',                     False, "리터럴 뒤 주석"),
        ('        Log("a" + "\u2303b");',                    True,  "이어붙인 두 번째 리터럴"),
    ]
    ok = True
    for line, want, why in cases:
        got = glyph_in_literal(line)
        good = got == want; ok &= good
        print(f"  [{'OK ' if good else 'FAIL'}] 기대={want} 실제={got}  ({why})")
    if not ok:
        print("\n★ 탐지력 검증 실패 — 아래 '0건'은 무효다."); return 2

    print("\n=== 1. 런타임 트리 전수 스캔 ===")
    off = scan()
    print(f"  스캔 대상 파일 {sum(1 for dp,_,fns in os.walk(ROOT) if '/Tests/' not in dp.replace(chr(92),'/')+'/' for f in fns if f.endswith('.cs'))}개")
    print(f"  위반 {len(off)}건")
    for o in off: print("   ", o)

    print("\n=== 2. 공허함 방지 — 단일 정의처에는 글리프가 실제로 있는가 ===")
    src = open(SINGLE, encoding="utf-8").read()
    hits = sum(1 for line in src.split("\n") if glyph_in_literal(line))
    print(f"  ShortcutLabel.cs 안의 리터럴 글리프 {hits}건 "
          f"({'OK — 스캐너가 눈이 멀지 않았다' if hits > 0 else '★FAIL — 스캐너가 아무것도 못 본다'})")
    return 0 if (not off and hits > 0) else 1

sys.exit(main())
