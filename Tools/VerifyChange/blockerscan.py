#!/usr/bin/env python3
# verify-change 독립 재구현 — SuspendClickBlockerAuditTests 의 스캐너를 C#이 아니라 파이썬으로
# 다시 쓴다(같은 코드를 다시 돌리는 것은 검증이 아니다).
import os, sys

NEEDLE = "new GameObject("

def creates_blocker(src, markers):
    i = 0
    while True:
        i = src.find(NEEDLE, i)
        if i < 0: return False
        start = i + len(NEEDLE)
        end = src.find("\n", start)
        if end < 0: end = len(src)
        arg = src[start:end]
        for m in markers:
            if m in arg: return True
        i = start

def polls_panel(src):  return "ArePanelsSuppressed" in src
def polls_any(src):    return ("IsSuspended" in src) or ("ArePanelsSuppressed" in src)

def scan(root, markers):
    out = []
    for dirpath, _, files in os.walk(root):
        for f in sorted(files):
            if not f.endswith(".cs"): continue
            p = os.path.join(dirpath, f)
            src = open(p, encoding="utf-8").read()
            if creates_blocker(src, markers):
                out.append((f, polls_panel(src), polls_any(src)))
    return sorted(out)

if __name__ == "__main__":
    root = sys.argv[1]
    print("=== CreatesFullRectBlocker (Blocker 만) — 등급1 검사 대상 ===")
    for f, panel, any_ in scan(root, ["Blocker"]):
        print(f"  {f:38s} ArePanelsSuppressed={panel}  any={any_}")
    print("=== CreatesClickBlocker (Blocker+ClickTarget) — 등급2 검사 대상 ===")
    for f, panel, any_ in scan(root, ["Blocker", "ClickTarget"]):
        print(f"  {f:38s} any={any_}")
